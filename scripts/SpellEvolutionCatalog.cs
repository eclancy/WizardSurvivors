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

	// Magic Missile: a homing violet dart at the nearest enemy, the starter's spell. EVERY CARD ITS OWN
	// (.ai/spell-roster.md section 2h, phase 2, 2026-10-29): Piercing Stardust (+3 pierce) and Arcane Gatling
	// Barrage (+2 darts, a shorter cooldown, +5 pierce) only moved numbers, and Singularity Core pulled -
	// Hollow Star's verb. Each now changes what the dart does where it can be seen, out of what a dart that
	// never misses is: it forks, it threads the pack, it bursts, it leaves static, it swells into an orb.
	// The ids and each card's element gain are the ones they always were, so saves carry over; the
	// behaviour is switched on by id (MagicMissile.ThreadedNeedleId and the rest, StaticCharges).
	private static void SetupMagicMissileEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = MagicMissile.TwinVolleyId,
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

		// Was Piercing Stardust, +3 pierce: the dart flew on in a straight line, and in a crowd that thins as
		// it is fought a straight line mostly met air. Now it turns on the next enemy itself.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = MagicMissile.ThreadedNeedleId,
			DisplayName = "Threaded Needle",
			Description = $"The missile threads the pack: after each hit it turns on the nearest enemy it has not struck and flies on, {MagicMissile.ThreadStrikes} more times, stitching a thread of stardust through them.",
			MilestoneLevel = 4,
			VisualTag = "ThreadedNeedle",
			ModulateColor = new Color(0.86f, 0.94f, 1.0f),
			SynergyTag = "Arcane / Multi-target",
			SynergyDescription = "One dart, four enemies: it pays most where the pack is loose and a straight line would miss."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = MagicMissile.UnstableId,
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Unstable Detonation",
			// Kept (2026-10-29): it always changed the hit into a splash, but drew nothing; the burst now
			// throws arcane sparks out to the reach it strikes.
			Description = "Missiles detonate where they hit: an arcane burst throws sparks out round the impact, hitting everything in it for 60% damage.",
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
		// Was Arcane Gatling Barrage: +2 darts, -44% cooldown, +5 pierce, +25% - more of the same dart. The
		// cadence rides along; what is new is what the darts leave on what they strike.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = MagicMissile.OverchargeId,
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Overcharge",
			Description = $"Every missile leaves a spark of static crackling round what it hits, and an enemy carrying {MagicMissile.OverchargeSparks} overloads: lightning bursts out of it, striking it and everything round it for {MagicMissile.OverchargeShare:0}x a missile. Missiles loosed 40% faster.",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.6f,
			VisualTag = "Overcharge",
			ModulateColor = new Color(0.86f, 0.94f, 1.0f),
			SynergyTag = "Lightning / Burst",
			SynergyDescription = "Ascension: the darts keep finding the nearest, so an elite or a boss overloads again and again - and a crowd goes off in bursts."
		});

		// Was Singularity Core, orbs that pulled the crowd in: the pull is Hollow Star's (2e). The orb stays,
		// as something that grinds through the pack rather than drags it.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = MagicMissile.ArcaneOrbId,
			DisplayName = "Arcane Orb",
			Description = $"Every missile is loosed as a great slow orb of arcane light that rolls straight through the pack, striking everything under it {1f / MagicMissile.OrbTickSeconds:0} times a second, and bursts where its flight ends for {MagicMissile.OrbBurstShare * 100f:0}% of a missile. +{(MagicMissile.OrbDamage - 1f) * 100f:0}% damage, {(MagicMissile.OrbCooldown - 1f) * 100f:0}% longer cooldown.",
			MilestoneLevel = 8,
			CooldownMultiplier = MagicMissile.OrbCooldown,
			DamageMultiplier = MagicMissile.OrbDamage,
			VisualTag = "ArcaneOrb",
			ModulateColor = new Color(0.94f, 0.88f, 1.0f),
			SynergyTag = "Arcane / Zone",
			SynergyDescription = "Ascension: slow and heavy - each orb holds a lane of the pack under it for two seconds."
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
			// x2.2, not x1.5: measured x2.58 of the spell at level 7 after (2026-10-26, --enemy-hp=10).
			Description = "Cosmic singularity pulls enemies inward and freezes them for 2.5 seconds (+80% Area, +120% Damage).",
			MilestoneLevel = 8,
			AreaMultiplier = 1.8f,
			DamageMultiplier = 2.2f,
			Effect = SpellEffect.Freeze,
			EffectValue = 2.5f,
			VisualTag = "EventHorizon",
			ModulateColor = new Color(0.35f, 0.65f, 1.0f),
			SynergyTag = "Arcane / Gravity",
			SynergyDescription = "Ascension: Locks down entire screen with freezing gravitational pull."
		});
	}

	// SPIRITUAL WEAPON: one block per FORM (SpellData.Variants; .ai/decisions/2026-10-28-spell-variants.md).
	// The spell is taken as an axe, a hammer or a sword, and each form's milestones are its own: their ids
	// start with the form's id, so every one has its own icon, its own seen-ascension record and its own
	// row of readers (EvolutionStatReaders, by form). A template with no form chosen - the catalog's own,
	// which no run plays - carries its default form's block, so CardLevelCap and anything else that reads
	// a template sees a real set.
	//
	// The shape of every block is the hybrid's (Arcane + Light): one level 4 that deepens Light, one level 8
	// that deepens Arcane, both bought with the 12% cooldown every hybrid pays for a tag; and per form one
	// Ascension that TRANSFORMS the weapon - a colossal new drawing on the boss cell (tools/art/spirit_weapons.py)
	// at ScaleMultiplier 2, which SpiritualWeapon reads as the weapon's size and the reach of its blow alike.
	// Every behaviour named by id lives in SpiritualWeapon; every number on a card is one it reads.
	private static void SetupSpiritualWeaponEvolutions(SpellData spell)
	{
		string form = !string.IsNullOrEmpty(spell.SelectedVariantId)
			? spell.SelectedVariantId
			: spell.DefaultVariant?.Id ?? SpiritualWeapon.AxeId;
		switch (form)
		{
			case SpiritualWeapon.HammerId:
				SetupSpiritHammerEvolutions(spell);
				break;
			case SpiritualWeapon.SwordId:
				SetupSpiritSwordEvolutions(spell);
				break;
			default:
				SetupSpiritAxeEvolutions(spell);
				break;
		}
	}

	private static readonly Color SpiritArcaneTint = new Color(0.86f, 0.78f, 1.0f);
	private static readonly Color SpiritLightTint = new Color(1.0f, 0.95f, 0.76f);

	// THE AXE: heavy and slow; a blow cleaves everything within reach of its quarry and throws it back.
	private static void SetupSpiritAxeEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.BroadaxeId,
			DisplayName = "Broadaxe",
			Description = "A broader blade: the cleave reaches 25% wider and every blow hits 10% harder.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.25f,
			DamageMultiplier = 1.1f,
			VisualTag = "SpiritBroadaxe",
			SynergyTag = "Arcane / Wider cleave",
			SynergyDescription = "More of the pack inside every blow."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.HeadsmanId,
			DisplayName = "Headsman",
			// SpiritualWeapon.Acquire prefers the wounded; ResolveBlow doubles a blow on one (HeadsmanThreshold).
			Description = "The axe goes for the wounded: a blow on an enemy under half its health deals double.",
			MilestoneLevel = 4,
			VisualTag = "SpiritHeadsman",
			SynergyTag = "Arcane / Execute",
			SynergyDescription = "Finishes what the rest of the build starts."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.DawnsplitterId,
			BonusElementWeights = new() { { "Light", 1 } },
			DisplayName = "Dawnsplitter",
			Description = "The axe burns with dawn-light: its " + ElementPaths.Describe(ElementRider.Weaken, 0.3f, 3f) + ".",
			CooldownMultiplier = AttunementGainCooldownCost,
			MilestoneLevel = 4,
			Rider = ElementRider.Weaken,
			RiderMagnitude = 0.3f,
			RiderSeconds = 3f,
			VisualTag = "SpiritDawnsplitter",
			ModulateColor = SpiritLightTint,
			SynergyTag = "Light / Weaken",
			SynergyDescription = "What it cleaves hits you softer."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.GreataxeId,
			DisplayName = "Executioner's Greataxe",
			// The colossal form: its own drawing on the boss cell, twice the axe's size (Scale), so its
			// cleave is twice as wide; the knockback is the axe's own doubled (KnockbackBonus, read through
			// SpellData.GetKnockbackSpeedAtLevel on top of SpiritualWeapon.AxeKnockback). No damage bonus:
			// four times the ground under every blow is the card, and with +40% on top it measured 613 a
			// second at level 8 against peers of 331-465 (.ai/spell-roster.md 2f).
			Description = "The axe becomes a colossal double-bitted greataxe, twice the size: it cleaves twice as wide and throws what it hits twice as far.",
			MilestoneLevel = 8,
			ScaleMultiplier = 2.0f,
			KnockbackBonus = SpiritualWeapon.AxeKnockback / SpellData.KnockbackSpeedPerPoint,
			VisualTag = "SpiritGreataxe",
			SynergyTag = "Light / Colossal",
			SynergyDescription = "Ascension: one blow clears the front rank."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.WhirlingAxesId,
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Whirling Axes",
			Description = "Two more axes, spinning end over end between blows: three heavy blows where there was one.",
			CooldownMultiplier = AttunementGainCooldownCost,
			MilestoneLevel = 8,
			ProjectileCountBonus = 2,
			VisualTag = "SpiritWhirlingAxes",
			ModulateColor = SpiritArcaneTint,
			SynergyTag = "Arcane / More axes",
			SynergyDescription = "Ascension: three heavy blows round you at once."
		});
	}

	// THE HAMMER: an area slam that shakes the ground and slows; it goes for the densest knot in reach.
	private static void SetupSpiritHammerEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.AftershockId,
			DisplayName = "Aftershock",
			// SpiritualWeapon.QueueAftershock: the second wave is AftershockReach x the slam and
			// AftershockShare of its damage, AftershockDelay later.
			Description = "The ground answers every slam: 0.4 s later a second shockwave runs out 40% wider for 60% damage, and slows again.",
			MilestoneLevel = 4,
			VisualTag = "SpiritAftershock",
			SynergyTag = "Arcane / Echo",
			SynergyDescription = "Every slam lands twice."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.EarthshakerId,
			DisplayName = "Earthshaker",
			Description = "A heavier head: every slam reaches 35% wider and slows 15% harder.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.35f,
			SlowMagnitudeBonus = 0.15f,
			VisualTag = "SpiritEarthshaker",
			SynergyTag = "Arcane / Control",
			SynergyDescription = "The knot it slows stays a knot."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.HallowedGroundId,
			BonusElementWeights = new() { { "Light", 1 } },
			DisplayName = "Hallowed Ground",
			// The slam itself Weakens through the rider (Player.DealDamageToEnemy); the hallowed ground is
			// SpiritualWeapon's, as wide as the slam, and lasts the ZoneDuration below.
			Description = "Every slam leaves the ground hallowed for 2.5 s: whatever stands in it is Weakened (-30% damage dealt) and slowed.",
			CooldownMultiplier = AttunementGainCooldownCost,
			MilestoneLevel = 4,
			Rider = ElementRider.Weaken,
			RiderMagnitude = 0.3f,
			RiderSeconds = 3f,
			Effect = SpellEffect.ZoneDuration,
			EffectValue = 2.5f,
			VisualTag = "SpiritHallowedGround",
			ModulateColor = SpiritLightTint,
			SynergyTag = "Light / Zone",
			SynergyDescription = "Holy ground that weakens what walks in."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.TitansMaulId,
			DisplayName = "Titan's Maul",
			// The colossal form, twice the hammer's size: the slam is twice as wide by its Scale. The stop
			// is a freeze (SpellData.GetFreezeSecondsAtLevel); a boss takes a hard slow instead (Enemy.ApplySlow).
			Description = "The hammer becomes a colossal maul, twice the size: its slam reaches twice as wide, hits 20% harder and stops what it hits dead for 0.5 s.",
			MilestoneLevel = 8,
			ScaleMultiplier = 2.0f,
			DamageMultiplier = 1.2f,
			Effect = SpellEffect.Freeze,
			EffectValue = 0.5f,
			VisualTag = "SpiritTitansMaul",
			SynergyTag = "Light / Colossal",
			SynergyDescription = "Ascension: the ground stops them where they stand."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.HammerfallId,
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Hammerfall",
			Description = "Two more hammers, falling in turn so the ground never stops shaking. Every slam hits 15% harder.",
			CooldownMultiplier = AttunementGainCooldownCost,
			MilestoneLevel = 8,
			ProjectileCountBonus = 2,
			DamageMultiplier = 1.15f,
			VisualTag = "SpiritHammerfall",
			ModulateColor = SpiritArcaneTint,
			SynergyTag = "Arcane / More hammers",
			SynergyDescription = "Ascension: a slam somewhere every moment."
		});
	}

	// THE SWORD: the fastest swings and the most damage to one enemy; it parries bolts along its blade.
	private static void SetupSpiritSwordEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.TwinBladesId,
			DisplayName = "Twin Blades",
			Description = "A second sword joins the first and hunts its own quarry. Every sword strikes 10% softer.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 1,
			DamageMultiplier = 0.9f,
			VisualTag = "SpiritTwinBlades",
			SynergyTag = "Arcane / More swords",
			SynergyDescription = "Two duels at once."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.RiposteId,
			DisplayName = "Riposte",
			// SpiritualWeapon.ParryBolts: RiposteGuard x the guard, and a parry hands the sword a swing.
			Description = "Each sword's guard reaches 60% further, and every bolt it parries hands it a strike at once. Strikes hit 30% harder.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.3f,
			VisualTag = "SpiritRiposte",
			SynergyTag = "Arcane / Parry",
			SynergyDescription = "The archers feed it."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.SoulSiphonId,
			BonusElementWeights = new() { { "Light", 1 } },
			DisplayName = "Soul Siphon",
			// SpiritualWeapon.Siphon, capped per second (SiphonPerSecond) the way Soul Drain is per cast.
			Description = "Every strike heals you 1 HP, at most 3 HP a second.",
			CooldownMultiplier = AttunementGainCooldownCost,
			MilestoneLevel = 4,
			Effect = SpellEffect.Lifesteal,
			EffectValue = 1f,
			VisualTag = "SpiritSoulSiphon",
			ModulateColor = SpiritLightTint,
			SynergyTag = "Light / Healing",
			SynergyDescription = "Sustain from every cut."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.GreatswordId,
			DisplayName = "Celestial Greatsword",
			// The colossal form, twice the sword's size: the stroke strikes along the blade
			// (SpiritualWeapon.StrikeAlongBlade), as long as the blade is drawn.
			Description = "The sword becomes a colossal greatsword of light, twice the size. Its stroke cuts every enemy along the blade as it falls, not only its quarry, and hits 30% harder.",
			MilestoneLevel = 8,
			ScaleMultiplier = 2.0f,
			DamageMultiplier = 1.3f,
			VisualTag = "SpiritGreatsword",
			SynergyTag = "Light / Colossal",
			SynergyDescription = "Ascension: a duel blade that cuts a line."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = SpiritualWeapon.ThousandCutsId,
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Thousand Cuts",
			// SpiritualWeapon: FlurryCuts cuts of FlurryShare each, FlurryGap apart.
			Description = "Every swing becomes a flurry of three cuts, each at 85%. A cut whose quarry has fallen goes to the nearest enemy.",
			CooldownMultiplier = AttunementGainCooldownCost,
			MilestoneLevel = 8,
			VisualTag = "SpiritThousandCuts",
			ModulateColor = SpiritArcaneTint,
			SynergyTag = "Arcane / Flurry",
			SynergyDescription = "Ascension: the boss-killer."
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
			// Fireball.ScatterClusterBombs: each projectile past the first is a bomblet (2026-10-26). 3,
			// not the 2 it set, so the card's three bomblets are three.
			Description = "Each blast throws out 3 bomblets that burst just past its edge for half damage.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 3,
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
			// It flies at 0.7 and bursts at x2 since 2026-10-26; a sun that scorched as it drifted was
			// never built, so the card no longer says it does.
			Description = "A slow, swollen sun that bursts for +150% Damage and +100% Area.",
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
		// Frost Shard fires nothing: every death near the player shatters (Player.OnEnemyDiedAt), and
		// what a shatter has is a burst radius, a slow, and the corpse it comes out of. These five cards
		// were written for a bolt - pierce, projectile speed, cooldown - and not one of their numbers
		// reached the shatter until 2026-10-26. Each now changes one of the three, and says which.
		// Ice Statues has no cooldown price for its tag: the shatter has no cooldown to pay it with.
		//
		// EVERY CARD ITS OWN (.ai/spell-roster.md section 2h, phase 2, 2026-10-29): three of the five were
		// still numbers on the burst - a harder slow, a bigger burst, more shards. Each now changes what a
		// death does where you can see it: the dead stand as ice and break again, shards fly from the
		// corpse, the crowd freezes, the kills roll on through the pack, the corpses melt and refreeze. The
		// ids and the element each gains are the ones they always were; the behaviour is FrostShatter's.
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frost_shard_glacial_spike",
			BonusElementWeights = new() { { "Ice", 1 } },
			DisplayName = "Ice Statues",
			// Was Glacial Spike - a 30% harder slow, and the name of another spell. Now the dead themselves
			// show the cold (FrostShatter, on Player.EnemyKilled: a body a shatter killed is not frozen).
			Description = $"The dead freeze where they fall: each stands a moment as a statue of ice, then breaks, shattering a second time for {FrostShatter.StatueShare * 100f:0}%. +1 Ice tag.",
			MilestoneLevel = 4,
			VisualTag = "IceStatues",
			ModulateColor = new Color(0.9f, 0.97f, 1.0f),
			SynergyTag = "Ice / Burst",
			SynergyDescription = "Every kill hits twice - once as it falls, once as its statue breaks a second later."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = FrostShatter.IceFlurryId,
			DisplayName = "Ice Flurry",
			// Kept (2026-10-29): it always threw something - but struck its enemies the instant the shatter
			// went off, drawn as a puff where they stood. The shards now fly there (FrostShatter.Fling): each
			// projectile past the first is one.
			Description = "Every shatter flings 2 shards of ice out at the enemies just beyond it; each flies to its enemy and strikes and chills it.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 2,
			VisualTag = "IceFlurry",
			ModulateColor = new Color(0.7f, 1.0f, 1.0f),
			SynergyTag = "Ice / Reach",
			SynergyDescription = "Carries the cold past the edge of the burst."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "frost_shard_frost_nova",
			DisplayName = "Frost Nova Burst",
			// 1.0, not the 1.5 it promised: it is every death near the player, and 1.5 s held the front of
			// a crowd still for good once kills came faster than one a second.
			Description = "The shatter freezes what it catches solid for 1.0s instead of slowing it.",
			MilestoneLevel = 4,
			Effect = SpellEffect.Freeze,
			EffectValue = 1.0f,
			VisualTag = "FrostNova",
			ModulateColor = new Color(0.35f, 0.75f, 1.0f),
			SynergyTag = "Ice / Freeze",
			SynergyDescription = "Every kill in a crowd stops the crowd round it."
		});

		// Level 8 (2 choices)
		// Was Absolute Zero, +100% damage and +30% area: the same burst, bigger. Now the burst carries on -
		// the one card that lets a shatter's kills shatter, to a fixed depth (FrostShatter.KilledInShatter).
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = FrostShatter.AvalancheId,
			DisplayName = "Avalanche",
			Description = $"What a shatter kills shatters in its turn a beat later, up to {FrostShatter.AvalancheDepth} deep, so one death in a crowd rolls out through the pack in waves of ice. +60% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.6f,
			VisualTag = "Avalanche",
			ModulateColor = new Color(0.9f, 0.97f, 1.0f),
			SynergyTag = "Ice / Chain reaction",
			SynergyDescription = "Ascension: the horde kills itself - the denser the pack, the further each death rolls."
		});

		// Was Blizzard Engine, three more flung shards and +150%: Ice Flurry again, bigger. Now the spell's two
		// halves, Water and Ice, meet on the ground: the corpse melts, the next death freezes it.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = FrostShatter.MeltwaterId,
			BonusElementWeights = new() { { "Water", 1 } },
			DisplayName = "Meltwater",
			Description = $"Every corpse melts into a pool of meltwater for {FrostShatter.MeltSeconds:0}s, and the next shatter to reach a pool freezes it: it bursts into spikes of ice under whatever stands in it, for {FrostShatter.MeltShare * 100f:0}% of a shatter. +100% damage, and +1 Water tag.",
			MilestoneLevel = 8,
			DamageMultiplier = 2f,
			VisualTag = "Meltwater",
			ModulateColor = new Color(0.86f, 0.95f, 1.0f),
			SynergyTag = "Ice / Water",
			SynergyDescription = "Ascension: where the fighting has been, the ground is waiting for the next death to freeze it."
		});
	}

	// Chain Lightning is the DAMAGE half of the chain pair (see .ai/spell-roster.md). Its base form
	// hops once, close, and loses almost nothing doing it. What these options sell is the thing its
	// base form deliberately does not do: splitting. Both splitting branches cost damage, so the
	// choice is a real fork rather than a strictly better version of the spell.
	//
	// Level 4 is also its element choice: Storm Reach deepens Lightning (and is the Stormcaller's
	// Apotheosis path), Storm Surge branches into Water, Return Stroke stays as it is.
	//
	// EVERY CARD ITS OWN (.ai/spell-roster.md section 2h, phase 2, 2026-10-29): the chain leaps long, it
	// comes back, it runs on through its kills, it holds what it strikes. The ids, the element each gains
	// and the Stormcaller's path (Storm Reach, then Cascade) are the ones they always were; the behaviour
	// is ChainLightning's, switched on by these ids (StormReachId and the rest), and Electrocutions'.
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

		// Storm Reach (the Stormcaller's level 4, so its id, its Lightning and its numbers stay): it was
		// three more hops and a wider jump - the same short hops, more of them. Now the hops are long.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = ChainLightning.StormReachId,
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Storm Reach",
			Description = "The chain leaps long: every hop jumps to the farthest enemy it can reach instead of the nearest, so the arc sprawls across the whole pack. 3 more hops and a 35% longer leap, for 15% less a hop.",
			MilestoneLevel = 4,
			ChainArcBonus = 3,
			DamageMultiplier = 0.85f,
			AreaMultiplier = 1.35f,
			VisualTag = "StormReach",
			ModulateColor = new Color(0.86f, 0.94f, 1.0f),
			SynergyTag = "Lightning / Reach",
			SynergyDescription = "Wider rather than heavier - one arc that crosses the crowd from side to side."
		});

		// Was Dense Current, +40% and crits: the same bolt, harder. Now the bolt comes back.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = ChainLightning.ReturnStrokeId,
			DisplayName = "Return Stroke",
			Description = $"Where the chain ends, the stroke runs back along it to your hand, striking every enemy on the way again for {ChainLightning.ReturnShare * 100f:0}%. +30% damage.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.3f,
			VisualTag = "ReturnStroke",
			ModulateColor = new Color(1.0f, 0.98f, 0.86f),
			SynergyTag = "Lightning / Single target",
			SynergyDescription = "Every enemy in the chain is struck twice, the nearest included: the option that keeps the spell short and heavy."
		});

		// Level 8 (2 choices)
		// Cascade (the Stormcaller's Ascension: id, Lightning and reach kept). It split every hop three ways
		// again - a net of short arcs. Now the chain runs on through what it kills.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = ChainLightning.CascadeId,
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Cascade",
			Description = $"The chain comes apart on what it kills: every enemy a hop kills flashes and throws {ChainLightning.CascadeForks} more arcs out of its body, up to {ChainLightning.CascadeMaxForks} a cast, so a pack it can kill is torn through by a branching storm. Hops leap 30% further and hit 50% harder.",
			MilestoneLevel = 8,
			ChainArcBonus = 1,
			DamageMultiplier = 1.5f,
			AreaMultiplier = 1.3f,
			VisualTag = "Cascade",
			ModulateColor = new Color(0.86f, 0.94f, 1.0f),
			SynergyTag = "Lightning / Saturation",
			SynergyDescription = "Ascension: in a crowd it can kill, one cast runs on through the pack until it stops killing."
		});

		// Was Thunderhead (x2 and a hop - a name Gale Blade's stroke from the sky has since taken). Still the
		// anti-elite answer, now one you can see: the bolt holds what it strikes.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = ChainLightning.ElectrocutionId,
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Electrocution",
			Description = $"The first enemy the bolt strikes is held in the current for {ChainLightning.ElectrocuteSeconds:0.#}s, convulsing as arcs crawl over it, struck again every {ChainLightning.ElectrocuteTickSeconds:0.#}s for {ChainLightning.ElectrocuteShare * 100f:0}% of the bolt; if it dies held, the current leaps to the nearest enemy and holds that. +50% damage and one more hop.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.5f,
			ChainArcBonus = 1,
			VisualTag = "Electrocution",
			ModulateColor = new Color(1.0f, 0.98f, 0.86f),
			SynergyTag = "Lightning / Single target",
			SynergyDescription = "Ascension: the anti-elite answer - whatever walks at you first is held and burned while the chain goes on."
		});
	}

	// Shadow Bolt is the DEBUFF half. It barely kills anything on its own and is not meant to - its
	// job is to make the other five spells hit harder, so every option here adds or deepens a
	// debuff rather than adding damage. The one exception carries damage as its cost of admission.
	//
	// EVERY CARD ITS OWN (.ai/spell-roster.md section 2e, built 2026-10-29). The cards were hand-written
	// and almost all numbers; each now changes what the curse does where you can see it, from what the
	// spell already is - shadow lightning that arcs far, hits softly and leaves a trail - and its numbers
	// ride along: a sigil that the next hit breaks, paths of rot along the arcs, the fork, a pall of night
	// where the curse ends, three curses at once. The ids stay, so every save's choices and the Ascension
	// browser carry over. The behaviour and its numbers are ChainLightning's (BlackSigilId and the rest,
	// switched on by these ids alone, so Chain Lightning is untouched) and ShadowCurse's.
	private static void SetupShadowBoltEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_withering_mark",
			BonusElementWeights = new() { { "Darkness", 1 } },
			DisplayName = "Black Sigil",
			Description = $"Everything the curse touches wears a black sigil at its feet, and the next hit it takes from any of your other spells breaks it in a burst of shadow that lays the curse on everything near and strikes for {ChainLightning.SigilBurstDamageShare:0}x the bolt. +12% damage taken.",
			MilestoneLevel = 4,
			Effect = SpellEffect.Vulnerability,
			EffectValue = 0.12f,
			VisualTag = "BlackSigil",
			ModulateColor = new Color(0.72f, 0.36f, 0.92f),
			SynergyTag = "Darkness / Vulnerability",
			SynergyDescription = "Every other spell you own sets the sigils off: the busier the loadout, the further the curse spreads."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_creeping_rot",
			BonusElementWeights = new() { { "Poison", 1 } },
			DisplayName = "Creeping Rot",
			Description = $"Every arc sinks into the ground as a path of rot that lies {ChainLightning.RotPathSeconds:0.#}s, poisoning, slowing and exposing whatever crosses it. Deeper poison and a heavier drag.",
			MilestoneLevel = 4,
			PoisonTickBonus = 4,
			// The drag as Effect SlowPower, which ChainLightning reads (0.72 to 0.57 of their speed).
			// It was a SlowMagnitudeBonus, which nothing on this spell reads, until 2026-10-26.
			Effect = SpellEffect.SlowPower,
			EffectValue = 0.15f,
			VisualTag = "CreepingRot",
			ModulateColor = new Color(0.45f, 0.72f, 0.30f),
			SynergyTag = "Poison / Control",
			SynergyDescription = "The survival option - the ground between you and the pack is cursed, and a crowd that cannot close cannot hit you."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_spreading_dark",
			DisplayName = "Spreading Dark",
			Description = "The curse forks at every jump - two arcs leave each enemy it reaches - and runs two hops further. No extra damage.",
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
			// +45% damage of its own as well (2026-10-26): x2.76 of the spell at level 7 with it, at --enemy-hp=10.
			Description = $"Where the curse ends, night falls: a pall of darkness hangs over the last enemy it reached for {ChainLightning.PallSeconds:0}s, cursing and striking everything under it every half second. +20% damage taken, two hops further, and the curse bites 45% harder.",
			DamageMultiplier = 1.45f,
			MilestoneLevel = 8,
			Effect = SpellEffect.Vulnerability,
			EffectValue = 0.20f,
			ChainArcBonus = 2,
			VisualTag = "Anathema",
			ModulateColor = new Color(0.86f, 0.30f, 1.0f),
			SynergyTag = "Darkness / Vulnerability",
			SynergyDescription = "Ascension: every chain ends in a patch of night your other spells hit harder in."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "shadow_bolt_plaguebearer",
			// No BonusElementWeights. Element growth on a hybrid happens at level 4; granting one
			// here as well would let a player who took the matching level 4 option reach three of
			// a single element, which only a pure spell may do.
			DisplayName = "Threefold Curse",
			Description = $"{ChainLightning.ThreefoldCurses} curses leave the staff at once, each at its own enemy and each chaining on its own, and the rot they carry does real damage. +20% damage.",
			MilestoneLevel = 8,
			PoisonTickBonus = 8,
			DamageMultiplier = 1.2f,
			VisualTag = "ThreefoldCurse",
			ModulateColor = new Color(0.52f, 0.84f, 0.34f),
			SynergyTag = "Poison / Damage",
			SynergyDescription = "Ascension: the curse fans out across the front of the pack, and kills through poison rather than impact."
		});
	}

	// Gale Blade: a blade thrown out past its target and back to the hand, cutting on both legs
	// (ElementalBolt, Returning). It left the chain archetype to Chain Lightning and Shadow Bolt, and its
	// cards were a strike's numbers - damage, crit, crit damage, speed - until 2026-10-29
	// (.ai/spell-roster.md 2e, "Gale Blade"). Every card now changes the throw the player watches, out of
	// what a boomerang of wind and lightning is: it hangs at the end of its throw, comes home on a hook,
	// trails a live wire to the hand, calls lightning down where it turns, bursts into a squall. The ids
	// and the element each level 4 card gains are the ones they always were, so saves carry over; the
	// behaviour is switched on by id (BoltEvolutions).
	private static void SetupGaleBladeEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = BoltEvolutions.EddyId,
			BonusElementWeights = new() { { "Wind", 1 } },
			DisplayName = "Eddy",
			Description = $"At the end of its throw the blade hangs in an eddy for {BoltEvolutions.EddySeconds:0.#}s, swelling and spinning, cutting everything round it again and again before it comes home. +25% damage.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.25f,
			VisualTag = "Eddy",
			ModulateColor = new Color(0.86f, 0.98f, 0.94f),
			SynergyTag = "Wind / Hang time",
			SynergyDescription = "Throw it into the pack and let the pack sit in the eddy."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = BoltEvolutions.HookedThrowId,
			DisplayName = "Hooked Throw",
			Description = "The blade comes home on a wide hook instead of back along its throw, cutting a second path through the pack. +10% critical strike chance.",
			MilestoneLevel = 4,
			Effect = SpellEffect.CritChance,
			EffectValue = 0.1f,
			VisualTag = "HookedThrow",
			ModulateColor = new Color(0.78f, 0.94f, 1.0f),
			SynergyTag = "Wind / Two paths",
			SynergyDescription = "Out along one line and home along another: twice the ground for one throw."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = BoltEvolutions.LiveWireId,
			BonusElementWeights = new() { { "Lightning", 1 } },
			DisplayName = "Live Wire",
			Description = $"A crackling wire of lightning runs from your hand to the blade while it flies, and cuts whatever it crosses for {BoltEvolutions.LiveWireShare * 100f:0}% of a cut every {BoltEvolutions.LiveWireTickSeconds:0.#}s. The blade flies 20% faster.",
			MilestoneLevel = 4,
			SpeedMultiplier = 1.2f,
			VisualTag = "LiveWire",
			ModulateColor = new Color(1.0f, 0.97f, 0.78f),
			SynergyTag = "Lightning / Line",
			SynergyDescription = "Everything between you and the blade is in the way of the wire."
		});

		// Level 8 (2 choices). No BonusElementWeights: element growth on a hybrid happens at level 4, and
		// granting one here as well would let a player who took the matching level 4 option reach three
		// of a single element, which only a pure spell may do.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = BoltEvolutions.ThunderheadId,
			DisplayName = "Thunderhead",
			Description = $"Each time the blade turns, lightning strikes where it hangs: everything near takes {BoltEvolutions.ThunderShare * 100f:0}% of a cut and is staggered. Every cut +15%.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.15f,
			ScaleMultiplier = 1.3f,
			VisualTag = "Thunderhead",
			ModulateColor = new Color(1.0f, 0.96f, 0.74f),
			SynergyTag = "Ascension / Lightning",
			SynergyDescription = "Ascension: every throw ends in a thunderclap on the pack it was thrown at."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = BoltEvolutions.SquallId,
			DisplayName = "Squall",
			Description = $"At the end of its throw the blade bursts into {BoltEvolutions.SquallBlades}, and all of them come home by different paths, each cutting for {BoltEvolutions.SquallShare * 100f:0}%. -20% cooldown.",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.8f,
			VisualTag = "Squall",
			ModulateColor = new Color(0.80f, 0.98f, 0.94f),
			SynergyTag = "Ascension / Wind",
			SynergyDescription = "Ascension: the way home sweeps a fan of ground, not a line."
		});
	}


	private static void SetupAegisWardEvolutions(SpellData spell)
	{
		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "aegis_ward_reflective",
			DisplayName = "Reflective Plating",
			// Player.TryReflectAbsorbed, since 2026-10-26 (it was +15 shield and nothing else).
			Description = "+15 shield, and whatever the shield absorbs, the nearest enemy takes back at 150%.",
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
			// The +50% was an EffectValue of 50 with no Effect, and the armour was never built. The
			// spell's damage IS the shield (Player, the aegis_ward branch), so the capacity is a damage
			// multiplier and the card says only that (2026-10-26). Metal's tag is the attunement.
			Description = "The ward is forged in metal: +50% max shield capacity and +1 Metal tag, for 12% slower recharge.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			DamageMultiplier = 1.5f,
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
			// Player.SunAegisFlash, since 2026-10-26. The beams while charged were never built and went.
			Description = "Double shield capacity and +1 Light tag; a breaking shield flashes, staggering and weakening everything close.",
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
			// Built 2026-10-26 (Player.CrystalShellBurst): the break bursts for twice the shield's size on
			// everything within reach. The PierceBonus of 8 went - a burst reaches all it touches anyway.
			Description = "+40 shield, and a breaking shield bursts into crystal shards that cut everything close for twice its size.",
			MilestoneLevel = 8,
			DamageBonus = 40,
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

	// Void Lance: a short-lived lance of shadow thrust out of the wizard's hands at the nearest enemy, where
	// his position is the aim. EVERY CARD ITS OWN (.ai/spell-roster.md section 2h, phase 2, 2026-10-29):
	// Abyssal Pierce (open 0.5 s longer), Rift Blade (+50% size) and Annihilation Beam (x2 size, +200%) only
	// moved numbers, and Gravity Spike dragged the crowd onto the lance - Hollow Star's pull. Each now
	// changes the lance where it can be seen: it scars the ground, it runs out behind you too, it frightens
	// what it pierces, it turns round you like a clock hand, it ends in a black hole. The ids and the element
	// each gains are the ones they always were; the behaviour is VoidLance's, by id, and VoidScars'.
	private static void SetupVoidLanceEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = VoidLance.ScarId,
			DisplayName = "Abyssal Scar",
			// Was Abyssal Pierce, the lance held open 0.5 s longer (Effect ZoneDuration): the same lance,
			// lingering. What lingers now is left on the ground, apart from the lance.
			Description = $"As the lance withdraws it tears a scar of void into the ground along its line that stays {VoidLance.ScarSeconds:0}s, cutting whatever stands on it for {VoidLance.ScarShare * 100f:0}% of the lance every {VoidLance.ScarTickSeconds:0.#}s.",
			MilestoneLevel = 4,
			VisualTag = "AbyssalScar",
			ModulateColor = new Color(0.92f, 0.86f, 1.0f),
			SynergyTag = "Darkness / Ground",
			SynergyDescription = "Every cast leaves a line the pack has to cross to reach you."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = VoidLance.GlaiveId,
			DisplayName = "Void Glaive",
			// Was Rift Blade, +50% length and width (ScaleMultiplier 1.5): a bigger lance. Now a second blade.
			Description = $"The lance runs out of both hands: with every cast a second blade, {VoidLance.GlaiveBackShare * 100f:0}% as long, thrusts out behind you along the same line.",
			MilestoneLevel = 4,
			VisualTag = "VoidGlaive",
			ModulateColor = new Color(0.92f, 0.86f, 1.0f),
			SynergyTag = "Darkness / Position",
			SynergyDescription = "Whatever closes on your back is cut by the same thrust that goes for the nearest."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = VoidLance.DreadId,
			BonusElementWeights = new() { { "Darkness", 1 } },
			DisplayName = "Dread",
			// Was Gravity Spike, which dragged the crowd onto the lance - the pull is Hollow Star's (2e).
			// Darkness's other verb is fear (Player.TryFrighten, a chance on every Darkness hit): the lance
			// makes it certain, through Enemy.ApplyFear and its immunities.
			Description = $"Everything the lance runs through is struck with dread and turns and runs from you for {VoidLance.DreadSeconds:0.#}s (elites and bosses stand firm). +20% damage, and +1 Darkness tag.",
			CooldownMultiplier = 1.12f,
			DamageMultiplier = 1.2f,
			MilestoneLevel = 4,
			VisualTag = "Dread",
			ModulateColor = new Color(0.86f, 0.78f, 1.0f),
			SynergyTag = "Darkness / Fear",
			SynergyDescription = "The defensive choice: a lane of the pack scattered away from you with every cast."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = VoidLance.WitchingHourId,
			BonusElementWeights = new() { { "Arcane", 1 } },
			DisplayName = "Witching Hour",
			// Was Annihilation Beam, the lance twice as long and wide for +200%: the same thrust, bigger. The
			// roster's own plan for the spell (section 2, "fixed, slowly rotating - position, not aim") is
			// the Ascension: one lance that never withdraws.
			Description = $"The lance no longer withdraws: it stays in your hands and turns round you like the hand of a clock, once every {VoidLance.WitchingTurnSeconds:0.#}s, cutting everything it sweeps across. +1 Arcane tag.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			VisualTag = "WitchingHour",
			ModulateColor = new Color(0.94f, 0.86f, 1.0f),
			SynergyTag = "Arcane / Position",
			SynergyDescription = "Ascension: no aim at all - stand where the hand will sweep the most of them."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = VoidLance.CollapseId,
			DisplayName = "Cosmic Collapse",
			// VoidLance.CollapseAtTerminus, since 2026-10-26: a 60 px burst times this Area (132 px). Kept in
			// phase 2 (2026-10-29), its hole drawn: a disc of void that swells and caves in at the tip.
			Description = "The lance ends in a black hole: at its tip a disc of void swells and caves in on itself, bursting over everything round it (+140% Damage, +120% Area).",
			MilestoneLevel = 8,
			DamageMultiplier = 2.4f,
			AreaMultiplier = 2.2f,
			ScaleMultiplier = 1.8f,
			VisualTag = "CosmicCollapse",
			ModulateColor = new Color(0.3f, 0.1f, 0.7f),
			SynergyTag = "Darkness / Singularity",
			SynergyDescription = "Ascension: a second hit at the far end of every lance."
		});
	}

	private static void SetupCycloneSlashEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = OrbitingBlade.TwinGyresId,
			// Phase 2 (.ai/spell-roster.md section 2h): once Dual Windblades, +2 blades and +30% spin on the
			// one ring. A second ring now (OrbitingBlade's inner ring); the id stays. The outer ring keeps one
			// of the two blades: with none, against a crowd that lives, the card measured 374-385 a second at
			// level 7 to Dual Windblades' 425 - an inner ring cuts only what gets through the outer one.
			DisplayName = "Twin Gyres",
			Description = $"A second, inner ring of {OrbitingBlade.InnerBlades} blades whirls the other way round you, holding tight "
				+ "whatever the gust does: when you run and the outer ring swings wide, what slips through the gap still meets "
				+ "a blade. +1 blade on the outer ring, which spins 30% faster.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 1,
			SpeedMultiplier = 1.3f,
			VisualTag = "TwinGyres",
			ModulateColor = new Color(0.5f, 1.0f, 0.8f),
			SynergyTag = "Wind / Two rings",
			SynergyDescription = "A wide ring for the crowd and a tight one for whatever reaches you."
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
			Id = OrbitingBlade.WindShearId,
			BonusElementWeights = new() { { "Wind", 1 } },
			// Phase 2: once Tailwind Surge, "the blades spin 25% faster" - a number. Now a full gust throws
			// crescents off the ring (OrbitingBlade.ShedCrescents). Laelaps' required level 4: the id and
			// Wind's tag stay, and so does the spin, which his Endless Chase was measured on.
			DisplayName = "Wind Shear",
			Description = $"At a full gust the blades shed crescents of wind: in turn, each flings one off along its spin every "
				+ $"{OrbitingBlade.ShearSeconds:0.#} s, and it flies on past the ring, cutting everything it crosses for "
				+ $"{OrbitingBlade.ShearShare * 100f:0}% of a cut. Stop and it stops. The blades spin 25% faster.",
			MilestoneLevel = 4,
			SpeedMultiplier = 1.25f,
			VisualTag = "WindShear",
			ModulateColor = new Color(0.6f, 0.95f, 1.0f),
			SynergyTag = "Wind / Crescents",
			SynergyDescription = "Deepens Wind. The whirlwind's reach, thrown past the ring while you keep running."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "cyclone_slash_typhoon_core",
			BonusElementWeights = new() { { "Wind", 1 } },
			DisplayName = "Typhoon Core",
			// The Area reaches the orbit and the blades since 2026-10-26 (OrbitingBlade.RefreshComputedStats).
			// "Permanent" is literal since the whirlwind became a running one (issue #68, 2026-10-28): it
			// never collapses when you stop (OrbitingBlade.TyphoonEvolutionId).
			// Phase 2 (section 2h): visible as it was - it stays wide when you stop - and the words say so first.
			Description = "A permanent hurricane: the ring never falls in when you stop, it stands at its widest round you, and it "
				+ "is two and a half times the size. +100% damage.",
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
			// Phase 2 (section 2h): kept - it is visible - but flagged: its drag is the pull, Hollow Star's verb.
			Description = "The whirlwind drags the crowd round it in to the blades, and they cut harder. +150% damage.",
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
			Id = ElementalPulse.SunDogsId,
			DisplayName = "Sun Dogs",
			Description = $"Two mock suns hang either side of you and flare with every pulse, each {ElementalPulse.SunDogReach * 100f:0}% as wide as the flare, "
				+ $"for {ElementalPulse.SunDogDamageShare * 100f:0}% of its damage - and as the flare builds while you stand still they drift farther out. Pulses 20% faster.",
			MilestoneLevel = 4,
			CooldownMultiplier = 0.8f,
			VisualTag = "SunDogs",
			ModulateColor = new Color(1.0f, 0.97f, 0.82f),
			SynergyTag = "Light / Width",
			SynergyDescription = "Three suns abreast: the flare reaches the packs at your sides."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = ElementalPulse.ProminenceId,
			BonusElementWeights = new() { { "Fire", 1 } },
			DisplayName = "Solar Prominence",
			// Was Scorching Heat: +45% damage and the tag. The game has no burn status, so the heat is now a
			// thing the flare throws (ElementalPulse.HurlProminences).
			Description = $"Every pulse hurls {ElementalPulse.ProminenceLoops} loops of fire off the flare's rim - one more at half charge, another at full - that arc over "
				+ $"and come down on the nearest enemies past its edge, each bursting for {ElementalPulse.ProminenceShare * 100f:0}% of the pulse. +1 Fire tag, +15% damage, 12% slower pulses.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 4,
			DamageMultiplier = 1.15f,
			VisualTag = "SolarProminence",
			ModulateColor = new Color(1.0f, 0.85f, 0.6f),
			SynergyTag = "Light / Fire",
			SynergyDescription = "Deepens Fire. The close spell reaches the rank that hangs back from it."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "solar_flare_blinding_flash",
			DisplayName = "Blinding Flash",
			Description = $"Some of what the flare catches reels away blind: {ElementalPulse.BlindingFlashChance * 100f:0}% of it is staggered "
				+ $"{ElementalPulse.BlindingFlashStunSeconds:0.#} s and thrown back out of the light.",
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
			// And a ward: enemy bolts that come inside the flare burn away (ElementalPulse.BurnBolts).
			Description = "A flare that fills the clearing and burns away every enemy bolt that enters it: +100% Area, +100% Damage, for 12% slower pulses.",
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
			// ElementalPulse.FireCoronaBeams, since 2026-10-26 (afternoon).
			Description = $"Every pulse throws {ElementalPulse.CoronaBeams} sunbeams far past the flare, each cutting everything along it, and +140% damage.",
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
			Id = BlackTentacles.CrushingGripId,
			BonusElementWeights = new() { { "Earth", 1 } },
			DisplayName = "Crushing Grip",
			// Phase 2 (.ai/spell-roster.md section 2h): the root was always there; the coil that shows it is
			// new (BlackTentacles.DrawCoils), so the card leads with what is seen.
			Description = "The tentacles hold what they hit: every slap leaves a coil wound round its target's legs that roots it "
				+ "for 2 seconds. +50% slap damage.",
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

		// Fling (phase 2, .ai/spell-roster.md section 2h): once Abyssal Spread, +60% rift and +2 tentacles -
		// numbers on the same pit. Now the arms throw what they catch (BlackTentacles.Throw); the id stays.
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = BlackTentacles.FlingId,
			DisplayName = "Fling",
			Description = "The tentacles throw what they catch: a slap that lands picks its target up and hurls it across the pit "
				+ $"at the enemy furthest from it, and where it comes down it and everything round it take {BlackTentacles.FlingShare * 100f:0}% "
				+ "of a slap. Elites and bosses are too heavy to throw. +1 tentacle, the rift 20% wider.",
			MilestoneLevel = 4,
			ProjectileCountBonus = 1,
			AreaMultiplier = 1.2f,
			VisualTag = "Fling",
			ModulateColor = new Color(0.4f, 0.15f, 0.6f),
			SynergyTag = "Tentacles / Throw",
			SynergyDescription = "Every slap lands twice: once on what it caught, and again on whatever that lands on."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = "tentacles_soul_drain",
			DisplayName = "Soul Drain",
			Description = $"Every {BlackTentacles.SoulDrainEvery}th slap that lands tears a wisp of life out of what it struck; "
				+ $"it flies to you and heals 1 HP as it arrives, up to {BlackTentacles.SoulDrainCap} HP a cast.",
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
			// Phase 2: the words lead with the forest it draws (BlackTentacles' seven pools), as they did not.
			Description = "The rift breaks into a forest of seven small pits over a field half again as wide, each with its own arms. "
				+ "It opens 44% sooner, and its tentacles slap for +120% damage.",
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

	// Thorned Vine (id thorn_vine, scripts/ThornVine.cs): one vine that bursts up beside the densest pack
	// and lashes across it, cutting through several and poisoning them. Its five milestones wore the
	// default archetype's names until 2026-10-29 and only moved its numbers; each now changes what the
	// vine does (.ai/spell-roster.md section 2e), read by id in ThornVine and Player.FireThornVine. The
	// ids are the old ones, so saves and the Ascension browser carry over, and so are the element gains:
	// Grass deepens on the first card, Poison on the first Ascension, each at the hybrid's price.
	private static void SetupThornVineEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = ThornVine.BarbedLashId,
			DisplayName = "Barbed Lash",
			Description = $"The thorns break off in what the vine cuts: for {ThornBarbs.BarbSeconds:0.#} s, every few steps a barbed enemy "
				+ $"walks tears it open again for {ThornBarbs.TearShare * 100f:0}% of the lash, and leaves a splash of sap behind it.",
			MilestoneLevel = 4,
			BonusElementWeights = new() { { "Grass", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "BarbedLash",
			SynergyTag = "Grass / Barbs",
			SynergyDescription = "Deepens Grass. The more the crowd walks, the more it bleeds - and it always walks at you."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = ThornVine.SnapbackId,
			DisplayName = "Snapback",
			Description = "The whip cracks out and back: after every lash it tears free and sweeps the same arc again the other way, "
				+ "cutting through as many again.",
			MilestoneLevel = 4,
			VisualTag = "Snapback",
			SynergyTag = "Grass / Two strokes",
			SynergyDescription = "Every lash lands twice on the same pack."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = ThornVine.EntangleId,
			DisplayName = "Entangle",
			Description = $"The vine coils round the feet of everything it cuts and roots it for {ThornVine.EntangleSeconds:0.#} s. +20% damage.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.2f,
			VisualTag = "Entangle",
			SynergyTag = "Grass / Root",
			SynergyDescription = "The front of the pack stops where it was cut - for every area spell beside it."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = ThornVine.ThicketId,
			DisplayName = "Thicket",
			Description = $"Where the whip lands, thorny brambles grow across the whole arc it swept and stand {ThornThicket.Seconds:0.#} s, the newest {ThornThicket.MaxStanding} at a time: "
				+ $"whatever is in them is cut for {ThornThicket.TickShare * 100f:0}% of the lash every {ThornThicket.TickSeconds:0.#} s, "
				+ $"poisoned and slowed {(1f - ThornThicket.SlowMultiplier) * 100f:0}%. The vine reaches 30% farther and lashes 60% harder.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.6f,
			AreaMultiplier = 1.3f,
			BonusElementWeights = new() { { "Poison", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "Thicket",
			SynergyTag = "Ascension / Poison",
			SynergyDescription = "Ascension: deepens Poison. Every lash leaves a crescent of thorns the pack has to wade through."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = ThornVine.HydraVineId,
			DisplayName = "Hydra Vine",
			Description = $"One root bursts up in the thick of the pack and splits into {ThornVine.HydraHeads} heads, "
				+ $"{(ThornVine.HydraReach - 1f) * 100f:0}% longer, each lashing its own third of the ring round it - the pack whipped from the inside. "
				+ "-40% cooldown, and each head cuts through 2 more.",
			MilestoneLevel = 8,
			CooldownMultiplier = 0.6f,
			PierceBonus = 2,
			VisualTag = "HydraVine",
			SynergyTag = "Ascension / Heads",
			SynergyDescription = "Ascension: three lashes a cast, all round the knot."
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
			Id = ConeBlast.ArcticBlastId,
			DisplayName = "Arctic Blast",
			// The 180 degrees is ConeBlast.HalfAngleRad, since 2026-10-26. x1.4 and no RangeBonus, so the
			// range is the +40% the card says; x1.5 on top of +50 px was +79%. Kept in phase 2 (section 2h):
			// a cone's angle is its shape, and a half-moon is a different weapon from a 60-degree cone.
			Description = $"The cone opens into a half-moon: the ice sprays {ConeBlast.ArcticBlastDegrees:0} degrees across the front of you "
				+ "instead of 60, and reaches 40% farther.",
			MilestoneLevel = 4,
			AreaMultiplier = 1.4f,
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
			Id = ConeBlast.ZeroPointId,
			BonusElementWeights = new() { { "Ice", 1 } },
			DisplayName = "Zero Point Shockwave",
			// Phase 2 (.ai/spell-roster.md section 2h): it was +150% damage and +40% reach on the same spray.
			// Now the blast goes on past its own end - ConeBlast's relay - and the numbers ride along.
			Description = $"The cold gathers at the tip of the cone into a star of ice and bursts on: a second cone, as wide and "
				+ $"{ConeBlast.RelayLength * 100f:0}% as long, is thrown on from that point into what stood behind the front. "
				+ "The cone reaches 40% farther, +120% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.2f,
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
			SynergyTag = "Ice / Relay",
			SynergyDescription = "Ascension: deepens Ice. One blast that goes on through the front rank into the pack behind it."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "cone_glacier_tide",
			DisplayName = "Glacier Tide",
			Description = "The blast goes out as a wall of ice that throws everything it hits away from you. +100% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.0f,
			KnockbackBonus = 120f,
			VisualTag = "GlacierTide",
			ModulateColor = new Color(0.6f, 0.9f, 1.0f),
			SynergyTag = "Ice / Force",
			SynergyDescription = "Ascension: every cast clears the space in front of you."
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
			// The shockwave's KnockbackBonus is read by MeteorImpact since 2026-10-26.
			Description = "Increases direct impact damage by +75%, and the impact throws enemies back.",
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
			// The whole volley still falls (Player.FireMeteorSwarm); there is no single asteroid, so the
			// card says what each meteor becomes (2026-10-26).
			Description = "Every meteor is a colossal impact: +250% Damage and three times the blast.",
			CooldownMultiplier = 1.12f,
			MilestoneLevel = 8,
			DamageMultiplier = 3.5f,
			AreaMultiplier = 3.0f,
			VisualTag = "ExtinctionEvent",
			ModulateColor = new Color(1.0f, 0.2f, 0.05f),
			SynergyTag = "Fire / Cataclysm",
			SynergyDescription = "Ascension: a volley of craters that overlap into one."
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
			Id = GroundSpike.SplinterFieldId,
			DisplayName = "Splinter Field",
			// Phase 2 (.ai/spell-roster.md section 2h): it was "the eruption bursts 35% wider" - a number on
			// the same disc. Now the eruption splinters through the ground (GroundSpike.LaySplinters). It
			// is the Geomancer's required level 4, so its id and its Earth stay.
			Description = $"The eruption splinters through the ground: as its cracks run out, a splinter of glass punches up under "
				+ $"each of the {GroundSpike.SplinterCount} nearest enemies just past its rim, one after another, striking what "
				+ $"stands over it for {GroundSpike.SplinterShare * 100f:0}% of the spike. The eruption 15% wider.",
			MilestoneLevel = 4,
			BonusElementWeights = new() { { "Earth", 1 } },
			// No ScaleMultiplier since 2026-10-28: the spike draws its cracks, chips and splinters out of
			// the radius it hits (GroundSpike), so the area is drawn as wide as it hurts.
			AreaMultiplier = 1.15f,
			VisualTag = "SplinterField",
			ModulateColor = new Color(0.55f, 0.48f, 0.42f),
			SynergyTag = "Earth / Splinters",
			SynergyDescription = "Deepens Earth. One eruption reaches the ring of the crowd round its target."
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
			Id = GroundSpike.DeepStrataId,
			DisplayName = "Deep Strata",
			// Phase 2: it was +45% damage and a longer cast range. Now what the spike breaks out under goes up
			// with it (Enemy.ApplyLaunch, through GroundSpike.Erupt); the reach and some damage ride along.
			Description = $"The spike is driven up from far deeper and throws everything it breaks out under up into the air: "
				+ $"for {GroundSpike.LaunchSeconds:0.#} s they hang there and do nothing, then drop where they were. Elites and "
				+ "bosses are too heavy to throw and reel instead. +40% damage, and it reaches farther.",
			MilestoneLevel = 4,
			// +40%: at +30% it measured 460 a second at level 7 against the old card's 532 (+45%, thirty
			// times health); what is in the air is not on the wizard, which the number does not show.
			DamageMultiplier = 1.4f,
			RangeBonus = 120f,
			VisualTag = "DeepStrata",
			ModulateColor = new Color(0.42f, 0.40f, 0.44f),
			SynergyTag = "Earth / Upheaval",
			SynergyDescription = "No new element. The front rank spends half its life in the air instead of on you."
		});

		// Level 8 (2 choices)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = GroundSpike.TectonicId,
			DisplayName = "Tectonic Uprising",
			// Phase 2: it was +120% damage and +60% area on the same eruption. Now the ground it broke stays
			// open and keeps erupting (GroundSpike's vent). The Geomancer's required level 8, so the id and
			// its Earth stay; Sundering raises its ring on top of it as before.
			Description = $"The ground does not close: where each spike broke out a vent stays open for {GroundSpike.VentSeconds:0} s, "
				+ $"its cracks smouldering, and every {GroundSpike.VentBeatSeconds:0.#} s it throws up a fresh spike of glass under "
				+ $"the nearest enemy that comes near it, for {GroundSpike.VentShare * 100f:0}% of the spike. +80% damage, the eruption 30% wider.",
			MilestoneLevel = 8,
			BonusElementWeights = new() { { "Earth", 1 } },
			DamageMultiplier = 1.8f,
			// No ScaleMultiplier, as for Splinter Field: the area is what is drawn.
			AreaMultiplier = 1.3f,
			VisualTag = "TectonicUprising",
			ModulateColor = new Color(0.36f, 0.33f, 0.36f),
			SynergyTag = "Earth / Vents",
			SynergyDescription = "Ascension: deepens Earth. Where the swarm stood, the ground goes on breaking under whatever walks there next."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = "obsidian_spike_basalt_lance",
			DisplayName = "Basalt Lance",
			// An eruption already hits everything in it; the PierceBonus of 4 that said "runs enemies
			// through" reached nothing and went (2026-10-26). What it pays is the damage.
			// GroundSpike.DriveLance, since 2026-10-26: three more spikes run on through the column behind
			// the target. The Ascension had been damage alone on the same round eruption.
			Description = $"The spike is forged rather than grown - a needle of volcanic glass: +140% damage, and "
				+ $"it drives on through the crowd behind its target, {GroundSpike.LanceFollowers} more spikes in a line.",
			MilestoneLevel = 8,
			DamageMultiplier = 2.4f,
			// Its ScaleMultiplier of 1.3 went 2026-10-28: with no area behind it, it drew each spike
			// 30% wider than it hit.
			VisualTag = "BasaltLance",
			ModulateColor = new Color(0.52f, 0.54f, 0.60f),
			SynergyTag = "Earth / Metal",
			SynergyDescription = "Ascension: no new element - a needle that skewers whole columns."
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
				+ $"damage (x{1f + SporeMushroom.DoomcapDamagePerMushroom * 6f:0.#} at six), and +65% more on top. Cooldown +40%.",
			MilestoneLevel = 8,
			// x1.65 on top of the per-mushroom bonus: x1.92 of the scatter at level 7 with it (2026-10-26, --enemy-hp=30).
			DamageMultiplier = 1.65f,
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
				+ $"and each of those sprouts {SporeMushroom.BloomSecondChildren} more. Every burst +85% damage.",
			MilestoneLevel = 8,
			// x1.94 of the spell at level 7 with the damage (2026-10-26, --enemy-hp=30): the sprouts land where the first burst
			// already thinned the crowd.
			DamageMultiplier = 1.85f,
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
			second.Length > 0 ? $"Colossal scale ascension (+80% Damage, three times the Area), steeped in {second}." : "Colossal scale ascension (+80% Damage, three times the Area).",
			"Ascension / Cataclysm", "Ascension: Massive raw damage and area destruction.",
			o => { o.DamageMultiplier = 1.8f; o.AreaMultiplier = 3.0f; o.ScaleMultiplier = 3.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			"Continuous rapid cycling ascension (-50% Cooldown, +3 Projectiles, +4 Pierce).",
			"Ascension / Speed", "Ascension: Non-stop rapid barrage.",
			o => { o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; o.ScaleMultiplier = 1.4f; }));
	}

	// Gravewell (scripts/GravewellTrap.cs, GravePath.cs): a grave opened on the swarm's path that springs
	// when trodden on and roots what it catches. Its five milestones wore the default archetype's names
	// until 2026-10-29 and only moved its numbers; each now changes what the grave does (.ai/spell-roster.md
	// section 2e), read by id in GravewellTrap.For and Player.FireGravewellTrap. The ids are the old ones, so
	// saves and the Ascension browser carry over, and so are the element gains: Earth deepens on the first
	// card, Darkness on the first Ascension, each at the hybrid's price. None of them pulls: the pull is
	// Hollow Star's (the grave's own tug toward the pit is a third of the way and no more).
	private static void SetupGravewellEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = GravewellTrap.BoneSpikesId,
			DisplayName = "Bone Spikes",
			Description = $"When a grave springs it spits its bones out: {GravewellTrap.BoneCount} shards of bone fly out past the collapse, "
				+ $"each striking the first enemy in its way for {GravewellTrap.BoneShare * 100f:0}% of the collapse's damage. +20% damage.",
			MilestoneLevel = 4,
			// +20%: without it the graves and their bones measured 58% of the old Amplified Impact at level 7
			// (--enemy-hp=30), whose collapse was 30% wider.
			DamageMultiplier = 1.2f,
			BonusElementWeights = new() { { "Earth", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "BoneSpikes",
			SynergyTag = "Earth / Shards",
			SynergyDescription = "Deepens Earth. A grave reaches the ones following the one that sprang it."
		});
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = GravewellTrap.QuicksetId,
			DisplayName = "Quickset",
			Description = "Graves tear open at once, already armed, earth flung off them, hard in front of each enemy coming: "
				+ "the first to tread on one springs it. Graves are dug 30% more often.",
			MilestoneLevel = 4,
			CooldownMultiplier = 0.7f,
			VisualTag = "Quickset",
			SynergyTag = "Darkness / Ambush",
			SynergyDescription = "No wait for the swarm to walk in: it is already standing on the grave."
		});
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = GravewellTrap.GraveChillId,
			DisplayName = "Grave Chill",
			Description = $"What the grave holds is chilled to the bone, its breath smoking cold: Weakened (-{GravewellTrap.ChillWeaken * 100f:0}% damage dealt) "
				+ $"while held and {GravewellTrap.ChillAfterSeconds:0.#} s after. +20% damage.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.2f,
			VisualTag = "GraveChill",
			SynergyTag = "Darkness / Weaken",
			SynergyDescription = "What climbs out of the grave hits like it is still in it."
		});
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = GravewellTrap.MassGraveId,
			DisplayName = "Mass Grave",
			Description = $"One great grave a cast, opened under the front of the swarm: its bank twice as wide, its collapse "
				+ $"{GravewellTrap.MassBlastScale:0.#} times as wide, holding what it takes {GravewellTrap.MassHoldBonus:0.#} s longer and draining it "
				+ $"for {GravewellTrap.DrainShare * 100f:0}% every {GravewellTrap.DrainTickSeconds:0.##} s while it does. +80% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.8f,
			BonusElementWeights = new() { { "Darkness", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "MassGrave",
			SynergyTag = "Ascension / Darkness",
			SynergyDescription = "Ascension: deepens Darkness. One grave the size of the front rank, and nothing in it walks out."
		});
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = GravewellTrap.GraveLineId,
			DisplayName = "Grave Line",
			Description = "The graves are dug in a line along the ground you just crossed - a minefield behind you for whatever follows - "
				+ "or, when you stand your ground, in a line out toward what is coming. 3 more graves a cast, dug twice as often, +80% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.8f,
			CooldownMultiplier = 0.5f,
			ProjectileCountBonus = 3,
			VisualTag = "GraveLine",
			SynergyTag = "Ascension / Minefield",
			SynergyDescription = "Ascension: run, and the ground behind you fills with graves."
		});
	}

	// Hollow Star (scripts/HollowStar.cs): a well that drags a pack into one knot and collapses on it.
	// Area is both the pull's reach and the collapse. Speed is how hard it drags. Projectiles are wells,
	// each on its own pack (Player.FireHollowStar).
	//
	// Its own four cards since 2026-10-29 (.ai/spell-roster.md section 2e), each a thing the pull does
	// that the player can watch: the knot crushed by its own weight, two knots dragged into one, a star
	// that collapses three times, a collapse that seeds wells on the next packs. The pull is this spell's
	// verb and no other's, so its cards may develop it. Branch() keeps each id, its element gain and the
	// gain's price; the name and the look are set here, after it. Crush and Black Sun (ids
	// amplified_impact and titan_cataclysm) are John Dee's Apotheosis path, as their ids always were.
	// The behaviours are HollowStar's, by id; only the numbers on the cards ride the stat fields.
	private static void SetupHollowStarEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"The collapse crushes the knot by its own weight: +{HollowStar.CrushPerBody * 100f:0}% damage for every enemy caught in it, "
				+ $"up to +{HollowStar.CrushPerBody * HollowStar.CrushMostBodies * 100f:0}% at {HollowStar.CrushMostBodies}. The core swells as the knot fills.",
			"Knot / Power", "Pull a pack in tight and every body in it hits the rest harder.",
			o => { o.DisplayName = "Crush"; o.VisualTag = "Crush"; o.ModulateColor = Colors.White; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"Two wells open on two packs at once and drift together as they pull, tethered, dragging both knots into one "
				+ "for both collapses to land on. Each drags 15% harder.",
			"Twin / Pull", "Two packs become one knot, and it is hit twice.",
			o => { o.DisplayName = "Twin Stars"; o.VisualTag = "TwinStars"; o.ModulateColor = Colors.White; o.ProjectileCountBonus = 1; o.SpeedMultiplier = 1.15f; }));
		AddElementPath(spell);
		// +60%: at +30% it measured x1.5 of the spell at level 7 against a crowd it was still clearing; at
		// +60%, x2.30 against one it could not (2026-10-29, --enemy-hp=30; the Titan it replaced, x2.63).
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"The star does not go out: it pulls and collapses {HollowStar.BlackSunCollapses} times before it fades, and a mote circles "
				+ "its core for each collapse it has left. Half again as wide, +60% damage.",
			"Ascension / Black Sun", "Ascension: one star holds a pack for six seconds and crushes it three times.",
			o => { o.DisplayName = "Black Sun"; o.VisualTag = "BlackSun"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.6f; o.AreaMultiplier = 1.5f; }));
		// -40% cooldown and +20%: at +40% it measured x2.26 of level 7, 40% over the Infinite Horizon it
		// replaced (x1.99) at the same footing (2026-10-29, --enemy-hp=30); +20% puts it about x1.94.
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"Each collapse throws {HollowStar.NovaShards} shards of the star at the nearest packs outside it, and each opens a smaller well "
				+ "where it lands that knots that pack in turn. -40% cooldown, +20% damage.",
			"Ascension / Nova", "Ascension: one knot seeds the next, across the field.",
			o => { o.DisplayName = "Nova Seed"; o.VisualTag = "NovaSeed"; o.ModulateColor = Colors.White; o.CooldownMultiplier = 0.6f; o.DamageMultiplier = 1.2f; }));
	}

	// Bramble Seed (scripts/BrambleSeed.cs): a seed at your feet that grows into a patch of thorns. Area
	// is its full-grown radius. Speed is how fast it grows there. Projectiles are patches allowed to stand
	// at once - still one seed a cast, since a cast that scattered seeds would be Wildwood's verb, the
	// Apotheosis's - and they matter: with the cap fixed at three, a shorter cooldown only withered each
	// patch before it had grown.
	//
	// Its own four cards since 2026-10-29 (.ai/spell-roster.md section 2e), each something the thicket
	// visibly does or leaves: burrs carried out on the swarm, runners creeping to the crowd, one great
	// hedged keep, a storm of thrown thorns. None sows another patch - that is Wildwood's - so the
	// proposal's Runners end in thorns rather than a second patch, and its Seedstorm (seeds scattered
	// round you, a patch where each lands) became Thornstorm. Branch() keeps each id, its element gain
	// and the gain's price; the name and the look are set here. Burrs and Bramble Keep (ids
	// amplified_impact and titan_cataclysm) are Vainamoinen's Apotheosis path, as their ids always were.
	private static void SetupBrambleSeedEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"Thorns cling: whatever walks out of the thicket carries burrs for {BrambleSeed.BurrSeconds:0.#}s that slow it and prick it "
				+ $"for {BrambleSeed.BurrPrick * 100f:0}% of a thorn's bite. +25% damage, and the thicket grows 15% wider.",
			"Burrs / Slow", "The swarm carries your thicket out with it.",
			o => { o.DisplayName = "Burrs"; o.VisualTag = "Burrs"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.25f; o.AreaMultiplier = 1.15f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"Once half grown, the thicket sends {BrambleSeed.RunnerCount} thorned runners creeping over the ground to the nearest enemies past "
				+ "its rim; whatever stands on one is cut and slowed as if inside. Grows 25% faster.",
			"Runners / Reach", "The thicket reaches for the crowd that will not walk into it.",
			o => { o.DisplayName = "Runners"; o.VisualTag = "Runners"; o.ModulateColor = Colors.White; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		// +80% and +70%: x1.87 and x1.72 of the spell at level 7 standing still (2026-10-29, --enemy-hp=60,
		// --stand-still) against x1.80 and x1.64 for the Titan Cataclysm and Infinite Horizon they replaced
		// on the same footing - which were clearing the field, and these were not. At +50% and +40% they
		// read x1.69 and x1.63.
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"One great thicket three times as wide, ringed by a hedge of thorns that cuts {BrambleSeed.HedgeDamage * 100f:0}% harder and slows to "
				+ "a crawl. It stands while you stay inside it; walk out, and the next cast sows a new keep at your feet. +80% damage.",
			"Ascension / Keep", "Ascension: hold your ground and the hedge holds the swarm.",
			o => { o.DisplayName = "Bramble Keep"; o.VisualTag = "BrambleKeep"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.8f; o.AreaMultiplier = 3.0f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"Every grown thicket throws a ring of {BrambleSeed.ThornsPerVolley} thorns out to {BrambleSeed.ThornReach:0}x its width every {BrambleSeed.ThornVolleySeconds:0.#}s, "
				+ "hitting whatever they fly through. 2 more thickets may stand at once, -30% cooldown, +70% damage.",
			"Ascension / Thornstorm", "Ascension: the thicket fights past its own edge.",
			o => { o.DisplayName = "Thornstorm"; o.VisualTag = "Thornstorm"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.7f; o.CooldownMultiplier = 0.7f; o.ProjectileCountBonus = 2; }));
	}

	// Contagion (scripts/Contagion.cs): a plague on one carrier that jumps to everything near it when it
	// dies. Area is the jump. Speed is how fast the poison works - the same dose, sooner, so carriers
	// fall and pass it on sooner. Projectiles are carriers, each with its own plague. Pierce is how many
	// more enemies one plague may reach before it burns out (Contagion.MaxInfected).
	//
	// Its own four cards since 2026-10-29 (.ai/spell-roster.md section 2e), each a new way the plague
	// moves through the crowd, drawn over it: a fever cloud left by the dead, a carrier that bursts the
	// moment it is cast on, every carrier bursting on one beat, a wind of the plague. Fever Burst's
	// "dosing all near it" was what a death already did, so its card is the hit and the cloud; the
	// proposal's Quickening is named Outbreak, since Quickening is one of Gale Blade's cards. Branch()
	// keeps each id, its element gain and the gain's price; the name and the look are set here.
	private static void SetupContagionEvolutions(SpellData spell)
	{
		const int reach = 8;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"A carrier that dies bursts into a fever cloud: everything in the jump is hit for {Contagion.FeverHit * 100f:0}%, carrier or "
				+ $"not, and the cloud hangs where it fell for {Contagion.FeverCloudSeconds:0.#}s, infecting whatever walks into it. +25% damage.",
			"Fever / Burst", "Every death in the pack is a blow to the rest of it.",
			o => { o.DisplayName = "Fever Burst"; o.VisualTag = "FeverBurst"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			"The plague breaks out at once: the first carrier bursts the moment it is infected, spreading it through its own pack, "
				+ "and still jumps again when it dies.",
			"Outbreak / Cadence", "No waiting for the first death: the pack has it on the cast.",
			o => { o.DisplayName = "Outbreak"; o.VisualTag = "Outbreak"; o.ModulateColor = Colors.White; }));
		AddElementPath(spell);
		// +80% and +60%: within 2% and 6% of the Titan Cataclysm and Infinite Horizon they replaced against the
		// same crowd (2026-10-29, --enemy-hp=12, where all four clear the field), and within 10% on the
		// 8-minute run. At +30% each they fell a fifth short.
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"Every {Contagion.PandemicSeconds:0.#}s, every carrier on the field bursts at once without dying: each burst infects what is "
				+ $"round it and hits it for {Contagion.PandemicHit * 100f:0}%. The marks swell before each beat. Jumps 50% farther, +80% damage.",
			"Ascension / Pandemic", "Ascension: the whole infected crowd goes off on one beat.",
			o => { o.DisplayName = "Pandemic"; o.VisualTag = "Pandemic"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.8f; o.AreaMultiplier = 1.5f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"Each cast also looses a cloud of the plague that drifts out from you through the crowd for {Contagion.WindSeconds:0.#}s, "
				+ $"infecting everything it passes. Each plague can reach {reach} more enemies, -40% cooldown, +60% damage.",
			"Ascension / Plague Wind", "Ascension: the plague walks through the swarm on its own.",
			o => { o.DisplayName = "Plague Wind"; o.VisualTag = "PlagueWind"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.6f; o.CooldownMultiplier = 0.6f; o.PierceBonus = reach; }));
	}

	// Iron Palisade (scripts/IronPalisade.cs): a line of stakes between you and the pack. Area is the
	// wall's length (it read none, and Titan Cataclysm's x2 visual scale stood in for it). Speed is how
	// often the stakes cut what leans on them. Projectiles are walls, each toward another pack. Pierce
	// means nothing to a wall, which stops what it meets, so it became Zone Duration: how long it stands.
	//
	// Its own four cards since 2026-10-29 (.ai/spell-roster.md section 2e), each a change to the wall you
	// can see: a spiked rail and the blood it draws, caltrops left where it stood, a ring of stakes round a
	// pack, a wall that walks. The proposal's Quick Raise (the wall going up in a ring round you where you
	// stop) is Sundering's verb - the Geomancer's Apotheosis raises a ring round him that shoves the near
	// ones back - so the cadence card leaves caltrops behind instead. Branch() keeps each id, its element
	// gain and the gain's price; the name and the look are set here.
	private static void SetupIronPalisadeEvolutions(SpellData spell)
	{
		const float stand = 1.0f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"A spiked rail runs along the stakes: what touches the wall is thrown back {(IronPalisade.RailThrow - 1f) * 100f:0}% harder and bleeds "
				+ $"for {IronPalisade.BleedSeconds:0.#}s, {IronPalisade.BleedShare * 100f:0}% of a cut every half second. +20% damage, and the wall is 15% longer.",
			"Rail / Bleed", "The front is a thing they leave bleeding.",
			o => { o.DisplayName = "Spiked Rail"; o.VisualTag = "SpikedRail"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.2f; o.AreaMultiplier = 1.15f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"When the wall comes down, its stakes break into caltrops strewn on its far side for {IronPalisade.CaltropSeconds:0.#}s, cutting what "
				+ $"walks over them for {IronPalisade.CaltropShare * 100f:0}% and slowing it {(1f - IronPalisade.CaltropSlow) * 100f:0}%. -25% cooldown.",
			"Caltrops / Ground", "The ground the wall held stays held after it falls.",
			o => { o.DisplayName = "Caltrops"; o.VisualTag = "Caltrops"; o.ModulateColor = Colors.White; o.CooldownMultiplier = 0.75f; }));
		AddElementPath(spell);
		// Iron Ring at +150% and a ring of 1.4 half-lengths, Rolling Wall at -50% cooldown, two more walls and
		// +30%: x2.96 and x2.46 of the spell at level 7 against a crowd it cannot clear (2026-10-29,
		// --enemy-hp=30), against x2.77 and x2.30 for the cards they replaced. At +80% (ring 1.1) and -30%
		// with one more wall they read x1.4-1.6 at --enemy-hp=10.
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"The stakes close into a ring round the nearest pack: what is inside is shoved back in, what is outside kept out, and "
				+ "whatever leans on the stakes is cut. +150% damage.",
			"Ascension / Iron Ring", "Ascension: a pack penned where it stood, cutting itself on the bars.",
			o => { o.DisplayName = "Iron Ring"; o.VisualTag = "IronRing"; o.ModulateColor = Colors.White; o.DamageMultiplier = 2.5f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			"Once raised, the wall marches forward at a walk, shoving the pack ahead of it and cutting it as it goes, until it meets "
				+ $"water or a wall. Two more walls a cast toward other packs, each stands {stand:0.#}s longer, -50% cooldown, +30% damage.",
			"Ascension / Rolling Wall", "Ascension: the front moves, and the swarm goes with it.",
			o => { o.DisplayName = "Rolling Wall"; o.VisualTag = "RollingWall"; o.ModulateColor = Colors.White; o.DamageMultiplier = 1.3f; o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 2; o.Effect = SpellEffect.ZoneDuration; o.EffectValue = stand; }));
	}

	// Kindled Ward (scripts/KindledWard.cs): a mote of light that drifts at the nearest enemy and burns
	// what is near it. Area is its burn. Speed is how fast it drifts - still slower than you, so still
	// placed. Projectiles are wards allowed to burn at once, which it read already. Pierce means nothing
	// to a burn that takes everything within reach, so it became Zone Duration: how long a ward lasts.
	//
	// EVERY CARD ITS OWN (.ai/spell-roster.md section 2e, built 2026-10-29): a flare the first time a ward
	// meets the pack, a trail of wisps where it drifts, Rimelight's cold light (the element path), a pyre
	// (today's bigger burn), and a flock that circles you instead of drifting off. The ids, element gains
	// and tints stay the archetype's (Branch); the name is set after it. The behaviour and its numbers
	// are KindledWard's (FlareId and the rest) and WardWisps'.
	private static void SetupKindledWardEvolutions(SpellData spell)
	{
		const float longer = 3f;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"The first time a ward meets the pack it flares: a burst of light {KindledWard.FlareReachScale:0.#} times as wide as its burn, for {KindledWard.FlareDamageShare:0.#}x a strike. +20% damage.",
			"Light / Burst", "Each new ward opens with a blast where it reaches them.",
			o => { o.DisplayName = "Flare"; o.VisualTag = "Flare"; o.DamageMultiplier = 1.2f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"A drifting ward sheds wisps of its light behind it: each hangs where it was left for {KindledWard.WispSeconds:0.#}s and sears whatever walks into it for {KindledWard.WispDamageShare * 100f:0}% of a strike every half second. Wards drift 25% faster.",
			"Light / Trail", "Every ward draws a line of light the pack has to cross.",
			o => { o.DisplayName = "Wisp Trail"; o.VisualTag = "WispTrail"; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		// +80% damage, not +150%: the ward grows every level now (SpellData_KindledWard.tres), and twice
		// a burn that is already 84% wider compounds - solo at level 8 against a 30x-health crowd it
		// dealt 6,253 a second at +150% against Infinite Horizon's 2,731 (2026-10-27, seed 11). The sun
		// stays the size it was promised; the number came down. Renamed Pyre in 2e, its numbers unchanged.
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			"Every ward burns as a pyre twice as wide - a small sun drifting into the pack. +80% damage.",
			"Ascension / Pyre", "Ascension: each ward is a small sun.",
			o => { o.DisplayName = "Pyre"; o.VisualTag = "Pyre"; o.DamageMultiplier = 1.8f; o.AreaMultiplier = 2.0f; }));
		// x1.8 damage: the extra casts land where the first ones are already working (caps, spacing,
		// a knot already held), so it adds damage: x2.79 of the spell at level 7 with it (2026-10-26, --enemy-hp=30).
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"The wards no longer drift off: they circle you as a flock, each breaking off to burn whatever comes near and flying back to its place. 3 more may burn at once, each lasts {longer:0}s longer, -50% cooldown, +80% damage.",
			"Ascension / Flock", "Ascension: a ring of light that meets the pack wherever it closes on you.",
			o => { o.DisplayName = "Kindling Flock"; o.VisualTag = "KindlingFlock"; o.DamageMultiplier = 1.8f; o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.Effect = SpellEffect.ZoneDuration; o.EffectValue = longer; }));
	}

	// THE BOLTS' OWN CARDS (.ai/spell-roster.md 2e, built 2026-10-29). Riptide, Molten Shard and Hunter's
	// Draw kept the archetype's five names - each card a bolt's number on a bolt - until every card was
	// made to say, in its name and its first words, what the player SEES it do. The behaviour is the
	// spell's own script's, switched on by the card's id (BoltEvolutions); Branch() still fixes each id,
	// element gain and its price, so Tituba's path, RiptideWave.TitanEvolutionId and the ascension
	// browser carry over. Only the name, the words and the look move, through Renamed(). The numbers on a
	// card ride along: they are never the card.
	private static void Renamed(SpellEvolutionOption option, string name, string visualTag, Color tint)
	{
		option.DisplayName = name;
		option.VisualTag = visualTag;
		option.ModulateColor = tint;
	}

	// Riptide: a wave fired once down a line, piercing what it passes (ElementalBolt, drawn by RiptideWave,
	// which paints every form itself - Player.FireBoltSpell drops the cards' tint on it). A bolt's size is
	// the node's scale: the collision and the wave drawn from it grow together. Breaker and Tidal Wall are
	// Tituba's Apotheosis path, so Breaker keeps the 30% bigger wave the path was sized with (a Tidal Wall
	// about 800 across at level 8).
	private static void SetupRiptideEvolutions(SpellData spell)
	{
		Color sea = new Color(0.55f, 0.82f, 1.0f);
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"The wave throws everything it breaks over back along its run. +35% damage, and a wave 30% bigger.",
			"Water / Knockback", "Clears a lane, and shoves the pack back into the next wave's path.",
			o => { Renamed(o, "Breaker", "Breaker", sea); o.DamageMultiplier = 1.35f; o.ScaleMultiplier = 1.3f; o.KnockbackBonus = 100f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"The wave builds as it runs - it leaves your hand at {BoltEvolutions.GroundswellStart * 100f:0}% of its size and breaks at {BoltEvolutions.GroundswellEnd * 100f:0}% at the end of its run. -25% cooldown, and it runs 25% faster.",
			"Water / Distance", "Fire it long: the farther the pack, the wider the water that reaches it.",
			o => { Renamed(o, "Groundswell", "Groundswell", sea); o.CooldownMultiplier = 0.75f; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		// A TIDAL WAVE (2026-10-27, "riptide size should end up being a huge portion of the screen per
		// shot"), named for what it is since 2026-10-29: Tidal Wall. x3 of a level 8 wave (224 px) is about
		// 620 px across, 800 with Breaker's 30% - most of a phone's width and most of a PC's height. A wave
		// that size is not spent on the first ranks it meets. Its look is RiptideWave's titanic sea.
		// RISES, AND COMES SELDOM (2026-10-30, "tidal wall starts too big, it should start small and grow
		// as it moves away from you. it's also firing far too fast"): it leaves the hand a wave's size and
		// rises to that height by the end of its run (ElementalBolt.Growth), and the sea takes three times
		// as long to gather - one wall on the screen, not three. Its damage a hit is what it was: three walls
		// at once were overkilling the same pack, and one measures where three did (BoltEvolutions.TidalWall*,
		// .ai/spell-roster.md 2i).
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"The wave leaves your hand at its own size and rises as it rolls out, into a wall of water {BoltEvolutions.TidalWallScale:0} times as wide by the end of its run. +{(BoltEvolutions.TidalWallDamage - 1f) * 100f:0}% damage and it breaks through 40 more before it is spent, but the sea takes {BoltEvolutions.TidalWallCooldown:0} times as long to gather between walls.",
			"Ascension / Water", "Ascension: the sea comes in - seldom, and all at once.",
			o => { Renamed(o, "Tidal Wall", "TidalWall", sea); o.DamageMultiplier = BoltEvolutions.TidalWallDamage; o.ScaleMultiplier = BoltEvolutions.TidalWallScale; o.CooldownMultiplier = BoltEvolutions.TidalWallCooldown; o.PierceBonus = 40; }));
		// Not "Spring Tide", as 2e first proposed: that is Tituba's Apotheosis's name.
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"Three waves fan out from your hand at once, the middle one on your aim and the others {BoltEvolutions.TridentSpreadDegrees:0} degrees to either side. -{(1f - BoltEvolutions.TridentCooldown) * 100f:0}% cooldown, and each wave {(BoltEvolutions.TridentDamage - 1f) * 100f:0}% harder and tearing through 4 more.",
			"Ascension / Water", "Ascension: a wedge of sea in front of you every cast.",
			// Scaled back in the Ascension audit (2026-10-30): waves 40% bigger at half the cooldown stood two
			// fans on the screen at once (BoltEvolutions.TridentScale).
			o => { Renamed(o, "Trident", "Trident", sea); o.DamageMultiplier = BoltEvolutions.TridentDamage; o.CooldownMultiplier = BoltEvolutions.TridentCooldown; o.ProjectileCountBonus = 2; o.PierceBonus = 4; o.ScaleMultiplier = BoltEvolutions.TridentScale; }));
	}

	// Molten Shard: a homing shard at a random enemy in range that burns away a share of its health
	// (ElementalBolt). Every card is something the shard does with molten metal: it leaves slag, it
	// splits, it lodges and erupts, it dives into a pool of lava, it is forged in a ring and loosed.
	private static void SetupMoltenShardEvolutions(SpellData spell)
	{
		Color forge = new Color(1.0f, 0.78f, 0.6f);
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"The shard's flight leaves a line of burning slag for {BoltEvolutions.SlagSeconds:0.#}s, and whatever crosses it burns for {BoltEvolutions.SlagTickShare * 100f:0}% of a hit every {BoltEvolutions.SlagTickSeconds:0.#}s. +20% damage.",
			"Metal / Ground", "Shards fly through the pack, so the slag lies where they walk.",
			o => { Renamed(o, "Slag Trail", "SlagTrail", forge); o.DamageMultiplier = 1.2f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"On its first hit the shard splits in two, and each half homes on a new enemy for {BoltEvolutions.SplitShare * 100f:0}% of the hit. -20% cooldown, and shards fly 25% faster.",
			"Fire / Spread", "One shard becomes three hits on three enemies, each withered.",
			o => { Renamed(o, "Splitting Shard", "SplittingShard", forge); o.CooldownMultiplier = 0.8f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			$"The shard lodges in what it hits, glowing hotter, and erupts {BoltEvolutions.MagmaFuseSeconds:0.#}s later for {BoltEvolutions.MagmaShare * 100f:0}% of the hit on everything round it.",
			"Fire / Burst", "It stops piercing and starts bursting: best in a crowd.",
			o => { Renamed(o, "Magma Core", "MagmaCore", forge); o.DamageMultiplier = 1.1f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"The shard dives into the ground where it strikes, and a pool of lava wells up there for {BoltEvolutions.CalderaSeconds:0}s, burning everything in it for {BoltEvolutions.CalderaTickShare * 100f:0}% of a hit twice a second. +60% damage.",
			"Ascension / Fire", "Ascension: wherever a shard lands, the ground burns.",
			o => { Renamed(o, "Caldera", "Caldera", forge); o.DamageMultiplier = 1.6f; o.ScaleMultiplier = 1.5f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"Shards gather in a ring round you as they are forged, and when it holds {BoltEvolutions.ForgeVolley} they all launch at once, each at a different enemy. -50% cooldown, 4 forged a cast, +4 pierce, 40% larger.",
			"Ascension / Metal", "Ascension: a ring of molten blades, loosed together.",
			o => { Renamed(o, "Forge Swarm", "ForgeSwarm", forge); o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; o.ScaleMultiplier = 1.4f; }));
	}

	// Hunter's Draw: a volley loosed when you stop running (Player.TickHuntersDraw and LooseHuntersVolley,
	// ElementalBolt arrows). Its cooldown is the time to nock; its size is its scale. Every card is the
	// bow doing something a bow does: a heavier draw, a shot on the run, a barbed head, a volley into the
	// sky, a marked quarry.
	private static void SetupHuntersDrawEvolutions(SpellData spell)
	{
		Color fletch = new Color(0.82f, 0.98f, 1.0f);
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			"A full draw looses heavier arrows that throw back and stagger what they hit. +30% damage.",
			"Metal / Knockback", "Run the full draw before you stop, and the volley holds the line for you.",
			o => { Renamed(o, "Heavy Draw", "HeavyDraw", fletch); o.DamageMultiplier = 1.3f; o.KnockbackBonus = 150f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"While you run with the bow drawn, it looses a snap shot at the nearest enemy every few strides, for {BoltEvolutions.RunningShotShare * 100f:0}% of an arrow. The volley still comes when you stop. -15% time to nock, and arrows fly 25% faster.",
			"Wind / Movement", "The bow is never idle while you kite.",
			o => { Renamed(o, "Running Shot", "RunningShot", fletch); o.CooldownMultiplier = 0.85f; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			$"An arrow that stops in an enemy stays in it, and the wound bleeds {BoltEvolutions.BarbedTickShare * 100f:0}% of the arrow's hit every {BoltEvolutions.BarbedTickSeconds:0.#}s for {BoltEvolutions.BarbedSeconds:0}s. +10% damage.",
			"Metal / Bleed", "Three barbs in one brute bleed it three times over.",
			o => { Renamed(o, "Barbed", "Barbed", fletch); o.DamageMultiplier = 1.1f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"The volley goes straight up and comes down as a field of arrows over the pack nearest where you stopped - {BoltEvolutions.RainDropsPerArrow} for every arrow you loose. +50% damage.",
			"Ascension / Wind", "Ascension: stop, and the sky over the pack falls on it.",
			o => { Renamed(o, "Rain of Arrows", "RainOfArrows", fletch); o.DamageMultiplier = 1.5f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"A full draw marks the strongest enemy in reach - the whole volley seeks it, and for {BoltEvolutions.MarkSeconds:0}s it takes {BoltEvolutions.MarkVulnerable * 100f:0}% more from everything. -50% time to nock, +3 arrows a volley, +4 pierce, and every arrow +60% damage.",
			"Ascension / Single target", "Ascension: the brute at the back of the pack goes first.",
			o => { Renamed(o, "Hunter's Mark", "HuntersMark", fletch); o.DamageMultiplier = 1.6f; o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = 3; o.PierceBonus = 4; o.ScaleMultiplier = 1.4f; }));
	}

	// Glacial Spike (scripts/IceSpike.cs, GlacierCrown.cs, FrostPatch.cs): spikes that erupt from the
	// ground under the nearest enemy and hit what stands round them. Its five milestones wore the default
	// archetype's names until 2026-10-29 and only moved its numbers; each now changes what the ice does
	// (.ai/spell-roster.md section 2e), read by id in IceSpike and Player.FireIceSpikes. The ids are the
	// old ones, so saves and the Ascension browser carry over, and so are the element gains: Earth
	// deepens on the first card, Ice on the first Ascension, each at the hybrid's price.
	private static void SetupGlacialSpikeEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = IceSpike.ShatterId,
			DisplayName = "Shatter",
			Description = $"As each spike withdraws it bursts into a ring of {IceSpike.ShatterShards} ice splinters that fly out past it, "
				+ $"each striking the first enemy in its way for {IceSpike.ShatterShare * 100f:0}% of the spike's damage. +20% damage.",
			MilestoneLevel = 4,
			// +20%: without it the spike and its splinters measured 61% of the old Amplified Impact at level 7
			// (--enemy-hp=30), whose spikes were 30% wider.
			DamageMultiplier = 1.2f,
			BonusElementWeights = new() { { "Earth", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "Shatter",
			SynergyTag = "Earth / Splinters",
			SynergyDescription = "Deepens Earth. Every spike reaches past its own rim into the ring round it."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = IceSpike.SpikeRunId,
			DisplayName = "Spike Run",
			Description = $"The spikes stop scattering: they race along the ground across the front of the pack, one after another, "
				+ $"the middle one under the target - and {IceSpike.RunExtra} more join the run.",
			MilestoneLevel = 4,
			VisualTag = "SpikeRun",
			SynergyTag = "Ice / Line",
			SynergyDescription = "A wall of ice rising along the front rank, not a cluster round one enemy."
		});

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = IceSpike.RimefootId,
			DisplayName = "Rimefoot",
			Description = $"Every spike leaves the ground it rose from frozen for {FrostPatch.Seconds:0.#} s: whatever crosses the ice "
				+ $"is slowed {(1f - FrostPatch.SlowMultiplier) * 100f:0}%. +20% damage.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.2f,
			VisualTag = "Rimefoot",
			SynergyTag = "Ice / Slow",
			SynergyDescription = "The next spike finds them still standing on the last one's ice."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = IceSpike.GlacierCrownId,
			DisplayName = "Glacier Crown",
			Description = $"{GlacierCrown.RingSpikes} more spikes erupt in a ring round the target and stand {GlacierCrown.StandSeconds:0.#} s: "
				+ "the pack inside is penned, turned back from the ice whichever way it walks, and the crown strikes everything in "
				+ "and on it once. Every spike +150% damage.",
			MilestoneLevel = 8,
			// x2.5: measured at --enemy-hp=30 the crown at +50% made 772 a second against level 7's 896 - the
			// old Titan Cataclysm's spike three times as wide reached a whole screen, the crown a ring round one pack.
			DamageMultiplier = 2.5f,
			BonusElementWeights = new() { { "Ice", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "GlacierCrown",
			SynergyTag = "Ascension / Ice",
			SynergyDescription = "Ascension: deepens Ice. A cage of ice round the front of the swarm, for every area spell you own."
		});

		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = IceSpike.FrozenSpireId,
			DisplayName = "Frozen Spire",
			Description = $"The spike under the target rises twice as tall, reaches {(IceSpike.SpireReach - 1f) * 100f:0}% wider "
				+ $"and stands {IceSpike.SpireSeconds:0.#} s as a pillar of ice: whatever touches it is struck for "
				+ $"{IceSpike.SpireTouchShare * 100f:0}% and frozen {IceSpike.SpireFreezeSeconds:0.#} s. -40% cooldown, every spike +80% damage.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.8f,
			CooldownMultiplier = 0.6f,
			VisualTag = "FrozenSpire",
			SynergyTag = "Ascension / Pillars",
			SynergyDescription = "Ascension: pillars that stand in the swarm's way and freeze what walks into them."
		});
	}

	// Scorching Ray (scripts/ScorchingRayBeam.cs): beams at the nearest enemies, each striking one.
	// Speed is how fast a beam reaches its target. Pierce burns through the target into the enemies
	// behind it on the beam's line. Projectiles are beams, which it read already. A beam that strikes
	// one enemy has no Area, so Area is a burst where it lands - none until an evolution grows one -
	// that scorches what stands beside the target for half damage (ScorchingRayBeam.BaseSplashRadius).
	//
	// EVERY CARD ITS OWN (.ai/spell-roster.md section 2e, built 2026-10-29), from what the beam already
	// is - a thin red laser that ramps on a held target (Player.FireScorchingRay) and splashes into the
	// ground: a burst that grows with that heat, a ray that swings on when its target falls, a beam that
	// runs on to the screen's edge, a ray split three ways, a lance that sweeps. The ids, element gains
	// and tints stay the archetype's (Branch); the name is set after it. The behaviour and its numbers
	// are ScorchingRayBeam's (FlashpointId and the rest).
	private static void SetupScorchingRayEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"Every ray bursts where it strikes, scorching what stands beside its target for {ScorchingRayBeam.SplashDamageFraction * 100f:0}% of its damage, and the hotter the ray the wider the burst: a target held to full heat flashes over in a blast {ScorchingRayBeam.FlashpointHeatGrowth * 100f:0}% wider, at the ray's full damage. +20% damage.",
			"Heat / Burst", "Hold a ray on something big, and the pack round it goes up with it.",
			o => { o.DisplayName = "Flashpoint"; o.VisualTag = "Flashpoint"; o.DamageMultiplier = 1.2f; o.AreaMultiplier = 1.3f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"A ray whose target falls swings on to the nearest enemy and burns it as hot as it was, {(ScorchingRayBeam.MaxHandoffs == 1 ? "once" : $"up to {ScorchingRayBeam.MaxHandoffs} times")} a cast, and holds that heat on it into the next. Rays strike 25% faster.",
			"Heat / Chain", "The heat you built on an elite is not lost when it dies.",
			o => { o.DisplayName = "Handoff"; o.VisualTag = "Handoff"; o.SpeedMultiplier = 1.25f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.ReactiveCatalyst,
			"The beam runs on through its target to the edge of the screen, burning every enemy on its line. +15% damage.",
			"Line / Pierce", "Line them up, and one ray takes the column.",
			o => { o.DisplayName = "Burn Through"; o.VisualTag = "BurnThrough"; o.DamageMultiplier = 1.15f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"Every ray splits where it strikes into {ScorchingRayBeam.PrismSplits} rays that burn the enemies round its target, each for {ScorchingRayBeam.PrismDamageShare * 100f:0}% of the ray, its heat and all - and they stay that hot into the next cast. +60% damage.",
			"Ascension / Prism", "Ascension: one ray on the boss is four rays into its escort.",
			o => { o.DisplayName = "Prism"; o.VisualTag = "Prism"; o.DamageMultiplier = 1.6f; }));
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"Every ray widens into a lance that stays lit after it strikes and sweeps on to the next enemy, and the next, burning everything it crosses - wider the hotter it burns. -30% cooldown.",
			"Ascension / Lance", "Ascension: beams that cut across the pack instead of stopping in it.",
			o => { o.DisplayName = "Sunlance"; o.VisualTag = "Sunlance"; o.CooldownMultiplier = 0.7f; }));
	}

	// Cinderbreath (scripts/Flamethrower.cs, FlamePuff.cs): a held jet of thrown tongues of flame. Area
	// is how WIDE each tongue burns - never how far, or the jet would outrange a bolt - and it reached
	// nothing before. Speed is how fast tongues leave the nozzle. Projectiles are tongues per throw.
	// Pierce means nothing to a tongue that already burns everything it touches, so Infinite Horizon's
	// projectiles and pierce are two more tongues a throw together: three at once, tripling the jet.
	// Its cooldown is the recharge between burns. Amplified Impact and Titan Cataclysm are the
	// Pyromancer's Apotheosis path.
	//
	// EVERY CARD ITS OWN (.ai/spell-roster.md section 2e, built 2026-10-29). Each card's name and first
	// line are what the jet does differently where you can see it - a burst at the tip, embers left on
	// the ground, a longer breath and a wall of fire, three jets - and its numbers ride along. The ids,
	// the element gains and the tints stay the archetype's (Branch), so the Pyromancer's Apotheosis path
	// (Backdraft, then Dragon's Breath) and every save carry over; only the name is set here, after
	// Branch, which is why each block below writes DisplayName itself. The behaviour and its numbers
	// are Flamethrower's (BackdraftId, EmberWakeId, DragonsBreathId) and CinderGround's.
	private static void SetupCinderbreathEvolutions(SpellData spell)
	{
		const int extraTongues = 2;
		spell.Level4Options.Add(Branch(spell, DefaultBranch.AmplifiedImpact,
			$"Every burn ends in a backdraft: as the jet goes out, its fire rolls back into a burst at the tip that scorches everything round it for {Flamethrower.BackdraftDamageShare:0}x a tongue's damage. +35% damage.",
			"Power / Burst", "The jet's far end is where the pack is caught - and blown apart.",
			o => { o.DisplayName = "Backdraft"; o.VisualTag = "Backdraft"; o.DamageMultiplier = 1.35f; }));
		spell.Level4Options.Add(Branch(spell, DefaultBranch.HyperAcceleration,
			$"The jet leaves embers on the ground: every second tongue drops one where it goes out, and the cone it swept burns for {Flamethrower.EmberSeconds:0}s, scorching whatever walks in for {Flamethrower.EmberDamageShare * 100f:0}% of a tongue every half second. Tongues thrown 25% faster.",
			"Ground / Burn", "Sweep the jet across their path, and the ground they cross is on fire.",
			o => { o.DisplayName = "Ember Wake"; o.VisualTag = "EmberWake"; o.SpeedMultiplier = 1.25f; }));
		AddElementPath(spell);
		spell.Level8Options.Add(Branch(spell, DefaultBranch.TitanCataclysm,
			$"A dragon's breath: the jet reaches {(Flamethrower.DragonReach - 1f) * 100f:0}% farther with tongues twice as wide, and where each burn ends a wall of fire rises across its path and stands {Flamethrower.WallSeconds:0}s, burning whatever crosses it. +80% damage.",
			"Ascension / Breath", "Ascension: a breath that holds the line, and a wall the pack has to walk through to reach you.",
			o => { o.DisplayName = "Dragon's Breath"; o.VisualTag = "DragonsBreath"; o.DamageMultiplier = 1.8f; o.AreaMultiplier = 2.0f; }));
		// x1.9 damage: the extra casts land where the first ones are already working (caps, spacing,
		// a knot already held), so it adds damage: x2.41 of the spell at level 7 with it (2026-10-26, --enemy-hp=10).
		// The three jets each turned on their own enemy are Eric's (2026-10-28, Flamethrower.SpreadAims):
		// the proposal's Fire Fan, which is its name now.
		spell.Level8Options.Add(Branch(spell, DefaultBranch.InfiniteHorizon,
			$"It breathes {1 + extraTongues} jets at once, fanned out, each turned on its own enemy in its own direction. -50% recharge between burns, every hit +90% damage.",
			"Ascension / Fan", "Ascension: fire every way at once.",
			o => { o.DisplayName = "Fire Fan"; o.VisualTag = "FireFan"; o.DamageMultiplier = 1.9f; o.CooldownMultiplier = 0.5f; o.ProjectileCountBonus = extraTongues; }));
	}

	// Mirefoot (scripts/TrailWeaver.cs, LingeringZone.cs, MireSurface.cs): bog pools laid behind you as you
	// walk; its cooldown is how far apart they are laid. Its five milestones wore the default archetype's
	// names until 2026-10-29 and only moved its numbers; each now changes what the bog does
	// (.ai/spell-roster.md section 2e), read by id in TrailWeaver. The ids are the old ones, so saves and
	// the Ascension browser carry over, and so are the element gains: Water deepens on the first card,
	// Grass on the first Ascension, each at the hybrid's price (a longer stride between pools).
	private static void SetupMirefootEvolutions(SpellData spell)
	{
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = TrailWeaver.SuckingMudId,
			DisplayName = "Sucking Mud",
			Description = $"The first enemy to wade into each pool is held there for {TrailWeaver.MudHoldSeconds:0.#} s, the mud heaped "
				+ "round its feet, while the bog bites. +20% damage.",
			MilestoneLevel = 4,
			DamageMultiplier = 1.2f,
			BonusElementWeights = new() { { "Water", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "SuckingMud",
			SynergyTag = "Water / Hold",
			SynergyDescription = "Deepens Water. Whatever follows you closest stops dead in your footprints."
		});
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = TrailWeaver.BroadWakeId,
			DisplayName = "Broad Wake",
			Description = $"Every stride lays its pool with two smaller ones beside it, so the bog spreads three pools across behind you.",
			MilestoneLevel = 4,
			VisualTag = "BroadWake",
			SynergyTag = "Water / Width",
			SynergyDescription = "A wake the flanks cannot walk round."
		});
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = TrailWeaver.FenGasId,
			DisplayName = "Fen Gas",
			Description = $"The bog breathes: each pool lets up a cloud of marsh gas every few seconds that hangs {FenGas.Seconds:0.#} s "
				+ "and poisons whatever is under it for as much as a bite of the bog.",
			MilestoneLevel = 4,
			VisualTag = "FenGas",
			SynergyTag = "Grass / Poison",
			SynergyDescription = "The trail keeps hurting what has already waded out of it."
		});
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = TrailWeaver.GreatBogId,
			DisplayName = "Great Bog",
			Description = $"Stop, and the bog round you spreads into one marsh {TrailWeaver.MarshScale:0} pools wide that drags everything in it "
				+ $"to a crawl, and keeps welling up while you stand. Walking, the pools are 40% wider and bite 60% harder.",
			MilestoneLevel = 8,
			DamageMultiplier = 1.6f,
			AreaMultiplier = 1.4f,
			BonusElementWeights = new() { { "Grass", 1 } },
			CooldownMultiplier = AttunementGainCooldownCost,
			VisualTag = "GreatBog",
			SynergyTag = "Ascension / Grass",
			SynergyDescription = "Ascension: deepens Grass. Kite them onto the trail, then plant your feet."
		});
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = TrailWeaver.WillOWispsId,
			DisplayName = "Will-o'-Wisps",
			Description = $"Marsh lights rise out of the pools and go hunting: each flies at the nearest enemy and breaks on it for "
				+ $"{WispSwarm.Share:0.#} bites of the bog. The trail holds 5 more pools, each lingering 1.5 s longer, laid 30% closer. Every bite +50% damage.",
			MilestoneLevel = 8,
			// +50%: without it the wisps and the longer trail measured x1.57 of level 7 at --enemy-hp=30 (seed 13),
			// and x1.88-1.95 at +40% (seeds 13, 12) - under the floor an Ascension has to clear (the old
			// Infinite Horizon was x1.34).
			DamageMultiplier = 1.5f,
			CooldownMultiplier = 0.7f,
			ProjectileCountBonus = 5,
			Effect = SpellEffect.ZoneDuration,
			EffectValue = 1.5f,
			VisualTag = "WillOWisps",
			SynergyTag = "Ascension / Wisps",
			SynergyDescription = "Ascension: the trail reaches out for what will not walk into it."
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
				"A drifting light that chills what it burns."),
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
	// rain cannot read. Pure Light, so level 4 deepens Light (Shooting Stars) or branches into Ice
	// (Frostfall, the element path), with an untagged third (Skipping Stars); level 8 deepens Light
	// (Starburst - the stars explode) or takes no tag (Constellation - the stars link). Every card says
	// what changes on screen (section 2h, phase 2 - the level 4 pair were Clear Night's +2 stars and
	// Downpour's faster, finer rain, numbers only). Every number on these cards is one Starfall reads:
	// the count through GetProjectileCountAtLevel, the rate through the cooldown, the landing through
	// GetAreaMultiplierAtLevel, the damage through GetDamageAtLevel, and the four behaviours through
	// HasEvolution. No ScaleMultiplier or SpeedMultiplier: the stars are drawn out of the radius they
	// hit.
	// "third", "fourth": a card that names which of a stream it changes. Words, so a tune of the count
	// rewrites the card.
	private static string OrdinalWord(int n) => n switch
	{
		2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth",
		_ => $"{n}th",
	};

	private static string TimesWord(int n) => n switch { 1 => "once", 2 => "twice", 3 => "three times", _ => $"{n} times" };

	private static void SetupStarfallEvolutions(SpellData spell)
	{
		// Every percentage on these cards is computed from the number the option carries, so a tune can
		// never leave a card promising what the spell no longer does.
		const float burstDamage = 2.1f;
		const float burstArea = 2.2f;
		const float burstCooldown = 2.0f;
		const int constellationStars = 1;

		// Level 4 (3 choices)
		// A streak covers about three and a half landings' ground, so one star in three streaking is most of
		// Clear Night's +2 stars on its own: with full-damage streaks and +15% it measured 47-77 a second at
		// level 4 against Clear Night's 36 the same day. At 80% and no bonus, 44 at level 4 but 353 at level 8
		// against Clear Night's 411 - a streak is never a heavy star, so it feeds Starburst less. +10%.
		const float shootingDamage = 1.1f;
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Starfall.ShootingStarsId,
			DisplayName = "Shooting Stars",
			Description = $"Every {OrdinalWord(Starfall.ShootingEvery)} star streaks in low across the whole rain instead of falling: "
				+ $"a line of light from rim to rim that strikes everything it crosses for {Starfall.ShootingDamageFraction * 100f:0}% of a star. "
				+ $"+{(shootingDamage - 1f) * 100f:0}% damage, +1 Light tag.",
			MilestoneLevel = 4,
			DamageMultiplier = shootingDamage,
			BonusElementWeights = new() { { "Light", 1 } },
			VisualTag = "ShootingStars",
			SynergyTag = "Light / Line",
			SynergyDescription = "Deepens Light. Each streak crosses the whole rain, so a crowd anywhere under it is cut."
		});

		// The element path. Ice for the fiction (the cold light of stars) and because Ice has only three
		// spell carriers. Starfall repaints every star in Ice's ramp; the slow is the rider.
		spell.Level4Options.Add(ElementPathOption(spell, "frostfall", "Frostfall", Element.Ice, 0.70f, 1.5f,
			"The stars fall cold",
			"Everything standing in the rain is dragged to a crawl."));

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Starfall.SkippingStarsId,
			DisplayName = "Skipping Stars",
			Description = $"Every star lands at its slant and skips on {TimesWord(Starfall.SkipCount)}, like a stone across water: "
				+ $"the first skip lands {Starfall.SkipDamage[0] * 100f:0}% as hard and {Starfall.SkipRadius[0] * 100f:0}% as wide, "
				+ $"the second {Starfall.SkipDamage[1] * 100f:0}% and {Starfall.SkipRadius[1] * 100f:0}%.",
			MilestoneLevel = 4,
			VisualTag = "SkippingStars",
			SynergyTag = "Reach / Coverage",
			SynergyDescription = "No new element. Every star crosses ground instead of landing on one spot."
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
				+ $"{Starfall.LinkDamageFraction * 100f:0}% of a star's damage. +{constellationStars} star every cloudburst, and every star hits twice as hard.",
			MilestoneLevel = 8,
			ProjectileCountBonus = constellationStars,
			// x2: x3.2 of the spell at level 7 with it (2026-10-26, --enemy-hp=10) - the lines hit, but
			// at a fraction of a star, and an Ascension doubles its spell.
			DamageMultiplier = 2.0f,
			VisualTag = "Constellation",
			SynergyTag = "Ascension / Zone",
			SynergyDescription = "Ascension: the rain draws a map of itself on the ground, and the map cuts."
		});
	}

	// THE FAMILIAR: one block per FORM (SpellData.Variants; .ai/decisions/2026-10-28-spell-variants.md), as
	// Spiritual Weapon's are. Eric: "when you FIRST take familiar, you should be able to select your familiar.
	// each has it's own advantages - crow moves faster, cat gives passive bonuses, dog does the most direct
	// damage. they each have their own mutations/ascensions". Every milestone id starts with its form's id
	// (Familiar.cs names them all), so each has its own icon, its own seen-ascension record and its own row
	// of readers (EvolutionStatReaders, by form). A template with no form chosen carries the dog's block,
	// the default form.
	//
	// Every form is pure Arcane, so each level 4 offers one branch that deepens Arcane and one element path
	// (ValidateElementPaths) - the dog's into Ice, the crow's into Lightning, the cat's into Darkness - and
	// each level 8 one that deepens Arcane and one that takes no tag. Every form's look is its own art
	// (tools/art/familiar.py), and after an element path its Ascensions are drawn in that element. Every
	// number on these cards is one the familiar reads: the strike through GetDamageAtLevel, the jaws (or
	// beak, or claws) through GetAreaMultiplierAtLevel, the count through GetProjectileCountAtLevel, the
	// strike rate through the cooldown, and every verb by id (HasEvolution) with its numbers in Familiar's
	// own constants. No ScaleMultiplier: each look is drawn at its own size.
	private static void SetupFamiliarEvolutions(SpellData spell)
	{
		switch (Familiar.FormOf(spell))
		{
			case Familiar.Kind.Crow:
				SetupFamiliarCrowEvolutions(spell);
				break;
			case Familiar.Kind.Cat:
				SetupFamiliarCatEvolutions(spell);
				break;
			default:
				SetupFamiliarDogEvolutions(spell);
				break;
		}
	}

	// THE DOG: the hound pack the Familiar was before it had forms, and the most direct damage of the three.
	// Its line is the old one - Great Hound, the Rimehound, Littermates, Cerberus - under the dog's own ids
	// (the save's record of the old Cerberus still reads as this one: Familiar.RenamedMilestones). The Griffin
	// went to the crow, the form that flies; in its place the dog takes the Hound of Annwn, which turns the
	// pack on the strongest thing in reach - the dog's damage, pointed at what needs it most.
	private static void SetupFamiliarDogEvolutions(SpellData spell)
	{
		// Measured with the forms (2026-10-28, .ai/spell-roster.md 2g, the roster's real-time method). The
		// Great Hound is +20% and 40% wider (was +35% and 60%): with it the dog's levels 4-6 already sit with
		// the Spiritual Weapon axe's, the top of the roster (91-136 a second against 104-156). Cerberus is
		// +10% (was +35%): Great Hound + Cerberus measured 599 a second at level 8 against the band's 331-465
		// and the axe's 475; at +10%, 480 - the dog the most damage of the three forms, at the band's top.
		const float greatDamage = 1.2f;
		const float greatJaws = 1.4f;
		const int littermates = 1;
		const float litterDamage = 0.85f;
		const float cerberusDamage = 1.1f;
		const float annwnDamage = 2.2f;
		const float annwnJaws = 1.5f;

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
		SpellEvolutionOption rime = ElementPathOption(spell, "dog_rimehound", "Rimehound", Element.Ice, 0.70f, 1.5f,
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
			Description = $"Three heads: every bite is {Familiar.CerberusHeads} - its quarry and the two enemies close beside it "
				+ $"(farther with Area) - for +{(cerberusDamage - 1f) * 100f:0}% damage. +1 Arcane tag.",
			MilestoneLevel = 8,
			DamageMultiplier = cerberusDamage,
			BonusElementWeights = new() { { "Arcane", 1 } },
			VisualTag = "Cerberus",
			SynergyTag = "Ascension / Arcane",
			SynergyDescription = "Ascension: deepens Arcane. Three jaws on every leap."
		});

		// Annwn's hounds are the Welsh otherworld's: white, red-eared, and they hunt what is worth hunting.
		// Familiar.Acquire turns the pack on an elite or a boss near the wizard before anything else, and
		// Familiar.Strike lands a bite on one AnnwnEliteMultiplier times over. (It first hunted the most
		// health anywhere in reach and spent its bites running there - a third of Cerberus's damage. Now it
		// guards as the dog does: with the Great Hound it measured 478 a second at level 8 in real time,
		// Cerberus 480; with Littermates 360. .ai/spell-roster.md 2g.)
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.AnnwnId,
			DisplayName = "Hound of Annwn",
			Description = $"It becomes a white hound of the otherworld, red-eared: every bite lands {(annwnDamage - 1f) * 100f:0}% harder "
				+ $"with jaws {(annwnJaws - 1f) * 100f:0}% wider, it goes for an elite or a boss first when one is near you, "
				+ $"and a bite on one lands {Familiar.AnnwnEliteMultiplier:0.#} times over.",
			MilestoneLevel = 8,
			DamageMultiplier = annwnDamage,
			AreaMultiplier = annwnJaws,
			VisualTag = "HoundOfAnnwn",
			SynergyTag = "Ascension / Hunt",
			SynergyDescription = "Ascension: no new element. The pack goes for the biggest thing on the field."
		});
	}

	// THE CROW: the fastest of the three. Less in a peck than a hound puts in a bite, and a shorter cooldown
	// to make it up; it flies, ranges half again as far, goes for what shoots first, and is back on the next
	// quarry before a hound has turned. Its milestones lean on that: a heavier bird, a storm in its feathers,
	// more of them - then the battle-crow that finishes what is already falling, or the Griffin (the dog's,
	// before forms), whose swoop strikes a whole line.
	private static void SetupFamiliarCrowEvolutions(SpellData spell)
	{
		const float ravenDamage = 1.3f;
		const float ravenBeak = 1.8f;
		const int murder = 1;
		const float murderDamage = 0.9f;
		// +100% (2026-10-28): at +200% Raven + Morrigan measured 585 a second at level 8, a quarter over the
		// band; at +100%, 380, under the dog's 480 (.ai/spell-roster.md 2g).
		const float morriganDamage = 2.0f;
		const float griffinDamage = 1.2f;
		const float griffinCooldown = 1.3f;

		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.RavenId,
			DisplayName = "Raven",
			Description = $"It grows into a raven of your light: +{(ravenDamage - 1f) * 100f:0}% damage, and its beak "
				+ $"strikes {(ravenBeak - 1f) * 100f:0}% wider, taking what stands beside its quarry. +1 Arcane tag.",
			MilestoneLevel = 4,
			DamageMultiplier = ravenDamage,
			AreaMultiplier = ravenBeak,
			BonusElementWeights = new() { { "Arcane", 1 } },
			VisualTag = "Raven",
			SynergyTag = "Arcane / Size",
			SynergyDescription = "Deepens Arcane. A heavier bird that pecks the knot."
		});

		// The element path. Lightning because a crow out of a thunderhead is the oldest weather-omen there
		// is, and because a stagger on every peck suits the form that strikes most often: a crow that never
		// lets a quarry finish a step. The flock repaints itself storm-grey and lit; the stagger is the rider.
		spell.Level4Options.Add(ElementPathOption(spell, "crow_stormcrow", "Stormcrow", Element.Lightning, 0f, 0.2f,
			"The crow flies out of the storm",
			"Every peck staggers what it strikes."));

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.MurderId,
			DisplayName = "Murder of Crows",
			Description = $"The flock grows: +{murder} crow, each with its own quarry; every crow pecks "
				+ $"{(1f - murderDamage) * 100f:0}% softer.",
			MilestoneLevel = 4,
			ProjectileCountBonus = murder,
			DamageMultiplier = murderDamage,
			VisualTag = "MurderOfCrows",
			SynergyTag = "Flock / Count",
			SynergyDescription = "No new element. More pecks, spread across the crowd."
		});

		// Level 8 (2 choices)
		// The Morrigan is the Irish battle-crow who chooses who falls. Familiar.Strike reads her by id: a peck
		// on an ordinary enemy at or under MorriganExecuteFraction of its health is the rest of it, and on an
		// elite or a boss there lands MorriganEliteMultiplier times over.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.MorriganId,
			DisplayName = "The Morrigan",
			Description = $"It becomes the battle-crow that chooses who falls: a peck on an enemy under "
				+ $"{Familiar.MorriganExecuteFraction * 100f:0}% of its health kills it outright, and on an elite or a "
				+ $"boss there lands {Familiar.MorriganEliteMultiplier:0.#} times over. +{(morriganDamage - 1f) * 100f:0}% damage, "
				+ "+1 Arcane tag.",
			MilestoneLevel = 8,
			DamageMultiplier = morriganDamage,
			BonusElementWeights = new() { { "Arcane", 1 } },
			VisualTag = "TheMorrigan",
			SynergyTag = "Ascension / Arcane",
			SynergyDescription = "Ascension: deepens Arcane. Everything else wounds; the crows finish."
		});

		// The Griffin was the hound pack's Ascension; it is the crow's now, the form that already flies.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.GriffinId,
			DisplayName = "Griffin",
			Description = $"It grows into a griffin: a swoop through its quarry and on past it, striking everything along its line "
				+ $"(a wider line with Area) for +{(griffinDamage - 1f) * 100f:0}% damage. Swoops come "
				+ $"{(griffinCooldown - 1f) * 100f:0}% less often than pecks.",
			MilestoneLevel = 8,
			DamageMultiplier = griffinDamage,
			CooldownMultiplier = griffinCooldown,
			VisualTag = "Griffin",
			SynergyTag = "Ascension / Line",
			SynergyDescription = "Ascension: no new element. Every strike is a line."
		});
	}

	// THE CAT: the weakest attack and the most help. Its own swipe is a fraction of a bite; its worth is what
	// it grants while it is out - every spell casting faster, and more hits slipped - which its levels raise
	// (the Cast speed and Evasion lines in SpellData_Familiar.tres, read through Familiar.GrantsOf). Its
	// milestones raise those grants, mark what it claws for every other spell, mend the wizard, and at the
	// last either make the cat the great beast of the tale or have it give a life for his.
	private static void SetupFamiliarCatEvolutions(SpellData spell)
	{
		const float palugDamage = 1.5f;
		const float palugClaws = 1.6f;

		// Level 4 (3 choices)
		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.GrimalkinId,
			DisplayName = "Grimalkin",
			Description = $"It grows old and wise, a witch's cat: everything it grants you is "
				+ $"{(Familiar.GrimalkinGrants - 1f) * 100f:0}% stronger. +1 Arcane tag.",
			MilestoneLevel = 4,
			BonusElementWeights = new() { { "Arcane", 1 } },
			VisualTag = "Grimalkin",
			SynergyTag = "Arcane / Grants",
			SynergyDescription = "Deepens Arcane. More cast speed and more evasion, for every spell you hold."
		});

		// The element path. Darkness because the Cait Sith is the fairy cat, black with a white star on its
		// breast; and because Darkness's rider is Vulnerable - what the cat claws takes more from every
		// spell the wizard owns, which is the cat's way of fighting: through his. It repaints itself black.
		spell.Level4Options.Add(ElementPathOption(spell, "cat_cait_sith", "Cait Sith", Element.Darkness, 0.25f, 3f,
			"The cat comes back from the fairy hills",
			"What it claws, every spell you own hits harder."));

		spell.Level4Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.PurrId,
			DisplayName = "Purr",
			Description = $"It purrs at your heel: you mend {Familiar.PurrRegenPerSecond:0.#} HP a second while it is out.",
			MilestoneLevel = 4,
			VisualTag = "Purr",
			SynergyTag = "Grants / Healing",
			SynergyDescription = "No new element. Health back for standing your ground."
		});

		// Level 8 (2 choices)
		// Cath Palug, the monstrous cat of the Welsh tale that Kay went out to fight. Its grants are read by id
		// (Familiar.GrantsOf, CathPalugGrants); its swipe is the claws and damage below.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.CathPalugId,
			DisplayName = "Cath Palug",
			Description = $"It grows into the monstrous cat of the old tale: everything it grants you is "
				+ $"{(Familiar.CathPalugGrants - 1f) * 100f:0}% stronger again, and its swipes tear "
				+ $"{(palugClaws - 1f) * 100f:0}% wider and land {(palugDamage - 1f) * 100f:0}% harder. +1 Arcane tag.",
			MilestoneLevel = 8,
			DamageMultiplier = palugDamage,
			AreaMultiplier = palugClaws,
			BonusElementWeights = new() { { "Arcane", 1 } },
			VisualTag = "CathPalug",
			SynergyTag = "Ascension / Arcane",
			SynergyDescription = "Ascension: deepens Arcane. The most the cat can give every other spell."
		});

		// One more life, given once, the moment it is taken (Player.FireFamiliar), spent the ordinary way.
		spell.Level8Options.Add(new SpellEvolutionOption
		{
			Id = Familiar.NineLivesId,
			DisplayName = "Nine Lives",
			Description = "The cat gives one of its lives for yours: +1 life. When you would fall, you stand back up with half your health.",
			MilestoneLevel = 8,
			VisualTag = "NineLives",
			SynergyTag = "Ascension / Lives",
			SynergyDescription = "Ascension: no new element. A run that does not end the first time."
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
				+ $"far end that hits everything around it (a wider clap with Area) for "
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
			// A percentage of the rune's own flight, never a pixel count (2026-10-28, "never mention px").
			Description = $"Runes fly {(farWindSpeed - 1f) * 100f:0}% faster and "
				+ $"{(spell.BaseRange > 0f ? farWindRange / spell.BaseRange * 100f : 0f):0}% farther.",
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
			// Only a rune that reaches the end of its flight turns (SleetRunes), with its pierce restored; a
			// rune spent in a body breaks there. The card said every rune (2026-10-26).
			Description = "A rune that reaches the end of its flight is turned back to you by the wind, and "
				+ $"pierces again on the way home. +{(galeDamage - 1f) * 100f:0}% damage, +1 Wind tag.",
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
