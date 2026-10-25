# -*- coding: utf-8 -*-
"""The Enchanted Forest's own cast (.ai/bestiary.md section 4.1). Run: python tools/art/cast_forest.py

The wood itself, taken: its beasts, its spirits, and the woodsmen who worked it. The dire wolf, the
bramble and the wisp were drawn first (foes_forest.py) and are this chapter's reference; these are
the other nine, one per job the spawner has, each on the stat block of the crypt creature it
replaces in the forest.

WHAT EVERY ONE OF THEM KEEPS, from .ai/world-and-tone.md rather than from taste:

  The deep half of its ramps. Linen is the fur and foliage row - a grey-green, which is what a wood
  looks like under no light - stone is its wood, lichen its moss. Nothing here reaches the bright
  end of a ramp except the one bone feature (antlers, claws, a horn) that a silhouette is read by.

  ONE MARK. Two pixels of cold arcane in every face, the same two the crypt's dead carry, because
  the wood did not join him - he took it. The one exception is the effigy, which he BUILT, so it
  burns: fire eyes in a wicker head, and nothing else in the chapter is allowed fire.

  ONE ATTACHMENT EACH, and no two alike within the wood (section 2): the wolf is a horizontal bar,
  the bramble a lace with holes in it, the wisp a pair of wings. To those nine more are added - a
  hooked leaf over a face, a trailing streamer of moss, a crown of dead twigs with a seed floating
  beside it, a cross of sticks wider than anything else here, an antler rack, the only bulging
  shape in the wood, forepaws raised above everything, a curled horn held up, and a cage on a pole
  rising past a head. Filled solid black, each one is still nameable.

THREE PALETTE CHARACTERS ARE REDEFINED, and this is why it is safe. The roster palette spends every
printable character, and has no lichen row, no red cloth row and no grass ramp. The four PLAYER
PIGMENT rows (ember, azure, amber, verdant) are reserved to the player cast, so no enemy may ever
wear them - which leaves their characters free to MEAN something else in an enemy palette. Here the
azure characters are lichen and the ember characters are red cloth, both five-stop material rows,
and the lightning characters are the grass element ramp. anim_sets steps a pixel along the ramp its
CHARACTER belongs to, lightest to darkest, so a hurt flash or a death fade still walks each of these
along its own row in the right order: the only thing that changed is which colours the steps are.
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

G = roster.G
mass = roster.mass
fold = roster.fold
cut = roster.cut
eyes = roster.eyes
contact = roster.contact

PALETTE = dict(roster.PALETTE)
# Lichen in the azure characters, red cloth in the ember characters (hi, lit, base, shade, deep).
for _ch, _hex in zip("U5678", B.MATERIALS["lichen"]):
    PALETTE[_ch] = _hex
for _ch, _hex in zip("Q1234", B.MATERIALS["red"]):
    PALETTE[_ch] = _hex
# Grass in the lightning characters (core, hot, mid, edge): the chapter's bolt element, so the
# dryad's seed pod is the colour of what it throws (.ai/bestiary.md section 6).
for _ch, _hex in zip("[];:", B.ELEMENTS["grass"]):
    PALETTE[_ch] = _hex

# Readable names for the redefined characters.
MOSS_HI, MOSS_LIT, MOSS, MOSS_SH, MOSS_DP = "U", "5", "6", "7", "8"
RED_HI, RED_LIT, RED, RED_SH, RED_DP = "Q", "1", "2", "3", "4"
SEED_CORE, SEED_HOT, SEED_MID, SEED_EDGE = "[", "]", ";", ":"

CELL = 32
FEET = CELL - 2


# --- local helpers ------------------------------------------------------------------------------

def inside(g, x, y):
    return g.get(x, y) not in (".", "W")


def paint(g, pts, ch, only_inside=True):
    """Recolour pixels, never past the silhouette when only_inside."""
    for (x, y) in pts:
        if not only_inside or inside(g, x, y):
            g.set(x, y, ch)


def blob(g, cx, cy, rx, ry, mid, lit, dark, deep=None):
    """An ellipse lit from the upper left: a lit crescent on the key side, a dark one on the far
    side, and the deep stop tucked under it. Hand-placed furniture goes on top of this."""
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            nx, ny = (x - cx) / float(rx), (y - cy) / float(ry)
            d = nx * nx + ny * ny
            if d > 1.0:
                continue
            k = -(nx + ny) * 0.7071
            if k > 0.42 and d > 0.30:
                ch = lit
            elif deep and k < -0.55 and d > 0.55:
                ch = deep
            elif k < -0.30 and d > 0.40:
                ch = dark
            else:
                ch = mid
            g.set(x, y, ch)


def rim(g, y0, y1, skip="o", min_run=2):
    """The 1 px key rim on the left edge of every run in rows y0..y1 (contract section 2).

    Only on runs at least `min_run` wide. A rim beside a one-pixel limb doubles the limb into a
    white wire, which at game size is all that is left of it - the first pass of the beater ran on
    two white sticks. A thin limb is separated by its own lit stop instead.
    """
    for y in range(int(y0), int(y1) + 1):
        x = 1
        while x < g.cell:
            here = g.get(x, y)
            if here != "." and g.get(x - 1, y) == ".":
                run = 0
                while x + run < g.cell and g.get(x + run, y) not in (".", "W"):
                    run += 1
                if here not in skip and run >= min_run:
                    g.set(x - 1, y, "W")
                x += max(1, run)
                continue
            x += 1


# Leaf stamps: L lit, M mid, D dark, O occ. A leaf at 32 px is four or five pixels and its whole
# read is its POINT, so every stamp has a tip and a lit edge on the key side; rectangles laid in
# courses read as brickwork (the first pass of the leafwight did exactly that).
LEAVES = {
    "dr": ["LM...", "MMMD.", ".DMMD", "...DO"],
    "dl": ["...ML", ".DMMM", "DMMD.", "OD..."],
    "dn": ["LMD", "MMD", ".D."],
    "up": [".L.", "LMD", "MMD"],
}


def leaf(g, x, y, kind, lit, mid, dark, only_inside=True):
    for j, row in enumerate(LEAVES[kind]):
        for i, k in enumerate(row):
            if k == ".":
                continue
            px, py = x + i, y + j
            if only_inside and not inside(g, px, py):
                continue
            g.set(px, py, {"L": lit, "M": mid, "D": dark, "O": "o"}[k])


def twig(g, pts, lit, dark):
    """A one-pixel stick along a polyline: lit, with its dark side under it."""
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + 1):
            t = i / float(max(1, n))
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            g.set(x, y, lit)
            if g.get(x, y + 1) == ".":
                g.set(x, y + 1, dark)


# =================================================================================================
# THE NINE
# =================================================================================================

def leafwight():
    """BULK. A forager buried in the leaf-fall, still walking: a heap of dead leaves wearing a
    person's outline, under one curled oak leaf hooked forward over the face like a hood.

    Built from LEAVES - pointed stamps overlapping down the heap at different angles, each with a
    lit edge and a tip - because a heap drawn as one tone with speckle reads as a bush, and a bush
    is the decor; and the first pass laid rectangles in rows, which read as a wall. The points are
    what make it a pile of separate things, i.e. leaves.
    """
    g = G(CELL)
    # The heap, widest at the hem and leaning a little into the walk.
    mass(g, [(12, 11, 19), (16, 9, 21), (22, 8, 22), (27, 7, 23), (28, 7, 23)], "E", "e", "A")

    # THE HEM: leaves hanging off it point-down, different lengths, daylight between some.
    for (x, d) in zip(range(6, 25), [1, 2, 1, 0, 2, 2, 1, 0, 1, 2, 1, 0, 2, 1, 2, 2, 0, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 28 + k, "A" if k < d else "E")
    for x in (9, 13, 17, 22):
        cut(g, (x, 29), (x, 30))

    # THE LEAVES, hand-placed: different angles, overlapping, the lower ones drawn last so they lie
    # over the ones above. Brown is gold's shade stop - dead leaves are the one warm pigment in the
    # wood, and it is pigment, not light: a handful, never on the edge.
    for (x, y, kind, lit, mid, dark) in (
            (10, 13, "dr", "l", "e", "A"), (15, 13, "dl", "e", "E", "A"),
            (8, 16, "dn", "e", "E", "A"), (12, 16, "dr", "g", "u", "o"), (17, 16, "dl", "l", "e", "A"),
            (9, 19, "dl", "e", "E", "o"), (14, 19, "dn", "l", "e", "A"), (18, 19, "dr", "e", "E", "A"),
            (7, 22, "dr", "l", "e", "A"), (12, 22, "dl", "g", "u", "o"), (17, 22, "dr", "e", "E", "o"),
            (9, 25, "dn", "e", "E", "o"), (13, 25, "dr", "l", "e", "A"), (19, 25, "dn", "g", "u", "o"),
            (16, 26, "dl", "e", "E", "A")):
        leaf(g, x, y, kind, lit, mid, dark)

    # THE STALKS: stone, poking out of the heap at odd angles - the one hard thing in it.
    twig(g, [(9, 22), (6, 19)], "p", "J")
    twig(g, [(20, 27), (24, 25)], "N", "J")
    g.set(5, 18, "N")

    # THE HOOD: one big oak leaf, curled over and hooked forward and DOWN past the face, so its
    # tip hangs in front of the eyes like a beak - the shape a cap or a cowl never has.
    mass(g, [(5, 14, 18), (6, 12, 20), (8, 10, 22), (10, 10, 24), (12, 10, 25), (13, 11, 25)],
         "e", "l", "E")
    for (x, y, ch) in ((24, 14, "e"), (25, 14, "E"), (24, 15, "E"), (25, 15, "A"), (24, 16, "A")):
        g.set(x, y, ch)
    # Its lobed margin: rounded bumps with notches between, so it is an oak leaf and not a cap.
    cut(g, (14, 5), (18, 5), (21, 6), (24, 9), (10, 9))
    for (x, y, ch) in ((16, 4, "l"), (15, 4, "L"), (17, 4, "e"), (20, 5, "l"), (19, 5, "e"),
                       (23, 7, "e"), (25, 10, "E"), (9, 10, "l"), (9, 11, "e")):
        g.set(x, y, ch)
    # The midrib, running the length of the leaf, and side veins off it.
    fold(g, [(11, 13), (12, 9), (15, 6), (19, 7), (22, 9), (24, 13)], "A", "L")
    fold(g, [(14, 7), (16, 10)], "E", None)
    fold(g, [(19, 8), (19, 11)], "E", None)
    fold(g, [(12, 10), (14, 12)], "E", None)

    # THE FACE, in the shadow under the hook.
    roster.void(g, 11, 15, 2.6, 2.2, cx=19.0)
    g.span(10, 16, 23, "A")

    # A twig of an arm, reaching out of the heap.
    _limb(g, 20, 18, 25, 21, 0.8, "N", "p", "J")
    for (fx, fy) in ((26, 20), (26, 22), (25, 23)):
        g.set(fx, fy, "p")

    rim(g, 4, 28)
    contact(g, 7, 24)
    eyes(g, 12, 18, 21, "Y", "y")
    return g.rows()


def beater():
    """FAST. One of the beaters who drove game for the hunt, still running ahead of something.

    Gangly and pitched forward, mid-stride, with daylight between the legs - and a long streamer of
    hanging moss trailing off the shoulders. The moss is LICHEN, a different row and a different
    value from the linen body: a trail drawn in the body's own tone swallows the body (roster.py's
    runner learnt that), and the streamer is the attachment the figure is read by.
    """
    g = G(CELL)
    # THE STREAMER first, behind everything: strands of moss trailing back and drooping, with gaps.
    for (x0, y0, x1, y1, tone) in ((15, 10, 4, 13, MOSS), (15, 11, 2, 16, MOSS_SH),
                                   (14, 12, 3, 19, MOSS), (14, 13, 5, 22, MOSS_DP),
                                   (13, 14, 7, 21, MOSS_SH)):
        _limb(g, x0, y0, x1, y1, 1.1, tone, MOSS_LIT, MOSS_DP)
    for (x, y) in ((3, 14), (2, 17), (4, 20), (6, 23), (8, 22)):
        g.set(x, y, MOSS_DP)
    g.set(5, 12, MOSS_LIT)
    g.set(9, 13, MOSS_HI)

    # LEGS: a long stride, knees bent, thin.
    _limb(g, 14, 19, 18, 24, 1.3, "E", "e", "A")       # near leg, thigh
    _limb(g, 18, 24, 21, 29, 1.1, "E", "e", "A")       # shin, reaching forward
    _limb(g, 13, 19, 10, 24, 1.2, "A", "E", "o")       # far leg, thigh
    _limb(g, 10, 24, 6, 28, 1.0, "A", "E", "o")        # shin, trailing back
    g.span(FEET, 20, 23, "A")                          # the near foot, flat on the ground
    g.set(20, FEET, "e")
    g.span(FEET - 1, 4, 6, "A")                         # the far foot, pushing off
    g.set(4, FEET - 1, "E")

    # THE BODY, pitched forward: a narrow torso from hip to shoulder.
    mass(g, [(11, 16, 20), (14, 14, 19), (17, 13, 17), (20, 12, 16)], "E", "e", "A")
    fold(g, [(18, 12), (15, 16), (14, 19)], "A", "e")
    g.span(17, 12, 16, "o")                            # a cord at the waist
    g.set(13, 17, "u")

    # ARMS: the near one thrust forward with the beating stick, the far one flung back.
    _limb(g, 19, 13, 23, 16, 1.0, "E", "e", "A")
    g.set(24, 16, "h")
    g.set(24, 15, "i")
    twig(g, [(22, 18), (28, 9)], "N", "J")            # the switch, held up and forward
    g.set(28, 8, "p")
    g.set(27, 10, "p")
    _limb(g, 16, 13, 12, 14, 0.9, "A", "E", "o")

    # THE HEAD: a ragged hood thrust forward, void inside.
    mass(g, [(6, 18, 21), (7, 17, 23), (9, 16, 24), (11, 17, 24), (12, 18, 23)], "E", "e", "A")
    roster.void(g, 8, 11, 2.0, 1.4, cx=21.2)
    g.set(16, 7, "e")
    g.set(15, 8, "E")

    rim(g, 6, 29, skip="o" + MOSS + MOSS_SH + MOSS_DP + MOSS_LIT + MOSS_HI)
    contact(g, 5, 24)
    eyes(g, 9, 20, 22, "Y", "y")
    return g.rows()


def dryad():
    """RANGED. The tree's own spirit: bark-thin under a crown of dead twigs, with a seed pod
    floating detached at head height - the thing it throws, in the grass ramp it throws in.

    Tall and narrow like every caster, and kept well away from Elderbark's outline: no trunk, no
    branches for arms, no canopy. A thin figure in strips of bark with twigs for a crown is a
    person-shaped thing the tree let go of, not a small tree.
    """
    g = G(CELL)
    # THE ROBE OF BARK: narrow, long, splitting into strips at the hem.
    mass(g, [(11, 13, 17), (16, 12, 18), (22, 11, 19), (27, 10, 21), (29, 9, 21)], "N", "p", "J")
    # Furrows down the bark, uneven, each a dark stop with a lit one beside it.
    fold(g, [(13, 13), (12, 20), (11, 28)], "J", "P")
    fold(g, [(15, 12), (15, 19), (14, 29)], "j", "p")
    fold(g, [(17, 14), (18, 22), (18, 29)], "J", "N")
    fold(g, [(20, 24), (20, 29)], "j", None)
    # Linen showing between the strips: the pale under-bark.
    for (x, y) in ((14, 16), (14, 17), (16, 21), (16, 22), (13, 24), (19, 26), (17, 27)):
        g.set(x, y, "e")
    g.set(16, 23, "E")
    # The hem splits into root-like strips with daylight between them.
    for x in (12, 16, 19):
        cut(g, (x, 29), (x, 28))
    g.set(8, 29, "j")
    g.set(22, 29, "J")

    # ARM, reaching up toward the pod: thin, bark, ending in twig fingers.
    _limb(g, 17, 14, 21, 11, 0.9, "N", "p", "J")
    for (fx, fy) in ((22, 10), (22, 9), (23, 11)):
        g.set(fx, fy, "p")
    g.set(10, 15, "j")
    _limb(g, 12, 14, 10, 19, 0.8, "j", "N", "J")

    # THE HEAD: a narrow bark mask with the void set into it.
    mass(g, [(7, 13, 16), (8, 12, 17), (11, 12, 17), (12, 13, 16)], "e", "l", "E")
    roster.void(g, 9, 11, 1.6, 1.0, cx=15.0)
    g.set(12, 9, "E")

    # THE CROWN OF DEAD TWIGS: forking up and out, uneven, the tallest off-centre.
    for pts in ([(13, 7), (11, 4), (9, 3)], [(11, 5), (10, 6), (8, 6)],
                [(14, 6), (14, 3), (13, 2)], [(14, 4), (16, 2)],
                [(16, 7), (18, 4), (20, 3)], [(18, 5), (20, 6)], [(17, 7), (19, 8)]):
        twig(g, pts, "p", "J")
    g.set(9, 3, "P")
    g.set(20, 3, "P")

    # THE SEED POD, detached: an almond in the grass ramp with daylight all round it.
    for (y, x0, x1, tone) in ((5, 25, 26, SEED_EDGE), (6, 24, 27, SEED_MID), (7, 24, 27, SEED_MID),
                              (8, 24, 27, SEED_MID), (9, 25, 26, SEED_EDGE)):
        g.span(y, x0, x1, tone)
    g.set(25, 6, SEED_HOT)
    g.set(25, 7, SEED_CORE)
    g.set(26, 8, SEED_EDGE)
    g.set(25, 4, SEED_EDGE)

    rim(g, 2, 29, skip="o" + SEED_EDGE + SEED_MID + SEED_HOT + SEED_CORE)
    contact(g, 9, 22)
    eyes(g, 10, 14, 16, "Y", "y")
    return g.rows()


def effigy():
    """TURRET. A warning effigy he bound from the woodsmen's withies and staked at the path.

    BUILT, so it burns: fire in the wicker head where everything else in the wood stares cold.
    A cross of bound sticks with the arms out wider than anything else in the chapter, on a stake
    driven into a heap of stones - it is the one thing here that does not walk, and it should look
    planted.
    """
    g = G(CELL)
    # THE CAIRN it is staked in.
    mass(g, [(27, 12, 19), (29, 10, 21), (30, 10, 21)], "N", "p", "J")
    for (x, y) in ((13, 28), (17, 29), (11, 30), (19, 30)):
        g.set(x, y, "o")
    g.set(15, 27, "P")
    # THE STAKE.
    g.rect(15, 20, 16, 27, "j")
    g.line(15, 20, 15, 27, "N")

    # THE BODY: a bundle of withies, peeled pale and gone grey, bound with rope at the chest and
    # the waist. Each withy is its own column, in its own tone, so the bundle is a bundle of sticks
    # and not a barrel - the first pass banded a solid body in gold and read as a machine.
    withies = [(12, "E"), (13, "e"), (14, "N"), (15, "l"), (16, "E"), (17, "p"), (18, "e"), (19, "A")]
    for (x, tone) in withies:
        top = 12 if 13 <= x <= 18 else 14
        g.line(x, top, x, 22, tone)
    for (x, y) in ((13, 17), (16, 19), (18, 14), (14, 21)):
        g.set(x, y, "o")                               # gaps in the bundle, one dark pixel each
    for (by, bx0, bx1) in ((15, 11, 20), (20, 12, 19)):
        g.span(by, bx0, bx1, "A")
        g.set(bx0, by, "E")
        g.set(bx0 + 3, by, "E")
        g.span(by + 1, bx0 + 1, bx1 - 1, "o")
    # Withy ends splaying out under the waist binding into a ragged skirt of sticks.
    for (x0, x1, y1, tone) in ((12, 9, 26, "E"), (14, 12, 25, "N"), (17, 18, 25, "e"),
                               (19, 22, 26, "E"), (13, 13, 24, "e")):
        g.line(x0, 22, x1, y1, tone)

    # THE CROSSBAR: a bundle of three withies lashed across, arms flung wide, ends frayed to
    # different lengths.
    g.span(11, 5, 26, "e")
    g.span(12, 4, 27, "N")
    g.span(13, 5, 25, "J")
    g.span(11, 3, 4, "E")
    g.span(13, 26, 28, "E")
    for (x, y) in ((2, 10), (3, 13), (29, 11), (28, 10), (27, 14)):
        g.set(x, y, "N")
    for x in (9, 21):
        g.line(x, 10, x + 1, 14, "A")                  # rope lashings, at uneven places
    g.line(14, 10, 18, 14, "A")                        # the cross-lash at the stake
    g.line(18, 10, 14, 14, "A")
    # Rags hung off one arm only: an effigy is dressed, carelessly.
    for (x, y0, y1) in ((23, 14, 19), (24, 14, 17), (25, 14, 21)):
        g.line(x, y0, x, y1, "A")
    g.set(23, 14, "E")
    g.set(25, 21, "E")
    g.set(24, 18, "E")

    # THE WICKER HEAD: an open cage of pale withies with the dark - and the fire - inside it.
    g.disc(15.5, 6.5, 4.0, "o")
    ring = [(14, 2), (15, 2), (16, 2), (17, 2), (13, 3), (18, 3), (12, 4), (19, 4), (12, 5), (19, 5),
            (11, 6), (20, 6), (12, 7), (19, 7), (12, 8), (19, 8), (13, 9), (18, 9), (14, 10),
            (15, 10), (16, 10), (17, 10)]
    for i, (x, y) in enumerate(ring):
        g.set(x, y, "e" if (x < 16 and y < 7) else ("E" if i % 3 else "A"))
    for pts in ([(13, 4), (18, 9)], [(16, 3), (13, 9)], [(19, 5), (16, 10)]):
        twig(g, pts, "E", "o")
    g.set(13, 3, "l")
    g.set(14, 2, "l")
    # Fire, in the eyes and in one crack under them - and nowhere else on the figure.
    g.set(15, 8, "~")
    g.set(16, 8, "=")
    rim(g, 2, 30)
    contact(g, 9, 22)
    eyes(g, 6, 14, 17, "+", "-")
    return g.rows()


def gloamstag():
    """CHARGER. A stag gone gaunt, head lowered to charge, the rack thrust forward.

    The rack is the attachment, and it has to be one the wolf can never be mistaken for: the wolf
    is a low bar with its head forward; the stag stands taller on long legs, its back slopes DOWN
    to a lowered head, and above the head is the largest open shape in the chapter, in bone - the
    one bright material on it, so the eye finds the rack before the body.
    """
    g = G(CELL)
    mid, lit, dark = "A", "E", "o"
    # LEGS: long, thin, paired close under each mass with a gap between the pairs.
    for (hx, hy, kx, ky, fx, th) in ((7, 19, 6, 24, 7, 1.2), (10, 19, 11, 24, 10, 1.1),
                                     (17, 19, 17, 24, 18, 1.1), (19, 19, 20, 24, 21, 1.0)):
        _limb(g, hx, hy, kx, ky, th, mid, lit, dark)
        _limb(g, kx, ky, fx, FEET - 1, th * 0.75, mid, lit, dark)
        g.set(fx, FEET, "q")
        g.set(fx + 1, FEET, "q")

    # THE MASSES: haunch and shoulder, the back sloping down toward the head.
    SB.swell(g, 13, 20, [3.5, 4.5, 4.5, 3.8, 2.8], mid, lit, dark, cx=9.0)
    SB.swell(g, 13, 20, [3.0, 4.2, 4.2, 3.6, 2.6], mid, lit, dark, cx=18.0)
    for x in range(10, 18):
        top = 14 + (1 if 12 < x < 16 else 0)
        for y in range(top, 20):
            if g.get(x, y) == ".":
                g.set(x, y, mid)
    # Gaunt: ribs showing along the barrel, uneven.
    for (x, y0, y1) in ((12, 15, 18), (14, 16, 19), (16, 15, 18)):
        g.line(x, y0, x, y1, "o")
        g.set(x - 1, y0, "e")
    g.line(6, 14, 18, 13, lit)
    g.set(4, 14, mid)
    g.set(3, 13, lit)                                   # the short tail, flicked up
    # The pale rump patch every deer carries, a stop lighter than the haunch, and the haunch's
    # own crease where the thigh meets the barrel.
    for (x, y) in ((5, 15), (6, 15), (5, 16), (6, 16), (5, 17)):
        g.set(x, y, "e")
    g.set(7, 16, "E")
    fold(g, [(10, 14), (11, 17), (10, 20)], "o", "E")
    # The shoulder blade, and a shaggy ruff of winter coat hanging under the neck.
    fold(g, [(17, 14), (19, 17)], "o", "E")
    for (x, y, ch) in ((20, 19, "o"), (21, 20, "A"), (21, 21, "o"), (22, 21, "E"), (20, 20, "E")):
        g.set(x, y, ch)
    # Hocks and knees: one lit pixel at each joint, so the legs are jointed, not sticks.
    for (x, y) in ((6, 24), (11, 24), (17, 24), (20, 24)):
        g.set(x, y, "e")

    # NECK, down and forward off the shoulder; HEAD, long, muzzle low.
    for i, x in enumerate(range(20, 24)):
        y0 = 15 + i
        g.rect(x, y0, x, y0 + 3, mid)
        g.set(x, y0, lit)
    mass(g, [(18, 22, 25), (20, 23, 27), (22, 24, 28), (23, 25, 28)], mid, lit, dark, rim=False)
    g.set(28, 23, "o")
    g.set(27, 22, "E")
    g.span(24, 25, 27, "o")

    # THE RACK: two beams rising forward off the skull, each with tines. The far beam is drawn
    # first and a stop darker, so the near one reads in front of it.
    for pts in ([(22, 17), (20, 12), (21, 7), (23, 4)], [(20, 11), (17, 9)], [(21, 8), (19, 5)]):
        twig(g, pts, "n", "m")
    for pts in ([(24, 17), (26, 13), (26, 8), (28, 4)], [(26, 13), (29, 12)],
                [(26, 9), (29, 7)], [(26, 7), (24, 4)], [(25, 15), (28, 16)]):
        twig(g, pts, "s", "n")
    g.set(28, 4, "S")
    g.set(23, 4, "k")
    g.set(24, 17, "k")
    g.set(25, 16, "k")
    # An ear, back-swept under the rack.
    g.set(22, 18, "E")
    g.set(21, 17, lit)

    rim(g, 12, 25, skip="o" + "snmkS")
    contact(g, 5, 23)
    eyes(g, 20, 25, 26, "Y", "y")
    return g.rows()


def burstpod():
    """EXPLODER. A seed-husk swollen past splitting, walking on spindly root legs.

    The only bulging shape in the wood - everything else here tapers or spreads - with its seams
    split and the pale seed showing through, so it reads as about to come apart before it does.
    NOT A MUSHROOM: Toxic Spore Burst is mushrooms, and a player who learnt that a cap means a
    spell of theirs must never meet one coming at them.

    It FACES LEFT. ExploderEnemy.tscn, the stand-in it copies, says SpriteFacesRightByDefault =
    false and the brief is to copy that scene's properties, not retune them - so the drawing turns
    to match the scene rather than the other way round. Its feel numbers lean the other way too.
    """
    g = G(CELL)
    # ROOT LEGS, spindly and jointed, splayed under the husk.
    for pts in ([(12, 23), (9, 26), (8, 30)], [(15, 24), (14, 27), (13, 30)],
                [(18, 24), (19, 27), (19, 30)], [(21, 22), (23, 26), (24, 30)]):
        twig(g, pts, "N", "J")
    for (x, y) in ((7, 30), (12, 30), (20, 30), (25, 30)):
        g.set(x, y, "j")

    # THE HUSK: an egg, swollen low.
    blob(g, 16.0, 16.5, 7.4, 8.4, "e", "l", "E", deep="A")
    # SEGMENT SEAMS, curved like the ribs of a pod, from the tip to the base.
    for pts in ([(15, 8), (11, 13), (10, 19), (13, 24)], [(17, 8), (17, 16), (16, 24)],
                [(18, 9), (22, 14), (22, 20), (19, 24)]):
        fold(g, pts, "A", "l", side=(-1, 0))
    # SPLITS: two seams burst open, occ with pale seed inside.
    for (x, y) in ((11, 15), (11, 16), (11, 17), (10, 18)):
        g.set(x, y, "o")
    g.set(12, 16, "k")
    g.set(12, 17, "s")
    for (x, y) in ((21, 17), (21, 18), (22, 19), (20, 20)):
        g.set(x, y, "o")
    g.set(20, 18, "n")
    g.set(20, 19, "k")
    # Stone flecks on the husk, and a dry patch where it has stretched thinnest.
    for (x, y) in ((14, 12), (19, 12), (13, 21), (18, 22), (15, 19)):
        g.set(x, y, "j")
    # THE TIP: the stalk it hung by, curled over to the left.
    twig(g, [(16, 8), (15, 5), (12, 4)], "p", "J")
    g.set(11, 5, "N")

    # THE FACE: a split low on the leading (left) side, with the mark in it.
    roster.void(g, 13, 15, 2.2, 1.6, cx=12.8)
    rim(g, 4, 30, skip="o")
    contact(g, 7, 25)
    eyes(g, 14, 11, 14, "y", "Y")
    return g.rows()


def hollow_bear():
    """SLAMMER. A bear reared on its hind legs; the slam is it coming down.

    The forepaws are the highest thing in the cell, held up and forward over its own muzzle, which
    is what the ring on the ground is about to agree with. A round body, a round hump, round ears
    and a muzzle pushed out past the outline: every one of those is curved, which is the bear's
    whole read against a forest of sticks and points.

    It is drawn against Elderbark on purpose (.ai/bestiary.md section 2, nothing echoes its own
    chapter's boss). The treant is a squared trunk with lichen patches, branches radiating out of
    it and a ribbed slot in its chest; so the bear has no moss, no hollow, and arms that reach the
    same way together instead of spreading - two passes that spread them read as a stump with
    branches, which is exactly him.
    """
    g = G(CELL)
    mid, lit, dark = "A", "E", "o"
    # THE FAR FORELEG first, behind the head: up and forward, its paw the very top of the figure.
    _limb(g, 15, 13, 21, 5, 1.6, "A", "E", "o")
    blob(g, 21.5, 3.6, 2.1, 1.5, "A", "E", "o")
    for (x, y, ch) in ((24, 3, "k"), (24, 5, "n"), (23, 5, "n")):
        g.set(x, y, ch)

    # HIND LEGS and the haunch: short, thick, planted, the near foot forward.
    blob(g, 12.0, 23.5, 6.4, 4.8, mid, lit, dark)
    mass(g, [(25, 7, 12), (28, 6, 12), (30, 5, 12)], mid, lit, dark)
    mass(g, [(25, 15, 19), (28, 16, 20), (30, 16, 22)], mid, lit, dark)
    g.set(13, 30, "o")
    for x in (6, 8, 17, 19, 21):
        g.set(x, FEET, "n")

    # THE BODY: a round pear, reared and leaning into the slam.
    SB.swell(g, 10, 25, [3.6, 5.6, 6.8, 7.0, 6.4], mid, lit, dark, cx=13.0)
    blob(g, 10.5, 11.5, 4.4, 3.4, mid, lit, dark)       # the hump, behind the head
    # Guard hair along the hump, a stop lighter, and the scruff under it.
    for (x, y) in ((8, 9), (9, 8), (10, 8), (11, 8), (12, 8), (7, 10)):
        g.set(x, y, "e")
    for (x, y) in ((9, 10), (11, 10), (13, 10), (7, 12)):
        g.set(x, y, "o")
    # The belly, a stop lighter, down the front of the reared body.
    for y in range(15, 25):
        x = 18 + (1 if 17 <= y <= 22 else 0)
        g.set(x, y, "E")
        g.set(x - 1, y, "E" if y % 3 else "e")
    # Fur: strokes carried through the mass, uneven, each a dark stop beside a lit one.
    for pts in ([(8, 14), (7, 19), (8, 23)], [(11, 13), (10, 17)], [(13, 18), (12, 22)],
                [(9, 25), (11, 28)], [(15, 24), (16, 28)], [(15, 14), (14, 17)]):
        fold(g, pts, "o", "e")

    # THE NEAR FORELEG: up and forward, claws hooked down. Drawn before the head, so the muzzle
    # and ears land on top of it - the first pass buried the face behind its own arm.
    _limb(g, 18, 14, 25, 7, 1.9, mid, "e", dark)
    blob(g, 25.5, 6.0, 2.2, 1.7, mid, "e", dark)
    for (x, y, ch) in ((28, 5, "s"), (28, 7, "k"), (27, 8, "k"), (26, 8, "n")):
        g.set(x, y, ch)

    # THE HEAD, forward of the hump: round skull, round ears, the muzzle pushed out.
    blob(g, 17.0, 9.5, 3.2, 2.8, mid, lit, dark)
    for (y, x0, x1) in ((9, 19, 23), (10, 19, 24), (11, 19, 23)):
        g.span(y, x0, x1, "e" if y == 9 else "E")
    g.set(19, 9, "l")
    g.set(23, 11, "A")
    g.set(24, 10, "o")                                  # the nose
    g.set(23, 11, "o")                                  # the open mouth
    g.set(22, 12, "s")                                  # a tooth
    g.span(12, 18, 21, "o")
    for (x, y) in ((14, 6), (18, 6)):
        g.rect(x, y - 1, x + 1, y, mid)
        g.set(x, y - 1, lit)
        g.set(x + 1, y, dark)
    roster.void(g, 8, 10, 1.4, 1.0, cx=17.6)

    rim(g, 2, 29, skip="o" + "snkS")
    contact(g, 5, 23)
    eyes(g, 9, 16, 18, "Y", "y")
    return g.rows()


def huntsman():
    """SUMMONER. The hunt's master, raising a curled horn to call the beaters out of the wood.

    The horn, held up at hand height, is the attachment - bone, curled, the bell turned forward.
    Under it a long coat and a hood whose tail hangs down the back, and on the coat the one patch
    of colour the hunt ever had: old livery red, faded, a stripe across the chest and no more.
    """
    g = G(CELL)
    # THE HOOD'S TAIL (a liripipe), hanging down the back.
    _limb(g, 12, 8, 7, 20, 1.3, "A", "E", "o")
    g.set(6, 21, "A")
    g.set(7, 22, "E")

    # THE COAT: long, to the ground, a split at the back.
    mass(g, [(10, 12, 18), (16, 11, 19), (23, 10, 21), (29, 9, 22)], "E", "e", "A")
    cut(g, (11, 27), (11, 28), (11, 29), (12, 29))
    fold(g, [(14, 13), (13, 21), (13, 29)], "A", "e")
    fold(g, [(18, 17), (19, 29)], "A", None)
    # THE LIVERY: a red stripe across the chest, and a gold clasp on it.
    for (x, y) in ((13, 13), (14, 14), (15, 14), (16, 15), (17, 16), (18, 17)):
        g.set(x, y, RED)
        g.set(x, y + 1, RED_SH)
    g.set(12, 13, RED_LIT)
    g.set(15, 15, "g")
    # A belt, and the knife on it.
    g.span(21, 10, 21, "o")
    g.set(17, 21, "u")
    g.line(19, 22, 20, 25, "q")
    g.set(19, 22, "r")

    # THE HOOD.
    mass(g, [(4, 13, 16), (5, 12, 18), (7, 11, 19), (9, 11, 19), (10, 12, 18)], "E", "e", "A")
    roster.void(g, 6, 9, 2.0, 1.4, cx=16.6)

    # THE ARM raised, and THE HORN held up at the face: a J of bone, mouthpiece at the void, the
    # body swinging down under the hand and up again, the bell flared open and pointed at the sky.
    # Two pixels thick all the way, tapering only at the mouth - a one-pixel curve at 32 px is a
    # hair, and the horn is the thing this figure is named by.
    _limb(g, 17, 12, 21, 12, 1.1, "E", "e", "A")
    # The horn as a crescent: its thickness grows from the mouthpiece to the bell, the outer
    # (lower) edge lit by the key, the inner edge in shade.
    for i in range(12):
        t = i / 11.0
        a = math.pi * (1.05 - 1.30 * t)                 # from the mouth, down under, and up
        x = 22.0 + 4.6 * math.cos(a)
        y = 7.0 + 4.2 * math.sin(a)
        w = 0.6 + 1.6 * t
        g.span(int(round(y)), x - w * 0.5, x + w * 0.5, "k")
        g.set(x - w * 0.5, y, "s")
        g.set(x + w * 0.5, y, "n")
        if g.get(x, y + 1) == ".":
            g.set(x, y + 1, "m")
    g.set(17, 7, "n")                                   # the mouthpiece, at the void
    for (x, y, ch) in ((24, 2, "s"), (25, 2, "S"), (26, 2, "k"), (27, 3, "k"), (27, 4, "n"),
                       (25, 3, "o"), (26, 3, "o"), (26, 4, "m"), (23, 3, "k")):
        g.set(x, y, ch)                                 # the bell, flared, dark inside
    g.set(21, 12, "h")                                  # the hand round the horn's belly
    g.set(22, 12, "i")
    g.set(20, 12, "f")

    rim(g, 4, 29, skip="o" + "snkmS")
    contact(g, 8, 23)
    eyes(g, 7, 15, 17, "Y", "y")
    return g.rows()


def gamekeeper():
    """MINIBOSS, on the 48 cell. His jailer in the wood: tall and gaunt in a long coat, a man-trap
    slung on his back, and a gibbet cage hung from a pole over his shoulder, rising past his head.

    The cage is the silhouette: nothing else in the chapter has a box hung in the air above it,
    and it is also what a jailer in a wood would carry - the forest's own cell, for whatever he
    catches. Steel for the trap and the cage, the coat in linen, and one gold buckle, which is the
    whole warm budget.
    """
    c = 48
    feet = c - 2
    g = G(c)
    # THE MAN-TRAP, slung on the back and showing past it: two jaws shut on each other, an arc of
    # steel each, with the teeth interlocking down the seam between them. Drawn FIRST, so the coat
    # lands over the half of it that is behind him.
    for y in range(16, 31):
        t = (y - 16) / 14.0
        bulge = math.sin(math.pi * t)
        xo = 16.0 - 7.0 * bulge                        # the outer edge of the shut jaws
        xs = 16.0 - 3.6 * bulge                        # the seam where the teeth meet
        g.span(y, xo, 17, "R")
        g.set(xo, y, "r")
        g.set(xo + 1, y, "q")
        g.set(xs, y, "o")
        g.set(xs + (1 if y % 2 else -1), y, "t" if y % 2 else "r")
    g.set(15, 16, "r")
    g.set(15, 30, "q")
    for (x, y) in ((16, 15), (16, 31)):
        g.set(x, y, "q")                                # the hinge posts

    # LEGS, thin, below the coat.
    for (lx, tone, lit_) in ((19, "A", "E"), (25, "E", "e")):
        g.rect(lx, 40, lx + 2, feet - 1, tone)
        g.line(lx, 40, lx, feet - 1, lit_)
        g.span(feet, lx - 1, lx + 4, "q")
        g.span(feet - 1, lx, lx + 4, "R")
        g.set(lx, feet - 1, "r")

    # THE COAT: long, gaunt through the chest, flaring to tails.
    mass(g, [(14, 18, 28), (20, 17, 28), (28, 16, 29), (36, 14, 31), (41, 13, 32)], "E", "e", "A")
    cut(g, (22, 39), (22, 40), (22, 41), (23, 41), (21, 41))
    for pts in ([(20, 15), (19, 27), (16, 41)], [(26, 17), (27, 30), (29, 41)], [(23, 31), (24, 40)]):
        fold(g, pts, "A", "e")
    for (x, y) in ((18, 33), (27, 35), (15, 38)):
        g.set(x, y, "l")
    # Lapels and a row of buttons.
    g.line(21, 14, 23, 22, "A")
    g.line(26, 14, 24, 22, "A")
    for by in (24, 27, 30):
        g.set(24, by, "r")
    # THE BELT and the one gold buckle.
    g.span(28, 16, 29, "o")
    g.span(29, 16, 29, "A")
    g.rect(22, 28, 24, 29, "G")
    g.set(22, 28, "H")
    g.set(24, 29, "u")
    g.set(23, 28, "o")

    # ARMS: the near one up to the pole, the far one hanging.
    _limb(g, 27, 16, 31, 23, 1.6, "E", "e", "A")
    g.rect(31, 22, 33, 24, "h")
    g.set(31, 22, "f")
    g.set(33, 24, "i")
    _limb(g, 18, 16, 16, 30, 1.4, "A", "E", "o")
    g.rect(15, 30, 17, 32, "i")
    g.set(15, 30, "h")

    # THE HEAD: gaunt, under a wide-brimmed hat.
    mass(g, [(9, 21, 26), (11, 21, 26), (13, 22, 25)], "E", "e", "A", rim=False)
    roster.void(g, 10, 13, 2.0, 1.3, cx=23.8)
    g.rect(21, 4, 27, 8, "A")
    g.span(4, 22, 26, "E")
    g.line(21, 5, 21, 8, "E")
    g.span(7, 21, 27, "q")                             # the hat band
    g.span(9, 18, 30, "A")                             # the brim
    g.span(8, 19, 29, "E")
    g.set(18, 9, "e")

    # THE POLE, over the near shoulder and up behind the head, and THE CAGE hung from its end.
    roster.shaft(g, 33, 25, 8, 4, "p", "N", "J", width=2)
    g.line(8, 4, 8, 6, "q")                              # the hook and chain
    g.set(8, 5, "r")
    # The cage: a domed top, bars at uneven gaps, a floor plate, and darkness inside.
    g.rect(4, 9, 12, 19, "o")
    g.span(7, 6, 10, "R")
    g.span(8, 5, 11, "r")
    g.set(5, 8, "t")
    for bx in (4, 6, 9, 12):
        g.line(bx, 9, bx, 19, "r" if bx < 8 else "R")
    g.line(4, 9, 4, 19, "t")
    g.span(13, 4, 12, "R")
    g.span(20, 3, 13, "q")
    g.span(19, 4, 12, "R")
    g.set(11, 14, "q")
    g.set(10, 17, "q")
    # Something left in it: a scrap of cloth caught on a bar.
    g.set(7, 16, "A")
    g.set(7, 17, "E")

    rim(g, 4, 45, skip="o")
    contact(g, 13, 33, y=feet)
    eyes(g, 11, 22, 25, "Y", "y")
    return g.rows()


CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    ("forest-leafwight", leafwight, 32, (0.8, 2.4, 2), "ForestLeafwightFrames"),
    ("forest-beater", beater, 32, (1.6, 3.4, 3), "ForestBeaterFrames"),
    ("forest-dryad", dryad, 32, (0.9, 2.2, 3), "ForestDryadFrames"),
    ("forest-effigy", effigy, 32, (0.4, 2.0, 1), "ForestEffigyFrames"),
    ("forest-gloamstag", gloamstag, 32, (1.8, 3.0, 3), "ForestGloamstagFrames"),
    # Faces left (see burstpod), so it leans into its strike and is knocked back the other way.
    ("forest-burstpod", burstpod, 32, (1.1, -2.0, -3), "ForestBurstpodFrames"),
    ("forest-hollow-bear", hollow_bear, 32, (0.6, 4.0, 1), "ForestHollowBearFrames"),
    ("forest-huntsman", huntsman, 32, (0.9, 2.6, 2), "ForestHuntsmanFrames"),
    ("forest-gamekeeper", gamekeeper, 48, (0.5, 4.2, 1), "ForestGamekeeperFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    # Largest cell first: bestiary_kit.report sizes its contact sheet by the FIRST creature's cell,
    # so a 32 px creature ahead of the 48 px miniboss makes it refuse the miniboss's rows. Written
    # order is unchanged for build.py, which never previews.
    bestiary_kit.run(sorted(CAST, key=lambda c: -c[2]), PALETTE, "forest")
