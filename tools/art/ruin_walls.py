# -*- coding: utf-8 -*-
"""The Mystic Ruins' broken walls: the `ruin_wall` set. Drawn by tools/art/tiles.py (wall_set_modules)
- run tiles.py, never this. Piece V1 of .ai/endgame-castle.md (7.3): "broken spires and shattered walls".

The ruin topology (scripts/LevelGenerator.Ruin.cs) is the Dungeon's maze at the rates that broke it up,
with gaps knocked through its walls, fallen pillars, courts and colonnades. Its cells are the `wall`
terrain like every wall in the game; this set is only how they are DRAWN on the Ruins
(StageEnvironmentProfile.WallSet = "ruin_wall"), in the jail set's shape (jail_walls.py): every autotile
role, the three stubs the spawn clearing leaves, plain and dressed variants.

UNDER THE OPEN SKY, SO THE ORDER OF VALUES TURNS OVER. The jail and the cave are lightless, and there the
top of a wall is the darkest thing on screen. A ruin stands outdoors, and the flat top of a wall is the
one surface that faces the key light full on: so here the TOP is the lightest stone (the stone row's
base, its edges lit), the FACE in the row's shade under a lit coping, and the floor (the Ruins'
`graystone`, on the row's shade with its deep in it) below both, with the wall's shadow thrown across it
(jail_walls.shadow, shared by every wall). Still two stops either side of the floor at most, and never
`hi` as a plane: the ground stays the quietest thing on screen.

WEATHERED, NOT KEPT. Big ashlar blocks rather than the jail's small courses, chipped at every corner,
the coping broken off along stretches, grass and moss in the joints of the tops, ivy down the faces.
Nothing on a ruin wall is warm (a warm glint is a secret's tell, .ai/stage-secrets.md) and nothing is lit:
the wards cut into the stones are dark - "the old wards were broken from the inside" - so a ward carved
here is a dead one, a groove in the stone, and the Broken Ward's living stones will be the only ones that
answer. Nothing animates, and nothing is drawn as a way through: a niche is shallow and empty.

THE SAME RULES AS THE GROUND (tiles.py): every colour from the contract, features a neighbour could cut
kept inside the tile, and what crosses an edge - the courses of a face, the rows of slabs on a top - at
the same place in every variant.
"""
from __future__ import division

import math

import bonelight as bl
import jail_walls as jw
from jail_walls import BAYER, SIZE, _get, _set, hashed

OCC = bl.OCC
CLEAR = (0, 0, 0, 0)

TERRAIN = "ruin_wall"

ST = jw.ST      # stone: hi lit base shade deep
LI = jw.LI      # lichen (moss, grass in the joints)
VE = [bl.tone("verdant", r) for r in ("hi", "lit", "base", "shade", "deep")]   # living leaves (ivy)
SK = jw.SK      # bone

SIDE_ROLES = ("edge_left", "edge_right")

FACE_DRESSINGS = [
    ("ivy", 4),         # ivy down the face from the top, in leaves of the living row
    ("ward", 2),        # a ward cut into a block, dead: a groove, no light
    ("niche", 2),       # a shallow, empty niche where something stood
    ("breach", 2),      # a block gone out of the face, cracks running from the hole
    ("tumble", 2),      # fallen blocks at the foot
    ("moss", 2),        # moss in the lower joints where the rain runs off
    ("column", 1),      # a pilaster carved in the face, broken off above
]
SIDE_DRESSINGS = [("grass", 2), ("ivy", 1)]
CAP_DRESSINGS = [("grass", 2), ("fallen", 1)]

STUB_SIDES = jw.STUB_SIDES

PLAIN_VARIANTS = {
    "edge_bottom": 4, "corner_bl": 2, "corner_br": 2,
    "edge_left": 3, "edge_right": 3,
    "edge_top": 4, "fill": 4,
}


def variants(role):
    """[(dressing or None, plain index, weight)] for a role, plain first, in manifest order."""
    plain = PLAIN_VARIANTS.get(role, 2)
    out = [(None, k, 1) for k in range(plain)]
    if role == "edge_bottom":
        out += [(d, 0, w) for d, w in FACE_DRESSINGS]
    elif role in SIDE_ROLES:
        out += [(d, 0, w) for d, w in SIDE_DRESSINGS]
    elif role in ("edge_top", "fill"):
        out += [(d, 0, w) for d, w in CAP_DRESSINGS]
    return out


def seed_for(dressing, plain_index):
    names = [d for d, _ in FACE_DRESSINGS]
    return 61 + plain_index * 17 + (names.index(dressing) * 7 + 3 if dressing in names else 0)


def wander(i, salt, seed, amp):
    """A sum of sin(pi n i / 47): zero at both ends of the tile, so any variant meets any other."""
    a1 = hashed(seed, 1, salt) * 2.0 - 1.0
    a2 = hashed(seed, 2, salt) * 2.0 - 1.0
    t = math.pi * i / float(SIZE - 1)
    return amp * (0.65 * a1 * math.sin(t) + 0.35 * a2 * math.sin(2 * t))


def tuft(c, x, y, k):
    """A tuft of grass in a joint: two blades and a lit tip, the shadow on one side."""
    _set(c, x, y, LI[3])
    _set(c, x - 1, y - 1, LI[2])
    _set(c, x + 1, y - 2, LI[1] if k % 3 else VE[3])
    _set(c, x + 1, y - 1, LI[2])
    _set(c, x + 1, y, LI[4])


# --- the top of a wall --------------------------------------------------------------------------------------
#
# Slabs in two courses 24 px deep - big, so a top reads as a few great stones and not as brickwork, which
# is what a face is - the course lines at the same rows in every variant; within a course the joint is
# the variant's own, kept seven pixels off both side edges, so a slab cut by the edge is one slab with
# its neighbour.

SLAB_ROWS = (0, 24)
SLAB_H = 24


def cap(c, seed):
    """The top of a ruined wall from above: worn slabs in the stone row's base, each lit along its upper
    and left edges and falling into an `occ` joint on its lower and right, chipped at the corners, with
    grass coming up between them."""
    c.rect(0, 0, SIZE - 1, SIZE - 1, ST[2])
    for r, top in enumerate(SLAB_ROWS):
        bottom = top + SLAB_H - 1
        # The joint under the course, shared by every variant.
        for x in range(SIZE):
            _set(c, x, bottom, OCC if hashed(x // 3, r, 5200) < 0.8 else ST[4])
            _set(c, x, (top) % SIZE, ST[1] if hashed(x // 2, r, 5201) < 0.75 else ST[2])
        # One joint across the course, sometimes two, the variant's own.
        jx = 9 + int(hashed(seed, r, 5202) * 26)
        joints = [jx]
        if hashed(seed, r, 5203) < 0.35 and jx < 22:
            joints.append(jx + 14 + int(hashed(seed, r, 5206) * 8))
        for j in joints:
            if j > SIZE - 7:
                continue
            for y in range(top + 1, bottom):
                _set(c, j, y, OCC)
                _set(c, j + 1, y, ST[1])
                _set(c, j - 1, y, ST[3])
            # A chipped corner where the joint meets the course line.
            _set(c, j + 1, top + 1, OCC)
            _set(c, j - 1, bottom - 1, OCC)
            _set(c, j - 2, bottom - 1, ST[3])
        # Wear: pits and a crack in the stone, inside the tile.
        for k in range(2):
            px = 8 + int(hashed(seed, r * 3 + k, 5204) * 32)
            py = top + 5 + int(hashed(seed, r * 3 + k, 5205) * 14)
            _set(c, px, py, ST[3])
            _set(c, px + 1, py, OCC)
            _set(c, px, py - 1, ST[1])
        if hashed(seed, r, 5207) < 0.5:
            x, y = 10 + int(hashed(seed, r, 5208) * 24), top + 6
            for k in range(9):
                x += 1
                y += 1 if hashed(k, seed, 5209) < 0.4 else 0
                _set(c, x, y, ST[3])
    # Moss creeping over the slabs from the joints, in the ground's lichen at its darkest.
    for k in range(2):
        cx = 8 + int(hashed(k, seed, 5213) * 32)
        cy = SLAB_ROWS[k % 2] + SLAB_H - 2
        for y in range(cy - 3, cy + 2):
            for x in range(cx - 5, cx + 6):
                if (x - cx) ** 2 / 25.0 + (y - cy) ** 2 / 9.0 < 1.0 and hashed(x, y, 5214) < 0.7:
                    _set(c, x, y, LI[3] if hashed(x, y, 5215) < 0.5 else LI[4])
    # Grass in the joints, a few tufts, inside the tile.
    for k in range(4 + int(hashed(seed, 9, 5210) * 3)):
        x = 4 + int(hashed(k, seed, 5211) * 40)
        y = SLAB_ROWS[int(hashed(k, seed, 5212) * 2)] + SLAB_H - 1
        if 3 <= y < SIZE - 1:
            tuft(c, x, y, k)
            tuft(c, x + 2, y + 1, k + 1)


def rim_top(c, seed):
    """The north edge: the wall's far side dropping away, lit, broken into bites here and there."""
    for x in range(SIZE):
        w = 1.5 + wander(x, 5300, seed, 1.2)
        bite = hashed(x // 5, seed, 5301) < 0.18 and 4 < x < SIZE - 5
        depth = w + (2.5 if bite else 0.0)
        for y in range(0, int(depth + 2.5)):
            if y < depth - 0.5:
                _set(c, x, y, CLEAR)
            elif y < depth + 0.5:
                _set(c, x, y, ST[1])
            elif y < depth + 1.5:
                _set(c, x, y, ST[2])


def rim_left(c, seed):
    for y in range(SIZE):
        w = 1.5 + wander(y, 5310, seed, 1.2)
        for x in range(0, int(w + 2.5)):
            if x < w - 0.5:
                _set(c, x, y, CLEAR)
            elif x < w + 0.5:
                _set(c, x, y, ST[1])


def rim_right(c, seed):
    """The east edge faces away from the key: an edge in shadow."""
    for y in range(SIZE):
        w = 1.5 + wander(y, 5320, seed, 1.2)
        for k in range(0, int(w + 2.5)):
            x = SIZE - 1 - k
            if k < w - 0.5:
                _set(c, x, y, CLEAR)
            elif k < w + 1.5:
                _set(c, x, y, OCC if k < w + 0.5 else ST[3])


def chip_corner(c, corner, r):
    """A corner knocked off: transparent outside a rough arc, so the floor shows where the stone went."""
    cx = r if "l" in corner else SIZE - r
    cy = r if "t" in corner else SIZE - r
    for y in range(SIZE):
        for x in range(SIZE):
            inx = (x + 0.5 < cx) if "l" in corner else (x + 0.5 > cx)
            iny = (y + 0.5 < cy) if "t" in corner else (y + 0.5 > cy)
            if inx and iny and math.hypot(x + 0.5 - cx, y + 0.5 - cy) > r - (hashed(x, y, 5330) - 0.5) * 1.5:
                _set(c, x, y, CLEAR)


# --- the face ----------------------------------------------------------------------------------------------
#
# A coping on rows 0-5, three courses of ashlar 12 deep (6, 18, 30), the foot at 42-47. The rows are the
# same in every variant; only the joints move, kept seven pixels off both side edges.

COPING = 5
COURSES = (6, 18, 30)
COURSE_H = 12
FOOT = 42


def face(c, seed):
    """The front of a ruined wall: a broken coping catching the key, big chipped ashlar in the stone
    row's shade, darker toward the foot, and the line it stands on."""
    for y in range(SIZE):
        for x in range(SIZE):
            _set(c, x, y, ST[3])
    for ci, top in enumerate(COURSES):
        course(c, ci, top, seed)
    # The coping: a lit arris, the stone, a shadow under its lip - broken off along stretches, where the
    # course below shows to the top.
    for x in range(SIZE):
        broken = hashed(x // 6, seed, 5400) < 0.22 and 6 < x < SIZE - 7
        if broken:
            _set(c, x, 0, CLEAR)
            _set(c, x, 1, ST[2])
            _set(c, x, 2, ST[3])
            continue
        _set(c, x, 0, ST[1] if hashed(x // 2, 0, 5401) < 0.8 else ST[2])
        _set(c, x, 1, ST[2])
        _set(c, x, 2, ST[2])
        _set(c, x, 3, ST[3] if hashed(x // 3, 3, 5402) < 0.6 else ST[2])
        _set(c, x, 4, ST[4])
        _set(c, x, COPING, OCC)
    # Weathering down the lower course into the foot: the shade sinking to the deep, in 2 px dither.
    for y in range(COURSES[2] + 4, FOOT):
        for x in range(0, SIZE, 2):
            th = (BAYER[(y // 2) % 4][(x // 2) % 4] + 0.5) / 16.0
            if th < (y - COURSES[2] - 4) / 10.0 * 0.55:
                for dx in (0, 1):
                    if _get(c, x + dx, y) == ST[3]:
                        _set(c, x + dx, y, ST[4])
    for x in range(SIZE):
        _set(c, x, FOOT, ST[4])
        for y in range(FOOT + 1, SIZE):
            _set(c, x, y, OCC)
    # Grit fallen at the foot.
    for k in range(4):
        x = 5 + int(hashed(k, 1, 5410) * 38)
        _set(c, x, FOOT - 1, ST[2])
        _set(c, x + 1, FOOT - 1, ST[3])
        _set(c, x + 2, FOOT, OCC)


def course(c, ci, top, seed):
    first = 8 + int(hashed(ci, seed, 5420) * 12)
    js = [first, min(first + 15 + int(hashed(ci, seed, 5421) * 8), SIZE - 8)]
    mortar = top + COURSE_H - 1
    for y in range(top, mortar):
        k = y - top
        tone = ST[2] if k == 0 else (ST[4] if k == COURSE_H - 2 else ST[3])
        for x in range(SIZE):
            _set(c, x, y, tone)
    for x in range(SIZE):
        _set(c, x, mortar, OCC if hashed(x // 2, ci, 5422) < 0.85 else ST[4])
    for j in js:
        for y in range(top, mortar):
            _set(c, j, y, OCC)
            _set(c, j + 1, y, ST[2] if y - top < 3 else ST[3])
        # The corners of the blocks either side of the joint, knocked round.
        _set(c, j - 1, top, OCC)
        _set(c, j + 1, mortar - 1, OCC)
    # A chip or a crack on the block wholly inside the tile.
    x0, x1 = js[0] + 3, js[1] - 2
    r = hashed(ci, 1, 5430 + seed)
    if r < 0.35:
        px = x0 + int(hashed(ci, 2, 5431 + seed) * max(1, x1 - x0 - 4))
        for k in range(5):
            _set(c, px + k, top + 3 + (k * 3) // 4, OCC)
            _set(c, px + k, top + 2 + (k * 3) // 4, ST[2])
    elif r < 0.6:
        _set(c, x0, top, OCC)
        _set(c, x0 + 1, top, OCC)
        _set(c, x0, top + 1, OCC)


def face_end_left(c):
    for y in range(0, FOOT):
        _set(c, 0, y, ST[1] if y > 0 else ST[2])
        if _get(c, 1, y) in (ST[3], ST[4]):
            _set(c, 1, y, ST[2])


def face_end_right(c):
    for y in range(0, SIZE):
        _set(c, SIZE - 1, y, OCC)
        if _get(c, SIZE - 2, y) in (ST[1], ST[2], ST[3]):
            _set(c, SIZE - 2, y, ST[4])


# --- dressing on a face -----------------------------------------------------------------------------------

def leaf(c, x, y, k):
    """One ivy leaf: three pixels of the living row with a dark vein, lit on its upper-left."""
    _set(c, x, y, VE[3])
    _set(c, x - 1, y, VE[2] if k % 2 else VE[3])
    _set(c, x, y - 1, VE[2])
    _set(c, x + 1, y + 1, VE[4])


def dress_ivy(c, seed, x0=10, x1=38):
    """Ivy down the face from the top: stems that wander as they hang, leaves along them, thinning out
    before the foot."""
    for s in range(3):
        x = x0 + (x1 - x0) * (s + 0.5) / 3.0 + (hashed(s, seed, 5500) - 0.5) * 6
        length = 18 + int(hashed(s, seed, 5501) * 20)
        for y in range(0, min(FOOT - 2, length)):
            x += (hashed(y // 3, s, 5502) - 0.5) * 1.2
            ix = int(round(x))
            _set(c, ix, y, VE[4])
            if hashed(y, s, 5503) < 0.45:
                leaf(c, ix + (1 if y % 2 else -1), y, y)


def dress_ward(c, seed):
    """A ward cut into the middle course: a ring and three strokes through it, grooved into the stone
    - dark, because it is dead. A groove is `occ` with the lip below it lit."""
    cx, cy, r = 24, COURSES[1] + 5, 5
    for k in range(40):
        a = k * math.pi * 2 / 40
        x, y = int(round(cx + math.cos(a) * r)), int(round(cy + math.sin(a) * r * 0.9))
        _set(c, x, y, OCC)
        if math.sin(a) > 0.3:
            _set(c, x, y + 1, ST[2])
    for (dx0, dy0, dx1, dy1) in ((-7, -4, 7, 4), (-7, 4, 7, -4), (0, -6, 0, 6)):
        n = max(abs(dx1 - dx0), abs(dy1 - dy0))
        for k in range(n + 1):
            x = int(round(cx + dx0 + (dx1 - dx0) * k / float(n)))
            y = int(round(cy + dy0 + (dy1 - dy0) * k / float(n)))
            if COURSES[1] <= y < COURSES[1] + COURSE_H - 1:
                _set(c, x, y, OCC)
    # The crack it broke along.
    x, y = cx + 2, cy - 1
    for k in range(9):
        x += 1
        y += 1 if k % 2 else 0
        _set(c, x, y, OCC)


def dress_niche(c, seed):
    """A shallow arched niche across two courses, empty: whatever stood in it is gone."""
    x0, x1, y1 = 16, 31, COURSES[2] + 9
    mid = (x0 + x1) / 2.0
    half = (x1 - x0) / 2.0
    for x in range(x0 - 2, x1 + 3):
        dx = (x - mid) / (half + 2)
        if abs(dx) > 1.0:
            continue
        top = int(COURSES[1] + 1 + (1.0 - math.sqrt(1.0 - dx * dx)) * 6)
        _set(c, x, top, ST[1])
    for x in range(x0, x1 + 1):
        dx = (x - mid) / half
        top = int(COURSES[1] + 3 + (1.0 - math.sqrt(max(0.0, 1.0 - dx * dx))) * 6)
        for y in range(top, y1 + 1):
            _set(c, x, y, ST[4] if (x > mid + 3 and y > y1 - 6) else OCC)
    for x in range(x0 - 2, x1 + 3):
        _set(c, x, y1 + 1, ST[1])
        _set(c, x, y1 + 2, ST[3])


def dress_breach(c, seed):
    """A block gone from the middle course, the hole dark, cracks running out of it."""
    top = COURSES[1]
    x0, x1 = 14, 29
    for y in range(top, top + COURSE_H - 1):
        for x in range(x0, x1 + 1):
            _set(c, x, y, OCC if (x > x0 + 1 and y > top + 1) else ST[4])
    for x in range(x0 + 2, x1 + 1):
        _set(c, x, top + COURSE_H - 2, ST[3])
    for (sx, sy, dx, dy, n) in ((x1, top + 2, 1, -1, 7), (x0, top + COURSE_H - 2, -1, 1, 8), (x1 - 3, top + COURSE_H - 1, 1, 1, 9)):
        x, y = sx, sy
        for k in range(n):
            x += dx if k % 3 != 2 else 0
            y += dy if k % 3 != 1 else 0
            _set(c, x, y, OCC)


def dress_tumble(c, seed):
    """Blocks fallen from the top, lying at the foot against the face."""
    for (x, w, h) in ((8, 9, 6), (19, 7, 5), (31, 10, 7)):
        y0 = FOOT - h + 1
        for y in range(y0, FOOT + 1):
            for xx in range(x, x + w):
                tone = ST[2] if y == y0 else (ST[3] if xx > x else ST[2])
                _set(c, xx, y, tone)
        for xx in range(x, x + w):
            _set(c, xx, y0, ST[1])
        for y in range(y0, FOOT + 1):
            _set(c, x + w, y, OCC)
        _set(c, x + w // 2, y0 + 2, OCC)


def dress_moss(c, seed):
    """Moss in the lower joints and over the foot, where the rain runs off the stone."""
    for y in range(COURSES[2], FOOT + 1):
        for x in range(SIZE):
            here = _get(c, x, y)
            if here in (OCC, ST[4]) and hashed(x // 2, y // 2, 5600) < 0.4 + (y - COURSES[2]) / 30.0:
                _set(c, x, y, LI[4] if hashed(x, y, 5601) < 0.6 else LI[3])
    for k in range(4):
        tuft(c, 6 + int(hashed(k, seed, 5602) * 36), FOOT, k)


def dress_column(c, seed):
    """A pilaster carved in the face, its top broken off: flutes lit on their left."""
    x0, x1 = 18, 29
    top = COURSES[0] + 3
    for y in range(top, FOOT):
        for x in range(x0, x1 + 1):
            flute = (x - x0) % 3
            _set(c, x, y, ST[2] if flute == 0 else (ST[3] if flute == 1 else ST[4]))
        _set(c, x0 - 1, y, ST[1])
        _set(c, x1 + 1, y, OCC)
    # The broken top: a jagged line, and the base moulding.
    for x in range(x0 - 1, x1 + 2):
        bite = int(hashed(x, 1, 5610) * 4)
        for y in range(top - 1, top + bite):
            _set(c, x, y, OCC if y == top + bite - 1 else ST[3])
    for x in range(x0 - 2, x1 + 3):
        _set(c, x, FOOT - 3, ST[1])
        _set(c, x, FOOT - 2, ST[2])
        _set(c, x, FOOT - 1, OCC)


FACE_PAINTERS = {
    "ivy": lambda c, s: dress_ivy(c, s),
    "ward": dress_ward,
    "niche": dress_niche,
    "breach": dress_breach,
    "tumble": dress_tumble,
    "moss": dress_moss,
    "column": dress_column,
}


# --- dressing on a top ---------------------------------------------------------------------------------------

def dress_cap_grass(c, seed, x0=6, x1=42):
    for k in range(10):
        x = x0 + int(hashed(k, seed, 5700) * (x1 - x0))
        y = 6 + int(hashed(k, seed, 5701) * 36)
        tuft(c, x, y, k)


def dress_cap_fallen(c, seed):
    """A block lying on top of the wall where it fell from higher up."""
    x, y, w, h = 15, 18, 15, 9
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            _set(c, xx, yy, ST[1] if yy == y or xx == x else ST[2])
    for yy in range(y + 1, y + h + 1):
        _set(c, x + w, yy, OCC)
    for xx in range(x + 1, x + w + 1):
        _set(c, xx, y + h, OCC)


def dress_side(c, seed, dressing, west):
    if dressing == "grass":
        dress_cap_grass(c, seed, 4 if west else 30, 18 if west else 44)
    elif dressing == "ivy":
        # Ivy over the edge facing the floor, hanging down the side of the wall.
        x = 3 if west else SIZE - 4
        for y in range(4, SIZE - 4):
            if hashed(y // 2, seed, 5710) < 0.7:
                leaf(c, x + (1 if (y % 3 == 0) else 0), y, y)


# --- the tile ------------------------------------------------------------------------------------------------

def paint(c, role, dressing, plain_index, sides, inner):
    """Draw one ruined-wall tile. `sides` are the sides with floor beyond them (t, b, l, r); `inner` is
    the floored diagonal of an inner corner, as tiles.INNER gives it."""
    seed = seed_for(dressing, plain_index)
    if role == "patch":
        cap(c, seed)
        rim_top(c, seed)
        rim_left(c, seed)
        rim_right(c, seed)
        f = jw._Shift(c, 18)
        face(f, seed)
        face_end_left(f)
        face_end_right(f)
        chip_corner(c, "tl", 7)
        chip_corner(c, "tr", 7)
        return

    if "b" in sides:
        face(c, seed)
        if dressing in FACE_PAINTERS:
            FACE_PAINTERS[dressing](c, seed)
        if "l" in sides:
            face_end_left(c)
        if "r" in sides:
            face_end_right(c)
        return

    cap(c, seed)
    if role in SIDE_ROLES and dressing:
        dress_side(c, seed, dressing, role == "edge_left")
    elif dressing == "grass":
        dress_cap_grass(c, seed)
    elif dressing == "fallen":
        dress_cap_fallen(c, seed)
    if "t" in sides:
        rim_top(c, seed)
    if "l" in sides:
        rim_left(c, seed)
    if "r" in sides:
        rim_right(c, seed)
    if "t" in sides and "l" in sides:
        chip_corner(c, "tl", 7)
    if "t" in sides and "r" in sides:
        chip_corner(c, "tr", 7)
    if inner:
        v, h = inner
        if (v, h) == ("t", "l"):
            for k in range(3):
                _set(c, k, 0, ST[1])
                _set(c, 0, k, ST[1])
            _set(c, 0, 0, CLEAR)
        elif (v, h) == ("t", "r"):
            for k in range(3):
                _set(c, SIZE - 1, k, OCC)
                _set(c, SIZE - 2, k, OCC)
            _set(c, SIZE - 1, 0, CLEAR)
        elif (v, h) == ("b", "l"):
            for y in range(SIZE - 14, SIZE):
                _set(c, 0, y, ST[1])
        elif (v, h) == ("b", "r"):
            for y in range(SIZE - 14, SIZE):
                _set(c, SIZE - 1, y, OCC)
                _set(c, SIZE - 2, y, OCC)
