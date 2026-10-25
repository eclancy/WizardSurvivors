# -*- coding: utf-8 -*-
"""The Emberdeep's own cast (.ai/bestiary.md section 4.8). Run: python tools/art/cast_ember.py

"His seat, and the end of the road." The one chapter whose cast was MADE rather than taken, so the
one where every creature burns: two pixels of fire for a mark where every other chapter's creatures
stare with his cold arcane, and at most a crack or two of fire besides. Only the Deep Warden blazes,
and the restraint here is what lets it. Eleven creatures, every one a new look on the numbers of the
Cursed Dungeon creature it replaces; the module contract is bestiary_kit.py's docstring.

WHAT THEY ARE MADE OF. Iron and stone, and red only as old leather (the bellows, an apron). Nothing
is horned and nothing is a heavy biped with a bridge of shoulders: the Deep Warden is an H.

THE FLOOR DECIDES THE STOPS. The Emberdeep floor is stone's shade (#141A26), so stone's base and
shade - and steel's deep - sit within a few points of the ground and a figure drawn in them is a
silhouette with nothing inside it (art-direction.md section 4c). The bodies here take steel's SHADE
as their mass with steel's base on the key edge, and stone only in its top stop; the deep stops are
for crevices and the line under a plate, never an outline.

CHARACTERS. roster.PALETTE never mapped the cloth red row, so this chapter re-points the ember
PLAYER PIGMENT characters at it, as the Sands module does (cast_sands.py explains why re-pointing a
listed ramp is the safe way: anim_sets steps it through the hurt flash and the death fade):

    Q 1 2 3 4   ember -> cloth red
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
from cast_sands import blob, chain
from foes_forest import _limb

G = roster.G
FEET = 30

PALETTE = dict(roster.PALETTE)
for _ch, _col in zip("Q1234", B.MATERIALS["red"]):
    PALETTE[_ch] = _col


def mark(g, x, y):
    """THE mark, for a thing he built: two pixels of fire, core and hot, side by side."""
    g.set(x, y, "+")
    g.set(x + 1, y, "-")


def crack(g, pts):
    """A crack of fire through iron or slag: the mid stop, an edge stop under it, occ beside.
    One or two per creature and no more - the Deep Warden is the one that blazes."""
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + 1):
            t = i / float(max(1, n))
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            if g.get(x, y) in (".", "W"):
                continue
            g.set(x, y, "=" if i % 3 else "~")
            if g.get(x + 1, y + 1) not in (".", "W", "=", "~"):
                g.set(x + 1, y + 1, "o")


# =================================================================================================
# BULK - Slag Thrall. A thrall he poured from slag, squat and hunched.
# =================================================================================================

def slag_thrall():
    """Squat, lumpy, and cowled in the plates that cooled on top of it.

    The attachment is the CRUST: plates of cooled iron shingled over the head and shoulders and
    sloping down off them like a cowl, each tilted, each with a lit lip and a dark underside, ragged
    where they end. The first pass laid them flat and level and the thrall came out wearing a
    pagoda. Under the crust the body is poured rock - lumps, pits and one seam where it has not
    finished cooling - and it is the lowest upright figure in the chapter, which is what a thrall is.
    """
    g = G(32)
    # THE BODY: poured, lumpy, wider at the bottom where it slumped.
    blob(g, [(15.5, 19.0, 5.0), (15.5, 23.5, 6.4), (16.0, 26.5, 6.6), (12.5, 21.5, 3.6),
             (19.0, 21.0, 3.8)], "P", "R", "p")
    for (x, y, ch) in ((12, 23, "R"), (13, 24, "p"), (18, 25, "R"), (19, 26, "N"), (16, 21, "N"),
                       (21, 22, "p"), (10, 27, "N"), (14, 28, "R"), (20, 28, "p"), (17, 23, "o"),
                       (11, 25, "o"), (15, 26, "N")):
        g.set(x, y, ch)
    roster.fold(g, [(13, 20), (12, 29)], "N", "R")
    roster.fold(g, [(19, 22), (20, 29)], "j", None)
    crack(g, [(14, 22), (16, 24), (15, 27)])
    # arms: thick, slumped, hanging a little away from the body
    for (x0, x1, lit) in ((10, 8, "R"), (21, 23, "p")):
        _limb(g, x0, 19, x1, 26, 1.9, "P", lit, "N")
        g.disc(x1, 27, 1.6, "P")
        g.set(x1 - 1, 26, "R")
        g.set(x1 + 1, 28, "N")
    g.line(11, 20, 10, 25, "o")
    g.line(20, 20, 21, 25, "o")

    # THE CRUST: tilted plates, shingled from the crown down over the shoulders. Each is drawn as
    # a run of short columns stepping down away from the crown, lit along its top edge.
    def shingle(x0, x1, y0, slope, depth):
        for i, x in enumerate(range(x0, x1 + 1) if x1 >= x0 else range(x0, x1 - 1, -1)):
            top = int(round(y0 + slope * i))
            for k in range(depth):
                g.set(x, top + k, "R" if k else "r")
            g.set(x, top + depth, "o")
        g.set(x0, int(round(y0)), "t")

    shingle(16, 24, 15.0, 0.55, 3)        # right shoulder, low
    shingle(15, 7, 15.0, 0.50, 3)         # left shoulder, low
    shingle(17, 23, 11.5, 0.65, 3)        # right of the head
    shingle(14, 9, 11.5, 0.60, 3)         # left of the head
    shingle(15, 20, 8.0, 0.45, 3)         # the crown, two plates overlapping at the ridge
    shingle(16, 11, 8.0, 0.45, 3)
    g.set(15, 7, "r")
    g.set(16, 7, "q")
    roster.cut(g, (24, 20), (7, 19))
    # the face: a dark hollow under the crown's front edge
    g.span(13, 13, 18, "o")
    g.span(14, 14, 18, "o")
    g.span(12, 15, 18, "q")
    roster.contact(g, 8, 24)
    mark(g, 15, 13)
    return g.rows()


# =================================================================================================
# FAST - Chainhound. Also the Kennelmaster's minion.
# =================================================================================================

def chainhound():
    """An iron hound, head low, a broken chain dragging behind it.

    Horizontal like the forest's wolf, and built the way that file says an animal has to be -
    masses first, a deep chest and a tucked waist, legs paired close under each mass and thin, a
    neck that angles down. The first pass was a plank on four posts, which is the table that file
    names. What makes it his is that it is MADE: plate for a hide with seams for ribs, a collar,
    and the chain he kept it on still fixed to the collar, snapped, trailing back over its flank to
    the ground. The trailing chain is the attachment; nothing else in the chapter drags.
    """
    g = G(32)
    mid, lit, dark = "R", "r", "q"
    # LEGS: thin, jointed, paired close under each mass with a wide gap between the pairs.
    for (hx, hy, kx, ky, px, th) in ((10, 21, 8, 25, 9, 1.1), (12, 21, 13, 25, 12, 1.0),
                                     (20, 21, 21, 25, 20, 1.1), (22, 21, 24, 25, 23, 1.0)):
        _limb(g, hx, hy, kx, ky, th, mid, lit, dark)
        _limb(g, kx, ky, px, FEET - 1, th * 0.85, mid, lit, dark)
        g.span(FEET, px - 1, px + 1.5, "q")
        g.set(px - 1, FEET, "r")
    # THE MASSES: a small haunch, a deep chest, the waist tucked up between them.
    g.disc(11.0, 18.5, 3.3, mid)
    g.disc(20.5, 18.0, 3.9, mid)
    g.disc(20.0, 20.5, 3.0, mid)
    for x in range(12, 20):
        t = (x - 12) / 7.0
        top = 16.2 + 0.8 * (1.0 - abs(t - 0.5) * 2.0)
        bot = 19.5 + 1.6 * t
        for y in range(int(round(top)), int(round(bot)) + 1):
            g.set(x, y, mid)
    for x in range(7, 25):
        col = [y for y in range(10, 26) if g.get(x, y) == mid]
        if col:
            g.set(x, col[0], lit)
            g.set(x, col[-1], dark)
    # seams in the plate: the shoulder plate and the haunch plate, and two ribs between
    g.line(17, 15, 16, 20, "o")
    g.line(14, 16, 13, 20, "q")
    g.set(15, 17, "q")
    g.line(18, 16, 22, 15, "t")
    g.set(10, 16, "t")
    # NECK, angled down and forward
    for i, x in enumerate(range(23, 27)):
        top = 15.5 + i * 0.8
        for y in range(int(top), int(top) + 4):
            g.set(x, y, mid)
        g.set(x, int(top), lit)
        g.set(x, int(top) + 4, dark)
    # HEAD: a long iron muzzle, the jaw a plate under it
    g.rect(26, 18, 28, 21, mid)
    g.span(18, 26, 28, lit)
    for x in (29, 30):
        g.set(x, 19, mid)
        g.set(x, 20, mid)
        if x == 29:
            g.set(x, 19, lit)
    g.set(30, 21, dark)
    g.span(22, 26, 29, "q")
    g.span(23, 27, 28, "o")
    g.set(29, 22, "t")
    # ears: two spikes swept back
    g.line(25, 16, 24, 14, "q")
    g.line(27, 17, 27, 15, "R")
    g.set(24, 14, "r")
    # THE COLLAR, and the snapped chain trailing back over the flank to the ground.
    g.line(23, 16, 24, 21, "t")
    g.line(24, 16, 25, 21, "q")
    chain(g, [(23, 17), (18, 15), (12, 15), (8, 17), (6, 21), (5, 26), (4, 29)], "r", "q")
    g.set(5, 29, "q")
    g.set(5, 30, "r")
    g.set(3, 30, "q")
    roster.contact(g, 7, 25)
    mark(g, 27, 19)
    return g.rows()


# =================================================================================================
# HEAVY - Iron Bulwark. A squat engine.
# =================================================================================================

def bulwark():
    """A squat engine pushing a riveted plate before it.

    The shielder's flat plane, as a machine would carry it: a tall slab of plate at the front,
    gripped at its edge, with the engine behind it short and broad on piston legs. Rivets run round
    the plate's BORDER at uneven spacing with one dent across it, because a plate studded all over
    is the radiator the roster notes warn about. The mark looks out of a slot in the engine, beside
    the plate rather than over it.
    """
    g = G(32)
    # the exhaust stack, at the back
    g.rect(8, 7, 10, 13, "R")
    g.line(8, 7, 8, 13, "r")
    g.line(10, 7, 10, 13, "q")
    g.span(6, 7, 11, "q")
    g.span(5, 7, 11, "R")
    g.set(7, 5, "r")
    # THE ENGINE: broad, short, a dome on top.
    blob(g, [(13.5, 17.0, 5.6), (13.5, 21.5, 6.0), (13.0, 24.5, 5.6)], "R", "r", "q")
    SB.ridges(g, 15, 26, "R", "q", "r", step=4, jitter=5)
    roster.fold(g, [(9, 18), (9, 27)], "q", "r")
    # the slot with the mark in it
    g.span(16, 11, 15, "o")
    g.span(17, 11, 14, "o")
    # piston legs
    for (x, ch) in ((10, "r"), (16, "R")):
        g.rect(x, 26, x + 2, FEET, "R")
        g.line(x, 26, x, FEET, ch)
        g.line(x + 2, 26, x + 2, FEET, "q")
        g.span(28, x, x + 2, "q")
        g.span(FEET, x - 1, x + 3, "q")
        g.set(x - 1, FEET, "r")
    # the arm gripping the plate's edge
    g.rect(16, 18, 19, 20, "R")
    g.line(16, 18, 19, 18, "r")
    g.line(16, 21, 19, 21, "o")
    # THE PLATE: tall, flat, held before it, straight edges against the round engine.
    g.rect(19, 7, 26, 27, "R")
    g.line(19, 7, 26, 7, "t")
    g.line(19, 8, 19, 27, "r")
    g.line(20, 8, 20, 27, "r")
    g.line(26, 8, 26, 27, "q")
    g.line(19, 27, 26, 27, "q")
    g.line(18, 8, 18, 27, "o")
    g.span(28, 19, 26, "o")
    SB.rivets(g, [(21, 9), (24, 10), (22, 15), (25, 17), (21, 22), (24, 24)], "t", "q")
    g.line(22, 11, 25, 13, "q")              # the dent
    g.set(23, 11, "r")
    g.line(21, 19, 24, 20, "q")
    roster.contact(g, 8, 27)
    mark(g, 12, 16)
    return g.rows()


# =================================================================================================
# WALL - Kiln Brute. A kiln that walks.
# =================================================================================================

def kiln_brute():
    """Broad as a kiln, two flue pipes rising off its shoulders, a shut furnace door for a belly.

    The bruiser's job - a wall you read from across the arena - done without horns or pauldrons:
    the shoulder line is broken by two PIPES of different heights, and the mass is a dome of laid
    brick, courses staggered so it is masonry and not a grid. The door in its belly is shut, and the
    one thing that burns besides its eyes is the seam of light under that door.
    """
    g = G(32)
    # THE FLUES, behind the dome's shoulders: one tall, one short, each capped.
    for (x, top) in ((8, 3), (21, 6)):
        g.rect(x, top + 1, x + 2, 15, "R")
        g.line(x, top + 1, x, 15, "r")
        g.line(x + 2, top + 1, x + 2, 15, "q")
        g.span(top, x - 1, x + 3, "r")
        g.span(top + 1, x - 1, x + 3, "q")
        g.set(x - 1, top, "t")
        g.span(top + 6, x, x + 2, "q")
    # THE DOME of brick
    SB.swell(g, 9, 27, [5.5, 8.4, 9.6, 10.0, 10.0, 9.4, 8.6], "P", "P", "p", cx=15.5)
    SB.courses(g, 10, 27, "P", "N", "R", high=3, wide=5)
    g.span(9, 11, 20, "R")
    # stubby arms of brick, hanging at the sides
    for (x0, x1) in ((6, 4), (25, 27)):
        _limb(g, x0, 15, x1, 22, 1.6, "P", "R", "N")
        g.disc(x1, 23, 1.4, "p")
        g.set(x1 - 1, 22, "R")
    g.line(7, 15, 7, 21, "o")
    g.line(24, 15, 24, 21, "o")
    # THE DOOR: iron, shut, hinged left, latched right, the seam under it alight.
    g.rect(12, 16, 19, 23, "R")
    g.line(12, 16, 19, 16, "r")
    g.line(12, 16, 12, 23, "r")
    g.line(19, 16, 19, 23, "q")
    g.span(19, 13, 18, "q")
    g.set(11, 17, "q")
    g.set(11, 21, "q")
    g.set(18, 20, "t")
    g.set(18, 21, "q")
    g.span(24, 12, 19, "~")
    g.span(24, 14, 17, "=")
    g.set(15, 24, "-")
    g.span(25, 12, 19, "o")
    # the head: a stub of chimney cap between the flues, a slit for the mark
    g.rect(12, 6, 18, 9, "R")
    g.line(12, 6, 18, 6, "r")
    g.line(18, 6, 18, 9, "q")
    g.span(8, 13, 17, "o")
    # legs: short brick stumps, wide apart
    for (x, ch) in ((9, "R"), (18, "p")):
        g.rect(x, 26, x + 3, FEET, "P")
        g.line(x, 26, x, FEET, ch)
        g.line(x + 3, 26, x + 3, FEET, "N")
        g.span(FEET, x - 1, x + 4, "N")
    g.rect(13, 27, 17, FEET, "o")
    roster.contact(g, 8, 23)
    mark(g, 14, 8)
    return g.rows()


# =================================================================================================
# RANGED - The Kindler. A thin iron figure that throws coals.
# =================================================================================================

def kindler():
    """Thin as a fire-iron, a snuffer for a head, and the coal it throws floating at its shoulder.

    The caster's detached orb is a COAL, crusted dark with fire in its cracks, with daylight between
    it and the tongs reaching up for it. The body is mostly holes - a cage of iron bars for a chest
    on two thin legs - so filled black it is the one see-through figure in the chapter, under a
    tall cone. The coal is the colour of what it throws, so its bolt is fire.
    """
    g = G(32)
    # LEGS: two thin irons, a step apart
    for (hx, fx, ch) in ((14, 12, "r"), (17, 19, "R")):
        g.line(hx, 21, fx, FEET - 1, ch)
        g.line(hx + 1, 21, fx + 1, FEET - 1, "q")
        g.span(FEET, fx - 1, fx + 2, "q")
        g.set(fx - 1, FEET, "r")
    # THE CAGE: a chest of bars, open between them
    for (x, ch) in ((12, "r"), (14, "R"), (16, "R"), (18, "q")):
        g.line(x, 12, x + (0 if x < 15 else 0), 20, ch)
    for (y, a, b) in ((12, 12, 18), (16, 12, 18), (20, 13, 17)):
        g.span(y, a, b, "R")
        g.set(a, y, "r")
        g.set(b, y, "q")
    g.span(21, 13, 18, "q")
    g.set(15, 18, "~")                        # an ember caught in the cage
    # THE SNUFFER: a tall cone for a head, the mark under its rim
    for i, y in enumerate(range(3, 11)):
        hw = 0.6 + i * 0.55
        g.span(y, 15 - hw, 15 + hw, "R")
        g.set(15 - hw, y, "r")
        g.set(15 + hw, y, "q")
    g.set(15, 2, "r")
    g.span(7, 12, 17, "q")
    g.set(13, 7, "t")
    g.line(14, 4, 13, 9, "q")
    g.set(16, 8, "q")
    g.span(10, 10, 20, "r")
    g.span(11, 11, 19, "o")
    g.set(10, 10, "t")
    for (x, y) in ((13, 25), (18, 25)):
        g.set(x, y, "t")
        g.set(x + 1, y, "o")
    # the hanging arm, tongs at its end
    g.line(12, 13, 9, 20, "R")
    g.line(11, 13, 8, 20, "r")
    g.set(8, 21, "q")
    g.set(10, 21, "q")
    g.set(8, 22, "R")
    g.set(10, 22, "R")
    # THE REACHING ARM, up toward the coal, ending in open tongs
    g.line(18, 13, 21, 9, "R")
    g.line(19, 13, 22, 9, "q")
    g.line(21, 8, 21, 6, "r")
    g.line(23, 8, 23, 7, "q")
    # THE COAL, with nothing joining it to the tongs
    g.disc(26, 5, 2.5, "~")
    g.disc(26, 5, 1.7, "=")
    g.set(26, 5, "-")
    g.set(25, 4, "+")
    for (x, y) in ((27, 3), (28, 5), (24, 6), (27, 7)):
        g.set(x, y, "q")
    roster.contact(g, 10, 21)
    mark(g, 14, 11)
    return g.rows()


# =================================================================================================
# TURRET - Flue Mask. A mask on a chimney stack, venting fire.
# =================================================================================================

def flue_mask():
    """The tallest thing in the chapter: a chimney stack with an iron face fixed to it.

    The stack is laid brick on a broader plinth; the mask is wider than the stack, so it stands out
    of the column as the one feature filled black; and the flue at the top vents a short tongue of
    fire, which is where the bolts come from. Built, as every turret is - and here as everything is.
    """
    g = G(32)
    # THE PLINTH and THE STACK
    g.rect(8, 26, 23, 29, "P")
    g.line(8, 26, 23, 26, "R")
    g.line(23, 26, 23, 29, "p")
    g.set(7, 27, "W")
    SB.swell(g, 6, 26, [4.2, 4.4, 4.6, 4.8, 5.2, 5.6], "P", "P", "p", cx=15.5)
    SB.courses(g, 7, 28, "P", "N", "R", high=3, wide=4)
    # the cap and the flue's mouth
    g.span(5, 10, 21, "r")
    g.span(6, 10, 21, "q")
    g.set(10, 5, "t")
    g.span(4, 12, 19, "o")
    # the fire venting from the flue, short
    for (x, y, ch) in ((14, 3, "~"), (15, 3, "="), (16, 3, "="), (17, 3, "~"), (15, 2, "-"),
                       (16, 2, "~")):
        g.set(x, y, ch)
    # THE MASK: wider than the stack, a brow, two holes and an open mouth
    blob(g, [(15.5, 14.0, 4.6), (15.5, 17.5, 4.0)], "R", "r", "q")
    g.span(11, 10, 21, "r")
    g.set(10, 11, "t")
    g.span(12, 11, 20, "q")
    g.rect(12, 13, 13, 14, "o")
    g.rect(17, 13, 18, 14, "o")
    g.line(15, 13, 15, 16, "q")
    g.set(16, 14, "r")
    g.span(18, 13, 18, "o")
    g.span(19, 14, 17, "o")
    g.set(15, 19, "~")
    g.span(20, 13, 18, "q")
    # straps that fix it to the stack
    g.line(9, 15, 11, 16, "q")
    g.line(20, 16, 22, 15, "q")
    roster.contact(g, 7, 24)
    mark(g, 12, 14)
    g.set(17, 14, "+")
    g.set(18, 14, "-")
    g.set(13, 14, "o")
    return g.rows()


# =================================================================================================
# CHARGER - Ironjaw. A forged beast that is mostly jaw.
# =================================================================================================

def ironjaw():
    """Coiled low behind a jaw half the size of the whole beast, open.

    The pose is the telegraph, as the crypt lunger's is: everything gathered, the haunches folded
    under, and the jaw - a hinged wedge of iron with a row of teeth on each half - already open.
    Low and wedge-shaped where the Chainhound is long and lean, so the two dogs of the chapter never
    share a silhouette.
    """
    g = G(32)
    # HAUNCHES, folded under, at the back
    blob(g, [(10.5, 23.0, 4.6), (12.0, 26.0, 4.2), (14.5, 21.0, 4.2)], "R", "r", "q")
    SB.ridges(g, 19, 28, "R", "q", "r", step=3, jitter=4)
    # the folded hind leg and the forelegs under the hinge
    g.line(7, 27, 12, 29, "q")
    g.span(FEET, 7, 13, "q")
    g.set(7, FEET, "r")
    for (x, ch) in ((17, "r"), (20, "R")):
        g.rect(x, 24, x + 1, FEET - 1, "R")
        g.line(x, 24, x, FEET - 1, ch)
        g.span(FEET, x - 1, x + 2, "q")
    # THE LOWER JAW, slung forward
    for i, x in enumerate(range(15, 29)):
        t = i / 13.0
        top = 21.0 - 1.5 * t
        bot = 24.0 - 1.5 * t
        for y in range(int(round(top)), int(round(bot)) + 1):
            g.set(x, y, "R")
        g.set(x, round(top), "r")
        g.set(x, round(bot), "q")
    # THE UPPER JAW: the biggest mass on it, a wedge thrust forward and up
    for i, x in enumerate(range(13, 30)):
        t = i / 16.0
        top = 11.0 + 2.0 * t * t
        bot = 17.0 - 1.0 * t
        for y in range(int(round(top)), int(round(bot)) + 1):
            g.set(x, y, "R")
        g.set(x, round(top), "r")
        if i < 8:
            g.set(x, round(top) + 1, "r")
        g.set(x, round(bot), "q")
    SB.ridges(g, 12, 16, "R", "q", "r", step=3, jitter=7)
    SB.rivets(g, [(20, 12), (24, 13), (27, 14), (19, 22), (24, 21)], "t", "q")
    g.line(11, 19, 9, 26, "o")
    g.set(10, 20, "t")
    # the mouth between, and the teeth
    for x in range(16, 29):
        for y in range(18, 21):
            if g.get(x, y) in (".", "R", "r", "q"):
                g.set(x, y, "o")
    for x in range(17, 29, 3):
        g.set(x, 18, "t")
        g.set(x + 1, 20, "r")
    g.set(28, 18, "t")
    # THE HINGE: a great bolt where the jaws meet
    g.disc(15, 18, 2.2, "R")
    g.set(14, 17, "t")
    g.set(16, 19, "q")
    g.set(15, 18, "o")
    roster.contact(g, 6, 27)
    mark(g, 17, 13)
    return g.rows()


# =================================================================================================
# EXPLODER - Burst Bellows. Faces LEFT: the stand-in sets SpriteFacesRightByDefault = false.
# =================================================================================================

def bellows():
    """A forge bellows on legs, its leather swelling at every pleat. The nozzle leads, to the left.

    The pleats are the attachment: leather folds fanning out from the nozzle between two iron-shod
    boards, each fold a lit ridge over a dark crease, bowed out past the boards because the thing is
    full and still filling, and zig-zagging where the leather closes the open end. The first pass ran
    its folds vertically and came out a red barrel. Red, old leather, the one body in the chapter
    that is not iron or stone; the mark looks out of a split between two pleats.
    """
    g = G(32)
    # THE LEATHER, swollen, between the boards
    blob(g, [(15.0, 17.0, 6.0), (20.0, 17.0, 6.6), (23.5, 17.0, 5.2), (10.5, 17.0, 3.8)],
         "2", "1", "3", rim=False)
    # pleats fanning from the nozzle, each a ridge (lit) over a crease (dark)
    for (x1, y1) in ((26, 11), (27, 14), (27, 17), (27, 20), (26, 23)):
        roster.fold(g, [(10, 17), (x1, y1)], "4", "Q", side=(0, -1))
    # the zig-zag where the leather closes the open end
    for (x, y) in ((27, 12), (28, 13), (27, 15), (28, 16), (27, 18), (28, 19), (27, 21), (28, 22)):
        g.set(x, y, "1" if x == 28 else "3")
    # THE BOARDS: iron-shod planks, two pixels thick, meeting at the nozzle
    for i, x in enumerate(range(8, 26)):
        t = i / 17.0
        yt = 15 - 6.0 * t
        yb = 18 + 7.0 * t
        g.set(x, yt - 1, "r")
        g.set(x, yt, "R")
        g.set(x, yt + 1, "q")
        if g.get(x, yt - 2) == ".":
            g.set(x, yt - 2, "W")
        g.set(x, yb, "R")
        g.set(x, yb + 1, "q")
        if i % 4 == 0:
            g.set(x, yb - 1, "r")
    SB.rivets(g, [(11, 13), (17, 11), (23, 9), (13, 20), (20, 23)], "t", "q")
    # the handle, off the top board's end
    g.line(25, 8, 28, 5, "R")
    g.line(26, 8, 28, 6, "q")
    g.set(28, 4, "r")
    # THE NOZZLE, leading
    g.rect(4, 16, 8, 17, "R")
    g.line(4, 16, 8, 16, "r")
    g.set(4, 17, "q")
    g.set(3, 16, "q")
    g.set(9, 16, "q")
    g.set(9, 17, "o")
    # a split between two pleats with the mark in it, and one seam glowing where it gives
    g.span(16, 12, 15, "o")
    g.span(17, 13, 14, "o")
    crack(g, [(22, 12), (24, 14)])
    # legs: stubby iron, under the bottom board
    for (x, fx) in ((12, 11), (19, 20)):
        g.line(x, 22, fx, FEET - 1, "R")
        g.line(x + 1, 22, fx + 1, FEET - 1, "q")
        g.span(FEET, fx - 1, fx + 2, "q")
        g.set(fx - 1, FEET, "r")
    roster.contact(g, 7, 24)
    mark(g, 13, 16)
    return g.rows()


# =================================================================================================
# SLAMMER - Trip-Hammer. A forge hammer that walks.
# =================================================================================================

def trip_hammer():
    """A squat housing on two legs, and one great hammer-arm raised over it.

    Asymmetric on purpose: the slammer's rule is that the thing that will land is the highest thing
    in the cell, and here it is a block of iron on a heavy beam pivoting off one shoulder, with the
    cam wheel that trips it on the housing's flank. The Kiln Brute is broad and brick; this is iron
    and lopsided. The first pass hung a small head on a one-pixel beam over a small housing and
    read as a lamp post; the head and the beam carry the weight now.
    """
    g = G(32)
    # legs
    for (x, ch) in ((8, "r"), (15, "R")):
        g.rect(x, 25, x + 3, FEET, "R")
        g.line(x, 25, x, FEET, ch)
        g.line(x + 3, 25, x + 3, FEET, "q")
        g.span(27, x, x + 3, "q")
        g.span(FEET, x - 1, x + 4, "q")
        g.set(x - 1, FEET, "r")
    # THE HOUSING: a broad iron box, rounded on top, ridged
    blob(g, [(13.0, 16.5, 5.5), (13.0, 20.5, 6.0)], "R", "r", "q")
    g.rect(7, 18, 19, 25, "R")
    g.line(7, 18, 7, 25, "r")
    g.line(19, 18, 19, 25, "q")
    g.span(25, 7, 19, "q")
    g.span(26, 8, 19, "o")
    g.set(6, 19, "W")
    SB.ridges(g, 12, 24, "R", "q", "r", step=4, jitter=6)
    # the cam wheel on its flank, toothed, an ember caught in its teeth
    g.disc(11.5, 21.0, 2.8, "q")
    g.disc(11.5, 21.0, 1.8, "R")
    g.set(11, 20, "r")
    g.set(12, 21, "o")
    for (x, y) in ((9, 18), (14, 19), (9, 23), (13, 24), (14, 22)):
        g.set(x, y, "r")
    g.set(8, 20, "~")
    # the slot with the mark
    g.span(15, 13, 17, "o")
    # the shadow under the head, drawn BEFORE the beam: drawn after, it cut the beam where it enters
    # the head and the head floated free of it at game scale
    g.span(9, 20, 29, "o")
    # THE BEAM: from the pivot on the shoulder up and over into the head, four pixels thick
    for i in range(15):
        t = i / 14.0
        x, y = 17.0 + 6.5 * t, 13.0 - 7.5 * t
        g.set(x - 1, y, "r")
        g.set(x, y, "r")
        g.set(x + 1, y, "R")
        g.set(x + 2, y, "q")
    g.disc(17, 13, 2.0, "R")
    g.set(16, 12, "t")
    g.set(17, 13, "o")
    # the short counter-arm, behind
    g.line(16, 13, 10, 11, "R")
    g.line(16, 14, 10, 12, "q")
    g.set(9, 11, "r")
    g.set(9, 12, "q")
    # THE HAMMER HEAD: the highest and heaviest thing in the cell
    g.rect(19, 2, 30, 8, "R")
    g.line(19, 2, 30, 2, "r")
    g.line(19, 2, 19, 8, "r")
    g.line(20, 3, 20, 7, "r")
    g.line(30, 3, 30, 8, "q")
    g.line(19, 8, 30, 8, "q")
    g.set(19, 2, "t")
    g.line(24, 3, 24, 7, "q")
    g.line(25, 3, 25, 7, "t")
    g.set(28, 4, "t")
    g.set(28, 5, "q")
    g.set(22, 6, "q")
    roster.contact(g, 7, 20)
    mark(g, 14, 15)
    return g.rows()


# =================================================================================================
# SUMMONER - Kennelmaster. Calls the Chainhounds.
# =================================================================================================

def kennelmaster():
    """The keeper of his hounds, holding up a ring of leashes at hand height.

    The ring is the attachment: an iron hoop held out in front of the body with three chains
    hanging from it, each ending in an open collar - the hounds it calls are off them. An iron
    figure under a round kettle helm with a short sloping brim, in an old red leather apron, where
    the Kindler is bars and a cone and the Trip-Hammer is a machine. (A flat crown and a flat wide
    brim made the first pass a gunslinger.)
    """
    g = G(32)
    roster.mass(g, [(11, 12, 18), (14, 10, 20), (22, 10, 20), (29, 9, 21)], "R", "r", "q")
    # THE APRON: red leather down the front, folds, a strap
    for y in range(15, 29):
        a = 12 + (y - 15) * 0.07
        g.span(y, a, 18, "2")
        g.set(a, y, "1")
        g.set(18, y, "3")
    roster.fold(g, [(15, 17), (14, 28)], "3", "1")
    roster.fold(g, [(17, 20), (17, 28)], "4", None)
    g.span(15, 12, 18, "4")
    g.span(14, 11, 19, "q")
    roster.cut(g, (15, 29))
    SB.rivets(g, [(11, 15), (19, 16), (10, 24), (20, 25)], "t", "q")
    g.line(12, 24, 18, 22, "3")
    g.set(15, 23, "u")
    # a mail collar over the shoulders
    for x in range(11, 20):
        g.set(x, 13, "r" if x % 2 else "q")
    # THE HELM: a round bowl, the brim short and sloping down at the sides
    blob(g, [(15.0, 7.5, 3.6)], "R", "r", "q")
    g.span(4, 14, 16, "r")
    g.set(13, 5, "t")
    g.span(9, 10, 20, "r")
    g.span(10, 10, 20, "q")
    g.set(9, 10, "r")
    g.set(21, 10, "q")
    g.set(9, 11, "q")
    g.set(21, 11, "q")
    g.line(15, 5, 15, 8, "q")                   # the bowl's seam
    g.span(11, 12, 18, "o")
    g.span(12, 13, 17, "o")
    # the hanging arm
    g.line(11, 14, 9, 21, "R")
    g.line(12, 14, 10, 21, "q")
    g.rect(8, 21, 10, 23, "R")
    g.set(8, 21, "r")
    g.line(11, 15, 11, 20, "o")
    # THE ARM holding out the ring
    g.line(19, 15, 22, 14, "R")
    g.line(19, 16, 22, 15, "q")
    g.rect(21, 13, 22, 15, "r")
    # THE RING, and the leashes hanging off it
    for (dx, dy) in ((0, -3), (1, -3), (2, -2), (3, -1), (3, 0), (3, 1), (2, 2), (1, 3), (0, 3),
                     (-1, 3), (-2, 2), (-3, 1), (-3, 0), (-3, -1), (-2, -2), (-1, -3)):
        g.set(25 + dx, 13 + dy, "r" if dy < 0 or dx < 0 else "q")
    g.set(24, 10, "t")
    for (x, y1) in ((23, 22), (26, 24), (28, 20)):
        chain(g, [(x, 16), (x, y1)], "r", "q")
        g.set(x - 1, y1 + 1, "q")
        g.set(x + 1, y1 + 1, "q")
        g.set(x, y1 + 2, "R")
    roster.contact(g, 8, 22)
    mark(g, 14, 11)
    return g.rows()


# =================================================================================================
# MINIBOSS - The Chainwright. 48 x 48. His jailer in his own seat.
# =================================================================================================

def chainwright():
    """The forge every shackle in the land came from, walking, and one arm of it a chain-hoist.

    Filled black: a hunched forge on two short legs, its head a chimney hood, with a lattice jib
    rising off one shoulder and reaching out over the head, a pulley at its end and chains hanging
    from it to a hook with a shackle on it. Lopsided and top-heavy toward the jib, where the Warden
    is a plated slab with chains at its wrists and the Deep Warden is an H with horns; it carries
    neither horns nor a cell door. The forge in its chest is shut - an iron hatch with a seam of
    light under it - because only the Deep Warden blazes.

    The first pass gave it a box for a head and a one-pixel jib, and read as a knight with a
    fishing rod. The jib is a girder now, two rails with the lattice between, and the head is the
    hood of the forge it is.
    """
    g = G(48)
    F = 46
    # LEGS, short, thick, a stride apart
    for (lx0, lx1, key) in ((10, 16, "r"), (21, 27, "R")):
        g.rect(lx0, 37, lx1, F, "R")
        g.line(lx0, 37, lx0, F, key)
        g.line(lx1, 37, lx1, F, "q")
        g.span(41, lx0, lx1, "q")
        g.span(F, lx0 - 1, lx1 + 2, "q")
        g.span(F - 1, lx0 - 1, lx1 + 2, "R")
        g.set(lx0 - 1, F - 1, "r")
    g.rect(17, 38, 20, F, "o")
    for (lx0, lx1) in ((10, 16), (21, 27)):
        g.rect(lx0, 39, lx1, 40, "r")
        g.line(lx0, 41, lx1, 41, "o")
        g.set(lx0 + 2, 39, "t")
        g.set(lx1 - 1, 43, "q")
        g.set(lx0 + 1, 44, "t")
    # THE FORGE: a hunched mass, broad, leaning toward the jib
    blob(g, [(18.0, 23.0, 9.5), (19.0, 29.0, 10.0), (18.5, 34.0, 9.0), (23.0, 19.0, 7.0)],
         "R", "r", "q")
    SB.ridges(g, 15, 38, "R", "q", "r", step=4, jitter=5)
    roster.fold(g, [(11, 21), (10, 37)], "q", "r")
    # the hatch over the forge's mouth, shut, its seam alight
    g.rect(14, 25, 24, 32, "R")
    g.line(14, 25, 24, 25, "r")
    g.line(14, 25, 14, 32, "r")
    g.line(24, 25, 24, 32, "q")
    g.line(15, 26, 23, 26, "t")
    for x in (17, 21):
        g.line(x, 27, x, 31, "q")
    g.span(33, 14, 24, "~")
    g.span(33, 16, 22, "=")
    g.set(19, 33, "-")
    g.span(34, 14, 24, "o")
    SB.rivets(g, [(15, 27), (23, 27), (15, 31), (23, 31)], "t", "q")
    # shackles hung at its belt: open, as everything it made is
    for (x, y) in ((11, 35), (26, 36)):
        chain(g, [(x, y), (x, y + 4)], "r", "q")
        for (ox, oy) in ((-1, 5), (-1, 6), (0, 7), (1, 6), (1, 5)):
            g.set(x + ox, y + oy, "r" if ox < 0 else "q")
    # THE HEAD: a forge's chimney hood, sunk between the shoulders, stepping up to a short stack
    for i, y in enumerate(range(8, 16)):
        hw = 2.0 + i * 0.75
        g.span(y, 17 - hw, 17 + hw, "R")
        g.set(17 - hw, y, "r")
        g.set(17 + hw, y, "q")
        if g.get(17 - hw - 1, y) == ".":
            g.set(17 - hw - 1, y, "W")
    g.rect(15, 4, 19, 7, "R")
    g.line(15, 4, 15, 7, "r")
    g.line(19, 4, 19, 7, "q")
    g.span(3, 14, 20, "r")
    g.span(4, 14, 20, "q")
    g.set(14, 3, "t")
    g.span(10, 13, 21, "q")
    g.span(12, 13, 21, "o")
    g.span(13, 12, 22, "o")
    g.span(16, 11, 23, "o")
    SB.rivets(g, [(13, 10), (20, 10), (12, 14), (22, 14)], "t", "q")
    # the far arm: heavy, hanging, a fist like an anvil's horn
    g.rect(6, 20, 9, 31, "R")
    g.line(6, 20, 6, 31, "r")
    g.line(9, 20, 9, 31, "q")
    for y in (24, 28):
        g.span(y, 6, 9, "q")
    g.rect(5, 31, 10, 35, "R")
    g.line(5, 31, 10, 31, "r")
    g.line(5, 36, 10, 36, "o")
    g.set(4, 33, "R")
    g.line(10, 20, 10, 30, "o")
    # THE JIB: a girder off the near shoulder, rising and reaching out over the head
    for i in range(20):
        t = i / 19.0
        x, y = 27.0 + 13.0 * t, 18.0 - 13.0 * t
        g.set(x, y - 1, "r")
        g.set(x + 1, y - 1, "r")
        g.set(x + 1, y + 2, "q")
        g.set(x + 2, y + 2, "q")
        g.set(x + 1, y, "R" if i % 3 else "o")
        g.set(x + 1, y + 1, "o" if i % 3 else "R")
    g.rect(25, 16, 30, 20, "R")                    # the housing at its foot
    g.line(25, 16, 30, 16, "r")
    g.set(27, 18, "o")
    g.set(28, 18, "t")
    # the pulley at its end
    g.disc(41.5, 4.5, 2.6, "R")
    g.set(40, 3, "r")
    g.set(41, 2, "r")
    g.set(43, 6, "q")
    g.set(41, 4, "o")
    # THE CHAINS from the pulley to the hook, and a shackle on the hook
    chain(g, [(43, 7), (43, 27)], "r", "q")
    chain(g, [(40, 7), (40, 22)], "q", "R")
    g.set(39, 23, "q")
    g.set(41, 23, "q")
    g.set(40, 24, "R")
    for (ox, oy) in ((0, 28), (1, 29), (1, 30), (0, 31), (-1, 31), (-2, 30), (-2, 29)):
        g.set(43 + ox, oy, "r" if ox >= 0 else "q")
    g.set(44, 29, "q")
    for (ox, oy) in ((-2, 32), (-3, 33), (-3, 34), (-2, 35), (-1, 34), (-1, 33)):
        g.set(43 + ox, oy, "r" if oy < 34 else "q")
    roster.contact(g, 8, 31, y=F)
    mark(g, 16, 13)
    return g.rows()


# =================================================================================================

CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    #
    # THE MINIBOSS IS LISTED FIRST: bestiary_kit.report sizes its contact sheet from the first
    # creature, and a 32 cell first raises on the 48-cell jailer (see cast_sands.py).
    ("ember-chainwright", chainwright, 48, (0.5, 4.2, 1), "EmberChainwrightFrames"),
    ("ember-slag-thrall", slag_thrall, 32, (0.6, 2.6, 2), "EmberSlagThrallFrames"),
    ("ember-chainhound", chainhound, 32, (1.8, 3.6, 2), "EmberChainhoundFrames"),
    ("ember-bulwark", bulwark, 32, (0.5, 2.8, 1), "EmberBulwarkFrames"),
    ("ember-kiln-brute", kiln_brute, 32, (0.5, 3.6, 1), "EmberKilnBruteFrames"),
    ("ember-kindler", kindler, 32, (0.9, 2.2, 3), "EmberKindlerFrames"),
    ("ember-flue-mask", flue_mask, 32, (0.15, -2.0, 1), "EmberFlueMaskFrames"),
    ("ember-ironjaw", ironjaw, 32, (1.2, 4.6, 2), "EmberIronjawFrames"),
    ("ember-bellows", bellows, 32, (1.0, -2.2, -3), "EmberBellowsFrames"),
    ("ember-trip-hammer", trip_hammer, 32, (0.5, 4.4, 1), "EmberTripHammerFrames"),
    ("ember-kennelmaster", kennelmaster, 32, (0.9, 2.6, 2), "EmberKennelmasterFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    bestiary_kit.run(CAST, PALETTE, "ember")
