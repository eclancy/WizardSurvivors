using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>Everything an achievement is allowed to ask about.</summary>
/// <remarks>
/// <see cref="Run"/> is null everywhere except the moment a run ends - the main menu has no current
/// run. A condition that can only be measured against <see cref="Run"/> therefore cannot show
/// progress anywhere the player would look at it, which is why most conditions are written against
/// <see cref="Lifetime"/> instead and the run only folds into that at the end.
/// </remarks>
public sealed class AchievementContext
{
	public global::RunResult Run { get; init; }
	public SaveData Save { get; init; }

	public LifetimeStats Lifetime => Save?.Lifetime ?? EmptyLifetime;
	private static readonly LifetimeStats EmptyLifetime = new();

	public static AchievementContext ForSave(SaveData save) => new() { Save = save };
	public static AchievementContext ForRun(SaveData save, global::RunResult run) => new() { Save = save, Run = run };
}

public sealed class AchievementProgress
{
	public float Current { get; init; }
	public float Target { get; init; }
	public string Display { get; init; } = string.Empty;
	public bool IsComplete => Current >= Target;
	public float Fraction => Target <= 0f ? 1f : Math.Clamp(Current / Target, 0f, 1f);
}

public enum AchievementCategory
{
	Progress,
	Mastery,
	Exploration,
	Challenge,
}

public sealed class AchievementDefinition
{
	public string Id { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string RewardText { get; init; } = string.Empty;
	public string SpellUnlockId { get; init; } = string.Empty;

	/// <summary>A boon this achievement grants, if any. Same role as SpellUnlockId.</summary>
	public string BoonUnlockId { get; init; } = string.Empty;
	public string CharacterUnlockId { get; init; } = string.Empty;
	public int CurrencyReward { get; init; }
	public AchievementCategory Category { get; init; } = AchievementCategory.Progress;

	/// <summary>
	/// How far along the player is, in the same unit as <see cref="Target"/>.
	/// </summary>
	/// <remarks>
	/// Completion and progress are derived from this one function rather than being two independent
	/// lambdas. That is the whole point of the shape: an achievement whose "am I done" and "how far
	/// am I" disagree is a bug nobody notices, because each half looks correct on its own.
	/// </remarks>
	public Func<AchievementContext, float> Measure { get; init; } = _ => 0f;

	public float Target { get; init; } = 1f;

	/// <summary>Formats a measured value for the card. Defaults to a plain integer.</summary>
	public Func<float, string> FormatValue { get; init; }

	public bool IsComplete(AchievementContext context) => Measure(context) >= Target;

	public AchievementProgress GetProgress(AchievementContext context)
	{
		float current = Measure(context);
		Func<float, string> format = FormatValue ?? (value => ((int)value).ToString());
		return new AchievementProgress
		{
			Current = current,
			Target = Target,
			// A one-of-one achievement is a yes/no question; showing "0 / 1" for it reads as a
			// counter that is broken rather than as something not yet done.
			Display = Target <= 1f ? string.Empty : $"{format(Math.Min(current, Target))} / {format(Target)}",
		};
	}

	/// <summary>Seconds formatted as m:ss, for time-based targets.</summary>
	public static string FormatTime(float seconds)
	{
		int total = Math.Max(0, (int)seconds);
		return $"{total / 60}:{total % 60:00}";
	}
}

public static class AchievementDefinitions
{
	private const float TenMinutes = 600f;
	private const float FiveMinutes = 300f;

	public static readonly IReadOnlyList<AchievementDefinition> All = new List<AchievementDefinition>
	{
		// --- Progress: the spine of the campaign ---------------------------------------------
		new()
		{
			Id = "first_blood", DisplayName = "First Blood",
			Description = "Defeat 1 enemy.",
			RewardText = "Unlocks Arcane Explosion", SpellUnlockId = "arcane_explosion",
			Category = AchievementCategory.Progress,
			Measure = c => c.Lifetime.TotalKills, Target = 1,
		},
		new()
		{
			Id = "survivor", DisplayName = "Survivor",
			Description = "Survive for 10 minutes in a single run.",
			RewardText = "Unlocks Frost Shard", SpellUnlockId = "frost_shard",
			Category = AchievementCategory.Progress,
			Measure = c => c.Lifetime.BestTimeByStage.Values.DefaultIfEmpty(0f).Max(),
			Target = TenMinutes, FormatValue = AchievementDefinition.FormatTime,
		},
		new()
		{
			Id = "veteran", DisplayName = "Veteran",
			Description = "Clear a chapter without falling.",
			RewardText = "Unlocks Meteor Swarm", SpellUnlockId = "meteor_swarm",
			Category = AchievementCategory.Progress,
			Measure = c => c.Lifetime.TotalVictories, Target = 1,
		},
		new()
		{
			Id = "max_level", DisplayName = "Archmage Training",
			Description = "Reach level 20 in a run.",
			RewardText = "Unlocks Solar Flare", SpellUnlockId = "solar_flare",
			Category = AchievementCategory.Progress,
			Measure = c => c.Lifetime.BestPlayerLevel, Target = 20,
		},
		new()
		{
			Id = "hoarder", DisplayName = "Full Grimoire",
			Description = "Carry six spells at once.",
			RewardText = "Unlocks Void Lance", SpellUnlockId = "void_lance",
			Category = AchievementCategory.Progress,
			Measure = c => c.Lifetime.BestSpellSlotsFilled, Target = Player.MaxSpellSlots,
		},

		// --- Mastery: element thresholds, measured at their peak, not at run end ---------------
		new()
		{
			Id = "elementalist", DisplayName = "Elementalist",
			Description = "Hold 4 instances of any one element.",
			RewardText = "Unlocks Cyclone Slash", SpellUnlockId = "cyclone_slash",
			Category = AchievementCategory.Mastery,
			Measure = c => c.Lifetime.BestElementCount.Values.DefaultIfEmpty(0).Max(), Target = 4,
		},
		ElementAdept("fire_adept", "Fire Adept", "Fire", "scorching_ray", "Scorching Ray"),
		ElementAdept("ice_adept", "Ice Adept", "Ice", "glacial_spike", "Glacial Spike"),
		ElementAdept("poison_adept", "Poison Adept", "Poison", "toxic_spore_burst", "Toxic Spore Burst"),
		ElementAdept("earth_adept", "Earth Adept", "Earth", "black_tentacles", "Black Tentacles"),
		ElementAdept("wind_adept", "Wind Adept", "Wind", "gale_blade", "Gale Blade"),

		// --- Challenge -------------------------------------------------------------------------
		new()
		{
			Id = "untouchable", DisplayName = "Untouchable",
			Description = "Survive 5 minutes without being hit.",
			RewardText = "Unlocks Umbral Veil", BoonUnlockId = "umbral_veil",
			Category = AchievementCategory.Challenge,
			Measure = c => c.Lifetime.BestUndamagedSeconds,
			Target = FiveMinutes, FormatValue = AchievementDefinition.FormatTime,
		},
		new()
		{
			Id = "exterminator", DisplayName = "Exterminator",
			Description = "Defeat 2,000 enemies in total.",
			RewardText = "300 Arcane Energy", CurrencyReward = 300,
			Category = AchievementCategory.Challenge,
			Measure = c => c.Lifetime.TotalKills, Target = 2000,
		},
		new()
		{
			Id = "elite_hunter", DisplayName = "Elite Hunter",
			Description = "Open 50 chests.",
			RewardText = "200 Arcane Energy", CurrencyReward = 200,
			Category = AchievementCategory.Challenge,
			Measure = c => c.Lifetime.TotalChestsOpened, Target = 50,
		},

		// --- Exploration: chapters, bosses and relics ------------------------------------------
		BossClear("forest_cleared", "Forest Cleared", "forest_treant", "the Enchanted Forest", "thorn_vine", "Thorn Vine"),
		BossClear("castle_conqueror", "Dungeon Conqueror", "castle_warden", "the Cursed Dungeon", "shadow_bolt", "Shadow Bolt"),
		BossClear("ruins_delver", "Ruins Delver", "ruins_sentinel", "the Mystic Ruins", "spiritual_weapon", "Spiritual Weapon"),
		new()
		{
			Id = "relic_hunter", DisplayName = "Relic Hunter",
			Description = "Collect 12 different relics.",
			RewardText = "250 Arcane Energy", CurrencyReward = 250,
			Category = AchievementCategory.Exploration,
			Measure = c => c.Lifetime.ChestItemsCollected.Count, Target = 12,
		},
		new()
		{
			Id = "set_collector", DisplayName = "Attuned",
			Description = "Complete 3 different relic sets.",
			RewardText = "250 Arcane Energy", CurrencyReward = 250,
			Category = AchievementCategory.Exploration,
			Measure = c => c.Lifetime.ChestSetsCompleted.Count, Target = 3,
		},
		new()
		{
			Id = "many_hands", DisplayName = "Many Hands",
			Description = "Play a run as 4 different wizards.",
			RewardText = "200 Arcane Energy", CurrencyReward = 200,
			Category = AchievementCategory.Exploration,
			Measure = c => c.Lifetime.CharactersPlayed.Count(id => !id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase)),
			Target = 4,
		},
	};

	// The five element achievements differ only by element and reward, so they are built rather
	// than written out - five near-identical literals is five chances to paste the wrong element.
	private static AchievementDefinition ElementAdept(string id, string name, string element, string spellId, string spellName) => new()
	{
		Id = id,
		DisplayName = name,
		Description = $"Hold 4 instances of {element}.",
		RewardText = $"Unlocks {spellName}",
		SpellUnlockId = spellId,
		Category = AchievementCategory.Mastery,
		Measure = c => c.Lifetime.BestCountForElement(element),
		Target = 4,
	};

	// Boss ids are matched exactly now. They used to be matched by *substring* against terms like
	// "forest" and "castle", guarded only by a validator warning - so a boss id that happened to
	// contain another's keyword would silently grant the wrong spell.
	private static AchievementDefinition BossClear(string id, string name, string bossId, string place, string spellId, string spellName) => new()
	{
		Id = id,
		DisplayName = name,
		Description = $"Defeat the boss of {place}.",
		RewardText = $"Unlocks {spellName}",
		SpellUnlockId = spellId,
		Category = AchievementCategory.Exploration,
		Measure = c => c.Lifetime.HasDefeatedBoss(bossId) ? 1f : 0f,
		Target = 1,
	};

	public static AchievementDefinition GetById(string id) =>
		All.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
