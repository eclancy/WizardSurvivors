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
			// option used to declare was silently doing nothing.
			Effect = SpellEffect.SplitOnHit,
			EffectValue = 30f,
			SpeedMultiplier = 1.25f,
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
			SpeedMultiplier = 1.4f,
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
			SpeedMultiplier = 0.65f,
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
			Description = "Explosion releases two rapid concentric shockwaves with +60% Knockback.",
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
			Description = "Leaves a pulsating hazard zone for 2.5s dealing continuous ticks (+40% Area).",
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
			Description = "Massive screen-wide fiery blast (+150% Area, +100% Damage) incinerating enemy armor.",
			MilestoneLevel = 8,
			AreaMultiplier = 2.5f,
			DamageMultiplier = 2.0f,
			ScaleMultiplier = 2.2f,
			Effect = SpellEffect.Burn,
			EffectValue = 5f,
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
			Description = "Cosmic singularity pulls enemies inward and freezes them for 2.5 seconds.",
			MilestoneLevel = 8,
			AreaMultiplier = 1.8f,
			DamageMultiplier = 1.5f,
			ScaleMultiplier = 1.75f,
			SlowMagnitudeBonus = 1.0f,
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
			AreaMultiplier = 1.4f,
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
			AreaMultiplier = 1.3f,
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
		// Metal's Wither on every enemy the blast catches. PierceBonus and SpeedMultiplier are left
		// at their old values for whenever Fireball learns to pierce; nothing reads them today.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "fireball_comet",
			BonusElementWeights = new() { { "Metal", 1 } },
			DisplayName = "Iron Comet",
			Description = "The fireball is forged around an iron core: +1 Metal tag, and "
				+ ElementPaths.Describe(ElementRider.Wither, 0.05f, 0f) + ".",
			MilestoneLevel = 4,
			PierceBonus = 3,
			SpeedMultiplier = 1.4f,
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
			ScaleMultiplier = 1.35f,
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
			AreaMultiplier = 1.4f,
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
			SlowMagnitudeBonus = 1.0f,
			ScaleMultiplier = 1.5f,
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
			SlowMagnitudeBonus = 0.8f,
			ScaleMultiplier = 1.2f,
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
			AreaMultiplier = 1.8f,
			ScaleMultiplier = 1.5f,
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
			ScaleMultiplier = 1.4f,
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
		// the tag. The card now says that, plus Metal's Wither on everything the blades cut.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "cyclone_slash_razor_gale",
			BonusElementWeights = new() { { "Metal", 1 } },
			DisplayName = "Razor Gale",
			Description = "The blades are edged in steel: +35% damage, +1 Metal tag, and "
				+ ElementPaths.Describe(ElementRider.Wither, 0.04f, 0f) + ".",
			MilestoneLevel = 4,
			DamageMultiplier = 1.35f,
			RangeBonus = 40f,
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
			ScaleMultiplier = 2.0f,
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
			AreaMultiplier = 1.8f,
			ScaleMultiplier = 1.7f,
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
			Description = "Pulses ignite enemies with intense burning damage.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			Effect = SpellEffect.Burn,
			EffectValue = 4f,
			DamageMultiplier = 1.25f,
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
			Description = "Releases perpetual blinding solar bursts across the screen (+150% Area, +100% Damage).",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			AreaMultiplier = 2.5f,
			ScaleMultiplier = 2.2f,
			VisualTag = "Daybreak",
			ModulateColor = new Color(1.0f, 0.95f, 0.4f),
			SynergyTag = "Light / Zenith",
			SynergyDescription = "Ascension: Screen-wide holy solar apocalypse."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "solar_flare_corona_discharge",
			DisplayName = "Corona Discharge",
			Description = "Pulses shoot 8 holy sunbeams in all directions.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.4f,
			ProjectileCountBonus = 8,
			ScaleMultiplier = 1.8f,
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
			Description = "Enemies hit by the cone are frozen solid for 2.0 seconds.",
			MilestoneLevel = 4,
			Effect = SpellEffect.Freeze,
			EffectValue = 2.0f,
			SlowMagnitudeBonus = 1.0f,
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
			// pierce - so the card says what it pays: the damage and the tag.
			BonusElementWeights = new() { { "Water", 1 } },
			DisplayName = "Frost Shrapnel",
			Description = "Meltwater runs through the cone: +30% damage and +1 Water tag.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 5,
			PierceBonus = 3,
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
			// Frostweaver measured below the other wizards. Scale matches so the cone is drawn as far
			// as it hurts. The "360-degree" blast the old text described was never built - the full
			// ring is The Long Winter's, the Apotheosis, and stays its own.
			AreaMultiplier = 1.4f,
			ScaleMultiplier = 1.4f,
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
			ScaleMultiplier = 1.9f,
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
			ScaleMultiplier = 2.5f,
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
			ScaleMultiplier = 1.6f,
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
			SpeedMultiplier = 1.5f,
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

	private static void SetupDefaultArchetypeEvolutions(SpellData spell)
	{
		// Fifteen spells have no hand-written block and land here, so the element gains are derived
		// rather than authored: the spell deepens its first element at level 4 and its second at
		// level 8. A hybrid therefore reaches 2 in each and never 3 in either, which is the rule
		// that keeps depth a pure spell's speciality. A pure spell landing here deepens its one
		// element at both milestones, and pays nothing for it - or, at level 4, takes its element
		// path instead (BuildDefaultElementPath), which is the one authored part of this archetype.
		List<string> elements = ElementNames(spell);
		bool isPure = elements.Count == 1;
		string firstElement = elements.Count > 0 ? elements[0] : string.Empty;
		string secondElement = elements.Count > 1 ? elements[1] : firstElement;
		float gainCost = isPure ? 1.0f : AttunementGainCooldownCost;

		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_amplified_impact",
			DisplayName = "Amplified Impact",
			Description = firstElement.Length > 0
				? $"+35% Damage and +30% Area, and the spell drinks deeper of {firstElement}."
				: "+35% Damage and +30% Area of effect.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.35f,
			AreaMultiplier = 1.3f,
			BonusElementWeights = firstElement.Length > 0 ? new() { { firstElement, 1 } } : new(),
			CooldownMultiplier = gainCost,
			VisualTag = "AmplifiedImpact",
			ModulateColor = new Color(1.0f, 0.85f, 0.4f),
			SynergyTag = "Power / Area",
			SynergyDescription = "General power enhancement."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_hyper_acceleration",
			DisplayName = "Hyper Acceleration",
			Description = "-30% Cooldown and +25% Projectile speed.",
			MilestoneLevel = 4,
			CooldownMultiplier = 0.7f,
			SpeedMultiplier = 1.25f,
			VisualTag = "HyperAcceleration",
			ModulateColor = new Color(0.4f, 0.9f, 1.0f),
			SynergyTag = "Speed / Cadence",
			SynergyDescription = "Rapid firing cycle."
		});

		// A pure spell's third choice is its element path - the branch that ADDS an element, against
		// Amplified Impact, which deepens its own (.ai/spell-roster.md, "Element paths"). It replaced
		// Reactive Catalyst for those spells and keeps that option's +20% damage; the +2 pierce, which
		// only a projectile ever read, became the element. Hyper Acceleration keeps an untagged option
		// on the table. A hybrid keeps Reactive Catalyst: it names two elements already, and a spell
		// may never name a third (RegressionChecks.ValidateEvolutionElementCeiling).
		SpellEvolutionOption path = isPure ? BuildDefaultElementPath(spell) : null;
		if (path != null)
		{
			spell.Level4Options.Add(path);
		}
		else
		{
			spell.Level4Options.Add(new SpellEvolutionOption
			{
				Id = $"{spell.Id}_reactive_catalyst",
				DisplayName = "Reactive Catalyst",
				// It promised "+2 Pierce or Chain arcs with secondary splash damage". No spell that lands
				// here chains (only Chain Lightning reads chain arcs, and it has its own block), nothing
				// in the game splashes for this option, and until 2026-10-22 ElementalBolt never read an
				// evolution's pierce either - so for Riptide it was a hidden +20% damage and nothing else.
				// Now it says exactly what it pays: the damage always, the pierce on a spell that fires
				// a projectile (MagicMissile and ElementalBolt read GetPierceAtLevel).
				Description = "+20% Damage, and projectiles pierce 2 more enemies.",
				MilestoneLevel = 4,
				PierceBonus = 2,
				DamageMultiplier = 1.2f,
				VisualTag = "ReactiveCatalyst",
				ModulateColor = new Color(0.85f, 0.45f, 1.0f),
				SynergyTag = "Pierce / Power",
				SynergyDescription = "Multi-target efficiency."
			});
		}

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_titan_cataclysm",
			DisplayName = "Titan Cataclysm",
			Description = secondElement.Length > 0
				? $"Colossal scale ascension (+150% Damage, +100% Area), steeped in {secondElement}."
				: "Colossal scale ascension (+150% Damage, +100% Area).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.5f,
			AreaMultiplier = 2.0f,
			BonusElementWeights = secondElement.Length > 0 ? new() { { secondElement, 1 } } : new(),
			CooldownMultiplier = gainCost,
			ScaleMultiplier = 2.0f,
			VisualTag = "TitanCataclysm",
			ModulateColor = new Color(1.0f, 0.3f, 0.15f),
			SynergyTag = "Ascension / Cataclysm",
			SynergyDescription = "Ascension: Massive raw damage and area destruction."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = $"{spell.Id}_infinite_horizon",
			DisplayName = "Infinite Horizon",
			Description = "Continuous rapid cycling ascension (-50% Cooldown, +3 Projectiles, +4 Pierce).",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.5f,
			ProjectileCountBonus = 3,
			PierceBonus = 4,
			ScaleMultiplier = 1.4f,
			VisualTag = "InfiniteHorizon",
			ModulateColor = new Color(0.4f, 0.95f, 1.0f),
			SynergyTag = "Ascension / Speed",
			SynergyDescription = "Ascension: Non-stop rapid barrage."
		});
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
	// These are the seven pure spells on the default archetype. Each element was chosen for the
	// spell's own fiction first and for the thin elements second (.ai/passives-and-items.md 3a).

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
}
