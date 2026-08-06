---
name: godot-autoshooter-feature
description: Use this skill when adding or changing Wizard Survivors gameplay features in Godot, especially spells, enemies, upgrades, UI wiring, and progression behavior.
---

# Godot Autoshooter Feature Workflow

Use this skill when a task touches core gameplay code in Wizard Survivors and needs to stay aligned with the project's Godot architecture.

## Project map
- Player flow lives in `scripts/Player.cs` and the main gameplay scene controller lives in `scripts/Node2DGame.cs`.
- Spell content is data-driven through `SpellData`, `SpellEffect`, `SpellLevelUpgrade`, and `.tres` resources.
- Enemies are `CharacterBody2D` scenes that join the `"enemies"` group and interact through duck-typed combat calls.
- Meta progression lives in `SaveData` and `SaveManager`.

## Workflow
1. Read the relevant `.ai/` docs before broad changes.
2. Decide which content path you are extending:
   - **Active spell**: `SpellData` resource + `.tscn` scene + script + player firing loop integration.
   - **Passive spell**: subclass `PassiveSpellEffect` and wire it through the existing player refresh/create helpers.
   - **Enemy or wave behavior**: enemy scene/script plus any spawning or game-controller integration.
   - **Progression or UI**: autoload/save wiring plus HUD/menu updates as needed.
3. Reuse existing patterns before creating new helpers.
4. Preserve group-based gameplay logic and verify collision layers/masks only where physics separation truly matters.
5. Validate the affected loop in Godot and note any manual runtime checks that remain.

## Guardrails
- Do not extend the legacy `Weapon` path for new content.
- Prefer `GetTree().GetNodesInGroup("enemies")` for AoE gameplay interactions.
- If you change `collision_layer`, update every relevant `collision_mask` in scenes and runtime-created areas.
- Avoid broad exception swallowing or silent fallbacks around gameplay state.
- Keep performance in mind: avoid unnecessary per-frame allocations for swarm-heavy effects.

## Examples

### Example: adding a new active spell
- Create a new `SpellData` resource with element weights and level-up data.
- Build a matching spell scene and script for the projectile or effect.
- Hook the spell into the existing player firing loop instead of adding a parallel timer system.
- Verify enemy detection and hit behavior against the `"enemies"` group.

### Example: adding a new passive defensive spell
- Subclass `PassiveSpellEffect`.
- Implement `OnPlayerDamaged(int)` or pulse behavior with `UsesPulseTimer` and `OnPulseTick()`.
- Register it through the player's defensive spell creation/refresh path.
- Verify the result against the `TakeDamage` ordering so the effect triggers at the intended stage.

## Learnings
- Collision bugs in this project are often mask mismatches, not script logic errors.
- Passive defensive spells do not need their own scenes by default; the existing player-owned effect pattern is the preferred path.
- Local `dotnet` CLI assumptions are unreliable here; use Godot's own build/runtime flow.
