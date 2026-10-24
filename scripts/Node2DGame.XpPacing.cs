using Godot;

// XP KEEPS PACE WITH THE RUN (.ai/run-pacing.md, section 10). Eric, after a playtest: "exp should
// scale better so the player levels up reasonably as they go through the levels."
//
// A level costs more every level (Player.CalculateXPForLevel, linear), but what a kill paid never
// changed: two XP at minute one and two XP at minute fourteen. From minute four or five the field
// sits at its cap and the kill rate is what the build can manage, so income stopped growing while the
// cost kept climbing, and the gap between level-ups grew all run long: measured, a level every half
// minute at minute four and one a minute (or none) from minute eight, the run ending at level 20-21
// with no Ascension. A build that stops growing kills less as enemy health climbs, which pays less.
//
// So every point of XP is worth more the further into the run it is picked up: a multiplier on the
// pace clock, applied in Player.AddXp after every other bonus. On the pace clock because the run's
// own toughness is (spawn health is 6 + 10 a pace minute): the enemies that are harder to kill pay
// more, and a shorter run brings both on sooner together. It moves nothing about WHO levels: income
// is still kills, so a player who kills more still levels sooner, and one who falls behind still
// falls behind - the swarm deals with him (Node2DGame.SwarmPressure.cs). Applied to the XP rather than
// the orb, so the orb art keeps its three tiers (XPOrb) and the fraction is carried as every other
// bonus is (Player.xpFraction).
public partial class Node2DGame
{
	/// <summary>How much more a point of XP is worth for each pace minute of the run.</summary>
	/// <remarks>
	/// 0.10: a point is worth 1.5 at pace minute five, 2 at ten and 2.5 at the boss. Chosen on
	/// full-fidelity runs (--fixed-fps 30, .ai/run-pacing.md section 10.4) with the curve left at
	/// 10 + 45. 0.15 was the first try and was tuned on timescale-2 runs that, at the 1-5 fps a full
	/// field ran at, skipped enough spell hits to understate every bot's kills by a quarter or more;
	/// measured properly it ended the ordinary bot's run at level 37.
	/// </remarks>
	[Export] public float XpGrowthPerPaceMinute { get; set; } = XpDefaultGrowthPerPaceMinute;

	/// <summary>The default, as a constant RegressionChecks can hold without building a game node.</summary>
	public const float XpDefaultGrowthPerPaceMinute = 0.10f;

	/// <summary>What a point of XP picked up now is worth; frozen with the clock when the boss arrives.</summary>
	public float XpPaceMultiplier => 1f + Mathf.Max(0f, XpGrowthPerPaceMinute) * PaceMinutes;
}
