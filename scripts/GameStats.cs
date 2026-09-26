using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum RunOutcome
{
	Defeat,
	Victory
}

public class RunSpellSnapshot
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public int Level { get; set; } = 1;
	public bool IsLegendary { get; set; } = false;
}

public class RunResult
{
	public RunOutcome Outcome { get; set; } = RunOutcome.Defeat;
	public string BalancePresetId { get; set; } = WizardSurvivors.scripts.GlobalStatsManager.BalancePresetDefault;
	public string StageId { get; set; } = string.Empty;
	public int FinalPlayerLevel { get; set; } = 1;
	public float TimeSurvived { get; set; } = 0f;
	public int EnemiesKilled { get; set; } = 0;
	public string BossId { get; set; } = string.Empty;
	public bool TookDamageBeforeFiveMinutes { get; set; } = false;
	public Dictionary<string, int> ElementCounts { get; set; } = new();
	public List<RunSpellSnapshot> EquippedSpells { get; set; } = new();
	public int TotalDamageDealt { get; set; } = 0;
	public int TotalDamageTaken { get; set; } = 0;
	public int HitsTaken { get; set; } = 0;
	public int LevelUpsGained { get; set; } = 0;
	public int RerollsUsed { get; set; } = 0;
	public int SwapsUsed { get; set; } = 0;
	public int RemovalsUsed { get; set; } = 0;
	public int SkipsUsed { get; set; } = 0;

	// The three run-scoped level-up charges. Counted separately from rerolls because they come
	// from a pool that does not refill, so "used 2" means something quite different for these.
	public int BansUsed { get; set; } = 0;
	public int SavesUsed { get; set; } = 0;
	public int AuguriesUsed { get; set; } = 0;
	public Dictionary<string, int> SpellPickCounts { get; set; } = new();
	public Dictionary<string, int> SpellUpgradeCounts { get; set; } = new();
	public float ArcaneRewardMultiplier { get; set; } = 1.0f;
	public string ArcaneRewardBreakdown { get; set; } = string.Empty;

	// --- Filled by RunEvents (issue: achievements v2) ------------------------------------------
	// Everything above describes the run as a set of totals. These describe what actually happened
	// in it, which is what a varied achievement condition needs to ask about.

	/// <summary>Which wizard played the run. Was never recorded at all before.</summary>
	public string CharacterId { get; set; } = string.Empty;
	public Dictionary<string, int> KillsByEnemyType { get; set; } = new();
	/// <summary>Highest instance count each element ever reached, not the end-of-run snapshot.</summary>
	public Dictionary<string, int> PeakElementCounts { get; set; } = new();
	public List<string> ChestItemIds { get; set; } = new();
	public List<string> ChestSetIds { get; set; } = new();
	public List<string> DiscoveredSiteIds { get; set; } = new();
	public List<string> EvolvedSpellIds { get; set; } = new();
	public int ElitesKilled { get; set; } = 0;
	public int ChestsOpened { get; set; } = 0;
	public int RevivesUsed { get; set; } = 0;
	/// <summary>Seconds survived before the first hit landed; equals TimeSurvived if never hit.</summary>
	public float UndamagedSeconds { get; set; } = 0f;
}

public partial class GameStats : Node
{
	public static RunResult LastCompletedRunResult { get; private set; }
	private static int totalDamageDealt = 0;
	private static int totalDamageTaken = 0;
	private static int hitsTaken = 0;
	private static int levelUpsGained = 0;
	private static int rerollsUsed = 0;
	private static int swapsUsed = 0;
	private static int removalsUsed = 0;
	private static int skipsUsed = 0;
	private static int bansUsed = 0;
	private static int savesUsed = 0;
	private static int auguriesUsed = 0;
	private static readonly Dictionary<string, int> spellPickCounts = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, int> spellUpgradeCounts = new(StringComparer.OrdinalIgnoreCase);

	public int EnemiesKilled { get; set; } = 0;
	public RunResult LastRunResult { get; private set; }

	public static void RecordRunResult(RunResult result)
	{
		LastCompletedRunResult = result;
	}

	public static void ResetRunTelemetry()
	{
		totalDamageDealt = 0;
		totalDamageTaken = 0;
		hitsTaken = 0;
		levelUpsGained = 0;
		rerollsUsed = 0;
		swapsUsed = 0;
		removalsUsed = 0;
		skipsUsed = 0;
		bansUsed = 0;
		savesUsed = 0;
		auguriesUsed = 0;
		spellPickCounts.Clear();
		spellUpgradeCounts.Clear();
	}

	public static void RecordDamageDealt(int amount)
	{
		if (amount > 0)
			totalDamageDealt += amount;
	}

	public static void RecordDamageTaken(int amount)
	{
		if (amount <= 0)
			return;

		totalDamageTaken += amount;
		hitsTaken++;
	}

	public static void RecordLevelUp()
	{
		levelUpsGained++;
	}

	public static void RecordRerollUsed()
	{
		rerollsUsed++;
	}

	public static void RecordSwapUsed()
	{
		swapsUsed++;
	}

	public static void RecordRemovalUsed()
	{
		removalsUsed++;
	}

	public static void RecordBanUsed() => bansUsed++;

	public static void RecordSaveUsed() => savesUsed++;

	public static void RecordAuguryUsed() => auguriesUsed++;

	public static void RecordSkipUsed()
	{
		skipsUsed++;
	}

	public static void RecordSpellPicked(string spellId)
	{
		IncrementCounter(spellPickCounts, spellId);
	}

	public static void RecordSpellUpgraded(string spellId)
	{
		IncrementCounter(spellUpgradeCounts, spellId);
	}

	private static void IncrementCounter(Dictionary<string, int> map, string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return;

		string normalized = key.Trim();
		map[normalized] = map.TryGetValue(normalized, out int existing) ? existing + 1 : 1;
	}

	public static void ApplyTelemetryToRunResult(RunResult result)
	{
		if (result == null)
			return;

		result.TotalDamageDealt = totalDamageDealt;
		result.TotalDamageTaken = totalDamageTaken;
		result.HitsTaken = hitsTaken;
		result.LevelUpsGained = levelUpsGained;
		result.RerollsUsed = rerollsUsed;
		result.SwapsUsed = swapsUsed;
		result.RemovalsUsed = removalsUsed;
		result.SkipsUsed = skipsUsed;
		result.BansUsed = bansUsed;
		result.SavesUsed = savesUsed;
		result.AuguriesUsed = auguriesUsed;
		result.SpellPickCounts = spellPickCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
		result.SpellUpgradeCounts = spellUpgradeCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
	}

	public void SetLastRunResult(RunResult result)
	{
		RecordRunResult(result);
		LastRunResult = result;
		EnemiesKilled = result?.EnemiesKilled ?? 0;
	}

	public void Reset()
	{
		EnemiesKilled = 0;
		LastRunResult = null;
		LastCompletedRunResult = null;
		ResetRunTelemetry();
	}
}
