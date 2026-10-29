# -*- coding: utf-8 -*-
"""Every evolution's own icon. Run: python tools/art/evolution_icons.py [--only=id,spell,...] [--no-sheet]

THE REQUEST (Eric, 2026-10-28): "mutations and ascensions should all have their own unique altered
icons. for example, rimehound should not just be a blue version of the icon, it should be a similar
dog icon with an icy background and some icicles added to the screen, etc". Until this generator
every level 4 mutation, level 8 Ascension and Apotheosis form was shown with its SPELL's icon, tinted
by the card's ModulateColor - Rimehound was the Familiar in blue, and Titan Cataclysm the same picture
as Amplified Impact. Now every evolution id has a picture of its own at
assets/bonelight/ui/evolutions/<evolution id>.png, which scripts/EvolutionIcons.cs loads wherever an
evolution is shown, and RegressionChecks.ValidateEvolutionIcons reports an id without one.

WHAT AN EVOLUTION ICON IS. The spell's own subject - drawn by the SAME painter as the spell's icon
(spell_icons.py, spell_icons_core.py, familiar.py), never copied out of a PNG, so the player still
knows the spell - altered to show what the card does. It is built in layers:

  1. THE MILESTONE'S FRAME. A level 4 mutation is a disc of its element's night with a rim of that
     element's light. A level 8 Ascension is the same disc with the night's own grain in it, in a gold
     ring with four gems of the element: the bigger thing it is, at a glance and at 24 pixels. An
     Apotheosis is a smaller disc in a gold ring wearing a crown, on a burst of rays in its element
     that fills the corners - the grandest object in the set.
  2. THE BACKDROP'S ELEMENT: the element the card GAINS, else the spell's first. Rimehound's night is
     ice; Hellbreath's is darkness; an untagged card's is its spell's own.
  3. VERBS UNDER THE SUBJECT: speed lines and afterimages for a faster card, circling arrows for a
     shorter cooldown on a spell that does not travel, a vortex for a pull, a pool for a lingering
     zone, impact rays for a bigger one, a shockwave for a colossal one.
  4. THE SUBJECT, PLACED (Place): grown for Area, colossal for a Titan, a volley of copies for more
     projectiles, turned round a centre for orbiting blades, widened for a wider cone, re-lit in a new
     element's ramp for an element path (recolour() - the same ramp remap the game does to the spell:
     Hellbreath's black flame, Rimelight's cold ward), or replaced by a hand-drawn form for a card that
     changes what the thing IS (Cerberus's three heads, the Elder God's maw, the Lattice's web).
  5. OVER IT: the gained element's accent at the rim - icicles hanging from the top for Ice, flame
     tongues for Fire, a wave for Water, sparks for Lightning, drips for Poison, a vine for Grass, rocks
     for Earth, tendrils for Darkness, glints for Light, motes for Arcane, rivets for Metal, gusts for
     Wind - and the card's verb: a heart for lifesteal, roots for a root, a crit's glint, a blast at the
     tip, a fork, chevrons for knockback, ice shards for a freeze.

THE SPEC TABLE. derive() builds every card's spec FROM ITS DATA - the element it gains, its stat
multipliers, its effect - so a card added to the catalog gets a sensible icon the first time this is
run. OVERRIDES then hand-writes the cards whose verb the numbers cannot say; today that is nearly all
of them, because two untagged cards of one spell can carry the same kinds of number. The data is
tools/art/evolution_catalog.json: the catalog as the game builds it, written by
`"$GODOT_BIN" --headless --path . scenes/_AscensionAudit.tscn -- --no-save-writes --json=tools/art/evolution_catalog.json`,
because ids like `<spell>_titan_cataclysm` are generated in C# and only C# can list them. When a card is
added or renamed: re-run that, then this, then import (`--import`).

CHECKS, before anything is written: every pixel is a contract colour (raster.Canvas.audit, every tone
from bonelight.py); every icon differs from its spell's icon, and from every sibling of the same
milestone, in at least MIN_DIFFERENCE of the pixels either covers; and every spell in the catalog has
an entry in SPELLS. The contact sheet (tools/art/_evolution_icons_sheet.png, gitignored) lays each
spell's icon beside all of its evolutions, each on the ground the game shows it on (the new spell's
page, the mutation's teal roundel, the obsidian plate, the ember plate).

Authored 32x32 and written at x2 (64x64), the UI icon cell of .ai/art-direction.md section 1.
"""
from __future__ import print_function, division

import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bonelight as bl
import raster
import familiar
import spell_icons as si
import obsidian_glass as glass
import spell_icons_core as sc
import spore_mushrooms as spores
import spirit_weapons as sw

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CATALOG = os.path.join(HERE, "evolution_catalog.json")
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "evolutions")
SHEET = os.path.join(HERE, "_evolution_icons_sheet.png")

CELL = 32
C0 = (CELL - 1) / 2.0
OCC = bl.OCC
M = bl.MATERIALS
E = bl.ELEMENTS
TAU = math.pi * 2.0
GOLD = M["gold"]

# The share of the pixels either icon covers that two icons must disagree on: an evolution against
# its spell's icon, and against each sibling of the same milestone. A card that only recoloured its
# spell measured well under this, and that was the complaint.
MIN_DIFFERENCE = 0.30

_Real = raster.Canvas


# =============================================================================================
# Placing a subject: the spell's own painter, redrawn through a map
# =============================================================================================

class Place(object):
    """Where and how a subject is drawn: scaled (sy for a different height) and turned (degrees)
    about its own centre, moved by (dx, dy) - or put at `at` - mirrored, and darkened a stop or two
    for a copy standing behind."""

    def __init__(self, s=1.0, sy=None, rot=0.0, dx=0.0, dy=0.0, at=None, mirror=False, darken=0):
        self.sx = s
        self.sy = s if sy is None else sy
        self.rot = rot
        self.dx = dx
        self.dy = dy
        self.at = at
        self.mirror = mirror
        self.darken = darken

    def mapper(self, anchor):
        ax, ay = anchor
        ca, sa = math.cos(math.radians(self.rot)), math.sin(math.radians(self.rot))
        tx, ty = self.at if self.at else (ax + self.dx, ay + self.dy)
        sx, sy, mirror = self.sx, self.sy, self.mirror

        def f(x, y):
            if mirror:
                x = 2 * ax - x
            u, v = (x - ax) * sx, (y - ay) * sy
            return (tx + u * ca - v * sa, ty + u * sa + v * ca)
        return f


class XCanvas(_Real):
    """A Canvas that draws every primitive through a Place.

    The painters build their pictures out of primitives - discs, polygons, lines, single pixels - so
    redrawing them through a map RE-RASTERISES the shapes at the new size: a disc grown 1.3 times is a
    crisp bigger disc on the same one-pixel grid, never a resampled or doubled one, which keeps the
    grid rule of art-direction.md section 1. Pixel-level primitives (set, and line, rect and blit,
    which all come through set) fill the block their source pixel maps to when growing, so a wall of
    stakes drawn pixel by pixel grows without gaps; when shrinking the last write wins, which for a
    painter is its top layer. Turned, a line is redrawn between its mapped ends and a rect becomes a
    polygon, so nothing turns into a dotted line. poly_shade keeps its light angle unmapped, so the key
    stays upper left on a mirrored or turned subject.
    """

    def __init__(self, w, h, bg=None, place=None, anchor=(C0, C0)):
        _Real.__init__(self, w, h, bg)
        self.place = place or Place()
        self.m = self.place.mapper(anchor)
        self.raw = False
        self.turned = abs(self.place.rot) > 0.01

    def set(self, x, y, c):
        if c is None:
            return
        if self.raw:
            return _Real.set(self, int(x), int(y), c)
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return
        sx, sy = int(x), int(y)
        p = self.place
        if self.turned:
            X, Y = self.m(sx, sy)
            n = max(1, int(round(min(p.sx, p.sy))))
            x0 = int(math.floor(X + 0.5 - (n - 1) / 2.0))
            y0 = int(math.floor(Y + 0.5 - (n - 1) / 2.0))
            for yy in range(y0, y0 + n):
                for xx in range(x0, x0 + n):
                    _Real.set(self, xx, yy, c)
            return
        if p.sx >= 1.0 and p.sy >= 1.0:
            (xa, ya), (xb, yb) = self.m(sx - 0.5, sy - 0.5), self.m(sx + 0.5, sy + 0.5)
            xa, xb = sorted((xa, xb))
            x0 = int(math.floor(xa + 0.5))
            x1 = max(x0, int(math.floor(xb + 0.5)) - 1)
            y0 = int(math.floor(ya + 0.5))
            y1 = max(y0, int(math.floor(yb + 0.5)) - 1)
            for yy in range(y0, y1 + 1):
                for xx in range(x0, x1 + 1):
                    _Real.set(self, xx, yy, c)
        else:
            X, Y = self.m(sx, sy)
            _Real.set(self, int(math.floor(X + 0.5)), int(math.floor(Y + 0.5)), c)

    def _raw(self, fn, *args, **kw):
        self.raw = True
        try:
            fn(self, *args, **kw)
        finally:
            self.raw = False

    def line(self, x0, y0, x1, y1, c):
        if not self.turned:
            return _Real.line(self, x0, y0, x1, y1, c)
        (a, b), (d, e) = self.m(int(x0), int(y0)), self.m(int(x1), int(y1))
        self._raw(_Real.line, int(round(a)), int(round(b)), int(round(d)), int(round(e)), c)

    def rect(self, x0, y0, x1, y1, c):
        if not self.turned:
            return _Real.rect(self, x0, y0, x1, y1, c)
        pts = [self.m(x0 - 0.5, y0 - 0.5), self.m(x1 + 0.5, y0 - 0.5), self.m(x1 + 0.5, y1 + 0.5), self.m(x0 - 0.5, y1 + 0.5)]
        self._raw(_Real.poly, pts, c)

    def hline(self, x0, x1, y, c):
        if not self.turned:
            return _Real.hline(self, x0, x1, y, c)
        self.line(min(x0, x1), y, max(x0, x1), y, c)

    def vline(self, x, y0, y1, c):
        if not self.turned:
            return _Real.vline(self, x, y0, y1, c)
        self.line(x, min(y0, y1), x, max(y0, y1), c)

    def disc(self, cx, cy, rx, ry, c):
        X, Y = self.m(cx, cy)
        self._raw(_Real.disc, X, Y, rx * self.place.sx, ry * self.place.sy, c)

    def ring(self, cx, cy, rx, ry, c, thick=1):
        X, Y = self.m(cx, cy)
        self._raw(_Real.ring, X, Y, rx * self.place.sx, ry * self.place.sy, c, max(1, int(round(thick * self.place.sx))))

    def radial(self, cx, cy, rx, ry, colors):
        X, Y = self.m(cx, cy)
        self._raw(_Real.radial, X, Y, rx * self.place.sx, ry * self.place.sy, colors)

    def poly(self, pts, c):
        self._raw(_Real.poly, [self.m(x, y) for (x, y) in pts], c)

    def poly_shade(self, pts, colors, ang=-0.7854, bias=0.0, gamma=1.0):
        self._raw(_Real.poly_shade, [self.m(x, y) for (x, y) in pts], colors, ang=ang, bias=bias, gamma=gamma)


def paint(fn, place=None, anchor=(C0, C0)):
    """Calls a painter with the canvas it returns drawn through `place`.

    Only the FIRST canvas the painter makes is mapped. A painter that builds a part on a canvas of its
    own and lays it over the first (the Familiar's head, Sleet Runes' rune) gets a plain canvas for the
    part, which is mapped once when it is laid down - never twice.
    """
    made = []

    def factory(w, h, bg=None):
        cv = XCanvas(w, h, bg, place, anchor) if not made else _Real(w, h, bg)
        made.append(cv)
        return cv

    raster.Canvas = factory
    try:
        return fn()
    finally:
        raster.Canvas = _Real


def image(fn):
    """Marks a subject as a finished picture (a familiar.Fig render) rather than a painter: it is laid
    down through place_image, which votes when shrinking."""
    fn.image = True
    return fn


def place_image(src, place, anchor):
    """A finished canvas laid through a Place. Growing fills each pixel's block; shrinking votes, so a
    block of source pixels becomes its commonest opaque colour, not whichever was scanned last."""
    if place.sx >= 1.0 and place.sy >= 1.0 or abs(place.rot) > 0.01:
        out = XCanvas(CELL, CELL, None, place, anchor)
        out.blit(src.img, 0, 0)
        return out
    m = place.mapper(anchor)
    votes = {}
    total = {}
    for y in range(src.h):
        for x in range(src.w):
            X, Y = m(x, y)
            k = (int(math.floor(X + 0.5)), int(math.floor(Y + 0.5)))
            total[k] = total.get(k, 0) + 1
            p = src.get(x, y)
            if p[3]:
                v = votes.setdefault(k, {})
                v[p] = v.get(p, 0) + 1
    out = _Real(CELL, CELL, None)
    for k, v in votes.items():
        if sum(v.values()) * 2 < total[k]:
            continue
        best = max(v.items(), key=lambda kv: (kv[1], raster._lum(kv[0])))[0]
        out.set(k[0], k[1], best)
    return out


def bbox(cv):
    xs, ys = [], []
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.get(x, y)[3]:
                xs.append(x)
                ys.append(y)
    if not xs:
        return (0, 0, CELL - 1, CELL - 1)
    return (min(xs), min(ys), max(xs), max(ys))


def over(dst, src, dx=0, dy=0, inside=None):
    """src laid over dst wherever src has a pixel (and inside() allows)."""
    for y in range(src.h):
        for x in range(src.w):
            p = src.get(x, y)
            if p[3] and (inside is None or inside(x + dx, y + dy)):
                dst.set(x + dx, y + dy, p)


# --- re-lighting ---------------------------------------------------------------------------------

def _ramp_table():
    """hex -> (row, index, length) for every element ramp and material row."""
    t = {}
    for name, tones in E.items():
        for i, h in enumerate(tones):
            t.setdefault(h.upper(), (name, i, 4))
    for name, tones in M.items():
        for i, h in enumerate(tones):
            t.setdefault(h.upper(), (name, i, 5))
    return t


RAMP = _ramp_table()
# A material's five stops onto an element's four: hi to core, lit to hot, base to mid, the dark two to
# the edge.
MAT_TO_EL = [0, 1, 2, 3, 3]


def _hex(p):
    return "#%02X%02X%02X" % (p[0], p[1], p[2])


def recolour(cv, rows, element, keep_core=False):
    """Every pixel of the named rows re-lit in `element`'s ramp - the remap the game does when a card
    moves a spell into another element. keep_core leaves the white-hot core and hot stops alone, so
    only the identity (mid and edge, art-direction.md section 3) moves."""
    target = E[element]
    out = _Real(cv.w, cv.h, None)
    for y in range(cv.h):
        for x in range(cv.w):
            p = cv.get(x, y)
            if not p[3]:
                continue
            row = RAMP.get(_hex(p))
            if row and row[0] in rows:
                idx = row[1] if row[2] == 4 else MAT_TO_EL[row[1]]
                if not (keep_core and idx < 2):
                    p = target[idx]
            out.set(x, y, p)
    return out


def darken(cv, steps=1):
    """Every pixel `steps` stops down its own row: a copy standing behind the one in front."""
    out = _Real(cv.w, cv.h, None)
    for y in range(cv.h):
        for x in range(cv.w):
            p = cv.get(x, y)
            if not p[3]:
                continue
            row = RAMP.get(_hex(p))
            if row:
                tones = E[row[0]] if row[2] == 4 else M[row[0]]
                p = tones[min(len(tones) - 1, row[1] + steps)]
            out.set(x, y, p)
    return out


def silhouette(cv, colour):
    out = _Real(cv.w, cv.h, None)
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.get(x, y)[3]:
                out.set(x, y, colour)
    return out


def glow_round(dst, cv, colour):
    """A one-pixel glow of `colour` round cv's silhouette: what lifts a DARK subject (a pit, a hole)
    off a dark night. Off by default - on a bright subject it reads as an outline."""
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.get(x, y)[3]:
                continue
            for (ox, oy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if 0 <= x + ox < cv.w and 0 <= y + oy < cv.h and cv.get(x + ox, y + oy)[3]:
                    dst.set(x, y, colour)
                    break


# =============================================================================================
# The milestone's frame, and the element's night
# =============================================================================================

# Each element's night: three stops of the disc's glow (the lit middle to the edge), and the rim
# (shaded arc, lit arc). Material kin of the element a long way down, so the subject - drawn in the
# emissive ramps - is always the brightest thing in the icon, and "nothing dark is grey" holds.
BACK = {
    "fire":      dict(glow=[M["red"][3], M["red"][4], OCC], rim=E["fire"][3], lit=E["fire"][2]),
    "ice":       dict(glow=[M["azure"][3], M["azure"][4], M["wool"][4]], rim=E["ice"][3], lit=E["ice"][1]),
    "water":     dict(glow=[M["arcane"][3], M["arcane"][4], M["wool"][4]], rim=E["water"][3], lit=E["water"][2]),
    "lightning": dict(glow=[M["wool"][3], M["wool"][4], M["violet"][4]], rim=E["lightning"][3], lit=E["lightning"][1]),
    "arcane":    dict(glow=[M["violet"][3], M["violet"][4], OCC], rim=E["arcane"][3], lit=E["arcane"][2]),
    "darkness":  dict(glow=[M["violet"][4], OCC, OCC], rim=E["darkness"][2], lit=E["darkness"][1]),
    "light":     dict(glow=[M["amber"][4], M["gold"][4], OCC], rim=E["light"][3], lit=E["light"][1]),
    "grass":     dict(glow=[M["verdant"][4], OCC, OCC], rim=E["grass"][3], lit=E["grass"][2]),
    "poison":    dict(glow=[M["linen"][3], M["linen"][4], OCC], rim=E["poison"][3], lit=E["poison"][2]),
    "earth":     dict(glow=[M["flesh"][4], M["red"][4], OCC], rim=E["earth"][3], lit=E["earth"][2]),
    "metal":     dict(glow=[M["steel"][4], M["stone"][4], OCC], rim=E["metal"][3], lit=E["metal"][1]),
    "wind":      dict(glow=[M["lichen"][3], M["lichen"][4], M["stone"][4]], rim=E["wind"][3], lit=E["wind"][2]),
}
# The grain of each night (an Ascension's and an Apotheosis's): the night's own material, a stop or two
# above its fill, never bright enough to compete with the subject.
GRAIN = {
    "fire": "ember", "ice": "azure", "water": "arcane", "lightning": "wool", "arcane": "violet",
    "darkness": "violet", "light": "amber", "grass": "verdant", "poison": "linen", "earth": "flesh",
    "metal": "steel", "wind": "lichen",
}

DISC_R = {4: 15.0, 8: 15.0, 9: 13.0}


def in_disc(r):
    def f(x, y):
        dx, dy = x - C0, y - C0
        return dx * dx + dy * dy <= r * r
    return f


def facing(x, y, cx=C0, cy=C0):
    """How squarely a point on a circle faces the key (upper left): 1 facing it, -1 away."""
    a = math.atan2(y - cy, x - cx)
    return math.cos(a - math.radians(225.0))


def night(c, element, r):
    """The disc: a dithered glow, lit a little up and left of centre, toward the key."""
    g = BACK[element]["glow"]
    tmp = _Real(CELL, CELL, None)
    tmp.radial(C0 - 1.5, C0 - 2.0, r + 2.5, r + 2.5, g)
    over(c, tmp, inside=in_disc(r - 0.5))


def rim(c, element, r):
    b = BACK[element]
    for y in range(CELL):
        for x in range(CELL):
            d = math.hypot(x - C0, y - C0)
            if r - 1.0 < d <= r:
                c.set(x, y, b["lit"] if facing(x, y) > 0.35 else b["rim"])


def gold_ring(c, r):
    """The Ascension's ring: two pixels of gold lit on the key side, and a line of occlusion inside
    it, so the disc's light never runs into the gold."""
    for y in range(CELL):
        for x in range(CELL):
            d = math.hypot(x - C0, y - C0)
            f = facing(x, y)
            if r - 1.0 < d <= r:
                c.set(x, y, GOLD[1] if f > 0.3 else (GOLD[2] if f > -0.5 else GOLD[3]))
            elif r - 2.0 < d <= r - 1.0:
                c.set(x, y, GOLD[0] if f > 0.7 else (GOLD[1] if f > -0.2 else GOLD[2]))
            elif r - 3.0 < d <= r - 2.0:
                c.set(x, y, OCC)


def gems(c, r, ramp):
    """Four gems of the element set into the ring, at the compass points."""
    for (x, y) in ((C0, C0 - r + 1.0), (C0 + r - 1.0, C0), (C0, C0 + r - 1.0), (C0 - r + 1.0, C0)):
        x, y = int(round(x)), int(round(y))
        for (ox, oy) in ((0, -2), (2, 0), (0, 2), (-2, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)):
            c.set(x + ox, y + oy, GOLD[3] if ox + oy > 0 else GOLD[0])
        for (ox, oy) in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            c.set(x + ox, y + oy, ramp[2])
        c.set(x, y, ramp[0])
        c.set(x - 1, y - 1, GOLD[0])


def sunburst(c, ramp, r0=11.0, r1=23.0, n=16):
    """The Apotheosis's burst: rays out of the disc to the corners, long and short in turn, each a
    wedge with a hot spine."""
    core, hot, mid, edge = ramp
    for k in range(n):
        a = TAU * k / n + TAU / (2 * n)
        long_ray = k % 2 == 0
        length = r1 if long_ray else r1 - 6.0
        half = 0.17 if long_ray else 0.12
        c.poly([(C0 + math.cos(a - half) * r0, C0 + math.sin(a - half) * r0),
                (C0 + math.cos(a) * length, C0 + math.sin(a) * length),
                (C0 + math.cos(a + half) * r0, C0 + math.sin(a + half) * r0)], edge)
        c.poly([(C0 + math.cos(a - half * 0.45) * r0, C0 + math.sin(a - half * 0.45) * r0),
                (C0 + math.cos(a) * (length - 3.0), C0 + math.sin(a) * (length - 3.0)),
                (C0 + math.cos(a + half * 0.45) * r0, C0 + math.sin(a + half * 0.45) * r0)], mid if long_ray else edge)
        if long_ray:
            c.line(C0 + math.cos(a) * r0, C0 + math.sin(a) * r0,
                   C0 + math.cos(a) * (length - 7.0), C0 + math.sin(a) * (length - 7.0), hot)


def crown(c, ramp):
    """Three gold points standing on the top of the Apotheosis's ring, a gem of the element in each."""
    top = C0 - DISC_R[9] - 0.5
    for (cx, h) in ((C0 - 5.5, 3.0), (C0, 5.0), (C0 + 5.5, 3.0)):
        base = top + 2.0
        c.poly([(cx - 2.4, base), (cx, base - h - 1.0), (cx + 2.4, base)], GOLD[2])
        c.line(cx - 1, base - 1, cx, base - h, GOLD[0])
        c.set(int(round(cx)), int(round(base - h)), ramp[0])
    c.hline(int(C0 - 8), int(C0 + 8), int(top + 2), GOLD[1])
    c.hline(int(C0 - 8), int(C0 + 8), int(top + 3), GOLD[3])


# --- the grain of an Ascension's night -------------------------------------------------------------

def _g(element):
    return M[GRAIN[element]]


def grain_ice(c):
    m = _g("ice")
    for (x, y, s) in ((7, 9, 2), (24, 8, 1), (9, 23, 1), (25, 22, 2), (5, 16, 1)):
        for k in range(1, s + 1):
            for (ox, oy) in ((k, 0), (-k, 0), (0, k), (0, -k)):
                c.set(x + ox, y + oy, m[3])
        c.set(x, y, m[2])


def grain_fire(c):
    m = _g("fire")
    for (x, y) in ((8, 8), (22, 6), (25, 14), (6, 18), (12, 4), (27, 20), (10, 13)):
        c.set(x, y, m[3] if (x + y) % 3 else m[2])


def grain_water(c):
    m = _g("water")
    for (x, y, rr) in ((8, 10, 1.6), (23, 7, 1.2), (25, 19, 1.8), (7, 21, 1.2)):
        c.ring(x, y, rr, rr, m[3])


def grain_lightning(c):
    m = _g("lightning")
    pts = [(3, 9), (9, 12), (7, 16), (14, 19), (12, 23), (19, 28)]
    for i in range(len(pts) - 1):
        c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], m[3])


def grain_arcane(c):
    m = _g("arcane")
    for d in range(0, 360, 6):
        if (d // 6) % 5 == 4:
            continue
        a = math.radians(d)
        c.set(C0 + math.cos(a) * 10.5, C0 + math.sin(a) * 10.5, m[2] if (d // 30) % 3 == 0 else m[3])


def grain_darkness(c):
    m = _g("darkness")
    for arm in range(3):
        base = arm * TAU / 3.0 + 0.4
        for i in range(30):
            t = i / 30.0
            rr = 12.5 - t * 7.0
            a = base + t * 2.4
            c.set(C0 + math.cos(a) * rr, C0 + math.sin(a) * rr, m[3])


def grain_light(c):
    m = _g("light")
    for k in range(12):
        a = TAU * k / 12.0
        for i in range(6, 14):
            if (i + k) % 2 == 0:
                c.set(C0 + math.cos(a) * i, C0 + math.sin(a) * i, m[3])


def grain_grass(c):
    m = _g("grass")
    for (x, y, ang) in ((7, 9, 0.6), (24, 7, 2.4), (6, 20, -0.4), (25, 22, 3.6)):
        for i in range(4):
            c.set(x + math.cos(ang) * i, y + math.sin(ang) * i, m[2])


def grain_poison(c):
    m = _g("poison")
    for (x, y, rr) in ((8, 9, 1.5), (23, 6, 1.0), (26, 17, 1.6), (6, 20, 1.0)):
        c.ring(x, y, rr, rr, m[2])


def grain_earth(c):
    m = _g("earth")
    for (y, x0, x1) in ((22, 2, 13), (24, 18, 30), (27, 4, 22), (19, 23, 30)):
        c.hline(x0, x1, y, m[3])


def grain_metal(c):
    m = _g("metal")
    c.line(4, 20, 20, 4, m[3])
    c.line(12, 28, 28, 12, m[3])


def grain_wind(c):
    m = _g("wind")
    for (cx, cy, rr, a0, a1) in ((12, 14, 9.0, 3.4, 5.6), (20, 18, 8.0, 0.2, 2.4)):
        for i in range(24):
            a = a0 + (a1 - a0) * i / 23.0
            c.set(cx + math.cos(a) * rr, cy + math.sin(a) * rr, m[3])


GRAINS = {
    "ice": grain_ice, "fire": grain_fire, "water": grain_water, "lightning": grain_lightning,
    "arcane": grain_arcane, "darkness": grain_darkness, "light": grain_light, "grass": grain_grass,
    "poison": grain_poison, "earth": grain_earth, "metal": grain_metal, "wind": grain_wind,
}


# =============================================================================================
# Element accents: what a card that GAINS an element adds over its subject, at the rim
# =============================================================================================

def disc_top(x, r):
    dx = x - C0
    return None if abs(dx) >= r else C0 - math.sqrt(r * r - dx * dx)


def disc_bottom(x, r):
    dx = x - C0
    return None if abs(dx) >= r else C0 + math.sqrt(r * r - dx * dx)


def acc_ice(c, r, heavy=False):
    """Icicles hanging from the top of the disc, and the frost on the rim they hang from."""
    core, hot, mid, edge = E["ice"]
    for x in range(int(C0 - r + 3), int(C0 + r - 2)):
        t = disc_top(x, r)
        if t is not None:
            c.set(x, int(math.ceil(t)), core if x % 3 == 0 else hot)
    spikes = ((8, 4), (11, 7), (14, 4), (18, 8), (21, 5), (24, 6)) if heavy else ((9, 5), (13, 8), (18, 6), (22, 9))
    for (x, length) in spikes:
        t = disc_top(x, r)
        if t is None:
            continue
        y0 = int(math.ceil(t)) + 1
        for k in range(length):
            c.set(x, y0 + k, hot if k < length - 1 else core)
            if k < length // 2 + 1:
                c.set(x + 1, y0 + k, mid if k else hot)
                c.set(x - 1, y0 + k, edge) if k < length // 3 else None
        c.set(x, y0 + length, mid)


def acc_fire(c, r, heavy=False):
    """Flame tongues licking up from the bottom of the disc."""
    core, hot, mid, edge = E["fire"]
    tongues = ((6, 6, -1), (10, 9, 1), (14, 6, -1), (18, 10, 1), (22, 7, -1), (26, 5, 1)) if heavy \
        else ((7, 5, -1), (12, 8, 1), (19, 7, -1), (24, 5, 1))
    for (x, h, sway) in tongues:
        b = disc_bottom(x, r)
        if b is None:
            continue
        base = b - 0.5
        c.poly([(x - 2.4, base), (x - 1.0, base - h * 0.55), (x + sway, base - h), (x + 1.4, base - h * 0.5), (x + 2.4, base)], edge)
        c.poly([(x - 1.4, base), (x - 0.4, base - h * 0.5), (x + sway * 0.5, base - h * 0.75), (x + 1.2, base)], mid)
        c.poly([(x - 0.6, base), (x, base - h * 0.4), (x + 0.6, base)], hot)


def acc_water(c, r, heavy=False):
    """A wave crossing the bottom of the disc, and drops falling from the top."""
    core, hot, mid, edge = E["water"]
    surface = 23.0 if heavy else 24.5
    for x in range(CELL):
        b = disc_bottom(x, r - 0.5)
        if b is None:
            continue
        s = surface + math.sin(x * 0.55) * 1.3
        for y in range(int(math.ceil(s)), int(b) + 1):
            c.set(x, y, edge if y > s + 1.5 else mid)
        crest = math.sin(x * 0.55) < -0.55
        c.set(x, int(math.ceil(s)) - (1 if crest else 0), core if crest else hot)
    for (x, y) in ((9, 6), (22, 5)) + (((15, 3),) if heavy else ()):
        c.set(x, y, hot)
        c.set(x, y + 1, mid)
        c.set(x + 1, y + 1, edge)
        c.set(x, y + 2, mid)
        c.set(x + 1, y + 2, edge)


def acc_lightning(c, r, heavy=False):
    """Sparks forking in from the rim."""
    core, hot, mid, edge = E["lightning"]
    forks = [((27, 6), (24, 9), (25, 11), (22, 13)), ((4, 24), (8, 22), (7, 19), (10, 17))]
    if heavy:
        forks.append(((26, 27), (23, 24), (25, 22), (21, 20)))
    for pts in forks:
        for i in range(len(pts) - 1):
            c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], hot)
        c.set(pts[0][0], pts[0][1], core)
        c.set(pts[-1][0], pts[-1][1], mid)
    for (x, y) in ((6, 7), (26, 18)):
        for (ox, oy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            c.set(x + ox, y + oy, mid)
        c.set(x, y, core)


def acc_poison(c, r, heavy=False):
    """Drips running down from the top of the disc, and bubbles at the bottom."""
    core, hot, mid, edge = E["poison"]
    for (x, length) in ((10, 4), (17, 6), (23, 3)) + (((13, 2), (20, 3)) if heavy else ()):
        t = disc_top(x, r)
        if t is None:
            continue
        y0 = int(math.ceil(t))
        for k in range(length):
            c.set(x, y0 + k, mid)
        c.set(x, y0 + length, mid)
        c.set(x + 1, y0 + length, edge)
        c.set(x, y0 + length + 1, edge)
        c.set(x - 1, y0 + length, hot)
    for (x, y, rr) in ((9, 25, 1.4), (14, 27, 1.0), (22, 26, 1.6)):
        c.ring(x, y, rr, rr, mid)
        c.set(x - 1, y - 1, core)


def acc_grass(c, r, heavy=False):
    """A vine climbing the rim from the lower left over the top, in leaf."""
    core, hot, mid, edge = E["grass"]
    a0, a1 = math.radians(140.0), math.radians(310.0 if heavy else 265.0)
    rr = r - 1.5
    n = 60
    prev = None
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / float(n)
        x, y = C0 + math.cos(a) * rr, C0 + math.sin(a) * rr
        if prev:
            c.line(prev[0], prev[1], x, y, edge)
        prev = (x, y)
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / float(n)
        c.set(C0 + math.cos(a) * (rr - 1), C0 + math.sin(a) * (rr - 1), mid)
    leaves = 5 if heavy else 4
    for k in range(leaves):
        a = a0 + (a1 - a0) * (k + 0.5) / float(leaves)
        x, y = C0 + math.cos(a) * (rr - 1), C0 + math.sin(a) * (rr - 1)
        ia = a + math.pi + (0.6 if k % 2 else -0.6)
        for d in range(1, 4):
            c.set(x + math.cos(ia) * d, y + math.sin(ia) * d, mid if d < 3 else hot)
        c.set(x + math.cos(ia + 0.5) * 2, y + math.sin(ia + 0.5) * 2, hot)


def acc_earth(c, r, heavy=False):
    """Rocks thrown up, and the floor cracked across the bottom of the disc."""
    core, hot, mid, edge = E["earth"]
    for (x, y, w) in ((6, 9, 3), (25, 7, 2), (27, 18, 2), (4, 17, 2)) + (((19, 4, 2),) if heavy else ()):
        c.rect(x, y, x + w - 1, y + w - 1, mid)
        c.set(x, y, hot)
        c.hline(x, x + w - 1, y + w, edge)
        c.vline(x + w, y, y + w, edge)
    pts = [(5, 26), (9, 27), (12, 26), (16, 28), (20, 27), (24, 28), (27, 26)]
    for i in range(len(pts) - 1):
        c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], edge)
    for (x, y) in ((9, 26), (16, 27), (24, 27)):
        c.set(x, y, mid)


def acc_darkness(c, r, heavy=False):
    """Shadow tendrils creeping in from the rim."""
    core, hot, mid, edge = E["darkness"]
    for (a0, sweep) in ((2.3, 1.0), (5.6, -0.9), (0.6, 0.8)) + (((3.9, -0.9),) if heavy else ()):
        prev = None
        for i in range(16):
            t = i / 15.0
            rr = r - 0.5 - t * 6.5
            a = a0 + sweep * t * t
            x, y = C0 + math.cos(a) * rr, C0 + math.sin(a) * rr
            if prev:
                c.line(prev[0], prev[1], x, y, mid if t < 0.6 else hot)
            if t < 0.45:
                c.set(x + 1, y, edge)
            prev = (x, y)
        c.set(prev[0], prev[1], core)


def acc_light(c, r, heavy=False):
    """Glints of light caught on the rim."""
    stars = [(25, 6, 3), (6, 25, 2), (5, 8, 1)] + ([(27, 24, 2)] if heavy else [])
    for (x, y, arm) in stars:
        m_glint(c, x, y, arm, E["light"])


def acc_arcane(c, r, heavy=False):
    """Motes of arcane light going round inside the rim."""
    core, hot, mid, edge = E["arcane"]
    n = 8 if heavy else 6
    for k in range(n):
        a = TAU * k / n + 0.3
        x, y = C0 + math.cos(a) * (r - 2.5), C0 + math.sin(a) * (r - 2.5)
        c.set(x, y, core if k % 2 == 0 else hot)
        c.set(x + 1, y, mid)
        c.set(x, y + 1, edge)


def acc_metal(c, r, heavy=False):
    """Rivets driven round the rim."""
    core, hot, mid, edge = E["metal"]
    n = 10 if heavy else 8
    for k in range(n):
        a = TAU * k / n + TAU / (2 * n)
        x, y = int(round(C0 + math.cos(a) * (r - 2.0))), int(round(C0 + math.sin(a) * (r - 2.0)))
        c.set(x, y, core)
        c.set(x + 1, y, mid)
        c.set(x, y + 1, mid)
        c.set(x + 1, y + 1, edge)


def acc_wind(c, r, heavy=False):
    """Gusts sweeping across the disc, each ending in a curl."""
    core, hot, mid, edge = E["wind"]
    for (y, x0, x1, curl) in ((8, 4, 16, 1), (24, 14, 28, -1)) + (((15, 2, 9, 1),) if heavy else ()):
        for x in range(x0, x1 + 1):
            c.set(x, y + math.sin((x - x0) * 0.4) * 0.8, mid if x < x1 - 3 else hot)
        cx, cy = x1, y - 2 * curl
        for d in range(0, 270, 30):
            a = math.radians(d) * curl + (math.pi / 2 if curl > 0 else -math.pi / 2)
            c.set(cx + math.cos(a) * 2, cy + math.sin(a) * 2, hot)
        c.set(x0, y, core)


ACCENTS = {
    "ice": acc_ice, "fire": acc_fire, "water": acc_water, "lightning": acc_lightning, "poison": acc_poison,
    "grass": acc_grass, "earth": acc_earth, "darkness": acc_darkness, "light": acc_light,
    "arcane": acc_arcane, "metal": acc_metal, "wind": acc_wind,
}


# =============================================================================================
# Verbs: what the card DOES. Each is f(c, ctx) once bound (see V below).
# =============================================================================================

def _unit(v):
    n = math.hypot(v[0], v[1]) or 1.0
    return (v[0] / n, v[1] / n)


def m_speed(c, ramp, travel, box, n=4, length=9):
    """Speed lines trailing back from the subject, against its travel: two pixels wide, bright at the
    head and cooling down the ramp to the tail, so they read as motion and not as more of the thing."""
    core, hot, mid, edge = ramp
    tx, ty = _unit(travel)
    px, py = -ty, tx
    cx, cy = (box[0] + box[2]) / 2.0, (box[1] + box[3]) / 2.0
    half = max(box[2] - box[0], box[3] - box[1]) / 2.0
    spread = min(9.0, half + 1.0)
    offs = [(-1.0 + 2.0 * k / (n - 1)) * spread for k in range(n)]
    for k, off in enumerate(offs):
        sx = cx - tx * half * 0.45 + px * off
        sy = cy - ty * half * 0.45 + py * off
        ln = length + (k % 2) * 4
        ex, ey = sx - tx * ln, sy - ty * ln
        c.line(sx, sy, ex, ey, edge)
        c.line(sx + px, sy + py, sx + px - tx * ln * 0.75, sy + py - ty * ln * 0.75, edge)
        c.line(sx, sy, sx - tx * ln * 0.6, sy - ty * ln * 0.6, mid)
        c.line(sx, sy, sx - tx * ln * 0.25, sy - ty * ln * 0.25, hot)
        c.set(sx, sy, core)


def m_cycle(c, ramp, centre=(C0, C0), rr=12.5):
    """A shorter cooldown on a spell that does not travel: two arrows chasing each other round it,
    two pixels thick and brightening toward their heads, so they read at the 40 a plate shows."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for a0 in (math.radians(200.0), math.radians(20.0)):
        span = math.radians(120.0)
        pts = []
        for i in range(40):
            t = i / 39.0
            a = a0 + span * t
            x, y = cx + math.cos(a) * rr, cy + math.sin(a) * rr
            c.set(cx + math.cos(a) * (rr + 1), cy + math.sin(a) * (rr + 1), edge if t < 0.5 else mid)
            c.set(x, y, mid if t < 0.5 else hot)
            pts.append((x, y))
        a = a0 + span
        tx, ty = -math.sin(a), math.cos(a)
        hx, hy = pts[-1]
        for side in (-1, 1):
            nx, ny = math.cos(a) * side, math.sin(a) * side
            for k in range(0, 4):
                c.set(hx - tx * k + nx * k, hy - ty * k + ny * k, hot if k < 3 else mid)
                c.set(hx - tx * (k + 1) + nx * k, hy - ty * (k + 1) + ny * k, mid)
        c.set(hx + tx, hy + ty, core)
        c.set(hx, hy, core)


def m_ghosts(c, subject, travel, colours, steps=(4, 8)):
    """Afterimages: the subject's silhouette left behind it, fading."""
    tx, ty = _unit(travel or (1, 0))
    for k, d in reversed(list(enumerate(steps))):
        over(c, silhouette(subject, colours[min(k, len(colours) - 1)]), int(round(-tx * d)), int(round(-ty * d)))


def m_rays(c, ramp, centre, r0=8.0, r1=14.5, n=12):
    """An impact's rays behind the subject: what makes a bigger card hit harder."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for k in range(n):
        a = TAU * k / n + 0.13
        long_ray = k % 2 == 0
        end = r1 if long_ray else r1 - 3.5
        c.line(cx + math.cos(a) * r0, cy + math.sin(a) * r0, cx + math.cos(a) * end, cy + math.sin(a) * end,
               mid if long_ray else edge)
        if long_ray:
            c.set(cx + math.cos(a) * (r0 + 1), cy + math.sin(a) * (r0 + 1), hot)


def m_shockwave(c, ramp, centre=(C0, C0), rr=12.5, rings=2):
    """Broken rings going out: a colossal card's shock."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for k in range(rings):
        r = rr - k * 3.0
        for d in range(0, 360, 3):
            if (d // 24) % 4 == 3:
                continue
            a = math.radians(d + k * 30)
            c.set(cx + math.cos(a) * r, cy + math.sin(a) * r, mid if k == 0 else edge)
            if k == 0:
                c.set(cx + math.cos(a) * (r - 1), cy + math.sin(a) * (r - 1), edge)
        for d in range(10, 360, 45):
            a = math.radians(d + k * 30)
            c.set(cx + math.cos(a) * r, cy + math.sin(a) * r, hot)


def m_vortex(c, ramp, centre=(C0, C0), rr=13.5, arms=3, turns=3.2):
    """A pull: arms winding in, bright where they go under."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for arm in range(arms):
        base = arm * TAU / arms + 0.5
        for i in range(60):
            t = i / 59.0
            r = rr - t * (rr - 3.5)
            a = base + t * turns
            c.set(cx + math.cos(a) * r, cy + math.sin(a) * r, edge if t < 0.3 else (mid if t < 0.75 else hot))
            if t < 0.55:
                c.set(cx + math.cos(a) * (r - 1), cy + math.sin(a) * (r - 1), edge)


def m_pool(c, ramp, cy=25.5, rx=11.5, ry=3.4):
    """What a lingering card leaves on the ground: a pool of the spell's own light."""
    core, hot, mid, edge = ramp
    c.disc(C0, cy, rx, ry, edge)
    c.disc(C0 - 0.5, cy - 0.3, rx - 2.5, ry - 1.1, mid)
    for (x, y) in ((C0 - 6, cy - 1), (C0 + 3, cy), (C0 + 7, cy - 1)):
        c.set(x, y, hot)
    c.set(C0 - 2, cy - 1, core)


def m_magma(c, cy=25.5, rx=12.5):
    """Burning ground: a crusted pool of magma, flames standing on it."""
    f = E["fire"]
    c.disc(C0, cy, rx, 3.6, OCC)
    c.disc(C0, cy - 0.3, rx - 1.5, 2.8, f[3])
    for (x, y) in ((8, 25), (12, 26), (17, 25), (21, 26), (24, 25)):
        c.set(x, y, f[2])
        c.set(x + 1, y, f[1])
    for (x, h) in ((7, 4), (14, 6), (20, 5), (25, 3)):
        c.poly([(x - 1.5, cy - 1), (x + 0.3, cy - 1 - h), (x + 1.5, cy - 1)], f[2])
        c.set(x, cy - 2, f[1])


def m_roots(c, cy=30.0):
    """Held in place: roots rising from the bottom and curling over."""
    e = E["earth"]
    for (x0, dirx, h) in ((8, 1, 10), (16, -1, 12), (24, -1, 9)):
        prev = None
        for i in range(13):
            t = i / 12.0
            x = x0 + dirx * math.sin(t * 2.4) * 3.5
            y = cy - t * h
            if prev:
                c.line(prev[0], prev[1], x, y, e[3] if t < 0.5 else e[2])
                if t < 0.5:
                    c.line(prev[0] + 1, prev[1], x + 1, y, e[3])
            prev = (x, y)
        c.set(prev[0], prev[1], e[1])


def m_freeze(c, centre, spread=10.0):
    """Frozen solid: shards of ice standing round the subject."""
    i = E["ice"]
    cx, cy = centre
    for (a, h) in ((200, 6), (240, 8), (300, 7), (340, 5), (20, 6), (150, 5), (90, 5)):
        ang = math.radians(a)
        bx, by = cx + math.cos(ang) * spread, cy + math.sin(ang) * spread
        tx, ty = cx + math.cos(ang) * (spread + h), cy + math.sin(ang) * (spread + h)
        px, py = -math.sin(ang) * 1.7, math.cos(ang) * 1.7
        c.poly([(bx - px, by - py), (tx, ty), (bx + px, by + py)], i[3])
        c.line(bx, by, tx, ty, i[1])
        c.set(tx, ty, i[0])


HEART = ((0, 0), (1, 0), (3, 0), (4, 0), (-1, 1), (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1),
         (-1, 2), (0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (0, 3), (1, 3), (2, 3), (3, 3),
         (4, 3), (1, 4), (2, 4), (3, 4), (2, 5))


def m_heart(c, x=23, y=5, drops=True):
    """Lifesteal: a heart, and the blood running to it."""
    e = M["ember"]
    for (ox, oy) in HEART:
        c.set(x + ox, y + oy, e[3] if (ox + oy > 6 or oy == 5) else (e[2] if ox + oy > 3 else e[1]))
    c.set(x, y + 1, e[0])
    c.set(x + 1, y, e[0])
    if drops:
        for (dx, dy) in ((-5, 7), (-8, 10), (-3, 10)):
            c.set(x + dx, y + dy, e[1])
            c.set(x + dx, y + dy + 1, e[3])


def m_glint(c, x, y, arm=4, ramp=None):
    """A critical edge: a four-point star of light."""
    core, hot, mid, edge = ramp or E["light"]
    x, y = int(round(x)), int(round(y))
    for k in range(1, arm + 1):
        for (ox, oy) in ((k, 0), (-k, 0), (0, k), (0, -k)):
            c.set(x + ox, y + oy, hot if k <= (arm + 1) // 2 else mid)
    if arm >= 3:
        for (ox, oy) in ((1, 1), (-1, -1), (1, -1), (-1, 1)):
            c.set(x + ox, y + oy, hot)
    c.set(x, y, core)


def m_blast(c, ramp, x, y, r=5.0):
    """A burst where it lands."""
    core, hot, mid, edge = ramp
    pts = []
    for k in range(16):
        a = TAU * k / 16.0
        rr = r if k % 2 == 0 else r * 0.55
        pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
    c.poly(pts, edge)
    c.disc(x, y, r * 0.5, r * 0.5, mid)
    c.disc(x, y, r * 0.28, r * 0.28, hot)
    c.set(x, y, core)


def m_chevrons(c, ramp, centre=(C0, C0), gap=10, vertical=False):
    """Knockback: chevrons thrown outward on either side."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for side in (-1, 1):
        for k in range(2):
            o = gap + k * 3
            for d in range(-3, 4):
                along = side * (o + (3 - abs(d)) * 0.8)
                if vertical:
                    c.set(cx + d, cy + along, hot if k == 0 else mid)
                else:
                    c.set(cx + along, cy + d, hot if k == 0 else mid)


def m_pierce(c, ramp, tip, travel, marks=2):
    """It goes through: a line of light run on through the tip, skewering marks - dark rings, the
    shape the game's own enemies take at this size - one after another."""
    core, hot, mid, edge = ramp
    tx, ty = _unit(travel)
    px, py = -ty, tx
    x0, y0 = tip
    c.line(x0 - tx * 26, y0 - ty * 26, x0 + tx * 30, y0 + ty * 30, mid)
    c.line(x0 - tx * 26 + px, y0 - ty * 26 + py, x0 + tx * 30 + px, y0 + ty * 30 + py, edge)
    for k in range(marks):
        d = 3 + k * 6
        x, y = x0 + tx * d, y0 + ty * d
        c.disc(x, y, 2.6, 2.6, mid)
        c.disc(x, y, 1.6, 1.6, OCC)
        c.set(x, y, hot)
        c.set(x + tx, y + ty, core)


def dart(c, ramp, x0, y0, x1, y1):
    """A small bolt of light from (x0, y0) to its head at (x1, y1): what a split throws."""
    core, hot, mid, edge = ramp
    tx, ty = _unit((x1 - x0, y1 - y0))
    px, py = -ty, tx
    c.line(x0, y0, x1, y1, mid)
    c.line(x0 + px, y0 + py, x1 - tx * 2 + px, y1 - ty * 2 + py, edge)
    c.poly([(x1 + tx * 2, y1 + ty * 2), (x1 - tx * 2 + px * 2, y1 - ty * 2 + py * 2),
            (x1 - tx * 2 - px * 2, y1 - ty * 2 - py * 2)], hot)
    c.set(x1, y1, core)


def m_fork(c, ramp, tip, travel, spread=0.75, length=8):
    """It splits: two shards leaving the tip either side of its travel."""
    core, hot, mid, edge = ramp
    a = math.atan2(travel[1], travel[0])
    for side in (-1, 1):
        b = a + side * spread
        x1, y1 = tip[0] + math.cos(b) * length, tip[1] + math.sin(b) * length
        c.line(tip[0], tip[1], x1, y1, mid)
        c.line(tip[0] + math.cos(b) * 2, tip[1] + math.sin(b) * 2, x1, y1, hot)
        c.disc(x1, y1, 1.2, 1.2, mid)
        c.set(x1, y1, core)


def m_chain(c, ramp, start, ends, node=1.6):
    """It jumps: jagged arcs out to the next victims, a node of light on each."""
    core, hot, mid, edge = ramp
    for (ex, ey) in ends:
        sx, sy = start
        prev = (sx, sy)
        for i in range(1, 5):
            t = i / 4.0
            j = ((1 if i % 2 else -1) * 1.5) if i < 4 else 0.0
            x, y = sx + (ex - sx) * t + j, sy + (ey - sy) * t - j
            c.line(prev[0], prev[1], x, y, hot)
            prev = (x, y)
        c.disc(ex, ey, node, node, mid)
        c.set(ex, ey, core)


def m_crack(c, ramp, y=27, glow=True):
    """The ground torn open across the bottom, light coming up through it."""
    core, hot, mid, edge = ramp
    pts = [(3, y - 1), (7, y + 1), (10, y - 1), (14, y + 2), (18, y), (22, y + 2), (25, y), (29, y + 1)]
    for i in range(len(pts) - 1):
        c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], OCC)
        c.line(pts[i][0], pts[i][1] + 1, pts[i + 1][0], pts[i + 1][1] + 1, mid if glow else edge)
    for (x, yy) in pts[1:-1:2]:
        c.set(x, yy + 1, hot)


def m_cloud(c, cy=7.0):
    """A storm cloud across the top of the disc: the thunderhead."""
    w = M["wool"]
    for (x, y, r) in ((9, cy + 1, 4.5), (15, cy - 1, 5.5), (22, cy, 5.0), (26, cy + 2, 3.5), (5, cy + 3, 3.0)):
        c.disc(x, y, r, r * 0.8, w[3])
    for (x, y, r) in ((9, cy, 3.2), (15, cy - 2, 4.0), (22, cy - 1, 3.6)):
        c.disc(x - 0.5, y - 0.5, r, r * 0.7, w[2])
    for (x, y) in ((8, cy - 2), (13, cy - 5), (14, cy - 5), (21, cy - 4)):
        c.set(x, y, w[1])
    c.hline(5, 27, int(cy + 4), w[4])


def m_orbs(c, points):
    """XP orbs and a flask, being drawn in."""
    g = E["grass"]
    for (x, y) in points:
        c.disc(x, y, 1.6, 1.6, g[3])
        c.set(x, y, g[1])
        c.set(x - 1, y - 1, g[0])


def m_trail(c, ramp, start, end, dash=2):
    """A dotted line of travel: how far it now reaches."""
    core, hot, mid, edge = ramp
    n = int(math.hypot(end[0] - start[0], end[1] - start[1]))
    for i in range(n):
        if (i // dash) % 2 == 0:
            t = i / float(max(1, n))
            c.set(start[0] + (end[0] - start[0]) * t, start[1] + (end[1] - start[1]) * t, mid if t < 0.8 else hot)


def m_drops(c, ramp, points):
    """Drops of the spell's stuff thrown off it."""
    core, hot, mid, edge = ramp
    for (x, y) in points:
        c.set(x, y, hot)
        c.set(x, y + 1, mid)
        c.set(x + 1, y + 1, edge)


# =============================================================================================
# The spells: each one's painter, which way it travels, where its business end is, and which of its
# colours are its effect (the rows an element path re-lights)
# =============================================================================================

SPELLS = {
    "magic_missile":     dict(paint=sc.magic_missile, travel=(1, 0), tip=(29, 16), rows=("arcane",)),
    "fireball":          dict(paint=sc.fireball, travel=(1, 1), tip=(25, 25), rows=("fire",)),
    "frost_shard":       dict(paint=sc.frost_shard, travel=None, tip=(16, 16), rows=("ice",)),
    "chain_lightning":   dict(paint=sc.chain_lightning, travel=(0.5, 1), tip=(22, 29), rows=("lightning",)),
    "shadow_bolt":       dict(paint=sc.shadow_bolt, travel=None, tip=(16, 16), rows=("darkness",)),
    "thorn_vine":        dict(paint=sc.thorn_vine, travel=(1, -1), tip=(28, 5), rows=("grass", "poison"), dark=True),
    "gale_blade":        dict(paint=sc.gale_blade, travel=(1, 0), tip=(27, 14), rows=("wind",)),
    "molten_shard":      dict(paint=sc.molten_shard, travel=(1, -0.8), tip=(27, 4), rows=("fire",), dark=True),
    "cone_of_cold":      dict(paint=sc.cone_of_cold, travel=(1, 0), tip=(29, 16), rows=("ice",)),
    "solar_flare":       dict(paint=sc.solar_flare, travel=None, tip=(16, 16), rows=("light",)),
    "scorching_ray":     dict(paint=sc.scorching_ray, travel=(1, -1), tip=(28, 3), rows=("fire",)),
    "obsidian_spike":    dict(paint=sc.obsidian_spike, travel=(0, -1), tip=(21, 1), rows=("earth",), dark=True),
    "cyclone_slash":     dict(paint=sc.cyclone_slash, travel=(1, 0), tip=(24, 16), rows=("wind",)),
    "void_lance":        dict(paint=sc.void_lance, travel=(1, -1), tip=(30, 1), rows=("darkness", "arcane")),
    "meteor_swarm":      dict(paint=sc.meteor_swarm, travel=(1, 1), tip=(24, 18), rows=("fire",)),
    "black_tentacles":   dict(paint=sc.black_tentacles, travel=None, tip=(16, 8), rows=("poison",)),
    # Spiritual Weapon is taken in a form (SpellData.Variants), and every form's milestones are its own, so
    # each form is a subject of its own: the weapon itself, drawn by tools/art/spirit_weapons.py (subject_of).
    "spiritual_weapon_axe":    dict(paint=None, travel=(0.64, -0.77), tip=(22, 8), rows=("arcane",)),
    "spiritual_weapon_hammer": dict(paint=None, travel=(0.64, -0.77), tip=(22, 8), rows=("arcane",)),
    "spiritual_weapon_sword":  dict(paint=None, travel=(0.64, -0.77), tip=(24, 6), rows=("arcane",)),
    "aegis_ward":        dict(paint=sc.aegis_ward, travel=None, tip=(16, 15), rows=("light",), dark=True),
    "arcane_explosion":  dict(paint=sc.arcane_explosion, travel=None, tip=(16, 16), rows=("arcane",)),
    "glacial_spike":     dict(paint=sc.glacial_spike, travel=(0, -1), tip=(16, 3), rows=("ice",)),
    "hunters_draw":      dict(paint=sc.hunters_draw, travel=(1, 0), tip=(30, 16), rows=("wind",)),
    "toxic_spore_burst": dict(paint=sc.spore_burst, travel=(0, -1), tip=(16, 6), rows=("poison",)),
    "riptide":           dict(paint=si.riptide, travel=(1, 0), tip=(28, 15), rows=("azure", "water")),
    "cinderbreath":      dict(paint=si.cinderbreath, travel=(1, -1), tip=(29, 5), rows=("fire",)),
    "mirefoot":          dict(paint=si.mirefoot, travel=None, tip=(16, 21), rows=("azure",), dark=True),
    "kindled_ward":      dict(paint=si.kindled_ward, travel=None, tip=(16, 16), rows=("amber", "light")),
    "gravewell":         dict(paint=si.gravewell, travel=None, tip=(16, 17), rows=("earth",)),
    "iron_palisade":     dict(paint=si.iron_palisade, travel=None, tip=(16, 8), rows=("metal",)),
    "contagion":         dict(paint=si.contagion, travel=None, tip=(16, 16), rows=("poison",)),
    "bramble_seed":      dict(paint=si.bramble_seed, travel=(0, -1), tip=(16, 4), rows=("grass",)),
    "hollow_star":       dict(paint=si.hollow_star, travel=None, tip=(16, 16), rows=("darkness",)),
    "starfall":          dict(paint=lambda: si.starfall(sky=False), travel=(0.75, 1), tip=(21, 22), rows=("light",)),
    "familiar":          dict(paint=image(lambda: familiar.portrait()), travel=(1, 0), tip=(27, 19), rows=()),
    # The Familiar is taken in a form too (a dog, a crow or a cat), each its own subject, drawn by
    # tools/art/familiar.py's portraits with the calm eye (familiar.calm_eye).
    "familiar_dog":      dict(paint=image(lambda: familiar.portrait()), travel=(1, 0), tip=(27, 19), rows=()),
    "familiar_crow":     dict(paint=image(lambda: familiar.crow_portrait()), travel=(1, 0), tip=(30, 17), rows=()),
    "familiar_cat":      dict(paint=image(lambda: familiar.cat_portrait()), travel=None, tip=(16, 19), rows=()),
    "stormsweep":        dict(paint=lambda: si.stormsweep(sky=False), travel=None, tip=(25, 20), rows=("lightning",)),
    "sleet_runes":       dict(paint=lambda: si.sleet_runes(sky=False), travel=(0.83, -0.56), tip=(22, 11), rows=("ice", "wind")),
}

def subject_of(card):
    """What a card's icon is drawn from: its spell's FORM, for a spell taken in one (the catalog's
    "variant", written by _AscensionAudit), else its spell."""
    return card.get("variant") or card["spell"]


def weapon(kind, box=24.0, angle=-50.0):
    """A Spiritual Weapon subject: one weapon from spirit_weapons.py, fitted to `box` and centred, with
    no night of its own - the frame lays the milestone's."""
    @image
    def f():
        c = _Real(CELL, CELL, None)
        over(c, sw.group([(kind, angle, (16.0, 16.0), sw.ICON_SCALE[kind])], box=box))
        return c
    return f


for _key, _kind in (("spiritual_weapon_axe", "axe"), ("spiritual_weapon_hammer", "hammer"), ("spiritual_weapon_sword", "sword")):
    SPELLS[_key]["paint"] = weapon(_kind)

# The spell's icon as the game ships it, for the difference check and the sheet: the four painters that
# draw their own night are drawn with it here.
BASE = {
    "spiritual_weapon_axe": lambda: sw.icon("axe"), "spiritual_weapon_hammer": lambda: sw.icon("hammer"),
    "spiritual_weapon_sword": lambda: sw.icon("sword"),
    "starfall": si.starfall, "familiar": si.familiar_icon, "stormsweep": si.stormsweep,
    "familiar_dog": si.familiar_dog_icon, "familiar_crow": si.familiar_crow_icon, "familiar_cat": si.familiar_cat_icon,
    "sleet_runes": si.sleet_runes,
}


class Spec(object):
    """How one evolution's icon is built. derive() fills it from the card's data; OVERRIDES edit it."""

    def __init__(self):
        self.back = None         # the night's element
        self.grain = None        # the night's grain: None means "if an Ascension or above"
        self.accent = None       # an element's accent over the subject (a card that gains one)
        self.heavy = False       # a denser accent: Ascensions and the Apotheosis
        self.recolour = None     # the element the subject is re-lit in (an element path)
        self.keep_core = False   # ...leaving its white-hot core and hot stop alone
        self.place = Place()     # the subject
        self.copies = []         # Places of more of the subject, drawn behind it
        self.under = []          # f(c, ctx): on the night, under the subject
        self.over = []           # f(c, ctx): over the subject
        self.glow = None         # a colour to glow round the subject; None: the rim's, if SPELLS says dark
        self.subject = None      # a hand-drawn subject (a painter, or an image()), replacing the spell's
        self.rows = None         # the rows recolour() re-lights, if not the spell's
        self.ramp = None         # the verbs' element (default: the subject's own after any re-light)
        self.why = ""            # one line for the sheet


ANCHORS = {}


def anchor_of(fn):
    """The point a subject is scaled and turned about: the centre of what its painter draws."""
    key = id(fn)
    if key not in ANCHORS:
        cv = fn() if getattr(fn, "image", False) else paint(fn)
        x0, y0, x1, y1 = bbox(cv)
        ANCHORS[key] = ((x0 + x1) / 2.0, (y0 + y1) / 2.0)
    return ANCHORS[key]


class Ctx(object):
    """What a verb may read: the card, the spec, the subject as placed, its tip and travel, the ramps."""

    def __init__(self, card, spec, subject, info):
        self.card = card
        self.spec = spec
        self.subject = subject
        self.r = DISC_R[card["milestone"]]
        self.box = bbox(subject)
        self.centre = ((self.box[0] + self.box[2]) / 2.0, (self.box[1] + self.box[3]) / 2.0)
        fn = spec.subject or info["paint"]
        m = spec.place.mapper(anchor_of(fn))
        self.tip = m(*info["tip"])
        travel = info["travel"]
        if travel:
            o = m(0.0, 0.0)
            t = m(travel[0], travel[1])
            travel = (t[0] - o[0], t[1] - o[1])
        self.travel = travel
        self.ramp = E[spec.ramp or spec.recolour or primary(card)]
        self.back = E[spec.back]


def primary(card):
    return [e.lower() for e in card["spell_elements"]][0]


def draw_subject(spec, info, place):
    fn = spec.subject or info["paint"]
    anchor = anchor_of(fn)
    if getattr(fn, "image", False):
        cv = place_image(fn(), place, anchor)
    else:
        cv = paint(fn, place, anchor)
    if spec.recolour:
        rows = spec.rows if spec.rows is not None else info["rows"]
        cv = recolour(cv, set(rows), spec.recolour, spec.keep_core)
    if place.darken:
        cv = darken(cv, place.darken)
    return cv


def compose(card, spec):
    info = SPELLS[subject_of(card)]
    tier = card["milestone"]
    r = DISC_R[tier]
    c = _Real(CELL, CELL, None)

    # 1. The Apotheosis's burst, behind everything.
    if tier >= 9:
        sunburst(c, E[spec.back])

    # 2. The night, and (an Ascension's, an Apotheosis's) its grain.
    night(c, spec.back, r)
    if spec.grain or (spec.grain is None and tier >= 8):
        g = _Real(CELL, CELL, None)
        GRAINS[spec.back](g)
        over(c, g, inside=in_disc(r - 2.5))

    # 3. The rim of a mutation; the gold of an Ascension or an Apotheosis.
    if tier == 4:
        rim(c, spec.back, r)
    else:
        gold_ring(c, r + (0.5 if tier >= 9 else 0.0))

    # 4. The subject and its copies, and what goes on the night under them.
    main = draw_subject(spec, info, spec.place)
    copies = [draw_subject(spec, info, p) for p in spec.copies]
    ctx = Ctx(card, spec, main, info)
    for f in spec.under:
        f(c, ctx)
    # A thin or dark subject (a steel splinter on a steel night, a vine on a green one) is lifted off
    # its night by a pixel of the rim's light round it; a bright one needs none.
    glow = spec.glow
    if glow is None and info.get("dark"):
        glow = BACK[spec.back]["rim"]
    for cv in copies + [main]:
        if glow:
            glow_round(c, cv, glow)
        over(c, cv)

    # 5. The element's accent, and the verbs over the subject.
    if spec.accent:
        ACCENTS[spec.accent](c, r, spec.heavy)
    for f in spec.over:
        f(c, ctx)

    # 6. The milestone's furniture, on top of everything.
    if tier == 8:
        gems(c, r, E[spec.back])
    elif tier >= 9:
        crown(c, E[spec.back])
    return c


# =============================================================================================
# Bound verbs, for the spec table
# =============================================================================================

def v_speed(n=4, length=9):
    """Speed lines on a travelling subject; circling arrows on one that stays put."""
    def f(c, x):
        if x.travel:
            m_speed(c, x.ramp, x.travel, x.box, n, length)
        else:
            m_cycle(c, x.ramp, (C0, C0), x.r - 2.5)
    return f


def v_cycle():
    return lambda c, x: m_cycle(c, x.ramp, (C0, C0), x.r - 2.5)


def v_ghosts(steps=(4, 8)):
    return lambda c, x: m_ghosts(c, x.subject, x.travel, [x.ramp[3], BACK[x.spec.back]["rim"]], steps)


def v_rays(r0=8.0, n=12):
    return lambda c, x: m_rays(c, x.ramp, x.centre, r0, x.r - 0.5, n)


def v_shock(rings=2):
    return lambda c, x: m_shockwave(c, x.ramp, (C0, C0), x.r - 2.0, rings)


def v_vortex(arms=3, turns=3.2):
    return lambda c, x: m_vortex(c, x.ramp, (C0, C0), x.r - 1.5, arms, turns)


def v_pool(cy=25.5, rx=11.5, ry=3.4, element=None):
    return lambda c, x: m_pool(c, E[element] if element else x.ramp, cy, rx, ry)


def v_blast(r=5.5, at=None, element=None):
    return lambda c, x: m_blast(c, E[element] if element else x.ramp, (at or x.tip)[0], (at or x.tip)[1], r)


def v_glint(arm=4, at=None, element="light"):
    return lambda c, x: m_glint(c, (at or x.tip)[0], (at or x.tip)[1], arm, E[element])


def v_chevrons(gap=10, vertical=False):
    return lambda c, x: m_chevrons(c, x.ramp, x.centre, gap, vertical)


def v_pierce(marks=2):
    return lambda c, x: m_pierce(c, x.ramp, x.tip, x.travel or (1, -1), marks)


def pierced(sp, c, marks=2, s=0.74):
    """A piercing card: the subject drawn back from its travel to leave room ahead of its tip for
    the marks it goes on through."""
    tx, ty = _unit(info_of(c)["travel"] or (1, -1))
    sp.place = Place(s, dx=-tx * 6.0, dy=-ty * 6.0)
    U(sp, v_pierce(marks))


def ring_of(n, s, radius, pointing, start=-90.0):
    """n of a subject round the centre, each turned to point outward - a wheel of blades. `pointing`
    is the angle (degrees, screen) the subject points along as its painter draws it."""
    out = []
    for k in range(n):
        a = start + 360.0 * k / n
        t = math.radians(a)
        out.append(Place(s, rot=a - pointing, at=(C0 + math.cos(t) * radius, C0 + math.sin(t) * radius),
                         darken=0))
    return out


def v_fork(spread=0.75, length=8):
    return lambda c, x: m_fork(c, x.ramp, x.tip, x.travel or (1, 0), spread, length)


def U(sp, *fs):
    sp.under.extend(fs)


def O(sp, *fs):
    sp.over.extend(fs)


# =============================================================================================
# The default spec, from the card's own data
# =============================================================================================

def volley(n, travel, front=0.72, back=0.58, spread=8.0):
    """Places for n of a subject: abreast across its travel with the last (the front one) a step ahead;
    a triangle for a subject that does not travel. Absolute, about the cell's centre."""
    if travel:
        tx, ty = _unit(travel)
        px, py = -ty, tx
        if n == 2:
            return [Place(back + 0.04, at=(C0 - px * 5 - tx * 2, C0 - py * 5 - ty * 2), darken=1),
                    Place(front, at=(C0 + px * 4 + tx * 2, C0 + py * 4 + ty * 2))]
        return [Place(back, at=(C0 - px * spread - tx * 3, C0 - py * spread - ty * 3), darken=1),
                Place(back, at=(C0 + px * spread - tx * 3, C0 + py * spread - ty * 3), darken=1),
                Place(front, at=(C0 + tx * 2, C0 + ty * 2))]
    if n == 2:
        return [Place(back + 0.04, at=(C0 - 5, C0 - 4), darken=1), Place(front, at=(C0 + 4, C0 + 4))]
    return [Place(back, at=(C0 - 7, C0 - 6), darken=1), Place(back, at=(C0 + 7, C0 - 6), darken=1),
            Place(front - 0.04, at=(C0, C0 + 5))]


def set_volley(sp, places):
    sp.copies = places[:-1]
    sp.place = places[-1]


def about(pivot, angle, anchor, s=1.0, darken=0):
    """The subject turned by `angle` degrees about `pivot` (its emitter, its nock), not its centre."""
    ax, ay = anchor
    ca, sa = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    ox, oy = (ax - pivot[0]) * s, (ay - pivot[1]) * s
    return Place(s, rot=angle, at=(pivot[0] + ox * ca - oy * sa, pivot[1] + ox * sa + oy * ca), darken=darken)


def derive(card):
    """The default spec, from the card's own data: what a card nobody has hand-drawn looks like."""
    info = SPELLS[subject_of(card)]
    els = [e.lower() for e in card["spell_elements"]]
    gains = [e.lower() for (e, n) in sorted(card["gains"].items()) if n > 0]
    gain = gains[0] if gains else None
    tier = card["milestone"]
    sp = Spec()
    sp.back = gain or els[0]
    sp.accent = gain
    sp.heavy = tier >= 8
    if gain and (gain not in els or card.get("replaces")):
        sp.recolour = gain
    why = []
    if gain:
        why.append(("into " if sp.recolour else "deeper ") + gain)

    proj = card["projectiles"] + card["branches"]
    big = max(card["area"], card["scale"])
    fast = card["cooldown"] <= 0.75 or card["speed"] >= 1.2
    eff = card["effect"]
    travel = info["travel"]

    if proj >= 1:
        set_volley(sp, volley(3 if proj >= 2 else 2, travel))
        why.append("more of it")
    elif big >= 2.0:
        sp.place = Place(1.22)
        U(sp, v_shock())
        why.append("colossal")
    elif big >= 1.25:
        sp.place = Place(1.12)
        U(sp, v_rays())
        why.append("bigger")

    if fast:
        U(sp, v_speed())
        if travel and proj < 1:
            U(sp, v_ghosts())
        why.append("faster")

    if eff == "VortexPull":
        U(sp, v_vortex())
        why.append("pulls")
    elif eff == "Freeze":
        O(sp, lambda c, x: m_freeze(c, x.centre, 9.0))
        why.append("freezes")
    elif eff == "RootDuration":
        O(sp, lambda c, x: m_roots(c))
        why.append("holds")
    elif eff in ("ZoneDuration", "DotDamage"):
        U(sp, v_pool())
        why.append("lingers")
    elif eff == "Burn":
        U(sp, lambda c, x: m_magma(c))
        why.append("burns the ground")
    elif eff == "Lifesteal":
        O(sp, lambda c, x: m_heart(c))
        why.append("heals")
    elif eff in ("CritChance", "CritDamage"):
        O(sp, v_glint())
        why.append("crits")
    elif eff == "ExplosionOnHit":
        O(sp, v_blast())
        why.append("bursts")
    elif eff == "SplitOnHit":
        O(sp, v_fork())
        why.append("splits")

    if card["pierce"] >= 2 and travel and proj < 1:
        if sp.place.sx == 1.0:
            pierced(sp, card)
        else:
            U(sp, v_pierce())
        why.append("pierces")
    if card["knockback"] > 0:
        O(sp, v_chevrons(11))
        why.append("throws back")
    if not sp.under and not sp.over and not sp.copies and sp.place.sx == 1.0 and not sp.recolour:
        U(sp, v_rays())
        why.append("harder")
    sp.why = ", ".join(why)
    return sp


# =============================================================================================
# Hand-drawn subjects: the cards that change what the thing IS
# =============================================================================================

def _cv():
    # Looked up at call time, so paint() can map the canvas a hand-drawn subject makes.
    return raster.Canvas(CELL, CELL, None)


def zig(c, a, b, tone, jag=1.3, steps=4, core=None):
    """A jagged stroke from a to b: lightning, the way the painters draw it."""
    (x0, y0), (x1, y1) = a, b
    px, py = -(y1 - y0), (x1 - x0)
    n = math.hypot(px, py) or 1.0
    px, py = px / n, py / n
    prev = a
    for i in range(1, steps + 1):
        t = i / float(steps)
        j = ((jag if i % 2 else -jag) if i < steps else 0.0)
        p = (x0 + (x1 - x0) * t + px * j, y0 + (y1 - y0) * t + py * j)
        c.line(prev[0], prev[1], p[0], p[1], tone)
        if core and 0 < i < steps:
            c.set(p[0], p[1], core)
        prev = p


@image
def hound_head():
    return familiar.portrait()


@image
def rime_head():
    return familiar.portrait(familiar.RIME)


@image
def great_hound():
    """The Great Hound: the familiar's head over the wizard's mantle - indigo wool, a gold edge, an
    arcane clasp - which is what its sprite wears (familiar.py's GREAT skin)."""
    c = familiar.portrait(familiar.GREAT)
    w = M["wool"]
    cape = [(0, 31), (0, 26), (4, 22), (9, 20.5), (14, 22), (18, 26), (20, 31)]
    c.poly_shade(cape, [w[1], w[1], w[2], w[3], w[4]], bias=0.05)
    for (x0, y0, x1, y1) in ((5, 24, 3, 31), (10, 23, 9, 31), (15, 25, 16, 31)):
        c.line(x0, y0, x1, y1, w[4])
        c.line(x0 + 1, y0, x1 + 1, y1, w[1])
    edge = [(0, 26), (4, 22), (9, 20.5), (14, 22), (18, 26), (20, 31)]
    for i in range(len(edge) - 1):
        c.line(edge[i][0], edge[i][1], edge[i + 1][0], edge[i + 1][1], GOLD[1])
    c.disc(9.5, 21.5, 1.8, 1.8, GOLD[2])
    c.set(9, 21, E["arcane"][1])
    c.set(10, 21, E["arcane"][0])
    c.set(8, 20, GOLD[0])
    return c


def head_at(skin, hat, s, at, steps=0, mirror=False):
    cv = place_image(familiar.portrait(skin, hat=hat), Place(s, at=at, mirror=mirror), anchor_of(hound_head))
    return darken(cv, steps) if steps else cv


@image
def littermates():
    """Two hounds: the second a step behind the first and a stop darker."""
    c = _Real(CELL, CELL, None)
    over(c, head_at(familiar.HOUND, True, 0.72, (10.0, 10.5), 1))
    over(c, head_at(familiar.HOUND, True, 0.82, (20.0, 19.5)))
    return c


@image
def cerberus_heads():
    """Three heads fanned from one neck, the outer two looking out either way and the hat on the
    middle one, which stands above them - the hat goes on one head of three, as on the sprite."""
    c = _Real(CELL, CELL, None)
    over(c, head_at(familiar.CERB, False, 0.6, (8.0, 20.0), 1, mirror=True))
    over(c, head_at(familiar.CERB, False, 0.6, (24.0, 20.0), 1))
    over(c, head_at(familiar.CERB, True, 0.72, (16.0, 12.5)))
    return c


@image
def griffin_form():
    """The Griffin, the familiar's level 8 form, drawn by familiar.griffin at its own 64 cell and voted
    down: eagle head and forequarters in gold, the wizard's indigo wings up, the hat on. Framed on the
    head and the wings, as the hound's icon is framed on its head."""
    g = familiar.griffin(familiar.GRIFF, familiar.GRIFFIN_ANIMS[0][1][0]).render(finish=True)
    for y in range(48, g.h):
        for x in range(g.w):
            g.px[x, y] = (0, 0, 0, 0)
    return place_image(g, Place(0.55, at=(C0 - 0.5, C0 + 0.5)), (34.0, 22.0))


def lattice():
    """The Lattice: everything the chain struck, still linked, the web discharging along its edges."""
    c = _cv()
    core, hot, mid, edge = E["lightning"]
    nodes = [(7, 10), (17, 5), (26, 12), (10, 21), (22, 23), (16, 14), (5, 16)]
    links = [(0, 1), (1, 2), (0, 5), (1, 5), (2, 5), (3, 5), (5, 4), (2, 4), (3, 4), (0, 6), (6, 3)]
    for (a, b) in links:
        zig(c, nodes[a], nodes[b], edge, 1.0, 3)
    for (a, b) in links[::2]:
        zig(c, nodes[a], nodes[b], hot, 1.0, 3, core)
    for (x, y) in nodes:
        c.disc(x, y, 2.1, 2.1, mid)
        c.disc(x, y, 1.2, 1.2, hot)
        c.set(x, y, core)
    return c


def constellation():
    """The Constellation: fallen stars still lit where they landed, joined by lines of light."""
    c = _cv()
    core, hot, mid, edge = E["light"]
    stars = [(6, 20, 2), (11, 10, 3), (19, 15, 2), (25, 7, 2), (24, 24, 3), (14, 26, 1)]
    for (a, b) in ((0, 1), (1, 2), (2, 3), (2, 4), (4, 5), (5, 0)):
        (x0, y0, _), (x1, y1, _) = stars[a], stars[b]
        n = int(math.hypot(x1 - x0, y1 - y0))
        for i in range(n):
            t = i / float(n)
            c.set(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, mid if i % 3 else hot)
    for (x, y, arm) in stars:
        m_glint(c, x, y, arm, E["light"])
    return c


def daybreak():
    """Daybreak Avatar: the flare become the sun coming up over the clearing it fills."""
    c = _cv()
    core, hot, mid, edge = E["light"]
    hz = 21
    for k in range(9):
        a = math.pi + math.pi * (k + 0.5) / 9.0
        long_ray = k % 2 == 0
        r1 = 15.0 if long_ray else 12.0
        c.line(16 + math.cos(a) * 10, hz + math.sin(a) * 10, 16 + math.cos(a) * r1, hz + math.sin(a) * r1,
               hot if long_ray else mid)
    for (r, tone) in ((9.0, edge), (7.5, mid), (5.5, hot), (3.0, core)):
        for y in range(int(hz - r), hz + 1):
            for x in range(int(16 - r), int(16 + r) + 1):
                if (x - 15.5) ** 2 + (y - hz) ** 2 <= r * r:
                    c.set(x, y, tone)
    c.hline(1, 30, hz + 1, hot)
    c.hline(2, 29, hz + 2, mid)
    for (x0, x1, y) in ((5, 27, hz + 4), (9, 22, hz + 6)):
        c.hline(x0, x1, y, edge)
    return c


def doomcap():
    """Doomcap: one titanic mushroom in place of the scatter, cracked with the light about to burst
    out of it - spore_mushrooms.py's own stem and cap."""
    c = _cv()
    v = spores.VARIANTS["poison"]
    spores.stem(c, 16, 29, 17, 3.0, ring=22)
    spores.cap(c, 16, 12, 13.0, 9.0, v, glow=0.9,
               spots=[(-0.45, -0.35, 1.3), (0.3, -0.55, 1.1), (0.05, -0.05, 0.9), (0.55, -0.1, 0.8)], cracks=3)
    c.hline(9, 23, 30, OCC)
    return c


def small_cap(c, x, base, h, rx, variant="poison"):
    v = spores.VARIANTS[variant]
    spores.stem(c, x, base, base - h, 1.2)
    spores.cap(c, x, base - h - 1, rx, rx * 0.7, v, glow=0.5, spots=[(-0.2, -0.3, 0.6)])


def iron_comet():
    """Iron Comet: the fireball forged round an iron core - a ball of steel with the fire streaming
    off behind it."""
    c = _cv()
    f = E["fire"]
    s = M["steel"]
    c.poly([(13, 23), (2, 7), (5, 3), (22, 13)], f[3])
    c.poly([(15, 22), (5, 7), (8, 5), (21, 15)], f[2])
    c.poly([(17, 21), (9, 10), (11, 9), (20, 17)], f[1])
    c.disc(20.5, 20.5, 7.0, 7.0, s[3])
    c.disc(19.5, 19.5, 5.6, 5.6, s[2])
    c.disc(18.5, 18.5, 3.4, 3.4, s[1])
    c.disc(17.5, 17.5, 1.5, 1.5, s[0])
    for d in range(-20, 120, 9):
        a = math.radians(d)
        c.set(20.5 + math.cos(a) * 6.6, 20.5 + math.sin(a) * 6.6, f[2] if d % 2 else f[1])
    c.line(24, 23, 22, 25, s[4])
    return c


def event_horizon():
    """Event Horizon: the blast collapsed into a singularity - a hole with its accretion disc."""
    c = _cv()
    a = E["arcane"]
    c.disc(16, 16, 14.0, 5.5, a[3])
    c.disc(16, 16, 12.0, 4.3, a[2])
    c.disc(16, 16, 9.5, 3.0, a[1])
    for d in range(195, 346, 4):
        t = math.radians(d)
        c.set(16 + math.cos(t) * 8.5, 15 + math.sin(t) * 8.5, a[2])
        c.set(16 + math.cos(t) * 7.5, 15 + math.sin(t) * 7.5, a[1])
    c.disc(16, 15, 6.5, 6.5, a[0])
    c.disc(16, 15, 5.5, 5.5, OCC)
    c.hline(4, 28, 17, a[2])
    c.hline(7, 25, 18, a[1])
    c.hline(10, 22, 17, a[0])
    return c


def singularity_core():
    """Singularity Core: the missile become a gravitational orb, the dart's trail still behind it."""
    c = _cv()
    a = E["arcane"]
    d = E["darkness"]
    for (y, x0) in ((14, 2), (17, 4), (20, 7)):
        c.hline(x0, 13, y, a[3])
        c.hline(x0 + 5, 13, y, a[2])
    c.disc(19, 16, 9.0, 9.0, d[3])
    c.disc(19, 16, 7.5, 7.5, a[3])
    c.ring(19, 16, 6.5, 6.5, a[1])
    c.disc(19, 16, 5.5, 5.5, OCC)
    c.set(16, 13, a[0])
    c.set(17, 13, a[1])
    return c


def annihilation_beam():
    """Annihilation Beam: the lance become a death ray, twice as long and twice as wide."""
    c = _cv()
    d = E["darkness"]
    a = E["arcane"]

    def quad(w):
        k = w * 0.707
        return [(1 - k, 31 - k), (31 - k, 1 - k), (31 + k, 1 + k), (1 + k, 31 + k)]
    for (w, tone) in ((6.0, d[3]), (4.6, a[3]), (3.2, a[2]), (1.8, a[1]), (0.7, a[0])):
        c.poly(quad(w), tone)
    c.disc(4, 28, 4.0, 4.0, a[2])
    c.disc(4, 28, 2.2, 2.2, a[0])
    return c


def cardinal_storm():
    """Cardinal Storm: the sweep going all four ways at once round the caster's sigil."""
    c = _cv()
    core, hot, mid, edge = E["lightning"]
    for q in range(4):
        a0 = math.radians(q * 90 - 30)
        a1 = math.radians(q * 90 + 30)
        pts = []
        for i in range(9):
            t = i / 8.0
            r = 12.5 + ((1.0 if i % 2 else -1.0) if 0 < i < 8 else 0.0)
            a = a0 + (a1 - a0) * t
            pts.append((C0 + math.cos(a) * r, C0 + math.sin(a) * r))
        for i in range(len(pts) - 1):
            c.line(pts[i][0] + 1, pts[i][1], pts[i + 1][0] + 1, pts[i + 1][1], edge)
        for i in range(len(pts) - 1):
            c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], hot)
        for i in range(2, len(pts) - 3):
            c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], core)
        mid_a = (a0 + a1) / 2.0
        for r in (5.0, 7.0, 9.0):
            c.set(C0 + math.cos(mid_a) * r, C0 + math.sin(mid_a) * r, mid)
    c.ring(C0, C0, 3.2, 3.2, mid)
    c.disc(C0, C0, 1.6, 1.6, core)
    return c


def typhoon_core():
    """Typhoon Core: the arc grown into a hurricane that never stops, its eye on the caster."""
    c = _cv()
    core, hot, mid, edge = E["wind"]
    for arm in range(4):
        base = arm * TAU / 4.0
        for i in range(70):
            t = i / 69.0
            r = 14.0 - t * 10.0
            a = base + t * 3.6
            tone = edge if t < 0.25 else (mid if t < 0.7 else hot)
            c.set(C0 + math.cos(a) * r, C0 + math.sin(a) * r, tone)
            if t < 0.6:
                c.set(C0 + math.cos(a) * (r - 1), C0 + math.sin(a) * (r - 1), edge if t < 0.3 else mid)
    c.disc(C0, C0, 3.2, 3.2, M["lichen"][4])
    c.ring(C0, C0, 3.5, 3.5, core)
    return c


def spirit_mirror():
    """The Spirit Mirror: John Dee's obsidian scrying glass, the well turning in it."""
    c = _cv()
    d = E["darkness"]
    c.rect(14, 24, 17, 30, GOLD[2])
    c.vline(14, 24, 30, GOLD[1])
    c.vline(17, 24, 30, GOLD[3])
    c.hline(13, 18, 30, GOLD[3])
    c.disc(15.5, 13, 11.2, 11.2, GOLD[2])
    c.disc(15.5, 13, 10.0, 10.0, GOLD[3])
    for deg in range(180, 300, 6):
        a = math.radians(deg)
        c.set(15.5 + math.cos(a) * 10.7, 13 + math.sin(a) * 10.7, GOLD[0])
    c.disc(15.5, 13, 9.0, 9.0, OCC)
    for arm in range(3):
        base = arm * TAU / 3.0 + 0.6
        for i in range(36):
            t = i / 35.0
            r = 7.5 - t * 5.0
            a = base + t * 3.2
            c.set(15.5 + math.cos(a) * r, 13 + math.sin(a) * r, d[2] if t < 0.5 else d[1])
    c.disc(15.5, 13, 1.6, 1.6, OCC)
    c.set(11, 8, d[0])
    c.set(12, 7, d[1])
    return c


def elder_maw(c, x):
    """Elder God Maw: the rift become a mouth, ringed with teeth and eyes."""
    bone = M["skin"]
    p = E["poison"]
    cx, cy = 16.0, 21.5
    c.disc(cx, cy, 11.0, 5.8, E["earth"][3])
    c.disc(cx, cy, 9.5, 4.6, OCC)
    for k in range(16):
        a = TAU * k / 16.0
        x0, y0 = cx + math.cos(a) * 9.5, cy + math.sin(a) * 4.6
        x1, y1 = cx + math.cos(a) * 7.0, cy + math.sin(a) * 2.9
        c.line(x0, y0, x1, y1, bone[1] if math.sin(a) < 0 else bone[2])
        c.set(x1, y1, bone[0])
    for (ex, ey) in ((5, 14), (11, 11), (21, 11), (27, 14), (16, 9)):
        c.disc(ex, ey, 1.8, 1.3, p[2])
        c.set(ex, ey, OCC)
        c.set(ex - 1, ey - 1, p[0])


def crater(c, x, y, rx):
    f = E["fire"]
    c.disc(x, y, rx, rx * 0.42, OCC)
    c.disc(x, y - 0.3, rx - 1.2, rx * 0.42 - 0.8, f[3])
    c.disc(x - 0.5, y - 0.5, rx * 0.5, rx * 0.2, f[2])
    for k in range(3):
        fx = x - rx * 0.6 + k * rx * 0.6
        h = 3 + (k % 2) * 2
        c.poly([(fx - 1.3, y - 1), (fx + 0.4, y - 1 - h), (fx + 1.4, y - 1)], f[2])
        c.set(fx, y - 2, f[1])


# =============================================================================================
# The cards, one by one. Each override is f(spec, card) and edits what derive() made.
# =============================================================================================

OVERRIDES = {}


def card(*ids):
    def reg(fn):
        for i in ids:
            OVERRIDES[i] = fn
        return fn
    return reg


def info_of(c):
    return SPELLS[subject_of(c)]


def bigger(sp, s=1.12, rays=True):
    sp.place = Place(s)
    if rays:
        U(sp, v_rays())


def colossal(sp, s=1.24, shock=True):
    sp.place = Place(s)
    if shock:
        U(sp, v_shock())


def faster(sp, c, ghosts=True, n=4, s=0.8):
    """A faster card: the subject drawn smaller and a step ahead along its travel, with speed lines
    and afterimages behind it - room the lines need, or a big subject simply covers them. A subject
    that does not travel gets the circling arrows instead."""
    travel = info_of(c)["travel"]
    if travel:
        tx, ty = _unit(travel)
        sp.place = Place(s, dx=tx * 4.0, dy=ty * 4.0)
    else:
        sp.place = Place(s)
    U(sp, v_speed(n))
    if ghosts and travel:
        U(sp, v_ghosts())


def reset(sp):
    sp.place = Place()
    sp.copies = []
    sp.under = []
    sp.over = []


# --- Aegis Ward ------------------------------------------------------------------------------------

@card("aegis_ward_reflective")
def _(sp, c):
    """Reflective Plating: what the shield absorbs goes back - a dark bolt in, a bright one out."""
    reset(sp)
    sp.back = "light"

    def bounce(cv, x):
        d, l = E["darkness"], E["light"]
        cv.line(2, 6, 12, 13, d[2])
        cv.line(3, 6, 13, 13, d[3])
        for (px, py) in ((12, 13), (13, 14)):
            cv.set(px, py, l[0])
        cv.line(17, 13, 30, 6, l[1])
        cv.line(17, 14, 30, 7, l[2])
        cv.set(30, 6, l[0])
        m_glint(cv, 16, 13, 2, l)
    O(sp, bounce)


@card("aegis_ward_quick_bastion")
def _(sp, c):
    """Quick Bastion: the shield recharging sooner - arrows going round it."""
    reset(sp)
    sp.place = Place(0.86)
    U(sp, lambda cv, x: m_cycle(cv, E["light"], (C0, C0), 13.0))


@card("aegis_ward_fortified")
def _(sp, c):
    """Fortified Aegis: the ward forged in metal - a bigger shield, banded and riveted."""
    reset(sp)
    sp.place = Place(1.12)
    sp.ramp = "metal"
    U(sp, v_rays(9.0, 8))

    def banded(cv, x):
        m = E["metal"]
        for (px, py) in ((9, 9), (22, 9), (8, 19), (23, 19), (15.5, 26)):
            cv.set(px, py, m[0])
            cv.set(px + 1, py + 1, m[3])
        cv.hline(7, 24, 22, m[3])
        cv.hline(7, 24, 21, m[1])
    O(sp, banded)


@card("aegis_ward_sun_aegis")
def _(sp, c):
    """Sun Aegis: a breaking shield flashes - the shield on a sun."""
    reset(sp)
    sp.place = Place(0.92)
    U(sp, lambda cv, x: m_rays(cv, E["light"], (C0, C0 - 1), 6.0, 14.5, 16))


@card("aegis_ward_crystal_spikes")
def _(sp, c):
    """Thorn Crystal Shell: a breaking shield bursts into crystal shards."""
    reset(sp)
    sp.back = "arcane"
    sp.place = Place(0.84)

    def shards(cv, x):
        a = E["arcane"]
        for deg in range(0, 360, 45):
            t = math.radians(deg + 22)
            bx, by = C0 + math.cos(t) * 9.5, C0 + math.sin(t) * 9.5
            ex, ey = C0 + math.cos(t) * 14.0, C0 + math.sin(t) * 14.0
            px, py = -math.sin(t) * 1.5, math.cos(t) * 1.5
            cv.poly([(bx - px, by - py), (ex, ey), (bx + px, by + py)], a[3])
            cv.line(bx, by, ex, ey, a[1])
            cv.set(ex, ey, a[0])
    U(sp, shards)


# --- Arcane Explosion ------------------------------------------------------------------------------

@card("arcane_explosion_dual_shockwave")
def _(sp, c):
    """Dual Shockwave: two rings, and the crowd thrown clear of them."""
    reset(sp)
    sp.place = Place(0.8)
    U(sp, v_shock(2))
    O(sp, v_chevrons(10))


@card("arcane_explosion_static_nova")
def _(sp, c):
    """Static Nova: the blast crackling - re-lit in Lightning, sparks thrown off it."""
    pass


@card("arcane_explosion_lingering_rift")
def _(sp, c):
    """Lingering Rift: a rift left on the ground under the blast."""
    reset(sp)
    sp.place = Place(0.72, dy=-4)

    def rift(cv, x):
        a = E["arcane"]
        cv.disc(C0, 24.5, 12.5, 4.2, a[3])
        cv.disc(C0, 24.5, 10.0, 2.8, OCC)
        zig(cv, (6, 24.5), (26, 24.5), a[1], 1.0, 6, a[0])
    U(sp, rift)


@card("arcane_explosion_supernova")
def _(sp, c):
    """Supernova: the blast become a star going off, burning through Arcane into Fire."""
    reset(sp)
    sp.place = Place(1.18)
    sp.ramp = "fire"
    U(sp, lambda cv, x: m_rays(cv, E["fire"], (C0, C0), 7.0, 14.5, 16))
    O(sp, lambda cv, x: m_blast(cv, E["fire"], C0, C0, 6.0))


@card("arcane_explosion_event_horizon")
def _(sp, c):
    """Event Horizon: a singularity that pulls in and freezes."""
    reset(sp)
    sp.subject = event_horizon
    U(sp, v_vortex(3, 2.6))
    O(sp, lambda cv, x: m_freeze(cv, (C0, C0 + 1), 10.5))


# --- Black Tentacles -------------------------------------------------------------------------------

@card("tentacles_constriction")
def _(sp, c):
    """Crushing Grip: the arms closed round what they caught, and rooted it."""
    reset(sp)

    def grip(cv, x):
        e = E["earth"]
        p = E["poison"]
        cv.disc(17, 9, 3.2, 3.2, M["linen"][2])
        cv.set(16, 8, M["linen"][0])
        for (a0, a1) in ((150, 390), (330, 560)):
            for d in range(a0, a1, 12):
                t = math.radians(d)
                cv.set(17 + math.cos(t) * 4.2, 9 + math.sin(t) * 3.6, p[2] if d % 24 else p[1])
        m_roots(cv)
    O(sp, grip)


@card("tentacles_abyssal_spread")
def _(sp, c):
    """Abyssal Spread: a wider rift with more arms."""
    reset(sp)
    sp.place = Place(1.3, sy=0.9, dy=2)
    O(sp, lambda cv, x: m_chevrons(cv, E["poison"], (C0, 24.0), 12))


@card("tentacles_soul_drain")
def _(sp, c):
    """Soul Drain: the arms drink, and it comes back to you as health."""
    reset(sp)
    sp.place = Place(0.86, dy=3)

    def drain(cv, x):
        e = M["ember"]
        for (px, py) in ((9, 11), (13, 8), (18, 6), (22, 9)):
            cv.set(px, py, e[1])
            cv.set(px, py + 1, e[2])
        m_heart(cv, 13, 1, drops=False)
    O(sp, drain)


@card("tentacles_elder_god_maw")
def _(sp, c):
    """Elder God Maw: the rift opens into a maw ringed with eyes, dragging the crowd to its teeth."""
    reset(sp)
    U(sp, v_vortex(3, 2.8))
    O(sp, elder_maw)


@card("tentacles_shadow_forest")
def _(sp, c):
    """Shadow Forest: rifts over a whole field, a forest of arms."""
    reset(sp)
    set_volley(sp, [Place(0.58, at=(9.5, 12.5), darken=1), Place(0.58, at=(22.5, 12.0), darken=1),
                    Place(0.72, at=(C0, 19.5))])


# --- Bramble Seed ----------------------------------------------------------------------------------

@card("bramble_seed_amplified_impact")
def _(sp, c):
    reset(sp)
    bigger(sp, 1.14)


@card("bramble_seed_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: a shorter cooldown and a faster grower - the arrows going round it."""
    reset(sp)
    sp.place = Place(0.82, dy=1)
    U(sp, v_cycle())


@card("bramble_seed_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.25)


@card("bramble_seed_infinite_horizon")
def _(sp, c):
    reset(sp)
    set_volley(sp, [Place(0.6, at=(8.5, 15.5), darken=1), Place(0.6, at=(23.5, 15.5), darken=1),
                    Place(0.74, at=(C0, 17.0))])


@card("bramble_seed_wildwood")
def _(sp, c):
    """Wildwood: a patch that finishes growing throws three seeds round itself."""
    reset(sp)
    sp.place = Place(0.82, at=(C0, 16.5))
    sp.copies = [Place(0.36, at=(7.0, 9.5), darken=1), Place(0.36, at=(25.0, 9.0), darken=1),
                 Place(0.36, at=(24.5, 23.5), darken=1)]

    def thrown(cv, x):
        g = E["grass"]
        for (ex, ey) in ((7.0, 9.5), (25.0, 9.0), (24.5, 23.5)):
            m_trail(cv, g, (C0, 18.0), (ex, ey), 2)
    U(sp, thrown)


# --- Chain Lightning -------------------------------------------------------------------------------

@card("chain_lightning_forked_surge")
def _(sp, c):
    """Storm Surge: the arc forks through falling rain."""
    O(sp, lambda cv, x: m_chain(cv, E["water"], (14, 11), [(5, 19), (27, 21)], 1.4))


@card("chain_lightning_storm_reach")
def _(sp, c):
    """Storm Reach: more hops, a wider jump - the arc going on to three more."""
    reset(sp)
    sp.place = Place(0.82, dx=-3, dy=-1)
    O(sp, lambda cv, x: m_chain(cv, E["lightning"], x.tip, [(27, 14), (28, 25)], 1.8))


@card("chain_lightning_dense_current")
def _(sp, c):
    """Dense Current: no more chaining - one heavier bolt that crits, struck twice as thick."""
    reset(sp)
    sp.place = Place(1.1)
    sp.copies = [Place(1.1, dx=2.0, dy=0.0), Place(1.1, dx=-2.0, dy=1.0, darken=1)]
    U(sp, lambda cv, x: m_rays(cv, E["lightning"], x.centre, 9.0, 14.5, 8))
    O(sp, v_glint(4, at=(24, 8)))


@card("chain_lightning_cascade")
def _(sp, c):
    """Cascade: every hop splits again and reaches further."""
    reset(sp)
    sp.place = Place(0.9, dx=-2)

    def tree(cv, x):
        l = E["lightning"]
        m_chain(cv, l, (14, 11), [(24, 7), (4, 21)], 1.4)
        m_chain(cv, l, (18, 22), [(27, 18), (26, 27)], 1.4)
    O(sp, tree)


@card("chain_lightning_thunderhead")
def _(sp, c):
    """Thunderhead: one arc, twice the force - a single great bolt out of a storm cloud."""
    reset(sp)
    sp.place = Place(1.08, dy=3)
    O(sp, lambda cv, x: m_cloud(cv, 8.0))
    O(sp, lambda cv, x: m_blast(cv, E["lightning"], x.tip[0], min(27.0, x.tip[1]), 4.0))


@card("chain_lightning_lattice")
def _(sp, c):
    reset(sp)
    sp.subject = lattice


# --- Cinderbreath ----------------------------------------------------------------------------------

@card("cinderbreath_amplified_impact")
def _(sp, c):
    """Amplified Impact: a hotter jet, its tongues wider - the jet on a bed of its own flames."""
    reset(sp)
    sp.place = Place(0.9, dx=2, dy=-4)
    U(sp, v_rays(8.0, 10))


@card("cinderbreath_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: a shorter recharge, tongues thrown faster - arrows going round the jet,
    tongues flung on ahead of it."""
    reset(sp)
    sp.place = Place(0.72, dx=-1, dy=1)
    U(sp, v_cycle())

    def thrown(cv, x):
        f = E["fire"]
        for (px, py, r) in ((25.0, 6.5, 2.2), (28.5, 10.5, 1.6), (21.5, 4.0, 1.4)):
            cv.disc(px, py, r, r, f[3])
            cv.disc(px - 0.4, py + 0.4, r * 0.6, r * 0.6, f[1])
    O(sp, thrown)


@card("cinderbreath_hellbreath")
def _(sp, c):
    """Hellbreath: the fire burns black."""
    sp.rows = ("fire",)


@card("cinderbreath_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.16)
    sp.place = Place(1.16, dx=1.5, dy=-1.5)


@card("cinderbreath_infinite_horizon")
def _(sp, c):
    """Infinite Horizon: three tongues thrown at once, fanned from the one nozzle."""
    reset(sp)
    anchor = anchor_of(SPELLS["cinderbreath"]["paint"])
    pivot = (6.0, 26.0)
    sp.copies = [about(pivot, -36.0, anchor, 0.74), about(pivot, 36.0, anchor, 0.74)]
    sp.place = about(pivot, 0.0, anchor, 0.8)


@card("cinderbreath_cataclysm")
def _(sp, c):
    """Cataclysm: what the flame kills leaves a burning crater, and the craters spread."""
    reset(sp)
    sp.place = Place(0.74, at=(13.0, 12.0))

    def craters(cv, x):
        crater(cv, 9.5, 24.5, 6.0)
        crater(cv, 22.5, 22.0, 5.0)
        crater(cv, 23.0, 9.5, 3.5)
    U(sp, craters)


# --- Cone of Cold ----------------------------------------------------------------------------------

@card("cone_deep_freeze")
def _(sp, c):
    """Deep Freeze: what the cone hits is frozen solid."""
    reset(sp)
    sp.place = Place(0.9, dx=-1)
    O(sp, lambda cv, x: m_freeze(cv, (21, 16), 6.5))


@card("cone_wide_angle")
def _(sp, c):
    """Arctic Blast: the cone opened out to half a circle."""
    reset(sp)
    sp.place = Place(1.0, sy=1.55, dx=-1)
    U(sp, lambda cv, x: m_rays(cv, E["ice"], (5, 16), 18.0, 26.0, 10))


@card("cone_ice_shards")
def _(sp, c):
    """Frost Shrapnel: meltwater through the cone."""
    O(sp, lambda cv, x: m_drops(cv, E["water"], [(26, 9), (28, 21), (21, 25)]))


@card("cone_zero_point_wave")
def _(sp, c):
    """Zero Point Shockwave: a freezing blast reaching far further."""
    reset(sp)
    sp.place = Place(1.0, sy=1.2, dx=-4)
    U(sp, lambda cv, x: m_shockwave(cv, E["ice"], (4, 16), 21.0, 2))
    U(sp, lambda cv, x: m_speed(cv, E["ice"], (1, 0), (2, 8, 30, 24), 4, 6))


@card("cone_glacier_tide")
def _(sp, c):
    """Glacier Tide: the blast throws everything it hits away from you - a wall of ice going out."""
    reset(sp)
    sp.place = Place(0.86, dx=-3)

    def tide(cv, x):
        i = E["ice"]
        for (x0, h) in ((22, 10), (25, 13), (28, 9)):
            cv.poly([(x0 - 2, 16 + h * 0.5), (x0 + 1, 16 - h * 0.5), (x0 + 3, 16 + h * 0.5)], i[3])
            cv.line(x0, 16 + h * 0.4, x0 + 1, 16 - h * 0.5, i[1])
        m_chevrons(cv, i, (24, 16), 4)
    O(sp, tide)


@card("cone_of_cold_long_winter")
def _(sp, c):
    """The Long Winter: the cone opened into a full ring that freezes everything."""
    reset(sp)
    anchor = anchor_of(SPELLS["cone_of_cold"]["paint"])
    places = []
    for k in range(8):
        a = 45.0 * k
        t = math.radians(a)
        places.append(Place(0.42, rot=a, at=(C0 + math.cos(t) * 6.8, C0 + math.sin(t) * 6.8)))
    sp.copies = places[:-1]
    sp.place = places[-1]
    O(sp, lambda cv, x: m_glint(cv, C0, C0, 3, E["ice"]))


# --- Contagion -------------------------------------------------------------------------------------

@card("contagion_amplified_impact")
def _(sp, c):
    reset(sp)
    bigger(sp, 1.14)


@card("contagion_hyper_acceleration")
def _(sp, c):
    reset(sp)
    sp.place = Place(0.82)
    U(sp, v_cycle())


@card("contagion_marsh_fever")
def _(sp, c):
    pass


@card("contagion_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.24)


@card("contagion_infinite_horizon")
def _(sp, c):
    """Infinite Horizon: more carriers, and each plague reaches further - a bigger web of sores."""
    reset(sp)
    sp.place = Place(0.7)

    def web(cv, x):
        p = E["poison"]
        for (ex, ey) in ((4, 15), (27, 18), (17, 3), (9, 27), (24, 27), (26, 6)):
            m_trail(cv, p, (C0, C0), (ex, ey), 1)
            cv.disc(ex, ey, 2.4, 2.4, p[3])
            cv.disc(ex, ey, 1.4, 1.4, p[2])
            cv.set(ex - 1, ey - 1, p[1])
    U(sp, web)


# --- Cyclone Slash ---------------------------------------------------------------------------------

@card("cyclone_slash_dual_blades")
def _(sp, c):
    """Dual Windblades: three blades on the wind's wheel, spinning faster."""
    reset(sp)
    places = ring_of(3, 0.62, 8.0, 0.0, 0.0)
    sp.copies = places[1:]
    sp.place = places[0]
    O(sp, lambda cv, x: m_glint(cv, C0, C0, 2, E["wind"]))


@card("cyclone_slash_razor_gale")
def _(sp, c):
    """Razor Gale: the blades edged in steel."""
    O(sp, v_glint(3, at=(22, 6), element="metal"))


@card("cyclone_slash_tailwind_surge")
def _(sp, c):
    reset(sp)
    sp.place = Place(0.95, dx=2)
    U(sp, v_speed(5))
    U(sp, v_ghosts((3, 6)))


@card("cyclone_slash_typhoon_core")
def _(sp, c):
    reset(sp)
    sp.subject = typhoon_core


@card("cyclone_slash_vortex_reaper")
def _(sp, c):
    """Vortex Reaper: the blades drag the crowd in to the circle and cut - marks being hauled in
    toward the blade, streaks behind them."""
    reset(sp)
    sp.place = Place(0.9, dx=3)

    def hauled(cv, x):
        w = E["wind"]
        for (a, r) in ((150.0, 11.0), (205.0, 10.0), (250.0, 11.5), (110.0, 10.5)):
            t = math.radians(a)
            px, py = C0 + math.cos(t) * r, C0 + math.sin(t) * r
            ox, oy = math.cos(t), math.sin(t)
            cv.line(px + ox * 1.5, py + oy * 1.5, px + ox * 5.0, py + oy * 5.0, w[3])
            cv.line(px + ox * 1.5, py + oy * 1.5, px + ox * 3.0, py + oy * 3.0, w[2])
            cv.disc(px, py, 1.8, 1.8, OCC)
            cv.ring(px, py, 2.0, 2.0, w[2])
            cv.set(px - ox * 2.5, py - oy * 2.5, w[1])
    U(sp, hauled)


# --- Familiar --------------------------------------------------------------------------------------
# Three forms (the dog, the crow, the cat), each its own subject (SPELLS) and five cards. Every head is a
# familiar.py portrait with the calm eye (Eric, 2026-10-28, of the old icon: "it looks a little creepy, the
# eye is too prominent"); the dog's mouth is closed and easy.

@image
def annwn_head():
    """The Hound of Annwn: the white hound, red-eared, its eyes lit low (familiar.ANNWN)."""
    return familiar.portrait(familiar.ANNWN)


def crow_at(bird, s, at, steps=0, hat=True):
    cv = place_image(familiar.crow_portrait(bird, hat=hat), Place(s, at=at), anchor_of(crow_head))
    return darken(cv, steps) if steps else cv


@image
def crow_head():
    return familiar.crow_portrait()


@image
def raven_head():
    """The Raven: the crow grown heavy, its throat shaggy (familiar.RAVEN)."""
    return familiar.crow_portrait(familiar.RAVEN)


@image
def stormcrow_head():
    """The Stormcrow: storm-blue, lit along its edges in lightning (familiar.STORMCROW)."""
    return familiar.crow_portrait(familiar.STORMCROW)


@image
def morrigan_head():
    """The Morrigan: the violet battle-crow, crested, its eye lit low in ember (familiar.MORRIGAN)."""
    return familiar.crow_portrait(familiar.MORRIGAN)


@image
def murder():
    """A Murder of Crows: two more crows behind the first, a stop darker each."""
    c = _Real(CELL, CELL, None)
    over(c, crow_at(familiar.CROW, 0.6, (9.0, 9.0), 2, hat=False))
    over(c, crow_at(familiar.CROW, 0.66, (23.0, 11.0), 1, hat=False))
    over(c, crow_at(familiar.CROW, 0.8, (15.0, 19.0)))
    return c


@image
def grimalkin_head():
    """Grimalkin: the old witch's cat in the wizard's mantle (familiar.GRIMALKIN)."""
    return familiar.cat_portrait(familiar.GRIMALKIN, mantle=True)


@image
def cait_sith_head():
    """The Cait Sith: the black fairy cat with the white star on its breast (familiar.CAIT_SITH)."""
    return familiar.cat_portrait(familiar.CAIT_SITH)


@image
def cath_palug_head():
    """Cath Palug: the great spotted cat of the old tale, in the mantle (familiar.CATH_PALUG)."""
    return familiar.cat_portrait(familiar.CATH_PALUG, mantle=True)


@image
def nine_lives():
    """Nine Lives: the cat, and the life it gives - a second of it behind, a stop darker."""
    c = _Real(CELL, CELL, None)
    ghost = place_image(familiar.cat_portrait(familiar.CAT, hat=False), Place(0.62, at=(23.0, 10.0)), anchor_of(cat_head))
    over(c, darken(ghost, 2))
    over(c, place_image(familiar.cat_portrait(familiar.CAT), Place(0.86, at=(14.0, 18.0)), anchor_of(cat_head)))
    return c


@image
def cat_head():
    return familiar.cat_portrait()


@card("familiar_dog_great_hound")
def _(sp, c):
    """Great Hound: grown into a great hound in the wizard's mantle, the arcane light behind it."""
    reset(sp)
    sp.subject = great_hound
    sp.place = Place(1.0, dx=1)
    U(sp, lambda cv, x: m_rays(cv, E["arcane"], (17, 15), 9.0, 14.5, 12))


@card("familiar_dog_rimehound")
def _(sp, c):
    """Rimehound - Eric's own example: "a similar dog icon with an icy background and some icicles
    added". The hound in its frost coat (familiar.RIME, the sprite's own), set low so the icy night
    shows above it, snow in that night, icicles hanging from the rim, and its breath freezing."""
    reset(sp)
    sp.subject = rime_head
    sp.recolour = None
    sp.grain = True
    sp.place = Place(0.84, at=(18.0, 19.0))

    def breath(cv, x):
        i = E["ice"]
        for (px, py, t) in ((28, 21, 0), (29, 19, 1), (30, 22, 1), (28, 24, 2), (26, 26, 1), (29, 25, 0)):
            cv.set(px, py, i[t])
        m_glint(cv, 27, 15, 1, i)
    O(sp, breath)


@card("familiar_dog_littermates")
def _(sp, c):
    """Littermates: another hound answers the call."""
    reset(sp)
    sp.subject = littermates


@card("familiar_dog_cerberus")
def _(sp, c):
    """Cerberus: three heads, three bites. The sprite's hellhound coat (familiar.CERB) is nearly as
    dark as the arcane night, so the heads take a pixel of ember light round them, the colour of
    its eyes."""
    reset(sp)
    sp.subject = cerberus_heads
    sp.glow = E["fire"][3]


@card("familiar_crow_griffin")
def _(sp, c):
    """Griffin: the familiar takes wing - the griffin itself, the swoop it flies dotted under it."""
    reset(sp)
    sp.subject = griffin_form

    def swoop(cv, x):
        a = E["arcane"]
        for i in range(26):
            t = i / 25.0
            px = 4 + t * 24
            py = 20 + math.sin(t * math.pi) * 7
            if i % 2 == 0:
                cv.set(px, py, a[2] if t < 0.7 else a[1])
        m_glint(cv, 28, 20, 1, a)
    U(sp, swoop)


# --- Fireball --------------------------------------------------------------------------------------


@card("familiar_dog_hound_of_annwn")
def _(sp, c):
    """Hound of Annwn: the white, red-eared hound of the otherworld - and the strongest thing in reach, a
    gold sight on the night behind it, because it hunts the biggest."""
    reset(sp)
    sp.subject = annwn_head
    sp.glow = E["fire"][3]

    def sight(cv, x):
        g = GOLD
        cv.ring(C0, C0, 12.5, 12.5, g[2])
        for (x0, y0, x1, y1) in ((C0, 1, C0, 5), (C0, 26, C0, 30), (1, C0, 5, C0), (26, C0, 30, C0)):
            cv.line(x0, y0, x1, y1, g[1])
    U(sp, sight)


@card("familiar_crow_raven")
def _(sp, c):
    """Raven: the crow grown heavy, the arcane light behind it."""
    reset(sp)
    sp.subject = raven_head
    U(sp, lambda cv, x: m_rays(cv, E["arcane"], (16, 14), 9.0, 14.5, 12))


@card("familiar_crow_stormcrow")
def _(sp, c):
    """Stormcrow: the crow out of the storm, lit in lightning, sparks off its wing."""
    reset(sp)
    sp.subject = stormcrow_head
    sp.recolour = None


@card("familiar_crow_murder_of_crows")
def _(sp, c):
    """Murder of Crows: the flock grows."""
    reset(sp)
    sp.subject = murder


@card("familiar_crow_the_morrigan")
def _(sp, c):
    """The Morrigan: the battle-crow that chooses who falls - crested, violet, the glint of the stroke
    that finishes."""
    reset(sp)
    sp.subject = morrigan_head
    sp.glow = E["fire"][3]
    O(sp, lambda cv, x: m_glint(cv, 27, 24, 3, E["fire"]))


@card("familiar_cat_grimalkin")
def _(sp, c):
    """Grimalkin: the old witch's cat in the wizard's mantle, the arcane light behind it."""
    reset(sp)
    sp.subject = grimalkin_head
    U(sp, lambda cv, x: m_rays(cv, E["arcane"], (16, 15), 9.0, 14.5, 12))


@card("familiar_cat_cait_sith")
def _(sp, c):
    """Cait Sith: the black fairy cat with its white star."""
    reset(sp)
    sp.subject = cait_sith_head
    sp.recolour = None
    sp.glow = E["darkness"][2]


@card("familiar_cat_purr")
def _(sp, c):
    """Purr: the cat at ease - and the heart it mends."""
    reset(sp)
    sp.subject = cat_head
    sp.place = Place(0.9, at=(15.0, 17.0))
    O(sp, lambda cv, x: m_heart(cv, 25, 6))


@card("familiar_cat_cath_palug")
def _(sp, c):
    """Cath Palug: the monstrous cat of the old tale, the ground shaken round it."""
    reset(sp)
    sp.subject = cath_palug_head
    U(sp, v_shock(2))


@card("familiar_cat_nine_lives")
def _(sp, c):
    """Nine Lives: the cat gives one of its lives for yours."""
    reset(sp)
    sp.subject = nine_lives
    O(sp, lambda cv, x: m_heart(cv, 25, 24, drops=False))

@card("fireball_pyroclasm")
def _(sp, c):
    """Pyroclasm: the impact leaves a pool of burning magma."""
    reset(sp)
    sp.place = Place(0.74, dy=-4)
    sp.accent = None
    U(sp, lambda cv, x: m_magma(cv, 25.0))


@card("fireball_cluster")
def _(sp, c):
    """Cluster Bombs: each blast throws out three bomblets."""
    reset(sp)
    sp.place = Place(0.72, dx=-3, dy=-3)
    sp.copies = [Place(0.3, at=(24.5, 11.0), darken=0), Place(0.3, at=(25.0, 23.5), darken=0),
                 Place(0.3, at=(12.0, 25.5), darken=0)]


@card("fireball_comet")
def _(sp, c):
    """Iron Comet: forged round an iron core."""
    reset(sp)
    sp.subject = iron_comet
    sp.recolour = None


@card("fireball_hellfire_nova")
def _(sp, c):
    """Hellfire Nova: it detonates in a colossal ring of flame."""
    reset(sp)
    sp.place = Place(0.8)
    U(sp, lambda cv, x: m_shockwave(cv, E["fire"], (C0, C0), 12.5, 2))
    U(sp, lambda cv, x: m_rays(cv, E["fire"], (C0, C0), 8.0, 14.0, 16))


@card("fireball_armageddon_sun")
def _(sp, c):
    """Armageddon Sun: a slow, swollen sun."""
    reset(sp)
    sp.place = Place(1.3)

    def spots(cv, x):
        f = E["fire"]
        for (sx, sy, r) in ((12, 13, 1.6), (19, 20, 1.2), (21, 11, 1.0)):
            cv.disc(sx, sy, r, r, f[3])
        for (a0, ln) in ((300, 5), (40, 4), (150, 5)):
            t = math.radians(a0)
            for k in range(ln):
                cv.set(C0 + math.cos(t) * (13 + k * 0.3), C0 + math.sin(t) * (13 - k * 0.2) - k * 0.3, f[1] if k < 2 else f[2])
    O(sp, spots)


# --- Frost Shard -----------------------------------------------------------------------------------

@card("frost_shard_glacial_spike")
def _(sp, c):
    """Glacial Spike: deeper cold, a harder slow - a spike of ice standing up out of the shatter."""
    reset(sp)
    sp.place = Place(0.9, dy=3)

    def spike(cv, x):
        i = E["ice"]
        cv.poly([(12.5, 21), (16, 5), (19.5, 21)], i[3])
        cv.poly([(14.5, 21), (16, 7), (17.5, 21)], i[2])
        cv.line(16, 7, 16, 20, i[1])
        cv.set(16, 5, i[0])
        cv.set(16, 6, i[0])
    O(sp, spike)


@card("frost_shard_ice_flurry")
def _(sp, c):
    """Ice Flurry: every shatter flings two more shards at what is beyond it."""
    reset(sp)
    sp.place = Place(0.78, dx=-3, dy=2)

    def flung(cv, x):
        i = E["ice"]
        for (sx, sy, ex, ey) in ((18, 12, 28, 4), (19, 17, 30, 15)):
            cv.line(sx, sy, ex, ey, i[2])
            cv.line(sx + 3, sy - 2, ex, ey, i[1])
            cv.disc(ex, ey, 1.3, 1.3, i[1])
            cv.set(ex, ey, i[0])
    O(sp, flung)


@card("frost_shard_frost_nova")
def _(sp, c):
    """Frost Nova Burst: the shatter freezes what it catches solid."""
    reset(sp)
    sp.place = Place(0.78)
    O(sp, lambda cv, x: m_freeze(cv, (C0, C0), 8.5))


@card("frost_shard_absolute_zero")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.18)
    O(sp, lambda cv, x: m_glint(cv, C0, C0, 4, E["ice"]))


@card("frost_shard_blizzard_engine")
def _(sp, c):
    """Blizzard Engine: sleet out of every corpse."""
    reset(sp)
    sp.place = Place(0.74, dy=-3)

    def sleet(cv, x):
        w = E["water"]
        for (sx, sy) in ((4, 8), (9, 4), (24, 5), (27, 11), (6, 16), (26, 18), (12, 20), (20, 19)):
            cv.line(sx, sy, sx - 2, sy + 4, w[2])
            cv.set(sx, sy, w[0])
    O(sp, sleet)


# --- Gale Blade ------------------------------------------------------------------------------------

@card("gale_blade_honed_edge")
def _(sp, c):
    """Honed Edge: the blade whetted - a bright edge run along it."""
    reset(sp)
    sp.place = Place(1.06)

    def edge(cv, x):
        w = E["wind"]
        for (px, py) in ((8, 6), (12, 5), (16, 5), (20, 6), (23, 8), (26, 12), (27, 16), (27, 20), (25, 24)):
            cv.set(px, py, w[0])
    O(sp, edge)


@card("gale_blade_keen_wind")
def _(sp, c):
    """Keen Wind: a far better chance to crit."""
    reset(sp)
    sp.place = Place(0.92, dx=-2, dy=2)
    sp.back = "light"
    O(sp, v_glint(4, at=(24, 8)))
    O(sp, v_glint(2, at=(8, 24)))


@card("gale_blade_quickening")
def _(sp, c):
    pass


@card("gale_blade_executioners_gale")
def _(sp, c):
    """Executioner's Gale: crits land with a whole extra multiplier."""
    reset(sp)
    sp.place = Place(1.15)
    sp.back = "fire"

    def mark(cv, x):
        m_glint(cv, 24, 9, 5, E["light"])
        r = M["ember"]
        for k in range(-3, 4):
            cv.set(9 + k, 22 + k, r[1])
            cv.set(9 + k, 22 - k, r[1])
    O(sp, mark)


@card("gale_blade_tempest_rhythm")
def _(sp, c):
    """Tempest Rhythm: thrown faster and harder."""
    reset(sp)
    sp.place = Place(0.95, dx=3)
    U(sp, v_speed(5, 10))
    U(sp, v_ghosts((4, 8)))


# --- Glacial Spike ---------------------------------------------------------------------------------

@card("glacial_spike_amplified_impact")
def _(sp, c):
    """Amplified Impact: spikes 30% wider, deepening Earth - the spike spread wide, rocks thrown."""
    reset(sp)
    sp.place = Place(1.3, sy=1.0)
    sp.accent = "earth"


@card("glacial_spike_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: a shorter cooldown, the ice erupting sooner - a smaller spike shooting up,
    speed lines under it."""
    reset(sp)
    faster(sp, c, s=0.7)


@card("glacial_spike_reactive_catalyst")
def _(sp, c):
    """Reactive Catalyst: what the spike impales is pinned."""
    reset(sp)
    sp.place = Place(0.9, dy=-1)
    O(sp, lambda cv, x: m_roots(cv))


@card("glacial_spike_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.2)
    sp.place = Place(1.2, dy=-1)


@card("glacial_spike_infinite_horizon")
def _(sp, c):
    reset(sp)
    set_volley(sp, [Place(0.62, at=(8.5, 16.5), darken=1), Place(0.62, at=(23.5, 16.5), darken=1),
                    Place(0.8, at=(C0, 15.0))])


# --- Gravewell -------------------------------------------------------------------------------------

@card("gravewell_amplified_impact")
def _(sp, c):
    reset(sp)
    bigger(sp, 1.12)


@card("gravewell_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: the pits open sooner - arrows going round a smaller pit."""
    reset(sp)
    sp.place = Place(0.68)
    U(sp, v_cycle())


@card("gravewell_reactive_catalyst")
def _(sp, c):
    """Reactive Catalyst: the earth holds what it catches longer - hands of earth out of the pit."""
    reset(sp)
    sp.place = Place(1.0, dy=2)

    def hold(cv, x):
        e = E["earth"]
        for (x0, lean) in ((11, -1), (16, 0), (21, 1)):
            cv.line(x0, 20, x0 + lean * 2, 12, e[2])
            cv.line(x0 + 1, 20, x0 + 1 + lean * 2, 12, e[3])
            cv.set(x0 + lean * 2, 11, e[1])
            cv.set(x0 + lean * 2 - 1, 12, e[1])
            cv.set(x0 + lean * 2 + 2, 12, e[2])
    O(sp, hold)


@card("gravewell_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.22)
    sp.glow = E["darkness"][2]


@card("gravewell_infinite_horizon")
def _(sp, c):
    reset(sp)
    set_volley(sp, volley(3, None, 0.66, 0.52))


# --- Hollow Star -----------------------------------------------------------------------------------

@card("hollow_star_amplified_impact")
def _(sp, c):
    reset(sp)
    bigger(sp, 1.14)


@card("hollow_star_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: the well drags harder into a tighter knot."""
    reset(sp)
    sp.place = Place(0.86)
    U(sp, v_vortex(3, 2.4))


@card("hollow_star_eclipse")
def _(sp, c):
    """Eclipse: the well burns with a corona."""
    U(sp, lambda cv, x: m_rays(cv, E["light"], (C0, C0), 6.0, 14.5, 16))


@card("hollow_star_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.24)


@card("hollow_star_infinite_horizon")
def _(sp, c):
    reset(sp)
    set_volley(sp, volley(3, None, 0.62, 0.5))
    O(sp, lambda cv, x: m_roots(cv))


@card("hollow_star_spirit_mirror")
def _(sp, c):
    """The Spirit Mirror: Dee's obsidian glass, and every orb in its reach pulled to it."""
    reset(sp)
    sp.subject = spirit_mirror
    O(sp, lambda cv, x: m_orbs(cv, [(4, 9), (27, 8), (5, 21), (26, 21)]))


# --- Hunter's Draw ---------------------------------------------------------------------------------

def extra_arrows(cv, x, angles=(-22, 22)):
    w = E["wind"]
    for a in angles:
        t = math.radians(a)
        ex, ey = 8 + math.cos(t) * 21, 16 + math.sin(t) * 21
        cv.line(8, 16, ex, ey, w[2])
        hx, hy = math.cos(t), math.sin(t)
        px, py = -hy, hx
        cv.poly([(ex + hx * 2, ey + hy * 2), (ex - hx * 2 + px * 2, ey - hy * 2 + py * 2), (ex - hx * 2 - px * 2, ey - hy * 2 - py * 2)], w[1])
        cv.set(ex + hx * 2, ey + hy * 2, w[0])


@card("hunters_draw_amplified_impact")
def _(sp, c):
    """Amplified Impact: broader arrowheads, of iron."""
    reset(sp)

    def head(cv, x):
        m = E["metal"]
        cv.poly([(31, 16), (24, 11), (24, 21)], m[3])
        cv.poly([(30, 16), (25, 13), (25, 19)], m[2])
        cv.line(25, 13, 30, 16, m[1])
        cv.set(31, 16, m[0])
    O(sp, head)


@card("hunters_draw_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: nocked sooner, and the arrow gone on the wind."""
    reset(sp)
    sp.back = "wind"
    sp.place = Place(0.8, dx=-2)

    def loosed(cv, x):
        w = E["wind"]
        for (y, x0, x1) in ((9, 14, 26), (16, 17, 31), (23, 14, 26)):
            cv.line(x0, y, x1, y, w[3])
            cv.line(x1 - 5, y, x1, y, w[1])
        cv.poly([(31, 16), (27, 13), (27, 19)], w[1])
    O(sp, loosed)


@card("hunters_draw_reactive_catalyst")
def _(sp, c):
    reset(sp)
    pierced(sp, c, 2, 0.72)


@card("hunters_draw_titan_cataclysm")
def _(sp, c):
    """Titan Cataclysm: colossal arrows."""
    reset(sp)
    sp.place = Place(1.0, dx=-2)

    def giant(cv, x):
        w = E["wind"]
        cv.rect(4, 14, 24, 17, w[3])
        cv.hline(4, 24, 15, w[2])
        cv.poly([(31, 15.5), (21, 6), (21, 25)], w[3])
        cv.poly([(29, 15.5), (22, 9), (22, 22)], w[2])
        cv.line(22, 9, 29, 15, w[1])
        cv.set(30, 15, w[0])
    O(sp, giant)
    U(sp, v_shock(1))


@card("hunters_draw_infinite_horizon")
def _(sp, c):
    """Infinite Horizon: three more arrows in every volley."""
    reset(sp)
    O(sp, extra_arrows)


# --- Iron Palisade ---------------------------------------------------------------------------------

@card("iron_palisade_amplified_impact")
def _(sp, c):
    """Amplified Impact: a longer wall - more stakes either side of it."""
    reset(sp)
    sp.place = Place(1.0)
    sp.copies = [Place(0.62, at=(5.0, 19.0), darken=1), Place(0.62, at=(27.0, 19.0), darken=1)]


@card("iron_palisade_hyper_acceleration")
def _(sp, c):
    reset(sp)
    sp.place = Place(0.8)
    U(sp, v_cycle())


@card("iron_palisade_lightning_rod")
def _(sp, c):
    """Lightning Rod: the stakes draw lightning."""
    sp.recolour = None
    sp.back = "lightning"

    def rods(cv, x):
        l = E["lightning"]
        for (sx, top) in ((5, 9), (11, 6), (16, 8), (21, 5), (26, 9)):
            zig(cv, (sx + 1, top - 7), (sx + 1, top - 1), l[1], 1.0, 3, l[0])
            cv.set(sx + 1, top - 1, l[0])
    O(sp, rods)


@card("iron_palisade_titan_cataclysm")
def _(sp, c):
    """Titan Cataclysm: a colossal palisade, twice as long - wide and tall on its shockwave."""
    reset(sp)
    sp.place = Place(1.12, sy=1.1, dy=1)
    U(sp, v_shock(2))


@card("iron_palisade_infinite_horizon")
def _(sp, c):
    """Infinite Horizon: more walls, toward other packs - three short walls, apart."""
    reset(sp)
    set_volley(sp, [Place(0.5, sy=0.62, at=(8.5, 10.0)), Place(0.5, sy=0.62, at=(23.5, 10.0)),
                    Place(0.56, sy=0.7, at=(C0, 21.0))])


# --- Kindled Ward ----------------------------------------------------------------------------------

@card("kindled_ward_amplified_impact")
def _(sp, c):
    reset(sp)
    bigger(sp, 1.1)


@card("kindled_ward_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: the wards drift faster."""
    reset(sp)
    sp.place = Place(0.74, dx=3, dy=-2)
    U(sp, lambda cv, x: m_ghosts(cv, x.subject, (1, -0.6), [E["light"][3], M["amber"][3]], (4, 8)))
    U(sp, lambda cv, x: m_speed(cv, E["light"], (1, -0.6), x.box, 3, 8))


@card("kindled_ward_rimelight")
def _(sp, c):
    pass


@card("kindled_ward_titan_cataclysm")
def _(sp, c):
    """Titan Cataclysm: each ward a small sun - corona rays round a bigger ward."""
    reset(sp)
    sp.place = Place(0.92)
    U(sp, lambda cv, x: m_rays(cv, E["light"], (C0, C0), 9.5, 14.5, 16))
    O(sp, lambda cv, x: [m_glint(cv, px, py, 2, E["light"]) for (px, py) in ((8, 8), (24, 24))])


@card("kindled_ward_infinite_horizon")
def _(sp, c):
    reset(sp)
    set_volley(sp, volley(3, None, 0.62, 0.5))


# --- Magic Missile ---------------------------------------------------------------------------------

@card("magic_missile_twin_volley")
def _(sp, c):
    """Twin Volley: the missile forks on its first hit, two shards flying out either side."""
    reset(sp)
    sp.place = Place(0.66, at=(10.0, 16.0))

    def split(cv, x):
        a = E["arcane"]
        dart(cv, a, 18, 15, 27, 7)
        dart(cv, a, 18, 17, 27, 25)
        m_glint(cv, 18, 16, 2, a)
    O(sp, split)


@card("magic_missile_piercing_needle")
def _(sp, c):
    """Piercing Stardust: one long needle on through three more."""
    reset(sp)
    sp.place = Place(1.0, sy=0.55, dx=1)

    def skewered(cv, x):
        a = E["arcane"]
        for px in (8, 16, 24):
            cv.disc(px, 16, 3.2, 3.2, a[2])
            cv.disc(px, 16, 2.2, 2.2, OCC)
    U(sp, skewered)
    O(sp, lambda cv, x: [m_glint(cv, px, py, 1, E["arcane"]) for (px, py) in ((8, 8), (24, 24), (13, 25), (22, 7))])


@card("magic_missile_unstable_shard")
def _(sp, c):
    """Unstable Detonation: the missile bursts in an arcane splash where it hits."""
    reset(sp)
    sp.place = Place(0.6, at=(9.0, 16.0))
    U(sp, lambda cv, x: m_rays(cv, E["arcane"], (21, 16), 4.0, 10.0, 12))
    O(sp, lambda cv, x: m_blast(cv, E["arcane"], 21, 16, 7.5))


@card("magic_missile_gatling_barrage")
def _(sp, c):
    """Arcane Gatling Barrage: a stream of darts."""
    reset(sp)
    sp.copies = [Place(0.5, at=(9.0, 9.5), darken=1), Place(0.5, at=(7.0, 22.5), darken=1),
                 Place(0.56, at=(14.0, 16.0), darken=0)]
    sp.place = Place(0.62, at=(21.0, 12.0))
    sp.copies.append(Place(0.6, at=(22.0, 21.0)))
    U(sp, v_speed(5, 6))


@card("magic_missile_singularity_core")
def _(sp, c):
    reset(sp)
    sp.subject = singularity_core
    U(sp, lambda cv, x: m_vortex(cv, E["arcane"], (19, 16), 13.0, 3, 2.6))


# --- Meteor Swarm ----------------------------------------------------------------------------------

@card("meteor_cluster")
def _(sp, c):
    """Meteor Shower: more meteors a volley."""
    reset(sp)
    sp.place = Place(0.7, at=(17.0, 17.0))
    sp.copies = [Place(0.55, at=(9.0, 9.0), darken=1), Place(0.5, at=(24.0, 8.0), darken=1)]


@card("meteor_magma_crater")
def _(sp, c):
    reset(sp)
    sp.place = Place(0.78, dy=-4)
    U(sp, lambda cv, x: m_magma(cv, 25.0))


@card("meteor_starcrash")
def _(sp, c):
    """Cosmic Impact: one meteor hitting hard enough to throw the crowd back."""
    reset(sp)
    sp.place = Place(0.62, at=(12.0, 11.0))
    sp.accent = None

    def impact(cv, x):
        m_blast(cv, E["fire"], 20, 22, 7.0)
        m_chevrons(cv, E["earth"], (20, 22), 9)
        acc_earth(cv, x.r, False)
    O(sp, impact)


@card("meteor_extinction_event")
def _(sp, c):
    """Extinction Event: every meteor a colossal impact."""
    reset(sp)
    sp.place = Place(0.9, at=(11.0, 11.0))

    def big(cv, x):
        f = E["fire"]
        cv.disc(17, 17, 9.0, 9.0, f[3])
        cv.disc(16, 16, 7.0, 7.0, f[2])
        cv.disc(15, 15, 4.5, 4.5, f[1])
        cv.disc(14, 14, 2.2, 2.2, f[0])
        for k in range(4):
            cv.line(9 - k, 9 - k * 0.5, 3 - k, 3 - k * 0.5, f[3])
    O(sp, big)
    U(sp, v_shock(2))


@card("meteor_orbital_bombardment")
def _(sp, c):
    """Orbital Bombardment: a non-stop rain of burning stars."""
    reset(sp)
    sp.place = Place(0.5, at=(20.0, 20.0))
    sp.copies = [Place(0.45, at=(10.0, 8.0), darken=1), Place(0.45, at=(22.0, 7.0), darken=1),
                 Place(0.45, at=(8.0, 21.0), darken=1), Place(0.45, at=(15.0, 14.0))]
    U(sp, lambda cv, x: m_speed(cv, E["fire"], (1, 1), (4, 4, 28, 28), 5, 8))


# --- Mirefoot --------------------------------------------------------------------------------------

@card("mirefoot_amplified_impact")
def _(sp, c):
    """Amplified Impact: wider pools."""
    reset(sp)
    sp.place = Place(1.2, sy=1.0, dy=1)


@card("mirefoot_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: pools laid closer, biting more often - on the reed bed's night, going
    round faster."""
    reset(sp)
    sp.back = "grass"
    sp.place = Place(0.78)
    U(sp, v_cycle())


@card("mirefoot_reactive_catalyst")
def _(sp, c):
    """Reactive Catalyst: the bog drags harder - roots up out of the water to hold what wades in."""
    reset(sp)
    sp.back = "grass"
    sp.place = Place(0.95, dy=-2)
    O(sp, lambda cv, x: m_roots(cv))


@card("mirefoot_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.22)


@card("mirefoot_infinite_horizon")
def _(sp, c):
    """Infinite Horizon: pools twice as close, a long trail of them."""
    reset(sp)
    sp.back = "grass"
    sp.copies = [Place(0.44, at=(8.0, 8.0), darken=1), Place(0.5, at=(13.5, 13.5), darken=1),
                 Place(0.56, at=(19.0, 19.0))]
    sp.place = Place(0.62, at=(24.0, 24.5))


# --- Molten Shard ----------------------------------------------------------------------------------

@card("molten_shard_amplified_impact")
def _(sp, c):
    reset(sp)
    bigger(sp, 1.15)


@card("molten_shard_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: the splinter loosed faster - on the forge's night, trailing heat."""
    reset(sp)
    sp.back = "fire"
    sp.place = Place(0.78, dx=3, dy=-3)
    faster(sp, c)


@card("molten_shard_reactive_catalyst")
def _(sp, c):
    reset(sp)
    pierced(sp, c, 2, 0.7)


@card("molten_shard_titan_cataclysm")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.3)


@card("molten_shard_infinite_horizon")
def _(sp, c):
    reset(sp)
    set_volley(sp, volley(3, SPELLS["molten_shard"]["travel"], 0.7, 0.56, 9.0))


# --- Obsidian Spike --------------------------------------------------------------------------------

@card("obsidian_spike_splinter_field")
def _(sp, c):
    """Splinter Field: the eruption bursts wider - the cracks run further and splinters of glass come
    up where they end, as the spell draws it (GroundSpike's splinters)."""
    reset(sp)
    sp.place = Place(0.66, at=(C0, 14.0))

    def field(cv, x):
        e = E["earth"]
        for path in ([(15, 24), (11, 26), (6, 26)], [(17, 24), (21, 26), (26, 26)],
                     [(15, 25), (12, 28), (9, 29)], [(17, 25), (20, 28), (23, 29)]):
            glass.crack(cv, path, e)
        for (apex, fl, fr, rd, gy) in (((4, 20), 4, 9, 6, 27), ((28, 20), 23, 28, 26, 27),
                                      ((8, 24), 7, 12, 9, 30), ((24, 24), 20, 25, 22, 30)):
            glass.blade(cv, apex, fl, fr, rd, gy, heat=0.8, ramp=e, band=False, ripples=False, spec=0.6)
    O(sp, field)


@card("obsidian_spike_umbral_vein")
def _(sp, c):
    """Umbral Vein: black glass that drinks the light."""
    sp.glow = E["darkness"][2]


@card("obsidian_spike_deep_strata")
def _(sp, c):
    """Deep Strata: driven from far deeper, reaching further - the spikes tall and narrow, up out of
    the layered ground."""
    reset(sp)
    sp.place = Place(0.72, sy=1.0, dy=-3)

    def strata(cv, x):
        e = E["earth"]
        for (y, tone) in ((26, e[3]), (28, e[2]), (30, e[3])):
            cv.hline(3, 28, y, tone)
        cv.line(16, 26, 16, 30, e[1])
    O(sp, strata)


@card("obsidian_spike_tectonic_uprising")
def _(sp, c):
    reset(sp)
    colossal(sp, 1.22)
    sp.place = Place(1.22, dy=-1)
    O(sp, lambda cv, x: m_crack(cv, E["earth"], 26))


@card("obsidian_spike_basalt_lance")
def _(sp, c):
    """Basalt Lance: a needle of volcanic glass, forged rather than grown, driving on through the
    crowd behind its target - the spell's own glass (obsidian_glass) drawn long and thin and lit by
    the forge, with the needles that follow it running away up the line."""
    reset(sp)
    sp.ramp = "fire"

    def lance(cv, x):
        f = E["fire"]
        part = _Real(CELL, CELL, None)
        # The followers first, smaller and further up the line, then the needle in front.
        glass.blade(part, (27, 6), 25, 29, 27, 15, heat=0.7, ramp=f, band=False, ripples=False, spec=0.6)
        glass.blade(part, (22, 4), 19, 25, 22, 21, heat=0.85, ramp=f, band=False, ripples=False, spec=0.6)
        glass.blade(part, (14, 2), 10, 18, 13, 29, heat=1.0, ramp=f, chip=0.5, spec=0.7)
        glow_round(cv, part, f[3])
        over(cv, part)
    # The spell's own cluster, small at the needle's foot, so it is still Obsidian Spike.
    sp.place = Place(0.5, at=(10.0, 24.0))
    O(sp, lance)


@card("obsidian_spike_sundering")
def _(sp, c):
    """Sundering: a broken ring of obsidian raised round you, the crowd shoved out of it."""
    reset(sp)
    sp.subject = sundering_ring
    O(sp, lambda cv, x: m_glint(cv, C0, 17.0, 2, E["earth"]))


def sundering_ring():
    """The Apotheosis's ring: blades of the spell's glass stood round an empty middle in a ring seen
    from above and in front, the back ones a stop darker and the front ones over them, with the
    three gaps the ring really has (SunderingRing: three of twelve slots left open)."""
    cv = raster.Canvas(CELL, CELL, None)
    e = E["earth"]
    cx, cy, rx, ry = 16.0, 18.0, 11.0, 6.5
    slots = []
    for k in range(9):
        if k in (2, 5, 8):
            continue
        a = math.radians(-90 + 40 * k + 10)
        slots.append((cx + math.cos(a) * rx, cy + math.sin(a) * ry))
    slots.sort(key=lambda p: p[1])
    for (x, y) in slots:
        front = y > cy
        h = 9 if front else 7
        lean = 1 if x > cx else -1
        glass.blade(cv, (x + lean, y - h), x - 2, x + 2, x, y, heat=0.6 if front else 0.3, ramp=e,
                    band=False, ripples=False, spec=0.6)
    return cv


# --- Riptide ---------------------------------------------------------------------------------------

@card("riptide_amplified_impact")
def _(sp, c):
    reset(sp)
    bigger(sp, 1.14)


@card("riptide_hyper_acceleration")
def _(sp, c):
    reset(sp)
    sp.place = Place(0.9, dx=2)
    faster(sp, c)


@card("riptide_brackish_tide")
def _(sp, c):
    pass


@card("riptide_titan_cataclysm")
def _(sp, c):
    """Titan Cataclysm: a tidal wave."""
    reset(sp)
    colossal(sp, 1.3)
    sp.place = Place(1.3, dx=-1, dy=-1)


@card("riptide_infinite_horizon")
def _(sp, c):
    reset(sp)
    set_volley(sp, [Place(0.55, at=(10.0, 9.0), darken=1), Place(0.55, at=(22.0, 11.0), darken=1),
                    Place(0.7, at=(C0, 20.0))])


@card("riptide_spring_tide")
def _(sp, c):
    """Spring Tide: the ground behind the wave flooded, the current dragging along its path."""
    reset(sp)
    sp.place = Place(0.8, dx=1, dy=-4)

    def flood(cv, x):
        w = E["water"]
        for y in range(22, 27):
            for xx in range(3, 29):
                if in_disc(12.5)(xx, y):
                    cv.set(xx, y, w[3] if (xx + y) % 4 else w[2])
        for (cx, cy) in ((8, 24), (16, 24), (24, 24)):
            for d in range(-2, 3):
                cv.set(cx + 2 - abs(d), cy + d, w[0] if d == 0 else w[1])
    O(sp, flood)


# --- Scorching Ray ---------------------------------------------------------------------------------

@card("scorching_ray_amplified_impact")
def _(sp, c):
    reset(sp)
    sp.place = Place(0.86, dx=-2, dy=2)
    O(sp, lambda cv, x: m_blast(cv, E["fire"], x.tip[0], x.tip[1], 6.0))


@card("scorching_ray_hyper_acceleration")
def _(sp, c):
    """Hyper Acceleration: the rays strike faster - one after another, the last still fading."""
    reset(sp)
    sp.place = Place(0.84, dx=-3, dy=-3)
    sp.copies = [Place(0.84, dx=5, dy=5, darken=2), Place(0.84, dx=1, dy=1, darken=1)]
    U(sp, lambda cv, x: m_cycle(cv, E["fire"], (C0, C0), 13.0))


@card("scorching_ray_reactive_catalyst")
def _(sp, c):
    """Reactive Catalyst: each ray burns through into two more behind it."""
    reset(sp)
    sp.place = Place(0.74, dx=-4, dy=4)

    def through(cv, x):
        f = E["fire"]
        for (px, py) in ((20, 12), (25, 7)):
            cv.disc(px, py, 2.0, 2.0, OCC)
            cv.ring(px, py, 2.4, 2.4, f[3])
            cv.set(px, py, f[1])
        cv.line(17, 15, 29, 3, f[1])
        cv.line(18, 15, 30, 3, f[2])
    O(sp, through)


@card("scorching_ray_titan_cataclysm")
def _(sp, c):
    """Titan Cataclysm: every ray bursts on its target, three times as wide."""
    reset(sp)
    sp.place = Place(0.8, dx=-3, dy=3)
    U(sp, lambda cv, x: m_blast(cv, E["arcane"], 22, 10, 9.0))
    O(sp, lambda cv, x: m_blast(cv, E["fire"], 22, 10, 5.0))


@card("scorching_ray_infinite_horizon")
def _(sp, c):
    """Infinite Horizon: more rays, fanned from the one emitter."""
    reset(sp)
    anchor = anchor_of(SPELLS["scorching_ray"]["paint"])
    pivot = (5.0, 26.0)
    sp.copies = [about(pivot, -26.0, anchor, 0.86, 1), about(pivot, 26.0, anchor, 0.86, 1)]
    sp.place = about(pivot, 0.0, anchor, 0.9)


# --- Shadow Bolt -----------------------------------------------------------------------------------

@card("shadow_bolt_withering_mark")
def _(sp, c):
    """Withering Mark: everything it touches left far more exposed - a broken target in the ring."""
    reset(sp)

    def mark(cv, x):
        d = E["darkness"]
        for (a, b) in (((16, 7), (16, 12)), ((16, 20), (16, 25)), ((7, 16), (12, 16)), ((20, 16), (25, 16))):
            cv.line(a[0], a[1], b[0], b[1], d[1])
        cv.set(16, 16, d[0])
        zig(cv, (12, 11), (20, 21), d[2], 1.0, 4)
    O(sp, mark)


@card("shadow_bolt_creeping_rot")
def _(sp, c):
    """Creeping Rot: deeper poison, a heavier drag."""
    reset(sp)
    sp.place = Place(0.86, dy=-3)
    U(sp, lambda cv, x: m_pool(cv, E["poison"], 26.0, 12.0, 3.4))


@card("shadow_bolt_spreading_dark")
def _(sp, c):
    """Spreading Dark: the curse splits at every jump and goes further."""
    reset(sp)
    sp.place = Place(0.6, at=(11.0, 11.0))
    sp.copies = [Place(0.36, at=(25.0, 10.0), darken=1), Place(0.36, at=(23.0, 24.0), darken=1),
                 Place(0.36, at=(9.0, 25.0), darken=1)]
    U(sp, lambda cv, x: [m_trail(cv, E["darkness"], (11, 11), p, 2) for p in ((25, 10), (23, 24), (9, 25))])


@card("shadow_bolt_anathema")
def _(sp, c):
    """Anathema: the mark become a sentence - a sigil burnt in the ring."""
    reset(sp)
    sp.place = Place(1.1)

    def sigil(cv, x):
        d = E["darkness"]
        pts = [(C0 + math.cos(math.radians(-90 + 144 * k)) * 6.0, C0 + math.sin(math.radians(-90 + 144 * k)) * 6.0) for k in range(6)]
        for i in range(5):
            cv.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], d[1])
        cv.set(C0, C0, d[0])
    O(sp, sigil)
    U(sp, lambda cv, x: m_chain(cv, E["darkness"], (C0, C0), [(4, 5), (28, 27)], 1.4))


@card("shadow_bolt_plaguebearer")
def _(sp, c):
    """Plaguebearer: splits twice over and carries a rot of its own."""
    reset(sp)
    sp.place = Place(0.78, dy=-3)
    U(sp, lambda cv, x: m_chain(cv, E["poison"], (C0, 13), [(4, 22), (27, 21), (16, 28)], 1.5))
    O(sp, lambda cv, x: m_drops(cv, E["poison"], [(9, 22), (22, 23), (15, 26)]))


# --- Sleet Runes -----------------------------------------------------------------------------------

@card("sleet_runes_deep_rime")
def _(sp, c):
    reset(sp)
    sp.place = Place(1.0, dy=2)


@card("sleet_runes_rune_flurry")
def _(sp, c):
    """Rune Flurry: two more runes every cast."""
    reset(sp)
    sp.copies = [Place(0.62, at=(10.0, 9.0), darken=1), Place(0.62, at=(22.0, 22.0), darken=1)]
    sp.place = Place(0.8, at=(16.0, 15.5))


@card("sleet_runes_far_wind")
def _(sp, c):
    """Far Wind: the runes fly faster and further."""
    reset(sp)
    sp.place = Place(0.7, dx=4, dy=-3)
    U(sp, v_speed(4, 12))
    sp.back = "wind"


@card("sleet_runes_sigil_fan")
def _(sp, c):
    """Sigil Fan: every rune leaves at once, fanned wide."""
    reset(sp)
    anchor = anchor_of(SPELLS["sleet_runes"]["paint"])
    sp.copies = [about((7.0, 22.0), a, anchor, 0.62, 1) for a in (-40, -20, 20, 40)]
    sp.place = about((7.0, 22.0), 0, anchor, 0.7)


@card("sleet_runes_turning_gale")
def _(sp, c):
    """Turning Gale: the wind turns a rune back at the end of its flight."""
    reset(sp)
    sp.place = Place(0.74, dx=-2, dy=1)

    def turn(cv, x):
        w = E["wind"]
        for d in range(-150, 120, 6):
            t = math.radians(d)
            cv.set(16 + math.cos(t) * 11.5, 15 + math.sin(t) * 11.5, w[2] if d < 60 else w[1])
        t = math.radians(120)
        hx, hy = 16 + math.cos(t) * 11.5, 15 + math.sin(t) * 11.5
        for k in range(1, 4):
            cv.set(hx + k, hy - k * 0.2, w[1])
            cv.set(hx + k * 0.2, hy - k, w[1])
        cv.set(hx, hy, w[0])
    U(sp, turn)


# --- Solar Flare -----------------------------------------------------------------------------------

@card("solar_flare_radiance")
def _(sp, c):
    reset(sp)
    sp.place = Place(1.12)
    U(sp, v_cycle())


@card("solar_flare_scorching_heat")
def _(sp, c):
    """Scorching Heat: the flare runs hot - into Fire."""
    sp.recolour = "fire"
    sp.keep_core = True


@card("solar_flare_blinding_flash")
def _(sp, c):
    """Blinding Flash: stuns and throws back."""
    reset(sp)
    sp.place = Place(0.76)
    O(sp, lambda cv, x: m_chevrons(cv, E["light"], (C0, C0), 10))
    O(sp, lambda cv, x: m_chevrons(cv, E["light"], (C0, C0), 10, True))


@card("solar_flare_daybreak")
def _(sp, c):
    reset(sp)
    sp.subject = daybreak


@card("solar_flare_corona_discharge")
def _(sp, c):
    """Corona Discharge: eight sunbeams thrown far past the flare."""
    reset(sp)
    sp.place = Place(0.66)

    def beams(cv, x):
        l = E["light"]
        for k in range(8):
            t = TAU * k / 8.0 + TAU / 16.0
            cv.line(C0 + math.cos(t) * 6, C0 + math.sin(t) * 6, C0 + math.cos(t) * 15, C0 + math.sin(t) * 15, l[1])
            cv.set(C0 + math.cos(t) * 14, C0 + math.sin(t) * 14, l[0])
    U(sp, beams)


# --- Spiritual Weapon ------------------------------------------------------------------------------

# Each form's milestones are its own (SpellData.Variants), and so is each icon's subject: the form's own
# weapon, drawn by spirit_weapons.py at the heading the card needs rather than turned after (a weapon
# rasterised at its heading keeps its pixels). The colossal Ascensions draw the colossal weapon.

def weapons(layers, box=26.0):
    """Several Spiritual Weapons as one subject: `layers` is [(kind, angle, (gx, gy) grip, scale)], fitted
    to `box` and centred (spirit_weapons.group)."""
    @image
    def f():
        c = _Real(CELL, CELL, None)
        over(c, sw.group(layers, box=box))
        return c
    return f


def m_slash(c, ramp, cx, cy, r, a0, a1, width=2):
    """A blow's arc: the path the head swept, cooling behind it and white-hot where it lands."""
    core, hot, mid, edge = ramp
    steps = max(10, int(abs(a1 - a0) / 3.0))
    for i in range(steps + 1):
        t = i / float(steps)
        a = math.radians(a0 + (a1 - a0) * t)
        for k in range(width):
            c.set(cx + math.cos(a) * (r - k), cy + math.sin(a) * (r - k), edge if t < 0.35 else mid if t < 0.75 else hot)
    a = math.radians(a1)
    c.set(cx + math.cos(a) * r, cy + math.sin(a) * r, core)


# The axe: heavy, a cleave round its quarry, a throw.

@card("spiritual_weapon_axe_broadaxe")
def _(sp, c):
    """Broadaxe: a broader blade - the axe at its biggest, the impact thrown out wide round it."""
    reset(sp)
    sp.subject = weapon("axe", box=29.0)
    U(sp, v_rays(9.0, 14))
    sp.why = "a wider cleave"


@card("spiritual_weapon_axe_headsman")
def _(sp, c):
    """Headsman: the axe coming down, the stroke behind it and a glint where it falls on the wounded."""
    reset(sp)
    sp.subject = weapon("axe", box=22.0, angle=60.0)
    U(sp, lambda cv, x: m_slash(cv, E["arcane"], 13.0, 18.0, 12.0, 200.0, 330.0, 2))
    O(sp, lambda cv, x: m_glint(cv, 21, 24, 3, E["light"]))
    sp.why = "doubles on the wounded"


@card("spiritual_weapon_axe_dawnsplitter")
def _(sp, c):
    """Dawnsplitter: the axe in the dawn - Light's night, its rays behind the blade."""
    reset(sp)
    sp.subject = weapon("axe", box=24.0)
    U(sp, lambda cv, x: m_rays(cv, E["light"], (C0, C0), 7.0, 14.5, 10))
    sp.why = "into light: weakens"


@card("spiritual_weapon_axe_executioners_greataxe")
def _(sp, c):
    """Executioner's Greataxe: the colossal form - the double-bitted greataxe, and the ground shaken."""
    reset(sp)
    sp.subject = weapon("greataxe", box=29.0)
    U(sp, v_shock(2))
    sp.why = "colossal greataxe"


@card("spiritual_weapon_axe_whirling_axes")
def _(sp, c):
    """Whirling Axes: three axes spun out from one centre, and the turn they spin on."""
    reset(sp)
    sp.subject = weapons([("axe", a, (16.0 + math.cos(math.radians(a)) * 2.0, 16.0 + math.sin(math.radians(a)) * 2.0), 0.6)
                          for a in (-90.0, 30.0, 150.0)], box=28.0)
    U(sp, lambda cv, x: m_cycle(cv, E["arcane"], (C0, C0), 13.0))
    sp.why = "more axes, spinning"


# The hammer: a slam that shakes the ground and slows.

@card("spiritual_weapon_hammer_aftershock")
def _(sp, c):
    """Aftershock: the slam, and the second wave running out after it."""
    reset(sp)
    sp.subject = weapon("hammer", box=20.0)
    U(sp, v_shock(3))
    sp.why = "a second shockwave"


@card("spiritual_weapon_hammer_earthshaker")
def _(sp, c):
    """Earthshaker: the hammer come down head first, the ground torn open under it."""
    reset(sp)
    sp.subject = weapon("hammer", box=24.0, angle=100.0)
    sp.place = Place(1.0, dy=-3.0)
    O(sp, lambda cv, x: m_crack(cv, E["arcane"], 26))
    sp.why = "a wider, slower slam"


@card("spiritual_weapon_hammer_hallowed_ground")
def _(sp, c):
    """Hallowed Ground: the hammer over a pool of holy light that it leaves where it lands."""
    reset(sp)
    sp.subject = weapon("hammer", box=21.0)
    sp.place = Place(1.0, dy=-3.0)
    U(sp, v_pool(25.0, 12.0, 3.6, element="light"))
    sp.why = "leaves holy ground"


@card("spiritual_weapon_hammer_titans_maul")
def _(sp, c):
    """Titan's Maul: the colossal form - the maul, its shock, and the ice of what it stops dead."""
    reset(sp)
    sp.subject = weapon("maul", box=27.0)
    U(sp, v_shock(2))
    O(sp, lambda cv, x: m_freeze(cv, (C0, C0 + 2.0), 11.0))
    sp.why = "colossal maul; stops them"


@card("spiritual_weapon_hammer_hammerfall")
def _(sp, c):
    """Hammerfall: three hammers falling head first in turn, and the ground they break."""
    reset(sp)
    sp.subject = weapons([("hammer", 90.0, (7.0, 2.0), 0.55), ("hammer", 90.0, (25.0, 0.0), 0.55),
                          ("hammer", 90.0, (16.0, 6.0), 0.6)], box=27.0)
    sp.place = Place(1.0, dy=-2.0)
    O(sp, lambda cv, x: m_crack(cv, E["arcane"], 27))
    sp.why = "more hammers falling"


# The sword: fast, single, a guard against bolts.

@card("spiritual_weapon_sword_twin_blades")
def _(sp, c):
    """Twin Blades: two swords crossed."""
    reset(sp)
    sp.subject = weapons([("sword", -125.0, (24.0, 26.0), 0.62), ("sword", -55.0, (8.0, 26.0), 0.62)], box=27.0)
    sp.why = "a second sword"


@card("spiritual_weapon_sword_riposte")
def _(sp, c):
    """Riposte: a dark bolt struck back off the blade as a bright one."""
    reset(sp)
    sp.subject = weapon("sword", box=24.0, angle=-80.0)
    sp.place = Place(1.0, dx=3.0)

    def bounce(cv, x):
        d, a = E["darkness"], E["arcane"]
        cv.line(2, 22, 13, 17, d[2])
        cv.line(2, 23, 13, 18, d[3])
        cv.set(13, 17, d[1])
        cv.line(15, 15, 5, 5, a[1])
        cv.line(15, 16, 5, 6, a[2])
        cv.set(5, 5, a[0])
        m_glint(cv, 15, 16, 2, E["light"])
    O(sp, bounce)
    sp.why = "parries strike back"


@card("spiritual_weapon_sword_soul_siphon")
def _(sp, c):
    """Soul Siphon: the sword in Light's night, a heart drawn off its blade."""
    reset(sp)
    sp.subject = weapon("sword", box=24.0)
    sp.place = Place(1.0, dx=2.0, dy=-2.0)
    O(sp, lambda cv, x: m_heart(cv, 5, 18))
    sp.why = "into light: heals"


@card("spiritual_weapon_sword_celestial_greatsword")
def _(sp, c):
    """Celestial Greatsword: the colossal form - the winged greatsword, lit like the sun."""
    reset(sp)
    sp.subject = weapon("greatsword", box=29.0)
    U(sp, lambda cv, x: m_rays(cv, E["light"], (C0, C0), 8.0, 14.5, 12))
    sp.why = "colossal greatsword"


@card("spiritual_weapon_sword_thousand_cuts")
def _(sp, c):
    """Thousand Cuts: the sword and the three cuts of one swing."""
    reset(sp)
    sp.subject = weapon("sword", box=20.0, angle=-35.0)
    sp.place = Place(1.0, dx=2.0, dy=2.0)

    def cuts(cv, x):
        for (r, a0, a1) in ((13.0, 190.0, 260.0), (10.0, 200.0, 280.0), (7.0, 210.0, 300.0)):
            m_slash(cv, E["arcane"], 18.0, 18.0, r, a0, a1, 1)
    U(sp, cuts)
    sp.why = "a flurry of cuts"


# --- Starfall --------------------------------------------------------------------------------------

@card("starfall_clear_night")
def _(sp, c):
    """Clear Night: the sky clears and more stars come through."""
    reset(sp)
    O(sp, lambda cv, x: [si._falling_star(cv, px, py, ln, E["light"], 1) for (px, py, ln) in ((17, 6, 5), (27, 15, 5), (6, 6, 4))])


@card("starfall_frostfall")
def _(sp, c):
    pass


@card("starfall_downpour")
def _(sp, c):
    """Downpour: the drizzle turns to a downpour of smaller stars."""
    reset(sp)
    sp.place = Place(0.7)

    def rain(cv, x):
        l = E["light"]
        for (px, py) in ((5, 6), (12, 3), (20, 4), (27, 9), (24, 17), (8, 15), (15, 11), (28, 25), (4, 23), (12, 27)):
            cv.line(px, py, px - 2, py - 3, l[3])
            cv.set(px, py, l[1])
            cv.set(px + 1, py, l[0])
    U(sp, rain)


@card("starfall_starburst")
def _(sp, c):
    """Starburst: each star swells and explodes, throwing sparks."""
    reset(sp)
    sp.place = Place(0.74, dx=-4, dy=-4)
    O(sp, lambda cv, x: m_blast(cv, E["light"], 21, 21, 8.0))
    O(sp, lambda cv, x: [m_glint(cv, px, py, 1, E["light"]) for (px, py) in ((27, 12), (11, 27), (28, 27))])


@card("starfall_constellation")
def _(sp, c):
    reset(sp)
    sp.subject = constellation


# --- Stormsweep ------------------------------------------------------------------------------------

@card("stormsweep_high_voltage")
def _(sp, c):
    """High Voltage: every sweep reaches further and wider."""
    reset(sp)
    sp.place = Place(1.12, dx=1, dy=-1)


@card("stormsweep_iron_current")
def _(sp, c):
    pass


@card("stormsweep_rapid_discharge")
def _(sp, c):
    """Rapid Discharge: a shorter cooldown, the sweep crackling through faster - arrows going round."""
    reset(sp)
    sp.place = Place(0.8, dx=1, dy=-1)
    U(sp, v_cycle())


@card("stormsweep_thunderclap")
def _(sp, c):
    """Thunderclap: every arc sweeps twice, and ends in a thunderclap."""
    reset(sp)
    anchor = anchor_of(SPELLS["stormsweep"]["paint"])
    sp.copies = [about((7.0, 24.0), 0.0, anchor, 0.72, 1)]
    sp.place = about((7.0, 24.0), 0.0, anchor, 0.96)
    O(sp, lambda cv, x: m_blast(cv, E["lightning"], x.tip[0], x.tip[1], 6.0))


@card("stormsweep_cardinal_storm")
def _(sp, c):
    reset(sp)
    sp.subject = cardinal_storm


# --- Thorned Vine ----------------------------------------------------------------------------------
# Its five cards each change what the vine does (2026-10-29): thorns left in what it cuts, a second
# stroke back, a coil round the feet, a crescent of brambles, three heads from one root.

VINE_ROOT = (3.5, 28.5)


@card("thorn_vine_amplified_impact")
def _(sp, c):
    """Barbed Lash: the thorns break off in what it cuts, and the barbed bleed sap as they walk."""
    reset(sp)
    sp.place = Place(0.8, dx=-3, dy=3)

    def barbs(cv, x):
        for (x0, y0, x1, y1) in ((17, 15, 23, 19), (21, 10, 27, 13), (12, 20, 17, 25)):
            dart(cv, E["grass"], x0, y0, x1, y1)
        m_drops(cv, E["poison"], ((22, 24), (26, 21), (19, 27)))
    O(sp, barbs)


@card("thorn_vine_hyper_acceleration")
def _(sp, c):
    """Snapback: the whip cracks out and back - a second stroke over the same arc."""
    reset(sp)
    sp.copies = [Place(0.78, rot=-90.0, darken=1)]
    sp.place = Place(0.78)

    def there_and_back(cv, x):
        g = E["grass"]
        pts = []
        for i in range(25):
            a = math.radians(-160 + 140 * i / 24.0)
            pts.append((C0 + math.cos(a) * 12.0, C0 + 2 + math.sin(a) * 11.0))
        for (px, py) in pts:
            cv.set(px, py, g[2])
        for (hx, hy), (bx, by) in ((pts[0], pts[2]), (pts[-1], pts[-3])):
            tx, ty = _unit((hx - bx, hy - by))
            nx, ny = -ty, tx
            for k in range(3):
                cv.set(hx - tx * k + nx * k, hy - ty * k + ny * k, g[1])
                cv.set(hx - tx * k - nx * k, hy - ty * k - ny * k, g[1])
            cv.set(hx, hy, g[0])
    O(sp, there_and_back)


@card("thorn_vine_reactive_catalyst")
def _(sp, c):
    """Entangle: the vine coils round the feet of what it cuts and roots it where it stands."""
    reset(sp)
    sp.back = "poison"
    sp.place = Place(0.74, dx=4, dy=-4)

    def coil(cv, x):
        g = E["grass"]
        cv.disc(10.5, 18.5, 2.8, 3.2, OCC)
        cv.rect(8, 19, 13, 24, OCC)
        cv.set(9.5, 18, E["darkness"][1])
        cv.set(11.5, 18, E["darkness"][1])
        cv.ring(10.5, 24.5, 6.5, 2.6, OCC, 2)
        cv.ring(10.5, 24.5, 5.5, 2.0, g[3], 1)
        for (px, py) in ((6, 23), (9, 22), (13, 22), (15, 24)):
            cv.set(px, py, g[1])
        for (px, py) in ((4, 25), (17, 23), (12, 27)):
            cv.set(px, py, g[0])
    O(sp, coil)


@card("thorn_vine_titan_cataclysm")
def _(sp, c):
    """Thicket: where the whip lands, a crescent of thorny brambles stays on the ground."""
    reset(sp)
    sp.place = Place(0.66, dx=-6, dy=6)

    def thicket(cv, x):
        g = E["grass"]
        p = E["poison"]
        for (r, tone, ph) in ((10.0, g[3], 0.0), (13.5, g[2], 1.7), (16.5, g[3], 3.1)):
            for d in range(-92, 4, 3):
                t = math.radians(d)
                wob = math.sin(d * 0.21 + ph) * 1.2
                px, py = 7 + math.cos(t) * (r + wob), 25 + math.sin(t) * (r + wob)
                cv.set(px, py, tone)
                if (d // 3) % 4 == 0:
                    cv.set(px + math.cos(t) * 1.5, py + math.sin(t) * 1.5, g[1])
        for (px, py) in ((17, 13), (22, 19), (12, 10), (23, 25)):
            cv.set(px, py, p[1])
            cv.set(px, py + 1, p[3])
    O(sp, thicket)


@card("thorn_vine_infinite_horizon")
def _(sp, c):
    """Hydra Vine: one root splits into three heads, each lashing its own third of the ring round it."""
    reset(sp)
    s = 0.62
    ax, ay = anchor_of(SPELLS["thorn_vine"]["paint"])
    rx, ry = VINE_ROOT
    places = []
    for k, a in enumerate((120.0, 240.0, 0.0)):
        t = math.radians(a)
        ox, oy = (ax - rx) * s, (ay - ry) * s
        at = (C0 + ox * math.cos(t) - oy * math.sin(t), C0 + 1 + ox * math.sin(t) + oy * math.cos(t))
        places.append(Place(s, rot=a, at=at, darken=1 if k < 2 else 0))
    set_volley(sp, places)

    def root(cv, x):
        e = E["earth"]
        cv.disc(C0, C0 + 1, 2.6, 2.0, OCC)
        cv.set(C0 - 1, C0, e[2])
        cv.set(C0 + 1, C0 + 1, e[3])
    O(sp, root)


# --- Toxic Spore Burst -----------------------------------------------------------------------------

@card("toxic_spore_lingering_rot")
def _(sp, c):
    """Lingering Rot: the burst leaves its cloud hanging."""
    reset(sp)
    sp.place = Place(0.86, dy=-2)
    U(sp, lambda cv, x: m_pool(cv, E["poison"], 25.0, 13.0, 4.5))


@card("toxic_spore_ember_caps")
def _(sp, c):
    """Ember Caps: the spores trade Poison for Fire."""
    pass


@card("toxic_spore_giant_caps")
def _(sp, c):
    """Giant Caps: two fewer mushrooms, each far bigger - a pair of big caps under the spores."""
    reset(sp)
    sp.place = Place(0.56, at=(C0, 8.5))

    def caps(cv, x):
        small_cap(cv, 10, 29, 6, 6.5)
        small_cap(cv, 22, 29, 8, 7.5)
    O(sp, caps)


@card("toxic_spore_doomcap")
def _(sp, c):
    reset(sp)
    sp.subject = doomcap
    U(sp, v_shock(1))


@card("toxic_spore_mycelial_bloom")
def _(sp, c):
    """Mycelial Bloom: mycelium runs out from every burst and more mushrooms sprout past it."""
    reset(sp)
    sp.place = Place(0.78, dy=-3)

    def bloom(cv, x):
        g = E["grass"]
        for (x0, x1, y) in ((3, 14, 27), (18, 29, 27), (6, 12, 29), (20, 26, 29)):
            for xx in range(x0, x1 + 1):
                cv.set(xx, y + (1 if xx % 3 == 0 else 0), g[2] if xx % 2 else g[3])
        small_cap(cv, 5, 27, 4, 3.2)
        small_cap(cv, 27, 27, 5, 3.5)
    O(sp, bloom)


# --- Void Lance ------------------------------------------------------------------------------------

@card("void_lance_abyssal_pierce")
def _(sp, c):
    """Abyssal Pierce: the lance hangs open, cutting what walks into it."""
    reset(sp)
    sp.place = Place(0.86, dx=1, dy=-3)
    U(sp, lambda cv, x: m_pool(cv, E["darkness"], 26.0, 12.0, 3.4))
    U(sp, lambda cv, x: zig(cv, (6, 26), (26, 26), E["arcane"][1], 0.8, 6, E["arcane"][0]))


@card("void_lance_rift_blade")
def _(sp, c):
    """Rift Blade: a longer, wider lance cleaving wide paths."""
    reset(sp)
    sp.place = Place(1.25)

    def cleave(cv, x):
        a = E["arcane"]
        for d in range(120, 330, 4):
            t = math.radians(d)
            cv.set(16 + math.cos(t) * 14, 16 + math.sin(t) * 14, a[2] if d < 250 else a[1])
    U(sp, cleave)


@card("void_lance_gravity_spike")
def _(sp, c):
    reset(sp)
    sp.place = Place(0.92)
    U(sp, v_vortex(3, 2.8))


@card("void_lance_annihilation_beam")
def _(sp, c):
    reset(sp)
    sp.subject = annihilation_beam


@card("void_lance_cosmic_collapse")
def _(sp, c):
    """Cosmic Collapse: the lance ends in a void burst."""
    reset(sp)
    sp.place = Place(0.8, dx=-3, dy=3)

    def burst(cv, x):
        d = E["darkness"]
        cv.disc(23, 9, 7.5, 7.5, d[3])
        cv.disc(23, 9, 5.5, 5.5, d[2])
        cv.disc(23, 9, 3.5, 3.5, OCC)
        cv.ring(23, 9, 3.8, 3.8, d[0])
        for k in range(8):
            t = TAU * k / 8.0
            cv.line(23 + math.cos(t) * 8, 9 + math.sin(t) * 8, 23 + math.cos(t) * 11, 9 + math.sin(t) * 11, d[1])
    O(sp, burst)


def spec_for(card_data):
    sp = derive(card_data)
    fix = OVERRIDES.get(card_data["id"])
    if fix:
        fix(sp, card_data)
    return sp


# =============================================================================================
# Checks, output and the contact sheet
# =============================================================================================

def difference(a, b):
    """The share of the pixels either icon covers that the two do not agree on."""
    differ = 0
    covered = 0
    for y in range(CELL):
        for x in range(CELL):
            p, q = a.get(x, y), b.get(x, y)
            if not p[3] and not q[3]:
                continue
            covered += 1
            if p != q:
                differ += 1
    return differ / float(max(1, covered))


def base_icon(spell):
    return (BASE.get(spell) or SPELLS[spell]["paint"])()


def load_cards():
    with open(CATALOG) as f:
        return json.load(f)["cards"]


# The ground each kind of card's icon is shown on in the game (CardFamilyArt): the new spell's page,
# the mutation's teal roundel, the obsidian plate's glass, the ember plate.
GROUNDS = {"base": (242, 220, 196), 4: (22, 40, 46), 8: (11, 36, 54), 9: (22, 7, 10)}


def write_sheet(rows, path, zoom=2):
    from PIL import Image, ImageDraw
    cell = CELL * zoom
    pad = 6
    label_h = 14
    cols = max(len(r[1]) for r in rows) + 1
    name_w = 120
    W = name_w + cols * (cell + pad) + pad
    H = len(rows) * (cell + label_h + pad) + pad
    sheet = Image.new("RGBA", (W, H), (30, 30, 36, 255))
    draw = ImageDraw.Draw(sheet)
    for i, (spell, items) in enumerate(rows):
        y = pad + i * (cell + label_h + pad)
        draw.text((6, y + cell // 2 - 6), spell, fill=(230, 230, 230, 255))
        entries = [("spell", "base", base_icon(spell))] + items
        for j, (label, tier, cv) in enumerate(entries):
            x = name_w + pad + j * (cell + pad)
            sheet.paste(Image.new("RGBA", (cell, cell), GROUNDS[tier] + (255,)), (x, y))
            sheet.alpha_composite(cv.img.resize((cell, cell), Image.NEAREST), (x, y))
            draw.text((x + 1, y + cell + 1), label[:11], fill=(200, 200, 200, 255))
    sheet.convert("RGB").save(path)


def main(argv):
    only = None
    for a in argv:
        if a.startswith("--only="):
            only = set(a[len("--only="):].split(","))
    cards = load_cards()
    problems = []
    for c in cards:
        if subject_of(c) not in SPELLS:
            problems.append("%s: its spell %s has no entry in SPELLS" % (c["id"], subject_of(c)))
    cards = [c for c in cards if subject_of(c) in SPELLS]

    made = {}
    for c in cards:
        if only and c["id"] not in only and c["spell"] not in only and subject_of(c) not in only:
            continue
        cv = compose(c, spec_for(c))
        bad = cv.audit()
        if bad:
            problems.append("%s: off-contract colours %s" % (c["id"], ", ".join(h for h, _ in bad[:5])))
        made[c["id"]] = (c, cv)

    # Every icon its own picture: unlike its spell's, and unlike each sibling of its milestone.
    bases = {}
    for cid, (c, cv) in sorted(made.items()):
        key = subject_of(c)
        if key not in bases:
            bases[key] = base_icon(key)
        d = difference(cv, bases[key])
        if d < MIN_DIFFERENCE:
            problems.append("%s: only %.0f%% different from %s's own icon" % (cid, d * 100, key))
    ids = sorted(made)
    for i, a in enumerate(ids):
        for b in ids[i + 1:]:
            ca, cb = made[a][0], made[b][0]
            if subject_of(ca) != subject_of(cb) or ca["milestone"] != cb["milestone"]:
                continue
            d = difference(made[a][1], made[b][1])
            if d < MIN_DIFFERENCE:
                problems.append("%s and %s: only %.0f%% different" % (a, b, d * 100))

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    for cid, (c, cv) in sorted(made.items()):
        cv.scaled(2).save(os.path.join(OUT_DIR, cid + ".png"))
    print("%d evolution icons written to %s" % (len(made), os.path.relpath(OUT_DIR, ROOT)))

    if "--no-sheet" not in argv:
        order = []
        for c in cards:
            if c["id"] in made and subject_of(c) not in order:
                order.append(subject_of(c))
        rows = []
        for spell in order:
            mine = sorted((c for c in cards if subject_of(c) == spell and c["id"] in made), key=lambda c: c["milestone"])
            rows.append((spell, [(c["id"][len(spell) + 1:] if c["id"].startswith(spell + "_") else c["id"],
                                  c["milestone"], made[c["id"]][1]) for c in mine]))
        write_sheet(rows, SHEET)
        print("contact sheet: %s" % os.path.relpath(SHEET, ROOT))

    for p in problems:
        print("PROBLEM " + p)
    if problems:
        print("%d problems" % len(problems))
        return 1
    print("palette clean; every icon differs from its spell's and its siblings' in at least %d%% of its pixels"
          % int(MIN_DIFFERENCE * 100))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
