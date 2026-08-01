# Gameplay, Spell, and Enemy Behavior Backlog

Status: Proposed
Priority: High
Labels: gameplay, spell-system, visuals, enemy-ai, passive-spells

## Summary
Collect the requested gameplay and spell-system improvements into one backlog issue so they can be prioritized and implemented as a cohesive pass.

## Proposed Work Items

### 1) [Gameplay / Spell Design] Lightning passive retool
- Problem: The current Lightning passive wording around “bonus to chain bolt” is unclear and likely does not read as a meaningful effect.
- Proposal: Rework the behavior into a clearer, more intuitive mechanic that feels like a true passive bonus rather than an ambiguous projectile-count modifier.
- Expected outcome: The passive should have a readable effect that fits the spell identity and is easy to understand in play.

### 2) [Visuals / Effects] Remove stray electricity-ball animation
- Problem: A floating electricity-ball-style animation is appearing in the field at times and looks like a stray environmental artifact.
- Proposal: Remove the placeholder effect and reserve that animation for future offensive attacks or spell visuals.
- Expected outcome: The field stays visually clean, and the effect can be repurposed later for intentional attacks.

### 3) [Gameplay / Ability Design] Erupting Earth
- Problem: There is no clear earth-based spell or environmental effect that uses a player-centered eruption mechanic.
- Proposal: Add a new effect where a stone wall erupts from the ground rock-by-rock, moving outward from the player, stunning and damaging all enemies it hits. The stones should then retreat back into the ground, again rock-by-rock, starting from the player.
- Expected outcome: The spell feels distinct, readable, and visually satisfying while fitting the earth/terrain fantasy.

### 4) [Enemy AI / Movement] Enemy collision and movement polish
- Problem: Enemies do not feel responsive enough when interacting with the player or nearby enemies.
- Proposal: Enemies should be lightly pushed back when they touch the player, lightly pushed away from nearby enemies, and typically receive slight noise in their pathing as they move toward the player.
- Expected outcome: Combat feels less rigid and more dynamic, with better spacing and less “stuck” behavior.

### 5) [Passive Spell Design] Passive spells should have two abilities
- Problem: Passive spells currently feel too narrow or too similar, and their utility is not always clear.
- Proposal: Each passive spell should have two abilities:
  - A unique passive effect tied to its identity (for example, a fire passive that ignites nearby enemies).
  - A general stat-boosting effect that improves overall player performance or offensive output, such as amount, area, cooldown, defense, health, or another relevant stat.
- Expected outcome: Passives become more meaningful, more distinct, and easier to build around.

## Acceptance Criteria
- The Lightning passive has a clear and understandable effect.
- The stray electricity-ball visual is removed from the field.
- Erupting Earth is defined as a distinct, player-centered earth spell concept with clear timing and effect behavior.
- Enemy movement feels less rigid and has better local spacing behavior.
- Passive spells each support both a unique identity effect and a broader stat or offensive benefit.

## Notes
This issue should be treated as a design-and-implementation pass covering spell clarity, visuals, enemy feel, and passive spell identity.
