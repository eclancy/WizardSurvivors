using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Starfall: a drizzle of falling stars over the ground around the player, aimed at nothing.
//
// WHY IT EXISTS (.ai/spell-roster.md, "Starfall"). Every area spell in the roster decides where it
// lands: Meteor Swarm on the enemies, Gravewell ahead of one, Toxic Spore Burst among them, Solar
// Flare on the caster. This one does not. Stars land at random on the ground in a circle round the
// player, one at a time, all the time - so its value is COVERAGE, not aim. A crowd standing in the
// rain is hit; a crowd outside it is not, and the only aiming there is is the player walking the
// rain into them. It is the roster's one weather spell.
//
// CADENCE. The fire loop can only cast on a cooldown, and a volley every cooldown would be the
// fixed-timer burst the spell exists to avoid. So the cooldown is a CLOUDBURST: it hands this node a
// number of stars (the spell's count plus Amount), and the node lets them fall one at a time, spread
// across the next interval with a jittered gap. Cooldown reduction is a heavier rain, Amount is more
// stars, and nothing in the world ever shows a cast.
//
// ONE NODE, EVERY STAR. The node lives in the arena at the origin and draws in world coordinates, so
// a star that has landed stays where it fell however the player moves. Stars are slots in a fixed
// array, drawn in this node's _Draw on the 2 px art grid (WhipLimb.Cell); nothing is instanced or
// allocated per star, which is what lets dozens be in the air on a phone.
//
// THE ANIMATION IS THE HIT AREA (art-direction.md section 3, no area markers). A falling star is a
// white head with a short trail, slanting in from the upper left; it lands with a twinkle and a burst
// of light thrown out by ImpactDebris, whose front stops exactly on the radius that was tested. The
// twinkle's arms stop well inside it. Nothing is drawn on the ground before a star lands: the star in
// the sky is the only warning, as Meteor Swarm's rock is.
//
// The ascensions (SpellEvolutionCatalog.SetupStarfallEvolutions):
//   Starburst     - every star is heavy: it lands, swells for FuseSeconds and bursts on its (much
//                   wider) radius, throwing SparksPerBurst sparks that arc out past the rim and land
//                   as small stars of their own.
//   Constellation - every star stays lit where it fell, and is drawn to the nearest lit star by a
//                   line of light on the ground; the lines strike whatever they touch.
// Frostfall, the level 4 element path, repaints every star in Ice's ramp (IPaintsElementPath); its
// slow is the rider Player.DealDamageToEnemy applies.
public partial class Starfall : Node2D, IPaintsElementPath
{
	// Evolution ids. SpellEvolutionCatalog builds the options from these, this node reads them.
	public const string ClearNightId = "starfall_clear_night";
	public const string FrostfallId = "starfall_frostfall";
	public const string DownpourId = "starfall_downpour";
	public const string StarburstId = "starfall_starburst";
	public const string ConstellationId = "starfall_constellation";

	/// <summary>The most stars one cloudburst may hand over. Amount can push a count past this.</summary>
	public const int MaxStarsPerCloudburst = 24;

	// --- the rain (tuned in scenes/Starfall.tscn) -------------------------------------------------

	// The hit radius of one landing at level 1, before the levels' AreaSize (px), the player's Area
	// and every milestone's AreaMultiplier. Small on purpose: a drizzle is many small hits.
	[Export] public float BaseStarRadius { get; set; } = 28f;
	// Seconds from appearing in the sky to landing. The fall is the only warning, so it is long enough
	// to see and short enough that a star still lands where the crowd is.
	[Export] public float FallSeconds { get; set; } = 0.5f;
	// How far above its landing a star appears, and how far to the left per unit of drop: every star
	// slants in from the upper left, the key light's side (art-direction.md section 2).
	[Export] public float FallHeight { get; set; } = 180f;
	[Export] public float FallSlant { get; set; } = 0.42f;

	// --- Starburst --------------------------------------------------------------------------------
	public const float FuseSeconds = 0.3f;
	public const int SparksPerBurst = 3;
	public const float SparkDamageFraction = 0.4f;
	public const float SparkRadiusFraction = 0.4f;
	// Sparks land between these multiples of the burst radius from its centre - past the rim, so a
	// spark never lands inside the burst that threw it - after an arc of SparkArcHeight px.
	private const float SparkReachMin = 1.15f;
	private const float SparkReachMax = 1.6f;
	private const float SparkFlightSeconds = 0.38f;
	private const float SparkArcHeight = 46f;
	// A heavy star falls a little slower, so the eye has time to see it is a different thing.
	private const float HeavyFallScale = 1.25f;

	// --- Constellation ----------------------------------------------------------------------------
	public const float LingerSeconds = 2.5f;
	public const float LinkRange = 140f;
	public const float LinkTickSeconds = 0.5f;
	public const float LinkDamageFraction = 0.55f;
	// A lit star takes at most this many lines, so the web spreads rather than collapsing on one hub.
	private const int MaxLinksPerStar = 3;
	// The line is drawn 6 px wide (a 2 px core in a 6 px glow). It is tested against enemy CENTRES,
	// so it reaches half an enemy's body beyond that - RiptideWave's allowance - and what it strikes
	// is exactly what it is seen to touch.
	private const float LineHalfWidth = 3f;
	public static float LinkReach => LineHalfWidth + RiptideWave.BodyAllowance;

	// Gap between two landing twinkles' sounds. One sound a star would be a machine gun at level 8.
	private const int SoundGapMs = 90;
	// How long a landed star's twinkle lasts; the burst under it is ImpactDebris.LifeSeconds. Its arms
	// reach this share of the radius at most - the burst's front is what lands on the radius itself.
	private const float TwinkleSeconds = 0.3f;
	private const float TwinkleReach = 0.7f;
	// Debris per landing. ImpactDebris.CountFor gives up to 36 on a Starburst's radius; 24 still puts a
	// third of them - the front - on the rim, which is what shows the reach. A spark's burst is 8.
	private const int MaxDebris = 24;
	private const int SparkDebris = 8;
	private const float SparkFlashSeconds = 0.12f;

	private const int MaxStars = 128;
	private const int MaxLit = 24;
	private const int MaxLinks = 32;
	private const int MaxPendingSparks = 24;

	private enum StarKind : byte { Drop, Heavy, Spark }
	private enum Phase : byte { Falling, Fuse, Landed }

	private struct Star
	{
		public Vector2 Land;     // world
		public Vector2 Origin;   // world: where it appeared (a spark: the burst that threw it)
		public float Age;        // seconds since it appeared
		public float Fall;       // seconds to land
		public float PhaseAge;   // seconds in the current phase, once it has landed
		public float Radius;
		public int Damage;
		public uint Seed;
		public StarKind Kind;
		public Phase Phase;
		public bool Hit;
		public bool Live;
	}

	private struct Lit
	{
		public Vector2 At;
		public float Age;
		public float Life;
		public int Links;
		public uint Seed;
		public bool Live;
	}

	private struct Link
	{
		public Vector2 A;
		public Vector2 B;
		public float Age;
		public float Life;
		public float Flash;
		public uint Seed;
		public bool Live;
	}

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; private set; }

	// The sky as of the last cloudburst. Every star takes its numbers when it appears.
	private int damage = 5;
	private float starRadius = 28f;
	private float rainRadius = 190f;
	private bool bursting;
	private bool linking;
	private float linger = LingerSeconds;
	private ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Light);
	private Color tint = Colors.White;

	// The drizzle clock: stars owed by the last cloudburst, and the gap between two of them.
	private int owed;
	private float emitGap = 0.3f;
	private float nextGap;
	private float emitClock;

	private readonly Star[] stars = new Star[MaxStars];
	private readonly Lit[] lit = new Lit[MaxLit];
	private readonly Link[] links = new Link[MaxLinks];
	private readonly Star[] pendingSparks = new Star[MaxPendingSparks];
	private int pendingCount;
	private int nextStar;
	private int nextLit;
	private int nextLink;
	private int liveLinks;
	private float linkClock;
	private uint seedClock;
	private float clock;

	private readonly List<Enemy> nearBuffer = new();
	private readonly RandomNumberGenerator rng = new();
	private Node2D ground;

	public override void _Ready()
	{
		rng.Randomize();
		// The sky: over the bodies the stars fall among, as ImpactDebrisLayer is; under the HUD.
		ZIndex = 6;
		// The web: the Constellation's lit stars and lines, just under the falling stars and over the
		// bodies. It was drawn on the floor first (Bramble Seed's -1), and in a crowd the bodies hid every
		// line exactly where it struck - a line has to be seen crossing a body to be read as touching it.
		ground = new Node2D { Name = "StarfallWeb", ZAsRelative = false, ZIndex = 5 };
		AddChild(ground);
		ground.Draw += DrawGround;
		nextGap = 0f;
	}

	/// <summary>
	/// One cooldown's worth of rain: <paramref name="count"/> stars to let fall across the next
	/// <paramref name="interval"/> seconds, and the numbers they fall with. Called by the fire loop
	/// every cooldown, which is also what keeps the node current through level-ups and milestones.
	/// </summary>
	public void Cloudburst(SpellData spell, int count, float interval, float playerArea,
		float damageMultiplier, float durationMultiplier, Color visualTint)
	{
		if (spell == null)
			return;
		SpellData = spell;
		int level = Math.Max(1, spell.CurrentLevel);
		damage = Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(level) * damageMultiplier));
		// Every area term once, here: the levels' AreaSize lines (px), the player's Area, and every
		// milestone's AreaMultiplier through the collector. The rain's own width is the spell's range,
		// which only its levels move - Area makes each star bigger, not the rain thinner.
		starRadius = MathF.Max(8f, (BaseStarRadius + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, level))
			* playerArea * spell.GetAreaMultiplierAtLevel(level));
		rainRadius = MathF.Max(60f, spell.GetRangeAtLevel(level));
		bursting = spell.HasEvolution(StarburstId);
		linking = spell.HasEvolution(ConstellationId);
		linger = LingerSeconds * MathF.Max(0.1f, durationMultiplier);
		ramp = ElementColors.GetRamp(spell.GetBranchElement() ?? Element.Light);
		tint = visualTint;

		count = Math.Clamp(count, 1, MaxStarsPerCloudburst);
		emitGap = MathF.Max(0.02f, interval / count);
		// A backlog is capped at two cloudbursts, so a cooldown cut mid-rain cannot pour.
		owed = Math.Min(owed + count, count * 2);
	}

	public override void _Process(double delta)
	{
		var perfStart = FrameProfiler.Begin();
		float dt = (float)delta;
		clock += dt;
		TickDrizzle(dt);
		TickStars(dt);
		if (linking || liveLinks > 0)
			TickConstellation(dt);
		QueueRedraw();
		ground?.QueueRedraw();
		FrameProfiler.End(FrameProfiler.Section.Starfall, perfStart);
	}

	// --- the drizzle ------------------------------------------------------------------------------

	private void TickDrizzle(float dt)
	{
		emitClock += dt;
		while (owed > 0 && emitClock >= nextGap)
		{
			emitClock -= nextGap;
			owed--;
			LetOneFall();
			// Jittered, so a steady rain never reads as a metronome.
			nextGap = emitGap * rng.RandfRange(0.55f, 1.45f);
		}
		// Nothing owed: the clock may not bank time, or the next cloudburst would arrive as a volley.
		if (owed == 0)
			emitClock = MathF.Min(emitClock, nextGap);
	}

	private void LetOneFall()
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		// Uniform over the AREA of the rain (the square root), so the drizzle is as dense at its edge
		// as at the player's feet. It aims at nothing.
		float angle = rng.Randf() * Mathf.Tau;
		float distance = rainRadius * MathF.Sqrt(rng.Randf());
		Vector2 land = PlayerRef.GlobalPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
		float height = FallHeight * rng.RandfRange(0.85f, 1.15f);
		float slant = FallSlant * rng.RandfRange(0.8f, 1.2f);

		StarKind kind = bursting ? StarKind.Heavy : StarKind.Drop;
		AddStar(new Star
		{
			Land = land,
			Origin = land + new Vector2(-height * slant, -height),
			Fall = FallSeconds * rng.RandfRange(0.88f, 1.12f) * (kind == StarKind.Heavy ? HeavyFallScale : 1f),
			Radius = starRadius,
			Damage = damage,
			Seed = ++seedClock,
			Kind = kind,
			Phase = Phase.Falling,
			Live = true,
		});
	}

	private void AddStar(in Star star)
	{
		// The next free slot; the oldest when every one is live - on a sky that full one star more or
		// less is invisible, and the cap is what keeps the cost flat.
		for (int i = 0; i < MaxStars; i++)
		{
			int slot = (nextStar + i) % MaxStars;
			if (!stars[slot].Live)
			{
				nextStar = slot;
				break;
			}
		}
		stars[nextStar] = star;
		nextStar = (nextStar + 1) % MaxStars;
	}

	private void TickStars(float dt)
	{
		for (int i = 0; i < MaxStars; i++)
		{
			if (!stars[i].Live)
				continue;

			ref Star s = ref stars[i];
			s.Age += dt;
			switch (s.Phase)
			{
				case Phase.Falling:
					if (s.Age >= s.Fall)
					{
						s.PhaseAge = 0f;
						if (s.Kind == StarKind.Heavy)
						{
							// A heavy star lands and swells before it bursts: the swelling star is the
							// warning, never a ring round where it will hit.
							s.Phase = Phase.Fuse;
						}
						else
						{
							s.Phase = Phase.Landed;
							Land(ref s);
						}
					}
					break;
				case Phase.Fuse:
					s.PhaseAge += dt;
					if (s.PhaseAge >= FuseSeconds)
					{
						s.Phase = Phase.Landed;
						s.PhaseAge = 0f;
						Land(ref s);
					}
					break;
				default:
					s.PhaseAge += dt;
					if (s.PhaseAge >= ImpactDebris.LifeSeconds)
						s.Live = false;
					break;
			}
		}

		// Sparks thrown this frame join the sky after the walk, so a burst can never overwrite the
		// star that threw it.
		for (int i = 0; i < pendingCount; i++)
			AddStar(pendingSparks[i]);
		pendingCount = 0;
	}

	private void Land(ref Star s)
	{
		s.Hit = Strike(s.Land, s.Radius, s.Damage) > 0;
		SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellStarfall, s.Land, 0.08f, SfxCatalog.SpellStarfall, SoundGapMs);

		if (s.Kind == StarKind.Heavy)
			ThrowSparks(s);
		else if (s.Kind == StarKind.Drop && linking)
			Kindle(s.Land);
	}

	// Everything whose centre is inside the radius takes the hit - the same number the burst is drawn
	// out to. CollectNear is a superset; the live-position test below is the authority.
	private int Strike(Vector2 at, float radius, int amount)
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return 0;

		int hits = 0;
		float radiusSquared = radius * radius;
		EnemyRegistry.CollectNear(at, radius, nearBuffer);
		foreach (Enemy enemy in nearBuffer)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (enemy.GlobalPosition.DistanceSquaredTo(at) > radiusSquared)
				continue;
			PlayerRef.DealDamageToEnemy(enemy, amount, source: SpellData);
			hits++;
		}
		nearBuffer.Clear();
		return hits;
	}

	private void ThrowSparks(in Star burst)
	{
		int sparkDamage = Math.Max(1, Mathf.RoundToInt(burst.Damage * SparkDamageFraction));
		float sparkRadius = MathF.Max(8f, burst.Radius * SparkRadiusFraction);
		float turn = rng.Randf() * Mathf.Tau;
		for (int k = 0; k < SparksPerBurst && pendingCount < MaxPendingSparks; k++)
		{
			float angle = turn + k * Mathf.Tau / SparksPerBurst + rng.RandfRange(-0.35f, 0.35f);
			float reach = burst.Radius * rng.RandfRange(SparkReachMin, SparkReachMax);
			pendingSparks[pendingCount++] = new Star
			{
				Origin = burst.Land,
				Land = burst.Land + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach,
				Fall = SparkFlightSeconds * rng.RandfRange(0.9f, 1.1f),
				Radius = sparkRadius,
				Damage = sparkDamage,
				Seed = ++seedClock,
				Kind = StarKind.Spark,
				Phase = Phase.Falling,
				Live = true,
			};
		}
	}

	// --- Constellation ----------------------------------------------------------------------------

	private void Kindle(Vector2 at)
	{
		// The nearest lit star in reach that can still take a line.
		int best = -1;
		float bestSquared = LinkRange * LinkRange;
		for (int j = 0; j < MaxLit; j++)
		{
			if (!lit[j].Live || lit[j].Links >= MaxLinksPerStar)
				continue;
			float d = lit[j].At.DistanceSquaredTo(at);
			if (d < bestSquared && d > 4f)
			{
				bestSquared = d;
				best = j;
			}
		}

		int slot = NextFreeLit();
		lit[slot] = new Lit { At = at, Life = linger, Seed = ++seedClock, Live = true };

		if (best < 0 || best == slot)
			return;

		// A line lives as long as the older of its two stars.
		float life = lit[best].Life - lit[best].Age;
		if (life <= 0.05f)
			return;
		lit[best].Links++;
		lit[slot].Links++;
		int link = NextFreeLink();
		if (!links[link].Live)
			liveLinks++;
		links[link] = new Link { A = lit[best].At, B = at, Life = life, Seed = ++seedClock, Live = true };
	}

	// Both reuse the oldest slot when every one is live, the same rule as the stars.
	private int NextFreeLit()
	{
		for (int i = 0; i < MaxLit; i++)
		{
			int slot = (nextLit + i) % MaxLit;
			if (!lit[slot].Live)
			{
				nextLit = (slot + 1) % MaxLit;
				return slot;
			}
		}
		int oldest = nextLit;
		nextLit = (nextLit + 1) % MaxLit;
		return oldest;
	}

	private int NextFreeLink()
	{
		for (int i = 0; i < MaxLinks; i++)
		{
			int slot = (nextLink + i) % MaxLinks;
			if (!links[slot].Live)
			{
				nextLink = (slot + 1) % MaxLinks;
				return slot;
			}
		}
		int oldest = nextLink;
		nextLink = (nextLink + 1) % MaxLinks;
		return oldest;
	}

	private void TickConstellation(float dt)
	{
		for (int j = 0; j < MaxLit; j++)
		{
			if (!lit[j].Live)
				continue;
			lit[j].Age += dt;
			if (lit[j].Age >= lit[j].Life)
				lit[j].Live = false;
		}

		for (int j = 0; j < MaxLinks; j++)
		{
			if (!links[j].Live)
				continue;
			links[j].Age += dt;
			links[j].Flash = MathF.Max(0f, links[j].Flash - dt);
			if (links[j].Age >= links[j].Life)
			{
				links[j].Live = false;
				liveLinks--;
			}
		}

		linkClock += dt;
		if (linkClock < LinkTickSeconds)
			return;
		linkClock -= LinkTickSeconds;
		StrikeLinks();
	}

	// Every enemy touching any line takes one hit a tick, however many lines it stands across.
	private void StrikeLinks()
	{
		if (liveLinks <= 0 || PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		int amount = Math.Max(1, Mathf.RoundToInt(damage * LinkDamageFraction));
		float reachSquared = LinkReach * LinkReach;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;
			for (int j = 0; j < MaxLinks; j++)
			{
				if (!links[j].Live)
					continue;
				if (DistanceSquaredToSegment(p, links[j].A, links[j].B) > reachSquared)
					continue;
				links[j].Flash = 0.18f;
				PlayerRef.DealDamageToEnemy(enemy, amount, source: SpellData);
				break;
			}
		}
	}

	private static float DistanceSquaredToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float lengthSquared = ab.LengthSquared();
		float t = lengthSquared > 0.0001f ? Mathf.Clamp((p - a).Dot(ab) / lengthSquared, 0f, 1f) : 0f;
		return p.DistanceSquaredTo(a + ab * t);
	}

	// --- drawing ----------------------------------------------------------------------------------

	public override void _Draw()
	{
		var perfStart = FrameProfiler.Begin();
		DrawSky();
		FrameProfiler.End(FrameProfiler.Section.Starfall, perfStart);
	}

	private void DrawSky()
	{
		Transform2D toLocal = GlobalTransform.AffineInverse();
		for (int i = 0; i < MaxStars; i++)
		{
			if (!stars[i].Live)
				continue;
			Star s = stars[i];
			switch (s.Phase)
			{
				case Phase.Falling:
					DrawFalling(toLocal, s);
					break;
				case Phase.Fuse:
					DrawFuse(toLocal * s.Land, s);
					break;
				default:
					Vector2 at = toLocal * s.Land;
					if (s.Kind == StarKind.Spark)
					{
						// A spark lands with a white flash and a small burst, no twinkle: Starburst throws
						// three a detonation, and a full twinkle on each was more than half the spell's
						// draw calls (profiled at level 8: ~740 calls a frame, ~330 after).
						ImpactDebris.Draw(this, at, s.Radius, s.PhaseAge, s.Seed, ramp, SparkDebris, tint);
						if (s.PhaseAge < SparkFlashSeconds)
							Square(this, at, 6f, Ink(ramp.Core, 1f - s.PhaseAge / SparkFlashSeconds));
					}
					else
					{
						ImpactDebris.Draw(this, at, s.Radius, s.PhaseAge, s.Seed, ramp,
							Math.Min(ImpactDebris.CountFor(s.Radius), MaxDebris), tint);
						DrawTwinkle(this, at, s.Radius * TwinkleReach, s.PhaseAge, s.Hit, s.Kind == StarKind.Heavy ? 6f : 4f);
					}
					break;
			}
		}
	}

	private Color Ink(Color c, float alpha) => new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha * tint.A);

	private void Square(CanvasItem canvas, Vector2 at, float size, Color color) =>
		canvas.DrawRect(WhipLimb.Cell(at, size), color);

	// One run of a twinkle's arm, from..to along an axis, as a single rectangle snapped to the 2 px
	// grid - the same pixels a square per step would stamp, in one draw call instead of ten.
	private static void Bar(CanvasItem canvas, Vector2 at, Vector2 axis, float from, float to, float thick, Color color)
	{
		if (to <= from)
			return;
		Vector2 a = at + axis * from;
		Vector2 b = at + axis * to;
		Vector2 half = new Vector2(MathF.Abs(axis.Y), MathF.Abs(axis.X)) * (thick * 0.5f);
		float x0 = Snap(MathF.Min(a.X, b.X) - half.X);
		float y0 = Snap(MathF.Min(a.Y, b.Y) - half.Y);
		float x1 = Snap(MathF.Max(a.X, b.X) + half.X);
		float y1 = Snap(MathF.Max(a.Y, b.Y) + half.Y);
		canvas.DrawRect(new Rect2(x0, y0, MathF.Max(WhipLimb.Grid, x1 - x0), MathF.Max(WhipLimb.Grid, y1 - y0)), color);
	}

	private static float Snap(float v) => MathF.Round(v / WhipLimb.Grid) * WhipLimb.Grid;

	private static Vector2 SnapPoint(Vector2 p) => new Vector2(Snap(p.X), Snap(p.Y));

	private static readonly Vector2[] Axes = { Vector2.Right, Vector2.Left, Vector2.Down, Vector2.Up };

	// A white head and a short trail, slanting in from the upper left; a spark arcs instead.
	private void DrawFalling(Transform2D toLocal, in Star s)
	{
		float u = Mathf.Clamp(s.Age / s.Fall, 0f, 1f);
		Vector2 head;
		Vector2 heading;
		if (s.Kind == StarKind.Spark)
		{
			float lift = 4f * SparkArcHeight * u * (1f - u);
			head = s.Origin.Lerp(s.Land, u) + new Vector2(0f, -lift);
			heading = (s.Land - s.Origin) + new Vector2(0f, -4f * SparkArcHeight * (1f - 2f * u));
		}
		else
		{
			// Accelerating: a star drops, it does not glide.
			float eased = MathF.Pow(u, 1.6f);
			head = s.Origin.Lerp(s.Land, eased);
			heading = s.Land - s.Origin;
		}
		heading = heading.LengthSquared() > 0.0001f ? heading.Normalized() : Vector2.Down;
		head = toLocal * head;

		bool heavy = s.Kind == StarKind.Heavy;
		bool spark = s.Kind == StarKind.Spark;
		float fadeIn = Mathf.Clamp(s.Age / 0.08f, 0f, 1f);

		// Sized as a projectile is (art-direction.md section 1: a 32 px cell at x2), not as a mote. The
		// first build drew a 4 px head with a 21 px trail, which at the camera's 0.85 is three screen
		// pixels - the rain was there and nobody could see it.
		//
		// The trail: three continuous runs back along the heading - thick and hot at the head, then
		// mid, then a thin gold edge - never drawn behind where the star appeared. Stamped squares at a
		// step read as a dotted line and faded before the eye found them; three lines are three draw
		// calls, against twelve squares.
		float length = (heavy ? 72f : spark ? 22f : 56f) * MathF.Min(1f, (toLocal * s.Origin).DistanceTo(head) / (heavy ? 72f : spark ? 22f : 56f));
		float thick = heavy ? 6f : spark ? 2f : 4f;
		Vector2 tail = -heading * length;
		Vector2 a = SnapPoint(head);
		Vector2 b = SnapPoint(head + tail * 0.3f);
		Vector2 c2 = SnapPoint(head + tail * 0.65f);
		Vector2 d = SnapPoint(head + tail);
		DrawLine(c2, d, Ink(ramp.Edge, fadeIn * 0.55f), 2f);
		DrawLine(b, c2, Ink(ramp.Mid, fadeIn * 0.8f), MathF.Max(2f, thick - 2f));
		DrawLine(a, b, Ink(ramp.Hot, fadeIn), thick);

		// The head: a diamond of glow, a white core, and four glints that flicker so it twinkles as
		// it falls.
		float core = heavy ? 10f : spark ? 4f : 6f;
		bool flick = ((int)(clock * 14f + s.Seed)) % 2 == 0;
		float glint = (heavy ? 14f : spark ? 6f : 10f) * (flick ? 1f : 0.7f);
		float halo = core + (heavy ? 6f : 4f);
		Color glow = Ink(ramp.Mid, fadeIn * 0.55f);
		Bar(this, head, Vector2.Right, -halo * 0.5f, halo * 0.5f, core - 2f, glow);
		Bar(this, head, Vector2.Down, -halo * 0.5f, halo * 0.5f, core - 2f, glow);
		Color hot = Ink(ramp.Hot, fadeIn);
		foreach (Vector2 axis in Axes)
			Bar(this, head, axis, core * 0.5f, glint, 2f, hot);
		Square(this, head, core, Ink(ramp.Core, fadeIn));
	}

	// A heavy star on the ground, swelling and flickering faster until it goes. Motes are drawn in
	// toward it from inside its radius: it gathers, it does not mark.
	private void DrawFuse(Vector2 at, in Star s)
	{
		float t = Mathf.Clamp(s.PhaseAge / FuseSeconds, 0f, 1f);
		float arm = Mathf.Lerp(6f, MathF.Max(8f, s.Radius * 0.35f), t);
		bool flick = ((int)(clock * (18f + t * 30f) + s.Seed)) % 2 == 0;
		Color hot = Ink(ramp.Hot, 1f);
		Color mid = Ink(ramp.Mid, 0.9f);
		foreach (Vector2 axis in Axes)
		{
			Bar(this, at, axis, 4f, arm * 0.5f, flick ? 4f : 2f, hot);
			Bar(this, at, axis, arm * 0.5f, arm, 2f, mid);
		}
		float gather = s.Radius * 0.4f * (1f - t);
		for (int k = 0; k < 6; k++)
		{
			float a = k * Mathf.Tau / 6f + s.Seed * 0.7f;
			Square(this, at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (gather + 6f), 2f, mid);
		}
		Square(this, at, flick ? 10f : 8f, Ink(ramp.Core, 1f));
	}

	// The landing twinkle: four long arms and four short ones, out fast and drawn back in. Arms stop
	// at reach, which callers keep well inside the radius the burst under it reaches.
	private void DrawTwinkle(CanvasItem canvas, Vector2 at, float reach, float age, bool hit, float diagonalStep)
	{
		if (age >= TwinkleSeconds)
			return;
		const float rise = 0.05f;
		float k = age < rise ? age / rise : 1f - (age - rise) / (TwinkleSeconds - rise);
		float arm = reach * k * (hit ? 1f : 0.8f);
		float alpha = age < TwinkleSeconds * 0.6f ? 1f : (TwinkleSeconds - age) / (TwinkleSeconds * 0.4f);
		// Each arm cools outward in three runs: white and thick at the heart, then hot, then mid.
		Color core = Ink(ramp.Core, alpha);
		Color hot = Ink(ramp.Hot, alpha);
		Color mid = Ink(ramp.Mid, alpha);
		foreach (Vector2 axis in Axes)
		{
			Bar(canvas, at, axis, 0f, arm * 0.3f, 6f, core);
			Bar(canvas, at, axis, arm * 0.3f, arm * 0.65f, 4f, hot);
			Bar(canvas, at, axis, arm * 0.65f, arm, 2f, mid);
		}
		// The short diagonal arms, stamped every other grid step: a sparkle, at half the calls.
		float diagonal = arm * 0.45f;
		for (float d = 3f; d <= diagonal; d += diagonalStep)
		{
			Color c = Ink(d < diagonal * 0.5f ? ramp.Hot : ramp.Mid, alpha);
			Square(canvas, at + new Vector2(d, d), 2f, c);
			Square(canvas, at + new Vector2(-d, d), 2f, c);
			Square(canvas, at + new Vector2(d, -d), 2f, c);
			Square(canvas, at + new Vector2(-d, -d), 2f, c);
		}
		Square(canvas, at, hit ? 10f : 8f, Ink(ramp.Core, alpha));
	}

	// The Constellation: the lit stars and the lines between them, over the bodies. A line is a 2 px
	// core over a 6 px glow (the width LinkReach is measured from) with one bright mote running along
	// it - three draw calls a line. It flashes white along its length on the tick it strikes.
	private void DrawGround()
	{
		if (ground == null || (!linking && liveLinks <= 0))
			return;
		var perfStart = FrameProfiler.Begin();
		DrawWeb();
		FrameProfiler.End(FrameProfiler.Section.Starfall, perfStart);
	}

	private void DrawWeb()
	{
		Transform2D toLocal = ground.GlobalTransform.AffineInverse();

		for (int j = 0; j < MaxLinks; j++)
		{
			if (!links[j].Live)
				continue;
			Link l = links[j];
			float left = l.Life - l.Age;
			float fade = Mathf.Clamp(MathF.Min(l.Age / 0.12f, left / 0.3f), 0f, 1f);
			bool flashing = l.Flash > 0f;
			Vector2 a = toLocal * l.A;
			Vector2 b = toLocal * l.B;
			float length = a.DistanceTo(b);
			if (length < 2f)
				continue;
			Vector2 dir = (b - a) / length;
			Color glow = Ink(ramp.Mid, (flashing ? 0.6f : 0.32f) * fade);
			Color line = Ink(flashing ? ramp.Core : ramp.Hot, (flashing ? 1f : 0.85f) * fade);
			ground.DrawLine(a, b, glow, LineHalfWidth * 2f);
			ground.DrawLine(a, b, line, 2f);
			float run = (clock * 90f + l.Seed * 13f) % length;
			Square(ground, a + dir * run, 4f, Ink(ramp.Core, fade));
		}

		for (int j = 0; j < MaxLit; j++)
		{
			if (!lit[j].Live)
				continue;
			Lit s = lit[j];
			float left = s.Life - s.Age;
			float fade = Mathf.Clamp(MathF.Min(s.Age / 0.1f, left / 0.3f), 0f, 1f);
			float pulse = 0.75f + 0.25f * MathF.Sin(clock * 7f + s.Seed);
			Vector2 at = toLocal * s.At;
			Color hot = Ink(ramp.Hot, fade * pulse);
			bool flick = ((int)(clock * 6f + s.Seed)) % 3 == 0;
			float arm = flick ? 10f : 7f;
			// Two bars through the centre and a core: three calls a lit star, at up to 24 of them.
			Bar(ground, at, Vector2.Right, -arm, arm, 2f, hot);
			Bar(ground, at, Vector2.Down, -arm, arm, 2f, hot);
			Square(ground, at, 6f, Ink(ramp.Core, fade));
		}
	}
}
