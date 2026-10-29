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
the spell actually does. Glacial Spike grows and cracks; Obsidian Spike bursts out of the ground,
catches the light and breaks back into it (its own 48x48 sheet, obsidian_sheet). Where
a spell has no motion of its own - an arrow, a thorn - it gets one frame and no loop, because four
frames of an arrow not moving is worse than one. (The spore cloud that was here went with the old
Toxic Spore Burst pulse; its mushrooms are spore_mushrooms.py. Glacial Spike's grow-and-crack moved to
glacial_ice.py in 2026-10-29's pixel-art pass, which draws it as faceted ice in three sizes.)
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import obsidian_glass as glass
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

# Glacial Spike's sheet left this file on 2026-10-29 for its own generator, glacial_ice.py: three sizes of
# faceted ice in a 40x72 cell, never scaled (the pixel-art pass). It writes glacial-spike.png itself.


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

# Obsidian Spike's sheet: the 48x48 impact cell of art-direction.md section 1, rendered x2 and shown
# at scale 1 (scenes/ObsidianSpike.tscn, drawn by scripts/GroundSpike.cs). Four rows:
#   0  the cluster, OBSIDIAN_FRAMES frames: the tip breaking the ground, the burst, the glint
#      running up the ridge, the fracture, the collapse back into the ground
#   1  a splinter, SPLINTER_FRAMES frames: the small blades that come up at the ends of the cracks
#      once the eruption is wider than its own cluster (Splinter Field, Tectonic Uprising)
#   2, 3  the same two, lit by Darkness instead of Earth: Umbral Vein, the spell's element path
# The ground cracks, the chips of glass thrown out and the dust are NOT here: they are drawn by
# GroundSpike out of the radius the eruption hits, so they reach exactly as far as it hurts.
OBSIDIAN_CELL = 48
OBSIDIAN_GY = 44
OBSIDIAN_FRAMES = 12
SPLINTER_FRAMES = 7


def _scaled_blade(c, b, r, heat, ramp, gy, **kw):
    """A blade (apex, foot_l, foot_r, ridge) risen r of its height: the part above the ground of a
    blade sliding up along its own axis, so it comes up point first and widens as it rises."""
    (ax, ay), fl, fr, rd = b
    if r <= 0.02:
        return
    fc = (fl + fr) / 2.0
    apex = (fc + (ax - fc) * r, gy - (gy - ay) * r)
    glass.blade(c, apex, fc + (fl - fc) * r, fc + (fr - fc) * r, fc + (rd - fc) * r, gy,
                heat=heat, ramp=ramp, **kw)


def _fracture(c, b, ramp, gy, at=(0.42, 0.66)):
    """Glowing fracture lines across a standing blade, just before it breaks."""
    (ax, ay), fl, fr, rd = b
    hot, mid = ramp[1], ramp[2]
    for k, t in enumerate(at):
        y = ay + (gy - ay) * t
        xl = ax + (fl - ax) * t + 1
        xr = ax + (fr - ax) * t - 1
        xm = (xl + xr) / 2.0
        c.line(xl, y + 1, xm, y - 1 + k, hot)
        c.line(xm, y - 1 + k, xr, y + 1, mid)


def _mouth(c, cx, gy, heat, ramp):
    """Where the glass came through the ground: a slot of the element's light at the foot, white-hot
    at its heart while the eruption is fresh, cooling down the ramp after."""
    core, hot, mid, edge = ramp
    if heat <= 0.0:
        return
    w = 3 + int(round(6 * heat))
    c.hline(cx - w, cx + w, gy + 1, edge)
    c.hline(cx - w + 2, cx + w - 2, gy + 1, mid if heat < 0.8 else hot)
    if heat >= 0.5:
        c.hline(cx - w + 3, cx + w - 3, gy, mid)
    if heat >= 0.8:
        c.hline(cx - 2, cx + 2, gy + 1, core)
        c.hline(cx - 1, cx + 1, gy, hot)
    c.set(cx - w - 1, gy + 2, edge)
    c.set(cx + w + 1, gy + 2, edge)


# The cluster at full height: the icon's composition (spell_icons_core.obsidian_spike) on the bigger
# cell - one tall blade leaning into the burst, a blade splayed out behind it on the left and one in
# front on the right, and a stub breaking the ground in front. ((apex), foot_l, foot_r, ridge).
CLUSTER = (
    ("left", ((6, 22), 10, 18, 14)),
    ("main", ((31, 5), 17, 33, 24)),
    ("right", ((44, 26), 31, 40, 34)),
    ("stub", ((15, 35), 11, 20, 16)),
)

# Frame by frame: the main blade's rise, the side blades' rise, the heat at the foot, where the
# glint stands on the main ridge (0 point .. 1 foot, None for none), the fracture, and how many
# chips of the broken blade are falling (0 none). Frames 0-2 come before the hit; frame 3 is the
# hit (GroundSpike.TelegraphDuration), overshooting a little; 4-6 hold while the light runs up the
# ridge; 7 cracks; 8-11 fall back into the ground.
CLUSTER_FRAMES = (
    # main, sides, heat, glint, fracture, falling
    (0.10, 0.00, 0.55, None, False, 0),
    (0.38, 0.12, 0.75, None, False, 0),
    (0.78, 0.50, 0.90, None, False, 0),
    (1.06, 0.98, 1.00, None, False, 0),
    (1.00, 1.00, 0.90, 0.55, False, 0),
    (1.00, 1.00, 0.75, 0.22, False, 0),
    (1.00, 1.00, 0.60, 0.04, False, 0),
    (1.00, 0.95, 0.50, None, True, 0),
    (0.66, 0.62, 0.40, None, True, 1),
    (0.40, 0.34, 0.30, None, False, 2),
    (0.18, 0.12, 0.20, None, False, 3),
    (0.06, 0.00, 0.10, None, False, 4),
)


def obsidian_cluster(i, ramp):
    c = raster.Canvas(OBSIDIAN_CELL, OBSIDIAN_CELL, None)
    gy = OBSIDIAN_GY
    rise, sides, heat, glint, fracture, falling = CLUSTER_FRAMES[i]
    blades = dict(CLUSTER)

    # Before the hit: pebbles of the floor jumping as the ground is pushed up from under.
    if i <= 2:
        stone = bl.MATERIALS["stone"]
        for (x, y) in ((19, 41 - i), (29, 40 - 2 * i), (24, 42 - i)):
            c.set(x, y, stone[0])
            c.set(x, y + 1, stone[2])

    _mouth(c, 25, gy, heat, ramp)
    _scaled_blade(c, blades["left"], sides, heat * 0.7, ramp, gy, lean_l=0.5, spec=0.5, ripples=False)
    _scaled_blade(c, blades["main"], rise, heat, ramp, gy, lean_l=1.5, lean_r=0.5, chip=0.4, spec=0.62)
    _scaled_blade(c, blades["right"], sides, heat * 0.8, ramp, gy, lean_r=0.5, spec=0.45, ripples=False)
    _scaled_blade(c, blades["stub"], min(1.0, sides * 1.1), heat * 0.8, ramp, gy + 1, band=False,
                  ripples=False, spec=0.6)

    if glint is not None:
        (ax, ay), fl, fr, rd = blades["main"]
        sx = int(round(ax + (rd - ax) * glint))
        sy = int(round(ay + (gy - ay) * glint))
        arm = 2 if glint < 0.1 else 1
        for d in range(1, arm + 1):
            for (dx, dy) in ((-d, 0), (d, 0), (0, -d), (0, d)):
                c.set(sx + dx, sy + dy, glass.SPEC2 if d == 1 else glass.GLASS[0])
        c.set(sx, sy, glass.SPEC)

    if fracture:
        if rise >= 0.99:
            _fracture(c, blades["main"], ramp, gy)
        _fracture(c, blades["right"], ramp, gy, at=(0.5,))

    # The broken top of the main blade coming down in pieces round what is left of it.
    for k in range(falling):
        for (x, y) in ((27, 12), (35, 18), (21, 16), (31, 24))[:4 - (k // 2)]:
            fy = min(gy - 1, y + 7 * k + (x % 3))
            fx = x + (-2 if x < 25 else 2) * k
            glass.chip(c, fx, fy)
    return c


# A splinter: one small blade and a stub, coming up where a crack ends, then sinking.
SPLINTER = (((25, 30), 21, 29, 24), ((19, 37), 17, 23, 20))
SPLINTER_RISE = ((0.30, 0.0), (0.70, 0.3), (1.08, 0.9), (1.00, 1.0), (1.00, 1.0), (0.55, 0.5), (0.20, 0.1))


def obsidian_splinter(i, ramp):
    c = raster.Canvas(OBSIDIAN_CELL, OBSIDIAN_CELL, None)
    gy = OBSIDIAN_GY
    rise, stub = SPLINTER_RISE[i]
    heat = (1.0, 1.0, 0.9, 0.7, 0.5, 0.35, 0.2)[i]
    _mouth(c, 24, gy, heat * 0.6, ramp)
    _scaled_blade(c, SPLINTER[0], rise, heat, ramp, gy, lean_l=0.5, spec=0.55, ripples=False, band=True)
    _scaled_blade(c, SPLINTER[1], stub, heat, ramp, gy, band=False, ripples=False, spec=0.6)
    if i == 3:
        (ax, ay), _, _, _ = SPLINTER[0]
        for (dx, dy) in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            c.set(ax + dx, ay + 2 + dy, glass.SPEC2)
        c.set(ax, ay + 2, glass.SPEC)
    if i == 4:
        _fracture(c, SPLINTER[0], ramp, gy, at=(0.5,))
    return c


def obsidian_sheet():
    """The four rows, as one image, x2. Returns (image, problems)."""
    from PIL import Image
    cell = OBSIDIAN_CELL
    sheet = Image.new("RGBA", (cell * 2 * OBSIDIAN_FRAMES, cell * 2 * 4), (0, 0, 0, 0))
    problems = []
    for row, (paint, frames, ramp) in enumerate((
            (obsidian_cluster, OBSIDIAN_FRAMES, E("earth")),
            (obsidian_splinter, SPLINTER_FRAMES, E("earth")),
            (obsidian_cluster, OBSIDIAN_FRAMES, E("darkness")),
            (obsidian_splinter, SPLINTER_FRAMES, E("darkness")))):
        for i in range(frames):
            cv = paint(i, ramp)
            bad = cv.audit()
            if bad:
                problems.append("row %d frame %d: %s" % (row, i, ", ".join(h for h, _ in bad[:5])))
            sheet.alpha_composite(cv.scaled(2), (i * cell * 2, row * cell * 2))
    return sheet, problems


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
    ("frost-shard", frost_shard, 1, False),
    ("thorn-vine", thorn_vine, 1, False),
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

    sheet, problems = obsidian_sheet()
    if problems:
        ok = False
        print("OFF-CONTRACT COLOURS in obsidian-spike: " + "; ".join(problems))
    else:
        sheet.save(os.path.join(OUT_DIR, "obsidian-spike.png"))
        total += 2 * (OBSIDIAN_FRAMES + SPLINTER_FRAMES)
        print("  %-20s %d frames on four rows, %dx%d cells" % ("obsidian-spike", 2 * (OBSIDIAN_FRAMES + SPLINTER_FRAMES),
                                                             OBSIDIAN_CELL, OBSIDIAN_CELL))

    if not ok:
        return 1
    print("")
    print("%d spell effects, %d frames, palette clean" % (len(FX) + 1, total))
    return 0


if __name__ == "__main__":
    sys.exit(main())
