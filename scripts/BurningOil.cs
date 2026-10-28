using Godot;
using WizardSurvivors.scripts;

// The burning oil the lantern-tender spills (LanternTenderEnemy, .ai/side-events.md): a wedge of the
// ground in front of it, alight for a few seconds after the splash has landed.
//
// It is the one thing in the fight that is still there after it has hit. Every other enemy attack in
// the game lands and is gone, so the ground a fight has already happened on is as safe as any other;
// this makes the player unable to stand where the tender last swung, which is the "enemies that leave
// hazard behind" gap .ai/enemy-behaviour.md section 10 names. It is StageHazard's job - hurt whoever
// stands in it, on an interval, never an enemy - in a shape an enemy chose.
//
// THE SHAPE IS THE SPLASH'S. It is built from the same reach, half-angle and facing as the cone the
// player was shown before the splash, at the tender's position on the frame it landed, and measured
// through GroundSlamAttack.Contains - the one authority on every telegraph shape - so the oil burns
// exactly the wedge that was marked and the flames drawn on it are laid out to that wedge's edge.
// Tested against the player's position, as the splash is, so the two never disagree about a pixel.
//
// A bare Node2D created in code and parented to the arena, so a tender killed while it burns does not
// take the fire with it.
public partial class BurningOil : Node2D
{
	public const string TexturePath = "res://assets/bonelight/effects/tender-oil.png";
	private const int Frames = 6;
	private const float Fps = 10f;
	// Where a tongue stands in its frame, in drawn pixels (swamp_tender.OIL_FOOT x2).
	private static readonly Vector2 TongueFoot = new(32f, 56f);
	private const float IgniteSeconds = 0.25f;
	private const float DieSeconds = 0.8f;
	// Spacing between tongues along an arc, in px. Close enough that the wedge reads as one fire.
	private const float TongueSpacing = 34f;

	private static Texture2D texture;
	// The slick under the flames: the deep end of stone, the darkest material there is above occ.
	private static readonly Color Slick = new(0.043f, 0.059f, 0.094f, 0.5f);

	private GroundSlamAttack shape;
	private float lifetime;
	private float tickSeconds;
	private int damage;
	private float age;
	private float burn;
	private Vector2[] tongues = System.Array.Empty<Vector2>();
	private int[] phases = System.Array.Empty<int>();
	private readonly Vector2[] wedge = new Vector2[18];
	private Node2D light;

	/// <summary>Bites landed on the player this process, for probes.</summary>
	public static int BitesLanded { get; private set; }

	/// <summary>
	/// Sets <paramref name="reach"/> by <paramref name="halfAngleDegrees"/> of ground alight in front of
	/// <paramref name="origin"/>, along <paramref name="facing"/>, for <paramref name="seconds"/>.
	/// </summary>
	/// <param name="damagePerTick">Already through the spiller's ScaleOutgoingDamage.</param>
	public static BurningOil Spill(Node arena, Vector2 origin, Vector2 facing, float reach, float halfAngleDegrees,
		float seconds, float tickSeconds, int damagePerTick)
	{
		if (arena == null || !IsInstanceValid(arena))
			return null;

		var oil = new BurningOil
		{
			lifetime = Mathf.Max(0.5f, seconds),
			tickSeconds = Mathf.Max(0.1f, tickSeconds),
			damage = damagePerTick,
			Name = "BurningOil",
		};
		// The cone is the shape; its clock is never run. Contains is all that is asked of it.
		oil.shape = new GroundSlamAttack(1f, 1f, reach, damagePerTick, LanternFireStrike.FlameMid)
		{
			Shape = TelegraphShape.Cone,
			HalfAngleDegrees = halfAngleDegrees,
			Facing = facing,
		};
		oil.LayOut();
		arena.AddChild(oil);
		oil.GlobalPosition = origin;
		return oil;
	}

	// Where the tongues stand: arcs across the wedge from near the spiller out to its rim, the last arc
	// ON the rim, so the furthest fire is the edge of what burns. Fixed for the patch's life, so the
	// fire does not crawl.
	private void LayOut()
	{
		var points = new System.Collections.Generic.List<Vector2>();
		var offsets = new System.Collections.Generic.List<int>();
		float half = Mathf.DegToRad(shape.HalfAngleDegrees);
		float centre = shape.Facing.Angle();
		int arcs = Mathf.Max(2, Mathf.RoundToInt(shape.Radius / 48f));
		for (int a = 1; a <= arcs; a++)
		{
			float d = shape.Radius * (a == arcs ? 0.96f : (a - 0.35f) / arcs);
			int count = Mathf.Max(1, Mathf.RoundToInt(d * half * 2f / TongueSpacing) + 1);
			for (int k = 0; k < count; k++)
			{
				// A little jitter, fixed per tongue, so the field is not a lattice.
				float jitter = ((k * 37 + a * 11) % 7 - 3) * 0.012f;
				float angle = count == 1 ? centre : centre - half * 0.94f + (half * 1.88f) * k / (count - 1) + jitter;
				points.Add(Vector2.FromAngle(angle) * d);
				offsets.Add((k * 5 + a * 3) % Frames);
			}
		}
		// Far tongues first, so the nearer (lower on screen) stand in front of them.
		var order = new int[points.Count];
		for (int i = 0; i < order.Length; i++)
			order[i] = i;
		System.Array.Sort(order, (x, y) => points[x].Y.CompareTo(points[y].Y));
		tongues = new Vector2[order.Length];
		phases = new int[order.Length];
		for (int i = 0; i < order.Length; i++)
		{
			tongues[i] = points[order[i]];
			phases[i] = offsets[order[i]];
		}

		// The wedge outline, for the slick under the fire.
		wedge[0] = Vector2.Zero;
		for (int i = 0; i < wedge.Length - 1; i++)
		{
			float angle = centre - half + half * 2f * i / (wedge.Length - 2);
			wedge[i + 1] = Vector2.FromAngle(angle) * shape.Radius;
		}
	}

	public override void _Ready()
	{
		texture ??= GD.Load<Texture2D>(TexturePath);
		// Under the actors and over the floor, like StageHazard: a burning patch never hides a body.
		ZIndex = -1;
		light = new Node2D { Name = "Light", ZIndex = 9, Material = ExploreArt.Additive, TextureFilter = TextureFilterEnum.Linear };
		light.Draw += OnLightDraw;
		AddChild(light);
	}

	public override void _ExitTree()
	{
		if (light != null && IsInstanceValid(light))
			light.Draw -= OnLightDraw;
	}

	/// <summary>Whether <paramref name="world"/> is in the fire. The splash's own shape.</summary>
	public bool Burns(Vector2 world) => shape.Contains(GlobalPosition, world);

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		light?.QueueRedraw();
		if (age >= lifetime)
		{
			QueueFree();
			return;
		}

		// StageHazard's rule: the clock only runs while he is in it, and a bite resets it - so walking
		// through costs a bite only if he lingers, and standing in it costs one every interval.
		Player player = Node2DGame.Current?.RunPlayer;
		if (player == null || player.IsDead || !Burns(player.GlobalPosition))
			return;
		burn += dt;
		if (burn < tickSeconds || damage <= 0)
			return;
		burn = 0f;
		player.TakeDamage(damage);
		BitesLanded++;
	}

	private float Strength => Mathf.Min(Mathf.Clamp(age / IgniteSeconds, 0f, 1f),
		Mathf.Clamp((lifetime - age) / DieSeconds, 0f, 1f));

	public override void _Draw()
	{
		float strength = Strength;
		if (strength <= 0f)
			return;
		DrawColoredPolygon(wedge, new Color(Slick, Slick.A * strength));
		if (texture == null)
			return;
		float cell = texture.GetHeight();
		int baseFrame = (int)(age * Fps);
		var tint = new Color(1f, 1f, 1f, strength);
		for (int i = 0; i < tongues.Length; i++)
		{
			int frame = (baseFrame + phases[i]) % Frames;
			DrawTextureRectRegion(texture, new Rect2(tongues[i] - TongueFoot, new Vector2(cell, cell)),
				new Rect2(frame * cell, 0f, cell, cell), tint);
		}
	}

	private void OnLightDraw()
	{
		float strength = Strength * (0.85f + 0.15f * Mathf.Sin(age * 13f));
		if (strength <= 0f)
			return;
		Vector2 mid = shape.Facing.Normalized() * shape.Radius * 0.55f;
		ExploreArt.DrawGlow(light, mid, shape.Radius * 0.9f, new Color(LanternFireStrike.FlameMid, 0.22f * strength));
		ExploreArt.DrawGlow(light, mid, shape.Radius * 0.4f, new Color(LanternFireStrike.FlameHot, 0.18f * strength));
	}
}
