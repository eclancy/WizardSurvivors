# -*- coding: utf-8 -*-
"""The Sunken Cave's floor (.ai/endgame-castle.md 7.3, piece V2). Drawn by tools/art/tiles.py, which
finds this module by its name and appends TERRAINS to its own - run tiles.py, never this.

The Cave stood on the Ruins' `graystone` until it had ground of its own. `cave_floor` is a terrain used
as the ground, the way the Forest stands on `grass` and the Fields on `stubble` (LevelGenerator's
palette names it as GroundTerrain), because a chapter module can add terrains but not floor styles.

WHY IT STANDS ON THE STONE ROW'S BASE AND NOT ITS SHADE. The Cave is walled now (the caverns,
cave_walls.py), and walled ground has one rule the open floors never needed: the floor must be the
lightest of the three things on screen, the rock's face a step under it and the top of the rock the
darkest - or the passages stop reading as passages. The Cursed Dungeon learned it first and lifted its
flagstones to `base` for the same reason (.ai/terrain.md section 6, "Walls were void"); every natural
floor stands on `shade`, which is where the rock's face has to be. So this floor is `base` with
hollows of `shade`, and still within two stops of itself: the quietest thing on screen.

What makes it a cave floor rather than a ruin's: the damp. Patches of the `arcane` row's deep - a
cold, wet blue-black where water has stood on the stone - and no grass, no paving, nothing built.
Matte, not wet (.ai/art-direction.md section 2): no highlight and no sheen, nothing above the floor's
own stops. The cave's real water is the pools (water.py's stone `pool`), and a floor that glinted
would read as more of it.

Walkable, not a hazard: this module sets no HAZARDS and no BLOCKERS (tiles.py refuses one that does).
"""
import tiles
from tiles import OCC, clumps, crack, hashed, inside, pebble, ramp, specks


def body_cave_floor(c):
    """Worn stone on the stone row's base, with hollows a stop down and damp where water stood."""
    st = ramp("stone")
    ar = ramp("arcane")
    c.rect(0, 0, tiles.SIZE - 1, tiles.SIZE - 1, st[2])
    # Hollows and wear: soft blotches a stop down, the texture in shapes rather than in static.
    clumps(c, 9, 2, 4, st[3], 2101, fill=0.45)
    # The damp: fewer, a little larger, in the arcane row's deep - wet stone under a cold key.
    clumps(c, 4, 2, 4, ar[4], 2111, fill=0.4)
    # Grit, a pixel at a time (the only thing that may touch a tile edge), the odd one lit.
    specks(c, 0.008, st[3], 2121)
    specks(c, 0.004, st[1], 2123)
    specks(c, 0.004, OCC, 2125)
    # Incidents on some variants only: one on every tile is a pattern with a 48 px period.
    v = tiles.VARIANT
    if v % 3 == 0:
        crack(c, inside(v, 2131, 8), inside(v + 3, 2133, 8), 5 + int(hashed(v, 1, 2135) * 5), OCC, 2137 + v)
    if v % 2 == 1:
        pebble(c, inside(v * 3, 2141, 5), inside(v * 3 + 1, 2143, 5), 1 + v % 3 // 2)


TERRAINS = [
    ("cave_floor", body_cave_floor, None),
]
