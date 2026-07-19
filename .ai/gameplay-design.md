# Gameplay Design

## Core Loop
1. Move to avoid enemies.
2. Auto-fire spells.
3. Collect XP.
4. Level up and choose upgrades.
5. Survive waves.
6. Die -> meta progression -> retry.

## Systems

### Spell System
- Each spell is a Resource.
- Properties:
  - damage
  - cooldown
  - projectile scene
  - modifiers
- Supports one or two element tags per spell.
- Element counts create threshold bonuses at 2, 4, and 6 owned instances.
- Element thresholds are based on summed element weight across equipped spells; a double-weight spell counts as 2 instances toward that element.
- Canonical elements: Fire, Ice, Arcane, Darkness, Light, Grass, Earth, Wind, Lightning, Poison, Metal, Water.

### Element Threshold Balance
- Fire: +10% / +20% / +35% damage to nearby enemies.
- Ice: -10% / -20% / -35% enemy move speed for 2 seconds on hit.
- Arcane: +10% / +20% / +35% XP gained.
- Darkness: -10% / -20% / -35% incoming damage.
- Light: heal 3% / 6% / 10% of damage dealt.
- Grass: +1 / +2 / +4 HP per second regeneration.
- Earth: +20 / +50 / +100 max HP.
- Wind: +10% / +20% / +35% move speed.
- Lightning: +10% / +20% / +35% chance to chain a bolt to a second enemy on hit.
- Poison: +2 / +4 / +8 stacking-resistant damage over time per tick for 3 seconds on hit.
- Metal: -1 / -2 / -4 flat damage taken per hit.
- Water: -5% / -10% / -18% spell cooldowns.

### Upgrade System
- Level-up choices.
- Stat boosts.
- Spell modifiers.
- Element previews.
- If spell slots are full, new-spell choices enter a replace flow; players can also remove an owned spell or skip the level-up choice.

### Enemy Waves
- Time-based spawns.
- Increasing density.
- Elite enemies and minibosses.

### Meta Progression
- Permanent upgrades.
- Unlockable characters.
- Spell mastery.
