using Godot;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

public class SaveData
{
	// 8 introduced the unlock economy. Before it, every spell was free, so a save written at 7 or
	// below has UnlockedSpellIds that mean nothing - Migrate reads that as "owned everything".
	public const int CurrentSchemaVersion = 9;
	public const int CampaignSchemaVersion = 8;
	private const int MaxTelemetryHistory = 25;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public int TotalCurrency { get; set; } = 0;
	public List<string> UnlockedCharacterIds { get; set; } = new();
	public List<string> UnlockedStageIds { get; set; } = new();
	public List<string> UnlockedSpellIds { get; set; } = new();

	/// <summary>Boons the player has earned or bought. Starter boons are not listed; they are free.</summary>
	public List<string> UnlockedBoonIds { get; set; } = new();
	public List<string> UnlockedAchievementIds { get; set; } = new();
	// Spells the player has deliberately set aside so they stop being offered at level-up. Distinct
	// from "locked": these are owned, and can be put back at any time.
	public List<string> RemovedSpellIds { get; set; } = new();
	public Dictionary<string, int> MaxDifficultyCleared { get; set; } = new();
	public Dictionary<string, int> ArcaneUpgradeLevels { get; set; } = new();
	public List<RunTelemetryRecord> RunTelemetryHistory { get; set; } = new();
	// Aggregates across every run ever played. The telemetry ring above is capped at 25 entries and
	// drops most of what an achievement asks about, so it cannot serve this purpose.
	public LifetimeStats Lifetime { get; set; } = new();
	public bool HasSeenGameplayOnboarding { get; set; } = false;
	public bool EnableGameplayOnboardingTips { get; set; } = true;
	public bool HasToggledOnboardingTipsAtLeastOnce { get; set; } = false;
	public bool PlaytestModeEnabled { get; set; } = false;
	public string BalancePresetId { get; set; } = GlobalStatsManager.BalancePresetDefault;
	public Dictionary<string, bool> PlaytestChecklistState { get; set; } = new();
	// Bus volumes, linear 0..1 exactly as the sliders show them. Before these existed the
	// options screen wrote straight to AudioServer and every launch came back at full volume.
	// No schema bump is needed: FromGodotDictionary guards every read with ContainsKey, so an
	// older save simply keeps these defaults.
	public float MasterVolume { get; set; } = 1.0f;
	public float MusicVolume { get; set; } = 1.0f;
	public float SfxVolume { get; set; } = 1.0f;
	public bool AudioMuted { get; set; } = false;

	public void RecordRunTelemetry(RunResult result)
	{
		if (result == null)
			return;

		RunTelemetryHistory.Insert(0, RunTelemetryRecord.FromRunResult(result));
		if (RunTelemetryHistory.Count > MaxTelemetryHistory)
			RunTelemetryHistory.RemoveRange(MaxTelemetryHistory, RunTelemetryHistory.Count - MaxTelemetryHistory);
	}

	/// <summary>
	/// Brings an older save up to <see cref="CurrentSchemaVersion"/>. Call once, immediately after
	/// loading, before anything reads unlock state.
	/// </summary>
	/// <remarks>
	/// Until now SchemaVersion was stored, read, and never once compared against anything - there was
	/// no migration step at all, and forward compatibility rested entirely on missing JSON keys
	/// falling back to property defaults.
	///
	/// That is not good enough for this change. Before schema 8 every spell in the game was unlocked
	/// by default, so UnlockedSpellIds was almost always empty - not because the player had earned
	/// nothing, but because there was nothing to earn. Shipping the campaign without this step would
	/// silently confiscate 27 spells from anyone with an existing save. So a pre-8 save is credited
	/// with everything the old default set contained, and loses nothing.
	/// </remarks>
	public bool Migrate()
	{
		if (SchemaVersion >= CurrentSchemaVersion)
		{
			// Also covers a save written by a *newer* build than this one: leave it alone rather than
			// rewriting fields this version does not understand.
			SchemaVersion = System.Math.Max(SchemaVersion, CurrentSchemaVersion);
			return false;
		}

		int from = SchemaVersion;

		// Stepwise, and it has to be. This used to be one block guarded only by "is the save old",
		// which works exactly once: the moment a second migration exists, a schema-8 save runs the
		// pre-8 grant as well and is handed content it never earned. Each step below is gated on the
		// version it was introduced at, so a save skips only what it already has.

		if (SchemaVersion < 8)
			MigrateToCampaignUnlocks();

		if (SchemaVersion < 9)
			MigratePassiveSpellsToBoons();

		GD.Print($"SaveData: migrated schema {from} -> {CurrentSchemaVersion}; " +
			$"{UnlockedSpellIds.Count} spells, {UnlockedBoonIds.Count} boons, {UnlockedCharacterIds.Count} wizards.");
		SchemaVersion = CurrentSchemaVersion;
		return true;
	}

	/// <summary>Schema 8: nothing used to be earned, so credit a pre-campaign save with everything.</summary>
	private void MigrateToCampaignUnlocks()
	{
		foreach (string spellId in GlobalStatsManager.LegacyDefaultUnlockedSpellIds)
		{
			if (!UnlockedSpellIds.Any(id => id.Equals(spellId, System.StringComparison.OrdinalIgnoreCase)))
				UnlockedSpellIds.Add(spellId);
		}

		// Characters were likewise all free, and the roster is small enough that granting it outright
		// is kinder than asking a returning player to re-earn wizards they have already played.
		foreach (string characterId in UnlockCatalog.AllWizardIds)
		{
			if (!UnlockedCharacterIds.Any(id => id.Equals(characterId, System.StringComparison.OrdinalIgnoreCase)))
				UnlockedCharacterIds.Add(characterId);
		}
	}

	/// <summary>
	/// Schema 9: passive spells left the game and boons replaced them.
	/// </summary>
	/// <remarks>
	/// Two jobs. Strip the eleven dead ids, because a save carrying references to spells that no
	/// longer exist keeps offering them to a validator that will rightly complain. And pay back what
	/// was stripped: a player who had bought or earned a passive is credited with a boon instead,
	/// rather than silently losing currency they had already spent.
	/// </remarks>
	private void MigratePassiveSpellsToBoons()
	{
		int owned = UnlockedSpellIds.Count(id => RetiredPassiveSpellIds.Contains(id, System.StringComparer.OrdinalIgnoreCase));

		UnlockedSpellIds.RemoveAll(id => RetiredPassiveSpellIds.Contains(id, System.StringComparer.OrdinalIgnoreCase));
		RemovedSpellIds.RemoveAll(id => RetiredPassiveSpellIds.Contains(id, System.StringComparer.OrdinalIgnoreCase));

		// Paid back as purchasable boons, cheapest first, one for one. Starter boons are free to
		// everybody so they are not worth crediting, and an achievement boon should still be earned.
		foreach (UnlockDefinition boon in UnlockCatalog.PurchasableBoons.OrderBy(b => b.PurchaseCost).Take(owned))
		{
			if (!UnlockedBoonIds.Any(id => id.Equals(boon.Id, System.StringComparison.OrdinalIgnoreCase)))
				UnlockedBoonIds.Add(boon.Id);
		}
	}

	/// <summary>The eleven passive spells that schema 9 removed. Kept only so a save can be cleaned.</summary>
	private static readonly string[] RetiredPassiveSpellIds =
	{
		"aegis_ward", "thornmail_barrier", "frozen_bulwark", "stormguard_aura", "venom_cloak",
		"guardian_vines", "tidal_barrier", "stone_bulwark", "blur", "fortunes_favor", "haste",
	};

	public Godot.Collections.Dictionary ToGodotDictionary()
	{
		var characterIds = new Godot.Collections.Array<string>();
		foreach (string id in UnlockedCharacterIds)
		{
			characterIds.Add(id);
		}

		var stageIds = new Godot.Collections.Array<string>();
		foreach (string id in UnlockedStageIds)
		{
			stageIds.Add(id);
		}

		var spellIds = new Godot.Collections.Array<string>();
		foreach (string id in UnlockedSpellIds)
		{
			spellIds.Add(id);
		}

		var boonIds = new Godot.Collections.Array<string>();
		foreach (string id in UnlockedBoonIds)
		{
			boonIds.Add(id);
		}

		var achievementIds = new Godot.Collections.Array<string>();
		foreach (string id in UnlockedAchievementIds)
		{
			achievementIds.Add(id);
		}

		var removedSpellIds = new Godot.Collections.Array<string>();
		foreach (string id in RemovedSpellIds)
		{
			removedSpellIds.Add(id);
		}

		var difficultyMap = new Godot.Collections.Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in MaxDifficultyCleared)
		{
			difficultyMap[entry.Key] = entry.Value;
		}

		var arcaneUpgradeLevels = new Godot.Collections.Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in ArcaneUpgradeLevels)
		{
			arcaneUpgradeLevels[entry.Key] = entry.Value;
		}

		var telemetryHistory = new Godot.Collections.Array<Godot.Collections.Dictionary>();
		foreach (RunTelemetryRecord record in RunTelemetryHistory)
		{
			telemetryHistory.Add(record.ToGodotDictionary());
		}

		var playtestChecklistState = new Godot.Collections.Dictionary<string, bool>();
		foreach (KeyValuePair<string, bool> entry in PlaytestChecklistState)
		{
			playtestChecklistState[entry.Key] = entry.Value;
		}

		return new Godot.Collections.Dictionary
		{
			["SchemaVersion"] = SchemaVersion,
			["TotalCurrency"] = TotalCurrency,
			["UnlockedCharacterIds"] = characterIds,
			["UnlockedStageIds"] = stageIds,
			["UnlockedSpellIds"] = spellIds,
			["UnlockedBoonIds"] = boonIds,
			["UnlockedAchievementIds"] = achievementIds,
			["RemovedSpellIds"] = removedSpellIds,
			["MaxDifficultyCleared"] = difficultyMap,
			["ArcaneUpgradeLevels"] = arcaneUpgradeLevels,
			["RunTelemetryHistory"] = telemetryHistory,
			["Lifetime"] = Lifetime.ToGodotDictionary(),
			["HasSeenGameplayOnboarding"] = HasSeenGameplayOnboarding,
			["EnableGameplayOnboardingTips"] = EnableGameplayOnboardingTips,
			["HasToggledOnboardingTipsAtLeastOnce"] = HasToggledOnboardingTipsAtLeastOnce,
			["PlaytestModeEnabled"] = PlaytestModeEnabled,
			["MasterVolume"] = MasterVolume,
			["MusicVolume"] = MusicVolume,
			["SfxVolume"] = SfxVolume,
			["AudioMuted"] = AudioMuted,
			["BalancePresetId"] = BalancePresetId,
			["PlaytestChecklistState"] = playtestChecklistState
		};
	}

	public static SaveData FromVariant(Variant variant)
	{
		var result = new SaveData();
		if (variant.VariantType != Variant.Type.Dictionary)
		{
			return result;
		}

		var root = variant.AsGodotDictionary();

		if (root.ContainsKey("SchemaVersion"))
		{
			result.SchemaVersion = root["SchemaVersion"].AsInt32();
		}

		if (root.ContainsKey("TotalCurrency"))
		{
			result.TotalCurrency = root["TotalCurrency"].AsInt32();
		}

		if (root.ContainsKey("UnlockedCharacterIds"))
		{
			var array = root["UnlockedCharacterIds"].AsGodotArray();
			foreach (Variant value in array)
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.UnlockedCharacterIds.Add(id);
				}
			}
		}

		if (root.ContainsKey("UnlockedStageIds"))
		{
			var array = root["UnlockedStageIds"].AsGodotArray();
			foreach (Variant value in array)
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.UnlockedStageIds.Add(id);
				}
			}
		}

		if (root.ContainsKey("UnlockedSpellIds"))
		{
			var array = root["UnlockedSpellIds"].AsGodotArray();
			foreach (Variant value in array)
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.UnlockedSpellIds.Add(id);
				}
			}
		}

		if (root.ContainsKey("UnlockedBoonIds"))
		{
			var array = root["UnlockedBoonIds"].AsGodotArray();
			foreach (Variant value in array)
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.UnlockedBoonIds.Add(id);
				}
			}
		}

		if (root.ContainsKey("UnlockedAchievementIds"))
		{
			var array = root["UnlockedAchievementIds"].AsGodotArray();
			foreach (Variant value in array)
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.UnlockedAchievementIds.Add(id);
				}
			}
		}

		if (root.ContainsKey("RemovedSpellIds"))
		{
			var array = root["RemovedSpellIds"].AsGodotArray();
			foreach (Variant value in array)
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.RemovedSpellIds.Add(id);
				}
			}
		}

		if (root.ContainsKey("MaxDifficultyCleared"))
		{
			var map = root["MaxDifficultyCleared"].AsGodotDictionary();
			foreach (Variant key in map.Keys)
			{
				string mapId = key.AsString();
				if (string.IsNullOrWhiteSpace(mapId))
				{
					continue;
				}

				result.MaxDifficultyCleared[mapId] = map[key].AsInt32();
			}
		}

		if (root.ContainsKey("ArcaneUpgradeLevels"))
		{
			var map = root["ArcaneUpgradeLevels"].AsGodotDictionary();
			foreach (Variant key in map.Keys)
			{
				string upgradeId = key.AsString();
				if (string.IsNullOrWhiteSpace(upgradeId))
				{
					continue;
				}

				result.ArcaneUpgradeLevels[upgradeId] = map[key].AsInt32();
			}
		}

		if (root.ContainsKey("RunTelemetryHistory"))
		{
			var array = root["RunTelemetryHistory"].AsGodotArray();
			foreach (Variant entry in array)
			{
				if (entry.VariantType == Variant.Type.Dictionary)
					result.RunTelemetryHistory.Add(RunTelemetryRecord.FromDictionary(entry.AsGodotDictionary()));
			}

			if (result.RunTelemetryHistory.Count > MaxTelemetryHistory)
				result.RunTelemetryHistory = result.RunTelemetryHistory.GetRange(0, MaxTelemetryHistory);
		}

		if (root.ContainsKey("Lifetime") && root["Lifetime"].VariantType == Variant.Type.Dictionary)
			result.Lifetime = LifetimeStats.FromDictionary(root["Lifetime"].AsGodotDictionary());

		if (root.ContainsKey("HasSeenGameplayOnboarding"))
			result.HasSeenGameplayOnboarding = root["HasSeenGameplayOnboarding"].AsBool();

		if (root.ContainsKey("EnableGameplayOnboardingTips"))
			result.EnableGameplayOnboardingTips = root["EnableGameplayOnboardingTips"].AsBool();

		if (root.ContainsKey("HasToggledOnboardingTipsAtLeastOnce"))
			result.HasToggledOnboardingTipsAtLeastOnce = root["HasToggledOnboardingTipsAtLeastOnce"].AsBool();

		if (root.ContainsKey("PlaytestModeEnabled"))
			result.PlaytestModeEnabled = root["PlaytestModeEnabled"].AsBool();

		if (root.ContainsKey("MasterVolume"))
			result.MasterVolume = (float)root["MasterVolume"].AsDouble();

		if (root.ContainsKey("MusicVolume"))
			result.MusicVolume = (float)root["MusicVolume"].AsDouble();

		if (root.ContainsKey("SfxVolume"))
			result.SfxVolume = (float)root["SfxVolume"].AsDouble();

		if (root.ContainsKey("AudioMuted"))
			result.AudioMuted = root["AudioMuted"].AsBool();

		if (root.ContainsKey("BalancePresetId"))
			result.BalancePresetId = GlobalStatsManager.NormalizeBalancePresetId(root["BalancePresetId"].AsString());

		if (root.ContainsKey("PlaytestChecklistState"))
		{
			var map = root["PlaytestChecklistState"].AsGodotDictionary();
			foreach (Variant key in map.Keys)
			{
				string itemId = key.AsString();
				if (string.IsNullOrWhiteSpace(itemId))
					continue;

				result.PlaytestChecklistState[itemId] = map[key].AsBool();
			}
		}

		return result;
	}
}

public sealed class RunTelemetryRecord
{
	public string BalancePresetId { get; set; } = GlobalStatsManager.BalancePresetDefault;
	public string StageId { get; set; } = string.Empty;
	public string Outcome { get; set; } = "Defeat";
	public int FinalPlayerLevel { get; set; } = 1;
	public float TimeSurvived { get; set; } = 0f;
	public int EnemiesKilled { get; set; } = 0;
	public int TotalDamageDealt { get; set; } = 0;
	public int TotalDamageTaken { get; set; } = 0;
	public int HitsTaken { get; set; } = 0;
	public int LevelUpsGained { get; set; } = 0;
	public int RerollsUsed { get; set; } = 0;
	public int SwapsUsed { get; set; } = 0;
	public int RemovalsUsed { get; set; } = 0;
	public int SkipsUsed { get; set; } = 0;
	public float ArcaneRewardMultiplier { get; set; } = 1.0f;
	public string ArcaneRewardBreakdown { get; set; } = string.Empty;
	public Dictionary<string, int> SpellPickCounts { get; set; } = new();
	public Dictionary<string, int> SpellUpgradeCounts { get; set; } = new();

	public static RunTelemetryRecord FromRunResult(RunResult run)
	{
		return new RunTelemetryRecord
		{
			BalancePresetId = GlobalStatsManager.NormalizeBalancePresetId(run.BalancePresetId),
			StageId = run.StageId,
			Outcome = run.Outcome.ToString(),
			FinalPlayerLevel = run.FinalPlayerLevel,
			TimeSurvived = run.TimeSurvived,
			EnemiesKilled = run.EnemiesKilled,
			TotalDamageDealt = run.TotalDamageDealt,
			TotalDamageTaken = run.TotalDamageTaken,
			HitsTaken = run.HitsTaken,
			LevelUpsGained = run.LevelUpsGained,
			RerollsUsed = run.RerollsUsed,
			SwapsUsed = run.SwapsUsed,
			RemovalsUsed = run.RemovalsUsed,
			SkipsUsed = run.SkipsUsed,
			ArcaneRewardMultiplier = run.ArcaneRewardMultiplier,
			ArcaneRewardBreakdown = run.ArcaneRewardBreakdown ?? string.Empty,
			SpellPickCounts = CopyIntMap(run.SpellPickCounts),
			SpellUpgradeCounts = CopyIntMap(run.SpellUpgradeCounts)
		};
	}

	public Godot.Collections.Dictionary ToGodotDictionary()
	{
		return new Godot.Collections.Dictionary
		{
			["BalancePresetId"] = BalancePresetId,
			["StageId"] = StageId,
			["Outcome"] = Outcome,
			["FinalPlayerLevel"] = FinalPlayerLevel,
			["TimeSurvived"] = TimeSurvived,
			["EnemiesKilled"] = EnemiesKilled,
			["TotalDamageDealt"] = TotalDamageDealt,
			["TotalDamageTaken"] = TotalDamageTaken,
			["HitsTaken"] = HitsTaken,
			["LevelUpsGained"] = LevelUpsGained,
			["RerollsUsed"] = RerollsUsed,
			["SwapsUsed"] = SwapsUsed,
			["RemovalsUsed"] = RemovalsUsed,
			["SkipsUsed"] = SkipsUsed,
			["ArcaneRewardMultiplier"] = ArcaneRewardMultiplier,
			["ArcaneRewardBreakdown"] = ArcaneRewardBreakdown,
			["SpellPickCounts"] = ToGodotMap(SpellPickCounts),
			["SpellUpgradeCounts"] = ToGodotMap(SpellUpgradeCounts)
		};
	}

	public static RunTelemetryRecord FromDictionary(Godot.Collections.Dictionary dict)
	{
		var result = new RunTelemetryRecord();

		if (dict.ContainsKey("BalancePresetId")) result.BalancePresetId = GlobalStatsManager.NormalizeBalancePresetId(dict["BalancePresetId"].AsString());
		if (dict.ContainsKey("StageId")) result.StageId = dict["StageId"].AsString();
		if (dict.ContainsKey("Outcome")) result.Outcome = dict["Outcome"].AsString();
		if (dict.ContainsKey("FinalPlayerLevel")) result.FinalPlayerLevel = dict["FinalPlayerLevel"].AsInt32();
		if (dict.ContainsKey("TimeSurvived")) result.TimeSurvived = dict["TimeSurvived"].AsSingle();
		if (dict.ContainsKey("EnemiesKilled")) result.EnemiesKilled = dict["EnemiesKilled"].AsInt32();
		if (dict.ContainsKey("TotalDamageDealt")) result.TotalDamageDealt = dict["TotalDamageDealt"].AsInt32();
		if (dict.ContainsKey("TotalDamageTaken")) result.TotalDamageTaken = dict["TotalDamageTaken"].AsInt32();
		if (dict.ContainsKey("HitsTaken")) result.HitsTaken = dict["HitsTaken"].AsInt32();
		if (dict.ContainsKey("LevelUpsGained")) result.LevelUpsGained = dict["LevelUpsGained"].AsInt32();
		if (dict.ContainsKey("RerollsUsed")) result.RerollsUsed = dict["RerollsUsed"].AsInt32();
		if (dict.ContainsKey("SwapsUsed")) result.SwapsUsed = dict["SwapsUsed"].AsInt32();
		if (dict.ContainsKey("RemovalsUsed")) result.RemovalsUsed = dict["RemovalsUsed"].AsInt32();
		if (dict.ContainsKey("SkipsUsed")) result.SkipsUsed = dict["SkipsUsed"].AsInt32();
		if (dict.ContainsKey("ArcaneRewardMultiplier")) result.ArcaneRewardMultiplier = dict["ArcaneRewardMultiplier"].AsSingle();
		if (dict.ContainsKey("ArcaneRewardBreakdown")) result.ArcaneRewardBreakdown = dict["ArcaneRewardBreakdown"].AsString();

		if (dict.ContainsKey("SpellPickCounts"))
			result.SpellPickCounts = FromGodotMap(dict["SpellPickCounts"].AsGodotDictionary());
		if (dict.ContainsKey("SpellUpgradeCounts"))
			result.SpellUpgradeCounts = FromGodotMap(dict["SpellUpgradeCounts"].AsGodotDictionary());

		return result;
	}

	private static Godot.Collections.Dictionary<string, int> ToGodotMap(Dictionary<string, int> source)
	{
		var map = new Godot.Collections.Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in source)
			map[entry.Key] = entry.Value;

		return map;
	}

	private static Dictionary<string, int> FromGodotMap(Godot.Collections.Dictionary map)
	{
		var result = new Dictionary<string, int>();
		foreach (Variant key in map.Keys)
		{
			string id = key.AsString();
			if (string.IsNullOrWhiteSpace(id))
				continue;

			result[id] = map[key].AsInt32();
		}

		return result;
	}

	private static Dictionary<string, int> CopyIntMap(Dictionary<string, int> source)
	{
		if (source == null)
			return new Dictionary<string, int>();

		var copy = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in source)
			copy[entry.Key] = entry.Value;

		return copy;
	}
}
