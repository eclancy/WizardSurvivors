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
			ResurfaceRadius, ScaleOutgoingDamage(ResurfaceDamage), ArchiveColor);
	}

	protected override void StartDeath()
	{
		// A dive in progress would keep driving position out of a corpse.
		burrow?.Abort();
		base.StartDeath();
	}

	protected override void DrawGroundTells(CanvasItem floor)
	{
		base.DrawGroundTells(floor);

		if (!burrow.IsUnder)
			return;

		// A mound, so the player can track it under the floor. Three circles rather than a sprite:
		// the whole point of being under is that there is no sprite. On the floor, under every body.
		var soil = new Color(ArchiveColor.R * 0.5f, ArchiveColor.G * 0.5f, ArchiveColor.B * 0.6f, 0.55f);
		float swell = 1f + 0.18f * Mathf.Sin(burrow.Progress * Mathf.Tau * 3f);
		floor.DrawCircle(Vector2.Zero, 26f * swell, soil);
		floor.DrawArc(Vector2.Zero, 34f * swell, 0f, Mathf.Tau, 24, soil, 2.5f, true);
	}

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// IT COMES APART INTO LOOSE PAGES. A slab of record with its two tablets hung beside it, written
	// over in his violet (sprite_bosses.py). Every line of that writing flares at once as it dies -
	// the archive read out one last time - and then the tablets, which only ever floated, drop and
	// break on the floor, and the slab itself unbinds from the top down: a seam of light runs down it
	// and above the seam there is nothing left but pages, lifting off, turning over, and settling
	// across the ruins around where it stood. The last thing to go is the arch it stood on.
	private BossDeath.Piece deathSlab;
	private BossDeath.Piece deathTabletLeft;
	private BossDeath.Piece deathTabletRight;
	private float pageCarry;

	private const float UnbindStart = 0.45f;
	private const float UnbindEnd = 1.9f;
	private const float LeftTabletLands = 0.5f;
	private const float RightTabletLands = 0.62f;
	private const float PagesPerSecond = 26f;
	private static readonly Color RuinDust = new(0.5f, 0.48f, 0.6f, 0.55f);

	protected internal override float DeathSeconds => 2.8f;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		deathTabletLeft = death.Cut(new Vector2I(15, 59), new Rect2I(0, 30, 30, 34));
		deathSlab = death.Cut(new Vector2I(47, 94), new Rect2I(30, 0, 34, 96));
		deathTabletRight = death.Cut(new Vector2I(78, 49), new Rect2I(64, 20, 32, 32));
		pageCarry = 0f;
	}

	private float UnbindRow(float t) => 6f + 90f * BossDeath.EaseInOut(BossDeath.Span(t, UnbindStart, UnbindEnd));

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathSlab == null)
			return;

		float s = death.ArtScale.Y;
		ElementColors.Ramp arcane = ElementColors.GetRamp(Element.Arcane);
		// Every line of the writing at once: the record read out as it ends.
		float read = BossDeath.Span(t, 0f, 0.1f) * (1f - BossDeath.Span(t, 0.3f, 0.6f));
		var flare = new Color(arcane.Mid, 0.75f * read);
		deathTabletLeft.Glow = flare;
		deathTabletRight.Glow = flare;
		deathSlab.Glow = new Color(arcane.Mid, 0.35f * read);

		// The tablets only ever floated. They drop, tipping as they go, and break.
		DropTablet(deathTabletLeft, t, 0.12f, LeftTabletLands, 94f - 59f, -8f, s);
		DropTablet(deathTabletRight, t, 0.2f, RightTabletLands, 94f - 49f, 10f, s);

		// The slab unbinds from the top down.
		deathSlab.VisibleTop = UnbindRow(t);

		if (death.Crossed(LeftTabletLands))
			BreakTablet(death, deathTabletLeft, 14f);
		if (death.Crossed(RightTabletLands))
			BreakTablet(death, deathTabletRight, 78f);

		// Pages off the seam for as long as there is slab above the arch to give them.
		if (t >= UnbindStart && t < UnbindEnd)
		{
			pageCarry += PagesPerSecond * dt;
			float row = UnbindRow(t);
			while (pageCarry >= 1f)
			{
				pageCarry -= 1f;
				death.Emit(new BossDeath.Throw
				{
					Cell = BossDeath.Cell.Page, Frames = 4, Fps = 9f, SpeedMin = 20f, SpeedMax = 100f,
					UpMin = 70f, UpMax = 170f, Gravity = 170f, Terminal = 40f, Drag = 1.2f, Flutter = 8f,
					FlutterHz = 1.2f, Bounce = 0f, Friction = 10f, Life = DeathSeconds - t, Fade = 0.5f,
					DepthJitter = 54f,
				}, 1, new Vector2(47f, row), new Vector2(13f, 1f));
			}
		}
	}

	private static void DropTablet(BossDeath.Piece tablet, float t, float from, float lands, float rows, float tipDegrees, float s)
	{
		float u = BossDeath.EaseIn(BossDeath.Span(t, from, lands));
		tablet.Offset = new Vector2(0f, rows * s * u);
		tablet.Rotation = Mathf.DegToRad(tipDegrees) * u;
		tablet.Visible = t < lands;
	}

	private static void BreakTablet(BossDeath death, BossDeath.Piece tablet, float x)
	{
		Vector2 at = death.PieceToLocal(tablet, new Vector2(x, 50f));
		var ground = new Vector2(at.X, death.Foot.Y - 4f);
		death.EmitAt(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.TabletShardA, Variants = 2, Tinted = true, SpeedMin = 50f, SpeedMax = 160f,
			UpMin = 70f, UpMax = 170f, Bounce = 0.3f, Friction = 7f, Spin = 6f, Life = 2.0f, Fade = 0.4f,
		}, 7, ground, 4f, 18f);
		death.EmitAt(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.Page, Frames = 4, Fps = 9f, SpeedMin = 20f, SpeedMax = 80f, UpMin = 80f, UpMax = 150f,
			Gravity = 170f, Terminal = 40f, Drag = 1.2f, Flutter = 8f, Bounce = 0f, Friction = 10f, Life = 2.0f, Fade = 0.5f,
		}, 5, ground, 6f, 16f);
		death.DustRing(ground, 34f, 6, RuinDust, 0.6f, 12f);
	}

	// The seam it unbinds along: a line of its own violet across the slab, flickering.
	protected internal override void DrawDeathLight(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathSlab == null)
			return;

		ElementColors.Ramp arcane = ElementColors.GetRamp(Element.Arcane);
		float read = BossDeath.Span(t, 0f, 0.06f) * (1f - BossDeath.Span(t, 0.2f, 0.45f));
		BossDeath.Disc(canvas, death.ArtToLocal(new Vector2(47.5f, 26f)), 7f, 7f, new Color(arcane.Hot, 0.9f * read));

		if (t < UnbindStart || t > UnbindEnd + 0.1f)
			return;
		float row = UnbindRow(t);
		float flicker = 0.75f + 0.25f * Mathf.Sin(t * 41f);
		Vector2 a = death.ArtToLocal(new Vector2(33f, row));
		Vector2 b = death.ArtToLocal(new Vector2(62f, row));
		BossDeath.Line(canvas, a, b, new Color(arcane.Mid, 0.6f * flicker), BossDeath.Px * 2f);
		BossDeath.Line(canvas, a, b, new Color(arcane.Core, 0.9f * flicker));
	}
}
