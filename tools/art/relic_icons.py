# -*- coding: utf-8 -*-
"""Relic and set icons. Run: python tools/art/relic_icons.py

Thirty-one relics and ten Full Set Enchantments (and the Marginalia level-up card, which is
drawn with the two tome relics - section 11), 32x32 rendered at x2 onto the UI cell in
`.ai/art-direction.md` section 1 - the same cell the spell and boon icons use, because all three
are offered on the same card and have to survive being compared.

WHY THEY EXIST. Every one of these was a slice of a bought GUI sheet: `ui-png-elements2-*` for
most of the relics, `ui-png-iconsmenu-*` for the sets, and a handful of spell-VFX frames for the
elemental ones. That art is licensed for use and not for redistribution, so none of it can be in
a public repository - see `.ai/asset-licensing.md`. These replace all thirty-nine.

THEY ARE OBJECTS, NOT EFFECTS, which is the rule `boon_icons.py` established and the reason its
helpers are imported here rather than copied. A spell icon is emissive with a white-hot core; a
relic is a thing the player is carrying, so it is drawn on the MATERIAL rows and lit from the
upper left, and it only touches an element ramp where the object is genuinely glowing. Sharing
one lighting model across boons and relics is what makes "this card is not a spell" readable at a
glance without anybody being told.

A SET IS ONE OBJECT AND A RELIC IS ONE PIECE OF IT (issue #63). The first pass drew each relic as
whatever its name suggested - a key, a cloak, a compass - and then drew each set as an unrelated
badge, so holding two pieces of a set told the player nothing about the third. Now every set is
the parts of one real thing:

    Eternal Guardian   The Barrow Harness       breastplate, greaves, gorget
    Bastion of Spikes  The Thorned Buckler      boss, thorns, hoarfrost rivets
    Vaultguard         The Reliquary            key, vial, casket
    Emberline          The Brazier              coals, bowl, tripod
    Stormbound         The Lightning Rod        copper coil, rod tip, split lens
    Elemental Mastery  The Jewelled Crown       the crown, frost / storm / prism jewels
    Fortune's Favor    The Regalia              the crown (shared), signet ring, orb of state
    Speed Demon        The Seven-League Boots   left boot, right boot
    Deathbringer       The Reaper's Scythe      blade, snath, whetstone
    Life Drain         The Tithe Chalice        cup, heartstone, wormwood

and the set icon is that thing ASSEMBLED, drawn with the same helper calls as its pieces. That is
the whole mechanism of "seeing two pieces tells you what the third looks like": the pieces of one
object share a material row, a metal and an edge treatment, so at icon size they read as siblings
before anybody reads a name. The item ids and filenames are unchanged - only the drawings moved.

EVERY EDGE IS LIT BY ONE FUNCTION. `_rim()` walks a finished silhouette and lights whatever faces
the key (up or left of an empty pixel) and darkens whatever faces away. Doing that per shape by
hand is how two pieces of the same object end up lit from different sides, which reads as two art
styles - so the pieces get their bulk drawn in base/shade and `_rim()` does the rest, identically.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import boon_icons as boon
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_RELICS = os.path.join(ROOT, "assets", "bonelight", "ui", "relics")
OUT_SETS = os.path.join(ROOT, "assets", "bonelight", "ui", "sets")

CELL = 32

# One lighting model for every carried object in the game. See the note above.
_ramp = boon._ramp
_lit_disc = boon._lit_disc
_shadow = boon._shadow

# --- the materials the ten objects are made of ------------------------------------------------
# Named once, so that "the buckler is dark iron" is a single decision rather than five scattered
# ramp lookups that can drift apart.
#
# Two of these are not material rows, and both are deliberate:
#
# WOOD is the EARTH element ramp. The palette has no wood row, and the brown material rows are
# either a shadow colour (stone) or skin (flesh). Earth is what `spell_icons.gravewell` already uses
# for lit soil, and it is the only warm brown light enough to survive a near-black card. It is
# padded to five stops with gold.deep, which is the warm near-black a wood crevice actually is.
#
# COPPER is the FLESH row. There is no copper row either, and flesh.base #A87058 is a dull copper to
# the eye; the lightning rod is the only place in the game that row is not a living thing.
STEEL = _ramp("steel")
BONE = _ramp("skin")
GOLD = _ramp("gold")
LEATHER = _ramp("flesh")
COPPER = _ramp("flesh")
GREYWOOD = _ramp("linen")
CLOTH_RED = _ramp("red")
GLASS = _ramp("arcane")
_e = bl.ELEMENTS["earth"]
WOOD = [_e[0], _e[1], _e[2], _e[3], bl.tone("gold", "deep")]


# --- shared parts -----------------------------------------------------------------------------

def _tones(mat):
    return _ramp(mat) if isinstance(mat, str) else mat


def _rim(c, mat, box=None):
    """Light the silhouette of everything drawn in `mat`'s base or shade.

    A pixel with empty space up or to the left of it faces the key and takes `lit`; one with empty
    space down or to the right faces away and takes `deep`. A pixel that is both - a one-pixel
    strand - is left alone, because lighting it either way makes a thin thing vanish.

    Only base and shade are touched, so a highlight or trim drawn before the call survives it, and
    a neighbouring object in a different material is never repainted. The decisions are all made
    against a snapshot, so a pixel lit early in the walk cannot change the verdict on the next one.
    """
    hi, lit, base, shade, deep = [raster.rgb(t) for t in _tones(mat)]
    x0, y0, x1, y1 = box or (0, 0, c.w - 1, c.h - 1)
    targets = (base, shade)

    def empty(x, y):
        # The cast shadow is translucent; it is not a surface, so an edge above it still faces away.
        return c.get(x, y)[3] < 200

    changes = []
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if c.get(x, y) not in targets:
                continue
            ul = empty(x - 1, y) or empty(x, y - 1)
            dr = empty(x + 1, y) or empty(x, y + 1)
            if ul and not dr:
                changes.append((x, y, lit))
            elif dr and not ul:
                changes.append((x, y, deep))
    for (x, y, t) in changes:
        c.set(x, y, t)


def _erase(c, x, y):
    x, y = int(x), int(y)
    if 0 <= x < c.w and 0 <= y < c.h:
        c.px[x, y] = (0, 0, 0, 0)


def _erase_disc(c, cx, cy, rx, ry):
    for y in range(int(cy - ry), int(cy + ry) + 1):
        for x in range(int(cx - rx), int(cx + rx) + 1):
            nx = (x - cx) / float(rx)
            ny = (y - cy) / float(ry)
            if nx * nx + ny * ny <= 1.0:
                _erase(c, x, y)


def _thick(c, pts, w, colour):
    """A polyline with body: a stroke `w` pixels across, for handles, legs and wire."""
    r = w / 2.0
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        n = int(max(abs(bx - ax), abs(by - ay))) * 2 + 1
        for k in range(n + 1):
            t = k / float(n)
            c.disc(ax + (bx - ax) * t, ay + (by - ay) * t, r, r, colour)


def cut_gem(c, cx, cy, r, ramp):
    """An octagonal cut stone seen from above: a flat table, and eight crown facets round it,
    each lit by which way it faces.

    Every jewel in the crown is THIS cut, in a different ramp. Three stones cut alike is what
    says "these were made for the same three holes" - a teardrop, a nugget and a prism, which is
    what the three used to be, say nothing of the kind.

    The first pass was a round disc with a star of facet lines on it, and at icon size it read as
    a ball - the yellow one as a sun. A stone reads as CUT when its outline has corners and its
    facets are flat planes of different value, so they are drawn as planes, not as lines.
    """
    if r < 4.5:
        # Too small for planes: a socket-sized stone is a dark rim, a body and one lit facet.
        c.disc(cx, cy, r, r, ramp[3])
        c.disc(cx - 0.3, cy - 0.3, r * 0.65, r * 0.65, ramp[2])
        c.set(cx - r * 0.35, cy - r * 0.35, ramp[0])
        return
    outer = [(cx + math.cos(math.radians(22.5 + 45 * k)) * r,
              cy + math.sin(math.radians(22.5 + 45 * k)) * r) for k in range(8)]
    table = [(cx + math.cos(math.radians(22.5 + 45 * k)) * r * 0.48,
              cy + math.sin(math.radians(22.5 + 45 * k)) * r * 0.48) for k in range(8)]
    c.poly(outer, ramp[3])
    for k in range(8):
        facing = math.cos(math.radians(45 * k + 45) - math.radians(225.0))
        tone = ramp[1] if facing > 0.6 else (ramp[2] if facing > -0.4 else ramp[3])
        c.poly([outer[k], outer[(k + 1) % 8], table[(k + 1) % 8], table[k]], tone)
    c.poly(table, ramp[2])
    # the facet edges, so neighbouring planes of the same value still split
    for k in range(8):
        c.line(table[k][0], table[k][1], outer[k][0], outer[k][1], ramp[3])
    for k in range(8):
        a, b = table[k], table[(k + 1) % 8]
        c.line(a[0], a[1], b[0], b[1], ramp[3] if k in (0, 1, 2, 7) else ramp[1])
    c.set(cx - r * 0.25, cy - r * 0.25, ramp[0])
    c.set(cx - r * 0.15, cy - r * 0.3, ramp[1])


# ==============================================================================================
# 1. THE BARROW HARNESS - Eternal Guardian
# Grave-dug plate: one cold steel, trimmed in bone. The bone trim is the family mark - it is on
# the neck of the breastplate, the knee of each greave and the lip of the gorget, and it is the
# only bone-pale pixel in any defensive relic, so two pieces side by side are visibly a suit.
# ==============================================================================================

def _profile(t, stops):
    """Piecewise-linear width profile: stops is [(t, w), ...] with t rising from 0 to 1."""
    for (t0, w0), (t1, w1) in zip(stops, stops[1:]):
        if t <= t1:
            return w0 + (w1 - w0) * (t - t0) / float(max(1e-6, t1 - t0))
    return stops[-1][1]


def _breastplate(c, cx, top, half, h, faulds=2):
    """A cuirass: broad chest, pinched waist, a point at the belly, and lames hung below it.

    The steel is kept to base and shade on purpose. Steel's lit stop and bone's lit stop are
    nearly the same value, so a breastplate lit like the other steel relics swallowed its own
    trim - the first pass read as a grey shirt. A darker shell is what lets the bone edge show.
    """
    bh, bl_, bb, bs, bd = BONE
    hi, lit, mid, shade, deep = STEEL
    waist = top + int(h * 0.68)
    pts = [(cx - half, top + 2), (cx - 4, top), (cx, top + 3), (cx + 4, top), (cx + half, top + 2),
           (cx + half - 3, top + 7), (cx + half - 2, top + 10), (cx + half - 3, waist - 1),
           (cx, waist + 2), (cx - half + 3, waist - 1), (cx - half + 2, top + 10),
           (cx - half + 3, top + 7)]
    c.poly(pts, mid)
    # A convex shell: the right-hand third turns away from the key.
    for y in range(top, waist + 3):
        for x in range(int(cx + half * 0.3), int(cx + half + 1)):
            if c.get(x, y) == raster.rgb(mid):
                c.set(x, y, shade)
    # The lames under the waist: each a band one pixel wider than the one above, split by occ.
    y = waist + 2
    for k in range(faulds):
        w = half - 2 + k
        c.rect(cx - w, y, cx + w, y + 2, mid)
        c.rect(cx + w * 0.3, y, cx + w, y + 2, shade)
        y += 3
    _rim(c, STEEL, (int(cx - half - 1), top - 1, int(cx + half + 1), y + 1))
    for k in range(faulds):
        ly = waist + 2 + k * 3
        c.hline(cx - half + 3 + (k == 0) * 0, cx + half - 3 + k, ly, OCC)
        c.hline(cx - half + 3 - k, cx - 1, ly + 1, bl_)
        c.hline(cx, cx + half - 3 + k, ly + 1, bb)
    # the medial ridge, catching the key down the breastbone
    c.vline(cx - 1, top + 5, waist, lit)
    c.set(cx - 1, top + 5, hi)
    # The neck: a real U cut out of the top. The first cut was a three-pixel dip, which at 1x
    # was a flat top - and a breastplate with no neck hole is a book.
    nr = max(3.0, half * 0.42)
    _erase_disc(c, cx, top, nr, nr * 0.9)
    # Bone trim, two pixels thick so it survives 1x: the neck, both armholes, and the belly line.
    for deg in range(0, 181, 3):
        r = math.radians(deg)
        x = cx + math.cos(r) * (nr + 0.6)
        y = top + math.sin(r) * (nr * 0.9 + 0.6)
        c.set(x, y, bh if deg > 110 else (bl_ if deg > 60 else bb))
        c.set(x, y + 1, bl_ if deg > 90 else bb)
    c.line(cx - half, top + 2, cx - half + 3, top + 7, bl_)
    c.line(cx - half + 1, top + 2, cx - half + 3, top + 6, bl_)
    c.line(cx + half, top + 2, cx + half - 3, top + 7, bb)
    c.line(cx - half + 3, waist - 1, cx, waist + 2, bl_)
    c.line(cx, waist + 2, cx + half - 3, waist - 1, bb)
    c.set(cx - half, top + 2, bh)
    for (rx, ry) in ((cx - half + 4, top + 3), (cx + half - 4, top + 3)):
        c.set(rx, ry, bh if rx < cx else bb)


def _greave(c, cx, top, bot, half):
    """A shin guard: knee cop, a calf that swells and narrows to the ankle, a flared foot edge.

    The first pass had near-straight sides and read as a pair of pillars. A greave is shaped
    like the leg inside it, and at 32px that shape is the only thing saying "leg".
    """
    bh, bl_, bb, bs, bd = BONE
    hi, lit, mid, shade, deep = STEEL
    prof = [(0.0, 0.8), (0.3, 1.0), (0.78, 0.55), (0.9, 0.62), (1.0, 0.95)]
    for y in range(top + 3, bot + 1):
        t = (y - top - 3) / float(max(1, bot - top - 3))
        w = half * _profile(t, prof)
        c.hline(cx - w, cx + w, y, mid)
        c.hline(cx + w * 0.2, cx + w, y, shade)
    # the knee cop, a separate plate over the top of it, with a point at the bottom
    c.disc(cx, top + 2, half * 0.85, 2.6, mid)
    c.poly([(cx - 2, top + 4), (cx, top + 6), (cx + 2, top + 4)], mid)
    _rim(c, STEEL, (int(cx - half - 2), top - 2, int(cx + half + 2), bot + 1))
    c.line(cx - half * 0.8, top + 4, cx, top + 6, OCC)
    c.line(cx, top + 6, cx + half * 0.8, top + 4, OCC)
    # bone trim on the knee and the ankle band
    c.hline(cx - half * 0.7, cx + half * 0.7, top, bb)
    c.hline(cx - half * 0.7, cx, top, bh)
    ay = top + 3 + int((bot - top - 3) * 0.84)
    c.hline(cx - half * 0.6, cx + half * 0.6, ay, bb)
    c.hline(cx - half * 0.6, cx - 1, ay, bl_)
    c.vline(cx - half * 0.55, top + 8, ay - 3, lit)


def _gorget(c, cx, cy, rx, ry):
    bh, bl_, bb, bs, bd = BONE
    hi, lit, mid, shade, deep = STEEL
    c.disc(cx, cy, rx, ry, mid)
    c.disc(cx + rx * 0.35, cy + ry * 0.3, rx * 0.6, ry * 0.6, shade)
    # Overlapping lames: one occlusion line between each pair, per art-direction 4b - one dark
    # pixel between two plates is worth more than five tones inside either.
    c.ring(cx, cy, rx * 0.74, ry * 0.74, OCC, 1)
    _erase_disc(c, cx, cy - ry * 0.42, rx * 0.5, ry * 0.52)
    _rim(c, STEEL, (int(cx - rx - 1), int(cy - ry - 1), int(cx + rx + 1), int(cy + ry + 1)))
    # the rolled bone lip around the neck
    hrx, hry = rx * 0.5 + 1.0, ry * 0.52 + 1.0
    for deg in range(0, 360, 4):
        a = math.radians(deg)
        x = cx + math.cos(a) * hrx
        y = cy - ry * 0.42 + math.sin(a) * hry
        if y < cy - ry * 0.42 - hry * 0.3:
            continue
        facing = math.cos(a - math.radians(45.0))   # the inside of a hole is lit on its FAR side
        c.set(x, y, bl_ if facing > 0.0 else bb)
    c.set(cx - rx * 0.62, cy - ry * 0.45, bh)


def aegis_sigil():
    """Barrow Breastplate. The middle of the harness, and the piece the other two hang from."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 9)
    _breastplate(c, 16, 3, 10, 21, faulds=2)
    return c


def ironhide_cloak():
    """Barrow Greaves. Drawn as the pair, because one shin guard alone reads as a vase."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 11)
    _greave(c, 10, 4, 28, 5)
    _greave(c, 22, 4, 28, 5)
    return c


def basalt_carapace():
    """Barrow Gorget: the neck guard. A collar of lames around an empty neck."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 10)
    _gorget(c, 16, 15, 13.5, 12.0)
    return c


def set_eternal_guardian():
    """The Barrow Harness, stood up as the empty suit: gorget, breastplate, greaves."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 9)
    _greave(c, 12, 19, 28, 3)
    _greave(c, 20, 19, 28, 3)
    _breastplate(c, 16, 6, 10, 10, faulds=1)
    _gorget(c, 16, 5, 7.5, 4.5)
    return c


# ==============================================================================================
# 2. THE THORNED BUCKLER - Bastion of Spikes
# A small round shield of dark iron. Its family marks are the thorns, which are the same spike in
# both the relic and the set, and the FROST-BLUE heads of the rivets, which are the only ice
# pixels on it.
# ==============================================================================================

def _thorn(c, cx, cy, deg, r0, r1, half):
    """One spike pointing out from a centre: a triangle, lit on whichever face sees the key."""
    hi, lit, mid, shade, deep = STEEL
    a = math.radians(deg)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    bx, by = cx + ux * r0, cy + uy * r0
    tip = (cx + ux * r1, cy + uy * r1)
    left = (bx + px * half, by + py * half)
    right = (bx - px * half, by - py * half)
    c.poly([left, tip, right], mid)
    # The face whose outward normal points up-left takes the light.
    lit_face, dark_face = (left, right) if (px + py) < 0 else (right, left)
    c.line(lit_face[0], lit_face[1], tip[0], tip[1], lit)
    c.line(dark_face[0], dark_face[1], tip[0], tip[1], shade)
    c.set(tip[0], tip[1], hi if (ux + uy) < 0.5 else lit)


def _frost_rivet(c, x, y, r):
    """A rivet head gone white with hoarfrost: the ice ramp, lit like a dome."""
    i = bl.ELEMENTS["ice"]
    c.disc(x, y, r, r, i[3])
    c.disc(x - 0.4, y - 0.4, r * 0.62, r * 0.62, i[2])
    c.set(x - r * 0.4, y - r * 0.4, i[0])


def _boss(c, cx, cy, flange, dome):
    hi, lit, mid, shade, deep = STEEL
    c.disc(cx, cy, flange, flange, shade)
    _rim(c, STEEL, (int(cx - flange - 1), int(cy - flange - 1), int(cx + flange + 1), int(cy + flange + 1)))
    c.ring(cx, cy, dome + 1.0, dome + 1.0, OCC, 1)
    _lit_disc(c, cx, cy, dome, "steel")
    c.disc(cx - dome * 0.35, cy - dome * 0.35, dome * 0.32, dome * 0.32, lit)
    c.set(cx - dome * 0.4, cy - dome * 0.4, hi)


def protective_ward():
    """Buckler Boss: the domed centre, with the four empty holes the rivets go through."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 10)
    _boss(c, 16, 15, 12.0, 7.0)
    for deg in (45, 135, 225, 315):
        a = math.radians(deg)
        c.disc(16 + math.cos(a) * 9.6, 15 + math.sin(a) * 9.6, 1.2, 1.2, OCC)
    return c


def iron_fang():
    """Buckler Thorns: the ring of spikes that goes round the rim, off the shield."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 9)
    for deg in range(0, 360, 45):
        _thorn(c, 16, 15, deg + 22.5, 7.0, 14.5, 2.6)
    c.ring(16, 15, 8.5, 8.5, STEEL[2], 3)
    _rim(c, STEEL, (4, 3, 28, 27))
    c.ring(16, 15, 5.6, 5.6, OCC, 1)
    return c


def hoarfrost_nail():
    """Hoarfrost Rivets: three nails with frost on their heads. The heads are what shows on the
    finished buckler, so they carry the colour; the shanks are the same iron as everything else."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, mid, shade, deep = STEEL
    _shadow(c, 16, 10)
    for (x, y, ln) in ((9, 9, 14), (16, 5, 18), (23, 10, 13)):
        for k in range(ln):
            t = k / float(ln - 1)
            w = 1.6 * (1.0 - t ** 2.2)
            c.hline(x - w, x + w, y + 2 + k, mid)
        c.set(x, y + 2 + ln, shade)
        _rim(c, STEEL, (x - 3, y, x + 3, y + ln + 3))
        _frost_rivet(c, x, y, 3.6)
    return c


def set_bastion():
    """The Thorned Buckler, whole: thorns round the rim, the boss, and four frost rivets."""
    c = raster.Canvas(CELL, CELL, None)
    for deg in range(0, 360, 45):
        _thorn(c, 16, 16, deg + 22.5, 9.5, 15.5, 2.4)
    c.disc(16, 16, 11.0, 11.0, STEEL[3])
    _rim(c, STEEL)
    c.ring(16, 16, 11.0, 11.0, STEEL[2], 1)
    for deg in range(180, 285, 4):
        a = math.radians(deg)
        c.set(16 + math.cos(a) * 10.6, 16 + math.sin(a) * 10.6, STEEL[1])
    _boss(c, 16, 16, 6.0, 4.0)
    for deg in (45, 135, 225, 315):
        a = math.radians(deg)
        _frost_rivet(c, 16 + math.cos(a) * 8.0, 16 + math.sin(a) * 8.0, 1.7)
    return c


# ==============================================================================================
# 3. THE RELIQUARY - Vaultguard
# A small box for holy things: dark wood bound in gold. Gold is the family mark - the key is gold,
# the vial's stopper and collar are gold, the casket's straps are gold - and the wood only appears
# on the casket, which is the piece the other two belong IN.
# ==============================================================================================

def _key(c, cx, top, scale=1.0):
    """An upright gold key: bow, shaft, bit, all at least two pixels across."""
    hi, lit, mid, shade, deep = GOLD
    r = 5.0 * scale
    c.disc(cx, top + r, r, r, mid)
    _erase_disc(c, cx, top + r, r * 0.45, r * 0.45)
    c.rect(cx - 1, top + 2 * r - 1, cx + 1, top + 2 * r + 15 * scale, mid)
    for (ty, tw) in ((10, 4), (14, 6)):
        y = top + 2 * r + ty * scale
        c.rect(cx + 2, y, cx + 1 + tw * scale, y + 1, mid)
    _rim(c, GOLD, (0, 0, c.w - 1, c.h - 1))
    c.set(cx - r * 0.6, top + r * 0.4, hi)


def _vial(c, cx, top, bot, half):
    """A stoppered vial: steel-pale glass, a gold collar and stopper, something living inside."""
    g = bl.ELEMENTS["grass"]
    hi, lit, mid, shade, deep = STEEL
    gh, gl, gm, gs, gd = GOLD
    body_top = top + 6
    # glass body with a round foot
    c.rect(cx - half, body_top, cx + half, bot - half, mid)
    c.disc(cx, bot - half, half, half, mid)
    _rim(c, STEEL, (int(cx - half - 1), body_top - 1, int(cx + half + 1), bot + 1))
    # the contents: healing, so the grass ramp, and only a faint glow - it is stored, not spent
    fill = body_top + (bot - body_top) // 3
    c.rect(cx - half + 1, fill, cx + half - 1, bot - half, g[3])
    c.disc(cx, bot - half, half - 1, half - 1, g[3])
    c.rect(cx - half + 2, fill + 1, cx + half - 2, bot - half, g[2])
    c.hline(cx - half + 1, cx + half - 1, fill, g[2])
    c.set(cx - half + 2, fill + 1, g[1])
    c.vline(cx - half + 1, body_top + 1, fill - 1, hi)
    # gold collar and stopper - the piece of the vial that is the reliquary's
    c.rect(cx - half + 1, body_top - 2, cx + half - 1, body_top - 1, gm)
    c.rect(cx - 2, top, cx + 2, body_top - 3, gs)
    c.disc(cx, top + 1, 2.4, 1.6, gm)
    _rim(c, GOLD, (int(cx - half - 1), top - 2, int(cx + half + 1), body_top))
    c.set(cx - 1, top, gh)


def _casket(c, x0, y0, x1, y1, lid_h, closed=True):
    """The box: a wood body with gold corner straps and a gold lock plate.

    The wood is the warm earth ramp kept to its DARK half, so the gold straps sit a full step
    above it. Built the other way round - light wood, gold straps - the two are the same value
    and the straps disappear into the planks.
    """
    wh, wl, wm, ws, wd = WOOD
    gh, gl, gm, gs, gd = GOLD
    c.rect(x0, y0, x1, y1, ws)
    for y in range(y0 + 3, y1, 3):
        c.hline(x0 + 1, x1 - 1, y, wd)
    c.hline(x0, x1, y0, wm)
    c.vline(x0, y0, y1, wm)
    c.hline(x0, x1, y1, wd)
    # gold straps at both corners and a band along the top edge
    for sx in (x0 + 1, x1 - 2):
        c.rect(sx, y0, sx + 1, y1, gm)
        c.vline(sx, y0, y1, gl)
    c.hline(x0, x1, y0, gl)
    c.set(x0, y0, gh)
    # the lock plate, with its keyhole
    mx = (x0 + x1) // 2
    c.rect(mx - 2, y0 + 2, mx + 2, y0 + 7, gm)
    c.hline(mx - 2, mx + 2, y0 + 2, gl)
    c.vline(mx - 2, y0 + 2, y0 + 7, gl)
    c.set(mx, y0 + 4, OCC)
    c.vline(mx, y0 + 5, y0 + 6, OCC)
    if closed:
        # a gabled lid, like a little church - which is what a reliquary is
        c.poly([(x0, y0), (x0 + 3, y0 - lid_h), (x1 - 3, y0 - lid_h), (x1, y0)], wm)
        c.line(x0, y0 - 1, x0 + 3, y0 - lid_h, gl)
        c.hline(x0 + 3, x1 - 3, y0 - lid_h, gl)
        c.line(x1, y0 - 1, x1 - 3, y0 - lid_h, gs)
        c.hline(x0 + 1, x1 - 1, y0 - 1, OCC)
        for x in range(x0 + 6, x1 - 4, 4):
            c.vline(x, y0 - lid_h + 1, y0 - 2, ws)
        # a gold cross on the ridge
        c.vline(mx, y0 - lid_h - 5, y0 - lid_h - 1, gm)
        c.hline(mx - 2, mx + 2, y0 - lid_h - 3, gm)
        c.set(mx, y0 - lid_h - 5, gh)
        c.set(mx - 2, y0 - lid_h - 3, gl)


def relic_key():
    """Reliquary Key. Gold, because everything that fits the reliquary is gold."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 6)
    _key(c, 14, 2, 1.0)
    return c


def vial_of_vitality():
    """Reliquary Vial: the stoppered vial that sits in the casket. The gold collar is its tie to
    the set; the faint green inside is the only thing that glows."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 7)
    _vial(c, 16, 3, 28, 6)
    return c


def phylactery():
    """Reliquary Casket: the box itself, shut and locked."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 12)
    _casket(c, 4, 15, 27, 28, 6, closed=True)
    return c


def set_vaultguard():
    """The Reliquary, open: the lid thrown back, the vial standing in it, the key in the lock."""
    c = raster.Canvas(CELL, CELL, None)
    wh, wl, wm, ws, wd = WOOD
    gh, gl, gm, gs, gd = GOLD
    _shadow(c, 14, 12)
    # the lid, opened and leaning back: its inside face, dark wood framed in gold
    c.poly([(4, 15), (6, 3), (24, 3), (26, 15)], wd)
    c.line(4, 15, 6, 3, gl)
    c.hline(6, 24, 3, gl)
    c.line(26, 15, 24, 3, gs)
    c.poly([(8, 13), (9, 6), (21, 6), (22, 13)], ws)
    # the vial, standing inside - its top half shows above the box front
    _vial(c, 15, 4, 22, 4)
    # the box front, drawn last so it hides the bottom of the vial
    c.hline(4, 26, 15, OCC)
    _casket(c, 3, 16, 26, 28, 0, closed=False)
    # the key in the lock: the shaft goes in at the plate, the bow sticks out to the right
    c.rect(16, 20, 25, 21, gm)
    c.hline(16, 25, 20, gl)
    c.disc(28, 20.5, 3.0, 3.0, gm)
    c.disc(28, 20.5, 1.1, 1.1, OCC)
    _rim(c, GOLD, (24, 16, 31, 25))
    c.set(26, 18, gh)
    return c


# ==============================================================================================
# 4. THE BRAZIER - Emberline
# Black iron and live coals. The iron is steel kept to its lit end so a "black" object is still
# visible on a black card; the coals are the fire ramp, and they are the only thing that glows.
# ==============================================================================================

COALS = ((16, 13, 3.4), (11, 15, 3.0), (21, 15, 3.0), (13, 11, 2.6), (19, 11, 2.6),
         (7, 17, 2.4), (25, 17, 2.4), (16, 9, 2.4), (16, 17, 3.0))


def _coals(c, cx, cy, s=1.0):
    """A heap of coals: each lump a dark crust, cracked open to a glowing inside.

    Every lump gets an occlusion ring first, so the heap reads as separate pieces rather than as
    one orange blob - which is what a disc of the fire ramp is, and what the first pass was.
    """
    f = bl.ELEMENTS["fire"]
    for (x, y, r) in COALS:
        c.disc(cx + (x - 16) * s, cy + (y - 13) * s, r * s + 0.8, r * s + 0.8, OCC)
    for i, (x, y, r) in enumerate(COALS):
        x, y, r = cx + (x - 16) * s, cy + (y - 13) * s, r * s
        c.disc(x, y, r, r, f[3])
        c.disc(x - r * 0.25, y - r * 0.25, r * 0.6, r * 0.6, f[2])
        if i % 3 == 0:
            c.set(x - r * 0.3, y - r * 0.3, f[1])
        if i % 4 == 1:
            c.set(x, y, f[0])


def _bowl(c, cx, rim_y, rx, depth, lit_inside=False):
    """A wide iron bowl with a lip and a ring handle at each side."""
    hi, lit, mid, shade, deep = STEEL
    f = bl.ELEMENTS["fire"]
    for y in range(rim_y, rim_y + depth + 1):
        t = (y - rim_y) / float(depth)
        w = rx * math.sqrt(max(0.0, 1.0 - t * t * 0.75))
        c.hline(cx - w, cx + w, y, mid)
        c.hline(cx + w * 0.3, cx + w, y, shade)
    for sx in (-1, 1):
        c.ring(cx + sx * (rx + 1), rim_y + 3, 2.2, 2.2, mid, 1)
    _rim(c, STEEL, (int(cx - rx - 4), rim_y - 4, int(cx + rx + 4), rim_y + depth + 1))
    # the lip, seen from slightly above: an ellipse with the dark inside showing
    c.disc(cx, rim_y, rx, 3.0, mid)
    c.disc(cx, rim_y + 0.5, rx - 2, 1.8, OCC)
    c.hline(cx - rx + 2, cx + rx - 2, rim_y - 2, lit)
    c.set(cx - rx + 1, rim_y - 1, hi)
    if lit_inside:
        c.hline(cx - rx + 5, cx + rx - 5, rim_y + 1, f[3])
        c.set(cx - 2, rim_y + 1, f[2])
        c.set(cx + 3, rim_y + 1, f[2])


def _tripod(c, cx, top, bot, spread):
    """Three iron legs from a ring, the outer two splayed, each ending in a curled foot."""
    # No cross-brace. The first pass had one, and a top ring, a brace and straight legs is a
    # ladder - it read as an easel. The legs bow outward and curl at the foot instead, which is
    # what wrought iron does and what a brazier stand looks like.
    hi, lit, mid, shade, deep = STEEL
    H = float(bot - top)
    for sx, tone in ((-1, mid), (1, shade)):
        _thick(c, [(cx + sx * spread * 0.45, top + 1), (cx + sx * spread * 0.8, top + H * 0.3),
                   (cx + sx * spread * 0.82, top + H * 0.7), (cx + sx * spread, bot - 1)], 2, tone)
        # the scroll foot, turning back in under itself
        c.disc(cx + sx * (spread - 1), bot - 1, 1.6, 1.4, tone)
    _thick(c, [(cx, top + 1), (cx, bot - 1)], 2, mid)
    c.hline(cx - 1, cx + 2, bot, mid)
    c.disc(cx, top, spread * 0.55, 1.8, mid)
    _rim(c, STEEL, (int(cx - spread - 3), top - 3, int(cx + spread + 3), bot + 1))
    c.disc(cx, top, spread * 0.55 - 1.5, 0.8, OCC)
    # a knot where the three legs are riveted together, a third of the way down the middle leg
    c.disc(cx, top + H * 0.4, 1.8, 1.8, mid)
    c.set(cx - 1, top + H * 0.4 - 1, hi)
    c.set(cx + 1, top + H * 0.4 + 1, deep)


def ember_flask():
    """Brazier Coals: a heap of them, crusted dark and cracked open to the fire inside."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 11)
    _coals(c, 16, 16, 1.25)
    return c


def inferno_core():
    """Brazier Bowl: the iron bowl, empty but for a last line of embers in the bottom."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 6)
    _bowl(c, 16, 11, 12.0, 12, lit_inside=True)
    c.rect(14, 24, 18, 27, STEEL[2])
    _rim(c, STEEL, (13, 23, 19, 28))
    return c


def obsidian_heart():
    """Brazier Tripod: the three iron legs the bowl sits in."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 13)
    _tripod(c, 16, 6, 28, 12)
    return c


def set_emberline():
    """The Brazier, lit: coals heaped in the bowl, the bowl on its tripod, the fire standing up."""
    c = raster.Canvas(CELL, CELL, None)
    f = bl.ELEMENTS["fire"]
    _shadow(c, 16, 12)
    _tripod(c, 16, 19, 28, 10)
    _bowl(c, 16, 13, 11.0, 7)
    _coals(c, 16, 11, 0.75)
    # flames: three tongues off the coals, white at the root
    for (x, top, w) in ((16, 1, 2.6), (11, 5, 1.8), (21, 4, 1.8)):
        for y in range(top, 10):
            t = (y - top) / float(10 - top)
            ww = w * math.sin(t * math.pi * 0.5)
            c.hline(x - ww, x + ww, y, f[3] if t < 0.35 else f[2])
            if t > 0.5:
                c.hline(x - ww * 0.5, x + ww * 0.5, y, f[1])
    c.set(16, 8, f[0])
    c.set(16, 9, f[0])
    return c


# ==============================================================================================
# 5. THE LIGHTNING ROD - Stormbound
# Copper and steel. The steel is the rod, the copper is everything wound on or fitted to it - the
# coil, the tip's collar, the lens ring - and that copper is the family mark. The lightning ramp
# appears only where current is actually jumping.
# ==============================================================================================

def _spark(c, pts, core=False):
    l = bl.ELEMENTS["lightning"]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        c.line(ax, ay, bx, by, l[2])
    for (x, y) in pts[1:-1]:
        c.set(x, y, l[1])
    if core:
        c.set(pts[0][0], pts[0][1], l[0])


def _shaft(c, cx, top, bot):
    c.rect(cx - 1, top, cx + 1, bot, STEEL[2])
    _rim(c, STEEL, (cx - 2, top - 1, cx + 2, bot + 1))


def _coil(c, cx, top, turns, rx, pitch):
    """Copper wire wound round the shaft: the front half of each turn, with the shaft between."""
    hi, lit, mid, shade, deep = COPPER
    for i in range(turns):
        y = top + i * pitch
        c.line(cx - rx, y + 1, cx + rx, y - 1, mid)
        c.line(cx - rx, y + 2, cx + rx, y, shade)
        c.set(cx - rx, y + 1, lit)
        c.set(cx - rx + 1, y + 1, lit)
        c.set(cx - rx + 2, y + 1, hi if i == 0 else lit)
        c.set(cx + rx, y, deep)


def _tip(c, cx, top, bot):
    """The finial: a long steel point over a ball and a copper collar."""
    hi, lit, mid, shade, deep = STEEL
    ch, cl, cm, cs, cd = COPPER
    ball = bot - 6
    for y in range(top, ball - 1):
        t = (y - top) / float(max(1, ball - 2 - top))
        w = 0.4 + 2.0 * t
        c.hline(cx - w, cx + w, y, mid)
        c.hline(cx + 1, cx + w, y, shade)
    _rim(c, STEEL, (cx - 4, top - 1, cx + 4, ball - 1))
    c.vline(cx - 1, top + 3, ball - 3, lit)
    c.set(cx, top, hi)
    _lit_disc(c, cx, ball, 2.6, "steel")
    c.rect(cx - 3, ball + 3, cx + 3, ball + 5, cm)
    c.hline(cx - 3, cx + 3, ball + 3, cl)
    c.hline(cx - 3, cx + 3, ball + 5, cd)
    c.set(cx - 3, ball + 3, ch)


def _lens(c, cx, cy, r, cracked=True):
    """Glass in a copper ring, with a lug top and bottom where it clamps to the rod."""
    gh, gl, gm, gs, gd = GLASS
    for dy in (-1, 1):
        c.rect(cx - 1, cy + dy * (r + 0.5) - 1, cx + 1, cy + dy * (r + 0.5) + 1, COPPER[2])
    c.disc(cx, cy, r, r, COPPER[2])
    _rim(c, COPPER, (int(cx - r - 2), int(cy - r - 3), int(cx + r + 2), int(cy + r + 3)))
    # A small lens gets a thinner ring: at r=5 a two-pixel ring left a three-pixel glass and the
    # whole thing read as a copper block on the rod.
    g = r - (2 if r >= 7 else 1.3)
    c.disc(cx, cy, g, g, gm)
    c.disc(cx + 0.6, cy + 0.6, g - 1, g - 1, gs)
    c.disc(cx - g * 0.35, cy - g * 0.35, g * 0.35, g * 0.35, gl)
    c.set(cx - g * 0.5, cy - g * 0.5, gh)
    if cracked:
        # The split: a jagged line clean across, occlusion with a lit lip on the key side, and
        # the lower shard a step darker - it has dropped out of true.
        s = g / 8.0
        pts = [(cx - 7 * s, cy - 5 * s), (cx - 2 * s, cy - 1 * s), (cx + 1 * s, cy - 3 * s),
               (cx + 3 * s, cy + 2 * s), (cx + 7 * s, cy + 5 * s)]
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            c.line(ax, ay, bx, by, OCC)
            c.line(ax, ay - 1, bx, by - 1, gh)


def storm_lattice():
    """Copper Coil: the winding, on a stub of the rod, with current jumping off its ends."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 6)
    _shaft(c, 16, 3, 28)
    _coil(c, 16, 7, 6, 8, 3)
    _spark(c, [(8, 8), (5, 5), (6, 3), (3, 1)], core=True)
    _spark(c, [(24, 22), (27, 24), (26, 26), (29, 28)])
    return c


def ethereal_blade():
    """Rod Tip: the sharp finial, alive at its point."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 5)
    _tip(c, 16, 4, 28)
    l = bl.ELEMENTS["lightning"]
    _spark(c, [(16, 3), (12, 2), (11, 0)])
    _spark(c, [(17, 3), (21, 1), (23, 2)])
    c.set(16, 3, l[0])
    c.rect(15, 26, 17, 28, STEEL[2])
    _rim(c, STEEL, (14, 26, 18, 28))
    return c


def cracked_prism():
    """Split Lens: the rod's glass, cracked through, in its copper ring."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 8)
    _lens(c, 16, 15, 11.0)
    return c


def set_stormbound():
    """The Lightning Rod, crackling: tip on top, the lens below it, the coil round the shaft."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 7)
    _shaft(c, 16, 6, 28)
    c.rect(11, 27, 21, 28, COPPER[2])
    _rim(c, COPPER, (10, 26, 22, 28))
    _coil(c, 16, 21, 3, 6, 2)
    _lens(c, 16, 14, 5.5, cracked=True)
    _tip(c, 16, 0, 9)
    _spark(c, [(15, 1), (10, 3), (11, 6), (6, 8)], core=True)
    _spark(c, [(17, 1), (22, 2), (21, 5), (26, 6)])
    _spark(c, [(22, 19), (25, 21), (24, 24), (28, 25)])
    return c


# ==============================================================================================
# 6. THE JEWELLED CROWN - Elemental Mastery (and the crown is shared with the Regalia)
# One gold crown with three sockets in its band. The three jewels are cut alike to fit them; only
# their element ramp differs. The empty crown and the jewelled crown are the same function.
# ==============================================================================================

JEWELS = ("ice", "lightning", "arcane")


def _crown(c, cx, top, bot, half, jewels=None):
    hi, lit, mid, shade, deep = GOLD
    band_top = bot - max(4, int((bot - top) * 0.42))
    # the three points, centre tallest, each tipped with a ball
    for (px, ptop, pw) in ((cx - half + 2, top + 3, 3), (cx, top, 3), (cx + half - 2, top + 3, 3)):
        c.poly([(px - pw, band_top), (px, ptop + 1), (px + pw, band_top)], mid)
        c.disc(px, ptop + 1, 1.3, 1.3, mid)
    c.rect(cx - half, band_top, cx + half, bot, mid)
    c.rect(cx + 2, band_top, cx + half, bot, shade)
    _rim(c, GOLD, (int(cx - half - 2), top - 2, int(cx + half + 2), bot + 1))
    c.hline(cx - half, cx + half, band_top, lit)
    c.hline(cx - half + 1, cx + half - 1, band_top + 1, OCC)
    c.hline(cx - half, cx + half, bot, deep)
    c.set(cx - half + 1, top + 3, hi)
    c.set(cx - 1, top + 1, hi)
    # the sockets: three holes in the band, empty unless the jewels are given
    sr = max(1.2, (bot - band_top - 2) / 2.2)
    sy = (band_top + 2 + bot) / 2.0
    for i, sx in enumerate((cx - half * 0.62, cx, cx + half * 0.62)):
        c.disc(sx, sy, sr + 0.7, sr + 0.7, lit if sx <= cx else mid)
        if jewels:
            cut_gem(c, sx, sy, sr, bl.ELEMENTS[jewels[i]])
        else:
            c.disc(sx, sy, sr, sr, OCC)
            c.set(sx + sr * 0.5, sy + sr * 0.5, deep)


def _jewel(name):
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 7)
    cut_gem(c, 16, 15, 11.0, bl.ELEMENTS[name])
    return c


def aegis_crown():
    """The Crown: the gold band, empty. It is a piece of TWO sets - the jewels go into it, and it
    is what the Regalia is worn with - so it is drawn exactly as both set icons draw it."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 12)
    _crown(c, 16, 6, 26, 12)
    return c


def frozen_tear():
    """Frost Jewel: the crown's cut, in ice."""
    return _jewel("ice")


def thunderstone():
    """Storm Jewel: the crown's cut, in lightning."""
    return _jewel("lightning")


def crystal_prism():
    """Prism Jewel: the crown's cut, in arcane."""
    return _jewel("arcane")


def set_elemental_mastery():
    """The Jewelled Crown: the band with all three stones set."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 12)
    _crown(c, 16, 5, 27, 13, jewels=JEWELS)
    return c


# ==============================================================================================
# 7. THE REGALIA - Fortune's Favor
# The crown again, plus the two things a monarch holds. All gold, which is the family mark; the
# ring and the orb are each given ONE engraved line so they are not just two gold circles.
# ==============================================================================================

def _signet(c, cx, cy, s=1.0):
    """A heavy ring seen at an angle, with a flat oval bezel on top carrying the seal."""
    hi, lit, mid, shade, deep = GOLD
    c.ring(cx, cy + 3 * s, 8 * s, 5.5 * s, mid, max(2, int(3 * s)))
    _rim(c, GOLD, (int(cx - 9 * s - 1), int(cy - 6 * s), int(cx + 9 * s + 1), int(cy + 9 * s + 1)))
    bx, by = cx, cy - 3 * s
    c.disc(bx, by + 1, 6.5 * s, 3.8 * s, shade)
    c.disc(bx, by, 6.5 * s, 3.8 * s, mid)
    c.disc(bx - 0.5, by - 0.3, 4.6 * s, 2.5 * s, lit)
    # the seal: a cut mark in the face
    c.vline(bx, by - 2 * s, by + 1 * s, deep)
    c.hline(bx - 2 * s, bx + 2 * s, by - 0.5 * s, deep)
    c.set(bx - 5 * s, by - 1 * s, hi)


def _orb(c, cx, cy, r):
    """A gold sphere with a band round its equator, a half-band over the top, and a cross."""
    hi, lit, mid, shade, deep = GOLD
    _lit_disc(c, cx, cy, r, "gold")
    c.disc(cx + r * 0.3, cy + r * 0.3, r * 0.55, r * 0.55, shade)
    c.disc(cx - r * 0.35, cy - r * 0.35, r * 0.32, r * 0.32, lit)
    c.hline(cx - r + 1, cx + r - 1, cy, deep)
    c.hline(cx - r + 1, cx + r - 1, cy - 1, lit)
    c.vline(cx, cy - r + 1, cy - 1, deep)
    # the cross on top
    ct = cy - r - max(3, int(r * 0.6))
    c.rect(cx - 0.5, ct, cx + 0.5, cy - r, mid)
    c.hline(cx - 1.5, cx + 1.5, ct + 1, mid)
    _rim(c, GOLD, (int(cx - 3), int(ct - 1), int(cx + 3), int(cy - r + 1)))
    c.set(cx, ct, hi)


def lucky_coin():
    """Signet Ring. The seal is the only mark on it, and it is what says "royal" rather than
    "wedding band"."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 9)
    _signet(c, 16, 16, 1.3)
    return c


def compass_rose():
    """Orb of State: gold, banded, with a cross on top."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 9)
    _orb(c, 16, 18, 10.0)
    return c


def set_fortunes_favor():
    """The Regalia laid out: the crown above, the orb and the ring beneath it."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 13)
    _crown(c, 16, 1, 13, 10)
    _orb(c, 23, 22, 6.5)
    _signet(c, 8, 22, 0.8)
    return c


# ==============================================================================================
# 8. THE SEVEN-LEAGUE BOOTS - Speed Demon
# One pair: the same leather, the same quicksilver buckle. The right boot is the left one
# mirrored, with a rune cut into its shaft so the two are not literally the same icon. The
# lighting is NOT mirrored - `_rim()` relights the flipped silhouette from the upper left.
# ==============================================================================================

BOOT = [(7, 0), (17, 0), (17, 15), (18, 19), (18, 23), (1, 23), (0, 21), (1, 18), (5, 16),
        (8, 14), (7, 0)]


def _boot(c, ox, oy, s, mirror, rune):
    hi, lit, mid, shade, deep = LEATHER
    sh, sl, sm, ss, sd = STEEL
    W = 18.0

    def P(x, y):
        return (ox + s * ((W - x) if mirror else x), oy + s * y)

    c.poly([P(x, y) for (x, y) in BOOT], mid)
    # the far side of the shaft and the back of the heel turn away from the light
    shade_pts = [(13, 3), (17, 3), (17, 15), (18, 19), (18, 21), (13, 21)]
    if mirror:
        shade_pts = [(4, 3), (7, 3), (8, 14), (5, 16), (1, 18), (1, 21), (4, 21)]
    c.poly([P(x, y) for (x, y) in shade_pts], shade)
    _rim(c, LEATHER)
    # the turned-down cuff
    c.poly([P(6, 0), P(18, 0), P(18, 3), P(6, 3)], shade)
    a, b = P(6, 0), P(18, 0)
    c.hline(min(a[0], b[0]), max(a[0], b[0]), a[1], lit)
    a, b = P(6, 4), P(18, 4)
    c.hline(min(a[0], b[0]) + 1, max(a[0], b[0]) - 1, a[1], OCC)
    # the sole and the heel block
    a, b = P(1, 23), P(18, 23)
    c.hline(min(a[0], b[0]), max(a[0], b[0]), a[1], deep)
    a, b = P(13, 21), P(18, 23)
    c.rect(min(a[0], b[0]), a[1], max(a[0], b[0]), b[1], deep)
    # the strap across the instep, and its quicksilver buckle
    a, b = P(7, 13), P(17, 14)
    c.rect(min(a[0], b[0]), a[1], max(a[0], b[0]), b[1], shade)
    bx, by = P(11, 12.5)
    bw = max(1, int(round(2.5 * s)))
    c.rect(bx - bw, by, bx + bw, by + bw + 1, sm)
    c.hline(bx - bw, bx + bw, by, sh)
    c.vline(bx - bw, by, by + bw + 1, sl)
    c.set(bx, by + 1, sd)
    if rune:
        # The rune: a pair of chevrons pointing the way the boot walks, cut through the leather
        # to something lit underneath. Wind ramp, because this is the piece that goes fast.
        w = bl.ELEMENTS["wind"]
        for k in (0, 1):
            x0, y0 = P(13 - k * 3, 5)
            x1, y1 = P(11 - k * 3, 7.5)
            x2, y2 = P(13 - k * 3, 10)
            c.line(x0, y0, x1, y1, w[2])
            c.line(x1, y1, x2, y2, w[2])
            c.set(x1, y1, w[0])


def quicksilver_pendant():
    """Left Boot: toe to the left, leather and a quicksilver buckle."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 15, 10)
    _boot(c, 5, 4, 1.0, False, False)
    return c


def haste_rune():
    """Right Boot: the left one mirrored, with the rune on it that makes them distinguishable."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 17, 10)
    _boot(c, 9, 4, 1.0, True, True)
    return c


def set_speed_demon():
    """The Seven-League Boots: the pair, side by side, toes out."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 14)
    _boot(c, 0, 10, 0.78, False, False)
    _boot(c, 17, 10, 0.78, True, True)
    return c


# ==============================================================================================
# 9. THE REAPER'S SCYTHE - Deathbringer
# Dark steel blade, grey-wood snath, grey whetstone. The steel ferrule is drawn on the snath as
# well as the blade, so even the wooden piece carries the blade's metal.
# ==============================================================================================

def _scythe_blade(c, heel, point, sag, width):
    """A long curved blade hanging off a spine: lit on the spine, a bright honed edge underneath."""
    hi, lit, mid, shade, deep = STEEL
    (hx, hy), (px, py) = heel, point
    n = int(abs(hx - px)) * 2 + 2
    for k in range(n + 1):
        t = k / float(n)
        x = hx + (px - hx) * t
        y = hy + (py - hy) * t + sag * math.sin(t * math.pi) * (1.0 - t * 0.3)
        w = width * (1.0 - t) ** 0.7 + 0.6
        c.vline(x, y, y + w, mid)
        c.set(x, y + 1, shade) if w > 2.5 else None
    _rim(c, STEEL, (int(min(hx, px)) - 2, int(min(hy, py)) - 2, int(max(hx, px)) + 2,
                    int(max(hy, py) + sag + width) + 2))
    # The honed edge is the brightest line in the icon, because it is what the thing is FOR.
    for k in range(n + 1):
        t = k / float(n)
        if t > 0.94:
            continue
        x = hx + (px - hx) * t
        y = hy + (py - hy) * t + sag * math.sin(t * math.pi) * (1.0 - t * 0.3)
        w = width * (1.0 - t) ** 0.7 + 0.6
        c.set(x, y + w, hi if k % 5 else lit)
    c.set(px, py, hi)


def _snath(c, pts, w, nibs, ferrule_at=None):
    """The long grey-wood handle, with grips that stick out to the side."""
    hi, lit, mid, shade, deep = GREYWOOD
    _thick(c, pts, w, mid)
    for (nx, ny, dx, dy) in nibs:
        _thick(c, [(nx, ny), (nx + dx, ny + dy)], 2, mid)
    _rim(c, GREYWOOD)
    for (nx, ny, dx, dy) in nibs:
        c.set(nx + dx, ny + dy, hi)
    if ferrule_at:
        fx, fy = ferrule_at
        c.rect(fx - 2, fy, fx + 1, fy + 2, STEEL[2])
        c.hline(fx - 2, fx + 1, fy, STEEL[1])
        c.set(fx - 2, fy, STEEL[0])


def _whetstone(c, cx, cy, length, width, deg):
    """A spindle of grey stone, worn hollow along the side it is drawn across a blade on."""
    hi, lit, mid, shade, deep = BONE
    a = math.radians(deg)
    ux, uy = math.cos(a), math.sin(a)
    for y in range(int(cy - length), int(cy + length) + 1):
        for x in range(int(cx - length), int(cx + length) + 1):
            dx, dy = x - cx, y - cy
            along = dx * ux + dy * uy
            across = -dx * uy + dy * ux
            if (along / length) ** 2 + (across / width) ** 2 <= 1.0:
                c.set(x, y, shade if across > width * 0.25 else mid)
    _rim(c, BONE, (int(cx - length - 1), int(cy - length - 1), int(cx + length + 1), int(cy + length + 1)))
    # the worn groove, and one glint where it is still wet
    for k in range(-int(length * 0.6), int(length * 0.6) + 1):
        c.set(cx + ux * k - uy * 0.5, cy + uy * k + ux * 0.5, deep)
    c.set(cx - ux * length * 0.4 - uy * (width * 0.5), cy - uy * length * 0.4 + ux * -width * 0.5, hi)


def spectral_fang():
    """Scythe Blade: the long curved steel, with its tang to fix it to the snath."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 15, 9)
    _thick(c, [(27, 13), (28, 6)], 2, STEEL[3])
    c.rect(26, 5, 29, 7, STEEL[2])
    _rim(c, STEEL, (24, 3, 31, 15))
    _scythe_blade(c, (27, 10), (2, 16), 6.0, 6.0)
    return c


def wrath_amulet():
    """Scythe Snath: the long curved handle, with its two grips and the ferrule the blade fits."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 7)
    _snath(c, [(23, 3), (20, 12), (16, 20), (12, 28)], 3,
           nibs=[(20, 11, -6, -1), (15, 21, -6, -1)], ferrule_at=(24, 2))
    return c


def duellists_chalk():
    """Whetstone: the grey sharpening stone every scythe is carried with."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 10)
    _whetstone(c, 16, 16, 12.0, 4.6, -35)
    return c


def set_deathbringer():
    """The Reaper's Scythe, assembled: snath, blade, and the whetstone lashed to the snath."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 18, 8)
    _snath(c, [(22, 4), (21, 13), (18, 21), (16, 28)], 3,
           nibs=[(21, 12, -5, -1), (18, 21, -5, 0)], ferrule_at=(23, 3))
    _scythe_blade(c, (22, 6), (1, 14), 5.0, 4.5)
    # The whetstone is carried with the scythe, not fixed to it, so it lies at the foot of the
    # snath rather than being lashed on - the lashed version was a knot of three materials at
    # the one place the eye needed to read "handle".
    _whetstone(c, 8, 26, 5.5, 2.2, -12)
    return c


# ==============================================================================================
# 10. THE TITHE CHALICE - Life Drain
# Dark silver and deep red. Silver is steel; the red is the cloth-red row, which is a maroon, so
# it is kept to its top two stops and given one fire-edge glint where it needs to be wet or lit.
# ==============================================================================================

def _cup(c, cx, rim_y, rx, depth):
    """The bowl, brim-full of something dark red."""
    hi, lit, mid, shade, deep = STEEL
    rh, rl, rm, rs, rd = CLOTH_RED
    for y in range(rim_y, rim_y + depth + 1):
        t = (y - rim_y) / float(depth)
        w = rx * (1.0 - 0.62 * t ** 1.7)
        c.hline(cx - w, cx + w, y, mid)
        c.hline(cx + w * 0.35, cx + w, y, shade)
    _rim(c, STEEL, (int(cx - rx - 1), rim_y - 2, int(cx + rx + 1), rim_y + depth + 1))
    # the brim, and the tithe in it
    c.disc(cx, rim_y, rx, 2.4, lit)
    c.disc(cx, rim_y + 0.4, rx - 1.5, 1.4, rh)
    c.hline(cx - rx + 3, cx - 1, rim_y, bl.ELEMENTS["fire"][3])
    c.set(cx + rx - 1, rim_y, mid)
    c.set(cx - rx + 1, rim_y - 1, hi)
    # an engraved band below the brim
    c.hline(cx - rx + 2, cx + rx - 2, rim_y + 4, deep)


def _heartstone(c, cx, cy, r):
    """A red stone in a silver collet. Faceted like the crown jewels? No - cabochon, smooth and
    domed, so it reads as blood rather than as a fourth jewel for the other set."""
    rh, rl, rm, rs, rd = CLOTH_RED
    c.disc(cx, cy, r + 1.4, r + 1.4, STEEL[2])
    _rim(c, STEEL, (int(cx - r - 3), int(cy - r - 3), int(cx + r + 3), int(cy + r + 3)))
    c.disc(cx, cy, r, r, rl)
    c.disc(cx - r * 0.15, cy - r * 0.15, r * 0.72, r * 0.72, rh)
    c.disc(cx - r * 0.35, cy - r * 0.35, r * 0.3, r * 0.3, bl.ELEMENTS["fire"][3])
    c.set(cx - r * 0.45, cy - r * 0.45, LEATHER[0])
    c.set(cx + r * 0.6, cy + r * 0.4, rs)


def _stem(c, cx, top, bot, foot_rx):
    c.rect(cx - 1, top, cx + 1, bot - 2, STEEL[2])
    c.disc(cx, bot - 1, foot_rx, 1.8, STEEL[2])
    _rim(c, STEEL, (int(cx - foot_rx - 1), top - 1, int(cx + foot_rx + 1), bot + 1))
    c.hline(cx - foot_rx + 1, cx + foot_rx - 1, bot - 2, STEEL[1])


def _wormwood(c, root, sprigs, bend=0.0):
    """Sprigs of a bitter silver-green herb: curved stems with leaves that point up and out.

    The first pass put a leaf pixel either side of every other stem pixel, and six straight
    stems doing that is a grid - it read as a broom. Leaves are short diagonal strokes now,
    alternating sides and staggered, on stems that bow, and the stems are fewer.
    """
    hi, lit, mid, shade, deep = GREYWOOD
    rx, ry = root
    for j, (tx, ty) in enumerate(sprigs):
        n = int(max(abs(tx - rx), abs(ty - ry))) + 1
        pts = []
        for k in range(n + 1):
            t = k / float(n)
            # bow each stem sideways, more the further it gets from the root
            ox = bend * math.sin(t * math.pi * 0.9) * (1 if j % 2 else -1)
            pts.append((rx + (tx - rx) * t + ox, ry + (ty - ry) * t))
        for (x, y) in pts:
            c.set(x, y, lit)
        for k in range(2, n, 2):
            x, y = pts[k]
            side = -1 if (k // 2 + j) % 2 else 1
            # a leaf: two pixels, pointing up and away from the stem
            c.set(x + side, y - 1, lit if side < 0 else mid)
            c.set(x + side * 2, y - 2, hi if side < 0 else lit)
        c.set(pts[-1][0], pts[-1][1], hi)


def essence_chalice():
    """Chalice Cup: the bowl alone, broken off its stem at the socket."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 6)
    _cup(c, 16, 9, 12.0, 14)
    c.rect(14, 24, 18, 27, STEEL[3])
    _rim(c, STEEL, (13, 23, 19, 28))
    c.hline(14, 18, 27, OCC)
    return c


def heart_of_renewal():
    """Heartstone: the red stone, in the knop it is set into on the stem."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 16, 6)
    c.rect(14, 2, 18, 28, STEEL[2])
    _rim(c, STEEL, (13, 1, 19, 28))
    for y in (4, 26):
        c.hline(14, 18, y, OCC)
    _heartstone(c, 16, 15, 7.0)
    return c


def wormwood_tithe():
    """Tithe of Wormwood: the bundle of herb that is offered into the cup, tied in the chalice's
    red."""
    c = raster.Canvas(CELL, CELL, None)
    rh, rl, rm, rs, rd = CLOTH_RED
    _shadow(c, 16, 6)
    _wormwood(c, (16, 21), [(7, 6), (12, 2), (17, 1), (22, 3), (27, 8)], bend=1.5)
    # the stalks below the tie
    for x in (15, 16, 17):
        c.vline(x, 22, 28, GREYWOOD[3])
    c.vline(15, 23, 27, GREYWOOD[2])
    # the cord
    c.rect(13, 20, 19, 23, rl)
    c.hline(13, 19, 20, rh)
    c.hline(13, 19, 23, rs)
    c.set(13, 21, rh)
    return c


def set_life_drain():
    """The Tithe Chalice, whole: cup, heartstone in the stem, wormwood draped over the rim."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 15, 8)
    _stem(c, 15, 14, 28, 7.0)
    _cup(c, 15, 6, 10.0, 9)
    _heartstone(c, 15, 20, 2.6)
    # the herb, laid over the right-hand side of the brim and hanging down outside it
    _wormwood(c, (22, 6), [(29, 16), (27, 19)], bend=1.0)
    _wormwood(c, (22, 6), [(18, 1)], bend=0.5)
    c.rect(21, 5, 23, 7, CLOTH_RED[1])
    c.set(21, 5, CLOTH_RED[0])
    return c


# ==============================================================================================
# 11. THE TOME RELICS - the Scribe's Penknife and the Blank Folio (no set)
# The two relics that act on the spell book rather than on the wizard, and the only relics that
# are pieces of nothing (ChestItemCatalog.TomeRelicIds). What ties them instead is the VELLUM: the
# leaf is vellum, and the curl coming off the knife's edge is a shaving of it. Vellum is the flesh
# row's lit and base stops, never its hi - flesh hi #F2DCC4 IS the parchment page these icons are
# shown on in the chest, and a leaf drawn in it would vanish there. Every vellum edge is outlined
# in flesh deep for the same reason.
# ==============================================================================================

VELLUM = _ramp("flesh")


def _shaving(c, x, y):
    """A curl of vellum scraped off a page: a ribbon that leaves the edge and rolls up on itself,
    lit on its outer face. A closed ring read as a coin; the tail is what makes it a shaving."""
    hi, lit, base, shade, deep = VELLUM
    for (dx, dy, t) in [(0, 0, lit), (1, 1, lit), (2, 2, base),           # the tail, off the edge
                        (3, 2, lit), (4, 2, lit), (5, 3, lit), (5, 4, base),  # the roll's back
                        (4, 5, shade), (3, 5, deep), (2, 4, deep), (3, 4, base)]:
        c.set(x + dx, y + dy, t)


def scribes_penknife():
    """Scribe's Penknife: the short curved knife a scribe scrapes a mistake off the page with,
    and the shaving it has just taken. Handle low left, edge up right, like a knife picked up."""
    c = raster.Canvas(CELL, CELL, None)
    _shadow(c, 15, 9)
    # the handle: warm wood, thick enough to read at icon size, lit by the house rim
    _thick(c, [(5, 27), (14, 18)], 4, WOOD[2])
    _rim(c, WOOD, (2, 14, 18, 30))
    c.set(5, 27, WOOD[3])
    # the gold ferrule where the blade is seated
    _thick(c, [(14, 18), (16, 16)], 5, GOLD[2])
    _rim(c, GOLD, (11, 13, 19, 21))
    # the blade: a straight spine on top, the edge bellied below and sweeping up to the point
    hi, lit, mid, shade, deep = STEEL
    c.poly([(16, 15), (27, 4), (28, 3), (27, 8), (24, 12), (20, 16), (17, 18)], mid)
    for (ax, ay, bx, by) in [(16, 15, 27, 4)]:
        c.line(ax, ay, bx, by, lit)
    # the honed edge: the brightest line on the knife, because the edge is the point of it
    for (x, y) in [(18, 17), (19, 17), (20, 16), (21, 15), (22, 14), (23, 13), (24, 12),
                   (25, 11), (26, 9), (27, 7), (27, 6)]:
        c.set(x, y, hi)
    c.set(28, 3, hi)
    for (x, y) in [(18, 16), (20, 15), (22, 13)]:
        c.set(x, y, shade)
    # the shaving it took, falling away under the edge
    _shaving(c, 21, 18)
    return c


def blank_folio():
    """Blank Folio: one leaf of vellum, gilt on its edges, ruled and never written on, with the
    sewing holes down its spine edge where it is bound into the book."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = VELLUM
    _shadow(c, 16, 9)
    page = [(8, 4), (24, 3), (25, 22), (20, 28), (9, 28)]
    c.poly(page, lit)
    # the turned corner: the back of the leaf showing, a shade darker, under a gilt fold line
    c.poly([(25, 22), (20, 28), (19, 23)], base)
    c.line(19, 23, 25, 22, shade)
    c.line(19, 23, 20, 28, shade)
    # outline: deep all round, so the leaf holds its shape on the parchment page as well as on stone
    for (a, b) in zip(page, page[1:] + page[:1]):
        c.line(a[0], a[1], b[0], b[1], deep)
    # gilt: the head and the fore-edge, which is where a bound leaf shows its gold
    c.line(9, 4, 23, 3, GOLD[1])
    c.line(24, 4, 24, 21, GOLD[1])
    c.set(23, 4, GOLD[0])
    # the ruling - faint, and empty. A blank page has to look written-ON-able, not just pale.
    for y in (9, 13, 17, 21):
        for x in range(12, 22 if y < 21 else 18, 2):
            c.set(x, y, base)
    # the spine edge: a light key down the left, and the stitches that bind it in
    c.vline(9, 5, 27, hi)
    for y in (7, 12, 17, 22, 26):
        c.set(10, y, CLOTH_RED[0])
        c.set(9, y, CLOTH_RED[1])
    return c


def marginalia():
    """Marginalia - not a relic: the level-up card for a full book with nothing left to offer
    (Player.MarginaliaId). A quill over a scrap of vellum with a line written in the margin. In
    this file because it is drawn in the tome relics' vellum and lit the same way."""
    c = raster.Canvas(CELL, CELL, None)
    hi, lit, base, shade, deep = VELLUM
    _shadow(c, 14, 10)
    # the scrap: square to the cell, so its outline is unbroken at icon size (a tilted one broke up)
    c.rect(4, 13, 20, 27, lit)
    c.hline(4, 20, 13, deep)
    c.hline(4, 20, 27, deep)
    c.vline(4, 13, 27, deep)
    c.vline(20, 13, 27, deep)
    c.hline(5, 19, 14, hi)
    # the note, in ink: three written lines, the last one short, the first ending at the nib. Lines
    # and not marks: two short squiggles and a dash read as a face.
    ink = bl.tone("violet", "shade")
    for (x0, x1, y) in [(6, 13, 18), (6, 18, 21), (6, 14, 24)]:
        for x in range(x0, x1 + 1):
            c.set(x, y - (1 if (x // 3) % 2 else 0), ink)
    # the quill: bone-white vane down to a steel nib resting at the end of the line
    bh, bl_, bb, bs, bd = BONE
    vane = [(29, 2), (25, 4), (20, 9), (17, 14), (16, 16), (19, 13), (24, 9), (28, 5)]
    c.poly(vane, bb)
    c.line(29, 2, 16, 16, bh)
    for (x, y) in [(27, 5), (25, 7), (23, 9), (21, 11), (19, 13)]:
        c.set(x, y, bs)
    c.line(16, 16, 13, 19, STEEL[2])
    c.set(13, 19, STEEL[0])
    c.set(14, 18, STEEL[3])
    return c


RELICS = [
    ("relic_key", relic_key), ("ember_flask", ember_flask), ("wrath_amulet", wrath_amulet),
    ("ethereal_blade", ethereal_blade), ("spectral_fang", spectral_fang),
    ("obsidian_heart", obsidian_heart), ("aegis_sigil", aegis_sigil), ("iron_fang", iron_fang),
    ("basalt_carapace", basalt_carapace), ("aegis_crown", aegis_crown),
    ("ironhide_cloak", ironhide_cloak), ("protective_ward", protective_ward),
    ("vial_of_vitality", vial_of_vitality), ("heart_of_renewal", heart_of_renewal),
    ("phylactery", phylactery), ("essence_chalice", essence_chalice),
    ("quicksilver_pendant", quicksilver_pendant), ("haste_rune", haste_rune),
    ("compass_rose", compass_rose), ("lucky_coin", lucky_coin),
    ("storm_lattice", storm_lattice), ("inferno_core", inferno_core),
    ("frozen_tear", frozen_tear), ("thunderstone", thunderstone),
    ("crystal_prism", crystal_prism), ("cracked_prism", cracked_prism),
    ("duellists_chalk", duellists_chalk), ("hoarfrost_nail", hoarfrost_nail),
    ("wormwood_tithe", wormwood_tithe),
    ("scribes_penknife", scribes_penknife), ("blank_folio", blank_folio),
]

# Not relics, but drawn with them (see marginalia()). assets/bonelight/ui/icons is the home of the
# odd UI icon that belongs to no roster.
OUT_ICONS = os.path.join(ROOT, "assets", "bonelight", "ui", "icons")
EXTRAS = [("marginalia", marginalia)]

SETS = [
    ("vaultguard", set_vaultguard), ("emberline", set_emberline), ("stormbound", set_stormbound),
    ("bastion_of_spikes", set_bastion), ("deathbringer", set_deathbringer),
    ("eternal_guardian", set_eternal_guardian), ("life_drain", set_life_drain),
    ("elemental_mastery", set_elemental_mastery), ("speed_demon", set_speed_demon),
    ("fortunes_favor", set_fortunes_favor),
]


def _emit(out_dir, icons, label):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    ok = True
    for name, fn in icons:
        canvas = fn()
        bad = canvas.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s:" % name)
            for h, n in bad[:6]:
                print("   %s  x%d" % (h, n))
            continue
        canvas.scaled(2).save(os.path.join(out_dir, name + ".png"))
    print("  %-22s %d icons" % (label, len(icons)))
    return ok


def main():
    ok = _emit(OUT_RELICS, RELICS, "relics")
    ok &= _emit(OUT_SETS, SETS, "full set enchantments")
    ok &= _emit(OUT_ICONS, EXTRAS, "level-up extras")
    if not ok:
        return 1
    print("  %d icons, palette clean" % (len(RELICS) + len(SETS) + len(EXTRAS)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
