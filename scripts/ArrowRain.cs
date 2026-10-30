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
///
/// Pixel art (2026-10-29): every arrow was stroked lines and polygons over a translucent outline. Each is
/// now one quad of scenes/shaders/hunters_arrow.gdshader (mode B) - the arrow the bow looses, a Metal
/// head and a Wind shaft on the world's 2 px grid, backed by an art pixel of occlusion - laid round its
/// point with its heading, how much of it is in the ground and how much of it is left in its vertex
/// colour. A falling arrow's point comes down exactly on the spot it strikes, and stays buried there.
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
	private float age;
	private Vector2 fallDirection;

	// The rain's arrows: shorter than the one on the bow, since a falling arrow is seen end-on-ish.
	private const float RainArrowLength = 34f;
	// How deep a standing arrow is in the ground: its head and a little of its shaft.
	private const float StandBury = 14f;
	private static Shader arrowShader;
	private static readonly StringName ParamWorldQuads = "world_quads";
	private static readonly StringName ParamArrowLength = "arrow_length";
	private static readonly StringName ParamGilded = "gilded";
	private ShaderMaterial material;
	private bool configured;
	private readonly Vector2[] quad = new Vector2[4];
	private readonly Color[] quadColours = new Color[4];
	private readonly Vector2[] quadUv = new Vector2[4];

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
		arrowShader ??= GD.Load<Shader>("res://scenes/shaders/hunters_arrow.gdshader");
		material = new ShaderMaterial { Shader = arrowShader };
		Material = material;
		material.SetShaderParameter(ParamWorldQuads, true);
		material.SetShaderParameter(ParamArrowLength, RainArrowLength);
	}

	public override void _Process(double delta)
	{
		// The spell is handed over after Loose returns. The arrows paint themselves, so the multiply tint
		// the caster gives the node is dropped (it would land in the arrows' vertex colours, which carry
		// their numbers); a Legendary's gold is the shader's own.
		if (!configured)
		{
			configured = true;
			Modulate = Colors.White;
			material.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
		}
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
		// Not before the tint is dropped: it would be multiplied into the arrows' numbers.
		if (!configured)
			return;
		Transform2D toLocal = GlobalTransform.AffineInverse();

		// The volley going up: the arrows leave the bow straight up and out of sight.
		if (age < BoltEvolutions.RainRiseSeconds)
		{
			float t = age / BoltEvolutions.RainRiseSeconds;
			for (int i = 0; i < climbing; i++)
			{
				float x = (i - (climbing - 1) * 0.5f) * 7f;
				Vector2 point = origin + new Vector2(x, -44f - ClimbSpeed * age);
				DrawArrow(toLocal, point, Vector2.Up, 1f - t * t, 0f);
			}
		}

		for (int i = 0; i < drops; i++)
		{
			float untilLanding = landAt[i] - age;
			if (untilLanding > FallSeconds)
				continue;
			if (untilLanding > 0f)
			{
				// Coming down: from up and behind, its point onto its spot.
				float p = 1f - untilLanding / FallSeconds;
				DrawArrow(toLocal, land[i] + FallFrom * (1f - p), fallDirection, 1f, 0f);
			}
			else if (-untilLanding < StandSeconds)
			{
				// Standing in the ground where it struck, its head buried, dissolving.
				float left = 1f - (-untilLanding) / StandSeconds;
				DrawArrow(toLocal, land[i], fallDirection, left, StandBury);
			}
		}
	}

	// One arrow's quad, laid round its point in world space (hunters_arrow.gdshader, mode B).
	private void DrawArrow(Transform2D toLocal, Vector2 point, Vector2 heading, float left, float bury)
	{
		float half = RainArrowLength + 8f;
		Color data = new Color(Mathf.PosMod(heading.Angle(), Mathf.Tau) / Mathf.Tau, bury / 64f, Mathf.Clamp(left, 0f, 1f), 0f);
		quadColours[0] = quadColours[1] = quadColours[2] = quadColours[3] = data;
		quadUv[0] = new Vector2(-half, -half);
		quadUv[1] = new Vector2(half, -half);
		quadUv[2] = new Vector2(half, half);
		quadUv[3] = new Vector2(-half, half);
		for (int v = 0; v < 4; v++)
			quad[v] = toLocal * (point + quadUv[v]);
		DrawPrimitive(quad, quadColours, quadUv);
	}
}
