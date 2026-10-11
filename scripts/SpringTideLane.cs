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
public partial class SpringTideLane : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	public float Length { get; set; } = 360f;
	public float HalfWidth { get; set; } = 40f;
	public Vector2 Heading { get; set; } = Vector2.Right;

	private const float LifeSeconds = 4f;
	private const float DragSpeed = 150f;
	private const float SlowMultiplier = 0.45f;
	private const float HitInterval = 0.6f;

	private float age;
	private float tick;
	private readonly Dictionary<ulong, float> nextHitAt = new();

	private static readonly Color Water = new Color(0.25f, 0.50f, 0.90f, 0.28f);
	private static readonly Color Foam = new Color(0.80f, 0.92f, 1.0f, 0.75f);

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
		if (age >= LifeSeconds)
		{
			QueueFree();
			return;
		}

		tick -= dt;
		if (tick > 0f) return;
		tick = 0.2f;
		if (PlayerRef == null || !IsInstanceValid(PlayerRef)) return;

		int damage = Mathf.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(SpellData.CurrentLevel) ?? 5) * 0.3f));
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy e || e.IsDying) continue;
			// In the lane's own frame: x along the heading from the caster, y across it.
			Vector2 local = ToLocal(e.GlobalPosition);
			if (local.X < 0f || local.X > Length || Mathf.Abs(local.Y) > HalfWidth) continue;

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
		float grow = Mathf.Clamp(age / 0.3f, 0f, 1f);
		float fade = Mathf.Clamp((LifeSeconds - age) / 0.6f, 0f, 1f);
		float len = Length * grow;
		DrawRect(new Rect2(0f, -HalfWidth, len, HalfWidth * 2f), new Color(Water, Water.A * fade));
		// Foam lines running with the current, so the drag direction is visible.
		for (int k = 0; k < 4; k++)
		{
			float y = -HalfWidth + (k + 0.5f) * HalfWidth * 0.5f;
			float offset = (age * DragSpeed + k * 37f) % 60f;
			for (float x = offset; x < len; x += 60f)
				DrawLine(new Vector2(x, y), new Vector2(Mathf.Min(x + 22f, len), y), new Color(Foam, Foam.A * fade), 2f);
		}
	}
}
