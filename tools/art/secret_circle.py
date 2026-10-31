# -*- coding: utf-8 -*-
"""The Broken Circle's secrets (.ai/achievements-and-secrets.md 8.3). Run: python tools/art/secret_circle.py

    world/heath/fallen-star-crater.png   BC1, the Fallen Star (the spell vault), two frames: the crater with
                                         the star's stone lying in it, dark, and the stone split open and
                                         empty once its light has gone to him
    world/heath/star-shard.png           three shards of it, the seven warm things strewn along the trail
    world/heath/scorch-1.png .. -3.png   burnt heather: the trail the star cut across the heath, laid in a
                                         dead straight line by FallenStar.cs
    world/heath/scorch-tuft-1.png, -2    singed tufts and fused glass, its far tell round the crater
    world/heath/star-glass-1.png, -2     (SecretFarTell; the glass only near)
    tools/art/_secret_circle_preview.png (gitignored) on the heather, for looking at

"A line of burnt heather running dead straight across the heath, glinting at intervals." Nothing else on
a moor is straight; the trail is the tell, and the seven shards along it are the glints. At its end, a
crater: a bowl punched in the peat, its far lip thrown up and lit, its near wall in shadow, fused glass
round its rim, and in it the star's own stone - violet-black, cracked, with gold in the crack. The heath
is the quietest ground in the game (terrain_heath.py: nothing reaches the bright end of a ramp), so the
burn is drawn in occlusion, stone's deep stops and gold's dead end, and only the shards and the stone's
heart are warm.

Saved at x2. FallenStar.cs stands the crater's middle on the node and draws the burn and the shards on
the world's 2-unit grid; the glints and the lights on the rim are drawn in code on top.
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
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "heath")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_circle_preview.png")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


GO = ramp("gold")
FL = ramp("flesh")
LN = ramp("linen")
ST = ramp("stone")
VI = ramp("violet")
SL = ramp("steel")
LT = bl.ELEMENTS["light"]


def th(x, y):
    return raster.DITHER[y % 8][x % 8] / 64.0


def hashed(k, salt):
    v = (k * 2654435761 + salt * 40503 + 977) % 4294967296
    v = (v ^ (v >> 13)) * 1274126177 % 4294967296
    return (v % 100003) / 100003.0


# --- the crater ----------------------------------------------------------------------------------------

CW, CH = 96, 60
CCX, CCY = 48, 34          # the bowl's middle: FallenStar.cs stands it on the node
RX, RY = 40, 20            # the rim
STONE = (48, 33)           # the star's stone; its heart, where the light is, one row up


def crater(opened):
    c = raster.Canvas(CW, CH, None)
    # The burn round it: scorched heather thinning outward, so the bowl sits in a black scar.
    for y in range(CH):
        for x in range(CW):
            d = math.hypot((x - CCX) / (RX + 7.0), (y - CCY) / (RY + 6.0))
            if d > 1.0:
                continue
            if (1.0 - d) * 2.2 + 0.15 * math.sin(x * 0.7 + y * 0.4) > th(x, y):
                c.set(x, y, OCC if hashed(x * 5 + y * 11, 3) > 0.25 else ST[4])
    # The bowl: inside the rim, the far wall lit (it faces the key), the near wall in shadow, the floor dark.
    for y in range(CCY - RY, CCY + RY + 1):
        for x in range(CCX - RX, CCX + RX + 1):
            nx = (x - CCX) / float(RX)
            ny = (y - CCY) / float(RY)
            d = math.sqrt(nx * nx + ny * ny)
            if d > 1.0:
                continue
            if d > 0.86:
                # the lip, thrown up: lit on the far (upper) side, sunk on the near
                tone = ST[1] if ny < -0.3 else (ST[3] if ny < 0.4 else ST[2])
                if ny < -0.6 and th(x, y) < 0.3:
                    tone = ST[0]
            elif ny < 0:
                # the far inner wall catches the light, darkening down into the bowl
                t = (-ny) * 1.2 - (1.0 - d) * 0.6
                tone = ST[2] if t > th(x, y) else ST[3]
            else:
                # the near inner wall faces away: shadow, then the floor's deep
                tone = ST[4] if (ny + (1.0 - d)) * 0.9 > th(x, y) else OCC
            c.set(x, y, tone)
    # Fused glass round the rim: dark lumps with a single cold glint each (cold: only the star is warm).
    for k in range(14):
        a = k * math.pi * 2 / 14 + 0.21 * (k % 3)
        x = int(round(CCX + math.cos(a) * (RX + 1)))
        y = int(round(CCY + math.sin(a) * (RY + 1)))
        c.set(x, y, VI[3])
        c.set(x + 1, y, VI[4])
        if k % 2 == 0:
            c.set(x, y - 1, SL[1] if math.sin(a) < 0 else SL[2])
    # Clods thrown out of it, lying on the burn.
    for k in range(12):
        a = hashed(k, 7) * math.pi * 2
        r = 1.15 + hashed(k, 9) * 0.25
        x = int(CCX + math.cos(a) * RX * r)
        y = int(CCY + math.sin(a) * RY * r)
        c.set(x, y, LN[3])
        c.set(x + 1, y, LN[4])
        c.set(x, y + 1, OCC)
    sx, sy = STONE
    if not opened:
        # The star's stone: a violet-black boulder half sunk in the floor, lit on its upper left, cracked,
        # with the gold showing in the crack - the light he missed.
        for y in range(sy - 7, sy + 4):
            for x in range(sx - 9, sx + 10):
                nx = (x - sx) / 9.0
                ny = (y - (sy - 1)) / 6.5
                d = math.sqrt(nx * nx + ny * ny)
                if d > 1.0:
                    continue
                v = (-nx - ny) * 0.7 - (0.3 if d > 0.8 else 0.0)
                if v > 0.55:
                    tone = VI[1]
                elif v > 0.1 + 0.3 * (th(x, y) - 0.5):
                    tone = VI[2]
                elif v > -0.4:
                    tone = VI[3]
                else:
                    tone = VI[4]
                c.set(x, y, tone)
        c.hline(sx - 8, sx + 8, sy + 3, OCC)
        for (x0, y0, x1, y1) in ((sx - 4, sy - 6, sx - 1, sy - 2), (sx - 1, sy - 2, sx + 3, sy), (sx + 3, sy, sx + 6, sy - 3)):
            c.line(x0, y0, x1, y1, GO[3])
        c.set(sx - 1, sy - 2, GO[2])
        c.set(sx, sy - 2, GO[2])
        c.set(sx + 1, sy - 1, GO[3])
    else:
        # Split open and empty: two halves fallen apart, their broken faces dark, the hollow where the
        # light was a black pit between them.
        for (cx, lean) in ((sx - 6, -1), (sx + 6, 1)):
            for y in range(sy - 5, sy + 4):
                for x in range(cx - 5, cx + 6):
                    nx = (x - cx) / 5.0
                    ny = (y - (sy - 1)) / 4.5
                    if nx * nx + ny * ny > 1.0:
                        continue
                    inner = (x - cx) * lean < -1
                    tone = VI[4] if inner else (VI[2] if (-nx - ny) > 0.2 else VI[3])
                    c.set(x, y, tone)
        c.disc(sx, sy, 3, 2, OCC)
        c.hline(sx - 11, sx + 11, sy + 4, OCC)
    return c


# --- the shards, the burn, the far tell ------------------------------------------------------------------

SHARD = 14


def shard(k):
    """A shard of the star: a jagged sliver of its violet-black stone with the warm running through it, its
    broken edge lit gold. Lying on the heather, so it has a contact line."""
    c = raster.Canvas(SHARD, SHARD, None)
    shapes = [
        [(2, 11), (5, 3), (8, 1), (11, 6), (9, 11)],
        [(1, 10), (4, 5), (10, 2), (12, 8), (7, 11)],
        [(3, 11), (4, 4), (7, 2), (10, 5), (11, 11)],
    ]
    pts = shapes[k % len(shapes)]
    c.poly_shade(pts + [pts[0]], [VI[1], VI[2], VI[3], VI[4]], ang=-0.8)
    # the warm vein through it, and its lit broken edge
    (x0, y0), (x1, y1) = pts[1], pts[3]
    c.line(x0 + 1, y0 + 1, x1 - 1, y1, GO[1])
    c.line(pts[1][0], pts[1][1], pts[2][0], pts[2][1], LT[2])
    c.set((x0 + x1) // 2, (y0 + y1) // 2, LT[1])
    c.hline(2, 11, 12, OCC)
    return c


def scorch(k, w=26, h=14):
    """Burnt heather: a ragged blotch of char and ash with the stems still standing black in it."""
    c = raster.Canvas(w, h, None)
    cx, cy = w / 2.0, h / 2.0
    for y in range(h):
        for x in range(w):
            d = math.hypot((x - cx) / (cx - 0.5), (y - cy) / (cy - 0.5))
            wob = 0.25 * math.sin(x * 0.9 + k * 2.1) + 0.2 * math.sin(y * 1.3 - k)
            cover = (1.0 - d) * 2.0 + wob
            if cover <= th(x, y) or d > 1.0:
                continue
            tone = OCC if cover > 0.9 else ST[4]
            if hashed(x * 13 + y * 7 + k, 31) < 0.06:
                tone = ST[2]               # ash
            c.set(x, y, tone)
    # charred stems standing up out of it
    for j in range(5 + k):
        x = int(2 + hashed(j, 40 + k) * (w - 4))
        y = int(h / 2 + (hashed(j, 50 + k) - 0.5) * (h - 4))
        c.set(x, y, GO[4])
        c.set(x, y - 1, GO[4])
        c.set(x + (1 if j % 2 else -1), y - 2, OCC)
    return c


def star_glass(k):
    """Fused glass: beads of the heath melted where the star came down - dark, with one cold glint."""
    c = raster.Canvas(14, 9, None)
    for j, (x, y) in enumerate(((3, 5), (7, 3), (10, 6), (5, 7))[: 3 + k]):
        c.set(x, y, VI[3])
        c.set(x + 1, y, VI[4])
        c.set(x, y + 1, VI[4])
        c.set(x + 1, y + 1, OCC)
        c.set(x, y - 1 if j % 2 else y, SL[1] if j == 0 else VI[2])
    return c


# --- out ----------------------------------------------------------------------------------------------

def audit(name, c):
    bad = c.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:5]))
        return False
    return True


def strip(frames, w, h):
    img = Image.new("RGBA", (w * SCALE * len(frames), h * SCALE), (0, 0, 0, 0))
    for i, c in enumerate(frames):
        img.alpha_composite(c.scaled(SCALE), (i * w * SCALE, 0))
    return img


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True
    sheets = []

    frames = [crater(False), crater(True)]
    for i, c in enumerate(frames):
        ok = audit("fallen-star-crater frame %d" % i, c) and ok
    img = strip(frames, CW, CH)
    img.save(os.path.join(OUT, "fallen-star-crater.png"))
    sheets.append(img)

    frames = [shard(k) for k in range(3)]
    for i, c in enumerate(frames):
        ok = audit("star-shard %d" % i, c) and ok
    img = strip(frames, SHARD, SHARD)
    img.save(os.path.join(OUT, "star-shard.png"))
    sheets.append(img)

    row = []
    singles = [("scorch-%d" % (k + 1), scorch(k)) for k in range(3)]
    singles += [("scorch-tuft-%d" % (k + 1), scorch(k + 3, 14, 9)) for k in range(2)]
    singles += [("star-glass-%d" % (k + 1), star_glass(k)) for k in range(2)]
    for name, c in singles:
        ok = audit(name, c) and ok
        one = c.scaled(SCALE)
        one.save(os.path.join(OUT, name + ".png"))
        row.append(one)
    tells = Image.new("RGBA", (sum(r.size[0] + 8 for r in row), max(r.size[1] for r in row)), (0, 0, 0, 0))
    x = 0
    for r in row:
        tells.alpha_composite(r, (x, 0))
        x += r.size[0] + 8
    sheets.append(tells)
    print("  fallen star  crater 2 frames at %dx%d, 3 shards, 3 scorches, 2 tufts, 2 glass"
          % (CW * SCALE, CH * SCALE))

    pad = 10
    pw = max(s.size[0] for s in sheets) + pad * 2
    ph = sum(s.size[1] for s in sheets) + pad * (len(sheets) + 1)
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(VI[3]))
    y = pad
    for s in sheets:
        sheet.alpha_composite(s, (pad, y))
        y += s.size[1] + pad
    sheet.resize((sheet.size[0] * 2, sheet.size[1] * 2), Image.NEAREST).save(PREVIEW)
    print("preview: tools/art/_secret_circle_preview.png")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
