using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>A SpellEvolutionOption field, or a group of them that one reader always takes together.</summary>
[Flags]
public enum EvolutionStat
{
	None = 0,
	Damage = 1 << 0,        // DamageBonus, DamageMultiplier
	Cooldown = 1 << 1,      // CooldownBonus, CooldownMultiplier
	Projectiles = 1 << 2,   // ProjectileCountBonus
	Range = 1 << 3,         // RangeBonus
	Area = 1 << 4,          // AreaMultiplier
	Pierce = 1 << 5,        // PierceBonus
	ChainArcs = 1 << 6,     // ChainArcBonus
	ChainBranches = 1 << 7, // ChainBranchBonus
	Knockback = 1 << 8,     // KnockbackBonus
	Slow = 1 << 9,          // SlowMagnitudeBonus
	PoisonTick = 1 << 10,   // PoisonTickBonus
	Speed = 1 << 11,        // SpeedMultiplier
	Scale = 1 << 12,        // ScaleMultiplier
	Rider = 1 << 13,        // Rider, RiderMagnitude, RiderSeconds
	// Effect and EffectValue. Which effects a spell reads is a list of its own (SpellStatReaders.Effects);
	// this flag only names the field in a report and in KnownUnbuilt.
	Effect = 1 << 14,
}

/// <summary>What one spell's script reads of the options its milestones grant, and where.</summary>
public sealed class SpellStatReaders
{
	private readonly SpellEffect[] effects;

	public SpellStatReaders(EvolutionStat stats, string readIn, SpellEffect[] effects)
	{
		Stats = stats;
		ReadIn = readIn;
		this.effects = effects ?? Array.Empty<SpellEffect>();
	}

	public EvolutionStat Stats { get; }
	public string ReadIn { get; }
	public IReadOnlyList<SpellEffect> Effects => effects;

	public bool ReadsEffect(SpellEffect effect) => Array.IndexOf(effects, effect) >= 0;
}

/// <summary>A number a card promises that its spell never built, kept visible until it is.</summary>
public readonly record struct UnbuiltPromise(string OptionId, EvolutionStat Stat, SpellEffect Effect, string Promise);

/// <summary>
/// Which SpellEvolutionOption fields each spell's script actually reads, so that every number on a
/// level 4, level 8 or Apotheosis card is one that reaches the spell (RegressionChecks.ValidateEvolutionReaders).
/// </summary>
/// <remarks>
/// Why it exists: thirteen spells shared one set of default evolutions written for a bolt, and a
/// trap, a wall, a plague, a ward and a flamethrower all offered "+25% projectile speed" and "pierce 2
/// more enemies" that nothing they ran ever read. Nothing could see it - a field nobody reads is a
/// field set, which compiles and loads - so it took a player to ask "isn't it an aoe? does that even
/// make sense?" (2026-10-24). A table cannot read C#, so this one is written by hand from the scripts,
/// and each entry says where its reads are. **A new spell needs an entry here** (the validator says so
/// until it has one), and a spell whose script starts or stops reading a field updates its row.
///
/// What "reads" means, field by field:
///   - Damage, Cooldown: the spell's damage and the interval its cooldown decides. Frost Shard casts
///     nothing on its cooldown and Arcane Explosion is charge-driven, so neither reads Cooldown.
///   - Area: SpellData.GetAreaMultiplierAtLevel reaches what is hit. The player's own Area is not this.
///   - Speed: SpellData.GetSpeedMultiplier reaches the spell, as whatever "faster" is for it.
///   - Scale: an evolution's ScaleMultiplier reaches what the player sees, because the spell's node
///     keeps the scale Player.ApplyLegendaryVisual gives it. A spell that draws its own hit radius drops
///     the scale (Bramble Seed's lesson), and a persistent one is only ever scaled once, when it is made
///     at level 1, before any evolution exists - neither reads Scale.
///   - Rider: the spell's hits go through Player.DealDamageToEnemy with itself as the source, which is
///     where an element path's rider (and a per-spell CritDamage) applies.
///   - Effects: the SpellEffect values the spell asks its SpellData for (GetEffectValueAtLevel,
///     HasEffectFlag), plus CritDamage for every spell that passes its source.
/// </remarks>
public static class EvolutionStatReaders
{
	private const EvolutionStat Core = EvolutionStat.Damage | EvolutionStat.Cooldown;

	// ElementalBolt's reads (RefreshComputedStats, ResolveImpact), for the five spells that fly on it.
	private const EvolutionStat Bolt = Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Pierce
		| EvolutionStat.Speed | EvolutionStat.Scale | EvolutionStat.Rider;
	private static readonly SpellEffect[] BoltEffects =
	{
		SpellEffect.CritChance, SpellEffect.ProjectileSpeed, SpellEffect.Pierce, SpellEffect.AreaSize,
		SpellEffect.SlowPower, SpellEffect.SlowDuration, SpellEffect.DotDamage, SpellEffect.ZoneDuration,
		SpellEffect.CritDamage,
	};

	// ChainLightning.cs, which Shadow Bolt's scene runs too.
	// Area is the jump (ChainLightning.SpawnBranches), since 2026-10-26.
	private const EvolutionStat Chain = Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.ChainArcs
		| EvolutionStat.ChainBranches | EvolutionStat.PoisonTick | EvolutionStat.Scale | EvolutionStat.Rider;
	private static readonly SpellEffect[] ChainEffects =
	{
		SpellEffect.CritChance, SpellEffect.SlowPower, SpellEffect.Vulnerability, SpellEffect.CritDamage,
	};

	private static SpellStatReaders R(EvolutionStat stats, string readIn, params SpellEffect[] effects) => new(stats, readIn, effects);

	private static readonly Dictionary<string, SpellStatReaders> BySpell = new(StringComparer.OrdinalIgnoreCase)
	{
		["magic_missile"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Pierce
				| EvolutionStat.Scale | EvolutionStat.Rider,
			"MagicMissile.RefreshComputedStats and the missile loop in Player._PhysicsProcess",
			SpellEffect.ProjectileSpeed, SpellEffect.AreaSize, SpellEffect.ExplosionOnHit, SpellEffect.SplitOnHit,
			SpellEffect.Pierce, SpellEffect.VortexPull, SpellEffect.CritDamage),
		["arcane_explosion"] = R(EvolutionStat.Damage | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Knockback | EvolutionStat.Rider,
			"ArcaneExplosion.RefreshComputedStats, TriggerExplosion and OpenLingeringRift; charge-driven and persistent, so no cooldown and no visual scale",
			SpellEffect.VortexPull, SpellEffect.Knockback, SpellEffect.Freeze, SpellEffect.ZoneDuration, SpellEffect.CritDamage),
		["spiritual_weapon"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Speed | EvolutionStat.Scale
				| EvolutionStat.Rider,
			"SpiritualWeapon.RefreshComputedStats and Siphon (its size is the node's scale, so an evolution's Area would double it)",
			SpellEffect.ProjectileSpeed, SpellEffect.AreaSize, SpellEffect.Lifesteal, SpellEffect.CritDamage),
		["fireball"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.Scale | EvolutionStat.Rider,
			"Fireball.RefreshComputedStats, LeaveMagmaPool and ScatterClusterBombs (one ball; projectiles past the first are bomblets)",
			SpellEffect.AreaSize, SpellEffect.Burn, SpellEffect.CritDamage),
		["frost_shard"] = R(EvolutionStat.Damage | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Slow
				| EvolutionStat.Rider,
			"Player.OnEnemyDiedAt and FlingShatterShards: a shatter on every death, so it casts nothing on its cooldown (projectiles past the first are flung shards)",
			SpellEffect.Freeze, SpellEffect.CritDamage),
		["chain_lightning"] = R(Chain, "ChainLightning.cs", ChainEffects),
		["shadow_bolt"] = R(Chain, "ChainLightning.cs, which ShadowBolt.tscn runs", ChainEffects),
		["gale_blade"] = R(Bolt, "ElementalBolt (Returning)", BoltEffects),
		["riptide"] = R(Bolt, "ElementalBolt, RiptideWave and Player.FireBoltSpell", BoltEffects),
		["molten_shard"] = R(Bolt, "ElementalBolt and Player.FireBoltSpell", BoltEffects),
		["hunters_draw"] = R(Bolt, "ElementalBolt and Player.LooseHuntersVolley", BoltEffects),
		["aegis_ward"] = R(Core,
			"the aegis_ward branch of Player._PhysicsProcess: damage is the shield, cooldown the recharge; Player.CrystalShellBurst on a break"),
		["void_lance"] = R(Core | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Scale,
			"VoidLance.RefreshComputedStats, PullOntoLance and CollapseAtTerminus (hit at its drawn scale; Area is the collapse; its hits pass no source, so no rider)",
			SpellEffect.ZoneDuration, SpellEffect.VortexPull),
		["cyclone_slash"] = R(EvolutionStat.Damage | EvolutionStat.Projectiles | EvolutionStat.Area | EvolutionStat.Speed | EvolutionStat.Rider,
			"OrbitingBlade.RefreshComputedStats and PullTowardRing; persistent, so its cooldown only refreshes it and it is never re-scaled",
			SpellEffect.VortexPull, SpellEffect.CritDamage),
		["solar_flare"] = R(Core | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Knockback | EvolutionStat.Rider,
			"ElementalPulse.RefreshComputedStats and TriggerPulse (Area is the flare's reach, Knockback Blinding Flash's shove)",
			SpellEffect.CritDamage),
		["black_tentacles"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Rider,
			"BlackTentacles.cs (the rift is drawn at the radius it slows, so no visual scale)",
			SpellEffect.AreaSize, SpellEffect.ZoneDuration, SpellEffect.SlowPower, SpellEffect.SlowDuration,
			SpellEffect.RootDuration, SpellEffect.Lifesteal, SpellEffect.VortexPull, SpellEffect.CritDamage),
		["thorn_vine"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Pierce
				| EvolutionStat.Speed | EvolutionStat.Rider,
			"ThornVine.cs and Player.FireThornVine (drawn out of its reach, so no visual scale)",
			SpellEffect.AreaSize, SpellEffect.DotDamage, SpellEffect.ZoneDuration, SpellEffect.Pierce, SpellEffect.CritDamage),
		["cone_of_cold"] = R(Core | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Knockback | EvolutionStat.Rider,
			"ConeBlast.cs and Player.FireConeBlast (which drops the visual scale)",
			SpellEffect.SlowPower, SpellEffect.SlowDuration, SpellEffect.Freeze, SpellEffect.CritDamage),
		["obsidian_spike"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Scale
				| EvolutionStat.Rider,
			"GroundSpike.cs and Player.FireGroundSpike (its scale is the sprite's size, kept in step with Area)",
			SpellEffect.CritDamage),
		["meteor_swarm"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Knockback | EvolutionStat.Rider,
			"MeteorImpact.cs and Player.FireMeteorSwarm (the volley is the projectile count plus Amount; drops the visual scale)",
			SpellEffect.Burn, SpellEffect.CritDamage),
		["starfall"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Rider,
			"Starfall.Cloudburst and the fire loop (the stars a cloudburst lets fall are the projectile count; Area sizes each star, never the rain)",
			SpellEffect.AreaSize, SpellEffect.CritDamage),
		["familiar"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Rider,
			"Familiar.Summon and its bites (the hounds are the projectile count; Area sizes the jaws, a Cerberus's reach and a swoop's swath, never the hunt)",
			SpellEffect.CritDamage),
		// The facing family (.ai/spell-roster.md). Neither reads Scale: the arc is drawn out of the band
		// it sweeps, and the rune sheet is drawn at one size.
		["stormsweep"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.Rider,
			"Stormsweep.Cast and the fire loop (sweeps are the projectile count; Range is the arc's length, Area its length and band, Speed its tempo)",
			SpellEffect.AreaSize, SpellEffect.CritDamage),
		["sleet_runes"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Pierce | EvolutionStat.Speed
				| EvolutionStat.Rider,
			"SleetRunes.Cast and the fire loop (runes are the projectile count; Range is how far one flies, Speed scales its speed)",
			SpellEffect.ProjectileSpeed, SpellEffect.Pierce, SpellEffect.CritDamage),
		["toxic_spore_burst"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.PoisonTick | EvolutionStat.Rider,
			"SporeMushroom.For and Player.FireSporeMushrooms (drops the visual scale)",
			SpellEffect.AreaSize, SpellEffect.DotDamage, SpellEffect.CritDamage),

		// The thirteen that left the default archetype (SpellEvolutionCatalog, "The default archetype,
		// spell by spell"). None reads Scale: each draws itself out of the radius it hits.
		["gravewell"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.Rider,
			"GravewellTrap.For and Player.FireGravewellTrap",
			SpellEffect.AreaSize, SpellEffect.RootDuration, SpellEffect.CritDamage),
		["hollow_star"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.Rider,
			"Player.FireHollowStar and OpenHollowStar, HollowStar.Collapse",
			SpellEffect.AreaSize, SpellEffect.ZoneDuration, SpellEffect.RootDuration, SpellEffect.CritDamage),
		["bramble_seed"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Area | EvolutionStat.Speed | EvolutionStat.Slow
				| EvolutionStat.Rider,
			"Player.FireBrambleSeed and BrambleSeed.CapFor (planted at the feet, so no range)",
			SpellEffect.AreaSize, SpellEffect.ZoneDuration, SpellEffect.CritDamage),
		["contagion"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Pierce
				| EvolutionStat.Speed | EvolutionStat.Rider,
			"Player.FireContagion and SpreadContagion, Contagion.Infect (pierce is its MaxInfected)",
			SpellEffect.AreaSize, SpellEffect.ZoneDuration, SpellEffect.Pierce, SpellEffect.CritDamage),
		["iron_palisade"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.Rider,
			"Player.FireIronPalisade and RaiseIronPalisade",
			SpellEffect.AreaSize, SpellEffect.ZoneDuration, SpellEffect.CritDamage),
		["kindled_ward"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Area | EvolutionStat.Speed | EvolutionStat.Rider,
			"Player.FireKindledWard (it has no reach to cast within)",
			SpellEffect.ZoneDuration, SpellEffect.CritDamage),
		["glacial_spike"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.Rider,
			"IceSpike.RefreshComputedStats and Player.FireIceSpikes",
			SpellEffect.AreaSize, SpellEffect.RootDuration, SpellEffect.CritDamage),
		["scorching_ray"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Pierce
				| EvolutionStat.Speed | EvolutionStat.Rider,
			"ScorchingRayBeam.RefreshComputedStats and Player.FireScorchingRay",
			SpellEffect.CritChance, SpellEffect.Pierce, SpellEffect.CritDamage),
		["cinderbreath"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Range | EvolutionStat.Area | EvolutionStat.Speed
				| EvolutionStat.Rider,
			"Flamethrower.RefreshComputedStats and FlamePuff (area is the width of a tongue, never its reach)",
			SpellEffect.CritDamage),
		["mirefoot"] = R(Core | EvolutionStat.Projectiles | EvolutionStat.Area | EvolutionStat.Speed | EvolutionStat.Slow
				| EvolutionStat.Rider,
			"TrailWeaver.DropPool and PoolCap (its cooldown is the pools' spacing; it has no reach)",
			SpellEffect.ZoneDuration, SpellEffect.CritDamage),
	};

	public static bool TryGet(string spellId, out SpellStatReaders readers) =>
		BySpell.TryGetValue(spellId ?? string.Empty, out readers);

	/// <summary>
	/// Every card below promises something its spell does not do, because the mechanic was never built:
	/// a burn, a freeze, a pull, an area or a count the spell has no reader for. They predate the reader
	/// check, every one is on a hand-written block, and each is a decision - build it, or rewrite the card
	/// to drop it - rather than a typo, so they are listed rather than deleted. This is the backlog, not
	/// an allowlist to grow: a new option has to set only what its spell reads, and an entry here that
	/// comes true (the spell starts reading it, or the option stops setting it) is reported as stale so
	/// the list only ever shrinks. Fields no card mentioned were simply removed instead.
	/// </summary>
	public static readonly UnbuiltPromise[] KnownUnbuilt =
	{
		// Empty since 2026-10-26: the last 46 were built or their cards rewritten to say what they pay.
	};

	private static UnbuiltPromise U(string optionId, EvolutionStat stat, string promise) =>
		new(optionId, stat, SpellEffect.None, promise);

	private static UnbuiltPromise U(string optionId, SpellEffect effect, string promise) =>
		new(optionId, EvolutionStat.Effect, effect, promise);

	/// <summary>The fields an option sets to something other than "no change".</summary>
	public static EvolutionStat StatsSetBy(SpellEvolutionOption option)
	{
		EvolutionStat set = EvolutionStat.None;
		if (option == null)
			return set;
		if (option.DamageBonus != 0 || !Same(option.DamageMultiplier, 1f)) set |= EvolutionStat.Damage;
		if (!Same(option.CooldownBonus, 0f) || !Same(option.CooldownMultiplier, 1f)) set |= EvolutionStat.Cooldown;
		if (option.ProjectileCountBonus != 0) set |= EvolutionStat.Projectiles;
		if (!Same(option.RangeBonus, 0f)) set |= EvolutionStat.Range;
		if (!Same(option.AreaMultiplier, 1f)) set |= EvolutionStat.Area;
		if (option.PierceBonus != 0) set |= EvolutionStat.Pierce;
		if (option.ChainArcBonus != 0) set |= EvolutionStat.ChainArcs;
		if (option.ChainBranchBonus != 0) set |= EvolutionStat.ChainBranches;
		if (!Same(option.KnockbackBonus, 0f)) set |= EvolutionStat.Knockback;
		if (!Same(option.SlowMagnitudeBonus, 0f)) set |= EvolutionStat.Slow;
		if (option.PoisonTickBonus != 0) set |= EvolutionStat.PoisonTick;
		if (!Same(option.SpeedMultiplier, 1f)) set |= EvolutionStat.Speed;
		if (!Same(option.ScaleMultiplier, 1f)) set |= EvolutionStat.Scale;
		if (option.Rider != ElementRider.None || !Same(option.RiderMagnitude, 0f) || !Same(option.RiderSeconds, 0f)) set |= EvolutionStat.Rider;
		if (option.Effect != SpellEffect.None || !Same(option.EffectValue, 0f)) set |= EvolutionStat.Effect;
		return set;
	}

	private static bool Same(float a, float b) => MathF.Abs(a - b) < 0.0001f;
}
