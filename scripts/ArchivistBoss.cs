using Godot;
using WizardSurvivors.scripts;

// Chapter 5, the Mystic Ruins: THE ARCHIVIST.
//
// It never takes a step. It stands where it stands and turns, and a wedge of light turns with it,
// and the whole fight is the player orbiting to stay off the wedge. Then, every so often, it drops
// through the floor and comes up somewhere else, and the orbit the player had settled into is
// pointing at nothing.
//
// TWO IDEAS, AND THEY ARE OPPOSITES ON PURPOSE.
//
// The sweep is a POSITIONAL threat with no reaction time in it at all: it is visible the whole
// time, it turns at a constant rate, and a player who keeps moving with it is never hit. That
// suits a game where the player never aims - the only input the fight asks for is the one input
// the player actually has.
//
// The burrow is the opposite: nothing to read, a fixed wait, and then a new problem. It is also
// the answer to what a stationary boss is otherwise vulnerable to, which is being parked on at
// maximum range and melted. Going untargetable paces the fight with time that cannot be spent
// damaging, which is worth more than the same delay expressed as extra health, because extra
// health makes a fight longer and this makes it harder.
//
// ONE DELIBERATE EXCEPTION to the telegraph rules. Everything else in this project aims on the
// frame the wind-up starts and then holds, because an aim that keeps tracking gives the player no
// sideways to step to. The sweep tracks continuously - it has to, it is a sweep - and that is
// legitimate only because the wedge is drawn for its entire life. The player is not reading a tell
// and predicting a hit; they are looking at the hit.
public partial class ArchivistBoss : BossEnemy
{
	/// <summary>Degrees the beam turns per second. Slow enough to walk away from, fast enough to catch a stander.</summary>
	[Export] public float SweepDegreesPerSecond { get; set; } = 42f;

	/// <summary>Half-angle of the wedge. Narrow: a wide one is a room, not a beam.</summary>
	[Export] public float SweepHalfAngle { get; set; } = 19f;

	[Export] public float BurrowIntervalSeconds { get; set; } = 12f;
	[Export] public float BurrowTravelSeconds { get; set; } = 2.4f;

	/// <summary>How far from the player it comes back up. Close enough to matter, not on top of them.</summary>
	[Export] public float ResurfaceDistance { get; set; } = 240f;

	/// <summary>Damage of the hit that lands where it comes up, and how long that hit is telegraphed.</summary>
	[Export] public int ResurfaceDamage { get; set; } = 5;
	[Export] public float ResurfaceRadius { get; set; } = 120f;
	[Export] public float ResurfaceTellSeconds { get; set; } = 0.9f;

	[Export] public Color ArchiveColor { get; set; } = new Color(0.62f, 0.52f, 0.95f);

	private BurrowMove burrow;
	private float untilBurrow;
	// Which way it turns. Flipped on each dive so the orbit the player settled into stops working
	// the moment it comes back up - otherwise the burrow costs them nothing but a walk.
	private float sweepSign = 1f;

	public override void _Ready()
	{
		base._Ready();
		burrow = new BurrowMove(BurrowTravelSeconds);
		untilBurrow = BurrowIntervalSeconds;
	}

	// A narrow wedge, and an interval far shorter than the wind-up: the beam is essentially always
	// on, and each cycle is it charging to full reach and discharging. The brief gap between cycles
	// is what makes a continuous beam still read as a series of hits.
	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, ArchiveColor)
		{
			Shape = TelegraphShape.Cone,
			HalfAngleDegrees = SweepHalfAngle,
		};

	protected override void OnPhaseEntered(int phase)
	{
		ApplyEnrage();
		// Enrage on a boss that does not chase would be half wasted, so it buys the thing that
		// actually matters here: the beam turns faster, and the orbit gets tighter.
		SweepDegreesPerSecond *= 1.4f;
	}

	// It is a fixture. Nothing it does involves walking, and returning Zero also suppresses the
	// separation nudge, which would otherwise shove it off the spot its beam is anchored to.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer) => Vector2.Zero;

	// The beam does not care where the player is; it sweeps whether or not anyone is in it. Gating
	// it on range would make it flicker off whenever the player stepped back, which is exactly when
	// the fight most needs something on screen to be afraid of.
	protected override bool WantsToAttack(Node2D target, float distanceToPlayer) => !burrow.IsUnder;

	// Overridden to NOT aim at the player. The sweep's facing belongs to the sweep.
	protected override void OnAttackStarted(Node2D target) => PlayAttackAnimation();

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickSweep((float)delta);
		TickBurrow((float)delta);
	}

	private void TickSweep(float delta)
	{
		if (burrow.IsUnder || Slam == null)
			return;

		Slam.Facing = Slam.Facing.Rotated(Mathf.DegToRad(SweepDegreesPerSecond * sweepSign) * delta);
	}

	private void TickBurrow(float delta)
	{
		switch (burrow.Tick(delta))
		{
			case BurrowMove.Beat.Idle:
				untilBurrow -= delta;
				if (untilBurrow <= 0f)
					BeginDive();
				return;

			case BurrowMove.Beat.Submerged:
				SetTargetable(false);
				return;

			case BurrowMove.Beat.Travelling:
				// Driven directly rather than through MoveAndSlide: collision is off while under,
				// so there is nothing for the physics body to resolve against and a straight lerp
				// is both cheaper and exactly what the mound drawn above it says is happening.
				GlobalPosition = burrow.CurrentPosition;
				return;

			case BurrowMove.Beat.Surfaced:
				GlobalPosition = burrow.Destination;
				SetTargetable(true);
				untilBurrow = BurrowIntervalSeconds;
				sweepSign = -sweepSign;
				// The beam restarts pointing away from the player, so coming up is not itself a hit.
				Node2D player = TargetPlayer;
				if (player != null && IsInstanceValid(player))
					Slam.Facing = (GlobalPosition - player.GlobalPosition).Normalized();
				return;
		}
	}

	private void BeginDive()
	{
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
		{
			untilBurrow = BurrowIntervalSeconds;
			return;
		}

		float angle = GD.Randf() * Mathf.Tau;
		Vector2 destination = target.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ResurfaceDistance;
		burrow.Begin(GlobalPosition, destination);

		// The exit is telegraphed on the ground before it gets there. A boss that surfaced unseen
		// would be the one part of this fight the player could not read, and it would land next to
		// them roughly a third of the time.
		TelegraphedGroundHit.Place(GetParent(), destination,
			Mathf.Max(0.2f, Mathf.Min(ResurfaceTellSeconds, BurrowTravelSeconds - 0.1f)),
			ResurfaceRadius, ResurfaceDamage, ArchiveColor);
	}

	protected override void StartDeath()
	{
		// A dive in progress would keep driving position out of a corpse.
		burrow?.Abort();
		base.StartDeath();
	}

	public override void _Draw()
	{
		base._Draw();

		if (!burrow.IsUnder)
			return;

		// A mound, so the player can track it under the floor. Three circles rather than a sprite:
		// the whole point of being under is that there is no sprite.
		var soil = new Color(ArchiveColor.R * 0.5f, ArchiveColor.G * 0.5f, ArchiveColor.B * 0.6f, 0.55f);
		float swell = 1f + 0.18f * Mathf.Sin(burrow.Progress * Mathf.Tau * 3f);
		DrawCircle(Vector2.Zero, 26f * swell, soil);
		DrawArc(Vector2.Zero, 34f * swell, 0f, Mathf.Tau, 24, soil, 2.5f, true);
	}
}
