# -*- coding: utf-8 -*-
"""Gravewell's pit, and the earth that closes over what it catches. Run: python tools/art/gravewell.py

The playtest said: "does the grave ability do anything? i couldn't tell what it was supposed to do".
It was a faint dark disc with a faint ring round it, on a dark floor, under a crowd. This is the
object the spell actually is - an open grave in the ground - drawn so it reads at a glance on a
phone, without a ring that says where it reaches.

Two strips, written to assets/bonelight/effects/:

  gravewell-pit.png    48x48 cell, 13 frames: open 4, armed 4 (looping), collapse 5, in that order
  gravewell-grasp.png  32x16 cell, 6 frames: rise 2, hold 2 (looping), crumble 2

The frame counts are an API - scripts/GravewellTrap.cs reads them as OpenFrames, ArmedFrames,
CollapseFrames and GraspFrames - so change both.

AUTHORED 1:1 AND SHOWN AT x2 (.ai/art-direction.md section 1). The pit is centred on the trap's
point, and its lip sits at LIP_RADIUS art pixels - 20, which is 40 px in the world against the 42 px
trigger, so an enemy treading on the turned earth is an enemy that springs it. That is the one
number the art and the code share; GravewellTrap.TriggerRadius says so beside it.

WHAT IS AND IS NOT A SPRITE. Only things of a fixed size are drawn here: the pit, which is the
trigger, and the heap of earth that holds one enemy. The collapse's reach is the blast radius,
which Area grows continuously, so no sheet at an integer scale could match it: the cracks and the
heave that run out to it are drawn by scenes/shaders/gravewell_collapse.gdshader out of the radius
the code tests.

Lit from the upper left like everything else. The turned earth is the Earth emissive ramp with the
gold row's shade and deep under it - the spell is a made thing, and on the near-black stone floors
a bank drawn in the stone row vanished (spell_icons.gravewell learned the same on its card). The
mouth is Darkness: an edge-tone wall falling to occlusion, with motes of the Darkness ramp rising
out of it while it waits, so an armed pit is never just a black hole. A weathered headstone in the
bone row leans at the back, which is what says "grave" rather than "hole".
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

PIT_CELL = 48
CX, CY = 24.0, 26.0
LIP_RADIUS = 20.0     # the trigger: 40 px in the world at x2 (GravewellTrap.TriggerRadius is 42)
MOUTH_RADIUS = 13.0
PHASES = [("open", 4), ("armed", 4), ("collapse", 5)]

# THE MASS GRAVE (2026-10-29, the pixel-art pass). Mass Grave's bank is twice a grave's
# (GravewellTrap.MassBankScale), and the code used to draw it by showing this same 48 px pit at x4:
# art pixels four world px across among a game drawn at x2 (art-direction section 1 - "a boss is a
# bigger CELL, never a bigger scale"). So the pit is drawn twice: once at S = 1, the grave, and once at
# S = 2 into a 96 px cell, the great grave - every radius, reach and placement doubled, every pebble,
# clod, crack pixel and headstone the size it always was, so the bigger grave has more in it rather than
# bigger pixels, and three stones lean at its back instead of one. Its bank is LIP_RADIUS x 2 = 40 art
# px, 80 in the world: the same trigger the x4 drawing showed. set_size() rebinds the geometry below.
S = 1
HEADSTONES = [(31.0, 1, 0)]
MASS_HEADSTONES = [(34.0, -1, 9), (49.0, 1, 5), (64.0, 2, 10)]


def set_size(s):
    """The geometry of a grave drawn at s times the size (1, the grave; 2, the Mass Grave)."""
    global S, PIT_CELL, CX, CY, LIP_RADIUS, MOUTH_RADIUS, HEADSTONES
    S = s
    PIT_CELL = 48 * s
    CX, CY = 24.0 * s, 26.0 * s
    LIP_RADIUS = 20.0 * s
    MOUTH_RADIUS = 13.0 * s
    HEADSTONES = [(31.0, 1, 0)] if s == 1 else MASS_HEADSTONES

GRASP_W, GRASP_H = 32, 16
GRASP_FRAMES = 6

EARTH = bl.ELEMENTS["earth"]                    # core, hot, mid, edge
DARK = bl.ELEMENTS["darkness"]                  # core, hot, mid, edge
# Soil, light to dark: the Earth ramp's hot, mid and edge, then the gold row's shade and deep.
SOIL = [EARTH[1], EARTH[2], EARTH[3], bl.tone("gold", "shade"), bl.tone("gold", "deep")]
BONE = [bl.tone("skin", r) for r in bl.ROLES]   # hi, lit, base, shade, deep
GLOOM = [DARK[3], bl.tone("violet", "deep"), OCC]

KEY = (-0.70710678, -0.70710678)


def dither(x, y):
    return raster.DITHER[y % 8][x % 8] / 64.0


def h2(x, y, s=0.0):
    """A stable hash in 0..1 for one pixel, so every frame scatters its pebbles the same way."""
    v = math.sin(x * 12.9898 + y * 78.233 + s * 37.719) * 43758.5453
    return v - math.floor(v)


def wob(a, s, amount):
    """The lip is dug, not drawn with a compass: three harmonics so it reads as a shape."""
    return 1.0 + amount * (0.55 * math.sin(3.0 * a + 1.3 + s) + 0.3 * math.sin(5.0 * a - 0.7 + s * 1.7)
                           + 0.15 * math.sin(8.0 * a + 2.1))


def ramp_pick(ramp, t, x, y):
    """t of the way along a ramp, as an ordered dither between neighbouring tones."""
    t = min(0.999, max(0.0, t)) * (len(ramp) - 1)
    i = int(t)
    return ramp[i + 1] if (t - i) > dither(x, y) else ramp[i]


def ramp_band(ramp, t, x, y, soft=0.3):
    """The same walk, but flat inside each tone and dithered only across the last `soft` of it.
    A bank only a few pixels wide dithered across its whole width was all grain and no form."""
    t = min(0.999, max(0.0, t)) * (len(ramp) - 1)
    i = int(t)
    f = t - i
    if f < 1.0 - soft:
        return ramp[i]
    return ramp[i + 1] if (f - (1.0 - soft)) / soft > dither(x, y) else ramp[i]


# --- the parts --------------------------------------------------------------------------------

def headstone(c, x0, y0, lean, sink=0, crack=True):
    """A small weathered stone, round-topped, leaning `lean` px per 10 rows, sunk `sink` rows."""
    w, h = 8, 13
    for row in range(h - sink):
        y = y0 + row + sink
        shift = int(round((h - row) * lean / 10.0))
        half = w / 2.0 if row > 1 else (w / 2.0 - 1.0 if row == 1 else w / 2.0 - 2.0)
        x_start = int(round(x0 + shift - half))
        x_end = int(round(x0 + shift + half)) - 1
        for x in range(x_start, x_end + 1):
            u = (x - x_start) / float(max(1, x_end - x_start))
            tone = BONE[1] if u < 0.3 else BONE[2] if u < 0.7 else BONE[3]
            if row < 2 and u < 0.5:
                tone = BONE[0]
            c.set(x, y, tone)
        c.set(x_start - 1, y, OCC)
        c.set(x_end + 1, y, OCC)
        if row == 0:
            c.hline(x_start, x_end, y - 1, OCC)
    if crack and sink == 0:
        c.set(int(x0 + lean * 0.6), y0 + 4, BONE[4])
        c.set(int(x0 + lean * 0.6) + 1, y0 + 5, BONE[4])
        c.set(int(x0 + lean * 0.5), y0 + 6, BONE[4])
    # A worn inscription line, one pixel of shade.
    if sink < 4:
        c.hline(int(x0 + lean * 0.7) - 1, int(x0 + lean * 0.7) + 1, y0 + 3 + sink, BONE[3])


def bone(c, x, y, horizontal=True):
    """A thrown-up bone, four pixels and two knuckles."""
    if horizontal:
        c.hline(x, x + 3, y, BONE[1])
        c.set(x - 1, y - 1, BONE[0])
        c.set(x - 1, y + 1, BONE[2])
        c.set(x + 4, y - 1, BONE[1])
        c.set(x + 4, y + 1, BONE[3])
    else:
        c.vline(x, y, y + 3, BONE[1])
        c.set(x - 1, y - 1, BONE[0])
        c.set(x + 1, y - 1, BONE[2])


def pit(c, mouth, lip, seed=0.0, broken=0.0, rubble=0.0, glow=0.0, motes=(), scar=False):
    """The grave. `mouth` and `lip` are radii in art px; `broken` 0..1 eats gaps in the lip and
    `rubble` 0..1 fills the mouth with fallen earth. `motes` are (dx, dy, tone) inside the mouth."""
    for y in range(int(CY - lip) - 3, int(CY + lip) + 4):
        for x in range(int(CX - lip) - 3, int(CX + lip) + 4):
            dx, dy = x + 0.5 - CX, y + 0.5 - CY
            d = math.hypot(dx, dy)
            if d < 0.01:
                a = 0.0
                ux, uy = 0.0, 1.0
            else:
                a = math.atan2(dy, dx)
                ux, uy = dx / d, dy / d
            facing = ux * KEY[0] + uy * KEY[1]
            rm = mouth * wob(a, seed, 0.10)
            rl = lip * wob(a, seed + 2.0, 0.07)
            if d <= rm and mouth > 0.5:
                depth = 1.0 - d / max(0.5, rm)
                if rubble > 0.0 and h2(x, y, seed) < rubble * (1.0 - 0.6 * depth):
                    # Earth fallen in: chunks of soil with occlusion between them.
                    k = h2(x + 7, y + 3, seed)
                    c.set(x, y, SOIL[2] if k > 0.66 else SOIL[3] if k > 0.33 else SOIL[4])
                    continue
                if rm - d < 1.2:
                    # The wall where the bank turns under: the far side (lower right) catches the
                    # light, the near side is already in the dark.
                    c.set(x, y, SOIL[2] if facing < -0.35 else GLOOM[0])
                    continue
                if rm - d < 2.4 and facing < -0.2:
                    c.set(x, y, SOIL[3])
                    continue
                if scar:
                    c.set(x, y, OCC)
                    continue
                c.set(x, y, ramp_pick(GLOOM, depth * 2.2 + 0.15, x, y))
                continue
            if d <= rl:
                band = (d - rm) / max(0.5, rl - rm)     # 0 at the crest, 1 at the outer edge
                if broken > 0.0:
                    sector = int((a / TAU + 0.5) * 11.0 + seed * 3.0)
                    if h2(sector, 3, seed) < broken * 0.55 and band > 0.25:
                        continue
                    if h2(x, y, seed + 9.0) < broken * 0.35:
                        continue
                # Lit on the upper left, shaded on the lower right; lighter at the crest.
                t = 0.46 - 0.36 * facing + 0.32 * band
                if scar:
                    t += 0.35
                tone = ramp_band(SOIL, t, x, y)
                # Pebbles and clods: the internal edges that stop a bank reading as a flat ring.
                k = h2(x, y, seed + 4.0)
                if k > 0.94:
                    tone = SOIL[0] if facing > 0.0 else SOIL[1]
                elif k < 0.05:
                    tone = SOIL[4]
                c.set(x, y, tone)
                continue
            # Outside the lip: the contact line under the lower-right bank.
            if d <= rl + 1.2 and facing < -0.25 and not scar:
                c.set(x, y, OCC)
    for (mx, my, tone) in motes:
        c.set(int(CX + mx), int(CY + my), tone)
    if glow > 0.0:
        # A breath of Darkness on the inner wall, travelling round it while the pit waits.
        for k in range(6):
            a = glow * TAU + k * 0.18
            r = mouth * wob(a, seed, 0.10) - 1.6
            c.set(int(CX + math.cos(a) * r), int(CY + math.sin(a) * r), DARK[2] if k % 3 else DARK[1])


def clods(c, n, reach, seed, inner=None):
    """Earth thrown clear of the lip, inside the cell."""
    if inner is None:
        inner = LIP_RADIUS + 0.5
    for k in range(n):
        a = TAU * ((k * 0.618 + seed * 0.13) % 1.0)
        r = inner + (reach - inner) * h2(k, 1, seed)
        x, y = CX + math.cos(a) * r, CY + math.sin(a) * r
        if 1 <= x < PIT_CELL - 1 and 1 <= y < PIT_CELL - 1:
            c.set(x, y, SOIL[1] if math.cos(a) * KEY[0] + math.sin(a) * KEY[1] > 0 else SOIL[3])


def cracks(c, n, length, seed):
    """The ground splitting where the pit is about to open."""
    for k in range(n):
        a = TAU * k / n + seed + 0.4 * math.sin(k * 2.3)
        x, y = CX, CY
        for s in range(int(length)):
            a += 0.35 * math.sin(s * 1.7 + k)
            x += math.cos(a)
            y += math.sin(a)
            c.set(x, y, OCC)
            if s % 3 == 1:
                c.set(x - 1, y - 1, SOIL[2])


# --- the frames -------------------------------------------------------------------------------

# The stones stand on the ground behind the grave (HEADSTONES, set by set_size): x of each one's
# middle, its lean, and the row its top is on. Drawn AFTER the bank, because they stand up in front of
# the ground behind them. Every other placement below is a grave's, times S.


def stones(c, dx=0, dy=0, lean=None, sink=0, crack=True):
    """Every headstone of the grave, moved (dx, dy), leant to `lean` (None: its own) and sunk."""
    for (x, own_lean, row) in HEADSTONES:
        headstone(c, x + dx, row + dy, own_lean if lean is None else lean, sink=sink, crack=crack)


def open_frame(i):
    c = raster.Canvas(PIT_CELL, PIT_CELL)
    if i == 0:
        cracks(c, 5 * S, 7 * S, 0.3)
        pit(c, 2.0 * S, 4.5 * S, broken=0.4)
        clods(c, 4 * S, 9.0 * S, 0.5, inner=5.0 * S)
        return c
    s = [0.0, 0.45, 0.75, 0.93][i]
    cracks(c, 6 * S, (4 + 6 * s) * S, 0.3)
    pit(c, MOUTH_RADIUS * s, LIP_RADIUS * min(1.0, s * 1.1), broken=0.3 * (1.0 - s))
    clods(c, (6 + 3 * i) * S, LIP_RADIUS * s + 4.0 * S, 0.9 + i, inner=LIP_RADIUS * s)
    if i >= 2:
        # The stones rise out of the ground as the grave opens.
        stones(c, sink=8 if i == 2 else 4, crack=False)
    return c


def armed_frame(i, n=4):
    """The waiting pit. Motes rise out of the dark on their own clocks and a breath of Darkness
    travels round the inner wall, so even a pit in the corner of the screen is visibly alive."""
    c = raster.Canvas(PIT_CELL, PIT_CELL)
    motes = []
    rising = ((-4, 1.0, 0.0), (3, 1.0, 0.45), (0, 1.0, 0.7), (-1, 1.0, 0.2))
    if S > 1:
        rising += ((-12, 1.0, 0.85), (9, 1.0, 0.3), (14, 1.0, 0.6), (-9, 1.0, 0.1))
    for k, (mx, speed, start) in enumerate(rising):
        u = (i / float(n) * speed + start) % 1.0
        my = (6.0 - u * 11.0) * S      # from the bottom of the mouth up past the rim
        tone = DARK[0] if u > 0.7 else DARK[1] if u > 0.35 else DARK[2]
        # The first four are a grave's, placed in its units; the Mass Grave's four more are its own.
        motes.append((mx * (S if k < 4 else 1) + (1 if (k + i) % 2 else 0), my, tone))
    pit(c, MOUTH_RADIUS, LIP_RADIUS, seed=0.0, glow=i / float(n), motes=motes)
    bone(c, 8 * S, 34 * S)
    c.set(36 * S, 37 * S, BONE[1])
    c.set(36 * S + 1, 37 * S, BONE[2])
    if S > 1:
        bone(c, 70, 76, horizontal=False)
        bone(c, 20, 82)
    # A trickle of soil running in over the far lip, one grain lower each frame.
    for k in range(2):
        c.set(30 * S - k, 33 * S + ((i + k * 2) % 4), SOIL[1])
    clods(c, 8 * S, 23.0 * S, 0.0)
    stones(c)
    return c


def collapse_frame(i):
    """The pit takes what tripped it: the mouth gapes, the bank slides in, the stone goes over,
    and what is left is a scar of turned earth that the code fades away once the hold is over."""
    c = raster.Canvas(PIT_CELL, PIT_CELL)
    if i == 0:
        pit(c, 16.0 * S, 21.0 * S, seed=0.0, broken=0.25, glow=0.6)
        clods(c, 14 * S, 23.5 * S, 3.0)
        stones(c, -1, 2, lean=4, sink=2, crack=True)
    elif i == 1:
        pit(c, 18.0 * S, 21.5 * S, seed=0.0, broken=0.6, rubble=0.15)
        clods(c, 16 * S, 23.5 * S, 5.0)
        stones(c, -3, 5, lean=7, sink=7, crack=False)
        # Dust thrown up: bone-grey puffs at the broken bank.
        for k in range(10 * S):
            a = TAU * k / (10.0 * S) + 0.3
            r = (19.0 + 2.5 * h2(k, 5)) * S
            c.set(CX + math.cos(a) * r, CY + math.sin(a) * r - 1, BONE[3] if k % 2 else BONE[2])
    elif i == 2:
        pit(c, 15.0 * S, 20.0 * S, seed=0.0, broken=0.3, rubble=0.45)
        c.set(25 * S, 23 * S, BONE[2])
        c.set(25 * S + 1, 23 * S, BONE[3])
        clods(c, 10 * S, 23.0 * S, 6.0)
    elif i == 3:
        pit(c, 10.0 * S, 18.0 * S, seed=0.0, broken=0.15, rubble=0.6)
        clods(c, 6 * S, 21.0 * S, 7.0)
    else:
        pit(c, 3.5 * S, 15.0 * S, seed=0.0, rubble=0.0, scar=True)
        clods(c, 4 * S, 18.0 * S, 8.0, inner=15.5 * S)
    return c


def grasp_frame(i):
    """Earth heaped over one enemy's feet, with fingers of Darkness closing round them: what makes
    a rooted skeleton read as held by the pit rather than as merely standing still. Feet on row 14
    (cell-2), the contact line on row 15."""
    c = raster.Canvas(GRASP_W, GRASP_H)
    cx, base = 16.0, 14
    rx = [6.0, 9.5, 11.0, 11.0, 10.0, 7.0][i]
    ry = [1.6, 3.0, 4.0, 4.2, 3.0, 1.6][i]
    broken = [0.0, 0.0, 0.0, 0.0, 0.35, 0.6][i]
    for y in range(int(base - ry) - 1, base + 1):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            nx = (x + 0.5 - cx) / rx
            ny = (y + 0.5 - base) / max(0.6, ry)
            if nx * nx + ny * ny > 1.0 or y > base:
                continue
            if broken and h2(x, y, 2.0) < broken:
                continue
            t = 0.25 + 0.5 * (nx + 1.0) * 0.5 + 0.35 * (ny + 1.0) * 0.5
            tone = ramp_pick(SOIL, t, x, y)
            if h2(x, y, 6.0) > 0.92:
                tone = SOIL[0]
            c.set(x, y, tone)
    c.hline(int(cx - rx) + 1, int(cx + rx) - 1, base + 1, OCC)
    # Fingers of Darkness out of the heap, thick at the knuckle, thin at the tip, and hooked in over
    # the feet - a bar of violet reads as a fence post; a crook reads as a hand.
    reach = [2, 4, 7, 7, 4, 0][i]
    flex = [0.0, 0.3, 0.8, 1.0, 0.5, 0.0][i]
    for (fx, curl) in ((cx - 7, 1), (cx + 7, -1), (cx - 2, 1), (cx + 3, -1)):
        if reach <= 0:
            continue
        root = int(base - ry) + 1
        tip_x = tip_y = None
        for k in range(reach + 1):
            u = k / float(reach)
            y = root - k
            x = int(round(fx + curl * (u ** 2) * (2.0 + 2.0 * flex)))
            if u < 0.55:
                c.set(x - 1, y, DARK[2])     # the knuckle's lit side, toward the key
                c.set(x, y, DARK[3])
                c.set(x + 1, y, OCC)
            else:
                c.set(x, y, DARK[2] if curl > 0 else DARK[3])
            tip_x, tip_y = x, y
        # The hooked tip: one pixel in over the feet, white-hot at the very end.
        c.set(tip_x + curl, tip_y, DARK[1])
        if i in (2, 3):
            c.set(tip_x + curl, tip_y + 1, DARK[0])
    return c


# --- writing ----------------------------------------------------------------------------------

def write_strip(frames, w, h, name):
    from PIL import Image
    strip = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for i, c in enumerate(frames):
        bad = c.audit()
        if bad:
            print("OFF-CONTRACT COLOURS in %s frame %d: %s" % (name, i, bad[:4]))
            return False
        strip.alpha_composite(c.img, (i * w, 0))
    strip.save(os.path.join(OUT_DIR, name))
    print("  %-28s %2d frames of %dx%d" % (name, len(frames), w, h))
    return True


def pit_strip():
    frames = [open_frame(i) for i in range(4)]
    frames += [armed_frame(i) for i in range(4)]
    frames += [collapse_frame(i) for i in range(5)]
    return frames


def main(argv=None):
    global OUT_DIR
    if argv is not None and len(argv) > 1:
        OUT_DIR = argv[1]
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    set_size(1)
    ok = write_strip(pit_strip(), PIT_CELL, PIT_CELL, "gravewell-pit.png")
    # The Mass Grave: the same grave at twice the size in a 96 px cell, shown at x2 like everything.
    set_size(2)
    ok &= write_strip(pit_strip(), PIT_CELL, PIT_CELL, "gravewell-pit-mass.png")
    set_size(1)
    ok &= write_strip([grasp_frame(i) for i in range(GRASP_FRAMES)], GRASP_W, GRASP_H, "gravewell-grasp.png")
    if not ok:
        return 1
    print("gravewell art: palette clean")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
