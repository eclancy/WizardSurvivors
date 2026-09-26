using Godot;
using System;

namespace WizardSurvivors.scripts;

// A patch of ground that hurts things standing on it, for a while, and then stops.
//
// Deliberately generic and deliberately dumb: it does not move, aim, seek or expire early. It is
// dropped somewhere by something else, it ticks, it fades. Mirefoot drops a line of them behind the
// player; anything later that wants to leave something on the floor should use this rather than
// writing a third variant of the same twenty lines.
//
// It draws itself, like Cinderbreath and for the same reason - the shape that is drawn is computed
// from the same radius that is tested, so the tell cannot lie about its reach.
public partial class LingeringZone : Node2D
{
	[Export] public float Radius { get; set; } = 46f;
	[Export] public float Duration { get; set; } = 4.0f;
	[Export] public float TickSeconds { get; set; } = 0.5f;

	[Export] public bool AppliesSlow { get; set; } = true;
	[Export] public float SlowMultiplier { get; set; } = 0.6f;

	[Export] public Color FillColor { get; set; } = new Color(0.20f, 0.38f, 0.30f, 0.46f);
	[Export] public Color EdgeColor { get; set; } = new Color(0.44f, 0.62f, 0.38f, 0.62f);

	public int DamagePerTick { get; set; } = 2;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private float age = 0f;
	private float tickTimer = 0f;

	// The outline is randomised once, at birth, and then never recomputed. Two reasons: a pool
	// whose edge crawls every frame reads as a liquid simulation nobody asked for, and a trail of
	// eight identical circles reads as a stamp tool. Fixed-but-different is what puddles look like.
	private const int EdgePoints = 14;
	private readonly Vector2[] outline = new Vector2[EdgePoints];

	public override void _Ready()
	{
		ZIndex = 1;

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < EdgePoints; i++)
		{
			float angle = Mathf.Tau * i / EdgePoints;
			float wobble = rng.RandfRange(0.82f, 1.14f);
			outline[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Radius * wobble;
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;

		tickTimer += dt;
		if (tickTimer >= TickSeconds)
		{
			tickTimer = 0f;
			ApplyToOccupants();
		}

		QueueRedraw();

		if (age >= Duration)
			QueueFree();
	}

	private void ApplyToOccupants()
	{
		if (PlayerRef == null)
			return;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > Radius)
				continue;

			if (DamagePerTick > 0 && node.HasMethod("TakeDamage"))
				PlayerRef.DealDamageToEnemy(node, DamagePerTick, source: SpellData);

			// Re-applied every tick rather than once on entry, so the slow lasts exactly as long as
			// the enemy stays in the pool and wears off shortly after it leaves. Applying it once
			// with a long duration would let an enemy clip the edge and stay slowed across the map.
			if (AppliesSlow && node.HasMethod("ApplySlow"))
				node.Call("ApplySlow", SlowMultiplier, TickSeconds * 1.6f);
		}
	}

	public override void _Draw()
	{
		// Fades over the last third rather than the whole life, so for most of its duration it looks
		// like a hazard rather than like something already gone.
		float remaining = 1f - Mathf.Clamp(age / MathF.Max(0.01f, Duration), 0f, 1f);
		float fade = remaining < 0.34f ? remaining / 0.34f : 1f;

		DrawColoredPolygon(outline, FillColor with { A = FillColor.A * fade });
		DrawPolyline(outline, EdgeColor with { A = EdgeColor.A * fade }, 2.0f);
		// Closing segment - DrawPolyline leaves the loop open.
		DrawLine(outline[EdgePoints - 1], outline[0], EdgeColor with { A = EdgeColor.A * fade }, 2.0f);
	}
}
