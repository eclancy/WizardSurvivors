using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Shared script for simple "AoE pulse" offensive spells (issue #13 roster expansion): Solar Flare,
// Toxic Spore Burst. Behaves like ArcaneExplosion (auto-repeating pulse around the caster, one
// persistent instance per equipped spell) plus an optional guaranteed status effect applied to
// everything caught in the blast.
public partial class ElementalPulse : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;
	[Export] public float TotalLifetime = 0f; // 0 = infinite (persistent child of Player)

	[Export] public bool GuaranteedPoison { get; set; } = false;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;

	private int damage = 5;
	private float range = 100f;
	private float cooldown = 1.5f;
	private float lifeElapsed = 0f;
	private float fireTimer = 0f;
	private GpuParticles2D particles;
	private PlaceholderShape visual;

	public override void _Ready()
	{
		RefreshComputedStats();
		particles = GetNodeOrNull<GpuParticles2D>("Particles");
		visual = GetNodeOrNull<PlaceholderShape>("PlaceholderShape");
		TriggerPulse();
		fireTimer = 0f;
	}

	public override void _Process(double delta)
	{
		if (PlayerRef != null)
			GlobalPosition = PlayerRef.GlobalPosition;

		if (TotalLifetime > 0f)
		{
			lifeElapsed += (float)delta;
			if (lifeElapsed >= TotalLifetime)
			{
				QueueFree();
				return;
			}
		}

		fireTimer += (float)delta;
		if (fireTimer >= cooldown)
		{
			TriggerPulse();
			fireTimer = 0f;
		}
	}

	private void TriggerPulse()
	{
		var parent = GetTree().CurrentScene;
		if (parent == null) return;
		var enemies = parent.GetChildren().OfType<Node2D>().Where(n => n.IsInGroup("enemies"));
		var player = PlayerRef as Player;
		foreach (var e in enemies)
		{
			if (GlobalPosition.DistanceTo(e.GlobalPosition) <= range)
			{
				player?.DealDamageToEnemy(e, damage);
				if (GuaranteedPoison && e.HasMethod("ApplyPoison"))
					e.Call("ApplyPoison", PoisonDamagePerTick, PoisonDuration);
			}
		}

		if (particles != null)
			particles.Restart();

		if (visual != null)
			visual.Radius = MathF.Max(4f, range * 0.9f);
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 5) * DamageMultiplier));
		range = (SpellData?.GetRangeAtLevel(CurrentLevel) ?? 100f) * AreaMultiplier;
		cooldown = MathF.Max(0.05f, (SpellData?.GetCooldownAtLevel(CurrentLevel) ?? 1.5f) * CooldownMultiplier);
	}
}
