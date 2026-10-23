using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Spring Tide, Tituba's Apotheosis (.ai/decisions/2026-10-10-apotheosis.md). Verb: flood.
//
// Riptide already lines enemies up: a non-homing wave that pierces down a row. Its final form
// floods the ground it travels over. Each cast leaves a lane of water behind it for LifeSeconds;
// whatever stands in the lane is slowed and dragged along the wave's heading by the current. The
// player aims a line, and the water moves the swarm down it - into a wall, a Gravewell, a crater,
// or simply away.
//
// "Flood" was on #59's list of unclaimed verbs, against Water. The drag rides ApplyKnockback, so
// boss knockback resistance applies.
//
// **The water follows the wave.** The lane used to fill its whole length in 0.3 s, so it raced
// ahead of the wave that was meant to be leaving it behind - which stopped reading as "the wave
// floods the ground" the moment the wave became a visible crest (RiptideWave). With FloodSpeed set
// (the bolt's own speed) the front of the water keeps pace with the crest, and each stretch of lane
// stays flooded for LifeSeconds from when the wave crossed it, so every point is wet exactly as
// long as before. The hit test and the drawing both read FrontX/TailX, so the lane cannot be drawn
// anywhere it does not drag. FloodSpeed 0 keeps the old fill-at-once behaviour.
public partial class SpringTideLane : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	public float Length { get; set; } = 360f;
	public float HalfWidth { get; set; } = 40f;
	public Vector2 Heading { get; set; } = Vector2.Right;
	public float FloodSpeed { get; set; } = 0f;

	private const float LifeSeconds = 4f;
	private const float DragSpeed = 150f;
	private const float SlowMultiplier = 0.45f;
	private const float HitInterval = 0.6f;
	private const float DrainSeconds = 0.6f;

	private float age;
	private float tick;
	private readonly Dictionary<ulong, float> nextHitAt = new();

	private static readonly Color Water = new Color(0.25f, 0.50f, 0.90f, 0.28f);
	private static readonly Color Foam = new Color(0.80f, 0.92f, 1.0f, 0.75f);

	// Seconds the wave takes to cross the lane; zero when the lane fills at once.
	private float FillSeconds => FloodSpeed > 0f ? Length / FloodSpeed : 0f;

	// The leading edge of the water, along the heading from the caster.
	private float FrontX => FloodSpeed > 0f ? Mathf.Min(Length, age * FloodSpeed) : Length * Mathf.Clamp(age / 0.3f, 0f, 1f);

	// Behind this the water has drained: each stretch lasts LifeSeconds from when the wave crossed it.
	private float TailX => FloodSpeed > 0f ? Mathf.Max(0f, (age - LifeSeconds) * FloodSpeed) : 0f;

	public override void _Ready()
	{
		ZIndex = 1;
		Rotation = Heading.Angle();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		if (age >= LifeSeconds + FillSeconds)
		{
			QueueFree();
			return;
		}

		tick -= dt;
		if (tick > 0f) return;
		tick = 0.2f;
		if (PlayerRef == null || !IsInstanceValid(PlayerRef)) return;

		float front = FrontX;
		float tail = TailX;
		int damage = Mathf.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(SpellData.CurrentLevel) ?? 5) * 0.3f));
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy e || e.IsDying) continue;
			// In the lane's own frame: x along the heading from the caster, y across it.
			Vector2 local = ToLocal(e.GlobalPosition);
			if (local.X < tail || local.X > front || Mathf.Abs(local.Y) > HalfWidth) continue;

			e.ApplySlow(SlowMultiplier, 0.4f);
			e.ApplyKnockback(Heading * DragSpeed);
			ulong id = e.GetInstanceId();
			if (!nextHitAt.TryGetValue(id, out float readyAt) || age >= readyAt)
			{
				nextHitAt[id] = age + HitInterval;
				PlayerRef.DealDamageToEnemy(e, damage, source: SpellData);
			}
		}
	}

	public override void _Draw()
	{
		float front = FrontX;
		float tail = TailX;
		if (front <= tail)
			return;
		// The last DrainSeconds of each stretch fade, so the lane drains from the caster's end
		// outward in the order the wave flooded it. Without FloodSpeed the whole lane fades together.
		float drainLength = FloodSpeed > 0f ? DrainSeconds * FloodSpeed : 0f;
		float fadeAll = FloodSpeed > 0f ? 1f : Mathf.Clamp((LifeSeconds - age) / DrainSeconds, 0f, 1f);
		float solidFrom = Mathf.Min(front, tail + drainLength);

		if (drainLength > 0f && solidFrom > tail)
			DrawRect(new Rect2(tail, -HalfWidth, solidFrom - tail, HalfWidth * 2f), new Color(Water, Water.A * 0.45f));
		DrawRect(new Rect2(solidFrom, -HalfWidth, front - solidFrom, HalfWidth * 2f), new Color(Water, Water.A * fadeAll));

		// Foam lines running with the current, so the drag direction is visible.
		for (int k = 0; k < 4; k++)
		{
			float y = -HalfWidth + (k + 0.5f) * HalfWidth * 0.5f;
			float offset = (age * DragSpeed + k * 37f) % 60f;
			for (float x = offset; x < front; x += 60f)
			{
				if (x + 22f < tail)
					continue;
				float from = Mathf.Max(x, tail);
				float a = from < solidFrom ? 0.45f : fadeAll;
				DrawLine(new Vector2(from, y), new Vector2(Mathf.Min(x + 22f, front), y), new Color(Foam, Foam.A * a), 2f);
			}
		}
	}
}
