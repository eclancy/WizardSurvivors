# -*- coding: utf-8 -*-
"""The Blighted Swamp's secrets (.ai/achievements-and-secrets.md 8.6). Run: python tools/art/secret_swamp.py

    world/swamp/reed-lean-1.png, -2   sedge leaning to +x: the far tell. Nearer the Swallow-Hole there are
                                      more of them, and every one leans toward it (SecretFarTell.LeanToward
                                      flips the ones east of it, so they lean back west)
    world/swamp/reed-mire-1.png, -2   the same sedge standing in a puddle of the hole's black mud: the far
                                      tell's near band, where the bog has started to go black
    world/swamp/rim-reed-1.png .. -3  taller reeds bowed hard over: they stand round the hole's rim, all
                                      leaning in (SwallowHole.cs places and flips them)
    tools/art/_secret_swamp_preview.png  (gitignored) on a strip of bog, for looking at

"A sinkhole of black mud in the bog. The reeds lean toward it, and something glints at the bottom."
The hole itself is not here: it is a surface, drawn by scenes/shaders/swallow_hole.gdshader on the
world's 2-unit cell (the mire's way), because it moves - it swirls, bubbles, gulps what walks in and
heaves when it gives up the light. These are what stands round it.

THE SAME REEDS AS THE BOG'S. The swamp's own tussocks (trees.reed_tuft) are verdant blades, linen tips
and flesh-brown cattails; these are drawn the same way, so the far tell reads as the bog's reeds doing
something, not as new plants. The lean is the whole tell: a reed's blades all bend the same way, more
toward the top (a curve, not a slant), so a clump reads as pulled.

THE MUD. Black mud is the occlusion tone and stone's deep, with a wet sheen of lichen on the lip that
faces the key light (the down-right one, as the mire's waterline does): black, not a hole in the
picture. Nothing here is emissive.

CELLS. Authored 1:1, saved at x2 (.ai/art-direction.md section 1). Every canvas has the same margin
under its foot - FOOT_MARGIN art pixels - so one FootFromBottom serves every texture in the far tell
(SecretFarTell has one): the contact line is FOOT_MARGIN pixels up from the bottom edge, which at x2
is 2 * FOOT_MARGIN - 1 texture pixels to the middle of that row (SwallowHole.FootFromBottom).

Runs on Python 2.7 and 3.13 alike, with the same output: its own integer generator, not `random`.
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
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "swamp")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_swamp_preview.png")
SCALE = 2
FOOT_MARGIN = 6


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


VE = ramp("verdant")
LN = ramp("linen")
FL = ramp("flesh")
ST = ramp("stone")
LI = ramp("lichen")


class Rnd(object):
    """xorshift32: the same numbers on every Python."""

    def __init__(self, seed):
        self.s = ((seed * 2654435761) + 12345) & 0xFFFFFFFF or 1

    def next(self):
        s = self.s
        s ^= (s << 13) & 0xFFFFFFFF
        s ^= s >> 17
        s ^= (s << 5) & 0xFFFFFFFF
        self.s = s & 0xFFFFFFFF
        return self.s

    def random(self):
        return self.next() / 4294967296.0

    def randint(self, a, b):
        return a + self.next() % (b - a + 1)


def mud_puddle(c, cx, base, rx, ry, seed):
    """Black mud round a foot: occlusion in the middle, stone's deep at the ragged edge, and a wet sheen
    of lichen along the lip that faces the key (down and to the right), as the mire's waterline is lit."""
    rnd = Rnd(seed)
    for y in range(int(base - ry) - 1, int(base + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            nx = (x - cx) / float(rx)
            ny = (y - base) / float(ry)
            d = math.sqrt(nx * nx + ny * ny)
            wobble = 0.9 + 0.12 * math.sin(x * 1.3 + seed) + 0.06 * math.sin(y * 2.1)
            if d > wobble:
                continue
            th = raster.DITHER[y % 8][x % 8] / 64.0
            if d > wobble - 0.22:
                tone = ST[4] if (wobble - d) / 0.22 < th else OCC
            else:
                tone = OCC
            c.set(x, y, tone)
    # The sheen: a short broken arc on the lower-right lip, and a bubble.
    for k in range(7):
        a = 0.2 + k * 0.18
        x = int(round(cx + math.cos(a) * rx * 0.72))
        y = int(round(base + math.sin(a) * ry * 0.62))
        if k % 3 != 2:
            c.set(x, y, LI[2] if k % 2 else LI[3])
    bx = int(round(cx - rx * 0.3 + rnd.randint(-2, 2)))
    c.set(bx, int(base), LI[1])
    c.set(bx + 1, int(base), LI[3])


def reeds(c, cx, base, h, lean, seed, blades=14, cattails=(1, 2), spread=None):
    """A clump of sedge bowed toward +x: each blade curves over more toward its tip (lean * t ** 1.7).
    Lit from the upper left: a blade's left side is its base tone, its right side shade, every fourth
    pixel catching the light; the bowed tips go to linen, as the bog's own tussocks do."""
    rnd = Rnd(seed)
    w = spread if spread is not None else max(6, int(h * 0.35))
    for k in range(blades):
        x0 = cx + rnd.randint(-w // 2, w // 2)
        ln = h * (0.5 + rnd.random() * 0.5)
        own = (rnd.random() - 0.35) * 0.18 * h
        phase = rnd.randint(0, 3)
        steps = int(ln)
        for j in range(steps):
            t = j / float(max(1, steps))
            x = int(round(x0 + (lean + own) * (t ** 1.7) * (ln / float(h))))
            y = base - j
            if t > 0.78:
                col = LN[2] if j % 2 else LN[3]
            elif (j + phase) % 4 == 0:
                # Each blade catches the light on its own beat, so the clump is not striped across.
                col = VE[2]
            else:
                col = VE[3] if x <= cx else VE[4]
            c.set(x, y, col)
            # Thicken the lower half a little: blades are wider at the root.
            if t < 0.35 and k % 2 == 0:
                c.set(x + 1, y, VE[4])
    for k in range(rnd.randint(cattails[0], cattails[1])):
        x0 = cx + rnd.randint(-w // 4, w // 4)
        top = h - 2 - rnd.randint(0, 4)
        # The stem bows with the rest; the head hangs at its end, the flesh row's brown.
        hx, hy = x0, base
        for j in range(int(top)):
            t = j / float(max(1, top))
            hx = int(round(x0 + lean * 0.9 * (t ** 1.7)))
            hy = base - j
            if j > 2:
                c.set(hx, hy, LN[3])
        for j in range(5):
            dx = int(round(lean * 0.12 * j / 5.0))
            c.set(hx + dx, hy - j + 4, FL[3] if j else FL[2])
            c.set(hx + dx + 1, hy - j + 4, FL[4])


def shadow(c, cx, base, rx):
    c.disc(cx, base, rx, 2, OCC)


def reed_lean(seed, w, h, lean):
    c = raster.Canvas(w, h + FOOT_MARGIN, None)
    base = h
    cx = w // 2 - int(lean * 0.35)
    shadow(c, cx, base, w * 0.3)
    reeds(c, cx, base, h, lean, seed)
    return c


def reed_mire(seed, w, h, lean):
    c = raster.Canvas(w, h + FOOT_MARGIN, None)
    base = h
    cx = w // 2 - int(lean * 0.35)
    mud_puddle(c, cx + 1, base + 1, w * 0.42, 4.2, seed)
    reeds(c, cx, base, h, lean, seed + 7, blades=12)
    return c


def rim_reed(seed, w, h, lean, cattails):
    c = raster.Canvas(w, h + FOOT_MARGIN, None)
    base = h
    cx = 8
    shadow(c, cx, base, 6)
    reeds(c, cx, base, h, lean, seed, blades=16, cattails=cattails, spread=10)
    return c


SPRITES = [
    ("reed-lean-1", lambda: reed_lean(301, 30, 32, 9)),
    ("reed-lean-2", lambda: reed_lean(307, 26, 26, 7)),
    ("reed-mire-1", lambda: reed_mire(311, 34, 30, 9)),
    ("reed-mire-2", lambda: reed_mire(317, 30, 24, 7)),
    ("rim-reed-1", lambda: rim_reed(331, 34, 44, 16, (2, 3))),
    ("rim-reed-2", lambda: rim_reed(337, 30, 38, 13, (1, 2))),
    ("rim-reed-3", lambda: rim_reed(347, 26, 30, 9, (0, 1))),
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
    images = []
    for name, make in SPRITES:
        c = make()
        ok = audit(name, c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, name + ".png"))
        images.append(img)
    print("  swamp secrets  %d sprites (far tell, near band, rim)" % len(images))

    # The preview: every sprite on the bog's floor tone, then mirrored, three times up.
    pad = 8
    pw = sum(i.size[0] + pad for i in images) + pad
    ph = max(i.size[1] for i in images) * 2 + pad * 3
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(LI[4]))
    x = pad
    for img in images:
        sheet.alpha_composite(img, (x, pad))
        sheet.alpha_composite(img.transpose(Image.FLIP_LEFT_RIGHT), (x, pad * 2 + images[0].size[1] + 20))
        x += img.size[0] + pad
    sheet.resize((sheet.size[0] * 3, sheet.size[1] * 3), Image.NEAREST).save(PREVIEW)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
