# -*- coding: utf-8 -*-
"""The small things lying on the ground, and the first landmark. Run: python tools/art/ground_detail.py

Two outputs:

  DETAIL      48x48 transparent tiles - flowers, clover, lilypads, reeds, pebbles, a mushroom
              ring, cracks, snow - in `assets/bonelight/tiles/detail/`, with `detail.json`.
              LevelTilePainter paints them into one extra TileMapLayer per streamed chunk, and
              GroundDetail decides which goes on which cell.

  LANDMARKS   the flower pen: a little wattle fence round one glowing moonflower, in three
              sprites (`assets/bonelight/world/landmarks/`) so the fence's front rail can sort in
              front of the player and the back rail behind him. Node2DGame places it, rarely.

WHY TILES AND NOT SPRITES. The ask was "a lot more fun little environment elements", and a lot
is the problem: decor is a StaticBody2D, a CollisionShape2D and a Sprite2D per item, and a
hundred flowers a screen at three nodes each, on a phone, streamed in and out with the chunks,
is exactly the node count `.ai/terrain.md` section 7 measured flat and wants to keep flat. A
TileMapLayer draws its cells in batched quadrants and costs ONE node a chunk however many cells
it holds. The price is that a detail sits on the 48 px grid - which is why every set has several
variants with the drawing placed at different spots inside the cell, so the grid never shows.

THE RULES FOR GROUND DETAIL, all from `.ai/art-direction.md` and `.ai/world-and-tone.md`:

  - It is never the subject. Tiny (a flower is three to five pixels), drawn in the material rows
    and never in an element's emissive ramp, so a flower cannot be mistaken for a spell, a
    pickup or a telegraph. The XP orbs are `grass`, `arcane` and `light` emissives at 8-12 px;
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

# SIZE. The first pass drew five-pixel flowers, and at camera zoom 0.85 a five-pixel flower is a
# coloured speck - photographed in the Forest, the ground looked empty with dust on it. A head is
# now a 3x3 bloom with a centre, the plants stand 4-14 px tall, and a clump is several of them.
# Still small beside a 28 px enemy, still in material tones; just big enough to be a flower.

def stem(c, x, y, h, tone=None):
    for k in range(h):
        c.set(x, y - k, tone or VE[3])


def leafpair(c, x, y):
    c.set(x - 1, y, VE[2])
    c.set(x - 2, y - 1, VE[3])
    c.set(x + 1, y - 1, VE[3])
    c.set(x + 2, y - 2, VE[2])


def daisy(c, x, y, petal, centre, h=3, shade=None):
    """A 3x3 bloom - centre and eight petals, the lower-right ones in shade - on a stem."""
    stem(c, x, y + 1 + h, h)
    shade = shade or petal
    for dx, dy in ((-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)):
        c.set(x + dx, y + dy, shade if dx + dy > 0 else petal)
    c.set(x, y - 2, petal)
    c.set(x - 2, y, petal)
    c.set(x, y, centre)


def bud(c, x, y, petal, lit, h=3, shade=None):
    """A tulip cup, 3 wide and 3 tall, lit on its upper-left."""
    stem(c, x, y + 2 + h, h)
    shade = shade or petal
    for dx, dy in ((-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1), (0, 2), (-1, -1), (1, -1)):
        c.set(x + dx, y + dy, shade if dx > 0 or dy > 1 else petal)
    c.set(x - 1, y - 1, lit)
    c.set(x - 1, y, lit)


def bell(c, x, y, petal, lit):
    """A drooping bluebell spray: an arched stem with three bells hanging off it."""
    for k in range(7):
        c.set(x + k, y - (1 if 1 < k < 5 else 0), VE[3])
    stem(c, x, y + 6, 6)
    for k in (1, 3, 5):
        dy = 0 if k == 3 else 1
        c.set(x + k, y + 2 - dy, lit if k == 1 else petal)
        c.set(x + k + 1, y + 2 - dy, petal)
        c.set(x + k, y + 3 - dy, petal)
        c.set(x + k + 1, y + 3 - dy, petal)


def tuft(c, x, y, mid, light, dark=None):
    """A tuft of long grass: five blades fanning from one root, the outer ones arching."""
    for j in range(5):
        c.set(x, y - j, mid if j < 4 else light)
    for j in range(4):
        c.set(x - 1 - j // 2, y - 1 - j, mid if j < 3 else light)
        c.set(x + 1 + j // 2, y - 1 - j, mid if j < 3 else light)
    c.set(x - 3, y - 2, light)
    c.set(x + 3, y - 3, light)
    if dark:
        c.set(x, y + 1, dark)
        c.set(x + 1, y + 1, dark)
        c.set(x - 1, y + 1, dark)


def pebble(c, x, y, big):
    """A stone, lit upper-left, on its contact shadow. 3x2, or 5x3 when big."""
    w = 2 if big else 1
    for dx in range(-w, w + 1):
        c.set(x + dx, y + 1, OCC)
        c.set(x + dx, y, ST[3] if dx > 0 else ST[2])
        if abs(dx) < w or not big:
            c.set(x + dx, y - 1, ST[1] if dx <= 0 else ST[2])
    if big:
        c.set(x - 1, y - 2, ST[0])
        c.set(x, y - 2, ST[1])
        c.set(x + 1, y - 2, ST[2])
        c.set(x + w + 1, y + 1, OCC)


def leaf(c, x, y, body, vein, turn):
    """A fallen leaf, 3x5, with its midrib, lying one of two ways."""
    if turn % 2:
        pts = [(0, -2), (-1, -1), (0, -1), (1, -1), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1), (0, 2)]
        rib = [(0, -1), (0, 0), (0, 1), (0, 2)]
    else:
        pts = [(-2, 0), (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 0), (0, 1), (1, -1), (1, 0), (1, 1), (2, 0)]
        rib = [(-1, 0), (0, 0), (1, 0), (2, 0)]
    for dx, dy in pts:
        c.set(x + dx, y + dy, body)
    for dx, dy in rib:
        c.set(x + dx, y + dy, vein)


def mushroom(c, x, y, cap, lit, dots=False, shade=None):
    """A toadstool: a 5-wide cap on a pale stalk, with its shadow."""
    shade = shade or cap
    for dx in (-1, 0, 1):
        c.set(x + dx, y + 1, OCC)
    c.set(x, y, SK[2])
    c.set(x, y - 1, SK[3])
    for dx in range(-2, 3):
        c.set(x + dx, y - 2, shade if dx > 0 else cap)
    for dx in range(-1, 2):
        c.set(x + dx, y - 3, cap if dx >= 0 else lit)
    c.set(x - 1, y - 2, lit)
    if dots:
        c.set(x + 1, y - 3, SK[1])
        c.set(x - 2, y - 2, SK[1])


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
    def draw(c, r):
        n = r.i(5, 8)
        cx, cy = spot(r, 14)
        for k in range(n):
            x = cx + r.i(-11, 11)
            y = cy + r.i(-9, 9)
            if kind == "daisy":
                daisy(c, x, y, petal, centre, r.i(2, 4), shade)
            elif kind == "bud":
                bud(c, x, y, petal, centre, r.i(2, 4), shade)
            else:
                bell(c, x - 3, y, petal, centre)
            if r.f() < 0.6:
                leafpair(c, x, y + 4)
    return draw


def wildflowers(c, r):
    """A mixed clump - the one set that does not look planted."""
    cx, cy = spot(r, 14)
    palette = [(SK[1], GO[1], SK[2]), (GO[1], GO[0], GO[2]), (VI[0], GO[1], VI[1]),
               (FL[1], FL[0], FL[2]), (AR[1], SK[1], AR[2])]
    for k in range(r.i(6, 9)):
        p, ce, sh = palette[r.i(0, len(palette) - 1)]
        x, y = cx + r.i(-12, 12), cy + r.i(-9, 9)
        if r.f() < 0.65:
            daisy(c, x, y, p, ce, r.i(2, 4), sh)
        else:
            bud(c, x, y, p, ce, r.i(2, 3), sh)
        if r.f() < 0.5:
            leafpair(c, x, y + 4)


def clover(c, r):
    """Clover: trefoils of three 2x2 leaflets, a few in a patch."""
    cx, cy = spot(r, 10)
    for k in range(r.i(4, 7)):
        x, y = cx + r.i(-9, 9), cy + r.i(-7, 7)
        for lx, ly in ((0, -2), (-2, 0), (1, 0)):
            c.set(x + lx, y + ly, VE[2] if lx <= 0 and ly < 0 else VE[3])
            c.set(x + lx + 1, y + ly, VE[3])
            c.set(x + lx, y + ly + 1, VE[3])
            c.set(x + lx + 1, y + ly + 1, VE[4])
        c.set(x, y + 2, VE[4])


def tufts(c, r):
    cx, cy = spot(r, 8)
    for k in range(r.i(2, 4)):
        tuft(c, cx + r.i(-6, 6), cy + r.i(-4, 4), LI[2], LI[1] if k % 2 else VE[3], LI[4])


def dry_grass(c, r):
    cx, cy = spot(r, 8)
    for k in range(r.i(2, 4)):
        tuft(c, cx + r.i(-6, 6), cy + r.i(-4, 4), GO[3], GO[2], GO[4])


def leaves(c, r):
    cx, cy = spot(r, 8)
    for k in range(r.i(3, 5)):
        body = (GO[3], FL[3], RD[2], GO[2])[r.i(0, 3)]
        leaf(c, cx + r.i(-7, 7), cy + r.i(-6, 6), body, FL[4], r.i(0, 1))


def twigs(c, r):
    x, y = spot(r, 10)
    run = r.i(7, 12)
    slope = r.f() * 0.6 - 0.3
    for j in range(run):
        c.set(x + j, int(y + j * slope), GO[4])
        if j == run // 2:
            c.set(x + j + 1, int(y + j * slope) - 1, GO[4])
            c.set(x + j + 2, int(y + j * slope) - 2, GO[4])
    c.set(x, int(y) + 1, OCC)


def pebbles(c, r):
    cx, cy = spot(r, 8)
    for k in range(r.i(2, 4)):
        pebble(c, cx + r.i(-6, 6), cy + r.i(-5, 5), k == 0)


def rubble(c, r):
    cx, cy = spot(r, 8)
    for k in range(r.i(3, 6)):
        x, y = cx + r.i(-7, 7), cy + r.i(-6, 6)
        if r.f() < 0.4:
            pebble(c, x, y, True)
        else:
            c.set(x, y, ST[2])
            c.set(x + 1, y, ST[3])
            c.set(x, y + 1, OCC)


def mushroom_ring(c, r):
    """A fairy ring: seven to ten tiny caps round a circle, a couple missing."""
    cx, cy = SIZE // 2 + r.i(-4, 4), SIZE // 2 + r.i(-4, 4)
    rad = r.i(9, 13)
    n = r.i(8, 11)
    red = r.f() < 0.4
    for k in range(n):
        if r.f() < 0.12:
            continue
        a = k * TAU / n + r.f() * 0.25
        x = int(cx + math.cos(a) * rad)
        y = int(cy + math.sin(a) * rad * 0.8)
        if red:
            mushroom(c, x, y, RD[1], RD[0], dots=True)
        else:
            mushroom(c, x, y, FL[3], FL[2])
    # the darker, greener grass a ring grows on
    for k in range(18):
        a = r.f() * TAU
        c.set(int(cx + math.cos(a) * (rad - 2)), int(cy + math.sin(a) * (rad - 2) * 0.8), VE[4])


def mushrooms(c, r):
    cx, cy = spot(r, 9)
    red = r.f() < 0.5
    for k in range(r.i(2, 3)):
        x, y = cx + r.i(-4, 4), cy + r.i(-3, 3)
        if red:
            mushroom(c, x, y, RD[1], RD[0], dots=True)
        else:
            mushroom(c, x, y, GO[2], GO[1])


def mossy_stone(c, r):
    x, y = spot(r, 10)
    w = r.i(3, 5)
    for dy in range(-2, 2):
        for dx in range(-w, w + 1):
            if abs(dx) == w and dy in (-2, 1):
                continue
            tone = ST[2]
            if dy == -2 or dx == -w:
                tone = ST[1]
            elif dy == 1 or dx == w:
                tone = ST[3]
            c.set(x + dx, y + dy, tone)
    for dx in range(-w + 1, w + 1):
        c.set(x + dx, y + 2, OCC)
    for k in range(r.i(3, 6)):
        c.set(x + r.i(-w, w), y - 2 + r.i(0, 1), VE[3] if k % 2 else VE[4])


def lilypad(flower):
    def draw(c, r):
        n = r.i(1, 3)
        cx, cy = spot(r, 13)
        for k in range(n):
            x = cx + (r.i(-10, 10) if k else 0)
            y = cy + (r.i(-8, 8) if k else 0)
            rad = r.i(6, 8) if k == 0 else r.i(4, 5)
            notch = r.f() * TAU
            for dy in range(-rad, rad + 1):
                for dx in range(-rad - 1, rad + 2):
                    if (dx * 0.85) ** 2 + dy * dy > rad * rad:
                        continue
                    a = math.atan2(dy, dx)
                    if abs(((a - notch + math.pi) % TAU) - math.pi) < 0.45 and dx * dx + dy * dy > 1:
                        continue
                    tone = VE[3]
                    if dx + dy < -rad * 0.7:
                        tone = VE[2]
                    elif dx + dy > rad * 0.8:
                        tone = VE[4]
                    c.set(x + dx, y + dy, tone)
            # the water's own shadow under the lower-right rim
            c.set(x + rad, y + 1, AZ[4])
            c.set(x + 1, y + rad, AZ[4])
            if flower and k == 0:
                # a water lily: a pale cup with a warm heart, on the pad's lit side
                fx, fy = x - 1, y - 1
                for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1), (-1, -1), (1, -1)):
                    c.set(fx + dx, fy + dy, SK[1] if dy < 0 else SK[2])
                c.set(fx, fy, GO[1])
                if r.f() < 0.5:
                    c.set(fx - 1, fy - 1, FL[0])
    return draw


def reeds(c, r):
    """Cattails at a shoreline: a bed of stems and blades, brown heads, standing in the shallows."""
    cx, cy = spot(r, 12)
    for k in range(r.i(5, 8)):
        x = cx + r.i(-9, 9)
        y = cy + r.i(-3, 6)
        h = r.i(9, 15)
        lean = r.i(-2, 2)
        for j in range(h):
            t = j / float(h)
            c.set(x + int(round(lean * t * t)), y - j, VE[3] if j % 4 else LI[1])
        if r.f() < 0.65:
            hx, hy = x + lean, y - h
            for j in range(4):
                c.set(hx, hy - j, FL[3] if j else FL[2])
                c.set(hx + 1, hy - j, FL[4])
            c.set(hx, hy - 4, VE[3])
        else:
            # a blade, arching away
            for j in range(6):
                c.set(x + (j // 2) * (1 if k % 2 else -1), y - 2 - j, VE[2] if j < 4 else LI[1])
        c.set(x, y + 1, AZ[4])
        c.set(x + 1, y + 1, AZ[4])


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
# A wattle-and-post fence round a bed of dark earth, one moonflower in the middle, its light lying
# on the ground round it. PEN_W x PEN_H is the fence's outer size in world pixels; the gate is a
# gap in the front rail. Three sprites, each anchored at its own bottom edge (Node2DGame offsets
# them that way) so the holder's y-sort puts the front rail in front of an actor standing behind
# it and the back rail behind one standing in front of it.

PEN_W = 168
PEN_H = 120
RAIL_H = 12            # how tall the fence stands, drawn upward from its ground line
GATE_W = 32


def post(c, x, base, h):
    c.set(x, base + 1, OCC)
    c.set(x + 1, base + 1, OCC)
    for k in range(h):
        c.set(x, base - k, GO[2] if k < h - 1 else GO[1])
        c.set(x + 1, base - k, GO[3])
    c.set(x, base - h, GO[1])


def wattle_run(c, x0, x1, base, r):
    """A horizontal rail: posts every 12 px, two woven withies between them."""
    for x in range(x0, x1 + 1):
        for row, tone in ((RAIL_H - 4, GO[3]), (RAIL_H - 8, GO[2])):
            wob = 1 if ((x // 3) + row) % 2 else 0
            c.set(x, base - row - wob, tone)
            c.set(x, base - row - wob + 1, GO[4])
        c.set(x, base + 1, OCC if x % 2 else GO[4])
    for x in range(x0, x1 + 1, 12):
        post(c, x, base, RAIL_H)
    post(c, x1 - 1, base, RAIL_H)


def wattle_side(c, x, y0, y1):
    """A rail running away from the camera: seen end-on, it is posts and a thin top line."""
    for y in range(y0, y1 + 1):
        c.set(x, y - RAIL_H + 2, GO[3])
        c.set(x + 1, y - RAIL_H + 2, GO[4])
        c.set(x + 2, y, OCC if y % 2 else GO[4])
    for y in range(y0, y1 + 1, 14):
        post(c, x, y, RAIL_H)


def pen_back():
    """Back rail, both side rails, the bed and the flower's light. Anchored at the back rail."""
    w, h = PEN_W + 4, PEN_H + RAIL_H + 4
    c = raster.Canvas(w, h, None)
    top = RAIL_H + 1                      # the back rail's ground line
    bottom = h - 3                        # the front rail's ground line (drawn by pen_front)
    cx, cy = w // 2, (top + bottom) // 2 + 4
    # The bed: an oval of tilled earth in the middle, NOT the whole pen - the first render filled
    # the fence wall to wall with striped soil and it read as a rug. The turf carries on inside the
    # fence (these pixels are clear), so the pen is a place in the meadow rather than a hole in it.
    for y in range(cy - 20, cy + 21):
        for x in range(cx - 36, cx + 37):
            d = ((x - cx) / 36.0) ** 2 + ((y - cy) / 20.0) ** 2
            if d > 1.0:
                continue
            if d > 0.78 and (x + y) % 2:
                continue
            tone = FL[4] if (y // 3) % 2 else GO[4]
            if (x * 7 + y * 13) % 23 == 0:
                tone = LN[4]
            c.set(x, y, tone)
    # The moonflower's light, as a scatter of cold motes on the soil round it - not a pool, which
    # read as a puddle. Ice, not arcane, light or grass: those three are the XP orbs, and nothing
    # on the ground may look like one.
    ice = bl.ELEMENTS["ice"]
    r = Rand(77)
    for k in range(70):
        a = r.f() * TAU
        d = r.f() ** 0.7
        x = int(cx + math.cos(a) * d * 26)
        y = int(cy - 6 + math.sin(a) * d * 14)
        c.set(x, y, ice[3] if d > 0.45 else ice[2])
    # A ring of ordinary flowers round the bed, so the special one has company it outshines.
    for k in range(14):
        a = k * TAU / 14 + r.f() * 0.2
        x = int(cx + math.cos(a) * 41)
        y = int(cy + math.sin(a) * 24)
        p, ce = [(SK[1], GO[1]), (VI[0], GO[1]), (FL[1], FL[0])][k % 3]
        daisy(c, x, y, p, ce, 2)
    # Stepping stones from the gate to the bed.
    for k, (sx, sy) in enumerate(((cx - 3, bottom - 6), (cx + 4, bottom - 12))):
        for dy in range(-1, 2):
            for dx in range(-3, 4):
                if abs(dx) == 3 and dy != 0:
                    continue
                c.set(sx + dx, sy + dy, ST[1] if dy < 0 else ST[2])
        for dx in range(-2, 3):
            c.set(sx + dx, sy + 2, OCC)
    wattle_side(c, 2, top, bottom)
    wattle_side(c, w - 5, top, bottom)
    wattle_run(c, 2, w - 4, top, r)
    return c


def pen_front():
    """The front rail with its gate gap. Anchored at its own ground line."""
    w, h = PEN_W + 4, RAIL_H + 4
    c = raster.Canvas(w, h, None)
    base = h - 3
    gate0 = (w - GATE_W) // 2
    wattle_run(c, 2, gate0, base, None)
    wattle_run(c, gate0 + GATE_W, w - 4, base, None)
    # the gate posts are taller - it is a way in, and it should say so
    post(c, gate0, base, RAIL_H + 2)
    post(c, gate0 + GATE_W, base, RAIL_H + 2)
    return c


def moonflower():
    """The special flower: a tall stem, broad leaves, a pale bloom with a cold light at its heart.

    The only emissive thing a landmark carries, and it is the ICE ramp on purpose - see pen_back.
    Bigger than any flower in the detail sets by half again, and taller than the fence: the first
    one was the size of a daisy and photographed as one more speck in the bed.
    """
    w, h = 32, 44
    c = raster.Canvas(w, h, None)
    ice = bl.ELEMENTS["ice"]
    x = w // 2
    base = h - 2
    for dx in (-2, -1, 0, 1):
        c.set(x + dx, base + 1, OCC)
    for k in range(26):
        c.set(x, base - k, VE[3] if k % 5 else VE[2])
        c.set(x + 1, base - k, VE[4])
    # two broad leaves, lit on top
    for side, y0 in ((-1, base - 6), (1, base - 11)):
        for k in range(8):
            ly = y0 - int(k * 0.6)
            lx = x + side * (2 + k)
            c.set(lx, ly, VE[2] if k < 5 else VE[1])
            c.set(lx, ly + 1, VE[3])
            if 1 < k < 6:
                c.set(lx, ly + 2, VE[4])
    cy = base - 31
    # petals: bone-white, lit on the upper left, five lobes, the light at the centre
    for dy in range(-7, 8):
        for dx in range(-8, 9):
            d = (dx / 8.0) ** 2 + (dy / 6.5) ** 2
            if d > 1.0:
                continue
            ang = math.atan2(dy, dx)
            if 0.5 < d and abs(math.sin(ang * 2.5)) < 0.3:
                continue
            tone = SK[1] if dx + dy < 0 else SK[2]
            if d > 0.7 and dx + dy > 3:
                tone = SK[3]
            c.set(x + dx, cy + dy, tone)
    # the heart: a cold light, four steps from white to the ramp's edge
    for dx, dy, t in ((0, 0, 0), (-1, 0, 1), (1, 0, 1), (0, -1, 1), (0, 1, 2), (-1, -1, 1),
                      (1, 1, 2), (-2, 0, 2), (2, 0, 2), (0, 2, 3), (-1, 1, 2), (1, -1, 2)):
        c.set(x + dx, cy + dy, ice[t])
    c.set(x - 5, cy - 3, SK[0])
    c.set(x - 4, cy - 4, SK[0])
    # motes drifting up off it
    for mx, my, t in ((-9, -9, 1), (7, -11, 2), (-3, -13, 2), (10, -4, 3), (-11, -2, 3)):
        c.set(x + mx, cy + my, ice[t])
    return c


LANDMARKS = [
    ("flower-pen-back", pen_back),
    ("flower-pen-front", pen_front),
    ("flower-pen-flower", moonflower),
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

    for name, draw in LANDMARKS:
        c = draw()
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:4]))
            continue
        c.save(os.path.join(OUT_LANDMARK, name + ".png"))
        print("  landmark %s %dx%d" % (name, c.w, c.h))

    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/ground_detail.py - small things on the ground, '
                'one 48x48 cell each, painted by LevelTilePainter.PaintChunkDetail where GroundDetail '
                'says.",\n'
                '  "frames": [\n%s\n  ]\n}\n' % ",\n".join(frames))
    io.open(os.path.join(OUT_DETAIL, "detail.json"), "w", encoding="utf-8",
            newline="\n").write(manifest.decode("utf-8"))
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
