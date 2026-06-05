# Technical Architecture

## Core Scenes
- Player (CharacterBody2D)
- Enemy (CharacterBody2D)
- Spell projectiles (various scenes)
- GameController (autoload)
- WaveController
- UpgradeUI
- SaveSystem (autoload)

## Data-Driven Design
- Spells defined in .tres/.res Resources.
- Upgrades defined in .tres Resources.
- Enemy stats defined in .tres Resources.

## Performance Notes
- Use object pooling for projectiles.
- Avoid per-frame allocations.
- Use CanvasLayer for UI.
- Use FastNoiseLite for procedural patterns if needed.
