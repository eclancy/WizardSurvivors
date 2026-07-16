# Gameplay Design

## Core Loop
1. Move to avoid enemies.
2. Auto-fire spells.
3. Collect XP.
4. Level up and choose upgrades.
5. Survive waves.
6. Die â†’ meta progression â†’ retry.

## Systems

### Spell System
- Each spell is a Resource.
- Properties:
  - damage
  - cooldown
  - projectile scene
  - modifiers
- Supports one or two element tags per spell.
- Element counts create threshold synergies at 2, 4, and 6 owned instances.

### Upgrade System
- Level-up choices.
- Stat boosts.
- Spell modifiers.
- Element previews.
- Replace, remove, or skip options when leveling up.

### Enemy Waves
- Time-based spawns.
- Increasing density.
- Elite enemies and minibosses.

### Meta Progression
- Permanent upgrades.
- Unlockable characters.
- Spell mastery.
