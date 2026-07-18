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
		if (result.EnemiesKilled > 0)
			changed |= UnlockAchievement(data, "first_blood");

		if (result.TimeSurvived >= 600f)
			changed |= UnlockAchievement(data, "survivor");

		if (result.TimeSurvived >= 1200f)
			changed |= UnlockAchievement(data, "veteran");

		if (result.FinalPlayerLevel >= 20)
		{
			changed |= UnlockAchievement(data, "max_level");
			changed |= GlobalStatsManager.UnlockSpell(data, "solar_flare");
		}

		if (result.ElementCounts != null && result.ElementCounts.Values.Any(count => count >= 4))
			changed |= UnlockAchievement(data, "elementalist");

		if (result.EquippedSpells != null && result.EquippedSpells.Count >= Player.MaxSpellSlots)
			changed |= UnlockAchievement(data, "hoarder");

		if (result.TimeSurvived >= 300f && !result.TookDamageBeforeFiveMinutes)
			changed |= UnlockAchievement(data, "untouchable");

		if (result.Outcome == global::RunOutcome.Victory && !string.IsNullOrWhiteSpace(result.BossId))
		{
			changed |= ApplyBossAchievement(data, result.BossId);
		}

		return changed;
	}

	private static bool ApplyBossAchievement(SaveData data, string bossId)
	{
		string normalized = bossId.Trim().ToLowerInvariant();
		bool changed = false;
		if (normalized.Contains("forest") || normalized.Contains("enchanted"))
			changed |= UnlockAchievement(data, "forest_cleared");
		if (normalized.Contains("castle") || normalized.Contains("cursed"))
		{
			changed |= UnlockAchievement(data, "castle_conqueror");
			changed |= GlobalStatsManager.UnlockSpell(data, "shadow_bolt");
		}
		if (normalized.Contains("ruins") || normalized.Contains("mystic"))
			changed |= UnlockAchievement(data, "ruins_delver");

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