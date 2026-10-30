# -*- coding: utf-8 -*-
"""Open water: the surface, the shore band and the moving ripples. Run: python tools/art/water.py

WATER IS NOT TILES ANY MORE. Until 2026-10 a pond was fourteen autotile roles, each a whole 48 px
cell of saturated `azure` with a rounded shore cut into it - which on screen was a bright royal-blue
staircase, the most artificial thing in every Forest and Swamp photograph. Now the SHAPE is
computed (scripts/WaterShape.cs: smooth shorelines at sub-cell resolution, straight diagonals,
round corners, the same polygons the feet collide with) and drawn by a shader
(scenes/shaders/water_surface.gdshader) from a distance field. This file draws what that shader
colours it with, per biome:

  <style>_surface.png   192x192, tiles seamlessly in world space. The body of the water: one dark
                        tone, broad soft patches of a second (shallows, a reflected canopy), a few
                        horizontal reflection streaks and three or four glints. 192 and not 48 so
                        the repeat is four cells wide and the eye does not find it.
  <style>_tones.png     8x1: the shore band and the moving marks - see TONE_SLOTS.
  ripple.png            128x128 mask of ripple crests, shared. The shader slides two copies of it
                        across the surface in different directions, slowly, so the water moves
                        without any pixel changing more than about once a second.

THE PALETTE IS THE BIOME'S, NOT WATER'S. `azure` is player pigment (`.ai/art-direction.md` section 3,
reserved to the Frostweaver) and the old water wore it; it was also two value steps brighter than
any ground it lay on. Every tone here comes from the material rows the ground itself is drawn from,
one or two steps darker than the floor round it, so a pond sits in the stage's value range and
reads as water by its SHAPE, its shore and its reflections rather than by being the brightest thing
on screen. Forest ponds are deep blue-teal, the Swamp's murk is grey-green with brown in it and
duckweed on it, the Frozen Waste's water is black-blue with a pale ice shelf for a shore, and
anything else (a cave or castle pool) is near-black stone-blue.

THE THREE RENDERS THAT WERE THROWN AWAY (`.ai/terrain.md` section 3) still hold: no sine bands, no
per-pixel hashed mottle, no blocky depth mosaic. Every mark here is a shape - a patch, a streak,
a glint - and there are few of them.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "tiles", "water")

SURFACE = 192
RIPPLE = 128
TAU = math.pi * 2.0


def t(mat, role):
    return bl.tone(mat, role)


# What each slot of <style>_tones.png is for. The shader reads them by index (TONE_* constants in
# water_surface.gdshader), so the ORDER IS AN API.
TONE_SLOTS = [
    "lip_outer",   # 0  the damp earth furthest from the water, dithered out into the ground
    "lip_inner",   # 1  wet mud at the very edge
    "waterline",   # 2  the first pixel of water - the bank's shadow (or, frozen, the ice shelf)
    "edge",        # 3  dark water near the bank, dithered into the body
    "ripple",      # 4  the moving crests
    "glint",       # 5  the brightest thing on the water, used a handful of times
    "scum",        # 6  a second moving mark: duckweed in the swamp, slush on the frozen water
    "shallows",    # 7  lit shallows under the sunward (down-right) bank
]

# Per biome. body/alt/streak/glint/scum paint the surface; the rest fill TONE_SLOTS.
STYLES = {
    # A woodland pond: deep blue-teal under the turf's teal, with the canopy reflected in broad
    # patches of the lichen row. Wet earth lip in the lichen and linen deeps.
    "forest": dict(
        body=t("arcane", "deep"), alt=t("lichen", "deep"), streak=t("arcane", "shade"),
        glint=t("lichen", "lit"), scum=None,
        lip_outer=t("lichen", "deep"), lip_inner=t("linen", "deep"),
        waterline=t("stone", "deep"), edge=t("stone", "shade"),
        ripple=t("arcane", "shade"), glint_tone=t("lichen", "hi"), scum_tone=t("lichen", "deep"),
        shallows=t("arcane", "shade"),
    ),
    # Swamp murk: grey-green, darker than the mud, with duckweed on it. Brown silt patches were
    # tried and read as stains.
    "swamp": dict(
        body=t("linen", "deep"), alt=t("lichen", "deep"), streak=t("linen", "shade"),
        glint=t("linen", "lit"), scum=t("verdant", "shade"),
        lip_outer=t("gold", "deep"), lip_inner=t("linen", "deep"),
        waterline=t("stone", "deep"), edge=t("stone", "deep"),
        ripple=t("linen", "base"), glint_tone=t("linen", "lit"), scum_tone=t("verdant", "deep"),
        shallows=t("linen", "shade"),
    ),
    # Frozen Waste: black-blue water with a pale ice shelf for a shore and frost on the ground.
    "frozen": dict(
        body=t("arcane", "deep"), alt=t("stone", "shade"), streak=t("arcane", "shade"),
        glint=t("steel", "base"), scum=t("steel", "shade"),
        lip_outer=t("steel", "shade"), lip_inner=t("skin", "shade"),
        waterline=t("skin", "deep"), edge=t("steel", "deep"),
        ripple=t("steel", "deep"), glint_tone=t("steel", "lit"), scum_tone=t("steel", "deep"),
        shallows=t("steel", "deep"),
    ),
    # Any other stage (a cave or castle pool): near-black stone-blue on stone.
    "pool": dict(
        body=t("stone", "deep"), alt=t("arcane", "deep"), streak=t("stone", "base"),
        glint=t("arcane", "shade"), scum=None,
        lip_outer=t("stone", "deep"), lip_inner=t("linen", "deep"),
        waterline=bl.OCC, edge=t("stone", "deep"),
        ripple=t("stone", "base"), glint_tone=t("arcane", "base"), scum_tone=t("stone", "shade"),
        shallows=t("stone", "shade"),
    ),
    # THE ABYSS (the Last Bridge, .ai/endgame-castle.md 3.2): not water you could stand at the edge of,
    # but the drop off the side of the bridge - the dark sea a long way below. So the body is the darkest
    # thing on screen, `occ`, the absence of a floor, as the old maze void was; what says "far below" is
    # mist lying over it in a half-tone of stone's deep stop (the generic patches, and the drifting
    # ripple marks, which here are mist moving, not wavelets), and the sea under the mist in a few long
    # broken streaks of arcane's deep stop - dark blue, never a highlight. There is no bank: the deck
    # ends at a kerb, so the lip on the deck is stone's shade and deep (a contact shadow, not wet earth),
    # the first pixel over the edge is `occ`, and the dark band under it is the side of the bridge going
    # down, in stone's deep stop - wide on the shores facing the viewer (the bottom-right drop, where
    # the bridge's flank shows), narrow under the far parapet. The "sunward shallows" slot is mist lit
    # by nothing: stone's deep stop again, so the top-left drop never grows a lighter band.
    "abyss": dict(
        body=bl.OCC, alt=t("stone", "deep"), streak=t("arcane", "deep"),
        glint=t("stone", "shade"), scum=None,
        lip_outer=t("stone", "shade"), lip_inner=t("stone", "deep"),
        waterline=bl.OCC, edge=t("stone", "deep"),
        ripple=t("stone", "deep"), glint_tone=t("stone", "shade"), scum_tone=t("stone", "deep"),
        shallows=t("stone", "deep"),
        # Mist: more of the half-tone than open water gets (up to half the pixels, not three in eight),
        # and longer streaks - the sea seen through it is a swell a long way down, not ripples close by.
        cover=0.5, streak_run=(10, 26), streak_groups=6,
    ),
}

# Each style's surface seed. They were 11 + 17 * (its place in the sorted styles) when there were four;
# written out so a style added later (the abyss) cannot shift the others' seeds and redraw their water.
SEEDS = {"forest": 11, "frozen": 28, "pool": 45, "swamp": 62, "abyss": 83}


class Rand(object):
    """Deterministic, so the art comes out identical on every run."""

    def __init__(self, seed):
        self.s = (seed * 2654435761 + 12345) & 0x7FFFFFFF

    def f(self):
        self.s = (self.s * 1103515245 + 12345) & 0x7FFFFFFF
        return (self.s >> 8) / float(1 << 23)

    def i(self, lo, hi):
        return lo + int(self.f() * (hi - lo + 1)) if hi > lo else lo


def field(x, y, waves):
    """A smooth field that tiles over SURFACE: a sum of waves with whole-number periods."""
    v = 0.0
    for a, i, j, ph in waves:
        v += a * math.sin(TAU * (i * x + j * y) / float(SURFACE) + ph)
    return v


def surface(style, seed):
    s = STYLES[style]
    r = Rand(seed)
    c = raster.Canvas(SURFACE, SURFACE, s["body"])
    # Broad patches: a low-frequency field over the tile, shown as a HALF-TONE of a second tone -
    # never solid. Solid patches of it, in any colour, read as camouflage blobs printed on the
    # water; at most three pixels in eight of the alt tone, ordered, reads as the water being a
    # little murkier there. Frequencies 1..3 per 192 px: patches 30-90 px across.
    waves = [(1.0 / k, r.i(1, 3) * (1 if r.f() < 0.5 else -1), r.i(0, 3), r.f() * TAU)
             for k in range(1, 6)]
    vals = [[field(x, y, waves) for x in range(SURFACE)] for y in range(SURFACE)]
    flat = sorted(v for row in vals for v in row)
    lo, hi = flat[int(len(flat) * 0.55)], flat[int(len(flat) * 0.97)]
    dither = raster.BAYER8
    # How much of the second tone a patch may carry at most, and the streaks' length and count: the
    # open-water numbers unless the style says otherwise (only the abyss does).
    most = s.get("cover", 0.375)
    run_lo, run_hi = s.get("streak_run", (5, 16))
    for y in range(SURFACE):
        for x in range(SURFACE):
            cover = min(most, max(0.0, (vals[y][x] - lo) / (hi - lo)) * most)
            if cover * 64.0 > dither[y % 8][x % 8]:
                c.set(x, y, s["alt"])
    # Reflection streaks: short horizontal lines in loose rows, as a reflection breaks up on a
    # surface that is not quite still. Grouped, and few groups: a field of single dashes is the
    # knitted chevron again.
    for g in range(s.get("streak_groups", 8)):
        gx, gy = r.i(0, SURFACE - 1), r.i(0, SURFACE - 1)
        for k in range(r.i(3, 5)):
            x0 = gx + r.i(-10, 10)
            y0 = gy + k * r.i(2, 4)
            run = r.i(run_lo, run_hi)
            gap = r.i(2, run - 3) if run > 8 and r.f() < 0.6 else -1
            for j in range(run):
                if j == gap or j == gap + 1:
                    continue
                c.set((x0 + j) % SURFACE, y0 % SURFACE, s["streak"])
    # Glints: a lit pixel with a dimmer one beside it, four per tile.
    for k in range(4):
        gx, gy = r.i(0, SURFACE - 1), r.i(0, SURFACE - 1)
        c.set(gx, gy, s["glint"])
        c.set((gx + 1) % SURFACE, gy, s["streak"])
    # Duckweed (swamp) / slush (frozen): small clumps, not specks.
    if s["scum"]:
        for k in range(10):
            cx, cy = r.i(0, SURFACE - 1), r.i(0, SURFACE - 1)
            for n in range(r.i(4, 9)):
                x, y = cx + r.i(-3, 3), cy + r.i(-2, 2)
                c.set(x % SURFACE, y % SURFACE, s["scum"])
                if r.f() < 0.5:
                    c.set((x + 1) % SURFACE, y % SURFACE, s["scum"])
    return c


def tones(style):
    s = STYLES[style]
    order = [s["lip_outer"], s["lip_inner"], s["waterline"], s["edge"], s["ripple"],
             s["glint_tone"], s["scum_tone"], s["shallows"]]
    c = raster.Canvas(len(order), 1, None)
    for i, col in enumerate(order):
        c.set(i, 0, col)
    return c


def ripple():
    """Ripple crests: shallow arcs a few pixels wide, two pixels tall at the middle. A mask - the
    shader colours it - drawn in bone so it passes the audit."""
    c = raster.Canvas(RIPPLE, RIPPLE, None)
    r = Rand(4711)
    mark = bl.tone("skin", "hi")
    for k in range(9):
        x0, y0 = r.i(0, RIPPLE - 1), r.i(0, RIPPLE - 1)
        run = r.i(5, 9)
        for j in range(run):
            lift = 1 if 1 <= j < run - 1 else 0
            c.set((x0 + j) % RIPPLE, (y0 - lift) % RIPPLE, mark)
    return c


def fill_tile():
    """A 48x48 crop of the forest surface: the single `water_fill` tile tiles.json keeps, so the
    manifest still says water exists and blocks. Nothing paints it - see the module docstring."""
    src = surface("forest", 11)
    c = raster.Canvas(48, 48, None)
    for y in range(48):
        for x in range(48):
            c.set(x, y, src.get(x, y))
    return c


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    ok = True
    out = []
    for style in sorted(STYLES):
        out.append(("%s_surface" % style, surface(style, SEEDS[style])))
        out.append(("%s_tones" % style, tones(style)))
    out.append(("ripple", ripple()))
    for name, c in out:
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in water/%s: %s" % (name, bad[:4]))
            continue
        c.save(os.path.join(OUT_DIR, name + ".png"))
        print("  water/%-16s %dx%d" % (name, c.w, c.h))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
