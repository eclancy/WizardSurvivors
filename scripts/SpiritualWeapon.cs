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
// it is what says it is in the air. The canonical angles (CanonImpact, CanonRaised, CanonIdle) are for a
// weapon to the RIGHT of its quarry; one to the left mirrors them, so the arc always goes over the top.
//
// THE DRAWING. tools/art/spirit_weapons.py draws every form at 32 headings (pixel art turned by the engine
// smears; a sheet rasterised at the heading keeps its pixels - SleetRunes' reason) on the elite cell, and
// the three colossal Ascension forms on the boss cell, twice the size. The node picks the cell nearest
// the heading and draws it at x2. Nothing is instanced per swing: weapons, trails, motes, cracks and
// hallowed ground are slots in fixed arrays, drawn by two canvas items (the air above the actors, the
// ground under them).
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
	public const string RiposteId = "spiritual_weapon_sword_riposte";
	public const string SoulSiphonId = "spiritual_weapon_sword_soul_siphon";
	public const string GreatswordId = "spiritual_weapon_sword_celestial_greatsword";
	public const string ThousandCutsId = "spiritual_weapon_sword_thousand_cuts";

	public enum Form : byte { Axe, Hammer, Sword }

	/// <summary>The most weapons in the air, whatever the count says.</summary>
	public const int MaxWeapons = 6;

	// --- what a blow reaches, in world pixels before the player's Area, the form's Area and its Scale ----
	/// <summary>How far from where the sword's blade comes down its quarry may stand and still be cut.</summary>
	public const float SwordReach = 34f;
	public const float AxeCleaveRadius = 46f;
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

	// The sword's guard: half the width of the blade that cuts bolts, and Riposte's reach on it.
	private const float ParryHalfWidth = 10f;
	public const float RiposteGuard = 1.6f;

	// Thousand Cuts: the flurry.
	public const int FlurryCuts = 3;
	public const float FlurryShare = 0.8f;

	// Soul Siphon: at most this much healed a second, whatever the swords land.
	public const int SiphonPerSecond = 3;

	// --- the drawings (tools/art/spirit_weapons.py), in world pixels at x2 ---------------------------------
	private const int Headings = 32;
	private const int BaseCell = 48;
	private const int ColossalCell = 96;
	// From the grip to the middle of the drawing, and to the head (the axe's bit, the hammer's face, the
	// sword's point of percussion two thirds up the blade), along the heading.
	private const float BaseGripToCentre = 26f;
	private const float BaseGripToHead = 52f;
	private const float ColossalGripToCentre = 56f;
	private const float ColossalGripToHead = 112f;

	// --- the swing, in degrees, for a weapon to the right of its quarry (mirrored on the left) -------------
	private const float CanonImpact = 145f;   // head down onto the quarry, from above and to its right
	private const float CanonRaised = 345f;   // wound back over its shoulder: the arc runs over the top
	private const float CanonIdle = 285f;     // hanging head-up, leaning away from its quarry
	private const float FlurryRaised = 230f;  // a flurry's short lift between cuts

	private enum Phase : byte { Heel, Hunt, Raise, Strike, Recover, Leave }

	private sealed class Weapon
	{
		public Vector2 Pivot;
		public float Angle;            // radians, world, from the grip toward the head
		public Phase Phase;
		public float T;
		public Enemy Target;
		public bool HasSwing;
		public float SwingDelay;
		public float Side = 1f;        // +1: to the right of its quarry
		public float Retarget;
		public float Bob;
		public Vector2 FromPivot;
		public float FromCanon;
		public float Duration;
		public bool Mini;              // a flurry's short swing
		public int FlurryLeft;
		public float Ground;           // the y its shadow lies on
		public float Alpha = 1f;
		public float Spin;             // Whirling Axes: its turn between blows
		public bool Leaving;
		// The last strike, for the slash it leaves: where it came from and went to, how far through it
		// the head is (eased, 0 to 1; -1 before the first), and how long since it landed. The slash is
		// drawn from these - the head's own path, sampled - so it is a smooth arc at any frame rate, where
		// a trail of the head's per-frame positions was a few long straight chords.
		public Vector2 SlashFromPivot, SlashToPivot;
		public float SlashFromCanon, SlashSide = 1f, SlashProgress = -1f, SlashAge;
		// The angles of the last few frames of a strike, for its afterimages.
		public float Ghost1 = float.NaN, Ghost2 = float.NaN;
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
		public bool Live;
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
	private Texture2D baseSheet;
	private Texture2D colossalSheet;
	private Texture2D sheet;
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
	private float slowPower = HammerSlow;
	private float slowSeconds = HammerSlowSeconds;
	private float freezeSeconds;
	private float knockback = AxeKnockback;
	private float zoneSeconds;
	private bool headsman, aftershock, hallowed, whirling, riposte, siphon, flurry, alongBlade;
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

	/// <summary>One line for the balance report: is it swinging, and how much does each swing reach.</summary>
	public static string StatsLine() =>
		$"spirit weapon: {StatSwings} swings, {StatHits} enemies struck ({(StatSwings > 0 ? StatHits / (float)StatSwings : 0f):0.0} a swing); "
		+ $"{StatBoltsParried} bolts parried, {StatBoltsCrushed} crushed; {StatExecutions} executions, {StatEchoes} aftershocks";

	public override void _Ready()
	{
		rng.Randomize();
		GlobalPosition = Vector2.Zero;
		// The weapons are in the air, over the bodies (as Familiar's effects are) and under the HUD; their
		// shadows, the cracks and the hallowed ground are on the floor, under the bodies.
		ground = new Node2D { Name = "SpiritWeaponGround", ZAsRelative = false, ZIndex = -1 };
		air = new Node2D { Name = "SpiritWeaponAir", ZAsRelative = false, ZIndex = 6 };
		AddChild(ground);
		AddChild(air);
		ground.Draw += DrawGround;
		air.Draw += DrawAir;
	}

	public override void _ExitTree()
	{
		if (camera != null && IsInstanceValid(camera))
			camera.Offset = Vector2.Zero;
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
		riposte = spell.HasEvolution(RiposteId);
		flurry = spell.HasEvolution(ThousandCutsId);
		alongBlade = spell.HasEvolution(GreatswordId);
		colossal = spell.HasEvolution(GreatswordId) || spell.HasEvolution(GreataxeId) || spell.HasEvolution(TitansMaulId);
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
			weapons[i].Leaving = true;
			weapons[i].Phase = Phase.Leave;
			weapons[i].T = 0f;
			live--;
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

	private void AddWeapon()
	{
		var w = new Weapon { Bob = rng.Randf() * Mathf.Tau, Phase = Phase.Heel, Alpha = 0f };
		Vector2 at = PlayerRef != null ? HeelSpot(weapons.Count, Math.Max(1, weapons.Count + 1)) : Vector2.Zero;
		w.Pivot = at;
		w.Angle = Mathf.DegToRad(-90f);
		w.Ground = PlayerRef?.FootPosition.Y ?? at.Y + 40f;
		weapons.Add(w);
		Puff(at + Dir(w.Angle) * GripToCentre, inward: true);
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
					Puff(w.Pivot + Dir(w.Angle) * GripToCentre, inward: false);
					weapons.RemoveAt(i);
				}
			}
			if (form == Form.Sword)
				ParryBolts();
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
		ground?.QueueRedraw();
	}

	private void Tick(Weapon w, int index, int count, float dt)
	{
		w.T += dt;
		w.Bob += dt * 2.6f;
		w.SwingDelay = MathF.Max(0f, w.SwingDelay - dt);
		w.Alpha = w.Phase == Phase.Leave ? MathF.Max(0f, 1f - w.T / 0.2f) : MathF.Min(1f, w.Alpha + dt / 0.18f);
		if (w.SlashProgress >= 1f)
			w.SlashAge += dt;
		Vector2 player = PlayerRef.GlobalPosition;

		switch (w.Phase)
		{
			case Phase.Leave:
				return;
			case Phase.Raise:
				TickRaise(w);
				return;
			case Phase.Strike:
				TickStrike(w);
				return;
			case Phase.Recover:
				TickRecover(w);
				return;
		}

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
			Vector2 heel = HeelSpot(index, count) + new Vector2(0f, MathF.Sin(w.Bob) * 3f);
			Approach(w, heel, FlySpeed() * 0.9f, dt);
			float lean = (index - (count - 1) * 0.5f) * 0.22f;
			Hang(w, Mathf.DegToRad(-90f) + lean, dt);
			w.Ground = Mathf.Lerp(w.Ground, PlayerRef.FootPosition.Y + 6f, MathF.Min(1f, dt * 6f));
			return;
		}

		w.Phase = Phase.Hunt;
		Vector2 t = w.Target.GlobalPosition;
		// Keep the side it came in on until it is well over the other side, or a weapon level with its
		// quarry would swap sides every frame.
		float dx = w.Pivot.X - t.X;
		if (dx * w.Side < -0.5f * GripToHead)
			w.Side = -w.Side;
		Vector2 hold = ImpactPivot(t, w.Side) + new Vector2(w.Side * 0.10f * GripToHead, -0.30f * GripToHead + MathF.Sin(w.Bob) * 2f);
		Approach(w, hold, FlySpeed(), dt);
		Hang(w, Mirror(CanonIdle, w.Side), dt);
		w.Ground = Mathf.Lerp(w.Ground, t.Y + 26f, MathF.Min(1f, dt * 8f));

		if (w.HasSwing && w.SwingDelay <= 0f && w.Pivot.DistanceTo(hold) < 0.45f * GripToHead)
			StartRaise(w, mini: false);
	}

	// Between blows a weapon hangs head-up; Whirling Axes' spin end over end instead.
	private void Hang(Weapon w, float rest, float dt)
	{
		if (whirling)
		{
			w.Spin = (w.Spin + dt * 9f) % Mathf.Tau;
			w.Angle = rest + w.Spin;
			return;
		}
		w.Spin = 0f;
		w.Angle = TurnToward(w.Angle, rest, dt * 9f);
	}

	private void StartRaise(Weapon w, bool mini)
	{
		w.Phase = Phase.Raise;
		w.T = 0f;
		w.Mini = mini;
		w.FromPivot = w.Pivot;
		w.FromCanon = ToCanon(w.Angle, w.Side);
		w.Duration = (mini ? 0.05f : RaiseSeconds()) / tempo;
		w.Spin = 0f;
		if (!mini)
		{
			w.HasSwing = false;
			w.FlurryLeft = flurry ? FlurryCuts - 1 : 0;
			StatSwings++;
		}
	}

	private void TickRaise(Weapon w)
	{
		if (!Alive(w.Target))
		{
			EndSwing(w);
			return;
		}
		float t = Mathf.Clamp(w.T / w.Duration, 0f, 1f);
		float e = t * t * (3f - 2f * t);
		Vector2 target = w.Target.GlobalPosition;
		Vector2 raised = ImpactPivot(target, w.Side) + RaiseLift(w);
		w.Pivot = w.FromPivot.Lerp(raised, e);
		float canon = Mathf.Lerp(w.FromCanon, w.Mini ? FlurryRaised : CanonRaised, e);
		w.Angle = Mirror(canon, w.Side);
		w.Ground = Mathf.Lerp(w.Ground, target.Y + 26f, 0.3f);
		if (t >= 1f)
		{
			w.Phase = Phase.Strike;
			w.T -= w.Duration;
			w.FromPivot = w.Pivot;
			w.FromCanon = w.Mini ? FlurryRaised : CanonRaised;
			w.Duration = (w.Mini ? 0.05f : StrikeSeconds()) / tempo;
			w.Ghost1 = w.Ghost2 = float.NaN;
			// The whoosh as it comes over: one per swing, throttled so a flight of swords is one sound.
			if (!w.Mini)
				SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellOrbit, w.Pivot, 0.12f, SfxCatalog.SpellOrbit, 70);
		}
	}

	private Vector2 RaiseLift(Weapon w) =>
		w.Mini ? new Vector2(0f, -0.12f * GripToHead) : new Vector2(w.Side * 0.15f * GripToHead, -0.50f * GripToHead);

	private void TickStrike(Weapon w)
	{
		Vector2 target = Alive(w.Target) ? w.Target.GlobalPosition : HeadAt(w);
		float t = Mathf.Clamp(w.T / w.Duration, 0f, 1f);
		// A blow that accelerates: slow over the top, fast at the end.
		float e = t * t;
		w.Ghost2 = w.Ghost1;
		w.Ghost1 = w.Angle;
		w.Pivot = w.FromPivot.Lerp(ImpactPivot(target, w.Side), e);
		w.Angle = Mirror(Mathf.Lerp(w.FromCanon, CanonImpact, e), w.Side);
		w.SlashFromPivot = w.FromPivot;
		w.SlashToPivot = ImpactPivot(target, w.Side);
		w.SlashFromCanon = w.FromCanon;
		w.SlashSide = w.Side;
		w.SlashProgress = e;
		w.SlashAge = 0f;
		if (t >= 1f)
		{
			Blow(w, HeadAt(w), w.Mini ? FlurryShare : (flurry ? FlurryShare : 1f));
			w.T -= w.Duration;
			if (w.FlurryLeft > 0 && Alive(w.Target))
			{
				w.FlurryLeft--;
				StartRaise(w, mini: true);
				return;
			}
			w.Phase = Phase.Recover;
			w.FromPivot = w.Pivot;
			w.FromCanon = CanonImpact;
			w.Duration = RecoverSeconds() / tempo;
		}
	}

	private void TickRecover(Weapon w)
	{
		float t = Mathf.Clamp(w.T / w.Duration, 0f, 1f);
		float e = 1f - (1f - t) * (1f - t);
		Vector2 settle = w.FromPivot + new Vector2(0f, -0.35f * GripToHead);
		w.Pivot = w.FromPivot.Lerp(settle, e) + new Vector2(0f, -MathF.Sin(t * MathF.PI) * 6f);
		// Back up the way it came down, to hang head-up again.
		w.Angle = Mirror(Mathf.Lerp(CanonImpact, CanonIdle, e), w.Side);
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

	private Vector2 ImpactPivot(Vector2 target, float side) => target - Dir(Mirror(CanonImpact, side)) * GripToHead;

	private Vector2 HeadAt(Weapon w) => w.Pivot + Dir(w.Angle) * GripToHead;

	// Where the head was at progress p (eased, 0 to 1) through the last strike.
	private Vector2 SlashHead(Weapon w, float p) =>
		w.SlashFromPivot.Lerp(w.SlashToPivot, p) + Dir(Mirror(Mathf.Lerp(w.SlashFromCanon, CanonImpact, p), w.SlashSide)) * GripToHead;

	// The weapons at rest hang over the wizard's shoulders in a shallow arc, behind the way he faces.
	private Vector2 HeelSpot(int index, int count)
	{
		Vector2 player = PlayerRef.GlobalPosition;
		float back = -(PlayerRef.FacingSide);
		float spread = (index - (count - 1) * 0.5f);
		float a = Mathf.DegToRad(-90f + back * 38f + spread * 30f);
		float r = 46f + 6f * MathF.Abs(spread);
		// The grip hangs low and the head up, so the drawing sits over his shoulder rather than his head.
		return player + Dir(a) * r + new Vector2(0f, 18f);
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
	private float RaiseSeconds() => form switch { Form.Sword => 0.11f, Form.Hammer => 0.26f, _ => 0.30f } * (colossal ? 1.15f : 1f);
	private float StrikeSeconds() => form switch { Form.Sword => 0.06f, Form.Hammer => 0.08f, _ => 0.09f };
	private float RecoverSeconds() => form switch { Form.Sword => 0.08f, Form.Hammer => 0.22f, _ => 0.20f };

	private static bool Alive(Enemy e) =>
		e != null && IsInstanceValid(e) && !e.IsDying && e.InEnemyRegistry;

	// --- the quarry ----------------------------------------------------------------------------------------

	private Enemy Acquire(Weapon me, Vector2 player)
	{
		EnemyRegistry.CollectNear(player, huntRange, nearBuffer);
		Enemy best = null;
		float bestScore = float.MaxValue;
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
			if (score < bestScore)
			{
				bestScore = score;
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
		if (quarry == null)
		{
			// The quarry stepped out from under it: the nearest thing the blade came down on, if any.
			float best = (reach + RiptideWave.BodyAllowance) * (reach + RiptideWave.BodyAllowance);
			EnemyRegistry.CollectNear(at, reach + RiptideWave.BodyAllowance, nearBuffer);
			foreach (Enemy e in nearBuffer)
			{
				float d = e.GlobalPosition.DistanceSquaredTo(at);
				if (Alive(e) && d <= best)
				{
					best = d;
					quarry = e;
				}
			}
			nearBuffer.Clear();
		}
		if (quarry != null)
			Strike(quarry, dealt);
		if (alongBlade)
			StrikeAlongBlade(w, dealt, quarry);
		// A flash at the point and the light it throws, to the reach it tested.
		if (arena != null)
			ImpactDebrisLayer.Spawn(arena, at, MathF.Max(10f, reach * 0.7f), arcane, tint);
		Sparks(at, 6, arcane, 70f);
	}

	// Celestial Greatsword: everything along the blade as it comes down, as long as the blade is drawn
	// (from a third of the way up the blade to its point), the width of the blade plus a body's half-width.
	private void StrikeAlongBlade(Weapon w, int dealt, Enemy already)
	{
		Vector2 dir = Dir(w.Angle);
		Vector2 a = w.Pivot + dir * (GripToHead * 0.35f);
		Vector2 b = w.Pivot + dir * (GripToHead * 1.25f);
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
	// hanging at his shoulder, hunting or in the swing - is cut out of the air. A guard, not a wall: the
	// blades are where the swords are, and a volley from the other side gets through. Riposte widens the
	// guard and turns every parry into a strike.
	private void ParryBolts()
	{
		IReadOnlyList<EnemyProjectile> bolts = EnemyProjectile.Live;
		if (bolts.Count == 0)
			return;
		float guard = ParryHalfWidth * scale * (riposte ? RiposteGuard : 1f);
		for (int b = bolts.Count - 1; b >= 0; b--)
		{
			EnemyProjectile bolt = bolts[b];
			if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight)
				continue;
			Vector2 p = bolt.GlobalPosition;
			foreach (Weapon w in weapons)
			{
				if (w.Leaving)
					continue;
				Vector2 dir = Dir(w.Angle);
				Vector2 a = w.Pivot + dir * (GripToHead * 0.2f);
				Vector2 ab = dir * (GripToHead * 1.05f);
				float u = Mathf.Clamp((p - a).Dot(ab) / MathF.Max(1e-3f, ab.LengthSquared()), 0f, 1f);
				float reach = guard + bolt.Radius;
				if ((a + ab * u).DistanceSquaredTo(p) > reach * reach)
					continue;
				bolt.Destroy(Element.Arcane);
				StatBoltsParried++;
				Sparks(p, 5, light, 80f);
				if (riposte)
				{
					w.HasSwing = true;
					w.SwingDelay = 0f;
				}
				break;
			}
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
		slams[nextSlam] = new Slam { At = at, Radius = radius, Life = echo ? 0.45f : 0.7f, Seed = rng.Randi(), Live = true };
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

	private Color Tinted(Color c, float alpha) => new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha);

	private static Vector2 Snap(Vector2 p) => new Vector2(MathF.Round(p.X * 0.5f) * 2f, MathF.Round(p.Y * 0.5f) * 2f);

	// The floor: each weapon's shadow, a slam's cracks, hallowed ground.
	private void DrawGround()
	{
		Color occ = new Color(0.02f, 0.027f, 0.047f, 1f);
		foreach (Weapon w in weapons)
		{
			// The shadow shrinks and fades the higher the weapon is: that is what says it is in the air.
			Vector2 centre = w.Pivot + Dir(w.Angle) * GripToCentre;
			float height = MathF.Max(0f, w.Ground - centre.Y);
			float k = Mathf.Clamp(1f - height / 220f, 0.35f, 1f);
			float rx = (colossal ? 34f : 18f) * k;
			Color c = new Color(occ, 0.30f * k * w.Alpha);
			Vector2 at = Snap(new Vector2(centre.X, w.Ground));
			for (int row = -2; row <= 2; row += 2)
			{
				float f = 1f - MathF.Abs(row) / 3.2f;
				float half = MathF.Round(rx * f * 0.5f) * 2f;
				ground.DrawRect(new Rect2(at.X - half, at.Y + row - 1f, half * 2f, 2f), c);
			}
		}

		// A slam's cracks: jagged lines out from where it landed, to exactly the radius it hit, glowing and
		// then going dark. Deterministic from the slam's seed, so a crack does not crawl from frame to frame.
		for (int i = 0; i < MaxSlams; i++)
		{
			ref Slam s = ref slams[i];
			if (!s.Live)
				continue;
			float t = s.Age / s.Life;
			float grow = MathF.Min(1f, s.Age / 0.08f);
			float alpha = t < 0.5f ? 1f : (1f - t) / 0.5f;
			uint seed = s.Seed;
			int arms = 7;
			for (int a = 0; a < arms; a++)
			{
				float ang = (a + Hash01(ref seed) * 0.6f) * Mathf.Tau / arms;
				Vector2 p = s.At;
				int steps = 4;
				for (int k2 = 1; k2 <= steps; k2++)
				{
					float rr = s.Radius * grow * k2 / steps;
					float wob = (Hash01(ref seed) - 0.5f) * 0.5f;
					Vector2 q = s.At + new Vector2(MathF.Cos(ang + wob), MathF.Sin(ang + wob) * 0.62f) * rr;
					ground.DrawLine(Snap(p), Snap(q), new Color(occ, 0.75f * alpha), 4f);
					ground.DrawLine(Snap(p), Snap(q), Tinted(t < 0.35f ? arcane.Hot : arcane.Edge, alpha), 2f);
					p = q;
				}
			}
		}

		// Hallowed ground: a ring of light runes on the floor, inside the radius it weakens, fading as it ends.
		for (int i = 0; i < MaxZones; i++)
		{
			ref Zone z = ref zones[i];
			if (!z.Live)
				continue;
			float f = z.Left / MathF.Max(0.01f, z.Life);
			float alpha = MathF.Min(1f, f * 3f) * 0.85f;
			uint seed = z.Seed;
			int runes = 10;
			for (int r = 0; r < runes; r++)
			{
				float ang = r * Mathf.Tau / runes + Hash01(ref seed) * 0.3f;
				float rr = z.Radius * (0.55f + 0.35f * Hash01(ref seed));
				Vector2 p = z.At + new Vector2(MathF.Cos(ang) * rr, MathF.Sin(ang) * rr * 0.62f);
				Color c = Tinted(r % 2 == 0 ? light.Hot : light.Edge, alpha);
				ground.DrawRect(WhipLimb.Cell(p, 4f), c);
				ground.DrawRect(WhipLimb.Cell(p + new Vector2(4f, 0f), 2f), c);
				ground.DrawRect(WhipLimb.Cell(p + new Vector2(0f, -4f), 2f), c);
			}
		}
	}

	// The air: the weapons, their afterimages and slashes, and the motes.
	private void DrawAir()
	{
		float cellWorld = sheetCell * 2f;
		foreach (Weapon w in weapons)
		{
			if (sheet == null)
				break;
			int row = sheetRows > 1 && w.Side < 0f ? 1 : 0;
			// Afterimages through a strike: the last two headings, faint.
			if (w.Phase == Phase.Strike)
			{
				if (!float.IsNaN(w.Ghost2))
					DrawWeapon(w, w.Ghost2, row, cellWorld, Tinted(arcane.Mid, 0.22f * w.Alpha));
				if (!float.IsNaN(w.Ghost1))
					DrawWeapon(w, w.Ghost1, row, cellWorld, Tinted(arcane.Hot, 0.38f * w.Alpha));
			}
			DrawSlash(w);
			DrawWeapon(w, w.Angle, row, cellWorld, new Color(tint.R, tint.G, tint.B, w.Alpha));
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

	// The slash: the arc the head swept, from where it came over the top to where it is - thick and white
	// at the head, thinning and cooling down the ramp behind it - and, once the blow has landed, the whole
	// arc fading in a sixth of a second. Under the weapon, so the weapon leads it.
	private void DrawSlash(Weapon w)
	{
		if (w.SlashProgress <= 0f || w.SlashAge >= SlashLife)
			return;
		float fade = 1f - w.SlashAge / SlashLife;
		float span = form == Form.Sword ? 0.75f : 0.9f;
		float from = MathF.Max(0f, w.SlashProgress - span);
		float width = (form == Form.Sword ? 6f : 10f) * (colossal ? 2f : 1f);
		Vector2 prev = Snap(SlashHead(w, from));
		for (int k = 1; k <= SlashSamples; k++)
		{
			float f = k / (float)SlashSamples;
			Vector2 at = Snap(SlashHead(w, Mathf.Lerp(from, w.SlashProgress, f)));
			Color c = f > 0.8f ? arcane.Core : f > 0.5f ? arcane.Hot : f > 0.25f ? arcane.Mid : arcane.Edge;
			float alpha = fade * (0.35f + 0.65f * f);
			air.DrawLine(prev, at, Tinted(c, alpha), MathF.Max(2f, MathF.Round(width * (0.3f + 0.7f * f) * 0.5f) * 2f));
			prev = at;
		}
	}

	private void DrawWeapon(Weapon w, float angle, int row, float cellWorld, Color modulate)
	{
		int cell = ((int)MathF.Round(angle / Mathf.Tau * Headings) % Headings + Headings) % Headings;
		// The drawing's heading is the CELL's, so its centre is placed along the cell's heading, not the
		// exact angle: the grip then sits where the drawing's grip is.
		float drawn = cell * Mathf.Tau / Headings;
		Vector2 centre = Snap(w.Pivot + Dir(drawn) * GripToCentre);
		var dest = new Rect2(centre - new Vector2(cellWorld, cellWorld) * 0.5f, new Vector2(cellWorld, cellWorld));
		var src = new Rect2(cell * sheetCell, row * sheetCell, sheetCell, sheetCell);
		air.DrawTextureRectRegion(sheet, dest, src, modulate);
	}

	// A small deterministic hash for the cracks and runes: the same slam draws the same cracks every frame.
	private static float Hash01(ref uint state)
	{
		state ^= state << 13;
		state ^= state >> 17;
		state ^= state << 5;
		return (state & 0xFFFFFF) / (float)0x1000000;
	}
}
