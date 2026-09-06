# -*- coding: utf-8 -*-
"""Shared furniture for the splash screens.

Seven screens that each invent their own wizard would not be seven takes on one game, so the
figure, the wordmark, the sigils and the light behaviour live here and the screens compose
them. What a screen is allowed to change is camera, palette emphasis, and what is on frame -
which is the same rule the three direction proposals were judged under.
"""

import math
import random

import bonelight as bl
import font
import raster

M = bl.MATERIALS
E = bl.ELEMENTS
OCC = bl.OCC
RIM = bl.RIM


def ramp_rows(colors, n):
    """Spread `colors` (top -> bottom) over n rows, for per-row lettering gradients."""
    out = []
    for i in range(n):
        t = i / float(max(1, n - 1)) * (len(colors) - 1)
        out.append(colors[int(round(t))])
    return out


# --- wordmark ---------------------------------------------------------------
# Both lines are tuned to land on 186px so the block reads as one justified stack.
TITLE_W = font.measure("WIZARD", font.DISPLAY, 3, 2)
SUB_W = font.measure("SURVIVORS", font.DISPLAY, 2, 2)


def wordmark(c, cx, y, top_colors, sub_colors, halo=None, shadow=OCC, gap=8):
    """Draw the two-line wordmark centred on cx with its cap-line at y. Returns bottom y."""
    x1 = cx - TITLE_W // 2
    x2 = cx - SUB_W // 2
    y2 = y + font.DISPLAY_H * 3 + gap
    if halo:
        font.outline(c, "WIZARD", x1, y, font.DISPLAY, halo, 3, 2)
        font.outline(c, "SURVIVORS", x2, y2, font.DISPLAY, halo, 2, 2)
    font.draw(c, "WIZARD", x1, y, font.DISPLAY, ramp_rows(top_colors, font.DISPLAY_H),
              3, 2, shadow=shadow, shadow_off=(2, 3))
    font.draw(c, "SURVIVORS", x2, y2, font.DISPLAY, ramp_rows(sub_colors, font.DISPLAY_H),
              2, 2, shadow=shadow, shadow_off=(2, 2))
    return y2 + font.DISPLAY_H * 2


def caption(c, text, cx, y, color, scale=1, track=1, shadow=OCC):
    w = font.measure(text, font.SMALL, scale, track)
    font.draw(c, text, cx - w // 2, y, font.SMALL, color, scale, track,
              shadow=shadow, shadow_off=(1, 1))
    return w


# --- figures ----------------------------------------------------------------
# One geometry, two renderers. The silhouette and the lit figure must be the same wizard or
# the seven screens stop being seven views of one game - and the first pass proved the point
# by producing a shape that read as a traffic cone at every size it appeared.
#
# The read is carried by three things, in this order: the BRIM of the hat (a cone alone is a
# cone), the step from brim to shoulder, and the staff breaking the silhouette on one side.


def wizard_shapes(cx, yb, u):
    """Named polygons for the figure, in draw order. u = height / 32."""
    return [
        ("robe", [(cx - 4.5 * u, yb - 17 * u), (cx + 4.5 * u, yb - 17 * u),
                  (cx + 9.5 * u, yb - 1 * u), (cx + 8.5 * u, yb),
                  (cx - 8.5 * u, yb), (cx - 9.5 * u, yb - 1 * u)]),
        ("sleeve", [(cx + 3 * u, yb - 16 * u), (cx + 8.5 * u, yb - 14 * u),
                    (cx + 9.5 * u, yb - 9 * u), (cx + 6 * u, yb - 8.5 * u),
                    (cx + 4 * u, yb - 12 * u)]),
        ("sleeveL", [(cx - 3 * u, yb - 16 * u), (cx - 8 * u, yb - 13.5 * u),
                     (cx - 8.5 * u, yb - 9 * u), (cx - 5.5 * u, yb - 9 * u),
                     (cx - 4 * u, yb - 12 * u)]),
        ("head", [(cx - 3.4 * u, yb - 21 * u), (cx + 3.4 * u, yb - 21 * u),
                  (cx + 3 * u, yb - 16.5 * u), (cx - 3 * u, yb - 16.5 * u)]),
        ("brim", [(cx - 10.5 * u, yb - 21.6 * u), (cx - 5 * u, yb - 23.4 * u),
                  (cx + 5 * u, yb - 23.4 * u), (cx + 10.5 * u, yb - 21.6 * u),
                  (cx + 5.5 * u, yb - 20.2 * u), (cx - 5.5 * u, yb - 20.2 * u)]),
        ("cone", [(cx + 1.6 * u, yb - 32 * u), (cx + 3.2 * u, yb - 29 * u),
                  (cx + 5.6 * u, yb - 22.6 * u), (cx - 4.8 * u, yb - 22.6 * u),
                  (cx - 2.4 * u, yb - 27 * u)]),
    ]


def _staff_geom(cx, yb, u):
    """Staff x, top y, orb centre. Held out on the key-facing side so it breaks the outline."""
    sx = cx - 10.5 * u
    return sx, yb - 27 * u, (sx, yb - 29.5 * u)


def wizard_sil(c, cx, ybase, h, color=OCC, rim=None, staff_ramp=None):
    """Flat-fill wizard for poster compositions. h is total height including the hat."""
    u = h / 32.0
    for _, pts in wizard_shapes(cx, ybase, u):
        c.poly(pts, color)
    sx, stop, orb = _staff_geom(cx, ybase, u)
    for i in range(max(1, int(round(1.6 * u)))):
        c.vline(int(sx) + i, stop, ybase, color)
    if staff_ramp:
        r = max(2.5, 3.4 * u)
        c.radial(orb[0], orb[1], r, r, [staff_ramp[0], staff_ramp[1], staff_ramp[2],
                                        staff_ramp[3], None])
    if rim:
        _rim_pass(c, cx - 13 * u, cx + 13 * u, ybase - 33 * u, ybase, rim)


def wizard_back(c, cx, ybase, s=2, robe="wool", lit=True, staff_ramp=None):
    """The lit figure: the same geometry, shaded on the five-tone rule with the key upper-left."""
    u = s * 32 / 32.0
    r = M[robe]
    h = 32 * s
    u = h / 32.0
    c.disc(cx, ybase + max(1, u), 10 * u, 2.6 * u, OCC)
    shapes = dict(wizard_shapes(cx, ybase, u))
    # bulk
    c.poly(shapes["robe"], r[2])
    c.poly(shapes["sleeve"], r[3])
    c.poly(shapes["sleeveL"], r[1])
    # key-lit left third of the robe
    c.poly([(cx - 4.5 * u, ybase - 17 * u), (cx - 1 * u, ybase - 17 * u),
            (cx - 3 * u, ybase), (cx - 8.5 * u, ybase)], r[1])
    c.poly([(cx + 5 * u, ybase - 12 * u), (cx + 7 * u, ybase - 12 * u),
            (cx + 9.5 * u, ybase - 1 * u), (cx + 6 * u, ybase)], r[4])
    c.hline(cx - 9.5 * u, cx + 9.5 * u, ybase, OCC)
    # hood / head, then the hat over it
    c.poly(shapes["head"], r[3])
    c.poly(shapes["cone"], r[2])
    c.poly([(cx - 2.4 * u, ybase - 27 * u), (cx - 4.8 * u, ybase - 22.6 * u),
            (cx - 1.4 * u, ybase - 22.6 * u), (cx + 0.4 * u, ybase - 28 * u)], r[1])
    c.poly(shapes["brim"], r[2])
    c.poly([(cx - 10.5 * u, ybase - 21.6 * u), (cx - 5 * u, ybase - 23.4 * u),
            (cx + 0 * u, ybase - 23.4 * u), (cx - 2 * u, ybase - 21.8 * u)], r[1])
    # occlusion under the brim: the shape only reads if the brim casts
    c.hline(cx - 5.5 * u, cx + 5.5 * u, ybase - 20.0 * u, OCC)
    c.hline(cx - 4.5 * u, cx + 4.5 * u, ybase - 19.3 * u, r[4])
    # the void where a face would be
    c.disc(cx, ybase - 18.4 * u, 2.4 * u, 1.5 * u, r[4])
    if lit:
        _rim_pass(c, cx - 13 * u, cx + 13 * u, ybase - 33 * u, ybase, RIM)
    sx, stop, orb = _staff_geom(cx, ybase, u)
    for i in range(max(1, int(round(1.6 * u)))):
        c.vline(int(sx) + i, stop, ybase, M["gold"][2])
    c.vline(int(sx), stop, ybase, M["gold"][1])
    ramp = staff_ramp or E["arcane"]
    rr = max(3.0, 4.2 * u)
    c.radial(orb[0], orb[1], rr, rr, [ramp[0], ramp[1], ramp[2], ramp[3], None])


def _rim_pass(c, x0, x1, y0, y1, color):
    """1px bone-white on the key-facing (left) silhouette edge. The contract's only separation
    device for actors, and the reason none of them carry an outline."""
    for y in range(int(y0), int(y1) + 1):
        for x in range(int(x0), int(x1) + 1):
            if c.get(x, y)[3] and not c.get(x - 1, y)[3]:
                c.set(x - 1, y, color)
                break


ENEMY_KINDS = ("swarmer", "runner", "bruiser", "shielder")


def enemy_sil(c, cx, ybase, kind, h, color=OCC, rim=None):
    """Small enemy silhouettes carrying the taxonomy's class-reading attachment."""
    u = h / 20.0
    if kind == "swarmer":
        c.poly([(cx - 3.5 * u, ybase), (cx + 3.5 * u, ybase), (cx + 3 * u, ybase - 11 * u),
                (cx + 1.5 * u, ybase - 17 * u), (cx, ybase - 20 * u),
                (cx - 1.5 * u, ybase - 17 * u), (cx - 3 * u, ybase - 11 * u)], color)
    elif kind == "runner":
        c.poly([(cx - 5 * u, ybase), (cx + 4 * u, ybase), (cx + 4 * u, ybase - 9 * u),
                (cx + 1 * u, ybase - 14 * u), (cx - 4 * u, ybase - 12 * u)], color)
        c.poly([(cx - 4 * u, ybase - 11 * u), (cx - 4 * u, ybase - 5 * u),
                (cx - 11 * u, ybase - 3 * u), (cx - 9 * u, ybase - 10 * u)], color)
    elif kind == "bruiser":
        c.poly([(cx - 7 * u, ybase), (cx + 7 * u, ybase), (cx + 6 * u, ybase - 12 * u),
                (cx - 6 * u, ybase - 12 * u)], color)
        c.poly([(cx - 4 * u, ybase - 11 * u), (cx + 4 * u, ybase - 11 * u),
                (cx + 3 * u, ybase - 17 * u), (cx - 3 * u, ybase - 17 * u)], color)
        c.poly([(cx - 3 * u, ybase - 16 * u), (cx - 8 * u, ybase - 22 * u),
                (cx - 5 * u, ybase - 15 * u)], color)
        c.poly([(cx + 3 * u, ybase - 16 * u), (cx + 8 * u, ybase - 22 * u),
                (cx + 5 * u, ybase - 15 * u)], color)
    else:  # shielder
        c.poly([(cx - 4 * u, ybase), (cx + 5 * u, ybase), (cx + 4 * u, ybase - 12 * u),
                (cx + 2 * u, ybase - 17 * u), (cx - 3 * u, ybase - 16 * u)], color)
        c.rect(cx - 8 * u, ybase - 15 * u, cx - 3 * u, ybase - 2 * u, color)
    if rim:
        _rim_pass(c, cx - 12 * u, cx + 10 * u, ybase - 23 * u, ybase, rim)


# --- woods -------------------------------------------------------------------
def _stroke(c, x0, y0, x1, y1, r0, r1, color):
    """A tapering rounded stroke. Limbs drawn as polygons come out faceted; drawn as a chain
    of discs they keep a round section all the way to the tip."""
    steps = max(2, int(math.hypot(x1 - x0, y1 - y0)) + 1)
    for i in range(steps + 1):
        t = i / float(steps)
        rr = max(0.5, r0 + (r1 - r0) * t)
        c.disc(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, rr, rr, color)


def _clump(c, cx, cy, rx, ry, n, rng, dark, light):
    """A mass of leaves as scattered discs rather than one filled shape.

    A solid ellipse reads as a lollipop tree. Scattering it leaves a ragged, gappy edge, which
    is what makes a canopy look like foliage and, at this value, like foliage you cannot see
    through.
    """
    for _ in range(n):
        a = rng.random() * 6.28318
        d = rng.random() ** 0.55
        r = 1 + (rng.random() < 0.3) + (rng.random() < 0.12)
        c.disc(cx + math.cos(a) * rx * d, cy + math.sin(a) * ry * d, r, r,
               dark if rng.random() < 0.86 else light)


def _limbs(c, x0, y0, ang, length, width, depth, rng, dark, light, leaf_r, density, vines):
    """Recursive limb, with a leaf clump at every terminal tip.

    Shared by tree() and bough() so a branch reaching in from off-frame is built the same way
    as one on a trunk - a canopy assembled from two different routines does not read as one
    wood.
    """
    x1 = x0 + math.cos(ang) * length
    y1 = y0 + math.sin(ang) * length
    _stroke(c, x0, y0, x1, y1, width, max(0.7, width * 0.55), dark)
    if depth <= 0:
        _clump(c, x1, y1, leaf_r, leaf_r * 0.78, int(leaf_r * 2.4 * density), rng, dark, light)
        if vines and rng.random() < 0.35:          # something hanging, unexplained
            for j in range(int(leaf_r * 1.6)):
                if j % 3 != 2:
                    c.set(int(x1 + rng.randrange(-2, 3)), int(y1 + leaf_r * 0.8 + j), dark)
        return
    for k in (-1, 1):
        _limbs(c, x1, y1, ang + k * (0.36 + rng.random() * 0.34),
               length * (0.60 + rng.random() * 0.14), max(0.8, width * 0.62), depth - 1,
               rng, dark, light, leaf_r * 0.86, density, vines)
    if rng.random() < 0.45:                        # an odd third limb: no tree is a fork
        _limbs(c, x1, y1, ang + (rng.random() - 0.5) * 0.5, length * 0.5,
               max(0.8, width * 0.5), depth - 1, rng, dark, light, leaf_r * 0.8,
               density, vines)


def tree(c, x, ybase, h, spread, seed=0, dark=None, light=None, density=1.0, vines=True):
    """A tall crooked tree in near-silhouette, canopy included.

    Vigil started as a stone circle on open moor and the standing stones read as trees anyway,
    so they became trees: a clearing in deep woods is a better answer to "where is he" than a
    field is, and a canopy closing over the frame is most of what makes a scene feel like
    somewhere you are lost rather than somewhere you arrived.
    """
    rng = random.Random(seed)
    dark = dark or M["stone"][4]
    light = light or M["stone"][3]
    lean = (rng.random() - 0.5) * h * 0.10
    tx = x + lean
    ty = ybase - h * 0.52
    _stroke(c, x, ybase, tx, ty, max(1.2, h * 0.030), max(1.0, h * 0.013), dark)
    for k in (-1, 1):                              # root flare
        _stroke(c, x, ybase - h * 0.03, x + k * h * 0.045, ybase + 1,
                max(1.0, h * 0.016), max(0.8, h * 0.008), dark)
    for k in (-1, 1, 0):
        _limbs(c, tx, ty + rng.random() * h * 0.06,
               -1.5708 + k * (0.42 + rng.random() * 0.2),
               h * (0.20 + rng.random() * 0.08), max(1.0, h * 0.016), 2,
               rng, dark, light, spread * 0.20, density, vines)


def bough(c, x, y, ang, length, width, seed=0, dark=None, light=None, leaf_r=16,
          density=1.0, depth=3):
    """A limb reaching in from off-frame, for the overhead canopy."""
    rng = random.Random(seed)
    _limbs(c, x, y, ang, length, width, depth, rng,
           dark or OCC, light or M["stone"][4], leaf_r, density, False)


def canopy(c, y0, y1, clear, n, seed=0, dark=None, light=None, width=360, density=1.0):
    """Leaf mass filling a band of sky, with one deliberate hole left in it.

    [H[2J[3J is (cx, cy, rx, ry): the break in the canopy the wordmark sits in. Foliage thickens
    with distance from that hole, so the clearing reads as something you are looking up
    through rather than as a gap the artist forgot to fill.
    """
    rng = random.Random(seed)
    dark = dark or OCC
    light = light or M["stone"][4]
    cx, cy, rx, ry = clear
    # The hole is lobed, not elliptical. A clean ellipse of missing leaves reads as a vignette
    # someone applied rather than as a break in a canopy, and it was the first thing that gave
    # the effect away.
    ph = [rng.random() * 6.28318 for _ in range(3)]
    placed, tries = 0, 0
    while placed < n and tries < n * 40:
        tries += 1
        x = rng.randrange(-24, width + 24)
        y = rng.randrange(int(y0), int(y1))
        nx = (x - cx) / float(rx)
        ny = (y - cy) / float(ry)
        d = math.sqrt(nx * nx + ny * ny)
        a = math.atan2(ny, nx)
        edge = (1.0 + 0.20 * math.sin(a * 3 + ph[0]) + 0.12 * math.sin(a * 5 + ph[1])
                + 0.07 * math.sin(a * 9 + ph[2]))
        if d < edge:
            continue
        if rng.random() > min(1.0, 0.20 + (d - edge) * 1.8):
            continue
        r = rng.randrange(5, 19)
        _clump(c, x, y, r, r * 0.76, int(r * 2.3 * density), rng, dark, light)
        placed += 1


def flame(c, x, ybase, h, ramp, seed=0, width=None, halo=None):
    """A standing flame: bloom, tapering body, hot inner core, sparks above it.

    Built as a stack of discs down a wavering axis rather than as one teardrop polygon. A
    flame that is a single smooth shape reads as a leaf; the value break between the body and
    the core, and the ragged edge the disc stack leaves, are what make it read as burning.
    """
    # The wide bloom is PIGMENT, not the element ramp - it is the ground and air the flame is
    # lighting, not the flame. It has to follow the flame's colour even so: a green fire over
    # a gold halo is lighting something that is not there.
    halo = halo or M["gold"]
    rng = random.Random(seed)
    w = width or h * 0.185
    # Two-stage bloom. One filled ellipse of the ramp bottom is a solid gold disc, and the
    # flame drawn inside it disappears - which is why the first pass read as a glowing ball
    # with a tail. The wide halo is pigment gold, dim; only the tight one emits.
    c.radial(x, ybase - h * 0.30, h * 0.95, h * 1.02,
             [halo[2], halo[3], None])
    c.radial(x, ybase - h * 0.30, h * 0.42, h * 0.48, [ramp[3], None])
    lean = (rng.random() - 0.5) * h * 0.22
    n = max(7, int(h))
    for i in range(n + 1):                                # body
        t = i / float(n)
        prof = (1.0 - t) ** 0.55 * (1.0 + 0.35 * math.sin(t * 3.14159))
        px = x + lean * t * t + math.sin(t * 5.2 + rng.random() * 0.25) * h * 0.035
        py = ybase - h * t
        c.disc(px, py, max(0.6, w * prof), max(0.7, w * prof * 1.2),
               ramp[3] if t > 0.62 else ramp[2])
    # The core is deliberately thin. At half the body width it swallowed the flame and the
    # whole thing read as a glowing lump rather than as something burning.
    for i in range(n + 1):
        t = i / float(n)
        prof = (1.0 - t) ** 0.75 * (1.0 + 0.3 * math.sin(t * 3.14159))
        c.disc(x + lean * t * t, ybase - h * t * 0.62,
               max(0.5, w * prof * 0.30), max(0.6, w * prof * 0.42),
               ramp[0] if t < 0.35 else ramp[1])
    for _ in range(3):                                    # sparks
        c.disc(x + (rng.random() - 0.5) * h * 0.7,
               ybase - h * (1.05 + rng.random() * 0.55), 1, 1,
               ramp[1] if rng.random() < 0.5 else ramp[2])


def brush(c, x, y, w, h, seed=0, dark=None, light=None, density=1.0):
    """Low undergrowth: a leaf mass wider than it is tall, sitting on a line.

    The trunks alone leave a clean gap between the wood and the ground, and a clean gap is
    the opposite of claustrophobic - it reads as a park. Brush closes it.
    """
    rng = random.Random(seed)
    _clump(c, x, y, w, h, int(w * 2.6 * density), rng,
           dark or M["stone"][4], light or M["stone"][3])


def grass(c, x, ybase, h, seed=0, dark=None, light=None, blades=5):
    """One tuft: a fan of blades from a single point, one of them catching light."""
    rng = random.Random(seed)
    dark = dark or M["lichen"][4]
    light = light or M["lichen"][2]
    n = max(2, blades - 1 + rng.randrange(3))
    for i in range(n):
        t = (i / float(max(1, n - 1))) * 2.0 - 1.0
        tip_x = x + t * h * 0.55 + (rng.random() - 0.5) * h * 0.2
        tip_y = ybase - h * (0.55 + rng.random() * 0.55)
        c.line(x + t * h * 0.12, ybase, tip_x, tip_y, dark)
    c.line(x, ybase, x + (rng.random() - 0.5) * h * 0.5,
           ybase - h * (0.7 + rng.random() * 0.4), light)


def boulder(c, x, ybase, w, h, seed=0, ramp=None):
    ramp = ramp or M["stone"]
    rng = random.Random(seed)
    pts = []
    for i in range(9):
        a = math.pi + i * math.pi / 8.0
        pts.append((x + math.cos(a) * w * 0.5 * (0.85 + rng.random() * 0.3),
                    ybase + math.sin(a) * h * (0.85 + rng.random() * 0.3)))
    pts += [(x + w * 0.5, ybase), (x - w * 0.5, ybase)]
    c.poly_shade(pts, [ramp[3], ramp[4], OCC], ang=-0.7854, gamma=0.8)
    c.hline(x - w * 0.5, x + w * 0.5, ybase, OCC)


# --- world furniture --------------------------------------------------------
def starfield(c, y0, y1, n, seed, colors=None):
    colors = colors or [M["skin"][0], M["skin"][1], M["arcane"][1]]
    rng = random.Random(seed)
    for _ in range(n):
        x = rng.randrange(0, c.w)
        y = rng.randrange(int(y0), int(y1))
        c.set(x, y, colors[rng.randrange(len(colors))])


def moon(c, cx, cy, r, glow=True):
    if glow:
        c.radial(cx, cy, r * 3.2, r * 3.2,
                 [M["wool"][3], M["wool"][3], M["wool"][4], M["stone"][4], None])
    c.disc(cx, cy, r, r, M["skin"][1])
    # key from the upper left, so the terminator sits lower-right
    c.disc(cx - r * 0.22, cy - r * 0.22, r * 0.82, r * 0.82, M["skin"][0])
    rng = random.Random(7)
    # Maria first: a few large soft patches are what actually make a disc read as a moon.
    for (ox, oy, rx, ry) in [(-0.24, 0.26, 0.36, 0.24), (0.30, -0.12, 0.24, 0.32),
                             (0.04, 0.54, 0.26, 0.15), (-0.46, -0.34, 0.18, 0.14)]:
        c.disc(cx + ox * r, cy + oy * r, rx * r, ry * r, M["skin"][2])
    # Then a handful of small craters, flat. Every crater having a lit centre turned the
    # first pass into a sheet of soap bubbles.
    for _ in range(int(r * 0.16)):
        a = rng.random() * 6.28318
        d = rng.random() ** 0.5 * r * 0.9
        x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
        cr = max(1, int(rng.random() ** 2.4 * r * 0.09) + 1)
        c.disc(x, y, cr, cr, M["skin"][2])
        c.disc(x + cr * 0.45, y + cr * 0.45, max(1, cr * 0.4), max(1, cr * 0.4), M["skin"][1])
    # terminator: the lower-right limb falls off, which is what gives the disc volume
    for _ in range(int(r * 22)):
        a = rng.random() * 6.28318
        d = (0.55 + rng.random() * 0.45) * r
        x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
        if (x - cx) + (y - cy) > r * 0.35 and rng.random() < ((x - cx) + (y - cy)) / (r * 1.6):
            c.set(int(x), int(y), M["skin"][2])


def candle(c, x, ybase, h=9, ramp=None):
    ramp = ramp or E["fire"]
    c.rect(x - 1, ybase - h, x + 1, ybase, M["linen"][1])
    c.vline(x - 1, ybase - h, ybase, M["linen"][0])
    c.set(x + 1, ybase, OCC)
    c.radial(x, ybase - h - 3, 7, 8, [ramp[0], ramp[1], ramp[2], ramp[3], None])
    c.set(x, ybase - h - 2, ramp[0])
    c.set(x, ybase - h - 3, ramp[0])


def stone_floor(c, y0, y1, tile=32, seed=3, ramp=None):
    """Top-down flagstones: the camera the game is actually played at."""
    ramp = ramp or M["stone"]
    rng = random.Random(seed)
    c.rect(0, y0, c.w - 1, y1, ramp[2])
    for ty in range(int(y0) // tile, int(y1) // tile + 1):
        for tx in range(0, c.w // tile + 1):
            x0 = tx * tile
            yy0 = ty * tile
            v = rng.random()
            fill = ramp[2] if v < 0.72 else ramp[3]
            c.rect(x0 + 1, max(y0, yy0 + 1), x0 + tile - 2, min(y1, yy0 + tile - 2), fill)
            # grout
            c.hline(x0, x0 + tile - 1, yy0, ramp[4])
            c.vline(x0, yy0, yy0 + tile - 1, ramp[4])
            # a key-facing bevel on the upper-left of each stone
            c.hline(x0 + 1, x0 + tile - 2, yy0 + 1, ramp[1])
            c.vline(x0 + 1, yy0 + 1, yy0 + tile - 2, ramp[1])
            for _ in range(6):
                c.set(x0 + rng.randrange(2, tile - 2), yy0 + rng.randrange(2, tile - 2),
                      ramp[3] if rng.random() < 0.5 else ramp[1])
    c.rect(0, 0, c.w - 1, y0 - 1, ramp[4]) if y0 > 0 else None


# --- the twelve elements as sigils -------------------------------------------
# Drawn rather than generated: the element set is the game's spine, and a procedural glyph
# would make Fire and Lightning read as the same shape at 7px.
SIGILS = [
    ("fire", ["...#...", "..###..", ".##.##.", "##...##", "##.#.##", ".##.##.", "..###.."]),
    ("ice", ["#..#..#", ".#.#.#.", "..###..", "#######", "..###..", ".#.#.#.", "#..#..#"]),
    ("arcane", ["..###..", ".#...#.", "#..#..#", "#.###.#", "#..#..#", ".#...#.", "..###.."]),
    ("darkness", ["..###..", ".#####.", "###.###", "##...##", "###.###", ".#####.", "..###.."]),
    ("light", ["#..#..#", ".#.#.#.", "..###..", "##.#.##", "..###..", ".#.#.#.", "#..#..#"]),
    ("grass", ["...#...", "..#.#..", ".#.#.#.", "#..#..#", "...#...", "..###..", ".#####."]),
    ("earth", [".......", ".#####.", "##...##", "#.....#", "##...##", ".#####.", "......."]),
    ("wind", [".......", "#####..", ".....#.", "######.", ".....#.", "#####..", "......."]),
    ("lightning", ["...##..", "..##...", ".##....", "#####..", "...##..", "..##...", ".##...."]),
    ("poison", ["..###..", ".#####.", "##.#.##", "#######", ".#####.", "..#.#..", ".#...#."]),
    ("metal", ["#..#..#", "#..#..#", "#######", "...#...", "#######", "#..#..#", "#..#..#"]),
    ("water", ["...#...", "..###..", ".#####.", "#######", "##...##", ".#####.", "..###.."]),
]


def sigil(c, cx, cy, idx, scale=1, glow=True, bright=1):
    name, grid = SIGILS[idx % len(SIGILS)]
    ramp = E[name]
    n = len(grid)
    x0 = int(cx - (n * scale) // 2)
    y0 = int(cy - (n * scale) // 2)
    if glow:
        c.radial(cx, cy, n * scale * 0.95, n * scale * 0.95,
                 [ramp[2], ramp[3], ramp[3], None])
    body = ramp[bright]
    for ry, row in enumerate(grid):
        for rx, cell in enumerate(row):
            if cell != "#":
                continue
            for sy in range(scale):
                for sx in range(scale):
                    c.set(x0 + rx * scale + sx, y0 + ry * scale + sy, body)
    # white-hot centre, per the contract: identity lives in mid and edge, not the core
    c.set(int(cx), int(cy), ramp[0])
    return name


# --- macro props -------------------------------------------------------------
# A skull outline in normalised (x, y) from its centre. Drawn as a profile rather than a
# circle because the first pass proved a disc with two dots on it reads as an emoji: the
# cranium has to be wider than the jaw, the temple has to pinch, and the chin has to taper.
SKULL_OUTLINE = [
    (0.00, -1.00), (0.34, -0.95), (0.60, -0.78), (0.76, -0.50), (0.80, -0.18),
    (0.74, 0.06), (0.60, 0.20), (0.50, 0.30), (0.46, 0.46), (0.38, 0.62),
    (0.24, 0.74), (0.00, 0.78), (-0.24, 0.74), (-0.38, 0.62), (-0.46, 0.46),
    (-0.50, 0.30), (-0.60, 0.20), (-0.74, 0.06), (-0.80, -0.18), (-0.76, -0.50),
    (-0.60, -0.78), (-0.34, -0.95),
]

# The socket is an angled pentagon, not a circle - the tilt is what makes a skull look
# hostile rather than surprised.
SOCKET = [(-0.46, -0.30), (-0.06, -0.20), (0.02, 0.10), (-0.20, 0.24), (-0.48, 0.08)]


def _shape(cx, cy, w, h, pts, dx=0.0, dy=0.0, k=1.0, mirror=False):
    out = []
    for (x, y) in pts:
        sx = -x if mirror else x
        out.append((cx + (sx * k + dx) * w * 0.5, cy + (y * k + dy) * h * 0.5))
    return out


# Orbital socket in socket-local units. Irregular and leaning outward, because a circle
# reads as a goggle lens - which is exactly what the first pass produced.
SOCKET_SHAPE = [(-1.00, -0.50), (-0.30, -1.00), (0.70, -0.80), (1.00, -0.05),
                (0.60, 0.80), (-0.30, 1.00), (-0.90, 0.40)]


def _local(cx, cy, rx, ry, pts, mirror=False):
    return [(cx + (-p[0] if mirror else p[0]) * rx, cy + p[1] * ry) for p in pts]


def skull(c, cx, cy, w, h, ramp=None, socket=None, teeth=True):
    """A front-facing skull sized to fill whatever it is given, from prop size up to hero size.

    Everything is measured in half-width/half-height like SKULL_OUTLINE is. The first pass
    mixed the two conventions, so every feature came out at twice its intended size and the
    sockets ended up bigger than the eye ridges that were supposed to contain them.
    """
    ramp = ramp or M["skin"]
    sock = socket if socket is not None else E["arcane"]
    hw, hh = w * 0.5, h * 0.5
    body = _shape(cx, cy, w, h, SKULL_OUTLINE)
    c.poly_shade(body, [ramp[0], ramp[1], ramp[2], ramp[3], ramp[4]],
                 ang=-0.7854, bias=0.05, gamma=1.4)
    # brow shelf: the band of shade under the cranium that turns a dome into a face
    c.poly_shade([(cx - hw * 0.60, cy - hh * 0.30), (cx + hw * 0.60, cy - hh * 0.30),
                  (cx + hw * 0.66, cy - hh * 0.06), (cx - hw * 0.66, cy - hh * 0.06)],
                 [ramp[2], ramp[3]], ang=1.5708)
    # temple hollows
    c.poly_shade(_shape(cx, cy, w, h, [(-0.79, -0.30), (-0.60, -0.16), (-0.58, 0.16),
                                       (-0.73, 0.10)]), [ramp[2], ramp[3]], ang=0.0)
    c.poly_shade(_shape(cx, cy, w, h, [(0.79, -0.30), (0.60, -0.16), (0.58, 0.16),
                                       (0.73, 0.10)]), [ramp[3], ramp[4]], ang=0.0)
    # sockets
    for sgn in (-1, 1):
        ex = cx + sgn * hw * 0.34
        ey = cy - hh * 0.02
        rx, ry = hw * 0.21, hh * 0.20
        mir = sgn < 0
        c.poly(_local(ex, ey, rx, ry, SOCKET_SHAPE, mir), OCC)
        # an ember sunk in the socket, not a ring painted round its rim: the eye has to look
        # lit from inside the skull or it reads as jewellery
        if sock:
            c.radial(ex + sgn * rx * 0.10, ey + ry * 0.24, rx * 0.58, ry * 0.52,
                     list(sock) + [None])
        # orbital rim catching the key on the upper-left of each socket
        for p in _local(ex, ey, rx * 1.06, ry * 1.06, SOCKET_SHAPE, mir):
            if p[0] < ex + rx * 0.3 and p[1] < ey:
                c.disc(p[0], p[1], 1, 1, ramp[0])
    # cheekbones
    c.poly(_shape(cx, cy, w, h, [(-0.62, 0.18), (-0.30, 0.26), (-0.32, 0.38),
                                 (-0.56, 0.34)]), ramp[1])
    c.poly(_shape(cx, cy, w, h, [(0.62, 0.18), (0.30, 0.26), (0.32, 0.38),
                                 (0.56, 0.34)]), ramp[4])
    # nasal aperture: narrow at the bridge, flaring at the base
    c.poly([(cx, cy + hh * 0.10), (cx - hw * 0.045, cy + hh * 0.26),
            (cx - hw * 0.13, cy + hh * 0.42), (cx, cy + hh * 0.38),
            (cx + hw * 0.13, cy + hh * 0.42), (cx + hw * 0.045, cy + hh * 0.26)], OCC)
    if teeth:
        c.poly_shade([(cx - hw * 0.34, cy + hh * 0.50), (cx + hw * 0.34, cy + hh * 0.50),
                      (cx + hw * 0.30, cy + hh * 0.76), (cx - hw * 0.30, cy + hh * 0.76)],
                     [ramp[1], ramp[2], ramp[3]], ang=-0.7854)
        c.hline(cx - hw * 0.33, cx + hw * 0.33, cy + hh * 0.63, ramp[4])
        n = max(3, int(hw * 0.16))
        step = (hw * 0.66) / n
        for i in range(n + 1):
            c.vline(int(cx - hw * 0.33 + i * step), cy + hh * 0.50, cy + hh * 0.76, ramp[4])
        c.hline(cx - hw * 0.31, cx + hw * 0.31, cy + hh * 0.77, OCC)
    # cranial suture: the detail that says bone rather than mask
    c.line(cx - hw * 0.03, cy - hh * 0.94, cx - hw * 0.10, cy - hh * 0.44, ramp[3])
    c.line(cx - hw * 0.10, cy - hh * 0.44, cx - hw * 0.42, cy - hh * 0.36, ramp[3])
    c.line(cx - hw * 0.10, cy - hh * 0.44, cx + hw * 0.32, cy - hh * 0.40, ramp[3])


def brazier(c, x, ybase, h=18, ramp=None):
    ramp = ramp or E["fire"]
    c.rect(x - 2, ybase - h, x + 2, ybase, M["steel"][3])
    c.vline(x - 2, ybase - h, ybase, M["steel"][2])
    c.rect(x - 7, ybase - h - 6, x + 7, ybase - h, M["steel"][3])
    c.hline(x - 7, x + 7, ybase - h - 6, M["steel"][1])
    c.radial(x, ybase - h - 10, 13, 16, [ramp[0], ramp[1], ramp[2], ramp[3], None])
