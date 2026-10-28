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

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// THE FORGE GOES COLD. Every other boss was taken; this one was BUILT, in his seat, and it burns
	// with his making (sprite_bosses.py gives it the only fire-lit heart in the cast). So it does not
	// crack like ice or fold like cloth - it fails like a furnace. The heart flares, fire runs out of
	// it along every seam of the plate, and then it bursts: both pauldrons are thrown off, plate and
	// embers go everywhere, and the ground under it caves into a molten crater. What is left slumps
	// down into the crater, and the last of the fire goes out of it - the one light in the game that
	// was his, put out. Then the crater cools. It is the last fight, so it is the longest fall.
	private BossDeath.Piece deathCore;
	private BossDeath.Piece deathPauldronLeft;
	private BossDeath.Piece deathPauldronRight;
	private Vector2[][] deathSeams;
	private Node2D deathCrater;
	private BossDeath deathStage;
	private float emberCarry;

	private const float BurstAt = 0.62f;
	private const float PauldronsLand = 1.2f;
	private const float CraterRadius = 116f;

	protected internal override float DeathSeconds => 3.0f;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		deathStage = death;
		deathPauldronLeft = death.Cut(new Vector2I(16, 27), new Rect2I(0, 8, 32, 38));
		deathPauldronRight = death.Cut(new Vector2I(80, 27), new Rect2I(64, 8, 32, 38));
		deathCore = death.Cut(new Vector2I(48, 94),
			new Rect2I(32, 0, 32, 46), new Rect2I(0, 0, 32, 8), new Rect2I(64, 0, 32, 8), new Rect2I(0, 46, 96, 50));

		var heart = new Vector2(47.5f, 40f);
		float[] headings = { -2.4f, -0.7f, 0.4f, 1.1f, 2.0f, 2.7f, -1.57f, Mathf.Pi * 0.5f };
		deathSeams = new Vector2[headings.Length][];
		for (int i = 0; i < headings.Length; i++)
			deathSeams[i] = death.CrackPath((uint)(i * 23 + 5), heart, headings[i], 18, 3f);

		// The crater, through the same quad and shader Meteor Swarm's are (CraterGround), on a node of
		// its own because the shader is its material. A step under the death's own ground layer, so the
		// plate that lands in it lies on top.
		deathCrater = new Node2D { Name = "Crater", ZIndex = CraterGround.GroundZ - 1, ZAsRelative = false, Material = CraterGround.Material };
		death.AddChild(deathCrater);
		deathCrater.Draw += DrawDeathCrater;
		emberCarry = 0f;
	}

	public override void _ExitTree()
	{
		if (deathCrater != null && IsInstanceValid(deathCrater))
			deathCrater.Draw -= DrawDeathCrater;
		base._ExitTree();
	}

	private void DrawDeathCrater()
	{
		if (deathStage == null || !IsInstanceValid(deathStage))
			return;
		float t = deathStage.T;
		if (t < BurstAt)
			return;
		float age = t - BurstAt;
		float heat = CraterGround.HeatAt(age, 0.9f, 1.1f, 7u);
		float left = 1f - BossDeath.Span(t, 2.4f, DeathSeconds);
		CraterGround.Draw(deathCrater, deathStage.Foot + new Vector2(0f, -10f), CraterRadius, heat, age, left);
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathCore == null)
			return;

		deathCrater?.QueueRedraw();
		float s = death.ArtScale.X;
		ElementColors.Ramp fire = ElementColors.GetRamp(Element.Fire);

		// The heart flares, faster and brighter until it goes.
		float strain = BossDeath.Span(t, 0f, BurstAt);
		float flicker = 0.8f + 0.2f * Mathf.Sin(t * (20f + 30f * strain));
		float shudder = t < BurstAt ? BossDeath.Shudder(t, strain * 1.5f, 22f) * s : 0f;
		float burn = (0.15f + 0.45f * strain) * flicker * (1f - BossDeath.Span(t, 1.2f, 2.0f));
		deathCore.Glow = new Color(fire.Mid, burn);

		// Then it slumps down into the crater, and the last of the fire goes out of the plate.
		float slump = BossDeath.EaseIn(BossDeath.Span(t, 0.8f, 1.8f));
		deathCore.Offset = new Vector2(shudder, 0f);
		deathCore.Scale = new Vector2(1f + 0.12f * slump, 1f - 0.7f * slump);
		float cool = BossDeath.Span(t, 1.0f, 2.0f);
		deathCore.Color = new Color(1f - 0.62f * cool, 1f - 0.7f * cool, 1f - 0.68f * cool, 1f - BossDeath.Span(t, 2.0f, 2.6f));

		// The pauldrons are blown off either side and come down at its feet.
		ThrowPauldron(deathPauldronLeft, t, -1f, shudder, s);
		ThrowPauldron(deathPauldronRight, t, 1f, shudder, s);

		// Embers off it for as long as it burns.
		if (t < 2.2f)
		{
			emberCarry += (t < BurstAt ? 10f + 30f * strain : 26f) * dt;
			while (emberCarry >= 1f)
			{
				emberCarry -= 1f;
				death.Emit(new BossDeath.Throw
				{
					Size = 2f, Color = fire.Hot, ColorB = fire.Edge, Light = true, NoLand = true,
					SpeedMin = 5f, SpeedMax = 40f, UpMin = 30f, UpMax = 110f, Gravity = -30f, Drag = 0.8f,
					Flutter = 4f, Life = 1.1f, Fade = 0.7f,
				}, 1, new Vector2(47.5f, 46f), new Vector2(30f, 30f));
			}
		}

		if (death.Crossed(PauldronsLand))
		{
			foreach (BossDeath.Piece pauldron in new[] { deathPauldronLeft, deathPauldronRight })
			{
				Vector2 at = death.PieceToLocal(pauldron, new Vector2(pauldron == deathPauldronLeft ? 16f : 80f, 40f));
				death.DustRing(new Vector2(at.X, death.Foot.Y - 6f), 40f, 6, new Color(0.30f, 0.24f, 0.26f, 0.7f), 0.7f, 14f);
			}
		}

		if (!death.Crossed(BurstAt))
			return;

		var heartArt = new Vector2(47.5f, 40f);
		death.Emit(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.ArmourA, Variants = 2, Tinted = true, SpeedMin = 90f, SpeedMax = 280f,
			UpMin = 120f, UpMax = 300f, Gravity = 680f, Bounce = 0.3f, Friction = 6f, Spin = 7f,
			Life = DeathSeconds - BurstAt, Fade = 0.5f, DepthJitter = 16f,
		}, 14, heartArt, new Vector2(26f, 24f));
		death.Emit(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.Spike, Tinted = true, SpeedMin = 70f, SpeedMax = 200f, UpMin = 140f, UpMax = 260f,
			Bounce = 0.25f, Friction = 6f, Spin = 8f, Life = DeathSeconds - BurstAt, Fade = 0.5f,
		}, 5, new Vector2(47.5f, 14f), new Vector2(36f, 6f));
		death.Emit(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.Slag, SpeedMin = 40f, SpeedMax = 150f, UpMin = 80f, UpMax = 200f,
			Bounce = 0f, Friction = 9f, Life = DeathSeconds - BurstAt, Fade = 0.6f,
		}, 8, heartArt, new Vector2(18f, 18f));
		death.Emit(new BossDeath.Throw
		{
			Size = 4f, Color = fire.Core, ColorB = fire.Mid, Light = true, SpeedMin = 60f, SpeedMax = 260f,
			UpMin = 120f, UpMax = 320f, Gravity = 520f, Bounce = 0f, Friction = 8f, Life = 0.9f, Fade = 0.6f,
		}, 26, heartArt, new Vector2(10f, 10f));
		Vector2 heart = death.ArtToLocal(heartArt);
		death.PuffAt(heart, 16f, 70f, 0.3f, new Color(fire.Hot, 0.8f), 0f, 0f, true);
		death.PuffAt(heart, 10f, 50f, 0.3f, new Color(fire.Core, 0.9f), 0f, 0f, true);
		for (int i = 0; i < 7; i++)
		{
			Vector2 at = heart + new Vector2(death.Rand(-50f, 50f), death.Rand(-40f, 30f));
			death.PuffAt(at, 10f, 30f, 0.9f, new Color(0.16f, 0.10f, 0.11f, 0.7f), 40f, death.Rand(0.05f, 0.3f));
		}
	}

	private static void ThrowPauldron(BossDeath.Piece pauldron, float t, float side, float shudder, float s)
	{
		float u = BossDeath.Span(t, BurstAt, PauldronsLand);
		// Out and up, then down onto the ground at its feet: 49 rows from where it sat to the ground.
		float arc = 4f * u * (1f - u);
		pauldron.Offset = new Vector2(shudder + side * 62f * s * BossDeath.EaseOut(u), (49f * u * u - 34f * arc) * s);
		pauldron.Rotation = side * Mathf.DegToRad(75f) * BossDeath.EaseOut(u);
		float cool = BossDeath.Span(t, 1.0f, 2.0f);
		pauldron.Color = new Color(1f - 0.55f * cool, 1f - 0.6f * cool, 1f - 0.58f * cool, 1f - BossDeath.Span(t, 2.1f, 2.7f));
	}

	// Fire running out of the heart along every seam, before it bursts.
	protected internal override void DrawDeathLight(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathCore == null || t >= BurstAt + 0.1f)
			return;
		ElementColors.Ramp fire = ElementColors.GetRamp(Element.Fire);
		float fade = 1f - BossDeath.Span(t, BurstAt, BurstAt + 0.1f);
		for (int i = 0; i < deathSeams.Length; i++)
		{
			float grown = BossDeath.EaseOut(BossDeath.Span(t, i * 0.03f, 0.5f + i * 0.01f));
			death.DrawCrack(canvas, deathCore, deathSeams[i], grown, new Color(fire.Hot, fade), new Color(fire.Mid, 0.55f * fade));
		}
		float r = 10f + 14f * BossDeath.Span(t, 0f, BurstAt);
		BossDeath.Disc(canvas, death.ArtToLocal(new Vector2(47.5f, 40f)), r, r, new Color(fire.Hot, 0.7f * fade));
	}
}
