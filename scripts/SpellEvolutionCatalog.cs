using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

public static class SpellEvolutionCatalog
{
	/// <summary>
	/// What a hybrid pays to deepen one of its elements: a 12% longer cooldown.
	/// </summary>
	/// <remarks>
	/// A pure spell's deepening is free, because it is the compensation for carrying half the base
	/// element weight of a hybrid (see Player.GetAttunementMultiplier). A hybrid is owed nothing,
	/// so its element gain is bought. Without a price, every hybrid would simply take the tag every
	/// time and element depth would stop being a decision.
	/// </remarks>
	public const float AttunementGainCooldownCost = 1.12f;

	/// <summary>The elements a spell declares, in the order its resource lists them.</summary>
	private static List<string> ElementNames(SpellData spell)
	{
		var names = new List<string>();
		if (spell?.ElementWeights == null)
			return names;

		foreach (var pair in spell.ElementWeights)
			names.Add(pair.Key);

		return names;
	}

	// EVERY NUMBER AN OPTION SETS MUST BE ONE ITS SPELL READS. EvolutionStatReaders says which fields
	// each spell's script reads, and RegressionChecks.ValidateEvolutionReaders reports an option that
	// sets any other - the way thirteen spells came to offer "projectile speed" to traps, walls and
	// flames. A few hand-written cards below still promise something their spell never built (a burn,
	// a freeze, a pull, a wider area); each is listed, with what it promises, in
	// EvolutionStatReaders.KnownUnbuilt, and that list is the backlog - not a place to add to.
	public static void EnsureEvolutionCoverage(SpellData spell)
	{
		if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
			return;

		// If options are already loaded/configured on the resource, do not overwrite unless empty
		if (spell.Level4Options != null && spell.Level4Options.Count >= 3 &&
			spell.Level8Options != null && spell.Level8Options.Count >= 2)
		{
			return;
		}

		spell.Level4Options = new Godot.Collections.Array<SpellEvolutionOption>();
		spell.Level8Options = new Godot.Collections.Array<SpellEvolutionOption>();

		string id = spell.Id.Trim().ToLowerInvariant();

		switch (id)
		{
			case "magic_missile":
				SetupMagicMissileEvolutions(spell);
				break;
			case "arcane_explosion":
				SetupArcaneExplosionEvolutions(spell);
				break;
			case "spiritual_weapon":
				SetupSpiritualWeaponEvolutions(spell);
				break;
			case "fireball":
				SetupFireballEvolutions(spell);
				break;
			case "frost_shard":
				SetupFrostShardEvolutions(spell);
				break;
			case "chain_lightning":
				SetupChainLightningEvolutions(spell);
				break;
			case "shadow_bolt":
				SetupShadowBoltEvolutions(spell);
				break;
			case "gale_blade":
				SetupGaleBladeEvolutions(spell);
				break;
			case "aegis_ward":
				SetupAegisWardEvolutions(spell);
				break;
			case "thornmail_barrier":
				SetupThornmailEvolutions(spell);
				break;
			case "frozen_bulwark":
				SetupFrozenBulwarkEvolutions(spell);
				break;
			case "venom_cloak":
				SetupVenomCloakEvolutions(spell);
				break;
			case "void_lance":
				SetupVoidLanceEvolutions(spell);
				break;
			case "cyclone_slash":
				SetupCycloneSlashEvolutions(spell);
				break;
			case "solar_flare":
				SetupSolarFlareEvolutions(spell);
				break;
			case "black_tentacles":
				SetupBlackTentaclesEvolutions(spell);
				break;
			case "thorn_vine":
				SetupThornVineEvolutions(spell);
				break;
			case "cone_of_cold":
				SetupConeOfColdEvolutions(spell);
				break;
			case "obsidian_spike":
				SetupObsidianSpikeEvolutions(spell);
				break;
			case "meteor_swarm":
				SetupMeteorSwarmEvolutions(spell);
				break;
			case "toxic_spore_burst":
				SetupToxicSporeEvolutions(spell);
				break;
			// The thirteen that used to share the default archetype's words, which were written for a
			// bolt (section "The default archetype, spell by spell" below).
			case "gravewell":
				SetupGravewellEvolutions(spell);
				break;
			case "hollow_star":
				SetupHollowStarEvolutions(spell);
				break;
			case "bramble_seed":
				SetupBrambleSeedEvolutions(spell);
				break;
			case "contagion":
				SetupContagionEvolutions(spell);
				break;
			case "iron_palisade":
				SetupIronPalisadeEvolutions(spell);
				break;
			case "kindled_ward":
				SetupKindledWardEvolutions(spell);
				break;
			case "riptide":
				SetupRiptideEvolutions(spell);
				break;
			case "molten_shard":
				SetupMoltenShardEvolutions(spell);
				break;
			case "hunters_draw":
				SetupHuntersDrawEvolutions(spell);
				break;
			case "glacial_spike":
				SetupGlacialSpikeEvolutions(spell);
				break;
			case "scorching_ray":
				SetupScorchingRayEvolutions(spell);
				break;
			case "cinderbreath":
				SetupCinderbreathEvolutions(spell);
				break;
			case "mirefoot":
				SetupMirefootEvolutions(spell);
				break;
			case "starfall":
				SetupStarfallEvolutions(spell);
				break;
			case "familiar":
				SetupFamiliarEvolutions(spell);
				break;
			// The facing family (.ai/spell-roster.md): a sweep and a stream, each its own block.
			case "stormsweep":
				SetupStormsweepEvolutions(spell);
				break;
			case "sleet_runes":
				SetupSleetRunesEvolutions(spell);
				break;
			default:
				SetupDefaultArchetypeEvolutions(spell);
				break;
		}
	}

	private static void SetupMagicMissileEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "magic_missile_twin_volley",
			DisplayName = "Twin Volley",
			Description = "The missile forks on its first hit, spraying two shards straight out to either side for 30% damage.",
			MilestoneLevel = 4,
			// The fork is the whole upgrade, so it carries no ProjectileCountBonus. That is
			// deliberate: the magic missile firing loop in Player never asks SpellData for a
			// projectile count (it reads the player-wide amountBonus instead), so the +1 this
			// option used to declare was silently doing nothing. (So was a SpeedMultiplier of 1.25:
			// MagicMissile reads no evolution's speed, and the card never claimed one. Gone.)
			Effect = SpellEffect.SplitOnHit,
			EffectValue = 30f,
			VisualTag = "TwinVolley",
			ModulateColor = new Color(1.0f, 0.88f, 0.4f),
			SynergyTag = "Arcane / Multi-target",
			SynergyDescription = "Synergizes with raw damage and attack speed - the shards fly straight, so they pay off against a line or a wall of enemies."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "magic_missile_piercing_needle",
			DisplayName = "Piercing Stardust",
			Description = "Missiles pierce through 3 enemies and leave a glowing stardust trail.",
			MilestoneLevel = 4,
			PierceBonus = 3,
			VisualTag = "PiercingNeedle",
			ModulateColor = new Color(0.35f, 0.95f, 1.0f),
			SynergyTag = "Arcane / Pierce",
			SynergyDescription = "Synergizes with dense enemy hordes and projectile buffs."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "magic_missile_unstable_shard",
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Unstable Detonation",
			Description = "Missiles detonate in a 60px arcane splash on hit, dealing 60% splash damage.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			Effect = SpellEffect.ExplosionOnHit,
			EffectValue = 60f,
			AreaMultiplier = 1.3f,
			VisualTag = "UnstableShard",
			ModulateColor = new Color(0.92f, 0.45f, 1.0f),
			SynergyTag = "Arcane / AOE",
			SynergyDescription = "Synergizes with Area modifiers and crowd control."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "magic_missile_gatling_barrage",
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Arcane Gatling Barrage",
			Description = "Fires a continuous rapid stream of comet darts (+2 Projectiles, -50% Cooldown, +5 Pierce).",
			MilestoneLevel = 8,
			ProjectileCountBonus = 2,
			CooldownMultiplier = 0.560f,
			PierceBonus = 5,
			DamageMultiplier = 1.25f,
			ScaleMultiplier = 1.2f,
			VisualTag = "GatlingBarrage",
			ModulateColor = new Color(0.45f, 0.85f, 1.0f),
			SynergyTag = "Arcane / Wind",
			SynergyDescription = "Ascension: Overwhelming single-target and line DPS."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "magic_missile_singularity_core",
			DisplayName = "Singularity Core",
			Description = "Fires massive gravitational orbs (+120% Area, +100% Damage) that pull in and crush enemies.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			AreaMultiplier = 2.2f,
			ScaleMultiplier = 1.85f,
			Effect = SpellEffect.VortexPull,
			EffectValue = 120f,
			VisualTag = "SingularityCore",
			ModulateColor = new Color(0.75f, 0.2f, 1.0f),
			SynergyTag = "Arcane / Darkness",
			SynergyDescription = "Ascension: Colossal zone control and mass vortex pull."
		});
	}

	private static void SetupArcaneExplosionEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "arcane_explosion_dual_shockwave",
			DisplayName = "Dual Shockwave",
			// "Two rapid concentric shockwaves" was never built; the knockback is, since 2026-10-26.
			Description = "The blast hits 35% harder and throws enemies 70% further.",
			MilestoneLevel = 4,
			KnockbackBonus = 70f,
			DamageMultiplier = 1.35f,
			VisualTag = "DualShockwave",
			ModulateColor = new Color(0.4f, 0.85f, 1.0f),
			SynergyTag = "Arcane / Force",
			SynergyDescription = "Repels fast swarming enemies with double blast waves."
		});

		// The element path (ElementPaths): Arcane's branch into Lightning. Its three chain sparks were
		// never built - ArcaneExplosion reads neither ChainArcBonus nor the Chain effect - so the card
		// promised a chain and paid only the tag. It now pays Reactive Catalyst's +20% damage, the tag,
		// and Lightning's stagger on everything the blast catches, and says so.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "arcane_explosion_static_nova",
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Static Nova",
			Description = "The blast crackles with static: +20% damage, +1 Lightning tag, and "
				+ ElementPaths.Describe(ElementRider.Shock, 0f, 0.3f) + ".",
			MilestoneLevel = 4,
			DamageMultiplier = 1.2f,
			Rider = ElementRider.Shock,
			RiderSeconds = 0.3f,
			VisualTag = "StaticNova",
			ModulateColor = ElementColors.GetEvolutionTint(Element.Lightning),
			SynergyTag = "Arcane / Lightning",
			SynergyDescription = "Branches into Lightning. Every detonation pins the crowd where it stands for a moment."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "arcane_explosion_lingering_rift",
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Lingering Rift",
			Description = "Each blast leaves a rift on the ground for 2.5s that hurts what stands in it (+40% Area).",
			MilestoneLevel = 4,
			AreaMultiplier = 1.4f,
			Effect = SpellEffect.ZoneDuration,
			EffectValue = 2.5f,
			VisualTag = "LingeringRift",
			ModulateColor = new Color(0.85f, 0.35f, 0.95f),
			SynergyTag = "Arcane / Zone",
			SynergyDescription = "Creates persistent defensive zones against advancing waves."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "arcane_explosion_supernova",
			DisplayName = "Supernova",
			// The "incinerating" was an Effect Burn of 5 that nothing read - there is no burn status
			// (.ai/gameplay-design.md, "Status effects"). Its share went into the damage, x2.0 to x2.2.
			Description = "A massive blast: +150% Area and +120% Damage.",
			MilestoneLevel = 8,
			AreaMultiplier = 2.5f,
			DamageMultiplier = 2.2f,
			VisualTag = "Supernova",
			ModulateColor = new Color(1.0f, 0.4f, 0.12f),
			SynergyTag = "Arcane / Fire",
			SynergyDescription = "Ascension: Decimates massive waves with burning cataclysm."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "arcane_explosion_event_horizon",
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Event Horizon",
			// ArcaneExplosion reads the freeze, and a freezing blast pulls inward (2026-10-26). The
			// SlowMagnitudeBonus of 1.0 that restated the freeze as a slow went: nothing read it.
			Description = "Cosmic singularity pulls enemies inward and freezes them for 2.5 seconds.",
			MilestoneLevel = 8,
			AreaMultiplier = 1.8f,
			DamageMultiplier = 1.5f,
			Effect = SpellEffect.Freeze,
			EffectValue = 2.5f,
			VisualTag = "EventHorizon",
			ModulateColor = new Color(0.35f, 0.65f, 1.0f),
			SynergyTag = "Arcane / Gravity",
			SynergyDescription = "Ascension: Locks down entire screen with freezing gravitational pull."
		});
	}

	private static void SetupSpiritualWeaponEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "spiritual_weapon_whirlwind",
			DisplayName = "Phantom Whirlwind",
			Description = "+2 orbiting spectral blades and +40% rotation speed.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 2,
			SpeedMultiplier = 1.4f,
			VisualTag = "PhantomWhirlwind",
			ModulateColor = new Color(0.45f, 0.9f, 1.0f),
			SynergyTag = "Light / Speed",
			SynergyDescription = "Creates an impenetrable melee blender around the player."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "spiritual_weapon_cleave",
			DisplayName = "Spectral Cleave",
			Description = "Extends blade reach by +60px and increases damage by +50%.",
			MilestoneLevel = 4,
			RangeBonus = 60f,
			DamageMultiplier = 1.5f,
			ScaleMultiplier = 1.35f,
			VisualTag = "SpectralCleave",
			ModulateColor = new Color(1.0f, 0.9f, 0.4f),
			SynergyTag = "Light / Range",
			SynergyDescription = "Slices through elite enemies and shielded targets."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "spiritual_weapon_soul_siphon",
			BonusElementWeights = new() { { "Light", 1 } },
			DisplayName = "Soul Siphon",
			Description = "Blade strikes heal the player for 1 HP on hit.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			Effect = SpellEffect.Lifesteal,
			EffectValue = 1f,
			VisualTag = "SoulSiphon",
			ModulateColor = new Color(0.45f, 1.0f, 0.65f),
			SynergyTag = "Light / Grass",
			SynergyDescription = "High survivability sustain through constant melee hits."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "spiritual_weapon_blade_tempest",
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Blade Tempest",
			Description = "Blades detach into 6 autonomous seeking phantom daggers (+4 Projectiles, -40% Cooldown).",
			MilestoneLevel = 8,
			ProjectileCountBonus = 4,
			CooldownMultiplier = 0.672f,
			DamageMultiplier = 1.4f,
			ScaleMultiplier = 1.3f,
			VisualTag = "BladeTempest",
			ModulateColor = new Color(0.85f, 0.95f, 1.0f),
			SynergyTag = "Light / Wind",
			SynergyDescription = "Ascension: Relentless multi-target homing blades."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "spiritual_weapon_executioner",
			DisplayName = "Celestial Executioner",
			Description = "Colossal holy greatsword slams down periodically for +200% Damage and +100% Area.",
			MilestoneLevel = 8,
			DamageMultiplier = 3.0f,
			AreaMultiplier = 2.0f,
			ScaleMultiplier = 2.1f,
			VisualTag = "CelestialExecutioner",
			ModulateColor = new Color(1.0f, 0.85f, 0.25f),
			SynergyTag = "Light / Earth",
			SynergyDescription = "Ascension: Devastating orbital boss-killer burst."
		});
	}

	private static void SetupFireballEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "fireball_pyroclasm",
			BonusElementWeights = new() { { "Fire", 1 } },
			DisplayName = "Pyroclasm",
			Description = "Impact leaves a pool of burning magma that scorches ground for 3 seconds.",
			MilestoneLevel = 4,
			Effect = SpellEffect.Burn,
			EffectValue = 3f,
			VisualTag = "Pyroclasm",
			ModulateColor = new Color(1.0f, 0.5f, 0.1f),
			SynergyTag = "Fire / Zone",
			SynergyDescription = "Denies choke points with lingering fire damage."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "fireball_cluster",
			DisplayName = "Cluster Bombs",
			Description = "Splits into 3 mini explosive projectiles upon impact.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 2,
			DamageMultiplier = 1.25f,
			VisualTag = "ClusterBombs",
			ModulateColor = new Color(1.0f, 0.75f, 0.25f),
			SynergyTag = "Fire / Multishot",
			SynergyDescription = "Multiplies area coverage and burst hits."
		});

		// The element path (ElementPaths): Fire's branch into Metal. It promised to pierce 3 enemies
		// at +40% speed, but Fireball reads neither pierce nor speed - it bursts on the first body it
		// meets - so its only real effect was the tag. The card now says what it does: the tag, and
		// Metal's Wither on every enemy the blast catches. Its PierceBonus 3 and SpeedMultiplier 1.4
		// sat on here "for whenever Fireball learns to pierce" and went with the reader check
		// (EvolutionStatReaders): a number no card states and no script reads is only a trap for the
		// next person, and the day Fireball pierces, its card will say how far.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "fireball_comet",
			BonusElementWeights = new() { { "Metal", 1 } },
			DisplayName = "Iron Comet",
			Description = "The fireball is forged around an iron core: +1 Metal tag, and "
				+ ElementPaths.Describe(ElementRider.Wither, 0.05f, 0f) + ".",
			MilestoneLevel = 4,
			Rider = ElementRider.Wither,
			RiderMagnitude = 0.05f,
			VisualTag = "PiercingComet",
			ModulateColor = ElementColors.GetEvolutionTint(Element.Metal),
			SynergyTag = "Fire / Metal",
			SynergyDescription = "Branches into Metal. The blast burns a share of everything's health away - worth most against elites and bosses."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "fireball_hellfire_nova",
			BonusElementWeights = new() { { "Fire", 1 } },
			DisplayName = "Hellfire Nova",
			Description = "Detonates in a colossal flame shockwave (+150% Area, +100% Damage).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			AreaMultiplier = 2.5f,
			ScaleMultiplier = 2.2f,
			VisualTag = "HellfireNova",
			ModulateColor = new Color(1.0f, 0.2f, 0.05f),
			SynergyTag = "Fire / AOE",
			SynergyDescription = "Ascension: Screen-wide incendiary destruction."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "fireball_armageddon_sun",
			DisplayName = "Armageddon Sun",
			Description = "Slow drifting sun orb that scorches all nearby enemies (+150% Damage, +100% Area).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.5f,
			AreaMultiplier = 2.0f,
			SpeedMultiplier = 0.7f,
			ScaleMultiplier = 2.0f,
			VisualTag = "ArmageddonSun",
			ModulateColor = new Color(1.0f, 0.9f, 0.3f),
			SynergyTag = "Fire / Solar",
			SynergyDescription = "Ascension: Massive drifting aura of pure heat."
		});
	}

	private static void SetupFrostShardEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frost_shard_glacial_spike",
			BonusElementWeights = new() { { "Ice", 1 } },
			DisplayName = "Glacial Spike",
			Description = "Heavy ice spike with +4 Pierce and +50% slow potency.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			PierceBonus = 4,
			SlowMagnitudeBonus = 0.5f,
			VisualTag = "GlacialSpike",
			ModulateColor = new Color(0.45f, 0.85f, 1.0f),
			SynergyTag = "Ice / Pierce",
			SynergyDescription = "Punctures lines of enemies while freezing them."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frost_shard_ice_flurry",
			DisplayName = "Ice Flurry",
			Description = "Fires +2 shards in a spread with +35% projectile speed.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 2,
			SpeedMultiplier = 1.35f,
			VisualTag = "IceFlurry",
			ModulateColor = new Color(0.7f, 1.0f, 1.0f),
			SynergyTag = "Ice / Speed",
			SynergyDescription = "Widespread rapid freezing coverage."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frost_shard_frost_nova",
			DisplayName = "Frost Nova Burst",
			Description = "Impact triggers a radial frost wave that freezes nearby enemies for 1.5s.",
			MilestoneLevel = 4,
			Effect = SpellEffect.Freeze,
			EffectValue = 1.5f,
			VisualTag = "FrostNova",
			ModulateColor = new Color(0.35f, 0.75f, 1.0f),
			SynergyTag = "Ice / AOE",
			SynergyDescription = "Area freeze lockdown on projectile impact."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "frost_shard_absolute_zero",
			DisplayName = "Absolute Zero",
			Description = "Frozen targets shatter on hit, releasing deadly piercing ice shrapnel (+100% Damage).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			PierceBonus = 4,
			VisualTag = "AbsoluteZero",
			ModulateColor = new Color(0.85f, 0.95f, 1.0f),
			SynergyTag = "Ice / Destruction",
			SynergyDescription = "Ascension: Shattering chain reaction across frozen hordes."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "frost_shard_blizzard_engine",
			BonusElementWeights = new() { { "Water", 1 } },
			DisplayName = "Blizzard Engine",
			Description = "Continuous storm of frost shards with -50% Cooldown and +3 Projectiles.",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.560f,
			ProjectileCountBonus = 3,
			VisualTag = "BlizzardEngine",
			ModulateColor = new Color(0.6f, 0.85f, 1.0f),
			SynergyTag = "Ice / Wind",
			SynergyDescription = "Ascension: Relentless blizzard barrage."
		});
	}

	// Chain Lightning is the DAMAGE half of the chain pair (see .ai/spell-roster.md). Its base form
	// hops once, close, and loses almost nothing doing it. What these options sell is the thing its
	// base form deliberately does not do: splitting. Both splitting branches cost damage, so the
	// choice is a real fork rather than a strictly better version of the spell.
	//
	// Level 4 is also its element choice: Storm Reach deepens Lightning (and is the Stormcaller's
	// Apotheosis path), Storm Surge branches into Water, Dense Current stays as it is.
	private static void SetupChainLightningEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		// Storm Surge keeps the id it had as Forked Surge, and its split, and now carries the storm's
		// rain with it: the element path (ElementPaths). Chain Lightning paints the Water arc itself.
		const float surgeDrag = 0.75f;
		const float surgeSeconds = 1.5f;
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "chain_lightning_forked_surge",
			DisplayName = "Storm Surge",
			Description = "The arc forks through falling rain: it splits three ways at every hop for 30% less each, +1 Water tag, and "
				+ ElementPaths.Describe(ElementRider.Slow, surgeDrag, surgeSeconds) + ".",
			MilestoneLevel = 4,
			ChainBranchBonus = 2,
			DamageMultiplier = 0.7f,
			BonusElementWeights = new() { { "Water", 1 } },
			Rider = ElementRider.Slow,
			RiderMagnitude = surgeDrag,
			RiderSeconds = surgeSeconds,
			VisualTag = "StormSurge",
			ModulateColor = ElementColors.GetEvolutionTint(Element.Water),
			SynergyTag = "Lightning / Water",
			SynergyDescription = "Branches into Water. Turns a single heavy bolt into a net that drags the crowd. Wants area and cooldown, not raw damage."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "chain_lightning_storm_reach",
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Storm Reach",
			Description = "+3 chain arcs and a wider jump, for 15% less damage per hop.",
			MilestoneLevel = 4,
			ChainArcBonus = 3,
			DamageMultiplier = 0.85f,
			AreaMultiplier = 1.35f,
			VisualTag = "StormReach",
			ModulateColor = new Color(0.65f, 0.88f, 1.0f),
			SynergyTag = "Lightning / Reach",
			SynergyDescription = "Longer rather than wider - one arc that crosses the whole crowd."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "chain_lightning_dense_current",
			DisplayName = "Dense Current",
			Description = "No extra chaining. The bolt simply hits 40% harder and crits more often.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.4f,
			Effect = SpellEffect.CritChance,
			EffectValue = 0.15f,
			VisualTag = "DenseCurrent",
			ModulateColor = new Color(1.0f, 0.97f, 0.62f),
			SynergyTag = "Lightning / Single target",
			SynergyDescription = "The option that keeps the spell what it already is. Good against elites and bosses."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "chain_lightning_cascade",
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Cascade",
			Description = "Every hop splits again and reaches further, at 40% of the damage.",
			MilestoneLevel = 8,
			ChainBranchBonus = 3,
			ChainArcBonus = 2,
			DamageMultiplier = 0.6f,
			AreaMultiplier = 1.3f,
			VisualTag = "Cascade",
			ModulateColor = new Color(0.72f, 0.94f, 1.0f),
			SynergyTag = "Lightning / Saturation",
			SynergyDescription = "Ascension: the arc stops being a bolt and becomes weather. Pairs with anything that adds damage back."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "chain_lightning_thunderhead",
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Thunderhead",
			Description = "One arc, twice the force, and it barely weakens as it jumps.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			ChainArcBonus = 1,
			ScaleMultiplier = 1.5f,
			VisualTag = "Thunderhead",
			ModulateColor = new Color(1.0f, 0.95f, 0.5f),
			SynergyTag = "Lightning / Single target",
			SynergyDescription = "Ascension: the anti-elite answer. Everything the spell has, pointed at one thing."
		});
	}

	// Shadow Bolt is the DEBUFF half. It barely kills anything on its own and is not meant to - its
	// job is to make the other five spells hit harder, so every option here adds or deepens a
	// debuff rather than adding damage. The one exception carries damage as its cost of admission.
	private static void SetupShadowBoltEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_withering_mark",
			BonusElementWeights = new() { { "Darkness", 1 } },
			DisplayName = "Withering Mark",
			Description = "Everything it touches is left far more exposed (+12% damage taken).",
			MilestoneLevel = 4,
			Effect = SpellEffect.Vulnerability,
			EffectValue = 0.12f,
			VisualTag = "WitheringMark",
			ModulateColor = new Color(0.72f, 0.36f, 0.92f),
			SynergyTag = "Darkness / Vulnerability",
			SynergyDescription = "Multiplies every other spell you own. The more damage the rest of the loadout deals, the more this is worth."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_creeping_rot",
			BonusElementWeights = new() { { "Poison", 1 } },
			DisplayName = "Creeping Rot",
			Description = "Deeper poison and a much heavier drag on anything struck.",
			MilestoneLevel = 4,
			PoisonTickBonus = 4,
			SlowMagnitudeBonus = 0.15f,
			VisualTag = "CreepingRot",
			ModulateColor = new Color(0.45f, 0.72f, 0.30f),
			SynergyTag = "Poison / Control",
			SynergyDescription = "The survival option - a crowd that cannot close is a crowd that cannot hit you."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_spreading_dark",
			DisplayName = "Spreading Dark",
			Description = "The curse splits at every jump and travels two hops further. No extra damage.",
			MilestoneLevel = 4,
			ChainBranchBonus = 1,
			ChainArcBonus = 2,
			VisualTag = "SpreadingDark",
			ModulateColor = new Color(0.35f, 0.16f, 0.52f),
			SynergyTag = "Darkness / Spread",
			SynergyDescription = "More bodies debuffed rather than heavier debuffs. Wants a wide crowd."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_anathema",
			// No BonusElementWeights. Element growth on a hybrid happens at level 4; granting one
			// here as well would let a player who took the matching level 4 option reach three of
			// a single element, which only a pure spell may do.
			DisplayName = "Anathema",
			Description = "The mark becomes a sentence: +20% damage taken, spread two hops further.",
			MilestoneLevel = 8,
			Effect = SpellEffect.Vulnerability,
			EffectValue = 0.20f,
			ChainArcBonus = 2,
			VisualTag = "Anathema",
			ModulateColor = new Color(0.86f, 0.30f, 1.0f),
			SynergyTag = "Darkness / Vulnerability",
			SynergyDescription = "Ascension: the whole screen takes more from everything. The debuff caps, so stack damage elsewhere."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_plaguebearer",
			// No BonusElementWeights. Element growth on a hybrid happens at level 4; granting one
			// here as well would let a player who took the matching level 4 option reach three of
			// a single element, which only a pure spell may do.
			DisplayName = "Plaguebearer",
			Description = "Splits twice over and carries a rot that does real damage on its own.",
			MilestoneLevel = 8,
			PoisonTickBonus = 8,
			ChainBranchBonus = 2,
			DamageMultiplier = 1.2f,
			VisualTag = "Plaguebearer",
			ModulateColor = new Color(0.52f, 0.84f, 0.34f),
			SynergyTag = "Poison / Damage",
			SynergyDescription = "Ascension: the one branch where Shadow Bolt kills things itself, through poison rather than impact."
		});
	}

	// Gale Blade left the chain archetype to Chain Lightning and Shadow Bolt, so what it needs from
	// its milestones is the thing neither of those is: a fast, hard, critical single strike. Damage,
	// crit rate, crit damage and attack speed, and nothing else.
	private static void SetupGaleBladeEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "gale_blade_honed_edge",
			BonusElementWeights = new() { { "Wind", 1 } },
			DisplayName = "Honed Edge",
			Description = "+50% damage on every strike.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.5f,
			VisualTag = "HonedEdge",
			ModulateColor = new Color(0.86f, 0.95f, 0.88f),
			SynergyTag = "Wind / Damage",
			SynergyDescription = "The flat answer. Scales with everything that raises spell damage."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "gale_blade_keen_wind",
			DisplayName = "Keen Wind",
			Description = "+25% critical strike chance.",
			MilestoneLevel = 4,
			Effect = SpellEffect.CritChance,
			EffectValue = 0.25f,
			VisualTag = "KeenWind",
			ModulateColor = new Color(0.72f, 0.92f, 1.0f),
			SynergyTag = "Wind / Crit",
			SynergyDescription = "Worth more the more crit damage you are carrying - pairs with Storm Lattice and the Stormbound set."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "gale_blade_quickening",
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Quickening",
			Description = "Throws 30% faster.",
			MilestoneLevel = 4,
			CooldownMultiplier = 0.7f,
			SpeedMultiplier = 1.25f,
			VisualTag = "Quickening",
			ModulateColor = new Color(1.0f, 0.96f, 0.68f),
			SynergyTag = "Wind / Attack speed",
			SynergyDescription = "More swings means more crit rolls and more on-hit triggers, so it compounds with the other two."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "gale_blade_executioners_gale",
			// No BonusElementWeights. Element growth on a hybrid happens at level 4; granting one
			// here as well would let a player who took the matching level 4 option reach three of
			// a single element, which only a pure spell may do.
			DisplayName = "Executioner's Gale",
			Description = "Critical strikes land with a full extra multiplier behind them.",
			MilestoneLevel = 8,
			Effect = SpellEffect.CritDamage,
			EffectValue = 1.0f,
			ScaleMultiplier = 1.3f,
			VisualTag = "ExecutionersGale",
			ModulateColor = new Color(1.0f, 0.88f, 0.72f),
			SynergyTag = "Wind / Crit damage",
			SynergyDescription = "Ascension: dead weight without crit chance, and enormous with it. Take Keen Wind first."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "gale_blade_tempest_rhythm",
			// No BonusElementWeights. Element growth on a hybrid happens at level 4; granting one
			// here as well would let a player who took the matching level 4 option reach three of
			// a single element, which only a pure spell may do.
			DisplayName = "Tempest Rhythm",
			Description = "Throws 40% faster and 25% harder.",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.6f,
			DamageMultiplier = 1.25f,
			SpeedMultiplier = 1.4f,
			VisualTag = "TempestRhythm",
			ModulateColor = new Color(0.78f, 0.98f, 0.92f),
			SynergyTag = "Wind / Attack speed",
			SynergyDescription = "Ascension: the consistent one. No dependency on crit, so it works in any build."
		});
	}


	private static void SetupAegisWardEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "aegis_ward_reflective",
			DisplayName = "Reflective Plating",
			Description = "Shield break reflects 150% absorbed damage back to attacker.",
			MilestoneLevel = 4,
			DamageBonus = 15,
			VisualTag = "ReflectivePlating",
			ModulateColor = new Color(1.0f, 0.85f, 0.35f),
			SynergyTag = "Metal / Retaliation",
			SynergyDescription = "Punishes attacking enemies with burst retaliation."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "aegis_ward_quick_bastion",
			DisplayName = "Quick Bastion",
			Description = "Reduces shield recharge delay by 40%.",
			MilestoneLevel = 4,
			CooldownMultiplier = 0.6f,
			VisualTag = "QuickBastion",
			ModulateColor = new Color(0.5f, 0.85f, 1.0f),
			SynergyTag = "Water / Defense",
			SynergyDescription = "Rapid shield regeneration for sustained combat."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "aegis_ward_fortified",
			BonusElementWeights = new() { { "Metal", 1 } },
			DisplayName = "Fortified Aegis",
			Description = "Increases max shield capacity by +50% and grants +2 flat armor while active.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			EffectValue = 50f,
			VisualTag = "FortifiedAegis",
			ModulateColor = new Color(0.9f, 0.75f, 0.5f),
			SynergyTag = "Earth / Metal",
			SynergyDescription = "Heavy defensive fortification against big hits."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "aegis_ward_sun_aegis",
			BonusElementWeights = new() { { "Light", 1 } },
			DisplayName = "Sun Aegis",
			Description = "Radiates holy solar beams while charged; shield break blinds and stuns all nearby enemies.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			VisualTag = "SunAegis",
			ModulateColor = new Color(1.0f, 0.95f, 0.4f),
			SynergyTag = "Light / Solar",
			SynergyDescription = "Ascension: Turns defensive barrier into a radiant offensive weapon."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "aegis_ward_crystal_spikes",
			DisplayName = "Thorn Crystal Shell",
			Description = "Shield break detonates into 12 piercing crystal shards dealing massive damage.",
			MilestoneLevel = 8,
			DamageBonus = 40,
			PierceBonus = 8,
			VisualTag = "ThornCrystal",
			ModulateColor = new Color(0.6f, 1.0f, 0.5f),
			SynergyTag = "Metal / Poison",
			SynergyDescription = "Ascension: Lethal radial crystal burst upon shield break."
		});
	}

	private static void SetupThornmailEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "thornmail_poison_spines",
			DisplayName = "Venom Spines",
			Description = "Retaliation spikes apply deadly poison to attacking enemies.",
			MilestoneLevel = 4,
			PoisonTickBonus = 4,
			VisualTag = "VenomSpines",
			ModulateColor = new Color(0.4f, 0.95f, 0.3f),
			SynergyTag = "Poison / Earth",
			SynergyDescription = "Inflicts lingering venom on contact."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "thornmail_iron_brambles",
			BonusElementWeights = new() { { "Earth", 1 } },
			DisplayName = "Iron Brambles",
			Description = "Increases retaliation range by +50% and grants +2 flat armor.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			RangeBonus = 50f,
			VisualTag = "IronBrambles",
			ModulateColor = new Color(0.75f, 0.75f, 0.8f),
			SynergyTag = "Metal / Earth",
			SynergyDescription = "Expanded retaliation area and damage reduction."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "thornmail_rooting_thorns",
			DisplayName = "Entangling Thorns",
			Description = "Retaliation hits root enemies in place for 1.5 seconds.",
			MilestoneLevel = 4,
			Effect = SpellEffect.RootDuration,
			EffectValue = 1.5f,
			VisualTag = "EntanglingThorns",
			ModulateColor = new Color(0.6f, 0.85f, 0.35f),
			SynergyTag = "Grass / Control",
			SynergyDescription = "Roots aggressive melee attackers in place."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "thornmail_living_grove",
			BonusElementWeights = new() { { "Grass", 1 } },
			DisplayName = "Living Grove",
			Description = "Player is surrounded by permanent swirling razor vines that shred all nearby foes.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 2.2f,
			AreaMultiplier = 1.8f,
			ScaleMultiplier = 1.6f,
			VisualTag = "LivingGrove",
			ModulateColor = new Color(0.5f, 1.0f, 0.4f),
			SynergyTag = "Grass / Destruction",
			SynergyDescription = "Ascension: Persistent high-damage thorny aura."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "thornmail_retaliation_nova",
			DisplayName = "Colossal Brambleburst",
			Description = "Taking damage triggers an explosive radial volley of 20 piercing iron thorns.",
			MilestoneLevel = 8,
			DamageBonus = 50,
			PierceBonus = 10,
			ScaleMultiplier = 1.7f,
			VisualTag = "Brambleburst",
			ModulateColor = new Color(0.9f, 0.8f, 0.4f),
			SynergyTag = "Metal / Force",
			SynergyDescription = "Ascension: Massive screen-clearing retaliation volley."
		});
	}

	private static void SetupFrozenBulwarkEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frozen_bulwark_permafrost",
			DisplayName = "Permafrost Aura",
			Description = "Enemies near the player suffer permanent -35% movement speed.",
			MilestoneLevel = 4,
			SlowMagnitudeBonus = 0.35f,
			VisualTag = "PermafrostAura",
			ModulateColor = new Color(0.5f, 0.85f, 1.0f),
			SynergyTag = "Ice / Control",
			SynergyDescription = "Aura of constant movement suppression."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frozen_bulwark_ice_armor",
			BonusElementWeights = new() { { "Metal", 1 } },
			DisplayName = "Glacial Barrier",
			Description = "Increases armor and grants a 40% chance to completely freeze attackers.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			Effect = SpellEffect.Freeze,
			EffectValue = 2.0f,
			VisualTag = "GlacialBarrier",
			ModulateColor = new Color(0.7f, 0.95f, 1.0f),
			SynergyTag = "Ice / Metal",
			SynergyDescription = "High defense with freeze counter."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frozen_bulwark_frostbite",
			DisplayName = "Frostbite Shards",
			Description = "Freezing an enemy causes them to take continuous cold damage ticks.",
			MilestoneLevel = 4,
			DamageBonus = 10,
			VisualTag = "FrostbiteShards",
			ModulateColor = new Color(0.4f, 0.7f, 1.0f),
			SynergyTag = "Ice / DOT",
			SynergyDescription = "Converts frost control into offensive damage."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "frozen_bulwark_glacier_avatar",
			BonusElementWeights = new() { { "Ice", 1 } },
			DisplayName = "Glacier Avatar",
			Description = "Surrounds player in a colossal ice barrier (+100 Max HP, pulses screen-wide freezing waves).",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			AreaMultiplier = 2.0f,
			ScaleMultiplier = 1.8f,
			VisualTag = "GlacierAvatar",
			ModulateColor = new Color(0.85f, 0.95f, 1.0f),
			SynergyTag = "Ice / Earth",
			SynergyDescription = "Ascension: Unyielding defensive fortress."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "frozen_bulwark_ice_shatter_burst",
			DisplayName = "Shatterstorm",
			Description = "When frozen enemies are struck, they explode in freezing shards that chain to others.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.4f,
			PierceBonus = 6,
			ScaleMultiplier = 1.6f,
			VisualTag = "Shatterstorm",
			ModulateColor = new Color(0.6f, 0.9f, 1.0f),
			SynergyTag = "Ice / Chain",
			SynergyDescription = "Ascension: Massive chain-reaction shattering explosions."
		});
	}

	private static void SetupVenomCloakEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "venom_cloak_corrosive_miasma",
			BonusElementWeights = new() { { "Poison", 1 } },
			DisplayName = "Corrosive Miasma",
			Description = "+50% Poison tick damage and shreds enemy armor.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			PoisonTickBonus = 6,
			VisualTag = "CorrosiveMiasma",
			ModulateColor = new Color(0.5f, 0.9f, 0.2f),
			SynergyTag = "Poison / Acid",
			SynergyDescription = "Dramatically increases damage over time."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "venom_cloak_spreading_fog",
			DisplayName = "Spreading Fog",
			Description = "Increases poison cloud radius by +60% and pulse rate by +30%.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.6f,
			CooldownMultiplier = 0.7f,
			VisualTag = "SpreadingFog",
			ModulateColor = new Color(0.35f, 0.85f, 0.4f),
			SynergyTag = "Poison / Area",
			SynergyDescription = "Massive toxic cloud enveloping the entire area."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "venom_cloak_neurotoxin",
			DisplayName = "Neurotoxin",
			Description = "Poisoned enemies are slowed by 40% and deal 25% less damage.",
			MilestoneLevel = 4,
			SlowMagnitudeBonus = 0.4f,
			VisualTag = "Neurotoxin",
			ModulateColor = new Color(0.65f, 0.35f, 0.85f),
			SynergyTag = "Poison / Darkness",
			SynergyDescription = "Cripples enemy speed and offensive threat."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "venom_cloak_plague_bearer",
			DisplayName = "Plague Bearer",
			Description = "Poisoned enemies explode on death into toxic clouds that infect surrounding hordes.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.2f,
			AreaMultiplier = 2.0f,
			PoisonTickBonus = 12,
			ScaleMultiplier = 1.7f,
			VisualTag = "PlagueBearer",
			ModulateColor = new Color(0.4f, 1.0f, 0.25f),
			SynergyTag = "Poison / Epidemic",
			SynergyDescription = "Ascension: Self-propagating chain-reaction epidemic."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "venom_cloak_shadow_catalyst",
			BonusElementWeights = new() { { "Darkness", 1 } },
			DisplayName = "Shadow Catalyst",
			Description = "Transforms poison cloud into dark void acid (+150% Damage, grants player +20% Dodge).",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 2.5f,
			AreaMultiplier = 1.8f,
			ScaleMultiplier = 1.6f,
			VisualTag = "ShadowCatalyst",
			ModulateColor = new Color(0.55f, 0.15f, 0.85f),
			SynergyTag = "Darkness / Evasion",
			SynergyDescription = "Ascension: Void damage combined with illusory dodge."
		});
	}

	private static void SetupVoidLanceEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "void_lance_abyssal_pierce",
			DisplayName = "Abyssal Pierce",
			Description = "+4 Pierce and leaves a dark energy trail in its wake.",
			MilestoneLevel = 4,
			PierceBonus = 4,
			VisualTag = "AbyssalPierce",
			ModulateColor = new Color(0.6f, 0.2f, 0.95f),
			SynergyTag = "Darkness / Pierce",
			SynergyDescription = "Pierces deep enemy lines."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "void_lance_rift_blade",
			DisplayName = "Rift Blade",
			Description = "+50% Projectile size and width, cleaving wide paths.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.5f,
			ScaleMultiplier = 1.4f,
			VisualTag = "RiftBlade",
			ModulateColor = new Color(0.8f, 0.3f, 1.0f),
			SynergyTag = "Darkness / Area",
			SynergyDescription = "Massive beam profile."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "void_lance_gravity_spike",
			BonusElementWeights = new() { { "Darkness", 1 } },
			DisplayName = "Gravity Spike",
			Description = "Enemies hit by the lance are pulled along with it.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			Effect = SpellEffect.VortexPull,
			EffectValue = 80f,
			VisualTag = "GravitySpike",
			ModulateColor = new Color(0.45f, 0.15f, 0.8f),
			SynergyTag = "Darkness / Pull",
			SynergyDescription = "Drags enemy clusters."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "void_lance_annihilation_beam",
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Annihilation Beam",
			Description = "Fires a continuous screen-piercing death ray (+200% Damage, infinite pierce).",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 3.0f,
			PierceBonus = 99,
			ScaleMultiplier = 2.0f,
			VisualTag = "AnnihilationBeam",
			ModulateColor = new Color(0.9f, 0.2f, 1.0f),
			SynergyTag = "Darkness / Ultimate",
			SynergyDescription = "Ascension: Infinite pierce screen-long death beam."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "void_lance_cosmic_collapse",
			DisplayName = "Cosmic Collapse",
			Description = "Lance creates a catastrophic black hole explosion at its terminus (+150% Area).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.4f,
			AreaMultiplier = 2.2f,
			ScaleMultiplier = 1.8f,
			VisualTag = "CosmicCollapse",
			ModulateColor = new Color(0.3f, 0.1f, 0.7f),
			SynergyTag = "Darkness / Singularity",
			SynergyDescription = "Ascension: Massive end-point gravitational detonation."
		});
	}

	private static void SetupCycloneSlashEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "cyclone_slash_dual_blades",
			DisplayName = "Dual Windblades",
			Description = "+2 orbiting cyclone blades with +30% rotation speed.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 2,
			SpeedMultiplier = 1.3f,
			VisualTag = "DualWindblades",
			ModulateColor = new Color(0.5f, 1.0f, 0.8f),
			SynergyTag = "Wind / Orbit",
			SynergyDescription = "Expands orbiting strike coverage."
		});

		// The element path (ElementPaths): Wind's branch into Metal. The crescents it promised were
		// never built and OrbitingBlade does not read RangeBonus, so what it paid was +35% damage and
		// the tag. The card now says that, plus Metal's Wither on everything the blades cut. (The
		// unread RangeBonus of 40 went with the reader check, EvolutionStatReaders.)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "cyclone_slash_razor_gale",
			BonusElementWeights = new() { { "Metal", 1 } },
			DisplayName = "Razor Gale",
			Description = "The blades are edged in steel: +35% damage, +1 Metal tag, and "
				+ ElementPaths.Describe(ElementRider.Wither, 0.04f, 0f) + ".",
			MilestoneLevel = 4,
			DamageMultiplier = 1.35f,
			Rider = ElementRider.Wither,
			RiderMagnitude = 0.04f,
			VisualTag = "RazorGale",
			ModulateColor = ElementColors.GetEvolutionTint(Element.Metal),
			SynergyTag = "Wind / Metal",
			SynergyDescription = "Branches into Metal. A blender that shaves a share off everything that walks into it."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "cyclone_slash_tailwind_surge",
			BonusElementWeights = new() { { "Wind", 1 } },
			DisplayName = "Tailwind Surge",
			Description = "Spinning blades grant the caster +25% movement speed.",
			MilestoneLevel = 4,
			SpeedMultiplier = 1.25f,
			VisualTag = "TailwindSurge",
			ModulateColor = new Color(0.6f, 0.95f, 1.0f),
			SynergyTag = "Wind / Mobility",
			SynergyDescription = "High mobility kiting advantage."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "cyclone_slash_typhoon_core",
			BonusElementWeights = new() { { "Wind", 1 } },
			DisplayName = "Typhoon Core",
			Description = "Player is engulfed in a colossal permanent hurricane (+150% Area, +100% Damage).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			AreaMultiplier = 2.5f,
			VisualTag = "TyphoonCore",
			ModulateColor = new Color(0.4f, 1.0f, 0.85f),
			SynergyTag = "Wind / Storm",
			SynergyDescription = "Ascension: Massive permanent surrounding whirlwind."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "cyclone_slash_vortex_reaper",
			DisplayName = "Vortex Reaper",
			Description = "Slashes suck enemies into the blade circle and slice for critical damage (+150% Damage).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.5f,
			Effect = SpellEffect.VortexPull,
			EffectValue = 100f,
			VisualTag = "VortexReaper",
			ModulateColor = new Color(0.9f, 1.0f, 0.6f),
			SynergyTag = "Wind / Force",
			SynergyDescription = "Ascension: Gravitational vacuum melee execution."
		});
	}

	private static void SetupSolarFlareEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "solar_flare_radiance",
			DisplayName = "Radiant Glow",
			Description = "+40% Area and pulses 25% faster.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.4f,
			CooldownMultiplier = 0.75f,
			VisualTag = "RadiantGlow",
			ModulateColor = new Color(1.0f, 0.95f, 0.5f),
			SynergyTag = "Light / Speed",
			SynergyDescription = "Faster pulses and larger aura."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "solar_flare_scorching_heat",
			BonusElementWeights = new() { { "Fire", 1 } },
			DisplayName = "Scorching Heat",
			// It promised to ignite, with an Effect Burn of 4 that nothing read: the game has no burn
			// status (.ai/gameplay-design.md, "Status effects"), and Fire carries no rider - its path is
			// raw damage. So the card says what it pays, and the burn's share went into the damage
			// (x1.25 to x1.45) rather than vanishing with it (2026-10-26).
			Description = "The flare runs hot: +45% damage and +1 Fire tag, for 12% slower pulses.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			DamageMultiplier = 1.45f,
			VisualTag = "ScorchingHeat",
			ModulateColor = new Color(1.0f, 0.65f, 0.2f),
			SynergyTag = "Light / Fire",
			SynergyDescription = "Combines Light and Fire elemental synergy."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "solar_flare_blinding_flash",
			DisplayName = "Blinding Flash",
			Description = "Pulses have a 30% chance to stun and knock back enemies.",
			MilestoneLevel = 4,
			KnockbackBonus = 50f,
			VisualTag = "BlindingFlash",
			ModulateColor = new Color(1.0f, 1.0f, 0.8f),
			SynergyTag = "Light / Force",
			SynergyDescription = "Crowd control and repulsion."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "solar_flare_daybreak",
			BonusElementWeights = new() { { "Light", 1 } },
			DisplayName = "Daybreak Avatar",
			Description = "A flare that fills the clearing: +100% Area, +100% Damage, for 12% slower pulses.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			// x2.0, not the x2.5 it promised and never delivered (ElementalPulse read only the player's
			// area until 2026-10-26). On top of Radiant Glow's x1.4 that is a 250 px flare against a
			// 90 px one; x2.5 made it 315, most of a phone's screen, every two seconds.
			AreaMultiplier = 2.0f,
			VisualTag = "Daybreak",
			ModulateColor = new Color(1.0f, 0.95f, 0.4f),
			SynergyTag = "Light / Zenith",
			SynergyDescription = "Ascension: Screen-wide holy solar apocalypse."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "solar_flare_corona_discharge",
			DisplayName = "Corona Discharge",
			// It promised 8 sunbeams (ProjectileCountBonus 8) that ElementalPulse never fired: a pulse
			// has no projectiles. What it paid was the damage, so that is what the card says now
			// (2026-10-26); the beams are a build of their own if the flare ever wants a second shape.
			Description = "Every pulse is a corona discharge: +140% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.4f,
			VisualTag = "CoronaDischarge",
			ModulateColor = new Color(1.0f, 0.85f, 0.2f),
			SynergyTag = "Light / Beams",
			SynergyDescription = "Ascension: Omnidirectional piercing radiant beams."
		});
	}

	// Black Tentacles is a rift that slows and the tentacles in it that slap (scripts/BlackTentacles.cs),
	// so every branch says which half it changes. Four of these five used to promise things nothing
	// read: Abyssal Spread's and both ascensions' area never reached the hit (only their x2 / x1.7
	// ScaleMultiplier reached the DRAWING, which is gone - the rift is drawn at the size it slows),
	// Soul Drain's lifesteal had no reader at all, and the Maw's vortex was never pulled. Each now
	// does what its card says, and the cards were rewritten to say only that.
	private static void SetupBlackTentaclesEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "tentacles_constriction",
			BonusElementWeights = new() { { "Earth", 1 } },
			DisplayName = "Crushing Grip",
			Description = "+50% slap damage, and every slap roots its target for 2 seconds.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			DamageMultiplier = 1.5f,
			// Read by BlackTentacles.Land: a root (a slow of 0) on the enemy each slap lands on.
			Effect = SpellEffect.RootDuration,
			EffectValue = 2.0f,
			VisualTag = "CrushingGrip",
			ModulateColor = new Color(0.6f, 0.2f, 0.8f),
			SynergyTag = "Earth / Root",
			SynergyDescription = "The tentacles hold what they hit."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "tentacles_abyssal_spread",
			DisplayName = "Abyssal Spread",
			Description = "+60% rift size and +2 tentacles.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.6f,
			ProjectileCountBonus = 2,
			VisualTag = "AbyssalSpread",
			ModulateColor = new Color(0.4f, 0.15f, 0.6f),
			SynergyTag = "Poison / Area",
			SynergyDescription = "A wider rift to slow in, and more arms to reach across it."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "tentacles_soul_drain",
			DisplayName = "Soul Drain",
			Description = $"Every {BlackTentacles.SoulDrainEvery}th slap that lands heals you 1 HP, up to {BlackTentacles.SoulDrainCap} HP a cast.",
			MilestoneLevel = 4,
			Effect = SpellEffect.Lifesteal,
			EffectValue = 1f,
			VisualTag = "SoulDrain",
			ModulateColor = new Color(0.5f, 0.85f, 0.7f),
			SynergyTag = "Poison / Life",
			SynergyDescription = "A trickle of health while the tentacles are feeding."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "tentacles_elder_god_maw",
			DisplayName = "Elder God Maw",
			Description = "A vast maw opens instead of a rift (+50% size), ringed with eyes. It drags everything inside toward its teeth, and its tentacles slap for +200% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 3.0f,
			// 1.5, not the 2.2 this said before: that never reached the hit, and what the player saw
			// was the x2 drawing of a 90 px rift. 1.5 of a levelled rift (120 px) is that size, honestly.
			AreaMultiplier = 1.5f,
			// Pull speed in px/s, read by BlackTentacles.Scan.
			Effect = SpellEffect.VortexPull,
			EffectValue = 150f,
			VisualTag = "ElderGodMaw",
			ModulateColor = new Color(0.3f, 0.05f, 0.5f),
			SynergyTag = "Darkness / Eldritch",
			SynergyDescription = "Ascension: the ground opens and looks back."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "tentacles_shadow_forest",
			BonusElementWeights = new() { { "Poison", 1 } },
			DisplayName = "Shadow Forest",
			Description = "The rift splinters into a scatter of small rifts over a wider field (+50% size). It opens 44% more often and its tentacles slap for +120% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.2f,
			// 1.5, not 2.5: see the Maw. The drawing it replaced was x1.7 of 90 px.
			AreaMultiplier = 1.5f,
			CooldownMultiplier = 0.560f,
			VisualTag = "ShadowForest",
			ModulateColor = new Color(0.5f, 0.2f, 0.75f),
			SynergyTag = "Poison / Swarm",
			SynergyDescription = "Ascension: a field of tentacles rather than one pit of them."
		});
	}

	// Thorned Vine (id thorn_vine) used the default archetype, whose words were written for a bolt:
	// "+25% Projectile speed", "+2 Pierce or Chain arcs", "+3 Projectiles". The vine has no flight and
	// no chain, so the same five branches - same ids, same numbers, same element gains, so nothing
	// that names them changes - are spelled out for what the vine does with them (scripts/ThornVine.cs):
	// area is its reach, pierce is how many it cuts through before it snags, speed is how fast it
	// grows and lashes, and projectiles are more vines. The element rule is the default's: Grass
	// deepens at level 4, Poison at level 8, so neither ever reaches 3.
	private static void SetupThornVineEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_amplified_impact",
			DisplayName = "Amplified Impact",
			Description = "+35% damage and +30% reach, and the vine drinks deeper of Grass.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.35f,
			AreaMultiplier = 1.3f,
			BonusElementWeights = new() { { "Grass", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "AmplifiedImpact",
			ModulateColor = new Color(1.0f, 0.85f, 0.4f),
			SynergyTag = "Power / Area",
			SynergyDescription = "A longer, heavier lash."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_hyper_acceleration",
			DisplayName = "Hyper Acceleration",
			Description = "-30% cooldown, and the vine grows and lashes 25% faster.",
			MilestoneLevel = 4,
			CooldownMultiplier = 0.7f,
			SpeedMultiplier = 1.25f,
			VisualTag = "HyperAcceleration",
			ModulateColor = new Color(0.4f, 0.9f, 1.0f),
			SynergyTag = "Speed / Cadence",
			SynergyDescription = "Lash after lash."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_reactive_catalyst",
			DisplayName = "Reactive Catalyst",
			Description = "+20% damage, and each lash cuts through 2 more enemies before it snags.",
			MilestoneLevel = 4,
			PierceBonus = 2,
			DamageMultiplier = 1.2f,
			VisualTag = "ReactiveCatalyst",
			ModulateColor = new Color(0.85f, 0.45f, 1.0f),
			SynergyTag = "Pierce",
			SynergyDescription = "Wider swathes through a packed swarm."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_titan_cataclysm",
			DisplayName = "Titan Cataclysm",
			Description = "A colossal vine: +150% damage and twice the reach, steeped in Poison.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.5f,
			AreaMultiplier = 2.0f,
			BonusElementWeights = new() { { "Poison", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "TitanCataclysm",
			ModulateColor = new Color(1.0f, 0.3f, 0.15f),
			SynergyTag = "Ascension / Cataclysm",
			SynergyDescription = "Ascension: one lash that clears a swathe of the field."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_infinite_horizon",
			DisplayName = "Infinite Horizon",
			Description = "-50% cooldown, +3 vines sprouting at other enemies, and each lash cuts through 4 more enemies.",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.5f,
			ProjectileCountBonus = 3,
			PierceBonus = 4,
			VisualTag = "InfiniteHorizon",
			ModulateColor = new Color(0.4f, 0.95f, 1.0f),
			SynergyTag = "Ascension / Speed",
			SynergyDescription = "Ascension: a thicket of lashing vines."
		});
	}

	private static void SetupConeOfColdEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "cone_deep_freeze",
			BonusElementWeights = new() { { "Ice", 1 } },
			DisplayName = "Deep Freeze",
			Description = "Enemies hit by the cone are frozen solid for 1.2 seconds.",
			MilestoneLevel = 4,
			// Read by ConeBlast since 2026-10-26; until then it froze nothing, and it is the level 4
			// the Frostweaver's Apotheosis requires. 1.2, not the 2.0 it promised: the cone recasts
			// every 1.8 s, so 2.0 would never let anything in front of it move again. The
			// SlowMagnitudeBonus of 1.0 that said the same thing as a slow is gone - nothing read it.
			Effect = SpellEffect.Freeze,
			EffectValue = 1.2f,
			VisualTag = "DeepFreeze",
			ModulateColor = new Color(0.5f, 0.85f, 1.0f),
			SynergyTag = "Ice / Freeze",
			SynergyDescription = "Total freeze control in a front cone."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "cone_wide_angle",
			DisplayName = "Arctic Blast",
			Description = "Expands cone arc to 180 degrees with +40% range.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.5f,
			RangeBonus = 50f,
			VisualTag = "ArcticBlast",
			ModulateColor = new Color(0.7f, 0.95f, 1.0f),
			SynergyTag = "Ice / Area",
			SynergyDescription = "Wide semi-circle front sweep."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "cone_ice_shards",
			// Water rather than Metal. Three of the six pure spells branched to Metal and none to
			// Water, which left Water with three carriers in the whole game - two of them passive
			// spells that are being removed. Ice melting into water is also simply the better
			// branch than ice behaving like shrapnel.
			//
			// The element path (ElementPaths). No rider: Water's is a drag, and every enemy the cone
			// touches is already slowed harder than that (the strongest slow wins). It promised five
			// piercing spikes, which ConeBlast never fired - it reads neither projectile count nor
			// pierce - so the card says what it pays: the damage and the tag. (The five and the three
			// went with the reader check, EvolutionStatReaders.)
			BonusElementWeights = new() { { "Water", 1 } },
			DisplayName = "Frost Shrapnel",
			Description = "Meltwater runs through the cone: +30% damage and +1 Water tag.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.3f,
			VisualTag = "FrostShrapnel",
			ModulateColor = ElementColors.GetEvolutionTint(Element.Water),
			SynergyTag = "Ice / Water",
			SynergyDescription = "Branches into Water. A harder-hitting cone that counts toward Water instead of Ice."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "cone_zero_point_wave",
			BonusElementWeights = new() { { "Ice", 1 } },
			DisplayName = "Zero Point Shockwave",
			Description = "A freezing blast that reaches far further: +150% Damage, +40% reach.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.5f,
			// 1.4, not the 2.2 it promised and never delivered: a 260 px cone at x2.2 reaches 572 px,
			// most of the screen. x1.4 is 364 px and about twice the area, which is the gap the
			// Frostweaver measured below the other wizards. The spray is drawn out of the cone's own
			// range, which carries this Area, so it is drawn as far as it hurts; the ScaleMultiplier of
			// 1.4 that once matched it was dropped by Player.FireConeBlast and is gone. The "360-degree"
			// blast the old text described was never built - the full ring is The Long Winter's, the
			// Apotheosis, and stays its own.
			AreaMultiplier = 1.4f,
			VisualTag = "ZeroPointWave",
			ModulateColor = new Color(0.85f, 0.95f, 1.0f),
			SynergyTag = "Ice / FullBurst",
			SynergyDescription = "Ascension: Omnidirectional 360-degree shatter nova."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "cone_glacier_tide",
			DisplayName = "Glacier Tide",
			Description = "Surges forward as an unstoppable wall of ice pushing all enemies away.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			KnockbackBonus = 120f,
			VisualTag = "GlacierTide",
			ModulateColor = new Color(0.6f, 0.9f, 1.0f),
			SynergyTag = "Ice / Force",
			SynergyDescription = "Ascension: Massive sweeping glacier wave with huge knockback."
		});
	}

	private static void SetupMeteorSwarmEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "meteor_cluster",
			DisplayName = "Meteor Shower",
			Description = "Spawns +3 meteors per volley with +30% impact area.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 3,
			AreaMultiplier = 1.3f,
			VisualTag = "MeteorShower",
			ModulateColor = new Color(1.0f, 0.55f, 0.2f),
			SynergyTag = "Fire / Multishot",
			SynergyDescription = "High carpet-bombing saturation."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "meteor_magma_crater",
			DisplayName = "Magma Craters",
			Description = "Impacts leave burning lava craters that incinerate ground for 4s.",
			MilestoneLevel = 4,
			Effect = SpellEffect.Burn,
			EffectValue = 4f,
			VisualTag = "MagmaCraters",
			ModulateColor = new Color(1.0f, 0.35f, 0.1f),
			SynergyTag = "Fire / Zone",
			SynergyDescription = "Area denial and lingering damage."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "meteor_starcrash",
			BonusElementWeights = new() { { "Earth", 1 } },
			DisplayName = "Cosmic Impact",
			Description = "Increases direct impact damage by +75% with heavy shockwaves.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			DamageMultiplier = 1.75f,
			KnockbackBonus = 80f,
			VisualTag = "CosmicImpact",
			ModulateColor = new Color(1.0f, 0.8f, 0.3f),
			SynergyTag = "Earth / Force",
			SynergyDescription = "Massive single-impact burst."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "meteor_extinction_event",
			BonusElementWeights = new() { { "Fire", 1 } },
			DisplayName = "Extinction Event",
			Description = "Summons a colossal cataclysmic asteroid that obliterates the entire screen (+250% Damage).",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 3.5f,
			AreaMultiplier = 3.0f,
			VisualTag = "ExtinctionEvent",
			ModulateColor = new Color(1.0f, 0.2f, 0.05f),
			SynergyTag = "Fire / Cataclysm",
			SynergyDescription = "Ascension: Screen-wiping single asteroid impact."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "meteor_orbital_bombardment",
			DisplayName = "Orbital Bombardment",
			Description = "Continuous non-stop rain of burning stars (-60% Cooldown, +6 Meteors).",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.4f,
			ProjectileCountBonus = 6,
			VisualTag = "OrbitalBombardment",
			ModulateColor = new Color(1.0f, 0.7f, 0.15f),
			SynergyTag = "Fire / Storm",
			SynergyDescription = "Ascension: Continuous unending meteor rain."
		});
	}

	// The Geomancer's opener. It had no block of its own and fell through to the generic archetype,
	// which is a poor fit for a starting spell - the one spell every Geomancer run is built around
	// deserves real branches, and as a pure spell it needs the element gains the other four have.
	private static void SetupObsidianSpikeEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "obsidian_spike_splinter_field",
			DisplayName = "Splinter Field",
			Description = "The spike shatters on eruption, throwing stone shards through everything around it (+35% radius).",
			MilestoneLevel = 4,
			BonusElementWeights = new() { { "Earth", 1 } },
			// The shatter IS the wider eruption. It used to also carry ExplosionOnHit, which only
			// Magic Missile reads, so the required branch for the Geomancer's form did nothing at all.
			// Scale matches area so the spike is drawn as wide as it hurts.
			AreaMultiplier = 1.35f,
			ScaleMultiplier = 1.35f,
			VisualTag = "SplinterField",
			ModulateColor = new Color(0.55f, 0.48f, 0.42f),
			SynergyTag = "Earth / Shatter",
			SynergyDescription = "Deepens Earth. Turns a single-target spike into a small area hit."
		});

		// The element path (ElementPaths). Its shadow bleed was a DotDamage effect, which GroundSpike
		// never read, so the branch dealt nothing but the tag. Darkness's rider is what it does now.
		const float veinExposure = 0.10f;
		const float veinSeconds = 3f;
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "obsidian_spike_umbral_vein",
			DisplayName = "Umbral Vein",
			Description = "Black glass drinks the light: +1 Darkness tag, and "
				+ ElementPaths.Describe(ElementRider.Vulnerable, veinExposure, veinSeconds) + ".",
			MilestoneLevel = 4,
			BonusElementWeights = new() { { "Darkness", 1 } },
			Rider = ElementRider.Vulnerable,
			RiderMagnitude = veinExposure,
			RiderSeconds = veinSeconds,
			VisualTag = "UmbralVein",
			// The ramp's tint rather than the old near-black, which multiplied the spike almost out of sight.
			ModulateColor = ElementColors.GetEvolutionTint(Element.Darkness),
			SynergyTag = "Earth / Darkness",
			SynergyDescription = "Branches into Darkness. Trades attunement for a second element, and every spike marks what it hits for the rest of the loadout."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "obsidian_spike_deep_strata",
			DisplayName = "Deep Strata",
			Description = "The spike is driven from far deeper: +45% damage and a longer reach.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.45f,
			RangeBonus = 120f,
			VisualTag = "DeepStrata",
			ModulateColor = new Color(0.42f, 0.40f, 0.44f),
			SynergyTag = "Earth / Power",
			SynergyDescription = "No new element. Straight power for a build that is already where it wants to be."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "obsidian_spike_tectonic_uprising",
			DisplayName = "Tectonic Uprising",
			Description = "A ridge of black stone tears open beneath the target (+120% Damage, +60% Area).",
			MilestoneLevel = 8,
			BonusElementWeights = new() { { "Earth", 1 } },
			DamageMultiplier = 2.2f,
			// 1.6, not the 2.5 it promised and never delivered: with Splinter Field that is a 97 px
			// eruption, about the Frostweaver's cone in area x damage. 2.5 would be 152 px, eleven
			// times the base disc. KnockbackBonus 140 is gone too - nothing reads it.
			AreaMultiplier = 1.6f,
			ScaleMultiplier = 1.6f,
			VisualTag = "TectonicUprising",
			ModulateColor = new Color(0.36f, 0.33f, 0.36f),
			SynergyTag = "Earth / Cataclysm",
			SynergyDescription = "Ascension: a third Earth instance, and the ground goes with it."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "obsidian_spike_basalt_lance",
			DisplayName = "Basalt Lance",
			Description = "The spike is forged rather than grown - a needle of volcanic glass that runs enemies through.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.4f,
			PierceBonus = 4,
			ScaleMultiplier = 1.3f,
			VisualTag = "BasaltLance",
			ModulateColor = new Color(0.52f, 0.54f, 0.60f),
			SynergyTag = "Earth / Metal",
			SynergyDescription = "Ascension: no new element - just a needle that skewers whole columns."
		});
	}

	// Toxic Spore Burst sows mushrooms that swell and burst (scripts/SporeMushroom.cs). It used to
	// land in the default archetype, whose Titan Cataclysm promised "+100% Area" to a pulse that
	// never read GetAreaMultiplierAtLevel - the "Colossal" Ascension grew nothing. Every branch here
	// is about the mushrooms, every area term reaches SporeMushroom's radius, and that radius is
	// what is drawn. The numbers in the cards are SporeMushroom's constants, interpolated.
	//
	// The level 4 milestone is the element decision for a hybrid (Poison + Grass): Lingering Rot
	// deepens Poison and pays the hybrid's cooldown price; Ember Caps TRADES the spores' Poison for
	// Fire (ReplacesElement - still two elements, no gain, so no price) and the art turns to embers;
	// Giant Caps changes no tag. At level 8 Mycelial Bloom deepens Grass - the mushroom itself, the
	// one tag every path keeps - and Doomcap changes no tag, so its gate asks for whatever the spell
	// has become (Player.GetAscensionGateElements reads the current tags). Checked by
	// RegressionChecks.ValidateEvolutionElementCeiling: Lingering Rot + Bloom is Poison 2 / Grass 2,
	// Ember Caps + Bloom is Grass 2 / Fire 1, and nothing reaches three elements or three of one.
	private static void SetupToxicSporeEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SporeMushroom.LingeringRotId,
			DisplayName = "Lingering Rot",
			Description = $"Each burst leaves a spore cloud as wide as itself for {SporeMushroom.CloudSeconds:0.#}s "
				+ "that keeps poisoning anything inside. Poison +1 per tick.",
			MilestoneLevel = 4,
			PoisonTickBonus = 1,
			BonusElementWeights = new() { { "Poison", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "LingeringRot",
			ModulateColor = new Color(0.78f, 1.0f, 0.55f),
			SynergyTag = "Poison / Zone",
			SynergyDescription = "Deepens Poison. Best with anything that holds enemies still."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SporeMushroom.EmberCapsId,
			DisplayName = "Ember Caps",
			// Names both tags, as the card's element line does ("Poison → Fire").
			Description = $"The spores trade Poison for Fire: bursts hit 50% harder and light {SporeMushroom.EmberPatches} "
				+ $"patches for {SporeMushroom.EmberSeconds:0.#}s that burn for {SporeMushroom.EmberTickFraction * 100f:0}% "
				+ "of the burst every half second.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.5f,
			ReplacesElement = "Poison",
			BonusElementWeights = new() { { "Fire", 1 } },
			VisualTag = "EmberCaps",
			ModulateColor = new Color(1.0f, 0.62f, 0.32f),
			SynergyTag = "Grass / Fire",
			SynergyDescription = "For a build that wants Fire tags, not Poison."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SporeMushroom.GiantCapsId,
			DisplayName = "Giant Caps",
			Description = "Two fewer mushrooms, and each takes 67% longer to swell - but bursts 60% wider for +80% damage. "
				+ "Cooldown +20%.",
			MilestoneLevel = 4,
			ProjectileCountBonus = -2,
			SpeedMultiplier = 0.6f,
			AreaMultiplier = 1.6f,
			DamageMultiplier = 1.8f,
			CooldownMultiplier = 1.2f,
			VisualTag = "GiantCaps",
			SynergyTag = "Size / Power",
			SynergyDescription = "No new element. Fewer, slower, far bigger bursts."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SporeMushroom.DoomcapId,
			DisplayName = "Doomcap",
			Description = $"One titanic Doomcap grows in place of the scatter for {SporeMushroom.DoomcapFuseSeconds:0.#}s, "
				+ $"then bursts 180% wider. Every mushroom it replaces adds {SporeMushroom.DoomcapDamagePerMushroom * 100f:0}% "
				+ $"damage (x{1f + SporeMushroom.DoomcapDamagePerMushroom * 6f:0.#} at six). Cooldown +40%.",
			MilestoneLevel = 8,
			AreaMultiplier = 2.8f,
			CooldownMultiplier = 1.4f,
			VisualTag = "Doomcap",
			ModulateColor = new Color(0.92f, 1.0f, 0.70f),
			SynergyTag = "Ascension / Colossal",
			SynergyDescription = "Ascension: one mushroom the size of the fight, in whatever element the spores became."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SporeMushroom.MycelialBloomId,
			DisplayName = "Mycelial Bloom",
			Description = $"Every burst sends mycelium out: {SporeMushroom.BloomFirstChildren} more mushrooms sprout past its rim, "
				+ $"swell twice as fast and burst at {SporeMushroom.BloomRadiusScale * 100f:0}% of its size for half damage, "
				+ $"and each of those sprouts {SporeMushroom.BloomSecondChildren} more.",
			MilestoneLevel = 8,
			BonusElementWeights = new() { { "Grass", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "MycelialBloom",
			ModulateColor = new Color(0.82f, 1.0f, 0.62f),
			SynergyTag = "Ascension / Grass",
			SynergyDescription = "Ascension: deepens Grass. The chain spreads across the ground the swarm is crossing."
		});
	}

	// --- The default archetype, spell by spell -----------------------------------------------------
	//
	// Five branches that every spell without a hand-written block used to share word for word:
	// Amplified Impact, Hyper Acceleration and Reactive Catalyst at level 4, Titan Cataclysm and
	// Infinite Horizon at level 8. Their words were written for a bolt - "+25% Projectile speed",
	// "projectiles pierce 2 more enemies", "+3 Projectiles, +4 Pierce" - and thirteen spells that are
	// mostly not bolts carried them. Gravewell, a trap in the ground, offered projectile speed and
	// pierce (2026-10-24: "isn't it an aoe? does that even make sense?"), and across the thirteen the
	// speed, the pierce, and often the count and the area, reached nothing at all.
	//
	// Each spell has its own copy now, in Thorned Vine's way. Branch() fixes what a copy may not change -
	// the id, the name, the element gain, the attunement price of that gain and the look, which are what
	// ApotheosisCatalog, RiptideWave.TitanEvolutionId, the ascension browser's SeenAscensionIds
	// (spell_id:branch_id) and RegressionChecks name - so a block can only change what a branch DOES and
	// SAYS. Every number a block sets is one its spell reads (EvolutionStatReaders, held by
	// RegressionChecks.ValidateEvolutionReaders). The budget is the archetype's: a stat that means
	// nothing to a spell is traded for one of like weight that it reads. What each one became is the
	// table in .ai/spell-roster.md, "The default archetype, spell by spell".

	private enum DefaultBranch
	{
		AmplifiedImpact,
		HyperAcceleration,
		ReactiveCatalyst,
		TitanCataclysm,
		InfiniteHorizon,
	}

	/// <summary>
	/// One default-archetype branch of <paramref name="spell"/>, with the words and stats its block
	/// gives it. Its id, name, milestone, element gain, the price of that gain and its look are set here
	/// and nowhere else.
	/// </summary>
	private static SpellEvolutionOption Branch(SpellData spell, DefaultBranch branch, string description,
		string synergyTag, string synergy, Action<SpellEvolutionOption> stats)
	{
		// The archetype's element rule, derived rather than authored: the first element deepens at level
		// 4 and the second at level 8, so a hybrid reaches 2 of each and never 3 of either, which keeps
		// depth a pure spell's speciality. A pure spell deepens its one element at both, for nothing - it
		// is owed that for carrying half a hybrid's base weight - and a hybrid buys its gain with
		// AttunementGainCooldownCost.
		List<string> elements = ElementNames(spell);
		bool isPure = elements.Count == 1;
		string first = elements.Count > 0 ? elements[0] : string.Empty;
		string second = elements.Count > 1 ? elements[1] : first;
		float gainCost = isPure ? 1.0f : AttunementGainCooldownCost;

		(string suffix, string name, int milestone, string gain, string visual, Color tint) = branch switch
		{
			DefaultBranch.AmplifiedImpact => ("amplified_impact", "Amplified Impact", 4, first, "AmplifiedImpact", new Color(1.0f, 0.85f, 0.4f)),
			DefaultBranch.HyperAcceleration => ("hyper_acceleration", "Hyper Acceleration", 4, string.Empty, "HyperAcceleration", new Color(0.4f, 0.9f, 1.0f)),
			DefaultBranch.ReactiveCatalyst => ("reactive_catalyst", "Reactive Catalyst", 4, string.Empty, "ReactiveCatalyst", new Color(0.85f, 0.45f, 1.0f)),
			DefaultBranch.TitanCataclysm => ("titan_cataclysm", "Titan Cataclysm", 8, second, "TitanCataclysm", new Color(1.0f, 0.3f, 0.15f)),
			_ => ("infinite_horizon", "Infinite Horizon", 8, string.Empty, "InfiniteHorizon", new Color(0.4f, 0.95f, 1.0f)),
		};

		var option = new SpellEvolutionOption
		{
			Id = $"{spell.Id}_{suffix}",
			DisplayName = name,
			Description = description,
			MilestoneLevel = milestone,
			VisualTag = visual,
			ModulateColor = tint,
			SynergyTag = synergyTag,
			SynergyDescription = synergy,
		};
		stats?.Invoke(option);
		if (gain.Length > 0)
		{
			option.BonusElementWeights = new() { { gain, 1 } };
			// After the block's own stats, so the price multiplies whatever cooldown the branch buys.
			option.CooldownMultiplier *= gainCost;
		}
		return option;
	}

	// The level 4 element path of a pure spell (below, "Element paths"). Every pure spell on the
	// archetype has one; a missing entry is a broken catalog, said out loud rather than papered over.
	private static void AddElementPath(SpellData spell)
	{
		SpellEvolutionOption path = BuildDefaultElementPath(spell);
		if (path == null)
		{
			GD.PushError($"SpellEvolutionCatalog: pure spell '{spell.Id}' has no element path in BuildDefaultElementPath.");
			return;
		}
		spell.Level4Options.Add(path);
	}

	// A spell with no block of its own. No spell in the roster lands here any more. One that does gets
	// the old words for a bolt, and RegressionChecks.ValidateEvolutionReaders reports it until it has an
	// entry in EvolutionStatReaders - and then reports every number here its script does not read, which
	// is the signal that it needs a block of its own like the thirteen below.
	private static void SetupDefaultArchetypeEvolutions(SpellData spell)
	{
		List<string> elements = ElementNames(spell);
		bool isPure = elements.Count == 1;
		string first = elements.Count > 0 ? elements[0] : string.Empty;
		string second = elements.Count > 1 ? elements[1] : first;

		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			first.Length > 0 ? $"+35% Damage and +30% Area, and the spell drinks deeper of {first}." : "+35% Damage and +30% Area of effect.",
			"Power / Area", "General power enhancement.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% Cooldown and +25% Projectile speed.",
			"Speed / Cadence", "Rapid firing cycle.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		// A pure spell's third choice is its element path - the branch that ADDS an element, against
		// Amplified Impact, which deepens its own (.ai/spell-roster.md, "Element paths"). A hybrid keeps
		// Reactive Catalyst: it names two elements already, and may never name a third
		// (RegressionChecks.ValidateEvolutionElementCeiling).
		SpellEvolutionOption path = isPure ? BuildDefaultElementPath(spell) : null;
		spell.Level4Options.Add(path ?? Branch(spell, DefaultBranch.ReactiveCatalyst,
			"+20% Damage, and projectiles pierce 2 more enemies.",
			"Pierce / Power", "Multi-target efficiency.",
			o => { o.DamageMultiplier = 1.2f; o.PierceBonus = 2; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			second.Length > 0 ? $"Colossal scale ascension (+150% Damage, +100% Area), steeped in {second}." : "Colossal scale ascension (+150% Damage, +100% Area).",
			"Ascension / Cataclysm", "Ascension: Massive raw damage and area destruction.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; o.ScaleMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			"Continuous rapid cycling ascension (-50% Cooldown, +3 Projectiles, +4 Pierce).",
			"Ascension / Speed", "Ascension: Non-stop rapid barrage.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; o.ScaleMultiplier = 1.4f; }));
	}

	// Gravewell (scripts/GravewellTrap.cs): a pit on the swarm's path that springs when trodden on and
	// roots what it catches. Area is the collapse's blast. Speed is how fast a pit opens - it cannot
	// spring while opening. Projectiles are pits, which it read already. Pierce means nothing to a pit
	// that catches everything inside its blast, so it became the hold: Root Duration, how long the
	// earth keeps what it took.
	private static void SetupGravewellEvolutions(SpellData spell)
	{
		const float catalystHold = 0.5f;
		const float horizonHold = 1.0f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage and a 30% wider collapse.",
			"Power / Area", "A wider fall catches what was following.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and each pit opens 25% faster, ready sooner for what walks in.",
			"Speed / Cadence", "More pits waiting, and armed sooner.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			$"+20% damage, and the earth holds what it catches {catalystHold:0.#}s longer.",
			"Power / Hold", "A pinned enemy is ground the swarm cannot cross.",
			o => { o.DamageMultiplier = 1.2f; o.Effect = SpellEffect.RootDuration; o.EffectValue = catalystHold; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A colossal grave: +150% damage and twice the blast.",
			"Ascension / Cataclysm", "Ascension: one collapse swallows the pack that set it off.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% cooldown, 3 more pits a cast, and whatever falls in is held {horizonHold:0.#}s longer.",
			"Ascension / Speed", "Ascension: a grave on every approach.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.Effect = SpellEffect.RootDuration; o.EffectValue = horizonHold; }));
	}

	// Hollow Star (scripts/HollowStar.cs): a well that drags a pack into one knot and collapses on it.
	// Area is both the pull's reach and the collapse. Speed is how hard it drags. Projectiles are wells,
	// each on its own pack (Player.FireHollowStar). Pierce became Root Duration on the collapse: the knot
	// is held where it closed, which is what the spell is for - it gathers for the area spells beside
	// it. Amplified Impact and Titan Cataclysm are John Dee's Apotheosis path; their numbers stand.
	private static void SetupHollowStarEvolutions(SpellData spell)
	{
		const float knotHold = 1.0f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage, and the well pulls from 30% farther and closes 30% wider.",
			"Power / Area", "A bigger knot for every area spell you own.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and the well drags 25% harder, into a tighter knot.",
			"Speed / Cadence", "A tighter knot, more often.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A colossal well: +150% damage, twice the reach and twice the collapse.",
			"Ascension / Cataclysm", "Ascension: one well empties the ground around it.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% cooldown, 3 more wells open on other packs, and each knot is held where it closed for {knotHold:0.#}s.",
			"Ascension / Speed", "Ascension: knots all over the field, held still for your area spells.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.Effect = SpellEffect.RootDuration; o.EffectValue = knotHold; }));
	}

	// Bramble Seed (scripts/BrambleSeed.cs): a seed at your feet that grows into a patch of thorns. Area
	// is its full-grown radius. Speed is how fast it grows there. Projectiles are patches allowed to stand
	// at once - still one seed a cast, since a cast that scattered seeds would be Wildwood's verb, the
	// Apotheosis's - and they matter: with the cap fixed at three, a halved cooldown only withered each
	// patch before it had grown. Pierce means nothing to thorns that hurt everything in the patch, so it
	// became their drag. Amplified Impact and Titan Cataclysm are Vainamoinen's Apotheosis path.
	private static void SetupBrambleSeedEvolutions(SpellData spell)
	{
		const float drag = 0.2f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage, and the thicket grows 30% wider.",
			"Power / Area", "More ground to drag them back across.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and every seed grows to full size 25% faster.",
			"Speed / Cadence", "Wide and dangerous sooner.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A colossal thicket: +150% damage and twice as wide.",
			"Ascension / Cataclysm", "Ascension: the patch is the field.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% cooldown, 3 more patches may grow at once, and their thorns slow {drag * 100f:0}% harder.",
			"Ascension / Speed", "Ascension: plant as you kite, and the ground behind you stays thorned.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.SlowMagnitudeBonus = drag; }));
	}

	// Contagion (scripts/Contagion.cs): a plague on one carrier that jumps to everything near it when it
	// dies. Area is the jump. Speed is how fast the poison works - the same dose, sooner, so carriers
	// fall and pass it on sooner. Projectiles are carriers, each with its own plague. Pierce is how many
	// more enemies one plague may reach before it burns out (Contagion.MaxInfected).
	private static void SetupContagionEvolutions(SpellData spell)
	{
		const int reach = 8;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage, and the plague jumps 30% farther.",
			"Power / Area", "Looser packs still catch it.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and the plague works 25% faster: the same dose of poison, sooner.",
			"Speed / Cadence", "Carriers fall sooner, so it spreads sooner.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A colossal plague: +150% damage, and it jumps twice as far.",
			"Ascension / Cataclysm", "Ascension: one carrier can take a wave.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% cooldown, 3 more carriers a cast, and each plague can reach {reach} more enemies before it burns out.",
			"Ascension / Speed", "Ascension: the swarm is never free of it.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = reach; }));
	}

	// Iron Palisade (scripts/IronPalisade.cs): a line of stakes between you and the pack. Area is the
	// wall's length (it read none, and Titan Cataclysm's x2 visual scale stood in for it). Speed is how
	// often the stakes cut what leans on them. Projectiles are walls, each toward another pack. Pierce
	// means nothing to a wall, which stops what it meets, so it became Zone Duration: how long it stands.
	private static void SetupIronPalisadeEvolutions(SpellData spell)
	{
		const float stand = 1.0f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage, and a 30% longer wall.",
			"Power / Area", "A front too long to walk round.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and the stakes cut 25% more often.",
			"Speed / Cadence", "What leans on the wall is cut to pieces.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A colossal palisade: +150% damage and twice as long.",
			"Ascension / Cataclysm", "Ascension: one wall holds a whole side.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% cooldown, 3 more walls a cast toward other packs, and each stands {stand:0.#}s longer.",
			"Ascension / Speed", "Ascension: stakes on every side of you.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.Effect = SpellEffect.ZoneDuration; o.EffectValue = stand; }));
	}

	// Kindled Ward (scripts/KindledWard.cs): a mote of light that drifts at the nearest enemy and burns
	// what is near it. Area is its burn. Speed is how fast it drifts - still slower than you, so still
	// placed. Projectiles are wards allowed to burn at once, which it read already. Pierce means nothing
	// to a burn that takes everything within reach, so it became Zone Duration: how long a ward lasts.
	private static void SetupKindledWardEvolutions(SpellData spell)
	{
		const float longer = 3f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage, and each ward burns 30% wider.",
			"Power / Area", "Each mote takes more of the pack it reaches.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and wards drift 25% faster.",
			"Speed / Cadence", "Still slower than you, so still placed.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"Colossal wards: +150% damage and twice the burn.",
			"Ascension / Cataclysm", "Ascension: each ward is a small sun.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% cooldown, 3 more wards may burn at once, and each lasts {longer:0}s longer.",
			"Ascension / Speed", "Ascension: a constellation of wards around you.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.Effect = SpellEffect.ZoneDuration; o.EffectValue = longer; }));
	}

	// Riptide: a wave fired once down a line, piercing what it passes (ElementalBolt, drawn by
	// RiptideWave). It is a projectile, so the bolt's words fit it - and are true now that every number
	// reaches it: ElementalBolt reads the evolutions' Speed, and Player.FireBoltSpell fires the spell's
	// count. A bolt's Area is its size, which is the node's scale: the collision circle and the wave drawn
	// from it grow together. So the archetype's Area (which ElementalBolt never read) is carried as
	// ScaleMultiplier at the same number - Titan Cataclysm's x2 was already the doubled swath of
	// RiptideWave's titanic form. Amplified Impact and Titan Cataclysm are Tituba's Apotheosis path.
	private static void SetupRiptideEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage and a 30% bigger wave.",
			"Power / Area", "A wider line to catch them in.",
			o => { o.DamageMultiplier = 1.35f; o.ScaleMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and the wave runs 25% faster.",
			"Speed / Cadence", "Less time to step out of its way.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A titanic wave: +150% damage and twice the swath it cuts.",
			"Ascension / Cataclysm", "Ascension: the sea comes in.",
			o => { o.DamageMultiplier = 2.5f; o.ScaleMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			"-50% cooldown, and 3 more waves at other enemies, each 40% bigger and tearing through 4 more.",
			"Ascension / Speed", "Ascension: lines of water every way you face.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; o.ScaleMultiplier = 1.4f; }));
	}

	// Molten Shard: a homing bolt at a random enemy in range that burns away a share of its health
	// (ElementalBolt). A projectile, so it keeps the bolt's words; what changed is that they are true -
	// its Speed and its extra shards reached nothing before - and that its size is its scale, as for
	// Riptide.
	private static void SetupMoltenShardEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage and 30% larger shards.",
			"Power / Area", "Harder to miss what you aimed at.",
			o => { o.DamageMultiplier = 1.35f; o.ScaleMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown and +25% projectile speed.",
			"Speed / Cadence", "Rapid firing cycle.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			"+20% damage, and shards pierce 2 more enemies.",
			"Pierce / Power", "Through the front rank and into the next.",
			o => { o.DamageMultiplier = 1.2f; o.PierceBonus = 2; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"Colossal shards: +150% damage and twice the size.",
			"Ascension / Cataclysm", "Ascension: each shard is a falling forge.",
			o => { o.DamageMultiplier = 2.5f; o.ScaleMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			"-50% cooldown, +3 shards a cast at other enemies, +4 pierce, and 40% larger.",
			"Ascension / Speed", "Ascension: molten rain across the field.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; o.ScaleMultiplier = 1.4f; }));
	}

	// Hunter's Draw: a volley loosed when you stop running (Player.LooseHuntersVolley, ElementalBolt
	// arrows). A projectile, so it keeps the bolt's words, and every number already reached it but the
	// Speed, which ElementalBolt reads now. Its cooldown is the time to nock; its size is its scale.
	private static void SetupHuntersDrawEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage and 30% broader arrowheads.",
			"Power / Area", "Harder to slip past a volley.",
			o => { o.DamageMultiplier = 1.35f; o.ScaleMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% time to nock, and arrows fly 25% faster.",
			"Speed / Cadence", "Draw, stop, loose, and run again sooner.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			"+20% damage, and arrows pierce 2 more enemies.",
			"Pierce / Power", "Line them up and one volley takes the column.",
			o => { o.DamageMultiplier = 1.2f; o.PierceBonus = 2; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"Colossal arrows: +150% damage and twice the size.",
			"Ascension / Cataclysm", "Ascension: every arrow a ballista bolt.",
			o => { o.DamageMultiplier = 2.5f; o.ScaleMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			"-50% time to nock, +3 arrows in every volley, +4 pierce, and 40% larger.",
			"Ascension / Speed", "Ascension: a storm of arrows every time you stop.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; o.ScaleMultiplier = 1.4f; }));
	}

	// Glacial Spike (scripts/IceSpike.cs): spikes that erupt from the ground at an enemy and hit what
	// stands round them. Area is the spike's radius - it read none, and Titan Cataclysm's x2 visual
	// scale drew the spike twice as big as it hit. Speed is how fast the ice erupts. Projectiles are
	// spikes, which it read already. Pierce means nothing to an eruption that hits everything in its
	// radius, so it became Root Duration: what a spike impales is pinned.
	private static void SetupGlacialSpikeEvolutions(SpellData spell)
	{
		const float catalystPin = 0.4f;
		const float horizonPin = 0.6f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage and 30% wider spikes.",
			"Power / Area", "More of the pack on the ice when it breaks.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and the ice erupts 25% faster.",
			"Speed / Cadence", "Less time to walk off the spot.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			$"+20% damage, and each spike pins what it impales for {catalystPin:0.#}s.",
			"Power / Pin", "A held enemy is one the next spike finds.",
			o => { o.DamageMultiplier = 1.2f; o.Effect = SpellEffect.RootDuration; o.EffectValue = catalystPin; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"Colossal spikes: +150% damage and twice as wide.",
			"Ascension / Cataclysm", "Ascension: the ground itself turns to ice.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% cooldown, 3 more spikes a cast, and what a spike impales is pinned {horizonPin:0.#}s more.",
			"Ascension / Speed", "Ascension: a field of ice that holds what it catches.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.Effect = SpellEffect.RootDuration; o.EffectValue = horizonPin; }));
	}

	// Scorching Ray (scripts/ScorchingRayBeam.cs): beams at the nearest enemies, each striking one.
	// Speed is how fast a beam reaches its target. Pierce burns through the target into the enemies
	// behind it on the beam's line. Projectiles are beams, which it read already. A beam that strikes
	// one enemy has no Area, so Area is a burst where it lands - none until an evolution grows one -
	// that scorches what stands beside the target for half damage (ScorchingRayBeam.BaseSplashRadius).
	private static void SetupScorchingRayEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"+35% damage, and every ray bursts on its target, scorching what stands beside it for {ScorchingRayBeam.SplashDamageFraction * 100f:0}% of its damage.",
			"Power / Area", "A beam that singes the pack around what it burns.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% cooldown, and the rays strike 25% faster.",
			"Speed / Cadence", "Rapid firing cycle.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			"+20% damage, and each ray burns through its target into 2 more enemies behind it.",
			"Pierce / Power", "Through the front rank and into the next.",
			o => { o.DamageMultiplier = 1.2f; o.PierceBonus = 2; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"Colossal rays: +150% damage, and every ray bursts on its target, wide enough to scorch a pack for {ScorchingRayBeam.SplashDamageFraction * 100f:0}% of its damage.",
			"Ascension / Cataclysm", "Ascension: every beam ends in a blast.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			"-50% cooldown, 3 more rays, and each burns through 4 more enemies behind its target.",
			"Ascension / Speed", "Ascension: a lattice of beams through the crowd.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; }));
	}

	// Cinderbreath (scripts/Flamethrower.cs, FlamePuff.cs): a held jet of thrown tongues of flame. Area
	// is how WIDE each tongue burns - never how far, or the jet would outrange a bolt - and it reached
	// nothing before. Speed is how fast tongues leave the nozzle. Projectiles are tongues per throw.
	// Pierce means nothing to a tongue that already burns everything it touches, so Infinite Horizon's
	// projectiles and pierce are two more tongues a throw together: three at once, tripling the jet.
	// Its cooldown is the recharge between burns. Amplified Impact and Titan Cataclysm are the
	// Pyromancer's Apotheosis path.
	private static void SetupCinderbreathEvolutions(SpellData spell)
	{
		const int extraTongues = 2;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage, and tongues of flame 30% wider.",
			"Power / Area", "More of the cone on fire.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"-30% recharge between burns, and the jet throws its tongues 25% faster.",
			"Speed / Cadence", "Burn after burn.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A colossal jet: +150% damage, and tongues twice as wide.",
			"Ascension / Cataclysm", "Ascension: a wall of fire at arm's length.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"-50% recharge between burns, and the jet throws {1 + extraTongues} tongues at a time.",
			"Ascension / Speed", "Ascension: a jet that is hardly ever dark.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = extraTongues; }));
	}

	// Mirefoot (scripts/TrailWeaver.cs, LingeringZone.cs): bog pools laid behind you as you walk. Its
	// cooldown is how far apart the pools are laid, and the cards say so. Area is a pool's radius. Speed
	// is how often a pool bites. Pierce means nothing to a pool that hurts everything in it, so Reactive
	// Catalyst's became drag (Slow). Projectiles are pools the trail may hold at once - a longer trail,
	// since the cooldown already buys a denser one - and Infinite Horizon's pierce became Zone Duration,
	// how long a pool lingers.
	private static void SetupMirefootEvolutions(SpellData spell)
	{
		const float drag = 0.15f;
		const int morePools = 5;
		const float linger = 1.5f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"+35% damage, and pools 30% wider.",
			"Power / Area", "A wider wake to follow you through.",
			o => { o.DamageMultiplier = 1.35f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"Pools are laid 30% closer together, and bite 25% more often.",
			"Speed / Cadence", "A thicker trail for every step.",
			o => { o.CooldownMultiplier = 0.7f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			$"+20% damage, and the bog slows {drag * 100f:0}% harder.",
			"Power / Drag", "What follows you cannot catch you.",
			o => { o.DamageMultiplier = 1.2f; o.SlowMagnitudeBonus = drag; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"A colossal mire: +150% damage and pools twice as wide.",
			"Ascension / Cataclysm", "Ascension: the ground you crossed is a swamp.",
			o => { o.DamageMultiplier = 2.5f; o.AreaMultiplier = 2.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"Pools are laid twice as close, the trail holds {morePools} more of them, and each lingers {linger:0.#}s longer.",
			"Ascension / Speed", "Ascension: a long, deep wake behind you.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = morePools; o.Effect = SpellEffect.ZoneDuration; o.EffectValue = linger; }));
	}

	// --- Element paths -----------------------------------------------------------------------------
	//
	// "Other spells should have paths that change their elemental tags when leveling up, such as a
	// water element spell becoming water + poison later." The rule that answers it: a pure spell's
	// level 4 offers one branch that DEEPENS its own element (a second tag of it, keeping attunement
	// and pushing its tier) and one that ADDS a different element and grows the spell toward it - so
	// the milestone is a real decision, never a free tag. A branch costs attunement (the spell names
	// two elements from then on), so it has to pay something a deepening does not: its element's
	// status on every hit (ElementPaths.RiderFor), and a look that says which element it took.
	//
	// Hybrids are not given one: a spell may never name more than two elements, so a hybrid's path
	// is deepening its other half, which every hybrid already offers. Branching stays a level 4
	// decision; level 8 only deepens, or a pure spell could reach three elements.
	//
	// These are the seven pure spells whose other branches are the default archetype's (each block
	// above takes its path from here, through AddElementPath). Each element was chosen for the spell's
	// own fiction first and for the thin elements second (.ai/passives-and-items.md 3a).

	private static SpellEvolutionOption BuildDefaultElementPath(SpellData spell)
	{
		return spell.Id.Trim().ToLowerInvariant() switch
		{
			// Tituba's tide, gone foul. The user's own example.
			"riptide" => ElementPathOption(spell, "brackish_tide", "Brackish Tide", Element.Poison, 3f, 3f,
				"The surge runs brackish",
				"Every enemy the wave drags is left rotting. Pairs with Contagion and the Poison tier."),
			// Black fire. Fire had never been paired with Darkness anywhere in the roster.
			"cinderbreath" => ElementPathOption(spell, "hellbreath", "Hellbreath", Element.Darkness, 0.10f, 2f,
				"The fire burns black",
				"Everything the jet licks takes more from the rest of your spells."),
			// Thorns grown hard as nails; Metal's share-of-health burn suits a patch elites walk through.
			"bramble_seed" => ElementPathOption(spell, "ironbriar", "Ironbriar", Element.Metal, 0.05f, 0f,
				"The thorns grow iron",
				"A share of every elite's health stays in the patch."),
			// Contagion is kept by the lantern-tender in the Blighted Swamp: the plague goes by water.
			"contagion" => ElementPathOption(spell, "marsh_fever", "Marsh Fever", Element.Water, 0.75f, 3f,
				"The plague travels by water",
				"A pack that cannot leave the carrier is a pack that catches it."),
			// A black well with a ring of fire round it is an eclipse. Light is a thin element.
			"hollow_star" => ElementPathOption(spell, "eclipse", "Eclipse", Element.Light, 0.30f, 3f,
				"The well burns with a corona",
				"The knot it gathers hits you 30% softer while it lasts."),
			// A line of iron stakes in a storm.
			"iron_palisade" => ElementPathOption(spell, "lightning_rod", "Lightning Rod", Element.Lightning, 0f, 0.25f,
				"The stakes draw lightning",
				"Whatever the wall cuts stops where it stands."),
			// A cold light. Ice has only three spell carriers.
			"kindled_ward" => ElementPathOption(spell, "rimelight", "Rimelight", Element.Ice, 0.70f, 1.5f,
				"The ward burns cold",
				"A drifting light that holds what it burns."),
			_ => null,
		};
	}

	// One element path: +20% damage (what Reactive Catalyst paid), +1 tag of the new element, its
	// rider, and its tint. The card text is built from the same numbers the hit applies.
	private static SpellEvolutionOption ElementPathOption(SpellData spell, string suffix, string name,
		Element element, float magnitude, float seconds, string lead, string synergy)
	{
		ElementRider rider = ElementPaths.RiderFor(element);
		string riderText = rider == ElementRider.None ? string.Empty : $", and {ElementPaths.Describe(rider, magnitude, seconds)}";
		string own = ElementNames(spell).Count > 0 ? ElementNames(spell)[0] : "Spell";
		return new SpellEvolutionOption
		{
			Id = $"{spell.Id}_{suffix}",
			DisplayName = name,
			Description = $"{lead}: +20% damage, +1 {element} tag{riderText}.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.2f,
			BonusElementWeights = new() { { element.ToString(), 1 } },
			Rider = rider,
			RiderMagnitude = magnitude,
			RiderSeconds = seconds,
			VisualTag = name.Replace(" ", string.Empty),
			ModulateColor = ElementColors.GetEvolutionTint(element),
			SynergyTag = $"{own} / {element}",
			SynergyDescription = $"Branches into {element}. {synergy}"
		};
	}

	// Starfall (scripts/Starfall.cs, .ai/spell-roster.md "Starfall"): a rain aimed at nothing. Its own
	// block rather than the default archetype, whose cards promise pierce and projectile speed that a
	// rain cannot read. Pure Light, so level 4 deepens Light (Clear Night) or branches into Ice
	// (Frostfall, the element path), with an untagged third (Downpour); level 8 deepens Light
	// (Starburst - the stars explode) or takes no tag (Constellation - the stars link). Every number
	// on these cards is one Starfall reads: the count through GetProjectileCountAtLevel, the rate
	// through the cooldown, the landing through GetAreaMultiplierAtLevel, the damage through
	// GetDamageAtLevel, and the two behaviours through HasEvolution. No ScaleMultiplier or
	// SpeedMultiplier: the stars are drawn out of the radius they hit, and nothing travels.
	private static void SetupStarfallEvolutions(SpellData spell)
	{
		// Every percentage on these cards is computed from the number the option carries, so a tune can
		// never leave a card promising what the spell no longer does.
		const int clearNightStars = 2;
		const float clearNightDamage = 1.15f;
		const float downpourRate = 1.4f;
		const float downpourArea = 0.85f;
		const float burstDamage = 2.1f;
		const float burstArea = 2.2f;
		const float burstCooldown = 2.0f;
		const int constellationStars = 1;

		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Starfall.ClearNightId,
			DisplayName = "Clear Night",
			Description = $"The sky clears and more stars come through: +{clearNightStars} stars every cloudburst, "
				+ $"+{(clearNightDamage - 1f) * 100f:0}% damage, +1 Light tag.",
			MilestoneLevel = 4,
			ProjectileCountBonus = clearNightStars,
			DamageMultiplier = clearNightDamage,
			BonusElementWeights = new() { { "Light", 1 } },
			VisualTag = "ClearNight",
			SynergyTag = "Light / Density",
			SynergyDescription = "Deepens Light. A thicker rain: more of the crowd under a star at once."
		});

		// The element path. Ice for the fiction (the cold light of stars) and because Ice has only three
		// spell carriers. Starfall repaints every star in Ice's ramp; the slow is the rider.
		spell.Level4Options.Add(ElementPathOption(spell, "frostfall", "Frostfall", Element.Ice, 0.70f, 1.5f,
			"The stars fall cold",
			"Everything standing in the rain is dragged to a crawl."));

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Starfall.DownpourId,
			DisplayName = "Downpour",
			Description = $"The drizzle turns to a downpour: stars fall {(downpourRate - 1f) * 100f:0}% more often, "
				+ $"but each lands {(1f - downpourArea) * 100f:0}% smaller.",
			MilestoneLevel = 4,
			CooldownMultiplier = 1f / downpourRate,
			AreaMultiplier = downpourArea,
			VisualTag = "Downpour",
			SynergyTag = "Rate / Coverage",
			SynergyDescription = "No new element. More, smaller stars: hits over size."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Starfall.StarburstId,
			DisplayName = "Starburst",
			Description = $"Each star lands, swells for {Starfall.FuseSeconds:0.#}s and explodes {(burstArea - 1f) * 100f:0}% "
				+ $"wider for +{(burstDamage - 1f) * 100f:0}% damage, throwing {Starfall.SparksPerBurst} sparks that land "
				+ $"past its rim as small stars ({Starfall.SparkDamageFraction * 100f:0}% damage). Stars fall "
				+ $"{(burstCooldown == 2f ? "half as often" : $"{burstCooldown:0.#}x less often")}. +1 Light tag.",
			MilestoneLevel = 8,
			DamageMultiplier = burstDamage,
			AreaMultiplier = burstArea,
			CooldownMultiplier = burstCooldown,
			BonusElementWeights = new() { { "Light", 1 } },
			VisualTag = "Starburst",
			SynergyTag = "Ascension / Light",
			SynergyDescription = "Ascension: deepens Light. Fewer stars, and every one of them a detonation."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Starfall.ConstellationId,
			DisplayName = "Constellation",
			Description = $"Each star stays lit where it fell for {Starfall.LingerSeconds:0.#}s, joined to the nearest lit "
				+ $"star by a line of light; every {Starfall.LinkTickSeconds:0.#}s the lines strike all they touch for "
				+ $"{Starfall.LinkDamageFraction * 100f:0}% of a star's damage. +{constellationStars} star every cloudburst.",
			MilestoneLevel = 8,
			ProjectileCountBonus = constellationStars,
			VisualTag = "Constellation",
			SynergyTag = "Ascension / Zone",
			SynergyDescription = "Ascension: the rain draws a map of itself on the ground, and the map cuts."
		});
	}

	// The Familiar (scripts/Familiar.cs, .ai/spell-roster.md "Familiar"): the wizard's hound, and what it
	// becomes - the developer's "can evolve/grow/become a new creature on mutation/ascension". Pure
	// Arcane, so level 4 deepens Arcane (Great Hound - it grows) or branches into Ice (Rimehound, the
	// element path), with an untagged third (Littermates - the pack grows instead); level 8 deepens Arcane
	// (Cerberus - three heads, three bites) or takes no tag (Griffin - it takes wing and swoops). Every
	// form is its own art (tools/art/familiar.py). Every number on these cards is one the pack reads: the
	// bite through GetDamageAtLevel, the jaws through GetAreaMultiplierAtLevel, the hounds through
	// GetProjectileCountAtLevel, the swoop rate through the cooldown, and the two new verbs by id
	// (HasEvolution) with their reach in Familiar's own constants. No ScaleMultiplier: each form is drawn
	// at its own size, and the bite's reach is drawn out of the radius it tests.
	private static void SetupFamiliarEvolutions(SpellData spell)
	{
		const float greatDamage = 1.35f;
		const float greatJaws = 1.6f;
		const int littermates = 1;
		const float litterDamage = 0.85f;
		const float cerberusDamage = 1.35f;
		const float griffinDamage = 1.2f;
		const float griffinCooldown = 1.3f;

		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.GreatHoundId,
			DisplayName = "Great Hound",
			Description = $"It grows into a great hound in the wizard's mantle: +{(greatDamage - 1f) * 100f:0}% damage, "
				+ $"jaws {(greatJaws - 1f) * 100f:0}% wider to maul what stands beside its quarry. +1 Arcane tag.",
			MilestoneLevel = 4,
			DamageMultiplier = greatDamage,
			AreaMultiplier = greatJaws,
			BonusElementWeights = new() { { "Arcane", 1 } },
			VisualTag = "GreatHound",
			SynergyTag = "Arcane / Size",
			SynergyDescription = "Deepens Arcane. One heavy hound that bites the knot."
		});

		// The element path. Ice because a hound of frost is the fiction (and the developer's own example),
		// and because Ice has only three spell carriers. The pack repaints itself in frost; the slow is the rider.
		// Its breath changes with it: frost round the jaws widens the bite, so a frost hound chills the knot
		// it bites into rather than one body - and pays back some of the attunement the branch gives up.
		const float rimeBreath = 1.4f;
		SpellEvolutionOption rime = ElementPathOption(spell, "rimehound", "Rimehound", Element.Ice, 0.70f, 1.5f,
			"The hound runs cold",
			"Whatever it bites is dragged to a crawl.");
		rime.AreaMultiplier = rimeBreath;
		rime.Description += $" Frost breath: jaws {(rimeBreath - 1f) * 100f:0}% wider.";
		spell.Level4Options.Add(rime);

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.LittermatesId,
			DisplayName = "Littermates",
			Description = $"Another hound answers the call: +{littermates} hound, each with its own quarry; "
				+ $"every hound bites {(1f - litterDamage) * 100f:0}% softer.",
			MilestoneLevel = 4,
			ProjectileCountBonus = littermates,
			DamageMultiplier = litterDamage,
			VisualTag = "Littermates",
			SynergyTag = "Pack / Count",
			SynergyDescription = "No new element. More bites, spread across the crowd."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.CerberusId,
			DisplayName = "Cerberus",
			Description = $"Three heads: every bite is {Familiar.CerberusHeads} - its quarry and the two enemies nearest it "
				+ $"(within {Familiar.CerberusReach:0} px) - for +{(cerberusDamage - 1f) * 100f:0}% damage. +1 Arcane tag.",
			MilestoneLevel = 8,
			DamageMultiplier = cerberusDamage,
			BonusElementWeights = new() { { "Arcane", 1 } },
			VisualTag = "Cerberus",
			SynergyTag = "Ascension / Arcane",
			SynergyDescription = "Ascension: deepens Arcane. Three jaws on every leap."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.GriffinId,
			DisplayName = "Griffin",
			Description = $"It takes wing: a swoop through its quarry and {Familiar.SwoopOvershoot:0} px on, striking all within "
				+ $"{Familiar.SwoopHalfWidth:0} px of its line for +{(griffinDamage - 1f) * 100f:0}% damage. Flies over water and walls; "
				+ $"swoops {(griffinCooldown - 1f) * 100f:0}% less often than bites.",
			MilestoneLevel = 8,
			DamageMultiplier = griffinDamage,
			CooldownMultiplier = griffinCooldown,
			VisualTag = "Griffin",
			SynergyTag = "Ascension / Line",
			SynergyDescription = "Ascension: no new element. Every strike is a line."
		});
	}

	// Stormsweep (scripts/Stormsweep.cs, .ai/spell-roster.md "The facing family"): an arc of storm-light
	// swept down the side the player faces. Pure Lightning, so level 4 deepens Lightning (High Voltage)
	// or branches into Metal (Iron Current, the element path), with an untagged third (Rapid Discharge);
	// level 8 deepens Lightning (Thunderclap - the double sweep) or takes no tag (Cardinal Storm - all
	// four ways). Every number on these cards is one Stormsweep reads: damage through GetDamageAtLevel,
	// the cooldown through the fire loop, the arc's length and band through GetAreaMultiplierAtLevel, its
	// tempo through GetSpeedMultiplier, and the two behaviours through HasEvolution. No ScaleMultiplier:
	// the arc is drawn out of the band it sweeps.
	private static void SetupStormsweepEvolutions(SpellData spell)
	{
		// Every percentage on these cards is computed from the number the option carries, so a tune can
		// never leave a card promising what the spell no longer does.
		const float voltageDamage = 1.3f;
		const float voltageArea = 1.2f;
		const float rapidCooldown = 0.7f;
		const float rapidTempo = 1.25f;
		const float cardinalCooldown = 1.25f;

		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Stormsweep.HighVoltageId,
			DisplayName = "High Voltage",
			Description = $"The storm runs hotter: +{(voltageDamage - 1f) * 100f:0}% damage, and every sweep reaches "
				+ $"{(voltageArea - 1f) * 100f:0}% further and wider. +1 Lightning tag.",
			MilestoneLevel = 4,
			DamageMultiplier = voltageDamage,
			AreaMultiplier = voltageArea,
			BonusElementWeights = new() { { "Lightning", 1 } },
			VisualTag = "HighVoltage",
			SynergyTag = "Lightning / Power",
			SynergyDescription = "Deepens Lightning. A heavier sweep over more of the ground beside you."
		});

		// The element path. Metal for the fiction - a storm drawn through iron, the arc carrying it - and
		// for what a sweep is for: its first touch on an elite Withers a share of its health on every
		// pass. Stormsweep repaints the arc as beads of iron on the current (IPaintsElementPath); Wither
		// is the rider.
		spell.Level4Options.Add(ElementPathOption(spell, "iron_current", "Iron Current", Element.Metal, 0.05f, 0f,
			"The storm runs through iron",
			"A share of every elite's health stays in the iron."));

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Stormsweep.RapidDischargeId,
			DisplayName = "Rapid Discharge",
			Description = $"{(1f - rapidCooldown) * 100f:0}% shorter cooldown, and every sweep crackles through "
				+ $"{(rapidTempo - 1f) * 100f:0}% faster.",
			MilestoneLevel = 4,
			CooldownMultiplier = rapidCooldown,
			SpeedMultiplier = rapidTempo,
			VisualTag = "RapidDischarge",
			SynergyTag = "Cadence",
			SynergyDescription = "No new element. Sweep after sweep, so turning to face a crowd pays off sooner."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Stormsweep.ThunderclapId,
			DisplayName = "Thunderclap",
			Description = "Every arc sweeps twice, down and back up, and each pass ends in a thunderclap at its "
				+ $"far end that hits everything within {Stormsweep.ThunderRadius:0} px for "
				+ $"{Stormsweep.ThunderDamageFraction * 100f:0}% of the sweep's damage. +1 Lightning tag.",
			MilestoneLevel = 8,
			BonusElementWeights = new() { { "Lightning", 1 } },
			VisualTag = "Thunderclap",
			SynergyTag = "Ascension / Lightning",
			SynergyDescription = "Ascension: deepens Lightning. A double sweep that ends in thunder at both ends of its pass."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Stormsweep.CardinalStormId,
			DisplayName = "Cardinal Storm",
			Description = "Every sweep goes all four ways at once - left, right, up and down - "
				+ $"but comes {(cardinalCooldown - 1f) * 100f:0}% less often.",
			MilestoneLevel = 8,
			CooldownMultiplier = cardinalCooldown,
			VisualTag = "CardinalStorm",
			SynergyTag = "Ascension / Coverage",
			SynergyDescription = "Ascension: the one form that does not care which way you face."
		});
	}

	// Sleet Runes (scripts/SleetRunes.cs, .ai/spell-roster.md "The facing family"): runes of rime cast the
	// way the player faces. Ice + Wind, a hybrid, so its element path is deepening either half: level 4
	// deepens Ice (Deep Rime) and level 8 Wind (Turning Gale), each at the hybrid's price, and neither
	// ever reaches 3. Every number on these cards is one SleetRunes reads: damage, cooldown, the count
	// through GetProjectileCountAtLevel, pierce through GetPierceAtLevel, reach through GetRangeAtLevel,
	// speed through GetSpeedMultiplier, and the two behaviours through HasEvolution. No Area and no
	// ScaleMultiplier: a rune's reach is its Range, and the sheet is drawn at one size.
	private static void SetupSleetRunesEvolutions(SpellData spell)
	{
		const float rimeDamage = 1.35f;
		const int rimePierce = 1;
		const int flurryRunes = 2;
		const float farWindSpeed = 1.3f;
		const float farWindRange = 120f;
		const int fanRunes = 3;
		const int fanPierce = 1;
		const float galeDamage = 1.35f;

		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SleetRunes.DeepRimeId,
			DisplayName = "Deep Rime",
			Description = $"The runes are cut deeper in the cold: +{(rimeDamage - 1f) * 100f:0}% damage, and each passes "
				+ $"through {rimePierce} more enemy. +1 Ice tag.",
			MilestoneLevel = 4,
			DamageMultiplier = rimeDamage,
			PierceBonus = rimePierce,
			BonusElementWeights = new() { { "Ice", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "DeepRime",
			SynergyTag = "Ice / Power",
			SynergyDescription = "Deepens Ice. Fewer runes spent on the front rank."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SleetRunes.RuneFlurryId,
			DisplayName = "Rune Flurry",
			Description = $"+{flurryRunes} runes every cast.",
			MilestoneLevel = 4,
			ProjectileCountBonus = flurryRunes,
			VisualTag = "RuneFlurry",
			SynergyTag = "Volume",
			SynergyDescription = "No new element. A longer stream: more of the line in front of you is struck."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SleetRunes.FarWindId,
			DisplayName = "Far Wind",
			Description = $"Runes fly {(farWindSpeed - 1f) * 100f:0}% faster and {farWindRange:0} px farther.",
			MilestoneLevel = 4,
			SpeedMultiplier = farWindSpeed,
			RangeBonus = farWindRange,
			VisualTag = "FarWind",
			SynergyTag = "Reach",
			SynergyDescription = "No new element. Reach what is coming before it reaches you."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SleetRunes.SigilFanId,
			DisplayName = "Sigil Fan",
			Description = $"Every cast leaves at once, fanned across {SleetRunes.FanDegrees:0} degrees: "
				+ $"+{fanRunes} runes, and each passes through {fanPierce} more enemy.",
			MilestoneLevel = 8,
			ProjectileCountBonus = fanRunes,
			PierceBonus = fanPierce,
			VisualTag = "SigilFan",
			SynergyTag = "Ascension / Spread",
			SynergyDescription = "Ascension: the stream becomes a spray - less aim, more of the field in front of you."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SleetRunes.TurningGaleId,
			DisplayName = "Turning Gale",
			Description = "At the end of its flight the wind turns every rune back to you, and it strikes again on "
				+ $"the way, through as many as it struck going out. +{(galeDamage - 1f) * 100f:0}% damage, +1 Wind tag.",
			MilestoneLevel = 8,
			DamageMultiplier = galeDamage,
			BonusElementWeights = new() { { "Wind", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "TurningGale",
			SynergyTag = "Ascension / Wind",
			SynergyDescription = "Ascension: deepens Wind. Every rune is two passes, and the second comes from behind them."
		});
	}
}
