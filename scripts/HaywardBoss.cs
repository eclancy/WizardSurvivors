using Godot;
using WizardSurvivors.scripts;

// Chapter 2, the Fallow Fields: THE HAYWARD (.ai/campaign.md section 5).
//
// The parish officer who kept the hedges and impounded strays, walking behind a hurdle - a woven
// fence panel - held upright before him. "He keeps his hurdle between you and him. Go round to his
// back."
//
// THE VERB NO OTHER BOSS ASKS: GO ROUND IT. Every spell in this game aims itself, so the only way
// to hit his back is to stand there. While the player is in front of him - within GuardHalfAngle of
// the way his hurdle faces - every hit is cut to GuardedDamageMultiplier; from the side and behind it
// lands in full. He turns to face the player at a limited rate, so circling at a comfortable radius
// outpaces him (the wizard walks 220 px/s: at 150 px that is 84 degrees a second against his 55).
// Elderbark asked the player to stand and deliver; this one asks him to move with intent, and
// teaches the sidestep the Gaoler asks at chapter 5 and the orbit the Archivist asks at 7.
//
// THE SHOVE: a cone in front, driven along the hurdle's facing - so it lands only where he is
// blocking, and the ground that is bad to stand on (your hits are wasted there) is the ground that
// hurts. He only winds it up when the player is in front and in reach, and plants while he does,
// with his facing locked: a cone that kept turning through its tell would have no sideways to step
// to (GroundSlamAttack.Facing).
//
// PHASE 2, at half health: he turns faster, and every third attack is a full SWING - a ring all
// round him - so hugging his back is answered too. The swing also comes due early when the player
// has stood behind him in reach for a while, or a player glued to his back would never see it.
//
// Built from the shared primitives only: GroundSlamAttack as a Cone and a Ring, AdjustSteering for
// the plant, and Enemy.AdjustIncomingDamage - the one shared hook the fight needed, and nothing else
// in Enemy.cs. Read with .ai/enemy-behaviour.md, "The eight chapter bosses".
public partial class HaywardBoss : BossEnemy
{
	/// <summary>Half-angle of the front his hurdle covers. Hits are cut while the player is inside it.</summary>
	[Export] public float GuardHalfAngleDegrees { get; set; } = 60f;

	/// <summary>What a hit is worth while the player stands in front of the hurdle.</summary>
	[Export] public float GuardedDamageMultiplier { get; set; } = 0.2f;

	/// <summary>How fast he turns to face the player, degrees a second. The circling player's margin.</summary>
	[Export] public float TurnDegreesPerSecond { get; set; } = 55f;

	/// <summary>The turn rate from phase 2 on.</summary>
	[Export] public float PhaseTwoTurnDegreesPerSecond { get; set; } = 75f;

	/// <summary>Half-angle of the shove. Under the guard's, so the shove lands only where he blocks.</summary>
	[Export] public float ShoveHalfAngleDegrees { get; set; } = 50f;

	/// <summary>Reach of the phase 2 swing, a ring all round him.</summary>
	[Export] public float SwingRadius { get; set; } = 120f;

	/// <summary>In phase 2, every this-many-th attack is the swing.</summary>
	[Export] public int SwingEvery { get; set; } = 3;

	/// <summary>Seconds a player may stand behind him, in reach, before the swing comes due anyway.</summary>
	[Export] public float BackHuggingSeconds { get; set; } = 2.5f;

	/// <summary>Where the fight changes.</summary>
	[Export] public float PhaseTwoHealthFraction { get; set; } = 0.5f;

	// The hurdle's facing, a world direction. Starts toward the player at _Ready and only ever turns
	// at the turn rate.
	private Vector2 guardFacing = Vector2.Right;
	private float turnRate;
	private bool phaseTwo;
	private int attacksSinceSwing;
	private float behindSeconds;
	private bool swingDue;
	private bool swinging;
	// Seconds left on the brief flare drawn along the guarded front when a hit is cut. The read that
	// tells a player WHY the numbers went small.
	private float blockFlash;
	private float guardHalfAngleRadians;
	private AnimatedSprite2D? sprite;
	private readonly RandomNumberGenerator damageRng = new();

	// Straw and old wood: the shove's warning. Warm, against the cold of every taken thing's mark.
	private static readonly Color ShoveColor = new(0.94f, 0.68f, 0.26f);
	// The swing is the same verb's other answer, a shade hotter so the ring and the cone read apart.
	private static readonly Color SwingColor = new(1.0f, 0.5f, 0.22f);
	private static readonly Color GuardColor = new(0.86f, 0.74f, 0.48f);

	// The drawn guard: a dashed arc on the ground round his feet, spanning the guarded front. The
	// sprite can only face left or right; the arc says exactly where "in front" is. An ellipse,
	// because it lies on the floor in three-quarter view, centred where his boots are (the node is
	// the middle of a 192 px sprite, 92 px above them) - drawn round the node it was a halo.
	private static readonly Vector2 GuardArcCentre = new(0f, 78f);
	private const float GuardArcRadiusX = 104f;
	private const float GuardArcRadiusY = 46f;
	private const int GuardArcDashes = 9;
	private const float BlockFlashSeconds = 0.18f;

	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, ShoveColor)
		{
			Shape = TelegraphShape.Cone,
			HalfAngleDegrees = ShoveHalfAngleDegrees,
		};

	protected override float[] BuildPhaseThresholds() => new[] { PhaseTwoHealthFraction };

	public override void _Ready()
	{
		base._Ready();
		turnRate = TurnDegreesPerSecond;
		guardHalfAngleRadians = Mathf.DegToRad(Mathf.Clamp(GuardHalfAngleDegrees, 1f, 179f));
		damageRng.Randomize();
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		Node2D target = TargetPlayer;
		if (target != null && IsInstanceValid(target))
		{
			Vector2 toPlayer = target.GlobalPosition - GlobalPosition;
			if (toPlayer.LengthSquared() > 0.01f)
				guardFacing = toPlayer.Normalized();
		}
		// The shove must land only where he blocks: a cone wider than the guard would hurt a player
		// who had already stepped out of the front. RegressionChecks has no case for this boss (it
		// is a shared file), so the rule is held here.
		if (ShoveHalfAngleDegrees >= 90f || ShoveHalfAngleDegrees > GuardHalfAngleDegrees)
			GD.PushError($"HaywardBoss: ShoveHalfAngleDegrees {ShoveHalfAngleDegrees} must be under 90 and within the guard's {GuardHalfAngleDegrees}.");
	}

	protected override void OnPhaseEntered(int phase)
	{
		if (phase != 1)
			return;
		phaseTwo = true;
		turnRate = PhaseTwoTurnDegreesPerSecond;
		// Warmer: the change has a cause on screen, as every boss's does (BossEnemy.ApplyEnrage).
		SetBaseModulate(new Color(1.0f, 0.84f, 0.72f));
		// It announces itself: the first attack of the new phase is the new attack.
		swingDue = true;
		Slam.Prime();
	}

	/// <summary>The way the hurdle faces, a world direction. For probes and photographs.</summary>
	public Vector2 GuardFacing => guardFacing;

	/// <summary>True while the player stands inside the guarded front.</summary>
	public bool PlayerInFront
	{
		get
		{
			Node2D target = TargetPlayer;
			if (target == null || !IsInstanceValid(target))
				return false;
			Vector2 toPlayer = target.GlobalPosition - GlobalPosition;
			if (toPlayer.LengthSquared() < 1f)
				return true;
			return Mathf.Abs(guardFacing.AngleTo(toPlayer)) <= guardHalfAngleRadians;
		}
	}

	// THE HURDLE. Position, not projectile direction: every spell aims itself, so where the player
	// stands is the only choice he has, and it is the one this fight is about. Rounded by chance,
	// not to nearest, as Enemy.ScaleOutgoingDamage is - a 3-damage hit at x0.2 must average 0.6, not
	// round to 1 every time and make the hurdle a 33% cut against small hits.
	protected override int AdjustIncomingDamage(int amount)
	{
		if (amount <= 0 || !PlayerInFront)
			return amount;

		float scaled = amount * Mathf.Clamp(GuardedDamageMultiplier, 0f, 1f);
		int whole = Mathf.FloorToInt(scaled);
		blockFlash = BlockFlashSeconds;
		return damageRng.Randf() < scaled - whole ? whole + 1 : whole;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		base._PhysicsProcess(delta);
		if (blockFlash > 0f)
			blockFlash -= dt;
		TickTurn(dt);
		TickBackHugging(dt);
		// The sprite can only face left or right, and Enemy faces it toward the player the moment he
		// crosses. That would show the hurdle toward the player while the guard still faced away, so
		// the sprite follows the guard instead - it turns when he has turned.
		if (sprite != null)
			sprite.FlipH = SpriteFacesRightByDefault ? guardFacing.X < 0f : guardFacing.X > 0f;
	}

	private void TickTurn(float dt)
	{
		// Locked through a wind-up: the shove goes where the hurdle pointed when it began.
		if (Slam != null && Slam.IsWindingUp)
			return;
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
			return;
		Vector2 toPlayer = target.GlobalPosition - GlobalPosition;
		if (toPlayer.LengthSquared() < 1f)
			return;
		float diff = guardFacing.AngleTo(toPlayer);
		float step = Mathf.DegToRad(turnRate) * dt;
		guardFacing = guardFacing.Rotated(Mathf.Clamp(diff, -step, step)).Normalized();
	}

	// Phase 2 only: standing in reach behind him brings the swing due, so the answer to "go round"
	// is never "and then stand there forever".
	private void TickBackHugging(float dt)
	{
		if (!phaseTwo || swingDue)
			return;
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
			return;
		bool behindInReach = !PlayerInFront && GlobalPosition.DistanceTo(target.GlobalPosition) <= SwingRadius;
		behindSeconds = behindInReach ? behindSeconds + dt : Mathf.Max(0f, behindSeconds - dt);
		if (behindSeconds >= BackHuggingSeconds)
			swingDue = true;
	}

	protected override bool WantsToAttack(Node2D target, float distanceToPlayer)
	{
		if (swingDue)
			return distanceToPlayer <= SwingRadius;
		// The shove: only at a player in front and in reach of the hurdle.
		return PlayerInFront && distanceToPlayer <= SlamRadius;
	}

	protected override void OnAttackStarted(Node2D target)
	{
		PlayAttackAnimation();
		swinging = swingDue;
		if (swinging)
		{
			Slam.Shape = TelegraphShape.Ring;
			Slam.Radius = SwingRadius;
			Slam.WarningColor = SwingColor;
			swingDue = false;
			behindSeconds = 0f;
			attacksSinceSwing = 0;
		}
		else
		{
			Slam.Shape = TelegraphShape.Cone;
			Slam.Radius = SlamRadius;
			Slam.HalfAngleDegrees = ShoveHalfAngleDegrees;
			Slam.WarningColor = ShoveColor;
			// Along the hurdle, not at the player: it is the hurdle being driven forward.
			Slam.Facing = guardFacing;
			if (phaseTwo && ++attacksSinceSwing >= SwingEvery - 1)
				swingDue = true;
		}
	}

	// The guard is an arc on the ground, under every body, with the slam over it (Enemy.DrawGroundTells).
	protected override void DrawGroundTells(CanvasItem floor)
	{
		DrawGuard(floor);
		base.DrawGroundTells(floor);
	}

	// The guarded front as a dashed arc on the ground: faint at rest, bright for a moment when it
	// turns a hit. Drawn in local space (enemies never rotate, so a world direction is a local one),
	// and before the slam's warning so the warning is always on top of it.
	private void DrawGuard(CanvasItem floor)
	{
		if (IsDying)
			return;
		float flash = Mathf.Clamp(blockFlash / BlockFlashSeconds, 0f, 1f);
		var color = new Color(GuardColor, 0.30f + 0.5f * flash);
		float width = 3f + 2f * flash;
		float start = guardFacing.Angle() - guardHalfAngleRadians;
		float span = guardHalfAngleRadians * 2f;
		const int stepsPerDash = 4;
		for (int k = 0; k < GuardArcDashes; k++)
		{
			float a0 = start + k * span / GuardArcDashes;
			float a1 = a0 + span / GuardArcDashes * 0.62f;
			for (int i = 0; i < stepsPerDash; i++)
			{
				Vector2 p0 = OnGround(a0 + (a1 - a0) * i / stepsPerDash, 1f);
				Vector2 p1 = OnGround(a0 + (a1 - a0) * (i + 1) / stepsPerDash, 1f);
				floor.DrawLine(p0, p1, color, width);
			}
		}
		// The two ends, a short tick outward, so the edge of the front - the line to get across -
		// is the clearest part of it.
		for (int side = -1; side <= 1; side += 2)
		{
			float a = guardFacing.Angle() + side * guardHalfAngleRadians;
			floor.DrawLine(OnGround(a, 0.86f), OnGround(a, 1.14f), color, 3f);
		}
	}

	// A point on the guard's ellipse at world angle a, scaled out by k.
	private static Vector2 OnGround(float a, float k) =>
		GuardArcCentre + new Vector2(Mathf.Cos(a) * GuardArcRadiusX, Mathf.Sin(a) * GuardArcRadiusY) * k;

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// THE HURDLE GOES DOWN. He was the parish officer behind his woven panel, and the fight was about
	// the panel; so it is the panel that falls first and flattest - toppling away from him onto the
	// stubble, bursting straw and withies where it lands. His hat is knocked off and spins away behind
	// him, his crook drops the other way, and the man himself folds down onto his knees where he
	// stood. What he kept out is let in. Straw and old wood, from cast_fields.py's gold row.
	private BossDeath.Piece deathCrook;
	private BossDeath.Piece deathHat;
	private BossDeath.Piece deathMan;
	private BossDeath.Piece deathHurdle;

	private const float HurdleLands = 0.7f;
	private const float CrookLands = 0.82f;
	private const float HatLands = 0.86f;
	private static readonly Color StubbleDust = new(0.62f, 0.55f, 0.40f, 0.55f);

	protected internal override float DeathSeconds => 2.7f;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		// Four pieces that do not overlap, cut on the drawing (boss_hayward.py): the crook on the left,
		// the hat, the man under it, and the hurdle with the stake tips above it.
		deathCrook = death.Cut(new Vector2I(19, 94), new Rect2I(0, 0, 23, 96));
		deathHat = death.Cut(new Vector2I(42, 12), new Rect2I(26, 2, 32, 13));
		deathMan = death.Cut(new Vector2I(36, 94), new Rect2I(23, 15, 25, 81));
		deathHurdle = death.Cut(new Vector2I(68, 92), new Rect2I(48, 15, 48, 81), new Rect2I(58, 0, 38, 15));
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathHurdle == null)
			return;

		float s = death.ArtScale.X;
		// The hurdle topples away from him, flat onto the field, with a little bounce.
		float topple = BossDeath.EaseIn(BossDeath.Span(t, 0.1f, HurdleLands));
		float bounce = 0.07f * Mathf.Sin(Mathf.Pi * BossDeath.Span(t, HurdleLands, HurdleLands + 0.2f));
		deathHurdle.Rotation = Mathf.DegToRad(86f) * topple - bounce;

		// His crook drops the other way.
		deathCrook.Rotation = -Mathf.DegToRad(80f) * BossDeath.EaseIn(BossDeath.Span(t, 0.18f, CrookLands));

		// The hat goes up and spins away behind him: once round, and down on its brim, a little tilted.
		float hat = BossDeath.Span(t, 0.04f, HatLands);
		float arc = 4f * hat * (1f - hat);
		deathHat.Offset = new Vector2(-46f * s * hat, (80f * hat * hat - 26f * arc) * s);
		deathHat.Rotation = -Mathf.DegToRad(375f) * BossDeath.EaseOut(hat);

		// And he folds down onto his knees, slumping toward where his hurdle was.
		float fold = BossDeath.EaseInOut(BossDeath.Span(t, 0.3f, 1.1f));
		deathMan.Sink(death, 26f * fold);
		deathMan.Rotation = Mathf.DegToRad(8f) * fold;

		float dim = BossDeath.Span(t, 1.05f, 1.75f);
		var dusk = new Color(1f - 0.5f * dim, 1f - 0.5f * dim, 1f - 0.4f * dim, 1f);
		deathMan.Color = new Color(dusk, 1f - BossDeath.Span(t, 1.6f, 2.2f));
		var gone = new Color(dusk, 1f - BossDeath.Span(t, 1.9f, 2.5f));
		deathHurdle.Color = gone;
		deathCrook.Color = gone;
		deathHat.Color = gone;

		if (death.Crossed(0.02f))
		{
			// The last blow, through the weave.
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.StrawA, Variants = 3, Tinted = true, SpeedMin = 40f, SpeedMax = 150f,
				UpMin = 60f, UpMax = 180f, Gravity = 520f, Drag = 0.8f, Bounce = 0.1f, Friction = 8f, Spin = 6f,
				Life = DeathSeconds - 0.05f, Fade = 0.4f,
			}, 10, new Vector2(70f, 52f), new Vector2(14f, 28f));
		}

		if (death.Crossed(HurdleLands))
		{
			// Flat on the stubble: the weave bursts along its whole length.
			for (int k = 0; k < 4; k++)
			{
				Vector2 along = death.PieceToLocal(deathHurdle, new Vector2(68f, 84f - k * 20f));
				var ground = new Vector2(along.X, death.Foot.Y - 4f);
				death.EmitAt(new BossDeath.Throw
				{
					Cell = BossDeath.Cell.StrawA, Variants = 3, Tinted = true, SpeedMin = 30f, SpeedMax = 140f,
					UpMin = 70f, UpMax = 190f, Gravity = 560f, Drag = 0.8f, Bounce = 0.1f, Friction = 8f, Spin = 6f,
					Life = DeathSeconds - HurdleLands, Fade = 0.4f,
				}, 5, ground, 6f, 14f);
				death.PuffAt(ground, 6f, 22f, 0.6f, StubbleDust, 12f, k * 0.03f);
			}
			Vector2 mid = death.PieceToLocal(deathHurdle, new Vector2(68f, 50f));
			death.EmitAt(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Withy, Tinted = true, SpeedMin = 40f, SpeedMax = 120f, UpMin = 60f, UpMax = 140f,
				Bounce = 0.2f, Friction = 7f, Spin = 5f, Life = DeathSeconds - HurdleLands, Fade = 0.4f,
			}, 4, new Vector2(mid.X, death.Foot.Y - 4f), 6f, 30f);
		}

		if (death.Crossed(CrookLands))
		{
			Vector2 tip = death.PieceToLocal(deathCrook, new Vector2(14f, 10f));
			death.DustRing(new Vector2(tip.X, death.Foot.Y - 4f), 22f, 4, StubbleDust, 0.5f, 10f);
		}
		if (death.Crossed(HatLands))
		{
			Vector2 brim = death.PieceToLocal(deathHat, new Vector2(42f, 12f));
			death.PuffAt(brim, 6f, 18f, 0.45f, StubbleDust, 6f);
		}
	}
}
