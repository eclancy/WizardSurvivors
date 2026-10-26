using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// The Familiar: a hound the wizard conjures out of his own light, that runs at his heel and bites
// what comes close (.ai/spell-roster.md, "Familiar").
//
// WHY IT EXISTS. The developer: "add new spell, one that summons a pet that fights with you (clearly
// a wizard dog, but can evolve/grow/become a new creature on mutation/ascension)". It is the roster's
// autonomous summon - phase 5 of .ai/spell-variety.md, the one weapon type that makes the screen feel
// like it has an ally in it. Kindled Ward was built as that archetype WITHOUT an ally, on the reading of
// .ai/world-and-tone.md that the player has no escort; the familiar keeps the half of that reading that
// matters - it is something the wizard MADE, lit with his light, not someone who came to help - and
// gives the made thing a shape the developer asked for. It is not Spiritual Weapon either: that is an
// orbit, a blade on a fixed circle round the caster that hits what it passes. This chooses its quarry,
// runs to it, and leaves the wizard's side to get it.
//
// THE SIGNATURE (aim / cadence / demand / payload). Aim: the enemy nearest the PLAYER inside the hunting
// range - it guards rather than hunts, so it goes for what is about to reach him. Cadence: the cooldown
// hands every hound one bite; a hound that holds one leaps the moment its quarry is in reach. Demand:
// none it enforces, but it fights where the wizard stands - walk him away and it breaks off and comes to
// heel. Payload: a pounce and a bite, one enemy and whatever stands in its jaws.
//
// THE BODY. Each hound is plain state in this node plus one AnimatedSprite2D, a child of this node,
// which is y-sorted with the arena so a hound stands in front of or behind the bodies round it. It is
// not a physics body and not in any group: no enemy can see it, hit it or be blocked by it, and it
// never blocks the player. It meets the ground the way the enemies' routing does - MazeNavigation's
// solid grid (water, maze walls, trunks, fences) - by refusing to step onto a blocked cell, sliding
// along it, and routing home round it on the navigation's flow field. If it is ever stuck, left behind
// or standing on something solid anyway, it BLINKS: a puff of its own light, and it is back at heel.
// A familiar can do that; it is a spell. The balance harness counts every blink (StatsLine).
//
// THE FORMS (SpellEvolutionCatalog.SetupFamiliarEvolutions; tools/art/familiar.py draws all of them):
//   Great Hound (level 4, +Arcane) - heavier, mantled, jaws wide enough to maul what stands beside.
//   Rimehound   (level 4, the Ice path) - frost fur; every bite slows (the rider, in DealDamageToEnemy).
//   Littermates (level 4) - another hound.
//   Cerberus    (level 8, +Arcane) - three heads: every bite takes the quarry and the two enemies
//                                    nearest it.
//   Griffin     (level 8) - it takes wing: a swoop through the quarry and on past it in a straight
//                           line, striking everything it passes. It flies, so ground is nothing to it.
// After the Ice path, Cerberus and the Griffin are drawn in frost.
//
// NO MARKERS (art-direction.md section 3): the bite's reach is the jaws snapping shut on the bite radius
// and ImpactDebris thrown out to exactly it; the swoop's swath is the feathers it sheds, inside it.
public partial class Familiar : Node2D, IPaintsElementPath
{
	// Evolution ids. SpellEvolutionCatalog builds the options from these; this node reads them.
	public const string GreatHoundId = "familiar_great_hound";
	// ElementPathOption names its option "<spell id>_<suffix>".
	public const string RimehoundId = "familiar_rimehound";
	public const string LittermatesId = "familiar_littermates";
	public const string CerberusId = "familiar_cerberus";
	public const string GriffinId = "familiar_griffin";

	/// <summary>The most hounds a pack holds, whatever Amount says.</summary>
	public const int MaxHounds = 6;

	// --- Cerberus ---------------------------------------------------------------------------------
	/// <summary>Heads that bite on every Cerberus bite: the quarry and the nearest others.</summary>
	public const int CerberusHeads = 3;
	/// <summary>How far from the quarry the other two heads reach, before Area.</summary>
	public const float CerberusReach = 64f;

	// --- Griffin ----------------------------------------------------------------------------------
	/// <summary>Half the width of the swath a swoop strikes, before Area; tested against enemy centres
	/// with RiptideWave.BodyAllowance on top, so what it strikes is what its feathers are seen to cross.</summary>
	public const float SwoopHalfWidth = 22f;
	/// <summary>How far past its quarry a swoop carries on.</summary>
	public const float SwoopOvershoot = 130f;
	public const float SwoopSpeed = 760f;

	// --- the hunt (tuned in scenes/Familiar.tscn) -------------------------------------------------
	[Export] public float RunSpeed { get; set; } = 330f;
	[Export] public float SprintSpeed { get; set; } = 450f;
	[Export] public float FlySpeed { get; set; } = 400f;
	// The jaws: what one bite closes on, round the quarry's centre, before Area.
	[Export] public float BaseBiteRadius { get; set; } = 18f;
	// How far it leaps, as a share of the hunting range - so "Range" on a level card is both.
	[Export] public float LeapReachFraction { get; set; } = 0.42f;
	[Export] public float CrouchSeconds { get; set; } = 0.08f;
	[Export] public float LeapSeconds { get; set; } = 0.22f;
	[Export] public float BiteSeconds { get; set; } = 0.16f;
	[Export] public float HowlSeconds { get; set; } = 0.85f;
	// Past this from the wizard's feet it gives up walking and blinks home.
	[Export] public float BlinkDistance { get; set; } = 470f;
	// This long unable to step, and it blinks home.
	[Export] public float StuckSeconds { get; set; } = 0.6f;

	private const float BlinkSeconds = 0.14f;
	private const float RetargetSeconds = 0.2f;
	// How long the wizard must stand still before a resting hound sits rather than shadowing his back.
	private const float HeelBehind = 40f;
	private const float HeelSide = 26f;
	// Sound throttle: a pack of six biting together is one snap, not six.
	private const int SoundGapMs = 110;

	private const int MaxMotes = 128;
	private const int MaxSnaps = 24;

	private static readonly StringName AnimIdle = "idle";
	private static readonly StringName AnimRun = "run";
	private static readonly StringName AnimPounce = "pounce";
	private static readonly StringName AnimHowl = "howl";
	private static readonly StringName EnemiesGroup = "enemies";

	private enum Form { Hound, Rimehound, GreatHound, Cerberus, CerberusRime, Griffin, GriffinRime }
	private enum State : byte { Heel, Chase, Crouch, Leap, Bite, Howl, BlinkOut, BlinkIn, Swoop }
	private enum BlinkWhy : byte { Arrive, Stuck, Leash, Inside, Dismiss }

	private sealed class Hound
	{
		public AnimatedSprite2D Sprite;
		public Vector2 Pos;
		public State State;
		public float T;
		public Enemy Target;
		public bool HasBite;
		public float Retarget;
		public float Stuck;
		public float Inside;
		public float Facing = 1f;
		public Vector2 From;
		public Vector2 To;
		public float Duration;
		public BlinkWhy Why;
		public bool HowlNext;
		public bool Leaving;
		public float EmitClock;
		public readonly List<Enemy> SwoopHits = new(16);
	}

	private struct Mote
	{
		public Vector2 At;
		public Vector2 Vel;
		public float Age;
		public float Life;
		public Color Colour;
		public float Size;
		public bool Live;
	}

	private struct Snap
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public bool Live;
	}

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; private set; }

	private readonly List<Hound> hounds = new(MaxHounds);
	private readonly List<Enemy> nearBuffer = new(64);
	private readonly Mote[] motes = new Mote[MaxMotes];
	private readonly Snap[] snaps = new Snap[MaxSnaps];
	private int nextMote;
	private int nextSnap;
	private readonly RandomNumberGenerator rng = new();
	private Node2D fx;

	// The pack as of the last cooldown.
	private Form form = Form.Hound;
	private bool formSet;
	private int lastLevel = -1;
	private int damage = 12;
	private float biteRadius = 18f;
	private float huntRange = 200f;
	private float leapReach = 84f;
	private float areaMul = 1f;
	private bool threeHeads;
	private bool flies;
	private ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Arcane);
	private Color tint = Colors.White;
	private Vector2 heelDir = new(-1f, 0.35f);

	// --- what the harness reads (scripts/_BalanceHarness.cs). Static: one run per process there. ---
	public static int StatBites { get; private set; }
	public static int StatLeaps { get; private set; }
	public static int StatSwoops { get; private set; }
	public static int StatBlinksStuck { get; private set; }
	public static int StatBlinksLeash { get; private set; }
	public static int StatBlinksInside { get; private set; }
	public static float StatWorstStuck { get; private set; }
	public static long StatHoundFrames { get; private set; }
	public static long StatFramesOnSolid { get; private set; }
	// The same question asked of the terrain itself (TerrainStreamer, the data the collision is painted
	// from) rather than of the navigation the hound steers by - so a missing navigation window cannot
	// make "never on water or a wall" true by default. Debug builds only; a release build skips it.
	public static long StatNavFrames { get; private set; }
	public static long StatFramesOnSolidTerrain { get; private set; }
	private static readonly bool AuditTerrain = OS.IsDebugBuild();

	/// <summary>One line for the balance report: is the pack biting, and did any hound get stuck.</summary>
	public static string StatsLine() =>
		$"familiar     : {StatBites} bites from {StatLeaps} leaps, {StatSwoops} swoops; blinks home - stuck {StatBlinksStuck}, "
		+ $"left behind {StatBlinksLeash}, on solid ground {StatBlinksInside}; worst stall {StatWorstStuck:0.00} s; "
		+ $"{StatFramesOnSolid} of {StatHoundFrames} hound-frames standing on water or a wall by the navigation "
		+ $"(present on {StatNavFrames}), {StatFramesOnSolidTerrain} by the terrain itself";

	public override void _Ready()
	{
		rng.Randomize();
		// The hounds are children of this node, and y-sort nests: they sort with the bodies in the arena.
		YSortEnabled = true;
		ZIndex = 0;
		GlobalPosition = Vector2.Zero;
		// The effects: over the bodies, as ImpactDebrisLayer is; under the HUD.
		fx = new Node2D { Name = "FamiliarFx", ZAsRelative = false, ZIndex = 6, YSortEnabled = false };
		AddChild(fx);
		fx.Draw += DrawFx;
	}

	/// <summary>
	/// One cooldown: the pack's numbers, its size, and a bite for every hound that has none. Called by
	/// the fire loop every cooldown (Player.FireFamiliar), which is what keeps it current through level-ups
	/// and milestones - and what makes the cooldown the bite rate.
	/// </summary>
	public void Summon(SpellData spell, int count, float playerArea, float damageMultiplier, Color visualTint)
	{
		if (spell == null)
			return;
		SpellData = spell;
		int level = Math.Max(1, spell.CurrentLevel);
		damage = Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(level) * damageMultiplier));
		// Every area term once: the player's Area and every milestone's AreaMultiplier. It sizes the jaws
		// (and a Cerberus's reach, and a swoop's swath) - never the hunting range, which only levels move.
		areaMul = MathF.Max(0.25f, playerArea * spell.GetAreaMultiplierAtLevel(level));
		biteRadius = MathF.Max(8f, BaseBiteRadius * areaMul);
		huntRange = MathF.Max(80f, spell.GetRangeAtLevel(level));
		leapReach = huntRange * LeapReachFraction;
		threeHeads = spell.HasEvolution(CerberusId);
		flies = spell.HasEvolution(GriffinId);
		ramp = ElementColors.GetRamp(spell.GetBranchElement() ?? Element.Arcane);
		tint = visualTint;

		Form wanted = ResolveForm(spell);
		bool changed = !formSet || wanted != form;
		form = wanted;

		count = Math.Clamp(count, 1, MaxHounds);
		int live = 0;
		foreach (Hound h in hounds)
			if (!h.Leaving)
				live++;
		while (live < count)
		{
			AddHound(arriving: true);
			live++;
		}
		for (int i = hounds.Count - 1; i >= 0 && live > count; i--)
		{
			if (hounds[i].Leaving)
				continue;
			StartBlink(hounds[i], BlinkWhy.Dismiss);
			live--;
		}

		if (changed)
		{
			SpriteFrames frames = LoadFrames(form);
			foreach (Hound h in hounds)
				ApplyFrames(h, frames);
		}
		// A new level or a new form is greeted with a howl; the first summoning arrives with one.
		if (formSet && (changed || level != lastLevel))
			foreach (Hound h in hounds)
				h.HowlNext = true;
		// Summoned, or become something new: the one loud moment the familiar has.
		if (changed)
			SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellSummon, PlayerRef?.GlobalPosition ?? GlobalPosition, 0.04f);
		formSet = true;
		lastLevel = level;

		foreach (Hound h in hounds)
		{
			h.HasBite = true;
			if (h.Sprite != null)
				h.Sprite.SelfModulate = tint;
		}
	}

	private static Form ResolveForm(SpellData spell)
	{
		bool rime = spell.HasEvolution(RimehoundId);
		if (spell.HasEvolution(CerberusId))
			return rime ? Form.CerberusRime : Form.Cerberus;
		if (spell.HasEvolution(GriffinId))
			return rime ? Form.GriffinRime : Form.Griffin;
		if (spell.HasEvolution(GreatHoundId))
			return Form.GreatHound;
		return rime ? Form.Rimehound : Form.Hound;
	}

	/// <summary>The SpriteFrames each form loads, by the name tools/art/familiar.py writes.</summary>
	public static string FramesPath(string formName) => $"res://scenes/resources/Familiar{formName}Frames.tres";

	/// <summary>Every form's frames file, for RegressionChecks.ValidateFamiliar.</summary>
	public static readonly string[] FormNames =
		{ "Hound", "Rimehound", "Greathound", "Cerberus", "CerberusRime", "Griffin", "GriffinRime" };

	// The cell each form is drawn on (tools/art/familiar.py FORMS): the hound on the elite row, every
	// larger form on the 64 cell.
	private static int CellOf(Form f) => f switch
	{
		Form.Hound or Form.Rimehound => 48,
		_ => 64,
	};

	private static SpriteFrames LoadFrames(Form f) => GD.Load<SpriteFrames>(FramesPath(FormNames[(int)f]));

	// How far in front of its feet a hound's jaws are, in world pixels: where to stand so the bite lands
	// on the quarry. Read off the drawings at x2.
	private float JawReach => form switch
	{
		Form.GreatHound => 46f,
		Form.Cerberus or Form.CerberusRime => 50f,
		_ => 34f,
	};
	private const float GroundBelowCentre = 28f;

	private void AddHound(bool arriving)
	{
		var h = new Hound
		{
			Sprite = new AnimatedSprite2D
			{
				Name = "Hound",
				Scale = new Vector2(2f, 2f),
				TextureFilter = TextureFilterEnum.Nearest,
			},
		};
		Vector2 at = PlayerRef != null ? HeelSpot(hounds.Count) : Vector2.Zero;
		h.Pos = at;
		h.Sprite.Position = at;
		AddChild(h.Sprite);
		hounds.Add(h);
		ApplyFrames(h, LoadFrames(form));
		if (arriving)
		{
			h.State = State.BlinkIn;
			h.T = 0f;
			h.HowlNext = true;
			Puff(at, inward: true);
		}
	}

	private void ApplyFrames(Hound h, SpriteFrames frames)
	{
		if (h.Sprite == null || frames == null)
			return;
		int cell = CellOf(form);
		h.Sprite.SpriteFrames = frames;
		// The feet row (cell - 2) sits on the node's origin, which is the hound's ground point.
		h.Sprite.Offset = new Vector2(0f, -(cell * 0.5f - 1f));
		h.Sprite.Play(AnimIdle);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		MazeNavigation nav = MazeNavigation.Active;
		Vector2 v = PlayerRef.Velocity;
		if (v.LengthSquared() > 900f)
			heelDir = heelDir.Lerp(-v.Normalized(), MathF.Min(1f, dt * 4f)).Normalized();

		for (int i = hounds.Count - 1; i >= 0; i--)
		{
			Hound h = hounds[i];
			Tick(h, i, dt, nav);
			if (h.Leaving && h.State == State.BlinkOut && h.T >= BlinkSeconds)
			{
				h.Sprite?.QueueFree();
				hounds.RemoveAt(i);
			}
		}
		Separate(dt, nav);

		TerrainStreamer terrain = AuditTerrain ? Node2DGame.Current?.Terrain as TerrainStreamer : null;
		foreach (Hound h in hounds)
		{
			StatHoundFrames++;
			if (nav != null)
				StatNavFrames++;
			if (!flies && nav != null && nav.IsBlockedAt(h.Pos))
				StatFramesOnSolid++;
			if (!flies && terrain != null && h.State is not (State.BlinkOut or State.BlinkIn)
				&& (terrain.IsBlockedAtWorld(h.Pos) || terrain.IsWallAtWorld(h.Pos)))
				StatFramesOnSolidTerrain++;
			if (h.Sprite == null)
				continue;
			h.Sprite.Position = h.Pos;
			h.Sprite.FlipH = h.Facing < 0f;
			float alpha = h.State switch
			{
				State.BlinkOut => 1f - Mathf.Clamp(h.T / BlinkSeconds, 0f, 1f),
				State.BlinkIn => Mathf.Clamp(h.T / BlinkSeconds, 0f, 1f),
				_ => 1f,
			};
			h.Sprite.Modulate = new Color(1f, 1f, 1f, alpha);
		}

		TickFx(dt);
		fx?.QueueRedraw();
	}

	// --- one hound ---------------------------------------------------------------------------------

	private void Tick(Hound h, int index, float dt, MazeNavigation nav)
	{
		h.T += dt;
		Vector2 player = PlayerRef.GlobalPosition;
		Vector2 feet = PlayerRef.FootPosition;

		switch (h.State)
		{
			case State.BlinkOut:
				Show(h, AnimIdle, -1, 1f);
				if (h.T >= BlinkSeconds && !h.Leaving)
				{
					h.Pos = FreeHeelSpot(index, nav);
					Puff(h.Pos, inward: true);
					h.State = State.BlinkIn;
					h.T = 0f;
					h.Stuck = 0f;
					h.Inside = 0f;
					h.Target = null;
				}
				return;
			case State.BlinkIn:
				Show(h, AnimIdle, -1, 1f);
				if (h.T >= BlinkSeconds)
					Settle(h);
				return;
			case State.Howl:
				Show(h, AnimHowl, Mathf.Clamp((int)(h.T / HowlSeconds * 6f), 0, 5), 1f);
				EmitHowl(h, dt);
				if (h.T >= HowlSeconds || h.Pos.DistanceTo(feet) > 190f)
					Settle(h);
				return;
			case State.Crouch:
				Show(h, AnimPounce, 0, 1f);
				if (h.T >= CrouchSeconds)
				{
					if (!Alive(h.Target))
					{
						Settle(h);
						return;
					}
					h.From = h.Pos;
					h.To = AttackSpot(h, h.Target);
					h.Duration = LeapSeconds;
					h.State = State.Leap;
					// Carry what is left of the frame into the leap: on a long frame (a loaded machine,
					// a phone) every phase taking a whole frame of its own stretched a bite to a second.
					h.T -= CrouchSeconds;
					StatLeaps++;
				}
				return;
			case State.Leap:
			{
				float t = Mathf.Clamp(h.T / h.Duration, 0f, 1f);
				if (Alive(h.Target))
					h.To = AttackSpot(h, h.Target);
				float eased = 1f - (1f - t) * (1f - t);
				h.Pos = h.From.Lerp(h.To, eased);
				Show(h, AnimPounce, 1 + Math.Min(2, (int)(t * 3f)), 1f);
				EmitTrail(h, dt);
				if (t >= 1f)
				{
					Bite(h);
					h.State = State.Bite;
					h.T -= h.Duration;
				}
				return;
			}
			case State.Bite:
				Show(h, AnimPounce, h.T < BiteSeconds * 0.5f ? 4 : 5, 1f);
				if (h.T >= BiteSeconds)
					Settle(h);
				return;
			case State.Swoop:
				TickSwoop(h, dt);
				return;
		}

		// Heel or chase. First the leash: too far, stuck, or standing on something solid - blink home.
		float fromFeet = h.Pos.DistanceTo(feet);
		if (fromFeet > BlinkDistance)
		{
			StartBlink(h, BlinkWhy.Leash);
			return;
		}
		if (!flies)
		{
			if (h.Stuck >= StuckSeconds)
			{
				StartBlink(h, BlinkWhy.Stuck);
				return;
			}
			if (nav != null && nav.IsBlockedAt(h.Pos))
			{
				h.Inside += dt;
				if (h.Inside > 0.25f)
				{
					StartBlink(h, BlinkWhy.Inside);
					return;
				}
			}
			else
				h.Inside = 0f;
		}

		if (h.HowlNext)
		{
			h.HowlNext = false;
			h.State = State.Howl;
			h.T = 0f;
			return;
		}

		h.Retarget -= dt;
		if (h.Retarget <= 0f || !Alive(h.Target) || h.Target.GlobalPosition.DistanceTo(player) > huntRange * 1.3f)
		{
			h.Retarget = RetargetSeconds;
			h.Target = Acquire(h, player, nav);
		}

		if (h.Target == null)
		{
			h.State = State.Heel;
			Vector2 heel = flies ? HeelSpot(index) + new Vector2(0f, 6f) : HeelSpot(index);
			float speed = fromFeet > 160f ? SprintSpeed : RunSpeed;
			bool arrived = flies ? Fly(h, heel, FlySpeed, dt) : Walk(h, heel, speed, dt, nav, homeward: true);
			if (arrived)
			{
				// At heel: face the way the wizard faces, or the nearest trouble.
				Show(h, AnimIdle, -1, 1f);
			}
			else
				Show(h, AnimRun, -1, flies ? 1f : Mathf.Clamp(speed / RunSpeed, 0.8f, 1.5f));
			return;
		}

		h.State = State.Chase;
		Enemy quarry = h.Target;
		if (flies)
		{
			float dist = h.Pos.DistanceTo(quarry.GlobalPosition + new Vector2(0f, GroundBelowCentre));
			if (h.HasBite && dist <= leapReach * 2.4f)
			{
				StartSwoop(h, quarry);
				return;
			}
			Vector2 hold = quarry.GlobalPosition + new Vector2(0f, GroundBelowCentre) + (h.Pos - quarry.GlobalPosition).Normalized() * leapReach * 1.6f;
			if (Fly(h, h.HasBite ? quarry.GlobalPosition + new Vector2(0f, GroundBelowCentre) : hold, FlySpeed, dt))
				Show(h, AnimIdle, -1, 1f);
			else
				Show(h, AnimRun, -1, 1f);
			return;
		}

		Vector2 spot = AttackSpot(h, quarry);
		float toSpot = h.Pos.DistanceTo(spot);
		if (h.HasBite && toSpot <= leapReach && LeapClear(h.Pos, spot, nav))
		{
			h.Facing = quarry.GlobalPosition.X >= h.Pos.X ? 1f : -1f;
			h.State = State.Crouch;
			h.T = 0f;
			return;
		}
		if (!h.HasBite && toSpot < leapReach * 0.7f)
		{
			// Waiting on the next bite: hold off a little, facing it, ready.
			h.Facing = quarry.GlobalPosition.X >= h.Pos.X ? 1f : -1f;
			Show(h, AnimIdle, -1, 1.6f);
			return;
		}
		if (Walk(h, spot, RunSpeed * 1.1f, dt, nav, homeward: false))
			Show(h, AnimIdle, -1, 1f);
		else
			Show(h, AnimRun, -1, 1.1f);
	}

	private void Settle(Hound h)
	{
		h.State = h.Target != null ? State.Chase : State.Heel;
		h.T = 0f;
	}

	private static bool Alive(Enemy e) =>
		e != null && IsInstanceValid(e) && !e.IsDying && e.InEnemyRegistry;

	// The quarry: the enemy nearest the WIZARD inside the hunting range, one no other hound has claimed
	// if there is one, and one this hound can walk to in a straight line (no lake or wall between them).
	private Enemy Acquire(Hound h, Vector2 player, MazeNavigation nav)
	{
		EnemyRegistry.CollectNear(player, huntRange, nearBuffer);
		Enemy blockedA = null, blockedB = null;
		for (int attempt = 0; attempt < 3; attempt++)
		{
			Enemy best = null;
			float bestScore = float.MaxValue;
			float rangeSq = huntRange * huntRange;
			foreach (Enemy e in nearBuffer)
			{
				if (!Alive(e) || e == blockedA || e == blockedB)
					continue;
				float dPlayer = e.GlobalPosition.DistanceSquaredTo(player);
				if (dPlayer > rangeSq)
					continue;
				float score = MathF.Sqrt(dPlayer) + 0.25f * e.GlobalPosition.DistanceTo(h.Pos);
				if (Claimed(h, e))
					score += 160f;
				if (score < bestScore)
				{
					bestScore = score;
					best = e;
				}
			}
			if (best == null || flies || nav == null || nav.HasClearWalk(h.Pos, AttackSpot(h, best)))
			{
				nearBuffer.Clear();
				return best;
			}
			if (blockedA == null) blockedA = best;
			else blockedB = best;
		}
		nearBuffer.Clear();
		return null;
	}

	private bool Claimed(Hound me, Enemy e)
	{
		foreach (Hound h in hounds)
			if (h != me && h.Target == e)
				return true;
		return false;
	}

	// Where to stand so the jaws are on the quarry: beside it on its own ground line, on this side of it.
	// The side holds while the hound is nearly level with its quarry, or a hound passing under it would
	// swap sides every frame.
	private Vector2 AttackSpot(Hound h, Enemy e)
	{
		float dx = e.GlobalPosition.X - h.Pos.X;
		float side = dx > 6f ? -1f : dx < -6f ? 1f : -h.Facing;
		return e.GlobalPosition + new Vector2(side * JawReach, GroundBelowCentre);
	}

	private static bool LeapClear(Vector2 from, Vector2 to, MazeNavigation nav) =>
		nav == null || (!nav.IsBlockedAt(to) && nav.HasClearWalk(from, to));

	private Vector2 HeelSpot(int index)
	{
		Vector2 feet = PlayerRef.FootPosition;
		Vector2 side = new Vector2(-heelDir.Y, heelDir.X) * ((index % 2 == 0) ? 1f : -1f);
		return feet + heelDir * (HeelBehind + 10f * (index / 2)) + side * (HeelSide + 14f * (index / 2));
	}

	// A heel spot on open ground: the hound's own, or the first of eight round the wizard that is.
	private Vector2 FreeHeelSpot(int index, MazeNavigation nav)
	{
		Vector2 want = HeelSpot(index);
		if (nav == null || flies || !nav.IsBlockedAt(want))
			return want;
		Vector2 feet = PlayerRef.FootPosition;
		for (int k = 0; k < 8; k++)
		{
			float a = k * Mathf.Tau / 8f;
			Vector2 p = feet + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 46f;
			if (!nav.IsBlockedAt(p))
				return p;
		}
		return feet;
	}

	// One step toward a goal on the ground. Never onto a blocked cell: it slides along one instead, and
	// on the way home it takes the navigation's route round water and walls. True once there.
	private bool Walk(Hound h, Vector2 goal, float speed, float dt, MazeNavigation nav, bool homeward)
	{
		Vector2 to = goal - h.Pos;
		float dist = to.Length();
		if (dist < 3f)
		{
			h.Stuck = MathF.Max(0f, h.Stuck - dt * 2f);
			return true;
		}
		Vector2 dir = to / dist;
		if (homeward && nav != null && !nav.HasClearWalk(h.Pos, goal))
		{
			Vector2 flow = nav.FlowDirectionAt(h.Pos);
			if (flow != Vector2.Zero)
				dir = flow;
		}
		float step = MathF.Min(speed * dt, dist);
		Vector2 next = h.Pos + dir * step;
		if (nav != null && nav.IsBlockedAt(next))
		{
			Vector2 alongX = h.Pos + new Vector2(MathF.Sign(dir.X) * step, 0f);
			Vector2 alongY = h.Pos + new Vector2(0f, MathF.Sign(dir.Y) * step);
			if (MathF.Abs(dir.X) > 0.05f && !nav.IsBlockedAt(alongX))
				next = alongX;
			else if (MathF.Abs(dir.Y) > 0.05f && !nav.IsBlockedAt(alongY))
				next = alongY;
			else
			{
				h.Stuck += dt;
				StatWorstStuck = MathF.Max(StatWorstStuck, h.Stuck);
				return false;
			}
		}
		h.Stuck = MathF.Max(0f, h.Stuck - dt);
		if (MathF.Abs(next.X - h.Pos.X) > 0.05f)
			h.Facing = next.X > h.Pos.X ? 1f : -1f;
		h.Pos = next;
		return false;
	}

	// The griffin: straight there, over anything.
	private bool Fly(Hound h, Vector2 goal, float speed, float dt)
	{
		Vector2 to = goal - h.Pos;
		float dist = to.Length();
		if (dist < 4f)
			return true;
		Vector2 step = to / dist * MathF.Min(speed * dt, dist);
		if (MathF.Abs(step.X) > 0.05f)
			h.Facing = step.X > 0f ? 1f : -1f;
		h.Pos += step;
		return false;
	}

	// Hounds do not stand in each other.
	private void Separate(float dt, MazeNavigation nav)
	{
		for (int i = 0; i < hounds.Count; i++)
		{
			Hound a = hounds[i];
			if (a.State is State.Leap or State.Swoop or State.BlinkOut)
				continue;
			for (int j = i + 1; j < hounds.Count; j++)
			{
				Hound b = hounds[j];
				if (b.State is State.Leap or State.Swoop or State.BlinkOut)
					continue;
				Vector2 d = a.Pos - b.Pos;
				float l = d.Length();
				const float Gap = 22f;
				if (l >= Gap || l < 0.01f)
					continue;
				Vector2 push = d / l * (Gap - l) * MathF.Min(1f, dt * 8f) * 0.5f;
				Nudge(a, push, nav);
				Nudge(b, -push, nav);
			}
		}
	}

	private void Nudge(Hound h, Vector2 by, MazeNavigation nav)
	{
		Vector2 next = h.Pos + by;
		if (flies || nav == null || !nav.IsBlockedAt(next))
			h.Pos = next;
	}

	private void StartBlink(Hound h, BlinkWhy why)
	{
		if (h.State == State.BlinkOut)
			return;
		switch (why)
		{
			case BlinkWhy.Stuck: StatBlinksStuck++; break;
			case BlinkWhy.Leash: StatBlinksLeash++; break;
			case BlinkWhy.Inside: StatBlinksInside++; break;
		}
		h.Why = why;
		h.Leaving = why == BlinkWhy.Dismiss;
		h.State = State.BlinkOut;
		h.T = 0f;
		Puff(h.Pos, inward: false);
	}

	// --- the bite -----------------------------------------------------------------------------------

	private void Bite(Hound h)
	{
		h.HasBite = false;
		if (PlayerRef == null || SpellData == null)
			return;
		Enemy quarry = Alive(h.Target) ? h.Target : null;
		Vector2 centre = quarry?.GlobalPosition ?? h.Pos + new Vector2(h.Facing * JawReach, -GroundBelowCentre);
		StatBites++;
		BiteAt(centre, quarry);
		if (threeHeads)
		{
			// The other two heads take the two enemies nearest the quarry.
			float reach = CerberusReach * areaMul;
			Enemy second = null, third = null;
			float d2 = float.MaxValue, d3 = float.MaxValue;
			EnemyRegistry.CollectNear(centre, reach, nearBuffer);
			foreach (Enemy e in nearBuffer)
			{
				if (!Alive(e) || e == quarry)
					continue;
				float d = e.GlobalPosition.DistanceSquaredTo(centre);
				if (d > reach * reach)
					continue;
				if (d < d2)
				{
					third = second; d3 = d2;
					second = e; d2 = d;
				}
				else if (d < d3)
				{
					third = e; d3 = d;
				}
			}
			nearBuffer.Clear();
			if (second != null)
				BiteAt(second.GlobalPosition, second);
			if (third != null)
				BiteAt(third.GlobalPosition, third);
		}
		SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellFamiliarBite, centre, 0.08f, SfxCatalog.SpellFamiliarBite, SoundGapMs);
	}

	// One set of jaws closing at `centre`: the quarry, and everything else whose centre is inside them.
	private void BiteAt(Vector2 centre, Enemy quarry)
	{
		if (quarry != null)
			PlayerRef.DealDamageToEnemy(quarry, damage, source: SpellData);
		float r2 = biteRadius * biteRadius;
		EnemyRegistry.CollectNear(centre, biteRadius, nearBuffer);
		foreach (Enemy e in nearBuffer)
		{
			if (e == quarry || !Alive(e) || e.GlobalPosition.DistanceSquaredTo(centre) > r2)
				continue;
			PlayerRef.DealDamageToEnemy(e, damage, source: SpellData);
		}
		nearBuffer.Clear();
		AddSnap(centre, biteRadius);
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, centre, biteRadius, ramp, tint);
	}

	// --- the griffin's swoop -----------------------------------------------------------------------

	private void StartSwoop(Hound h, Enemy quarry)
	{
		Vector2 ground = quarry.GlobalPosition + new Vector2(0f, GroundBelowCentre);
		Vector2 dir = ground - h.Pos;
		if (dir.LengthSquared() < 1f)
			dir = new Vector2(h.Facing, 0f);
		dir = dir.Normalized();
		h.From = h.Pos;
		h.To = ground + dir * SwoopOvershoot;
		h.Duration = Mathf.Clamp(h.From.DistanceTo(h.To) / SwoopSpeed, 0.18f, 0.6f);
		h.Facing = dir.X >= 0f ? 1f : -1f;
		h.SwoopHits.Clear();
		h.HasBite = false;
		h.State = State.Swoop;
		h.T = 0f;
		StatSwoops++;
	}

	private void TickSwoop(Hound h, float dt)
	{
		float t = Mathf.Clamp(h.T / h.Duration, 0f, 1f);
		Vector2 prev = h.Pos;
		h.Pos = h.From.Lerp(h.To, t);
		Show(h, AnimPounce, Math.Min(5, 1 + (int)(t * 4.5f)), 1f);
		StrikeAlong(h, prev, h.Pos);
		EmitFeathers(h, prev, h.Pos, dt);
		if (t >= 1f)
		{
			h.State = State.Heel;
			h.T = 0f;
			h.Target = null;
			h.Retarget = 0f;
		}
	}

	// Everything whose centre is within the swath of the stretch flown this frame, once per swoop. The
	// swoop flies on the ground line; an enemy's ground point is its centre plus GroundBelowCentre.
	private void StrikeAlong(Hound h, Vector2 a, Vector2 b)
	{
		if (PlayerRef == null || SpellData == null)
			return;
		float half = SwoopHalfWidth * areaMul + RiptideWave.BodyAllowance;
		Vector2 mid = (a + b) * 0.5f - new Vector2(0f, GroundBelowCentre);
		float reach = a.DistanceTo(b) * 0.5f + half;
		EnemyRegistry.CollectNear(mid, reach, nearBuffer);
		Vector2 ab = b - a;
		float ll = MathF.Max(1e-3f, ab.LengthSquared());
		foreach (Enemy e in nearBuffer)
		{
			if (!Alive(e) || h.SwoopHits.Contains(e))
				continue;
			Vector2 p = e.GlobalPosition + new Vector2(0f, GroundBelowCentre);
			float u = Mathf.Clamp((p - a).Dot(ab) / ll, 0f, 1f);
			if ((a + ab * u).DistanceSquaredTo(p) > half * half)
				continue;
			h.SwoopHits.Add(e);
			PlayerRef.DealDamageToEnemy(e, damage, source: SpellData);
			StatBites++;
			if (GetParent() is Node2D arena)
				ImpactDebrisLayer.Spawn(arena, e.GlobalPosition, MathF.Max(10f, half * 0.6f), ramp, tint);
			SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellFamiliarBite, e.GlobalPosition, 0.08f, SfxCatalog.SpellFamiliarBite, SoundGapMs);
		}
		nearBuffer.Clear();
	}

	// --- animation ---------------------------------------------------------------------------------

	private static void Show(Hound h, StringName anim, int frame, float speed)
	{
		AnimatedSprite2D s = h.Sprite;
		if (s == null || s.SpriteFrames == null)
			return;
		if (frame < 0)
		{
			if (s.Animation != anim || !s.IsPlaying())
				s.Play(anim);
			s.SpeedScale = speed;
			return;
		}
		if (s.Animation != anim)
			s.Animation = anim;
		if (s.IsPlaying())
			s.Pause();
		s.Frame = frame;
	}

	// --- effects: fixed pools, drawn on the 2 px grid ------------------------------------------------

	private void AddMote(Vector2 at, Vector2 vel, float life, Color colour, float size)
	{
		motes[nextMote] = new Mote { At = at, Vel = vel, Life = life, Colour = colour, Size = size, Live = true };
		nextMote = (nextMote + 1) % MaxMotes;
	}

	private void AddSnap(Vector2 at, float radius)
	{
		snaps[nextSnap] = new Snap { At = at, Radius = radius, Live = true };
		nextSnap = (nextSnap + 1) % MaxSnaps;
	}

	// A blink: a puff of the hound's own light, flying out from where it vanished or in to where it comes.
	private void Puff(Vector2 at, bool inward)
	{
		Vector2 body = at - new Vector2(0f, 16f);
		for (int i = 0; i < 12; i++)
		{
			float a = i * Mathf.Tau / 12f + rng.Randf() * 0.4f;
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			float d = rng.RandfRange(18f, 30f);
			Color c = i % 3 == 0 ? ramp.Core : i % 3 == 1 ? ramp.Hot : ramp.Mid;
			if (inward)
				AddMote(body + dir * d, -dir * d / 0.22f, 0.22f, c, 4f);
			else
				AddMote(body + dir * 4f, dir * d / 0.26f, 0.26f, c, 4f);
		}
	}

	private void EmitTrail(Hound h, float dt)
	{
		h.EmitClock += dt;
		while (h.EmitClock >= 0.03f)
		{
			h.EmitClock -= 0.03f;
			Vector2 back = h.Pos + new Vector2(-h.Facing * rng.RandfRange(6f, 14f), -rng.RandfRange(14f, 26f));
			AddMote(back, new Vector2(-h.Facing * 30f, -20f), 0.24f, rng.Randf() < 0.5f ? ramp.Hot : ramp.Mid, 2f);
		}
	}

	private void EmitHowl(Hound h, float dt)
	{
		h.EmitClock += dt;
		while (h.EmitClock >= 0.07f)
		{
			h.EmitClock -= 0.07f;
			float head = CellOf(form) * 0.8f;
			Vector2 at = h.Pos + new Vector2(h.Facing * rng.RandfRange(4f, 14f), -head - rng.RandfRange(0f, 8f));
			AddMote(at, new Vector2(h.Facing * rng.RandfRange(6f, 18f), -rng.RandfRange(40f, 70f)), 0.5f,
				rng.Randf() < 0.5f ? ramp.Core : ramp.Hot, rng.Randf() < 0.3f ? 4f : 2f);
		}
	}

	// The swoop sheds feathers of the wizard's indigo and its own light, inside the swath it struck.
	private void EmitFeathers(Hound h, Vector2 a, Vector2 b, float dt)
	{
		float half = SwoopHalfWidth * areaMul;
		Vector2 ab = b - a;
		if (ab.LengthSquared() < 1f)
			return;
		Vector2 n = new Vector2(-ab.Y, ab.X).Normalized();
		int count = Math.Max(1, (int)(ab.Length() / 10f));
		for (int i = 0; i < count; i++)
		{
			Vector2 p = a.Lerp(b, rng.Randf()) - new Vector2(0f, GroundBelowCentre) + n * rng.RandfRange(-half, half);
			Color c = (i % 3) switch { 0 => ramp.Hot, 1 => new Color(0.42f, 0.53f, 0.88f), _ => ramp.Mid };
			AddMote(p, n * rng.RandfRange(-16f, 16f) + new Vector2(0f, 12f), 0.35f, c, i % 4 == 0 ? 4f : 2f);
		}
	}

	private void TickFx(float dt)
	{
		for (int i = 0; i < MaxMotes; i++)
		{
			if (!motes[i].Live)
				continue;
			motes[i].Age += dt;
			motes[i].At += motes[i].Vel * dt;
			if (motes[i].Age >= motes[i].Life)
				motes[i].Live = false;
		}
		for (int i = 0; i < MaxSnaps; i++)
		{
			if (!snaps[i].Live)
				continue;
			snaps[i].Age += dt;
			if (snaps[i].Age >= SnapSeconds)
				snaps[i].Live = false;
		}
	}

	private const float SnapSeconds = 0.18f;
	private const int SnapTeeth = 5;

	private void DrawFx()
	{
		for (int i = 0; i < MaxMotes; i++)
		{
			ref Mote m = ref motes[i];
			if (!m.Live)
				continue;
			float f = m.Age / m.Life;
			Color c = new Color(m.Colour.R * tint.R, m.Colour.G * tint.G, m.Colour.B * tint.B, f < 0.6f ? 1f : (1f - f) / 0.4f);
			fx.DrawRect(WhipLimb.Cell(m.At, m.Size), c);
		}
		// The jaws: two rows of teeth closing on the bite, the outermost on its radius, then a flash.
		for (int i = 0; i < MaxSnaps; i++)
		{
			ref Snap s = ref snaps[i];
			if (!s.Live)
				continue;
			float t = s.Age / SnapSeconds;
			float close = MathF.Min(1f, t / 0.55f);
			float gap = s.Radius * (1f - close * 0.85f);
			float alpha = t < 0.7f ? 1f : (1f - t) / 0.3f;
			// Each tooth sits on an arc inside the radius and closes toward the bite's centre line, so
			// nothing is ever drawn past what the bite tested.
			float rr = MathF.Max(4f, s.Radius - 2f);
			for (int k = 0; k < SnapTeeth; k++)
			{
				float a = Mathf.Lerp(-2.55f, -0.59f, k / (float)(SnapTeeth - 1));
				var up = new Vector2(MathF.Cos(a) * rr, MathF.Sin(a) * rr * (1f - close * 0.85f));
				var down = new Vector2(up.X, -up.Y);
				Color c = k % 2 == 0 ? ramp.Core : ramp.Hot;
				c = new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha);
				fx.DrawRect(WhipLimb.Cell(s.At + up, 4f), c);
				fx.DrawRect(WhipLimb.Cell(s.At + down, 4f), c);
				fx.DrawRect(WhipLimb.Cell(s.At + up * 0.7f, 2f), c);
				fx.DrawRect(WhipLimb.Cell(s.At + down * 0.7f, 2f), c);
			}
			if (close >= 1f && gap >= 0f)
			{
				Color flash = new Color(ramp.Core.R * tint.R, ramp.Core.G * tint.G, ramp.Core.B * tint.B, alpha);
				fx.DrawRect(WhipLimb.Cell(s.At, 6f), flash);
			}
		}
	}
}
