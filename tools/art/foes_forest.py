# -*- coding: utf-8 -*-
"""The Enchanted Forest's own enemies. Run: python tools/art/foes_forest.py

WHY THIS FILE EXISTS. The shipping roster is ten enemies and seven of them are the same silhouette:
a narrow top flaring to a wide flat base. Filled solid black - which is the test section 4 sets -
the swarmer, bruiser, shielder, lunger, slammer, summoner and hexer are one bell-shaped blob in
seven paint jobs. They also say nothing about where the player is standing: the same grey-green
humanoids turn up in the forest, the dungeon and the volcano.

So this is the first BIOME FAMILY. Everything here is built to break the bell shape, and the three
of them break it in three different directions:

  THE DIRE WOLF IS HORIZONTAL. It is the first enemy in the game that is wider than it is tall -
  about 30 x 20 against the cast's average of 20 x 28 - and the first with four legs. Nothing else
  in the game reads as an animal, and at a glance across a crowded arena that difference does more
  than any amount of interior detail.

  THE WISP HAS NO GROUND CONTACT AND ITS WINGS ARE WIDER THAN ITS BODY. A small mass with two much
  larger shapes either side of it, and a detached shadow underneath - section 4's Flyer row, which
  until now only the skull sentry used, and it used it timidly.

  THE BRAMBLE IS LOW AND SPRAWLING. Wider than tall, with thin tendrils reaching out of it, so its
  silhouette has holes in it. Every other enemy in the game is a solid mass.

WHAT MAKES THEM THE DARK WIZARD'S. The palette rules do not relax for a biome. These wear the deep
half of their ramps and carry the cold arcane mark in their eyes, exactly like the skeletons - the
fiction is that the forest was TAKEN, not that it is a different faction. A wolf with warm amber
eyes would read as wildlife; a wolf with two points of the same cold light the skeletons carry
reads as something that used to be wildlife.

Linen is the fur and foliage row here - #A2B0A0 down to #1A2220 is a grey-green, which is what a
forest looks like under no light, and it is already in the contract.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pixel as P
import roster

CELL = roster.ENEMY_CELL
FEET = CELL - 2
PALETTE = roster.PALETTE
G = roster.G


def _limb(g, x0, y0, x1, y1, thick, mid, lit, dark):
    """A tapering limb between two points. Used for legs, tendrils and the wolf's tail."""
    n = int(max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        w = thick * (1.0 - 0.55 * t)
        g.span(y, x - w, x + w, mid)
        g.set(x - w, y, lit)
        g.set(x + w, y, dark)


def wolf():
    """A dire wolf, head low, mid-stalk.

    HORIZONTAL IS THE WHOLE POINT. The cast is a column of upright figures; this is a bar, and
    filled solid black it is the only thing on screen that is not standing up.

    TWO PASSES WENT WRONG BEFORE THIS ONE, and both failures are worth naming because any quadruped
    at this size will hit them. The first drew a row of raised hackles along the spine and a thick
    triangular tail, and came out a STEGOSAURUS. The second fixed those and came out a TABLE: a
    plank of uniform height on four evenly spaced identical sticks.

    A table is what you get when you build a quadruped out of a rectangle and four lines. An animal
    is built out of MASSES - a haunch, a barrel, a shoulder, a skull - with the legs hanging off
    them, so this is drawn as four overlapping discs first and the outline falls out of that. The
    rear haunch is the biggest mass on the figure and it is what makes the back half read as an
    animal rather than as furniture; the legs are paired close under each mass with a wide gap
    between the pairs, which is what stops them being table legs.
    """
    g = G(CELL)
    mid, lit, dark = "E", "e", "A"

    # TAIL: thin, low, streaming back off the haunch.
    _limb(g, 10, 19, 2, 15, 1.3, mid, lit, dark)
    g.set(2, 14, dark)

    # LEGS. Paired close under each mass, with a wide gap between front and rear pairs, and each
    # bent at the joint. Four evenly spaced verticals is a table.
    for (hx, hy, kx, ky, px, thick) in ((10, 22, 9, 26, 10, 1.5), (13, 22, 14, 26, 14, 1.3),
                                        (21, 22, 20, 26, 20, 1.4), (24, 22, 25, 26, 25, 1.2)):
        _limb(g, hx, hy, kx, ky, thick, mid, lit, dark)
        _limb(g, kx, ky, px, FEET - 1, thick * 0.8, mid, lit, dark)
        g.span(FEET, px - 1.3, px + 1.3, dark)
        g.set(px - 1.3, FEET, mid)

    # THE MASSES. Haunch, barrel, shoulder - drawn as overlapping discs so the back has a line
    # rather than a flat top.
    g.disc(12, 19, 4.2, mid)          # rear haunch, the biggest mass on the animal
    g.disc(21, 19, 3.4, mid)          # shoulder
    for x in range(12, 22):           # the barrel between them, dipping at the waist
        t = (x - 12) / 9.0
        top = 17.0 + 1.2 * (1.0 - abs(t - 0.5) * 2.0)
        for y in range(int(round(top)), 23):
            g.set(x, y, mid)

    # The back line and the belly, lit and shaded, so the barrel is not a flat field.
    for x in range(9, 25):
        col = [y for y in range(12, 26) if g.get(x, y) == mid]
        if col:
            g.set(x, col[0], lit)
            g.set(x, col[-1], dark)
    g.set(12, 16, lit)
    g.set(21, 16, lit)

    # NECK: angles DOWN and forward off the shoulder. A level neck is a dog.
    for i, x in enumerate(range(23, 27)):
        top = 18.0 + i * 0.6
        for y in range(int(top), int(top) + 4):
            g.set(x, y, mid)
        g.set(x, int(top), lit)
        g.set(x, int(top) + 4, dark)

    # HEAD: skull mass plus a MUZZLE, which is the one feature that separates a wolf from a bear.
    g.disc(27, 20, 2.5, mid)
    for i, x in enumerate(range(29, 32)):
        t = i / 2.0
        for y in (20, 21):
            g.set(x, y + t * 0.8, mid)
        g.set(x, 20 + t * 0.8, lit)
        g.set(x, 22 + t * 0.8, dark)
    g.set(31, 22, dark)
    g.set(30, 23, "S")                # one tooth, and one is enough at this size
    g.span(23, 27, 29, dark)          # the jawline

    # EARS: small, back-swept, ON TOP of the skull.
    for (ex, lean) in ((26, -1), (28, 0)):
        g.set(ex, 17, mid)
        g.set(ex + lean, 16, mid)
        g.set(ex + lean, 15, dark)

    # A single raised line of fur along the spine - one pixel, not a row of spikes.
    for x in range(13, 22):
        g.set(x, 15 if x % 3 else 14, dark)

    g.span(FEET + 1, 8, 27, "o")
    roster.eyes(g, 19, 27, 29, "Y", "y")
    return g.rows()


def wisp():
    """A woodland fairy, taken. Small, winged, hovering.

    THE WINGS ARE WIDER THAN THE BODY, which is the whole silhouette: a six-pixel figure with a
    span either side of it that nothing else in the cast comes close to, and it survives being
    filled black because the wings have veins and gaps in them.

    THE FIRST PASS DREW THEM IN THE ARCANE ELEMENT RAMP, which is violet, and at this size four
    violet streaks either side of a dark body read as damage numbers rather than as wings - they
    did not even look attached. Wings are MEMBRANE: a solid shape with a lit leading edge and veins
    through it, in the same grey-green everything in this forest is made of. The only arcane on the
    figure is the pair of points in its face, which is the mark every taken thing carries and the
    only thing on an enemy allowed to glow.

    NO GROUND CONTACT: the `occ` shadow sits detached below, section 4's Flyer rule.

    It is NOT cute. Hunched, thin, a head a third of its height, and the same cold void for a face.
    A woodland fairy is exactly the sort of small harmless thing the fiction says he reached first.
    """
    g = G(CELL)
    mid, lit, dark = "E", "e", "A"

    # WINGS: upper pair long and swept up, lower pair short. Drawn as a filled membrane from a
    # leading edge downward, so each is one connected shape rather than a row of dashes.
    for (side, root_y, span, rise, depth) in ((-1, 13, 11, 5.0, 6.0), (1, 13, 11, 5.0, 6.0),
                                              (-1, 18, 6, 2.0, 3.5), (1, 18, 6, 2.0, 3.5)):
        for i in range(span):
            t = i / float(span - 1)
            x = 15.5 + side * (2.5 + i * 1.15)
            edge = root_y - rise * (t * 2.0 - t * t * 1.4)
            d = depth * (1.0 - t * 0.62)
            for k in range(int(d)):
                g.set(x, edge + k, dark if k else lit)
            if i % 3 == 2:                     # a vein, every third column
                for k in range(int(d)):
                    g.set(x, edge + k, mid)
            g.set(x, edge + int(d), "o")       # the trailing edge, occluded

    # BODY: thin and hunched, knees drawn up. It has not walked in a long time.
    for y in range(14, 21):
        t = (y - 14) / 6.0
        hw = 1.5 + 0.9 * t
        g.span(y, 15.5 - hw, 15.5 + hw, mid)
        g.set(15.5 - hw, y, lit)
        g.set(15.5 + hw, y, dark)
    for (lx, ly) in ((14, 21), (17, 21)):
        g.set(lx, ly, mid)
        g.set(lx, ly + 1, dark)

    _limb(g, 14, 16, 12, 19, 0.9, mid, lit, dark)
    _limb(g, 17, 16, 19, 19, 0.9, mid, lit, dark)

    # HEAD: large for the body, and mostly a void.
    g.disc(15.5, 12, 2.7, mid)
    g.set(13, 10, lit)
    roster.void(g, 11, 13, 2.0, 1.4, cx=15.5)
    for (lx, ly) in ((13, 9), (15, 8), (18, 9)):          # a ragged crown of leaves
        g.set(lx, ly, dark)
        g.set(lx, ly - 1, mid)

    # THE DETACHED SHADOW. The gap between it and the figure is the point.
    g.span(FEET, 12, 19, "o")
    g.span(FEET + 1, 13, 18, "o")

    roster.eyes(g, 12, 14, 17, "Y", "y")
    return g.rows()


def bramble():
    """A thorn-tangle that moves. Low, wide, and full of holes.

    Every other enemy in the game is a solid mass with a clean outline. This one is mostly gaps:
    a knot of vine near the ground with tendrils reaching out of it, so the silhouette is lace
    rather than a blob. That makes it the cheapest enemy in the cast to read in a crowd, because
    the eye finds the one shape it can see through.

    It is the swarmer role for this biome - small, many, no attack worth the name - so it is built
    to be legible at a glance and cheap in pixels.
    """
    g = G(CELL)
    mid, lit, dark = "A", "E", "o"
    wood, wood_lit = "j", "N"

    # THE KNOT: a low wide mass, wider than tall, sitting on the ground.
    for y in range(22, FEET + 1):
        t = (y - 22) / 8.0
        hw = 5.0 + 4.6 * t
        g.span(y, 15.5 - hw, 15.5 + hw, mid)
        g.set(15.5 - hw, y, lit)
        g.set(15.5 + hw, y, dark)

    # Woody strands running through it, so it is a tangle rather than a lump.
    for (sx0, sy0, sx1, sy1) in ((7, 29, 14, 23), (11, 30, 19, 24), (17, 30, 24, 25),
                                 (20, 29, 12, 26)):
        g.line(sx0, sy0, sx1, sy1, wood)
        g.line(sx0, sy0 - 1, sx1, sy1 - 1, wood_lit)

    # TENDRILS: four, reaching up and out at different heights and angles, each ending in a thorn.
    # Uneven on purpose - a symmetrical plant is a houseplant.
    for (tx0, ty0, tx1, ty1, th) in ((9, 24, 3, 14, 1.6), (13, 23, 10, 11, 1.4),
                                     (19, 23, 24, 13, 1.5), (22, 25, 29, 18, 1.3)):
        _limb(g, tx0, ty0, tx1, ty1, th, wood, wood_lit, "o")
        # the thorn on the end, and a couple down the length
        g.set(tx1, ty1 - 1, "p")
        g.set(tx1 - 1, ty1, "p")
        mx, my = (tx0 + tx1) / 2.0, (ty0 + ty1) / 2.0
        g.set(mx + 1, my, "p")

    # A hollow in the middle of the knot, with the mark in it. The one dark centre in a shape made
    # of gaps, so the eye knows where the thing actually is.
    roster.void(g, 24, 28, 3.0, 2.2, cx=15.5)
    g.span(23, 12, 19, dark)

    g.span(FEET + 1, 5, 26, "o")
    roster.eyes(g, 25, 14, 18, "Y", "y")
    return g.rows()


FOREST = [("wolf", wolf), ("wisp", wisp), ("bramble", bramble)]


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_forest")
    if not os.path.isdir(out):
        os.makedirs(out)
    print("forest family (%dx%d):" % (CELL, CELL))
    for name, fn in FOREST:
        rows = fn()
        filled, edges, ratio = roster.density(rows)
        xs = [x for y in range(CELL) for x in range(CELL) if rows[y][x] != "."]
        ys = [y for y in range(CELL) for x in range(CELL) if rows[y][x] != "."]
        P.write_strip([rows], PALETTE, os.path.join(out, "%s-pose.png" % name), CELL)
        print("  %-9s %2dw x %2dh  %3d px  %.2f edges/px%s"
              % (name, max(xs) - min(xs) + 1, max(ys) - min(ys) + 1, filled, ratio,
                 "" if ratio >= 0.80 else "   SPARSE"))
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
