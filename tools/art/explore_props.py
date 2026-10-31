# -*- coding: utf-8 -*-
"""Exploration: the bog lantern, the three wayfinders, and the marks the wayfinders give.

Run: python tools/art/explore_props.py

See .ai/side-events.md, "Finding the event". A chapter's side event is out in the world from the
first second of a run, hidden, and the player walks to it. Each event chapter also hides ONE
wayfinder near the start - an object that, once found, puts an arrow at the edge of the screen for
every point of interest on the map. This draws:

  world/explore/bog-lantern.png     the Swamp's lantern, 64x64 cell: three lit frames and one dark
  world/explore/arcane-compass.png  the default wayfinder (Forest, Swamp, Sands), 32x32, 4 frames
  world/explore/gaolers-map.png     the Cursed Dungeon's: the jailer's map nailed to a post
  world/explore/bearing-cairn.png   the Frozen Waste's: a snow-capped cairn with a bearing plate
  world/explore/explore.json        where each sprite's foot is, and where the lantern's flame is
  ui/poi/poi-badge-his.png          the arrow badge for HIS places (the events): arcane cyan rim
  ui/poi/poi-badge-found.png        the badge for a wayfinder seen and not yet taken: gold rim
  ui/poi/poi-<glyph>.png            the 12x12 pictogram inside a badge, one per kind of place

THE LIGHT TELLS YOU WHOSE IT IS (.ai/world-and-tone.md: "every point of warm light is a piece of
it he does not have"). The lantern is his - "nothing living lights them" - so its flame is the cold,
sickly poison ramp and it FLICKERS (the code does the flicker; these frames only change the flame's
shape). The wayfinders are things he missed, so each carries the secrets' near tell from
.ai/stage-secrets.md section 2: one 2x2 cluster of gold `hi` with a one-pixel halo that BREATHES
across the four frames, and nothing else on the object moves much. Shape and rhythm carry it, not
hue alone.

Like props_fx.py, every sprite is authored on its contract cell and written at x2, so the game
draws it at scale 1 and a source pixel is always two screen pixels.
"""
from __future__ import division, print_function

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_WORLD = os.path.join(ROOT, "assets", "bonelight", "world", "explore")
OUT_UI = os.path.join(ROOT, "assets", "bonelight", "ui", "poi")
PREVIEW = os.path.join(ROOT, "tools", "art", "_explore_props_preview.png")

SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


POISON = bl.ELEMENTS["poison"]   # core, hot, mid, edge
GOLD = ramp("gold")
STEEL = ramp("steel")
STONE = ramp("stone")
LINEN = ramp("linen")
LICHEN = ramp("lichen")
SKIN = ramp("skin")
WOOD = ramp("flesh")             # the chest's planks are this row too (props_fx.plank)
# Parchment. The bone row read as a whiteboard; living flesh is the palette's one warm material
# besides gold, and its upper half is the colour of old paper.
PARCH = [WOOD[0], WOOD[0], WOOD[1], WOOD[2], OCC]
RED = ramp("red")
ARCANE = ramp("arcane")
VIOLET = ramp("violet")
ICE = bl.ELEMENTS["ice"]


def glint(c, x, y, phase):
    """The near tell every found thing carries: a 2x2 core of gold hi with a one-pixel halo of gold
    lit, breathing. phase 0 is the trough (halo only, two pixels), 2 the crest (core and full halo).
    Never larger than this - a glint the size of a spell reads as a spell (stage-secrets.md 2)."""
    if phase in (1, 2, 3):
        for dx, dy in ((-1, 0), (2, 0), (0, -1), (1, -1), (0, 2), (1, 2)):
            c.set(x + dx, y + dy, GOLD[1])
    else:
        c.set(x, y - 1, GOLD[1])
        c.set(x + 1, y + 2, GOLD[1])
    if phase == 2:
        for dx, dy in ((-1, -1), (2, -1), (-1, 2), (2, 2)):
            c.set(x + dx, y + dy, GOLD[2])
    core = GOLD[0] if phase in (1, 2, 3) else GOLD[1]
    c.rect(x, y, x + 1, y + 1, core)


# --- the bog lantern ----------------------------------------------------------------------------
#
# A crook of dead wood rising out of a tussock, with an iron cage hanging from the crook and a cold
# flame in the cage. 64x64 cell (the contract's large prop), so the pole stands a little taller than
# the wizard: the brief was "more prominent", and the first lantern was a 4 px line and a 12 px box
# that disappeared under his feet.

LANTERN_CELL = 64
LANTERN_GROUND = 62          # feet on row cell-2 (contract section 5)
LANTERN_FOOT_X = 31
LANTERN_FLAME = (44, 27)     # the flame's centre, in cell pixels; the code puts the glow here

# The pole's centre column at a row, so the crook leans a little. Dead wood is not straight.
def _pole_x(y):
    if y >= 42:
        return 31
    if y >= 26:
        return 30
    return 29


def lantern(i, n):
    c = raster.Canvas(LANTERN_CELL, LANTERN_CELL, None)
    lit = i < 3

    # contact line, then the tussock the pole is driven into
    # Centred on 62, not 62.5: there it reached row 64, one below the cell, and was sliced flat
    # along its foot (tools/art/clip_check.py).
    c.disc(31, 62, 10, 1.6, OCC)
    c.disc(31, 59, 9, 3.6, LICHEN[4])
    c.disc(30, 58, 7, 2.8, LICHEN[3])
    c.hline(25, 33, 56, LICHEN[2])
    for bx, top, lean in ((24, 50, -1), (27, 47, 0), (35, 49, 1), (38, 52, 1), (22, 53, -1)):
        for y in range(top, 59):
            t = (59 - y) / float(59 - top)
            c.set(bx + int(round(lean * t * 2)), y, LICHEN[1] if y < top + 2 else LICHEN[2])
    c.set(27, 47, LICHEN[0])

    # the pole: three columns, lit on the key side, a crack down the shaded side
    for y in range(12, 60):
        x = _pole_x(y)
        c.set(x - 1, y, LINEN[1])
        c.set(x, y, LINEN[2])
        c.set(x + 1, y, LINEN[3])
        if y % 7 in (2, 3):
            c.set(x + 1, y, LINEN[4])
        if y % 11 == 5:
            c.set(x, y, LINEN[3])
    c.set(_pole_x(12) - 1, 12, LINEN[0])
    # a knot and a split stub, so it reads as a branch rather than a post
    c.set(31, 38, LINEN[4])
    c.set(32, 37, LINEN[2])
    c.set(33, 36, LINEN[1])

    # the crook: up and over to the right, then a hook hanging down
    for x in range(29, 44):
        y = 11 - int(round(2.0 * math.sin((x - 29) / 15.0 * math.pi)))
        c.set(x, y, LINEN[1])
        c.set(x, y + 1, LINEN[3])
    c.set(44, 10, LINEN[2])
    c.set(45, 11, LINEN[2])
    c.set(45, 12, LINEN[3])

    # chain
    for y in range(13, 18):
        c.set(44 if y % 2 else 45, y, STEEL[3] if y % 2 else STEEL[2])

    # the cage: a cap, three bars and a floor. The bars' inner faces catch the flame.
    c.hline(40, 48, 18, STEEL[1])
    c.hline(39, 49, 19, STEEL[2])
    c.set(40, 18, STEEL[0])
    c.hline(39, 49, 20, STEEL[3])
    c.hline(39, 49, 32, STEEL[2])
    c.hline(40, 48, 33, STEEL[4])
    if lit:
        # the cage is dark inside; the flame stands in the middle of it and lights only what is
        # near. A cage full of green read as a lit box, not a lantern.
        c.rect(40, 21, 48, 31, OCC)
        c.disc(44, 28, 3.6, 3.8, POISON[3])
        flame_shapes = [
            # (lean of the tip, height above the base, half-width)
            (0, 7, 2.2),
            (-1, 8, 1.9),
            (1, 6, 2.5),
        ]
        lean, height, half = flame_shapes[i]
        base_y = 31
        for y in range(base_y - height, base_y):
            t = (y - (base_y - height)) / float(height)
            w = half * math.sin(min(1.0, t * 1.15) * math.pi * 0.6)
            xc = 44 + lean * (1.0 - t) * 1.4
            if w < 0.6:
                c.set(int(round(xc)), y, POISON[1])
                continue
            c.hline(int(round(xc - w)), int(round(xc + w)), y, POISON[2])
            if w > 1.0:
                c.hline(int(round(xc - w + 1)), int(round(xc + w - 1)), y, POISON[1])
        c.rect(44, base_y - 3, 44, base_y - 2, POISON[0])
        c.set(43, base_y - 2, POISON[0])
        c.set(int(round(44 + lean * 1.4)), base_y - height - 1, POISON[2])
    else:
        c.rect(40, 21, 48, 31, OCC)
        c.vline(44, 29, 31, STONE[1])
        c.set(44, 28, STONE[3])
    for bx in (39, 49):
        c.vline(bx, 21, 31, STEEL[2])
        if lit:
            c.vline(bx + (1 if bx == 39 else -1), 25, 30, POISON[3])
    c.vline(39, 21, 31, STEEL[1])
    c.vline(49, 21, 31, STEEL[3])
    if lit:
        # the cold light picks out the cap's underside and the floor's top edge
        c.hline(41, 47, 20, POISON[3])
        c.hline(41, 47, 32, POISON[2])
    return c


# --- the wayfinders -----------------------------------------------------------------------------

WAY_CELL = 32
WAY_GROUND = 29


def compass(i, n):
    """A brass compass, open on a flat stone. The needle wavers a few degrees as the glint breathes;
    nothing else moves. Dark arcane face, gold case: it is a piece of his magic in hands he never
    caught."""
    c = raster.Canvas(WAY_CELL, WAY_CELL, None)
    c.disc(16, 29.5, 12, 1.5, OCC)
    # the slab: lit top, shaded lip
    c.disc(16, 26, 12, 3.6, STONE[3])
    c.disc(16, 25, 12, 3.4, STONE[2])
    c.disc(15, 24.5, 10, 2.4, STONE[1])
    c.hline(6, 26, 28, STONE[4])
    c.set(9, 23, STONE[0])
    c.set(22, 26, STONE[4])

    # the bail ring above the case, and its shadow on the slab
    c.ring(16, 12.5, 2.2, 1.6, GOLD[2])
    c.set(15, 11, GOLD[1])
    c.disc(17, 24, 8, 2, STONE[3])

    # the case
    c.disc(16, 19, 9, 6, GOLD[3])
    c.disc(16, 18.5, 8.5, 5.6, GOLD[2])
    for a in range(0, 360, 6):
        r = math.radians(a)
        x = 16 + 8.0 * math.cos(r)
        y = 18.5 + 5.2 * math.sin(r)
        # key from the upper left: that half of the rim is lit, the opposite shaded
        lightness = -(math.cos(r) + math.sin(r))
        c.set(int(round(x)), int(round(y)), GOLD[1] if lightness > 0.5 else GOLD[2] if lightness > -0.6 else GOLD[3])
    # the face
    c.disc(16, 18.5, 6.4, 4.1, VIOLET[4])
    c.disc(15.5, 18, 5.2, 3.2, ARCANE[4])
    for (tx, ty) in ((16, 15), (16, 22), (10, 18), (22, 18)):
        c.set(tx, ty, GOLD[2])
    c.set(13, 16, ARCANE[3])

    # the needle: a narrow diamond, the north half gold hi and the south half red, wavering a
    # pixel either way as the glint breathes
    sway = [-1, 0, 1, 0][i]
    c.set(16 + sway, 14, GOLD[0])
    c.set(16 + sway, 15, GOLD[0])
    c.hline(15 + (sway > 0), 16 + (sway >= 0), 16, GOLD[0])
    c.hline(15, 17, 17, GOLD[1])
    c.hline(15, 17, 18, GOLD[1])
    c.set(16, 18, GOLD[2])
    c.hline(15, 17, 19, RED[0])
    c.hline(15 - (sway < 0), 16 - (sway > 0) + (sway < 0), 20, RED[0])
    c.set(16 - sway, 21, RED[1])
    c.set(16 - sway, 22, RED[1])
    glint(c, 10, 15, [0, 1, 2, 1][i])
    return c


def gaolers_map(i, n):
    """The jailer's own map of the cells, nailed to a post in a corridor. Parchment in the flesh row,
    the walls inked in occlusion, one cell marked in red. A brass nail carries the glint, and the
    curled corner lifts in a draught."""
    c = raster.Canvas(WAY_CELL, WAY_CELL, None)
    c.disc(16, 30, 6, 1.3, OCC)
    # the post
    for y in range(4, 30):
        c.set(14, y, WOOD[2])
        c.set(15, y, WOOD[3])
        c.set(16, y, WOOD[3])
        c.set(17, y, WOOD[4])
        if y % 6 == 1:
            c.set(16, y, WOOD[4])
    c.hline(14, 17, 4, WOOD[1])
    c.hline(13, 18, 25, STEEL[2])
    c.hline(13, 18, 26, STEEL[3])
    c.set(13, 25, STEEL[1])

    # the sheet, a little skewed, its lower-right corner curling forward
    lift = [0, 1, 2, 1][i]
    for y in range(8, 22):
        x0 = 7 + (1 if y in (8, 21) else 0)
        x1 = 25 - (1 if y in (8, 21) else 0)
        if y >= 18:
            x1 -= (y - 17) + lift // 2
        c.hline(x0, x1, y, PARCH[2])
        c.set(x0, y, PARCH[1])
        c.set(x1, y, PARCH[3])
    c.hline(8, 24, 8, PARCH[1])
    c.hline(8, 22, 21, PARCH[3])
    # the curl, standing off the sheet
    for k in range(4 + lift):
        c.set(25 - k, 22 - k + lift // 2, PARCH[1] if k == 0 else PARCH[0] if k == 1 else PARCH[2])
        c.set(25 - k, 23 - k + lift // 2, PARCH[3])
    # ink: corridors as a little lattice, and one cell marked
    for x in range(9, 23, 4):
        c.vline(x, 10, 19, PARCH[4])
    for y in (10, 14, 18):
        c.hline(9, 21, y, PARCH[4])
    c.set(11, 14, PARCH[2])
    c.set(15, 18, PARCH[2])
    c.set(17, 10, PARCH[2])
    c.rect(18, 15, 20, 17, PARCH[2])
    c.set(18, 15, RED[0])
    c.set(20, 15, RED[0])
    c.set(19, 16, RED[0])
    c.set(18, 17, RED[0])
    c.set(20, 17, RED[0])
    # the nail
    c.set(16, 8, STEEL[3])
    glint(c, 15, 7, [0, 1, 2, 1][i])
    return c


def bearing_cairn(i, n):
    """A cairn of four stones, snow on every top, a brass plate cut with an arrow on the highest.
    Travellers mark routes across snow this way; this one marks his."""
    c = raster.Canvas(WAY_CELL, WAY_CELL, None)
    c.disc(16, 29.5, 11, 1.6, OCC)

    # Steel's grey-blue rather than the floor stone, which is the ground's own value and vanished
    # into it.
    def stone(cx, cy, rx, ry):
        c.disc(cx, cy, rx, ry, STEEL[4])
        c.disc(cx - 0.5, cy - 0.4, rx - 0.6, ry - 0.5, STEEL[3])
        c.disc(cx - 1.5, cy - 1.0, rx * 0.55, ry * 0.45, STEEL[2])
        c.hline(int(cx - rx + 1), int(cx + rx - 1), int(cy + ry), OCC)
        # snow on the top
        c.hline(int(cx - rx * 0.6), int(cx + rx * 0.4), int(cy - ry + 0.5), SKIN[1])
        c.hline(int(cx - rx * 0.4), int(cx + rx * 0.1), int(cy - ry + 0.5) - 1 if ry > 2.2 else int(cy - ry + 0.5), SKIN[0])

    stone(16, 26, 10, 3.6)
    stone(15, 20.5, 7.5, 3.0)
    stone(17, 15.5, 5.6, 2.6)
    stone(16, 11, 4.2, 2.2)
    # the plate on the top stone: brass, an arrow cut into it
    c.rect(13, 10, 19, 12, GOLD[2])
    c.hline(13, 19, 10, GOLD[1])
    c.hline(13, 19, 12, GOLD[3])
    c.hline(14, 17, 11, GOLD[4])
    c.set(18, 11, GOLD[4])
    c.set(17, 10, GOLD[3])
    c.set(17, 12, GOLD[3])
    # a lichen stain down the shaded side
    c.set(20, 21, LICHEN[2])
    c.set(21, 22, LICHEN[3])
    c.set(23, 26, LICHEN[2])
    glint(c, 12, 9, [0, 1, 2, 1][i])
    return c


# --- the arrows' badges and glyphs --------------------------------------------------------------
#
# A diamond, not the chest's round bubble and not the objective's gold triangle: three promises,
# three shapes (an objective, a reward, a place). The code draws the pointing tail, so the art
# never has to be rotated off the pixel grid.

BADGE = 24


def badge(rim):
    c = raster.Canvas(BADGE, BADGE, None)
    hi, lit, base, shade, deep = rim
    mid = BADGE // 2
    for y in range(BADGE):
        half = mid - abs(y - mid + 0.5) - 0.5
        if half < 0:
            continue
        x0 = int(math.ceil(mid - half - 0.5))
        x1 = int(math.floor(mid + half - 0.5))
        c.hline(x0, x1, y, STONE[4])
        # two-pixel rim, lit on the upper-left edges and shaded on the lower-right (the key light)
        upper = y < mid
        for k in (0, 1):
            c.set(x0 + k, y, (lit if upper else base) if k == 0 else (base if upper else shade))
            c.set(x1 - k, y, (base if upper else shade) if k == 0 else shade)
        if y in (0, 1):
            c.hline(x0, x1, y, lit)
        if y in (BADGE - 1, BADGE - 2):
            c.hline(x0, x1, y, shade)
    c.set(mid - 1, 0, hi)
    c.set(mid, 0, hi)
    c.set(1, mid - 1, hi)
    return c


# 12x12 pictograms, a clear pixel all round (the stat glyph rule). Characters map to the palette
# below; '.' is clear.
GLYPH = 12
GLYPH_PAL = {
    "o": OCC,
    "s": STEEL[1], "S": STEEL[2], "z": STEEL[3], "Z": STEEL[4],
    "g": GOLD[0], "G": GOLD[1], "y": GOLD[2], "Y": GOLD[3],
    "p": POISON[0], "P": POISON[1], "q": POISON[2], "Q": POISON[3],
    "l": LINEN[0], "L": LINEN[1], "m": LINEN[2],
    "e": LICHEN[1],
    "i": ICE[0], "I": ICE[1], "j": ICE[2], "J": ICE[3],
    "v": VIOLET[3],
    "w": WOOD[1], "W": WOOD[2], "x": WOOD[3],
    "k": SKIN[0], "K": SKIN[1], "c": SKIN[2], "C": SKIN[3],
    "r": RED[0],
    "n": WOOD[0], "N": WOOD[1], "b": WOOD[2],
    "t": STEEL[1], "T": STEEL[2], "u": STEEL[3],
    "a": ARCANE[1], "A": ARCANE[3],
}

GLYPHS = {
    "lantern": [
        "............",
        ".....ss.....",
        ".....z......",
        "...sSSSSs...",
        "...S.QQ.z...",
        "...SQPPQz...",
        "...SqppqZ...",
        "...SQPPQz...",
        "...SzzzzZ...",
        "....zzzz....",
        "............",
        "............",
    ],
    "grove": [
        "............",
        "..e.....e...",
        "..l.e..l....",
        "...l.ll.....",
        "....lL......",
        "....lL..e...",
        "....lLmL....",
        "....lL......",
        "....lLm.....",
        "...lLLLm....",
        "..mm...mm...",
        "............",
    ],
    "cell": [
        "............",
        ".SSSSSSSSSS.",
        ".s.s.s.s.sz.",
        ".s.s.s.s.sz.",
        ".s.s.yy.s.z.",
        ".s.s.GY.s.z.",
        ".s.s.s.s.sz.",
        ".s.s.s.s.sz.",
        ".s.s.s.s.sz.",
        ".zzzzzzzzzz.",
        "............",
        "............",
    ],
    "ice": [
        "............",
        "..iiiiiiI...",
        "..iIIjjIjJ..",
        "..IjjvvjjJ..",
        "..Ijvvvvj J.",
        "..Ijjvvjj J.",
        "..IjjvvjjJ..",
        "..IjvjjvjJ..",
        "..IjvjjvjJ..",
        "..JJJJJJJJ..",
        "............",
        "............",
    ],
    "caravan": [
        "............",
        "....ssss....",
        "...s.ww.z...",
        "..s.w.ww.z..",
        "..sww.WWWz..",
        "..s.wyW.Wz..",
        "..sWWWW.xz..",
        "..s.WW.x.z..",
        "...z.xx.z...",
        "....zzzz....",
        "............",
        "............",
    ],
    "compass": [
        "............",
        ".....yy.....",
        "...GyyyyY...",
        "..Gv..g.vY..",
        "..yv.gg.vY..",
        "..yvv.Gvvy..",
        "..yv.zz.vY..",
        "..Yv..z.vY..",
        "...YyyyyY...",
        ".....YY.....",
        "............",
        "............",
    ],
    "map": [
        "............",
        "..nnnnnnnN..",
        "..nNoNoNoN..",
        "..nN.N.N.b..",
        "..nNoNoNoN..",
        "..nN.Nr.rb..",
        "..nNoNoNrN..",
        "..nN.Nr.rb..",
        "..nNoNoNbb..",
        "..Nbbbbbb...",
        "............",
        "............",
    ],
    "cairn": [
        "............",
        ".....kK.....",
        "....GyyY....",
        "....tTTu....",
        "...kKTTuu...",
        "...tTTTuu...",
        "..kKKTTTuu..",
        "..tTTTTTuu..",
        ".kKTTTTTTuu.",
        ".tTTTTTTTuu.",
        "..uuuuuuuu..",
        "............",
    ],
}


def glyph(name):
    rows = GLYPHS[name]
    c = raster.Canvas(GLYPH, GLYPH, None)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row[:GLYPH]):
            if ch in (".", " "):
                continue
            c.set(x, y, GLYPH_PAL[ch])
    return c


# --- output -------------------------------------------------------------------------------------

SHEETS = [
    # name, function, frames, cell, ground row, foot column
    ("bog-lantern", lantern, 4, LANTERN_CELL, LANTERN_GROUND, LANTERN_FOOT_X),
    ("arcane-compass", compass, 4, WAY_CELL, WAY_GROUND, 16),
    ("gaolers-map", gaolers_map, 4, WAY_CELL, WAY_GROUND, 16),
    ("bearing-cairn", bearing_cairn, 4, WAY_CELL, WAY_GROUND, 16),
]


def _audit(name, c):
    bad = c.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s:" % name)
        for h, cnt in bad[:6]:
            print("   %s  x%d" % (h, cnt))
        return False
    return True


def main():
    for d in (OUT_WORLD, OUT_UI):
        if not os.path.isdir(d):
            os.makedirs(d)

    ok = True
    manifest = {"scale": SCALE, "sprites": []}
    previews = []
    for name, fn, frames, cell, ground, foot in SHEETS:
        strip = Image.new("RGBA", (cell * SCALE * frames, cell * SCALE), (0, 0, 0, 0))
        for i in range(frames):
            c = fn(i, frames)
            ok = _audit("%s frame %d" % (name, i), c) and ok
            strip.alpha_composite(c.scaled(SCALE), (i * cell * SCALE, 0))
        strip.save(os.path.join(OUT_WORLD, name + ".png"))
        previews.append(strip)
        entry = {
            "id": name,
            "frames": frames,
            "frame_w": cell * SCALE,
            "frame_h": cell * SCALE,
            # Rows from the top of a frame, in PNG pixels: the bottom edge of the feet's row.
            "ground": (ground + 1) * SCALE,
            "anchor_x": foot * SCALE + SCALE // 2,
        }
        if name == "bog-lantern":
            # The middle of the flame, in PNG pixels of a frame; the code centres the glow on it.
            entry["flame"] = [LANTERN_FLAME[0] * SCALE + SCALE // 2, LANTERN_FLAME[1] * SCALE + SCALE // 2]
            entry["lit_frames"] = 3
        manifest["sprites"].append(entry)
        print("  %-16s %d frames at %dpx" % (name, frames, cell * SCALE))

    with open(os.path.join(OUT_WORLD, "explore.json"), "w") as f:
        json.dump(manifest, f, indent=1, sort_keys=True)
        f.write("\n")

    ui = []
    for name, rim in (("his", ARCANE), ("found", GOLD)):
        c = badge(rim)
        ok = _audit("badge " + name, c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT_UI, "poi-badge-%s.png" % name))
        ui.append(img)
    for name in sorted(GLYPHS):
        c = glyph(name)
        ok = _audit("glyph " + name, c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT_UI, "poi-%s.png" % name))
        ui.append(img)
    print("  poi badges 2, glyphs %d" % len(GLYPHS))

    # A preview on the swamp's floor tone, at x3 of the shipped size, for checking by eye.
    width = max(p.size[0] for p in previews)
    height = sum(p.size[1] for p in previews) + 64
    sheet = Image.new("RGBA", (width, height), raster.rgb(STONE[3]))
    y = 0
    for p in previews:
        sheet.alpha_composite(p, (0, y))
        y += p.size[1]
    x = 0
    for img in ui:
        if img.size[0] == BADGE * SCALE:
            sheet.alpha_composite(img, (x, y + 4))
        else:
            badge_img = ui[0]
            sheet.alpha_composite(badge_img, (x, y + 4))
            off = (BADGE - GLYPH) * SCALE // 2
            sheet.alpha_composite(img, (x + off, y + 4 + off))
        x += BADGE * SCALE + 6
    sheet = sheet.resize((sheet.size[0] * 3, sheet.size[1] * 3), Image.NEAREST)
    sheet.save(PREVIEW)

    if not ok:
        return 1
    print("exploration props written, palette clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
