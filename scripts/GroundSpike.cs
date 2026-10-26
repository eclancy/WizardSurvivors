using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Obsidian Spike (Earth + Darkness, issue #13): erupts a spike of black stone beneath the nearest
// enemy after a short telegraph delay, dealing AoE damage at that spot. The world visual is a
// one-shot spike-sheet animation that grows out of the ground, peaks, then disintegrates.
public partial class GroundSpike : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float TelegraphDuration { get; set; } = 0.5f;
	[Export] public float BaseExplosionRadius { get; set; } = 45f;
	[Export] public int EruptionFrame { get; set; } = 4;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;
	// Basalt Lance (obsidian_spike_basalt_lance): the eruption drives on through the column behind its
	// target - LanceFollowers more spikes in a line away from the caster, each a beat after the last and
	// LanceSpacing radii apart, so the needle the card describes is something the player watches run
	// through a crowd. It was +140% damage on the same round eruption until 2026-10-26, which read as
	// nothing having changed at the Ascension. Followers are spikes like any other: same damage, radius
	// and sheet, and LanceStep > 0 marks one, so a follower never starts a lance of its own.
	public const string BasaltLanceId = "obsidian_spike_basalt_lance";
	public const int LanceFollowers = 3;
	private const float LanceSpacing = 1.6f;
	private const float LanceBeatSeconds = 0.09f;
	public int LanceStep { get; set; }

	private int damage = 6;
	private float explosionRadius = 45f;
	private float telegraphTimer = 0f;
	private bool erupted = false;
	private AnimatedSprite2D spikeAnimation;

	public override void _Ready()
	{
		RefreshComputedStats();
		spikeAnimation = GetNodeOrNull<AnimatedSprite2D>("SpikeAnimation");
		if (spikeAnimation != null)
			spikeAnimation.Connect("animation_finished", new Callable(this, nameof(OnSpikeAnimationFinished)));
		ResetVisualState();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void CastAt(Vector2 position)
	{
		GlobalPosition = position;
		telegraphTimer = 0f;
		erupted = false;
		ResetVisualState();
	}

	public override void _Process(double delta)
	{
		if (erupted)
			return;

		telegraphTimer += (float)delta;
		if (spikeAnimation != null)
		{
			if (spikeAnimation.Frame >= EruptionFrame)
			{
				Erupt();
				return;
			}
		}

		if (telegraphTimer >= TelegraphDuration)
			Erupt();
	}

	private void Erupt()
	{
		if (erupted)
			return;
		erupted = true;

		// The group, like every AoE (CLAUDE.md) - see the same fix in ConeBlast. CurrentScene's children
		// are the enemies only when the run IS the scene, so under the balance harness this hit nothing.
		var enemies = EnemyRegistry.Snapshot.OfType<Node2D>();
		var player = PlayerRef as Player;
		foreach (var e in enemies)
		{
			if (GlobalPosition.DistanceTo(e.GlobalPosition) <= explosionRadius)
				player?.DealDamageToEnemy(e, damage, source: SpellData);
		}

		var burst = GetNodeOrNull<GpuParticles2D>("ExplosionParticles");
		if (burst != null)
		{
			burst.Emitting = true;
			burst.Restart();
		}

		if (LanceStep == 0 && SpellData != null && SpellData.HasEvolution(BasaltLanceId))
			DriveLance();
	}

	private void DriveLance()
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef) || GetParent() is not Node parent || string.IsNullOrEmpty(SceneFilePath))
			return;
		Vector2 heading = GlobalPosition - PlayerRef.GlobalPosition;
		heading = heading.LengthSquared() > 0.01f ? heading.Normalized() : Vector2.Right;
		var scene = ResourceLoader.Load<PackedScene>(SceneFilePath);
		if (scene == null)
			return;
		Vector2 origin = GlobalPosition;
		// Captured now: the first spike frees itself when its sheet ends, before the last follower lands.
		SpellData spell = SpellData;
		Node2D caster = PlayerRef;
		float damageMultiplier = DamageMultiplier, areaMultiplier = AreaMultiplier;
		int level = CurrentLevel;
		Vector2 scale = Scale;
		Color modulate = Modulate;
		for (int step = 1; step <= LanceFollowers; step++)
		{
			Vector2 at = origin + heading * explosionRadius * LanceSpacing * step;
			int captured = step;
			var timer = GetTree().CreateTimer(LanceBeatSeconds * step);
			timer.Timeout += () =>
			{
				if (!IsInstanceValid(parent) || !IsInstanceValid(caster))
					return;
				var follower = scene.Instantiate<Node2D>();
				if (follower is GroundSpike spike)
				{
					spike.SpellData = spell;
					spike.DamageMultiplier = damageMultiplier;
					spike.AreaMultiplier = areaMultiplier;
					spike.LanceStep = captured;
					spike.SetSpellLevel(level);
					spike.PlayerRef = caster;
					spike.Scale = scale;
					spike.Modulate = modulate;
				}
				parent.AddChild(follower);
				(follower as GroundSpike)?.CastAt(at);
			};
		}
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 6) * DamageMultiplier));
		explosionRadius = MathF.Max(4f, BaseExplosionRadius * AreaMultiplier);
	}

	private void ResetVisualState()
	{
		if (spikeAnimation == null)
			return;

		spikeAnimation.Visible = true;
		spikeAnimation.Frame = 0;
		spikeAnimation.FrameProgress = 0f;
		spikeAnimation.Play("default");
	}

	private void OnSpikeAnimationFinished()
	{
		if (IsInstanceValid(this))
			QueueFree();
	}
}
