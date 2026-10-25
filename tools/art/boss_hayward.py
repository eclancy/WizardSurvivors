# -*- coding: utf-8 -*-
"""The Fallow Fields' boss: the Hayward (.ai/campaign.md section 5). Run: python tools/art/boss_hayward.py

    assets/bonelight/enemies/fields-hayward-{moving,attack,hurt,death}.png
    scenes/resources/FieldsHaywardFrames.tres
    tools/art/_boss_hayward.png             (gitignored) the pose at x4, and filled black

The parish officer who kept the hedges and impounded strays, walking behind a hurdle - a woven
fence panel - held upright before him. "He keeps his hurdle between you and him. Go round to his
back." Drawn on the 96 boss cell like the other chapter bosses (sprite_bosses.py), and its own
module so this chapter never edits that file.

ITS OUTLINE: A FIGURE BEHIND A DOOR. The other bosses are a T, a bell, a dome, a slab with
tablets, a wedge, an S and an H (sprite_bosses.py). This is an upright rectangle with a man's
head and hat above its left shoulder and a staff with a hook rising behind him - and the hurdle is
on the side he FACES, because which side it is on is the whole fight. HaywardBoss.cs flips the
sprite with the hurdle's facing rather than with the player, so the drawn panel and the guarded
front are always the same side.

THE HURDLE IS WOVEN, AND THE WEAVE IS THE DENSITY. Upright stakes (sails) every few pixels, and
withies woven over and under them in courses - each course lit where it passes over a stake and
dark where it passes under, alternating course by course - so the panel is a basket wall and not
a door. Hazel and willow in gold's shade and base, lit on the key side, the stakes a stop darker,
their sharpened feet below the bottom course. The man is linen, the people of the land, in a
broad hat; his hand on the top rail; the mark under the brim is the one cold light on him.
"""
from __future__ import print_function

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import anim_sets
import pixel as P
import roster
import spriteframes
import sprite_bosses as SB
from cast_fields import PALETTE, GOLD_HI, GOLD_LIT, GOLD, GOLD_SH, GOLD_DP
from cast_ruins import poly

G = roster.G
CELL = 96
FEET = CELL - 2
STEM = "fields-hayward"
SHEET = "FieldsHaywardFrames"
OUT_ENEMIES = os.path.join("assets", "bonelight", "enemies")

# Boss feel (build.py's BOSS_FEEL): a man walking behind a heavy panel barely sways, drives his
# whole weight into the shove, and hardly flinches - the hurdle takes it.
FEEL = (0.35, 5.5, 1)

# The hurdle's frame, in cell pixels: its left and right edges, its top and bottom courses. The
# far (top) edge is shifted a little toward the key, which is all the perspective a panel square to
# the viewer needs to read as standing in three-quarter.
H_LEFT, H_RIGHT = 50, 86
H_TOP, H_BOTTOM = 24, 84
H_SKEW = 2


def hurdle_x(x, y):
    """The column x of the panel's face, slid by the skew at row y (more at the top)."""
    t = (H_BOTTOM - y) / float(H_BOTTOM - H_TOP)
    return int(round(x - H_SKEW * t))


def hurdle(g):
    """The woven panel: stakes, then the courses woven through them, then the twisted top rail."""
    stakes = list(range(H_LEFT, H_RIGHT + 1, 6))
    if stakes[-1] != H_RIGHT:
        stakes.append(H_RIGHT)
    # The face first, in deep shadow: what shows between the rods is the dark behind the panel.
    for y in range(H_TOP, H_BOTTOM + 1):
        g.span(y, hurdle_x(H_LEFT, y), hurdle_x(H_RIGHT, y), GOLD_DP)
    # THE COURSES: one withy rod per three rows - two rows of rod and a row of shadow under it -
    # running the whole width, continuous, so it reads as rods and not as blocks. Where a rod
    # passes IN FRONT of a stake it bows out toward the light (a pixel up, its top lit); where it
    # passes behind one the stake is drawn over it. The next course goes the other way round.
    # (Blocks of lit and dark in a grid, the first try, was a brick wall.)
    courses = list(range(H_TOP + 3, H_BOTTOM - 1, 3))
    for band, y0 in enumerate(courses):
        for x in range(H_LEFT, H_RIGHT + 1):
            seg = sum(1 for s in stakes if s <= x) - 1
            nearest = min(stakes, key=lambda s: abs(s - x))
            in_front = (stakes.index(nearest) + band) % 2 == 0
            bow = -1 if (in_front and abs(nearest - x) <= 1) else 0
            top, under = y0 + bow, y0 + 1 + bow
            xx = hurdle_x(x, top)
            lit = in_front and abs(nearest - x) <= 2 and (band + x) % 4 != 3
            g.set(xx, top, GOLD_LIT if lit else GOLD)
            g.set(hurdle_x(x, under), under, GOLD_SH if (x + band) % 5 else GOLD)
            g.set(hurdle_x(x, y0 + 2), y0 + 2, "o" if (x * 3 + band) % 5 == 0 else GOLD_DP)
    # Weathering: grey where the rain sits on a rod, and a broken end sticking out past the edge.
    for (x, y) in ((58, 40), (71, 55), (63, 70), (79, 33), (54, 61), (75, 78), (66, 46), (82, 64)):
        g.set(hurdle_x(x, y), y, "e")
        g.set(hurdle_x(x + 1, y), y, "E")
    for (x, y, ch) in ((H_RIGHT + 1, 42, GOLD_SH), (H_RIGHT + 2, 41, GOLD), (H_RIGHT + 1, 69, GOLD_DP),
                       (H_RIGHT + 2, 70, GOLD_SH)):
        g.set(hurdle_x(x, y), y, ch)
    # THE STAKES: drawn over every course except the ones that pass in front of them.
    for i, s in enumerate(stakes):
        for y in range(H_TOP - 3, H_BOTTOM + 5):
            xx = hurdle_x(s, y)
            course = next((b for b, y0 in enumerate(courses) if y0 - 1 <= y <= y0 + 1), None)
            if course is not None and (i + course) % 2 == 0:
                continue                                # this course's rod passes in front here
            g.set(xx, y, GOLD_SH if (y // 4) % 3 else GOLD)
            g.set(xx + 1, y, GOLD_DP)
        # the sharpened foot
        foot = H_BOTTOM + 5
        g.set(hurdle_x(s, foot), foot, GOLD_DP)
        g.set(hurdle_x(s, foot - 1), foot - 1, GOLD_SH)
        # the cut top, pale
        g.set(hurdle_x(s, H_TOP - 3), H_TOP - 3, "l")
    # THE TOP RAIL: the withies twisted round the top, a rope-like edge lit on its upper side.
    for x in range(H_LEFT - 1, H_RIGHT + 2):
        for (dy, tone) in ((0, GOLD if x % 3 else GOLD_LIT), (1, GOLD_SH if x % 3 != 1 else GOLD), (2, GOLD_DP)):
            g.set(hurdle_x(x, H_TOP + dy), H_TOP + dy, tone)
    # The bottom edge: the last course's shadow on the ground below it is the panel standing off it.
    for x in range(H_LEFT, H_RIGHT + 1):
        g.set(hurdle_x(x, H_BOTTOM + 1), H_BOTTOM + 1, "o")
    # The key rim along the panel's left (lit) edge where nothing stands beside it.
    for y in range(H_TOP - 3, H_BOTTOM + 1):
        x = hurdle_x(H_LEFT, y) - 1
        if g.get(x, y) == ".":
            g.set(x, y, "W")


def hayward():
    g = G(CELL)

    # THE STAFF first, behind everything: a long rod up past his shoulder, an iron hook on top -
    # the pound-keeper's crook for catching strays by the leg.
    roster.shaft(g, 34, 86, 18, 6, "l", "e", "A", width=3)
    for (x, y, ch) in ((17, 5, "r"), (16, 4, "t"), (15, 3, "t"), (14, 3, "r"), (13, 4, "R"), (12, 5, "R"),
                       (12, 6, "q"), (13, 7, "r"), (14, 8, "q"), (18, 6, "R"), (16, 5, "q"), (19, 7, "q")):
        g.set(x, y, ch)                                 # the hook, curling back
    g.rect(19, 9, 21, 11, "R")                          # the iron ferrule binding it on
    g.set(19, 9, "t")
    g.set(21, 11, "q")

    # THE LEGS AND BOOTS, below the coat, left of the panel.
    for (lx, tone, lit) in ((28, "J", "N"), (40, "j", "N")):
        g.rect(lx, 82, lx + 6, FEET - 1, tone)
        g.line(lx, 82, lx, FEET - 1, lit)
        g.rect(lx - 1, FEET - 2, lx + 8, FEET, "q")
        g.span(FEET - 2, lx - 1, lx + 8, "R")
        g.set(lx - 1, FEET - 2, "r")

    # THE COAT: long, heavy, a shoulder cape over it, hanging to the shins. Linen in its deep half.
    SB.swell(g, 26, 84, [11.0, 15.0, 16.5, 16.0, 15.0, 15.5, 17.0], "E", "e", "A", cx=40.0)
    for pts in ([(29, 34), (27, 58), (26, 82)], [(36, 36), (35, 60), (34, 83)], [(44, 38), (45, 62), (46, 83)]):
        roster.fold(g, pts, "A", "e")
    # the split up the back of the coat, and its ragged hem
    for y in range(66, 85):
        g.set(31, y, "o")
        g.set(32, y, "A")
    for (x, d) in zip(range(23, 58), [1, 2, 1, 0, 2, 1, 1, 2, 0, 1, 2, 1, 0, 1, 1, 2, 1, 0, 2, 1, 0, 1, 2, 1, 1, 0,
                                      1, 2, 1, 0, 1, 1, 2, 1, 0]):
        for k in range(1, d + 1):
            g.set(x, 84 + k, "A" if k < d else "E")
    # THE SHOULDER CAPE, a stop lighter, its edge catching the key, buttoned at the throat.
    SB.swell(g, 22, 40, [9.0, 14.0, 17.0, 18.5], "e", "l", "E", cx=40.0)
    for x in range(23, 59):
        if g.get(x, 40) not in (".", "W"):
            g.set(x, 41, "A" if x % 4 else "o")
    roster.fold(g, [(34, 26), (31, 39)], "E", "l")
    roster.fold(g, [(46, 26), (49, 39)], "E", None)
    # THE BELT, with a cow-horn on a strap at the hip and the brass badge of his office.
    g.span(58, 24, 52, "J")
    g.span(59, 24, 52, "o")
    g.rect(40, 57, 43, 60, GOLD_SH)
    g.set(40, 57, GOLD_LIT)
    g.set(42, 59, GOLD_DP)
    for (x, y, ch) in ((26, 60, "k"), (25, 61, "k"), (24, 62, "n"), (24, 63, "n"), (25, 64, "m"), (26, 64, "m"),
                       (27, 61, "s"), (27, 62, "n"), (28, 62, "m")):
        g.set(x, y, ch)                                 # the horn, for calling the pinder

    # THE FAR HAND on the staff.
    g.rect(31, 62, 34, 65, "m")
    g.set(31, 62, "n")

    # THE HURDLE, over the front of him.
    hurdle(g)

    # THE HEAD above the hurdle's left shoulder, under a broad hat: the brim is the widest thing on
    # his top line, and the mark is under it.
    SB.swell(g, 13, 24, [5.0, 6.0, 6.0, 5.0], "E", "e", "A", cx=42.0)
    roster.void(g, 15, 22, 3.6, 2.6, cx=43.5)
    # the hat: a low crown, a brim drooping at the back, a band
    g.span(13, 30, 55, "J")
    g.span(12, 29, 56, "j")
    g.span(14, 31, 54, "o")
    g.set(29, 13, "j")
    g.set(28, 13, "J")
    g.set(56, 13, "J")
    SB.swell(g, 4, 11, [5.0, 7.0, 7.5, 8.0], "j", "N", "J", cx=42.0)
    g.span(10, 35, 49, GOLD_DP)                         # the hat band, old leather
    g.span(11, 34, 50, "J")
    for (x, y) in ((37, 5), (36, 6), (35, 8)):
        g.set(x, y, "p")                                # the crown's lit side
    # THE NEAR HAND on the hurdle's top rail, knuckles over it.
    for (x, y, ch) in ((51, 22, "n"), (52, 22, "k"), (53, 22, "k"), (54, 22, "n"), (51, 23, "m"), (52, 23, "n"),
                       (53, 23, "n"), (54, 23, "m"), (52, 21, "s"), (53, 21, "k")):
        g.set(x, y, ch)
    roster.shaft(g, 46, 30, 51, 23, "e", "E", "A", width=2)  # the forearm, from the cape to the rail

    rim_left(g)
    roster.contact(g, 18, 88, y=FEET)
    roster.eyes(g, 18, 42, 46, "Y", "y")
    return g.rows()


def rim_left(g):
    """The 1 px key rim on the left edge of each row, outside the hurdle (which has its own)."""
    for y in range(2, FEET):
        for x in range(1, 50):
            ch = g.get(x, y)
            if ch == "W":
                break
            if ch != "." and g.get(x - 1, y) == ".":
                run = 0
                while g.get(x + run, y) not in (".", "W") and x + run < CELL:
                    run += 1
                if run >= 2:
                    g.set(x - 1, y, "W")
                break


def build(root=ROOT):
    pose = hayward()
    problems = P.check_anchor("%s pose" % STEM, [pose], CELL)
    lean, reach, knock = FEEL
    spec = []
    for anim, frames, loop, speed in anim_sets.full_set(pose, lean=lean, reach=reach, knock=knock):
        png = "%s-%s.png" % (STEM, anim)
        P.write_strip(frames, PALETTE, os.path.join(root, OUT_ENEMIES, png), CELL)
        spec.append((anim, png, len(frames), loop, speed))
    spriteframes.write(os.path.join(root, "scenes", "resources", SHEET + ".tres"), spec,
                       OUT_ENEMIES.replace(os.sep, "/"), cell=CELL)
    return pose, sum(a[2] for a in spec), problems


def main():
    pose, n, problems = build()
    filled, edges, ratio = roster.density(pose)
    print("%s  %dx%d  %d px  %.2f edges/px%s  %d frames -> scenes/resources/%s.tres"
          % (STEM, CELL, CELL, filled, ratio, "" if ratio >= 0.80 else "   SPARSE", n, SHEET))
    black = ["".join("o" if ch != "." else "." for ch in row) for row in pose]
    P.preview([("pose", [pose]), ("silhouette", [black])], dict(PALETTE, o="#05070C"),
              os.path.join(HERE, "_boss_hayward.png"), zoom=4, bg=(46, 34, 8), cell=CELL)
    if problems:
        print("ANCHOR PROBLEMS:")
        for p in problems:
            print("   " + p)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
