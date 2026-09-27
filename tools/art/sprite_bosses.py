# -*- coding: utf-8 -*-
"""The seven remaining chapter bosses, plus the Still Warden's guard.

Run: python tools/art/sprite_bosses.py     -> tools/art/_bosses/ (gitignored)
Shipping build: python tools/art/build.py

Elderbark already had `sprite_treant.py`; this is the other seven, drawn to the same contract and
on the same 96x96 boss cell. `.ai/art-direction.md` section 1 is explicit that a boss may not be a
scaled basic enemy, so none of these is one.

THE ONE RULE THAT SHAPED ALL SEVEN: SILHOUETTE FIRST, AND NO TWO ALIKE.

A player meets these fifteen minutes apart, across eight hours, with a swarm on screen. Nothing
about a boss is read from its interior at that moment - it is read from its outline, once, in the
half second before deciding which way to run. So the seven were designed as seven OUTLINES before
a single pixel of detail was placed, and they are deliberately different classes of shape:

    Gaoler        a T      - a low four-legged mount with a vertical rider on it
    Hollow Choir  a bell   - narrow, headless, no limbs at all
    Mother Rot    a dome   - the only boss wider than it is tall
    Archivist     a slab   - a thin vertical with two DETACHED tablets beside it
    Still Warden  a wedge  - broad frozen base narrowing to armoured shoulders
    Long Coil     an S     - a rearing curve, mass at the top, nothing at the bottom
    Deep Warden   an H     - the only one with two heavy legs and horns

Say those seven words to someone who has played the game and they can name the fight. That is the
test this file is written against, and it is a much harder one than the density metric.

WHAT THEY ARE MADE OF, from `.ai/world-and-tone.md` rather than from taste. Six of the seven are
things the dark wizard TOOK, so they wear the deep half of their material rows and carry the cold
arcane mark - two pixels, and no more, because the mark means "occupied" and a thing covered in it
reads as decorated instead. The Warden of the Deep is the exception: it was BUILT, in his own
seat, so it burns. That single difference in emissive colour is the last boss announcing whose it
is, before it has moved.

DENSITY, AND THE FIRST PASS THAT FAILED IT. Section 4b asks 0.80 internal edges per filled pixel.
The first version of this file came in between 0.35 and 0.50 on six of the eight, and looking at
the contact sheet said exactly why: every mass was ONE character wide. A body filled with a single
tone has no interior at all, so at 96px it is a shape with a colour, and a shape with a colour at
that size reads as furniture - the Gaoler was a table, the Archivist was a bookcase, the Long Coil
was a boot.

What fixed it was not more speckle. It was giving each figure a STRUCTURE that happens to be made
of boundaries: courses for masonry, ridges for plate, facets for ice, scale rows for the serpent,
veins for the swollen. Those are the four helpers below, and the reason they are separate helpers
rather than one noise function is that the noise function was tried first and every sprite came
out wearing the same gravel.
"""
import os

import bonelight as B
import pixel as P
import roster

CELL = 96
GUARD_CELL = 48
CX = 47.5

# The roster palette is already everything these need: material rows for the bodies, element ramps
# for the marks. No new rows - the contract asks for a written amendment before one is added, and
# nothing here wanted a colour the table does not have.
PALETTE = dict(roster.PALETTE)
G = roster.G


# --- the texture toolkit ----------------------------------------------------------------------
# Each of these recolours pixels that are ALREADY THERE. That ordering matters: drawing texture
# into a mass after the mass exists means the texture can never leak past the silhouette, which is
# how the first pass ended up with barding hanging in mid-air beside the horse.

def ridges(g, y0, y1, only, dark, lit, step=3, jitter=5):
    """Horizontal ridge lines across plate. The armour texture.

    `only` is the tone it is allowed to overwrite, so a ridge stops at the edge of the plate
    instead of running across whatever is behind it. Rows are jittered: evenly spaced ridges at
    any cell size read as a radiator, which is the failure the roster notes record talking a 0.90
    density sprite into the bin.
    """
    for y in range(int(y0), int(y1) + 1):
        if (y + (y * y) // jitter) % step:
            continue
        run = 0
        for x in range(g.cell):
            if g.get(x, y) == only:
                g.set(x, y, dark)
                run += 1
            elif run:
                g.set(x - run, y, lit)
                run = 0


def courses(g, y0, y1, only, joint, face, high=7, wide=13):
    """Masonry: horizontal joints every `high` rows, vertical joints every `wide`, staggered.

    The stagger is the whole thing. Aligned vertical joints are a grid and a grid is a tiled
    floor; offsetting alternate courses by half a block is what makes stone read as laid.
    """
    for y in range(int(y0), int(y1) + 1):
        course = (y - int(y0)) // high
        if (y - int(y0)) % high == 0:
            for x in range(g.cell):
                if g.get(x, y) == only:
                    g.set(x, y, joint)
            continue
        offset = (course % 2) * (wide // 2)
        for x in range(g.cell):
            if g.get(x, y) == only and (x + offset) % wide == 0:
                g.set(x, y, joint)
            elif g.get(x, y) == only and (x + offset) % wide == 1:
                g.set(x, y, face)


def facets(g, planes, edge, high):
    """Flat-shaded triangles for ice and crystal. Hard edges, never a gradient.

    Ice at this size is planes catching light. A soft one reads as water, which is a different
    element in this palette and would be a lie about what the fight does.
    """
    for (fx, fy, fw, fh, fill) in planes:
        for i in range(fh):
            w = fw * (1.0 - i / float(fh))
            g.span(fy + i, fx - w * 0.5, fx + w * 0.5, fill)
        g.line(fx - fw * 0.5, fy + fh - 1, fx, fy, edge)
        g.line(fx, fy, fx + fw * 0.5, fy + fh - 1, high)


def veins(g, strokes, tone):
    """Short curved strokes under a surface. For the swollen and the fungal.

    Short and MANY, rather than long and few: the first pass drew full-length threads down Mother
    Rot and they read as a string bag over her rather than as anything inside her.
    """
    for (vx, vy, vlen, bow) in strokes:
        for i in range(vlen):
            t = i / float(max(1, vlen - 1))
            x = vx + i
            y = vy + bow * (t - t * t) * 4.0
            if g.get(x, y) != ".":
                g.set(x, y, tone)


def scales(g, path, mid, high, every=3):
    """Overlapping arcs along a swept path. The serpent texture.

    Arcs rather than a hatch: a hatch at this size is a mesh, but an arc has a direction, and its
    direction is what carries the curve of a neck that is only twelve pixels wide.
    """
    for i, (px, py, pr) in enumerate(path):
        if i % every:
            continue
        for k in range(-2, 3):
            sx = px + k * (pr * 0.42)
            sy = py - pr * 0.30 + abs(k) * 0.9
            if g.get(sx, sy) != ".":
                g.set(sx, sy, high if (i + k) % 3 else mid)
            if g.get(sx, sy + 1) != ".":
                g.set(sx, sy + 1, "o" if (i + k) % 4 == 0 else mid)


def rivets(g, spots, head, shade):
    """Two-pixel fasteners. The cheapest boundary there is, and the one that says 'made'."""
    for (rx, ry) in spots:
        if g.get(rx, ry) != ".":
            g.set(rx, ry, head)
        if g.get(rx, ry + 1) != ".":
            g.set(rx, ry + 1, shade)


def spike(g, x, y, dx, dy, half, mid, lit, dark):
    """A tapered point, drawn as spans rather than as offset lines.

    A thick diagonal built from offset Bresenham lines interleaves into a checkerboard, which this
    project has now paid for twice - once on Scorching Ray and once on the first crown of horns
    that came out of here.
    """
    n = int(max(abs(dx), abs(dy)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        px = x + dx * t
        py = y + dy * t
        w = half * (1.0 - t)
        g.span(py, px - w, px + w, mid)
        g.set(px - w, py, lit)
        g.set(px + w, py, dark)


def limb(g, joints, half, mid, lit, dark):
    """A jointed limb: a list of points, constant thickness, with a swelling at each joint.

    The swelling is what stopped the first pass reading as an insect. Four straight sticks under a
    barrel are legs on a table; four sticks with knees in them are legs on an animal.
    """
    for j in range(len(joints) - 1):
        x0, y0 = joints[j]
        x1, y1 = joints[j + 1]
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + 1):
            t = i / float(max(1, n))
            px = x0 + (x1 - x0) * t
            py = y0 + (y1 - y0) * t
            g.span(py, px - half, px + half, mid)
            g.set(px - half, py, lit)
            g.set(px + half, py, dark)
    for (jx, jy) in joints[1:-1]:
        g.disc(jx, jy, half + 1.2, mid)
        g.disc(jx - 0.8, jy - 0.8, half * 0.55, lit)
        g.set(jx + half, jy + 1, dark)


def hollow(g, y0, y1, hw0, hw1, cx=CX):
    """A socket: tapered occlusion, because a rectangle of black reads as a letterbox."""
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        g.span(y, cx - hw, cx + hw, "o")


def mark(g, x, y, hot, cool):
    """THE mark. Two pixels, and the whole rule is that there are only ever two.

    Cold arcane on everything the dark wizard took, fire on the one thing he built. On a 96px
    figure the restraint is what makes it read as something looking OUT of the mass rather than as
    a mass with lights on it.
    """
    g.set(x, y, hot)
    g.set(x + 1, y, cool)


def shadow(g, x0, x1, y):
    """The contact line. Without it a 96px figure floats, and nothing else on screen does."""
    g.span(y, x0, x1, "o")


def swell(g, y0, y1, widths, mid, lit, dark, cx=CX, wobble=0):
    """A mass whose half-width is INTERPOLATED FROM A HAND-WRITTEN PROFILE.

    The single most useful helper here, and it exists because the first pass built every body from
    a linear taper - which is a cone, and five cones is why the contact sheet read as a set of
    traffic furniture. A profile lets a silhouette have a shoulder, a waist and a flare, which is
    the difference between a bell and a triangle.
    """
    n = len(widths) - 1
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0)) * n
        i = min(n - 1, int(t))
        hw = widths[i] + (widths[i + 1] - widths[i]) * (t - i)
        if wobble:
            hw += wobble * ((y * 7 + y // 3) % 3 - 1) * 0.5
        g.span(y, cx - hw, cx + hw, mid)
        g.set(cx - hw, y, lit)
        g.set(cx - hw + 1, y, lit)
        g.set(cx + hw, y, dark)
        g.set(cx + hw - 1, y, dark)


# --- chapter 2: the Gaoler ---------------------------------------------------------------------

def gaoler():
    """A T: a low four-legged mount, and a tall figure riding it.

    The read has to survive the phase change. At half health the rider dismounts and the fight
    changes shape, so the mount and the rider are two separable masses with a hard occ seam
    between them - the rider is not a bump on the horse's back, it is an object sitting on one,
    and the player should be able to see the join before it ever comes apart.

    Nothing about the mount is equine beyond the proportion. It is four jointed legs, a barrel and
    a lowered head, which at this size is all a quadruped ever is - but the JOINTS are not
    optional. The first pass hung four straight sticks under a slab and the result was a table.
    """
    g = G(CELL)

    # LEGS FIRST, so the barrel overlaps them and the near pair reads as in front. Each is three
    # points with a knee, and the far pair is one tone darker so the two sides separate.
    limb(g, [(30, 58), (24, 74), (27, 89)], 4.0, "J", "j", "o")
    limb(g, [(62, 58), (69, 73), (66, 89)], 4.0, "J", "j", "o")
    limb(g, [(36, 60), (32, 75), (35, 90)], 4.5, "j", "N", "J")
    limb(g, [(58, 60), (63, 75), (60, 90)], 4.5, "j", "N", "J")
    for fx in (27, 35, 60, 66):
        g.span(90, fx - 5, fx + 5, "j")
        g.span(91, fx - 5, fx + 5, "J")
        g.span(92, fx - 4, fx + 4, "o")

    # BARREL: an ellipse, not a box. The whole mount hangs off this one shape reading as a body.
    for y in range(44, 70):
        t = (y - 57.0) / 13.5
        if abs(t) >= 1.0:
            continue
        hw = 31.0 * (1.0 - t * t) ** 0.5
        g.span(y, 46 - hw, 46 + hw, "j")
        g.set(46 - hw, y, "N")
        g.set(46 - hw + 1, y, "N")
        g.set(46 + hw, y, "J")
    ridges(g, 46, 68, "j", "J", "N", step=4, jitter=3)


    # BARDING: a skirt FITTED to the barrel, not a rectangle laid across it. The first pass drew
    # a solid block from hip to hip and it covered the barrel, the legs and most of the reason
    # the thing read as an animal - the sheet came out as a bench with a man on it.
    for y in range(58, 74):
        t = (y - 58) / 16.0
        hw = 28.0 - 9.0 * t * t
        g.span(y, 46 - hw, 46 + hw, "q")
        g.set(46 - hw, y, "R")
        g.set(46 + hw, y, "o")
    for sx in range(20, 73, 9):
        g.disc(sx, 73, 4.5, "q")
        g.set(sx, 77, "o")
        g.set(sx - 3, 74, "R")
    ridges(g, 59, 76, "q", "o", "R", step=4, jitter=7)
    rivets(g, [(26, 62), (38, 61), (50, 62), (62, 61), (30, 70), (44, 71), (58, 70)], "r", "q")

    # NEAR LEGS AGAIN, over the barding, so the skirt hangs BEHIND the legs on this side and the
    # mount has depth rather than being a cut-out.
    limb(g, [(36, 62), (32, 75), (35, 90)], 4.5, "j", "N", "J")
    limb(g, [(58, 62), (63, 75), (60, 90)], 4.5, "j", "N", "J")

    # NECK AND HEAD: forward, down, and CONNECTED. The first pass left the head as a loose
    # rectangle off to the side, which is the clearest single way to stop a figure being one
    # thing. It is drawn last on this side so nothing overlaps it.
    limb(g, [(24, 50), (14, 58), (9, 66)], 7.0, "j", "N", "J")
    for y in range(60, 80):
        t = (y - 60) / 20.0
        hw = 9.0 - 5.0 * t * t
        c = 10.0 - 6.0 * t
        g.span(y, c - hw, c + hw, "j")
        g.set(c - hw, y, "N")
        g.set(c + hw, y, "J")
        g.set(c + hw + 1, y, "o")
    ridges(g, 62, 78, "j", "J", "N", step=3, jitter=4)
    g.span(72, 0, 9, "J")                             # the mouth line, flat and long
    g.span(73, 0, 8, "o")
    hollow(g, 64, 68, 3.5, 2.0, cx=9.0)
    g.set(11, 65, "Z")
    spike(g, 16, 60, 5, -13, 2.5, "J", "j", "o")      # one ear, back-swept
    spike(g, 12, 61, -1, -10, 2.0, "J", "j", "o")

    # THE SEAM. A hard occlusion line across the saddle, so the rider is demonstrably a separate
    # object sitting on the mount rather than growing out of it.
    g.span(45, 34, 64, "o")
    g.span(44, 36, 62, "o")

    # THE RIDER: hunched, cloaked, and the only vertical in the figure. Wool over steel - a jailer
    # is a man in a coat with keys, not a knight.
    swell(g, 10, 44, [6.0, 9.0, 13.0, 11.0, 15.0], "b", "c", "a", cx=51.0)
    ridges(g, 20, 44, "b", "a", "c", step=5, jitter=4)
    for fold in (-8.0, -3.0, 2.0, 7.0):
        for y in range(18, 45):
            fx = 51.0 + fold * (0.6 + 0.4 * (y - 18) / 27.0)
            if g.get(fx, y) in ("b", "a"):
                g.set(fx, y, "a" if y % 3 else "c")
    roster.plate(g, 40, 18, 62, 25, "R", "r", "q")           # pauldron bar
    rivets(g, [(43, 20), (50, 20), (57, 20)], "r", "q")
    # near leg, over the barding, so he is astride rather than perched
    limb(g, [(46, 40), (40, 50), (38, 60)], 3.5, "a", "b", "o")
    g.disc(38, 62, 4.0, "q")

    # HELM: a bucket with a slot. No face at all - the chapter is about cells and the thing that
    # locks them, and a face would make it a person.
    g.rect(44, 4, 58, 18, "R")
    g.line(44, 4, 58, 4, "r")
    g.line(44, 18, 58, 18, "q")
    g.line(44, 4, 44, 18, "r")
    g.span(11, 45, 57, "o")
    g.span(12, 46, 56, "o")
    ridges(g, 5, 10, "R", "q", "r", step=2, jitter=9)
    g.span(3, 47, 55, "q")

    # THE KEYS: the one oversized signature attachment, on the off hand where it swings. Gold is
    # the only warm row in the palette and this is a dozen pixels of it, which is the whole budget
    # a figure this dark can carry.
    limb(g, [(60, 28), (72, 36), (78, 44)], 3.0, "b", "c", "a")
    g.disc(80, 50, 6.0, "u")
    g.disc(80, 50, 4.0, "g")
    g.disc(80, 50, 2.5, "o")
    for (kx, ky, kl) in ((74, 56, 4), (80, 59, 5), (86, 55, 3)):
        g.span(ky, kx, kx + kl, "g")
        g.span(ky + 1, kx + kl - 1, kx + kl, "H")
        g.set(kx, ky + 1, "u")
        g.set(kx + kl, ky + 2, "u")

    mark(g, 49, 11, "Z", "Y")
    shadow(g, 4, 90, 93)
    return g.rows()


# --- chapter 3: the Hollow Choir ---------------------------------------------------------------

def hollow_choir():
    """A bell. Narrow, headless, limbless - the least body of anything in the game.

    Three of these stand on the field at once, so it is the one boss that has to work MULTIPLIED.
    Everything that would have made a single figure more interesting - arms, a stance, an angle -
    was cut for that reason: three copies of an asymmetric pose read as a crowd of different
    things, and three copies of a narrow vertical read as a choir. The fight is about the set, not
    the member.

    It is a BELL and not a cone, which took a hand-written profile to get: a crown, a shoulder, a
    waist, and then the flare. A linear taper is a traffic cone and that is exactly what the first
    pass produced.

    What carries the idea is the column of mouths down the front - it is called a Choir and it has
    no head, so the mouths have to be somewhere. They are offset left and right down the line
    rather than stacked, because stacked they were a ladder.
    """
    g = G(CELL)

    swell(g, 8, 88, [6.0, 11.0, 13.5, 10.5, 12.0, 17.0, 26.0, 29.0], "E", "e", "A", wobble=1)

    # FOLDS: six columns carried the whole height, wandering. They are most of the density and all
    # of the reason the cloth reads as cloth.
    for i, off in enumerate((-19.0, -12.0, -6.0, 1.0, 8.0, 15.0, 21.0)):
        for y in range(14, 88):
            t = (y - 14) / 74.0
            fx = CX + off * (0.35 + 0.65 * t) + (1 if (y + i * 5) % 9 < 4 else 0)
            if g.get(fx, y) in ("E", "e", "A"):
                g.set(fx, y, "A" if (y + i) % 4 else "e")

    # THE COWL IS EMPTY. A hood with nothing in it is the whole design, and it has to be a real
    # hole through the mass rather than a dark patch on it.
    g.span(7, 42, 53, "e")
    hollow(g, 11, 30, 7.5, 4.0)
    g.span(10, 40, 55, "A")
    g.set(39, 12, "e")
    g.set(56, 12, "A")

    # THE MOUTHS: six slots, alternating side to side, no two the same width, each with bone teeth
    # above and below. Stacked and equal they were a rung ladder; offset and uneven they are a row
    # of things that could sing.
    for (my, mx, mw) in ((36, -6, 5), (44, 5, 7), (52, -8, 4),
                         (61, 6, 8), (71, -5, 6), (80, 8, 5)):
        cx = CX + mx
        g.span(my, cx - mw, cx + mw, "o")
        g.span(my + 1, cx - mw + 1, cx + mw - 1, "o")
        g.span(my + 2, cx - mw + 2, cx + mw - 2, "o")
        # Teeth, not a lip. A full-width bone bar above each slot turned six mouths into six
        # shelves, which is the second time a stack of horizontals has read as furniture here.
        for tx in range(int(cx - mw) + 1, int(cx + mw), 3):
            g.set(tx, my, "n")
            g.set(tx + 1, my + 2, "m")
        for k in (-1, 0, 1):
            g.set(cx - mw + k, my + 1, "m")
            g.set(cx + mw - k, my + 1, "m")
        g.set(cx - mw - 1, my, "n")
        g.set(cx + mw + 1, my + 1, "A")

    # HEM: torn, and torn irregularly. An even hem is a staircase.
    roster.tatters(g, [(89, 20, 31), (90, 22, 30), (89, 35, 45), (91, 36, 43),
                       (89, 49, 62), (91, 51, 60), (90, 66, 74), (89, 68, 73)],
                   "E", "e", "A")

    # Cave water has soaked the lower cloth on one side only. Applied along the folds rather than
    # as a field, so it darkens the garment instead of printing a texture on it.
    for y in range(56, 90):
        for x in range(int(CX) - 26, int(CX) + 4):
            if g.get(x, y) == "A" and (x * 5 + y * 3 + (y // 4)) % 9 < 2:
                g.set(x, y, "/")

    mark(g, 46, 20, "Z", "Y")
    shadow(g, 18, 78, 92)
    return g.rows()


# --- chapter 4: Mother Rot ---------------------------------------------------------------------

def mother_rot():
    """A dome. The only boss wider than it is tall, and the only one with no vertical at all.

    Her mechanic is that she sheds, so the silhouette is drawn already coming apart: the lower
    edge is not an outline but a row of sacs at different stages of dropping off, and two of them
    are already detached and sitting on the ground beside her. A boss that splits should look,
    standing still, like something that is about to.

    She is skin and flesh from the DEEP half - bloated rather than rotten. The world doc is
    explicit that this fiction is cold and occupied rather than gory, so nothing here is bleeding;
    it is swollen, which is worse and costs no red.
    """
    g = G(CELL)

    # THE MASS: broad, low, asymmetric. A symmetric dome is a hill.
    for y in range(32, 84):
        t = (y - 32) / 52.0
        hw = 18.0 + 24.0 * (1.0 - (1.0 - t) ** 2.2)
        c = CX + 3.0 * (1.0 - t)
        g.span(y, c - hw, c + hw, "m")
        g.set(c - hw, y, "n")
        g.set(c - hw + 1, y, "n")
        g.set(c - hw + 2, y, "n")
        g.set(c + hw, y, "o")
        g.set(c + hw - 1, y, "m")

    # VEINS: many short bowed strokes rather than long threads. The first pass drew full-length
    # ones and they read as a string bag thrown over her.
    strokes = []
    for i in range(34):
        vx = 10 + (i * 23) % 70
        vy = 38 + (i * 17) % 42
        strokes.append((vx, vy, 4 + (i % 4), 1.0 if i % 2 else -1.0))
    veins(g, strokes, "I")
    veins(g, [(s[0] + 1, s[1] + 1, s[2], -s[3]) for s in strokes[::2]], "i")

    # A SLACK CREASE where the mass folds over itself. One, not a set - two would be a stack of
    # tyres, and the whole point is that she is one bag rather than several.
    for x in range(14, 80):
        y = 66 + int(3.0 * ((x - 47) / 33.0) ** 2)
        if g.get(x, y) != ".":
            g.span(y, x, x, "o")
            g.set(x, y - 1, "n")

    # THE SACS: five along the underside at three sizes, plus two already off her. The detached
    # pair is the silhouette idea and they sit low and outboard so they read against the ground.
    for (sx, sy, sr) in ((14, 78, 7.0), (28, 85, 9.0), (45, 88, 7.5),
                         (61, 85, 8.5), (75, 79, 6.5)):
        g.disc(sx, sy, sr + 1, "o")
        g.disc(sx, sy, sr, "m")
        g.disc(sx - 1.5, sy - 1.5, sr * 0.55, "n")
        g.disc(sx - 2, sy - 2, sr * 0.2, "s")
        veins(g, [(sx - 3, sy + 2, 6, 1.0)], "I")
    for (sx, sy, sr) in ((5, 88, 4.5), (89, 86, 5.0)):
        g.disc(sx, sy, sr + 1, "o")
        g.disc(sx, sy, sr, "m")
        g.disc(sx, sy - 1, sr * 0.5, ")")
        g.set(sx, sy - 2, "(")

    # SPORE BLOOM: poison, and only on the crown, where a thing that throws spores throws them
    # from. Three stops of the ramp so it has depth rather than being a sticker.
    for (bx, by, br) in ((33, 35, 5.0), (50, 30, 7.0), (65, 37, 4.5), (23, 44, 3.5)):
        g.disc(bx, by, br + 1, "o")
        g.disc(bx, by, br, "_")
        g.disc(bx - 1, by - 1, br * 0.55, ")")
        g.set(bx - 1, by - int(br) + 1, "(")
        for k in range(-1, 2):
            g.set(bx + k * 2, by + br, "o")

    # THE HEAD is sunk into the mass and barely there. She is a body first, and a big face would
    # make her a monster with a body rather than a body that happens to be looking at you.
    g.span(45, 36, 56, "n")
    g.span(44, 38, 54, "s")
    hollow(g, 46, 55, 7.0, 3.5, cx=46.0)
    g.set(38, 46, "m")
    g.set(55, 46, "o")

    # LIMBS: two stubs, not arms. Something this shape does not have arms; it has parts of itself
    # that reach further than the rest.
    limb(g, [(19, 58), (9, 64), (2, 72)], 5.0, "m", "n", "o")
    limb(g, [(76, 55), (86, 60), (93, 68)], 4.5, "m", "n", "o")

    mark(g, 44, 50, "Z", "Y")
    shadow(g, 2, 94, 92)
    return g.rows()


# --- chapter 5: the Archivist ------------------------------------------------------------------

def archivist():
    """A slab, and two tablets floating beside it that are NOT attached.

    The detachment is the entire silhouette. Every other figure in this game is one connected
    mass, so a figure with pieces of itself hanging in the air a dozen pixels off the body is
    instantly the odd one out - which is exactly right for the boss that does not walk, does not
    chase, and is not in any ordinary sense an animal.

    It is masonry, and masonry is LAID: staggered courses, a broken crown, and a crack running
    most of its height. The first pass banded it evenly the whole way down and it read as a
    bookcase, which is what evenly spaced horizontals always read as.
    """
    g = G(CELL)

    # THE SLAB: widest at the top. Bottom-heavy would read as a monument; top-heavy reads as a
    # thing leaning over you.
    swell(g, 10, 84, [16.0, 15.0, 13.5, 12.0, 10.5, 9.5], "j", "N", "J")
    courses(g, 12, 84, "j", "J", "P", high=8, wide=11)

    # A BROKEN CROWN. A clean top edge is a plinth; a chewed one is a ruin, and this chapter is
    # called the Mystic Ruins.
    for (bx, bw, bd) in ((33, 5, 4), (41, 7, 2), (50, 4, 6), (56, 6, 3), (61, 3, 5)):
        for k in range(bd):
            g.span(10 + k, bx, bx + bw, ".")
    g.span(9, 32, 63, ".")
    for x in range(32, 64):
        for y in range(9, 20):
            if g.get(x, y) == "j" and g.get(x, y - 1) == ".":
                g.set(x, y, "P")

    # THE CRACK: one, running two thirds of the height, branching once. Drawn in occ so it is a
    # gap rather than a line.
    for (x0, y0, x1, y1) in ((52, 22, 49, 46), (49, 46, 53, 68), (49, 46, 43, 60)):
        g.line(x0, y0, x1, y1, "o")
        g.line(x0 + 1, y0, x1 + 1, y1, "J")

    # THE SLOT: one horizontal aperture, most of the width of the slab. Not a face - a boss made of
    # masonry should look like it is reading you rather than looking at you.
    g.span(25, 34, 61, "o")
    g.span(26, 33, 62, "o")
    g.span(27, 34, 61, "o")
    g.span(24, 33, 62, "P")
    g.span(23, 34, 61, "p")
    g.span(28, 35, 60, "J")

    # THE TABLETS: two, different sizes, at different heights, TILTED, with a clear gap of empty
    # cell between each and the slab. Same size at the same height would read as ears; the tilt is
    # what says they are held there rather than built there.
    for (tx, ty, tw, th, lean) in ((14, 32, 9, 15, 0.35), (78, 24, 8, 13, -0.30)):
        for i in range(th * 2):
            y = ty + i
            off = lean * (i - th)
            g.span(y, tx - tw + off, tx + tw + off, "j")
            g.set(tx - tw + off, y, "N")
            g.set(tx + tw + off, y, "J")
        for ry in range(ty + 3, ty + th * 2 - 2, 4):
            off = lean * (ry - ty - th)
            g.span(ry, tx - tw + 2 + off, tx + tw - 2 + off, "J")
            g.span(ry - 1, tx - tw + 3 + off, tx + tw - 4 + off, "P")
        for ry in range(ty + 4, ty + th * 2 - 3, 4):
            off = lean * (ry - ty - th)
            for rx in range(int(tx - tw + 3 + off), int(tx + tw - 3 + off), 3):
                if (rx + ry) % 5 < 2:
                    g.set(rx, ry, "z")
                    g.set(rx, ry + 1, "o")

    # IT DOES NOT STAND. The slab stops short of the floor and the shadow is drawn under the gap,
    # which is the cheapest way to say "hovering" and the only one that works at 96px.
    for x in range(38, 58):
        if g.get(x, 84) != ".":
            g.set(x, 84, "o")
    g.disc(CX, 92, 14.0, "o")
    g.disc(CX, 92, 9.0, "J")

    mark(g, 46, 26, "y", "z")
    return g.rows()


# --- chapter 6: the Still Warden ---------------------------------------------------------------

def still_warden():
    """A wedge: frozen into the floor at the bottom, armoured at the top, no legs at all.

    It never moves in the fight and it must never look as though it could. There is no stance and
    no gait - the body simply becomes ice below the waist and the ice becomes the ground. The
    widest part of the silhouette is at the very bottom, which is the opposite of every other
    figure here and is what makes it read as rooted rather than as standing.

    THE ICE IS DARK. The first pass filled the spur with the ice ramp's brighter stops and the
    result was a saturated blue triangle - the single most brightly lit thing in a game whose
    contract says nothing the dark wizard holds may be brightly lit. It is stone now, with ice
    only on the facet edges, which is also how ice actually reads: the plane is dark and the
    boundary catches the light.
    """
    g = G(CELL)

    # THE SPUR: a broad base of frozen rubble running off the bottom of the cell.
    # Stone BASE, not stone deep. Twice now a mass drawn from the bottom of a ramp has come out
    # as a hole in the screen - sprite_treant.py records the same correction. Deep is the
    # direction; invisible is not.
    swell(g, 52, 94, [13.0, 20.0, 28.0, 34.0, 39.0], "N", "p", "J")
    # Filled in VISIBLE stone with one lit edge each. The first pass filled them with the two
    # darkest stops and edged them in bright ice, so all that showed was a handful of pale
    # diagonals floating over black - it read as scribble, not as crystal.
    facets(g, [(31, 60, 15, 18, "j"), (60, 64, 17, 22, "N"), (20, 76, 19, 15, "j"),
               (68, 78, 16, 13, "N"), (46, 71, 13, 22, "j"), (11, 87, 14, 7, "N")],
           "p", "J")
    ridges(g, 56, 92, "N", "j", "P", step=5, jitter=3)
    # Rime along the top edge of each plane only, where light would actually catch it.
    for x in range(8, 88):
        for y in range(53, 93):
            if g.get(x, y) in ("j", "N") and g.get(x, y - 1) == ".":
                g.set(x, y, "?")

    # THE TORSO: steel plate, deep, set into the ice. Much narrower than the shoulders, so the
    # figure pinches at the waist and the wedge reads twice over.
    swell(g, 24, 58, [17.0, 15.0, 12.5, 13.0, 15.0], "q", "R", "o")
    ridges(g, 28, 58, "q", "o", "R", step=4, jitter=6)
    rivets(g, [(38, 32), (48, 31), (57, 32), (36, 44), (47, 43), (58, 44)], "r", "q")

    # PAULDRONS, pushed out past the torso so the shoulder line is the second widest thing in the
    # figure after the ice - and hung with frost rather than crowned with it.
    roster.plate(g, 18, 24, 37, 35, "R", "r", "q")
    roster.plate(g, 58, 24, 77, 35, "R", "r", "q")
    ridges(g, 25, 35, "R", "q", "r", step=3, jitter=5)
    for (px, py, ph) in ((22, 24, 7), (29, 23, 10), (35, 25, 6),
                         (61, 25, 6), (67, 23, 10), (74, 24, 7)):
        spike(g, px, py, 0, -ph, 2.5, "N", "?", "J")

    # ARMS CROSSED, with gauntlets. Not holding anything - the guards do the reaching, and a weapon
    # here would promise a swing the fight never delivers.
    limb(g, [(26, 38), (40, 46), (60, 49)], 4.5, "R", "r", "o")
    limb(g, [(70, 38), (56, 44), (36, 51)], 4.5, "q", "R", "o")
    ridges(g, 38, 52, "R", "q", "r", step=3, jitter=4)
    g.disc(62, 50, 5.5, "R")
    g.disc(60, 48, 2.5, "r")
    g.disc(34, 52, 5.5, "R")
    g.disc(32, 50, 2.5, "r")

    # HELM: a visor slot and a crest of ice. No face; the chapter's idea is a guard that has been
    # standing there long enough to have frozen over.
    g.rect(38, 8, 57, 26, "R")
    g.line(38, 8, 57, 8, "r")
    g.line(38, 26, 57, 26, "q")
    g.line(38, 8, 38, 26, "r")
    ridges(g, 9, 15, "R", "q", "r", step=2, jitter=9)
    g.span(19, 40, 56, "o")
    g.span(20, 39, 57, "o")
    g.span(18, 40, 56, "q")
    spike(g, 47, 8, 0, -8, 3.0, "N", "?", "J")
    spike(g, 41, 9, -5, -6, 2.0, "j", "?", "J")
    spike(g, 54, 9, 5, -6, 2.0, "j", "?", "J")

    mark(g, 46, 19, "<", ">")
    shadow(g, 4, 92, 94)
    return g.rows()


# --- chapter 7: the Long Coil ------------------------------------------------------------------

def long_coil():
    """An S: a rearing curve with the mass at the top and nothing holding it up.

    Every other boss meets the floor across a wide contact. This one meets it at a single point,
    through a hole, and everything above that point leans. Read cold, the outline says "the rest
    of this is underground" - which is the fight, because the sprite is a fraction of the creature
    and the fraction changes.

    IT HAD TO BE MADE LIGHTER. The first pass built the neck out of the two darkest stone stops
    and it came out as a black sock: no interior, no curve, and no way to tell the head from the
    body. The neck is stone BASE now with the deep stops used only for the shading edge, which is
    the same correction sprite_treant.py records making for exactly the same reason.
    """
    g = G(CELL)

    # THE HOLE: sand pushed up in a ring, drawn first so the neck sits in it rather than on it.
    for y in range(78, 94):
        t = (y - 78) / 16.0
        hw = 10.0 + 26.0 * t
        g.span(y, 40 - hw, 40 + hw, "u")
        g.set(40 - hw, y, "g")
        g.set(40 - hw + 1, y, "g")
        g.set(40 + hw, y, "o")
    for i in range(22):
        sx = 8 + (i * 13) % 64
        sy = 82 + (i * 7) % 10
        if g.get(sx, sy) == "u":
            g.span(sy, sx, sx + 2, "H" if i % 3 else "g")
    hollow(g, 78, 86, 10.0, 5.0, cx=40.0)

    # THE NECK: a swept path, sampled as discs. Thicker at the bottom, and lit down its left.
    path = []
    for i in range(52):
        t = i / 51.0
        px = 40.0 + 18.0 * (t ** 1.5) - 5.0 * (t ** 3)
        py = 84.0 - 52.0 * t
        path.append((px, py, 13.5 - 4.0 * t))
    # The neck is one stop DARKER than the skull. That contrast is the only thing that separates
    # them, and without it the two merge into a single ramp - the first two passes both came out
    # as a boot for exactly this reason.
    for (px, py, pr) in path:
        g.disc(px, py, pr, "j")
        g.disc(px - pr * 0.35, py, pr * 0.50, "N")
        g.set(px + pr - 1, py, "J")
        g.set(px + pr, py, "o")
    scales(g, path, "N", "p", every=3)

    # BELLY PLATES on the leading edge: wide horizontal bands, which is the one part of a serpent
    # that is not scaled and the fastest way to say which side is the front.
    for i, (px, py, pr) in enumerate(path):
        if i % 4 or i > 44:
            continue
        g.span(py, px - pr, px - pr * 0.35, "p")
        g.span(py + 1, px - pr + 1, px - pr * 0.4, "J")

    # THE HEAD: drawn ACROSS the cell rather than down it, because a serpent skull is a
    # horizontal object and the first two passes drew it as a vertical one - which is why they
    # both came out continuous with the neck instead of sitting on the end of it.
    #
    # Snout at the left, the mass at the back where it meets the neck, and a real gap of
    # occlusion under it before the jaw starts.
    for x in range(24, 74):
        t = (x - 24) / 50.0
        top = 25.0 - 13.0 * (t ** 0.6)
        bot = 32.0 + 3.0 * (t ** 1.4)
        for y in range(int(top), int(bot) + 1):
            g.set(x, y, "N")
        g.set(x, top, "p")
        g.set(x, top + 1, "p")
        g.set(x, bot, "J")
    ridges(g, 14, 34, "N", "j", "P", step=4, jitter=3)
    for x in range(26, 72, 5):                   # plate joints across the skull
        for y in range(12, 36):
            if g.get(x, y) == "N":
                g.set(x, y, "j")
            elif g.get(x, y) == "j":
                g.set(x, y, "P")

    # BROW: overhanging, and the only bright line on the figure. It is what makes the head read
    # as looking rather than as pointing.
    for x in range(26, 72):
        t = (x - 26) / 46.0
        y = int(25.0 - 13.0 * (t ** 0.6))
        g.set(x, y - 1, "P")
        g.set(x, y, "p")
        g.set(x, y + 1, "o")

    # THE JAW, hinged at the back and dropped away from the skull.
    for x in range(26, 68):
        t = (x - 26) / 42.0
        top = 36.0 - 2.0 * t
        bot = 42.0 - 5.0 * t * t
        for y in range(int(top), int(bot) + 1):
            g.set(x, y, "j")
        g.set(x, top, "N")
        g.set(x, bot, "o")
    for x in range(26, 68):
        t = (x - 26) / 42.0
        g.set(x, int(34.0 - 1.0 * t), "o")
        g.set(x, int(35.0 - 1.0 * t), "o")
    for tx in range(30, 66, 5):                  # teeth, top row and bottom row
        g.set(tx, 33, "s")
        g.set(tx, 34, "n")
        g.set(tx + 2, 36, "s")
        g.set(tx + 2, 37, "n")

    # HORN: one, swept back. One is a feature; two is a bull.
    spike(g, 68, 16, 17, -12, 4.5, "u", "g", "o")
    g.set(83, 5, "H")

    mark(g, 52, 22, "Z", "Y")
    shadow(g, 4, 88, 94)
    return g.rows()


# --- chapter 8: the Warden of the Deep ---------------------------------------------------------

def deep_warden():
    """An H: two heavy legs, a bridge of shoulders, and horns. The only bipedal boss in the set.

    It is last, so it is the one allowed to be the obvious monster. Everything else here was
    designed to avoid the shape a player expects a boss to be; this is that shape, on purpose,
    because after seven fights that each refused it, the familiar outline is the surprise.

    IT IS THE ONLY ONE THAT BURNS. Six bosses carry the cold arcane mark because they were taken.
    This one was built, in his own seat, and the difference is stated in the one place a player
    will register it: the light coming out of it is fire, and there is a great deal more than two
    pixels of it - the two-pixel rule is a rule about things that are occupied, not about things
    that are his.
    """
    g = G(CELL)

    # LEGS: two columns, wide apart, planted, with knees. The gap between them is a third of the
    # cell and it is the clearest single thing in the silhouette.
    for (lx, kx) in ((28, 24), (66, 70)):
        limb(g, [(lx, 60), (kx, 76), (lx, 90)], 8.5, "q", "R", "o")
        g.span(90, lx - 12, lx + 12, "q")
        g.span(91, lx - 12, lx + 12, "R")
        g.span(92, lx - 11, lx + 11, "o")
        ridges(g, 62, 90, "q", "o", "R", step=4, jitter=5)
        rivets(g, [(lx - 6, 66), (lx + 5, 68), (lx - 5, 80), (lx + 6, 82)], "r", "q")

    # TORSO: a wedge from the hips up to the shoulders.
    swell(g, 32, 66, [25.0, 22.0, 19.0, 17.0, 16.0], "q", "R", "o")
    ridges(g, 34, 66, "q", "o", "R", step=5, jitter=3)

    # THE CRACKS: fire through the plate, branching, all running from the chest outward. Written
    # out as a fixed set rather than generated, because a generated crack field is a craquelure
    # and a craquelure reads as a broken pot.
    for (x0, y0, x1, y1) in ((47, 40, 34, 52), (47, 40, 60, 50), (47, 40, 47, 64),
                             (34, 52, 28, 62), (60, 50, 66, 60), (47, 56, 39, 66),
                             (47, 56, 56, 67), (28, 62, 24, 70), (66, 60, 71, 69)):
        g.line(x0, y0, x1, y1, "=")
        g.line(x0 + 1, y0, x1 + 1, y1, "~")
        g.line(x0 - 1, y0 + 1, x1 - 1, y1 + 1, "o")
    g.disc(47, 40, 6.0, "~")
    g.disc(47, 40, 4.0, "=")
    g.disc(47, 40, 2.5, "-")
    g.set(47, 40, "+")

    # SHOULDERS: the bridge of the H, spanning nearly the whole cell, spiked on both.
    roster.plate(g, 4, 26, 32, 42, "R", "r", "q")
    roster.plate(g, 63, 26, 91, 42, "R", "r", "q")
    ridges(g, 28, 42, "R", "q", "r", step=3, jitter=4)
    rivets(g, [(8, 30), (16, 29), (24, 30), (68, 30), (76, 29), (84, 30),
               (10, 38), (22, 38), (72, 38), (84, 38)], "r", "q")
    for (px, py, ph) in ((8, 26, 11), (16, 25, 14), (24, 26, 10),
                         (70, 26, 10), (78, 25, 14), (86, 26, 11)):
        spike(g, px, py, 0, -ph, 3.0, "q", "R", "o")

    # ARMS: heavy, hanging, ending in fists at knee height. They are what makes the H read as a
    # thing rather than as a diagram.
    limb(g, [(14, 42), (9, 58), (13, 70)], 7.0, "q", "R", "o")
    limb(g, [(81, 42), (86, 56), (83, 68)], 7.0, "q", "R", "o")
    ridges(g, 44, 70, "q", "o", "R", step=4, jitter=7)
    g.disc(13, 75, 8.5, "q")
    g.disc(83, 73, 8.5, "q")
    g.disc(10, 72, 4.0, "R")
    g.disc(80, 70, 4.0, "R")
    g.set(13, 83, "o")
    g.set(83, 81, "o")

    # HEAD: small, sunk between the shoulders, horned. Small is the point - the shoulders are the
    # boss, and a big head would compete with them.
    g.rect(39, 10, 57, 30, "R")
    g.line(39, 10, 57, 10, "r")
    g.line(39, 30, 57, 30, "q")
    g.line(39, 10, 39, 30, "r")
    ridges(g, 11, 18, "R", "q", "r", step=2, jitter=9)
    spike(g, 40, 11, -13, -10, 4.0, "q", "R", "o")
    spike(g, 56, 11, 13, -10, 4.0, "q", "R", "o")

    # The visor is a furnace, not a pair of eyes. The last boss is the only thing in the game lit
    # from inside, and that is the point being made.
    g.span(22, 41, 56, "o")
    g.span(23, 40, 57, "o")
    g.span(22, 43, 54, "~")
    g.span(22, 45, 51, "=")
    g.set(47, 22, "-")
    g.set(48, 22, "+")
    g.span(24, 44, 53, "~")

    shadow(g, 8, 90, 94)
    return g.rows()


# --- the Still Warden's guard ------------------------------------------------------------------

def rime_guard():
    """48x48, the elite cell. A halberd guard that only strikes the wedge in front of it.

    ITS WHOLE JOB IS TO HAVE A FRONT. The enemy behind it takes nothing, so the sprite has to make
    which way it faces unmistakable from across a crowded screen: the shield is a flat plane on
    one side, the halberd rakes forward over it, the helm is cut away on that side, and there is
    nothing at all on the back half. A symmetric guard would be a lie about its own hitbox.
    """
    g = G(GUARD_CELL)
    gcx = 27.0

    # BODY: narrow, hunched forward over the shield arm, with a waist.
    swell(g, 12, 40, [5.0, 8.0, 6.5, 9.0], "q", "R", "o", cx=gcx)
    ridges(g, 14, 40, "q", "o", "R", step=3, jitter=4)
    rivets(g, [(24, 20), (30, 21), (25, 30), (31, 31)], "r", "q")

    # HELM with a forward-cut visor, tilted the way it faces.
    g.rect(21, 4, 33, 15, "R")
    g.line(21, 4, 33, 4, "r")
    g.line(21, 15, 33, 15, "q")
    g.line(21, 4, 21, 15, "r")
    g.span(10, 20, 29, "o")
    g.span(11, 19, 28, "o")
    spike(g, 27, 4, -3, -6, 2.0, "J", "?", "j")

    # LEGS, planted and squared: a guard braces, it does not stride.
    limb(g, [(23, 38), (21, 43), (22, 45)], 3.0, "q", "R", "o")
    limb(g, [(32, 38), (34, 43), (33, 45)], 3.0, "q", "R", "o")
    g.span(46, 18, 26, "q")
    g.span(46, 29, 37, "q")

    # THE SHIELD: a plane, on the facing side only, and oversized. It is the read.
    g.rect(5, 13, 17, 41, "q")
    g.line(5, 13, 17, 13, "r")
    g.line(5, 13, 5, 41, "R")
    g.line(5, 41, 17, 41, "o")
    g.line(17, 13, 17, 41, "o")
    ridges(g, 15, 40, "q", "o", "R", step=4, jitter=3)
    rivets(g, [(7, 16), (14, 16), (7, 37), (14, 37)], "r", "q")
    g.disc(11, 27, 5.0, "J")
    g.disc(11, 27, 3.0, "?")
    g.disc(11, 27, 1.5, "<")

    # THE HALBERD, raked forward over the shield so the threat and the facing are one line.
    limb(g, [(38, 45), (30, 22), (25, 3)], 1.5, "q", "R", "o")
    spike(g, 24, 5, -9, -4, 2.5, "J", "?", "j")
    g.span(8, 17, 26, "j")
    g.span(7, 18, 25, "?")
    g.set(17, 9, "<")

    mark(g, 24, 10, "<", ">")
    g.span(47, 12, 38, "o")
    return g.rows()


BOSSES = [
    ("gaoler", gaoler),
    ("hollow-choir", hollow_choir),
    ("mother-rot", mother_rot),
    ("archivist", archivist),
    ("still-warden", still_warden),
    ("long-coil", long_coil),
    ("deep-warden", deep_warden),
]


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_bosses")
    if not os.path.isdir(out):
        os.makedirs(out)

    sparse = []
    for name, fn in BOSSES + [("rime-guard", rime_guard)]:
        cell = GUARD_CELL if name == "rime-guard" else CELL
        pose = fn()
        filled, edges, ratio = roster.density(pose)
        P.write_strip([pose], PALETTE, os.path.join(out, "%s-pose.png" % name), cell)
        flag = "" if ratio >= 0.80 else "   SPARSE"
        if flag:
            sparse.append(name)
        print("%-14s %dx%d  %5d px  %5d edges  %.2f edges/px%s"
              % (name, cell, cell, filled, edges, ratio, flag))

    print("wrote %s" % out)
    if sparse:
        print("below the 0.80 density floor: %s" % ", ".join(sparse))


if __name__ == "__main__":
    main()
