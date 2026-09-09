using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Summoner (issue #33). It keeps its distance and periodically calls up a small pack of weak,
// fast minions around itself.
//
// It is the only enemy in the roster that makes the player choose a target. Everything else is
// killed in whatever order it arrives, because killing any one of them changes nothing about the
// rest; killing this one stops the fight getting worse. That is a decision the swarm cannot pose on
// its own, and it is worth more than the damage the summoner itself never deals - it has no attack
// at all.
//
// Two hard caps keep it honest, and both matter for different reasons. MaxActiveMinions bounds what
// it costs to run in a scene that is already swarm-heavy. MaxTotalSummons bounds what it *pays*: a
// summoner that produced minions forever would be a standing XP fountain, and parking next to one
// would be better than playing the stage.
public partial class SummonerEnemy : Enemy
{
	[Export] public float SummonIntervalSeconds { get; set; } = 6.5f;
	[Export] public float SummonWindUpSeconds { get; set; } = 1.1f;
	[Export] public int MinionsPerSummon { get; set; } = 2;

	/// <summary>Live minions from this summoner. The performance cap.</summary>
	[Export] public int MaxActiveMinions { get; set; } = 3;

	/// <summary>Minions this summoner will ever produce. The anti-farming cap - see the class comment.</summary>
	[Export] public int MaxTotalSummons { get; set; } = 8;

	[Export] public string MinionScenePath { get; set; } = "res://scenes/FastEnemy.tscn";

	// Minion health is a fraction of this summoner's own, which Node2DGame already scaled for the
	// minute of the run it spawned in. A flat number would make late-game minions free kills and
	// the whole enemy pointless after five minutes.
	[Export] public float MinionHealthFraction { get; set; } = 0.35f;

	/// <summary>How far out the minions appear. Far enough to read as a ring, close enough to be its doing.</summary>
	[Export] public float SummonSpawnRadius { get; set; } = 58f;

	// It has no attack, so its whole positioning job is to be inconvenient to reach. Ordered set
	// like RangedEnemy's: inside retreat it backs off, between the two it circles, beyond standoff
	// it closes. RegressionChecks enforces the ordering.
	[Export] public float StandoffDistance { get; set; } = 300f;
	[Export] public float RetreatDistance { get; set; } = 190f;

	[Export] public Color SummonColor { get; set; } = new Color(0.55f, 0.85f, 0.62f);

	private AttackTelegraph summon;
	private PackedScene minionScene;
	private readonly List<Enemy> activeMinions = new();
	private int totalSummoned;
	// Which way it circles. Fixed per enemy so a pair of summoners drift apart rather than stacking.
	private float strafeSign = 1f;

	public override void _Ready()
	{
		base._Ready();
		summon = new AttackTelegraph(SummonIntervalSeconds, SummonWindUpSeconds);
		strafeSign = (GetInstanceId() % 2 == 0) ? 1f : -1f;

		minionScene = ResourceLoader.Load<PackedScene>(MinionScenePath);
		if (minionScene == null)
			GD.PushError($"SummonerEnemy '{EnemyType}': minion scene not found at '{MinionScenePath}', so it will never summon.");
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickSummon((float)delta);
	}

	// Approach, circle, or back off - and stand still while calling, because the ring it draws is a
	// promise about where the minions arrive.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		if (summon != null && summon.IsWindingUp)
			return Vector2.Zero;

		if (distanceToPlayer > StandoffDistance)
			return chaseDirection;

		if (distanceToPlayer < RetreatDistance)
			return -chaseDirection;

		return new Vector2(-chaseDirection.Y, chaseDirection.X) * strafeSign;
	}

	private void TickSummon(float delta)
	{
		PruneDeadMinions();

		switch (summon.Tick(delta, CanSummon()))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				SummonMinions();
				break;
		}
	}

	// Cheap and allocation-free: a swept in-place compaction rather than LINQ or a new list, because
	// this runs every physics frame on every summoner alive.
	private void PruneDeadMinions()
	{
		for (int i = activeMinions.Count - 1; i >= 0; i--)
		{
			Enemy minion = activeMinions[i];
			if (minion == null || !IsInstanceValid(minion) || !minion.IsInGroup("enemies"))
				activeMinions.RemoveAt(i);
		}
	}

	private bool CanSummon()
	{
		if (minionScene == null || totalSummoned >= MaxTotalSummons)
			return false;

		if (activeMinions.Count >= MaxActiveMinions)
			return false;

		// No point calling for help at a player who is not there. Also stops a summoner that has
		// wandered off-screen from quietly spending its lifetime budget where nobody can see it.
		Node2D target = TargetPlayer;
		return target != null && IsInstanceValid(target);
	}

	private void SummonMinions()
	{
		Node arena = GetParent();
		if (arena == null || !IsInstanceValid(arena) || minionScene == null)
			return;

		// Both caps are re-checked per minion, not just once for the batch, so a summon that begins
		// with one slot free adds one minion rather than the full MinionsPerSummon.
		int wanted = Mathf.Max(1, MinionsPerSummon);
		for (int i = 0; i < wanted; i++)
		{
			if (activeMinions.Count >= MaxActiveMinions || totalSummoned >= MaxTotalSummons)
				return;

			if (minionScene.Instantiate() is not Enemy minion)
			{
				GD.PushError($"SummonerEnemy '{EnemyType}': '{MinionScenePath}' has no Enemy script attached.");
				return;
			}

			minion.Health = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * MinionHealthFraction));
			minion.GlobalPosition = GlobalPosition + SpawnOffsetFor(i, wanted);

			activeMinions.Add(minion);
			totalSummoned++;
			// Parented to the arena, not to this summoner: killing the caller must not delete the
			// minions that are already the player's problem. Same reason Enemy parents its XP orbs
			// out rather than keeping them as children.
			arena.CallDeferred("add_child", minion);
		}
	}

	// Evenly spaced around the ring the tell drew, offset by the summoner's identity so two of them
	// standing together do not drop minions on the same spots.
	private Vector2 SpawnOffsetFor(int index, int count)
	{
		float baseAngle = (GetInstanceId() % 360) * Mathf.Pi / 180f;
		float angle = baseAngle + (Mathf.Tau * index / Mathf.Max(1, count));
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SummonSpawnRadius;
	}

	// A recycled summoner starts its cooldown over, but keeps its lifetime budget spent: resetting
	// totalSummoned would turn Node2DGame's respawn recycling into a way around MaxTotalSummons.
	public override void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		base.ResetForRespawn(newPos, newHealth);
		summon = new AttackTelegraph(SummonIntervalSeconds, SummonWindUpSeconds);
	}

	public override void _Draw()
	{
		if (summon == null || !summon.IsWindingUp)
			return;

		// The tell shows *where*, not just *when*: a ring at the summon radius plus a mote growing
		// on each spot a minion is about to occupy. A player who reads it walks out of the ring and
		// gets a free moment on the summoner before the pack turns around.
		float progress = summon.WindUpProgress;
		var ring = new Color(SummonColor.R, SummonColor.G, SummonColor.B, 0.25f + 0.40f * progress);
		DrawArc(Vector2.Zero, SummonSpawnRadius, 0f, Mathf.Tau, 32, ring, 2.5f, true);

		int wanted = Mathf.Max(1, MinionsPerSummon);
		for (int i = 0; i < wanted; i++)
		{
			Vector2 spot = SpawnOffsetFor(i, wanted);
			DrawCircle(spot, Mathf.Lerp(2.0f, 9.0f, progress), new Color(SummonColor.R, SummonColor.G, SummonColor.B, 0.30f + 0.45f * progress));
		}
	}
}
