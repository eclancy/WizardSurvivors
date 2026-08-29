# Chest Item MVP Design Brief

Status: Draft
Priority: High

## Goal
Define a concrete first-pass chest item roster and three synergy sets that fit the existing Wizard Survivors systems and the art already in the repo.

## Art-first direction
The feature should use the currently available asset families as the primary design anchor instead of waiting for a full custom art pass. This keeps the feature testable and gives the visual team clear reference points for future polish.

### Asset families used
- Chest: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-*.png`
- Chest open: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-open-*.png`
- Keys / treasure: `...-keys-*.png`
- Flasks / potions: `...-flasks-*.png`
- Shields: `fx-10-magic-sprite-sheet-effects-pixel-art-8-self-shield-shield.png`
- Lightning: `fx-10-magic-sprite-sheet-effects-pixel-art-1-lightning-bolt-lightning.png`
- Explosion / fire: `...-10-fire-ball-fire-ball.png`, `...-5-explosion-explosion.png`
- Spikes: `...-6-spikes-from-ground-spikes.png`

## Core design rule
Items should each have one strong identity, one clear gameplay trigger, and one simple sentence of readability. Set bonuses should be more than a flat stat bump; they should create a recognizable build identity.

## MVP item roster

### 1) Relic Key
- Asset: key / chest / treasure family
- Role: treasure-hunter / chest synergy
- Effect: +15% pickup radius. 8% chance on enemy kill to create a short-lived bonus chest pulse at the player’s location.
- Readability: “Treasure calls to treasure.”
- Rarity: Uncommon

### 2) Aegis Sigil
- Asset: shield family
- Role: sustain / defense / early stability
- Effect: gain a small shield when hit. While below 50% HP, reduce incoming damage by 8%.
- Readability: “Fortify the ward.”
- Rarity: Rare

### 3) Ember Flask
- Asset: flask / potion family
- Role: fire / burn / sustain
- Effect: +10% fire spell damage. Burning enemies take +15% more damage from all sources.
- Readability: “Heat multiplies.”
- Rarity: Rare

### 4) Storm Lattice
- Asset: lightning family
- Role: crit / chain / arcane burst
- Effect: +8% spell crit chance. Critical hits have a 25% chance to chain lightning to a nearby enemy.
- Readability: “Thunder answers every hit.”
- Rarity: Rare

### 5) Inferno Core
- Asset: fireball / explosion family
- Role: burst / explosion / radial damage
- Effect: +12% spell area. Enemy kills by spells trigger a small explosion dealing 15% of enemy max HP as fire damage.
- Readability: “The spell remembers.”
- Rarity: Epic

### 6) Iron Fang
- Asset: spikes / trap family
- Role: retaliation / counterattack
- Effect: when the player is hit, nearby enemies take spike damage. Gain +8% damage reduction while at full health.
- Readability: “Teeth in the ground.”
- Rarity: Rare

## MVP set bonuses

### Set 1: Vaultguard
- Components: Relic Key + Aegis Sigil + Iron Fang
- Bonus: “Treasure Ward”
- Effect: Every time the player opens a chest, gain a short shield and heal for a small amount. While shielded, enemies within melee range suffer damage over time from a rune pulse.
- Design purpose: chest reward-focused defense and treasure-loop synergy.
- Visuals: chest burst + glowing shield flare + rune pulse ring

### Set 2: Emberline
- Components: Ember Flask + Inferno Core + Relic Key
- Bonus: “Flamebound Cache”
- Effect: Fire spells explode on kill, plus nearby enemies become ignited on cast. All fire effects gain +20% damage and +10% area.
- Design purpose: aggressive fire build identity with a treasure-feel loop.
- Visuals: orange-red bursts, ember ring, key shine

### Set 3: Stormbound
- Components: Storm Lattice + Inferno Core + Aegis Sigil
- Bonus: “Arc Ward”
- Effect: Critical hits chain twice, and every fifth cast sends a lightning shockwave. Gain +10% movement speed while shielded.
- Design purpose: hybrid burst and defense build with a clear lightning weapon fantasy.
- Visuals: blue arcs, bright shield pulse, crackling trail

### Set 4: Bastion of Spikes
- Components: Aegis Sigil + Iron Fang + Ember Flask
- Bonus: “Crimson Bastion”
- Effect: While below 50% health, the player gains damage reduction and a retaliatory spike field that detonates on hit. Burning enemies in the field take extra damage.
- Design purpose: defensive counterattack answer for players who want a more tanky hybrid build.
- Visuals: shield halo + spike ring + ember haze

## Balance principles
- No set should be a mandatory late-game pick.
- Each set should be competitive with existing spell builds, not a default replacement for the whole game.
- The feature should be encouraging, not punishing. Set completion should feel rewarding within a single run.
- Keep the item power curve smooth: the first item provides identity, the second item adds clarity, the third item completes the fantasy.

## Persona responsibility
- godot-gameplay-engineer: implement runtime logic for item effects and set registry
- level-design-wave-balance: tune chest frequency and rarity
- ui-ux-polish: add clear labels and progress tracking for discovered sets
- two-d-art-director: confirm key asset families and final icon language
- gameplay-qa-and-balance: validate balance and edge cases

## Acceptance criteria
- 6 chest items are defined with unique, readable effects.
- 3–4 set bonuses are defined with clear names and narrative identity.
- All items map to already-available art families.
- Each set creates a distinct build identity rather than just a stat bonus.
- The roster is specific enough to move into implementation with minimal rework.
