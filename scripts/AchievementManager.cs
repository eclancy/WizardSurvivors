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

	/// <summary>
	/// Everything the run earned as one card each, with ids so the results screen can draw the
	/// spell's icon, the wizard's portrait, the chapter's gate. The name lists above stay as they
	/// were; these are the same unlocks with enough on them to be shown rather than listed.
	/// </summary>
	public List<RunUnlockEntry> Entries { get; } = new();

	public bool Any => AchievementNames.Count > 0 || SpellNames.Count > 0
		|| WizardNames.Count > 0 || CurrencyAwarded > 0 || Entries.Count > 0;
}

/// <summary>What kind of thing a run unlocked - the line above its name on the results screen.</summary>
public enum RunUnlockKind
{
	Wizard,
	Spell,
	Boon,
	Chapter,
	Ascension,
	Apotheosis,
	/// <summary>An Unlocks entry finished whose reward is not a spell, boon or wizard - Arcane Energy.</summary>
	Deed,
}

/// <summary>One thing a run earned, as the results screen shows it.</summary>
public sealed class RunUnlockEntry
{
	public RunUnlockKind Kind { get; init; }
	/// <summary>The spell, boon, wizard, stage or deed id; "spell:branch" for an ascension path.</summary>
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	/// <summary>How it was earned, in a few words: "Unlock completed: Slayer", "Elderbark defeated".</summary>
	public string Source { get; init; } = string.Empty;
	/// <summary>The spell an ascension path belongs to, for its icon when the path has none.</summary>
	public string SpellId { get; init; } = string.Empty;
	/// <summary>A picture to use instead of the kind's own (an ascension branch's icon).</summary>
	public string IconPath { get; init; } = string.Empty;
	/// <summary>Arcane Energy a deed paid.</summary>
	public int Currency { get; init; }
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

			// The results screen shows the THING earned, with the deed as how it was earned - one
			// card for "Meteor Swarm, from Slayer" rather than a card for Slayer and another for
			// Meteor Swarm saying the same event twice. A deed that granted no thing (its reward is
			// Arcane Energy, or what it gives was already owned) is a card of its own.
			string earnedBy = $"Unlock completed: {definition.DisplayName}";
			List<RunUnlockEntry> granted = GrantUnlocks(data, definition, earnedBy);
			PayCurrency(data, definition, summary);

			// A deed completed in the run (a side event, a secret) was announced there, by its own beat,
			// and recorded there too (CompleteNow - SecretDirector.Found calls it since piece A4), so it only
			// arrives here when its in-run path did not record it - a secret found on an older build, say.
			// It is recorded now and its unlock granted; it gets a card only for a thing it granted.
			if (definition.CompletesInRun && granted.Count == 0 && definition.CurrencyReward <= 0)
				continue;

			// Only report on the run that actually earned it. Re-completing an achievement is
			// common - most of them are things a good run does every time - and re-announcing it
			// would bury the one line that matters.
			summary.AchievementNames.Add(definition.DisplayName);
			AddToSummary(summary, granted);

			if (granted.Count == 0)
			{
				summary.Entries.Add(new RunUnlockEntry
				{
					Kind = RunUnlockKind.Deed,
					Id = definition.Id,
					Name = definition.DisplayName,
					Source = definition.Description,
					Currency = definition.CurrencyReward,
				});
			}
		}

		// A deed done before its reward moved to it (schema 19 moved four) is paid its reward now, and
		// the gate chapters this save has met are written open for good - announced on the run that met
		// them, which a boss victory alone never does for a gate (StageCatalog.ChapterOpenedBy skips it).
		AddToSummary(summary, Reconcile(data));
		foreach (StageDefinition gate in GlobalStatsManager.RecordMetGates(data))
		{
			summary.Entries.Add(new RunUnlockEntry
			{
				Kind = RunUnlockKind.Chapter,
				Id = gate.Id,
				Name = gate.DisplayName,
				Source = "Every wizard on the road is free",
			});
		}

		return summary;
	}

	/// <summary>
	/// Completes a deed the moment it happens - a side event won, a secret found - grants its reward
	/// and saves at once (.ai/achievements-and-secrets.md 2.6; the DiscoveryRewards rule: a wizard carried
	/// out of a cell stays free even if the run is lost a minute later). The reward joins the level-up
	/// pool from the next level-up of the same run, which reads the save live (Player.GetLevelUpOptions).
	/// </summary>
	/// <returns>Every unlock it newly granted, named, for the event's line and the results screen.</returns>
	public static List<RunUnlockEntry> CompleteNow(SaveManager saveManager, string achievementId)
	{
		if (saveManager?.Data == null)
			return new List<RunUnlockEntry>();
		List<RunUnlockEntry> granted = Complete(saveManager.Data, achievementId);
		saveManager.SaveGame();
		return Named(granted);
	}

	/// <summary>
	/// <see cref="CompleteNow"/> without the save to disk and without names: records the deed, writes the
	/// fact a derived deed measures (the event won, the secret found) so the deed and its measure always
	/// agree, pays its Arcane Energy if this is the first time it is recorded, and grants every unlock it
	/// carries that the save does not own. Safe to call twice: the second call changes nothing.
	/// </summary>
	public static List<RunUnlockEntry> Complete(SaveData data, string achievementId)
	{
		var granted = new List<RunUnlockEntry>();
		AchievementDefinition definition = AchievementDefinitions.GetById(achievementId);
		if (data == null || definition == null)
		{
			if (definition == null)
				Godot.GD.PushError($"AchievementManager: no achievement '{achievementId}' to complete.");
			return granted;
		}

		if (!string.IsNullOrWhiteSpace(definition.SiteId))
			data.Lifetime.RecordEventWon(definition.SiteId);
		if (!string.IsNullOrWhiteSpace(definition.SecretId))
			data.RecordSecretFound(definition.SecretId);

		if (UnlockAchievement(data, definition.Id))
			PayCurrency(data, definition, null);
		granted.AddRange(GrantUnlocks(data, definition, $"Found during the run: {definition.DisplayName}"));
		return granted;
	}

	/// <summary>
	/// Grants the CURRENT unlock reward of every deed the save has done, where it is not already owned
	/// (.ai/achievements-and-secrets.md 2.6 and section 3, step 3). Only ever adds, and only unlocks -
	/// never Arcane Energy, which cannot be checked as already paid. So when a reward moves between deeds
	/// it reaches the saves that had done the deed with no schema step: Survivor done pays Hollow Star,
	/// Unwound done pays Fireball, and a reward that moved away stays where it was.
	/// </summary>
	/// <returns>Every unlock it granted, unnamed (the migration has no use for names).</returns>
	public static List<RunUnlockEntry> Reconcile(SaveData data)
	{
		var granted = new List<RunUnlockEntry>();
		if (data == null)
			return granted;
		foreach (string achievementId in data.UnlockedAchievementIds.ToList())
		{
			AchievementDefinition definition = AchievementDefinitions.GetById(achievementId);
			if (definition != null)
				granted.AddRange(GrantUnlocks(data, definition, $"Unlock completed: {definition.DisplayName}"));
		}
		return granted;
	}

	// Each thing a deed carries that the save does not own yet, granted. Unnamed, so the save migration
	// can run it before any resource is loaded; Named() fills the names for a screen.
	private static List<RunUnlockEntry> GrantUnlocks(SaveData data, AchievementDefinition definition, string earnedBy)
	{
		var granted = new List<RunUnlockEntry>();
		if (!string.IsNullOrWhiteSpace(definition.SpellUnlockId)
			&& GlobalStatsManager.UnlockSpell(data, definition.SpellUnlockId))
			granted.Add(new RunUnlockEntry { Kind = RunUnlockKind.Spell, Id = definition.SpellUnlockId, Name = definition.SpellUnlockId, Source = earnedBy });
		if (!string.IsNullOrWhiteSpace(definition.BoonUnlockId)
			&& GlobalStatsManager.UnlockBoon(data, definition.BoonUnlockId))
			granted.Add(new RunUnlockEntry { Kind = RunUnlockKind.Boon, Id = definition.BoonUnlockId, Name = definition.BoonUnlockId, Source = earnedBy });
		if (!string.IsNullOrWhiteSpace(definition.CharacterUnlockId)
			&& GlobalStatsManager.UnlockCharacter(data, definition.CharacterUnlockId))
			granted.Add(new RunUnlockEntry { Kind = RunUnlockKind.Wizard, Id = definition.CharacterUnlockId, Name = definition.CharacterUnlockId, Source = earnedBy });
		return granted;
	}

	private static void PayCurrency(SaveData data, AchievementDefinition definition, RunUnlockSummary summary)
	{
		if (definition.CurrencyReward <= 0)
			return;
		data.TotalCurrency += definition.CurrencyReward;
		if (summary != null)
			summary.CurrencyAwarded += definition.CurrencyReward;
	}

	// Into the results screen's lists. Boons are reported in the same list as spells: to the player they
	// are the same kind of thing - something new they can be offered.
	private static void AddToSummary(RunUnlockSummary summary, List<RunUnlockEntry> granted)
	{
		foreach (RunUnlockEntry entry in Named(granted))
		{
			(entry.Kind == RunUnlockKind.Wizard ? summary.WizardNames : summary.SpellNames).Add(entry.Name);
			summary.Entries.Add(entry);
		}
	}

	/// <summary>The same entries with the names a player reads: "Unlocked Meteor Swarm", not "meteor_swarm".</summary>
	public static List<RunUnlockEntry> Named(List<RunUnlockEntry> entries) =>
		entries.Select(e => new RunUnlockEntry
		{
			Kind = e.Kind,
			Id = e.Id,
			Name = e.Kind switch
			{
				RunUnlockKind.Spell => SpellDisplayName(e.Id),
				RunUnlockKind.Boon => BoonCatalog.GetById(e.Id)?.Name ?? e.Id,
				RunUnlockKind.Wizard => CharacterDisplayName(e.Id),
				_ => e.Name,
			},
			Source = e.Source,
			SpellId = e.SpellId,
			IconPath = e.IconPath,
			Currency = e.Currency,
		}).ToList();

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
