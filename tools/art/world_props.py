# -*- coding: utf-8 -*-
"""Everything scattered on the floor of a stage. Run: python tools/art/world_props.py

Two sets, and they are consumed by two different systems:

  DECOR   the trees, bushes, rocks and crystals Node2DGame.BuildDecorProps loads by path and
          scatters in clusters. Eleven textures.
  PROPS   the themed set PropCatalog loads from a manifest and Node2DGame.BuildCuratedProps
          scatters by the theme list each stage palette asks for. Twenty-one, three per theme,
          plus `props.json`.

They replace two bought prop packs and four bought sheets - art licensed for use and not for
redistribution, see `.ai/asset-licensing.md`. The manifest is ours and separate from the bought
ones for the reason the Sketchbook's tile manifest is: appending to somebody else's inventory
file is how an asset update silently deletes your work.

THEY ARE SEEN FROM ABOVE AND THEY ARE NEVER THE SUBJECT. A prop competes with the swarm for the
player's attention and must lose, so everything here is drawn darker and flatter than an actor:
no rim light, a heavy contact shadow, and the lit face reduced to a single stop rather than the
full five. The one exception is crystals, which glow - they are the only thing on a stage floor
that is allowed to be emissive, and that is what makes them read as worth walking towards.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DECOR = os.path.join(ROOT, "assets", "bonelight", "world")
OUT_PROPS = os.path.join(ROOT, "assets", "bonelight", "world", "props")

CELL = 32
BIG = 48
TAU = math.pi * 2.0


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


def shadow(c, cx, cy, rx, ry=None):
    """The contact ellipse. Every prop gets one - without it they hover."""
    c.disc(cx, cy, rx, ry if ry is not None else rx * 0.42, OCC)


def mound(c, cx, base, rx, ry, mat, dither=0):
    """A rounded mass on the ground, lit on the upper left only."""
    hi, lit, mid, shade, deep = ramp(mat)
    c.disc(cx, base - ry * 0.55, rx, ry, mid)
    c.disc(cx - rx * 0.22, base - ry * 0.8, rx * 0.62, ry * 0.6, lit)
    c.disc(cx + rx * 0.35, base - ry * 0.3, rx * 0.45, ry * 0.45, shade)
    if dither:
        for k in range(dither):
            a = (k * 2.399)
            r = (k / float(dither)) ** 0.5
            c.set(cx + math.cos(a) * rx * r * 0.85, base - ry * 0.55 + math.sin(a) * ry * r * 0.85,
                  deep if k % 2 else shade)


def stalk(c, x, base, height, lean, mat, leaves=0):
    """A stem with optional leaves. Used by ferns, bushes and grass tufts."""
    hi, lit, mid, shade, deep = ramp(mat)
    for k in range(int(height)):
        t = k / float(max(1, height))
        px = x + lean * (t ** 1.6)
        c.set(px, base - k, mid if k % 3 else lit)
        c.set(px + 1, base - k, deep)
    for k in range(leaves):
        t = 0.35 + 0.6 * k / float(max(1, leaves))
        px = x + lean * (t ** 1.6)
        py = base - height * t
        side = 1 if k % 2 else -1
        for j in range(4):
            c.set(px + side * (j + 1), py - j * 0.6, mid if j % 2 else lit)


def crystal(c, cx, base, height, half, element, lean=0.0):
    """A glowing shard. The one thing on a stage floor allowed to be emissive."""
    e = bl.ELEMENTS[element]
    top = base - height
    c.poly([(cx - half, base), (cx + lean, top), (cx + half, base), (cx - half, base)], e[3])
    c.poly([(cx - half * 0.35, base), (cx + lean, top), (cx + half * 0.35, base),
            (cx - half * 0.35, base)], e[2])
    c.line(cx + lean, top + 1, cx, base - 1, e[1])
    c.set(cx + lean, top, e[0])
    c.line(cx - half, base, cx + lean, top, OCC)


def box(c, x0, y0, x1, y1, mat, lid=True):
    """A crate or chest seen from above and slightly in front."""
    hi, lit, mid, shade, deep = ramp(mat)
    c.rect(x0, y0, x1, y1, mid)
    c.hline(x0, x1, y0, lit)
    c.hline(x0, x1, y1, OCC)
    c.vline(x0, y0, y1, lit)
    c.vline(x1, y0, y1, deep)
    if lid:
        c.hline(x0, x1, (y0 + y1) // 2, shade)
        c.hline(x0, x1, (y0 + y1) // 2 + 1, deep)
    for x in range(int(x0) + 2, int(x1) - 1, 5):
        c.vline(x, y0 + 1, y1 - 1, shade)


def bone(c, x, y, length, ang, mat):
    """One bone, with knobbed ends. The bones theme is built out of these."""
    hi, lit, mid, shade, deep = ramp(mat)
    dx, dy = math.cos(ang) * length, math.sin(ang) * length
    for w in (-1, 0, 1):
        c.line(x, y + w, x + dx, y + dy + w, mid if w == 0 else shade)
    for (px, py) in ((x, y), (x + dx, y + dy)):
        c.disc(px, py, 1.8, 1.6, mid)
        c.set(px - 1, py - 1, lit)


def post(c, x, base, height, mat, rungs=0):
    """An upright timber or iron post."""
    hi, lit, mid, shade, deep = ramp(mat)
    c.rect(x - 2, base - height, x + 2, base, mid)
    c.vline(x - 2, base - height, base, lit)
    c.vline(x + 2, base - height, base, deep)
    c.hline(x - 2, x + 2, base - height, lit)
    for k in range(rungs):
        y = base - height * (0.3 + 0.5 * k / max(1, rungs))
        c.hline(x - 5, x + 5, y, shade)
        c.hline(x - 5, x + 5, y + 1, deep)


# --- decor: what BuildDecorProps scatters ---------------------------------------------------

def tree(seed):
    """A canopy seen from above, with a trunk just visible under one edge.

    Top-down, so it is a blob of leaf with structure in it rather than a trunk with a shape on
    top. The structure is three overlapping lobes at different tones - a single disc of green
    reads as a bush however big it is.
    """
    c = raster.Canvas(BIG, BIG, None)
    lichen = ramp("lichen")
    shadow(c, 24, 42, 15, 5)
    c.rect(22, 33, 26, 43, ramp("flesh")[3])
    c.vline(22, 33, 43, ramp("flesh")[2])
    lobes = (((-7, -5), 13), ((6, -8), 11), ((3, 5), 12), ((-5, 7), 9)) if seed == 0 \
        else (((0, -8), 14), ((-8, 3), 11), ((8, 4), 12), ((0, 8), 8))
    for i, ((ox, oy), r) in enumerate(lobes):
        c.disc(24 + ox, 26 + oy, r, r * 0.88, lichen[3] if i % 2 else lichen[2])
    for i, ((ox, oy), r) in enumerate(lobes):
        c.disc(24 + ox - r * 0.25, 26 + oy - r * 0.3, r * 0.5, r * 0.45,
               lichen[1] if i % 2 else lichen[2])
    for k in range(26):
        a = k * 2.399 + seed
        rr = (k / 26.0) ** 0.5 * 15
        c.set(24 + math.cos(a) * rr, 26 + math.sin(a) * rr * 0.9, lichen[4] if k % 3 else lichen[0])
    return c


def bush(seed):
    c = raster.Canvas(CELL, CELL, None)
    shadow(c, 16, 27, 10, 3.5)
    mound(c, 16, 27, 11 - seed, 8 - seed, "lichen", dither=18)
    for k in range(5 + seed):
        stalk(c, 10 + k * 3, 25, 6 + (k % 3) * 2, (k - 2) * 0.6, "lichen", leaves=2)
    return c


def rock(seed):
    """A boulder, and the third time this project has had to be told that DEEP IS A DIRECTION
    AND INVISIBLE IS NOT.

    The first pass built it from stone base and shade - two of the darkest stops in the palette -
    sitting on a stage floor that is itself dark, and the result was a rock-shaped hole. The body
    is stone LIT now with base as the shaded face, so it reads as a lump catching the light
    rather than as an absence. sprite_treant.py and the Still Warden both record the same
    correction.
    """
    c = raster.Canvas(CELL, CELL, None)
    stone = ramp("stone")
    shadow(c, 16, 26, 11, 3.6)
    pts = [(5, 25), (8, 14), (14, 9), (22, 11), (27, 18), (25, 25)] if seed == 0 \
        else [(6, 25), (7, 16), (13, 11), (21, 13), (26, 20), (24, 25)]
    c.poly(pts + [pts[0]], stone[1])
    # the shaded face, on the lower right where the key light does not reach
    c.poly([(p[0] + 3, p[1] + 4) for p in pts[:4]] + [(pts[0][0] + 3, pts[0][1] + 4)], stone[2])
    for i in range(len(pts) - 1):
        c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], stone[0] if i < 3 else OCC)
    # two facet breaks, so it is a boulder rather than a pebble blown up
    c.line(11, 13, 18, 20, stone[3])
    c.line(18, 20, 24, 19, stone[3])
    c.set(12, 12, stone[0])
    return c


def gem_cluster(element):
    def draw():
        c = raster.Canvas(CELL, CELL, None)
        shadow(c, 16, 27, 10, 3.4)
        crystal(c, 16, 27, 18, 5, element, lean=0)
        crystal(c, 9, 27, 11, 3.5, element, lean=-2)
        crystal(c, 23, 27, 13, 4, element, lean=2)
        return c
    return draw


# --- themed props: what BuildCuratedProps scatters -------------------------------------------
# (id, theme, obstacle, painter)

def fern():
    c = raster.Canvas(CELL, CELL, None)
    shadow(c, 16, 28, 8, 3)
    for k in range(7):
        stalk(c, 16 + (k - 3) * 2, 28, 13 - abs(k - 3) * 2, (k - 3) * 1.4, "lichen", leaves=3)
    return c


def mushrooms():
    c = raster.Canvas(CELL, CELL, None)
    shadow(c, 16, 28, 9, 3)
    for (mx, my, r) in ((11, 22, 4.5), (19, 19, 5.5), (16, 26, 3.5)):
        c.rect(mx - 1, my, mx + 1, 28, ramp("skin")[3])
        c.disc(mx, my, r, r * 0.66, bl.ELEMENTS["poison"][3])
        c.disc(mx - 1, my - 1, r * 0.5, r * 0.36, bl.ELEMENTS["poison"][2])
        c.set(mx - 1, my - 2, bl.ELEMENTS["poison"][1])
    return c


def fallen_log():
    c = raster.Canvas(CELL, CELL, None)
    f = ramp("flesh")
    shadow(c, 16, 25, 14, 4)
    c.rect(3, 15, 28, 23, f[3])
    c.hline(3, 28, 15, f[2])
    c.hline(3, 28, 23, OCC)
    for x in range(5, 28, 5):
        c.vline(x, 16, 22, f[4])
    c.disc(28, 19, 4.2, 4.2, f[2])
    c.disc(28, 19, 2.4, 2.4, f[4])
    return c


def skull_pile():
    c = raster.Canvas(CELL, CELL, None)
    s = ramp("skin")
    shadow(c, 16, 27, 11, 3.6)
    for (sx, sy, r) in ((11, 22, 4.6), (21, 23, 4.0), (16, 17, 4.2)):
        c.disc(sx, sy, r, r * 0.92, s[2])
        c.disc(sx - 1, sy - 1, r * 0.5, r * 0.45, s[1])
        c.set(sx - 1.6, sy - 0.4, OCC)
        c.set(sx + 1.4, sy - 0.4, OCC)
        c.hline(sx - 1.6, sx + 1.4, sy + r * 0.55, OCC)
    return c


def ribcage():
    c = raster.Canvas(CELL, CELL, None)
    s = ramp("skin")
    shadow(c, 16, 27, 12, 3.6)
    c.rect(15, 8, 17, 26, s[2])
    for k in range(5):
        y = 11 + k * 3.4
        for side in (-1, 1):
            for j in range(6):
                c.set(16 + side * (2 + j), y + j * 0.55, s[2] if j % 2 else s[1])
    return c


def bone_heap():
    c = raster.Canvas(CELL, CELL, None)
    shadow(c, 16, 27, 11, 3.6)
    for k, ang in enumerate((0.2, 1.1, 2.3, 0.7, 2.9)):
        bone(c, 8 + k * 3, 20 + (k % 3) * 2, 11 - k, ang, "skin")
    return c


def wheelbarrow():
    c = raster.Canvas(CELL, CELL, None)
    st = ramp("steel")
    shadow(c, 16, 27, 12, 3.6)
    box(c, 6, 12, 24, 22, "flesh", lid=False)
    c.disc(22, 24, 4.4, 4.4, st[3])
    c.disc(22, 24, 2.0, 2.0, st[1])
    c.line(6, 14, 1, 22, st[2])
    return c


def ore_cart():
    c = raster.Canvas(CELL, CELL, None)
    st = ramp("steel")
    shadow(c, 16, 28, 13, 3.6)
    box(c, 4, 10, 27, 24, "steel", lid=False)
    for (ox, oy, r) in ((11, 11, 3.0), (18, 10, 3.6), (15, 13, 2.4)):
        c.disc(ox, oy, r, r * 0.8, ramp("stone")[2])
        c.set(ox - 1, oy - 1, bl.ELEMENTS["metal"][2])
    for wx in (9, 22):
        c.disc(wx, 26, 3.4, 3.4, st[4])
        c.disc(wx, 26, 1.4, 1.4, st[2])
    return c


def pick_and_timber():
    c = raster.Canvas(CELL, CELL, None)
    st = ramp("steel")
    shadow(c, 16, 27, 11, 3.4)
    post(c, 9, 27, 18, "flesh", rungs=0)
    c.line(14, 24, 26, 12, ramp("flesh")[2])
    c.line(15, 24, 27, 12, ramp("flesh")[3])
    c.poly([(22, 10), (29, 13), (24, 16), (22, 10)], st[2])
    c.set(29, 13, st[0])
    return c


def crystal_cluster():
    c = raster.Canvas(CELL, CELL, None)
    shadow(c, 16, 28, 11, 3.6)
    crystal(c, 16, 28, 20, 5.5, "arcane")
    crystal(c, 8, 28, 12, 3.6, "arcane", lean=-2)
    crystal(c, 24, 28, 14, 4.2, "arcane", lean=3)
    crystal(c, 19, 28, 8, 2.6, "arcane", lean=1)
    return c


def geode():
    c = raster.Canvas(CELL, CELL, None)
    stone = ramp("stone")
    a = bl.ELEMENTS["arcane"]
    shadow(c, 16, 27, 11, 3.6)
    c.disc(16, 20, 11, 9.5, stone[3])
    c.disc(16, 20, 8.5, 7.0, stone[2])
    c.disc(16, 20, 6.0, 5.0, OCC)
    for k in range(9):
        ang = k * TAU / 9.0
        c.line(16, 20, 16 + math.cos(ang) * 5.4, 20 + math.sin(ang) * 4.4, a[3] if k % 2 else a[2])
    c.disc(16, 20, 1.8, 1.6, a[0])
    c.set(10, 14, stone[0])
    return c


def shard_spray():
    c = raster.Canvas(CELL, CELL, None)
    shadow(c, 16, 28, 12, 3.4)
    for (sx, h, hw, lean, el) in ((7, 9, 3, -2, "ice"), (13, 15, 4, 0, "ice"),
                                  (20, 11, 3.4, 2, "ice"), (26, 7, 2.6, 3, "ice")):
        crystal(c, sx, 28, h, hw, el, lean=lean)
    return c


def gravestone():
    c = raster.Canvas(CELL, CELL, None)
    stone = ramp("stone")
    shadow(c, 16, 28, 10, 3.4)
    for y in range(9, 28):
        t = (y - 9) / 19.0
        half = 7.0 if t > 0.25 else 7.0 * (1.0 - (0.25 - t) ** 1.4 * 8.0)
        if half < 0.5:
            continue
        c.hline(16 - half, 16 + half, y, stone[2])
        c.set(16 - half, y, stone[1])
        c.set(16 + half, y, stone[4])
    for y in (15, 18, 21):
        c.hline(12, 20, y, stone[4])
    c.set(10, 11, stone[0])
    return c


def urn():
    c = raster.Canvas(CELL, CELL, None)
    g = ramp("gold")
    shadow(c, 16, 28, 9, 3.2)
    for y in range(11, 28):
        t = (y - 11) / 17.0
        half = 4.0 + 6.0 * math.sin(t * math.pi) ** 0.8
        c.hline(16 - half, 16 + half, y, g[3])
        c.set(16 - half, y, g[2])
        c.set(16 + half, y, g[4])
    c.rect(12, 8, 20, 11, g[3])
    c.hline(12, 20, 8, g[2])
    c.hline(11, 21, 19, g[4])
    c.set(12, 14, g[1])
    return c


def sarcophagus():
    c = raster.Canvas(CELL, CELL, None)
    stone = ramp("stone")
    shadow(c, 16, 29, 13, 3.6)
    c.poly([(7, 27), (9, 6), (23, 6), (25, 27), (7, 27)], stone[2])
    c.line(9, 6, 23, 6, stone[1])
    c.line(7, 27, 25, 27, OCC)
    c.poly([(12, 22), (13, 10), (19, 10), (20, 22), (12, 22)], stone[3])
    c.disc(16, 13, 2.6, 3.0, stone[4])
    c.set(10, 8, stone[0])
    return c


def coin_pile():
    c = raster.Canvas(CELL, CELL, None)
    g = ramp("gold")
    shadow(c, 16, 28, 12, 3.6)
    for k, (cx, cy, r) in enumerate(((11, 25, 4.0), (20, 26, 4.4), (16, 22, 4.2),
                                     (13, 20, 3.4), (19, 19, 3.0), (16, 16, 2.6))):
        c.disc(cx, cy, r, r * 0.55, g[3])
        c.disc(cx - r * 0.2, cy - 1, r * 0.66, r * 0.34, g[2])
        c.set(cx - r * 0.3, cy - 1, g[0] if k % 2 else g[1])
    return c


def open_crate():
    c = raster.Canvas(CELL, CELL, None)
    shadow(c, 16, 28, 12, 3.6)
    box(c, 5, 12, 26, 27, "flesh", lid=False)
    c.rect(8, 14, 23, 19, OCC)
    g = ramp("gold")
    for (cx, cy) in ((12, 17), (17, 16), (20, 18)):
        c.disc(cx, cy, 2.4, 1.4, g[3])
        c.set(cx - 1, cy - 1, g[1])
    return c


def goblet_spill():
    c = raster.Canvas(CELL, CELL, None)
    g = ramp("gold")
    shadow(c, 16, 28, 12, 3.4)
    for y in range(14, 20):
        half = 6.0 - 2.4 * (y - 14) / 6.0
        c.hline(10 - half * 0.0, 10 + half, y, g[3])
    c.rect(12, 20, 14, 25, g[4])
    c.rect(8, 25, 18, 26, g[3])
    for (cx, cy) in ((20, 24), (24, 26), (17, 27), (26, 22)):
        c.disc(cx, cy, 2.0, 1.2, g[3])
        c.set(cx - 1, cy, g[1])
    return c


def rack():
    c = raster.Canvas(CELL, CELL, None)
    f = ramp("flesh")
    st = ramp("steel")
    shadow(c, 16, 29, 13, 3.6)
    c.rect(5, 8, 27, 26, f[3])
    c.hline(5, 27, 8, f[2])
    c.hline(5, 27, 26, OCC)
    for x in (9, 16, 23):
        c.vline(x, 9, 25, f[4])
    for (rx, ry) in ((7, 10), (25, 10), (7, 24), (25, 24)):
        c.disc(rx, ry, 2.2, 2.2, st[3])
        c.set(rx - 1, ry - 1, st[1])
    return c


def brazier():
    c = raster.Canvas(CELL, CELL, None)
    st = ramp("steel")
    f = bl.ELEMENTS["fire"]
    shadow(c, 16, 29, 10, 3.4)
    for (lx, ly) in ((9, 29), (23, 29), (16, 30)):
        c.line(16, 20, lx, ly, st[3])
    for y in range(14, 21):
        half = 9.0 - 3.0 * (y - 14) / 7.0
        c.hline(16 - half, 16 + half, y, st[3])
        c.set(16 - half, y, st[2])
    c.hline(7, 25, 14, st[1])
    c.disc(16, 12, 6.0, 4.4, f[3])
    c.disc(16, 11, 3.6, 2.8, f[2])
    c.disc(16, 10, 1.8, 1.6, f[0])
    return c


def chain_post():
    c = raster.Canvas(CELL, CELL, None)
    st = ramp("steel")
    shadow(c, 16, 29, 8, 3.0)
    post(c, 14, 29, 22, "steel", rungs=0)
    for k in range(6):
        y = 12 + k * 2.8
        c.disc(18 + k * 1.4, y, 1.6, 1.4, st[3])
        c.set(18 + k * 1.4 - 1, y - 1, st[1])
    return c


DECOR = [
    ("tree-1", lambda: tree(0), BIG),
    ("tree-2", lambda: tree(1), BIG),
    ("bush-1", lambda: bush(0), CELL),
    ("bush-2", lambda: bush(1), CELL),
    ("rock-1", lambda: rock(0), CELL),
    ("rock-2", lambda: rock(1), CELL),
    ("crystal-violet", gem_cluster("arcane"), CELL),
    ("crystal-yellow", gem_cluster("lightning"), CELL),
    ("crystal-blue", gem_cluster("water"), CELL),
    ("crystal-green", gem_cluster("grass"), CELL),
    ("crystal-white", gem_cluster("ice"), CELL),
]

# (id, theme, obstacle, painter)
PROPS = [
    ("fern", "flora", False, fern),
    ("mushrooms", "flora", False, mushrooms),
    ("fallen-log", "flora", True, fallen_log),
    ("skull-pile", "bones", False, skull_pile),
    ("ribcage", "bones", True, ribcage),
    ("bone-heap", "bones", False, bone_heap),
    ("wheelbarrow", "mining", True, wheelbarrow),
    ("ore-cart", "mining", True, ore_cart),
    ("pick-and-timber", "mining", False, pick_and_timber),
    ("crystal-cluster", "crystal", True, crystal_cluster),
    ("geode", "crystal", False, geode),
    ("shard-spray", "crystal", False, shard_spray),
    ("gravestone", "funerary", True, gravestone),
    ("urn", "funerary", False, urn),
    ("sarcophagus", "funerary", True, sarcophagus),
    ("coin-pile", "treasure", False, coin_pile),
    ("open-crate", "treasure", True, open_crate),
    ("goblet-spill", "treasure", False, goblet_spill),
    ("rack", "torture", True, rack),
    ("brazier", "torture", True, brazier),
    ("chain-post", "torture", False, chain_post),
]


def emit(out_dir, items, label):
    """items is (name, painter, cell) - the cell is carried for documentation, not used here:
    each painter already builds a canvas of the size it wants."""
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    ok = True
    for (name, fn, _cell) in items:
        c = fn()
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s:" % name)
            for h, n in bad[:5]:
                print("   %s  x%d" % (h, n))
            continue
        c.scaled(2).save(os.path.join(out_dir, name + ".png"))
    print("  %-10s %d sprites" % (label, len(items)))
    return ok


def write_manifest():
    """Our own prop manifest, in the shape PropCatalog.Load already reads."""
    frames = []
    for (pid, theme, obstacle, fn) in PROPS:
        frames.append(
            '  {"id": "%s", "fileName": "%s.png", "path": "assets/bonelight/world/props/%s.png",\n'
            '   "placement": "floor", "propTheme": "%s", "usageClass": "prop",\n'
            '   "collision": "%s", "hazard": false, "width": 64, "height": 64}'
            % (pid, pid, pid, theme, "obstacle" if obstacle else "decoration"))

    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/world_props.py. Ours, kept separate from '
                'the bought prop packs so that updating one cannot take it with it. One line - a '
                'raw newline inside a JSON string is invalid and JsonDocument.Parse throws.",\n'
                '  "frames": [\n%s\n  ]\n}\n' % (",\n".join(frames)))
    path = os.path.join(OUT_PROPS, "props.json")
    import io
    io.open(path, "w", encoding="utf-8", newline="\n").write(manifest.decode("utf-8"))
    print("  manifest   %d props across %d themes"
          % (len(PROPS), len(set(p[1] for p in PROPS))))


def main():
    ok = emit(OUT_DECOR, DECOR, "decor")
    prop_items = [(p[0], p[1], p[2], p[3]) for p in PROPS]
    if not os.path.isdir(OUT_PROPS):
        os.makedirs(OUT_PROPS)
    for (pid, _theme, _obs, fn) in prop_items:
        c = fn()
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s:" % pid)
            for h, n in bad[:5]:
                print("   %s  x%d" % (h, n))
            continue
        c.scaled(2).save(os.path.join(OUT_PROPS, pid + ".png"))
    print("  props      %d sprites" % len(PROPS))
    write_manifest()
    if not ok:
        return 1
    print("")
    print("%d world sprites, palette clean" % (len(DECOR) + len(PROPS)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
