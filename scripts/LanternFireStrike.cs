using Godot;
using WizardSurvivors.scripts;

// One fire strike the lantern-tender throws (LanternTenderEnemy, .ai/side-events.md): a gobbet of the
// lanterns' cold flame lobbed out of its lantern in a high arc, onto a spot marked on the ground for
// the whole of its flight, where it bursts into a column of fire.
//
// It is TelegraphedGroundHit with something falling into it. The warning is GroundSlamAttack's ring,
// drawn and measured from the same radius, primed so it is on the ground from the frame the flame
// leaves the lantern and resolved once, against the player, on the frame the flame lands - so a
// strike cannot claim a pixel it never marked. What this adds is the two halves of a strike the eye
// tracks: the flame in the air, which says where the hit is coming FROM and how long it has left
// (.ai/art-direction.md: a pre-hit warning is the falling thing itself), and the burst when it lands
// (tools/art/swamp_tender.py, tender-strike.png), whose ring of tongues stands on the radius it hit.
//
// A bare Node2D created in code and parented to the arena, not the tender, for TelegraphedGroundHit's
// reason: a tender killed mid-volley must not delete flames the player is already dodging. No
// collision and no held reference to the player - it is found through the group, once, on landing.
public partial class LanternFireStrike : Node2D
{
	// The art (tools/art/swamp_tender.py). Saved at x2, so drawn at scale 1.
	public const string StrikeTexturePath = "res://assets/bonelight/effects/tender-strike.png";
	public const string EmberTexturePath = "res://assets/bonelight/effects/tender-ember.png";
	private const int StrikeFrames = 8;
	private const int EmberFrames = 4;
	private const float StrikeFps = 14f;
	private const float EmberFps = 12f;
	// Where the struck ground is in a strike frame, in drawn pixels (swamp_tender.STRIKE_GROUND x2).
	private static readonly Vector2 StrikeGround = new(96f, 120f);

	// The poison ramp (.ai/art-direction.md section 3): the bog lanterns' cold flame.
	public static readonly Color FlameCore = new(0.973f, 1f, 0.847f);   // #F8FFD8
	public static readonly Color FlameHot = new(0.863f, 0.957f, 0.549f);  // #DCF48C
	public static readonly Color FlameMid = new(0.706f, 0.878f, 0.290f);  // #B4E04A

	private static Texture2D strikeTexture;
	private static Texture2D emberTexture;

	private GroundSlamAttack shape;
	private Vector2 from;
	private float delay;
	private float flight;
	private float arcHeight;
	private float clock;
	private bool thrown;
	private bool resolved;
	private float burstClock = -1f;
	private Node2D flightLayer;
	private Node2D ring;
	private Node2D light;

	/// <summary>Seconds the burst plays after the flame lands; the node frees itself after it.</summary>
	public static float BurstSeconds => StrikeFrames / StrikeFps;

	/// <summary>
	/// Throws a flame from <paramref name="from"/> onto <paramref name="to"/>, leaving after
	/// <paramref name="delaySeconds"/> (so a volley leaves the lantern one flame at a time) and landing
	/// <paramref name="flightSeconds"/> later. Nothing is marked on the ground until it leaves: the mark
	/// and the flame appear together, and the flight is the whole of the warning.
	/// </summary>
	/// <param name="damage">
	/// Already through the thrower's <see cref="Enemy.ScaleOutgoingDamage"/>: this node is not an enemy,
	/// so GroundSlamAttack deals what it is given (the TelegraphedGroundHit convention).
	/// </param>
	public static LanternFireStrike Throw(Node arena, Vector2 from, Vector2 to, float delaySeconds,
		float flightSeconds, float radius, int damage, Color warningColor)
	{
		if (arena == null || !IsInstanceValid(arena))
			return null;

		var strike = new LanternFireStrike
		{
			from = from,
			delay = Mathf.Max(0f, delaySeconds),
			flight = Mathf.Max(0.2f, flightSeconds),
			// Higher for a longer throw, so a far strike reads as lobbed and not fired flat.
			arcHeight = Mathf.Clamp(from.DistanceTo(to) * 0.45f, 90f, 260f),
			Name = "LanternFireStrike",
		};
		strike.shape = new GroundSlamAttack(strike.flight, strike.flight, radius, damage, warningColor);
		arena.AddChild(strike);
		strike.GlobalPosition = to;
		return strike;
	}

	public override void _Ready()
	{
		strikeTexture ??= GD.Load<Texture2D>(StrikeTexturePath);
		emberTexture ??= GD.Load<Texture2D>(EmberTexturePath);
		// Its light is a child drawn additively over the actors, as the event lights are, so the flame
		// in the air and the fire on the ground brighten what is under them and never hide it.
		light = new Node2D { Name = "Light", ZIndex = 8, Material = ExploreArt.Additive, TextureFilter = TextureFilterEnum.Linear };
		light.Draw += OnLightDraw;
		AddChild(light);
		// The flame in the air is over everything on the ground. This node itself is y-sorted with the
		// actors at the spot it lands on, which is right for the ring and the column and wrong for a
		// thing in flight over somebody's head. Level with the light, so the tender's dark (7) never
		// hides a flame that is coming down.
		flightLayer = new Node2D { Name = "Flight", ZIndex = 8 };
		flightLayer.Draw += OnFlightDraw;
		AddChild(flightLayer);
		// The ring is painted on the floor, under every body: y-sorted with them it lay across every body
		// north of the spot (DrawLayers). The column it leaves still stands among them, as above.
		ring = new Node2D { Name = "Ring", ZAsRelative = false, ZIndex = DrawLayers.GroundTell };
		ring.Draw += OnRingDraw;
		AddChild(ring);
	}

	public override void _ExitTree()
	{
		if (light != null && IsInstanceValid(light))
			light.Draw -= OnLightDraw;
		if (flightLayer != null && IsInstanceValid(flightLayer))
			flightLayer.Draw -= OnFlightDraw;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		QueueRedraw();
		light?.QueueRedraw();
		flightLayer?.QueueRedraw();
		ring?.QueueRedraw();

		if (burstClock >= 0f)
		{
			burstClock += dt;
			shape.Tick(dt, false);
			if (burstClock >= BurstSeconds)
				QueueFree();
			return;
		}

		if (!thrown)
		{
			delay -= dt;
			if (delay > 0f)
				return;
			thrown = true;
			// Primed: the ring is on the ground the frame the flame leaves, and lands when it does.
			shape.Prime();
		}

		clock += dt;
		// wantsToAttack forced true, as TelegraphedGroundHit's: this exists only to land once.
		if (shape.Tick(dt, true) != AttackTelegraph.Beat.Resolved || resolved)
			return;

		resolved = true;
		burstClock = 0f;
		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is Node2D target && IsInstanceValid(target))
				shape.ResolveAgainst(this, target);
		}
	}

	// The flame's place in the air at progress t of its flight, in local space (this node is the spot
	// it lands on): a straight line from the lantern, lifted by a parabola.
	private Vector2 FlightPoint(float t)
	{
		Vector2 ground = (from - GlobalPosition) * (1f - t);
		return ground + Vector2.Up * (arcHeight * 4f * t * (1f - t));
	}

	private float FlightProgress => Mathf.Clamp(clock / flight, 0f, 1f);

	public override void _Draw()
	{
		if (!thrown)
			return;

		// The burst, standing on the spot it struck.
		if (burstClock < 0f || strikeTexture == null)
			return;
		int frame = Mathf.Min(StrikeFrames - 1, (int)(burstClock * StrikeFps));
		float size = strikeTexture.GetHeight();
		DrawTextureRectRegion(strikeTexture, new Rect2(-StrikeGround, new Vector2(size, size)),
			new Rect2(frame * size, 0f, size, size));
	}

	// The ring on the ground: GroundSlamAttack's dashed outline for the whole flight, then its strike.
	private void OnRingDraw()
	{
		if (thrown)
			shape.Draw(ring);
	}

	private void OnFlightDraw()
	{
		if (!thrown || burstClock >= 0f || emberTexture == null)
			return;
		// The flame in flight, turned to the way it is going, so it reads as thrown.
		float t = FlightProgress;
		Vector2 at = FlightPoint(t);
		Vector2 heading = FlightPoint(Mathf.Min(1f, t + 0.02f)) - at;
		float angle = heading.LengthSquared() > 0.001f ? heading.Angle() : Mathf.Pi * 0.5f;
		int emberFrame = (int)(clock * EmberFps) % EmberFrames;
		float cell = emberTexture.GetHeight();
		flightLayer.DrawSetTransform(at, angle, Vector2.One);
		flightLayer.DrawTextureRectRegion(emberTexture, new Rect2(-cell * 0.66f, -cell * 0.5f, cell, cell),
			new Rect2(emberFrame * cell, 0f, cell, cell));
		flightLayer.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private void OnLightDraw()
	{
		if (!thrown)
			return;
		if (burstClock >= 0f)
		{
			// The fire lights its own ground, brightest as it erupts and dying with the frames.
			float fade = 1f - Mathf.Clamp(burstClock / BurstSeconds, 0f, 1f);
			ExploreArt.DrawGlow(light, Vector2.Zero, shape.Radius * 1.9f, new Color(FlameMid, 0.30f * fade));
			ExploreArt.DrawGlow(light, new Vector2(0f, -40f), 70f, new Color(FlameHot, 0.45f * fade));
			return;
		}
		Vector2 at = FlightPoint(FlightProgress);
		ExploreArt.DrawGlow(light, at, 54f, new Color(FlameMid, 0.42f));
		ExploreArt.DrawGlow(light, at, 18f, new Color(FlameCore, 0.7f));
		// The spot it is falling on catches its light as it comes down.
		ExploreArt.DrawGlow(light, Vector2.Zero, shape.Radius * 1.3f, new Color(FlameMid, 0.12f * FlightProgress));
	}
}
