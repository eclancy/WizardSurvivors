using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

public class SaveData
{
	public const int CurrentSchemaVersion = 7;
	private const int MaxTelemetryHistory = 25;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public int TotalCurrency { get; set; } = 0;
	public List<string> UnlockedCharacterIds { get; set; } = new();
	public List<string> UnlockedStageIds { get; set; } = new();
	public List<string> UnlockedSpellIds { get; set; } = new();
	public List<string> UnlockedAchievementIds { get; set; } = new();
	public Dictionary<string, int> MaxDifficultyCleared { get; set; } = new();
	public Dictionary<string, int> ArcaneUpgradeLevels { get; set; } = new();
	public List<RunTelemetryRecord> RunTelemetryHistory { get; set; } = new();
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

		var achievementIds = new Godot.Collections.Array<string>();
		foreach (string id in UnlockedAchievementIds)
		{
			achievementIds.Add(id);
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
			["UnlockedAchievementIds"] = achievementIds,
			["MaxDifficultyCleared"] = difficultyMap,
			["ArcaneUpgradeLevels"] = arcaneUpgradeLevels,
			["RunTelemetryHistory"] = telemetryHistory,
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
