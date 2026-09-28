using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

	// Synergy set IDs
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
		WormwoodTithe
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

			// Uncommon
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

			_ => Array.Empty<(string, int)>(),
		};
	}

	// Every Effects list below is transcribed from Player.RefreshChestSetEffects. Where the old
	// flavour text promised a mechanic the code never implemented (Vaultguard's per-chest shield,
	// Emberline's per-cast growth, Stormbound's chaining crits) the entry states what actually
	// happens, so the synergy screen cannot mislead the player.
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
			Name = "Vaultguard",
			RequiredItemIds = new[] { RelicKey, AegisSigil, IronFang },
			Description = "Treasure Ward: every chest you crack open shields and mends you.",
			IconPath = SetIconRoot + "vaultguard.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Damage taken", Value = "-12%" },
				new ChestSetEffect { Label = "Shield per chest opened", Value = "6 points" },
				new ChestSetEffect { Label = "Heal per chest opened", Value = "+6 HP" }
			}
		},
		new ChestSetDefinition
		{
			Id = EmberlineSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Spell damage", Value = "+6%" } },
			ElementTags = new[] { ("Fire", 1), ("Light", 1) },
			Name = "Emberline",
			RequiredItemIds = new[] { EmberFlask, InfernoCore, RelicKey },
			Description = "Flamebound Cache: each cast stokes the fire a little hotter.",
			IconPath = SetIconRoot + "emberline.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Spell damage per cast", Value = "+0.6%" },
				new ChestSetEffect { Label = "Spell area per cast", Value = "+0.4%" },
				new ChestSetEffect { Label = "Ramp caps at", Value = "+18% damage, +12% area" }
			}
		},
		new ChestSetDefinition
		{
			Id = StormboundSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Crit chance", Value = "+4%" } },
			ElementTags = new[] { ("Lightning", 1), ("Metal", 1) },
			Name = "Stormbound",
			RequiredItemIds = new[] { StormLattice, InfernoCore, AegisSigil },
			Description = "Arc Ward: critical hits jump, and a live shield speeds your step.",
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
			Name = "Bastion of Spikes",
			RequiredItemIds = new[] { AegisSigil, IronFang, EmberFlask },
			Description = "Crimson Bastion: wounds turn your armour outward.",
			IconPath = SetIconRoot + "bastion_of_spikes.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Damage taken", Value = "-15%" },
				new ChestSetEffect { Label = "Retaliate when hit", Value = "below 30% HP" }
			}
		},
		// New sets
		new ChestSetDefinition
		{
			Id = DeathbringerSetId,
			ElementTags = new[] { ("Darkness", 1), ("Poison", 1) },
			Name = "Deathbringer",
			RequiredItemIds = new[] { WrathAmulet, SpectralFang },
			Description = "Lethal Strike: wounded enemies do not survive the follow-up.",
			IconPath = SetIconRoot + "deathbringer.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Spell damage", Value = "+25%" },
				new ChestSetEffect { Label = "Execute enemies", Value = "below 30% HP" }
			}
		},
		new ChestSetDefinition
		{
			Id = EternalGuardianSetId,
			PartialItemCount = 2,
			PartialEffects = new[] { new ChestSetEffect { Label = "Max HP", Value = "+30" } },
			ElementTags = new[] { ("Light", 1), ("Metal", 1) },
			Name = "Eternal Guardian",
			RequiredItemIds = new[] { AegisCrown, BasaltCarapace, ProtectiveWard },
			Description = "Fortress Ward: a deeper health pool behind thicker plate.",
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
			ElementTags = new[] { ("Darkness", 1), ("Water", 1) },
			Name = "Life Drain",
			RequiredItemIds = new[] { EssenceChalice, HeartOfRenewal },
			Description = "Endless Harvest: the swarm sustains you as it falls.",
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
			PartialEffects = new[] { new ChestSetEffect { Label = "Elemental potency", Value = "+15%" } },
			ElementTags = new[] { ("Arcane", 1), ("Ice", 1) },
			Name = "Elemental Mastery",
			RequiredItemIds = new[] { CrystalPrism, FrozenTear, Thunderstone },
			Description = "Prismatic Force: every element answers more sharply.",
			IconPath = SetIconRoot + "elemental_mastery.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Elemental potency", Value = "+40%" },
				new ChestSetEffect { Label = "Ice slow duration", Value = "+40%" },
				new ChestSetEffect { Label = "Ice slow strength", Value = "+20%" },
				new ChestSetEffect { Label = "Chain lightning radius", Value = "+50%" },
				new ChestSetEffect { Label = "Chain lightning targets", Value = "+1" }
			}
		},
		new ChestSetDefinition
		{
			Id = SpeedDemonSetId,
			ElementTags = new[] { ("Wind", 1), ("Grass", 1) },
			Name = "Speed Demon",
			RequiredItemIds = new[] { QuicksilverPendant, HasteRune },
			Description = "Swift Strike: you move and cast faster than the swarm can answer.",
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
			ElementTags = new[] { ("Water", 1), ("Light", 1) },
			Name = "Fortune's Favor",
			RequiredItemIds = new[] { LuckyCoin, CompassRose },
			Description = "Blessed Find: the run gives up its rewards more readily.",
			IconPath = SetIconRoot + "fortunes_favor.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Item drop rate", Value = "+25%" },
				new ChestSetEffect { Label = "XP gained", Value = "+25%" }
			}
		}
	};

	public static string GetDisplayName(string itemId)
	{
		return itemId switch
		{
			// Damage items
			CrackedPrism => "Cracked Prism",
			DuellistsChalk => "Duellist's Chalk",
			HoarfrostNail => "Hoarfrost Nail",
			WormwoodTithe => "Wormwood Tithe",
			RelicKey => "Relic Key",
			EmberFlask => "Ember Flask",
			WrathAmulet => "Wrath Amulet",
			EtherealBlade => "Ethereal Blade",
			SpectralFang => "Spectral Fang",
			ObsidianHeart => "Obsidian Heart",
			// Defense items
			AegisSigil => "Aegis Sigil",
			IronFang => "Iron Fang",
			BasaltCarapace => "Basalt Carapace",
			AegisCrown => "Aegis Crown",
			IronhideCloak => "Ironhide Cloak",
			ProtectiveWard => "Protective Ward",
			// Healing & Recovery items
			VialOfVitality => "Vial of Vitality",
			HeartOfRenewal => "Heart of Renewal",
			Phylactery => "Phylactery",
			EssenceChalice => "Essence Chalice",
			// Utility items
			QuicksilverPendant => "Quicksilver Pendant",
			HasteRune => "Haste Rune",
			CompassRose => "Compass Rose",
			LuckyCoin => "Lucky Coin",
			// Elemental items
			StormLattice => "Storm Lattice",
			InfernoCore => "Inferno Core",
			FrozenTear => "Frozen Tear",
			Thunderstone => "Thunderstone",
			CrystalPrism => "Crystal Prism",
			_ => itemId
		};
	}

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
			VialOfVitality => "Healing past full becomes shield instead of being wasted.",
			HeartOfRenewal => "+0.8 health per second.",
			Phylactery => "One revive per stage, at 25% health.",
			EssenceChalice => "+1 maximum health per kill, up to +50.",
			QuicksilverPendant => "+15% movement speed.",
			HasteRune => "+12% attack speed.",
			CompassRose => "+15% XP.",
			LuckyCoin => "+20% chest item drop rate.",
			StormLattice => "+8% critical chance.",
			InfernoCore => "+12% spell area.",
			FrozenTear => "+40% ice duration, +15% slow strength.",
			Thunderstone => "+50% lightning chain range, +1 chain.",
			CrystalPrism => "+30% damage from spells that carry only one element.",
			CrackedPrism => "+1 projectile, -25% spell damage.",
			DuellistsChalk => "Always crits an enemy at full health.",
			HoarfrostNail => "+25% damage to slowed or rooted enemies.",
			WormwoodTithe => "+39% enemies, +10% enemy speed, +30% XP, +25% drops, +3 luck.",
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
			_ => "res://assets/bonelight/ui/spells/unknown.png"
		};
	}

	// All twenty-nine are generated by tools/art/relic_icons.py. They used to be slices of a
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

	public static List<string> GetChestItemOptions(IReadOnlyCollection<string> ownedItems, RandomNumberGenerator rng = null, int count = 3)
	{
		if (rng == null)
			rng = new RandomNumberGenerator();

		var ownedSet = new HashSet<string>(ownedItems ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		var unowned = AllItemIds.Where(id => !ownedSet.Contains(id)).ToList();

		// Weighted by rarity rather than shuffled flat. A plain shuffle offered a Relic exactly as
		// often as the most ordinary trinket, which is the same as having no rarity at all.
		//
		// Each candidate draws a key of rng^(1/weight) and the highest keys win - weighted sampling
		// without replacement, so one roll cannot offer the same item twice and a low-weight item is
		// rare rather than impossible.
		var pool = unowned
			.OrderByDescending(id => MathF.Pow(MathF.Max(0.0001f, rng.Randf()), 1f / OfferWeightFor(GetRarity(id))))
			.ToList();

		if (pool.Count < count)
		{
			var remaining = AllItemIds.Where(id => !pool.Contains(id, StringComparer.OrdinalIgnoreCase)).OrderBy(_ => rng.Randf()).ToList();
			pool.AddRange(remaining);
		}

		return pool.Take(count).ToList();
	}

	public static string GetRandomItem(RandomNumberGenerator rng)
	{
		if (rng == null)
			rng = new RandomNumberGenerator();

		int index = rng.RandiRange(0, AllItemIds.Count - 1);
		return AllItemIds[index];
	}
}

// One concrete, numeric bonus granted by a completed set, kept as data so the synergy screen can
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
	// One short line of flavour. The precise numbers live in Effects, not here.
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
