# -*- coding: utf-8 -*-
"""The small things lying on the ground, and the first landmark. Run: python tools/art/ground_detail.py

Two outputs:

  DETAIL      48x48 transparent tiles - flowers, clover, lilypads, reeds, pebbles, a mushroom
              ring, cracks, snow - in `assets/bonelight/tiles/detail/`, with `detail.json`.
              LevelTilePainter paints them into one extra TileMapLayer per streamed chunk, and
              GroundDetail decides which goes on which cell.

  LANDMARKS   the flower pen: a walled garden round one glowing moonflower - rails, side
              segments, the flower and a ground decal (`assets/bonelight/world/landmarks/`, with
              `flower-pen.json`), each sorted at its own ground line so the wizard passes in front
              of the fence south of him and behind the fence north of him. Node2DGame places it,
              rarely, and its fence is solid.

WHY TILES AND NOT SPRITES. The ask was "a lot more fun little environment elements", and a lot
is the problem: decor is a StaticBody2D, a CollisionShape2D and a Sprite2D per item, and a
hundred flowers a screen at three nodes each, on a phone, streamed in and out with the chunks,
is exactly the node count `.ai/terrain.md` section 7 measured flat and wants to keep flat. A
TileMapLayer draws its cells in batched quadrants and costs ONE node a chunk however many cells
it holds. The price is that a detail sits on the 48 px grid - which is why every set has several
variants with the drawing placed at different spots inside the cell, so the grid never shows.

THE RULES FOR GROUND DETAIL, all from `.ai/art-direction.md` and `.ai/world-and-tone.md`:

  - It is never the subject - but it has to be a THING. Small (a flower head is 5x5 on a stem, a
    clump about 20 px across; see "SIZE, TWICE" below), drawn in the material rows and never in
    an element's emissive ramp, so a flower cannot be mistaken for a spell, a pickup or a
    telegraph. The XP orbs are `grass`, `arcane` and `light` emissives at 8-12 px;
    nothing here uses them.
  - No player pigment except `verdant`, which `.ai/terrain.md` section 4 already gave to living
    plants in the world (foliage on the ground's own ramp is a smudge).
  - Lit from the upper left like everything else, with an `occ` contact pixel under anything that
    stands up, so it sits on the ground plane rather than floating over it.
"""

import io
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster
import tile_atlas

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DETAIL = os.path.join(ROOT, "assets", "bonelight", "tiles", "detail")
OUT_LANDMARK = os.path.join(ROOT, "assets", "bonelight", "world", "landmarks")

SIZE = 48
TAU = math.pi * 2.0


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


ST = ramp("stone")
LI = ramp("lichen")
VE = ramp("verdant")
GO = ramp("gold")
FL = ramp("flesh")
SK = ramp("skin")
VI = ramp("violet")
AR = ramp("arcane")
RD = ramp("red")
LN = ramp("linen")
STL = ramp("steel")
AZ = ramp("azure")


class Rand(object):
    """A tiny deterministic generator - the art must come out identical on every run."""

    def __init__(self, seed):
        self.s = (seed * 2654435761 + 12345) & 0x7FFFFFFF

    def f(self):
        self.s = (self.s * 1103515245 + 12345) & 0x7FFFFFFF
        return (self.s >> 8) / float(1 << 23)

    def i(self, lo, hi):
        return lo + int(self.f() * (hi - lo + 1)) if hi > lo else lo


def spot(r, margin=7):
    """Somewhere inside the cell, clear of the edges. Different per variant, so the grid hides."""
    return r.i(margin, SIZE - 1 - margin), r.i(margin, SIZE - 1 - margin)


# --- small drawing vocabulary ----------------------------------------------------------------------

# SIZE, TWICE. The first pass drew five-pixel flowers and photographed as dust; the second drew 3x3
# heads on 4-14 px stems and still read, at camera zoom 0.85 on a 1280x720 screen, as two- and
# three-pixel specks (reported: "not fun little environment elements"). A flower now has a 5x5
# head with a lit and a shaded side, a stem with a leaf on it, and stands in a clump of its own
# foliage; a mushroom is a 7-9 px cap you can name; a lilypad is a pad 18-22 px across with its
# notch and its veins. Fewer of them (GroundDetail's densities came down) and each one a thing.
#
# Still never the subject: material tones only, the brightest petal is a `lit` tone and never a
# `hi` plane, nothing round and bright and alone. The XP orbs are round 8-12 px discs of lime
# #9BD455, lilac #C08CFF and pale yellow #FFF0A8 - so no flower head is a lone disc of those hues,
# yellow flowers are gold and come several to a clump, and nothing here glows.

def stem(c, x, y, h, tone=None):
    for k in range(h):
        c.set(x, y - k, tone or VE[3])


def leaf_on(c, x, y, side, length=3):
    """A leaf growing off a stem at (x, y): a short diagonal blade, lit on its upper edge."""
    for k in range(length):
        c.set(x + side * (k + 1), y - k // 2, VE[2] if k < length - 1 else VE[1])
        c.set(x + side * (k + 1), y - k // 2 + 1, VE[3])


def leafpair(c, x, y):
    leaf_on(c, x, y, -1, 3)
    leaf_on(c, x, y - 1, 1, 3)


def foliage(c, x, y, w, r):
    """The low leafy mound a clump of flowers stands in: a few dark leaves at the base, so the
    clump reads as a plant on the ground rather than as heads floating over it."""
    for k in range(w * 2):
        dx = r.i(-w, w)
        dy = r.i(-1, 2)
        c.set(x + dx, y + dy, VE[3] if (dx + dy) % 3 else VE[4])
        if r.f() < 0.4:
            c.set(x + dx + 1, y + dy, VE[2])
    for dx in range(-w + 2, w - 1, 2):
        c.set(x + dx, y + 3, OCC)


def daisy(c, x, y, petal, centre, h=5, shade=None):
    """A 5x5 bloom - a ring of petals, the lower-right ones in shade, a heart - on a stem."""
    shade = shade or petal
    stem(c, x, y + 3 + h, h + 1)
    leaf_on(c, x, y + 3 + h - 2, -1 if (x + y) % 2 else 1)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            if abs(dx) + abs(dy) > 3 or (abs(dx) == 2 and abs(dy) == 2):
                continue
            if dx == 0 and dy == 0:
                continue
            c.set(x + dx, y + dy, shade if dx + dy > 0 else petal)
    c.set(x, y, centre)
    c.set(x + 1, y + 1, centre)


def bud(c, x, y, petal, lit, h=5, shade=None):
    """A tulip cup, 4 wide and 5 tall with a notched rim, lit on its upper-left, on a stem."""
    shade = shade or petal
    stem(c, x, y + 4 + h, h + 1)
    leaf_on(c, x, y + 4 + h - 2, 1 if (x + y) % 2 else -1, 4)
    for dy in range(0, 5):
        for dx in range(-2, 2):
            if dy == 4 and dx in (-2, 1):
                continue
            if dy == 0 and dx in (-1, 0):
                continue  # the notch between the petal tips
            c.set(x + dx, y + dy, shade if dx >= 0 and dy > 1 else petal)
    c.set(x - 2, y + 1, lit)
    c.set(x - 2, y + 2, lit)
    c.set(x - 1, y + 1, lit)


def bell(c, x, y, petal, lit):
    """A bluebell: an arched stem with three 3x3 bells hanging along it."""
    for k in range(10):
        c.set(x + k, y - (1 if 1 < k < 8 else 0) - (1 if 3 < k < 6 else 0), VE[3])
    stem(c, x, y + 8, 8)
    leaf_on(c, x, y + 6, -1, 4)
    for k in (2, 5, 8):
        dy = 1 if k == 5 else 0
        for bx in range(3):
            for by in range(3):
                if by == 2 and bx == 1:
                    continue
                c.set(x + k - 1 + bx, y + 1 + dy + by, lit if bx == 0 and by == 0 else petal)


def tuft(c, x, y, mid, light, dark=None):
    """A tuft of long grass: blades fanning from one root, the outer ones arching."""
    for j in range(8):
        c.set(x, y - j, mid if j < 6 else light)
    for side in (-1, 1):
        for j in range(7):
            c.set(x + side * (1 + j // 2), y - 1 - j, mid if j < 5 else light)
        for j in range(5):
            c.set(x + side * (3 + j // 2), y - 1 - j, mid if j < 3 else light)
    if dark:
        for dx in (-2, -1, 0, 1, 2):
            c.set(x + dx, y + 1, dark)


def pebble(c, x, y, big):
    """A stone, lit upper-left, on its contact shadow. 5x3, or 7x4 when big."""
    w = 3 if big else 2
    hgt = 3 if big else 2
    for dx in range(-w, w + 1):
        c.set(x + dx, y + 1, OCC)
    for dy in range(-hgt, 1):
        for dx in range(-w, w + 1):
            if (abs(dx) == w and dy in (-hgt, 0)) or (abs(dx) == w - 1 and dy == -hgt and big):
                continue
            tone = ST[2]
            if dy == -hgt or (dx < 0 and dy == -hgt + 1):
                tone = ST[1]
            if dx > w - 2 or (dy == 0 and dx > 0):
                tone = ST[3]
            c.set(x + dx, y + dy, tone)
    c.set(x - w + 1, y - hgt + 1, ST[0] if big else ST[1])


def leaf(c, x, y, body, vein, turn):
    """A fallen leaf, 5x7, with its midrib and a stalk, lying one of two ways."""
    pts = []
    for dy in range(-3, 4):
        half = 2 - abs(dy) // 2 if abs(dy) < 3 else 0
        for dx in range(-half, half + 1):
            pts.append((dx, dy))
    if turn % 2:
        pts = [(dy, dx) for dx, dy in pts]
    for dx, dy in pts:
        c.set(x + dx, y + dy, body)
    for k in range(-3, 5):
        if turn % 2:
            c.set(x + k, y, vein)
        else:
            c.set(x, y + k, vein)


def mushroom(c, x, y, cap, lit, dots=False, shade=None, big=False):
    """A toadstool: a domed cap (7 or 9 wide) over a dark gill line, on a pale stalk, with its shadow."""
    shade = shade or cap
    w = 4 if big else 3
    sh = 4 if big else 3
    for dx in range(-w + 1, w):
        c.set(x + dx, y + 1, OCC)
    for k in range(sh):
        c.set(x, y - k, SK[2])
        c.set(x + 1, y - k, SK[3])
    top = y - sh
    # gills: the underside of the cap, in shadow
    for dx in range(-w + 1, w):
        c.set(x + dx, top, SK[3] if abs(dx) < w - 1 else SK[4])
    # the cap: three rows, narrowing
    for row, half in ((1, w), (2, w - 1), (3, w - 2)):
        for dx in range(-half, half + 1):
            c.set(x + dx, top - row, shade if dx > half // 2 or (row == 1 and dx > 0) else cap)
    c.set(x - w + 1, top - 2, lit)
    c.set(x - w + 2, top - 3, lit)
    c.set(x - 1, top - 3, lit)
    if dots:
        c.set(x + 1, top - 2, SK[1])
        c.set(x - 2, top - 1, SK[1])
        c.set(x + w - 1, top - 1, SK[2])


def crackline(c, x, y, length, tone, r):
    ang = r.f() * TAU
    px, py = float(x), float(y)
    for j in range(length):
        if j % 3 == 0:
            ang += (r.f() - 0.5) * 1.8
        px += math.cos(ang)
        py += math.sin(ang)
        if 2 <= px < SIZE - 2 and 2 <= py < SIZE - 2:
            c.set(int(px), int(py), tone)
            if j % 5 == 2 and r.f() < 0.5:
                c.set(int(px) + 1, int(py), tone)


# --- the detail sets -------------------------------------------------------------------------------
# Each takes (canvas, rand) and draws one variant. The variant number seeds the rand, so a set's
# variants differ in placement and count but not in what they are.

def flowers(petal, centre, kind="daisy", shade=None):
    """One clump: three to five flowers of a kind standing in their own foliage."""
    def draw(c, r):
        cx, cy = spot(r, 15)
        n = r.i(3, 5)
        foliage(c, cx, cy + 9, 7, r)
        for k in range(n):
            x = cx + r.i(-8, 8)
            y = cy + r.i(-6, 3)
            if kind == "daisy":
                daisy(c, x, y, petal, centre, r.i(3, 6), shade)
            elif kind == "bud":
                bud(c, x, y - 2, petal, centre, r.i(3, 5), shade)
            else:
                bell(c, x - 5, y, petal, centre)
    return draw


def wildflowers(c, r):
    """A mixed clump - the one set that does not look planted."""
    cx, cy = spot(r, 15)
    palette = [(SK[1], GO[1], SK[2]), (GO[1], GO[2], GO[2]), (VI[0], GO[1], VI[1]),
               (FL[1], GO[1], FL[2]), (AR[1], SK[1], AR[2])]
    foliage(c, cx, cy + 9, 8, r)
    for k in range(r.i(4, 6)):
        p, ce, sh = palette[r.i(0, len(palette) - 1)]
        x, y = cx + r.i(-9, 9), cy + r.i(-6, 3)
        if r.f() < 0.65:
            daisy(c, x, y, p, ce, r.i(3, 6), sh)
        else:
            bud(c, x, y - 2, p, ce, r.i(3, 5), sh)


def clover(c, r):
    """Clover: trefoils of three 3x3 leaflets with a pale heart, a few in a patch, one in flower."""
    cx, cy = spot(r, 12)
    for k in range(r.i(3, 5)):
        x, y = cx + r.i(-8, 8), cy + r.i(-6, 6)
        for lx, ly in ((-1, -3), (-3, 0), (1, 0)):
            for dy in range(3):
                for dx in range(3):
                    if (dx, dy) == (2, 2):
                        continue
                    c.set(x + lx + dx, y + ly + dy, VE[2] if dx + dy < 2 else VE[3])
            c.set(x + lx + 1, y + ly + 1, VE[1] if k % 2 else VE[2])
        c.set(x + 1, y + 3, VE[4])
        c.set(x + 1, y + 4, VE[4])
        c.set(x, y + 4, OCC)
        if k == 0 and r.f() < 0.5:
            # a clover head: a small pinkish-white puff on its own stalk
            hx, hy = x + 4, y - 6
            stem(c, hx, hy + 6, 4)
            for dx, dy in ((0, -1), (-1, 0), (0, 0), (1, 0), (0, 1), (-1, -1), (1, -1)):
                c.set(hx + dx, hy + dy, FL[1] if dy < 0 else FL[2])


def tufts(c, r):
    cx, cy = spot(r, 11)
    for k in range(r.i(2, 3)):
        tuft(c, cx + r.i(-7, 7), cy + r.i(-3, 5), LI[2] if k % 2 else VE[3], LI[1] if k % 2 else VE[2], LI[4])


def dry_grass(c, r):
    cx, cy = spot(r, 11)
    for k in range(r.i(2, 3)):
        tuft(c, cx + r.i(-7, 7), cy + r.i(-3, 5), GO[3], GO[2], GO[4])


def leaves(c, r):
    cx, cy = spot(r, 11)
    for k in range(r.i(3, 4)):
        body = (GO[3], FL[3], RD[2], GO[2])[r.i(0, 3)]
        leaf(c, cx + r.i(-8, 8), cy + r.i(-7, 7), body, FL[4], r.i(0, 1))


def twigs(c, r):
    """A fallen branch: a two-pixel limb with a fork and a twig off it, lit along its top."""
    x, y = spot(r, 12)
    x -= 8
    run = r.i(14, 20)
    slope = r.f() * 0.5 - 0.25
    for j in range(run):
        yy = int(y + j * slope)
        c.set(x + j, yy, GO[3] if j % 5 else GO[2])
        c.set(x + j, yy + 1, GO[4])
        if j % 2 == 0 and j < run - 2:
            c.set(x + j, yy + 2, OCC)
        if j == run // 2:
            for k in range(1, 5):
                c.set(x + j + k, yy - k, GO[3])
        if j == run // 3:
            for k in range(1, 4):
                c.set(x + j + k, yy + 1 + k, GO[4])


def pebbles(c, r):
    cx, cy = spot(r, 11)
    for k in range(r.i(2, 3)):
        pebble(c, cx + r.i(-8, 8), cy + r.i(-6, 6), k == 0)


def rubble(c, r):
    cx, cy = spot(r, 11)
    for k in range(r.i(3, 5)):
        pebble(c, cx + r.i(-9, 9), cy + r.i(-7, 7), r.f() < 0.4)


def mushroom_ring(c, r):
    """A fairy ring: six to eight caps round a circle, a couple missing, on darker grass."""
    cx, cy = SIZE // 2 + r.i(-2, 2), SIZE // 2 + r.i(-1, 2)
    rad = r.i(13, 15)
    n = r.i(7, 9)
    red = r.f() < 0.4
    # the darker, greener grass a ring grows on
    for k in range(40):
        a = r.f() * TAU
        c.set(int(cx + math.cos(a) * (rad - 1)), int(cy + math.sin(a) * (rad - 1) * 0.75), VE[4])
    pts = []
    for k in range(n):
        if r.f() < 0.15:
            continue
        a = k * TAU / n + r.f() * 0.25
        pts.append((int(cx + math.cos(a) * rad), int(cy + math.sin(a) * rad * 0.75)))
    for x, y in sorted(pts, key=lambda p: p[1]):
        if red:
            mushroom(c, x, y, RD[1], RD[0], dots=True, shade=RD[2])
        else:
            mushroom(c, x, y, FL[2], FL[1], shade=FL[3])


def mushrooms(c, r):
    """A cluster: one big toadstool and one or two small ones beside it, sorted back to front."""
    cx, cy = spot(r, 13)
    red = r.f() < 0.5
    pts = [(cx, cy, True)] + [(cx + r.i(5, 9) * (1 if k else -1), cy + r.i(-4, 3), False)
                               for k in range(r.i(1, 2))]
    for x, y, big in sorted(pts, key=lambda p: p[1]):
        if red:
            mushroom(c, x, y, RD[1], RD[0], dots=True, shade=RD[2], big=big)
        else:
            mushroom(c, x, y, GO[2], GO[1], shade=GO[3], big=big)


def mossy_stone(c, r):
    x, y = spot(r, 12)
    w = r.i(5, 7)
    for dy in range(-4, 2):
        for dx in range(-w, w + 1):
            if abs(dx) >= w - 1 and dy in (-4, 1) or (abs(dx) == w and dy in (-3, 0)):
                continue
            tone = ST[2]
            if dy <= -3 or dx <= -w + 1:
                tone = ST[1]
            elif dy == 1 or dx >= w - 1:
                tone = ST[3]
            c.set(x + dx, y + dy, tone)
    for dx in range(-w + 1, w + 1):
        c.set(x + dx, y + 2, OCC)
    # a cap of moss on the lit top, hanging down one side
    for dx in range(-w + 2, r.i(1, w - 1)):
        c.set(x + dx, y - 4, VE[3])
        c.set(x + dx, y - 3, VE[2] if dx % 3 else VE[3])
    for dy in range(-2, 1):
        c.set(x - w + 1, y + dy, VE[3] if dy % 2 else VE[4])


def lilypad(flower):
    """Lily pads: one big pad 18-22 px across with its notch and veins, and smaller ones by it."""
    def draw(c, r):
        n = r.i(1, 3)
        cx, cy = spot(r, 15)
        for k in range(n):
            x = cx + (r.i(-9, 9) if k else 0)
            y = cy + (r.i(-8, 8) if k else 0)
            rad = r.i(9, 11) if k == 0 else r.i(5, 7)
            notch = r.f() * TAU
            for dy in range(-rad, rad + 1):
                for dx in range(-rad - 1, rad + 2):
                    if (dx * 0.85) ** 2 + (dy * 1.1) ** 2 > rad * rad:
                        continue
                    a = math.atan2(dy, dx)
                    off = abs(((a - notch + math.pi) % TAU) - math.pi)
                    if off < 0.32 and dx * dx + dy * dy > 1:
                        continue
                    tone = VE[3]
                    rr = (dx * 0.85) ** 2 + (dy * 1.1) ** 2
                    if dx + dy < -rad * 0.6 and rr > (rad - 2) ** 2:
                        tone = VE[2]
                    elif dx + dy > rad * 0.6 and rr > (rad - 2) ** 2:
                        tone = VE[4]
                    c.set(x + dx, y + dy, tone)
            # veins from the heart, in the darker tone
            if rad >= 9:
                for v in range(5):
                    a = notch + math.pi + (v - 2) * 0.9
                    for j in range(2, rad - 2):
                        c.set(int(round(x + math.cos(a) * j)), int(round(y + math.sin(a) * j * 0.9)), VE[4])
                c.set(x - rad // 2, y - rad // 2, VE[1])
            # the pad's shadow on the water under its lower-right rim
            for j in range(-rad // 2, rad // 2):
                c.set(x + j + rad // 2, y + int(rad / 1.1) + 1 - abs(j) // 3, OCC)
            if flower and k == 0:
                # a water lily: a pale cup of petals with a gold heart, on the lit side of the pad
                fx, fy = x - 2, y - 2
                for dx, dy in ((0, -2), (-2, 0), (2, 0), (0, 2), (-1, -1), (1, -1), (-1, 1), (1, 1),
                               (-1, -2), (1, -2), (-2, -1), (2, -1)):
                    c.set(fx + dx, fy + dy, SK[1] if dy < 0 else SK[2])
                c.set(fx, fy, GO[1])
                c.set(fx + 1, fy, GO[2])
                c.set(fx, fy - 1, FL[0] if r.f() < 0.5 else SK[1])
    return draw


def reeds(c, r):
    """Cattails at a shoreline: a bed of stems and blades with fat brown heads, in the shallows.

    Brown heads in the gold row's deeps - they were mauve (flesh) and read as pink buds."""
    cx, cy = spot(r, 13)
    stems = []
    for k in range(r.i(5, 7)):
        stems.append((cx + r.i(-10, 10), cy + r.i(-2, 7), r.i(14, 22), r.i(-3, 3), k))
    for x, y, h, lean, k in sorted(stems, key=lambda s: s[1]):
        for j in range(h):
            t = j / float(h)
            sx = x + int(round(lean * t * t))
            c.set(sx, y - j, VE[3] if j % 5 else VE[2])
        hx, hy = x + lean, y - h
        if k % 3 != 2:
            # the cattail: 2 wide, 5 tall, lit on its left, a spike above
            for j in range(5):
                c.set(hx, hy - j, GO[3] if j else GO[4])
                c.set(hx + 1, hy - j, GO[4])
            c.set(hx, hy - 3, GO[2])
            c.set(hx, hy - 5, VE[3])
            c.set(hx, hy - 6, VE[3])
        else:
            # a blade, arching away
            side = 1 if k % 2 else -1
            for j in range(9):
                c.set(hx + (j // 3) * side, hy - j, VE[2] if j < 6 else VE[1])
        c.set(x, y + 1, OCC)
        c.set(x + 1, y + 1, OCC)


def bog_moss(c, r):
    cx, cy = spot(r, 9)
    for k in range(r.i(3, 5)):
        x, y = cx + r.i(-6, 6), cy + r.i(-5, 5)
        rad = r.i(1, 3)
        for dy in range(-rad, rad + 1):
            for dx in range(-rad, rad + 1):
                if dx * dx + dy * dy <= rad * rad:
                    c.set(x + dx, y + dy, VE[2] if dx + dy < -rad * 0.6 else VE[4])


def cracks(c, r):
    x, y = spot(r, 12)
    crackline(c, x, y, r.i(9, 15), OCC, r)
    if r.f() < 0.6:
        crackline(c, x, y, r.i(4, 8), OCC, r)


def cracked_earth(c, r):
    x, y = spot(r, 12)
    for k in range(3):
        crackline(c, x, y, r.i(6, 11), FL[4], r)


def moss_seam(c, r):
    x, y = spot(r, 10)
    for k in range(r.i(5, 9)):
        c.set(x + k, y + int(math.sin(k * 0.9) * 1.2), LI[4] if k % 2 else LI[3])
        if r.f() < 0.35:
            c.set(x + k, y - 1, VE[4])


def bone_bits(c, r):
    x, y = spot(r, 10)
    for k in range(r.i(1, 2)):
        bx, by = x + r.i(-5, 5), y + r.i(-4, 4)
        run = r.i(3, 5)
        for j in range(run):
            c.set(bx + j, by, SK[3])
        c.set(bx, by - 1, SK[3])
        c.set(bx + run - 1, by + 1, SK[3])
        c.set(bx, by + 1, OCC)


def snow(c, r):
    """A drift of snow: three overlapping lobes, dithered at the rim so it settles onto the floor.

    One solid grey ellipse read as a boulder. A drift is lumpy and its edge is broken, and it is
    lit on its upper-left lobe like everything else.
    """
    cx, cy = spot(r, 13)
    for k in range(3):
        lx, ly = cx + r.i(-6, 6), cy + r.i(-3, 3)
        rx, ry = r.i(4, 7), r.i(2, 4)
        for dy in range(-ry, ry + 1):
            for dx in range(-rx, rx + 1):
                d = (dx / float(rx)) ** 2 + (dy / float(ry)) ** 2
                if d > 1.0:
                    continue
                if d > 0.45 and (dx + dy + k) % 2:
                    continue
                if d > 0.8 and (dx * 3 + dy) % 3:
                    continue
                tone = SK[3]
                if d < 0.4 and dx + dy < 0:
                    tone = SK[2]
                c.set(lx + dx, ly + dy, tone)


def frost(c, r):
    cx, cy = spot(r, 9)
    for k in range(r.i(3, 6)):
        x, y = cx + r.i(-7, 7), cy + r.i(-6, 6)
        c.set(x, y, STL[2])
        c.set(x, y - 1, STL[1] if k % 2 else STL[2])
        c.set(x - 1, y, STL[3])
        c.set(x + 1, y, STL[3])


def ice_shards(c, r):
    cx, cy = spot(r, 9)
    for k in range(r.i(2, 4)):
        x, y = cx + r.i(-6, 6), cy + r.i(-5, 5)
        h = r.i(2, 4)
        for j in range(h):
            c.set(x, y - j, STL[1] if j == h - 1 else STL[2])
            c.set(x + 1, y - j, STL[3])
        c.set(x, y + 1, OCC)
        c.set(x + 1, y + 1, OCC)


def ash(c, r):
    cx, cy = spot(r, 10)
    for k in range(40):
        a = r.f() * TAU
        d = r.f() ** 0.6 * 9
        x, y = int(cx + math.cos(a) * d), int(cy + math.sin(a) * d * 0.6)
        c.set(x, y, LN[3] if r.f() < 0.4 else ST[2])


def obsidian(c, r):
    cx, cy = spot(r, 9)
    for k in range(r.i(2, 4)):
        x, y = cx + r.i(-6, 6), cy + r.i(-5, 5)
        c.set(x, y, VI[4])
        c.set(x + 1, y, VI[3])
        c.set(x, y - 1, VI[2])
        c.set(x + 1, y - 1, VI[4])
        c.set(x, y + 1, OCC)
        if k == 0:
            c.set(x - 1, y - 1, VI[1])


# set name -> (draw, variants). Names are what GroundDetail's rules ask for.
SETS = [
    ("flowers_white", flowers(SK[1], GO[1], "daisy", SK[2]), 4),
    ("flowers_yellow", flowers(GO[1], GO[0], "daisy", GO[2]), 4),
    ("flowers_violet", flowers(VI[0], GO[1], "daisy", VI[1]), 4),
    ("flowers_pink", flowers(FL[1], FL[0], "bud", FL[2]), 4),
    ("flowers_blue", flowers(AR[1], AR[0], "bell"), 4),
    ("flowers_red", flowers(RD[0], OCC, "bud", RD[1]), 3),
    ("flowers_mixed", wildflowers, 4),
    ("clover", clover, 4),
    ("tufts", tufts, 4),
    ("dry_grass", dry_grass, 3),
    ("leaves", leaves, 4),
    ("twigs", twigs, 3),
    ("pebbles", pebbles, 4),
    ("rubble", rubble, 3),
    ("mushroom_ring", mushroom_ring, 3),
    ("mushrooms", mushrooms, 3),
    ("mossy_stone", mossy_stone, 3),
    ("lilypad", lilypad(False), 4),
    ("lilypad_flower", lilypad(True), 3),
    ("reeds", reeds, 4),
    ("bog_moss", bog_moss, 3),
    ("cracks", cracks, 4),
    ("cracked_earth", cracked_earth, 3),
    ("moss_seam", moss_seam, 3),
    ("bone_bits", bone_bits, 3),
    ("snow", snow, 4),
    ("frost", frost, 3),
    ("ice_shards", ice_shards, 3),
    ("ash", ash, 3),
    ("obsidian", obsidian, 3),
]


# --- the flower pen ------------------------------------------------------------------------------
#
# A walled garden: a wattle-and-post fence round a plot twelve cells by nine, two gates, a path
# from one to the other round a central bed, four flower beds, and one moonflower in the middle
# with a cold light on the soil round it.
#
# REDRAWN BIGGER, THE SECOND TIME (2026-10). The first pen was a doormat; the second was 204 x 140
# inside a 20 px fence and the wizard could walk straight through it. Reported: "fence with the
# flower should be much bigger area, I should not be able to walk directly through the fence."
# So the plot is 576 x 432 world pixels now - a place you walk INTO - and the fence is solid: its
# rails stop the boots on the ground's physics layer, and the flow field sends the horde round to
# a gate (Node2DGame.CreateFlowerPen, .ai/terrain.md section 8).
#
# TWO SCALES, AND WHY. What stands up - rails, posts, the moonflower - is drawn at x2 like every
# other world sprite, so a post is as chunky as a tree trunk next to it. What lies on the ground -
# the beds, the path, the soil - is drawn 1:1 like the ground tiles and the detail layer it sits
# among, so a bed of daisies matches the daisies outside the fence.
#
# THE LAYOUT IS SHARED WITH THE GAME. PEN_CELLS_W, PEN_CELLS_H, GATE_X and GATE_W are also
# FlowerPenLayout's constants in scripts/Landmark.cs - the cells the fence blocks in the navigation
# grid and where its collision runs - and `flower-pen.json` carries them so
# RegressionChecks.ValidateGroundArt can hold the two against each other. The fence's ground line
# runs down the MIDDLE of the ring of cells round the plot; the gates are whole cells.

CELL = 48                  # world pixels per cell
PEN_CELLS_W = 12
PEN_CELLS_H = 9
GATE_X = 5                 # the first gate cell, in the north and the south rail alike
GATE_W = 2
PEN_W = PEN_CELLS_W * CELL         # 576, world
PEN_H = PEN_CELLS_H * CELL         # 432, world
LINE = CELL // 2                   # the fence's ground line, in from the plot's edge (world)
RAIL_H = 20                # authored px the fence stands (x2: 40 world - half the wizard)
POST_EVERY = 12            # authored px between posts: two to a cell
SEG = CELL // 2            # authored length of one side segment: one cell
STAND = 2                  # the standing parts' render scale


def post(c, x, base, h):
    """A split-log post, 3 px wide: lit face left, shade right, a pale cut top, its shadow. Weathered
    wood, a stop down from the old gold-lit posts: at 40 px tall and fifty to a garden, gold read as
    a brass railing rather than as wattle."""
    for dx in range(-1, 4):
        c.set(x + dx, base + 1, OCC)
    for k in range(h):
        c.set(x, base - k, GO[2] if k < h - 1 else GO[1])
        c.set(x + 1, base - k, GO[3])
        c.set(x + 2, base - k, GO[4])
    c.set(x, base - h, FL[2])
    c.set(x + 1, base - h, GO[2])
    c.set(x + 2, base - h, GO[3])
    # a knot, and a lashing where the withies are tied on
    c.set(x + 1, base - h // 2, OCC)
    c.set(x, base - RAIL_H + 9, GO[3])


def withy(c, x0, x1, y, phase):
    """One woven rail: a two-pixel withy that dips behind every post and rises between them."""
    for x in range(x0, x1 + 1):
        t = ((x - x0 + phase) % POST_EVERY) / float(POST_EVERY)
        wob = int(round(math.sin(t * TAU) * 1.2))
        c.set(x, y + wob, GO[3] if x % 4 else GO[2])
        c.set(x, y + wob + 1, GO[4])
        c.set(x, y + wob + 2, OCC if x % 3 else None)


def wattle_run(c, x0, x1, base):
    """A horizontal stretch of fence: posts every POST_EVERY px, two woven withies between them."""
    withy(c, x0, x1, base - RAIL_H + 4, 0)
    withy(c, x0, x1, base - RAIL_H + 11, POST_EVERY // 2)
    for x in range(x0, x1 + 1):
        if x % 2 == 0:
            c.set(x, base + 1, OCC)
    for x in range(x0, x1 - 2, POST_EVERY):
        post(c, x - 1, base, RAIL_H)
    post(c, x1 - 1, base, RAIL_H)


def gate_post(c, x, base):
    """Taller and capped: a way in, and it should say so from across the screen."""
    post(c, x, base, RAIL_H + 6)
    c.set(x - 1, base - RAIL_H - 6, GO[2])
    c.hline(x - 1, x + 3, base - RAIL_H - 7, GO[1])
    c.set(x + 3, base - RAIL_H - 6, GO[3])


def rail_canvas():
    w = PEN_W // STAND
    h = RAIL_H + 12
    return raster.Canvas(w, h, None), h - 4


def gaps():
    """The gate, in authored x: from the edge of the first gate cell to the far edge of the last."""
    return GATE_X * CELL // STAND, (GATE_X + GATE_W) * CELL // STAND


def pen_back():
    """The north rail with its back gate - two capped posts and nothing between them."""
    c, base = rail_canvas()
    g0, g1 = gaps()
    west, east = LINE // STAND, (PEN_W - LINE) // STAND
    wattle_run(c, west, g0 - 1, base)
    wattle_run(c, g1, east, base)
    gate_post(c, g0 - 3, base)
    gate_post(c, g1, base)
    return c, base


def pen_front():
    """The south rail with the front gate, its hurdle swung open against the left post."""
    c, base = rail_canvas()
    g0, g1 = gaps()
    west, east = LINE // STAND, (PEN_W - LINE) // STAND
    wattle_run(c, west, g0 - 1, base)
    wattle_run(c, g1, east, base)
    gate_post(c, g0 - 3, base)
    gate_post(c, g1, base)
    # the hurdle, seen nearly edge-on as it stands open into the garden: a frame and two bars
    for k in range(RAIL_H - 3):
        c.set(g0 + 1, base - 2 - k, GO[2])
        c.set(g0 + 2, base - 2 - k, GO[3])
    for k in range(8):
        for y in (base - 4, base - 11, base - RAIL_H + 2):
            c.set(g0 + 2 + k, y - k // 2, GO[2] if k % 3 else GO[1])
            c.set(g0 + 2 + k, y - k // 2 + 1, GO[4])
    c.vline(g0 + 9, base - 6 - RAIL_H + 6, base - 6, GO[3])
    return c, base


def pen_side():
    """One cell of a side rail, the fence seen running away from the camera: the withies are two
    lines climbing the cell, the posts stand at its middle and its foot. Node2DGame places one per
    cell down each side, each sorted at its own foot, so the horde and the wizard pass in front of
    the posts south of them and behind the ones north of them."""
    w = 8
    h = SEG + RAIL_H + 4
    c = raster.Canvas(w, h, None)
    foot = h - 3                      # the segment's south end, its anchor
    top = foot - SEG                  # its north end
    x = 3
    for g in range(top, foot + 1):
        c.set(x - 1, g - RAIL_H + 5, GO[2] if g % 3 else GO[3])
        c.set(x, g - RAIL_H + 5, GO[3])
        c.set(x + 1, g - RAIL_H + 5, GO[4])
        c.set(x - 1, g - RAIL_H + 12, GO[3])
        c.set(x, g - RAIL_H + 12, GO[3] if g % 2 else GO[4])
        c.set(x + 1, g - RAIL_H + 12, GO[4])
        if g % 2:
            c.set(x + 2, g, OCC)
    post(c, x - 1, top + SEG // 2, RAIL_H)
    post(c, x - 1, foot, RAIL_H)
    return c, foot


def moonflower():
    """The special flower: a tall stem, broad leaves, a pale bloom with a cold light at its heart.

    The only emissive thing a landmark carries, and it is the ICE ramp on purpose: the XP orbs are
    `grass`, `arcane` and `light`, and nothing on the ground may look like one. Drawn at x2 now, so
    it stands a little taller than the wizard (92 px against his 80): it is what the garden is for.
    """
    w, h = 34, 48
    c = raster.Canvas(w, h, None)
    ice = bl.ELEMENTS["ice"]
    x = w // 2
    base = h - 3
    for dx in range(-3, 4):
        c.set(x + dx, base + 1, OCC)
    for k in range(28):
        c.set(x - 1, base - k, VE[2] if k % 5 else VE[1])
        c.set(x, base - k, VE[3])
    for side, y0, ln in ((-1, base - 4, 10), (1, base - 10, 9), (-1, base - 16, 7)):
        for k in range(ln):
            ly = y0 - int(k * 0.55) + (k * k) // 30
            lx = x + side * (1 + k)
            wid = 3 if 1 < k < ln - 2 else 2
            for j in range(wid):
                c.set(lx, ly + j, VE[1] if j == 0 and k < ln - 2 else (VE[3] if j < wid - 1 else VE[4]))
    cy = base - 34
    for dy in range(-8, 9):
        for dx in range(-10, 11):
            d = (dx / 10.0) ** 2 + (dy / 7.5) ** 2
            if d > 1.0:
                continue
            ang = math.atan2(dy, dx)
            if 0.5 < d and abs(math.sin(ang * 2.5)) < 0.26:
                continue
            tone = SK[1] if dx + dy < 0 else SK[2]
            if d > 0.62 and dx + dy > 5:
                tone = SK[3]
            if d < 0.5 and abs(math.sin(ang * 2.5)) < 0.15:
                tone = SK[2]
            c.set(x + dx, cy + dy, tone)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            dd = dx * dx + dy * dy
            if dd > 5:
                continue
            c.set(x + dx, cy + dy, ice[0] if dd <= 1 else ice[1])
    c.set(x - 6, cy - 5, SK[0])
    c.set(x - 5, cy - 6, SK[0])
    for mx, my, t in ((-12, -10, 1), (10, -12, 2), (-4, -14, 2), (13, -4, 3), (-14, -2, 3)):
        c.set(x + mx, cy + my, ice[t])
    return c, base


def garden():
    """Everything inside the fence that lies on the ground, 1:1 in world pixels: the path from
    gate to gate round the central bed, the bed itself, four flower beds, and the moonflower's
    light on the soil. Clear everywhere else, so the turf runs on under the fence."""
    c = raster.Canvas(PEN_W, PEN_H, None)
    r = Rand(4242)
    cx, cy = PEN_W // 2, PEN_H // 2
    g0, g1 = GATE_X * CELL, (GATE_X + GATE_W) * CELL
    pw = 20                                                # half the path's width

    def earth(x, y, edge):
        """Packed earth, a step from the turf: never a hole, never a stripe."""
        h = (x * 7 + y * 13 + (x * y) % 7) % 11
        tone = LN[3] if h > 2 else LN[4]
        if edge and (x + y) % 2:
            return None
        c.set(x, y, tone)

    # the path: up from the front gate and down from the back one, and a ring round the bed
    ring_rx, ring_ry = 92, 64
    for y in range(LINE - 6, PEN_H - LINE + 8):
        for x in range(cx - pw - 2, cx + pw + 3):
            d = ((x - cx) / float(ring_rx)) ** 2 + ((y - cy) / float(ring_ry)) ** 2
            if d < 1.0:
                continue
            earth(x, y, abs(x - cx) > pw - 1)
    for y in range(cy - ring_ry - 12, cy + ring_ry + 13):
        for x in range(cx - ring_rx - 12, cx + ring_rx + 13):
            d = math.sqrt(((x - cx) / float(ring_rx)) ** 2 + ((y - cy) / float(ring_ry)) ** 2)
            if 0.76 < d < 1.0:
                earth(x, y, d < 0.79 or d > 0.97)
    # stepping stones down the middle of the path
    for y in range(LINE + 8, PEN_H - LINE, 22):
        d = ((cy - y) / float(ring_ry)) ** 2
        if d < 1.2:
            continue
        pebble(c, cx + r.i(-4, 4), y + r.i(-2, 2), True)
    for k in range(18):
        a = k * TAU / 18 + 0.2
        pebble(c, int(cx + math.cos(a) * ring_rx * 0.88), int(cy + math.sin(a) * ring_ry * 0.88), k % 3 == 0)

    # the central bed: furrowed earth in an oval, lit on its far lip
    bw, bh = 62, 40
    for y in range(cy - bh, cy + bh + 1):
        for x in range(cx - bw, cx + bw + 1):
            d = ((x - cx) / float(bw)) ** 2 + ((y - cy) / float(bh)) ** 2
            if d > 1.0:
                continue
            if d > 0.88 and (x + y) % 2:
                continue
            tone = LN[4] if (y - cy) % 5 in (0, 1) else FL[4]
            if (y - cy) % 5 == 4:
                tone = GO[4]
            c.set(x, y, tone)
    for k in range(160):
        a = k * TAU / 160
        c.set(int(round(cx + math.cos(a) * (bw + 1))), int(round(cy + math.sin(a) * (bh + 1))),
              GO[3] if math.sin(a) < 0 else OCC)
    # the moonflower's light, as cold motes on the soil round it - not a pool, which read as a puddle
    ice = bl.ELEMENTS["ice"]
    for k in range(150):
        a = r.f() * TAU
        d = r.f() ** 0.7
        c.set(int(cx + math.cos(a) * d * 46), int(cy + 6 + math.sin(a) * d * 26), ice[3] if d > 0.5 else ice[2])

    # four beds, each edged with a plank, in rows. Each has its own planting so the four corners
    # are not one bed stamped four times.
    beds = [
        (52, 54, 226, 146, [(SK[1], GO[1], SK[2]), (VI[0], GO[1], VI[1])], "daisy"),
        (350, 54, 524, 146, None, "cabbage"),
        (52, 290, 226, 380, [(FL[1], GO[1], FL[2]), (GO[1], GO[2], GO[2])], "bud"),
        (350, 290, 524, 380, [(AR[2], AR[1], AR[3])], "bell"),
    ]
    for (x0, y0, x1, y1, colours, kind) in beds:
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                tone = FL[4] if (y - y0) % 6 in (0, 1, 2) else LN[4]
                if (y - y0) % 6 == 5:
                    tone = GO[4]
                c.set(x, y, tone)
        c.hline(x0 - 1, x1 + 1, y0 - 2, GO[2])
        c.hline(x0 - 1, x1 + 1, y0 - 1, GO[3])
        c.hline(x0 - 1, x1 + 1, y1 + 1, GO[3])
        c.hline(x0 - 1, x1 + 1, y1 + 2, OCC)
        c.vline(x0 - 2, y0 - 2, y1 + 2, GO[3])
        c.vline(x1 + 2, y0 - 2, y1 + 2, GO[4])
        rows = (y0 + 20, y0 + 50, y0 + 80) if y1 - y0 > 85 else (y0 + 20, y0 + 50)
        for ri, ry in enumerate(rows):
            for k, x in enumerate(range(x0 + 12 + (ri % 2) * 7, x1 - 8, 17)):
                if kind == "cabbage":
                    cabbage(c, x, ry, r)
                    continue
                p, ce, sh = colours[(k + ri) % len(colours)]
                foliage(c, x, ry + 8, 5, r)
                for j in range(3):
                    fx, fy = x + (j - 1) * 4 + r.i(-1, 1), ry + r.i(-3, 2)
                    if kind == "daisy":
                        daisy(c, fx, fy, p, ce, r.i(3, 5), sh)
                    elif kind == "bud":
                        bud(c, fx, fy - 2, p, ce, r.i(3, 5), sh)
                    elif j == 0:
                        bell(c, fx - 5, fy, p, ce)
    # herbs in the four corners of the plot, and tufts along the inside of the fence
    for (hx, hy) in ((40, 34), (PEN_W - 46, 34), (40, PEN_H - 50), (PEN_W - 46, PEN_H - 50)):
        foliage(c, hx, hy + 8, 6, r)
        for j in range(4):
            tuft(c, hx + r.i(-6, 6), hy + r.i(0, 6), VE[3], VE[2], OCC)
    return c


def cabbage(c, x, y, r):
    """A vegetable bed's head: a round of leaves, lit upper left, its outer leaves splayed."""
    for dy in range(-5, 6):
        for dx in range(-6, 7):
            d = (dx / 6.0) ** 2 + (dy / 5.0) ** 2
            if d > 1.0:
                continue
            tone = VE[2] if dx + dy < -2 else (VE[3] if d < 0.7 else VE[4])
            if d < 0.25:
                tone = VE[1] if dx + dy < 0 else VE[2]
            c.set(x + dx, y + dy, tone)
    for dx in (-7, 7):
        c.set(x + dx, y + 1, VE[3])
        c.set(x + dx, y + 2, VE[4])
    for dx in range(-5, 6):
        c.set(x + dx, y + 6, OCC)


# (name, painter, scale). The painters return (canvas, ground row) - the row each sprite is
# anchored at - except the garden, which is a ground decal placed at the plot's corner.
LANDMARKS = [
    ("flower-pen-back", pen_back, STAND),
    ("flower-pen-front", pen_front, STAND),
    ("flower-pen-side", pen_side, STAND),
    ("flower-pen-flower", moonflower, STAND),
    ("flower-pen-garden", lambda: (garden(), 0), 1),
]


def main():
    for d in (OUT_DETAIL, OUT_LANDMARK):
        if not os.path.isdir(d):
            os.makedirs(d)

    ok = True
    frames = []
    for name, draw, variants in SETS:
        for v in range(variants):
            c = raster.Canvas(SIZE, SIZE, None)
            draw(c, Rand(hash_name(name) * 31 + v * 7919 + 1))
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s_%d: %s" % (name, v, bad[:4]))
                continue
            tid = "%s_%d" % (name, v)
            c.save(os.path.join(OUT_DETAIL, tid + ".png"))
            frames.append('  {"id": "%s", "set": "%s", "path": "assets/bonelight/tiles/detail/%s.png"}'
                          % (tid, name, tid))
        print("  %-16s %d variants" % (name, variants))

    pen = []
    for name, draw, scale in LANDMARKS:
        c, ground = draw()
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:4]))
            continue
        (c.scaled(scale) if scale > 1 else c.img).save(os.path.join(OUT_LANDMARK, name + ".png"))
        key = name.replace("flower-pen-", "")
        # anchor_x: the column that stands on the fence line (a side segment's posts) or the
        # plant's stem (the flower); the rails and the garden are placed by their left edge.
        anchor = {"side": 3 * scale + 1, "flower": (c.w // 2) * scale}.get(key, 0)
        pen.append('    "%s": {"path": "res://assets/bonelight/world/landmarks/%s.png", "width": %d, '
                   '"height": %d, "ground": %d, "anchor_x": %d}'
                   % (key, name, c.w * scale, c.h * scale, ground * scale, anchor))
        print("  landmark %s %dx%d on screen" % (name, c.w * scale, c.h * scale))
    # The layout the game must agree with (scripts/Landmark.cs, FlowerPenLayout) and each sprite's
    # ground row in pixels of its PNG, which is where Node2DGame anchors and sorts it.
    pen_manifest = ('{\n'
                    '  "_comment": "Generated by tools/art/ground_detail.py. cells, gate and line are the '
                    'layout FlowerPenLayout in scripts/Landmark.cs must match; ground is the anchor row '
                    'of each sprite in pixels of its PNG.",\n'
                    '  "cells_w": %d, "cells_h": %d, "gate_x": %d, "gate_w": %d, "line": %d, "segment": %d,\n'
                    '%s\n}\n' % (PEN_CELLS_W, PEN_CELLS_H, GATE_X, GATE_W, LINE, SEG * STAND, ",\n".join(pen)))
    io.open(os.path.join(OUT_LANDMARK, "flower-pen.json"), "w", encoding="utf-8",
            newline="\n").write(pen_manifest.decode("utf-8"))

    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/ground_detail.py - small things on the ground, '
                'one 48x48 cell each, painted by LevelTilePainter.PaintChunkDetail where GroundDetail '
                'says.",\n'
                '  "frames": [\n%s\n  ]\n}\n' % ",\n".join(frames))
    io.open(os.path.join(OUT_DETAIL, "detail.json"), "w", encoding="utf-8",
            newline="\n").write(manifest.decode("utf-8"))
    # The detail tiles are drawn from the same packed texture as the ground.
    tile_atlas.main()
    print("")
    print("%d detail tiles in %d sets, %d landmark sprites%s"
          % (len(frames), len(SETS), len(LANDMARKS), "" if ok else " - WITH PALETTE ERRORS"))
    return 0 if ok else 1


def hash_name(s):
    h = 0
    for ch in s:
        h = (h * 131 + ord(ch)) & 0xFFFFFF
    return h


if __name__ == "__main__":
    sys.exit(main())
