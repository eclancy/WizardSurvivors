using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// FALLING BEHIND IS FATAL (.ai/run-pacing.md, section 10). Eric, after a playtest: "if they fall
// behind they should eventually die, change how spawning/moving enemies works so that if the player
// falls behind, they will eventually get swarmed."
//
// Until this, a player whose build stopped keeping pace was protected by the arena itself. The live
// cap (140) stopped the trickle the moment the field was full, so an arena he could not clear simply
// stopped growing; enemies he walked away from were recycled to a random point on the ring, so the
// tail he left behind never caught him; and nothing the spawner did cared how many were already
// alive. Kiting at 220 against a horde at 0.85 of 74-155 was safe for as long as he cared to do it.
//
// THE SIGNAL is the one the arena already gives: the trickle choking on the cap. A spawn tick that
// wanted to send a body - not the boss fight, not the grace after a level-up, burst budget left - and
// found the field full means the player is not killing what the run is sending. Every half second the
// share of ticks that choked is read; pressure climbs (over SwarmPressureRiseSeconds) after a sweep
// where at least SwarmChokedShareToRise of them did and drains three times as fast after one where
// they did not, so it only builds while about three sweeps in four choke. A dense wave that is
// cleared costs nothing, a freeze build that holds the field near its cap without clearing it is
// tolerated, and a field nobody clears is at full pressure in about a minute. Measured at full
// fidelity (.ai/run-pacing.md 10.4): bots that keep up never passed 0.4 choked in a minute or 0.02
// pressure; one that took one level-up in three was at full pressure by minute six. Ticks come every spawnInterval, so the share is about
// exp(-kills a second x interval): the same threshold asks for more kills as the run's interval
// shrinks, which is the pace clock doing its job without a table. It reads no XP figure and no level
// curve, so it cannot drift when those are retuned.
//
// WHAT PRESSURE DOES, all through position, speed, toughness and folding, never through more nodes:
//   1. Stragglers catch up. A body off screen walks faster (SwarmStragglerSpeedBonus), one on screen a
//      little faster, so the comet tail behind a kiting player closes rather than trailing forever.
//   2. The ring closes. Fresh spawns and recycled bodies land nearer the screen edge, more of them
//      ahead of the player's heading (SwarmForwardBiasFull), and the recycler may move more bodies a
//      second. Past SwarmTrailRecyclePressure the sweep itself sends bodies far behind him round to
//      the front, so walking away from the horde walks into it.
//   3. The field swells. Past SwarmSwellPressure, bodies off screen fold into one another
//      (Enemy.AbsorbIntoSwarm): the absorber takes the other's health, mass and XP and hits harder,
//      the other leaves without dying. Each fold frees a slot the trickle then fills, so the cap holds
//      the node count while the arena's mass keeps growing with the schedule it could not place.
//
// A player who keeps the field under the cap never builds pressure, and every value above is then
// exactly today's: SwarmEdgeMargin is SpawnEdgeMargin, SwarmRecycleAllowance is
// MaxEnemyRecyclesPerSecond, every SwarmSpeedScale is 1 and nothing folds. The work is one sweep of the
// live enemies every SwarmSweepSeconds while there is pressure to apply, never per frame.
public partial class Node2DGame
{
	// Defaults as constants so RegressionChecks.ValidateSwarmPressure can hold them without building a
	// game node; the exports below start at them and can be tuned in the inspector.
	public const float SwarmDefaultStartPaceSeconds = 120f;
	public const float SwarmDefaultRiseSeconds = 45f;
	public const float SwarmDefaultFallSeconds = 15f;
	public const float SwarmDefaultChokedShareToRise = 0.5f;
	public const float SwarmDefaultStragglerSpeedBonus = 0.75f;
	public const float SwarmDefaultNearSpeedBonus = 0.2f;
	public const float SwarmDefaultFullEdgeMargin = 1.02f;
	public const float SwarmDefaultRingTighten = 0.6f;
	public const float SwarmDefaultForwardBiasFull = 3.5f;
	public const int SwarmDefaultExtraRecyclesPerSecond = 9;
	public const float SwarmDefaultTrailRecyclePressure = 0.5f;
	public const float SwarmDefaultSwellPressure = 0.35f;
	public const int SwarmDefaultSwellsPerSweep = 2;
	public const int SwarmDefaultMaxMass = 6;

	[Export] public bool EnableSwarmPressure { get; set; } = true;
	// P: before this point of the reference run no pressure builds. The opening is a lesson, and the
	// first elite (2:15) has not even arrived.
	[Export] public float SwarmPressureStartPaceSeconds { get; set; } = SwarmDefaultStartPaceSeconds;
	// R: a fully choked trickle takes this long to bring pressure from none to full, and a cleared one
	// this long to drain it. Real seconds: how fast a crowd thickens is a feel, not a point in the run.
	[Export] public float SwarmPressureRiseSeconds { get; set; } = SwarmDefaultRiseSeconds;
	[Export] public float SwarmPressureFallSeconds { get; set; } = SwarmDefaultFallSeconds;
	// F: the share of trickle ticks in a sweep that must choke for pressure to climb rather than drain.
	[Export] public float SwarmChokedShareToRise { get; set; } = SwarmDefaultChokedShareToRise;
	// F: speed bonus at full pressure, as a share of the body's own speed - off screen and on.
	[Export] public float SwarmStragglerSpeedBonus { get; set; } = SwarmDefaultStragglerSpeedBonus;
	[Export] public float SwarmNearSpeedBonus { get; set; } = SwarmDefaultNearSpeedBonus;
	// F: at full pressure the ring sits at this multiple of the screen edge (not under 1, or a body
	// appears on camera out of nothing - the bug the screen-edge ring was built to end) and keeps
	// this share less of its random depth.
	[Export] public float SwarmFullEdgeMargin { get; set; } = SwarmDefaultFullEdgeMargin;
	[Export] public float SwarmRingTighten { get; set; } = SwarmDefaultRingTighten;
	// F: the forward-bias exponent at full pressure (SpawnForwardBias is 1.8: two thirds ahead).
	[Export] public float SwarmForwardBiasFull { get; set; } = SwarmDefaultForwardBiasFull;
	// F: recycles a second added at full pressure, on top of MaxEnemyRecyclesPerSecond.
	[Export] public int SwarmExtraRecyclesPerSecond { get; set; } = SwarmDefaultExtraRecyclesPerSecond;
	// F: pressure past which bodies far behind a moving player are sent round to the front.
	[Export] public float SwarmTrailRecyclePressure { get; set; } = SwarmDefaultTrailRecyclePressure;
	// F: pressure past which bodies off screen fold into one another, how many folds a sweep makes
	// at full pressure, and the most bodies one may become.
	[Export] public float SwarmSwellPressure { get; set; } = SwarmDefaultSwellPressure;
	[Export] public int SwarmSwellsPerSweep { get; set; } = SwarmDefaultSwellsPerSweep;
	[Export] public int SwarmMaxMass { get; set; } = SwarmDefaultMaxMass;

	/// <summary>How far behind the player is, 0 (keeping up) to 1 (overrun). Read by the harness and F3.</summary>
	public float SwarmPressure { get; private set; }

	/// <summary>Bodies folded into others this run, and bodies sent round from behind - for the harness.</summary>
	public int SwarmSwellsThisRun { get; private set; }
	public int SwarmTrailRecyclesThisRun { get; private set; }

	/// <summary>Trickle ticks this run that counted toward pressure, and how many of them choked.</summary>
	public int SwarmTrickleTicksThisRun { get; private set; }
	public int SwarmTrickleChokedThisRun { get; private set; }

	// R: the sweep's cadence. Two a second is fast enough that a crowd's speed follows the pressure
	// within a stride, and slow enough that its cost - one pass over the live enemies - is noise.
	private const float SwarmSweepSeconds = 0.5f;
	private float swarmSweepTimer;
	private int swarmTrickleTicks, swarmTrickleChoked;
	// Slots freed by folding since the last trickle tick. The trickle fills them at once, and a tick
	// that could place a body only because the swarm folded two into one is still a tick the player
	// did not make room for: counted as choked, or folding would erase the signal that drives it and
	// pressure would fall the moment it began to work.
	private int swarmFoldsSinceTick;
	private float swarmChokedShare;
	// True while some body may still carry a speed scale other than 1, so the sweep that finds the
	// pressure gone puts every one back exactly once.
	private bool swarmScalesApplied;
	private readonly List<Enemy> swarmFoldCandidates = new();

	// --- read by the spawner and the recycler (each is today's value at no pressure) -------------

	private float SwarmEdgeMargin => Mathf.Lerp(SpawnEdgeMargin, Mathf.Min(SpawnEdgeMargin, Mathf.Max(1.0f, SwarmFullEdgeMargin)), SwarmPressure);
	private float SwarmEdgeDepth => SpawnEdgeDepth * (1f - Mathf.Clamp(SwarmRingTighten, 0f, 1f) * SwarmPressure);
	private float SwarmForwardBias => Mathf.Lerp(SpawnForwardBias, Mathf.Max(SpawnForwardBias, SwarmForwardBiasFull), SwarmPressure);
	private int SwarmRecycleAllowance => MaxEnemyRecyclesPerSecond + Mathf.RoundToInt(Mathf.Max(0, SwarmExtraRecyclesPerSecond) * SwarmPressure);

	// A recycled body comes back at the run's current strength, as the type it is, and as all the
	// bodies it has become.
	private int SwarmRecycleHealth(Enemy enemy) =>
		Mathf.Max(1, Mathf.RoundToInt(spawnHealth * Mathf.Max(0.1f, enemy.SpawnHealthMultiplier) * enemy.SwarmMass));

	private string SwarmDebugLine() =>
		$"Swarm pressure {SwarmPressure:0.00} | choked {swarmChokedShare:0.00} | folds {SwarmSwellsThisRun} | sent round {SwarmTrailRecyclesThisRun}";

	/// <summary>One trickle tick, from the spawn loop: did it find the field too full to send anything?</summary>
	private void NoteTrickleTick(int alive, int trickleCap)
	{
		int folded = swarmFoldsSinceTick;
		swarmFoldsSinceTick = 0;
		if (bossFightActive || spawnGraceRemaining > 0f)
			return;
		// A burst window already spent is the run's own ceiling, not the player falling behind.
		if (MaxSpawnsPerBurstWindow - spawnCountInBurstWindow <= 0)
			return;
		swarmTrickleTicks++;
		SwarmTrickleTicksThisRun++;
		if (alive + folded >= trickleCap)
		{
			swarmTrickleChoked++;
			SwarmTrickleChokedThisRun++;
		}
	}

	private void TickSwarmPressure(float delta)
	{
		swarmSweepTimer += delta;
		if (swarmSweepTimer < SwarmSweepSeconds)
			return;
		float dt = swarmSweepTimer;
		swarmSweepTimer = 0f;

		if (swarmTrickleTicks > 0)
			swarmChokedShare = swarmTrickleChoked / (float)swarmTrickleTicks;
		swarmTrickleTicks = 0;
		swarmTrickleChoked = 0;

		bool canBuild = EnableSwarmPressure && !bossFightActive && !runFinished
			&& player != null && IsInstanceValid(player)
			&& PaceSeconds >= SwarmPressureStartPaceSeconds;
		bool rising = canBuild && swarmChokedShare >= SwarmChokedShareToRise;
		float rate = rising
			? 1f / Mathf.Max(0.1f, SwarmPressureRiseSeconds)
			: -1f / Mathf.Max(0.1f, SwarmPressureFallSeconds);
		SwarmPressure = Mathf.Clamp(SwarmPressure + rate * dt, 0f, 1f);

		if (SwarmPressure <= 0f && !swarmScalesApplied)
			return;
		SweepSwarm();
	}

	// One pass over the live enemies: set every eligible body's speed for the pressure and where it
	// stands, collect the ones off screen that may fold, and send round the ones far behind.
	private void SweepSwarm()
	{
		if (player == null || !IsInstanceValid(player))
			return;

		float p = SwarmPressure;
		Vector2 origin = player.GlobalPosition;
		float visible = GetVisibleRadius();
		float visibleSq = visible * visible;
		float trailSq = visibleSq * 1.6f;
		Vector2 heading = measuredPlayerVelocity.LengthSquared() > 400f ? measuredPlayerVelocity.Normalized() : Vector2.Zero;
		bool sendRound = p >= SwarmTrailRecyclePressure && heading != Vector2.Zero;
		bool fold = p >= SwarmSwellPressure && SwarmMaxMass >= 2;
		swarmFoldCandidates.Clear();

		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !enemy.SwarmEligible || enemy.IsDying || !IsInstanceValid(enemy))
				continue;
			Vector2 offset = enemy.GlobalPosition - origin;
			float distanceSq = offset.LengthSquared();
			bool offScreen = distanceSq > visibleSq;
			enemy.SwarmSpeedScale = 1f + p * (offScreen ? SwarmStragglerSpeedBonus : SwarmNearSpeedBonus);
			if (!offScreen)
				continue;
			// Behind him and well clear of the screen: round to the front, through the recycler, which
			// owns the rate cap and the placement.
			if (sendRound && distanceSq > trailSq && offset.Dot(heading) < -0.3f * Mathf.Sqrt(distanceSq)
				&& recyclesThisWindow < SwarmRecycleAllowance)
			{
				// The recycler may still decline (its window); only a body that moved was sent.
				Vector2 was = enemy.GlobalPosition;
				RespawnEnemy(enemy);
				if (enemy.GlobalPosition != was)
				{
					SwarmTrailRecyclesThisRun++;
					continue;
				}
			}
			// Only the plain chaser folds. A telegraphed body draws its tell in local space against
			// numbers its own script owns, and a caster's whole threat is the bolt, not the body.
			if (fold && enemy.GetType() == typeof(Enemy) && enemy.IsTargetable && enemy.SwarmMass < SwarmMaxMass)
				swarmFoldCandidates.Add(enemy);
		}
		swarmScalesApplied = p > 0f;

		if (fold)
			FoldSwarm(Mathf.Max(1, Mathf.RoundToInt(p * SwarmSwellsPerSweep)), origin);
	}

	// The farthest body off screen is taken in by the off-screen body nearest the player that has room
	// for its mass: what is left furthest behind becomes weight at the front of the pack.
	private void FoldSwarm(int folds, Vector2 origin)
	{
		for (int n = 0; n < folds && swarmFoldCandidates.Count >= 2; n++)
		{
			int farthest = -1;
			float farthestSq = -1f;
			for (int i = 0; i < swarmFoldCandidates.Count; i++)
			{
				float d = swarmFoldCandidates[i].GlobalPosition.DistanceSquaredTo(origin);
				if (d > farthestSq)
				{
					farthestSq = d;
					farthest = i;
				}
			}
			Enemy absorbed = swarmFoldCandidates[farthest];
			swarmFoldCandidates.RemoveAt(farthest);

			int nearest = -1;
			float nearestSq = float.MaxValue;
			for (int i = 0; i < swarmFoldCandidates.Count; i++)
			{
				Enemy candidate = swarmFoldCandidates[i];
				if (candidate.SwarmMass + absorbed.SwarmMass > SwarmMaxMass)
					continue;
				float d = candidate.GlobalPosition.DistanceSquaredTo(origin);
				if (d < nearestSq)
				{
					nearestSq = d;
					nearest = i;
				}
			}
			if (nearest < 0)
				continue;
			Enemy absorber = swarmFoldCandidates[nearest];
			absorber.AbsorbIntoSwarm(absorbed);
			SwarmSwellsThisRun++;
			swarmFoldsSinceTick++;
			if (absorber.SwarmMass >= SwarmMaxMass)
				swarmFoldCandidates.RemoveAt(nearest);
		}
	}
}
