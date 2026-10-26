using Godot;
using System;
using System.Collections.Generic;

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
	// The arc is laid out as this many segments, hand to far end.
	private const int Points = 16;
	// Hit sparks drawn for one pass, at most - the rest of the crowd it struck still flinches (Enemy's
	// own hit flash), but forty sparks on one frame is noise, not information.
	private const int SparksPerPass = 6;
	// Gap between two sweep sounds. Cardinal Storm sweeps four at once, and three sweeps a cast at
	// level 8 is one every 0.14 s; one sound a sweep would be a drum roll.
	private const int SoundGapMs = 60;
	private const float Body = RiptideWave.BodyAllowance;
	// How far the arc wanders off its line, each side, at its middle (px). It flickers every
	// FlickerSeconds, which is what makes a line read as lightning rather than as a drawn stroke.
	private const float Jag = 4f;
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

	private struct Burst
	{
		public Vector2 At;       // world
		public float Age;
		public float Radius;
		public uint Seed;
		public bool Thunder;
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
	private readonly Vector2[] line = new Vector2[Points + 1];
	private readonly Vector2[] ghost = new Vector2[Points + 1];
	private readonly List<Enemy> nearBuffer = new();
	private int nextBurst;
	private uint seedClock;
	private bool drewLastFrame;
	private float clock;

	public override void _Ready()
	{
		// Over the bodies it strikes and the status marks on them (3), under the HUD's own layer.
		ZAsRelative = false;
		ZIndex = 4;
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
				AddBurst(enemy.GlobalPosition, Body, false);
			}
			PlayerRef.DealDamageToEnemy(enemy, l.Damage, source: SpellData);
		}
		nearBuffer.Clear();
	}

	private void EndOfPass(in Arc l, int k)
	{
		Vector2 end = ArcWorld(l, l.Reach, PassAcross(l, k, 1f, 1f));
		if (l.Passes < 2)
		{
			// The discharge at the arc's end: a spark no wider than a body. Not a hit area - nothing is
			// struck here that the arc did not cross.
			AddBurst(end, Body, false);
			return;
		}

		// Thunderclap: the arc's end bursts, and everything within ThunderRadius x Area takes a share of
		// the sweep. The burst is ImpactDebris thrown out to exactly the radius it tested.
		float radius = ThunderRadius * l.Area;
		AddBurst(end, radius, true);
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

	private void AddBurst(Vector2 at, float radius, bool isThunder)
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
		bursts[slot] = new Burst { At = at, Radius = radius, Seed = ++seedClock, Thunder = isThunder, Live = true };
	}

	// A point on the arc's frame in world space: `along` out from the player's centre, `across` off it.
	private Vector2 ArcWorld(in Arc l, float along, float across)
	{
		Vector2 origin = PlayerRef != null && IsInstanceValid(PlayerRef) ? PlayerRef.GlobalPosition : GlobalPosition;
		return origin + l.Dir * along + l.Across * across;
	}

	// --- drawing ----------------------------------------------------------------------------------

	public override void _Draw()
	{
		var perfStart = FrameProfiler.Begin();
		for (int i = 0; i < MaxArcs; i++)
		{
			if (arcs[i].Live && arcs[i].Delay <= 0f)
				DrawArc(arcs[i]);
		}
		DrawBursts();
		FrameProfiler.End(FrameProfiler.Section.Stormsweep, perfStart);
	}

	private Color Ink(Color c, float alpha) => new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha * tint.A);

	private void DrawArc(in Arc l)
	{
		float age = l.Age;
		float fade = 1f;
		int k = -1;
		float c = 0f;
		float endAlong = l.Reach;
		// Which beat it is in, and the arc's pose for it.
		if (age < LeapSeconds)
		{
			// Leaping out along the starting edge, the far end racing ahead.
			float u = age / LeapSeconds;
			endAlong = HandReach + (l.Reach - HandReach) * (1f - (1f - u) * (1f - u) * (1f - u));
		}
		else
		{
			for (int j = 0; j < l.Passes; j++)
			{
				if (age >= PassStart(j))
				{
					k = j;
					c = Mathf.Clamp((age - PassStart(j)) / SweepSeconds, 0f, 1f);
				}
			}
			float end = PassStart(l.Passes - 1) + SweepSeconds;
			if (age > end)
				fade = 1f - Mathf.Clamp((age - end) / LingerSeconds, 0f, 1f);
		}

		uint flicker = (uint)(clock / FlickerSeconds);
		// The afterglow: where the pass has already swept, three fading ghosts of the arc behind it. In
		// the linger they settle into five even lines across the swept band, fading out - the last look
		// at exactly the ground that was struck. The first build drew them in the edge tone at a third
		// opacity and the capture could not find the band; they are the mid tone now, and the nearest
		// ghost is as thick as the arc's own body.
		if (k >= 0)
		{
			if (c < 1f)
			{
				for (int g = 1; g <= 3; g++)
				{
					float cg = c - g * 0.2f;
					if (cg <= 0f)
						break;
					LayOut(l, k, cg, l.Reach, flicker + (uint)g * 7u, ghost, 0.6f);
					DrawPolyline(ghost, Ink(ramp.Mid, 0.75f - g * 0.18f), g == 1 ? 4f : 2f);
				}
			}
			else if (fade > 0.05f)
			{
				for (int g = 0; g < 5; g++)
				{
					float cg = g / 4f;
					LayOut(l, k, cg, l.Reach, flicker + (uint)g * 11u, ghost, 0.5f);
					DrawPolyline(ghost, Ink(ramp.Mid, 0.5f * fade * fade), 2f);
				}
			}
		}

		if (fade <= 0.02f)
			return;
		LayOut(l, k, c, endAlong, flicker, line, 1f);
		if (ironbound)
			DrawIronCurrent(fade, flicker);
		else
		{
			// Sized against the bodies it strikes: a 52 px skeleton dwarfed the first 4 px arc, and on a
			// phone it was a hair.
			DrawPolyline(line, Ink(ramp.Edge, 0.6f * fade), 12f);
			DrawPolyline(line, Ink(ramp.Mid, 0.95f * fade), 6f);
			DrawPolyline(line, Ink(ramp.Core, fade), 2f);
		}
		DrawForks(l, flicker, fade);
	}

	// Fills `points` with the arc at pass k, progress c (k = -1: leaping out along the starting edge),
	// reaching endAlong, jittered by `jag` of Jag and snapped to the 2 px art grid.
	private void LayOut(in Arc l, int k, float c, float endAlong, uint flicker, Vector2[] points, float jag)
	{
		float span = MathF.Max(1f, l.Reach - HandReach);
		for (int p = 0; p <= Points; p++)
		{
			float t = p / (float)Points;
			float along = HandReach + (endAlong - HandReach) * t;
			float s = Mathf.Clamp((along - HandReach) / span, 0f, 1f);
			float band = k < 0 ? -l.HalfBand + 3f * MathF.Sin(t * 9f + clock * 30f) * t : PassAcross(l, k, c, s);
			// Off the hand and onto the band in the first sixth of the arc.
			float drop = Smooth(Mathf.Clamp(t * 6f, 0f, 1f));
			float across = l.HandAcross + (band - l.HandAcross) * drop;
			// Lightning wanders: a fresh offset every flicker, none at the hand, most in the middle.
			if (p > 0 && p < Points)
				across += (Hash(l.Seed + flicker * 131u, p) - 0.5f) * 2f * Jag * jag * MathF.Sqrt(MathF.Sin(Mathf.Pi * t));
			Vector2 local = l.Dir * along + l.Across * across;
			points[p] = new Vector2(Snap(local.X), Snap(local.Y));
		}
	}

	// Two or three short forks off the arc, re-chosen every flicker - the crackle along its length.
	private void DrawForks(in Arc l, uint flicker, float fade)
	{
		Color hot = Ink(spark.Hot, fade);
		for (int f = 0; f < 3; f++)
		{
			float h = Hash(l.Seed * 7u + flicker, 40 + f);
			if (h < 0.35f)
				continue;
			int p = 3 + (int)(Hash(l.Seed + flicker * 3u, 50 + f) * (Points - 5));
			Vector2 from = line[p];
			Vector2 dir = (line[p + 1] - line[p]).Normalized();
			Vector2 normal = new Vector2(-dir.Y, dir.X) * (Hash(l.Seed + flicker, 60 + f) < 0.5f ? -1f : 1f);
			Vector2 mid = from + dir * 5f + normal * 6f;
			Vector2 end = mid + dir * 4f + normal * 5f;
			mid = new Vector2(Snap(mid.X), Snap(mid.Y));
			end = new Vector2(Snap(end.X), Snap(end.Y));
			DrawLine(from, mid, hot, 2f);
			DrawLine(mid, end, Ink(spark.Mid, fade * 0.8f), 2f);
		}
	}

	// Iron Current: the storm runs through iron. Beads of iron strung along the arc on the 2 px grid in
	// Metal's ramp, an occlusion line under them so they read on the floor, and a thread of Lightning
	// crawling over them - it is still the storm, carrying iron.
	private void DrawIronCurrent(float fade, uint flicker)
	{
		DrawPolyline(line, Ink(new Color(0.02f, 0.027f, 0.047f), 0.85f * fade), 8f);
		for (int p = 0; p < Points; p++)
		{
			Vector2 a = line[p];
			Vector2 b = line[p + 1];
			float length = a.DistanceTo(b);
			int beads = Math.Max(1, (int)(length / 6f));
			for (int n = 0; n < beads; n++)
			{
				Vector2 at = a.Lerp(b, (n + 0.5f) / beads);
				bool odd = ((p * 3 + n) & 1) == 1;
				// Alternate beads long and square, so a row of them reads as matter strung on the current.
				Vector2 size = odd ? new Vector2(6f, 4f) : new Vector2(4f, 4f);
				Rect2 rect = new Rect2(Snap(at.X - size.X * 0.5f), Snap(at.Y - size.Y * 0.5f), size.X, size.Y);
				DrawRect(rect, Ink(odd ? ramp.Mid : ramp.Hot, fade));
				if (odd)
					DrawRect(new Rect2(rect.Position + new Vector2(2f, 0f), new Vector2(2f, 2f)), Ink(ramp.Edge, fade));
			}
		}
		// The current: a thin white thread jittered fresh every flicker, so it crawls along the beads.
		Vector2 previous = line[0];
		for (int p = 1; p <= Points; p++)
		{
			Vector2 at = line[p] + new Vector2(0f, (Hash(flicker * 17u, p) - 0.5f) * 4f);
			at = new Vector2(Snap(at.X), Snap(at.Y));
			DrawLine(previous, at, Ink(spark.Core, fade * 0.9f), 2f);
			previous = at;
		}
	}

	private void DrawBursts()
	{
		Transform2D toLocal = GlobalTransform.AffineInverse();
		for (int b = 0; b < MaxBursts; b++)
		{
			if (!bursts[b].Live)
				continue;
			Burst burst = bursts[b];
			Vector2 at = toLocal * burst.At;
			if (burst.Thunder)
			{
				ImpactDebris.Draw(this, at, burst.Radius, burst.Age, burst.Seed, spark,
					ImpactDebris.CountFor(burst.Radius), tint);
				// The clap: a white flash at the arc's end for the first tenth of a second.
				if (burst.Age < 0.1f)
					DrawRect(WhipLimb.Cell(at, burst.Age < 0.05f ? 14f : 10f), Ink(spark.Core, 1f - burst.Age / 0.1f));
			}
			else
			{
				ImpactDebris.Draw(this, at, burst.Radius, burst.Age, burst.Seed, spark, 6, tint, streaks: false);
				if (burst.Age < 0.06f)
					DrawRect(WhipLimb.Cell(at, 6f), Ink(spark.Core, 1f - burst.Age / 0.06f));
			}
		}
	}

	// WhipLimb is the project's 2 px art-grid helper (shared with the tentacles and the vine); the name
	// is the class's, not this spell's.
	private static float Snap(float v) => MathF.Round(v / WhipLimb.Grid) * WhipLimb.Grid;

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
