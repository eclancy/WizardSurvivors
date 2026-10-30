# -*- coding: utf-8 -*-
"""The Sunken Cave's rock: the `cave_wall` set. Drawn by tools/art/tiles.py (wall_set_modules) - run
tiles.py, never this. Piece V2 of .ai/endgame-castle.md (7.3): "walls as rounded rock masses".

The caverns topology (scripts/LevelGenerator.Caverns.cs) leaves rock between winding passages and
chambers, and its cells are the `wall` terrain like every wall in the game - the same collision, the
same navigation, the same flags. This set is only how they are DRAWN on the Cave
(StageEnvironmentProfile.WallSet = "cave_wall"). It has the jail set's shape (jail_walls.py): every
autotile role, the three stubs the spawn clearing leaves, plain variants and dressed ones carrying a
weight, and LevelTilePainter paints it exactly as it paints the jail.

THREE-QUARTER VIEW, SO SOUTH DECIDES, as in the jail. Floor to the south is the rock's FACE, standing
toward the viewer: a lip where the top turns down, catching the key light, then a cliff of fractured
slabs in shade - the key is upper-left, behind a face that looks south - with damp running down it,
grit and an `occ` contact line at its foot. Anything else is the TOP of the rock from above: big,
rounded lumps in the deep of the stone row, each lit on its upper-left and falling into an `occ`
crevice on its lower-right, with a lit rim where it turns down out of sight on the north and west and
a shadowed one on the east.

THE SAME THREE VALUES IN THE SAME ORDER: the floor (terrain_cave.py's `cave_floor`, on the stone row's
base) is the lightest, the face sits under it in the row's shade, and the top of the rock is the
darkest thing on screen - so a passage reads as a passage whatever is in it.

ROUNDED, NOT SQUARE. The caverns' rock has slanting edges, which the autotiler resolves to staircases of
outer and inner corners. Every outer corner here is cut round (transparent, so the floor under the wall
shows: every cell carries a ground tile, .ai/terrain.md section 1) and every rim wanders along its
length, pinned at the tile's corners so any variant meets any other - so a staircase reads as a lumpy
edge of rock, not a stair. The collision stays the full cell (LevelTilePainter.AddWallCollision): the
largest corner cut leaves at most seven pixels of it uncovered, on the diagonal.

NOTHING IN THE ROCK IS LIGHT. "The only light is the one you brought": no sconce, no glow, nothing warm
(a warm glint is a secret's tell, .ai/stage-secrets.md), nothing in an XP orb's colours. The crystals
in the rock are unlit - the deep of the `arcane` row with a steel facet catching the key - and are the
small cousins of the Geode Heart the cave hides. Nothing animates: a drip is drawn hanging.
"""
from __future__ import division

import math

import bonelight as bl
import jail_walls as jw
from jail_walls import BAYER, SIZE, _get, _set, hashed

OCC = bl.OCC
CLEAR = (0, 0, 0, 0)

TERRAIN = "cave_wall"

ST = jw.ST      # stone: hi lit base shade deep
AR = jw.AR      # arcane material (cold blue)
STL = jw.STL    # steel
SK = jw.SK      # bone
LN = jw.LN      # grave linen

SIDE_ROLES = ("edge_left", "edge_right")

# Dressing: incidents on the rock, never wallpaper (.ai/terrain.md section 6, "Dressing, and why it is
# sparse" - the painter rolls a dressed variant on about one face in four, never two side by side).
FACE_DRESSINGS = [
    ("crystals", 4),    # a cluster of unlit crystals jutting from the face
    ("seep", 3),        # water running down the slabs, a wet stain at the foot
    ("drips", 3),       # stalactites hanging from the lip
    ("vein", 2),        # a pale quartz vein across the face
    ("fungus", 2),      # pale cave fungus in the damp at the foot
    ("cracked", 2),     # a fallen slab: a hollow and rubble below it
    ("bones", 1),       # something that did not get out, in a hollow at the foot
    ("fossil", 1),      # a coiled shell in the stone, for the player who stops to look
]
SIDE_DRESSINGS = [("crystals", 2), ("rubble", 1)]
CAP_DRESSINGS = [("crystals", 1), ("rubble", 1)]

# The one-tile stubs the spawn clearing leaves (LevelGenerator.ClearSpawn): the jail's three.
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
    return 53 + plain_index * 13 + (names.index(dressing) * 7 + 5 if dressing in names else 0)


# --- the wander: an edge that is not a ruler line --------------------------------------------------

def wander(i, salt, seed, amp):
    """How far an edge wanders at pixel i along it: a sum of sin(pi n i / 47), zero at both ends of
    the tile, so any variant meets any other exactly at the corners (tiles.wander's trick)."""
    a1 = hashed(seed, 1, salt) * 2.0 - 1.0
    a2 = hashed(seed, 2, salt) * 2.0 - 1.0
    a3 = hashed(seed, 3, salt) * 2.0 - 1.0
    t = math.pi * i / float(SIZE - 1)
    return amp * (0.6 * a1 * math.sin(t) + 0.35 * a2 * math.sin(2 * t) + 0.25 * a3 * math.sin(3 * t))


# --- the top of the rock -----------------------------------------------------------------------------------

class _Wrap(object):
    """A canvas view that wraps round the tile, for the lumps every variant shares (tiles.Wrap)."""

    def __init__(self, c):
        self.c = c

    def set(self, x, y, col):
        self.c.set(int(x) % SIZE, int(y) % SIZE, col)

    def get(self, x, y):
        return self.c.get(int(x) % SIZE, int(y) % SIZE)


def dome(c, cx, cy, r):
    """One lump of rock seen from above, shaded as a dome under the key: its upper-left lit, its body in
    the deep of the stone row, its lower-right rim falling into `occ` - drawn over whatever is behind it,
    so lumps laid north to south pile up into a mass of rock rather than tiling into cobbles."""
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            nx = (x + 0.5 - cx) / r
            ny = (y + 0.5 - cy) / (r * 0.85)
            d2 = nx * nx + ny * ny
            if d2 > 1.0:
                continue
            # The key from the upper left, and a little from above.
            light = (-nx - ny) * 0.55 + math.sqrt(max(0.0, 1.0 - d2)) * 0.45
            th = (BAYER[y % 4][x % 4] + 0.5) / 16.0
            if d2 > 0.82 and light < 0.1:
                tone = OCC
            elif light > 0.55 + th * 0.25:
                tone = ST[3]
            elif light < -0.15 - th * 0.2:
                tone = OCC if d2 > 0.6 else ST[4]
            else:
                tone = ST[4]
            c.set(x, y, tone)


def cap(c, seed):
    """Rock from above: a mass of rounded lumps in the deep of the stone row - the darkest thing on screen,
    but rock and not a hole. The lumps every variant shares wrap round the tile (so a lump the edge cuts
    carries on in the neighbour, whatever variant it is); the variant's own sit inside, on top."""
    c.rect(0, 0, SIZE - 1, SIZE - 1, ST[4])
    shared = []
    for k in range(7):
        shared.append((hashed(k, 1, 4200) * SIZE, hashed(k, 2, 4201) * SIZE, 8 + hashed(k, 3, 4202) * 6))
    for (x, y, r) in sorted(shared, key=lambda p: p[1]):
        dome(_Wrap(c), x, y, r)
    own = []
    for k in range(2):
        r = 6 + hashed(k, seed, 4203) * 4
        own.append((r + 2 + hashed(k, seed, 4204) * (SIZE - 2 * r - 4),
                    r + 2 + hashed(k, seed, 4205) * (SIZE - 2 * r - 4), r))
    for (x, y, r) in sorted(own, key=lambda p: p[1]):
        dome(c, x, y, r)


def rim_top(c, seed, x0=0, x1=SIZE - 1):
    """The north edge of a top: the rock turning down out of sight, lit by the key, wandering - the line
    that says "the top edge of something" from across the screen, so the brightest stone in the rock."""
    for x in range(x0, x1 + 1):
        w = 2 + wander(x, 4300, seed, 1.5)
        for y in range(0, int(w + 3.5)):
            if y < w - 0.5:
                _set(c, x, y, CLEAR)
            elif y < w + 0.5:
                _set(c, x, y, ST[1] if hashed(x // 2, seed, 4301) < 0.7 else ST[2])
            elif y < w + 2.5:
                _set(c, x, y, ST[2] if y < w + 1.5 else ST[3])


def rim_left(c, seed, y0=0, y1=SIZE - 1):
    for y in range(y0, y1 + 1):
        w = 2 + wander(y, 4310, seed, 1.5)
        for x in range(0, int(w + 2.5)):
            if x < w - 0.5:
                _set(c, x, y, CLEAR)
            elif x < w + 0.5:
                _set(c, x, y, ST[1] if hashed(y // 2, seed, 4311) < 0.4 else ST[2])
            else:
                _set(c, x, y, ST[3] if _get(c, x, y) != OCC else ST[4])


def rim_right(c, seed, y0=0, y1=SIZE - 1):
    """The east edge faces away from the key: a shadowed edge, not a lit one."""
    for y in range(y0, y1 + 1):
        w = 2 + wander(y, 4320, seed, 1.5)
        for k in range(0, int(w + 2.5)):
            x = SIZE - 1 - k
            if k < w - 0.5:
                _set(c, x, y, CLEAR)
            elif k < w + 1.5:
                _set(c, x, y, OCC)


def round_corner(c, corner, r, rim):
    """Cut a corner of the tile round: transparent outside an arc of radius r, with a rim tone along
    it. `corner` is one of tl, tr, bl, br; `rim` None draws no rim."""
    cx = r if "l" in corner else SIZE - r
    cy = r if "t" in corner else SIZE - r
    for y in range(SIZE):
        for x in range(SIZE):
            inx = (x + 0.5 < cx) if "l" in corner else (x + 0.5 > cx)
            iny = (y + 0.5 < cy) if "t" in corner else (y + 0.5 > cy)
            if not (inx and iny):
                continue
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if d > r:
                _set(c, x, y, CLEAR)
            elif d > r - 1.2 and rim is not None and _get(c, x, y) is not None:
                _set(c, x, y, rim)


# --- the face ---------------------------------------------------------------------------------------------

LIP = 5           # rows of the lip, before its wander
FOOT = 44         # the contact line's row


def face(c, seed):
    """A cliff of rock standing toward the viewer, in shade: the lip catching the key on top, then rock
    split by wandering fractures, each with its east side lit where the key reaches into it, darkening
    toward a foot of fallen grit. Everything within seven pixels of a side edge is the same in every
    variant, so a face of mixed variants is one cliff; only the fractures are the variant's own."""
    # The body: the stone row's shade, sinking to its deep toward the foot through a 2 px dither, and
    # faint strata - rows every variant shares - so it is stone and not a painted band.
    for y in range(SIZE):
        sink = max(0.0, (y - 22) / 20.0)
        for x in range(SIZE):
            th = (BAYER[(y // 2) % 4][(x // 2) % 4] + 0.5) / 16.0
            _set(c, x, y, ST[4] if th < sink * 0.8 else ST[3])
    for (ly, salt) in ((17, 4410), (31, 4411)):
        for x in range(SIZE):
            h = hashed(x // 4, ly, salt)
            if h < 0.55:
                _set(c, x, ly + (1 if h < 0.15 else 0), ST[4])
            elif h > 0.85:
                _set(c, x, ly, ST[2])
    # Fractures: wandering cracks from the lip down, kept seven pixels off both edges. The rock east of a
    # crack is lit where the key reaches into it; west of it, a step darker.
    count = 2 + int(hashed(seed, 9, 4419) * 2)
    for j in range(count):
        x = 8.0 + (j + hashed(seed, j, 4420)) * (32.0 / count)
        start = LIP + 1 + int(hashed(seed, j, 4423) * 6)
        end = FOOT - int(hashed(seed, j, 4424) * 16)
        for y in range(start, end):
            x += (hashed(y // 2, j, 4422 + seed) - 0.5) * 1.3
            ix = max(7, min(SIZE - 8, int(round(x))))
            _set(c, ix, y, OCC)
            _set(c, ix + 1, y, ST[2] if (y - start) % 7 else ST[1])
            if _get(c, ix + 2, y) == ST[4]:
                _set(c, ix + 2, y, ST[3])
            _set(c, ix - 1, y, ST[4])
    # The lip: the top of the rock turning down, lit, its lower edge wandering, a shadow under it.
    for x in range(SIZE):
        h = LIP + wander(x, 4430, seed, 2.0)
        for y in range(0, int(h) + 2):
            if y < h - 1:
                _set(c, x, y, ST[1] if (y == 0 and hashed(x // 2, seed, 4431) < 0.45) else ST[2])
            elif y < h:
                _set(c, x, y, ST[2])
            elif y < h + 1:
                _set(c, x, y, OCC)
    # The foot: grit fallen from the face, and the contact line it all stands on.
    for x in range(SIZE):
        for y in range(FOOT, SIZE):
            _set(c, x, y, OCC)
    for k in range(7):
        x = 3 + int(hashed(k, 1, 4450) * 42)
        r = 1 + int(hashed(k, 2, 4451) * 2)
        for dy in range(r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r + 1:
                    _set(c, x + dx, FOOT - 1 - dy + r // 2, ST[3] if dx + dy < 0 else ST[4])
        _set(c, x - r, FOOT - 1 - r // 2, ST[2])
        _set(c, x + r + 1, FOOT, OCC)


def face_end_left(c, seed):
    """The west end of a face: the corner catches the key down its length, and the foot is round."""
    for y in range(0, FOOT):
        w = 1 + max(0.0, wander(y, 4460, seed, 1.0))
        for x in range(0, int(w + 1)):
            _set(c, x, y, ST[2] if x < w else ST[3])
    round_corner(c, "bl", 10, None)


def face_end_right(c, seed):
    for y in range(0, SIZE):
        _set(c, SIZE - 1, y, OCC)
        if _get(c, SIZE - 2, y) in (ST[1], ST[2], ST[3]):
            _set(c, SIZE - 2, y, ST[4])
    round_corner(c, "br", 10, None)


# --- dressing on a face ------------------------------------------------------------------------------------

def crystal(c, bx, by, height, lean, width=3):
    """One unlit crystal, standing out of the rock from (bx, by) up `height` px, leaning `lean` px
    over its height: a dark facet, a body and a steel facet catching the key on its left."""
    for k in range(height):
        t = k / float(max(1, height - 1))
        cx = bx + lean * t
        half = max(0.5, width * (1.0 - t * t) * 0.5 + 0.5)
        y = by - k
        x0, x1 = int(round(cx - half)), int(round(cx + half))
        for x in range(x0, x1 + 1):
            if x == x0:
                tone = STL[2] if k < height - 1 else STL[1]
            elif x == x1:
                tone = AR[4]
            else:
                tone = AR[3] if x > (x0 + x1) / 2.0 else AR[2]
            _set(c, x, y, tone)
    _set(c, int(round(bx + lean)), by - height, STL[1])
    # Its shadow on the rock, down and to the right.
    for k in range(height // 2):
        _set(c, bx + width // 2 + 2, by - k, OCC)


def dress_crystals(c, seed, base_y=34, x=22):
    for k, (dx, h, lean, w) in enumerate(((0, 13, -3, 4), (6, 9, 2, 3), (-6, 7, -4, 3), (3, 5, 1, 2))):
        crystal(c, x + dx, base_y - (k % 2), h, lean, w)
    # Where they break out of the stone.
    for xx in range(x - 9, x + 11):
        if hashed(xx, seed, 4500) < 0.6:
            _set(c, xx, base_y + 1, OCC)


def dress_seep(c, seed):
    cx = 25
    for y in range(LIP + 1, FOOT):
        w = 2 + int(hashed(y // 3, 1, 4510) * 3)
        for x in range(cx - w // 2, cx + w - w // 2):
            here = _get(c, x, y)
            if here in (ST[2], ST[3], ST[4], OCC):
                _set(c, x, y, AR[4] if (x + y) % 3 else AR[3])
        if y % 6 == 2:
            _set(c, cx - 1, y, STL[3])
    # The stain where it pools at the foot.
    for x in range(cx - 9, cx + 10):
        for y in range(FOOT - 3, FOOT):
            d = abs(x - cx) / 9.0 + (FOOT - 1 - y) / 3.0
            if d < 1.0 and hashed(x, y, 4511) < 0.85:
                _set(c, x, y, AR[4] if d > 0.35 else AR[3])


def dress_drips(c, seed):
    """Stalactites hanging from the lip, drawn still: rock tapering to a point, lit on its left."""
    for k, (x, h) in enumerate(((10, 9), (17, 5), (29, 12), (37, 6))):
        top = LIP + 1
        for j in range(h):
            half = max(0, (h - j) // 4)
            for xx in range(x - half, x + half + 1):
                _set(c, xx, top + j, ST[2] if xx < x else ST[3])
            _set(c, x + half + 1, top + j, OCC)
        _set(c, x, top + h, ST[2])
        # A bead of water at the tip, and a dark spot below where the drops land.
        _set(c, x, top + h + 1, AR[3])
        if h > 8:
            _set(c, x, FOOT - 2, AR[4])
            _set(c, x + 1, FOOT - 2, AR[4])


def dress_vein(c, seed):
    """A seam of pale quartz across the slabs, in the bone row's middle stops."""
    x, y = 4.0, 30.0
    for k in range(48):
        x += 0.9
        y -= 0.45 + (hashed(k // 4, 1, 4520) - 0.5) * 0.8
        ix, iy = int(round(x)), int(round(y))
        if not (LIP + 3 <= iy < FOOT - 2):
            continue
        _set(c, ix, iy, SK[3])
        _set(c, ix, iy + 1, SK[4] if k % 3 else SK[3])
        if hashed(k, 2, 4521) < 0.25:
            _set(c, ix, iy - 1, SK[2])


def dress_fungus(c, seed):
    """Pale fungus where the damp meets the foot: small caps on short stems, the grave-linen row."""
    for k, (x, h, r) in enumerate(((12, 4, 2), (16, 6, 3), (21, 3, 2), (31, 5, 2), (35, 3, 1))):
        foot = FOOT - 1
        for j in range(h):
            _set(c, x, foot - j, LN[2])
            _set(c, x + 1, foot - j, LN[3])
        top = foot - h
        for dx in range(-r, r + 2):
            _set(c, x + dx, top, LN[1] if dx <= 0 else LN[2])
            _set(c, x + dx, top + 1, LN[3])
        _set(c, x + r + 2, top + 1, OCC)


def dress_cracked(c, seed):
    """A slab fallen out of the face: the hollow behind it dark, its sill lit, rubble below."""
    x0, x1, y0, y1 = 15, 31, 17, 30
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            # A rough oval hollow.
            d = ((x - (x0 + x1) / 2.0) / ((x1 - x0) / 2.0)) ** 2 + ((y - (y0 + y1) / 2.0) / ((y1 - y0) / 2.0)) ** 2
            if d < 1.0:
                _set(c, x, y, OCC if (d < 0.75 or y > y0 + 2) else ST[4])
    for x in range(x0 + 2, x1 - 1):
        _set(c, x, y1 - 1, ST[3])
        _set(c, x, y1, ST[2])
    for (x, r) in ((12, 2), (19, 1), (24, 2), (30, 1), (34, 2)):
        _set(c, x, FOOT - 1, ST[2])
        _set(c, x + 1, FOOT - 1, ST[3])
        if r > 1:
            _set(c, x, FOOT - 2, ST[1])
            _set(c, x + 1, FOOT - 2, ST[2])
            _set(c, x + 2, FOOT - 1, OCC)


def dress_bones(c, seed):
    """A low hollow at the foot of the rock with bones in it: something that did not get out."""
    for y in range(FOOT - 13, FOOT):
        for x in range(9, 40):
            d = ((x - 24) / 15.0) ** 2 + ((y - FOOT) / 13.0) ** 2
            if d < 1.0:
                _set(c, x, y, OCC if d < 0.82 else ST[4])
    jw.bone(c, 12, FOOT - 3, 22, FOOT - 5)
    jw.bone(c, 27, FOOT - 6, 37, FOOT - 3)
    jw.skull(c, 18, FOOT - 13, dim=True)


def dress_fossil(c, seed):
    """A coiled shell in the stone: a spiral of pale lines, quiet, on one slab."""
    cx, cy = 24.0, 24.0
    prev = None
    for k in range(70):
        a = k * 0.32
        r = 0.6 + k * 0.11
        x, y = int(round(cx + math.cos(a) * r)), int(round(cy + math.sin(a) * r * 0.9))
        if (x, y) != prev:
            _set(c, x, y, SK[3] if k % 5 else SK[2])
            prev = (x, y)
    # Its ribs, every so often across the coil.
    for k in range(18, 70, 6):
        a = k * 0.32
        r = 0.6 + k * 0.11
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r * 0.9
        _set(c, int(round(x - math.cos(a))), int(round(y - math.sin(a))), SK[4])


FACE_PAINTERS = {
    "crystals": lambda c, s: dress_crystals(c, s),
    "seep": dress_seep,
    "drips": dress_drips,
    "vein": dress_vein,
    "fungus": dress_fungus,
    "cracked": dress_cracked,
    "bones": dress_bones,
    "fossil": dress_fossil,
}


# --- dressing on a top ---------------------------------------------------------------------------------------

def dress_cap_crystals(c, seed, x=24, y=30):
    for (dx, h, lean, w) in ((0, 9, -2, 3), (5, 6, 2, 2), (-4, 5, -2, 2)):
        crystal(c, x + dx, y, h, lean, w)


def dress_cap_rubble(c, seed):
    for k in range(6):
        px = 9 + int(hashed(k, seed, 4600) * 30)
        py = 10 + int(hashed(k, seed, 4601) * 28)
        _set(c, px, py, ST[3])
        _set(c, px + 1, py, ST[4])
        _set(c, px, py - 1, ST[2] if k % 2 else ST[3])
        _set(c, px + 1, py + 1, OCC)


# --- the tile -------------------------------------------------------------------------------------------------

class _Shift(jw._Shift):
    """The jail's view squeezed into the rows below `oy`, for a stub's short face."""
    pass


def paint(c, role, dressing, plain_index, sides, inner):
    """Draw one rock tile. `sides` are the sides with floor beyond them (t, b, l, r); `inner` is the
    floored diagonal of an inner corner, as tiles.INNER gives it."""
    seed = seed_for(dressing, plain_index)
    if role == "patch":
        # A lone stub (only ever left by the spawn clearing): a top above, a short face below, round.
        cap(c, seed)
        rim_top(c, seed)
        rim_left(c, seed)
        rim_right(c, seed)
        f = _Shift(c, 18)
        face(f, seed)
        face_end_left(f, seed)
        face_end_right(f, seed)
        round_corner(c, "tl", 16, ST[2])
        round_corner(c, "tr", 16, OCC)
        return

    if "b" in sides:
        face(c, seed)
        if dressing in FACE_PAINTERS:
            FACE_PAINTERS[dressing](c, seed)
        if "l" in sides:
            face_end_left(c, seed)
        if "r" in sides:
            face_end_right(c, seed)
        return

    cap(c, seed)
    if dressing == "crystals":
        if role in SIDE_ROLES:
            dress_cap_crystals(c, seed, x=12 if role == "edge_left" else 36, y=32)
        else:
            dress_cap_crystals(c, seed)
    elif dressing == "rubble":
        dress_cap_rubble(c, seed)
    if "t" in sides:
        rim_top(c, seed)
    if "l" in sides:
        rim_left(c, seed)
    if "r" in sides:
        rim_right(c, seed)
    # Outer corners round: lit where the key falls on them (the north-west), dark elsewhere.
    if "t" in sides and "l" in sides:
        round_corner(c, "tl", 16, ST[2])
    if "t" in sides and "r" in sides:
        round_corner(c, "tr", 16, OCC)
    if inner:
        v, h = inner
        if (v, h) == ("t", "l"):
            # The rims of the two neighbours meeting at this corner, lit.
            for k in range(3):
                _set(c, k, 0, ST[2])
                _set(c, 0, k, ST[2])
            _set(c, 0, 0, CLEAR)
        elif (v, h) == ("t", "r"):
            for k in range(3):
                _set(c, SIZE - 1, k, OCC)
                _set(c, SIZE - 2, k, OCC)
            _set(c, SIZE - 1, 0, CLEAR)
        elif (v, h) == ("b", "l"):
            # The rock's west edge running down past the face beside it.
            rim_left(c, seed, SIZE - 14, SIZE - 1)
        elif (v, h) == ("b", "r"):
            rim_right(c, seed, SIZE - 14, SIZE - 1)
