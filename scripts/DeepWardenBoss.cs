using Godot;
using WizardSurvivors.scripts;

// Chapter 8, the Emberdeep: THE WARDEN OF THE DEEP. The last fight.
//
// It has four phases and each one is a chapter boss played back. It charges like the Gaoler, it
// sweeps like the Archivist, it burrows like the Long Coil, and at the end it does all three
// while the ceiling comes down the way the Still Warden's did.
//
// WHY A RECAP AND NOT A NEW IDEA. A final boss that introduces a mechanic gets to be read for the
// first time at the exact moment the player has the most to lose, which is where a campaign
// usually chooses between unfair and trivial. A recap asks a different question: not "can you
// read this", which the player has already proved seven times, but "can you still read the first
// one after six chapters of not needing to". Everything here has been survived before; nothing
// here has been survived in sequence, with no break between, at a health total that does not
// forgive a single missed read.
//
// It reuses the shared primitives rather than the chapter classes - GroundSlamAttack for all three
// warning shapes, BurrowMove for the dive, TelegraphedGroundHit for the bombardment - so a fix to
// any of those reaches the finale too. What is duplicated here is only the STEERING for each mode,
// which is a handful of lines apiece and genuinely different because it has to hand off.
public partial class DeepWardenBoss : BossEnemy
{
	private enum Mode
	{
		/// <summary>Phase 1. Telegraphed lane, then it runs the lane. The Gaoler.</summary>
		Charge,
		/// <summary>Phase 2. Planted, a wedge of fire turning at a constant rate. The Archivist.</summary>
		Sweep,
		/// <summary>Phase 3. Mostly under, killable in windows. The Long Coil.</summary>
		Burrow,
		/// <summary>Phase 4. All of it, and the ceiling comes down. The Still Warden.</summary>
		Cataclysm,
	}

	[Export] public float ChargeHalfWidth { get; set; } = 32f;
	[Export] public float ChargeTellFraction { get; set; } = 0.42f;
	[Export] public float ChargeSpeedMultiplier { get; set; } = 3.2f;

	[Export] public float SweepDegreesPerSecond { get; set; } = 54f;
	[Export] public float SweepHalfAngle { get; set; } = 26f;
	[Export] public float SweepRadius { get; set; } = 330f;

	[Export] public float SurfacedSeconds { get; set; } = 5.0f;
	[Export] public float DiveTravelSeconds { get; set; } = 2.2f;
	[Export] public float ResurfaceDistance { get; set; } = 200f;
	[Export] public int ResurfaceDamage { get; set; } = 7;
	[Export] public float ResurfaceRadius { get; set; } = 140f;

	[Export] public float EmberVolleyIntervalSeconds { get; set; } = 2.8f;
	[Export] public int EmbersPerVolley { get; set; } = 4;
	[Export] public float EmberTellSeconds { get; set; } = 0.95f;
	[Export] public float EmberRadius { get; set; } = 72f;
	[Export] public int EmberDamage { get; set; } = 5;
	[Export] public float EmberScatterRadius { get; set; } = 150f;

	[Export] public Color EmberColor { get; set; } = new Color(1.0f, 0.46f, 0.18f);

	private Mode mode = Mode.Charge;
	private BurrowMove dive;
	private float untilDive;
	private float untilVolley;
	private bool chargeHitLanded;
	private float sweepSign = 1f;

	public override void _Ready()
	{
		base._Ready();
		dive = new BurrowMove(DiveTravelSeconds);
		untilDive = SurfacedSeconds;
		untilVolley = EmberVolleyIntervalSeconds;
	}

	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, EmberColor)
		{
			Shape = TelegraphShape.Line,
			HalfWidth = ChargeHalfWidth,
		};

	// Four quarters. Even spacing rather than a late cliff, because the point of the fight is that
	// each mode gets long enough to be recognised as the boss it is quoting.
	protected override float[] BuildPhaseThresholds() => new[] { 0.75f, 0.5f, 0.25f };

	protected override void OnPhaseEntered(int phase)
	{
		switch (phase)
		{
			case 1:
				EnterSweep();
				break;
			case 2:
				EnterBurrow();
				break;
			default:
				EnterCataclysm();
				break;
		}
	}

	private void EnterSweep()
	{
		mode = Mode.Sweep;
		Slam.Shape = TelegraphShape.Cone;
		Slam.Radius = SweepRadius;
		Slam.HalfAngleDegrees = SweepHalfAngle;
		// A beam has to be visible for its whole life or it is not a beam, so the wind-up becomes
		// nearly the entire cycle. The short gap is the discharge, and it is the frame the hit
		// actually lands on.
		Slam.WindUpSeconds = 1.3f;
		Slam.IntervalSeconds = 0.1f;
		Slam.Prime();
		SetBaseModulate(new Color(1.0f, 0.72f, 0.46f));
	}

	private void EnterBurrow()
	{
		mode = Mode.Burrow;
		Slam.Shape = TelegraphShape.Ring;
		Slam.Radius = SlamRadius;
		Slam.WindUpSeconds = SlamTelegraphSeconds;
		Slam.IntervalSeconds = SlamIntervalSeconds * 0.8f;
		untilDive = SurfacedSeconds;
		SetBaseModulate(new Color(0.94f, 0.58f, 0.36f));
	}

	private void EnterCataclysm()
	{
		mode = Mode.Cataclysm;
		ApplyEnrage();
		// Back to the lane it opened with, at the speed the last phase earned. The first thing the
		// player learned, asked again while everything else is still happening.
		Slam.Shape = TelegraphShape.Line;
		Slam.Radius = SlamRadius * 1.4f;
		Slam.HalfWidth = ChargeHalfWidth;
		Slam.WindUpSeconds = SlamTelegraphSeconds * 0.85f;
	}

	protected override bool PlantsDuringWindUp => mode != Mode.Charge && mode != Mode.Cataclysm;

	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		switch (mode)
		{
			case Mode.Sweep:
				return Vector2.Zero;

			case Mode.Burrow:
				return dive.IsUnder ? Vector2.Zero : chaseDirection;

			default:
				if (Slam == null || !Slam.IsWindingUp)
					return chaseDirection;
				// Plant for the tell, then run the lane. Not at the player: a lane that curved
				// could not be stepped out of, and stepping out of it is the answer.
				return Slam.WindUpProgress < ChargeTellFraction
					? Vector2.Zero
					: Slam.Facing * ChargeSpeedMultiplier;
		}
	}

	protected override bool WantsToAttack(Node2D target, float distanceToPlayer)
	{
		switch (mode)
		{
			case Mode.Sweep:
				return true;
			case Mode.Burrow:
				return !dive.IsUnder && distanceToPlayer <= SlamRadius * SlamEngageRadiusMultiplier;
			default:
				return distanceToPlayer >= SlamRadius * 0.2f && distanceToPlayer <= SlamRadius * 1.4f;
		}
	}

	protected override void OnAttackStarted(Node2D target)
	{
		chargeHitLanded = false;
		// The sweep owns its own facing; everything else aims once, here, and then holds.
		if (mode == Mode.Sweep)
		{
			PlayAttackAnimation();
			return;
		}

		base.OnAttackStarted(target);
	}

	protected override void OnAttackResolved(Node2D target)
	{
		if (chargeHitLanded)
			return;

		base.OnAttackResolved(target);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		float step = (float)delta;

		if (mode == Mode.Sweep && Slam != null)
			Slam.Facing = Slam.Facing.Rotated(Mathf.DegToRad(SweepDegreesPerSecond * sweepSign) * step);

		if (mode == Mode.Burrow)
			TickDive(step);

		if (mode == Mode.Charge || mode == Mode.Cataclysm)
			TickRunOver();

		if (mode == Mode.Cataclysm)
			TickEmbers(step);
	}

	// Same reasoning as the Gaoler: a charge has to hurt where it passes, not where it stops, or
	// the player it ran through is behind it by the time the telegraph resolves.
	private void TickRunOver()
	{
		if (chargeHitLanded || Slam == null || !Slam.IsWindingUp || Slam.WindUpProgress < ChargeTellFraction)
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

	private const float RunOverBodyRadius = 30f;

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
				sweepSign = -sweepSign;
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
		TelegraphedGroundHit.Place(GetParent(), destination,
			Mathf.Max(0.2f, DiveTravelSeconds - 0.1f), ResurfaceRadius, ScaleOutgoingDamage(ResurfaceDamage), EmberColor);
	}

	private void TickEmbers(float delta)
	{
		if (IsDying)
			return;

		untilVolley -= delta;
		if (untilVolley > 0f)
			return;

		untilVolley = EmberVolleyIntervalSeconds;

		Node2D target = TargetPlayer;
		Node arena = GetParent();
		if (target == null || !IsInstanceValid(target) || arena == null || !IsInstanceValid(arena))
			return;

		for (int i = 0; i < Mathf.Max(1, EmbersPerVolley); i++)
		{
			float angle = GD.Randf() * Mathf.Tau;
			float distance = Mathf.Sqrt(GD.Randf()) * EmberScatterRadius;
			Vector2 where = target.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
			TelegraphedGroundHit.Place(arena, where, EmberTellSeconds, EmberRadius, ScaleOutgoingDamage(EmberDamage), EmberColor);
		}
	}

	protected override void StartDeath()
	{
		dive?.Abort();
		base.StartDeath();
	}

	public override void _Draw()
	{
		base._Draw();

		if (mode != Mode.Burrow || !dive.IsUnder)
			return;

		var soil = new Color(EmberColor.R * 0.6f, EmberColor.G * 0.4f, EmberColor.B * 0.3f, 0.6f);
		float swell = 1f + 0.2f * Mathf.Sin(dive.Progress * Mathf.Tau * 3f);
		DrawCircle(Vector2.Zero, 30f * swell, soil);
		DrawArc(Vector2.Zero, 38f * swell, 0f, Mathf.Tau, 24, soil, 2.5f, true);
	}
}
