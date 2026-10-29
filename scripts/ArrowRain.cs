using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Hunter's Draw's Rain of Arrows (.ai/spell-roster.md 2e): the volley loosed straight up, coming down
/// a moment later as a field of arrows over the pack nearest where the wizard stopped.
/// </summary>
/// <remarks>
/// The volley's arrows climb out of the bow and are gone off the top of the view; then RainDropsPerArrow
/// arrows for each of them fall, one after another over RainSpreadSeconds, each on its own spot in a
/// disc of RainRadius (times the player's Area) round the pack. An arrow strikes what stands at its spot
/// - RainHitRadius and a body, against enemy centres, as every zone is measured - throws its debris out
/// to exactly that, and stands in the ground a moment before it fades. The disc is never drawn (section
/// 2c): the arrows coming down are the field.
///
/// One node per volley, with every drop decided at birth into fixed arrays, so nothing is allocated while
/// it falls. In the air (DrawLayers.Air): it is arrows in flight over the bodies.
/// </remarks>
public partial class ArrowRain : Node2D
{
	private const int MaxDrops = 96;
	private const float FallSeconds = 0.22f;
	private const float StandSeconds = 0.45f;
	private const float ClimbSpeed = 950f;
	// The fall leans a little, so the arrows read as falling rather than as lines drawn on the floor.
	private static readonly Vector2 FallFrom = new Vector2(-46f, -230f);

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }
	public int DropDamage { get; set; } = 1;
	public float StaggerSeconds { get; set; }
	public bool Barbed { get; set; }

	private Vector2 origin;
	private Vector2 centre;
	private float radius;
	private int climbing;
	private int drops;
	private readonly Vector2[] land = new Vector2[MaxDrops];
	private readonly float[] landAt = new float[MaxDrops];
	private readonly bool[] struck = new bool[MaxDrops];
	private readonly Vector2[] fletch = new Vector2[3];
	private readonly Vector2[] head = new Vector2[3];
	private float age;
	private Vector2 fallDirection;

	private static readonly Color Outline = new Color(0.02f, 0.03f, 0.05f, 0.9f);
	private static readonly Color Shaft = new Color(0.66f, 0.93f, 1.0f, 1f);

	/// <summary>
	/// A rain from a volley of <paramref name="arrows"/> loosed at <paramref name="from"/>, falling over
	/// <paramref name="over"/> within <paramref name="fieldRadius"/>.
	/// </summary>
	public static ArrowRain Loose(Node2D arena, Vector2 from, Vector2 over, float fieldRadius, int arrows)
	{
		var rain = new ArrowRain();
		rain.origin = from;
		rain.centre = over;
		rain.radius = MathF.Max(20f, fieldRadius);
		rain.climbing = Math.Clamp(arrows, 1, 12);
		rain.drops = Math.Clamp(arrows * BoltEvolutions.RainDropsPerArrow, 1, MaxDrops);
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < rain.drops; i++)
		{
			// Uniform over the disc, and in a shuffled order so the field fills all over at once.
			float r = rain.radius * MathF.Sqrt(rng.Randf());
			float a = rng.Randf() * Mathf.Tau;
			rain.land[i] = over + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
			rain.landAt[i] = BoltEvolutions.RainRiseSeconds + rng.Randf() * BoltEvolutions.RainSpreadSeconds;
		}
		rain.fallDirection = (-FallFrom).Normalized();
		rain.Name = "ArrowRain";
		arena.AddChild(rain);
		return rain;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
	}

	public override void _Process(double delta)
	{
		age += (float)delta;
		for (int i = 0; i < drops; i++)
		{
			if (!struck[i] && age >= landAt[i])
			{
				struck[i] = true;
				Strike(land[i]);
			}
		}
		QueueRedraw();
		if (age >= BoltEvolutions.RainRiseSeconds + BoltEvolutions.RainSpreadSeconds + StandSeconds + 0.05f)
			QueueFree();
	}

	private void Strike(Vector2 at)
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		float reach = BoltEvolutions.RainHitRadius + RiptideWave.BodyAllowance;
		Enemy first = null;
		float firstD = float.MaxValue;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			float d = at.DistanceSquaredTo(enemy.GlobalPosition);
			if (d > reach * reach)
				continue;
			PlayerRef.DealDamageToEnemy(enemy, DropDamage, source: SpellData);
			if (StaggerSeconds > 0f && !enemy.IsDying)
				enemy.ApplyShock(StaggerSeconds);
			if (d < firstD && !enemy.IsDying) { first = enemy; firstD = d; }
		}
		// Barbed: the arrow stays in what it came down on.
		if (Barbed && first != null && GetParent() is Node2D arena)
			LodgedArrows.Lodge(arena, first, fallDirection, DropDamage, SpellData, PlayerRef);
		if (GetParent() is Node2D world)
			ImpactDebrisLayer.Spawn(world, at, BoltEvolutions.RainHitRadius, ElementColors.GetRamp(Element.Wind), Colors.White);
	}

	public override void _Draw()
	{
		DrawSetTransformMatrix(GlobalTransform.AffineInverse());

		// The volley going up: the arrows leave the bow straight up and out of sight.
		if (age < BoltEvolutions.RainRiseSeconds)
		{
			float t = age / BoltEvolutions.RainRiseSeconds;
			for (int i = 0; i < climbing; i++)
			{
				float x = (i - (climbing - 1) * 0.5f) * 7f;
				Vector2 tip = origin + new Vector2(x, -36f - ClimbSpeed * age);
				DrawArrow(tip, Vector2.Up, 1f - t * t);
			}
		}

		for (int i = 0; i < drops; i++)
		{
			float untilLanding = landAt[i] - age;
			if (untilLanding > FallSeconds)
				continue;
			if (untilLanding > 0f)
			{
				// Coming down: from up and behind, onto its spot.
				float p = 1f - untilLanding / FallSeconds;
				Vector2 tip = land[i] + FallFrom * (1f - p);
				DrawArrow(tip, fallDirection, 1f);
			}
			else if (-untilLanding < StandSeconds)
			{
				// Standing in the ground, half buried, fading.
				float fade = 1f - (-untilLanding) / StandSeconds;
				DrawArrow(land[i] + fallDirection * 6f, fallDirection, fade, 0.55f);
			}
		}
	}

	private void DrawArrow(Vector2 tip, Vector2 heading, float alpha, float showLength = 1f)
	{
		const float length = 26f;
		Vector2 tail = tip - heading * length * showLength;
		Vector2 across = heading.Orthogonal();
		Color outline = Outline with { A = Outline.A * alpha };
		DrawLine(tip, tail - heading * 2f, outline, 5f);
		DrawLine(tip, tail, Shaft with { A = alpha }, 2.4f);
		// The fletching sweeps back from the shaft, so its point runs the way the arrow flies. Drawn the
		// other way round (as it first was) it is a second head at the tail, and in the first post-merge
		// screenshot every arrow coming down on the pack read as one going up.
		fletch[0] = tail + across * 4.5f;
		fletch[1] = tail + heading * 7f;
		fletch[2] = tail - across * 4.5f;
		DrawPolyline(fletch, outline, 4f);
		DrawPolyline(fletch, Shaft.Lightened(0.2f) with { A = alpha }, 2f);
		if (showLength < 1f)
			return;
		// The head: the one part that says which way it is going, so it is solid and outlined.
		head[0] = tip + heading * 7.5f;
		head[1] = tip - heading * 5.5f + across * 6.5f;
		head[2] = tip - heading * 5.5f - across * 6.5f;
		DrawColoredPolygon(head, outline);
		head[0] = tip + heading * 5f;
		head[1] = tip - heading * 4f + across * 4.5f;
		head[2] = tip - heading * 4f - across * 4.5f;
		DrawColoredPolygon(head, Shaft.Lightened(0.35f) with { A = alpha });
	}
}
