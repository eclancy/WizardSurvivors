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
		// The thicket is one quad through bramble_patch.gdshader (see "the roots" below).
		patchShader ??= GD.Load<Shader>("res://scenes/shaders/bramble_patch.gdshader");
		patchMaterial = new ShaderMaterial { Shader = patchShader };
		Material = patchMaterial;
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
			// Its own plain material, so the thicket's shader never reaches the thorns in the air.
			air = new Node2D { Name = "Thorns", ZAsRelative = false, ZIndex = DrawLayers.Air, Material = new CanvasItemMaterial() };
			AddChild(air);
			air.Draw += DrawAir;
		}
		BuildRoots();
	}

	public override void _ExitTree()
	{
		if (air != null && IsInstanceValid(air))
			air.Draw -= DrawAir;
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
		PaintPatch();
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
	// PIXEL ART (2026-10-29, "Update all spells to the same quality we expect now"). The roots, the soil,
	// the runners and the hedge were antialiased polylines, circles and lines, their colours lerped and
	// their fading done in alpha. They are now one quad a patch through bramble_patch.gdshader, which
	// snaps every fragment to the world's 2 px cell and lays each root out exactly as DrawRoot used to -
	// so the drawn reach is still Radius - in Bonelight ramp stops with Bayer the only gradient. Every
	// root's shape is fixed when the patch is sown and uploaded once (BuildRoots); per frame only scalars
	// cross (PaintPatch). What is in the air - Thornstorm's volley and the burrs - is stamped on the same
	// grid with DrawRect, in ramp stops, and shortens or sheds its spikes rather than fading.
	//
	// Up to twelve of these stand at once under Wildwood, so nothing here allocates per frame.

	private const int LongRoots = 6;
	private const int ShortRoots = 5;
	private const int RootCount = LongRoots + ShortRoots;
	private const float WitherSeconds = 0.55f;
	// The last stretch of every life: roots draw back and dry out.
	private const float RetractSeconds = 0.6f;
	private const float DrySeconds = 1.1f;
	// Bramble Keep's bushes: one every this many px round the rim, never more than the shader's mask holds.
	private const float HedgeSpacing = 22f;

	private readonly float[] rootAngle = new float[RootCount];
	private readonly float[] rootLength = new float[RootCount];
	private readonly float[] rootDelay = new float[RootCount];
	private readonly float[] rootPhase = new float[RootCount];
	private readonly float[] rootCurl = new float[RootCount];
	private readonly float[] rootWidth = new float[RootCount];
	private readonly float[] rootWhipEvery = new float[RootCount];

	private static Shader patchShader;
	private ShaderMaterial patchMaterial;
	private static readonly StringName ParamRootA = "root_a";
	private static readonly StringName ParamRootB = "root_b";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamAge = "age";
	private static readonly StringName ParamRadius = "radius";
	private static readonly StringName ParamRetract = "retract";
	private static readonly StringName ParamDry = "dry";
	private static readonly StringName ParamFade = "fade";
	private static readonly StringName ParamGrown = "grown";
	private static readonly StringName ParamRunner0 = "runner0";
	private static readonly StringName ParamRunner1 = "runner1";
	private static readonly StringName ParamRunner2 = "runner2";
	private static readonly StringName ParamHedge = "hedge";
	private static readonly StringName ParamHedgeBlocked = "hedge_blocked";
	private static readonly StringName ParamLivingEdge = "living_edge";
	private static readonly StringName ParamLivingMid = "living_mid";
	private static readonly StringName ParamLivingHot = "living_hot";
	private static readonly StringName ParamThornTip = "thorn_tip";
	// Bramble Keep: which bushes stand in water or a wall, one bit each, worked out again only when the
	// rim moves; and the extent the quad must reach this frame.
	private int hedgeBushes = -1;
	private float hedgeRim = -1f;
	private Vector4I hedgeBlocked;
	private float drawReach;

	// Grass and Earth from the emissive table (.ai/art-direction.md section 3), plus the shared contact
	// shadow, in opaque stops for what is stamped in the air. The thicket's own are in its shader.
	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);        // #05070C
	private static readonly Color Bark = new Color(0.478f, 0.314f, 0.141f, 1f);       // earth edge #7A5024
	private static readonly Color BarkLit = new Color(0.788f, 0.604f, 0.369f, 1f);    // earth mid #C99A5E
	// The Grass half of the palette, repainted by an element path (PaintElementPath).
	private Color living = new Color(0.290f, 0.549f, 0.227f, 1f);                     // grass edge #4A8C3A
	private Color livingMid = new Color(0.608f, 0.831f, 0.333f, 1f);                  // grass mid #9BD455
	private Color tipGlow = new Color(0.812f, 0.933f, 0.580f, 1f);                    // grass hot #CFEE94
	private Color thornTip = new Color(0.812f, 0.933f, 0.580f, 1f);                   // grass hot #CFEE94

	// Once, from _Ready: the same four slots in the element's ramp, so a branched patch reads as
	// that element at a glance and the bark beneath still says it is a root.
	private void PaintElementPath(Element element)
	{
		ElementColors.Ramp ramp = ElementColors.GetRamp(element);
		living = ramp.Edge;
		livingMid = ramp.Mid;
		tipGlow = ramp.Hot;
		thornTip = ramp.Core;
	}

	private void BuildRoots()
	{
		// Seeded per patch so three patches side by side do not writhe in step.
		var rng = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
		float turn = rng.Randf() * Mathf.Tau;
		var rootA = new Vector4[RootCount];
		var rootB = new Vector4[RootCount];
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
			rootA[i] = new Vector4(rootAngle[i], rootLength[i], rootDelay[i], rootPhase[i]);
			rootB[i] = new Vector4(rootCurl[i], rootWidth[i], rootWhipEvery[i], 0f);
		}
		// The roots' shapes and the palette, once: per frame only scalars cross (PaintPatch).
		patchMaterial.SetShaderParameter(ParamRootA, rootA);
		patchMaterial.SetShaderParameter(ParamRootB, rootB);
		patchMaterial.SetShaderParameter(ParamSeed, turn);
		patchMaterial.SetShaderParameter(ParamLivingEdge, living);
		patchMaterial.SetShaderParameter(ParamLivingMid, livingMid);
		patchMaterial.SetShaderParameter(ParamLivingHot, tipGlow);
		patchMaterial.SetShaderParameter(ParamThornTip, thornTip);
		PaintPatch();
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	// This frame's thicket for bramble_patch.gdshader: how old and how wide, how far drawn back and dried,
	// where each runner has crept to, and the hedge. The same numbers the old DrawRoot, DrawRunner and
	// DrawHedge read, so the roots still end on Radius and the runners on the strip they cut.
	private void PaintPatch()
	{
		if (patchMaterial == null)
			return;
		float r = Radius;
		float left = Lifetime - age;
		float fade = Mathf.Clamp(left / 0.4f, 0f, 1f);
		float retract = Smooth(left / RetractSeconds);
		float dry = 1f - Mathf.Clamp(left / DrySeconds, 0f, 1f);
		float grown = Mathf.Clamp(age / GrowSeconds, 0f, 1f);
		patchMaterial.SetShaderParameter(ParamAge, age);
		patchMaterial.SetShaderParameter(ParamRadius, r);
		patchMaterial.SetShaderParameter(ParamRetract, retract);
		patchMaterial.SetShaderParameter(ParamDry, dry);
		patchMaterial.SetShaderParameter(ParamFade, fade);
		patchMaterial.SetShaderParameter(ParamGrown, grown);
		drawReach = r + 12f;

		// Runners: from inside the thicket out to where each has crept, and back in as the patch retracts.
		for (int i = 0; i < RunnerCount; i++)
		{
			Vector4 runner = Vector4.Zero;
			if (i < runnerCount)
			{
				float from = r * 0.7f;
				float to = MathF.Min(RunnerTip(r), runnerLength[i]);
				to = from + (to - from) * retract;
				runner = new Vector4(runnerDir[i].X, runnerDir[i].Y, from, to);
				drawReach = MathF.Max(drawReach, to + 12f);
			}
			patchMaterial.SetShaderParameter(i == 0 ? ParamRunner0 : i == 1 ? ParamRunner1 : ParamRunner2, runner);
		}

		// Bramble Keep's hedge: chest-high on a skeleton when grown - at the scale of a keep three times as
		// wide, anything smaller read as stubble round the rim, not a hedge.
		Vector4 hedge = Vector4.Zero;
		if (keep && r >= 24f)
		{
			float height = (9f + 15f * grown) * retract;
			int bushes = Math.Clamp(Mathf.RoundToInt(Mathf.Tau * r / HedgeSpacing), 8, MaxHedgeBushes);
			if (height >= 2f)
			{
				hedge = new Vector4(bushes, rootAngle[0], height, r);
				drawReach = MathF.Max(drawReach, r + height + 8f);
			}
			// No bush stands in water or a wall: a keep three times as wide often reaches a shore.
			if (bushes != hedgeBushes || MathF.Abs(r - hedgeRim) >= 1f)
			{
				hedgeBushes = bushes;
				hedgeRim = r;
				hedgeBlocked = BlockedBushes(bushes, rootAngle[0], r);
				patchMaterial.SetShaderParameter(ParamHedgeBlocked, hedgeBlocked);
			}
		}
		patchMaterial.SetShaderParameter(ParamHedge, hedge);
	}

	// One bit a bush (the shader's hedge_blocked): set where its foot would stand on blocked ground.
	private Vector4I BlockedBushes(int bushes, float turn, float r)
	{
		MazeNavigation nav = MazeNavigation.Active;
		var mask = new Vector4I();
		if (nav == null)
			return mask;
		for (int i = 0; i < bushes; i++)
		{
			float a = turn + i * Mathf.Tau / bushes;
			Vector2 foot = new Vector2(MathF.Cos(a), MathF.Sin(a)) * (r - 5f);
			if (!nav.IsBlockedAt(GlobalPosition + foot))
				continue;
			int bit = 1 << (i % 32);
			switch (i / 32)
			{
				case 0: mask.X |= bit; break;
				case 1: mask.Y |= bit; break;
				case 2: mask.Z |= bit; break;
				default: mask.W |= bit; break;
			}
		}
		return mask;
	}

	// One quad over everything the thicket reaches this frame; the shader draws inside it.
	public override void _Draw()
	{
		if (Lifetime - age <= 0f)
			return;
		DrawRect(new Rect2(-drawReach, -drawReach, drawReach * 2f, drawReach * 2f), Colors.White);
	}

	// A square of `size` round `local` (this node's frame) on the WORLD's 2 px grid: the patch is sown at
	// the wizard's feet, which are anywhere, and the thorns in the air must sit on the same grid as the
	// thicket under them.
	private Rect2 GridCell(Vector2 local, float size)
	{
		float g = WhipLimb.Grid;
		Vector2 o = GlobalPosition;
		float half = size * 0.5f;
		float x = MathF.Round((o.X + local.X - half) / g) * g - o.X;
		float y = MathF.Round((o.Y + local.Y - half) / g) * g - o.Y;
		return new Rect2(x, y, size, size);
	}

	// What is over the bodies: Thornstorm's volley in flight, and the burrs on their carriers.
	private void DrawAir()
	{
		if (air == null)
			return;
		float g = WhipLimb.Grid;
		if (volleyAge >= 0f)
		{
			float u = Mathf.Clamp(volleyAge / ThornFlightSeconds, 0f, 1f);
			float eased = 1f - (1f - u) * (1f - u);
			float at = Mathf.Lerp(volleyFrom, volleyFrom * ThornReach, eased);
			// Spent, a thorn falls away from its tail forward, never fades.
			float shed = u < 0.75f ? 0f : (u - 0.75f) / 0.25f;
			for (int pass = 0; pass < 2; pass++)
			{
				for (int i = 0; i < ThornsPerVolley; i++)
				{
					float a = volleyTurn + i * Mathf.Tau / ThornsPerVolley;
					var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
					Vector2 tail = dir * MathF.Max(volleyFrom, at - 10f);
					int cells = Math.Max(1, (int)((at - MathF.Max(volleyFrom, at - 10f)) / g));
					for (int k = (int)(cells * shed); k <= cells; k++)
					{
						Vector2 p = tail + dir * (k * g);
						if (pass == 0)
							air.DrawRect(GridCell(p, g * 2f), Occ);
						else
							air.DrawRect(GridCell(p, g), k == cells ? thornTip : k >= cells / 2 ? tipGlow : living);
					}
				}
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
			// A ball of bark bristling with its element's thorns; as it lets go it sheds them, the
			// diagonals first, and is gone.
			float left = burrLeft[i];
			bool diagonals = left > 0.4f;
			bool spikes = left > 0.2f;
			air.DrawRect(GridCell(at, g * 4f), Occ);
			if (spikes)
			{
				air.DrawRect(GridCell(at + new Vector2(0f, -g * 2f), g * 2f), Occ);
				air.DrawRect(GridCell(at + new Vector2(0f, g * 2f), g * 2f), Occ);
				air.DrawRect(GridCell(at + new Vector2(-g * 2f, 0f), g * 2f), Occ);
				air.DrawRect(GridCell(at + new Vector2(g * 2f, 0f), g * 2f), Occ);
			}
			air.DrawRect(GridCell(at, g * 2f), Bark);
			air.DrawRect(GridCell(at + new Vector2(-g * 0.5f, -g * 0.5f), g), BarkLit);
			if (spikes)
			{
				air.DrawRect(GridCell(at + new Vector2(0f, -g * 2f), g), thornTip);
				air.DrawRect(GridCell(at + new Vector2(0f, g * 2f), g), tipGlow);
				air.DrawRect(GridCell(at + new Vector2(-g * 2f, 0f), g), thornTip);
				air.DrawRect(GridCell(at + new Vector2(g * 2f, 0f), g), tipGlow);
			}
			if (diagonals)
			{
				air.DrawRect(GridCell(at + new Vector2(-g * 1.5f, -g * 1.5f), g), livingMid);
				air.DrawRect(GridCell(at + new Vector2(g * 1.5f, -g * 1.5f), g), livingMid);
				air.DrawRect(GridCell(at + new Vector2(-g * 1.5f, g * 1.5f), g), living);
				air.DrawRect(GridCell(at + new Vector2(g * 1.5f, g * 1.5f), g), living);
			}
		}
	}
}
