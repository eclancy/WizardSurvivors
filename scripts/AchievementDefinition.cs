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

/// <summary>What kind of thing an achievement asks (.ai/achievements-and-secrets.md, section 2).</summary>
public enum AchievementKind
{
	/// <summary>A simple challenge measured over runs - kills, time survived, level, elements held. Names no chapter.</summary>
	Feat,
	/// <summary>A chapter's deed: beat its boss, win its side event, run its quarry down.</summary>
	Road,
	/// <summary>Something found in the world; derived from a <see cref="SecretDefinition"/>.</summary>
	Secret,
	/// <summary>Owning a whole set: every spell, a dozen relics.</summary>
	Collection,
}

/// <summary>When an achievement is shown on the achievements page (.ai/achievements-and-secrets.md 4.2).</summary>
public enum AchievementVisibility
{
	/// <summary>From the first run.</summary>
	Always,
	/// <summary>
	/// Once its chapter is playable. A deed that frees a SECRET wizard is a sealed row
	/// (<see cref="AchievementDefinition.SealedLine"/>) until his chapter opens - the character select's rule.
	/// </summary>
	ChapterOpen,
	/// <summary>Counted, never named, until it has been seen (<see cref="SaveData.GlimpsedSecretIds"/>) or found.</summary>
	Hidden,
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

	/// <summary>Feat, Road, Secret or Collection (.ai/achievements-and-secrets.md section 2).</summary>
	public AchievementKind Kind { get; init; } = AchievementKind.Feat;

	/// <summary>
	/// The chapter it belongs to, a StageCatalog id: a Road deed's chapter, a secret's. Empty for a Feat
	/// or a Collection, which may be done anywhere. Groups the achievements page by chapter, and is what
	/// RegressionChecks.ValidateGateReachability reads to prove a gate counts nothing behind itself.
	/// </summary>
	public string StageId { get; init; } = string.Empty;

	/// <summary>When it is shown (<see cref="AchievementDefinitions.IsListed"/>).</summary>
	public AchievementVisibility Visibility { get; init; } = AchievementVisibility.Always;

	/// <summary>
	/// True for side events and secrets: completed the moment it happens, mid-run, and saved on the spot
	/// (<see cref="AchievementManager.CompleteNow"/>), not measured at the end of the run.
	/// </summary>
	public bool CompletesInRun { get; init; }

	/// <summary>For a side event's deed: the site it mirrors (SideEventDirector.ForStage). Empty otherwise.</summary>
	public string SiteId { get; init; } = string.Empty;

	/// <summary>For a secret's deed: the <see cref="SecretDefinition"/> it mirrors. Empty otherwise.</summary>
	public string SecretId { get; init; } = string.Empty;

	/// <summary>
	/// A Hidden deed's words once seen (.ai/achievements-and-secrets.md 4.3): what the player saw, as an
	/// observation. Taken from the secret, so the page invents nothing.
	/// </summary>
	public string SeenLine { get; init; } = string.Empty;

	/// <summary>
	/// What may be said before it is seen or open: a Hidden deed's chapter ("Hidden somewhere in the
	/// Sunken Cave" - also the Spell Index's line for a vault's spell), or, for a deed that frees a secret
	/// wizard, "The Cursed Dungeon holds someone". Empty for every deed that is never sealed.
	/// </summary>
	public string SealedLine { get; init; } = string.Empty;

	/// <summary>
	/// For a chapter clear, the boss it records; empty for every other deed. It is what lets the
	/// deeds list follow the campaign order and keep a chapter that is not built yet off the list
	/// (<see cref="AchievementDefinitions.Listed"/>).
	/// </summary>
	public string BossId { get; init; } = string.Empty;

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
	/// <summary>
	/// How many Black Hares a hound must run down in one run of the Broken Circle to free Laelaps (the
	/// "coursing" deed). Measured in real time on the Circle, opening on the hound (seed 11, 15 minutes):
	/// the --survivor bot, which spread its picks over six spells and never levelled the hound, coursed 116
	/// of 344 hares - a third of them, the rest falling to its other spells; the same bot taking only the
	/// Familiar coursed 436 of 444. A hundred is most of a run with the hound beside other spells, or a few
	/// minutes of one built round it - a deed, not a side effect of taking the dog. (Sixty, first set from
	/// the fixed-fps bench, which understates the hound, came in a short opening.)
	/// </summary>
	public const int CoursingTarget = 100;

	/// <summary>
	/// A side event's deed is "event_" and its site id; a secret's is "secret_" and the secret's id. Made
	/// from the id rather than written by hand, so a deed can never drift from what it mirrors, and
	/// prefixed so neither can ever collide with a hand-written deed. Constants, so UnlockCatalog can
	/// name a deed while this class is still building the list that reads it.
	/// </summary>
	public const string EventDeedPrefix = "event_";
	public const string SecretDeedPrefix = "secret_";

	public static string EventDeedId(string siteId) => EventDeedPrefix + (siteId ?? string.Empty).Trim();

	public static string SecretDeedId(string secretId) => SecretDeedPrefix + (secretId ?? string.Empty).Trim();

	/// <summary>The Grimoire Restored: every spell recovered, now the collection rather than the final gate's key.</summary>
	public const string GrimoireRestoredId = "grimoire_restored";

	/// <summary>
	/// What each side event's deed is called and says (.ai/achievements-and-secrets.md 2.1 and 2.3). The
	/// reward line names what UnlockCatalog lists at the site; RegressionChecks holds the two together.
	/// The Cursed Dungeon's deed is "The Open Cell", not the doc's "Tituba Freed": a rescued wizard's name
	/// is his reward and stays hidden while he is locked (.ai/side-events.md, "Secret wizards"), and the
	/// deed is listed from the day the Dungeon opens.
	/// </summary>
	private static readonly (string Site, string Name, string Description, string Reward)[] SideEventWords =
	{
		("the_grove", "The Grove Held", "Hold the corrupted grove in the Enchanted Forest until it grows back.", "Unlocks Bramble Seed"),
		("the_well", "The Last Coin", "Keep the last clean well in the Fallow Fields from being fouled.", "Unlocks Wishing Coin"),
		("the_moot", "The Circle Relit", "Relight the runes of the stone circle on the Broken Circle.", "Unlocks Gilded Mote"),
		("gaolers_key", "The Open Cell", "Carry the Gaoler's key to the locked cell in the Cursed Dungeon.", "Frees Tituba"),
		("bog_lanterns", "The Lanterns Out", "Snuff the lanterns of the Blighted Swamp and face what tends them.", "Unlocks Contagion"),
		("the_thaw", "Out of the Ice", "Thaw the wizard out of the ice in the Frozen Waste.", "Frees Väinämöinen"),
		("caravan_master", "The Caravan Stopped", "Catch the caravan master on the Scorched Sands road.", "Unlocks Iron Palisade"),
	};

	private const float TenMinutes = 600f;
	private const float ThreeMinutes = 180f;

	/// <summary>
	/// Every deed: the hand-written ones - Feats, the chapters' boss clears, the Collection deeds - with
	/// the clears standing in campaign order (<see cref="InCampaignOrder"/>), then one Road deed derived
	/// from every side event's site and one Secret deed derived from every secret, each in campaign order
	/// (.ai/achievements-and-secrets.md 2.6).
	/// </summary>
	public static readonly IReadOnlyList<AchievementDefinition> All = InCampaignOrder(new List<AchievementDefinition>
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
			// Hollow Star since schema 19 (it was sold): Darkness's only pure carrier stays early. Frost
			// Shard, which this paid, is the Geode Heart's - Eric's giant crystal (SecretCatalog).
			Id = "survivor", DisplayName = "Survivor",
			Description = "Survive for 10 minutes in a single run.",
			RewardText = "Unlocks Hollow Star", SpellUnlockId = "hollow_star",
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
			// Easier since 2026-10-28 ("umbral veil should be easier to unlock - instead, not taking
			// damage for less time (getting shield hit wouldn't count for example)"): three minutes,
			// not five, and only a hit that costs HP ends it (Player.TakeDamage records it).
			Id = "untouchable", DisplayName = "Untouchable",
			Description = "Survive 3 minutes without losing health. A hit your shield takes does not count.",
			RewardText = "Unlocks Umbral Veil", BoonUnlockId = "umbral_veil",
			Category = AchievementCategory.Challenge,
			Measure = c => c.Lifetime.BestUndamagedSeconds,
			Target = ThreeMinutes, FormatValue = AchievementDefinition.FormatTime,
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
		BossClear("forest_cleared", "Forest Cleared", "forest_treant", "the Enchanted Forest", "thorn_vine", "Thorned Vine"),
		BossClear("castle_conqueror", "Dungeon Conqueror", "castle_warden", "the Cursed Dungeon", "shadow_bolt", "Shadow Bolt"),
		BossClear("cave_choir_silenced", "Silence in the Deep", "cave_choir", "the Sunken Cave", "riptide", "Riptide"),
		BossClear("swamp_cleansed", "Cut the Rot Out", "swamp_mother", "the Blighted Swamp", "mirefoot", "Mirefoot"),
		BossClear("ruins_delver", "Ruins Delver", "ruins_sentinel", "the Mystic Ruins", "spiritual_weapon", "Spiritual Weapon"),
		BossClearBoon("waste_thawed", "Broke the Guard", "frost_warden", "the Frozen Waste", "rimebriar", "Rimebriar"),
		// Two things since schema 19: the boon it always paid, and Fireball, moved here from the Warden of
		// the Deep's deed - a spell only the far side of the old final gate paid, while that gate counted
		// every spell, so no save written since schema 8 could ever open the Emberdeep
		// (.ai/achievements-and-secrets.md 1.4). The Sands are the latest chapter before the gate.
		new()
		{
			Id = "sands_unwound", DisplayName = "Unwound",
			Description = "Defeat the boss of the Scorched Sands.",
			RewardText = "Unlocks Fireball and Sunsteel Filament",
			SpellUnlockId = "fireball", BoonUnlockId = "sunsteel_filament",
			Category = AchievementCategory.Exploration,
			Kind = AchievementKind.Road,
			Visibility = AchievementVisibility.ChapterOpen,
			StageId = StageOfBoss("sands_coil"),
			BossId = "sands_coil",
			Measure = c => c.Lifetime.HasDefeatedBoss("sands_coil") ? 1f : 0f,
			Target = 1,
		},
		// The gate chapter's own deed pays Arcane Energy since schema 19 - its Fireball moved to the Long
		// Coil - and opens the castle (.ai/endgame-castle.md) once it is built. A save that had done it
		// keeps its Fireball and is not paid the 300: reconciling pays unlocks, never currency.
		new()
		{
			Id = "emberdeep_ended", DisplayName = "The Long Road Back",
			Description = "Defeat the boss of the Emberdeep.",
			RewardText = "300 Arcane Energy", CurrencyReward = 300,
			Category = AchievementCategory.Exploration,
			Kind = AchievementKind.Road,
			Visibility = AchievementVisibility.ChapterOpen,
			StageId = StageOfBoss("emberdeep_warden"),
			BossId = "emberdeep_warden",
			Measure = c => c.Lifetime.HasDefeatedBoss("emberdeep_warden") ? 1f : 0f,
			Target = 1,
		},
		// The two early chapters (.ai/campaign.md) pay in boons, for the reason the Frozen Waste and
		// the Sands do. Both boons were sold-but-never-sold (the shop registers no boons), so until then
		// no new save could ever be dealt them. Listed only once their chapter is playable.
		BossClearBoon("fields_hurdled", "Round the Hurdle", "fields_hayward", "the Fallow Fields", "thornseed", "Thornseed"),
		BossClearBoon("circle_unclosed", "Out of the Ring", "heath_ninth_stone", "the Broken Circle", "shadegrease", "Shadegrease"),
		// Pays in currency rather than content, and that is the point: the Sketchbook is a bonus
		// chapter, so putting a spell or a wizard behind it would make an optional chapter
		// mandatory for anyone completing the campaign.
		new()
		{
			Id = "sketchbook_cleared", DisplayName = "Best Drawing",
			Description = "Defeat Wizard Dude in the Sketchbook.",
			RewardText = "400 Arcane Energy", CurrencyReward = 400,
			Category = AchievementCategory.Exploration,
			Kind = AchievementKind.Road,
			Visibility = AchievementVisibility.ChapterOpen,
			StageId = StageOfBoss("kid_wizard"),
			// Its boss, like every other chapter clear's, so StageRewards finds what the Sketchbook pays
			// by the same rule (.ai/world-map.md, section 9). Outside the campaign, so InCampaignOrder
			// keeps it last among the clears, which is where it already stood.
			BossId = "kid_wizard",
			Measure = c => c.Lifetime.HasDefeatedBoss("kid_wizard") ? 1f : 0f,
			Target = 1,
		},
		new()
		{
			Id = "relic_hunter", DisplayName = "Relic Hunter",
			Description = "Collect 12 different relics.",
			RewardText = "250 Arcane Energy", CurrencyReward = 250,
			Category = AchievementCategory.Exploration,
			Kind = AchievementKind.Collection,
			Measure = c => c.Lifetime.ChestItemsCollected.Count, Target = 12,
		},
		// The collector's wizard, freed by collecting. Thirty is two runs that reach the boss on the
		// chest economy where elites carry them (about fifteen a run - .ai/passives-and-items.md,
		// section 8), and lifetime chests are a stat every save has kept since the campaign began,
		// so nothing new is recorded and a long-time player is handed him at the end of the next run.
		// A Feat: it names no chapter, so every gate can count him (GlobalStatsManager.GateWizardIds).
		new()
		{
			Id = "mortlake_library", DisplayName = "The Library at Mortlake",
			Description = "Open 30 chests.",
			RewardText = "Frees John Dee", CharacterUnlockId = "john_dee",
			Category = AchievementCategory.Exploration,
			Measure = c => c.Lifetime.TotalChestsOpened, Target = 30,
		},
		// THE COURSING. Laelaps, the hound fated to catch whatever it hunted, was set in stone on the high heath
		// mid-chase, and the black hares of the Broken Circle are his quarry; a hound of the player's own
		// light finishing the chase frees him. Only a kill by the Familiar taken as a dog counts
		// (Familiar.Strike), and only a Black Hare - the Circle's fast body and the Warrener's summons - so
		// the deed names one map, one form and one quarry. Measured over one run (RunEvents, then the
		// lifetime best), so the menu shows the best run so far and the pause screen this run's count.
		// A Road deed of the Circle: it names the chapter, and frees a wizard secret until the Circle opens.
		new()
		{
			Id = "coursing", DisplayName = "The Coursing",
			Description = $"In the Broken Circle, let your Familiar, taken as a hound, run down {CoursingTarget} Black Hares in one run.",
			RewardText = "Frees Laelaps", CharacterUnlockId = "laelaps",
			Category = AchievementCategory.Challenge,
			Kind = AchievementKind.Road,
			Visibility = AchievementVisibility.ChapterOpen,
			StageId = "stage_10",
			SealedLine = SomeoneHeldIn("stage_10"),
			Measure = c => Math.Max(c.Lifetime.BestHaresCoursed, c.Run?.HaresCoursed ?? 0),
			Target = CoursingTarget,
		},
		new()
		{
			Id = "set_collector", DisplayName = "Attuned",
			Description = "Complete 3 different relic sets.",
			RewardText = "250 Arcane Energy", CurrencyReward = 250,
			Category = AchievementCategory.Exploration,
			Kind = AchievementKind.Collection,
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
		// THE GRIMOIRE RESTORED (.ai/achievements-and-secrets.md 2.5, decision 1). Every spell recovered was
		// the final gate's key until schema 19; the gate counts wizards now, and the spells are the
		// collection. Measured live against the catalog, so a spell added later raises the target for those
		// still collecting - and needs no migration, which a gate counting spells did every time.
		new()
		{
			Id = GrimoireRestoredId, DisplayName = "The Grimoire Restored",
			Description = "Recover every spell.",
			RewardText = "500 Arcane Energy", CurrencyReward = 500,
			Category = AchievementCategory.Exploration,
			Kind = AchievementKind.Collection,
			Measure = c => GlobalStatsManager.GetUnlockedSpellCount(c.Save),
			Target = UnlockCatalog.AllSpellIds.Count(),
		},
	}).Concat(SideEventDeeds()).Concat(SecretDeeds()).ToList();

	/// <summary>
	/// One Road deed for every chapter's side event (SideEventDirector.ForStage), in campaign order. Its
	/// reward is whatever UnlockCatalog lists at the site; its words are <see cref="SideEventWords"/>.
	/// Completed the moment the event is won (DiscoveryRewards), measured by LifetimeStats.EventsWon.
	/// </summary>
	private static IEnumerable<AchievementDefinition> SideEventDeeds()
	{
		foreach (StageDefinition stage in StageCatalog.InDisplayOrder)
		{
			string site = global::SideEventDirector.ForStage(stage.Id)?.SiteId;
			if (string.IsNullOrWhiteSpace(site))
				continue;
			string siteId = site;
			(string Site, string Name, string Description, string Reward) words =
				SideEventWords.FirstOrDefault(w => w.Site.Equals(siteId, StringComparison.OrdinalIgnoreCase));
			List<UnlockDefinition> pays = UnlockCatalog.ForDiscoverySite(siteId).ToList();
			string wizard = pays.FirstOrDefault(d => d.Kind == UnlockKind.Character)?.Id ?? string.Empty;
			yield return new AchievementDefinition
			{
				Id = EventDeedId(siteId),
				// RegressionChecks.ValidateAchievements reports a site with no words; the id stands in.
				DisplayName = words.Name ?? siteId,
				Description = words.Description ?? string.Empty,
				RewardText = words.Reward ?? string.Empty,
				SpellUnlockId = pays.FirstOrDefault(d => d.Kind == UnlockKind.Spell)?.Id ?? string.Empty,
				BoonUnlockId = pays.FirstOrDefault(d => d.Kind == UnlockKind.Boon)?.Id ?? string.Empty,
				CharacterUnlockId = wizard,
				Category = AchievementCategory.Exploration,
				Kind = AchievementKind.Road,
				Visibility = AchievementVisibility.ChapterOpen,
				CompletesInRun = true,
				StageId = stage.Id,
				SiteId = siteId,
				SealedLine = UnlockCatalog.GetCharacter(wizard)?.IsSecret == true ? SomeoneHeldIn(stage.Id) : string.Empty,
				Measure = c => c.Lifetime.HasWonEvent(siteId) ? 1f : 0f,
				Target = 1,
			};
		}
	}

	/// <summary>Whether a side event's site has words written for its deed. For the validator.</summary>
	public static bool HasSideEventWords(string siteId) =>
		SideEventWords.Any(w => w.Site.Equals(siteId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

	/// <summary>The site ids the words table names, for the validator's other direction.</summary>
	public static IEnumerable<string> SideEventWordSites => SideEventWords.Select(w => w.Site);

	/// <summary>One Secret deed for every secret written down, built or planned, in campaign order.</summary>
	private static IEnumerable<AchievementDefinition> SecretDeeds() =>
		SecretCatalog.Defined
			.OrderBy(s => CampaignRankOfStage(s.StageId))
			.Select(SecretCatalog.AsAchievement);

	/// <summary>
	/// The deeds a save-less caller may show: all of them but a deed for a chapter not built yet, a
	/// Hidden one, and one that frees a secret wizard - those need the save to know whether they have
	/// been seen or revealed (<see cref="IsListed"/>). The old Unlocks list reads this until the
	/// achievements page (piece A2) reads <see cref="ListedFor"/>. Evaluation still walks <see cref="All"/>.
	/// </summary>
	public static IEnumerable<AchievementDefinition> Listed => ListedFor(null);

	/// <summary>The deeds this save is shown in full (<see cref="IsListed"/>).</summary>
	public static IEnumerable<AchievementDefinition> ListedFor(SaveData save) => All.Where(d => IsListed(d, save));

	/// <summary>
	/// Whether a deed is shown in full on this save (.ai/achievements-and-secrets.md 4.2):
	///   - Always: yes;
	///   - ChapterOpen: once its chapter is playable, and a deed that frees a secret wizard only once he is
	///     revealed (his chapter open, or him already free) - before that it is a sealed row (<see cref="IsSealed"/>);
	///   - Hidden: once the secret is built and has been seen or found.
	/// A null save reveals nothing secret.
	/// </summary>
	public static bool IsListed(AchievementDefinition deed, SaveData save)
	{
		if (deed == null || !IsChapterBuilt(deed))
			return false;
		switch (deed.Visibility)
		{
			case AchievementVisibility.Hidden:
				return save != null && SecretCatalog.Get(deed.SecretId)?.IsBuilt == true
					&& (save.HasGlimpsedSecret(deed.SecretId) || save.HasFoundSecret(deed.SecretId)
						|| GlobalStatsManager.IsAchievementUnlocked(save, deed.Id));
			case AchievementVisibility.ChapterOpen:
				return !IsSealed(deed, save);
			default:
				return true;
		}
	}

	/// <summary>
	/// A deed that frees a secret wizard whose chapter has not opened: shown as a sealed row with its
	/// <see cref="AchievementDefinition.SealedLine"/>, never named (the character select's rule).
	/// </summary>
	public static bool IsSealed(AchievementDefinition deed, SaveData save)
	{
		if (deed == null || string.IsNullOrWhiteSpace(deed.CharacterUnlockId))
			return false;
		UnlockDefinition wizard = UnlockCatalog.GetCharacter(deed.CharacterUnlockId);
		return wizard?.IsSecret == true && !GlobalStatsManager.IsCharacterRevealed(save, wizard.Id);
	}

	/// <summary>The deeds that belong to a chapter, in the order of <see cref="All"/>.</summary>
	public static IEnumerable<AchievementDefinition> ForStage(string stageId) =>
		All.Where(d => d.StageId.Equals(stageId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

	/// <summary>Kept for the old callers: listed with no save in hand.</summary>
	public static bool IsListed(AchievementDefinition deed) => IsListed(deed, null);

	// A deed whose chapter is not built yet is a deed nobody can do: not listed, still evaluated.
	private static bool IsChapterBuilt(AchievementDefinition deed)
	{
		if (!string.IsNullOrWhiteSpace(deed.BossId))
		{
			BossDefinition boss = BossCatalog.GetById(deed.BossId);
			int index = boss != null ? BossCatalog.StageIndexOf(boss) : -1;
			return index < 0 || StageCatalog.GetByIndex(index).IsPlayable;
		}
		StageDefinition stage = StageCatalog.GetById(deed.StageId);
		return stage == null || stage.IsPlayable;
	}

	/// <summary>"the Sunken Cave", "the Fallow Fields": a chapter's name mid-sentence, with one article.</summary>
	public static string ChapterPhrase(string stageId)
	{
		string name = StageCatalog.DisplayNameFor(stageId) ?? string.Empty;
		return name.StartsWith("The ", StringComparison.Ordinal) ? "the " + name.Substring(4) : "the " + name;
	}

	// "The Cursed Dungeon holds someone" (.ai/achievements-and-secrets.md 4.2): a sealed row's line.
	private static string SomeoneHeldIn(string stageId)
	{
		string phrase = ChapterPhrase(stageId);
		return phrase.Length > 0 ? char.ToUpperInvariant(phrase[0]) + phrase.Substring(1) + " holds someone" : string.Empty;
	}

	// The chapter clears are written in the order the chapters were made, and the deeds page lists
	// them as written. Moving them into campaign order here - in the slots they already hold, so no
	// other deed moves - is what keeps that page following the campaign when the order changes,
	// rather than a second copy of the order kept by hand in the list above.
	private static List<AchievementDefinition> InCampaignOrder(List<AchievementDefinition> deeds)
	{
		var slots = new List<int>();
		for (int i = 0; i < deeds.Count; i++)
		{
			if (!string.IsNullOrWhiteSpace(deeds[i].BossId))
				slots.Add(i);
		}
		var ordered = slots.Select(i => deeds[i])
			.OrderBy(d => CampaignRank(d.BossId))
			.ToList();
		for (int k = 0; k < slots.Count; k++)
			deeds[slots[k]] = ordered[k];
		return deeds;
	}

	private static int CampaignRank(string bossId)
	{
		BossDefinition boss = BossCatalog.GetById(bossId);
		int index = boss != null ? BossCatalog.StageIndexOf(boss) : -1;
		return index >= 0 ? CampaignRankOfStage(StageCatalog.GetByIndex(index).Id) : int.MaxValue;
	}

	private static int CampaignRankOfStage(string stageId)
	{
		int position = StageCatalog.CampaignPositionOf(stageId);
		return position >= 0 ? position : int.MaxValue;
	}

	// The chapter a boss's deed belongs to: the boss's own (BossCatalog), as an id.
	private static string StageOfBoss(string bossId)
	{
		BossDefinition boss = BossCatalog.GetById(bossId);
		int index = boss != null ? BossCatalog.StageIndexOf(boss) : -1;
		return index >= 0 ? StageCatalog.GetByIndex(index).Id : string.Empty;
	}

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
		Kind = AchievementKind.Road,
		Visibility = AchievementVisibility.ChapterOpen,
		StageId = StageOfBoss(bossId),
		BossId = bossId,
		Measure = c => c.Lifetime.HasDefeatedBoss(bossId) ? 1f : 0f,
		Target = 1,
	};

	// Two chapter clears pay in a boon rather than a spell. Not an oversight and not filler: the
	// shop's stock is the same finite pool the achievement track draws from, and moving all seven
	// boss rewards onto spells would have left four spells in it. A boon is a real reward and the
	// Frozen Waste handing over Rimebriar is a better fit than any spell left unclaimed was.
	private static AchievementDefinition BossClearBoon(string id, string name, string bossId, string place, string boonId, string boonName) => new()
	{
		Id = id,
		DisplayName = name,
		Description = $"Defeat the boss of {place}.",
		RewardText = $"Unlocks {boonName}",
		BoonUnlockId = boonId,
		Category = AchievementCategory.Exploration,
		Kind = AchievementKind.Road,
		Visibility = AchievementVisibility.ChapterOpen,
		StageId = StageOfBoss(bossId),
		BossId = bossId,
		Measure = c => c.Lifetime.HasDefeatedBoss(bossId) ? 1f : 0f,
		Target = 1,
	};

	public static AchievementDefinition GetById(string id) =>
		string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(a => a.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
}
