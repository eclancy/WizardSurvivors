# -*- coding: utf-8 -*-
"""Boon icons. Run: python tools/art/boon_icons.py

Fourteen 32x32 icons, one per boon, rendered at x2 to the 32x32 UI cell in art-direction.md
section 1 - the same cell the spell icons use, because a boon is offered on the same card as a
spell and has to survive the comparison.

Until now boons had no icon at all. LevelUpOption fell through to DefaultSpellIcon and tinted it by
the boon's dominant element, so all fourteen were the same picture in slightly different colours -
which is worse than no icon, because it reads as a spell the player has already seen.

WHAT MAKES A BOON ICON DIFFERENT FROM A SPELL ICON. A spell icon is an effect: emissive, white-hot
core, identity in the mid and edge. A boon is an OBJECT the player is carrying - a bead, a flask, a
coin, a scrap of cloth. So these are drawn on the MATERIAL rows, lit from the upper left like any
other object in the game, and they use the element ramps only where the object is genuinely glowing.
That difference is deliberate and it is the main thing telling the player, at a glance, that this
card is not a spell.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "boons")

CELL = 32


def _ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


def _lit_disc(c, cx, cy, r, mat):
    """A round object with the key light on its upper left, the house lighting model."""
    hi, lit, base, shade, deep = _ramp(mat)
    c.disc(cx, cy, r, r, base)
    for deg in range(0, 360, 3):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(225.0))
        tone = hi if facing > 0.72 else (lit if facing > 0.1 else (deep if facing < -0.5 else shade))
        c.set(cx + math.cos(a) * (r - 0.4), cy + math.sin(a) * (r - 0.4), tone)
    c.set(cx - r * 0.38, cy - r * 0.38, hi)


def _shadow(c, cx, w):
    c.hline(int(cx - w), int(cx + w), 29, OCC)


# --- the fourteen ----------------------------------------------------------------

def iron_rivets():
    """A plate with four rivets through it. Metal and Earth, armour."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("steel")
    c.poly([(7, 7), (25, 8), (26, 24), (8, 25), (7, 7)], base)
    c.hline(7, 25, 7, lit)
    c.vline(7, 7, 25, lit)
    c.hline(8, 26, 25, deep)
    c.vline(26, 8, 24, shade)
    for (x, y) in [(11, 11), (21, 11), (11, 21), (21, 21)]:
        c.disc(x, y, 2.2, 2.2, shade)
        c.set(x - 1, y - 1, hi)
        c.set(x + 1, y + 1, deep)
    _shadow(c, 16, 9)
    return c


def quicksilver_bead():
    """A bead of running metal that never sits still. Metal and Lightning, cooldowns."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("skin")
    _lit_disc(c, 15.5, 15.0, 8.0, "skin")
    # It is a liquid, so it sheds droplets rather than holding an edge.
    for (x, y, t) in [(24, 9, lit), (26, 12, base), (7, 22, base), (5, 19, shade)]:
        c.set(x, y, t)
    c.disc(24, 21, 2.0, 2.0, base)
    c.set(23, 20, hi)
    _shadow(c, 16, 8)
    return c


def sunsteel_filament():
    """A drawn thread of warm metal, coiled. Light and Metal, armour."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("gold")
    glow = bl.ELEMENTS["light"][1]
    for r in (10.5, 7.5, 4.5):
        c.ring(16, 16, r, r, base, 1)
    for deg in range(180, 330, 6):
        a = math.radians(deg)
        for r in (10.5, 7.5, 4.5):
            c.set(16 + math.cos(a) * r, 16 + math.sin(a) * r, lit)
    c.set(16, 16, glow)
    c.set(9, 9, hi)
    c.set(23, 23, deep)
    _shadow(c, 16, 9)
    return c


def tidewater_flask():
    """Salt water in a stoppered bottle. Water and Ice, regeneration."""
    c = raster.Canvas(CELL, CELL, None)
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("linen")
    water = bl.ELEMENTS["water"][2]
    water_hi = bl.ELEMENTS["water"][1]
    c.poly([(12, 7), (19, 7), (19, 12), (23, 18), (23, 26), (8, 26), (8, 18), (12, 12), (12, 7)], g_base)
    c.poly([(10, 18), (21, 18), (21, 24), (10, 24), (10, 18)], water)
    c.hline(10, 21, 18, water_hi)
    c.vline(9, 19, 25, g_lit)
    c.vline(22, 19, 25, g_deep)
    c.rect(11, 5, 20, 7, g_shade)
    c.hline(11, 20, 5, g_hi)
    c.set(12, 20, water_hi)
    _shadow(c, 16, 8)
    return c


def saltbound_chain():
    """Three crusted links. Water and Metal, it lashes back."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("steel")
    salt = bl.tone("linen", "hi")
    for (cx, cy) in [(9, 10), (16, 16), (23, 22)]:
        c.ring(cx, cy, 4.2, 3.4, base, 2)
        c.set(cx - 3, cy - 2, lit)
        c.set(cx + 3, cy + 2, deep)
    for (x, y) in [(7, 7), (18, 13), (25, 19), (12, 19)]:
        c.set(x, y, salt)
    _shadow(c, 18, 7)
    return c


def deepwater_pearl():
    """A pearl that draws things toward it. Water and Arcane, pickup range."""
    c = raster.Canvas(CELL, CELL, None)
    _lit_disc(c, 16, 16, 7.5, "linen")
    pull = bl.ELEMENTS["water"][2]
    for r in (11.0, 13.5):
        for deg in range(0, 360, 30):
            a = math.radians(deg)
            c.set(16 + math.cos(a) * r, 16 + math.sin(a) * r, pull)
    _shadow(c, 16, 7)
    return c


def lantern_oil():
    """A flask of oil that burns hotter than it should. Light and Fire, spell damage."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("amber")
    flame = bl.ELEMENTS["fire"][1]
    flame_core = bl.ELEMENTS["fire"][0]
    c.poly([(11, 13), (20, 13), (23, 19), (23, 26), (8, 26), (8, 19), (11, 13)], base)
    c.vline(9, 19, 25, lit)
    c.vline(22, 19, 25, deep)
    c.hline(9, 22, 26, deep)
    c.rect(13, 10, 18, 13, shade)
    # The wick, lit.
    c.poly([(15, 4), (17, 4), (18, 8), (16, 10), (14, 8), (15, 4)], flame)
    c.set(16, 7, flame_core)
    c.set(16, 6, flame_core)
    _shadow(c, 16, 8)
    return c


def gilded_mote():
    """A fleck of a warded circle, still alight. Light and Arcane, maximum health."""
    c = raster.Canvas(CELL, CELL, None)
    edge = bl.ELEMENTS["light"][3]
    mid = bl.ELEMENTS["light"][2]
    hot = bl.ELEMENTS["light"][1]
    core = bl.ELEMENTS["light"][0]
    c.disc(16, 16, 8.0, 8.0, edge)
    c.disc(16, 16, 5.5, 5.5, mid)
    c.disc(16, 16, 3.2, 3.2, hot)
    c.disc(16, 16, 1.6, 1.6, core)
    for (x, y) in [(16, 4), (16, 28), (4, 16), (28, 16)]:
        c.set(x, y, hot)
    for (x, y) in [(8, 8), (24, 8), (8, 24), (24, 24)]:
        c.set(x, y, mid)
    return c


def mossgrown_charm():
    """An old charm the wood has taken back. Grass and Earth, maximum health."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("gold")
    moss = bl.tone("lichen", "base")
    moss_lit = bl.tone("lichen", "lit")
    c.ring(16, 18, 7.5, 7.5, base, 2)
    c.set(12, 13, hi)
    c.set(21, 24, deep)
    c.vline(16, 5, 11, shade)
    c.hline(13, 19, 5, lit)
    for (x, y) in [(10, 21), (11, 23), (13, 24), (20, 13), (22, 15), (9, 17)]:
        c.set(x, y, moss)
    for (x, y) in [(11, 22), (21, 14)]:
        c.set(x, y, moss_lit)
    _shadow(c, 16, 7)
    return c


def thornseed():
    """A seed that puts out roots wherever it lands. Grass and Poison, spell area."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("verdant")
    husk = bl.tone("amber", "shade")
    c.disc(16, 15, 6.0, 7.0, husk)
    c.set(13, 11, bl.tone("amber", "lit"))
    c.set(19, 20, bl.tone("amber", "deep"))
    for (x0, y0, x1, y1) in [(16, 9, 16, 4), (11, 12, 6, 8), (21, 12, 26, 8),
                             (11, 19, 6, 23), (21, 19, 26, 23)]:
        c.line(x0, y0, x1, y1, base)
    for (x, y) in [(16, 4), (6, 8), (26, 8), (6, 23), (26, 23)]:
        c.set(x, y, lit)
    _shadow(c, 16, 6)
    return c


def shadegrease():
    """A pot of something nothing gets hold of. Darkness and Wind, movement."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("stone")
    slick = bl.ELEMENTS["darkness"][2]
    slick_hi = bl.ELEMENTS["darkness"][1]
    c.poly([(9, 14), (23, 14), (22, 26), (10, 26), (9, 14)], base)
    c.vline(10, 15, 25, lit)
    c.vline(22, 15, 25, deep)
    c.hline(9, 23, 13, shade)
    c.poly([(11, 12), (21, 12), (20, 15), (12, 15), (11, 12)], slick)
    c.hline(12, 19, 12, slick_hi)
    for (x, y) in [(24, 18), (25, 21)]:
        c.set(x, y, slick)
    _shadow(c, 16, 7)
    return c


def wishing_coin():
    """Thrown into still water by someone who is not coming back. Water and Light, luck."""
    c = raster.Canvas(CELL, CELL, None)
    _lit_disc(c, 16, 15, 8.5, "gold")
    mark = bl.tone("gold", "deep")
    c.vline(16, 11, 19, mark)
    c.hline(13, 19, 15, mark)
    ripple = bl.ELEMENTS["water"][2]
    c.hline(6, 26, 27, ripple)
    c.hline(9, 23, 29, ripple)
    return c


def umbral_veil():
    """A hand's width of somewhere else. Darkness and Ice, evasion."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("violet")
    c.poly([(7, 6), (25, 6), (24, 20), (20, 26), (16, 21), (12, 26), (8, 20), (7, 6)], base)
    c.hline(7, 25, 6, lit)
    c.vline(8, 7, 20, lit)
    c.vline(24, 7, 20, deep)
    for y in range(9, 22, 3):
        c.hline(10, 22, y, shade)
    c.set(11, 8, hi)
    return c


def rimebriar():
    """Frost-blackened thorns that close on whatever reaches you. Ice and Grass."""
    c = raster.Canvas(CELL, CELL, None)
    stem = bl.tone("verdant", "deep")
    frost = bl.ELEMENTS["ice"][1]
    frost_core = bl.ELEMENTS["ice"][0]
    c.line(6, 26, 14, 14, stem)
    c.line(14, 14, 24, 7, stem)
    c.line(26, 25, 18, 15, stem)
    for (x0, y0, x1, y1) in [(10, 20, 6, 17), (17, 11, 14, 6), (21, 10, 25, 12), (22, 20, 26, 19)]:
        c.line(x0, y0, x1, y1, stem)
        c.set(x1, y1, frost)
    for (x, y) in [(9, 21), (14, 14), (20, 10), (24, 7), (21, 18)]:
        c.set(x, y, frost)
    c.set(14, 13, frost_core)
    c.set(24, 6, frost_core)
    _shadow(c, 16, 9)
    return c


ICONS = [
    ("iron_rivets", iron_rivets),
    ("quicksilver_bead", quicksilver_bead),
    ("sunsteel_filament", sunsteel_filament),
    ("tidewater_flask", tidewater_flask),
    ("saltbound_chain", saltbound_chain),
    ("deepwater_pearl", deepwater_pearl),
    ("lantern_oil", lantern_oil),
    ("gilded_mote", gilded_mote),
    ("mossgrown_charm", mossgrown_charm),
    ("thornseed", thornseed),
    ("shadegrease", shadegrease),
    ("wishing_coin", wishing_coin),
    ("umbral_veil", umbral_veil),
    ("rimebriar", rimebriar),
]


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    ok = True
    for name, fn in ICONS:
        canvas = fn()
        bad = canvas.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s:" % name)
            for h, n in bad[:6]:
                print("   %s  x%d" % (h, n))
            continue
        canvas.scaled(2).save(os.path.join(OUT_DIR, name + ".png"))
        print("  %-20s 64x64" % name)

    if not ok:
        return 1
    print("")
    print("%d boon icons, palette clean" % len(ICONS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
