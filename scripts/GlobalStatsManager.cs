using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

public static class GlobalStatsManager
{
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
}