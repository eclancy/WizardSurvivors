# -*- coding: utf-8 -*-
"""The fire and smoke that stand in a burning crater. Run: python tools/art/crater_fire.py

Reported 2026-10-28: "fires from meteor and the crater should look better and more detailed". The
burning ground of Meteor Swarm's Magma Craters and of Cataclysm's craters was Ember Caps' 16 px lick
of flame (spore_mushrooms.ember_frame), four frames of three stubby tongues, stamped in rings and
then multiplied flat red by the evolution's tint. This is the fire those craters stand up out of.

One sheet, written to assets/bonelight/effects/crater-fire.png: a 16x24 cell, three rows of eight.

  row 0  blaze   a full flame, looping: a main tongue and two side tongues out of step, each
                 swaying from the root up so the tip whips more than the base, a white-hot heart
                 low in the main tongue, a coal bed under it, and now and then a wisp torn off
                 the tip. The flames that fill a crater while it burns.
  row 1  lick    the same fire burnt low, looping: shorter, narrower, more edge than core. The
                 flames on the crater's outskirts, and every flame on its way out.
  row 2  smoke   one puff's life, not a loop: a dense knot that swells, thins and dissolves,
                 lit warm from the fire under it while it is young. CraterFire lifts it as it
                 plays, so the sprite only has to grow.

The cell size and row order are an API: scripts/CraterFire.cs reads them as Cell, FootRow and the
Row* constants, so change both.

AUTHORED 1:1 AND SHOWN AT x2 (.ai/art-direction.md section 1): a blaze is 48 px tall in the world,
a little over half the wizard. Every flame stands on the bottom row of its cell, which is the point
CraterFire places on the ground.

Palette: the fire element ramp, under section 3's rule - the core is white-hot and the identity
lives in the mid and edge - and the red row's lit and base for the coals going out. The smoke is
the palette's own pale darks (bone shade and deep, stone hi), because on a floor whose brightest
stone is #3A4658 a smoke drawn darker than the ground is not there at all.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")

W, H = 16, 24
FRAMES = 8
TAU = math.pi * 2.0

CORE, HOT, MID, EDGE = bl.ELEMENTS["fire"]
COAL_LIT = bl.tone("red", "lit")
COAL = bl.tone("red", "base")
SMOKE_HI = bl.tone("stone", "hi")
SMOKE_LIT = bl.tone("skin", "shade")
SMOKE = bl.tone("skin", "deep")
SMOKE_WARM = bl.tone("flesh", "shade")

# Heat bands, hottest first: a pixel takes the first tone whose threshold its heat clears.
BLAZE_BANDS = [(0.95, CORE), (0.68, HOT), (0.38, MID), (0.0, EDGE)]
LICK_BANDS = [(1.0, CORE), (0.74, HOT), (0.44, MID), (0.0, EDGE)]


def dither(x, y):
    return raster.DITHER[y % 8][x % 8] / 64.0


def tongue(heat, cx, base_y, height, half_width, sway, phase, t):
    """Adds one tongue of flame into the heat field.

    The tongue rises from base_y; its width falls from half_width at the root to nothing at the tip,
    and its centre line bends by `sway` art pixels at the tip, more the higher it goes - a flame whips
    at the top and is held at the root. The heat a pixel gets is highest low in the middle of the
    tongue and falls toward its sides and its tip, so the bands nest as a flame's do instead of
    stacking as stripes.
    """
    if height < 1.0:
        return
    top = base_y - height
    for y in range(int(math.floor(top)), base_y + 1):
        u = (base_y - y) / float(height)          # 0 at the root, 1 at the tip
        if u < 0.0 or u > 1.0:
            continue
        bend = sway * u * u * math.sin(TAU * t + phase + u * 2.4)
        centre = cx + bend
        # Full at the root, a little pinched at the very bottom, tapering to the tip.
        width = half_width * (1.0 - u) ** 0.6 * (0.7 + 0.3 * min(1.0, u * 5.0))
        for x in range(int(math.floor(centre - width - 1)), int(math.ceil(centre + width + 1)) + 1):
            lateral = abs(x + 0.5 - centre) / max(0.6, width)
            if lateral > 1.0:
                continue
            h = (1.0 - u) * 0.95 + (1.0 - lateral) * 0.45 - 0.25
            if 0 <= x < W and 0 <= y < H:
                heat[y][x] = max(heat[y][x], h)


def paint(c, heat, bands):
    for y in range(H):
        for x in range(W):
            h = heat[y][x]
            if h <= -0.5:
                continue
            # The outermost pixel of every tongue is edge whatever its heat, so the flame has a rim.
            rim = False
            for (dx, dy) in ((1, 0), (-1, 0), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < W and 0 <= ny < H) or heat[ny][nx] <= -0.5:
                    rim = True
            if rim:
                c.set(x, y, EDGE)
                continue
            for threshold, tone in bands:
                if h + (dither(x, y) - 0.5) * 0.12 >= threshold:
                    c.set(x, y, tone)
                    break


def coal_bed(c, x0, x1, i, hot):
    """The coals the flame stands on: the bottom row, never still, cooling toward its ends."""
    for x in range(x0, x1 + 1):
        k = (x * 7 + i * 3) % 5
        mid = (x0 + x1) / 2.0
        outer = abs(x - mid) / max(1.0, (x1 - x0) / 2.0)
        if outer > 0.8:
            tone = COAL if k < 3 else COAL_LIT
        elif hot and k == 0 and outer < 0.4:
            tone = HOT
        else:
            tone = MID if k < 2 else EDGE
        c.set(x, H - 1, tone)
    c.set(x0 - 1, H - 1, COAL)
    c.set(x1 + 1, H - 1, COAL)


def flame_frame(i, low):
    c = raster.Canvas(W, H)
    t = i / float(FRAMES)
    heat = [[-1.0] * W for _ in range(H)]
    base = H - 2
    s = math.sin(TAU * t)
    if low:
        # Three short tongues out of step; the middle one the tallest.
        tongue(heat, 4.5, base, 5.0 + 1.6 * math.sin(TAU * t + 1.9), 2.2, 1.0, 0.7, t)
        tongue(heat, 11.5, base, 5.5 + 1.6 * math.sin(TAU * t + 4.0), 2.2, 1.2, 2.9, t)
        tongue(heat, 8.0, base, 8.5 + 1.8 * s, 3.2, 1.6, 0.0, t)
        paint(c, heat, LICK_BANDS)
        coal_bed(c, 3, 12, i, False)
        return c

    # The blaze: two side tongues behind a main one, each on its own beat.
    tongue(heat, 4.0, base, 9.0 + 2.6 * math.sin(TAU * t + 2.2), 2.6, 1.4, 1.3, t)
    tongue(heat, 12.0, base, 10.0 + 2.6 * math.sin(TAU * t + 4.4), 2.6, 1.6, 3.6, t)
    tongue(heat, 8.0, base, 15.5 + 2.5 * s, 4.6, 2.4, 0.0, t)
    paint(c, heat, BLAZE_BANDS)

    # A wisp torn off the tip at the top of the main tongue's beat: two pixels of flame above it,
    # drifting with the sway. It is what makes a loop read as fire rather than as a wobbling shape.
    if s > 0.35:
        top = int(round(base - (15.5 + 2.5 * s))) - 2 - (1 if s > 0.8 else 0)
        x = int(round(8.0 + 2.6 * math.sin(TAU * t + 2.4)))
        c.set(x, top, MID)
        c.set(x, top - 1, EDGE)
        if s > 0.8:
            c.set(x + 1, top - 3, EDGE)
    coal_bed(c, 2, 13, i, True)
    return c


def smoke_frame(i):
    """One puff at stage i of its life: a knot that swells, thins and dissolves."""
    c = raster.Canvas(W, H)
    t = i / float(FRAMES - 1)
    cx, cy = 8.0, 15.0 - 3.0 * t
    r = 2.6 + 4.2 * t
    # Thinned only toward the end and at the edges: a puff dithered to half all its life read as a
    # checkerboard at x2, not as smoke. CraterFire's alpha does the rest of the fading.
    keep = 1.0 - 0.5 * t ** 1.5
    lobes = [(0.0, 0.0, 1.0), (-0.45, 0.25, 0.75), (0.5, 0.15, 0.7), (0.1, -0.45, 0.68)]
    for y in range(H):
        for x in range(W):
            best = 9.0
            for (ox, oy, rs) in lobes:
                dx = (x + 0.5 - (cx + ox * r)) / (r * rs)
                dy = (y + 0.5 - (cy + oy * r)) / (r * rs * 0.9)
                best = min(best, dx * dx + dy * dy)
            if best > 1.0:
                continue
            # Thinner toward the edge of the puff, and thinner as it ages.
            density = keep * (1.15 - best * 0.55)
            if dither(x, y) > density:
                continue
            # Lit from the upper left like everything else; warm underneath while the fire is close.
            nx = x + 0.5 - cx
            ny = y + 0.5 - cy
            light = -(nx + ny) / (r * 1.4142)
            if t < 0.45 and ny > r * 0.15 and dither(x + 3, y + 5) < 0.6 - t:
                tone = SMOKE_WARM
            elif light > 0.35:
                tone = SMOKE_LIT if t < 0.6 else SMOKE_HI
            elif light > -0.3:
                tone = SMOKE_HI
            else:
                tone = SMOKE
            c.set(x, y, tone)
    return c


def build():
    rows = [
        [flame_frame(i, False) for i in range(FRAMES)],
        [flame_frame(i, True) for i in range(FRAMES)],
        [smoke_frame(i) for i in range(FRAMES)],
    ]
    return rows


def main():
    from PIL import Image
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    rows = build()
    sheet = Image.new("RGBA", (W * FRAMES, H * len(rows)), (0, 0, 0, 0))
    for r, frames in enumerate(rows):
        for i, c in enumerate(frames):
            bad = c.audit()
            if bad:
                print("OFF-CONTRACT COLOURS in crater-fire row %d frame %d: %s" % (r, i, bad[:4]))
                return 1
            sheet.alpha_composite(c.img, (i * W, r * H))
    path = os.path.join(OUT_DIR, "crater-fire.png")
    sheet.save(path)
    print("  %-28s %d rows of %d frames, %dx%d cells" % ("crater-fire.png", len(rows), FRAMES, W, H))
    return 0


if __name__ == "__main__":
    sys.exit(main())
