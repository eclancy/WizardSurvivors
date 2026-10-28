using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Black Tentacles (Poison + Earth, issue #28): a rift tears open on the densest pack in range,
// tentacles burst up out of it, and for the rest of the cast they whip at whatever is inside.
//
// "black tentacles should have a summon animation, and the tentacles should whip around attacking
// enemies. the tentacle slaps do damage, the aoe slows them." So the work is split between the two
// halves of the spell, and neither does the other's job:
//
// - **The rift slows.** Everything inside CurrentRadius is slowed, re-applied every ScanSeconds
//   with a short linger (Enemy.ApplySlow is stacking-resistant: the stronger multiplier and the
//   longer remaining time win, nothing adds), so the slow holds exactly while an enemy stands in
//   the rift and wears off just after it steps out. The rift deals no damage of its own.
// - **The tentacles hurt.** Each picks the nearest enemy in the rift that no other tentacle is
//   already going for, winds up, lashes across to it, and lands: the target and anything within
//   ImpactRadius of the landing take the slap's damage. Then it recovers and picks again. The
//   slaps are staggered, so a rift of five tentacles is a steady drumming rather than a volley.
//
// Levels add tentacles, rift size and slow strength (SpellData_BlackTentacles.tres); Amount adds
// tentacles. Cast-to-cast: rift opens (OpenSeconds; the maw takes longer), holds for ZoneDuration,
// tentacles sink and the rift closes (CloseSeconds).
//
// **One radius.** CurrentRadius - the spell's radius times how far the rift has opened - is what
// the slow scan measures against and what the ground shader draws (tentacle_rift.gdshader), so the
// rift can never be drawn bigger than it slows. That was a live bug: Elder God Maw and Shadow
// Forest grew the DRAWING by their ScaleMultiplier (x2, x1.7) while their AreaMultiplier never
// reached the hit at all. Their area now reaches the hit (Player.FireBlackTentacles) and the node
// is never scaled; see CastAt.
//
// **Pixel art throughout.** The ground is a shader evaluated per 2 px art pixel; the tentacles are
// stamped onto the same grid by WhipLimb. Nothing is antialiased and every colour is a Bonelight
// tone: the darkness ramp for the tentacles (it is BLACK Tentacles), poison for their suckers and
// what they spatter, earth for the clods the rift throws up.
//
// Cost is flat in the swarm: one group scan per ScanSeconds (not per frame) fills a reused list of
// what is inside, and every slap picks from that list. Nothing is allocated per frame.
//
// **The Maw breaks up through the earth** (2026-10-28: "elder god maw summon animation should be
// cooler - it should look like it's breaking through the earth and throwing earth aside"). Its
// opening is one beat in three, as fractions of MawOpenSeconds: the ground cracks out from the centre
// to the edge of the slow and the middle of it heaves and trembles (shader, to MawBreach); it bursts,
// the heaved plates thrown aside as tumbling slabs and clods with dust blown out after them (here,
// DrawAir/DrawRubble); and the maw rises out of the hole it made, teeth lengthening out of the dark,
// tipping the broken ground up round it into the torn rim it keeps for the rest of its life. What is
// thrown lands ON that rim and lies there as rubble until the maw closes and takes it back down.
// The one-radius rule holds through all of it: every crack, slab, shadow and landed clod is inside
// CurrentRadius on the frame it is drawn. Only what is in the AIR is drawn above its spot on the
// ground (over the actors, AirZIndex) - and its shadow, on the ground, is inside the radius.
//
// **Two maws never overlap** ("they should never overlap"): every open maw is in liveMaws, and
// Player.FireBlackTentacles places a new one only where its full radius, plus MawGap, is clear of
// every live maw's full radius (MawFits) - the size each one is drawn to, held for its whole life,
// closing included. See Player.TryPlaceMaw for what it does when the pack is already under a maw.
public partial class BlackTentacles : Node2D
{
	private const int TentaclesBackZIndex = -50;
	// Over the bodies (0), so a slap visibly lands on its target; under the status marks (3).
	private const int ArmsZIndex = 1;
	public const int MaxTentacles = 12;
	private const float ScanSeconds = 0.15f;
	// How long the slow outlives a scan, so it never flickers between two of them.
	private const float SlowLinger = 0.25f;
	private const float RiseSeconds = 0.24f;
	private const float WindUpSeconds = 0.14f;
	private const float StrikeSeconds = 0.10f;
	private const float RecoverSeconds = 0.24f;
	private const float ImpactShowSeconds = 0.26f;
	private const int TrailPoints = 6;
	// Soul Drain: every SoulDrainEvery-th slap that lands heals 1 HP, at most SoulDrainCap a cast.
	// The player has 20 HP; a heal per slap would have been a tier-6 Grass bonus on a level 4 pick.
	public const int SoulDrainEvery = 4;
	public const int SoulDrainCap = 3;

	// --- Elder God Maw's summon. The fractions are shared with tentacle_rift.gdshader (BREACH, HOLE,
	// RIM, DOME) and must move with it.
	// Of MawOpenSeconds: the ground heaves until the breach (and the maw has risen by the shader's
	// RISEN, 0.85, which nothing here needs).
	private const float MawBreach = 0.30f;
	// Of the radius: where the teeth end and where the torn rim ends; the eyes have the rest.
	private const float MawHole = 0.74f;
	private const float MawRim = 0.86f;
	// Of the slow's reach at the moment: how far out the heaving dome reaches.
	private const float MawDome = 0.62f;
	/// <summary>Clear ground kept between two maws' rims, in px. Four art pixels, which also absorbs
	/// CastAt's snap to the art grid (at most 1.5 px), so two rims can never touch.</summary>
	public const float MawGap = 8f;
	private const int MaxSlabs = 18;
	private const int MaxClods = 26;
	private const int MaxDebris = MaxSlabs + MaxClods;
	private const int MaxDust = 20;
	private const float DustSeconds = 0.6f;
	private const float LandDustSeconds = 0.3f;
	// What is in the air passes over the bodies (0) and the arms (1); under the status marks (3).
	private const int AirZIndex = 2;

	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ZoneDuration { get; set; } = 3.0f;
	[Export] public float BaseRadius { get; set; } = 90f;
	// Speed multiplier inside the rift before level bonuses: 0.45 is a 55% slow.
	[Export] public float SlowMultiplier { get; set; } = 0.45f;
	// Seconds between one tentacle's slaps, before the player's cooldown reduction.
	[Export] public float SlapInterval { get; set; } = 0.6f;
	// What a slap hits besides its target: anything this close to where it lands.
	[Export] public float ImpactRadius { get; set; } = 40f;
	[Export] public float OpenSeconds { get; set; } = 0.35f;
	[Export] public float MawOpenSeconds { get; set; } = 0.8f;
	[Export] public float CloseSeconds { get; set; } = 0.4f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public float DamageMultiplier { get; set; } = 1.0f;
	// The player's area times the spell's evolutions' (Player.FireBlackTentacles).
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	// The player's Amount: more tentacles.
	public int TentacleBonus { get; set; }
	public Node2D PlayerRef;

	private enum Face { Pool, ElderGodMaw, ShadowForest }
	private enum Arm { Hidden, Rising, Idle, WindUp, Strike, Recover, Sinking }

	private Face face = Face.Pool;
	private int damage = 2;
	private float radius = 90f;
	private float duration = 3f;
	private int tentacles = 3;
	private float slapInterval = 0.8f;
	private float slowMultiplier = 0.45f;
	private float slowLinger = SlowLinger;
	private float rootSeconds;
	private bool soulDrain;
	private float vortexPull;

	private float age;
	private float scanTimer;
	private int slapsLanded;
	private int healed;
	private float seed;
	private bool cast;
	private readonly List<Enemy> inside = new List<Enemy>();

	private float OpenTime => face == Face.ElderGodMaw ? MawOpenSeconds : OpenSeconds;
	private float CloseAt => OpenTime + duration;

	/// <summary>How far the rift is open, 0..1: opening, held, closing.</summary>
	private float OpenAmount
	{
		get
		{
			// The Maw's cracks run out ahead of it, and the cracks are the edge of the slow, so its
			// ground tears open fast and slows down (half its reach by the breach) rather than easing in.
			if (age < OpenTime)
				return face == Face.ElderGodMaw ? MawOpenCurve(age / OpenTime) : Smooth(age / OpenTime);
			if (age < CloseAt)
				return 1f;
			return 1f - Smooth((age - CloseAt) / CloseSeconds);
		}
	}

	/// <summary>The one hit authority: what the slow measures against and what the rift is drawn to.</summary>
	public float CurrentRadius => radius * OpenAmount;

	/// <summary>Whether this cast is an Elder God Maw. Valid once SpellData is set and a level applied.</summary>
	public bool IsMaw => face == Face.ElderGodMaw;

	/// <summary>
	/// The furthest anything of this cast is drawn on the ground: the radius it opens to. Valid from
	/// SetSpellLevel, before the node is in the tree - Player.FireBlackTentacles places a maw with it.
	/// </summary>
	public float DrawnRadius => radius;

	// --- two maws never overlap -----------------------------------------------------------------

	// Every Elder God Maw in the world, from its cast until it leaves the tree. Static, because a maw
	// must know about every other maw whoever cast it; emptied as they free, so it cannot outlive a run.
	private static readonly List<BlackTentacles> liveMaws = new List<BlackTentacles>();
	private bool registeredMaw;

	public static int LiveMawCount => liveMaws.Count;

	/// <summary>
	/// Whether a maw of <paramref name="mawRadius"/> opened at <paramref name="at"/> would keep
	/// MawGap of clear ground from every live maw - measured by full radii, so a maw still opening
	/// or already closing holds its whole ground.
	/// </summary>
	public static bool MawFits(Vector2 at, float mawRadius)
	{
		foreach (BlackTentacles maw in liveMaws)
		{
			float clear = maw.radius + mawRadius + MawGap;
			if (at.DistanceSquaredTo(maw.GlobalPosition) < clear * clear)
				return false;
		}
		return true;
	}

	/// <summary>
	/// Moves <paramref name="at"/> straight out from the centre of each live maw it would overlap
	/// until the two rims are MawGap apart. False if it cannot be cleared: it sits on a maw's exact
	/// centre (no "out"), or clearing one maw puts it on another.
	/// </summary>
	public static bool PushClearOfMaws(ref Vector2 at, float mawRadius)
	{
		for (int pass = 0; pass <= liveMaws.Count; pass++)
		{
			BlackTentacles blocking = null;
			foreach (BlackTentacles maw in liveMaws)
			{
				float clear = maw.radius + mawRadius + MawGap;
				if (at.DistanceSquaredTo(maw.GlobalPosition) < clear * clear)
				{
					blocking = maw;
					break;
				}
			}
			if (blocking == null)
				return true;
			Vector2 away = at - blocking.GlobalPosition;
			if (away.LengthSquared() < 1f)
				return false;
			at = blocking.GlobalPosition + away.Normalized() * (blocking.radius + mawRadius + MawGap);
		}
		return MawFits(at, mawRadius);
	}

	/// <summary>
	/// The closest any two live maws' rims come, in px (negative is an overlap); float.MaxValue with
	/// fewer than two. For _UiShot, which prints it through a burst of maws.
	/// </summary>
	public static float ClosestMawGap()
	{
		float closest = float.MaxValue;
		for (int i = 0; i < liveMaws.Count; i++)
		{
			for (int j = i + 1; j < liveMaws.Count; j++)
			{
				float gap = liveMaws[i].GlobalPosition.DistanceTo(liveMaws[j].GlobalPosition) - liveMaws[i].radius - liveMaws[j].radius;
				closest = MathF.Min(closest, gap);
			}
		}
		return closest;
	}

	public override void _ExitTree()
	{
		if (registeredMaw)
		{
			liveMaws.Remove(this);
			registeredMaw = false;
		}
	}

	// --- the tentacles, as parallel arrays allocated once ---------------------------------------
	private readonly WhipLimb[] limbs = new WhipLimb[MaxTentacles];
	private readonly Arm[] state = new Arm[MaxTentacles];
	private readonly float[] stateAt = new float[MaxTentacles];
	private readonly Vector2[] rootPos = new Vector2[MaxTentacles];
	private readonly float[] restAngle = new float[MaxTentacles];
	private readonly float[] restLength = new float[MaxTentacles];
	private readonly float[] phase = new float[MaxTentacles];
	private readonly float[] side = new float[MaxTentacles];
	private readonly float[] emergeAt = new float[MaxTentacles];
	private readonly float[] nextSlapAt = new float[MaxTentacles];
	private readonly Enemy[] target = new Enemy[MaxTentacles];
	private readonly Vector2[] aimPos = new Vector2[MaxTentacles];
	private readonly float[] strikeAngle = new float[MaxTentacles];
	private readonly float[] strikeLength = new float[MaxTentacles];
	private readonly float[] fromAngle = new float[MaxTentacles];
	private readonly float[] fromLength = new float[MaxTentacles];
	private readonly float[] impactAt = new float[MaxTentacles];
	private readonly Vector2[] impactPos = new Vector2[MaxTentacles];
	private readonly Vector2[] trail = new Vector2[MaxTentacles * TrailPoints];
	private readonly int[] trailCount = new int[MaxTentacles];
	private readonly float[] poseAngle = new float[MaxTentacles];
	private readonly float[] poseLength = new float[MaxTentacles];

	// Shadow Forest's small rifts: xy centre, z radius, at full open.
	private const int ForestPools = 7;
	private readonly Vector4[] pools = new Vector4[ForestPools];

	// The Maw's thrown earth, laid out once a cast (LayOutDebris). Slabs first, then clods. Distances
	// are from the centre along debrisAngle; ages are this cast's.
	private int slabCount;
	private int debrisCount;
	private readonly float[] debrisAngle = new float[MaxDebris];
	private readonly float[] debrisFrom = new float[MaxDebris];
	private readonly float[] debrisTo = new float[MaxDebris];
	private readonly float[] debrisLaunch = new float[MaxDebris];
	private readonly float[] debrisFlight = new float[MaxDebris];
	private readonly float[] debrisHeight = new float[MaxDebris];
	private readonly float[] debrisSpin = new float[MaxDebris];
	private readonly Vector2[] debrisSize = new Vector2[MaxDebris];
	// Below this OpenAmount, while closing, the piece has gone back down with the ground.
	private readonly float[] debrisSink = new float[MaxDebris];
	private int dustCount;
	private readonly float[] dustAngle = new float[MaxDust];
	private readonly float[] dustFrom = new float[MaxDust];
	private readonly float[] dustTo = new float[MaxDust];
	private readonly float[] dustSize = new float[MaxDust];
	private readonly float[] dustDelay = new float[MaxDust];
	// When the last of it has landed and its dust has settled: the air is empty and the rubble still.
	private float debrisSettledAt;

	private Node2D ground;
	private Node2D arms;
	// The Maw only: what is in the air (over the actors), and on the ground what has landed plus the
	// shadows of what has not (with the rift, under them).
	private Node2D air;
	private Node2D rubble;
	private ShaderMaterial groundMaterial;
	private static Shader riftShader;
	private static readonly StringName ParamRadius = "radius";
	private static readonly StringName ParamOpen = "open_amount";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamFace = "face";
	private static readonly StringName ParamPools = "pools";
	private static readonly StringName ParamPoolCount = "pool_count";
	private static readonly StringName ParamLook = "look";
	private static readonly StringName ParamMawAge = "maw_age";
	private static readonly StringName ParamGilded = "gilded";
	private static readonly StringName ParamSummon = "summon";

	// The darkness ramp and the robe violet row for the limbs, poison for the suckers and the
	// spatter, from .ai/art-direction.md section 3; occlusion for the outline.
	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);
	private static readonly Color DarkEdge = new Color(0.208f, 0.125f, 0.361f, 1f);
	private static readonly Color DarkMid = new Color(0.541f, 0.361f, 0.769f, 1f);
	private static readonly Color DarkHot = new Color(0.706f, 0.549f, 0.910f, 1f);
	private static readonly Color RobeLit = new Color(0.306f, 0.267f, 0.439f, 1f);
	private static readonly Color PoisonEdge = new Color(0.420f, 0.580f, 0.125f, 1f);
	private static readonly Color PoisonMid = new Color(0.706f, 0.878f, 0.290f, 1f);
	private static readonly Color PoisonHot = new Color(0.863f, 0.957f, 0.549f, 1f);
	private static readonly Color GoldLit = new Color(0.847f, 0.690f, 0.290f, 1f);
	// The thrown earth: the earth ramp lit, and below its edge the gold row's shade and deep, which
	// is what the Gravewell's bank is made of - the one dark soil the contract holds.
	private static readonly Color EarthHot = new Color(0.910f, 0.769f, 0.549f, 1f);
	private static readonly Color EarthMid = new Color(0.788f, 0.604f, 0.369f, 1f);
	private static readonly Color EarthEdge = new Color(0.478f, 0.314f, 0.141f, 1f);
	private static readonly Color SoilShade = new Color(0.329f, 0.247f, 0.063f, 1f);
	private static readonly Color SoilDeep = new Color(0.180f, 0.133f, 0.031f, 1f);

	public override void _Ready()
	{
		// Under every body regardless of insertion order: it is a hole in the ground.
		ZAsRelative = false;
		ZIndex = TentaclesBackZIndex;

		riftShader ??= GD.Load<Shader>("res://scenes/shaders/tentacle_rift.gdshader");
		groundMaterial = new ShaderMaterial { Shader = riftShader };
		ground = new Node2D { Name = "Rift", ShowBehindParent = true, Material = groundMaterial };
		ground.Draw += DrawGroundQuad;
		AddChild(ground);
		arms = new Node2D { Name = "Arms", ZAsRelative = false, ZIndex = ArmsZIndex };
		arms.Draw += DrawArms;
		AddChild(arms);

		for (int i = 0; i < MaxTentacles; i++)
			limbs[i] ??= new WhipLimb();

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		seed = rng.RandfRange(0f, Mathf.Tau);

		RefreshComputedStats();
	}

	// The ascension decides what the ground is, and the two level 8 branches are different enough
	// that they had to: "Elder God Maw" promising a maw and then drawing the same rift as every
	// other cast would be the card lying about what it bought.
	private void RefreshFace()
	{
		string tag = SpellData?.GetVisualTag() ?? string.Empty;
		face = tag switch
		{
			"ElderGodMaw" => Face.ElderGodMaw,
			"ShadowForest" => Face.ShadowForest,
			_ => Face.Pool,
		};
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void CastAt(Vector2 position)
	{
		// The art grid: the shader and the stamped limbs both count 2 px cells from this origin.
		GlobalPosition = new Vector2(MathF.Round(position.X / WhipLimb.Grid) * WhipLimb.Grid,
			MathF.Round(position.Y / WhipLimb.Grid) * WhipLimb.Grid);
		// This spell draws every one of its faces itself, at the size it hits. An evolution's
		// ScaleMultiplier would only grow the drawing, and its tint is a stand-in for spells that
		// have no art of their own - so both are dropped here, and a legendary roll gilds the lip.
		Scale = Vector2.One;
		Modulate = Colors.White;

		age = 0f;
		scanTimer = 0f;
		slapsLanded = 0;
		healed = 0;
		inside.Clear();
		cast = true;
		LayOutTentacles();
		if (face == Face.ElderGodMaw)
		{
			if (!registeredMaw)
			{
				// Placement is Player.TryPlaceMaw's; this only says so loudly if anything ever skips it.
				if (!MawFits(GlobalPosition, radius - MawGap))
					GD.PushError($"BlackTentacles: an Elder God Maw opened over another at {GlobalPosition} (radius {radius})");
				liveMaws.Add(this);
				registeredMaw = true;
			}
			if (air == null)
			{
				// Rubble draws after the rift in its own z (-50), so on the ground and under every body;
				// the air is absolute, over them.
				rubble = new Node2D { Name = "Rubble" };
				rubble.Draw += DrawRubble;
				AddChild(rubble);
				air = new Node2D { Name = "Air", ZAsRelative = false, ZIndex = AirZIndex };
				air.Draw += DrawAir;
				AddChild(air);
			}
			LayOutDebris();
		}

		groundMaterial.SetShaderParameter(ParamRadius, radius);
		groundMaterial.SetShaderParameter(ParamSeed, seed);
		groundMaterial.SetShaderParameter(ParamFace, (int)face);
		groundMaterial.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
		groundMaterial.SetShaderParameter(ParamPools, pools);
		groundMaterial.SetShaderParameter(ParamPoolCount, face == Face.ShadowForest ? ForestPools : 0);
		UpdateGround();
		ground.QueueRedraw();

		SfxPlayer.AtPosition(SfxCatalog.SpellSummon, GlobalPosition);
	}

	public override void _Process(double delta)
	{
		if (!cast)
			return;
		float dt = (float)delta;
		age += dt;
		if (age >= CloseAt + CloseSeconds)
		{
			QueueFree();
			return;
		}

		scanTimer -= dt;
		if (scanTimer <= 0f)
		{
			scanTimer = ScanSeconds;
			Scan();
		}

		for (int i = 0; i < tentacles; i++)
			UpdateArm(i);
		UpdateGround();
		arms.QueueRedraw();
		if (air != null)
		{
			// Only while something moves: in the hold the rubble lies still and the air is empty, so
			// the last frame drawn stands (one more redraw after it settles, to clear the air).
			bool settling = age < debrisSettledAt + 0.05f;
			if (settling)
				air.QueueRedraw();
			if (settling || age >= CloseAt)
				rubble.QueueRedraw();
		}
	}

	// --- the rift: slow, and the Maw's pull -----------------------------------------------------

	private void Scan()
	{
		inside.Clear();
		float r = CurrentRadius;
		if (r < 1f)
			return;
		float rangeSquared = r * r;
		Vector2 centre = GlobalPosition;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = enemy.GlobalPosition - centre;
			float distanceSquared = offset.LengthSquared();
			if (distanceSquared > rangeSquared)
				continue;
			inside.Add(enemy);
			enemy.ApplySlow(slowMultiplier, ScanSeconds + slowLinger);
			// Elder God Maw drags what is inside toward its teeth. ApplyKnockback, as Hollow Star's
			// pull, so boss knockback resistance applies; eased near the centre so nothing jitters.
			if (vortexPull > 0f && distanceSquared > 64f)
			{
				float distance = MathF.Sqrt(distanceSquared);
				enemy.ApplyKnockback(-offset / distance * MathF.Min(vortexPull, distance * 3f));
			}
		}
	}

	// --- the tentacles -------------------------------------------------------------------------

	private void LayOutTentacles()
	{
		var rng = new RandomNumberGenerator { Seed = GetInstanceId() };
		if (face == Face.ShadowForest)
		{
			// Pushed toward the rim and alternated so seven small rifts read as a scatter, not a lump -
			// and the outer three have their lips on the radius, because with no disc drawn under them
			// (2026-10-23) the rifts themselves are how far the slow reaches.
			pools[0] = new Vector4(0f, 0f, radius * 0.30f, 0f);
			for (int p = 1; p < ForestPools; p++)
			{
				float a = seed + p * Mathf.Tau / (ForestPools - 1);
				bool outer = p % 2 == 0;
				float ring = radius * (outer ? 0.70f : 0.48f);
				pools[p] = new Vector4(MathF.Cos(a) * ring, MathF.Sin(a) * ring, radius * (outer ? 0.30f : 0.24f), 0f);
			}
		}

		float rest = Mathf.Clamp(radius * 0.55f, 30f, 90f);
		for (int i = 0; i < MaxTentacles; i++)
		{
			float a = seed + i * Mathf.Tau / Math.Max(1, tentacles) + rng.RandfRange(-0.25f, 0.25f);
			Vector2 outward = new Vector2(MathF.Cos(a), MathF.Sin(a));
			switch (face)
			{
				case Face.ElderGodMaw:
					// On the lip, hanging in over the teeth.
					rootPos[i] = outward * radius * 0.78f;
					restAngle[i] = a + Mathf.Pi + rng.RandfRange(-0.35f, 0.35f);
					restLength[i] = Mathf.Clamp(radius * 0.30f, 26f, 80f);
					break;
				case Face.ShadowForest:
					Vector4 pool = pools[i % ForestPools];
					rootPos[i] = new Vector2(pool.X, pool.Y) + outward * pool.Z * 0.3f;
					restAngle[i] = a;
					restLength[i] = Mathf.Clamp(radius * 0.26f, 20f, 60f);
					break;
				default:
					rootPos[i] = outward * radius * 0.38f;
					restAngle[i] = a + rng.RandfRange(-0.3f, 0.3f);
					restLength[i] = rest;
					break;
			}
			phase[i] = rng.Randf() * Mathf.Tau;
			side[i] = i % 2 == 0 ? 1f : -1f;
			// Burst up once the ground is mostly open, one after another.
			emergeAt[i] = OpenTime * 0.55f + i * 0.045f;
			nextSlapAt[i] = OpenTime + 0.05f + i * slapInterval / Math.Max(1, tentacles);
			state[i] = Arm.Hidden;
			target[i] = null;
			impactAt[i] = -10f;
			trailCount[i] = 0;
		}
	}

	// Where tentacle i comes out of the ground this frame. Closing, the ground goes back in and the
	// roots with it - held where they were laid out, the Maw's (on its lip) stood on bare ground
	// outside the shrinking rift for most of a fifth of a second, drawn bigger than it slowed.
	private Vector2 RootOf(int i) => age >= CloseAt ? rootPos[i] * OpenAmount : rootPos[i];

	private void Enter(int i, Arm next)
	{
		// Every pose is a blend from the one it left, so record where it was.
		fromAngle[i] = poseAngle[i];
		fromLength[i] = poseLength[i];
		state[i] = next;
		stateAt[i] = age;
	}

	private void UpdateArm(int i)
	{
		float t = age - stateAt[i];
		bool closing = age >= CloseAt;
		switch (state[i])
		{
			case Arm.Hidden:
				if (age >= emergeAt[i] && !closing)
					Enter(i, Arm.Rising);
				break;
			case Arm.Rising:
				if (closing) Enter(i, Arm.Sinking);
				else if (t >= RiseSeconds) Enter(i, Arm.Idle);
				break;
			case Arm.Idle:
				if (closing) Enter(i, Arm.Sinking);
				else if (age >= nextSlapAt[i] && age < CloseAt - WindUpSeconds - StrikeSeconds)
					TryBeginSlap(i);
				break;
			case Arm.WindUp:
				Track(i);
				if (t >= WindUpSeconds) { Enter(i, Arm.Strike); trailCount[i] = 0; }
				break;
			case Arm.Strike:
				Track(i);
				if (t >= StrikeSeconds)
				{
					Pose(i, 1f);
					Land(i);
					Enter(i, Arm.Recover);
					return;
				}
				break;
			case Arm.Recover:
				if (t >= RecoverSeconds) Enter(i, closing ? Arm.Sinking : Arm.Idle);
				break;
		}
		Pose(i, -1f);
	}

	// Follow the target while winding up and lashing, so the slap lands where it is now.
	private void Track(int i)
	{
		Enemy e = target[i];
		if (e != null && IsInstanceValid(e) && !e.IsDying)
			aimPos[i] = e.GlobalPosition - GlobalPosition;
	}

	private void TryBeginSlap(int i)
	{
		Vector2 from = GlobalPosition + RootOf(i);
		Enemy best = null;
		float bestDistance = float.MaxValue;
		bool bestClaimed = true;
		foreach (Enemy e in inside)
		{
			if (!IsInstanceValid(e) || e.IsDying)
				continue;
			bool claimed = IsClaimed(e, i);
			float d = from.DistanceSquaredTo(e.GlobalPosition);
			// Something nobody is already lashing at beats something closer that somebody is.
			if (best == null || (bestClaimed && !claimed) || (claimed == bestClaimed && d < bestDistance))
			{
				best = e;
				bestDistance = d;
				bestClaimed = claimed;
			}
		}
		if (best == null)
		{
			nextSlapAt[i] = age + ScanSeconds;
			return;
		}
		target[i] = best;
		aimPos[i] = best.GlobalPosition - GlobalPosition;
		side[i] = -side[i];
		nextSlapAt[i] = age + slapInterval;
		Enter(i, Arm.WindUp);
	}

	private bool IsClaimed(Enemy e, int self)
	{
		for (int j = 0; j < tentacles; j++)
		{
			if (j != self && target[j] == e && (state[j] == Arm.WindUp || state[j] == Arm.Strike))
				return true;
		}
		return false;
	}

	// The slap lands: the target, and anything within ImpactRadius of where it came down.
	private void Land(int i)
	{
		impactAt[i] = age;
		impactPos[i] = aimPos[i];
		strikeAngle[i] = poseAngle[i];
		strikeLength[i] = poseLength[i];
		Enemy struck = target[i];
		target[i] = null;
		if (PlayerRef is not Player player || !IsInstanceValid(player))
			return;

		Vector2 at = GlobalPosition + aimPos[i];
		bool landed = false;
		if (struck != null && IsInstanceValid(struck) && !struck.IsDying)
		{
			player.DealDamageToEnemy(struck, damage, source: SpellData);
			// Crushing Grip: the tentacle holds what it hits.
			if (rootSeconds > 0f && IsInstanceValid(struck) && !struck.IsDying)
				struck.ApplySlow(0f, rootSeconds);
			landed = true;
		}
		float splashSquared = ImpactRadius * ImpactRadius;
		foreach (Enemy e in inside)
		{
			if (e == struck || !IsInstanceValid(e) || e.IsDying)
				continue;
			if (e.GlobalPosition.DistanceSquaredTo(at) > splashSquared)
				continue;
			player.DealDamageToEnemy(e, damage, source: SpellData);
			landed = true;
		}

		if (!landed)
			return;
		slapsLanded++;
		if (soulDrain && healed < SoulDrainCap && slapsLanded % SoulDrainEvery == 0)
		{
			player.Heal(1);
			healed++;
		}
	}

	// Where tentacle i is this frame: its heading, length, how it curls and how a wave runs down
	// it, laid out into its WhipLimb. `strikeEnd` >= 0 forces the strike's final pose, so the tip
	// sits exactly on the target on the frame the damage lands.
	private void Pose(int i, float strikeEnd)
	{
		float t = age - stateAt[i];
		float sway = 0.28f * MathF.Sin(age * 1.3f + phase[i]);
		float idleAngle = restAngle[i] + sway;
		float idleCurl = 1.3f * MathF.Sin(age * 0.9f + phase[i] * 1.7f);
		float idleLength = restLength[i] * (0.88f + 0.12f * MathF.Sin(age * 1.1f + phase[i]));
		Vector2 toAim = aimPos[i] - RootOf(i);
		float aimAngle = toAim.Angle();
		float reach = MathF.Max(8f, toAim.Length());
		float wave = age * 3f + phase[i];

		float angle, length, curl, waveAmp;
		switch (state[i])
		{
			case Arm.Hidden:
				poseLength[i] = 0f;
				poseAngle[i] = idleAngle;
				return;
			case Arm.Rising:
			{
				// Bursts up past its rest length and settles: the whip of breaking ground.
				float u = Mathf.Clamp(t / RiseSeconds, 0f, 1f);
				angle = idleAngle;
				length = idleLength * EaseOutBack(u);
				curl = idleCurl * u + side[i] * 1.2f * (1f - u);
				waveAmp = 0.5f;
				break;
			}
			case Arm.WindUp:
			{
				// Draws back across the target, coiling.
				float u = Smooth(t / WindUpSeconds);
				angle = Mathf.LerpAngle(fromAngle[i], aimAngle - side[i] * 0.9f, u);
				length = Mathf.Lerp(fromLength[i], reach * 0.55f, u);
				curl = Mathf.Lerp(idleCurl, -side[i] * 1.6f, u);
				waveAmp = 0.2f * (1f - u);
				break;
			}
			case Arm.Strike:
			{
				// The lash: the base swings onto the target first and the tip follows, uncoiling.
				float u = strikeEnd >= 0f ? 1f : Mathf.Clamp(t / StrikeSeconds, 0f, 1f);
				float e = 1f - (1f - u) * (1f - u);
				angle = Mathf.LerpAngle(aimAngle - side[i] * 0.9f, aimAngle, e);
				length = Mathf.Lerp(reach * 0.55f, reach, e);
				curl = -side[i] * 1.6f * (1f - u) * (1f - u) + side[i] * 0.5f * MathF.Sin(Mathf.Pi * u) * (1f - u);
				waveAmp = 0.25f * (1f - u);
				break;
			}
			case Arm.Recover:
			{
				float u = Smooth(t / RecoverSeconds);
				angle = Mathf.LerpAngle(strikeAngle[i], idleAngle, u);
				length = Mathf.Lerp(strikeLength[i], idleLength, u);
				curl = idleCurl * u;
				waveAmp = 0.35f * u;
				break;
			}
			case Arm.Sinking:
			{
				// Pulled back down, writhing.
				float u = Smooth(t / (CloseSeconds * 0.8f));
				angle = fromAngle[i] + sway;
				length = fromLength[i] * (1f - u);
				curl = idleCurl + side[i] * 1.4f * u;
				waveAmp = 0.5f;
				break;
			}
			default:
				angle = idleAngle;
				length = idleLength;
				curl = idleCurl;
				waveAmp = 0.35f;
				break;
		}

		poseAngle[i] = angle;
		poseLength[i] = length;
		limbs[i].Layout(RootOf(i), angle, length, curl, waveAmp, wave);
		if (state[i] == Arm.Strike && trailCount[i] < TrailPoints)
			trail[i * TrailPoints + trailCount[i]++] = limbs[i].Tip;
	}

	// --- drawing -------------------------------------------------------------------------------

	private void UpdateGround()
	{
		groundMaterial.SetShaderParameter(ParamOpen, OpenAmount);
		groundMaterial.SetShaderParameter(ParamTime, age);
		if (face == Face.ElderGodMaw)
		{
			groundMaterial.SetShaderParameter(ParamSummon, Mathf.Clamp(age / OpenTime, 0f, 1f));
			groundMaterial.SetShaderParameter(ParamMawAge, MathF.Max(0f, age - OpenTime));
			if (PlayerRef != null && IsInstanceValid(PlayerRef))
			{
				Vector2 look = PlayerRef.GlobalPosition - GlobalPosition;
				groundMaterial.SetShaderParameter(ParamLook, look.LengthSquared() > 1f ? look.Normalized() : Vector2.Down);
			}
		}
	}

	// One quad over the whole rift; the shader decides every pixel of it.
	private void DrawGroundQuad()
	{
		float half = radius + 16f;
		ground.DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), Colors.White);
	}

	private void DrawArms()
	{
		if (!cast)
			return;
		bool gilded = SpellData?.IsLegendary ?? false;
		// Thick enough to read against an 86 px skeleton: at 6 px they vanished into the swarm.
		float baseWidth = WhipLimb.SnapSize(Mathf.Clamp(radius * 0.12f, 8f, 16f));
		for (int i = 0; i < tentacles; i++)
		{
			DrawImpact(i);
			if (state[i] == Arm.Hidden || poseLength[i] < WhipLimb.Grid * 2f)
				continue;
			bool striking = state[i] == Arm.Strike || state[i] == Arm.WindUp;
			DrawTrail(i);

			// Where it comes out of the ground: a lip of shadow round the root.
			arms.DrawRect(WhipLimb.Cell(RootOf(i), baseWidth + 4f), Occ);
			// Near-black at the root, violet toward the tip, with the key light along one edge: the
			// light is what draws a black limb on a dark swarm, not the fill.
			limbs[i].Draw(arms, baseWidth, WhipLimb.Grid * 2f, Occ, DarkEdge, RobeLit, gilded ? GoldLit : DarkHot, taper: 1.7f);

			// Suckers down one side, rot-green; brighter while it is lashing.
			for (float s = 0.3f; s < 0.85f; s += 0.13f)
			{
				Vector2 dir = limbs[i].DirectionAt(s);
				Vector2 normal = new Vector2(-dir.Y, dir.X) * side[i];
				float w = WhipLimb.Grid * 2f + (baseWidth - WhipLimb.Grid * 2f) * MathF.Pow(1f - s, 1.7f);
				arms.DrawRect(WhipLimb.Cell(limbs[i].At(s) + normal * MathF.Max(0f, w * 0.5f - WhipLimb.Grid), WhipLimb.Grid),
					striking ? PoisonMid : PoisonEdge);
			}
			arms.DrawRect(WhipLimb.Cell(limbs[i].Tip, WhipLimb.Grid), striking ? PoisonHot : PoisonMid);
		}
	}

	// The whip arc: where the tip has been during the lash, fading out just after it lands.
	private void DrawTrail(int i)
	{
		int n = trailCount[i];
		if (n < 2)
			return;
		float since = age - impactAt[i];
		bool fresh = state[i] == Arm.Strike || since < 0.12f;
		if (!fresh)
			return;
		Color tone = state[i] == Arm.Strike ? DarkMid : new Color(DarkMid, 1f - since / 0.12f);
		for (int k = 1; k < n; k++)
		{
			Vector2 a = trail[i * TrailPoints + k - 1];
			Vector2 b = trail[i * TrailPoints + k];
			float steps = MathF.Max(1f, a.DistanceTo(b) / WhipLimb.Grid);
			for (float s = 0f; s < steps; s += 1f)
				arms.DrawRect(WhipLimb.Cell(a.Lerp(b, s / steps), WhipLimb.Grid), tone);
		}
	}

	// The slap landing: a ring that opens to exactly ImpactRadius - what it hit besides its target -
	// and rot spattered inside it.
	private void DrawImpact(int i)
	{
		float since = age - impactAt[i];
		if (since < 0f || since > ImpactShowSeconds)
			return;
		float u = since / ImpactShowSeconds;
		Color ring = u < 0.3f ? PoisonHot : (u < 0.65f ? PoisonMid : PoisonEdge);
		float r = ImpactRadius * (0.4f + 0.6f * MathF.Sqrt(u));
		const int RingCells = 14;
		for (int k = 0; k < RingCells; k++)
		{
			float a = k * Mathf.Tau / RingCells + phase[i];
			arms.DrawRect(WhipLimb.Cell(impactPos[i] + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r, WhipLimb.Grid), ring);
		}
		if (u < 0.35f)
			arms.DrawRect(WhipLimb.Cell(impactPos[i], WhipLimb.Grid * 3f), Occ);
		for (int k = 0; k < 6; k++)
		{
			float a = phase[i] * 2.3f + k * 1.9f;
			float d = ImpactRadius * MathF.Min(1f, (0.3f + 0.12f * k) * (0.5f + u));
			arms.DrawRect(WhipLimb.Cell(impactPos[i] + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, WhipLimb.Grid),
				k % 2 == 0 ? PoisonMid : PoisonEdge);
		}
	}

	// --- the Maw's summon: the earth it throws aside ---------------------------------------------

	// Laid out once a cast. Slabs are the dome's heaved plates (the shader's, at the breach), thrown
	// aside onto the torn rim; clods are its crumbs, flung higher and further, out over the eyes. Every
	// one comes down inside the radius. Dust is blown out after them along the ground.
	private void LayOutDebris()
	{
		var rng = new RandomNumberGenerator { Seed = GetInstanceId() * 2654435761UL + 17UL };
		float breachAt = MawBreach * MawOpenSeconds;
		float dome = radius * MawOpenCurve(MawBreach) * MawDome;
		slabCount = Math.Clamp((int)(radius / 11f), 10, MaxSlabs);
		int clods = Math.Clamp((int)(radius / 7f), 14, MaxClods);
		debrisCount = slabCount + clods;
		debrisSettledAt = breachAt + 0.08f + DustSeconds;
		for (int i = 0; i < debrisCount; i++)
		{
			bool slab = i < slabCount;
			int n = slab ? slabCount : clods;
			int k = slab ? i : i - slabCount;
			// Spread round the turn, clods between the slabs.
			debrisAngle[i] = seed + (k + (slab ? 0f : 0.5f) + rng.RandfRange(-0.35f, 0.35f)) * Mathf.Tau / n;
			float w = WhipLimb.SnapSize(slab ? rng.RandfRange(10f, 22f) : rng.RandfRange(2f, 5f));
			float h = slab ? WhipLimb.SnapSize(w * rng.RandfRange(0.5f, 0.8f)) : w;
			debrisSize[i] = new Vector2(w, h);
			debrisFrom[i] = dome * rng.RandfRange(0.15f, 0.85f);
			// Clear of the teeth, and with all of it - corners, contact shadow - inside the radius.
			float near = radius * (MawHole + 0.02f) + MathF.Min(w, h) * 0.5f;
			float far = MathF.Min(slab ? radius * (MawRim + 0.03f) : radius, radius - DebrisMargin(i));
			debrisTo[i] = rng.RandfRange(MathF.Min(near, far), far);
			debrisLaunch[i] = breachAt + rng.RandfRange(0f, 0.05f);
			debrisFlight[i] = slab ? rng.RandfRange(0.36f, 0.5f) : rng.RandfRange(0.3f, 0.55f);
			debrisHeight[i] = slab ? rng.RandfRange(20f, 40f) : rng.RandfRange(26f, 54f);
			debrisSpin[i] = rng.RandfRange(1.5f, 4f) * (rng.Randf() < 0.5f ? -1f : 1f);
			debrisSink[i] = rng.RandfRange(0.3f, 0.9f);
			debrisSettledAt = MathF.Max(debrisSettledAt, debrisLaunch[i] + debrisFlight[i] + LandDustSeconds);
		}
		// Blown out of the breach past the lip, over the rim it is tearing up.
		dustCount = Math.Clamp((int)(radius / 13f), 10, MaxDust);
		for (int i = 0; i < dustCount; i++)
		{
			dustAngle[i] = seed * 1.7f + (i + rng.RandfRange(-0.3f, 0.3f)) * Mathf.Tau / dustCount;
			dustSize[i] = rng.RandfRange(7f, 13f);
			dustFrom[i] = dome * rng.RandfRange(0.5f, 1f);
			dustTo[i] = radius * rng.RandfRange(MawHole + 0.04f, 0.95f) - dustSize[i];
			dustDelay[i] = rng.RandfRange(0f, 0.08f);
		}
	}

	// Where piece i is this frame: its spot on the ground, how high above it, and whether it is down.
	// False before it is thrown, and once the closing ground has taken it.
	private bool DebrisAt(int i, out Vector2 spot, out float height, out bool landed)
	{
		spot = Vector2.Zero;
		height = 0f;
		landed = false;
		float since = age - debrisLaunch[i];
		if (since < 0f)
			return false;
		float u = since / debrisFlight[i];
		landed = u >= 1f;
		float distance = landed ? debrisTo[i] : Mathf.Lerp(debrisFrom[i], debrisTo[i], u);
		height = landed ? 0f : debrisHeight[i] * 4f * u * (1f - u);
		float margin = DebrisMargin(i);
		float open = OpenAmount;
		if (age >= CloseAt)
		{
			// Closing, the ground goes back in and takes the rubble with it, edge first.
			distance *= open;
			if (open < debrisSink[i] || distance + margin > radius * open)
				return false;
		}
		else
		{
			// The one radius: where it is on the ground (its shadow, or where it lies) is inside the slow.
			distance = MathF.Max(0f, MathF.Min(distance, radius * open - margin));
		}
		spot = new Vector2(MathF.Cos(debrisAngle[i]), MathF.Sin(debrisAngle[i])) * distance;
		return true;
	}

	// How far past its spot piece i can reach on the ground: its half-diagonal, its contact shadow one
	// art pixel down and right, and the snap to the art grid.
	private float DebrisMargin(int i) => debrisSize[i].Length() * 0.5f + WhipLimb.Grid * 2.5f;

	// Over the actors: the slabs and clods in flight, and the dust.
	private void DrawAir()
	{
		if (!cast || face != Face.ElderGodMaw)
			return;
		for (int i = 0; i < debrisCount; i++)
		{
			if (!DebrisAt(i, out Vector2 spot, out float height, out bool landed) || landed)
				continue;
			Vector2 at = spot + new Vector2(0f, -height);
			if (i >= slabCount)
			{
				air.DrawRect(Box(at, debrisSize[i]), i % 4 == 0 ? EarthHot : (i % 2 == 0 ? EarthMid : EarthEdge));
				continue;
			}
			// A slab turning over as it flies: its top, edge on, its torn underside, edge on.
			float turn = (age - debrisLaunch[i]) * debrisSpin[i] * MathF.PI;
			float c = MathF.Cos(turn);
			if (MathF.Abs(c) < 0.38f)
				DrawChunk(air, at, new Vector2(debrisSize[i].X, WhipLimb.Grid * 2f), SoilShade, EarthMid, SoilDeep, false);
			else if (c > 0f)
				DrawChunk(air, at, debrisSize[i], EarthEdge, EarthMid, SoilShade, true);
			else
				DrawChunk(air, at, debrisSize[i], SoilShade, EarthEdge, SoilDeep, false);
		}
		DrawDust();
	}

	// On the ground, under the actors: the shadow of each slab still in the air, and what has landed.
	private void DrawRubble()
	{
		if (!cast || face != Face.ElderGodMaw)
			return;
		float g = WhipLimb.Grid;
		for (int i = 0; i < debrisCount; i++)
		{
			if (!DebrisAt(i, out Vector2 spot, out float height, out bool landed))
				continue;
			Vector2 size = debrisSize[i];
			if (!landed)
			{
				// Smaller the higher it is: where it is going to come down.
				if (i < slabCount)
				{
					float s = 1f - 0.45f * height / debrisHeight[i];
					rubble.DrawRect(Box(spot, new Vector2(WhipLimb.SnapSize(size.X * s), WhipLimb.SnapSize(size.Y * 0.6f * s))), Occ);
				}
				continue;
			}
			// Down: its contact shadow one art pixel to the lower right, then the piece lying on it.
			rubble.DrawRect(Box(spot + new Vector2(g, g), size), Occ);
			if (i < slabCount)
				DrawChunk(rubble, spot, size, i % 2 == 0 ? EarthEdge : SoilShade, i % 2 == 0 ? EarthMid : EarthEdge, SoilDeep, i % 3 == 0);
			else
				rubble.DrawRect(Box(spot, size), i % 2 == 0 ? EarthEdge : SoilShade);
		}
	}

	// The dust: a ring of it blown out of the breach along the ground (never further than the slow
	// reaches), rising as it thins; and a puff either side of every slab where it lands.
	private void DrawDust()
	{
		float breachAt = MawBreach * MawOpenSeconds;
		float open = OpenAmount;
		for (int i = 0; i < dustCount; i++)
		{
			float since = age - breachAt - dustDelay[i];
			if (since < 0f || since >= DustSeconds)
				continue;
			float u = since / DustSeconds;
			float eased = 1f - (1f - u) * (1f - u);
			var dir = new Vector2(MathF.Cos(dustAngle[i]), MathF.Sin(dustAngle[i]));
			float distance = MathF.Max(0f, MathF.Min(Mathf.Lerp(dustFrom[i], dustTo[i], eased), radius * open - dustSize[i] - WhipLimb.Grid * 2f));
			DrawPuff(air, dir * distance + new Vector2(0f, -14f * eased), dustSize[i] * (0.5f + 0.5f * eased), u);
		}
		for (int i = 0; i < slabCount; i++)
		{
			float since = age - debrisLaunch[i] - debrisFlight[i];
			if (since < 0f || since >= LandDustSeconds || !DebrisAt(i, out Vector2 spot, out _, out _))
				continue;
			float u = since / LandDustSeconds;
			Vector2 dir = spot.LengthSquared() > 1f ? spot.Normalized() : Vector2.Right;
			var across = new Vector2(-dir.Y, dir.X) * (debrisSize[i].X * 0.5f + 6f * u);
			var up = new Vector2(0f, -4f * u);
			DrawPuff(air, spot + across + up, 3f + 3f * u, u);
			DrawPuff(air, spot - across + up, 3f + 3f * u, u);
		}
	}

	// A puff of dust: a disc of whole art pixels, one rect a row so no two overlap and a fading puff
	// fades evenly, its top third lit. It is the dark soil, never the lit earth - opaque and bright, a
	// puff reads as a boulder - and it is thin from the start: it is the one thing here that is seen
	// through, because it is air.
	private static void DrawPuff(CanvasItem canvas, Vector2 centre, float r, float u)
	{
		float g = WhipLimb.Grid;
		int cells = Math.Clamp((int)MathF.Round(r / g), 2, 7);
		float alpha = 0.7f * (u < 0.4f ? 1f : Mathf.Clamp((1f - u) / 0.6f, 0f, 1f));
		Color body = u < 0.35f ? SoilShade : SoilDeep;
		Color lit = u < 0.35f ? EarthEdge : SoilShade;
		body.A = alpha;
		lit.A = alpha;
		float cx = MathF.Round(centre.X / g) * g;
		float cy = MathF.Round(centre.Y / g) * g;
		for (int row = -cells; row < cells; row++)
		{
			float y = row + 0.5f;
			int half = (int)MathF.Round(MathF.Sqrt(cells * cells - y * y));
			if (half <= 0)
				continue;
			canvas.DrawRect(new Rect2(cx - half * g, cy + row * g, half * 2 * g, g), row < -cells / 3 ? lit : body);
		}
	}

	// A chunk of earth: corners knocked off, lit along its top and left (the key is upper left), deep
	// along its bottom and right, and on the bigger faces one hot pixel and one crack.
	private static void DrawChunk(CanvasItem canvas, Vector2 centre, Vector2 size, Color body, Color lit, Color deep, bool detail)
	{
		float g = WhipLimb.Grid;
		Rect2 box = Box(centre, size);
		float x = box.Position.X, y = box.Position.Y, w = box.Size.X, h = box.Size.Y;
		if (w < g * 3f || h < g * 3f)
		{
			canvas.DrawRect(box, body);
			canvas.DrawRect(new Rect2(x, y, w, g), lit);
			return;
		}
		canvas.DrawRect(new Rect2(x + g, y + g, w - g * 2f, h - g * 2f), body);
		canvas.DrawRect(new Rect2(x + g, y, w - g * 2f, g), lit);
		canvas.DrawRect(new Rect2(x, y + g, g, h - g * 2f), lit);
		canvas.DrawRect(new Rect2(x + g, y + h - g, w - g * 2f, g), deep);
		canvas.DrawRect(new Rect2(x + w - g, y + g, g, h - g * 2f), deep);
		if (detail && w >= g * 5f && h >= g * 4f)
		{
			canvas.DrawRect(new Rect2(x + g, y + g, g, g), EarthHot);
			canvas.DrawRect(new Rect2(x + MathF.Floor(w * 0.5f / g) * g, y + h - g * 3f, g, g * 2f), deep);
		}
	}

	// A box of whole art pixels centred on a point, snapped as WhipLimb.Cell snaps a square.
	private static Rect2 Box(Vector2 centre, Vector2 size)
	{
		float g = WhipLimb.Grid;
		return new Rect2(MathF.Round((centre.X - size.X * 0.5f) / g) * g, MathF.Round((centre.Y - size.Y * 0.5f) / g) * g, size.X, size.Y);
	}

	// --- stats -------------------------------------------------------------------------------

	private void RefreshComputedStats()
	{
		RefreshFace();
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 2) * DamageMultiplier));
		float areaBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f;
		radius = MathF.Max(20f, (BaseRadius + areaBonus) * AreaMultiplier);
		duration = MathF.Max(0.5f, (ZoneDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f)) * DurationMultiplier);
		tentacles = Math.Clamp((SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 3) + TentacleBonus, 1, MaxTentacles);
		// A slap is wind-up, lash and recovery; the interval can never be shorter than one of them.
		slapInterval = MathF.Max(WindUpSeconds + StrikeSeconds + RecoverSeconds + 0.04f, SlapInterval * MathF.Max(0.01f, CooldownMultiplier));
		float slowPower = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f;
		slowMultiplier = Mathf.Clamp(SlowMultiplier - slowPower, 0.1f, 1f);
		slowLinger = SlowLinger + (SpellData?.GetEffectValueAtLevel(SpellEffect.SlowDuration, CurrentLevel) ?? 0f);
		rootSeconds = SpellData?.GetEffectValueAtLevel(SpellEffect.RootDuration, CurrentLevel) ?? 0f;
		soulDrain = (SpellData?.GetEffectValueAtLevel(SpellEffect.Lifesteal, CurrentLevel) ?? 0f) > 0f;
		vortexPull = SpellData?.GetEffectValueAtLevel(SpellEffect.VortexPull, CurrentLevel) ?? 0f;
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	// The Maw's opening: fast, then slowing - 0.51 of the reach by the breach (MawBreach 0.30).
	private static float MawOpenCurve(float x)
	{
		x = 1f - Mathf.Clamp(x, 0f, 1f);
		return 1f - x * x;
	}

	private static float EaseOutBack(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		const float c = 1.9f;
		float y = x - 1f;
		return 1f + (c + 1f) * y * y * y + c * y * y;
	}
}
