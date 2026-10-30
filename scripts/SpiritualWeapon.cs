using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Spiritual Weapon: a weapon of light that floats at the wizard's side, picks an enemy, rises into the
// air over it, swings down onto it and winds up again (.ai/spell-roster.md, "Spiritual Weapon").
//
// WHY IT CHANGED (2026-10-28, Eric): "spiritual weapon should be able to select one of 3 weapons when you
// initially pick it, they each have different advantages. axe is heavier but more damage, hammer is aoe
// that shakes ground and slows enemies, and sword is the most direct damage to single targets, faster
// swings ... the animation for this should be that there is a single floating weapon that targets
// enemies. it should animate by raising into the air, swinging down onto the targeted enemy, and then
// winding up again. mutations/ascensions can up the amount as well, or change the weapon into a whole
// new, much bigger and more detailed version of the weapon." It was blades on a fixed orbit round the
// caster, the job Cyclone Slash does; it is now the one spell that picks a quarry and hits it from above.
//
// THE FORMS (SpellData.Variants, chosen once on the page after the spell's card; the decision record is
// .ai/decisions/2026-10-28-spell-variants.md). The form is the run's copy of the spell's
// SelectedVariantId, read once per cooldown:
//   Axe    - heavy, slow, the most damage a blow. Cleaves everything within AxeCleaveRadius of its quarry
//            and throws it back. Goes for the enemy nearest the wizard.
//   Hammer - an area slam: everything within HammerSlamRadius is hit, slowed and jolted, the ground cracks
//            and shakes, and enemy bolts inside the slam are crushed. Goes for the densest knot in reach.
//   Sword  - the fastest swings; a blow lands on its quarry and nothing else. Goes for the toughest enemy
//            in reach, and cuts enemy bolts out of the air anywhere along its blade (ParryBolts).
//
// THE SIGNATURE (aim / cadence / demand / payload). Aim: per form, above. Cadence: every cooldown hands
// each weapon one swing (Arm), staggered across the interval so several weapons fall in turn; a weapon
// holding one spends it the moment it is over its quarry. Demand: none it enforces; it guards, so it
// fights within the spell's range of the wizard. Payload: one blow from above.
//
// THE SWING. Each weapon is a grip point in the air (Pivot) and a heading (Angle) from the grip to its
// head. Hunting, it hangs head-up beside its quarry; RAISE lifts it and winds it back over its shoulder;
// STRIKE brings it over the top and down until its head is on the quarry - the blow resolves on that
// frame, at the head; RECOVER lets it bounce up and settle head-up again. Its shadow on the ground under
// it is what says it is in the air. What a swing does is a Cut (the table below): the heading it winds
// back to, how far it turns coming down and settling, where its grip goes, how long each part takes.
// The canonical angles are for a weapon to the RIGHT of its quarry; one to the left mirrors them, so the
// arc always goes over the top.
//
// THE SWORD AT SPEED (2026-10-30, Eric: "make sure the animation for the sword resets cleanly, and doesn't
// look goofy if it's repeatedly striking too fast"). Its swing took a fixed quarter second (nearly half
// under Thousand Cuts) whatever the cooldown, so at level 8 with cooldown boons the swings queued back to
// back: the sword stopped dead at the top of every bounce and jerked straight into the next wind-up, and
// a raise whose quarry died mid-air was dropped half-drawn. Now (sword only - the axe and the hammer swing
// well inside their cooldowns): the swing is fitted inside SwingBudget of the interval, the wind-up and
// recovery giving way first and the cut itself a little (pace); a swing that is already due starts from
// partway up the recovery rather than from a dead stop; every swing starts from wherever the blade is,
// turning the short way round; a raise whose quarry falls re-aims at the enemy nearest where it was going;
// and the last swing's slash and afterimages finish fading on their own, at the poses they were drawn at,
// rather than being cut off by the next.
//
// THE HOVER (2026-10-30, Eric, playing 0.9.2: "Spiritual weapons should hover a little bit further away
// from the player and have a small hover animation to make them appear to be actually floating in
// space"). A weapon waiting - at his side or beside its quarry - rises and falls three art pixels, drifts
// one side to side at half the rate and rocks a few degrees as it drifts (Hover, HoverTilt). The offset is
// whole art pixels, so it steps a pixel at a time on the world's 2-unit cell and never swims; each weapon
// breathes from its own phase at its own rate, so a flight of them never bobs in lockstep. At his side it
// hangs further out than it did (HeelReach), clear of his silhouette, a thing of its own.
//
// SPREAD OUT (2026-10-30, Eric: "spiritual weapon, when you have multiples, should always target enemies
// that are a certain distance away from each other. multiple weapons can target the same miniboss or boss,
// but the sprites should not stack on top of each other when targeting"). A weapon choosing a quarry passes
// over any enemy within TargetSpacing of another weapon's quarry while an enemy farther out is in reach;
// an elite, miniboss or boss is exempt and may be shared. Weapons that do end up on one quarry (or, with
// nothing else in reach, on neighbours) take stations round it (Station): alternate sides, and each pair
// further out and higher than the last, so their drawings never lie on top of each other.
//
// TWIN BLADES (2026-10-30, Eric: "Twin sword mutation should create a second smaller dagger, and be capped
// at two blades"). One dagger of light, however many swords the levels have fanned out: the off hand. It
// rests on his other side, keeps to the far flank of the main-hand sword's quarry, and in the gap after
// each of that sword's cuts darts in and stabs it (Stab, DaggerShare of a blow). It was +1 sword at -10%,
// which the levels already give.
//
// DEFLECTION (RiposteId; 2026-10-30, Eric: "Riposte description is confusing. Replace with the ability to
// deflect projectiles"). Every sword still cuts a bolt that touches its blade. With Deflection a sword
// waiting by him or beside its quarry watches the bolts coming at the wizard and breaks off for one: it
// darts to where the bolt will be, its blade across the bolt's path, and swats it out of the air - broken,
// its pieces thrown on across the path, never sent back (returning a bolt is Laelaps' Endless Chase's verb).
// Then that sword rests DeflectRest before it breaks off again, so it is a guard and not a wall, as the
// Familiar's catch is (Familiar.CatchRule). A sword mid-swing finishes its swing first.
//
// THE DRAWING. tools/art/spirit_weapons.py draws every form at 32 headings (pixel art turned by the engine
// smears; a sheet rasterised at the heading keeps its pixels - SleetRunes' reason) on the elite cell, the
// three colossal Ascension forms on the boss cell, twice the size, and Twin Blades' dagger on the
// projectile cell. The node picks the cell nearest the heading and draws it at x2. Nothing is instanced per
// swing: weapons, trails, motes, cracks and hallowed ground are slots in fixed arrays, drawn by four canvas
// items - the afterimages and the air above the actors, the quake and the shadows under them - every one
// opaque pixel art on the world's 2 px grid (spirit_weapon_air.gdshader turns their alpha into dithered
// coverage; spirit_quake.gdshader draws the cracks and the holy ground, 2026-10-29).
//
// NO MARKERS (art-direction.md section 3): a blow's reach is shown by ImpactDebris thrown out to exactly
// it (the cleave, the slam), and a slam's cracks run out to the radius it hit and stop there.
public partial class SpiritualWeapon : Node2D
{
	// --- the forms and their milestones (SpellData_SpiritualWeapon.tres; SpellEvolutionCatalog) ---------
	public const string AxeId = "spiritual_weapon_axe";
	public const string HammerId = "spiritual_weapon_hammer";
	public const string SwordId = "spiritual_weapon_sword";

	public const string BroadaxeId = "spiritual_weapon_axe_broadaxe";
	public const string HeadsmanId = "spiritual_weapon_axe_headsman";
	public const string DawnsplitterId = "spiritual_weapon_axe_dawnsplitter";
	public const string GreataxeId = "spiritual_weapon_axe_executioners_greataxe";
	public const string WhirlingAxesId = "spiritual_weapon_axe_whirling_axes";

	public const string AftershockId = "spiritual_weapon_hammer_aftershock";
	public const string EarthshakerId = "spiritual_weapon_hammer_earthshaker";
	public const string HallowedGroundId = "spiritual_weapon_hammer_hallowed_ground";
	public const string TitansMaulId = "spiritual_weapon_hammer_titans_maul";
	public const string HammerfallId = "spiritual_weapon_hammer_hammerfall";

	public const string TwinBladesId = "spiritual_weapon_sword_twin_blades";
	// The card is Deflection now (2026-10-30); the id is the one saves and the catalog already hold.
	public const string RiposteId = "spiritual_weapon_sword_riposte";
	public const string SoulSiphonId = "spiritual_weapon_sword_soul_siphon";
	public const string GreatswordId = "spiritual_weapon_sword_celestial_greatsword";
	public const string ThousandCutsId = "spiritual_weapon_sword_thousand_cuts";

	public enum Form : byte { Axe, Hammer, Sword }

	/// <summary>The most weapons in the air, whatever the count says. Twin Blades' dagger is not one of them.</summary>
	public const int MaxWeapons = 6;

	// --- what a blow reaches, in world pixels before the player's Area, the form's Area and its Scale ----
	/// <summary>How far from where the sword's blade comes down its quarry may stand and still be cut.</summary>
	public const float SwordReach = 34f;
	// The axe's cleave is the smallest area of the three (it was 46): the hammer is the area weapon. Its
	// enemies a swing did not fall with it - about six at level 8 at either size, in the balance bot's
	// crowd - so the axe was brought into its band by its numbers, not this (.ai/spell-roster.md 2f).
	public const float AxeCleaveRadius = 38f;
	public const float HammerSlamRadius = 64f;
	/// <summary>The axe's throw, in px/s for Enemy.ApplyKnockback; Executioner's Greataxe doubles it.</summary>
	public const float AxeKnockback = 240f;

	// The hammer's slow, before its level table and Earthshaker; and the jolt that is the ground shaking.
	public const float HammerSlow = 0.30f;
	public const float HammerSlowSeconds = 1.2f;
	private const float HammerJoltSeconds = 0.12f;
	private const float MaxSlow = 0.75f;

	// Aftershock: the second wave.
	public const float AftershockDelay = 0.4f;
	public const float AftershockReach = 1.4f;
	public const float AftershockShare = 0.6f;

	// Headsman: an enemy under this much of its health takes double.
	public const float HeadsmanThreshold = 0.5f;

	// Hallowed Ground: what standing in it does, re-applied every tick (stacking-resistant underneath).
	private const float HallowWeaken = 0.3f;
	private const float HallowSlow = 0.25f;
	private const float HallowTick = 0.25f;

	// The sword's guard: half the width of the blade that cuts bolts.
	private const float ParryHalfWidth = 10f;

	// Deflection (RiposteId): a sword watches the bolts coming at the wizard inside DeflectWatch of him, and
	// one whose meeting point is inside DeflectReach of its blade it breaks off for, reaching it in
	// DeflectSeconds; then it rests DeflectRest before it will break off again. The Familiar's crow watches
	// 320 and reaches 230; a sword, which also has a quarry to keep, a little less.
	public const float DeflectWatch = 260f;
	public const float DeflectReach = 220f;
	public const float DeflectSeconds = 0.16f;
	public const float DeflectRest = 0.8f;
	private const float DeflectThreatCosine = 0.6f;

	// Twin Blades: the dagger's stab is DaggerShare of a sword's blow, once in the gap after each of the
	// main-hand sword's cuts, DaggerGap after it lands. At level 4 (two swords) that is +35% on the spell,
	// where the old extra sword at -10% each was +35% too; at level 7 (three) +23%, against its +20%.
	public const float DaggerShare = 0.7f;
	public const float DaggerReach = 26f;
	private const float DaggerGap = 0.04f;
	private const float DaggerFlySpeed = 760f;

	// Thousand Cuts: the flurry. A cut whose quarry has fallen goes to the nearest enemy within FlurryReach
	// of the blade instead of being lost: without it, in a crowd of fodder the first cut killed and the other
	// two never landed, and the boss-killer measured half its peers (217 a second at level 8).
	public const int FlurryCuts = 3;
	public const float FlurryShare = 0.85f;
	private const float FlurryReach = 120f;

	// The sword at speed: its whole swing (the flurry's included) is fitted inside SwingBudget of the
	// interval, leaving the rest to get back over its quarry; the wind-up and recovery shorten by the pace,
	// the cut by its square root, and never past MaxPace (a cut at a third of its time is a flicker). A
	// swing already due starts once the recovery is RecoverCutIn of the way up.
	private const float SwingBudget = 0.8f;
	private const float MaxPace = 2.5f;
	private const float RecoverCutIn = 0.55f;

	// Spread out: no two weapons on quarries closer than this (an elite, a miniboss or a boss may be shared),
	// while anything farther out is in reach; about the width of a weapon's drawing.
	private const float TargetSpacing = 96f;

	// Soul Siphon: at most this much healed a second, whatever the swords land.
	public const int SiphonPerSecond = 3;

	// --- the drawings (tools/art/spirit_weapons.py), in world pixels at x2 ---------------------------------
	private const int Headings = 32;
	private const int BaseCell = 48;
	private const int ColossalCell = 96;
	private const int DaggerCell = 32;
	// From the grip to the middle of the drawing, and to the head (the axe's bit, the hammer's face, the
	// sword's and the dagger's point of percussion two thirds up the blade), along the heading.
	private const float BaseGripToCentre = 26f;
	private const float BaseGripToHead = 52f;
	private const float ColossalGripToCentre = 56f;
	private const float ColossalGripToHead = 112f;
	private const float DaggerGripToCentre = 17f;
	private const float DaggerGripToHead = 32f;
	private const string DaggerSheetPath = "res://assets/bonelight/effects/spirit-dagger.png";

	// --- at his side ----------------------------------------------------------------------------------------
	// The weapons at rest hang in a shallow arc behind the way he faces: HeelBack degrees off straight up, at
	// HeelReach from his middle, up to HeelFan degrees between neighbours (the fan never spans more than
	// HeelFanSpan) and the outer ones a little further out. At 46 px and 38 degrees (until 2026-10-30) a
	// weapon's drawing sat on his shoulder and read as part of him, and a fan of three arced up over his
	// hat; out here, centred nearly level with him, the fan runs down his back side and every blade clears
	// his outline by about its own width.
	private const float HeelReach = 70f;
	private const float HeelBack = 72f;
	private const float HeelFan = 34f;
	private const float HeelFanSpan = 100f;
	private const float HeelFanReach = 8f;
	private const float HeelDrop = 18f;
	// Twin Blades' dagger rests on his other side, in front where the swords are behind: the off hand.
	private const float DaggerHeelReach = 62f;
	private const float DaggerHeelAngle = 50f;

	// --- the hover ------------------------------------------------------------------------------------------
	private const float HoverRise = 6f;          // world px either way: three art pixels
	private const float HoverSway = 2f;          // one art pixel, at half the rate of the rise
	private const float HoverTiltDegrees = 7f;   // past half a heading cell, so at the ends it rocks one cell
	private const float HoverRate = 2.6f;        // radians a second of the rise: a breath every 2.4 s
	private const float GoldenAngle = 2.39996f;  // between neighbours' phases, so no two start together

	// --- the swing, in degrees, for a weapon to the right of its quarry (mirrored on the left) -------------
	private const float CanonImpact = 145f;   // head down onto the quarry, from above and to its right
	private const float CanonRaised = 345f;   // wound back over its shoulder: the arc runs over the top
	private const float CanonIdle = 285f;     // hanging head-up, leaning away from its quarry
	private const float FlurryRaised = 230f;  // a flurry's short lift between cuts
	private const float DaggerIdle = 160f;    // the dagger hangs pointed at its quarry, cocked to stab

	// One swing, canonical (a weapon to the right of its quarry; mirrored on the left). Raised is the heading
	// it winds back to; Sweep how far it turns coming down onto the quarry, signed (negative turns the heading
	// down, which for a weapon on the right is over the top); Settle how far it turns after. Lift is where the
	// grip is wound back to from the grip it lands at, and Withdraw where it goes after, in GripToHead with x
	// away from the quarry. Then the seconds of each part, before the pace and the spell's tempo. The blow is
	// tested at the head, and the slash is the head's path: both from these numbers.
	private readonly struct Cut
	{
		public Cut(float raised, float sweep, float settle, Vector2 lift, Vector2 withdraw, float raise, float strike, float recover)
		{
			Raised = raised;
			Sweep = sweep;
			Settle = settle;
			Lift = lift;
			Withdraw = withdraw;
			Raise = raise;
			Strike = strike;
			Recover = recover;
		}

		public readonly float Raised, Sweep, Settle;
		public readonly Vector2 Lift, Withdraw;
		public readonly float Raise, Strike, Recover;
	}

	// The three forms' swing: over the top from behind the shoulder, the swing as it was built.
	private static readonly Cut AxeCut = Overhead(0.30f, 0.09f, 0.20f);
	private static readonly Cut HammerCut = Overhead(0.26f, 0.08f, 0.22f);
	private static readonly Cut SwordCut = Overhead(0.11f, 0.06f, 0.08f);
	// Thousand Cuts' cuts after the first: a short lift and back down onto the quarry.
	private static readonly Cut FlurryCut = new(FlurryRaised, CanonImpact - FlurryRaised, CanonIdle - CanonImpact,
		new Vector2(0f, -0.12f), new Vector2(0f, -0.35f), 0.05f, 0.05f, 0.08f);
	// Twin Blades' stab: cocked back along its line, a short thrust in with a little turn, and out again.
	private static readonly Cut DaggerCut = new(150f, 10f, 15f, new Vector2(0.55f, -0.10f), new Vector2(0.35f, -0.15f),
		0.07f, 0.04f, 0.09f);

	private static Cut Overhead(float raise, float strike, float recover) =>
		new(CanonRaised, CanonImpact - CanonRaised, CanonIdle - CanonImpact, new Vector2(0.15f, -0.50f), new Vector2(0f, -0.35f),
			raise, strike, recover);

	private enum Phase : byte { Heel, Hunt, Raise, Strike, Recover, Leave, Deflect }

	// A strike's slash: the head's path through it, from where it came round to where it landed, how far
	// through it the head is (eased, 0 to 1; -1 before the first) and how long since it landed. Drawn from
	// these - the head's own path, sampled - so it is a smooth arc at any frame rate, where a trail of the
	// head's per-frame positions was a few long straight chords.
	private struct SlashArc
	{
		public Vector2 FromPivot, ToPivot;
		public float FromCanon, ToCanon, Side, Progress, Age;
	}

	private sealed class Weapon
	{
		public Vector2 Pivot;
		public float Angle;            // radians, world, from the grip toward the head
		public Phase Phase;
		public float T;
		public Enemy Target;
		public Vector2 AimAt;          // where its quarry was, for a raise whose quarry falls
		public bool HasSwing;
		public float SwingDelay;
		public float Side = 1f;        // +1: to the right of its quarry
		public float Station;          // how many weapons before it share its quarry's ground (Spread out)
		public float Retarget;
		public float Bob;              // the hover's phase
		public float BobRate = 1f;     // and its own rate, so a flight of weapons drifts apart
		public Vector2 FromPivot;
		public float FromCanon;
		public float RaisedCanon;      // this swing's cut, unwrapped so its lerps turn the way it means
		public float ImpactCanon;
		public float Duration;
		public bool Mini;              // a flurry's later cut
		public int FlurryLeft;
		public bool Dagger;            // Twin Blades' dagger, not one of the swords
		public float Ground;           // the y its shadow lies on
		public float Alpha = 1f;
		public float Spin;             // Whirling Axes: its turn between blows
		public bool Leaving;
		// Deflection: the bolt it has broken off for, the way it swats it, and its rest after.
		public EnemyProjectile Bolt;
		public Vector2 Aside;
		public float SwatSign;
		public float FromAngle;
		public float DeflectRest;
		// This strike's slash, and the last one's finishing its fade under it.
		public SlashArc Slash = new() { Side = 1f, Progress = -1f };
		public SlashArc Fading = new() { Side = 1f, Progress = -1f, Age = SlashLife };
		// The last two frames of a strike, for its afterimages: their headings, and (the sword) their grips.
		public float Ghost1 = float.NaN, Ghost2 = float.NaN;
		public Vector2 GhostPivot1, GhostPivot2;
	}

	private const float SlashLife = 0.16f;
	private const int SlashSamples = 12;

	private struct Mote
	{
		public Vector2 At, Vel;
		public float Age, Life, Size;
		public Color Colour;
		public bool Live;
	}

	private struct Slam
	{
		public Vector2 At;
		public float Radius, Age, Life;
		public uint Seed;
		public bool Live, Echo;
	}

	private struct Zone
	{
		public Vector2 At;
		public float Radius, Left, Life, Tick;
		public uint Seed;
		public bool Live;
	}

	private struct Echo
	{
		public Vector2 At;
		public float Radius, Delay;
		public int Damage;
		public bool Live;
	}

	private const int MaxMotes = 160;
	private const int MaxSlams = 16;
	private const int MaxZones = 12;
	private const int MaxEchoes = 12;

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; private set; }

	private readonly List<Weapon> weapons = new(MaxWeapons);
	private Weapon dagger;
	private readonly List<Enemy> nearBuffer = new(96);
	private readonly List<Enemy> hitBuffer = new(64);
	private readonly Mote[] motes = new Mote[MaxMotes];
	private readonly Slam[] slams = new Slam[MaxSlams];
	private readonly Zone[] zones = new Zone[MaxZones];
	private readonly Echo[] echoes = new Echo[MaxEchoes];
	private int nextMote, nextSlam, nextZone, nextEcho;
	private readonly RandomNumberGenerator rng = new();
	private Node2D air;
	private Node2D ground;
	private Node2D ghosts;
	private Node2D quake;
	private ShaderMaterial quakeMaterial;
	private const string AirShaderPath = "res://scenes/shaders/spirit_weapon_air.gdshader";
	private const string QuakeShaderPath = "res://scenes/shaders/spirit_quake.gdshader";
	private static readonly StringName ParamTint = "tint";
	// A slam's and a zone's radius ride in a colour channel as radius / QuakeRadiusScale (spirit_quake).
	private const float QuakeRadiusScale = 512f;
	private Texture2D baseSheet;
	private Texture2D colossalSheet;
	private Texture2D sheet;
	private Texture2D daggerSheet;
	private int sheetCell = BaseCell;
	private int sheetRows = 1;

	// The weapons as of the last cooldown.
	private Form form = Form.Axe;
	private bool armed;
	private bool colossal;
	private string lastLook = string.Empty;
	private int damage = 20;
	private float critBonus;
	private float huntRange = 200f;
	private float areaMul = 1f;
	private float scale = 1f;
	private float tempo = 1f;
	private float pace = 1f;
	private float slowPower = HammerSlow;
	private float slowSeconds = HammerSlowSeconds;
	private float freezeSeconds;
	private float knockback = AxeKnockback;
	private float zoneSeconds;
	private bool headsman, aftershock, hallowed, whirling, deflect, twin, siphon, flurry, alongBlade;
	private Color tint = Colors.White;
	private ElementColors.Ramp arcane = ElementColors.GetRamp(Element.Arcane);
	private ElementColors.Ramp light = ElementColors.GetRamp(Element.Light);

	// Soul Siphon's window.
	private float siphonWindow;
	private int siphoned;

	// The ground shaking: the player's camera, nudged for a moment after a slam.
	private Camera2D camera;
	private float shakeLeft;
	private float shakeAmplitude;
	private const float ShakeSeconds = 0.18f;

	// --- what the balance harness reads (scripts/_BalanceHarness.cs). Static: one run per process there. --
	public static int StatSwings { get; private set; }
	public static int StatHits { get; private set; }
	public static int StatBoltsParried { get; private set; }
	public static int StatBoltsCrushed { get; private set; }
	public static int StatExecutions { get; private set; }
	public static int StatEchoes { get; private set; }
	public static int StatBoltsDeflected { get; private set; }
	public static int StatDaggerStabs { get; private set; }
	public static int StatReaims { get; private set; }
	public static float StatPeakPace { get; private set; } = 1f;

	/// <summary>One line for the balance report: is it swinging, and how much does each swing reach.</summary>
	public static string StatsLine() =>
		$"spirit weapon: {StatSwings} swings, {StatHits} enemies struck ({(StatSwings > 0 ? StatHits / (float)StatSwings : 0f):0.0} a swing); "
		+ $"{StatBoltsParried} bolts parried ({StatBoltsDeflected} deflected), {StatBoltsCrushed} crushed; {StatExecutions} executions, {StatEchoes} aftershocks; "
		+ $"{StatDaggerStabs} dagger stabs; {StatReaims} raises re-aimed; swing pace up to x{StatPeakPace:0.00}";

	/// <summary>What RegressionChecks cannot see from outside: no part of any swing takes no time. Empty when sound.</summary>
	public static List<string> CutProblems()
	{
		var problems = new List<string>();
		void Check(string name, in Cut c)
		{
			if (c.Raise <= 0f || c.Strike <= 0f || c.Recover <= 0f)
				problems.Add($"{name} has a part that takes no time (raise {c.Raise}, strike {c.Strike}, recover {c.Recover}); the swing's lerps divide by it.");
		}
		Check("the axe's swing", AxeCut);
		Check("the hammer's swing", HammerCut);
		Check("the sword's swing", SwordCut);
		Check("a flurry's cut", FlurryCut);
		Check("the dagger's stab", DaggerCut);
		return problems;
	}

	public override void _Ready()
	{
		rng.Randomize();
		GlobalPosition = Vector2.Zero;
		// The weapons are in the air, over the bodies (as Familiar's effects are) and under the HUD; their
		// shadows, the cracks and the hallowed ground are on the floor, under the bodies.
		//
		// PIXEL ART (2026-10-29, "Update all spells to the same quality we expect now"). Four canvases, each
		// drawing opaque cells on the world's 2 px grid. The quake and the holy ground are a shader of their
		// own (spirit_quake.gdshader), one quad a slam; the shadows, the afterimages, the weapons, the slash
		// and the motes keep their sheet cells and rects but go through spirit_weapon_air.gdshader, which turns
		// every alpha they are drawn with into dithered coverage - a weapon dissolves in, an afterimage is a
		// stipple of its shape, the slash breaks up as it cools - where they used to be translucent.
		var dither = new ShaderMaterial { Shader = GD.Load<Shader>(AirShaderPath) };
		var ghostDither = new ShaderMaterial { Shader = dither.Shader };
		ghostDither.SetShaderParameter("silhouette", true);
		quakeMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(QuakeShaderPath) };
		quake = new Node2D { Name = "SpiritWeaponQuake", ZAsRelative = false, ZIndex = -1, Material = quakeMaterial };
		ground = new Node2D { Name = "SpiritWeaponGround", ZAsRelative = false, ZIndex = -1, Material = dither };
		ghosts = new Node2D { Name = "SpiritWeaponGhosts", ZAsRelative = false, ZIndex = 6, Material = ghostDither };
		air = new Node2D { Name = "SpiritWeaponAir", ZAsRelative = false, ZIndex = 6, Material = dither };
		// In this order, so the shadows lie over the cracks and the weapon over its afterimages.
		AddChild(quake);
		AddChild(ground);
		AddChild(ghosts);
		AddChild(air);
		quake.Draw += DrawQuake;
		ground.Draw += DrawGround;
		ghosts.Draw += DrawGhosts;
		air.Draw += DrawAir;
	}

	public override void _ExitTree()
	{
		if (camera != null && IsInstanceValid(camera))
			camera.Offset = Vector2.Zero;
		// A bolt a sword had broken off for is free again for the Familiar's hounds.
		foreach (Weapon w in weapons)
			ReleaseBolt(w);
	}

	/// <summary>Which form a held copy of the spell is.</summary>
	public static Form FormOf(SpellData spell) => spell?.SelectedVariantId switch
	{
		HammerId => Form.Hammer,
		SwordId => Form.Sword,
		_ => Form.Axe,
	};

	/// <summary>
	/// One cooldown: the weapons' numbers, how many there are, and a swing for each, staggered across the
	/// interval so several weapons fall in turn. Called by the fire loop every cooldown
	/// (Player.FireSpiritualWeapon), which is what keeps it current through level-ups and milestones.
	/// </summary>
	public void Arm(SpellData spell, int count, float playerArea, float damageMultiplier, Color visualTint, float interval)
	{
		if (spell == null)
			return;
		SpellData = spell;
		int level = Math.Max(1, spell.CurrentLevel);
		form = FormOf(spell);
		damage = Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(level) * damageMultiplier));
		critBonus = MathF.Max(0f, spell.GetEffectValueAtLevel(SpellEffect.CritChance, level));
		huntRange = MathF.Max(80f, spell.GetRangeAtLevel(level));
		// Every area term once: the player's, every milestone's, and the level table's Area lines (a share).
		areaMul = MathF.Max(0.25f, playerArea * spell.GetAreaMultiplierAtLevel(level)
			* (1f + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, level)));
		// The Ascension's colossal form is drawn twice the size and reaches twice as far: Scale is both.
		scale = MathF.Max(0.25f, spell.GetScaleMultiplier());
		tempo = MathF.Max(0.25f, spell.GetSpeedMultiplier());
		slowPower = MathF.Min(MaxSlow, HammerSlow + spell.GetEffectValueAtLevel(SpellEffect.SlowPower, level)
			+ spell.GetSlowMagnitudeAtLevel(level));
		slowSeconds = HammerSlowSeconds + spell.GetEffectValueAtLevel(SpellEffect.SlowDuration, level);
		freezeSeconds = spell.GetFreezeSecondsAtLevel(level);
		knockback = AxeKnockback + spell.GetKnockbackSpeedAtLevel(level);
		zoneSeconds = spell.GetEffectValueAtLevel(SpellEffect.ZoneDuration, level);
		siphon = spell.GetEffectValueAtLevel(SpellEffect.Lifesteal, level) > 0f;
		headsman = spell.HasEvolution(HeadsmanId);
		aftershock = spell.HasEvolution(AftershockId);
		hallowed = spell.HasEvolution(HallowedGroundId) && zoneSeconds > 0f;
		whirling = spell.HasEvolution(WhirlingAxesId);
		deflect = form == Form.Sword && spell.HasEvolution(RiposteId);
		twin = form == Form.Sword && spell.HasEvolution(TwinBladesId);
		flurry = spell.HasEvolution(ThousandCutsId);
		alongBlade = spell.HasEvolution(GreatswordId);
		colossal = spell.HasEvolution(GreatswordId) || spell.HasEvolution(GreataxeId) || spell.HasEvolution(TitansMaulId);
		pace = form == Form.Sword ? PaceFor(interval) : 1f;
		StatPeakPace = MathF.Max(StatPeakPace, pace);
		if (tint != visualTint || armed == false)
			quakeMaterial?.SetShaderParameter(ParamTint, visualTint);
		tint = visualTint;

		string look = $"{form}:{colossal}";
		bool changed = look != lastLook;
		if (changed)
			LoadSheets();
		// Summoned, or become something new: the one loud moment the weapon has (the Familiar's rule).
		if (changed)
			SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellSummon, PlayerRef?.GlobalPosition ?? GlobalPosition, 0.04f);
		lastLook = look;

		count = Math.Clamp(count, 1, MaxWeapons);
		int live = 0;
		foreach (Weapon w in weapons)
			if (!w.Leaving)
				live++;
		while (live < count)
		{
			AddWeapon();
			live++;
		}
		for (int i = weapons.Count - 1; i >= 0 && live > count; i--)
		{
			if (weapons[i].Leaving)
				continue;
			ReleaseBolt(weapons[i]);
			weapons[i].Leaving = true;
			weapons[i].Phase = Phase.Leave;
			weapons[i].T = 0f;
			live--;
		}

		// Twin Blades: one dagger, however many swords.
		if (twin && dagger == null)
			AddDagger();
		else if (!twin && dagger != null && !dagger.Leaving)
		{
			dagger.Leaving = true;
			dagger.Phase = Phase.Leave;
			dagger.T = 0f;
		}

		// A swing for every weapon that has none, in turn across the interval.
		int k = 0;
		float step = live > 1 ? MathF.Max(0f, interval) / live : 0f;
		foreach (Weapon w in weapons)
		{
			if (w.Leaving)
				continue;
			if (!w.HasSwing)
			{
				w.HasSwing = true;
				w.SwingDelay = step * k;
			}
			k++;
		}
		armed = true;
	}

	// How much faster than its own the sword's swing must go to finish inside SwingBudget of the interval:
	// the wind-ups and the recovery (A) shorten by the pace and the cuts (B) by its square root, so
	// A / pace + B / sqrt(pace) = budget, a quadratic in 1 / sqrt(pace). One at any ordinary cooldown.
	private float PaceFor(float interval)
	{
		float a = SwordCut.Raise * (colossal ? 1.15f : 1f) + SwordCut.Recover;
		float b = SwordCut.Strike;
		if (flurry)
		{
			a += (FlurryCuts - 1) * FlurryCut.Raise;
			b += (FlurryCuts - 1) * FlurryCut.Strike;
		}
		a /= tempo;
		b /= tempo;
		float budget = SwingBudget * MathF.Max(0.05f, interval);
		if (a + b <= budget)
			return 1f;
		float x = (-b + MathF.Sqrt(b * b + 4f * a * budget)) / (2f * a);
		return Mathf.Clamp(1f / (x * x), 1f, MaxPace);
	}

	private void LoadSheets()
	{
		string name = form switch { Form.Hammer => "hammer", Form.Sword => "sword", _ => "axe" };
		string big = form switch { Form.Hammer => "maul", Form.Sword => "greatsword", _ => "greataxe" };
		baseSheet = GD.Load<Texture2D>($"res://assets/bonelight/effects/spirit-{name}.png");
		colossalSheet = colossal ? GD.Load<Texture2D>($"res://assets/bonelight/effects/spirit-{big}.png") : null;
		sheet = colossal && colossalSheet != null ? colossalSheet : baseSheet;
		sheetCell = colossal && colossalSheet != null ? ColossalCell : BaseCell;
		sheetRows = sheet != null ? Math.Max(1, sheet.GetHeight() / sheetCell) : 1;
		if (sheet == null)
			GD.PushError($"SpiritualWeapon: no drawing for the {form}{(colossal ? " (colossal)" : "")}; run tools/art/spirit_weapons.py and import.");
	}

	private float GripToHead => (colossal ? ColossalGripToHead : BaseGripToHead);
	private float GripToCentre => (colossal ? ColossalGripToCentre : BaseGripToCentre);

	// The dagger is its own drawing on its own cell, and never colossal.
	private float HeadOf(Weapon w) => w.Dagger ? DaggerGripToHead : GripToHead;
	private float CentreOf(Weapon w) => w.Dagger ? DaggerGripToCentre : GripToCentre;

	private void AddWeapon()
	{
		var w = new Weapon
		{
			Bob = weapons.Count * GoldenAngle + rng.Randf() * 0.5f,
			BobRate = 0.85f + 0.3f * rng.Randf(),
			Phase = Phase.Heel,
			Alpha = 0f,
		};
		Vector2 at = PlayerRef != null ? HeelSpot(weapons.Count, Math.Max(1, weapons.Count + 1)) : Vector2.Zero;
		w.Pivot = at;
		w.Angle = Mathf.DegToRad(-90f);
		w.Ground = PlayerRef?.FootPosition.Y ?? at.Y + 40f;
		weapons.Add(w);
		Puff(at + Dir(w.Angle) * CentreOf(w), inward: true);
	}

	private void AddDagger()
	{
		if (daggerSheet == null)
		{
			daggerSheet = GD.Load<Texture2D>(DaggerSheetPath);
			if (daggerSheet == null)
				GD.PushError($"SpiritualWeapon: no drawing for Twin Blades' dagger at {DaggerSheetPath}; run tools/art/spirit_weapons.py and import.");
		}
		dagger = new Weapon
		{
			Dagger = true,
			Bob = Mathf.Pi + rng.Randf() * 0.5f,
			BobRate = 0.85f + 0.3f * rng.Randf(),
			Phase = Phase.Heel,
			Alpha = 0f,
		};
		Vector2 at = PlayerRef != null ? DaggerHeelSpot() : Vector2.Zero;
		dagger.Pivot = at;
		dagger.Angle = Mathf.DegToRad(-90f);
		dagger.Ground = PlayerRef?.FootPosition.Y ?? at.Y + 40f;
		Puff(at + Dir(dagger.Angle) * CentreOf(dagger), inward: true);
	}

	// The first sword that is not leaving: the one Twin Blades' dagger fights beside.
	private Weapon MainHand()
	{
		foreach (Weapon w in weapons)
			if (!w.Leaving)
				return w;
		return null;
	}

	// --- the frame ---------------------------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		float dt = (float)delta;
		if (armed)
		{
			int n = 0;
			foreach (Weapon w in weapons)
				if (!w.Leaving)
					n++;
			for (int i = weapons.Count - 1; i >= 0; i--)
			{
				Weapon w = weapons[i];
				Tick(w, i, n, dt);
				if (w.Leaving && w.Phase == Phase.Leave && w.T >= 0.2f)
				{
					Puff(w.Pivot + Dir(w.Angle) * CentreOf(w), inward: false);
					weapons.RemoveAt(i);
				}
			}
			if (dagger != null)
			{
				TickDagger(dagger, dt);
				if (dagger.Leaving && dagger.Phase == Phase.Leave && dagger.T >= 0.2f)
				{
					Puff(dagger.Pivot + Dir(dagger.Angle) * CentreOf(dagger), inward: false);
					dagger = null;
				}
			}
			if (form == Form.Sword)
			{
				if (deflect)
					Intercept(PlayerRef.GlobalPosition);
				ParryBolts();
			}
		}

		TickEchoes(dt);
		TickZones(dt);
		TickFx(dt);
		TickShake(dt);
		siphonWindow -= dt;
		if (siphonWindow <= 0f)
		{
			siphonWindow = 1f;
			siphoned = 0;
		}
		air?.QueueRedraw();
		ghosts?.QueueRedraw();
		ground?.QueueRedraw();
		quake?.QueueRedraw();
	}

	// Every weapon's clocks, the dagger's included.
	private static void Advance(Weapon w, float dt)
	{
		w.T += dt;
		w.Bob = (w.Bob + dt * HoverRate * w.BobRate) % (2f * Mathf.Tau);
		w.SwingDelay = MathF.Max(0f, w.SwingDelay - dt);
		w.DeflectRest = MathF.Max(0f, w.DeflectRest - dt);
		w.Alpha = w.Phase == Phase.Leave ? MathF.Max(0f, 1f - w.T / 0.2f) : MathF.Min(1f, w.Alpha + dt / 0.18f);
		if (w.Slash.Progress >= 1f)
			w.Slash.Age += dt;
		w.Fading.Age += dt;
	}

	// The parts of a swing every weapon shares. True when the weapon is in one (or leaving), and has been
	// moved for this frame.
	private bool TickSwing(Weapon w)
	{
		switch (w.Phase)
		{
			case Phase.Leave:
				return true;
			case Phase.Raise:
				TickRaise(w);
				return true;
			case Phase.Strike:
				TickStrike(w);
				return true;
			case Phase.Recover:
				TickRecover(w);
				return true;
			case Phase.Deflect:
				TickDeflect(w);
				return true;
		}
		return false;
	}

	private void Tick(Weapon w, int index, int count, float dt)
	{
		Advance(w, dt);
		if (TickSwing(w))
			return;
		Vector2 player = PlayerRef.GlobalPosition;

		// Heel or hunt: pick (or keep) a quarry, then hang beside it or at the wizard's shoulder.
		w.Retarget -= dt;
		if (w.Retarget <= 0f || !Alive(w.Target) || w.Target.GlobalPosition.DistanceTo(player) > huntRange * 1.3f)
		{
			w.Retarget = 0.2f;
			Enemy was = w.Target;
			w.Target = Acquire(w, player);
			if (w.Target != null && w.Target != was)
				w.Side = w.Pivot.X >= w.Target.GlobalPosition.X ? 1f : -1f;
		}

		if (w.Target == null)
		{
			w.Phase = Phase.Heel;
			w.Station = 0f;
			Approach(w, HeelSpot(index, count) + Hover(w), FlySpeed() * 0.9f, dt);
			float lean = (index - (count - 1) * 0.5f) * 0.22f;
			Hang(w, Mathf.DegToRad(-90f) + lean + HoverTilt(w), dt);
			w.Ground = Mathf.Lerp(w.Ground, PlayerRef.FootPosition.Y + 6f, MathF.Min(1f, dt * 6f));
			return;
		}

		w.Phase = Phase.Hunt;
		Vector2 t = w.Target.GlobalPosition;
		float reach = HeadOf(w);
		// Keep the side it came in on until it is well over the other side, or a weapon level with its
		// quarry would swap sides every frame - unless it shares its quarry's ground, when its station says.
		float dx = w.Pivot.X - t.X;
		if (!TakeStation(w) && dx * w.Side < -0.5f * reach)
			w.Side = -w.Side;
		Vector2 hold = ImpactPivot(w, t, CanonImpact) + new Vector2(w.Side * 0.10f * reach, -0.30f * reach) + StationOffset(w) + Hover(w);
		Approach(w, hold, FlySpeed(), dt);
		Hang(w, Mirror(CanonIdle, w.Side) + HoverTilt(w), dt);
		w.Ground = Mathf.Lerp(w.Ground, t.Y + 26f, MathF.Min(1f, dt * 8f));

		if (w.HasSwing && w.SwingDelay <= 0f && w.Pivot.DistanceTo(hold) < 0.45f * reach)
			StartRaise(w, mini: false);
	}

	// Twin Blades' dagger: at his other side while the main-hand sword has no quarry; else on the far flank
	// of that sword's quarry, cocked, and in the gap after each of the sword's cuts (CueDagger) it stabs.
	private void TickDagger(Weapon d, float dt)
	{
		Advance(d, dt);
		if (TickSwing(d))
			return;
		Weapon main = MainHand();
		Enemy quarry = main != null && Alive(main.Target) ? main.Target : null;
		if (quarry == null)
		{
			d.Phase = Phase.Heel;
			d.Target = null;
			d.HasSwing = false;
			Approach(d, DaggerHeelSpot() + Hover(d), DaggerFlySpeed * 0.9f, dt);
			Hang(d, Mathf.DegToRad(-90f) + HoverTilt(d), dt);
			d.Ground = Mathf.Lerp(d.Ground, PlayerRef.FootPosition.Y + 6f, MathF.Min(1f, dt * 6f));
			return;
		}

		d.Phase = Phase.Hunt;
		d.Target = quarry;
		d.Side = -main.Side;
		Vector2 t = quarry.GlobalPosition;
		float reach = HeadOf(d);
		Vector2 hold = ImpactPivot(d, t, DaggerCut.Raised + DaggerCut.Sweep) + Lift(d, DaggerCut)
			+ new Vector2(d.Side * 0.10f * reach, -0.15f * reach) + Hover(d);
		Approach(d, hold, DaggerFlySpeed, dt);
		Hang(d, Mirror(DaggerIdle, d.Side) + HoverTilt(d), dt);
		d.Ground = Mathf.Lerp(d.Ground, t.Y + 26f, MathF.Min(1f, dt * 8f));

		if (d.HasSwing && d.SwingDelay <= 0f && d.Pivot.DistanceTo(hold) < 0.6f * reach)
			StartRaise(d, mini: false);
	}

	// A cut of the main-hand sword has landed: the dagger, if it is free, stabs in the gap after it.
	private void CueDagger(Weapon cutter)
	{
		if (dagger == null || dagger.Leaving || dagger.HasSwing || cutter != MainHand())
			return;
		if (dagger.Phase != Phase.Hunt && dagger.Phase != Phase.Heel)
			return;
		dagger.HasSwing = true;
		dagger.SwingDelay = DaggerGap;
	}

	// Between blows a weapon hangs head-up; Whirling Axes' spin end over end instead.
	private void Hang(Weapon w, float rest, float dt)
	{
		if (whirling && !w.Dagger)
		{
			w.Spin = (w.Spin + dt * 9f) % Mathf.Tau;
			w.Angle = rest + w.Spin;
			return;
		}
		w.Spin = 0f;
		w.Angle = TurnToward(w.Angle, rest, dt * 9f);
	}

	private Cut CutOf(Weapon w)
	{
		if (w.Dagger)
			return DaggerCut;
		if (w.Mini)
			return FlurryCut;
		return form switch { Form.Sword => SwordCut, Form.Hammer => HammerCut, _ => AxeCut };
	}

	// The sword and its dagger keep the pace; the axe and the hammer always swing at their own.
	private float PaceOf(Weapon w) => form == Form.Sword ? pace : 1f;

	private void StartRaise(Weapon w, bool mini)
	{
		w.Phase = Phase.Raise;
		w.T = 0f;
		w.Mini = mini;
		w.Spin = 0f;
		if (w.Dagger)
		{
			w.HasSwing = false;
			StatDaggerStabs++;
		}
		else if (!mini)
		{
			w.HasSwing = false;
			w.FlurryLeft = flurry ? FlurryCuts - 1 : 0;
			StatSwings++;
		}
		Cut cut = CutOf(w);
		AimFromHere(w, cut);
		float raise = mini || w.Dagger ? cut.Raise : cut.Raise * (colossal ? 1.15f : 1f);
		w.Duration = raise / PaceOf(w) / tempo;
		if (Alive(w.Target))
			w.AimAt = w.Target.GlobalPosition;
	}

	// The wind-up starts from the pose the blade is in. The sword winds up the short way round from
	// wherever it is - partway up a recovery, back from a deflect - rather than spinning the long way; the
	// axe and the hammer wind up the one way they always have, a spinning axe included.
	private void AimFromHere(Weapon w, in Cut cut)
	{
		w.FromPivot = w.Pivot;
		w.FromCanon = ToCanon(w.Angle, w.Side);
		w.RaisedCanon = form == Form.Sword ? NearestTurn(cut.Raised, w.FromCanon) : cut.Raised;
		w.ImpactCanon = w.RaisedCanon + cut.Sweep;
	}

	private void TickRaise(Weapon w)
	{
		Cut cut = CutOf(w);
		if (!Alive(w.Target))
		{
			// Its quarry fell while it wound up. The sword re-aims at what stands nearest where it was going,
			// from where the blade is, in what is left of the wind-up; anything else lets the swing go.
			Enemy next = form == Form.Sword && !w.Dagger ? NearestTo(w.AimAt, FlurryReach) : null;
			if (next == null)
			{
				EndSwing(w);
				return;
			}
			w.Target = next;
			AimFromHere(w, cut);
			w.Duration = MathF.Max(0.03f, w.Duration - w.T);
			w.T = 0f;
			StatReaims++;
		}
		float t = Mathf.Clamp(w.T / w.Duration, 0f, 1f);
		float e = t * t * (3f - 2f * t);
		Vector2 target = w.Target.GlobalPosition;
		w.AimAt = target;
		Vector2 raised = ImpactPivot(w, target, w.ImpactCanon) + Lift(w, cut) + StationOffset(w);
		w.Pivot = w.FromPivot.Lerp(raised, e);
		w.Angle = Mirror(Mathf.Lerp(w.FromCanon, w.RaisedCanon, e), w.Side);
		w.Ground = Mathf.Lerp(w.Ground, target.Y + 26f, 0.3f);
		if (t >= 1f)
		{
			w.Phase = Phase.Strike;
			w.T -= w.Duration;
			w.FromPivot = w.Pivot;
			w.FromCanon = w.RaisedCanon;
			w.Duration = cut.Strike / MathF.Sqrt(PaceOf(w)) / tempo;
			w.Ghost1 = w.Ghost2 = float.NaN;
			// The last strike's slash, if it is still breaking up, finishes on its own under this one.
			if (w.Slash.Progress > 0f && w.Slash.Age < SlashLife)
			{
				w.Fading = w.Slash;
				w.Fading.Progress = 1f;
			}
			// The whoosh as it comes over: one per swing, throttled so a flight of swords is one sound. The
			// dagger's stab and a flurry's later cuts make none of their own.
			if (!w.Mini && !w.Dagger)
				SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellOrbit, w.Pivot, 0.12f, SfxCatalog.SpellOrbit, 70);
		}
	}

	// Where a cut winds the grip back to, from the grip it lands at: x away from the quarry.
	private Vector2 Lift(Weapon w, in Cut cut) => new Vector2(w.Side * cut.Lift.X, cut.Lift.Y) * HeadOf(w);

	private void TickStrike(Weapon w)
	{
		Cut cut = CutOf(w);
		Vector2 target = Alive(w.Target) ? w.Target.GlobalPosition : HeadAt(w);
		float t = Mathf.Clamp(w.T / w.Duration, 0f, 1f);
		// A blow that accelerates: slow over the top, fast at the end.
		float e = t * t;
		w.Ghost2 = w.Ghost1;
		w.Ghost1 = w.Angle;
		w.GhostPivot2 = w.GhostPivot1;
		w.GhostPivot1 = w.Pivot;
		Vector2 impact = ImpactPivot(w, target, w.ImpactCanon);
		w.Pivot = w.FromPivot.Lerp(impact, e);
		w.Angle = Mirror(Mathf.Lerp(w.FromCanon, w.ImpactCanon, e), w.Side);
		w.Slash.FromPivot = w.FromPivot;
		w.Slash.ToPivot = impact;
		w.Slash.FromCanon = w.FromCanon;
		w.Slash.ToCanon = w.ImpactCanon;
		w.Slash.Side = w.Side;
		w.Slash.Progress = e;
		w.Slash.Age = 0f;
		if (t >= 1f)
		{
			if (w.Dagger)
				Stab(w, HeadAt(w));
			else
			{
				Blow(w, HeadAt(w), flurry ? FlurryShare : 1f);
				if (form == Form.Sword)
					CueDagger(w);
			}
			w.T -= w.Duration;
			if (w.FlurryLeft > 0 && !Alive(w.Target))
				w.Target = NearestTo(HeadAt(w), FlurryReach);
			if (w.FlurryLeft > 0 && Alive(w.Target))
			{
				w.FlurryLeft--;
				StartRaise(w, mini: true);
				return;
			}
			w.Phase = Phase.Recover;
			w.FromPivot = w.Pivot;
			w.Duration = cut.Recover / PaceOf(w) / tempo;
		}
	}

	private void TickRecover(Weapon w)
	{
		Cut cut = CutOf(w);
		float t = Mathf.Clamp(w.T / w.Duration, 0f, 1f);
		float e = 1f - (1f - t) * (1f - t);
		Vector2 settle = w.FromPivot + new Vector2(w.Side * cut.Withdraw.X, cut.Withdraw.Y) * HeadOf(w);
		// The bounce shrinks as the swing quickens: a full hop crammed into a hurried recovery is a twitch.
		float bounce = (w.Dagger ? 3f : 6f) / PaceOf(w);
		w.Pivot = w.FromPivot.Lerp(settle, e) + new Vector2(0f, -MathF.Sin(t * MathF.PI) * bounce);
		// Back up the way it came down, to hang head-up again.
		w.Angle = Mirror(Mathf.Lerp(w.ImpactCanon, w.ImpactCanon + cut.Settle, e), w.Side);
		// A sword whose next swing is already due goes into it from here, still rising, rather than coming
		// to a dead stop at the top and jerking into the wind-up.
		if (form == Form.Sword && !w.Dagger && t >= RecoverCutIn && w.HasSwing && w.SwingDelay <= 0f && Alive(w.Target))
		{
			w.Mini = false;
			StartRaise(w, mini: false);
			return;
		}
		if (t >= 1f)
			EndSwing(w);
	}

	private void EndSwing(Weapon w)
	{
		w.Phase = w.Target != null ? Phase.Hunt : Phase.Heel;
		w.T = 0f;
		w.Mini = false;
		w.FlurryLeft = 0;
		w.Retarget = 0f;
	}

	// --- where things are ----------------------------------------------------------------------------------

	private static Vector2 Dir(float angle) => new Vector2(MathF.Cos(angle), MathF.Sin(angle));

	private static float Mirror(float canonDegrees, float side) =>
		Mathf.DegToRad(side >= 0f ? canonDegrees : 180f - canonDegrees);

	// A world angle as the canonical one (a weapon to the right), in [90, 450) so the swing's lerps never
	// cross a wrap: idle 285, raised 345, impact 145 + 360 = 505 is never needed because the strike goes
	// down from 345 to 145.
	private static float ToCanon(float angle, float side)
	{
		float deg = Mathf.RadToDeg(angle);
		float c = side >= 0f ? deg : 180f - deg;
		c %= 360f;
		if (c < 0f) c += 360f;
		if (c < 90f) c += 360f;
		return c;
	}

	// The turn of `degrees` (plus or minus whole turns) nearest `from`: where a lerp from `from` should end
	// to go the short way round.
	private static float NearestTurn(float degrees, float from) => degrees + 360f * MathF.Round((from - degrees) / 360f);

	// The grip that puts this weapon's head on `target` at the canonical heading `canon`.
	private Vector2 ImpactPivot(Weapon w, Vector2 target, float canon) => target - Dir(Mirror(canon, w.Side)) * HeadOf(w);

	private Vector2 HeadAt(Weapon w) => w.Pivot + Dir(w.Angle) * HeadOf(w);

	// Where the head was at progress p (eased, 0 to 1) through a strike.
	private Vector2 SlashHead(Weapon w, in SlashArc s, float p) =>
		s.FromPivot.Lerp(s.ToPivot, p) + Dir(Mirror(Mathf.Lerp(s.FromCanon, s.ToCanon, p), s.Side)) * HeadOf(w);

	// The weapons at rest hang in a shallow arc beside the wizard, behind the way he faces.
	private Vector2 HeelSpot(int index, int count)
	{
		Vector2 player = PlayerRef.GlobalPosition;
		float back = -(PlayerRef.FacingSide);
		float spread = (index - (count - 1) * 0.5f);
		float fan = count > 1 ? MathF.Min(HeelFan, HeelFanSpan / (count - 1)) : 0f;
		float a = Mathf.DegToRad(-90f + back * HeelBack + spread * fan);
		float r = HeelReach + HeelFanReach * MathF.Abs(spread);
		// The grip hangs low and the head up, so the drawing stands beside him rather than over his head.
		return player + Dir(a) * r + new Vector2(0f, HeelDrop);
	}

	private Vector2 DaggerHeelSpot()
	{
		float a = Mathf.DegToRad(-90f + PlayerRef.FacingSide * DaggerHeelAngle);
		return PlayerRef.GlobalPosition + Dir(a) * DaggerHeelReach + new Vector2(0f, HeelDrop);
	}

	// The hover: the rise and the sway, in whole art pixels so it steps on the grid (a multiple of 2 added
	// to a point moves its snapped drawing by exactly that much); the tilt rocks with the sway's drift.
	private static Vector2 Hover(Weapon w) =>
		Snap(new Vector2(MathF.Sin(w.Bob * 0.5f) * HoverSway, MathF.Sin(w.Bob) * HoverRise));

	private static float HoverTilt(Weapon w) => Mathf.DegToRad(HoverTiltDegrees) * MathF.Cos(w.Bob * 0.5f);

	// Spread out: the weapons before this one in the list whose quarry is this one's, or stands within
	// TargetSpacing of it, take the stations first. The first keeps its own side; the rest alternate sides
	// from it, each pair further out and higher. True when it shares, and its side was set by its station.
	private bool TakeStation(Weapon w)
	{
		int before = 0;
		Weapon first = null;
		float r2 = TargetSpacing * TargetSpacing;
		Vector2 at = w.Target.GlobalPosition;
		foreach (Weapon o in weapons)
		{
			if (o == w)
				break;
			if (o.Leaving || !Alive(o.Target))
				continue;
			if (o.Target != w.Target && o.Target.GlobalPosition.DistanceSquaredTo(at) > r2)
				continue;
			first ??= o;
			before++;
		}
		w.Station = before;
		if (first == null)
			return false;
		w.Side = before % 2 == 0 ? first.Side : -first.Side;
		return true;
	}

	// A station's place: each pair out from the quarry and up by most of a weapon's reach.
	private Vector2 StationOffset(Weapon w)
	{
		if (w.Dagger || w.Station < 1f)
			return Vector2.Zero;
		float tier = MathF.Floor((w.Station + 1f) * 0.5f);
		float reach = HeadOf(w);
		return new Vector2(w.Side * 0.45f * reach * tier, -0.55f * reach * tier);
	}

	private void Approach(Weapon w, Vector2 goal, float speed, float dt)
	{
		Vector2 to = goal - w.Pivot;
		float d = to.Length();
		if (d < 0.5f)
			return;
		// Eased in: fast across the field, settling as it arrives, never past the goal.
		float step = MathF.Min(d, MathF.Max(speed * dt * MathF.Min(1f, d / 60f + 0.25f), d * MathF.Min(1f, dt * 10f)));
		w.Pivot += to / d * step;
	}

	private static float TurnToward(float from, float to, float rate)
	{
		float diff = Mathf.Wrap(to - from, -MathF.PI, MathF.PI);
		return from + diff * MathF.Min(1f, rate);
	}

	private float FlySpeed() => form switch { Form.Sword => 640f, Form.Hammer => 470f, _ => 430f } * (colossal ? 1.15f : 1f);

	private static bool Alive(Enemy e) =>
		e != null && IsInstanceValid(e) && !e.IsDying && e.InEnemyRegistry;

	// --- the quarry ----------------------------------------------------------------------------------------

	// Each form's choice (nearest, densest knot, toughest), and then Spread out: an enemy inside
	// TargetSpacing of another weapon's quarry is only taken when nothing in reach is clear of them all. An
	// elite, a miniboss or a boss is never crowded out - several weapons may share one, on their stations.
	private Enemy Acquire(Weapon me, Vector2 player)
	{
		EnemyRegistry.CollectNear(player, huntRange, nearBuffer);
		Enemy best = null, bestCrowded = null;
		float bestScore = float.MaxValue, bestCrowdedScore = float.MaxValue;
		float rangeSq = huntRange * huntRange;
		float slam = HammerSlamRadius * areaMul * scale;
		int considered = 0;
		foreach (Enemy e in nearBuffer)
		{
			if (!Alive(e))
				continue;
			float dPlayerSq = e.GlobalPosition.DistanceSquaredTo(player);
			if (dPlayerSq > rangeSq)
				continue;
			float dPlayer = MathF.Sqrt(dPlayerSq);
			float score = dPlayer + 0.2f * e.GlobalPosition.DistanceTo(me.Pivot);
			switch (form)
			{
				case Form.Sword:
					// The toughest in reach: an elite or a boss first, then whatever has the most health.
					score -= 0.6f * MathF.Min(e.Health, 600);
					break;
				case Form.Hammer:
					// The densest knot: how many others stand inside the slam round it. Capped, so a late
					// swarm cannot make one choice quadratic.
					if (considered++ < 40)
						score -= 34f * Neighbours(e, slam);
					break;
				case Form.Axe:
					if (headsman && e.HealthFraction < HeadsmanThreshold)
						score -= 90f;
					break;
			}
			if (Claimed(me, e))
				score += form == Form.Hammer ? 260f : 140f;
			if (!e.IsMiniBoss && Crowds(me, e))
			{
				if (score < bestCrowdedScore)
				{
					bestCrowdedScore = score;
					bestCrowded = e;
				}
			}
			else if (score < bestScore)
			{
				bestScore = score;
				best = e;
			}
		}
		nearBuffer.Clear();
		return best ?? bestCrowded;
	}

	// True when another weapon's quarry is this enemy or stands within TargetSpacing of it.
	private bool Crowds(Weapon me, Enemy e)
	{
		float r2 = TargetSpacing * TargetSpacing;
		Vector2 at = e.GlobalPosition;
		foreach (Weapon w in weapons)
		{
			if (w == me || w.Leaving || !Alive(w.Target))
				continue;
			if (w.Target == e || w.Target.GlobalPosition.DistanceSquaredTo(at) <= r2)
				return true;
		}
		return false;
	}

	// The enemy nearest a point within a radius, or null: where a flurry's next cut goes when its quarry falls.
	private Enemy NearestTo(Vector2 at, float radius)
	{
		EnemyRegistry.CollectNear(at, radius, nearBuffer);
		Enemy best = null;
		float bestSq = radius * radius;
		foreach (Enemy e in nearBuffer)
		{
			if (!Alive(e))
				continue;
			float d = e.GlobalPosition.DistanceSquaredTo(at);
			if (d <= bestSq)
			{
				bestSq = d;
				best = e;
			}
		}
		nearBuffer.Clear();
		return best;
	}

	private int Neighbours(Enemy centre, float radius)
	{
		int n = 0;
		float r2 = radius * radius;
		Vector2 c = centre.GlobalPosition;
		foreach (Enemy e in nearBuffer)
		{
			if (e != centre && e.GlobalPosition.DistanceSquaredTo(c) <= r2)
				n++;
		}
		return n;
	}

	private bool Claimed(Weapon me, Enemy e)
	{
		foreach (Weapon w in weapons)
			if (w != me && !w.Leaving && w.Target == e)
				return true;
		return false;
	}

	// --- the blow ------------------------------------------------------------------------------------------

	private void Blow(Weapon w, Vector2 at, float share)
	{
		if (PlayerRef == null || SpellData == null)
			return;
		int dealt = Math.Max(1, Mathf.RoundToInt(damage * share));
		Node2D arena = GetParent() as Node2D;
		switch (form)
		{
			case Form.Sword:
				SwordBlow(w, at, dealt, arena);
				break;
			case Form.Hammer:
				HammerSlam(at, dealt, HammerSlamRadius * areaMul * scale, arena, echo: false);
				break;
			default:
				AxeBlow(w, at, dealt, arena);
				break;
		}
	}

	private void SwordBlow(Weapon w, Vector2 at, int dealt, Node2D arena)
	{
		float reach = SwordReach * areaMul * scale;
		Enemy quarry = Alive(w.Target) && w.Target.GlobalPosition.DistanceTo(at) <= reach + RiptideWave.BodyAllowance ? w.Target : null;
		// The quarry stepped out from under it: the nearest thing the blade came down on, if any.
		quarry ??= NearestTo(at, reach + RiptideWave.BodyAllowance);
		if (quarry != null)
			Strike(quarry, dealt);
		if (alongBlade)
			StrikeAlongBlade(w, dealt, quarry);
		// A flash at the point and the light it throws, to the reach it tested.
		if (arena != null)
			ImpactDebrisLayer.Spawn(arena, at, MathF.Max(10f, reach * 0.7f), arcane, tint);
		Sparks(at, 6, arcane, 70f);
	}

	// Twin Blades' dagger: one stab on its quarry and nothing else, for DaggerShare of a blow. It is never
	// colossal, so its reach is its own, not the greatsword's.
	private void Stab(Weapon w, Vector2 at)
	{
		if (PlayerRef == null || SpellData == null)
			return;
		int dealt = Math.Max(1, Mathf.RoundToInt(damage * DaggerShare));
		float reach = DaggerReach * areaMul;
		Enemy quarry = Alive(w.Target) && w.Target.GlobalPosition.DistanceTo(at) <= reach + RiptideWave.BodyAllowance ? w.Target : null;
		quarry ??= NearestTo(at, reach + RiptideWave.BodyAllowance);
		if (quarry != null)
			Strike(quarry, dealt);
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, at, MathF.Max(8f, reach * 0.7f), arcane, tint);
		Sparks(at, 4, light, 60f);
	}

	// Celestial Greatsword: everything along the blade as it comes down, as long as the blade is drawn
	// (from a third of the way up the blade to its point), the width of the blade plus a body's half-width.
	private void StrikeAlongBlade(Weapon w, int dealt, Enemy already)
	{
		Vector2 dir = Dir(w.Angle);
		float reach = HeadOf(w);
		Vector2 a = w.Pivot + dir * (reach * 0.35f);
		Vector2 b = w.Pivot + dir * (reach * 1.25f);
		float half = 14f * scale + RiptideWave.BodyAllowance;
		Vector2 mid = (a + b) * 0.5f;
		EnemyRegistry.CollectNear(mid, a.DistanceTo(b) * 0.5f + half, nearBuffer);
		Vector2 ab = b - a;
		float ll = MathF.Max(1e-3f, ab.LengthSquared());
		hitBuffer.Clear();
		foreach (Enemy e in nearBuffer)
		{
			if (!Alive(e) || e == already)
				continue;
			Vector2 p = e.GlobalPosition;
			float u = Mathf.Clamp((p - a).Dot(ab) / ll, 0f, 1f);
			if ((a + ab * u).DistanceSquaredTo(p) <= half * half)
				hitBuffer.Add(e);
		}
		nearBuffer.Clear();
		foreach (Enemy e in hitBuffer)
			Strike(e, dealt);
		hitBuffer.Clear();
		for (int i = 0; i < 6; i++)
			AddMote(a.Lerp(b, i / 5f), new Vector2(rng.RandfRange(-30f, 30f), rng.RandfRange(-50f, -10f)), 0.3f,
				i % 2 == 0 ? light.Core : light.Hot, 4f);
	}

	private void AxeBlow(Weapon w, Vector2 at, int dealt, Node2D arena)
	{
		float radius = AxeCleaveRadius * areaMul * scale;
		Vector2 player = PlayerRef.GlobalPosition;
		EnemyRegistry.CollectNear(at, radius + RiptideWave.BodyAllowance, hitBuffer);
		float r2 = (radius + RiptideWave.BodyAllowance) * (radius + RiptideWave.BodyAllowance);
		bool quarryHit = false;
		foreach (Enemy e in hitBuffer)
		{
			if (!Alive(e) || e.GlobalPosition.DistanceSquaredTo(at) > r2)
				continue;
			quarryHit |= e == w.Target;
			int hit = dealt;
			if (headsman && e.HealthFraction < HeadsmanThreshold)
			{
				hit *= 2;
				StatExecutions++;
				Sparks(e.GlobalPosition, 5, light, 90f);
			}
			Vector2 away = e.GlobalPosition - player;
			Strike(e, hit);
			if (away.LengthSquared() > 1f && IsInstanceValid(e) && !e.IsDying)
				e.ApplyKnockback(away.Normalized() * knockback);
		}
		hitBuffer.Clear();
		if (!quarryHit && Alive(w.Target) && w.Target.GlobalPosition.DistanceSquaredTo(at) <= r2 * 1.5f)
			Strike(w.Target, dealt);
		if (arena != null)
			ImpactDebrisLayer.Spawn(arena, at, radius, arcane, tint);
		Sparks(at, 8, arcane, 110f);
	}

	private void HammerSlam(Vector2 at, int dealt, float radius, Node2D arena, bool echo)
	{
		EnemyRegistry.CollectNear(at, radius + RiptideWave.BodyAllowance, hitBuffer);
		float r2 = (radius + RiptideWave.BodyAllowance) * (radius + RiptideWave.BodyAllowance);
		foreach (Enemy e in hitBuffer)
		{
			if (!Alive(e) || e.GlobalPosition.DistanceSquaredTo(at) > r2)
				continue;
			Strike(e, dealt);
			if (!IsInstanceValid(e) || e.IsDying)
				continue;
			// The ground goes out from under them: slowed, jolted and - under the Titan's Maul - stopped dead
			// (a freeze is a slow to nothing; Enemy.ApplySlow holds a boss above its floor).
			e.ApplySlow(1f - slowPower, slowSeconds);
			if (freezeSeconds > 0f && !echo)
				e.ApplySlow(0f, freezeSeconds);
			e.ApplyShock(HammerJoltSeconds, 2.5f, 38f);
		}
		hitBuffer.Clear();

		// Enemy bolts inside the slam are crushed into the ground with it.
		IReadOnlyList<EnemyProjectile> bolts = EnemyProjectile.Live;
		for (int b = bolts.Count - 1; b >= 0; b--)
		{
			EnemyProjectile bolt = bolts[b];
			if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight)
				continue;
			float reach = radius + bolt.Radius;
			if (bolt.GlobalPosition.DistanceSquaredTo(at) > reach * reach)
				continue;
			bolt.Destroy(Element.Arcane);
			StatBoltsCrushed++;
		}

		AddSlam(at, radius, echo);
		if (arena != null)
			ImpactDebrisLayer.Spawn(arena, at, radius, hallowed ? light : arcane, tint);
		Shake(echo ? 1.5f : colossal ? 4f : 2.5f);
		if (!echo && aftershock)
			QueueEcho(at, radius * AftershockReach, Math.Max(1, Mathf.RoundToInt(dealt * AftershockShare)));
		if (!echo && hallowed)
			AddZone(at, radius, zoneSeconds);
	}

	private void Strike(Enemy e, int amount)
	{
		PlayerRef.DealDamageToEnemy(e, amount, critBonus, source: SpellData);
		StatHits++;
		if (siphon)
			Siphon();
	}

	private void Siphon()
	{
		if (siphoned >= SiphonPerSecond || PlayerRef == null)
			return;
		PlayerRef.Heal(1);
		siphoned++;
	}

	// --- the sword's guard ---------------------------------------------------------------------------------

	// THE SWORD PARRIES (2026-10-28, Eric: "a few spells should help guard you from projectiles"; kept from
	// the orbiting blades, which cut what they passed through). Any enemy bolt that touches a sword's blade -
	// hanging at his shoulder, hunting or in the swing, Twin Blades' dagger's too - is cut out of the air. A
	// guard, not a wall: the blades are where the swords are, and a volley from the other side gets through.
	// Deflection sends a sword out to put its blade in a bolt's way (Intercept); the cut is still this one,
	// tested against the blade as it is drawn.
	private void ParryBolts()
	{
		IReadOnlyList<EnemyProjectile> bolts = EnemyProjectile.Live;
		if (bolts.Count == 0)
			return;
		for (int b = bolts.Count - 1; b >= 0; b--)
		{
			EnemyProjectile bolt = bolts[b];
			if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight)
				continue;
			Weapon cutter = null;
			foreach (Weapon w in weapons)
			{
				if (!w.Leaving && BladeTouches(w, bolt))
				{
					cutter = w;
					break;
				}
			}
			if (cutter == null && dagger != null && !dagger.Leaving && BladeTouches(dagger, bolt))
				cutter = dagger;
			if (cutter == null)
				continue;
			Vector2 p = bolt.GlobalPosition;
			Weapon deflector = bolt.ClaimedBy is Weapon claimant && claimant.Phase == Phase.Deflect && claimant.Bolt == bolt ? claimant : null;
			bolt.Destroy(Element.Arcane);
			StatBoltsParried++;
			if (deflector != null)
			{
				// Swatted aside: its pieces fly on across its path, the way the blade was going.
				StatBoltsDeflected++;
				SwatAside(p, deflector.Aside);
				EndDeflect(deflector, DeflectRest);
			}
			else
				Sparks(p, 5, light, 80f);
		}
	}

	// The blade from just above the grip to past the head, as wide as the guard and the bolt.
	private bool BladeTouches(Weapon w, EnemyProjectile bolt)
	{
		float reach = HeadOf(w);
		float guard = ParryHalfWidth * (w.Dagger ? 0.7f : scale);
		Vector2 dir = Dir(w.Angle);
		Vector2 a = w.Pivot + dir * (reach * 0.2f);
		Vector2 ab = dir * (reach * 1.05f);
		Vector2 p = bolt.GlobalPosition;
		float u = Mathf.Clamp((p - a).Dot(ab) / MathF.Max(1e-3f, ab.LengthSquared()), 0f, 1f);
		float r = guard + bolt.Radius;
		return (a + ab * u).DistanceSquaredTo(p) <= r * r;
	}

	// --- Deflection -----------------------------------------------------------------------------------------

	// Each bolt coming at the wizard inside DeflectWatch of him, not already taken by a guard, goes to the
	// nearest free sword whose blade can reach where it will be in DeflectSeconds.
	private void Intercept(Vector2 player)
	{
		IReadOnlyList<EnemyProjectile> bolts = EnemyProjectile.Live;
		for (int b = 0; b < bolts.Count; b++)
		{
			EnemyProjectile bolt = bolts[b];
			if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight || bolt.ClaimedBy != null)
				continue;
			Vector2 toPlayer = player - bolt.GlobalPosition;
			float distance = toPlayer.Length();
			if (distance > DeflectWatch || distance < 1f)
				continue;
			if (bolt.Heading.Dot(toPlayer / distance) < DeflectThreatCosine)
				continue;
			Vector2 meet = bolt.GlobalPosition + bolt.Heading * bolt.Speed * DeflectSeconds;
			Weapon best = null;
			float bestDistance = DeflectReach;
			foreach (Weapon w in weapons)
			{
				if (w.Leaving || w.DeflectRest > 0f || (w.Phase != Phase.Heel && w.Phase != Phase.Hunt))
					continue;
				float d = (w.Pivot + Dir(w.Angle) * (HeadOf(w) * 0.65f)).DistanceTo(meet);
				if (d < bestDistance)
				{
					bestDistance = d;
					best = w;
				}
			}
			if (best != null)
				StartDeflect(best, bolt, meet);
		}
	}

	private void StartDeflect(Weapon w, EnemyProjectile bolt, Vector2 meet)
	{
		bolt.ClaimedBy = w;
		w.Bolt = bolt;
		w.Phase = Phase.Deflect;
		w.T = 0f;
		w.Duration = DeflectSeconds;
		w.FromPivot = w.Pivot;
		w.FromAngle = w.Angle;
		// Across the bolt's path, reaching over it from the side the sword comes in on; the pieces go on
		// across, the way the blade was going.
		Vector2 across = new Vector2(-bolt.Heading.Y, bolt.Heading.X);
		if (across.Dot(meet - w.Pivot) < 0f)
			across = -across;
		w.Aside = across;
		// The swing closes onto the path from downstream of it, so the point moves with the bolt, never
		// against it: knocked aside, not batted back.
		w.SwatSign = bolt.Heading.Cross(across) > 0f ? -1f : 1f;
	}

	private void TickDeflect(Weapon w)
	{
		EnemyProjectile bolt = w.Bolt;
		if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight)
		{
			// Taken by another guard, broken on something, or gone: back to its quarry with a short rest.
			EndDeflect(w, DeflectRest * 0.5f);
			return;
		}
		float t = Mathf.Clamp(w.T / w.Duration, 0f, 1f);
		float e = 1f - (1f - t) * (1f - t);
		float reach = HeadOf(w);
		Vector2 meet = bolt.GlobalPosition + bolt.Heading * bolt.Speed * MathF.Max(0f, w.Duration - w.T);
		w.Pivot = w.FromPivot.Lerp(meet - w.Aside * (reach * 0.65f), e);
		float contact = w.Aside.Angle();
		float swat = contact + w.SwatSign * Mathf.DegToRad(50f) * (1f - e);
		w.Angle = Mathf.LerpAngle(w.FromAngle, swat, MathF.Min(1f, t * 3f));
		// ParryBolts takes the bolt the frame the blade is on it. One that slipped past is let go.
		if (w.T >= w.Duration + 0.1f)
			EndDeflect(w, DeflectRest * 0.4f);
	}

	private void EndDeflect(Weapon w, float rest)
	{
		ReleaseBolt(w);
		w.DeflectRest = rest;
		w.Phase = Alive(w.Target) ? Phase.Hunt : Phase.Heel;
		w.T = 0f;
		w.Retarget = 0f;
	}

	private static void ReleaseBolt(Weapon w)
	{
		if (w.Bolt != null && IsInstanceValid(w.Bolt) && w.Bolt.ClaimedBy == w)
			w.Bolt.ClaimedBy = null;
		w.Bolt = null;
	}

	private void SwatAside(Vector2 at, Vector2 aside)
	{
		Vector2 along = new Vector2(-aside.Y, aside.X);
		for (int i = 0; i < 8; i++)
		{
			Vector2 dir = (aside + along * rng.RandfRange(-0.45f, 0.45f)).Normalized();
			AddMote(at, dir * rng.RandfRange(90f, 170f), 0.24f, i % 3 == 0 ? light.Core : i % 3 == 1 ? arcane.Hot : arcane.Mid,
				i % 2 == 0 ? 4f : 2f);
		}
	}

	// --- the hammer's echoes and hallowed ground ----------------------------------------------------------

	private void QueueEcho(Vector2 at, float radius, int amount)
	{
		echoes[nextEcho] = new Echo { At = at, Radius = radius, Damage = amount, Delay = AftershockDelay, Live = true };
		nextEcho = (nextEcho + 1) % MaxEchoes;
	}

	private void TickEchoes(float dt)
	{
		for (int i = 0; i < MaxEchoes; i++)
		{
			if (!echoes[i].Live)
				continue;
			echoes[i].Delay -= dt;
			if (echoes[i].Delay > 0f)
				continue;
			echoes[i].Live = false;
			StatEchoes++;
			if (PlayerRef != null && SpellData != null)
				HammerSlam(echoes[i].At, echoes[i].Damage, echoes[i].Radius, GetParent() as Node2D, echo: true);
		}
	}

	private void AddZone(Vector2 at, float radius, float seconds)
	{
		zones[nextZone] = new Zone { At = at, Radius = radius, Left = seconds, Life = seconds, Tick = 0f, Seed = rng.Randi(), Live = true };
		nextZone = (nextZone + 1) % MaxZones;
	}

	private void TickZones(float dt)
	{
		for (int i = 0; i < MaxZones; i++)
		{
			ref Zone z = ref zones[i];
			if (!z.Live)
				continue;
			z.Left -= dt;
			if (z.Left <= 0f)
			{
				z.Live = false;
				continue;
			}
			// Motes of light rise out of it, inside it, the whole time it stands.
			if (rng.Randf() < dt * 10f)
			{
				float a = rng.Randf() * Mathf.Tau;
				float r = MathF.Sqrt(rng.Randf()) * z.Radius * 0.9f;
				AddMote(z.At + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.6f), new Vector2(0f, -rng.RandfRange(20f, 40f)), 0.6f,
					rng.Randf() < 0.5f ? light.Hot : light.Mid, 2f);
			}
			z.Tick -= dt;
			if (z.Tick > 0f)
				continue;
			z.Tick = HallowTick;
			EnemyRegistry.CollectNear(z.At, z.Radius, nearBuffer);
			float r2 = z.Radius * z.Radius;
			foreach (Enemy e in nearBuffer)
			{
				if (!Alive(e) || e.GlobalPosition.DistanceSquaredTo(z.At) > r2)
					continue;
				e.ApplyWeaken(HallowWeaken, HallowTick * 2.5f);
				e.ApplySlow(1f - HallowSlow, HallowTick * 2.5f);
			}
			nearBuffer.Clear();
		}
	}

	// --- the ground shaking --------------------------------------------------------------------------------

	private void Shake(float amplitude)
	{
		if (camera == null || !IsInstanceValid(camera))
			camera = PlayerRef?.GetNodeOrNull<Camera2D>("Camera2D");
		if (camera == null)
			return;
		shakeAmplitude = MathF.Max(shakeAmplitude * (shakeLeft / ShakeSeconds), amplitude);
		shakeLeft = ShakeSeconds;
	}

	private void TickShake(float dt)
	{
		if (shakeLeft <= 0f || camera == null || !IsInstanceValid(camera))
			return;
		shakeLeft -= dt;
		if (shakeLeft <= 0f)
		{
			camera.Offset = Vector2.Zero;
			shakeAmplitude = 0f;
			return;
		}
		float k = shakeAmplitude * (shakeLeft / ShakeSeconds);
		// Mostly up and down: the ground jumps, it does not slide.
		camera.Offset = new Vector2(rng.RandfRange(-0.35f, 0.35f) * k, rng.RandfRange(-1f, 1f) * k);
	}

	// --- effects: fixed pools, drawn on the 2 px grid -----------------------------------------------------

	private void AddMote(Vector2 at, Vector2 vel, float life, Color colour, float size)
	{
		motes[nextMote] = new Mote { At = at, Vel = vel, Life = life, Colour = colour, Size = size, Live = true };
		nextMote = (nextMote + 1) % MaxMotes;
	}

	private void AddSlam(Vector2 at, float radius, bool echo)
	{
		slams[nextSlam] = new Slam { At = at, Radius = radius, Life = echo ? 0.45f : 0.7f, Seed = rng.Randi(), Live = true, Echo = echo };
		nextSlam = (nextSlam + 1) % MaxSlams;
	}

	private void Sparks(Vector2 at, int n, in ElementColors.Ramp ramp, float speed)
	{
		for (int i = 0; i < n; i++)
		{
			float a = rng.Randf() * Mathf.Tau;
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			AddMote(at, dir * rng.RandfRange(speed * 0.5f, speed), 0.22f, i % 2 == 0 ? ramp.Core : ramp.Hot, i % 3 == 0 ? 4f : 2f);
		}
	}

	private void Puff(Vector2 at, bool inward)
	{
		for (int i = 0; i < 12; i++)
		{
			float a = i * Mathf.Tau / 12f + rng.Randf() * 0.4f;
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			float d = rng.RandfRange(16f, 28f);
			Color c = i % 3 == 0 ? arcane.Core : i % 3 == 1 ? arcane.Hot : arcane.Mid;
			if (inward)
				AddMote(at + dir * d, -dir * d / 0.2f, 0.2f, c, 4f);
			else
				AddMote(at + dir * 4f, dir * d / 0.24f, 0.24f, c, 4f);
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
		for (int i = 0; i < MaxSlams; i++)
		{
			if (!slams[i].Live)
				continue;
			slams[i].Age += dt;
			if (slams[i].Age >= slams[i].Life)
				slams[i].Live = false;
		}
	}

	// A colour of the ramp under the spell's tint, with an alpha the canvas's shader reads as coverage.
	private Color Tinted(Color c, float alpha) => new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha);

	private static Vector2 Snap(Vector2 p) => new Vector2(MathF.Round(p.X * 0.5f) * 2f, MathF.Round(p.Y * 0.5f) * 2f);

	// The floor: each weapon's shadow. A half-tone of occlusion (the canvas dithers it), shrinking and
	// thinning the higher the weapon is: that is what says it is in the air.
	private void DrawGround()
	{
		foreach (Weapon w in weapons)
			DrawShadow(w, colossal ? 34f : 18f);
		if (dagger != null)
			DrawShadow(dagger, 10f);
	}

	private void DrawShadow(Weapon w, float width)
	{
		Color occ = new Color(0.02f, 0.027f, 0.047f, 1f);
		Vector2 centre = w.Pivot + Dir(w.Angle) * CentreOf(w);
		float height = MathF.Max(0f, w.Ground - centre.Y);
		float k = Mathf.Clamp(1f - height / 220f, 0.35f, 1f);
		float rx = width * k;
		Color c = new Color(occ, 0.6f * k * w.Alpha);
		Vector2 at = Snap(new Vector2(centre.X, w.Ground));
		for (int row = -2; row <= 2; row += 2)
		{
			float f = 1f - MathF.Abs(row) / 3.2f;
			float half = MathF.Round(rx * f * 0.5f) * 2f;
			ground.DrawRect(new Rect2(at.X - half, at.Y + row - 1f, half * 2f, 2f), c);
		}
	}

	// The quake under a slam (and Aftershock's echo) and the hallowed ground, through spirit_quake.gdshader:
	// one quad each, the slam's numbers in the vertex colour, out to exactly the radius it hit. Cracks
	// running out to it, a heave of broken ground racing to it, holy light scattered over the zone.
	private void DrawQuake()
	{
		Texture2D quad = ScorchGround.QuadTexture;
		for (int i = 0; i < MaxSlams; i++)
		{
			ref Slam s = ref slams[i];
			if (!s.Live)
				continue;
			float r = MathF.Min(s.Radius, QuakeRadiusScale - 1f);
			var extent = new Vector2(r, r * CraterGround.Squash);
			var parameters = new Color(Mathf.Clamp(s.Age / s.Life, 0f, 1f), r / QuakeRadiusScale, (s.Seed % 997u) / 997f,
				s.Echo ? 0.5f : 0.25f);
			quake.DrawTextureRect(quad, new Rect2(Snap(s.At) - extent, extent * 2f), false, parameters);
		}
		for (int i = 0; i < MaxZones; i++)
		{
			ref Zone z = ref zones[i];
			if (!z.Live)
				continue;
			float r = MathF.Min(z.Radius, QuakeRadiusScale - 1f);
			var extent = new Vector2(r, r * CraterGround.Squash);
			var parameters = new Color(Mathf.Clamp(z.Left / MathF.Max(0.01f, z.Life), 0f, 1f), r / QuakeRadiusScale,
				(z.Seed % 997u) / 997f, 1f);
			quake.DrawTextureRect(quad, new Rect2(Snap(z.At) - extent, extent * 2f), false, parameters);
		}
	}

	// The afterimages through a strike: the last two headings, as stipples of the weapon's own shape in
	// Arcane's mid and hot (the canvas fills the shape with the colour and dithers it by the alpha). The
	// sword's are drawn at the grips they had (at speed, turned round the grip it has now they fanned out
	// over the blade); the axe's and the hammer's round its grip, as they always were. The dagger's stab is
	// a thrust along one line, where an afterimage would lie under the dagger itself.
	private void DrawGhosts()
	{
		if (sheet == null)
			return;
		bool trueGrips = form == Form.Sword;
		foreach (Weapon w in weapons)
		{
			if (w.Phase != Phase.Strike)
				continue;
			int row = sheetRows > 1 && w.Side < 0f ? 1 : 0;
			if (!float.IsNaN(w.Ghost2))
				DrawWeapon(ghosts, w, trueGrips ? w.GhostPivot2 : w.Pivot, w.Ghost2, row, Tinted(arcane.Mid, 0.3f * w.Alpha));
			if (!float.IsNaN(w.Ghost1))
				DrawWeapon(ghosts, w, trueGrips ? w.GhostPivot1 : w.Pivot, w.Ghost1, row, Tinted(arcane.Hot, 0.5f * w.Alpha));
		}
	}

	// The air: the slashes, the weapons, and the motes.
	private void DrawAir()
	{
		foreach (Weapon w in weapons)
		{
			if (sheet == null)
				break;
			int row = sheetRows > 1 && w.Side < 0f ? 1 : 0;
			DrawSlash(w, w.Fading);
			DrawSlash(w, w.Slash);
			// A weapon coming or going dissolves through the dither rather than fading.
			DrawWeapon(air, w, w.Pivot, w.Angle, row, new Color(tint.R, tint.G, tint.B, w.Alpha));
		}
		if (dagger != null)
		{
			DrawSlash(dagger, dagger.Fading);
			DrawSlash(dagger, dagger.Slash);
			DrawWeapon(air, dagger, dagger.Pivot, dagger.Angle, 0, new Color(tint.R, tint.G, tint.B, dagger.Alpha));
		}

		for (int i = 0; i < MaxMotes; i++)
		{
			ref Mote m = ref motes[i];
			if (!m.Live)
				continue;
			float f = m.Age / m.Life;
			air.DrawRect(WhipLimb.Cell(m.At, m.Size), Tinted(m.Colour, f < 0.6f ? 1f : (1f - f) / 0.4f));
		}
	}

	// The slash: the arc the head swept, from where it came over the top to where it is - wide and white at
	// the head, narrowing and cooling down the ramp behind it - stamped as square cells on the grid along the
	// head's own path, sampled; and, once the blow has landed, the whole arc breaking up in a sixth of a
	// second. Under the weapon, so the weapon leads it.
	private void DrawSlash(Weapon w, in SlashArc s)
	{
		if (s.Progress <= 0f || s.Age >= SlashLife)
			return;
		float fade = 1f - s.Age / SlashLife;
		float span = form == Form.Sword && !w.Dagger ? 0.75f : 0.9f;
		float from = MathF.Max(0f, s.Progress - span);
		float width = w.Dagger ? 4f : (form == Form.Sword ? 6f : 10f) * (colossal ? 2f : 1f);
		Vector2 prev = SlashHead(w, s, from);
		for (int k = 1; k <= SlashSamples; k++)
		{
			float f = k / (float)SlashSamples;
			Vector2 at = SlashHead(w, s, Mathf.Lerp(from, s.Progress, f));
			Color c = Tinted(f > 0.8f ? arcane.Core : f > 0.5f ? arcane.Hot : f > 0.25f ? arcane.Mid : arcane.Edge,
				fade * (0.35f + 0.65f * f));
			float size = WhipLimb.SnapSize(width * (0.3f + 0.7f * f));
			// A square every half its size along the chord, so the stroke is solid and its edge a staircase
			// of whole art pixels at any angle.
			int n = Math.Max(1, Mathf.CeilToInt(prev.DistanceTo(at) / MathF.Max(WhipLimb.Grid, size * 0.5f)));
			for (int j = 1; j <= n; j++)
				air.DrawRect(WhipLimb.Cell(prev.Lerp(at, j / (float)n), size), c);
			prev = at;
		}
	}

	private void DrawWeapon(Node2D canvas, Weapon w, Vector2 pivot, float angle, int row, Color modulate)
	{
		Texture2D drawing = w.Dagger ? daggerSheet : sheet;
		if (drawing == null)
			return;
		int cellPx = w.Dagger ? DaggerCell : sheetCell;
		float cellWorld = cellPx * 2f;
		int cell = ((int)MathF.Round(angle / Mathf.Tau * Headings) % Headings + Headings) % Headings;
		// The drawing's heading is the CELL's, so its centre is placed along the cell's heading, not the
		// exact angle: the grip then sits where the drawing's grip is.
		float drawn = cell * Mathf.Tau / Headings;
		Vector2 centre = Snap(pivot + Dir(drawn) * CentreOf(w));
		var dest = new Rect2(centre - new Vector2(cellWorld, cellWorld) * 0.5f, new Vector2(cellWorld, cellWorld));
		var src = new Rect2(cell * cellPx, row * cellPx, cellPx, cellPx);
		canvas.DrawTextureRectRegion(drawing, dest, src, modulate);
	}
}
