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

## Draw layers (2026-10-28)
The arena (`Node2DGame`) y-sorts, and y-sort only orders things that share a z. Anything left at z 0
is sorted in among the bodies by where its origin stands, so a blast or a bolt is cut by every body
south of its centre and a ring painted on the floor lies across every body north of it. Eric's rule:
"the hammer itself should be above enemies, but the quake it creates should be underneath their
sprites. Same with meteors - the meteor and explosion should be rendered above enemies, but the fires
should be rendered underneath."

So every world effect picks a side from `scripts/DrawLayers.cs`, with `ZAsRelative = false`:

| Layer | z | What |
|---|---|---|
| `Terrain` | -100 | the painted ground |
| `Decal` | -20 | walkable decoration, scorch marks |
| `Ground` | -15 | craters, pools, lanes, the maw's pit (crater fire is -14) |
| `GroundTell` | -1 | warnings and marks painted round a body's feet |
| `Bodies` | 0 | the player, enemies, familiars that walk (the dog, the cat), chests, trees: y-sorted |
| `Air` | 6 | projectiles, blasts, swung weapons, thrown sprays, the familiar that flies (the crow, the griffin) |
| `FloatingText` | 20 | damage numbers |

One effect is often both, and is then two nodes: `SpiritualWeapon` (weapon in the air, cracks on the
floor), `MeteorImpact` and `CraterLayer`, `Contagion` (spores up, carriers' marks down). An enemy's
floor tells go in `Enemy.DrawGroundTells`, which paints on a ground node made once in `_Ready`; only
what belongs to the body (a caster's gathering mote, the Long Coil's segments) stays in `_Draw`.

To find what is drawn among the bodies: `_UiShot --layer-census` prints a `LAYER` line per drawn
node class with its effective z and whether a y-sorted parent sorts it.

