# -*- coding: utf-8 -*-
"""The rest of the spell icons. Run: python tools/art/spell_icons_core.py

`spell_icons.py` holds the icons drawn alongside brand-new spells. This file is the back-fill: the
eighteen spells still wearing icons out of a bought pack, which is the largest single job on the
kill list in .ai/art-replacement-manifest.md.

Split into its own module rather than appended to the other one because they are different jobs with
different lifetimes - that file grows by one function whenever a spell is added, this one shrinks to
nothing and gets deleted when the back-fill is done.

THE RULE THAT SEPARATES THESE FROM THE BOON ICONS. A spell icon is an EFFECT: emissive, drawn on the
element ramps, white-hot at the core with the identity in the mid and edge (art-direction.md section
3). A boon icon is an OBJECT the player is carrying, drawn on the material rows and lit from the
upper left. Keeping those two apart is most of what tells a player, at a glance, which kind of card
they are looking at - so nothing here uses a material tone except where a spell genuinely contains
a made thing, like Aegis Ward's shield or Molten Shard's splinter of iron.

Authored 32x32, rendered x2, per the UI cell in section 1.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
TAU = math.pi * 2.0
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# WHERE THESE GO, and why it was moved.
#
# They used to be written into assets/organized/ui/ under a "ui-derived-spell-icon-" prefix, which
# was true once - the first ones really were cut down from a bought GUI pack - and has not been
# true since they were all redrawn. The cost of leaving them there was not tidiness: that
# directory is the bought art, all of it is licensed for use and not for redistribution, and the
# removal that has to happen before this repo can be public is `rm -r assets/organized`. Twenty-six
# pieces of our own art were sitting in the blast radius. See .ai/asset-licensing.md.
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "spells")

CELL = 32


def E(name):
    """core, hot, mid, edge for an element."""
    return bl.ELEMENTS[name]


def burst(c, cx, cy, r, ramp, core_frac=0.28):
    """A radial emissive blob: edge outward, core in the middle."""
    core, hot, mid, edge = ramp
    c.disc(cx, cy, r, r, edge)
    c.disc(cx, cy, r * 0.76, r * 0.76, mid)
    c.disc(cx, cy, r * 0.5, r * 0.5, hot)
    c.disc(cx, cy, r * core_frac, r * core_frac, core)


def spray(c, pts, ramp, bright=1):
    """Loose motes. `bright` picks how far up the ramp they sit."""
    core, hot, mid, edge = ramp
    tone = [edge, mid, hot, core][max(0, min(3, bright))]
    for (x, y) in pts:
        c.set(x, y, tone)


def bolt(c, pts, ramp, width=2):
    """A jagged stroke with a bright core down the middle."""
    core, hot, mid, edge = ramp
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        if width >= 3:
            c.line(x0, y0 - 1, x1, y1 - 1, edge)
            c.line(x0, y0 + 1, x1, y1 + 1, edge)
        if width >= 2:
            c.line(x0 + 1, y0, x1 + 1, y1, mid)
            c.line(x0 - 1, y0, x1 - 1, y1, mid)
        c.line(x0, y0, x1, y1, hot)
    for (x, y) in pts:
        c.set(x, y, core)


def nested(c, outline, ramp, steps=(1.0, 0.72, 0.46, 0.22), cy=16):
    """Concentric insets of one outline toward a horizontal spine - a shape made of light."""
    core, hot, mid, edge = ramp
    tones = [edge, mid, hot, core]
    for k, tone in zip(steps, tones):
        c.poly([(x, cy + (y - cy) * k) for (x, y) in outline], tone)


# =============================================================================
# The eighteen
# =============================================================================

def magic_missile():
    """A dart of raw arcane force with a spent trail. The roster's honest baseline."""
    c = raster.Canvas(CELL, CELL, None)
    r = E("arcane")
    nested(c, [(10, 16), (20, 9), (29, 16), (20, 23), (10, 16)], r)
    for i, x in enumerate(range(2, 11, 2)):
        c.set(x, 16, r[3] if i < 2 else r[2])
    spray(c, [(24, 5), (27, 24), (16, 4)], r, 0)
    return c


def fireball():
    """A heavy burning sphere. Round and massive, where Cinderbreath is a thrown jet."""
    c = raster.Canvas(CELL, CELL, None)
    r = E("fire")
    burst(c, 17, 17, 11.5, r)
    # Flame licking off the top-left, the direction it is travelling from.
    for (x, y) in [(8, 7), (6, 10), (10, 5), (12, 8)]:
        c.set(x, y, r[2])
    spray(c, [(4, 6), (29, 26), (27, 6), (5, 27)], r, 0)
    return c


def frost_shard():
    """Shards flying outward from nothing - the spell fires when something DIES, not when cast."""
    c = raster.Canvas(CELL, CELL, None)
    r = E("ice")
    for deg in (20, 75, 140, 200, 255, 310):
        a = math.radians(deg)
        x0, y0 = 16 + math.cos(a) * 4, 16 + math.sin(a) * 4
        x1, y1 = 16 + math.cos(a) * 13, 16 + math.sin(a) * 13
        c.line(x0, y0, x1, y1, r[2])
        c.line(x0, y0, (x0 + x1) / 2.0, (y0 + y1) / 2.0, r[1])
        c.set(x1, y1, r[3])
    c.disc(16, 16, 3.2, 3.2, r[1])
    c.disc(16, 16, 1.6, 1.6, r[0])
    return c


def chain_lightning():
    """One heavy fork. Damage, not spread - Shadow Bolt is the one that goes everywhere."""
    c = raster.Canvas(CELL, CELL, None)
    r = E("lightning")
    bolt(c, [(8, 3), (14, 11), (10, 15), (18, 22), (14, 26), (22, 29)], r, width=2)
    bolt(c, [(14, 11), (22, 9), (26, 14)], r, width=1)
    spray(c, [(4, 8), (28, 22)], r, 1)
    return c


def shadow_bolt():
    """A hollow curse-mark, not a dart.

    The first pass drew this the same way as Magic Missile - a nested lens pointing right - and at
    32 pixels the two were one icon in two colours, which is the exact failure this whole back-fill
    exists to fix. Two spells may share a delivery archetype; they may not share a silhouette.

    So it is a ring rather than a projectile, with nothing inside it. Darkness in this game is
    absence, and a shape whose middle is the darkest thing on the card reads as a hole punched in
    the world. The rot creeping off it is what it actually does - it spreads debuffs, and almost no
    damage.
    """
    c = raster.Canvas(CELL, CELL, None)
    dark = E("darkness")
    rot = E("poison")

    c.disc(16, 16, 11.0, 11.0, dark[3])
    c.disc(16, 16, 8.6, 8.6, dark[2])
    c.disc(16, 16, 6.6, 6.6, OCC)
    # A broken bright rim, so the ring reads as charged rather than drawn.
    for deg in range(0, 360, 14):
        a = math.radians(deg)
        if (deg // 14) % 3 == 0:
            continue
        c.set(16 + math.cos(a) * 9.8, 16 + math.sin(a) * 9.8, dark[1])

    # Rot running off the underside - the payload, and the only warm-ish colour in the icon.
    for (x, y) in [(10, 25), (13, 28), (20, 26), (23, 29), (6, 21)]:
        c.set(x, y, rot[2])
    for (x, y) in [(11, 26), (21, 27)]:
        c.set(x, y, rot[1])
    return c


def thorn_vine():
    """A barbed runner. Grass that pierces rather than grows."""
    c = raster.Canvas(CELL, CELL, None)
    g = E("grass")
    p = E("poison")
    pts = [(3, 26), (10, 20), (14, 14), (21, 9), (28, 5)]
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        c.line(x0, y0, x1, y1, g[2])
        c.line(x0, y0 - 1, x1, y1 - 1, g[1])
    for (bx, by, dx, dy) in [(10, 20, -4, -2), (14, 14, 4, -3), (21, 9, -3, 4), (7, 23, 3, 3)]:
        c.line(bx, by, bx + dx, by + dy, p[2])
        c.set(bx + dx, by + dy, p[1])
    c.set(28, 5, g[0])
    return c


def gale_blade():
    """A crescent thrown out and coming back - the returning weapon."""
    c = raster.Canvas(CELL, CELL, None)
    w = E("wind")
    outer = [(6, 6), (18, 4), (27, 12), (28, 22), (22, 28), (20, 23), (24, 18), (22, 11), (14, 8), (6, 6)]
    c.poly(outer, w[3])
    inner = [(9, 8), (18, 7), (24, 13), (25, 21), (22, 25), (21, 22), (23, 17), (20, 11), (13, 9), (9, 8)]
    c.poly(inner, w[2])
    for (x, y) in [(11, 8), (19, 8), (25, 15), (26, 22)]:
        c.set(x, y, w[1])
    # The return arc, dotted so it reads as a path rather than a second blade.
    for (x, y) in [(6, 12), (5, 17), (7, 22), (11, 26)]:
        c.set(x, y, w[3])
    return c


def molten_shard():
    """A splinter of iron still glowing from the forge. Metal body, fire at the edges."""
    c = raster.Canvas(CELL, CELL, None)
    f = E("fire")
    iron = bl.tone("steel", "shade")
    iron_lit = bl.tone("steel", "lit")
    body = [(5, 22), (13, 12), (20, 6), (27, 4), (25, 12), (18, 19), (9, 26), (5, 22)]
    c.poly(body, iron)
    c.line(6, 22, 26, 5, iron_lit)
    # Heat, concentrated along the cutting edge rather than washed over the whole splinter.
    for (x, y) in [(24, 6), (21, 9), (18, 12), (15, 15), (12, 18), (9, 22)]:
        c.set(x, y, f[2])
    for (x, y) in [(23, 7), (17, 13), (11, 20)]:
        c.set(x, y, f[1])
    spray(c, [(29, 3), (4, 27)], f, 0)
    return c


def cone_of_cold():
    """A sweep of frost widening from the caster."""
    c = raster.Canvas(CELL, CELL, None)
    i = E("ice")
    c.poly([(4, 16), (26, 4), (30, 10), (30, 22), (26, 28), (4, 16)], i[3])
    c.poly([(6, 16), (24, 8), (27, 12), (27, 20), (24, 24), (6, 16)], i[2])
    c.poly([(7, 16), (20, 11), (23, 14), (23, 18), (20, 21), (7, 16)], i[1])
    c.poly([(8, 16), (15, 14), (15, 18), (8, 16)], i[0])
    # Flecks of ice carried in the blast.
    spray(c, [(28, 6), (31, 16), (28, 26), (20, 5), (20, 27)], i, 0)
    return c




def solar_flare():
    """A sun going off around the caster. The brightest icon in the set, deliberately."""
    c = raster.Canvas(CELL, CELL, None)
    l = E("light")
    burst(c, 16, 16, 8.5, l, core_frac=0.34)
    for deg in range(0, 360, 30):
        a = math.radians(deg)
        long_ray = (deg // 30) % 2 == 0
        r0, r1 = 9.5, 15.0 if long_ray else 12.5
        c.line(16 + math.cos(a) * r0, 16 + math.sin(a) * r0,
               16 + math.cos(a) * r1, 16 + math.sin(a) * r1, l[1] if long_ray else l[2])
    return c


def scorching_ray():
    """A beam, and the only icon in the set that runs corner to corner.

    Drawn as nested QUADS, not as stacked lines. Two earlier passes thickened the beam by drawing
    the same 45-degree line at perpendicular offsets, and both came out as a crosshatch: a diagonal
    Bresenham line is a staircase of single pixels, so copies of it offset by one pixel interleave
    into a checkerboard instead of filling. Any diagonal thicker than one pixel has to be a polygon.
    """
    c = raster.Canvas(CELL, CELL, None)
    f = E("fire")

    def quad(w):
        # Perpendicular to the beam direction (1,-1) is (1,1), scaled by the half-width.
        d = w * 0.707
        return [(4 - d, 27 - d), (28 - d, 3 - d), (28 + d, 3 + d), (4 + d, 27 + d)]

    for (w, tone) in ((4.2, f[3]), (2.9, f[2]), (1.7, f[1]), (0.7, f[0])):
        c.poly(quad(w), tone)

    # The emitter flares; the far end frays into sparks.
    c.disc(5, 26, 3.6, 3.6, f[2])
    c.disc(5, 26, 1.8, 1.8, f[0])
    spray(c, [(30, 2), (26, 1), (31, 6)], f, 1)
    return c


def obsidian_spike():
    """Black glass coming up through the floor.

    Obsidian is black and the card is near-black, so a faithful obsidian spike is an invisible
    spike - the same trap the Gravewell icon fell into. The bodies are therefore drawn from the
    EARTH ramp with occlusion only on the shaded face: lit rock reading as dark glass, rather than
    dark glass reading as nothing.
    """
    c = raster.Canvas(CELL, CELL, None)
    e = E("earth")
    for (bx, top, w) in ((16, 2, 6), (8, 12, 4), (24, 14, 4)):
        # Lit face on the left, shadow face on the right, hard edge between - which is what makes
        # a triangle read as a three-dimensional shard instead of a flat wedge.
        c.poly([(bx - w, 28), (bx, top), (bx, 28), (bx - w, 28)], e[2])
        c.poly([(bx, top), (bx + w, 28), (bx, 28), (bx, top)], e[3])
        c.line(bx, top, bx, 27, OCC)
        c.line(bx - w + 1, 27, bx - 1, top + 2, e[1])
        c.set(bx, top, e[0])
    c.hline(2, 30, 29, OCC)
    spray(c, [(5, 26), (28, 27), (13, 24)], e, 1)
    return c


def cyclone_slash():
    """A sweep of wind in front of the caster - an arc, never a full ring."""
    c = raster.Canvas(CELL, CELL, None)
    w = E("wind")
    for (r, tone) in ((13.0, w[3]), (11.0, w[2]), (9.0, w[1])):
        for deg in range(-64, 66, 3):
            a = math.radians(deg)
            c.set(11 + math.cos(a) * r, 16 + math.sin(a) * r, tone)
    for deg in range(-40, 42, 20):
        a = math.radians(deg)
        c.set(11 + math.cos(a) * 15.0, 16 + math.sin(a) * 15.0, w[0])
    spray(c, [(6, 5), (5, 27), (26, 8)], w, 0)
    return c


def void_lance():
    """A long thin spike of nothing, pointed and going through.

    Shares Darkness with Shadow Bolt and must not share its shape: that one is a ring with a hole,
    this is the thinnest silhouette in the set.
    """
    c = raster.Canvas(CELL, CELL, None)
    a_r = E("arcane")
    d = E("darkness")
    c.poly([(2, 20), (26, 5), (30, 9), (8, 26), (2, 20)], d[3])
    c.poly([(4, 20), (25, 7), (27, 9), (8, 24), (4, 20)], d[2])
    c.line(5, 20, 26, 8, a_r[1])
    c.set(28, 7, a_r[0])
    spray(c, [(1, 24), (4, 28)], d, 1)
    return c


def meteor_swarm():
    """Several falling, not one landing. Fireball is the single heavy hit."""
    c = raster.Canvas(CELL, CELL, None)
    f = E("fire")
    for (cx, cy, r) in ((9, 9, 3.4), (21, 6, 2.6), (24, 18, 3.0), (13, 21, 2.2)):
        burst(c, cx, cy, r, f, core_frac=0.4)
        # Each drags a tail up and left, the direction they all came from.
        c.line(cx - r - 1, cy - r - 1, cx - r - 4, cy - r - 4, f[3])
    c.hline(3, 29, 29, f[3])
    spray(c, [(6, 26), (18, 27), (27, 26)], f, 2)
    return c


def black_tentacles():
    """Arms coming out of a hole in the ground. Eldritch, and nothing else in the set is round-armed."""
    c = raster.Canvas(CELL, CELL, None)
    p = E("poison")
    e = E("earth")
    # The pit.
    c.disc(16, 22, 10.0, 5.0, OCC)
    for deg in range(0, 360, 10):
        a = math.radians(deg)
        c.set(16 + math.cos(a) * 10.2, 22 + math.sin(a) * 5.2, e[3])
    # Four arms, each curling a different way so it never reads as a plant.
    arms = [
        [(10, 20), (7, 14), (9, 8), (13, 5)],
        [(15, 19), (15, 12), (18, 7)],
        [(20, 20), (24, 15), (23, 9)],
        [(23, 22), (28, 19), (29, 14)],
    ]
    for arm in arms:
        for i in range(len(arm) - 1):
            (x0, y0), (x1, y1) = arm[i], arm[i + 1]
            c.line(x0, y0, x1, y1, p[3])
            c.line(x0 + 1, y0, x1 + 1, y1, p[2])
        c.set(arm[-1][0], arm[-1][1], p[1])
    return c


def spiritual_weapon():
    """A spectral SWORD - hilt, guard and blade.

    The first pass reused the same nested lens as Magic Missile and came out as a purple blob at
    another angle. A weapon needs the parts that make it a weapon: the crossguard is what the eye
    reads, and it is two pixels wide.
    """
    c = raster.Canvas(CELL, CELL, None)
    a_r = E("arcane")

    # Blade, point at the upper right, drawn as light with no occlusion anywhere.
    blade = [(11, 21), (22, 6), (26, 5), (25, 9), (14, 24), (11, 21)]
    c.poly(blade, a_r[3])
    c.poly([(12, 21), (22, 8), (24, 7), (23, 10), (14, 22), (12, 21)], a_r[2])
    c.line(13, 21, 24, 7, a_r[1])
    c.set(25, 6, a_r[0])

    # Crossguard and grip - the parts that say "sword" rather than "bolt".
    c.line(6, 22, 15, 17, a_r[2])
    c.line(6, 23, 15, 18, a_r[3])
    c.line(7, 27, 12, 22, a_r[3])
    c.set(8, 26, a_r[1])

    spray(c, [(29, 3), (4, 28), (18, 3)], a_r, 1)
    return c


def aegis_ward():
    """A shield of light over a plate of steel - the one spell that is also an object."""
    c = raster.Canvas(CELL, CELL, None)
    l = E("light")
    steel = bl.tone("steel", "base")
    steel_lit = bl.tone("steel", "hi")
    steel_dark = bl.tone("steel", "deep")
    body = [(16, 3), (27, 8), (26, 19), (16, 29), (6, 19), (5, 8), (16, 3)]
    c.poly(body, steel)
    c.line(16, 3, 6, 8, steel_lit)
    c.line(6, 8, 6, 19, steel_lit)
    c.line(27, 8, 26, 19, steel_dark)
    c.line(26, 19, 16, 29, steel_dark)
    # The ward itself, burning on the face rather than behind it.
    c.disc(16, 15, 5.0, 5.5, l[3])
    c.disc(16, 15, 3.2, 3.6, l[2])
    c.disc(16, 15, 1.6, 1.8, l[0])
    return c


def glacial_spike():
    """One enormous spike of ice, point up, with the shard reading as GLASS rather than as rock.

    The difference from Obsidian Spike next to it in the list is the whole job: same silhouette
    family, so the separation has to come from tone and interior. Obsidian is opaque and lit only
    on one face; this is translucent, so it carries a bright core up its middle and the facets
    catch light on both sides.
    """
    c = raster.Canvas(CELL, CELL, None)
    i = E("ice")

    # The main shard. Tall, slightly off-centre, base wider than a needle so it has weight.
    c.poly([(9, 30), (16, 3), (23, 30), (9, 30)], i[3])
    c.poly([(13, 30), (16, 3), (19, 30), (13, 30)], i[2])
    c.line(16, 4, 16, 28, i[1])
    c.set(16, 3, i[0])
    c.set(16, 4, i[0])

    # Facet breaks across it: three horizontal steps, uneven, each a lit line over a dark one.
    for (fy, half) in ((11, 3), (18, 5), (24, 6)):
        c.hline(16 - half, 16 + half, fy, i[1])
        c.hline(16 - half, 16 + half, fy + 1, OCC)

    # Two smaller shards, different heights, so the icon is a formation and not one object.
    for (bx, top, w) in ((6, 15, 3), (26, 18, 3)):
        c.poly([(bx - w, 30), (bx, top), (bx + w, 30), (bx - w, 30)], i[3])
        c.line(bx, top + 1, bx, 29, i[2])
        c.set(bx, top, i[0])

    c.hline(2, 30, 30, OCC)
    return c


def hunters_draw():
    """A drawn bow, side on: the limbs, the string pulled back, and the arrow on it.

    Wind rather than an element ramp for the shaft, because the spell is a shot rather than a
    spell - the one icon in the set whose subject is a TOOL. It is the most literal drawing here,
    and deliberately: a bow is instantly legible at 32px and nothing else in the roster is one.
    """
    c = raster.Canvas(CELL, CELL, None)
    w = E("wind")
    g = bl.MATERIALS["gold"]

    # The limbs: two arcs meeting at the grip, drawn as short segments so the curve is even.
    for sign in (-1, 1):
        prev = None
        for k in range(11):
            t = k / 10.0
            x = 22 - 8.0 * (t ** 1.7)
            y = 16 + sign * (14.0 * (1.0 - t))
            if prev:
                c.line(prev[0], prev[1], x, y, g[2])
                c.line(prev[0] + 1, prev[1], x + 1, y, g[3])
            prev = (x, y)
        c.set(22, 16 + sign * 14, g[1])

    # The string, pulled to a point behind the grip.
    c.line(22, 2, 8, 16, w[3])
    c.line(22, 30, 8, 16, w[3])
    c.set(8, 16, w[1])

    # The arrow, nocked and pointing out of the frame.
    c.hline(8, 29, 16, w[2])
    c.hline(8, 26, 15, w[3])
    c.poly([(29, 16), (25, 13), (25, 19), (29, 16)], w[1])
    c.set(30, 16, w[0])
    return c


def spore_burst():
    """A pod that has just let go: the husk at the bottom, and the cloud coming off it.

    Poison, and the one icon in the set built around NEGATIVE space - the cloud is drawn as
    scattered discs rather than a mass, so the card reads as something dispersing rather than as
    a solid green blob, which is what the pack icon it replaces looked like.
    """
    c = raster.Canvas(CELL, CELL, None)
    p = E("poison")

    # The husk, split open.
    c.poly([(11, 30), (10, 23), (16, 20), (22, 23), (21, 30), (11, 30)], p[3])
    c.poly([(13, 29), (13, 24), (16, 22), (19, 24), (19, 29), (13, 29)], p[2])
    c.line(16, 21, 16, 29, OCC)
    c.hline(9, 23, 30, OCC)

    # The cloud. Sizes and positions written out rather than stepped, because an evenly spaced
    # spore cloud is a polka dot.
    for (sx, sy, r, tone) in ((16, 12, 4.2, p[3]), (9, 15, 3.0, p[3]), (23, 14, 3.4, p[3]),
                              (16, 12, 2.4, p[2]), (9, 15, 1.6, p[2]), (23, 14, 1.9, p[2]),
                              (13, 6, 2.2, p[3]), (20, 5, 1.8, p[3]), (16, 3, 1.4, p[2])):
        c.disc(sx, sy, r, r, tone)
    for (sx, sy) in ((16, 11), (9, 15), (23, 13), (16, 3)):
        c.set(sx, sy, p[1])
    c.set(16, 11, p[0])
    return c


ICONS = [
    ("magic-missile-arcane", magic_missile),
    ("fireball-fire", fireball),
    ("frost-shard-ice", frost_shard),
    ("chain-lightning", chain_lightning),
    ("shadow-bolt-darkness", shadow_bolt),
    ("thorn-vine-grass", thorn_vine),
    ("gale-blade-wind", gale_blade),
    ("molten-shard-metal", molten_shard),
    ("cone-of-cold-ice", cone_of_cold),
    ("solar-flare-light", solar_flare),
    ("scorching-ray-fire", scorching_ray),
    ("obsidian-spike-earth", obsidian_spike),
    ("cyclone-slash-wind", cyclone_slash),
    ("void-lance-darkness", void_lance),
    ("meteor-swarm-fire", meteor_swarm),
    ("black-tentacles-poison", black_tentacles),
    ("spiritual-weapon-arcane", spiritual_weapon),
    ("aegis-ward-light", aegis_ward),
    ("glacial-spike-ice", glacial_spike),
    ("hunters-draw-bow", hunters_draw),
    ("spore-burst-green", spore_burst),
]


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    ok = True
    for name, fn in ICONS:
        canvas = fn()
        bad = canvas.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s:" % name)
            for h, n in bad[:6]:
                print("   %s  x%d" % (h, n))
            continue
        canvas.scaled(2).save(os.path.join(OUT_DIR, "%s.png" % name))
        print("  %-24s 64x64" % name)

    if not ok:
        return 1
    print("")
    print("%d spell icons, palette clean" % len(ICONS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
