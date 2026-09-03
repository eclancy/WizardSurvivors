using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// The wind-up clock every telegraphed enemy attack runs on: wait out a cooldown, plant for a
/// visible wind-up, then resolve exactly once.
/// </summary>
/// <remarks>
/// This shape was written twice before it was named - <c>BossEnemy</c>'s ground slam had its own
/// pair of float timers, and the ranged caster needed the same pair with different numbers. It is
/// deliberately not a Node: an attack pattern is bookkeeping its owner ticks, not something with a
/// place in the scene tree, and a swarm-heavy scene should not pay for a child node per attacker.
///
/// The wind-up is the whole contract. An enemy attack that lands without one is indistinguishable
/// from damage arriving at random, so <see cref="Beat.Started"/> is the caller's cue to plant the
/// enemy and draw its tell, and <see cref="Beat.Resolved"/> only ever fires after the player has
/// had <see cref="WindUpSeconds"/> to react.
/// </remarks>
public sealed class AttackTelegraph
{
	public enum Beat
	{
		/// <summary>On cooldown, or holding fire because the caller said not yet.</summary>
		Idle,
		/// <summary>The wind-up began this frame: plant the enemy and play its tell.</summary>
		Started,
		/// <summary>Mid wind-up. The attack has not landed and can still be read.</summary>
		WindingUp,
		/// <summary>The attack lands this frame, once.</summary>
		Resolved
	}

	/// <summary>Seconds between the start of one attack and the next becoming available.</summary>
	public float IntervalSeconds { get; set; }

	/// <summary>Seconds the enemy spends visibly winding up before the attack lands.</summary>
	public float WindUpSeconds { get; set; }

	private float cooldown;
	private float windUpRemaining;

	public AttackTelegraph(float intervalSeconds, float windUpSeconds)
	{
		IntervalSeconds = Mathf.Max(0.05f, intervalSeconds);
		WindUpSeconds = Mathf.Max(0f, windUpSeconds);
		// Start on a full cooldown so nothing attacks on the frame it spawns, which would land
		// before the player has even seen it arrive.
		cooldown = IntervalSeconds;
	}

	public bool IsWindingUp => windUpRemaining > 0f;

	/// <summary>0 the frame the wind-up starts, 1 the frame before it resolves. For drawing tells.</summary>
	public float WindUpProgress => WindUpSeconds > 0f
		? 1f - Mathf.Clamp(windUpRemaining / WindUpSeconds, 0f, 1f)
		: 1f;

	/// <summary>
	/// Advances the clock one frame. <paramref name="wantsToAttack"/> is the caller's gate - range,
	/// line of sight, whatever the attack needs - and is only consulted at the moment a wind-up
	/// would start. Once started, a wind-up always resolves: an attack that could be cancelled by
	/// stepping away mid-tell would teach the player that the tell means nothing.
	/// </summary>
	public Beat Tick(float delta, bool wantsToAttack)
	{
		if (windUpRemaining > 0f)
		{
			windUpRemaining -= delta;
			if (windUpRemaining > 0f)
				return Beat.WindingUp;

			windUpRemaining = 0f;
			return Beat.Resolved;
		}

		cooldown -= delta;
		if (cooldown > 0f || !wantsToAttack)
			return Beat.Idle;

		// The cooldown is only consumed when the attack actually begins, so an enemy that spent the
		// whole cooldown out of range fires the instant it closes rather than waiting a second time.
		cooldown = IntervalSeconds;
		if (WindUpSeconds <= 0f)
			return Beat.Resolved;

		windUpRemaining = WindUpSeconds;
		return Beat.Started;
	}
}
