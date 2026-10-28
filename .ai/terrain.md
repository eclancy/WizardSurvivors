# Terrain: how the ground is painted, and what stops you

Three layers (four, with the ground detail of section 8, and the water of section 3 drawn on its
own), fifteen autotile terrains, and two independent properties a terrain can have. This is the part of the game a player looks at for
fifteen minutes at a stretch without ever noticing it, which is the standard it has to meet.

## 1. Three layers, and why transparency is the useful one

`LevelTilePainter` builds three `TileMapLayer`s over one shared `TileSet`:

| Layer | What it holds | Collision |
|---|---|---|
| `Ground` | a base floor tile on **every** cell, always | off |
| `Overlay` | the autotiled terrain — grass, moss, lava, ice… (not water: section 3) | on, but no tile carries a body any more |
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

Water's collision is the polygons it is drawn from (`WaterShape`, section 3), on the same physics
layer the maze walls use (layer 4, below), and **shaped to the shore, not the cell**: a shoreline
cell is part water and part bank, and a full-cell body would stop the player short of water on
ground that plainly looks walkable - the most annoying kind of invisible wall. Those polygons
never leave the water cells, so every cell-level authority below still describes them truthfully.

Three other places have to agree, and they are easy to miss:

- `LevelGenerator.SafeZoneExcludedTerrains` — a lake generated over the spawn point would start
  the run with the player inside a wall.
- `Node2DGame.IsImpassablePosition` — everything that *places* rather than moves: enemy spawns,
  formations, elite spawns. The water half is asked of the chunk data directly; the "is this
  connected to the player" half (an island, a sealed corridor) is asked of `MazeNavigation`, which
  exists on every procedural stage now (section 9).
- `Node2DGame.PropAvoidTerrains` — already listed water before any of this.

On the maze one more thing reads the walls to PLACE something: `LevelGenerator.MazeCorridorCentre`,
which puts each stage hazard in the middle of its corridor (section 6). It asks the same chunk data's
wall flag (`TerrainStreamer.IsWallAtWorld`) as everything else, so it is a reader of the one
authority rather than a fifth one - but if the walls ever stop being the lattice's two-tile strips,
it is the place that has to change with them, and `RegressionChecks.ValidateMazeHazards` will say so.

Since the stages became endless (section 7), all four read **one chunk's data**: the water's
shape and collision are built from its blocked flags (`TerrainStreamer.FillWater`), `TerrainStreamer.IsBlockedAtWorld` / `IsWallAtWorld` answer
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
problem until the sort origin moves - and for trees and the garden fence it has (section 10):
they sort at their ground line lifted by the distance from his origin to his footprint, so they
are on layer 4 and meet his boots. Rocks, crystals and the themed props are still layer 1.

Four things follow from it:

- **Anything that must stop at water or a wall masks layer 4.** Today that is the feet and
  `Enemy.ConfigureEntityCollision`. A new moving body that masks only layer 1 walks across lakes,
  through tree trunks and through garden fences, which are on layer 4 too (section 10).
- **Moving something else means moving it through its own body.** The player shoves ordinary
  enemies out of his way (`Enemy.ComputePlayerShove`, `.ai/enemy-behaviour.md` section 8), and the
  shove is added to the enemy's own `MoveAndSlide`, so it meets the ground the enemy masks and a
  body pushed toward a lake slides along the shore. The push it replaced wrote `GlobalPosition`
  directly on every body touching him, which walks through any wall.
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

## 3. Water: drawn from its shape, not from tiles

Until 2026-10 a pond was fourteen autotile roles of `body_water`, each a whole 48 px cell of
saturated `azure` with a rounded shore cut into it. On screen that was a bright royal-blue
staircase - reported, after the natural-ground pass, as the most artificial thing on screen. Two
faults, and neither could be fixed inside a tile: the colour (azure is player pigment, two value
steps above any ground it lay on) and the shape (a tile knows its eight neighbours, and whether an
outer corner is the corner of a lake or one step of a diagonal is decided two cells away).

### The shape: `WaterShape`

For every water cell, the convex polygon of it that is water. **The one rule: water never leaves
its cells.** The blocked grid is per cell and is read by the flow field (section 9), placement,
prop avoidance and the streamer's flags, and none of that changed; every shape here only REMOVES
water from blocked cells, so the drawn water is always inside cells those authorities call water.
That is enough for smooth shapes, because a smooth shore only ever needs to remove: a 45 degree
staircase of cells becomes a straight diagonal through its outer corners, which come out half
water and half bank.

A cell's water is its square intersected with:

| constraint | where | what |
|---|---|---|
| inset | every side whose neighbour is dry | the shore 9 px in, wandering +-4 px. The wander is a smooth noise ALONG the shore line, sampled at the cell corners and straight between them, so it is continuous across cells and chunks and each cell's stretch is one straight segment (convex) |
| bevel | every corner whose two side neighbours are water and whose diagonal is dry | a chamfer from one inset line to the other: the inside of a bend is never a right angle |
| cut | every OUTER corner within reach | from the corner, walk both sides to where the shore turns. Both walks end in an inside bend within two cells: the corner is a step of a diagonal, and the cut is a straight chord between the two bends - consecutive steps' chords are collinear, which is what turns a staircase into a straight shore. Otherwise a round corner (a superellipse) of up to 40 px, never more than half the side, so a lone cell is a disc and a 2x2 pond a circle. One of each: m 1.5 |

All three are convex, so each cell is one convex polygon: one collision shape, and a cheap
point-in-polygon test. A cut's ear can reach three cells from its corner, so a chunk reads a window
of blocked flags 10 cells into its neighbours (`WaterShape.WindowMargin`, filled by
`TerrainStreamer.FillWater`); `RegressionChecks.ValidateWaterShapes` builds two neighbouring Swamp
chunks and requires every cell they both build to come out identical, and that no water cell is
left with nothing drawn in it (an invisible no-go square for the flow field).

### The same polygons are the collision

`WaterSurface` gives each chunk one server-side static body (`PhysicsServer2D`, no nodes) on layer
4: a convex shape per partial cell, a rectangle per run of whole-water cells. It used to be a
polygon on each water tile's SOURCE, which can only be one shape for every cell using that tile.
So the drawn shore and the place the feet stop are the same line by construction; `_UiShot
--shore` now finds that line by probing the physics rather than assuming a fixed inset, and
measured exactly what the tile shores did: boots stop 0 px from it walking south, +4 north, -2
east and west (the boots' widest frame is 60 px against the 56 px footprint).

`GetCollider()` on a slide against water returns the `WaterSurface` node (the body carries its
instance id). Anything that asked "did I hit terrain" with `is TileMapLayer` now has to ask
`is TileMapLayer or WaterSurface` - `_MazeProbe` did.

### The drawing: a distance field and a shader

`WaterSurface` turns the polygons into a signed distance to the shore, one byte per 6 px texel,
+-12 px, for the chunk and one cell round it, by splatting each shoreline segment onto the texels
near it. `scenes/shaders/water_surface.gdshader` reads it with bilinear filtering at the middle of
every world pixel, so the shore is a smooth curve on the art's own pixel grid. From that one number:

- **outside** (0 to -6 px): the bank - damp earth dithered out into the ground, wet mud at the
  water. A step from the ground's colour, never a line drawn round the pond.
- **the first pixel**: the bank's shadow on the water, on shores facing away from the light.
- **dark water under the bank**, dithered into the body. The key light is upper-left
  (`.ai/art-direction.md` section 2), so the band is wider where the bank is up-left of the water
  and narrower where the light reaches over it, and on the sunward side there is a lighter band of
  lit shallows. The facing comes from the field's gradient.
- **the body**: `<style>_surface.png`, 192 px square, tiled in world space.
- **ripples**: `ripple.png`, two copies sliding across at about 2 px a second in different
  directions, so the water moves without any pixel changing more than about once a second.

Every colour is picked from generated art (`tools/art/water.py`): nothing is blended, so nothing
off the contract can appear. Per biome (`LevelTilePainter.WaterStyle`): a **forest** pond is deep
blue-teal under the turf with the canopy's lichen in it and reeds and lilies; the **swamp** is
grey-green murk darker than its mud, with duckweed; the **frozen** water of the Frozen Waste is
black-blue with a pale ice shelf for a shore and frost for a bank; and anything else is a near-black
stone **pool** (no other chapter has water today). The Cave has no water - it uses the Ruins'
palette - and none was added: adding water to a stage changes where you can walk.

### Three renders that were thrown away (the tile era), and two more

They all failed the same way, by drawing too much, and the surface still obeys them:

1. **Three half-wavelength sine bands.** Across a pond these line up into a knitted chevron; the
   motif repeats every 48 pixels and the eye finds it instantly.
2. **Per-pixel hashed mottle.** Worse. A field of independent pixels is static, and static reads
   as sandpaper at any density high enough to see.
3. **3x3 depth blocks with a dark tone in the mix.** A mosaic - a pattern again, just squarer.

And from this pass:

4. **Solid patches of a second tone** in the surface (a reflected canopy, brown silt), 30-90 px
   across. In any colour they read as camouflage printed on the water. The patches are now a
   half-tone - at most three pixels in eight of the second tone, ordered - which reads as the water
   being a little murkier there.
5. **A solid wet-mud ring** at the water's edge, in a warm brown against grey ground: an outline
   round every pond, which is the fault section 8 removed from every other terrain. The bank is
   now dithered from the waterline outward, in tones a step from the ground's.

Since section 8 every walkable terrain blends through a dithered mask; `PROFILES` and
`PROFILE_OF` in `tiles.py` say how. Only the two hazards keep an outline: a lava edge that faded
into the grass would be lying about where the damage starts. `tiles.json` still carries ONE water
tile, `water_fill`, so the manifest says water exists and blocks (section 5); nothing paints it.

### Cost

Two streamer steps per chunk with water near it, after the overlay: the shape and the field, then
the texture, the node and the body. Debug build, desktop, `--terrain-log`, two-minute walks:

| | Swamp, before | Swamp, after | Forest, before | Forest, after |
|---|---|---|---|---|
| a border crossing, all steps | 22-31 ms (mean 24.3) | 21-34 ms (mean 25.9) | 29-33 ms | 31-37 ms |
| worst paint step of a crossing | 1.0-1.9 ms | 1.1-2.0 ms (water) | 0.8-2.0 ms | 0.7-1.5 ms (water) |
| node count, far out | 830-950 | 860-950 | ~4,100 | ~4,000 |

Per chunk on the Swamp: shape 0.3 ms, field 0.5 ms, texture and body under 1 ms. The first
chunk of a stage pays ~28 ms once (shader compile and JIT), inside the initial load. One node a
chunk, the same as the tile overlay it replaced. The shader runs only over the chunk's cells near
water; the pixel cost on a phone has not been measured.

### Still needs eyes (water)

- **The shader on the phone.** It ran only on a desktop here, in the Compatibility renderer's
  desktop GL. The phone is the same renderer, but its cost per pixel and whether the ripples
  shimmer or crawl on a Pixel 9 have not been looked at. Phone-canvas photographs were taken on
  the desktop (`--portrait --safe-area`).
- **Long straight shores.** A rectangular blob still makes a long straight shore, softened only
  by the +-4 px wander; on the Frozen Waste, with nothing growing on it, that shows most.
- **Islands and one-cell channels** are handled (every constraint is local) but were not seen in
  the photographs.
- **Lily pads near a cut.** A long two-cell diagonal chord can nick the corner of a water FILL cell
  by up to 8 px, and a lily pad drawn there could overhang the bank by a few pixels.

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

**Superseded for trees and bushes (2026-10).** Those numbers were for 96 px trees. Trees are now
200-300 px and the counts came down with them (section 10): the Forest has 150 mature trees, 90
young ones, 300 bushes and the same 240 rocks. The ramp rule below still holds for every one of
them.

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

**The game draws the tiles from one packed texture, `atlas.png`**, not from the PNGs one by one
(`tools/art/tile_atlas.py`, run by `tiles.py` and `ground_detail.py` at the end of every run, full
or partial). One texture per tile made nearly every visible cell its own draw call - about 2,000 a
frame on the phone - because the Compatibility renderer batches only while the texture stays the
same; `.ai/performance.md` has the numbers. Each cell has a one-pixel extruded border that the
painter's atlas source steps over (margins 1, separation 2, Godot's texture padding off). A tile
the atlas lacks still paints from its own PNG, so a stale atlas costs frame rate, not art, and
`RegressionChecks.ValidateGroundArt` warns about it. Commit `atlas.png`, `atlas.json` and the
`.import` with the tiles. Walls take an alternative tile of their cell for the collision square,
so the same art used as floor elsewhere stays walkable.

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

### Walls were void, and the floor was lifted to meet them

The walls asked for a `darkbrick` material that no manifest has defined since the bought pack was
removed, and `CuratedTileCatalog.GroundVariants` quietly fell back to the first material it had:
the dungeon's walls were **sand**, tinted grey-blue by `WallModulate`, about one value step from
its floor. They became `floor_void_0`, a flat `occ` (#05070C) tile from `tiles.py`, with
`WallModulate` white. `occ` and not #000: the contract has no pure black, and the palette audit in
`raster.py` rejects it. `LevelTilePainter.EnsureWallSources` fails loudly when that material is
missing, and `RegressionChecks.ValidateBlockingTerrain` checks that `void` exists - it is still the
fallback for a wall role with no jail art (below).

Black walls against the old `stonetile` floor would not have helped by much: every floor is built
on its row's `deep` tone with `occ` speckle, two points above the wall. `stonetile` (used only by the
Castle) is now `flagstone` style - laid 24 px flags on stone `base`, mortar in `deep`, a lit top
lip and no `occ` - so the darkest pixel of the floor is still visibly lighter than the wall. The
courses sit on the same rows in every variant; shifting them per variant drew a seam on every tile
boundary.

### The walls are a jail (2026-10)

Reported after a playtest: "the black walls in that level should be a nicely detailed stone jail
walls. add skulls, sconces, and a bunch of details to make it look like a dungeon jail." The void
tile went. The walls are the `wall` terrain now, drawn by `tools/art/jail_walls.py` (run through
`tiles.py`, packed into the atlas like every tile) and painted per cell by the role the autotiler
resolved for it (`LevelTilePainter.WallTileFor`). Nothing about where a wall is, or what it stops,
changed: the same cells, the same full-cell square on layer 4 on every wall tile, the same
`FlagWall`.

**Three-quarter view, so south decides.** The walls are two tiles thick, so every wall tile touches
floor on a side or a corner, and the role says which. Floor to the SOUTH is the wall's **face**,
standing toward the viewer: a coping with a lit arris, three courses of cut stone with `occ` mortar,
damp creeping up the bottom course and an `occ` contact line at the foot. Anything else is the
**cap**, the top of the wall from above: cobbles in the deep end of the stone row, a lit bevel on
edges facing north or west (the key light) and an `occ` edge facing east. So a horizontal wall is a
cap row over a face row, a vertical one two columns of cap with a face at a free south end.

**Three values, one order.** Mean L* over each tile's pixels: the floor (`floor_stonetile_*`) 13.9,
the plain faces 9.3, the plain caps 4.8, against the void's 1.9. The floor is the lightest stone on
screen, the face sits a step under it with lit edges, and the top of a wall is the darkest thing
there, so the corridors read as they did when the walls were black - and the wall reads as a thing
with a top and a front rather than a hole.

**Its shadow on the floor.** The key is upper-left, so a wall throws its shadow down and right: a
dithered `occ` band across the floor at the foot of every face and beside every vertical wall's east
side (`wall_shadow_*`, four tiles). It is what stands the face up off the floor. They are painted
into the chunk's `Walls` layer on the FLOOR cells next to a wall, with no collision - the same atlas,
so no extra draw call - from `TerrainChunk.WallNeighbours`, which `TerrainStreamer.GenerateData`
reads off the padded grid so a cell on a chunk border knows the wall in the next chunk. Paint only:
nothing that places or moves anything reads it.

**Dressing, and why it is sparse.** A role has PLAIN variants (four faces, four caps, three of each
side) and DRESSED ones, each carrying its dressing and a weight in `tiles.json`:

| on | dressing (weight) |
|---|---|
| faces | lit sconce (5), skull in an arched niche (3), barred cell window (3), guttered sconce (2), chains and a shackle (2), manacles (2), bones and a skull at the foot (2), skull on a spike (2), a seep with moss (1), a fallen block (1), tally marks (1), a drain grate (1) |
| vertical walls' tops | lit sconce (3), guttered sconce (1), a chain over the edge (1) |
| tops | moss (1), a broken cobble (1) |

A cell is dressed when a hash of it rolls under its role's chance (faces 0.5, sides 0.16, tops 0.06)
**and the cell before it along the wall did not roll under 0.5**, so two dressed tiles never stand
side by side and about one face in four carries something. Everything is a hash of the world cell,
so a chunk painted again is the same and a chunk border changes nothing. Section 4b of
`.ai/art-direction.md` is why the plain variants still carry wear (chips, pits, damp, moss in the
foot joint): plain does not mean flat.

**His light, not the player's.** A lit sconce burns the `arcane` material row round a white core -
the same cold mark his creatures carry in their eyes (`.ai/world-and-tone.md`) - and pools it on the
stone round it; the rest are cold iron and soot. Nothing on a wall is warm (the player carries the
only warm light, and a secret's tell is a warm glint), nothing is an XP orb's colour, nothing
animates (decor never does), skulls are 11 px in the bone row's middle stops - a fifth the size of
the Skull Sentry and with no eye-light - and nothing is drawn as a way through: the cell window is
high in the face and barred, the drain is small and barred, and no opening shows floor behind it.

**The spawn clearing's stubs.** `LevelGenerator.ClearSpawn` opens a disc round the start and cuts
the two-tile walls near it to one tile. Every such cell resolves to `Patch`, which only says "not a
shape the set has", and painted as a pillar they stacked into a column of little pillars at the very
first screen of the run. `TerrainChunk.WallNeighbours` also carries a wall cell's open sides, and
`LevelTilePainter.WallRoleKey` turns a patch into what it is: a one-tile wall top (`patch_v`), its
north end (`patch_cap`), its face (`patch_face`), or an ordinary edge or corner.

**Checked.** `RegressionChecks.ValidateGroundArt` requires a plain tile for all seventeen roles, a lit
sconce and the four shadows; a missing role would paint as the old void, a wall that works and looks
like a hole. **Still needs eyes:** whether the dressing density reads as a jail or as clutter over a
fifteen-minute run, whether the cold flames read as sconces on the phone (they are 9 px wide at 1:1,
drawn at the floor's pixel scale like every tile), and whether a lit sconce ever competes with a
cold-element spell for the eye in a full swarm.

### Traps in the middle of the path

Reported in the same playtest: "the traps should always be in the center of the paths they're
within, not randomly next to the walls." Spike plates and flame vents were scattered at random
points of their chunk and only refused a point ON a wall. Measured on seed 4242 (four minutes of the
harness below), 162 of the 175 placed stood off their corridor's centreline, many of them against a
wall - a trap that reads as a mistake and plays as a corner the swarm pins you in.

On the maze every candidate is now moved before it is tested
(`LevelGenerator.MazeCorridorCentre`, from the lattice the walls are made of): into the middle of a
hall if a post round its cell is open, otherwise onto the centreline of the arm of its cell it is
nearest (keeping how far along the corridor it was), otherwise onto the other open arm or the cell's
middle. It asks the chunk data's wall flag at a cell's four passages and four posts, nothing else, so
the walls that stop the player and the walls the snap reads are one authority. Every snapped point is
half a corridor - 144 px - from every wall it can see, and a trap is never on a wall at all.

What did not change, and what had to:

- **Same rolls, same density.** `Node2DGame.HazardPlanFor` draws the same rolls in the same order as
  the scatter it replaced, so an open stage (the Ruins, the Emberdeep - no other stage has walls)
  places exactly what it did. A snapped point outside its own chunk is dropped, so a cell centre on a
  chunk border belongs to one chunk. Measured on seed 4242, stage 1, four minutes:
  4.91 hazards a painted chunk centred, 4.86 with the old scatter (the closest two were 92 px apart
  then, 240 now).
- **The plan is pure and kept with the data** (`TerrainChunk.HazardPlan`): a function of the seed and
  the ground, never of the run, so a revisited chunk has its traps where they were.
- **Separation across chunk borders.** Centred hazards share centrelines, and a centreline running
  across a chunk border could carry a plate from each side a few pixels apart. A chunk now gives way
  to the plans of the four chunks before it in (row, column) order - their plans, which this rule
  never shrinks, so nothing recurses and any two hazards that both stand are at least
  `StageHazardSeparation` (240 px) apart. The old scatter only kept it within a chunk.
- **Nothing stands on a trap.** Decor, grounded decor and themed props keep 72 px from every planned
  hazard in their chunk and the ones round it (`IsNearPlannedHazard`). That applies wherever there
  are hazards (the Ruins too), and is the one change to their furnishing.
- Side-event sites already sat at cell centres and reserve their footprint; hazards keep out of it
  as before. Hazards are areas with no body (`StageHazard`, collision layer 0), so a centred one never
  narrows a corridor for anyone. Enemy spawns and recycling never asked about hazards.

**Checked twice, both against the walls rather than the snap.** `RegressionChecks.ValidateMazeHazards`
generates real maze ground on three seeds, snaps 1,200 points and measures each one's distance to the
nearest wall tile: under 143 px is a warning, and so is a run that never reached a corridor, a
junction and a hall (broken on purpose, it reported 1,103 of 1,177 off-centre). `_BalanceHarness
--check-feet` prints a `HARNESS hazards:` line on any stage: placed, per painted chunk, the closest
two, how many have decor within 64 px, and on the maze how many have a wall within 143 px, sampled on
rings round each one. Shipping run: 0, 0, closest two 240 px.

**What a trap looks like, and where it bites (2026-10).** Reported: "the floor fire traps should be
an animated vent, and the floor spikes should be a bunch of holes." The spike trap is an iron plate
of seven holes whose tips glint in the tell and shoot up in the active phase; the vent is a slotted
iron grate with an ember in it that heats up, sparks, vents a sheet of fire and cools. Both are drawn
exactly the size of their collision shape with no ring round them, and `StageHazard` now tests the
player's BOOTS against that shape (`Player.FootprintOverlaps`), not his body - the same rule the
lava follows (section 2). Measured with `_UiShot --goto=hazard --hold`: boots on a spike plate, 5
bites; the body over the plate with the boots 60 px south of it (where the old body test bit), 0.
`.ai/art-inventory.md`, "Hazard art needs its own telegraph", has the rules for drawing another.

### Traps in the middle of the HALLWAY, in rows (2026-10-28)

Eric, after the centring above: "make the traps less random, they should be organized in the middle
of hallways etc." Centred across the corridor, a trap still kept how far ALONG the corridor its
random point had been, so a field of them down the middle of random corridors at random distances
still read as scatter.

Two changes, both maze-only:

- **Only two kinds of spot** (`LevelGenerator.MazeHallwayMiddle`, which replaced
  `MazeCorridorCentre`): the middle of a hallway - the doorway halfway between two cell centres,
  where the passage strip is - or the middle of a hall. The point goes to the nearest of the eight
  round its cell that is open. Every trap is therefore on the maze's own grid, a cell apart down a
  straight run.
- **A trapped hallway is a row** (`Node2DGame.ExtendMazeTrapRow`): a trap in a hallway's middle is
  joined by the next doorways along the same straight run, up to three, of the same kind, firing one
  after another down the hall 0.35 s apart - a gauntlet with a rhythm to time, rather than three dice
  rolls that landed near each other. A hall's middle stands alone. The row spends the chunk's count
  rather than adding to it and draws no rolls, so density holds and open stages are untouched.

Measured (`_BalanceHarness -- --stage=1 --immortal --explore --check-feet --minutes=4 --seed=4242`,
whose `HARNESS hazards:` line now also counts how many stand a cell's pitch from another in a
straight line): before, 180 placed, 5.00 a chunk, closest two 240 px, **0 in a hallway row**; after,
175 placed, 5.00 a chunk, closest two 272 px (two doorways of one cell), 0 within 144 px of a wall,
**153 in a hallway row**. `RegressionChecks.ValidateMazeHazards` now also fails any snapped point
that is neither a hallway's middle nor a hall's ("the traps are scattered again").

**The wooden squares were a rack.** The same report: "the wooden squares in cursed dungeon dont read
- what are they?" The dungeon's `torture` theme had a `rack` drawn as a square board of planks with
a rivet in each corner, which from above is a crate lid. It is a `cage` now (`tools/art/world_props.py`):
iron bars round a skull, drawn back to front so the bones sit inside, the Gaoler's chapter's own
object. Same theme, same obstacle flag, same 64 px cell.

**Which chapter has which trap** (2026-10-28: "there shouldn't be fire vents outside. double check
that all traps make sense for the map they're in"). A placed trap is something somebody built, and a
flame vent is an iron grate set in a floor, so:

| chapter | placed traps | ground hazards (section 2) |
|---|---|---|
| Cursed Dungeon (Castle) | spike plates and flame vents, in hallway rows | pits |
| Ruins | spike plates only - old traps in the stones; its vents (0.18) went | pits (collapsed floor) |
| Emberdeep (Volcanic) | none - it had vents and nothing else, iron grates on open basalt | lava, pits |
| every other chapter | none | none |

`RegressionChecks.ValidateHazardPlacement` holds it: vents only in an indoor chapter, spike plates only
where somebody built something. A volcano's own fire is the lava, which was already there; a natural
fissure that flares on a rhythm would be the right trap for it if the Emberdeep ever wants one, and it
would need its own art rather than the grate.

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
the overlay and walls; the water's shape and field, then its texture and body (section 3; skipped
when no water is near); its prop holder; one step per decor set; the themed props; the stage
hazards. A frame takes one step, and more only while a 2 ms budget lasts (`StepBudgetMs`). The
new chunks are two chunks away when they are queued, so at 60 fps there are hundreds of frames to
do it in. The one exception is the guarantee: if the next chunk in line touches the player's own,
the streamer keeps stepping until it is done, whatever that costs. It only fires if streaming has
fallen far behind - the harness at 3 fps crossed a chunk in twelve frames, and before this rule
the maze window stopped following the player. Tile sources (a texture each) are all built at stage start by
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
  the old one-off scatter round the start never had to - and on the maze they stand in the middle
  of their corridor, keep their separation across chunk borders, and have no decor standing on them
  (section 6, "Traps in the middle of the path").
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
- **Water no longer has a profile.** It had one here (`shore`: 8 px in, a 7 px fade, round
  corners of 22) and its collision followed it role by role; both were replaced by the computed
  shape of section 3.

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

The small things lying on the ground: flower clumps in seven kinds (`flowers_white`, `_yellow`,
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
  `light` emissives) - and open ground in the Forest carries something on about one cell in
  thirteen (the rule densities sum to about 0.077 there; estimated from the rules, not counted).
- **Big enough to be things (second pass).** The first sets drew 3x3 flower heads on 4-14 px
  stems, and at zoom 0.85 on a 1280x720 screen they photographed as two- and three-pixel specks:
  "not fun little environment elements". Every set was redrawn larger and the densities came down
  by about a third with it - fewer, each one a thing. A flower is a 5x5 head with a lit and a
  shaded side on a stem with a leaf, three to five of them standing in a mound of their own
  foliage; a mushroom cluster is a 9 px toadstool with a gill line and one or two smaller ones; a
  lily pad is 18-22 px with its notch and veins (and a 5x5 lily); reeds stand 14-22 px with fat
  brown cattails (they were mauve, and read as pink buds); stones are 5-7 px with a lit top and a
  contact shadow. The XP orbs are round 8-12 px discs of lime, lilac and pale yellow, so no flower
  head is a lone disc of those, yellow flowers are gold and come in clumps, and the brightest
  petal is a `lit` tone, never `hi`. Lily pads were raised a little (forest 0.10, swamp 0.15 of
  water fill) because they are what says "pond" from across the screen.
- **Cost.** Planning a chunk took 7-15 ms in the first version, against a 2 ms frame budget, from
  hashing four noise corners per rule per cell. It now fills each rule's lattice once per chunk,
  compiles the rules per stage into groups by site, and skips any cell whose roll is above three
  times its group's summed density before reading noise or neighbours: 1.0-2.0 ms a chunk, painting
  0.5-2 ms (debug build, desktop, with other sessions' Godot running).
  `RegressionChecks.ValidateGroundArt` holds every set a rule names against `detail.json`.

### The landmark: a flower pen

A walled garden: a wattle fence round a plot 12 x 9 cells (576 x 432 px), a gate in the middle of
its north and its south rail, a path from one to the other round a central bed, four planted beds
(white and violet daisies, a row of cabbages, pink and gold tulips, bluebells), herbs in the
corners, and one moonflower in the middle with a cold light on the soil round it.

It has been redrawn bigger twice. The first pen was a 12 px fence round a doormat of soil and
photographed as a thin brown rectangle. The second was 204 x 140 inside a 20 px fence, fitted into
5 x 4 cells, and the wizard could walk straight through it. Reported: "fence with the flower should
be much bigger area, I should not be able to walk directly through the fence." The garden now
covers about a third of a 1280 x 720 screen. You walk into it rather than past it.

**Two scales.** Everything that stands up (rails, side segments, the moonflower) is drawn at x2
like every other world sprite, so a post is as chunky as the tree trunks round it. The fence stands
40 px, half the wizard, and the moonflower 92 px, a little taller than him. What lies on the ground
(beds, path, soil, the light) is one 1:1 decal, like the ground tiles and the detail layer, so a bed
of daisies matches the daisies outside the fence. All of it is `tools/art/ground_detail.py`, which
also writes `flower-pen.json`: each part's ground row and anchor column, and the layout numbers.

**Sorting.** Each standing part is its own sprite, anchored at its own ground line
`WorldSprites.SortLift` below its node (section 10). There is a back rail, a front rail with its
gate, the flower, and one segment per cell down each side, each sorted at its south end. So the
wizard is drawn in front of the fence south of his boots and behind the fence north of them,
including beside a side rail. The garden decal is `ZIndex` -20, under every actor.

- **Where.** Forest, Swamp, Ruins and Cave only - a flower pen in the ash or the snow would be a
  joke the world has not earned. A chunk rolls 0.25 for one (`LandmarkChance`). The pen needs its
  12 x 9 footprint and a clear ring round it on open floor (no water, no blob terrain, so a fence
  never straddles a shore). It stays at least 3 cells from its chunk's border and never within
  1400 px of the start, and gets 24 tries (`LandmarkAttempts`, up from 8: the footprint is five
  times the area it was). Measured on seed 4242 walking east for two minutes: Forest 19,
  Swamp 21 and Ruins 21 pens placed, against 19, 30 and 26 for the small pen. The Forest kept its
  rate. The Swamp lost about a third, because its peat, moss and water leave fewer open stretches
  of 14 x 11 cells.
- **Deterministic.** `Node2DGame.LandmarkCells` decides from the stage seed and the chunk's own
  terrain, once per chunk's data, and keeps the answer on `TerrainChunk.Landmark`. It runs from
  `TerrainStreamer.PlanFeatures`, the moment the data exists, because the fence has to be in the
  flags before anything reads them (below). Ground detail is not painted inside the pen. Decor,
  themed props and stage hazards are kept out of it (`IsInLandmark`), trees by 3 cells and bushes
  by 1, so no crown hides the garden and no trunk half-closes a gate.

### The fence is solid

The previous note here said "No collision, chosen": a solid pen is a corner the swarm can pin the
player in, and open-stage enemies had no pathfinding to find the way in. Section 9 gave them the
flow field, and the garden answers the corner with a second gate. What makes it solid:

- **Layer 4, the ground's, met by the boots.** The rails are one `StaticBody2D` (`Fence`, under
  the `Landmark`) with six rectangles: the north and south rails either side of their gate, and the
  two sides. Each is 14 px deep, centred on the drawn fence line, on
  `LevelTilePainter.TerrainCollisionLayer`. The player meets that layer with his `Feet` only, so
  the fence stops him where its foot is drawn, not at his chest. Layer 1 would have stopped his
  body collider, the middle of his sprite, 20-40 px short of the fence walking north and with his
  boots through it walking south (section 2). Enemies mask layer 4 too. Measured with
  `_UiShot --shore=up --shore-fence` (walks the wizard into the nearest straight run of fence and
  probes the physics): boots stop 4.3 px short of the rail's collision, from outside walking north
  and from inside walking north into the back rail. Water measured +4 north.
- **In the navigation grid, as fence.** `FlowerPenLayout` (in `scripts/Landmark.cs`) is the one
  description of the shape: the ring of cells round the plot is fence, except the two gate cells
  of the north and south rails. `Node2DGame.PlanChunkFeatures` stamps those cells into the chunk's
  flags as `TerrainStreamer.FlagFence`, and `FillNavigation` makes them solid but not walls. So
  the horde routes to a gate and a bolt still flies over a garden fence. The straight-walk test
  (`HasClearWalk`) and placement (`IsReachable`, and `IsImpassablePosition` asks
  `IsFenceAtWorld` directly) read the same flag. The collision rectangles lie on fence cells only,
  water's rule from section 3: the grid never calls a cell open that a body is stopped in.
- **Gates.** Two cells, 96 px: wider than the wizard's 56 px boots and than the widest body in the
  game, the Deep Warden's 88. Two gates, opposite, so the garden is a room with two doors and
  never a dead end. Ghosts (`IgnoresDecorCollision`) are not excepted from the fence; they mask
  layer 4 and route through the gates like everyone else.
- **Checked.** `RegressionChecks.ValidateFlowerPen` stamps a pen the way the game does and
  requires: rails solid and not walls, gates open, the straight walk through the fence blocked,
  the inside reachable only the long way, the flow beside a side rail running along it rather
  than into it, and every collision rectangle on fence cells. `ValidateGroundArt` holds
  `flower-pen.json` against `FlowerPenLayout`, so the art cannot be drawn to one size while the
  fence collides at another.
- **Still needs eyes.** A full swarm chasing the wizard INTO the garden: whether the horde
  funnelling through a 96 px gate reads as a choke point he can use, or as a trap. A boss fight
  near a garden, since a charge is stopped by the rails like anything else. And the side rails,
  which are thin in three-quarter view: the collision is right, but whether they read as solid
  from across the screen is for a playtest.

- **The light is `ice`, on purpose.** The XP orbs are `grass`, `arcane` and `light`; nothing on
  the ground may look like one, and the fence frames the flower as a place rather than an item.
- **The hook for secrets.** `Landmark.Spawned` / `Landmark.Despawned` (static events), the
  `landmarks` group, `Landmark.Key` (stage, kind and chunk - stable across visits and across runs
  on the same terrain seed, so "found" state can be saved against it), `Bounds` (fence line to
  fence line), `Centre` (the flower) and, new, `Gates` (the middle of each gap). What a secret does
  belongs in that system; `Landmark` owns placement and appearance only. `--terrain-log` prints
  each pen's key and position as it is placed, which is the quick way to find one to `--teleport`
  to.

### Tools

- `--terrain-seed=N` (any scene that runs `Node2DGame`) pins the terrain, so a before and an after
  photograph show the same lakes.
- `python tools/art/tiles.py grass moss` redraws only the named terrains or materials while
  iterating, and leaves `tiles.json` alone; only a full run writes the manifest. **Run it in full
  before committing.**

### Still needs eyes

- **The Swamp's enemies against moss and mud** and every stage on the phone, at the Pixel 9 canvas.
  The photographs here were all desktop, 1280x720.
- **Water was made of whole cells** - fixed in section 3, which has its own list.
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
prevent. The water's collision on layer 4 is built from the same flags and never leaves the
cells they mark (section 3), so the grid and the bodies cannot disagree.

**Props are not in the grid; a garden fence is.** Tree trunks, rocks and fallen logs are small
`StaticBody2D`s that a body slides off. Stamping them in would mean the furnishing code writing
into navigation, and a thicket that walls off a gap is the one case that would need it; trees
keep a minimum spacing for that reason (section 10). The flower pen's fence is the exception: a
576 px wall of rails is not something a body slides off, so its cells are `FlagFence`, solid in
the grid, stamped with the chunk's data (section 8). Ghosts (`IgnoresDecorCollision`)
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

## 10. Trees and bushes at the size of the world

Reported: "Trees should be bigger so the size is closer to realistic. We can use bushes as well."
The trees were 48 px canopies seen from straight above, drawn at x2: 96 px on screen, the height
of the wizard. They read as round green bushes with a stalk.

### The art: `tools/art/trees.py`

Three-quarter view, like every actor: a trunk standing on the ground and a crown above it. Authored
1:1 and saved at x2. The 80 px wizard against them:

| class | sprites | on screen | x the wizard |
|---|---|---|---|
| mature | `oak-large-*`, `fir-1`, `willow-*` | 270-308 px | 3.4-3.9 |
| medium | `oak-medium-*`, `fir-2`, `gnarled-*`, `swamp-snag-1`, `dead-tree-1`, `bleached-tree-1` | 220-260 px | 2.8-3.2 |
| young | `birch-*`, `sapling-1`, `swamp-snag-2`, `bleached-tree-2` | 170-230 px | 2.1-2.9 |
| bush | five Forest (two with berries), sedge tussocks and a shrub (Swamp), brambles (Ruins, Cave), dead bushes and scrub (Sands) | 38-66 px tall | knee to waist |

Per biome: the Forest has oaks, firs, birches and saplings. The Swamp has willows (a dome and a
curtain of strands with the trunk showing through) and drowned snags hung with moss. The Ruins and
the Cave have gnarled half-dead trees and ash-grey dead ones. The Sands have sun-bleached dead
trees. A crown is leaf clusters, each shaded as a lump of its own and as part of the whole crown.
The lower-right seam where one cluster overlaps the one behind it is the internal edge that makes
it many lumps of leaf rather than one blob. Foliage is `verdant`, never the ground's `lichen`
(section 4). Bark is `flesh`, `skin` (birch, bleached wood) or an ash ramp of `skin` and `linen`:
`stone` was invisible on the stone floors. Every tree has an `occ` contact shadow and a dithered
pool of shade under its crown. `trees.json` gives each sprite's ground line, trunk column and trunk
width. The four old sprites (`tree-1`, `tree-2`, `bush-1`, `bush-2`) and their painters in
`world_props.py` are gone.

### Sorting at the feet

Everything in a chunk sorts by its node's y, and the wizard's node is the middle of his sprite. His
footprint (`Feet`, the thing a trunk should stop) is centred 36 px below it. So every grounded
sprite - tree, bush, every part of the flower pen - puts its node `WorldSprites.SortLift` (36)
above its ground line and hangs its texture that far below the node. The wizard is then drawn in
front of a trunk exactly when his footprint is south of the trunk's foot, and behind the crown
exactly when he is north of it. The old decor sorted its middle against his middle, which at 300
px would have put him in front of a tree while standing behind its trunk. Enemies' origins sit a
little nearer their feet than his do; for a basic enemy the error is about 6 px.

### What blocks

- **A tree: the foot of its trunk, on layer 4.** A rectangle the trunk's width (12-28 px) by 12 px
  at the ground line, on the ground's physics layer. The player meets that layer with his boots
  only (section 2), so he stops where the trunk is drawn and walks freely behind the crown.
  Enemies mask layer 4 and stop the same way. On layer 1 the box would have met his body
  collider, the middle of his sprite, which for a tall tree means stopping at his chest. Ghosts
  still pass through: the body is in `decor_props` and gets the usual exception.
- **A bush: nothing.** A sprite and no body, one node where the old bush was three. A swarm
  squeezing through a thicket of small solid bodies wedges on them; one wading through knee-high
  shrubs looks right and plays like open ground. The y-sort still draws a bush over the legs of
  anything standing behind it.
- **Trunks are not in the navigation grid.** At 12-28 px a body slides off one. What stops a
  stand of trees becoming a wall is spacing: a trunk keeps `MatureTreeSpacing` (120 px) from the
  others of its set in its chunk, a young tree `YoungTreeSpacing` (72 px), and trees keep 3 cells
  from a garden. Measured below with `_MazeProbe`, whose new `snagged` line counts stalled
  enemies whose last slide was a decor body or a fence.

### The crown thins when he is behind it

`scenes/shaders/canopy_fade.gdshader`, one `ShaderMaterial` shared by every tree. Each frame
`Node2DGame.UpdateCanopyFade` writes the wizard's position to it: one uniform, whatever the tree
count. A tree whose node is south of him (y-sort is drawing it over him) drops up to 70% of its
pixels, ordered-dithered on the art's own 2 px grid, within 150 px of him. Pixels are dropped, not
blended, so no off-palette colour appears. The radius is wider than he is on purpose: the enemies
that matter are the ones about to reach him, and the hole shows that ring of the swarm too.
Enemies under a crown away from him stay hidden. That is the readability cost of big trees, and
the density below is what keeps it small.

### Density

Per 8400 x 8400 field (section 4), and about how many that puts on a 1280 x 720 screen before
spacing, water and landmarks reject some:

| stage | trees | bushes | per screen |
|---|---|---|---|
| Forest | 150 mature/medium, 90 young (was 430 at 96 px) | 300 | ~2.7 + 1.6 trees, ~5.4 bushes |
| Swamp | 50 willows, 60 snags (was 8 small trees) | 220 tussocks and shrubs (was 18 + 10) | ~2 trees, ~4 bushes |
| Ruins | 48 gnarled and dead (was 8) | 110 brambles (was none) | ~0.9 trees, ~2 bushes |
| Cave | 14 dead and gnarled (was 10 Forest trees) | 60 brambles (was 10 bushes) | ~0.25, ~1 |
| Sands | 26 bleached, mostly alone (was none) | 150 dead bushes and scrub (was none) | ~0.5, ~2.7 |

The Forest is the one stage that got fewer trees. 430 crowns of 300 px would cover most of the
ground. The others got more, because they had almost nothing: the Swamp and the Ruins averaged a
seventh of a tree per screen.

### Cost

Desktop, debug build, headless, `_BalanceHarness --minutes=2 --timescale=2 --walk=0 --immortal
--terrain-log --terrain-seed=4242`, one run each, before and after, with other sessions' Godot
running throughout:

| | Forest | Swamp | Ruins | Sands |
|---|---|---|---|---|
| nodes after the initial load | 2,869 → 1,918 | 463 → 829 | 833 → 1,078 | 379 → 548 |
| nodes far out | 3,800-4,270 → 2,700-3,050 | 580-790 → 970-1,190 | 1,050-1,320 → 1,300-1,600 | 490-720 → 710-780 |
| a crossing, all steps | 33-95 → 29-74 ms | 28-64 → 41-73 ms | 22-69 → 46-88 ms | 16-40 → 26-47 ms |
| worst furnish step of a crossing | 0.9-6.5 → 0.8-6.7 ms | 0.9-14 → 1.1-2.7 ms | 0.9-15 → 1.5-7.5 ms | 0.3-0.7 → 0.4-1.5 ms |

The Forest got cheaper. It has fewer trees, and a bush is one node where it was three. The other
stages cost more because they gained decor they never had, but they stay under the Forest's old
figures. Everything still hangs off the chunk's one holder and is freed with it. Every tree shares
one material, so the canopy fade adds no draw state per tree.

**Walking (feet).** `_BalanceHarness --minutes=2 --character=1 --timescale=2 --walk=30 --immortal
--stage=0 --check-feet`: 0 of 2,693 frames with the footprint inside anything on layer 4, 84
against it, 1 stuck check. On seed 4242: 0 of 3,598, 149 against, 2 stuck checks.
Trunks and fences now count as the ground for that check, and the boots never went into one.

**The swarm (flow field).** `_MazeProbe --stage=0 --seconds=300 --bot=roam`. The new `snagged`
line is stalled enemies whose last slide was a decor body or a fence:

| | reach | detour | pinned | snagged | close |
|---|---|---|---|---|---|
| before, random seed | 100% | 1.05 | 0.0% | - | 15.7% |
| before, seed 4242 | 99.1% | 1.07 | 0.6% | - | 19.1% |
| after, random seed | 100% | 1.06 | 0.0% | 1.8% | 17.5% |
| after, seed 4242 (two runs) | 97.6%, 95.7% | 1.10 | 0.2% | 2.2% | 12.9% |

Trees do not wall the swarm off: 2% of stalled enemies were last against a trunk, a rock or a
fence. The two seed-4242 runs each had a stretch of 10-20 s where reach fell to 50-80%. The probe's
new `low reach` line showed the player on open ground, not a fence or a shore, two chunks from the
navigation window's centre: the roaming bot at timescale 4 kept crossing chunk borders, so the
streamer never settled and the window, which only moves once it has, fell behind
(`RebuildNavigationIfDue`). That behaviour predates this change. The bot hits trunks and turns
more often now, which crosses borders more. Worth a look if a real player ever outruns the window.

### Still needs eyes (trees)

- **Enemies under a crown in a full swarm**, away from the wizard: whether the Forest's density
  hides too much of the horde. The fade only opens round him. A playtest question.
- **The phone.** Photographed on the desktop at the Pixel 9 canvas (`--portrait --safe-area`).
  The shader's cost per pixel on a phone has not been measured; it is a texture read and a few
  multiplies over each tree's quad.
- **A boss under a crown.** The fade keys on the wizard, not the boss, and a boss fight dragged
  into the Forest's thickest stand has not been looked at.
