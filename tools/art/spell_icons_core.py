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
import obsidian_glass as glass
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


def _bezier(p0, p1, p2, p3, n):
    """n + 1 points along a cubic Bezier, ends included."""
    out = []
    for i in range(n + 1):
        t = i / float(n)
        u = 1.0 - t
        out.append((u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0],
                    u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]))
    return out


def _unit(dx, dy):
    d = math.hypot(dx, dy) or 1.0
    return dx / d, dy / d


def _lash_path(root, into, at, out_ctrl, bend, tip, radius, loop_side, n=40):
    """A whip's centreline with a loop rolled into it: out of `root` toward `at`, once round a loop of
    `radius` on the `loop_side` (+1 left of the way it runs, -1 right) where it reaches `at`, and on to
    `tip`. The same shape WhipStroke.Splice lays in the game: a circle on the lash at the bend, a little
    long along it."""
    head = _bezier(root, into, into, at, n // 3)
    tx, ty = _unit(at[0] - into[0], at[1] - into[1])
    nx, ny = (ty, -tx) if loop_side > 0 else (-ty, tx)
    loop = []
    for k in range(1, 25):
        a = TAU * k / 24.0
        loop.append((at[0] + tx * radius * 1.15 * math.sin(a) + nx * radius * (1.0 - math.cos(a)),
                     at[1] + ty * radius * 1.15 * math.sin(a) + ny * radius * (1.0 - math.cos(a))))
    tail = _bezier(at, out_ctrl, bend, tip, n)
    return head + loop + tail[1:]


def _lash_width(w0, w1, t):
    """A whip's taper: thick for most of its length, thinning fast toward the tip."""
    return w1 + (w0 - w1) * (1.0 - t) ** 0.6


def _stamp_lash(c, path, w0, w1, tone_at, outline=OCC):
    """Stamps a lash along `path`, `w0` px wide at its start thinning to `w1` at its end: an outline a pixel
    wider all round first, then the body, each point's colour picked by tone_at(t, nx, ny) - t along it,
    (nx, ny) the offset from the centreline - so a side can be lit and a stretch can change material."""
    n = len(path)
    lengths = [0.0]
    for i in range(1, n):
        lengths.append(lengths[-1] + math.hypot(path[i][0] - path[i - 1][0], path[i][1] - path[i - 1][1]))
    total = lengths[-1] or 1.0
    body = {}
    for i in range(n):
        t = lengths[i] / total
        r = _lash_width(w0, w1, t) * 0.5
        cx, cy = path[i]
        for y in range(int(cy - r - 2), int(cy + r + 3)):
            for x in range(int(cx - r - 2), int(cx + r + 3)):
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                key = (x, y)
                if d <= r + 0.15:
                    if key not in body or body[key][0] > d - r:
                        body[key] = (d - r, t, (x + 0.5 - cx), (y + 0.5 - cy))
                elif d <= r + 1.15 and key not in body:
                    c.set(x, y, outline)
    for (x, y), (_, t, ox, oy) in body.items():
        c.set(x, y, tone_at(t, ox, oy))
    return lengths, total


def thorn_vine():
    """The vine mid-crack (2026-10-31): "the vine lash spell icon is very basic and doesn't show action."

    It was a straight barbed runner, a diagonal line with four spikes - a picture of a thing, not of what
    the spell does. Now it is the whip stroke the game draws (scripts/WhipStroke.cs): the vine up out of
    torn ground at the lower left, bark where it leaves the earth, a LOOP rolled into it as the wave runs
    out along it, and the tip whipped round to the upper right where it CRACKS - a white-hot star of
    grass-light and a spit of poison - with speed lines behind the tip along the way it came. Thorns are
    raked back down the whole length. An occlusion line round all of it, as the game's vine has, holds it
    on the cream card and the dark HUD alike. The root and the tip stay where the evolution cards expect
    them (tools/art/evolution_icons.py, VINE_ROOT and tip)."""
    c = raster.Canvas(CELL, CELL, None)
    g = E("grass")
    p = E("poison")
    e = E("earth")
    root, tip = (3.5, 28.5), (27.5, 5.5)
    into, at, out_ctrl, bend = (8.5, 28.5), (13.0, 23.5), (16.5, 9.0), (20.0, 4.0)
    path = _lash_path(root, into, at, out_ctrl, bend, tip, 3.3, +1)

    # The tip's swing: two arcs of speed lines under the hook it whipped over, round the way it came -
    # from out to the right, up and over to where it cracks - in dashes, hottest by the tip.
    for k, r in enumerate((7.5, 10.5)):
        for d in range(-72, 6 - k * 12, 3):
            if (d // 3 + k) % 4 == 3:
                continue
            a = math.radians(d)
            heat = (d + 72) / 78.0
            c.set(19.5 + math.cos(a) * r, 18.5 + math.sin(a) * r,
                  g[1] if heat < 0.3 else (g[2] if heat < 0.65 else g[3]))

    def tone(t, ox, oy):
        lit = (ox + oy) < -0.4         # the side toward the key light, upper left
        shade = (ox + oy) > 0.6
        if t < 0.1:                     # bark where it leaves the ground
            return e[2] if lit else e[3]
        if t > 0.94:                    # the poisoned tip, white-hot as it cracks
            return p[1] if t < 0.975 else p[0]
        return g[1] if lit else (g[3] if shade else g[2])

    w0, w1 = 3.4, 1.0
    lengths, total = _stamp_lash(c, path, w0, w1, tone)

    # Thorns raked back toward the root, alternating sides, along the whole of it but the very ends:
    # each a pixel of the lit green with an occlusion pixel behind it, so it reads off the stem.
    side = 1
    for i in range(2, len(path) - 4):
        t = lengths[i] / total
        if not (0.1 < t < 0.9) or int(lengths[i] / 3.2) == int(lengths[i - 1] / 3.2):
            continue
        tx, ty = _unit(path[i + 1][0] - path[i - 1][0], path[i + 1][1] - path[i - 1][1])
        nx, ny = -ty * side, tx * side
        w = _lash_width(w0, w1, t) * 0.5
        for (reach, col) in ((w + 0.9, OCC), (w + 1.9, g[1])):
            bx, by = path[i][0] + nx * reach - tx * (reach - w) * 0.8, path[i][1] + ny * reach - ty * (reach - w) * 0.8
            here = c.get(int(bx), int(by))
            if here[3] == 0 or here[:3] == raster.rgb(OCC)[:3]:
                c.set(bx, by, col)
        side = -side

    # Torn ground where it comes up.
    for (x, y, tone_) in ((0, 30, e[3]), (1, 31, OCC), (6, 31, e[3]), (7, 30, e[2]), (2, 31, e[2]), (5, 31, OCC), (8, 31, e[3])):
        c.set(x, y, tone_)

    # The crack: a white-hot heart, four arms, the diagonals short, and poison spat the way it went.
    tx_, ty_ = int(round(tip[0])), int(round(tip[1]))
    for d in (1, 2, 3):
        tone_ = g[0] if d == 1 else (g[1] if d == 2 else g[2])
        for (dx, dy) in ((d, 0), (-d, 0), (0, d), (0, -d)):
            c.set(tx_ + dx, ty_ + dy, tone_)
    for (dx, dy) in ((1, 1), (-1, -1), (1, -1), (-1, 1)):
        c.set(tx_ + dx, ty_ + dy, p[1])
    c.set(tx_, ty_, g[0])
    for (dx, dy, tone_) in ((-4, -3, p[2]), (3, -4, p[1]), (-5, 0, p[2]), (4, 2, p[3])):
        c.set(tx_ + dx, ty_ + dy, tone_)
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
    """A blade of black volcanic glass bursting up through cracked, glowing ground.

    Redrawn 2026-10-28 (Eric: "obsidian spike icon and in game sprite doesn't look very cool,
    update"). It was three tan triangles in the Earth ramp standing on a black line - rock, not
    glass, and a wedge in any colour. Now it is the spike the spell puts in the crowd
    (spell_fx.obsidian_spike), drawn by the same hand (obsidian_glass.blade): one tall knapped blade
    leaning into the burst with two smaller ones splayed off its foot, every one faceted - a lit
    plane with the sky's reflection down it, a shadow plane with the ripples obsidian breaks in, and
    a specular streak down the ridge that ends in a star. The Earth is the light: the ground it tore
    through is cracked open and white-hot where it came out, and the glass mirrors that heat at its
    foot. A few chips of glass and sparks are still in the air.

    It reads on both grounds the icon sits on: dark-on-light on the parchment page, and on the dark
    HUD slot by its specular, its reflection band and the glowing ground. Glacial Spike, beside it
    in the list, is one pale translucent spike with a lit spine; this is a dark cluster lit from below.
    """
    c = raster.Canvas(CELL, CELL, None)
    e = E("earth")
    gy = 27

    # The ground torn open: cracks running out from the mouth the glass came through, hot at the
    # mouth and cooling down the ramp as they run - sideways along the ground and fanning toward
    # the viewer, never one line across the card (that read as a plinth). Drawn first: the blades
    # stand in them.
    for path in (
        [(14, 28), (11, 28), (8, 29), (4, 29)],
        [(18, 28), (22, 29), (25, 29), (29, 30)],
        [(15, 29), (13, 30), (12, 31)],
        [(17, 29), (19, 30), (21, 31)],
    ):
        glass.crack(c, path, e)

    # The blades. The tall one leans into the burst; two smaller ones splay outward off its foot,
    # one behind on the left and one in front on the right, and a stub breaks the ground in front -
    # the lean and the splay are what say "burst" rather than "grown".
    glass.blade(c, (3, 12), 6, 12, 9, gy, heat=0.6, ramp=e, lean_l=0.5, lean_r=0, spec=0.5, ripples=False)
    glass.blade(c, (21, 1), 11, 22, 16, gy, heat=1.0, ramp=e, lean_l=1.0, lean_r=0.5, chip=0.4, spec=0.62)
    glass.blade(c, (30, 15), 21, 27, 23, gy, heat=0.8, ramp=e, lean_l=0, lean_r=0.5, spec=0.45, ripples=False)
    glass.blade(c, (9, 21), 7, 13, 10, gy + 1, heat=0.8, ramp=e, band=False, ripples=False, spec=0.6)

    # Where it came out: the mouth of the crack at its foot, white-hot.
    c.hline(12, 19, 28, e[1])
    c.hline(14, 17, 28, e[0])

    # The star on the ridge: the key caught at one point of the edge, the sharpest thing on the card.
    sx, sy = 20, 4
    for (dx, dy) in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        c.set(sx + dx, sy + dy, glass.SPEC2)
    c.set(sx, sy, glass.SPEC)

    # Chips of glass thrown off the burst, each lit on its upper pixel, and the eruption's sparks.
    for (x, y) in ((9, 6), (28, 5), (3, 22)):
        glass.chip(c, x, y)
    spray(c, [(13, 15), (26, 11), (6, 6)], e, 2)
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
    # Redrawn 2026-10-22: the first version was one wide parallelogram, a bar with no point and no
    # shaft, and the user read it as a stick. A lance is three things at icon size - a long thin
    # shaft, a long tapered head, and the collar where they meet - so it is drawn as those three,
    # on the diagonal from the lower left, with the void still trailing off the butt.
    c = raster.Canvas(CELL, CELL, None)
    a_r = E("arcane")
    d = E("darkness")

    # The shaft: two pixels wide, dark edge under a mid core, so it stays a line and not a bar.
    c.line(3, 29, 18, 14, d[3])
    c.line(4, 29, 19, 14, d[3])
    c.line(3, 28, 18, 13, d[2])

    # The head: a long thin diamond, widest a third of the way up, ending in a needle tip.
    c.poly([(17, 13), (19, 8), (30, 1), (24, 12), (19, 15), (17, 13)], d[3])
    c.poly([(18, 13), (20, 9), (28, 3), (23, 11), (19, 14), (18, 13)], d[2])
    # The bright edge down its spine is what makes it read as a blade rather than a leaf.
    c.line(19, 13, 29, 2, a_r[1])
    c.set(30, 1, a_r[0])
    c.set(29, 2, a_r[0])

    # The collar, across the shaft where the head sits - short, or it becomes a sword's crossguard.
    c.line(16, 12, 19, 15, a_r[2])
    c.line(17, 12, 20, 15, d[3])

    # Void bleeding off the butt and along the line of travel.
    spray(c, [(1, 30), (2, 26), (6, 30), (8, 25)], d, 1)
    spray(c, [(4, 23), (11, 28)], a_r, 2)
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
    """The spell's own card, which opens the page of its three forms: the axe, the hammer and the sword
    crossed on the wizard's violet night (2026-10-28, spell variants). Each form has its own card
    icon (spell_icons.py), all drawn by tools/art/spirit_weapons.py - the renderer that draws the weapons
    the game swings, so the cards show the same three weapons.
    """
    import spirit_weapons
    return spirit_weapons.trio()


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
    """The spike the spell draws, at its last frame before it withdraws (spell_fx.glacial_spike):
    one tall clean triangle of ice with a lit spine and the three cracks across it, standing in a
    scatter of broken floor - it erupts from the ground under its target.

    It was a stepped tower with dark bands across it and a smaller spike either side - a pagoda,
    and nothing the spell ever puts on screen. Eric, 2026-10-28: "glacial spike icon does't look
    like the actual spell". The difference from Obsidian Spike next to it in the list is tone and
    count: one translucent ice spike with a bright spine, against a splayed cluster of black glass
    lit from the ground below.
    """
    c = raster.Canvas(CELL, CELL, None)
    i = E("ice")
    top, base = 3, 28

    # The spike, the spell's own shape: widening evenly to the floor, edge tone outside, body in.
    for y in range(top, base + 1):
        u = (y - top) / float(base - top)
        half = 1.0 + 7.5 * u
        c.hline(16 - half, 16 + half, y, i[3])
        c.hline(16 - half + 1, 16 + half - 1, y, i[2])
    c.line(16, top + 1, 16, base - 1, i[1])
    c.set(16, top, i[0])
    c.set(16, top + 1, i[0])

    # The cracks it shows as it is about to go back into the ground, where the spell draws them.
    for (x0, y0, x1, y1) in ((16, 11, 10, 20), (16, 16, 22, 25), (16, 21, 12, 28)):
        c.line(x0, y0, x1, y1, i[0])

    # Broken floor thrown out at its foot, either side.
    for (cx, cy, r) in ((5, 28, 1.7), (8.5, 26, 1.2), (27, 28, 1.7), (23.5, 26, 1.2), (2.5, 25, 0.9), (29.5, 25, 0.9)):
        c.disc(cx, cy, r, r * 0.8, i[2])
        c.set(cx - 0.5, cy - 0.5, i[1])

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


def arcane_explosion():
    """A ring going out from a point. The only spell in the roster with no aim at all - it goes
    off around you - so the icon is drawn concentric rather than pointing anywhere."""
    c = raster.Canvas(CELL, CELL, None)
    a = E("arcane")
    for (r, tone) in ((14.0, a[3]), (10.5, a[2]), (7.0, a[3])):
        for d in range(0, 360, 4):
            ang = math.radians(d)
            c.set(16 + math.cos(ang) * r, 16 + math.sin(ang) * r, tone)
            c.set(16 + math.cos(ang) * (r - 1), 16 + math.sin(ang) * (r - 1), tone)
    burst(c, 16, 16, 5.0, a)
    for d in range(0, 360, 60):
        ang = math.radians(d + 15)
        c.set(16 + math.cos(ang) * 15.5, 16 + math.sin(ang) * 15.5, a[0])
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
    ("arcane-explosion", arcane_explosion),
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
