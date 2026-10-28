using Godot;
using WizardSurvivors.scripts;

// Chapter 2, the Cursed Dungeon: THE GAOLER.
//
// The fight teaches one thing - a lane is dodged sideways, never backwards - and then takes the
// lane away and makes you re-learn the same ground at half the distance.
//
// PHASE 1, MOUNTED. It charges. A lane appears in front of it, holds for a beat while it is still
// standing still, and then it runs the whole length of that lane. Backpedalling is the instinct
// and it is the losing move: the charge is faster than the player and the lane is longer than the
// screen is tall, so the only answer is to step out of it. Between charges it is slow, which is
// the window to actually damage it.
//
// PHASE 2, ON FOOT, at half health. The horse is gone. It is faster, its cooldown is shorter, and
// its attack becomes a short cone it can throw from much closer - so the distance that was safe in
// phase 1 is exactly the distance that kills in phase 2. Same lesson, re-taught at a range the
// player had learned to treat as free.
//
// The charge is built from the travelling-telegraph primitive documented on BossEnemy: nothing
// here moves the warning, it moves the BOSS, and the warning is drawn on the boss.
public partial class GaolerBoss : BossEnemy
{
	/// <summary>Half-width of the charge lane. Wide enough to read, narrow enough to leave.</summary>
	[Export] public float ChargeHalfWidth { get; set; } = 30f;

	/// <summary>How much of the wind-up is spent planted, showing the lane before anything moves.</summary>
	[Export] public float ChargeTellFraction { get; set; } = 0.45f;

	/// <summary>Speed multiplier along the lane once the tell is over.</summary>
	[Export] public float ChargeSpeedMultiplier { get; set; } = 3.4f;

	/// <summary>Reach and half-angle of the dismounted cone.</summary>
	[Export] public float FootSweepRadius { get; set; } = 132f;
	[Export] public float FootSweepHalfAngle { get; set; } = 48f;

	/// <summary>Dismounting is the whole of phase 2, so it is worth stating in numbers.</summary>
	[Export] public float DismountHealthFraction { get; set; } = 0.5f;
	[Export] public float DismountSpeedMultiplier { get; set; } = 1.5f;
	[Export] public float DismountIntervalMultiplier { get; set; } = 0.55f;

	private bool onFoot;
	// One hit per charge. Without it the run-over check below would bill the player every frame the
	// horse was on top of them, which at charge speed is four or five frames.
	private bool chargeHitLanded;

	// A lane, not a ring. Radius is the length it covers, which is what the charge runs.
	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, new Color(0.74f, 0.30f, 0.86f))
		{
			Shape = TelegraphShape.Line,
			HalfWidth = ChargeHalfWidth,
		};

	protected override float[] BuildPhaseThresholds() => new[] { DismountHealthFraction, EnrageHealthFraction };

	protected override void OnPhaseEntered(int phase)
	{
		if (phase == 1)
		{
			Dismount();
			return;
		}

		// Phase 2 is the ordinary enrage on top of whatever it is by then. A boss whose last
		// threshold did something entirely new would have no read left at the point the player is
		// closest to dying.
		ApplyEnrage();
	}

	private void Dismount()
	{
		onFoot = true;
		Speed *= DismountSpeedMultiplier;
		SlamIntervalSeconds *= DismountIntervalMultiplier;
		Slam.IntervalSeconds = SlamIntervalSeconds;
		Slam.Shape = TelegraphShape.Cone;
		Slam.Radius = FootSweepRadius;
		Slam.HalfAngleDegrees = FootSweepHalfAngle;
		// Colder and paler: the thing that was riding is now the thing chasing.
		SetBaseModulate(new Color(0.78f, 0.74f, 0.92f));
		// It arrives on foot already swinging. The dismount is the most dangerous moment of the
		// fight and it should not be followed by a free cooldown.
		Slam.Prime();
	}

	// Mounted it charges, so it must not plant for the whole wind-up. On foot it plants like
	// anything else - a cone thrown while running would be undodgeable.
	protected override bool PlantsDuringWindUp => onFoot;

	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		if (onFoot || Slam == null || !Slam.IsWindingUp)
			return base.AdjustSteering(chaseDirection, distanceToPlayer);

		// The tell: planted, lane drawn, nothing moving yet. This is the window the player reads.
		if (Slam.WindUpProgress < ChargeTellFraction)
			return Vector2.Zero;

		// And then it runs down the lane it drew, not at the player. Chasing mid-charge would mean
		// the lane curved, and a curved lane cannot be stepped out of.
		return Slam.Facing * ChargeSpeedMultiplier;
	}

	protected override void OnAttackStarted(Node2D target)
	{
		chargeHitLanded = false;
		base.OnAttackStarted(target);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickRunOver();
	}

	// THE CHARGE HAS TO HURT WHERE IT PASSES, not where it stops.
	//
	// A telegraph resolves at the caster's position on the frame it lands, which is right for
	// everything that stands still and wrong for this: by the time the wind-up ends, the player it
	// ran through is behind it and would take nothing at all. So the lane is the PATH, and standing
	// on the path when the horse arrives is the hit. The lane drawn ahead of the boss still resolves
	// normally at the end, for a player who backed to the far end of it rather than stepping aside.
	private void TickRunOver()
	{
		if (onFoot || chargeHitLanded || Slam == null || !Slam.IsWindingUp)
			return;

		if (Slam.WindUpProgress < ChargeTellFraction)
			return;

		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target) || !target.HasMethod("TakeDamage"))
			return;

		if (GlobalPosition.DistanceTo(target.GlobalPosition) > ChargeHalfWidth + RunOverBodyRadius)
			return;

		chargeHitLanded = true;
		int dealt = ScaleOutgoingDamage(SlamDamage);
		if (dealt > 0)
			target.Call("TakeDamage", dealt);
	}

	// Half the boss body, roughly: the horse is wider than the lane markings and being clipped by
	// its shoulder should count.
	private const float RunOverBodyRadius = 26f;

	protected override void OnAttackResolved(Node2D target)
	{
		// Already billed for being run over - do not charge twice for one charge.
		if (!onFoot && chargeHitLanded)
			return;

		base.OnAttackResolved(target);
	}

	// Mounted, it wants range to charge into - a charge that starts on top of the player is just a
	// contact hit. On foot it commits at whatever range the cone reaches.
	protected override bool WantsToAttack(Node2D target, float distanceToPlayer)
	{
		if (onFoot)
			return distanceToPlayer <= FootSweepRadius * 1.6f;

		return distanceToPlayer >= SlamRadius * 0.25f && distanceToPlayer <= SlamRadius * 1.3f;
	}

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// THE KEYS SCATTER. It is the jailer of the place where a wizard is held, and the one thing it
	// carries that is not armour is the ring of keys at its hand (sprite_bosses.py draws it in gold,
	// the only warm metal on it). So that goes first: the ring flares and bursts, and the keys to every
	// cell in the dungeon go skittering across the floor in every direction and lie there winking -
	// the cells are open. The chains it hung with come off the mount in the same breath. Then the
	// mount's legs fold, the rider is pitched forward off it over the horse's neck, and both go dark.
	// The horse's head is on the left as drawn, so "forward" is left in art space.
	private BossDeath.Piece deathKeyring;
	private BossDeath.Piece deathRider;
	private BossDeath.Piece deathMount;

	private const float KeysBurst = 0.14f;
	private const float RiderLands = 0.95f;
	private static readonly Color KeyGlint = new(1f, 0.94f, 0.7f, 0.95f);
	private static readonly Color DungeonDust = new(0.42f, 0.46f, 0.56f, 0.6f);

	protected internal override void OnDeathBegin(BossDeath death)
	{
		// Three pieces that do not overlap: the ring of keys at the hand, the rider above the saddle,
		// and the mount (with the corner under the keys).
		deathKeyring = death.Cut(new Vector2I(82, 52), new Rect2I(72, 42, 24, 22));
		deathRider = death.Cut(new Vector2I(36, 42), new Rect2I(0, 0, 96, 42));
		deathMount = death.Cut(new Vector2I(68, 94), new Rect2I(0, 42, 72, 54), new Rect2I(72, 64, 24, 32));
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathMount == null)
			return;

		float s = death.ArtScale.X;
		float shudder = t < 0.3f ? BossDeath.Shudder(t, 1f, 20f) * s : 0f;

		// The ring of keys flares gold at the hand, and goes.
		deathKeyring.Offset = new Vector2(shudder, 0f);
		deathKeyring.Glow = new Color(1f, 0.82f, 0.4f, 0.9f * BossDeath.Span(t, 0f, KeysBurst));
		deathKeyring.Visible = t < KeysBurst;

		// The mount's legs fold under it, front first.
		float buckle = BossDeath.EaseIn(BossDeath.Span(t, 0.2f, 0.78f));
		deathMount.Sink(death, 12f * buckle);
		deathMount.Offset += new Vector2(shudder, 0f);
		deathMount.Rotation = -Mathf.DegToRad(7f) * buckle;

		// The rider is pitched forward off it, over the neck, and lands face down in front.
		float pitch = BossDeath.EaseIn(BossDeath.Span(t, 0.28f, RiderLands));
		float bounce = 0.06f * Mathf.Sin(Mathf.Pi * BossDeath.Span(t, RiderLands, RiderLands + 0.22f));
		deathRider.Rotation = -Mathf.DegToRad(84f) * pitch + bounce;
		deathRider.Offset = new Vector2(shudder - 24f * s * pitch, (12f * buckle + 38f * pitch) * s);

		// Then the iron goes dark, and is gone; the keys stay longest.
		float dim = BossDeath.Span(t, 1.0f, 1.7f);
		var iron = new Color(1f - 0.5f * dim, 1f - 0.5f * dim, 1f - 0.4f * dim, 1f - BossDeath.Span(t, 1.6f, 2.2f));
		deathRider.Color = iron;
		deathMount.Color = iron;

		if (death.Crossed(KeysBurst))
		{
			Vector2 hand = new(82f, 54f);
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.KeyLong, Variants = 2, Tinted = true,
				SpeedMin = 90f, SpeedMax = 250f, UpMin = 120f, UpMax = 260f, Gravity = 700f,
				Bounce = 0.42f, Friction = 3.6f, Spin = 10f, Life = DeathSeconds - KeysBurst, Fade = 0.35f,
				DepthJitter = 6f, Glint = KeyGlint,
			}, 9, hand, new Vector2(3f, 3f));
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Ring, Tinted = true, SpeedMin = 20f, SpeedMax = 60f, UpMin = 60f, UpMax = 120f,
				Bounce = 0.5f, Friction = 5f, Spin = 4f, Life = DeathSeconds - KeysBurst, Fade = 0.35f, Glint = KeyGlint,
			}, 1, hand, Vector2.Zero);
			death.PuffAt(death.ArtToLocal(hand), 6f, 30f, 0.3f, new Color(1f, 0.78f, 0.36f, 0.8f), 4f, 0f, true);
		}

		if (death.Crossed(0.1f))
		{
			// The chains it hung with, off the harness and the rider's belt.
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.LinkFlat, Variants = 2, Tinted = true,
				SpeedMin = 60f, SpeedMax = 210f, UpMin = 90f, UpMax = 220f, Bounce = 0.32f, Friction = 5f,
				Spin = 8f, Life = 2.3f, Fade = 0.4f, Glint = new Color(0.86f, 0.92f, 1f, 0.5f),
			}, 12, new Vector2(42f, 48f), new Vector2(22f, 6f), 0f, 0.012f);
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Shackle, Tinted = true, SpeedMin = 40f, SpeedMax = 130f, UpMin = 80f, UpMax = 160f,
				Bounce = 0.3f, Friction = 6f, Spin = 6f, Life = 2.3f, Fade = 0.4f,
			}, 2, new Vector2(48f, 40f), new Vector2(10f, 3f));
		}

		if (death.Crossed(RiderLands))
		{
			Vector2 face = death.PieceToLocal(deathRider, new Vector2(46f, 12f));
			death.DustRing(new Vector2(face.X, death.Foot.Y - 10f), 46f, 7, DungeonDust, 0.7f, 14f);
		}
	}
}
