# Terrain: how the ground is painted, and what stops you

Three layers (four, with the ground detail of section 8), fifteen autotile terrains, and two
independent properties a terrain can have. This is the part of the game a player looks at for
fifteen minutes at a stretch without ever noticing it, which is the standard it has to meet.

## 1. Three layers, and why transparency is the useful one

`LevelTilePainter` builds three `TileMapLayer`s over one shared `TileSet`:

| Layer | What it holds | Collision |
|---|---|---|
| `Ground` | a base floor tile on **every** cell, always | off |
| `Overlay` | the autotiled terrain — grass, water, lava, ice… | **on**, but only blocking tiles carry a body |
| `Walls` | maze walls, painted as flat `occ` blocks (§6) | on |

**Every cell gets a ground tile, including the ones an overlay covers.** That is not redundancy,
it is the mechanism that makes shorelines possible: a transparent pixel in an overlay tile shows
whatever ground is under it, so an edge tile can fade into its surroundings without knowing what
they are. No per-neighbour transition tiles, nothing to keep in sync, and it works with any
terrain added later.

## 2. Hazard and blocking are different things

Two independent flags, both read from `assets/bonelight/tiles/tiles.json`:

- **`hazard`** — standing in it hurts you. Lava and pits. Checked by `IsHazardAtWorld`.
- **`blocking`** — you cannot walk into it. Water. Checked by `IsBlockedAtWorld`, and enforced by
  a real collision body.

Water is not a hazard and lava is not a blocker, which is why these cannot be one flag. A hazard
only works if you *can* step in it.

A blocking terrain gets a collision polygon attached to its tile **source**, once per
(terrain, role), on the same physics layer the maze walls use (layer 4, below). **The polygon is shaped to the
role**, not the cell: an edge tile is half shoreline and a corner is mostly shoreline, and giving
those a full-cell body would stop the player a tile short of the water on ground that plainly
looks walkable — the most annoying kind of invisible wall. See `BlockingPolygon`.

Three other places have to agree, and they are easy to miss:

- `LevelGenerator.SafeZoneExcludedTerrains` — a lake generated over the spawn point would start
  the run with the player inside a wall.
- `Node2DGame.IsImpassablePosition` — everything that *places* rather than moves: enemy spawns,
  formations, elite spawns. The water half is asked of the chunk data directly; the "is this
  connected to the player" half (an island, a sealed corridor) is asked of `MazeNavigation`, which
  exists on every procedural stage now (section 9).
- `Node2DGame.PropAvoidTerrains` — already listed water before any of this.

Since the stages became endless (section 7), all four read **one chunk's data**: the painter
paints its collision from it, `TerrainStreamer.IsBlockedAtWorld` / `IsWallAtWorld` answer
`IsImpassablePosition` from it, prop scatter asks `TerrainAtWorld`, and `MazeNavigation`'s window is
filled from it by `TerrainStreamer.FillNavigation` - walls and water as "cannot walk here", walls
alone as "stops a shot". A query about ground nobody has generated yet
generates that chunk's data rather than answering "unknown", so a placement can never disagree
with what is later painted there.

### The player meets the ground with his feet

Water and maze walls are on **their own physics layer, 4** (`LevelTilePainter.TerrainCollisionLayer`),
not layer 1 with the decor. Reported as "make sure the wizard's feet can't hang over the water":
the player's body collider was the middle of the sprite (y -11 to +24) and his boots are drawn
down to +44, so walking south his feet hung 20 px over the lake, and walking north his body
stopped his boots about 40 px short of it.

The player now carries a second body, `Feet` in `player.tscn`: a capsule on its side, 56 wide and
16 deep, whose bottom is the bottom row of the character cell - the contact line every sheet
stands its boots on. It has **no layer** (nothing can hit it) and **one mask, layer 4**. The main
body keeps its old shape and layers, and no longer meets the ground at all. `Player.MovePlayer`
moves the body first (enemies, decor) and then replays the same step with only the feet against
only the ground (`ConstrainToGround`), so the ground always has the last word. On the north shore
his body now stands in front of the water, as anything does in a three-quarter view.

Why not layer 1 for the feet: decor colliders were sized for the body, and the player's y-sort
origin is the middle of his sprite, not his feet. Feet against a small rock would let him walk up
until the rock drew over his legs while his boots stood in front of it. Decor stays a body
problem until the sort origin moves.

Three things follow from it:

- **Anything that must stop at water or a wall masks layer 4.** Today that is the feet and
  `Enemy.ConfigureEntityCollision`. A new moving body that masks only layer 1 walks across lakes.
- **"What is he standing on" is asked at `Player.FootPosition`**, not `GlobalPosition` - the lava
  and pit damage in `Node2DGame.UpdateHazardDamage` does.
- **Placement checks did not change.** `IsImpassablePosition`, the safe zone and prop avoidance
  are per cell and about enemies and props; the player is never placed, only moved.

Measured with `_UiShot --shore=up|down|left|right` (walks the player into the nearest straight
shore, or a maze wall on stage 1, and logs how far the drawn boots stopped from the collision
line: 0 px south, +4 north, -2 east and west, where the boots' widest frame is 60 px against the
56 px footprint and the 2 px sit in the shore's dithered fade) and `_BalanceHarness --check-feet`
(no frame of several two-minute walks on the Swamp (stage 3) and the maze (stage 1) had the
footprint inside the ground, and no enemy stood in water or a wall).

**Enemies are blocked by water too.** They mask layer 4, as the player's feet do, so a
lake is a mutual obstacle exactly as a wall is. They used to steer straight at the player and pile
up on the near shore; they now route round it on the same navigation field the maze uses
(section 9).

## 3. Water, and three renders that were thrown away

`body_water` in `tools/art/tiles.py`. It was painted on the `wool` row — a muted periwinkle —
using the *deepest* tone of it, `#14183A`. Dark, desaturated and faintly purple: the one thing it
did not look like was water. It is on `azure` now, two steps up, which is both brighter and
actually the row that means water.

The three failed attempts are worth knowing because they all failed the same way — by drawing too
much:

1. **Three half-wavelength sine bands.** Across a pond these line up into a knitted chevron; the
   motif repeats every 48 pixels and the eye finds it instantly.
2. **Per-pixel hashed mottle.** Worse. A field of independent pixels is static, and static reads
   as sandpaper at any density high enough to see.
3. **3×3 depth blocks with a dark tone in the mix.** A mosaic — a pattern again, just squarer,
   and the near-black tone punched holes in a surface that has to read as one bright plane.

What works at this size is a nearly flat field with a *few* deliberate marks: one base tone, depth
patches on a 4×4 grid at about a tenth coverage and only one step lighter, five short horizontal
dashes for ripple crests, and three glints. That is the entire tile.

The same lesson governs the shoreline. `shore()` replaces the `band()` treatment every other
terrain uses, because `band()` lays occlusion along the exposed side and on water that reads as a
black line drawn round the pond. Instead the outer band is dithered away to transparent — in 2×2
blocks, because per-pixel it came out furry and sand-blasted — with foam as scattered dots rather
than a line, since a line of foam at the inner lip is just a paler border.

Since section 8 every walkable terrain blends this way, not only water; `PROFILES` and
`PROFILE_OF` in `tiles.py` say how. Only the two hazards keep an outline: a lava edge that faded
into the grass would be lying about where the damage starts.

## 4. Decor density, and the two things that hide it

The Enchanted Forest had no trees in it for as long as it existed, and neither of the reasons was
"the art is missing" — all eleven decor sprites and all twenty-one themed props were generated,
wired and spawning the whole time.

**The field was 8400 × 8400 and the camera sees 720 × 1280 — about 1.2% of it.** (The stage is
endless now, section 7, and the field survives as a unit of *density*: every count below means
"this many per 8400 × 8400", and each streamed chunk gets its share.) At the old counts
(32 trees, 24 bushes, 22 rocks, 42 props) the expected number on screen was **1.6**. You could
cross the chapter and never pass a tree, which is exactly what happened.

**Clustering divides the count, so raising it alone does not work.**
`BuildClusteredPositions(count, targetSize, ...)` makes `count / targetSize` clumps. At the old
target size of 8, tripling the count just made the same few thickets denser and left the ground
between them as bare as before. The Forest uses a target size of **3** now, so more clumps of
fewer things.

The shipping numbers are 430 trees, 300 bushes, 240 rocks and 260 flora props. Measured with a
census rather than judged by eye — four samples gave **8 to 20 decor objects on screen**, trees 0
to 7, which is the clearings-and-thickets variation clustering exists to produce. Props were
raised far less than trees on purpose: one flora prop in three is a `fallen-log`, which is an
obstacle with a `StaticBody2D`, and a wood the player has to pick their way through is a different
game from a wood they run in.

`PropCount` on `StageEnvironmentProfile` is what lets a chapter override the shared
`CuratedPropCount`. One global number could not serve both a wood that wants ferns and a castle
that wants a handful of torture racks.

### Foliage cannot share a ramp with the ground it stands on

The second reason, and the one no count would have fixed. `body_grass` paints the ground in
`lichen[3]`; `tree()`, `bush()` and `fern()` painted their foliage in `lichen[1..4]`. **Same ramp.**
A tree standing on grass in the same five colours as the grass is a smudge at any size and any
count — and the giveaway was there all along: `mushrooms()` is the one flora prop drawn on a
different ramp (`poison`), and it was the only one anybody could see.

Foliage is on `verdant` now. Ground stays muted, living things are saturated, and the two separate
on hue and value at once.

Rocks stayed on `stone` and were left alone. They are much quieter than the foliage against dark
teal, but they do read — a photograph settled that, after an earlier guess here said they were
invisible. A rock in a wood should not shout.

## 5. The trap

**`tiles.json` is generated.** Re-run `python tools/art/tiles.py` from a checkout where the
`BLOCKERS` set has been lost and the `blocking` flag quietly disappears — the art still renders,
the game still builds, the tiles still paint, and the player walks across the lake. Nothing else
in the project would notice, so `RegressionChecks.ValidateBlockingTerrain` checks it both ways:
that water blocks, and that nothing meant to be crossed has picked the flag up.

After regenerating any tile, run `"$GODOT_BIN" --headless --path . --import` or the new PNGs load
as null and the tiles simply do not draw.

## 6. The maze (Cursed Dungeon)

The only maze stage. Reported after a playtest as "enemies trapped in other corridors most of the
time" and "hard to read". Three causes, three fixes, all measured with
`scenes/_MazeProbe.tscn` (headless, fixed maze seed, immortal player standing still or roaming):

    "$GODOT_BIN" --headless --path . scenes/_MazeProbe.tscn -- --stage=1 --seconds=300 --bot=roam

### The layout was a perfect maze

`LevelGenerator.GenerateMaze` carved a recursive backtracker and stopped: one route between any
two cells, long dead ends, and two corridors seen side by side through one wall that were twenty
tiles of walking apart. At camera zoom 0.85 the player sees four or five corridors at once, so the
horde was visible, walking the long way round, and never arriving. It is now the skeleton only.
Three passes follow: **braid** (90% of dead ends get a second exit, preferring another dead end),
**loops** (12% of the remaining walls between neighbouring cells knocked through) and **halls**
(20% of the wall posts whose four passages are all open removed, joining four corridor blocks
into a room). Corridors stay 6 tiles wide with 2-tile walls. Higher loop and hall rates were tried
(18% / 35%) and on a phone-portrait screen the walls broke up into isolated slabs - an open floor
with furniture, not a dungeon.

The maze is endless now, and those passes run per **block** of 4 × 4 cells rather than over the
whole stage; section 7 says how blocks join. The rates are unchanged. The `_MazeProbe` table below
was measured on the old fixed maze and has not been re-run.

### The flow field drove bodies into corners

`MazeNavigation` was a four-way BFS whose flow pointed each cell at its single lowest neighbour.
A shortest Manhattan path hugs every inside corner, and a 40-50 px enemy body told "go right" from
the cell beside one overlaps the wall block diagonally ahead and slides into its face with nothing
left to slide along. It is now an eight-way Dijkstra (no cutting a wall corner) with a surcharge
for entering a cell that touches a wall, so routes run down the middle of a corridor and swing
wide round corners; the flow is a gradient over every cheaper neighbour, so a turn is a curve; a
body shoved inside a wall cell is steered back out rather than straight at the player. Costs are
small integers, so the queue is a bucket queue and the recompute stays linear. It stops at a
walking horizon (`SearchHorizonTiles`, 64), which took a recompute from about 10.6 ms to 1.6 ms
(desktop, debug build; five a second). Whether a cell is a sealed pocket is answered by component
labels computed once per maze, so the horizon is never mistaken for "unreachable".

### Spawns and recycling measured in straight lines

The spawn ring is just off screen, and a point on it can be one wall and a long walk away.
`Node2DGame.IsPoorMazeSpawn` rejects a spawn or formation slot whose walk exceeds 1.7x the straight
line plus 4 tiles, and `IsImpassablePosition` now also rejects pockets not connected to the player.
`Enemy` recycles on walking distance as well as straight distance in a maze
(`MazeRecyclePathFactor`, 1.6x `RespawnDistance`): an enemy stuck a long way round comes back at a
spawn point that connects. The spawn budget has to stay well under the recycle threshold, or a
fresh spawn is recycled on its first frame.

| `_MazeProbe`, seed 20261014, 300 s | before | after |
|---|---|---|
| median walk / straight line, roaming player | 2.96 | 1.18 |
| median walk / straight line, still player | 1.50 | 1.12 |
| stalled enemies pinned on a wall | 8-12% | 0-0.1% |
| within 8 walking tiles, roaming | 10% | 20-36% |
| within 8 walking tiles, still | 22% | 36-71% |

"Stalled" overall stays high for a roaming bot because a player walking away also stops the
walk shrinking; the wall-pinned share is the number that isolates the bug.

### Walls are void, and the floor was lifted to meet them

The walls asked for a `darkbrick` material that no manifest has defined since the bought pack was
removed, and `CuratedTileCatalog.GroundVariants` quietly fell back to the first material it had:
the dungeon's walls were **sand**, tinted grey-blue by `WallModulate`, about one value step from
its floor. They are now `floor_void_0`, a flat `occ` (#05070C) tile from `tiles.py`, with
`WallModulate` white. `occ` and not #000: the contract has no pure black, and the palette audit in
`raster.py` rejects it. `LevelTilePainter.EnsureWallSources` now fails loudly when the wall
material is missing, and `RegressionChecks.ValidateBlockingTerrain` checks that `void` exists.

Black walls against the old `stonetile` floor would not have helped by much: every floor is built
on its row's `deep` tone with `occ` speckle, two points above the wall. `stonetile` (used only by the
Castle) is now `flagstone` style - laid 24 px flags on stone `base`, mortar in `deep`, a lit top
lip and no `occ` - so the darkest pixel of the floor is still visibly lighter than the wall. The
courses sit on the same rows in every variant; shifting them per variant drew a seam on every tile
boundary.

## 7. Chunk streaming: the stage has no edge

Every procedural stage goes on for ever in every direction (GitHub issue #35). It used to be one
181 × 181 grid painted at stage start, with the player clamped three tiles inside it; now the
ground is generated, painted and furnished in chunks round wherever the player is, and forgotten
behind him. `TerrainStreamer` owns it; `Node2DGame` drives it from `_Process`.

### The numbers

| knob | value | why |
|---|---|---|
| chunk | 32 × 32 tiles, 1536 px | four maze cells (8 tiles each) to a side, so the maze lattice and the chunks line up |
| painted | every chunk within 2 (Chebyshev) of the player's: 5 × 5 | the area of the old arena, so memory and node count are what they were |
| forgotten | painted chunks past 3 | one more than they load at, so pacing across a border does not paint and forget the same column |
| data kept | to 4 | a few KB a chunk; furnishing reads the ring outside the painted square |
| nearest unpainted ground | at least 3072 px from the player | a 32:9 monitor at `keep_height` and zoom 0.85 sees 1506 px either side; the spawn ring is screen edge × 1.08 + 330; `Enemy.RespawnDistance` is 1600 |

`Node2DGame.TerrainLoadRadiusChunks` is the one knob. Do not lower it to 1 without re-reading
that last row: 1536 px is inside the spawn ring on a wide monitor.

### Work only happens at a border, one step a frame

`TerrainStreamer.Update` compares the player's chunk with the last one - two integer divides,
no allocation - and does nothing else until it changes. A crossing queues the new column, nearest
first, and the queue is drained **a step at a time**: a chunk's data; the ground in two halves;
the overlay and walls; its prop holder; one step per decor set; the themed props; the stage
hazards. A frame takes one step, and more only while a 2 ms budget lasts (`StepBudgetMs`). The
new chunks are two chunks away when they are queued, so at 60 fps there are hundreds of frames to
do it in. The one exception is the guarantee: if the next chunk in line touches the player's own,
the streamer keeps stepping until it is done, whatever that costs. It only fires if streaming has
fallen far behind - the harness at 3 fps crossed a chunk in twelve frames, and before this rule
the maze window stopped following the player. Tile sources (a texture and, for water, a collision polygon) are all built at stage start by
`LevelTilePainter.PrewarmTerrainSources`; building one the first time a far chunk needed an inner
corner was a 47 ms frame.

`--terrain-log` (any scene that runs `Node2DGame`) prints one line per crossing - steps, worst
step of each kind, chunks painted, chunks with data, node count - and any single step over 20 ms
with whether the .NET collector ran inside it.

### The seam rule

**A cell must come out the same whichever chunk asked for it**, or there is a line across the
world every 1536 px. Three things hold it, and breaking any one of them reintroduces the line:

1. **Raw terrain is a pure function of the world tile.** Blobs are scattered per chunk from a
   seed hashed from the chunk coordinate; a chunk is stamped with its eight neighbours' blobs too,
   in one global order (row, column, index), so a lake across a border is one lake. The ragged
   edge of a blob is a hash of the tile, not the next number off an RNG. The spawn clearing and
   the safe zone are distances from world tile (0, 0).
2. **Cleanup is local.** `TerrainAutotiler.CleanupGrid` used to edit in place in scan order, which
   could carry a change any distance along a row in one pass. It is double-buffered now: a pass
   reads only the previous pass, so a change spreads at most one cell per pass.
3. **Each chunk is cleaned and autotiled with a margin of 10 cells of its neighbours**, and only
   its middle is kept. Eight cleanup passes reach eight cells, the autotile role reads one more,
   so 10 leaves every kept cell exactly what one infinite grid would have given it.

`RegressionChecks.ValidateTerrainSeams` checks it at every startup: a chunk resolved with the
shipping margin must match the same chunk resolved with twice it, cell for cell, and two
neighbours must agree on every raw cell they share.

Variants are hashed too: which ground tile goes under a cell and which wall tile is drawn come from
a hash of the cell (`LevelTilePainter.VariantSeed`), so a chunk forgotten and painted again when
the player walks back is identical.

### The maze is endless too

The maze is a lattice in world tiles: cell (i, j) is a 6-tile corridor block with a 2-tile wall
strip east and south, shifted three tiles so the spawn point is the middle of cell (0, 0). Cells
are grouped into **blocks of 4 × 4**. Each block runs the backtracker and the braid, loop and hall
passes (section 6) on its own cells, from a seed hashed from the block, so it is connected inside.
**The passages across a block border belong to neither block**: they are a hash of the border
itself (`LevelGenerator.DoorMask`), each open with chance 0.7 (about the share of walls a block
opens inside itself) and at least one always, so both blocks see the same doorways. Every block connected inside and to all four neighbours is a
connected world, with loops within and between blocks. A wall post on a block border never
becomes a hall, because that needs the other block's passages to decide.

`MazeNavigation` is a **window** now, not a map: the painted 5 × 5 chunks, 160 tiles square,
filled from the streamer's chunk data. It moves only once the player is 8 tiles past the edge of
the chunk it is centred on (a player fighting on a border would otherwise rebuild it twice a
second), which still leaves him 56 tiles from its edge - past the 53 at which `Enemy` recycles
on walking distance. The rebuild is **staged**: fill, clearance rows, clearance columns in two
halves, components, then swap and recompute, one per frame, into a staging copy, so the live
window answers every query until the new one is complete. Components are labelled within the window: a corridor that only joins the
player's outside it reads as a pocket, and nothing is spawned there. `IsImpassablePosition` treats
anything outside the window as unreachable, so nothing is placed beyond the ground that exists.

### Furnishing, and what else had assumed an edge

- **Decor, themed props and stage hazards stream with their chunk**, at the density the old fixed
  field gave them (section 4), from a generator seeded by (stage seed, chunk, set) so a revisited
  chunk is furnished the same - which is the point of a stage hazard. The **clump** is the unit,
  not the item: a chunk rolls how many whole clumps it holds, so sparse sets keep their clusters.
  A chunk's things hang off one y-sorted holder node (nested y-sort merges with the game's), so
  forgetting it is one `QueueFree`. Spikes and vents now also avoid water, lava and walls, which
  the old one-off scatter round the start never had to.
- **Ghost enemies** (`IgnoresDecorCollision`) set up their decor exceptions at spawn; decor
  streamed in later makes the exception from its side.
- **`ClampPositionToStageBounds` returns every position unchanged** on a procedural stage. It is
  kept because the placeholder room fallback (`UseProceduralTerrain = false`) still uses it.
  Everything that placed through it still rejects water, walls and unconnected pockets through
  `IsImpassablePosition`.
- **Side events, chests and the boss** were already placed relative to the player, within about
  1100 px, so they always land on painted ground. The **boss** additionally keeps the 3 × 3 chunks
  under itself painted (`TerrainStreamer.Update`'s anchor), so a fight dragged a long way never has
  it walking over ground whose walls and water have no bodies yet.
- **XP orbs left behind** used to be bounded by the arena. Every 2 s, orbs more than 4200 px from
  the player are folded into one far orb that carries their total (`Node2DGame.SweepFarXpOrbs`):
  the XP stays in the world, the node count does not grow with distance walked.
- There is no minimap, no camera limit and no off-screen culling that assumed the bounds.

### Measured

`_BalanceHarness` gained `--stage=N` and `--walk=DEG` (walk a bearing; on a maze it plans a route
over the live navigation window). `_UiShot` gained `--teleport=X,Y`.

    "$GODOT_BIN" --headless --path . scenes/_BalanceHarness.tscn -- --minutes=3 --timescale=2 --stage=0 --walk=0 --immortal --terrain-log

All on a desktop, **debug** build, headless, at `--timescale=2`, 2-3 minutes of game time walking
one bearing with `--immortal`. A phone in a release build will be slower per step; these say how
the work is shaped, not what a Pixel 9 pays.

| | Forest (stage 0) | Cursed Dungeon (stage 1, maze) | Swamp (stage 3) |
|---|---|---|---|
| reached | x = 24,000 px, 16 chunks out | x = 20,200 px, 13 chunks out | (-16,300, -5,200), 11 chunks out |
| initial load (stage start, all at once) | 25 painted, 49 with data, 470 ms | 290-350 ms | 354 ms |
| a crossing | 45-55 steps, 45-140 ms in all | 47 steps, 42-90 ms | 52 steps, 40-50 ms |
| worst step of a crossing, typical | data 1-4, paint 3-6, furnish 2-7 ms | data 1-3, paint 3-6, furnish 2-4 ms | data 2-4, paint 3-5, furnish < 1 ms |
| painted / with data, far out | 30-36 / 56-64 | 25-34 / 51-62 | 33-35 / 61-63 |
| node count, near start then far out | 2,900-4,100 then 3,550-4,550 | 700-1,000 then 900-1,350 | 384-520 then 600-630 |

The node count is flat with distance: what moves it is the enemy count. A walking run and a run
standing at the start on the same branch had comparable frame rates and physics time (25-36 fps
walking with 24-34 enemies, 34-35 standing with 14-17). Earlier runs that seemed to show walking
costing half the frame rate did not reproduce; this machine's frame rate moves a lot from run to run.

**Outliers.** A handful of single steps per run took 20-55 ms. Most had a .NET gen1 collection
inside them (`--terrain-log` says so). Generating a chunk used to allocate about 110 KB of scratch,
and that is now reused (`GenerateChunkInto`, `TerrainAutotiler.ResolveInPlace`), so a chunk
allocates only the 13 KB it keeps. Collections still land in steps now and then, because the
rest of the game allocates too, and one or two outliers had no collection in them at all. The
machine also had another session's Godot running throughout. **Whether a phone hitches at a chunk
border has not been measured on the phone.**

**Maze window moves**, staged, ms per frame: fill 0.2-1.0, rows 1.1-2.0, columns in two halves
1.2-3.3 each, components 2.9-3.6, swap and field 1.0-1.1. All of it in one frame was 9-12 ms, and
a player pacing a chunk border paid that twice a second before the hysteresis.

**`_MazeProbe`, roaming, seed 20261014, 300 s**, after: reach 95.9-99.9%, median detour 1.34-1.44,
pinned 0.7-1.0%, close 16-23%, flow-field recompute median 1.0-1.5 ms. Not like-for-like with the
section 6 table: the roaming bot used to bounce round inside a clamped arena and now wanders as far
as it likes, so enemies trail it out past the search horizon. The doorway chance went from 0.5 to
0.7 between the two runs (detour 1.44, then 1.34). One run each, so read them as "nothing broke"
rather than as a tuning result.

## 8. Natural ground: blends, detail and landmarks

Reported after a playtest: "the variations in ground texture are jarring on some levels. The
forest has large dark patches of something unrecognisable, and they don't blend with the
surrounding grass." Photographed on every stage at 1280x720 (`_UiShot --terrain-seed=4242`, near
the start and at `--teleport=9100,-6300`) before anything changed.

### What the dark patches were

`mossy_rock`, the Forest's second blob terrain (`LevelGenerator` palette, weight 1.4). Its body
was `body_cobble` - setts in `stone.deep` with `occ` joints - with lichen dabbed on, two value steps
under the `lichen.shade` turf, and its edge was `band()`: an `occ` line with two dark tones inside
it. Stamped as blobs that the cleanup squares into staircases, it came out as black rectangles
with a drawn outline. Nothing about it said "moss" at play scale.

The other stages had the same three faults in other colours: every floor material was per-pixel
static (`deep` with 20% `occ` and 10% `shade` pixels hashed independently, the failure section 3
had already found for water); the Ruins, the Cave and the Swamp stamped the Forest's opaque teal
turf as bright rectangles on near-black stone; the Swamp and the Emberdeep stood on `darktile`, a
grid of black lines whose offset moved per variant, so the grid broke at every tile; and on the
Scorched Sands `dark_dirt` was a purple-black static slab.

### Edges: one mask, three profiles

Every role tile is the terrain's body with a COVERAGE MASK cut out of it, computed from a signed
distance to the boundary (`signed_distance` in `tiles.py`). The profile decides where the
boundary sits, how wide the fade is, how round the corners are and how far the line wanders:

| profile | used by | boundary | fade | corner radius | wander |
|---|---|---|---|---|---|
| `soft` | every walkable terrain | 15 px in | 22 px, 2x2 dither | 30 | +-6 px |
| `shore` | water | 8 px in | 7 px | 22 | +-3 px |
| `hard` | lava, pit | 5 px in | none, `occ` lip | 14 | +-1.5 px |

- **Corners are arcs**; outer corners are a quarter-round of the inset rectangle, inner corners a
  quarter-disc of ground of the boundary's radius. That is most of what stopped blobs reading as
  stamped.
- **The wander is pinned at the tile corners.** It is a sum of `sin(pi * n * i / 47)`, zero at
  both ends of every side, so any variant of an edge meets any variant of the next one exactly and
  only the middle of each tile moves.
- **Every role has variants** (fills 6, edges 3, corners and inner corners 2), and the painter
  picks one per cell from a hash (`LevelTilePainter.EnsureTerrainSources`, salt `0x7E2A`), so a
  long shore is not one wiggle repeated. `CuratedTileCatalog.TileVariants` keeps them all; the
  old index kept only the last one it read.
- **Water's collision follows the round corners.** `BlockingPolygon` gives outer corners an arc of
  radius 22 at the 8.6 px inset, inner corners a chamfer, and a lone patch a 12-gon; the old boxes
  left a triangle of visible grass in every corner that the player could not stand on. The edges'
  boxes are unchanged, and the art's boundary (8 px +- 3) straddles them.

### Bodies you can name, at a value near the ground

| terrain | what it is | note |
|---|---|---|
| `grass` | Forest turf: one tone, scattered fanning tufts, a little speck | no static; variants 3-5 carry one clover sprig, a leaf or a pebble |
| `meadow` | a clearing: the turf one stop lighter, with clover | new |
| `moss` | moss cushions on `verdant`, lit upper-left, and leaf litter, on the turf's own tone | new; replaces `mossy_rock` in the Forest |
| `dirt` | grey-brown bare earth with pebbles, on `linen` | new; on `gold` it was a saturated brown hole |
| `overgrowth` | tufts and moss clumps over CLEAR pixels | new; replaces opaque `grass` in the Ruins and the Cave |
| `rocky_pit` | scree: loose stones over CLEAR pixels | walkable and not a hazard; it used to be drawn as a pit |
| `cobble` | setts with CLEAR joints | the floor under it shows between them, so it sits on sand as well as on stone |
| `dark_dirt` | peat: `linen.deep` with warm clods | was `stone.deep` with 30% `occ` |
| `mossy_rock` | flat stones half sunk in `verdant` moss | the Swamp's; on a `lichen.deep` bed now |

Floors (`MATERIALS`): `graystone` (Ruins, Cave, Frozen Waste) and the new `mud` (Swamp) and
`basalt` (Emberdeep) are the `natural` style - one tone, small soft clumps of its neighbours, a
crack or a pebble on some variants only; `sand` is the same on `gold.deep` with two ripples and
half the incident, because it is the brightest floor. `darktile` is still generated, because
`StageEnvironmentCatalog` names its PNGs as a background texture, but no palette paints it.

Per stage, in `LevelGenerator.Palettes`: the Forest stamps water, moss, meadow and dirt (bigger
blobs, density 0.0065); the Swamp stands on mud with water, peat, moss and mossy stones; the Ruins
swap `grass` for `overgrowth`; the Sands lost `dark_dirt` and keep paving and scree; the Emberdeep
stands on basalt. Walkability is unchanged: `water` is still the only blocker, `lava` and `pit`
the only hazards, and `RegressionChecks.ValidateBlockingTerrain` now names every new terrain as
crossable.

### Three things the fill tiles taught, the hard way

1. **A blotch per tile is a lattice.** Soft low-frequency shading, one or two blotches per tile,
   came out as polka dots with a 48 px period - and an ordered-dither blotch was no better. A fill
   tile cannot carry variation larger than itself; large-scale variation has to come from the blob
   terrains and the detail layer, which are placed in world space.
2. **A margin is a grid.** Keeping every feature off the tile edge (so it is never cut by a
   different neighbour) left a bare frame round every tile, and over moss or overgrowth that is a
   visible grid of rosettes.
3. **The shared layer fixes both seams and grids.** `scatter()` draws a share of each body's
   features as variant 0 whatever variant is being painted, anywhere, wrapping round the tile. A
   shared feature is identical in every variant, so whatever neighbour a cell gets, the part that
   wrapped off one side continues on the other. The rest are the variant's own, kept inside.

### The detail layer

The small things lying on the ground: flowers in seven kinds (`flowers_white`, `_yellow`,
`_violet`, `_pink` tulips, `_blue` bells, `_red`, `_mixed`), clover, grass tufts, fallen leaves,
twigs, pebbles, mushrooms, fairy rings, mossy stones, lilypads with and without a water lily,
cattail reeds, bog moss, rubble, cracks, a moss seam, bone fragments, snow drifts, frost, ice
shards, ash, obsidian chips, dry grass and cracked earth. Thirty sets, 104 tiles, all 48x48 and
transparent, drawn by `tools/art/ground_detail.py` into `assets/bonelight/tiles/detail/` with
`detail.json`.

- **A TileMapLayer, not sprites.** Decor is three nodes an item (a body, a shape, a sprite), and a
  hundred flowers a screen streamed in and out would undo the flat node count section 7 measured.
  Each painted chunk gets one more layer, `Detail`, over its overlay, drawn in batched quadrants:
  one node a chunk however many flowers it holds. It paints in furnishing part 1
  (`Node2DGame.FurnishGroundDetail`), before the decor sets.
- **`GroundDetail` decides, the painter paints.** Rules per stage name a set, a SITE (open floor;
  the fill cells of a named terrain, never its fading edges; open floor beside water; water's fill;
  water's edge and outer corners) and a density. Lilypads go on water fill, reeds on the shoreline
  cells and the ground beside them, the meadow gets most of the flowers and the moss gets the
  fungus and the litter. Walls and hazard cells carry nothing.
- **Seam-free by construction.** One roll per cell is a hash of (stage seed, world cell), and the
  clustering noise is a value-noise lattice hashed in world cells, so a cell gets the same thing
  whichever chunk asks. The variant drawn is a hash of the cell too (salt `0x0D7A`).
- **Clustered.** Each rule's density is scaled by `3 n^2` of its own noise field (mean about 1),
  so flowers come in drifts with bare turf between them.
- **Never the subject.** Everything is drawn in material tones - never an element's emissive ramp,
  so no flower can be mistaken for a spell, a telegraph or an XP orb (`grass`, `arcane` and
  `light` emissives) - and open ground in the Forest carries something on about one cell in nine
  (the rule densities sum to about 0.12 there; estimated from the rules, not counted).
- **Cost.** Planning a chunk took 7-15 ms in the first version, against a 2 ms frame budget, from
  hashing four noise corners per rule per cell. It now fills each rule's lattice once per chunk,
  compiles the rules per stage into groups by site, and skips any cell whose roll is above three
  times its group's summed density before reading noise or neighbours: 1.0-2.0 ms a chunk, painting
  0.5-2 ms (debug build, desktop, with other sessions' Godot running).
  `RegressionChecks.ValidateGroundArt` holds every set a rule names against `detail.json`.

### The landmark: a flower pen

A little wattle fence round a bed of dark earth, a ring of daisies, and one moonflower in the
middle with a cold light on the soil round it. Three sprites under one `Landmark` node (back rail
and side rails with the bed; the flower; the front rail with a gate), each anchored at its own
ground line so the chunk holder's y-sort puts the front rail in front of a player behind it and
the back rail behind a player in front of it.

- **Where.** Forest, Swamp, Ruins and Cave only - a flower pen in the ash or the snow would be a
  joke the world has not earned. A chunk rolls 0.25 for one (`LandmarkChance`); the pen needs a
  5 x 4-cell footprint of open floor with a clear ring round it, at least 3 cells from its chunk's
  border, and never within 1400 px of the start. Seed 4242 in the Forest put 6 in the 25 chunks
  round the start: one every few screens of walking.
- **Deterministic.** `Node2DGame.LandmarkCells` decides from the stage seed and the chunk's own
  terrain, once per chunk's data, and keeps the answer on `TerrainChunk.Landmark`. Ground detail
  is not painted inside it, and decor, themed props and stage hazards are kept out of it
  (`IsInLandmark`), including a neighbour's clump spilling over the border.
- **No collision, chosen.** The fence stops nothing. A solid pen is a corner a swarm can pin the
  player in, and it would need the ghost enemies' decor exceptions; nothing yet needs it solid.
  If a secret ever wants the player to use the gate, give the rails a `StaticBody2D` then.
- **The light is `ice`, on purpose.** The XP orbs are `grass`, `arcane` and `light`; nothing on
  the ground may look like one, and the fence frames the flower as a place rather than an item.
- **The hook for secrets.** `Landmark.Spawned` / `Landmark.Despawned` (static events), the
  `landmarks` group, `Landmark.Key` (stage, kind and chunk - stable across visits and across runs
  on the same terrain seed, so "found" state can be saved against it), `Bounds` and `Centre`. What
  a secret does belongs in that system; `Landmark` owns placement and appearance only.
  `--terrain-log` prints each pen's key and position as it is placed, which is the quick way to
  find one to `--teleport` to.

### Tools

- `--terrain-seed=N` (any scene that runs `Node2DGame`) pins the terrain, so a before and an after
  photograph show the same lakes.
- `python tools/art/tiles.py grass moss` redraws only the named terrains or materials while
  iterating, and leaves `tiles.json` alone; only a full run writes the manifest. **Run it in full
  before committing.**

### Still needs eyes

- **The Swamp's enemies against moss and mud** and every stage on the phone, at the Pixel 9 canvas.
  The photographs here were all desktop, 1280x720.
- **Water is still made of whole cells.** Rounded corners, a wandering edge, reeds and lilypads
  break it up, but a large lake is still visibly built from 48 px steps; a smoother shoreline
  would need sub-cell shapes in the generator, and the four blocking authorities would have to
  follow.
- **Detail density** was tuned by eye on a still frame. Whether a field of flowers competes with
  XP orbs in a full swarm wants a playtest.

## 9. Enemy navigation: one field, every stage

On an open stage the swarm used to steer straight at the player, so a lake between them was a
shore lined with enemies pushing into the water. The maze already had a flow field
(`MazeNavigation`, section 6), and the open stages now share it. It is still the one navigation
authority, and the name is historical. There is no second pathfinder.

### What the grid holds

`TerrainStreamer.FillNavigation` fills the window from the chunk flags. `solid` is walls and
blocking terrain (water), which is what a body cannot walk into. `wall` is walls alone, which is
also what stops a shot. Ranged line of sight (`HasLineOfSight`) and `EnemyProjectile` read
`IsWallAt`, so a bolt still flies over a lake. A shoreline cell counts as solid whole, even
though its collision polygon covers only part of it. Routing a body a little wide of a shore
costs nothing; routing it into the half of a cell that is water is the wedge this exists to
prevent. Both read the same flags as the tile collision on layer 4, so the grid and the bodies
cannot disagree.

**Props are not in the grid.** Trees, rocks and fallen logs are small `StaticBody2D`s that a body
slides off. Stamping them in would mean the furnishing code writing into navigation, and a
thicket that walls off a gap is the one case that would need it. Ghosts (`IgnoresDecorCollision`)
ignore decor only, so they route like everyone else. An enemy that clears its layer-4 mask
(`Enemy.FollowsTerrain`) ignores the field and is never recycled for walking distance.

### Who reads it, and when

- **Maze:** every enemy follows the field all the time, as before.
- **Open stage:** an enemy follows the field only while its straight walk to the player is
  blocked. `HasClearWalk` is an exact grid walk (Amanatides and Woo), stops at the first solid
  cell, skips the cells at both ends, and returns at once on a window with no water. It runs with
  the re-aim (`AimRefreshFrames`, every fourth physics frame per enemy), not every frame. An enemy
  with a clear run keeps the direct chase, with its lead and its slot on the encircle ring. That is
  deliberate: a field over eight neighbours bends an open-ground approach toward the nearest
  compass octant, and open-field feel was not to change.
- The flow is folded into the `chaseDirection` that `AdjustSteering` receives, so lungers,
  slammers and bosses keep their own steering semantics. A dash is locked to its committed
  heading and a planted wind-up still returns zero.
- **Placement and recycling** now apply on open stages too. `IsImpassablePosition` rejects a
  point not connected to the player (an island inside a ring of water). `IsPoorMazeSpawn` rejects
  a spawn on the far shore whose walk is over 1.7x the straight line plus 4 tiles. `Enemy`
  recycles on walking distance (`MazeRecyclePathFactor`) on both stage types.

### Why it is cheap, and flat in enemy count

- **One field for the whole horde.** An enemy reads its cell's direction, which is lazy and cached
  per cell per field. It allocates nothing.
- **Rebuilt only when the player changes cell,** at most every 0.2 s in a maze and every 0.5 s on
  an open stage. On open ground the field only steers enemies whose straight walk is blocked, and
  two tiles of staleness moves the point they head round a lake toward by two tiles. A player
  holding his ground costs nothing at all.
- **Built a slice a frame, double-buffered.** `FieldCellsPerFrame` (2048) cells per frame go into
  a second distance table, which is swapped in whole, so a reader never sees half a field. The
  bucket queue can be resumed mid-bucket, because no edge costs less than a straight step, so
  nothing is ever added to the bucket being drained. `RegressionChecks.ValidateNavigation` proves
  that a field built 37 cells at a time equals one built whole.
- **Window moves are bounded per step.** Previously they ran as six steps of up to 3.6 ms (section
  7), the last a synchronous swap plus a whole field. Now: fill, two row bands, four column bands,
  a resumable component flood fill (8192 cells a step), the new window's field in slices into the
  staging table, then a swap that only exchanges references. That is about seventeen frames a move.
- **Work follows the frame's delta** (`FramesOfWork`). The budgets are sized for a 60 fps frame. A
  long frame does several frames' worth, so a low frame rate cannot stretch a field or a move over
  seconds of game time. Before this, `_MazeProbe` on a loaded desktop (a few fps at timescale 4)
  fell from 15% of the horde close to the player to 10%, and from 100% routable to 96%.

### A bug the generalisation found

The staged window rebuild (section 7) had its column pass and component labelling reading and
writing the **live** tables (`_solid`, `_penalty`, `_wallPush`, `_component`) instead of the
staging ones. The swap then put the stale copies live. From that change until this one the maze
ran with no wall surcharge, no push off the walls, and every cell labelled as one pocket. That
explains why the pinned share in section 7's measurement rose from section 6's 0-0.1% to 0.7-1.0%.
Every step before the swap now writes only `*Next` arrays, and the swap is the only place they
meet.

### Measured

Desktop, **debug** build, headless, `--stress-swarm` (fills to the 140 cap at once), one minute
at timescale 1. The machine had six to seven other sessions' Godot processes running, so frames
ran at 3-7 fps and frame means moved by 10 ms between identical runs. Read the navigation's own
timings, which are direct, before the frame means, which are not.

    "$GODOT_BIN" --headless --path . scenes/_BalanceHarness.tscn -- --minutes=1 --timescale=1 --immortal --stage=3 --stress-swarm [--walk=0] [--no-open-nav] [--terrain-log]

The `PERF nav` line (every 2 s under `--stress-swarm`, or on a phone debug build) reports fields
built, mean and worst whole-field ms, the worst single slice, and all enemies' navigation work
(line test plus flow read) in µs per physics frame.

| ~137-139 enemies | Swamp, kiting | Swamp, walking | Forest, kiting | Maze, kiting | Maze, walking |
|---|---|---|---|---|---|
| fields per game second | 0.6 | 0.7 | 0.6 | 0.9 | 1.5 |
| whole field, mean / worst | 2.5 / 4.7 ms | 2.5 / 3.9 ms | 1.9 / 2.9 ms | 1.0 / 1.9 ms | 1.0 / 1.5 ms |
| worst slice (one frame's share) | 1.8 ms | 1.7 ms | 1.1 ms | 1.0 ms | 0.8 ms |
| window move, worst step | - | 1.9 ms | - | - | 0.9 ms |
| all enemies' steering, per physics frame | 56 µs (max 140) | 95 µs (max 255) | 48 µs (max 88) | 56 µs (max 89) | 65 µs (max 95) |

Before this change a maze field was a whole recompute five times a second, 0.5-1.5 ms each
(`_MazeProbe`), whether or not the player had moved. A window move spent up to 3.6 ms in one step,
plus a whole field on the frame it swapped. Open stages paid nothing and routed nothing. Same-build
A/B frame means, with `--no-open-nav` giving open stages no window at all:

| process / physics, ms | routing off | routing on |
|---|---|---|
| Swamp, kiting | 23.6 / 40.3 | 23.9 / 38.7 |
| Swamp, walking | 28.4 / 34.8 and 21.3 / 48.2 | 23.8 / 44.4 and 25.7 / 47.4 |
| Forest, kiting (heavy load) | 45.2 / 37.6 | 55.9 / 34.1 |
| Forest, kiting (lighter load, two runs each) | 37.0 / 18.6 and 33.7 / 19.5 | 36.7 / 22.1 and 38.8 / 21.5 |

Most of these differences are inside the run-to-run noise. The exception is the lighter Forest
pair: physics there ran 2-3 ms higher with routing on, while navigation's own work in physics
measured 29 µs per frame. If that gap is real, it comes from behaviour (where the crowd ends up,
and what it presses against) rather than from computing the route. The bot's end positions and
collision-pair counts differed between the runs. It is worth a look on the phone. The direct cost
is 0.03-0.1 ms of a physics frame plus 1-2 ms of field work per game second, spread a slice at a
time. **None of this has been
measured on the phone**, and a Pixel 9 release build will differ in both directions: it has a
slower core and a faster (non-debug) build.

**Behaviour**, `_MazeProbe` (seed 20261014, roaming bot, 300 s at timescale 4; one run each unless
two are given):

| | reach | detour | stalled | pinned on terrain | close |
|---|---|---|---|---|---|
| Swamp, straight chase (`--no-open-routing`) | 99.9% | 1.10 | 52.3% | **6.3%** | 12.2% |
| Swamp, routed (two runs) | 99.2-99.8% | 1.07 | 56.2-58.4% | **0.4-0.5%** | 11.3-13.3% |
| Maze, before (code as merged, stashed) | 100% | 1.39 | 51.1% | 0.8% | 15.4% |
| Maze, after (two runs) | 99.7% | 1.18-1.22 | 50.7-52.1% | 0.1-0.2% | 13.9-15.1% |

"Pinned on terrain" is the share of stalled enemies whose last slide hit a `TileMapLayer` (water
or wall). On the Swamp it is the number this section exists to move.

    "$GODOT_BIN" --headless --path . scenes/_MazeProbe.tscn -- --stage=3 --seconds=300 --bot=roam [--no-open-routing]

**Photographed**, on the same ground both times:

    "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- --scene=res://scenes/node_2d_game.tscn --stage=3 --character=1 --shore=up --stress-swarm --terrain-seed=4242 --frames=1200 --shot=nav-on [--no-open-routing]

The player stands at the south shore of a pond with a full swarm. With a straight chase, the pond's
far shore and west edge are lined with enemies pressing into the water, and so is the south shore
of the next lake. Routed, both shores are empty and the swarm closes over open ground. **What
still needs eyes:** whether the flow round a *large* lake reads as a horde going round rather
than a trickle at its ends. That is a playtest question, and so is the phone.
