# -*- coding: utf-8 -*-
"""The Court's floors (.ai/endgame-castle.md 5.2 and 5.8). Not run on its own: tools/art/tiles.py
finds this module (tiles.chapter_ground_modules) and appends its TERRAINS, so

    python tools/art/tiles.py court_marble runner_red      # redraw only these while iterating
    python tools/art/tiles.py                               # the full run that writes tiles.json

"A court with no one left in it." The halls stand on the castle's flags (the Dungeon's `stonetile`:
the same stone his cells are cut from, one stair down). What is laid on them is the Court's:

    court_marble    a chequer of polished stone - the Great Hall, the Chapel, the Treasury, the
                    Keepsake Doors and the throne room's vestibule
    court_boards    oak boards - the Library, the Gallery, the Map Room, the Minstrels' Gallery, the Solar
    runner_red      the carpet down the middle of the red wing's halls (the Great Hall's)
    runner_blue     the blue wing's (the Library's and the Chapel's)
    runner_green    the green wing's (the Armoury, the Kennels and the stair from the cells)
    runner_gold     the gold wing's (the Treasury's, and the hall to the throne)

LAID, NOT GROWN. Every other walkable terrain fades into the floor through a dithered mask, because
moss and dirt have no edge. A carpet and a laid floor have nothing but an edge: a straight, square
border somebody wove or set, lit along the side that faces the key (north and west) and throwing a
one-pixel contact shadow on the flags beside it (south and east). So these draw their own edges
(tiles.EDGE_OF) instead of a profile's fade. The layout never lets one touch a wall
(court_layout.py), so the edge is always drawn over the flags.

WHICH PART OF THE HOUSE AM I IN (5.2). Each wing's runner is its own hue, from the deep half of its
ramp - pigment, not light: the cloth of a house, never a glow - and, because hue must never be the
only carrier, its own PATTERN too, in the field and in the border:

    red     a lozenge lattice          border: a gold thread with gold dots
    blue    small crosses               border: two pale lines
    green   a chequer                   border: a zigzag
    gold    rings                       border: beads

QUIET, as every floor is (tiles.py's note): values within a stop or two of the flags, nothing
emissive, nothing in an XP orb's colours, and every pattern a whole number of periods across the
tile so a runner of any variants side by side is one carpet.
"""
from __future__ import division

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tiles as T

SIZE = T.SIZE
OCC = T.OCC
CLEAR = T.CLEAR
ramp = T.ramp
hashed = T.hashed

ST = ramp("stone")
SK = ramp("skin")
GD = ramp("gold")
RD = ramp("red")
WO = ramp("wool")
LI = ramp("lichen")
LN = ramp("linen")

# Where a laid edge sits: 5 px in from the cell's exposed side, square corners, no wander. The inner
# corner is a quarter-disc of that radius (tiles.signed_distance), so a runner's T-junction has a
# small rounded notch rather than a pinhole.
LAID = dict(b=5.0, w=0.0, r=0.0, amp=0.0)


# --- the drawn edge ---------------------------------------------------------------------------------

def _e(role, x, y):
    return T.signed_distance(role, x, y, LAID)


def laid_edge(band, border):
    """An EDGE_OF function: cut the role's shape out square, then over the outermost `band` pixels
    inside it draw border(c, x, y, depth, along) - depth 0 at the boundary - with the lit line on the
    north and west boundary and the contact shadow outside the south and east one."""
    def edge(c, role):
        inside = {}
        for y in range(-1, SIZE + 1):
            for x in range(-1, SIZE + 1):
                inside[(x, y)] = _e(role, x, y)
        for y in range(SIZE):
            for x in range(SIZE):
                e = inside[(x, y)]
                if e < 0:
                    # Outside: clear, but for the shadow the carpet throws one pixel south and east.
                    if inside[(x, y - 1)] >= 0 or inside[(x - 1, y)] >= 0:
                        c.set(x, y, OCC)
                    else:
                        c.set(x, y, CLEAR)
                    continue
                if e >= band:
                    continue
                # Along which way does this stretch of border run? Across the steeper change in e.
                ex = abs(inside[(x + 1, y)] - inside[(x - 1, y)])
                ey = abs(inside[(x, y + 1)] - inside[(x, y - 1)])
                along = y if ex > ey else x
                border(c, x, y, int(e), along)
                # The lit lip, where the key catches the top or the west edge of the weave.
                if e < 1 and (inside[(x, y - 1)] < 0 or inside[(x - 1, y)] < 0):
                    c.set(x, y, border_lit(border))
    return edge


def border_lit(border):
    return getattr(border, "lit", ST[1])


# --- the laid floors ---------------------------------------------------------------------------------

def body_marble(c):
    """Polished stone in a chequer of 24 px squares, a stop apart, with a vein or two in the light ones.

    Kept darker than any body that stands on it: the cast is drawn from the deep half of linen and
    steel (L* 20-30), so the lighter square is stone's base stop and the darker its shade - the
    flags' own value, one stop apart - and the veins are stone's lit and highest, thin and broken. A
    chequer of lit and shade stops was tried first and read as a chessboard across a hall the size of
    the Great Hall. The squares sit on the same pixels in every variant (so a floor is one chequer);
    the veins and the wear are each variant's own.
    """
    for y in range(SIZE):
        for x in range(SIZE):
            light = ((x // 24) + (y // 24)) % 2 == 0
            c.set(x, y, ST[2] if light else ST[3])
    # The joints between squares: a fine dark line along the top and left of each, lit just below.
    for k in (0, 24):
        for i in range(SIZE):
            c.set(i, k, ST[4])
            c.set(k, i, ST[4])
    # Veins: a wandering line across a light square, on some variants.
    for sq, (ox, oy) in enumerate(((0, 0), (24, 24))):
        if hashed(sq, 1, 2101) > 0.55:
            continue
        ang = hashed(sq, 2, 2102) * math.pi
        px, py = ox + 4 + hashed(sq, 3, 2103) * 16, oy + 4 + hashed(sq, 4, 2104) * 16
        for j in range(14):
            if j % 4 == 0:
                ang += (hashed(j, sq, 2105) - 0.5) * 1.2
            px += math.cos(ang)
            py += math.sin(ang)
            ix, iy = int(px), int(py)
            if ox + 1 <= ix < ox + 23 and oy + 1 <= iy < oy + 23 and hashed(j, sq, 2106) < 0.8:
                c.set(ix, iy, ST[1] if j % 3 else ST[0])
    # Wear: a scuffed corner on a dark square now and then.
    if T.VARIANT in (2, 4):
        x, y = 30 + int(hashed(T.VARIANT, 1, 2110) * 12), 4 + int(hashed(T.VARIANT, 2, 2111) * 14)
        for k in range(5):
            c.set(x + k % 3, y + k // 3, ST[2])


def marble_border(c, x, y, depth, along):
    # An inlaid kerb round the field: a dark band with a pale fillet in it.
    c.set(x, y, ST[4] if depth in (0, 3) else (ST[2] if depth in (1, 2) else ST[3]))


marble_border.lit = ST[0]


def body_boards(c):
    """Oak boards, laid east-west: six planks to the tile on fixed rows, end joints staggered.

    Gold's deep stop for the wood and its shade for the grain - the one warm brown the contract holds
    for a floor, and pigment only. Every plank's seam is on the same row in every variant, so boards
    run on through any neighbour; the end joints and the grain are the variant's own.
    """
    c.rect(0, 0, SIZE - 1, SIZE - 1, GD[4])
    for row in range(6):
        top = row * 8
        for x in range(SIZE):
            c.set(x, top, OCC)                      # the seam
            if hashed(x, row, 2201) < 0.7:
                c.set(x, top + 1, GD[3])            # the plank's lit edge
        # End joints, two a plank row at the variant's own places, kept off the tile edges.
        for k in range(2):
            jx = 6 + int(hashed(row, k, 2203 + T.VARIANT) * 36)
            for y in range(top + 1, top + 8):
                c.set(jx, y, OCC)
                c.set(jx + 1, y, GD[3] if y == top + 1 else GD[4])
        # Grain: a broken streak along the plank.
        gy = top + 3 + int(hashed(row, 3, 2207) * 3)
        gx = int(hashed(row, 4, 2208 + T.VARIANT) * 30)
        for x in range(gx, gx + 10 + int(hashed(row, 5, 2209) * 8)):
            if hashed(x, row, 2210) < 0.75:
                c.set(x % SIZE, gy, GD[3])
    # A knot, now and then.
    if T.VARIANT in (1, 3, 5):
        kx, ky = T.inside(T.VARIANT, 2215, 6), 4 + 8 * int(hashed(T.VARIANT, 6, 2216) * 5)
        c.set(kx, ky, OCC)
        c.set(kx + 1, ky, GD[3])


def boards_border(c, x, y, depth, along):
    # A frame of boards laid the other way round the room, its own seam inside it.
    if depth == 3:
        c.set(x, y, OCC)
    elif depth == 0:
        c.set(x, y, GD[3])
    else:
        c.set(x, y, GD[4] if (along // 13) % 2 or depth != 1 else GD[3])


boards_border.lit = GD[2]


# --- the runners -----------------------------------------------------------------------------------
#
# Each is a field (a pattern of period 8, 12, 16 or 24, so it tiles) inside a border band 9 px deep,
# whose own pattern runs along the edge on a period dividing 48. In a two-cell runner every cell is an
# edge tile, so a runner is mostly border and a stripe of field: the border has to carry the wing too.

RUNNER_BAND = 9


def field_red(c):
    """Lozenges: a diamond lattice of red's shade on red's base, a lit point at every crossing."""
    for y in range(SIZE):
        for x in range(SIZE):
            u = (x + y) % 12
            v = (x - y) % 12
            c.set(x, y, RD[3] if u == 0 or v == 0 else RD[2])
            if u == 6 and v == 6:
                c.set(x, y, RD[1])


def border_red(c, x, y, depth, along):
    if depth == 0 or depth == 8:
        c.set(x, y, RD[4])
    elif depth in (2, 6):
        c.set(x, y, GD[3])                           # the gold thread either side
    elif depth == 4 and along % 6 == 3:
        c.set(x, y, GD[2])                           # its dots
    else:
        c.set(x, y, RD[4] if depth in (1, 7) else RD[3])


border_red.lit = RD[1]


def field_blue(c):
    """Crosses: a small cross of wool's deep on its shade every 12 px, a pale point in each."""
    c.rect(0, 0, SIZE - 1, SIZE - 1, WO[3])
    for cy in range(6, SIZE, 12):
        for cx in range(6, SIZE, 12):
            off = 6 if (cy // 12) % 2 else 0
            px = (cx + off) % SIZE
            for d in (-2, -1, 1, 2):
                c.set((px + d) % SIZE, cy, WO[4])
                c.set(px, (cy + d) % SIZE, WO[4])
            c.set(px, cy, WO[2])


def border_blue(c, x, y, depth, along):
    if depth in (2, 5):
        c.set(x, y, WO[2] if along % 8 else WO[3])  # two pale lines, broken now and then
    else:
        c.set(x, y, WO[4])


border_blue.lit = WO[2]


def field_green(c):
    """A chequer of 8 px squares in lichen's shade and deep, the darker crossed with a fine line."""
    for y in range(SIZE):
        for x in range(SIZE):
            dark = ((x // 8) + (y // 8)) % 2 == 1
            c.set(x, y, LI[4] if dark else LI[3])
            if dark and (x % 8) == (y % 8):
                c.set(x, y, LI[3])


def border_green(c, x, y, depth, along):
    # A zigzag running along the edge: period 8 along, between depths 2 and 6.
    z = along % 8
    zig = 2 + (z if z < 4 else 8 - z)
    if depth == zig:
        c.set(x, y, LI[2])
    elif depth in (0, 8):
        c.set(x, y, OCC if depth == 8 else LI[4])
    else:
        c.set(x, y, LI[4])


border_green.lit = LI[1]


def field_gold(c):
    """Rings: a roundel of gold's shade every 16 px on its deep stop, lit on its upper left.

    Gold is the brightest pigment in the contract (its base is a light source on this floor), so the
    gold wing's runner is drawn a stop lower than the others' fields - the shade for the rings, the
    deep for the cloth - or it is the loudest thing in every hall it runs down."""
    c.rect(0, 0, SIZE - 1, SIZE - 1, GD[4])
    for cy in range(8, SIZE, 16):
        for cx in range(8, SIZE, 16):
            for y in range(cy - 4, cy + 5):
                for x in range(cx - 4, cx + 5):
                    d = math.hypot(x - cx, y - cy)
                    if 2.6 <= d < 3.8:
                        c.set(x % SIZE, y % SIZE, GD[3] if (x - cx) + (y - cy) < 2 else OCC)
            c.set(cx, cy, GD[3])


def border_gold(c, x, y, depth, along):
    # Beads: a row of dashes, light and dark by turns, between two dark lines.
    if depth in (1, 7):
        c.set(x, y, OCC)
    elif depth == 4:
        c.set(x, y, GD[3] if (along // 3) % 2 == 0 else GD[4])
    else:
        c.set(x, y, GD[4])


border_gold.lit = GD[3]


# terrain -> (painter, rim tone for a hard edge): the rim is unused - each draws its own edge.
TERRAINS = [
    ("court_marble", body_marble, None),
    ("court_boards", body_boards, None),
    ("runner_red", field_red, None),
    ("runner_blue", field_blue, None),
    ("runner_green", field_green, None),
    ("runner_gold", field_gold, None),
]

T.EDGE_OF.update({
    "court_marble": laid_edge(5, marble_border),
    "court_boards": laid_edge(5, boards_border),
    "runner_red": laid_edge(RUNNER_BAND, border_red),
    "runner_blue": laid_edge(RUNNER_BAND, border_blue),
    "runner_green": laid_edge(RUNNER_BAND, border_green),
    "runner_gold": laid_edge(RUNNER_BAND, border_gold),
})
