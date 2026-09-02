using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

public static class GlobalStatsManager
{
	public const float DynamicRewardMinMultiplier = 0.90f;
	public const float DynamicRewardMaxMultiplier = 1.55f;
	public const string BalancePresetCasual = "casual";
	public const string BalancePresetDefault = "default";
	public const string BalancePresetHardcore = "hardcore";

	private static readonly HashSet<string> DefaultUnlockedSpellIds = new(StringComparer.OrdinalIgnoreCase)
	{
		"magic_missile",
		"arcane_explosion",
		"spiritual_weapon",
		"fireball",
		"frost_shard",
		"shadow_bolt",
		"thorn_vine",
		"gale_blade",
		"solar_flare",
		"molten_shard",
		"chain_lightning",
		"toxic_spore_burst",
		"obsidian_spike",
		"cyclone_slash",
		"void_lance",
		"glacial_spike",
		"black_tentacles",
		"cone_of_cold",
		"scorching_ray",
		"meteor_swarm",
		"aegis_ward",
		"thornmail_barrier",
		"frozen_bulwark",
		"stormguard_aura",
		"venom_cloak",
		"guardian_vines",
		"tidal_barrier",
		"stone_bulwark",
		"blur",
		"fortunes_favor",
		"haste"
	};

	public static int GetUpgradeLevel(SaveData data, string upgradeId)
	{
		if (data == null || string.IsNullOrWhiteSpace(upgradeId))
			return 0;

		return data.ArcaneUpgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;
	}

	public static bool IsSpellUnlockedForLevelUp(SaveData data, string spellId)
	{
		if (string.IsNullOrWhiteSpace(spellId))
			return false;

		return DefaultUnlockedSpellIds.Contains(spellId)
			|| (data != null && data.UnlockedSpellIds.Any(id => id.Equals(spellId, StringComparison.OrdinalIgnoreCase)));
	}

	public static bool UnlockSpell(SaveData data, string spellId)
	{
		if (data == null || string.IsNullOrWhiteSpace(spellId) || IsSpellUnlockedForLevelUp(data, spellId))
			return false;

		data.UnlockedSpellIds.Add(spellId);
		return true;
	}

	// Stage 0 is always available - a fresh save with no stages unlocked must still have somewhere
	// to play. Everything past it is earned by beating the previous stage's boss.
	public static bool IsStageUnlocked(SaveData data, string stageId)
	{
		if (string.IsNullOrWhiteSpace(stageId))
			return false;

		if (stageId.Equals(DefaultUnlockedStageId, StringComparison.OrdinalIgnoreCase))
			return true;

		return data != null && data.UnlockedStageIds.Any(id => id.Equals(stageId, StringComparison.OrdinalIgnoreCase));
	}

	public static bool UnlockStage(SaveData data, string stageId)
	{
		if (data == null || string.IsNullOrWhiteSpace(stageId) || IsStageUnlocked(data, stageId))
			return false;

		data.UnlockedStageIds.Add(stageId);
		return true;
	}

	public const string DefaultUnlockedStageId = "stage_0";

	public static float GetDynamicArcaneRewardMultiplier(SaveData data, RunResult currentRun, out string breakdown)
	{
		float multiplier = 1.0f;
		var notes = new List<string>();
		if (data == null || currentRun == null)
		{
			breakdown = "No dynamic modifiers";
			return multiplier;
		}

		int defeatStreak = GetConsecutiveOutcomeCount(data, "Defeat");
		if (defeatStreak > 0)
		{
			float pityBonus = Math.Min(0.24f, defeatStreak * 0.045f);
			multiplier += pityBonus;
			notes.Add($"Pity +{Mathf.RoundToInt(pityBonus * 100f)}%");
		}

		int victoryStreak = GetConsecutiveOutcomeCount(data, "Victory");
		if (victoryStreak >= 2)
		{
			float momentumBonus = Math.Min(0.14f, (victoryStreak - 1) * 0.025f);
			multiplier += momentumBonus;
			notes.Add($"Momentum +{Mathf.RoundToInt(momentumBonus * 100f)}%");
		}

		float varietyBonus = CalculateVarietyBonus(data, currentRun);
		if (varietyBonus > 0f)
		{
			multiplier += varietyBonus;
			notes.Add($"Variety +{Mathf.RoundToInt(varietyBonus * 100f)}%");
		}

		float improvementBonus = CalculateImprovementBonus(data, currentRun);
		if (improvementBonus > 0f)
		{
			multiplier += improvementBonus;
			notes.Add($"Improvement +{Mathf.RoundToInt(improvementBonus * 100f)}%");
		}

		multiplier = Mathf.Clamp(multiplier, DynamicRewardMinMultiplier, DynamicRewardMaxMultiplier);
		breakdown = notes.Count == 0 ? "No dynamic modifiers" : string.Join(", ", notes);
		return multiplier;
	}

	public static float GetNextRunArcanePreviewMultiplier(SaveData data)
	{
		if (data == null)
			return 1.0f;

		int defeatStreak = GetConsecutiveOutcomeCount(data, "Defeat");
		int victoryStreak = GetConsecutiveOutcomeCount(data, "Victory");
		float multiplier = 1.0f;
		if (defeatStreak > 0)
			multiplier += Math.Min(0.24f, defeatStreak * 0.045f);
		if (victoryStreak >= 2)
			multiplier += Math.Min(0.14f, (victoryStreak - 1) * 0.025f);

		return Mathf.Clamp(multiplier, DynamicRewardMinMultiplier, DynamicRewardMaxMultiplier);
	}

	public static string NormalizeBalancePresetId(string? presetId)
	{
		string normalized = presetId?.Trim().ToLowerInvariant() ?? BalancePresetDefault;
		return normalized switch
		{
			BalancePresetCasual => BalancePresetCasual,
			BalancePresetHardcore => BalancePresetHardcore,
			_ => BalancePresetDefault
		};
	}

	public static string GetBalancePresetDisplayName(string? presetId)
	{
		return NormalizeBalancePresetId(presetId) switch
		{
			BalancePresetCasual => "Casual",
			BalancePresetHardcore => "Hardcore",
			_ => "Default"
		};
	}

	public static float GetArcaneRewardScaleForPreset(string? presetId)
	{
		return NormalizeBalancePresetId(presetId) switch
		{
			BalancePresetCasual => 0.92f,
			BalancePresetHardcore => 1.12f,
			_ => 1.00f
		};
	}

	public static int GetConsecutiveOutcomeCount(SaveData data, string outcome)
	{
		if (data == null || data.RunTelemetryHistory == null || data.RunTelemetryHistory.Count == 0 || string.IsNullOrWhiteSpace(outcome))
			return 0;

		int count = 0;
		foreach (RunTelemetryRecord run in data.RunTelemetryHistory)
		{
			if (!string.Equals(run.Outcome, outcome, StringComparison.OrdinalIgnoreCase))
				break;
			count++;
		}

		return count;
	}

	private static float CalculateVarietyBonus(SaveData data, RunResult currentRun)
	{
		if (currentRun.EquippedSpells == null || currentRun.EquippedSpells.Count == 0)
			return 0f;

		var recentSpellIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (RunTelemetryRecord run in data.RunTelemetryHistory.Take(3))
		{
			foreach (string id in run.SpellPickCounts.Keys)
			{
				if (!string.IsNullOrWhiteSpace(id))
					recentSpellIds.Add(id);
			}
		}

		if (recentSpellIds.Count == 0)
			return 0f;

		int freshSpells = currentRun.EquippedSpells.Count(spell => !recentSpellIds.Contains(spell.Id));
		if (freshSpells <= 0)
			return 0f;

		return Math.Min(0.10f, freshSpells * 0.025f);
	}

	private static float CalculateImprovementBonus(SaveData data, RunResult currentRun)
	{
		RunTelemetryRecord? previous = data.RunTelemetryHistory.FirstOrDefault();
		if (previous == null)
			return 0f;

		float bonus = 0f;
		if (currentRun.TimeSurvived > previous.TimeSurvived)
			bonus += Math.Min(0.06f, (currentRun.TimeSurvived - previous.TimeSurvived) / 2100f);

		if (currentRun.EnemiesKilled > previous.EnemiesKilled)
			bonus += Math.Min(0.04f, (currentRun.EnemiesKilled - previous.EnemiesKilled) / 900f);

		return Math.Min(0.08f, bonus);
	}
}