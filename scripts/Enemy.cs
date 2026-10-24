using Godot;
using System;
using WizardSurvivors.scripts;

public partial class Enemy : CharacterBody2D
{
	private Vector2 knockbackVelocity = Vector2.Zero;
	private float knockbackTime = 0f;
	private const float KnockbackDuration = 0.45f;
	private float shockTimeRemaining = 0f;
	private float shockAmplitude = 0f;
	private float shockFrequency = 0f;
	private float shockPhase = 0f;
	// Slow/root status (issue #16/#22 defensive spells): multiplier 0 = fully rooted.
	private float slowMultiplier = 1f;
	private float slowTimeRemaining = 0f;
	private float vulnerableTimeRemaining = 0f;
	private float vulnerableBonus = 0f;
	// Weaken (issue #66): the fraction this enemy's outgoing damage is cut by while it lasts.
	private float weakenFraction = 0f;
	private float weakenTimeRemaining = 0f;
	// Wither (issue #66): the share of max HP already burned away. Permanent for this life.
	private float witherFraction = 0f;
	// Poison status: flat damage per tick while poisonTimeRemaining > 0.
	private int poisonDamagePerTick = 0;
	private float poisonTimeRemaining = 0f;
	private float poisonTickTimer = 0f;
	private const float PoisonTickInterval = 1.0f;
	// Small per-enemy movement variation so paths are less robotic
	// Three wander octaves per enemy at unrelated frequencies. Two sines at a fixed ratio - which
	// is what this was - is periodic, so every enemy traced the same tidy serpentine at a different
	// phase. Three incommensurate ones do not close the loop inside the lifetime of an enemy, so no
	// path repeats and no two paths rhyme.
	private float wanderPhaseA = 0f;
	private float wanderPhaseB = 0f;
	private float wanderPhaseC = 0f;
	private float wanderFreqA = 1f;
	private float wanderFreqB = 1.7f;
	private float wanderFreqC = 0.4f;
	private float wanderAmpA = 0.5f;
	private float wanderAmpB = 0.3f;
	private float wanderAmpC = 0.2f;
	private float speedDriftPhase = 0f;
	private float speedDriftFreq = 0.5f;
	private float currentSpeedDrift = 1f;
	private float turnResponse = 9f;
	private Vector2 cachedSeparation = Vector2.Zero;
	// This enemy slot on the ring, as an angular offset from whatever bearing it currently sits at
	// relative to the player. Persistent, so an enemy keeps sliding the same way around the ring as
	// it closes rather than jittering between sides.
	private float approachBearingOffset = 0f;
	// Per-enemy movement character, all rolled together in RollMovementVariation.
	private float speedJitter = 1f;
	private float leadJitter = 1f;
	private float spacingJitter = 1f;
	private Vector2 cachedApproachDir = Vector2.Zero;
	private bool hasCachedApproachDir = false;
	// On an open stage: whether the straight walk to the player crosses water (MazeNavigation.HasClearWalk),
	// re-tested with the aim rather than every frame. Only an enemy whose way is blocked reads the
	// flow field; one with a clear run keeps its lead and its slot on the ring.
	private bool walkBlocked = false;
	// The player measured velocity - actual displacement per second, not the velocity it intended.
	//
	// CharacterBody2D.Velocity is what the player asked for, and it keeps reporting full speed while
	// the player is not moving at all: the arena boundary clamps position rather than blocking
	// physically, so a player held against the edge still reads 220. Leading off that number sent
	// every enemy at a point beyond the wall that the player would never reach, and the swarm
	// stacked up past them instead of on them. Measured from positions, this cannot lie.
	private Vector2 measuredPlayerVelocity = Vector2.Zero;
	/// <summary>The player measured velocity, for subclasses that need to lead a target.</summary>
	protected Vector2 MeasuredPlayerVelocity => measuredPlayerVelocity;
	private Vector2 lastPlayerPosition = Vector2.Zero;
	private bool hasLastPlayerPosition = false;
	private const ulong SeparationUpdateInterval = 4;
	private RandomNumberGenerator rng = new RandomNumberGenerator();
	[Export] public float Speed { get; set; } = 125f;
	/// <summary>
	/// The scene's own Speed, captured before Node2DGame scaled it by the run's pace.
	/// </summary>
	/// <remarks>
	/// Recycling is the reason this exists. The pace multiplier climbs over a run, but an enemy is
	/// scaled once at spawn, so one that keeps being recycled would carry minute-zero speed forever
	/// and the arena would slowly fill with permanently slow stragglers. Rescaling needs the original
	/// number - multiplying the already-scaled Speed again would compound.
	/// </remarks>
	public float SpawnBaseSpeed { get; set; } = 0f;
	[Export] public int Health { get; set; } = 20;
	[Export] public string EnemyType { get; set; } = "Enemy";
	[Export] public float RespawnDistance { get; set; } = 1600f;
	// On a maze stage, an enemy whose WALK to the player (MazeNavigation.PathTilesAt) exceeds
	// RespawnDistance times this is recycled even while it is close in a straight line. Kept well
	// above what Node2DGame.IsPoorMazeSpawn lets a fresh spawn start at, so a new enemy is never
	// recycled on its first frame.
	[Export] public float MazeRecyclePathFactor { get; set; } = 1.6f;
	[Export] public bool SpriteFacesRightByDefault { get; set; } = true;
	[Export] public float MinPlayerSeparation { get; set; } = 27f;
	[Export] public float OverlapResolveSpeed { get; set; } = 230f;
	// Grown with the bodies. Every enemy sprite and hitbox went up 35%, and spacing that stayed
	// where it was would have the crowd standing inside each other rather than shoulder to
	// shoulder - the separation radius has to describe the body it is keeping apart.
	[Export] public float EnemySpacingRadius { get; set; } = 59f;
	[Export] public float EnemySpacingStrength { get; set; } = 95f;
	// Total lateral wobble on an approach, as a fraction of the heading. Split across three
	// per-enemy octaves rather than spent on one sine, so a path wanders instead of weaving.
	// 0.24 rather than the 0.34 first tried: measured against a circling player, the wider
	// wobble spent enough forward progress to halve how many enemies reached contact.
	[Export] public float PathNoiseStrength { get; set; } = 0.24f;
	// How much an enemy speed breathes over time, either side of its own fixed jitter. A constant
	// speed is uniform even when every enemy has a different one - the pack still holds its shape.
	[Export] public float SpeedDriftAmount { get; set; } = 0.12f;
	// How quickly an enemy turns onto a new heading, as an exponential rate. Rolled per enemy, so
	// some are nimble and some lumber. Without it every body snaps to its desired direction on the
	// same frame and the crowd pivots like one object.
	// Measured, not guessed. At 4.5-13 the easing was slow enough that a pack tracking a
	// circling player leaned into every turn and fell behind - contact dropped from about 4.6
	// to 2.6. At 9-18 contact is back at the old level and the crowd still turns raggedly.
	[Export] public float TurnResponseMin { get; set; } = 9f;
	[Export] public float TurnResponseMax { get; set; } = 18f;
	// How far ahead of the player to aim. Pure pursuit - steering at where the player is right now -
	// is what lets a faster player kite forever: slower pursuers chasing a target on a circular path
	// converge onto a limit cycle *inside* that circle, which is the knot of enemies that ends up
	// rotating in the middle while the player runs rings around it. Aiming at where the player will
	// be makes them cut the chord instead.
	[Export] public float LeadPursuitSeconds { get; set; } = 0.75f;
	// Encirclement. Every enemy used to steer at the identical point, so the swarm arrived as one
	// lump from one bearing and the player only ever had to outrun a single blob. Each enemy now
	// aims at its own slot on a ring around the player, so the pack fans into an arc and closes from
	// several sides at once. It also unjams the pile: enemies are solid to each other at runtime
	// (ConfigureEntityCollision masks layer 2, which the .tscn does not), so a pack converging on one
	// point grinds itself to a halt against its own bodies.
	[Export] public float EncircleRadius { get; set; } = 110f;
	// Inside this the enemy steers straight at the player - the last stretch is always a commit,
	// never a drift around the ring.
	[Export] public float EncircleCollapseDistance { get; set; } = 90f;
	// Distance over which the ring offset ramps from nothing to full, measured outward from the
	// collapse distance.
	[Export] public float EncircleFalloff { get; set; } = 300f;
	// Per-enemy speed spread, rolled at spawn. Without it every enemy of a type moves at exactly the
	// same speed, so a pack behind a moving player holds its formation perfectly and the whole horde
	// appears to slide along beside you as one sheet. A few percent either way and the pack stretches
	// and reshuffles continuously, which is what a crowd of separate things looks like.
	[Export] public float SpeedVariance { get; set; } = 0.14f;
	// Spread on how far ahead each enemy leads. Every enemy leading by the identical amount points
	// the entire pack at one predicted point, which tightens the formation rather than loosening it -
	// interception made the sliding-sheet look worse before this.
	[Export] public float LeadVarianceMin { get; set; } = 0.55f;
	[Export] public float LeadVarianceMax { get; set; } = 1.25f;
	// How often an enemy re-aims, in physics frames, staggered per enemy so they do not all turn on
	// the same tick. A pack that recomputes in lockstep pivots as one body; staggering it makes the
	// turn ripple through the crowd. Also cheaper than re-aiming everything every frame.
	[Export] public int AimRefreshFrames { get; set; } = 4;
	[Export] public bool IgnoresDecorCollision { get; set; } = false;
	[Export] public bool IsMiniBoss { get; set; } = false;

	// THE PLAYER SHOVES THROUGH THE CROWD (Eric: "monsters should make way for the player more, the
	// player should be able to push them out of the way"). A crowd of ordinary enemies used to be a
	// wall: the player's body masked layer 2, every enemy is solid to every other, so a pack in front
	// of him was a pile he could not move and he stood pinned in it. Now:
	//
	//   - The player's body no longer collides with ordinary enemies at all (Player.ConfigureEntity-
	//     Collision). He walks into them; they are pushed aside by ComputePlayerShove below, and the
	//     engine's own depenetration (each enemy still masks the player's layer 1) slides them off his
	//     body. What it costs him is speed - Player.CrowdSlowPerEnemy, per enemy he is pushing.
	//   - Elites, minibosses and bosses still block. They also sit on BlockerCollisionLayer, the one
	//     layer the player's body masks besides decor, so he slides off them exactly as before.
	//   - The shove moves an enemy through its own MoveAndSlide, which masks walls and water (layer 4),
	//     so a shove can never put a body in a lake. Pinned against one, it slides along the shore or
	//     stays put and the player passes it.
	//
	// Layer 5 (bit value 16) carried nothing before this. Only the player's body masks it; weapons,
	// the HurtBox and every enemy still find blockers through layer 2, which they also keep.
	public const int BlockerCollisionLayer = 5;

	/// <summary>True if the player cannot shove this body: elites, minibosses and bosses.</summary>
	/// <remarks>
	/// IsMiniBoss is already the one flag all three carry (BossEnemy sets it before base._Ready, and
	/// Node2DGame sets it on an elite before AddChild), and it is read at _Ready, so it must be set
	/// before the enemy enters the tree - as every caller does today.
	/// </remarks>
	public bool BlocksPlayer => IsMiniBoss;

	/// <summary>
	/// What kind of chest carrier this is - "elite", "warden", "ghost" - or empty for none. Set by
	/// Node2DGame on the bodies it sends, never by a scene: IsMiniBoss cannot say it, because Rockem
	/// carries that flag both as the Sketchbook's scheduled miniboss and as 7% of its ordinary table,
	/// and an event's elites are paid by the event's own chest. Chests come from what the player
	/// kills on purpose (DropTable), so a carrier opts in rather than every big body paying out.
	/// </summary>
	public string ChestSource { get; set; } = string.Empty;

	// How far ahead of the player's front edge an enemy starts stepping out of his path, in px, and
	// how hard. A yield is something an enemy does, so it is gentle and it fades in with distance:
	// at 0.45 of his speed over 70 px a body dead ahead has cleared about half his lane before he
	// reaches it, which is what reads as the crowd making way rather than bouncing off him.
	[Export] public float PlayerYieldReach { get; set; } = 70f;
	[Export] public float PlayerYieldShare { get; set; } = 0.45f;
	// The shove itself, once he is touching it: sideways at a little over his own speed, so a body
	// dead ahead (about 34 px to clear) is out of his lane in about a quarter of a second, plus a
	// small forward carry so it is swept along rather than run over. Larger carry made a bulldozer
	// pile in front of him, which is the wall this replaced.
	[Export] public float PlayerShoveSideShare { get; set; } = 1.15f;
	[Export] public float PlayerShoveCarryShare { get; set; } = 0.3f;
	private const float PlayerShoveContactGap = 6f;
	// The body's radius, for the lane test. Measured once from the collision shape at _Ready.
	private float bodyRadius = 20f;
	// Which way a body exactly on the player's line steps, so a column does not all go one side.
	private float shoveTieSide = 1f;
	private Player? playerBody;
	// Which of the two death sounds this enemy gets. Silhouette already tells the player WHAT
	// died; this tells them how big it was. Minibosses are heavy without needing the flag set
	// per scene, and BossEnemy overrides PlayDeathSound outright.
	[Export] public bool HeavyDeath { get; set; } = false;
	// Contact damage is dealt by the player's overlap loop, not by the enemy, so this is the value
	// it reads back (duck-typed). 1 keeps every existing enemy exactly as it was; a boss raises it
	// so its melee actually hurts.
	[Export] public int ContactDamage { get; set; } = 1;
	// 0 = knocked around like anything else, 1 = immovable. A boss that skids across the arena on
	// every hit stops reading as a boss.
	[Export] public float KnockbackResistance { get; set; } = 0f;
	// Floor for ApplySlow. Defaults to 0 so ordinary enemies can still be frozen solid; a boss
	// raises it so a freeze build cannot simply park it for the whole fight.
	[Export] public float MinSlowMultiplier { get; set; } = 0f;
	public float HealthFraction => maxHealth > 0 ? Mathf.Clamp(Health / (float)maxHealth, 0f, 1f) : 1f;
	public int MaxHealth => maxHealth;

	private Node2D? player;
	// Subclasses (BossEnemy) need the same target this one steers toward, without re-resolving it.
	protected Node2D? TargetPlayer => player;
	private AnimatedSprite2D? animatedSprite;
	private Sprite2D? sprite;
	private int maxHealth = 0;
	private PackedScene floatingTextScene = ResourceLoader.Load<PackedScene>("res://scenes/FloatingText.tscn");
	private PackedScene xpOrbScene = ResourceLoader.Load<PackedScene>("res://scenes/XPOrb.tscn");
	// Bonus Drop Table (issue #25): rare extra drops on death, chance scaled by the player's Luck stat.
	private PackedScene healthPickupScene = ResourceLoader.Load<PackedScene>("res://scenes/HealthPickup.tscn");
	private PackedScene buffItemScene = ResourceLoader.Load<PackedScene>("res://scenes/BuffItem.tscn");
	private Tween? hitFlashTween;
	// The size the hit punch returns to. Read from Scale only when no punch is running: reading it
	// mid-punch took the enlarged size as the new rest, so anything hit faster than the punch eases
	// back - every boss, under any build - grew 4% a hit and never shrank.
	private Vector2 punchRestScale = Vector2.One;
	// The sprite's original modulate (e.g. per-type or elite tint). The hit-flash restores to this
	// instead of white so damaged enemies keep the color that signals their strength/type.
	private Color baseModulate = Colors.White;
	private Vector2 animatedSpriteBasePosition = Vector2.Zero;
	private Vector2 spriteBasePosition = Vector2.Zero;
	// Set once health hits 0. The enemy stays in the tree for the length of its death animation,
	// but leaves the "enemies" group and drops its collision immediately so it can no longer be
	// targeted, damaged, or bump the player while the corpse plays out.
	private bool isDying = false;

	/// <summary>
	/// True from the frame this enemy hit 0 HP. Anything holding a reference to an enemy across
	/// frames (the player's contact-damage set, for one) must drop it when this turns true - a
	/// corpse is still in the tree and still overlapping, but must never act on the player again.
	/// </summary>
	public bool IsDying => isDying;

	/// <summary>Whether a slow or root is currently on this enemy.</summary>
	/// <remarks>Read by Hoarfrost Nail, which makes slowed enemies take more damage.</remarks>
	public bool IsSlowed => slowTimeRemaining > 0f;

	/// <summary>Rooted solid by a freeze (ApplySlow at 0). The Long Winter shatters what dies like this.</summary>
	public bool IsFrozen => slowTimeRemaining > 0f && slowMultiplier <= 0.001f;

	/// <summary>Whether a Vulnerable debuff is currently on this enemy.</summary>
	public bool IsVulnerable => vulnerableTimeRemaining > 0f;

	/// <summary>Extra damage taken as a fraction, e.g. 0.25 for +25%. Zero when not vulnerable.</summary>
	public float VulnerabilityBonus => vulnerableTimeRemaining > 0f ? vulnerableBonus : 0f;

	/// <summary>Whether a Weaken debuff is currently cutting this enemy's damage.</summary>
	public bool IsWeakened => weakenTimeRemaining > 0f;

	/// <summary>Whether Wither has burned away part of this enemy's max HP.</summary>
	public bool IsWithered => witherFraction > 0f;

	/// <summary>Every debuff on this enemy, as StatusEffectLayer draws them. A freeze is Freeze, not Slow.</summary>
	/// <remarks>
	/// Poison counts by its clock, not its tick damage, so a zero-damage poison (the harness's
	/// --status-all) still shows - what is being drawn is the status, not the damage.
	/// </remarks>
	public StatusVisual ActiveStatusVisuals
	{
		get
		{
			StatusVisual v = StatusVisual.None;
			if (slowTimeRemaining > 0f)
				v |= slowMultiplier <= 0.001f ? StatusVisual.Freeze : StatusVisual.Slow;
			if (poisonTimeRemaining > 0f)
				v |= StatusVisual.Poison;
			if (vulnerableTimeRemaining > 0f)
				v |= StatusVisual.Vulnerable;
			if (weakenTimeRemaining > 0f)
				v |= StatusVisual.Weaken;
			if (witherFraction > 0f)
				v |= StatusVisual.Wither;
			return v;
		}
	}

	/// <summary>How long one status has left, for the fade at its end. Wither never ends.</summary>
	public float StatusVisualTimeRemaining(StatusVisual status) => status switch
	{
		StatusVisual.Slow or StatusVisual.Freeze => slowTimeRemaining,
		StatusVisual.Poison => poisonTimeRemaining,
		StatusVisual.Vulnerable => vulnerableTimeRemaining,
		StatusVisual.Weaken => weakenTimeRemaining,
		StatusVisual.Wither => witherFraction > 0f ? float.PositiveInfinity : 0f,
		_ => 0f,
	};

	// StatusEffectLayer's bookkeeping: this enemy's index in its list (-1 when not drawn), and a
	// per-enemy frame offset so a debuffed pack does not animate in lockstep.
	internal int StatusFxSlot { get; set; } = -1;
	internal int StatusFxPhase { get; private set; }

	private bool statusFxGeometryReady;
	private StatusFxGeometry statusFxGeometry;

	/// <summary>Where this enemy's feet, head and shoulders are, for placing status marks. Measured once.</summary>
	internal StatusFxGeometry StatusFxGeometry
	{
		get
		{
			if (!statusFxGeometryReady)
			{
				statusFxGeometry = MeasureStatusFxGeometry();
				statusFxGeometryReady = true;
			}
			return statusFxGeometry;
		}
	}

	// Read off the sprite's first frame rather than declared per scene: thirty-odd enemy scenes at
	// scales from 1.6 to 3 on cells from 17 to 96 pixels, and a per-scene number is one more thing
	// to forget when the next enemy is added. The figure fractions follow .ai/art-direction.md
	// section 5 - feet on row cell-2, a figure filling roughly the top nine-tenths of its cell.
	private StatusFxGeometry MeasureStatusFxGeometry()
	{
		Rect2 rect = new Rect2(-43f, -43f, 86f, 86f);
		if (animatedSprite?.SpriteFrames != null)
		{
			Vector2 size = new Vector2(32f, 32f);
			StringName anim = animatedSprite.Animation;
			SpriteFrames frames = animatedSprite.SpriteFrames;
			if (frames.HasAnimation(anim) && frames.GetFrameCount(anim) > 0
				&& frames.GetFrameTexture(anim, 0) is Texture2D texture)
				size = texture.GetSize();
			Vector2 topLeft = (animatedSprite.Centered ? -size * 0.5f : Vector2.Zero) + animatedSprite.Offset;
			Vector2 scale = animatedSprite.Scale.Abs();
			rect = new Rect2(animatedSpriteBasePosition + topLeft * scale, size * scale);
		}
		else if (sprite != null)
		{
			Rect2 local = sprite.GetRect();
			Vector2 scale = sprite.Scale.Abs();
			rect = new Rect2(spriteBasePosition + local.Position * scale, local.Size * scale);
		}

		// x2 on the basic and elite cells, x4 on a boss: one integer either way (art-direction §1),
		// and a mark on a 190-pixel boss at x2 would be a speck.
		float pixelScale = 2f * Mathf.Max(1, Mathf.RoundToInt(rect.Size.Y / 90f));
		return new StatusFxGeometry(
			feetY: rect.Position.Y + rect.Size.Y * 0.94f,
			headY: rect.Position.Y + rect.Size.Y * 0.10f,
			halfWidth: rect.Size.X * 0.5f * 0.75f,
			pixelScale: pixelScale);
	}

	// Registers with the arena's status drawer and refreshes the sprite's own tint and animation
	// rate. Called from every Apply* that actually landed.
	private void OnStatusApplied()
	{
		StatusEffectLayer.Current?.Track(this);
		RefreshStatusLook();
	}

	// The part of a status that lives on the sprite rather than in the layer: a tint, and the
	// animation slowed with the body or stopped dead by a freeze. Written on transitions only -
	// SelfModulate, not Modulate, so the hit flash and the elite and boss tints that own Modulate
	// (baseModulate, SetBaseModulate) compose with it instead of fighting it.
	// 0 none, 1 poisoned, 2 slowed, 3 frozen. Poison is the only mark-only status with a tint, and
	// it is faint: modulate is a status channel (.ai/art-direction.md §4), but a green swarm is not.
	private int statusLook;
	private float statusLookSpeed = 1f;
	private static readonly Color FrozenLook = new Color(0.80f, 1.04f, 1.40f);
	private static readonly Color SlowedLook = new Color(0.84f, 0.98f, 1.30f);
	private static readonly Color PoisonedLook = new Color(0.90f, 1.06f, 0.82f);

	private void RefreshStatusLook()
	{
		int look = 0;
		if (!isDying && !StatusEffectLayer.Disabled)
			look = IsFrozen ? 3 : IsSlowed ? 2 : poisonTimeRemaining > 0f ? 1 : 0;
		// The walk slows with the body, floored so a heavy slow still visibly walks; a freeze stops it.
		float speed = look == 3 ? 0f : look == 2 ? Mathf.Clamp(slowMultiplier, 0.35f, 1f) : 1f;
		if (look == statusLook && Mathf.IsEqualApprox(speed, statusLookSpeed))
			return;

		statusLook = look;
		statusLookSpeed = speed;
		Color tint = look switch
		{
			3 => FrozenLook,
			2 => SlowedLook,
			1 => PoisonedLook,
			_ => Colors.White,
		};
		if (animatedSprite != null)
		{
			animatedSprite.SelfModulate = tint;
			animatedSprite.SpeedScale = speed;
		}
		if (sprite != null)
			sprite.SelfModulate = tint;
	}

	/// <summary>
	/// When non-zero, this enemy walks this heading instead of chasing the player. Used by side
	/// events whose enemy has somewhere to be - the Scorched Sands caravan master walks its road and
	/// ignores the player entirely. The length is a speed multiplier, as with AdjustSteering.
	/// </summary>
	public Vector2 ScriptedHeading { get; set; } = Vector2.Zero;

	// Ceiling on Wither for this enemy. A boss lowers it (see BossEnemy) for the same reason it
	// raises MinSlowMultiplier: a debuff that takes a quarter of an ordinary enemy's health would
	// take a quarter of a boss fight.
	[Export] public float MaxWitherFraction { get; set; } = 0.25f;

	public override void _Ready()
	{
		maxHealth = Health;
		AddToGroup("enemies");
		ConfigureEntityCollision();
		ConfigureDecorCollisionExceptions();
		player = GetParent().GetNodeOrNull<Node2D>("CharacterBody2D");
		playerBody = player as Player;
		bodyRadius = MeasureBodyRadius();
		shoveTieSide = (GetInstanceId() & 1) == 0 ? 1f : -1f;
		animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (animatedSprite != null)
		{
			animatedSpriteBasePosition = animatedSprite.Position;
			if (!animatedSprite.IsPlaying())
			{
				StringName animToPlay = animatedSprite.Animation;
				if (animToPlay == default)
					animatedSprite.Play();
				else
					animatedSprite.Play(animToPlay);
			}
		}
		if (animatedSprite != null)
			animatedSprite.AnimationFinished += OnOneShotAnimationFinished;
		ConfigureEliteMarker();
		if (sprite != null)
			spriteBasePosition = sprite.Position;
		CanvasItem? visual = (CanvasItem?)animatedSprite ?? sprite;
		if (visual != null)
			baseModulate = visual.Modulate;
		SetProcess(true);
		SetPhysicsProcess(true);
		// Initialize per-enemy wander parameters
		rng.Randomize();
		RollMovementVariation();
		StatusFxPhase = (int)(GetInstanceId() % (ulong)StatusEffectLayer.Frames);
	}

	// Smoothed so a single stuttering frame does not swing everyone off course.
	private void TrackPlayerVelocity(float delta)
	{
		if (player == null || delta <= 0f)
			return;

		Vector2 position = player.GlobalPosition;
		if (hasLastPlayerPosition)
		{
			Vector2 sample = (position - lastPlayerPosition) / delta;
			measuredPlayerVelocity = measuredPlayerVelocity.Lerp(sample, 0.35f);
		}

		lastPlayerPosition = position;
		hasLastPlayerPosition = true;
	}

	// Everything that makes this enemy move unlike the one next to it. Rolled at spawn and again on
	// recycling, so a recycled wave does not arrive as a matched set.
	//
	// The jitters are multipliers held apart from the exported values rather than folded into them,
	// so re-rolling cannot compound: Speed is already scaled once by Node2DGame at spawn and again
	// for elites, and multiplying it in place here would drift every time an enemy came back.
	private void RollMovementVariation()
	{
		// Roughly +-50 degrees. Wide enough that a pack spreads across a real arc, narrow enough that
		// nobody sets off around the long side of the player, which looks broken and wastes the
		// little speed these have.
		approachBearingOffset = rng.RandfRange(-0.9f, 0.9f);
		speedJitter = 1f + rng.RandfRange(-SpeedVariance, SpeedVariance);
		leadJitter = rng.RandfRange(LeadVarianceMin, LeadVarianceMax);
		// Uneven personal space, so the crowd does not settle into a lattice.
		spacingJitter = rng.RandfRange(0.82f, 1.24f);
		wanderPhaseA = rng.Randf() * Mathf.Tau;
		wanderPhaseB = rng.Randf() * Mathf.Tau;
		wanderPhaseC = rng.Randf() * Mathf.Tau;
		// Deliberately not harmonics of each other, and each enemy draws its own three, so the sum
		// is quasi-periodic rather than a shared rhythm.
		wanderFreqA = rng.RandfRange(0.55f, 1.05f);
		wanderFreqB = rng.RandfRange(1.30f, 2.40f);
		wanderFreqC = rng.RandfRange(0.15f, 0.40f);
		// Amplitudes normalised so the three together stay within PathNoiseStrength no matter how the
		// weights fall - otherwise an unlucky roll sends an enemy sideways instead of forward.
		float a = rng.RandfRange(0.3f, 1f);
		float b = rng.RandfRange(0.2f, 0.8f);
		float c = rng.RandfRange(0.4f, 1f);
		float total = a + b + c;
		wanderAmpA = a / total;
		wanderAmpB = b / total;
		wanderAmpC = c / total;
		speedDriftPhase = rng.Randf() * Mathf.Tau;
		speedDriftFreq = rng.RandfRange(0.25f, 0.75f);
		currentSpeedDrift = 1f;
		turnResponse = rng.RandfRange(TurnResponseMin, TurnResponseMax);
		hasCachedApproachDir = false;
	}

	// This enemy actual movement speed, including its personal jitter and any slow on it.
	private float EffectiveSpeed => Speed * slowMultiplier * speedJitter * currentSpeedDrift * SwarmSpeedScale;

	/// <summary>
	/// Where this enemy should steer: the player predicted position, offset to this enemy own slot
	/// on the encircling ring. Both terms fade out as it closes, so the final approach is a straight
	/// line at the player.
	/// </summary>
	// When this enemy and the player would meet if both held their current course: the smallest
	// positive root of |toPlayer + playerVelocity * t| = speed * t.
	//
	// Returns 0 when there is no such time, and 0 means "do not lead at all - just steer at where
	// they are". That degenerate case is the honest one: a player who is faster than this enemy and
	// running directly away can never be intercepted, so there is no point aiming anywhere except
	// at their back. The old formula had no such case and produced a lead time regardless, which is
	// how an enemy ended up aiming behind itself.
	private static float SolveInterceptTime(Vector2 toPlayer, Vector2 playerVelocity, float speed)
	{
		float a = playerVelocity.LengthSquared() - (speed * speed);
		float b = 2f * toPlayer.Dot(playerVelocity);
		float c = toPlayer.LengthSquared();

		// Player and enemy exactly matched in speed: the quadratic degenerates to a linear one.
		if (Mathf.Abs(a) < 0.001f)
		{
			if (Mathf.Abs(b) < 0.001f)
				return 0f;
			float linear = -c / b;
			return linear > 0f ? linear : 0f;
		}

		float discriminant = (b * b) - (4f * a * c);
		if (discriminant < 0f)
			return 0f;

		float root = Mathf.Sqrt(discriminant);
		float first = (-b - root) / (2f * a);
		float second = (-b + root) / (2f * a);

		// Soonest meeting, so the enemy commits to the nearest interception rather than a later one
		// further around the player path.
		float best = float.MaxValue;
		if (first > 0f)
			best = first;
		if (second > 0f && second < best)
			best = second;

		return best < float.MaxValue ? best : 0f;
	}

	private Vector2 ComputeApproachDirection(Vector2 playerPosition, float distanceToPlayer, float currentSpeed)
	{
		Vector2 aim = playerPosition;

		if (LeadPursuitSeconds > 0f)
		{
			// Solved interception, not distance/speed.
			//
			// The lead used to be `distance / ownSpeed` - how long this enemy takes to reach where
			// the player is standing *now* - and the player velocity was then applied over that
			// whole window unconditionally. Walk straight at an enemy and that slides the aim point
			// along the line between you, through the enemy, and out the far side: the enemy then
			// steers at a point behind its own back and runs away from you at full speed. Measured
			// at 1.84s of retreat in an 8s head-on approach, starting around 90 units out, which is
			// exactly where the encircle term stops masking it.
			//
			// The intercept time below cannot do that. It is the time at which the two would
			// actually meet, so the aim point always lies between the enemy and the player when the
			// player is closing, and the enemy walks into you instead of away.
			float lead = SolveInterceptTime(playerPosition - GlobalPosition, measuredPlayerVelocity, currentSpeed);
			if (lead > 0f)
				// Still capped, for the original reason: a distant slow enemy must not aim at a
				// point the player will have left long before it arrives.
				aim += measuredPlayerVelocity * Mathf.Min(lead, LeadPursuitSeconds * leadJitter);
		}

		if (EncircleRadius > 0f && distanceToPlayer > EncircleCollapseDistance)
		{
			float spread = Mathf.Clamp(
				(distanceToPlayer - EncircleCollapseDistance) / Mathf.Max(1f, EncircleFalloff), 0f, 1f);

			// Fanning out sideways costs closing speed, and these enemies do not have any to spare -
			// the fastest of them runs at under half the player. Measured: an ungated ring offset
			// nearly tripled how many enemies reach a circling player, and cut a straight-line chase
			// by ten times, because every chaser behind a fleeing player veered off the tail and
			// never recovered the ground.
			//
			// So the offset is spent only out of slack. escapeRate is how fast the player is pulling
			// directly away from THIS enemy: near zero when they circle (their motion is tangential
			// to us, so a detour costs nothing), and at full player speed when they run straight
			// away, where any detour is ground we never get back.
			float escapeRate = measuredPlayerVelocity.Dot((playerPosition - GlobalPosition).Normalized());
			float slack = Mathf.Clamp(1f - (escapeRate / Mathf.Max(1f, currentSpeed)), 0f, 1f);
			spread *= slack;

			if (spread > 0.001f)
			{
				float ownBearing = (GlobalPosition - playerPosition).Angle();
				aim += Vector2.FromAngle(ownBearing + (approachBearingOffset * spread)) * EncircleRadius * spread;
			}
		}

		Vector2 delta = aim - GlobalPosition;
		if (delta.LengthSquared() > 0.0001f)
			return delta.Normalized();

		Vector2 fallback = playerPosition - GlobalPosition;
		return fallback.LengthSquared() > 0.0001f ? fallback.Normalized() : Vector2.Right;
	}

	private void ConfigureEntityCollision()
	{
		SetCollisionLayerValue(2, true);
		SetCollisionMaskValue(1, true);
		SetCollisionMaskValue(2, true);
		// Water and maze walls moved off layer 1 onto their own (LevelTilePainter.TerrainCollisionLayer)
		// so the player could meet them with his feet. Enemies still stop at them exactly as before.
		SetCollisionMaskValue(LevelTilePainter.TerrainCollisionLayer, true);
		// The one layer the player's body masks besides decor: what he cannot shove, he slides off.
		SetCollisionLayerValue(BlockerCollisionLayer, BlocksPlayer);
	}

	// Every enemy scene is a circle today but one rectangle; the lane test only needs a radius, so a
	// rectangle counts its longer half. Scale as the scene sets it - this runs once, before anything
	// could rescale the body, and an elite (the one runtime rescale) is a blocker anyway.
	private float MeasureBodyRadius()
	{
		var shapeNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		float radius = shapeNode?.Shape switch
		{
			CircleShape2D circle => circle.Radius,
			CapsuleShape2D capsule => capsule.Radius,
			RectangleShape2D rectangle => Mathf.Max(rectangle.Size.X, rectangle.Size.Y) * 0.5f,
			_ => 20f,
		};
		float scale = Mathf.Abs(Scale.X) * (shapeNode != null ? Mathf.Abs(shapeNode.Scale.X) : 1f);
		return Mathf.Max(4f, radius * scale);
	}

	/// <summary>
	/// The velocity the player's walk adds to this body this frame: a yield as he approaches, a
	/// shove once he is touching it, nothing if it is not in his path or cannot be shoved.
	/// </summary>
	/// <remarks>
	/// O(1), reading only the player's own step (Player.ShoveVelocity), so the crowd costs nothing
	/// beyond what every enemy already pays to chase him.
	///
	/// <paramref name="canYield"/> is false for a body that is not choosing where it goes this frame:
	/// planted mid wind-up (AdjustSteering returned Zero), stunned, or frozen solid. A yield is
	/// something an enemy does, and a planted one holds still exactly as AdjustSteering promises.
	/// A shove is something done TO it, and it still lands: every telegraph draws in its owner's
	/// local space and resolves at the owner's position on the frame it lands (GroundSlamAttack,
	/// SummonerEnemy, RangedEnemy), so a shoved slammer carries its ring with it and the shape the
	/// player sees is still the shape that hits. Holding a planted body immovable would need it to
	/// block, and a planted slammer in a crowd would pin him again - the thing this exists to end.
	/// </remarks>
	private Vector2 ComputePlayerShove(bool canYield)
	{
		if (playerBody == null || isDying || BlocksPlayer || playerBody.IsDead)
			return Vector2.Zero;

		Vector2 push = playerBody.ShoveVelocity;
		float speedSquared = push.LengthSquared();
		if (speedSquared < 1f)
			return Vector2.Zero;

		float speed = Mathf.Sqrt(speedSquared);
		Vector2 heading = push / speed;
		Vector2 offset = GlobalPosition - playerBody.GlobalPosition;
		// Behind his centre it is being passed, not walked into; his body slides it off.
		float along = offset.Dot(heading);
		if (along < 0f)
			return Vector2.Zero;

		Vector2 side = new Vector2(-heading.Y, heading.X);
		float lateral = offset.Dot(side);
		float lane = Player.ShoveHalfWidth + bodyRadius;
		float absLateral = Mathf.Abs(lateral);
		if (absLateral >= lane)
			return Vector2.Zero;

		// Edge to edge along his path: how far he still has to walk before his front meets it.
		float gap = along - bodyRadius - Player.ShoveHalfWidth;
		if (gap > PlayerYieldReach)
			return Vector2.Zero;

		Vector2 aside = side * (absLateral > 1f ? Mathf.Sign(lateral) : shoveTieSide);
		// 1 dead ahead, 0 at the edge of his lane: the nearer the middle, the further it has to go.
		float inLane = 1f - (absLateral / lane);

		if (gap <= PlayerShoveContactGap)
			return (aside * speed * PlayerShoveSideShare * (0.35f + 0.65f * inLane))
				+ (heading * speed * PlayerShoveCarryShare);

		if (!canYield)
			return Vector2.Zero;

		float approach = 1f - ((gap - PlayerShoveContactGap) / Mathf.Max(1f, PlayerYieldReach - PlayerShoveContactGap));
		return aside * speed * PlayerYieldShare * approach * inLane;
	}

	private void ConfigureDecorCollisionExceptions()
	{
		if (!IgnoresDecorCollision)
			return;

		foreach (Node node in GetTree().GetNodesInGroup("decor_props"))
		{
			if (node is CollisionObject2D collisionObject)
				AddCollisionExceptionWith(collisionObject);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		QueueRedraw();
		// Before any of the branches below, so an enemy that is shocked, knocked back or planted
		// mid wind-up still has a current reading when it comes to aim.
		if (player != null && IsInstanceValid(player))
			TrackPlayerVelocity((float)delta);
		bool shocked = shockTimeRemaining > 0f;
		// Whether this body is choosing where it goes this frame, which is what a yield needs (see
		// ComputePlayerShove). Only the chase branch below can make it true.
		bool canYield = false;
		if (shocked)
		{
			shockTimeRemaining = Math.Max(0f, shockTimeRemaining - (float)delta);
			Velocity = Vector2.Zero;
			UpdateShockVisual((float)delta);
		}
		else if (knockbackTime > 0f)
		{
			float decayThreshold = KnockbackDuration * 0.3f;
			if (knockbackTime > decayThreshold)
			{
				// First 70%: constant velocity
				Velocity = knockbackVelocity;
			}
			else
			{
				// Last 30%: linearly decay velocity to zero
				float t = knockbackTime / decayThreshold; // t goes from 1 to 0
				Velocity = knockbackVelocity * t;
			}
			knockbackTime -= (float)delta;
			if (knockbackTime <= 0f)
			{
				knockbackVelocity = Vector2.Zero;
				knockbackTime = 0f;
			}
		}
		else if (player != null && IsInstanceValid(player))
		{
			Vector2 playerOffset = player.GlobalPosition - GlobalPosition;
			float distanceToPlayer = playerOffset.Length();

			// If an enemy drifts inside the player's center/footprint, push it back out so enemies
			// cannot remain stuck intersecting the player indefinitely.
			if (distanceToPlayer < MinPlayerSeparation)
			{
				Vector2 escapeDir = distanceToPlayer > 0.001f
					? -playerOffset / distanceToPlayer
					: new Vector2(Mathf.Cos(wanderPhaseA), Mathf.Sin(wanderPhaseA));
				Velocity = escapeDir * OverlapResolveSpeed;
				canYield = true;
			}
			else
			{
				// Interception and encirclement, rather than the player current position. Still a
				// unit vector - AdjustSteering owns speed, and its returned length is the multiplier.
				// Recomputed on a stagger rather than every frame: the wander below still varies the
				// heading smoothly every frame, so this reads as reaction time, not as stutter.
				int refreshInterval = Math.Max(1, AimRefreshFrames);
				bool reaim = !hasCachedApproachDir
					|| (Engine.GetPhysicsFrames() + GetInstanceId()) % (ulong)refreshInterval == 0;
				if (reaim)
				{
					cachedApproachDir = ComputeApproachDirection(
						player.GlobalPosition, distanceToPlayer, EffectiveSpeed);
					hasCachedApproachDir = true;
				}

				var toPlayer = cachedApproachDir;

				// Follow the shared terrain-aware flow field round walls and water instead of steering
				// straight at the player, which wedges a body on the near shore of a lake or the near
				// side of a maze wall. The field already routes round geometry, and a ring offset laid
				// over it would push enemies into the very walls it is steering them around.
				//
				// In a maze, always. On an open stage only while the straight walk is blocked: a field
				// over eight neighbours bends an open-ground approach toward the nearest compass octant,
				// so an enemy with a clear run keeps the direct chase - lead, ring slot and all - and
				// open-field feel is exactly what it was. The line test runs with the re-aim, one
				// enemy in AimRefreshFrames per frame, and costs nothing on ground with no water.
				var primaryDir = toPlayer;
				var nav = MazeNavigation.Routing;
				if (nav != null && FollowsTerrain)
				{
					long navStart = MazeNavigation.MeasureSteering ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
					if (reaim)
						walkBlocked = nav.IsMaze || !nav.HasClearWalk(GlobalPosition, player.GlobalPosition);
					if (walkBlocked)
					{
						Vector2 flow = nav.FlowDirectionAt(GlobalPosition);
						if (flow != Vector2.Zero)
							primaryDir = flow;
					}
					if (MazeNavigation.MeasureSteering)
						MazeNavigation.AddSteeringTicks(System.Diagnostics.Stopwatch.GetTimestamp() - navStart);
				}

				float step = (float)delta;
				speedDriftPhase += step * speedDriftFreq;
				currentSpeedDrift = 1f + (Mathf.Sin(speedDriftPhase) * SpeedDriftAmount);
				wanderPhaseA += step * wanderFreqA;
				wanderPhaseB += step * wanderFreqB;
				wanderPhaseC += step * wanderFreqC;
				float wobble = ((Mathf.Sin(wanderPhaseA) * wanderAmpA)
					+ (Mathf.Sin(wanderPhaseB) * wanderAmpB)
					+ (Mathf.Sin(wanderPhaseC) * wanderAmpC)) * PathNoiseStrength;
				var lateral = new Vector2(-primaryDir.Y, primaryDir.X);
				var variedDir = (primaryDir + (lateral * wobble)).Normalized();

				// Not every enemy simply runs at the player. A caster holding its range, or any
				// enemy planted mid wind-up, reshapes the steering here rather than re-implementing
				// the flow field, wander and separation work around it.
				var steering = AdjustSteering(variedDir, distanceToPlayer);
				if (ScriptedHeading != Vector2.Zero)
					steering = ScriptedHeading;

				if ((Engine.GetPhysicsFrames() + GetInstanceId()) % SeparationUpdateInterval == 0)
				{
					cachedSeparation = Vector2.Zero;
					float spacingRadius = EnemySpacingRadius * spacingJitter;
					float spacingRadiusSquared = spacingRadius * spacingRadius;
					foreach (Node node in GetTree().GetNodesInGroup("enemies"))
					{
						if (node is not Enemy enemy || enemy == this || !IsInstanceValid(enemy))
							continue;

						Vector2 offsetFromEnemy = GlobalPosition - enemy.GlobalPosition;
						float distanceSquared = offsetFromEnemy.LengthSquared();
						if (distanceSquared > spacingRadiusSquared || distanceSquared <= 0.001f)
							continue;

						float distance = Mathf.Sqrt(distanceSquared);
						Vector2 pushDir = offsetFromEnemy / distance;
						float weight = 1f - (distance / spacingRadius);
						cachedSeparation += pushDir * weight * EnemySpacingStrength;
					}
				}

				if (steering.LengthSquared() <= 0.0001f)
				{
					// A deliberate hold. Separation must not creep it off its spot, or a planted
					// wind-up would slide out from under the tell the player is reading. Set outright
					// rather than eased, for the same reason: "still" has to mean still.
					Velocity = Vector2.Zero;
				}
				else
				{
					canYield = slowMultiplier > 0f;
					float steeringSpeed = steering.Length();
					Vector2 desiredVelocity;
					if (cachedSeparation.LengthSquared() > 0.001f)
					{
						var separationDir = cachedSeparation.Normalized();
						// Blend on direction alone and re-apply the steering magnitude afterwards.
						// AdjustSteering documents a longer-than-unit return as "move faster than
						// Speed", and normalizing the blend used to discard exactly that - so a
						// charging lunger reverted to a walk for as long as anything was near enough
						// to push against, which in a swarm is most of the dash. Every steering that
						// returns a unit vector is unaffected.
						var combinedDir = (steering / steeringSpeed + separationDir * 0.35f).Normalized();
						desiredVelocity = combinedDir * steeringSpeed * EffectiveSpeed;
					}
					else
					{
						desiredVelocity = steering * EffectiveSpeed;
					}

					// Ease onto the new heading instead of snapping to it. Velocity used to be
					// assigned outright, so every enemy changed direction completely within one frame
					// and the whole crowd turned together like a single rotating object. With a
					// per-enemy response rate they lean into their turns at different rates and the
					// pack stops moving as one piece.
					//
					// Exponential, so it behaves the same at any physics rate. Skipped entirely when
					// the steering asks to move faster than Speed - that is a lunger mid-dash, and a
					// dash that ramps in is not a dash.
					Velocity = steeringSpeed > 1.05f
						? desiredVelocity
						: Velocity.Lerp(desiredVelocity, 1f - Mathf.Exp(-turnResponse * step));
				}
			}
		}
		// The player's shove rides this frame's move only, and is taken back out afterwards. The
		// chase eases Velocity toward its target from last frame's value, so a shove left in it
		// would compound - at a turn rate of 9 the steady state is about seven times the shove.
		// The shove is a displacement he causes, not momentum the enemy owns.
		Vector2 shove = ComputePlayerShove(canYield);
		Vector2 steered = Velocity;
		if (shove != Vector2.Zero)
			Velocity = steered + shove;
		MoveAndSlide();
		if (shove != Vector2.Zero)
			Velocity = steered;
		if (!shocked)
			UpdateShockVisual((float)delta);

		if (slowTimeRemaining > 0f)
		{
			slowTimeRemaining -= (float)delta;
			if (slowTimeRemaining <= 0f)
			{
				slowTimeRemaining = 0f;
				slowMultiplier = 1f;
				RefreshStatusLook();
			}
		}

		if (vulnerableTimeRemaining > 0f)
		{
			vulnerableTimeRemaining -= (float)delta;
			if (vulnerableTimeRemaining <= 0f)
			{
				vulnerableTimeRemaining = 0f;
				vulnerableBonus = 0f;
			}
		}

		if (weakenTimeRemaining > 0f)
		{
			weakenTimeRemaining -= (float)delta;
			if (weakenTimeRemaining <= 0f)
			{
				weakenTimeRemaining = 0f;
				weakenFraction = 0f;
			}
		}

		if (poisonTimeRemaining > 0f)
		{
			poisonTimeRemaining -= (float)delta;
			poisonTickTimer += (float)delta;
			if (poisonTickTimer >= PoisonTickInterval)
			{
				poisonTickTimer = 0f;
				if (poisonDamagePerTick > 0)
					TakeDamage(poisonDamagePerTick);
			}
			if (poisonTimeRemaining <= 0f)
			{
				poisonTimeRemaining = 0f;
				poisonDamagePerTick = 0;
				RefreshStatusLook();
			}
		}
		if (player != null && IsInstanceValid(player))
		{
			var dist = player.GlobalPosition.DistanceTo(GlobalPosition);
			// Recycling an abandoned enemy back to the spawn ring. This asked for "respawn_enemy", but
			// Godot only snake_cases *engine* methods - a user-defined C# method is registered under
			// its C# name, so HasMethod never matched and not one enemy was ever recycled. Elites are
			// exempt: a miniboss that vanishes and reappears elsewhere reads as a bug, and the player
			// may be deliberately walking away from one.
			// In a maze, distance is measured along the corridors too. An enemy three walls away can
			// be a short straight line and a very long walk from the player, and it would trudge that
			// walk for the rest of the run in plain sight - the "trapped in other corridors" report.
			// Recycled instead, it comes back at a spawn point the corridors actually connect.
			// The same holds on open ground for an enemy on the far side of a lake.
			bool walkTooFar = false;
			var mazeNav = MazeNavigation.Routing;
			if (mazeNav != null && FollowsTerrain && dist <= RespawnDistance)
			{
				float pathTiles = mazeNav.PathTilesAt(GlobalPosition);
				// -1 on open, connected floor means past the field's search horizon, which is
				// further still. On a wall cell it means the body was shoved in, and the field's
				// escape direction is already walking it back out - no recycle for that.
				walkTooFar = pathTiles < 0f
					? mazeNav.IsReachable(GlobalPosition)
					: pathTiles * mazeNav.TileSize > RespawnDistance * MazeRecyclePathFactor;
			}
			if ((dist > RespawnDistance || walkTooFar) && !isDying && !IsMiniBoss)
			{
				// The parent, not GetTree().CurrentScene: enemies are added as children of the
				// game node, and CurrentScene is whatever scene was launched - which is the game
				// in normal play but not under a test harness, and not if the game is ever
				// instanced inside something else.
				var host = GetParent();
				if (host != null && host.HasMethod("RespawnEnemy"))
				{
					host.CallDeferred("RespawnEnemy", this);
				}
			}
		}

		UpdateFacing();
	}

	/// <summary>
	/// Last word on which way this enemy steers, given the chase direction the base class worked
	/// out (flow field, wander and path noise already folded in) and how far the player is.
	/// </summary>
	/// <remarks>
	/// Return the direction unchanged - the default - to chase. Return a unit vector to steer
	/// somewhere else; it is multiplied by <see cref="Speed"/>, so anything longer than one unit
	/// moves faster than the enemy is supposed to. Return <see cref="Vector2.Zero"/> to stand
	/// still, which also suppresses the separation nudge so the hold is exact.
	/// </remarks>
	protected virtual Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer) => chaseDirection;

	/// <summary>
	/// True if this body is stopped by walls and water (it masks LevelTilePainter.TerrainCollisionLayer,
	/// where their tile collision lives), so it should route round them. Nothing in the roster ignores
	/// terrain today - ghosts only ignore decor (IgnoresDecorCollision) - but one that clears this
	/// mask flies straight, and is never recycled for a walk it does not have to make.
	/// </summary>
	private bool FollowsTerrain => GetCollisionMaskValue(LevelTilePainter.TerrainCollisionLayer);

	private void UpdateFacing()
	{
		float facingX = 0f;
		if (player != null && IsInstanceValid(player))
			facingX = player.GlobalPosition.X - GlobalPosition.X;
		else
			facingX = Velocity.X;

		if (Mathf.Abs(facingX) < 0.001f)
			return;

		bool shouldFaceRight = facingX > 0f;
		bool flip = SpriteFacesRightByDefault ? !shouldFaceRight : shouldFaceRight;

		if (animatedSprite != null)
			animatedSprite.FlipH = flip;
		if (sprite != null)
			sprite.FlipH = flip;
	}
	public void ApplyKnockback(Vector2 force)
	{
		if (isDying)
			return;

		knockbackVelocity = force * (1f - Mathf.Clamp(KnockbackResistance, 0f, 1f));
		knockbackTime = KnockbackDuration;
	}

	public void ApplyShock(float duration, float shakeAmplitude = 3.5f, float shakeFrequency = 42f)
	{
		if (isDying)
			return;

		shockTimeRemaining = Math.Max(shockTimeRemaining, duration);
		shockAmplitude = Math.Max(0.5f, shakeAmplitude);
		shockFrequency = Math.Max(1f, shakeFrequency);
		shockPhase = rng.RandfRange(0f, Mathf.Tau);
	}

	// Slows (or, at multiplier 0, roots/freezes) the enemy for `duration` seconds. Refreshes to the
	// stronger effect and the longer remaining duration if already active (issue #16/#22).
	public void ApplySlow(float multiplier, float duration)
	{
		if (isDying)
			return;

		multiplier = Mathf.Clamp(multiplier, Mathf.Clamp(MinSlowMultiplier, 0f, 1f), 1f);
		if (slowTimeRemaining <= 0f || multiplier < slowMultiplier)
			slowMultiplier = multiplier;
		slowTimeRemaining = Mathf.Max(slowTimeRemaining, duration);
		if (slowTimeRemaining > 0f)
			OnStatusApplied();
	}

	/// <summary>Marks the enemy Vulnerable: it takes more damage from every source.</summary>
	/// <remarks>
	/// Stacking-resistant in exactly the same way as ApplySlow and ApplyPoison - the strongest
	/// magnitude and the longest remaining duration win, and neither adds to the other. Two debuff
	/// spells hitting the same target must not multiply into an execute.
	///
	/// This is the payload Shadow Bolt exists to deliver. It is deliberately a status on the ENEMY
	/// rather than a buff on the player, so it is visible on the target, it expires on its own, and
	/// every source of damage benefits from it rather than only the spell that applied it.
	/// </remarks>
	public void ApplyVulnerable(float bonus, float duration)
	{
		if (isDying)
			return;

		bonus = Mathf.Clamp(bonus, 0f, MaxVulnerability);
		if (vulnerableTimeRemaining <= 0f || bonus > vulnerableBonus)
			vulnerableBonus = bonus;

		vulnerableTimeRemaining = Mathf.Max(vulnerableTimeRemaining, duration);
		if (vulnerableTimeRemaining > 0f)
			OnStatusApplied();
	}

	// A ceiling on the debuff itself, not just on any one application. Vulnerability multiplies
	// every other damage source the player has, so an uncapped version scales with the whole build
	// rather than with the spell that applied it.
	private const float MaxVulnerability = 0.60f;

	/// <summary>Weakens the enemy: every hit it lands on the player is cut by <paramref name="fraction"/>.</summary>
	/// <remarks>
	/// Stacking-resistant like ApplySlow and ApplyVulnerable - strongest magnitude, longest remaining
	/// duration. The defensive mirror of Vulnerable, and Light's first presence on the battlefield
	/// rather than in the player's stat sheet (issue #66).
	/// </remarks>
	public void ApplyWeaken(float fraction, float duration)
	{
		if (isDying)
			return;

		fraction = Mathf.Clamp(fraction, 0f, Mathf.Clamp(MaxWeakenFraction, 0f, MaxWeakenCeiling));
		if (weakenTimeRemaining <= 0f || fraction > weakenFraction)
			weakenFraction = fraction;

		weakenTimeRemaining = Mathf.Max(weakenTimeRemaining, duration);
		if (weakenTimeRemaining > 0f)
			OnStatusApplied();
	}

	// Ceiling on Weaken for this enemy; a boss lowers it (see BossEnemy).
	[Export] public float MaxWeakenFraction { get; set; } = 0.50f;

	// Half, never more, whatever a scene says. A weaken that could reach 100% would be an
	// invulnerability aura.
	private const float MaxWeakenCeiling = 0.50f;

	/// <summary>
	/// What a hit of <paramref name="amount"/> from this enemy actually deals after Weaken. Every
	/// enemy-to-player damage site must route through this, or Weaken silently does nothing there.
	/// </summary>
	/// <remarks>
	/// Rounded stochastically, not to nearest. Almost every enemy hits for exactly 1, and 1 x 0.7
	/// rounds back to 1 - a weaken that only rounded would do nothing at all against the swarm. So
	/// the fractional part becomes a chance: a 30% weaken makes 30% of 1-damage touches land for 0,
	/// which is the right average and reads as "they keep missing". A 0 means the caller should skip
	/// the hit entirely, so a weakened miss triggers no on-hurt reactions, exactly like a dodge.
	/// </remarks>
	public int ScaleOutgoingDamage(int amount)
	{
		if (amount <= 0 || weakenTimeRemaining <= 0f || weakenFraction <= 0f)
			return amount;

		float scaled = amount * (1f - weakenFraction);
		int whole = Mathf.FloorToInt(scaled);
		return rng.Randf() < scaled - whole ? whole + 1 : whole;
	}

	/// <summary>Withers the enemy: burns away <paramref name="fraction"/> of its max HP, permanently.</summary>
	/// <remarks>
	/// Strongest wins, never sums - a second, weaker wither does nothing, and a stronger one only
	/// takes the difference. That keeps it bounded by <see cref="MaxWitherFraction"/> however many
	/// sources apply it.
	///
	/// The HP comes off current health and the health fraction falls with it, so a boss bar visibly
	/// drops. Unlike damage it ignores Vulnerable and crits, is measured against MAX health so it is
	/// worth most against the things hardest to kill, and can never kill: it leaves at least 1 HP
	/// for a real hit to finish (issue #66).
	/// </remarks>
	public void ApplyWither(float fraction)
	{
		if (isDying || maxHealth <= 0)
			return;

		fraction = Mathf.Clamp(fraction, 0f, Mathf.Clamp(MaxWitherFraction, 0f, 1f));
		if (fraction <= witherFraction)
			return;

		int burned = Mathf.RoundToInt(maxHealth * (fraction - witherFraction));
		witherFraction = fraction;
		Health = Math.Max(1, Health - burned);
		OnStatusApplied();
	}

	// Applies a stacking-resistant poison DoT: takes the stronger tick damage and the longer
	// remaining duration (issue #16/#22).
	public void ApplyPoison(int damagePerTick, float duration)
	{
		if (isDying)
			return;

		poisonDamagePerTick = Math.Max(poisonDamagePerTick, damagePerTick);
		poisonTimeRemaining = Mathf.Max(poisonTimeRemaining, duration);
		if (poisonTimeRemaining > 0f)
			OnStatusApplied();
	}
	// 	var rng = new RandomNumberGenerator();
	// 	rng.Randomize();
	// 					var angle = rng.Randf() * (Mathf.Pi * 2.0f);
	// 	var radius = rng.RandfRange(250f, 800f);
	// 	var newPos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	// 	ResetForRespawn(newPos, maxHealth);
	// }
	// 			}
	// 		}
	// 	}

	public void TakeDamage(int amount, bool isCrit = false)
	{
		if (isDying)
			return;

		if (amount > 0)
			GameStats.RecordDamageDealt(amount);

		PlayHitFeedback(isCrit);

		// Show floating damage text
		if (floatingTextScene != null)
		{
			var textNode = floatingTextScene.Instantiate<Node2D>();
			Color color = isCrit ? new Color(1f, 0.85f, 0.1f, 1f) : new Color(1, 0.2f, 0.2f, 1); // Yellow for crits, red otherwise
			string text = isCrit ? $"{amount}!" : amount.ToString();
			if (textNode is FloatingText ft)
			{
				ft.Text = text;
				ft.Color = color;
				ft.GlobalPosition = this.GlobalPosition;
				if (isCrit)
					ft.Scale *= 1.4f;
			}
			else
			{
				textNode.Set("Text", text);
				textNode.Set("Color", color);
				textNode.Set("GlobalPosition", this.GlobalPosition);
			}
			// Add to the enemy's parent so it persists after enemy is freed
			GetParent().AddChild(textNode);
		}
		Health -= amount;
		if (Health <= 0)
			StartDeath();
	}

	// Drops rewards immediately, then plays the death animation if this enemy has one before
	// freeing. Rewards must not wait on the animation - the player should never lose XP because
	// a corpse was still animating when the run ended.
	/// <summary>Whether anything can currently see, hit or collide with this enemy.</summary>
	public bool IsTargetable { get; private set; } = true;

	/// <summary>
	/// Takes the enemy out of play without killing it, or puts it back.
	/// </summary>
	/// <remarks>
	/// The primitive every burrowing boss is built on, and the reason a stationary boss in a
	/// bullet heaven can be paced with something other than more health: time the player cannot
	/// spend damaging is worth more than a bigger health bar, because it forces them to move
	/// instead of to wait.
	///
	/// It is the same teardown StartDeath does - leave the group, zero the collision - minus the
	/// dying. The group is what matters: every targeting path and every AoE sweep in this game
	/// reads "enemies", so leaving it makes the boss invisible to all of them at once with no
	/// change to any of them.
	///
	/// **Death and untargetability must not be confused.** A dying enemy is already out of the
	/// group; calling this with true on a corpse would put a dead thing back in it, so isDying
	/// short-circuits the whole method.
	/// </remarks>
	public void SetTargetable(bool targetable)
	{
		if (isDying || IsTargetable == targetable)
			return;

		IsTargetable = targetable;

		if (targetable)
		{
			if (!IsInGroup("enemies"))
				AddToGroup("enemies");
			ConfigureEntityCollision();
			if (animatedSprite != null)
				animatedSprite.Visible = true;
			return;
		}

		if (IsInGroup("enemies"))
			RemoveFromGroup("enemies");
		CollisionLayer = 0;
		CollisionMask = 0;
		Velocity = Vector2.Zero;
		if (animatedSprite != null)
			animatedSprite.Visible = false;
	}

	protected virtual void StartDeath()
	{
		if (isDying)
			return;

		isDying = true;
		RunEvents.RecordKill(EnemyType, IsMiniBoss);

		// Before the group leave and the collision zeroing below, while GlobalPosition still
		// means something. The sound is allowed to outlast the corpse - it gates nothing.
		PlayDeathSound();

		if (HasSignal("killed"))
			EmitSignal("killed");

		// One notification per death, wherever the damage came from - a spell, a poison tick, a
		// trap. Duck-typed through the group rather than a held reference or a static event: the
		// former would need every enemy wired to the player, and the latter is how this project
		// has twice ended up with a finalizer touching a freed Godot object.
		foreach (Node watcher in GetTree().GetNodesInGroup("player"))
		{
			if (watcher.HasMethod("OnEnemyDiedAt"))
				watcher.Call("OnEnemyDiedAt", GlobalPosition);
		}
		DropXp();
		TryDropBonusItem();
		// A chest carrier - an elite, a Warden, the Sketchbook's ghost - hands its chest to the run,
		// which decides whether this one pays and where it lands (Node2DGame.DropCarriedChest).
		if (!string.IsNullOrEmpty(ChestSource) && Node2DGame.Current is Node2DGame chestGame && IsInstanceValid(chestGame))
			chestGame.DropCarriedChest(this);

		// Leave the group first so AoE sweeps and targeting skip the corpse this same frame.
		if (IsInGroup("enemies"))
			RemoveFromGroup("enemies");
		Velocity = Vector2.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		SetPhysicsProcess(false);
		// The status marks go with the collision, this frame. And the sprite's status look is
		// undone before the death strip plays: a freeze leaves SpeedScale at 0, and a death
		// animation that never advances never finishes, so a corpse killed frozen would never free.
		StatusEffectLayer.Current?.Untrack(this);
		RefreshStatusLook();

		if (animatedSprite != null && animatedSprite.SpriteFrames != null
			&& animatedSprite.SpriteFrames.HasAnimation("death"))
		{
			// Restore the base tint so a hit flash mid-death doesn't freeze the corpse red.
			animatedSprite.Modulate = baseModulate;
			if (hitFlashTween != null && hitFlashTween.IsValid() && hitFlashTween.IsRunning())
				Scale = punchRestScale;
			hitFlashTween?.Kill();
			animatedSprite.AnimationFinished += OnDeathAnimationFinished;
			animatedSprite.Play("death");
			return;
		}

		// No death animation (most of the kids sheets): defer freeing so FloatingText survives a frame.
		CallDeferred("queue_free");
	}

	/// <summary>The death cue. Overridden by BossEnemy, which is not an enemy dying.</summary>
	protected virtual void PlayDeathSound()
	{
		SfxPlayer.EnemyDeath(GlobalPosition, HeavyDeath || IsMiniBoss);
	}

	private void OnDeathAnimationFinished()
	{
		QueueFree();
	}

	// Every enemy sheet in the pack ships an "attack" and a "take-damage" strip that this project
	// never wired up, so enemies used to slide into the player and shrug off arrows with the same
	// looping walk cycle. These two play those strips as one-shots over the walk.
	//
	// Enemies whose SpriteFrames lack the animation - the kids sheets, SlowEnemy - fall through
	// untouched; HasAnimation is the guard, the same way StartDeath already handles a missing
	// death animation.
	private bool oneShotAnimationPlaying;

	/// <summary>Plays the attack swing. Called by the player when this enemy lands a contact hit.</summary>
	public void PlayAttackAnimation() => PlayOneShotAnimation("attack");

	// Elites were signalled by scale and tint alone, which is easy to miss in a swarm where every
	// type is already a different size and colour. A gold ring underfoot is unambiguous.
	//
	// This reuses the shield-ring sheet rather than a dedicated sprite. The obvious alternative -
	// swapping elites onto the dungeon pack's v2 character sprites - was rejected: those sets are
	// four-frame *idles* only, so an elite wearing one would lose its walk, attack, hurt and death
	// animations. A marker is a smaller change and strictly more information.
	private void ConfigureEliteMarker()
	{
		if (!IsMiniBoss)
			return;

		var frames = GD.Load<SpriteFrames>("res://scenes/resources/ShieldAuraFrames.tres");
		if (frames == null)
			return;

		var marker = new AnimatedSprite2D
		{
			Name = "EliteMarker",
			SpriteFrames = frames,
			Animation = "active",
			// Under the enemy, so a dense pack still shows a ring per elite.
			ZIndex = -1,
			// The project filters nearest now, but this squashes a 72x72 sheet to a
			// non-uniform 0.55 x 0.32 - nearest drops whole rows there and the ring
			// breaks into dashes. Keep Linear until migration phase 4 redraws the
			// marker at a real cell size (.ai/art-direction.md section 6).
			TextureFilter = TextureFilterEnum.Linear,
			Scale = new Vector2(0.55f, 0.32f),
			Position = new Vector2(0f, 8f),
			Modulate = new Color(1.0f, 0.82f, 0.30f, 0.75f)
		};
		AddChild(marker);
		marker.Play("active");
	}

	private void PlayOneShotAnimation(StringName animation)
	{
		// Never override the death animation: a corpse mid-collapse must not flinch.
		if (isDying || oneShotAnimationPlaying || animatedSprite?.SpriteFrames == null)
			return;
		if (!animatedSprite.SpriteFrames.HasAnimation(animation))
			return;

		oneShotAnimationPlaying = true;
		animatedSprite.Play(animation);
	}

	private void OnOneShotAnimationFinished()
	{
		// StartDeath connects its own handler for "death"; this one must keep out of its way.
		if (isDying)
			return;

		oneShotAnimationPlaying = false;
		if (animatedSprite?.SpriteFrames?.HasAnimation("moving") == true)
			animatedSprite.Play("moving");
	}

	public override void _ExitTree()
	{
		if (animatedSprite != null && IsInstanceValid(animatedSprite))
			animatedSprite.AnimationFinished -= OnOneShotAnimationFinished;
		StatusEffectLayer.Current?.Untrack(this);
	}

	// Retints the enemy permanently. Goes through baseModulate rather than the sprite alone so the
	// hit flash restores to the new colour instead of snapping back to the old one - a boss that
	// darkens on enrage must stay dark for the rest of the fight.
	protected void SetBaseModulate(Color color)
	{
		baseModulate = color;
		CanvasItem target = (CanvasItem?)animatedSprite ?? sprite;
		if (target != null)
			target.Modulate = color;
	}

	private void PlayHitFeedback(bool isCrit)
	{
		// Throttled and pitch-varied inside SfxPlayer, not here: in a dense wave this runs dozens
		// of times a second across the whole swarm, and the budget has to be spent globally.
		SfxPlayer.EnemyHurt(GlobalPosition);
		PlayOneShotAnimation("hurt");

		CanvasItem target = (CanvasItem?)animatedSprite ?? sprite;
		if (target == null)
			return;

		if (hitFlashTween == null || !hitFlashTween.IsValid() || !hitFlashTween.IsRunning())
			punchRestScale = Scale;
		hitFlashTween?.Kill();
		// Blend the flash over the enemy's base tint so its type/elite color still reads during the flash,
		// then restore to that base tint (not white) so the color is never permanently lost.
		Color flashColor = isCrit ? new Color(1.0f, 0.9f, 0.45f, 1.0f) : new Color(1.0f, 0.55f, 0.55f, 1.0f);
		target.Modulate = baseModulate * flashColor;

		Scale = punchRestScale * (isCrit ? 1.08f : 1.04f);

		hitFlashTween = CreateTween();
		hitFlashTween.SetParallel(true);
		hitFlashTween.TweenProperty(target, "modulate", baseModulate, 0.1f);
		hitFlashTween.TweenProperty(this, "scale", punchRestScale, 0.1f);
	}

	// Bonus Drop Table (issue #25): after the guaranteed XP orb, one roll for at most one extra item -
	// a bonus XP orb, a health flask or a speed potion. The chances are DropTable's. They used to be
	// one shared chance split three ways evenly, which three chest-frequency relics also raised, so
	// a run holding the Signet Ring dropped a speed potion every few seconds.
	private void TryDropBonusItem()
	{
		var typedPlayer = player as Player;
		DropTable.BonusDrop drop = DropTable.Roll(rng.Randf(), typedPlayer?.GetLuckLevel01() ?? 0f);
		// The potion's spacing is held by the run, not by this enemy: it is about how often the burst
		// is felt, across every kill. Outside a run (a probe scene) there is no spacing to keep.
		if (drop == DropTable.BonusDrop.SpeedPotion && Node2DGame.Current is Node2DGame game
			&& IsInstanceValid(game) && !game.TryClaimSpeedPotion())
			return;

		PackedScene sceneToSpawn = drop switch
		{
			DropTable.BonusDrop.HealthFlask => healthPickupScene,
			DropTable.BonusDrop.SpeedPotion => buffItemScene,
			DropTable.BonusDrop.XpOrb => xpOrbScene,
			_ => null
		};
		if (sceneToSpawn == null)
			return;

		var item = sceneToSpawn.Instantiate<Node2D>();
		item.GlobalPosition = GlobalPosition;
		if (drop == DropTable.BonusDrop.XpOrb && item is XPOrb bonusOrb)
		{
			bonusOrb.Value *= DropTable.BonusOrbMultiplier;
			bonusOrb.Scale *= 1.6f;
		}

		var scene = GetTree().CurrentScene as Node;
		if (scene != null)
			scene.CallDeferred("add_child", item);
		else
			GetTree().Root.CallDeferred("add_child", item);
	}

	private void DropXp()
	{
		if (xpOrbScene != null)
		{
			var orb = xpOrbScene.Instantiate<Node2D>();
			orb.GlobalPosition = GlobalPosition;
			if (orb is XPOrb xpOrb)
			{
				if (IsMiniBoss)
					xpOrb.Value = 50;
				else if (EnemyType.Equals("Tank", StringComparison.OrdinalIgnoreCase) || EnemyType.Equals("TankEnemy", StringComparison.OrdinalIgnoreCase))
					xpOrb.Value = 5;
				else if (EnemyType.Equals("Fast", StringComparison.OrdinalIgnoreCase) || EnemyType.Equals("FastEnemy", StringComparison.OrdinalIgnoreCase))
					xpOrb.Value = 3;
				else
					xpOrb.Value = 2;
				// A swollen body pays for every body it took in (AbsorbIntoSwarm): folding the swarm
				// moves its XP, it never destroys any.
				if (!IsMiniBoss)
					xpOrb.Value *= SwarmMass;

				// A GREATER ORB: the kill's own orb comes up one size larger (DropTable.GreaterOrbValue).
				// What the Signet Ring, the Tithe of Wormwood and the Regalia pay now, in place of more
				// chests - an XP bonus that shows on the ground as a bigger, brighter orb. Ordinary kills
				// only: a miniboss's orb is already the largest.
				// The chance is read before the roll, so a player with none draws nothing from the RNG.
				if (!IsMiniBoss && player is Player typedPlayer && xpOrb.Value < DropTable.GreaterOrbValue
					&& typedPlayer.GetGreaterOrbChance() is float bigger && bigger > 0f && rng.Randf() < bigger)
					xpOrb.Value = DropTable.GreaterOrbValue;
			}
			var scene = GetTree().CurrentScene as Node;
			if (scene != null && scene.HasMethod("AddXp"))
			{
				var cb = new Callable(scene, "AddXp");
				if (orb.HasSignal("picked_up"))
					orb.Connect("picked_up", cb);
			}
			if (scene != null)
				scene.CallDeferred("add_child", orb);
			else
				GetTree().Root.CallDeferred("add_child", orb);
		}
	}

	// Virtual because recycling an enemy has to reset its behaviour too, not just its position and
	// health: a caster carries an attack cooldown, and a relocated one that arrives with that
	// cooldown already spent would fire the instant it appears.
	public virtual void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		GlobalPosition = newPos;
		Health = newHealth;
		maxHealth = newHealth;
		Velocity = Vector2.Zero;
		// A recycled enemy takes a new slot on the ring and a fresh set of movement quirks. Keeping
		// the old ones would have the recycled portion of the swarm all moving alike again.
		RollMovementVariation();
		hasLastPlayerPosition = false;
		measuredPlayerVelocity = Vector2.Zero;
		// A recycled enemy is a fresh enemy as far as the player can tell, so it must not arrive
		// still carrying the debuffs of the body it used to be - least of all a permanent Wither,
		// which would otherwise follow it round the ring for the rest of the run.
		slowMultiplier = 1f;
		slowTimeRemaining = 0f;
		vulnerableBonus = 0f;
		vulnerableTimeRemaining = 0f;
		weakenFraction = 0f;
		weakenTimeRemaining = 0f;
		witherFraction = 0f;
		poisonDamagePerTick = 0;
		poisonTimeRemaining = 0f;
		// And none of its marks, tint or slowed walk either.
		StatusEffectLayer.Current?.Untrack(this);
		RefreshStatusLook();
		QueueRedraw();
	}

	// Status marks (issue #66) used to be a pip per debuff drawn here, under the sprite. They are
	// StatusEffectLayer's now - one shared drawer for the whole arena, animated, with a shape per
	// status - so an enemy's own _Draw is its telegraphs and nothing else. Subclasses still call
	// base._Draw(); it is harmless, and keeps them right if anything is ever drawn here again.

	private void UpdateShockVisual(float delta)
	{
		Vector2 offset = Vector2.Zero;
		if (shockTimeRemaining > 0f)
		{
			shockPhase += delta * shockFrequency;
			float decay = MathF.Max(0.25f, shockTimeRemaining / MathF.Max(0.001f, shockAmplitude));
			offset = new Vector2(Mathf.Sin(shockPhase * 2.0f), Mathf.Cos(shockPhase * 2.7f)) * shockAmplitude * decay * 0.18f;
		}

		if (animatedSprite != null)
			animatedSprite.Position = animatedSpriteBasePosition + offset;
		if (sprite != null)
			sprite.Position = spriteBasePosition + offset;
	}

	// --- The swarm closing in (Node2DGame.SwarmPressure.cs, .ai/run-pacing.md section 10) ---------
	// Set by Node2DGame and each read in one place, so a player who keeps up meets exactly the enemy
	// he always did: every value here sits at its default until the arena has been choking on its
	// cap long enough to build pressure. Nothing here runs per frame.

	/// <summary>A trickle or formation body - one the swarm may speed up, fold, or bring back ahead.</summary>
	/// <remarks>
	/// Never an elite, an event's body, a summoned minion or a boss: each of those is counted or placed
	/// by something that would break if the swarm moved or removed it. Set by Node2DGame.SpawnEnemy.
	/// </remarks>
	public bool SwarmEligible { get; set; }

	/// <summary>The spawn table's health multiplier for this body's type (a tank's 2.2).</summary>
	/// <remarks>
	/// Kept so a recycled body comes back as the type it is. RespawnEnemy used to hand every recycled
	/// body the plain spawn health, so a tank the player walked away from came back a skeleton's
	/// strength - which mattered little while recycling was rare and matters once the swarm leans on it.
	/// </remarks>
	public float SpawnHealthMultiplier { get; set; } = 1f;

	/// <summary>A speed factor the swarm sets on a sweep: under pressure, stragglers off screen catch up.</summary>
	public float SwarmSpeedScale { get; set; } = 1f;

	/// <summary>How many bodies this one is: one, plus every body it has absorbed.</summary>
	/// <remarks>
	/// A swollen body carries the health and the XP of everything it took in, and hits harder
	/// (<see cref="AbsorbIntoSwarm"/>), so the cap holds the node count while the arena's mass keeps
	/// growing with the spawn schedule it could not place. Recycling keeps it (Node2DGame.RespawnEnemy).
	/// </remarks>
	public int SwarmMass { get; private set; } = 1;

	private int swarmBaseContactDamage = -1;

	/// <summary>Takes <paramref name="other"/> in: its health, its mass and the XP its mass is worth.</summary>
	/// <remarks>
	/// Drawn the same size: .ai/art-direction.md allows no fractional scale, and modulate is a status
	/// channel, so what tells a swollen body apart is that it does not die, hits harder, and drops a
	/// bigger orb. Contact damage grows by one for every two bodies past the first - a mass-5 body
	/// touches for 3, against the ordinary 1 every 0.2 s.
	/// </remarks>
	public void AbsorbIntoSwarm(Enemy other)
	{
		if (other == this || other.isDying || isDying)
			return;
		if (swarmBaseContactDamage < 0)
			swarmBaseContactDamage = ContactDamage;
		int gained = Math.Max(1, other.Health);
		Health += gained;
		maxHealth += gained;
		SwarmMass += other.SwarmMass;
		ContactDamage = swarmBaseContactDamage + (SwarmMass - 1) / 2;
		other.DiscardAbsorbed();
	}

	/// <summary>Leaves the swarm without dying: no XP, no drops, no death - its mass went elsewhere.</summary>
	/// <remarks>
	/// Out of the enemies group and the collision through SetTargetable, the one authority on group
	/// membership, so whatever keeps the list of live enemies sees it go the way it sees a burrow.
	/// </remarks>
	private void DiscardAbsorbed()
	{
		SetTargetable(false);
		StatusEffectLayer.Current?.Untrack(this);
		SetPhysicsProcess(false);
		QueueFree();
	}
}
