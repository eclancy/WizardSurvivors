# -*- coding: utf-8 -*-
"""The Sunken Cave's own cast (.ai/bestiary.md section 4.3). Run: python tools/art/cast_cave.py

"His shadow followed the water down into the dark." The miners and pearl divers who worked these
tunnels, and the blind pale things that lived in them, taken.

THE FLOOR DECIDES THE PALETTE. The cave's ground is stone's own shade stop (#141A26), so a body
drawn in the deep half of stone is a hole in the floor with two eyes in it. Cave life is pale -
bone is this chapter's signature row, on the bat's wings, the crab's claw, the cricket's legs and
the fish's spines - and the stone things (the bat's fur, the crab's shell, the idol) carry their
weight in stone's lit and hi stops and the rim, never its shade. Lichen is the wet: the Drowned's
hair, the diver's, the fish. Steel is what the miners brought down with them.

NO BELLS. The Hollow Choir is a bell - narrow at the top, flaring smooth to a wide hem - so nothing
here may be one. The Drowned in particular, whose hair widens to its hem, is drawn hunched and
asymmetric and full of gaps, the opposite of a bell's smooth closed outline.

The palette characters are redefined exactly as in cast_forest.py, and for the same reason: lichen
and red cloth in the player-pigment characters no enemy may wear, and the chapter's bolt element -
water here - in the lightning characters, so the pearl is the colour of what the diver throws.
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
for _ch, _hex in zip("[];:", B.ELEMENTS["water"]):
    PALETTE[_ch] = _hex

MOSS_HI, MOSS_LIT, MOSS, MOSS_SH, MOSS_DP = "U", "5", "6", "7", "8"
RED_HI, RED_LIT, RED, RED_SH, RED_DP = "Q", "1", "2", "3", "4"
WATER_CORE, WATER_HOT, WATER_MID, WATER_EDGE = "[", "]", ";", ":"
WET = MOSS + MOSS_SH + MOSS_DP + MOSS_LIT + MOSS_HI

CELL = 32
FEET = CELL - 2


def strand(g, x0, y0, x1, y1, tone, wave=0.0):
    """One lank strand of wet hair, hanging: a one-pixel line with a slight sway."""
    n = int(max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        x = x0 + (x1 - x0) * t + wave * math.sin(t * math.pi * 1.5)
        y = y0 + (y1 - y0) * t
        g.set(x, y, tone)


def drop(g, x, y):
    """A falling drop, detached: one pale pixel with a darker tail above it."""
    g.set(x, y, MOSS_HI)
    g.set(x, y - 1, MOSS_SH)


# =================================================================================================
# THE ELEVEN
# =================================================================================================

def drowned():
    """BULK. Whoever the water took when it rose: hunched, sodden, its lank hair hanging in a
    curtain past its face and widening to the hem, and the two points of the mark showing THROUGH
    the hair, with a strand hanging between them.

    A QUESTION MARK, NOT A BELL. The Choir is a smooth closed bell, and a figure wrapped in hair
    that widens to its hem is one already - the first pass filled black was a rounded dome. So the
    back is hunched up high, the head hangs forward and low in front of the shoulders, the arms
    dangle clear of the body, and the hair falls from the head in loose strands with the floor
    showing between them, past the outline rather than inside it.
    """
    g = G(CELL)
    # THE BODY: the back hunched up high, the front falling away under the hanging head.
    mass(g, [(7, 10, 12), (8, 8, 14), (10, 7, 16), (14, 6, 17), (20, 7, 16), (26, 6, 17), (28, 6, 18)],
         "E", "e", "A")
    fold(g, [(9, 10), (8, 18), (9, 27)], "A", "e")
    fold(g, [(13, 11), (13, 19), (12, 28)], "A", "e")
    g.set(10, 8, "l")
    g.set(9, 9, "l")
    g.set(12, 8, "e")
    # A ragged, dragging hem.
    for (x, d) in zip(range(5, 19), [1, 2, 2, 1, 0, 1, 2, 1, 0, 2, 1, 2, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 28 + k, "A" if k < d else "E")
    cut(g, (9, 29), (9, 30), (14, 29), (14, 30))
    g.span(FEET, 17, 19, "n")                           # a bare, drowned-pale foot
    g.set(17, FEET, "k")
    # THE ARMS, dangling forward from the hunched shoulder, clear of the body, pale at the hands.
    _limb(g, 16, 13, 20, 22, 1.0, "E", "e", "A")
    _limb(g, 14, 14, 15, 23, 0.9, "A", "E", "o")
    for (x, y, ch) in ((20, 23, "k"), (21, 23, "n"), (20, 24, "n"), (21, 25, "m"), (15, 24, "n"),
                       (16, 25, "m")):
        g.set(x, y, ch)

    # THE HEAD, hung forward and low in front of the shoulders.
    mass(g, [(11, 17, 21), (12, 16, 23), (15, 16, 23), (16, 17, 22)], MOSS_SH, MOSS_LIT, MOSS_DP)
    roster.void(g, 14, 17, 2.0, 1.5, cx=20.6)
    _limb(g, 14, 10, 17, 12, 1.2, "E", "e", "A")         # the neck, craned forward and down
    # THE HAIR: lank strands from the head, hanging past the face to below the hands, with the
    # floor showing between them.
    for (x0, y0, x1, y1, tone, wave) in (
            (23, 13, 25, 24, MOSS_SH, 0.5), (22, 13, 23, 22, MOSS, -0.4), (21, 14, 21, 21, MOSS_DP, 0.0),
            (24, 14, 26, 21, MOSS_DP, 0.3), (19, 13, 18, 23, MOSS, 0.5), (18, 12, 16, 19, MOSS_SH, 0.0),
            (17, 12, 15, 17, MOSS_DP, -0.3)):
        strand(g, x0, y0, x1, y1, tone, wave)
    for (x, y) in ((18, 11), (20, 11), (17, 12)):
        g.set(x, y, MOSS_LIT)                            # wet sheen on the crown, top-left
    g.set(19, 11, MOSS_HI)
    # DROPS, falling off the ends of the hair and the hands.
    for (x, y) in ((25, 27), (22, 28), (18, 26), (26, 23)):
        drop(g, x, y)

    rim(g, 6, 28, skip="o" + WET)
    contact(g, 5, 21)
    # The mark, through the hair: a strand hangs between the two points.
    eyes(g, 16, 19, 22, "Y", "y")
    g.set(21, 16, MOSS_DP)
    return g.rows()


def gloomwing():
    """FAST, and the Roost Hermit's minion. A cave bat the size of a dog.

    A flyer, by section 4's rule: the wings are wider than anything else on it and nothing touches
    the ground - its shadow sits detached below. The membrane is BONE, pale against the cave's dark
    floor, with the finger bones drawn through it a stop lighter, so the wings read as wings and not
    as two grey blades; the body is stone in its lit stops, furred.
    """
    g = G(CELL)
    cx = 15.5
    # THE WINGS: raised in a V mid-beat. Per column, from the body out: a leading edge rising to the
    # wrist and falling to the tip, and a trailing edge scalloped up between the fingers.
    fingers = (5, 8, 11, 13)
    for side in (-1, 1):
        for i in range(2, 15):
            x = cx + side * i
            t = i / 14.0
            top = 13.0 - 9.0 * math.sin(min(1.0, t * 1.25) * math.pi * 0.5) + (3.5 * max(0.0, t - 0.8) / 0.2)
            dist = min(abs(i - f) for f in fingers)
            bottom = 18.0 - 6.0 * t - (2.2 - dist * 0.9 if dist < 2 else 0.0) * 0 - (0 if dist == 0 else 1.6 / (dist + 0.6))
            for y in range(int(round(top)), int(round(bottom)) + 1):
                g.set(x, y, "n" if y > top + 1 else "k")
            g.set(x, int(round(bottom)), "m")
        # the arm and finger bones, a stop lighter than the membrane they hold
        wrist = (cx + side * 6, 6)
        twig(g, [(cx + side * 2, 12), wrist], "s", "n")
        for f in fingers[1:]:
            tip_y = 18.0 - 6.0 * (f / 14.0)
            g.line(wrist[0], wrist[1], cx + side * f, tip_y - 0.6, "k")
        g.set(wrist[0] + side, wrist[1] - 1, "S")      # the thumb claw
    # THE BODY: small, furred, stone in its lit stops.
    blob(g, cx, 15.5, 2.8, 4.2, "N", "P", "j", deep="J")
    for (x, y) in ((14, 14), (16, 16), (15, 18), (17, 13)):
        g.set(x, y, "p")
    # THE HEAD and ITS EARS: tall, pointed, the second thing a bat is read by after the wings.
    blob(g, cx, 10.5, 2.4, 2.0, "N", "P", "j")
    for (x, y0) in ((13, 6), (18, 6)):
        g.line(x, y0, x + (1 if x < 16 else -1), 9, "N")
        g.set(x, y0, "P")
        g.set(x + (1 if x < 16 else -1), y0 + 1, "j")
    roster.void(g, 10, 11, 1.5, 1.0, cx=cx)
    g.set(15, 12, "S")                                  # a fang
    # The feet, tucked, with claws.
    g.set(14, 20, "j")
    g.set(17, 20, "j")
    g.set(14, 21, "n")
    g.set(17, 21, "n")
    # THE DETACHED SHADOW.
    g.span(FEET, 11, 20, "o")
    g.span(FEET + 1, 12, 19, "o")
    rim(g, 4, 21, skip="o")
    eyes(g, 10, 14, 16, "Y", "y")
    return g.rows()


def shieldclaw():
    """HEAVY. A blind cave crab grown huge, hunched behind one enormous flat claw.

    The claw is the shield plane section 4 asks a shielder for: a slab held up to one side of a
    curved body, flat-faced, bone along its edges so its outline is the brightest thing on it. It
    is a CLAW and not a door - widest across the palm, narrowing to the wrist, knobbled along the
    edge, with the pincer open at the top - because a rectangle with rungs on it is a ladder, which
    is what the first pass drew.
    """
    g = G(CELL)
    # LEGS first, splayed from under the shell, jointed, the far ones a stop darker.
    for (x0, y0, kx, ky, x1, tone) in ((8, 23, 4, 22, 2, "n"), (9, 24, 5, 25, 3, "m"),
                                       (11, 25, 8, 27, 6, "n"), (15, 25, 15, 28, 14, "m"),
                                       (17, 25, 19, 27, 20, "n")):
        twig(g, [(x0, y0), (kx, ky), (x1, FEET)], tone, "J")
        g.set(kx, ky, "k")
    # THE SHELL: a broad low dome, knobbled.
    blob(g, 12.5, 20.5, 8.6, 5.2, "N", "p", "j", deep="J")
    for (x, y) in ((9, 17), (12, 16), (15, 17), (7, 20), (11, 19), (14, 21), (17, 19)):
        g.set(x, y, "P" if (x + y) % 2 else "k")       # tubercles, catching the key
        g.set(x, y + 1, "J")
    g.line(5, 22, 19, 23, "J")                          # the shell's rim, overhanging the legs
    g.line(5, 21, 18, 22, "p")
    # THE ARM up to the claw, under it.
    _limb(g, 17, 22, 21, 24, 1.3, "N", "p", "J")
    # THE CLAW: the palm, a flattened slab widest in the middle, held upright before the body.
    SB.swell(g, 9, 25, [2.6, 3.8, 4.2, 4.0, 3.2, 2.2], "N", "p", "j", cx=23.5)
    for y in range(9, 26):
        lo = min(x for x in range(32) if inside(g, x, y) and x > 18)
        hi = max(x for x in range(32) if inside(g, x, y))
        g.set(lo, y, "k" if y % 3 else "s")             # the bone edge, knobbled
        g.set(hi, y, "n" if y % 4 else "m")
    # its face: a few tubercles and a dark seam, unevenly placed - texture, not rungs
    for (x, y, ch) in ((22, 12, "P"), (24, 15, "j"), (23, 18, "P"), (25, 20, "j"), (22, 22, "j"),
                       (25, 13, "J"), (24, 23, "P")):
        g.set(x, y, ch)
    fold(g, [(25, 10), (26, 16), (25, 23)], "J", "p")
    # the pincer: the fixed finger and the moving one, open, a gap between them
    for (x, y, ch) in ((21, 8, "k"), (21, 7, "s"), (22, 6, "s"), (23, 5, "k"), (22, 8, "N"), (22, 7, "N"),
                       (25, 8, "k"), (26, 7, "k"), (27, 6, "n"), (27, 5, "m"), (26, 8, "N"),
                       (24, 8, "o"), (23, 7, "o"), (24, 7, "o"), (24, 6, "o")):
        g.set(x, y, ch)
    g.line(20, 19, 20, 25, "o")                         # the seam where claw meets body
    # Mouthparts, under the front of the shell.
    g.set(18, 24, "k")
    g.set(18, 25, "n")
    rim(g, 5, 29, skip="o")
    contact(g, 3, 26)
    eyes(g, 19, 15, 17, "Y", "y")
    return g.rows()


def hollow_miner():
    """WALL. A miner, broad and stooped, a pick laid across one shoulder breaking the line.

    The lamp on his helm is long dark - stone and steel, no glow - because the player carries the
    only warm light on screen (.ai/world-and-tone.md). Stooped, where the Rockbreaker beside him
    stands upright: the two quarrymen are told apart by posture before tool.
    """
    g = G(CELL)
    # THE PICK first, so the shoulder lands over its handle: the shaft from the hand up over the
    # back shoulder, and the head behind, its two points breaking the outline.
    roster.shaft(g, 22, 19, 9, 5, "p", "N", "J", width=2)
    for (x, y, ch) in ((4, 9, "r"), (5, 8, "R"), (6, 7, "R"), (7, 6, "r"), (8, 5, "R"), (9, 4, "r"),
                       (10, 3, "R"), (11, 3, "q"), (5, 9, "q"), (3, 10, "t")):
        g.set(x, y, ch)
    g.set(11, 2, "t")

    # LEGS, short and planted, and boots.
    for (lx, tone) in ((10, "j"), (16, "N")):
        g.rect(lx, 25, lx + 3, 29, tone)
        g.line(lx, 25, lx, 29, "p")
        g.span(FEET, lx - 1, lx + 4, "q")
        g.set(lx - 1, FEET, "R")
    g.rect(14, 27, 15, 30, "o")
    # THE BODY: broad, stooped, a jacket of linen over the shoulders.
    SB.swell(g, 11, 26, [5.0, 7.0, 7.4, 7.0, 6.4], "E", "e", "A", cx=14.5)
    fold(g, [(10, 13), (9, 19), (10, 25)], "A", "e")
    fold(g, [(17, 14), (18, 20), (17, 25)], "A", None)
    g.span(22, 8, 21, "o")                              # the belt
    g.set(13, 22, "u")
    g.span(23, 8, 21, "j")
    g.set(19, 23, "N")                                  # a pouch on the belt
    g.set(19, 24, "j")
    # A patch sewn on the back of the jacket, and the knees of his trousers worn through.
    g.rect(9, 16, 11, 18, "e")
    g.set(9, 16, "l")
    for (x, y) in ((12, 16), (12, 18), (9, 19), (11, 19)):
        g.set(x, y, "o")
    for (x, y) in ((11, 26), (18, 26)):
        g.set(x, y, "p")
        g.set(x + 1, y, "J")
    # THE ARMS: the near one up to the pick handle, the far one hanging.
    _limb(g, 18, 13, 22, 18, 1.4, "E", "e", "A")
    g.rect(21, 18, 23, 19, "n")
    g.set(21, 18, "k")
    _limb(g, 9, 13, 7, 21, 1.3, "A", "E", "o")
    g.set(7, 22, "n")

    # THE HEAD, pushed forward of the shoulders, under a steel helm with a brim and a dead lamp.
    g.rect(16, 6, 22, 9, "R")
    g.span(5, 17, 21, "r")
    g.span(6, 16, 22, "R")
    g.set(17, 5, "t")
    g.span(10, 15, 24, "q")                             # the brim
    g.span(9, 15, 23, "R")
    g.set(15, 9, "r")
    # the lamp: a dark lens in a steel ring, cold
    g.rect(21, 6, 23, 8, "q")
    g.set(22, 7, "J")
    g.set(21, 6, "r")
    g.set(23, 8, "o")
    roster.void(g, 11, 13, 2.4, 1.6, cx=19.5)
    rim(g, 2, 29, skip="o")
    contact(g, 8, 22)
    eyes(g, 12, 18, 21, "Y", "y")
    return g.rows()


def pearl_diver():
    """RANGED. A diver, tall and dripping, who throws the pearl she drowned for.

    The pearl floats detached at her shoulder in the water ramp - the bolt she throws is the same
    colour - with daylight all round it. Upright and narrow, her wet hair down her back rather than
    over her face, which is what keeps her from being read as the Drowned beside her.
    """
    g = G(CELL)
    # THE HAIR first, down the back to the waist: wet lichen, in strands.
    for (x0, y0, x1, y1, tone, wave) in ((12, 6, 9, 18, MOSS_SH, 0.5), (13, 6, 10, 20, MOSS, 0.4),
                                         (12, 7, 8, 16, MOSS_DP, 0.0), (14, 6, 11, 17, MOSS_DP, 0.3)):
        strand(g, x0, y0, x1, y1, tone, wave)
    # THE BODY: a diver's wrap, tall and narrow, slick and dark.
    mass(g, [(10, 13, 17), (15, 12, 17), (21, 12, 18), (24, 11, 19)], "E", "e", "A")
    fold(g, [(14, 11), (14, 17), (13, 24)], "A", "e")
    g.line(12, 15, 18, 17, "A")                          # the wrap's binding, across the chest
    g.line(12, 19, 18, 21, "A")
    g.set(16, 16, "l")
    # A torn hem at the knee, and bare drowned-pale legs below it.
    for (x, d) in zip(range(11, 20), [1, 0, 2, 1, 0, 1, 2, 0, 1]):
        for k in range(1, d + 1):
            g.set(x, 24 + k, "A")
    for (lx, tone, lit) in ((13, "n", "k"), (16, "m", "n")):
        g.rect(lx, 25, lx + 1, 29, tone)
        g.line(lx, 25, lx, 29, lit)
        g.span(FEET, lx - 1, lx + 2, tone)
    g.set(15, 28, "o")
    # THE ARM, raised toward the pearl; the other hanging, a net bag at the hip.
    _limb(g, 17, 12, 21, 9, 1.0, "n", "k", "m")
    for (x, y, ch) in ((22, 8, "k"), (22, 9, "n"), (21, 10, "m")):
        g.set(x, y, ch)
    _limb(g, 12, 12, 10, 19, 0.9, "m", "n", "o")
    for (x, y, ch) in ((9, 20, "j"), (10, 21, "N"), (9, 22, "p"), (11, 22, "j"), (10, 23, "J"),
                       (10, 20, "o")):
        g.set(x, y, ch)                                  # the net bag, empty
    # THE HEAD: a narrow face, mostly void, under the wet hair.
    mass(g, [(5, 13, 16), (6, 12, 17), (9, 12, 17), (10, 13, 16)], MOSS_SH, MOSS_LIT, MOSS_DP)
    roster.void(g, 7, 10, 1.6, 1.0, cx=15.4)
    g.set(13, 5, MOSS_LIT)
    g.set(14, 4, MOSS_HI)
    # THE PEARL, detached, in the water ramp.
    for (y, x0, x1, tone) in ((8, 25, 26, WATER_EDGE), (9, 24, 27, WATER_MID), (10, 24, 27, WATER_MID),
                              (11, 24, 27, WATER_EDGE), (12, 25, 26, WATER_EDGE)):
        g.span(y, x0, x1, tone)
    g.set(25, 9, WATER_CORE)
    g.set(26, 9, WATER_HOT)
    g.set(25, 10, WATER_HOT)
    # Drops off the hem and the hair.
    for (x, y) in ((11, 27), (18, 28), (8, 21)):
        drop(g, x, y)
    rim(g, 4, 29, skip="o" + WET + WATER_EDGE + WATER_MID + WATER_HOT + WATER_CORE)
    contact(g, 10, 19)
    eyes(g, 8, 14, 16, "Y", "y")
    return g.rows()


def echo_idol():
    """TURRET. A face he carved into a stalagmite, its mouth open; the cave's echo comes out of it
    as bolts. BUILT, so it burns: fire eyes, and one crack of fire deep in the mouth.

    The stalagmite spike is the attachment - the one tall point in the chapter. A narrow needle of
    rock with two shorter spikes grown up against it, lumpy, banded with flowstone: a CLUSTER of
    points rather than one cone, because a single cone flaring to a wide foot is the Choir's bell.
    """
    g = G(CELL)
    # TWO SHORTER SPIKES, either side and behind, a stop darker.
    SB.swell(g, 14, 29, [0.4, 1.2, 2.0, 2.8, 3.4], "j", "N", "J", cx=8.0)
    SB.swell(g, 18, 29, [0.4, 1.4, 2.2, 3.0], "j", "N", "J", cx=23.5)
    # THE SPIKE: a hand-written width profile, lumpy, from a needle point to a narrow foot.
    SB.swell(g, 2, 29, [0.3, 0.9, 1.6, 2.4, 3.6, 4.2, 3.8, 4.4, 4.8, 5.4], "N", "p", "j", cx=15.5)
    # FLOWSTONE: wavy bands down the rock, lit over dark, at uneven heights.
    for (y0, amp) in ((7, 0.5), (22, 1.0), (26, 0.8), (18, 0.6)):
        for x in range(3, 29):
            y = y0 + amp * math.sin(x * 0.9 + y0)
            if inside(g, x, y):
                g.set(x, y, "j")
                if inside(g, x, y - 1):
                    g.set(x, y - 1, "P")
    for (x, y) in ((8, 16), (23, 20), (7, 22), (24, 25)):
        g.set(x, y, "P")
    # Wet lichen streaks running down from the face, and a drip off the needle.
    for (x, y0, y1) in ((13, 21, 27), (18, 21, 28)):
        g.line(x, y0, x, y1, MOSS_SH)
        g.set(x, y0, MOSS_LIT)
    g.set(16, 4, MOSS_LIT)
    # THE FACE, carved: a heavy brow, two sockets, a mouth open in an O.
    g.span(11, 12, 19, "J")
    g.span(10, 13, 18, "P")
    for x in (13, 17):
        g.rect(x, 12, x + 1, 13, "o")
    g.line(15, 13, 15, 15, "j")                         # the carved nose
    g.set(16, 15, "p")
    for (y, x0, x1) in ((16, 14, 17), (17, 13, 18), (18, 13, 18), (19, 14, 17)):
        g.span(y, x0, x1, "o")
    g.span(15, 14, 17, "P")                             # the upper lip, lit
    g.span(20, 14, 17, "j")
    g.set(15, 18, "~")                                  # the fire, deep in the throat
    # THE RUBBLE it rises from.
    for (x, y, ch) in ((4, 29, "p"), (5, 28, "N"), (27, 29, "N"), (28, 28, "p"), (3, 30, "j"), (28, 30, "j")):
        g.set(x, y, ch)
    rim(g, 2, 30, skip="o" + WET)
    contact(g, 3, 28)
    eyes(g, 12, 13, 18, "+", "-")
    g.set(14, 13, "~")
    g.set(18, 13, "=")
    return g.rows()


def leaper():
    """CHARGER. A camel cricket the size of a hound, folded to spring.

    The hind legs are the attachment: folded so the knee stands well above the humped back, which
    nothing else in the cave does, the thigh thick and the shin a long spined line down to the
    ground behind. The antennae sweep back over everything, longer than the body, as a cave
    cricket's are. Bone in its deep stops for the body, segmented in ARCS - vertical stripes on a
    hump read as a radiator - and stone at the knee.
    """
    g = G(CELL)
    # THE ANTENNAE first, behind everything: two long thin arcs from the head, swept back high.
    for (amp, tone, end, endy) in ((11.0, "n", 1, 8), (9.0, "m", 4, 11)):
        for i in range(40):
            t = i / 39.0
            x = 23.0 - (23.0 - end) * t
            y = 19.0 - amp * math.sin(t * math.pi * 0.62) + (endy - 19.0 + amp * math.sin(math.pi * 0.62)) * t * t
            g.set(x, y, tone)
    # THE FAR HIND LEG, a stop darker, and the small legs under the body.
    twig(g, [(12, 21), (9, 7), (6, 28)], "m", "J")
    for (x0, y0, x1, y1, x2) in ((17, 24, 18, 27, 20), (20, 24, 22, 26, 24)):
        twig(g, [(x0, y0), (x1, y1), (x2, FEET)], "n", "J")
    # THE BODY: humped, segmented in arcs.
    SB.swell(g, 16, 26, [1.6, 4.6, 6.2, 6.6, 6.0, 4.0], "m", "n", "J", cx=15.0)
    for (cx_, r) in ((9.0, 5.0), (12.5, 5.6), (16.0, 5.8)):
        for a in range(-60, 61, 6):
            x = cx_ + r * 0.35 * math.cos(math.radians(a)) * 0
            x = cx_ + (a / 60.0) * 0.6
            y = 21 - r * math.cos(math.radians(a))
            if inside(g, x, y):
                g.set(x, y, "J")
                if inside(g, x - 1, y):
                    g.set(x - 1, y, "k")
    for (x, y) in ((11, 16), (14, 16), (17, 17), (8, 18)):
        g.set(x, y, "k")
    g.set(13, 16, "s")
    # Mottling, as a cave cricket's back has: dark flecks between the segments.
    for (x, y) in ((10, 19), (15, 19), (18, 21), (12, 22), (7, 22), (16, 23)):
        g.set(x, y, "J")
    # THE HEAD: small, low, at the front, with palps hanging.
    blob(g, 22.5, 22.0, 2.8, 2.6, "n", "k", "m")
    for (x, y, ch) in ((24, 25, "m"), (25, 25, "n"), (23, 26, "m"), (25, 23, "J")):
        g.set(x, y, ch)
    # THE NEAR HIND LEG: the thigh thick and banded, its knee high above the back; the shin a long
    # spined line down to the ground behind; a stone knot at the knee.
    _limb(g, 13, 22, 8, 7, 2.2, "n", "k", "m")
    for (x, y) in ((11, 17), (10, 14), (9, 11)):
        g.set(x, y, "J")
        g.set(x - 1, y, "k")
    g.disc(7.6, 6.2, 1.5, "N")
    g.set(7, 5, "P")
    g.set(8, 7, "J")
    twig(g, [(7, 7), (4, 18), (2, 29)], "k", "m")
    for (x, y) in ((6, 12), (5, 16), (4, 20), (3, 24)):
        g.set(x - 1, y, "s")                           # spines down the shin
    g.span(FEET, 1, 3, "J")
    rim(g, 4, 29, skip="o" + "J")
    contact(g, 2, 25)
    eyes(g, 21, 22, 24, "Y", "y")
    return g.rows()


def bloatfish():
    """EXPLODER. A spined fish swollen round with water, hauling itself along on its fins.

    A ball of spines: the only round thing in the cave, bristling, with pale bone spines standing
    out all round a lichen body. It FACES LEFT, like the Burstpod and for the same reason - its
    stand-in scene says SpriteFacesRightByDefault = false.
    """
    g = G(CELL)
    cx, cy = 16.0, 17.5
    # SPINES first, radiating, so the body lands over their roots.
    for k in range(16):
        a = math.pi * (0.95 + k / 15.0 * 1.25)          # round the top and both sides
        r0, r1 = 7.0, 10.2 if k % 2 else 9.0
        x0, y0 = cx + r0 * math.cos(a), cy + r0 * 0.92 * math.sin(a)
        x1, y1 = cx + r1 * math.cos(a), cy + r1 * 0.92 * math.sin(a)
        g.line(x0, y0, x1, y1, "n")
        g.set(x1, y1, "s" if k % 3 else "k")
    # THE BODY: a ball, lichen, the belly a stop paler.
    blob(g, cx, cy, 7.4, 7.0, MOSS, MOSS_LIT, MOSS_SH, deep=MOSS_DP)
    for y in range(20, 25):
        for x in range(12, 21):
            if inside(g, x, y) and (x - cx) ** 2 / 49.0 + (y - cy) ** 2 / 44.0 < 0.75:
                g.set(x, y, "n" if (x + y) % 3 else "k")
    # Spots, irregular, and the spine roots as dark dots.
    for (x, y) in ((14, 13), (19, 14), (17, 17), (21, 18), (12, 16), (16, 11)):
        g.set(x, y, MOSS_DP)
    # THE TAIL, a small fan behind (to the right, since it faces left).
    for (x, y, ch) in ((24, 16, MOSS_SH), (25, 15, MOSS), (26, 14, MOSS_LIT), (25, 17, MOSS_SH),
                       (26, 18, MOSS), (27, 19, MOSS_SH), (26, 16, "o")):
        g.set(x, y, ch)
    # THE MOUTH, a small O at the front, lipped in bone.
    for (x, y, ch) in ((8, 18, "k"), (8, 20, "n"), (7, 19, "o"), (8, 19, "o"), (9, 19, "o"), (9, 18, "n")):
        g.set(x, y, ch)
    # THE FINS it hauls itself on: two pectoral fans planted on the ground.
    for (x0, x1) in ((9, 13), (18, 22)):
        for i, x in enumerate(range(x0, x1 + 1)):
            g.line(x0 + 2, 24, x, FEET, MOSS_SH if i % 2 else MOSS)
        g.span(FEET, x0, x1, MOSS_DP)
    rim(g, 4, 29, skip="o")
    contact(g, 8, 23)
    eyes(g, 15, 11, 13, "y", "Y")
    return g.rows()


def rockbreaker():
    """SLAMMER. A quarryman raising a maul overhead in both hands.

    The maul head is the highest thing in the cell, wound back over his head - the ring is it
    coming down. UPRIGHT and square-shouldered, where the Hollow Miner stoops: the two quarrymen
    are told apart by posture before tool. Both arms are up and clear of the body, with the head
    between them; the first pass hid them inside a box of a torso. A red sash at the waist - old
    pigment, the one patch of colour he still has.
    """
    g = G(CELL)
    # THE MAUL first: the handle from the hands back over the head, the steel head up behind.
    roster.shaft(g, 16, 8, 8, 4, "p", "N", "J", width=2)
    g.rect(2, 2, 9, 7, "R")
    g.span(2, 3, 9, "r")
    g.line(2, 3, 2, 7, "r")
    g.set(3, 2, "t")
    g.set(2, 3, "t")
    g.span(7, 2, 9, "q")
    g.line(9, 2, 9, 7, "q")
    for (x, y) in ((4, 4), (6, 3), (7, 5), (5, 6)):
        g.set(x, y, "q")                                # pitting on the face of the head
    g.rect(10, 4, 10, 5, "q")                           # the collar where the handle enters
    # LEGS, apart and planted.
    for (lx, tone, lit) in ((10, "j", "N"), (17, "N", "p")):
        g.rect(lx, 24, lx + 3, 29, tone)
        g.line(lx, 24, lx, 29, lit)
        g.span(FEET, lx - 1, lx + 4, "q")
    g.rect(14, 26, 16, 30, "o")
    # THE BODY: a V - broad at the shoulder, narrow at the sash.
    SB.swell(g, 12, 25, [6.2, 6.0, 5.0, 4.4, 5.4], "E", "e", "A", cx=15.0)
    fold(g, [(12, 14), (12, 20)], "A", "e")
    fold(g, [(18, 14), (18, 21)], "A", None)
    g.line(15, 13, 15, 17, "A")                         # the open collar
    g.set(14, 16, "l")
    # THE RED SASH, wrapped at the waist, its end hanging.
    g.span(20, 10, 20, RED)
    g.span(21, 10, 20, RED_SH)
    g.set(10, 20, RED_LIT)
    g.line(18, 22, 19, 25, RED_SH)
    g.set(18, 22, RED)
    # ARMS: both up from the shoulders, clear of the head, gripping the handle overhead.
    _limb(g, 9, 13, 12, 6, 1.5, "E", "e", "A")
    _limb(g, 21, 13, 18, 6, 1.5, "E", "e", "A")
    g.line(10, 12, 13, 8, "o")                          # daylight between arm and head
    g.line(20, 12, 18, 9, "o")
    for (x, y) in ((12, 5), (16, 6)):
        g.rect(x, y, x + 1, y + 1, "n")
        g.set(x, y, "k")
    # THE HEAD, between the arms: a capped head, the void under the cap.
    g.rect(14, 8, 17, 12, "A")
    g.span(8, 14, 17, "j")
    g.span(7, 14, 17, "N")
    g.set(14, 7, "p")
    roster.void(g, 10, 12, 1.6, 1.0, cx=16.0)
    rim(g, 2, 29, skip="o")
    contact(g, 8, 22)
    eyes(g, 10, 15, 17, "Y", "y")
    return g.rows()


def roost_hermit():
    """SUMMONER, who calls down the Gloomwings. A hermit who lived among the bats.

    The attachment is the staff he leans on, crooked and taller than he is, with bats roosting
    upside down off its crook and its branch - the same bats he calls. Bent under a ragged cloak of
    STONE-grey, with linen under it and a bone beard down his chest: the Drowned beside him is a
    hunched linen shape too, and section 2 asks for two creatures of one chapter to differ in
    material as well as outline. The staff is bleached driftwood, so it reads against the cloak.
    """
    g = G(CELL)
    # THE CLOAK: hunched, ragged, heavy at the back, in stone's lit stops.
    mass(g, [(9, 10, 16), (14, 7, 18), (21, 6, 19), (27, 6, 20), (28, 6, 20)], "N", "P", "j")
    fold(g, [(10, 11), (8, 19), (8, 27)], "J", "p")
    fold(g, [(12, 14), (11, 22), (12, 28)], "j", "p")
    for (x, y) in ((9, 16), (11, 24), (7, 22), (13, 12)):
        g.set(x, y, "P")
    # The linen robe under it, showing down the front.
    for y in range(14, 29):
        x0 = 15 + (1 if y > 20 else 0)
        g.span(y, x0, x0 + 2, "E")
        g.set(x0, y, "e")
        g.set(x0 + 3, y, "A")
    for (x, d) in zip(range(5, 21), [1, 2, 1, 2, 0, 1, 2, 2, 1, 0, 1, 2, 1, 0, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 28 + k, "j" if k < d else "N")
    cut(g, (9, 29), (9, 30), (15, 29), (15, 30))
    # A cord at the waist, a pouch on it.
    g.line(8, 20, 19, 19, "o")
    g.set(10, 20, "e")
    g.set(10, 21, "A")
    # THE BEARD, long, bone, hanging down the front.
    roster.beard(g, 17.0, 12, 19, 1.4, 1.0, sweep=0.8)
    # THE HOOD, pulled forward, linen.
    mass(g, [(5, 13, 16), (6, 12, 18), (8, 11, 19), (11, 12, 19)], "E", "e", "A")
    roster.void(g, 8, 10, 1.6, 1.0, cx=17.0)
    # THE STAFF: crooked, knotted driftwood, from the ground up past his head to a crook.
    twig(g, [(23, 30), (22, 22), (24, 14), (23, 7), (24, 4), (21, 2), (19, 4)], "e", "A")
    twig(g, [(23, 29), (22, 21), (24, 13), (23, 6)], "l", "A")
    g.line(24, 12, 28, 9, "e")                          # a side branch
    g.set(28, 9, "l")
    for (x, y) in ((22, 18), (24, 10)):
        g.set(x, y, "A")                                # knots
    # THE HAND on it.
    _limb(g, 18, 13, 22, 15, 1.0, "E", "e", "A")
    g.rect(22, 14, 23, 15, "n")
    g.set(22, 14, "k")
    # BATS, roosting upside down off the crook and the branch: hung by the feet, wings folded round
    # them, bone along the fold, the ears pointing down.
    for (bx, by) in ((19, 5), (27, 10), (25, 15)):
        for (dx, dy, ch) in ((0, 0, "n"), (-1, 1, "k"), (0, 1, "N"), (1, 1, "n"), (-1, 2, "n"),
                             (0, 2, "P"), (1, 2, "j"), (-1, 3, "m"), (0, 3, "N"), (1, 3, "J"),
                             (0, 4, "j"), (-1, 5, "N"), (1, 5, "N")):
            g.set(bx + dx, by + dy, ch)
    rim(g, 3, 28, skip="o")
    contact(g, 5, 24)
    eyes(g, 9, 16, 18, "Y", "y")
    return g.rows()


def anchor_warden():
    """MINIBOSS, on the 48 cell. His jailer below: a hulk in a riveted diving helm, dragging an
    anchor on a chain.

    The helm is a round steel dome with a barred port, and the mark is BEHIND the grille - the one
    place the cold light shows. The anchor lies on the ground off his back side, its chain running
    up to his fist: nothing else in the game drags a thing behind it, and nothing else in the cave
    is this round at the top and this wide at the bottom.
    """
    c = 48
    feet = c - 2
    g = G(c)
    # THE ANCHOR first, on the ground behind him: a shank lying aslant, a ring at its head, the
    # crown and two flukes at its foot.
    roster.shaft(g, 4, 34, 13, 45, "r", "R", "q", width=2)
    for (x, y, ch) in ((3, 32, "r"), (4, 31, "R"), (5, 32, "q"), (3, 33, "R"), (5, 33, "q"),
                       (4, 33, "o")):
        g.set(x, y, ch)                                 # the ring
    g.line(2, 37, 9, 34, "R")                           # the stock, across the shank
    g.line(2, 38, 9, 35, "q")
    for (x0, y0, x1, y1) in ((13, 45, 8, 42), (13, 45, 17, 41)):
        g.line(x0, y0, x1, y1, "R")
        g.line(x0, y0 - 1, x1, y1 - 1, "r")
    for (x, y) in ((7, 41), (8, 40), (17, 40), (18, 39)):
        g.set(x, y, "t")                                # the fluke points, catching the key
    g.span(feet + 1, 3, 18, "o")

    # THE CHAIN, from the anchor ring up to his fist.
    for i in range(14):
        t = i / 13.0
        x = 4 + (15 - 4) * t
        y = 31 + (33 - 31) * t - 5.0 * math.sin(t * math.pi) * 0.4
        g.set(x, y, "r" if i % 2 else "q")
        g.set(x, y + 1, "R" if i % 2 else "o")

    # LEGS: thick, in lead-weighted boots.
    for (lx, tone, lit) in ((19, "A", "E"), (28, "E", "e")):
        g.rect(lx, 37, lx + 5, 42, tone)
        g.line(lx, 37, lx, 42, lit)
        g.rect(lx - 1, 42, lx + 6, feet, "q")
        g.span(42, lx - 1, lx + 6, "R")
        g.set(lx - 1, 42, "r")
        for rx in (lx + 1, lx + 4):
            g.set(rx, 44, "t")
    # THE BODY: the diving suit, canvas, hulking.
    SB.swell(g, 19, 38, [8.0, 10.6, 11.0, 10.4, 9.0], "E", "e", "A", cx=25.0)
    for pts in ([(18, 22), (16, 30), (18, 37)], [(25, 24), (25, 37)], [(31, 22), (33, 30), (32, 37)]):
        fold(g, pts, "A", "e")
    for (x, y) in ((20, 33), (29, 29), (22, 26)):
        g.set(x, y, "l")
    # The weight belt, lead blocks on it, unevenly.
    g.span(33, 15, 35, "q")
    g.span(34, 15, 35, "R")
    for bx in (17, 22, 30):
        g.rect(bx, 33, bx + 2, 35, "j")
        g.set(bx, 33, "N")
    # THE CORSELET: a riveted collar plate the helm sits on.
    g.rect(17, 17, 33, 21, "R")
    g.span(17, 18, 32, "r")
    g.span(21, 17, 33, "q")
    for rx in (18, 21, 24, 27, 30, 32):
        g.set(rx, 19, "t")
        g.set(rx, 20, "q")
    # ARMS: the back one down to the chain, the front one hanging heavy.
    _limb(g, 15, 21, 14, 31, 2.2, "A", "E", "o")
    g.rect(12, 31, 16, 34, "q")
    g.span(31, 12, 16, "R")
    _limb(g, 35, 21, 37, 33, 2.2, "E", "e", "A")
    g.rect(35, 33, 39, 36, "R")
    g.span(33, 35, 39, "r")
    g.span(36, 35, 39, "q")

    # THE HELM: a round steel dome, riveted, with a barred port in front.
    blob(g, 25.0, 10.0, 7.6, 7.0, "R", "r", "q", deep="o")
    g.set(21, 5, "t")
    g.set(20, 6, "t")
    for (rx, ry) in ((19, 12), (22, 15), (28, 16), (31, 13), (32, 8), (29, 4), (24, 3)):
        g.set(rx, ry, "t")
        g.set(rx, ry + 1, "q")
    # The port: a dark disc, its grille, and the mark behind it.
    g.disc(28.0, 10.5, 3.2, "o")
    for ang in range(0, 360, 40):
        a = math.radians(ang)
        g.set(28 + 3.6 * math.cos(a), 10.5 + 3.6 * math.sin(a), "r")
    g.line(28, 7, 28, 14, "q")
    g.line(25, 10, 31, 10, "q")
    # A small side port, and the air-hose stub at the back of the helm.
    g.disc(19.5, 9.5, 1.2, "o")
    g.set(19, 8, "r")
    g.rect(15, 11, 17, 13, "q")
    g.set(15, 11, "R")

    rim(g, 2, 45, skip="o")
    contact(g, 18, 36, y=feet)
    eyes(g, 9, 26, 29, "Y", "y")
    return g.rows()


CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    ("cave-drowned", drowned, 32, (0.7, 2.2, 2), "CaveDrownedFrames"),
    ("cave-gloomwing", gloomwing, 32, (0.8, 3.0, 3), "CaveGloomwingFrames"),
    ("cave-shieldclaw", shieldclaw, 32, (0.5, 2.8, 1), "CaveShieldclawFrames"),
    ("cave-hollow-miner", hollow_miner, 32, (0.7, 3.6, 1), "CaveHollowMinerFrames"),
    ("cave-pearl-diver", pearl_diver, 32, (0.9, 2.2, 3), "CavePearlDiverFrames"),
    ("cave-echo-idol", echo_idol, 32, (0.3, 1.6, 1), "CaveEchoIdolFrames"),
    ("cave-leaper", leaper, 32, (1.8, 3.4, 3), "CaveLeaperFrames"),
    # Faces left (see bloatfish), so it leans into its strike and is knocked back the other way.
    ("cave-bloatfish", bloatfish, 32, (1.1, -2.0, -3), "CaveBloatfishFrames"),
    ("cave-rockbreaker", rockbreaker, 32, (0.6, 4.0, 1), "CaveRockbreakerFrames"),
    ("cave-roost-hermit", roost_hermit, 32, (0.9, 2.6, 2), "CaveRoostHermitFrames"),
    ("cave-anchor-warden", anchor_warden, 48, (0.5, 4.2, 1), "CaveAnchorWardenFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    # Largest cell first: bestiary_kit.report sizes its contact sheet by the first creature's cell.
    bestiary_kit.run(sorted(CAST, key=lambda c: -c[2]), PALETTE, "cave")
