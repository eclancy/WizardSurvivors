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
//
// **Pixel art (2026-10-29).** The lane was a translucent blue rectangle with translucent dashes stroked
// down it, turned to the heading: a wash on a rotated grid. It is now one quad drawn by
// scenes/shaders/spring_tide_lane.gdshader, which snaps every fragment to the world's 2 px cell before it
// measures it in the lane's frame - shallow water in opaque Water and azure tones with the floor showing
// between its cells, foam streaks running down it at DragSpeed, a lip of foam at the front and a Bayer
// dissolve where it drains. The shader is handed FrontX, TailX and HalfWidth, the drag's own numbers.
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

	private static Shader laneShader;
	private static readonly StringName ParamFront = "front";
	private static readonly StringName ParamTail = "tail";
	private static readonly StringName ParamHalfWidth = "half_width";
	private static readonly StringName ParamDrainLength = "drain_length";
	private static readonly StringName ParamFadeAll = "fade_all";
	private static readonly StringName ParamDragSpeed = "drag_speed";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamGilded = "gilded";
	private ShaderMaterial material;

	// Seconds the wave takes to cross the lane; zero when the lane fills at once.
	private float FillSeconds => FloodSpeed > 0f ? Length / FloodSpeed : 0f;

	// The leading edge of the water, along the heading from the caster.
	private float FrontX => FloodSpeed > 0f ? Mathf.Min(Length, age * FloodSpeed) : Length * Mathf.Clamp(age / 0.3f, 0f, 1f);

	// Behind this the water has drained: each stretch lasts LifeSeconds from when the wave crossed it.
	private float TailX => FloodSpeed > 0f ? Mathf.Max(0f, (age - LifeSeconds) * FloodSpeed) : 0f;

	public override void _Ready()
	{
		// On the ground, under every actor: the ground layer the craters use (absolute -15, over the
		// terrain at -100 and the walkable decals at -20). At 1 the water was painted over the wizard
		// wading in it (2026-10-28, "should not cover other elements that would stand on top of the
		// ground, like the player").
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
		Rotation = Heading.Angle();

		laneShader ??= GD.Load<Shader>("res://scenes/shaders/spring_tide_lane.gdshader");
		material = new ShaderMaterial { Shader = laneShader };
		Material = material;
		material.SetShaderParameter(ParamHalfWidth, HalfWidth);
		material.SetShaderParameter(ParamDragSpeed, DragSpeed);
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
		material.SetShaderParameter(ParamDrainLength, FloodSpeed > 0f ? DrainSeconds * FloodSpeed : 0f);
		UpdateWater();
	}

	// The numbers the drag reads, handed to the shader: where the front and the drained tail are now.
	private void UpdateWater()
	{
		material.SetShaderParameter(ParamFront, FrontX);
		material.SetShaderParameter(ParamTail, TailX);
		material.SetShaderParameter(ParamFadeAll, FloodSpeed > 0f ? 1f : Mathf.Clamp((LifeSeconds - age) / DrainSeconds, 0f, 1f));
		material.SetShaderParameter(ParamTime, age);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		UpdateWater();
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
		foreach (Node node in EnemyRegistry.Snapshot)
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

	// One quad over the whole run of the lane, drawn once: the shader shows only the water between the
	// drained tail and the front, so the quad never has to follow them.
	public override void _Draw()
	{
		DrawRect(new Rect2(-WhipLimb.Grid * 2f, -HalfWidth - WhipLimb.Grid * 2f, Length + WhipLimb.Grid * 4f, HalfWidth * 2f + WhipLimb.Grid * 4f), Colors.White);
	}
}
