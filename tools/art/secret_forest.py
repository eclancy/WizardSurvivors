# -*- coding: utf-8 -*-
"""The Enchanted Forest's secrets (.ai/achievements-and-secrets.md 8.1). Run: python tools/art/secret_forest.py

    world/forest/first-tree-clearing.png   F4, the First Tree's clearing, two frames left to right: barren
                                           (the bed cracked dry) and planted (the bed turned over). The dry
                                           ground, the ring of nine stumps and the seed-bed, as one picture
    world/forest/first-tree-sapling.png    the sapling, six frames: what grows in the bed over six seconds
    tools/art/_secret_forest_preview.png   (gitignored) on the forest's turf, for looking at

THE FIRST TREE WAS DRAWN IN CODE, AND OFF THE CONTRACT. Until the secrets' pixel-art pass (2026-10-30,
the spells' contract of .ai/art-direction.md sections 1-3 and 6 brought to the secrets) FirstTree.cs drew
its clearing with DrawCircle and DrawColoredPolygon: a translucent disc of dry ground, antialiased
trapezoid stumps with rings drawn by DrawArc, a lerp-free but off-palette brown, a sapling crowned with
smooth circles. Everything here is the same picture, at the same size and in the same places - the ring
at FirstTree.RingRadius flattened to 0.62, the stump sizes from the same two little sequences, the bed at
BedRadius flattened by half, the sapling's height 10 + 35t art pixels - redrawn as pixel art in the
palette's rows: dead wood in gold's lower stops with flesh heartwood on the cut, dry ground in linen
thinned by the Bayer matrix (never a translucent disc), living leaves in verdant (the ground's own living
green, .ai/terrain.md section 4).

Saved at x2, like every world sprite. FirstTree.cs draws the clearing with its middle - the bed's middle -
on the node, and the sapling with its foot there, both on the world's 2-unit grid (SpellInk.AlignToWorldGrid).
The breathing glint is drawn in code on top (SecretSite.DrawGlint): a near tell must breathe.
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
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "forest")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_forest_preview.png")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


GO = ramp("gold")
FL = ramp("flesh")
LN = ramp("linen")
VE = ramp("verdant")
LI = ramp("lichen")
ST = ramp("stone")


def th(x, y):
    """The Bayer threshold of a pixel, 0 to 1: the one gradient the contract allows."""
    return raster.DITHER[y % 8][x % 8] / 64.0


def hashed(k, salt):
    """A fixed pseudo-random 0..1 from two integers: the same on Python 2 and 3."""
    v = (k * 2654435761 + salt * 40503 + 977) % 4294967296
    v = (v ^ (v >> 13)) * 1274126177 % 4294967296
    return (v % 100003) / 100003.0


def blob(c, cx, cy, rx, ry, tones, salt=0):
    """A rounded mass lit from the upper left: tones are (lit, base, shade, deep). Dithered between stops
    along the key axis, so it turns rather than stacks; its lower-right rim is the deepest stop."""
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            nx = (x - cx) / float(rx)
            ny = (y - cy) / float(ry)
            d = math.sqrt(nx * nx + ny * ny)
            if d > 1.0:
                continue
            # 1 on the key side, -1 away from it, pulled down at the rim.
            v = (-nx - ny) / 1.4142 * 0.9 - (0.35 if d > 0.82 else 0.0) + 0.15
            v += (hashed(x * 31 + y, salt) - 0.5) * 0.1
            t = (v + 1.0) / 2.0 * (len(tones) - 1)
            i = max(0, min(len(tones) - 2, int(t)))
            f = t - i
            # tones run light to dark; t runs dark to light
            hi_i = len(tones) - 1 - i
            lo = tones[hi_i]
            up = tones[hi_i - 1]
            c.set(x, y, up if f > th(x, y) else lo)


# --- F4, the First Tree's clearing ------------------------------------------------------------------

CW, CH = 220, 140
CCX, CCY = 110, 78          # the bed's middle: FirstTree.cs puts it on the node
RING_RX, RING_RY = 85, 53   # FirstTree.RingRadius (170) at x2, flattened to 0.62
BED_RX, BED_RY = 23, 11     # FirstTree.BedRadius (46) at x2, flattened by half
GROUND_RX, GROUND_RY = 105, 63
STUMPS = 9


def dry_ground(c):
    """The clearing: a worn patch of dry earth, thick in the middle and thinning to nothing at its edge,
    so the ring reads as a place and not as scatter - and with no edge a player could learn to stand on."""
    for y in range(CCY - GROUND_RY, CCY + GROUND_RY + 1):
        for x in range(CCX - GROUND_RX, CCX + GROUND_RX + 1):
            d = math.hypot((x - CCX) / float(GROUND_RX), (y - CCY) / float(GROUND_RY))
            if d > 1.0:
                continue
            cover = 0.8 * (1.0 - d * d) + 0.12 * math.sin(x * 0.37 + y * 0.21) + 0.08 * math.sin(x * 0.11 - y * 0.53)
            if cover > th(x, y):
                c.set(x, y, LN[3])
                if cover > 0.7 and hashed(x * 7 + y * 131, 3) < 0.05:
                    c.set(x, y, LN[4])
    # Clods and pebbles, only where the ground is bare.
    for k in range(26):
        a = hashed(k, 11) * math.pi * 2
        r = math.sqrt(hashed(k, 13)) * 0.85
        x = int(CCX + math.cos(a) * GROUND_RX * r)
        y = int(CCY + math.sin(a) * GROUND_RY * r)
        if k % 3 == 0:
            c.set(x, y, ST[1])
            c.set(x + 1, y, ST[2])
            c.set(x, y + 1, OCC)
            c.set(x + 1, y + 1, OCC)
        else:
            c.set(x, y, GO[4])
            c.set(x + 1, y, GO[4] if k % 2 else FL[4])


def stump(c, fx, fy, w, h):
    """A cut trunk flaring to its roots, its top showing the rings - wood, not rock. Lit on the left."""
    half = w // 2
    top = fy - h
    c.hline(fx - half - 4, fx + half + 4, fy + 1, OCC)
    # The roots: four rows flaring out from the trunk to the ground, the far side in shadow.
    for k in range(4):
        y = fy - k
        spread = 4 - k
        for x in range(fx - half - spread, fx + half + spread + 1):
            if x < fx - half + 1:
                tone = GO[2] if k > 0 and (x + y) % 2 == 0 else GO[3]
            elif x > fx + half - 2:
                tone = GO[4]
            else:
                tone = GO[3]
            c.set(x, y, tone)
        # a gap between the root on the left and the next, so they read as roots
        c.set(fx - half - spread + 2, y, GO[4] if k < 2 else None)
    # The trunk, a pixel narrower at the cut than at the ground.
    for y in range(top, fy - 3):
        inset = 1 if y < top + h * 0.4 else 0
        x0, x1 = fx - half + inset, fx + half - inset
        for x in range(x0, x1 + 1):
            u = (x - x0) / float(max(1, x1 - x0))
            if u < 0.22:
                tone = GO[2] if u < 0.1 or th(x, y) < 0.45 else GO[3]
            elif u < 0.72:
                tone = GO[3]
            else:
                tone = GO[4] if u > 0.85 or th(x, y) < 0.5 else GO[3]
            c.set(x, y, tone)
        # bark furrows
        for x in range(x0 + 2, x1 - 1, 3):
            if (y + x) % 5 != 0:
                c.set(x, y, GO[4])
    # The cut: heartwood, one ring and a dark heart; the lip catches the key on its upper left.
    crx = max(2, half - 1)
    cry = max(2, int(round(half * 0.45)))
    c.disc(fx, top, crx, cry, FL[2])
    for y in range(top - cry, top + cry + 1):
        for x in range(fx - crx, fx + crx + 1):
            nx = (x - fx) / float(crx)
            ny = (y - top) / float(cry)
            d = math.sqrt(nx * nx + ny * ny)
            if 0.78 < d <= 1.0:
                c.set(x, y, FL[1] if nx + ny < -0.3 else (GO[3] if nx + ny > 0.4 else FL[2]))
    if crx >= 4:
        c.ring(fx, top, crx * 0.55, max(1.2, cry * 0.55), FL[3])
    c.set(fx, top, GO[4])
    c.set(fx + 1, top, GO[4])
    # a radial check in the cut, the dry crack every old stump has
    c.line(fx + 1, top, fx + crx - 1, top + 1, FL[3])


def seed_bed(c, planted):
    """A patch of dark earth, cracked dry until the sapling is in it; turned over once it is."""
    for y in range(CCY - BED_RY, CCY + BED_RY + 1):
        for x in range(CCX - BED_RX, CCX + BED_RX + 1):
            nx = (x - CCX) / float(BED_RX)
            ny = (y - CCY) / float(BED_RY)
            d = math.sqrt(nx * nx + ny * ny)
            if d > 1.0:
                continue
            tone = LN[4]
            # its lip: the far edge lit, the near edge sunk
            if d > 0.8:
                tone = LN[3] if ny < -0.2 else OCC if ny > 0.4 else LN[4]
            elif hashed(x * 13 + y * 7, 21) < 0.08:
                tone = FL[4]
            c.set(x, y, tone)
    if not planted:
        # FirstTree's old cracks, at the same places (its px halved): dry, so the light stays dim in it.
        for (x0, y0, x1, y1) in ((-15, -2, -4, 1), (-4, 1, 3, -4), (3, -4, 14, 2), (-2, 1, -1, 7)):
            c.line(CCX + x0, CCY + y0, CCX + x1, CCY + y1, OCC)
    else:
        # Turned earth: a low mound lit on its upper left, a dark ring where the spade went in.
        blob(c, CCX, CCY - 1, 10, 4.5, [FL[3], FL[4], LN[4], OCC], salt=5)
        c.ring(CCX, CCY, 12, 6, OCC)


def stump_spots():
    """The nine stumps, as FirstTree.cs placed them: (foot x, foot y, width, height) in art pixels."""
    spots = []
    for i in range(STUMPS):
        angle = i * math.pi * 2 / STUMPS + 0.3
        x = int(round(CCX + math.cos(angle) * RING_RX))
        y = int(round(CCY + math.sin(angle) * RING_RY))
        w = int((26 + (i * 7 % 3) * 6) / 2)
        h = int((20 + (i * 5 % 4) * 5) / 2)
        spots.append((x, y, w, h))
    return sorted(spots, key=lambda s: s[1])


def clearing(planted):
    c = raster.Canvas(CW, CH, None)
    dry_ground(c)
    spots = stump_spots()
    # The bed sits among the stumps: those behind it are drawn first.
    for s in spots:
        if s[1] <= CCY:
            stump(c, *s)
    seed_bed(c, planted)
    for s in spots:
        if s[1] > CCY:
            stump(c, *s)
    return c


# --- the sapling --------------------------------------------------------------------------------------

SW, SH = 48, 60
SFX, SFY = 24, 57                     # its foot: FirstTree.cs stands it on the bed's middle
STEM = [3, 8, 15, 22, 29, 35]         # 10 + 35t art pixels at the end of each second of its growth
CROWN = [0, 0, 0, 6, 9, 12]


def leaf(c, x, y, side, big=False):
    """A leaf off the stem: a little lit blade of verdant pointing up and out."""
    pts = [(0, 0), (side * 1, -1), (side * 2, -1), (side * 3, -2)]
    if big:
        pts += [(side * 4, -2), (side * 2, -2), (side * 3, -3)]
    for (dx, dy) in pts:
        c.set(x + dx, y + dy, VE[2] if dy < -1 or side < 0 else VE[3])
    c.set(x + side * 1, y, VE[4])
    c.set(x + side * 2, y, VE[4] if big else None)


def sapling(k):
    c = raster.Canvas(SW, SH, None)
    h = STEM[k]
    c.hline(SFX - 3, SFX + 3, SFY + 1, OCC)
    wide = k >= 3
    for y in range(SFY - h, SFY + 1):
        c.set(SFX - 1, y, GO[2] if (y % 4) else GO[1])
        c.set(SFX, y, GO[3])
        if wide and y > SFY - h + 4:
            c.set(SFX + 1, y, GO[4])
    if k == 0:
        # A sprout: two seed leaves at the top of a short stem.
        leaf(c, SFX - 1, SFY - h, -1)
        leaf(c, SFX, SFY - h, 1)
        return c
    # Leaves along the stem, alternating, two more as it grows.
    for j in range(1 + min(k, 3)):
        y = SFY - 3 - int((h - 4) * (j + 1) / float(min(k, 3) + 2))
        leaf(c, SFX - 1 if j % 2 == 0 else SFX + (1 if wide else 0), y, -1 if j % 2 == 0 else 1, big=k >= 2)
    if CROWN[k] == 0:
        leaf(c, SFX - 1, SFY - h, -1)
        leaf(c, SFX, SFY - h - 1, 1)
        return c
    r = CROWN[k]
    cy = SFY - h
    tones = [VE[1], VE[2], VE[3], VE[4]]
    # The crown: three lobes, the back one darker, lit on the upper left.
    blob(c, SFX + r * 0.45, cy + r * 0.2, r * 0.6, r * 0.5, [VE[2], VE[3], VE[4], OCC], salt=k)
    blob(c, SFX, cy, r, r * 0.8, tones, salt=k + 10)
    blob(c, SFX - r * 0.4, cy - r * 0.3, r * 0.55, r * 0.45, tones, salt=k + 20)
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

    frames = [clearing(False), clearing(True)]
    for i, c in enumerate(frames):
        ok = audit("first-tree-clearing frame %d" % i, c) and ok
    img = strip(frames, CW, CH)
    img.save(os.path.join(OUT, "first-tree-clearing.png"))
    sheets.append(img)

    frames = [sapling(k) for k in range(len(STEM))]
    for i, c in enumerate(frames):
        ok = audit("first-tree-sapling frame %d" % i, c) and ok
    img = strip(frames, SW, SH)
    img.save(os.path.join(OUT, "first-tree-sapling.png"))
    sheets.append(img)
    print("  first tree   clearing 2 frames at %dx%d, sapling %d frames at %dx%d"
          % (CW * SCALE, CH * SCALE, len(STEM), SW * SCALE, SH * SCALE))


    # The preview: each sheet on the forest's turf (lichen shade), stacked.
    pad = 10
    pw = max(s.size[0] for s in sheets) + pad * 2
    ph = sum(s.size[1] for s in sheets) + pad * (len(sheets) + 1)
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(LI[3]))
    y = pad
    for s in sheets:
        sheet.alpha_composite(s, (pad, y))
        y += s.size[1] + pad
    sheet.save(PREVIEW)
    print("preview: tools/art/_secret_forest_preview.png")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
