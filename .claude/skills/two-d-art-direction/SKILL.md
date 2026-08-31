---
name: two-d-art-direction
description: Defining Wizard Survivors 2D visual direction — sprite and spell-VFX asset briefs, UI icon direction, elemental shape language and palette, silhouette and readability rules for dense top-down swarms, and Godot import notes. Use when creating art briefs or judging whether a visual reads in combat.
---

# 2D Art Direction for Wizard Survivors

Use this skill when you need art direction that supports a readable top-down autoshooter while staying realistic for a lightweight indie content pipeline.

## Visual goals
- Make the player, enemies, pickups, projectiles, and danger areas instantly readable.
- Keep the game magical and expressive without burying gameplay information under noise.
- Use limited-detail shapes and color coding that remain clear during heavy combat.

## Direction checklist
1. Define the gameplay purpose of the asset first.
2. Specify silhouette, scale, and the angle the player sees most often.
3. Assign a controlled palette and element-specific accent colors.
4. Limit animation scope to the few beats that sell the action: idle, travel, impact, dissolve, pulse, or pickup pop.
5. Describe supporting VFX separately from the base sprite so readability can be tuned independently.
6. Include practical implementation notes for Aseprite export and Godot import when useful.

## Style guidance
- Favor bold silhouettes over internal detail.
- Reserve the brightest highlights for player actions, pickups, and high-threat enemy tells.
- Keep backgrounds and floor tiles quieter than gameplay actors.
- Use shape language to distinguish elements:
  - Fire: flares, embers, tapered aggressive motion.
  - Ice: shards, crisp edges, restrained motion trails.
  - Arcane: circles, runes, layered glow.
  - Poison: droplets, fumes, sickly gradients.
  - Lightning: forks, zig-zags, sharp flashes.

## Deliverable formats
- **Asset brief**: role, silhouette, palette, animation beats, VFX, implementation notes.
- **Prompt-ready concept brief**: concise paragraph suitable for image generation or contractor handoff.
- **Consistency pass**: list of what to unify across a set of assets.

## Examples

### Example: spell icon brief
- Role: level-up choice icon for a lightning spell.
- Silhouette: centered forked bolt inside a simple rune frame.
- Palette: desaturated steel base with bright cyan-white highlight.
- Motion cue: subtle outward spark burst on selection.

### Example: enemy sprite brief
- Role: fast flanker enemy that must read as dangerous but fragile.
- Silhouette: narrow body with oversized forward-leaning claws.
- Palette: dark base values with one hot accent color for the attack tell.
- Animation: 4-frame run, 2-frame hit flash, brief dissolve on death.

## Learnings
- In an autoshooter, readability problems usually come from value clutter before they come from missing detail.
- Element identity should be readable even when color is partially obscured by overlapping combat effects.
