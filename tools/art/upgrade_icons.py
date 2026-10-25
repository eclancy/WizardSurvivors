# -*- coding: utf-8 -*-
"""Arcane Upgrade icons. Run: python tools/art/upgrade_icons.py

Twenty 32x32 icons, one per permanent upgrade sold in the main menu's Arcane Upgrades shop
(MainMenu.BuildUpgradeDefinitions), rendered at x2 to the 32x32 UI cell in art-direction.md
section 1 - the cell the spell and boon icons use. The ids are the shop's ids, and the C# loads
res://assets/bonelight/ui/upgrades/<id>.png by them: rename one here and rename it there.

Until now every row in that shop led with a 24px stat glyph and nothing else, so twenty rows were
twenty sentences told apart only by reading them (.ai/level-up-screen.md, "Arcane Upgrades").

WHAT AN UPGRADE ICON IS. A spell icon is an EFFECT - emissive, white-hot core, identity in the mid
and edge. A boon icon is an OBJECT you carry, on the material rows. An arcane upgrade is a power
the wizard has bound into himself for good, so each one is drawn as the token of a vow or a working
- a carved tablet, an hourglass bound in thread, a phoenix's feather, a die with fate cracked
through it - lit from the upper left like any object, with its magic in the element ramps.

THEY SIT IN A SOCKET, NOT ON PAPER. The shop sets every icon in a small socket of arcane night glass
(ui_frames.py:upgrade_socket), the way the boon relic sits behind its window - so these are drawn
for a dark ground: mid and light values carry the shape, and a dark mass gets a lit rim rather than
being left to sink into the glass.

ONE MARK MAKES THEM A SET: every icon is fringed in the arcane ramp's edge tone at part alpha, the
light of the Arcane Energy they were bought with (boon_icons.py fringes each boon in ITS element;
these all share one, because they are all one kind of thing). Part alpha for the reason the boon
fringe and the cast shadows are: it has to read as light over whatever is under it, never as an
outline, which art-direction.md keeps off objects.

The hearts are the ember row, as stat_icons.py's heart and regen glyphs are: a hit point is the
player's own life, and that row is the player's. Nothing else here wears a player pigment.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "upgrades")
PREVIEW = os.path.join(ROOT, "tools", "art", "_upgrade_icons_preview.png")
SOCKET = os.path.join(ROOT, "assets", "bonelight", "ui", "ui-upgrade-socket.png")

CELL = 32
_ROLE = {"core": 0, "hot": 1, "mid": 2, "edge": 3}

# The set's mark (see the module docstring). 120 of 255: visible as light on the night glass,
# and too faint on any ground to read as a drawn line.
FRINGE_ALPHA = 120
SHADOW_ALPHA = 110


def T(material, role):
    return bl.tone(material, role)


def E(element, role):
    return bl.ELEMENTS[element][_ROLE[role]]


def _ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


def _alpha(colour, a):
    r, g, b, _ = raster.rgb(colour)
    return (r, g, b, a)


def _sprite(c, ox, oy, rows, pal):
    """ASCII rows into the canvas at (ox, oy). '.' and any letter not in `pal` are left alone."""
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in pal and pal[ch] is not None:
                c.set(ox + x, oy + y, pal[ch])


def _shadow(c, cx, w, row=29):
    c.hline(int(cx - w), int(cx + w), row, _alpha(OCC, SHADOW_ALPHA))


def _glint(c, x, y, element):
    """A four-point glint: a core pixel and four hot arms."""
    c.set(x, y, E(element, "core"))
    for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        c.set(x + dx, y + dy, E(element, "hot"))


def _lit_disc(c, cx, cy, r, mat):
    """A round body with the key on its upper left, the house lighting model (boon_icons.py)."""
    hi, lit, base, shade, deep = _ramp(mat)
    c.disc(cx, cy, r, r, base)
    for deg in range(0, 360, 3):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(225.0))
        tone = hi if facing > 0.72 else (lit if facing > 0.1 else (deep if facing < -0.5 else shade))
        c.set(cx + math.cos(a) * (r - 0.4), cy + math.sin(a) * (r - 0.4), tone)


def _heart(c, cx, top, w, h, fill):
    """A heart filled with `fill`, `w` wide and `h` tall, its lobes' tops on row `top`."""
    r = w / 4.0
    lx, rx = cx - r, cx + r
    ly = top + r
    for y in range(int(top), int(top + h) + 1):
        for x in range(int(cx - w / 2.0) - 1, int(cx + w / 2.0) + 2):
            px, py = x + 0.5, y + 0.5
            inside = ((px - lx) ** 2 + (py - ly) ** 2 <= r * r) or ((px - rx) ** 2 + (py - ly) ** 2 <= r * r)
            if not inside and py >= ly:
                # Below the lobes the sides run straight to the point.
                span = (w / 2.0) * (1.0 - (py - ly) / float(top + h - ly))
                inside = abs(px - cx) <= span
            if inside:
                c.set(x, y, fill(x, y) if callable(fill) else fill)


# --- the twenty --------------------------------------------------------------------------------

def damage():
    """Runes of Ruin: a bone tablet with a blade-rune burning in it. +spell damage."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("skin")
    c.rect(9, 5, 22, 27, base)
    c.hline(10, 21, 4, hi)
    c.hline(9, 22, 5, lit)
    c.vline(9, 6, 27, lit)
    c.vline(22, 6, 27, shade)
    c.hline(9, 22, 27, deep)
    c.hline(10, 21, 28, OCC)
    for (x, y) in ((9, 4), (22, 4), (9, 5), (22, 5)):
        c.set(x, y, (0, 0, 0, 0))
    c.set(10, 5, hi)
    # Weathering: a chipped corner and two hairline cracks, all on the shaded side of the stone.
    for (x, y) in ((21, 7), (20, 8), (21, 9), (11, 23), (12, 24), (12, 25), (19, 21), (20, 22)):
        c.set(x, y, shade)
    c.set(22, 26, deep)
    # The rune, cut first as a groove (shade) then filled with fire: two blades crossed point-up,
    # the stat glyph's sword twice over. (A single upright blade with its guard low read as an
    # inverted cross on the tablet - a symbol, not a weapon.)
    # Each blade is two pixels wide - one was a hairline X at icon size - with a guard square
    # across it and a hilt below.
    blades = []
    for i in range(0, 9):
        blades += [(12 + i, 19 - i), (13 + i, 19 - i)]
        blades += [(19 - i, 19 - i), (18 - i, 19 - i)]
    blades += [(21, 10), (10, 10)]
    guards = [(10, 19), (11, 18), (13, 21), (14, 20), (21, 19), (20, 18), (18, 21), (17, 20)]
    hilts = [(11, 21), (10, 22), (20, 21), (21, 22)]
    rune = set(blades + guards + hilts)
    for (x, y) in rune:
        for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in rune and 10 <= x + dx <= 21:
                c.set(x + dx, y + dy, shade)
    for (x, y) in blades:
        c.set(x, y, E("fire", "mid"))
    for i in range(2, 9):
        c.set(12 + i, 19 - i, E("fire", "hot"))
        c.set(18 - i, 19 - i, E("fire", "hot"))
    for (x, y) in guards:
        c.set(x, y, E("fire", "hot"))
    for (x, y) in hilts:
        c.set(x, y, E("fire", "edge"))
    for (x, y) in ((15, 13), (16, 13), (21, 10), (10, 10)):
        c.set(x, y, E("fire", "core"))
    _shadow(c, 16, 7)
    return c


def recovery():
    """Emberheart: a heart of live coal with a cross of light in it, flames licking off its top.
    Health coming back - the regen glyph's cross, in fire."""
    c = raster.Canvas(CELL, CELL, None)

    def coal(x, y):
        # Lit from the upper left, like any object: the far side falls to the ember's edge.
        d = (x - 9) + (y - 10)
        if d < 6:
            return E("fire", "mid")
        if d < 22:
            return E("fire", "edge")
        return T("red", "hi")
    _heart(c, 16, 10, 20, 18, coal)
    # Cracks of heat across the coal.
    for (x, y) in ((10, 15), (11, 16), (12, 16), (21, 13), (22, 14), (20, 21), (19, 22), (13, 21)):
        c.set(x, y, E("fire", "hot"))
    # The cross of light: regeneration (stat_icons.py regen).
    for (x, y) in ((16, 14), (16, 15), (16, 17), (16, 18), (14, 16), (15, 16), (17, 16), (18, 16)):
        c.set(x, y, E("fire", "hot"))
    c.set(16, 16, E("fire", "core"))
    # Flames off the two lobes, lit side brighter.
    _sprite(c, 7, 3, [
        "..h.......h...",
        ".hm......hm...",
        ".mm.....mm..m.",
        "mme....mme.me.",
        ".e......e..e..",
    ], {"h": E("fire", "hot"), "m": E("fire", "mid"), "e": E("fire", "edge")})
    c.set(11, 6, E("fire", "core"))
    _shadow(c, 16, 6)
    return c


def cooldowns():
    """Chronoweave: an hourglass bound with a thread of arcane light. -cooldown."""
    c = raster.Canvas(CELL, CELL, None)
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    s_hi, s_lit, s_base, s_shade, s_deep = _ramp("skin")
    # Caps.
    for y in (4, 26):
        c.hline(8, 23, y, g_base)
        c.hline(8, 23, y + 1, g_shade)
        c.hline(9, 22, y, g_lit)
    c.set(8, 4, g_hi)
    c.hline(8, 23, 27, g_deep)
    # Posts.
    c.vline(9, 6, 25, g_lit)
    c.vline(22, 6, 25, g_shade)
    # Glass: two bulbs pinched at row 15, drawn as an outline of pale bone.
    for y in range(6, 26):
        t = abs(y - 15.5) / 10.0
        half = 1.0 + 5.0 * min(1.0, t * 1.6)
        lx = int(round(15.5 - half))
        rx = int(round(15.5 + half))
        c.set(lx, y, s_lit)
        c.set(rx, y, s_shade)
    # Sand: arcane light, a little left in the top bulb, a heap in the bottom, the stream between.
    for y in range(10, 13):
        t = abs(y - 15.5) / 10.0
        half = 5.0 * min(1.0, t * 1.6)
        for x in range(int(round(15.5 - half)) + 1, int(round(15.5 + half))):
            c.set(x, y, E("arcane", "mid"))
    for y in range(20, 26):
        t = abs(y - 15.5) / 10.0
        half = 1.0 + 5.0 * min(1.0, t * 1.6)
        width = (y - 19) * 1.0
        for x in range(int(round(15.5 - min(half - 1, width))), int(round(15.5 + min(half - 1, width))) + 1):
            c.set(x, y, E("arcane", "mid") if x > 15 else E("arcane", "hot"))
    for y in range(13, 20):
        c.set(15, y, E("arcane", "hot"))
    c.set(15, 16, E("arcane", "core"))
    # The weave: a thread wound twice round the waist, crossing in front.
    for (x0, y0, x1, y1) in ((10, 12, 21, 18), (10, 18, 21, 12)):
        c.line(x0, y0, x1, y1, E("arcane", "edge"))
    for (x, y) in ((12, 13), (19, 13), (13, 17), (18, 17)):
        c.set(x, y, E("arcane", "hot"))
    _shadow(c, 16, 8, 28)
    return c


def area():
    """Aether Bloom: a flower of light opening outward in eight petals. +area and range."""
    c = raster.Canvas(CELL, CELL, None)
    cx, cy = 15.5, 15.5
    for k in range(8):
        a = math.radians(k * 45.0 - 90.0)
        long_ = 12.5 if k % 2 == 0 else 10.0
        for step in range(0, int(long_ * 4)):
            r = step / 4.0
            width = 2.6 * math.sin(math.pi * min(1.0, r / long_)) if r > 1 else 1.0
            for w in range(-3, 4):
                px = cx + math.cos(a) * r - math.sin(a) * w * 0.5
                py = cy + math.sin(a) * r + math.cos(a) * w * 0.5
                if abs(w * 0.5) > width:
                    continue
                frac = r / long_
                role = "edge" if frac > 0.78 else ("mid" if frac > 0.42 else "hot")
                c.set(int(px), int(py), E("arcane", role))
    # The key picks out the upper-left petals' inner faces.
    for (x, y) in ((11, 11), (12, 12), (15, 7), (15, 8), (8, 15), (9, 15)):
        c.set(x, y, E("arcane", "hot"))
    c.disc(cx, cy, 2.6, 2.6, E("arcane", "hot"))
    c.disc(cx, cy, 1.4, 1.4, E("arcane", "core"))
    # Pollen of light at the tips.
    for (x, y) in ((15, 2), (28, 15), (15, 28), (2, 15)):
        c.set(x, y, E("light", "hot"))
    return c


def attack_speed():
    """Tempest Rhythm: a bolt struck on the beat, with the wind of its passing. +attack speed."""
    c = raster.Canvas(CELL, CELL, None)
    _sprite(c, 7, 3, [
        "...........eee",
        "..........ehhe",
        ".........ehhe.",
        "........ehhme.",
        ".......ehhme..",
        "......ehhme...",
        ".....ehhme....",
        "....ehhhhhhee.",
        "...eccchhhme..",
        "...eeeehhme...",
        "......ehhme...",
        ".....ehhme....",
        "....ehhme.....",
        "...ehhme......",
        "..ehme........",
        ".ehe..........",
        "ee............",
    ], {"c": E("lightning", "core"), "h": E("lightning", "hot"), "m": E("lightning", "mid"),
        "e": E("lightning", "edge")})
    # The wind: three streaks behind the bolt, the beat it rides.
    w = E("wind", "mid")
    we = E("wind", "edge")
    for (x0, x1, y) in ((2, 8, 9), (1, 6, 13), (3, 7, 17)):
        c.hline(x0, x1, y, w)
        c.set(x0, y, we)
    for (x0, x1, y) in ((21, 27, 21), (23, 28, 25)):
        c.hline(x0, x1, y, w)
        c.set(x1, y, we)
    return c


def duration():
    """Eternal Wick: a candle whose flame does not gutter. +effect duration."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("skin")
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    # Wax.
    c.rect(12, 12, 19, 25, base)
    c.vline(12, 12, 25, hi)
    c.vline(13, 12, 25, lit)
    c.vline(19, 12, 25, shade)
    c.vline(18, 14, 25, base)
    c.hline(12, 19, 12, lit)
    # Drips over the lip.
    for (x, y0, y1) in ((14, 13, 16), (17, 13, 18)):
        c.vline(x, y0, y1, hi if x < 16 else lit)
    c.set(17, 19, lit)
    # The dish.
    c.hline(8, 23, 26, g_lit)
    c.hline(9, 22, 27, g_base)
    c.hline(10, 21, 28, g_shade)
    c.set(8, 26, g_hi)
    c.set(23, 26, g_shade)
    # Wick and flame.
    c.vline(16, 10, 11, OCC)
    _sprite(c, 13, 2, [
        "...e..",
        "..em..",
        "..mhe.",
        ".mhhm.",
        ".mhch.",
        ".mhcm.",
        "..hm..",
        "..e...",
    ], {"c": E("fire", "core"), "h": E("fire", "hot"), "m": E("fire", "mid"), "e": E("fire", "edge")})
    # The steady light it throws: a still ring, not a flicker.
    for (x, y) in ((11, 4), (21, 4), (10, 7), (22, 7), (11, 10), (21, 10)):
        c.set(x, y, _alpha(E("fire", "hot"), 170))
    _shadow(c, 16, 7)
    return c


def amount():
    """Twinstar Sigil: two stars where there was one. +1 projectile."""
    c = raster.Canvas(CELL, CELL, None)

    def star(cx, cy, arm, element):
        for k in range(-arm, arm + 1):
            role = "core" if abs(k) <= arm // 4 else ("hot" if abs(k) <= arm // 2 else "mid")
            c.set(cx + k, cy, E(element, role))
            c.set(cx, cy + k, E(element, role))
        for k in range(1, max(2, arm // 3) + 1):
            for (dx, dy) in ((k, k), (-k, k), (k, -k), (-k, -k)):
                c.set(cx + dx, cy + dy, E(element, "hot" if k == 1 else "mid"))
        for (dx, dy) in ((arm, 0), (-arm, 0), (0, arm), (0, -arm)):
            c.set(cx + dx, cy + dy, E(element, "edge"))
    star(11, 11, 8, "light")
    star(22, 21, 6, "arcane")
    # The bond between them: a faint arc of motes.
    for (x, y) in ((17, 9), (20, 11), (22, 14), (9, 19), (11, 22), (14, 24)):
        c.set(x, y, _alpha(E("arcane", "hot"), 200))
    return c


def movespeed():
    """Fleetfoot Glyph: a winged boot, the wind behind it. +movement speed."""
    c = raster.Canvas(CELL, CELL, None)
    l_hi, l_lit, l_base, l_shade, l_deep = _ramp("flesh")
    g_lit, g_base = T("gold", "lit"), T("gold", "base")
    # Side on, toe to the right: a tall shaft, the instep sweeping down into a long foot, a
    # rounded toe and a heel block - the stat glyph's boot, at icon size.
    _sprite(c, 9, 4, [
        "gggggggg.........",
        "Lhhhhhhs.........",
        "Lbbbbbbs.........",
        "Lbbbbbbs.........",
        "Lbbbbbbs.........",
        "Lbbbbbbs.........",
        "Lbbbbbbs.........",
        "Lbbbbbbbs........",
        "Lbbbbbbbbss......",
        "Lbbbbbbbbbbss....",
        "Lbbbbbbbbbbbbss..",
        "Lbbbbbbbbbbbbbbs.",
        "Lbbbbbbbbbbbbbbbs",
        "Lbbbbbbbbbbbbbbbs",
        "Lbbbbbbbbbbbbbbbs",
        "ddddddddddddddddd",
        "kkkk.............",
    ], {"g": g_lit, "L": l_lit, "h": l_hi, "b": l_base, "s": l_shade, "d": l_deep, "k": l_deep})
    # Stitching down the instep and a gold buckle at the ankle.
    for (x, y) in ((17, 13), (19, 14), (21, 15), (23, 16)):
        c.set(x, y, l_lit)
    c.rect(13, 11, 15, 12, g_base)
    c.set(13, 11, g_lit)
    # The wing at the ankle, swept back: three feathers of bone, lit on their upper edges.
    s_hi, s_lit, s_base, s_shade, s_deep = _ramp("skin")
    _sprite(c, 0, 4, [
        "....hhh....",
        "..hhllll...",
        "hhllbbbll..",
        ".hhlllbbs..",
        "hllbbbbs...",
        "..hllbbs...",
        ".hlbbs.....",
        "...bs......",
    ], {"h": s_hi, "l": s_lit, "b": s_base, "s": s_shade})
    # Wind off the heel.
    for (x0, x1, y) in ((1, 7, 15), (2, 7, 18)):
        c.hline(x0, x1, y, E("wind", "mid"))
        c.set(x0, y, E("wind", "edge"))
    _shadow(c, 17, 9, 21)
    return c


def magnet():
    """Void Lure: a small dark well that draws the motes in. +pickup range."""
    c = raster.Canvas(CELL, CELL, None)
    v_hi, v_lit, v_base, v_shade, v_deep = _ramp("violet")
    cx, cy = 16.0, 16.0
    # Three arms of motes on an inward spiral, each a streak that brightens as it falls in.
    for arm in range(3):
        for k in range(0, 28):
            t = k / 27.0
            a = math.radians(arm * 120.0 + t * 200.0)
            r = 14.5 - t * 6.0
            x = int(round(cx + math.cos(a) * r))
            y = int(round(cy + math.sin(a) * r))
            role = "edge" if t < 0.3 else ("mid" if t < 0.65 else "hot")
            c.set(x, y, E("arcane", role) if arm == 1 else E("light", role))
        c.set(int(round(cx + math.cos(math.radians(arm * 120.0 + 200.0)) * 8.5)),
              int(round(cy + math.sin(math.radians(arm * 120.0 + 200.0)) * 8.5)), E("light", "core"))
    # The well: a dark disc with a lit rim, so it holds its shape on the night glass.
    c.disc(cx, cy, 7.4, 7.4, v_deep)
    c.disc(cx, cy, 5.4, 5.4, OCC)
    for deg in range(0, 360, 3):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(225.0))
        tone = E("darkness", "hot") if facing > 0.6 else (E("darkness", "mid") if facing > -0.2 else E("darkness", "edge"))
        c.set(cx + math.cos(a) * 7.0, cy + math.sin(a) * 7.0, tone)
    # The swirl inside it.
    for (x, y) in ((13, 14), (14, 12), (16, 12), (18, 13), (19, 15), (19, 17), (17, 19), (15, 19), (13, 17)):
        c.set(x, y, v_base)
    for (x, y) in ((15, 14), (17, 15), (16, 17)):
        c.set(x, y, v_lit)
    c.set(16, 16, E("darkness", "core"))
    return c


def growth():
    """Sage Spiral: an open book with a spiral of green light rising off it. +XP gain."""
    c = raster.Canvas(CELL, CELL, None)
    p_hi, p_lit, p_base, p_shade, p_deep = _ramp("flesh")
    r_hi, r_lit, r_base, r_shade, r_deep = _ramp("red")
    # The cover, under the pages.
    c.poly([(3, 21), (15, 23), (16, 23), (28, 21), (28, 26), (16, 28), (15, 28), (3, 26)], r_base)
    c.hline(3, 15, 26, r_lit)
    c.hline(16, 28, 26, r_shade)
    # The two pages, curving up from the gutter.
    c.poly([(4, 18), (9, 17), (15, 19), (15, 25), (9, 23), (4, 24)], p_hi)
    c.poly([(16, 19), (22, 17), (27, 18), (27, 24), (22, 23), (16, 25)], p_lit)
    for (x0, x1, y) in ((6, 13, 20), (6, 12, 22), (18, 25, 20), (19, 25, 22)):
        c.hline(x0, x1, y, p_base)
    c.vline(15, 19, 25, p_shade)
    c.vline(16, 19, 25, p_base)
    # The spiral of growth, rising and widening.
    pts = []
    for k in range(0, 60):
        t = k / 59.0
        a = t * 3.2 * math.pi
        r = 1.0 + t * 7.0
        pts.append((15.5 + math.cos(a) * r, 15.0 - t * 11.0 + math.sin(a) * r * 0.45))
    for i, (x, y) in enumerate(pts):
        t = i / float(len(pts))
        role = "hot" if t < 0.35 else ("mid" if t < 0.75 else "edge")
        c.set(int(round(x)), int(round(y)), E("grass", role))
    c.set(15, 15, E("grass", "core"))
    # A leaf at the top of the spiral.
    _sprite(c, 19, 2, ["..mh", ".mhe", "mme.", "e..."], {"h": E("grass", "hot"), "m": E("grass", "mid"), "e": E("grass", "edge")})
    return c


def greed():
    """Avarice Seal: a purse of Arcane Energy, tied and sealed in gold. +Arcane Energy gain."""
    c = raster.Canvas(CELL, CELL, None)
    l_hi, l_lit, l_base, l_shade, l_deep = _ramp("flesh")
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    # A leather purse, side on: a round belly, a cord pulled tight, and the mouth above it
    # FLARING open in a ruffle - the ruffle is what says bag rather than bottle.
    _sprite(c, 4, 4, [
        "....s.hl..ls.s......",
        "...shlhlblbsbs......",
        "..shllbbbbbbbbs.....",
        "...sllbbbbbbbs......",
        "......ggggggg.......",
        ".....sddddddds......",
        "....lbbbbbbbbbs.....",
        "...lbbbbbbbbbbbs....",
        "..lhbbbbbbbbbbbbs...",
        ".lhbbbbbbbbbbbbbbs..",
        ".lhbbbbbbbbbbbbbbs..",
        "lhbbbbbbbbbbbbbbbbs.",
        "lhbbbbbbbbbbbbbbbbs.",
        "lbbbbbbbbbbbbbbbbbs.",
        "lbbbbbbbbbbbbbbbbss.",
        ".lbbbbbbbbbbbbbbss..",
        "..sbbbbbbbbbbbbss...",
        "...dssssssssssdd....",
        ".....dddddddddd.....",
    ], {"h": l_hi, "l": l_lit, "b": l_base, "s": l_shade, "d": l_deep, "g": g_lit})
    c.set(10, 8, g_hi)
    c.set(17, 8, g_shade)
    # The cord's tails.
    c.vline(11, 9, 12, g_base)
    c.vline(12, 9, 11, g_lit)
    # Arcane light spilling from the open mouth.
    for (x, y, role) in ((11, 3, "mid"), (13, 2, "hot"), (15, 3, "mid"), (14, 1, "core"), (9, 2, "edge"),
                         (17, 1, "edge"), (12, 4, "hot")):
        c.set(x, y, E("arcane", role))
    # The seal: a gold disc on the purse's face with an energy orb struck in it.
    _lit_disc(c, 13.5, 17.5, 4.4, "gold")
    c.disc(13.5, 17.5, 2.2, 2.2, E("arcane", "mid"))
    c.set(12, 16, E("arcane", "core"))
    c.set(13, 17, E("arcane", "hot"))
    _shadow(c, 14, 8, 23)
    return c


def extra_lives():
    """Phoenix Oath: a phoenix feather, burning and not consumed. +1 revive."""
    c = raster.Canvas(CELL, CELL, None)
    # The shaft runs from the quill at the lower left to the tip at the upper right.
    x0, y0, x1, y1 = 6.0, 27.0, 25.0, 4.0
    length = math.hypot(x1 - x0, y1 - y0)
    ux, uy = (x1 - x0) / length, (y1 - y0) / length
    nx, ny = -uy, ux
    for i in range(int(length * 3)):
        s = i / 3.0
        t = s / length
        if t < 0.18:
            continue
        # The vane, widest past the middle, curling at the tip.
        half = 5.2 * math.sin(math.pi * min(1.0, (t - 0.18) / 0.82)) ** 0.8
        for w in range(-14, 15):
            off = w / 2.0
            if abs(off) > half:
                continue
            px = x0 + ux * s + nx * off
            py = y0 + uy * s + ny * off
            edge = abs(off) / max(0.01, half)
            role = "edge" if edge > 0.72 else ("mid" if edge > 0.35 or t < 0.45 else "hot")
            if t > 0.85 and edge < 0.6:
                role = "hot"
            c.set(int(px), int(py), E("fire", role))
    # The barbs: notches in the vane where the feather parts.
    for (x, y) in ((11, 15), (12, 16), (19, 13), (20, 14), (14, 21), (22, 9)):
        c.set(x, y, (0, 0, 0, 0))
    # The quill and shaft, white-hot down the middle.
    for i in range(int(length * 2)):
        s = i / 2.0
        t = s / length
        px, py = x0 + ux * s, y0 + uy * s
        c.set(int(px), int(py), E("fire", "core") if t > 0.2 else T("gold", "lit"))
    c.set(25, 4, E("fire", "core"))
    _glint(c, 27, 7, "fire")
    return c


def rerolls():
    """Fate Fracture: a die with fate cracked through it. +1 reroll."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = _ramp("skin")
    # A cube seen from above-left: top face hi, left face lit, right face shade.
    top = [(16, 4), (27, 9), (16, 14), (5, 9)]
    left = [(5, 9), (16, 14), (16, 28), (5, 22)]
    right = [(16, 14), (27, 9), (27, 22), (16, 28)]
    c.poly(top, hi)
    c.poly(left, lit)
    c.poly(right, base)
    c.line(5, 9, 16, 14, base)
    c.line(16, 14, 27, 9, lit)
    c.line(16, 14, 16, 28, shade)
    c.line(5, 22, 16, 28, deep)
    c.line(16, 28, 27, 22, deep)
    c.vline(27, 9, 22, shade)
    # Pips: one on the top, two on the left, three on the right.
    for (x, y) in ((15, 8), (16, 8), (15, 9), (16, 9)):
        c.set(x, y, deep)
    for (x, y) in ((8, 14), (9, 14), (12, 21), (13, 21)):
        c.set(x, y, shade)
    for (x, y) in ((19, 17), (22, 19), (25, 21), (24, 13)):
        c.set(x, y, deep)
    # The fracture: a crack of arcane light from the top corner down through the near edge.
    crack = [(17, 5), (17, 6), (18, 7), (18, 8), (17, 9), (17, 10), (18, 11), (17, 12), (17, 13),
             (16, 15), (17, 16), (17, 17), (16, 18), (16, 19), (17, 20), (17, 21), (16, 22)]
    for (x, y) in crack:
        c.set(x, y, E("arcane", "hot"))
    for (x, y) in crack[::4]:
        c.set(x, y, E("arcane", "core"))
    for (x, y) in ((19, 7), (15, 17), (18, 21)):
        c.set(x, y, E("arcane", "mid"))
    _shadow(c, 16, 10, 29)
    return c


def bans():
    """Proscription: an edict with a burning line struck through it. +1 spell ban."""
    c = raster.Canvas(CELL, CELL, None)
    p_hi, p_lit, p_base, p_shade, p_deep = _ramp("flesh")
    # The sheet, its top rolled.
    c.rect(8, 7, 23, 26, p_hi)
    c.vline(23, 8, 26, p_lit)
    c.hline(8, 23, 26, p_base)
    c.rect(7, 4, 24, 7, p_lit)
    c.hline(7, 24, 4, p_hi)
    c.hline(7, 24, 7, p_base)
    c.vline(6, 5, 6, p_base)
    c.vline(25, 5, 6, p_shade)
    # The writing.
    for (x0, x1, y) in ((10, 21, 10), (10, 19, 13), (10, 21, 16), (10, 17, 19), (10, 20, 22)):
        c.hline(x0, x1, y, T("violet", "lit"))
    # The red seal at the foot.
    c.disc(20.0, 23.5, 2.6, 2.6, T("red", "lit"))
    c.set(19, 22, T("red", "hi"))
    # The proscription: a burning stroke corner to corner.
    for i in range(0, 22):
        x = 5 + i
        y = 27 - i
        c.set(x, y, E("fire", "mid"))
        c.set(x, y - 1, E("fire", "edge"))
        c.set(x + 1, y, E("fire", "edge"))
    for i in range(3, 20, 4):
        c.set(5 + i, 27 - i, E("fire", "hot"))
    _shadow(c, 16, 8, 28)
    return c


def banked_levels():
    """Hoarded Insight: a small chest, its lid lifted on the light stored in it. +1 saved level-up."""
    c = raster.Canvas(CELL, CELL, None)
    w_hi, w_lit, w_base, w_shade, w_deep = _ramp("flesh")
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    # The lid, thrown back: its inside face, dark, standing up behind the opening.
    c.rect(7, 3, 25, 10, w_shade)
    c.hline(7, 25, 3, w_base)
    c.vline(7, 3, 10, w_base)
    c.hline(8, 24, 4, w_deep)
    for x in (10, 22):
        c.vline(x, 3, 10, g_shade)
    # Light rising out of it, in front of the lid: rays and motes of what is stored inside.
    for (x0, y0, x1, y1) in ((12, 12, 10, 6), (16, 12, 16, 5), (20, 12, 22, 6)):
        c.line(x0, y0, x1, y1, E("light", "mid"))
    for (x, y) in ((13, 9), (19, 9), (11, 7), (21, 7)):
        c.set(x, y, E("light", "hot"))
    _glint(c, 16, 8, "light")
    # The body.
    c.rect(7, 14, 25, 26, w_base)
    c.hline(7, 25, 14, w_lit)
    c.vline(7, 14, 26, w_lit)
    c.vline(25, 15, 26, w_shade)
    c.hline(7, 25, 26, w_deep)
    c.hline(7, 25, 13, E("light", "mid"))
    # Gold bands and the lock plate.
    for x in (10, 22):
        c.vline(x, 14, 25, g_lit if x < 16 else g_base)
        c.vline(x, 10, 12, g_base)
    c.rect(14, 16, 18, 21, g_lit)
    c.vline(18, 16, 21, g_shade)
    c.hline(14, 18, 21, g_shade)
    c.set(14, 16, g_hi)
    c.set(16, 18, OCC)
    c.set(16, 19, OCC)
    _shadow(c, 16, 10, 27)
    return c


def auguries():
    """Augury: a scrying glass on a gold stand, an eye in it. +1 spell summoned to a level-up."""
    c = raster.Canvas(CELL, CELL, None)
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    cx, cy, r = 16.0, 13.0, 9.0
    # The glass: arcane, darker toward the far side, a hard highlight on the near one.
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if d > r:
                continue
            facing = ((x - cx) + (y - cy)) / max(1.0, d)
            if d > r - 1.2:
                tone = E("arcane", "hot") if facing < -0.6 else E("arcane", "edge")
            else:
                tone = T("arcane", "shade") if facing > 0.2 else T("arcane", "base")
            c.set(x, y, tone)
    # The eye.
    _sprite(c, 10, 10, [
        "...hhhhh...",
        ".hmmmmmmmh.",
        "hmmmeeemmmh",
        ".hmmmeeemh.",
        "...hhhhh...",
    ], {"h": E("arcane", "hot"), "m": E("arcane", "mid"), "e": OCC})
    c.set(15, 12, E("arcane", "core"))
    # The highlight.
    for (x, y) in ((11, 7), (12, 6), (11, 8)):
        c.set(x, y, E("arcane", "core"))
    # The stand: three claws and a foot.
    c.poly([(11, 21), (21, 21), (19, 24), (13, 24)], g_base)
    c.hline(11, 21, 21, g_lit)
    c.set(11, 21, g_hi)
    c.rect(10, 25, 22, 26, g_base)
    c.hline(10, 22, 25, g_lit)
    c.hline(10, 22, 26, g_shade)
    c.vline(22, 25, 26, g_deep)
    for (x, y) in ((9, 19), (10, 20), (23, 19), (22, 20)):
        c.set(x, y, g_lit if x < 16 else g_shade)
    _shadow(c, 16, 7, 27)
    return c


def vitality():
    """Vitality: a heart worn in a gold locket's bail - life you keep. +max HP."""
    c = raster.Canvas(CELL, CELL, None)
    e_hi, e_lit, e_base, e_shade, e_deep = _ramp("ember")

    def flesh(x, y):
        d = (x - 6) + (y - 9)
        if d < 5:
            return e_hi
        if d < 11:
            return e_lit
        if d < 26:
            return e_base
        return e_shade
    _heart(c, 16, 8, 24, 20, flesh)
    # The far edge of the heart in shadow, so it turns.
    for y in range(8, 29):
        for x in range(31, 0, -1):
            if c.get(x, y)[3]:
                c.set(x, y, e_deep)
                break
    for x in range(4, 29):
        for y in range(30, 0, -1):
            if c.get(x, y)[3]:
                if x > 12:
                    c.set(x, y, e_deep)
                break
    c.set(9, 11, "#FFFFFF" if False else e_hi)
    # The bail it hangs from.
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    c.ring(16.0, 6.0, 2.6, 2.6, g_lit, 1)
    c.set(17, 8, g_shade)
    c.set(18, 7, g_shade)
    c.hline(15, 17, 9, g_base)
    return c


def luck():
    """Fortune Thread: a spool of gold thread, its loose end tied off in a clover. +Luck."""
    c = raster.Canvas(CELL, CELL, None)
    w_hi, w_lit, w_base, w_shade, w_deep = _ramp("flesh")
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    # Flanges, top and bottom.
    for y0 in (7, 23):
        c.rect(6, y0, 21, y0 + 2, w_base)
        c.hline(6, 21, y0, w_lit)
        c.hline(6, 21, y0 + 2, w_shade)
        c.set(6, y0, w_hi)
    # The thread, wound: alternating bands of gold so it reads as many turns.
    for y in range(10, 23):
        tone = g_lit if y % 2 == 0 else g_base
        c.hline(8, 19, y, tone)
        c.set(8, y, g_hi if y % 2 == 0 else g_lit)
        c.set(19, y, g_shade)
    # The loose end, curling out to the right.
    for (x, y) in ((20, 13), (21, 13), (22, 12), (23, 11), (24, 11), (25, 12), (25, 13), (24, 14),
                   (23, 15), (23, 16), (24, 17), (25, 18)):
        c.set(x, y, g_lit)
    # The clover at the end of it.
    _sprite(c, 22, 18, [
        ".mm.mm",
        "mhhmhm",
        ".mmmm.",
        "mhmmhm",
        ".mm.mm",
    ], {"h": E("grass", "hot"), "m": E("grass", "mid")})
    c.set(25, 20, E("grass", "core"))
    _glint(c, 4, 4, "light")
    _shadow(c, 14, 8, 27)
    return c


def crit_chance():
    """Keen Focus: a lens that draws the light down to one hard point. +crit chance."""
    c = raster.Canvas(CELL, CELL, None)
    g_hi, g_lit, g_base, g_shade, g_deep = _ramp("gold")
    cx, cy, r = 13.0, 13.0, 8.5
    # The glass, faintly arcane, so the star in it reads as seen THROUGH it.
    c.disc(cx, cy, r - 1, r - 1, _alpha(T("arcane", "base"), 150))
    # The rim, lit upper left.
    for deg in range(0, 360, 2):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(225.0))
        tone = g_hi if facing > 0.7 else (g_lit if facing > 0.0 else (g_base if facing > -0.6 else g_shade))
        for rr in (r - 0.6, r - 1.4):
            c.set(cx + math.cos(a) * rr, cy + math.sin(a) * rr, tone)
    # The handle.
    for i in range(0, 9):
        x, y = 19 + i, 19 + i
        c.set(x, y, g_base)
        c.set(x + 1, y, g_shade)
        c.set(x, y + 1, g_shade)
        c.set(x - 1, y, g_lit)
    # The crit star, as the stat glyph draws it, white-hot at the focus.
    _sprite(c, 8, 8, [
        "....m....",
        "....h....",
        "...mhm...",
        "..mhch...",
        "mhhcccchm",
        "...hchm..",
        "...mhm...",
        "....h....",
        "....m....",
    ], {"c": E("light", "core"), "h": E("light", "hot"), "m": E("light", "edge")})
    c.set(10, 9, E("arcane", "core"))
    return c


def curation():
    """Redaction: a page with one line inked out, and the quill that did it. One spell set aside."""
    c = raster.Canvas(CELL, CELL, None)
    p_hi, p_lit, p_base, p_shade, p_deep = _ramp("flesh")
    c.rect(6, 5, 21, 27, p_hi)
    c.vline(21, 6, 27, p_lit)
    c.hline(6, 21, 27, p_base)
    c.hline(6, 21, 5, p_hi)
    # A folded corner.
    c.poly([(18, 5), (21, 5), (21, 8)], (0, 0, 0, 0))
    c.poly([(18, 5), (18, 8), (21, 8)], p_lit)
    c.line(18, 5, 21, 8, p_base)
    for (x0, x1, y) in ((8, 16, 9), (8, 19, 12), (8, 18, 18), (8, 15, 21), (8, 19, 24)):
        c.hline(x0, x1, y, T("violet", "lit"))
    # The redaction: a heavy bar of ink across one line, ragged where the nib lifted.
    c.rect(7, 14, 20, 16, T("violet", "deep"))
    c.hline(8, 19, 14, OCC)
    c.set(20, 14, (0, 0, 0, 0))
    c.set(7, 16, T("violet", "shade"))
    # The quill, crossing from the lower right.
    s_hi, s_lit, s_base, s_shade, s_deep = _ramp("skin")
    for i in range(0, 13):
        x, y = 28 - i, 28 - i
        c.set(x, y, s_shade)
    for i in range(3, 13):
        x, y = 28 - i, 28 - i
        c.set(x - 1, y, s_lit)
        c.set(x - 2, y + 1, s_hi if i % 2 else s_lit)
        c.set(x + 1, y + 1, s_base)
    c.set(16, 16, OCC)
    c.set(17, 17, T("gold", "lit"))
    return c


# The shop's order (MainMenu.BuildUpgradeDefinitions), which is also the preview sheet's.
ICONS = [
    ("damage", damage),
    ("recovery", recovery),
    ("cooldowns", cooldowns),
    ("area", area),
    ("attack_speed", attack_speed),
    ("duration", duration),
    ("amount", amount),
    ("movespeed", movespeed),
    ("magnet", magnet),
    ("growth", growth),
    ("greed", greed),
    ("extra_lives", extra_lives),
    ("rerolls", rerolls),
    ("bans", bans),
    ("banked_levels", banked_levels),
    ("auguries", auguries),
    ("vitality", vitality),
    ("luck", luck),
    ("crit_chance", crit_chance),
    ("curation", curation),
]


def _fringe(c):
    """The set's mark: the arcane edge tone, at part alpha, round every massed silhouette."""
    def opaque(x, y):
        return c.get(x, y)[3] >= 200

    def massed(x, y):
        if not opaque(x, y):
            return False
        n = sum(1 for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)) if opaque(x + dx, y + dy))
        return n >= 2

    ring = _alpha(E("arcane", "edge"), FRINGE_ALPHA)
    marks = []
    for y in range(CELL):
        for x in range(CELL):
            if c.get(x, y)[3] != 0:
                continue
            for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if massed(x + dx, y + dy):
                    marks.append((x, y))
                    break
    for (x, y) in marks:
        c.set(x, y, ring)
    return c


def preview(icons):
    """Every icon at x4, in its socket and bare on the parchment page, for judging by eye."""
    from PIL import Image
    socket = Image.open(SOCKET).convert("RGBA") if os.path.exists(SOCKET) else None
    cols = 10
    cell = 200
    rows = (len(icons) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell, rows * cell * 2), raster.rgb(T("stone", "base")))
    page = Image.new("RGBA", (cell - 16, cell - 16), raster.rgb(T("flesh", "hi")))
    for i, (name, canvas) in enumerate(icons):
        x = (i % cols) * cell
        y = (i // cols) * cell * 2
        if socket is not None:
            s = socket.resize((socket.size[0] * 2, socket.size[1] * 2), Image.NEAREST)
            sheet.paste(s, (x + (cell - s.size[0]) // 2, y + (cell - s.size[1]) // 2), s)
        big = canvas.img.resize((CELL * 4, CELL * 4), Image.NEAREST)
        sheet.paste(big, (x + (cell - CELL * 4) // 2, y + (cell - CELL * 4) // 2), big)
        sheet.paste(page, (x + 8, y + cell + 8))
        sheet.paste(big, (x + (cell - CELL * 4) // 2, y + cell + (cell - CELL * 4) // 2), big)
    sheet.save(PREVIEW)


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    ok = True
    done = []
    for name, fn in ICONS:
        canvas = _fringe(fn())
        bad = canvas.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s:" % name)
            for h, n in bad[:6]:
                print("   %s  x%d" % (h, n))
            continue
        canvas.scaled(2).save(os.path.join(OUT_DIR, name + ".png"))
        done.append((name, canvas))
        print("  %-16s 64x64" % name)
    preview(done)
    if not ok:
        return 1
    print("")
    print("%d upgrade icons, palette clean (preview: %s)" % (len(done), PREVIEW))
    return 0


if __name__ == "__main__":
    sys.exit(main())
