# -*- coding: utf-8 -*-
"""The Scorched Sands' secrets (.ai/achievements-and-secrets.md 8.9). Run: python tools/art/secret_sands.py

    world/sands/fulgurite.png          the Fulgurite, three frames left to right: still (the glint is drawn
                                       over its root in code), charged (the storm is on it: its veins lit),
                                       ringing (the last strike drawn into it). Fulgurite.cs reads Hframes = 3
    world/sands/glass-beads-1 .. -3    beads of fused sand glass lying half sunk: the far tell, commoner
                                       toward it
    world/sands/glass-twig-1, -2       little branches of the same glass standing out of the sand: the far
                                       tell's near band
    tools/art/_secret_sands_preview.png  (gitignored) on the sands' floor tone, for looking at

"A branching tree of glass standing in the dunes, under a sky darker over it than anywhere else." Where
lightning strikes sand it fuses a tube of glass down into it, branching as the bolt branched: a
fulgurite. This one stands up out of the dune, taller than a man, the shape of the bolt that made it.

THE GLASS IS STORMSWEEP'S. The spell it teaches is Wind and Lightning, so the glass is Wind's ramp - pale,
cold, the one cold thing on a warm floor - and the light in it is Lightning's. Lit from the upper left:
each branch's left edge is Wind's hot, its body mid, its right edge Wind's edge, with occlusion where it
meets the sand. Charged, a seam of Lightning's hot runs up the inside of every branch; ringing, the whole
tree is Lightning's light with Wind's white at its tips (the contract: every element's core is white-hot).
The root is a crater of fused sand - gold's shade and deep - where the glint sits.

The storm itself (the cloud over it, the bolts, the telegraphs) is drawn in code: it moves.

CELLS. Authored 1:1, saved at x2. The far-tell pieces share BEAD_MARGIN under their feet (Fulgurite.BeadFoot).

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
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "sands")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_sands_preview.png")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


GO = ramp("gold")
ST = ramp("stone")
WIND = bl.ELEMENTS["wind"]          # core, hot, mid, edge
BOLT = bl.ELEMENTS["lightning"]

FW, FH = 64, 96                     # the tree's frame
ROOT_X, ROOT_Y = 32, 88             # its root (Fulgurite.cs anchors the frame here)

# The tree, as branches: (x0, y0, x1, y1, width at the start, width at the end). The trunk forks like a
# bolt seen upside down - the long leader up, the side branches kinking off it and splitting again.
BRANCHES = [
    (32, 88, 30, 66, 4.5, 3.6),
    (30, 66, 34, 46, 3.6, 3.0),
    (34, 46, 31, 26, 3.0, 2.2),
    (31, 26, 33, 9, 2.2, 1.0),
    (30, 66, 19, 52, 2.6, 1.8),
    (19, 52, 14, 34, 1.8, 1.0),
    (19, 52, 9, 47, 1.4, 1.0),
    (34, 46, 45, 35, 2.4, 1.6),
    (45, 35, 49, 18, 1.6, 1.0),
    (45, 35, 54, 32, 1.2, 1.0),
    (31, 76, 41, 68, 1.8, 1.2),
    (41, 68, 46, 58, 1.2, 1.0),
    (31, 26, 24, 16, 1.4, 1.0),
    (14, 34, 10, 26, 1.0, 1.0),
]
TIPS = [(33, 9), (14, 34), (9, 47), (49, 18), (54, 32), (46, 58), (24, 16), (10, 26)]


def branch(c, x0, y0, x1, y1, w0, w1, state):
    """A tapered glass tube from (x0, y0) to (x1, y1): lit edge on the key side, body, shaded edge, and -
    charged - a seam of the bolt's light up its middle."""
    length = max(1.0, math.hypot(x1 - x0, y1 - y0))
    nx, ny = -(y1 - y0) / length, (x1 - x0) / length       # across the branch
    if nx > 0:
        nx, ny = -nx, -ny                                  # point the normal to the left (the key's side)
    steps = int(length * 3)
    for s in range(steps + 1):
        t = s / float(steps)
        cx = x0 + (x1 - x0) * t
        cy = y0 + (y1 - y0) * t
        w = w0 + (w1 - w0) * t
        for k in range(-int(w + 1), int(w + 1) + 1):
            a = k / max(0.5, w)
            if abs(a) > 1.0:
                continue
            x = int(round(cx + nx * k * 0.5))
            y = int(round(cy + ny * k * 0.5))
            if state == "ringing":
                tone = BOLT[0] if abs(a) < 0.35 else (BOLT[1] if a < 0.4 else BOLT[2])
            elif a < -0.45:
                tone = WIND[1]                             # the lit edge
            elif a > 0.5:
                tone = WIND[3]                             # the shaded edge
            elif state == "charged" and abs(a) < 0.3:
                tone = BOLT[1]                             # the seam of light inside
            else:
                tone = WIND[2]
            c.set(x, y, tone)
        # Occlusion just outside the shaded edge, so the glass stands off the sand.
        if state != "ringing":
            ox = int(round(cx - nx * (w * 0.5 + 1)))
            oy = int(round(cy - ny * (w * 0.5 + 1)))
            if c.get(ox, oy)[3] == 0:
                c.set(ox, oy, OCC)


def root(c):
    """The crater of fused sand the tree stands in: glassy gold-brown, lit on its far lip."""
    c.disc(ROOT_X, ROOT_Y + 1, 13, 4.2, OCC)
    c.disc(ROOT_X, ROOT_Y, 12, 3.6, GO[4])
    c.disc(ROOT_X - 1, ROOT_Y - 1, 8, 2.2, GO[3])
    for k in range(18):
        a = math.pi + k * math.pi / 17
        x = int(round(ROOT_X + math.cos(a) * 11))
        y = int(round(ROOT_Y + math.sin(a) * 3.2))
        c.set(x, y, GO[2] if k % 3 else GO[1])
    # Beads of glass round the rim.
    for (dx, dy) in ((-10, 2), (9, 2), (-6, 3), (12, 0), (4, 3)):
        c.set(ROOT_X + dx, ROOT_Y + dy, WIND[2])
        c.set(ROOT_X + dx, ROOT_Y + dy - 1, WIND[1])


def fulgurite(state):
    c = raster.Canvas(FW, FH, None)
    root(c)
    for (x0, y0, x1, y1, w0, w1) in BRANCHES:
        branch(c, x0, y0, x1, y1, w0, w1, state)
    for (x, y) in TIPS:
        c.set(x, y, WIND[0] if state != "ringing" else BOLT[0])
    return c


BW, BH = 22, 14
BEAD_MARGIN = 3


def beads(seed, n):
    """Beads of sand glass half sunk in the dune: pale knobs, lit on their upper left, in a little shadow."""
    c = raster.Canvas(BW, BH, None)
    base = BH - BEAD_MARGIN
    for k in range(n):
        x = 4 + (k * 7 + seed * 3) % 14
        y = base - (k * 3 + seed) % 4
        r = 1.6 + ((k + seed) % 3) * 0.5
        c.disc(x + 1, y + 1, r + 0.6, r * 0.6, OCC)
        c.disc(x, y, r, r * 0.8, WIND[2])
        c.set(x - 1, y - 1, WIND[1])
        c.set(x + 1, y, WIND[3])
    return c


def twig(seed):
    """A little fulgurite standing out of the sand: two or three glass prongs from a fused knob."""
    c = raster.Canvas(BW, BH + 8, None)
    base = BH + 8 - BEAD_MARGIN
    c.disc(11, base, 6, 1.8, OCC)
    c.disc(11, base - 1, 4, 1.6, GO[4])
    prongs = [(11, base - 1, 9 + seed % 3, base - 12, 1.4, 0.8), (11, base - 1, 15, base - 8, 1.0, 0.6)]
    if seed % 2:
        prongs.append((11, base - 1, 6, base - 7, 0.9, 0.6))
    for (x0, y0, x1, y1, w0, w1) in prongs:
        branch(c, x0, y0, x1, y1, w0, w1, "still")
        c.set(int(x1), int(y1), WIND[0])
    return c


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
    strip = Image.new("RGBA", (FW * SCALE * 3, FH * SCALE), (0, 0, 0, 0))
    for i, state in enumerate(("still", "charged", "ringing")):
        c = fulgurite(state)
        ok = audit("fulgurite %s" % state, c) and ok
        strip.alpha_composite(c.scaled(SCALE), (i * FW * SCALE, 0))
    strip.save(os.path.join(OUT, "fulgurite.png"))

    pieces = []
    for i, n in enumerate((3, 4, 2)):
        c = beads(i + 1, n)
        ok = audit("glass beads %d" % (i + 1), c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, "glass-beads-%d.png" % (i + 1)))
        pieces.append(img)
    for i in range(2):
        c = twig(i + 1)
        ok = audit("glass twig %d" % (i + 1), c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, "glass-twig-%d.png" % (i + 1)))
        pieces.append(img)
    print("  sands secrets  3 fulgurite frames, %d far-tell pieces" % len(pieces))

    pad = 8
    pw = strip.size[0] + pad * 2 + sum(p.size[0] + pad for p in pieces)
    ph = strip.size[1] + pad * 2
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(GO[4]))
    sheet.alpha_composite(strip, (pad, pad))
    x = strip.size[0] + pad * 2
    for p in pieces:
        sheet.alpha_composite(p, (x, ph - p.size[1] - pad))
        x += p.size[0] + pad
    sheet.resize((sheet.size[0] * 2, sheet.size[1] * 2), Image.NEAREST).save(PREVIEW)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
