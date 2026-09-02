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
		CrystalPrism
	};

	// Every Effects list below is transcribed from Player.RefreshChestSetEffects. Where the old
	// flavour text promised a mechanic the code never implemented (Vaultguard's per-chest shield,
	// Emberline's per-cast growth, Stormbound's chaining crits) the entry states what actually
	// happens, so the synergy screen cannot mislead the player.
	private const string SetIconRoot = "res://assets/organized/ui/ui-png-iconsmenu-";

	public static readonly IReadOnlyList<ChestSetDefinition> Sets = new[]
	{
		// Original sets
		new ChestSetDefinition
		{
			Id = VaultguardSetId,
			Name = "Vaultguard",
			RequiredItemIds = new[] { RelicKey, AegisSigil, IronFang },
			Description = "Treasure Ward: every chest you crack open shields and mends you.",
			IconPath = SetIconRoot + "3.png",
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
			Name = "Emberline",
			RequiredItemIds = new[] { EmberFlask, InfernoCore, RelicKey },
			Description = "Flamebound Cache: each cast stokes the fire a little hotter.",
			IconPath = SetIconRoot + "24.png",
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
			Name = "Stormbound",
			RequiredItemIds = new[] { StormLattice, InfernoCore, AegisSigil },
			Description = "Arc Ward: critical hits jump, and a live shield speeds your step.",
			IconPath = SetIconRoot + "8.png",
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
			Name = "Bastion of Spikes",
			RequiredItemIds = new[] { AegisSigil, IronFang, EmberFlask },
			Description = "Crimson Bastion: wounds turn your armour outward.",
			IconPath = SetIconRoot + "19.png",
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
			Name = "Deathbringer",
			RequiredItemIds = new[] { WrathAmulet, SpectralFang },
			Description = "Lethal Strike: wounded enemies do not survive the follow-up.",
			IconPath = SetIconRoot + "20.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Spell damage", Value = "+25%" },
				new ChestSetEffect { Label = "Execute enemies", Value = "below 30% HP" }
			}
		},
		new ChestSetDefinition
		{
			Id = EternalGuardianSetId,
			Name = "Eternal Guardian",
			RequiredItemIds = new[] { AegisCrown, BasaltCarapace, ProtectiveWard },
			Description = "Fortress Ward: a deeper health pool behind thicker plate.",
			IconPath = SetIconRoot + "11.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Max HP", Value = "+80" },
				new ChestSetEffect { Label = "Damage taken", Value = "-18%" }
			}
		},
		new ChestSetDefinition
		{
			Id = LifeDrainSetId,
			Name = "Life Drain",
			RequiredItemIds = new[] { EssenceChalice, HeartOfRenewal },
			Description = "Endless Harvest: the swarm sustains you as it falls.",
			IconPath = SetIconRoot + "13.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Heal per kill", Value = "+1 HP" }
			}
		},
		new ChestSetDefinition
		{
			Id = ElementalMasterySetId,
			Name = "Elemental Mastery",
			RequiredItemIds = new[] { CrystalPrism, FrozenTear, Thunderstone },
			Description = "Prismatic Force: every element answers more sharply.",
			IconPath = SetIconRoot + "5.png",
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
			Name = "Speed Demon",
			RequiredItemIds = new[] { QuicksilverPendant, HasteRune },
			Description = "Swift Strike: you move and cast faster than the swarm can answer.",
			IconPath = SetIconRoot + "9.png",
			Effects = new[]
			{
				new ChestSetEffect { Label = "Move speed", Value = "+20%" },
				new ChestSetEffect { Label = "Attack speed", Value = "+15%" }
			}
		},
		new ChestSetDefinition
		{
			Id = FortunesFavorSetId,
			Name = "Fortune's Favor",
			RequiredItemIds = new[] { LuckyCoin, CompassRose },
			Description = "Blessed Find: the run gives up its rewards more readily.",
			IconPath = SetIconRoot + "14.png",
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
			// Damage items
			RelicKey => "Passively expands pickup magnet range by +18.",
			EmberFlask => "Passively increases spell damage by +12%.",
			WrathAmulet => "Passively increases spell damage by +8%.",
			EtherealBlade => "Passively increases critical damage multiplier by +0.3x.",
			SpectralFang => "Passively increases spell damage by +15% against enemies below 50% HP.",
			ObsidianHeart => "Passively increases spell damage by +10%, but reduces movement speed by 5%.",
			// Defense items
			AegisSigil => "Passively reduces incoming damage taken by +8%.",
			IronFang => "Passively reduces incoming damage taken by +6%.",
			BasaltCarapace => "Passively reduces incoming damage taken by +10%.",
			AegisCrown => "Passively increases maximum HP by +30.",
			IronhideCloak => "Passively reduces incoming damage taken by +7%.",
			ProtectiveWard => "Passively grants +15% damage reduction while a shield is active.",
			// Healing & Recovery items
			VialOfVitality => "Passively increases all healing received by +20%.",
			HeartOfRenewal => "Passively increases maximum HP by +50 and regeneration by +0.5/sec.",
			Phylactery => "Grants a one-time revive per stage, restoring 25% HP.",
			EssenceChalice => "Gain +1 HP for each enemy killed (max +50).",
			// Utility items
			QuicksilverPendant => "Passively increases movement speed by +15%.",
			HasteRune => "Passively increases attack speed by +12%.",
			CompassRose => "Passively increases XP gain by +15%.",
			LuckyCoin => "Passively increases chest item drop rates by +20%.",
			// Elemental items
			StormLattice => "Passively increases spell critical chance by +8%.",
			InfernoCore => "Passively expands spell area of effect by +12%.",
			FrozenTear => "Passively increases ice duration by +40% and slow potency by +15%.",
			Thunderstone => "Passively increases lightning chain radius by +50% and chain count by +1.",
			CrystalPrism => "Passively increases all elemental effect potency by +20%.",
			_ => "A powerful relic acquired from a treasure chest."
		};
	}

	public static string GetIconPath(string itemId)
	{
		return itemId switch
		{
			// Damage items
			RelicKey => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-keys-1-1.png",
			EmberFlask => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-flasks-1-1.png",
			WrathAmulet => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-10-fire-ball2.png",
			EtherealBlade => "res://assets/organized/ui/ui-png-iconsmenu-4.png",
			SpectralFang => "res://assets/organized/ui/ui-png-iconsmenu-12.png",
			ObsidianHeart => "res://assets/organized/ui/ui-png-skills-icon-11.jpg",
			// Defense items
			AegisSigil => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-8-shield.png",
			IronFang => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-6-spikes.png",
			BasaltCarapace => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-6-spikes2.png",
			AegisCrown => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-3.png",
			IronhideCloak => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-8-shield2.png",
			ProtectiveWard => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-8-shield2.png",
			// Healing & Recovery items
			VialOfVitality => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-flasks-1-2.png",
			HeartOfRenewal => "res://assets/organized/ui/ui-png-skills-icon-2.png",
			Phylactery => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-4.png",
			EssenceChalice => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-2.png",
			// Utility items
			QuicksilverPendant => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-1-lightning2.png",
			HasteRune => "res://assets/organized/ui/ui-png-elements2-2.png",
			CompassRose => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-keys-1-2.png",
			LuckyCoin => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-3-midas-touch2.png",
			// Elemental items
			StormLattice => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-2-lightning-from-above.png",
			InfernoCore => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-10-fire-ball2.png",
			FrozenTear => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-1-lightning2.png",
			Thunderstone => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-2-lightning-from-above-1.png",
			CrystalPrism => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-3-midas-touch.png",
			_ => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-1.png"
		};
	}

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

		var pool = new List<string>();
		var shuffledUnowned = unowned.OrderBy(_ => rng.Randf()).ToList();
		pool.AddRange(shuffledUnowned);

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
}
