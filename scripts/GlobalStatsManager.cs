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
	/// rather than logged so the Spell Index's Set aside button can say it out loud instead of just refusing.
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
			reason = "You have not found this spell yet.";
			return false;
		}

		if (GetRemovedSpellCount(data) >= GetCurationSlotsOwned(data))
		{
			int capacity = GetCurationSlotCapacity(data);
			reason = capacity > 0
				? "Buy another Redaction in Arcane Upgrades."
				: $"Find {MinimumOfferablePool} spells that can be offered before any can be set aside.";
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

	/// <summary>
	/// Whether a wizard's card may show who he is - his silhouette and how he is found - rather than
	/// a sealed blank. Always true except for a locked secret (<see cref="UnlockDefinition.IsSecret"/>)
	/// whose reveal chapter has not opened.
	/// </summary>
	/// <remarks>
	/// Derived, never stored: the save already says which chapters are open, so there is no
	/// "revealed" flag to migrate or to fall out of step with it. It also means the reveal follows
	/// the stage list exactly - a chapter appears there, and the wizard it holds appears here, on the
	/// same unlock.
	/// </remarks>
	public static bool IsCharacterRevealed(SaveData data, string characterId)
	{
		UnlockDefinition definition = UnlockCatalog.GetCharacter(characterId);
		if (definition == null || !definition.IsSecret || IsCharacterUnlocked(data, characterId))
			return true;

		return IsStageAvailable(data, StageCatalog.GetById(definition.RevealStageId));
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

	// --- The gate chapters ----------------------------------------------------------------------
	// A gate chapter (StageGate.PreviousBossAndWizards: the Emberdeep, and the castle's Throne when it
	// is built) opens when its previous chapter's boss is beaten and every wizard whose rescue comes
	// before it is free (.ai/achievements-and-secrets.md 2.5, .ai/endgame-castle.md 1.6). It used to
	// be one global gate, "every spell recovered and every wizard freed", and a new save could never
	// meet it: Fireball was paid only behind it. Spells are the collection now (The Grimoire Restored).

	public static bool AllSpellsUnlocked(SaveData data) =>
		UnlockCatalog.AllSpellIds.All(id => IsSpellUnlockedForLevelUp(data, id));

	/// <summary>Spells still missing, for the Grimoire's "what is left".</summary>
	public static int RemainingSpellCount(SaveData data) =>
		UnlockCatalog.AllSpellIds.Count(id => !IsSpellUnlockedForLevelUp(data, id));

	/// <summary>
	/// The wizards a gate chapter counts: every wizard (<see cref="UnlockCatalog.AllWizardIds"/>, which
	/// leaves out Laelaps) whose rescue can happen before it. Written per chapter, so a wizard freed in
	/// a chapter that moves behind a gate - Tituba, when the Cursed Dungeon goes into the castle - drops
	/// out of that gate's count by himself and into the next one's, and a gate can never wait on a
	/// person held behind it (the Fireball bug, for a wizard).
	/// </summary>
	public static IEnumerable<string> GateWizardIds(StageDefinition gate) =>
		UnlockCatalog.AllWizardIds.Where(id => CountsTowardGate(id, gate));

	/// <summary>
	/// Whether a gate counts this wizard: he is a wizard (not outside the campaign gate) and his rescue
	/// chapter - the chapter his source deed belongs to - comes before the gate. A starter, or one freed by
	/// a Feat that names no chapter (John Dee's chests), can be had before any gate, so every gate counts
	/// him. A chapter outside the campaign counts only if it is open from the first run.
	/// </summary>
	public static bool CountsTowardGate(string wizardId, StageDefinition gate)
	{
		UnlockDefinition wizard = UnlockCatalog.GetCharacter(wizardId);
		if (wizard == null || wizard.OutsideCampaignGate || gate == null)
			return false;
		string chapter = RescueChapterOf(wizard);
		if (string.IsNullOrWhiteSpace(chapter))
			return true;
		int position = StageCatalog.CampaignPositionOf(chapter);
		if (position < 0)
			return StageCatalog.GetById(chapter)?.Gate == StageGate.Open;
		return position < StageCatalog.CampaignPositionOf(gate.Id);
	}

	/// <summary>The chapter a wizard is freed in: his source deed's StageId. Empty for a starter or a Feat.</summary>
	public static string RescueChapterOf(UnlockDefinition wizard) =>
		UnlockCatalog.SourceDeedOf(wizard)?.StageId ?? string.Empty;

	/// <summary>The wizards a gate counts that this save has not freed yet, for the world map's card.</summary>
	public static int RemainingGateWizardCount(SaveData data, StageDefinition gate) =>
		GateWizardIds(gate).Count(id => !IsCharacterUnlocked(data, id));

	/// <summary>
	/// Whether a gate chapter's gate is met live: its previous chapter's boss beaten (the lifetime record)
	/// and every wizard it counts free. False for a chapter that is not a gate chapter.
	/// </summary>
	public static bool IsGateMet(SaveData data, StageDefinition gate) =>
		gate != null && gate.Gate == StageGate.PreviousBossAndWizards
		&& HasClearedPreviousChapter(data, gate)
		&& GateWizardIds(gate).All(id => IsCharacterUnlocked(data, id));

	/// <summary>
	/// Writes every gate chapter whose gate is met into the save's opened list, so it stays open (rule 1
	/// of <see cref="IsStageEarned"/>) whatever is added to its count later. A wizard added before a
	/// gate would otherwise close it, live, to every save that had it open - which is why schemas 10,
	/// 12, 14, 15 and 16 each had to grant what they added. Only adds. Returns whether it wrote anything.
	/// Run after every run's achievements and in the schema 19 step.
	/// </summary>
	public static bool RecordMetGates(SaveData data)
	{
		if (data == null)
			return false;
		bool wrote = false;
		foreach (StageDefinition gate in StageCatalog.GateChapters)
		{
			if (!IsStageUnlocked(data, gate.Id) && IsGateMet(data, gate))
				wrote |= UnlockStage(data, gate.Id);
		}
		return wrote;
	}

	/// <summary>
	/// Whether a chapter can be entered, resolving its <see cref="StageGate"/>. The gate chapters are
	/// the reason this is not just a lookup in the unlocked-stages list.
	/// </summary>
	public static bool IsStageAvailable(SaveData data, StageDefinition stage)
	{
		if (stage == null || !stage.IsPlayable)
			return false;

		return IsStageEarned(data, stage);
	}

	/// <summary>
	/// Whether the save has earned a chapter, whether or not it is built yet. What
	/// <see cref="IsStageAvailable"/> answers once the chapter is playable.
	/// </summary>
	/// <remarks>
	/// A boss-gated chapter is earned three ways, and any one is enough (.ai/campaign.md, "The gate"):
	///
	///   1. Its id is in the save's opened list - what a boss victory writes, and what every save
	///      before the campaign order already holds.
	///   2. The save has opened a chapter LATER in the campaign. The campaign is one road, so being
	///      past a chapter is having been through it. This is what keeps the order free to change:
	///      the two early chapters, inserted before the Cave, open by themselves for a save that is
	///      already at the Cave or beyond - now, and the day they become playable - with no
	///      migration then, and nobody is ever stopped by a chapter that was added behind them.
	///   3. The boss of the chapter before it in the order has been beaten (LifetimeStats, which
	///      every save has kept since schema 8). Same purpose: a reorder re-derives what a recorded
	///      kill opens instead of trusting what the old order wrote.
	///
	/// Nothing here ever closes a chapter a save could already enter: each rule only adds.
	///
	/// A gate chapter (<see cref="StageGate.PreviousBossAndWizards"/>) keeps rules 1 and 2 - the
	/// opened list, which the schema 19 step writes for every save that had met the old gate, and being
	/// past it - and takes <see cref="IsGateMet"/> in place of rule 3: its previous boss AND the wizards.
	/// </remarks>
	public static bool IsStageEarned(SaveData data, StageDefinition stage)
	{
		if (stage == null)
			return false;

		return stage.Gate switch
		{
			StageGate.Open => true,
			StageGate.PreviousBossAndWizards => IsStageUnlocked(data, stage.Id)
				|| HasPassedInCampaign(data, stage)
				|| IsGateMet(data, stage),
			_ => IsStageUnlocked(data, stage.Id)
				|| HasPassedInCampaign(data, stage)
				|| HasClearedPreviousChapter(data, stage),
		};
	}

	/// <summary>Rule 2 of <see cref="IsStageEarned"/>: a boss-gated chapter later in the campaign is open.</summary>
	public static bool HasPassedInCampaign(SaveData data, StageDefinition stage)
	{
		if (data == null || stage == null)
			return false;

		int position = StageCatalog.CampaignPositionOf(stage.Id);
		if (position < 0)
			return false;

		foreach (string id in data.UnlockedStageIds)
		{
			StageDefinition opened = StageCatalog.GetById(id);
			if (opened != null && opened.Gate == StageGate.PreviousBoss
				&& StageCatalog.CampaignPositionOf(opened.Id) > position)
				return true;
		}
		return false;
	}

	/// <summary>Rule 3 of <see cref="IsStageEarned"/>: the previous playable chapter's boss is beaten.</summary>
	public static bool HasClearedPreviousChapter(SaveData data, StageDefinition stage)
	{
		StageDefinition previous = StageCatalog.PreviousChapter(stage);
		BossDefinition boss = previous != null ? BossCatalog.ForStageIndex(previous.Index) : null;
		return boss != null && data?.Lifetime != null && data.Lifetime.HasDefeatedBoss(boss.Id);
	}

	/// <param name="parts">
	/// Optional. Each bonus that applied, by the name a player is shown (the results screen's
	/// Arcane Energy breakdown) and its size as a fraction. The multiplier is the same with or
	/// without it.
	/// </param>
	public static float GetDynamicArcaneRewardMultiplier(SaveData data, RunResult currentRun, out string breakdown,
		List<(string Name, float Bonus)> parts = null)
	{
		float multiplier = 1.0f;
		var notes = new List<string>();
		if (data == null || currentRun == null)
		{
			breakdown = "No dynamic modifiers";
			return multiplier;
		}

		// There is no losing-streak ("pity") bonus any more: Eric, 2026-10-30, "remove pity streak". It
		// paid up to +24% after consecutive defeats and showed as "Pity streak N" on the main menu.

		int victoryStreak = GetConsecutiveOutcomeCount(data, "Victory");
		if (victoryStreak >= 2)
		{
			float momentumBonus = Math.Min(0.14f, (victoryStreak - 1) * 0.025f);
			multiplier += momentumBonus;
			notes.Add($"Momentum +{Mathf.RoundToInt(momentumBonus * 100f)}%");
			parts?.Add(("Winning streak", momentumBonus));
		}

		float varietyBonus = CalculateVarietyBonus(data, currentRun);
		if (varietyBonus > 0f)
		{
			multiplier += varietyBonus;
			notes.Add($"Variety +{Mathf.RoundToInt(varietyBonus * 100f)}%");
			parts?.Add(("Spells new to you lately", varietyBonus));
		}

		float improvementBonus = CalculateImprovementBonus(data, currentRun);
		if (improvementBonus > 0f)
		{
			multiplier += improvementBonus;
			notes.Add($"Improvement +{Mathf.RoundToInt(improvementBonus * 100f)}%");
			parts?.Add(("Better than your last run", improvementBonus));
		}

		multiplier = Mathf.Clamp(multiplier, DynamicRewardMinMultiplier, DynamicRewardMaxMultiplier);
		breakdown = notes.Count == 0 ? "No dynamic modifiers" : string.Join(", ", notes);
		return multiplier;
	}

	public static float GetNextRunArcanePreviewMultiplier(SaveData data)
	{
		if (data == null)
			return 1.0f;

		int victoryStreak = GetConsecutiveOutcomeCount(data, "Victory");
		float multiplier = 1.0f;
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