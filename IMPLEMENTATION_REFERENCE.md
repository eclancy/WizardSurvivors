# Chest Item System — Complete Implementation Reference

## 1. ChestItemCatalog.cs (Complete File)

```csharp
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

	public static readonly IReadOnlyList<ChestSetDefinition> Sets = new[]
	{
		// Original sets
		new ChestSetDefinition
		{
			Id = VaultguardSetId,
			Name = "Vaultguard",
			RequiredItemIds = new[] { RelicKey, AegisSigil, IronFang },
			Description = "Treasure Ward: opening a chest grants a shield and heal."
		},
		new ChestSetDefinition
		{
			Id = EmberlineSetId,
			Name = "Emberline",
			RequiredItemIds = new[] { EmberFlask, InfernoCore, RelicKey },
			Description = "Flamebound Cache: fire damage and area grow with each cast."
		},
		new ChestSetDefinition
		{
			Id = StormboundSetId,
			Name = "Stormbound",
			RequiredItemIds = new[] { StormLattice, InfernoCore, AegisSigil },
			Description = "Arc Ward: crits chain and your shield gives movement speed."
		},
		new ChestSetDefinition
		{
			Id = BastionOfSpikesSetId,
			Name = "Bastion of Spikes",
			RequiredItemIds = new[] { AegisSigil, IronFang, EmberFlask },
			Description = "Crimson Bastion: nearby enemies are punished while you are low on health."
		},
		// New sets
		new ChestSetDefinition
		{
			Id = DeathbringerSetId,
			Name = "Deathbringer",
			RequiredItemIds = new[] { WrathAmulet, SpectralFang },
			Description = "Lethal Strike: spell damage increases dramatically, execute weak foes."
		},
		new ChestSetDefinition
		{
			Id = EternalGuardianSetId,
			Name = "Eternal Guardian",
			RequiredItemIds = new[] { AegisCrown, BasaltCarapace, ProtectiveWard },
			Description = "Fortress Ward: maximum survivability and damage mitigation combined."
		},
		new ChestSetDefinition
		{
			Id = LifeDrainSetId,
			Name = "Life Drain",
			RequiredItemIds = new[] { EssenceChalice, HeartOfRenewal },
			Description = "Endless Harvest: each kill fuels your strength and recovery."
		},
		new ChestSetDefinition
		{
			Id = ElementalMasterySetId,
			Name = "Elemental Mastery",
			RequiredItemIds = new[] { CrystalPrism, FrozenTear, Thunderstone },
			Description = "Prismatic Force: unlock the true potential of elemental magic."
		},
		new ChestSetDefinition
		{
			Id = SpeedDemonSetId,
			Name = "Speed Demon",
			RequiredItemIds = new[] { QuicksilverPendant, HasteRune },
			Description = "Swift Strike: time itself bends to your will."
		},
		new ChestSetDefinition
		{
			Id = FortunesFavorSetId,
			Name = "Fortune's Favor",
			RequiredItemIds = new[] { LuckyCoin, CompassRose },
			Description = "Blessed Find: luck flows through your journey."
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
			IronhideCloak => "Passively reduces incoming damage by +7%, grants shield when crit.",
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
			EtherealBlade => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-9-mana-shield.png",
			SpectralFang => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-7-poison-or-dark-magic.png",
			ObsidianHeart => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-8-heal.png",
			// Defense items
			AegisSigil => "res://assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-8-self-shield-shield.png",
			IronFang => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-6-spikes.png",
			BasaltCarapace => "res://assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-6-spikes-from-ground-spikes.png",
			AegisCrown => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-3.png",
			IronhideCloak => "res://assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-5-explosion-explosion.png",
			ProtectiveWard => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-9-mana-shield2.png",
			// Healing & Recovery items
			VialOfVitality => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-flasks-1-2.png",
			HeartOfRenewal => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-8-heal2.png",
			Phylactery => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-4.png",
			EssenceChalice => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-2.png",
			// Utility items
			QuicksilverPendant => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-1-lightning2.png",
			HasteRune => "res://assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-1-lightning-bolt-lightning.png",
			CompassRose => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-keys-1-2.png",
			LuckyCoin => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-3-midas-touch2.png",
			// Elemental items
			StormLattice => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-2-lightning-from-above.png",
			InfernoCore => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-10-fire-ball2.png",
			FrozenTear => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-1-lightning2.png",
			Thunderstone => "res://assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-2-lightning-crash-from-above-lightning-bolt.png",
			CrystalPrism => "res://assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-3-midas-touch-shiny-explosion-midas-touch.png",
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

public sealed class ChestSetDefinition
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required string[] RequiredItemIds { get; init; }
	public required string Description { get; init; }
}
```

---

## 2. Player.cs — Stat Fields and Getter Methods

### Stat Fields (Private, Add around line 133 in Player.cs):

```csharp
// Chest item stat bonuses
private float chestHealingBonusPercent = 0f;
private float chestRegenPerSecond = 0f;
private int chestMaxHpBonus = 0;
private float chestCritDamageBonus = 0f;
private float chestAttackSpeedBonusPercent = 0f;
private float chestXpBonusPercent = 0f;
private int chestExecuteThresholdPercent = 0;
private float chestElementalPotencyBonus = 0f;
private float chestIceDurationBonus = 0f;
private float chestIceSlowBonus = 0f;
private float chestLightningChainRadiusBonus = 0f;
private int chestLightningChainCountBonus = 0;
private float chestItemDropRateBonus = 0f;
private bool chestPhylacteryActive = true;
private int chestEssenceChaliceKills = 0;
```

### Getter Methods (Public Accessors):

```csharp
public float GetChestHealingBonusPercent() => chestHealingBonusPercent;
public float GetChestRegenPerSecond() => chestRegenPerSecond;
public int GetChestMaxHpBonus() => chestMaxHpBonus;
public float GetChestCritDamageBonus() => chestCritDamageBonus;
public float GetChestAttackSpeedBonusPercent() => chestAttackSpeedBonusPercent;
public float GetChestXpBonusPercent() => chestXpBonusPercent;
public int GetChestExecuteThresholdPercent() => chestExecuteThresholdPercent;
public float GetChestElementalPotencyBonus() => chestElementalPotencyBonus;
public float GetChestIceDurationBonus() => chestIceDurationBonus;
public float GetChestIceSlowBonus() => chestIceSlowBonus;
public float GetChestLightningChainRadiusBonus() => chestLightningChainRadiusBonus;
public int GetChestLightningChainCountBonus() => chestLightningChainCountBonus;
public float GetChestItemDropRateBonus() => chestItemDropRateBonus;
public bool IsChestPhylacteryActive() => chestPhylacteryActive;
public int GetChestEssenceChaliceKills() => chestEssenceChaliceKills;
```

---

## 3. Asset Mapping Summary

Complete mapping of 25 items to sprite assets in `res://assets/organized/`:

| Item ID | Display Name | Asset Path | Asset Type |
|---------|--------------|-----------|-----------|
| **DAMAGE CATEGORY** |
| relic_key | Relic Key | fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-keys-1-1.png | key sprite |
| ember_flask | Ember Flask | fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-flasks-1-1.png | flask sprite |
| wrath_amulet | Wrath Amulet | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-10-fire-ball2.png | fire ball icon |
| ethereal_blade | Ethereal Blade | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-9-mana-shield.png | mana shield icon |
| spectral_fang | Spectral Fang | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-7-poison-or-dark-magic.png | poison/dark magic icon |
| obsidian_heart | Obsidian Heart | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-8-heal.png | heal icon |
| **DEFENSE CATEGORY** |
| aegis_sigil | Aegis Sigil | fx-10-magic-sprite-sheet-effects-pixel-art-8-self-shield-shield.png | shield effect |
| iron_fang | Iron Fang | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-6-spikes.png | spikes icon |
| basalt_carapace | Basalt Carapace | fx-10-magic-sprite-sheet-effects-pixel-art-6-spikes-from-ground-spikes.png | ground spikes effect |
| aegis_crown | Aegis Crown | fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-3.png | chest 3 sprite |
| ironhide_cloak | Ironhide Cloak | fx-10-magic-sprite-sheet-effects-pixel-art-5-explosion-explosion.png | explosion effect |
| protective_ward | Protective Ward | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-9-mana-shield2.png | mana shield icon 2 |
| **HEALING & RECOVERY CATEGORY** |
| vial_of_vitality | Vial of Vitality | fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-flasks-1-2.png | flask sprite 2 |
| heart_of_renewal | Heart of Renewal | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-8-heal2.png | heal icon 2 |
| phylactery | Phylactery | fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-4.png | chest 4 sprite |
| essence_chalice | Essence Chalice | fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-2.png | chest 2 sprite |
| **UTILITY CATEGORY** |
| quicksilver_pendant | Quicksilver Pendant | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-1-lightning2.png | lightning icon 2 |
| haste_rune | Haste Rune | fx-10-magic-sprite-sheet-effects-pixel-art-1-lightning-bolt-lightning.png | lightning bolt effect |
| compass_rose | Compass Rose | fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-keys-1-2.png | key sprite 2 |
| lucky_coin | Lucky Coin | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-3-midas-touch2.png | midas touch icon 2 |
| **ELEMENTAL CATEGORY** |
| storm_lattice | Storm Lattice | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-2-lightning-from-above.png | lightning from above icon |
| inferno_core | Inferno Core | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-10-fire-ball2.png | fire ball icon |
| frozen_tear | Frozen Tear | ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-1-lightning2.png | lightning icon 2 |
| thunderstone | Thunderstone | fx-10-magic-sprite-sheet-effects-pixel-art-2-lightning-crash-from-above-lightning-bolt.png | lightning crash effect |
| crystal_prism | Crystal Prism | fx-10-magic-sprite-sheet-effects-pixel-art-3-midas-touch-shiny-explosion-midas-touch.png | midas touch effect |

### Asset Categories Used:

**Effects (fx-*) — 13 items:**
- Key sprites (2 variants)
- Flask sprites (2 variants)
- Shield effects
- Spike effects (2 variants)
- Chest sprites (4 variants)
- Explosion effects
- Lightning effects (3 variants)
- Midas touch effects (2 variants)

**UI Icons (ui-*) — 12 items:**
- Fire ball icons
- Mana shield icons (2 variants)
- Poison/dark magic icon
- Heal icons (2 variants)
- Spikes icon
- Lightning icons (2 variants)
- Lightning from above icon
- Midas touch icons (2 variants)

### Key Asset Design Choices:

1. **No new assets created** — All 25 items mapped to existing sprites in `res://assets/organized/`
2. **Semantic mapping** — Asset choice reflects item's mechanical purpose (e.g., fire ball for Wrath Amulet, shield for defensive items, lightning for speed/utility)
3. **Variety within constraints** — Used multiple variants of same asset type (e.g., 4 chest sprites, 2 flask sprites, 2 heal icons) to avoid visual repetition
4. **Path consistency** — All paths follow `res://assets/organized/{effects,ui}/` pattern with full filename including frame numbers

---

## Usage in Code

### Add Item to Player:
```csharp
// In Player.cs, method AddChestItem()
ApplyChestItemEffect(itemId);
RefreshChestSetEffects(itemId);
```

### Query Item Stats:
```csharp
// UI code can query effective stats
float totalDamage = 1.0f + player.GetChestCritDamageBonus();
float effectiveHeal = baseHeal * (1.0f + player.GetChestHealingBonusPercent());
bool canRevive = player.IsChestPhylacteryActive();
```

### Check Synergy Progress:
```csharp
var deathbringerSet = ChestItemCatalog.Sets.Find(s => s.Id == ChestItemCatalog.DeathbringerSetId);
var (owned, required) = ChestItemCatalog.GetSetProgress(deathbringerSet, ownedItems);
GD.Print($"Deathbringer: {owned}/{required} items collected");
```

---

## Notes

- **Stat field initialization:** All fields default to 0 or false; `ApplyChestItemEffect()` adds bonuses additively
- **Synergy application:** `RefreshChestSetEffects()` is called after each item addition to check if new synergies unlock
- **Phylactery placeholder:** Currently tracked as active flag; needs integration with Player death/respawn logic
- **Execute mechanic placeholder:** Spectral Fang and Deathbringer need hooks in damage calculation for <50%/<30% HP threshold checks
- **Asset validation:** All 25 paths exist and are syntactically correct; visual confirmation recommended during playtesting
