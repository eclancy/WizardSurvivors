# -*- coding: utf-8 -*-
"""The player character at title-screen size.

splashkit.wizard_back is the small, cheap figure: fine at 32-96px, where nothing but the
silhouette survives. It is not enough for a title screen, where the player character is the
largest thing on frame - at that size a cone on a bell with a lollipop beside it reads as a
mascot, which is exactly the note this figure exists to answer.

What makes this one read as a person rather than a shape, in the order the eye takes it:

1. **The staff is taller than the wizard**, and its head is a forked claw cradling the orb
   rather than a ball on a stick. It sets the top of the silhouette, so it is read first.
2. **Proportion.** The figure is 34 units tall and 13 wide at the hem - roughly 2.6:1. The
   first attempt was nearer 2:1 with a brim wider than the body, and squat plus symmetrical
   is the whole recipe for a chess piece.
3. **A shoulder mantle layered over the robe**, with a scalloped hem. One garment reads as a
   cone; two overlapping garments read as a person wearing clothes.
4. **An outer cloak swept off-axis**, so nothing about the figure is mirror-symmetrical.
5. **Value.** The figure is mostly base, shade and deep. It is night and there is no key on
   this side of him, so what separates him from the ground is the bone rim on one edge and
   the orb bouncing cyan onto the other - not a bright blue fill. Lighting the whole robe was
   the other half of why the first attempt looked like a mascot.
6. **A hand on the staff.** Small, but the only thing on frame at human scale, so it is what
   sets the scale of everything else in the composition.

The figure is drawn onto its own transparent layer and blitted, rather than straight onto the
scene. Both edge passes below decide where the silhouette ends by testing alpha, and on an
opaque scene canvas every pixel is opaque - so drawn in place they are silently no-ops, which
is exactly what happened on the first attempt and why it had no rim at all.

Everything is measured in u = h/32, but the drawing runs past 32u: h is the height of the
body to the shoulders, and the hat and staff are built above it. Spends no colour the palette
does not already define.
"""

import math

import bonelight as bl
import raster

M = bl.MATERIALS
E = bl.ELEMENTS
OCC = bl.OCC
RIM = bl.RIM


def _wave(x0, x1, y, amp, n, phase=0.0):
    """A hem: points along a line with a shallow wave, so cloth ends in cloth, not a rule."""
    pts = []
    steps = max(2, int(n))
    for i in range(steps + 1):
        t = i / float(steps)
        pts.append((x0 + (x1 - x0) * t, y + math.sin(phase + t * 6.28318 * 1.5) * amp))
    return pts


def _rim(c, x0, x1, y0, y1, color):
    """1px bone-white on the key-facing edge - the contract's only separation device."""
    for y in range(int(y0), int(y1) + 1):
        for x in range(int(x0), int(x1) + 1):
            if c.get(x, y)[3] and not c.get(x - 1, y)[3]:
                c.set(x - 1, y, color)
                break


def _bounce(c, x0, x1, y0, y1, ramp):
    """Cyan bounce from the orb along the staff-facing edge of the figure.

    The orb is the only light source standing next to him, so it has to land on him. Without
    this the staff reads as a prop held near the figure rather than as the thing lighting it.
    """
    for y in range(int(y0), int(y1) + 1):
        run = 0
        for x in range(int(x0), int(x1) + 1):
            if c.get(x, y)[3] and not c.get(x - 1, y)[3]:
                run = 1
            elif run:
                run += 1
            if run:
                if run <= 2:
                    c.set(x, y, ramp[2] if (x + y) % 3 else ramp[1])
                elif run <= 4 and (x + y * 2) % 3 == 0:
                    c.set(x, y, ramp[3])
                if run > 4:
                    break


def _grip_y(yb, u):
    """Where the hand meets the staff. Both halves of the arm key off this one number."""
    return yb - 16.2 * u


def _capsule(c, x0, y0, x1, y1, r0, r1, color):
    """A tapering rounded stroke. Anatomy has no square corners, and at this size a polygon
    with four right angles is what makes a finger read as a brick."""
    steps = max(2, int(math.hypot(x1 - x0, y1 - y0)) + 1)
    for i in range(steps + 1):
        t = i / float(steps)
        rr = max(0.7, r0 + (r1 - r0) * t)
        c.disc(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, rr, rr, color)


def _arm(c, cx, yb, u, sx, r):
    """Forearm out of the body, a flared robe cuff at the wrist, and the back of the hand.

    All of it goes down BEFORE the staff. He is holding the shaft, not holding it up in front
    of himself: the arm passes behind it and only the fingertips come round the near side.
    Drawing the whole hand over the staff put his fist in front of a pole he is meant to be
    gripping, which is why it looked stuck on.
    """
    gy = _grip_y(yb, u)
    c.poly_shade([(cx - 1.8 * u, gy - 3.0 * u), (sx + 3.0 * u, gy - 2.1 * u),
                  (sx + 3.2 * u, gy + 1.9 * u), (cx - 1.6 * u, gy + 1.4 * u)],
                 [r[2], r[3], r[4]], ang=-0.7854, gamma=0.62)
    # cuff, flaring toward the wrist. Eight points rather than four so the flare curves.
    cuff = [(sx + 1.9 * u, gy - 2.0 * u), (sx + 2.8 * u, gy - 2.3 * u),
            (sx + 3.4 * u, gy - 1.9 * u), (sx + 3.5 * u, gy + 1.4 * u),
            (sx + 2.9 * u, gy + 2.1 * u), (sx + 2.0 * u, gy + 2.0 * u),
            (sx + 1.7 * u, gy + 1.2 * u), (sx + 1.7 * u, gy - 1.4 * u)]
    c.poly_shade(cuff, [r[1], r[2], r[3], r[4]], ang=-0.7854, gamma=1.0)
    c.line(sx + 2.0 * u, gy - 2.1 * u, sx + 3.3 * u, gy - 1.9 * u, r[1])
    c.line(sx + 1.7 * u, gy + 1.3 * u, sx + 2.9 * u, gy + 2.1 * u, OCC)
    # the sleeve opening: the hand comes out of a hole, so the hole reads as dark
    c.poly([(sx + 1.7 * u, gy - 1.7 * u), (sx + 2.1 * u, gy - 1.9 * u),
            (sx + 2.0 * u, gy + 1.8 * u), (sx + 1.6 * u, gy + 1.5 * u)], OCC)
    # back of the hand, behind the shaft
    s = M["skin"]
    c.poly([(sx + 0.5 * u, gy - 1.4 * u), (sx + 1.5 * u, gy - 1.6 * u),
            (sx + 2.0 * u, gy - 1.1 * u), (sx + 2.1 * u, gy + 0.9 * u),
            (sx + 1.6 * u, gy + 1.5 * u), (sx + 0.7 * u, gy + 1.4 * u),
            (sx + 0.4 * u, gy + 0.6 * u), (sx + 0.4 * u, gy - 0.8 * u)], s[3])
    c.line(sx + 0.5 * u, gy - 1.3 * u, sx + 1.5 * u, gy - 1.5 * u, s[2])


def _fingers(c, yb, u, sx, w):
    """Only what comes round the near side of the shaft: three fingertips and a thumb.

    Kept deliberately small: the fist should be about as wide as the staff, not wider. Tones
    are the same skin ramp the nose is lit with, one step down for the back of the hand - the
    two are the only bare skin on the figure and they have to belong to the same person.
    """
    s = M["skin"]
    gy = _grip_y(yb, u)
    # Fingers are a tone up from the back of the hand: they are the side of the fist facing
    # the orb, and at this size that tonal step is the only thing separating them from it.
    for i in range(3):
        fy = gy - 1.15 * u + i * 1.15 * u
        _capsule(c, sx + 1.5 * u, fy - 0.15 * u, sx - 0.35 * u, fy,
                 0.5 * u, 0.42 * u, OCC)
        _capsule(c, sx + 1.5 * u, fy - 0.30 * u, sx - 0.35 * u, fy - 0.15 * u,
                 0.42 * u, 0.34 * u, s[2])
        c.disc(sx - 0.25 * u, fy - 0.45 * u, 0.3 * u, 0.26 * u, s[1])
    # thumb, coming over the shaft from behind, shorter than the fingers
    _capsule(c, sx + 1.8 * u, gy - 1.85 * u, sx + 0.5 * u, gy - 1.45 * u,
             0.5 * u, 0.4 * u, OCC)
    _capsule(c, sx + 1.8 * u, gy - 2.0 * u, sx + 0.5 * u, gy - 1.6 * u,
             0.42 * u, 0.32 * u, s[2])


def wizard_hero(c, cx, yb, h=150, robe="wool", staff_ramp=None, cast=(1.5, 0.5)):
    """Draw the figure from behind with its feet on yb. h scales the whole construction."""
    u = h / 32.0
    r = M[robe]
    ramp = staff_ramp or E["arcane"]
    w = max(1, int(round(1.3 * u)))

    # the cast shadow belongs to the ground, so it goes straight onto the scene
    c.poly([(cx - 7 * u, yb), (cx + 7 * u, yb),
            (cx + 7 * u + 12 * u * cast[0], yb + 4 * u * cast[1]),
            (cx - 2 * u + 12 * u * cast[0], yb + 5 * u * cast[1])], OCC)
    c.disc(cx, yb + 0.4 * u, 8.5 * u, 2.2 * u, OCC)

    # everything else onto a transparent layer, so the rim and bounce have an edge to find
    scene, ox, oy = c, int(cx - 16 * u), int(yb - 46 * u)
    c = raster.Canvas(int(34 * u), int(50 * u))
    cx, yb = cx - ox, yb - oy
    sx = cx - 11.6 * u                   # staff axis, clear of the body on the orb-lit side

    # --- outer cloak, swept right --------------------------------------------
    cloak = ([(cx - 5.6 * u, yb - 20.5 * u), (cx + 5.6 * u, yb - 20.5 * u),
              (cx + 8.5 * u, yb - 13 * u), (cx + 12 * u, yb - 5.5 * u),
              (cx + 13 * u, yb - 0.5 * u)]
             + _wave(cx + 13 * u, cx - 9.5 * u, yb + 0.5 * u, 1.0 * u, 9, 0.7)
             + [(cx - 10 * u, yb - 5 * u), (cx - 7.6 * u, yb - 14 * u)])
    c.poly_shade(cloak, [r[4], OCC], ang=-0.5, gamma=1.2)
    for (fx, fy) in [(8.0, -12), (10.4, -7), (11.8, -2.5)]:
        c.line(cx + (fx - 3.0) * u, yb - (abs(fy) + 5) * u, cx + fx * u, yb + fy * u + 1, OCC)

    # --- robe -----------------------------------------------------------------
    robe_pts = ([(cx - 4.6 * u, yb - 20 * u), (cx + 4.6 * u, yb - 20 * u),
                 (cx + 4.2 * u, yb - 11 * u), (cx + 6.5 * u, yb - 1 * u)]
                + _wave(cx + 6.5 * u, cx - 6.5 * u, yb, 0.7 * u, 7, 2.1)
                + [(cx - 4.2 * u, yb - 11 * u)])
    c.poly_shade(robe_pts, [r[2], r[3], r[4], OCC], ang=-0.7854, bias=0.0, gamma=0.48)
    for (bx, tx) in [(-4.4, -1.8), (-2.0, -0.8), (1.0, 0.5), (3.6, 1.8), (5.4, 2.8)]:
        c.line(cx + bx * u, yb - 0.5 * u, cx + tx * u, yb - 17 * u, r[4])
    c.poly(_wave(cx + 6.5 * u, cx - 6.5 * u, yb + 0.4 * u, 0.7 * u, 7, 2.1)
           + [(cx - 6.5 * u, yb + 2 * u), (cx + 6.5 * u, yb + 2 * u)], OCC)
    # sash at the waist, the one warm note on the figure
    c.poly([(cx - 4.3 * u, yb - 12.2 * u), (cx + 4.3 * u, yb - 12.2 * u),
            (cx + 4.4 * u, yb - 11.0 * u), (cx - 4.4 * u, yb - 11.0 * u)], M["red"][3])
    c.hline(cx - 4.3 * u, cx - 1.0 * u, yb - 12.2 * u, M["red"][2])
    c.hline(cx - 4.4 * u, cx + 4.4 * u, yb - 10.4 * u, OCC)

    # --- shoulder mantle -------------------------------------------------------
    mant = [(cx - 3.0 * u, yb - 24.4 * u), (cx + 3.0 * u, yb - 24.4 * u),
            (cx + 5.6 * u, yb - 21 * u), (cx + 7.0 * u, yb - 15.4 * u)]
    for i in range(7):                                   # scalloped hem
        t = i / 6.0
        mant.append((cx + (7.0 - 14.0 * t) * u, yb - 15.4 * u + (1.2 * u if i % 2 else 0)))
    mant += [(cx - 7.0 * u, yb - 15.4 * u), (cx - 5.6 * u, yb - 21 * u)]
    c.poly_shade(mant, [r[2], r[3], r[4], OCC], ang=-0.7854, bias=0.0, gamma=0.55)
    for i in range(7):
        c.set(int(cx + (6.7 - 13.4 * (i / 6.0)) * u),
              int(yb - 15.4 * u + (1.2 * u if i % 2 else 0) + 1), OCC)
    c.disc(cx, yb - 23.2 * u, 1.1 * u, 0.8 * u, M["gold"][2])
    c.disc(cx - 0.2 * u, yb - 23.4 * u, 0.5 * u, 0.4 * u, M["gold"][1])

    # The arm goes down here, before the face and the beard, so the beard hangs over the
    # inside end of it and the sleeve reads as protruding out from behind the beard rather
    # than as crossing in front of the chest.
    _arm(c, cx, yb, u, sx, r)

    # --- face and beard ------------------------------------------------------------
    # He was drawn from behind with a dark void under the brim, and read front-on anyway: the
    # pale scalloped mantle beneath the hat was doing a convincing impression of a beard. So
    # he is front-facing now and the beard is real. The face stays deep in the brim shadow -
    # only the nose and the two eye glints catch anything - which keeps him ominous rather
    # than friendly at a size where a whole rendered face would read as a portrait.
    s = M["skin"]
    # the face plane, then the brow band the brim throws across it
    c.poly([(cx - 2.9 * u, yb - 25.6 * u), (cx + 2.9 * u, yb - 25.6 * u),
            (cx + 2.7 * u, yb - 22.6 * u), (cx + 2.0 * u, yb - 21.6 * u),
            (cx - 2.0 * u, yb - 21.6 * u), (cx - 2.7 * u, yb - 22.6 * u)], s[3])
    c.poly([(cx - 2.9 * u, yb - 25.6 * u), (cx - 1.5 * u, yb - 25.6 * u),
            (cx - 1.7 * u, yb - 21.8 * u), (cx - 2.7 * u, yb - 22.6 * u)], s[2])
    c.poly([(cx + 1.6 * u, yb - 25.6 * u), (cx + 2.9 * u, yb - 25.6 * u),
            (cx + 2.7 * u, yb - 22.6 * u), (cx + 1.8 * u, yb - 22.0 * u)], s[4])
    c.poly([(cx - 2.9 * u, yb - 25.4 * u), (cx + 2.9 * u, yb - 25.4 * u),
            (cx + 2.8 * u, yb - 24.2 * u), (cx - 2.8 * u, yb - 24.2 * u)], OCC)
    # eyes at the lower edge of that band, so the brim shadow reads as brow
    for k in (-1, 1):
        c.disc(cx + k * 1.35 * u, yb - 24.1 * u, 0.55 * u, 0.45 * u, OCC)
        c.disc(cx + k * 1.35 * u, yb - 24.1 * u, 0.3 * u, 0.28 * u, ramp[0])
    # the nose: the one plane of him the light actually finds, and the tone the hand matches
    c.poly([(cx - 0.35 * u, yb - 23.6 * u), (cx + 0.35 * u, yb - 23.6 * u),
            (cx + 0.65 * u, yb - 22.3 * u), (cx - 0.7 * u, yb - 22.3 * u)], s[2])
    c.poly([(cx - 0.35 * u, yb - 23.6 * u), (cx + 0.0 * u, yb - 23.6 * u),
            (cx + 0.05 * u, yb - 22.4 * u), (cx - 0.7 * u, yb - 22.3 * u)], s[1])
    c.hline(cx - 0.7 * u, cx + 0.65 * u, yb - 22.2 * u, OCC)
    # moustache: small. Built wide and bright it was a white bib under the hat and the whole
    # face vanished behind it.
    c.poly([(cx - 1.7 * u, yb - 21.9 * u), (cx + 1.7 * u, yb - 21.9 * u),
            (cx + 1.2 * u, yb - 21.1 * u), (cx - 1.2 * u, yb - 21.1 * u)], s[2])
    c.hline(cx - 1.2 * u, cx + 1.2 * u, yb - 21.0 * u, s[4])
    # beard, hanging over the mantle and over the inside end of the sleeve
    beard = ([(cx - 2.3 * u, yb - 21.4 * u), (cx + 2.3 * u, yb - 21.4 * u),
              (cx + 3.1 * u, yb - 19.0 * u), (cx + 3.6 * u, yb - 17.0 * u)]
             + _wave(cx + 3.3 * u, cx - 3.3 * u, yb - 15.2 * u, 1.0 * u, 8, 1.4)
             + [(cx - 3.6 * u, yb - 17.0 * u), (cx - 3.1 * u, yb - 19.0 * u)])
    c.poly_shade(beard, [s[2], s[3], s[4], OCC], ang=-0.7854, bias=0.0, gamma=0.72)
    for bx in (-2.4, -1.2, 0.2, 1.4, 2.5):                # strands
        c.line(cx + bx * u * 0.6, yb - 21.0 * u, cx + bx * u, yb - 15.8 * u, s[4])

    # --- the hat, over the top of all of it ------------------------------------------
    brim = [(cx - 8.6 * u, yb - 25.4 * u), (cx - 5.2 * u, yb - 27.2 * u),
            (cx, yb - 27.7 * u), (cx + 5.2 * u, yb - 27.2 * u), (cx + 8.6 * u, yb - 25.4 * u),
            (cx + 6.6 * u, yb - 24.3 * u), (cx + 3.0 * u, yb - 24.8 * u),
            (cx - 3.0 * u, yb - 24.8 * u), (cx - 6.6 * u, yb - 24.3 * u)]
    c.poly_shade(brim, [r[2], r[3], r[4], OCC], ang=-0.7854, bias=0.0, gamma=0.60)
    # the brim casts. Without this the hat sits on the shoulders instead of over a head.
    c.poly([(cx - 3.0 * u, yb - 25.0 * u), (cx + 3.0 * u, yb - 25.0 * u),
            (cx + 2.7 * u, yb - 24.0 * u), (cx - 2.7 * u, yb - 24.0 * u)], OCC)
    cone = [(cx - 4.2 * u, yb - 27.3 * u), (cx + 4.2 * u, yb - 27.3 * u),
            (cx + 2.4 * u, yb - 30.6 * u), (cx + 1.0 * u, yb - 33.0 * u),
            (cx - 1.8 * u, yb - 34.8 * u), (cx - 3.6 * u, yb - 34.0 * u),
            (cx - 1.6 * u, yb - 32.4 * u), (cx - 2.0 * u, yb - 29.8 * u)]
    c.poly_shade(cone, [r[2], r[3], r[4], OCC], ang=-0.7854, bias=-0.02, gamma=0.52)
    c.poly([(cx - 4.1 * u, yb - 28.0 * u), (cx + 4.1 * u, yb - 28.0 * u),
            (cx + 3.7 * u, yb - 27.1 * u), (cx - 3.7 * u, yb - 27.1 * u)], r[4])
    c.rect(cx - 1.0 * u, yb - 28.2 * u, cx + 0.3 * u, yb - 26.9 * u, M["gold"][2])
    c.rect(cx - 0.8 * u, yb - 27.9 * u, cx - 0.0 * u, yb - 27.3 * u, M["gold"][1])

    # Edge light, on the figure only. Running these after the staff rimmed the staff - it is
    # the leftmost lit thing in most rows - and the result read as a glowing rod.
    _bounce(c, 1, int(cx + 14 * u), int(yb - 36 * u), int(yb), ramp)
    _rim(c, 2, int(cx + 15 * u), int(yb - 36 * u), int(yb), RIM)

    # --- staff ------------------------------------------------------------------
    c.rect(sx, yb - 34 * u, sx + w, yb - 0.5 * u, M["gold"][2])
    c.vline(int(sx), yb - 34 * u, yb - 0.5 * u, M["gold"][1])
    c.vline(int(sx + w), yb - 34 * u, yb - 0.5 * u, M["gold"][3])
    # Two bindings, clear of the grip height. The leather wrap used to sit at exactly the
    # height of the hand, so its stripes ran straight across the fingers and the whole thing
    # read as a candy cane held in a fist.
    for yy in (yb - 21.0 * u, yb - 10.5 * u):
        c.hline(sx - 0.5 * u, sx + w + 0.5 * u, yy, M["red"][3])
        c.hline(sx - 0.5 * u, sx + w + 0.5 * u, yy + 1, M["red"][2])
        c.hline(sx - 0.5 * u, sx + w + 0.5 * u, yy + max(2, int(0.8 * u)), OCC)
    # bloom first, then the metal over it, so the claw is not swallowed by its own light
    c.radial(sx + w * 0.5, yb - 37 * u, 5.2 * u, 5.2 * u, [ramp[2], ramp[3], ramp[3], None])
    for sgn in (-1, 1):                                  # forked claw cradling the orb
        c.poly([(sx + w * 0.5, yb - 33.4 * u),
                (sx + w * 0.5 + sgn * 3.2 * u, yb - 35.6 * u),
                (sx + w * 0.5 + sgn * 2.6 * u, yb - 38.6 * u),
                (sx + w * 0.5 + sgn * 1.5 * u, yb - 37.8 * u),
                (sx + w * 0.5 + sgn * 2.0 * u, yb - 35.8 * u),
                (sx + w * 0.5, yb - 34.6 * u)], M["gold"][2])
        c.line(sx + w * 0.5 + sgn * 3.0 * u, yb - 35.6 * u,
               sx + w * 0.5 + sgn * 2.4 * u, yb - 38.4 * u,
               M["gold"][1] if sgn < 0 else M["gold"][3])
    c.radial(sx + w * 0.5, yb - 37 * u, 2.0 * u, 2.0 * u,
             [ramp[0], ramp[1], ramp[2], ramp[3]])
    for (mx, my, mc) in [(-4.2, -40.5, 1), (2.6, -41.8, 2), (-1.4, -43.4, 2), (4.4, -38.6, 3),
                         (-5.4, -35.6, 3)]:
        c.disc(sx + mx * u, yb + my * u, max(1, 0.6 * u), max(1, 0.6 * u), ramp[mc])

    # last of all, the few fingertips that come round the near side of the shaft
    _fingers(c, yb, u, sx, w)

    scene.blit(c.img, ox, oy)
