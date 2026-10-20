# -*- coding: utf-8 -*-
"""Status marks - the small animated effect an enemy wears while a debuff is on it.

Run: python tools/art/status_fx.py

One sheet, assets/bonelight/effects/status-marks.png: six rows of six 24x24 frames, authored 1:1
and drawn at x2 by StatusEffectLayer (x4 on a boss). One texture on purpose. Every mark in the
arena is drawn by two canvas items out of this one atlas, so a swarm of 140 slowed and poisoned
enemies is two batches rather than two hundred and eighty draw calls - the phone runs the
Compatibility renderer, where a texture switch is what breaks a batch.

THE ROW ORDER IS AN API. StatusEffectLayer addresses rows by index (StatusVisual order), and
RegressionChecks.ValidateStatusEffectAtlas checks the sheet is exactly ROWS x FRAMES cells.

WHY SHAPE AND MOTION, NOT COLOUR. Colour is the third thing a player reads in a swarm, after
silhouette and motion, and two of these share a hue family (slow and freeze are both Ice). So each
status owns a shape and a direction of travel that would still name it in greyscale:

  slow        streaks dragging sideways off the feet, and motes lifting slowly   (Ice)
  freeze      a crust of ice shards round the feet, a glint walking round it      (Ice)
  poison      hollow bubbles rising off the shoulder and popping                  (Poison)
  vulnerable  four brackets closing on a cracked diamond over the head            (Darkness)
  weaken      chevrons sinking down beside the head                               (Light)
  wither      flakes of ash with a dying ember in them, tumbling down the body   (Metal, molten)

Wither is Molten Shard's debuff, so it is Metal's ash grey carrying Fire's ember - the old pip it
replaces was ember for the same reason. Ash alone, in the dark half of any ramp, vanishes into a
Bonelight floor; the ember is what keeps a fleck visible.

The two marks drawn over the floor rather than over the body (slow, freeze) sit on the feet row:
cell row FEET_ROW is where the enemy's feet are. Everything else is placed by the layer against
the enemy's head or shoulder, so its own pivot is documented on the row below.

These are EFFECTS, so they follow the spell rule (.ai/art-direction.md section 3): element ramps,
white-hot where they are brightest. The light marks (weaken, vulnerable) carry a 1px occ shadow on
their underside so they still read over the Ice chapter's pale ground; that is a cast shadow, not
an outline, and nothing here is an actor.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "effects", "status-marks.png")
PREVIEW = os.path.join(ROOT, "tools", "art", "_status_fx_preview.png")

CELL = 24
FRAMES = 6
FEET_ROW = 20
TAU = math.pi * 2.0
OCC = bl.OCC

ICE = bl.ELEMENTS["ice"]          # core hot mid edge
POISON = bl.ELEMENTS["poison"]
DARK = bl.ELEMENTS["darkness"]
LIGHT = bl.ELEMENTS["light"]
FIRE = bl.ELEMENTS["fire"]
METAL = bl.ELEMENTS["metal"]


def phase(i, offset):
    """0..1 through a six-frame loop. Every particle runs on i/FRAMES, so frame 6 is frame 0."""
    return (i / float(FRAMES) + offset) % 1.0


# --- slow ---------------------------------------------------------------------------------------

def slow(i, n):
    """Streaks dragging off the feet, as if the air had thickened round them. Feet row 20.

    Sideways, at foot height, and slow (the layer plays this row at the lowest rate): the only
    mark here whose motion is horizontal, which is what tells it from freeze at a glance even
    though both are Ice. Drawn OVER the body, unlike freeze: drawn under it, a cloaked enemy hid
    nearly all of it, and a slow is the status a player most needs to see working.
    """
    c = raster.Canvas(CELL, CELL)
    # (start x, row, direction, phase offset). Pairs leave each side half a loop apart.
    streaks = [(10, 17, -1, 0.0), (14, 19, 1, 0.25), (10, 21, -1, 0.5), (14, 18, 1, 0.75)]
    for x0, y, d, off in streaks:
        p = phase(i, off)
        head = x0 + d * int(round(p * 9))
        length = 4 + int(round(math.sin(p * math.pi) * 4))
        # Brightest at the head, cooling into the tail. The edge tone is the dark side, so a streak
        # still has a boundary against a pale floor.
        hot = p < 0.6
        tones = ([ICE[0], ICE[0], ICE[1], ICE[1], ICE[2], ICE[2], ICE[3], ICE[3]] if hot
                 else [ICE[1], ICE[1], ICE[2], ICE[2], ICE[3], ICE[3], ICE[3], ICE[3]])
        for k in range(min(length, len(tones))):
            c.set(head - d * k, y, tones[k])
        # A two-pixel-deep head, shaded underneath, so a streak is a stroke and not a hairline.
        for k in range(3):
            c.set(head - d * k, y + 1, ICE[2] if (hot and k == 0) else ICE[3])
    # Two motes lifting off the ground, slower than anything else in the sheet.
    for mx, off in ((4, 0.1), (19, 0.6)):
        p = phase(i, off)
        y = 18 - int(round(p * 8))
        x = mx + int(round(math.sin(p * TAU)))
        c.set(x, y, ICE[0] if p < 0.6 else ICE[1])
        c.set(x + 1, y, ICE[1])
        c.set(x, y + 1, ICE[2])
        c.set(x + 1, y + 1, ICE[3])
    return c


# --- freeze -------------------------------------------------------------------------------------

def freeze(i, n):
    """A crust of ice round the feet, with shards standing out of it. Feet row 20.

    Static except for one glint that walks round the rim - a freeze is the one status where the
    enemy itself stops moving (the layer's owner pauses its animation), so the mark must not be
    busy. The back of the ellipse is hidden by the legs it is drawn under; the shards are on the
    sides and front where they show.
    """
    c = raster.Canvas(CELL, CELL)
    cx, cy, rx, ry = 11.5, 20.5, 10.5, 3.2
    for y in range(CELL):
        for x in range(CELL):
            nx = (x - cx) / rx
            ny = (y - cy) / ry
            d = nx * nx + ny * ny
            if d > 1.0:
                continue
            # Lit along the upper-left rim, dark to the lower right: the key light, on a flat sheet.
            if d > 0.62:
                c.set(x, y, ICE[1] if (nx + ny) < -0.2 else ICE[2] if (nx + ny) < 0.5 else ICE[3])
            else:
                c.set(x, y, ICE[3])
    # Interior frost: a few hot pixels so the patch reads as ice, not paint.
    for x, y in ((6, 20), (9, 21), (13, 19), (16, 21), (11, 22)):
        c.set(x, y, ICE[2])

    # Shards: (base x, base y, height). Left column lit, right column in shade, tip white.
    shards = [(1, 20, 6), (4, 22, 4), (8, 23, 5), (14, 23, 4), (18, 22, 5), (21, 20, 6), (5, 18, 3), (18, 18, 3)]
    for bx, by, h in shards:
        for k in range(h):
            y = by - k
            c.set(bx, y, ICE[1] if k < h - 1 else ICE[0])
            if k < h - 2:
                c.set(bx + 1, y, ICE[3])
        c.set(bx - 1, by, ICE[2])

    # The glint, once round the rim per loop, starting at the front.
    a = TAU * 0.25 + TAU * (i / float(FRAMES))
    gx = int(round(cx + math.cos(a) * (rx - 0.6)))
    gy = int(round(cy + math.sin(a) * (ry - 0.4)))
    c.set(gx, gy, ICE[0])
    for dx, dy in ((1, 0), (-1, 0), (0, -1), (0, 1)):
        c.set(gx + dx, gy + dy, ICE[1])
    return c


# --- poison -------------------------------------------------------------------------------------

def bubble(c, x, y, r):
    """A hollow bubble: a ring, white-hot on its upper left and the edge tone low right.

    The hole is the point. A 3x3 ring has a one-pixel hole and reads as a filled square at x2, so
    the smallest true bubble here is 4x4 round a 2x2 hole.
    """
    if r == 0:
        c.set(x, y, POISON[2])
        c.set(x + 1, y, POISON[3])
        c.set(x, y - 1, POISON[1])
        return
    size = 4 if r == 1 else 6
    lo, hi = 0, size - 1
    for dy in range(size):
        for dx in range(size):
            edge = dx in (lo, hi) or dy in (lo, hi)
            corner = dx in (lo, hi) and dy in (lo, hi)
            if not edge or corner:
                continue
            tone = POISON[3] if dx + dy > size - 1 else POISON[2]
            c.set(x + dx - size // 2, y + dy - size // 2, tone)
    c.set(x - size // 2 + 1, y - size // 2 + 1, POISON[0])
    c.set(x - size // 2 + 1, y - size // 2, POISON[1])


def poison(i, n):
    """Bubbles rising off the shoulder and bursting above it. Pivot: bottom centre, row 21.

    Hollow rings rather than dots, so a bubble can never be mistaken for a slow mote, and upward,
    which nothing else in the sheet is.
    """
    c = raster.Canvas(CELL, CELL)
    for x0, off, big in ((8, 0.0, 1), (15, 1.0 / 3.0, 2), (11, 2.0 / 3.0, 1)):
        p = phase(i, off)
        y = 21 - int(round(p * 17))
        x = x0 + int(round(math.sin((p + off) * TAU) * 1.5))
        if p < 0.15:
            bubble(c, x, y, 0)
        elif p < 0.8:
            bubble(c, x, y, big)
        else:
            # The pop: four droplets flung out from where it was.
            for dx, dy in ((-2, -1), (2, -1), (-1, 1), (1, 1)):
                c.set(x + dx, y + dy, POISON[1])
            c.set(x, y - 2, POISON[0])
    return c


# --- vulnerable ---------------------------------------------------------------------------------

def vulnerable(i, n):
    """Brackets closing on a cracked diamond: "this one breaks". Pivot: bottom centre, row 21.

    The only mark that holds still in space. It pulses inward instead, which is the read of a
    target being locked rather than of something happening to the enemy.
    """
    c = raster.Canvas(CELL, CELL)
    cx, cy = 12, 14
    r = [7, 6, 5, 5, 6, 7][i % FRAMES]
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        ox, oy = cx + sx * r, cy + sy * r
        # An L of two-pixel arms pointing back toward the centre, with its cast shadow.
        pts = [(ox, oy), (ox - sx, oy), (ox - 2 * sx, oy), (ox, oy - sy), (ox, oy - 2 * sy)]
        for x, y in pts:
            c.set(x + 1, y + 1, OCC)
        for x, y in pts:
            c.set(x, y, DARK[2])
        c.set(ox, oy, DARK[0] if r <= 5 else DARK[1])

    # The diamond, lit upper left.
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            if abs(dx) + abs(dy) > 3:
                continue
            if abs(dx) + abs(dy) == 3:
                tone = DARK[1] if dx + dy < 0 else DARK[3]
            else:
                tone = DARK[2]
            c.set(cx + dx, cy + dy, tone)
    for dx, dy in ((1, 3), (2, 2), (3, 1), (0, 4)):
        c.set(cx + dx, cy + dy, OCC)
    # The crack. Bright on the beat the brackets are tightest.
    crack = DARK[0] if r <= 5 else DARK[1]
    for dx, dy in ((0, -3), (0, -2), (-1, -1), (0, 0), (1, 1), (0, 2), (0, 3)):
        c.set(cx + dx, cy + dy, crack)
    c.set(cx - 2, cy - 1, DARK[3])
    return c


# --- weaken -------------------------------------------------------------------------------------

def chevron(c, x, y, top, under):
    """A downward V, two pixels thick, with an occ shadow under it."""
    arm = [(-3, 0), (-2, 1), (-1, 2), (0, 3), (1, 2), (2, 1), (3, 0)]
    for dx, dy in arm:
        c.set(x + dx, y + dy + 2, OCC)
    for dx, dy in arm:
        c.set(x + dx, y + dy + 1, under)
        c.set(x + dx, y + dy, top)


def weaken(i, n):
    """Chevrons sinking: whatever this enemy hits with is going down. Pivot: centre, row 10.

    Gold, geometric and evenly spaced, against wither's dark tumbling flakes - both fall, so the
    two are told apart by shape and by value, not by direction.
    """
    c = raster.Canvas(CELL, CELL)
    for k in range(2):
        y = 2 + (i + 6 * k) % 12
        if y >= 11:
            top, under = LIGHT[3], LIGHT[3]
        elif y <= 3:
            top, under = LIGHT[2], LIGHT[3]
        else:
            top, under = LIGHT[0], LIGHT[2]
        chevron(c, 12, y, top, under)
    return c


# --- wither -------------------------------------------------------------------------------------

def wither(i, n):
    """Ash flakes with a dying ember in each, tumbling down the body. Pivot: centre, row 11.

    The ember cools as it falls - hot at the top, dull red lower, ash alone at the bottom - which
    is the "burned away" of the mechanic drawn as a fleck's whole life. Flakes are a 2x2 block
    plus a corner, not a pixel or two: anything smaller vanished against a dark body at x2.
    """
    c = raster.Canvas(CELL, CELL)
    ash = METAL[3]
    shadow = bl.tone("steel", "deep")
    for k, x0 in enumerate((3, 8, 13, 18, 21, 6)):
        p = phase(i, k / 6.0)
        y = 1 + int(round(p * 19))
        x = x0 + int(round(math.sin(p * TAU * 1.5 + k) * 2))
        ember = FIRE[0] if p < 0.2 else FIRE[1] if p < 0.45 else FIRE[2] if p < 0.7 else FIRE[3] if p < 0.85 else None
        # The ember sits on a different corner each frame and a stray corner pixel flips side,
        # which is what makes a flake tumble rather than slide. Three states, so the cycle
        # divides the six-frame loop and frame six is frame zero.
        turn = (i + k) % 3
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            c.set(x + dx, y + dy, ash)
        c.set(x + 1, y + 2, shadow)
        c.set(x + 2, y + 2, shadow)
        spur = [(-1, 0), (2, 1), (0, 2)][turn]
        c.set(x + spur[0], y + spur[1], ash)
        if ember:
            corner = [(0, 0), (1, 0), (1, 1)][turn]
            c.set(x + corner[0], y + corner[1], ember)
    return c


# Order is StatusVisual order in StatusEffectLayer.cs - do not reorder one without the other.
ROWS = [
    ("slow", slow),
    ("freeze", freeze),
    ("poison", poison),
    ("vulnerable", vulnerable),
    ("weaken", weaken),
    ("wither", wither),
]


def main():
    from PIL import Image

    d = os.path.dirname(OUT)
    if not os.path.isdir(d):
        os.makedirs(d)

    sheet = Image.new("RGBA", (CELL * FRAMES, CELL * len(ROWS)), (0, 0, 0, 0))
    ok = True
    for r, (name, fn) in enumerate(ROWS):
        for i in range(FRAMES):
            c = fn(i, FRAMES)
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s frame %d:" % (name, i))
                for h, cnt in bad[:5]:
                    print("   %s  x%d" % (h, cnt))
            sheet.alpha_composite(c.img, (i * CELL, r * CELL))
        print("  %-12s %d frames" % (name, FRAMES))
    sheet.save(OUT)

    # A preview on the two floors that matter: the reference stone, and a pale one standing in for
    # the Ice chapter, where a light mark is most at risk of disappearing.
    zoom = 6
    floors = [bl.tone("stone", "base"), bl.tone("steel", "lit")]
    pw = CELL * FRAMES * zoom
    ph = CELL * len(ROWS) * zoom
    preview = Image.new("RGBA", (pw * len(floors) + 12 * (len(floors) + 1), ph + 24), (5, 7, 12, 255))
    big = sheet.resize((pw, ph), Image.NEAREST)
    for f, floor in enumerate(floors):
        x = 12 + f * (pw + 12)
        preview.paste(raster.rgb(floor), (x, 12, x + pw, 12 + ph))
        preview.alpha_composite(big, (x, 12))
    preview.convert("RGB").save(PREVIEW)

    if not ok:
        return 1
    print("")
    print("%d status marks, %d frames, palette clean -> %s" % (len(ROWS), len(ROWS) * FRAMES,
                                                             os.path.relpath(OUT, ROOT)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
