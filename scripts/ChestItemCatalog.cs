using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class ChestItemCatalog
{
	public const string RelicKey = "relic_key";
	public const string AegisSigil = "aegis_sigil";
	public const string EmberFlask = "ember_flask";
	public const string StormLattice = "storm_lattice";
	public const string InfernoCore = "inferno_core";
	public const string IronFang = "iron_fang";

	public const string VaultguardSetId = "vaultguard";
	public const string EmberlineSetId = "emberline";
	public const string StormboundSetId = "stormbound";
	public const string BastionOfSpikesSetId = "bastion_of_spikes";

	public static readonly IReadOnlyList<string> AllItemIds = new[]
	{
		RelicKey,
		AegisSigil,
		EmberFlask,
		StormLattice,
		InfernoCore,
		IronFang
	};

	public static readonly IReadOnlyList<ChestSetDefinition> Sets = new[]
	{
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
		}
	};

	public static string GetDisplayName(string itemId)
	{
		return itemId switch
		{
			RelicKey => "Relic Key",
			AegisSigil => "Aegis Sigil",
			EmberFlask => "Ember Flask",
			StormLattice => "Storm Lattice",
			InfernoCore => "Inferno Core",
			IronFang => "Iron Fang",
			_ => itemId
		};
	}

	public static string GetDescription(string itemId)
	{
		return itemId switch
		{
			RelicKey => "Passively expands pickup magnet range by +18.",
			AegisSigil => "Passively reduces incoming damage taken by +8%.",
			EmberFlask => "Passively increases spell damage by +12%.",
			StormLattice => "Passively increases spell critical chance by +8%.",
			InfernoCore => "Passively expands spell area of effect by +12%.",
			IronFang => "Passively reduces incoming damage taken by +6%.",
			_ => "A powerful relic acquired from a treasure chest."
		};
	}

	public static string GetIconPath(string itemId)
	{
		return itemId switch
		{
			RelicKey => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-keys-1-1.png",
			AegisSigil => "res://assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-8-self-shield-shield.png",
			EmberFlask => "res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-flasks-1-1.png",
			StormLattice => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-3-lightning-strike2.png",
			InfernoCore => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-1-fire-ball2.png",
			IronFang => "res://assets/organized/ui/ui-10-magic-sprite-sheet-effects-pixel-art-icons-that-go-with-the-spells-6-spikes.png",
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
