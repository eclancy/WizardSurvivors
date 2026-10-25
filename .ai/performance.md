# Performance: what a frame costs, and how to measure it

"The game lags terribly on my phone as the spawns pick up." This is what that turned out to be,
what was done, how to measure it again, and the rules that keep a full swarm cheap. Every number
here was measured (2026-10-23/24, branch `perf/phone`); none is an estimate.

## 1. The budget

60 fps is 16.7 ms a frame. On the phone the frame is: the physics steps (enemy steering and
`MoveAndSlide`, the player, projectiles), the process callbacks (the game, spells, orbs, damage
numbers), the engine's own work (the physics solver, area signals, deferred calls) and rendering,
all on one thread - the Compatibility renderer runs single-threaded.

**A slow frame makes the next one slower.** Godot runs as many 60 Hz physics steps as the last
frame took (up to `physics/common/max_physics_steps_per_frame`, default 8), so a frame that runs
long is followed by one that steps physics twice, then three times. Once one step costs more than
about 16 ms the game falls into it and stays there: the PC harness sat at 4-8 steps a frame and
5-10 fps. That is the "lags terribly", and why the cost of one physics step is the number that
matters most.

## 2. What it was

Measured on the desktop the harness runs on (an i7-1065G7 laptop, headless, Pyromancer, 8 minutes,
`--immortal --all-unlocked --timescale=1`), from a CPU trace (`dotnet-trace`) and the profiler below.
Late game, ~125 enemies:

| Cost | Share | What it was |
|---|---|---|
| Separation steering | ~55% of the main thread | Every enemy, every fourth step, walked `GetTree().GetNodesInGroup("enemies")`. That builds a native array, and every element read out of it crosses the interop boundary and looks the object up again (`godotsharp_instance_from_id`), then `GlobalPosition` crosses again. At 140 enemies: ~35 arrays and ~5,000 marshalled reads per step. 7.5 ms of a 13 ms step. |
| `MoveAndSlide` | ~20% | 140 bodies in contact with each other. Two thirds of its cost is enemy-enemy contact (measured by switching that mask off in a stress run: 20 us a call to 6.5 us). Inherent; see section 7. |
| Allocation | 180-200 KB a frame, a GC every ~1.5 s | Mostly `Player.GetElementInstanceCounts`, rebuilt (a dictionary per spell, a walk of each spell's Godot dictionary of tags) on every spell's cooldown tick and several times a hit. Also a `StringName` or `NodePath` built from a string literal at every hot call site, a Tween per hit, a scene instance per damage number. |
| Damage numbers | ~0.4 ms each | `FloatingText` instanced a scene and built a `Label` per hit. |
| Debug prints | 6% | `GD.Print` per XP orb and per Spiritual Weapon hit. On a phone each is a write to logcat. |
| Tile draw calls | 1,600-3,300 a frame | Every 48x48 tile was its own texture, and the renderer batches only while the texture stays the same: nearly every visible ground, overlay and detail cell was its own draw call - 95% of all draw calls (hiding the tile layers took a frame from ~2,000 draws to ~70). |

**The harness was measuring itself.** `_BalanceHarness.AutoPickLevelUp` walked every node in the
game (`FindNode`, ~3,000 nodes) every frame looking for the level-up menu - 83% of the main thread
at minute one - and `NearestXpOrb` walked the game's children every frame (9%). Every harness
frame-time number before this branch was mostly the bot. Both are fixed; harness costs are now
their own section (`harness=`) so they can never be read as the game's again.

**The Performance monitors mislead.** `Performance.Monitor.TimeProcess` / `TimePhysicsProcess` are
refreshed once a second with the WORST frame of that second, so the harness's old `process=` and
`physics=` figures were an average of spikes ("physics=200 ms" with 32 enemies). Use the profiler.

## 3. What changed

- **`EnemyRegistry`** (`scripts/EnemyRegistry.cs`): the "enemies" group as a C# array, kept in step
  by the only code that changes the group (`Enemy._Ready`, `SetTargetable`, `StartDeath`,
  `ExploderEnemy.Detonate`, `_EnterTree`/`_ExitTree`). `Join`/`Leave` do the group change themselves,
  so the two cannot drift; the group is still the authority and `IsInGroup("enemies")` still answers
  the same. A dying enemy leaves both in the same call. `Snapshot` is immutable (a change makes a new
  array), so a walk may kill enemies. Order is join order, which is tree order for everything spawned
  under the game. Every `GetNodesInGroup("enemies")` in gameplay code now reads `Snapshot`.
- **A grid over the swarm** (`EnemyRegistry.ForEachNear` / `CollectNear`): 64 px cells, rebuilt at
  most once a physics step (and on a join). Separation reads it - its neighbours' positions are as of
  the step's start, at most one step of walking away. `CollectNear` is a superset for scans that must
  stay exact: it pads by 64 px and returns candidates in `Snapshot` order, and the caller keeps its own
  live-position test. Separation cost: 20 ms a frame to 0.1-0.4 ms.
- **Element counts cached** (`Player.CachedElementInstanceCounts`): rebuilt only when an equipped spell,
  a milestone, a legendary roll, or the count of boons, relics or completed sets changes - compared
  exactly, not hashed. `GetElementInstanceCounts()` still returns a fresh copy.
- **Damage numbers pooled** (`FloatingText.Show`): a faded number hides in place and is taken up by
  the next hit under the same parent. Looks identical.
- **The hit flash is a clock, not a Tween** (`Enemy.TickHitFlash`): the same 0.1 s linear ease of
  tint and punch, ticked in `_PhysicsProcess`. It eases to the *current* base tint, so a boss whose
  enrage retints it mid-flash keeps the new colour (the Tween eased to the tint it captured and undid
  the enrage - a fix, and the only visible difference).
- **Per-body work trimmed in `Enemy._PhysicsProcess`**: `QueueRedraw` only for classes that override
  `_Draw` (measured once per class); the shock shake and `FlipH` written only on a change; the stagger
  tick and the positions read once a step.
- **The hit path calls an `Enemy` directly** (`Player.DealDamageToEnemy`): `TakeDamage`, `ApplySlow` and
  `ApplyPoison` are plain methods nothing overrides, so the direct call is the one `Call` reached; any
  other target still goes through `Call`, with the method names as static `StringName`s.
- **Per-frame `NodePath`s and shader parameter names are static** (`Node2DGame.ProcessTick`).
- **Debug prints removed** from the XP pickup, Spiritual Weapon and Arcane Explosion paths.
- **The tile atlas** (`tools/art/tile_atlas.py`, `LevelTilePainter`): every bonelight tile in one
  1600x1000 texture with one-pixel extruded borders, one `TileSetAtlasSource`. Draw calls on the
  phone's portrait view: ~2,000 to ~60-200. Screenshots with and without it on four stages differ
  only in the run clock and the player's animation frame. `.ai/terrain.md` section 5.

Gameplay is unchanged: the same targets by the same rules, the same damage, the same group
membership on the same frame. Separation reads neighbour positions up to one physics step old,
which moves no body by more than a fraction of a pixel's worth of steering.

## 4. PC, before and after

Headless, Pyromancer, seed 1234, 8 minutes, `--immortal --all-unlocked --timescale=1`. "Before" is
`main` at e7fa930 with only the profiler and the two harness fixes added; "after" is this branch.
Frame-weighted means over each band. `phys/step` is the physics callbacks per step - the number the
death spiral turns on. (Headless frames are uncapped, so "fps" above 60 is headroom.)

| Band | Enemies | fps | frame ms | p99 ms | phys/step ms | steps/frame | alloc KB/frame | GC pause ms/s |
|---|---|---|---|---|---|---|---|---|
| 1-2.5 min, before | 38 | 122 | 8.2 | 37.5 | 4.6 | 0.48 | 14.9 | 5.9 |
| 1-2.5 min, after | 42 | 136 | 7.4 | 26.9 | 2.0 | 0.44 | 2.9 | 1.6 |
| 4-5.5 min, before | 106 | 19 | 52.5 | 621 | 14.3 | 2.37 | 115 | 5.0 |
| 4-5.5 min, after | 118 | 115 | 8.7 | 33.4 | 5.5 | 0.52 | 3.8 | 2.7 |
| 6.5-8 min, before | 120 | 9.8 | 101.6 | 551 | 17.1 | 4.10 | 200 | 5.5 |
| 6.5-8 min, after | 126 | 80 | 12.5 | 80.3 | 6.8 | 0.75 | 5.1 | 2.4 |

**Pixel 9**, release build, the same harness flags (section 6 says how), portrait 720x1280 canvas,
vsync on - so 16.7 ms is the floor and 60 fps the ceiling. Measured on an *intermediate* build of
this branch: the registry, the grid, the element-count cache, pooled damage numbers, the hit-flash
clock, `QueueRedraw` only where something draws and the debug prints gone - but not yet the tile
atlas, the direct hit path, the static `NodePath`s or the position reads.

| Band | Enemies | fps | frame ms | p95 ms | p99 ms | phys ms | proc ms | render ms | draws |
|---|---|---|---|---|---|---|---|---|---|
| 1-2.5 min | 45 | 59.9 | 16.7 | 24.1 | 27.5 | 1.7 | 1.4 | ~12 | ~2,000 |
| 4-5.5 min | 126 | 60.0 | 16.7 | 23.0 | 27.2 | 2.8 | 1.6 | ~9 | 2,200-3,300 |
| 6.5-8 min | 129 | 60.0 | 16.7 | 22.6 | 25.8 | 2.8 | 1.6 | ~9 | 2,200-3,400 |

The phone held 60 fps at the cap with the scripts using 4.4 ms of the frame. Its CPU is much
faster than the laptop's: `MoveAndSlide` was 7.9 us a call there against 20-30 us here. What was
left was rendering ~2,000-3,400 draw calls (`render` includes the buffer swap, so part of it is the
wait for vsync) - which the tile atlas takes to ~60-200. `GC.GetTotalPauseDuration` reads 0 on the
phone's Mono runtime, so its `pause=` is not a measurement there; the collection counts are.

**Not yet measured on the phone:** the unchanged `main` (the "before") and the final build. Both
measurement APKs were built; the phone was locked, then off the network, for the rest of the
session. Section 6 is the recipe.

## 5. The profiler

`scripts/FrameProfiler.cs`. Off unless asked for; then two instances bracket every callback (the
head at the lowest priority, the tail at the highest) and it prints, every N real seconds:

```
PERF #12 5.0s fps=60.1 frame=16.64ms p50=16.4 p95=22.1 p99=25.5 max=32.1 | phys=2.79 proc=1.64 engine=12.21 (render=8.54) steps/f=1.00 | enemies=130 orbs=4 nodes=3377 pairs=61 draws=2512 | gc=1/0/0 pause=0.0ms alloc=5.8KB/f
PERF sections us/frame (calls/frame, KB allocated/frame): enemy=2770(94.2,0K) separation=117(25.3,0K) move=1817(94.2,0K) ...
```

- **frame** is the real period between frames, with its percentiles - a hitch is what a player
  feels and a mean hides it. **phys** is the physics callbacks summed over the frame's steps, **proc**
  the process callbacks, **engine** the rest of the frame (the physics solver, area signals - a
  projectile's hit handler runs there - deferred calls, rendering, and on a phone the wait for vsync),
  **render** the span between `RenderingServer.FramePreDraw` and `FramePostDraw` (on a phone this
  includes the buffer swap, so near 60 fps part of it is waiting). **draws** is the renderer's draw
  calls (0 headless). **alloc** is the main thread's allocation per frame and **gc** the collections
  by generation.
- **Sections** are inclusive spans in the code that scales with the swarm (`FrameProfiler.Section`).
  Add one by adding to the enum and `SectionNames` and bracketing the code with
  `var t = FrameProfiler.Begin(); ... FrameProfiler.End(Section.X, t);`. Code that runs per body per
  step uses `BeginSampledStep` (per node per frame: `BeginSampledFrame`): timed on one step in four
  and scaled up, because a timestamp costs ~1.5 us on that laptop and 8,000 of them a second were 13%
  of a trace. Disabled, a site costs one static bool read.
- **`PERF hitch`** lines: any single frame over 50 ms, with what it was spent on.
- **`PERF canvas`** (every sixth report): the visible CanvasItems by class and how many are in view -
  what the renderer is given.
- It also checks `EnemyRegistry.Count` against the group and reports an error if they differ.

Turn it on with **`--profile`** (or `--profile=SECONDS`, default 5) in the user arguments of any run
that builds `Node2DGame`, in any build. For example:

```
"$GODOT_BIN" --headless --path . scenes/_BalanceHarness.tscn -- --minutes=8 --character=1 --all-unlocked --immortal --timescale=1 --profile=10
```

Run the harness at **`--timescale=1`** for frame costs: its default of 6 makes each physics step
cover a tenth of a second and changes what a frame holds. Run one Godot at a time; parallel runs
and other sessions' runs distort each other. The laptop varies run to run by 20-30% - compare
per-call costs (`move=` time over calls) as well as totals.

**Headless measures CPU only.** For draw calls run the harness in a window at the phone's canvas:
`"$GODOT_BIN" --path . --resolution 540x1212 scenes/_BalanceHarness.tscn -- --portrait ... --profile=10`.
Don't read that window's `render=` time: on this laptop an empty frame (everything hidden, 6 draws)
still took 32 ms - it is the desktop's presentation, not the game. Read `draws=` and `PERF canvas`.

For where managed time goes, attach a sampler to the running game: `dotnet tool install -g
dotnet-trace`, then `dotnet-trace collect -p <pid of Godot_v4.5.1-stable_mono_win64.exe> --duration
00:00:00:30` and `dotnet-trace convert x.nettrace --format speedscope`. It sees managed frames only;
the time inside a native call is charged to the managed caller (which is how `MoveAndSlide` showed up).

## 6. On the phone

The phone runs the **release-signed** build: a debug-signed APK fails to install over it
(`INSTALL_FAILED_UPDATE_INCOMPATIBLE`), and **never uninstall - it wipes the save**. Sign the way
`tools/release/release.sh` does, with the keystore password passed only in the Godot export's
environment (`GODOT_ANDROID_KEYSTORE_RELEASE_*`), never on a command line, in a file in the repo or
in a log - and check the export log does not contain it.

**Command-line arguments did not reach a release build's scene choice.** Launching with
`am start -n com.ecec.wizardsurvivors/com.godot.game.GodotApp --esa command_line_params
"res://scenes/_BalanceHarness.tscn,--,--profile"` opened the title screen. What worked is a
**temporary measurement build**, never committed: `run/main_scene` set to the harness, and the harness
given its arguments in code when `OS.HasFeature("mobile")` and it has none
(`AllUnlocked = true; Immortal = true; TimeScale = 1f; Minutes = 8f; CharacterIndex = 1;
FrameProfiler.ForcedInterval = 5f;`). `--all-unlocked` sets `SaveManager.WritesDisabled`, so the real
save is never written. Install it, launch it (`adb shell monkey -p com.ecec.wizardsurvivors -c
android.intent.category.LAUNCHER 1`), and read the lines from logcat:

```
adb logcat -v time godot:V "*:S" | grep -E "PERF|HARNESS"
```

The run plays itself and quits at the clock. The screen must be on and unlocked for it to render at
all. **Put the normal release build back afterwards.** No input is ever sent to the phone; it is a
personal device, and a tap lands in whatever app is in front (check `adb shell dumpsys activity
activities | grep topResumed` before anything that could).

## 7. What still costs, and the levers left

- **`MoveAndSlide`** is now the largest single CPU cost: ~20-30 us a call on the laptop at the cap,
  two thirds of it enemy-enemy contact. Not changed, because every option changes how the crowd
  moves: grounded-to-floating motion mode (grounded zeroes downward velocity on another enemy's
  "floor" and does not slide on a first contact), fewer slides, circle bodies, or enemies far
  off-screen not colliding with each other. The last is the safest if it is ever needed - nothing on
  screen would change.
- **`physics/common/max_physics_steps_per_frame`** (default 8) decides what an overloaded phone does:
  at 8 it keeps real time and drops to single-digit fps; at 2-3 it slows the game down instead and
  keeps the frame rate. A design call, not made here.
- **Physics at 30 Hz with physics interpolation** would halve every per-step cost. It changes every
  timing that ticks in `_PhysicsProcess`; not done.
- **Spell scans** read the registry now, but most still test every enemy each time (at 140 that is
  140 position reads); `CollectNear` is there for the ones that become hot.

## 8. Rules for new code

- **Never `GetTree().GetNodesInGroup("enemies")` in anything that runs per frame or per hit.** Read
  `EnemyRegistry.Snapshot` (same members, same order, no marshalling), or `CollectNear` /
  `ForEachNear` for "who is near here". Only `EnemyRegistry.Join`/`Leave` may change the group.
- **No string literal where Godot takes a `StringName` or `NodePath` in hot code** (`HasMethod`,
  `Call`, `Play`, `HasAnimation`, `GetNode`, `SetShaderParameter`, `TweenProperty`, `IsInGroup`): each
  builds a new finalizable object. Make it a `static readonly` field.
- **No `GD.Print` per event.** Print per run, or behind a flag.
- **No Tween, no scene instance, no `new` per hit** in anything a swarm triggers. Pool or tick.
- **Read a node property once per step**, not once per use: every read is a call into the engine.
- **Tiles go through the atlas.** A new tile manifest under `assets/bonelight` belongs in
  `tile_atlas.py`'s list.
- **Measure before and after, on the same harness flags, and say which.**
