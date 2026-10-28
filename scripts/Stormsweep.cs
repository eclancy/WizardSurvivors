using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WizardSurvivors.scripts;

// Stormsweep (pure Lightning): an arc of storm-light leapt from the wizard's hand to the side he is
// facing and swept down through the ground beside him, every cooldown, aimed at nothing.
//
// WHY IT EXISTS (.ai/spell-roster.md, "The facing family"). The developer asked for spells that work
// like the genre's directional weapons - "they launch in a specific direction from the player every
// time they fire, not targeting enemies" - and then, of the first build: "I didn't mean literally whip
// and dagger, I still want spells themed around magic." So the mechanics are that genre's and the
// fiction is spellcraft. Every other spell in the roster decides where it goes - an enemy, a pack, the
// ground near one, the caster. This one is decided by the PLAYER: it sweeps the side his body faces
// (Player.FacingSide, the same rule that flips his sprite), so walking is aiming, and a crowd on the
// other side is a crowd it never touches until he turns or it buys a second sweep.
//
// THE SHAPE. A wide flat band beside him: from his hand out to the arc's reach, and HalfBand above and
// below his centre. One arc is three beats - it LEAPS out along the top edge of the band to full
// length, SWEEPS down across the whole band (its far end leading), and LINGERS, flickering, while the
// afterglow of where it swept fades. Damage lands during the sweep, on each enemy at the moment the
// drawn arc passes across it: an enemy is struck when, at its distance along the arc, the arc's across
// position moved past its centre this frame (plus RiptideWave.BodyAllowance for the body it stands in).
// So the band is struck by being swept, top to bottom, and nothing outside what was swept is touched.
// No ring, no box, no preview: the arc and its afterglow are the hit area (.ai/art-direction.md 3).
//
// THE WHIP (2026-10-28). "stormsweep looks robotic. it should be shaped and animated more like a
// whip." The beats and the hit test above are unchanged to the frame; what changed is the picture. The
// arc is drawn as a lash of storm-light (scenes/shaders/stormsweep_lash.gdshader), thick at the hand and
// thinning to a one-pixel tip, laid out fresh every frame (Pose): in the LEAP it unfurls along the
// band's top edge with a loop rolling out from the hand to the tip, and snaps straight - the CRACK, a
// white-hot tip and sparks no wider than a body; in the SWEEP it lies on exactly the curve the hit test
// sweeps, tip leading, with a ripple running down it and a smear of the ground just struck behind it;
// in the LINGER it goes slack and springs back toward the hand, cooling and dissolving while the smear
// of the swept band fades. It is still storm-light, not leather (no lash or whip in its name, card or
// icon - .ai/spell-roster.md, "The facing family"): the developer asked for the motion.
//
// AMOUNT is more sweeps, in sequence, ArcGap apart: the second goes to the OPPOSITE side, the third to
// the facing side again, and so on - the genre's rule for this shape. Area grows the band both ways
// (longer and taller): Range lines lengthen the arc, AreaSize lines (px) thicken the band, and the
// player's Area and every milestone's AreaMultiplier scale both.
//
// ONE NODE, EVERY ARC. A child of the Player, so an arc stays at his hand while he walks; arcs are
// slots in a fixed array and each pass's hits are a fixed array of instance ids. Bursts (the spark off
// the arc's end, a Thunderclap) are kept in world space and stay where they went off. Nothing is
// instanced or allocated per arc or per hit.
//
// The milestones (SpellEvolutionCatalog.SetupStormsweepEvolutions):
//   High Voltage    (level 4, +1 Lightning) - harder, longer and taller.
//   Iron Current    (level 4, the element path, +1 Metal) - the storm runs through iron: beads of
//                   iron strung along the current (repainted here, IPaintsElementPath); the first hit
//                   on anything Withers it.
//   Rapid Discharge (level 4) - a shorter cooldown, and every arc runs faster (SpeedMultiplier).
//   Thunderclap     (level 8, +1 Lightning) - every arc sweeps twice, down and back up, and each pass
//                   ends in a thunderclap at the arc's far end.
//   Cardinal Storm  (level 8) - every sweep goes all four ways at once.
public partial class Stormsweep : Node2D, IPaintsElementPath
{
	// Evolution ids. SpellEvolutionCatalog builds the options from these; this node reads them.
	public const string HighVoltageId = "stormsweep_high_voltage";
	public const string IronCurrentId = "stormsweep_iron_current";
	public const string RapidDischargeId = "stormsweep_rapid_discharge";
	public const string ThunderclapId = "stormsweep_thunderclap";
	public const string CardinalStormId = "stormsweep_cardinal_storm";

	/// <summary>The most sweeps (crosses, under Cardinal Storm) one cast may queue. Amount can ask for more.</summary>
	public const int MaxArcsPerCast = 8;

	// --- the band (tuned in scenes/Stormsweep.tscn) ------------------------------------------------

	// Half the band's height at level 1, before AreaSize lines and every Area multiplier. The band is
	// a little taller than a skeleton is wide either side of the player's centre, so a sweep at a crowd
	// walking along his row takes the row, and one passing a body's width above or below it does not.
	[Export] public float BaseHalfBand { get; set; } = 22f;
	// Where along the arc the hand is: the arc leaves here, not the player's centre.
	[Export] public float HandReach { get; set; } = 14f;
	// Seconds between the sweeps of one cast, at tempo 1.
	[Export] public float ArcGap { get; set; } = 0.14f;

	// The three beats of an arc, at tempo 1. The leap is the warning: a sweep never lands on a frame the
	// arc was not already seen reaching across.
	public const float LeapSeconds = 0.07f;
	public const float SweepSeconds = 0.08f;
	public const float ResweepPause = 0.05f;
	public const float LingerSeconds = 0.16f;

	// --- Thunderclap --------------------------------------------------------------------------------
	public const float ThunderRadius = 56f;
	public const float ThunderDamageFraction = 0.8f;

	private const int MaxArcs = 32;
	private const int MaxHitsPerPass = 64;
	private const int MaxBursts = 48;
	// The arc is laid out as this many segments, hand to far end. stormsweep_lash.gdshader's PTS is this
	// plus one and its arrays are sized for MaxDrawn lashes: change them together.
	private const int Points = 16;
	// Hit sparks drawn for one pass, at most - the rest of the crowd it struck still flinches (Enemy's
	// own hit flash), but forty sparks on one frame is noise, not information.
	private const int SparksPerPass = 6;
	// Gap between two sweep sounds. Cardinal Storm sweeps four at once, and three sweeps a cast at
	// level 8 is one every 0.14 s; one sound a sweep would be a drum roll.
	private const int SoundGapMs = 60;
	private const float Body = RiptideWave.BodyAllowance;
	// How far the lash wanders off its line, each side, at its middle (px). It flickers every
	// FlickerSeconds, the crackle that says the rope is lightning. It was 4 px when the arc was a
	// zigzag; a whip's curve is the shape now, and the wander only has to keep it alive.
	private const float Jag = 1.5f;
	private const float FlickerSeconds = 0.035f;

	private struct Arc
	{
		public Vector2 Dir;      // local: unit, along the arc
		public Vector2 Across;   // local: unit, the way the first pass sweeps
		public float HandAcross; // across position of the hand, where the arc leaves it
		public float Delay;      // seconds (tempo-scaled) before it starts
		public float Age;        // seconds (tempo-scaled) since it started
		public float Reach;
		public float HalfBand;
		public float Area;       // every Area term together; a Thunderclap grows by it
		public int Damage;
		public int Passes;       // 1, or 2 under Thunderclap
		public int PassesDone;   // passes whose end has been resolved
		public int Hits;         // ids in this arc's row of hitIds, for the pass under way
		public int Sparks;       // sparks drawn for the pass under way
		public float LastPass;   // the pass's progress last frame, 0..1, for the swept-range test
		public int PassIndex;    // which pass LastPass belongs to; -1 before the first
		public uint Seed;
		public bool Live;
	}

	private enum BurstKind
	{
		Spark,   // a hit: sparks off the body struck
		Crack,   // the lash's tip snapping straight at full reach
		Thunder, // Thunderclap's burst at the far end of a pass
	}

	private struct Burst
	{
		public Vector2 At;       // world
		public float Age;
		public float Radius;
		public uint Seed;
		public BurstKind Kind;
		public bool Live;
	}

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; private set; }

	// The numbers of the last cast. Each arc takes its own when it is queued.
	private int damage = 7;
	private float reach = 150f;
	private float halfBand = 22f;
	private float area = 1f;
	private float tempo = 1f;
	private bool thunder;
	private bool cardinal;
	private bool ironbound;
	private ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Lightning);
	private readonly ElementColors.Ramp spark = ElementColors.GetRamp(Element.Lightning);
	private Color tint = Colors.White;

	private readonly Arc[] arcs = new Arc[MaxArcs];
	private readonly ulong[] hitIds = new ulong[MaxArcs * MaxHitsPerPass];
	private readonly Burst[] bursts = new Burst[MaxBursts];
	private readonly List<Enemy> nearBuffer = new();
	private int nextBurst;
	private uint seedClock;
	private bool drewLastFrame;
	private float clock;

	// --- the lash (drawn by stormsweep_lash.gdshader; see "THE WHIP" above) ------------------------
	// The most arcs drawn at once - the shader's array size. Cardinal Storm at three sweeps a cast has
	// about twelve alive; past sixteen, the newest wait a frame or two for a slot to draw in.
	private const int MaxDrawn = 16;
	private const int LashPoints = Points + 1;
	// Half the lash's width at the hand (x the square root of Area, 4-8 px) and at the tip.
	private const float HandleHalfWidth = 5f;
	private const float TipHalfWidth = 1.2f;
	// How long the tip stays white after the crack, and how long the crack's flash lasts.
	private const float CrackSeconds = 0.05f;
	private const float CrackFlashSeconds = 0.08f;
	// How far beyond the band a lash's quad reaches: its width, its crackle and the crack on its tip.
	private const float QuadPad = 10f;

	// THE LASHES GO TO THE GPU AS A TEXTURE, NOT AS UNIFORM ARRAYS. They change every frame, and a
	// Variant of a packed array allocates a finalizable disposer on the C# side: five arrays a frame was
	// 0.3 KB a frame in the profiler. A float image of DataTexels x MaxDrawn, rewritten in place from one
	// byte buffer and pushed with ImageTexture.Update, crosses as native data and allocates nothing.
	// Row d is lash d; its texels are the points two to a texel (0-8), then shape, frame, smear and fx
	// (stormsweep_lash.gdshader reads them by these numbers).
	private const int PointTexels = (LashPoints + 1) / 2;
	private const int ShapeTexel = PointTexels;
	private const int FrameTexel = PointTexels + 1;
	private const int SmearTexel = PointTexels + 2;
	private const int FxTexel = PointTexels + 3;
	private const int DataTexels = PointTexels + 4;

	private static Shader lashShader;
	private static readonly StringName ParamLashData = "lash_data";
	private static readonly StringName ParamHandReach = "hand_reach";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamIronbound = "ironbound";
	private static readonly StringName ParamRampCore = "ramp_core";
	private static readonly StringName ParamRampHot = "ramp_hot";
	private static readonly StringName ParamRampMid = "ramp_mid";
	private static readonly StringName ParamRampEdge = "ramp_edge";
	private static readonly StringName ParamSparkCore = "spark_core";
	private static readonly StringName ParamSparkHot = "spark_hot";
	private static readonly StringName ParamSparkMid = "spark_mid";
	private static readonly StringName ParamTint = "tint";

	// The four arms of the crack's star, then the four of its diagonal turn.
	private static readonly Vector2[] CrackArms =
	{
		new(1f, 0f), new(0f, 1f), new(-1f, 0f), new(0f, -1f),
		new(0.7071f, 0.7071f), new(-0.7071f, 0.7071f), new(-0.7071f, -0.7071f), new(0.7071f, -0.7071f),
	};

	private Node2D lashLayer;
	private ShaderMaterial lashMaterial;
	private Image lashImage;
	private ImageTexture lashTexture;
	// The image's bytes: DataTexels x MaxDrawn texels of four floats.
	private readonly byte[] lashBytes = new byte[DataTexels * MaxDrawn * 16];
	private readonly Vector2[] lashPts = new Vector2[LashPoints];
	private readonly Vector2[] lashQuads = new Vector2[MaxDrawn * 4];
	private readonly Vector2[] quad = new Vector2[4];
	private readonly Vector2[] quadUv = new Vector2[4];
	private readonly Color[] quadColours = { Colors.White, Colors.White, Colors.White, Colors.White };
	private int drawn;
	private bool lashDrewLastFrame;

	public override void _Ready()
	{
		// Over the bodies it strikes and the status marks on them (3), under the HUD's own layer.
		ZAsRelative = false;
		ZIndex = 4;

		// The lash is a child drawn behind this node, so the sparks and the crack (drawn here) sit on it.
		lashShader ??= GD.Load<Shader>("res://scenes/shaders/stormsweep_lash.gdshader");
		lashMaterial = new ShaderMaterial { Shader = lashShader };
		lashImage = Image.CreateEmpty(DataTexels, MaxDrawn, false, Image.Format.Rgbaf);
		lashTexture = ImageTexture.CreateFromImage(lashImage);
		lashMaterial.SetShaderParameter(ParamLashData, lashTexture);
		lashLayer = new Node2D { Name = "Lash", ShowBehindParent = true, Material = lashMaterial };
		AddChild(lashLayer);
		lashLayer.Draw += DrawLashes;
		PaintLash();
	}

	// Disconnected on the way out and connected again on a way back in (_Ready runs only once).
	public override void _EnterTree()
	{
		if (lashLayer != null)
			lashLayer.Draw += DrawLashes;
	}

	public override void _ExitTree()
	{
		if (lashLayer != null)
			lashLayer.Draw -= DrawLashes;
	}

	/// <summary>
	/// One cast: <paramref name="count"/> sweeps (crosses, under Cardinal Storm) queued ArcGap apart,
	/// the first to <paramref name="facingSide"/>, alternating after it. Called by the fire loop every
	/// cooldown, which is also what keeps the node current through level-ups and milestones.
	/// </summary>
	public void Cast(SpellData spell, int count, int facingSide, float playerArea, float damageMultiplier, Color visualTint)
	{
		if (spell == null)
			return;
		SpellData = spell;
		int level = Math.Max(1, spell.CurrentLevel);
		damage = Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(level) * damageMultiplier));
		// Every area term once, here. The reach is the spell's range (its levels' Range lines and any
		// milestone's RangeBonus); the band is BaseHalfBand plus the levels' AreaSize lines (px); and
		// the player's Area and every milestone's AreaMultiplier scale both - "area makes it longer and
		// wider".
		area = MathF.Max(0.1f, playerArea * spell.GetAreaMultiplierAtLevel(level));
		reach = MathF.Max(HandReach + 40f, spell.GetRangeAtLevel(level) * area);
		halfBand = MathF.Max(8f, (BaseHalfBand + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, level)) * area);
		tempo = Mathf.Clamp(spell.GetSpeedMultiplier(), 0.25f, 4f);
		thunder = spell.HasEvolution(ThunderclapId);
		cardinal = spell.HasEvolution(CardinalStormId);
		Element? branch = spell.GetBranchElement();
		ironbound = branch == Element.Metal;
		ramp = ElementColors.GetRamp(branch ?? Element.Lightning);
		tint = visualTint;
		PaintLash();

		int side = facingSide >= 0 ? 1 : -1;
		count = Math.Clamp(count, 1, MaxArcsPerCast);
		for (int k = 0; k < count; k++)
		{
			float delay = k * ArcGap;
			if (cardinal)
			{
				// The whole cross at once; Amount repeats the cross rather than turning it.
				Queue(new Vector2(side, 0f), delay);
				Queue(new Vector2(-side, 0f), delay);
				Queue(Vector2.Down, delay);
				Queue(Vector2.Up, delay);
			}
			else
			{
				// The facing side first, then the other, alternating.
				Queue(new Vector2(k % 2 == 0 ? side : -side, 0f), delay);
			}
		}
	}

	private void Queue(Vector2 dir, float delay)
	{
		int slot = -1;
		for (int i = 0; i < MaxArcs; i++)
		{
			if (!arcs[i].Live)
			{
				slot = i;
				break;
			}
		}
		// Every slot is mid-sweep: the oldest is nearly done, and an arc more or less on a screen this
		// busy is invisible. The cap is what keeps the cost flat.
		if (slot < 0)
		{
			float oldest = float.MinValue;
			for (int i = 0; i < MaxArcs; i++)
			{
				if (arcs[i].Age > oldest)
				{
					oldest = arcs[i].Age;
					slot = i;
				}
			}
		}

		bool horizontal = MathF.Abs(dir.X) > 0.5f;
		arcs[slot] = new Arc
		{
			Dir = dir,
			// Over the top and down for a sweep to the side, and left to right for one up or down.
			Across = horizontal ? Vector2.Down : Vector2.Right,
			// The hand is a little above his centre; the arc leaves it and drops onto the band.
			HandAcross = horizontal ? -8f : 0f,
			Delay = delay,
			Reach = reach,
			HalfBand = halfBand,
			Area = area,
			Damage = damage,
			Passes = thunder ? 2 : 1,
			PassIndex = -1,
			Seed = ++seedClock,
			Live = true,
		};
	}

	public override void _Process(double delta)
	{
		var perfStart = FrameProfiler.Begin();
		float dt = (float)delta;
		clock += dt;
		bool any = false;
		for (int i = 0; i < MaxArcs; i++)
		{
			if (!arcs[i].Live)
				continue;
			any = true;
			Tick(i, dt * tempo);
		}
		for (int b = 0; b < MaxBursts; b++)
		{
			if (!bursts[b].Live)
				continue;
			any = true;
			bursts[b].Age += dt;
			if (bursts[b].Age >= ImpactDebris.LifeSeconds)
				bursts[b].Live = false;
		}
		// Once more after the last one goes, or its final frame would stay on screen with nothing to
		// clear it: a canvas item keeps what it last drew until it is redrawn.
		if (any || drewLastFrame)
			QueueRedraw();
		drewLastFrame = any;

		LayOutLashes();
		if (drawn > 0 || lashDrewLastFrame)
			lashLayer?.QueueRedraw();
		lashDrewLastFrame = drawn > 0;
		FrameProfiler.End(FrameProfiler.Section.Stormsweep, perfStart);
	}

	// --- the arc's clock ------------------------------------------------------------------------------

	private static float PassStart(int k) => LeapSeconds + k * (SweepSeconds + ResweepPause);

	private static float Lifetime(in Arc l) => PassStart(l.Passes - 1) + SweepSeconds + LingerSeconds;

	private void Tick(int i, float dt)
	{
		ref Arc l = ref arcs[i];
		if (l.Delay > 0f)
		{
			l.Delay -= dt;
			if (l.Delay > 0f)
				return;
			dt = -l.Delay;
			l.Delay = 0f;
			SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellStormsweep, ArcWorld(l, l.Reach * 0.5f, 0f), 0.06f, SfxCatalog.SpellStormsweep, SoundGapMs);
		}
		l.Age += dt;

		// The pass the clock is in now. Every pass from the one last worked on up to it is finished in
		// order, so a long frame that jumps a whole pass still sweeps all of it, and a pass that is over
		// is never started again.
		int current = -1;
		for (int k = 0; k < l.Passes; k++)
		{
			if (l.Age >= PassStart(k))
				current = k;
		}
		for (int k = Math.Max(0, l.PassIndex); k <= current; k++)
		{
			if (l.PassIndex != k)
			{
				// A new pass: a fresh hit list, so the second pass of a Thunderclap strikes again.
				l.PassIndex = k;
				l.LastPass = 0f;
				l.Hits = 0;
				l.Sparks = 0;
				if (k > 0)
					SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellStormsweep, ArcWorld(l, l.Reach * 0.5f, 0f), 0.06f, SfxCatalog.SpellStormsweep, SoundGapMs);
				// The crack: the lash has snapped straight at full reach and its tip goes off - sparks
				// no wider than a body, round the tip on the edge this pass starts from. A picture, not
				// a hit: nothing is struck here that the sweep does not cross.
				AddBurst(ArcWorld(l, l.Reach, PassAcross(l, k, 0f, 1f)), Body, BurstKind.Crack);
			}
			float c = Mathf.Clamp((l.Age - PassStart(k)) / SweepSeconds, 0f, 1f);
			// Only ground the arc moved across this frame. Once it has reached the far edge it strikes
			// nothing more - the linger is a picture of what was hit, not a line that keeps cutting.
			if (c > l.LastPass)
			{
				Sweep(i, k, l.LastPass, c);
				l.LastPass = c;
			}
			if (c >= 1f && l.PassesDone <= k)
			{
				l.PassesDone = k + 1;
				EndOfPass(l, k);
			}
		}

		if (l.Age >= Lifetime(l))
			l.Live = false;
	}

	// Where pass k's arc lies across the band at fraction s along it, c of the way through the pass.
	// The far end leads and the hand follows, so a sweeping arc, not a falling bar; at c = 0 the whole
	// arc is on the edge it starts from, at c = 1 on the far one.
	private static float PassAcross(in Arc l, int k, float c, float s)
	{
		float from = k % 2 == 0 ? -l.HalfBand : l.HalfBand;
		float lead = Smooth(Mathf.Clamp(c * 1.35f - (1f - s) * 0.35f, 0f, 1f));
		return from + (-from - from) * lead;
	}

	// Strikes everything whose centre the arc crossed between progress c0 and c1 of pass k: at the
	// enemy's distance along the arc, the band between the two across positions, widened by the body
	// it stands in. In the order the sweep reaches them is not needed - every one of them is struck.
	private void Sweep(int i, int k, float c0, float c1)
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		ref Arc l = ref arcs[i];
		Vector2 origin = PlayerRef.GlobalPosition;
		Vector2 centre = origin + l.Dir * (l.Reach * 0.5f);
		EnemyRegistry.CollectNear(centre, l.Reach * 0.5f + l.HalfBand + Body, nearBuffer);
		float span = MathF.Max(1f, l.Reach - HandReach);
		int row = i * MaxHitsPerPass;
		foreach (Enemy enemy in nearBuffer)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = enemy.GlobalPosition - origin;
			float along = offset.Dot(l.Dir);
			if (along < HandReach - Body || along > l.Reach + Body)
				continue;
			float s = Mathf.Clamp((along - HandReach) / span, 0f, 1f);
			float a0 = PassAcross(l, k, c0, s);
			float a1 = PassAcross(l, k, c1, s);
			float across = offset.Dot(l.Across);
			if (across < MathF.Min(a0, a1) - Body || across > MathF.Max(a0, a1) + Body)
				continue;

			ulong id = enemy.GetInstanceId();
			bool seen = false;
			for (int h = 0; h < l.Hits; h++)
			{
				if (hitIds[row + h] == id)
				{
					seen = true;
					break;
				}
			}
			if (seen)
				continue;
			if (l.Hits < MaxHitsPerPass)
				hitIds[row + l.Hits++] = id;

			if (l.Sparks < SparksPerPass)
			{
				l.Sparks++;
				AddBurst(enemy.GlobalPosition, Body, BurstKind.Spark);
			}
			PlayerRef.DealDamageToEnemy(enemy, l.Damage, source: SpellData);
		}
		nearBuffer.Clear();
	}

	private void EndOfPass(in Arc l, int k)
	{
		// A single pass ends quietly now: its discharge is the crack at the start of the pass, where the
		// lash snaps straight (Tick), and the lash springs back from here.
		if (l.Passes < 2)
			return;
		Vector2 end = ArcWorld(l, l.Reach, PassAcross(l, k, 1f, 1f));

		// Thunderclap: the arc's end bursts, and everything within ThunderRadius x Area takes a share of
		// the sweep. The burst is ImpactDebris thrown out to exactly the radius it tested.
		float radius = ThunderRadius * l.Area;
		AddBurst(end, radius, BurstKind.Thunder);
		SfxPlayer.Cast(Element.Lightning, end);
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		int amount = Math.Max(1, Mathf.RoundToInt(l.Damage * ThunderDamageFraction));
		float radiusSquared = radius * radius;
		EnemyRegistry.CollectNear(end, radius, nearBuffer);
		foreach (Enemy enemy in nearBuffer)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (enemy.GlobalPosition.DistanceSquaredTo(end) > radiusSquared)
				continue;
			PlayerRef.DealDamageToEnemy(enemy, amount, source: SpellData);
		}
		nearBuffer.Clear();
	}

	private void AddBurst(Vector2 at, float radius, BurstKind kind)
	{
		int slot = -1;
		for (int b = 0; b < MaxBursts; b++)
		{
			int s = (nextBurst + b) % MaxBursts;
			if (!bursts[s].Live)
			{
				slot = s;
				break;
			}
		}
		if (slot < 0)
			slot = nextBurst;
		nextBurst = (slot + 1) % MaxBursts;
		bursts[slot] = new Burst { At = at, Radius = radius, Seed = ++seedClock, Kind = kind, Live = true };
	}

	// A point on the arc's frame in world space: `along` out from the player's centre, `across` off it.
	private Vector2 ArcWorld(in Arc l, float along, float across)
	{
		Vector2 origin = PlayerRef != null && IsInstanceValid(PlayerRef) ? PlayerRef.GlobalPosition : GlobalPosition;
		return origin + l.Dir * along + l.Across * across;
	}

	// --- the lash ---------------------------------------------------------------------------------
	//
	// Every live arc's lash, laid out as LashPoints points in this node's frame (the player's), and
	// handed to stormsweep_lash.gdshader with what it needs to draw it: its widths and heat, its frame,
	// the smear of ground it has just swept and the crack on its tip - one row of the lash data texture
	// each, written in place into one byte buffer.

	// The shader's colours: the arc's ramp (Lightning, or Metal on Iron Current), the storm's own for
	// the crackle, a Legendary's gold over all of it. Set on every cast, which is when they can change.
	private void PaintLash()
	{
		if (lashMaterial == null)
			return;
		lashMaterial.SetShaderParameter(ParamHandReach, HandReach);
		lashMaterial.SetShaderParameter(ParamIronbound, ironbound);
		lashMaterial.SetShaderParameter(ParamRampCore, ramp.Core);
		lashMaterial.SetShaderParameter(ParamRampHot, ramp.Hot);
		lashMaterial.SetShaderParameter(ParamRampMid, ramp.Mid);
		lashMaterial.SetShaderParameter(ParamRampEdge, ramp.Edge);
		lashMaterial.SetShaderParameter(ParamSparkCore, spark.Core);
		lashMaterial.SetShaderParameter(ParamSparkHot, spark.Hot);
		lashMaterial.SetShaderParameter(ParamSparkMid, spark.Mid);
		lashMaterial.SetShaderParameter(ParamTint, tint);
	}

	private void LayOutLashes()
	{
		drawn = 0;
		for (int i = 0; i < MaxArcs && drawn < MaxDrawn; i++)
		{
			if (!arcs[i].Live || arcs[i].Delay > 0f)
				continue;
			Pose(arcs[i], drawn);
			drawn++;
		}
		if (drawn == 0 || lashImage == null)
			return;
		lashImage.SetData(DataTexels, MaxDrawn, false, Image.Format.Rgbaf, lashBytes);
		lashTexture.Update(lashImage);
		lashMaterial.SetShaderParameter(ParamTime, clock);
	}

	// One texel of the lash data: row d, texel x.
	private void PutTexel(int d, int x, Vector4 value) =>
		MemoryMarshal.Cast<byte, Vector4>(lashBytes.AsSpan())[d * DataTexels + x] = value;

	// One arc's pose for this frame, into row d of the lash data: which beat it is in decides the shape.
	private void Pose(in Arc l, int d)
	{
		float span = MathF.Max(1f, l.Reach - HandReach);
		uint flicker = (uint)(clock / FlickerSeconds);
		int last = l.Passes - 1;
		float sweepsEnd = PassStart(last) + SweepSeconds;
		float handle = Mathf.Clamp(HandleHalfWidth * MathF.Sqrt(l.Area), 4f, 8f);
		float heat = 1f;
		float dissolve = 0f;
		float crack = 0f;
		Vector4 smear = Vector4.Zero;
		float age = l.Age;

		if (age < LeapSeconds)
		{
			Unfurl(l, age / LeapSeconds, span, flicker);
		}
		else if (age < sweepsEnd)
		{
			int k = 0;
			for (int j = 1; j < l.Passes; j++)
			{
				if (age >= PassStart(j))
					k = j;
			}
			float since = age - PassStart(k);
			float c = Mathf.Clamp(since / SweepSeconds, 0f, 1f);
			// On exactly the curve the hit test sweeps, a ripple running down it as it goes.
			LayAlongBand(l, k, c, span, flicker, 2.5f * (1f - 0.6f * c), age);
			// Behind it, the band it has just crossed; in Thunderclap's pause at the far edge, all of it.
			smear = since < SweepSeconds
				? new Vector4(PassFrom(l, k), c, MathF.Max(0f, c - 0.55f), 1f)
				: new Vector4(PassFrom(l, k), 1f, 0f, 0.5f);
			crack = Mathf.Clamp(1f - since / CrackSeconds, 0f, 1f);
		}
		else
		{
			float v = Mathf.Clamp((age - sweepsEnd) / LingerSeconds, 0f, 1f);
			Recoil(l, v, span, flicker);
			heat = 1f - 0.85f * v;
			handle *= 1f - 0.4f * v;
			dissolve = Smooth((v - 0.45f) / 0.55f);
			float fade = 1f - v;
			smear = new Vector4(PassFrom(l, last), 1f, 0f, 0.7f * fade * fade);
		}

		for (int x = 0; x < PointTexels; x++)
		{
			Vector2 a = lashPts[2 * x];
			Vector2 b = 2 * x + 1 < LashPoints ? lashPts[2 * x + 1] : a;
			PutTexel(d, x, new Vector4(a.X, a.Y, b.X, b.Y));
		}
		PutTexel(d, ShapeTexel, new Vector4(handle, TipHalfWidth, heat, 0f));
		PutTexel(d, FrameTexel, new Vector4(l.Dir.X, l.Dir.Y, l.Reach, l.HalfBand));
		PutTexel(d, SmearTexel, smear);
		PutTexel(d, FxTexel, new Vector4(dissolve, crack, 0f, 0f));

		// The quad the shader draws it in: the band and its body allowance, from just behind the hand.
		float a0 = HandReach - QuadPad;
		float a1 = l.Reach + QuadPad;
		float h = l.HalfBand + QuadPad;
		int q = d * 4;
		lashQuads[q] = l.Dir * a0 - l.Across * h;
		lashQuads[q + 1] = l.Dir * a1 - l.Across * h;
		lashQuads[q + 2] = l.Dir * a1 + l.Across * h;
		lashQuads[q + 3] = l.Dir * a0 + l.Across * h;
	}

	// THE LEAP: the lash leaves the hand along the band's starting edge and unrolls. A loop rolls out
	// from the hand to the tip - the part past it folded back toward the hand, a radius into the band -
	// and shrinks as it goes, so at the end of the leap the whole lash lies straight along the edge:
	// exactly the pose the sweep starts from, and the moment the tip cracks.
	private void Unfurl(in Arc l, float u, float span, uint flicker)
	{
		float edge = PassFrom(l, 0);
		float into = edge < 0f ? 1f : -1f;
		// Its loop is at most a little over half the band across, so it never leaves the band.
		float unrolled = 0.15f + 0.85f * (1f - (1f - u) * (1f - u));
		float rho = MathF.Min(l.HalfBand * 0.6f, 13f) * (1f - unrolled) + 2f;
		float straight = unrolled * span;
		float loop = MathF.Min(MathF.PI * rho, MathF.Max(0f, span - straight));
		float back = MathF.Max(0f, MathF.Min(span - straight - loop, straight));
		float total = MathF.Max(1f, straight + loop + back);
		float cx = HandReach + straight;
		float cy = OffTheHand(l, edge, unrolled) + into * rho;
		for (int p = 0; p <= Points; p++)
		{
			float sigma = total * p / Points;
			float along, across;
			if (sigma <= straight)
			{
				along = HandReach + sigma;
				across = OffTheHand(l, edge, sigma / span);
			}
			else if (sigma <= straight + loop)
			{
				float theta = (sigma - straight) / rho;
				along = cx + rho * MathF.Sin(theta);
				across = cy - into * rho * MathF.Cos(theta);
			}
			else
			{
				along = cx - (sigma - straight - loop);
				across = OffTheHand(l, edge, (along - HandReach) / span) + into * 2f * rho;
			}
			across += Wander(l, flicker, p) * 0.6f;
			lashPts[p] = l.Dir * along + l.Across * across;
		}
	}

	// THE SWEEP: the lash on PassAcross itself - the curve the hit test sweeps, tip leading - with a
	// ripple of a couple of px running down it. The ripple and the wander stay well inside the body
	// allowance the hit is measured with.
	private void LayAlongBand(in Arc l, int k, float c, float span, uint flicker, float ripple, float phase)
	{
		for (int p = 0; p <= Points; p++)
		{
			float t = p / (float)Points;
			float across = OffTheHand(l, PassAcross(l, k, c, t), t)
				+ Wander(l, flicker, p)
				+ ripple * MathF.Sin(Mathf.Tau * (t * 1.6f - phase * 9f)) * MathF.Sin(Mathf.Pi * t);
			lashPts[p] = l.Dir * (HandReach + span * t) + l.Across * across;
		}
	}

	// THE LINGER: spent, the lash goes slack - sagging back into the band it swept - and springs back
	// toward the hand, a wave running up it, as the shader cools and dissolves it.
	private void Recoil(in Arc l, float v, float span, uint flicker)
	{
		float far = -PassFrom(l, l.Passes - 1);
		float into = far < 0f ? 1f : -1f;
		float length = 1f - 0.7f * v * v;
		float slack = Smooth(v * 2.5f) * MathF.Min(l.HalfBand * 0.55f, 14f) * (1f - 0.5f * v);
		for (int p = 0; p <= Points; p++)
		{
			float t = p / (float)Points;
			float along = HandReach + span * length * t;
			float sag = MathF.Sin(Mathf.Pi * t) * slack;
			float wave = MathF.Abs(MathF.Sin(Mathf.Tau * (t * 1.3f + v * 2.2f))) * 3f * (1f - v) * t;
			float across = OffTheHand(l, far, length * t) + into * (sag + wave) + Wander(l, flicker, p) * (1f - v);
			lashPts[p] = l.Dir * along + l.Across * across;
		}
	}

	// The across position of the edge pass k starts from (PassAcross at c = 0).
	private static float PassFrom(in Arc l, int k) => k % 2 == 0 ? -l.HalfBand : l.HalfBand;

	// Off the hand and onto the band over the first sixth of the arc's reach (t is along it, 0..1).
	private static float OffTheHand(in Arc l, float band, float t) =>
		l.HandAcross + (band - l.HandAcross) * Smooth(t * 6f);

	// Lightning wanders: a fresh offset every flicker, none at the hand or the tip, most in the middle.
	private static float Wander(in Arc l, uint flicker, int p)
	{
		if (p <= 0 || p >= Points)
			return 0f;
		float t = p / (float)Points;
		return (Hash(l.Seed + flicker * 131u, p) - 0.5f) * 2f * Jag * MathF.Sqrt(MathF.Sin(Mathf.Pi * t));
	}

	// One quad per lash, carrying its row in the UV; the shader draws the lash, its crackle and its
	// smear inside it and throws the rest away.
	private void DrawLashes()
	{
		var perfStart = FrameProfiler.Begin();
		for (int d = 0; d < drawn; d++)
		{
			int q = d * 4;
			quad[0] = lashQuads[q];
			quad[1] = lashQuads[q + 1];
			quad[2] = lashQuads[q + 2];
			quad[3] = lashQuads[q + 3];
			Vector2 row = new Vector2(d, 0f);
			quadUv[0] = quadUv[1] = quadUv[2] = quadUv[3] = row;
			lashLayer.DrawPrimitive(quad, quadColours, quadUv);
		}
		FrameProfiler.End(FrameProfiler.Section.Stormsweep, perfStart);
	}

	// --- the bursts -------------------------------------------------------------------------------

	public override void _Draw()
	{
		var perfStart = FrameProfiler.Begin();
		DrawBursts();
		FrameProfiler.End(FrameProfiler.Section.Stormsweep, perfStart);
	}

	private Color Ink(Color c, float alpha) => new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha * tint.A);

	private void DrawBursts()
	{
		Transform2D toLocal = GlobalTransform.AffineInverse();
		for (int b = 0; b < MaxBursts; b++)
		{
			if (!bursts[b].Live)
				continue;
			Burst burst = bursts[b];
			Vector2 at = toLocal * burst.At;
			switch (burst.Kind)
			{
				case BurstKind.Thunder:
					ImpactDebris.Draw(this, at, burst.Radius, burst.Age, burst.Seed, spark,
						ImpactDebris.CountFor(burst.Radius), tint);
					// The clap: a white flash at the arc's end for the first tenth of a second.
					if (burst.Age < 0.1f)
						DrawRect(WhipLimb.Cell(at, burst.Age < 0.05f ? 14f : 10f), Ink(spark.Core, 1f - burst.Age / 0.1f));
					break;
				case BurstKind.Crack:
					DrawCrack(at, burst);
					break;
				default:
					ImpactDebris.Draw(this, at, burst.Radius, burst.Age, burst.Seed, spark, 6, tint, streaks: false);
					if (burst.Age < 0.06f)
						DrawRect(WhipLimb.Cell(at, 6f), Ink(spark.Core, 1f - burst.Age / 0.06f));
					break;
			}
		}
	}

	// The crack at the tip: a white-hot star of four arms - square or diagonal by the burst's seed - that
	// shrinks over CrackFlashSeconds, and sparks thrown out to the burst's radius (a body), which is all
	// the reach the tip's flash may claim.
	private void DrawCrack(Vector2 at, in Burst burst)
	{
		ImpactDebris.Draw(this, at, burst.Radius, burst.Age, burst.Seed, spark, 8, tint);
		if (burst.Age >= CrackFlashSeconds)
			return;
		float u = burst.Age / CrackFlashSeconds;
		DrawRect(WhipLimb.Cell(at, u < 0.35f ? 8f : 4f), Ink(spark.Core, 1f));
		float arm = MathF.Min(burst.Radius - WhipLimb.Grid, u < 0.5f ? 10f : 6f);
		int first = (burst.Seed & 1u) == 1u ? 4 : 0;
		Color hot = Ink(u < 0.5f ? spark.Core : spark.Hot, 1f);
		for (int a = 0; a < 4; a++)
		{
			Vector2 dir = CrackArms[first + a];
			for (float r = 4f; r <= arm; r += WhipLimb.Grid)
				DrawRect(WhipLimb.Cell(at + dir * r, WhipLimb.Grid), hot);
		}
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	// A small integer hash to 0..1, so the flicker needs no stored state (ImpactDebris's).
	private static float Hash(uint seed, int i)
	{
		uint h = (uint)i * 374761393u + seed * 668265263u;
		h = (h ^ (h >> 13)) * 1274126177u;
		h ^= h >> 16;
		return (h & 0xFFFFFF) / 16777216f;
	}
}
