# -*- coding: utf-8 -*-
"""The Fallow Fields' ground (.ai/campaign.md section 5). Not run on its own: tools/art/tiles.py
finds this module (tiles.chapter_ground_modules) and appends its TERRAINS to its own, so

    python tools/art/tiles.py stubble furrow hedge_bank     # redraw only these while iterating
    python tools/art/tiles.py                                # the full run that writes tiles.json

"He takes a tithe of everything that grows here." The farms beyond the wood, after the harvest he
took: a stubble floor, patches of bare ploughed earth, and the grassy banks the hedgerows grow
on. Three terrains, all walkable, all on the `soft` edge profile (.ai/terrain.md section 8), and
no hazard or blocker - water is the chapter's one blocker and it is tiles.py's own.

THE STUBBLE IS A TERRAIN, NOT A MATERIAL. The design asked for "a stubble material", and a
material in tiles.py is one of a fixed set of floor STYLES (natural, flagstone, void) that a
chapter module cannot add to without editing tiles.py. A terrain brings its own painter, and a
terrain's fills are what LevelGenerator's GroundTerrain paints every open cell with - which is
exactly how the Forest stands on `grass`. So `stubble` is a terrain used as the ground.

FROM THE DEEP HALF, AND QUIET. Stubble stands on gold's deep stop with linen's deep in it, the
cut straw in gold's shade and base - two stops at most inside a tile (.ai/art-direction.md 4c) -
so a linen-grey field hand on it reads by hue before value, and nothing on the floor competes with
an XP orb. The ploughed earth is linen's deep with flesh-deep clods; the bank is lichen's deep
with grass and dock.

ROWS THAT TILE. A harvested field and a ploughed one both come in rows, and rows across tiles are
the one place a stripe is the subject rather than the failure. Every row sits on the SAME pixel
rows in every variant (as flagstone's courses do), and its wave is a whole number of periods across
the tile, so a row runs on through any neighbour; only what lies in it changes per variant.
"""
from __future__ import division

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tiles as T

SIZE = T.SIZE
OCC = T.OCC
ramp = T.ramp
hashed = T.hashed
inside = T.inside
scatter = T.scatter
specks = T.specks
clumps = T.clumps
tuft = T.tuft
pebble = T.pebble
leaf = T.leaf

GD = ramp("gold")
LN = ramp("linen")
FL = ramp("flesh")
LI = ramp("lichen")
VE = ramp("verdant")

# Rows per tile. Six (one every 8 px) photographed as lines of print and as a striped rug: at
# camera zoom 0.85 that is a row every 7 screen pixels, which is texture the eye reads as pattern.
STUBBLE_ROWS = 4          # rows of cut stalks per tile: one every 12 px
FURROWS = 4               # ridges per tile, likewise


def body_stubble(c):
    """A harvested field: cut stalks standing in rows on dark soil.

    Gold's deep stop for the soil, soft patches of linen's deep where the straw has rotted down,
    and the stubble itself - each stalk two or three pixels of gold's shade with a base-stop tip,
    its foot in shadow - laid along six rows. The rows are on fixed pixel rows in every variant;
    which stalks stand, and where, is the variant's own.
    """
    c.rect(0, 0, SIZE - 1, SIZE - 1, GD[4])
    # Mostly the variant's own: a shared blotch repeats every 48 px and a field of them is a grid.
    clumps(c, 6, 1, 4, LN[4], 1101, fill=0.35, shared=0.15)
    clumps(c, 3, 1, 3, GD[3], 1103, fill=0.1, shared=0.15)
    specks(c, 0.006, LN[3], 1105)
    for row in range(STUBBLE_ROWS):
        base = row * 12 + 7
        x = int(hashed(row, 1, 1111) * 4)
        k = 0
        while x < SIZE:
            gap = 3 + int(hashed(row, k, 1113) * 5)
            if hashed(row, k, 1115) > 0.35:            # stalks missing, in runs: a row, not a dotted line
                h = 2 + (1 if hashed(row, k, 1117) > 0.7 else 0)
                y = base + (1 if hashed(row, k, 1119) > 0.7 else 0)
                tip = GD[2] if hashed(row, k, 1123) > 0.8 else GD[3]
                for j in range(h):
                    c.set(x, (y - j) % SIZE, GD[3] if j < h - 1 else tip)
                c.set(x, (y + 1) % SIZE, LN[4])
                if hashed(row, k, 1121) > 0.6 and x + 1 < SIZE:
                    c.set(x + 1, (y + 1) % SIZE, OCC)    # the stalk's shadow, away from the key
            x += gap
            k += 1
    # Now and then a fallen straw lying across the rows, or a weed the plough missed.
    if T.VARIANT in (2, 5):
        x, y = inside(T.VARIANT, 1131, 8), inside(T.VARIANT + 3, 1133, 8)
        for j in range(5):
            c.set(x + j, y + (j // 3), GD[2] if j % 2 else GD[3])
    elif T.VARIANT == 4:
        tuft(c, inside(1, 1141, 6), inside(2, 1143, 6), LN[4], LN[3], LN[2])


def body_furrow(c):
    """Bare ploughed earth: four ridges of turned soil with a dark trough between each.

    The ridge is the stubble's own soil, gold's deep stop, with its top lit a stop up where the key
    catches it; the trough is linen's deep with occlusion in it. Ploughed ground is the field with
    its stubble turned under, so it is the same earth, ridged and shadowed - grey-green ridges read
    as a slab of concrete beside the stubble, and flesh's deep as a purple rug. Each ridge wanders
    by a pixel on a sine whose period is the tile, the same in every variant, so a ridge never
    breaks at a seam; the clods and the odd stone are each variant's own.
    """
    c.rect(0, 0, SIZE - 1, SIZE - 1, GD[4])
    for row in range(FURROWS):
        base = row * 12 + 3
        for x in range(SIZE):
            # A kink of a pixel now and then, not a wave: a wavy grey ridge read as ripples on water.
            wave = int(round(0.6 * math.sin(2.0 * math.pi * x / SIZE + row * 1.3)))
            top = base + wave
            for j in range(1, 6):
                c.set(x, (top + j) % SIZE, GD[4] if (x * 5 + j * 3 + row) % 6 else FL[4])
            c.set(x, top % SIZE, GD[3] if (x * 7 + row) % 9 > 3 else GD[4])
            # the trough: two rows of shadow, the lower lit by nothing at all now and then
            c.set(x, (top + 6) % SIZE, LN[4])
            c.set(x, (top + 7) % SIZE, OCC if (x * 3 + row) % 3 == 0 else LN[4])
    clumps(c, 5, 1, 2, FL[4], 1157, fill=0.3)
    # Clods turned up on the ridges, and specks of the trough's dark.
    def clod(cv, x, y, k):
        cv.set(x, y, GD[4] if k % 3 == 0 else FL[4])
        cv.set(x + 1, y, LN[2] if k % 2 else FL[4])
        cv.set(x, y + 1, LN[4])
    scatter(c, 9, 1151, 3, clod)
    specks(c, 0.01, OCC, 1155)
    if T.VARIANT % 2 == 1:
        pebble(c, inside(T.VARIANT, 1161, 5), inside(T.VARIANT + 2, 1163, 5), 1 + T.VARIANT % 3 // 2)


def body_hedge_bank(c):
    """The bank a hedgerow grows on: rough grass and dock over dark earth, a few fallen leaves.

    Verdant's deep for the grass in shadow - a green bank, where lichen's teal on the warm stubble
    read as a pond - with lichen's deep in the hollows, tufts in verdant's shade, dock leaves a stop
    up: living things on the saturated ramp, as .ai/terrain.md section 4 found they must be or they
    smudge into the ground. Leaf litter from the hedge in gold and flesh.
    """
    c.rect(0, 0, SIZE - 1, SIZE - 1, VE[4])
    clumps(c, 9, 2, 4, LI[4], 1201, fill=0.45)
    specks(c, 0.014, OCC, 1203)
    scatter(c, 12, 1211, 3, lambda cv, x, y, k: tuft(cv, x, y, OCC if k % 3 == 0 else None, VE[3], VE[3] if k % 4 else LI[3]))

    def dock(cv, x, y, k):
        for (dx, dy, t) in ((0, 0, 3), (1, 0, 2), (2, -1, 2), (-1, 1, 3), (0, 1, 4), (1, 1, 3)):
            cv.set(x + dx, y + dy, VE[t])
        cv.set(x + 1, y + 2, OCC)
    scatter(c, 4, 1221, 5, dock, shared=0.25)
    scatter(c, 4, 1231, 3, lambda cv, x, y, k: leaf(cv, x, y, (GD[3], FL[3], GD[2])[k % 3], FL[4], k))
    if T.VARIANT == 3:
        pebble(c, inside(2, 1241, 5), inside(5, 1243, 5), 2)


# terrain -> (painter, rim tone for a hard edge): all soft, so no rim.
TERRAINS = [
    ("stubble", body_stubble, None),
    ("furrow", body_furrow, None),
    ("hedge_bank", body_hedge_bank, None),
]
