# -*- coding: utf-8 -*-
"""Relic and set icons. Run: python tools/art/relic_icons.py

Twenty-nine relics and ten Full Set Enchantments, 32x32 rendered at x2 onto the UI cell in
`.ai/art-direction.md` section 1 - the same cell the spell and boon icons use, because all three
are offered on the same card and have to survive being compared.

WHY THEY EXIST. Every one of these was a slice of a bought GUI sheet: `ui-png-elements2-*` for
most of the relics, `ui-png-iconsmenu-*` for the sets, and a handful of spell-VFX frames for the
elemental ones. That art is licensed for use and not for redistribution, so none of it can be in
a public repository - see `.ai/asset-licensing.md`. These replace all thirty-nine.

THEY ARE OBJECTS, NOT EFFECTS, which is the rule `boon_icons.py` established and the reason its
helpers are imported here rather than copied. A spell icon is emissive with a white-hot core; a
relic is a thing the player is carrying, so it is drawn on the MATERIAL rows and lit from the
upper left, and it only touches an element ramp where the object is genuinely glowing. Sharing
one lighting model across boons and relics is what makes "this card is not a spell" readable at a
glance without anybody being told.

A SET ICON IS THE ASSEMBLED OBJECT. Each Full Set Enchantment is the pieces of one real thing, so
its icon is that thing whole - the vault door, the lantern, the crown - rather than a badge or a
monogram. The relics that make it up are the parts.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import boon_icons as boon
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_RELICS = os.path.join(ROOT, "assets", "bonelight", "ui", "relics")
OUT_SETS = os.path.join(ROOT, "assets", "bonelight", "ui", "sets")

CELL = 32

# One lighting model for every carried object in the game. See the note above.
_ramp = boon._ramp
_lit_disc = boon._lit_disc
_shadow = boon._shadow


# --- shared parts -----------------------------------------------------------------------------
# Each of these is a thing several relics are made of. Written once so that, for instance, every
# blade in the set catches light on the same edge - two blades lit from opposite sides read as
# two art styles rather than as two objects.

def plate(c, x0, y0, x1, y1, mat, rivets=()):
    """A flat panel: lit along the top and left, dark along the bottom and right."""
    hi, lit, base, shade, deep = _ramp(mat)
    c.rect(x0, y0, x1, y1, base)
    c.hline(x0, x1, y0, lit)
    c.vline(x0, y0, y1, lit)
    c.hline(x0, x1, y1, deep)
    c.vline(x1, y0, y1, shade)
    c.set(x0, y0, hi)
    for (rx, ry) in rivets:
        c.disc(rx, ry, 1.4, 1.4, shade)
        c.set(rx - 1, ry - 1, hi)


def blade(c, tip, base_y, half, mat, edge_mat=None):
    """A tapering blade, point up, with a lit edge down one side and occlusion down the other."""
    hi, lit, mid, shade, deep = _ramp(mat)
    tx, ty = tip
    c.poly([(tx - half, base_y), (tx, ty), (tx + half, base_y), (tx - half, base_y)], mid)
    c.line(tx, ty + 1, tx - half + 1, base_y - 1, lit if edge_mat is None else edge_mat)
    c.line(tx + 1, ty + 1, tx + half - 1, base_y - 1, deep)
    c.line(tx, ty, tx, base_y - 1, shade)
    c.set(tx, ty, hi)


def hilt(c, cx, y, half, mat):
    """Crossguard and grip under a blade."""
    hi, lit, mid, shade, deep = _ramp(mat)
    c.rect(cx - half, y, cx + half, y + 1, mid)
    c.hline(cx - half, cx + half, y, lit)
    c.hline(cx - half, cx + half, y + 1, deep)
    c.rect(cx - 1, y + 2, cx + 1, y + 6, shade)
    c.vline(cx - 1, y + 2, y + 6, mid)
    c.set(cx, y + 7, hi)


def vessel(c, cx, neck_y, bot_y, half, glass, fill_ramp, fill_from=None):
    """A glass vessel with something lit inside it.

    The glass is a material row and the contents are an element ramp, which is the only place
    these icons mix the two - a flask of fire is exactly an object with an effect inside it.
    """
    hi, lit, mid, shade, deep = _ramp(glass)
    fill_from = (neck_y + bot_y) // 2 if fill_from is None else fill_from

    c.poly([(cx - half, bot_y), (cx - half, neck_y + 3), (cx - 2, neck_y),
            (cx + 2, neck_y), (cx + half, neck_y + 3), (cx + half, bot_y),
            (cx - half, bot_y)], mid)
    # contents
    c.rect(cx - half + 1, fill_from, cx + half - 1, bot_y - 1, fill_ramp[3])
    c.rect(cx - half + 2, fill_from + 1, cx + half - 2, bot_y - 2, fill_ramp[2])
    c.hline(cx - half + 2, cx + half - 2, fill_from, fill_ramp[1])
    c.set(cx, fill_from, fill_ramp[0])
    # glass over the top of it
    c.vline(cx - half, neck_y + 3, bot_y, lit)
    c.vline(cx + half, neck_y + 3, bot_y, deep)
    c.hline(cx - half, cx + half, bot_y, deep)
    c.rect(cx - 2, neck_y - 3, cx + 2, neck_y, shade)
    c.hline(cx - 2, cx + 2, neck_y - 3, lit)
    c.set(cx - half + 1, fill_from - 3, hi)


def gem(c, cx, cy, r, ramp, facets=True):
    """A cut stone from an element ramp: a bright core, a dark rim, and hard facet lines."""
    c.disc(cx, cy, r, r, ramp[3])
    c.disc(cx, cy, r * 0.66, r * 0.66, ramp[2])
    c.disc(cx - r * 0.2, cy - r * 0.2, r * 0.3, r * 0.3, ramp[1])
    c.set(cx - r * 0.25, cy - r * 0.25, ramp[0])
    if facets:
        for deg in (30, 150, 270):
            a = math.radians(deg)
            c.line(cx, cy, cx + math.cos(a) * r, cy + math.sin(a) * r, OCC)


def shield(c, cx, top, bot, half, mat, boss_ramp=None):
    """A heater shield: square shoulders, a point at the bottom."""
    hi, lit, mid, shade, deep = _ramp(mat)
    for y in range(int(top), int(bot) + 1):
        t = (y - top) / float(max(1, bot - top))
        w = half * (1.0 - max(0.0, (t - 0.45)) ** 1.6 * 1.9)
        if w < 0.5:
            continue
        c.hline(cx - w, cx + w, y, mid)
        c.set(cx - w, y, lit)
        c.set(cx + w, y, deep)
    c.hline(cx - half, cx + half, top, lit)
    c.set(cx - half, top, hi)
    if boss_ramp:
        gem(c, cx, (top + bot) / 2.0 - 1, 3.4, boss_ramp, facets=False)


def band(c, cx, cy, r, mat, thick=2):
    """A ring of metal, seen face on."""
    hi, lit, mid, shade, deep = _ramp(mat)
    c.ring(cx, cy, r, r, mid, thick)
    c.ring(cx, cy, r, r * 0.98, lit, 1)
    c.set(cx - r * 0.7, cy - r * 0.7, hi)
    c.set(cx + r * 0.7, cy + r * 0.7, deep)


# --- damage -----------------------------------------------------------------------------------

def relic_key():
    """The key that opens the chests. Drawn UPRIGHT and thick, not on a diagonal.

    The first version ran the shaft corner to corner as a one-pixel line and came out as a ring
    with a scratch under it. A key is a bow, a shaft and a bit, and at this size all three have to
    be at least two pixels wide or the object is not there.
    """
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")

    # bow: a fat ring at the top
    band(c, 16, 8, 6.0, "steel", 3)
    c.disc(16, 8, 3.0, 3.0, OCC)

    # shaft: three pixels wide, straight down
    c.rect(15, 13, 18, 28, mid)
    c.vline(15, 13, 28, lit)
    c.vline(18, 13, 28, deep)

    # bit: two teeth off one side, which is the only part that says "key" rather than "lollipop"
    for (ty, tw) in ((20, 6), (25, 8)):
        c.rect(19, ty, 19 + tw, ty + 2, mid)
        c.hline(19, 19 + tw, ty, lit)
        c.hline(19, 19 + tw, ty + 2, deep)
    c.set(14, 6, hi)
    _shadow(c, 16, 8)
    return c


def ember_flask():
    """A flask with fire shut inside it."""
    c = raster.Canvas(CELL, CELL, None)
    vessel(c, 16, 9, 27, 7, "steel", bl.ELEMENTS["fire"], fill_from=16)
    _shadow(c, 16, 8)
    return c


def wrath_amulet():
    """Two crossed blades hung on a chain. The only relic drawn as a pair."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")

    # The chain, then a solid plate, then the blades cut INTO the plate. Two free-floating
    # hairline swords was the first attempt and it read as a scribble; an amulet is a disc you
    # can see, with something on it.
    for y in range(2, 8):
        c.set(11 - (y - 2) * 0.5, y, shade)
        c.set(21 + (y - 2) * 0.5, y, shade)
    _lit_disc(c, 16, 18, 11.0, "gold")
    c.disc(16, 18, 8.5, 8.5, _ramp("gold")[3])

    for sign in (-1, 1):
        # blade: four pixels wide, so it survives being on top of the plate
        for k in range(14):
            t = k / 13.0
            x = 16 - sign * 6 + sign * 12 * t
            y = 25 - 14 * t
            w = 2.0 - 0.9 * t
            c.hline(x - w, x + w, y, mid if sign < 0 else shade)
            c.set(x - w, y, lit)
        c.set(16 + sign * 6, 11, hi)
    c.rect(12, 20, 20, 21, deep)
    gem(c, 16, 18, 2.4, bl.ELEMENTS["fire"], facets=False)
    _shadow(c, 16, 10)
    return c


def ethereal_blade():
    """A blade you can see through: a steel hilt holding an arcane edge."""
    c = raster.Canvas(CELL, CELL, None)
    a = bl.ELEMENTS["arcane"]
    for y in range(4, 21):
        t = (y - 4) / 17.0
        w = 1.0 + 3.2 * t
        c.hline(16 - w, 16 + w, y, a[3])
        c.hline(16 - w + 1, 16 + w - 1, y, a[2])
        c.set(16, y, a[1])
    c.set(16, 4, a[0])
    hilt(c, 16, 21, 6, "steel")
    _shadow(c, 16, 6)
    return c


def spectral_fang():
    """A curved tooth on a thong. Bone, not metal - the one relic that was alive."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("skin")

    # Wide at the root and tapering to the point, with the curve in the CENTRELINE rather than in
    # the width. The first pass had it three pixels across at its fattest, which at 32px is a
    # scratch rather than a tooth.
    prev = None
    for k in range(22):
        t = k / 21.0
        x = 13 + 8.0 * (t ** 1.8)
        y = 7 + 20.0 * t
        w = 5.5 * (1.0 - t ** 1.3) + 0.5
        c.hline(x - w, x + w, y, mid)
        c.set(x - w, y, lit)
        c.set(x - w + 1, y, lit)
        c.set(x + w, y, deep)
        prev = (x, y)
    c.set(prev[0], prev[1], hi)
    # A groove down it, which is what makes a tooth read as bone rather than as a claw.
    for k in range(4, 18):
        t = k / 21.0
        c.set(13 + 8.0 * (t ** 1.8) + 1, 7 + 20.0 * t, shade)

    c.line(4, 6, 26, 9, shade)
    c.line(4, 7, 26, 10, deep)
    band(c, 14, 7, 3.0, "gold", 2)
    _shadow(c, 17, 7)
    return c


def obsidian_heart():
    """Black glass, cut as a heart. Drawn from the EARTH ramp for the reason the Obsidian Spike
    icon records: real obsidian on a near-black card is an invisible object."""
    c = raster.Canvas(CELL, CELL, None)
    e = bl.ELEMENTS["earth"]

    # Built as two lobes and a V, explicitly, rather than as one tapering mass - a heart is a
    # shape with a NOTCH in the top and the first pass had no notch, so it came out as a shield.
    c.disc(11, 12, 5.6, 5.6, e[3])
    c.disc(21, 12, 5.6, 5.6, e[3])
    for y in range(12, 28):
        t = (y - 12) / 16.0
        w = 10.5 * (1.0 - t ** 1.7)
        if w < 0.5:
            continue
        c.hline(16 - w, 16 + w, y, e[3])

    # the lit faces: upper-left of each lobe, and a broad plane down the left of the point
    c.disc(10, 11, 3.2, 3.2, e[2])
    c.disc(20, 11, 2.6, 2.6, e[2])
    for y in range(13, 25):
        t = (y - 13) / 12.0
        w = 9.0 * (1.0 - t ** 1.7)
        c.hline(16 - w, 16 - w * 0.25, y, e[2])
    c.disc(9, 10, 1.6, 1.6, e[1])
    c.set(9, 9, e[0])

    # the facet split, which is what makes it cut glass rather than an organ
    c.line(16, 9, 16, 26, OCC)
    c.line(11, 14, 16, 20, OCC)
    c.line(21, 14, 16, 20, OCC)
    _shadow(c, 16, 9)
    return c


# --- defence ----------------------------------------------------------------------------------

def aegis_sigil():
    c = raster.Canvas(CELL, CELL, None)
    shield(c, 16, 5, 28, 10, "steel")
    _shadow(c, 16, 7)
    return c


def iron_fang():
    """A band of iron teeth. The relic that hurts whatever touches you."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    plate(c, 5, 13, 27, 19, "steel", rivets=((8, 16), (24, 16)))
    for bx in range(7, 26, 4):
        c.poly([(bx, 19), (bx + 2, 26), (bx + 4, 19), (bx, 19)], mid)
        c.line(bx, 19, bx + 2, 25, lit)
        c.set(bx + 2, 26, hi)
        c.poly([(bx, 13), (bx + 2, 8), (bx + 4, 13), (bx, 13)], shade)
    _shadow(c, 16, 10)
    return c


def basalt_carapace():
    """Overlapping stone plates, like a shell."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("stone")
    for i, (y, half) in enumerate(((9, 6), (14, 9), (19, 11), (24, 10))):
        c.poly([(16 - half, y + 4), (16 - half + 2, y), (16 + half - 2, y),
                (16 + half, y + 4), (16 - half, y + 4)], mid if i % 2 else shade)
        c.line(16 - half + 2, y, 16 + half - 2, y, lit)
        c.hline(16 - half, 16 + half, y + 4, OCC)
    c.set(12, 10, hi)
    _shadow(c, 16, 11)
    return c


def aegis_crown():
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("gold")
    c.rect(7, 19, 25, 24, mid)
    c.hline(7, 25, 19, lit)
    c.hline(7, 25, 24, deep)
    for (px, top) in ((8, 9), (16, 6), (24, 9)):
        c.poly([(px - 3, 19), (px, top), (px + 3, 19), (px - 3, 19)], mid)
        c.line(px - 3, 19, px, top, lit)
        c.set(px, top, hi)
    for (gx, gy) in ((11, 21), (16, 21), (21, 21)):
        c.disc(gx, gy, 1.6, 1.6, shade)
        c.set(gx - 1, gy - 1, hi)
    _shadow(c, 16, 10)
    return c


def ironhide_cloak():
    """A hanging cloak with a metal clasp. Cloth, so it is the only soft silhouette here."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("wool")
    for y in range(8, 28):
        t = (y - 8) / 20.0
        w = 4.0 + 8.0 * t
        c.hline(16 - w, 16 + w, y, mid)
        c.set(16 - w, y, lit)
        c.set(16 + w, y, deep)
    for off in (-6, 0, 6):
        for y in range(11, 28):
            c.set(16 + off * (0.4 + 0.6 * (y - 8) / 20.0), y, shade if (y + off) % 3 else deep)
    for (hx, hy) in ((9, 27), (14, 28), (20, 27), (24, 26)):
        c.set(hx, hy, deep)
    band(c, 16, 8, 3.0, "steel", 2)
    _shadow(c, 16, 11)
    return c


def protective_ward():
    """A shield with a lit rune set into it."""
    c = raster.Canvas(CELL, CELL, None)
    shield(c, 16, 5, 28, 10, "steel", boss_ramp=bl.ELEMENTS["light"])
    _shadow(c, 16, 7)
    return c


# --- healing ----------------------------------------------------------------------------------

def vial_of_vitality():
    c = raster.Canvas(CELL, CELL, None)
    vessel(c, 16, 10, 26, 6, "steel", bl.ELEMENTS["grass"], fill_from=17)
    _shadow(c, 16, 7)
    return c


def heart_of_renewal():
    """A heart with light in it. Flesh row, which is the one warm material in the palette."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("flesh")
    for y in range(9, 27):
        t = (y - 9) / 18.0
        w = 9.5 * (1.0 - t ** 2.4) if t > 0.2 else 9.5
        c.hline(16 - w, 16 + w, y, mid)
        c.set(16 - w, y, lit)
        c.set(16 + w, y, deep)
    c.disc(11, 11, 4.2, 4.2, mid)
    c.disc(21, 11, 4.2, 4.2, mid)
    c.disc(10, 10, 2.2, 2.2, lit)
    c.set(9, 9, hi)
    gem(c, 16, 18, 3.0, bl.ELEMENTS["light"], facets=False)
    _shadow(c, 16, 8)
    return c


def phylactery():
    """A small locked casket. It holds the extra life, so it is shut."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("gold")
    plate(c, 7, 14, 25, 26, "gold", rivets=((10, 24), (22, 24)))
    c.poly([(7, 14), (11, 9), (21, 9), (25, 14), (7, 14)], shade)
    c.line(11, 9, 21, 9, lit)
    c.rect(14, 16, 18, 21, deep)
    c.disc(16, 18, 1.4, 1.4, OCC)
    gem(c, 16, 11, 2.0, bl.ELEMENTS["darkness"], facets=False)
    _shadow(c, 16, 10)
    return c


def essence_chalice():
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("gold")
    for y in range(8, 18):
        t = (y - 8) / 10.0
        w = 8.0 - 4.5 * (t ** 1.8)
        c.hline(16 - w, 16 + w, y, mid)
        c.set(16 - w, y, lit)
        c.set(16 + w, y, deep)
    c.rect(11, 8, 21, 11, bl.ELEMENTS["arcane"][2])
    c.hline(11, 21, 8, bl.ELEMENTS["arcane"][1])
    c.set(14, 8, bl.ELEMENTS["arcane"][0])
    c.rect(15, 18, 17, 24, shade)
    c.vline(15, 18, 24, mid)
    c.rect(11, 25, 21, 26, mid)
    c.hline(11, 21, 25, lit)
    _shadow(c, 16, 9)
    return c


# --- utility ----------------------------------------------------------------------------------

def quicksilver_pendant():
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    for y in range(3, 12):
        c.set(9 + (y - 3) * 0.55, y, shade)
        c.set(23 - (y - 3) * 0.55, y, shade)
    band(c, 16, 13, 2.6, "steel", 1)
    _lit_disc(c, 16, 20, 6.5, "steel")
    gem(c, 16, 20, 3.4, bl.ELEMENTS["water"], facets=False)
    _shadow(c, 16, 7)
    return c


def haste_rune():
    """A rune stone with the motion cut into it rather than painted beside it."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("stone")
    c.poly([(9, 7), (23, 6), (25, 24), (8, 26), (9, 7)], mid)
    c.line(9, 7, 23, 6, lit)
    c.line(8, 26, 25, 24, deep)
    c.vline(9, 7, 26, lit)
    w = bl.ELEMENTS["wind"]
    for (y, x0, x1) in ((11, 11, 21), (16, 12, 23), (21, 11, 20)):
        c.hline(x0, x1, y, w[2])
        c.hline(x0 + 2, x1, y - 1, w[3])
        c.set(x1, y, w[0])
    c.set(10, 8, hi)
    _shadow(c, 16, 9)
    return c


def compass_rose():
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("gold")
    _lit_disc(c, 16, 16, 12.0, "gold")
    c.disc(16, 16, 9.0, 9.0, OCC)
    for (dx, dy) in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        c.poly([(16, 16), (16 + dx * 8 - dy * 2, 16 + dy * 8 - dx * 2),
                (16 + dx * 8 + dy * 2, 16 + dy * 8 + dx * 2), (16, 16)],
               lit if (dx + dy) < 0 else shade)
    c.disc(16, 16, 1.8, 1.8, bl.ELEMENTS["arcane"][1])
    c.set(16, 16, bl.ELEMENTS["arcane"][0])
    _shadow(c, 16, 11)
    return c


def lucky_coin():
    c = raster.Canvas(CELL, CELL, None)
    _lit_disc(c, 16, 17, 11.0, "gold")
    hi, lit, mid, shade, deep = _ramp("gold")
    c.ring(16, 17, 7.5, 7.5, shade, 1)
    for deg in range(0, 360, 72):
        a = math.radians(deg - 90)
        c.set(16 + math.cos(a) * 4.4, 17 + math.sin(a) * 4.4, hi)
    c.disc(16, 17, 2.0, 2.0, lit)
    _shadow(c, 16, 10)
    return c


# --- elemental --------------------------------------------------------------------------------

def storm_lattice():
    """A metal frame with current running across it."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    for x in (8, 16, 24):
        c.vline(x, 6, 26, mid)
        c.vline(x + 1, 6, 26, deep)
    for y in (8, 16, 24):
        c.hline(7, 25, y, mid)
        c.hline(7, 25, y + 1, deep)
    l = bl.ELEMENTS["lightning"]
    for (x0, y0, x1, y1) in ((8, 8, 16, 16), (16, 16, 24, 24), (24, 8, 16, 16)):
        c.line(x0, y0, x1, y1, l[2])
        c.line(x0, y0 + 1, x1, y1 + 1, l[3])
    c.disc(16, 16, 2.2, 2.2, l[1])
    c.set(16, 16, l[0])
    c.set(8, 6, hi)
    _shadow(c, 16, 10)
    return c


def inferno_core():
    """A caged fire. The cage is what makes it an object rather than a fireball."""
    c = raster.Canvas(CELL, CELL, None)
    f = bl.ELEMENTS["fire"]
    hi, lit, mid, shade, deep = _ramp("steel")
    c.disc(16, 17, 10.0, 10.0, f[3])
    c.disc(16, 17, 7.0, 7.0, f[2])
    c.disc(16, 16, 4.0, 4.0, f[1])
    c.disc(16, 16, 1.8, 1.8, f[0])
    for deg in range(0, 360, 45):
        a = math.radians(deg)
        c.line(16, 17, 16 + math.cos(a) * 11, 17 + math.sin(a) * 11, shade)
    band(c, 16, 17, 11.0, "steel", 2)
    c.set(9, 11, hi)
    _shadow(c, 16, 10)
    return c


def frozen_tear():
    c = raster.Canvas(CELL, CELL, None)
    i = bl.ELEMENTS["ice"]

    # A round bottom with a point drawn ON it, rather than one profile trying to be both. The
    # single-formula version came out as a cone every time.
    c.disc(16, 21, 8.0, 8.0, i[3])
    for y in range(5, 21):
        t = (y - 5) / 16.0
        w = 8.0 * (t ** 2.1)
        if w < 0.4:
            continue
        c.hline(16 - w, 16 + w, y, i[3])

    c.disc(15, 20, 5.0, 5.0, i[2])
    for y in range(9, 21):
        t = (y - 9) / 12.0
        w = 5.0 * (t ** 2.0)
        c.hline(15 - w, 15 + w, y, i[2])
    c.disc(14, 19, 2.2, 2.2, i[1])
    c.set(13, 18, i[0])
    c.line(16, 6, 16, 14, i[1])
    c.set(16, 5, i[0])
    _shadow(c, 16, 8)
    return c


def thunderstone():
    """A stone with a live crack through it."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("stone")
    c.poly([(7, 12), (14, 5), (25, 9), (26, 22), (16, 27), (7, 21), (7, 12)], mid)
    c.line(7, 12, 14, 5, lit)
    c.line(14, 5, 25, 9, lit)
    c.line(16, 27, 26, 22, deep)
    l = bl.ELEMENTS["lightning"]
    for (x0, y0, x1, y1) in ((12, 8, 17, 15), (17, 15, 13, 19), (13, 19, 21, 25)):
        c.line(x0, y0, x1, y1, l[2])
        c.line(x0 + 1, y0, x1 + 1, y1, l[3])
    c.set(17, 15, l[0])
    c.set(9, 12, hi)
    _shadow(c, 16, 9)
    return c


def crystal_prism():
    c = raster.Canvas(CELL, CELL, None)
    a = bl.ELEMENTS["arcane"]
    c.poly([(16, 4), (26, 16), (16, 28), (6, 16), (16, 4)], a[3])
    c.poly([(16, 4), (26, 16), (16, 16), (16, 4)], a[2])
    c.poly([(16, 16), (16, 28), (6, 16), (16, 16)], a[2])
    c.line(16, 4, 16, 28, OCC)
    c.line(6, 16, 26, 16, OCC)
    c.line(16, 5, 25, 16, a[1])
    c.set(16, 4, a[0])
    _shadow(c, 16, 9)
    return c


# --- rule-changers ----------------------------------------------------------------------------

def cracked_prism():
    """The prism again, broken. The pair has to read as before and after."""
    c = crystal_prism()
    a = bl.ELEMENTS["arcane"]
    for (x0, y0, x1, y1) in ((11, 10, 19, 20), (19, 20, 15, 26), (19, 11, 22, 21)):
        c.line(x0, y0, x1, y1, OCC)
        c.line(x0 + 1, y0, x1 + 1, y1, a[3])
    c.set(19, 20, a[1])
    return c


def duellists_chalk():
    """A stub of chalk and the line it has drawn. The line is the relic, not the stick."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("skin")

    # A fat upright stub, and the line it has drawn much bolder beneath it. The relic is the LINE
    # - the duel is fought across it - so the line gets the weight rather than the stick.
    c.rect(12, 4, 20, 19, mid)
    c.hline(12, 20, 4, lit)
    c.vline(12, 4, 19, lit)
    c.vline(20, 4, 19, deep)
    c.hline(12, 20, 19, deep)
    c.set(13, 5, hi)
    for y in (6, 11, 16):
        c.hline(13, 19, y, shade)

    for x in range(3, 30, 5):
        c.rect(x, 24, x + 3, 26, lit)
        c.hline(x, x + 3, 26, shade)
    _shadow(c, 16, 6)
    return c


def hoarfrost_nail():
    """An iron nail with frost grown along it."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")

    # A big square head and a shank five pixels across. A spike drawn two pixels wide reads as a
    # line, and a line with frost on it reads as nothing at all.
    c.rect(8, 4, 24, 9, mid)
    c.hline(8, 24, 4, lit)
    c.hline(8, 24, 9, deep)
    c.vline(8, 4, 9, lit)
    for y in range(9, 29):
        t = (y - 9) / 20.0
        w = 4.0 * (1.0 - t ** 1.6)
        if w < 0.4:
            continue
        c.hline(16 - w, 16 + w, y, mid)
        c.set(16 - w, y, lit)
        c.set(16 + w, y, deep)
    c.set(16, 29, hi)

    # Frost: branched, on both sides, and drawn as spans so it has body.
    i = bl.ELEMENTS["ice"]
    for (fx, fy, dx, dy) in ((12, 14, -6, -4), (20, 18, 6, -4), (11, 22, -5, -3), (21, 25, 5, -3)):
        for k in range(5):
            t = k / 4.0
            c.set(fx + dx * t, fy + dy * t, i[2] if k % 2 else i[3])
            c.set(fx + dx * t, fy + dy * t + 1, i[3])
        c.set(fx + dx, fy + dy, i[0])
    c.set(10, 5, hi)
    _shadow(c, 16, 8)
    return c


def wormwood_tithe():
    """A bound sprig of bitter herb. The only relic that is a plant."""
    c = raster.Canvas(CELL, CELL, None)
    g = bl.ELEMENTS["grass"]
    p = bl.ELEMENTS["poison"]
    hi, lit, mid, shade, deep = _ramp("wool")
    for sign, lean in ((-1, 7), (0, 0), (1, -7)):
        for k in range(12):
            t = k / 11.0
            x = 16 + sign * 6 * t + lean * 0.0
            y = 22 - 16 * t
            c.set(x, y, g[3] if k % 2 else g[2])
            if k % 4 == 3:
                c.set(x - sign * 2 - 1, y + 1, p[3])
                c.set(x - sign * 2 - 1, y, p[2])
    c.set(16, 6, g[0])
    c.rect(13, 22, 19, 25, mid)
    c.hline(13, 19, 22, lit)
    c.hline(13, 19, 25, deep)
    _shadow(c, 16, 7)
    return c


# --- the ten Full Set Enchantments --------------------------------------------------------------
# Each set is the pieces of one real object, so each icon is that object whole.

def set_vaultguard():
    """A vault door: the thing the key, the shield and the plates are parts of."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    plate(c, 4, 4, 27, 27, "steel",
          rivets=((7, 7), (24, 7), (7, 24), (24, 24)))
    band(c, 16, 16, 8.0, "steel", 2)
    for deg in range(0, 360, 45):
        a = math.radians(deg)
        c.line(16 + math.cos(a) * 4, 16 + math.sin(a) * 4,
               16 + math.cos(a) * 9, 16 + math.sin(a) * 9, shade)
    c.disc(16, 16, 3.0, 3.0, deep)
    c.set(15, 15, hi)
    return c


def set_emberline():
    """A lantern, lit. Emberline is the fire set, so the object is the thing that carries fire."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    band(c, 16, 5, 3.0, "steel", 1)
    c.rect(9, 9, 23, 11, mid)
    c.hline(9, 23, 9, lit)
    f = bl.ELEMENTS["fire"]
    c.rect(11, 11, 21, 23, f[3])
    c.rect(13, 13, 19, 23, f[2])
    c.disc(16, 19, 3.0, 4.0, f[1])
    c.disc(16, 19, 1.4, 2.0, f[0])
    c.vline(10, 11, 23, mid)
    c.vline(22, 11, 23, deep)
    c.rect(9, 24, 23, 26, mid)
    c.hline(9, 23, 24, lit)
    c.hline(9, 23, 26, deep)
    _shadow(c, 16, 9)
    return c


def set_stormbound():
    """A coil with current across it."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    for i, y in enumerate(range(8, 26, 4)):
        c.hline(9, 23, y, mid)
        c.hline(9, 23, y + 1, deep)
        c.set(9 if i % 2 else 23, y, lit)
    c.vline(8, 8, 26, shade)
    c.vline(24, 8, 26, shade)
    l = bl.ELEMENTS["lightning"]
    c.line(16, 4, 13, 14, l[2])
    c.line(13, 14, 19, 17, l[2])
    c.line(19, 17, 15, 28, l[2])
    c.line(17, 4, 14, 14, l[3])
    c.set(13, 14, l[0])
    c.set(16, 4, l[1])
    return c


def set_bastion():
    """A shield wall of spikes."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    shield(c, 16, 8, 29, 11, "steel")
    for (px, top) in ((8, 3), (16, 1), (24, 3)):
        c.poly([(px - 2, 9), (px, top), (px + 2, 9), (px - 2, 9)], mid)
        c.line(px - 2, 9, px, top, lit)
        c.set(px, top, hi)
    gem(c, 16, 16, 3.0, bl.ELEMENTS["metal"], facets=False)
    return c


def set_deathbringer():
    """A headsman's axe. The damage set, and the bluntest object in the ten."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    wh, wl, wm, ws, wd = _ramp("skin")

    # The haft is bone BASE, not deep. Drawn in the darkest stop it was invisible against the
    # card and the whole icon read as a floating grey wedge.
    c.rect(14, 3, 18, 29, wm)
    c.vline(14, 3, 29, wl)
    c.vline(18, 3, 29, wd)
    c.set(15, 4, wh)

    # A crescent head: an outer arc and a bite taken out of the inner edge, which is what makes
    # an axe an axe rather than a trapezoid.
    # Built from TWO arcs, a convex outer edge and a convex inner one, so the blade is thick at
    # the horns and thin at the waist. The first version filled everything between the haft and
    # one arc, which is a half-disc: it read as a round shield bolted to a stick.
    for y in range(4, 26):
        t = (y - 15.0) / 11.0
        if abs(t) >= 1.0:
            continue
        outer = 13.0 * (1.0 - t * t) ** 0.5
        inner = 5.5 * (1.0 - t * t)
        if outer - inner < 0.6:
            continue
        c.hline(18 + inner, 18 + outer, y, mid)
        c.set(18 + outer, y, lit)
        c.set(18 + outer - 1, y, lit)
        c.set(18 + inner, y, deep)
    c.rect(17, 12, 24, 18, mid)
    c.hline(17, 24, 12, lit)
    c.hline(17, 24, 18, deep)
    c.set(30, 15, hi)

    # A counterweight on the other side, so the head is not hanging off one edge of the cell.
    c.poly([(14, 10), (6, 12), (6, 18), (14, 20), (14, 10)], shade)
    c.line(14, 10, 6, 12, mid)
    c.set(7, 12, lit)
    # A bound pommel rather than a gem: a stone on the butt of an axe reads as a lollipop.
    c.rect(13, 25, 19, 28, shade)
    c.hline(13, 19, 25, mid)
    c.hline(13, 19, 28, deep)
    return c


def set_eternal_guardian():
    """A closed helm."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("steel")
    for y in range(6, 25):
        t = (y - 6) / 19.0
        w = 10.0 - 2.0 * (t ** 2)
        c.hline(16 - w, 16 + w, y, mid)
        c.set(16 - w, y, lit)
        c.set(16 + w, y, deep)
    c.hline(7, 25, 6, lit)
    c.rect(9, 14, 23, 16, OCC)
    c.hline(9, 23, 13, shade)
    for x in range(11, 23, 4):
        c.vline(x, 18, 23, OCC)
    c.rect(6, 25, 26, 27, shade)
    c.hline(6, 26, 25, mid)
    c.set(9, 8, hi)
    return c


def set_life_drain():
    """A chalice catching a falling drop."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("gold")
    f = bl.ELEMENTS["grass"]
    for y in range(14, 23):
        t = (y - 14) / 9.0
        w = 8.0 - 4.5 * (t ** 1.8)
        c.hline(16 - w, 16 + w, y, mid)
        c.set(16 - w, y, lit)
        c.set(16 + w, y, deep)
    c.rect(9, 14, 23, 16, f[2])
    c.hline(9, 23, 14, f[1])
    c.rect(15, 23, 17, 27, shade)
    c.rect(11, 28, 21, 29, mid)
    for (dy, r) in ((4, 2.2), (9, 1.6)):
        c.disc(16, dy, r, r * 1.3, f[3])
        c.set(16, dy - 1, f[0])
    return c


def set_elemental_mastery():
    """Four stones in a ring, one per element the set touches."""
    c = raster.Canvas(CELL, CELL, None)
    band(c, 16, 16, 12.0, "gold", 2)
    for deg, el in ((270, "fire"), (0, "lightning"), (90, "grass"), (180, "ice")):
        a = math.radians(deg)
        gem(c, 16 + math.cos(a) * 12, 16 + math.sin(a) * 12, 3.6, bl.ELEMENTS[el], facets=False)
    gem(c, 16, 16, 4.0, bl.ELEMENTS["arcane"], facets=True)
    return c


def set_speed_demon():
    """A winged boot."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("flesh")
    c.poly([(10, 10), (18, 10), (19, 21), (27, 22), (27, 27), (9, 27), (10, 10)], mid)
    c.line(10, 10, 18, 10, lit)
    c.hline(9, 27, 27, deep)
    c.vline(10, 10, 27, lit)
    c.hline(10, 19, 18, shade)
    w = bl.ELEMENTS["wind"]
    for (wy, wl) in ((12, 9), (15, 7), (18, 5)):
        c.hline(10 - wl, 9, wy, w[2])
        c.set(10 - wl, wy, w[0])
    c.set(11, 11, hi)
    return c


def set_fortunes_favor():
    """A stack of coins. The only set object that is a QUANTITY rather than a thing."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = _ramp("gold")
    for i, y in enumerate((25, 21, 17)):
        half = 10 - i
        c.rect(16 - half, y - 2, 16 + half, y, mid)
        c.hline(16 - half, 16 + half, y - 2, lit)
        c.hline(16 - half, 16 + half, y, deep)
        c.set(16 - half, y - 2, hi)
    _lit_disc(c, 16, 10, 6.5, "gold")
    c.ring(16, 10, 3.6, 3.6, shade, 1)
    return c


RELICS = [
    ("relic_key", relic_key), ("ember_flask", ember_flask), ("wrath_amulet", wrath_amulet),
    ("ethereal_blade", ethereal_blade), ("spectral_fang", spectral_fang),
    ("obsidian_heart", obsidian_heart), ("aegis_sigil", aegis_sigil), ("iron_fang", iron_fang),
    ("basalt_carapace", basalt_carapace), ("aegis_crown", aegis_crown),
    ("ironhide_cloak", ironhide_cloak), ("protective_ward", protective_ward),
    ("vial_of_vitality", vial_of_vitality), ("heart_of_renewal", heart_of_renewal),
    ("phylactery", phylactery), ("essence_chalice", essence_chalice),
    ("quicksilver_pendant", quicksilver_pendant), ("haste_rune", haste_rune),
    ("compass_rose", compass_rose), ("lucky_coin", lucky_coin),
    ("storm_lattice", storm_lattice), ("inferno_core", inferno_core),
    ("frozen_tear", frozen_tear), ("thunderstone", thunderstone),
    ("crystal_prism", crystal_prism), ("cracked_prism", cracked_prism),
    ("duellists_chalk", duellists_chalk), ("hoarfrost_nail", hoarfrost_nail),
    ("wormwood_tithe", wormwood_tithe),
]

SETS = [
    ("vaultguard", set_vaultguard), ("emberline", set_emberline), ("stormbound", set_stormbound),
    ("bastion_of_spikes", set_bastion), ("deathbringer", set_deathbringer),
    ("eternal_guardian", set_eternal_guardian), ("life_drain", set_life_drain),
    ("elemental_mastery", set_elemental_mastery), ("speed_demon", set_speed_demon),
    ("fortunes_favor", set_fortunes_favor),
]


def _emit(out_dir, icons, label):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    ok = True
    for name, fn in icons:
        canvas = fn()
        bad = canvas.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s:" % name)
            for h, n in bad[:6]:
                print("   %s  x%d" % (h, n))
            continue
        canvas.scaled(2).save(os.path.join(out_dir, name + ".png"))
    print("  %-22s %d icons" % (label, len(icons)))
    return ok


def main():
    ok = _emit(OUT_RELICS, RELICS, "relics")
    ok &= _emit(OUT_SETS, SETS, "full set enchantments")
    if not ok:
        return 1
    print("  %d icons, palette clean" % (len(RELICS) + len(SETS)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
