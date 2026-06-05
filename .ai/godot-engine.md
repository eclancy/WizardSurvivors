# Godot Engine Notes

## Engine Version
Godot 4.x (update this file when version changes).

## Why Godot
- Open-source, no licensing issues.
- Excellent 2D performance.
- GDScript enables fast iteration.
- Scene system supports modular design.

## Conventions
- Use autoloads for global systems (GameState, SaveData, RNG).
- Prefer composition via scenes over deep inheritance.
- Use signals for decoupling (player_hit, xp_collected, spell_fired).
- Use Resources for spells, upgrades, and enemy definitions.

## Best Practices
- Keep _process lightweight; use timers and signals.
- Use CharacterBody2D for player and enemies.
- Use NavigationAgent2D for advanced enemy pathing.
- Use AnimationPlayer or AnimatedSprite2D for visuals.
- Use TileMap for arenas.
- Use object pooling for projectiles.
