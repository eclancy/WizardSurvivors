using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>How a thing is obtained. The extension point for new unlock mechanics.</summary>
public enum UnlockSource
{
	/// <summary>Available from the first run. Never gated.</summary>
	Starter,
	/// <summary>
	/// Granted when the achievement named by <see cref="UnlockDefinition.SourceId"/> completes. Since
	/// schema 19 (.ai/achievements-and-secrets.md) every spell that is not a starter comes this way - a
	/// Feat, a chapter's Road deed, or a secret found in the world - and so does every wizard.
	/// </summary>
	Achievement,
	/// <summary>Bought with Arcane Energy in the main menu shop. Boons only since schema 19: the shop sells no spells.</summary>
	Purchase,
	/// <summary>
	/// RETIRED at schema 19: no entry uses it. A side event's unlock is now the reward of that event's
	/// Road deed (<see cref="UnlockSource.Achievement"/>), and the site survives only as the deed's
	/// trigger, kept on <see cref="UnlockDefinition.SiteId"/>. Left in the enum so the code that still
	/// names it compiles, and so <c>RegressionChecks.ValidateUnlockCatalog</c> can refuse it by name.
	/// </summary>
	Discovery,
}

public enum UnlockKind
{
	Spell,
	Character,

	/// <summary>
	/// A boon: permanent, never levelled, and held in its own six slots beside the spells.
	/// </summary>
	Boon,
}

/// <summary>One unlockable thing and the single way it is obtained.</summary>
public sealed class UnlockDefinition
{
	/// <summary>Spell id or character id, matching the id on the SpellData / CharacterData resource.</summary>
	public string Id { get; init; } = string.Empty;
	public UnlockKind Kind { get; init; }
	public UnlockSource Source { get; init; }

	/// <summary>The achievement id that grants it. Empty for Starter and Purchase.</summary>
	public string SourceId { get; init; } = string.Empty;

	/// <summary>
	/// For something a chapter's side event pays: the event's site id, as <c>SideEventDirector.ForStage</c>
	/// maps it. Its <see cref="SourceId"/> is that event's Road deed
	/// (<see cref="AchievementDefinitions.EventDeedId"/>), and the deed reads its reward from here - so what
	/// a site pays is written once, on this entry, and nowhere else. Empty for anything else.
	/// </summary>
	public string SiteId { get; init; } = string.Empty;

	/// <summary>Arcane Energy price. Only read when <see cref="Source"/> is Purchase.</summary>
	public int PurchaseCost { get; init; }

	/// <summary>
	/// One line telling the player how to get this, shown on the locked card. Never leave it empty:
	/// a lock the player cannot read is indistinguishable from content that is simply missing.
	/// </summary>
	public string LockedHint { get; init; } = string.Empty;

	/// <summary>
	/// A secret is not even shown locked until its reveal condition is met: its card is a sealed
	/// blank - no silhouette, no name, no <see cref="LockedHint"/> - and only then becomes an
	/// ordinary locked card. Characters only; the character select is the one screen that reads it.
	/// </summary>
	public bool IsSecret { get; init; }

	/// <summary>
	/// What reveals a secret: the chapter with this id becoming enterable
	/// (<c>GlobalStatsManager.IsStageAvailable</c>). Derived from the stages the save has opened,
	/// so being revealed costs no save field. Empty on anything that is not secret.
	/// </summary>
	public string RevealStageId { get; init; } = string.Empty;

	/// <summary>
	/// A character who is not one of the imprisoned WIZARDS, so the final chapter's gate - "the previous
	/// boss beaten and every wizard freed" - does not count him (<see cref="UnlockCatalog.AllWizardIds"/>).
	/// Laelaps, the hound: freeing him is an optional deed in one chapter, and counting him would make
	/// that deed mandatory for the finale and close the Emberdeep, live, to every save that had opened it
	/// - the reason schemas 10, 12, 14, 15 and 16 had to grant what they added. Characters only.
	/// </summary>
	public bool OutsideCampaignGate { get; init; }
}

/// <summary>
/// The single authority on how every spell and character is obtained.
/// </summary>
/// <remarks>
/// This replaces a scattering of implicit rules that had quietly cancelled each other out. Every
/// spell used to sit in <c>GlobalStatsManager.DefaultUnlockedSpellIds</c>, which meant all fifteen
/// achievement rewards and all twenty-two shop entries granted something the player already had —
/// the entire unlock economy was inert, and nothing in the build or the validators noticed, because
/// no one place knew what the rules were supposed to be. Now there is one.
///
/// The catalog is deliberately exhaustive rather than a set of exceptions:
/// <see cref="RegressionChecks"/> asserts that every spell in the catalog and every character in
/// the roster has exactly one entry here, so a new spell that nobody assigned a source to fails
/// validation instead of silently becoming unobtainable.
/// </remarks>
public static class UnlockCatalog
{
	// The opening hand. Four signature spells - one per starting character - plus Magic Missile,
	// plus three passives chosen so no early build is without an answer: a shield, flat armour and
	// evasion. Eight candidates against three level-up slots is roughly a one-in-three chance a
	// given spell is offered; the old all-35 pool was one in twelve, which is why builds used to
	// feel like something that happened to the player rather than something they chose.
	//
	// The four used to be purpose-built basics (Ember, Icicle, Spark, Stone Shard), each a plain
	// bolt that flew at the nearest enemy. Four spells that differed only in colour made all four
	// wizards open the same way, so they were deleted and the starting slots handed to spells that
	// already do something: a lobbed explosion, a cone, a chaining arc, a ground eruption. That
	// pulled these four out of the achievement rewards, which is why the block below hands those
	// four achievements spells promoted from the shop instead.
	private static readonly UnlockDefinition[] Definitions =
	{
		// The Fire opener is a flamethrower rather than a lobbed bolt. A cone you hold while the
		// crowd closes is a different thing to be good at than a shot you line up, and it is the
		// only starter whose damage is applied by presence rather than by a hit.
		Starter("cinderbreath", UnlockKind.Spell),
		Starter("cone_of_cold", UnlockKind.Spell),
		Starter("chain_lightning", UnlockKind.Spell),
		Starter("obsidian_spike", UnlockKind.Spell),
		Starter("magic_missile", UnlockKind.Spell),
		// The one defensive spell. Passive spells went, but a shield is an active thing with a
		// cooldown and a break, not a number that sits on the player - so it stayed a spell and
		// costs a slot like any other.
		Starter("aegis_ward", UnlockKind.Spell),

		// The opening boons. One each for Metal, Water, Light and Grass - the four elements that
		// lose most of their carriers when passive spells go, so the player can reach them from the
		// first level-up rather than only after a shop trip.
		Starter("iron_rivets", UnlockKind.Boon),
		Starter("tidewater_flask", UnlockKind.Boon),
		Starter("lantern_oil", UnlockKind.Boon),
		Starter("mossgrown_charm", UnlockKind.Boon),
		// The stand-still boon (Taproot), a starter so the mechanic it shares with the Brazier set is
		// in every run's pool from the first level-up rather than behind three chests. (The shop sold
		// no boons until 2026-10-26, which is why the PurchaseBoon entries below were cards no new save
		// could be dealt; MainMenu's Arcane Upgrades now has a boon shelf, PurchasableBoons.)
		Starter("taproot", UnlockKind.Boon),

		// Achievement rewards. These ids must match AchievementDefinitions.All, which
		// ValidateUnlockCatalog checks in both directions - an achievement granting a spell that
		// claims a different source, or a spell pointing at an achievement that does not exist,
		// are both the same silent dead end the old economy was made of.
		//
		// EVERY SPELL THAT IS NOT A STARTER IS EARNED (Eric, 2026-10-29; .ai/achievements-and-secrets.md):
		// eleven Feats, nine Road deeds of the chapters, and nine spell vaults hidden in the world. The
		// shop sells no spells. What moved at schema 19, and why, is that doc's section 2.2.
		FromAchievement("arcane_explosion", "first_blood", "Defeat your first enemy."),
		// Darkness's only pure carrier, sold until schema 19 so the element could be built early. Ten
		// minutes is the first good run, so it stays early as a Feat. Survivor paid Frost Shard before;
		// Frost Shard is the Geode Heart's now (below).
		FromAchievement("hollow_star", "survivor", "Survive ten minutes in a single run."),
		FromAchievement("meteor_swarm", "veteran", "Clear a chapter without falling."),
		FromAchievement("solar_flare", "max_level", "Reach level 20 in a single run."),
		FromAchievement("cyclone_slash", "elementalist", "Reach 4 instances of any element."),
		FromAchievement("void_lance", "hoarder", "Fill all six spell slots in a run."),
		FromAchievementBoon("umbral_veil", "untouchable", "Survive three minutes without losing health."),
		FromAchievement("scorching_ray", "fire_adept", "End a run with 4 Fire instances."),
		FromAchievement("glacial_spike", "ice_adept", "End a run with 4 Ice instances."),
		FromAchievement("toxic_spore_burst", "poison_adept", "End a run with 4 Poison instances."),
		FromAchievement("black_tentacles", "earth_adept", "End a run with 4 Earth instances."),
		FromAchievement("gale_blade", "wind_adept", "End a run with 4 Wind instances."),
		FromAchievement("thorn_vine", "forest_cleared", "Defeat the boss of the Enchanted Forest."),
		FromAchievement("shadow_bolt", "castle_conqueror", "Defeat the boss of the Cursed Dungeon."),
		FromAchievement("spiritual_weapon", "ruins_delver", "Defeat the boss of the Mystic Ruins."),
		FromAchievement("riptide", "cave_choir_silenced", "Defeat the boss of the Sunken Cave."),
		FromAchievement("mirefoot", "swamp_cleansed", "Defeat the boss of the Blighted Swamp."),
		FromAchievementBoon("rimebriar", "waste_thawed", "Defeat the boss of the Frozen Waste."),
		// The Long Coil's deed pays two things: the boon it always paid, and Fireball. Fireball was the
		// Warden of the Deep's, in the Emberdeep - behind the old final gate, which counted every spell,
		// so no save written since schema 8 could ever open the Emberdeep (.ai/achievements-and-secrets.md
		// 1.4). The big slow blast is a late shape, and chapter 9 is the latest place before the gate.
		FromAchievement("fireball", "sands_unwound", "Defeat the boss of the Scorched Sands."),
		FromAchievementBoon("sunsteel_filament", "sands_unwound", "Defeat the boss of the Scorched Sands."),
		// The two early chapters (.ai/campaign.md). Boons, not spells: chosen when the final gate still
		// counted every spell (it counts wizards now), and kept, since a boon is what those chapters'
		// elements were short of. Both were PurchaseBoon entries that no screen sold - cards no new save
		// could ever be dealt - and they have a source at last.
		FromAchievementBoon("thornseed", "fields_hurdled", "Defeat the boss of the Fallow Fields."),
		FromAchievementBoon("shadegrease", "circle_unclosed", "Defeat the boss of the Broken Circle."),

		// THE SPELL VAULTS (.ai/achievements-and-secrets.md 6.4): one secret in each chapter before the
		// gate that pays a spell, once ever, through its derived deed (SecretCatalog.AsAchievement). Eight
		// of the nine were sold in the shop until schema 19, and every copy anyone bought stays bought;
		// Frost Shard was Survivor's, and is Eric's giant crystal's now. The hint names the chapter and
		// never the place or the verb (decision 5) - it is the Spell Index's line for a locked spell, and
		// RegressionChecks holds it to the deed's SealedLine.
		FromSecret("hunters_draw", "pale_hart", "Hidden somewhere in the Enchanted Forest."),
		FromSecret("familiar", "pinfold", "Hidden somewhere in the Fallow Fields."),
		FromSecret("starfall", "fallen_star", "Hidden somewhere in the Broken Circle."),
		FromSecret("frost_shard", "geode_heart", "Hidden somewhere in the Sunken Cave."),
		FromSecret("molten_shard", "cold_forge", "Hidden somewhere in the Cursed Dungeon."),
		FromSecret("gravewell", "swallow_hole", "Hidden somewhere in the Blighted Swamp."),
		FromSecret("kindled_ward", "broken_ward", "Hidden somewhere in the Mystic Ruins."),
		FromSecret("sleet_runes", "buried_runes", "Hidden somewhere in the Frozen Waste."),
		FromSecret("stormsweep", "fulgurite", "Hidden somewhere in the Scorched Sands."),

		// Boons fill the shop that passive spells used to. Priced by how much a run changes when
		// one turns up: a stat nudge is cheap, a whole defensive layer is not. The three boons are
		// all the shop still sells of the catalog (piece A3 decides where they sit).
		PurchaseBoon("quicksilver_bead", 110),
		PurchaseBoon("deepwater_pearl", 120),
		PurchaseBoon("saltbound_chain", 140),
		// Thornseed, Shadegrease, Gilded Mote and Wishing Coin were listed here too, when the shop sold
		// no boons; they are the early chapters' rewards now (above, and the discoveries below).
		// The two reactive boons are priced above the stat boons because they change how a fight
		// plays rather than how long it lasts - standing in the crowd becomes a thing you do on
		// purpose rather than a thing you survive.

		// The four starting wizards. Listed rather than assumed, so the roster and the catalog
		// cannot disagree about who the player begins with.
		Starter("pyromancer", UnlockKind.Character),
		Starter("frostweaver", UnlockKind.Character),
		Starter("stormcaller", UnlockKind.Character),
		Starter("geomancer", UnlockKind.Character),
		Starter("test_wizard", UnlockKind.Character),

		// FOUND IN THE WORLD (.ai/side-events.md). Each chapter's side event is a place where the
		// dark wizard keeps something he took; winning it carries the thing out. The site id is the
		// event's, as SideEventDirector maps it. These were the Discovery entries until schema 19; each
		// is now the reward of its event's Road deed (AchievementDefinitions.EventDeedId), which is
		// derived from this site - so the deeds list shows every one of them, which it never did - and
		// is still granted the moment the event is won (DiscoveryRewards -> AchievementManager.CompleteNow).
		//
		// The two rescued wizards are SECRET until the chapter that holds them opens. Before that
		// the player has no way to know a prisoner exists, so a silhouette and a hint naming a place
		// they cannot enter would be a spoiler with nothing to act on. The reveal is the beat the
		// chapter card already plays - "He keeps a wizard in one of these cells." - which is shown
		// the moment the chapter opens; the character select now agrees with it.
		// RegressionChecks.ValidateUnlockCatalog holds the reveal chapter to the one whose side
		// event pays the wizard out.
		FromEvent("tituba", UnlockKind.Character, "gaolers_key", "Freed from a cell in the Cursed Dungeon.", secretUntilStage: "stage_1"),
		FromEvent("vainamoinen", UnlockKind.Character, "the_thaw", "Thawed out of the ice in the Frozen Waste.", secretUntilStage: "stage_5"),
		FromEvent("iron_palisade", UnlockKind.Spell, "caravan_master", "Carried by the caravan master on the Scorched Sands road."),
		FromEvent("contagion", UnlockKind.Spell, "bog_lanterns", "Kept by whatever tends the lanterns in the Blighted Swamp."),
		FromEvent("bramble_seed", UnlockKind.Spell, "the_grove", "Grown back in the corrupted grove of the Enchanted Forest."),
		// The early chapters' side events (.ai/campaign.md) pay relics, not people - see there for
		// why a new wizard is a decision for later. The coin thrown into the last well the dark wizard
		// has not fouled, and a fleck of the wizards' own warded circle, still lit.
		FromEvent("wishing_coin", UnlockKind.Boon, "the_well", "Drawn up from the well in the Fallow Fields."),
		FromEvent("gilded_mote", UnlockKind.Boon, "the_moot", "Left burning in the stones of the Broken Circle."),
		// FREED BY COLLECTING. John Dee's library at Mortlake was looted while he was abroad, and the
		// dark wizard keeps him the same way - scattered through the chests the player opens. The
		// collector is earned by collecting: lifetime chests, a stat every save already has.
		FromAchievementCharacter("john_dee", "mortlake_library", "Open 30 chests across your runs."),
		// FREED BY FINISHING HIS CHASE. Laelaps, the hound fated to catch whatever it hunted, set in stone on
		// the high heath mid-chase; the black hares there are what he was running down. A hound of the
		// player's own light finishes it for him (the "coursing" deed). Secret until the Broken Circle
		// opens - a hint naming a heath the player cannot reach is a spoiler with nothing to act on - and
		// outside the campaign gate: he is not a wizard, and the deed is optional (OutsideCampaignGate).
		FromAchievementCharacter("laelaps", "coursing",
			"The Coursing: in the Broken Circle, let your Familiar, taken as a hound, run down "
			+ $"{AchievementDefinitions.CoursingTarget} Black Hares in one run.",
			secretUntilStage: "stage_10", outsideCampaignGate: true),
	};

	private static UnlockDefinition Starter(string id, UnlockKind kind) => new()
	{
		Id = id,
		Kind = kind,
		Source = UnlockSource.Starter,
		LockedHint = string.Empty,
	};

	private static UnlockDefinition FromAchievementBoon(string boonId, string achievementId, string hint) => new()
	{
		Id = boonId,
		Kind = UnlockKind.Boon,
		Source = UnlockSource.Achievement,
		SourceId = achievementId,
		LockedHint = hint,
	};

	private static UnlockDefinition PurchaseBoon(string boonId, int cost) => new()
	{
		Id = boonId,
		Kind = UnlockKind.Boon,
		Source = UnlockSource.Purchase,
		PurchaseCost = cost,
		LockedHint = "Bought with Arcane Energy.",
	};

	private static UnlockDefinition FromAchievement(string spellId, string achievementId, string hint) => new()
	{
		Id = spellId,
		Kind = UnlockKind.Spell,
		Source = UnlockSource.Achievement,
		SourceId = achievementId,
		LockedHint = hint,
	};

	private static UnlockDefinition FromAchievementCharacter(string characterId, string achievementId, string hint,
		string secretUntilStage = "", bool outsideCampaignGate = false) => new()
	{
		Id = characterId,
		Kind = UnlockKind.Character,
		Source = UnlockSource.Achievement,
		SourceId = achievementId,
		LockedHint = hint,
		IsSecret = !string.IsNullOrWhiteSpace(secretUntilStage),
		RevealStageId = secretUntilStage ?? string.Empty,
		OutsideCampaignGate = outsideCampaignGate,
	};

	/// <summary>
	/// Something a chapter's side event pays: the reward of that event's Road deed, whose id is made
	/// from the site (<see cref="AchievementDefinitions.EventDeedPrefix"/>). A const prefix rather than a
	/// call into AchievementDefinitions, which reads this catalog while it builds its own list.
	/// </summary>
	/// <param name="secretUntilStage">
	/// A stage id makes a wizard secret until that chapter opens (see <see cref="UnlockDefinition.IsSecret"/>).
	/// </param>
	private static UnlockDefinition FromEvent(string id, UnlockKind kind, string siteId, string hint,
		string secretUntilStage = "") => new()
	{
		Id = id,
		Kind = kind,
		Source = UnlockSource.Achievement,
		SourceId = AchievementDefinitions.EventDeedPrefix + siteId,
		SiteId = siteId,
		LockedHint = hint,
		IsSecret = !string.IsNullOrWhiteSpace(secretUntilStage),
		RevealStageId = secretUntilStage ?? string.Empty,
	};

	/// <summary>
	/// A spell a vault pays (.ai/achievements-and-secrets.md 6.4): the reward of the deed derived from
	/// the secret (<see cref="SecretCatalog.AsAchievement"/>), whose id is made from the secret's.
	/// </summary>
	private static UnlockDefinition FromSecret(string spellId, string secretId, string hint) => new()
	{
		Id = spellId,
		Kind = UnlockKind.Spell,
		Source = UnlockSource.Achievement,
		SourceId = AchievementDefinitions.SecretDeedPrefix + secretId,
		LockedHint = hint,
	};

	/// <summary>
	/// Everything a side event's site gives. Read by the site's derived Road deed, by DiscoveryRewards
	/// (whether the event pays a chest instead) and by the world map's chapter card. The name is the
	/// old one: a "discovery site" is still what the world calls the place an event stands.
	/// </summary>
	public static IEnumerable<UnlockDefinition> ForDiscoverySite(string siteId) =>
		Definitions.Where(d => !string.IsNullOrEmpty(d.SiteId)
			&& d.SiteId.Equals(siteId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

	/// <summary>The deed that grants this unlock, or null for a starter or a purchase.</summary>
	public static AchievementDefinition SourceDeedOf(UnlockDefinition definition) =>
		definition?.Source == UnlockSource.Achievement ? AchievementDefinitions.GetById(definition.SourceId) : null;

	public static IReadOnlyList<UnlockDefinition> All => Definitions;

	public static UnlockDefinition Get(string id, UnlockKind kind)
	{
		if (string.IsNullOrWhiteSpace(id))
			return null;

		string trimmed = id.Trim();
		return Definitions.FirstOrDefault(d => d.Kind == kind && d.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
	}

	public static UnlockDefinition GetSpell(string spellId) => Get(spellId, UnlockKind.Spell);

	public static UnlockDefinition GetCharacter(string characterId) => Get(characterId, UnlockKind.Character);

	/// <summary>True when this thing needs no earning. Unknown ids are NOT starters - see remarks.</summary>
	/// <remarks>
	/// An id with no catalog entry returns false, so a spell someone forgot to assign is locked
	/// rather than free. That is the safe direction: a missing entry then shows up as content the
	/// player cannot reach, which is visible, instead of as content that quietly bypasses the
	/// economy, which is what the old default-unlocked set did to all thirty-five spells.
	/// </remarks>
	public static bool IsStarter(string id, UnlockKind kind) => Get(id, kind)?.Source == UnlockSource.Starter;

	/// <summary>
	/// Every spell the player is expected to own eventually: what The Grimoire Restored counts. The
	/// final gate counted it until schema 19; it counts wizards now (GlobalStatsManager.GateWizardIds).
	/// </summary>
	public static IEnumerable<string> AllSpellIds =>
		Definitions.Where(d => d.Kind == UnlockKind.Spell).Select(d => d.Id);

	/// <summary>
	/// Every wizard, excluding the development sandbox entry and anyone outside the campaign gate (a
	/// character who is not a wizard - <see cref="UnlockDefinition.OutsideCampaignGate"/>). Each
	/// wizard-gated chapter counts the ones whose rescue comes before it
	/// (<see cref="GlobalStatsManager.GateWizardIds"/>). The save migrations read frozen copies, never
	/// this, so a wizard added later cannot change what an old step did.
	/// </summary>
	public static IEnumerable<string> AllWizardIds =>
		AllCharacterIds.Where(id => Get(id, UnlockKind.Character)?.OutsideCampaignGate != true);

	/// <summary>Every playable character but the sandbox, wizard or not - what "own everything" means to a tool.</summary>
	public static IEnumerable<string> AllCharacterIds =>
		Definitions.Where(d => d.Kind == UnlockKind.Character
			&& !d.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
			.Select(d => d.Id);

	/// <summary>Every boon, in catalog order.</summary>
	public static IEnumerable<string> AllBoonIds =>
		All.Where(d => d.Kind == UnlockKind.Boon).Select(d => d.Id);

	/// <summary>
	/// The boons the shop sells, the last section of its Powers menu (MainMenu.Shop.cs). The only
	/// purchases in the catalog: since schema 19 the shop sells no spells
	/// (.ai/achievements-and-secrets.md 2.4), and ValidateUnlockCatalog refuses a spell or a wizard
	/// sourced any way but Starter or Achievement.
	/// </summary>
	public static IEnumerable<UnlockDefinition> PurchasableBoons =>
		All.Where(d => d.Kind == UnlockKind.Boon && d.Source == UnlockSource.Purchase);

	/// <summary>
	/// What to show on a locked card. Falls back to a plain string rather than an empty one so a
	/// card is never blank about why it is locked.
	/// </summary>
	public static string GetLockedHint(string id, UnlockKind kind)
	{
		UnlockDefinition definition = Get(id, kind);
		if (definition == null)
			return "Not yet obtainable.";

		return string.IsNullOrWhiteSpace(definition.LockedHint) ? "Not yet obtainable." : definition.LockedHint;
	}
}
