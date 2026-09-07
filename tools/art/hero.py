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
import random

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


def _inside(skip, x, y):
    """True if (x, y) falls in a rect the edge passes must leave alone. See _hood."""
    return skip is not None and skip[0] <= x <= skip[2] and skip[1] <= y <= skip[3]


def _rim(c, x0, x1, y0, y1, color, skip=None):
    """1px bone-white on the key-facing edge - the contract's only separation device."""
    for y in range(int(y0), int(y1) + 1):
        for x in range(int(x0), int(x1) + 1):
            if c.get(x, y)[3] and not c.get(x - 1, y)[3]:
                if not _inside(skip, x, y):
                    c.set(x - 1, y, color)
                break


def _bounce(c, x0, x1, y0, y1, ramp, skip=None):
    """Cyan bounce from the orb along the staff-facing edge of the figure.

    The orb is the only light source standing next to him, so it has to land on him. Without
    this the staff reads as a prop held near the figure rather than as the thing lighting it.
    """
    for y in range(int(y0), int(y1) + 1):
        run = 0
        for x in range(int(x0), int(x1) + 1):
            if c.get(x, y)[3] and not c.get(x - 1, y)[3]:
                if _inside(skip, x, y):
                    break
                run = 1
            elif run:
                run += 1
            if run:
                if run <= 2:
                    c.set(x, y, ramp[3] if (x + y) % 3 else ramp[2])
                elif run <= 4 and (x + y * 2) % 3 == 0:
                    c.set(x, y, ramp[3])
                if run > 4:
                    break


def _underlight(c, x0, x1, y0, y1, ramp):
    """Light thrown up onto the figure from the ward circle he is standing in.

    The horizontal twin of _bounce: it walks each column from the bottom up, finds the first
    lit pixel of the silhouette, and tints the few above it. Without it the circle is a bright
    thing on the floor that the man standing in it is somehow unaware of - the give-away that
    a light source has been drawn rather than lit with.
    """
    for x in range(int(x0), int(x1) + 1):
        run = 0
        for y in range(int(y1), int(y0) - 1, -1):
            if c.get(x, y)[3] and not c.get(x, y + 1)[3]:
                run = 1
            elif run:
                run += 1
            if run:
                if run <= 3:
                    c.set(x, y, ramp[1] if (x + y) % 4 else ramp[2])
                elif run <= 6:
                    if (x + y * 2) % 2 == 0:
                        c.set(x, y, ramp[3])
                elif run <= 11 and (x * 3 + y) % 4 == 0:
                    c.set(x, y, ramp[3])
                if run > 11:
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
    # Three points, not two. The first version was a single straight tapered quad from the
    # body to the wrist: no joint anywhere in it and four units thick, so it read as a plank
    # laid across him. The upper arm hangs down and out from the shoulder, the elbow bends,
    # and the forearm comes back up to the grip.
    shoulder = (cx - 4.4 * u, yb - 19.4 * u)
    elbow = (cx - 7.9 * u, yb - 14.4 * u)
    wrist = (sx + 1.9 * u, gy)
    for (a, b, r0, r1) in [(shoulder, elbow, 1.2 * u, 0.88 * u),
                           (elbow, wrist, 0.88 * u, 0.68 * u)]:
        _capsule(c, a[0], a[1], b[0], b[1], r0 + 0.25 * u, r1 + 0.25 * u, OCC)
        _capsule(c, a[0], a[1], b[0], b[1], r0, r1, r[4])
        _capsule(c, a[0] - 0.3 * u, a[1] - 0.3 * u, b[0] - 0.3 * u, b[1] - 0.3 * u,
                 r0 * 0.6, r1 * 0.6, r[3])
    # the joint itself, a shade wider than either bone, so the bend reads as a bend
    c.disc(elbow[0], elbow[1], 1.05 * u, 1.05 * u, r[4])
    c.disc(elbow[0] - 0.3 * u, elbow[1] - 0.32 * u, 0.6 * u, 0.6 * u, r[3])
    # cuff: a short flare square across the end of the forearm rather than square to the frame
    dx, dy = wrist[0] - elbow[0], wrist[1] - elbow[1]
    dl = max(0.001, math.hypot(dx, dy))
    dx, dy = dx / dl, dy / dl
    _capsule(c, wrist[0] - dx * 1.7 * u, wrist[1] - dy * 1.7 * u,
             wrist[0] + dx * 0.2 * u, wrist[1] + dy * 0.2 * u, 1.05 * u, 1.25 * u, r[3])
    _capsule(c, wrist[0] - dx * 1.7 * u - 0.3 * u, wrist[1] - dy * 1.7 * u - 0.3 * u,
             wrist[0] - dx * 0.5 * u - 0.3 * u, wrist[1] - dy * 0.5 * u - 0.3 * u,
             0.62 * u, 0.68 * u, r[2])
    # No filled sleeve opening. The hand overlaps the end of the cuff now, so all the seam
    # needs is the cuff edge line above - a dark ellipse there read as a hole in his forearm.
    # back of the hand, behind the shaft
    s = M["flesh"]
    c.poly([(sx + 0.15 * u, gy - 1.4 * u), (sx + 1.1 * u, gy - 1.6 * u),
            (sx + 1.5 * u, gy - 1.0 * u), (sx + 1.6 * u, gy + 1.1 * u),
            (sx + 1.1 * u, gy + 1.6 * u), (sx + 0.35 * u, gy + 1.5 * u),
            (sx + 0.05 * u, gy + 0.6 * u), (sx + 0.05 * u, gy - 0.8 * u)], s[3])
    c.line(sx + 0.15 * u, gy - 1.3 * u, sx + 1.1 * u, gy - 1.5 * u, s[2])


def _fingers(c, yb, u, sx, w):
    """Only what comes round the near side of the shaft: four fingertips and a thumb.

    Small, and in `flesh` rather than `skin`. `skin` in this palette is bone - moon-pale and
    cold - and a hand painted out of it reads as a gauntlet or a dead one. `flesh` is the warm
    row added to the contract for exactly this, and next to the gold shaft it is the only warm
    note on the figure besides the sash.
    """
    s = M["flesh"]
    gy = _grip_y(yb, u)
    # Four fingers, shifted left so the fist sits ON the shaft rather than beside it, and
    # each one a different length and thickness: index, middle, ring, little. Drawn to one
    # size they read as a rack of identical tubes rather than as a hand.
    FINGERS = [(0.80, 0.31), (0.96, 0.33), (0.88, 0.30), (0.68, 0.26)]
    for i, (reach, th) in enumerate(FINGERS):
        fy = gy - 1.16 * u + i * 0.78 * u
        near = sx - 0.62 * u * reach
        _capsule(c, sx + 0.9 * u, fy - 0.10 * u, near, fy,
                 th * u, (th - 0.06) * u, OCC)
        _capsule(c, sx + 0.9 * u, fy - 0.22 * u, near, fy - 0.10 * u,
                 (th - 0.07) * u, (th - 0.12) * u, s[2])
        c.disc(near + 0.1 * u, fy - 0.29 * u, 0.17 * u, 0.16 * u, s[1])  # knuckle, ward-lit
    # The thumb clamps down ACROSS the fingers rather than sitting above them. Laid parallel
    # it read as a fourth finger; crossing the stack is what makes the hand read as gripping
    # rather than as resting against the shaft.
    # It stops on the shaft rather than past the far side of it: a thumb long enough to clear
    # the fingers it crosses stops reading as a thumb and starts reading as a fifth finger
    # laid on top of the others.
    _capsule(c, sx + 1.45 * u, gy - 1.95 * u, sx + 0.25 * u, gy - 1.05 * u,
             0.35 * u, 0.27 * u, OCC)
    _capsule(c, sx + 1.45 * u, gy - 2.08 * u, sx + 0.25 * u, gy - 1.18 * u,
             0.28 * u, 0.21 * u, s[2])
    c.disc(sx + 0.30 * u, gy - 1.22 * u, 0.19 * u, 0.18 * u, s[1])


def _hood(c, cx, yb, u, r):
    """The void under the brim - the shadow the face sits in.

    Two things it must not do. It must not take an edge light: the first version was a flat
    OCC quad wider than anything around it, so in those rows it WAS the outer silhouette and
    both edge passes obligingly lit it. A shadow with a bone rim and a gold bounce on it stops
    being a shadow and becomes a black object. Hence the skip rect the caller hands _rim and
    _bounce; keep it around this shape.

    And it must not end on a rule. It used to stop dead on a horizontal line onto the mantle,
    which reads as a painted band rather than as light falling off. Dithering the bottom two
    units through robe deep and shade is what turns the edge into a transition.
    """
    c.poly_shade([(cx - 6.6 * u, yb - 24.5 * u), (cx + 6.6 * u, yb - 24.5 * u),
                  (cx + 5.4 * u, yb - 23.0 * u), (cx + 4.8 * u, yb - 21.0 * u),
                  (cx - 4.8 * u, yb - 21.0 * u), (cx - 5.4 * u, yb - 23.0 * u)],
                 [OCC, OCC, OCC, r[4], r[3]], ang=1.5708, gamma=0.8)


# --- the face -----------------------------------------------------------------------------
#
# Five grand-wizard beards, selected by name. They are genuine alternatives rather than one
# shape with knobs on: the silhouette is the whole design at this size, so a style that only
# differs by a couple of units of width is not a style.
#
# What they all share, and what every one of them learned the hard way:
#
#   * The mass is ONE dithered ramp. Ruled partings down the front were tried at four, eleven
#     and thirteen across, in occlusion and in deep, ragged and even, and at every count they
#     read as ruling on a surface rather than as hair. Length is what turns a streak into a
#     parting - everything here stays under a unit and a half.
#   * No keyline round the outside. A continuous occlusion line on the profile reads as a
#     shield boss hung on his chest. Hair has an edge that breaks up, so the edge is short
#     flicks, each leaving the profile by a quarter to half a unit; at a unit and a half they
#     are spines and the beard is a hedgehog.
#   * Width has a ceiling and the ceiling is the length. Taken out until it is as wide as it is
#     tall, any of these stops being a beard and becomes a ball.
#   * The moustache sits IN the beard's values and carries its own feathering. Pale and smooth,
#     it reads as two slabs laid across the face rather than as the same head of hair.
_BEARD_TOP = -23.60


def _lerp(profile, t):
    t = min(1.0, max(0.0, t))
    for i in range(len(profile) - 1):
        (t0, w0), (t1, w1) = profile[i], profile[i + 1]
        if t <= t1:
            return w0 + (w1 - w0) * (t - t0) / (t1 - t0)
    return profile[-1][1]


def _rows(cx, yb, u, top, bot, profile, skew=None, wob=0.055, n=30):
    """The beard as a stack of spans: (t, y, x_left, x_right).

    Both the outline and the texture come off the same list, so feathering always lands on the
    edge and streaks always land inside it whatever the profile or the sweep is doing.
    """
    out = []
    for i in range(n + 1):
        t = i / float(n)
        w = _lerp(profile, t) * (1.0 + wob * math.cos(t * 9.0)) * u
        y = yb + (top + (bot - top) * t) * u
        dx = skew(t) * u if skew else 0.0
        out.append((t, y, cx + dx - w, cx + dx + w))
    return out


def _rows_poly(rows):
    return ([(xl, y) for (t, y, xl, xr) in rows]
            + [(xr, y) for (t, y, xl, xr) in reversed(rows)])


def _at(rows, t):
    n = len(rows) - 1
    f = min(1.0, max(0.0, t)) * n
    i = min(n - 1, int(f))
    g = f - i
    a, b = rows[i], rows[i + 1]
    return (a[1] + (b[1] - a[1]) * g, a[2] + (b[2] - a[2]) * g, a[3] + (b[3] - a[3]) * g)


def _mass(c, u, rows, sk, rng, under=None, feather=58, streaks=84, lit=0.66):
    pts = _rows_poly(rows)
    c.poly([(x + 0.50 * u, y + 0.50 * u) for (x, y) in pts], OCC)
    c.poly_shade(pts, [sk[2], sk[3], sk[4]], ang=-0.7854, bias=0.0, gamma=0.48)
    for i in range(feather):
        t = 0.03 + rng.random() * 0.95
        y, xl, xr = _at(rows, t)
        sgn = -1 if rng.random() < 0.5 else 1
        x = (xr - 0.25 * u) if sgn > 0 else (xl + 0.25 * u)
        ln = (0.20 + rng.random() * 0.34) * u
        drop = (rng.random() * 0.7 - 0.1) * u
        c.line(x + 1, y + 1, x + sgn * ln + 1, y + drop + 1, OCC)
        c.line(x, y, x + sgn * ln, y + drop, sk[3] if rng.random() < 0.45 else sk[4])
    for i in range(streaks):
        t = 0.05 + rng.random() * 0.90
        y, xl, xr = _at(rows, t)
        x = (xl + xr) * 0.5 + (rng.random() * 2.0 - 1.0) * (xr - xl) * 0.47
        ln = (0.30 + rng.random() * 0.55) * u
        c.line(x, y, x + (rng.random() - 0.5) * 0.4 * u, y + ln,
               sk[4] if rng.random() < 0.42 else sk[2])
    if under:
        for (t, y, xl, xr) in rows:
            if t < lit or rng.random() < 0.45:
                continue
            c.line(xl + 1, y, xl + 2 + rng.random() * 2, y + rng.random() * 2 - 1, sk[1])


def _ring(c, u, rows, t, boss):
    """A metal band clamped round the beard at t. Gold, because the staff and the ward already
    are: a second metal on the figure reads as a different character's kit."""
    y, xl, xr = _at(rows, t)
    g = M["gold"]
    c.rect(xl - 0.34 * u, y - 0.90 * u, xr + 0.34 * u, y + 0.90 * u, OCC)
    c.rect(xl - 0.22 * u, y - 0.72 * u, xr + 0.22 * u, y + 0.72 * u, g[2])
    c.hline(xl - 0.10 * u, xr + 0.10 * u, y - 0.78 * u, g[1])
    c.hline(xl - 0.10 * u, xr + 0.10 * u, y + 0.70 * u, g[3])
    # corners knocked off both ways, so the band reads as a band round something round rather
    # than as a rectangle laid over it
    for (ex, ey) in [(xl - 0.22 * u, y - 0.72 * u), (xr + 0.22 * u, y - 0.72 * u),
                     (xl - 0.22 * u, y + 0.72 * u), (xr + 0.22 * u, y + 0.72 * u)]:
        c.set(int(ex), int(ey), OCC)
    c.hline(xl - 0.22 * u, xr + 0.22 * u, y - 0.72 * u, g[3])
    if boss:
        c.disc((xl + xr) * 0.5, y, 0.42 * u, 0.42 * u, g[1])


def _must_run(c, cx, yb, u, sk, rng, sgn, x0, y0, x1, y1, r0, r1, feather=12,
              feather_span=1.0, tip=0.0):
    """One moustache run: a tapering capsule out from beside the nose, mirrored by the caller.

    `feather_span` keeps the flicks off the outer end, and `tip` carries a hard needle past it.
    Both exist for the sharp variants: a capsule always ends in a rounded cap and the flicks
    always soften what they touch, so a moustache built only out of those two cannot come to a
    point however far its radius is taken down.
    """
    _capsule(c, cx + sgn * x0 * u, yb + (y0 + 0.40) * u, cx + sgn * x1 * u,
             yb + (y1 + 0.40) * u, (r0 + 0.07) * u, (r1 + 0.06) * u, OCC)
    _capsule(c, cx + sgn * x0 * u, yb + y0 * u, cx + sgn * x1 * u, yb + y1 * u,
             r0 * u, r1 * u, sk[3])
    _capsule(c, cx + sgn * (x0 - 0.10) * u, yb + (y0 - 0.35) * u,
             cx + sgn * (x1 - 0.35) * u, yb + (y1 - 0.15) * u,
             r0 * 0.46 * u, max(0.16, r1 * 0.60) * u, sk[2])
    for k in range(feather):
        f = rng.random() * feather_span
        fx = cx + sgn * (x0 + (x1 - x0) * f) * u
        fy = yb + (y0 + (y1 - y0) * f) * u + (r0 + (r1 - r0) * f) * u * 0.78
        c.line(fx, fy, fx + (rng.random() - 0.5) * 0.5 * u,
               fy + (0.20 + rng.random() * 0.45) * u, sk[4])
    if tip:
        ex = cx + sgn * (x1 + tip) * u
        ey = yb + y1 * u + (y1 - y0) * u * (tip / max(0.01, x1 - x0))
        c.line(cx + sgn * x1 * u, yb + y1 * u + 1, ex, ey + 1, OCC)
        c.line(cx + sgn * x1 * u, yb + y1 * u, ex, ey, sk[2])


def _must(c, cx, yb, u, sk, rng, *a, **kw):
    for sgn in (-1, 1):
        _must_run(c, cx, yb, u, sk, rng, sgn, *a, **kw)


# --- the five -------------------------------------------------------------------------------

def _b_cascade(c, cx, yb, u, sk, rng, under):
    """A broad straight fall to below the belt. Verticality is the whole idea: it is the only
    one of the five that makes him taller rather than wider."""
    rows = _rows(cx, yb, u, _BEARD_TOP, -10.20,
                 [(0.00, 2.40), (0.10, 3.20), (0.26, 3.70), (0.45, 3.82), (0.66, 3.76),
                  (0.82, 3.50), (0.92, 3.00), (0.97, 2.10), (1.00, 0.55)])
    _mass(c, u, rows, sk, rng, under, feather=72, streaks=104)
    _must(c, cx, yb, u, sk, rng, 1.05, -22.10, 3.30, -22.55, 1.55, 0.55, feather=15)


def _b_storm(c, cx, yb, u, sk, rng, under):
    """Blown off the vertical, the way the cloak already is. The sweep is what carries it: a
    beard doing something is worth more than a beard being large."""
    rows = _rows(cx, yb, u, _BEARD_TOP, -12.20,
                 [(0.00, 2.45), (0.10, 3.10), (0.25, 3.55), (0.45, 3.60), (0.65, 3.20),
                  (0.80, 2.50), (0.92, 1.60), (1.00, 0.35)],
                 skew=lambda t: 3.4 * pow(t, 1.8))
    _mass(c, u, rows, sk, rng, under, feather=64, streaks=92, lit=0.55)
    _must_run(c, cx, yb, u, sk, rng, -1, 1.05, -22.30, 2.95, -22.05, 1.35, 0.42, feather=12)
    _must_run(c, cx, yb, u, sk, rng, 1, 1.05, -22.30, 4.30, -21.80, 1.35, 0.30, feather=16)


def _b_bound(c, cx, yb, u, sk, rng, under):
    """Gathered and clamped in two gold bands. Ritual rather than wild - the beard of somebody
    who dresses for the work, and the only one that puts metal on him below the staff."""
    rows = _rows(cx, yb, u, _BEARD_TOP, -11.40,
                 [(0.00, 2.45), (0.12, 3.35), (0.30, 3.80), (0.42, 3.60), (0.48, 2.10),
                  (0.55, 2.75), (0.70, 2.95), (0.80, 2.60), (0.86, 1.55), (0.92, 1.95),
                  (0.97, 1.45), (1.00, 0.40)], wob=0.03)
    _mass(c, u, rows, sk, rng, under, feather=54, streaks=88)
    _ring(c, u, rows, 0.48, True)
    _ring(c, u, rows, 0.86, False)
    _must(c, cx, yb, u, sk, rng, 1.05, -22.20, 3.25, -22.70, 1.40, 0.48, feather=13)
    for sgn in (-1, 1):
        g = M["gold"]
        c.rect(cx + sgn * 2.75 * u - 0.36 * u, yb - 23.15 * u,
               cx + sgn * 2.75 * u + 0.36 * u, yb - 22.25 * u, g[2])
        c.vline(int(cx + sgn * 2.75 * u - 0.36 * u), yb - 23.15 * u, yb - 22.25 * u, g[1])


def _b_mane(c, cx, yb, u, sk, rng, under):
    """Enormous and undivided - moustache, cheeks and beard all one head of hair spilling to
    the shoulders. Primal rather than tidy, and the most heavily feathered of the five."""
    rows = _rows(cx, yb, u, _BEARD_TOP, -12.20,
                 [(0.00, 3.10), (0.08, 4.30), (0.20, 5.05), (0.36, 5.30), (0.55, 5.25),
                  (0.70, 4.85), (0.82, 4.20), (0.92, 3.10), (0.97, 1.80), (1.00, 0.45)],
                 wob=0.085)
    _mass(c, u, rows, sk, rng, under, feather=104, streaks=134)
    _must(c, cx, yb, u, sk, rng, 0.90, -22.60, 4.05, -22.95, 1.30, 0.80, feather=18)


def _b_fork(c, cx, yb, u, sk, rng, under):
    """A heavy mass splitting into two weighted points, with the moustache waxed out to match.
    The most deliberate of the five: nothing about it happened by accident to the man wearing
    it, which is a particular kind of powerful."""
    main = _rows(cx, yb, u, _BEARD_TOP, -17.40,
                 [(0.00, 2.45), (0.16, 3.35), (0.42, 3.95), (0.70, 4.10), (0.90, 3.85),
                  (1.00, 3.55)])
    _mass(c, u, main, sk, rng, None, feather=40, streaks=44)
    for sgn in (-1, 1):
        prong = _rows(cx, yb, u, -18.40, -11.20,
                      [(0.00, 1.95), (0.22, 2.00), (0.50, 1.72), (0.75, 1.25),
                       (0.92, 0.72), (1.00, 0.22)],
                      skew=lambda t, g=sgn: g * (1.80 + 1.05 * t))
        _mass(c, u, prong, sk, rng, under if sgn < 0 else None, feather=34, streaks=48,
              lit=0.30)
    _must(c, cx, yb, u, sk, rng, 1.00, -22.35, 3.95, -23.85, 1.05, 0.22, feather=14)


# --- three sharpenings of mane ---------------------------------------------------------------
#
# All three keep mane's width up at the cheeks - that mass is what was working - and spend the
# change on the two things it was missing: a point at the bottom instead of a round hem, and a
# moustache that ends in something rather than trailing off. They differ in WHERE the taper
# starts, which is what decides whether the shape reads as heavy, as fast, or as hooked.


def _b_mane_spear(c, cx, yb, u, sk, rng, under):
    """Mane held wide to the middle, then a long even run to a single point. THE SHIPPING ONE.

    The taper is the whole length of the lower half, so the mass stays the subject and the
    point is where it arrives. Moustache swept out and DOWN to needles, which turns the head
    into one downward arrow: heavy first, sharp second.

    It starts a unit and a quarter lower than the rest of the set - at -22.35 rather than the
    shared -23.60 - so it hangs from under the moustache instead of running up past it to the
    cheekbones. What that buys is dark: the band the cheeks used to fill is hood shadow now,
    and the face is a moustache and a nose coming out of a void rather than a head of hair
    filling the brim. The bottom drops the same amount, so the length is unchanged.
    """
    rows = _rows(cx, yb, u, -22.35, -10.20,
                 [(0.00, 3.10), (0.08, 4.30), (0.20, 5.00), (0.34, 5.20), (0.48, 4.95),
                  (0.62, 4.30), (0.74, 3.45), (0.85, 2.40), (0.94, 1.30), (1.00, 0.20)],
                 wob=0.075)
    _mass(c, u, rows, sk, rng, under, feather=98, streaks=128, lit=0.58)
    _must(c, cx, yb, u, sk, rng, 0.95, -22.45, 4.15, -21.45, 1.32, 0.16,
          feather=15, feather_span=0.62, tip=0.55)


def _b_mane_blade(c, cx, yb, u, sk, rng, under):
    """Widest highest, then straight sides all the way down to a chisel point.

    Nearly a triangle rather than a bell, and the longest of the three. Straight sides are
    what make it read as edged: a curve that eases into its point looks grown, and a line
    that runs at the point looks cut. Moustache waxed UP and out, the most predatory of them.
    """
    rows = _rows(cx, yb, u, _BEARD_TOP, -10.60,
                 [(0.00, 3.20), (0.10, 4.55), (0.22, 5.20), (0.32, 5.25), (0.50, 4.55),
                  (0.68, 3.55), (0.84, 2.35), (0.95, 1.10), (1.00, 0.18)],
                 wob=0.05)
    _mass(c, u, rows, sk, rng, under, feather=92, streaks=132, lit=0.62)
    _must(c, cx, yb, u, sk, rng, 0.95, -22.30, 4.35, -24.05, 1.24, 0.14,
          feather=13, feather_span=0.55, tip=0.60)


def _b_mane_talon(c, cx, yb, u, sk, rng, under):
    """Mane's roundness kept up top, with the last third hooked off the vertical.

    The hook is late and small - under two units - because a sweep that starts high reads as
    wind, and a curve that only appears at the end reads as a claw. Moustache asymmetric to
    agree with it: the downwind side runs a unit further and both ends flick up.
    """
    rows = _rows(cx, yb, u, _BEARD_TOP, -11.60,
                 [(0.00, 3.10), (0.08, 4.35), (0.20, 5.10), (0.36, 5.30), (0.54, 5.00),
                  (0.70, 4.25), (0.83, 3.10), (0.93, 1.80), (1.00, 0.22)],
                 skew=lambda t: 1.95 * pow(t, 2.6), wob=0.08)
    _mass(c, u, rows, sk, rng, under, feather=100, streaks=130, lit=0.56)
    _must_run(c, cx, yb, u, sk, rng, -1, 0.95, -22.40, 3.55, -23.35, 1.28, 0.16,
              feather=13, feather_span=0.58, tip=0.52)
    _must_run(c, cx, yb, u, sk, rng, 1, 0.95, -22.40, 4.55, -23.20, 1.28, 0.14,
              feather=16, feather_span=0.58, tip=0.62)


_BEARDS = {"cascade": _b_cascade, "storm": _b_storm, "bound": _b_bound,
           "mane": _b_mane, "fork": _b_fork,
           "mane-spear": _b_mane_spear, "mane-blade": _b_mane_blade,
           "mane-talon": _b_mane_talon}
BEARD_STYLES = ["cascade", "storm", "bound", "mane", "fork"]
MANE_VARIANTS = ["mane-spear", "mane-blade", "mane-talon"]


def _darker():
    """rgb -> the hex one step down its own row, for casting a shadow over drawn pixels.

    Only `skin` and `flesh` are in it, which is what keeps the pass off the hat and the robe:
    a shadow cast by walking pixels has to be told what it is allowed to touch. One step and
    not two - at two the nose goes black.
    """
    m = {}
    for row in ("skin", "flesh"):
        tones = M[row]
        for i, t in enumerate(tones):
            m[raster.rgb(t)] = tones[i + 1] if i + 1 < len(tones) else OCC
    return m


def _hat_shadow(c, x0, y0, x1, y1, y_full, y_none):
    """The brim's shadow, cast over whatever has already been drawn under it.

    The hood void is a shape; this is a pass. It has to be a pass because the nose and the
    moustache are drawn after the void and over it - without this they come out fully lit
    inside a shadow that is meant to be swallowing the top of the face.
    """
    m = _darker()
    span = max(1.0, float(y_none - y_full))
    for y in range(int(y0), int(y1) + 1):
        k = (y_none - y) / span
        if k <= 0:
            continue
        for x in range(int(x0), int(x1) + 1):
            if k < 1.0 and raster.BAYER8[y % 8][x % 8] / 64.0 > k:
                continue
            px = c.get(x, y)
            if px[3] and px in m:
                c.set(x, y, m[px])


def _face(c, cx, yb, u, under=None, style="cascade"):
    """A nose and a beard coming out of the bottom of the hood shadow. Nothing else.

    There is no mouth, no eyes and no cheek - at this size they would be three dark specks in
    a void, which is what made the earlier front-facing head read as a mask. A nose catching
    the light and a beard with weight under it is the whole face, and it is enough.

    The beard is `skin`, the bone/pallor row, not a grey mixed for the job: white hair and old
    bone are the same material in this palette. The nose is `flesh`, matching the hand on the
    staff - they are the only two pieces of the man himself on frame and they have to agree.
    """
    s = M["flesh"]
    _BEARDS[style](c, cx, yb, u, M["skin"], random.Random(30211), under)
    # One tone, not four: at three pixels across, a body colour with a lit cap and a shaded
    # spot in it is three colours fighting over a shape the eye reads as one. A straight taper
    # to an apex on the centre line, because a bulb this small has no room to read as round -
    # the only thing that survives at this size is whether it ends in a point or does not.
    nose = [(cx + a * u, yb + b * u) for (a, b) in
            [(-0.36, -24.05), (0.36, -24.05), (0.00, -22.95)]]
    c.poly([(x + 1, y + 1) for (x, y) in nose], OCC)
    c.poly(nose, s[2])
    c.line(nose[0][0], nose[0][1], nose[2][0], nose[2][1], s[1])
    c.line(nose[1][0], nose[1][1], nose[2][0], nose[2][1], s[3])
    # The brim reaches further down the face than the hood void itself suggests, and over a
    # longer fade: it starts higher AND ends lower than it did, so the gradient across the nose
    # and the top of the moustache is something you can see happening rather than a step.
    _hat_shadow(c, cx - 9 * u, yb - 25.4 * u, cx + 9 * u, yb - 20.6 * u,
                yb - 23.70 * u, yb - 21.60 * u)


def wizard_hero(c, cx, yb, h=150, robe="wool", staff_ramp=None, cast=(1.5, 0.5),
                under_ramp=None, beard="mane-spear", flicker=1.0):
    """Draw the figure from behind with its feet on yb. h scales the whole construction."""
    u = h / 32.0
    r = M[robe]
    ramp = staff_ramp or E["arcane"]
    w = max(1, int(round(1.3 * u)))

    # the cast shadow belongs to the ground, so it goes straight onto the scene
    c.poly([(cx - 7 * u, yb), (cx + 7 * u, yb),
            (cx + 7 * u + 12 * u * cast[0], yb + 4 * u * cast[1]),
            (cx - 2 * u + 12 * u * cast[0], yb + 5 * u * cast[1])], OCC)
    # Dithered out at its edge rather than a hard ellipse. Against a lit floor a hard
    # black contact shadow stops reading as shadow and starts reading as a hole.
    c.radial(cx, yb + 0.4 * u, 9.4 * u, 2.6 * u,
             [OCC, OCC, M["violet"][4], M["violet"][3], None])
    # The contact shadow stays hard occlusion even with the ward lit under him: a figure
    # standing IN a light still blocks it. Painting the ward back over the near lip of the
    # shadow was tried and it punched a hole in the floor.

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

    # The arm goes on over the robe but UNDER the mantle, so the capelet covers the shoulder
    # end of it. Drawn after the mantle it sat on top of every layer he wears, which reads as
    # an arm laid over the outside of his clothes.
    _arm(c, cx, yb, u, sx, r)

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
    # No clasp at the throat. A gold disc sat here, and under the hood shadow with nothing
    # else below it, a warm rounded shape at chin height reads as a chin. The beard covers
    # this whole area now.

    # --- head, then the hat over it ----------------------------------------------
    # Only a sliver of the skull itself is ever visible - the hood shadow covers the rest -
    # but it has to reach below the shadow so the dithered bottom of the void fades onto
    # something rather than onto the transparent layer.
    c.poly([(cx - 2.6 * u, yb - 27 * u), (cx + 2.6 * u, yb - 27 * u),
            (cx + 2.4 * u, yb - 22.8 * u), (cx - 2.4 * u, yb - 22.8 * u)], r[4])
    brim = [(cx - 8.6 * u, yb - 25.4 * u), (cx - 5.2 * u, yb - 27.2 * u),
            (cx, yb - 27.7 * u), (cx + 5.2 * u, yb - 27.2 * u), (cx + 8.6 * u, yb - 25.4 * u),
            (cx + 6.6 * u, yb - 24.3 * u), (cx + 3.0 * u, yb - 24.8 * u),
            (cx - 3.0 * u, yb - 24.8 * u), (cx - 6.6 * u, yb - 24.3 * u)]
    c.poly_shade(brim, [r[2], r[3], r[4], OCC], ang=-0.7854, bias=0.0, gamma=0.60)
    # The brim casts, wide and deep. Without this the hat sits on the shoulders instead of
    # over a head, and this band is the dark the face comes out of.
    _hood(c, cx, yb, u, r)
    _face(c, cx, yb, u, under_ramp, beard)
    cone =[(cx - 4.2 * u, yb - 27.3 * u), (cx + 4.2 * u, yb - 27.3 * u),
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
    # ...but NOT on the hood shadow. In those rows the void is the outermost lit thing, so
    # both passes would put a bone rim and a gold bounce on the one part of the figure whose
    # whole job is to be unlit. See _hood.
    hood = (int(cx - 7.4 * u), int(yb - 25.4 * u), int(cx + 7.4 * u), int(yb - 20.4 * u))
    # flicker steps the two edge passes DOWN their ramps rather than fading them. The rim is
    # RIM at full and bone tones below it; the bounce and the underlight shift one index
    # toward their dark end. Nothing is tinted and nothing is blended, so a flickering figure
    # still spends only contract colours - and the caller is expected to drive this off the
    # same clock as the flames, or the screen has two unrelated animations in it.
    step = 0 if flicker > 0.72 else (1 if flicker > 0.38 else 2)
    dim = lambda r: [r[min(len(r) - 1, i + step)] for i in range(len(r))]
    _bounce(c, 1, int(cx + 14 * u), int(yb - 36 * u), int(yb), dim(ramp), skip=hood)
    if under_ramp:
        _underlight(c, int(cx - 15 * u), int(cx + 15 * u), int(yb - 22 * u), int(yb + 1),
                    dim(under_ramp))
    _rim(c, 2, int(cx + 15 * u), int(yb - 36 * u), int(yb),
         [RIM, M["skin"][1], M["skin"][2]][step], skip=hood)

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
