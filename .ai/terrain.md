# Terrain: how the ground is painted, and what stops you

Three layers, eleven autotile terrains, and two independent properties a terrain can have. This
is the part of the game a player looks at for fifteen minutes at a stretch without ever noticing
it, which is the standard it has to meet.

## 1. Three layers, and why transparency is the useful one

`LevelTilePainter` builds three `TileMapLayer`s over one shared `TileSet`:

| Layer | What it holds | Collision |
|---|---|---|
| `Ground` | a base floor tile on **every** cell, always | off |
| `Overlay` | the autotiled terrain — grass, water, lava, ice… | **on**, but only blocking tiles carry a body |
| `Walls` | maze walls, painted as solid blocks | on |

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
(terrain, role), on the same physics layer the maze walls use. **The polygon is shaped to the
role**, not the cell: an edge tile is half shoreline and a corner is mostly shoreline, and giving
those a full-cell body would stop the player a tile short of the water on ground that plainly
looks walkable — the most annoying kind of invisible wall. See `BlockingPolygon`.

Three other places have to agree, and they are easy to miss:

- `LevelGenerator.SafeZoneExcludedTerrains` — a lake generated over the spawn point would start
  the run with the player inside a wall.
- `Node2DGame.IsImpassablePosition` — everything that *places* rather than moves: enemy spawns,
  formations, elite spawns. The water half is deliberately not gated on `MazeNavigation` the way
  the wall half is, because a maze wall only exists on a maze stage and a lake can be anywhere.
- `Node2DGame.PropAvoidTerrains` — already listed water before any of this.

**Enemies are blocked by water too.** They mask collision layer 1, the same as the player, so a
lake is a mutual obstacle exactly as a wall is. On the Swamp, which has by far the most water of
any chapter, that has not been playtested — enemies steer straight at the player and slide along
a shoreline rather than pathing round it, because the wall-aware flow field only runs on maze
stages.

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

`SHORELINE` in `tiles.py` is the set of terrains that blend rather than outline. Water is the only
member. An outline is not wrong for a hazard: a lava edge that faded into the grass would be lying
about where the damage starts.

## 4. Decor density, and the two things that hide it

The Enchanted Forest had no trees in it for as long as it existed, and neither of the reasons was
"the art is missing" — all eleven decor sprites and all twenty-one themed props were generated,
wired and spawning the whole time.

**The field is 8400 × 8400 and the camera sees 720 × 1280 — about 1.2% of it.** At the old counts
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
