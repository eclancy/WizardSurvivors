# Asset-Driven Chest Item Concept Pass

Status: Draft
Priority: High

## Objective
Create a first-pass roster of chest items and synergy sets that use the art and VFX already available in the repo rather than waiting for new asset creation. The goal is to keep momentum on design while staying compatible with the project’s current art inventory.

## Art inventory to leverage
- Chest visuals: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-*.png`
- Chest open animations: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-open-*.png`
- Keys and treasure props: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-keys-*.png`
- Flasks and potions: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-flasks-*.png`
- Coins / currency: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-coin-*.png`
- Arrow / projectile cues: `assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-just-arrow.png`
- Shield / defensive magic: `assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-8-self-shield-shield.png`
- Lightning / arcane: `assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-1-lightning-bolt-lightning.png`
- Fire and explosion: `assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-10-fire-ball-fire-ball.png` and `...-5-explosion-explosion.png`
- Ground spikes / hazards: `assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-6-spikes-from-ground-spikes.png`
- Fire wall / area denial: `assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-fire-wall-ignition-fire-wall.png`

## Design principle
Use the existing art families as identity anchors for the item roster. Each item should visually match an established fantasy tag, and each set should combine those identities into a readable story: treasure, defense, fire, lightning, and arcane power.

## Proposed MVP item roster

### 1) Relic Key
- Asset: key sprite family
- Role: treasure / unlock / pickup
- Effect: slightly increases item pickup radius and grants a small chance to spawn a free chest on enemy kill
- Purpose: reward exploration and treasure-hunting build identity

### 2) Aegis Sigil
- Asset: shield sprite
- Role: defense / invincibility
- Effect: gain a brief shield after taking damage; reduces damage taken briefly while below 50% HP
- Purpose: defensive set foundation

### 3) Ember Flask
- Asset: flask/potion sprite
- Role: burn / sustain / offense
- Effect: burning enemies have increased damage taken; healing pulses on fire activation
- Purpose: fire-themed sustain offense

### 4) Storm Lattice
- Asset: lightning bolt sprite
- Role: arcane burst / chain
- Effect: spells occasionally chain to nearby enemies; increases lightning-tag critical chance
- Purpose: electric build identity

### 5) Inferno Core
- Asset: fireball / explosion sprite
- Role: area explosion / burst
- Effect: killing an enemy with a spell causes a small explosion; reloads fire damage around the player
- Purpose: high-tempo offensive item

### 6) Iron Fang
- Asset: spikes / trap-like effect
- Role: hazard / counterattack
- Effect: enemies that hit the player take spike damage, and the player gains a small amount of armor
- Purpose: counterattack/area control

## Proposed set bonuses

### Set 1: Vaultguard
- Components: Relic Key, Aegis Sigil, Coin / Garland variant
- Bonus: "Treasure-touched guard" — gain a shield when opening a chest, and opening chests grants a minor heal and buff
- Visuals: chest opening burst + shield flare

### Set 2: Emberline
- Components: Ember Flask, Inferno Core, Fire Wall / blaze effect
- Bonus: "Living flame" — all fire-triggered effects deal extra damage and burn nearby enemies after casting a spell
- Visuals: orange-red VFX and flame ring around the player

### Set 3: Stormbound
- Components: Storm Lattice, Relic Key, Arrow / chain marker
- Bonus: "Arc surge" — chained lightning damage increases and critical hits arc to nearby enemies
- Visuals: blue lightning pulses and a bright arcane outline

### Set 4: Bastion of Spikes
- Components: Aegis Sigil, Iron Fang, Spikes / trap icon
- Bonus: "Thorns of the ward" — damage retaliation damages nearby enemies and reduces all incoming damage while in the active ward state
- Visuals: spike ring and defensive shield flare

## Recommended issue framing
This should be treated as an asset-informed prototype pass rather than a final art pass. The art is already present in the project and can support a strong first iteration of chest item concepts using existing families, while preserving room for later replacement with custom final art.

## Persona assignment
- two-d-art-director: select the visual families and confirm readability against the existing art style
- godot-gameplay-engineer: map the item effects into existing spell/passive architecture and test fit with the runtime systems
- ui-ux-polish: define how item names, descriptions, and set completion states read on screen
- level-design-wave-balance: tune rarity and chest pacing around these item concepts
- gameplay-qa-and-balance: validate that the first roster remains fair and fun
