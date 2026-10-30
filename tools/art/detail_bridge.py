# -*- coding: utf-8 -*-
"""The Last Bridge's ground detail (.ai/endgame-castle.md 3.7). Drawn by tools/art/ground_detail.py, which
finds this module by its name and appends DETAIL_SETS to its own - run ground_detail.py, never this.
GroundDetail.cs's Bridge block is what asks for these sets, and where.

What lies on a bridge a garrison has fought over, beside the shared cracks, rubble and pebbles:

  scorch        a burnt patch on the setts: soot dithered out from a black heart, a few grey flecks of ash.
                Where the pitch was fired. No ember in it - nothing on the ground is emissive.
  bolts         crossbow bolts that missed: a short dark shaft, an iron head, a stub of fletching, lying
                at angles, two or three together. Iron in steel's deep half, wood in gold's.
  pitch         a stain of spilled pitch: an irregular black pool with one lit pixel where the key catches
                its surface, and a drip run off it.

The ground_detail.py rules hold: material tones only, nothing emissive, nothing round, bright and alone.
None of it is bones: the bridge's dead are names on the parapet, never bodies (3.9).
"""
import math

import ground_detail
from ground_detail import GO, LN, OCC, ST, STL, spot


def scorch(c, r):
    """Soot on stone: a black heart dithered out to nothing, ash flecks on it."""
    cx, cy = spot(r, 14)
    rx, ry = r.i(7, 11), r.i(5, 8)
    for y in range(cy - ry, cy + ry + 1):
        for x in range(cx - rx, cx + rx + 1):
            d = ((x - cx) / float(rx)) ** 2 + ((y - cy) / float(ry)) ** 2
            if d > 1.0:
                continue
            # Ordered, thinning outward: the burn fades into the setts rather than ending at a ring.
            b = ((x * 5 + y * 3) % 8) / 8.0
            if d < 0.25:
                c.set(x, y, OCC)
            elif d < 0.6 and b < 0.7:
                c.set(x, y, ST[4])
            elif b < 0.35 * (1.0 - d) / 0.4:
                c.set(x, y, ST[4])
    for k in range(r.i(3, 6)):
        c.set(cx + r.i(-rx + 2, rx - 2), cy + r.i(-ry + 2, ry - 2), LN[3] if k % 2 else ST[2])


def bolt(c, x, y, ang, r):
    """One bolt lying on the deck: a dark wooden shaft, an iron head, a stub of fletching."""
    dx, dy = math.cos(ang), math.sin(ang)
    length = r.i(8, 11)
    for j in range(length):
        px, py = int(round(x + dx * j)), int(round(y + dy * j))
        if j < 2:
            c.set(px, py, STL[3] if j == 0 else STL[4])        # the head
        elif j > length - 3:
            c.set(px, py, LN[3])                                 # fletching
        else:
            c.set(px, py, GO[4] if j % 3 else GO[3])             # the shaft
        if j > 1 and j % 2 == 0:
            c.set(px + 1, py + 1, OCC)                           # its shadow, away from the key


def bolts(c, r):
    cx, cy = spot(r, 13)
    base = r.f() * math.pi * 2.0
    for k in range(r.i(2, 3)):
        bolt(c, cx + r.i(-6, 6), cy + r.i(-5, 5), base + (r.f() - 0.5) * 1.4, r)


def pitch(c, r):
    """A stain of pitch: an irregular black pool, lit once where the key catches it, with a drip run."""
    cx, cy = spot(r, 13)
    for k in range(r.i(3, 5)):
        lx, ly = cx + r.i(-5, 5), cy + r.i(-3, 3)
        rad = r.i(2, 4)
        for y in range(ly - rad, ly + rad + 1):
            for x in range(lx - rad - 1, lx + rad + 2):
                if ((x - lx) / float(rad + 1)) ** 2 + ((y - ly) / float(rad)) ** 2 <= 1.0:
                    c.set(x, y, OCC)
    # The drip run, down the deck.
    x, y = cx + r.i(-3, 3), cy + 3
    for j in range(r.i(3, 6)):
        c.set(x + (j // 3), y + j, OCC)
    c.set(cx - 2, cy - 2, ST[2])
    c.set(cx - 1, cy - 2, ST[3])


DETAIL_SETS = [
    ("scorch", scorch, 3),
    ("bolts", bolts, 4),
    ("pitch", pitch, 3),
]
