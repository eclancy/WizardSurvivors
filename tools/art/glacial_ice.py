# -*- coding: utf-8 -*-
"""Glacial Spike's ice: the spike that erupts under its target, in three sizes. Run: python tools/art/glacial_ice.py

Writes assets/bonelight/effects/glacial-spike.png, which scenes/GlacialSpike.tscn plays through
scripts/IceSpike.cs. It used to be spell_fx.glacial_spike: a flat blue triangle with a lit line down
it, three white strokes for cracks, out of a 32 px cell - and the code then scaled that sprite by the
spike's Area, so at level 8 its pixels came out 1.3 or 1.7 world px across and uneven, stretched art
on a grid of square art. Eric, 2026-10-29, of the spells that still looked like that: "does not look
up to our typical visual fidelity", then "Update all spells to the same quality we expect now".

THE BAR IS OBSIDIAN SPIKE'S GLASS (obsidian_glass.py), so this is the same object made of ice:

  1. FACETS WITH HARD EDGES. The spike is two planes meeting at a ridge, the key-facing one (left, the
     key is upper left, art-direction section 2) lit and the other in shadow, with no blend between.
     A fold low on the lit plane is a third facet a stop darker, so the lit side is not one flat sheet.
  2. A SPECULAR THAT IS A LINE. The ridge carries the Ice ramp's core from the point down, and a thin
     reflection band runs parallel to it on the lit plane.
  3. LIGHT THROUGH IT. Ice is not glass: light the lit plane takes in shows through the shadow plane as
     a band of the ramp's mid beside the ridge, which is what keeps the dark side reading as ice and not
     as a blue rock. Cold-element caution (art-direction section 3): the shadow plane falls to the
     azure row's shade and deep - the saturated blue the player cast owns, and Glacial Spike is the
     player's - so the spike holds its edge on the blue Castle floor, not the drained stone blue.
  4. IT COMES OUT OF THE GROUND. The spike is pushed up through a hole, not grown: every rising frame
     is the top of the same spike, cut by the ground line, with chips of ice thrown up beside it.

THREE SIZES, ONE RENDER SCALE (art-direction section 1: x2 for everything, a bigger thing is a bigger
cell, never a bigger scale). The code no longer scales the sprite at all; what grows with Area is the
reach, and the reach is shown by the ice the strike throws out to exactly its hit radius
(ImpactDebrisLayer, in IceSpike.Strike), as Obsidian Spike shows its own. So:

  row 0  "default"  the spike (30 art px tall, 60 on screen)
  row 1  "small"    a crown spike (Glacier Crown's ring of twelve, GlacierCrown.RingSpikeSize)
  row 2  "spire"    Frozen Spire, twice the spike's height (IceSpike.SpireDrawScale), with a flank of
                    smaller crystals - a pillar, not a stretched spike

Every row is FRAMES frames in a CELL_W x CELL_H cell, foot on GY, rendered x2 and shown at scale 1:
  0-3  rising through the ground   4  whole, the glint on its point (IceSpike.StandFrame holds here)
  5-7  cracking, more each frame   (the withdraw plays the strip backwards, sinking as it un-cracks)
IceSpike.ArtFootBelowCentre is (GY + 1 - CELL_H / 2) x 2: change both.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")

CELL_W, CELL_H = 40, 72
CX = 20
GY = 67
FRAMES = 8

ICE = bl.ELEMENTS["ice"]                      # core, hot, mid, edge
AZ_LIT = bl.tone("azure", "lit")
AZ_SHADE = bl.tone("azure", "shade")
AZ_DEEP = bl.tone("azure", "deep")
OCC = bl.OCC

# How far out of the ground, and how cracked, each frame is.
RISE = [0.2, 0.52, 0.84, 0.97, 1.0, 1.0, 1.0, 1.0]
CRACK = [0.0, 0.0, 0.0, 0.0, 0.0, 0.34, 0.68, 1.0]

# name -> (height, half width at the foot, flank crystals as (dx, height share, half width, lean))
SIZES = [
    ("default", 30.0, 7.0, [(-6.5, 0.42, 3.5, -3.0), (6.5, 0.34, 3.0, 3.0)]),
    ("small", 18.0, 4.0, [(4.0, 0.36, 2.0, 2.0)]),
    ("spire", 58.0, 9.5, [(-9.0, 0.36, 4.5, -4.0), (9.5, 0.28, 4.0, 4.0), (-4.0, 0.16, 3.0, -1.0)]),
]


def lerp(a, b, t):
    return a + (b - a) * t


def h2(x, y, s=0.0):
    v = math.sin(x * 12.9898 + y * 78.233 + s * 37.719) * 43758.5453
    return v - math.floor(v)


def dither(x, y):
    return raster.DITHER[y % 8][x % 8] / 64.0


class Blade(object):
    """One crystal: its point at (ax, ay), its foot `half` either side of `cx` on row `fy` (which may be
    below the ground line - a rising spike is the top of a whole one), the ridge foot at `rx`."""

    def __init__(self, cx, ay, height, half, lean=0.0, ridge=-0.2):
        self.ax = cx + lean
        self.ay = ay
        self.fy = ay + height
        self.h = height
        self.xl = cx - half
        self.xr = cx + half
        self.rx = cx + half * ridge

    def span(self, y):
        """Left edge, ridge and right edge at row y (y + 0.5 is the pixel's middle), or None."""
        u = (y + 0.5 - self.ay) / self.h
        if u < 0.0 or u > 1.0:
            return None
        # The edges bow out a little toward the foot: a knapped crystal, not a ruled triangle.
        bow = 0.6 * math.sin(math.pi * u)
        return (lerp(self.ax, self.xl, u) - bow, lerp(self.ax, self.rx, u), lerp(self.ax, self.xr, u) + bow, u)


def draw_blade(c, b, crack, seed, ground=GY, spec=0.5, band=True):
    """Shades every pixel of the blade above the ground line by the facet it is on."""
    top = int(math.floor(b.ay))
    for y in range(top, min(int(math.ceil(b.fy)), ground) + 1):
        s = b.span(y)
        if s is None:
            continue
        xl, xm, xr, u = s
        x0 = int(round(xl))
        x1 = int(round(xr))
        if x1 < x0:
            continue
        xmi = int(round(xm))
        for x in range(x0, x1 + 1):
            if x == xmi:
                # The ridge: the key's specular from the point down, cooling to hot.
                tone = ICE[0] if u < spec else ICE[1]
            elif x < xmi:
                # The lit plane: hot where it faces the key full on, high up and along its edge;
                # the lower plane, turned a little further from it, a stop down.
                lower = u > 0.42 + 0.25 * (xmi - x) / max(1.0, xmi - x0)
                tone = ICE[2] if lower else ICE[1]
                if x == x0:
                    tone = ICE[0] if u < 0.6 else ICE[1]
                if band and xmi - x == 2 and 0.18 < u < 0.56:
                    tone = ICE[0]
                # The fold: the low facet turned from the key, a stop darker, its crest catching it.
                # Its crest slants down away from the key edge, so it reads as a fold, not a dash.
                fold = 0.62 + 0.06 * math.sin(seed * 3.1) + 0.2 * (x - x0) / max(1.0, xmi - x0)
                if u > fold and x < lerp(x0, xmi, 0.6) and x > x0:
                    tone = ICE[3] if u > fold + 0.06 else ICE[0]
            else:
                # The shadow plane: light through the ice beside the ridge, the deep blue outside it.
                k = (x - xm) / max(1.0, xr - xm)
                if x == x1:
                    tone = AZ_DEEP
                elif k > 0.72:
                    tone = AZ_SHADE
                elif x - xmi == 1 or (k < 0.4 and u > 0.55):
                    tone = ICE[2]
                elif k < 0.55 and dither(x, y) < 0.35 * (1.0 - k):
                    tone = AZ_LIT
                else:
                    tone = ICE[3]
            c.set(x, y, tone)
    if crack > 0.0:
        draw_cracks(c, b, crack, seed, ground)
    # The point: one pixel of core, and a contact of deep where the shadow plane meets the ground.
    if b.ay < ground:
        c.set(int(round(b.ax)), int(math.floor(b.ay)), ICE[0])


def draw_cracks(c, b, amount, seed, ground):
    """Fractures running out from the ridge: dark on the lit plane, light inside the dark one."""
    paths = [(0.28, -1, 0.5), (0.46, 1, 0.62), (0.62, -1, 0.42), (0.74, 1, 0.5), (0.38, 1, 0.3)]
    for n, (at, side, length) in enumerate(paths):
        if n / float(len(paths)) >= amount * 1.05:
            break
        s = b.span(lerp(b.ay, b.fy, at))
        if s is None:
            continue
        y = lerp(b.ay, b.fy, at)
        x = s[1]
        steps = int(max(2, (s[2] - s[0]) * 0.5 * length * min(1.0, amount * 1.6)))
        for k in range(steps):
            x += side
            y += 0.7 + 0.5 * h2(k, n, seed)
            if y >= ground:
                break
            row = b.span(int(y))
            if row is None or not (row[0] + 0.5 < x < row[2] - 0.5):
                break
            on_lit = x < row[1]
            c.set(int(round(x)), int(y), ICE[3] if on_lit else ICE[1])
            if on_lit and k == 0:
                c.set(int(round(x)), int(y) + 1, ICE[2])


def ground_hole(c, half, rise, seed, ground=GY):
    """The ground the spike broke through: a hole a little wider than its foot, broken floor round it."""
    w = half * (0.55 + 0.6 * rise) + 1.5
    for x in range(int(CX - w) - 1, int(CX + w) + 2):
        dx = (x + 0.5 - CX) / w
        if abs(dx) > 1.0:
            continue
        depth = 1 if abs(dx) > 0.75 else 2
        for y in range(ground - 1, ground + depth):
            c.set(x, y, OCC)
    # Broken floor: slabs pushed up on either side, lit on top, rime caught on them.
    for k, side in enumerate((-1, 1, -1, 1)):
        x = CX + side * (w + 0.5 + 1.8 * k * 0.5 + h2(k, 3, seed))
        y = ground - (1 if k < 2 else 0)
        c.set(int(x), int(y), ICE[2] if side < 0 else ICE[3])
        c.set(int(x) + side, int(y), AZ_SHADE)
        c.set(int(x), int(y) + 1, OCC)
        if k < 2:
            c.set(int(x), int(y) - 1, ICE[0] if side < 0 else ICE[1])


def chips(c, frame, half, seed, ground=GY):
    """Chips of ice thrown up as the spike breaks out (rising frames only)."""
    if frame > 2:
        return
    for k in range(5):
        side = -1 if k % 2 == 0 else 1
        x = CX + side * (half * 0.4 + 1.5 + 2.2 * h2(k, 1, seed) + frame * 1.4)
        y = ground - 3 - frame * 3 - 4 * h2(k, 2, seed)
        c.set(int(x), int(y), ICE[0] if k < 2 else ICE[1])
        c.set(int(x) + side, int(y) + 1, ICE[3])


def glint(c, x, y):
    """The point catching the key: a star of core four pixels across."""
    for (dx, dy) in ((-1, 0), (1, 0), (0, -1), (0, 1), (-2, 0), (2, 0)):
        c.set(x + dx, y + dy, ICE[0] if abs(dx) + abs(dy) == 1 else ICE[1])


def frame(name, height, half, flanks, i):
    c = raster.Canvas(CELL_W, CELL_H, None)
    rise = RISE[i]
    crack = CRACK[i]
    seed = {"default": 1.0, "small": 2.0, "spire": 3.0}[name]
    ground_hole(c, half, rise, seed)
    # The flank crystals come up behind and beside the spike, a beat later.
    for (dx, share, fh, lean) in flanks:
        r = max(0.0, (rise - 0.15) / 0.85)
        if r <= 0.0:
            continue
        hh = height * share
        b = Blade(CX + dx, GY - hh * r, hh, fh, lean=lean, ridge=-0.1 if dx < 0 else 0.1)
        draw_blade(c, b, crack * 0.6, seed + dx, spec=0.35, band=False)
    b = Blade(CX, GY - height * rise, height, half, lean=0.0, ridge=-0.18)
    draw_blade(c, b, crack, seed)
    chips(c, i, half, seed)
    if i == 4:
        # Whole: the light runs the full length of the ridge, the moment before it cracks.
        b_full = Blade(CX, GY - height, height, half, lean=0.0, ridge=-0.18)
        for y in range(int(GY - height), GY):
            s = b_full.span(y)
            if s is not None and s[3] < 0.85:
                c.set(int(round(s[1])), y, ICE[0])
    # The contact line under the spike's shadow side, on the ground.
    c.hline(CX + 1, CX + int(half * rise) + 2, GY + 1, OCC)
    return c


def sheet():
    from PIL import Image
    out = Image.new("RGBA", (CELL_W * 2 * FRAMES, CELL_H * 2 * len(SIZES)), (0, 0, 0, 0))
    problems = []
    for row, (name, height, half, flanks) in enumerate(SIZES):
        for i in range(FRAMES):
            c = frame(name, height, half, flanks, i)
            bad = c.audit()
            if bad:
                problems.append("%s frame %d: %s" % (name, i, bad[:4]))
                continue
            out.alpha_composite(c.scaled(2), (i * CELL_W * 2, row * CELL_H * 2))
    return out, problems


def main(argv=None):
    img, problems = sheet()
    if problems:
        print("OFF-CONTRACT COLOURS in glacial-spike: " + "; ".join(problems))
        return 1
    path = os.path.join(OUT_DIR, "glacial-spike.png")
    if argv is not None and len(argv) > 1:
        path = argv[1]
    elif not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    img.save(path)
    print("  %-20s %d frames on %d rows, %dx%d cells, palette clean" % (
        "glacial-spike", FRAMES * len(SIZES), len(SIZES), CELL_W, CELL_H))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
