using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>How a thing is obtained. The extension point for new unlock mechanics.</summary>
public enum UnlockSource
{
	/// <summary>Available from the first run. Never gated.</summary>
	Starter,
	/// <summary>Granted when the achievement named by <see cref="UnlockDefinition.SourceId"/> completes.</summary>
	Achievement,
	/// <summary>Bought with Arcane Energy in the main menu shop.</summary>
	Purchase,
	/// <summary>Found in the world at the discovery site named by <see cref="UnlockDefinition.SourceId"/>.</summary>
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

	/// <summary>Achievement id or discovery site id. Empty for Starter and Purchase.</summary>
	public string SourceId { get; init; } = string.Empty;

	/// <summary>Arcane Energy price. Only read when <see cref="Source"/> is Purchase.</summary>
	public int PurchaseCost { get; init; }

	/// <summary>
	/// One line telling the player how to get this, shown on the locked card. Never leave it empty:
	/// a lock the player cannot read is indistinguishable from content that is simply missing.
	/// </summary>
	public string LockedHint { get; init; } = string.Empty;
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

		// Achievement rewards. These ids must match AchievementDefinitions.All, which
		// ValidateUnlockCatalog checks in both directions - an achievement granting a spell that
		// claims a different source, or a spell pointing at an achievement that does not exist,
		// are both the same silent dead end the old economy was made of.
		FromAchievement("arcane_explosion", "first_blood", "Defeat your first enemy."),
		FromAchievement("frost_shard", "survivor", "Survive ten minutes in a single run."),
		FromAchievement("meteor_swarm", "veteran", "Clear a chapter without falling."),
		FromAchievement("solar_flare", "max_level", "Reach level 20 in a single run."),
		FromAchievement("cyclone_slash", "elementalist", "Reach 4 instances of any element."),
		FromAchievement("void_lance", "hoarder", "Fill all six spell slots in a run."),
		FromAchievementBoon("umbral_veil", "untouchable", "Survive five minutes without being hit."),
		FromAchievement("scorching_ray", "fire_adept", "End a run with 4 Fire instances."),
		FromAchievement("glacial_spike", "ice_adept", "End a run with 4 Ice instances."),
		FromAchievement("toxic_spore_burst", "poison_adept", "End a run with 4 Poison instances."),
		FromAchievement("black_tentacles", "earth_adept", "End a run with 4 Earth instances."),
		FromAchievement("gale_blade", "wind_adept", "End a run with 4 Wind instances."),
		FromAchievement("thorn_vine", "forest_cleared", "Defeat the boss of the Enchanted Forest."),
		FromAchievement("shadow_bolt", "castle_conqueror", "Defeat the boss of the Cursed Dungeon."),
		FromAchievement("spiritual_weapon", "ruins_delver", "Defeat the boss of the Mystic Ruins."),

		// Bought with Arcane Energy. Roughly priced by how much a run changes when the spell shows
		// up: a second damage option is cheap, a whole defensive layer is not.
		Purchase("molten_shard", 110),
		// The first pure Water spell. Sold rather than awarded because Water had no guaranteed
		// carrier at all and an achievement gate would have left the element unreachable until
		// the player happened to satisfy it.
		Purchase("riptide", 120),
		// The three archetypes the roster had never had: a spell paid for by movement, an
		// autonomous one, and a trap. Priced above the plain damage options because each one
		// changes how a run is played rather than how hard it hits.
		Purchase("mirefoot", 130),
		Purchase("gravewell", 150),
		Purchase("kindled_ward", 170),
		Purchase("hunters_draw", 140),
		// Fireball was a starter until Cinderbreath replaced it. It is now the big slow one - a
		// five-second cooldown and a blast wide enough to be worth waiting for - which is a
		// later-game shape, so it is priced like one.
		Purchase("fireball", 190),

		// Boons fill the shop that passive spells used to. Priced by how much a run changes when
		// one turns up: a stat nudge is cheap, a whole defensive layer is not.
		PurchaseBoon("quicksilver_bead", 110),
		PurchaseBoon("thornseed", 110),
		PurchaseBoon("deepwater_pearl", 120),
		PurchaseBoon("shadegrease", 130),
		PurchaseBoon("saltbound_chain", 140),
		PurchaseBoon("sunsteel_filament", 140),
		PurchaseBoon("gilded_mote", 150),
		PurchaseBoon("wishing_coin", 160),
		// The two reactive boons are priced above the stat boons because they change how a fight
		// plays rather than how long it lasts - standing in the crowd becomes a thing you do on
		// purpose rather than a thing you survive.
		PurchaseBoon("rimebriar", 170),

		// The four starting wizards. Listed rather than assumed, so the roster and the catalog
		// cannot disagree about who the player begins with.
		Starter("pyromancer", UnlockKind.Character),
		Starter("frostweaver", UnlockKind.Character),
		Starter("stormcaller", UnlockKind.Character),
		Starter("geomancer", UnlockKind.Character),
		Starter("test_wizard", UnlockKind.Character),
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

	private static UnlockDefinition Purchase(string spellId, int cost) => new()
	{
		Id = spellId,
		Kind = UnlockKind.Spell,
		Source = UnlockSource.Purchase,
		PurchaseCost = cost,
		LockedHint = $"Study it in the Arcane Codex for {cost} Arcane Energy.",
	};

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

	/// <summary>Every spell the player is expected to own eventually. Drives the final-stage gate.</summary>
	public static IEnumerable<string> AllSpellIds =>
		Definitions.Where(d => d.Kind == UnlockKind.Spell).Select(d => d.Id);

	/// <summary>Every wizard, excluding the development sandbox entry.</summary>
	public static IEnumerable<string> AllWizardIds =>
		Definitions.Where(d => d.Kind == UnlockKind.Character
			&& !d.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
			.Select(d => d.Id);

	/// <summary>Spells sold in the shop, in catalog order.</summary>
	public static IEnumerable<string> AllBoonIds =>
		All.Where(d => d.Kind == UnlockKind.Boon).Select(d => d.Id);

	public static IEnumerable<UnlockDefinition> PurchasableBoons =>
		All.Where(d => d.Kind == UnlockKind.Boon && d.Source == UnlockSource.Purchase);

	public static IEnumerable<UnlockDefinition> PurchasableSpells =>
		Definitions.Where(d => d.Kind == UnlockKind.Spell && d.Source == UnlockSource.Purchase);

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
