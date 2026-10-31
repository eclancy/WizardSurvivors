# -*- coding: utf-8 -*-
"""The Fallow Fields' standing props (.ai/campaign.md section 5). Run: python tools/art/props_fields.py

    world/fields/stook-1.png, stook-2.png   sheaves stood together in the stubble to dry
    world/fields/fence-1.png, fence-2.png   a run of post-and-rail fence, one with a rail down

"Haycocks and fence rails are props." A STOOK rather than a haycock: a haycock is a rounded heap
of hay, and the chapter's Wall creature is a rounded rick of black hay with a man inside it - a
static prop of the same outline would be a decoy the player has to learn to ignore. A stook is
the other thing a harvested field is dotted with, sheaves leaned together into a pointed tent, and
nothing in the cast is that shape. Gold's shade and base, lit on the key side, so they read as
straw left out under no light rather than as pickups.

Both are walked through, like the bushes (.ai/terrain.md section 10, "What blocks"): knee to
waist high, a sprite and no body. Each is authored 1:1 and saved at x2, like every world sprite,
WITH ITS CONTACT SHADOW ON THE LAST ROW: the bottom edge of the PNG is the ground line and the
middle column stands on the placement point. That convention is the whole manifest -
Node2DGame's Fields decor reads it as GroundedSprite(ground = height, anchor = width / 2) - so
redraw within it or update both.
"""
from __future__ import division, print_function

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "fields")
PREVIEW = os.path.join(ROOT, "tools", "art", "_props_fields_preview.png")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


GO = ramp("gold")
FL = ramp("flesh")
LN = ramp("linen")


def sheaf(c, foot_x, foot_y, top_x, top_y, front, salt):
    """One sheaf leaned into the stook: a bundle three straws wide from its splayed butt to its
    ears, tied a third of the way down. Front sheaves catch the key; back ones are a stop darker."""
    lit, mid, dark = (GO[2], GO[3], GO[4]) if front else (GO[3], GO[4], OCC)
    n = max(abs(top_y - foot_y), 1)
    for i in range(n + 1):
        t = i / float(n)
        x = int(round(foot_x + (top_x - foot_x) * t))
        y = int(round(foot_y + (top_y - foot_y) * t))
        spread = 1 if t < 0.15 else 0                   # the butt splays a little on the ground
        c.set(x - 1 - spread, y, lit)
        c.set(x, y, mid if (i + salt) % 4 else lit)
        c.set(x + 1 + spread, y, dark)
    # the band
    bx = int(round(foot_x + (top_x - foot_x) * 0.62))
    by = int(round(foot_y + (top_y - foot_y) * 0.62))
    c.set(bx - 1, by, LN[3])
    c.set(bx, by, LN[2] if front else LN[3])
    c.set(bx + 1, by, LN[4])
    # the ears, fanned a little past the top
    for (dx, dy, tone) in ((-1, -1, GO[2] if front else GO[3]), (0, -2, GO[1] if front else GO[2]),
                           (1, -1, GO[3]), (0, -1, GO[2] if front else GO[3]), (-2, 0, GO[3]), (2, 0, GO[4])):
        c.set(top_x + dx, top_y + dy, tone)


def stook(i):
    w, h = 26, 24
    c = raster.Canvas(w, h, None)
    # The contact shadow, on the last rows. Centred on h - 2, not h - 1.5: there it reached row h,
    # one below the picture, and was sliced flat along its foot (tools/art/clip_check.py).
    c.disc(13, h - 2, 11, 1.6, OCC)
    peak = (13, 5) if i == 0 else (12, 4)
    # back sheaves first, then the front ones over them, all leaning in to the peak
    back = [(6, 21), (20, 21), (13, 20)] if i == 0 else [(5, 21), (19, 20), (11, 20), (16, 21)]
    front = [(8, 22), (17, 22), (12, 22)] if i == 0 else [(7, 22), (15, 22), (21, 22)]
    for k, (fx, fy) in enumerate(back):
        sheaf(c, fx, fy, peak[0] + (k - 1), peak[1] + 1, False, k)
    for k, (fx, fy) in enumerate(front):
        sheaf(c, fx, fy, peak[0] + (k - 1) // 2, peak[1] + 2, True, k + 3)
    # the gaps between the butts at the foot are shadow
    for x in range(8, 19, 3):
        if c.get(x, h - 3)[3]:
            c.set(x, h - 3, OCC)
    return c


def post(c, x, base, height, lean=0):
    """A split-log post: lit face left, shade right, a pale cut top."""
    for k in range(height):
        xx = x + (lean * k) // height
        c.set(xx, base - k, GO[2] if k < height - 1 else GO[1])
        c.set(xx + 1, base - k, GO[3])
        c.set(xx + 2, base - k, GO[4])
    top_x = x + lean
    c.set(top_x, base - height, FL[2])
    c.set(top_x + 1, base - height, GO[2])
    c.set(top_x + 2, base - height, GO[3])
    c.set(x + 1, base - height // 2, OCC)               # a knot
    for dx in range(-1, 4):
        c.set(x + dx, base + 1, OCC)


def rail(c, x0, y0, x1, y1):
    """A rail: a lit top, a shaded underside, and occlusion under it where it shades the ground."""
    n = max(abs(x1 - x0), 1)
    for i in range(n + 1):
        t = i / float(n)
        x = int(round(x0 + (x1 - x0) * t))
        y = int(round(y0 + (y1 - y0) * t))
        c.set(x, y, GO[2] if (i % 7) else GO[1])
        c.set(x, y + 1, GO[3])
        c.set(x, y + 2, GO[4] if i % 3 else OCC)


def fence(i):
    w, h = 36, 18
    c = raster.Canvas(w, h, None)
    base = h - 2
    for x in range(1, w - 1):
        if x % 3:
            c.set(x, h - 1, OCC)                        # the run's shadow, broken
    if i == 0:
        rail(c, 3, 5, 31, 5)
        rail(c, 3, 10, 31, 10)
        post(c, 2, base, 13)
        post(c, 17, base, 13)
        post(c, 31, base, 13)
    else:
        # a rail down: the far section's top rail fallen, one end still on its post
        rail(c, 3, 5, 18, 5)
        rail(c, 3, 10, 31, 10)
        rail(c, 19, 6, 32, 14)
        post(c, 2, base, 13)
        post(c, 17, base, 13)
        post(c, 31, base, 10, lean=1)
    return c


SPRITES = [
    ("stook-1", lambda: stook(0)),
    ("stook-2", lambda: stook(1)),
    ("fence-1", lambda: fence(0)),
    ("fence-2", lambda: fence(1)),
]


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True
    shots = []
    for name, fn in SPRITES:
        c = fn()
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:4]))
            continue
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, name + ".png"))
        shots.append(img)
        print("  %-10s %dx%d on screen" % (name, img.size[0], img.size[1]))
    # A preview on the stubble's tone, x3, for looking at.
    width = sum(s.size[0] for s in shots) + 20 * len(shots)
    height = max(s.size[1] for s in shots) + 20
    sheet = Image.new("RGBA", (width, height), raster.rgb(GO[4]))
    x = 10
    for s in shots:
        sheet.alpha_composite(s, (x, 10))
        x += s.size[0] + 20
    sheet.resize((sheet.size[0] * 3, sheet.size[1] * 3), Image.NEAREST).save(PREVIEW)
    print("preview: tools/art/_props_fields_preview.png")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
