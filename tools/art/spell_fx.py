# -*- coding: utf-8 -*-
"""Spell projectiles and impacts. Run: python tools/art/spell_fx.py

Thirteen effects, every one of which was a frame out of a bought magic-effects pack. That art is
licensed for use and not for redistribution, so none of it can be in a public repository - see
`.ai/asset-licensing.md`.

ON CONTRACT AT LAST, which is a second thing these fix. The bought frames were 72x72 cells shown
at scales of 0.24, 0.42, 0.58, 0.7, 0.8, 0.85 and 1.25 - seven different fractional scales under
nearest filtering, which is `.ai/art-direction.md` section 1's worst case: a pixel of source
covering a fraction of a pixel of screen, so the grid shimmers as the sprite moves. These are the
32x32 projectile cell, rendered at x2, shown at scale 2. One number, no fractions, nothing
resampled.

THEY ARE EFFECTS, NOT OBJECTS, which is the opposite of the rule the relic icons follow: emissive,
element ramps, white-hot core, identity in the mid and edge. The core of every element is the same
near-white, and that is deliberate - it is what makes a fireball and a frost shard read as the
same KIND of thing at a glance while still being instantly told apart.

WHAT THE ANIMATION IS FOR. A projectile that loops a shape is decoration; these animate the thing
the spell actually does. Glacial Spike grows and cracks, Obsidian Spike rises and withdraws, the
spore cloud expands and thins. Where a spell has no motion of its own - an arrow, a thorn - it
gets one frame and no loop, because four frames of an arrow not moving is worse than one.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")

CELL = 32
TAU = math.pi * 2.0


def E(name):
    return bl.ELEMENTS[name]


def blob(c, cx, cy, r, ramp, squash=1.0):
    """The standard emissive body: edge, mid, hot, white-hot core."""
    c.disc(cx, cy, r, r * squash, ramp[3])
    c.disc(cx, cy, r * 0.74, r * 0.74 * squash, ramp[2])
    c.disc(cx, cy, r * 0.46, r * 0.46 * squash, ramp[1])
    c.disc(cx, cy, r * 0.20, r * 0.20 * squash, ramp[0])


def jag(c, pts, ramp, width=1):
    """A bolt: a dark spine with a hot line on it, drawn point to point."""
    for i in range(len(pts) - 1):
        x0, y0 = pts[i]
        x1, y1 = pts[i + 1]
        for w in range(-width, width + 1):
            c.line(x0 + w, y0, x1 + w, y1, ramp[3] if abs(w) == width else ramp[2])
        c.line(x0, y0, x1, y1, ramp[1])
    for (px, py) in pts[::2]:
        c.set(px, py, ramp[0])


def spark(c, spots, ramp):
    for i, (sx, sy) in enumerate(spots):
        c.set(sx, sy, ramp[1] if i % 2 else ramp[2])
        c.set(sx + 1, sy, ramp[3])


# --- bolts ------------------------------------------------------------------------------------

def chain_lightning(i, n):
    """A live arc, re-jagged every frame. The zig-zag is the whole identity."""
    c = raster.Canvas(CELL, CELL, None)
    l = E("lightning")
    phase = i / float(n)
    pts = []
    for k in range(6):
        t = k / 5.0
        wob = math.sin((t * 3.0 + phase) * TAU) * 6.0
        pts.append((16 + wob, 2 + t * 28))
    jag(c, pts, l, 1)
    spark(c, [(16 + math.sin((phase + q * 0.3) * TAU) * 10, 6 + q * 8) for q in range(3)], l)
    return c


def shadow_bolt(i, n):
    """A hollow ring with a tail, NOT a bolt.

    It carries debuffs rather than damage and it must never be mistaken for Chain Lightning, so
    it is drawn as the opposite shape: a closed loop with a hole in it, trailing backwards.
    """
    c = raster.Canvas(CELL, CELL, None)
    d = E("darkness")
    pulse = 1.0 + 0.12 * math.sin(i / float(n) * TAU)
    c.disc(19, 16, 9.0 * pulse, 9.0 * pulse, d[3])
    c.disc(19, 16, 6.5 * pulse, 6.5 * pulse, d[2])
    c.disc(19, 16, 4.0 * pulse, 4.0 * pulse, OCC)
    c.ring(19, 16, 5.4 * pulse, 5.4 * pulse, d[1], 1)
    c.set(15, 12, d[0])
    for k in range(4):
        t = k / 3.0
        c.disc(11 - k * 2.4, 16 + math.sin((i / float(n) + t) * TAU) * 2.0,
               3.4 * (1.0 - t * 0.7), 3.4 * (1.0 - t * 0.7), d[3])
    return c


def gale_blade(i, n):
    """A crescent of moving air. Three arcs, each a little further along than the last."""
    c = raster.Canvas(CELL, CELL, None)
    w = E("wind")
    lead = i / float(n) * 4.0
    for k, (r, tone) in enumerate(((13.0, w[3]), (10.0, w[2]), (7.0, w[1]))):
        start = -0.9 + k * 0.12 + lead * 0.06
        for d in range(0, 110, 3):
            a = start + math.radians(d)
            c.set(10 + math.cos(a) * r, 16 + math.sin(a) * r, tone)
            c.set(10 + math.cos(a) * (r - 1), 16 + math.sin(a) * (r - 1), tone)
    c.set(10 + 13, 16, w[0])
    return c


# --- fire -------------------------------------------------------------------------------------

def fireball(i, n):
    """A burning ball with the flame trailing behind it rather than wrapped around it."""
    c = raster.Canvas(CELL, CELL, None)
    f = E("fire")
    wob = math.sin(i / float(n) * TAU)
    for k in range(5):
        t = k / 4.0
        c.disc(17 - k * 2.6, 16 + wob * t * 3.0, 6.5 * (1.0 - t * 0.72), 5.0 * (1.0 - t * 0.72),
               f[3] if k else f[2])
    blob(c, 20, 16, 8.0, f)
    spark(c, [(6, 12 + wob * 2), (9, 21 - wob * 2)], f)
    return c


def molten_shard(i, n):
    """A splinter of hot metal, tumbling. Metal ramp with a fire core - it is a THING, heated."""
    c = raster.Canvas(CELL, CELL, None)
    m = E("metal")
    f = E("fire")
    a = i / float(n) * math.pi
    dx, dy = math.cos(a) * 10.0, math.sin(a) * 10.0
    for w in (-2, -1, 0, 1, 2):
        c.line(16 - dx + w, 16 - dy, 16 + dx + w, 16 + dy, m[3] if abs(w) == 2 else m[2])
    c.line(16 - dx, 16 - dy, 16 + dx, 16 + dy, f[2])
    c.set(16 + dx, 16 + dy, f[0])
    c.set(16 - dx, 16 - dy, m[0])
    return c


def meteor_impact(i, n):
    """The crater, one frame. A struck ground rather than a ball in flight."""
    c = raster.Canvas(CELL, CELL, None)
    f = E("fire")
    c.disc(16, 20, 14.0, 6.5, f[3])
    c.disc(16, 20, 10.0, 4.5, f[2])
    c.disc(16, 20, 5.5, 2.4, f[1])
    c.disc(16, 20, 2.2, 1.2, f[0])
    for d in range(0, 360, 40):
        a = math.radians(d)
        c.line(16 + math.cos(a) * 7, 20 + math.sin(a) * 3.2,
               16 + math.cos(a) * 15, 20 + math.sin(a) * 7.0, f[3])
    return c


def scorching_ray(i, n):
    """One segment of a sustained beam, tiled along its length by ScorchingRayBeam."""
    c = raster.Canvas(CELL, CELL, None)
    f = E("fire")
    for (half, tone) in ((7.0, f[3]), (4.5, f[2]), (2.4, f[1]), (1.0, f[0])):
        c.rect(0, 16 - half, 31, 16 + half, tone)
    for x in range(2, 31, 6):
        c.set(x, 16 - 8, f[3])
        c.set(x + 3, 16 + 8, f[3])
    return c


# --- ice --------------------------------------------------------------------------------------

def glacial_spike(i, n):
    """It grows, then it cracks. Eight frames of one event rather than a loop."""
    c = raster.Canvas(CELL, CELL, None)
    ice = E("ice")
    t = i / float(n - 1)
    grow = min(1.0, t / 0.55)
    top = 30 - 26 * grow
    for y in range(int(top), 31):
        u = (y - top) / max(1.0, 30 - top)
        half = 1.0 + 6.0 * u
        c.hline(16 - half, 16 + half, y, ice[3])
        c.hline(16 - half + 1, 16 + half - 1, y, ice[2])
    c.line(16, top + 1, 16, 29, ice[1])
    c.set(16, int(top), ice[0])
    if t > 0.6:
        crack = (t - 0.6) / 0.4
        for (cx0, cy0, cx1, cy1) in ((16, 12, 11, 20), (16, 16, 21, 24), (16, 20, 13, 28)):
            c.line(cx0, cy0, cx0 + (cx1 - cx0) * crack, cy0 + (cy1 - cy0) * crack, ice[0])
    return c


def frost_shard(i, n):
    """A single splinter of ice, thrown. One frame - it does not change on the way."""
    c = raster.Canvas(CELL, CELL, None)
    ice = E("ice")
    c.poly([(4, 18), (26, 12), (28, 16), (6, 22), (4, 18)], ice[3])
    c.poly([(6, 17), (25, 13), (26, 15), (7, 20), (6, 17)], ice[2])
    c.line(8, 17, 25, 14, ice[1])
    c.set(27, 15, ice[0])
    c.set(28, 16, ice[0])
    spark(c, [(3, 14), (2, 21)], ice)
    return c


# --- earth and growth ---------------------------------------------------------------------------

def obsidian_spike(i, n):
    """Spikes out of the floor: up fast, hold, and withdraw. Ten frames of one gesture."""
    c = raster.Canvas(CELL, CELL, None)
    e = E("earth")
    t = i / float(n - 1)
    rise = min(1.0, t / 0.3) if t < 0.65 else max(0.0, 1.0 - (t - 0.65) / 0.35)
    for (bx, h, w) in ((16, 24, 5), (7, 15, 3), (25, 17, 3)):
        top = 30 - h * rise
        if 30 - top < 1.5:
            continue
        c.poly([(bx - w, 30), (bx, top), (bx + w, 30), (bx - w, 30)], e[3])
        c.line(bx - w + 1, 29, bx, top + 1, e[2])
        c.line(bx, top, bx, 29, OCC)
        c.set(bx, top, e[1])
    c.hline(2, 29, 30, OCC)
    return c


def thorn_vine(i, n):
    """A length of thorned stem. One frame: the spell moves it, the sprite does not."""
    c = raster.Canvas(CELL, CELL, None)
    g = E("grass")
    prev = None
    for k in range(26):
        t = k / 25.0
        x = 3 + 26 * t
        y = 16 + math.sin(t * TAU * 0.8) * 7.0
        for w in (-1, 0, 1):
            c.set(x, y + w, g[3] if abs(w) else g[2])
        prev = (x, y)
    for k in range(3, 26, 6):
        t = k / 25.0
        x = 3 + 26 * t
        y = 16 + math.sin(t * TAU * 0.8) * 7.0
        c.line(x, y, x + 2, y - 5, g[2])
        c.set(x + 2, y - 5, g[0])
        c.line(x, y, x - 2, y + 5, g[3])
        c.set(x - 2, y + 5, g[1])
    return c


def toxic_spore_burst(i, n):
    """A cloud opening out and thinning. The last frames are mostly gaps, which is the point."""
    c = raster.Canvas(CELL, CELL, None)
    p = E("poison")
    t = i / float(n - 1)
    spread = 4.0 + 11.0 * t
    for k, deg in enumerate(range(0, 360, 45)):
        a = math.radians(deg + t * 40)
        r = 3.6 * (1.0 - t * 0.55)
        cx = 16 + math.cos(a) * spread
        cy = 16 + math.sin(a) * spread * 0.85
        c.disc(cx, cy, r, r, p[3])
        if t < 0.7:
            c.disc(cx, cy, r * 0.55, r * 0.55, p[2])
        if k % 2 == 0:
            c.set(cx, cy, p[1])
    core = 6.0 * (1.0 - t)
    if core > 0.8:
        blob(c, 16, 16, core, p)
    return c


# --- other --------------------------------------------------------------------------------------

def hunters_arrow(i, n):
    """An arrow. Wood, iron and fletching - the one projectile in the game that is an object."""
    c = raster.Canvas(CELL, CELL, None)
    shaft = [bl.tone("skin", r) for r in ("hi", "lit", "base", "shade", "deep")]
    steel = [bl.tone("steel", r) for r in ("hi", "lit", "base", "shade", "deep")]
    w = E("wind")
    c.rect(4, 15, 25, 17, shaft[2])
    c.hline(4, 25, 15, shaft[1])
    c.hline(4, 25, 17, shaft[4])
    c.poly([(25, 12), (31, 16), (25, 20), (25, 12)], steel[2])
    c.line(25, 12, 31, 16, steel[1])
    c.set(31, 16, steel[0])
    for k in range(3):
        c.line(4 + k * 2, 11 + k, 10 + k * 2, 15, w[2])
        c.line(4 + k * 2, 21 - k, 10 + k * 2, 17, w[3])
    return c


def magic_missile(i, n):
    """A dart of arcane force. Solid, pointed, with a short trail.

    It has to be told apart from the other two things that fly in a straight line and glow:
    Chain Lightning is a jagged open line, Shadow Bolt is a hollow ring. So this one is the SOLID
    shape - a filled head with a tail behind it and no hole anywhere in it.
    """
    c = raster.Canvas(CELL, CELL, None)
    a = E("arcane")
    pulse = 1.0 + 0.10 * math.sin(i / float(n) * TAU)

    for k in range(5):
        t = k / 4.0
        c.disc(15 - k * 2.7, 16 + math.sin((i / float(n) + t) * TAU) * 1.6,
               4.0 * (1.0 - t * 0.78), 3.2 * (1.0 - t * 0.78), a[3])

    c.poly([(11, 10), (28, 16), (11, 22), (11, 10)], a[3])
    c.poly([(14, 12), (26, 16), (14, 20), (14, 12)], a[2])
    c.disc(18, 16, 4.0 * pulse, 3.4 * pulse, a[1])
    c.disc(18, 16, 1.8 * pulse, 1.6 * pulse, a[0])
    c.set(28, 16, a[0])
    spark(c, [(8, 12), (6, 20)], a)
    return c


def arcane_explosion(i, n):
    """A burst that opens into a ring and thins. Six frames of one event, not a loop.

    It goes off around the player rather than at a target, so the middle has to CLEAR - the last
    frames are an expanding rim with nothing inside it, which is what lets the player still see
    themselves standing in the middle of their own spell.
    """
    c = raster.Canvas(CELL, CELL, None)
    a = E("arcane")
    t = i / float(n - 1)
    r = 3.0 + 13.0 * (t ** 0.7)

    thick = 5.0 * (1.0 - t * 0.55)
    for d in range(0, 360, 3):
        ang = math.radians(d)
        # Nine lobes, not five. At five the ring came out a visible pentagon, which reads as a
        # rune rather than as a blast front.
        wob = math.sin(ang * 9.0 + t * 4.0) * 0.9
        for k in range(int(thick)):
            rr = r + wob - k
            tone = a[1] if k < thick * 0.3 else (a[2] if k < thick * 0.66 else a[3])
            c.set(16 + math.cos(ang) * rr, 16 + math.sin(ang) * rr, tone)

    if t < 0.45:
        core = 5.0 * (1.0 - t / 0.45)
        blob(c, 16, 16, core, a)
    for k in range(6):
        ang = k * TAU / 6.0 + t * 1.4
        c.set(16 + math.cos(ang) * (r + 3), 16 + math.sin(ang) * (r + 3), a[0])
    return c


# name -> (function, frame count, loop)
FX = [
    ("magic-missile", magic_missile, 4, True),
    ("arcane-explosion", arcane_explosion, 6, False),
    ("chain-lightning", chain_lightning, 6, True),
    ("shadow-bolt", shadow_bolt, 6, True),
    ("gale-blade", gale_blade, 4, True),
    ("fireball", fireball, 4, True),
    ("molten-shard", molten_shard, 4, True),
    ("meteor-impact", meteor_impact, 1, False),
    ("scorching-ray", scorching_ray, 1, False),
    ("glacial-spike", glacial_spike, 8, False),
    ("frost-shard", frost_shard, 1, False),
    ("obsidian-spike", obsidian_spike, 10, False),
    ("thorn-vine", thorn_vine, 1, False),
    ("toxic-spore-burst", toxic_spore_burst, 6, False),
    ("hunters-arrow", hunters_arrow, 1, False),
]


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    ok = True
    total = 0
    for name, fn, frames, _loop in FX:
        strip = None
        for i in range(frames):
            c = fn(i, frames)
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s frame %d:" % (name, i))
                for h, cnt in bad[:5]:
                    print("   %s  x%d" % (h, cnt))
                break
            img = c.scaled(2)
            if strip is None:
                from PIL import Image
                strip = Image.new("RGBA", (CELL * 2 * frames, CELL * 2), (0, 0, 0, 0))
            strip.alpha_composite(img, (i * CELL * 2, 0))
        if strip is not None:
            strip.save(os.path.join(OUT_DIR, "%s.png" % name))
            total += frames
            print("  %-20s %d frame%s" % (name, frames, "" if frames == 1 else "s"))

    if not ok:
        return 1
    print("")
    print("%d spell effects, %d frames, palette clean" % (len(FX), total))
    return 0


if __name__ == "__main__":
    sys.exit(main())
