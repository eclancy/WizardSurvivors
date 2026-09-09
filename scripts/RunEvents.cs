using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>
/// Things that happen *during* a run, recorded as they happen so an achievement can ask about them
/// afterwards.
/// </summary>
/// <remarks>
/// Before this, the only in-run event the game tracked was a single bespoke bool on
/// <c>Node2DGame</c> - <c>tookDamageBeforeFiveMinutes</c> - that existed solely to service the
/// "untouchable" achievement. That pattern does not survive a second condition, let alone a dozen:
/// every new question would mean another field threaded through the run controller.
///
/// It sits alongside <see cref="GameStats"/> rather than inside it deliberately. GameStats records
/// *totals the player is shown* (damage, rerolls, level-ups); this records *events achievements ask
/// about*. Merging them would mean one class with two audiences and no clear rule for what belongs.
///
/// Static for the same reason GameStats' telemetry block is: a run is a singleton, and threading an
/// instance through Enemy, Player and every spell to record a kill would be worse.
/// </remarks>
public static class RunEvents
{
	private static readonly Dictionary<string, int> killsByEnemyType = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, int> peakElementCounts = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> chestItemsAcquired = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> chestSetsCompleted = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> sitesDiscovered = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> spellsEvolved = new(StringComparer.OrdinalIgnoreCase);
	private static int elitesKilled;
	private static int chestsOpened;
	private static int revivesUsed;
	// Seconds into the run the player first took damage, or -1 if they never did. Kept as a time
	// rather than a bool so "survive N minutes untouched" works for any N instead of only the one
	// hardcoded five-minute question the old flag could answer.
	private static float firstDamageAtSeconds = -1f;

	public static void Reset()
	{
		killsByEnemyType.Clear();
		peakElementCounts.Clear();
		chestItemsAcquired.Clear();
		chestSetsCompleted.Clear();
		sitesDiscovered.Clear();
		spellsEvolved.Clear();
		elitesKilled = 0;
		chestsOpened = 0;
		revivesUsed = 0;
		firstDamageAtSeconds = -1f;
	}

	public static void RecordKill(string enemyType, bool isElite)
	{
		if (!string.IsNullOrWhiteSpace(enemyType))
		{
			string key = enemyType.Trim();
			killsByEnemyType[key] = killsByEnemyType.TryGetValue(key, out int existing) ? existing + 1 : 1;
		}

		if (isElite)
			elitesKilled++;
	}

	public static void RecordChestOpened() => chestsOpened++;

	public static void RecordChestItem(string itemId)
	{
		if (!string.IsNullOrWhiteSpace(itemId))
			chestItemsAcquired.Add(itemId.Trim());
	}

	public static void RecordChestSetCompleted(string setId)
	{
		if (!string.IsNullOrWhiteSpace(setId))
			chestSetsCompleted.Add(setId.Trim());
	}

	public static void RecordSiteDiscovered(string siteId)
	{
		if (!string.IsNullOrWhiteSpace(siteId))
			sitesDiscovered.Add(siteId.Trim());
	}

	public static void RecordSpellEvolved(string spellId)
	{
		if (!string.IsNullOrWhiteSpace(spellId))
			spellsEvolved.Add(spellId.Trim());
	}

	public static void RecordRevive() => revivesUsed++;

	public static void RecordFirstDamage(float atSeconds)
	{
		if (firstDamageAtSeconds < 0f)
			firstDamageAtSeconds = Math.Max(0f, atSeconds);
	}

	/// <summary>
	/// Samples the element spread. Called whenever the loadout can have changed, because
	/// <c>RunResult.ElementCounts</c> is an end-of-run snapshot and cannot answer "did you ever
	/// hold six Fire" - a build that swapped away from Fire at level 18 looks like it never had it.
	/// </summary>
	public static void RecordElementCounts(Dictionary<string, int> counts)
	{
		if (counts == null)
			return;

		foreach (KeyValuePair<string, int> entry in counts)
		{
			if (!peakElementCounts.TryGetValue(entry.Key, out int best) || entry.Value > best)
				peakElementCounts[entry.Key] = entry.Value;
		}
	}

	public static void ApplyToRunResult(global::RunResult result, float timeSurvived)
	{
		if (result == null)
			return;

		result.KillsByEnemyType = killsByEnemyType.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
		result.PeakElementCounts = peakElementCounts.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
		result.ChestItemIds = chestItemsAcquired.ToList();
		result.ChestSetIds = chestSetsCompleted.ToList();
		result.DiscoveredSiteIds = sitesDiscovered.ToList();
		result.EvolvedSpellIds = spellsEvolved.ToList();
		result.ElitesKilled = elitesKilled;
		result.ChestsOpened = chestsOpened;
		result.RevivesUsed = revivesUsed;
		// Never hit means the whole run counts as untouched.
		result.UndamagedSeconds = firstDamageAtSeconds < 0f ? timeSurvived : firstDamageAtSeconds;
	}
}
