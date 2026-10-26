# -*- coding: utf-8 -*-
"""The Sleet Runes rune. Run: python tools/art/sleet_runes.py

One rune of rime carried on the wind, drawn at 32 headings, written to assets/bonelight/effects/:

  sleet-rune.png     32x32 cell, 32 cells in one strip: cell i points at i x 11.25 degrees, measured
                     the way Godot measures an angle (0 = +X, clockwise on screen, since y is down)

The cell count is an API: scripts/SleetRunes.cs reads it as Directions and picks the cell nearest a
rune's heading. A rune is a true projectile that flies the way the player faces - a keyboard gives
eight headings and a stick gives any - so the sheet is pre-rotated rather than the sprite turned by the
engine: pixel art rotated 37 degrees by a transform smears its edges across half-pixels, and one
rasterised at 33.75 degrees keeps every pixel on the grid. Thirty-two is enough that the drawn rune is
never more than 5.6 degrees off the line it flies.

WHAT IT IS. A spell, not a thrown object - the developer, of a first build that threw knives: "I still
want spells themed around magic". So nothing here is a blade or a hilt. It is a sigil: a diamond frame
of rime with a lit point at each corner, a ring inside it and a white-hot heart (the contract: every
element's core is white-hot), and behind it a tail of sleet - Ice's ramp banded with Wind's, because
Sleet Runes is Ice and Wind and the wind is what carries it. The tail says which way it flies at a
glance; the frame says it is magic. Authored 1:1 and shown x2 (.ai/art-direction.md section 1, the
projectile cell): 64 px in the world, the frame 14 art px long - about a skeleton's width on the ground.

The frame's lit edge faces the key light (upper left, section 2), computed per heading, and the whole
rune is ringed in occlusion: a pale blue sigil on a deep blue floor is the cold-element case section 3
warns about.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")

TAU = math.pi * 2.0
OCC = bl.OCC
CELL = 32
DIRECTIONS = 32

ICE_CORE, ICE_HOT, ICE_MID, ICE_EDGE = bl.ELEMENTS["ice"]
WIND_CORE, WIND_HOT, WIND_MID, WIND_EDGE = bl.ELEMENTS["wind"]

# The key light, in screen space: from the upper left.
KEY = (-1.0 / math.sqrt(2.0), -1.0 / math.sqrt(2.0))

# Rune space: u along the heading (it flies toward +u), v across it. The frame's centre sits a little
# ahead of the cell centre, so the node's position is the middle of frame and tail together.
UC = 2.5                     # the frame's centre along u
FRAME_U, FRAME_V = 7.0, 5.0  # the diamond's half-length and half-width
TAIL_FROM, TAIL_TO = -12.5, UC - FRAME_U + 1.0
TAIL_HALF = 2.4

SUB = 4                      # 4x4 samples a pixel
COVER = 0.45                 # a pixel is in if this much of it is


def part_at(u, v):
    """Which part of the rune a point in rune space is on, or None."""
    du = u - UC
    d = abs(du) / FRAME_U + abs(v) / FRAME_V
    r = math.sqrt(du * du + v * v)
    if d <= 1.0:
        for (tu, tv) in ((FRAME_U, 0.0), (-FRAME_U, 0.0), (0.0, FRAME_V), (0.0, -FRAME_V)):
            if (du - tu) ** 2 + (v - tv) ** 2 <= 1.6:
                return "tip"
        if r < 1.4:
            return "core"
        if 2.1 <= r <= 3.1:
            return "ring"
        if d >= 0.74:
            return "frame"
        return "fill"
    if TAIL_FROM <= u <= TAIL_TO:
        t = (u - TAIL_FROM) / (TAIL_TO - TAIL_FROM)
        if abs(v) <= TAIL_HALF * t ** 0.9:
            return "tail"
    return None


def colour_for(part, u, v, side_lit):
    if part in ("core", "tip"):
        return ICE_CORE
    if part == "ring":
        return ICE_HOT
    if part == "frame":
        return ICE_HOT if side_lit else ICE_MID
    if part == "fill":
        # The field inside the frame is the DARK of the ramp, so the ring and the heart read as a glyph
        # cut into it; filled with the mid tone the first draft read as a lump of ice.
        return ICE_EDGE
    if part == "tail":
        # Sleet banded with wind: alternate two-pixel bands of Ice and Wind, cooling toward the end.
        t = (u - TAIL_FROM) / (TAIL_TO - TAIL_FROM)
        band = int(math.floor((u - TAIL_FROM) / 2.0)) % 2 == 0
        if t > 0.6:
            return ICE_MID if band else WIND_MID
        return ICE_EDGE if band else WIND_EDGE
    return None


def rune(angle, scale=1.0, size=CELL, centre=None):
    """One rune flying along `angle` (radians, Godot's convention), on a size x size canvas. scale
    shrinks it (the card icon draws smaller ones behind the first); centre places it."""
    c = raster.Canvas(size, size, None)
    ca, sa = math.cos(angle), math.sin(angle)
    # The across axis, +90 degrees from the heading (screen: clockwise, since y is down).
    na = (-sa, ca)
    cx, cy = centre if centre else (size / 2.0, size / 2.0)
    filled = {}
    for py in range(size):
        for px in range(size):
            votes = {}
            inside = 0
            for sy in range(SUB):
                for sx in range(SUB):
                    x = px + (sx + 0.5) / SUB - cx
                    y = py + (sy + 0.5) / SUB - cy
                    u = (x * ca + y * sa) / scale
                    v = (x * na[0] + y * na[1]) / scale
                    part = part_at(u, v)
                    if part is None:
                        continue
                    inside += 1
                    votes[part] = votes.get(part, 0) + 1
            if inside < COVER * SUB * SUB:
                continue
            # The smallest parts win a tie, or the heart and the tips would vanish under the frame.
            order = {"core": 5, "tip": 4, "ring": 3, "frame": 2, "fill": 1, "tail": 0}
            part = max(votes.items(), key=lambda kv: (kv[1] * (1 + order[kv[0]]), order[kv[0]]))[0]
            x = px + 0.5 - cx
            y = py + 0.5 - cy
            u = (x * ca + y * sa) / scale
            v = (x * na[0] + y * na[1]) / scale
            # Which side of the frame this pixel is on, as a screen-space normal, against the key.
            du = u - UC
            nu = 1.0 if du >= 0.0 else -1.0
            nv = 1.0 if v >= 0.0 else -1.0
            normal = (ca * nu / FRAME_U + na[0] * nv / FRAME_V, sa * nu / FRAME_U + na[1] * nv / FRAME_V)
            side_lit = normal[0] * KEY[0] + normal[1] * KEY[1] > 0.0
            filled[(px, py)] = colour_for(part, u, v, side_lit)

    # Occlusion first, one pixel round everything, so the rune holds on any floor.
    for (px, py) in filled:
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                q = (px + dx, py + dy)
                if q not in filled:
                    c.set(q[0], q[1], OCC)
    for (px, py), colour in filled.items():
        c.set(px, py, colour)
    return c


def strip():
    sheet = raster.Canvas(CELL * DIRECTIONS, CELL, None)
    for i in range(DIRECTIONS):
        k = rune(i * TAU / DIRECTIONS)
        for y in range(CELL):
            for x in range(CELL):
                p = k.get(x, y)
                if p[3] > 0:
                    sheet.set(i * CELL + x, y, p)
    return sheet


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    sheet = strip()
    bad = sheet.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in sleet-rune (art-direction.md section 3):")
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return 1
    path = os.path.join(OUT_DIR, "sleet-rune.png")
    sheet.scaled(2).save(path)
    print("  %-40s %dx%d (%d headings)" % ("sleet-rune", CELL * DIRECTIONS * 2, CELL * 2, DIRECTIONS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
