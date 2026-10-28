using Godot;
using WizardSurvivors.scripts;

// Chapter 7, the Scorched Sands: THE LONG COIL.
//
// A burrower that spends most of the fight under the sand. It runs a lap beneath the player,
// throws up a ridge of sand where it is going, comes out of the ground at the end of it, and is
// vulnerable only for the few seconds it takes to get back under.
//
// THE FIGHT IS A WINDOW, NOT A HEALTH BAR. Every other boss can be damaged whenever the player can
// reach it, so every other boss is answered by sustained output. This one can only be damaged
// while it is up, which makes the question "how much damage can you land in four seconds" - a
// burst question, and the exact opposite of the one Elderbark asks in chapter one. A build that
// cannot spike is not locked out, but it will be here a while.
//
// ONLY THE HEAD TAKES DAMAGE, and it costs nothing to enforce: the head is the node, and the body
// behind it is drawn from a trail of positions the head already visited. There are no segment
// nodes, so there is nothing to accidentally make targetable, nothing extra in the "enemies"
// group, and no per-frame allocation - the trail is one preallocated ring buffer written once per
// physics frame.
public partial class LongCoilBoss : BossEnemy
{
	/// <summary>Seconds it stays up and killable after surfacing. The whole damage window.</summary>
	[Export] public float SurfacedSeconds { get; set; } = 4.2f;

	/// <summary>Seconds spent travelling under the sand between windows.</summary>
	[Export] public float DiveTravelSeconds { get; set; } = 3.0f;

	/// <summary>How far from the player it comes back up.</summary>
	[Export] public float ResurfaceDistance { get; set; } = 170f;

	/// <summary>The hit it lands coming out of the ground, and how long that is marked first.</summary>
	[Export] public int ResurfaceDamage { get; set; } = 6;
	[Export] public float ResurfaceRadius { get; set; } = 130f;
	[Export] public float ResurfaceTellSeconds { get; set; } = 1.1f;

	[Export] public int BodySegments { get; set; } = 9;
	[Export] public float SegmentSpacing { get; set; } = 17f;
	[Export] public Color SandColor { get; set; } = new Color(0.86f, 0.66f, 0.34f);

	private BurrowMove dive;
	private float untilDive;

	// The trail the body is drawn from: one position per physics frame, oldest overwritten. Fixed
	// size and written in place, because this runs every frame of a fifteen-minute run in a scene
	// that already has a swarm in it.
	private Vector2[] trail;
	private int trailHead;
	private int trailFilled;

	public override void _Ready()
	{
		base._Ready();
		dive = new BurrowMove(DiveTravelSeconds);
		untilDive = SurfacedSeconds;

		// One slot per frame of spacing, so SegmentSpacing is measured in samples rather than in
		// pixels - which is what keeps the body the same length whether it is sprinting or planted.
		trail = new Vector2[Mathf.Max(2, BodySegments) * TrailSamplesPerSegment];
		for (int i = 0; i < trail.Length; i++)
			trail[i] = GlobalPosition;
		trailFilled = trail.Length;
	}

	private const int TrailSamplesPerSegment = 5;

	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, SandColor);

	protected override void OnPhaseEntered(int phase)
	{
		ApplyEnrage();
		// A longer window rather than a shorter one. The interesting knob on this fight is how much
		// of it the player gets to spend attacking, and the enrage spends it the generous way on
		// purpose: it is more dangerous up than down.
		SurfacedSeconds *= 1.35f;
		DiveTravelSeconds *= 0.75f;
		dive.TravelSeconds = DiveTravelSeconds;
	}

	protected override bool WantsToAttack(Node2D target, float distanceToPlayer) =>
		!dive.IsUnder && base.WantsToAttack(target, distanceToPlayer);

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickDive((float)delta);
		RecordTrail();
	}

	private void TickDive(float delta)
	{
		switch (dive.Tick(delta))
		{
			case BurrowMove.Beat.Idle:
				untilDive -= delta;
				if (untilDive <= 0f)
					BeginDive();
				return;

			case BurrowMove.Beat.Submerged:
				SetTargetable(false);
				return;

			case BurrowMove.Beat.Travelling:
				GlobalPosition = dive.CurrentPosition;
				return;

			case BurrowMove.Beat.Surfaced:
				GlobalPosition = dive.Destination;
				SetTargetable(true);
				untilDive = SurfacedSeconds;
				return;
		}
	}

	private void BeginDive()
	{
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
		{
			untilDive = SurfacedSeconds;
			return;
		}

		float angle = GD.Randf() * Mathf.Tau;
		Vector2 destination = target.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ResurfaceDistance;
		dive.Begin(GlobalPosition, destination);

		// Marked before it arrives, and the mark is the whole reason the dive is fair: the ridge
		// says where, and this says exactly where and when.
		TelegraphedGroundHit.Place(GetParent(), destination,
			Mathf.Max(0.2f, Mathf.Min(ResurfaceTellSeconds, DiveTravelSeconds - 0.1f)),
			ResurfaceRadius, ScaleOutgoingDamage(ResurfaceDamage), SandColor);
	}

	private void RecordTrail()
	{
		trailHead = (trailHead + 1) % trail.Length;
		trail[trailHead] = GlobalPosition;
		if (trailFilled < trail.Length)
			trailFilled++;
	}

	protected override void StartDeath()
	{
		dive?.Abort();
		base.StartDeath();
	}

	public override void _Draw()
	{
		base._Draw();

		bool under = dive.IsUnder;
		int segments = Mathf.Max(2, BodySegments);

		// Under the sand the body is a ridge - lighter, flatter, no outline. Above it, it is the
		// coil itself. Same geometry either way, which is what stops the two states reading as two
		// different creatures.
		for (int i = 1; i <= segments; i++)
		{
			int back = i * TrailSamplesPerSegment;
			if (back >= trailFilled)
				break;

			Vector2 local = SegmentLocal(i);
			// Tapers along its length. A tube of equal circles is a caterpillar, not a coil.
			float taper = 1f - 0.62f * (i / (float)segments);
			float radius = SegmentSpacing * taper;
			// Falling, it goes under a segment at a time from the tail (the fall, below).
			if (deathT >= 0f)
			{
				radius *= 1f - BossDeath.EaseIn(BossDeath.Span(deathT, SegmentSinks(i), SegmentSinks(i) + SegmentSinkSeconds));
				if (radius < 1f)
					continue;
			}

			float alpha = under ? 0.42f * taper : 0.92f * taper;
			var body = new Color(SandColor.R * (under ? 0.86f : 0.58f),
				SandColor.G * (under ? 0.72f : 0.42f),
				SandColor.B * (under ? 0.50f : 0.26f), alpha);
			DrawCircle(local, radius, body);

			if (!under)
				DrawArc(local, radius, 0f, Mathf.Tau, 16, new Color(0.05f, 0.03f, 0.05f, 0.75f), 2f, true);
		}
	}

	// Where segment i of the body is, in local space: i * TrailSamplesPerSegment frames back along the
	// trail. Thrashing while it dies.
	private Vector2 SegmentLocal(int i)
	{
		int index = (trailHead - i * TrailSamplesPerSegment + trail.Length * 2) % trail.Length;
		Vector2 local = trail[index] - GlobalPosition;
		if (deathT >= 0f)
		{
			float thrash = 6f * (1f - BossDeath.Span(deathT, 0f, 0.9f));
			local += new Vector2(Mathf.Sin(deathT * 29f + i * 0.9f), Mathf.Cos(deathT * 23f + i * 1.3f)) * thrash;
		}
		return local;
	}

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// THE SAND TAKES IT BACK. It spent the fight under the sand and was only ever killable above it,
	// so it dies going under one last time and not coming up. It thrashes - the rearing neck whips,
	// the body writhes along its trail - and then the body goes down a segment at a time from the
	// tail, each one into a spurt of sand, and last the head sinks straight down into the ground it
	// came out of, throwing sand up round it as it goes, while the sand turns over itself in a slow
	// sinkhole and closes flat.
	private float deathT = -1f;
	private BossDeath.Piece deathHead;
	private float sandCarry;

	private const float SegmentSinkSeconds = 0.22f;
	private const float HeadSinkStart = 1.1f;
	private const float HeadSinkEnd = 2.0f;
	private static readonly Color[] Sand =
	{
		new(0.85f, 0.80f, 0.64f), new(0.66f, 0.55f, 0.38f), new(0.49f, 0.38f, 0.25f),
	};

	// The tail first: the last segment goes at 0.3 s, the one behind the head just before the head.
	private float SegmentSinks(int i) => 0.3f + (Mathf.Max(2, BodySegments) - i) * 0.09f;

	protected internal override float DeathSeconds => 2.8f;
	protected internal override bool RedrawsWhileDying => true;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		deathHead = death.CutWhole();
		sandCarry = 0f;
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		deathT = t;
		if (deathHead == null)
			return;

		// The neck whips about its root, wildly and then less.
		float whip = 1f - BossDeath.Span(t, 0f, 1.0f);
		deathHead.Rotation = Mathf.DegToRad(10f) * Mathf.Sin(t * Mathf.Tau * 3.2f) * whip;
		deathHead.Sink(death, death.FrameSize.Y * BossDeath.EaseIn(BossDeath.Span(t, HeadSinkStart, HeadSinkEnd)));

		int segments = Mathf.Max(2, BodySegments);
		for (int i = 1; i <= segments; i++)
		{
			if (i * TrailSamplesPerSegment >= trailFilled || !death.Crossed(SegmentSinks(i)))
				continue;
			// SegmentLocal is relative to the boss; the death sits at its origin, so it is the same space.
			Vector2 at = SegmentLocal(i);
			death.PuffAt(at, 6f, 22f, 0.5f, new Color(Sand[1], 0.7f), 10f);
			death.EmitAt(new BossDeath.Throw
			{
				Size = 2f, Color = Sand[0], ColorB = Sand[2], SpeedMin = 20f, SpeedMax = 80f, UpMin = 80f, UpMax = 180f,
				Gravity = 700f, Bounce = 0f, Friction = 12f, Life = 0.7f, Fade = 0.3f,
			}, 4, at, 0f, 8f);
		}

		if (death.Crossed(0.15f))
		{
			// Scales off it as it thrashes.
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Scale, Tinted = true, SpeedMin = 40f, SpeedMax = 140f, UpMin = 80f, UpMax = 180f,
				Bounce = 0.25f, Friction = 7f, Spin = 6f, Life = 1.5f, Fade = 0.4f,
			}, 8, new Vector2(52f, 50f), new Vector2(14f, 30f));
		}

		// Sand thrown up round the head for as long as it is going under.
		if (t >= HeadSinkStart && t < HeadSinkEnd)
		{
			sandCarry += 90f * dt;
			Vector2 foot = death.Foot;
			while (sandCarry >= 1f)
			{
				sandCarry -= 1f;
				death.EmitAt(new BossDeath.Throw
				{
					Size = death.Rand(0f, 1f) < 0.45f ? 4f : 2f, Color = Sand[0], ColorB = Sand[2],
					SpeedMin = 20f, SpeedMax = 110f, UpMin = 120f, UpMax = 280f, Gravity = 760f, Bounce = 0f,
					Friction = 12f, Life = 0.7f, Fade = 0.3f,
				}, 1, foot + new Vector2(death.Rand(-30f, 30f) * death.ArtScale.X * 0.5f, death.Rand(-4f, 4f)), 0f);
			}
		}

		if (death.Crossed(HeadSinkEnd))
			death.DustRing(death.Foot, 40f, 7, new Color(Sand[1], 0.65f), 0.6f, 14f);
	}

	// The sinkhole: three arms of sand turning in towards where the head went down, widening while it
	// sinks and closing over it after.
	protected internal override void DrawDeathGround(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathHead == null || t < 0.9f)
			return;
		float open = BossDeath.EaseOut(BossDeath.Span(t, 0.9f, 1.6f)) * (1f - BossDeath.EaseIn(BossDeath.Span(t, 2.0f, 2.7f)));
		float r = 96f * open;
		if (r < 4f)
			return;
		// A funnel, not a ring: four stops of sand stepping down into the dark where it went under -
		// lit lip, the slope, the shade, the hole - and grooves of darker sand turning down the slope.
		// (A ring round a boss is a telegraph in this game; this has no edge to read as one.)
		Vector2 centre = death.Foot;
		BossDeath.Disc(canvas, centre, r, r * 0.45f, new Color(Sand[0], 0.55f));
		BossDeath.Disc(canvas, centre, r * 0.78f, r * 0.35f, new Color(Sand[1], 0.85f));
		BossDeath.Disc(canvas, centre, r * 0.52f, r * 0.23f, new Color(Sand[2], 0.9f));
		BossDeath.Disc(canvas, centre, r * 0.26f, r * 0.12f, new Color(0.20f, 0.12f, 0.14f, 0.95f));
		const int arms = 4;
		const int dots = 22;
		for (int k = 0; k < arms; k++)
		{
			for (int j = 3; j <= dots; j++)
			{
				float u = j / (float)dots;
				float a = k * Mathf.Tau / arms + u * 2.8f + t * 3.4f;
				var at = centre + new Vector2(Mathf.Cos(a) * r * 0.9f * u, Mathf.Sin(a) * r * 0.9f * u * 0.45f);
				canvas.DrawRect(WhipLimb.Cell(at, BossDeath.Px), new Color(Sand[2] * 0.7f, 0.8f));
			}
		}
	}
}
