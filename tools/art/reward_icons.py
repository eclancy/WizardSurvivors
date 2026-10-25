# -*- coding: utf-8 -*-
"""Reward icons for the results screen. Run: python tools/art/reward_icons.py

The results screen (scripts/GameOverScreen.cs, .ai/level-up-screen.md section 6) shows everything a
run earned as a card with a picture. Most of those pictures already existed - a new spell is its
spell icon, a boon its boon icon, a freed wizard his portrait - and these are the five that did not:

    reward-energy       Arcane Energy, the purse a run pays into
    reward-deed         an Unlocks entry finished (its reward was Arcane Energy, not a thing)
    reward-chapter      a chapter opened by beating its boss
    reward-ascension    an Ascension path seen for the first time, when the path has no art
    reward-apotheosis   the same for an Apotheosis form

32x32 rendered at x2, the UI cell of `.ai/art-direction.md` section 1 that the spell, boon and
relic icons use - they sit in the same 64-unit well on the same card, so they have to survive
being compared. They are OBJECTS lit from the upper left on the material rows, as relics are, and
borrow relic_icons' `_rim` so an edge is lit by the same rule; they touch an element ramp only
where something is genuinely alight (the energy, the light through the gate, the risen star).

Every colour is a contract colour; `audit` checks it, as for every other generator.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import boon_icons as boon
import raster
import relic_icons as relic

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "rewards")
PREVIEW = os.path.join(ROOT, "tools", "art", "_reward_icons_preview.png")

CELL = 32

GOLD = relic.GOLD
BONE = relic.BONE
FLESH = boon._ramp("flesh")
RED = boon._ramp("red")
VIOLET = boon._ramp("violet")
ARCANE_GLASS = boon._ramp("arcane")
E = bl.ELEMENTS
_rim = relic._rim
_shadow = boon._shadow


def _emissive_orb(c, cx, cy, r, ramp):
    """A ball of light: the far rim in the ramp's edge, the near face hot, a core at the upper left.

    Not _lit_disc - that is a solid object lit from outside. Energy is lit from inside, so the
    face toward the key is the HOT tone and only the far rim falls to the edge colour, the same
    rule stat_icons.energy draws its 12-pixel orb by."""
    core, hot, mid, edge = ramp
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            nx = (x - cx) / float(r)
            ny = (y - cy) / float(r)
            d = math.sqrt(nx * nx + ny * ny)
            if d > 1.0:
                continue
            facing = -(nx + ny) / math.sqrt(2.0)
            if d > 0.78 and facing < 0.25:
                tone = edge
            elif facing > 0.35 and d < 0.85:
                tone = hot
            else:
                tone = mid
            c.set(x, y, tone)
    hx, hy = int(round(cx - r * 0.38)), int(round(cy - r * 0.42))
    for (x, y) in ((hx, hy), (hx + 1, hy), (hx, hy + 1)):
        c.set(x, y, core)


def energy():
    """Arcane Energy: a gold offering dish heaped with orbs of the arcane light, motes rising off it.

    The 12-pixel `stat-energy` glyph is one orb; this is the purse it goes into. A dish rather than
    a sack because a sack is a thing you carry and this is a thing you pay into between runs."""
    c = raster.Canvas(CELL, CELL, None)
    a = E["arcane"]
    hi, lit, base, shade, deep = GOLD
    _shadow(c, 16, 10)

    # The dish's mouth: the far half of the rim and the dark of the inside, behind the orbs.
    c.disc(16, 19, 12, 3.6, deep)
    for x in range(4, 29):
        t = (x - 16) / 12.0
        if abs(t) <= 1.0:
            c.set(x, int(round(19 - 3.6 * math.sqrt(1 - t * t))), shade)

    # The orbs, the big one behind the two small ones. Each small one is edged all round in the
    # ramp's edge tone first: three orbs of one light otherwise melt into a single lump of rock.
    _emissive_orb(c, 16.5, 12.0, 6.8, a)
    for (ox, oy, r) in ((9.0, 17.0, 3.7), (23.5, 17.5, 3.9)):
        c.disc(ox, oy, r + 1.1, r + 1.1, a[3])
        _emissive_orb(c, ox, oy, r, a)

    # The bowl under the lip, narrowing to its foot, then lit by the house rule.
    for y in range(20, 27):
        half = 12.0 - (y - 20) * 1.35
        c.hline(int(round(16 - half)), int(round(16 + half)), y, base)
    c.rect(13, 27, 19, 28, shade)
    c.hline(12, 20, 28, base)
    _rim(c, GOLD)
    # The near lip, drawn last so it lies over the orbs' feet: two pixels, lit on top.
    for x in range(4, 29):
        t = (x - 16) / 12.0
        if abs(t) <= 1.0:
            y = int(round(19 + 3.6 * math.sqrt(1 - t * t)))
            c.set(x, y, lit if x < 18 else base)
            c.set(x, y + 1, base if x < 18 else shade)
    c.set(5, 20, hi)
    c.set(6, 21, hi)

    # Motes lifting off the heap.
    for (x, y, tone) in ((7, 8, a[1]), (25, 6, a[0]), (12, 3, a[2]), (21, 2, a[1]), (27, 11, a[2]), (4, 13, a[2])):
        c.set(x, y, tone)
    c.set(25, 5, a[2])
    c.set(24, 6, a[2])
    return c


def deed():
    """An Unlocks entry finished: a charter, rolled at both ends, sealed in red wax with its ribbons.

    The parchment is the flesh row (the page every spell card is), the seal the red row the
    mutation page's wax seal is - so it reads as a document of this game rather than a scroll."""
    c = raster.Canvas(CELL, CELL, None)
    fh, fl, fb, fs, fd = FLESH
    _shadow(c, 16, 11)

    # The sheet between the rolls.
    c.rect(7, 7, 25, 22, fh)
    c.vline(25, 7, 22, fl)
    c.vline(24, 8, 22, fl)
    # Lines of a hand, uneven so they read as writing and not as a ruled grid.
    for (y, x0, x1) in ((10, 10, 22), (13, 10, 20), (16, 10, 22), (19, 10, 15)):
        for x in range(x0, x1 + 1):
            if (x + y) % 5 != 0:
                c.set(x, y, fb)

    def roll(y):
        c.rect(5, y, 27, y + 3, fl)
        c.hline(5, 27, y, fh)
        c.hline(5, 27, y + 2, fb)
        c.hline(5, 27, y + 3, fs)
        for cx in (4, 28):
            c.disc(cx, y + 1.5, 1.6, 2.0, fb)
            c.set(cx, y + 1, fh if cx == 4 else fl)
            c.set(cx, y + 3, fd)

    roll(4)
    roll(22)
    _rim(c, FLESH)

    # The seal over the foot of the sheet, its ribbons hanging below the roll.
    rh, rl, rb, rs, rd = RED
    for (x0, x1, top, bot, lean) in ((17, 18, 25, 29, -1), (22, 23, 25, 28, 1)):
        for y in range(top, bot + 1):
            off = (y - top) * lean // 2
            c.hline(x0 + off, x1 + off, y, rl if lean < 0 else rb)
    c.set(16, 29, rs)
    c.set(24, 28, rs)
    c.disc(20, 22.5, 4.2, 4.0, rb)
    c.ring(20, 22.5, 4.2, 4.0, rs)
    for (x, y) in ((17, 20), (18, 19), (17, 21), (19, 19)):
        c.set(x, y, rh)
    # The impression: a gold cross pressed into the wax.
    gh, gl, gb, gs, gd = GOLD
    c.vline(20, 21, 24, gb)
    c.hline(19, 21, 22, gb)
    c.set(20, 21, gl)
    c.set(19, 22, gl)
    return c


def chapter():
    """A chapter opened: a pale stone gate, a gold keystone, and the light of the place beyond it.

    Bone-pale masonry, not the stone row: the stone row is the colour of the card it sits on, and a
    gate drawn in it vanished into the well. The light is the Light element's ramp, white at the
    foot of the road and gold at the arch - the one warm, open thing on the card."""
    c = raster.Canvas(CELL, CELL, None)
    bh, bl_, bb, bs, bd = BONE
    light = E["light"]
    earth = E["earth"]
    _shadow(c, 16, 13)

    cx, cy = 16.0, 13.0
    outer, inner = 11.5, 6.5

    def in_opening(x, y):
        if 10 <= x <= 22 and cy <= y <= 28:
            return True
        return y < cy and math.hypot(x - cx, y - cy) <= inner

    # The masonry: two pillars and the arch between them.
    for y in range(1, 29):
        for x in range(4, 29):
            if in_opening(x, y):
                continue
            if y >= cy and (4 <= x <= 9 or 23 <= x <= 28):
                c.set(x, y, bb)
            elif y < cy and math.hypot(x - cx, y - cy) <= outer:
                c.set(x, y, bb)
    # Joints: courses on the pillars, voussoirs on the arch.
    for y in range(16, 29, 4):
        c.hline(4, 9, y, bs)
        c.hline(23, 28, y, bs)
    for k in range(1, 6):
        ang = math.pi + k * math.pi / 6.0
        for rr in (inner + 1, inner + 2.5, inner + 4):
            c.set(int(round(cx + math.cos(ang) * rr)), int(round(cy + math.sin(ang) * rr)), bs)
    _rim(c, BONE)

    # The light beyond, brightest at the far end of the road.
    for y in range(1, 29):
        for x in range(10, 23):
            if not in_opening(x, y):
                continue
            d = math.hypot((x - 16) / 7.0, (y - 27) / 16.0)
            idx = 0 if d < 0.35 else 1 if d < 0.6 else 2 if d < 0.85 else 3
            c.set(x, y, light[idx])

    # The road in, narrowing to the light.
    for y in range(20, 29):
        half = 1 + (y - 20) * 0.55
        for x in range(int(round(16 - half)), int(round(16 + half)) + 1):
            if 10 <= x <= 22:
                c.set(x, y, earth[1] if x < 16 else earth[2])

    # The keystone, gold - the stone that was turned.
    gh, gl, gb, gs, gd = GOLD
    c.rect(14, 1, 18, 4, gb)
    c.hline(14, 18, 1, gl)
    c.vline(14, 1, 4, gl)
    c.hline(15, 18, 4, gs)
    c.set(14, 1, gh)
    return c


def _risen_star(c, cx, cy, ramp, long_up=10, arm=6):
    """A four-point star with its upper ray longest: something risen, not merely shining."""
    core, hot, mid, edge = ramp
    rays = ((0, -1, long_up), (0, 1, arm + 1), (-1, 0, arm), (1, 0, arm))
    for (dx, dy, n) in rays:
        for k in range(n + 1):
            w = 1 if k > n * 0.6 else 2 if k > 2 else 3
            for s in range(-w + 1, w):
                x = cx + dx * k + (s if dy else 0)
                y = cy + dy * k + (s if dx else 0)
                tone = core if k <= 1 else hot if k <= n * 0.4 else mid if k < n else edge
                c.set(x, y, tone)
    for (x, y) in ((cx - 3, cy - 3), (cx + 3, cy - 3), (cx - 3, cy + 3), (cx + 3, cy + 3)):
        c.set(x, y, mid)
    c.set(cx, cy, core)


def _medallion(c, face, ring_ramp):
    """A round plate in a gold ring: the Ascension's obsidian, as the level 8 plates are."""
    fh, fl, fb, fs, fd = face
    c.disc(16, 16, 12.5, 12.5, GOLD[2])
    c.disc(16, 16, 10.5, 10.5, fs)
    c.disc(15.5, 15.5, 9.0, 9.0, fb)
    for deg in range(0, 360, 2):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(225.0))
        tone = GOLD[0] if facing > 0.75 else GOLD[1] if facing > 0.15 else GOLD[3] if facing < -0.5 else GOLD[2]
        c.set(16 + math.cos(a) * 12.0, 16 + math.sin(a) * 12.0, tone)
        c.set(16 + math.cos(a) * 11.2, 16 + math.sin(a) * 11.2, ring_ramp[3] if facing < 0 else ring_ramp[2])


def ascension():
    """An Ascension path revealed: the arcane star risen on an obsidian medallion in gold."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 10)
    _medallion(c, VIOLET, ARCANE_GLASS)
    glass = ARCANE_GLASS
    _risen_star(c, 16, 17, [glass[0], glass[1], glass[2], glass[3]], long_up=11, arm=8)
    return c


def apotheosis():
    """An Apotheosis revealed: the same medallion, burning - the Fire ramp, as the ember plate is."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 10)
    _medallion(c, RED, [E["fire"][0], E["fire"][1], E["fire"][2], E["fire"][3]])
    _risen_star(c, 16, 17, E["fire"], long_up=11, arm=8)
    return c


ICONS = [
    ("reward-energy", energy),
    ("reward-deed", deed),
    ("reward-chapter", chapter),
    ("reward-ascension", ascension),
    ("reward-apotheosis", apotheosis),
]


def preview(rendered):
    """The five at x2 on the two grounds they meet - the results card's icon well (stone deep) and
    parchment - and at x4, so a shape that only reads big is caught."""
    from PIL import Image
    grounds = [bl.tone("stone", "deep"), bl.tone("stone", "base"), bl.tone("flesh", "hi")]
    cellw = 64 + 128 + 24
    sheet = Image.new("RGBA", (cellw * len(rendered), 140 * len(grounds)), (0, 0, 0, 255))
    for i, (name, canvas) in enumerate(rendered):
        for gi, ground in enumerate(grounds):
            sheet.paste(Image.new("RGBA", (cellw, 140), raster.rgb(ground)), (i * cellw, gi * 140))
            x2 = canvas.scaled(2)
            x4 = canvas.scaled(4)
            sheet.paste(x2, (i * cellw + 8, gi * 140 + 38), x2)
            sheet.paste(x4, (i * cellw + 80, gi * 140 + 6), x4)
    sheet.save(PREVIEW)


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    ok = True
    rendered = []
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
        rendered.append((name, canvas))
        print("  %-20s 64x64" % name)
    if not ok:
        return 1
    preview(rendered)
    print("preview: %s" % os.path.relpath(PREVIEW, ROOT).replace(os.sep, "/"))
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
