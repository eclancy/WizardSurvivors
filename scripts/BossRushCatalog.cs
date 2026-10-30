using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>
/// BOSS RUSH (issue #12, "the ultimate endgame mode with sequential boss encounters";
/// .ai/boss-levels-design.md, "Boss Rush"). One run in which every warden of the campaign comes for
/// the player in the order the campaign meets them, and the last is the Warden of the Deep.
/// </summary>
/// <remarks>
/// It is an ordinary run with its bosses spread through it rather than one at the end, and the
/// shape is chosen so that nothing about pacing has to be re-tuned for it:
///
///   - The arena is the final chapter, his seat. The rush is the long road back walked in one go,
///     which is also what the Warden of the Deep's own fight already says ("every fight you have
///     already won, in sequence"), so the mode ends on the one boss built to end it.
///   - The bosses arrive on the PACE clock, evenly from <see cref="FirstArrivalPaceSeconds"/>, and the
///     last at the ordinary boss time. The clock stops for every fight, as it does for the one boss
///     of a normal run, so a fight never costs the player run time and the gap between two arrivals is
///     always the full step of swarm, XP and chests.
///   - Each boss is scaled to where in the run it arrives, by the same curve the swarm's health
///     follows (Node2DGame.BossRushHealthScale): a boss met at minute four is as tough against a
///     minute-four build as it is against a fifteen-minute one at the end of its own chapter.
///   - Felling one pays a chest where it fell and half its chapter bonus in Arcane Energy; the run
///     is won only when the last falls.
///
/// It opens only once the Warden of the Deep has fallen in his own chapter, so nobody meets a boss
/// here before they have beaten it where it lives, and so the rush records nothing about chapters:
/// it opens none, grants no chapter deed and writes no boss to the save's defeated list
/// (Node2DGame.BuildRunResult). Its own records are SaveData.BossRushBestFelled and
/// BossRushBestClearSeconds.
/// </remarks>
public static class BossRushCatalog
{
	/// <summary>When the first boss arrives, on the pace clock. The rest follow evenly to the ordinary boss time.</summary>
	/// <remarks>
	/// Two and a half minutes: long enough for a wizard to hold two or three spells, and early enough
	/// that the run is mostly bosses. Earlier and Elderbark meets a lone level-five starter spell.
	/// </remarks>
	public const float FirstArrivalPaceSeconds = 150f;

	/// <summary>
	/// The floor on a boss's health scale. The swarm curve starts at 6 / 156 of its end value, which
	/// would make a boss at minute two a few seconds' work: too short to see the fight it is.
	/// </summary>
	public const float MinHealthScale = 0.2f;

	/// <summary>Of each felled boss's ArcaneVictoryBonus, the share the rush pays.</summary>
	/// <remarks>
	/// Half: a full clear pays about twice what the finale pays on its own, for a run a few minutes
	/// longer than an ordinary one that faces eight bosses instead of one.
	/// </remarks>
	public const float ArcaneBonusShare = 0.5f;

	/// <summary>
	/// The chapter the rush is fought in: the Emberdeep, the first gate chapter (StageCatalog.GateChapter).
	/// It was the campaign's last (FinalChapter), which is the Throne once the castle comes after the
	/// gate - and the rush is the road's wardens on the road's summit, not a fight in his hall
	/// (.ai/endgame-castle.md 1.8, unchanged by Eric's choice).
	/// </summary>
	public static StageDefinition Arena => StageCatalog.GateChapter;

	/// <summary>
	/// Every campaign boss that has a scene of its own, in campaign order; the final chapter's is
	/// last. A chapter whose boss still fights as a stand-in is left out - the rush would be Elderbark
	/// three times - and joins by itself the day its scene exists.
	/// </summary>
	public static IReadOnlyList<BossDefinition> Roster =>
		StageCatalog.Campaign
			.Select(stage => BossCatalog.ForStageIndex(stage.Index))
			.Where(boss => boss != null && !boss.IsOnStandIn)
			.ToList();

	/// <summary>The rush's last boss: the final chapter's.</summary>
	public static BossDefinition Finale => Arena == null ? null : BossCatalog.ForStageIndex(Arena.Index);

	/// <summary>Whether the save has opened the rush: the final chapter's boss has fallen there.</summary>
	public static bool IsOpen(SaveData data)
	{
		BossDefinition finale = Finale;
		return data != null && finale != null && data.Lifetime != null && data.Lifetime.HasDefeatedBoss(finale.Id);
	}

	/// <summary>
	/// When the boss at <paramref name="order"/> (0-based) of <paramref name="count"/> arrives, on the
	/// pace clock. The last arrives at <paramref name="finalePaceSeconds"/>, the ordinary boss time.
	/// </summary>
	public static float ArrivalPaceSeconds(int order, int count, float finalePaceSeconds)
	{
		if (count <= 1 || order >= count - 1)
			return finalePaceSeconds;
		float step = (finalePaceSeconds - FirstArrivalPaceSeconds) / (count - 1);
		return FirstArrivalPaceSeconds + order * step;
	}
}
