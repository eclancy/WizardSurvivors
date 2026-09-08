using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

// Black Tentacles (Poison + Earth, issue #28): summons a stationary AoE zone at the target/impact
// point for a duration; enemies inside are slowed and take repeated damage ticks. New "placed
// zone" archetype - a targeted offensive cast rather than a self-centered passive pulse.
public partial class BlackTentacles : Node2D
{
	private const int TentaclesBackZIndex = -50;

	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ZoneDuration { get; set; } = 3.0f;
	[Export] public float TickInterval { get; set; } = 0.5f;
	[Export] public float BaseRadius { get; set; } = 90f;
	[Export] public float PulseSpeed { get; set; } = 4.8f;
	[Export] public float PulseStrength { get; set; } = 0.07f;
	[Export] public float SlowMultiplier { get; set; } = 0.45f;
	[Export] public int WiggleLineCount { get; set; } = 7;
	[Export] public float WiggleWidth { get; set; } = 2.5f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 3;
	private float radius = 55f;
	private float duration = 3.0f;
	private float elapsed = 0f;
	private float tickTimer = 0f;
	private float visualTime = 0f;
	private float scaledTickInterval = 0.5f;
	private float scaledSlowDuration = 1.2f;
	private readonly HashSet<Node2D> enemiesInside = new HashSet<Node2D>();

	public override void _Ready()
	{
		// Keep the zone visually underneath enemies regardless of insertion order.
		ZAsRelative = false;
		ZIndex = TentaclesBackZIndex;
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
		visualTime = 0f;
		enemiesInside.Clear();
		UpdateEnemiesInsideAndApplyOnEntry();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		elapsed += (float)delta;
		tickTimer += (float)delta;
		visualTime += (float)delta;
		UpdateEnemiesInsideAndApplyOnEntry();
		QueueRedraw();
		if (tickTimer >= scaledTickInterval)
		{
			tickTimer -= scaledTickInterval;
			Pulse();
		}
		if (elapsed >= duration)
			QueueFree();
	}

	public override void _Draw()
	{
		float pulse = 1f + Mathf.Sin(visualTime * PulseSpeed) * PulseStrength;
		float drawRadius = radius * pulse;

		DrawCircle(Vector2.Zero, drawRadius, new Color(0.03f, 0.03f, 0.04f, 0.66f));
		DrawArc(Vector2.Zero, drawRadius, 0f, Mathf.Tau, 48, new Color(0.17f, 0.17f, 0.19f, 0.72f), 2f, true);

		int lineCount = Math.Max(3, WiggleLineCount);
		for (int i = 0; i < lineCount; i++)
		{
			float baseAngle = (Mathf.Tau / lineCount) * i;
			DrawWiggle(baseAngle, drawRadius, i);
		}
	}

	private void DrawWiggle(float baseAngle, float drawRadius, int lineIndex)
	{
		const int pointsPerLine = 14;
		var points = new Vector2[pointsPerLine];
		float startR = drawRadius * 0.20f;
		float endR = drawRadius * 0.88f;
		float spread = drawRadius * 0.10f;
		float phase = visualTime * 5.2f + lineIndex * 0.73f;

		for (int j = 0; j < pointsPerLine; j++)
		{
			float t = j / (float)(pointsPerLine - 1);
			float r = Mathf.Lerp(startR, endR, t);
			float angleWave = Mathf.Sin((t * 10f) + phase) * 0.22f;
			Vector2 dir = new Vector2(Mathf.Cos(baseAngle + angleWave), Mathf.Sin(baseAngle + angleWave));
			Vector2 tangent = dir.Orthogonal();
			float lateral = Mathf.Sin((t * 15f) + phase * 1.2f) * spread * (1f - t * 0.4f);
			points[j] = dir * r + tangent * lateral;
		}

		DrawPolyline(points, new Color(0.22f, 0.22f, 0.24f, 0.82f), WiggleWidth, true);
	}

	private void Pulse()
	{
		var stale = new List<Node2D>();
		foreach (var e in enemiesInside)
		{
			if (e == null || !IsInstanceValid(e) || GlobalPosition.DistanceTo(e.GlobalPosition) > radius)
				stale.Add(e);
			else
				ApplyZoneEffects(e);
		}

		foreach (var e in stale)
			enemiesInside.Remove(e);
	}

	private void UpdateEnemiesInsideAndApplyOnEntry()
	{
		var currentInside = new HashSet<Node2D>();
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > radius)
				continue;

			currentInside.Add(enemy);
			if (!enemiesInside.Contains(enemy))
				ApplyZoneEffects(enemy);
		}

		enemiesInside.RemoveWhere(enemy => enemy == null || !IsInstanceValid(enemy) || !currentInside.Contains(enemy));
		foreach (var enemy in currentInside)
			enemiesInside.Add(enemy);
	}

	private void ApplyZoneEffects(Node2D enemy)
	{
		var player = PlayerRef as Player;
		player?.DealDamageToEnemy(enemy, damage, source: SpellData);
		if (enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", SlowMultiplier, scaledSlowDuration);
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 2) * DamageMultiplier));
		radius = MathF.Max(10f, BaseRadius * AreaMultiplier);
		duration = MathF.Max(0.5f, (ZoneDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f)) * DurationMultiplier);
		scaledTickInterval = MathF.Max(0.08f, TickInterval * MathF.Max(0.01f, CooldownMultiplier));
		scaledSlowDuration = MathF.Max(0.1f, scaledTickInterval + 0.2f + (SpellData?.GetEffectValueAtLevel(SpellEffect.RootDuration, CurrentLevel) ?? 0f));
		QueueRedraw();
	}
}
