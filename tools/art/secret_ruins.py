# -*- coding: utf-8 -*-
"""The Mystic Ruins' secrets (.ai/achievements-and-secrets.md 8.7). Run: python tools/art/secret_ruins.py

    world/ruins/ward-stone.png          the Broken Ward's five stones: fifteen frames left to right, three a
                                        notch count (1 to 5): dark, lit, dimming. Frame (n - 1) * 3 + state
                                        (BrokenWard.cs reads it as Hframes = 15)
    world/ruins/ward-heart.png          the flat stone at the ring's middle where the ward's light was: dark,
                                        lit. The warm glint is drawn over its cup in code (it must breathe)
    world/ruins/ward-fragment-1 .. -3   broken pieces of ward stone lying in the rubble, a notch or two cut in
                                        each: the far tell, commoner toward the ring
    world/ruins/ward-fragment-warm-1, -2  the same with the last of the light still in a notch: the far
                                        tell's near band
    tools/art/_secret_ruins_preview.png (gitignored) on the ruins' floor tone, for looking at

"Five ward stones in a ring, each cut with a different number of notches, all of them dark." The ward
was broken from the inside; the stones still stand. Touched in the order of their notches they light,
one by one, and the ward stands again for a while.

CUT STONE, PALE ENOUGH TO STAND OFF THE FLOOR. The ruins' floor is the stone row itself, so a stone drawn
only in that row would vanish into it. These are cut from the heath's granite family (props_heath:
bone's lower stops for the lit faces, stone's upper stops for the shade) with bone's base on the lit
edge: a dressed face, lit from the upper left, its left face lit, its front base, its right face shade.

THE NOTCHES. Cut across the front face's left edge, one under another, like tally strokes: a groove in
occlusion with its lower lip catching the light (the inside of a cut faces up). Five on the tallest is
the most the face holds and still reads at a glance, which is what the sequence asks - he counts them.
Lit, a groove holds the ward's light: Light's ramp, white-hot in the middle (the contract: every
element's core is white-hot), its edge gold. Dimming, the light has gone to embers - gold's shade and
base - for the beat after a wrong stone, before they go dark.

CELLS. Authored 1:1, saved at x2. Every far-tell piece has the same margin under its foot (FRAG_MARGIN),
so one SecretFarTell.FootFromBottom serves them all (BrokenWard.FragmentFoot).

Runs on Python 2.7 and 3.13 alike, with the same output: no `random`.
"""
from __future__ import division, print_function

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "ruins")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_ruins_preview.png")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


SK = ramp("skin")
ST = ramp("stone")
GO = ramp("gold")
LIGHT = bl.ELEMENTS["light"]       # core, hot, mid, edge

# The dressed stone, lit to dark: bone's base on the lit edge, then the heath's granite.
CUT = [SK[2], SK[3], SK[4], ST[0], ST[1], ST[2]]

SW, SH = 26, 48          # a ward stone's frame
FOOT_Y = 43              # its contact line
STATES = ("dark", "lit", "dim")


def stone_faces(top_l, top_r):
    """The stone's three faces as polygons: it tapers from 18 px at the foot to 14 at the shoulder, its
    top cut on a slant (top_l, top_r are the heights of the shoulder at its left and right)."""
    l0, f0, r0, e0 = 4, 7, 19, 22          # left edge, front face's left, front's right, right edge, at the foot
    l1, f1, r1, e1 = 7, 9, 17, 19          # the same at the shoulder: a stone tapers, a slab does not
    left = [(l0, FOOT_Y), (f0, FOOT_Y + 1), (f1, top_l + 1), (l1, top_l)]
    front = [(f0, FOOT_Y + 1), (r0, FOOT_Y + 1), (r1, top_r + 1), (f1, top_l + 1)]
    right = [(r0, FOOT_Y + 1), (e0, FOOT_Y), (e1, top_r), (r1, top_r + 1)]
    cap = [(l1, top_l), (f1, top_l + 1), (r1, top_r + 1), (e1, top_r), (r1 - 1, top_r - 2), (f1 + 1, top_l - 2)]
    return left, front, right, cap


def ward_stone(notches, state):
    c = raster.Canvas(SW, SH, None)
    c.disc(13, FOOT_Y + 1, 11, 2.6, OCC)
    # Taller stones for more notches, so the count reads in the silhouette too.
    top_l = 14 - notches * 2
    top_r = top_l + 2
    left, front, right, cap = stone_faces(top_l, top_r)
    c.poly_shade(left, [CUT[0], CUT[1], CUT[1]], ang=-0.4)
    c.poly_shade(front, [CUT[2], CUT[3], CUT[3], CUT[4]], ang=-0.6, gamma=1.1)
    c.poly_shade(right, [CUT[4], CUT[5]], ang=-0.4)
    c.poly(cap, CUT[1])
    c.line(cap[5][0], cap[5][1], cap[4][0], cap[4][1], CUT[0])
    # The lit edge, and the contact line along the foot.
    c.line(6, top_l, 4, FOOT_Y, CUT[0])
    c.line(20, top_r, 22, FOOT_Y, ST[3])
    c.hline(5, 21, FOOT_Y + 1, OCC)
    # A chip off the shoulder: a broken ward, still standing.
    c.set(18, top_r + 2, OCC)
    c.set(17, top_r + 2, CUT[4])
    c.set(18, top_r + 3, CUT[4])
    # The notches, top down, one every five rows from just under the shoulder.
    y0 = top_l + 7
    for k in range(notches):
        y = y0 + k * 5
        if state == "lit":
            row = [LIGHT[3], LIGHT[1], LIGHT[0], LIGHT[0], LIGHT[0], LIGHT[1], LIGHT[2]]
            for i, col in enumerate(row):
                c.set(8 + i, y, col)
            # The cut's lit lower lip takes the light's gold edge: the groove glows, nothing spills.
            c.hline(9, 13, y + 1, LIGHT[3])
        elif state == "dim":
            for i in range(7):
                c.set(8 + i, y, GO[3] if i in (0, 6) else GO[2])
            c.hline(9, 13, y + 1, CUT[2])
        else:
            c.hline(8, 14, y, OCC)
            c.hline(9, 13, y + 1, CUT[1])
    return c


HW, HH = 48, 26          # the heart stone's frame
HEART_Y = 13             # its middle (BrokenWard puts the glint over this point's cup)


def ward_heart(lit):
    """A flat round stone laid in the ground at the ring's middle, a ring cut in its face and a cup at its
    centre where the ward's light was. Lit, the cut ring holds the light."""
    c = raster.Canvas(HW, HH, None)
    cx = HW // 2
    c.disc(cx, HEART_Y + 3, 21, 7.5, OCC)
    c.disc(cx, HEART_Y + 1, 20, 7.0, CUT[4])
    c.disc(cx - 1, HEART_Y, 19, 6.4, CUT[3])
    c.disc(cx - 4, HEART_Y - 1, 12, 3.6, CUT[2])
    # The cut ring.
    for k in range(48):
        a = k * math.pi * 2.0 / 48
        x = int(round(cx + math.cos(a) * 13.0))
        y = int(round(HEART_Y + math.sin(a) * 4.2))
        if lit:
            c.set(x, y, LIGHT[1] if k % 3 else LIGHT[0])
        else:
            c.set(x, y, OCC if math.sin(a) < 0.3 else CUT[1])
    # The cup.
    c.disc(cx, HEART_Y, 3.2, 1.6, OCC)
    c.set(cx + 1, HEART_Y + 1, CUT[2])
    if lit:
        c.set(cx, HEART_Y, LIGHT[0])
        c.set(cx - 1, HEART_Y, LIGHT[1])
    # Two old cracks out from the cup: broken from the inside.
    c.line(cx + 3, HEART_Y + 1, cx + 9, HEART_Y + 4, OCC)
    c.line(cx - 4, HEART_Y - 1, cx - 10, HEART_Y - 3, OCC)
    return c


FW, FH = 26, 18          # a fragment's frame
FRAG_MARGIN = 4          # art pixels under every fragment's foot


def fragment(shape, notches, warm):
    """A broken piece of ward stone lying on its side: a block with a dressed face toward the viewer and a
    notch or two cut in it. `warm`: the last of the ward's light still in a notch."""
    c = raster.Canvas(FW, FH, None)
    base = FH - FRAG_MARGIN
    pts = shape
    c.disc(13, base, 11, 2.2, OCC)
    top = [(x, y) for (x, y) in pts]
    c.poly_shade(top, [CUT[1], CUT[2], CUT[3], CUT[4]], ang=-0.7)
    # The dressed face: the lower band of the block.
    xs = [p[0] for p in pts]
    x0, x1 = min(xs) + 1, max(xs) - 1
    c.hline(x0, x1, base - 1, CUT[4])
    c.hline(x0 + 1, x1, base, OCC)
    # The broken edge, lit.
    c.line(pts[0][0], pts[0][1], pts[1][0], pts[1][1], CUT[0])
    for k in range(notches):
        y = base - 4 - k * 3
        if warm and k == 0:
            c.hline(x0 + 3, x0 + 7, y, GO[3])
            c.set(x0 + 5, y, GO[2])
            c.set(x0 + 4, y, GO[2])
        else:
            c.hline(x0 + 3, x0 + 7, y, OCC)
            c.hline(x0 + 4, x0 + 6, y + 1, CUT[1])
    return c


SHAPES = [
    [(3, 7), (16, 4), (22, 8), (22, 13), (4, 14)],
    [(5, 5), (19, 6), (21, 13), (6, 14), (3, 10)],
    [(2, 9), (11, 3), (23, 7), (21, 14), (5, 14)],
]


def audit(name, c):
    bad = c.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:5]))
        return False
    return True


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True

    strip = Image.new("RGBA", (SW * SCALE * 15, SH * SCALE), (0, 0, 0, 0))
    for n in range(1, 6):
        for s, state in enumerate(STATES):
            c = ward_stone(n, state)
            ok = audit("ward stone %d %s" % (n, state), c) and ok
            strip.alpha_composite(c.scaled(SCALE), (((n - 1) * 3 + s) * SW * SCALE, 0))
    strip.save(os.path.join(OUT, "ward-stone.png"))

    heart = Image.new("RGBA", (HW * SCALE * 2, HH * SCALE), (0, 0, 0, 0))
    for i, lit in enumerate((False, True)):
        c = ward_heart(lit)
        ok = audit("ward heart %s" % ("lit" if lit else "dark"), c) and ok
        heart.alpha_composite(c.scaled(SCALE), (i * HW * SCALE, 0))
    heart.save(os.path.join(OUT, "ward-heart.png"))

    frags = []
    for i, (shape, notches) in enumerate(((SHAPES[0], 2), (SHAPES[1], 1), (SHAPES[2], 2))):
        c = fragment(shape, notches, False)
        ok = audit("ward fragment %d" % (i + 1), c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, "ward-fragment-%d.png" % (i + 1)))
        frags.append(img)
    for i, (shape, notches) in enumerate(((SHAPES[1], 2), (SHAPES[2], 1))):
        c = fragment(shape, notches, True)
        ok = audit("ward fragment warm %d" % (i + 1), c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, "ward-fragment-warm-%d.png" % (i + 1)))
        frags.append(img)
    print("  ruins secrets  15 ward-stone frames, the heart, 5 fragments")

    pad = 8
    pw = max(strip.size[0], heart.size[0] + sum(f.size[0] + pad for f in frags)) + pad * 2
    ph = strip.size[1] + heart.size[1] + pad * 3
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(ST[3]))
    sheet.alpha_composite(strip, (pad, pad))
    y = strip.size[1] + pad * 2
    sheet.alpha_composite(heart, (pad, y))
    x = pad * 2 + heart.size[0]
    for f in frags:
        sheet.alpha_composite(f, (x, y))
        x += f.size[0] + pad
    sheet.resize((sheet.size[0] * 2, sheet.size[1] * 2), Image.NEAREST).save(PREVIEW)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
