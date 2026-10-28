# -*- coding: utf-8 -*-
"""The lantern-tender: the Blighted Swamp side event's last fight. Run: python tools/art/swamp_tender.py

"The lanterns still burn. Nothing living lights them." With all four bog lanterns snuffed, the
thing that tends them comes looking for whoever put them out (scripts/BogLanternsEvent.cs). Until
2026-10-28 it was an ordinary swamp creature from the stage table at elite scale, so it had no shape
of its own. Eric: "lantern tender should have a glowing lantern and be bigger so it's much easier to
see him. he should fire animated fire strikes and have a few different special abilities".

This draws everything the fight shows that is not light:

  assets/bonelight/enemies/swamp-lantern-tender-{moving,attack,hurt,death}.png
      the tender, on the 96 cell at x2 - the Boo's precedent for a miniboss on the boss cell
      (.ai/enemy-behaviour.md 7b) - with its SpriteFrames, scenes/resources/SwampLanternTenderFrames.tres
  assets/bonelight/effects/tender-strike.png   a fire strike landing: 8 frames on the 96 AoE cell
  assets/bonelight/effects/tender-ember.png    the flame it throws, in flight: 4 frames on the 32 cell
  assets/bonelight/effects/tender-oil.png      one tongue of the burning oil it spills: 6 frames, 32 cell

The three effects are authored 1:1 and saved at x2 like props_fx.py and spell_flames.py, so the game
draws them at scale 1; the tender's strips are 1:1 like every other enemy's and drawn at scale 2.

WHAT IT IS. The keeper of the fen's lanterns, taken: tall and stooped, bent over from tending, in a
long linen coat wet to the thigh, a short violet cape over a hunched back (violet is what the swamp's
carriers of light and sound wear - the Lamplighter's hood, the Reed Piper), a sodden wide hat with
bog weed hanging off the brim, and the mark under it. It wades: there are no feet, only the coat's
hem dragging in a wake of mud and standing water, as the Mired does.

THE LANTERN IS THE SILHOUETTE. It carries a crook of the same dead wood the bog lanterns hang from,
taller than itself, and from the crook one of THEIR lanterns: the same iron cage, the same cold
flame in the poison ramp. It is the highest thing in the cell and the brightest thing on the screen,
so the player finds the tender by the light the way he found the lanterns. That is allowed because
it is light (.ai/art-direction.md: "elements are emissive, not pigment") - the cage's inner bars, the
underside of the brim and the knuckles on the crook catch it in the ramp's edge stop, exactly as
the bog lantern's bars do. `.ai/bestiary.md` keeps the Lamplighter's flame UNCAGED so that an
ordinary caster never looks like an event lantern; the tender is the event, and is meant to.

In its other hand it carries a brass oil can with a long spout, which is the one warm thing on it
(gold, under the 15% the pigment rows are held to) and the reason its spilt-oil attack reads as
something it does rather than something that happens.

NOTHING ECHOES MOTHER ROT, the chapter's boss and its only dome: this is a column that leans.

THE FLAME. Every effect here is the poison ramp - core white, identity in the mid and edge - because
what the tender throws is the lanterns' own fire, "nothing living lights them". It is a cold flame
and it is still a flame: tongues, a white root, flecks thrown off it. One constant (FLAME below)
would make it the fire ramp instead.
"""
from __future__ import division, print_function

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import anim_sets
import bonelight as B
import pixel as P
import raster
import roster
import spriteframes
import sprite_bosses as SB
from cast_forest import blob, rim, twig
from cast_swamp import PALETTE as SWAMP_PALETTE, reed, ripple
from foes_forest import _limb

G = roster.G
mass = roster.mass
fold = roster.fold
cut = roster.cut
eyes = roster.eyes
contact = roster.contact

# The swamp cast's palette: lichen in the azure characters, red cloth in the ember ones, and the
# poison ramp in the lightning characters (core, hot, mid, edge), so anim_sets steps the flame along
# its own ramp in a hurt flash and a death.
PALETTE = dict(SWAMP_PALETTE)
MOSS_HI, MOSS_LIT, MOSS, MOSS_SH, MOSS_DP = "U", "5", "6", "7", "8"
F_CORE, F_HOT, F_MID, F_EDGE = "[", "]", ";", ":"
FLAME_CH = F_CORE + F_HOT + F_MID + F_EDGE
WET = MOSS_HI + MOSS_LIT + MOSS + MOSS_SH + MOSS_DP

CELL = 96
FEET = CELL - 2

OUT_ENEMIES = os.path.join(ROOT, "assets", "bonelight", "enemies")
OUT_EFFECTS = os.path.join(ROOT, "assets", "bonelight", "effects")
OUT_FRAMES = os.path.join(ROOT, "scenes", "resources")
STEM = "swamp-lantern-tender"
SHEET = "SwampLanternTenderFrames"

# How it moves (anim_sets.full_set): a slow wading sway, a long lean into a throw, and almost no
# stagger - a miniboss that recoils like a swarmer stops being one (sprite_warden's note).
FEEL = (0.6, 4.0, 1)

# Where the lantern's flame sits in the cell. scripts/LanternTenderEnemy.cs places its light here
# (LanternFlameOffset, in local pixels: (x - 48, y - 48) * 2 plus the sprite's offset) - change one,
# change both. The flame is the heart of the light the player sees from across the bog.
LANTERN_FLAME = (80, 28)

# The hat brim's row at its middle (it sags lower at both ends).
BRIM_Y = 30

# The fire. Poison: the bog lanterns' cold flame, which is what the tender throws.
FLAME = B.ELEMENTS["poison"]   # core, hot, mid, edge


# =================================================================================================
# THE TENDER
# =================================================================================================

def crook(g):
    """The crook: dead wood like the bog lanterns' poles, from the mud up past the hat, curling
    forward over the top into a hook. Lit on the key side, a split down the shaded one."""
    # The shaft. It leans a little and is not straight: dead wood.
    for y in range(9, FEET + 1):
        x = 66 if y > 60 else (65 if y > 30 else 64)
        g.set(x - 1, y, "l")
        g.set(x, y, "e")
        g.set(x + 1, y, "A")
        if y % 9 in (3, 4):
            g.set(x + 1, y, "o")                          # the split
        if y % 13 == 7:
            g.set(x, y, "E")                              # a knot
    # A stub where a branch was snapped off, and a second knot.
    for (x, y, ch) in ((67, 70, "e"), (68, 69, "l"), (69, 68, "A"), (62, 50, "A"), (63, 51, "e")):
        g.set(x, y, ch)
    # The crook: up and over to the front, then down into a hook.
    pts = [(64, 9), (65, 6), (68, 4), (72, 3), (76, 4), (79, 6), (80, 9)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + 1):
            t = i / float(max(1, n))
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            g.set(x, y, "l" if y < 6 or x < 66 else "e")
            g.set(x, y + 1, "A")
    g.set(63, 9, "L")
    g.set(81, 10, "A")
    g.set(80, 10, "e")


def lantern(g):
    """One of the bog lanterns, carried: a ring and a short chain off the hook, a peaked cap, four
    bars, a floor plate and a drip finial - and inside, darkness and one cold flame that lights only
    what is near it (the cage's inner faces, its floor). A cage full of green reads as a lit box."""
    fx, fy = LANTERN_FLAME
    # Chain from the hook.
    for y in range(11, 15):
        g.set(80, y, "r" if y % 2 else "R")
    g.set(79, 12, "q")
    # The ring at the top of the cap.
    g.set(79, 15, "R")
    g.set(81, 15, "R")
    g.set(80, 14, "r")
    # The cap: a shallow pyramid, lit on the key side.
    for (y, x0, x1) in ((16, 78, 82), (17, 76, 84), (18, 74, 86)):
        g.span(y, x0, x1, "R")
        g.set(x0, y, "t")
        g.set(x1, y, "q")
    g.set(77, 16, "T")
    g.span(19, 74, 86, "q")
    # The body: dark inside.
    g.rect(75, 20, 85, 36, "o")
    # THE FLAME: a teardrop on a white root, leaning a little, its light on the floor plate.
    for (y, x0, x1, ch) in ((22, 80, 80, F_EDGE), (23, 80, 81, F_EDGE), (24, 79, 81, F_MID),
                            (25, 79, 82, F_MID), (26, 78, 82, F_MID), (27, 78, 82, F_HOT),
                            (28, 78, 82, F_HOT), (29, 78, 82, F_HOT), (30, 79, 81, F_CORE),
                            (31, 79, 81, F_CORE), (32, 78, 82, F_MID), (33, 77, 83, F_EDGE)):
        g.span(y, x0, x1, ch)
    g.set(80, 26, F_HOT)
    g.set(79, 27, F_CORE)
    g.set(80, 28, F_CORE)
    g.set(77, 26, F_EDGE)
    g.set(83, 28, F_EDGE)
    g.set(81, 21, F_EDGE)                                 # a fleck torn off the tip
    # The bars, at uneven gaps: the outer pair lit and shaded, the inner pair catching the flame.
    for (x, ch) in ((74, "t"), (86, "q")):
        g.line(x, 19, x, 36, ch)
    g.line(77, 20, 77, 35, "R")
    g.line(83, 20, 83, 35, "R")
    for y in range(24, 33):
        g.set(76, y, F_EDGE if y % 3 else "o")            # the inner faces, lit by the flame
        g.set(84, y, F_EDGE if y % 4 else "o")
    g.span(20, 76, 84, F_EDGE)                            # the cap's underside, lit from below
    # The floor plate and the drip finial.
    g.span(37, 74, 86, "r")
    g.span(36, 76, 84, F_MID)                             # the flame's light on the floor's top edge
    g.span(38, 75, 85, "q")
    g.set(74, 37, "t")
    g.rect(79, 39, 81, 40, "R")
    g.set(80, 41, "q")


def wake(g):
    """The bog it wades in: the coat's hem is in it, so there are no feet - a lumpy wake of mud and
    standing water round the hem, clods and ripples off it, as the Mired drags. Never a level band:
    a flat slab under a figure reads as the plinth of a statue."""
    tops = {}
    for x in range(22, 70):
        # Hand-written lumps rather than a curve: the mud is pushed up where the coat drags.
        bump = {23: 1, 24: 2, 25: 3, 26: 2, 31: 1, 32: 2, 39: 1, 44: 2, 45: 3, 46: 2, 47: 1,
                53: 1, 58: 2, 59: 3, 60: 2, 65: 1, 66: 2}.get(x, 0)
        top = 91 - bump - (1 if 30 < x < 62 else 0)
        tops[x] = top
        for y in range(top, FEET + 1):
            g.set(x, y, "N" if (x * 5 + y) % 9 else "j")
        g.set(x, top, "p")
    for (x, y) in ((25, 90), (33, 92), (45, 90), (54, 93), (60, 90), (40, 93), (49, 94), (67, 93)):
        g.set(x, y, "J")
        g.set(x - 1, y - 1, "P")
    for (x, y) in ((17, 91), (19, 88), (74, 90), (77, 92), (15, 94), (72, 86)):
        g.set(x, y, "N")                                  # clods thrown off
        g.set(x, y - 1, "p")
    for (x, y, w) in ((36, 93, 3), (57, 94, 2), (27, 94, 1), (69, 91, 1), (46, 94, 2), (20, 93, 1)):
        ripple(g, x, y, w)
    return tops


def coat(g):
    """The long coat: linen, gaunt through the chest, flaring to a hem dragged dark and wet. Its
    planes are in linen's base and lit stops and its darks under the floor's value, because the
    swamp's ground is linen's own shade stop (cast_swamp.py)."""
    mass(g, [(40, 34, 55), (50, 32, 55), (60, 31, 55), (70, 28, 57), (80, 25, 60), (88, 23, 63)],
         "e", "l", "A")
    # The ragged wet hem, darker and torn, sinking into the wake.
    for (x, d) in zip(range(23, 64), [2, 3, 1, 2, 3, 2, 1, 3, 2, 2, 1, 3, 2, 1, 2, 3, 2, 2, 1, 2, 3,
                                      1, 2, 2, 3, 1, 2, 2, 1, 3, 2, 1, 2, 2, 3, 1, 2, 1, 2, 3, 2]):
        for k in range(d):
            g.set(x, 86 + k, "A" if k else MOSS_SH)
    # Wet: the bog has soaked up the coat from the hem in a ragged tide line, darker below it, with
    # a few runs climbing higher. Hand-placed: evenly spaced streaks read as a radiator.
    for x in range(24, 64):
        tide = 80 + ((x * 7) % 5) - (2 if x in (29, 30, 43, 51, 52, 58) else 0)
        for y in range(tide, 87):
            if g.get(x, y) in ("e", "l"):
                g.set(x, y, "E")
        if g.get(x, tide - 1) in ("e", "l"):
            g.set(x, tide - 1, "l" if x % 3 else "e")
    for (x, top, bottom) in ((28, 71, 80), (34, 75, 81), (44, 69, 80), (49, 74, 79), (57, 72, 81)):
        for y in range(top, bottom):
            if g.get(x, y) in ("e", "l", "A"):
                g.set(x, y, MOSS_SH if y > top + 2 else "E")
    for (x, y) in ((31, 84), (47, 83), (39, 85), (55, 84), (26, 85)):
        g.set(x, y, MOSS)                                 # weed caught on the hem
    # LONG CREASES from the belt to the hem, a dark stop with a lit one beside it.
    for pts in ([(37, 60), (34, 72), (31, 86)], [(46, 62), (47, 74), (49, 86)],
                [(41, 66), (40, 78), (40, 86)], [(53, 64), (56, 86)]):
        fold(g, pts, "A", "l")
    # The front opening down the right side, and its buttons (bone, old).
    g.line(52, 42, 52, 58, "A")
    for y in (44, 49, 54):
        g.set(51, y, "k")
        g.set(51, y + 1, "n")
    # Patches: one sewn on, one torn through to the lining.
    g.rect(35, 68, 38, 71, "E")
    g.line(35, 68, 38, 68, "l")
    g.set(38, 71, "A")
    for (x, y) in ((44, 76), (45, 76), (45, 77)):
        g.set(x, y, "o")
    g.set(43, 76, "l")
    # THE BELT, a strap of stone-dark leather, with a steel buckle and what hangs off it.
    g.span(58, 31, 55, "o")
    g.span(59, 31, 55, "J")
    g.rect(47, 58, 49, 59, "r")
    g.set(47, 58, "t")
    g.set(49, 59, "q")
    # A bundle of spare wicks and tapers on a cord at the hip: bone, bound.
    g.line(36, 60, 36, 63, "q")
    for (x, y0, y1) in ((34, 63, 69), (35, 62, 70), (36, 63, 68), (37, 64, 69)):
        g.line(x, y0, x, y1, "k" if x % 2 else "n")
        g.set(x, y0, "s")
    g.span(65, 34, 37, "u")
    # A snuffer on a cord: a small steel cone on a stick.
    g.line(50, 60, 50, 66, "A")
    for (y, x0, x1) in ((67, 49, 51), (68, 48, 52), (69, 48, 52)):
        g.span(y, x0, x1, "R")
        g.set(x0, y, "t")
    g.span(70, 48, 52, "q")


def cape(g):
    """The short cape over the hump: violet, deep, its lower edge torn into tags. The hump is what
    stoops the figure - its back rises higher than its head."""
    # The hump is the top of the body: higher than the hat, which hangs in front of it.
    mass(g, [(23, 34, 40), (25, 30, 46), (28, 28, 50), (33, 27, 54), (39, 27, 55), (45, 29, 55),
             (50, 31, 54)], "w", "v", "X")
    # The torn lower edge: tags of uneven length.
    for (x, d) in zip(range(27, 56), [1, 3, 2, 0, 4, 2, 1, 3, 0, 2, 4, 1, 2, 3, 0, 1, 3, 2, 4, 1,
                                      0, 2, 3, 1, 2, 4, 1, 0, 2]):
        for k in range(d):
            g.set(x, 51 + k, "x" if k < d - 1 else "X")
    # Folds over the hump and down the back.
    fold(g, [(34, 26), (31, 34), (29, 47)], "X", "V")
    fold(g, [(41, 27), (39, 39), (38, 50)], "X", "v")
    fold(g, [(48, 31), (49, 42), (50, 50)], "x", "v")
    for (x, y) in ((35, 41), (44, 36), (37, 29), (33, 45)):
        g.set(x, y, "V")
    for (x, y) in ((43, 46), (31, 40)):
        g.set(x, y, "X")
    # The lantern's light on the cape's front edge, which faces it.
    for y in (37, 39, 42, 45):
        if g.get(55, y) in ("w", "x", "X"):
            g.set(55, y, F_EDGE)
    # A clasp at the throat: tarnished steel.
    g.rect(51, 33, 52, 34, "R")
    g.set(51, 33, "t")


def arms(g):
    """The near arm forward, gripping the crook at the chest; the far arm hanging, with the oil can."""
    # The far arm, behind, down to the hip: sleeve in the coat's deep stop.
    _limb(g, 31, 44, 23, 60, 2.6, "A", "E", "o")
    # The far hand, bone, round the can's bail.
    g.rect(20, 58, 24, 61, "n")
    g.set(20, 58, "k")
    g.set(21, 58, "s")
    g.set(24, 61, "m")
    # THE OIL CAN, hanging from the hand by its bail: a tall brass body with rounded shoulders, a
    # cone to a capped neck, and the long thin spout rising off its foot - the spout is what makes it
    # a can and not a bag. The one warm thing on it.
    g.line(18, 60, 18, 63, "u")                           # the bail
    g.line(25, 60, 25, 63, "u")
    g.span(59, 19, 24, "u")
    for (y, x0, x1) in ((62, 20, 23), (63, 19, 24), (64, 18, 25)):
        g.span(y, x0, x1, "g")                           # the cone to the neck
        g.set(x0, y, "G")
    g.set(21, 61, "H")
    g.set(22, 61, "G")
    for y in range(65, 77):
        x0, x1 = (17, 26) if 66 <= y <= 75 else (18, 25)
        g.span(y, x0, x1, "g")
        g.set(x0, y, "G")
        g.set(x0 + 1, y, "G" if y < 71 else "g")
        g.set(x1, y, "u")
    g.span(70, 17, 26, "u")                               # the seam round its middle
    g.set(18, 69, "H")
    g.set(19, 67, "H")
    for (x, y) in ((22, 73), (24, 67)):
        g.set(x, y, "u")                                  # dents
    g.span(77, 18, 25, "o")
    # The spout: long and thin, rising forward off the foot, a drip falling from its mouth.
    g.line(26, 74, 34, 66, "G")
    g.line(27, 74, 35, 66, "u")
    g.set(35, 65, "u")
    g.set(36, 68, "J")
    g.set(36, 71, "J")
    # The near arm: shoulder under the cape, the elbow out, the hand up on the crook at the chest.
    _limb(g, 52, 42, 58, 52, 2.8, "e", "l", "A")
    _limb(g, 58, 52, 63, 47, 2.4, "e", "l", "A")
    g.set(58, 52, "A")
    # The near hand, bone, gripping: knuckles lit by the lantern above it.
    g.rect(62, 44, 66, 48, "n")
    g.span(44, 62, 66, "k")
    g.set(62, 44, "s")
    g.set(66, 48, "m")
    g.set(63, 44, F_EDGE)
    g.set(65, 44, F_EDGE)
    g.line(63, 46, 66, 46, "m")                           # the fingers' line round the wood


def head(g):
    """The head, hung forward of the hump on a long neck, under a sodden wide hat. The brim sags at
    both ends and bog weed hangs off it; the void under it carries the mark. The brim's near edge
    is lit from below by the lantern."""
    # Where the brim sits at a column: it sags at both ends.
    def brim_y(x):
        return BRIM_Y + int(round(2.6 * ((x - 55.5) / 15.5) ** 2))

    # The neck, reaching forward and down out of the cape.
    mass(g, [(30, 49, 55), (36, 50, 57)], "A", "E", "o", rim=False)
    # Lank hair at the back of the head, under the brim.
    for (x, y0, y1) in ((46, 32, 39), (47, 32, 41), (48, 33, 38), (49, 32, 37)):
        g.line(x, y0, x, y1, "A" if x % 2 else "E")
    # The face: the void, tapering to a long jaw.
    mass(g, [(31, 49, 62), (35, 50, 62), (40, 52, 60), (43, 54, 58)], "o", "o", "o", rim=False)
    g.set(51, 39, "A")
    g.set(61, 35, "A")
    # THE HAT: a tall crushed crown in stone, a band of old red cloth, and a brim that droops.
    mass(g, [(19, 49, 58), (21, 48, 60), (26, 47, 61), (29, 46, 62)], "N", "p", "J")
    g.set(50, 19, "P")
    g.set(56, 20, "j")                                    # the crown is crushed in on top
    g.set(57, 20, "J")
    fold(g, [(53, 20), (52, 28)], "J", "p")
    fold(g, [(58, 22), (59, 28)], "j", None)
    g.span(28, 47, 61, "2")                               # the band
    g.span(29, 47, 61, "3")
    g.set(47, 28, "1")
    g.set(54, 29, "4")
    # The brim, sagging at both ends.
    for x in range(40, 72):
        y = brim_y(x)
        g.set(x, y, "N" if 43 < x < 68 else "j")
        g.set(x, y + 1, "J")
        if x in (40, 71):
            g.set(x, y + 2, "J")
    for x in range(41, 49):
        g.set(x, brim_y(x), "p")                          # the key-lit near brim
    # Underside of the brim's front, lit from below by the lantern beside it.
    for x in (63, 65, 66, 68, 69):
        g.set(x, brim_y(x) + 1, F_EDGE)
    # BOG WEED hanging off the brim, uneven, in lichen.
    for (x, length) in ((41, 6), (43, 9), (45, 4), (66, 4), (68, 7), (70, 3)):
        top = brim_y(x) + 2
        for k in range(length):
            g.set(x + (1 if k > 5 else 0), top + k, MOSS if k % 3 else MOSS_LIT)
        g.set(x, top + length, MOSS_SH)


def tender():
    g = G(CELL)
    wake(g)
    crook(g)
    coat(g)
    cape(g)
    arms(g)
    head(g)
    lantern(g)
    rim(g, 2, FEET, skip="o" + FLAME_CH + WET + "J")
    contact(g, 18, 74, y=FEET)
    eyes(g, 36, 53, 57, "Y", "y")
    return g.rows()


# =================================================================================================
# THE FLAMES
# =================================================================================================

STRIKE_CELL = 96
# Where the struck ground is in the strike cell: the code places the frame so this pixel lands on
# the strike's centre. A ground circle round it of STRIKE_REACH pixels is the hit radius / 2
# (the PNG is saved at x2): LanternTenderEnemy.StrikeRadius 64 = 32 here.
STRIKE_GROUND = (48, 60)
STRIKE_REACH = 32
STRIKE_FRAMES = 8

EMBER_CELL = 32
EMBER_FRAMES = 4
OIL_CELL = 32
OIL_FRAMES = 6
# The oil tongue stands on this pixel of its cell.
OIL_FOOT = (16, 28)


def _tongue(c, x, y, height, half, lean, tones, core=True):
    """One tongue of flame standing on (x, y): widest a third of the way up, a point at the top that
    leans, nested stops so the core is a knot at the root rather than a stripe up the middle."""
    edge, mid, hot, white = tones[3], tones[2], tones[1], tones[0]
    for i in range(int(height) + 1):
        t = i / float(max(1, height))                         # 0 at the root, 1 at the tip
        w = half * math.sin(min(1.0, (t + 0.12) * 1.25) * math.pi * 0.62) * (1.0 - t) ** 0.35
        cx = x + lean * t * t
        yy = int(round(y - i))
        if w < 0.5:
            c.set(int(round(cx)), yy, edge if t > 0.8 else mid)
            continue
        c.hline(int(round(cx - w)), int(round(cx + w)), yy, edge)
        if w > 1.1:
            c.hline(int(round(cx - w + 1)), int(round(cx + w - 1)), yy, mid)
        if w > 2.0 and t < 0.75:
            c.hline(int(round(cx - w * 0.45)), int(round(cx + w * 0.45)), yy, hot)
        if core and w > 1.6 and t < 0.32:
            c.hline(int(round(cx - w * 0.3)), int(round(cx + w * 0.3)), yy, white)


def strike_strip():
    """A strike landing, eight frames, read at 14 fps: the splash, the column, the crown of tongues
    round the hit circle, then the column breaking and going out to embers.

    THE RING OF TONGUES IS THE HIT RADIUS. The furthest flames stand on a circle of STRIKE_REACH round
    the struck point, which is half the radius the code tests (the strip is saved at x2), so what
    burns is what was hit - the same rule the player's spells keep (.ai/art-direction.md section 3),
    held here for an enemy's so the warning ring and the fire agree."""
    tones = FLAME
    c = raster.Canvas(STRIKE_CELL * STRIKE_FRAMES, STRIKE_CELL, None)
    gx, gy = STRIKE_GROUND
    r = STRIKE_REACH

    def ring_tongues(ox, count, height, half, phase, keep=1.0):
        # Far half first, so the near tongues (the bottom of the circle) stand in front of them.
        order = sorted(range(count), key=lambda k: math.sin(phase + k * 2.0 * math.pi / count))
        for k in order:
            a = phase + k * 2.0 * math.pi / count
            if ((k * 7 + 3) % 10) / 10.0 >= keep:
                continue
            tx = gx + math.cos(a) * r * 0.92
            ty = gy + math.sin(a) * r * 0.92
            h = height * (0.7 + 0.3 * ((k * 5) % 4) / 3.0)
            _tongue(c, ox + tx, ty, h, half, (k % 3 - 1) * 1.5, tones, core=False)

    def scorch(ox, rad, tone):
        """The struck ground burning: a ragged blotch of the edge stop, holed and lumpy. A regular
        hatch here read as a net thrown over the fire."""
        for y in range(gy - rad, gy + rad + 1):
            for x in range(gx - rad, gx + rad + 1):
                d = math.sqrt((x - gx) ** 2 + (y - gy) ** 2)
                lump = 0.18 * math.sin(math.atan2(y - gy, x - gx) * 5.0 + rad) * rad
                if d > rad + lump:
                    continue
                hole = ((x * 73856093) ^ (y * 19349663)) % 100
                if d > rad * 0.55 and hole < 30:
                    continue
                c.set(ox + x, y, tone)

    # f0: the splash. The thrown flame hits and flattens: a bright pool, droplets flung to the rim.
    ox = 0
    c.disc(ox + gx, gy, 12, 12, tones[3])
    c.disc(ox + gx, gy, 9, 9, tones[2])
    c.disc(ox + gx, gy, 6, 6, tones[1])
    c.disc(ox + gx, gy, 3, 3, tones[0])
    for k in range(10):
        a = k * 2 * math.pi / 10 + 0.3
        d = r * (0.55 + 0.4 * (k % 3) / 2.0)
        c.disc(ox + gx + math.cos(a) * d, gy + math.sin(a) * d, 1.2, 1.2, tones[2] if k % 2 else tones[3])
    _tongue(c, ox + gx, gy, 14, 5, 0, tones)

    # f1: the column erupts; the first tongues catch round the rim.
    ox = STRIKE_CELL
    scorch(ox, 14, tones[3])
    c.disc(ox + gx, gy, 8, 8, tones[2])
    _tongue(c, ox + gx, gy + 2, 40, 9, -2, tones)
    ring_tongues(ox, 9, 12, 2.4, 0.2, keep=0.6)

    # f2: the full column and the full crown of tongues on the circle.
    ox = STRIKE_CELL * 2
    scorch(ox, 18, tones[3])
    ring_tongues(ox, 12, 18, 2.8, 0.0)
    _tongue(c, ox + gx, gy + 3, 56, 12, 3, tones)
    _tongue(c, ox + gx - 7, gy + 2, 30, 6, -4, tones)
    _tongue(c, ox + gx + 8, gy + 2, 26, 5, 5, tones)
    for (x, y) in ((gx - 4, 6), (gx + 9, 10), (gx - 12, 18)):
        c.set(ox + x, y, tones[2])                            # flecks thrown off the top

    # f3: still burning, the column leaning as it starts to tear.
    ox = STRIKE_CELL * 3
    scorch(ox, 18, tones[3])
    ring_tongues(ox, 12, 16, 2.7, 0.26)
    _tongue(c, ox + gx, gy + 3, 50, 11, -5, tones)
    _tongue(c, ox + gx + 9, gy + 2, 24, 5, 6, tones)
    for (x, y) in ((gx + 6, 4), (gx - 10, 9), (gx + 14, 16)):
        c.set(ox + x, y, tones[2])

    # f4: the column breaks: its head tears off and rises; the crown thins.
    ox = STRIKE_CELL * 4
    scorch(ox, 18, tones[3])
    ring_tongues(ox, 12, 12, 2.4, 0.5, keep=0.8)
    _tongue(c, ox + gx, gy + 2, 26, 9, 3, tones)
    c.disc(ox + gx - 2, gy - 40, 4, 5, tones[3])
    c.disc(ox + gx - 2, gy - 40, 2.5, 3, tones[2])
    c.set(ox + gx - 2, gy - 40, tones[1])

    # f5: low fire round the rim, a stump in the middle.
    ox = STRIKE_CELL * 5
    scorch(ox, 16, tones[3])
    ring_tongues(ox, 12, 8, 2.0, 0.7, keep=0.6)
    _tongue(c, ox + gx, gy + 1, 14, 6, -2, tones, core=False)
    c.disc(ox + gx + 3, gy - 46, 2.5, 3, tones[3])
    c.set(ox + gx + 3, gy - 46, tones[2])

    # f6: guttering: a few tongues left on the circle.
    ox = STRIKE_CELL * 6
    ring_tongues(ox, 12, 5, 1.6, 0.9, keep=0.35)
    _tongue(c, ox + gx, gy, 6, 3, 1, tones, core=False)

    # f7: embers.
    ox = STRIKE_CELL * 7
    for k in range(9):
        a = k * 2 * math.pi / 9 + 1.1
        d = r * (0.3 + 0.65 * ((k * 4) % 5) / 4.0)
        c.set(ox + int(round(gx + math.cos(a) * d)), int(round(gy + math.sin(a) * d)), tones[3] if k % 3 else tones[2])
    return c


def ember_strip():
    """The flame in flight, four flickering frames of a comet pointing +X: a round heavy head, a torn
    tail. The code rotates it to the way it is travelling, so it reads as THROWN and not dropped."""
    tones = FLAME
    c = raster.Canvas(EMBER_CELL * EMBER_FRAMES, EMBER_CELL, None)
    shapes = [
        # (head x, head radius, tail length, tail wobble)
        (20, 5.0, 13, 1.0),
        (21, 4.6, 15, -1.2),
        (20, 5.2, 12, 0.6),
        (21, 4.8, 14, -0.8),
    ]
    for i, (hx, hr, tail, wob) in enumerate(shapes):
        ox = EMBER_CELL * i
        cy = 16
        for k in range(tail):
            t = k / float(tail)
            x = hx - hr * 0.6 - k
            w = hr * (1.0 - t) ** 1.3
            y = cy + wob * math.sin(t * 4.0 + i)
            c.vline(ox + int(round(x)), int(round(y - w)), int(round(y + w)), tones[3])
            if w > 1.6:
                c.vline(ox + int(round(x)), int(round(y - w + 1)), int(round(y + w - 1)), tones[2])
        c.disc(ox + hx, cy, hr, hr, tones[3])
        c.disc(ox + hx, cy, hr - 1.2, hr - 1.2, tones[2])
        c.disc(ox + hx + 0.5, cy, hr - 2.3, hr - 2.4, tones[1])
        c.disc(ox + hx + 1, cy, 1.2, 1.2, tones[0])
        for (x, y) in ((hx - tail - 2, cy + (i % 3) - 1), (hx - tail + 3, cy - 4 + i), (hx + hr + 2, cy + 2 - i)):
            c.set(ox + int(x), int(y), tones[3])
    return c


def oil_strip():
    """One tongue of the burning oil: six frames on a loop, flickering in height and lean. The code
    lays a field of these over the wedge it spilled, offset in phase, out to the wedge's edge."""
    tones = FLAME
    c = raster.Canvas(OIL_CELL * OIL_FRAMES, OIL_CELL, None)
    fx, fy = OIL_FOOT
    shapes = [(16, 5.0, 1.0), (19, 4.6, -1.5), (14, 5.4, 2.0), (18, 4.8, 0.0), (21, 4.2, -2.4), (15, 5.2, 1.2)]
    for i, (h, half, lean) in enumerate(shapes):
        ox = OIL_CELL * i
        # The pool the tongue stands in: a dark slick with the flame's light on it.
        c.disc(ox + fx, fy + 1, 7, 2, B.MATERIALS["stone"][4])
        c.hline(ox + fx - 5, ox + fx + 5, fy + 1, tones[3])
        _tongue(c, ox + fx, fy, h, half, lean, tones)
        c.set(ox + fx + int(lean) + (i % 3) - 1, fy - h - 3, tones[3])     # a fleck above it
    return c


# =================================================================================================
# WRITING
# =================================================================================================

def write_tender(root=ROOT):
    pose = tender()
    problems = P.check_anchor("%s pose" % STEM, [pose], CELL)
    lean, reach, knock = FEEL
    spec = []
    for anim, frames, loop, speed in anim_sets.full_set(pose, lean=lean, reach=reach, knock=knock):
        png = "%s-%s.png" % (STEM, anim)
        P.write_strip(frames, PALETTE, os.path.join(root, "assets", "bonelight", "enemies", png), CELL)
        spec.append((anim, png, len(frames), loop, speed))
    spriteframes.write(os.path.join(root, "scenes", "resources", SHEET + ".tres"), spec,
                       "assets/bonelight/enemies", cell=CELL)
    return pose, sum(s[2] for s in spec), problems


def write_effects(root=ROOT):
    out = os.path.join(root, "assets", "bonelight", "effects")
    if not os.path.isdir(out):
        os.makedirs(out)
    made = []
    for name, canvas, cell, n in (("tender-strike", strike_strip(), STRIKE_CELL, STRIKE_FRAMES),
                                  ("tender-ember", ember_strip(), EMBER_CELL, EMBER_FRAMES),
                                  ("tender-oil", oil_strip(), OIL_CELL, OIL_FRAMES)):
        bad = canvas.audit()
        if bad:
            raise ValueError("%s has off-contract colours: %s" % (name, bad[:6]))
        canvas.scaled(2).save(os.path.join(out, name + ".png"))
        made.append((name, n, cell * 2))
    return made


def preview(pose, path):
    """The pose at x4 on the swamp's floor, its silhouette filled black beside it (the section 4
    test), and the derived strips under it."""
    from PIL import Image
    floor = (0x2C, 0x38, 0x34, 255)
    zoom = 4
    anims = anim_sets.full_set(pose, *FEEL)
    cols = max(len(f) for _n, f, _l, _s in anims)
    w = max(2, cols) * (CELL * 2 + 4) + 4
    h = (CELL * zoom + 8) + len(anims) * (CELL * 2 + 4) + 4
    sheet = Image.new("RGBA", (w, h), floor)
    big = P.render(pose, PALETTE).resize((CELL * zoom, CELL * zoom), Image.NEAREST)
    sheet.alpha_composite(big, (4, 4))
    black = Image.new("RGBA", big.size, (0, 0, 0, 255))
    sheet.paste(black, (8 + CELL * zoom, 4), big)
    y = CELL * zoom + 12
    for _name, frames, _loop, _speed in anims:
        for i, rows in enumerate(frames):
            im = P.render(P.pad(rows, CELL, CELL), PALETTE).resize((CELL * 2, CELL * 2), Image.NEAREST)
            sheet.alpha_composite(im, (4 + i * (CELL * 2 + 4), y))
        y += CELL * 2 + 4
    sheet.convert("RGB").save(path)


def main():
    pose, n, problems = write_tender()
    filled, edges, ratio = roster.density(pose)
    xs = [x for yy in range(CELL) for x in range(CELL) if pose[yy][x] != "."]
    ys = [yy for yy in range(CELL) for x in range(CELL) if pose[yy][x] != "."]
    print("  %-24s %dx%d  %2dw x %2dh  %4d px  %.2f edges/px%s"
          % (STEM, CELL, CELL, max(xs) - min(xs) + 1, max(ys) - min(ys) + 1, filled, ratio,
             "" if ratio >= 0.80 else "   SPARSE"))
    print("  %-24s %3d frames -> scenes/resources/%s.tres" % (STEM, n, SHEET))
    for name, frames, size in write_effects():
        print("  %-24s %d frames of %dx%d -> assets/bonelight/effects/%s.png" % (name, frames, size, size, name))
    out = os.path.join(HERE, "_cast")
    if not os.path.isdir(out):
        os.makedirs(out)
    path = os.path.join(out, "lantern-tender.png")
    preview(pose, path)
    print("preview: %s" % os.path.relpath(path, ROOT).replace(os.sep, "/"))
    if problems:
        print("ANCHOR PROBLEMS (contract section 5):")
        for p in problems:
            print("   " + p)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
