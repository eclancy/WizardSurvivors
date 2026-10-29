using Godot;
using System;
using WizardSurvivors.scripts;

// Bramble Seed: a seed dropped at the player's feet that grows into a thorn patch over several
// seconds (.ai/side-events.md). Grown back in the corrupted grove of the Enchanted Forest.
//
// **The payload nothing else has: it gets bigger the longer it lives.** Every zone in the game is
// at full size the moment it appears. A seed is small and harmless for its first seconds and wide
// and dangerous at the end, so its value is in where the player will be later rather than where
// they are now - plant, kite a loop, and drag the swarm back across what has grown.
//
// **Pure Grass.** Grass had no pure spell, so it could never reach its capstone.
//
// At most MaxAlive patches stand at once; a new seed past that withers the oldest.
//
// **Ironbriar** (the level 4 element path into Metal, ElementPaths): the living green of the roots,
// their thorns and tips are repainted in Metal's ramp - steel thorns on bark.
//
// **Its own four cards** (2026-10-29, .ai/spell-roster.md section 2e), each something the thicket
// visibly does or leaves. The ids are the template's, so Vainamoinen's path (Burrs, then Bramble Keep)
// and every save carry over. None of them sows another patch: a planting that becomes several is
// Wildwood's verb (swarm), the Apotheosis's, and a card that took it would leave the final form
// nothing of its own - which is why the proposal's Runners end in thorns, not a second patch, and why
// its Seedstorm became Thornstorm.
//   - Burrs (level 4): what walks out of the thicket carries burrs - drawn on it - that slow it and
//     prick it for BurrSeconds.
//   - Runners (level 4): once half grown, the thicket sends thorned runners creeping over the ground
//     to the nearest enemies past its rim, and whatever stands on one is cut and slowed as if inside.
//   - Bramble Keep (level 8): one great thicket three times as wide, ringed by a hedge that cuts
//     harder and slows to a crawl. It stands while the wizard stays inside it (each cast tends it,
//     Player.FireBrambleSeed); walking out of it sows the next.
//   - Thornstorm (level 8): every grown thicket throws a ring of thorns out past its rim each
//     ThornVolleySeconds, to ThornReach times its width; what a thorn passes through is hit.
public partial class BrambleSeed : Node2D, IPaintsElementPath
{
	public const string BurrsId = "bramble_seed_amplified_impact";
	public const string RunnersId = "bramble_seed_hyper_acceleration";
	public const string KeepId = "bramble_seed_titan_cataclysm";
	public const string ThornstormId = "bramble_seed_infinite_horizon";

	// Burrs: how long a burr clings once its carrier is out of the thicket, and what it pricks for each
	// tick, as a share of a tick of the thicket.
	public const float BurrSeconds = 2.5f;
	public const float BurrPrick = 0.5f;
	private const int MaxBurrs = 32;

	// Runners: how many a thicket sends, how far past its full-grown rim one may creep, how fast it
	// creeps, and how wide a strip of ground it cuts. Two runners leave at least RunnerSpreadDegrees
	// apart, so they go to different parts of the crowd.
	public const int RunnerCount = 3;
	public const float RunnerReach = 220f;
	public const float RunnerCreepSpeed = 90f;
	private const float RunnerHalfWidth = 12f;
	private const float RunnerSpreadDegrees = 50f;
	private const int RunnerSegments = 12;

	// Bramble Keep: the hedge is the outer HedgeDepth of the thicket; what is in it takes HedgeDamage
	// more and is slowed to HedgeSlow.
	public const float HedgeDepth = 26f;
	public const float HedgeDamage = 1.5f;
	public const float HedgeSlow = 0.3f;
	private const int MaxHedgeBushes = 128;

	// Thornstorm: a volley every ThornVolleySeconds while grown, ThornsPerVolley thorns, out to
	// ThornReach radii, each dealing ThornDamage of a tick to what it passes through.
	public const float ThornVolleySeconds = 0.8f;
	public const int ThornsPerVolley = 24;
	public const float ThornReach = 2f;
	public const float ThornDamage = 1f;
	private const float ThornFlightSeconds = 0.3f;
	// Half a body's width and half a thorn's: a thorn drawn passing through a body hits it.
	private const float ThornHalfWidth = 14f;

	[Export] public float GrowSeconds { get; set; } = 6f;
	[Export] public float HoldSeconds { get; set; } = 3f;
	[Export] public float StartRadius { get; set; } = 16f;
	[Export] public float MaxRadius { get; set; } = 110f;
	[Export] public float TickSeconds { get; set; } = 0.5f;
	[Export] public float SlowMultiplier { get; set; } = 0.6f;
	// How long the slow outlasts the thicket once a body is out of it (the SlowDuration level line).
	public float SlowLingerSeconds { get; set; } = 0f;

	public const int MaxAlive = 3;

	/// <summary>
	/// Patches the player may have standing at once: MaxAlive, and one more for every count the spell
	/// has past its base (Thornstorm). Bounded by the count, which no level raises. Bramble Keep stands
	/// one at a time, and Player.FireBrambleSeed decides that itself.
	/// </summary>
	public static int CapFor(SpellData spell) =>
		MaxAlive + Math.Max(0, (spell?.GetProjectileCountAtLevel(spell.CurrentLevel) ?? 1) - (spell?.BaseProjectileCount ?? 1));

	// Wildwood, Vainamoinen's Apotheosis (verb: swarm). A patch sown by the player is generation 0;
	// once it finishes growing it throws WildwoodSeeds of its own at its edge, generation 1, which
	// do not seed again. Bounded by construction: at most MaxAlive x (1 + WildwoodSeeds) patches.
	public int Generation { get; set; } = 0;
	private const int WildwoodSeeds = 3;
	private bool seeded;

	public int Damage { get; set; } = 7;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private float age;
	private float tickTimer;

	// The cards, read once from the spell in _Ready.
	private bool burrs;
	private bool runners;
	private bool keep;

	/// <summary>Sown as a Bramble Keep. Only a keep is tended; a patch sown before the card is not one.</summary>
	public bool IsKeep => keep;
	private bool thornstorm;

	// Burrs: who carries one, and for how much longer once out of the thicket. Swap-removed, never resized.
	private readonly Enemy[] burrEnemy = new Enemy[MaxBurrs];
	private readonly float[] burrLeft = new float[MaxBurrs];
	private int burrCount;

	// Runners: each one's bearing, where its tip stops (from the seed), and the age they set out at.
	private readonly Vector2[] runnerDir = new Vector2[RunnerCount];
	private readonly float[] runnerLength = new float[RunnerCount];
	private int runnerCount;
	private float runnersAt = -1f;

	// Thornstorm: the volley in flight - its age (negative: none), its turn, and the rim it left from.
	private float volleyTimer;
	private float volleyAge = -1f;
	private float volleyTurn;
	private float volleyFrom;

	// What flies or clings over the bodies - thorns in the air, burrs on a carrier - on DrawLayers.Air.
	// The thicket, its runners and its hedge stay on the ground under them.
	private Node2D air;

	/// <summary>
	/// The one hit authority. The tick below measures enemies against it and _Draw is built out of
	/// it, so the roots can never be drawn further than they hurt: this spell was once painted twice
	/// as wide as its damage (.ai/gameplay-design.md), and the fix only holds while both read here.
	/// </summary>
	public float Radius => Mathf.Lerp(StartRadius, MaxRadius, Mathf.Clamp(age / GrowSeconds, 0f, 1f));

	/// <summary>True once a newer seed has pushed this one out; it is pulling its roots back in.</summary>
	public bool IsWithering => witherAt >= 0f;

	/// <summary>True once the patch has stopped hurting - pushed out, or its hold over - and is only
	/// drawing its roots back in. Not counted against MaxAlive.</summary>
	public bool IsSpent => IsWithering || age >= GrowSeconds + HoldSeconds;

	// When the patch is gone: its hold, then RetractSeconds of the roots going back into the ground
	// AFTER it has stopped hurting - or WitherSeconds after it was pushed out. The roots used to start
	// retracting while the patch still hurt, and a disc at the full radius covered for them; with the
	// disc gone (no area markers), the roots have to be the honest extent to the last hurting frame.
	private float Lifetime => IsWithering
		? Mathf.Min(GrowSeconds + HoldSeconds + RetractSeconds, witherAt + WitherSeconds)
		: GrowSeconds + HoldSeconds + RetractSeconds;
	private float witherAt = -1f;

	public override void _Ready()
	{
		// Under every actor, over the ground decor (-20): roots come out of the floor, so the player and
		// the swarm stand on them. At 1, as the old disc was, the thicket was drawn over the wizard.
		ZAsRelative = false;
		ZIndex = DrawLayers.GroundTell;
		if (SpellData?.GetBranchElement() is Element branch)
			PaintElementPath(branch);
		if (SpellData != null)
		{
			burrs = SpellData.HasEvolution(BurrsId);
			runners = SpellData.HasEvolution(RunnersId);
			keep = SpellData.HasEvolution(KeepId);
			thornstorm = SpellData.HasEvolution(ThornstormId);
		}
		if (burrs || thornstorm)
		{
			air = new Node2D { Name = "Thorns", ZAsRelative = false, ZIndex = DrawLayers.Air };
			AddChild(air);
			air.Draw += DrawAir;
		}
		BuildRoots();
	}

	/// <summary>
	/// Pushed out by a newer seed. It used to vanish on the frame the fourth seed landed; now the
	/// roots pull back into the ground over WitherSeconds, and it stops hurting at once so the
	/// three-patch cap holds exactly.
	/// </summary>
	public void Wither()
	{
		if (!IsWithering)
			witherAt = age;
	}

	/// <summary>
	/// Bramble Keep: the wizard is still inside, so the keep stands at least <paramref name="seconds"/>
	/// more. A keep pushed out (withering) is not brought back.
	/// </summary>
	public void Tend(float seconds)
	{
		if (!IsWithering)
			HoldSeconds = MathF.Max(HoldSeconds, age + seconds - GrowSeconds);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		air?.QueueRedraw();
		// Gone from the ground. Burrs already on a carrier cling on until they drop, so the node waits for them.
		if (age >= Lifetime && burrCount == 0)
		{
			QueueFree();
			return;
		}

		if (!seeded && Generation == 0 && age >= GrowSeconds && SpellData?.IsApotheosis == true)
		{
			seeded = true;
			Scatter();
		}

		if (volleyAge >= 0f)
			volleyAge = volleyAge + dt > ThornFlightSeconds ? -1f : volleyAge + dt;
		if (thornstorm && age >= GrowSeconds && !IsSpent && PlayerRef != null && IsInstanceValid(PlayerRef))
		{
			volleyTimer -= dt;
			if (volleyTimer <= 0f)
			{
				volleyTimer = ThornVolleySeconds;
				Volley();
			}
		}

		tickTimer -= dt;
		if (tickTimer > 0f)
			return;
		tickTimer = TickSeconds;

		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		float r = Radius;
		if (!IsSpent)
		{
			if (runners && runnersAt < 0f && age >= GrowSeconds * 0.5f)
				SendRunners();
			Hurt(r);
		}
		if (burrs)
			TickBurrs(IsSpent ? 0f : r);
	}

	// One tick of the thicket: what is inside is cut and slowed (and, in a keep's hedge, cut harder and
	// slowed to a crawl, and under Burrs picks up a burr); what stands on a runner outside is cut as if inside.
	private void Hurt(float r)
	{
		float radiusSquared = r * r;
		float hedgeSquared = keep ? MathF.Max(0f, r - HedgeDepth) * MathF.Max(0f, r - HedgeDepth) : float.MaxValue;
		float runnerTip = runnerCount > 0 ? RunnerTip(r) : 0f;
		float runnerReachSquared = runnerTip * runnerTip;
		int hedgeDamage = Mathf.RoundToInt(Damage * (1f + HedgeDamage));
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = enemy.GlobalPosition - GlobalPosition;
			float distanceSquared = offset.LengthSquared();
			if (distanceSquared <= radiusSquared)
			{
				bool inHedge = distanceSquared >= hedgeSquared;
				PlayerRef.DealDamageToEnemy(enemy, inHedge ? hedgeDamage : Damage, source: SpellData);
				enemy.ApplySlow(inHedge ? HedgeSlow : SlowMultiplier, TickSeconds + 0.2f + SlowLingerSeconds);
				if (burrs)
					Burr(enemy);
				continue;
			}
			if (runnerCount == 0 || distanceSquared > runnerReachSquared)
				continue;
			for (int i = 0; i < runnerCount; i++)
			{
				float along = offset.Dot(runnerDir[i]);
				if (along < r || along > MathF.Min(runnerTip, runnerLength[i]) || MathF.Abs(offset.Cross(runnerDir[i])) > RunnerHalfWidth)
					continue;
				PlayerRef.DealDamageToEnemy(enemy, Damage, source: SpellData);
				enemy.ApplySlow(SlowMultiplier, TickSeconds + 0.2f + SlowLingerSeconds);
				break;
			}
		}
	}

	// --- Burrs -------------------------------------------------------------------------------------

	private void Burr(Enemy enemy)
	{
		for (int i = 0; i < burrCount; i++)
		{
			if (burrEnemy[i] == enemy)
			{
				burrLeft[i] = BurrSeconds;
				return;
			}
		}
		if (burrCount >= MaxBurrs)
			return;
		burrEnemy[burrCount] = enemy;
		burrLeft[burrCount] = BurrSeconds;
		burrCount++;
	}

	// The burrs on carriers out of the thicket prick and hold them back; a burr drops BurrSeconds after
	// its carrier left, or when it dies. `r` 0: the thicket has stopped hurting, so every carrier is out.
	private void TickBurrs(float r)
	{
		int prick = Math.Max(1, Mathf.RoundToInt(Damage * BurrPrick));
		float radiusSquared = r * r;
		for (int i = burrCount - 1; i >= 0; i--)
		{
			Enemy enemy = burrEnemy[i];
			bool gone = enemy == null || !IsInstanceValid(enemy) || enemy.IsDying;
			if (!gone && GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) > radiusSquared)
			{
				PlayerRef.DealDamageToEnemy(enemy, prick, source: SpellData);
				if (IsInstanceValid(enemy) && !enemy.IsDying)
					enemy.ApplySlow(SlowMultiplier, TickSeconds + 0.2f);
				burrLeft[i] -= TickSeconds;
			}
			if (gone || burrLeft[i] <= 0f || !IsInstanceValid(enemy) || enemy.IsDying)
			{
				burrCount--;
				burrEnemy[i] = burrEnemy[burrCount];
				burrLeft[i] = burrLeft[burrCount];
				burrEnemy[burrCount] = null;
			}
		}
	}

	// --- Runners -----------------------------------------------------------------------------------

	// Up to RunnerCount runners toward the nearest enemies past the full-grown rim and within RunnerReach
	// of it, each on its own bearing. A runner stops short of water and walls (MazeNavigation), and no
	// runner sets out if nothing is out there yet - the next tick looks again.
	private void SendRunners()
	{
		float outerSquared = (MaxRadius + RunnerReach) * (MaxRadius + RunnerReach);
		float innerSquared = MaxRadius * MaxRadius;
		float spread = MathF.Cos(Mathf.DegToRad(RunnerSpreadDegrees));
		MazeNavigation nav = MazeNavigation.Active;
		runnerCount = 0;
		while (runnerCount < RunnerCount)
		{
			Enemy best = null;
			float bestSquared = float.MaxValue;
			foreach (Node node in EnemyRegistry.Snapshot)
			{
				if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
					continue;
				float d2 = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
				if (d2 <= innerSquared || d2 > outerSquared || d2 >= bestSquared)
					continue;
				Vector2 bearing = (enemy.GlobalPosition - GlobalPosition) / MathF.Sqrt(d2);
				bool crowded = false;
				for (int i = 0; i < runnerCount && !crowded; i++)
					crowded = bearing.Dot(runnerDir[i]) > spread;
				if (crowded)
					continue;
				best = enemy;
				bestSquared = d2;
			}
			if (best == null)
				break;
			float distance = MathF.Sqrt(bestSquared);
			Vector2 dir = (best.GlobalPosition - GlobalPosition) / distance;
			float length = MathF.Min(distance + 16f, MaxRadius + RunnerReach);
			if (nav != null)
			{
				for (float s = MaxRadius * 0.8f; s <= length; s += 10f)
				{
					if (nav.IsBlockedAt(GlobalPosition + dir * s))
					{
						length = s - 10f;
						break;
					}
				}
			}
			runnerDir[runnerCount] = dir;
			runnerLength[runnerCount] = length;
			runnerCount++;
		}
		if (runnerCount > 0)
			runnersAt = age;
	}

	// How far from the seed the runners' tips have crept, before each stops at its own length.
	private float RunnerTip(float r) => runnersAt < 0f ? 0f : r * 0.7f + RunnerCreepSpeed * (age - runnersAt);

	// --- Thornstorm ---------------------------------------------------------------------------------

	// A ring of thorns thrown out from the rim, turned a little differently each time. Resolved as it is
	// thrown, against each thorn's own line: a body in the ring between the rim and ThornReach radii is
	// hit if the nearest thorn's line passes within ThornHalfWidth of it, so what is hit is what a thorn
	// is drawn flying through - never the whole ring.
	private void Volley()
	{
		float r = Radius;
		volleyAge = 0f;
		volleyFrom = r;
		volleyTurn = (float)GD.RandRange(0.0, Mathf.Tau);
		float step = Mathf.Tau / ThornsPerVolley;
		float innerSquared = r * r * 0.72f;
		float outer = r * ThornReach;
		int damage = Math.Max(1, Mathf.RoundToInt(Damage * ThornDamage));
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = enemy.GlobalPosition - GlobalPosition;
			float d2 = offset.LengthSquared();
			if (d2 < innerSquared || d2 > outer * outer)
				continue;
			float angle = MathF.Atan2(offset.Y, offset.X) - volleyTurn;
			float nearest = MathF.Round(angle / step) * step;
			float lateral = MathF.Sqrt(d2) * MathF.Abs(MathF.Sin(angle - nearest));
			if (lateral <= ThornHalfWidth)
				PlayerRef.DealDamageToEnemy(enemy, damage, source: SpellData);
		}
	}

	private void Scatter()
	{
		var scene = GD.Load<PackedScene>(SceneFilePath);
		Node parent = GetParent();
		if (scene == null || parent == null)
			return;
		float turn = (float)GD.RandRange(0.0, Mathf.Tau);
		for (int i = 0; i < WildwoodSeeds; i++)
		{
			if (scene.Instantiate() is not BrambleSeed child)
				continue;
			child.Generation = Generation + 1;
			child.SpellData = SpellData;
			child.PlayerRef = PlayerRef;
			child.Damage = Damage;
			child.MaxRadius = MaxRadius * 0.8f;
			child.HoldSeconds = HoldSeconds;
			child.GrowSeconds = GrowSeconds;
			child.SlowMultiplier = SlowMultiplier;
			child.SlowLingerSeconds = SlowLingerSeconds;
			parent.AddChild(child);
			float a = turn + i * Mathf.Tau / WildwoodSeeds;
			child.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * MaxRadius * 1.6f;
		}
	}

	// --- the roots ----------------------------------------------------------------------------
	//
	// "It should look more like roots writhing out." The patch used to be a green disc with fourteen
	// static spokes at its edge, which read as a decal. Now it is thorned roots: they burst out of
	// the seed in a staggered half-second, writhe the whole time (a wave runs down each one from the
	// seed to the tip), whip now and then, and at the end pull back into the ground and dry out.
	//
	// Drawn, not a sprite sheet, for the reason Radius gives: a root is laid out in polar terms, its
	// distance from the seed running 0..length and length never more than Radius, so the writhing
	// bends a root sideways and can never carry it past the ground it hurts. The long roots end
	// exactly on Radius, which is what makes the edge of the thicket the edge of the damage - and the
	// only thing that does: the faint disc and rim that used to sit under them at the full radius
	// were an area marker, and went (2026-10-23). The roots now hold their full length until the
	// patch has stopped hurting, and only then pull back in (Lifetime).
	//
	// Everything per frame is written into arrays allocated once in BuildRoots. Up to twelve of
	// these stand at once under Wildwood, so _Draw must not allocate.

	private const int LongRoots = 6;
	private const int ShortRoots = 5;
	private const int RootCount = LongRoots + ShortRoots;
	private const int Segments = 10;
	// A root is three polylines, thick to thin: the base, the middle and the tip.
	private const int BaseEnd = 4;
	private const int MidEnd = 7;
	private const int ThornsPerRoot = 3;
	private const float WitherSeconds = 0.55f;
	// The last stretch of every life: roots draw back and dry out.
	private const float RetractSeconds = 0.6f;
	private const float DrySeconds = 1.1f;

	private readonly float[] rootAngle = new float[RootCount];
	private readonly float[] rootLength = new float[RootCount];
	private readonly float[] rootDelay = new float[RootCount];
	private readonly float[] rootPhase = new float[RootCount];
	private readonly float[] rootCurl = new float[RootCount];
	private readonly float[] rootWidth = new float[RootCount];
	private readonly float[] rootWhipEvery = new float[RootCount];
	private Vector2[][] baseRun;
	private Vector2[][] midRun;
	private Vector2[][] tipRun;
	private Vector2[] thornLines;
	private Vector2[] path;

	// Grass and Earth from the emissive table (.ai/art-direction.md section 3), plus the shared
	// contact shadow for the outline so a root reads on a mossy forest floor as well as on stone.
	private static readonly Color Occ = new Color(0.02f, 0.03f, 0.05f, 0.85f);
	private static readonly Color Soil = new Color(0.16f, 0.11f, 0.06f, 0.55f);
	private static readonly Color Bark = new Color(0.48f, 0.31f, 0.14f, 1f);        // Earth edge #7A5024
	private static readonly Color Dry = new Color(0.36f, 0.27f, 0.19f, 1f);
	private static readonly Color Clod = new Color(0.79f, 0.60f, 0.37f, 0.8f);      // Earth mid #C99A5E
	// The Grass half of the palette, repainted by an element path (PaintElementPath).
	private Color living = new Color(0.29f, 0.55f, 0.23f, 1f);                     // Grass edge #4A8C3A
	private Color thornTip = new Color(0.81f, 0.93f, 0.58f, 1f);                   // Grass hot #CFEE94
	private Color tipGlow = new Color(0.61f, 0.83f, 0.33f, 0.9f);                  // Grass mid #9BD455

	// Once, from _Ready: the same four slots in the element's ramp, so a branched patch reads as
	// that element at a glance and the bark beneath still says it is a root.
	private void PaintElementPath(Element element)
	{
		ElementColors.Ramp ramp = ElementColors.GetRamp(element);
		living = ramp.Edge;
		thornTip = ramp.Core;
		tipGlow = new Color(ramp.Hot, 0.9f);
	}

	private void BuildRoots()
	{
		// Seeded per patch so three patches side by side do not writhe in step.
		var rng = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
		float turn = rng.Randf() * Mathf.Tau;
		for (int i = 0; i < RootCount; i++)
		{
			bool isLong = i < LongRoots;
			int slot = isLong ? i : i - LongRoots;
			int slots = isLong ? LongRoots : ShortRoots;
			float spread = Mathf.Tau / slots;
			rootAngle[i] = turn + slot * spread + (isLong ? 0f : spread * 0.5f) + rng.RandfRange(-0.18f, 0.18f);
			rootLength[i] = isLong ? 1f : rng.RandfRange(0.48f, 0.74f);
			rootDelay[i] = rng.RandfRange(0f, 0.32f) + (isLong ? 0f : 0.12f);
			rootPhase[i] = rng.Randf() * Mathf.Tau;
			rootCurl[i] = (rng.Randf() < 0.5f ? -1f : 1f) * rng.RandfRange(0.35f, 0.75f);
			rootWidth[i] = isLong ? rng.RandfRange(7.0f, 8.5f) : rng.RandfRange(4.8f, 5.8f);
			rootWhipEvery[i] = rng.RandfRange(1.5f, 2.6f);
		}

		baseRun = new Vector2[RootCount][];
		midRun = new Vector2[RootCount][];
		tipRun = new Vector2[RootCount][];
		for (int i = 0; i < RootCount; i++)
		{
			baseRun[i] = new Vector2[BaseEnd + 1];
			midRun[i] = new Vector2[MidEnd - BaseEnd + 1];
			tipRun[i] = new Vector2[Segments - MidEnd + 1];
		}
		path = new Vector2[Segments + 1];
		thornLines = new Vector2[ThornsPerRoot * 4];
		if (runners)
		{
			runnerPath = new Vector2[RunnerSegments + 1];
			runnerThorns = new Vector2[RunnerSegments * 2];
		}
		if (keep)
		{
			hedgeCanes = new Vector2[MaxHedgeBushes * 6];
			hedgeBarbs = new Vector2[MaxHedgeBushes * 4];
		}
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	public override void _Draw()
	{
		if (baseRun == null)
			return;

		float r = Radius;
		float left = Lifetime - age;
		// Burrs outliving the thicket: the roots are gone, only the air layer still draws.
		if (left <= 0f)
			return;
		float fade = Mathf.Clamp(left / 0.4f, 0f, 1f);
		float retract = Smooth(left / RetractSeconds);
		float dry = 1f - Mathf.Clamp(left / DrySeconds, 0f, 1f);

		// Torn soil where the seed went in, wider as the roots thicken.
		float mound = 5f + 4f * Mathf.Clamp(age / GrowSeconds, 0f, 1f);
		DrawCircle(Vector2.Zero, mound + 3f, new Color(Soil, Soil.A * fade));

		for (int i = 0; i < RootCount; i++)
			DrawRoot(i, r, retract, dry, fade);
		for (int i = 0; i < runnerCount; i++)
			DrawRunner(i, r, retract, dry, fade);
		if (keep)
			DrawHedge(r, retract, dry, fade);

		DrawCircle(Vector2.Zero, mound * 0.55f, new Color(Occ, Occ.A * fade));
	}

	// Runners: a thorned root creeping along the ground from inside the thicket out to its tip, with a
	// slow sideways crawl and barbs raked back - the shape the roots have, laid flat and long. Never
	// further than RunnerTip and its own length, which is where it cuts.
	private Vector2[] runnerPath;
	private Vector2[] runnerThorns;

	private void DrawRunner(int i, float r, float retract, float dry, float fade)
	{
		float from = r * 0.7f;
		float to = MathF.Min(RunnerTip(r), runnerLength[i]);
		to = from + (to - from) * retract;
		if (to - from < 4f)
			return;
		Vector2 dir = runnerDir[i];
		var side = new Vector2(-dir.Y, dir.X);
		float phase = rootPhase[i % RootCount];
		for (int j = 0; j <= RunnerSegments; j++)
		{
			float s = j / (float)RunnerSegments;
			float d = Mathf.Lerp(from, to, s);
			float sway = MathF.Sin(age * 2.2f + phase - s * 5f) * 4f * MathF.Min(1f, s * 3f);
			runnerPath[j] = dir * d + side * sway;
		}
		int t = 0;
		for (int j = 1; j < RunnerSegments; j++)
		{
			Vector2 along = (runnerPath[j + 1] - runnerPath[j - 1]).Normalized();
			Vector2 barbSide = new Vector2(-along.Y, along.X) * (j % 2 == 0 ? 1f : -1f);
			runnerThorns[t++] = runnerPath[j];
			runnerThorns[t++] = runnerPath[j] + barbSide * 5f - along * 3f;
		}
		Color body = Bark.Lerp(living, 0.45f).Lerp(Dry, dry);
		body.A = fade;
		DrawPolyline(runnerPath, new Color(Occ, Occ.A * fade), 6f);
		DrawPolyline(runnerPath, body, 4f);
		DrawMultiline(runnerThorns, new Color(thornTip.Lerp(Dry, dry), fade), 1.5f);
		if (dry < 0.6f)
			DrawCircle(runnerPath[RunnerSegments], 2.2f, new Color(tipGlow, tipGlow.A * fade * (1f - dry)));
	}

	// Bramble Keep's hedge: thorn bushes standing shoulder to shoulder round the rim, three canes each
	// fanned up from a foot on the circle and barbed, rising as the keep grows. Two batched multilines
	// (an outline and the canes) and one of barbs, out of arrays made once in BuildRoots.
	private Vector2[] hedgeCanes;
	private Vector2[] hedgeBarbs;

	private void DrawHedge(float r, float retract, float dry, float fade)
	{
		if (hedgeCanes == null || r < 24f)
			return;
		float grown = Mathf.Clamp(age / GrowSeconds, 0f, 1f);
		// Chest-high on a skeleton when grown: at the scale of a keep three times as wide, anything
		// smaller read as stubble round the rim, not a hedge.
		float height = (9f + 15f * grown) * retract;
		if (height < 2f)
			return;
		int bushes = Math.Clamp(Mathf.RoundToInt(Mathf.Tau * r / 22f), 8, MaxHedgeBushes);
		float turn = rootAngle[0];
		var up = new Vector2(0f, -1f);
		// No bush stands in water or a wall: a keep three times as wide often reaches a shore.
		MazeNavigation nav = MazeNavigation.Active;
		int c = 0, b = 0;
		for (int i = 0; i < bushes; i++)
		{
			float a = turn + i * Mathf.Tau / bushes;
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			// Inside the rim by half a bush, so no cane stands outside the ground the hedge cuts.
			Vector2 foot = dir * (r - 5f);
			if (nav != null && nav.IsBlockedAt(GlobalPosition + foot))
				continue;
			var side = new Vector2(-dir.Y, dir.X);
			float sway = MathF.Sin(age * 1.6f + i * 1.7f) * 1.5f;
			float h = height * (i % 3 == 0 ? 1f : 0.8f);
			Vector2 mid = foot + up * h + side * sway;
			Vector2 left = foot + (up * 0.75f - side * 0.55f) * h;
			Vector2 right = foot + (up * 0.75f + side * 0.55f) * h;
			hedgeCanes[c++] = foot; hedgeCanes[c++] = mid;
			hedgeCanes[c++] = foot; hedgeCanes[c++] = left;
			hedgeCanes[c++] = foot; hedgeCanes[c++] = right;
			hedgeBarbs[b++] = mid; hedgeBarbs[b++] = mid + side * 4f + up;
			hedgeBarbs[b++] = (foot + left) * 0.5f; hedgeBarbs[b++] = (foot + left) * 0.5f - side * 4f;
		}
		Color cane = Bark.Lerp(living, 0.35f).Lerp(Dry, dry);
		cane.A = fade;
		DrawMultiline(hedgeCanes.AsSpan(0, c), new Color(Occ, Occ.A * fade), 6f);
		DrawMultiline(hedgeCanes.AsSpan(0, c), cane, 3.5f);
		DrawMultiline(hedgeBarbs.AsSpan(0, b), new Color(thornTip.Lerp(Dry, dry), fade), 2f);
	}

	// What is over the bodies: Thornstorm's volley in flight, and the burrs on their carriers.
	private void DrawAir()
	{
		if (air == null)
			return;
		if (volleyAge >= 0f)
		{
			float u = Mathf.Clamp(volleyAge / ThornFlightSeconds, 0f, 1f);
			float eased = 1f - (1f - u) * (1f - u);
			float at = Mathf.Lerp(volleyFrom, volleyFrom * ThornReach, eased);
			float alpha = u < 0.75f ? 1f : (1f - u) / 0.25f;
			Color outline = new Color(Occ, Occ.A * alpha);
			Color thorn = new Color(thornTip, alpha);
			for (int i = 0; i < ThornsPerVolley; i++)
			{
				float a = volleyTurn + i * Mathf.Tau / ThornsPerVolley;
				var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
				Vector2 tip = dir * at;
				Vector2 tail = dir * MathF.Max(volleyFrom, at - 10f);
				air.DrawLine(tail, tip, outline, 4f);
				air.DrawLine(tail, tip, thorn, 2f);
			}
		}
		for (int i = 0; i < burrCount; i++)
		{
			Enemy enemy = burrEnemy[i];
			if (enemy == null || !IsInstanceValid(enemy))
				continue;
			// On the body, a little above its feet, at a spot of its own so a crowd's burrs do not stack.
			float h = ((i * 7919) % 13) / 13f;
			Vector2 at = ToLocal(enemy.GlobalPosition) + new Vector2((h - 0.5f) * 12f, -8f - h * 8f);
			float alpha = MathF.Min(1f, burrLeft[i] / 0.4f);
			Color spike = new Color(thornTip, alpha);
			air.DrawLine(at + new Vector2(-4f, 0f), at + new Vector2(4f, 0f), spike, 1.5f);
			air.DrawLine(at + new Vector2(0f, -4f), at + new Vector2(0f, 4f), spike, 1.5f);
			air.DrawLine(at + new Vector2(-3f, -3f), at + new Vector2(3f, 3f), spike, 1f);
			air.DrawLine(at + new Vector2(-3f, 3f), at + new Vector2(3f, -3f), spike, 1f);
			air.DrawRect(WhipLimb.Cell(at, WhipLimb.Grid * 2f), new Color(Bark, alpha));
		}
	}

	private void DrawRoot(int i, float radius, float retract, float dry, float fade)
	{
		// Erupt: each root shoots out to the current radius over a quarter second after its delay,
		// overshooting its writhe while it does - that is the whip of something breaking ground.
		float since = age - rootDelay[i];
		if (since <= 0f)
			return;
		float erupt = Smooth(since / 0.26f);
		float length = radius * rootLength[i] * erupt * retract;
		if (length < 2f)
			return;

		// The writhe is a wave travelling down the root from seed to tip; the whip is a short
		// periodic surge of that wave, at a different interval on every root.
		float whipClock = (age + rootPhase[i]) % rootWhipEvery[i];
		float whip = whipClock < 0.3f ? Mathf.Sin(whipClock / 0.3f * Mathf.Pi) : 0f;
		float burst = 1f - erupt;
		// Radians, so a long root sways further sideways than a short one, as a real one would.
		float amplitude = (0.16f + 0.22f * whip + 0.35f * burst) * (1f - 0.6f * dry);
		float speed = 3.1f + 1.7f * whip;

		for (int j = 0; j <= Segments; j++)
		{
			float s = (float)j / Segments;
			float wave = Mathf.Sin(age * speed + rootPhase[i] - s * 4.2f);
			// The tip hooks over, and the hook breathes.
			float curl = rootCurl[i] * Smooth((s - 0.62f) / 0.38f) * (0.8f + 0.35f * Mathf.Sin(age * 2.3f + rootPhase[i]));
			float theta = rootAngle[i] + amplitude * s * wave + curl;
			float d = s * length;
			path[j] = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * d;
		}

		for (int j = 0; j <= BaseEnd; j++)
			baseRun[i][j] = path[j];
		for (int j = BaseEnd; j <= MidEnd; j++)
			midRun[i][j - BaseEnd] = path[j];
		for (int j = MidEnd; j <= Segments; j++)
			tipRun[i][j - MidEnd] = path[j];

		// Bark at the base, greening toward the living tip: a root, not a vine.
		Color body = Bark.Lerp(living, 0.2f).Lerp(Dry, dry);
		Color midColour = Bark.Lerp(living, 0.5f).Lerp(Dry, dry);
		Color tipColour = living.Lerp(Dry, dry);
		midColour.A = fade;
		body.A = fade;
		tipColour.A = fade;
		Color outline = new Color(Occ, Occ.A * fade);
		float w = rootWidth[i] * (0.6f + 0.4f * erupt);

		float wMid = w * 0.62f;
		float wTip = w * 0.32f;
		DrawPolyline(baseRun[i], outline, w + 2f);
		DrawPolyline(midRun[i], outline, wMid + 2f);
		DrawPolyline(tipRun[i], outline, wTip + 2f);
		// Polyline segments are separate quads, so a thick one shows a notch at every bend. A disc on
		// each of the base's joints fills it; the thinner runs do not need it.
		for (int j = 1; j <= BaseEnd; j++)
			DrawCircle(path[j], w * 0.5f + 1f, outline);
		for (int j = 1; j <= BaseEnd; j++)
			DrawCircle(path[j], w * 0.5f, body);
		DrawPolyline(baseRun[i], body, w);
		DrawPolyline(midRun[i], midColour, wMid);
		DrawPolyline(tipRun[i], tipColour, wTip);

		// Thorns: hooked barbs along the middle of the root, alternating sides and raked back toward
		// the seed. Held inside s = 0.85 and pointed sideways, so a barb never reaches past Radius.
		int t = 0;
		for (int k = 0; k < ThornsPerRoot; k++)
		{
			int j = 3 + k * 2;
			Vector2 at = path[j];
			Vector2 along = (path[j + 1] - path[j - 1]).Normalized();
			Vector2 side = new Vector2(-along.Y, along.X) * ((k % 2 == 0) ? 1f : -1f);
			float barb = 3.5f + w * 0.5f;
			thornLines[t++] = at;
			thornLines[t++] = at + side * barb - along * (barb * 0.6f);
			thornLines[t++] = at + along * 2f;
			thornLines[t++] = at + side * barb - along * (barb * 0.6f);
		}
		DrawMultiline(thornLines, new Color(thornTip.Lerp(Dry, dry), fade), 1.5f);

		// A living tip, and a clod of earth thrown up where the root first broke ground.
		if (dry < 0.6f)
			DrawCircle(path[Segments], 1.8f + 1.2f * whip, new Color(tipGlow, tipGlow.A * fade * (1f - dry)));
		if (burst > 0.05f)
		{
			// Capped at the root's own length, so even the debris stays on the ground that hurts.
			float flung = Mathf.Min(length, length * 0.5f + 14f * (1f - burst));
			Vector2 thrown = path[Segments].Normalized() * flung;
			DrawCircle(thrown, 2.2f * burst + 0.6f, new Color(Clod, Clod.A * burst * fade));
		}
	}
}
