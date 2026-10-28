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

	// What the results screen draws a spell with (GameOverScreen, .ai/level-up-screen.md section 6).
	// Plain data on purpose - a path, never a Texture2D: this snapshot outlives the run inside
	// GameStats.LastCompletedRunResult, a static, and a static route to a RefCounted resource is the
	// teardown crash BonelightSkin's remarks describe.
	public int MaxLevel { get; set; } = 1;
	public bool IsPassive { get; set; } = false;
	public string IconPath { get; set; } = string.Empty;
	public List<(string Element, int Weight)> Elements { get; set; } = new();
	/// <summary>The branch taken at each milestone, by display name; empty where none was.</summary>
	public string MutationName { get; set; } = string.Empty;
	public string AscensionName { get; set; } = string.Empty;
	public string ApotheosisName { get; set; } = string.Empty;
	/// <summary>The same branches by id, which is how their own icons are found (EvolutionIcons).</summary>
	public string MutationId { get; set; } = string.Empty;
	public string AscensionId { get; set; } = string.Empty;
	public string ApotheosisId { get; set; } = string.Empty;
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
	// Always 0 since the level-up swap was removed (spells are permanent once taken); kept so the run
	// history written before then still loads and reads the same.
	public int SwapsUsed { get; set; } = 0;
	// Spells cut out of the book - by the Scribe's Penknife now, by the level-up erase before it.
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
	// Apotheosis form ids reached this run (issue #60). What a future "reach a final form"
	// achievement measures against.
	public List<string> ApotheosisIds { get; set; } = new();
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
	// What actually came off the health bar, after armour and shields. totalDamageTaken is the raw hit
	// (Player.DamageTaken carries the pre-mitigation amount, by design), so every defensive relic and
	// boon was invisible in it: the balance pass of issue #44 measured the Barrow Harness taking more
	// "damage" than no relic at all.
	private static int totalHpLost = 0;
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
		totalHpLost = 0;
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
		damageBySource.Clear();
	}

	public static void RecordDamageDealt(int amount)
	{
		if (amount > 0)
			totalDamageDealt += amount;
	}

	/// <summary>Damage attributed to the spell that dealt it, for balance work.</summary>
	/// <remarks>
	/// Recorded in Player.DealDamageToEnemy, which is the only place the SpellData source is
	/// known - Enemy.TakeDamage sees a number and nothing else, which is why the running total
	/// above could never be broken down.
	///
	/// Two knowable gaps, worth remembering before reading too much into a table:
	/// damage-over-time ticks are applied by the enemy to itself and arrive with no source, and
	/// a killing blow is counted at its full value even when the target had less health left.
	/// Both land in the unattributed bucket or inflate a share slightly; neither changes which
	/// spell is carrying a run.
	/// </remarks>
	public static void RecordDamageBySource(string sourceId, int amount)
	{
		if (amount <= 0)
			return;

		string key = string.IsNullOrWhiteSpace(sourceId) ? "(unattributed)" : sourceId;
		damageBySource[key] = damageBySource.TryGetValue(key, out int existing) ? existing + amount : amount;
	}

	public static IReadOnlyDictionary<string, int> DamageBySource => damageBySource;

	private static readonly Dictionary<string, int> damageBySource =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	// Read by the balance harness, which has no RunResult until the run is over.
	public static int RunDamageTaken => totalDamageTaken;
	// Everything enemies lost, damage-over-time ticks included - the half of a poison spell that
	// RecordDamageBySource cannot see. In a --solo harness run every source is that one spell.
	public static int RunDamageDealt => totalDamageDealt;
	public static int RunHitsTaken => hitsTaken;
	public static int RunHpLost => totalHpLost;

	/// <summary>Health actually lost to a hit, after armour and shields (Player.TakeDamage).</summary>
	public static void RecordHpLost(int amount)
	{
		if (amount > 0)
			totalHpLost += amount;
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
