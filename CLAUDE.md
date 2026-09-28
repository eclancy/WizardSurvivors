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
- `.ai/spell-variety.md` — why most spells feel alike (22 of 23 auto-aim on a fixed timer), the axes of weapon variety the genre uses that we do not, the phased plan for closing that, and the item ideas worth taking from Vampire Survivors and Magic Survival. **Read it before adding a spell or a chest item.**
- `.ai/spell-roster.md` — the roster rethink: all 24 spells with a distinct aim, cadence, demand and payload, the three collisions that caused the sameness, and the dependency order for building it.
- `.ai/passives-and-items.md` — boons, item rarity and Full Set Enchantments; section 3a holds the element-carrier audit and the two reactive boons.
- `.ai/enemy-behaviour.md` — the plan for per-enemy behaviour (splitting blobs, circling wolves, teleporting mages) and the Halls of Torment inspiration pass.
- `.ai/art-inventory.md` — what art we own vs. actually reference, and where the unused art could go. **Read it before adding art or concluding we lack a sprite**; note especially that 420 world props are mis-filed under `assets/organized/ui/`.
- `.ai/world-and-tone.md` — the premise, and the five concrete things it decides: why the player
  is alone, why the dead have no faction colour, why the player sits on the 48×48 cell, and the
  proportion rules for drawing him. **Read it before deciding tone, palette family or silhouette
  for any character.** `art-direction.md` is still the contract; this says what to point it at.
- `.ai/art-direction.md` — the visual contract for all *new* art: cell sizes, the Bonelight light model and palette, silhouette taxonomy, animation and import settings, and the migration order. **Read it before drawing, tinting, or scaling anything.** The existing art predates it.
- `.ai/art-replacement-manifest.md` — the art backlog: every sheet, tile, effect and icon still to be redrawn, which sheets are shared by several jobs, and what draws itself and needs no art at all.
- `.ai/audio-direction.md` — the sound contract: why the set is synthesised, the mono/WAV format rules, the twelve-element voice palette, the loudness table (how loud a sound is depends on how *often* it plays, not how important it feels), the frequency budget, and the Godot integration notes. **Read it before adding, retuning or wiring any sound.**
- `.ai/audio-manifest.md` — the audio inventory and wiring backlog: all 69 generated files, what each is for, the proposed trigger site for each, the wiring order, and what is deliberately absent. **Every file is generated; none is wired yet.**
- `.ai/title-screen.md` — the shipping title screen: how the scene avoids fractional scaling, the draw order, every knob, and four bugs not to reintroduce. **Read it before touching `TitleScreen.tscn`, `tools/art/splash.py` or `tools/art/hero.py`.** The art is generated, not painted: edit the generator and re-run it, never the PNG.
- **The backlog lives on GitHub**, not in this repo — `gh issue list`, or the `/issue` command. `.ai/issues/` was migrated there and removed. Several open issues predate work that has since landed, so check the code before assuming one is still open.
- `.ai/asset-licensing.md` — the audit behind "can this repo be public". Short answer: no. The
  bought packs under `assets/organized/` are CraftPix and still hold up every ground tile, every
  world prop, the chests and traps, several spell VFX and the default spell icon; and 54 commits
  of history carry them even if the files are deleted. **Read it before deleting anything under
  `assets/`** — 26 of our own generated spell icons are mis-filed inside the pack directory.
- `.ai/decisions/` — ADRs

## Build & run

- **`dotnet build WizardSurvivors.sln`** — the default verification loop. ~6s, no Godot needed, catches essentially all C# errors.
- **`"$GODOT_BIN" --path .`** — run the game. `$GODOT_BIN` is set in `.claude/settings.json`; if it is unset or missing, stop and say so rather than guessing a path.
- **A fresh worktree needs an import — or a seeded cache.** `tools/new-worktree.sh` copies `.godot/` for you, which is the fast path. Without it: `"$GODOT_BIN" --headless --path . --import`, which processes ~2000 PNGs and takes many minutes. Until one or the other has happened, `.godot/imported/` is empty and `.godot/uid_cache.bin` does not exist, so every scene load fails with `Unrecognized UID` or `Failed loading resource`. Those errors mean "not imported yet", **not** "your change broke something".
- `run/main_scene` in `project.godot` is a **UID** (`uid://beb5w8ip125sd` → `scenes/TitleScreen.tscn`), which only resolves once `uid_cache.bin` exists. Before then, launch a scene by explicit path: `"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit`.

**`dotnet build` does not validate `.tscn` wiring, resource paths, or signal connections — only Godot does.** When you have only run a build, say so explicitly in your summary and list the in-Godot checks that remain.

## Working alongside other sessions

Several sessions run against this repo at once. **One session, one worktree, one branch** — sessions sharing a checkout share the working tree, the index *and* HEAD, so they silently overwrite each other's edits and rewrite each other's commits. That is not a merge conflict anyone gets told about; it looks like your own file reverting under you mid-task.

```
tools/new-worktree.sh art/beards      # -> ../ws-art-beards, branch art/beards, .godot seeded
git worktree remove ../ws-art-beards  # when done
```

Seeding works because **`.godot/` holds no absolute paths** — the `.md5` files are content hashes and the 2217 `*.import` files are tracked — so the cache is portable between worktrees of this project. Verified: a seeded worktree builds and loads `TitleScreen.tscn` headless with no reimport. `bin/`, `obj/` and `.godot/` are gitignored and per-directory, so parallel `dotnet build` never collides.

Whether or not you are in your own worktree:

- **Stage explicit paths. Never `git add -A` or `git add .`** — other sessions leave unrelated work in the tree, and `-A` sweeps it into your commit. This has already happened once: 87 files of audio work landed in an art commit.
- **Do not rebase or amend a branch another session might be holding.**
- Merge to `main` at the end of a piece of work, not continuously.
- If a file you edited reverts, or a commit hash you made changes, another session is in your tree. Say so rather than re-applying blindly.

## The `Global` trap

Two different things share the name `Global`:

- `Global.gd` (repo root) is the **registered autoload** in `project.godot` — a 4-line GDScript that **nothing reads**.
- `scripts/Global.cs` is a plain `public static class Global` (not a Node, not an autoload). **All real code uses this one**: `SelectedCharacterIdx`, `SelectedStageIdx`, `TestWizardStartingSpellId`.

Access it as `Global.SelectedCharacterIdx`. Never `GetNode("/root/Global")` — that returns the dead GDScript.

## Architecture

Scene flow: TitleScreen (**hosts MainMenu as a child** — pressing a key adds the menu over the artwork rather than changing scene; everything that returns "to the main menu" loads TitleScreen with `Global.OpenMenuImmediately = true`) → CharacterSelection → StageSelection → `scenes/node_2d_game.tscn` / `scripts/Node2DGame.cs` → GameOverScreen → MainMenu. Selection state passes through the static `Global` class above.

`scripts/Player.cs` (`CharacterBody2D`): movement, HP pipeline, equipped-spell firing loop (`spellFireTimers`, max 6 slots via `MaxSpellSlots`), level-up options (`GetLevelUpOptions`), element tier counting (`GetElementInstanceCounts` / `GetElementTier`).

`scripts/Enemy.cs` (`CharacterBody2D`): `TakeDamage`, `ApplyKnockback`, `ApplySlow` (0 = freeze/root), `ApplyPoison`. The latter two are **stacking-resistant** — they take max magnitude + max remaining duration, not additive. Always joins group `"enemies"` in `_Ready()`.

**Death is animated, not instant.** At 0 HP, `StartDeath()` drops rewards immediately, then leaves the `"enemies"` group and zeroes collision *in the same frame* so a corpse can never be targeted, damaged, or bump the player — only then does it play the `death` animation and `QueueFree` on finish. An enemy whose `SpriteFrames` has no `death` animation (e.g. `BooEnemy`) frees immediately instead. The `isDying` flag guards `TakeDamage` and all four `Apply*` status methods. If you add an enemy type, either give its `SpriteFrames` a non-looping `death` animation or rely on that fallback.

**Enemy attack patterns.** An enemy with behaviour is an `Enemy` subclass, not a state-machine framework. Four shared pieces carry all of them:

- `AttackTelegraph` — the wind-up clock: cooldown, visible wind-up, resolve once. `Prime()` clears the cooldown so the next tick winds up at once; it can never skip a wind-up.
- `GroundSlamAttack` — a telegraphed hit anchored on its owner, in a `TelegraphShape` of `Ring`, `Cone` or `Line`. **`Contains()` is the single authority on every shape and both the damage and the paint read it**, so a shape cannot be drawn one size and hit at another. It resolves at the owner's position *on the frame it lands*, which is what makes a travelling telegraph free.
- `BurrowMove` — one dive: under, travel, up. It does **not** make its owner untargetable; that is `Enemy.SetTargetable`, so there is only ever one authority on group membership.
- `TelegraphedGroundHit` — a warning placed on the ground away from whoever caused it. Code-created `Node2D`, no scene, and deliberately not an `Area2D`: the hit is measured once against the shape the player was shown.

Enemy subclasses: `RangedEnemy`, `LungerEnemy`, `SlammerEnemy` (its `SlamShape` can be a cone — that is all the Rime Guard is), `ExploderEnemy`, `SummonerEnemy`, `BossEnemy`.

**`BossEnemy` is both Elderbark and the base of the other seven.** Used bare it is chapter one. A subclass overrides five hooks and nothing else: `BuildSlam()` (what the attack *is*), `BuildPhaseThresholds()` (health fractions where the fight changes), `OnPhaseEntered(n)`, `PlantsDuringWindUp` (false for a charge), `WantsToAttack(...)`. `Enemy.SetTargetable(bool)` is the untargetable-but-alive primitive every burrower rides on. Chapter bosses: `GaolerBoss`, `HollowChoirBoss`, `MotherRotBoss`, `ArchivistBoss`, `StillWardenBoss`, `LongCoilBoss`, `DeepWardenBoss` — see `.ai/enemy-behaviour.md` for what each fight asks and why.

Steering hooks into `Enemy.AdjustSteering(chaseDirection, distanceToPlayer)`. **The returned vector's length is a speed multiplier** — return a unit vector to move at `Speed`, something longer to move faster (the Lunger's dash), or `Vector2.Zero` to hold exactly still, which also suppresses the separation nudge so a planted wind-up cannot drift off the tell it is drawing.

Every telegraphed enemy must also override `ResetForRespawn` to rebuild its telegraph. `Node2DGame.RespawnEnemy` recycles a far-away enemy to the spawn ring, and one that arrives with a wind-up already banked attacks before the player has seen it. Bosses are exempt — elites are never recycled.

Two validators cover what `dotnet build` cannot see: `RegressionChecks.ValidateAttackPatternEnemies` for the ordinary enemies and `ValidateBossPatterns` for the eight bosses — above all that no wind-up is zero, and for the bosses that a charge tell is strictly between 0 and 1 and a burrow outlasts the tell it places.

**Driving a boss fight headless:** `"$GODOT_BIN" --headless --path . scenes/_BossProbe.tscn -- --stage=4`. It shortens the survival clock and then bleeds the boss on a fixed timer, so every phase threshold is reached in a known time rather than depending on what the level-up RNG handed the bot. `--focus` drains only the first body and exists for the Hollow Choir, whose three voices otherwise die on the same frame and never exercise the revive. It renders nothing — **whether a fight is readable still needs eyes.**

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

## Unlocks and the campaign

`scripts/UnlockCatalog.cs` is the single authority on how every spell and character is obtained
(`Starter` / `Achievement` / `Purchase` / `Discovery`). `GlobalStatsManager` reads it;
`DefaultUnlockedSpellIds` is gone, surviving only as `LegacyDefaultUnlockedSpellIds` for save
migration. **A spell or character with no catalog entry is permanently locked**, and
`RegressionChecks.ValidateUnlockCatalog` reports it — so adding content means adding an entry.

`scripts/StageCatalog.cs` is the chapter roster and the only place a stage is named. It replaced
four disagreeing copies (`StageSelection`, `GameOverScreen`, two switches in `Node2DGame`, and
`StageEnvironmentCatalog.GetForStageIndex`). Chapters with `IsPlayable = false` are listed but not
enterable; flipping that is the last step of building one, not the first.

`SaveData.CurrentSchemaVersion` is 8 and **`SaveData.Migrate()` is now real** — it runs from
`SaveManager.LoadGame`. Before 8 there was no migration at all. Any change that narrows what a save
implicitly grants must add a migration step, or it silently confiscates content from existing
players.

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
- Duck-typing over strict types: `IsInGroup("enemies")` + `HasMethod("TakeDamage")`. This decoupling is intentional — follow it. **But pass every argument, including ones with C# defaults.** `Enemy.TakeDamage(int, bool = false)` is matched by Godot on name *and* argument count, so `Call("TakeDamage", n)` throws `Nonexistent function 'TakeDamage' in base 'Enemy'` and deals nothing — while `HasMethod` still returns true, which is what makes it silent. The player's own `TakeDamage(int)` takes one, so the same line is correct against the player and broken against an enemy.
- AoE iterates `GetTree().GetNodesInGroup("enemies")`.
- Namespaces are inconsistent — `WizardSurvivors.scripts` in most data/helper files, global namespace for node-attached classes like `Player`/`Enemy`/`Node2DGame` (a Godot requirement). Match the file you are editing; don't crusade.
- Comment density is high and comments cite issue numbers (`issue #22`, `#13`) and explain rationale rather than mechanics. Match that.

## Repo gotchas

- **The application icon is a crop of the title screen.** `python tools/art/app_icon.py` frames
  the wizard out of `assets/bonelight/ui/title-screen.png` and writes `icon.png` (the Godot
  project manager, via `config/icon`) and `icon.ico` (the exported Windows binary, via
  `config/windows_native_icon`). It **only reads** that PNG - `.ai/title-screen.md` still governs
  it, and the art is still edited by changing `splash.py` and re-running, never by touching the
  image. Re-run the pipeline and the icon follows. The framing was chosen by comparing candidates
  at **16 pixels**, which is the size that actually decides an icon. The stock Godot robot it
  replaced is gone.
- Worktrees are **siblings of the repo root**, named `ws-<branch with slashes as dashes>` (`../ws-art-beards`). `git worktree list` is the truth about what exists; don't assume either one checkout or a fleet. See "Working alongside other sessions" above.
- **Only Python 2.7 is installed**, with PIL. The art generators under `tools/art/` and the audio generators under `tools/audio/` are written for it. There is no `python3`, no numpy, no ImageMagick, no audio encoder, and no virtualenv.
- **The kids' art is the one exception to everything below, and it is never regenerated.**
  Eric's kids drew the twelve sheets in `assets/kidsart/source/`, and they are the entire cast of
  chapter 8 (The Sketchbook). `tools/art/kids_art.py` only ever *moves* those pixels - crop,
  repack into square cells, key white out of the one JPEG. **Do not repalettise them, redraw them
  to `.ai/art-direction.md`, list them in the replacement backlog, or derive hurt/death frames for
  them**: a derived frame is art they did not draw going on screen under their name, and
  `Enemy.StartDeath` already frees an enemy whose `SpriteFrames` has no `death`. Anything in that
  directory that IS generated - the squared-paper ground - says so in a comment. Same reasoning as
  `assets/testwizard/`, which keeps Eric's own drawing outside `assets/bonelight/`.
- **Art and audio are generated, not authored.** `python tools/art/build.py` writes the sprites; `python tools/audio/build.py` writes `assets/sfx/` (~90 s) and `python tools/audio/analyse.py --confuse` audits how distinguishable the sounds are. Edit the generator and re-run — never hand-edit a generated `.png` or `.wav`.
- **Do not put backticks or apostrophes inside a Bash heredoc here.** The shell substitutes and mis-parses them even inside a quoted delimiter, which silently mangles Markdown and Python. Use the Write/Edit tools for any file containing them.
- Assets are **plain Git objects, not LFS** — `git lfs ls-files` returns nothing, so a missing or malformed image is never a "run `git lfs pull`" problem. `.gitattributes` explains why LFS is deliberately off and what to check before turning it on.
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
