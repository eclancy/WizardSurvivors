# -*- coding: utf-8 -*-
"""The Court's walls (.ai/endgame-castle.md 5.8). Drawn by tools/art/tiles.py (tiles.wall_modules) as
the `court_wall` terrain, which the Court's StageEnvironmentProfile.WallSet names:

    python tools/art/tiles.py court_wall      # redraw only these while iterating
    python tools/art/tiles.py                  # the full run that writes tiles.json

THE SAME WALL AS THE JAIL'S, IN HIS HOUSE. Everything jail_walls.py says about how a wall tile knows
what it is holds here unchanged - the walls are two tiles thick, the autotiler's role says which sides
are floor, and in three-quarter view the side that matters is SOUTH: floor to the south is the wall's
FACE, standing toward the viewer, and anything else is its CAP, the top of the wall from above. The
same fourteen roles, the same three stubs, the same plain-and-dressed variants with their weights, and
the same three values in one order: the floor is the lightest thing, the face a step under it, the cap
the darkest. Only what the wall is made of changes.

    the face   a coping with its lit arris; a cornice; plaster in grave-linen's deepest stop; a dado
               rail; and a wainscot of oak panels (gold's deep half, as the Court's boards are) down
               to a skirting and the contact line. A palace's wall, not a gaol's.
    the cap    dressed slabs laid in courses, not the jail's rough cobbles: somebody built this well.

WHAT HANGS ON IT, AND WHY. His cold fire in the sconces (the `arcane` row round a white core, as in his
cells - it is his house); banners of the household with no device on them (the Household Guard's
shield has none either: his house has no arms but him); empty gilt frames (a court with no one left
in it); a blank shield; an urn in a niche; a crack. And TAPESTRIES, hung true - because the Court's
hidden ways are tapestries hung over real gaps (5.5, props_court.py's hidden tapestry), and if only the
hidden ones were tapestries every tapestry would be a pointer. The hidden one hangs a pixel off true,
and something is moving in the air in front of it; these do not, and nothing is.

Nothing on a wall is warm (the player's light, a secret's glint), nothing is an XP orb's colour,
nothing animates, and no dressing is drawn as a way through.
"""

import math

import bonelight as bl
import jail_walls as J

WALL_TERRAIN = "court_wall"

OCC = bl.OCC
SIZE = 48

ST = J.ST
LN = J.LN
GD = J.ramp("gold")
RD = J.ramp("red")
VI = J.ramp("violet")
STL = J.STL
AR = J.AR
SK = J.SK

hashed = J.hashed
_set = J._set
_get = J._get


def _put(c, x, y, col):
    # Unclipped to one tile: the tapestry is drawn on a tile and on the five-cell hidden-way sprite.
    c.set(int(x), int(y), col)

# The roles and stubs are the jail's, so the painter asks for the same keys of either set.
SIDE_ROLES = J.SIDE_ROLES
STUB_SIDES = J.STUB_SIDES
PLAIN_VARIANTS = J.PLAIN_VARIANTS

FACE_DRESSINGS = [
    ("sconce", 4),        # his cold fire, the light pooled on the plaster and the panels
    ("sconce_dead", 2),   # guttered: cold iron and soot
    ("banner", 3),        # the household's banner, red, with no device
    ("frame", 3),         # an empty gilt frame
    ("tapestry", 2),      # a tapestry hung true (the hidden ones hang a pixel off it)
    ("shield", 1),        # a blank shield
    ("niche", 1),         # an urn in an arched niche
    ("crack", 1),         # a crack through the plaster
]
SIDE_DRESSINGS = [("sconce", 3), ("sconce_dead", 1)]


def variants(role):
    """[(dressing or None, plain index, weight)] for a role, plain first, in manifest order."""
    plain = PLAIN_VARIANTS.get(role, 2)
    out = [(None, k, 1) for k in range(plain)]
    if role == "edge_bottom":
        out += [(d, 0, w) for d, w in FACE_DRESSINGS]
    elif role in SIDE_ROLES:
        out += [(d, 0, w) for d, w in SIDE_DRESSINGS]
    return out


# --- the face -------------------------------------------------------------------------------------
#
# Rows, fixed in every variant so a face is one wall whatever stands beside it:
COPING_SHADOW = 4
CORNICE = 5
DADO = 24
PANEL_TOP = 27
PANEL_BOTTOM = 41
SKIRTING = 42
FOOT = 44
PANEL_PITCH = 16          # three panels a tile, on the same columns in every variant


def face(c, seed):
    # The coping, as the jail's: the lit arris is what says "the top edge of something tall".
    for x in range(SIZE):
        _set(c, x, 0, ST[1])
        _set(c, x, 1, ST[1] if hashed(x // 3, 1, 4099) < 0.3 else ST[2])
        _set(c, x, 2, ST[2])
        _set(c, x, 3, ST[3])
        _set(c, x, COPING_SHADOW, OCC)
    # The cornice: a moulding of plaster under the coping, lit on its upper lip.
    for x in range(SIZE):
        _set(c, x, CORNICE, LN[2] if x % 6 else LN[3])
        _set(c, x, CORNICE + 1, LN[3])
        _set(c, x, CORNICE + 2, OCC if x % 6 == 3 else LN[4])
    # The plaster, in linen's deepest stop, with a few soft blotches a stop up - kept inside the
    # tile, the variant's own, so they never cut at a seam.
    for y in range(CORNICE + 3, DADO):
        for x in range(SIZE):
            _set(c, x, y, LN[4])
    for k in range(3):
        cx = 6 + int(hashed(k, seed, 4110) * 36)
        cy = CORNICE + 6 + int(hashed(k, seed, 4111) * 10)
        r = 2 + int(hashed(k, seed, 4112) * 3)
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r - 1, cx + r + 2):
                d = ((x - cx) / float(r + 1)) ** 2 + ((y - cy) / float(r)) ** 2
                if d < 1.0 and hashed(x // 2, y // 2, 4113 + seed) < (1.0 - d) * 0.9:
                    _set(c, x, y, LN[3])
    # The dado rail: oak, lit along its top, its shadow under it.
    for x in range(SIZE):
        _set(c, x, DADO, GD[3])
        _set(c, x, DADO + 1, GD[4])
        _set(c, x, DADO + 2, OCC)
    # The wainscot: three raised panels, each a field framed by its stiles and rails, its bevel lit on
    # the top and left (the key is upper-left) and in shadow on the bottom and right.
    for y in range(PANEL_TOP, PANEL_BOTTOM + 1):
        for x in range(SIZE):
            _set(c, x, y, GD[4])
    for p in range(SIZE // PANEL_PITCH):
        x0 = p * PANEL_PITCH + 3
        x1 = x0 + PANEL_PITCH - 6
        y0, y1 = PANEL_TOP + 2, PANEL_BOTTOM - 2
        for x in range(x0, x1 + 1):
            _set(c, x, y0, GD[3])
            _set(c, x, y1, OCC)
        for y in range(y0, y1 + 1):
            _set(c, x0, y, GD[3])
            _set(c, x1, y, OCC)
        # Grain in the field: a streak or two, the variant's own.
        for k in range(2):
            gx = x0 + 2 + int(hashed(p, k, 4120 + seed) * (x1 - x0 - 4))
            for y in range(y0 + 2, y1 - 1):
                if hashed(y, p * 3 + k, 4121 + seed) < 0.45:
                    _set(c, gx, y, GD[3])
    # The skirting and the foot: the contact line the wall stands on.
    for x in range(SIZE):
        _set(c, x, SKIRTING, GD[3])
        _set(c, x, SKIRTING + 1, GD[4])
        _set(c, x, FOOT, ST[4])
        for y in range(FOOT + 1, SIZE):
            _set(c, x, y, OCC)
    # Wear on some variants: a scuffed panel corner, a chip out of the coping.
    if hashed(seed, 1, 4130) < 0.4:
        px = 8 + int(hashed(seed, 2, 4131) * 30)
        _set(c, px, PANEL_BOTTOM - 3, OCC)
        _set(c, px + 1, PANEL_BOTTOM - 3, OCC)
        _set(c, px, PANEL_BOTTOM - 4, GD[3])
    if hashed(seed, 3, 4132) < 0.4:
        px = 6 + int(hashed(seed, 4, 4133) * 34)
        _set(c, px, 1, OCC)
        _set(c, px + 1, 1, OCC)
        _set(c, px + 1, 2, ST[3])


def face_end_left(c):
    # The west end of a face: its corner catches the key light down to the foot.
    for y in range(0, FOOT):
        _set(c, 0, y, ST[1] if y < CORNICE else (LN[2] if y < DADO else GD[3]))
        here = _get(c, 1, y)
        if here in (ST[3], ST[4], LN[4], GD[4]):
            _set(c, 1, y, ST[2] if y < CORNICE else (LN[3] if y < DADO else GD[4]))


def face_end_right(c):
    for y in range(0, SIZE):
        _set(c, SIZE - 1, y, OCC)
        here = _get(c, SIZE - 2, y)
        if here in (ST[1], ST[2], ST[3], LN[2], LN[3], GD[3]):
            _set(c, SIZE - 2, y, ST[4] if y < CORNICE else (LN[4] if y < DADO else GD[4]))


# --- the cap --------------------------------------------------------------------------------------

def cap(c, seed):
    """The top of the wall: dressed slabs in courses, in the deep end of the stone row.

    Three courses of 16 to a tile, slabs 24 long and offset half a slab course to course, on the same
    pixels in every variant (so a run of cap is one wall top); each slab lit along its upper edge and
    parted from the next by `occ`. The darkest stone on screen, as the jail's cap is, and calm.
    """
    for y in range(SIZE):
        course = y // 16
        off = 12 if course % 2 else 0
        for x in range(SIZE):
            ly = y % 16
            lx = (x + off) % 24
            if ly == 15 or lx == 23:
                tone = OCC
            elif ly == 0:
                tone = ST[3]
            elif lx == 0:
                tone = ST[3] if ly < 8 else ST[4]
            else:
                tone = ST[4]
            _set(c, x, y, tone)
    # A worn slab or a hairline crack, the variant's own, inside the tile.
    if hashed(seed, 5, 4140) < 0.5:
        x, y = 8 + int(hashed(seed, 6, 4141) * 26), 4 + int(hashed(seed, 7, 4142) * 36)
        for k in range(7):
            _set(c, x + k, y + (k // 3), OCC)


# --- dressing on a face --------------------------------------------------------------------------

# His cold light on the plaster, the panels and the stone round a flame (jail_walls.light_pool's rule,
# for a wall that is not all stone).
NEAR = {ST[4]: AR[4], ST[3]: AR[3], ST[2]: AR[2], ST[1]: AR[2], ST[0]: AR[1], OCC: ST[4],
        LN[4]: AR[4], LN[3]: AR[3], LN[2]: AR[2], GD[4]: AR[4], GD[3]: AR[3]}
FAR = {ST[4]: AR[4], ST[3]: AR[4], ST[2]: AR[3], ST[1]: AR[3], ST[0]: AR[2],
       LN[3]: AR[4], LN[2]: AR[3], GD[3]: AR[4]}


def light_pool(c, cx, cy, r):
    for y in range(int(cy - r), int(cy + r) + 1):
        for x in range(int(cx - r), int(cx + r) + 1):
            d = math.hypot(x - cx, (y - cy) * 0.9) / float(r)
            if d >= 1.0:
                continue
            here = _get(c, x, y)
            if here is None:
                continue
            th = (J.BAYER[y % 4][x % 4] + 0.5) / 16.0
            if d < 0.6 and th < 1.05 - d * 1.1:
                if here in NEAR:
                    _set(c, x, y, NEAR[here])
            elif th < (1.0 - d) * 1.1:
                if here in FAR:
                    _set(c, x, y, FAR[here])


def dress_sconce(c, lit, cx=23, cup_y=17):
    if lit:
        light_pool(c, cx + 1, cup_y - 4, 20)
    else:
        for y in range(CORNICE + 3, cup_y - 1):
            spread = 1 + (cup_y - y) // 3
            for x in range(cx - spread + 1, cx + spread + 1):
                th = (J.BAYER[y % 4][x % 4] + 0.5) / 16.0
                if th < 0.6 and _get(c, x, y) in (LN[4], LN[3]):
                    _set(c, x, y, ST[4] if th > 0.25 else OCC)
    J.sconce_body(c, cx, cup_y)
    if lit:
        J.cold_flame(c, cx, cup_y - 2)
    else:
        for x in range(cx - 2, cx + 3):
            _set(c, x, cup_y - 2, LN[3])
        _set(c, cx, cup_y - 3, OCC)


def dress_banner(c):
    """The household's banner: red cloth from a gold rod under the cornice, a swallowtail at its foot,
    lit on its left edge and folded once down its length. No device: his house has no arms but him."""
    x0, x1, top, foot = 16, 31, CORNICE + 3, 38
    for x in range(x0 - 2, x1 + 3):
        _set(c, x, top - 1, GD[3])
        _set(c, x, top, OCC)
    _set(c, x0 - 2, top - 1, GD[2])
    _set(c, x1 + 2, top - 1, GD[4])
    fold = (x0 + x1) // 2
    for y in range(top + 1, foot + 1):
        for x in range(x0, x1 + 1):
            tone = RD[2]
            if x == x0:
                tone = RD[1]
            elif x == x1:
                tone = RD[4]
            elif x in (fold, fold + 1):
                tone = RD[3] if x == fold + 1 else RD[2]
            _set(c, x, y, tone)
    # The swallowtail: a notch up the middle of the foot.
    for k in range(5):
        for x in range(fold - (4 - k), fold + (5 - k)):
            _set(c, x, foot - k, LN[4] if foot - k < DADO else GD[4])
    # The fringe at the two tails, and the banner's shadow on the wall to its right.
    for x in list(range(x0, fold - 4)) + list(range(fold + 5, x1 + 1)):
        _set(c, x, foot + 1, GD[3] if x % 2 else GD[4])
    for y in range(top + 2, foot + 1):
        _set(c, x1 + 1, y, OCC)


def dress_frame(c):
    """An empty gilt frame: the canvas gone, only the dark of the wall behind it."""
    x0, x1, y0, y1 = 12, 35, CORNICE + 4, DADO - 3
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            edge = x in (x0, x0 + 1, x1 - 1, x1) or y in (y0, y0 + 1, y1 - 1, y1)
            if edge:
                lit = x <= x0 + 1 or y <= y0 + 1
                _set(c, x, y, GD[2] if lit else GD[4])
            else:
                _set(c, x, y, OCC if (x + y) % 5 else ST[4])
    _set(c, x0, y0, GD[1])
    for x in range(x0 + 1, x1 + 2):
        _set(c, x, y1 + 1, OCC)
    for y in range(y0 + 1, y1 + 2):
        _set(c, x1 + 1, y, OCC)


def tapestry(c, x0, y0, w, h, skew=0, seed=0):
    """A woven hanging on a rod: a field of violet's deep half with a lozenge of red in it and a border,
    a fringe at its foot. `skew` drops its right-hand end that many pixels: a hanging a pixel off true
    (props_court.py's hidden tapestry). Shared, so a tapestry hung over a hidden way is the same cloth
    as one hung on a wall."""
    x1 = x0 + w - 1
    for x in range(x0 - 1, x1 + 2):
        _put(c, x, y0 - 1, GD[3])
    _put(c, x0 - 1, y0 - 1, GD[2])
    mid = (x0 + x1) / 2.0
    for x in range(x0, x1 + 1):
        drop = int(round(skew * (x - x0) / float(max(1, w - 1))))
        for y in range(y0, y0 + h + drop):
            bx = x - x0
            by = y - y0 - drop
            border = bx < 2 or bx > w - 3 or by < 2 or by > h - 3 + drop
            if border:
                tone = VI[4] if (bx + by) % 3 else RD[3]
            else:
                # The lozenge in the middle, and a small one above and below it.
                u = abs(x - mid) / (w * 0.32) + abs(by - h / 2.0) / (h * 0.30)
                if u < 0.55:
                    tone = RD[2] if u < 0.3 else RD[3]
                elif u < 0.62:
                    tone = GD[4]
                else:
                    tone = VI[3] if (bx // 3 + by // 2) % 2 else VI[4]
            if x == x0:
                tone = VI[2]
            _put(c, x, y, tone)
        # The fringe.
        if x % 2 == 0:
            _put(c, x, y0 + h + drop, GD[4])
        _put(c, x, y0 + h + drop + 1, OCC)
    for y in range(y0, y0 + h + skew):
        _put(c, x1 + 1, y, OCC)


def dress_tapestry(c):
    tapestry(c, 8, CORNICE + 4, 32, 30)


def dress_shield(c):
    """A blank kite shield of steel on the plaster: the guard's shield, with no device on it."""
    cx, top = 24, CORNICE + 5
    for k in range(16):
        half = 7 if k < 7 else max(0, 7 - (k - 6) * 7 // 9)
        y = top + k
        for x in range(cx - half, cx + half + 1):
            tone = STL[3]
            if x < cx - half + 2:
                tone = STL[2]
            elif x > cx + half - 2:
                tone = STL[4]
            _set(c, x, y, tone)
        _set(c, cx + half + 1, y + 1, OCC)
    for x in range(cx - 7, cx + 8):
        _set(c, x, top, STL[2])


def dress_niche(c):
    """An arched niche in the plaster with a stone urn on its sill."""
    x0, x1, y1 = 15, 32, DADO - 2
    mid = (x0 + x1) / 2.0
    half = (x1 - x0) / 2.0
    for x in range(x0 - 2, x1 + 3):
        dx = (x - mid) / (half + 2)
        if abs(dx) > 1.0:
            continue
        top = int(round(CORNICE + 9 - math.sqrt(1.0 - dx * dx) * 6))
        _set(c, x, top - 1, LN[2])
        for y in range(top, y1 + 1):
            inner = x0 <= x <= x1 and y > top
            _set(c, x, y, (ST[4] if x > mid else OCC) if inner else LN[3])
    for x in range(x0 - 2, x1 + 3):
        _set(c, x, y1 + 1, LN[2])
    # The urn: a bellied stone pot, lit on its left.
    ux, base = int(mid), y1
    for k, w in enumerate((2, 3, 4, 4, 3, 2, 3)):
        y = base - k
        for x in range(ux - w, ux + w + 1):
            _set(c, x, y, SK[3] if x < ux - w + 2 else SK[4])
    for x in range(ux - 3, ux + 4):
        _set(c, x, base - 7, SK[3])


def dress_crack(c):
    x, y = 14, CORNICE + 3
    for k in range(DADO - CORNICE - 3):
        x += 1 if hashed(k, 1, 4150) < 0.45 else (-1 if hashed(k, 2, 4151) < 0.3 else 0)
        _set(c, x, y + k, OCC)
        if hashed(k, 3, 4152) < 0.3:
            _set(c, x + 1, y + k, LN[3])


FACE_PAINTERS = {
    "sconce": lambda c: dress_sconce(c, True),
    "sconce_dead": lambda c: dress_sconce(c, False),
    "banner": dress_banner,
    "frame": dress_frame,
    "tapestry": dress_tapestry,
    "shield": dress_shield,
    "niche": dress_niche,
    "crack": dress_crack,
}


# --- the tile -------------------------------------------------------------------------------------

def seed_for(dressing, plain_index):
    names = [d for d, _ in FACE_DRESSINGS]
    return 41 + plain_index * 13 + (names.index(dressing) * 7 + 5 if dressing in names else 0)


def paint(c, role, dressing, plain_index, sides, inner):
    """Draw one wall tile; the arguments are jail_walls.paint's."""
    seed = seed_for(dressing, plain_index)
    if role == "patch":
        cap(c, seed)
        J.rim_top(c)
        J.rim_left(c)
        J.rim_right(c)
        f = J._Shift(c, 18)
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
    if "t" in sides:
        J.rim_top(c)
    if "l" in sides:
        J.rim_left(c)
    if "r" in sides:
        J.rim_right(c)
    if role in SIDE_ROLES and dressing in ("sconce", "sconce_dead"):
        J.dress_side(c, dressing, role == "edge_left")
    if inner:
        v, h = inner
        if (v, h) == ("t", "l"):
            for k in range(3):
                _set(c, k, 0, ST[1])
                _set(c, 0, k, ST[1])
            _set(c, 1, 1, ST[2])
        elif (v, h) == ("t", "r"):
            for k in range(3):
                _set(c, SIZE - 1, k, OCC)
                _set(c, SIZE - 2, k, OCC)
        elif (v, h) == ("b", "l"):
            J.rim_left(c)
        elif (v, h) == ("b", "r"):
            J.rim_right(c)
