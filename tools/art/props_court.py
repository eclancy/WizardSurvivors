# -*- coding: utf-8 -*-
"""The Court's furniture (.ai/endgame-castle.md 5.4 and 5.8). Run: python tools/art/props_court.py

Everything that stands in the Court's rooms, written to assets/bonelight/world/court/ with court.json
(each sprite's size, its scale and where it stands). CourtRules places them from court_layout.json.

    the Great Hall      banquet-table (three of them), high-table, platter, fowl, fowl-bones
    the Chapel          altar, pew
    the Library         shelf, lectern
    the Armoury         rack
    the Kennels         kennel, straw
    the Map Room        map-table (the house's own plan on it, drawn from court_layout.json)
    the Treasury        coffer, strongbox
    the Gallery         portrait-covered, portrait (a freed wizard's, uncovered)
    the Minstrels'      harp, music-stand
    the Solar           bed, desk
    the Kitchens        oven, work-table, larder
    the Well Court      well
    the stair           stair (up from the cells, and the back stair to the Minstrels' Gallery)
    the doors           throne-doors, throne-doors-open, keepsake-door, keepsake-door-open, reliquary
    the hidden way      tapestry-hidden (a tapestry over a real gap, a pixel off true)
    the signs           plaque, sign, glyph-<name> (fifteen), arrow-<n|e|s|w>

TWO SCALES, AND WHY. Furniture is authored 1:1 and saved at x2, like every world sprite: an art pixel
is two world pixels. What is PART OF A WALL - the throne room doors, the ten keepsake doors and the
tapestry over the hidden way - is drawn at the wall tiles' own 1:1 and saved at x1, because it stands
in a wall's cells beside court_walls.py's tiles, and a door at twice the pixel size of the stone it is
set in reads as a sticker. The tapestry is painted with court_walls.py's own face, cap and tapestry,
so the wall it hangs on is the same wall that runs either side of it.

THE FOWL (5.4, Eric's roast fowl): a roast bird on a pewter platter, warm brown and crisp - the one
warm thing in the Great Hall, as food should be - and drawn to be a pickup a player knows on sight:
round, lit, sitting up on its dish. Pigment, not light: it does not glow, so it is never a secret's
warm glint or an XP orb. Eaten, it leaves its bones on the platter until another comes.

WHAT THE HOUSE HOLDS BACK. No suit of armour on a stand (the Empty Armour is the Court's Wall
creature), no candelabrum (the Candelabrum is its Turret), no censer and no hound: a prop shaped like
one of the cast is a decoy the player has to learn to ignore. Candles are cold and unlit, frames are
empty or covered, the kennels empty: a court with no one left in it.

Every colour is from the contract (each sprite is audited), contact shadows are `occ` on the last
rows, and the key light is upper-left.
"""
from __future__ import division, print_function

import io
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import court_walls as CW
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "court")
LAYOUT = os.path.join(ROOT, "assets", "bonelight", "layouts", "court_layout.json")
PREVIEW = os.path.join(ROOT, "tools", "art", "_props_court_preview.png")


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


GD = ramp("gold")
ST = ramp("stone")
STL = ramp("steel")
LN = ramp("linen")
SK = ramp("skin")
FL = ramp("flesh")
RD = ramp("red")
VI = ramp("violet")
LI = ramp("lichen")
WO = ramp("wool")


def hashed(x, y, salt=0):
    h = (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)
    h &= 0x7FFFFFFF
    return ((h >> 7) % 10007) / 10007.0


def box(c, x0, y0, x1, y1, fill, lit=None, dark=None):
    """A filled rectangle lit on its top and left edges and shaded on its bottom and right."""
    c.rect(x0, y0, x1, y1, fill)
    if lit:
        c.hline(x0, x1, y0, lit)
        c.vline(x0, y0, y1, lit)
    if dark:
        c.hline(x0, x1, y1, dark)
        c.vline(x1, y0, y1, dark)


def shadow_row(c, x0, x1, y):
    for x in range(int(x0), int(x1) + 1):
        c.set(x, y, OCC)


# --- the Great Hall ---------------------------------------------------------------------------------

TABLE_W, TABLE_H = 72, 580       # 144 x 1160 on screen: a table and its two benches
TABLE_TOP_X0, TABLE_TOP_X1 = 16, 55


def platter_offsets():
    """Where the four platters stand on a banquet table, in authored pixels from its centre: the
    layout puts them at 14.5, 21, 27.5 and 34 cells down a hall whose table centre is 24 cells down."""
    return [int((k * 6.5 + 14.5 - 24.0) * 24) for k in range(4)]


def bench(c, x0, x1, y0, y1):
    box(c, x0, y0, x1, y1, GD[4], GD[3], OCC)
    for y in range(y0 + 12, y1 - 6, 40):
        c.hline(x0 + 1, x1 - 1, y, OCC)          # the joint between two bench planks
    for y in (y0 + 6, y1 - 8):
        c.rect(x0 + 1, y, x1 - 1, y + 2, GD[3])  # a leg's top, seen past the seat


def banquet_table():
    c = raster.Canvas(TABLE_W, TABLE_H, None)
    # Its shadow on the floor, south and east, then the benches either side, then the table.
    c.rect(TABLE_TOP_X0 + 3, 6, TABLE_TOP_X1 + 3, TABLE_H - 2, OCC)
    bench(c, 2, 11, 18, TABLE_H - 20)
    bench(c, TABLE_W - 12, TABLE_W - 3, 18, TABLE_H - 20)
    box(c, TABLE_TOP_X0, 2, TABLE_TOP_X1, TABLE_H - 5, GD[4], GD[3], OCC)
    # The board's seams, lengthwise.
    for x in (TABLE_TOP_X0 + 10, TABLE_TOP_X1 - 10):
        for y in range(4, TABLE_H - 6):
            if hashed(x, y // 7, 5101) < 0.85:
                c.set(x, y, OCC)
    # The cloth down the middle: pale linen, its edges in shade, hanging over the table's ends.
    cx0, cx1 = TABLE_TOP_X0 + 12, TABLE_TOP_X1 - 12
    c.rect(cx0, 0, cx1, TABLE_H - 3, LN[2])
    c.vline(cx0, 0, TABLE_H - 3, LN[1])
    c.vline(cx1, 0, TABLE_H - 3, LN[3])
    for y in range(0, TABLE_H - 3, 9):
        c.set(cx0 + 4, y, LN[3])
        c.set(cx1 - 3, y + 4, LN[3])
    c.hline(cx0, cx1, TABLE_H - 3, LN[3])
    c.hline(cx0, cx1, TABLE_H - 2, OCC)
    # Laid places down both sides: a plate and a goblet at each, between the platters (which are
    # sprites of their own, so they can hold a bird or its bones).
    centre = TABLE_H // 2
    platters = [centre + o for o in platter_offsets()]
    for side, px in ((0, TABLE_TOP_X0 + 5), (1, TABLE_TOP_X1 - 5)):
        y = 30
        while y < TABLE_H - 30:
            if all(abs(y - p) > 30 for p in platters):
                plate(c, px, y)
                goblet(c, px + (4 if side == 0 else -4), y - 9)
            y += 34
    # Candlesticks down the cloth, cold: iron and a guttered stub, nobody has lit them.
    y = 50
    while y < TABLE_H - 40:
        if all(abs(y - p) > 26 for p in platters):
            candlestick(c, (cx0 + cx1) // 2, y)
        y += 70
    return c


def plate(c, x, y):
    c.rect(x - 3, y - 2, x + 3, y + 2, STL[3])
    c.rect(x - 2, y - 1, x + 2, y + 1, STL[4])
    c.set(x - 3, y - 2, STL[2])
    c.hline(x - 2, x + 3, y + 3, OCC)


def goblet(c, x, y):
    c.set(x - 1, y, GD[3])
    c.set(x, y, GD[2])
    c.set(x + 1, y, GD[3])
    c.set(x, y + 1, GD[3])
    c.set(x, y + 2, GD[4])
    c.hline(x - 1, x + 1, y + 3, GD[3])
    c.hline(x - 1, x + 2, y + 4, OCC)


def candlestick(c, x, y):
    c.rect(x - 2, y + 2, x + 2, y + 3, STL[3])
    c.set(x - 2, y + 2, STL[2])
    c.vline(x, y - 4, y + 1, STL[3])
    c.rect(x - 1, y - 9, x + 1, y - 5, SK[3])     # the stub, unlit
    c.set(x - 1, y - 9, SK[2])
    c.set(x, y - 10, OCC)                          # a cold wick
    c.hline(x - 2, x + 3, y + 4, OCC)


def high_table():
    w, h = 200, 44
    c = raster.Canvas(w, h, None)
    c.rect(8, 22, w - 6, h - 2, OCC)
    # The chair behind it: high-backed, empty, the one seat on the dais.
    cx = w // 2
    box(c, cx - 10, 0, cx + 10, 22, GD[4], GD[3], OCC)
    c.rect(cx - 7, 3, cx + 7, 19, RD[3])
    c.vline(cx - 7, 3, 19, RD[2])
    box(c, 4, 18, w - 9, h - 8, GD[4], GD[3], OCC)
    c.rect(20, 18, w - 25, h - 10, LN[2])
    c.hline(20, w - 25, 18, LN[1])
    c.hline(20, w - 25, h - 10, LN[3])
    for x in range(30, w - 30, 26):
        if abs(x - cx) > 16:
            plate(c, x, 28)
            goblet(c, x + 6, 23)
    candlestick(c, cx - 30, 28)
    candlestick(c, cx + 30, 28)
    return c


def platter():
    c = raster.Canvas(20, 13, None)
    c.disc(10, 8, 9, 4, OCC)
    c.disc(9.5, 6.5, 9, 4, STL[3])
    c.disc(9.5, 6.2, 7, 2.8, STL[4])
    for x in range(3, 10):
        c.set(x, 3, STL[2])
    c.set(4, 4, STL[2])
    return c


def fowl():
    """A roast bird sitting up on its dish: a crisp brown breast lit on its upper left, two drumsticks
    with pale knuckles of bone, and a scrap of herb. 16 x 14, sits on the platter's centre."""
    c = raster.Canvas(16, 14, None)
    c.disc(8, 11.5, 7, 2, OCC)
    # The body.
    for y in range(2, 12):
        for x in range(1, 15):
            d = ((x - 7.5) / 6.5) ** 2 + ((y - 7.0) / 4.8) ** 2
            if d > 1.0:
                continue
            # Roasted: gold's base for the crisp skin, flesh's lit where the key catches the breast,
            # gold's shade underneath and occlusion at the rim on the far side.
            rel = (x - 7.5) * 0.6 + (y - 7.0)
            tone = GD[2]
            if rel < -4.4:
                tone = FL[1]
            elif rel < -2.4:
                tone = FL[2]
            elif rel > 2.4:
                tone = GD[3]
            if d > 0.78 and rel > 0:
                tone = GD[4]
            c.set(x, y, tone)
    # Crackling: a few darker crisp marks along the breast.
    for (x, y) in ((5, 5), (8, 4), (10, 6), (6, 8), (9, 9)):
        c.set(x, y, GD[3])
    # The drumsticks, out to either side, each ending in a knuckle of bone.
    for side, x0 in ((-1, 2), (1, 13)):
        for k in range(3):
            c.set(x0 + side * k, 9 + k // 2, GD[3] if k else GD[2])
        kx = x0 + side * 3
        c.set(kx, 10, SK[1])
        c.set(kx, 11, SK[2])
        c.set(kx + side, 11, SK[2])
    # A sprig of herb on top: the only green on the table, lichen's lit stop.
    c.set(7, 2, LI[1])
    c.set(8, 2, LI[2])
    c.set(8, 1, LI[1])
    return c


def fowl_bones():
    c = raster.Canvas(16, 14, None)
    c.disc(8, 11.5, 6, 1.6, OCC)
    # A ribcage and two bones: bone's base and shade, never its brightest (a thing, not a light).
    for k in range(4):
        x = 5 + k * 2
        c.vline(x, 6, 9, SK[2] if k % 2 else SK[3])
    c.hline(5, 11, 6, SK[2])
    c.hline(4, 12, 10, SK[3])
    for (x, y) in ((2, 9), (3, 9), (13, 8), (14, 9)):
        c.set(x, y, SK[2])
    return c


# --- the Chapel --------------------------------------------------------------------------------------

def pew():
    w, h = 140, 16
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 4, w - 1, h - 1, OCC)
    box(c, 0, 0, w - 4, 5, GD[4], GD[3], OCC)            # the back rail
    box(c, 0, 7, w - 4, h - 4, GD[4], GD[3], OCC)        # the seat
    for x in (2, w // 2, w - 7):
        c.rect(x, 5, x + 2, h - 4, GD[3])                # the ends and the middle support
    return c


def altar():
    w, h = 64, 34
    c = raster.Canvas(w, h, None)
    c.rect(4, h - 5, w - 1, h - 1, OCC)
    box(c, 2, 12, w - 6, h - 5, ST[2], ST[1], ST[4])     # the stone table
    c.rect(4, 12, w - 8, 18, LN[2])                      # its cloth
    c.hline(4, w - 8, 12, LN[1])
    c.hline(4, w - 8, 18, LN[3])
    for x in range(6, w - 8, 4):
        c.set(x, 19, LN[3] if x % 8 else LN[4])         # the cloth's fringe
    # Two cold candles and, between them, a dish with nothing in it.
    for cx in (14, w - 18):
        c.rect(cx - 1, 2, cx + 1, 11, SK[3])
        c.set(cx - 1, 2, SK[2])
        c.set(cx, 1, OCC)
        c.rect(cx - 3, 10, cx + 3, 11, STL[3])
    c.disc(w // 2 - 2, 10, 6, 2, STL[3])
    c.disc(w // 2 - 2, 9.6, 4, 1.2, STL[4])
    return c


# --- the Library -------------------------------------------------------------------------------------

def shelf():
    """A bookcase: oak, four shelves of spines in old cloth - red, linen, violet, gold - all from the
    deep half, so a wall of books is a texture and not a rainbow."""
    w, h = 56, 46
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    box(c, 0, 0, w - 4, h - 4, GD[4], GD[3], OCC)
    spines = [RD[3], LN[3], VI[3], GD[3], RD[2], LN[4], VI[4], WO[4]]
    for s in range(4):
        y0 = 3 + s * 10
        y1 = y0 + 7
        c.rect(3, y0, w - 7, y1, OCC)
        x = 3
        k = 0
        while x < w - 8:
            bw = 2 + int(hashed(s, k, 5201) * 3)
            top = y0 + int(hashed(s, k, 5203) * 3)
            if hashed(s, k, 5205) < 0.88:
                tone = spines[int(hashed(s, k, 5207) * len(spines))]
                c.rect(x, top, min(x + bw - 1, w - 7), y1, tone)
                c.set(x, top, LN[2] if tone in (LN[3], LN[4]) else tone)
            x += bw + (1 if hashed(s, k, 5209) < 0.3 else 0)
            k += 1
        c.hline(2, w - 6, y1 + 1, GD[3])                 # the shelf's lit edge
    return c


def lectern():
    w, h = 18, 30
    c = raster.Canvas(w, h, None)
    c.disc(9, h - 2, 6, 1.6, OCC)
    c.rect(8, 12, 10, h - 3, GD[4])
    c.vline(8, 12, h - 3, GD[3])
    c.rect(5, h - 4, 13, h - 3, GD[4])
    # The slope, and the book open on it.
    box(c, 1, 3, 16, 12, GD[4], GD[3], OCC)
    c.rect(3, 4, 8, 10, SK[3])
    c.rect(9, 4, 14, 10, SK[3])
    c.vline(8, 4, 10, OCC)
    for y in range(5, 10, 2):
        c.hline(4, 7, y, SK[4])
        c.hline(10, 13, y, SK[4])
    c.set(3, 4, SK[2])
    return c


# --- the Armoury -------------------------------------------------------------------------------------

def rack():
    """A rack of arms: spears and a halberd upright, two swords, a blank round shield. No helm, no
    breastplate - nothing a suit of armour could be stood up from."""
    w, h = 50, 40
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    box(c, 1, 26, w - 4, 31, GD[4], GD[3], OCC)         # the rack's lower rail
    box(c, 1, 8, w - 4, 11, GD[4], GD[3], OCC)          # its upper rail
    for x in (2, w - 6):
        c.rect(x, 6, x + 2, h - 4, GD[4])
        c.vline(x, 6, h - 4, GD[3])
    for k, x in enumerate((8, 14, 20)):
        c.vline(x, 1, h - 5, GD[3] if k % 2 else GD[4])  # the haft
        c.set(x, 0, STL[2])                               # the point
        c.rect(x - 1, 1, x + 1, 3, STL[3])
        c.set(x - 1, 1, STL[2])
    for x in (27, 32):
        c.vline(x, 5, 30, STL[3])                        # the blades
        c.vline(x + 1, 5, 30, STL[4])
        c.set(x, 5, STL[2])
        c.hline(x - 2, x + 3, 22, GD[3])                 # the cross-guard
        c.rect(x, 23, x + 1, 27, GD[4])
    c.disc(42, 18, 5, 5, STL[3])
    c.disc(41, 17, 3, 3, STL[4])
    c.set(39, 14, STL[2])
    c.set(38, 15, STL[2])
    return c


# --- the Kennels ---------------------------------------------------------------------------------------

def kennel():
    w, h = 32, 28
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    # A box of boards with a pitched roof, an arched door, dark inside, and nobody in it.
    for y in range(0, 9):
        half = 2 + y * 13 // 8
        c.hline(w // 2 - 2 - half, w // 2 - 2 + half, y, GD[3] if y < 2 else (GD[4] if y % 3 else OCC))
    box(c, 2, 9, w - 6, h - 4, GD[4], GD[3], OCC)
    for x in range(6, w - 8, 5):
        c.vline(x, 10, h - 5, OCC)
    for y in range(13, h - 4):
        for x in range(10, 20):
            d = ((x - 14.5) / 5.0) ** 2 + ((y - (h - 4)) / 10.0) ** 2
            if d < 1.0:
                c.set(x, y, OCC)
    # The broken chain out of the door, its last link on the ground.
    for k in range(4):
        c.set(16 + k * 2, h - 5 + (k % 2), STL[3])
        c.set(17 + k * 2, h - 5 + (k % 2), STL[4])
    c.set(25, h - 4, STL[2])
    return c


def straw():
    w, h = 32, 14
    c = raster.Canvas(w, h, None)
    for k in range(70):
        x = 2 + int(hashed(k, 1, 5301) * (w - 6))
        y = 2 + int(hashed(k, 2, 5303) * (h - 5))
        d = ((x - w / 2.0) / (w / 2.0)) ** 2 + ((y - h / 2.0) / (h / 2.0)) ** 2
        if d > 1.0:
            continue
        tone = GD[3] if hashed(k, 3, 5305) < 0.6 else GD[2]
        c.set(x, y, tone)
        c.set(x + 1, y + (1 if k % 2 else 0), GD[4])
    return c


# --- the Map Room ----------------------------------------------------------------------------------

def map_table():
    """The table map of the house: the Court's own plan, read from court_layout.json and drawn on a
    sheet of old linen - the walls in ink, the halls bare, the rooms washed a stop darker. The same
    building the player is standing in, which is the room's whole point (5.2)."""
    w, h = 84, 70
    c = raster.Canvas(w, h, None)
    c.rect(4, h - 4, w - 1, h - 1, OCC)
    box(c, 1, 2, w - 5, h - 5, GD[4], GD[3], OCC)
    sx0, sy0, sx1, sy1 = 6, 6, w - 10, h - 9
    c.rect(sx0, sy0, sx1, sy1, LN[2])
    c.hline(sx0, sx1, sy0, LN[1])
    c.vline(sx0, sy0, sy1, LN[1])
    try:
        doc = json.load(io.open(LAYOUT, encoding="utf-8"))
        rows = doc["rows"]
        gw, gh = doc["width"], doc["height"]
        mw, mh = sx1 - sx0 - 3, sy1 - sy0 - 3
        for my in range(mh):
            for mx in range(mw):
                gx = int((mx + 0.5) * gw / float(mw))
                gy = int((my + 0.5) * gh / float(mh))
                ch = rows[gy][gx]
                if ch == "#":
                    # Ink only where a wall meets a floor: the plan's lines, not a solid block.
                    edge = False
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx = int((mx + 0.5 + dx) * gw / float(mw))
                        ny = int((my + 0.5 + dy) * gh / float(mh))
                        if 0 <= nx < gw and 0 <= ny < gh and rows[ny][nx] != "#":
                            edge = True
                    if edge:
                        c.set(sx0 + 2 + mx, sy0 + 2 + my, LN[4])
                elif ch in "mbc":
                    c.set(sx0 + 2 + mx, sy0 + 2 + my, LN[3])
    except (IOError, ValueError, KeyError):
        pass
    # Weights on the corners, and a pair of dividers left on it.
    for (x, y) in ((sx0 + 1, sy0 + 1), (sx1 - 2, sy0 + 1), (sx0 + 1, sy1 - 2), (sx1 - 2, sy1 - 2)):
        c.rect(x, y, x + 1, y + 1, STL[3])
    c.line(sx1 - 14, sy1 - 12, sx1 - 6, sy1 - 4, STL[3])
    c.line(sx1 - 14, sy1 - 12, sx1 - 18, sy1 - 3, STL[3])
    return c


# --- the Treasury --------------------------------------------------------------------------------------

def coffer():
    w, h = 28, 20
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    box(c, 1, 3, w - 4, h - 4, GD[4], GD[3], OCC)
    c.hline(1, w - 4, 7, OCC)                           # the lid's line
    for x in (5, w - 9):
        c.rect(x, 3, x + 1, h - 4, STL[3])               # iron straps
        c.set(x, 3, STL[2])
    c.rect(w // 2 - 3, 8, w // 2, 11, STL[3])           # the lock, shut
    c.set(w // 2 - 2, 9, OCC)
    return c


def strongbox():
    w, h = 36, 26
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    box(c, 1, 2, w - 4, h - 4, STL[4], STL[3], OCC)
    for y in (6, 12, 18):
        c.hline(2, w - 5, y, STL[3])
        for x in range(4, w - 6, 6):
            c.set(x, y, STL[2])                          # rivets
    c.rect(w // 2 - 4, 8, w // 2 + 1, 14, GD[4])
    c.set(w // 2 - 2, 11, OCC)
    return c


# --- the Gallery -------------------------------------------------------------------------------------

def frame(c, x0, y0, x1, y1):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if x in (x0, x0 + 1, x1 - 1, x1) or y in (y0, y0 + 1, y1 - 1, y1):
                c.set(x, y, GD[2] if (x <= x0 + 1 or y <= y0 + 1) else GD[4])
    c.set(x0, y0, GD[1])
    c.hline(x0 + 1, x1 + 1, y1 + 1, OCC)
    c.vline(x1 + 1, y0 + 1, y1 + 1, OCC)


def portrait_covered():
    """A portrait of one of the wizards he took, its face covered: a dark cloth hung over the canvas,
    caught on the frame's corners, the frame's gilt showing round it."""
    w, h = 24, 30
    c = raster.Canvas(w, h, None)
    frame(c, 1, 1, w - 3, h - 3)
    for y in range(3, h - 4):
        for x in range(3, w - 4):
            c.set(x, y, VI[4] if (x + y // 3) % 4 else VI[3])
    # The cloth's drape: folds down from the two top corners, hanging a little below the frame.
    for k in range(h - 6):
        c.set(6 + k // 4, 3 + k, VI[3])
        c.set(w - 8 - k // 4, 3 + k, VI[4] if k % 2 else OCC)
    for x in range(3, w - 4):
        c.set(x, h - 4 + (1 if x % 3 == 0 else 0), VI[4])
    return c


def portrait():
    """The same portrait, uncovered: a robed figure, head and shoulders, in warm light - the light of a
    wizard who is free. Nobody in particular; the frame says who by where it hangs."""
    w, h = 24, 30
    c = raster.Canvas(w, h, None)
    frame(c, 1, 1, w - 3, h - 3)
    c.rect(3, 3, w - 5, h - 5, LN[4])
    # A warm glow behind the head, and the figure: hood and shoulders in deep wool, a lit face.
    c.disc(11, 12, 6, 6, FL[4])
    for y in range(15, h - 4):
        half = min(8, 3 + (y - 15))
        c.hline(11 - half, 11 + half, y, WO[4])
        c.set(11 - half, y, WO[3])
    c.disc(11, 11, 3.5, 4, WO[4])
    c.disc(11, 12, 2, 2.5, FL[2])
    c.set(10, 11, FL[1])
    c.set(11, 13, FL[3])
    return c


# --- the Minstrels' Gallery --------------------------------------------------------------------------

def harp():
    w, h = 20, 34
    c = raster.Canvas(w, h, None)
    c.disc(10, h - 2, 7, 1.6, OCC)
    for y in range(2, h - 3):
        c.set(3 + y // 8, y, GD[3])                      # the pillar
        c.set(4 + y // 8, y, GD[4])
    for x in range(4, 17):
        c.set(x, 2 + (x - 4) // 3, GD[3])                # the neck
    for y in range(6, h - 4):
        c.set(16 - (h - 4 - y) // 6, y, GD[4])           # the soundbox
        c.set(17 - (h - 4 - y) // 6, y, OCC)
    for k in range(5):
        x = 6 + k * 2
        for y in range(4 + k, h - 6):
            if hashed(x, y, 5401) < 0.85:
                c.set(x, y, SK[3])                        # strings, slack
    return c


def music_stand():
    w, h = 12, 26
    c = raster.Canvas(w, h, None)
    c.disc(6, h - 2, 4, 1.2, OCC)
    c.vline(6, 9, h - 3, STL[3])
    c.hline(3, 9, h - 3, STL[4])
    box(c, 1, 2, 10, 9, STL[4], STL[3], OCC)
    c.rect(2, 3, 9, 7, SK[3])                           # a sheet, nobody to read it
    c.hline(3, 8, 4, SK[4])
    c.hline(3, 7, 6, SK[4])
    return c


# --- the Solar ---------------------------------------------------------------------------------------

def bed():
    """His bed: a canopied bed of black oak, its curtains drawn back, the covers smooth. Nobody sleeps
    here; the room is kept."""
    w, h = 58, 70
    c = raster.Canvas(w, h, None)
    c.rect(4, h - 4, w - 1, h - 1, OCC)
    box(c, 2, 0, w - 6, 10, GD[4], GD[3], OCC)          # the headboard and canopy
    for x in (2, w - 8):
        c.rect(x, 0, x + 2, h - 5, GD[4])
        c.vline(x, 0, h - 5, GD[3])
    box(c, 6, 10, w - 10, h - 6, VI[4], VI[3], OCC)     # the coverlet
    c.rect(9, 12, w - 13, 20, SK[3])                     # the pillow
    c.hline(9, w - 13, 12, SK[2])
    for y in range(24, h - 8, 6):
        c.hline(8, w - 12, y, VI[3])                     # its folds
    for x in (5, w - 10):
        c.rect(x, 10, x + 2, 34, RD[3])                  # the curtains, tied back
        c.vline(x, 10, 34, RD[2])
    return c


def desk():
    w, h = 38, 28
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    box(c, 1, 6, w - 4, h - 4, GD[4], GD[3], OCC)
    c.rect(6, 8, 18, 14, SK[3])                          # a letter, unfinished
    c.hline(7, 16, 10, SK[4])
    c.hline(7, 13, 12, SK[4])
    c.rect(24, 4, 28, 9, STL[4])                         # the inkwell
    c.set(24, 4, STL[3])
    c.line(28, 1, 25, 7, SK[2])                          # its quill
    return c


# --- the Kitchens --------------------------------------------------------------------------------------

def oven():
    """A bread oven built into the wall: a brick dome with a dark mouth. The fire is the vent in front
    of it, which is the hazard (5.9); the oven itself is cold brick."""
    w, h = 56, 48
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 4, w - 1, h - 1, OCC)
    for y in range(0, h - 4):
        for x in range(1, w - 3):
            d = ((x - (w - 4) / 2.0) / ((w - 4) / 2.0)) ** 2 + ((y - (h - 4)) / float(h - 4)) ** 2
            if d > 1.0:
                continue
            # Brick in red's deep half, mortar in occlusion, lit on the dome's upper left.
            brick = (x + (6 if (y // 5) % 2 else 0)) % 12 == 0 or y % 5 == 0
            tone = RD[3] if not brick else OCC
            if d > 0.8 and brick is False:
                tone = RD[2] if x < w / 2 else RD[4]
            c.set(x, y, tone)
    # The mouth: an arch, black, its sill lit.
    for y in range(h - 22, h - 6):
        for x in range(w // 2 - 11, w // 2 + 8):
            d = ((x - (w // 2 - 2)) / 9.5) ** 2 + ((y - (h - 6)) / 16.0) ** 2
            if d < 1.0:
                c.set(x, y, OCC)
    c.hline(w // 2 - 12, w // 2 + 8, h - 6, ST[2])
    return c


def work_table():
    w, h = 62, 26
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    box(c, 1, 2, w - 4, h - 4, GD[4], GD[3], OCC)
    c.rect(6, 6, 16, 12, GD[3])                          # a board
    c.line(8, 9, 15, 9, STL[3])                          # a knife on it
    c.disc(30, 10, 6, 4, ST[3])                          # a bowl
    c.disc(30, 9.5, 4, 2.5, ST[4])
    c.rect(42, 5, 52, 13, LN[3])                         # a sack
    c.hline(43, 51, 5, LN[2])
    return c


def larder():
    w, h = 52, 40
    c = raster.Canvas(w, h, None)
    c.rect(3, h - 3, w - 1, h - 1, OCC)
    box(c, 0, 0, w - 4, h - 4, GD[4], GD[3], OCC)
    for s in range(3):
        y = 4 + s * 11
        c.rect(3, y, w - 7, y + 8, OCC)
        x = 4
        k = 0
        while x < w - 10:
            if s == 2:
                c.rect(x, y + 3, x + 5, y + 8, LN[3])    # sacks
                c.set(x + 2, y + 3, LN[2])
                x += 8
            else:
                c.rect(x, y + 2, x + 3, y + 8, ST[2] if k % 2 else SK[4])   # jars
                c.set(x, y + 2, ST[1] if k % 2 else SK[3])
                x += 6
            k += 1
        c.hline(2, w - 6, y + 9, GD[3])
    return c


# --- the Well Court ----------------------------------------------------------------------------------

def well():
    w, h = 46, 46
    c = raster.Canvas(w, h, None)
    c.disc(24, h - 6, 20, 5, OCC)
    # The curb: a ring of dressed stone, lit on its upper left, and the dark water far down.
    c.disc(22, h - 12, 18, 9, ST[2])
    c.disc(22, h - 13, 14, 6.5, OCC)
    for x in range(6, 38):
        c.set(x, h - 21 + int(abs(x - 22) / 6.0), ST[1])
    # The posts and the roof over it, and a bucket on its rope.
    for x in (5, 37):
        c.rect(x, 4, x + 2, h - 12, GD[4])
        c.vline(x, 4, h - 12, GD[3])
    for y in range(0, 6):
        c.hline(2 + y, 42 - y, 5 - y, GD[3] if y == 5 else GD[4])
    c.hline(4, 40, 6, OCC)
    c.vline(22, 7, 19, LN[3])
    box(c, 19, 19, 25, 24, GD[4], GD[3], OCC)
    return c


# --- the stair -------------------------------------------------------------------------------------

def stair():
    """A stair going down through the floor: the stone lip lit, then each step a stop darker into the
    dark below - the way up from the cells, and the back stair to the minstrels."""
    w, h = 56, 40
    c = raster.Canvas(w, h, None)
    box(c, 0, 0, w - 1, h - 1, ST[2], ST[1], ST[3])
    tones = [ST[3], ST[3], ST[4], ST[4], OCC, OCC]
    for k, tone in enumerate(tones):
        y0 = 4 + k * 6
        c.rect(4, y0, w - 5, y0 + 5, tone)
        if k < 4:
            c.hline(4, w - 5, y0, ST[2] if k < 2 else ST[3])
    c.vline(4, 4, h - 5, ST[4])
    c.vline(w - 5, 4, h - 5, OCC)
    return c


# --- the doors (1:1, part of the wall) ------------------------------------------------------------------

def door_leaf(c, x0, y0, x1, y1, open_fraction=0.0):
    """An oak door of vertical boards with iron bands. Open: swung back, its edge showing, dark beyond."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            board = (x - x0) % 7 == 0
            c.set(x, y, OCC if board else GD[4])
    for x in range(x0, x1 + 1):
        if hashed(x, 1, 5501) < 0.6:
            c.set(x, y0 + 1, GD[3])
    for by in (y0 + 6, (y0 + y1) // 2, y1 - 7):
        c.hline(x0, x1, by, STL[4])
        c.hline(x0, x1, by + 1, STL[3])
        for x in range(x0 + 3, x1, 9):
            c.set(x, by + 1, STL[2])


def keepsake_door(opened):
    """One of the ten: an iron-banded door filling an alcove's mouth, three cells wide and the wall's
    two deep (1:1). Shut, a lock plate and a keyhole; open, swung in and dark behind, the keepsake's
    plinth inside lit by the warm light of a thing found."""
    w, h = 144, 96
    c = raster.Canvas(w, h, None)
    # The frame: stone jambs and a lintel, lit on the left and top.
    box(c, 0, 0, w - 1, h - 1, ST[3], ST[2], OCC)
    c.rect(6, 8, w - 7, h - 5, OCC)
    if not opened:
        door_leaf(c, 7, 9, w - 8, h - 6)
        cx = w // 2
        c.rect(cx - 5, h // 2 - 6, cx + 5, h // 2 + 6, STL[3])
        c.set(cx - 5, h // 2 - 6, STL[2])
        c.rect(cx - 1, h // 2 - 2, cx, h // 2 + 3, OCC)
    else:
        # The leaf swung back against the left jamb, seen edge-on; beyond it the alcove's dark.
        for y in range(9, h - 5):
            c.hline(7, 14, y, GD[4])
            c.set(14, y, GD[3])
            c.set(15, y, OCC)
    # The arch over the frame, a keystone, and the foot of the wall.
    for x in range(0, w):
        c.set(x, 0, ST[1])
    c.rect(w // 2 - 4, 0, w // 2 + 3, 6, ST[2])
    c.hline(w // 2 - 4, w // 2 + 3, 0, ST[1])
    c.hline(0, w - 1, h - 4, ST[4])
    for y in range(h - 3, h):
        c.hline(0, w - 1, y, OCC)
    return c


def throne_doors(opened):
    """The throne room doors, in the vestibule's north wall: two great leaves under a pointed arch, a
    crown carved in the keystone. 384 x 192 at 1:1 - eight cells by four, the wall's two cells and the
    rock above them. Open, the leaves swing in and what is beyond is dark: he is beyond them."""
    w, h = 384, 192
    c = raster.Canvas(w, h, None)
    # The arch's surround: dressed stone, the pointed arch, voussoirs lit on their upper left.
    cx = w // 2
    for y in range(h):
        for x in range(w):
            dx = abs(x - cx) / (w / 2.0)
            top = 10 + int((dx ** 0.6) * 60)
            if y < top:
                continue
            c.set(x, y, ST[3] if (x // 24 + y // 16) % 2 else ST[4])
            if y == top:
                c.set(x, y, ST[1])
    inner_top = 40
    for y in range(inner_top, h - 4):
        for x in range(32, w - 32):
            dx = abs(x - cx) / ((w - 64) / 2.0)
            top = inner_top + int((dx ** 0.6) * 54)
            if y >= top:
                c.set(x, y, OCC)
    if not opened:
        door_leaf(c, 40, 52, cx - 3, h - 6)
        door_leaf(c, cx + 2, 52, w - 41, h - 6)
        for x in range(40, w - 40):
            dx = abs(x - cx) / ((w - 80) / 2.0)
            top = 52 + int((dx ** 0.6) * 46)
            for y in range(inner_top + 8, top):
                c.set(x, y, OCC)
        c.rect(cx - 2, 52, cx + 1, h - 6, OCC)
        # The rings to pull them by.
        for rx in (cx - 14, cx + 12):
            for (dx, dy) in ((0, 0), (1, 0), (2, 1), (2, 2), (1, 3), (0, 3), (-1, 2), (-1, 1)):
                c.set(rx + dx, h // 2 + 20 + dy, STL[2])
    else:
        for y in range(52, h - 6):
            c.hline(40, 58, y, GD[4])
            c.set(58, y, GD[3])
            c.hline(w - 59, w - 41, y, GD[4])
            c.set(w - 59, y, OCC)
    # The crown in the keystone, carved and limed: bone's pale stop on the dark stone.
    kx, ky = cx - 9, 14
    c.rect(kx - 4, ky - 4, kx + 21, ky + 16, ST[2])
    c.hline(kx - 4, kx + 21, ky - 4, ST[1])
    crown = ["X...X...X", "XX.XXX.XX", "XXXXXXXXX", "XXXXXXXXX", ".XXXXXXX."]
    for j, row in enumerate(crown):
        for i, ch in enumerate(row):
            if ch == "X":
                c.rect(kx + i * 2, ky + j * 2 + 1, kx + i * 2 + 1, ky + j * 2 + 2, SK[1])
    for y in range(h - 4, h):
        c.hline(0, w - 1, y, OCC)
    return c


def reliquary(lit):
    """The keepsake's plinth in its alcove. Lit: the keepsake found, a small warm light on a stone
    plinth - the warm glint the game's secrets breathe, because that is what it is. Unlit: an empty
    plinth, never seen (its door stays shut)."""
    w, h = 22, 30
    c = raster.Canvas(w, h, None)
    c.disc(11, h - 2, 8, 1.6, OCC)
    box(c, 4, 14, 17, h - 3, ST[2], ST[1], ST[4])
    c.hline(2, 19, 14, ST[1])
    c.hline(2, 19, 15, ST[3])
    if lit:
        c.disc(10.5, 9, 4, 4, GD[3])
        c.disc(10.5, 8.5, 2.6, 2.6, GD[2])
        c.set(10, 7, GD[1])
        c.set(11, 7, GD[0])
    return c


def tapestry_hidden():
    """The hidden way (5.5): a tapestry over a real gap three cells wide, in a wall two cells deep. Drawn
    at 1:1 with court_walls.py's own cap and face either side, so the wall runs on unbroken over the
    gap and the jambs beside it; five cells wide in all. It hangs one pixel off true - its right-hand
    end a pixel lower than its left - which is the only thing wrong with it."""
    w, h = 240, 96
    c = raster.Canvas(w, h, None)
    for k in range(5):
        cap = raster.Canvas(48, 48, None)
        CW.cap(cap, 41 + k)
        CW.J.rim_top(cap)
        c.img.paste(cap.img, (k * 48, 0))
        face = raster.Canvas(48, 48, None)
        CW.face(face, 41 + k * 13)
        c.img.paste(face.img, (k * 48, 48))
    c.px = c.img.load()
    CW.tapestry(_Offset(c, 0, 48), 52, CW.CORNICE + 4, 136, 34, skew=1)
    return c


class _Offset(object):
    def __init__(self, c, ox, oy):
        self.c, self.ox, self.oy = c, ox, oy

    def set(self, x, y, col):
        self.c.set(int(x) + self.ox, int(y) + self.oy, col)

    def get(self, x, y):
        return self.c.get(int(x) + self.ox, int(y) + self.oy)


# --- the glyphs over the doors, and the signs at the junctions -------------------------------------------
#
# Carved into stone and limed: bone's palest working stop (never its brightest, which is a light) on a
# roundel of dark stone, so a glyph reads from across a hall on a phone (5.2) and is never warm (a
# secret's glint) or an XP orb's colour. Eleven pixels, one shape each, nothing a word.

GLYPHS = {
    "cup": [
        "XXXXXXXXX..",
        "XXXXXXXXX..",
        ".XXXXXXX...",
        ".XXXXXXX...",
        "..XXXXX....",
        "...XXX.....",
        "....X......",
        "....X......",
        "....X......",
        "..XXXXX....",
        ".XXXXXXX...",
    ],
    "pot": [
        "...........",
        "..X.....X..",
        ".XXXXXXXXX.",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        ".XXXXXXXXX.",
        "..XXXXXXX..",
        "..X.....X..",
        ".XX.....XX.",
    ],
    "lute": [
        "........XX.",
        ".......XX..",
        "......XX...",
        ".....XX....",
        "..XXXX.....",
        ".XXXXXX....",
        "XXXX.XXX...",
        "XXX...XX...",
        "XXXX.XXX...",
        ".XXXXXX....",
        "..XXXX.....",
    ],
    "bell": [
        ".....X.....",
        "....XXX....",
        "...XXXXX...",
        "...XXXXX...",
        "..XXXXXXX..",
        "..XXXXXXX..",
        "..XXXXXXX..",
        ".XXXXXXXXX.",
        "XXXXXXXXXXX",
        ".....X.....",
        "....XXX....",
    ],
    "book": [
        "...........",
        "XXXX...XXXX",
        "X..XX.XX..X",
        "X...XXX...X",
        "X...XXX...X",
        "X...XXX...X",
        "X...XXX...X",
        "X...XXX...X",
        "XXXXXXXXXXX",
        ".....X.....",
        "...........",
    ],
    "sword": [
        ".....X.....",
        ".....X.....",
        "....XXX....",
        "....XXX....",
        "....XXX....",
        "....XXX....",
        "....XXX....",
        "XXXXXXXXXXX",
        ".....X.....",
        ".....X.....",
        "....XXX....",
    ],
    "collar": [
        "...........",
        ".XX.....XX.",
        "XXXX...XXXX",
        ".XX.....XX.",
        "....XXX....",
        "..XX...XX..",
        ".XXXXXXXXX.",
        ".XXXXXXXXX.",
        "..XXXXXXX..",
        "...XXXXX...",
        "...........",
    ],
    "compass": [
        ".....X.....",
        ".....X.....",
        "....XXX....",
        ".X..XXX..X.",
        "..XXXXXXX..",
        "XXXXX.XXXXX",
        "..XXXXXXX..",
        ".X..XXX..X.",
        "....XXX....",
        ".....X.....",
        ".....X.....",
    ],
    "key": [
        "..XXX......",
        ".XX.XX.....",
        ".X...X.....",
        ".XX.XX.....",
        "..XXX......",
        "...X.......",
        "...X.......",
        "...XXX.....",
        "...X.......",
        "...XXX.....",
        "...X.......",
    ],
    "frame": [
        "XXXXXXXXXXX",
        "X.........X",
        "X...XXX...X",
        "X...XXX...X",
        "X....X....X",
        "X..XXXXX..X",
        "X.XXXXXXX.X",
        "X.XXXXXXX.X",
        "X.........X",
        "XXXXXXXXXXX",
        "...........",
    ],
    "moon": [
        "...XXXX....",
        "..XXX......",
        ".XXX.......",
        "XXX........",
        "XXX........",
        "XXX........",
        "XXX........",
        ".XXX.......",
        "..XXX......",
        "...XXXX....",
        "...........",
    ],
    "arch": [
        "...XXXXX...",
        "..XX...XX..",
        ".XX.....XX.",
        ".X.......X.",
        ".X..XXX..X.",
        ".X..XXX..X.",
        ".X...X...X.",
        ".X...X...X.",
        ".X.......X.",
        ".X.......X.",
        "XXXXXXXXXXX",
    ],
    "well": [
        ".....X.....",
        "...XXXXX...",
        ".XXXXXXXXX.",
        ".X.......X.",
        ".X...X...X.",
        ".X..XXX..X.",
        "XXXXXXXXXXX",
        "X.X.X.X.X.X",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        "...........",
    ],
    "stair": [
        "........XXX",
        "........X..",
        "......XXX..",
        "......X....",
        "....XXX....",
        "....X......",
        "..XXX......",
        "..X........",
        "XXX........",
        "X..........",
        "...........",
    ],
    "crown": [
        "...........",
        "X....X....X",
        "XX..XXX..XX",
        "XXX.XXX.XXX",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        "...........",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        "...........",
    ],
}

ARROWS = {
    "n": ["...X...", "..XXX..", ".XXXXX.", "XXXXXXX", "..XXX..", "..XXX..", "..XXX.."],
}


def _rot(pat):
    """A pattern turned a quarter clockwise."""
    n = len(pat)
    return ["".join(pat[n - 1 - j][i] for j in range(n)) for i in range(n)]


ARROWS["e"] = _rot(ARROWS["n"])
ARROWS["s"] = _rot(ARROWS["e"])
ARROWS["w"] = _rot(ARROWS["s"])


def carve(c, x0, y0, pat):
    """A pattern carved into stone: the glyph limed in bone, its incision's shadow on the upper left
    of each stroke and the stone's lit lip on its lower right."""
    for j, row in enumerate(pat):
        for i, ch in enumerate(row):
            if ch != "X":
                continue
            x, y = x0 + i, y0 + j
            c.set(x, y, SK[1])
    for j, row in enumerate(pat):
        for i, ch in enumerate(row):
            if ch != "X":
                continue
            x, y = x0 + i, y0 + j
            if (j + 1 >= len(pat) or pat[j + 1][i] != "X") and c.get(x, y + 1)[3] and c.get(x, y + 1)[:3] != raster.rgb(SK[1])[:3]:
                c.set(x, y + 1, OCC)


def glyph(name):
    """A glyph alone, 13 x 13: limed bone on a square of dark stone, for the signs to compose."""
    c = raster.Canvas(13, 13, None)
    box(c, 0, 0, 12, 12, ST[4], ST[3], OCC)
    carve(c, 1, 1, GLYPHS[name])
    return c


def arrow(d):
    c = raster.Canvas(9, 9, None)
    box(c, 0, 0, 8, 8, ST[4], ST[3], OCC)
    carve(c, 1, 1, ARROWS[d])
    return c


def plaque():
    """The roundel a door's glyph is set in (CourtRules lays the glyph over its middle): dressed stone,
    lit on its upper left, a dark ring, on the wall beside the door."""
    w = h = 19
    c = raster.Canvas(w, h, None)
    c.disc(9.5, 10.5, 9, 9, OCC)
    c.disc(9, 9, 9, 9, ST[3])
    c.disc(9, 9, 8, 8, ST[2])
    c.disc(9, 9, 7, 7, ST[4])
    for k in range(6):
        c.set(3 + k, 3 + (5 - k) // 2, ST[1])
    return c


def sign():
    """The plate a junction's sign is set on: an arrow and up to three glyphs side by side
    (CourtRules composes them), on a strip of dressed stone two cells wide."""
    w, h = 48, 17
    c = raster.Canvas(w, h, None)
    box(c, 0, 0, w - 2, h - 2, ST[3], ST[2], ST[4])
    c.hline(1, w - 1, h - 1, OCC)
    c.vline(w - 1, 1, h - 1, OCC)
    c.rect(2, 2, w - 4, h - 4, ST[4])
    return c


# ------------------------------------------------------------------------------------------------
# name -> (painter, scale, anchor). Anchor: where the sprite's placement point is - "ground" (the
# bottom edge's middle stands on it: things that stand up and sort with the bodies), "centre" (flat
# things lying on the floor, and things hung on a wall face), or "foot" (a wall piece whose bottom edge
# is the wall's foot line).

SPRITES = [
    ("banquet-table", banquet_table, 2, "centre"),
    ("high-table", high_table, 2, "centre"),
    ("platter", platter, 2, "centre"),
    ("fowl", fowl, 2, "centre"),
    ("fowl-bones", fowl_bones, 2, "centre"),
    ("pew", pew, 2, "centre"),
    ("altar", altar, 2, "ground"),
    ("shelf", shelf, 2, "ground"),
    ("lectern", lectern, 2, "ground"),
    ("rack", rack, 2, "ground"),
    ("kennel", kennel, 2, "ground"),
    ("straw", straw, 2, "centre"),
    ("map-table", map_table, 2, "centre"),
    ("coffer", coffer, 2, "ground"),
    ("strongbox", strongbox, 2, "ground"),
    ("portrait-covered", portrait_covered, 2, "centre"),
    ("portrait", portrait, 2, "centre"),
    ("harp", harp, 2, "ground"),
    ("music-stand", music_stand, 2, "ground"),
    ("bed", bed, 2, "ground"),
    ("desk", desk, 2, "ground"),
    ("oven", oven, 2, "ground"),
    ("work-table", work_table, 2, "centre"),
    ("larder", larder, 2, "ground"),
    ("well", well, 2, "ground"),
    ("stair", stair, 2, "centre"),
    ("throne-doors", lambda: throne_doors(False), 1, "foot"),
    ("throne-doors-open", lambda: throne_doors(True), 1, "foot"),
    ("keepsake-door", lambda: keepsake_door(False), 1, "foot"),
    ("keepsake-door-open", lambda: keepsake_door(True), 1, "foot"),
    ("reliquary", lambda: reliquary(True), 2, "ground"),
    ("tapestry-hidden", tapestry_hidden, 1, "foot"),
    ("plaque", plaque, 2, "centre"),
    ("sign", sign, 2, "centre"),
]
for _name in sorted(GLYPHS):
    SPRITES.append(("glyph-" + _name, (lambda n: lambda: glyph(n))(_name), 2, "centre"))
for _d in ("n", "e", "s", "w"):
    SPRITES.append(("arrow-" + _d, (lambda d: lambda: arrow(d))(_d), 2, "centre"))


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True
    manifest = {}
    shots = []
    for name, fn, scale, anchor in SPRITES:
        c = fn()
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:4]))
            continue
        img = c.scaled(scale) if scale != 1 else c.img
        img.save(os.path.join(OUT, name + ".png"))
        manifest[name] = {"width": img.size[0], "height": img.size[1], "scale": scale, "anchor": anchor}
        shots.append(img)
    manifest["_comment"] = ("Generated by tools/art/props_court.py - the Court's furniture. width/height on screen; "
                            "anchor: ground = bottom middle stands on the point, centre = centred on it, foot = "
                            "bottom middle on a wall's foot line.")
    manifest["_platterOffsets"] = [o * 2 for o in platter_offsets()]
    text = json.dumps(manifest, indent=1, sort_keys=True)
    text = text if isinstance(text, type(u"")) else text.decode("utf-8")
    io.open(os.path.join(OUT, "court.json"), "w", encoding="utf-8", newline="\n").write(text + u"\n")
    print("  %d court sprites -> assets/bonelight/world/court/ (court.json)" % len(manifest))

    # A preview on the flags' tone, for looking at.
    width = 20
    height = 0
    row_w = 0
    for s in shots:
        if row_w + s.size[0] > 1400:
            width = max(width, row_w)
            height += 1
            row_w = 0
        row_w += s.size[0] + 10
    sheet_w = 1420
    x, y, row_h = 10, 10, 0
    placed = []
    for s in shots:
        if s.size[1] > 400:
            s = s.crop((0, 0, s.size[0], 400))
        if x + s.size[0] > sheet_w - 10:
            x = 10
            y += row_h + 10
            row_h = 0
        placed.append((s, x, y))
        x += s.size[0] + 10
        row_h = max(row_h, s.size[1])
    sheet = Image.new("RGBA", (sheet_w, y + row_h + 10), raster.rgb(ST[2]))
    for s, px, py in placed:
        sheet.alpha_composite(s, (px, py))
    sheet.save(PREVIEW)
    print("preview: tools/art/_props_court_preview.png")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
