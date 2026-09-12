# -*- coding: utf-8 -*-
"""The Warden: the recurring miniboss, on the 48x48 elite cell.

Run: python tools/art/sprite_warden.py     -> tools/art/_warden/ (gitignored)

Replaces SoldierEnemy, which was a third-party pack sheet and - more to the point - was wrong
for the fiction. The comment in Node2DGame said the Soldier read instantly because it was "the
only living, armoured humanoid among a roster of skeletons, an orc and a ghost". That is a
silhouette argument built on an art-pack accident, and `.ai/world-and-tone.md` says the opposite
in so many words: the horde serves one will, it was made or taken by one hand, and his colour is
absence. A living human mercenary has no place in it.

WHAT IT IS. The premise is that the dark wizard has every other wizard locked up. Something has
to stand over a cell, and that thing is what keeps turning up to stop you - so the run's
recurring miniboss is his jailer. It arrives on a clock rather than on a spawn table because a
jailer is not part of the landscape; it is sent.

That decides every choice below, and each one is also a silhouette the roster does not already
own:

  IT WEARS A CELL DOOR. A bent iron grate across the chest with darkness behind it. This is the
  density solution and the identity at the same time: vertical bars are boundaries, which is what
  section 4b is asking for, and a barred chest is a thing no other enemy has. The bars are NOT
  evenly spaced - one is snapped short and one is bent aside. An even grate is the stripe failure
  the shielder already taught us, and a door with a bar missing tells you something besides.

  IT HAS NO FACE, IT HAS A HASP. A flat bolted plate where a head should be, with one horizontal
  slit in it. Every other enemy in the roster carries a tapered void with the arcane mark inside;
  this one carries a straight edge, which reads as manufactured rather than as hollowed out. The
  mark still burns through the slit - it is still his.

  THE CHAINS ARE THE SILHOUETTE. Two of them, drooping from the wrists almost to the floor and
  ending in open shackles. Filled solid black the figure is a wide plated slab with two curves
  hanging off it, and nothing else in the cast hangs. They are also the only part of the design
  that is about the prisoners rather than about the jailer: the shackles are open because whatever
  was in them is not in them now.

  THE KEYS ARE FOUR PIXELS OF GOLD. At the belt, and that is the whole budget. The restraint is
  the point - it is the one warm-ish note on a figure drawn entirely in the deep half of steel,
  so the eye goes to it, and what it goes to is the thing that locks wizards in.

WHY 48x48 AND NOT 32. Section 1's table puts elites and large enemies on the elite cell at the
same x2 everyone gets, so a miniboss is a bigger CELL, never a bigger scale. The Soldier was a
16px sheet at scale 3 - off the grid table in both directions at once. This is a ~40px figure at
x2, which lands at 80px: a head and shoulders above the 56px basic enemies, which is what a
miniboss arriving should look like before it does anything.
"""
import math
import os

import pixel as P
import roster

CELL = 48
FEET = CELL - 2
CX = 23.5

PALETTE = dict(roster.PALETTE)
G = roster.G
body = roster.body
plate = roster.plate
eyes = roster.eyes


def _grate(g, x0, x1, y0, y1):
    """The cell door it wears, as a small ARCHED hatch rather than a wall of bars.

    The first pass covered the whole torso with an even grid and the figure came out as a filing
    cabinet - which is the shielder's radiator failure repeated on a bigger cell, and the metric
    scored it 0.81 and said nothing, exactly as it did the first time. Three changes:

      It is small and inset, so there is plate on every side of it and it reads as a thing SET
      INTO a body instead of as the body's own pattern.

      The top is an arch. A rectangle inside a rectangle is a panel; an arch is a door.

      The bars are hand-listed at uneven gaps, and two of them are damaged - one snapped short,
      one bent aside. Even spacing carries no information, and a door with a bar missing carries
      quite a lot of it.
    """
    g.rect(x0, y0 + 2, x1, y1, "o")
    for k in range(2):                                # the arch, two stepped rows
        g.span(y0 + k, x0 + 2 - k, x1 - 2 + k, "o")
    g.line(x0 - 1, y0 + 1, x0 - 1, y1 + 1, "R")       # the frame, lit left, dark right
    g.line(x1 + 1, y0 + 1, x1 + 1, y1 + 1, "q")
    g.span(y1 + 1, x0 - 1, x1 + 1, "q")
    g.span(y0, x0 + 2, x1 - 2, "r")
    for (bx, stop, bend) in ((x0 + 1, y1, 0.0), (x0 + 3, y1, 0.0),
                             (x0 + 6, y0 + 5, 0.0), (x0 + 8, y1, -0.38)):
        for i, y in enumerate(range(y0 + 1, stop + 1)):
            g.set(bx + bend * i, y, "r")
            g.set(bx + bend * i + 1, y, "R")
        if stop < y1:
            g.set(bx, stop + 1, "q")                  # the snapped end, bent over
            g.set(bx + 1, stop + 2, "q")
    for hy in (y0 + 4, y0 + 9):                       # two braces, not a ladder of them
        g.span(hy, x0, x1, "R")
        g.set(x0 + 5, hy, "q")


def _chain(g, x, y, dx, dy, sag):
    """A drooping chain. Alternating tones are what make links read at one pixel wide - a chain
    drawn in one colour is a wire, and a wire does not weigh anything."""
    n = int(max(abs(dx), abs(dy))) + 1
    pts = []
    for i in range(n + 1):
        t = i / float(n)
        pts.append((x + dx * t, y + dy * t + sag * (t - t * t) * 4.0))
    for i, (px, py) in enumerate(pts):
        g.set(px, py, "q" if i % 2 else "r")
        g.set(px, py + 1, "R" if i % 2 else "q")
    # the open shackle on the end: a broken ring, and it is open because whatever was in it is out
    ex, ey = pts[-1]
    for (ox, oy) in ((-2, 0), (-2, 1), (-1, 2), (0, 3), (1, 2), (2, 1), (2, -1), (1, -2)):
        g.set(ex + ox, ey + oy, "r")
        g.set(ex + ox, ey + oy + 1, "q")
    g.rect(ex - 1, ey, ex + 1, ey + 2, "o")


def _rivets(g, spots):
    """Bolts. Lit pixel over a dark one, which is the cheapest way to say a thing is forged."""
    for (rx, ry) in spots:
        g.set(rx, ry, "t")
        g.set(rx, ry + 1, "q")


def warden():
    g = G(CELL)

    # --- LEGS, short and set wide apart. The gap between them is half the silhouette. --------
    for (lx0, lx1, key) in ((16, 21, "r"), (26, 32, "R")):
        g.rect(lx0, 36, lx1, FEET, "R")
        g.line(lx0, 36, lx0, FEET, key)
        g.line(lx1, 36, lx1, FEET, "q")
        g.span(40, lx0, lx1, "q")
        g.span(FEET, lx0 - 1, lx1 + 1, "q")
        g.span(FEET - 1, lx0 - 1, lx1 + 1, "R")
    g.rect(22, 38, 25, FEET, "o")

    # --- THE MASS. Two tapers meeting at a WAIST: broad shoulders in, hips back out. ---------
    # A single taper from shoulder to floor is a wedge, and a wedge with a square head on it is
    # the cabinet the first pass drew. The narrowing at y=32 is what makes this a figure.
    body(g, 16, 32, 11.5, 6.5, "R", "r", "q", folds=(-5.5, 5.0))
    body(g, 32, 38, 6.5, 9.0, "q", "R", "o", rim=False)
    g.span(32, 17, 30, "q")

    # --- THE GRATE: a small arched hatch, inset, with plate all round it. --------------------
    _grate(g, 19, 28, 20, 30)

    # --- SHOULDERS, UNEVEN and ROUNDED. --------------------------------------------------
    # The first pass built the left one as a diamond with two loose diagonals off it, and it read
    # as an axe head floating beside the body. Armour on a shoulder follows the shoulder: a
    # rounded cap, with the one spike GROWING OUT OF IT rather than hovering near it.
    for i, y in enumerate(range(13, 24)):
        w = 8.2 * math.sin(math.pi * (i + 0.7) / 11.6)
        if w <= 0.6:
            continue
        g.span(y, 12.5 - w, 12.5 + w * 0.62, "r" if i < 4 else "R")
        g.set(12.5 - w, y, "t" if i < 6 else "r")
        g.set(12.5 + w * 0.62, y, "q")
    for k in range(5):                                 # the spike, rooted in the cap
        g.set(7 - k * 0.9, 15 - k * 1.1, "R")
        g.set(8 - k * 0.9, 15 - k * 1.1, "q")
    g.set(3, 10, "r")
    for i, y in enumerate(range(15, 24)):               # right cap, lower and shallower
        w = 6.4 * math.sin(math.pi * (i + 0.8) / 9.8)
        if w <= 0.6:
            continue
        g.span(y, 34.5 - w * 0.55, 34.5 + w, "R" if i < 4 else "q")
        g.set(34.5 - w * 0.55, y, "r")
        g.set(34.5 + w, y, "q")
    plate(g, 32, 24, 38, 26, "q", "R", "o")
    _rivets(g, [(9, 17), (13, 20), (33, 18), (37, 20)])

    # --- ARMS. One hangs straight, one is bent - and both leave daylight beside the body. ----
    g.rect(8, 22, 12, 34, "R")
    g.line(8, 22, 8, 34, "r")
    g.line(12, 22, 12, 34, "q")
    for by in (26, 30):
        g.span(by, 8, 12, "q")
    g.rect(7, 34, 12, 38, "R")                         # the fist
    g.line(7, 34, 12, 34, "r")
    g.span(36, 8, 11, "q")
    g.line(7, 39, 12, 39, "o")
    g.line(34, 25, 37, 31, "R")
    g.line(35, 25, 38, 31, "q")
    g.line(33, 26, 36, 32, "r")
    g.rect(34, 31, 38, 35, "R")
    g.line(34, 31, 38, 31, "r")
    g.line(34, 36, 38, 36, "o")
    g.line(13, 22, 13, 33, "o")                        # seams that separate arm from torso
    g.line(33, 24, 34, 33, "o")

    _chain(g, 9, 39, 6, 6, 1.5)
    _chain(g, 36, 36, -5, 8, 1.7)

    # --- THE HEAD: narrow, bolted, on a neck. No hollow - a hasp with a slit in it. ----------
    g.rect(21, 16, 26, 18, "q")                        # the neck, and it is visible
    g.set(21, 16, "R")
    g.rect(19, 6, 28, 15, "R")                         # the helm: narrow, and it TAPERS
    g.span(4, 21, 26, "q")
    g.span(5, 20, 27, "R")
    g.span(16, 18, 29, "q")                            # a jaw wider than the crown
    g.span(17, 19, 28, "o")
    g.line(19, 6, 19, 15, "r")
    g.line(28, 6, 28, 15, "q")
    g.line(20, 5, 27, 5, "r")
    plate(g, 20, 7, 27, 14, "q", "R", "o")              # the hasp, bolted over the face
    g.span(10, 20, 27, "o")
    g.span(11, 21, 26, "o")
    g.line(23, 13, 23, 15, "q")
    _rivets(g, [(20, 7), (26, 7), (20, 12), (26, 12)])
    g.line(22, 4, 23, 1, "q")                           # a crest, snapped off short
    g.line(23, 4, 24, 1, "R")

    # --- THE KEYS. Four pixels, at the belt, and that is the entire warm budget. -------------
    plate(g, 17, 33, 30, 35, "q", "R", "o")
    g.set(29, 36, "u")
    g.set(30, 36, "g")
    g.set(30, 37, "G")
    g.set(29, 38, "u")

    # --- TASSETS. The hips were the one flat area left, which on a figure this size is a lot of
    # dead pixels. Three plates at uneven widths, hanging to uneven depths.
    for (tx0, tx1, ty1) in ((15, 20, 39), (21, 26, 41), (27, 31, 38)):
        g.rect(tx0, 36, tx1, ty1, "R")
        g.line(tx0, 36, tx1, 36, "r")
        g.line(tx0, ty1, tx1, ty1, "o")
        g.line(tx0, 36, tx0, ty1, "r")
        g.line(tx1, 36, tx1, ty1, "q")
    g.set(23, 38, "q")

    roster.contact(g, 14, 33, y=FEET)
    eyes(g, 10, 21, 26, "Y", "y")
    return g.rows()


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_warden")
    if not os.path.isdir(out):
        os.makedirs(out)
    pose = warden()
    filled, edges, ratio = roster.density(pose)
    P.write_strip([pose], PALETTE, os.path.join(out, "warden-pose.png"), CELL)
    print("warden %dx%d  %d px  %d edges  %.2f edges/px%s"
          % (CELL, CELL, filled, edges, ratio, "" if ratio >= 0.80 else "   SPARSE"))
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
