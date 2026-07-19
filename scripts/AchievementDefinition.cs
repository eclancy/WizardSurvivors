using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

public sealed class AchievementDefinition
{
	public string Id { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string RewardText { get; init; } = string.Empty;
	public string SpellUnlockId { get; init; } = string.Empty;
	public Func<global::RunResult, bool> IsComplete { get; init; } = _ => false;
}

public static class AchievementDefinitions
{
	public static readonly IReadOnlyList<AchievementDefinition> All = new List<AchievementDefinition>
	{
		new() { Id = "first_blood", DisplayName = "First Blood", Description = "Defeat at least one enemy in a run.", RewardText = "Unlocks Fireball", SpellUnlockId = "fireball", IsComplete = r => r.EnemiesKilled > 0 },
		new() { Id = "survivor", DisplayName = "Survivor", Description = "Survive for 10 minutes.", RewardText = "Unlocks Frost Shard", SpellUnlockId = "frost_shard", IsComplete = r => r.TimeSurvived >= 600f },
		new() { Id = "veteran", DisplayName = "Veteran", Description = "Survive for 20 minutes.", RewardText = "Unlocks Meteor Swarm", SpellUnlockId = "meteor_swarm", IsComplete = r => r.TimeSurvived >= 1200f },
		new() { Id = "max_level", DisplayName = "Archmage Training", Description = "Reach level 20 in a run.", RewardText = "Unlocks Solar Flare", SpellUnlockId = "solar_flare", IsComplete = r => r.FinalPlayerLevel >= 20 },
		new() { Id = "elementalist", DisplayName = "Elementalist", Description = "Reach 4 instances of any element in a run.", RewardText = "Unlocks Chain Lightning", SpellUnlockId = "chain_lightning", IsComplete = r => HasAnyElementAtLeast(r, 4) },
		new() { Id = "hoarder", DisplayName = "Full Grimoire", Description = "Fill all 6 spell slots in a run.", RewardText = "Unlocks Void Lance", SpellUnlockId = "void_lance", IsComplete = r => r.EquippedSpells != null && r.EquippedSpells.Count >= Player.MaxSpellSlots },
		new() { Id = "untouchable", DisplayName = "Untouchable", Description = "Survive 5 minutes without taking damage.", RewardText = "Unlocks Blur", SpellUnlockId = "blur", IsComplete = r => r.TimeSurvived >= 300f && !r.TookDamageBeforeFiveMinutes },
		new() { Id = "fire_adept", DisplayName = "Fire Adept", Description = "End a run with 4 Fire instances.", RewardText = "Unlocks Scorching Ray", SpellUnlockId = "scorching_ray", IsComplete = r => HasElementAtLeast(r, "Fire", 4) },
		new() { Id = "ice_adept", DisplayName = "Ice Adept", Description = "End a run with 4 Ice instances.", RewardText = "Unlocks Cone of Cold", SpellUnlockId = "cone_of_cold", IsComplete = r => HasElementAtLeast(r, "Ice", 4) },
		new() { Id = "poison_adept", DisplayName = "Poison Adept", Description = "End a run with 4 Poison instances.", RewardText = "Unlocks Toxic Spore Burst", SpellUnlockId = "toxic_spore_burst", IsComplete = r => HasElementAtLeast(r, "Poison", 4) },
		new() { Id = "earth_adept", DisplayName = "Earth Adept", Description = "End a run with 4 Earth instances.", RewardText = "Unlocks Obsidian Spike", SpellUnlockId = "obsidian_spike", IsComplete = r => HasElementAtLeast(r, "Earth", 4) },
		new() { Id = "wind_adept", DisplayName = "Wind Adept", Description = "End a run with 4 Wind instances.", RewardText = "Unlocks Gale Blade", SpellUnlockId = "gale_blade", IsComplete = r => HasElementAtLeast(r, "Wind", 4) },
		new() { Id = "forest_cleared", DisplayName = "Forest Cleared", Description = "Defeat the forest boss.", RewardText = "Unlocks Thorn Vine", SpellUnlockId = "thorn_vine", IsComplete = r => r.Outcome == global::RunOutcome.Victory && ContainsBossId(r, "forest", "enchanted") },
		new() { Id = "castle_conqueror", DisplayName = "Castle Conqueror", Description = "Defeat the castle boss.", RewardText = "Unlocks Shadow Bolt", SpellUnlockId = "shadow_bolt", IsComplete = r => r.Outcome == global::RunOutcome.Victory && ContainsBossId(r, "castle", "cursed") },
		new() { Id = "ruins_delver", DisplayName = "Ruins Delver", Description = "Defeat the ruins boss.", RewardText = "Unlocks Black Tentacles", SpellUnlockId = "black_tentacles", IsComplete = r => r.Outcome == global::RunOutcome.Victory && ContainsBossId(r, "ruins", "mystic") }
	};

	public static AchievementDefinition GetById(string id) => All.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

	private static bool HasAnyElementAtLeast(global::RunResult result, int count)
	{
		return result.ElementCounts != null && result.ElementCounts.Values.Any(value => value >= count);
	}

	private static bool HasElementAtLeast(global::RunResult result, string element, int count)
	{
		return result.ElementCounts != null
			&& result.ElementCounts.TryGetValue(element, out int value)
			&& value >= count;
	}

	private static bool ContainsBossId(global::RunResult result, params string[] terms)
	{
		if (string.IsNullOrWhiteSpace(result.BossId))
			return false;

		string normalized = result.BossId.Trim().ToLowerInvariant();
		return terms.Any(normalized.Contains);
	}
}