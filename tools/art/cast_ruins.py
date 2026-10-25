# -*- coding: utf-8 -*-
"""The Mystic Ruins' own cast (.ai/bestiary.md section 4.5). Run: python tools/art/cast_ruins.py

"The old wards were broken from the inside." The readers, statues and guardians of a school of
wards, turned on it - and the one thing he built to break them. Eleven creatures, each drawn as one
pose and derived to the contract's four animations by bestiary_kit.

THE FLOOR DECIDED THE MATERIALS. The Ruins floor is stone.shade (#141A26) flecked with stone.deep
and stone.base - measured off a run, not assumed - so the stone row is the floor's own row, and a
statue drawn in its dark half is the floor standing up. Two consequences, applied to every stone
creature here:

  Rough and laid stone (the gargoyle, the ward-cat, the obelisk, the lintel, the statue's body)
  carries its planes in stone.hi and stone.lit, keeps stone.base for the mass and spends the two
  floor tones only in crevices, where being the floor is the point.

  CARVED stone - the statue's ward-slab, the caryatid, the reliquary urn - is drawn in the DEEP HALF
  OF THE BONE ROW (bone.base / shade / deep: #9AA4B8 / #626E84 / #38404F). That is pale cut stone,
  marble and alabaster, and it is a material row the contract already has; it is not a new colour.
  It also gives the chapter two stones instead of one, which is the only way six stone creatures in
  one chapter separate by material as well as by shape (section 2, "Palette").

RED. The Censor wears red cloth, and the roster palette has no red row - every printable character
is spoken for. So this module's palette maps the five ember characters (Q 1 2 3 4, reserved to the
PLAYER and never used by anything here) onto the contract's `red` material row. They stay a
five-stop ramp in anim_sets.RAMPS, so a hurt flash and a death fade step red along red's own ramp
exactly as they step every other row. sprite_treant.PALETTE set the precedent of a module remapping
characters in its own palette.

The chapter's boss is a slab with two detached tablets, so nothing here floats a slab or a tablet:
the Folio's covers are a V of wings, and the Magister's detached thing is a rune, not a plate.
"""
from __future__ import print_function

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bestiary_kit
import bonelight as B
import roster
import sprite_bosses as SB

G = roster.G
FEET = 30

PALETTE = dict(roster.PALETTE)
PALETTE.update({
    "Q": B.tone("red", "hi"), "1": B.tone("red", "lit"), "2": B.tone("red", "base"),
    "3": B.tone("red", "shade"), "4": B.tone("red", "deep"),
})


# --- helpers ----------------------------------------------------------------------------------

def poly(g, pts, ch):
    """Fill a polygon, sampled at pixel centres."""
    ys = [p[1] for p in pts]
    n = len(pts)
    for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
        sy = y + 0.5
        xs = []
        for i in range(n):
            (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
            if (y0 <= sy < y1) or (y1 <= sy < y0):
                xs.append(x0 + (sy - y0) * (x1 - x0) / float(y1 - y0))
        xs.sort()
        for a, b in zip(xs[::2], xs[1::2]):
            x = int(round(a))
            while x + 0.5 < b:
                if x + 0.5 >= a:
                    g.set(x, y, ch)
                x += 1


def shade(g, mid, lit, dark, x0=0, y0=0, x1=None, y1=None):
    """Light a flat fill from its own outline: lit where the key (upper left) meets empty, dark where
    the far side does. Run on a mass right after it is filled, before any detail goes over it."""
    x1 = g.cell - 1 if x1 is None else x1
    y1 = g.cell - 1 if y1 is None else y1
    hits = []
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if g.get(x, y) != mid:
                continue
            if g.get(x - 1, y) in (".", "W") or g.get(x, y - 1) in (".", "W"):
                hits.append((x, y, lit))
            elif g.get(x + 1, y) == "." or g.get(x, y + 1) == ".":
                hits.append((x, y, dark))
    for x, y, ch in hits:
        g.set(x, y, ch)


def rim(g, y0, y1, xmax=None, min_run=1):
    """The 1 px key rim, on the leftmost edge of each row (contract section 2). Only the leftmost, so
    a hole inside the figure never grows a white seam. `min_run` skips rows whose leftmost run is
    thinner than that: on a quadruped the leftmost pixel of a row is often a one-pixel leg or strap,
    and a rim on it is a white fleck rather than an edge."""
    xmax = g.cell - 1 if xmax is None else xmax
    for y in range(y0, y1 + 1):
        for x in range(0, xmax + 1):
            ch = g.get(x, y)
            if ch == "W":
                break
            if ch != ".":
                run = 0
                while g.get(x + run, y) not in (".", "W") and x + run < g.cell:
                    run += 1
                if run >= min_run and x > 0 and g.get(x - 1, y) == ".":
                    g.set(x - 1, y, "W")
                break


def text(g, x0, x1, y, ink, every=2, skip=()):
    """A line of writing: short irregular runs of ink, never an even dash."""
    run = 0
    for x in range(int(x0), int(x1) + 1):
        if x in skip:
            continue
        if (x * 7 + y * 3) % 5 != 0:
            if g.get(x, y) != ".":
                g.set(x, y, ink)
            run += 1


def page(g, rows, fill, lit, dark, ink=None):
    """A hanging page from hand-written (y, x0, x1) rows: lit left edge, dark right edge, and a line
    of writing on every other row."""
    for i, (y, x0, x1) in enumerate(rows):
        g.span(y, x0, x1, fill)
        g.set(x0, y, lit)
        g.set(x1, y, dark)
        if ink and i % 2 == 1 and x1 - x0 >= 2:
            for x in range(int(x0) + 1, int(x1)):
                if (x + y) % 3:
                    g.set(x, y, ink)


def chain(g, pts, a="r", b="q", under="R"):
    """Links in alternating tones along a polyline - one tone is a wire, two are a chain."""
    k = 0
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + (1 if (x1, y1) == pts[-1] else 0)):
            t = i / float(max(1, n))
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            g.set(x, y, a if k % 2 else b)
            if under and k % 2 == 0:
                g.set(x, y + 1, under)
            k += 1


# --- the cast ---------------------------------------------------------------------------------

def unread():
    """BULK. A student of the wards, buried in paper: a short hunched violet cowl, and a hem of torn
    pages hanging off it at every angle. The pages are the read - nothing else in the chapter is
    wider at the floor than at the shoulder, and filled black the hem is a fringe, not a bell."""
    g = G(32)
    # the robe, leaning into the hunch: the head sits forward of the feet
    roster.body(g, 11, 24, 3.6, 6.2, "x", "w", "X", cx=16.0, lean=-1.4, folds=(-2.0, 1.6))
    roster.fold(g, [(13, 14), (12, 19), (12, 23)], "X", "w")

    # A page plastered to the back of the shoulder, sticking out behind: it breaks the line where
    # the swarmer's cowl is smooth.
    page(g, [(10, 9, 11), (11, 8, 12), (12, 7, 12), (13, 7, 12), (14, 8, 12), (15, 9, 11)],
         "e", "l", "E", ink="A")
    g.span(16, 9, 11, "o")

    # THE HEM: five torn pages, overlapping like shingles, each its own sheet. Back ones in shade,
    # the front one lit; every one a different length, and daylight between their torn ends.
    sheets = (
        ([(22, 6, 9), (23, 5, 9), (24, 5, 9), (25, 4, 8), (26, 5, 8), (27, 5, 7)], "E", "e", "A"),
        ([(22, 22, 25), (23, 22, 26), (24, 23, 26), (25, 23, 27), (26, 24, 27), (27, 25, 26)], "E", "e", "A"),
        ([(21, 9, 13), (22, 9, 13), (23, 9, 13), (24, 9, 13), (25, 9, 13), (26, 9, 13), (27, 10, 13),
          (28, 10, 12), (29, 11, 12)], "e", "l", "E"),
        ([(22, 18, 22), (23, 18, 22), (24, 18, 22), (25, 18, 22), (26, 19, 22), (27, 19, 22),
          (28, 20, 21)], "e", "l", "E"),
        ([(22, 13, 17), (23, 13, 17), (24, 13, 17), (25, 13, 17), (26, 13, 17), (27, 13, 17),
          (28, 14, 17), (29, 14, 17), (30, 15, 16)], "l", "L", "e"),
    )
    for rows, fill, lit, dark in sheets:
        page(g, rows, fill, lit, dark, ink="A" if fill != "l" else "E")
        y0, x0, x1 = rows[0]
        g.span(y0, x0, x1, "o")                 # each sheet's top edge sits under the robe
    for (x, y0, y1) in ((13, 25, 28), (18, 25, 27), (9, 24, 26)):
        for y in range(y0, y1 + 1):
            g.set(x, y, "o")

    # THE HOOD: pulled forward and down over a void, its peak dragging back
    for (y, x0, x1) in ((4, 13, 14), (5, 12, 16), (6, 12, 18), (7, 12, 20), (8, 12, 21),
                        (9, 12, 22), (10, 13, 22), (11, 13, 22), (12, 14, 21), (13, 15, 20)):
        g.span(y, x0, x1, "x")
        g.set(x0, y, "w")
        g.set(x1, y, "X")
    g.span(5, 12, 14, "w")
    g.set(13, 4, "v")
    roster.fold(g, [(14, 6), (14, 11)], "X", "v")
    roster.fold(g, [(16, 5), (17, 6)], "X", None)
    g.span(7, 16, 21, "X")                      # the brim's shadow
    roster.void(g, 8, 12, 2.4, 1.4, cx=19.0)
    g.set(22, 8, "w")

    # a pale hand clutching a crumpled sheet, out in front
    g.line(19, 15, 22, 17, "x")
    g.line(19, 16, 22, 18, "X")
    g.set(23, 17, "k")
    g.set(23, 18, "n")
    g.rect(24, 15, 26, 17, "l")
    g.set(25, 16, "E")
    g.set(26, 17, "e")
    g.set(24, 15, "L")

    rim(g, 4, 30)
    roster.contact(g, 5, 27)
    roster.eyes(g, 10, 17, 20, "Y", "y")
    return g.rows()


def folio():
    """FAST, and a flyer. A book flying on its own covers: the boards beat as wings in a V, the pages
    fan between them, and two points of the mark stare out of the gutter. Never a hanging slab - the
    Archivist's tablets are the one floating plane in the chapter."""
    g = G(32)
    # THE RIBBON first: a bookmark streaming back off the spine, the one thing on it that trails,
    # because a Fast creature reads as speed by what it leaves behind (the Grave Runner's rag).
    for i, (x, y) in enumerate(((14, 22), (13, 23), (12, 24), (10, 24), (9, 25), (7, 25), (6, 26), (5, 27))):
        g.set(x, y, "2" if i % 3 else "1")
        g.set(x, y + 1, "4")
    g.set(5, 27, "3")

    # THE COVERS: two thick boards, from the spine out to each wing tip, lit along the top
    for (x0, y0, x1, y1) in ((14.5, 21, 4.5, 9), (16.5, 21, 26.5, 9)):
        SB.limb(g, [(x0, y0), (x1, y1)], 2.0, "N", "p", "j")
    # the page block on each board: a wedge of leaves, their edges as lines along it
    poly(g, [(15.5, 20), (6.5, 9.5), (9.5, 8.5), (15.5, 15)], "e")
    poly(g, [(15.5, 20), (24.5, 9.5), (21.5, 8.5), (15.5, 15)], "e")
    for (x0, y0, x1, y1, ch) in ((15, 18, 7, 9, "l"), (15, 17, 9, 9, "E"), (15, 16, 8, 8, "L"),
                                 (16, 18, 24, 9, "E"), (16, 17, 22, 9, "l"), (16, 16, 23, 8, "A")):
        g.line(x0, y0, x1, y1, ch)
    # the gutter, dark, where the mark looks out
    poly(g, [(15.5, 21.5), (13.5, 16), (15.5, 13.5), (17.5, 16)], "o")
    # board edges: the lit near edge catching the key, the far edge in shade, an occ seam under
    # the pages so the leaves sit ON the boards
    g.line(5, 10, 13, 20, "P")
    g.line(26, 10, 18, 20, "J")
    g.line(7, 12, 13, 19, "j")
    g.line(24, 12, 18, 19, "J")
    # tooling on the boards, and gold corner fittings
    g.set(8, 14, "j")
    g.set(10, 17, "j")
    g.set(23, 15, "J")
    for (cx, cy, d) in ((5, 9, 1), (26, 9, -1)):
        g.set(cx, cy, "G")
        g.set(cx + d, cy, "g")
        g.set(cx, cy + 1, "u")
    # the spine and its headband
    g.span(21, 14, 17, "N")
    g.span(22, 14, 17, "j")
    g.set(14, 21, "p")
    g.set(15, 21, "P")
    # the clasp strap, hanging loose off the far board, gold catch on its end
    g.line(27, 10, 28, 13, "u")
    g.set(28, 14, "g")
    g.set(27, 15, "G")

    rim(g, 8, 23, xmax=14)
    # THE DETACHED SHADOW: a flyer touches nothing (contract section 4, Flyer)
    g.span(FEET, 10, 21, "o")
    g.span(FEET + 1, 12, 19, "o")
    roster.eyes(g, 17, 14, 17, "Y", "y")
    return g.rows()


def warded_statue():
    """HEAVY. A guardian statue holding its ward-slab before it: the slab is a flat pale plane on the
    side it faces, with a gold-inlaid ward cut into it - pigment, not light. The body behind it is
    rough stone with a crested helm; the slab is cut stone, so the shield plane is the brightest
    thing on it and reads first."""
    g = G(32)
    # LEGS, a wide stance, stone columns
    for (lx0, lx1) in ((8, 11), (13, 16)):
        g.rect(lx0, 23, lx1, FEET, "N")
        g.line(lx0, 23, lx0, FEET, "p")
        g.line(lx1, 23, lx1, FEET, "j")
        g.span(26, lx0, lx1, "j")
        g.span(FEET, lx0 - 1, lx1 + 1, "N")
        g.set(lx0 - 1, FEET, "p")
    g.rect(12, 24, 12, FEET, "o")

    # TORSO, broad, with a carved breastplate edge and one crack
    roster.mass(g, [(12, 7, 18), (17, 6, 18), (22, 7, 17), (24, 8, 17)], "N", "p", "j")
    g.span(18, 8, 17, "j")
    g.span(19, 8, 17, "p")
    roster.fold(g, [(9, 13), (12, 16), (11, 21)], "J", "P")
    g.span(23, 7, 17, "J")                      # the belt line
    g.span(22, 8, 16, "p")
    g.set(10, 22, "G")
    # the far shoulder, a rounded pauldron
    g.disc(8.5, 13.5, 2.6, "p")
    g.disc(8, 13, 1.2, "P")
    g.set(10, 15, "j")

    # HELM: crested, a T-cut face
    roster.mass(g, [(5, 10, 16), (11, 10, 16)], "N", "p", "j")
    g.span(12, 11, 16, "J")
    for (x, y) in ((9, 4), (10, 3), (11, 3), (12, 2), (13, 2), (14, 2), (15, 3)):
        g.set(x, y, "j")
        g.set(x, y + 1, "p")
    g.set(8, 5, "j")
    g.set(9, 5, "N")
    g.span(7, 11, 16, "o")                      # the eye slit
    g.rect(13, 8, 14, 10, "o")                  # the nose-to-chin slot
    g.span(6, 11, 16, "P")

    # THE WARD-SLAB: cut stone (bone row's deep half), tilted, with a carved border and the ward
    for i, y in enumerate(range(9, 30)):
        off = 1 if i < 9 else 0
        x0, x1 = 18 + off, 26 + off - (1 if i > 17 else 0)
        g.span(y, x0, x1, "n")
        g.set(x0, y, "k")
        g.set(x1, y, "m")
        if 1 < i < 19:
            g.set(x0 + 1, y, "m")
            g.set(x1 - 1, y, "k")
    g.span(9, 20, 26, "k")
    g.span(10, 20, 26, "m")
    g.span(28, 18, 25, "m")
    g.span(29, 18, 24, "J")
    # the ward: a ring of gold inlay with a stem and two crossbars, irregular
    for (x, y, ch) in ((21, 14, "g"), (22, 13, "g"), (23, 13, "u"), (24, 14, "u"), (24, 15, "u"),
                       (23, 16, "u"), (22, 16, "g"), (21, 15, "g"), (22, 17, "u"), (22, 18, "u"),
                       (22, 19, "g"), (22, 20, "u"), (22, 21, "u"), (21, 19, "u"), (23, 21, "u"),
                       (24, 21, "g"), (22, 22, "u"), (22, 23, "u")):
        g.set(x, y, ch)
    g.set(23, 14, "G")
    for (x, y) in ((20, 24), (21, 25), (21, 26), (22, 27)):  # a crack across the corner
        g.set(x, y, "o")
    g.set(25, 25, "m")
    g.set(24, 11, "S")

    # the arm gripping the slab's edge
    g.line(15, 14, 18, 16, "N")
    g.line(15, 15, 18, 17, "j")
    g.set(18, 15, "p")
    g.set(19, 15, "N")
    g.set(19, 16, "j")

    rim(g, 2, 30, xmax=17)
    roster.contact(g, 6, 26)
    roster.eyes(g, 7, 12, 15, "Y", "y")
    return g.rows()


def gargoyle():
    """WALL. A gargoyle down off its gutter, crouched broad: its wings folded high over the shoulders
    so their two points stand above its head, and horns. Weathered stone - the stone row's lit end
    on its planes, the floor's own tones only in the crevices."""
    g = G(32)
    # WINGS, folded high over the back: two spurs whose points stand above the head, the far one a
    # stop darker, the trailing edge scalloped where the membrane hangs between the fingers.
    # Wings a stop lighter than the floor's own tones: the gargoyle is the chapter's commonest wall
    # body, and drawn in stone.base it was a dark shape that only its rim found.
    poly(g, [(7, 21), (6, 12), (9, 4), (11, 11), (13, 18)], "N")
    poly(g, [(9, 20), (10, 12), (14, 2), (16, 10), (17, 17)], "p")
    for (x, y) in ((7, 20), (8, 21), (12, 18), (13, 17)):
        g.set(x, y, ".")
    g.line(9, 5, 7, 12, "p")                     # leading edges catch the key
    g.line(14, 3, 11, 11, "P")
    g.line(13, 4, 10, 13, "P")
    g.line(14, 6, 15, 14, "j")                   # the folded fingers
    g.line(9, 7, 10, 16, "j")
    g.line(12, 9, 13, 17, "N")
    g.line(15, 8, 16, 15, "N")
    g.set(9, 4, "P")
    g.set(14, 2, "P")

    # HAUNCH: the big rear mass, sitting; the TORSO rising out of it to a deep chest
    g.disc(12, 24.5, 4.8, "p")
    roster.mass(g, [(13, 15, 19), (16, 14, 21), (21, 13, 22), (25, 14, 21)], "p", "P", "N")
    shade(g, "p", "P", "N")
    g.line(11, 13, 9, 21, "o")                   # the seam between wing and back
    g.line(12, 14, 10, 22, "o")
    roster.fold(g, [(9, 23), (12, 21), (15, 22)], "J", "P")
    roster.fold(g, [(15, 26), (17, 28)], "N", None)
    for (y, x0, x1) in ((19, 18, 21), (22, 18, 21)):  # the ribbed belly, two plates only
        g.span(y, x0, x1, "N")
        g.span(y + 1, x0, x1, "j")
    g.set(18, 17, "N")

    # HIND FOOT, clawed, under the haunch; FORELEGS planted straight under the chest
    g.rect(7, 28, 14, 29, "N")
    g.span(28, 7, 13, "p")
    g.span(FEET, 7, 15, "j")
    for x in (7, 9, 11):
        g.set(x, FEET, "N")
    for (lx, lit, mid) in ((18, "P", "p"), (21, "p", "N")):
        g.rect(lx, 23, lx + 2, FEET - 1, mid)
        g.line(lx, 23, lx, FEET - 1, lit)
        g.line(lx + 2, 23, lx + 2, FEET - 1, "j")
        g.set(lx + 1, 26, "j")                   # the knee
        g.span(FEET, lx - 1, lx + 3, "j")
        g.set(lx + 3, FEET, "J")
    g.line(20, 24, 20, FEET, "o")

    # HEAD: thrust forward, a heavy brow, a wide open grimace, horns swept back
    roster.mass(g, [(10, 18, 23), (13, 17, 26), (16, 18, 26), (18, 19, 24)], "p", "P", "N")
    shade(g, "p", "P", "N", x0=16, y0=9, x1=28, y1=19)
    g.span(15, 21, 26, "o")                      # the mouth
    g.span(16, 22, 25, "J")
    for x in (22, 24, 26):
        g.set(x, 15, "k")                        # teeth
    g.set(23, 16, "n")
    g.span(12, 19, 24, "J")                      # the brow
    g.set(25, 12, "N")
    g.set(26, 13, "P")
    g.set(18, 14, "J")
    for (x, y, ch) in ((18, 9, "p"), (17, 8, "P"), (16, 7, "p"), (16, 6, "N"),
                       (21, 9, "N"), (21, 8, "p"), (20, 7, "N"), (20, 6, "j")):
        g.set(x, y, ch)                          # two horns, swept back
    g.set(19, 10, "J")

    rim(g, 2, 30)
    roster.contact(g, 6, 25)
    roster.eyes(g, 13, 20, 23, "Y", "y")
    return g.rows()


def magister():
    """RANGED. A master of the school: tall and narrow in wool, a square cap with a gold tassel, a
    stole edged in gold, and a rune of the light end of arcane floating DETACHED beside its head. A
    rune, not an orb - the Dungeon's Hexer owns the orb - and not a plate, which the Archivist owns."""
    g = G(32)
    roster.mass(g, [(12, 11, 17), (18, 10, 18), (24, 9, 19), (28, 8, 20)], "b", "c", "a")
    # folds: each a dark stop with a lit stop beside it, at uneven spacing
    roster.fold(g, [(11, 17), (10, 22), (10, 27)], "a", "c")
    roster.fold(g, [(18, 18), (18, 23), (19, 27)], "a", "c")
    roster.fold(g, [(12, 23), (13, 27)], "a", "d")
    # THE STOLE: one dark band down the front, its key-side edge gold, broken where it folds
    for y in range(13, 28):
        g.set(15, y, "a")
        g.set(16, y, "a")
        g.set(14, y, ("g", "u", "u", "g", "u")[y % 5])
    g.set(14, 14, "G")
    g.set(15, 27, "u")
    g.set(16, 27, "g")
    # the cord at the waist, knotted, one tail hanging
    g.span(20, 10, 18, "a")
    g.set(11, 20, "d")
    g.set(17, 20, "c")
    g.line(18, 21, 18, 24, "a")
    g.set(18, 24, "d")
    # the hem, uneven
    roster.tatters(g, [(28, 9, 14), (29, 10, 18), (28, 17, 20)], "a", "b", "o")
    # HIGH COLLAR, flared
    g.span(11, 10, 18, "a")
    g.span(12, 9, 19, "c")
    g.set(9, 12, "d")
    g.span(13, 10, 18, "a")
    # HOOD under the cap, and the void
    roster.mass(g, [(6, 11, 17), (11, 11, 18)], "b", "c", "a")
    roster.void(g, 7, 11, 2.2, 1.6, cx=14.8)
    # THE SQUARE CAP: flat top wider than the head, the tassel hanging off its corner
    g.span(4, 10, 19, "a")
    g.span(3, 11, 18, "b")
    g.span(5, 11, 18, "o")
    g.set(10, 4, "c")
    g.set(11, 3, "d")
    g.line(19, 4, 20, 7, "u")
    g.set(20, 8, "g")
    g.set(21, 8, "G")
    # THE RAISED ARM, a bell sleeve, a pale hand open toward the rune
    poly(g, [(16, 14), (20, 10), (22, 11), (19, 17)], "b")
    g.line(16, 14, 20, 10, "c")
    g.line(19, 16, 22, 12, "a")
    g.set(21, 10, "k")
    g.set(22, 9, "k")
    g.set(22, 10, "n")
    g.set(23, 9, "n")

    rim(g, 3, 30)
    roster.contact(g, 8, 21)
    roster.eyes(g, 9, 13, 16, "Y", "y")
    # THE RUNE, detached: a stemmed fork, light-end arcane, with transparent gap all round it.
    for (x, y, ch) in ((24, 3, "y"), (28, 3, "y"), (25, 4, "Y"), (27, 4, "Y"), (26, 5, "Z"),
                       (26, 4, "z"), (26, 6, "Y"), (26, 7, "y"), (25, 8, "z"), (27, 8, "z")):
        g.set(x, y, ch)
    return g.rows()


def obelisk():
    """TURRET, and BUILT: the thing he set inside the wards to break them. A short tapering obelisk on
    a broken plinth, two faces (lit and shadowed, with the arris catching the key), cut marks down
    the lit face, and one burning slot - the fire eye that says who made it."""
    g = G(32)
    # THE PLINTH: laid stone, two steps, courses staggered
    g.rect(7, 27, 24, FEET, "N")
    g.rect(9, 24, 22, 26, "N")
    SB.courses(g, 24, FEET, "N", "J", "P", high=3, wide=6)
    g.span(24, 9, 22, "p")
    g.span(27, 7, 24, "p")
    g.span(26, 9, 22, "j")
    g.line(24, 27, 24, FEET, "j")
    g.line(22, 24, 22, 26, "j")
    for (x, y) in ((7, 27), (8, 27), (24, 27)):    # chipped corners
        g.set(x, y, ".")
    g.set(5, FEET, "N")
    g.set(6, FEET, "p")
    g.set(26, FEET, "j")

    # THE SHAFT: two faces meeting at an arris left of centre, so the lit face is the narrow one
    for y in range(7, 24):
        t = (y - 7) / 16.0
        x0 = 12.6 - 1.8 * t
        x1 = 18.4 + 1.8 * t
        mid = x0 + (x1 - x0) * 0.42
        g.span(y, x0, mid, "p")
        g.span(y, mid + 1, x1, "N")
        g.set(x0, y, "P")
        g.set(mid, y, "P")
        g.set(x1, y, "j")
    # the pyramidion
    poly(g, [(15.5, 2.5), (12.0, 7.5), (19.0, 7.5)], "N")
    poly(g, [(15.5, 2.5), (12.0, 7.5), (15.0, 7.5)], "p")
    g.set(15, 3, "P")
    g.line(15, 3, 13, 6, "P")
    g.span(7, 12, 19, "J")
    # cut marks down the lit face, and on the shadowed one, irregular
    for (x, y) in ((12, 13), (13, 15), (12, 17), (13, 18), (12, 20), (13, 21), (17, 14), (18, 16),
                   (16, 19), (18, 20), (17, 22)):
        g.set(x, y, "J" if x < 15 else "j")
    g.line(18, 15, 17, 18, "J")
    # a crack from the slot down the shadowed face
    for (x, y) in ((17, 11), (18, 12), (17, 13), (18, 17), (19, 18), (19, 19)):
        g.set(x, y, "o")
    # THE SLOT: one burning aperture
    g.span(10, 13, 18, "o")
    g.span(11, 13, 18, "o")
    g.span(9, 13, 18, "J")
    g.set(15, 12, "o")

    rim(g, 3, 30)
    roster.contact(g, 6, 25)
    roster.eyes(g, 10, 14, 16, "+", "-")
    g.set(15, 10, "=")
    g.set(15, 11, "~")
    g.set(17, 11, "~")
    return g.rows()


def ward_cat():
    """CHARGER. A temple cat statue gone lean, coiled low to spring, tail up. Basalt - the dark end of
    stone, as such statues are cut - with a gold collar and one gold earring. The tail rising off the
    rump is the read; the crouch is the telegraph."""
    g = G(32)
    # TAIL: up off the rump, an S, its tip curling forward
    for (x, y) in ((8, 20), (7, 18), (6, 16), (6, 14), (7, 12), (8, 10), (9, 9), (10, 8), (11, 8)):
        g.disc(x, y, 1.0, "N")
    for (x, y) in ((6, 15), (6, 13), (7, 11), (8, 9), (9, 8)):
        g.set(x - 1, y, "p")
    g.set(11, 7, "p")
    g.set(12, 8, "j")

    # HAUNCH, raised and coiled; the BODY sloping down to a low chest
    g.disc(11, 22.5, 4.6, "N")
    roster.mass(g, [(19, 9, 13), (20, 10, 20), (23, 9, 23), (26, 9, 24)], "N", "p", "j")
    g.disc(20.5, 24, 3.2, "N")
    shade(g, "N", "p", "j")
    for x in range(10, 21):                       # the back line, catching the key
        col = [y for y in range(14, 26) if g.get(x, y) not in (".", "W")]
        if col:
            g.set(x, col[0], "P" if x % 3 else "p")
    roster.fold(g, [(10, 23), (13, 21), (15, 22)], "J", "p")
    roster.fold(g, [(17, 24), (19, 27)], "J", "p")
    g.set(14, 25, "j")

    # HIND LEG folded under, and the FORELEGS stretched forward along the ground
    g.rect(7, 26, 13, 29, "N")
    g.span(26, 7, 12, "p")
    g.span(29, 6, 13, "j")
    g.span(FEET, 6, 12, "J")
    g.set(6, 28, "p")
    g.rect(19, 27, 27, 29, "N")
    g.span(27, 19, 26, "p")
    g.span(29, 19, 27, "j")
    g.span(FEET, 20, 28, "J")
    g.set(27, 28, "N")
    g.set(28, 29, "j")
    g.span(28, 17, 18, "J")

    # HEAD, low and forward, ears pricked tall
    g.disc(23.5, 21, 2.9, "N")
    shade(g, "N", "p", "j", x0=19, y0=17, x1=28, y1=25)
    g.set(26, 22, "N")
    g.set(27, 22, "j")
    g.set(27, 21, "p")
    for (ex, ey) in ((21, 17), (25, 17)):
        g.set(ex, ey, "N")
        g.set(ex, ey - 1, "p")
        g.set(ex + 1, ey, "j")
    g.set(21, 15, "p")
    g.set(25, 15, "N")
    g.span(23, 22, 26, "J")                     # the jaw line
    # GOLD: a collar at the neck, one earring
    for (x, y, ch) in ((19, 20, "g"), (19, 21, "u"), (20, 22, "u"), (20, 23, "g")):
        g.set(x, y, ch)
    g.set(20, 21, "G")
    g.set(25, 18, "G")

    rim(g, 7, 30)
    roster.contact(g, 6, 28, y=FEET)
    roster.eyes(g, 20, 23, 25, "Y", "y")
    return g.rows()


def cracked_urn():
    """EXPLODER. A sealed reliquary urn on stubby legs, cracked all over. Alabaster - the palest thing
    in the chapter, because an exploder is the one creature that must be found before it arrives -
    banded in gold, with its lid knocked ajar and the mark looking out of the gap. Its stand-in faces
    left (SpriteFacesRightByDefault = false), so the gap and the stride face left too."""
    g = G(32)
    # LEGS, stubby, stepping left
    g.rect(10, 26, 12, 29, "m")
    g.set(10, 26, "n")
    g.span(FEET, 8, 12, "m")
    g.set(8, FEET, "n")
    g.rect(19, 26, 21, 29, "m")
    g.span(FEET, 18, 21, "J")
    g.set(18, FEET, "m")

    # THE SWELL
    SB.swell(g, 11, 27, [3.2, 5.8, 8.0, 8.3, 7.4, 5.4, 3.6], "n", "k", "m", cx=15.5)
    g.disc(12.5, 16, 2.0, "k")
    g.set(12, 15, "s")
    # GOLD BANDS, following the curve
    for (y, lo, hi) in ((14, 9, 22), (22, 9, 22)):
        for x in range(lo, hi + 1):
            if g.get(x, y) != ".":
                g.set(x, y, "g" if x < 15 else "u")
                g.set(x, y + 1, "o" if (x + y) % 4 == 0 else "m")
        g.set(lo + 1, y, "G")
    # HANDLES, a loop either side
    for (x, y) in ((6, 13), (5, 14), (5, 15), (5, 16), (6, 17)):
        g.set(x, y, "n")
    g.set(5, 15, "k")
    for (x, y) in ((25, 13), (26, 14), (26, 15), (26, 16), (25, 17)):
        g.set(x, y, "m")
    # CRACKS: three, long and branching, each with a lit lip on the key side. Short scattered ones
    # read as spots - a leopard, not a vessel about to burst.
    for pts in (((17, 11), (18, 13), (16, 16), (17, 18), (15, 21)),
                ((16, 16), (19, 17), (21, 20)),
                ((9, 16), (11, 18), (10, 21)),
                ((19, 24), (17, 26))):
        roster.fold(g, list(pts), "o", "k")
    for (x, y) in ((18, 17), (14, 21), (20, 19)):
        g.set(x, y, "J")

    # THE LIP and the LID knocked ajar to the left, a dark gap under it
    g.span(10, 11, 20, "n")
    g.span(11, 11, 20, "m")
    g.set(11, 10, "k")
    g.span(9, 9, 20, "o")
    # the lid: a LOW DOME with a flange, knocked ajar - a cone on top read as a hat
    for (y, x0, x1) in ((8, 8, 18), (7, 9, 17), (6, 10, 16), (5, 12, 15)):
        g.span(y, x0, x1, "n")
        g.set(x0, y, "k")
        g.set(x1, y, "m")
    g.span(8, 8, 18, "m")
    g.set(8, 8, "k")
    g.span(6, 11, 13, "k")
    g.set(11, 6, "s")
    g.span(7, 10, 16, "n")
    g.set(10, 7, "k")
    g.span(5, 13, 14, "g")                       # the knob, a gold boss
    g.set(13, 5, "G")
    g.set(14, 4, "u")
    g.set(13, 4, "g")

    rim(g, 3, 30)
    roster.contact(g, 7, 22)
    roster.eyes(g, 9, 11, 14, "Y", "y")
    return g.rows()


def lintel_bearer():
    """SLAMMER. A caryatid that held up a doorway, lifting the broken lintel to bring it down. The
    flat bar of laid stone above the head is the read - the slam is that bar coming down - and the
    figure under it is cut stone, draped, its arms raised to the bar."""
    g = G(32)
    # THE DRAPED BODY, widening to the floor, folds running its length
    roster.mass(g, [(13, 12, 19), (18, 11, 20), (24, 9, 21), (FEET, 8, 22)], "n", "k", "m")
    for (pts, lit) in ((((12, 15), (11, 22), (9, 29)), "k"), (((15, 14), (15, 22), (13, 29)), "k"),
                       (((18, 15), (18, 23), (18, 29)), "k"), (((20, 20), (21, 29)), None)):
        roster.fold(g, list(pts), "m", lit)
    roster.fold(g, [(14, 19), (17, 19)], "J", None)
    g.span(17, 12, 19, "m")                     # the girdle
    g.span(16, 12, 19, "k")
    g.set(15, 16, "g")
    g.span(FEET, 8, 22, "m")
    g.set(21, FEET, "J")
    # a foot stepping out from under the drape
    g.span(29, 20, 23, "n")
    g.span(FEET, 20, 24, "m")
    g.set(20, 29, "k")

    # ARMS raised to the bar
    for (x0, x1, lit) in ((10, 12, "k"), (19, 21, "n")):
        g.rect(x0, 8, x1, 14, "n")
        g.line(x0, 8, x0, 14, lit)
        g.line(x1, 8, x1, 14, "m")
    g.set(11, 11, "m")
    g.set(20, 11, "m")
    g.rect(13, 13, 18, 13, "m")

    # HEAD under the capital it carries, hair bound up
    roster.mass(g, [(9, 13, 18), (13, 14, 17)], "n", "k", "m")
    roster.void(g, 10, 12, 1.8, 1.2, cx=15.8)
    g.span(9, 13, 18, "m")
    g.span(8, 12, 19, "k")                      # the capital's cushion
    g.span(7, 11, 20, "n")
    g.set(11, 7, "k")

    # THE LINTEL: laid stone, thick, two blocks and a staggered joint, broken at both ends. In the
    # stone row's LIT half - drawn in the floor's own tones it read as a hat brim, not a beam.
    g.rect(5, 2, 26, 7, "p")
    g.span(2, 6, 25, "P")
    g.span(3, 5, 26, "P")
    g.set(5, 3, "p")
    g.span(6, 5, 26, "N")
    g.span(7, 5, 26, "j")
    g.line(12, 2, 12, 4, "J")                    # the joints: one through the upper course,
    g.span(5, 6, 26, "N")                        # a bed joint,
    g.line(19, 5, 19, 7, "J")                    # and one through the lower, staggered
    g.span(4, 13, 18, "p")
    for (x, y) in ((5, 2), (6, 2), (5, 7), (26, 2), (26, 3), (25, 2), (26, 7)):
        g.set(x, y, ".")
    g.set(4, 4, "p")
    g.set(4, 5, "N")
    g.set(27, 4, "N")
    g.set(27, 5, "j")
    g.set(9, 3, "N")
    g.set(16, 6, "P")
    g.set(23, 3, "N")
    g.set(24, 4, "N")

    rim(g, 2, 30)
    roster.contact(g, 7, 25)
    roster.eyes(g, 11, 14, 17, "Y", "y")
    return g.rows()


def lector():
    """SUMMONER. A reader holding open the book the folios fly out of: tall, violet, a long hood with
    its tail down the back, a gold chain of office, and the open book at hand height out in front -
    the same stone boards and fanned pages as the Folio, so the two read as one thing."""
    g = G(32)
    # Violet a stop LIGHTER than the Unread's: two violet robes in one chapter separate by value as
    # well as by height.
    roster.body(g, 9, 28, 2.8, 6.4, "w", "v", "x", cx=13.0, folds=(-1.8, 1.6))
    roster.fold(g, [(10, 18), (9, 24), (10, 28)], "X", "v")
    roster.fold(g, [(15, 21), (16, 27)], "x", "v")
    roster.tatters(g, [(28, 7, 13), (29, 8, 18), (28, 16, 20)], "x", "w", "o")
    # the hood's long tail, hanging down the back
    poly(g, [(8, 6), (10, 7), (9, 16), (7, 20), (6, 19)], "x")
    g.line(8, 7, 7, 18, "w")
    g.line(9, 8, 9, 15, "X")
    # THE HOOD, tall, peaked
    roster.mass(g, [(3, 11, 12), (5, 10, 15), (8, 9, 17), (11, 10, 17)], "w", "v", "x")
    roster.void(g, 6, 10, 2.2, 1.4, cx=14.6)
    g.span(5, 12, 16, "X")
    roster.fold(g, [(11, 5), (10, 10)], "x", "v")
    # GOLD chain of office across the shoulders, a medallion
    for (x, y, ch) in ((9, 12, "u"), (10, 13, "g"), (11, 13, "u"), (12, 14, "g"), (13, 14, "u"),
                       (14, 13, "g"), (15, 13, "u")):
        g.set(x, y, ch)
    g.set(12, 15, "G")
    g.set(12, 16, "u")

    # ARMS forward under the book
    g.line(14, 15, 18, 19, "w")
    g.line(14, 16, 18, 20, "x")
    g.line(11, 17, 19, 21, "w")
    g.line(11, 18, 19, 22, "x")
    # THE BOOK, open, held flat: two leaves on two boards, the same stone and paper as the Folio
    poly(g, [(16.5, 18.5), (21.5, 14.5), (21.5, 19.5)], "e")
    poly(g, [(22.5, 19.5), (22.5, 14.5), (28.5, 16.0), (28.5, 18.5)], "e")
    g.line(17, 18, 21, 15, "L")
    g.line(23, 15, 28, 16, "L")
    for (x0, y0, x1, y1, ch) in ((18, 18, 21, 16, "E"), (19, 19, 21, 18, "l"), (23, 17, 27, 17, "E"),
                                 (23, 18, 27, 18, "l")):
        g.line(x0, y0, x1, y1, ch)
    g.line(22, 15, 22, 19, "o")                  # the gutter
    g.line(16, 19, 22, 20, "N")                  # the boards under the leaves
    g.line(22, 20, 29, 19, "j")
    g.set(16, 19, "p")
    g.set(29, 18, "g")
    g.set(29, 17, "u")
    # pale hands under the boards
    g.set(18, 21, "k")
    g.set(24, 21, "k")
    g.set(25, 21, "n")
    # a loose leaf lifting off the open page
    g.set(25, 12, "l")
    g.set(26, 12, "e")
    g.set(26, 11, "L")

    rim(g, 3, 30)
    roster.contact(g, 8, 22)
    roster.eyes(g, 8, 14, 17, "Y", "y")
    return g.rows()


def censor():
    """MINIBOSS, on the 48 cell. His jailer among the books: a tall red-robed figure in an iron-masked
    hood, a padlocked book chained across its chest, and a great quill carried raked like a spear, its
    vane standing above the head. The chained book is what it guards; the quill is what it strikes
    out with. Nothing like the Warden's door and chains, and nothing floating."""
    g = G(48)
    F = 46
    # THE QUILL, behind everything: a long raked shaft, its vane high, a steel nib at the floor
    SB.limb(g, [(36, 44), (42, 6)], 0.6, "k", "s", "n")
    g.line(36, 45, 35, 46, "t")                  # the nib, planted
    g.set(36, 44, "T")
    g.set(36, 45, "R")
    for i in range(18):
        y = 5 + i
        x = 42 - (i * 6) / 37.0 * 6.0
        w = 3.2 * (1.0 - abs(i - 8) / 11.0)
        g.span(y, x - w - 0.5, x - 0.6, "e")
        g.span(y, x + 0.6, x + w * 0.6 + 0.5, "E")
        if i % 3 == 1:
            g.set(x - w, y, "o")                 # the vane splits, barb from barb
        else:
            g.set(x - w - 0.5, y, "l")
    g.set(42, 4, "e")
    g.set(41, 3, "l")

    # THE ROBE: red, broad at the hem, a waist
    roster.mass(g, [(16, 15, 31), (26, 15, 31), (34, 13, 33), (F, 11, 35)], "2", "1", "3")
    for (pts, lit) in ((((16, 27), (14, 36), (13, 45)), "1"), (((20, 33), (19, 40), (18, 45)), "1"),
                       (((27, 33), (28, 39), (29, 45)), "1"), (((31, 31), (33, 45)), None)):
        roster.fold(g, list(pts), "4", lit)
    roster.fold(g, [(23, 36), (23, 45)], "3", "2")
    # a steel-shod hem
    g.span(F, 11, 35, "q")
    g.span(F - 1, 11, 35, "R")
    for x in (14, 20, 27, 33):
        g.set(x, F - 1, "r")
    g.set(11, F - 1, "r")

    # THE MANTLE: two riveted steel pauldrons, rounded, with a gorget between them - shaped plates,
    # not a band across the shoulders.
    for (cx, cy, w, lit) in ((15.5, 18.5, 4.6, "r"), (31.0, 18.5, 4.2, "R")):
        for y in range(int(cy - 4), int(cy + 4)):
            t = (y - (cy - 4)) / 8.0
            hw = w * (1.0 - (t - 0.55) ** 2 * 1.6)
            if hw < 0.6:
                continue
            g.span(y, cx - hw, cx + hw, "R")
            g.set(cx - hw, y, lit)
            g.set(cx + hw, y, "q")
        g.span(int(cy + 3), cx - w + 1, cx + w - 1, "q")
        g.span(int(cy + 4), cx - w + 1, cx + w - 1, "o")
    SB.ridges(g, 15, 21, "R", "q", "r", step=3, jitter=5)
    g.rect(19, 15, 27, 18, "q")                  # the gorget
    g.span(15, 19, 27, "R")
    g.set(19, 16, "r")
    for (rx, ry) in ((13, 17), (17, 16), (29, 17), (33, 18)):
        g.set(rx, ry, "t")
        g.set(rx, ry + 1, "q")

    # SLEEVES: the far arm hanging, the near arm out to the quill
    roster.mass(g, [(22, 11, 15), (32, 10, 15), (34, 10, 14)], "3", "2", "4")
    g.rect(10, 34, 14, 36, "R")
    g.span(34, 10, 14, "r")
    g.span(37, 11, 13, "q")
    roster.mass(g, [(22, 31, 35), (27, 32, 37), (29, 33, 37)], "3", "2", "4")
    g.rect(33, 29, 38, 31, "R")                  # the gauntlet on the shaft
    g.span(29, 33, 38, "r")
    g.set(33, 30, "t")
    g.span(32, 33, 38, "o")

    # THE CHAINED BOOK: stone boards, a page-edge, a gold lock, and the chain round the body
    g.rect(17, 23, 29, 33, "N")
    g.line(17, 23, 29, 23, "p")
    g.line(17, 23, 17, 33, "p")
    g.line(29, 24, 29, 33, "j")
    g.line(17, 33, 29, 33, "J")
    for y in range(24, 33):
        g.set(30, y, "e" if y % 2 else "E")      # the page-edge, the book's thickness
    g.set(30, 33, "A")
    SB.ridges(g, 25, 32, "N", "j", "p", step=4, jitter=7)
    g.rect(19, 25, 27, 31, "j")                  # the tooled panel
    g.rect(20, 26, 26, 30, "N")
    for (x, y) in ((17, 23), (29, 23), (17, 33), (29, 33)):
        g.set(x, y, "g")
    chain(g, [(13, 16), (17, 25)])
    chain(g, [(33, 17), (29, 26)])
    chain(g, [(16, 28), (31, 28)], a="t", b="R", under="q")
    # the lock
    g.rect(22, 27, 25, 31, "g")
    g.line(22, 27, 25, 27, "G")
    g.line(25, 28, 25, 31, "u")
    g.set(23, 29, "o")
    g.set(23, 30, "u")
    g.line(22, 26, 22, 25, "u")
    g.line(25, 26, 25, 25, "u")
    g.span(24, 23, 24, "g")

    # THE HOOD, tall and peaked, and the iron mask under it
    roster.mass(g, [(4, 22, 23), (7, 20, 26), (12, 18, 29), (16, 17, 30)], "3", "2", "4")
    roster.fold(g, [(22, 5), (20, 10), (19, 15)], "4", "1")
    g.rect(21, 9, 28, 15, "R")
    g.line(21, 9, 28, 9, "r")
    g.line(21, 9, 21, 15, "r")
    g.line(28, 10, 28, 15, "q")
    g.span(12, 21, 28, "o")                      # the slit
    g.span(13, 22, 27, "q")
    g.line(24, 14, 24, 15, "q")
    g.set(22, 10, "t")
    g.set(27, 10, "t")

    rim(g, 3, F, xmax=33)
    roster.contact(g, 10, 37, y=F)
    roster.eyes(g, 12, 23, 26, "Y", "y")
    return g.rows()


CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    ("ruins-unread", unread, 32, (0.8, 2.4, 2), "RuinsUnreadFrames"),
    ("ruins-folio", folio, 32, (1.2, 2.6, 2), "RuinsFolioFrames"),
    ("ruins-warded-statue", warded_statue, 32, (0.4, 2.8, 1), "RuinsWardedStatueFrames"),
    ("ruins-gargoyle", gargoyle, 32, (0.6, 3.4, 1), "RuinsGargoyleFrames"),
    ("ruins-magister", magister, 32, (0.9, 2.2, 3), "RuinsMagisterFrames"),
    ("ruins-obelisk", obelisk, 32, (0.1, 2.0, 1), "RuinsObeliskFrames"),
    ("ruins-ward-cat", ward_cat, 32, (1.6, 4.2, 2), "RuinsWardCatFrames"),
    ("ruins-cracked-urn", cracked_urn, 32, (1.0, 2.0, 2), "RuinsCrackedUrnFrames"),
    ("ruins-lintel-bearer", lintel_bearer, 32, (0.5, 3.0, 1), "RuinsLintelBearerFrames"),
    ("ruins-lector", lector, 32, (0.9, 2.4, 2), "RuinsLectorFrames"),
    ("ruins-censor", censor, 48, (0.5, 4.0, 1), "RuinsCensorFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


def main(cast, palette, chapter):
    """bestiary_kit.run, split by cell. The kit's report() lays every pose on ONE preview sheet at
    the first creature's cell size, so a cast with its 48-cell miniboss in it stops with "row wider
    than cell". Reporting the two cell sizes as two sheets is the same report; the writing and the
    anchor check are the kit's own, unchanged."""
    for cell, suffix in ((32, ""), (48, "-48")):
        part = [c for c in cast if c[2] == cell]
        if part:
            bestiary_kit.report(part, palette, chapter + suffix)
    made, problems = bestiary_kit.build_cast(cast, palette)
    for stem, n, sheet in made:
        print("  %-24s %3d frames -> scenes/resources/%s.tres" % (stem, n, sheet))
    if problems:
        print("")
        print("ANCHOR PROBLEMS (contract section 5):")
        for p in problems:
            print("   " + p)
        sys.exit(1)
    print("all frames anchored correctly")


if __name__ == "__main__":
    main(CAST, PALETTE, "ruins")
