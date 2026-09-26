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

	// Was a 35-entry hardcoded set containing literally every spell in the game, which is what made
	// the whole unlock economy inert. UnlockCatalog is the authority now; this stays only as the
	// grandfathering list for saves written before the campaign existed (see SaveData.Migrate).
	public static readonly IReadOnlyList<string> LegacyDefaultUnlockedSpellIds = new[]
	{
		"magic_missile", "arcane_explosion", "spiritual_weapon", "fireball", "frost_shard",
		"shadow_bolt", "thorn_vine", "gale_blade", "solar_flare", "molten_shard", "chain_lightning",
		"toxic_spore_burst", "obsidian_spike", "cyclone_slash", "void_lance", "glacial_spike",
		"black_tentacles", "cone_of_cold", "scorching_ray", "meteor_swarm", "aegis_ward",
		"thornmail_barrier", "frozen_bulwark", "stormguard_aura", "venom_cloak", "guardian_vines",
		"tidal_barrier", "stone_bulwark", "blur", "fortunes_favor", "haste",
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

		return UnlockCatalog.IsStarter(spellId, UnlockKind.Spell)
			|| (data != null && data.UnlockedSpellIds.Any(id => id.Equals(spellId, StringComparison.OrdinalIgnoreCase)));
	}

	public static bool UnlockSpell(SaveData data, string spellId)
	{
		if (data == null || string.IsNullOrWhiteSpace(spellId) || IsSpellUnlockedForLevelUp(data, spellId))
			return false;

		data.UnlockedSpellIds.Add(spellId);
		return true;
	}

	public static bool IsBoonUnlocked(SaveData data, string boonId)
	{
		if (string.IsNullOrWhiteSpace(boonId))
			return false;

		return UnlockCatalog.IsStarter(boonId, UnlockKind.Boon)
			|| (data != null && data.UnlockedBoonIds.Any(id => id.Equals(boonId, StringComparison.OrdinalIgnoreCase)));
	}

	public static bool UnlockBoon(SaveData data, string boonId)
	{
		if (data == null || string.IsNullOrWhiteSpace(boonId) || IsBoonUnlocked(data, boonId))
			return false;

		data.UnlockedBoonIds.Add(boonId);
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

	// --- Spellbook curation ---------------------------------------------------------------------
	// Setting a spell aside stops it being offered at level-up. It is not a lock: the player owns
	// it, chose this, and can undo it at any time from the spellbook.
	//
	// Two things bound it, and they matter for different reasons. The *floor* is a design bound:
	// a book narrowed to three pages would make every level-up identical, so the pool may never
	// drop below MinimumOfferablePool. The *slots* are an economic bound: each one is bought with
	// Arcane Energy, so curation competes with the stat upgrades rather than being free.
	//
	// Capacity grows with the book, which is the point - on a fresh save with eight spells there is
	// nothing worth pruning, and the feature correctly offers nothing at all.

	/// <summary>The pool may never be narrowed below this many offerable spells.</summary>
	public const int MinimumOfferablePool = 12;

	public const string CurationUpgradeId = "curation";

	public static int GetUnlockedSpellCount(SaveData data) =>
		UnlockCatalog.AllSpellIds.Count(id => IsSpellUnlockedForLevelUp(data, id));

	/// <summary>How many curation slots the player could ever buy, given the book they have.</summary>
	public static int GetCurationSlotCapacity(SaveData data) =>
		Math.Max(0, GetUnlockedSpellCount(data) - MinimumOfferablePool);

	/// <summary>How many they have actually bought.</summary>
	public static int GetCurationSlotsOwned(SaveData data) =>
		Math.Min(GetUpgradeLevel(data, CurationUpgradeId), GetCurationSlotCapacity(data));

	public static bool IsSpellRemovedFromPool(SaveData data, string spellId)
	{
		if (data == null || string.IsNullOrWhiteSpace(spellId))
			return false;

		return data.RemovedSpellIds.Any(id => id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
	}

	public static int GetRemovedSpellCount(SaveData data) => data?.RemovedSpellIds.Count ?? 0;

	/// <summary>
	/// Whether this spell could be set aside right now, and if not, why. The reason is returned
	/// rather than logged so the spellbook button can say it out loud instead of just refusing.
	/// </summary>
	public static bool CanRemoveSpellFromPool(SaveData data, string spellId, out string reason)
	{
		reason = string.Empty;
		if (data == null || string.IsNullOrWhiteSpace(spellId))
		{
			reason = "No save loaded.";
			return false;
		}

		if (!IsSpellUnlockedForLevelUp(data, spellId))
		{
			reason = "You have not found this page yet.";
			return false;
		}

		if (GetRemovedSpellCount(data) >= GetCurationSlotsOwned(data))
		{
			int capacity = GetCurationSlotCapacity(data);
			reason = capacity > 0
				? "Buy another Redaction in the Arcane Codex."
				: $"Your book must hold {MinimumOfferablePool} offerable pages before any can be set aside.";
			return false;
		}

		if (GetUnlockedSpellCount(data) - GetRemovedSpellCount(data) - 1 < MinimumOfferablePool)
		{
			reason = $"At least {MinimumOfferablePool} pages must stay in the book.";
			return false;
		}

		return true;
	}

	/// <summary>Sets a spell aside or puts it back. Returns whether anything changed.</summary>
	public static bool SetSpellRemovedFromPool(SaveData data, string spellId, bool removed)
	{
		if (data == null || string.IsNullOrWhiteSpace(spellId))
			return false;

		bool currently = IsSpellRemovedFromPool(data, spellId);
		if (currently == removed)
			return false;

		if (removed)
		{
			if (!CanRemoveSpellFromPool(data, spellId, out _))
				return false;

			data.RemovedSpellIds.Add(spellId);
			return true;
		}

		// Putting a page back is always allowed - it only ever widens the pool.
		data.RemovedSpellIds.RemoveAll(id => id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		return true;
	}

	// --- Characters -----------------------------------------------------------------------------
	// SaveData.UnlockedCharacterIds has existed since the save format did, and until now nothing
	// anywhere wrote to it: the list had exactly one reader, every CharacterData shipped with
	// IsUnlocked = true, and so the whole rail was dormant. These two are the missing write path.

	public static bool IsCharacterUnlocked(SaveData data, string characterId)
	{
		if (string.IsNullOrWhiteSpace(characterId))
			return false;

		return UnlockCatalog.IsStarter(characterId, UnlockKind.Character)
			|| (data != null && data.UnlockedCharacterIds.Any(id => id.Equals(characterId, StringComparison.OrdinalIgnoreCase)));
	}

	public static bool UnlockCharacter(SaveData data, string characterId)
	{
		if (data == null || string.IsNullOrWhiteSpace(characterId) || IsCharacterUnlocked(data, characterId))
			return false;

		data.UnlockedCharacterIds.Add(characterId);
		return true;
	}

	// --- Achievements ---------------------------------------------------------------------------
	// Membership was open-coded as the same LINQ at three call sites, each free to get the string
	// comparison wrong independently.

	public static bool IsAchievementUnlocked(SaveData data, string achievementId)
	{
		if (data == null || string.IsNullOrWhiteSpace(achievementId))
			return false;

		return data.UnlockedAchievementIds.Any(id => id.Equals(achievementId, StringComparison.OrdinalIgnoreCase));
	}

	// --- The campaign gate ----------------------------------------------------------------------
	// The final chapter opens only when every spell has been recovered and every wizard freed.

	public static bool AllSpellsUnlocked(SaveData data) =>
		UnlockCatalog.AllSpellIds.All(id => IsSpellUnlockedForLevelUp(data, id));

	public static bool AllWizardsUnlocked(SaveData data) =>
		UnlockCatalog.AllWizardIds.All(id => IsCharacterUnlocked(data, id));

	public static bool IsCampaignComplete(SaveData data) =>
		AllSpellsUnlocked(data) && AllWizardsUnlocked(data);

	/// <summary>Spells still missing, for the "what is left" line on the final chapter's card.</summary>
	public static int RemainingSpellCount(SaveData data) =>
		UnlockCatalog.AllSpellIds.Count(id => !IsSpellUnlockedForLevelUp(data, id));

	public static int RemainingWizardCount(SaveData data) =>
		UnlockCatalog.AllWizardIds.Count(id => !IsCharacterUnlocked(data, id));

	/// <summary>
	/// Whether a chapter can be entered, resolving its <see cref="StageGate"/>. The campaign-gated
	/// final chapter is the reason this is not just a lookup in the unlocked-stages list.
	/// </summary>
	public static bool IsStageAvailable(SaveData data, StageDefinition stage)
	{
		if (stage == null || !stage.IsPlayable)
			return false;

		return stage.Gate switch
		{
			StageGate.Open => true,
			StageGate.CampaignComplete => IsCampaignComplete(data),
			_ => IsStageUnlocked(data, stage.Id),
		};
	}

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