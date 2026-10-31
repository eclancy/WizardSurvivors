# -*- coding: utf-8 -*-
"""Attack icons - the glyph an enemy shows over its head while it winds up an attack.

Run: python tools/art/telegraph_icons.py

One sheet, assets/bonelight/effects/telegraph-icons.png: six rows (one per glyph) of four 16x16
cells, authored 1:1 and drawn at x3 by TelegraphIcon (x4 over a boss) - 48 world units, the status
marks' size; at x2 it was a speck on the phone.

WHY ICONS (Eric, playing 0.9.2): "the attacks on enemies that are telegraphed through transparent
buildups animations are weird. like the jump attacks. remove them. if you need to telegraph an
attack, do so with icon animation." Every wind-up used to be a translucent shape growing on the
floor - a lunger's streak, a slammer's swelling disc. Now the attacker carries a glyph that pops in
empty, fills from the bottom as the wind-up runs out, blinks in its last third, and flashes white
on the strike. Where the ground matters (a ring, a cone, a lane) TelegraphOutline marks it with an
opaque dashed outline; the icon is the clock, the outline is the place.

THE ROW ORDER IS AN API: TelegraphGlyph's order (scripts/TelegraphIcon.cs).

  alert   "!"                    a special that is not a strike: the dark, a sweeping beam
  leap    three claw slashes     a lunge or a charge - it is coming AT you
  slam    an arrow onto the ground  a ring or a wedge struck round the attacker, or a hit falling
  shot    a reticle              a bolt, a fan of missiles, a lobbed flame
  burst   an eight-point star    it blows up, or screams, all round itself
  summon  a skull                it is calling up the dead

THE COLUMN ORDER IS AN API TOO: TelegraphIcon draws column 0 whole and the lit column over it,
clipped from the bottom, so the glyph fills like a gauge.

  0 empty   steel, unlit: the glyph the frame it appears
  1 full    the fire ramp: lit
  2 hot     a step hotter: the blink in the last third of the wind-up
  3 flash   white-hot: the strike

One warning ramp for every attacker, fire, on purpose. The world is cold, so warm reads first
(.ai/art-direction.md section 3), and an icon that changed hue per enemy would be one more thing to
learn; the attacker's own colour is on the outline on the ground. Every glyph has a 1px occlusion
outline, which is what keeps it readable over a pale floor and over the swarm.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "effects", "telegraph-icons.png")
PREVIEW = os.path.join(ROOT, "tools", "art", "_telegraph_icons_preview.png")

CELL = 16
OCC = bl.OCC
FIRE = bl.ELEMENTS["fire"]          # core hot mid edge
STEEL_LIT = bl.tone("steel", "lit")
STEEL_BASE = bl.tone("steel", "base")
STEEL_SHADE = bl.tone("steel", "shade")


def from_rows(rows):
    """A 16x16 mask from sixteen strings of sixteen characters, '#' set."""
    assert len(rows) == CELL, len(rows)
    mask = set()
    for y, row in enumerate(rows):
        assert len(row) == CELL, (y, row)
        for x, ch in enumerate(row):
            if ch == "#":
                mask.add((x, y))
    return mask


def alert():
    return from_rows([
        "................",
        "......####......",
        ".....######.....",
        ".....######.....",
        ".....######.....",
        ".....######.....",
        "......####......",
        "......####......",
        "......####......",
        ".......##.......",
        "................",
        "......####......",
        ".....######.....",
        ".....######.....",
        "......####......",
        "................",
    ])


def leap():
    """Three claw slashes, rising to the right, each tapering to a point at both ends."""
    mask = set()
    # (x at the foot, foot row, x at the tip, tip row)
    for x0, y0, x1, y1 in ((2, 13, 5, 3), (6, 14, 10, 2), (11, 12, 13, 4)):
        for y in range(y1, y0 + 1):
            t = (y0 - y) / float(y0 - y1)
            x = int(round(x0 + (x1 - x0) * t))
            mask.add((x, y))
            # Two wide in the middle of the stroke, one at the ends: a slash, not a bar.
            if y1 < y < y0:
                mask.add((x + 1, y))
    return mask


def slam():
    return from_rows([
        "................",
        "......####......",
        "......####......",
        "......####......",
        "......####......",
        "..############..",
        "...##########...",
        "....########....",
        ".....######.....",
        "..#...####...#..",
        "...#...##...#...",
        "................",
        ".##############.",
        ".##############.",
        "................",
        "................",
    ])


def shot():
    """A reticle: a ring, four ticks crossing into it, and the point it is laid on."""
    mask = set()
    cx = cy = 7.5
    for y in range(CELL):
        for x in range(CELL):
            d = math.hypot(x - cx, y - cy)
            if 4.4 <= d <= 6.3:
                mask.add((x, y))
    for k in range(1, 5):
        for a, b in ((7, k), (8, k), (7, 15 - k), (8, 15 - k), (k, 7), (k, 8), (15 - k, 7), (15 - k, 8)):
            mask.add((a, b))
    for a in (7, 8):
        for b in (7, 8):
            mask.add((a, b))
    return mask


def burst():
    """An eight-point star: everything round it is hit. Drawn by hand - a star rasterised from a
    polygon at this size loses its diagonal points to single pixels the outline swallows."""
    return from_rows([
        "................",
        ".......##.......",
        ".##....##....##.",
        ".###..####..###.",
        "..###.####.###..",
        "...##########...",
        "....########....",
        ".##############.",
        ".##############.",
        "....########....",
        "...##########...",
        "..###.####.###..",
        ".###..####..###.",
        ".##....##....##.",
        ".......##.......",
        "................",
    ])


def summon():
    return from_rows([
        "................",
        ".....######.....",
        "...##########...",
        "..############..",
        "..############..",
        "..###..##..###..",
        "..##...##...##..",
        "..##...##...##..",
        "..###..##..###..",
        "...####..####...",
        "....###..###....",
        ".....######.....",
        ".....#.##.#.....",
        ".....######.....",
        "................",
        "................",
    ])


# THE ORDER IS TelegraphGlyph's.
ROWS = [
    ("alert", alert),
    ("leap", leap),
    ("slam", slam),
    ("shot", shot),
    ("burst", burst),
    ("summon", summon),
]

# THE ORDER IS TelegraphIcon's columns.
COLUMNS = ["empty", "full", "hot", "flash"]

# Per column: (lit edge, body, shaded edge). Lit is the upper-left (the key light), shaded the
# lower-right, as every Bonelight form is.
TONES = {
    "empty": (STEEL_LIT, STEEL_BASE, STEEL_SHADE),
    "full": (FIRE[1], FIRE[2], FIRE[3]),
    "hot": (FIRE[0], FIRE[1], FIRE[2]),
    "flash": (FIRE[0], FIRE[0], FIRE[1]),
}


def paint(mask, column):
    c = raster.Canvas(CELL, CELL)
    lit, body, shade = TONES[column]
    for (x, y) in mask:
        if (x, y - 1) not in mask or (x - 1, y) not in mask:
            c.set(x, y, lit)
        elif (x, y + 1) not in mask or (x + 1, y) not in mask:
            c.set(x, y, shade)
        else:
            c.set(x, y, body)
    # The outline: every cell touching the glyph (eight ways - four ways leaves a checkerboard along
    # every diagonal) that is not in it, which also fills the skull's eyes and teeth and the reticle's
    # middle with the dark.
    for y in range(CELL):
        for x in range(CELL):
            if (x, y) in mask:
                continue
            if any((x + dx, y + dy) in mask for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                c.set(x, y, OCC)
    return c


def main():
    from PIL import Image

    d = os.path.dirname(OUT)
    if not os.path.isdir(d):
        os.makedirs(d)

    sheet = Image.new("RGBA", (CELL * len(COLUMNS), CELL * len(ROWS)), (0, 0, 0, 0))
    ok = True
    for r, (name, fn) in enumerate(ROWS):
        mask = fn()
        for (x, y) in mask:
            # The outline needs the cell's border free: a glyph touching its edge loses its outline there.
            if x < 1 or y < 1 or x > CELL - 2 or y > CELL - 2:
                ok = False
                print("GLYPH %s touches the cell border at (%d, %d)" % (name, x, y))
        for col, column in enumerate(COLUMNS):
            c = paint(mask, column)
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s %s:" % (name, column))
                for h, cnt in bad[:5]:
                    print("   %s  x%d" % (h, cnt))
            sheet.alpha_composite(c.img, (col * CELL, r * CELL))
        print("  %-8s %d px" % (name, len(mask)))
    sheet.save(OUT)

    # A preview on the two floors that matter: the reference stone, and a pale one standing in for the
    # Ice chapter, where a dark outline is what keeps a glyph from dissolving into the ground.
    zoom = 8
    floors = [bl.tone("stone", "base"), bl.tone("steel", "lit")]
    pw = CELL * len(COLUMNS) * zoom
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
    print("%d attack icons x %d states, palette clean -> %s" % (len(ROWS), len(COLUMNS), os.path.relpath(OUT, ROOT)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
