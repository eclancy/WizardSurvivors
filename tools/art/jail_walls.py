# -*- coding: utf-8 -*-
"""The Cursed Dungeon's walls: a jail. Drawn by tools/art/tiles.py as the `wall` terrain.

Reported after a playtest of the maze chapter: "the black walls in that level should be a nicely
detailed stone jail walls. add skulls, sconces, and a bunch of details to make it look like a
dungeon jail." The walls were `floor_void_0`, one flat `occ` tile (.ai/terrain.md section 6),
and the dungeon read as black slabs on a stone floor.

HOW A WALL TILE KNOWS WHAT IT IS. The maze's walls are two tiles thick (LevelGenerator: six-tile
corridors, two-tile walls), so every wall tile touches floor on at least one side or corner, and
the autotiler's role for the cell says which (TerrainAutotiler.RoleFor - the same fourteen roles
every other terrain uses, computed with the chunk's margin, so a role is seam-free). The view is
three-quarter, like every actor, so the side that matters is SOUTH:

  floor to the south      the FACE: the wall's front, standing toward the viewer. A coping with a
                          lit arris, three courses of cut stone with `occ` mortar, and an `occ`
                          contact line at the foot. All the dressing lives here - sconces, skulls
                          in niches, barred cell windows, chains and shackles, bones piled at the
                          foot, a drain grate, a seep.
  anything else           the CAP: the top of the wall seen from above. Rough cobbles in the deep
                          end of the stone row, darker than any floor, with a lit bevel on the
                          sides that face the key light (north, west) and an `occ` edge on the
                          side that faces away (east).

So a horizontal wall is a cap row over a face row, and a vertical one is two columns of cap with a
face at a free south end. Three values, in one order everywhere: the floor is the lightest stone,
the face sits a step under it with lit edges, and the top of a wall is the darkest thing on screen
- which is what it was when it was black, so a corridor is as obvious as it ever was.

THE LIGHT IN HIS JAIL IS HIS. `.ai/world-and-tone.md`: the player carries the only warm light, and
his creatures' eyes are the one cold `arcane` mark of the will that made them. So a lit sconce here
burns that same cold light - the `arcane` material row round a white core - and says whose jail
this is; the rest are guttered, cold iron and soot. Nothing on a wall is warm, so nothing on a wall
can be mistaken for the warm glint a secret will breathe (.ai/stage-secrets.md), for a flame vent
(fire is a hazard) or for the player. Nothing is in an XP orb's colours (the grass and light
emissives, the arcane ELEMENT's lilac), nothing animates (decor never does), and nothing is drawn
as a way through: a cell window is high in the wall and barred, a grate is small and barred, and
no opening shows floor behind it.

THE SAME RULES AS THE GROUND. Every colour is from the contract (tiles.py audits each tile).
Features that a different neighbour would cut stay inside the tile; what crosses an edge - the
courses, the coping, the cobbles of the cap - is at the same place in every variant, so a face of
six different variants side by side is one wall.
"""

import math

import bonelight as bl

OCC = bl.OCC
SIZE = 48


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


ST = ramp("stone")
LI = ramp("lichen")
LN = ramp("linen")
SK = ramp("skin")
FL = ramp("flesh")
STL = ramp("steel")
AR = ramp("arcane")

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def hashed(x, y, salt=0):
    """A stable value in [0,1). Callers fold the variant into the salt when they want it to vary."""
    h = (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)
    h &= 0x7FFFFFFF
    return ((h >> 7) % 10007) / 10007.0


# --- the dressing each role may carry --------------------------------------------------------------
#
# LevelTilePainter paints a PLAIN variant of a role unless the cell rolls a dressed one, and never
# two dressed cells side by side (.ai/terrain.md section 6): the plain tiles are the wall and the
# dressed ones are incidents on it, not wallpaper. The weight is how often a dressing is picked
# among a role's dressed variants - lit sconces most, because they were asked for by name and they
# are what makes a long corridor read as a place someone keeps.

SIDE_ROLES = ("edge_left", "edge_right")

FACE_DRESSINGS = [
    ("sconce", 5),        # lit with his cold fire, the light pooled on the stone round it
    ("sconce_dead", 2),   # guttered: cold iron, a burnt stub, soot up the wall
    ("skull_niche", 3),   # an arched niche with a skull set in it
    ("window", 3),        # a barred cell window, high in the wall
    ("chains", 2),        # two rings, a swag of chain between them, a shackle hanging
    ("manacles", 2),      # a pair of manacles on short chains, at hand height
    ("bones", 2),         # bones and a skull piled at the foot
    ("skull_spike", 2),   # a skull on an iron spike
    ("seep", 1),          # water down the stones, moss at the foot
    ("broken", 1),        # a block fallen out, cracks, rubble at the foot
    ("tally", 1),         # a prisoner's count scratched into the stone
    ("grate", 1),         # a barred drain at the foot
]
# The vertical walls show only their tops, so they get what can hang off a top's edge.
SIDE_DRESSINGS = [("sconce", 3), ("sconce_dead", 1), ("chains", 1)]
CAP_DRESSINGS = [("moss", 1), ("rubble", 1)]

# The spawn clearing (LevelGenerator.ClearSpawn) cuts the two-tile walls near the start down to one
# tile, and the autotiler calls every such cell `patch`. LevelTilePainter.WallRoleKey reads the cell's
# open sides and asks for one of these instead, so a stub is a one-tile wall, not a pile of pillars.
STUB_SIDES = {
    "patch_v": ("l", "r"),          # a one-tile wall running north-south: a top, both edges
    "patch_cap": ("t", "l", "r"),   # its north end
    "patch_face": ("b", "l", "r"),  # its south end, or a one-tile wall end-on: a face, both ends
}

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


def _set(c, x, y, col):
    if col is not None and 0 <= x < SIZE and 0 <= y < SIZE:
        c.set(int(x), int(y), col)


def _get(c, x, y):
    if not (0 <= x < SIZE and 0 <= y < SIZE):
        return None
    p = c.get(int(x), int(y))
    return "#%02X%02X%02X" % (p[0], p[1], p[2]) if p[3] else None


STONE_TONES = set(ST)


# --- the face -----------------------------------------------------------------------------------------

# Rows, fixed for every variant so a face is continuous whatever stands beside it: the coping on
# 0-4, three courses of 13, the foot on 44-47. Only the joints move.
COPING_SHADOW = 4
COURSE_TOPS = (5, 18, 31)
COURSE_H = 13
FOOT = 44


def face(c, seed):
    # The coping: a lit arris where the top of the wall turns down into its face, the cap stone,
    # and the shadow under its lip. The arris is the line that says "the top edge of something
    # tall" from across the screen, so it is the brightest stone in the wall.
    for x in range(SIZE):
        _set(c, x, 0, ST[1])
        _set(c, x, 1, ST[1] if hashed(x // 3, 1, 3099) < 0.3 else ST[2])
        _set(c, x, 2, ST[2])
        _set(c, x, 3, ST[3])
        _set(c, x, COPING_SHADOW, OCC)
    # A joint in the coping at a place every variant shares (variant 0's hash).
    jx = 10 + int(hashed(1, 2, 3100) * 26)
    for y in range(1, 4):
        _set(c, jx, y, OCC)
    _set(c, jx + 1, 1, ST[1])
    for k in range(3):
        if hashed(k, seed, 3101) < 0.6:
            _set(c, 2 + int(hashed(k, seed, 3102) * (SIZE - 4)), 0, ST[0])

    for ci, top in enumerate(COURSE_TOPS):
        course(c, ci, top, seed)

    # The foot: grime, then the contact line the wall stands on.
    for x in range(SIZE):
        _set(c, x, FOOT, ST[4])
        for y in range(FOOT + 1, SIZE):
            _set(c, x, y, OCC)
    # Damp creeping up from the floor into the bottom course, in the grave-linen row, dithered in
    # 2x2 blocks so it reads as a stain rather than as noise.
    low = COURSE_TOPS[2]
    for y in range(low + 5, FOOT):
        for x in range(0, SIZE, 2):
            if hashed(x // 2, y // 2, 3110 + seed) < (y - low - 4) / 8.0 * 0.6:
                for dx in (0, 1):
                    if _get(c, x + dx, y) == ST[3]:
                        _set(c, x + dx, y, LN[4])
    for k in range(4):
        x = int(hashed(k, seed, 3120) * (SIZE - 6)) + 3
        if hashed(k, seed, 3121) < 0.55:
            _set(c, x, FOOT, LI[3])
            _set(c, x + 1, FOOT, LI[4])
            _set(c, x, FOOT - 1, LI[4])


def joints_for(course_index, seed):
    """Two vertical joints per course, staggered course to course like a bond, kept 7 px off both
    tile edges so a block cut by the edge is never a sliver whatever the neighbour's joints are."""
    if course_index % 2:
        first = 13 + int(hashed(course_index, seed, 3131) * 9)
    else:
        first = 7 + int(hashed(course_index, seed, 3130) * 9)
    second = first + 17 + int(hashed(course_index, seed, 3132) * 7)
    return [first, min(second, SIZE - 8)]


def course(c, ci, top, seed):
    js = joints_for(ci, seed)
    mortar = top + COURSE_H - 1
    for y in range(top, mortar):
        k = y - top
        tone = ST[1] if k == 0 else (ST[2] if k == 1 else (ST[4] if k == COURSE_H - 2 else ST[3]))
        for x in range(SIZE):
            _set(c, x, y, tone)
    for x in range(SIZE):
        _set(c, x, mortar, OCC)
    for j in js:
        for y in range(top, mortar):
            _set(c, j, y, OCC)
            # light from the left: the block east of a joint is lit on its edge, the one west of it
            # is in its own shade
            _set(c, j + 1, y, ST[1] if y - top < 2 else ST[2])
            if y > top:
                _set(c, j - 1, y, ST[4])
    # Wear, only on the block wholly inside the tile (a block the edge cuts is the same on both
    # sides of it): a chipped corner, a pit, a scar.
    x0, x1 = js[0] + 3, js[1] - 2
    r = hashed(ci, 1, 3140 + seed)
    if r < 0.3:
        _set(c, x0 - 1, top, OCC)
        _set(c, x0, top, OCC)
        _set(c, x0 - 1, top + 1, OCC)
        _set(c, x0, top + 1, ST[4])
    elif r < 0.55:
        px = x0 + 2 + int(hashed(ci, 2, 3141 + seed) * max(1, x1 - x0 - 5))
        py = top + 4 + int(hashed(ci, 3, 3142 + seed) * 4)
        _set(c, px, py, OCC)
        _set(c, px + 1, py, OCC)
        _set(c, px, py - 1, ST[2])
        _set(c, px + 1, py + 1, ST[4])
    elif r < 0.75:
        px = x0 + 1 + int(hashed(ci, 4, 3143 + seed) * max(1, x1 - x0 - 7))
        for k in range(4):
            _set(c, px + k, top + 4 + k // 2, OCC)
            _set(c, px + k, top + 3 + k // 2, ST[2])


# --- the cap ------------------------------------------------------------------------------------------

# Cobbles: a 3 x 3 jittered lattice of stones, 16 px apart, periodic on the tile - so a cobble the
# edge cuts continues in the neighbour, every variant sharing the lattice. What varies by variant is
# the middle stone's shading and the incidents laid on top.
def _cap_points(seed):
    """The cobbles' centres. The eight round the edge are the same in every variant, which is what
    lets a cobble the tile edge cuts carry on in the neighbour. The middle one is the variant's own -
    moved, and on some variants split in two - and that is safe because the middle stone never
    reaches an edge (its centre is 20 px or more from every edge pixel, the edge stones' 13 or less)."""
    pts = []
    for j in range(3):
        for i in range(3):
            if i == 1 and j == 1:
                jx = (hashed(seed, 1, 3203) - 0.5) * 10
                jy = (hashed(seed, 2, 3204) - 0.5) * 10
            else:
                jx = (hashed(i, j, 3200) - 0.5) * 9
                jy = (hashed(i, j, 3201) - 0.5) * 9
            pts.append((8 + 16 * i + jx, 8 + 16 * j + jy))
    if hashed(seed, 3, 3206) < 0.5:
        mx, my = pts[4]
        ang = hashed(seed, 4, 3207) * math.pi
        pts[4] = (mx - math.cos(ang) * 4, my - math.sin(ang) * 4)
        pts.append((mx + math.cos(ang) * 4, my + math.sin(ang) * 4))
    return pts


def cap(c, seed):
    """The top of the wall: rough cobbles in the deep end of the stone row.

    The darkest stone in the game on purpose - this is the wall's mass, and the darkest thing on
    screen should still be the top of a wall - but stone and not a hole: cobbles parted by `occ`,
    each lit on its upper-left like every other stone, and calm, because a wall top covers a third
    of the screen and must never be the subject.
    """
    pts = _cap_points(seed)
    for y in range(SIZE):
        for x in range(SIZE):
            best, second, bi = 1e9, 1e9, 0
            for i, (px, py) in enumerate(pts):
                for ox in (-SIZE, 0, SIZE):
                    for oy in (-SIZE, 0, SIZE):
                        d = math.hypot(x + 0.5 - (px + ox), y + 0.5 - (py + oy))
                        if d < best:
                            best, second, bi = d, best, i
                            bx, by = px + ox, py + oy
                        elif d < second:
                            second = d
            gap = second - best
            if gap < 1.6:
                tone = OCC
            else:
                rel = (x + 0.5 - bx) + (y + 0.5 - by)
                tone = ST[4]
                if gap < 4.5 and rel < -2:
                    tone = ST[3]                 # the lit upper-left of a cobble
                elif gap < 3.0 and rel > 3:
                    tone = OCC                   # its lower-right falls into the joint
                if bi == 4 and hashed(seed, 0, 3205) < 0.4 and gap > 5 and rel < -4:
                    tone = ST[2]                 # the middle stone, worn smoother on some variants
            _set(c, x, y, tone)
    for k in range(3):
        x = 10 + int(hashed(k, seed, 3210) * 28)
        y = 10 + int(hashed(k, seed, 3211) * 28)
        if _get(c, x, y) == ST[4]:
            _set(c, x, y, OCC)


# --- rims: the edges of a top that face the floor ------------------------------------------------------

def rim_top(c):
    # A lit bevel: the wall's top edge turning down out of sight on the far side.
    for x in range(SIZE):
        _set(c, x, 0, ST[1])
        _set(c, x, 1, ST[2])
        _set(c, x, 2, ST[3] if _get(c, x, 2) != ST[2] else ST[2])
        if hashed(x // 2, 0, 3220) < 0.12:
            _set(c, x, 0, ST[0])


def rim_left(c, y0=0, y1=SIZE - 1):
    for y in range(y0, y1 + 1):
        _set(c, 0, y, ST[1])
        _set(c, 1, y, ST[2])
        _set(c, 2, y, ST[3] if _get(c, 2, y) != ST[2] else ST[2])


def rim_right(c, y0=0, y1=SIZE - 1):
    # The east side faces away from the key light: an edge in shadow, not a lit one.
    for y in range(y0, y1 + 1):
        _set(c, SIZE - 1, y, OCC)
        _set(c, SIZE - 2, y, OCC)
        if _get(c, SIZE - 3, y) in (ST[2], ST[3]):
            _set(c, SIZE - 3, y, ST[4])


def face_end_left(c):
    # The west end of a face: its corner catches the key light down to the foot.
    for y in range(0, FOOT):
        _set(c, 0, y, ST[1])
        if _get(c, 1, y) in (ST[3], ST[4], LN[4]):
            _set(c, 1, y, ST[2])


def face_end_right(c):
    for y in range(0, SIZE):
        _set(c, SIZE - 1, y, OCC)
        if _get(c, SIZE - 2, y) in (ST[1], ST[2], ST[3]):
            _set(c, SIZE - 2, y, ST[4])


# --- dressing on a face ----------------------------------------------------------------------------------

def light_pool(c, cx, cy, r):
    """His cold light falling on the stone round a flame: the stone's tones shifted into the `arcane`
    row, dithered by distance so the pool has no edge. Only stone is touched - the flame, the iron
    and the bone drawn after it are not."""
    near = {ST[4]: AR[4], ST[3]: AR[3], ST[2]: AR[2], ST[1]: AR[2], ST[0]: AR[1], OCC: ST[4], LN[4]: AR[4]}
    far = {ST[4]: AR[4], ST[3]: AR[4], ST[2]: AR[3], ST[1]: AR[3], ST[0]: AR[2]}
    for y in range(int(cy - r), int(cy + r) + 1):
        for x in range(int(cx - r), int(cx + r) + 1):
            d = math.hypot(x - cx, (y - cy) * 0.9) / float(r)
            if d >= 1.0:
                continue
            here = _get(c, x, y)
            if here is None:
                continue
            th = (BAYER[y % 4][x % 4] + 0.5) / 16.0
            if d < 0.6 and th < 1.05 - d * 1.1:
                if here in near:
                    _set(c, x, y, near[here])
            elif th < (1.0 - d) * 1.1:
                if here in far:
                    _set(c, x, y, far[here])


def iron_plate(c, x, y, w, h):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            _set(c, xx, yy, STL[4])
    for xx in range(x, x + w):
        _set(c, xx, y, STL[3])
    for yy in range(y, y + h):
        _set(c, x, yy, STL[3])
    _set(c, x, y, STL[2])
    _set(c, x + w // 2, y + h // 2, STL[2])       # the rivet
    for xx in range(x + 1, x + w + 1):
        _set(c, xx, y + h, OCC)


def sconce_body(c, cx, cup_y):
    """The iron, from the front: a wall plate, a stem up out of it and a cup."""
    iron_plate(c, cx - 3, cup_y + 6, 7, 7)
    for y in range(cup_y + 3, cup_y + 7):
        _set(c, cx, y, STL[2])
        _set(c, cx + 1, y, STL[3])
        _set(c, cx + 2, y, OCC)
    # the cup: a bowl wider at the rim, lit on its left, its rim catching the light
    for k, half in enumerate((5, 5, 4, 3)):
        y = cup_y + k
        for x in range(cx - half, cx + half + 2):
            _set(c, x, y, STL[3])
        _set(c, cx - half, y, STL[2])
        _set(c, cx + half + 1, y, STL[4])
    for x in range(cx - 5, cx + 7):
        _set(c, x, cup_y - 1, STL[2])
    _set(c, cx - 5, cup_y - 1, STL[1])
    # its shadow on the wall, down and to the right of the iron
    for y in range(cup_y + 1, cup_y + 5):
        _set(c, cx + 7, y, OCC)
    for y in range(cup_y + 7, cup_y + 14):
        _set(c, cx + 5, y, OCC)


def cold_flame(c, cx, base_y):
    """His fire: the arcane row's light round a white core. A teardrop 9 x 15, its tip leaning."""
    rows = [(0, -1), (0, -1), (1, -1), (1, -1), (1, 0), (2, 0), (2, 0), (3, 1), (3, 1),
            (4, 1), (4, 2), (4, 2), (4, 1), (3, 1), (2, -1)]
    tip = base_y - len(rows) + 1
    for k, (half, core) in enumerate(rows):
        y = tip + k
        lean = 2 if k < 3 else (1 if k < 6 else 0)
        for x in range(cx - half + lean, cx + half + lean + 2):
            edge = x in (cx - half + lean, cx + half + lean + 1)
            _set(c, x, y, AR[2] if edge else AR[1])
        if core >= 0:
            for x in range(cx - core + lean, cx + core + lean + 2):
                _set(c, x, y, AR[0])


def dress_sconce(c, lit, cx=23, cup_y=22):
    if lit:
        light_pool(c, cx + 1, cup_y - 6, 22)
    else:
        # Soot up the stone above a dead sconce: a widening smear of deep and occ.
        for y in range(COPING_SHADOW + 1, cup_y - 1):
            spread = 1 + (cup_y - y) // 3
            for x in range(cx - spread + 1, cx + spread + 1):
                th = (BAYER[y % 4][x % 4] + 0.5) / 16.0
                if th < 0.6 and _get(c, x, y) in STONE_TONES:
                    _set(c, x, y, ST[4] if th > 0.25 else OCC)
    sconce_body(c, cx, cup_y)
    if lit:
        cold_flame(c, cx, cup_y - 2)
    else:
        for x in range(cx - 2, cx + 3):
            _set(c, x, cup_y - 2, FL[4])
        for x in range(cx - 1, cx + 2):
            _set(c, x, cup_y - 3, FL[4])
        _set(c, cx, cup_y - 4, OCC)
        _set(c, cx - 3, cup_y - 2, LN[3])
        _set(c, cx + 3, cup_y - 2, LN[3])


SKULL = [
    "...XXXXX...",
    ".XXXXXXXXX.",
    "XXXXXXXXXXX",
    "XXXXXXXXXXX",
    "XXXXXXXXXXX",
    "XEEEXXXEEEX",
    "XEEEXXXEEEX",
    ".XEXXNXXEX.",
    "..XXXNXXX..",
    "..XTXTXTX..",
    "...TTTTT...",
]


def skull(c, x, y, dim=False):
    """A skull from the front, 11 x 11, top-left at (x, y): bone row, never its brightest stop, so
    it is a thing on a wall and not a light."""
    body, lit, dark = (SK[3], SK[2], SK[4]) if dim else (SK[2], SK[1], SK[3])
    for j, row in enumerate(SKULL):
        for i, ch in enumerate(row):
            if ch == ".":
                continue
            px, py = x + i, y + j
            if ch in ("E", "N"):
                _set(c, px, py, OCC)
            elif ch == "T":
                _set(c, px, py, dark if (i + j) % 2 else body)
            else:
                tone = body
                if j <= 3 and 1 <= i <= 5:
                    tone = lit
                if i >= 9 or (j >= 7 and i >= 6):
                    tone = dark
                _set(c, px, py, tone)
    for i in range(2, 10):
        _set(c, x + i, y + 11, OCC)


def dress_skull_niche(c):
    x0, x1, y1 = 14, 33, 34                # the opening; its floor is the sill at y1 + 1
    mid = (x0 + x1) / 2.0
    half = (x1 - x0) / 2.0
    # the voussoirs round the arch, lit on their upper-left
    for x in range(x0 - 3, x1 + 4):
        dx = (x - mid) / (half + 3)
        if abs(dx) > 1.0:
            continue
        outer = 15 - math.sqrt(1.0 - dx * dx) * 8
        for y in range(int(outer), y1 + 1):
            _set(c, x, y, ST[2] if x < mid else ST[3])
        _set(c, x, int(outer), ST[1])
    for k in range(-3, 4, 2):
        ax = int(mid + k * half / 3.5)
        dx = (ax - mid) / (half + 3)
        oy = int(15 - math.sqrt(max(0.0, 1.0 - dx * dx)) * 8)
        _set(c, ax, oy + 1, OCC)
        _set(c, ax, oy + 2, OCC)
    for x in range(x0, x1 + 1):
        dx = (x - mid) / half
        top = int(round(17 - math.sqrt(max(0.0, 1.0 - dx * dx)) * 7))
        for y in range(top, y1 + 1):
            # The recess: dark, the top and left in the key's shadow, a little light reaching the
            # floor of the niche on the right.
            _set(c, x, y, ST[4] if (y > y1 - 7 and x > x0 + 7) else OCC)
    # the sill
    for x in range(x0 - 3, x1 + 4):
        _set(c, x, y1 + 1, ST[1])
        _set(c, x, y1 + 2, ST[2])
        _set(c, x, y1 + 3, OCC)
    skull(c, 18, y1 - 11)


def dress_window(c):
    x0, x1, y0, y1 = 11, 36, 9, 26
    for x in range(x0 - 3, x1 + 4):
        _set(c, x, y0 - 3, ST[1])
        _set(c, x, y0 - 2, ST[2])
        _set(c, x, y0 - 1, ST[3])
        _set(c, x, y1 + 1, ST[1])
        _set(c, x, y1 + 2, ST[3])
        _set(c, x, y1 + 3, OCC)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            # A cell beyond, in the dark: nothing in it, and no floor showing.
            _set(c, x, y, ST[4] if (y > y1 - 3 and (x + y) % 3 == 0) else OCC)
    for y in range(y0, y1 + 1):
        _set(c, x0, y, ST[4])
        _set(c, x1, y, ST[3])
    # Bars, set into the lintel and sill, lit on their left.
    for bx in (14, 19, 24, 29, 33):
        for y in range(y0 - 1, y1 + 2):
            _set(c, bx, y, STL[2])
            _set(c, bx + 1, y, STL[3])
            if y0 <= y <= y1:
                _set(c, bx + 2, y, OCC)
    for x in range(x0 + 1, x1):
        if _get(c, x, 17) in (OCC, ST[4]):
            _set(c, x, 17, STL[3])
            _set(c, x, 18, OCC)
    for bx in (14, 19, 24, 29, 33):
        _set(c, bx, 17, STL[1])


def chain_link(c, x, y, vertical):
    if vertical:
        shape = ["XX", "X.", "X.", "XX"]
    else:
        shape = ["XXX", "X.X"]
    for j, row in enumerate(shape):
        for i, ch in enumerate(row):
            if ch == "X":
                _set(c, x + i, y + j, STL[2] if i == 0 else STL[3])
    _set(c, x + len(shape[0]), y + len(shape), OCC)


def ring(c, x, y):
    iron_plate(c, x, y - 3, 4, 3)
    for dx, dy in ((1, 0), (2, 0), (0, 1), (3, 1), (0, 2), (3, 2), (0, 3), (3, 3), (1, 4), (2, 4)):
        _set(c, x + dx, y + dy, STL[3])
    _set(c, x + 1, y, STL[1])
    _set(c, x, y + 1, STL[2])
    _set(c, x, y + 2, STL[2])
    _set(c, x + 2, y + 5, OCC)
    _set(c, x + 3, y + 5, OCC)
    _set(c, x + 4, y + 2, OCC)


def shackle(c, x, y):
    """An open cuff hanging from a chain: a band of steel, lit on the left."""
    shape = [".XXXX.", "XX..XX", "X....X", "X....X", "XX..XX", ".XXXX."]
    for j, row in enumerate(shape):
        for i, ch in enumerate(row):
            if ch == "X":
                _set(c, x + i, y + j, STL[2] if i <= 1 or j == 0 else STL[3])
    for i in range(1, 6):
        _set(c, x + i + 1, y + 6, OCC)


def dress_chains(c):
    ring(c, 7, 11)
    ring(c, 36, 13)
    for k in range(10):
        t = k / 9.0
        x = int(10 + t * 26)
        y = int(15 + t * 2 + math.sin(t * math.pi) * 10)
        chain_link(c, x, y, k % 2 == 0)
    for k in range(4):
        chain_link(c, 37, 19 + k * 4, True)
    shackle(c, 35, 35)


def dress_manacles(c):
    iron_plate(c, 19, 9, 10, 6)
    for side, x in ((-1, 18), (1, 28)):
        for k in range(4):
            chain_link(c, x + side * (k // 2), 16 + k * 4, True)
        shackle(c, x - 2 + side * 2, 32)


def bone(c, x0, y0, x1, y1):
    """A long bone lying: a shaft with a knob at each end, lit along its top."""
    n = max(abs(x1 - x0), abs(y1 - y0))
    for k in range(n + 1):
        x = int(round(x0 + (x1 - x0) * k / float(n)))
        y = int(round(y0 + (y1 - y0) * k / float(n)))
        _set(c, x, y, SK[2])
        _set(c, x, y + 1, SK[3])
        _set(c, x, y + 2, OCC)
    for (x, y) in ((x0, y0), (x1, y1)):
        for dx in (-1, 0):
            _set(c, x + dx, y - 1, SK[2])
            _set(c, x + dx, y, SK[2])
            _set(c, x + dx, y + 1, SK[3])
            _set(c, x + dx, y + 2, OCC)


def dress_bones(c):
    # A heap against the foot of the wall: it overlaps the bottom course, so it reads as piled
    # against the stone - and it stays in the wall's tile, so nothing lies on the floor.
    for y in range(FOOT - 9, FOOT + 2):
        for x in range(5, 44):
            d = ((x - 24) / 19.0) ** 2 + ((y - FOOT - 1) / 9.0) ** 2
            if d < 1.0:
                _set(c, x, y, OCC if d > 0.75 else ST[4])
    bone(c, 7, FOOT - 3, 21, FOOT - 6)
    bone(c, 27, FOOT - 7, 41, FOOT - 3)
    bone(c, 13, FOOT - 1, 33, FOOT)
    skull(c, 18, FOOT - 14)
    for k in range(3):
        x = 33 + k * 3
        for j in range(4):
            _set(c, x + j // 2, FOOT - 9 + j, SK[3] if j < 3 else OCC)


def dress_skull_spike(c):
    # A spike driven into the joint under the coping, a skull on it, the point through the crown.
    cx = 24
    iron_plate(c, cx - 4, 30, 9, 5)
    for y in range(8, 30):
        _set(c, cx, y, STL[2])
        _set(c, cx + 1, y, STL[3])
        _set(c, cx + 2, y, OCC)
    _set(c, cx, 7, STL[1])
    skull(c, cx - 5, 13)
    _set(c, cx, 12, STL[2])
    _set(c, cx + 1, 12, STL[3])


def dress_seep(c):
    # Water coming through the joints: a darker, greener streak from the coping to the foot, a wet
    # glint or two, and moss where it pools.
    cx = 27
    for y in range(COPING_SHADOW + 1, FOOT):
        w = 3 + int(hashed(y // 3, 1, 3300) * 3)
        for x in range(cx - w // 2, cx + w - w // 2):
            here = _get(c, x, y)
            if here in (ST[1], ST[2], ST[3]):
                _set(c, x, y, LN[4] if (x + y) % 3 else LI[4])
            elif here == ST[4]:
                _set(c, x, y, LI[4])
        if y % 7 == 3:
            _set(c, cx - 1, y, ST[1])
    for x in range(cx - 8, cx + 9):
        if hashed(x, 2, 3301) < 0.8:
            _set(c, x, FOOT - 1, LI[3] if x % 3 else LI[2])
            _set(c, x, FOOT, LI[4])
        if hashed(x, 3, 3302) < 0.45:
            _set(c, x, FOOT - 2, LI[3])


def dress_broken(c):
    # A block gone from the middle course: the hole dark, its sill lit where the key reaches in.
    top = COURSE_TOPS[1]
    x0, x1, y0, y1 = 15, 32, top, top + COURSE_H - 2
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            _set(c, x, y, OCC if (x > x0 + 1 and y > y0 + 1) else ST[4])
    for x in range(x0 + 2, x1 + 1):
        _set(c, x, y1, ST[3])
    for (sx, sy, dx, dy, n) in ((x1, y0 + 3, 1, 1, 10), (x0, y1, -1, 1, 9), (x0 + 6, y0, 1, -1, 6)):
        x, y = sx, sy
        for k in range(n):
            x += dx if k % 3 != 2 else 0
            y += dy if k % 3 != 1 else 0
            _set(c, x, y, OCC)
    for (x, r) in ((12, 2), (18, 1), (23, 2), (30, 1), (35, 2)):
        _set(c, x, FOOT - 1, ST[2])
        _set(c, x + 1, FOOT - 1, ST[3])
        if r > 1:
            _set(c, x, FOOT - 2, ST[1])
            _set(c, x + 1, FOOT - 2, ST[2])
            _set(c, x + 2, FOOT - 1, OCC)


def dress_tally(c):
    # Somebody held here counted days on the stone: groups of four strokes and a slash, scratched
    # paler than the block. Quiet - it is for the player who stops to look.
    y0 = COURSE_TOPS[1] + 3
    x = 7
    for group in range(3):
        for k in range(4):
            for y in range(y0, y0 + 6):
                _set(c, x + k * 2, y, ST[1])
        for k in range(9):
            _set(c, x - 1 + k, y0 + 5 - k * 6 // 9, ST[0] if k == 4 else ST[1])
        x += 12
    y0 = COURSE_TOPS[2] + 3
    for k in range(3):
        for y in range(y0, y0 + 6):
            _set(c, 10 + k * 2, y, ST[1])


def dress_grate(c):
    # A drain at the foot of the wall, arched and barred, the wet running out under it.
    x0, x1, y0 = 13, 34, COURSE_TOPS[2]
    mid = (x0 + x1) / 2.0
    for x in range(x0 - 2, x1 + 3):
        dx = (x - mid) / ((x1 - x0) / 2.0 + 2)
        top = int(round(y0 + 3 - math.sqrt(max(0.0, 1.0 - dx * dx)) * 5))
        _set(c, x, top - 1, ST[1])
        for y in range(top, FOOT + 1):
            _set(c, x, y, OCC if x0 <= x <= x1 else ST[2])
    for bx in (16, 20, 24, 28, 32):
        for y in range(y0 - 3, FOOT + 1):
            if _get(c, bx, y) == OCC:
                _set(c, bx, y, STL[3])
                if _get(c, bx - 1, y) == OCC and y % 3 == 0:
                    _set(c, bx - 1, y, STL[2])
    for x in range(x0, x1 + 1):
        if _get(c, x, y0 + 4) == OCC:
            _set(c, x, y0 + 4, STL[4])
    for x in range(x0 + 2, x1 - 1):
        _set(c, x, FOOT + 1, LI[4] if x % 2 else LN[4])


FACE_PAINTERS = {
    "sconce": lambda c: dress_sconce(c, True),
    "sconce_dead": lambda c: dress_sconce(c, False),
    "skull_niche": dress_skull_niche,
    "window": dress_window,
    "chains": dress_chains,
    "manacles": dress_manacles,
    "bones": dress_bones,
    "skull_spike": dress_skull_spike,
    "seep": dress_seep,
    "broken": dress_broken,
    "tally": dress_tally,
    "grate": dress_grate,
}


# --- dressing on a top -------------------------------------------------------------------------------

def dress_cap_moss(c, seed):
    # Cushions of moss on the wall's top, in the ground's own lichen at its darkest.
    for k in range(3):
        cx = 10 + int(hashed(k, seed, 3400) * 28)
        cy = 10 + int(hashed(k, seed, 3401) * 28)
        r = 3 + int(hashed(k, seed, 3402) * 3)
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 > r * r:
                    continue
                _set(c, x, y, LI[3] if (x - cx) + (y - cy) < -r * 0.6 else LI[4])
        _set(c, cx + r, cy + r // 2, OCC)


def dress_cap_rubble(c, seed):
    # A broken cobble: a crack across the top and a few loose stones on it.
    x, y = 8 + int(hashed(1, seed, 3410) * 8), 14 + int(hashed(2, seed, 3411) * 10)
    for k in range(26):
        x += 1
        if k % 4 == 1:
            y += 1 if hashed(k, seed, 3412) < 0.5 else -1
        _set(c, x, y, OCC)
    for k in range(4):
        px = 10 + int(hashed(k, seed, 3413) * 26)
        py = 26 + int(hashed(k, seed, 3414) * 14)
        _set(c, px, py, ST[2])
        _set(c, px + 1, py, ST[3])
        _set(c, px, py - 1, ST[1] if k % 2 else ST[2])
        _set(c, px + 1, py + 1, OCC)


def dress_side(c, dressing, west):
    """A sconce or a chain on a vertical wall, hung on the edge that faces the corridor. The top of
    the wall is what shows, so the iron sits on its edge and the flame stands up off it."""
    x = 8 if west else SIZE - 11
    if dressing in ("sconce", "sconce_dead"):
        lit = dressing == "sconce"
        if lit:
            light_pool(c, x + 1, 17, 17)
        edge = 0 if west else SIZE - 1
        for xx in range(min(edge, x), max(edge, x) + 1):
            _set(c, xx, 30, STL[3])
            _set(c, xx, 31, STL[4])
            _set(c, xx, 32, OCC)
        sconce_body(c, x, 23)
        if lit:
            cold_flame(c, x, 21)
        else:
            for xx in range(x - 2, x + 3):
                _set(c, xx, 21, FL[4])
            _set(c, x, 20, OCC)
    elif dressing == "chains":
        ex = 2 if west else SIZE - 7
        ring(c, ex, 9)
        for k in range(6):
            chain_link(c, ex + 1, 15 + k * 4, True)
        shackle(c, ex - 1, 39)


# --- the tile -------------------------------------------------------------------------------------------

def seed_for(dressing, plain_index):
    names = [d for d, _ in FACE_DRESSINGS]
    return 37 + plain_index * 11 + (names.index(dressing) * 5 + 3 if dressing in names else 0)


def paint(c, role, dressing, plain_index, sides, inner):
    """Draw one wall tile. `sides` are the sides with floor beyond them (t, b, l, r); `inner` is the
    floored diagonal of an inner corner, as tiles.INNER gives it."""
    seed = seed_for(dressing, plain_index)
    if role == "patch":
        # A lone stub of wall (only ever left by the spawn clearing): its top above, its face below.
        cap(c, seed)
        rim_top(c)
        rim_left(c)
        rim_right(c)
        f = _Shift(c, 18)
        face(f, seed)
        face_end_left(f)
        face_end_right(f)
        return

    if "b" in sides:
        face(c, seed)
        if dressing in FACE_PAINTERS:
            FACE_PAINTERS[dressing](c)
        if "l" in sides:
            face_end_left(c)
        if "r" in sides:
            face_end_right(c)
        return

    cap(c, seed)
    if dressing == "moss":
        dress_cap_moss(c, seed)
    elif dressing == "rubble":
        dress_cap_rubble(c, seed)
    if "t" in sides:
        rim_top(c)
    if "l" in sides:
        rim_left(c)
    if "r" in sides:
        rim_right(c)
    if role in SIDE_ROLES and dressing in ("sconce", "sconce_dead", "chains"):
        dress_side(c, dressing, role == "edge_left")
    if inner:
        v, h = inner
        if (v, h) == ("t", "l"):
            # the rims of the two neighbours meeting at this corner
            for k in range(3):
                _set(c, k, 0, ST[1])
                _set(c, 0, k, ST[1])
            _set(c, 1, 1, ST[2])
        elif (v, h) == ("t", "r"):
            for k in range(3):
                _set(c, SIZE - 1, k, OCC)
                _set(c, SIZE - 2, k, OCC)
        elif (v, h) == ("b", "l"):
            # the wall's west edge running down past the face beside it
            rim_left(c)
        elif (v, h) == ("b", "r"):
            rim_right(c)


class _Shift(object):
    """A canvas view squeezed into the rows below `oy`, for the patch's short face."""

    def __init__(self, c, oy):
        self.c, self.oy = c, oy

    def _y(self, y):
        return int(y) * (SIZE - self.oy) // SIZE + self.oy

    def set(self, x, y, col):
        self.c.set(int(x), self._y(y), col)

    def get(self, x, y):
        return self.c.get(int(x), self._y(y))


# --- the shadow a wall throws on the floor ---------------------------------------------------------------
#
# The key is from the upper left (.ai/art-direction.md section 2), so a wall's shadow falls down and
# to the right: across the floor at the foot of a face, and along the floor east of a vertical wall.
# Drawn on the FLOOR cell, as a transparent tile of `occ` broken in 2x2 blocks (the ground's soft
# edges are dithered the same way), so the flagstones still show through it. It is what makes a face
# stand up off the floor instead of lying on it - and it is quiet: no shape, nothing to pick out.
#
# Roles, named for the side of the FLOOR cell the wall is on: shade_n (a wall north of it, a face),
# shade_w (a wall west of it), shade_nw (both), shade_corner (only the north-west diagonal, where a
# face ends - the tail of the face's shadow).

SHADOW_ROLES = ["shade_n", "shade_w", "shade_nw", "shade_corner"]
SHADOW_FACE = 12     # rows of shadow under a face
SHADOW_SIDE = 7      # columns beside a vertical wall


def _shadow_density(d, length):
    """How much of a 2x2 block is shadow at d pixels from the wall."""
    if d >= length:
        return 0.0
    return 0.85 * (1.0 - d / float(length)) ** 0.8


def shadow(c, role):
    for by in range(0, SIZE, 2):
        for bx in range(0, SIZE, 2):
            dens = 0.0
            if role in ("shade_n", "shade_nw"):
                dens = max(dens, _shadow_density(by, SHADOW_FACE))
            if role in ("shade_w", "shade_nw"):
                dens = max(dens, _shadow_density(bx, SHADOW_SIDE))
            if role == "shade_corner":
                # the end of a face's shadow, sliding off to the right below its corner
                if bx < SHADOW_FACE - by:
                    dens = max(dens, _shadow_density(by + bx, SHADOW_FACE))
            th = (BAYER[(by // 2) % 4][(bx // 2) % 4] + 0.5) / 16.0
            if dens > th:
                for dy in (0, 1):
                    for dx in (0, 1):
                        _set(c, bx + dx, by + dy, OCC)
