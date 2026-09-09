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

		var parent = GetTree().CurrentScene;
		var enemies = parent?.GetChildren().OfType<Node2D>().Where(n => n.IsInGroup("enemies")) ?? Enumerable.Empty<Node2D>();
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
