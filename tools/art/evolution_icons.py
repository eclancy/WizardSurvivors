# -*- coding: utf-8 -*-
"""Every evolution's own icon. Run: python tools/art/evolution_icons.py [--only=id,id] [--no-sheet]

THE REQUEST (Eric): "mutations and ascensions should all have their own unique altered icons. for
example, rimehound should not just be a blue version of the icon, it should be a similar dog icon with
an icy background and some icicles added to the screen, etc". Until this generator every level 4
mutation, level 8 Ascension and Apotheosis form was shown with its SPELL's icon, tinted by the card's
ModulateColor - Rimehound was the Familiar in blue, Titan Cataclysm the same picture as Amplified
Impact. Now each evolution id has a picture of its own at
assets/bonelight/ui/evolutions/<evolution id>.png, which scripts/EvolutionIcons.cs loads wherever an
evolution is shown, and RegressionChecks.ValidateEvolutionIcons reports any id without one.

WHAT AN EVOLUTION ICON IS. The spell's own subject - drawn by the SAME painter as the spell's icon
(spell_icons.py, spell_icons_core.py, familiar.py), never copied out of a PNG - altered to show what
the card does, in layers:

  1. the frame of its milestone: a level 4 mutation is a disc of its element's night with a rim of the
     element's light; a level 8 Ascension the same disc in a gold ring with four gem studs, so it reads
     as the bigger thing it is; an Apotheosis a smaller disc in a double gold ring with a crown, on a
     burst of rays in its element that fills the corners - the grandest object in the set.
  2. the backdrop's element: the element the card GAINS, else the spell's first element. Each has a
     texture in the night's own tones (frost crystals, embers, bubbles, strata, a rune ring...).
  3. motifs drawn UNDER the subject for what the card does: speed lines and afterimages for a faster
     card, a vortex for a pull, a pool for a lingering zone, impact rays and a shockwave for a bigger one.
  4. the subject, transformed: grown for Area, colossal for a Titan, multiplied for more projectiles,
     re-lit in a new element's ramp for an element path (the ramp remap the game itself does, e.g.
     Hellbreath's black flame), or swapped for a hand-drawn form (Cerberus's three heads).
  5. motifs drawn OVER it: the element's accent for a card that gains one (icicles hanging from the
     rim for Ice, flame tongues for Fire, a wave for Water, sparks for Lightning, drips for Poison,
     a vine for Grass, rocks for Earth, tendrils for Darkness, glints for Light, motes for Arcane,
     rivets for Metal, gusts for Wind), and the card's verb (a heart for lifesteal, roots for a root,
     a crit glint, a blast at the tip, a fork, chevrons for knockback).

THE SPEC TABLE. Every card gets a spec DERIVED FROM ITS DATA (derive(): the element it gains, its
stat multipliers, its effect and rider), so a new card is covered the moment it is in the catalog;
OVERRIDES then hand-writes the cards whose verb the numbers cannot say (Cerberus, Supernova, the
Constellation, every Apotheosis form). The data is tools/art/evolution_catalog.json, the catalog as
the game builds it - written by `scenes/_AscensionAudit.tscn -- --json=tools/art/evolution_catalog.json`,
because ids like `<spell>_titan_cataclysm` are generated in C# and only C# can list them. When a card is
added or renamed: re-run that, then this.

CHECKS, before anything is written: every pixel is a contract colour (raster.Canvas.audit, every
tone from bonelight.py), every icon differs from its spell's icon and from each of its siblings by at
least MIN_DIFFERENCE of its pixels, and every catalog id has a spec. The contact sheet
(tools/art/_evolution_icons_sheet.png, gitignored) lays each spell's icon beside all of its
evolutions, each on the ground it is shown on in the game (the mutation roundel's teal, the
obsidian plate, the ember plate).

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
import spell_icons_core as sc

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

# The share of an icon's pixels that must differ from its spell's icon and from every sibling. A
# card that only recoloured its spell measured about 0.3 here, so the floor is above that.
MIN_DIFFERENCE = 0.30

_Real = raster.Canvas


# =============================================================================================
# The subject: the spell's own painter, drawn through a transform
# =============================================================================================

class XCanvas(_Real):
    """A Canvas that draws every primitive through one map: scale about the cell centre, shift, and
    optionally mirror left to right.

    The painters build their pictures out of primitives (discs, polygons, lines, single pixels), so
    redrawing them through a map RE-RASTERISES the shapes at the new size - a disc grown 1.4 times is
    a crisp bigger disc on the same one-pixel grid, not a blurred or doubled one, which is the grid
    rule of art-direction.md section 1 kept. Pixel-level primitives (set, line, rect and blit, which
    all come through set) fill the block their source pixel maps to when growing, so a wall of stakes
    drawn pixel by pixel grows without gaps; when shrinking the last write wins, which for a painter
    is its top layer. Areas (disc, ring, poly, poly_shade, radial) are drawn straight at the mapped
    coordinates. The key light stays upper left when mirrored, because poly_shade takes its light
    angle unmapped.
    """

    def __init__(self, w, h, bg=None, s=1.0, dx=0.0, dy=0.0, mirror=False):
        _Real.__init__(self, w, h, bg)
        self.s = s
        self.dx = dx
        self.dy = dy
        self.mirror = mirror
        self.raw = False

    def X(self, x):
        if self.mirror:
            x = self.w - 1 - x
        return C0 + (x - C0) * self.s + self.dx

    def Y(self, y):
        return C0 + (y - C0) * self.s + self.dy

    def set(self, x, y, c):
        if c is None:
            return
        if self.raw:
            return _Real.set(self, int(x), int(y), c)
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return
        sx, sy = int(x), int(y)
        if self.s >= 1.0:
            xa, xb = sorted((self.X(sx - 0.5), self.X(sx + 0.5)))
            x0 = int(math.floor(xa + 0.5))
            x1 = max(x0, int(math.floor(xb + 0.5)) - 1)
            y0 = int(math.floor(self.Y(sy - 0.5) + 0.5))
            y1 = max(y0, int(math.floor(self.Y(sy + 0.5) + 0.5)) - 1)
            for yy in range(y0, y1 + 1):
                for xx in range(x0, x1 + 1):
                    _Real.set(self, xx, yy, c)
        else:
            _Real.set(self, int(math.floor(self.X(sx) + 0.5)), int(math.floor(self.Y(sy) + 0.5)), c)

    def _raw(self, fn, *args, **kw):
        self.raw = True
        try:
            fn(self, *args, **kw)
        finally:
            self.raw = False

    def disc(self, cx, cy, rx, ry, c):
        self._raw(_Real.disc, self.X(cx), self.Y(cy), rx * self.s, ry * self.s, c)

    def ring(self, cx, cy, rx, ry, c, thick=1):
        self._raw(_Real.ring, self.X(cx), self.Y(cy), rx * self.s, ry * self.s, c, max(1, int(round(thick * self.s))))

    def radial(self, cx, cy, rx, ry, colors):
        self._raw(_Real.radial, self.X(cx), self.Y(cy), rx * self.s, ry * self.s, colors)

    def poly(self, pts, c):
        self._raw(_Real.poly, [(self.X(x), self.Y(y)) for (x, y) in pts], c)

    def poly_shade(self, pts, colors, ang=-0.7854, bias=0.0, gamma=1.0):
        self._raw(_Real.poly_shade, [(self.X(x), self.Y(y)) for (x, y) in pts], colors, ang=ang, bias=bias, gamma=gamma)


def paint(fn, s=1.0, dx=0.0, dy=0.0, mirror=False):
    """Calls a spell painter with every canvas it makes drawn through the map.

    Only the FIRST canvas it makes is mapped - the one it returns. A painter that builds a part on a
    canvas of its own and lays it over the first (the Familiar's head, Sleet Runes' rune) gets a plain
    canvas for the part, which is then mapped once when it is laid down, never twice.
    """
    made = []

    def factory(w, h, bg=None):
        cv = XCanvas(w, h, bg, s, dx, dy, mirror) if not made else _Real(w, h, bg)
        made.append(cv)
        return cv

    raster.Canvas = factory
    try:
        return fn()
    finally:
        raster.Canvas = _Real


def place(src, s=1.0, dx=0.0, dy=0.0, mirror=False):
    """A finished canvas (one the painters cannot redraw, like a familiar.Fig render) laid through
    the map. Growing fills each pixel's block; shrinking votes, so a 2x2 block becomes its commonest
    opaque colour rather than whichever pixel happened to be scanned last."""
    if s >= 1.0:
        out = XCanvas(CELL, CELL, None, s, dx, dy, mirror)
        out.blit(src.img, 0, 0)
        return out
    probe = XCanvas(CELL, CELL, None, s, dx, dy, mirror)
    votes = {}
    total = {}
    for y in range(src.h):
        for x in range(src.w):
            tx = int(math.floor(probe.X(x) + 0.5))
            ty = int(math.floor(probe.Y(y) + 0.5))
            total[(tx, ty)] = total.get((tx, ty), 0) + 1
            p = src.get(x, y)
            if p[3] == 0:
                continue
            v = votes.setdefault((tx, ty), {})
            v[p] = v.get(p, 0) + 1
    out = _Real(CELL, CELL, None)
    for (tx, ty), v in votes.items():
        opaque = sum(v.values())
        if opaque * 2 < total[(tx, ty)]:
            continue
        # The commonest colour; a tie goes to the brighter, so a one-pixel highlight survives.
        best = max(v.items(), key=lambda kv: (kv[1], raster._lum(kv[0])))[0]
        out.set(tx, ty, best)
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


def centre_of(cv):
    x0, y0, x1, y1 = bbox(cv)
    return ((x0 + x1) / 2.0, (y0 + y1) / 2.0)


def over(dst, src, dx=0, dy=0):
    """src laid over dst wherever src has a pixel."""
    for y in range(src.h):
        for x in range(src.w):
            p = src.get(x, y)
            if p[3]:
                dst.set(x + dx, y + dy, p)


# --- recolouring --------------------------------------------------------------------------------

def _ramp_table():
    """hex -> (row name, index, length) for every element ramp and material row."""
    t = {}
    for name, tones in E.items():
        for i, h in enumerate(tones):
            t.setdefault(h.upper(), (name, i, 4))
    for name, tones in M.items():
        for i, h in enumerate(tones):
            t.setdefault(h.upper(), (name, i, 5))
    return t


RAMP = _ramp_table()
# A material's five stops onto an element's four: hi to core, lit to hot, base to mid, the two dark
# stops to the edge.
MAT_TO_EL = [0, 1, 2, 3, 3]


def _hex(p):
    return "#%02X%02X%02X" % (p[0], p[1], p[2])


def recolour(cv, rows, element, keep_core=False):
    """Every pixel of the named rows (element ramps or material rows) re-lit in `element`'s ramp -
    the remap the game does when a card changes a spell's element (Hellbreath's black flame,
    Rimelight's cold ward). keep_core leaves the white-hot core and hot stops alone, so only the
    identity (mid and edge, art-direction.md section 3) moves."""
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
    """Every pixel one stop down its own row - a copy standing behind the one in front."""
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


def aura(dst, cv, colour, inside=None):
    """A one-pixel glow of `colour` round cv's silhouette, on dst - the subject's light on the night
    behind it, which is also what separates a dark subject from a dark backdrop."""
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.get(x, y)[3]:
                continue
            near = False
            for (ox, oy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = cv.get(x + ox, y + oy) if 0 <= x + ox < cv.w and 0 <= y + oy < cv.h else (0, 0, 0, 0)
                if q[3]:
                    near = True
                    break
            if near and (inside is None or inside(x, y)):
                dst.set(x, y, colour)


# =============================================================================================
# The milestone frame and the element backdrop
# =============================================================================================

# Each element's night: the three stops of the disc's glow (lit centre to edge) and the rim (lit arc,
# shaded arc). The night is the element's MATERIAL kin a long way down, so the subject - drawn in the
# emissive ramps - is always the brightest thing in the icon.
BACK = {
    "fire":      dict(glow=[M["ember"][4], M["red"][3], M["red"][4]], rim=E["fire"][3], lit=E["fire"][2]),
    "ice":       dict(glow=[M["azure"][3], M["azure"][4], M["wool"][4]], rim=E["ice"][3], lit=E["ice"][1]),
    "water":     dict(glow=[M["arcane"][3], M["arcane"][4], M["wool"][4]], rim=E["water"][3], lit=E["water"][2]),
    "lightning": dict(glow=[M["wool"][3], M["wool"][4], M["violet"][4]], rim=E["lightning"][3], lit=E["lightning"][1]),
    "arcane":    dict(glow=[M["violet"][2], M["violet"][3], M["violet"][4]], rim=E["arcane"][3], lit=E["arcane"][2]),
    "darkness":  dict(glow=[M["violet"][4], OCC, OCC], rim=E["darkness"][2], lit=E["darkness"][1]),
    "light":     dict(glow=[M["amber"][3], M["amber"][4], M["gold"][4]], rim=E["light"][3], lit=E["light"][1]),
    "grass":     dict(glow=[M["verdant"][3], M["verdant"][4], OCC], rim=E["grass"][3], lit=E["grass"][2]),
    "poison":    dict(glow=[M["linen"][3], M["linen"][4], OCC], rim=E["poison"][3], lit=E["poison"][2]),
    "earth":     dict(glow=[M["flesh"][3], M["flesh"][4], M["red"][4]], rim=E["earth"][3], lit=E["earth"][2]),
    "metal":     dict(glow=[M["steel"][3], M["steel"][4], M["stone"][4]], rim=E["metal"][3], lit=E["metal"][1]),
    "wind":      dict(glow=[M["lichen"][3], M["lichen"][4], M["stone"][4]], rim=E["wind"][3], lit=E["wind"][2]),
}
# The texture of each night: the night's own material, one or two stops lighter than its fill.
TEXTURE_ROW = {
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


def rim_angle_lit(x, y):
    """Faces the key (upper left)?"""
    a = math.atan2(y - C0, x - C0)
    return math.cos(a - math.radians(225.0))


def backdrop(c, element, r):
    b = BACK[element]
    g = b["glow"]
    # The glow sits a little up and left of centre, toward the key, so the disc reads as lit glass.
    c.radial(C0 - 1.5, C0 - 2.0, r + 2.5, r + 2.5, g)
    inside = in_disc(r)
    for y in range(CELL):
        for x in range(CELL):
            if not inside(x, y):
                c.px[x, y] = (0, 0, 0, 0)


def rim(c, element, r):
    b = BACK[element]
    for y in range(CELL):
        for x in range(CELL):
            d = math.hypot(x - C0, y - C0)
            if r - 1.0 < d <= r:
                c.set(x, y, b["lit"] if rim_angle_lit(x, y) > 0.35 else b["rim"])


def gold_ring(c, r, studs=None):
    """The Ascension's ring: two pixels of gold round the disc, lit on the key side, with an occlusion
    line inside it so the disc's own rim never runs into the gold."""
    g = M["gold"]
    for y in range(CELL):
        for x in range(CELL):
            d = math.hypot(x - C0, y - C0)
            f = rim_angle_lit(x, y)
            if r - 1.0 < d <= r:
                c.set(x, y, g[1] if f > 0.3 else (g[2] if f > -0.5 else g[3]))
            elif r - 2.0 < d <= r - 1.0:
                c.set(x, y, g[0] if f > 0.7 else (g[1] if f > -0.2 else g[2]))
            elif r - 3.0 < d <= r - 2.0:
                c.set(x, y, OCC)
    if studs:
        for (sx, sy) in ((C0, C0 - r + 1.0), (C0 + r - 1.0, C0), (C0, C0 + r - 1.0), (C0 - r + 1.0, C0)):
            gem(c, sx, sy, studs)


def gem(c, x, y, ramp):
    x = int(round(x))
    y = int(round(y))
    g = M["gold"]
    for (ox, oy) in ((0, -2), (2, 0), (0, 2), (-2, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)):
        c.set(x + ox, y + oy, g[3] if ox + oy > 0 else g[0])
    for (ox, oy) in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        c.set(x + ox, y + oy, ramp[2])
    c.set(x, y, ramp[0])
    c.set(x - 1, y - 1, g[0])


def sunburst(c, ramp, r0=12.0, r1=23.0, n=16):
    """The Apotheosis's burst: rays out of the disc to the corners of the cell, long and short in
    turn, each a wedge with a hot spine."""
    core, hot, mid, edge = ramp
    for k in range(n):
        a = TAU * k / n + TAU / (2 * n)
        long_ray = k % 2 == 0
        length = r1 if long_ray else r1 - 6.0
        half = 0.16 if long_ray else 0.12
        pts = [(C0 + math.cos(a - half) * r0, C0 + math.sin(a - half) * r0),
               (C0 + math.cos(a) * length, C0 + math.sin(a) * length),
               (C0 + math.cos(a + half) * r0, C0 + math.sin(a + half) * r0)]
        c.poly(pts, edge)
        inner = [(C0 + math.cos(a - half * 0.45) * r0, C0 + math.sin(a - half * 0.45) * r0),
                 (C0 + math.cos(a) * (length - 3.0), C0 + math.sin(a) * (length - 3.0)),
                 (C0 + math.cos(a + half * 0.45) * r0, C0 + math.sin(a + half * 0.45) * r0)]
        c.poly(inner, mid if long_ray else edge)
        if long_ray:
            c.line(C0 + math.cos(a) * r0, C0 + math.sin(a) * r0,
                   C0 + math.cos(a) * (length - 6.0), C0 + math.sin(a) * (length - 6.0), hot)


def crown(c, ramp):
    """Three gold points standing on the top of the Apotheosis ring, a gem of the element in each."""
    g = M["gold"]
    top = C0 - DISC_R[9] - 1.0
    for (cx, h) in ((C0 - 5.0, 3.0), (C0, 5.0), (C0 + 5.0, 3.0)):
        base = top + 2.0
        pts = [(cx - 2.2, base), (cx, base - h - 1.0), (cx + 2.2, base)]
        c.poly(pts, g[2])
        c.line(cx - 1, base - 1, cx, base - h, g[0])
        c.set(int(round(cx)), int(round(base - h - 1.0)), ramp[0])
    c.hline(int(C0 - 7), int(C0 + 7), int(top + 2), g[1])
    c.hline(int(C0 - 7), int(C0 + 7), int(top + 3), g[3])


# =============================================================================================
# Textures: the night's own grain, drawn inside the disc under everything
# =============================================================================================

def _row(element):
    return M[TEXTURE_ROW[element]]


def tex_ice(c, r):
    m = _row("ice")
    for (x, y, s) in ((7, 9, 2), (24, 8, 1), (9, 23, 1), (25, 22, 2), (16, 5, 1), (5, 16, 1)):
        for k in range(1, s + 1):
            for (ox, oy) in ((k, 0), (-k, 0), (0, k), (0, -k)):
                c.set(x + ox, y + oy, m[2])
        c.set(x, y, m[1])
        if s > 1:
            for (ox, oy) in ((1, 1), (-1, -1), (1, -1), (-1, 1)):
                c.set(x + ox, y + oy, m[3])


def tex_fire(c, r):
    m = _row("fire")
    for (x, y) in ((8, 8), (22, 6), (25, 14), (6, 18), (12, 4), (19, 10), (27, 20), (10, 13)):
        c.set(x, y, m[2] if (x + y) % 3 else m[1])


def tex_water(c, r):
    m = _row("water")
    for (x, y, rr) in ((8, 10, 1.6), (23, 7, 1.2), (25, 18, 1.8), (7, 21, 1.2), (14, 5, 1.0)):
        c.ring(x, y, rr, rr, m[2])
        c.set(x - 1, y - 1, m[1])


def tex_lightning(c, r):
    m = _row("lightning")
    pts = [(3, 9), (9, 12), (7, 16), (14, 19), (12, 23), (19, 28)]
    for i in range(len(pts) - 1):
        c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], m[2])
    pts = [(29, 6), (24, 10), (26, 13), (21, 16)]
    for i in range(len(pts) - 1):
        c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], m[3])


def tex_arcane(c, r):
    m = _row("arcane")
    rr = r - 3.5
    for d in range(0, 360, 6):
        if (d // 6) % 5 == 4:
            continue
        a = math.radians(d)
        c.set(C0 + math.cos(a) * rr, C0 + math.sin(a) * rr, m[1] if (d // 30) % 3 == 0 else m[2])


def tex_darkness(c, r):
    m = _row("darkness")
    for arm in range(3):
        base = arm * TAU / 3.0 + 0.4
        for i in range(30):
            t = i / 30.0
            rr = (r - 2.0) - t * 7.0
            a = base + t * 2.4
            c.set(C0 + math.cos(a) * rr, C0 + math.sin(a) * rr, m[3] if t > 0.4 else m[2])


def tex_light(c, r):
    m = _row("light")
    for k in range(12):
        a = TAU * k / 12.0
        for i in range(4, int(r)):
            if (i + k) % 2 == 0 or k % 2 == 0:
                c.set(C0 + math.cos(a) * i, C0 + math.sin(a) * i, m[3] if k % 2 else m[2])


def tex_grass(c, r):
    m = _row("grass")
    for (x, y, ang) in ((7, 9, 0.6), (24, 7, 2.4), (6, 20, -0.4), (25, 22, 3.6), (15, 4, 1.6)):
        for i in range(4):
            c.set(x + math.cos(ang) * i, y + math.sin(ang) * i, m[3])
        c.set(x + math.cos(ang + 0.6) * 2, y + math.sin(ang + 0.6) * 2, m[2])


def tex_poison(c, r):
    m = _row("poison")
    for (x, y, rr) in ((8, 9, 1.5), (23, 6, 1.0), (26, 17, 1.6), (6, 20, 1.0), (13, 4, 1.0), (19, 9, 0.8)):
        c.ring(x, y, rr, rr, m[1])


def tex_earth(c, r):
    m = _row("earth")
    for (y, x0, x1) in ((22, 2, 13), (24, 18, 30), (27, 4, 22), (19, 23, 30)):
        c.hline(x0, x1, y, m[3])
        c.hline(x0 + 2, x0 + 5, y - 1, m[2])


def tex_metal(c, r):
    m = _row("metal")
    c.line(4, 20, 20, 4, m[2])
    c.line(12, 28, 28, 12, m[2])
    for (x, y) in ((8, 8), (24, 9), (8, 23), (23, 24)):
        c.set(x, y, m[1])
        c.set(x + 1, y + 1, m[4])


def tex_wind(c, r):
    m = _row("wind")
    for (cx, cy, rr, a0, a1) in ((12, 14, 9.0, 3.4, 5.6), (20, 18, 8.0, 0.2, 2.4), (16, 16, 12.5, 4.0, 5.0)):
        for i in range(24):
            a = a0 + (a1 - a0) * i / 23.0
            c.set(cx + math.cos(a) * rr, cy + math.sin(a) * rr, m[2] if i > 6 else m[1])


TEXTURES = {
    "ice": tex_ice, "fire": tex_fire, "water": tex_water, "lightning": tex_lightning, "arcane": tex_arcane,
    "darkness": tex_darkness, "light": tex_light, "grass": tex_grass, "poison": tex_poison,
    "earth": tex_earth, "metal": tex_metal, "wind": tex_wind,
}


# =============================================================================================
# Element accents: what a card that GAINS an element adds over its subject, at the rim
# =============================================================================================

def disc_top(x, r):
    dx = x - C0
    if abs(dx) >= r:
        return None
    return C0 - math.sqrt(r * r - dx * dx)


def disc_bottom(x, r):
    dx = x - C0
    if abs(dx) >= r:
        return None
    return C0 + math.sqrt(r * r - dx * dx)


def acc_ice(c, r, heavy=False):
    """Icicles hanging from the top of the disc, the frost on the rim they hang from."""
    core, hot, mid, edge = E["ice"]
    for x in range(int(C0 - r + 3), int(C0 + r - 2)):
        t = disc_top(x, r)
        if t is not None and x % 2 == 0:
            c.set(x, int(math.ceil(t)), core if x % 4 == 0 else hot)
    spikes = ((8, 4), (11, 7), (14, 3), (17, 8), (20, 5), (23, 6), (26, 3))
    if not heavy:
        spikes = ((9, 4), (13, 7), (18, 5), (22, 8), (25, 3))
    for (x, length) in spikes:
        t = disc_top(x, r)
        if t is None:
            continue
        y0 = int(math.ceil(t)) + 1
        for k in range(length):
            wide = k < length // 2
            c.set(x, y0 + k, hot if k < length - 1 else core)
            if wide:
                c.set(x + 1, y0 + k, mid if k else hot)
        c.set(x, y0 + length, mid)


def acc_fire(c, r, heavy=False):
    """Flame tongues licking up from the bottom of the disc."""
    core, hot, mid, edge = E["fire"]
    tongues = ((7, 5, -1), (11, 8, 1), (16, 6, -1), (21, 9, 1), (25, 5, -1))
    if heavy:
        tongues = ((6, 6, -1), (9, 9, 1), (13, 7, -1), (17, 10, 1), (21, 8, -1), (25, 6, 1))
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
        b = disc_bottom(x, r)
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
    """Drips running down from the top of the disc; bubbles at the bottom."""
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
    """A vine climbing the rim from the lower left to the top, in leaf."""
    core, hot, mid, edge = E["grass"]
    a0, a1 = math.radians(140.0), math.radians(300.0 if heavy else 260.0)
    rr = r - 1.5
    n = 60
    prev = None
    for i in range(n + 1):
        t = i / float(n)
        a = a0 + (a1 - a0) * t
        x, y = C0 + math.cos(a) * rr, C0 + math.sin(a) * rr
        if prev:
            c.line(prev[0], prev[1], x, y, edge)
        prev = (x, y)
    for i in range(n + 1):
        t = i / float(n)
        a = a0 + (a1 - a0) * t
        c.set(C0 + math.cos(a) * (rr - 1), C0 + math.sin(a) * (rr - 1), mid)
    for k in range(5 if heavy else 4):
        a = a0 + (a1 - a0) * (k + 0.5) / (5.0 if heavy else 4.0)
        x, y = C0 + math.cos(a) * (rr - 1), C0 + math.sin(a) * (rr - 1)
        ia = a + math.pi + (0.6 if k % 2 else -0.6)
        for d in range(1, 4):
            c.set(x + math.cos(ia) * d, y + math.sin(ia) * d, mid if d < 3 else hot)
        c.set(x + math.cos(ia + 0.5) * 2, y + math.sin(ia + 0.5) * 2, hot)


def acc_earth(c, r, heavy=False):
    """Rocks thrown up from a cracked floor at the bottom of the disc."""
    core, hot, mid, edge = E["earth"]
    for (x, y, w) in ((6, 9, 3), (25, 7, 2), (27, 18, 2), (4, 17, 2)) + (((19, 4, 2),) if heavy else ()):
        c.rect(x, y, x + w - 1, y + w - 1, mid)
        c.set(x, y, hot)
        c.hline(x, x + w - 1, y + w, edge)
        c.vline(x + w, y, y + w, edge)
    y = 27
    pts = [(5, 26), (9, 27), (12, 26), (16, 28), (20, 27), (24, 28), (27, 26)]
    for i in range(len(pts) - 1):
        c.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], edge)
    for (x, yy) in ((9, 26), (16, 27), (24, 27)):
        c.set(x, yy, mid)


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
    core, hot, mid, edge = E["light"]
    stars = [(25, 6, 3), (6, 25, 2), (5, 8, 1)]
    if heavy:
        stars.append((27, 24, 2))
    for (x, y, arm) in stars:
        for k in range(1, arm + 1):
            for (ox, oy) in ((k, 0), (-k, 0), (0, k), (0, -k)):
                c.set(x + ox, y + oy, hot if k < arm else mid)
        c.set(x, y, core)


def acc_arcane(c, r, heavy=False):
    """Motes of arcane light orbiting inside the rim."""
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
        c.set(x, y, hot)
        c.set(x + 1, y, mid)
        c.set(x, y + 1, mid)
        c.set(x + 1, y + 1, edge)


def acc_wind(c, r, heavy=False):
    """Gusts sweeping across the disc, each ending in a curl."""
    core, hot, mid, edge = E["wind"]
    for (y, x0, x1, curl) in ((8, 4, 16, 1), (24, 14, 28, -1)) + (((15, 2, 9, 1),) if heavy else ()):
        for x in range(x0, x1 + 1):
            yy = y + math.sin((x - x0) * 0.4) * 0.8
            c.set(x, yy, mid if x < x1 - 3 else hot)
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
# Verb motifs: what the card DOES
# =============================================================================================

def m_speed(c, ramp, travel, box, heavy=False):
    """Speed lines trailing back from the subject against its travel; round ones for a subject that
    does not travel."""
    core, hot, mid, edge = ramp
    if travel is None:
        for (rr, a0, a1, tone) in ((13.0, 3.6, 5.2, mid), (13.0, 0.5, 2.1, mid), (11.0, 3.9, 4.8, edge), (11.0, 0.8, 1.7, edge)):
            for i in range(14):
                a = a0 + (a1 - a0) * i / 13.0
                c.set(C0 + math.cos(a) * rr, C0 + math.sin(a) * rr, tone if i > 3 else hot)
        return
    tx, ty = travel
    n = math.hypot(tx, ty)
    tx, ty = tx / n, ty / n
    px, py = -ty, tx
    cx, cy = (box[0] + box[2]) / 2.0, (box[1] + box[3]) / 2.0
    lines = (-6, -2, 2, 6) if not heavy else (-8, -5, -2, 1, 4, 7)
    for k, off in enumerate(lines):
        sx = cx - tx * 6 + px * off
        sy = cy - ty * 6 + py * off
        length = 7 + (k % 2) * 4
        ex, ey = sx - tx * length, sy - ty * length
        c.line(sx, sy, ex, ey, edge)
        c.line(sx, sy, sx - tx * length * 0.45, sy - ty * length * 0.45, mid)
        c.set(sx, sy, hot)


def m_rays(c, ramp, centre, r0=8.0, r1=15.0, n=12):
    """An impact's rays behind the subject: what makes a bigger card look like it hits harder."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for k in range(n):
        a = TAU * k / n + 0.13
        long_ray = k % 2 == 0
        end = r1 if long_ray else r1 - 3.0
        c.line(cx + math.cos(a) * r0, cy + math.sin(a) * r0, cx + math.cos(a) * end, cy + math.sin(a) * end,
               mid if long_ray else edge)
        if long_ray:
            c.set(cx + math.cos(a) * (r0 + 1), cy + math.sin(a) * (r0 + 1), hot)


def m_shockwave(c, ramp, centre, rr=13.5):
    """A broken ring going out: a colossal card's shock."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for d in range(0, 360, 4):
        if (d // 20) % 3 == 2:
            continue
        a = math.radians(d)
        c.set(cx + math.cos(a) * rr, cy + math.sin(a) * rr, mid)
        c.set(cx + math.cos(a) * (rr - 1), cy + math.sin(a) * (rr - 1), edge)
    for d in range(10, 360, 45):
        a = math.radians(d)
        c.set(cx + math.cos(a) * rr, cy + math.sin(a) * rr, hot)


def m_vortex(c, ramp, centre, rr=14.0, arms=3):
    """A pull: arms winding in."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for arm in range(arms):
        base = arm * TAU / arms + 0.5
        for i in range(48):
            t = i / 47.0
            r = rr - t * (rr - 4.5)
            a = base + t * 3.2
            tone = edge if t < 0.3 else (mid if t < 0.75 else hot)
            c.set(cx + math.cos(a) * r, cy + math.sin(a) * r, tone)
            if t < 0.5:
                c.set(cx + math.cos(a) * (r - 1), cy + math.sin(a) * (r - 1), edge)


def m_pool(c, ramp, cy=26.0, rx=11.5, ry=3.2):
    """What a lingering card leaves on the ground: a pool of the spell's own stuff."""
    core, hot, mid, edge = ramp
    c.disc(C0, cy, rx, ry, edge)
    c.disc(C0 - 0.5, cy - 0.3, rx - 2.5, ry - 1.1, mid)
    for (x, y) in ((C0 - 6, cy - 1), (C0 + 3, cy), (C0 + 7, cy - 1)):
        c.set(x, y, hot)
    c.set(C0 - 2, cy - 1, core)


def m_magma(c, cy=25.5):
    """Burning ground: a crusted magma pool, flames standing on it."""
    f = E["fire"]
    c.disc(C0, cy, 12.5, 3.6, OCC)
    c.disc(C0, cy - 0.3, 11.0, 2.8, f[3])
    for (x, y) in ((8, 25), (12, 26), (17, 25), (21, 26), (24, 25)):
        c.set(x, y, f[2])
        c.set(x + 1, y, f[1])
    for (x, h) in ((7, 4), (14, 6), (20, 5), (25, 3)):
        c.poly([(x - 1.5, cy - 1), (x + 0.3, cy - 1 - h), (x + 1.5, cy - 1)], f[2])
        c.set(x, cy - 2, f[1])


def m_roots(c, cy=30.0):
    """A root: earth's roots rising from the bottom and curling over."""
    e = E["earth"]
    for (x0, dirx, h) in ((8, 1, 9), (16, -1, 11), (24, -1, 8)):
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


def m_freeze(c, centre, spread=11.0):
    """Frozen solid: shards of ice standing round the subject."""
    i = E["ice"]
    cx, cy = centre
    for (a, h) in ((200, 6), (240, 8), (300, 7), (340, 5), (20, 6), (150, 5)):
        ang = math.radians(a)
        bx, by = cx + math.cos(ang) * spread, cy + math.sin(ang) * spread
        tx, ty = cx + math.cos(ang) * (spread + h), cy + math.sin(ang) * (spread + h)
        px, py = -math.sin(ang) * 1.6, math.cos(ang) * 1.6
        c.poly([(bx - px, by - py), (tx, ty), (bx + px, by + py)], i[3])
        c.line(bx, by, tx, ty, i[1])
        c.set(tx, ty, i[0])


def m_heart(c, x=24, y=6):
    """Lifesteal: a heart, and the blood running to it."""
    e = M["ember"]
    for (ox, oy) in ((0, 0), (1, 0), (3, 0), (4, 0), (-1, 1), (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1),
                     (-1, 2), (0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (0, 3), (1, 3), (2, 3), (3, 3),
                     (4, 3), (1, 4), (2, 4), (3, 4), (2, 5)):
        c.set(x + ox, y + oy, e[2] if ox + oy > 4 else e[1])
    c.set(x, y + 1, e[0])
    c.set(x + 1, y, e[0])
    for (dx, dy) in ((-5, 6), (-8, 9), (-3, 9)):
        c.set(x + dx, y + dy, e[1])
        c.set(x + dx, y + dy + 1, e[3])


def m_glint(c, x, y, arm=4, ramp=None):
    """A critical edge: a four-point star of light caught on the subject."""
    core, hot, mid, edge = ramp or E["light"]
    for k in range(1, arm + 1):
        for (ox, oy) in ((k, 0), (-k, 0), (0, k), (0, -k)):
            c.set(x + ox, y + oy, hot if k <= arm // 2 else mid)
    for (ox, oy) in ((1, 1), (-1, -1), (1, -1), (-1, 1)):
        c.set(x + ox, y + oy, hot)
    c.set(x, y, core)


def m_blast(c, ramp, x, y, r=5.0):
    """A burst where it hits."""
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


def m_chevrons(c, ramp, centre=(C0, C0), gap=10):
    """Knockback: chevrons thrown outward on either side."""
    core, hot, mid, edge = ramp
    cx, cy = centre
    for side in (-1, 1):
        for k in range(2):
            x = cx + side * (gap + k * 3)
            for d in range(-3, 4):
                c.set(x + side * (3 - abs(d)) * 0.8, cy + d, hot if k == 0 else mid)


def m_pierce(c, ramp, tip, travel):
    """It goes through: a needle of light on past the tip, punching through two marks."""
    core, hot, mid, edge = ramp
    tx, ty = travel
    n = math.hypot(tx, ty)
    tx, ty = tx / n, ty / n
    x0, y0 = tip
    x1, y1 = x0 + tx * 30, y0 + ty * 30
    c.line(x0 - tx * 22, y0 - ty * 22, x1, y1, mid)
    c.line(x0 - tx * 22 + 1, y0 - ty * 22, x1 + 1, y1, edge)
    for k in (-14, -6):
        x, y = x0 + tx * k * 0.5 - tx * 4, y0 + ty * k * 0.5 - ty * 4
        px, py = -ty, tx
        for d in (-2, -1, 1, 2):
            c.set(x + px * d, y + py * d, hot if abs(d) == 1 else edge)


def m_fork(c, ramp, tip, travel, spread=0.7, length=7):
    """It splits: two shards leaving the tip either side of its travel."""
    core, hot, mid, edge = ramp
    tx, ty = travel
    a = math.atan2(ty, tx)
    for side in (-1, 1):
        b = a + side * spread
        x1, y1 = tip[0] + math.cos(b) * length, tip[1] + math.sin(b) * length
        c.line(tip[0], tip[1], x1, y1, mid)
        c.line(tip[0] + math.cos(b) * 2, tip[1] + math.sin(b) * 2, x1, y1, hot)
        c.set(x1, y1, core)


def m_chain(c, ramp, start, ends):
    """It jumps: jagged arcs out to the next victims, a node of light on each."""
    core, hot, mid, edge = ramp
    for (ex, ey) in ends:
        sx, sy = start
        steps = 4
        prev = (sx, sy)
        for i in range(1, steps + 1):
            t = i / float(steps)
            jx = (1 if i % 2 else -1) * 1.5 if i < steps else 0
            x = sx + (ex - sx) * t + jx
            y = sy + (ey - sy) * t - jx
            c.line(prev[0], prev[1], x, y, hot)
            prev = (x, y)
        c.disc(ex, ey, 1.6, 1.6, mid)
        c.set(ex, ey, core)


def m_ghosts(c, subject, travel, colours, steps=(4, 8)):
    """Afterimages: the subject's silhouette left behind it, fading."""
    tx, ty = travel if travel else (1, 0)
    n = math.hypot(tx, ty)
    tx, ty = tx / n, ty / n
    for k, d in reversed(list(enumerate(steps))):
        ghost = silhouette(subject, colours[min(k, len(colours) - 1)])
        over(c, ghost, int(round(-tx * d)), int(round(-ty * d)))




# =============================================================================================
# The spells: each one's painter, which way it travels, where its business end is, and which of
# its colours are its effect (the rows an element path re-lights)
# =============================================================================================

SPELLS = {
    "magic_missile":     dict(paint=sc.magic_missile, travel=(1, 0), tip=(29, 16), rows=("arcane",)),
    "fireball":          dict(paint=sc.fireball, travel=(1, 1), tip=(25, 25), rows=("fire",)),
    "frost_shard":       dict(paint=sc.frost_shard, travel=None, tip=(16, 16), rows=("ice",)),
    "chain_lightning":   dict(paint=sc.chain_lightning, travel=(0.5, 1), tip=(22, 29), rows=("lightning",)),
    "shadow_bolt":       dict(paint=sc.shadow_bolt, travel=None, tip=(16, 16), rows=("darkness",)),
    "thorn_vine":        dict(paint=sc.thorn_vine, travel=(1, -1), tip=(28, 5), rows=("grass", "poison")),
    "gale_blade":        dict(paint=sc.gale_blade, travel=(1, 0), tip=(27, 14), rows=("wind",)),
    "molten_shard":      dict(paint=sc.molten_shard, travel=(1, -0.8), tip=(27, 4), rows=("fire",)),
    "cone_of_cold":      dict(paint=sc.cone_of_cold, travel=(1, 0), tip=(29, 16), rows=("ice",)),
    "solar_flare":       dict(paint=sc.solar_flare, travel=None, tip=(16, 16), rows=("light",)),
    "scorching_ray":     dict(paint=sc.scorching_ray, travel=(1, -1), tip=(28, 3), rows=("fire",)),
    "obsidian_spike":    dict(paint=sc.obsidian_spike, travel=(0, -1), tip=(16, 2), rows=("earth",)),
    "cyclone_slash":     dict(paint=sc.cyclone_slash, travel=(1, 0), tip=(24, 16), rows=("wind",)),
    "void_lance":        dict(paint=sc.void_lance, travel=(1, -1), tip=(30, 1), rows=("darkness", "arcane")),
    "meteor_swarm":      dict(paint=sc.meteor_swarm, travel=(1, 1), tip=(24, 18), rows=("fire",)),
    "black_tentacles":   dict(paint=sc.black_tentacles, travel=None, tip=(16, 8), rows=("poison",)),
    "spiritual_weapon":  dict(paint=sc.spiritual_weapon, travel=(1, -1), tip=(25, 6), rows=("arcane",)),
    "aegis_ward":        dict(paint=sc.aegis_ward, travel=None, tip=(16, 15), rows=("light",)),
    "arcane_explosion":  dict(paint=sc.arcane_explosion, travel=None, tip=(16, 16), rows=("arcane",)),
    "glacial_spike":     dict(paint=sc.glacial_spike, travel=(0, -1), tip=(16, 3), rows=("ice",)),
    "hunters_draw":      dict(paint=sc.hunters_draw, travel=(1, 0), tip=(30, 16), rows=("wind",)),
    "toxic_spore_burst": dict(paint=sc.spore_burst, travel=(0, -1), tip=(16, 6), rows=("poison",)),
    "riptide":           dict(paint=si.riptide, travel=(1, 0), tip=(28, 15), rows=("azure", "water")),
    "cinderbreath":      dict(paint=si.cinderbreath, travel=(1, -1), tip=(29, 5), rows=("fire",)),
    "mirefoot":          dict(paint=si.mirefoot, travel=None, tip=(16, 21), rows=("azure",)),
    "kindled_ward":      dict(paint=si.kindled_ward, travel=None, tip=(16, 16), rows=("amber", "light")),
    "gravewell":         dict(paint=si.gravewell, travel=None, tip=(16, 17), rows=("earth",)),
    "iron_palisade":     dict(paint=si.iron_palisade, travel=None, tip=(16, 8), rows=("metal",)),
    "contagion":         dict(paint=si.contagion, travel=None, tip=(16, 16), rows=("poison",)),
    "bramble_seed":      dict(paint=si.bramble_seed, travel=(0, -1), tip=(16, 4), rows=("grass",)),
    "hollow_star":       dict(paint=si.hollow_star, travel=None, tip=(16, 16), rows=("darkness",)),
    "starfall":          dict(paint=lambda: si.starfall(sky=False), travel=(0.75, 1), tip=(21, 22), rows=("light",)),
    "familiar":          dict(paint=lambda: si.familiar_icon(sky=False), travel=(1, 0), tip=(27, 19), rows=()),
    "stormsweep":        dict(paint=lambda: si.stormsweep(sky=False), travel=None, tip=(25, 20), rows=("lightning",)),
    "sleet_runes":       dict(paint=lambda: si.sleet_runes(sky=False), travel=(0.83, -0.56), tip=(22, 11), rows=("ice", "wind")),
}

# The spell's icon as the game ships it, for the difference check and the sheet: the four painters
# that draw their own night are drawn with it here.
BASE = {
    "starfall": si.starfall, "familiar": si.familiar_icon, "stormsweep": si.stormsweep,
    "sleet_runes": si.sleet_runes,
}


class Spec(object):
    """How one evolution's icon is built. derive() fills it from the card's data; OVERRIDES edit it."""

    def __init__(self):
        self.back = None         # the backdrop's element
        self.texture = True      # the night's own grain
        self.accent = None       # an element's accent over the subject (a card that gains one)
        self.heavy = False       # a denser accent: Ascensions and the Apotheosis
        self.recolour = None     # the element the subject is re-lit in (an element path)
        self.keep_core = False   # ...leaving its white-hot core and hot stop alone
        self.scale = 1.0         # the subject's scale about its own centre
        self.shift = (0.0, 0.0)
        self.mirror = False
        self.copies = []         # (dx, dy, scale, darken) more of the subject, behind it
        self.under = []          # f(c, ctx): drawn on the night, under the subject
        self.over = []           # f(c, ctx): drawn over the subject
        self.glow = True         # a one-pixel glow round the subject
        self.subject = None      # a hand-drawn subject: f() -> Canvas, replacing the painter
        self.ramp = None         # the verbs' ramp (default: the subject's own element after any re-light)
        self.why = ""            # one line for the sheet


class Ctx(object):
    """What a verb may read: the spell, the card, the subject as placed, and the ramps."""

    def __init__(self, card, spec, subject, info):
        self.card = card
        self.spec = spec
        self.subject = subject
        self.info = info
        self.r = DISC_R[card["milestone"]]
        self.box = bbox(subject)
        self.centre = ((self.box[0] + self.box[2]) / 2.0, (self.box[1] + self.box[3]) / 2.0)
        self.travel = info["travel"]
        tx, ty = info["tip"]
        if spec.mirror:
            tx = CELL - 1 - tx
            if self.travel:
                self.travel = (-self.travel[0], self.travel[1])
        bx, by = spec_anchor(spec, info)
        s = spec.scale
        self.tip = (bx + (tx - bx) * s + spec.shift[0], by + (ty - by) * s + spec.shift[1])
        own = spec.recolour or primary(card)
        self.ramp = E[spec.ramp or own]
        self.back = E[spec.back]


def primary(card):
    return [e.lower() for e in card["spell_elements"]][0]


def volley(n, travel):
    """Where n of the subject stand when a card multiplies it: abreast across its travel, the last
    (the one in front) a little ahead; a triangle for a subject that does not travel. (dx, dy, scale)."""
    if travel:
        tx, ty = travel
        d = math.hypot(tx, ty)
        tx, ty = tx / d, ty / d
        px, py = -ty, tx
        if n == 2:
            return [(-px * 5 - tx * 2, -py * 5 - ty * 2, 0.64), (px * 4 + tx * 2, py * 4 + ty * 2, 0.74)]
        return [(-px * 8 - tx * 3, -py * 8 - ty * 3, 0.58), (px * 8 - tx * 3, py * 8 - ty * 3, 0.58), (tx * 2, ty * 2, 0.70)]
    if n == 2:
        return [(-5, -4, 0.62), (4, 4, 0.72)]
    return [(-7, -6, 0.55), (7, -6, 0.55), (0, 5, 0.66)]


def multiply(spec, n, travel):
    """Turns the subject into a volley of n."""
    places = volley(n, travel)
    spec.copies = [(dx, dy, s, 1) for (dx, dy, s) in places[:-1]]
    dx, dy, s = places[-1]
    spec.scale = s
    spec.shift = (dx, dy)


def derive(card):
    """The default spec, from the card's own data."""
    info = SPELLS[card["spell"]]
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
        why.append(("path into " if sp.recolour else "deepens ") + gain)

    proj = card["projectiles"] + card["branches"]
    big = max(card["area"], card["scale"])
    fast = card["cooldown"] <= 0.75 or card["speed"] >= 1.2
    eff = card["effect"]
    travel = info["travel"]

    if proj >= 1:
        multiply(sp, 3 if proj >= 2 else 2, travel)
        why.append("x%d" % (3 if proj >= 2 else 2))
    elif big >= 2.0:
        sp.scale = 1.32
        sp.under.append(lambda c, x: m_shockwave(c, x.ramp, (C0, C0), x.r - 1.5))
        why.append("colossal")
    elif big >= 1.25:
        sp.scale = 1.14
        sp.under.append(lambda c, x: m_rays(c, x.ramp, x.centre, 9.0, x.r - 0.5))
        why.append("bigger")

    if fast:
        sp.under.append(lambda c, x: m_speed(c, x.ramp, x.travel, x.box, x.spec.heavy))
        why.append("faster")

    if eff == "VortexPull":
        sp.under.append(lambda c, x: m_vortex(c, x.ramp, (C0, C0), x.r - 1.0))
        why.append("pull")
    elif eff == "Freeze":
        sp.over.append(lambda c, x: m_freeze(c, x.centre, 9.0))
        why.append("freeze")
    elif eff == "RootDuration":
        sp.over.append(lambda c, x: m_roots(c))
        why.append("root")
    elif eff in ("ZoneDuration", "DotDamage"):
        sp.under.append(lambda c, x: m_pool(c, x.ramp))
        why.append("lingers")
    elif eff == "Burn":
        sp.under.append(lambda c, x: m_magma(c))
        why.append("burning ground")
    elif eff == "Lifesteal":
        sp.over.append(lambda c, x: m_heart(c))
        why.append("lifesteal")
    elif eff in ("CritChance", "CritDamage"):
        sp.over.append(lambda c, x: m_glint(c, int(x.tip[0]), int(x.tip[1]), 4))
        why.append("crit")
    elif eff == "ExplosionOnHit":
        sp.over.append(lambda c, x: m_blast(c, x.ramp, x.tip[0], x.tip[1], 5.5))
        why.append("blast")
    elif eff == "SplitOnHit":
        sp.over.append(lambda c, x: m_fork(c, x.ramp, x.tip, x.travel or (1, 0)))
        why.append("splits")
    elif eff in ("Vulnerability", "SlowPower"):
        sp.under.append(lambda c, x: m_shockwave(c, x.back, (C0, C0), x.r - 2.0))
        why.append(eff.lower())

    if card["pierce"] >= 2 and travel and proj < 1:
        sp.under.append(lambda c, x: m_pierce(c, x.ramp, x.tip, x.travel))
        why.append("pierce")
    if card["knockback"] > 0:
        sp.over.append(lambda c, x: m_chevrons(c, x.ramp, x.centre, 11))
        why.append("knockback")
    if card["arcs"] > 0 and proj < 1:
        sp.over.append(lambda c, x: m_chain(c, x.ramp, x.tip, [(x.tip[0] - 9, x.tip[1] - 3), (x.tip[0] - 3, x.tip[1] - 10)]))
        why.append("chains")
    if not sp.under and not sp.over and not sp.copies and sp.scale == 1.0 and not sp.recolour:
        # A card that only adds damage (and perhaps its own tag): it hits harder.
        sp.under.append(lambda c, x: m_rays(c, x.ramp, x.centre, 8.0, x.r - 0.5))
        why.append("harder")
    sp.why = ", ".join(why)
    return sp


# =============================================================================================
# Composition
# =============================================================================================

ANCHORS = {}


def spec_anchor(spec, info):
    """The point a scale is taken about: the centre of the subject as its painter draws it, so a
    subject drawn off-centre (Cinderbreath's nozzle, Riptide's swell) grows where it stands."""
    key = (id(info), id(spec.subject))
    if key not in ANCHORS:
        ANCHORS[key] = centre_of(subject_of(spec, info, plain=True))
    return ANCHORS[key]


def subject_of(spec, info, plain=False, s=1.0, dx=0.0, dy=0.0, mirror=False):
    fn = spec.subject or info["paint"]
    if plain:
        return paint(fn)
    return paint(fn, s, dx, dy, mirror)


def draw_subject(spec, info, s, offset, mirror):
    """The subject at scale s about its own centre, moved by offset."""
    bx, by = spec_anchor(spec, info)
    if mirror:
        bx = CELL - 1 - bx
    dx = (bx - C0) * (1.0 - s) + offset[0]
    dy = (by - C0) * (1.0 - s) + offset[1]
    fn = spec.subject or info["paint"]
    if s < 1.0:
        # Shrunk subjects are redrawn small rather than voted down, when the painter allows it.
        cv = paint(fn, s, dx, dy, mirror)
    else:
        cv = paint(fn, s, dx, dy, mirror)
    if spec.recolour and (info["rows"] or spec.subject is None):
        cv = recolour(cv, set(info["rows"]), spec.recolour, spec.keep_core)
    return cv


def compose(card, spec):
    info = SPELLS[card["spell"]]
    tier = card["milestone"]
    r = DISC_R[tier]
    c = _Real(CELL, CELL, None)

    # 1. The Apotheosis's burst, behind everything.
    if tier >= 9:
        sunburst(c, E[spec.back])

    # 2. The night, and its grain, clipped to the disc.
    disc = _Real(CELL, CELL, None)
    backdrop(disc, spec.back, r)
    if spec.texture:
        TEXTURES[spec.back](disc, r)
    inside = in_disc(r - 0.5)
    for y in range(CELL):
        for x in range(CELL):
            p = disc.get(x, y)
            if p[3] and inside(x, y):
                c.set(x, y, p)

    # 3. The rim (a mutation) or the gold (an Ascension, the Apotheosis).
    if tier == 4:
        rim(c, spec.back, r)
    else:
        gold_ring(c, r + (0.5 if tier >= 9 else 0.0))

    # 4. The subject and its copies, then what goes under them.
    main = draw_subject(spec, info, spec.scale, spec.shift, spec.mirror)
    copies = []
    for (dx, dy, s, dk) in spec.copies:
        cv = draw_subject(spec, info, s, (dx, dy), spec.mirror)
        if dk:
            cv = darken(cv, dk)
        copies.append(cv)
    ctx = Ctx(card, spec, main, info)
    for f in spec.under:
        f(c, ctx)
    glow = BACK[spec.back]["rim"]
    for cv in copies:
        if spec.glow:
            aura(c, cv, glow)
        over(c, cv)
    if spec.glow:
        aura(c, main, glow)
    over(c, main)

    # 5. The element's accent and the verbs over the subject.
    if spec.accent:
        ACCENTS[spec.accent](c, r, spec.heavy)
    for f in spec.over:
        f(c, ctx)

    # 6. The milestone's furniture, on top of everything.
    if tier == 8:
        for (sx, sy) in ((C0, C0 - r + 1.0), (C0 + r - 1.0, C0), (C0, C0 + r - 1.0), (C0 - r + 1.0, C0)):
            gem(c, sx, sy, E[spec.back])
    elif tier >= 9:
        crown(c, E[spec.back])
    return c


# =============================================================================================
# The hand-written cards: verbs the numbers cannot say
# =============================================================================================

OVERRIDES = {}


def spec_for(card):
    sp = derive(card)
    fix = OVERRIDES.get(card["id"])
    if fix:
        fix(sp, card)
    return sp


# =============================================================================================
# Checks, output, the sheet
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


GROUNDS = {
    # Where each kind of card's icon is shown in the game (CardFamilyArt): the new spell's page, the
    # mutation's teal roundel, the obsidian plate's glass, the ember plate.
    "base": (242, 220, 196), 4: (22, 40, 46), 8: (11, 36, 54), 9: (22, 7, 10),
}


def write_sheet(rows, path, zoom=2):
    from PIL import Image, ImageDraw
    cell = CELL * zoom
    pad = 6
    label_h = 22
    cols = max(len(r[1]) for r in rows) + 1
    name_w = 130
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
            g = GROUNDS[tier]
            sheet.paste(Image.new("RGBA", (cell, cell), g + (255,)), (x, y))
            sheet.alpha_composite(cv.img.resize((cell, cell), Image.NEAREST), (x, y))
            draw.text((x + 1, y + cell + 2), label[:14], fill=(200, 200, 200, 255))
    sheet.convert("RGB").save(path)


def main(argv):
    only = None
    for a in argv:
        if a.startswith("--only="):
            only = set(a[len("--only="):].split(","))
    cards = load_cards()
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    made = {}
    problems = []
    for card in cards:
        if card["spell"] not in SPELLS:
            problems.append("%s: spell %s has no entry in SPELLS" % (card["id"], card["spell"]))
            continue
        if only and card["id"] not in only and card["spell"] not in only:
            continue
        spec = spec_for(card)
        cv = compose(card, spec)
        bad = cv.audit()
        if bad:
            problems.append("%s: off-contract colours %s" % (card["id"], ", ".join(h for h, _ in bad[:5])))
        made[card["id"]] = (card, spec, cv)

    # Every icon must be its own picture: unlike its spell's, and unlike every sibling's.
    bases = {}
    for cid, (card, spec, cv) in made.items():
        spell = card["spell"]
        if spell not in bases:
            bases[spell] = base_icon(spell)
        d = difference(cv, bases[spell])
        if d < MIN_DIFFERENCE:
            problems.append("%s: only %.0f%% different from %s's icon" % (cid, d * 100, spell))
    ids = sorted(made)
    for i, a in enumerate(ids):
        for b in ids[i + 1:]:
            if made[a][0]["spell"] != made[b][0]["spell"]:
                continue
            d = difference(made[a][2], made[b][2])
            if d < MIN_DIFFERENCE:
                problems.append("%s and %s: only %.0f%% different" % (a, b, d * 100))

    for cid, (card, spec, cv) in sorted(made.items()):
        cv.scaled(2).save(os.path.join(OUT_DIR, cid + ".png"))
    print("%d evolution icons written to %s" % (len(made), os.path.relpath(OUT_DIR, ROOT)))

    if "--no-sheet" not in argv:
        rows = []
        order = []
        for card in cards:
            if card["id"] in made and card["spell"] not in order:
                order.append(card["spell"])
        for spell in order:
            items = [(made[c["id"]][0]["id"][len(spell) + 1:] if c["id"].startswith(spell + "_") else c["id"],
                      c["milestone"], made[c["id"]][2])
                     for c in sorted((c for c in cards if c["spell"] == spell and c["id"] in made),
                                     key=lambda c: c["milestone"])]
            rows.append((spell, items))
        write_sheet(rows, SHEET)
        print("contact sheet: %s" % os.path.relpath(SHEET, ROOT))

    for p in problems:
        print("PROBLEM " + p)
    if problems:
        print("%d problems" % len(problems))
        return 1
    print("palette clean, every icon its own picture")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
