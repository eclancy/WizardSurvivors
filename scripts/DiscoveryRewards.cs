using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// Pays out a discovery site: everything <see cref="UnlockCatalog"/> lists under
/// <see cref="UnlockSource.Discovery"/> with that site as its source.
/// </summary>
/// <remarks>
/// Granted and saved the moment the side event is won, not at the end of the run. The fiction is
/// that you carry a person out of a cell, and a person you carried out does not go back in because
/// you died three minutes later. It also means a crash or a quit after the event costs nothing.
/// </remarks>
public static class DiscoveryRewards
{
	public readonly struct Grant
	{
		public Grant(string displayName, bool isWizard)
		{
			DisplayName = displayName;
			IsWizard = isWizard;
		}

		public string DisplayName { get; }
		public bool IsWizard { get; }
	}

	/// <summary>True when everything the site gives is already owned, so the event pays a chest instead.</summary>
	public static bool IsSiteExhausted(SaveData data, string siteId)
	{
		foreach (UnlockDefinition definition in UnlockCatalog.ForDiscoverySite(siteId))
		{
			bool owned = definition.Kind == UnlockKind.Character
				? GlobalStatsManager.IsCharacterUnlocked(data, definition.Id)
				: GlobalStatsManager.IsSpellUnlockedForLevelUp(data, definition.Id);
			if (!owned)
				return false;
		}
		return true;
	}

	/// <summary>Unlocks and saves everything the site gives. Returns what was new.</summary>
	public static List<Grant> GrantSite(SaveManager saveManager, string siteId)
	{
		var granted = new List<Grant>();
		if (saveManager == null)
			return granted;

		foreach (UnlockDefinition definition in UnlockCatalog.ForDiscoverySite(siteId))
		{
			if (definition.Kind == UnlockKind.Character)
			{
				if (GlobalStatsManager.UnlockCharacter(saveManager.Data, definition.Id))
					granted.Add(new Grant(CharacterName(definition.Id), true));
			}
			else if (definition.Kind == UnlockKind.Spell)
			{
				if (GlobalStatsManager.UnlockSpell(saveManager.Data, definition.Id))
					granted.Add(new Grant(SpellName(definition.Id), false));
			}
		}

		if (granted.Count > 0)
			saveManager.SaveGame();
		return granted;
	}

	private static string CharacterName(string id) =>
		CharacterRoster.GetAll().FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Name ?? id;

	private static string SpellName(string id) =>
		ContentValidator.LoadAllSpells().FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Name ?? id;
}
