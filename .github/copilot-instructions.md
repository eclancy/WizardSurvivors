
# Wizard Survivors — AI coding agent instructions

Godot 4.5 C# (.NET 9) 2D roguelite (Vampire Survivors-like). Design/roadmap docs live in `.ai/` — read them before large design changes rather than duplicating them here: [.ai/project-overview.md](../.ai/project-overview.md), [.ai/gameplay-design.md](../.ai/gameplay-design.md), [.ai/technical-architecture.md](../.ai/technical-architecture.md), [.ai/godot-engine.md](../.ai/godot-engine.md), [.ai/roadmap.md](../.ai/roadmap.md), [.ai/versioning.md](../.ai/versioning.md), [.ai/content-pipeline.md](../.ai/content-pipeline.md). Backlog issues are tracked under `.ai/issues/` and on GitHub.

## Architecture
- Scene flow: TitleScreen → MainMenu → CharacterSelection → StageSelection → [node_2d_game.tscn](../scenes/node_2d_game.tscn) / [Node2DGame.cs](../scripts/Node2DGame.cs) → GameOverScreen → back to MainMenu. Selection state passed via the `Global` autoload ([Global.cs](../scripts/Global.cs): `SelectedCharacterIdx`, `SelectedStageIdx`).
- [Player.cs](../scripts/Player.cs) (`CharacterBody2D`): movement, HP pipeline (`TakeDamage` → `DamageTaken` signal → passive reactions → shield pool → flat reduction from passives → HP), equipped-spell firing loop (`spellFireTimers`, max 6 slots via `MaxSpellSlots`), level-up option generation (`GetLevelUpOptions`), element tier counting (`GetElementInstanceCounts`/`GetElementTier`).
- [Enemy.cs](../scripts/Enemy.cs) (`CharacterBody2D`): `TakeDamage`, `ApplyKnockback`, `ApplySlow` (0 = freeze/root), `ApplyPoison` — the latter two are stacking-resistant (take max magnitude + max remaining duration, not additive). Drops XP orb and `queue_free`s on death. Always added to group `"enemies"` in `_Ready()`.
- Spells are **data-driven** via `SpellData` resources (`.tres` files), not hardcoded — see [SpellData.cs](../scripts/SpellData.cs), [SpellEffect.cs](../scripts/SpellEffect.cs), [SpellLevelUpgrade.cs](../scripts/SpellLevelUpgrade.cs), [Element.cs](../scripts/Element.cs) (12 elements, `ElementWeights` per spell, tiers at counts 2/4/6). [Weapon.cs](../scripts/Weapon.cs) is legacy/superseded by `SpellData` — don't extend it for new content.
- Two spell archetypes:
  - **Active** (fired from `Player`'s weapon loop): [MagicMissile.cs](../scripts/MagicMissile.cs), [ArcaneExplosion.cs](../scripts/ArcaneExplosion.cs), [SpiritualWeapon.cs](../scripts/SpiritualWeapon.cs) — each pairs a script with a `.tscn` scene.
  - **Passive/defensive** (persistent children of `Player`, no scene needed): subclass [PassiveSpellEffect.cs](../scripts/PassiveSpellEffect.cs), overriding `OnPlayerDamaged(int)` for reactive spells or using `UsesPulseTimer` + `OnPulseTick()` for auras. Examples: AegisWard, FrozenBulwark, GuardianVines, VenomCloak, StoneBulwark, ThornmailBarrier, TidalBarrier, StormguardAura.
- Meta progression persists via [SaveData.cs](../scripts/SaveData.cs) + [SaveManager.cs](../scripts/SaveManager.cs) autoload (`user://savegame.json`): unlocked characters/stages, max difficulty cleared, and `ArcaneUpgradeLevels` (keys: damage, recovery, cooldowns, area, attack_speed, duration, amount, movespeed, magnet, growth, extra_lives, rerolls, vitality).

## Collision layers (verified from `.tscn` files — do not assume generic Godot defaults)
- Layer 2 (`collision_layer = 2`, `collision_mask = 1`): all enemies ([enemy.tscn](../scenes/enemy.tscn), [FastEnemy.tscn](../scenes/FastEnemy.tscn), [TankEnemy.tscn](../scenes/TankEnemy.tscn)).
- Layer 3 (`collision_layer = 3`, `collision_mask = 3`): player body + HurtBox ([player.tscn](../scenes/player.tscn)).
- Weapon `Area2D` scenes ([MagicMissile.tscn](../scenes/MagicMissile.tscn), [ArcaneExplosion.tscn](../scenes/ArcaneExplosion.tscn)) use `collision_mask = 2` to detect enemies.
- Rule: changing a node's `collision_layer` requires updating every `collision_mask` that must detect it, in both `.tscn` files and runtime-created `Area2D` instances (search for `new Area2D()`). Prefer `IsInGroup("enemies")` for gameplay logic; use layers only for physics separation. When unsure, inspect/edit layers in the Godot editor rather than hand-editing bitmasks.

## Conventions
- Damage/interaction checks favor duck-typing over strict types: `IsInGroup("enemies")` + `HasMethod("TakeDamage")`. This decouples scenes intentionally — follow it for new spell effects.
- New active spell: add a `SpellData` `.tres` resource, create `scenes/YourSpell.tscn` + script, wire firing into `Player`'s existing weapon loop instead of adding ad-hoc timers.
- New passive spell: subclass `PassiveSpellEffect`, register via the `Player.CreateDefensiveSpellData()` / `RefreshPassiveSpellInstance<T>()` pattern — code-only, no `.tscn`.
- Iterate enemies for AoE effects via `GetTree().GetNodesInGroup("enemies")` (see [ArcaneExplosion.cs](../scripts/ArcaneExplosion.cs)).

## Developer workflow
- No local `dotnet` CLI in this environment (no `global.json`/`nuget.config`) — Godot's own build system compiles C#. Use the "Run Godot Project" VS Code task (runs `godot` in the workspace root) to launch/build; re-open or refresh Godot after external C# edits to pick up new assemblies.
- Targets Godot 4.5 + .NET 9 (`WizardSurvivors.csproj`, `Godot.NET.Sdk` 4.5.0).
- Keep changes small and validate by playing the scene in the Godot editor — signal/collision issues are easiest to catch at runtime.
