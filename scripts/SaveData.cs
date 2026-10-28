using Godot;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

public class SaveData
{
	// 8 introduced the unlock economy. Before it, every spell was free, so a save written at 7 or
	// below has UnlockedSpellIds that mean nothing - Migrate reads that as "owned everything".
	// 11 added SeenAscensionIds (the ascension browser's sealed paths); 12 grants John Dee to a
	// save that has already finished the campaign; 13 re-derives the opened chapters under the
	// campaign order that moved the Cursed Dungeon to fifth (.ai/campaign.md); 14 grants Starfall
	// to a finished campaign the way 12 granted John Dee; 15 grants the Familiar the same way, and 16
	// the facing family (Stormsweep and Sleet Runes). 17 adds the stage secrets' two fields,
	// FoundSecretIds and FoundUpgradeLevels (.ai/stage-secrets.md section 5, issue #20).
	public const int CurrentSchemaVersion = 18;
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

	/// <summary>
	/// Every level 8 Ascension (and Apotheosis form) whose requirements the player has met at least
	/// once, as "spell_id:branch_id". Until a path is in here the ascension browser shows it SEALED -
	/// "???", no effect, no icon - and from the first level-up that finds it met it is shown in full
	/// for good, in every later run, taken or not (AscensionSightings, .ai/level-up-screen.md 4e).
	/// </summary>
	/// <remarks>
	/// Keyed by spell AND branch: the derived evolutions share their ids across spells (Titan
	/// Cataclysm is a branch of three of them), and meeting it on Cinderbreath says nothing about
	/// Bramble Seed's. Only a real run writes it; the harnesses never do.
	/// </remarks>
	public List<string> SeenAscensionIds { get; set; } = new();

	public Dictionary<string, int> MaxDifficultyCleared { get; set; } = new();
	public Dictionary<string, int> ArcaneUpgradeLevels { get; set; } = new();

	/// <summary>Every stage secret found at least once (SecretCatalog ids). Drives the Secrets page.</summary>
	public List<string> FoundSecretIds { get; set; } = new();

	/// <summary>
	/// Permanent upgrade levels FOUND in the world rather than bought (issue #20): a chapter's keepsake
	/// secret pays one. Kept apart from ArcaneUpgradeLevels so a shop refund or a respec can never eat
	/// them, and summed with it in Player.ApplyArcaneUpgrades, so they stack above the shop's cap.
	/// </summary>
	public Dictionary<string, int> FoundUpgradeLevels { get; set; } = new();

	public bool HasFoundSecret(string secretId) =>
		FoundSecretIds.Any(id => id.Equals(secretId, System.StringComparison.OrdinalIgnoreCase));

	/// <summary>Marks a secret found. False when it already was, so a reward can be paid once ever.</summary>
	public bool RecordSecretFound(string secretId)
	{
		if (string.IsNullOrWhiteSpace(secretId) || HasFoundSecret(secretId))
			return false;
		FoundSecretIds.Add(secretId);
		return true;
	}

	public int FoundUpgradeLevel(string upgradeId) =>
		FoundUpgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;

	/// <summary>The Boss Rush's records (issue #12, BossRushCatalog): the most bosses one rush has felled.</summary>
	/// <remarks>
	/// No schema bump for these two: they only add, and FromVariant guards every read with
	/// ContainsKey, so an older save reads as "never rushed".
	/// </remarks>
	public int BossRushBestFelled { get; set; } = 0;

	/// <summary>The fastest whole rush, in real seconds with the fights in. 0 until one is won.</summary>
	public float BossRushBestClearSeconds { get; set; } = 0f;
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
	/// <summary>Enemies throw their damage numbers when hit (DisplaySettings). No schema bump: an older
	/// save simply has the key missing and keeps the default, on.</summary>
	public bool ShowDamageNumbers { get; set; } = true;

	/// <summary>The key a seen ascension is stored under.</summary>
	public static string AscensionKey(string spellId, string branchId)
		=> $"{spellId?.Trim().ToLowerInvariant()}:{branchId?.Trim().ToLowerInvariant()}";

	public bool IsAscensionSeen(string spellId, string branchId)
	{
		if (string.IsNullOrWhiteSpace(spellId) || string.IsNullOrWhiteSpace(branchId))
			return false;
		string key = AscensionKey(spellId, branchId);
		return SeenAscensionIds.Any(id => id.Equals(key, System.StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Records a path as seen. True when it was not seen before (the caller saves).</summary>
	public bool MarkAscensionSeen(string spellId, string branchId)
	{
		if (string.IsNullOrWhiteSpace(spellId) || string.IsNullOrWhiteSpace(branchId)
			|| IsAscensionSeen(spellId, branchId))
			return false;
		SeenAscensionIds.Add(AscensionKey(spellId, branchId));
		return true;
	}

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
	/// <param name="log">False for the validator's sample saves, so their migrations are not
	/// mistaken in the console for the player's own.</param>
	public bool Migrate(bool log = true)
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

		if (SchemaVersion < 10)
			MigrateDiscoveriesIntoCompletedCampaign();

		if (SchemaVersion < 11)
			MigrateToSeenAscensions();
		// 11 is main's (SeenAscensionIds); John Dee is 12, so the two stack whichever lands first.
		if (SchemaVersion < 12)
			MigrateCollectorIntoCompletedCampaign();

		if (SchemaVersion < 13)
			MigrateToCampaignOrder();

		if (SchemaVersion < 14)
			MigrateStarfallIntoCompletedCampaign();

		if (SchemaVersion < 15)
			MigrateFamiliarIntoCompletedCampaign();

		if (SchemaVersion < 16)
			MigrateFacingSpellsIntoCompletedCampaign();

		if (SchemaVersion < 17)
			MigrateToSecrets();

		if (SchemaVersion < 18)
			MigrateRetiredLevelUpCharges();

		if (log)
			GD.Print($"SaveData: migrated schema {from} -> {CurrentSchemaVersion}; " +
				$"{UnlockedSpellIds.Count} spells, {UnlockedBoonIds.Count} boons, {UnlockedCharacterIds.Count} wizards, " +
				$"{UnlockedStageIds.Count} chapters opened.");
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
		//
		// Discovery wizards are excluded: they did not exist when everything was free, and a wizard
		// rescued from a cell is earned by going there (.ai/side-events.md), not handed to an old save.
		foreach (string characterId in UnlockCatalog.AllWizardIds)
		{
			// Only the wizards that existed when everything was free, which were the starters. A
			// wizard added since - John Dee, at 12 - is earned, not handed to an old save.
			if (UnlockCatalog.GetCharacter(characterId)?.Source == UnlockSource.Discovery
				|| WizardsAddedAtSchema12.Contains(characterId, System.StringComparer.OrdinalIgnoreCase))
				continue;
			if (!UnlockedCharacterIds.Any(id => id.Equals(characterId, System.StringComparison.OrdinalIgnoreCase)))
				UnlockedCharacterIds.Add(characterId);
		}
	}

	/// <summary>
	/// Schema 10: side events added five Discovery unlocks, which the final chapter's gate counts.
	/// </summary>
	/// <remarks>
	/// The Emberdeep's gate is evaluated live from "every spell recovered and every wizard freed", so
	/// adding five new things to recover would lock a player out of a chapter they had already
	/// opened. A save that met the gate as it stood before - everything that is not a Discovery
	/// unlock - is credited with the discoveries too. Any other save loses nothing and gains nothing.
	/// </remarks>
	private void MigrateDiscoveriesIntoCompletedCampaign()
	{
		// The gate as it stood at 9: the wizards added since (at 12) are not part of it, or a save
		// that had everything at 9 would be refused its discoveries for want of a wizard who did not
		// exist yet.
		bool hadEverything =
			UnlockCatalog.AllSpellIds.All(id => UnlockCatalog.GetSpell(id)?.Source == UnlockSource.Discovery
				|| SpellsAddedAtSchema14.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| SpellsAddedAtSchema15.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| SpellsAddedAtSchema16.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| GlobalStatsManager.IsSpellUnlockedForLevelUp(this, id))
			&& UnlockCatalog.AllWizardIds.All(id => UnlockCatalog.GetCharacter(id)?.Source == UnlockSource.Discovery
				|| WizardsAddedAtSchema12.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| GlobalStatsManager.IsCharacterUnlocked(this, id));
		if (!hadEverything)
			return;

		foreach (UnlockDefinition definition in UnlockCatalog.All.Where(d => d.Source == UnlockSource.Discovery))
		{
			if (definition.Kind == UnlockKind.Character)
				GlobalStatsManager.UnlockCharacter(this, definition.Id);
			else if (definition.Kind == UnlockKind.Spell)
				GlobalStatsManager.UnlockSpell(this, definition.Id);
		}
	}

	/// <summary>
	/// Schema 13: the campaign order (.ai/campaign.md) - the Cursed Dungeon moved from second to fifth,
	/// two new chapters went in before the Cave, and which boss opens which chapter is now read from
	/// StageCatalog's order instead of being written on each boss.
	/// </summary>
	/// <remarks>
	/// Nothing is taken away: every id in UnlockedStageIds stays, so a chapter a save could enter it
	/// can still enter. What has to be ADDED is what the save's victories open under the new order,
	/// which the old order never wrote. A save that had beaten the Gaoler had the Cave (what the
	/// Gaoler used to open); now the Gaoler opens the Blighted Swamp, and a player who has beaten him
	/// keeps everything after him reachable.
	///
	/// So: find every boss the save has beaten, and open what each opens now. A beaten boss is known
	/// three ways, any of which is enough - LifetimeStats.BossesDefeated (kept since schema 8), the
	/// boss-clear deeds in UnlockedAchievementIds, and the chapters the OLD order opened, since a
	/// boss victory was the only thing that ever wrote one (<see cref="OpenedBySchema12"/>).
	///
	/// Two things are deliberately NOT done here, because GlobalStatsManager.IsStageEarned derives
	/// them live: a chapter BEFORE one the save has opened is open (so the Cave opens for a save that
	/// only had the Dungeon, and the two early chapters will open by themselves for everyone past
	/// them the day they become playable - no second migration then), and a chapter whose previous
	/// boss is in the lifetime record is open.
	/// </remarks>
	private void MigrateToCampaignOrder()
	{
		var beaten = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (string bossId in Lifetime?.BossesDefeated ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(bossId))
				beaten.Add(bossId);
		}
		foreach (string achievementId in UnlockedAchievementIds)
		{
			string bossId = AchievementDefinitions.GetById(achievementId)?.BossId;
			if (!string.IsNullOrWhiteSpace(bossId))
				beaten.Add(bossId);
		}
		foreach ((string stageId, string bossId) in OpenedBySchema12)
		{
			if (UnlockedStageIds.Any(id => id.Equals(stageId, System.StringComparison.OrdinalIgnoreCase)))
				beaten.Add(bossId);
		}

		foreach (string bossId in beaten)
		{
			BossDefinition boss = BossCatalog.GetById(bossId);
			StageDefinition opened = boss != null ? BossCatalog.ChapterOpenedBy(boss) : null;
			if (opened != null)
				GlobalStatsManager.UnlockStage(this, opened.Id);
		}
	}

	/// <summary>
	/// What each boss opened up to schema 12 - BossDefinition.UnlocksStageId as it last stood, the
	/// only writer UnlockedStageIds ever had. Frozen here so the migration reads a save in the terms
	/// it was written in, whatever the order becomes. The Long Coil and the Sketchbook opened nothing.
	/// </summary>
	private static readonly (string StageId, string BossId)[] OpenedBySchema12 =
	{
		("stage_1", "forest_treant"),
		("stage_2", "castle_warden"),
		("stage_3", "cave_choir"),
		("stage_4", "swamp_mother"),
		("stage_5", "ruins_sentinel"),
		("stage_6", "frost_warden"),
	};

	/// <summary>Wizards that joined the roster at schema 12. The steps before it must neither count nor grant them.</summary>
	private static readonly string[] WizardsAddedAtSchema12 = { "john_dee" };

	/// <summary>
	/// Schema 12: John Dee joined the roster, freed by opening chests (the "Mortlake" achievement).
	/// </summary>
	/// <remarks>
	/// The same reason as schema 10: the Emberdeep's gate is "every wizard freed", evaluated live, so a
	/// new wizard would close a chapter a player had already opened - until the end of their next run,
	/// when the achievement (measured on lifetime chests, which such a player has long since opened)
	/// would hand him over. A save that met the gate as it stood before 12 is credited with him now.
	/// Any other save is untouched: he is earned the ordinary way.
	/// </remarks>
	private void MigrateCollectorIntoCompletedCampaign()
	{
		bool hadEverything =
			UnlockCatalog.AllSpellIds.All(id => SpellsAddedAtSchema14.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| SpellsAddedAtSchema15.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| SpellsAddedAtSchema16.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| GlobalStatsManager.IsSpellUnlockedForLevelUp(this, id))
			&& UnlockCatalog.AllWizardIds.All(id => WizardsAddedAtSchema12.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| GlobalStatsManager.IsCharacterUnlocked(this, id));
		if (!hadEverything)
			return;

		foreach (string characterId in WizardsAddedAtSchema12)
			GlobalStatsManager.UnlockCharacter(this, characterId);
	}

	/// <summary>Spells that joined the catalog at schema 14. The steps before it must neither count nor grant them.</summary>
	private static readonly string[] SpellsAddedAtSchema14 = { "starfall" };

	/// <summary>
	/// Schema 14: Starfall joined the shop.
	/// </summary>
	/// <remarks>
	/// Schema 10's and 12's reason again: the final chapter's gate is "every spell recovered and every
	/// wizard freed", evaluated live, so a new spell in the catalog would close a chapter a player had
	/// already opened until they bought it. A save that met the gate as it stood before 14 is credited
	/// with the new spell now. Any other save is untouched: it buys Starfall like anyone else.
	/// </remarks>
	private void MigrateStarfallIntoCompletedCampaign()
	{
		bool hadEverything =
			UnlockCatalog.AllSpellIds.All(id => SpellsAddedAtSchema14.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| SpellsAddedAtSchema15.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| SpellsAddedAtSchema16.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| GlobalStatsManager.IsSpellUnlockedForLevelUp(this, id))
			&& UnlockCatalog.AllWizardIds.All(id => GlobalStatsManager.IsCharacterUnlocked(this, id));
		if (!hadEverything)
			return;

		foreach (string spellId in SpellsAddedAtSchema14)
			GlobalStatsManager.UnlockSpell(this, spellId);
	}

	/// <summary>Spells that joined the catalog at schema 15. The steps before it must neither count nor grant them.</summary>
	private static readonly string[] SpellsAddedAtSchema15 = { "familiar" };

	/// <summary>
	/// Schema 15: the Familiar joined the shop.
	/// </summary>
	/// <remarks>
	/// Schema 14's reason again (and 10's and 12's): the final chapter's gate counts every spell in the
	/// catalog, live, so a new one would close a chapter a player had already opened until they bought
	/// it. A save that met the gate as it stood before 15 is credited with the new spell now; any other
	/// save buys it like anyone else.
	/// </remarks>
	private void MigrateFamiliarIntoCompletedCampaign()
	{
		bool hadEverything =
			UnlockCatalog.AllSpellIds.All(id => SpellsAddedAtSchema15.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| SpellsAddedAtSchema16.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| GlobalStatsManager.IsSpellUnlockedForLevelUp(this, id))
			&& UnlockCatalog.AllWizardIds.All(id => GlobalStatsManager.IsCharacterUnlocked(this, id));
		if (!hadEverything)
			return;

		foreach (string spellId in SpellsAddedAtSchema15)
			GlobalStatsManager.UnlockSpell(this, spellId);
	}

	/// <summary>Spells that joined the catalog at schema 16. The steps before it must neither count nor grant them.</summary>
	private static readonly string[] SpellsAddedAtSchema16 = { "stormsweep", "sleet_runes" };

	/// <summary>
	/// Schema 16: the facing family - Stormsweep and Sleet Runes - joined the shop.
	/// </summary>
	/// <remarks>
	/// Schema 14's reason again: the final chapter's gate counts every spell in the catalog, live, so two
	/// new ones would close a chapter a player had already opened until they bought both. A save that
	/// met the gate as it stood before 16 is credited with them now. Any other save is untouched. The
	/// same kind of grant as main's schema 15 (the Familiar), so the two can be folded into one step.
	/// </remarks>
	private void MigrateFacingSpellsIntoCompletedCampaign()
	{
		bool hadEverything =
			UnlockCatalog.AllSpellIds.All(id => SpellsAddedAtSchema16.Contains(id, System.StringComparer.OrdinalIgnoreCase)
				|| GlobalStatsManager.IsSpellUnlockedForLevelUp(this, id))
			&& UnlockCatalog.AllWizardIds.All(id => GlobalStatsManager.IsCharacterUnlocked(this, id));
		if (!hadEverything)
			return;

		foreach (string spellId in SpellsAddedAtSchema16)
			GlobalStatsManager.UnlockSpell(this, spellId);
	}

	/// <summary>
	/// Schema 11: the ascension browser seals a path until its requirements have been met once.
	/// </summary>
	/// <remarks>
	/// Starts every older save EMPTY, which is the honest answer rather than a cautious one: nothing
	/// a save wrote before this records an ascension met or taken. The run telemetry keeps how often
	/// each spell was upgraded, not which level 8 branch it took, and GameStats.ApotheosisIds is
	/// per-run and never saved - so there is nothing to seed from, and a guess would reveal paths the
	/// player never stood in front of. A returning player's sealed paths open on the first level-up
	/// that finds them met, which is the same moment a new player's do.
	/// </remarks>
	private void MigrateToSeenAscensions()
	{
		SeenAscensionIds ??= new List<string>();
	}

	/// <summary>
	/// Schema 17: the stage secrets' fields. Nothing to credit, on purpose: a secret pays nothing the
	/// campaign's gates count - never a spell, a wizard or a boon (.ai/stage-secrets.md section 1) - so
	/// no existing save can lose a chapter by not having found one.
	/// </summary>
	private void MigrateToSecrets()
	{
		FoundSecretIds ??= new List<string>();
		FoundUpgradeLevels ??= new Dictionary<string, int>();
	}

	/// <summary>
	/// The shop prices of the level-up charges removed on 2026-10-28 ("remove save and augury
	/// mechanics"): (base cost, cost added per level bought), as MainMenu registered them then. Kept
	/// here rather than read from the shop, so the refund never moves with the shop's prices - the
	/// rows are gone from the shop anyway.
	/// </summary>
	internal static readonly IReadOnlyDictionary<string, (int Base, int PerLevel)> RetiredLevelUpCharges =
		new Dictionary<string, (int Base, int PerLevel)>
		{
			["banked_levels"] = (110, 80), // Hoarded Insight: a saved level-up, two picks at the next
			["auguries"] = (130, 90),      // Augury: name a spell the next offer must hold
		};

	/// <summary>
	/// Schema 18: Save and Augury left the level-up screen, so every level bought of them is paid back
	/// in Arcane Energy at what it cost - level n (from 0) cost base + n x per-level - and the rows
	/// leave the save. A removal that kept the energy would be the silent confiscation CLAUDE.md warns
	/// of. Returns the refund, for the validator.
	/// </summary>
	internal int MigrateRetiredLevelUpCharges()
	{
		int refund = 0;
		foreach (KeyValuePair<string, (int Base, int PerLevel)> charge in RetiredLevelUpCharges)
		{
			if (!ArcaneUpgradeLevels.TryGetValue(charge.Key, out int levels))
				continue;
			for (int n = 0; n < levels; n++)
				refund += charge.Value.Base + n * charge.Value.PerLevel;
			ArcaneUpgradeLevels.Remove(charge.Key);
		}
		TotalCurrency += refund;
		return refund;
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
		//
		// From a FROZEN list - the purchasable boons as they stood at schema 9 - not from the catalog
		// as it is now. Schema 13 moved four of the seven to the early chapters (.ai/campaign.md), and
		// reading the live catalog would have quietly cut this payback from seven boons to three for
		// anyone still migrating up from 8.
		foreach (string boonId in PurchasableBoonsAtSchema9.Take(owned))
		{
			if (!UnlockedBoonIds.Any(id => id.Equals(boonId, System.StringComparison.OrdinalIgnoreCase)))
				UnlockedBoonIds.Add(boonId);
		}
	}

	/// <summary>The schema-9 payback's boons, cheapest first, exactly as the catalog priced them then.</summary>
	private static readonly string[] PurchasableBoonsAtSchema9 =
	{
		"quicksilver_bead", "thornseed", "deepwater_pearl", "shadegrease", "saltbound_chain",
		"gilded_mote", "wishing_coin",
	};

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

		var seenAscensionIds = new Godot.Collections.Array<string>();
		foreach (string id in SeenAscensionIds)
		{
			seenAscensionIds.Add(id);
		}

		var foundSecretIds = new Godot.Collections.Array<string>();
		foreach (string id in FoundSecretIds)
		{
			foundSecretIds.Add(id);
		}

		var foundUpgradeLevels = new Godot.Collections.Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in FoundUpgradeLevels)
		{
			foundUpgradeLevels[entry.Key] = entry.Value;
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
			["SeenAscensionIds"] = seenAscensionIds,
			["MaxDifficultyCleared"] = difficultyMap,
			["ArcaneUpgradeLevels"] = arcaneUpgradeLevels,
			["FoundSecretIds"] = foundSecretIds,
			["FoundUpgradeLevels"] = foundUpgradeLevels,
			["BossRushBestFelled"] = BossRushBestFelled,
			["BossRushBestClearSeconds"] = BossRushBestClearSeconds,
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
			["ShowDamageNumbers"] = ShowDamageNumbers,
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

		if (root.ContainsKey("SeenAscensionIds"))
		{
			var array = root["SeenAscensionIds"].AsGodotArray();
			foreach (Variant value in array)
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.SeenAscensionIds.Add(id);
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

		if (root.ContainsKey("FoundSecretIds"))
		{
			foreach (Variant value in root["FoundSecretIds"].AsGodotArray())
			{
				string id = value.AsString();
				if (!string.IsNullOrWhiteSpace(id))
				{
					result.FoundSecretIds.Add(id);
				}
			}
		}

		if (root.ContainsKey("FoundUpgradeLevels"))
		{
			var map = root["FoundUpgradeLevels"].AsGodotDictionary();
			foreach (Variant key in map.Keys)
			{
				string upgradeId = key.AsString();
				if (!string.IsNullOrWhiteSpace(upgradeId))
				{
					result.FoundUpgradeLevels[upgradeId] = map[key].AsInt32();
				}
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

		if (root.ContainsKey("BossRushBestFelled"))
			result.BossRushBestFelled = root["BossRushBestFelled"].AsInt32();

		if (root.ContainsKey("BossRushBestClearSeconds"))
			result.BossRushBestClearSeconds = (float)root["BossRushBestClearSeconds"].AsDouble();

		if (root.ContainsKey("MasterVolume"))
			result.MasterVolume = (float)root["MasterVolume"].AsDouble();

		if (root.ContainsKey("MusicVolume"))
			result.MusicVolume = (float)root["MusicVolume"].AsDouble();

		if (root.ContainsKey("SfxVolume"))
			result.SfxVolume = (float)root["SfxVolume"].AsDouble();

		if (root.ContainsKey("AudioMuted"))
			result.AudioMuted = root["AudioMuted"].AsBool();

		if (root.ContainsKey("ShowDamageNumbers"))
			result.ShowDamageNumbers = root["ShowDamageNumbers"].AsBool();

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
