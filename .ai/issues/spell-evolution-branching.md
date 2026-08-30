# Spell Evolution & Branching System Specification

## Overview
All spells (active and passive) in *Wizard Survivors* progress from Level 1 to 8. 
To introduce deeper buildcrafting, strategic agency, and visual variety:
- **Level 4 (Mutation Milestone):** Players choose 1 of **3 mutually exclusive** mechanical mutations that alter how the spell behaves (piercing, secondary AoE, chain sparks, speed/spread, or hazard creation).
- **Level 8 (Ascension Evolution):** Players choose 1 of **2 build-defining** capstone evolutions that fundamentally redefine the spell's identity, scaling, and synergies.

---

## Architecture & Data Contracts

### 1. `SpellEvolutionOption` Resource
A `[GlobalClass]` Godot Resource defining a single branch choice:
- `Id`: Unique branch identifier (e.g., `magic_missile_pierce_trail`, `magic_missile_singularity`).
- `DisplayName`: Player-facing name (e.g., "Piercing Stardust", "Singularity Vortex").
- `Description`: Clear explanation of mechanical alterations.
- `MilestoneLevel`: `4` (Mutation) or `8` (Ascension).
- `SynergyTag`: Primary elemental/mechanical synergy (e.g., `Arcane / Pierce`, `Fire / AOE`, `Ice / Control`).
- `SynergyDescription`: Helpful tooltip hint for combining with other passives/items.
- **Stat Deltas:** `DamageBonus`, `DamageMultiplier`, `CooldownBonus`, `AreaMultiplier`, `PierceBonus`, `ProjectileCountBonus`, `ChainArcBonus`, `SlowMagnitude`, `PoisonTickBonus`, `KnockbackBonus`.
- **Behavior Flags / Effect Types:** `SpellEffect` (Pierce, Chain, SplashOnHit, HazardOnHit, OrbitingNova, GroundSlam, etc.).
- **Visual Presentation:** `VisualTag`, `ModulateColor`, `ScaleMultiplier`, `SpeedMultiplier`.
- **Elemental Weights:** `BonusElementWeights` allowing evolutions to boost elemental threshold instance counts.

### 2. `SpellData` Integration
- `Level4Options`: Array of 3 `SpellEvolutionOption` resources.
- `Level8Options`: Array of 2 `SpellEvolutionOption` resources.
- `SelectedLevel4EvolutionId` / `SelectedLevel8EvolutionId`: Stored on the runtime clone instance in `Player.equippedSpells`.
- Helper methods for computed stats incorporating active evolutions.

### 3. Level-Up UI Flow
- When leveling a spell from Lv 3 -> 4 or Lv 7 -> 8:
  - `LevelUpMenu` detects the milestone upgrade.
  - Opens the **Spell Evolution Modal**:
    - **Level 4:** 3 Mutation Cards with emerald/cyan accents, showing mechanical alteration badges and synergy hints.
    - **Level 8:** 2 Ascension Cards with glowing gold/legendary borders, showcasing the ultimate spell forms.
  - Selecting an evolution applies it immediately to the runtime `SpellData`, recalculates spell stats, and updates projectile/visual properties.

---

## Spell Evolution Matrix

| Spell | Level 4 Mutation Choices (Pick 1 of 3) | Level 8 Ascension Choices (Pick 1 of 2) |
| :--- | :--- | :--- |
| **Magic Missile** | 1. *Twin Volley:* +1 Projectile, +20% Speed.<br>2. *Piercing Stardust:* +3 Pierce, trails arcane dust.<br>3. *Unstable Shard:* Detonates on hit for 50% splash damage. | 1. *Arcane Gatling Barrage:* Rapid continuous stream with high pierce & comet VFX.<br>2. *Singularity Core:* Slow piercing black hole orb pulling and crushing enemies. |
| **Arcane Explosion** | 1. *Dual Shockwave:* Fires 2 successive concentric blasts with +50% knockback.<br>2. *Static Nova:* Hits emit 3 chain lightning sparks.<br>3. *Lingering Rift:* Leaves a 2.5s vortex hazard dealing continuous ticks. | 1. *Supernova:* Screen-wide fiery explosion disintegrating weak enemies and applying burn.<br>2. *Event Horizon:* Inward collapsing cosmic vortex that roots/freezes all targets. |
| **Spiritual Weapon** | 1. *Phantom Whirlwind:* +3 blades, +40% spin speed.<br>2. *Spectral Cleave:* Massive reach, pierces armor and deals +50% damage.<br>3. *Soul Siphon:* Hits heal 2 HP and increase player move speed. | 1. *Blade Tempest:* Storm of autonomous homing daggers ricocheting between foes.<br>2. *Executioner Blade:* Giant ethereal greatsword slamming the ground every 2s. |
| **Fireball** | 1. *Pyroclasm:* Impact leaves a burning magma pool.<br>2. *Cluster Bombs:* Splits into 3 mini explosive projectiles on hit.<br>3. *Piercing Comet:* Passes through enemies leaving a fire wall. | 1. *Hellfire Nova:* Massive rolling shockwave of flame melting enemy armor.<br>2. *Armageddon Sun:* Giant sun orb scorching all nearby foes before a cataclysmic blast. |
| **Frost Shard** | 1. *Glacial Spike:* Heavy ice lance with +4 Pierce and +50% Slow.<br>2. *Ice Flurry:* Fires a 5-shard cone with high spread.<br>3. *Frost Nova Proc:* Impact triggers a freezing circular frost pulse. | 1. *Absolute Zero:* Frost shards shatter frozen targets into deadly ice shrapnel.<br>2. *Blizzard Engine:* Creates a permanent swirling snowstorm around the player. |
| **Chain Lightning** | 1. *High Voltage:* +3 Chain jumps and +35% Crit chance.<br>2. *Ball Lightning:* Spawns a slow drifting electrical orb emitting zaps.<br>3. *Forked Surge:* Each jump splits into 2 secondary arcs. | 1. *Thunder God Wrath:* Sky strikes smite all enemies in the chain for massive burst.<br>2. *Overcharge Conduit:* Electrifies the ground between chained enemies. |
| **Aegis Ward** (Passive) | 1. *Reflective Plating:* Shield break reflects 150% damage to attacker.<br>2. *Quick Bastion:* -40% shield recharge cooldown.<br>3. *Fortified Aegis:* +50% shield capacity, +2 flat armor while active. | 1. *Sun Aegis:* Radiates holy damage beams while charged; blinding burst on break.<br>2. *Thorn Crystal:* Shield break detonates into 8 piercing crystal shards. |
