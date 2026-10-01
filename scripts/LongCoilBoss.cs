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

			int index = (trailHead - back + trail.Length * 2) % trail.Length;
			Vector2 local = trail[index] - GlobalPosition;
			// Tapers along its length. A tube of equal circles is a caterpillar, not a coil.
			float taper = 1f - 0.62f * (i / (float)segments);
			float radius = SegmentSpacing * taper;

			float alpha = under ? 0.42f * taper : 0.92f * taper;
			var body = new Color(SandColor.R * (under ? 0.86f : 0.58f),
				SandColor.G * (under ? 0.72f : 0.42f),
				SandColor.B * (under ? 0.50f : 0.26f), alpha);
			DrawCircle(local, radius, body);

			if (!under)
				DrawArc(local, radius, 0f, Mathf.Tau, 16, new Color(0.05f, 0.03f, 0.05f, 0.75f), 2f, true);
		}
	}
}
