using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>
/// Everything the player has ever done, accumulated across runs.
/// </summary>
/// <remarks>
/// The save had no aggregate counters of any kind: the only cross-run data was a 25-entry
/// <c>RunTelemetryHistory</c> ring that drops <c>BossId</c>, <c>ElementCounts</c> and
/// <c>EquippedSpells</c> on the way in. So it could not answer "have you ever beaten the forest
/// boss" or "what is your best time in the Cave" - not because the data was old, but because it was
/// never stored.
///
/// This exists as much for the achievement *UI* as for the conditions. An achievement measured over
/// a single run can only be evaluated at the moment that run ends, which means the main menu has
/// nothing to show but "In Progress". Measured over lifetime stats, the same condition can report
/// "6:12 / 10:00" from anywhere. That is why bests are recorded per stage and per element rather
/// than only in aggregate.
/// </remarks>
public sealed class LifetimeStats
{
	public int TotalRuns { get; set; }
	public int TotalVictories { get; set; }
	public int TotalKills { get; set; }
	public int TotalLevelUps { get; set; }
	public int TotalChestsOpened { get; set; }
	public float TotalPlaytimeSeconds { get; set; }
	public int BestPlayerLevel { get; set; }
	public int BestSpellSlotsFilled { get; set; }
	/// <summary>Longest a run has ever gone before the first hit landed.</summary>
	public float BestUndamagedSeconds { get; set; }

	public Dictionary<string, float> BestTimeByStage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	/// <summary>Highest instance count ever held, per element. Feeds the "adept" achievements.</summary>
	public Dictionary<string, int> BestElementCount { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, int> KillsByEnemyType { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public List<string> BossesDefeated { get; set; } = new();
	public List<string> ChestItemsCollected { get; set; } = new();
	public List<string> ChestSetsCompleted { get; set; } = new();
	public List<string> CharactersPlayed { get; set; } = new();

	public float BestTimeOnStage(string stageId) =>
		!string.IsNullOrWhiteSpace(stageId) && BestTimeByStage.TryGetValue(stageId, out float best) ? best : 0f;

	public int BestCountForElement(string element) =>
		!string.IsNullOrWhiteSpace(element) && BestElementCount.TryGetValue(element, out int best) ? best : 0;

	public bool HasDefeatedBoss(string bossId) =>
		!string.IsNullOrWhiteSpace(bossId)
		&& BossesDefeated.Any(id => id.Equals(bossId, StringComparison.OrdinalIgnoreCase));

	/// <summary>Folds one finished run in. Call once, at run end, before achievements are evaluated.</summary>
	public void Absorb(global::RunResult run)
	{
		if (run == null)
			return;

		TotalRuns++;
		if (run.Outcome == global::RunOutcome.Victory)
			TotalVictories++;

		TotalKills += Math.Max(0, run.EnemiesKilled);
		TotalLevelUps += Math.Max(0, run.LevelUpsGained);
		TotalChestsOpened += Math.Max(0, run.ChestsOpened);
		TotalPlaytimeSeconds += Math.Max(0f, run.TimeSurvived);
		BestPlayerLevel = Math.Max(BestPlayerLevel, run.FinalPlayerLevel);
		BestUndamagedSeconds = Math.Max(BestUndamagedSeconds, run.UndamagedSeconds);
		BestSpellSlotsFilled = Math.Max(BestSpellSlotsFilled, run.EquippedSpells?.Count ?? 0);

		if (!string.IsNullOrWhiteSpace(run.StageId)
			&& run.TimeSurvived > BestTimeOnStage(run.StageId))
		{
			BestTimeByStage[run.StageId] = run.TimeSurvived;
		}

		// Peak counts, not the end-of-run snapshot - see RunEvents.RecordElementCounts.
		AbsorbBests(BestElementCount, run.PeakElementCounts);
		AbsorbTotals(KillsByEnemyType, run.KillsByEnemyType);

		AddDistinct(BossesDefeated, run.BossId);
		AddDistinct(CharactersPlayed, run.CharacterId);
		foreach (string itemId in run.ChestItemIds ?? new List<string>())
			AddDistinct(ChestItemsCollected, itemId);
		foreach (string setId in run.ChestSetIds ?? new List<string>())
			AddDistinct(ChestSetsCompleted, setId);
	}

	private static void AbsorbBests(Dictionary<string, int> target, Dictionary<string, int> source)
	{
		if (source == null)
			return;

		foreach (KeyValuePair<string, int> entry in source)
		{
			if (!target.TryGetValue(entry.Key, out int best) || entry.Value > best)
				target[entry.Key] = entry.Value;
		}
	}

	private static void AbsorbTotals(Dictionary<string, int> target, Dictionary<string, int> source)
	{
		if (source == null)
			return;

		foreach (KeyValuePair<string, int> entry in source)
			target[entry.Key] = target.TryGetValue(entry.Key, out int existing) ? existing + entry.Value : entry.Value;
	}

	private static void AddDistinct(List<string> list, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return;

		if (!list.Any(id => id.Equals(value, StringComparison.OrdinalIgnoreCase)))
			list.Add(value);
	}

	public Godot.Collections.Dictionary ToGodotDictionary()
	{
		return new Godot.Collections.Dictionary
		{
			["TotalRuns"] = TotalRuns,
			["TotalVictories"] = TotalVictories,
			["TotalKills"] = TotalKills,
			["TotalLevelUps"] = TotalLevelUps,
			["TotalChestsOpened"] = TotalChestsOpened,
			["TotalPlaytimeSeconds"] = TotalPlaytimeSeconds,
			["BestPlayerLevel"] = BestPlayerLevel,
			["BestSpellSlotsFilled"] = BestSpellSlotsFilled,
			["BestUndamagedSeconds"] = BestUndamagedSeconds,
			["BestTimeByStage"] = ToFloatMap(BestTimeByStage),
			["BestElementCount"] = ToIntMap(BestElementCount),
			["KillsByEnemyType"] = ToIntMap(KillsByEnemyType),
			["BossesDefeated"] = ToArray(BossesDefeated),
			["ChestItemsCollected"] = ToArray(ChestItemsCollected),
			["ChestSetsCompleted"] = ToArray(ChestSetsCompleted),
			["CharactersPlayed"] = ToArray(CharactersPlayed),
		};
	}

	public static LifetimeStats FromDictionary(Godot.Collections.Dictionary dict)
	{
		var result = new LifetimeStats();
		if (dict == null)
			return result;

		if (dict.ContainsKey("TotalRuns")) result.TotalRuns = dict["TotalRuns"].AsInt32();
		if (dict.ContainsKey("TotalVictories")) result.TotalVictories = dict["TotalVictories"].AsInt32();
		if (dict.ContainsKey("TotalKills")) result.TotalKills = dict["TotalKills"].AsInt32();
		if (dict.ContainsKey("TotalLevelUps")) result.TotalLevelUps = dict["TotalLevelUps"].AsInt32();
		if (dict.ContainsKey("TotalChestsOpened")) result.TotalChestsOpened = dict["TotalChestsOpened"].AsInt32();
		if (dict.ContainsKey("TotalPlaytimeSeconds")) result.TotalPlaytimeSeconds = dict["TotalPlaytimeSeconds"].AsSingle();
		if (dict.ContainsKey("BestPlayerLevel")) result.BestPlayerLevel = dict["BestPlayerLevel"].AsInt32();
		if (dict.ContainsKey("BestSpellSlotsFilled")) result.BestSpellSlotsFilled = dict["BestSpellSlotsFilled"].AsInt32();
		if (dict.ContainsKey("BestUndamagedSeconds")) result.BestUndamagedSeconds = dict["BestUndamagedSeconds"].AsSingle();
		if (dict.ContainsKey("BestTimeByStage")) result.BestTimeByStage = FromFloatMap(dict["BestTimeByStage"].AsGodotDictionary());
		if (dict.ContainsKey("BestElementCount")) result.BestElementCount = FromIntMap(dict["BestElementCount"].AsGodotDictionary());
		if (dict.ContainsKey("KillsByEnemyType")) result.KillsByEnemyType = FromIntMap(dict["KillsByEnemyType"].AsGodotDictionary());
		if (dict.ContainsKey("BossesDefeated")) result.BossesDefeated = FromArray(dict["BossesDefeated"].AsGodotArray());
		if (dict.ContainsKey("ChestItemsCollected")) result.ChestItemsCollected = FromArray(dict["ChestItemsCollected"].AsGodotArray());
		if (dict.ContainsKey("ChestSetsCompleted")) result.ChestSetsCompleted = FromArray(dict["ChestSetsCompleted"].AsGodotArray());
		if (dict.ContainsKey("CharactersPlayed")) result.CharactersPlayed = FromArray(dict["CharactersPlayed"].AsGodotArray());

		return result;
	}

	private static Godot.Collections.Dictionary<string, int> ToIntMap(Dictionary<string, int> source)
	{
		var map = new Godot.Collections.Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in source)
			map[entry.Key] = entry.Value;
		return map;
	}

	private static Godot.Collections.Dictionary<string, float> ToFloatMap(Dictionary<string, float> source)
	{
		var map = new Godot.Collections.Dictionary<string, float>();
		foreach (KeyValuePair<string, float> entry in source)
			map[entry.Key] = entry.Value;
		return map;
	}

	private static Godot.Collections.Array<string> ToArray(List<string> source)
	{
		var array = new Godot.Collections.Array<string>();
		foreach (string value in source)
			array.Add(value);
		return array;
	}

	private static Dictionary<string, int> FromIntMap(Godot.Collections.Dictionary map)
	{
		var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (Variant key in map.Keys)
		{
			string id = key.AsString();
			if (!string.IsNullOrWhiteSpace(id))
				result[id] = map[key].AsInt32();
		}
		return result;
	}

	private static Dictionary<string, float> FromFloatMap(Godot.Collections.Dictionary map)
	{
		var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
		foreach (Variant key in map.Keys)
		{
			string id = key.AsString();
			if (!string.IsNullOrWhiteSpace(id))
				result[id] = map[key].AsSingle();
		}
		return result;
	}

	private static List<string> FromArray(Godot.Collections.Array array)
	{
		var result = new List<string>();
		foreach (Variant value in array)
		{
			string id = value.AsString();
			if (!string.IsNullOrWhiteSpace(id))
				result.Add(id);
		}
		return result;
	}
}
