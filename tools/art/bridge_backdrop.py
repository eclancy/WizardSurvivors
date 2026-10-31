# -*- coding: utf-8 -*-
"""His castle on its rock, seen across the water from the Last Bridge (.ai/endgame-castle.md 3.4 and 3.7).
Run: python tools/art/bridge_backdrop.py

Writes assets/bonelight/world/bridge/castle_far.png, castle_mid.png and castle_near.png: the same castle
at three heights, authored 1:1 and saved at x2. BridgeBackdrop (scripts/BridgeBackdrop.cs) shows one of
them over the drop past the top-left parapet - never over the deck - swapping to the next as the player
passes a third and two thirds of the bridge, and raising it out of the mist a whole pixel at a time. It
never scales: a nearer castle is a bigger drawing, not a stretched one (.ai/art-direction.md).

WHAT IT IS. A rock standing in the sea, the castle's curtain wall on it, the Court's roofs behind the
wall, and one tower over all of it - the Throne's. Three things say which is nearer: the drawing is taller,
its stone a stop lighter (the far one is barely lifted off the black of the drop, the mist between), and
it carries more of his light in its windows - the arcane row, a pixel or two each, cold, never warm.
The foot of every drawing dissolves into the mist in an ordered dither, so where it stands is never a line.

It is a shape in the distance and must never compete with the fight: the darkest third of stone's row,
nothing above its lit stop, and the lit stop only on the near drawing's edges toward the key.
"""
from __future__ import division, print_function

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "bridge")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


ST = ramp("stone")
AR = ramp("arcane")


def h01(a, b, salt):
    v = (a * 73856093) ^ (b * 19349663) ^ (salt * 83492791)
    v &= 0x7FFFFFFF
    v = ((v ^ (v >> 13)) * 1274126177) & 0x7FFFFFFF
    return ((v >> 7) % 10007) / 10007.0


# The castle in units of its own width (0..1 across, 0..1 up from the waterline), drawn into whatever
# size each height is. (left, right, top) for each mass; the towers carry a crenellated top.
ROCK = [(0.0, 0.10), (0.06, 0.22), (0.16, 0.30), (0.30, 0.34), (0.50, 0.36), (0.70, 0.32), (0.84, 0.26),
        (0.94, 0.14), (1.0, 0.04)]
WALL = (0.10, 0.90, 0.50)
ROOFS = [(0.20, 0.38, 0.62), (0.42, 0.56, 0.66), (0.62, 0.80, 0.60)]
TOWERS = [(0.08, 0.17, 0.62), (0.30, 0.37, 0.70), (0.80, 0.90, 0.66), (0.57, 0.68, 1.0)]


def clear(c, x, y):
    if 0 <= x < c.w and 0 <= y < c.h:
        c.px[x, y] = (0, 0, 0, 0)


def castle(w, h, tones, windows, seed):
    """tones: (body, lit edge, dark side). windows: how many of his lights burn in it."""
    body, lit, dark = tones
    c = raster.Canvas(w, h, None)

    def X(f):
        return int(round(f * (w - 1)))

    def Y(f):
        return int(round((h - 1) * (1.0 - f)))

    # The rock: a ridge across the foot, from the sea.
    for x in range(w):
        f = x / float(w - 1)
        top = 0.0
        for i in range(len(ROCK) - 1):
            (x0, y0), (x1, y1) = ROCK[i], ROCK[i + 1]
            if x0 <= f <= x1:
                top = y0 + (y1 - y0) * (f - x0) / max(1e-6, x1 - x0)
        top += (h01(x, 1, seed) - 0.5) * 0.03
        for y in range(Y(top), h):
            c.set(x, y, dark if f > 0.55 else body)
        c.set(x, Y(top), lit if f < 0.45 else body)

    def mass(l, r, t, crenel):
        x0, x1, y0 = X(l), X(r), Y(t)
        for x in range(x0, x1 + 1):
            for y in range(y0, Y(0.28)):
                c.set(x, y, lit if x == x0 else (dark if x >= x1 - 1 else body))
        if crenel:
            for x in range(x0, x1 + 1):
                if (x - x0) % 3 != 2:
                    c.set(x, y0 - 1, lit if x == x0 else body)

    # The Court's roofs behind the wall: steep, one lit slope and one dark.
    for l, r, t in ROOFS:
        x0, x1, ytop = X(l), X(r), Y(t)
        mid = (x0 + x1) / 2.0
        for x in range(x0, x1 + 1):
            peak = ytop + int(abs(x - mid) * 1.6)
            for y in range(peak, Y(WALL[2]) + 1):
                c.set(x, y, lit if (x < mid and y == peak) else (body if x < mid else dark))
    mass(WALL[0], WALL[1], WALL[2], True)
    for l, r, t in TOWERS:
        mass(l, r, t, True)
    # The Throne's tower: the tallest, its spire a dark needle.
    l, r, t = TOWERS[-1]
    sx = (X(l) + X(r)) // 2
    for k in range(int(h * 0.12)):
        c.set(sx, Y(t) - 2 - k, dark if k % 3 else body)

    # His light: a pixel or two in the windows, arcane's base round a lit core on the nearest.
    spots = []
    for l, r, t in TOWERS + [WALL]:
        for k in range(3):
            spots.append((l + (r - l) * (0.3 + 0.4 * h01(k, int(t * 100), seed + 7)), 0.3 + (t - 0.3) * (0.35 + 0.4 * h01(k, int(l * 100), seed + 9))))
    for i in range(min(windows, len(spots))):
        j = int(h01(i, 3, seed + 11) * len(spots))
        fx, fy = spots[j]
        x, y = X(fx), Y(fy)
        if c.get(x, y)[3] == 0:
            continue
        c.set(x, y, AR[2] if windows < 6 else AR[1])
        if windows >= 6 and i % 3 == 0:
            c.set(x, y + 1, AR[2])

    # The mist takes the foot: an ordered dither that thins the bottom fifth to nothing at the water.
    mist = int(h * 0.22)
    for y in range(h - mist, h):
        k = (y - (h - mist)) / float(mist)
        for x in range(w):
            if raster.BAYER8[y % 8][x % 8] / 64.0 < k * 1.1:
                clear(c, x, y)
    return c


HEIGHTS = [
    # name, art size, (body, lit, dark), windows
    ("castle_far", (96, 52), (ST[4], ST[3], ST[4]), 3),
    ("castle_mid", (112, 70), (ST[3], ST[2], ST[4]), 5),
    ("castle_near", (128, 92), (ST[3], ST[1], ST[4]), 8),
]


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True
    for i, (name, (w, h), tones, windows) in enumerate(HEIGHTS):
        c = castle(w, h, tones, windows, 31 + i)
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:4]))
            continue
        c.scaled(SCALE).save(os.path.join(OUT, name + ".png"))
        print("  bridge/%-12s %dx%d on screen" % (name, w * SCALE, h * SCALE))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
