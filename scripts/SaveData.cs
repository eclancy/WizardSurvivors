using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

public class SaveData
{
	public int TotalCurrency { get; set; } = 0;
	public List<string> UnlockedCharacterIds { get; set; } = new();
	public List<string> UnlockedStageIds { get; set; } = new();
	public Dictionary<string, int> MaxDifficultyCleared { get; set; } = new();

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

		var difficultyMap = new Godot.Collections.Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in MaxDifficultyCleared)
		{
			difficultyMap[entry.Key] = entry.Value;
		}

		return new Godot.Collections.Dictionary
		{
			["TotalCurrency"] = TotalCurrency,
			["UnlockedCharacterIds"] = characterIds,
			["UnlockedStageIds"] = stageIds,
			["MaxDifficultyCleared"] = difficultyMap
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

		return result;
	}
}
