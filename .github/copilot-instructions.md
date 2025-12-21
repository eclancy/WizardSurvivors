
This project: Wizard Survivors — focused instructions for AI coding agents

Goal
- Help contributors and automated coding agents make safe, small, and correct edits to this Godot 4 C# project. Focus on runtime behavior, scene files (.tscn), and Godot/C# conventions used here.

Quick architecture summary
- Godot 4 project using C# (.NET). Entry point scenes and logic live under `scenes/` and `scripts/`.
- Main game node: `scenes/node_2d_game.tscn` / `scripts/Node2DGame.cs` — spawns enemies and owns the `Player` node (`CharacterBody2D` at `CharacterBody2D` path inside the scene). Use this file to understand spawn, UI hooks, and level-up flow.
- Player logic: `scripts/Player.cs` — movement (CharacterBody2D + `MoveAndSlide()`), HurtBox `Area2D` for damage detection, equipped weapon management, and firing logic.
- Enemy logic: `scripts/Enemy.cs` — `CharacterBody2D` that moves toward the player and supports `ApplyKnockback(Vector2)` and `TakeDamage(int)`. Enemies are added to group "enemies" in `_Ready()`.
- Weapons: `scripts/Weapon.cs` defines weapon data (Id enum, stats). Projectile/ability scenes and scripts live in `scenes/` and `scripts/` (e.g., `MagicMissile.tscn` + `scripts/MagicMissile.cs`, `ArcaneExplosion.tscn` + `scripts/ArcaneExplosion.cs`, `SpiritualWeapon.cs`). Weapons use Area2D signals (`area_entered`, `body_entered`) to deal damage to nodes in group `enemies`.

Collision layer mapping (explicit)
- Important: collision layers are integer bitmasks in Godot. Use these concrete mappings to avoid breaking masks (powers of two):
  - Layer 1 (bit=1): World / player main body (default CharacterBody2D layer).
  - Layer 2 (bit=2): Enemies (all `CharacterBody2D` enemy scenes should use `collision_layer = 2`).
  - Layer 3 (bit=4): Player HurtBox / UI detection areas (optional in some scenes).

Examples (how to set)
- Scene `.tscn` edits:
  - Enemy scene header (example):
    - `collision_layer = 2`
    - `collision_mask = 1` (so enemies collide with world but not the player body)
  - Magic missile/Arcane explosion Area2D in `.tscn`:
    - `collision_mask = 2` (so the Area detects enemies on layer 2)

- Runtime code edits (C#): when creating Area2D instances via code, ensure the Area's CollisionMask includes the enemy bit (example: set the mask so it contains 2). See `scripts/SpiritualWeapon.cs` for a runtime example.

Quick checklist for edits touching collisions
1. Identify which nodes are involved (CharacterBody2D vs Area2D).
2. If you change an object's `collision_layer`, update every `collision_mask` that must detect it (scene files and runtime-created Areas).
3. Prefer `IsInGroup("enemies")` checks for gameplay logic; use layers for physics separation only.
4. When uncertain, open the `.tscn` in Godot editor and inspect layer/mask UI — it's safer than hand-editing bitmasks.

Project-specific conventions to follow
- Godot layers: the project uses collision layers for gameplay separation. Enemy bodies are on layer 2; the player main `CharacterBody2D` is on the default layer (1). Weapon Areas and the player's HurtBox must have their `collision_mask` include enemy layer (2) to detect enemies.
- Group usage: dynamic checks and damage logic rely heavily on Godot groups. Enemies call `AddToGroup("enemies")` in `_Ready()` and weapons/damage checks use `IsInGroup("enemies")`. Prefer group checks over layer checks when deciding whether to call `TakeDamage`.
- Scene vs code: Many game behaviors combine scene settings and script logic. For example, weapon Areas use both scene `collision_mask` and script signal hookups (`Connect("area_entered", ...)`). When changing behavior, update both the `.tscn` and the script initialization where Areas are created at runtime (e.g., `SpiritualWeapon.cs`).
- Weapon data: `Weapon.GetArcaneWeapons()` returns a hard-coded list used as a data source. To add weapons, update this method and then ensure scenes/scripts that instantiate projectiles reference the new `ProjectileScenePath` or have the handler added to `Player` (e.g., adding PackedScene exports).

Key files to inspect for common edits
- `scripts/Player.cs` — movement, hurtbox, weapons, and firing loop.
- `scripts/Enemy.cs` — knockback, movement, health, death and XP drops.
- `scripts/Weapon.cs` — weapon definitions and level-up logic.
- `scripts/MagicMissile.cs`, `scripts/ArcaneExplosion.cs`, `scripts/SpiritualWeapon.cs` — projectile/ability implementations. Check `Connect(...)` calls and collision handling here.
- `scenes/*.tscn` — required to check `CollisionLayer`/`CollisionMask`, `CollisionShape2D`, exported PackedScene references and node paths.

Developer workflows & commands
- Run/play locally: Use Godot 4.5 (project is configured for Godot 4.5 + C#). The repository includes a Visual Studio solution and .csproj files — open in Godot or your IDE. To run from workspace, use the provided VS Code task labelled "Run Godot Project" which executes `godot` in the workspace root.
- Editing scenes: prefer using the Godot editor for collision layers and bitmask values when possible. If editing `.tscn` by hand, be careful with `collision_layer` and `collision_mask` integer bitmasks (layer bit values are powers of two: 1,2,4,8...).
- C# builds: Godot will build C# assemblies when running the project. If editing C# files outside Godot, re-open or refresh Godot to pick up compiled assemblies.

Patterns and anti-patterns spotted
- Pattern: Damage and interactions rely primarily on group membership (`IsInGroup("enemies")`) and method existence (`HasMethod("TakeDamage")`) rather than strictly typed references. This is intentional for decoupling scenes.
- Anti-pattern to avoid: Changing collision layer numbers without updating masks in both scenes and runtime-created Areas will break hit detection. Always update both.

Integration points and external dependencies
- Godot engine (4.5) with C# (.NET). No external NuGet packages appear to be required.
- Audio and textures are referenced from `assets/` and packed into scenes. Weapon scenes reference their projectile scenes via `ProjectileScenePath` and `PackedScene` exports on `Player`.

How to make safe edits (examples)
- Adding a new weapon: add its entry to `Weapon.GetArcaneWeapons()`; add an exported `PackedScene` property to `Player` if the weapon needs a scene reference; create `scenes/YourWeapon.tscn` and script handling with `Area2D` collision mask set to detect enemies (mask=2 in current convention). Ensure `Player._Ready()` initializes weaponFireTimers for the new `WeaponId`.
- Adjusting collision behavior (player should not be blocked by enemies): update enemy `.tscn` nodes to place enemy `CharacterBody2D` on layer 2 and set their `collision_mask` appropriately for world collisions; set `Player` HurtBox `collision_mask` to include 2, and ensure all weapon Areas have `collision_mask` set to 2 or set in runtime creation.

Editing rules for AI agents (concise)
1. Read `scripts/Player.cs` and `scripts/Enemy.cs` before changing physics or collision behaviors. Many interactions assume the `enemies` group and `ApplyKnockback`/`TakeDamage` methods exist.
2. If touching collision layers, update both `.tscn` files and any runtime-created Areas (search for `new Area2D()` in the codebase) to keep masks/layers consistent.
3. When adding functionality to the player or weapons, prefer hooking into existing weapon loop in `Player._PhysicsProcess` rather than sprinkling ad-hoc timers in new code.
4. Use `GetTree().GetNodesInGroup("enemies")` to iterate enemies when implementing area effects (ArcaneExplosion follows this pattern in `ArcaneExplosion.cs`).
5. Keep change size small and test in Godot: prefer incremental single-feature edits with runtime validation (play the scene) to confirm signal/collision behavior.

If anything above is unclear or you want more specifics (e.g., sample change to add a new weapon with code + scene), tell me which area and I will expand with concrete code/scene steps.

Collision layer mapping (explicit)
- Important: collision layers are integer bitmasks in Godot. Use these concrete mappings to avoid breaking masks (powers of two):
  - Layer 1 (bit=1): World / player main body (default CharacterBody2D layer).
  - Layer 2 (bit=2): Enemies (all `CharacterBody2D` enemy scenes should use `collision_layer = 2`).
  - Layer 3 (bit=4): Player HurtBox / UI detection areas (optional in some scenes).

Examples (how to set)
- Scene `.tscn` edits:
  - Enemy scene header (example):
    - `collision_layer = 2`
    - `collision_mask = 1` (so enemies collide with world but not the player body)
  - Magic missile/Arcane explosion Area2D in `.tscn`:
    - `collision_mask = 2` (so the Area detects enemies on layer 2)

- Runtime code edits (C#): when creating Area2D instances via code, set `area.CollisionMask = 2;` (see `scripts/SpiritualWeapon.cs`) to match scenes.

Quick checklist for edits touching collisions
1. Identify which nodes are involved (CharacterBody2D vs Area2D).
2. If you change an object's `collision_layer`, update every `collision_mask` that must detect it (scene files and runtime-created Areas).
3. Prefer IsInGroup("enemies") checks for gameplay logic; use layers for physics separation only.
4. When uncertain, open the `.tscn` in Godot editor and inspect layer/mask UI — it's safer than hand-editing bitmasks.

Project-specific conventions to follow
- Godot layers: the project uses collision layers for gameplay separation. Enemy bodies are on layer 2; the player main `CharacterBody2D` is on the default layer (1). Weapon Areas and the player's HurtBox must have their `collision_mask` include enemy layer (2) to detect enemies.
- Group usage: dynamic checks and damage logic rely heavily on Godot groups. Enemies call `AddToGroup("enemies")` in `_Ready()` and weapons/damage checks use `IsInGroup("enemies")`. Prefer group checks over layer checks when deciding whether to call `TakeDamage`.
- Scene vs code: Many game behaviors combine scene settings and script logic. For example, weapon Areas use both scene `collision_mask` and script signal hookups (`Connect("area_entered", ...)`). When changing behavior, update both the `.tscn` and the script initialization where Areas are created at runtime (e.g., `SpiritualWeapon.cs`).
- Weapon data: `Weapon.GetArcaneWeapons()` returns a hard-coded list used as a data source. To add weapons, update this method and then ensure scenes/scripts that instantiate projectiles reference the new `ProjectileScenePath` or have the handler added to `Player` (e.g., adding PackedScene exports).

Key files to inspect for common edits
- `scripts/Player.cs` — movement, hurtbox, weapons, and firing loop.
- `scripts/Enemy.cs` — knockback, movement, health, death and XP drops.
- `scripts/Weapon.cs` — weapon definitions and level-up logic.
- `scripts/MagicMissile.cs`, `scripts/ArcaneExplosion.cs`, `scripts/SpiritualWeapon.cs` — projectile/ability implementations. Check `Connect(...)` calls and collision handling here.
- `scenes/*.tscn` — required to check `CollisionLayer`/`CollisionMask`, `CollisionShape2D`, exported PackedScene references and node paths.

Developer workflows & commands
- Run/play locally: Use Godot 4.5 (project file lists `features = ["4.5", "C#"]`). The repository includes a VS solution and csproj files — open in Godot or your IDE. To run from workspace, use the provided VS Code task labelled "Run Godot Project" which executes `godot` in the workspace root.
- Editing scenes: prefer using the Godot editor for collision layers and bitmask values when possible. If editing `.tscn` by hand, be careful with `collision_layer` and `collision_mask` integer bitmasks (layer bit values are powers of two: 1,2,4,8...).
- C# builds: Godot will build C# assemblies when running the project. If editing C# files outside Godot, re-open or refresh Godot to pick up compiled assemblies.

Patterns and anti-patterns spotted
- Pattern: Damage and interactions rely primarily on group membership (`IsInGroup("enemies")`) and method existence (`HasMethod("TakeDamage")`) rather than strictly typed references. This is intentional for decoupling scenes.
- Anti-pattern to avoid: Changing collision layer numbers without updating masks in both scenes and runtime-created Areas will break hit detection. Always update both.

Integration points and external dependencies
- Godot engine (4.5) with C# (.NET). No external NuGet packages appear to be required.
- Audio and textures are referenced from `assets/` and packed into scenes. Weapon scenes reference their projectile scenes via `ProjectileScenePath` and `PackedScene` exports on `Player`.

How to make safe edits (examples)
- Adding a new weapon: add its entry to `Weapon.GetArcaneWeapons()`; add an exported `PackedScene` property to `Player` if the weapon needs a scene reference; create `scenes/YourWeapon.tscn` and script handling with `Area2D` collision mask set to detect enemies (mask=2 in current convention). Ensure `Player._Ready()` initializes weaponFireTimers for the new `WeaponId`.
- Adjusting collision behavior (player should not be blocked by enemies): update enemy `.tscn` nodes to place enemy `CharacterBody2D` on layer 2 and set their `collision_mask` appropriately for world collisions; set `Player` HurtBox `collision_mask` to include 2, and ensure all weapon Areas have `collision_mask` set to 2 or set in runtime creation.

Editing rules for AI agents (concise)
1. Read `scripts/Player.cs` and `scripts/Enemy.cs` before changing physics or collision behaviors. Many interactions assume the `enemies` group and `ApplyKnockback`/`TakeDamage` methods exist.
2. If touching collision layers, update both `.tscn` files and any runtime-created Areas (search for `new Area2D()` in the codebase) to keep masks/layers consistent.
3. When adding functionality to the player or weapons, prefer hooking into existing weapon loop in `Player._PhysicsProcess` rather than sprinkling ad-hoc timers in new code.
4. Use `GetTree().GetNodesInGroup("enemies")` to iterate enemies when implementing area effects (ArcaneExplosion follows this pattern in `ArcaneExplosion.cs`).
5. Keep change size small and test in Godot: prefer incremental single-feature edits with runtime validation (play the scene) to confirm signal/collision behavior.

If anything above is unclear or you want more specifics (e.g., sample change to add a new weapon with code + scene), tell me which area and I will expand with concrete code/scene steps.
This project: Wizard Survivors — focused instructions for AI coding agents

Goal
- Help contributors and automated coding agents make safe, small, and correct edits to this Godot 4 (C#) project. Focus on runtime behavior, scene files (.tscn), and Godot/C# conventions used here.

Quick architecture summary
- Godot 4 project using C# (.NET). Entry point scenes and logic live under `scenes/` and `scripts/`.
- Main game node: `scenes/node_2d_game.tscn` / `scripts/Node2DGame.cs` — spawns enemies and owns the `Player` node (`CharacterBody2D` at `CharacterBody2D` path inside the scene). Use this file to understand spawn, UI hooks, and level-up flow.
- Player logic: `scripts/Player.cs` — movement (CharacterBody2D + `MoveAndSlide()`), HurtBox `Area2D` for damage detection, equipped weapon management, and firing logic.
- Enemy logic: `scripts/Enemy.cs` — `CharacterBody2D` that moves toward the player and supports `ApplyKnockback(Vector2)` and `TakeDamage(int)`. Enemies are added to group "enemies" in `_Ready()`.
- Weapons: `scripts/Weapon.cs` defines weapon data (Id enum, stats). Projectile/ability scenes and scripts live in `scenes/` and `scripts/` (e.g., `MagicMissile.tscn` + `scripts/MagicMissile.cs`, `ArcaneExplosion.tscn` + `scripts/ArcaneExplosion.cs`, `SpiritualWeapon.cs`). Weapons use Area2D signals (`area_entered`, `body_entered`) to deal damage to nodes in group `enemies`.

Project-specific conventions to follow
- Godot layers: the project uses custom collision layers for gameplay separation. Recent edits put enemies on layer 2 and the player's main `CharacterBody2D` on the default layer (1). Weapon Areas and the player's HurtBox must have their `collision_mask` include the enemy layer (2) to detect enemies. Always check both `.tscn` and runtime-created `Area2D` instances for `CollisionMask` handling.
- Group usage: dynamic checks and damage logic rely heavily on Godot groups. Enemies call `AddToGroup("enemies")` in `_Ready()` and weapons/damage checks use `IsInGroup("enemies")`. Prefer group checks over layer checks when deciding whether to call `TakeDamage`.
- Scene vs code: Many game behaviors combine scene settings and script logic. For example, weapon Areas use both scene `collision_mask` and script signal hookups (`Connect("area_entered", ...)`). When changing behavior, update both the `.tscn` and the script initialization where Areas are created at runtime (e.g., `SpiritualWeapon.cs`).
- Weapon data: `Weapon.GetArcaneWeapons()` returns a hard-coded list used as a data source. To add weapons, update this method and then ensure scenes/scripts that instantiate projectiles reference the new `ProjectileScenePath` or have the handler added to `Player` (e.g., adding PackedScene exports).

Key files to inspect for common edits
- `scripts/Player.cs` — movement, hurtbox, weapons, and firing loop.
- `scripts/Enemy.cs` — knockback, movement, health, death and XP drops.
- `scripts/Weapon.cs` — weapon definitions and level-up logic.
- `scripts/MagicMissile.cs`, `scripts/ArcaneExplosion.cs`, `scripts/SpiritualWeapon.cs` — projectile/ability implementations. Check `Connect(...)` calls and collision handling here.
- `scenes/*.tscn` — required to check `CollisionLayer`/`CollisionMask`, `CollisionShape2D`, exported PackedScene references and node paths.

Developer workflows & commands
- Run/play locally: Use Godot 4.5 (project file lists `features = ["4.5", "C#"]`). The repository includes a VS solution and csproj files — open in Godot or your IDE. To run from workspace, use the provided VS Code task labelled "Run Godot Project" which executes `godot` in the workspace root.
- Editing scenes: prefer using the Godot editor for collision layers and bitmask values when possible. If editing `.tscn` by hand, be careful with `collision_layer` and `collision_mask` integer bitmasks (layer bit values are powers of two: 1,2,4,8...).
- C# builds: Godot will build C# assemblies when running the project. If editing C# files outside Godot, re-open or refresh Godot to pick up compiled assemblies.

Patterns and anti-patterns spotted
- Pattern: Damage and interactions rely primarily on group membership (`IsInGroup("enemies")`) and method existence (`HasMethod("TakeDamage")`) rather than strictly typed references. This is intentional for decoupling scenes.
- Anti-pattern to avoid: Changing collision layer numbers without updating masks in both scenes and runtime-created Areas will break hit detection. Always update both.

Integration points and external dependencies
- Godot engine (4.5) with C# (.NET). No external NuGet packages appear to be required.
- Audio and textures are referenced from `assets/` and packed into scenes. Weapon scenes reference their projectile scenes via `ProjectileScenePath` and `PackedScene` exports on `Player`.

How to make safe edits (examples)
- Adding a new weapon: add its entry to `Weapon.GetArcaneWeapons()`; add an exported `PackedScene` property to `Player` if the weapon needs a scene reference; create `scenes/YourWeapon.tscn` and script handling with `Area2D` collision mask set to detect enemies (mask=2 in current convention). Ensure `Player._Ready()` initializes weaponFireTimers for the new `WeaponId`.
- Adjusting collision behavior (player should not be blocked by enemies): update enemy `.tscn` nodes to place enemy `CharacterBody2D` on layer 2 and set their `collision_mask` appropriately for world collisions; set `Player` HurtBox `collision_mask` to include 2, and ensure all weapon Areas have `collision_mask` set to 2 or set in runtime creation.

Editing rules for AI agents (concise)
1. Read `scripts/Player.cs` and `scripts/Enemy.cs` before changing physics or collision behaviors. Many interactions assume the `enemies` group and `ApplyKnockback`/`TakeDamage` methods exist.
2. If touching collision layers, update both `.tscn` files and any runtime-created Areas (search for `new Area2D()` in the codebase) to keep masks/layers consistent.
3. When adding functionality to the player or weapons, prefer hooking into existing weapon loop in `Player._PhysicsProcess` rather than sprinkling ad-hoc timers in new code.
4. Use `GetTree().GetNodesInGroup("enemies")` to iterate enemies when implementing area effects (ArcaneExplosion follows this pattern in `ArcaneExplosion.cs`).
5. Keep change size small and test in Godot: prefer incremental single-feature edits with runtime validation (play the scene) to confirm signal/collision behavior.

If anything above is unclear or you want more specifics (e.g., sample change to add a new weapon with code + scene), tell me which area and I will expand with concrete code/scene steps.
