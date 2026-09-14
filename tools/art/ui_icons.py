# -*- coding: utf-8 -*-
"""Original UI icons. Run: python tools/art/ui_icons.py

The start of the set that replaces the third-party GUI pack's glyphs. Everything here is drawn
from scratch on the Bonelight palette.

TWO ICON SIZES, and the distinction matters:

  BUTTON icons are authored at 16x16 and rendered at x2, so they land at 32px - which sits
  correctly next to a line of 20-24px text on a 44-56px tall button. The contract's 32x32 UI cell
  in section 1 is for the BIG icons - spells, relics, chest sets - which are looked at as objects
  in their own right. Rendering one of those at x2 gives 64px, and 64px of gear on a 44px button
  would have to be downscaled, which throws away exactly the crispness the whole pipeline exists
  to protect.

  So: 16x16 for a glyph that labels a control, 32x32 for an icon that IS the content. Both at x2,
  both on the grid.

Light model per art-direction.md section 2: key from the upper left, so every raised edge is lit
on its top and left and shaded on its bottom and right.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "icons")

# 24, not 16. A cog needs three things across its radius - rim, ring, hole - and at sixteen pixels
# with a body radius of five there are three pixels of ring, which the rim shading then eats. The
# result was a lump with a square hole in it, twice. Twenty-four gives eight pixels of radius: rim,
# five of ring, and a hole that is still a hole after it has been lit on one side.
GLYPH = 24


def gear():
    """A cogwheel: iron, eight teeth, a hole through the middle.

    The teeth are placed by HAND, at whole-pixel coordinates, rather than computed from an angle.
    The first version put them on a circle with trigonometry and rounded to the nearest pixel, and
    at sixteen pixels that rounding is the whole design: two teeth merged into the body, one broke
    away from it entirely, and the wheel came out as a lump. At this size there are only a handful
    of legal positions and it is quicker to name them than to nudge a formula until it finds them.

    Eight teeth - four on the axes, four on the diagonals - which is what a gear looks like in the
    mind's eye. The axis teeth stick out past the body; the diagonal ones are shorter and sit half
    inside it, because eight teeth all at full length at this size closes the gaps between them and
    a gear with no gaps is a disc.
    """
    c = raster.Canvas(GLYPH, GLYPH, None)
    cx = cy = (GLYPH - 1) / 2.0

    iron = bl.tone("steel", "base")
    iron_lit = bl.tone("steel", "lit")
    iron_hi = bl.tone("steel", "hi")
    iron_dark = bl.tone("steel", "deep")
    iron_mid = bl.tone("steel", "shade")

    # Body radius and tooth positions are chosen together, not separately, and that is the lesson
    # of the passes before this one. A tooth has to do two things at once: touch the body, or it
    # floats; and reach past it, or it vanishes. With a body of radius 8 centred on 11.5 the rim
    # sits at 3.5 and 19.5, and the 45-degree rim at 5.84 and 17.16 - so an axis tooth spanning
    # rows 0-3 meets it, and a diagonal tooth at (5,5)-(7,7) has its inner corner 6.4 from centre,
    # inside, and its outer corner 9.2, outside. Both conditions hold by construction.
    BODY_R = 8.0

    # (x0, y0, x1, y1, catches_key). Key is upper left.
    TEETH = [
        (10, 0, 13, 3, True),        # N
        (0, 10, 3, 13, True),        # W
        (10, 20, 13, 23, False),     # S
        (20, 10, 23, 13, False),     # E
        (4, 4, 6, 6, True),          # NW
        (17, 4, 19, 6, True),        # NE
        (4, 17, 6, 19, False),       # SW
        (17, 17, 19, 19, False),     # SE
    ]
    for (x0, y0, x1, y1, toward) in TEETH:
        c.rect(x0, y0, x1, y1, iron if toward else iron_mid)
        c.hline(x0, x1, y0, iron_lit if toward else iron)
        c.vline(x0, y0, y1, iron_lit if toward else iron)
        c.hline(x0, x1, y1, iron_mid if toward else iron_dark)
        c.vline(x1, y0, y1, iron_mid if toward else iron_dark)

    # The body, drawn over the inner ends of the teeth so they are rooted rather than stuck on.
    c.disc(cx, cy, BODY_R, BODY_R, iron)
    for deg in range(0, 360, 2):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(225.0))
        tone = iron_hi if facing > 0.72 else (iron_lit if facing > 0.2 else
                                              (OCC if facing < -0.4 else iron_dark))
        c.set(cx + math.cos(a) * (BODY_R - 0.3), cy + math.sin(a) * (BODY_R - 0.3), tone)

    # The hole: round, and lit on its LOWER-RIGHT rim. A hole is lit on the opposite side to a
    # raised boss, and that one inversion is the whole difference between a bore and a stud.
    c.disc(cx, cy, 3.4, 3.4, OCC)
    for deg in range(0, 360, 4):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(45.0))
        if facing > 0.15:
            c.set(cx + math.cos(a) * 3.6, cy + math.sin(a) * 3.6, iron_lit)
        elif facing < -0.5:
            c.set(cx + math.cos(a) * 3.6, cy + math.sin(a) * 3.6, iron_dark)

    return c


ICONS = [
    ("gear", gear, GLYPH),
]


def write(canvas, name, cell):
    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s (art-direction.md section 3):" % name)
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return False

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    path = os.path.join(OUT_DIR, name + ".png")
    canvas.scaled(2).save(path)
    print("  %-12s %dx%d" % (name, cell * 2, cell * 2))
    return True


def main():
    ok = True
    for name, fn, cell in ICONS:
        ok &= write(fn(), name, cell)
    if not ok:
        return 1
    print("")
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
