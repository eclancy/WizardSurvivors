# -*- coding: utf-8 -*-
"""The Spiritual Weapon blade. Run: python tools/art/spirit_blade.py

Eight frames of one spectral blade, 48x48 per cell, rendered at x2 - the ELITE cell rather than the
32x32 projectile cell, because the brief was "bigger" and the way to be bigger in this project is
to move up a cell, never to scale a sprite. See art-direction.md section 1: render scale is x2 at
every tier, no exceptions.

WHAT MAKES IT READ AS ETHEREAL. Three things, and none of them is transparency:

  1. **It has no occlusion.** Every solid object in this game is anchored by `occ` somewhere - a
     contact shadow, a dark underside, a hard rim. This has none at all. A shape with no black in
     it does not sit in the world, which is exactly the point.
  2. **It is brightest in the middle and dissolves outward**, the opposite of a lit object, which
     is brightest where the key light strikes and darkest away from it. Nothing is lighting this
     blade; it is the light.
  3. **The edge is broken.** The outer band is dithered into the background rather than closing,
     so the silhouette never quite resolves - a solid edge is the single strongest cue that a
     thing is made of matter.

The eight frames are a shimmer, not a rotation: the core pulses and the wisps crawl along the
blade. The spinning is done by the orbit, and SpiritualWeapon now turns each blade to its tangent
so the shape sweeps rather than sliding sideways.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")

# Python 2.7 is the only Python here and it has no TAU.
TAU = math.pi * 2.0

CELL = 48
FRAMES = 8

# Arcane, because a spirit weapon is conjured rather than elemental, and the Arcane ramp is the
# one cold row whose edge is saturated enough to hold against a Castle floor (section 3's caution).
CORE = bl.ELEMENTS["arcane"][0]
HOT = bl.ELEMENTS["arcane"][1]
MID = bl.ELEMENTS["arcane"][2]
EDGE = bl.ELEMENTS["arcane"][3]


def _blade_outline(t):
    """A leaf-shaped blade pointing +X, breathing very slightly over the cycle."""
    breathe = 1.0 + math.sin(t * TAU) * 0.05
    pts = []
    tip_x, tail_x = 44.0, 6.0
    for i in range(0, 17):
        f = i / 16.0
        x = tail_x + (tip_x - tail_x) * f
        # Widest a third of the way up and then a long taper to the point. A profile that peaks
        # in the middle is a leaf, which is what the first pass came out as - a weapon is
        # asymmetric along its length and that asymmetry is most of what reads as "blade".
        w = 9.0 * math.sin(math.pi * (f ** 0.58)) ** 0.85 * breathe
        pts.append((x, 24 - w))
    for i in range(16, -1, -1):
        f = i / 16.0
        x = tail_x + (tip_x - tail_x) * f
        w = 9.0 * math.sin(math.pi * (f ** 0.58)) ** 0.85 * breathe
        pts.append((x, 24 + w))
    return pts


def _inset(pts, k):
    """Pull an outline toward its spine. Cheap, and exact enough at this size."""
    return [(x, 24 + (y - 24) * k) for (x, y) in pts]


def blade_frame(c, ox, t):
    outline = _blade_outline(t)

    # Body, brightest at the spine and dissolving outward - the inversion that sells "made of
    # light" over "lit from somewhere".
    c.poly([(x + ox, y) for (x, y) in outline], EDGE)
    c.poly([(x + ox, y) for (x, y) in _inset(outline, 0.72)], MID)
    c.poly([(x + ox, y) for (x, y) in _inset(outline, 0.44)], HOT)
    c.poly([(x + ox, y) for (x, y) in _inset(outline, 0.18)], CORE)

    # The broken outer edge. Every other pixel along the silhouette, phase-shifted per frame, so
    # the boundary crawls and never closes.
    for i, (x, y) in enumerate(outline):
        if (i + int(t * 8)) % 2 == 0:
            continue
        c.set(x + ox, y, MID)

    # Wisps trailing off the hilt end, drifting along the blade over the cycle.
    for k in range(4):
        phase = (t + k * 0.25) % 1.0
        wx = 6.0 - phase * 6.0
        wy = 24 + math.sin((t + k * 0.31) * TAU) * (3.0 + k)
        c.set(wx + ox, wy, EDGE if k > 1 else MID)

    # A few motes shed off the edge, which is the other thing a solid object never does.
    for k in range(3):
        a = (t + k / 3.0) * TAU
        mx = 24 + math.cos(a) * 15.0
        my = 24 + math.sin(a) * 9.0
        c.set(mx + ox, my, EDGE)


def strip():
    c = raster.Canvas(CELL * FRAMES, CELL, None)
    for f in range(FRAMES):
        blade_frame(c, f * CELL, f / float(FRAMES))
    return c


def main():
    canvas = strip()
    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS (art-direction.md section 3):")
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return 1

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    canvas.scaled(2).save(os.path.join(OUT_DIR, "spirit-blade.png"))
    print("  spirit-blade   %dx%d  (%d frames of %d)" % (CELL * FRAMES * 2, CELL * 2, FRAMES, CELL * 2))
    print("")
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
