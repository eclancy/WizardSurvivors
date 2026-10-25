# -*- coding: utf-8 -*-
"""The Blighted Swamp's own cast (.ai/bestiary.md section 4.4). Run: python tools/art/cast_swamp.py

"The lanterns still burn. Nothing living lights them." The peat cutters and ferry folk of the fen,
its toads and striders, and the swollen things Mother Rot sheds - taken.

THE FLOOR DECIDES THE PALETTE AGAIN. The swamp's ground is linen's own shade stop (#2C3834), with
patches of its deep stop, so a body drawn in linen's shade is a hole in the floor. The linen bodies
here (the Mired, the Bog Hound, the Peat Cutter) carry their planes in linen's base and lit stops
and their darks below the floor's value; the rest of the cast is told apart from the ground by
ROW - flesh for the toad and the brood, violet for the two who carry light and sound, stone for
the mud and the turtle's shell, lichen for the reeds and the wet.

NOTHING ECHOES MOTHER ROT, whose outline is a dome - the only boss wider than it is tall. Three of
this chapter's creatures would naturally be one: the turtle's shell is drawn canted up like a slab
as it shoulders forward, the toad sits up on its forelegs so its outline is a wedge, and the frog
idol is a SQUARED carved block with two eye bosses on its flat top. Her BROOD is the opposite on
purpose: it matches her materials exactly (bone in its deep stops, flesh veins, a white specular,
the same as the sacs along her underside) and is taller than wide, so it reads as a piece of her
walking about rather than a small copy of her.

The palette characters are redefined exactly as in cast_forest.py: lichen and red cloth in the
player-pigment characters no enemy may wear, and the chapter's bolt element - poison - as a full
four-stop ramp in the lightning characters, so the Lamplighter's flame is the colour of its bolt.
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
for _ch, _hex in zip("Q1234", B.MATERIALS["red"]):
    PALETTE[_ch] = _hex
for _ch, _hex in zip("[];:", B.ELEMENTS["poison"]):
    PALETTE[_ch] = _hex

MOSS_HI, MOSS_LIT, MOSS, MOSS_SH, MOSS_DP = "U", "5", "6", "7", "8"
ROT_CORE, ROT_HOT, ROT_MID, ROT_EDGE = "[", "]", ";", ":"
WET = MOSS + MOSS_SH + MOSS_DP + MOSS_LIT + MOSS_HI
FLAME = ROT_CORE + ROT_HOT + ROT_MID + ROT_EDGE

CELL = 32
FEET = CELL - 2


def reed(g, x, y0, y1, lean=0.0, head=True):
    """A reed or a bulrush: a thin lichen stem, a lit side, and a brown seed head at the top."""
    n = int(y1 - y0)
    for i in range(n + 1):
        y = y1 - i
        x_ = x + lean * (i / float(max(1, n))) ** 1.5
        g.set(x_, y, MOSS if i % 3 else MOSS_SH)
    if head:
        top_x = x + lean
        g.set(top_x, y0, "u")
        g.set(top_x, y0 + 1, "g")
        g.set(top_x, y0 + 2, "u")
        g.set(top_x, y0 - 1, MOSS_LIT)


def ripple(g, x, y, w):
    """A ring on the water where a foot or a stone meets it: lit near edge, dark far one."""
    g.span(y, x - w, x + w, MOSS_SH)
    g.set(x - w, y, MOSS_LIT)
    g.set(x + w, y, MOSS_DP)
    g.set(x - w + 1, y - 1, MOSS_HI)


# =================================================================================================
# THE ELEVEN
# =================================================================================================

def broodling():
    """WALL, and what Mother Rot sheds (her SpawnScenePath). A swollen sac the size of a child that
    has grown two stubby legs.

    It is made of HER: bone in its deep stops, short flesh veins under the skin, one white specular,
    exactly the sacs along her underside - so what she sheds in the fight is what has been walking
    at the player all run. And it stands UPRIGHT, taller than it is wide, because she is the dome:
    the brood must be a piece of her, never a small copy of her. A seam has split down its front,
    and the mark looks out of the split.
    """
    g = G(CELL)
    # TWO STUBBY LEGS, flesh, splayed under the weight.
    for (x0, x1, tone, lit) in ((11, 14, "I", "i"), (18, 21, "i", "h")):
        mass(g, [(24, x0 + 1, x1), (27, x0, x1), (29, x0 - 1, x1 + 1)], tone, lit, "o")
    g.span(FEET, 10, 15, "I")
    g.span(FEET, 17, 22, "I")
    # THE SAC: an upright egg, swollen low.
    SB.swell(g, 6, 26, [2.4, 4.8, 6.0, 6.8, 7.0, 6.6, 5.2], "m", "n", "I", cx=16.0)
    g.disc(13.8, 12.0, 3.0, "n")                         # the light catching its upper side
    g.disc(13.0, 11.0, 1.2, "k")
    g.set(12, 10, "s")
    # VEINS, short and many, in flesh, under the skin.
    SB.veins(g, [(10, 15, 5, 1.0), (18, 12, 4, -1.0), (19, 19, 4, 1.0), (11, 21, 5, -1.0),
                 (14, 23, 4, 1.0), (20, 16, 3, 1.0), (9, 18, 3, -1.0)], "I")
    SB.veins(g, [(11, 16, 4, -1.0), (17, 20, 3, 1.0), (15, 9, 3, 1.0)], "i")
    # THE NUB at the top, where it hung off her.
    for (x, y, ch) in ((15, 5, "i"), (16, 5, "I"), (16, 4, "i"), (17, 6, "I")):
        g.set(x, y, ch)
    # THE SPLIT SEAM down its front: dark, flesh at its lips, widest where the mark is.
    seam = [(17, 9), (17, 10), (18, 11), (18, 12), (18, 13), (18, 14), (19, 15), (19, 16), (19, 17),
            (19, 18), (18, 19), (18, 20), (18, 21), (17, 22)]
    for (x, y) in seam:
        g.set(x, y, "o")
        g.set(x - 1, y, "h" if y % 3 else "i")
        g.set(x + 1, y, "I")
    for (x, y) in ((18, 12), (19, 12), (19, 13)):
        g.set(x, y, "o")                                 # the split gapes open here
    rim(g, 4, 29, skip="o")
    contact(g, 9, 23)
    eyes(g, 13, 18, 19, "Y", "y")
    return g.rows()


def mired():
    """BULK. A peat cutter sunk to the waist in the bog and still walking - the bog comes with it.

    Reeds have grown up out of its back, taller than its head: that is the attachment, and the
    reason it reads as the swamp's and nothing else's. Below the waist it is not legs but a wake of
    mud thrown up round it - lumpy, clods flying off it, standing water on it - in stone and lichen.
    Never a smooth mound: a smooth low mound is a dome, and the dome is Mother Rot's.
    """
    g = G(CELL)
    # REEDS first, behind the shoulders, rising above the head line, in lichen's lit stops so they
    # read against a floor that is linen's shade.
    for (x, y0, lean) in ((9, 3, -1.5), (11, 2, -0.5), (12, 5, 0.6), (8, 7, -2.6)):
        reed(g, x, y0, 15, lean)
    for (x, y) in ((8, 6), (10, 9), (11, 12), (7, 11)):
        g.set(x, y, MOSS_LIT)
    # THE MUD: a lumpy wake it wades in - an uneven top edge, clods thrown off either side.
    for x in range(5, 27):
        bump = (2.0 if x in (7, 8, 14, 15, 16, 22, 23) else 0.0) + (1.0 if x in (11, 19, 25) else 0.0)
        top = 25.0 - bump - 2.2 * math.sin((x - 5) / 21.0 * math.pi)
        for y in range(int(round(top)), FEET + 1):
            g.set(x, y, "N")
        g.set(x, int(round(top)), "p")
    for (x, y) in ((8, 24), (12, 23), (19, 23), (23, 26), (10, 28), (17, 27), (6, 27), (21, 29)):
        g.set(x, y, "J")
        g.set(x - 1, y - 1, "P")
    for (x, y) in ((3, 24), (4, 22), (28, 23), (27, 21), (2, 27)):
        g.set(x, y, "N")                                 # clods flying off
        g.set(x, y - 1, "p")
    for (x, y, w) in ((15, 26, 2), (9, 27, 1), (22, 25, 1)):
        ripple(g, x, y, w)                               # standing water on the mud
    # THE TORSO, out of the mud to the waist, leaning into the wade.
    mass(g, [(10, 11, 18), (14, 10, 19), (19, 10, 20), (23, 9, 21)], "A", "e", "o")
    fold(g, [(12, 12), (11, 18), (12, 22)], "o", "e")
    fold(g, [(16, 13), (17, 20)], "o", "l")
    for (x, y) in ((14, 16), (13, 19), (18, 17)):
        g.set(x, y, "e")
    g.span(22, 9, 21, "j")                               # where the mud has climbed it
    g.set(10, 21, "N")
    g.set(19, 21, "p")
    # ARMS: one reaching forward, dragging through the mud, one hanging back.
    _limb(g, 18, 13, 23, 20, 1.2, "A", "e", "o")
    for (x, y, ch) in ((23, 21, "n"), (24, 21, "m"), (24, 22, "j")):
        g.set(x, y, ch)
    _limb(g, 11, 13, 9, 19, 1.0, "o", "A", "o")
    # THE HEAD: a cap of sodden cloth, the void under it.
    mass(g, [(6, 14, 18), (7, 13, 20), (10, 13, 20), (11, 14, 19)], "A", "e", "o")
    roster.void(g, 8, 11, 1.8, 1.2, cx=17.4)
    g.set(13, 7, "l")
    rim(g, 2, 29, skip="o" + WET + "ug")
    contact(g, 4, 27)
    eyes(g, 9, 16, 18, "Y", "y")
    return g.rows()


def strider():
    """FAST, and the Reed Piper's minion. A water-strider grown huge.

    A small body slung between very long splayed legs: MOSTLY HOLES, the opposite of every solid
    body in the fen, so it reads in a crowd by what the floor shows through it. Each foot rests in
    its own dimple on the water. Stone body, linen under it, bone at every joint.
    """
    g = G(CELL)
    # LEGS first: four long ones arching up from the body to a knee and down to the water, two
    # short ones held forward. Far legs a stop darker.
    for (pts, tone) in (([(15, 16), (9, 7), (2, 28)], "N"), ([(17, 16), (23, 6), (29, 27)], "p"),
                        ([(13, 17), (8, 13), (6, 29)], "j"), ([(18, 17), (21, 14), (24, 29)], "N"),
                        ([(20, 16), (24, 18), (26, 22)], "p"), ([(19, 17), (21, 21), (20, 24)], "j")):
        twig(g, pts, tone, "J")
        for (jx, jy) in pts[1:2]:
            g.set(jx, jy, "k")                            # bone at the knee
            g.set(jx, jy - 1, "s")
    # FEET in dimples on the water.
    for (x, y) in ((2, 29), (29, 28), (6, 30), (24, 30)):
        ripple(g, x, y, 1)
    # THE BODY: small, long, slung low between the knees.
    blob(g, 15.5, 15.5, 4.6, 2.2, "N", "P", "j", deep="J")
    g.span(17, 12, 19, "e")                               # the linen underside
    g.set(12, 17, "l")
    for x in (13, 16, 18):
        g.set(x, 15, "J")                                 # segment joints
    # THE HEAD, at the front, small, with short feelers.
    blob(g, 21.5, 14.5, 1.8, 1.6, "N", "P", "j")
    g.line(22, 13, 26, 10, "j")
    g.line(23, 13, 27, 12, "N")
    rim(g, 5, 29, skip="o" + WET)
    contact(g, 11, 20)
    eyes(g, 14, 21, 23, "Y", "y")
    return g.rows()


def mudback():
    """HEAVY. A snapping turtle the size of a cart, shouldering forward.

    The shell is the shield plane, CANTED UP - high at the back, low over the head - with a
    sawtooth keel along its back: a tilted plate, never a dome (Mother Rot is the dome). Its back is
    still a shell's - convex, the corners rounded, plated in scutes - so it reads as an animal and
    not a roof on legs. The head comes out from under the front edge, huge and beaked.
    """
    g = G(CELL)
    # LEGS: thick, short, clawed, mud on them.
    for (x0, x1, tone, lit) in ((5, 9, "A", "e"), (18, 22, "A", "e")):
        mass(g, [(23, x0, x1), (27, x0 - 1, x1), (29, x0 - 1, x1 + 1)], tone, lit, "o")
        for cx_ in range(x0 - 1, x1 + 2, 2):
            g.set(cx_, FEET, "n")
        g.set(x0, 27, "j")
        g.set(x1 - 1, 26, "N")
    # THE TAIL, short and pointed, behind.
    g.line(4, 22, 1, 24, "A")
    g.set(1, 24, "e")
    # THE SHELL: canted down to the front, its top edge bowed up, corners rounded.
    tops = {}
    for x in range(3, 25):
        t = (x - 3) / 21.0
        top = 8.0 + 8.0 * t - 2.6 * math.sin(t * math.pi)
        if x in (3, 24):
            top += 2
        bot = 19.0 + 4.5 * t - (1 if x == 3 else 0)
        tops[x] = int(round(top))
        for y in range(int(round(top)), int(round(bot)) + 1):
            g.set(x, y, "N")
        g.set(x, int(round(top)), "p")
        g.set(x, int(round(top)) + 1, "p" if x < 14 else "N")
        g.set(x, int(round(bot)), "k" if x % 4 else "n")      # the plastron's pale edge
        g.set(x, int(round(bot)) - 1, "J")
    # The keel: a sawtooth along the top edge.
    for x in range(5, 23, 3):
        g.set(x, tops[x] - 1, "p")
        g.set(x + 1, tops[x] - 1, "P")
    # The scutes: plates on the shell's face, each a lit edge over a dark seam, uneven.
    for pts in ([(7, 10), (6, 14), (8, 18)], [(12, 10), (11, 15), (13, 20)], [(17, 12), (16, 17), (18, 21)],
                [(4, 15), (9, 14), (13, 16), (18, 17), (23, 19)]):
        fold(g, pts, "J", "p")
    for (x, y) in ((9, 12), (14, 13), (19, 15), (10, 17), (15, 19)):
        g.set(x, y, "P")
    # THE HEAD, out from under the front edge: heavy, beaked, low.
    blob(g, 25.5, 21.0, 3.6, 2.8, "A", "e", "o")
    for (x, y, ch) in ((28, 21, "k"), (29, 22, "n"), (28, 23, "m"), (29, 21, "s"), (27, 23, "o")):
        g.set(x, y, ch)                                  # the hooked beak, bone
    g.line(22, 23, 27, 24, "o")                          # the jaw line
    for (x, y) in ((23, 19), (24, 22)):
        g.set(x, y, "l")
    rim(g, 5, 29, skip="o")
    contact(g, 2, 27)
    eyes(g, 20, 24, 26, "Y", "y")
    return g.rows()


def lamplighter():
    """RANGED. The one who keeps the lanterns burning: tall and hooded, with a cold flame hanging
    loose from a hooked pole - detached from it, daylight between - in the poison ramp it throws.

    NO CAGE, NO LANTERN: the bog lanterns of the side event must never look like an enemy, so the
    flame hangs free. Dark linen under a violet hood and mantle; the Reed Piper beside it is violet
    all through, so the two hooded figures differ in material as well as in what they carry.
    """
    g = G(CELL)
    # THE POLE first: from the ground, past the hand, up above the head to a hook bent forward.
    g.line(19, 29, 21, 4, "p")
    g.line(20, 29, 22, 4, "J")
    for (x, y, ch) in ((22, 3, "p"), (23, 2, "p"), (24, 2, "N"), (25, 3, "p"), (25, 4, "j")):
        g.set(x, y, ch)
    # THE ROBE: tall, narrow, dark linen, a ragged wet hem.
    mass(g, [(11, 12, 17), (17, 11, 18), (24, 10, 19), (28, 9, 19)], "A", "e", "o")
    fold(g, [(13, 12), (12, 20), (12, 28)], "o", "e")
    fold(g, [(16, 15), (16, 28)], "o", None)
    for (x, d) in zip(range(9, 20), [1, 0, 2, 1, 1, 0, 2, 1, 0, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 28 + k, "A" if k < d else MOSS_SH)
    # A belt, and the trade hung off it: a snuffer on a cord and a coil of wick.
    g.span(20, 10, 18, "X")
    g.set(10, 20, "v")
    g.line(12, 21, 12, 24, "q")
    g.rect(11, 25, 13, 26, "R")
    g.set(11, 25, "r")
    for (x, y, ch) in ((16, 21, "l"), (17, 21, "e"), (16, 22, "e"), (17, 22, "A"), (15, 22, "A")):
        g.set(x, y, ch)
    # Patches and wear on the robe, uneven.
    for (x, y) in ((14, 25), (17, 27), (11, 17)):
        g.set(x, y, "e")
        g.set(x + 1, y + 1, "o")
    # THE MANTLE AND HOOD: violet, over the shoulders.
    mass(g, [(5, 13, 16), (6, 12, 18), (9, 11, 18), (12, 10, 19), (14, 11, 18)], "w", "v", "X")
    fold(g, [(12, 9), (11, 14)], "X", "V")
    roster.void(g, 7, 10, 1.6, 1.0, cx=15.8)
    g.set(13, 5, "V")
    # THE HAND on the pole.
    _limb(g, 17, 13, 19, 16, 0.9, "w", "v", "X")
    g.rect(19, 15, 20, 16, "n")
    g.set(19, 15, "k")
    # THE FLAME, hanging loose below the hook, with daylight between: a teardrop in the poison ramp.
    for (y, x0, x1, tone) in ((7, 25, 25, ROT_EDGE), (8, 25, 26, ROT_MID), (9, 24, 26, ROT_MID),
                              (10, 24, 27, ROT_MID), (11, 24, 27, ROT_EDGE), (12, 25, 26, ROT_EDGE)):
        g.span(y, x0, x1, tone)
    g.set(25, 9, ROT_HOT)
    g.set(25, 10, ROT_CORE)
    g.set(26, 10, ROT_HOT)
    rim(g, 2, 29, skip="o" + FLAME + WET)
    contact(g, 9, 21)
    eyes(g, 8, 15, 17, "Y", "y")
    return g.rows()


def frog_idol():
    """TURRET. A frog's head he carved in stone and sank in the bog. BUILT, so it burns: fire in
    both eyes, and a glint of it far back in the wide mouth.

    Half-sunk, the mouth wide. The head is a frog's - two half-round eye bosses on top, a snout,
    a grin that runs back past the eyes - but the base it is cut from stays squared and the sides
    stay flat, chisel-straight: straight edges say MADE, and they keep the outline a carving rather
    than a dome, which is Mother Rot's.
    """
    g = G(CELL)
    # THE WATER it is sunk in.
    for (x, y, w) in ((15, 29, 12), (5, 27, 2), (27, 27, 2)):
        ripple(g, x, y, w)
    g.span(30, 4, 27, MOSS_DP)
    # THE HEAD: flat sides and a squared base, the top sloping down to a blunt snout at the front.
    for x in range(5, 27):
        t = (x - 5) / 21.0
        top = 15.0 + 3.5 * max(0.0, t - 0.55) / 0.45
        if x in (5, 26):
            top += 1
        for y in range(int(round(top)), 29):
            g.set(x, y, "N")
        g.set(x, int(round(top)), "P" if x < 20 else "p")
        if x < 8:
            for y in range(int(round(top)) + 1, 29):
                g.set(x, y, "p")
        if x > 23:
            for y in range(int(round(top)) + 1, 29):
                g.set(x, y, "j" if x < 26 else "J")
    # THE EYE BOSSES: half-round, on top at the back, one fire eye in each socket.
    for (cx_, tone) in ((9.5, "N"), (17.5, "N")):
        g.disc(cx_, 14.0, 3.0, tone)
        g.disc(cx_ - 0.8, 13.0, 1.6, "p")
        g.set(cx_ - 1.5, 11.5, "P")
        g.rect(cx_ - 1, 13, cx_ + 1, 14, "o")
        g.set(cx_ + 2, 15, "J")
    # THE MOUTH: a wide grin from the snout back past the eyes, lipped.
    for x in range(7, 26):
        y = 22 + (1 if x < 12 else 0) - (1 if x > 22 else 0)
        g.set(x, y, "o")
        g.set(x, y - 1, "P" if x < 18 else "p")
        g.set(x, y + 1, "J")
    g.set(15, 22, "~")                                   # the glint far back in the throat
    # THE RIDGES a frog has from the eye back along its head, carved as a lit edge over a groove.
    fold(g, [(12, 17), (16, 18), (20, 19)], "j", "P")
    fold(g, [(7, 19), (9, 20)], "j", "p")
    # A crack across the brow, and a chip off the front corner.
    g.line(13, 16, 15, 20, "J")
    cut(g, (26, 18))
    # THE NOSTRILS on the snout, and chisel marks and moss: uneven, so the block is old.
    g.set(23, 18, "o")
    g.set(25, 19, "o")
    for (x0, y0, x1, y1) in ((10, 24, 12, 27), (19, 24, 17, 27)):
        g.line(x0, y0, x1, y1, "j")
    for (x, y, ch) in ((7, 15, MOSS_LIT), (8, 16, MOSS), (13, 16, MOSS_SH), (21, 17, MOSS),
                       (6, 26, MOSS_SH), (25, 25, MOSS_SH)):
        g.set(x, y, ch)
    rim(g, 9, 29, skip="o" + WET)
    contact(g, 4, 27)
    g.set(9, 14, "+")
    g.set(10, 14, "=")
    g.set(17, 14, "-")
    g.set(18, 14, "~")
    return g.rows()


def bog_hound():
    """CHARGER. A gaunt hound, belly to the mud, about to spring.

    Coiled low, ears flat back, a long thin tail held out behind: a low wedge that is all haunch at
    the back and head at the front, the gathering the charge streak warns of. Linen in its deep
    stops, mud caked on its legs and belly.
    """
    g = G(CELL)
    mid, lit, dark = "A", "e", "o"
    # THE TAIL, long, thin, held out and up behind.
    twig(g, [(7, 20), (4, 17), (3, 12), (5, 9)], "A", "o")
    g.set(5, 8, "e")
    # LEGS, folded: the hind ones gathered under the haunch, the fore ones reaching out flat.
    _limb(g, 9, 23, 7, 28, 1.6, mid, lit, dark)
    _limb(g, 7, 28, 11, FEET - 1, 1.0, mid, lit, dark)
    _limb(g, 20, 24, 25, 29, 1.2, mid, lit, dark)
    _limb(g, 18, 25, 21, FEET - 1, 1.0, "o", "A", dark)
    for (x, y) in ((6, 28), (8, 29), (24, 29), (20, 29)):
        g.set(x, y, "j")                                 # mud caked on them
    g.span(FEET, 9, 13, "J")
    g.span(FEET, 24, 27, "J")
    # THE BODY: haunch high and gathered, the back sloping down to the shoulders.
    SB.swell(g, 17, 26, [3.0, 4.6, 5.0, 4.4, 3.4], mid, lit, dark, cx=10.0)
    for x in range(12, 21):
        t = (x - 12) / 8.0
        top = 19.0 + 1.6 * t
        for y in range(int(round(top)), 26):
            g.set(x, y, mid)
        g.set(x, int(round(top)), lit)
    # Gaunt: ribs along the flank, and a spine of raised hair.
    for (x, y0, y1) in ((13, 21, 24), (15, 21, 25), (17, 22, 25)):
        g.line(x, y0, x, y1, "o")
        g.set(x - 1, y0, "e")
    for x in range(8, 19, 2):
        g.set(x, 18 + (x - 8) // 6, "l")
    for (x, y) in ((12, 25), (15, 26), (9, 25)):
        g.set(x, y, "j")                                 # mud on the belly
    # THE HEAD, low and forward, ears pinned flat back.
    mass(g, [(19, 21, 25), (21, 21, 28), (23, 22, 28), (24, 23, 27)], mid, lit, dark, rim=False)
    g.line(20, 19, 17, 18, mid)                           # the ears, flat
    g.line(20, 20, 18, 19, lit)
    g.set(28, 22, "o")
    g.set(27, 24, "S")                                   # a tooth
    g.span(25, 23, 26, "o")
    rim(g, 8, 29, skip="o")
    contact(g, 5, 28)
    eyes(g, 21, 24, 26, "Y", "y")
    return g.rows()


def blister_toad():
    """EXPLODER. A toad swollen with blisters that burst.

    The blistered back is the bulge. It SITS UP on its forelegs, so its outline is a wedge - head
    high at the front, the back falling to the haunch - and not a dome: the dome is Mother Rot's.
    Flesh in its deep stops, lichen in the folds; the blisters are round and lit on top, and one has
    burst, which is a dark crater and not blood - nothing in this game bleeds.

    It FACES LEFT, like every exploder here, because its stand-in scene does.
    """
    g = G(CELL)
    mid, lit, dark = "i", "h", "I"
    # THE HIND LEG, folded at the back (right).
    blob(g, 22.0, 25.0, 4.2, 3.6, mid, lit, dark)
    g.line(18, 29, 26, 29, "I")
    for x in (19, 21, 23, 25):
        g.set(x, FEET, "i")
    # THE BODY: a wedge, high at the head end and falling to the haunch.
    for x in range(6, 25):
        t = (x - 6) / 18.0
        top = 12.0 + 8.0 * t
        bot = 26.0 + 1.5 * t
        for y in range(int(round(top)), int(round(bot)) + 1):
            g.set(x, y, mid)
        g.set(x, int(round(top)), lit)
        g.set(x, int(round(bot)), dark)
    # THE FORELEGS, propping it up at the front.
    for (x0, tone) in ((8, "i"), (12, "I")):
        g.line(x0, 22, x0 - 1, 29, tone)
        g.line(x0 + 1, 22, x0, 29, "I")
        g.span(FEET, x0 - 3, x0 + 1, tone)
    # THE BELLY: lichen-mottled, paler, under the jaw.
    for (x, y) in ((8, 20), (9, 21), (10, 22), (11, 21), (9, 19), (12, 23)):
        g.set(x, y, MOSS if (x + y) % 2 else MOSS_SH)
    # THE MOUTH: a wide line across the front of the head.
    g.line(5, 17, 12, 18, "o")
    g.set(5, 16, "h")
    # THE EYE BUMPS on top of the head, and the mark in them.
    for x in (8, 12):
        g.rect(x - 1, 10, x + 1, 12, mid)
        g.set(x - 1, 10, lit)
        g.set(x + 1, 12, dark)
    # THE BLISTERS: round, lit on top, along the back - and one burst.
    for (bx, by, br) in ((14, 15, 2.2), (18, 17, 2.6), (22, 20, 2.0), (16, 21, 1.6), (11, 15, 1.4)):
        g.disc(bx, by, br + 0.6, "I")
        g.disc(bx, by, br, "h")
        g.disc(bx - 0.6, by - 0.6, br * 0.5, "f")
        g.set(bx - 1, by - 1, "F")
    for (x, y) in ((20, 23), (21, 23), (20, 24)):
        g.set(x, y, "o")                                 # the burst one: a crater
    g.set(19, 23, MOSS_SH)
    g.set(21, 24, MOSS)
    for (x, y) in ((16, 25), (12, 24), (7, 24)):
        g.set(x, y, "I")                                 # warts
    rim(g, 9, 29, skip="o" + WET)
    contact(g, 4, 27)
    eyes(g, 11, 8, 12, "y", "Y")
    return g.rows()


def peat_cutter():
    """SLAMMER. A cutter raising a long peat spade overhead, blade highest - the ring on the ground
    is it coming down.

    Upright on his own legs, where the Mired beside him is sunk to the waist; linen coat, a flat
    cap, and a steel blade with the cutter's wing on its side, the one bright edge on him.
    """
    g = G(CELL)
    # THE SPADE first: the long handle from behind the shoulder up through the hands to the blade.
    roster.shaft(g, 9, 17, 20, 6, "e", "A", "o", width=2)
    # the blade: a narrow steel plate with its wing, highest in the cell
    g.rect(20, 2, 23, 6, "R")
    g.line(20, 2, 23, 2, "r")
    g.line(20, 2, 20, 6, "t")
    g.line(23, 2, 23, 6, "q")
    g.rect(24, 2, 25, 4, "R")                            # the wing
    g.set(24, 2, "r")
    g.set(25, 4, "q")
    g.set(21, 3, "q")
    g.set(22, 5, "q")
    g.set(21, 7, "o")                                    # where it is socketed on the handle
    # LEGS, apart, and boots in the mud.
    for (lx, tone, lit) in ((11, "A", "e"), (17, "A", "e")):
        g.rect(lx, 23, lx + 3, 28, tone)
        g.line(lx, 23, lx, 28, lit)
        g.rect(lx - 1, 29, lx + 4, FEET, "j")
        g.set(lx - 1, 29, "N")
    g.rect(15, 25, 16, 29, "o")
    # THE BODY: a linen coat, square in the shoulder.
    SB.swell(g, 11, 24, [5.6, 6.0, 5.2, 4.8, 5.2], "e", "l", "A", cx=15.0)
    fold(g, [(12, 13), (11, 19), (12, 24)], "A", "l")
    fold(g, [(17, 13), (18, 23)], "A", None)
    g.span(19, 10, 20, "o")                              # a rope belt
    g.set(11, 19, "u")
    g.line(18, 20, 19, 23, "u")
    # A pocket, a patch on the elbow-worn coat, and peat dirt down the front.
    g.rect(12, 20, 14, 22, "A")
    g.set(12, 20, "e")
    g.span(20, 13, 14, "o")
    for (x, y) in ((16, 14), (17, 16), (13, 16)):
        g.set(x, y, "j")
    g.rect(18, 15, 19, 16, "l")
    g.set(19, 16, "A")
    # ARMS: both up, gripping the handle overhead.
    _limb(g, 10, 12, 14, 9, 1.4, "e", "l", "A")
    _limb(g, 20, 12, 18, 7, 1.4, "e", "l", "A")
    for (x, y) in ((14, 9), (17, 6)):
        g.rect(x, y, x + 1, y + 1, "n")
        g.set(x, y, "k")
    # THE HEAD: a flat cap, the void under its peak.
    g.rect(13, 8, 17, 11, "A")
    g.span(7, 12, 18, "j")
    g.span(8, 12, 18, "N")
    g.set(12, 7, "p")
    roster.void(g, 9, 11, 1.6, 1.0, cx=15.6)
    rim(g, 2, 29, skip="o")
    contact(g, 9, 22)
    eyes(g, 10, 14, 16, "Y", "y")
    return g.rows()


def reed_piper():
    """SUMMONER, who calls the Mire Striders out of the water. A piper on bone reed-pipes.

    The pipes are the attachment: a stepped row of bone tubes held up to the face at hand height,
    the one bright thing on a figure that is violet all through - the Lamplighter beside it wears
    its violet only at the hood.
    """
    g = G(CELL)
    # THE ROBE: violet, a little hunched over the pipes, a ragged wet hem.
    mass(g, [(10, 11, 16), (15, 9, 18), (22, 8, 19), (27, 7, 20), (28, 7, 20)], "w", "v", "X")
    fold(g, [(11, 11), (10, 19), (10, 27)], "X", "V")
    fold(g, [(14, 14), (15, 27)], "x", "v")
    for (x, d) in zip(range(6, 21), [1, 2, 1, 0, 2, 1, 1, 2, 0, 1, 2, 1, 0, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 28 + k, "X" if k < d else MOSS_SH)
    cut(g, (9, 29), (9, 30), (15, 29), (15, 30))
    # A linen sash, a reed tucked in it.
    g.line(8, 19, 19, 17, "e")
    g.line(8, 20, 19, 18, "A")
    reed(g, 9, 14, 22, -1.0)
    # THE HOOD.
    mass(g, [(5, 12, 15), (6, 11, 17), (9, 10, 18), (11, 11, 18)], "x", "w", "X")
    roster.void(g, 7, 10, 1.6, 1.1, cx=16.0)
    g.set(12, 5, "V")
    # ARMS up to the pipes.
    _limb(g, 16, 13, 19, 12, 1.0, "x", "w", "X")
    _limb(g, 12, 13, 18, 14, 1.0, "X", "x", "o")
    # THE PIPES: a stepped row of bone tubes at the mouth, tallest nearest, bound across.
    for i, h in enumerate((8, 7, 6, 5, 4)):
        x = 19 + i
        g.line(x, 7, x, 7 + h, "k" if i % 2 == 0 else "n")
        g.set(x, 7, "s" if i % 2 == 0 else "k")
        g.set(x, 7 + h, "m")
    g.line(19, 10, 23, 10, "u")                           # the binding
    g.set(18, 8, "n")                                     # the mouthpiece end, at the void
    for (x, y) in ((19, 13), (20, 12)):
        g.set(x, y, "n")                                  # the fingers on the long pipes
    rim(g, 4, 29, skip="o" + WET + "ug")
    contact(g, 6, 21)
    eyes(g, 8, 14, 16, "Y", "y")
    return g.rows()


def ferryman():
    """MINIBOSS, on the 48 cell. His jailer on the fen: tall, hooded, a chain at his belt.

    The punt-pole is the silhouette: carried diagonally across him, longer than he is, so it runs
    out past him at both ends - nothing else in the swamp has one long straight line through it.
    Linen robes under a violet cowl, wet at the hem; steel at the chain and the pole's iron shoe.
    """
    c = 48
    feet = c - 2
    g = G(c)
    # THE ROBE: tall, long, linen, a ragged hem dark with water.
    mass(g, [(14, 17, 27), (20, 16, 29), (30, 15, 31), (40, 13, 32), (44, 12, 33)], "A", "e", "o")
    for pts in ([(19, 15), (18, 28), (16, 44)], [(25, 17), (26, 30), (27, 44)], [(22, 24), (22, 44)]):
        fold(g, pts, "o", "e")
    for (x, y) in ((18, 34), (28, 38), (21, 21)):
        g.set(x, y, "l")
    for (x, d) in zip(range(12, 34), [1, 2, 1, 0, 2, 1, 2, 1, 0, 1, 2, 2, 1, 0, 1, 2, 1, 0, 2, 1, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 44 + k, MOSS_SH if k < d else MOSS)
    cut(g, (16, 45), (16, 46), (24, 45), (24, 46), (29, 45), (29, 46))
    # THE COWL and MANTLE: violet, deep, over the head and shoulders.
    mass(g, [(4, 21, 25), (5, 19, 27), (8, 17, 29), (13, 15, 31), (17, 14, 32), (20, 15, 31)], "w", "v", "X")
    fold(g, [(18, 9), (16, 17)], "X", "V")
    fold(g, [(28, 11), (30, 18)], "X", None)
    roster.void(g, 8, 14, 2.8, 1.8, cx=24.4)
    g.span(7, 20, 28, "X")
    g.set(20, 5, "V")
    # THE BELT, and THE CHAIN hanging from it in a loop, a padlock at its end.
    g.span(28, 15, 31, "o")
    g.span(29, 15, 31, "X")
    for i in range(16):
        t = i / 15.0
        x = 27 + 6 * t
        y = 29 + 9 * math.sin(t * math.pi) * 0.9
        g.set(x, y, "r" if i % 2 else "q")
        g.set(x, y + 1, "R" if i % 2 else "o")
    g.rect(32, 30, 34, 33, "R")
    g.set(32, 30, "r")
    g.set(33, 32, "o")
    g.line(32, 29, 34, 29, "q")
    # THE PUNT-POLE, diagonally across him, past him at both ends; its iron shoe at the foot.
    roster.shaft(g, 5, 43, 43, 3, "p", "N", "J", width=3)
    for (x, y, ch) in ((4, 44, "r"), (5, 45, "R"), (3, 45, "q"), (4, 46, "q")):
        g.set(x, y, ch)
    # HANDS on the pole: pale, the drowned's pallor.
    for (x, y) in ((31, 15), (17, 30)):
        g.rect(x, y, x + 1, y + 1, "n")
        g.set(x, y, "k")
        g.set(x + 1, y + 1, "m")
    _limb(g, 28, 16, 31, 16, 1.2, "w", "v", "X")
    _limb(g, 18, 22, 18, 29, 1.2, "x", "w", "X")
    rim(g, 2, 45, skip="o" + WET)
    contact(g, 11, 34, y=feet)
    eyes(g, 11, 23, 26, "Y", "y")
    return g.rows()


CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    ("swamp-mired", mired, 32, (0.6, 2.2, 2), "SwampMiredFrames"),
    ("swamp-strider", strider, 32, (1.4, 3.0, 3), "SwampStriderFrames"),
    ("swamp-mudback", mudback, 32, (0.5, 2.8, 1), "SwampMudbackFrames"),
    ("swamp-broodling", broodling, 32, (0.6, 2.6, 2), "SwampBroodlingFrames"),
    ("swamp-lamplighter", lamplighter, 32, (0.9, 2.2, 3), "SwampLamplighterFrames"),
    ("swamp-frog-idol", frog_idol, 32, (0.3, 1.6, 1), "SwampFrogIdolFrames"),
    ("swamp-bog-hound", bog_hound, 32, (1.8, 4.0, 3), "SwampBogHoundFrames"),
    # Faces left (see blister_toad), so it leans into its strike and is knocked back the other way.
    ("swamp-blister-toad", blister_toad, 32, (1.1, -2.0, -3), "SwampBlisterToadFrames"),
    ("swamp-peat-cutter", peat_cutter, 32, (0.6, 4.0, 1), "SwampPeatCutterFrames"),
    ("swamp-reed-piper", reed_piper, 32, (0.9, 2.6, 2), "SwampReedPiperFrames"),
    ("swamp-ferryman", ferryman, 48, (0.5, 4.2, 1), "SwampFerrymanFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    # Largest cell first: bestiary_kit.report sizes its contact sheet by the first creature's cell.
    bestiary_kit.run(sorted(CAST, key=lambda c: -c[2]), PALETTE, "swamp")
