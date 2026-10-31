# -*- coding: utf-8 -*-
"""Spiritual Weapon's three forms and their colossal Ascensions. Run: python tools/art/spirit_weapons.py

THE REQUEST (Eric, 2026-10-28): "spiritual weapon should be able to select one of 3 weapons ... axe is
heavier but more damage, hammer is aoe that shakes ground and slows enemies, and sword is the most direct
damage to single targets ... there is a single floating weapon that targets enemies. it should animate by
raising into the air, swinging down onto the targeted enemy, and then winding up again. mutations/
ascensions can ... change the weapon into a whole new, much bigger and more detailed version of the
weapon." So: an axe, a hammer and a sword, each a weapon of light held by nothing, and for each a
colossal form on the boss cell - Executioner's Greataxe, Titan's Maul, Celestial Greatsword - drawn
again rather than scaled, with the detail a bigger cell can carry.

WHAT IS WRITTEN (assets/bonelight/effects/, authored 1:1 and saved 1:1 - SpiritualWeapon.cs draws a
cell at x2, which is the contract's render scale, art-direction.md section 1):

  spirit-axe.png         48 cell, 32 headings, TWO rows: the bit on the -v side (row 0) and on the +v
                         side (row 1), because a bit leads the swing and a weapon to the left of its
                         quarry swings the other way round. Lit for each, not flipped.
  spirit-hammer.png      48 cell, 32 headings, one row (it is symmetric).
  spirit-sword.png       48 cell, 32 headings, one row.
  spirit-greataxe.png    96 cell, 32 headings (double-bitted, so one row).
  spirit-maul.png        96 cell, 32 headings.
  spirit-greatsword.png  96 cell, 32 headings.
  spirit-dagger.png      32 cell, 32 headings, one row: Twin Blades' dagger (2026-10-30, Eric: "Twin sword
                         mutation should create a second smaller dagger"), the sword's make on the
                         projectile cell. Its grip is 8.5 art px behind the centre and its head 7.5 ahead.

Cell i points at i x 11.25 degrees, measured the way Godot measures an angle (0 = +X, clockwise on screen
since y is down). The cell count, the cell sizes and two distances are an API, read by SpiritualWeapon:
every weapon's GRIP is 13 art px behind the cell's centre on the elite cell (28 on the boss cell), and its
HEAD - the axe's bit, the hammer's face, the sword's point of percussion - 13 px ahead of it (28). Pixel
art turned by the engine smears; a weapon rasterised at its heading keeps its pixels (SleetRunes' reason).

WHAT THEY ARE MADE OF. The striking part - the blade, the bit, the head - is conjured light: Arcane's
emissive ramp, white-hot along the cutting edge (the contract: every element's core is white-hot), its
lit side a stop brighter than its shade side. The haft, the hilt and the guard are a made thing: gold,
the Light the spell carries, lit from the upper left in five tones, with grips wound in the wizard's
violet. Every weapon is ringed in occlusion and wears a faint part-alpha fringe of Arcane's edge round its
light, because it hangs in the air over enemies and floor alike and has to hold against both.

The colossal forms carry what the bigger cell allows: a double bit and a top spike on the greataxe, gems
in its collar and runes on its cheeks; banded faces, corner studs and a great rune on the maul; a winged
guard, a gem, a gilded ricasso and a fuller lettered with runes on the greatsword.

The card icons (spell_icons.py writes them) are drawn by the same renderer: icon(kind) on a disc of the
wizard's violet night, and trio() - all three crossed - for the spell's own card, which opens the page of
forms. tools/art/evolution_icons.py draws every milestone's icon from these too.

`python tools/art/spirit_weapons.py` also writes tools/art/_spirit_weapons_preview.png (gitignored): every
sheet on a floor tone, and the icons at x4.
"""
from __future__ import print_function, division

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")
PREVIEW = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_spirit_weapons_preview.png")

TAU = math.pi * 2.0
HEADINGS = 32
OCC = bl.OCC
ARC = bl.ELEMENTS["arcane"]     # core, hot, mid, edge
GOLD = bl.MATERIALS["gold"]     # hi, lit, base, shade, deep
VIOLET = bl.MATERIALS["violet"]
# Mjolnir's (2026-10-31): steel in five tones, leather from amber's three darkest, runes in Lightning's ramp.
STEEL = bl.MATERIALS["steel"]
LEATHER = bl.MATERIALS["amber"][2:5]
LIGHTNING = bl.ELEMENTS["lightning"]

# The key light in screen space: from the upper left (art-direction.md section 2).
KEY = (-1.0 / math.sqrt(2.0), -1.0 / math.sqrt(2.0))

SAMPLES = 3                     # 3x3 samples a pixel
COVER = 0.45                    # a pixel is in if this much of it is


# =============================================================================================
# Signed distances in weapon space: u along the weapon (grip to head), v across it. Negative inside.
# =============================================================================================

def sd_capsule(u, v, u0, u1, r, vc=0.0):
    t = u0 if u < u0 else u1 if u > u1 else u
    return math.hypot(u - t, v - vc) - r


def sd_vcapsule(u, v, uc, v0, v1, r):
    t = v0 if v < v0 else v1 if v > v1 else v
    return math.hypot(u - uc, v - t) - r


def sd_circle(u, v, cu, cv, r):
    return math.hypot(u - cu, v - cv) - r


def sd_box(u, v, cu, cv, hu, hv, chamfer=0.0):
    du = abs(u - cu) - hu
    dv = abs(v - cv) - hv
    d = math.hypot(max(du, 0.0), max(dv, 0.0)) + min(max(du, dv), 0.0)
    if chamfer > 0.0:
        d = max(d, (abs(u - cu) + abs(v - cv) - (hu + hv - chamfer)) / math.sqrt(2.0))
    return d


def _seg_dist(px, py, ax, ay, bx, by):
    abx, aby = bx - ax, by - ay
    l2 = abx * abx + aby * aby
    t = 0.0 if l2 <= 1e-9 else max(0.0, min(1.0, ((px - ax) * abx + (py - ay) * aby) / l2))
    return math.hypot(px - (ax + abx * t), py - (ay + aby * t))


def sd_poly(u, v, pts):
    d = 1e9
    inside = False
    n = len(pts)
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        d = min(d, _seg_dist(u, v, ax, ay, bx, by))
        if (ay > v) != (by > v):
            x = ax + (v - ay) * (bx - ax) / (by - ay)
            if u < x:
                inside = not inside
    return -d if inside else d


def polyline_dist(u, v, pts):
    return min(_seg_dist(u, v, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1]) for i in range(len(pts) - 1))


class Part(object):
    """One piece of a weapon: its shape, what it is made of, and how high it sits in the stack."""

    def __init__(self, sdf, mat, layer, box, edge=None, rune=None, depth=None):
        self.sdf = sdf            # f(u, v) -> signed distance
        self.mat = mat            # "gold", "violet", "glow", "core", "hot"
        self.layer = layer
        self.box = box            # (u0, u1, v0, v1), a cheap reject before the sdf
        self.edge = edge          # f(u, v) -> True on the cutting edge (white-hot)
        self.rune = rune          # f(u, v) -> True on a rune cut into the light (white-hot)
        # f(u, v) -> how far (art px) this point of the light is from where it strikes. A bit or a face
        # is brightest where it strikes and darkens toward the haft, which is what makes a lump of light
        # read as a cutting edge rather than a blob. None: shaded from its rim only.
        self.depth = depth

    def near(self, u, v):
        return self.box[0] - 1.0 <= u <= self.box[1] + 1.0 and self.box[2] - 1.0 <= v <= self.box[3] + 1.0


def _box_of(pts, pad=0.0):
    us = [p[0] for p in pts]
    vs = [p[1] for p in pts]
    return (min(us) - pad, max(us) + pad, min(vs) - pad, max(vs) + pad)


def _flip(pts):
    return [(u, -v) for (u, v) in pts]


def _grip(parts, bands, r, mat="violet", layer=3):
    for (a, b) in bands:
        parts.append(Part(lambda u, v, a=a, b=b: sd_capsule(u, v, a, b, r), mat, layer, (a - r, b + r, -r, r)))


# =============================================================================================
# The weapons. Base forms on the elite cell (u from -20 to +20, grip at -13, head at +13); colossal
# forms on the boss cell (u from -44 to +44, grip at -28, head at +28).
# =============================================================================================

def axe(side=-1):
    """The axe: a gold haft wound at the grip, a bearded bit of light on the `side` it leads with, a
    small back spike on the other, and a rune on the cheek. White-hot along the cutting edge."""
    s = float(side)
    parts = []
    parts.append(Part(lambda u, v: sd_capsule(u, v, -19.0, 17.5, 1.5), "gold", 1, (-20.5, 19, -1.5, 1.5)))
    parts.append(Part(lambda u, v: sd_circle(u, v, -19.4, 0.0, 2.1), "gold", 2, (-21.5, -17.3, -2.1, 2.1)))
    _grip(parts, [(-17.2, -16.0), (-14.8, -13.6), (-12.4, -11.2)], 1.85)
    bit = [(11.5, -2.4), (17.0, -2.4), (19.0, -5.5), (21.8, -10.0), (21.6, -13.2), (18.0, -15.0), (14.0, -15.8),
           (10.0, -15.2), (7.0, -13.6), (5.8, -11.6), (8.5, -9.0), (10.5, -5.5)]
    cut = [(21.6, -13.2), (18.0, -15.0), (14.0, -15.8), (10.0, -15.2), (7.0, -13.6), (5.8, -11.6)]
    if s > 0:
        bit, cut = _flip(bit), _flip(cut)
    parts.append(Part(lambda u, v: sd_poly(u, v, bit), "glow", 4, _box_of(bit),
                      edge=lambda u, v: polyline_dist(u, v, cut) < 1.45,
                      rune=lambda u, v: abs(u - 14.0) + abs(v - s * 9.6) < 1.3 and abs(u - 14.0) + abs(v - s * 9.6) > 0.45,
                      depth=lambda u, v: polyline_dist(u, v, cut)))
    spike = [(12.6, -s * 2.2), (15.6, -s * 2.2), (14.2, -s * 7.2)]
    parts.append(Part(lambda u, v: sd_poly(u, v, spike), "glow", 4, _box_of(spike)))
    parts.append(Part(lambda u, v: sd_box(u, v, 14.2, 0.0, 3.2, 2.5, 0.8), "gold", 5, (11, 17.4, -2.5, 2.5)))
    parts.append(Part(lambda u, v: sd_circle(u, v, 18.6, 0.0, 1.3), "gold", 5, (17.3, 19.9, -1.3, 1.3)))
    return parts, 22.0


def hammer():
    """The hammer: a gold haft wound at the grip, a block of light for a head with gold faces, a gold
    socket through it and a rune on each cheek, capped with a gold knob."""
    parts = []
    parts.append(Part(lambda u, v: sd_capsule(u, v, -19.0, 12.0, 1.6), "gold", 1, (-20.6, 13.6, -1.6, 1.6)))
    parts.append(Part(lambda u, v: sd_circle(u, v, -19.4, 0.0, 2.2), "gold", 2, (-21.6, -17.2, -2.2, 2.2)))
    _grip(parts, [(-17.2, -16.0), (-14.8, -13.6), (-12.4, -11.2)], 1.95)
    rune = lambda u, v: (abs(u - 14.5) < 0.6 and 2.8 < abs(v) < 6.0) or (abs(abs(v) - 4.4) < 0.6 and abs(u - 14.5) < 1.7)
    parts.append(Part(lambda u, v: sd_box(u, v, 14.5, 0.0, 5.6, 8.0, 1.8), "glow", 4, (8.9, 20.1, -8.0, 8.0),
                      edge=lambda u, v: abs(v) > 7.2, rune=rune, depth=lambda u, v: 8.0 - abs(v)))
    parts.append(Part(lambda u, v: sd_box(u, v, 14.5, 0.0, 6.0, 1.2), "gold", 5, (8.5, 20.5, -1.2, 1.2)))
    for sv in (-1.0, 1.0):
        parts.append(Part(lambda u, v, sv=sv: sd_box(u, v, 14.5, sv * 8.4, 5.0, 0.9), "gold", 5, (9.5, 19.5, sv * 8.4 - 0.9, sv * 8.4 + 0.9)))
    parts.append(Part(lambda u, v: sd_circle(u, v, 20.9, 0.0, 1.7), "gold", 6, (19.2, 22.6, -1.7, 1.7)))
    return parts, 22.0


def sword():
    """The sword: a gold pommel and guard, a violet grip bound in gold wire, and a blade of light with a
    white-hot fuller running most of its length."""
    parts = []
    parts.append(Part(lambda u, v: sd_circle(u, v, -18.4, 0.0, 2.3), "gold", 2, (-20.7, -16.1, -2.3, 2.3),
                      rune=lambda u, v: math.hypot(u + 18.4, v) < 0.75))
    parts.append(Part(lambda u, v: sd_capsule(u, v, -16.4, -10.8, 1.45), "violet", 1, (-17.9, -9.3, -1.45, 1.45)))
    _grip(parts, [(-15.0, -14.6), (-12.6, -12.2)], 1.6, mat="gold", layer=3)
    parts.append(Part(lambda u, v: sd_vcapsule(u, v, -9.8, -5.8, 5.8, 1.15), "gold", 4, (-11, -8.6, -7, 7)))
    for sv in (-1.0, 1.0):
        parts.append(Part(lambda u, v, sv=sv: sd_circle(u, v, -9.0, sv * 6.2, 1.25), "gold", 4, (-10.3, -7.7, sv * 6.2 - 1.3, sv * 6.2 + 1.3)))
    blade = [(-8.8, -2.6), (12.0, -2.35), (20.5, 0.0), (12.0, 2.35), (-8.8, 2.6)]
    parts.append(Part(lambda u, v: sd_poly(u, v, blade), "glow", 3, _box_of(blade),
                      edge=lambda u, v: -7.5 < u < 13.0 and abs(v) < 0.55))
    return parts, 22.0


def dagger():
    """Twin Blades' dagger, on the projectile cell (u from -13 to +13, grip at -8.5, head at +7.5): the
    sword's make, smaller - a gold pommel, a violet grip with one gold band, a short straight gold guard,
    and a leaf blade of light with a white-hot fuller."""
    parts = []
    parts.append(Part(lambda u, v: sd_circle(u, v, -11.5, 0.0, 1.4), "gold", 2, (-12.9, -10.1, -1.4, 1.4)))
    parts.append(Part(lambda u, v: sd_capsule(u, v, -10.2, -7.0, 1.2), "violet", 1, (-11.4, -5.8, -1.2, 1.2)))
    _grip(parts, [(-8.8, -8.4)], 1.35, mat="gold", layer=3)
    parts.append(Part(lambda u, v: sd_vcapsule(u, v, -6.2, -3.6, 3.6, 0.95), "gold", 4, (-7.2, -5.2, -4.6, 4.6)))
    blade = [(-5.4, -1.8), (5.0, -1.9), (12.8, 0.0), (5.0, 1.9), (-5.4, 1.8)]
    parts.append(Part(lambda u, v: sd_poly(u, v, blade), "glow", 3, _box_of(blade),
                      edge=lambda u, v: -4.5 < u < 8.5 and abs(v) < 0.5))
    return parts, 14.5


def greataxe():
    """Executioner's Greataxe: double-bitted, white-hot along both edges, a jewelled gold collar, a rune
    on each cheek, a spike of light on top and one at the butt, the haft banded and wound."""
    parts = []
    parts.append(Part(lambda u, v: sd_capsule(u, v, -41.0, 36.0, 2.6), "gold", 1, (-43.6, 38.6, -2.6, 2.6)))
    butt = [(-41.0, -2.6), (-41.0, 2.6), (-46.0, 0.0)]
    parts.append(Part(lambda u, v: sd_poly(u, v, butt), "glow", 2, _box_of(butt)))
    parts.append(Part(lambda u, v: sd_circle(u, v, -40.6, 0.0, 3.4), "gold", 3, (-44, -37.2, -3.4, 3.4)))
    _grip(parts, [(-37.0, -34.6), (-32.4, -30.0), (-27.8, -25.4)], 3.1)
    for (a, b) in ((-22.6, -21.6), (9.0, 10.0)):
        parts.append(Part(lambda u, v, a=a, b=b: sd_capsule(u, v, a, b, 3.2), "gold", 3, (a - 3.2, b + 3.2, -3.2, 3.2)))
    bit = [(20.5, -4.0), (33.5, -4.0), (37.5, -10.0), (42.0, -20.0), (41.5, -27.0), (35.0, -30.5), (27.0, -31.8),
           (19.0, -30.5), (12.5, -27.0), (11.5, -20.0), (16.5, -10.0)]
    cut = [(41.5, -27.0), (35.0, -30.5), (27.0, -31.8), (19.0, -30.5), (12.5, -27.0)]
    for flip in (False, True):
        b = _flip(bit) if flip else bit
        c = _flip(cut) if flip else cut
        sv = 1.0 if flip else -1.0

        def rune(u, v, sv=sv):
            r = math.hypot(u - 27.0, v - sv * 17.5)
            return 2.2 < r < 3.3 or r < 0.9 or (abs(u - 27.0) < 0.55 and 3.3 < abs(v - sv * 17.5) < 6.5)
        parts.append(Part(lambda u, v, b=b: sd_poly(u, v, b), "glow", 4, _box_of(b),
                          edge=lambda u, v, c=c: polyline_dist(u, v, c) < 2.2, rune=rune,
                          depth=lambda u, v, c=c: polyline_dist(u, v, c) * 0.5))
    top = [(34.0, -2.4), (34.0, 2.4), (46.5, 0.0)]
    parts.append(Part(lambda u, v: sd_poly(u, v, top), "glow", 4, _box_of(top), edge=lambda u, v: abs(v) < 0.6 and u > 38.0))
    parts.append(Part(lambda u, v: sd_box(u, v, 27.0, 0.0, 6.0, 4.6, 1.6), "gold", 5, (21, 33, -4.6, 4.6),
                      rune=lambda u, v: math.hypot(u - 27.0, v) < 1.9))
    parts.append(Part(lambda u, v: sd_capsule(u, v, 33.4, 35.2, 2.9), "gold", 5, (30.5, 38.1, -2.9, 2.9)))
    return parts, 47.0


def maul():
    """Titan's Maul: a great block of light banded in gold, its faces plated, studded at the corners,
    a great rune on each cheek, capped with a spike; the haft banded and wound."""
    parts = []
    parts.append(Part(lambda u, v: sd_capsule(u, v, -41.0, 18.0, 2.8), "gold", 1, (-43.8, 20.8, -2.8, 2.8)))
    parts.append(Part(lambda u, v: sd_circle(u, v, -41.0, 0.0, 3.6), "gold", 2, (-44.6, -37.4, -3.6, 3.6)))
    _grip(parts, [(-37.0, -34.6), (-32.4, -30.0), (-27.8, -25.4)], 3.3)
    parts.append(Part(lambda u, v: sd_capsule(u, v, -22.6, -21.4, 3.4), "gold", 3, (-26, -18, -3.4, 3.4)))

    def rune(u, v):
        av = abs(v)
        r = math.hypot(u - 29.5, av - 9.5)
        return (2.6 < r < 3.7) or r < 1.0 or (abs(u - 29.5) < 0.6 and 3.7 < abs(av - 9.5) < 6.0) \
            or (abs(av - 9.5) < 0.6 and 3.7 < abs(u - 29.5) < 6.0)
    parts.append(Part(lambda u, v: sd_box(u, v, 29.5, 0.0, 11.0, 17.0, 4.0), "glow", 4, (18.5, 40.5, -17, 17),
                      edge=lambda u, v: abs(v) > 15.6, rune=rune, depth=lambda u, v: (17.0 - abs(v)) * 0.5))
    parts.append(Part(lambda u, v: sd_box(u, v, 29.5, 0.0, 11.6, 2.2), "gold", 5, (17.9, 41.1, -2.2, 2.2)))
    for sv in (-1.0, 1.0):
        parts.append(Part(lambda u, v, sv=sv: sd_box(u, v, 29.5, sv * 17.4, 9.6, 1.5), "gold", 5, (19.9, 39.1, sv * 17.4 - 1.5, sv * 17.4 + 1.5)))
        for cu in (21.0, 38.0):
            parts.append(Part(lambda u, v, sv=sv, cu=cu: sd_circle(u, v, cu, sv * 13.0, 1.7), "gold", 6, (cu - 1.7, cu + 1.7, sv * 13 - 1.7, sv * 13 + 1.7)))
    spike = [(40.5, -2.6), (40.5, 2.6), (46.5, 0.0)]
    parts.append(Part(lambda u, v: sd_poly(u, v, spike), "glow", 6, _box_of(spike)))
    parts.append(Part(lambda u, v: sd_circle(u, v, 41.0, 0.0, 2.6), "gold", 7, (38.4, 43.6, -2.6, 2.6)))
    return parts, 47.0


def mjolnir():
    """Mjolnir, the hammer's Ascension since 2026-10-31 (Eric: "the sprite should be entirely different and
    more detailed, so it doesn't look like just a giant version of the smaller hammer", and a Mjolnir
    reference for a Lightning crossover). Not the spirit hammer grown: a made thing of steel, not of
    conjured light. A broad, deep block of a head - wider across than the haft is long, its corners cut -
    plated at both striking faces, grooved a hand in from each, and rune-cut down its cheek in Lightning's
    ramp: a ring with a bolt through it and a rune stroke either side. Under it a short, thick haft wound
    in leather the whole way, an iron pommel, and the wrist loop hanging off the pommel. The grip and the
    head keep the boss cell's points (28 behind and ahead of the centre), which SpiritualWeapon reads."""
    parts = []
    # The haft: short and thick, leather wound over steel; only its last stretch shows under the head.
    parts.append(Part(lambda u, v: sd_capsule(u, v, -36.0, 11.0, 3.6), "steel", 1, (-39.6, 14.6, -3.6, 3.6)))
    wraps = [(-33.5 + 4.2 * i, -31.3 + 4.2 * i) for i in range(9)]
    _grip(parts, wraps, 4.1, mat="leather", layer=2)
    parts.append(Part(lambda u, v: sd_capsule(u, v, 4.4, 5.8, 4.4), "steel", 3, (0.0, 10.2, -4.4, 4.4)))
    # The pommel, and the loop of cord through it.
    parts.append(Part(lambda u, v: sd_box(u, v, -37.6, 0.0, 2.6, 4.8, 1.4), "steel", 3, (-40.2, -35.0, -4.8, 4.8)))
    loop = lambda u, v: abs(math.hypot(u + 42.0, v - 2.5) - 3.4) - 1.1
    parts.append(Part(loop, "leather", 2, (-46.6, -37.4, -2.1, 7.1)))

    # The head: u 9 to 43, v -22 to 22, its corners cut.
    def rune(u, v):
        r = math.hypot(u - 26.0, v)
        if 5.6 < r < 7.0:
            return True
        # A bolt through the ring: a zigzag of three strokes, one art px wide.
        bolt = [(17.0, -2.4), (24.6, 2.0), (27.4, -2.0), (35.0, 2.4)]
        if r < 8.4 and polyline_dist(u, v, bolt) < 0.7:
            return True
        # A rune stroke either side: a stave with two short twigs (the old letters on its cheek).
        for sv in (-1.0, 1.0):
            cv = sv * 12.4
            if abs(v - cv) < 0.65 and 15.0 < u < 37.0:
                return True
            if abs((u - 31.0) - (v - cv) * 1.2 * sv) < 0.75 and 31.0 < u < 35.0:
                return True
            if abs((u - 18.0) + (v - cv) * 1.2 * sv) < 0.75 and 18.0 < u < 22.0:
                return True
        return False
    parts.append(Part(lambda u, v: sd_box(u, v, 26.0, 0.0, 17.0, 22.0, 5.0), "steel", 4, (9.0, 43.0, -22.0, 22.0),
                      rune=rune, depth=lambda u, v: 22.0 - abs(v)))
    # The striking faces: plates a stop brighter, with a rim of lightning where the strike lands.
    for sv in (-1.0, 1.0):
        parts.append(Part(lambda u, v, sv=sv: sd_box(u, v, 26.0, sv * 20.2, 15.4, 1.9, 1.6), "plate", 5,
                          (10.6, 41.4, sv * 20.2 - 1.9, sv * 20.2 + 1.9),
                          edge=lambda u, v, sv=sv: abs(v - sv * 22.0) < 0.9))
        # The groove a hand in from each face.
        parts.append(Part(lambda u, v, sv=sv: sd_box(u, v, 26.0, sv * 16.8, 14.6, 0.55), "groove", 5,
                          (11.4, 40.6, sv * 16.8 - 0.6, sv * 16.8 + 0.6)))
    # Where the haft goes through the head: a steel boss on its lower edge.
    parts.append(Part(lambda u, v: sd_box(u, v, 9.4, 0.0, 2.2, 6.0, 1.0), "steel", 6, (7.2, 11.6, -6.0, 6.0)))
    return parts, 47.5


def greatsword():
    """Celestial Greatsword: a jewelled pommel, a violet grip bound in gold wire, a winged guard with a
    gem, a gilded ricasso, and a long blade of light lettered with runes along its fuller."""
    parts = []
    parts.append(Part(lambda u, v: sd_circle(u, v, -40.0, 0.0, 4.2), "gold", 2, (-44.2, -35.8, -4.2, 4.2),
                      rune=lambda u, v: math.hypot(u + 40.0, v) < 1.6))
    parts.append(Part(lambda u, v: sd_capsule(u, v, -36.4, -24.8, 2.6), "violet", 1, (-39, -22.2, -2.6, 2.6)))
    _grip(parts, [(-34.0, -33.2), (-31.0, -30.2), (-28.0, -27.2)], 2.8, mat="gold", layer=3)
    parts.append(Part(lambda u, v: sd_vcapsule(u, v, -22.2, -12.5, 12.5, 2.2), "gold", 4, (-24.4, -20, -14.7, 14.7)))
    for sv in (-1.0, 1.0):
        wing = [(-23.5, sv * 11.5), (-19.0, sv * 12.5), (-15.5, sv * 16.5), (-19.5, sv * 16.0), (-24.0, sv * 14.5)]
        parts.append(Part(lambda u, v, w=wing: sd_poly(u, v, w), "gold", 4, _box_of(wing)))
    parts.append(Part(lambda u, v: sd_box(u, v, -22.0, 0.0, 2.8, 4.4, 1.2), "gold", 5, (-24.8, -19.2, -4.4, 4.4),
                      rune=lambda u, v: math.hypot(u + 22.0, v) < 1.9))
    blade = [(-19.6, -6.2), (24.0, -5.6), (43.0, 0.0), (24.0, 5.6), (-19.6, 6.2)]

    def runes(u, v):
        if abs(v) > 1.6:
            return False
        for c in (-9.0, -1.5, 6.0, 13.5, 21.0):
            if abs(u - c) < 0.6 and abs(v) < 1.6:
                return True
            if abs(u - c - 2.0) < 0.6 and abs(v) < 0.6:
                return True
        return False
    parts.append(Part(lambda u, v: sd_poly(u, v, blade), "glow", 3, _box_of(blade),
                      edge=lambda u, v: -14.0 < u < 30.0 and abs(v) < 0.6 and not runes(u, v), rune=runes))
    parts.append(Part(lambda u, v: sd_box(u, v, -17.2, 0.0, 2.4, 6.4), "gold", 5, (-19.6, -14.8, -6.4, 6.4)))
    return parts, 47.0


MODELS = {
    "axe": lambda mirror: axe(side=1 if mirror else -1),
    "hammer": lambda mirror: hammer(),
    "sword": lambda mirror: sword(),
    "greataxe": lambda mirror: greataxe(),
    # The hammer's Ascension is Mjolnir since 2026-10-31; the sheet keeps its name (spirit-maul.png), which
    # SpiritualWeapon loads. maul() above is the old giant hammer, kept for the record of what it replaced.
    "maul": lambda mirror: mjolnir(),
    "greatsword": lambda mirror: greatsword(),
    "dagger": lambda mirror: dagger(),
}

CELLS = {"axe": 48, "hammer": 48, "sword": 48, "greataxe": 96, "maul": 96, "greatsword": 96, "dagger": 32}
ROWS = {"axe": 2}
# How far behind the cell's centre the grip is, in art px, where it is not the elite or boss cell's.
GRIP_BACK = {"dagger": 8.5}


# =============================================================================================
# The renderer: supersampled coverage, the top part wins, shaded at the pixel's centre.
# =============================================================================================

def _top(parts, u, v):
    best = None
    for p in parts:
        if (best is None or p.layer >= best.layer) and p.near(u, v) and p.sdf(u, v) < 0.0:
            best = p
    return best


def _shade(part, d, facing, u, v, big):
    # Mjolnir's materials: steel and leather lit from the upper left, its runes and its faces' rims in
    # Lightning's ramp rather than Arcane's (it is the thunder-god's, not a weapon of the spell's light).
    if part.mat in ("steel", "plate", "groove", "leather"):
        if part.rune is not None and part.rune(u, v):
            return LIGHTNING[0] if facing > -0.2 else LIGHTNING[1]
        if part.edge is not None and part.edge(u, v):
            return LIGHTNING[1] if facing > -0.4 else LIGHTNING[2]
        if part.mat == "groove":
            return STEEL[4]
        if part.mat == "leather":
            # The wrap's turns: the top of each lit, its underside in shadow.
            return LEATHER[0] if facing > 0.7 else LEATHER[1] if facing > -0.45 else LEATHER[2]
        if d < 1.0:
            i = 0 if facing > 0.6 else 1 if facing > 0.2 else 2 if facing > -0.35 else 3 if facing > -0.75 else 4
            return STEEL[max(0, i - 1)] if part.mat == "plate" else STEEL[i]
        i = 1 if facing > 0.35 else 2 if facing > -0.4 else 3
        if part.mat == "plate":
            i = max(0, i - 1)
        # The cheek darkens into the middle of the block, so it reads as a mass and not a plate.
        if part.depth is not None and part.depth(u, v) > 13.0 and i < 3:
            i += 1
        return STEEL[i]
    if part.rune is not None and part.rune(u, v):
        return ARC[0]
    if part.mat in ("gold", "violet"):
        row = GOLD if part.mat == "gold" else VIOLET
        if d < 1.0:
            i = 0 if facing > 0.6 else 1 if facing > 0.2 else 2 if facing > -0.35 else 3 if facing > -0.75 else 4
        else:
            i = 1 if facing > 0.35 else 2 if facing > -0.4 else 3
        return row[i]
    if part.mat == "core":
        return ARC[0]
    if part.mat == "hot":
        return ARC[1]
    # Glow: the conjured light. White-hot along the cutting edge; then from the rim in, edge, mid, hot,
    # each a stop brighter on the side the key light faces.
    if part.edge is not None and part.edge(u, v):
        return ARC[0] if facing > -0.55 else ARC[1]
    lit = facing > 0.25
    if d < 1.0:
        return ARC[2] if lit else ARC[3]
    if part.depth is not None:
        # Brightest where it strikes, darkening toward the haft: hot, then mid, then the deep edge tone.
        k = part.depth(u, v)
        if k < 3.2:
            return ARC[1]
        if k < 6.0:
            return ARC[1] if lit else ARC[2]
        return ARC[2] if lit else ARC[3]
    if d < (2.4 if big else 2.2):
        return ARC[1] if lit else ARC[2]
    if big and 4.0 <= d < 5.0 and facing < -0.2:
        return ARC[2]
    return ARC[1]


def render(kind, angle_deg, size, scale=1.0, centre=None, mirror=False, aura=True):
    """One weapon, its head pointing along `angle_deg` (Godot's convention), on a size x size canvas."""
    parts, reach = MODELS[kind](mirror)
    big = CELLS[kind] > 48
    c = raster.Canvas(size, size, None)
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    cx, cy = centre if centre else (size / 2.0, size / 2.0)
    limit = reach * scale + 1.5
    filled = {}
    glow = set()
    S = SAMPLES
    for py in range(size):
        for px in range(size):
            x0 = px + 0.5 - cx
            y0 = py + 0.5 - cy
            if math.hypot(x0, y0) > limit:
                continue
            votes = {}
            inside = 0
            for sy in range(S):
                for sx in range(S):
                    x = px + (sx + 0.5) / S - cx
                    y = py + (sy + 0.5) / S - cy
                    u = (x * ca + y * sa) / scale
                    v = (-x * sa + y * ca) / scale
                    p = _top(parts, u, v)
                    if p is None:
                        continue
                    inside += 1
                    votes[p] = votes.get(p, 0) + 1
            if inside < COVER * S * S:
                continue
            # The higher part wins a near-tie, so a thin gold band is not eaten by the light under it.
            part = max(votes.items(), key=lambda kv: (kv[1] * (1.0 + 0.6 * kv[0].layer), kv[0].layer))[0]
            u = (x0 * ca + y0 * sa) / scale
            v = (-x0 * sa + y0 * ca) / scale
            d = -part.sdf(u, v) * scale
            e = 0.35
            gu = part.sdf(u + e, v) - part.sdf(u - e, v)
            gv = part.sdf(u, v + e) - part.sdf(u, v - e)
            nx = gu * ca - gv * sa
            ny = gu * sa + gv * ca
            n = math.hypot(nx, ny)
            facing = 0.0 if n < 1e-6 else (nx * KEY[0] + ny * KEY[1]) / n
            filled[(px, py)] = _shade(part, max(0.0, d), facing, u, v, big)
            if part.mat not in ("gold", "violet"):
                glow.add((px, py))

    # A ring of occlusion round everything, so it holds on any floor and against any body.
    occ = set()
    for (px, py) in filled:
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                q = (px + dx, py + dy)
                if q not in filled:
                    occ.add(q)
    # And past it, round the light only, a faint fringe of Arcane's edge: a weapon of light, not of metal.
    fringe = set()
    if aura:
        for (px, py) in glow:
            for dy in (-2, -1, 0, 1, 2):
                for dx in (-2, -1, 0, 1, 2):
                    q = (px + dx, py + dy)
                    if q not in filled and q not in occ:
                        fringe.add(q)
    edge = raster.rgb(ARC[3])
    for q in fringe:
        c.set(q[0], q[1], (edge[0], edge[1], edge[2], 96))
    for q in occ:
        c.set(q[0], q[1], OCC)
    for (px, py), colour in filled.items():
        c.set(px, py, colour)
    return c


def sheet(kind):
    cell = CELLS[kind]
    rows = ROWS.get(kind, 1)
    out = raster.Canvas(cell * HEADINGS, cell * rows, None)
    for r in range(rows):
        for i in range(HEADINGS):
            k = render(kind, i * 360.0 / HEADINGS, cell, mirror=(r == 1))
            for y in range(cell):
                for x in range(cell):
                    p = k.get(x, y)
                    if p[3] > 0:
                        out.set(i * cell + x, r * cell + y, p)
    return out


# =============================================================================================
# Icons, 32 x 32 (the UI cell), for spell_icons.py and evolution_icons.py
# =============================================================================================

ICON = 32
# How big each weapon is drawn on a card: the elite-cell forms nearly fill it on the diagonal, the
# colossal ones are drawn at the same length, so they are visibly the same weapon made heavier.
ICON_SCALE = {"axe": 0.86, "hammer": 0.86, "sword": 0.86, "greataxe": 0.42, "maul": 0.42, "greatsword": 0.42,
              "dagger": 0.86}


def night(c, r=14.5):
    """The wizard's violet night under a card's weapon: a light weapon on the cream page needs a ground."""
    c.disc(15.5, 15.5, r + 0.5, r + 0.5, VIOLET[2])
    c.disc(15.5, 15.5, r - 0.5, r - 0.5, VIOLET[4])


def _over(c, k):
    for y in range(k.h):
        for x in range(k.w):
            p = k.get(x, y)
            if p[3] > 0:
                c.set(x, y, p)


def _bbox(k):
    xs, ys = [], []
    for y in range(k.h):
        for x in range(k.w):
            if k.get(x, y)[3] > 0:
                xs.append(x)
                ys.append(y)
    return (min(xs), min(ys), max(xs), max(ys)) if xs else (0, 0, k.w - 1, k.h - 1)


def at_grip(kind, angle, scale, grip):
    """The centre a weapon must be drawn at for its grip to land on `grip`."""
    back = GRIP_BACK.get(kind, 28.0 if CELLS[kind] > 48 else 13.0)
    a = math.radians(angle)
    return (grip[0] + math.cos(a) * back * scale, grip[1] + math.sin(a) * back * scale)


def group(layers, box=29.0, size=ICON, aura=True):
    """Weapons laid together and fitted to the card: `layers` is [(kind, angle, (gx, gy) grip, scale)]
    in a working space; the whole group is scaled to fit `box` and centred, then drawn back to front."""
    def draw(k_scale, shift, canvas):
        # The working space's (16, 16) is the canvas's centre, so a trial on a big canvas never clips.
        mid = canvas / 2.0
        out = raster.Canvas(canvas, canvas, None)
        for kind, angle, grip, s in layers:
            g = (mid + (grip[0] - 16.0) * k_scale + shift[0], mid + (grip[1] - 16.0) * k_scale + shift[1])
            sc = s * k_scale
            _over(out, render(kind, angle, canvas, scale=sc, centre=at_grip(kind, angle, sc, g), aura=aura))
        return out
    big = size * 4
    x0, y0, x1, y1 = _bbox(draw(1.0, (0.0, 0.0), big))
    k = box / float(max(x1 - x0 + 1, y1 - y0 + 1))
    x0, y0, x1, y1 = _bbox(draw(k, (0.0, 0.0), big))
    off = big / 2.0 - size / 2.0
    shift = (round(size / 2.0 - 0.5 - ((x0 + x1) / 2.0 - off)), round(size / 2.0 - 0.5 - ((y0 + y1) / 2.0 - off)))
    return draw(k, shift, size)


def icon(kind, sky=True, angle=-50.0, aura=True):
    """One weapon on a card, fitted to it: pointing up and to the right, the way it hangs before it
    comes down."""
    c = raster.Canvas(ICON, ICON, None)
    if sky:
        night(c)
    _over(c, group([(kind, angle, (16.0, 16.0), ICON_SCALE[kind])], aura=aura))
    return c


def trio(sky=True):
    """Spiritual Weapon's own card - the card that opens the page of forms: all three, crossed, their
    grips together at the foot, the hammer upright behind, the axe and the sword leaning out."""
    c = raster.Canvas(ICON, ICON, None)
    if sky:
        night(c)
    grip = (16.0, 28.0)
    _over(c, group([("hammer", -90.0, grip, 0.6), ("axe", -122.0, grip, 0.6), ("sword", -58.0, grip, 0.6)]))
    return c


def axe_icon(sky=True):
    return icon("axe", sky)


def hammer_icon(sky=True):
    return icon("hammer", sky)


def sword_icon(sky=True):
    return icon("sword", sky)


# =============================================================================================

def write_preview(sheets, icons):
    from PIL import Image
    zoom = 2
    pad = 8
    w = max(s.w for s in sheets.values()) * zoom + pad * 2
    h = sum(s.h * zoom + pad for s in sheets.values()) + pad + ICON * 4 + pad
    img = Image.new("RGBA", (w, h), raster.rgb(bl.tone("stone", "base")))
    y = pad
    for name, s in sheets.items():
        img.alpha_composite(s.img.resize((s.w * zoom, s.h * zoom), Image.NEAREST), (pad, y))
        y += s.h * zoom + pad
    x = pad
    page = Image.new("RGBA", (ICON * 4, ICON * 4), (242, 220, 196, 255))
    for k in icons:
        img.paste(page, (x, y))
        img.alpha_composite(k.img.resize((ICON * 4, ICON * 4), Image.NEAREST), (x, y))
        x += ICON * 4 + pad
    img.convert("RGB").save(PREVIEW)


def main(argv):
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    only = None
    for a in argv:
        if a.startswith("--only="):
            only = set(a[len("--only="):].split(","))
    ok = True
    sheets = {}
    for kind in ("axe", "hammer", "sword", "greataxe", "maul", "greatsword", "dagger"):
        if only and kind not in only:
            continue
        s = sheet(kind)
        bad = s.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in spirit-%s:" % kind)
            for hx, n in bad[:8]:
                print("   %s  x%d" % (hx, n))
            continue
        path = os.path.join(OUT_DIR, "spirit-%s.png" % kind)
        s.save(path)
        sheets[kind] = s
        print("  %-34s %dx%d (%d headings x %d)" % ("spirit-%s" % kind, s.w, s.h, HEADINGS, ROWS.get(kind, 1)))
    icons = [axe_icon(), hammer_icon(), sword_icon(), trio(), icon("greataxe"), icon("maul"), icon("greatsword")]
    for k in icons:
        bad = k.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in an icon: %s" % ", ".join(hx for hx, _ in bad[:6]))
    if "--no-preview" not in argv and sheets:
        write_preview(sheets, icons)
        print("preview: %s" % os.path.relpath(PREVIEW, ROOT))
    if not ok:
        return 1
    print("palette clean")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
