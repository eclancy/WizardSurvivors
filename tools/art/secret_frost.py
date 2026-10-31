# -*- coding: utf-8 -*-
"""The Frozen Waste's secrets (.ai/achievements-and-secrets.md 8.8). Run: python tools/art/secret_frost.py

    world/frost/rune-stone.png         the Buried Runes' three stones, six frames left to right: stone 1
                                       buried, stone 1 dug, stone 2 buried, ... (BuriedRunes.cs reads it as
                                       Hframes = 6, frame 2 * k + dug). Flat slabs in the snow, seen from above
    world/frost/drift-1 .. -3          snow drifts carved by one wind - the long lee slope to the west, the
                                       steep face to the east - commoner toward the drift field: the far tell
    world/frost/drift-plume-1, -2      the same with spindrift streaming off the crest: the far tell's near
                                       band, where the wind is always up
    tools/art/_secret_frost_preview.png  (gitignored) on the waste's night floor, for looking at

"A drift field where the snow never stops blowing, except round anyone who stands still." The blowing
snow itself is not here: it moves, so it is drawn on the world's 2-unit cell by
scenes/shaders/drift_field.gdshader. These are what lies in it.

THE RUNES ARE SLEET RUNES'. The spell they teach throws runes of rime on the wind (tools/art/sleet_runes.py:
a diamond frame with a lit point at each corner, a ring inside, a white-hot heart). Each stone here is cut
with that diamond and something different inside it - the ring, a bar, a cross - so the three read as
three, and as the spell. Buried, the cut shows dark through the snow lying on the slab; dug, the snow is
thrown off round the edge and the cut holds Ice's light, banded with Wind's at the corners.

THE STONE. Flat slabs of the heath's granite family (bone's lower stops lit, stone's upper stops in
shade), seen from above with a thin dark edge toward the viewer, so they lie IN the snow rather than
standing on it. The snow is bone's upper stops - the palest thing on the waste's night floor.

THE DRIFTS. Every drift lies the same way, because one wind made them all: that, and their thickening,
is the tell. Lit from the upper left like everything else; the lee slope catches the key, the steep
windward face is in shade.

CELLS. Authored 1:1, saved at x2. Every drift has DRIFT_MARGIN art pixels under its foot, so one
SecretFarTell.FootFromBottom serves them (BuriedRunes.DriftFoot).

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
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "frost")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_frost_preview.png")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


SK = ramp("skin")
ST = ramp("stone")
AR = ramp("arcane")
ICE = bl.ELEMENTS["ice"]       # core, hot, mid, edge
WIND = bl.ELEMENTS["wind"]

SNOW = [SK[0], SK[1], SK[2], SK[3]]          # bone's upper stops: the snow
SLAB = [SK[3], SK[4], ST[0], ST[1], ST[2]]   # the heath's granite

RW, RH = 40, 24          # a rune-stone's frame
RCX, RCY = 20, 11        # the slab's middle (BuriedRunes puts the frame's middle on the rune's point)


def h2(x, y, s):
    """A fixed hash in 0..1: the same on every Python."""
    n = (x * 374761393 + y * 668265263 + s * 2246822519) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) / 4294967296.0


def slab(c, seed):
    """A flat stone in the snow: its top lit toward the upper left, a two-pixel edge toward the viewer."""
    rx, ry = 15.0, 7.0
    c.disc(RCX + 1, RCY + 3, rx + 1, ry, OCC)
    for y in range(int(RCY - ry) - 1, int(RCY + ry) + 3):
        for x in range(int(RCX - rx) - 1, int(RCX + rx) + 2):
            wob = 1.0 + 0.08 * math.sin(x * 0.9 + seed) + 0.06 * math.cos(y * 1.3 + seed)
            nx = (x - RCX) / rx
            top = (y - RCY) / ry
            side = (y - RCY - 2) / ry
            if nx * nx + top * top <= wob:
                t = (nx - top) * 0.5 + 0.5          # lit toward the upper left
                th = raster.DITHER[y % 8][x % 8] / 64.0
                c.set(x, y, SLAB[1] if t < 0.3 + th * 0.3 else (SLAB[2] if t < 0.7 + th * 0.2 else SLAB[3]))
            elif nx * nx + side * side <= wob:
                c.set(x, y, SLAB[4])


# Each sigil, as lines inside the diamond: (x0, y0, x1, y1) from the slab's middle.
DIAMOND = [(-7, 0, 0, -4), (0, -4, 7, 0), (7, 0, 0, 4), (0, 4, -7, 0)]
INSIDE = [
    [(-2, 0, 2, 0), (0, -1, 0, 1), (-1, -1, 1, -1), (-1, 1, 1, 1)],    # the ring (Sleet Runes' own)
    [(0, -3, 0, 3)],                                                    # a bar
    [(-3, -2, 3, 2), (-3, 2, 3, -2)],                                   # a cross
]


def rune(c, k, lit):
    """The cut. Dark: occlusion with its lower lip catching the light. Lit: Ice's ramp in the cut, white-hot
    in the middle, the diamond's four points in Wind's hot."""
    lines = DIAMOND + INSIDE[k]
    for (x0, y0, x1, y1) in lines:
        if lit:
            c.line(RCX + x0, RCY + y0, RCX + x1, RCY + y1, ICE[1])
        else:
            c.line(RCX + x0, RCY + y0 + 1, RCX + x1, RCY + y1 + 1, SLAB[0])
            c.line(RCX + x0, RCY + y0, RCX + x1, RCY + y1, OCC)
    if lit:
        for (x0, y0, x1, y1) in INSIDE[k]:
            c.line(RCX + x0, RCY + y0, RCX + x1, RCY + y1, ICE[0])
        for (x, y) in ((-7, 0), (7, 0), (0, -4), (0, 4)):
            c.set(RCX + x, RCY + y, WIND[1])
        c.set(RCX, RCY, ICE[0])
        # The edge of the light on the stone round the cut.
        for (x0, y0, x1, y1) in DIAMOND:
            c.line(RCX + x0, RCY + y0 + 1, RCX + x1, RCY + y1 + 1, ICE[3])


def snow_over(c, seed, cover):
    """Snow lying on the slab and round it in clumps of bone's upper stops - lit on their upper left,
    shaded under - and blown clear of the middle, so the cut's dark diamond shows through: what the
    settling snow uncovers is the rune, not the stone."""
    for y in range(RH):
        for x in range(RW):
            nx = (x - RCX) / 17.0
            ny = (y - RCY - 1) / 8.5
            d = math.hypot(nx, ny)
            if d > 1.0:
                continue
            # The middle is blown clear, a little ragged.
            if abs(nx) < 0.5 + 0.06 * math.sin(y * 1.7 + seed) and abs(ny) < 0.62:
                continue
            n = h2(x // 4, y // 3, seed)
            if n >= cover:
                continue
            # Where in its clump this pixel is: the clump's upper-left cells are lit, its last row shaded.
            above = h2(x // 4, (y - 1) // 3, seed) < cover and (y - 1) // 3 == y // 3
            left = h2((x - 1) // 4, y // 3, seed) < cover and (x - 1) // 4 == x // 4
            tone = SNOW[0] if not above and not left else (SNOW[2] if y % 3 == 2 else SNOW[1])
            c.set(x, y, tone)


def heap(c, seed):
    """Snow thrown off the dug stone, heaped in lumps round its rim, lit on top and shaded under."""
    for k in range(11):
        a = (k + 0.4 * h2(k, 3, seed)) * math.pi * 2.0 / 11
        x = int(round(RCX + math.cos(a) * 17.5))
        y = int(round(RCY + 1 + math.sin(a) * 9.0))
        w = 2 + int(h2(k, 5, seed) * 2)
        c.hline(x - w, x + w, y, SNOW[1])
        c.hline(x - w + 1, x + w - 1, y - 1, SNOW[0])
        c.hline(x - w, x + w, y + 1, SNOW[3])


def rune_stone(k, dug):
    c = raster.Canvas(RW, RH, None)
    slab(c, k * 3 + 1)
    rune(c, k, dug)
    if dug:
        heap(c, k)
    else:
        snow_over(c, k + 11, 0.62)
    return c


DW, DH = 40, 16          # a drift's frame
DRIFT_MARGIN = 3


def drift(seed, length, height, plume):
    """A wind-carved drift: a long lee slope rising from the west to a crest, and a steep face falling to
    the east. Lit from the upper left, so the lee catches the key and the face is in shade."""
    c = raster.Canvas(DW, DH, None)
    base = DH - DRIFT_MARGIN
    x0 = (DW - length) // 2
    crest = x0 + int(length * 0.72)
    c.hline(x0 + 1, x0 + length - 1, base + 1, OCC)
    for x in range(x0, x0 + length):
        if x <= crest:
            hgt = height * ((x - x0) / float(crest - x0)) ** 1.4
        else:
            hgt = height * (1.0 - (x - crest) / float(x0 + length - crest)) ** 0.7
        hgt += 0.6 * math.sin(x * 0.7 + seed)
        top = int(round(base - hgt))
        for y in range(top, base + 1):
            th = raster.DITHER[y % 8][x % 8] / 64.0
            if x > crest:
                tone = SNOW[3] if (y - top) > 1 else SNOW[2]
            else:
                depth = (y - top) / max(1.0, hgt)
                tone = SNOW[1] if depth < 0.3 + th * 0.2 else (SNOW[2] if depth < 0.8 else SNOW[3])
            c.set(x, y, tone)
        if x <= crest and top < base:
            c.set(x, top, SNOW[0] if (x - x0) > length * 0.3 else SNOW[1])
    if plume:
        # Spindrift streaming west off the crest: broken lines of the snow's palest, thinning out.
        ctop = int(round(base - height))
        for k in range(4):
            y = ctop - 1 - k
            for x in range(crest - 2 - k * 3, max(0, crest - 18 - k * 4), -1):
                if h2(x, y, seed + k) < 0.55 - k * 0.1:
                    c.set(x, y, SNOW[0] if k == 0 else SNOW[1])
    return c


DRIFTS = [
    ("drift-1", lambda: drift(3, 34, 7, False)),
    ("drift-2", lambda: drift(5, 26, 5, False)),
    ("drift-3", lambda: drift(9, 38, 9, False)),
    ("drift-plume-1", lambda: drift(13, 34, 8, True)),
    ("drift-plume-2", lambda: drift(17, 28, 6, True)),
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
    strip = Image.new("RGBA", (RW * SCALE * 6, RH * SCALE), (0, 0, 0, 0))
    for k in range(3):
        for dug in (False, True):
            c = rune_stone(k, dug)
            ok = audit("rune stone %d %s" % (k + 1, "dug" if dug else "buried"), c) and ok
            strip.alpha_composite(c.scaled(SCALE), ((2 * k + (1 if dug else 0)) * RW * SCALE, 0))
    strip.save(os.path.join(OUT, "rune-stone.png"))

    drifts = []
    for name, make in DRIFTS:
        c = make()
        ok = audit(name, c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, name + ".png"))
        drifts.append(img)
    print("  frost secrets  6 rune-stone frames, %d drifts" % len(drifts))

    pad = 8
    pw = max(strip.size[0], sum(d.size[0] + pad for d in drifts)) + pad * 2
    ph = strip.size[1] + drifts[0].size[1] + pad * 3
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(ST[3]))
    sheet.alpha_composite(strip, (pad, pad))
    x = pad
    for d in drifts:
        sheet.alpha_composite(d, (x, strip.size[1] + pad * 2))
        x += d.size[0] + pad
    sheet.resize((sheet.size[0] * 3, sheet.size[1] * 3), Image.NEAREST).save(PREVIEW)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
