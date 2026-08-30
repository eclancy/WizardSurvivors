# Chest Item Implementation Spec

Status: Draft
Priority: High

## Objective
Translate the approved chest item MVP into a clear technical and production plan so that the Godot gameplay engineer can implement it with minimal ambiguity.

## Design summary
The feature introduces a chest reward loop in which a player can find a unique item at random during a run. Each item has a distinct identity, and when the player owns all required items for a set, a synergy bonus triggers. This is a per-run system, not a permanent unlock system.

## Item and set rule set
- Chests are a rare random pickup during a run.
- Each item is permanent for the run once picked up.
- Each item has one clear identity and one clear trigger/buff.
- Set bonuses are additive and clear, but should not require exact duplicate counts or stacking complexity.
- Partial discovery is supported: collecting one item reveals the set clue or unlocks a partial set state.

## Recommended data model

### Item definition
```
class ChestItemDefinition
{
    string Id;
    string Name;
    string Description;
    string AssetPath;
    ItemRarity Rarity;
    ItemCategory Category; // defense, fire, lightning, treasure, retaliation
    string[] SetIds;
    string EffectType; // shield, burn, chain, pickup, explosion, retaliation
    float[] NumericParams;
    bool IsUniquePerRun;
}
```

### Set definition
```
class ChestSetDefinition
{
    string Id;
    string Name;
    string Description;
    string[] RequiredItemIds;
    string EffectType;
    float[] NumericParams;
    string VisualTag; // e.g. "shield-flare", "ember-ring", "arc-wave"
}
```

### Runtime state
```
class ChestRunState
{
    Dictionary<string, ChestItemDefinition> OwnedItems;
    HashSet<string> DiscoveredSetIds;
    HashSet<string> CompletedSetIds;
    List<string> OwnedItemIds;
}
```

## MVP item roster and effects

### 1) Relic Key
- Rarity: Uncommon
- Category: treasure
- Effect: +15% pickup radius. 8% chance on enemy kill to create a small treasure pulse at the player location.
- UI: “Treasure calls to treasure.”
- Asset: key / treasure set

### 2) Aegis Sigil
- Rarity: Rare
- Category: defense
- Effect: Gain a small shield when hit. While below 50% HP, reduce incoming damage by 8%.
- UI: “Fortify the ward.”
- Asset: shield family

### 3) Ember Flask
- Rarity: Rare
- Category: fire
- Effect: +10% fire spell damage. Burning enemies take 15% extra damage from all sources.
- UI: “Heat multiplies.”
- Asset: flask family

### 4) Storm Lattice
- Rarity: Rare
- Category: lightning
- Effect: +8% spell crit chance. Critical hits have a 25% chance to chain lightning to a nearby enemy.
- UI: “Thunder answers every hit.”
- Asset: lightning family

### 5) Inferno Core
- Rarity: Epic
- Category: fire
- Effect: +12% spell area. Enemy kills by spells trigger a small explosion dealing 15% of enemy max HP as fire damage.
- UI: “The spell remembers.”
- Asset: fireball / explosion family

### 6) Iron Fang
- Rarity: Rare
- Category: retaliation
- Effect: when hit, nearby enemies take spike damage. Gain +8% damage reduction while at full health.
- UI: “Teeth in the ground.”
- Asset: spike family

## MVP set bonuses

### Vaultguard
- Required items: Relic Key, Aegis Sigil, Iron Fang
- Effect: Opening a chest grants a short shield and small heal. While shielded, enemies in melee range suffer a rune pulse.DoT.
- Visual: chest burst + shield flare + rune ring
- Design purpose: treasure-hunter defensive set

### Emberline
- Required items: Ember Flask, Inferno Core, Relic Key
- Effect: Fire spells explode on kill. Nearby enemies are ignited on cast. All fire damage +20%, area +10%.
- Visual: orange-red ember ring and explosive burst
- Design purpose: fire burst build identity

### Stormbound
- Required items: Storm Lattice, Inferno Core, Aegis Sigil
- Effect: Critical hits chain twice. Every fifth cast triggers a lightning shockwave. Gain +10% movement speed while shielded.
- Visual: blue lightning arcs, shield flare
- Design purpose: hybrid burst + defense velocity set

### Bastion of Spikes
- Required items: Aegis Sigil, Iron Fang, Ember Flask
- Effect: While below 50% HP, gain damage reduction and a retaliatory spike field. Burning enemies in the field take extra damage.
- Visual: spike ring + ember haze
- Design purpose: defensive counterattack set

## UI behavior
- Show item names and short one-line descriptions in the reward popup.
- Add a synergy tab that lists discovered sets and current completion progress.
- When a new set is completed, show a short banner and a sync with the item list.
- Use clear icons from the existing art families to communicate set identity.

## Spawn and pickup flow
- Chest spawn frequency should be low enough to feel special, but common enough to produce a meaningful set progression loop.
- Spawn rules should be tuned by stage and difficulty, with special handling for chest-rich stages.
- If the player picks up a duplicate rare item, either ignore it or convert it into a minor currency/pulse for future balancing; do not make it silently break set logic.
- When a chest is opened, that item should be selected from a curated weighted drop table that includes the MVP items first.

## Implementation order
1. Data model + set registry
2. Chest spawning + pickup flow
3. Item application runtime logic
4. Set completion detection + synergy application
5. UI + notifications
6. Asset integration and polish
7. Balance pass and QA

## Persona ownership
- godot-gameplay-engineer: items, effects, set detection, chest flow
- level-design-wave-balance: chest weighting and pacing
- ui-ux-polish: UI and notification clarity
- two-d-art-director: art selection, readability, VFX fit
- gameplay-qa-and-balance: playtest and regression checklist

## Acceptance criteria
- A chest item system can be defined entirely in data with a register of items and sets.
- The MVP set roster can be implemented using the existing asset families and game logic.
- Full set completion triggers a visible and readable bonus effect.
- The system is ready for implementation without further design ambiguity.
