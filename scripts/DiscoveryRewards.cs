using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// Pays out a side event's site: completes the event's Road deed (AchievementDefinitions.EventDeedId),
/// which grants everything <see cref="UnlockCatalog"/> lists at that site (<see cref="UnlockCatalog.ForDiscoverySite"/>).
/// </summary>
/// <remarks>
/// Granted and saved the moment the side event is won, not at the end of the run. The fiction is
/// that you carry a person out of a cell, and a person you carried out does not go back in because
/// you died three minutes later. It also means a crash or a quit after the event costs nothing.
///
/// Since schema 19 (.ai/achievements-and-secrets.md 2.2) a site's unlock is the reward of a deed, not a
/// Discovery of its own, so the achievements list shows every event and what it pays. This is still the
/// one door the events go through: it records the win (LifetimeStats.EventsWon, what the deed measures)
/// and completes the deed through AchievementManager, the same path a secret takes.
/// </remarks>
public static class DiscoveryRewards
{
	public readonly struct Grant
	{
		public Grant(string id, string displayName, bool isWizard)
		{
			Id = id;
			DisplayName = displayName;
			IsWizard = isWizard;
		}

		/// <summary>The wizard or spell id, so the results screen can draw its portrait or icon.</summary>
		public string Id { get; }
		public string DisplayName { get; }
		public bool IsWizard { get; }
	}

	/// <summary>True when everything the site gives is already owned, so the event pays a chest instead.</summary>
	public static bool IsSiteExhausted(SaveData data, string siteId)
	{
		foreach (UnlockDefinition definition in UnlockCatalog.ForDiscoverySite(siteId))
		{
			bool owned = definition.Kind switch
			{
				UnlockKind.Character => GlobalStatsManager.IsCharacterUnlocked(data, definition.Id),
				UnlockKind.Boon => GlobalStatsManager.IsBoonUnlocked(data, definition.Id),
				_ => GlobalStatsManager.IsSpellUnlockedForLevelUp(data, definition.Id),
			};
			if (!owned)
				return false;
		}
		return true;
	}

	/// <summary>Records the win, grants and saves everything the site gives. Returns what was new.</summary>
	public static List<Grant> GrantSite(SaveManager saveManager, string siteId)
	{
		if (saveManager?.Data == null)
			return new List<Grant>();

		List<Grant> granted = GrantSite(saveManager.Data, siteId);
		// Always: the win itself is new to the save (EventsWon, the deed), even when what it pays was owned.
		saveManager.SaveGame();
		return granted;
	}

	/// <summary>
	/// <see cref="GrantSite(SaveManager, string)"/> on a save in memory, without writing it: what the
	/// validators run to prove a new save can earn what a gate counts.
	/// </summary>
	public static List<Grant> GrantSite(SaveData data, string siteId)
	{
		var granted = new List<Grant>();
		if (data == null || string.IsNullOrWhiteSpace(siteId))
			return granted;

		foreach (RunUnlockEntry entry in AchievementManager.Complete(data, AchievementDefinitions.EventDeedId(siteId)))
		{
			switch (entry.Kind)
			{
				case RunUnlockKind.Wizard:
					granted.Add(new Grant(entry.Id, CharacterName(entry.Id), true));
					break;
				// The early chapters' sites pay relics (.ai/campaign.md). Reported as a found thing like
				// a spell: the line the event shows says what was carried out, not what kind it is.
				case RunUnlockKind.Boon:
					granted.Add(new Grant(entry.Id, BoonCatalog.GetById(entry.Id)?.Name ?? entry.Id, false));
					break;
				case RunUnlockKind.Spell:
					granted.Add(new Grant(entry.Id, SpellName(entry.Id), false));
					break;
			}
		}
		return granted;
	}

	private static string CharacterName(string id) =>
		CharacterRoster.GetAll().FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Name ?? id;

	private static string SpellName(string id) =>
		ContentValidator.LoadAllSpells().FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Name ?? id;
}
