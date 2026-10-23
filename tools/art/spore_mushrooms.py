# -*- coding: utf-8 -*-
"""Toxic Spore Burst's mushrooms, and what they burst into. Run: python tools/art/spore_mushrooms.py

The spell sows mushrooms among the enemies; each one sprouts, swells, and bursts. Four kinds of
art, written to assets/bonelight/effects/:

  spore-mushroom-<v>.png   32x32 cell, 14 frames: sprout 5, swell 4, burst 5, in that order
  spore-giantcap-<v>.png   48x48 cell (the large-actor cell), the same 14 frames - Giant Caps
  spore-doomcap-<v>.png    64x64 cell, the same 14 frames - Doomcap, the titanic Ascension
  spore-puff-<v>.png       16x16 cell, 6 frames: one puff opening out and thinning
  spore-ember.png          16x16 cell, 4 frames, looping: a lick of flame (Ember Caps' patches)

<v> is the element the spores carry: `poison`, and `fire` for the Ember Caps mutation, which
trades the spell's Poison tag for Fire (scripts/SpellEvolutionCatalog.cs). The frame counts are an
API - scripts/SporeMushroom.cs reads them as SproutFrames/SwellFrames/BurstFrames - so change both.

AUTHORED 1:1 AND SHOWN AT x2, the actor convention of .ai/art-direction.md section 1, not the
pre-scaled strips spell_fx.py writes: the mushroom is an object standing on the ground among the
skeletons, so it is drawn like one, feet on row cell-2 and the occlusion line on row cell-1.

WHAT IS AND IS NOT A SPRITE. Only things of a fixed size are drawn here: the mushroom, a puff, a
flame. Everything whose size is a hit radius - the swell's warning ring, the burst, Lingering
Rot's cloud, an ember patch - is drawn by SporeMushroom out of the radius it tests, with these
puffs and flames placed INSIDE it. A burst sprite scaled to the radius was the old spell's bug:
drawn at three quarters of the ground it hurt, and never growing when the Ascension said it did.

The cap is emissive (it is a made light - .ai/world-and-tone.md: everything that glows is
something the player made), so it wears its element ramp: white-hot spots and gills, identity in
mid and edge. The stem is bone. Everything has the occlusion outline so it reads over the swarm.
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
PHASES = [("sprout", 5), ("swell", 4), ("burst", 5)]
FRAMES = sum(n for _p, n in PHASES)

STEM = [bl.tone("skin", r) for r in bl.ROLES]          # hi, lit, base, shade, deep
DIRT = [bl.tone("stone", r) for r in bl.ROLES]
# Smoke, for the fire variant once its flash has burned out: bone greys, pale enough to read on the floor.
ASH = [bl.tone("skin", "lit"), bl.tone("skin", "base"), bl.tone("skin", "shade"), bl.tone("skin", "deep")]

VARIANTS = {
    # cap: the element ramp, core / hot / mid / edge. under: the gill shadow, light then dark.
    "poison": {
        "cap": bl.ELEMENTS["poison"],
        "under": [bl.tone("linen", "shade"), bl.tone("linen", "deep")],
        "mote": bl.ELEMENTS["poison"],
    },
    "fire": {
        "cap": bl.ELEMENTS["fire"],
        "under": [bl.tone("red", "lit"), bl.tone("red", "base")],
        "mote": bl.ELEMENTS["fire"],
    },
}


def dither(x, y):
    return raster.DITHER[y % 8][x % 8] / 64.0


def outline(c):
    """Occlusion line round every opaque pixel. Read from a snapshot so the line never grows."""
    src = c.img.copy().load()
    for y in range(c.h):
        for x in range(c.w):
            if src[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < c.w and 0 <= ny < c.h and src[nx, ny][3]:
                    c.set(x, y, OCC)
                    break


# --- the parts --------------------------------------------------------------------------------

def stem(c, cx, base, top, hw, ring=None, torn=False, under=None):
    """A bone stem from `top` down to `base`, lit from the left, flaring at the foot."""
    top = int(round(top))
    for y in range(top, base + 1):
        flare = 1 if y >= base - 1 else 0
        w = int(round(hw)) + flare
        x0, x1 = cx - w, cx + w - 1
        for x in range(x0, x1 + 1):
            u = (x - x0) / float(max(1, x1 - x0))
            tone = STEM[0] if u < 0.18 else STEM[1] if u < 0.4 else STEM[2] if u < 0.72 else STEM[3]
            if x == x1:
                tone = STEM[4]
            c.set(x, y, tone)
        # Fibres down the stem: the internal edges that keep a pale column from reading as a bar.
        if (y - top) % 3 == 1 and x1 - x0 >= 3:
            c.set(cx - 1, y, STEM[3])
            c.set(cx + 1, y, STEM[1])
    if ring is not None and top < ring < base - 2:
        w = int(round(hw)) + 1
        c.hline(cx - w, cx + w - 1, ring, STEM[1])
        c.hline(cx - w + 1, cx + w - 2, ring + 1, STEM[3])
    if torn and under:
        # A ragged top where the cap tore away.
        w = int(round(hw))
        for x in range(cx - w, cx + w):
            if (x * 7 + top) % 3 == 0:
                c.set(x, top - 1, under[0])
            c.set(x, top, under[1] if (x + top) % 2 else under[0])


def cap(c, cx, cy, rx, ry, v, glow=0.0, spots=(), cracks=0):
    """The dome. `glow` 0..1 brightens the gills and spots; `cracks` > 0 splits it with light."""
    ramp = v["cap"]
    under = v["under"]
    hx, hy = cx - rx * 0.35, cy - ry * 0.55
    lip = cy + ry * 0.30
    body = [ramp[1], ramp[2], ramp[2], ramp[3]]
    for y in range(int(cy - ry) - 1, int(lip) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            nx = (x + 0.5 - cx) / rx
            ny = (y + 0.5 - cy) / ry
            if nx * nx + ny * ny > 1.0 or y > lip:
                continue
            d = math.sqrt(((x - hx) / (rx * 1.3)) ** 2 + ((y - hy) / (ry * 1.3)) ** 2)
            t = min(0.999, d) * (len(body) - 1)
            i = int(t)
            tone = body[i + 1] if (t - i) > dither(x, y) else body[i]
            if nx * nx + ny * ny > 0.80:
                tone = ramp[3]
            c.set(x, y, tone)
    # The underside: a shadow band with the gills glowing in it.
    gy = int(lip) + 1
    half = int(rx * 0.86)
    for x in range(int(cx - half), int(cx + half) + 1):
        c.set(x, gy, under[0])
        c.set(x, gy + 1, under[1] if abs(x - cx) > half * 0.55 else under[0])
        if (x - int(cx)) % 2 == 0 and abs(x - cx) < half - 1:
            c.set(x, gy, ramp[1] if glow < 0.5 else ramp[0])
            if glow > 0.25 and abs(x - cx) < half * 0.6:
                c.set(x, gy + 1, ramp[2] if glow < 0.75 else ramp[1])
    for (sx, sy, sr) in spots:
        px, py = cx + sx * rx, cy + sy * ry
        r = sr * (1.0 + glow * 0.5)
        c.disc(px, py, r + 0.6, r + 0.4, ramp[1])
        c.disc(px, py, max(0.6, r * 0.6), max(0.6, r * 0.5), ramp[0])
    for k in range(cracks):
        a = -math.pi * (0.15 + 0.7 * k / float(max(1, cracks - 1)))
        for s in range(int(rx * 0.95)):
            f = s / float(rx)
            c.set(cx + math.cos(a) * s, cy + math.sin(a) * s * (ry / float(rx)) + f * 1.5, ramp[0])


def mound(c, cx, base, rx, broken):
    """Earth heaved up at the foot: the sprout's first frame, and what is left after the burst."""
    c.disc(cx, base, rx, 2.2, DIRT[2])
    c.disc(cx - rx * 0.3, base - 0.6, rx * 0.5, 1.2, DIRT[1])
    if broken:
        c.line(cx - 2, base - 1, cx + 1, base, OCC)
        c.line(cx + 1, base - 1, cx + 3, base, OCC)


def specks(c, cx, base, spread, n, seed, up):
    for k in range(n):
        a = math.pi * (1.1 + 0.8 * ((k * 0.618 + seed) % 1.0))
        r = spread * (0.5 + 0.5 * ((k * 0.37 + seed) % 1.0))
        c.set(cx + math.cos(a) * r, base + math.sin(a) * r * up, DIRT[k % 2 + 1])


def puff_lobes(c, cx, cy, radius, lobe, ramp, thin, seed):
    """A ring of spore lobes; `thin` 0..1 punches dithered holes so the cloud visibly disperses."""
    n = 7
    for k in range(n):
        a = TAU * k / n + seed
        px, py = cx + math.cos(a) * radius, cy + math.sin(a) * radius * 0.8
        for y in range(int(py - lobe) - 1, int(py + lobe) + 2):
            for x in range(int(px - lobe) - 1, int(px + lobe) + 2):
                d = math.sqrt((x - px) ** 2 + (y - py) ** 2) / max(0.6, lobe)
                if d > 1.0 or dither(x, y) < thin:
                    continue
                c.set(x, y, ramp[3] if d > 0.6 else ramp[2] if d > 0.3 or thin > 0.3 else ramp[1])


# --- one mushroom, any size ------------------------------------------------------------------

class Shape(object):
    """The full-grown proportions of one mushroom on its cell."""

    def __init__(self, cell, cx, base, stem_h, hw, cap_rx, cap_ry, ring, spots, titan=False):
        self.cell = cell
        self.cx = cx
        self.base = base
        self.stem_h = stem_h
        self.hw = hw
        self.rx = cap_rx
        self.ry = cap_ry
        self.ring = ring
        self.spots = spots
        self.titan = titan
        self.k = cell / 32.0


SMALL = Shape(32, 16, 30, 11, 3, 10.0, 7.0, 21,
              [(-0.45, -0.35, 1.2), (0.15, -0.6, 1.0), (0.5, -0.2, 0.9), (-0.1, -0.05, 0.8)])
GIANT = Shape(48, 24, 46, 17, 5, 15.0, 10.5, 32,
              [(-0.48, -0.4, 1.7), (0.1, -0.68, 1.4), (0.5, -0.3, 1.5), (-0.12, -0.12, 1.1),
               (0.32, 0.0, 0.9), (-0.6, 0.02, 0.9)])
TITAN = Shape(64, 32, 62, 25, 7, 22.0, 15.0, 42,
              [(-0.5, -0.45, 2.4), (0.05, -0.7, 2.0), (0.48, -0.42, 2.2), (-0.15, -0.2, 1.6),
               (0.3, -0.05, 1.4), (-0.62, -0.02, 1.2), (0.7, 0.05, 1.1), (-0.3, -0.72, 1.3),
               (0.25, -0.35, 1.0)], titan=True)


def roots(c, s, reach):
    """Titan only: roots clawing out from the foot, and two small caps beside it."""
    for k, (dx, dy) in enumerate(((-1.0, 0.25), (-0.6, 0.45), (0.6, 0.45), (1.0, 0.25))):
        x0 = s.cx + (s.hw - 1) * (1 if dx > 0 else -1)
        c.line(x0, s.base - 1, x0 + dx * reach, s.base - 1 + dy * 3, STEM[3])
        c.line(x0, s.base, x0 + dx * reach * 0.8, s.base + dy * 2, STEM[4])


def side_caps(c, s, v, grow):
    if grow <= 0.2:
        return
    for side in (-1, 1):
        bx = s.cx + side * (s.hw + 9)
        h = int(4 + 4 * grow)
        stem(c, int(bx), s.base, s.base - h, 1.5)
        cap(c, bx, s.base - h - 1, 4.5 * grow + 1, 3.2 * grow + 1, v, spots=[(-0.2, -0.3, 0.7)])


def grown(c, s, v, scale, glow=0.0, spots=True, cracks=0, dx=0, swell=1.0):
    """The mushroom at `scale` of full size, standing on the cell's floor; `swell` inflates the cap."""
    h = max(2, s.stem_h * scale)
    hw = max(1.0, s.hw * (0.45 + 0.55 * scale))
    top = s.base - h
    rx = max(2.5, s.rx * scale * swell)
    ry = max(2.0, s.ry * scale * swell)
    cy = top - ry * 0.25
    cx = s.cx + dx
    stem(c, cx, s.base, top, hw, ring=s.ring if scale >= 0.98 else None)
    cap(c, cx, cy, rx, ry, v, glow=glow, spots=s.spots if spots else (), cracks=cracks)
    return cx, cy, rx, ry


def sprout_frame(s, v, i):
    c = raster.Canvas(s.cell, s.cell)
    scales = [0.0, 0.3, 0.55, 0.8, 1.0]
    k = scales[i]
    if s.titan:
        roots(c, s, 4 + 8 * k)
        side_caps(c, s, v, k)
    if i == 0:
        mound(c, s.cx, s.base, 5 * s.k, broken=True)
        c.set(s.cx, s.base - 2, v["cap"][2])
        c.set(s.cx - 1, s.base - 2, v["cap"][3])
        if s.titan:
            c.disc(s.cx, s.base - 3, 3, 2, v["cap"][3])
            c.set(s.cx, s.base - 4, v["cap"][1])
    else:
        mound(c, s.cx, s.base, 4 * s.k * (1.1 - k * 0.5), broken=False)
        grown(c, s, v, k, glow=0.1, spots=k > 0.5)
    outline(c)
    if i < 3:
        specks(c, s.cx, s.base - 1, 5 * s.k * (1 + i), 5 + i * 2, 0.13 * i, 1.0)
    occ_line(c, s, 0.4 + 0.6 * k)
    return c


def swell_frame(s, v, i):
    c = raster.Canvas(s.cell, s.cell)
    grow = 1.0 + 0.06 * (i + 1)
    tremble = [0, 1, -1, 1][i]
    if s.titan:
        roots(c, s, 12)
        side_caps(c, s, v, 1.0)
    # The cap inflates past the stem's proportions - that bulge, and the light splitting it, is
    # the warning. The ring on the ground (drawn by SporeMushroom) says how far it will reach.
    cx, cy, rx, ry = grown(c, s, v, 1.0, glow=0.3 + 0.23 * i, cracks=(0 if i < 2 else i * 2 - 1),
                           dx=tremble, swell=grow)
    outline(c)
    mote = v["mote"]
    # Spores already leaking from the gills (poison drips, fire throws sparks up).
    rising = v is VARIANTS["fire"]
    for k in range(2 + i * int(2 * s.k)):
        f = (k * 0.618 + i * 0.21) % 1.0
        x = cx + (f - 0.5) * rx * 1.8
        y = (cy - ry - 2 - f * 4 * s.k) if rising else (cy + ry * 0.5 + 2 + f * 3)
        c.set(x, y, mote[1] if k % 3 else mote[0])
    occ_line(c, s, 1.0)
    return c


def burst_frame(s, v, i):
    c = raster.Canvas(s.cell, s.cell)
    ramp = v["cap"]
    big = s.k if s.k < 2 else 2.5
    full_top = s.base - s.stem_h
    cy = full_top - s.ry * 0.25
    if s.titan:
        roots(c, s, 12 - i)
    # What is left standing: a torn stem that withers back into the ground.
    stem_top = [full_top, full_top + 1, full_top + 3, s.base - s.stem_h * 0.45, s.base - 3][i]
    if i < 4:
        stem(c, s.cx, s.base, stem_top, s.hw * (1.0 - 0.12 * i), torn=True, under=v["under"])
    else:
        mound(c, s.cx, s.base, 4 * big, broken=True)
    outline(c)
    # The flash, then the spores.
    if i == 0:
        c.radial(s.cx, cy, s.rx * 0.95, s.ry * 1.1, [ramp[0], ramp[0], ramp[0], ramp[1], ramp[1], ramp[2]])
    # Kept close round the cap: the spores that fill the burst's radius are SporeMushroom's puffs,
    # placed inside the radius it tests. These only say "the cap burst", so they must not reach
    # past the smallest radius a mushroom bursts at (a Mycelial Bloom grandchild, about 60 px).
    if 1 <= i <= 3:
        thin = [0.0, 0.1, 0.4, 0.72][i]
        radius = (s.rx * 0.5) + (s.rx * 0.25) * i
        lobe = (3.2 - 0.5 * i) * big
        spore = ramp if v is VARIANTS["poison"] or i < 2 else ASH
        puff_lobes(c, s.cx, cy, radius, lobe, spore, thin, 0.4 * i)
    # Cap fragments thrown out and falling.
    if 1 <= i <= 3:
        pieces = 5 if s.k < 1.4 else 7 if s.k < 2 else 9
        for k in range(pieces):
            a = -math.pi * (0.1 + 0.8 * k / float(pieces - 1))
            r = s.rx * (0.8 + 0.45 * i)
            fx = s.cx + math.cos(a) * r
            fy = cy + math.sin(a) * r * 0.7 + i * i * big
            c.rect(fx, fy, fx + big, fy + (1 if s.k < 2 else 2), ramp[3])
            c.set(fx, fy, ramp[2])
    if i >= 2:
        mote = v["mote"]
        for k in range(3 + 2 * (4 - i)):
            f = (k * 0.618 + i * 0.3) % 1.0
            c.set(s.cx + (f - 0.5) * s.rx * 2.2, cy - 2 + ((k * 0.41) % 1.0) * s.ry * 2, mote[2] if k % 2 else mote[1])
    occ_line(c, s, 0.6 if i < 4 else 0.35)
    return c


def occ_line(c, s, width):
    """The contact shadow on row cell-1, under everything."""
    half = max(1, int((s.hw + 3) * width))
    for x in range(s.cx - half, s.cx + half):
        if not c.get(x, s.cell - 1)[3]:
            c.set(x, s.cell - 1, OCC)


def mushroom_strip(s, v):
    frames = [sprout_frame(s, v, i) for i in range(5)]
    frames += [swell_frame(s, v, i) for i in range(4)]
    frames += [burst_frame(s, v, i) for i in range(5)]
    return frames


# --- the effects placed inside a radius ------------------------------------------------------

def puff_frame(v, i, n=6):
    """One puff, opening out and thinning. Drawn small; the burst places a dozen inside its radius."""
    c = raster.Canvas(16, 16)
    t = i / float(n - 1)
    ramp = v["cap"]
    smoke = v is VARIANTS["fire"] and t > 0.45
    colours = ASH if smoke else ramp
    puff_lobes(c, 8, 8, 1.5 + 3.6 * t, 3.4 - 1.2 * t, colours, max(0.0, t - 0.25) * 1.1, t * 0.8)
    if t < 0.35:
        c.disc(8, 8, 2.6 * (1 - t), 2.2 * (1 - t), ramp[1])
        c.set(8, 8, ramp[0])
    return c


def ember_frame(i, n=4):
    """A lick of flame on a burning patch: three tongues out of step with each other."""
    c = raster.Canvas(16, 16)
    f = bl.ELEMENTS["fire"]
    for k, (x0, h0) in enumerate(((5, 7), (8, 10), (11, 6))):
        h = h0 + [0, 2, -1, 1][(i + k) % 4]
        lean = [0, 1, 0, -1][(i + k * 2) % 4]
        for y in range(h):
            yy = 14 - y
            u = y / float(max(1, h))
            w = max(0, int(round(2.2 * (1.0 - u))))
            cx = x0 + int(round(lean * u))
            for x in range(cx - w, cx + w + 1):
                edge = abs(x - cx) == w
                c.set(x, yy, f[3] if edge else f[2] if u > 0.45 else f[1])
            if u < 0.3:
                c.set(cx, yy, f[0])
    c.hline(4, 12, 15, f[3])
    return c


# --- writing ----------------------------------------------------------------------------------

def write_strip(frames, cell, name):
    from PIL import Image
    strip = Image.new("RGBA", (cell * len(frames), cell), (0, 0, 0, 0))
    for i, c in enumerate(frames):
        bad = c.audit()
        if bad:
            print("OFF-CONTRACT COLOURS in %s frame %d: %s" % (name, i, bad[:4]))
            return False
        strip.alpha_composite(c.img, (i * cell, 0))
    strip.save(os.path.join(OUT_DIR, name))
    print("  %-28s %2d frames of %d px" % (name, len(frames), cell))
    return True


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    ok = True
    for vname in ("poison", "fire"):
        v = VARIANTS[vname]
        ok &= write_strip(mushroom_strip(SMALL, v), 32, "spore-mushroom-%s.png" % vname)
        ok &= write_strip(mushroom_strip(GIANT, v), 48, "spore-giantcap-%s.png" % vname)
        ok &= write_strip(mushroom_strip(TITAN, v), 64, "spore-doomcap-%s.png" % vname)
        ok &= write_strip([puff_frame(v, i) for i in range(6)], 16, "spore-puff-%s.png" % vname)
    ok &= write_strip([ember_frame(i) for i in range(4)], 16, "spore-ember.png")
    if not ok:
        return 1
    print("toxic spore art: palette clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
