using System;
using System.Linq;

namespace WizardSurvivors.scripts;

public static class AchievementManager
{
	public static bool ApplyRunAchievements(SaveData data, global::RunResult result)
	{
		if (data == null || result == null)
			return false;

		bool changed = false;
		foreach (AchievementDefinition definition in AchievementDefinitions.All)
		{
			if (!definition.IsComplete(result))
				continue;

			changed |= UnlockAchievement(data, definition.Id);
			if (!string.IsNullOrWhiteSpace(definition.SpellUnlockId))
				changed |= GlobalStatsManager.UnlockSpell(data, definition.SpellUnlockId);
		}

		return changed;
	}

	private static bool UnlockAchievement(SaveData data, string achievementId)
	{
		if (data.UnlockedAchievementIds.Any(id => id.Equals(achievementId, StringComparison.OrdinalIgnoreCase)))
			return false;

		data.UnlockedAchievementIds.Add(achievementId);
		return true;
	}
}