# -*- coding: utf-8 -*-
"""The Broken Circle's ground detail (.ai/campaign.md section 6). Drawn by tools/art/ground_detail.py,
which finds this module by its name and appends DETAIL_SETS to its own - run ground_detail.py, never
this. GroundDetail.cs's Heath block is what asks for these sets, and where.

Three things lie on a high heath, and they are what make the violet floor read as heather rather
than as stone:

  heather_tufts   the heather itself: low wiry bushes, dark green under a crown of violet bloom,
                  three or four to a clump, laid in drifts by GroundDetail's clustering.
  cotton_grass    the bog's flag: a tuft of thin blades with white cotton heads nodding on stalks.
                  It grows where the ground is wet - on the peat and at the tarns' edges.
  lichen_stones   loose granite, speckled, with a crust of lichen on the lit side.

The ground_detail.py rules hold: material tones only, nothing emissive, the brightest stop a `lit`
and never a `hi` plane, and nothing round, bright and alone - the XP orbs are lime, lilac and pale
yellow discs, so the heather's bloom is violet's dull material row in spikes, never a lilac dot, and
the cotton heads come several to a stalk-tuft, never one.
"""
import math

import ground_detail
from ground_detail import LI, LN, OCC, SK, ST, VE, VI, spot


def heather_bush(c, x, y, w, r):
    """One heather bush: a low rounded mound of wiry sprigs, mostly bloom on top. Sprigs, not a
    dome - every column is its own height, which is what heather looks like from a few yards and
    what keeps it from reading as moss. The bloom is most of what shows: the first pass, a green
    mound with a violet rim, read as grass, so the green is now only the dark of the stems under the
    flowers, and the flowers are violet's lit stop, with a fleck of its hi where the key catches the
    top and its base and shade on the far side: on a floor of violet's shade, bloom in violet's base
    was the floor again (photographed in the game, 2026-10-25). Its hi is a fleck, never a plane, and
    nowhere near the lilac of an XP orb."""
    hmax = 4.0 + w * 0.45
    for dx in range(-w, w + 1):
        t = dx / float(w + 0.5)
        h = int(round(hmax * math.sqrt(max(0.0, 1.0 - t * t)) + r.i(-1, 1)))
        if h < 1:
            continue
        top = y - h
        bloom = max(1, min(h - 1, 2 + r.i(0, 1)))
        for j in range(h):
            yy = y - j
            if yy <= top + bloom:
                if t < -0.3:
                    tone = VI[0] if (yy == top + 1 and (dx + yy) % 3 == 0) else VI[1]
                elif t > 0.45:
                    tone = VI[2] if (dx + yy) % 2 else VI[3]
                else:
                    tone = VI[1] if (dx * 3 + yy) % 4 else VI[2]
            else:
                tone = VE[4] if (dx + j) % 2 else VE[3]
            c.set(x + dx, yy, tone)
        c.set(x + dx, y + 1, OCC if abs(dx) < w else VI[4])
    # A few sprigs standing proud of the mound, bloom at their tips.
    for k in range(r.i(1, 3)):
        sx = x + r.i(-w + 2, w - 2)
        top = y - int(hmax) - 1 - r.i(1, 2)
        c.set(sx, top + 1, VE[4])
        c.set(sx, top, VI[1] if sx < x else VI[2])


def heather_tufts(c, r):
    cx, cy = spot(r, 12)
    bushes = [(cx + r.i(-7, 7), cy + r.i(-4, 6), r.i(7, 11)) for _ in range(r.i(1, 2))]
    for x, y, w in sorted(bushes, key=lambda b: b[1]):
        heather_bush(c, x, y, w, r)


def cotton_grass(c, r):
    """Cotton-grass: thin blades from one root and four to six white heads on taller stalks, each
    head a 2x2 tuft lit on its upper left, nodding a little to one side."""
    cx, cy = spot(r, 12)
    for k in range(r.i(1, 2)):
        x0, y0 = cx + r.i(-6, 6), cy + r.i(-3, 5)
        # the blades
        for side in (-1, 0, 1):
            for j in range(6):
                c.set(x0 + side * (j // 3), y0 - j, LN[2] if j < 4 else LN[1])
        c.set(x0 - 1, y0 + 1, OCC)
        c.set(x0, y0 + 1, OCC)
        c.set(x0 + 1, y0 + 1, OCC)
        # the stalks and their heads
        for s in range(r.i(4, 6)):
            sx = x0 + r.i(-4, 4)
            h = r.i(8, 13)
            lean = 1 if sx > x0 else (-1 if sx < x0 else 0)
            for j in range(h):
                c.set(sx + (lean if j > h * 0.6 else 0), y0 - j, LN[3] if j % 3 else LN[2])
            hx, hy = sx + lean, y0 - h
            c.set(hx, hy - 1, SK[1])
            c.set(hx + 1, hy - 1, SK[2])
            c.set(hx, hy, SK[2])
            c.set(hx + 1, hy, SK[3])
            if r.f() < 0.4:
                c.set(hx - 1, hy, SK[3])


def lichen_stones(c, r):
    """Two or three granite stones lying in the heather: speckled, lit upper-left, on a contact
    shadow, with a crust of lichen on the lit side."""
    cx, cy = spot(r, 12)
    for k in range(r.i(2, 3)):
        x, y = cx + r.i(-8, 8), cy + r.i(-6, 6)
        w = r.i(4, 6) if k == 0 else r.i(2, 4)
        hgt = 4 if k == 0 else 3
        for dx in range(-w, w + 1):
            c.set(x + dx, y + 1, OCC)
        for dy in range(-hgt, 1):
            for dx in range(-w, w + 1):
                if abs(dx) == w and dy in (-hgt, 0):
                    continue
                tone = ST[1]
                if dy == -hgt or dx == -w:
                    tone = ST[0]
                elif dy == 0 or dx == w:
                    tone = ST[2]
                elif r.f() < 0.22:
                    tone = SK[4]                          # feldspar
                c.set(x + dx, y + dy, tone)
        # the lichen crust, on the lit top-left
        for (dx, dy, t) in ((-w + 1, -hgt, 2), (-w + 2, -hgt, 1), (-w + 1, -hgt + 1, 3), (-w + 3, -hgt, 3)):
            if dx <= w - 1:
                c.set(x + dx, y + dy, LI[t])


DETAIL_SETS = [
    ("heather_tufts", heather_tufts, 4),
    ("cotton_grass", cotton_grass, 4),
    ("lichen_stones", lichen_stones, 3),
]
