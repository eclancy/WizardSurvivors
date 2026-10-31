using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WizardSurvivors.scripts;

// Thorned Vine (id thorn_vine, Grass + Poison). A thorned vine bursts out of the ground beside the
// densest pack in range, coils, cracks across the pack like a whip in one wide stroke, and withers back
// into the ground.
//
// "thorn vine looks ridiculous - it should be a whip animation that grows from the ground and then
// slaps enemies in an area, like a tentacle. rename it 'Thorned Vine'." It was a homing bolt with a
// green sprite on it. The display name changed; the id and every save key are still thorn_vine.
//
// A WHIP, NOT A STICK (2026-10-31). "its animation just rotates, I want it to look more like the real
// arc of a whip." It was one limb turned about its root through the sweep, a little curl riding on it.
// Now the lash is a whip stroke (WhipStroke, shared with Stormsweep): the root swings first while the
// vine beyond trails where it was; the bend between them, with a loop in it, rolls out along the vine to
// the tip, the vine behind it lying on the root's line; the tip, last and fastest, whips round the last
// of the arc and CRACKS - a white-hot snap of grass-light, a few pixels of shock and a spray of poison
// thrown the way it was going, no wider than a body. Then the vine goes slack, springs back a little,
// and withers.
//
// **What it hits, and in what order.** The stroke reaches enemies in the order the vine passes over
// them, and it cuts through pierce + 1 of them (BasePierce plus the spell's Pierce) before it SNAGS on
// the last one and stops there. That keeps the pierce the bolt had meaningful - "Pierce +1" is still
// one more enemy a cast - and keeps the payload what it was: the bolt hit up to that many in its line,
// the lash hits up to that many in its arc. Every one of them is poisoned as before.
//
// **Drawn where it hurts.** An enemy is struck when the vine's front (WhipStroke.Front: where the vine
// lies, at the enemy's distance from the root, this frame) has swept past it, plus BodyTolerance for
// the body it is standing in, and it is within the vine's reach. The vine is drawn from the same front,
// sampled; its loop turns only over ground the front has already crossed. Nothing is struck that the
// vine was not drawn across. On a snag the whole stroke stops dead where it was.
// THE AREA IT SWEEPS IS THE SAME as before: the fan of SweepDegrees about the root, out to the vine's
// length (plus BodyTolerance), all of it by the end of an unsnagged stroke. Only the order inside it
// changed - near the root first, the rim last, as the wave rolls out - and with it which enemies a
// snagging stroke reaches first.
//
// **Not Black Tentacles, and not Bramble Seed.** All three come out of the ground. Black Tentacles is
// a dark rift that lingers, slows, and lashes single targets with many limbs; Bramble Seed is a
// patch of roots that grows where it is sown and hurts by standing. This is ONE green limb, alive
// for a second, whose verb is the whip.
//
// Pixel art: the vine, its thorns and leaves and the arc its tip cut are one shader on one quad in the
// air, and the soil it tore is the same shader on a second quad on the ground
// (scenes/shaders/thorn_vine.gdshader). The node lays the vine out every frame as WhipStroke.Points
// points and hands them over in a one-row float texture (Stormsweep's way: nothing allocates). Bark in
// the earth row at the root, Grass with verdant shade above it, poison at the tip - every colour a ramp
// stop, Bayer the only gradient, nothing translucent. The coils, the strikes and the crack are stamped on
// the grid here, in ramp stops that step down and drop out cell by cell.
//
// **Its own five milestones (2026-10-29, .ai/spell-roster.md section 2e).** Each id names something the
// vine DOES, read here by id (SpellData.HasEvolution); the ids are the old default archetype's, so saves
// and the Ascension browser carry over. Each rides the whip stroke:
//   - Barbed Lash: the thorns break off in what it cuts, and a barbed enemy tears itself open as it
//     walks, leaving a trail of sap (ThornBarbs).
//   - Snapback: the whip cracks out and back - drawn back past where the first stroke landed, a second
//     whip stroke over the same arc the other way, with its own loop and its own crack.
//   - Entangle: what it cuts is rooted where it stands, the vine coiled round its feet.
//   - Thicket: the arc the first stroke swept is left a band of thorny brambles on the ground (ThornThicket).
//   - Hydra Vine: one root rises in the thick of the pack and splits into three heads, each cracking
//     across a third of the ring round it (Player.FireThornVine, LaunchHead).
public partial class ThornVine : Node2D
{
	public const string BarbedLashId = "thorn_vine_amplified_impact";
	public const string SnapbackId = "thorn_vine_hyper_acceleration";
	public const string EntangleId = "thorn_vine_reactive_catalyst";
	public const string ThicketId = "thorn_vine_titan_cataclysm";
	public const string HydraVineId = "thorn_vine_infinite_horizon";

	/// <summary>Entangle: how long what the vine cuts is rooted.</summary>
	public const float EntangleSeconds = 0.6f;
	/// <summary>Hydra Vine: heads a root splits into, and the share of the ring each one lashes.</summary>
	public const int HydraHeads = 3;
	private const float HydraHeadSweepDegrees = 130f;
	/// <summary>Hydra Vine: a head reaches this much farther than a lone vine, from the middle of the pack.</summary>
	public const float HydraReach = 1.2f;

	[Export] public float VineLength { get; set; } = 125f;
	[Export] public float SweepDegrees { get; set; } = 170f;
	[Export] public int BasePierce { get; set; } = 3;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;
	// An enemy is a body, not a point: struck when the vine's line comes this close to its centre.
	[Export] public float BodyTolerance { get; set; } = 12f;

	public SpellData SpellData { get; set; }
	public int CurrentLevel { get; set; } = 1;
	public float DamageMultiplier { get; set; } = 1f;
	// The player's area times the spell's evolutions' (Player.FireThornVine).
	public float AreaMultiplier { get; set; } = 1f;
	public Player PlayerRef { get; set; }

	// Recoil is Snapback's: the vine drawn back past where it landed before it cracks the other way. Done
	// is a withered vine that has nothing left to draw but Entangle's coils, kept until the last root ends.
	private enum Stage { Grow, Coil, Lash, Hold, Recoil, Wither, Done }

	private const float GrowSeconds = 0.22f;
	private const float CoilSeconds = 0.14f;
	private const float LashSeconds = 0.20f;
	private const float HoldSeconds = 0.14f;
	private const float RecoilSeconds = 0.10f;
	private const float WitherSeconds = 0.35f;
	private const int MaxStrikes = 32;
	// Entangle's coils, one per enemy rooted, drawn at its feet until the root ends.
	private const int MaxCoils = 24;

	// THE WHIP. The root swings through the arc over the first four fifths of the stroke; the vine beyond
	// the bend trails it by a quarter of the stroke - about a radian, which a vine can bend through without
	// looking stretched (in radians the lag grows with the arc, and Stormsweep's whole-band trail would be a
	// spiral here); the bend leaves the root an eighth of the way in and is a fifth of the vine long.
	private static readonly WhipStroke.Shape Whip = new(lag: 0.26f, handEnd: 0.8f, waveStart: 0.12f, bend: 0.2f);
	private const int Samples = WhipStroke.Samples;
	private const int LashPoints = WhipStroke.Points;
	// The lash texture: the points two to a texel.
	private const int LashTexels = (LashPoints + 1) / 2;
	// How long the crack's flash lasts (its sparks run ImpactDebris's life).
	private const float CrackFlashSeconds = 0.08f;

	private int damage = 3;
	private float length = 100f;
	private int maxHits = 4;
	private int poisonTick = 2;
	private float poisonSeconds = 3f;
	// Any SpeedMultiplier a milestone grants: the vine grows and lashes faster. None of its own five
	// carries one since 2026-10-29 (Hyper Acceleration's 1.25 became Snapback's second stroke).
	private float tempo = 1f;
	// The milestones that change what the vine does (read by id in SetSpellLevel).
	private bool barbed;
	private bool snapback;
	private bool entangle;
	private bool thicket;
	// 0 for the first stroke, 1 for Snapback's return.
	private int stroke;

	private Stage stage = Stage.Grow;
	private float stageAge;
	private float age;
	// Unscaled seconds since launch: a root is real time, whatever the vine's tempo.
	private float realAge;
	private float startAngle;
	private float sweep;
	private float side = 1f;
	private bool snagged;
	private float snagProgress;
	// The stroke's progress (0..1), held where it was when it snagged.
	private float lashU;
	private int hits;
	private float seed;
	private bool launched;

	// THE POSE. The vine is Samples points evenly out from the root, each at its own angle - its progress
	// along the stroke from startAngle, toward `side`, in radians - out to `ext` of its length, with a loop
	// spliced in at loopAt of radius loopRadius px. Every stage writes these; Paint lays them out.
	private readonly float[] theta = new float[Samples];
	// WhipStroke.Front at each sample this frame, 0..1 of the way from the wind-up to the end of the arc.
	private readonly float[] front = new float[Samples];
	// The pose the stroke under way started from (its wind-up), and the one a stage after it starts from.
	private readonly float[] theta0 = new float[Samples];
	private readonly float[] held = new float[Samples];
	private float ext;
	private float ext0 = 0.6f;
	private float heldExt;
	private float loopAt;
	private float loopRadius;
	private float heldLoopAt;
	private float heldLoopRadius;
	private readonly Vector2[] strokeSpace = new Vector2[LashPoints];
	private readonly Vector2[] lashPts = new Vector2[LashPoints];

	// The vine itself, drawn behind this node's own coils, strikes and crack by thorn_vine.gdshader: one
	// quad in the air for the vine and the arc its tip cut, one on the ground for the soil it tore.
	private static Shader vineShader;
	private static readonly StringName ParamLash = "lash";
	private static readonly StringName ParamRootDir = "root_dir";
	private static readonly StringName ParamBaseWidth = "base_width";
	private static readonly StringName ParamDry = "dry";
	private static readonly StringName ParamVenom = "venom";
	private static readonly StringName ParamGilded = "gilded";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSmear = "smear";
	private static readonly StringName ParamSmearR = "smear_r";
	private static readonly StringName ParamSmearFade = "smear_fade";
	private static readonly StringName ParamSoil = "soil";
	private static readonly StringName ParamGrowU = "grow_u";
	private static readonly StringName ParamClodReach = "clod_reach";
	private static readonly Color AirPass = Colors.White;
	private static readonly Color GroundPass = Colors.Black;
	private Node2D vineLayer;
	private Node2D soilLayer;
	private ShaderMaterial vineMaterial;
	private Image lashImage;
	private ImageTexture lashTexture;
	private readonly byte[] lashBytes = new byte[LashTexels * 16];
	private Rect2 vineQuad;
	private float baseWidth = 10f;
	// The values last handed to the shader (Paint sets a parameter only when it changes), NaN for never.
	private bool painted;
	private float lastDry = float.NaN, lastVenom = float.NaN, lastSoil = float.NaN, lastGrowU = float.NaN;
	private float lastSmearFade = float.NaN, lastSmearR = float.NaN;
	private int lastBreakAt = int.MinValue;
	private Vector2 lastRootDir = new(float.NaN, float.NaN);
	private Vector4 lastSmear = new(float.NaN, float.NaN, float.NaN, float.NaN);

	private readonly List<Enemy> candidates = new List<Enemy>();
	private readonly List<bool> struck = new List<bool>();
	private readonly Vector2[] strikeAt = new Vector2[MaxStrikes];
	private readonly float[] strikeTime = new float[MaxStrikes];
	private int strikeCount;
	private readonly Enemy[] coiled = new Enemy[MaxCoils];
	private readonly float[] coilUntil = new float[MaxCoils];
	private int coilCount;
	private float coilsEnd;
	// The crack of each stroke: where the tip landed, the way it was going, and when (age; below 0, none yet).
	private Vector2 crackAt;
	private float crackHeading;
	private float crackTime = -1f;
	private uint crackSeed;
	private bool cracked;

	// THE WRAP. A stroke that snags stops dead - but a whip that catches on something does not just stop:
	// the lash beyond the catch wraps round it and the tip cracks there. So on a snag the vine out to the
	// enemy it caught on holds where it was, and the rest of it winds round that enemy, inside
	// BodyTolerance of its centre - a body the stroke has struck - pulling its own tail in after it, and the
	// tip cracks on the enemy as the last of it goes round. Nothing the stroke had not struck is drawn
	// over. wrapS is where along the vine it caught (below 0: not wrapping), wrapCentre the enemy, root-local.
	private const float WrapSeconds = 0.09f;
	private const float WrapRadius = 9f;
	private const float WrapTurns = 1.15f;
	private const int CoilPoints = WhipStroke.LoopPoints - 2;
	private float wrapS = -1f;
	private Vector2 wrapCentre;
	private float wrapOmega;
	private float snagTime;
	private Enemy snagEnemy;
	// The segment the shader leaves out (the coil's end to the tail's start); below 0, none.
	private int breakAt = -1;
	private static readonly StringName ParamBreakAt = "break_at";

	// The Grass ramp, Earth and the gold row's soil for what has dried, Poison for what it leaves in a
	// wound (.ai/art-direction.md section 3). Opaque stops only: the coils and the strikes step down
	// these and drop out cell by cell, never fade.
	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);
	private static readonly Color EarthMid = new Color(0.788f, 0.604f, 0.369f, 1f);  // earth mid #C99A5E
	private static readonly Color EarthEdge = new Color(0.478f, 0.314f, 0.141f, 1f); // earth edge #7A5024
	private static readonly Color GoldShade = new Color(0.329f, 0.247f, 0.063f, 1f); // gold shade #543F10
	private static readonly Color GrassEdge = new Color(0.290f, 0.549f, 0.227f, 1f); // grass edge #4A8C3A
	private static readonly Color GrassMid = new Color(0.608f, 0.831f, 0.333f, 1f);  // grass mid #9BD455
	private static readonly Color GrassHot = new Color(0.812f, 0.933f, 0.580f, 1f);  // grass hot #CFEE94
	private static readonly Color GrassCore = new Color(0.957f, 1.0f, 0.863f, 1f);   // grass core #F4FFDC
	private static readonly Color PoisonCore = new Color(0.973f, 1.0f, 0.847f, 1f);  // poison core #F8FFD8
	private static readonly Color PoisonHot = new Color(0.863f, 0.957f, 0.549f, 1f); // poison hot #DCF48C
	private static readonly Color PoisonMid = new Color(0.706f, 0.878f, 0.290f, 1f); // poison mid #B4E04A
	private static readonly Color PoisonEdge = new Color(0.420f, 0.580f, 0.125f, 1f);// poison edge #6B9420
	private static readonly ElementColors.Ramp PoisonRamp = ElementColors.GetRamp(Element.Poison);
	// The crack's star: four arms square, then four diagonal.
	private static readonly Vector2[] CrackArms =
	{
		new(1f, 0f), new(0f, 1f), new(-1f, 0f), new(0f, -1f),
		new(0.7071f, 0.7071f), new(-0.7071f, 0.7071f), new(-0.7071f, -0.7071f), new(0.7071f, -0.7071f),
	};

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 3) * DamageMultiplier));
		float reachBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f;
		length = MathF.Max(30f, (VineLength + reachBonus) * AreaMultiplier);
		maxHits = 1 + Math.Max(0, BasePierce + (SpellData?.GetPierceAtLevel(CurrentLevel) ?? 0));
		poisonTick = Math.Max(1, PoisonDamagePerTick + Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f));
		poisonSeconds = MathF.Max(0.1f, PoisonDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f));
		tempo = MathF.Max(0.25f, SpellData?.GetSpeedMultiplier() ?? 1f);
		barbed = SpellData?.HasEvolution(BarbedLashId) ?? false;
		snapback = SpellData?.HasEvolution(SnapbackId) ?? false;
		entangle = SpellData?.HasEvolution(EntangleId) ?? false;
		thicket = SpellData?.HasEvolution(ThicketId) ?? false;
	}

	/// <summary>
	/// Plants the vine on <paramref name="caster"/>'s side of <paramref name="knot"/>, half its
	/// length out from it, aimed to sweep across it.
	/// </summary>
	public void Launch(Vector2 caster, Vector2 knot)
	{
		Vector2 toKnot = knot - caster;
		float distance = toKnot.Length();
		Vector2 heading = distance > 1f ? toKnot / distance : Vector2.Right;
		float rootDistance = MathF.Min(length * 0.5f, distance * 0.6f);
		Vector2 root = knot - heading * rootDistance;
		// A vine comes out of the ground, never out of a tarn or a wall (.ai/terrain.md): where the
		// spot between the wizard and the pack is blocked, it comes up under the pack instead.
		if (MazeNavigation.Active?.IsBlockedAt(root) == true)
			root = knot;
		Begin(root, heading.Angle(), Mathf.DegToRad(SweepDegrees));
	}

	/// <summary>
	/// Hydra Vine: one head of a root at <paramref name="root"/> (in the thick of the pack), lashing a
	/// third of the ring round it, centred on <paramref name="heading"/> (radians). Its heads share the
	/// root, so they come up as one plant.
	/// </summary>
	public void LaunchHead(Vector2 root, float heading)
	{
		length *= HydraReach;
		Begin(root, heading, Mathf.DegToRad(HydraHeadSweepDegrees));
	}

	private void Begin(Vector2 root, float heading, float sweepRadians)
	{
		// This spell draws its own forms at the size it hits; an evolution's ScaleMultiplier would
		// only grow the drawing, and its tint is a stand-in for spells without art. A legendary roll
		// gilds the thorns instead.
		Scale = Vector2.One;
		Modulate = Colors.White;
		GlobalPosition = new Vector2(MathF.Round(root.X / WhipLimb.Grid) * WhipLimb.Grid, MathF.Round(root.Y / WhipLimb.Grid) * WhipLimb.Grid);

		var rng = new RandomNumberGenerator { Seed = GetInstanceId() };
		seed = rng.Randf() * Mathf.Tau;
		side = rng.Randf() < 0.5f ? -1f : 1f;
		sweep = sweepRadians;
		startAngle = heading - side * sweep * 0.5f;
		baseWidth = WhipLimb.SnapSize(Mathf.Clamp(length * 0.11f, 8f, 14f));
		launched = true;
		// The audio manifest's proposed trigger: something arrives out of the ground.
		SfxPlayer.AtPosition(SfxCatalog.SpellSummon, GlobalPosition);
	}

	public override void _Ready()
	{
		// The vine lashes through the air over the bodies it hits (DrawLayers): it, its coils, its strikes
		// and its crack are drawn here and on the child behind it, and only the soil it tore lies on the
		// ground, on a child of its own.
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		vineShader ??= GD.Load<Shader>("res://scenes/shaders/thorn_vine.gdshader");
		vineMaterial = new ShaderMaterial { Shader = vineShader };
		lashImage = Image.CreateEmpty(LashTexels, 1, false, Image.Format.Rgbaf);
		lashTexture = ImageTexture.CreateFromImage(lashImage);
		vineMaterial.SetShaderParameter(ParamLash, lashTexture);
		vineLayer = new Node2D { Name = "Vine", ShowBehindParent = true, Material = vineMaterial };
		AddChild(vineLayer);
		vineLayer.Draw += DrawVine;
		soilLayer = new Node2D { Name = "Soil", Material = vineMaterial, ZAsRelative = false, ZIndex = DrawLayers.Ground };
		AddChild(soilLayer);
		soilLayer.Draw += DrawSoil;
	}

	public override void _ExitTree()
	{
		if (vineLayer != null && IsInstanceValid(vineLayer))
			vineLayer.Draw -= DrawVine;
		if (soilLayer != null && IsInstanceValid(soilLayer))
			soilLayer.Draw -= DrawSoil;
	}

	public override void _Process(double delta)
	{
		if (!launched)
			return;
		var perfStart = FrameProfiler.Begin();
		Tick(delta);
		FrameProfiler.End(FrameProfiler.Section.ThornVine, perfStart);
	}

	private void Tick(double delta)
	{
		float dt = (float)delta * tempo;
		age += dt;
		stageAge += dt;
		realAge += (float)delta;
		switch (stage)
		{
			case Stage.Grow:
				if (stageAge >= GrowSeconds) Enter(Stage.Coil);
				break;
			case Stage.Coil:
				if (stageAge >= CoilSeconds) BeginStroke();
				break;
			case Stage.Lash:
				if (stageAge >= LashSeconds) EndStroke();
				break;
			case Stage.Hold:
				if (stageAge >= HoldSeconds)
				{
					if (stroke == 0)
						EndFirstStroke();
					Hold();
					if (snapback && stroke == 0)
						BeginReturnStroke();
					else
						Enter(Stage.Wither);
				}
				break;
			case Stage.Recoil:
				if (stageAge >= RecoilSeconds) BeginStroke();
				break;
			case Stage.Wither:
				if (stageAge >= WitherSeconds) Enter(Stage.Done);
				break;
			case Stage.Done:
				// Nothing of the vine is left but the coils round what it rooted.
				if (realAge >= coilsEnd) { QueueFree(); return; }
				break;
		}
		Pose();
		if (stage == Stage.Lash)
			Sweep();
		Paint();
		QueueRedraw();
		vineLayer?.QueueRedraw();
		soilLayer?.QueueRedraw();
	}

	// A stroke begins from wherever the wind-up left the vine: that pose is the one it whips out of.
	private void BeginStroke()
	{
		Array.Copy(theta, theta0, Samples);
		ext0 = ext;
		lashU = 0f;
		cracked = false;
		wrapS = -1f;
		Enter(Stage.Lash);
		CollectCandidates();
	}

	// The stroke is over. Unless it snagged (and so cracks as its wrap closes), the tip has just landed
	// at the end of the arc: the crack.
	private void EndStroke()
	{
		if (!snagged)
		{
			lashU = 1f;
			Pose();
			Crack(lashPts[LashPoints - 1], startAngle + side * sweep + side * Mathf.Pi * 0.5f);
		}
		Hold();
		Enter(Stage.Hold);
	}

	private void Crack(Vector2 at, float heading)
	{
		cracked = true;
		crackAt = at;
		crackHeading = heading;
		crackTime = age;
		crackSeed = (uint)(GetInstanceId() & 0xFFFF) + (uint)stroke * 7919u;
	}

	// Remembers the pose the vine is in, for a stage that starts from it.
	private void Hold()
	{
		Array.Copy(theta, held, Samples);
		heldExt = ext;
		heldLoopAt = loopAt;
		heldLoopRadius = loopRadius;
	}

	// The first stroke is over: leave Thicket's brambles on the arc it swept.
	private void EndFirstStroke()
	{
		if (!thicket || PlayerRef == null || !IsInstanceValid(PlayerRef) || GetParent() is not Node2D arena)
			return;
		ThornThicket.Lay(arena, GlobalPosition, length, startAngle, side, snagged ? snagProgress : sweep, damage, poisonTick, poisonSeconds, SpellData, PlayerRef);
	}

	// Snapback: the vine is drawn back past where it landed and cracks back across the same arc the other
	// way - a fresh stroke, cutting through as many again. The pose it holds is turned into the new
	// stroke's terms (its progress from the new start, the other way), so the recoil starts where it is.
	private void BeginReturnStroke()
	{
		float end = snagged ? snagProgress : sweep;
		startAngle += side * end;
		sweep = end;
		side = -side;
		for (int i = 0; i < Samples; i++)
			held[i] = end - held[i];
		snagged = false;
		snagProgress = 0f;
		hits = 0;
		stroke = 1;
		Enter(Stage.Recoil);
	}

	private void Enter(Stage next)
	{
		stage = next;
		stageAge = 0f;
	}

	// Lays the vine out for this frame: theta, ext and the loop.
	private void Pose()
	{
		float wave = age * 9f + seed;
		switch (stage)
		{
			case Stage.Grow:
			{
				// Bursts out of the ground wound back past where the stroke will start, unwinding as it rises.
				float g = Mathf.Clamp(stageAge / GrowSeconds, 0f, 1f);
				ext = 0.55f * EaseOutBack(g);
				float curl = 0.6f * (1f - g) - 0.3f * g;
				for (int i = 0; i < Samples; i++)
				{
					float s = i / (float)(Samples - 1);
					theta[i] = -0.35f + curl * s * s + 0.25f * s * MathF.Sin(wave - s * 4.5f);
				}
				loopRadius = 0f;
				break;
			}
			case Stage.Coil:
			{
				// The backswing: drawn back and curled against the way it is about to crack - the wind-up the
				// player can read.
				float c = Smooth(stageAge / CoilSeconds);
				ext = 0.55f + 0.05f * c;
				float curl = Mathf.Lerp(-0.3f, -0.75f, c);
				for (int i = 0; i < Samples; i++)
				{
					float s = i / (float)(Samples - 1);
					theta[i] = -(0.35f + 0.2f * c) + curl * s * s + 0.075f * s * MathF.Sin(wave - s * 4.5f);
				}
				loopRadius = 0f;
				break;
			}
			case Stage.Lash:
			{
				// The whip stroke: every point from its wind-up to the end of the arc, as far as the front has
				// carried it. The vine flies out to its full length in the first quarter. Stops dead on a snag.
				if (!snagged)
					lashU = Mathf.Clamp(stageAge / LashSeconds, 0f, 1f);
				ext = Mathf.Lerp(ext0, 1f, Mathf.Clamp(lashU / 0.25f, 0f, 1f));
				WhipStroke.Lay(lashU, Whip, front);
				for (int i = 0; i < Samples; i++)
					theta[i] = theta0[i] + (sweep - theta0[i]) * front[i];
				WhipStroke.Loop(lashU, Whip, out loopAt, out float size);
				loopRadius = snagged ? 0f : size * LoopRadiusMax();
				break;
			}
			case Stage.Hold:
			{
				float h = Mathf.Clamp(stageAge / HoldSeconds, 0f, 1f);
				if (snagged)
				{
					// Caught: held taut where it stopped, a shiver of tension running back from the snag.
					for (int i = 0; i < Samples; i++)
					{
						float s = i / (float)(Samples - 1);
						theta[i] = held[i] - 0.025f * s * (0.5f + 0.5f * MathF.Sin(age * 40f));
					}
					ext = heldExt;
					loopAt = heldLoopAt;
					loopRadius = heldLoopRadius * (1f - h);
				}
				else
				{
					// Spent: the vine goes slack back over the ground it cut and springs back a little, a ripple
					// running up it from the tip - never past where it landed.
					float sag = 0.16f * Smooth(h * 2.2f) * (1f - 0.4f * h);
					for (int i = 0; i < Samples; i++)
					{
						float s = i / (float)(Samples - 1);
						float ripple = 0.07f * (1f - h) * s * MathF.Abs(MathF.Sin(Mathf.Tau * (s * 1.3f - h * 2.4f)));
						theta[i] = MathF.Min(held[i], sweep - sag * MathF.Sin(Mathf.Pi * s) - ripple);
					}
					ext = heldExt * (1f - 0.07f * Smooth(h));
					loopRadius = 0f;
				}
				break;
			}
			case Stage.Recoil:
			{
				// Snapback: torn free of what it wrapped and back past where it landed, curling against the
				// way it is about to crack.
				float r = Smooth(stageAge / RecoilSeconds);
				for (int i = 0; i < Samples; i++)
				{
					float s = i / (float)(Samples - 1);
					theta[i] = Mathf.Lerp(held[i], -0.18f - 0.5f * s * s, r);
				}
				ext = Mathf.Lerp(heldExt, 0.9f, r);
				loopRadius = 0f;
				break;
			}
			default:
			{
				// Wither: held as it was left - slack, or wound round what it caught - and below, after it is
				// laid out, drooped, dried and drawn back into the ground.
				Array.Copy(held, theta, Samples);
				ext = heldExt;
				loopAt = heldLoopAt;
				loopRadius = heldLoopRadius;
				break;
			}
		}
		Wrap();
		Lay();
		if (stage == Stage.Wither)
			Wilt(Smooth(stageAge / WitherSeconds));
		// A snagged stroke cracks as the last of it goes round what it caught.
		if (wrapS >= 0f && wrapOmega >= 1f && !cracked && breakAt > 0)
		{
			Vector2 end = lashPts[breakAt];
			Vector2 round = end - wrapCentre;
			Crack(end, round.Angle() + side * Mathf.Pi * 0.5f);
		}
	}

	// How far the wrap has wound, following what it caught while that still stands.
	private void Wrap()
	{
		if (wrapS < 0f)
		{
			breakAt = -1;
			return;
		}
		if (snagEnemy != null && IsInstanceValid(snagEnemy) && !snagEnemy.IsDying)
			wrapCentre = snagEnemy.GlobalPosition - GlobalPosition;
		wrapOmega = stage == Stage.Recoil
			? 1f - Smooth(stageAge / RecoilSeconds)
			: Smooth((age - snagTime) / WrapSeconds);
	}

	// The wither, on the laid-out points: each turned a little further the way it was going the farther
	// out it is (a droop), and all drawn in toward the root.
	private void Wilt(float d)
	{
		float reach = MathF.Max(1f, length * heldExt);
		float keep = 1f - d;
		for (int p = 0; p < LashPoints; p++)
		{
			Vector2 at = lashPts[p];
			float f = at.Length() / reach;
			lashPts[p] = at.Rotated(side * 0.9f * d * f * f) * keep;
		}
	}

	// The stroke's front at s along the vine, in radians from startAngle: WhipStroke.Front carried from the
	// wind-up to the end of the arc, laid into theta this frame (Pose) - the samples the vine is drawn
	// through, so the hit test (Sweep) reads exactly the line on screen.
	private float FrontAngle(float s) => WhipStroke.Sample(theta, s);

	private float LoopRadiusMax() => Mathf.Clamp(length * 0.09f, 8f, 14f);

	// The pose into points: the loop spliced in (in stroke space, held behind the vine), then each point
	// turned round the root to its angle and set out to its distance.
	private void Lay()
	{
		float reach = MathF.Max(1f, length * ext);
		if (wrapS >= 0f)
		{
			LayWrapped(reach);
			return;
		}
		WhipStroke.Splice(theta, loopAt, loopRadius, reach, reach, true, float.MinValue, strokeSpace);
		for (int p = 0; p < LashPoints; p++)
			lashPts[p] = Polar(strokeSpace[p].X, strokeSpace[p].Y, reach);
	}

	private Vector2 Polar(float s, float progress, float reach)
	{
		float a = startAngle + side * progress;
		return new Vector2(MathF.Cos(a), MathF.Sin(a)) * (s * reach);
	}

	// Snagged: the vine out to where it caught, as the stroke left it; from there a coil wound round what
	// it caught, wrapOmega of the way, closing in to two thirds of WrapRadius; and then - a break the
	// shader leaves undrawn - the rest of the vine where it trailed, pulled in toward the catch as the coil
	// takes it. The loop's points are the coil's; the count is the same.
	private void LayWrapped(float reach)
	{
		int split = Math.Min(Samples, (int)(wrapS * (Samples - 1) + 1e-4f) + 1);
		int o = 0;
		for (int i = 0; i < split; i++)
			lashPts[o++] = Polar(i / (float)(Samples - 1), theta[i], reach);
		Vector2 caught = Polar(wrapS, WhipStroke.Sample(theta, wrapS), reach);
		lashPts[o++] = caught;
		Vector2 rel = caught - wrapCentre;
		float r0 = MathF.Min(rel.Length(), BodyTolerance);
		float a0 = rel.Angle();
		for (int k = 1; k <= CoilPoints; k++)
		{
			float f = wrapOmega * k / CoilPoints;
			float a = a0 + side * Mathf.Tau * WrapTurns * f;
			lashPts[o++] = wrapCentre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Mathf.Lerp(r0, WrapRadius * 0.67f, f);
		}
		breakAt = o - 1;
		lashPts[o++] = caught;
		float end = wrapS + (1f - wrapOmega) * (1f - wrapS);
		for (int i = split; i < Samples; i++)
		{
			float s = MathF.Min(i / (float)(Samples - 1), end);
			lashPts[o++] = Polar(s, WhipStroke.Sample(theta, s), reach);
		}
	}

	private void CollectCandidates()
	{
		candidates.Clear();
		struck.Clear();
		float reachSquared = (length + BodyTolerance) * (length + BodyTolerance);
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) > reachSquared)
				continue;
			candidates.Add(enemy);
			struck.Add(false);
		}
	}

	// How far along the stroke `angle` is, in radians from its start, in the direction it travels.
	private float Progress(float angle) => side * Mathf.Wrap(angle - startAngle, -Mathf.Pi, Mathf.Pi);

	// Strikes, in the order the stroke reaches them, everything the vine's front has swept over: at the
	// enemy's distance from the root, the front's angle there (FrontAngle) has passed it.
	private void Sweep()
	{
		if (snagged)
			return;
		float reach = length * ext;

		while (hits < maxHits)
		{
			int next = -1;
			float nextProgress = float.MaxValue;
			for (int k = 0; k < candidates.Count; k++)
			{
				Enemy e = candidates[k];
				if (struck[k] || !IsInstanceValid(e) || e.IsDying)
					continue;
				Vector2 offset = e.GlobalPosition - GlobalPosition;
				float distance = offset.Length();
				if (distance > reach + BodyTolerance || distance < 1f)
					continue;
				float slack = MathF.Atan2(BodyTolerance, distance);
				float q = Progress(offset.Angle());
				if (q < -slack || q > MathF.Min(sweep, FrontAngle(MathF.Min(1f, distance / reach))) + slack)
					continue;
				if (q < nextProgress)
				{
					nextProgress = q;
					next = k;
				}
			}
			if (next < 0)
				return;

			struck[next] = true;
			Strike(candidates[next]);
			hits++;
			if (hits >= maxHits)
			{
				// Snagged: the stroke stops dead on the last thing it cut through, and what is left of the
				// vine past it wraps round it (THE WRAP).
				Enemy caught = candidates[next];
				snagged = true;
				snagProgress = Mathf.Clamp(nextProgress, 0f, sweep);
				snagEnemy = caught;
				snagTime = age;
				wrapCentre = IsInstanceValid(caught) ? caught.GlobalPosition - GlobalPosition : wrapCentre;
				wrapS = Mathf.Clamp(wrapCentre.Length() / MathF.Max(1f, reach), 0f, 1f);
				wrapOmega = 0f;
				loopRadius = 0f;
				return;
			}
		}
	}

	private void Strike(Enemy enemy)
	{
		if (strikeCount < MaxStrikes)
		{
			strikeAt[strikeCount] = enemy.GlobalPosition - GlobalPosition;
			strikeTime[strikeCount] = age;
			strikeCount++;
		}
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		PlayerRef.DealDamageToEnemy(enemy, damage, source: SpellData);
		if (!IsInstanceValid(enemy) || enemy.IsDying)
			return;
		enemy.ApplyPoison(poisonTick, poisonSeconds);
		// Barbed Lash: a thorn breaks off in the wound, and tears it as the enemy walks on.
		if (barbed && GetParent() is Node2D arena)
			ThornBarbs.For(arena).Barb(enemy, damage, SpellData, PlayerRef);
		// Entangle: rooted where it was cut (a boss's slow floor makes it a hard slow), the vine wound
		// round its feet for as long as the root holds.
		if (entangle)
		{
			enemy.ApplySlow(0f, EntangleSeconds);
			if (coilCount < MaxCoils)
			{
				coiled[coilCount] = enemy;
				coilUntil[coilCount++] = realAge + EntangleSeconds;
			}
			coilsEnd = MathF.Max(coilsEnd, realAge + EntangleSeconds);
		}
	}

	// --- drawing ---------------------------------------------------------------------------

	public override void _Draw()
	{
		if (!launched)
			return;
		DrawCoils();
		if (stage == Stage.Done)
			return;
		DrawStrikes();
		DrawCrack();
	}

	// The vine's numbers for this frame, to thorn_vine.gdshader: the points (written in place into the lash
	// texture), how dry it is, whether its tip carries the poison, the arc its tip has just cut, and the
	// soil it tore. Scalars and one texture update; nothing allocates.
	private void Paint()
	{
		if (vineMaterial == null || stage == Stage.Done)
			return;
		Span<Vector4> texels = MemoryMarshal.Cast<byte, Vector4>(lashBytes.AsSpan());
		float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;
		for (int x = 0; x < LashTexels; x++)
		{
			Vector2 a = lashPts[2 * x];
			Vector2 b = 2 * x + 1 < LashPoints ? lashPts[2 * x + 1] : a;
			texels[x] = new Vector4(a.X, a.Y, b.X, b.Y);
			minX = MathF.Min(minX, MathF.Min(a.X, b.X));
			minY = MathF.Min(minY, MathF.Min(a.Y, b.Y));
			maxX = MathF.Max(maxX, MathF.Max(a.X, b.X));
			maxY = MathF.Max(maxY, MathF.Max(a.Y, b.Y));
		}
		lashImage.SetData(LashTexels, 1, false, Image.Format.Rgbaf, lashBytes);
		lashTexture.Update(lashImage);

		// What never changes over a vine's life goes over once; the rest only when it changes - a
		// parameter set is a call into the rendering server, and most of these hold still for a whole stage.
		if (!painted)
		{
			painted = true;
			vineMaterial.SetShaderParameter(ParamBaseWidth, baseWidth);
			vineMaterial.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
			vineMaterial.SetShaderParameter(ParamSeed, seed);
			vineMaterial.SetShaderParameter(ParamClodReach, length * 0.28f);
		}
		float dry = stage == Stage.Wither ? Smooth(stageAge / WitherSeconds) : 0f;
		float a0 = startAngle + side * theta[1];
		Param(ParamRootDir, new Vector2(MathF.Cos(a0), MathF.Sin(a0)), ref lastRootDir);
		Param(ParamBreakAt, breakAt, ref lastBreakAt);
		Param(ParamDry, dry, ref lastDry);
		Param(ParamVenom, stage == Stage.Lash || stage == Stage.Hold ? 1f : 0f, ref lastVenom);

		// The arc the tip cut: from where the tip was a moment ago to where it is, fading through the hold -
		// so a sliver while the tip trails and a streak along the rim as it whips round. None on a snag:
		// the tip never got there.
		float fade = snagged ? 0f : stage == Stage.Lash ? 1f : stage == Stage.Hold ? 1f - stageAge / HoldSeconds : 0f;
		float tipNow = 0f, tipThen = 0f, reach = length * ext;
		if (fade > 0f)
		{
			tipNow = MathF.Max(0f, stage == Stage.Lash ? theta[Samples - 1] : sweep);
			float u = stage == Stage.Lash ? lashU : 1f;
			float from = theta0[Samples - 1];
			tipThen = MathF.Max(0f, from + (sweep - from) * WhipStroke.Front(MathF.Max(0f, u - 0.18f), 1f, Whip));
			reach = length * (stage == Stage.Lash ? ext : heldExt);
			// The quad takes in the arc: its two ends and its middle on the rim.
			for (int k = 0; k < 3; k++)
			{
				float a = startAngle + side * Mathf.Lerp(tipThen, tipNow, k * 0.5f);
				var rim = new Vector2(MathF.Cos(a), MathF.Sin(a)) * reach;
				minX = MathF.Min(minX, rim.X);
				minY = MathF.Min(minY, rim.Y);
				maxX = MathF.Max(maxX, rim.X);
				maxY = MathF.Max(maxY, rim.Y);
			}
		}
		float smearFade = tipNow > tipThen ? fade : 0f;
		Param(ParamSmearFade, smearFade, ref lastSmearFade);
		if (smearFade > 0f)
		{
			// The arc's streaks flicker on the clock; with no arc the shader never reads it.
			vineMaterial.SetShaderParameter(ParamTime, realAge);
			Param(ParamSmear, new Vector4(startAngle, side, tipThen, tipNow), ref lastSmear);
			Param(ParamSmearR, reach, ref lastSmearR);
		}
		Param(ParamSoil, 1f - dry, ref lastSoil);
		Param(ParamGrowU, stage == Stage.Grow ? Mathf.Clamp(stageAge / GrowSeconds, 0f, 1f) : -1f, ref lastGrowU);

		// Only the ground the vine, its thorns and leaves and its arc can be on is shaded.
		const float pad = 16f;
		vineQuad = new Rect2(minX - pad, minY - pad, maxX - minX + pad * 2f, maxY - minY + pad * 2f);
	}

	private void Param(StringName name, float value, ref float last)
	{
		if (value == last)
			return;
		last = value;
		vineMaterial.SetShaderParameter(name, value);
	}

	private void Param(StringName name, int value, ref int last)
	{
		if (value == last)
			return;
		last = value;
		vineMaterial.SetShaderParameter(name, value);
	}

	private void Param(StringName name, Vector2 value, ref Vector2 last)
	{
		if (value == last)
			return;
		last = value;
		vineMaterial.SetShaderParameter(name, value);
	}

	private void Param(StringName name, Vector4 value, ref Vector4 last)
	{
		if (value == last)
			return;
		last = value;
		vineMaterial.SetShaderParameter(name, value);
	}

	// The vine and the arc its tip cut, in the air: one quad over everything they can be on this frame.
	private void DrawVine()
	{
		if (!launched || stage == Stage.Done)
			return;
		vineLayer.DrawRect(vineQuad, AirPass);
	}

	// The soil it tore, on the ground: one quad round the root and the clods it throws.
	private void DrawSoil()
	{
		if (!launched || stage == Stage.Done)
			return;
		float r = baseWidth * 0.5f + 8f + length * 0.28f;
		soilLayer.DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), GroundPass);
	}

	// The crack of a stroke, where the tip landed: a white-hot snap of grass-light - a square heart and a
	// four-armed star, the other four arms short - that shrinks over CrackFlashSeconds; a few pixels of
	// shock opening round it; and poison thrown the way the tip was going. None of it wider than
	// BodyTolerance, the reach the tip's hit is allowed past the vine's length.
	private void DrawCrack()
	{
		if (crackTime < 0f)
			return;
		float since = age - crackTime;
		if (since >= ImpactDebris.LifeSeconds)
			return;
		float g = WhipLimb.Grid;
		ImpactDebris.Draw(this, crackAt, BodyTolerance, since, crackSeed, PoisonRamp, 9, Colors.White,
			crackHeading - 1.2f, 2.4f);
		if (since >= CrackFlashSeconds)
			return;
		float u = since / CrackFlashSeconds;
		DrawRect(WhipLimb.Cell(crackAt, u < 0.35f ? 8f : 4f), u < 0.5f ? GrassCore : GrassHot);
		float arm = MathF.Min(BodyTolerance - g, u < 0.4f ? 10f : 6f);
		int first = (crackSeed & 1u) == 1u ? 4 : 0;
		Color hot = u < 0.5f ? GrassCore : GrassHot;
		for (int a = 0; a < 4; a++)
		{
			Vector2 dir = CrackArms[first + a];
			for (float r = 4f; r <= arm; r += g)
				DrawRect(WhipLimb.Cell(crackAt + dir * r, g), hot);
			if (u < 0.5f)
				DrawRect(WhipLimb.Cell(crackAt + CrackArms[(first + 4 + a) % 8] * 4f, g), PoisonHot);
		}
		float ring = Mathf.Lerp(5f, BodyTolerance - g, u);
		Color shock = u < 0.5f ? GrassHot : GrassMid;
		for (int a = 0; a < 8; a++)
		{
			if (u > 0.5f && a % 2 == 1)
				continue;
			float ang = crackHeading + a * Mathf.Tau / 8f + 0.39f;
			DrawRect(WhipLimb.Cell(crackAt + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * ring, g), shock);
		}
	}

	// A tone chosen per cell between the living one and the dried one: the wither eats the green cell by
	// cell, in an order fixed per cell, rather than lerping it toward a brown no ramp holds.
	private static Color Dried(Color living, Color dried, float dry, int cell) =>
		Mathf.PosMod(cell * 0.6180339f, 1f) < dry ? dried : living;

	// Entangle: a loop of vine wound round the feet of everything rooted, growing round in the first
	// tenth of a second, thorns out, and drying and slackening in the last.
	private void DrawCoils()
	{
		for (int k = 0; k < coilCount; k++)
		{
			Enemy enemy = coiled[k];
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying || realAge >= coilUntil[k])
				continue;
			float since = realAge - (coilUntil[k] - EntangleSeconds);
			float grow = Mathf.Clamp(since / 0.1f, 0f, 1f);
			float dry = Mathf.Clamp((since - (EntangleSeconds - 0.12f)) / 0.12f, 0f, 1f);
			StatusFxGeometry body = enemy.StatusFxGeometry;
			Vector2 feet = ToLocal(enemy.GlobalPosition) + new Vector2(0f, body.FeetY - 2f);
			float rx = MathF.Max(10f, body.HalfWidth * 0.95f);
			float ry = MathF.Max(4f, rx * 0.38f);
			float turn = Mathf.Tau * grow;
			int steps = Math.Max(12, (int)(rx * 1.6f));
			for (int pass = 0; pass < 2; pass++)
			{
				for (int i = 0; i <= steps; i++)
				{
					float a = seed + turn * i / steps;
					Vector2 at = feet + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
					if (pass == 0)
						DrawRect(WhipLimb.Cell(at, WhipLimb.Grid * 3f), Occ);
					else
						DrawRect(WhipLimb.Cell(at, WhipLimb.Grid), MathF.Sin(a) < 0f ? Dried(GrassMid, EarthEdge, dry, i) : Dried(GrassEdge, GoldShade, dry, i));
				}
			}
			if (grow < 1f)
				continue;
			// Three thorns raked outward, and the tendril's end curling up the shin.
			for (int t = 0; t < 3; t++)
			{
				float a = seed + 1.1f + t * 2.1f;
				Vector2 rim = feet + new Vector2(MathF.Cos(a) * (rx + WhipLimb.Grid * 1.5f), MathF.Sin(a) * (ry + WhipLimb.Grid));
				DrawRect(WhipLimb.Cell(rim, WhipLimb.Grid), Dried(GrassHot, EarthMid, dry, t + 7));
			}
			Vector2 tip = feet + new Vector2(MathF.Cos(seed) * rx, MathF.Sin(seed) * ry);
			DrawRect(WhipLimb.Cell(tip + new Vector2(0f, -WhipLimb.Grid * 2f), WhipLimb.Grid), Dried(GrassMid, EarthEdge, dry, 11));
			DrawRect(WhipLimb.Cell(tip + new Vector2(WhipLimb.Grid, -WhipLimb.Grid * 4f), WhipLimb.Grid), Dried(GrassMid, EarthEdge, dry, 12));
		}
	}

	// A splash of poison where each enemy was cut, over a fifth of a second: white-hot in its first
	// instant, then stepping down the Poison ramp as its drops fly out, each drop going out on its own.
	private void DrawStrikes()
	{
		for (int k = 0; k < strikeCount; k++)
		{
			float since = age - strikeTime[k];
			if (since > 0.2f)
				continue;
			float u = since / 0.2f;
			Color c = u < 0.25f ? PoisonCore : u < 0.5f ? PoisonHot : u < 0.75f ? PoisonMid : PoisonEdge;
			Vector2 at = strikeAt[k];
			float spread = WhipLimb.Grid * (1f + 2f * u);
			if (u < 0.5f)
				DrawRect(WhipLimb.Cell(at, WhipLimb.Grid * 2f), u < 0.25f ? GrassHot : GrassMid);
			if (u < 0.9f)
				DrawRect(WhipLimb.Cell(at + new Vector2(spread, -spread), WhipLimb.Grid), c);
			if (u < 0.7f)
				DrawRect(WhipLimb.Cell(at + new Vector2(-spread, -spread * 0.5f), WhipLimb.Grid), c);
			if (u < 0.8f)
				DrawRect(WhipLimb.Cell(at + new Vector2(spread * 0.4f, spread), WhipLimb.Grid), c);
		}
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	private static float EaseOutBack(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		const float c = 1.9f;
		float y = x - 1f;
		return 1f + (c + 1f) * y * y * y + c * y * y;
	}
}
