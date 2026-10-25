# -*- coding: utf-8 -*-
"""The Broken Circle's own cast (.ai/campaign.md section 6). Run: python tools/art/cast_heath.py

"The circle where the wizards met. He came here first." The high heath's people - shepherds,
wallers, warreners, the heath-keepers who beat out its fires and the stewards who kept the moot for
the wizards - its bulls, adders and black hares, taken; and the iron rod he drove into the hill.

THE FLOOR DECIDES THE PALETTE. The heath is heather (violet's deep half) over peat (stone's shade),
so a body drawn in violet or stone from the deep end is a hole in the ground with two eyes in it.
Every body here carries its weight one or two stops up its ramp and leans on the rim; the deep
stops are kept for crevices. The rows are split by job, the way section 2 of .ai/bestiary.md asks:
linen for the heath's people (the shepherd's sodden cloak, the beater's smock, the bull's shag),
stone for what is black as the peat (the hare, the adder, the warrener's oilskin), violet only for
the two who kept the moot (the steward and the Lawspeaker), bone for horn, fleece and birch, and
steel for the one thing he built.

NOTHING ECHOES THE NINTH STONE. The boss is a tall single slab with a split down its face, so no
creature here is a tall slab, a menhir or an obelisk. The waller's wall is carried wide and low and
is built of many stones, not one; the thunder rod is a rod - three pixels wide - with a crown.

ONE ATTACHMENT EACH, no two alike on the heath (filled black, each is still nameable): a crook's
hook rising over a hood; ears laid back along a stretched line; a section of dry-stone wall carried
before a body; horns wider than a shaggy head; a spark floating loose at a raised hand; a crown of
prongs on a thin rod; a raised head over a coil; a swollen bale pinched by its ties; a broom of
birch twigs raised overhead; a net bag hanging from a short pole; a ring of iron on a staff taller
than the figure that holds it.

The palette characters are redefined exactly as in cast_forest.py and for the same reason: lichen
in the azure characters, which no enemy may wear as pigment. The chapter's bolt element is
lightning, which the roster palette already holds in "[];:", so the steward's spark needs no remap.
"""
from __future__ import print_function

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bestiary_kit
import bonelight as B
import roster
import sprite_bosses as SB
from foes_forest import _limb
# The generic drawing helpers live with the first chapter drawn; they paint, they do not decide.
from cast_forest import blob, rim, twig, inside

G = roster.G
mass = roster.mass
fold = roster.fold
cut = roster.cut
eyes = roster.eyes
contact = roster.contact

PALETTE = dict(roster.PALETTE)
for _ch, _hex in zip("U5678", B.MATERIALS["lichen"]):
    PALETTE[_ch] = _hex

MOSS_HI, MOSS_LIT, MOSS, MOSS_SH, MOSS_DP = "U", "5", "6", "7", "8"
# The chapter's bolt element (.ai/bestiary.md section 6): lightning, core / hot / mid / edge.
SPARK_CORE, SPARK_HOT, SPARK_MID, SPARK_EDGE = "[", "]", ";", ":"
SPARK = SPARK_CORE + SPARK_HOT + SPARK_MID + SPARK_EDGE

CELL = 32
FEET = CELL - 2


# --- local helpers ------------------------------------------------------------------------------

def stones(g, spots):
    """Field granite, each stone a small rounded lump: (x, y, w, h). Lit on its upper-left corner
    and shaded on its lower edge, the corners knocked off - the dry-stone waller's courses are
    rounded stones, never bricks, so no two are the same size. Granite is the one pale stone on the
    heath, so its faces are bone's lower stops over stone's top ones: in stone's own row a wall on a
    peat floor was a coal heap."""
    for (x, y, w, h) in spots:
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                corner = (xx == x and yy == y) or (xx == x + w - 1 and yy == y + h - 1)
                if corner and w > 2 and h > 1:
                    continue                            # round the stone off
                g.set(xx, yy, "m")
        g.set(x + (1 if w > 2 else 0), y, "n")
        if w > 2:
            g.set(x, y + (1 if h > 1 else 0), "n")
            g.set(x + 1, y + (1 if h > 2 else 0), "k" if (x + y) % 3 == 0 else "n")
        if h > 1:
            g.span(y + h - 1, x + 1, x + w - 2, "P")
        g.set(x + w - 1, y + h - 1 - (1 if h > 1 else 0), "p")


def coil(g, cx, cy, rx, ry, thick, zig=True, phase=0):
    """One turn of a snake lying on the ground, seen from the side: an elliptical tube, lit on its
    upper edge and dark on its lower one, with the adder's zigzag running along its top."""
    for k in range(0, 360, 2):
        a = math.radians(k)
        x = cx + rx * math.cos(a)
        y = cy + ry * math.sin(a)
        for t in range(int(thick)):
            g.set(x, y - t, "N")
    for k in range(0, 360, 2):
        a = math.radians(k)
        x = cx + rx * math.cos(a)
        y = cy + ry * math.sin(a)
        g.set(x, y - thick + 1, "p")                    # the lit back
        g.set(x, y, "j")                                # the shaded flank
    if zig:
        for k in range(0, 360, 12):
            a = math.radians(k + phase)
            x = cx + rx * math.cos(a)
            y = cy + ry * math.sin(a) - thick / 2.0 + (0.5 if (k // 12) % 2 else -0.5)
            g.set(x, y, "e" if (k // 12) % 2 else "l")


def spark(g, x, y):
    """The steward's spark: a four-armed star of lightning, white-hot at the centre and gold at
    the tips (.ai/art-direction.md: identity in the mid and edge, never in the core)."""
    for (dx, dy, ch) in ((0, 0, SPARK_CORE), (1, 0, SPARK_HOT), (-1, 0, SPARK_HOT), (0, 1, SPARK_HOT),
                         (0, -1, SPARK_HOT), (2, 0, SPARK_MID), (-2, 0, SPARK_MID), (0, 2, SPARK_MID),
                         (0, -2, SPARK_MID), (1, -1, SPARK_MID), (-1, 1, SPARK_MID), (3, -1, SPARK_EDGE),
                         (-3, 1, SPARK_EDGE), (1, 3, SPARK_EDGE), (-1, -3, SPARK_EDGE),
                         (1, 1, SPARK_EDGE), (-1, -1, SPARK_EDGE)):
        g.set(x + dx, y + dy, ch)


def chain(g, pts, a="r", b="q", under="R"):
    """Links in alternating tones along a polyline - one tone is a wire, two are a chain."""
    k = 0
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + (1 if (x1, y1) == pts[-1] else 0)):
            t = i / float(max(1, n))
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            g.set(x, y, a if k % 2 else b)
            if under and k % 2 == 0 and g.get(x, y + 1) != ".":
                g.set(x, y + 1, under)
            k += 1


# =================================================================================================
# THE ELEVEN
# =================================================================================================

def shepherd():
    """BULK. A hill shepherd in a sodden cloak, his crook rising above his head with its hook
    turned out, away from him.

    The crook is the attachment, and it is held IN FRONT of him and higher than the hood, so the
    hook is the top of the silhouette. Under it he is a hunched cone of wet linen - a short cape
    over the shoulders, the long cloak under it, both darkened down the back where the rain ran -
    and the cape and cloak are separated by a line of occ, because two layers of one cloth only
    read as two where something dark lies between them.
    """
    g = G(CELL)
    # THE CLOAK: hunched, wet, darker toward the hem.
    mass(g, [(9, 10, 16), (12, 8, 17), (16, 7, 18), (22, 6, 18), (27, 5, 19), (28, 5, 19)],
         "E", "e", "A")
    for (x0, y0, x1, y1) in ((8, 18, 7, 28), (11, 17, 10, 28), (14, 19, 14, 28), (17, 18, 17, 27)):
        g.line(x0, y0, x1, y1, "A")                     # wet runs down the cloak
        g.set(x0 - 1, y0 + 2, "e")
    for (x, y) in ((9, 22), (12, 24), (15, 21), (6, 25)):
        g.set(x, y, "e")
    # A ragged, dripping hem.
    for (x, d) in zip(range(5, 20), [1, 2, 1, 0, 2, 1, 1, 0, 2, 1, 0, 1, 2, 1, 0]):
        for k in range(1, d + 1):
            g.set(x, 28 + k, "A" if k < d else "E")
    cut(g, (8, 29), (8, 30), (13, 29), (13, 30))
    # BOOTS under it.
    for (bx, tone) in ((10, "j"), (15, "N")):
        g.span(FEET, bx, bx + 3, tone)
        g.set(bx, FEET, "p")
        g.set(bx + 3, FEET, "J")
    # THE CAPE over the shoulders, ending in drip points, an occ line under its hem.
    mass(g, [(9, 10, 16), (11, 8, 18), (14, 7, 19), (16, 7, 19)], "e", "l", "E")
    for (x, y) in ((7, 17), (8, 18), (10, 17), (12, 18), (13, 17), (15, 18), (17, 17), (18, 18),
                   (9, 17), (11, 17), (14, 17), (16, 17)):
        g.set(x, y, "e" if (x % 3) else "E")
    g.line(7, 19, 18, 19, "o")
    for (x, y) in ((8, 18), (11, 18), (14, 19), (17, 18)):
        g.set(x, y, "l")                                # the lit points of the cape's drips
    fold(g, [(11, 11), (10, 16)], "E", "l")
    fold(g, [(15, 12), (15, 16)], "E", None)
    # A satchel strap across the cape, and the satchel at his back hip.
    g.line(16, 11, 8, 18, "A")
    g.rect(4, 19, 7, 23, "j")
    g.span(19, 4, 7, "N")
    g.set(4, 19, "p")
    g.set(7, 23, "J")
    g.set(5, 21, "J")
    # THE HOOD, peaked and drooping forward over a void.
    mass(g, [(5, 12, 14), (6, 11, 16), (8, 10, 17), (10, 10, 18), (12, 11, 18)], "E", "e", "A")
    g.set(17, 6, "E")
    g.set(18, 7, "A")
    roster.void(g, 8, 11, 1.4, 1.0, cx=16.2)
    g.set(13, 6, "l")
    g.set(12, 7, "l")
    # THE HAND on the crook, and the arm under the cape.
    _limb(g, 17, 14, 21, 16, 1.0, "E", "e", "A")
    g.rect(21, 15, 22, 16, "n")
    g.set(21, 15, "k")
    # DROPS off the hem and the hood.
    for (x, y) in ((19, 26), (18, 11)):
        g.set(x, y, "L")
    rim(g, 3, 29, skip="o")
    # THE CROOK, after the rim so the staff is not doubled into a white wire: stone, from the ground
    # before him up past his hood, the hook turned out and away from him.
    roster.shaft(g, 21, FEET, 23, 7, "P", "p", "N", width=2)
    for (x, y, ch) in ((23, 6, "P"), (23, 5, "P"), (24, 4, "P"), (25, 3, "P"), (26, 3, "p"),
                       (27, 4, "N"), (28, 5, "N"), (28, 6, "j"), (28, 7, "j"), (27, 8, "J"),
                       (24, 5, "J"), (25, 4, "J"), (26, 4, "j"), (27, 5, "J"), (24, 6, "J")):
        g.set(x, y, ch)
    g.rect(21, 15, 22, 16, "n")                         # the hand closed on it
    g.set(21, 15, "k")
    contact(g, 6, 22)
    eyes(g, 9, 15, 17, "Y", "y")
    return g.rows()


def black_hare():
    """FAST, and the Warrener's minion. A hare as black as the peat, running flat out.

    The read is ONE LONG LINE: the hind feet thrown back, the forelegs reaching, the body stretched
    between them, and the ears laid flat back along the spine and out past the scut. Nothing else
    on the heath is this low and this long. It is built from masses - a big haunch, a barrel, a deep
    chest - because a hare is its haunch, and a stick body on stick legs read as an insect. It is
    stone, the colour of the peat it runs over, so its back is carried in stone's lit and hi stops
    and its belly in pale linen, or it would be a hole in the ground moving at speed.
    """
    g = G(CELL)
    # THE FAR LIMBS, a stop darker: a hind foot thrown back, a foreleg folded under the chest.
    _limb(g, 12, 23, 7, 27, 1.3, "N", "p", "j")
    g.line(7, 27, 5, 28, "j")
    _limb(g, 22, 23, 24, 27, 1.0, "N", "p", "j")
    g.set(25, 27, "j")
    # THE MASSES: haunch, barrel, chest - each lit on its upper left.
    blob(g, 11.0, 20.5, 5.0, 4.0, "p", "m", "N", deep="j")
    blob(g, 17.5, 20.0, 5.0, 3.0, "p", "m", "N", deep="j")
    blob(g, 22.5, 20.5, 2.8, 2.8, "p", "m", "N", deep="j")
    # The pale belly, under the barrel and the chest.
    for x in range(12, 25):
        col = [y for y in range(14, 26) if inside(g, x, y)]
        if len(col) > 4:
            g.set(x, col[-1], "E")
            g.set(x, col[-2], "l" if x % 4 == 1 else "e")
    # Fur laid back by the run: a dark stroke under a lit one, uneven.
    for (x0, y0, x1, y1) in ((9, 20, 13, 21), (14, 19, 19, 19), (17, 21, 21, 22), (8, 22, 11, 23)):
        g.line(x0, y0, x1, y1, "N")
        g.line(x0 + 1, y0 - 1, x1 - 1, y1 - 1, "P")
    g.set(11, 18, "j")
    for (x, y) in ((12, 17), (16, 17), (9, 18)):
        g.set(x, y, "n")                                 # a cold sheen on the black, top-left
    # THE SCUT, cocked up off the rump - pale underneath, as a hare's is.
    for (x, y, ch) in ((6, 18, "l"), (5, 19, "e"), (6, 19, "L"), (7, 18, "e")):
        g.set(x, y, ch)
    # THE HEAD, low and forward, the muzzle blunt.
    blob(g, 26.0, 18.5, 3.0, 2.4, "p", "m", "N")
    for (x, y, ch) in ((29, 19, "p"), (29, 20, "N"), (30, 19, "N"), (28, 21, "j"), (27, 21, "N"),
                       (26, 20, "e"), (27, 20, "E")):
        g.set(x, y, ch)
    g.set(30, 18, "k")                                   # the nose, catching the key
    # THE EARS, laid back along the spine and out past the scut: two blades, the near one with a
    # bone edge inside it, black at the tips.
    for (x0, y0, x1, y1, top, body, inner) in ((24, 16, 4, 14, "P", "p", "n"),
                                               (23, 17, 7, 16, "p", "N", "j")):
        n = x0 - x1
        for i in range(n + 1):
            t = i / float(n)
            x = x0 - i
            y = y0 + (y1 - y0) * t
            g.set(x, y, top)
            if t < 0.82:
                g.set(x, y + 1, inner if (i % 4 and t > 0.2) else body)
            if t > 0.88:
                g.set(x, y, "J")
    rim(g, 10, 24, skip="o")
    # THE NEAR LIMBS, after the rim so they are not doubled into white wires: the hind foot
    # stretched far back, the foreleg reaching to the ground ahead.
    _limb(g, 10, 23, 5, 26, 1.7, "p", "P", "N")
    for (x, y, ch) in ((4, 26, "N"), (4, 27, "j"), (5, 27, "N"), (6, 27, "j")):
        g.set(x, y, ch)
    _limb(g, 23, 22, 28, 27, 1.1, "p", "P", "N")
    twig(g, [(28, 27), (29, FEET)], "p", "J")
    g.span(FEET, 28, 31, "N")
    g.set(28, FEET, "P")
    contact(g, 7, 29)
    eyes(g, 18, 26, 27, "y", "Y")
    return g.rows()


def waller():
    """HEAVY. A dry-stone waller carrying a section of his wall on a frame before him.

    The wall is the shield plane section 4 asks a shielder for, and two rules decide how it is
    drawn. It is WIDE AND LOW - wider than it is tall - because the Ninth Stone is a tall slab and
    nothing on the heath may be one. And it is MANY STONES, round and every one a different size,
    laid in rough courses with a jagged row of copes set on edge along its top: a wall is built of
    stones, and a flat plane with even joints is a door (the first pass, in a full frame, was a
    crate). The frame is only a sledge under it and two short stiles of dark wood, so the copes are
    the top of the silhouette and the pale granite is the brightest thing on him.
    """
    g = G(CELL)
    # LEGS, braced under the load.
    for (lx, tone, lit) in ((5, "A", "E"), (10, "E", "e")):
        g.rect(lx, 24, lx + 2, 29, tone)
        g.line(lx, 24, lx, 29, lit)
        g.span(FEET, lx - 1, lx + 3, "j")
        g.set(lx - 1, FEET, "p")
    g.rect(11, 25, 12, 26, "j")                          # a leather knee-pad
    g.set(11, 25, "p")
    # THE BODY: hunched forward into the load.
    mass(g, [(10, 7, 12), (13, 5, 14), (18, 4, 15), (23, 4, 14), (24, 4, 14)], "E", "e", "A")
    fold(g, [(7, 13), (6, 19), (7, 23)], "A", "e")
    fold(g, [(10, 14), (10, 22)], "A", None)
    g.span(21, 4, 14, "o")                               # the belt
    g.set(8, 21, "u")
    g.set(12, 15, "l")
    # THE CAP and the head, pushed forward toward the wall's top.
    g.rect(8, 6, 14, 8, "j")
    g.span(5, 9, 13, "N")
    g.set(9, 5, "p")
    g.set(11, 6, "J")
    g.span(8, 8, 16, "J")                                # the peak
    g.span(7, 14, 16, "N")
    g.set(16, 8, "j")
    roster.void(g, 9, 11, 1.8, 1.4, cx=13.0)
    g.set(10, 9, "E")
    g.set(9, 10, "e")
    # THE WALL: rough courses of round stones, every one a different size, on dark joints.
    g.rect(16, 15, 30, 26, "J")
    for (x, y) in ((18, 19), (23, 22), (27, 17), (21, 25), (29, 24)):
        g.set(x, y, "o")
    stones(g, [(16, 23, 4, 3), (20, 24, 3, 2), (23, 23, 4, 3), (27, 23, 4, 3),
               (16, 20, 3, 3), (19, 20, 5, 3), (24, 21, 3, 2), (27, 20, 4, 3),
               (16, 17, 5, 3), (21, 17, 3, 3), (24, 18, 4, 3), (28, 17, 3, 3),
               (17, 15, 3, 2), (20, 15, 4, 2), (25, 15, 3, 3), (28, 15, 3, 2)])
    # THE COPES, set on edge along the top at uneven heights - the wall's crest.
    for (x, top, w) in ((16, 13, 2), (18, 12, 2), (20, 13, 1), (21, 11, 2), (23, 13, 2), (25, 12, 1),
                        (26, 11, 2), (28, 12, 2), (30, 13, 1)):
        for xx in range(x, x + w):
            g.line(xx, top + (xx - x), xx, 14, "n" if xx == x else "m")
        g.set(x, top, "k")
        g.set(x + w - 1, 14, "P")
    for (x, y) in ((17, 24), (21, 21), (28, 19), (18, 18), (25, 24), (22, 16), (29, 22)):
        g.set(x, y, MOSS_LIT if (x + y) % 2 else MOSS)  # lichen on the old faces
    g.set(26, 22, MOSS_HI)
    # THE FRAME: a sledge under the stones and two short stiles - dark, weathered wood.
    g.line(14, 27, 30, 27, "e")
    g.line(14, 28, 30, 28, "A")
    g.set(14, 27, "l")
    for px in (15, 30):
        g.line(px, 20, px, 27, "e" if px == 15 else "E")
        g.set(px, 20, "l")
    g.set(31, 27, "A")
    rim(g, 4, 29, skip="o")
    # THE HANDS on the near stile: one high, one low.
    for (hy, arm) in ((20, (11, 14)), (24, (12, 18))):
        _limb(g, arm[0], arm[1], 14, hy, 1.0, "E", "e", "A")
        g.rect(14, hy, 15, hy + 1, "n")
        g.set(14, hy, "k")
    contact(g, 3, 30)
    eyes(g, 10, 12, 14, "Y", "y")
    return g.rows()


def bull():
    """WALL. A shaggy bull of the high moor, head low, horns wider than its head over a fringe.

    The horns are the attachment: a pale bone lyre spread out either side of the lowered head and
    turned up at the tips, wider than anything else on it. Under them the whole animal is shag -
    long linen hair in LOCKS, each a dark stroke with a light one beside it, laid down the flank and
    hanging off the belly in a ragged curtain with the ground showing through. The first pass
    combed the coat into even vertical stripes and the bull came out a radiator. Built from masses -
    the hump of the shoulder above everything, the haunch, the barrel - and never a box on legs.
    """
    g = G(CELL)
    mid, lit, dark = "E", "e", "A"
    # THE TAIL, hanging off the rump, its tuft.
    _limb(g, 5, 15, 4, 23, 0.8, mid, lit, dark)
    g.rect(4, 23, 5, 25, dark)
    g.set(4, 23, lit)
    # THE MASSES: haunch, barrel, the hump of the shoulder, the neck dropping to a low head.
    g.disc(9.5, 17.5, 4.8, mid)
    g.disc(15.0, 14.5, 5.8, mid)
    g.disc(19.5, 18.0, 4.2, mid)
    for x in range(9, 22):
        for y in range(16, 24):
            g.set(x, y, mid)
    # Light the back, shade the belly.
    for x in range(4, 25):
        col = [y for y in range(6, 26) if g.get(x, y) == mid]
        if col:
            g.set(x, col[0], "l" if x in (12, 13, 14) else lit)
            g.set(x, col[1], lit)
            g.set(x, col[-1], dark)
    # THE LOCKS: short hanks of hair laid down and back, a dark stroke with a lit one above it, set
    # unevenly so the coat never reads as stripes.
    for (x, y, n) in ((7, 16, 3), (10, 13, 3), (13, 11, 3), (16, 10, 2), (12, 17, 4), (16, 15, 3),
                      (19, 14, 3), (9, 20, 3), (14, 20, 3), (18, 19, 3), (21, 17, 2), (6, 19, 2)):
        for i in range(n):
            if inside(g, x - i * 0.5, y + i):
                g.set(x - i * 0.5, y + i, dark)
            if inside(g, x - i * 0.5 - 1, y + i - 1):
                g.set(x - i * 0.5 - 1, y + i - 1, lit)
    # THE SHAG: a ragged curtain off the belly, longer in places, the ground showing through.
    for (x, d) in zip(range(6, 24), [2, 4, 3, 1, 4, 3, 2, 5, 3, 2, 4, 1, 3, 5, 2, 3, 4, 2]):
        for k in range(d):
            g.set(x, 23 + k, dark if (x + k) % 3 else mid)
        g.set(x, 23 + d - 1, "J" if d > 2 else dark)
    # THE HEAD, low and forward, turned a little toward the viewer.
    blob(g, 24.0, 21.5, 3.4, 4.0, mid, lit, dark, deep="J")
    for (x, y, ch) in ((24, 25, dark), (25, 25, "m"), (23, 25, mid), (24, 24, "n"), (25, 24, "k"),
                       (23, 24, "m"), (26, 24, "n")):
        g.set(x, y, ch)                                  # the wet muzzle, catching the key
    # A dewlap of shag hanging from the throat.
    for (x, d) in ((21, 3), (22, 4), (23, 2)):
        for k in range(d):
            g.set(x, 25 + k, dark)
    # THE FRINGE: hair falling over the eyes in strands.
    for (x, y0, y1, tone) in ((21, 18, 21, lit), (22, 17, 22, mid), (23, 17, 21, "l"), (24, 17, 22, dark),
                              (25, 17, 21, lit), (26, 18, 22, mid), (27, 18, 21, dark)):
        g.line(x, y0, x, y1, tone)
    rim(g, 4, 29, skip="o")
    # LEGS, after the rim: short, half hidden in the shag, a hoof under each.
    for (lx, tone) in ((7, dark), (10, mid), (18, dark), (21, mid)):
        for y in range(26, 30):
            if g.get(lx, y) == "." or y > 27:
                g.set(lx, y, tone)
            if g.get(lx + 1, y) == "." or y > 27:
                g.set(lx + 1, y, dark)
        g.set(lx, 28, lit if tone == mid else mid)
        g.span(FEET, lx - 1, lx + 1, "J")
        g.set(lx - 1, FEET, "j")
    # THE HORNS: a wide bone lyre out of the poll - out and a little down, then sweeping up - thick
    # at the root and pale at the tip. Straight segments read as a handlebar; this is a curve.
    for (p0, p1, p2, near) in (((22.0, 18.0), (16.0, 19.5), (16.5, 12.5), False),
                               ((26.0, 18.0), (32.0, 19.5), (30.5, 12.5), True)):
        for i in range(25):
            t = i / 24.0
            x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
            y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
            g.set(x, y, "S" if t > 0.85 else ("s" if t < 0.5 else "k"))
            if t < 0.55:
                g.set(x, y + 1, "n" if near else "m")
            if t < 0.3:
                g.set(x, y - 1, "k")
    g.set(23, 17, "k")
    g.set(25, 17, "k")
    contact(g, 5, 26)
    eyes(g, 20, 23, 25, "Y", "y")
    return g.rows()


def steward():
    """RANGED. One who kept the circle for the wizards: tall and hooded, a spark floating loose at
    the raised hand - it is what it throws.

    Violet, because the moot's people wore it, and carried in violet's lit and base stops with a
    long linen stole down the front: violet's deep end on a heather floor is nothing at all. The
    hood overhangs the face and the raised sleeve hangs open under the arm, so the figure has a
    caster's outline and not a column's. The spark is detached, with daylight all round it, and
    drawn in the chapter's lightning, the colour of the bolt that follows.
    """
    g = G(CELL)
    # THE ROBE: tall and narrow, flared at a ragged hem.
    mass(g, [(9, 12, 17), (13, 11, 18), (20, 10, 18), (26, 9, 19), (28, 8, 21)], "w", "v", "x")
    fold(g, [(12, 13), (11, 21), (10, 28)], "x", "v")
    fold(g, [(17, 14), (19, 27)], "X", None)
    for (x, d) in zip(range(8, 22), [1, 1, 2, 1, 0, 1, 2, 2, 1, 0, 1, 2, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 28 + k, "x" if k < d else "w")
    cut(g, (12, 29), (12, 30), (18, 29))
    g.set(14, FEET, "j")
    g.set(15, FEET, "N")
    # THE STOLE: a long strip of linen down the front, fringed at its end.
    for y in range(12, 26):
        x = 14 + (1 if y > 19 else 0)
        g.set(x, y, "e" if y % 5 else "l")
        g.set(x + 1, y, "E")
    for (x, y) in ((15, 26), (16, 27), (14, 26)):
        g.set(x, y, "A")
    g.set(15, 16, "u")                                   # a brooch, old gold
    # A rope girdle.
    g.line(10, 19, 18, 18, "o")
    g.set(11, 19, "n")
    g.line(11, 20, 10, 23, "n")
    # THE HOOD, peaked high and overhanging the face.
    mass(g, [(3, 13, 13), (5, 12, 15), (7, 11, 18), (9, 11, 18), (11, 12, 17)], "w", "v", "x")
    roster.void(g, 8, 11, 1.0, 1.4, cx=16.0)
    g.set(18, 7, "x")
    g.set(17, 7, "w")
    g.set(12, 6, "V")
    g.set(13, 4, "V")
    # THE ARM, raised to the spark, its sleeve hanging open under it; the other hanging.
    _limb(g, 17, 12, 21, 10, 1.1, "w", "v", "x")
    for (x, y0, y1) in ((18, 12, 15), (19, 12, 16), (20, 11, 14)):
        g.line(x, y0, x, y1, "x")
    g.set(19, 16, "X")
    g.set(18, 15, "w")
    g.set(22, 9, "n")
    g.set(22, 10, "m")
    g.set(21, 9, "k")
    _limb(g, 11, 12, 10, 19, 1.0, "x", "w", "X")
    # THE SPARK, detached, with daylight round it.
    spark(g, 26, 8)
    rim(g, 2, 29, skip="o" + SPARK)
    contact(g, 8, 22)
    eyes(g, 9, 15, 16, "Y", "y")
    return g.rows()


def thunder_rod():
    """TURRET. An iron rod he drove into the hill to call the storm down on the moot. BUILT, so it
    burns: fire in the crown where a taken thing would stare cold.

    THIN, NEVER A SLAB. The Ninth Stone is the chapter's tall slab, so this is a rod two pixels
    wide, twisted - the twist is its texture - with a crown of iron prongs splayed wide at its top
    and the burning cage between them. It stands in a low clod of torn turf and split granite where
    it went in (a tall mound under it, the first pass, made it a chess pawn). Steel in its shade and
    base stops with a lit edge: steel deep is invisible on peat.
    """
    g = G(CELL)
    # THE CLOD it was driven into: torn heather and split granite, low and uneven.
    mass(g, [(27, 11, 19), (28, 8, 22), (29, 6, 24), (30, 6, 25)], "w", "v", "x")
    stones(g, [(7, 28, 3, 2), (19, 27, 4, 3), (12, 28, 3, 2), (23, 29, 2, 1)])
    for (x, y) in ((10, 27), (13, 26), (17, 26), (21, 26), (8, 28)):
        g.set(x, y, "V")                                 # heather torn up round it
        g.set(x, y + 1, "x")
    g.set(15, 27, "o")
    g.set(16, 28, "o")                                   # the split where it went in
    # THE ROD: two pixels, twisted, a lit edge spiralling round it.
    for y in range(10, 28):
        g.set(15, y, "r")
        g.set(16, y, "q")
        if (y // 2) % 3 == 0:
            g.set(15, y, "t")
        elif (y // 2) % 3 == 2:
            g.set(16, y, "R")
    # Iron collars at two heights, and a stay run down to a stake.
    for cy in (17, 23):
        g.span(cy, 14, 17, "R")
        g.set(14, cy, "t")
        g.set(17, cy, "q")
    g.line(17, 18, 23, 26, "R")
    g.set(23, 26, "t")
    g.set(23, 27, "q")
    # THE CROWN: a collar, and prongs splayed wide out of it, barbed at the tips.
    g.span(10, 12, 19, "R")
    g.span(11, 13, 18, "q")
    g.set(12, 10, "t")
    for (x0, x1, top) in ((13, 7, 5), (14, 10, 3), (17, 21, 3), (18, 24, 5)):
        g.line(x0, 9, x1, top, "r")
        g.set(x1, top, "T")
        g.set(x1 + (1 if x1 > 15 else -1), top + 1, "R")   # the barb
        g.set(x1 + (1 if x1 > 15 else -1), top + 2, "q")
    g.line(15, 9, 15, 2, "t")
    g.line(16, 9, 16, 3, "R")
    g.set(15, 2, "T")
    # THE CAGE in the crown, where it burns.
    g.rect(13, 5, 18, 8, "q")
    g.span(5, 13, 18, "R")
    g.line(13, 5, 13, 8, "r")
    g.set(15, 8, "=")
    g.set(16, 6, "~")
    rim(g, 2, 29, skip="o")
    contact(g, 6, 25)
    eyes(g, 7, 14, 17, "+", "-")
    return g.rows()


def adder():
    """CHARGER. An adder the length of a man, coiled on the ground, its head raised and drawn back
    to strike.

    The coil is the attachment - stacked turns on the ground, the only round low mass on the heath
    - with the neck rising out of it in a hook and the broad flat head cocked back over it. Stone,
    black as the peat, carried in its lit stops along every turn's back, with the adder's zigzag
    down the spine in linen and a line of occ between the turns, so each reads as a turn and not as
    a basket.
    """
    g = G(CELL)
    # THE COIL: three turns stacked, the lowest widest.
    coil(g, 14.0, 27.0, 9.0, 2.6, 3, phase=0)
    coil(g, 14.5, 24.0, 7.0, 2.2, 3, phase=6)
    coil(g, 15.0, 21.0, 5.0, 1.8, 3, phase=3)
    # The joins between the turns, in occ, on the near side only.
    for (cx, cy, rx, ry) in ((14.0, 27.0, 9.0, 2.6), (14.5, 24.0, 7.0, 2.2)):
        for k in range(200, 340, 4):
            a = math.radians(k)
            x = cx + rx * 0.82 * math.cos(a)
            y = cy + ry * math.sin(a) - 2.6
            if inside(g, x, y):
                g.set(x, y, "J")
    # The lowest turn's belly on the ground, bone scales catching the key.
    for x in range(7, 22, 2):
        g.set(x, 29, "n")
    # THE NECK: rising from the top turn, curving back in a hook.
    pts = [(18, 20), (20, 17), (20, 14), (18, 11), (16, 10)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        _limb(g, x0, y0, x1, y1, 1.5, "N", "p", "j")
    for (x, y) in ((19, 18), (20, 15), (19, 12)):
        g.set(x, y, "l")                                 # the zigzag runs up the neck
        g.set(x + 1, y + 1, "e")
    g.set(21, 16, "n")
    g.set(21, 14, "n")
    # THE HEAD, flat and broad, cocked back and pointing at its prey.
    blob(g, 19.5, 8.0, 4.0, 2.3, "N", "p", "j", deep="J")
    for (x, y, ch) in ((24, 8, "N"), (24, 7, "p"), (25, 8, "j"), (23, 10, "j"), (24, 9, "J")):
        g.set(x, y, ch)
    g.set(17, 7, "e")                                    # the dark V on the crown, outlined
    g.set(18, 8, "J")
    g.set(19, 7, "J")
    g.set(20, 8, "J")
    g.set(21, 7, "e")
    g.set(16, 8, "l")
    # The tongue, flickering.
    g.set(26, 9, "o")
    g.set(27, 8, "o")
    g.set(27, 10, "o")
    # The tail tip, curling out from under the coil.
    twig(g, [(23, 28), (26, 27), (27, 25)], "p", "J")
    rim(g, 6, 29, skip="o")
    contact(g, 5, 24)
    eyes(g, 7, 22, 23, "Y", "y")
    return g.rows()


def fleece_bale():
    """EXPLODER. A bale of wet fleece swollen round nothing, splitting at its ties.

    The ropes are the attachment: a cross of cord over a round swell, tied off in a knot on top
    with its ends standing up, and the swell pinched in wherever a cord goes round it - so filled
    black it is a lumpy round bundle with a knot on its crown, nothing else on the heath. The wool
    is wet linen curled over with bone - the first pass, bone all over and banded by straight ties,
    was a steel crate - and it bulges pale out of the splits where the cord has bitten. The mark
    looks out of the widest split. It FACES LEFT, like every exploder, because its stand-in scene
    says SpriteFacesRightByDefault = false.
    """
    g = G(CELL)
    # THE BALE: a swell, lumpy - two lobes above the waist cord, one below.
    blob(g, 16.0, 21.5, 9.6, 7.0, "e", "l", "E", deep="A")
    blob(g, 11.5, 15.5, 5.2, 4.6, "e", "l", "E", deep="A")
    blob(g, 20.5, 15.0, 5.6, 5.0, "e", "l", "E", deep="A")
    # It sits on the ground: the bottom flattened and wettest.
    for x in range(4, 30):
        for y in (27, 28, 29):
            if inside(g, x, y):
                g.set(x, y, "A" if y == 29 else ("E" if (x + y) % 3 else "A"))
    # CURLS: hooks of fleece, bone over linen, laid irregularly - denser on the dry top.
    for (x, y) in ((9, 13), (13, 12), (18, 11), (22, 12), (24, 15), (8, 17), (12, 16), (16, 14),
                   (20, 16), (24, 19), (10, 21), (14, 20), (18, 21), (22, 22), (12, 24), (17, 25),
                   (21, 25), (7, 22), (15, 17), (25, 23)):
        if inside(g, x, y) and inside(g, x + 2, y + 1):
            dry = y < 20
            g.set(x, y, "k" if dry else "n")
            g.set(x + 1, y, ("s" if (x + y) % 4 == 0 else "k") if dry else "e")
            g.set(x + 2, y + 1, "E")
            g.set(x, y + 1, "A")
            g.set(x + 1, y + 1, "n" if dry else "E")
    # THE CORDS: one round the waist, one over the top from front to back, dark with a lit twist.
    waist = [(x, 20.0 + 0.9 * math.sin((x - 6) / 21.0 * math.pi)) for x in range(5, 28)]
    over = []
    for y in range(9, 28):
        t = (y - 9) / 18.0
        over.append((16.0 + 1.6 * math.sin(t * math.pi) - 0.6 * t, y))
    for i, (x, y) in enumerate(waist + over):
        if inside(g, x, y):
            g.set(x, y, "p" if i % 3 == 0 else "j")
            if inside(g, x + 1, y + 1) and g.get(x + 1, y + 1) not in ("p", "j"):
                g.set(x + 1, y + 1, "J")
    # Pinch the outline in where each cord goes round: the swell bulges between the bites.
    for (x, y) in ((6, 20), (6, 19), (6, 21), (7, 20), (26, 20), (26, 21), (25, 21), (16, 9),
                   (15, 9), (17, 9), (16, 28)):
        cut(g, (x, y))
    # THE KNOT on the crown and its two ends standing up off it.
    for (x, y, ch) in ((15, 9, "P"), (16, 9, "p"), (17, 9, "j"), (15, 10, "p"), (16, 10, "J"),
                       (14, 8, "p"), (13, 7, "P"), (12, 6, "j"), (18, 8, "p"), (19, 7, "N"), (20, 7, "j")):
        g.set(x, y, ch)
    # THE SPLITS, the fleece bulging pale out of them.
    for (x0, y0, x1, y1) in ((18, 23, 21, 24), (22, 17, 24, 18), (9, 24, 11, 25)):
        g.line(x0, y0, x1, y1, "o")
        g.line(x0, y0 - 1, x1, y1 - 1, "k")
        g.set(x0 + 1, y0 - 1, "s")
    # The widest split, at the front: the mark looks out of it.
    g.rect(7, 15, 10, 17, "o")
    g.span(14, 7, 10, "k")
    g.set(8, 14, "s")
    g.span(18, 8, 10, "n")
    rim(g, 4, 29, skip="o")
    # Wet locks hanging to the ground off the bottom edge.
    for (x, d) in ((8, 2), (12, 1), (19, 2), (23, 1)):
        for k in range(d):
            g.set(x, 29 + k, "A")
    contact(g, 6, 26)
    eyes(g, 16, 8, 9, "Y", "y")
    return g.rows()


def fire_beater():
    """SLAMMER. A heath-keeper bringing down a birch fire-beater - the broom of twigs on a long
    handle that the moor's fires were beaten out with.

    The broom is the highest thing in the cell, wound back over his head, and it is a FAN of pale
    birch twigs bound to the handle: not a block, not a blade. He stands square with both hands up
    ABOVE his head - daylight between each arm and the head, which the first pass, hands at the
    brow, never had - a kerchief over the lower face against the smoke, and his smock scorched at
    the hem.
    """
    g = G(CELL)
    # THE BROOM: birch twigs fanned out from the binding, pale, with deep gaps between them.
    for (x1, y1, tone) in ((2, 4, "k"), (3, 2, "n"), (5, 2, "s"), (7, 2, "k"), (2, 7, "n"),
                           (2, 9, "m"), (4, 6, "s"), (4, 9, "k"), (6, 3, "n"), (3, 4, "m"),
                           (8, 2, "n"), (3, 6, "k")):
        g.line(10, 6, x1, y1, tone)
        g.set(x1, y1, "S" if tone in ("s", "k") else tone)
    for (x, y) in ((6, 5), (7, 4), (5, 6), (8, 5), (6, 7), (4, 3)):
        g.set(x, y, "J")
    # The handle down to his hands, and the binding.
    roster.shaft(g, 18, 9, 11, 6, "p", "N", "J", width=2)
    g.rect(9, 5, 11, 7, "r")
    g.set(9, 5, "t")
    g.set(11, 7, "q")
    g.set(10, 6, "R")
    # LEGS, square and planted; gaiters.
    for (lx, tone, lit) in ((11, "A", "E"), (17, "E", "e")):
        g.rect(lx, 25, lx + 2, 29, tone)
        g.line(lx, 25, lx, 29, lit)
        g.rect(lx, 26, lx + 2, 28, "j")
        g.set(lx, 26, "p")
        g.span(FEET, lx - 1, lx + 3, "J")
        g.set(lx - 1, FEET, "N")
    # THE SMOCK: upright, square-shouldered, scorched at the hem.
    SB.swell(g, 16, 25, [5.4, 5.0, 4.6, 5.0, 5.6], "E", "e", "A", cx=15.5)
    fold(g, [(13, 17), (12, 21), (13, 25)], "A", "e")
    fold(g, [(18, 17), (19, 24)], "A", None)
    for (x, y) in ((10, 25), (12, 24), (13, 25), (17, 25), (19, 24), (20, 25), (16, 24)):
        g.set(x, y, "J")                                 # the scorch at the hem
    g.span(21, 11, 20, "o")                              # a belt
    g.set(14, 21, "u")
    # THE HEAD: a close cap, the void, and a kerchief knotted over the mouth.
    g.rect(13, 10, 18, 15, "A")
    g.span(10, 13, 18, "j")
    g.span(9, 14, 18, "N")
    g.set(14, 9, "p")
    g.set(13, 10, "p")
    roster.void(g, 11, 12, 1.6, 1.4, cx=16.2)
    g.span(13, 13, 18, "l")
    g.span(14, 13, 18, "e")
    g.set(13, 13, "L")
    g.set(18, 14, "E")
    g.set(19, 15, "e")                                   # the knot's tail
    # ARMS: both up from the shoulders, past the head, to the handle above it.
    _limb(g, 11, 17, 11, 8, 1.2, "E", "e", "A")
    _limb(g, 20, 17, 19, 9, 1.2, "E", "e", "A")
    for (x, y) in ((11, 7), (18, 8)):
        g.rect(x, y, x + 1, y + 1, "n")
        g.set(x, y, "k")
    rim(g, 2, 29, skip="o")
    contact(g, 9, 21)
    eyes(g, 11, 15, 17, "Y", "y")
    return g.rows()


def warrener():
    """SUMMONER, who calls the Black Hares out. The keeper of the heath's warrens, with a purse net
    held up on a short pole at hand height and a sack at his hip.

    The net is the attachment: a bag of mesh hanging off the pole's end, drawn as a diamond lattice
    of cord with the ground showing through every hole - so filled black it is a lace bag, which
    nothing else carries (the first pass, a checkerboard, read as a flag). A slouch hat, its crown
    dented and its brim dropping at the front (the shepherd beside him is hooded), a coat of
    stone-dark oilskin, and a full linen sack.
    """
    g = G(CELL)
    # THE COAT: stooped, stone, long.
    mass(g, [(11, 10, 16), (14, 8, 18), (20, 7, 18), (26, 7, 19), (28, 7, 19)], "N", "p", "j")
    fold(g, [(10, 13), (9, 21), (9, 27)], "J", "p")
    fold(g, [(15, 15), (15, 27)], "j", "p")
    for (x, y) in ((11, 18), (12, 24), (17, 22)):
        g.set(x, y, "P")
    # A deep pocket with its flap, horn buttons down the front, and the coat worn at the hem.
    g.rect(11, 21, 14, 24, "j")
    g.span(21, 11, 14, "p")
    g.span(22, 11, 14, "J")
    g.set(14, 24, "J")
    for y in (16, 19, 22, 25):
        g.set(17, y, "n")
        g.set(17, y + 1, "J")
    for (x, y) in ((8, 28), (12, 28), (18, 28)):
        g.set(x, y, "J")
        g.set(x, y - 1, "p")
    # The linen shirt at the throat.
    g.rect(15, 12, 17, 15, "e")
    g.set(15, 12, "l")
    g.set(17, 15, "A")
    # LEGS, and boots.
    for (lx, tone) in ((10, "A"), (15, "E")):
        g.rect(lx, 28, lx + 2, 29, tone)
        g.span(FEET, lx - 1, lx + 3, "J")
        g.set(lx - 1, FEET, "j")
    # THE SACK at the hip: linen, bulging, tied at the neck.
    blob(g, 7.0, 23.0, 3.4, 4.0, "E", "e", "A")
    g.set(7, 18, "u")
    g.set(6, 18, "A")
    g.set(8, 19, "o")
    for (x, y) in ((6, 22), (8, 25), (5, 25)):
        g.set(x, y, "l")
        g.set(x + 1, y + 1, "A")
    g.line(9, 19, 15, 13, "A")                           # its strap
    # THE HAT: a low dented crown, a brim dropping at the front.
    g.rect(11, 6, 16, 7, "j")
    g.span(5, 12, 15, "N")
    g.set(12, 5, "p")
    g.set(13, 6, "J")                                    # the dent
    g.span(8, 9, 19, "N")
    g.span(9, 10, 20, "J")
    g.set(9, 8, "p")
    g.set(20, 10, "j")
    g.set(21, 10, "J")
    g.set(10, 9, ".")
    g.span(7, 11, 16, "o")                               # the band
    g.set(12, 7, "u")
    roster.void(g, 10, 12, 1.6, 1.1, cx=16.0)
    rim(g, 3, 29, skip="o")
    # THE ARM up to the pole; the pole; the net hanging off its end.
    _limb(g, 17, 14, 21, 16, 1.2, "N", "p", "j")
    roster.shaft(g, 20, 19, 25, 11, "e", "A", "A", width=2)
    g.rect(20, 15, 21, 16, "n")
    g.set(20, 15, "k")
    # The purse net: a bag, gathered at the pole, a diamond lattice of cord.
    for y in range(12, 24):
        t = (y - 11) / 12.0
        half = 0.8 + 2.9 * math.sin(min(1.0, t * 1.15) * math.pi * 0.85)
        lo, hi = int(round(26.0 - half)), int(round(26.0 + half))
        for x in range(lo, hi + 1):
            on = ((x + y) % 3 == 0) or ((x - y) % 3 == 0) or x in (lo, hi)
            if on:
                knot = ((x + y) % 3 == 0) and ((x - y) % 3 == 0)
                g.set(x, y, "k" if knot else ("n" if y < 18 else "m"))
    g.set(26, 11, "k")
    g.set(25, 24, "m")
    g.set(26, 24, "m")
    contact(g, 4, 21)
    eyes(g, 11, 15, 17, "Y", "y")
    return g.rows()


def lawspeaker():
    """MINIBOSS, on the 48 cell. His jailer at the moot: the one who spoke the law to the wizards'
    council, and speaks his now.

    A rod of office taller than he is, crowned with a RING of iron - an open circle with the ground
    showing through it, the opposite of a slab - and chains of office hung across a stiff violet
    mantle in two heavy loops. The robe flares to the hem and the mantle breaks the shoulder line
    on one side only, so filled black he is a robed figure and a ringed staff, never the Ninth
    Stone's single slab.
    """
    c = 48
    feet = c - 2
    g = G(c)
    # THE ROD OF OFFICE: steel, from the ground beside him up past his head; the ring at its top.
    roster.shaft(g, 37, feet, 38, 13, "t", "r", "q", width=2)
    for ang in range(0, 360, 4):
        a = math.radians(ang)
        for r, ch in ((5.0, "r"), (4.2, "R"), (3.4, "q")):
            x = 38.5 + r * math.cos(a)
            y = 8.0 + r * math.sin(a)
            g.set(x, y, ch)
        if 120 < ang < 260:
            g.set(38.5 + 5.0 * math.cos(a), 8.0 + 5.0 * math.sin(a), "t")
    for (x, y) in ((34, 6), (43, 8), (38, 3), (39, 13)):
        g.set(x, y, "T")                                 # studs on the ring, catching the key
    g.rect(36, 13, 39, 14, "R")                          # the collar where the ring meets the rod
    g.set(36, 13, "t")
    # THE ROBE: long, flared at the hem.
    mass(g, [(16, 17, 29), (22, 15, 31), (32, 13, 32), (40, 12, 34), (44, 11, 35), (45, 11, 35)],
         "w", "v", "x")
    for pts in ([(17, 23), (15, 33), (14, 44)], [(22, 25), (21, 35), (20, 44)], [(27, 24), (28, 34), (28, 44)],
                [(32, 26), (33, 44)]):
        fold(g, pts, "x", "v")
    for (x, y) in ((16, 38), (24, 30), (30, 40), (18, 28)):
        g.set(x, y, "V")
    for (x, d) in zip(range(11, 36), [1, 2, 1, 1, 2, 0, 1, 2, 1, 0, 1, 1, 2, 1, 0, 1, 2, 1, 1, 0, 2, 1, 1, 2, 1]):
        for k in range(1, d + 1):
            g.set(x, 45 + k, "x" if k < d else "X")
    # The robe PARTED down the front over a dark studded under-robe, its edges trimmed in steel.
    for y in range(27, 46):
        w = 1 + (y - 27) // 7
        x0, x1 = 23 - w, 23 + w
        g.span(y, x0, x1, "x")
        g.set(x0, y, "w")
        g.set(x1, y, "X")
        g.set(x0 - 1, y, "r" if y % 4 else "t")
        g.set(x1 + 1, y, "q")
        if y % 4 == 1:
            g.set(23, y, "r")                            # studs down the under-robe
            g.set(23, y + 1, "q")
    # THE BELT, and a book of the law hung from it on a chain.
    g.span(26, 14, 32, "q")
    g.span(27, 14, 32, "R")
    g.set(23, 26, "G")
    g.set(24, 26, "u")
    chain(g, [(16, 27), (15, 31)])
    g.rect(12, 31, 17, 37, "x")
    g.rect(13, 32, 16, 36, "E")
    g.line(13, 32, 13, 36, "e")
    g.span(31, 12, 17, "w")
    g.set(12, 31, "v")
    g.line(17, 31, 17, 37, "X")
    g.set(14, 34, "r")
    # THE MANTLE: stiff, over the shoulders, broken higher on the far side.
    mass(g, [(13, 18, 28), (15, 14, 31), (19, 12, 33), (21, 12, 33)], "w", "v", "x")
    for (x, y) in ((12, 22), (14, 23), (17, 22), (20, 23), (23, 22), (26, 23), (29, 22), (32, 22)):
        g.set(x, y, "x")
    g.line(12, 21, 33, 21, "X")
    g.line(12, 22, 33, 22, "o")
    g.set(31, 13, "w")
    g.set(32, 13, "x")
    g.set(32, 12, "v")                                   # the stiff collar standing up behind the head
    g.line(31, 11, 31, 14, "x")
    # THE CHAINS OF OFFICE: two heavy loops across the mantle, and the seal.
    chain(g, [(15, 15), (18, 20), (23, 23), (28, 20), (31, 15)], "t", "r", "q")
    chain(g, [(14, 17), (17, 24), (23, 28), (29, 24), (32, 17)], "r", "R", "q")
    g.rect(22, 28, 24, 31, "r")
    g.set(22, 28, "T")
    g.set(24, 31, "q")
    g.set(23, 29, "u")
    g.set(23, 30, "o")
    # THE HOOD, a cowl over a void.
    mass(g, [(4, 21, 25), (6, 19, 27), (9, 18, 29), (12, 18, 29), (14, 19, 28)], "w", "v", "x")
    roster.void(g, 8, 12, 2.2, 1.6, cx=25.5)
    g.set(20, 6, "V")
    fold(g, [(21, 7), (20, 12)], "x", "v")
    # THE HAND on the rod, the far arm hanging in its sleeve.
    _limb(g, 31, 19, 36, 24, 1.6, "w", "v", "x")
    g.rect(35, 23, 37, 25, "n")
    g.set(35, 23, "k")
    g.set(37, 25, "m")
    _limb(g, 14, 20, 11, 30, 1.8, "x", "w", "X")
    rim(g, 2, 45, skip="o")
    contact(g, 10, 40, y=feet)
    eyes(g, 10, 24, 26, "Y", "y")
    return g.rows()


CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    ("heath-shepherd", shepherd, 32, (0.7, 2.4, 2), "HeathShepherdFrames"),
    ("heath-black-hare", black_hare, 32, (0.8, 3.0, 3), "HeathBlackHareFrames"),
    ("heath-waller", waller, 32, (0.5, 2.8, 1), "HeathWallerFrames"),
    ("heath-bull", bull, 32, (0.6, 3.4, 1), "HeathBullFrames"),
    ("heath-steward", steward, 32, (0.9, 2.2, 3), "HeathStewardFrames"),
    ("heath-thunder-rod", thunder_rod, 32, (0.3, 1.6, 1), "HeathThunderRodFrames"),
    ("heath-adder", adder, 32, (1.4, 3.6, 3), "HeathAdderFrames"),
    # Faces left (see fleece_bale), so it leans into its strike and is knocked back the other way.
    ("heath-fleece-bale", fleece_bale, 32, (1.1, -2.0, -3), "HeathFleeceBaleFrames"),
    ("heath-fire-beater", fire_beater, 32, (0.6, 4.0, 1), "HeathFireBeaterFrames"),
    ("heath-warrener", warrener, 32, (0.9, 2.6, 2), "HeathWarrenerFrames"),
    ("heath-lawspeaker", lawspeaker, 48, (0.5, 4.0, 1), "HeathLawspeakerFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    # Largest cell first: bestiary_kit.report sizes its contact sheet by the first creature's cell.
    bestiary_kit.run(sorted(CAST, key=lambda c: -c[2]), PALETTE, "heath")
