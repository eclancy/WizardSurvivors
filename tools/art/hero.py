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
                  (cx + 5.0 * u, yb - 23.2 * u), (cx + 4.0 * u, yb - 22.0 * u),
                  (cx - 4.0 * u, yb - 22.0 * u), (cx - 5.0 * u, yb - 23.2 * u)],
                 [OCC, OCC, OCC, r[4], r[3]], ang=1.5708, gamma=0.8)


# The beard as a profile rather than a point list: half-width at t, 0 at the jaw and 1 at the
# bottom. Narrow where it is tied to the face, widest around two thirds down, rounded off at
# the bottom. Written as a curve because every hand-placed point list drawn for this ended up
# with a straight run in it somewhere, and a beard has no straight edges.
_BEARD_TOP = -23.60
_BEARD_BOT = -15.60


def _beard_w(t):
    return 2.60 + 1.45 * math.sin(3.14159 * pow(t, 0.75)) - 2.20 * pow(t, 2.40)


def _beard_pts(cx, yb, u, n=18):
    left, right = [], []
    for i in range(n + 1):
        t = i / float(n)
        y = yb + (_BEARD_TOP + (_BEARD_BOT - _BEARD_TOP) * t) * u
        w = _beard_w(t) * u
        left.append((cx - w, y))
        right.append((cx + w, y))
    right.reverse()
    return left + right


def _tufts(cx, yb, u, seed=7717):
    """Where the bush is lumpy: (x, y, radius), around the outline and scattered inside.

    Discs, not strokes. Five tapering capsules hung off the jaw gave the beard a set of
    dreadlocks - a stroke has a direction and reads as a lock of hair however short it is,
    while an overlapping field of discs has none and reads as bulk.
    """
    rng = random.Random(seed)
    ytop = yb + _BEARD_TOP * u
    ybot = yb + _BEARD_BOT * u

    def keep(x, y, r, edge):
        # No tuft may swell past either end of the profile: unclamped, the top row of them
        # bulged up over the brim and buried the hood shadow the face is supposed to sit in.
        r = min(r, 0.42 * u + (y - ytop), 0.42 * u + (ybot - y))
        return (x, y, r, edge) if r > 0.26 * u else None

    out = []
    for i in range(40):                                        # the outline
        t = (i + 0.5) / 40.0
        y = yb + (_BEARD_TOP + (_BEARD_BOT - _BEARD_TOP) * t) * u
        w = _beard_w(t) * u
        for sgn in (-1, 1):
            if rng.random() < 0.42:
                continue
            r = (0.55 + rng.random() * 0.50) * u
            k = keep(cx + sgn * (w - 0.30 * u + rng.random() * 0.22 * u),
                     y + (rng.random() - 0.5) * 0.7 * u, r, True)
            if k:
                out.append(k)
    for i in range(26):                                        # the inside
        t = 0.10 + rng.random() * 0.86
        y = yb + (_BEARD_TOP + (_BEARD_BOT - _BEARD_TOP) * t) * u
        w = _beard_w(t) * u
        k = keep(cx + (rng.random() * 2.0 - 1.0) * w * 0.78,
                 y + (rng.random() - 0.5) * 0.6 * u,
                 (0.62 + rng.random() * 0.58) * u, False)
        if k:
            out.append(k)
    return out


def _face(c, cx, yb, u, under=None):
    """A nose and a beard coming out of the bottom of the hood shadow. Nothing else.

    There is no mouth, no eyes and no cheek - at this size they would be three dark specks in
    a void, which is what made the earlier front-facing head read as a mask. A nose catching
    the light and a beard with weight under it is the whole face, and it is enough.

    The beard is `skin`, the bone/pallor row, not a grey mixed for the job: white hair and old
    bone are the same material in this palette. The nose is `flesh`, matching the hand on the
    staff - they are the only two pieces of the man himself on frame and they have to agree.
    """
    sk = M["skin"]
    s = M["flesh"]
    pts = _beard_pts(cx, yb, u)
    tufts = _tufts(cx, yb, u)
    # occlusion first, offset down-right: without it the beard and the mantle behind it are
    # both cool mid-values and the beard dissolves into the capelet it is meant to lie on.
    c.poly([(x + 0.45 * u, y + 0.45 * u) for (x, y) in pts], OCC)
    for (tx, ty, tr, edge) in tufts:
        if edge:
            c.disc(tx + 0.45 * u, ty + 0.45 * u, tr + 0.1 * u, tr + 0.1 * u, OCC)
    # Dark. The first pass ran this near-linear from `skin` base and the beard came out a pale
    # kite the size of his chest - the brightest thing on the figure, in a scene where the man
    # himself is deliberately unlit. It is a grey beard at night: the mass sits at shade and
    # deep, and only the fringe the ward reaches comes up to base.
    c.poly_shade(pts, [sk[3], sk[4]], ang=-0.7854, bias=0.0, gamma=0.80)
    # Every tuft is a dark disc with a smaller light one set into its upper left. That pairing
    # is the whole bushiness: one flat value over the lot leaves a smooth mass with a bumpy
    # outline, which reads as a beard-shaped object rather than as hair.
    for (tx, ty, tr, edge) in tufts:
        c.disc(tx, ty, tr, tr, sk[4])
    # Lit caps, and no dark ring around each lump. Ringing every tuft in occlusion turned the
    # beard into a field of separate bubbles - the bushiness has to live in the outline and in
    # a few crevices, not in outlining every disc that makes up the mass.
    ylo = yb + (_BEARD_BOT + 2.6) * u
    rng = random.Random(4413)
    for (tx, ty, tr, edge) in sorted(tufts, key=lambda t: t[1]):
        if rng.random() < 0.34:
            continue
        c.disc(tx - tr * 0.22, ty - tr * 0.26, tr * 0.48, tr * 0.48,
               sk[2] if ty > ylo else sk[3])
    # No crevice strokes. A handful of dark diagonals through the mass read as cracks in a
    # rock rather than as partings in hair; the caps above and the lumpy outline carry it.
    # the ward reaching the underside of the bush, as a scatter rather than as tips
    if under:
        for (tx, ty, tr, edge) in tufts:
            if ty < yb + (_BEARD_BOT + 1.1) * u:
                continue
            c.disc(tx, ty + tr * 0.30, tr * 0.30, tr * 0.26, sk[1])
            c.set(int(tx), int(ty + tr * 0.70), under[3])
    # A bulb, and nothing else on it. Both earlier noses had a bridge, a flare and nostrils in
    # them, and at eight pixels tall that detail is noise: the read comes entirely from the
    # silhouette of something rounded poking down out of the dark.
    c.disc(cx - 0.05 * u, yb - 23.40 * u, 0.90 * u, 1.06 * u, s[3])
    c.disc(cx - 0.05 * u, yb - 23.55 * u, 0.74 * u, 0.86 * u, s[2])
    c.disc(cx - 0.26 * u, yb - 23.62 * u, 0.42 * u, 0.48 * u, s[1])


def wizard_hero(c, cx, yb, h=150, robe="wool", staff_ramp=None, cast=(1.5, 0.5),
                under_ramp=None):
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
    _face(c, cx, yb, u, under_ramp)
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
    hood = (int(cx - 7.4 * u), int(yb - 25.4 * u), int(cx + 7.4 * u), int(yb - 21.4 * u))
    _bounce(c, 1, int(cx + 14 * u), int(yb - 36 * u), int(yb), ramp, skip=hood)
    if under_ramp:
        _underlight(c, int(cx - 15 * u), int(cx + 15 * u), int(yb - 22 * u), int(yb + 1),
                    under_ramp)
    _rim(c, 2, int(cx + 15 * u), int(yb - 36 * u), int(yb), RIM, skip=hood)

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
