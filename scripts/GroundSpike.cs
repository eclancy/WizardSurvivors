using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Obsidian Spike (Earth + Darkness, issue #13): erupts a spike of black stone beneath the nearest
// enemy after a short telegraph delay, dealing AoE damage at that spot. Placeholder art: an
// expanding ring during the telegraph, then a burst of particles on eruption.
public partial class GroundSpike : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float TelegraphDuration { get; set; } = 0.5f;
	[Export] public float BaseExplosionRadius { get; set; } = 45f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 6;
	private float explosionRadius = 45f;
	private float telegraphTimer = 0f;
	private bool erupted = false;

	public override void _Ready()
	{
		RefreshComputedStats();
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
	}

	public override void _Process(double delta)
	{
		if (erupted)
			return;

		telegraphTimer += (float)delta;
		var visual = GetNodeOrNull<PlaceholderShape>("PlaceholderShape");
		if (visual != null)
		{
			float t = Mathf.Clamp(telegraphTimer / TelegraphDuration, 0f, 1f);
			visual.Radius = Mathf.Lerp(4f, explosionRadius, t);
			visual.Ring = true;
			visual.QueueRedraw();
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
				player?.DealDamageToEnemy(e, damage);
		}

		var burst = GetNodeOrNull<GpuParticles2D>("ExplosionParticles");
		if (burst != null)
		{
			burst.Emitting = true;
			burst.Restart();
		}

		var visual = GetNodeOrNull<PlaceholderShape>("PlaceholderShape");
		if (visual != null)
			visual.Visible = false;

		var timer = GetTree().CreateTimer(0.4);
		timer.Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 6) * DamageMultiplier));
		explosionRadius = MathF.Max(4f, BaseExplosionRadius * AreaMultiplier);
	}
}
