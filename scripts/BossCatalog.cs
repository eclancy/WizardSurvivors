using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>
/// One stage's end-of-run boss. A stage without an entry here still ends at
/// <c>Node2DGame.TimerVictorySeconds</c> the old way, so stages can gain bosses one at a time.
/// </summary>
public sealed class BossDefinition
{
	/// <summary>
	/// Stable id written into <c>RunResult.BossId</c>. AchievementDefinitions matches on substrings
	/// of this ("forest", "castle", "ruins"), so the id must contain its stage's keyword.
	/// </summary>
	public string Id { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	/// <summary>One line shown under the name on the arrival banner: what this boss punishes.</summary>
	public string Tagline { get; init; } = string.Empty;
	public string ScenePath { get; init; } = string.Empty;
	/// <summary>Health before the balance preset's elite scaling is applied.</summary>
	public int Health { get; init; } = 1000;
	/// <summary>Flat Arcane Energy added on top of the usual run reward for the kill.</summary>
	public int ArcaneVictoryBonus { get; init; } = 120;
	/// <summary>Stage id this victory unlocks, or empty if it gates nothing.</summary>
	public string UnlocksStageId { get; init; } = string.Empty;
}

public static class BossCatalog
{
	// Keyed by Global.SelectedStageIdx, the same index StageSelection hands to the run.
	private static readonly Dictionary<int, BossDefinition> ByStageIndex = new()
	{
		[0] = new BossDefinition
		{
			Id = "forest_treant",
			DisplayName = "Elderbark, the Treant",
			Tagline = "Slow, immense, and impossible to burst down. Bring damage you can sustain.",
			ScenePath = "res://scenes/ForestTreantBoss.tscn",
			Health = 4200,
			ArcaneVictoryBonus = 150,
			UnlocksStageId = "stage_1"
		}
	};

	public static BossDefinition ForStageIndex(int stageIndex)
	{
		return ByStageIndex.TryGetValue(stageIndex, out BossDefinition definition) ? definition : null;
	}

	public static BossDefinition GetById(string bossId)
	{
		if (string.IsNullOrWhiteSpace(bossId))
			return null;

		return ByStageIndex.Values.FirstOrDefault(boss => boss.Id.Equals(bossId, System.StringComparison.OrdinalIgnoreCase));
	}

	public static IEnumerable<BossDefinition> All => ByStageIndex.Values;

	/// <summary>The stage index a boss guards, for validation. -1 when the boss is not in the table.</summary>
	public static int StageIndexOf(BossDefinition definition)
	{
		foreach (KeyValuePair<int, BossDefinition> entry in ByStageIndex)
		{
			if (ReferenceEquals(entry.Value, definition))
				return entry.Key;
		}

		return -1;
	}
}
