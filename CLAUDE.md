# Wizard Survivors — agent instructions

Godot 4.5 Mono (.NET 9), C# 2D roguelite auto-shooter (Vampire Survivors–like).
`scripts/` (~80 `.cs`) and `scenes/` (~40 `.tscn`) are **flat** — no subdirectories. `.tres` resources and `Global.gd` live at the **repo root**, not in a resources folder.

## Design docs — read before designing

`.ai/` is the design source of truth. Read the relevant doc before any balance, design, or content decision. Do not restate its contents here.

- `.ai/project-overview.md` — pitch, scope, pillars
- `.ai/gameplay-design.md` — the 12 elements, element tier thresholds, upgrade economy
- `.ai/technical-architecture.md` — system boundaries and data flow
- `.ai/godot-engine.md` — engine conventions
- `.ai/roadmap.md`, `.ai/versioning.md`, `.ai/content-pipeline.md`
- `.ai/issues/` — 13 backlog specs. Some predate work that has since landed; check the code before trusting one.
- `.ai/decisions/` — ADRs

## Build & run

- **`dotnet build WizardSurvivors.sln`** — the default verification loop. ~6s, no Godot needed, catches essentially all C# errors.
- **`"$GODOT_BIN" --path .`** — run the game. `$GODOT_BIN` is set in `.claude/settings.json`; if it is unset or missing, stop and say so rather than guessing a path.
- **First run in a fresh worktree needs a one-time import**: `"$GODOT_BIN" --headless --path . --import`. It processes ~2000 PNGs and takes many minutes. Until it finishes, `.godot/imported/` is empty and `.godot/uid_cache.bin` does not exist, so every scene load fails with `Unrecognized UID` or `Failed loading resource`. Those errors mean "not imported yet", **not** "your change broke something".
- `run/main_scene` in `project.godot` is a **UID** (`uid://beb5w8ip125sd` → `scenes/TitleScreen.tscn`), which only resolves once `uid_cache.bin` exists. Before then, launch a scene by explicit path: `"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit`.

**`dotnet build` does not validate `.tscn` wiring, resource paths, or signal connections — only Godot does.** When you have only run a build, say so explicitly in your summary and list the in-Godot checks that remain.

## The `Global` trap

Two different things share the name `Global`:

- `Global.gd` (repo root) is the **registered autoload** in `project.godot` — a 4-line GDScript that **nothing reads**.
- `scripts/Global.cs` is a plain `public static class Global` (not a Node, not an autoload). **All real code uses this one**: `SelectedCharacterIdx`, `SelectedStageIdx`, `TestWizardStartingSpellId`.

Access it as `Global.SelectedCharacterIdx`. Never `GetNode("/root/Global")` — that returns the dead GDScript.

## Architecture

Scene flow: TitleScreen → MainMenu → CharacterSelection → StageSelection → `scenes/node_2d_game.tscn` / `scripts/Node2DGame.cs` → GameOverScreen → MainMenu. Selection state passes through the static `Global` class above.

`scripts/Player.cs` (`CharacterBody2D`): movement, HP pipeline, equipped-spell firing loop (`spellFireTimers`, max 6 slots via `MaxSpellSlots`), level-up options (`GetLevelUpOptions`), element tier counting (`GetElementInstanceCounts` / `GetElementTier`).

`scripts/Enemy.cs` (`CharacterBody2D`): `TakeDamage`, `ApplyKnockback`, `ApplySlow` (0 = freeze/root), `ApplyPoison`. The latter two are **stacking-resistant** — they take max magnitude + max remaining duration, not additive. Always joins group `"enemies"` in `_Ready()`.

**Death is animated, not instant.** At 0 HP, `StartDeath()` drops rewards immediately, then leaves the `"enemies"` group and zeroes collision *in the same frame* so a corpse can never be targeted, damaged, or bump the player — only then does it play the `death` animation and `QueueFree` on finish. An enemy whose `SpriteFrames` has no `death` animation (e.g. `BooEnemy`) frees immediately instead. The `isDying` flag guards `TakeDamage` and all four `Apply*` status methods. If you add an enemy type, either give its `SpriteFrames` a non-looping `death` animation or rely on that fallback.

Each enemy type has its **own** `SpriteFrames` in `scenes/resources/` (`EnemyFrames` = skeleton1, `FastEnemyFrames` = skeleton2, `TankEnemyFrames` = vampire). They are also tinted via `modulate` and scaled differently in their `.tscn`. Silhouette is the primary readability signal — do not collapse types back onto one sheet.

Spells are **data-driven** through `SpellData` `.tres` resources: `scripts/SpellData.cs`, `SpellEffect.cs`, `SpellLevelUpgrade.cs`, `Element.cs`. **`scripts/Weapon.cs` is legacy — never extend it for new content.**

Meta progression persists via `scripts/SaveData.cs` + the `SaveManager` autoload to `user://savegame.json` (unlocked characters/stages, max difficulty cleared, `ArcaneUpgradeLevels`).

## Damage pipeline (`Player.TakeDamage`, scripts/Player.cs:1246)

Order matters, and passive-spell work depends on it:

1. `IsDead` early-out.
2. **Dodge roll** — `min(0.75, Σ PassiveSpellEffect.GetDodgeChance())`. On a dodge the hit is fully avoided: **no signal, no HP loss, no reactions.**
3. `EmitSignal(DamageTaken, amount)` — reactive passives (Thornmail, Stormguard, Frozen Bulwark) fire here, on the **raw, pre-mitigation** amount.
4. Darkness % reduction (`GetDarknessDamageReductionPercent`).
5. Chest % reduction (`GetChestDamageReductionPercent`, +0.15 when `shieldPoints > 0` and Protective Ward is owned).
6. Shield pool absorb (`shieldPoints`).
7. Flat reduction — `Σ PassiveSpellEffect.GetFlatDamageReduction()` + Metal.
8. `CurrentHP -= mitigated`.
9. Bastion of Spikes retaliation if HP drops below 30%.
10. Death: spend an extra life and heal to `MaxHP / 2`, otherwise emit `Died`.

`DamageTaken` carries the **pre-mitigation** amount. A passive that should scale off damage *actually dealt* must read the HP delta, not the signal argument.

## Collision layers (verified from `.tscn` files — not Godot defaults)

- **Layer 2** (`collision_layer = 2`, `collision_mask = 1`): all enemies (`enemy.tscn`, `FastEnemy.tscn`, `TankEnemy.tscn`).
- **Layer 3** (`collision_layer = 3`, `collision_mask = 3`): player body + HurtBox (`player.tscn`).
- Weapon `Area2D` scenes (`MagicMissile.tscn`, `ArcaneExplosion.tscn`) use `collision_mask = 2` to detect enemies.

Changing a node's `collision_layer` requires updating **every** `collision_mask` that must detect it — in `.tscn` files *and* runtime-created areas (search `new Area2D()`). Prefer `IsInGroup("enemies")` for gameplay logic; use layers only for physics separation.

**Collision bugs in this project are usually mask mismatches, not script logic errors.**

## Adding content

**New active spell:**
1. `SpellData_<Name>.tres` at the **repo root** (that is where they all live), with element weights and level-up data.
2. `scenes/<Name>.tscn` + `scripts/<Name>.cs`.
3. Wire firing into `Player`'s existing `spellFireTimers` loop — do **not** add a parallel timer system.
4. **Mandatory:** add `"res://SpellData_<Name>.tres"` to the hardcoded `SpellResourcePaths` array in `scripts/ContentValidator.cs:11`. A resource missing from that allowlist is silently never validated.
5. Verify hit detection against the `"enemies"` group.

**New passive/defensive spell:**
1. Subclass `scripts/PassiveSpellEffect.cs`. **Code-only — no `.tscn`.**
2. Override `OnPlayerDamaged(int)` for reactive behavior, or set `UsesPulseTimer = true` + override `OnPulseTick()` for auras.
3. Register alongside the existing ten via `CreateDefensiveSpellData(...)` / `RefreshPassiveSpellInstance<T>()` around `scripts/Player.cs:408`.
4. Check the effect against the `TakeDamage` ordering above so it triggers at the intended stage.

## Validation

- `ContentValidator.ValidateAtStartup()` runs from `scripts/MainMenu.cs:88` and `scripts/Node2DGame.cs:224`, and folds in `RegressionChecks.RunAll()`. Output goes to the Godot console as `ContentValidator:` lines. **There is no way to run it without launching Godot.**
- `scripts/ChestItemIntegrationTest.cs` is a `Node` you attach to a scene; it asserts in `_Ready()`.
- `validate_chest_system.py` **cannot run** — this machine has only Python 2.7 and the script is Python 3.
- There is no CI, no test framework, and no `dotnet test`. Don't invent one without asking.

## Conventions

- **Tabs** for indentation (`.editorconfig` sets no indent rule, so match the file). LF line endings.
- Node scripts are `public partial class X : GodotType`. Tunables are `[Export] public T Name { get; set; }`.
- Signals: `[Signal] public delegate void FooEventHandler(...)`, emitted via `EmitSignal`, connected with `new Callable(this, nameof(Handler))` and disconnected in `_ExitTree()`.
- Duck-typing over strict types: `IsInGroup("enemies")` + `HasMethod("TakeDamage")`. This decoupling is intentional — follow it.
- AoE iterates `GetTree().GetNodesInGroup("enemies")`.
- Namespaces are inconsistent — `WizardSurvivors.scripts` in most data/helper files, global namespace for node-attached classes like `Player`/`Enemy`/`Node2DGame` (a Godot requirement). Match the file you are editing; don't crusade.
- Comment density is high and comments cite issue numbers (`issue #22`, `#13`) and explain rationale rather than mechanics. Match that.

## Repo gotchas

- This checkout is a **git worktree** on an `agents/*` branch. The main clone is at `C:/Users/ericc/Documents/GitHub/wizard-survivors` with four sibling worktrees. Never edit outside this worktree.
- Assets are **Git LFS** (`assets/imported/fantasy/**`, `assets/organized/**`). A ~130-byte text file where a PNG should be means `git lfs pull`.
- `.godot/` is generated and gitignored; don't read it and don't delete it casually (66 MB of assets reimport).
- `.ai/archive/` holds 14 point-in-time status reports (chest-item write-ups, completion summaries). **Historical — do not treat as current.** `CLAUDE.md` is the only `.md` at the repo root; keep it that way.

## Don't

- Extend `Weapon.cs`. All new content goes through `SpellData`.
- Add new root-level `UPPERCASE_STATUS.md` files. Durable notes go in `.ai/`.
- Hand-edit collision bitmasks without checking every mask that must still match.
- Create subdirectories under `scripts/` or `scenes/`.
- Swallow exceptions or add silent fallbacks around gameplay state.
- Allocate per-frame in swarm-heavy effects.
- Call a change verified when you have only run `dotnet build`.
