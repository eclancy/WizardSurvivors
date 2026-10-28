using Godot;
using System;
using WizardSurvivors.scripts;

// The stage boss. Everything about chasing, damage, status and death already lives in Enemy, so
// this only adds what makes a boss a boss: an identity the run result can record, a telegraphed
// attack that punishes standing in the wrong place, health thresholds that change how it fights,
// and a signal the run listens for so a kill can end the level in victory.
//
// Used bare, it is Elderbark: a slow, immense melee threat with one ground slam and one enrage. It
// is also the base every other chapter boss subclasses, and the division of labour between them is
// deliberate. THIS CLASS OWNS THE CLOCK - cooldown, wind-up, phase thresholds, the victory signal -
// and a subclass owns only what is different about its fight. A boss that needed its own copy of
// any of the above would be the third place in the project where a wind-up got written by hand.
//
// The five hooks a subclass has, and nothing else:
//
//   BuildSlam()            what the telegraphed attack IS - shape, reach, damage, colour
//   BuildPhaseThresholds() the health fractions where the fight changes
//   OnPhaseEntered(n)      what changes at each one
//   PlantsDuringWindUp     false for a boss that keeps moving through its own tell (a charge)
//   WantsToAttack(...)     when the wind-up is allowed to begin
//
// See .ai/enemy-behaviour.md for the eight chapter sketches and which primitive each needs.
public partial class BossEnemy : Enemy
{
	[Signal] public delegate void BossDefeatedEventHandler(string bossId);

	[Export] public string BossId { get; set; } = string.Empty;
	[Export] public string BossDisplayName { get; set; } = "Boss";

	// Ground slam: the boss plants its feet, a warning grows for the telegraph, then everything
	// inside it takes a heavy hit. The telegraph is the whole fight - the slam is only unfair if the
	// player cannot see it coming.
	[Export] public float SlamIntervalSeconds { get; set; } = 4.6f;
	[Export] public float SlamTelegraphSeconds { get; set; } = 0.85f;
	[Export] public float SlamRadius { get; set; } = 150f;
	[Export] public int SlamDamage { get; set; } = 4;

	// Below this fraction of max health the boss speeds up and slams harder. One threshold by
	// default, not three phases: the first boss should teach "it gets worse near the end", not a
	// dance routine. Subclasses that want a real second phase override BuildPhaseThresholds.
	[Export] public float EnrageHealthFraction { get; set; } = 0.34f;
	[Export] public float EnrageSpeedMultiplier { get; set; } = 1.35f;
	[Export] public float EnrageSlamIntervalMultiplier { get; set; } = 0.66f;

	// How far out the boss will commit to a wind-up, as a multiple of its reach. Above 1 so it
	// starts while the player is still approaching.
	[Export] public float SlamEngageRadiusMultiplier { get; set; } = 2.2f;

	// Cooldown, wind-up, warning shape and the hit itself all live in the shared telegraph, which
	// SlammerEnemy and ExploderEnemy run too.
	private GroundSlamAttack slam;
	private float[] phaseThresholds = Array.Empty<float>();
	private bool defeatAnnounced;

	/// <summary>The telegraphed attack, for subclasses that re-aim or retune it mid-fight.</summary>
	protected GroundSlamAttack Slam => slam;

	/// <summary>
	/// How many thresholds have been crossed. 0 is the opening fight; 1 is the enrage on a plain
	/// boss. Read it in <c>_PhysicsProcess</c> to gate behaviour a later phase adds.
	/// </summary>
	protected int CurrentPhase { get; private set; }

	/// <summary>The same number, readable from outside. For the HUD and for _BossProbe.</summary>
	public int BossPhase => CurrentPhase;

	/// <summary>
	/// What the boss health bar shows, 0 to 1. Its own health for every ordinary boss, and the
	/// whole set for a boss that is more than one body.
	/// </summary>
	/// <remarks>
	/// The HUD reads this rather than <c>HealthFraction</c> because the Hollow Choir is three linked
	/// bodies and the bar has to mean the fight rather than whichever third the run holds a
	/// reference to - a bar that jumped back to full on a revive would be lying about progress the
	/// player really had made.
	/// </remarks>
	public virtual float BossHealthFraction => HealthFraction;

	// Softer debuff ceilings than an ordinary enemy's, set here so a boss scene that says nothing
	// still gets them; a .tscn value overrides. Same reasoning as MinSlowMultiplier: a quarter of
	// an ordinary enemy's health is one shard, a quarter of a boss is a quarter of the fight.
	public BossEnemy()
	{
		MaxWitherFraction = 0.08f;
		MaxWeakenFraction = 0.25f;
	}

	public override void _Ready()
	{
		// Bosses are always elites as far as the rest of the game is concerned: the gold ring marker,
		// the fat XP drop and the elite headcount all key off this. Set before base._Ready, which is
		// where Enemy builds the marker.
		IsMiniBoss = true;
		base._Ready();
		slam = BuildSlam();
		phaseThresholds = BuildPhaseThresholds() ?? Array.Empty<float>();
	}

	/// <summary>
	/// The telegraphed attack this boss opens with. Override to change its shape - a cone or a lane
	/// tells the player which way to dodge, which a ring cannot.
	/// </summary>
	protected virtual GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, new Color(1.0f, 0.45f, 0.20f));

	/// <summary>
	/// Health fractions, descending, at which the fight changes. The default is the single enrage
	/// threshold, so a boss that overrides nothing behaves exactly as Elderbark always has.
	/// </summary>
	protected virtual float[] BuildPhaseThresholds() => new[] { EnrageHealthFraction };

	/// <summary>
	/// Called once when the boss crosses into phase <paramref name="phase"/> (1-based). The default
	/// is the enrage: faster on its feet, faster on its slam, and visibly hotter so the change in
	/// pace has a cause rather than feeling like a bug.
	/// </summary>
	protected virtual void OnPhaseEntered(int phase) => ApplyEnrage();

	/// <summary>
	/// Whether the boss holds still through its wind-up. True for anything that draws its warning on
	/// the ground it is standing on; false for a charge, which carries the warning with it.
	/// </summary>
	/// <remarks>
	/// This is the whole of the travelling-telegraph primitive. <see cref="GroundSlamAttack"/>
	/// already draws on the caster and resolves at the caster's position on the frame it lands, so
	/// a boss that simply does not plant has a moving attack with no new machinery at all.
	/// </remarks>
	protected virtual bool PlantsDuringWindUp => true;

	/// <summary>
	/// Whether a wind-up may begin this frame. The default only commits when the player is close
	/// enough that the attack could plausibly land, so the boss does not spend the fight rooted in
	/// place slamming empty ground.
	/// </summary>
	protected virtual bool WantsToAttack(Node2D target, float distanceToPlayer) =>
		distanceToPlayer <= SlamRadius * SlamEngageRadiusMultiplier;

	/// <summary>Called on the frame the wind-up begins. Aim here, not during it.</summary>
	protected virtual void OnAttackStarted(Node2D target)
	{
		PlayAttackAnimation();
		if (target != null && IsInstanceValid(target))
			slam.AimAt(GlobalPosition, target.GlobalPosition);
	}

	/// <summary>Called on the frame the attack lands, exactly once per wind-up.</summary>
	protected virtual void OnAttackResolved(Node2D target) => slam.ResolveAgainst(this, target);

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickPhases();
		TickSlam((float)delta);
	}

	// Planting the boss for its wind-up is the base class's job: returning Zero holds it exactly
	// where the warning is drawn, which is the only place the attack is honest. A boss that opts out
	// of planting keeps chasing, and the warning travels with it.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		return PlantsDuringWindUp && slam != null && slam.IsWindingUp ? Vector2.Zero : chaseDirection;
	}

	// Thresholds are crossed in order and only ever forward: a boss healed back above one does not
	// un-enrage, because a phase that can be undone teaches the player nothing.
	private void TickPhases()
	{
		while (CurrentPhase < phaseThresholds.Length && HealthFraction <= phaseThresholds[CurrentPhase])
		{
			CurrentPhase++;
			OnPhaseEntered(CurrentPhase);
		}
	}

	/// <summary>The default phase change: faster, harder, hotter. Callable from an override.</summary>
	protected void ApplyEnrage()
	{
		Speed *= EnrageSpeedMultiplier;
		SlamIntervalSeconds *= EnrageSlamIntervalMultiplier;
		if (slam != null)
			slam.IntervalSeconds = SlamIntervalSeconds;
		SetBaseModulate(new Color(1.0f, 0.62f, 0.52f));
	}

	private void TickSlam(float delta)
	{
		Node2D target = TargetPlayer;
		bool valid = target != null && IsInstanceValid(target);
		float distance = valid ? GlobalPosition.DistanceTo(target.GlobalPosition) : float.MaxValue;

		switch (slam.Tick(delta, valid && WantsToAttack(target, distance)))
		{
			case AttackTelegraph.Beat.Started:
				OnAttackStarted(target);
				break;
			case AttackTelegraph.Beat.Resolved:
				OnAttackResolved(target);
				break;
		}
	}

	// The warning is drawn on the boss itself rather than as a separate node, so an attack
	// allocates nothing - this runs in a scene that already has a swarm in it.
	//
	// Never on a corpse. A boss killed mid wind-up stops its clock with the warning half grown, and a
	// warning that stays drawn through the fall says a hit is coming that never will.
	public override void _Draw()
	{
		base._Draw();
		if (!IsDying)
			slam?.Draw(this);
	}

	// A boss death is a run-defining event, so it skips the throttling and the heavy/small
	// split that ordinary deaths go through and plays its own sound outright.
	protected override void PlayDeathSound()
	{
		SfxPlayer.AtPosition(SfxCatalog.BossDeath, GlobalPosition, 0.0f);
	}

	protected override void StartDeath()
	{
		bool alreadyDying = defeatAnnounced;
		defeatAnnounced = true;
		base.StartDeath();

		// Guard the emit rather than the call: base.StartDeath is itself re-entrant-safe, but the
		// run must never be told it won twice.
		if (!alreadyDying)
			EmitSignal(SignalName.BossDefeated, BossId);
	}

	// --- The fall ---------------------------------------------------------------------------------
	//
	// "Bosses should have cool death animations that relate to what they are" (2026-10-28). Every boss
	// used to end on the same six frames - the body squashed into the floor and darkened - which said
	// "an enemy died" about fifteen minutes of fight with something that had a name. Now each one
	// falls as what it is: this tree splits and topples, the Gaoler's keys scatter, the Choir's voices
	// go out one by one. BossDeath is the stage; the play is written here and in each subclass.
	//
	// Presentation only, and after the fact: Enemy.StartDeath has already dropped the rewards, left
	// the group and zeroed the collision on the killing frame, exactly as before. Nothing below can
	// touch the player, and the victory flow (Node2DGame) hears BossDefeated on the same frame it
	// always did. The corpse is freed when the fall ends instead of when the strip did.

	protected override bool StagesOwnDeath => true;

	protected override void BeginOwnDeath()
	{
		// The telegraph and anything else _Draw paints were drawn by a clock that has just stopped;
		// one redraw now that IsDying is set takes them off the corpse.
		QueueRedraw();
		Node2D witness = TargetPlayer;
		var death = new BossDeath();
		death.Configure(this, BodySprite, BodySpriteRestPosition, DeathDelaySeconds, DeathSeconds,
			witness != null && IsInstanceValid(witness) ? witness.GlobalPosition : (Vector2?)null);
		AddChild(death);
	}

	/// <summary>How long the fall lasts once it begins. About the length of boss_death (2.4 s).</summary>
	protected internal virtual float DeathSeconds => 2.7f;

	/// <summary>Seconds the body holds before it begins to fall. The Hollow Choir's voices take turns.</summary>
	protected internal virtual float DeathDelaySeconds => 0f;

	/// <summary>True for a boss whose own <c>_Draw</c> is part of its fall (the Long Coil's body).</summary>
	protected internal virtual bool RedrawsWhileDying => false;

	// ELDERBARK: FELLED. It is the forest with something looking out of it, and it goes the way a
	// great tree goes - except that it splits first. The cold mark that was looking out of the knot
	// runs down the trunk as a seam of light, the trunk parts along it, and the two halves fall away
	// from each other like a lightning-split oak, each hinged on its own foot. The crown sheds its dead
	// leaves into the air as it goes, the bones it had grown over drop out of the hollow where the
	// ribcage was, and where each half hits the ground it throws bark and dust. Then the wood goes
	// dark and the leaves lie where they fell. sprite_treant.py: bark on stone, moss on lichen, bone
	// caught in the trunk - every piece of debris is one of those three.
	private BossDeath.Piece fellLeft;
	private BossDeath.Piece fellRight;
	private BossDeath.Piece fellRoots;

	// The two outer feet of the trunk at the root line, which each half tips over on, and the row the
	// trunk is cut from its roots.
	private const int FellRootRow = 82;
	private const int FellLeftFoot = 24;
	private const int FellRightFoot = 72;
	private const float FellStart = 0.38f;
	private const float FellLand = 1.04f;

	private static readonly Color FellDust = new(0.48f, 0.54f, 0.50f, 0.6f);

	/// <summary>Called once, on the first frame of the fall: cut the body, throw what goes at once.</summary>
	protected internal virtual void OnDeathBegin(BossDeath death)
	{
		fellRoots = death.Cut(new Vector2I(48, 94), new Rect2I(0, FellRootRow, 96, 96 - FellRootRow));
		fellLeft = death.Cut(new Vector2I(FellLeftFoot, FellRootRow), new Rect2I(0, 0, 48, FellRootRow));
		fellRight = death.Cut(new Vector2I(FellRightFoot, FellRootRow), new Rect2I(48, 0, 48, FellRootRow));
	}

	/// <summary>Every frame of the fall, before the pieces are placed. <paramref name="t"/> is its own clock.</summary>
	protected internal virtual void TickDeath(BossDeath death, float t, float dt)
	{
		if (fellLeft == null)
			return;

		float s = death.ArtScale.X;
		// The seam opens a crack before anything falls, and the tree shudders against it.
		float shudder = t < FellStart ? BossDeath.Shudder(t, 1f - t / FellStart, 17f) * s : 0f;
		float part = BossDeath.Span(t, 0.12f, FellStart) * s;
		float fall = BossDeath.EaseIn(BossDeath.Span(t, FellStart, FellLand));
		float settle = 0.07f * MathF.Sin(Mathf.Pi * BossDeath.Span(t, FellLand, FellLand + 0.24f));
		float angle = Mathf.DegToRad(82f) * fall - settle;
		fellLeft.Offset = new Vector2(shudder - part, 0f);
		fellRight.Offset = new Vector2(shudder + part, 0f);
		fellLeft.Rotation = -angle;
		fellRight.Rotation = angle;
		fellRoots.Offset = new Vector2(shudder * 0.5f, 0f);

		// Then the wood goes cold and is gone - the roots last, where it stood.
		float dim = BossDeath.Span(t, 1.25f, 2.0f);
		var wood = new Color(1f - 0.5f * dim, 1f - 0.48f * dim, 1f - 0.4f * dim, 1f - BossDeath.Span(t, 1.85f, 2.45f));
		fellLeft.Color = wood;
		fellRight.Color = wood;
		fellRoots.Color = new Color(wood, 1f - BossDeath.Span(t, 2.05f, DeathSeconds));

		if (death.Crossed(0.16f))
		{
			// The crown sheds as the seam runs: dead leaves into the air, drifting down for two seconds.
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Leaf, Frames = 4, Fps = 7f, Tinted = true,
				SpeedMin = 20f, SpeedMax = 120f, UpMin = 40f, UpMax = 170f, Gravity = 260f, Terminal = 46f,
				Drag = 1.4f, Flutter = 7f, FlutterHz = 1.4f, Bounce = 0f, Friction = 12f,
				Life = 2.45f, Fade = 0.5f, DepthJitter = 44f,
			}, 30, new Vector2(48f, 18f), new Vector2(42f, 14f), 0f, 0.008f);
		}

		if (death.Crossed(0.46f))
		{
			// The hollow where the ribcage was is open now, and what the tree had grown over falls out.
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Bone, SpeedMin = 20f, SpeedMax = 70f, UpMin = 30f, UpMax = 90f,
				Angle = Mathf.Pi * 0.5f, Spread = Mathf.Pi, Spin = 5f, Bounce = 0.3f, Life = 2.1f, Fade = 0.4f,
			}, 4, new Vector2(48f, 68f), new Vector2(3f, 8f));
		}

		if (death.Crossed(FellLand))
		{
			// Each half lands on its crown: bark off the trunk, the leaves still on it shaken loose, dust.
			foreach (BossDeath.Piece half in new[] { fellLeft, fellRight })
			{
				bool left = half == fellLeft;
				Vector2 crown = death.PieceToLocal(half, new Vector2(left ? 22f : 74f, 20f));
				var onGround = new Vector2(crown.X, death.Foot.Y - 6f);
				float height = Mathf.Max(0f, onGround.Y - crown.Y);
				death.EmitAt(new BossDeath.Throw
				{
					Cell = BossDeath.Cell.BarkLong, Variants = 2, Tinted = true, SpeedMin = 60f, SpeedMax = 170f,
					UpMin = 90f, UpMax = 210f, Spin = 6f, Bounce = 0.35f, Life = 1.5f, Fade = 0.4f,
				}, 7, onGround, height * 0.3f, 40f);
				death.EmitAt(new BossDeath.Throw
				{
					Cell = BossDeath.Cell.Leaf, Frames = 4, Fps = 7f, Tinted = true,
					SpeedMin = 10f, SpeedMax = 70f, UpMin = 60f, UpMax = 150f, Gravity = 240f, Terminal = 44f,
					Drag = 1.6f, Flutter = 6f, Bounce = 0f, Life = 1.6f, Fade = 0.45f,
				}, 10, onGround, height * 0.6f, 50f);
				Vector2 thud = death.PieceToLocal(half, new Vector2(left ? 30f : 66f, 40f));
				death.DustRing(new Vector2(thud.X, death.Foot.Y - 8f), 56f, 8, FellDust, 0.8f, 16f);
			}
		}
	}

	/// <summary>On the ground, under every actor (absolute z -15): stains, scorches, frost.</summary>
	protected internal virtual void DrawDeathGround(BossDeath death, CanvasItem canvas, float t) { }

	/// <summary>In the air, over the body: anything that is not light.</summary>
	protected internal virtual void DrawDeathAir(BossDeath death, CanvasItem canvas, float t) { }

	/// <summary>Light, drawn additively over everything: glows, seams, sparks.</summary>
	protected internal virtual void DrawDeathLight(BossDeath death, CanvasItem canvas, float t)
	{
		if (fellLeft == null || t > 0.6f)
			return;

		// The mark leaves through the seam: a line of cold light from the crown to the roots, racing
		// down, then dying as the halves part. Jagged by a whole art pixel every few rows - a split, not
		// a ruled line.
		ElementColors.Ramp arcane = ElementColors.GetRamp(Element.Arcane);
		float run = BossDeath.EaseOut(BossDeath.Span(t, 0f, 0.3f));
		float fade = 1f - BossDeath.Span(t, 0.32f, 0.6f);
		int bottom = 6 + (int)((FellRootRow - 6) * run);
		for (int row = 6; row < bottom; row += 2)
		{
			float jag = MathF.Round(BossDeath.Hash(7u, row / 4) * 2f - 1f);
			Vector2 at = death.ArtToLocal(new Vector2(47.5f + jag, row));
			canvas.DrawRect(WhipLimb.Cell(at, BossDeath.Px * 2f), new Color(arcane.Mid, 0.75f * fade));
			canvas.DrawRect(WhipLimb.Cell(at, BossDeath.Px), new Color(arcane.Core, fade));
		}

		// And the two points of it in the knot flare once and go out.
		float eyes = BossDeath.Span(t, 0f, 0.08f) * (1f - BossDeath.Span(t, 0.12f, 0.4f));
		BossDeath.Disc(canvas, death.ArtToLocal(new Vector2(45f, 42f)), 6f, 6f, new Color(arcane.Hot, 0.8f * eyes));
		BossDeath.Disc(canvas, death.ArtToLocal(new Vector2(51f, 42f)), 6f, 6f, new Color(arcane.Hot, 0.8f * eyes));
	}
}
