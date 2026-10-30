# -*- coding: utf-8 -*-
"""The Last Bridge's deck (.ai/endgame-castle.md 3.7). Not run on its own: tools/art/tiles.py finds
this module (tiles.chapter_ground_modules) and appends its TERRAINS to its own, so

    python tools/art/tiles.py deck        # redraw only the deck while iterating
    python tools/art/tiles.py             # the full run that writes tiles.json

"His seat is across the water." The bridge from the black cliffs to his castle: a roadway of laid
setts, the one floor in the game somebody built to be walked on by an army.

ONE TERRAIN, USED AS THE GROUND. The design asks for "floor material `deck`", and a material in
tiles.py is one of a fixed set of floor STYLES a chapter module cannot add to (terrain_fields.py says
why). A terrain brings its own painter, and its fills are what LevelGenerator's GroundTerrain paints
every open cell with - the way the Forest stands on `grass` and the Fallow Fields on `stubble`. So
`deck` is a terrain, and the Bridge's palette stands on it.

SETTS WITH NO DIRECTION IN THEM. The bridge runs at 45 degrees and the grid of tiles does not, so
anything with a direction - courses, ruts, a running bond - would run across the bridge, not along it,
and every 48 px it would say so. The setts here are an irregular packing: sixteen centres relaxed
over the tile (Lloyd's method on the torus, so they wrap) and the joints are where two centres are
nearly equally near. It reads as squarish stones laid by hand, and nothing in it lines up.

THE JOINTS ARE SHARED, THE STONES ARE NOT. The joint pattern is the same in every variant - drawn
from a fixed seed, independent of the variant - so any variant meets any other across a tile edge
with no seam. What a variant changes is inside: two interior stones laid as one long one, a cracked
sett. Tone is the same in every stone (body_deck says why), so the half of a stone in the next tile
always matches.

QUIET. Stone's deep half: faces on its shade stop, the lit edge a stop up on the upper left (the key),
joints in its deep stop. The deck is
two stops above the abyss either side (`occ` with mist) and well under any actor, as every floor is.
Nothing grows here (decided: the bridge's dead are names, never bodies, and its weeds are none).
"""
from __future__ import division

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tiles as T

SIZE = T.SIZE
OCC = T.OCC
ST = T.ramp("stone")

SETTS = 16          # stones per tile: about 12 px each, 10 screen px at zoom 0.85
RELAX = 4           # Lloyd iterations: enough to even the spacing, not enough to make a grid


def h32(a, b, salt):
    """A hash that does NOT read tiles.VARIANT: the shared layer must be the same in every variant."""
    v = (a * 73856093) ^ (b * 19349663) ^ (salt * 83492791)
    v &= 0x7FFFFFFF
    v = (v ^ (v >> 13)) * 1274126177 & 0x7FFFFFFF
    return ((v >> 7) % 10007) / 10007.0


def wrap_delta(a, b):
    d = (a - b) % SIZE
    return d - SIZE if d > SIZE / 2.0 else d


def _centres():
    pts = [(h32(k, 1, 4101) * SIZE, h32(k, 2, 4103) * SIZE) for k in range(SETTS)]
    for _ in range(RELAX):
        sx = [0.0] * SETTS
        sy = [0.0] * SETTS
        n = [0] * SETTS
        for y in range(SIZE):
            for x in range(SIZE):
                best, bd = 0, 1e9
                for k, (px, py) in enumerate(pts):
                    dx, dy = wrap_delta(x + 0.5, px), wrap_delta(y + 0.5, py)
                    d = dx * dx + dy * dy
                    if d < bd:
                        best, bd = k, d
                px, py = pts[best]
                sx[best] += wrap_delta(x + 0.5, px)
                sy[best] += wrap_delta(y + 0.5, py)
                n[best] += 1
        pts = [((px + sx[k] / n[k]) % SIZE, (py + sy[k] / n[k]) % SIZE) if n[k] else (px, py)
               for k, (px, py) in enumerate(pts)]
    return pts


CENTRES = _centres()


def _cells():
    """Per pixel: (nearest centre, whether it is a joint). Computed once - the same in every variant."""
    owner = [[0] * SIZE for _ in range(SIZE)]
    joint = [[False] * SIZE for _ in range(SIZE)]
    for y in range(SIZE):
        for x in range(SIZE):
            ds = []
            for k, (px, py) in enumerate(CENTRES):
                dx, dy = wrap_delta(x + 0.5, px), wrap_delta(y + 0.5, py)
                ds.append((math.sqrt(dx * dx + dy * dy), k))
            ds.sort()
            owner[y][x] = ds[0][1]
            # A joint is where the two nearest stones are nearly as near: a line one pixel wide,
            # thickening a little where three meet, which is what laid stone does.
            joint[y][x] = ds[1][0] - ds[0][0] < 1.15
    return owner, joint


OWNER, JOINT = _cells()

# Stones whose pixels touch the tile edge. They keep the shared look in every variant.
BORDER = set()
for _y in range(SIZE):
    for _x in range(SIZE):
        if _x in (0, SIZE - 1) or _y in (0, SIZE - 1):
            BORDER.add(OWNER[_y][_x])


def _neighbours():
    """Pairs of interior stones that share a joint - candidates to be laid as one long stone."""
    pairs = set()
    for y in range(1, SIZE - 1):
        for x in range(1, SIZE - 1):
            if not JOINT[y][x]:
                continue
            a = OWNER[y][x]
            for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1), (2, 0), (0, 2)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < SIZE and 0 <= yy < SIZE:
                    b = OWNER[yy][xx]
                    if b != a and a not in BORDER and b not in BORDER:
                        pairs.add((min(a, b), max(a, b)))
    return sorted(pairs)


INTERIOR_PAIRS = _neighbours()


def body_deck(c):
    """The bridge's roadway: an irregular packing of setts, lit on the upper left.

    Faces on stone's shade stop. Each stone's upper-left rim catches the key a stop above its face, its
    lower-right rim sits a pixel into shadow, and the joints are stone's deep stop. Per variant: a pair
    of interior stones laid as one, or a cracked sett.
    """
    v = T.VARIANT
    merged = set()
    if v in (1, 3, 5) and INTERIOR_PAIRS:
        merged.add(INTERIOR_PAIRS[int(h32(v, 7, 4201) * len(INTERIOR_PAIRS))])
    if v == 5 and len(INTERIOR_PAIRS) > 1:
        merged.add(INTERIOR_PAIRS[int(h32(v, 9, 4203) * len(INTERIOR_PAIRS))])
    interior = [k for k in range(SETTS) if k not in BORDER]
    cracked = interior[int(h32(v, 5, 4207) * len(interior))] if (v in (2, 4) and interior) else -1
    # Every stone is the same face. The first pass made one stone in three a stop lighter, then one or
    # two interior stones per variant, and missing setts on one variant in six: in a field of tiles each
    # of them came back on the same few spots of every tile and photographed as a lattice of dots. The
    # deck's variety is world-space instead - the detail layer's cracks, scorch, stains and dropped
    # bolts, clustered (detail_bridge.py), and the palette's spills of scree.
    def light(k):
        return False

    def merged_with(a, b):
        return (min(a, b), max(a, b)) in merged

    def is_joint(x, y):
        if not JOINT[y][x]:
            return False
        # A joint between two stones laid as one is not drawn: look across it.
        a = OWNER[y][x]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            xx, yy = (x + dx) % SIZE, (y + dy) % SIZE
            b = OWNER[yy][xx]
            if b != a:
                return not merged_with(a, b)
        return True

    joint = [[is_joint(x, y) for x in range(SIZE)] for y in range(SIZE)]
    for y in range(SIZE):
        for x in range(SIZE):
            k = OWNER[y][x]
            if joint[y][x]:
                c.set(x, y, ST[4])
                continue
            face, lit = (ST[2], ST[1]) if light(k) else (ST[3], ST[2])
            up = joint[(y - 1) % SIZE][x]
            left = joint[y][(x - 1) % SIZE]
            down = joint[(y + 1) % SIZE][x]
            right = joint[y][(x + 1) % SIZE]
            if up or left:
                c.set(x, y, lit)
            elif (down or right) and h32(x, y, 4221) < 0.5:
                c.set(x, y, ST[4] if face == ST[3] else ST[3])
            else:
                c.set(x, y, face)
    if cracked >= 0:
        # A crack across one sett, inside it: from a pixel of that stone, a few steps, turning once.
        pts = [(x, y) for y in range(2, SIZE - 2) for x in range(2, SIZE - 2)
               if OWNER[y][x] == cracked and not joint[y][x]]
        if pts:
            x, y = pts[len(pts) // 2]
            ang = h32(v, cracked, 4231) * math.pi * 2.0
            px, py = float(x), float(y)
            for j in range(7):
                if j == 3:
                    ang += 0.9
                ix, iy = int(px), int(py)
                if 0 <= ix < SIZE and 0 <= iy < SIZE and OWNER[iy][ix] == cracked and not joint[iy][ix]:
                    c.set(ix, iy, ST[4])
                px += math.cos(ang)
                py += math.sin(ang)


# terrain -> (painter, rim tone for a hard edge), as tiles.TERRAINS. Walkable, not a hazard: this
# module sets no HAZARDS and no BLOCKERS (tiles.py refuses one that does). The drop is water.
TERRAINS = [
    ("deck", body_deck, None),
]
