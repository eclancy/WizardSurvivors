# -*- coding: utf-8 -*-
"""Interactables - the chest, the two traps, the two pickups, the shield aura - and one portrait.

Run: python tools/art/props_fx.py

Seven animations, thirty-two frames, replacing the last of the bought dungeon items-and-traps
pack and the last frame of the bought magic-effects pack. That art is licensed for use and not
for redistribution - see `.ai/asset-licensing.md`.

THESE ARE OBJECTS IN THE WORLD, so they follow the relic rule rather than the spell rule: drawn
on the material rows, lit from the upper left, and touching an element ramp only where the thing
genuinely glows - the light coming out of an open chest, the fire in a vent, the liquid in a
flask. The one exception is the shield aura, which is not an object at all.

EVERY ONE IS ON AN INTEGER SCALE. The pack art was 16x16 shown at x3 and 72x72 shown at x1, which
meant the traps, the chest and the pickups were all a different number of screen pixels per source
pixel from everything else in the game. These are the 32x32 cell (48x48 for the aura) rendered at
x2, so a source pixel is always exactly two screen pixels at scale 1.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "props")
OUT_CHARS = os.path.join(ROOT, "assets", "bonelight", "characters")

CELL = 32
AURA_CELL = 48
TAU = math.pi * 2.0


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


def plank(c, x0, y0, x1, y1, mat, grain=True):
    """A slab of wood: lit along the top, dark at the bottom, with grain lines down it."""
    hi, lit, base, shade, deep = ramp(mat)
    c.rect(x0, y0, x1, y1, base)
    c.hline(x0, x1, y0, lit)
    c.hline(x0, x1, y1, deep)
    c.vline(x0, y0, y1, lit)
    c.vline(x1, y0, y1, shade)
    if grain:
        for x in range(int(x0) + 2, int(x1) - 1, 4):
            c.vline(x, y0 + 1, y1 - 1, shade)
            c.vline(x + 1, y0 + 2, y1 - 2, base)
    c.set(x0, y0, hi)


def bandit(c, x0, x1, y, mat):
    """An iron band across a chest."""
    hi, lit, base, shade, deep = ramp(mat)
    c.rect(x0, y, x1, y + 1, base)
    c.hline(x0, x1, y, lit)
    c.hline(x0, x1, y + 1, deep)
    c.set(x0 + 1, y, hi)


# --- the chest ----------------------------------------------------------------------------------

def chest_idle(i, n):
    """Closed, with a glint travelling across the lid. The glint is the only thing that moves.

    A chest that bobbed would read as floating; a chest that pulsed would read as about to
    explode. It is a box on the floor and the animation has to say so.
    """
    c = raster.Canvas(CELL, CELL, None)
    steel = ramp("steel")
    g = ramp("gold")

    c.hline(5, 27, 28, OCC)
    plank(c, 5, 16, 27, 27, "flesh")
    # lid: a shallow arc rather than a flat top, so the box has a front and a back
    for y in range(9, 17):
        t = (y - 9) / 8.0
        half = 11.0 * (0.62 + 0.38 * t)
        c.hline(16 - half, 16 + half, y, ramp("flesh")[2])
        c.set(16 - half, y, ramp("flesh")[1])
        c.set(16 + half, y, ramp("flesh")[3])
    c.hline(9, 23, 9, ramp("flesh")[1])
    bandit(c, 5, 27, 15, "steel")
    c.vline(9, 10, 27, steel[3])
    c.vline(23, 10, 27, steel[3])

    # the lock
    c.rect(14, 16, 18, 21, g[2])
    c.hline(14, 18, 16, g[1])
    c.disc(16, 18, 1.2, 1.2, OCC)

    # the glint: three pixels sliding along the lid, and nothing else changes
    gx = 8 + (i / float(n)) * 16
    for k in range(3):
        c.set(gx + k, 11 - abs(k - 1), steel[0] if k == 1 else steel[1])
    return c


def chest_open(i, n):
    """The lid lifting, and light coming out. Four frames of one event."""
    c = raster.Canvas(CELL, CELL, None)
    t = i / float(n - 1)
    light = bl.ELEMENTS["light"]
    steel = ramp("steel")
    g = ramp("gold")

    c.hline(5, 27, 28, OCC)
    plank(c, 5, 16, 27, 27, "flesh")
    bandit(c, 5, 27, 15, "steel")

    # the light, growing out of the open box
    glow = 2.0 + 10.0 * t
    c.disc(16, 15, glow, glow * 0.8, light[3])
    c.disc(16, 15, glow * 0.66, glow * 0.52, light[2])
    c.disc(16, 15, glow * 0.34, glow * 0.27, light[1])
    if t > 0.3:
        c.disc(16, 15, glow * 0.14, glow * 0.12, light[0])

    # the interior, dark behind the light
    c.rect(7, 13, 25, 16, OCC)

    # the lid, hinged at the back and swinging up
    lift = 8.0 * t
    for y in range(int(9 - lift), int(15 - lift)):
        half = 11.0 * (0.72 + 0.28 * (y - (9 - lift)) / 6.0)
        c.hline(16 - half, 16 + half, y, ramp("flesh")[2])
        c.set(16 - half, y, ramp("flesh")[1])
        c.set(16 + half, y, ramp("flesh")[3])
    c.hline(9, 23, int(9 - lift), ramp("flesh")[1])
    bandit(c, 7, 25, int(13 - lift), "steel")
    c.rect(14, int(12 - lift), 18, int(15 - lift), g[2])

    for k in range(4):
        a = (t + k * 0.25) * TAU
        c.set(16 + math.cos(a) * (glow + 3), 13 + math.sin(a) * (glow + 2) * 0.6, light[1])
    return c


# --- the traps ------------------------------------------------------------------------------------

def spike_trap(i, n):
    """Flush, then out. Frame 0 is a plate with slots in it and nothing else, which is the frame
    the player has to learn to read - StageHazard scrubs this strip by progress."""
    c = raster.Canvas(CELL, CELL, None)
    stone = ramp("stone")
    steel = ramp("steel")
    t = i / float(n - 1)

    c.disc(16, 22, 14.0, 7.0, stone[3])
    c.disc(16, 22, 12.0, 5.8, stone[2])
    c.disc(16, 21, 6.0, 2.6, stone[3])
    for d in range(0, 360, 60):
        a = math.radians(d)
        c.set(16 + math.cos(a) * 10, 22 + math.sin(a) * 4.6, stone[1])

    for (sx, slot) in ((10, 0), (16, 1), (22, 2)):
        c.rect(sx - 2, 20 + slot % 2, sx + 2, 22 + slot % 2, OCC)
        h = 15.0 * t
        if h < 1.0:
            continue
        top = 21 - h
        c.poly([(sx - 2.4, 22), (sx, top), (sx + 2.4, 22), (sx - 2.4, 22)], steel[2])
        c.line(sx - 2, 21, sx, top + 1, steel[1])
        c.line(sx, top, sx, 21, steel[3])
        c.set(sx, top, steel[0])
    return c


def flame_vent(i, n):
    """A vent in the floor with fire coming out of it, guttering.

    The last frame is nearly empty on purpose. StageHazard plays this one as a LOOP while it is
    active rather than scrubbing it, so the cycle has to come back down to almost nothing or the
    vent never appears to breathe.
    """
    c = raster.Canvas(CELL, CELL, None)
    stone = ramp("stone")
    f = bl.ELEMENTS["fire"]
    heights = (0.30, 0.95, 0.65, 0.12)
    h = heights[i % len(heights)]

    c.disc(16, 26, 11.0, 4.6, stone[3])
    c.disc(16, 26, 9.0, 3.6, stone[2])
    c.rect(11, 23, 21, 26, OCC)
    for x in range(12, 21, 3):
        c.vline(x, 23, 25, stone[3])

    top = 24 - 20.0 * h
    if 24 - top > 1.5:
        for y in range(int(top), 25):
            u = max(0.0, (y - top) / max(1.0, 24 - top))
            half = 1.0 + 6.0 * (u ** 0.7) + math.sin((u * 3.0 + i) * TAU) * 0.8
            c.hline(16 - half, 16 + half, y, f[3])
            c.hline(16 - half + 1.5, 16 + half - 1.5, y, f[2])
            if u > 0.45:
                c.hline(16 - half * 0.34, 16 + half * 0.34, y, f[1])
        c.set(16, int(top), f[0])
        c.set(16, int(top) + 1, f[0])
    return c


# --- the pickups ----------------------------------------------------------------------------------

def flask(fill, mat):
    """A stoppered bottle with something in it, bobbing. Both pickups share this body.

    Deliberately one shape in two colours, which is the opposite of the rule everywhere else in
    this project - but a health pickup and a buff pickup are both "a bottle on the floor", and
    making them different objects would say they were different KINDS of thing.
    """
    def draw(i, n):
        c = raster.Canvas(CELL, CELL, None)
        glass = ramp(mat)
        bob = int(round(math.sin(i / float(n) * TAU) * 1.5))

        c.hline(11, 21, 29, OCC)
        y0, y1 = 12 + bob, 27 + bob
        c.poly([(10, y1), (10, y0 + 5), (13, y0 + 1), (19, y0 + 1), (22, y0 + 5),
                (22, y1), (10, y1)], glass[2])
        # contents, filling most of the belly
        c.rect(11, y0 + 7, 21, y1 - 1, fill[3])
        c.rect(12, y0 + 8, 20, y1 - 2, fill[2])
        c.hline(12, 20, y0 + 7, fill[1])
        c.set(16, y0 + 7, fill[0])
        # glass over it
        c.vline(10, y0 + 5, y1, glass[1])
        c.vline(22, y0 + 5, y1, glass[4])
        c.hline(10, 22, y1, glass[4])
        c.rect(13, y0 - 3, 19, y0 + 1, glass[3])
        c.hline(13, 19, y0 - 3, glass[1])
        c.rect(12, y0 - 5, 20, y0 - 3, ramp("flesh")[3])
        c.set(12, y0 + 9, glass[0])
        # one rising bubble, so the liquid reads as liquid
        c.set(18, y1 - 3 - (i * 3) % 9, fill[1])
        return c
    return draw


# --- the shield aura ------------------------------------------------------------------------------

def shield_aura(i, n):
    """A ring turning around the player. NOT an object - the one thing here drawn as an effect.

    Sparse on purpose: it sits on top of the player for as long as the shield lasts, and a solid
    disc would hide the character it is protecting.
    """
    c = raster.Canvas(AURA_CELL, AURA_CELL, None)
    a = bl.ELEMENTS["arcane"]
    cx = cy = AURA_CELL / 2.0 - 0.5
    spin = i / float(n) * TAU
    r = 20.0 + math.sin(spin * 2.0) * 1.2

    # the ring itself, thin and broken into arcs so it reads as turning
    for d in range(0, 360, 2):
        ang = math.radians(d)
        phase = (math.sin(ang * 3.0 + spin) + 1.0) * 0.5
        if phase < 0.35:
            continue
        tone = a[1] if phase > 0.86 else (a[2] if phase > 0.6 else a[3])
        c.set(cx + math.cos(ang) * r, cy + math.sin(ang) * r * 0.9, tone)
        if phase > 0.7:
            c.set(cx + math.cos(ang) * (r - 1), cy + math.sin(ang) * (r - 1) * 0.9, a[3])

    # six motes going round it, the fastest-moving thing so the eye reads direction
    for k in range(6):
        ang = spin * 1.6 + k * TAU / 6.0
        mx = cx + math.cos(ang) * r
        my = cy + math.sin(ang) * r * 0.9
        c.disc(mx, my, 1.8, 1.8, a[2])
        c.set(mx, my, a[0])
    return c


# --- one UI texture that is neither a spell nor a relic -------------------------------------------

PORTRAIT_CELL = 48


def unknown_wizard(i, n):
    """The portrait a character card falls back to when the character has no art of its own.

    It replaced a bought dungeon-pack priest, which was the only character sheet left in the game
    that we could not publish - and which was also a PRIEST standing in for a wizard.

    It is deliberately a hooded figure with no face: the cast is going to grow by eight rescued
    wizards long before all eight are drawn, and the honest picture for "we have not drawn this
    one yet" is somebody you cannot see. CharacterSelection already tints this to near-black on a
    locked card, so it has to read as a silhouette first and a figure second.
    """
    c = raster.Canvas(PORTRAIT_CELL, PORTRAIT_CELL, None)
    wool = ramp("wool")
    a = bl.ELEMENTS["arcane"]
    cx = 23.5

    c.hline(12, 35, 45, OCC)
    # robe: narrow at the shoulders, flaring to the hem, with fold columns down it
    for y in range(14, 45):
        t = (y - 14) / 31.0
        half = 6.0 + 11.0 * (t ** 1.4)
        c.hline(cx - half, cx + half, y, wool[2])
        c.set(cx - half, y, wool[1])
        c.set(cx + half, y, wool[4])
    for off in (-7.0, -2.0, 3.0, 8.0):
        for y in range(18, 45):
            t = (y - 18) / 27.0
            c.set(cx + off * (0.45 + 0.55 * t), y, wool[3] if y % 3 else wool[1])

    # hood: a cowl over a hole. The hole is the whole idea, so it is real occlusion.
    for y in range(6, 20):
        t = (y - 6) / 14.0
        half = 3.0 + 8.0 * (t ** 0.6)
        c.hline(cx - half, cx + half, y, wool[2])
        c.set(cx - half, y, wool[1])
        c.set(cx + half, y, wool[4])
    for y in range(11, 21):
        t = (y - 11) / 10.0
        half = 5.5 - 2.0 * t
        c.hline(cx - half, cx + half, y, OCC)
    c.hline(int(cx - 6), int(cx + 6), 10, wool[1])

    c.set(cx - 2, 15, a[1])
    c.set(cx + 2, 15, a[2])
    return c


FX = [
    ("unknown-portrait", unknown_wizard, 1, PORTRAIT_CELL),
    ("chest", chest_idle, 4, CELL),
    ("chest-open", chest_open, 4, CELL),
    ("spike-trap", spike_trap, 4, CELL),
    ("flame-vent", flame_vent, 4, CELL),
    ("health-flask", flask(bl.ELEMENTS["fire"], "steel"), 4, CELL),
    ("buff-flask", flask(bl.ELEMENTS["arcane"], "steel"), 4, CELL),
    ("shield-aura", shield_aura, 8, AURA_CELL),
]


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    from PIL import Image
    ok = True
    total = 0
    for name, fn, frames, cell in FX:
        strip = Image.new("RGBA", (cell * 2 * frames, cell * 2), (0, 0, 0, 0))
        for i in range(frames):
            c = fn(i, frames)
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s frame %d:" % (name, i))
                for h, cnt in bad[:5]:
                    print("   %s  x%d" % (h, cnt))
                break
            strip.alpha_composite(c.scaled(2), (i * cell * 2, 0))
        # The portrait is character art, not a world prop, so it lands with the other characters.
        where = OUT_CHARS if name == "unknown-portrait" else OUT_DIR
        if not os.path.isdir(where):
            os.makedirs(where)
        strip.save(os.path.join(where, "%s.png" % name))
        total += frames
        print("  %-16s %d frames at %dpx" % (name, frames, cell * 2))

    if not ok:
        return 1
    print("")
    print("%d interactables, %d frames, palette clean" % (len(FX), total))
    return 0


if __name__ == "__main__":
    sys.exit(main())
