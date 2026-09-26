using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>What a finished run actually earned the player, in the order it was earned.</summary>
/// <remarks>
/// This used to be a discarded <c>bool</c>. Achievements fired, spells were granted, and the game
/// said nothing at all - no toast, no line on the game over screen, nothing. An unlock the player
/// is never told about is very close to no unlock.
/// </remarks>
public sealed class RunUnlockSummary
{
	public List<string> AchievementNames { get; } = new();
	public List<string> SpellNames { get; } = new();
	public List<string> WizardNames { get; } = new();
	public int CurrencyAwarded { get; set; }

	public bool Any => AchievementNames.Count > 0 || SpellNames.Count > 0
		|| WizardNames.Count > 0 || CurrencyAwarded > 0;
}

public static class AchievementManager
{
	/// <summary>
	/// Evaluates every achievement against a finished run, granting what it earns.
	/// </summary>
	/// <returns>What was newly unlocked, for the game over screen to report.</returns>
	public static RunUnlockSummary ApplyRunAchievements(SaveData data, global::RunResult result)
	{
		var summary = new RunUnlockSummary();
		if (data == null || result == null)
			return summary;

		// Fold the run into the lifetime totals BEFORE evaluating anything. Almost every condition
		// is written against lifetime stats rather than the run - that is what lets the main menu
		// show real progress instead of the word "In Progress" - so a run evaluated first would
		// always be judged against the state before it happened, and every achievement would fire
		// exactly one run late.
		data.Lifetime.Absorb(result);

		AchievementContext context = AchievementContext.ForRun(data, result);

		foreach (AchievementDefinition definition in AchievementDefinitions.All)
		{
			if (!definition.IsComplete(context))
				continue;

			if (!UnlockAchievement(data, definition.Id))
				continue;

			// Only report on the run that actually earned it. Re-completing an achievement is
			// common - most of them are things a good run does every time - and re-announcing it
			// would bury the one line that matters.
			summary.AchievementNames.Add(definition.DisplayName);

			if (!string.IsNullOrWhiteSpace(definition.SpellUnlockId)
				&& GlobalStatsManager.UnlockSpell(data, definition.SpellUnlockId))
			{
				summary.SpellNames.Add(SpellDisplayName(definition.SpellUnlockId));
			}

			// Boons are reported in the same list as spells. To the player they are the same kind
			// of thing - something new they can be offered - and splitting the end-of-run summary
			// by an internal distinction would say nothing useful.
			if (!string.IsNullOrWhiteSpace(definition.BoonUnlockId)
				&& GlobalStatsManager.UnlockBoon(data, definition.BoonUnlockId))
			{
				BoonDefinition boon = BoonCatalog.GetById(definition.BoonUnlockId);
				summary.SpellNames.Add(boon?.Name ?? definition.BoonUnlockId);
			}

			if (!string.IsNullOrWhiteSpace(definition.CharacterUnlockId)
				&& GlobalStatsManager.UnlockCharacter(data, definition.CharacterUnlockId))
			{
				summary.WizardNames.Add(CharacterDisplayName(definition.CharacterUnlockId));
			}

			if (definition.CurrencyReward > 0)
			{
				data.TotalCurrency += definition.CurrencyReward;
				summary.CurrencyAwarded += definition.CurrencyReward;
			}
		}

		return summary;
	}

	private static bool UnlockAchievement(SaveData data, string achievementId)
	{
		if (GlobalStatsManager.IsAchievementUnlocked(data, achievementId))
			return false;

		data.UnlockedAchievementIds.Add(achievementId);
		return true;
	}

	// Reported by name rather than id: "Unlocked Meteor Swarm" is a reward, "unlocked meteor_swarm"
	// is a log line. Falls back to the id so a missing resource still says something true.
	private static string SpellDisplayName(string spellId)
	{
		foreach (string path in ContentValidator.SpellResourcePaths)
		{
			var spell = Godot.ResourceLoader.Load<SpellData>(path);
			if (spell != null && spell.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase))
				return spell.Name;
		}

		// Passive spells have no .tres - they are built in code by Player.CreateDefensiveSpellData -
		// so the loop above cannot name them and "Unlocked frozen_bulwark" reaches the player.
		// Their ids are snake_case of their display names, so title-casing recovers the right name
		// for every one of them. The single exception is "fortunes_favor", which loses its
		// apostrophe; it is not an achievement reward, and a wrong apostrophe beats a raw id.
		return TitleCaseId(spellId);
	}

	private static string TitleCaseId(string id)
	{
		string[] words = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0)
			return id;

		return string.Join(" ", words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
	}

	private static string CharacterDisplayName(string characterId)
	{
		CharacterData match = CharacterRoster.GetAll()
			.FirstOrDefault(c => c != null && c.Id.Equals(characterId, StringComparison.OrdinalIgnoreCase));
		return match != null ? match.Name : characterId;
	}
}
