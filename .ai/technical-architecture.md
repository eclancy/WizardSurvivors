# Technical Architecture

## Core Scenes
- `scenes/player.tscn` / `scripts/Player.cs` (`CharacterBody2D`)
- Enemy scenes (`enemy.tscn`, `FastEnemy.tscn`, `TankEnemy.tscn`) using `scripts/Enemy.cs`
- Spell scenes plus `SpellData` resources for active spells
- `Node2DGame.cs` as the main run scene controller
- `LevelUpMenu.cs` for level-up choices, rerolls, skips, element previews, full-slot replacement, and remove-only flow
- `SaveManager.cs` autoload for persistent meta progression

## Data-Driven Design
- Spells defined in .tres/.res Resources.
- Upgrades defined in .tres Resources.
- Enemy stats defined in .tres Resources.

## Element Threshold Runtime
- Each `SpellData` resource exposes `ElementWeights`; most spells contribute one or two element instances.
- `Player.GetElementInstanceCounts()` totals equipped spell weights plus the selected character's innate element bonus.
- `Player.GetElementTier()` resolves the active 0/2/4/6 threshold for each element.
- Threshold bonuses are applied in the relevant gameplay path: XP gain, healing from damage dealt, incoming damage mitigation, HP regeneration, Earth max HP, Wind movement speed, Water cooldowns, and on-hit Fire/Ice/Lightning/Poison effects.
- The gameplay HUD shows active element counts, and the player HP bar shows the active Earth max-HP bonus when present.

## Performance Notes
- Use object pooling for projectiles.
- Avoid per-frame allocations.
- Use CanvasLayer for UI.
- Use FastNoiseLite for procedural patterns if needed.
