using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public static class ChestItemCatalog
{
	// Damage items
	public const string RelicKey = "relic_key";
	public const string EmberFlask = "ember_flask";
	public const string WrathAmulet = "wrath_amulet";
	public const string EtherealBlade = "ethereal_blade";
	public const string SpectralFang = "spectral_fang";
	public const string ObsidianHeart = "obsidian_heart";

	// Defense items
	public const string AegisSigil = "aegis_sigil";
	public const string IronFang = "iron_fang";
	public const string BasaltCarapace = "basalt_carapace";
	public const string AegisCrown = "aegis_crown";
	public const string IronhideCloak = "ironhide_cloak";
	public const string ProtectiveWard = "protective_ward";

	// Healing & Recovery items
	public const string VialOfVitality = "vial_of_vitality";
	public const string HeartOfRenewal = "heart_of_renewal";
	public const string Phylactery = "phylactery";
	public const string EssenceChalice = "essence_chalice";

	// Utility items
	public const string QuicksilverPendant = "quicksilver_pendant";
	public const string HasteRune = "haste_rune";
	public const string CompassRose = "compass_rose";
	public const string LuckyCoin = "lucky_coin";

	// Elemental items
	public const string StormLattice = "storm_lattice";
	public const string InfernoCore = "inferno_core";
	public const string FrozenTear = "frozen_tear";
	public const string Thunderstone = "thunderstone";
	public const string CrystalPrism = "crystal_prism";

	// --- Rule-changers (see .ai/spell-variety.md section 6) ---------------------------------
	//
	// The twenty-five items above are all stat modifiers, which is the whole reason the item
	// roster reads as one item repeated. These four change a RULE instead: what crits, what
	// takes extra damage, what a projectile costs, and how hard the run itself is.
	//
	// The test each one has to pass is that you could describe it without using a percentage as
	// the subject of the sentence. "Slowed enemies are vulnerable" is a rule. "+12% damage" is
	// not, however large the number.
	public const string CrackedPrism = "cracked_prism";
	public const string DuellistsChalk = "duellists_chalk";
	public const string HoarfrostNail = "hoarfrost_nail";
	public const string WormwoodTithe = "wormwood_tithe";

	// --- Tome relics (.ai/passives-and-items.md, section 4b) --------------------------------
	//
	// A spell, once taken, is held for the rest of the run: there is no swap and no erase at a
	// level-up any more. These two are the only things in the game that change the spell book
	// itself rather than the wizard, and that is why they keep different rules from every relic
	// above - no set, no element tag (see TomeRelicIds).
	//
	// The Scribe's Penknife is a scribe's knife, the one that scrapes a mistake off vellum: claiming
	// it cuts one held spell out of the book. The Blank Folio is a leaf bound in: one more page, so
	// one more spell, for the run.
	public const string ScribesPenknife = "scribes_penknife";
	public const string BlankFolio = "blank_folio";

	// --- The collector's relics (.ai/passives-and-items.md, section 8) -----------------------
	//
	// Chests come from elites and minibosses now, not a clock, so a build that wants more of them
	// acts on WHO carries one and WHAT is inside: a lodestone for the pickup radius, a ledger that
	// marks more elites as carriers, and a drawer that hides a second chest in the first. They are
	// the three pieces of one cabinet of curiosities - John Dee's kind of object.
	public const string Lodestone = "lodestone";
	public const string CurioLedger = "curio_ledger";
	public const string HiddenDrawer = "hidden_drawer";

	/// <summary>The relics that act on the spell book. Never a piece of a set, never tagged.</summary>
	/// <remarks>
	/// No set, because a set is something you want more of and both of these are one-per-run: the
	/// knife is spent the moment it cuts, and a set that needed the Folio would turn a set chase into
	/// a hunt for the rarest relic in the pool. No tag, because a tag is element budget and a seventh
	/// page already is element budget - up to three tags' worth, in whatever spell fills it.
	/// RegressionChecks holds both rules.
	/// </remarks>
	public static readonly IReadOnlyList<string> TomeRelicIds = new[] { ScribesPenknife, BlankFolio };

	public static bool IsTomeRelic(string itemId) => TomeRelicIds.Contains(itemId, StringComparer.OrdinalIgnoreCase);

	// Full Set Enchantment ids. Stable: saves, telemetry and Player.RefreshChestSetEffects key on them,
	// so the objects were renamed around them rather than the other way round (issue #63).
	public const string VaultguardSetId = "vaultguard";
	public const string EmberlineSetId = "emberline";
	public const string StormboundSetId = "stormbound";
	public const string BastionOfSpikesSetId = "bastion_of_spikes";
	public const string DeathbringerSetId = "deathbringer";
	public const string EternalGuardianSetId = "eternal_guardian";
	public const string LifeDrainSetId = "life_drain";
	public const string ElementalMasterySetId = "elemental_mastery";
	public const string SpeedDemonSetId = "speed_demon";
	public const string FortunesFavorSetId = "fortunes_favor";
	public const string CabinetOfCuriositiesSetId = "cabinet_of_curiosities";

	public static readonly IReadOnlyList<string> AllItemIds = new[]
	{
		// Damage
		RelicKey,
		EmberFlask,
		WrathAmulet,
		EtherealBlade,
		SpectralFang,
		ObsidianHeart,
		// Defense
		AegisSigil,
		IronFang,
		BasaltCarapace,
		AegisCrown,
		IronhideCloak,
		ProtectiveWard,
		// Healing & Recovery
		VialOfVitality,
		HeartOfRenewal,
		Phylactery,
		EssenceChalice,
		// Utility
		QuicksilverPendant,
		HasteRune,
		CompassRose,
		LuckyCoin,
		// Elemental
		StormLattice,
		InfernoCore,
		FrozenTear,
		Thunderstone,
		CrystalPrism,
		// Rule-changers
		CrackedPrism,
		DuellistsChalk,
		HoarfrostNail,
		WormwoodTithe,
		// Tome relics
		ScribesPenknife,
		BlankFolio,
		// The collector's
		Lodestone,
		CurioLedger,
		HiddenDrawer
	};

	// --- Rarity (see .ai/passives-and-items.md) ---------------------------------------------
	//
	// Rarity does three jobs at once and they move together: how often an item is offered, how big
	// its effect is, and - the important one - whether it carries an element tag.
	//
	// Tying tags to rarity is what keeps tags scarce. Element weight is the tightest budget in the
	// game; five elements lost most of their carriers when passive spells were removed, and the
	// answer cannot be to hand a tag to all twenty-five chest items. It belongs on the ones the
	// player rarely sees.

	/// <summary>How often an item of each rarity is offered, relative to the others.</summary>
	private static float OfferWeightFor(ChestItemRarity rarity) => rarity switch
	{
		ChestItemRarity.Common => 1.0f,
		ChestItemRarity.Uncommon => 0.55f,
		ChestItemRarity.Rare => 0.22f,
		ChestItemRarity.Relic => 0.08f,
		_ => 1.0f,
	};

	public static ChestItemRarity GetRarity(string itemId)
	{
		return itemId switch
		{
			// Relic - build-defining, and the only items carrying two tags.
			ObsidianHeart => ChestItemRarity.Relic,
			// The curse. Relic rarity because it is the biggest single decision in the item pool,
			// not because it is the strongest thing in it - taking it makes the run harder.
			WormwoodTithe => ChestItemRarity.Relic,
			AegisCrown => ChestItemRarity.Relic,
			Phylactery => ChestItemRarity.Relic,
			// A seventh spell is the largest single change any relic makes to a run, so it sits in
			// the rarest tier. Ownership makes it one per run.
			BlankFolio => ChestItemRarity.Relic,
			// The one way back from a spell the player regrets. Rare rather than Uncommon because
			// spells are meant to be permanent: the knife is an occasional exception, not something
			// most chests after a full book would offer.
			ScribesPenknife => ChestItemRarity.Rare,

			// Rare - the elemental pieces, which is what makes them worth the name.
			StormLattice => ChestItemRarity.Rare,
			InfernoCore => ChestItemRarity.Rare,
			FrozenTear => ChestItemRarity.Rare,
			Thunderstone => ChestItemRarity.Rare,
			CrystalPrism => ChestItemRarity.Rare,
			EtherealBlade => ChestItemRarity.Rare,
			SpectralFang => ChestItemRarity.Rare,
			EssenceChalice => ChestItemRarity.Rare,
			HoarfrostNail => ChestItemRarity.Rare,
			// A chest inside a chest is a relic that pays in relics, so it is offered about as often
			// as the elemental pieces rather than every other chest.
			HiddenDrawer => ChestItemRarity.Rare,

			// Uncommon
			// A quarter more chests from elites is worth two or three relics a run.
			CurioLedger => ChestItemRarity.Uncommon,
			WrathAmulet => ChestItemRarity.Uncommon,
			BasaltCarapace => ChestItemRarity.Uncommon,
			IronhideCloak => ChestItemRarity.Uncommon,
			HeartOfRenewal => ChestItemRarity.Uncommon,
			HasteRune => ChestItemRarity.Uncommon,
			ProtectiveWard => ChestItemRarity.Uncommon,
			CrackedPrism => ChestItemRarity.Uncommon,
			DuellistsChalk => ChestItemRarity.Uncommon,

			// Common - everything else.
			_ => ChestItemRarity.Common,
		};
	}

	/// <summary>
	/// Element tags an item carries. Only Rare and Relic items carry any.
	/// </summary>
	public static IReadOnlyList<(string Element, int Weight)> GetElementTags(string itemId)
	{
		return itemId switch
		{
			ObsidianHeart => new[] { ("Darkness", 1), ("Earth", 1) },
			AegisCrown => new[] { ("Metal", 1), ("Light", 1) },
			Phylactery => new[] { ("Darkness", 1), ("Arcane", 1) },
			// Grass had no chest item carrying its tag at all, which the element audit in
			// .ai/passives-and-items.md flagged. A bitter root offered as payment closes that
			// gap and reads as the curse it is.
			WormwoodTithe => new[] { ("Darkness", 1), ("Grass", 1) },

			StormLattice => new[] { ("Lightning", 1) },
			InfernoCore => new[] { ("Fire", 1) },
			FrozenTear => new[] { ("Ice", 1) },
			Thunderstone => new[] { ("Lightning", 1) },
			CrystalPrism => new[] { ("Arcane", 1) },
			EtherealBlade => new[] { ("Wind", 1) },
			SpectralFang => new[] { ("Poison", 1) },
			EssenceChalice => new[] { ("Water", 1) },
			HoarfrostNail => new[] { ("Ice", 1) },
			// The drawer is the dark inside of the cabinet. Darkness had no Rare carrier (its three
			// item tags were all on Relics), and it is John Dee's element.
			HiddenDrawer => new[] { ("Darkness", 1) },

			_ => Array.Empty<(string, int)>(),
		};
	}

	// Every Effects list below is transcribed from Player.RefreshChestSetEffects. Where the old
	// flavour text promised a mechanic the code never implemented (Vaultguard's per-chest shield,
	// Emberline's per-cast growth, Stormbound's chaining crits) the entry states what actually
	// happens, so the Full Set Enchantment screen cannot mislead the player.
	//
	// The Descriptions used to be flavour ("A small gilded casket for what must not be lost") and
	// the player's report was that they should describe the effect, not fluff. They are now the
	// mechanic in one sentence with its numbers, because that sentence is what the chest card shows
	// under "completing this gives" - flavour there answered a question nobody was asking at the one
	// moment the real one matters. Re-verified against the code when they were rewritten
	// (2026-10-20), which turned up two labels that had drifted: the Jewelled Crown's "+1 chain
	// target" is really "chains trigger one hit sooner" (GetLightningChainRules subtracts it from the
	// hits required), and its "ice slow strength" is added to EVERY hit's slow, Ice tier or not.
	// A Full Set Enchantment is the pieces of one real object, so its icon is that object whole
	// rather than a badge or a monogram. Generated by tools/art/relic_icons.py.
	private const string SetIconRoot = "res://assets/bonelight/ui/sets/";

	public static readonly IReadOnlyList<ChestSetDefinition> Sets = new[]
	{
		// Original sets
		new ChestSetDefinition
		{
			Id = VaultguardSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Damage taken", Value = "-5%" } },
			ElementTags = new[] { ("Metal", 1), ("Earth", 1) },
			Name = "The Reliquary",
			RequiredItemIds = new[] { RelicKey, VialOfVitality, Phylactery },
			Description = "Take 12% less damage, and every chest you open heals 6 HP and raises a 6-point shield.",
			IconPath = SetIconRoot + "vaultguard.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Damage taken", Value = "-12%" },
				new ChestSetEffect { Label = "Shield on opening a chest", Value = "6 points" },
				new ChestSetEffect { Label = "Heal on opening a chest", Value = "+6 HP" }
			}
		},
		new ChestSetDefinition
		{
			Id = EmberlineSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Spell damage", Value = "+6%" } },
			ElementTags = new[] { ("Fire", 1), ("Light", 1) },
			Name = "The Brazier",
			RequiredItemIds = new[] { EmberFlask, InfernoCore, ObsidianHeart },
			// The stand-still set: a brazier burns hotter the longer it is tended. Was a per-cast ramp
			// to +18% / +12% that a loadout filled within seconds and never lost - a flat bonus with a
			// wind-up (Player.BrazierHeatUpSeconds says why it changed). Built from the constants the
			// code reads, so the card and the rule cannot drift. A no-break space between each number
			// and its "s": the PC chest page broke "over 4 / s" across two lines.
			Description = $"Stand still: heat builds over {Player.BrazierHeatUpSeconds:0}\u00A0s to {Percent(Player.BrazierDamageAtFullHeat)} spell damage and {Percent(Player.BrazierAreaAtFullHeat)} spell area. Moving cools it over {Player.BrazierCoolDownSeconds:0}\u00A0s.",
			IconPath = SetIconRoot + "emberline.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Spell damage at full heat", Value = Percent(Player.BrazierDamageAtFullHeat) },
				new ChestSetEffect { Label = "Spell area at full heat", Value = Percent(Player.BrazierAreaAtFullHeat) },
				new ChestSetEffect { Label = "Heat", Value = $"full after {Player.BrazierHeatUpSeconds:0}\u00A0s still, cold after {Player.BrazierCoolDownSeconds:0}\u00A0s moving" }
			}
		},
		new ChestSetDefinition
		{
			Id = StormboundSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Crit chance", Value = "+4%" } },
			ElementTags = new[] { ("Lightning", 1), ("Metal", 1) },
			Name = "The Lightning Rod",
			RequiredItemIds = new[] { StormLattice, EtherealBlade, CrackedPrism },
			Description = "+10% crit chance. Every critical hit arcs to the nearest enemy within 170 for half its damage, and you move 10% faster while shielded.",
			IconPath = SetIconRoot + "stormbound.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Crit chance", Value = "+10%" },
				new ChestSetEffect { Label = "Crits arc to a 2nd enemy", Value = "50% damage, 170 range" },
				new ChestSetEffect { Label = "Move speed while shielded", Value = "+10%" }
			}
		},
		new ChestSetDefinition
		{
			Id = BastionOfSpikesSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Damage taken", Value = "-6%" } },
			ElementTags = new[] { ("Earth", 1), ("Metal", 1) },
			Name = "The Thorned Buckler",
			RequiredItemIds = new[] { ProtectiveWard, IronFang, HoarfrostNail },
			Description = "Take 15% less damage. While you are below 30% HP, every hit that hurts you strikes all enemies within 120 for 8 damage + 3 per player level.",
			IconPath = SetIconRoot + "bastion_of_spikes.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Damage taken", Value = "-15%" },
				new ChestSetEffect { Label = "Retaliate when hit below 30% HP", Value = "8 + 3/level damage, 120 range" }
			}
		},
		// New sets
		new ChestSetDefinition
		{
			Id = DeathbringerSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Spell damage", Value = "+8%" } },
			ElementTags = new[] { ("Darkness", 1), ("Poison", 1) },
			Name = "The Reaper's Scythe",
			RequiredItemIds = new[] { SpectralFang, WrathAmulet, DuellistsChalk },
			Description = "+25% spell damage, and any hit on an enemy below 30% health kills it outright.",
			IconPath = SetIconRoot + "deathbringer.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Spell damage", Value = "+25%" },
				new ChestSetEffect { Label = "Any hit kills an enemy", Value = "below 30% HP" }
			}
		},
		new ChestSetDefinition
		{
			Id = EternalGuardianSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Max HP", Value = "+30" } },
			ElementTags = new[] { ("Light", 1), ("Metal", 1) },
			Name = "The Barrow Harness",
			RequiredItemIds = new[] { AegisSigil, IronhideCloak, BasaltCarapace },
			Description = "+80 max HP, and take 18% less damage.",
			IconPath = SetIconRoot + "eternal_guardian.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Max HP", Value = "+80" },
				new ChestSetEffect { Label = "Damage taken", Value = "-18%" }
			}
		},
		new ChestSetDefinition
		{
			Id = LifeDrainSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Max HP", Value = "+20" } },
			ElementTags = new[] { ("Darkness", 1), ("Water", 1) },
			Name = "The Tithe Chalice",
			RequiredItemIds = new[] { EssenceChalice, HeartOfRenewal, WormwoodTithe },
			Description = "Every kill heals 1 HP.",
			IconPath = SetIconRoot + "life_drain.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Heal per kill", Value = "+1 HP" }
			}
		},
		new ChestSetDefinition
		{
			Id = ElementalMasterySetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Elemental tier bonuses", Value = "+15%" } },
			ElementTags = new[] { ("Arcane", 1), ("Ice", 1) },
			Name = "The Jewelled Crown",
			RequiredItemIds = new[] { AegisCrown, FrozenTear, Thunderstone, CrystalPrism },
			Description = "Fire, Ice, Lightning and Poison tier bonuses +40%. Every hit slows 20% more for 40% longer. Lightning chains reach 50% farther and trigger 1 hit sooner.",
			IconPath = SetIconRoot + "elemental_mastery.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Elemental tier bonuses", Value = "+40%" },
				new ChestSetEffect { Label = "Slow duration", Value = "+40%" },
				new ChestSetEffect { Label = "Slow on every hit", Value = "+20%" },
				new ChestSetEffect { Label = "Chain lightning radius", Value = "+50%" },
				new ChestSetEffect { Label = "Chain lightning triggers", Value = "1 hit sooner" }
			}
		},
		new ChestSetDefinition
		{
			Id = SpeedDemonSetId,
			ElementTags = new[] { ("Wind", 1), ("Grass", 1) },
			Name = "The Seven-League Boots",
			RequiredItemIds = new[] { QuicksilverPendant, HasteRune },
			Description = "+20% move speed and +15% attack speed.",
			IconPath = SetIconRoot + "speed_demon.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Move speed", Value = "+20%" },
				new ChestSetEffect { Label = "Attack speed", Value = "+15%" }
			}
		},
		new ChestSetDefinition
		{
			Id = FortunesFavorSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "XP gained", Value = "+10%" } },
			ElementTags = new[] { ("Water", 1), ("Light", 1) },
			Name = "The Regalia",
			RequiredItemIds = new[] { AegisCrown, LuckyCoin, CompassRose },
			// Was "Chests appear 25% more often". Chests come from elites now, so the Regalia pays in
			// greater orbs - the same trade the Signet Ring made, larger.
			Description = $"+25% XP, and {GreaterOrbClause(Player.RegaliaGreaterOrbChance)}.",
			IconPath = SetIconRoot + "fortunes_favor.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "XP gained", Value = "+25%" },
				new ChestSetEffect { Label = "Bigger XP orbs", Value = GreaterOrbValueText(Player.RegaliaGreaterOrbChance) }
			}
		},
		// THE COLLECTOR'S SET. A cabinet of curiosities - the collector's whole house in one piece of
		// furniture, and John Dee's kind of object. Its tags are his element and the element whose
		// tiers pay XP: the build it is for is the one that gathers.
		new ChestSetDefinition
		{
			Id = CabinetOfCuriositiesSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Pickup radius", Value = $"+{Player.CabinetPartialPickupRadius}" } },
			ElementTags = new[] { ("Arcane", 1), ("Darkness", 1) },
			Name = "The Cabinet of Curiosities",
			RequiredItemIds = new[] { Lodestone, CurioLedger, HiddenDrawer },
			Description = $"Every elite drops a chest, and +{Player.CabinetFullPickupRadius} pickup radius.",
			IconPath = SetIconRoot + "cabinet_of_curiosities.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Elites that drop a chest", Value = "all of them" },
				new ChestSetEffect { Label = "Pickup radius", Value = $"+{Player.CabinetFullPickupRadius}" }
			}
		}
	};

	// One wording for the greater orb everywhere it is sold, built from the numbers the code reads.
	// "Bigger" rather than "greater": the Tithe of Wormwood's card lists its five stats in two columns
	// on a phone, which needs every one of them under 19 characters - "+5% bigger XP orbs" is 18.
	private static string GreaterOrbClause(float chance) =>
		$"+{Mathf.RoundToInt(chance * 100f)}% chance an XP orb drops bigger: {DropTable.GreaterOrbValue} XP instead of 2";

	private static string GreaterOrbValueText(float chance) =>
		$"+{Mathf.RoundToInt(chance * 100f)}% ({DropTable.GreaterOrbValue} XP, not 2)";

	// A fraction the code reads, as the card prints it: 0.3 -> "+30%".
	private static string Percent(float fraction) => $"+{Mathf.RoundToInt(fraction * 100f)}%";

	public static string GetDisplayName(string itemId)
	{
		// Each relic is named as the piece of the object its Full Set Enchantment assembles
		// (issue #63), so two pieces tell the player what the third is. The ids are the old names
		// and stay that way: saves, telemetry and icons all key on them.
		return itemId switch
		{
			// Damage items
			CrackedPrism => "Split Lens",
			DuellistsChalk => "Whetstone",
			HoarfrostNail => "Hoarfrost Rivets",
			WormwoodTithe => "Tithe of Wormwood",
			RelicKey => "Reliquary Key",
			EmberFlask => "Brazier Coals",
			WrathAmulet => "Scythe Snath",
			EtherealBlade => "Rod Tip",
			SpectralFang => "Scythe Blade",
			ObsidianHeart => "Brazier Tripod",
			// Defense items
			AegisSigil => "Barrow Breastplate",
			IronFang => "Buckler Thorns",
			BasaltCarapace => "Barrow Gorget",
			AegisCrown => "The Crown",
			IronhideCloak => "Barrow Greaves",
			ProtectiveWard => "Buckler Boss",
			// Healing & Recovery items
			VialOfVitality => "Reliquary Vial",
			HeartOfRenewal => "Heartstone",
			Phylactery => "Reliquary Casket",
			EssenceChalice => "Chalice Cup",
			// Utility items
			QuicksilverPendant => "Left Boot",
			HasteRune => "Right Boot",
			CompassRose => "Orb of Antioch",
			LuckyCoin => "Signet Ring",
			// Elemental items
			StormLattice => "Copper Coil",
			InfernoCore => "Brazier Bowl",
			FrozenTear => "Frost Jewel",
			Thunderstone => "Storm Jewel",
			CrystalPrism => "Prism Jewel",
			// The Cabinet of Curiosities
			Lodestone => "Lodestone",
			CurioLedger => "Curio Ledger",
			HiddenDrawer => "Hidden Drawer",
			// Tome relics - named as what they are, since they are pieces of no object
			ScribesPenknife => "Scribe's Penknife",
			BlankFolio => "Blank Folio",
			_ => itemId
		};
	}

	// The Reliquary Vial's line, built from the cap so the number on the card is the number in
	// Player.Heal. Worded the way the player put it ("heal past your max health by 20%"), and its
	// last clause is the short answer to "why is this not just more max HP": the shield is spent
	// before health. The rest of that answer - it only fills from healing that would otherwise be
	// wasted, and it is what Protective Ward and the Lightning Rod read as "shielded" - is in
	// .ai/passives-and-items.md.
	public static readonly string OverhealDescription =
		$"Heal past full health by up to {Mathf.RoundToInt(Player.OverhealShieldCapFraction * 100f)}% of max HP, as a shield that takes hits first.";

	// Both tome relics read their numbers from Player, for the same reason as the Vial: the card and
	// the rule cannot disagree. The knife's last clause is the protection rule (Player.GetCuttableSpells).
	public static readonly string BlankFolioDescription =
		$"+{Player.BlankFolioExtraSlots} spell slot: hold {Player.MaxSpellSlots + Player.BlankFolioExtraSlots} spells instead of {Player.MaxSpellSlots}.";
	public const string ScribesPenknifeDescription =
		"Cut 1 held spell out of your spell book: its slot is free again, and it is not offered again this run. Your wizard's own spell cannot be cut.";

	// The Split Lens's number comes from Player, where it is charged (GetSplitLensMultiplier).
	public static readonly string SplitLensDescription =
		$"+1 projectile to spells that fire them, and -{Mathf.RoundToInt((1f - Player.SplitLensDamageMultiplier) * 100f)}% damage to those spells only.";

	public static string GetDescription(string itemId)
	{
		return itemId switch
		{
			// One line, the change it makes, no framing. "Passively increases spell damage
			// by +12%" is nine words to say "+12% spell damage", and the reader is standing
			// in a crowd with the game paused. Damage reduction is called DEFENSE here and
			// nowhere is it called anything else.
			EmberFlask => "+12% spell damage.",
			WrathAmulet => "+25% spell damage for 4s after you are hit.",
			EtherealBlade => "+0.3x critical damage.",
			SpectralFang => "+15% spell damage to enemies below half health.",
			ObsidianHeart => "+10% spell damage, -5% movement speed.",
			RelicKey => "Chests offer one more relic to choose from.",
			AegisSigil => "+8% defense.",
			IronFang => "Enemies touching you take 6 damage per second.",
			BasaltCarapace => "Ignores one hit every 10s.",
			AegisCrown => "+30 maximum health.",
			IronhideCloak => "+20% defense while above 80% health.",
			ProtectiveWard => "+15% defense while a shield holds.",
			VialOfVitality => OverhealDescription,
			HeartOfRenewal => "+0.8 health per second.",
			Phylactery => "One revive per stage, at 25% health.",
			EssenceChalice => "+1 maximum health per kill, up to +50.",
			QuicksilverPendant => "+15% movement speed.",
			HasteRune => "+12% attack speed.",
			CompassRose => "+15% XP.",
			// Was "Chests appear 20% more often" - which also added 20 points to every kill's bonus
			// drop chance. Chests come from elites now (DropTable).
			LuckyCoin => GreaterOrbClause(Player.SignetRingGreaterOrbChance) + ".",
			Lodestone => $"+{Player.LodestonePickupRadius} pickup radius.",
			CurioLedger => $"+{Mathf.RoundToInt(Player.CurioLedgerEliteChestChance * 100f)}% chance an elite drops a chest ({Mathf.RoundToInt(DropTable.EliteChestChance * 100f)}% without it).",
			HiddenDrawer => $"{Mathf.RoundToInt(DropTable.NestedChestChance * 100f)}% chance a chest you open leaves another chest behind.",
			StormLattice => "+8% critical chance.",
			InfernoCore => "+12% spell area.",
			FrozenTear => "+40% ice duration, +15% slow strength.",
			Thunderstone => "+50% lightning chain range, chains trigger 1 hit sooner.",
			CrystalPrism => "+30% damage from spells that carry only one element.",
			// Both halves land on the same spells (0.9.3: "it should only reduce damage of spells that are
			// affected by the projectile count") - the ones the shop's Twinstar Sigil reaches, in its words.
			CrackedPrism => SplitLensDescription,
			DuellistsChalk => "Always crits an enemy at full health.",
			HoarfrostNail => "+25% damage to slowed or rooted enemies.",
			// "+25% more chests" became bigger orbs when chests stopped coming on a clock.
			WormwoodTithe => $"+39% enemies, +10% enemy speed, +30% XP, +{Mathf.RoundToInt(Player.WormwoodTitheGreaterOrbChance * 100f)}% bigger XP orbs, +3 luck.",
			ScribesPenknife => ScribesPenknifeDescription,
			BlankFolio => BlankFolioDescription,
			_ => "An unrecorded relic.",
		};
	}

	// Relic icons.
	//
	// These used to be drawn entirely from the spell-VFX icon set, which had two problems: three
	// images were each doing duty for two different relics (Protective Ward and Ironhide Cloak,
	// Wrath Amulet and Inferno Core, Frozen Tear and Quicksilver Pendant), and several were simply
	// wrong - Frozen Tear, an ice relic, wore a lightning bolt.
	//
	// Most now come from the GUI pack's equipment set (ui-png-elements2-*), which is a vocabulary
	// of armour, boots, weapons and vessels - the right register for gear. Spell-VFX icons are kept
	// only where the relic really is elemental. RegressionChecks asserts these stay distinct.
	public static string GetIconPath(string itemId)
	{
		return itemId switch
		{
			// Damage items
			RelicKey => Relic("relic_key"),
			EmberFlask => Relic("ember_flask"),
			WrathAmulet => Relic("wrath_amulet"),
			EtherealBlade => Relic("ethereal_blade"),
			SpectralFang => Relic("spectral_fang"),
			ObsidianHeart => Relic("obsidian_heart"),
			// Defense items
			AegisSigil => Relic("aegis_sigil"),
			IronFang => Relic("iron_fang"),
			BasaltCarapace => Relic("basalt_carapace"),
			AegisCrown => Relic("aegis_crown"),
			IronhideCloak => Relic("ironhide_cloak"),
			ProtectiveWard => Relic("protective_ward"),
			// Healing & Recovery items
			VialOfVitality => Relic("vial_of_vitality"),
			HeartOfRenewal => Relic("heart_of_renewal"),
			Phylactery => Relic("phylactery"),
			EssenceChalice => Relic("essence_chalice"),
			// Utility items
			QuicksilverPendant => Relic("quicksilver_pendant"),
			HasteRune => Relic("haste_rune"),
			CompassRose => Relic("compass_rose"),
			LuckyCoin => Relic("lucky_coin"),
			// Elemental items - these keep the spell-VFX icons because they genuinely are elemental
			StormLattice => Relic("storm_lattice"),
			InfernoCore => Relic("inferno_core"),
			FrozenTear => Relic("frozen_tear"),
			Thunderstone => Relic("thunderstone"),
			CrystalPrism => Relic("crystal_prism"),
			// Rule-changers
			CrackedPrism => Relic("cracked_prism"),
			DuellistsChalk => Relic("duellists_chalk"),
			HoarfrostNail => Relic("hoarfrost_nail"),
			WormwoodTithe => Relic("wormwood_tithe"),
			// Tome relics
			ScribesPenknife => Relic("scribes_penknife"),
			BlankFolio => Relic("blank_folio"),
			// The Cabinet of Curiosities
			Lodestone => Relic("lodestone"),
			CurioLedger => Relic("curio_ledger"),
			HiddenDrawer => Relic("hidden_drawer"),
			_ => "res://assets/bonelight/ui/spells/unknown.png"
		};
	}

	// All thirty-four are generated by tools/art/relic_icons.py. They used to be slices of a
	// bought GUI sheet - art licensed for use and not for redistribution. See
	// .ai/asset-licensing.md.
	private static string Relic(string name) => $"res://assets/bonelight/ui/relics/{name}.png";

	public static List<ChestSetDefinition> GetAssociatedSets(string itemId)
	{
		return Sets.Where(set => set.RequiredItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase)).ToList();
	}

	public static (int OwnedCount, int TotalRequired) GetSetProgress(ChestSetDefinition set, IReadOnlyCollection<string> ownedItems)
	{
		if (set == null || set.RequiredItemIds == null)
			return (0, 0);

		int ownedCount = set.RequiredItemIds.Count(id => ownedItems != null && ownedItems.Contains(id, StringComparer.OrdinalIgnoreCase));
		return (ownedCount, set.RequiredItemIds.Length);
	}

	/// <param name="unavailable">
	/// Relics that cannot be claimed right now and must not be dealt, owned or not. The Scribe's
	/// Penknife is the case: with a free slot in the book, or nothing in it but the wizard's own
	/// spell, it has nothing to cut and would be a dead card (Player.CanUsePenknife).
	/// </param>
	/// <summary>How much a relic's offer weight keeps each time a chest of this run has shown it and it was passed over.</summary>
	/// <remarks>
	/// THE SAME CARDS, CHEST AFTER CHEST (2026-10-28). Eric: "i see a lot of the same items in the same
	/// game. do i have to unlock more or do we need more items added to game?" Neither: relics have no
	/// unlocks and 34 is a full pool. A card passed over went straight back at full weight, and a common
	/// is weighted 12.5 times a Relic-tier piece, so the nine commons filled about half of every run's
	/// cards and a third of chests repeated a card from the chest before. Simulated over 10,000 runs of
	/// 15 chests, a random picker: 25 of 33 relics shown before, the most-shown 4.9 times, 4.9 chests
	/// sharing a card with the last; with this fade at 0.5 and the last chest's cards left out, 28.7 of
	/// 33, 3.3, and none. Rarity still decides most of it - Relic-tier cards went from 4% of all shown
	/// to 6%, Rare from 19% to 24% - so this is a little more power at the margins as well as variety.
	/// </remarks>
	public const float ShownOfferFade = 0.5f;

	public static List<string> GetChestItemOptions(IReadOnlyCollection<string> ownedItems, RandomNumberGenerator rng = null, int count = 3,
		IReadOnlyCollection<string> unavailable = null, ChestOfferMemory memory = null)
	{
		if (rng == null)
			rng = new RandomNumberGenerator();

		var ownedSet = new HashSet<string>(ownedItems ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		var blocked = new HashSet<string>(unavailable ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		// The last chest's cards sit this one out, unless that would leave too few to fill it.
		var unowned = AllItemIds.Where(id => !ownedSet.Contains(id) && !blocked.Contains(id)
			&& memory?.WasInLastOffer(id) != true).ToList();
		if (memory != null && unowned.Count < count)
			unowned = AllItemIds.Where(id => !ownedSet.Contains(id) && !blocked.Contains(id)).ToList();

		// Weighted by rarity rather than shuffled flat. A plain shuffle offered a Relic exactly as
		// often as the most ordinary trinket, which is the same as having no rarity at all.
		//
		// Each candidate draws a key of rng^(1/weight) and the highest keys win - weighted sampling
		// without replacement, so one roll cannot offer the same item twice and a low-weight item is
		// rare rather than impossible.
		// A card already shown this run and passed over counts for less each time (ShownOfferFade).
		var pool = unowned
			.OrderByDescending(id => MathF.Pow(MathF.Max(0.0001f, rng.Randf()),
				1f / (OfferWeightFor(GetRarity(id)) * MathF.Pow(ShownOfferFade, memory?.TimesShown(id) ?? 0))))
			.ToList();

		// Padding with owned relics once the pool runs dry. Never a tome relic: an owned Folio adds no
		// second page and a spent knife cuts nothing, so either one would be a card that does nothing.
		if (pool.Count < count)
		{
			var remaining = AllItemIds
				.Where(id => !pool.Contains(id, StringComparer.OrdinalIgnoreCase) && !blocked.Contains(id) && !IsTomeRelic(id))
				.OrderBy(_ => rng.Randf()).ToList();
			pool.AddRange(remaining);
		}

		List<string> offer = pool.Take(count).ToList();
		memory?.Record(offer);
		return offer;
	}

	// The no-menu fallback's roll (ChestReward). Never a tome relic: they only do anything through
	// the chest screen - the knife needs a spell named, and it is claimed by naming one.
	public static string GetRandomItem(RandomNumberGenerator rng)
	{
		if (rng == null)
			rng = new RandomNumberGenerator();

		var pool = AllItemIds.Where(id => !IsTomeRelic(id)).ToList();
		return pool[rng.RandiRange(0, pool.Count - 1)];
	}
}

// One concrete, numeric bonus granted by a completed set, kept as data so the Full Set Enchantment screen can
// list exactly what the player gets instead of paraphrasing it in prose.
//
// IMPORTANT: these mirror Player.RefreshChestSetEffects, which is the implementation. If you change
// a bonus there, change it here - RegressionChecks only verifies that every set declares at least
// one effect, not that the numbers agree.
public sealed class ChestSetEffect
{
	// What is being changed, e.g. "Spell damage".
	public required string Label { get; init; }
	// The signed, unit-bearing magnitude, e.g. "+18%" or "below 30% HP".
	public required string Value { get; init; }
}

public sealed class ChestSetDefinition
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required string[] RequiredItemIds { get; init; }
	// What the WHOLE set does, as one plain sentence with its numbers - never flavour. It is the
	// line a player reads on a chest card to decide whether finishing the set is worth a pick, so
	// it has to answer "what do I get" by itself. The object's identity lives in Name ("The
	// Brazier"); the line-by-line breakdown lives in Effects, and the two must agree with each
	// other and with Player.RefreshChestSetEffects. The partial tier is not repeated here - it has
	// its own PartialEffects rows.
	public required string Description { get; init; }
	public required string IconPath { get; init; }
	public required ChestSetEffect[] Effects { get; init; }

	/// <summary>
	/// Element tags granted when the whole set is assembled.
	/// </summary>
	/// <remarks>
	/// A completed set is the largest single commitment the item system asks for, so it pays the
	/// largest tag reward - two at once, enough to cross a threshold on its own. It is also the
	/// right home for the scarcest elements: a Metal set finishing into Metal is a better story
	/// than a Metal trinket turning up in a chest.
	///
	/// Not counted by RegressionChecks.ValidateElementReachability, deliberately. That check asks
	/// whether an element can be reached *reliably*, and a set depends on finding four specific
	/// items. Sets are a bonus on top of a guaranteed floor, never the floor itself.
	/// </remarks>
	public (string Element, int Weight)[] ElementTags { get; init; } = System.Array.Empty<(string, int)>();

	/// <summary>
	/// How many pieces earn the partial bonus. 0 means the set has none.
	/// </summary>
	/// <remarks>
	/// Only the three-piece sets carry one, and that falls out of the data rather than being a
	/// judgement call: on a two-piece set the "partial" would be one item, which is just owning
	/// the item. So the six three-piece sets pay a taste at two pieces, and the four two-piece
	/// sets stay all-or-nothing and keep a single unique effect as their whole identity.
	///
	/// The problem this solves: three pieces of a four-piece set used to be worth exactly
	/// nothing, and the player had no way to tell they were close. A partial makes progress
	/// legible without weakening the payoff, because the full effect is unchanged.
	/// </remarks>
	public int PartialItemCount { get; init; } = 0;

	/// <summary>Display rows for the partial bonus, same shape as <see cref="Effects"/>.</summary>
	public ChestSetEffect[] PartialEffects { get; init; } = System.Array.Empty<ChestSetEffect>();
}

/// <summary>
/// What one run's chests have already shown, so the next chest leans away from it
/// (ChestItemCatalog.ShownOfferFade). Held by the run (Node2DGame) and by the balance bot, which
/// deals its own chests and must deal them by the same rule.
/// </summary>
public sealed class ChestOfferMemory
{
	private readonly Dictionary<string, int> shown = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> lastOffer = new(StringComparer.OrdinalIgnoreCase);

	public int TimesShown(string id) => shown.TryGetValue(id, out int n) ? n : 0;

	public bool WasInLastOffer(string id) => lastOffer.Contains(id);

	public void Record(IEnumerable<string> offer)
	{
		lastOffer.Clear();
		foreach (string id in offer)
		{
			shown[id] = TimesShown(id) + 1;
			lastOffer.Add(id);
		}
	}
}
