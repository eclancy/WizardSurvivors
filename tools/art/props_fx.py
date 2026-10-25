# -*- coding: utf-8 -*-
"""Interactables - the chest, the two traps, the two pickups, the shield aura - and two portraits.

The traps also write their SpriteFrames (scenes/resources/SpikeTrapFrames.tres, FlameVentFrames.tres):
four animations each, one per StageHazard phase - see "the traps" below.

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
import random
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
#
# Reported: "the floor fire traps should be an animated vent, and the floor spikes should be a bunch of
# holes." Both are drawn in FOUR PHASES, one animation each, which StageHazard plays from its own
# clock (the timing, damage and cycle are StageHazard's and did not change):
#
#   idle    dormant - the thing as it lies in the floor, readable as a trap before it ever fires
#   tell    rising  - scrubbed by the rise's progress: the warning builds to the moment it can hurt
#   active  active  - the spikes shoot up and hold / the fire vents, looped
#   cool    falling - scrubbed: back down into the floor
#
# THE PLATE AND THE GRATE ARE THE DANGER AREA. There is no ring drawn round either any more (the
# marker StageHazard used to draw): the spike plate is exactly the 40 px circle its CollisionShape2D
# is, and the vent's grate exactly its 76 x 34 rectangle, and StageHazard tests the player's BOOTS
# against that shape (Player.FootprintOverlaps) - so a boot on the iron is a hit, and a boot off it
# never is. What stands up off them (spikes, flame) is drawn above, the way anything stands in a
# three-quarter view; it is the plate on the floor that says where.
#
# Both are on the material rows (iron is the steel row in its deep half, set into stone with an `occ`
# contact edge) and touch an emissive ramp only where something genuinely glows: the fire, and the
# glint on a spike tip in the tell - steel's `hi`, one of the few bright tones the contract keeps for
# exactly this, a warning.

SPIKE_PLATE_R = 10          # art px: the 20 px collision radius at x2
# Top-left of each 2x2 hole, back row first: seven in a staggered (hex) pattern. A square grid put
# three spikes in a column, and in a three-quarter view a column of spikes stacks into one long bar;
# staggered, they stand between each other as a bed of points.
SPIKE_HOLES = [(12, 9), (18, 9), (9, 15), (15, 15), (21, 15), (12, 21), (18, 21)]
SPIKE_FULL = 10             # art px a spike stands at full height: the back row's point on the top row


def spike_plate(c):
    """An iron plate let into the floor, full of holes. Round, the collision's own circle."""
    st = ramp("steel")
    cx = cy = 16.0
    for y in range(CELL):
        for x in range(CELL):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            r = math.hypot(dx, dy)
            # Pixel centres within the radius and nothing outside it: the plate, contact edge and
            # all, is the collision circle (RegressionChecks.ValidateStageHazards holds it to that).
            if r > SPIKE_PLATE_R:
                continue
            if r > SPIKE_PLATE_R - 1.0:
                tone = OCC                                      # set into the stone
            elif r > SPIKE_PLATE_R - 2.6:
                lit = (-dx - dy) / max(0.01, r)                 # the key is upper-left
                tone = st[2] if lit > 0.35 else (st[4] if lit < -0.35 else st[3])
            else:
                tone = st[3]
                if (-dx - dy) > 9.0 and r > 5:
                    tone = st[2]                                # a worn bright patch, upper-left
            c.set(x, y, tone)
    # rivets between the holes, near the rim
    for (rx, ry) in ((8, 10), (23, 10), (8, 21), (23, 21)):
        c.set(rx, ry, st[2])
        c.set(rx + 1, ry + 1, OCC)


def spike_holes(c, glint=(), peek=0):
    """The 3 x 3 holes: dark, their far (lower-right) lip lit, a point of steel deep inside each.
    `peek` 1 shows each tip inside its hole; `glint` lists holes whose tip catches the light."""
    st = ramp("steel")
    for k, (hx, hy) in enumerate(SPIKE_HOLES):
        c.rect(hx, hy, hx + 1, hy + 1, OCC)
        c.set(hx + 2, hy + 1, st[2])
        c.set(hx + 1, hy + 2, st[2])
        c.set(hx + 1, hy + 1, st[4] if peek == 0 else st[1])
        if peek:
            c.set(hx, hy + 1, st[3])
        if k in glint:
            c.set(hx + 1, hy, st[0])


def spike(c, hx, hy, h, glint):
    """One spike standing out of the hole at (hx, hy), h art px tall: a steel cone three pixels across
    at the hole and one at the point, lit on its left, with an `occ` edge on its right so a spike in
    front never merges with the one behind it."""
    st = ramp("steel")
    base = hy + 1
    top = base - h
    for y in range(top, base + 1):
        u = (y - top) / float(max(1, h))                          # 0 at the point
        if u < 0.3 or h < 4:
            c.set(hx, y, st[1])
            c.set(hx + 1, y, OCC if h >= 4 else st[3])
        else:
            c.set(hx - 1, y, st[1] if u > 0.6 else c.get(hx - 1, y))
            c.set(hx, y, st[1])
            c.set(hx + 1, y, st[3])
            c.set(hx + 2, y, OCC)
    c.set(hx, top, st[0] if glint else st[1])


def spike_frame(phase, i):
    c = raster.Canvas(CELL, CELL, None)
    spike_plate(c)
    if phase == "idle":
        spike_holes(c)
        return c
    if phase == "tell":
        # The tips come up to the mouths of the holes and catch the light: one glint, then three,
        # then every tip showing - the warning is a thing happening in the holes, not a ring.
        glints = ((3,), (0, 3, 6), (0, 1, 2, 3, 4, 5, 6))[i]
        spike_holes(c, glint=glints, peek=1 if i > 0 else 0)
        if i == 2:
            for (hx, hy) in SPIKE_HOLES:
                spike(c, hx, hy, 2, True)
        return c
    heights = {"active": (6, SPIKE_FULL), "cool": (8, 4, 1)}[phase]
    h = heights[i]
    spike_holes(c, peek=1)
    # back row first, so the front spikes stand in front of the ones behind them
    for (hx, hy) in SPIKE_HOLES:
        spike(c, hx, hy, h, phase == "active" and i == 1)
    return c


VENT_CELL = 64              # a large prop: the flame column needs the height
VENT_X0, VENT_X1 = 13, 50   # the grate, inclusive: 38 art px = the 76 px collision width at x2
VENT_Y0, VENT_Y1 = 24, 40   # 17 art px = the 34 px collision height
VENT_SLOTS = [15 + k * 5 for k in range(7)]   # left column of each 3 px slot


def vent_grate(c, glow):
    """An iron grate set into the floor, slotted. `glow` 0..1 is the fire under it showing through:
    nothing, an ember at the bottom of the slots, then the slots filling with heat to white."""
    st = ramp("steel")
    f = bl.ELEMENTS["fire"]
    c.rect(VENT_X0, VENT_Y0, VENT_X1, VENT_Y1, OCC)
    c.rect(VENT_X0 + 1, VENT_Y0 + 1, VENT_X1 - 1, VENT_Y1 - 1, st[3])
    c.hline(VENT_X0 + 1, VENT_X1 - 1, VENT_Y0 + 1, st[2])
    c.vline(VENT_X0 + 1, VENT_Y0 + 1, VENT_Y1 - 1, st[2])
    c.hline(VENT_X0 + 1, VENT_X1 - 1, VENT_Y1 - 1, st[4])
    c.vline(VENT_X1 - 1, VENT_Y0 + 1, VENT_Y1 - 1, st[4])
    for (rx, ry) in ((VENT_X0 + 2, VENT_Y0 + 2), (VENT_X1 - 2, VENT_Y0 + 2),
                     (VENT_X0 + 2, VENT_Y1 - 2), (VENT_X1 - 2, VENT_Y1 - 2)):
        c.set(rx, ry, st[1])
    top, bottom = VENT_Y0 + 3, VENT_Y1 - 3
    for sx in VENT_SLOTS:
        for y in range(top, bottom + 1):
            # how far up the slot the heat reaches, bottom first
            u = (bottom - y) / float(bottom - top)
            heat = glow * 1.4 - u
            if heat <= 0.0:
                tone = OCC
            elif heat < 0.35:
                tone = f[3]
            elif heat < 0.7:
                tone = f[2]
            elif heat < 1.0:
                tone = f[1]
            else:
                tone = f[0]
            for x in range(sx, sx + 3):
                c.set(x, y, tone)
        c.set(sx, top - 1, OCC)                                 # the slot's shaded upper lip
        c.set(sx + 1, top - 1, OCC)
        c.set(sx + 2, top - 1, OCC)


def vent_embers(c, i, n, rise, smoke):
    """Sparks and smoke coming off the grate in the tell and as it cools."""
    f = bl.ELEMENTS["fire"]
    st = ramp("stone")
    rnd = random.Random(7919 * n + i)
    for k in range(n):
        x = VENT_SLOTS[rnd.randint(0, len(VENT_SLOTS) - 1)] + rnd.randint(0, 2)
        y = VENT_Y0 + 1 - rnd.randint(1, rise)
        c.set(x, y, f[1] if rnd.random() < 0.5 else f[0])
    for k in range(smoke):
        x = VENT_SLOTS[rnd.randint(0, len(VENT_SLOTS) - 1)] + rnd.randint(-1, 3)
        y = VENT_Y0 - 3 - rnd.randint(0, rise + 4)
        r = 1.2 + rnd.random() * 1.6
        c.disc(x, y, r, r, st[1] if k % 2 else st[0])


def vent_flames(c, i, scale):
    """The fire out of the slots: a tongue from each, leaning together into one sheet. White at the
    slots, gold, orange, red at the ragged top - the element's own ramp, core to edge."""
    f = bl.ELEMENTS["fire"]
    base = VENT_Y0 + 2
    centres = [sx + 1 for sx in VENT_SLOTS]
    for x in range(VENT_X0 + 1, VENT_X1):
        # the tongue whose slot is nearest, and how near: tall over a slot, dipping over a bar
        k = min(range(len(centres)), key=lambda j: abs(centres[j] - x))
        off = abs(centres[k] - x) / 2.5
        envelope = 1.0 - ((x - 31.5) / 22.0) ** 2                 # tallest in the middle
        flick = 0.78 + 0.22 * math.sin((k * 1.7 + i * 1.9) * 1.3) + 0.12 * math.sin(i * 2.4 + k)
        h = scale * (13 + 18 * envelope) * flick * (1.0 - 0.32 * min(1.0, off))
        if h < 1.5:
            continue
        top = base - h
        for y in range(int(top), base + 1):
            u = (base - y) / h                                    # 0 at the slots, 1 at the tip
            if u < 0.18:
                tone = f[0]
            elif u < 0.45:
                tone = f[1]
            elif u < 0.78:
                tone = f[2]
            else:
                tone = f[3]
            c.set(x, y, tone)
    # loose tongues breaking off the top
    rnd = random.Random(31 + i)
    for k in range(4):
        x = rnd.randint(VENT_X0 + 6, VENT_X1 - 6)
        y = int(base - scale * (30 + rnd.randint(0, 6)))
        c.set(x, y, f[2])
        c.set(x, y + 1, f[3])
        c.set(x + 1, y + 1, f[3])


def vent_frame(phase, i):
    c = raster.Canvas(VENT_CELL, VENT_CELL, None)
    if phase == "idle":
        # Two frames, slow: an ember breathing at the bottom of the slots - a vent that is warm.
        vent_grate(c, (0.18, 0.26)[i])
    elif phase == "tell":
        # The heat climbing the slots, sparks, then smoke: four frames scrubbed across the rise.
        vent_grate(c, (0.4, 0.6, 0.85, 1.0)[i])
        vent_embers(c, i, 2 + i * 2, 3 + i * 3, i)
    elif phase == "active":
        vent_flames(c, i, 1.0)
        vent_grate(c, 1.2)
    else:
        # Cooling: the flame dropping back into the slots, smoke off it, the glow going out.
        vent_flames(c, i + 1, (0.45, 0.18, 0.0)[i])
        vent_grate(c, (0.9, 0.55, 0.3)[i])
        vent_embers(c, i + 7, 2, 6 + i * 2, 2 + i)
    return c


# name, cell, painter, then (animation, frames, speed in fps, loops) in strip order. StageHazard
# scrubs `tell` and `cool` by its own clock, so their speed is unused; `idle` and the vent's `active`
# play at it.
HAZARDS = [
    ("spike-trap", "SpikeTrapFrames", CELL, spike_frame,
     [("idle", 1, 1.0, True), ("tell", 3, 1.0, False), ("active", 2, 1.0, False), ("cool", 3, 1.0, False)]),
    ("flame-vent", "FlameVentFrames", VENT_CELL, vent_frame,
     [("idle", 2, 2.5, True), ("tell", 4, 1.0, False), ("active", 4, 12.0, True), ("cool", 3, 1.0, False)]),
]


def write_hazard(name, frames_name, cell, painter, anims):
    """The strip PNG and its SpriteFrames .tres, from one table, so the two cannot disagree. The
    .tres references the texture by path and has no uid of its own (the scenes load it by path), so
    writing it fresh is safe - see `.ai/art-direction.md`, Don't, on uids."""
    from PIL import Image
    total = sum(n for _, n, _, _ in anims)
    strip = Image.new("RGBA", (cell * 2 * total, cell * 2), (0, 0, 0, 0))
    k = 0
    for anim, n, _, _ in anims:
        for i in range(n):
            c = painter(anim, i)
            bad = c.audit()
            if bad:
                print("OFF-CONTRACT COLOURS in %s %s frame %d:" % (name, anim, i))
                for h, cnt in bad[:5]:
                    print("   %s  x%d" % (h, cnt))
                return False
            strip.alpha_composite(c.scaled(2), (k * cell * 2, 0))
            k += 1
    strip.save(os.path.join(OUT_DIR, "%s.png" % name))

    subs, anim_json = [], []
    k = 0
    for anim, n, speed, loops in anims:
        refs = []
        for i in range(n):
            sid = "Atlas_%s_%d" % (anim, i)
            subs.append('[sub_resource type="AtlasTexture" id="%s"]\natlas = ExtResource("tex_0")\n'
                        'region = Rect2(%d, 0, %d, %d)\n' % (sid, k * cell * 2, cell * 2, cell * 2))
            refs.append('{"duration": 1.0, "texture": SubResource("%s")}' % sid)
            k += 1
        anim_json.append('{"frames": [%s], "loop": %s, "name": &"%s", "speed": %s}'
                         % (", ".join(refs), "true" if loops else "false", anim, repr(float(speed))))
    tres = ('[gd_resource type="SpriteFrames" load_steps=%d format=3]\n\n'
            '[ext_resource type="Texture2D" path="res://assets/bonelight/props/%s.png" id="tex_0"]\n\n'
            '%s\n[resource]\nanimations = [%s]\n'
            % (total + 2, name, "\n".join(subs), ", ".join(anim_json)))
    path = os.path.join(ROOT, "scenes", "resources", "%s.tres" % frames_name)
    with open(path, "wb") as fh:
        fh.write(tres.encode("utf-8"))
    print("  %-16s %d frames at %dpx, %s" % (name, total, cell * 2, ", ".join("%s %d" % (a, n) for a, n, _, _ in anims)))
    return True


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
    one yet" is somebody you cannot see. A locked card draws only its silhouette (CharacterVisuals.
    BuildSilhouetteFrames), so it has to read as a silhouette first and a figure second.
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


# A question mark struck into the seal below, 10 wide and 15 tall, in a two-pixel stroke so it
# survives being read at a glance on a dark card. Rows top to bottom.
_QUERY = (
    "..######..",
    ".########.",
    "###....###",
    "##......##",
    "........##",
    ".......###",
    "......###.",
    ".....###..",
    "....###...",
    "....##....",
    "....##....",
    "..........",
    "..........",
    "....##....",
    "....##....",
)


def sealed_portrait(i, n):
    """What a SECRET wizard's card shows before anything about him is known.

    UnlockCatalog can mark a wizard secret until a reveal condition is met (the chapter that holds
    him opens, for the two rescued so far). Until then his card must give away nothing - not his
    shape, not how he is found - so it cannot be a silhouette of him, and it cannot be the hooded
    unknown above either, which is a figure. It is a seal: the same pooled red wax the mutation
    card is closed with (ui_frames.wax_seal), at portrait size, with a question struck into it.
    Something is behind it; it has not been opened.

    Drawn on the 48 cell at 1:1 so the menu draws it on the same pixel grid as the wizards beside
    it (PixelArtRect picks one whole number of screen pixels per art pixel for the whole row).
    """
    c = raster.Canvas(PORTRAIT_CELL, PORTRAIT_CELL, None)
    wax = ramp("red")
    cx, cy = 23.5, 23.5
    rng = random.Random(53)
    # A pooled, not a turned, edge: the radius wanders a little round the disc.
    wobble = [rng.choice((0.0, 0.5, 1.0)) for _ in range(16)]
    for y in range(PORTRAIT_CELL):
        for x in range(PORTRAIT_CELL):
            dx, dy = x - cx, y - cy
            r = (dx * dx + dy * dy) ** 0.5
            ang = (math.atan2(dy, dx) + math.pi) / TAU
            edge = 18.0 + wobble[int(ang * 16) % 16]
            if r > edge:
                continue
            if r > edge - 1.0:
                # The rim: occlusion on the side away from the key, shade toward it.
                tone = OCC if (dx + dy) > 0 else wax[3]
            elif r > edge - 3.0:
                # The raised lip of the pool, lit toward the upper left.
                facing = -(dx + dy) / max(r, 0.01)
                tone = wax[0] if facing > 0.55 else wax[1] if facing > -0.35 else wax[2]
            elif r > edge - 4.0:
                # The step down from the lip into the pressed field.
                tone = wax[3] if (dx + dy) < 0 else wax[2]
            elif 11.4 < r <= 12.4:
                # The signet's border, a turned ring pressed into the field.
                tone = wax[3]
            elif 12.4 < r <= 13.3 and (dx + dy) > 0:
                # ...whose far wall faces the key light and catches it.
                tone = wax[1]
            else:
                tone = wax[2]
            c.set(x, y, tone)

    # Air pocketed in the wax as it was poured: a pit and its lit far edge. Placed by hand clear of
    # the question and the ring.
    for (x, y) in ((14, 27), (32, 18), (31, 29), (17, 15), (27, 33)):
        c.set(x, y, wax[3])
        c.set(x + 1, y + 1, wax[1])

    # The question, struck in: the groove is shade, and the wall of the groove that faces the key
    # light (its lower right) catches it.
    x0, y0 = 19, 16
    struck = set()
    for row, line in enumerate(_QUERY):
        for col, ch in enumerate(line):
            if ch == "#":
                struck.add((x0 + col, y0 + row))
    for (x, y) in struck:
        c.set(x, y, wax[4])
    for (x, y) in struck:
        for (nx, ny) in ((x + 1, y), (x, y + 1)):
            if (nx, ny) not in struck:
                c.set(nx, ny, wax[1])
    return c


FX = [
    ("unknown-portrait", unknown_wizard, 1, PORTRAIT_CELL),
    ("sealed-portrait", sealed_portrait, 1, PORTRAIT_CELL),
    ("chest", chest_idle, 4, CELL),
    ("chest-open", chest_open, 4, CELL),
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
        # The portraits are character art, not world props, so they land with the other characters
        # - and at 1:1, like every wizard's sheet. The menu picks the scale (PixelArtRect); baked in
        # at x2, as the props are, the unknown portrait was drawn with pixels twice the size of the
        # wizard on the card next to it.
        portrait = name.endswith("-portrait")
        factor = 1 if portrait else 2
        strip = Image.new("RGBA", (cell * factor * frames, cell * factor), (0, 0, 0, 0))
        for i in range(frames):
            c = fn(i, frames)
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s frame %d:" % (name, i))
                for h, cnt in bad[:5]:
                    print("   %s  x%d" % (h, cnt))
                break
            strip.alpha_composite(c.scaled(factor), (i * cell * factor, 0))
        where = OUT_CHARS if portrait else OUT_DIR
        if not os.path.isdir(where):
            os.makedirs(where)
        strip.save(os.path.join(where, "%s.png" % name))
        total += frames
        print("  %-16s %d frames at %dpx" % (name, frames, cell * factor))

    for name, frames_name, cell, painter, anims in HAZARDS:
        ok = write_hazard(name, frames_name, cell, painter, anims) and ok
        total += sum(n for _, n, _, _ in anims)

    if not ok:
        return 1
    print("")
    print("%d interactables, %d frames, palette clean" % (len(FX) + len(HAZARDS), total))
    return 0


if __name__ == "__main__":
    sys.exit(main())
