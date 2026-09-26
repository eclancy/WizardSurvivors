using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>What a boon does. Interpreted by <c>Player.ApplyBoon</c>.</summary>
/// <remarks>
/// An enum and a magnitude rather than a delegate, so the catalog is pure data with no dependency
/// on <c>Player</c> - which is what lets a validator read the whole roster without a running game.
/// </remarks>
public enum BoonEffect
{
	Armor,
	MaxHealth,
	Regen,
	MoveSpeed,
	SpellDamage,
	SpellArea,
	CooldownReduction,
	PickupRadius,
	Evasion,

	// Feeds GetLegendaryChance and GetBonusDropChance. It needs a boon because the passive spells
	// that used to grant it are gone, and without one the stat would sit at its shop value forever
	// and two systems would quietly stop scaling.
	Luck,

	// --- Reactive effects ---------------------------------------------------------------
	//
	// The two below are the only boons that DO something rather than being a number, and they
	// exist because removing passive spells removed every reactive effect in the game at once -
	// Thornmail retaliated, Frozen Bulwark froze the attacker, Stormguard arced. Losing them made
	// a defensive build indistinguishable from a bigger health bar, which is the thing a defensive
	// build is supposed to feel different from.
	//
	// Both answer being STRUCK rather than being hurt: they fire on a hit that landed, even one
	// fully eaten by armour or a shield. Answering only HP loss would mean the better your defence
	// the less your defence reacts, which is backwards. Player throttles both on one shared
	// cooldown so standing in a crowd cannot turn a reaction into a permanent aura.

	/// <summary>Damages everything near the player when the player is hit. Magnitude is base damage.</summary>
	Retaliate,

	/// <summary>Slows everything near the player when the player is hit. Magnitude is the speed multiplier.</summary>
	Chill,
}

public sealed class BoonDefinition
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required string Description { get; init; }

	/// <summary>Element tags, same rules as a spell: never the same element twice.</summary>
	public required (string Element, int Weight)[] ElementWeights { get; init; }

	/// <summary>Where the boon's 64px icon lives, drawn by tools/art/boon_icons.py.</summary>
	/// <remarks>
	/// Boons had no icon at all until now: the level-up card fell through to DefaultSpellIcon and
	/// tinted it by dominant element, so all fourteen were one picture in fourteen shades - worse
	/// than nothing, because it read as a spell the player had already been offered.
	///
	/// They are drawn on the MATERIAL rows rather than the element ramps, unlike spell icons. A
	/// spell icon is an effect going off; a boon is an object you are carrying, and that difference
	/// is doing real work in telling the two kinds of card apart.
	/// </remarks>
	public string IconPath => $"res://assets/bonelight/ui/boons/{Id}.png";

	public required BoonEffect Effect { get; init; }
	public required float Magnitude { get; init; }
}

/// <summary>
/// Boons: permanent, non-levelling pickups that sit beside the spell loadout.
/// </summary>
/// <remarks>
/// See `.ai/passives-and-items.md`. Two rules shape every entry here.
///
/// **A boon never levels**, so it has to be worth taking the moment it is offered. A spell can be
/// weak at level 1 because level 8 justifies it; a boon has no level 8 to borrow against.
///
/// **A boon does not occupy a spell slot.** The six spell slots are what make a spell choice a
/// decision; a boon competing for one would just be a passive spell again, which is the thing
/// these replace. Boons have their own six slots - see <c>Player.MaxBoonSlots</c> - and that cap is
/// what keeps element tags honest, because uncapped tagged boons would put every capstone within
/// reach at once.
///
/// **The roster is deliberately lopsided toward Metal, Water, Light, Grass and Darkness.** Those
/// five elements get most of their carriers from passive spells today, and passive spells are being
/// removed; without that weighting their capstones become unreachable. `RegressionChecks`
/// .ValidateElementReachability is the check that holds this honest.
/// </remarks>
public static class BoonCatalog
{
	private static readonly BoonDefinition[] Definitions =
	{
		// --- Metal ------------------------------------------------------------------
		new()
		{
			Id = "iron_rivets", Name = "Iron Rivets",
			Description = "Plates riveted over what the robe does not cover. +8% Armor.",
			ElementWeights = new[] { ("Metal", 1), ("Earth", 1) },
			Effect = BoonEffect.Armor, Magnitude = 0.08f,
		},
		new()
		{
			Id = "quicksilver_bead", Name = "Quicksilver Bead",
			Description = "It never stops moving, and neither do your hands. -8% spell cooldowns.",
			ElementWeights = new[] { ("Metal", 1), ("Lightning", 1) },
			Effect = BoonEffect.CooldownReduction, Magnitude = 0.08f,
		},
		new()
		{
			Id = "sunsteel_filament", Name = "Sunsteel Filament",
			Description = "A thread of drawn light, warm to the touch. +8% Armor.",
			ElementWeights = new[] { ("Light", 1), ("Metal", 1) },
			Effect = BoonEffect.Armor, Magnitude = 0.08f,
		},

		// --- Water ------------------------------------------------------------------
		new()
		{
			Id = "tidewater_flask", Name = "Tidewater Flask",
			Description = "Salt water that closes what it is poured on. +0.6 health per second.",
			ElementWeights = new[] { ("Water", 1), ("Ice", 1) },
			Effect = BoonEffect.Regen, Magnitude = 0.6f,
		},
		// Was a third +8% Armor boon, alongside Iron Rivets and Sunsteel Filament. That redundancy
		// was an artifact of unifying damage reduction: the three used to sit on three different
		// mechanisms and read as three different things, and collapsing those into one Armor stat
		// quietly turned them into three copies of one card. Five of thirteen boons on two effects,
		// against six slots, meant a likely draw of the same card twice.
		//
		// It keeps its Water and Metal tags - Water has exactly one spell carrier in the game and
		// cannot afford to lose a boon - and changes only what it does.
		new()
		{
			Id = "saltbound_chain", Name = "Saltbound Chain",
			Description = "Crusted links that lash out at whatever strikes you. Hitting you costs damage.",
			ElementWeights = new[] { ("Water", 1), ("Metal", 1) },
			Effect = BoonEffect.Retaliate, Magnitude = 12f,
		},
		new()
		{
			Id = "deepwater_pearl", Name = "Deepwater Pearl",
			Description = "Things drift toward it. Pickups are drawn from further away.",
			ElementWeights = new[] { ("Water", 1), ("Arcane", 1) },
			Effect = BoonEffect.PickupRadius, Magnitude = 40f,
		},

		// --- Light ------------------------------------------------------------------
		new()
		{
			Id = "lantern_oil", Name = "Lantern Oil",
			Description = "Burns brighter than it should. +10% spell damage.",
			ElementWeights = new[] { ("Light", 1), ("Fire", 1) },
			Effect = BoonEffect.SpellDamage, Magnitude = 0.10f,
		},
		new()
		{
			Id = "gilded_mote", Name = "Gilded Mote",
			Description = "A fleck of the warded circle, still lit. +20 maximum health.",
			ElementWeights = new[] { ("Light", 1), ("Arcane", 1) },
			Effect = BoonEffect.MaxHealth, Magnitude = 20f,
		},

		// --- Grass ------------------------------------------------------------------
		new()
		{
			Id = "mossgrown_charm", Name = "Mossgrown Charm",
			Description = "Older than the occupation, and still growing. +25 maximum health.",
			ElementWeights = new[] { ("Grass", 1), ("Earth", 1) },
			Effect = BoonEffect.MaxHealth, Magnitude = 25f,
		},
		new()
		{
			Id = "thornseed", Name = "Thornseed",
			Description = "It puts out roots wherever a spell lands. +12% spell area.",
			ElementWeights = new[] { ("Grass", 1), ("Poison", 1) },
			Effect = BoonEffect.SpellArea, Magnitude = 0.12f,
		},

		// --- Darkness ---------------------------------------------------------------
		new()
		{
			Id = "shadegrease", Name = "Shadegrease",
			Description = "Nothing quite gets hold of you. +8% movement speed.",
			ElementWeights = new[] { ("Darkness", 1), ("Wind", 1) },
			Effect = BoonEffect.MoveSpeed, Magnitude = 0.08f,
		},
		new()
		{
			Id = "wishing_coin", Name = "Wishing Coin",
			Description = "Thrown into still water by someone who is not coming back. Luck rises.",
			ElementWeights = new[] { ("Water", 1), ("Light", 1) },
			Effect = BoonEffect.Luck, Magnitude = 3f,
		},

		// --- Ice ---------------------------------------------------------------------
		//
		// Tagged Ice and Grass rather than Ice and Water. Water already draws four of its seven tag
		// sources from boons and does not need a fifth; Grass is the only element in the game with
		// no chest item carrying its tag, so a boon is the cheapest place to widen it.
		new()
		{
			Id = "rimebriar", Name = "Rimebriar",
			Description = "Frost-blackened thorns close around anything that reaches you. Attackers are slowed.",
			ElementWeights = new[] { ("Ice", 1), ("Grass", 1) },
			Effect = BoonEffect.Chill, Magnitude = 0.45f,
		},

		new()
		{
			Id = "umbral_veil", Name = "Umbral Veil",
			Description = "A hand's width of somewhere else. +5% chance to evade a hit.",
			ElementWeights = new[] { ("Darkness", 1), ("Ice", 1) },
			Effect = BoonEffect.Evasion, Magnitude = 0.05f,
		},
	};

	public static IReadOnlyList<BoonDefinition> All => Definitions;

	public static BoonDefinition GetById(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			return null;

		return Definitions.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
	}

	public static bool IsBoon(string id) => GetById(id) != null;

	/// <summary>The boon's effect as a stat line, with no flavour.</summary>
	/// <remarks>
	/// A boon's Description carries a sentence of flavour and the number together, which was fine
	/// while the level-up card printed prose. The card shows stats only now, and a boon has no
	/// SpellData to read them off, so the line is rebuilt from the effect and the magnitude here -
	/// the one place that already knows what a magnitude means for each effect.
	/// </remarks>
	public static string DescribeEffect(BoonDefinition definition)
	{
		if (definition == null)
			return string.Empty;

		float m = definition.Magnitude;
		return definition.Effect switch
		{
			BoonEffect.Armor => $"+{m * 100f:0}% Armor",
			BoonEffect.MaxHealth => $"+{m:0} Max HP",
			BoonEffect.Regen => $"+{m:0.0} HP per second",
			BoonEffect.MoveSpeed => $"+{m * 100f:0}% Move Speed",
			BoonEffect.SpellDamage => $"+{m * 100f:0}% Spell Damage",
			BoonEffect.SpellArea => $"+{m * 100f:0}% Spell Area",
			BoonEffect.CooldownReduction => $"-{m * 100f:0}% Cooldowns",
			BoonEffect.PickupRadius => $"+{m:0} Pickup Radius",
			BoonEffect.Evasion => $"+{m * 100f:0}% Evasion",
			BoonEffect.Luck => $"+{m:0} Luck",
			BoonEffect.Retaliate => $"{m:0} damage to nearby enemies when struck",
			BoonEffect.Chill => $"Slows nearby enemies to {m * 100f:0}% when struck",
			_ => string.Empty,
		};
	}
}
