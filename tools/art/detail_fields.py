# -*- coding: utf-8 -*-
"""The Fallow Fields' ground detail (.ai/campaign.md section 5). Not run on its own: ground_detail.py
finds this module (ground_detail.chapter_detail_modules) and appends its DETAIL_SETS to its own,
so a full `python tools/art/ground_detail.py` draws them and lists them in detail.json, and
GroundDetail.cs's Fields rules ask for them by name.

Four small things a harvested field has lying in it, each a THING at the size ground_detail.py
settled on ("SIZE, TWICE") and none of them emissive:

  stubble_tufts   a clump of stalks the reaper missed, standing taller than the cut stubble
  thistle         a thistle gone over: a rosette of spined grey leaves and two or three purple heads
  poppy           field poppies, three to five cups on thin stems in their own leaves
  chaff           husks and broken straw lying flat where the threshing blew it

THE POPPIES ARE THE RED ROW, not ember. Ember is player pigment and the colour of fire spells; a
poppy that bright would be the brightest thing on the floor. `red` is old cloth's maroon - a
poppy under no light - and its brightest stop is the petal's lit edge only. No head is a lone
disc: a poppy is a cup with a dark heart, in a clump.
"""
from __future__ import division

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import ground_detail as GDT

OCC = GDT.OCC
GO = GDT.GO
LN = GDT.LN
LI = GDT.LI
VE = GDT.VE
VI = GDT.VI
RD = GDT.RD
FL = GDT.FL
spot = GDT.spot
stem = GDT.stem
leaf_on = GDT.leaf_on


def stubble_tufts(c, r):
    """Stalks the reaper missed: a few uncut straws standing together, two with their ears still on,
    leaning as straw does - never quite upright, never all one way."""
    cx, cy = spot(r, 12)
    for k in range(r.i(5, 7)):
        x = cx + r.i(-5, 5)
        h = r.i(6, 11)
        lean = r.f() * 0.6 - 0.3
        for j in range(h):
            xx = int(round(x + lean * j))
            c.set(xx, cy - j, GO[3] if j < h - 2 else GO[2])
        top = (int(round(x + lean * (h - 1))), cy - h + 1)
        if k % 3 == 0:
            # an ear: three pixels, lit on its key side
            c.set(top[0], top[1] - 1, GO[2])
            c.set(top[0], top[1] - 2, GO[1])
            c.set(top[0] + 1, top[1] - 1, GO[3])
        c.set(x + 1, cy + 1, OCC)
    for dx in range(-4, 5, 2):
        c.set(cx + dx, cy + 1, GO[4])


def thistle(c, r):
    """A thistle gone over: spined leaves flat in a rosette, a stem or two, purple heads on top of
    a spiny green cup - the head is violet's lit and base, never its hi."""
    cx, cy = spot(r, 13)
    # the rosette: spined leaves splayed flat, grey-green
    for (dx, dy, side) in ((-5, 2, -1), (4, 2, 1), (-3, 4, -1), (3, 4, 1), (0, 5, 1)):
        for k in range(4):
            x = cx + dx + side * k
            y = cy + dy + (k // 2)
            c.set(x, y, LN[2] if k < 3 else LN[1])
            if k % 2 == 1:
                c.set(x, y - 1, LN[1])                  # a spine
            c.set(x, y + 1, LN[4])
    for (dx, h) in ((-2, r.i(8, 10)), (2, r.i(6, 8)), (0, r.i(10, 12)))[:r.i(2, 3)]:
        x = cx + dx
        stem(c, x, cy + 1, h, LI[3])
        leaf_on(c, x, cy - h // 2, 1 if dx >= 0 else -1, 2)
        top = cy + 1 - h
        # the spiny cup, then the purple head
        c.set(x - 1, top + 1, LI[2])
        c.set(x, top + 1, LI[3])
        c.set(x + 1, top + 1, LI[4])
        c.set(x - 1, top, VI[1])
        c.set(x, top, VI[0])
        c.set(x + 1, top, VI[2])
        c.set(x, top - 1, VI[1])
        c.set(x - 1, top - 1, VI[2])
    c.set(cx, cy + 6, OCC)


def poppy(c, r):
    """Field poppies: three to five cups on thin bending stems, each a dark heart in red petals lit
    on the key side, standing in a few lobed leaves."""
    cx, cy = spot(r, 14)
    for k in range(r.i(4, 6)):
        dx = r.i(-6, 6)
        c.set(cx + dx, cy + 6 + r.i(-1, 1), VE[3])
        c.set(cx + dx + 1, cy + 6, VE[4])
    for k in range(r.i(3, 5)):
        x = cx + r.i(-7, 7)
        y = cy + r.i(-5, 2)
        h = r.i(4, 7)
        for j in range(h):
            c.set(x + (1 if j > h // 2 and k % 2 else 0), y + j, VE[3])
        # the cup: a bowl of four petals five across, its rim lit upper-left, a dark heart. Three
        # across photographed as a red speck.
        for (dx, dy, t) in ((-2, -1, 0), (-1, -2, 0), (0, -2, 0), (1, -2, 1), (2, -1, 1),
                            (-2, 0, 1), (-1, -1, 1), (0, -1, 1), (1, -1, 2), (2, 0, 2),
                            (-1, 0, 2), (0, 0, 4), (1, 0, 2), (-1, 1, 3), (0, 1, 3), (1, 1, 3)):
            c.set(x + dx, y + dy, OCC if t == 4 else RD[t])
    c.set(cx, cy + 8, OCC)


def chaff(c, r):
    """Husks and broken straw lying flat where the threshing blew it: short strokes at every angle,
    in gold's shade with a lit straw here and there, and husks as single base-stop pixels."""
    cx, cy = spot(r, 12)
    for k in range(r.i(7, 10)):
        x = cx + r.i(-9, 9)
        y = cy + r.i(-7, 7)
        dx, dy = ((1, 0), (1, 1), (0, 1), (1, -1))[r.i(0, 3)]
        n = r.i(2, 4)
        for j in range(n):
            c.set(x + dx * j, y + dy * j, GO[2] if (j == 0 and k % 3 == 0) else GO[3])
        c.set(x + dx * (n - 1), y + dy * (n - 1) + 1, GO[4])
    for k in range(r.i(4, 6)):
        c.set(cx + r.i(-8, 8), cy + r.i(-6, 6), GO[2])


# set name -> (draw, variants), in ground_detail.SETS' shape.
DETAIL_SETS = [
    ("stubble_tufts", stubble_tufts, 4),
    ("thistle", thistle, 3),
    ("poppy", poppy, 3),
    ("chaff", chaff, 3),
]
