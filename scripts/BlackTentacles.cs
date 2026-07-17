using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Black Tentacles (Poison + Earth, issue #28): summons a stationary AoE zone at the target/impact
// point for a duration; enemies inside are rooted and take repeated damage ticks. New "placed
// zone" archetype - a targeted offensive cast rather than a self-centered passive pulse.
public partial class BlackTentacles : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ZoneDuration { get; set; } = 3.0f;
	[Export] public float TickInterval { get; set; } = 1.0f;
	[Export] public float BaseRadius { get; set; } = 55f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 3;
	private float radius = 55f;
	private float duration = 3.0f;
	private float elapsed = 0f;
	private float tickTimer = 0f;

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
		elapsed = 0f;
		tickTimer = 0f;

		var visual = GetNodeOrNull<PlaceholderShape>("PlaceholderShape");
		if (visual != null)
		{
			visual.Radius = radius;
			visual.QueueRedraw();
		}
	}

	public override void _Process(double delta)
	{
		elapsed += (float)delta;
		tickTimer += (float)delta;
		if (tickTimer >= TickInterval)
		{
			tickTimer = 0f;
			Pulse();
		}
		if (elapsed >= duration)
			QueueFree();
	}

	private void Pulse()
	{
		var parent = GetTree().CurrentScene;
		var enemies = parent?.GetChildren().OfType<Node2D>().Where(n => n.IsInGroup("enemies")) ?? Enumerable.Empty<Node2D>();
		var player = PlayerRef as Player;
		foreach (var e in enemies)
		{
			if (GlobalPosition.DistanceTo(e.GlobalPosition) <= radius)
			{
				player?.DealDamageToEnemy(e, damage);
				if (e.HasMethod("ApplySlow"))
					e.Call("ApplySlow", 0f, TickInterval + 0.2f); // root, refreshed every tick
			}
		}
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 3) * DamageMultiplier));
		radius = MathF.Max(10f, BaseRadius * AreaMultiplier);
		duration = MathF.Max(0.5f, ZoneDuration * DurationMultiplier);
	}
}
