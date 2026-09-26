# -*- coding: utf-8 -*-
"""Cinderbreath flame puffs. Run: python tools/art/spell_flames.py

A five-frame strip of one tongue of flame, travelling +X, for the individual puffs Cinderbreath
throws. One horizontal strip, no padding, 32x32 cells per the projectile row of art-direction.md
section 1, rendered at x2 like everything else.

WHY FIVE SEPARATE PUFFS INSTEAD OF ONE CONE. The first version of the spell drew its whole cone in
_Draw as three shaded polygons. It was honest about its reach and it cost no art, but it read as a
lit wedge rather than as fire: nothing in it moved except a sine wobble on the edge, so the eye had
nothing to track. Fire in this genre reads because you can see individual tongues leave the caster,
travel, and die. That is what this sheet is for.

THE FRAMES ARE A LIFE, NOT A LOOP. Frame 0 is a bud at the nozzle, 2 is the full tongue, and 4 is
three embers with no body left. A puff plays the strip once over its whole flight and is freed, so
the animation IS the range indicator - when the flame has gone out, it has stopped hurting things.
Looping it would make a puff that fades and then relights halfway down the cone.

DIRECTIONALITY. Drawn pointing +X because FlamePuff sets Rotation from its heading, the same
contract ElementalBolt uses. The tail is blunt and the tip is pointed, so a rotated puff still
reads as something thrown rather than something dropped.

Palette: the fire element ramp from art-direction.md section 3, under that section's rule - the
core is white-hot and the identity lives in the mid and edge. Nothing here is a material tone; a
flame is emissive.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")

CELL = 32
FRAMES = 5

CORE = bl.ELEMENTS["fire"][0]
HOT = bl.ELEMENTS["fire"][1]
MID = bl.ELEMENTS["fire"][2]
EDGE = bl.ELEMENTS["fire"][3]


def _shift(points, ox):
    return [(x + ox, y) for (x, y) in points]


def _tongue(c, ox, outline, body, inner, core, flecks):
    """One frame: four nested teardrops plus detached flecks.

    Nested rather than separately positioned, because a flame is one mass seen through stages of
    heat - drawing the core as its own shape somewhere inside the body is what makes a flame read
    as a glowing ball with a tail instead of as fire.
    """
    c.poly(_shift(outline, ox), EDGE)
    c.poly(_shift(body, ox), MID)
    c.poly(_shift(inner, ox), HOT)
    if core:
        c.poly(_shift(core, ox), CORE)

    for (x, y, tone) in flecks:
        c.set(x + ox, y, tone)


def flame_strip():
    """Five frames of one tongue, drawn as a COMET rather than as a blob.

    The first pass drew each frame as nested ellipses with a horizontal bar of core through the
    middle. It was the right colour and completely the wrong shape: symmetrical on both axes, so a
    puff rotated to its heading looked identical travelling left, right or backwards, and the flat
    core bar read as a glowing lozenge. A thrown flame has a dense leading head and a torn tail
    streaming behind it, and the asymmetry is the whole reason the eye reads motion.

    So: head round and heavy at +X, tail tapering to a point at -X, edges deliberately uneven top
    against bottom, and the core a knot inside the head instead of a stripe down the middle.
    """
    c = raster.Canvas(CELL * FRAMES, CELL, None)

    # --- frame 0: a bud leaving the nozzle --------------------------------------------
    _tongue(
        c, 0,
        outline=[(11, 17), (15, 13), (19, 12), (22, 14), (23, 17), (21, 20), (17, 21), (13, 20), (11, 17)],
        body=[(13, 17), (16, 14), (19, 14), (21, 16), (20, 19), (16, 19), (13, 17)],
        inner=[(15, 17), (17, 15), (19, 16), (19, 18), (16, 18), (15, 17)],
        core=[(17, 16), (18, 16), (18, 17), (17, 17), (17, 16)],
        flecks=[(9, 16, EDGE), (8, 19, EDGE), (25, 15, MID)],
    )

    # --- frame 1: the tail catches up -------------------------------------------------
    _tongue(
        c, CELL,
        outline=[(5, 18), (10, 14), (15, 11), (20, 10), (24, 12), (26, 16), (24, 20), (19, 22), (13, 21), (8, 20), (5, 18)],
        body=[(9, 18), (13, 14), (18, 12), (22, 14), (23, 17), (21, 20), (16, 20), (11, 19), (9, 18)],
        inner=[(13, 17), (17, 14), (20, 15), (21, 18), (17, 19), (13, 18), (13, 17)],
        core=[(17, 16), (19, 16), (19, 18), (17, 18), (17, 16)],
        flecks=[(3, 17, EDGE), (2, 20, EDGE), (28, 14, MID), (29, 18, EDGE), (17, 8, MID)],
    )

    # --- frame 2: the full tongue -----------------------------------------------------
    _tongue(
        c, CELL * 2,
        outline=[(2, 19), (7, 15), (12, 11), (18, 8), (24, 9), (28, 13), (29, 17), (27, 21), (22, 24), (16, 23), (10, 22), (5, 21), (2, 19)],
        body=[(7, 19), (12, 14), (18, 11), (23, 12), (26, 16), (25, 20), (20, 22), (14, 21), (9, 20), (7, 19)],
        inner=[(12, 18), (17, 14), (22, 15), (24, 17), (22, 20), (17, 20), (12, 18)],
        core=[(18, 16), (21, 16), (22, 18), (19, 19), (17, 18), (18, 16)],
        flecks=[(0, 18, EDGE), (1, 22, EDGE), (4, 16, MID), (31, 15, MID), (30, 20, EDGE), (20, 5, MID), (13, 26, EDGE), (26, 25, EDGE)],
    )

    # --- frame 3: the head tears off the tail -----------------------------------------
    _tongue(
        c, CELL * 3,
        outline=[(4, 20), (8, 17), (12, 16), (14, 19), (11, 22), (6, 22), (4, 20)],
        body=[(6, 20), (9, 18), (12, 19), (10, 21), (7, 21), (6, 20)],
        inner=[(8, 19), (10, 20), (8, 21), (8, 19)],
        core=[],
        flecks=[(2, 23, EDGE), (15, 15, EDGE)],
    )
    # The head, already separate and still the hottest thing in the frame.
    c.poly(_shift([(18, 16), (23, 12), (28, 14), (29, 18), (26, 22), (21, 22), (17, 19), (18, 16)], CELL * 3), EDGE)
    c.poly(_shift([(20, 17), (24, 14), (27, 16), (26, 20), (22, 21), (19, 19), (20, 17)], CELL * 3), MID)
    c.poly(_shift([(22, 17), (25, 17), (25, 20), (22, 19), (22, 17)], CELL * 3), HOT)
    c.set(23 + CELL * 3, 18, CORE)
    for (x, y, tone) in [(31, 13, EDGE), (30, 22, EDGE), (24, 9, MID), (16, 24, EDGE)]:
        c.set(x + CELL * 3, y, tone)

    # --- frame 4: embers, no body -----------------------------------------------------
    ox = CELL * 4
    for (cx, cy, r) in [(24, 17, 2.4), (16, 20, 1.8), (9, 15, 1.5)]:
        c.disc(cx + ox, cy, r, r, EDGE)
        c.disc(cx + ox, cy, r - 1.0, r - 1.0, MID)
        c.set(cx + ox, cy, HOT)
    for (x, y, tone) in [(5, 19, EDGE), (12, 12, EDGE), (20, 24, EDGE), (29, 12, EDGE), (30, 21, EDGE)]:
        c.set(x + ox, y, tone)

    return c


def main():
    canvas = flame_strip()

    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS (art-direction.md section 3):")
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return 1

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    path = os.path.join(OUT_DIR, "cinderbreath-flame.png")
    canvas.scaled(2).save(path)
    print("  cinderbreath-flame   %dx%d  (%d frames of %d)" %
          (CELL * FRAMES * 2, CELL * 2, FRAMES, CELL * 2))
    print("")
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
