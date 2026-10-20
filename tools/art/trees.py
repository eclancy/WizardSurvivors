# -*- coding: utf-8 -*-
"""Trees and bushes at the size of the world they stand in. Run: python tools/art/trees.py

Writes `assets/bonelight/world/trees/` - one PNG per tree or bush, at x2 like every other world
sprite - and `trees.json`, which Node2DGame reads for each sprite's ground line and trunk footprint.

WHY THIS EXISTS (2026-10). The trees in `world_props.py` were 48 px canopies seen from straight
above, drawn at x2: 96 px on screen, the height of the wizard. Reported as "trees should be bigger
so the size is closer to realistic". A grown tree is three to five times the height of a man, so
here a mature tree is 2.5 to 4 times the wizard's 80 px figure - 200 to 320 px on screen - and it is
drawn the way the rest of the world is drawn, in three-quarter view: a trunk standing on the ground
and a crown above it, so he can walk BEHIND the crown and in front of the trunk.

THE SIZES. Authored 1:1 and saved at x2 (`.ai/art-direction.md` section 1: one render scale for
everything; a big tree is a bigger cell, never a bigger scale).

  class    authored height   on screen   x the wizard's 80 px
  large    140-150           280-300     3.5-3.75
  medium   112-124           224-248     2.8-3.1
  small    76-96             152-192     1.9-2.4   (saplings and birches: the clearing's edge)
  bush     14-30             28-60       knee to waist

THE RULES, from the contract and from what went wrong before:

  - Foliage is `verdant`, never `lichen` - `.ai/terrain.md` section 4: a canopy on the ground's
    own ramp is a smudge at any size. Bark is `flesh` (oak), `skin` (birch, bleached wood),
    `stone` and `linen` (dead wood). Nothing is emissive; a tree is never the subject.
  - Lit from the upper left. Each leaf cluster is shaded as a lump of its own AND as part of the
    crown, so the crown turns as one form while every cluster still has an edge - detail density is
    internal boundaries, not more colours (the standing note on sprite density).
  - Dither only at the transition between two tones (`band`), never across a whole face: a field
    of independent pixels is static, which `.ai/terrain.md` section 3 found twice.
  - Every tree and bush has an `occ` contact shadow, and trees a dithered pool of shade under the
    crown, so it stands ON the ground.

THE GROUND LINE. Each sprite's contact line - where the trunk meets the ground - is `ground` in the
manifest, in pixels of the saved PNG. Node2DGame sorts and collides every tree at that line, not at
the middle of the sprite, which is what lets the wizard pass behind a crown and in front of a trunk.
`trunk` is the width of the trunk's foot in world pixels: the only part of a tree that stops
anything (on the ground's physics layer, so it meets his boots; see `.ai/terrain.md` section 10).
"""

import io
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "trees")
TAU = math.pi * 2.0
SCALE = 2
# How far below the trunk's foot each canvas runs, in authored pixels: room for the contact shadow
# and the near half of the shade pool.
FOOT = 6


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


VE = ramp("verdant")
FL = ramp("flesh")
SK = ramp("skin")
ST = ramp("stone")
LN = ramp("linen")
LI = ramp("lichen")
GO = ramp("gold")
RD = ramp("red")
VI = ramp("violet")

# Dead wood. ASH is the Ruins' and the Cave's - on stone floors `stone` itself was invisible, so it
# is the bone ramp's grey end over linen. BLEACHED is the Sands': sun-whitened, one step down from
# bone so the brightest pixel of a dead tree is never brighter than the wizard's rim.
ASH = [SK[2], SK[3], LN[2], SK[4], OCC]
BLEACHED = SK

BAYER = raster.BAYER8

# The key light, upper left, as a unit vector in image space.
LX, LY = -0.62, -0.78


def band(tones, v, x, y, width=0.34):
    """v in 0..1 (1 brightest) to one of `tones` (brightest first).

    Flat steps, dithered only within `width` of each boundary: a turning form reads as a few clear
    tones with a soft seam between them rather than as a gradient of noise.
    """
    n = len(tones)
    t = (1.0 - max(0.0, min(1.0, v))) * (n - 1)
    i = int(math.floor(t))
    f = t - i
    if i >= n - 1:
        return tones[-1]
    # squeeze the fraction so most of each step is flat
    f = max(0.0, min(1.0, (f - 0.5) / max(0.01, width) + 0.5))
    th = (BAYER[y % 8][x % 8] + 0.5) / 64.0
    return tones[i + 1] if f > th else tones[i]


def hash2(x, y, s):
    h = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


# --- ground ---------------------------------------------------------------------------------------

def contact(c, cx, base, rx):
    """The occlusion under the trunk: a solid ellipse, the darkest thing the tree owns."""
    c.disc(cx, base, rx, max(1.5, rx * 0.32), OCC)


def shade_pool(c, cx, base, rx, ry, density=0.5):
    """The crown's shade on the ground, centred a little right of the trunk (the light is upper
    left). Ordered half-tone of `occ`, thinning to the rim: a pool, never a disc with an edge."""
    for y in range(int(base - ry), int(base + ry) + 1):
        for x in range(int(cx - rx), int(cx + rx) + 1):
            nx = (x - cx) / float(rx)
            ny = (y - base) / float(ry)
            d = nx * nx + ny * ny
            if d > 1.0:
                continue
            keep = density * (1.0 - d ** 1.5)
            if (BAYER[y % 8][x % 8] + 0.5) / 64.0 < keep and c.get(x, y)[3] == 0:
                c.set(x, y, OCC)


# --- wood -----------------------------------------------------------------------------------------

def trunk(c, cx, base, top, w0, w1, bark, seed, flare=3, lean=0.0, marks=None):
    """A tapering trunk from `base` up to `top`, lit on the left, with bark streaks and a root
    flare. `bark` is a five-tone ramp; `marks` (birch) is a colour for horizontal lenticels."""
    rnd = random.Random(seed)
    hgt = max(1, base - top)
    for y in range(int(top), int(base) + 1):
        t = (base - y) / float(hgt)                 # 0 at the foot, 1 at the top
        half = (w0 + (w1 - w0) * t) * 0.5
        if t < 0.18:                                # the root flare
            half += flare * (1.0 - t / 0.18) ** 2
        mid = cx + lean * t * t * hgt * 0.1
        x0, x1 = int(round(mid - half)), int(round(mid + half))
        for x in range(x0, x1 + 1):
            u = (x - x0) / float(max(1, x1 - x0))  # 0 left (lit) .. 1 right
            v = 0.82 - u * 0.78
            if hash2(x, y // 3, seed) < 0.16:        # bark streaks run up the trunk
                v -= 0.28
            col = band(bark[1:], v, x, y, 0.2)
            if x == x1:
                col = bark[4]
            c.set(x, y, col)
        if marks and t > 0.08 and rnd.random() < 0.22:
            mx = x0 + 1 + rnd.randrange(max(1, x1 - x0 - 1))
            for k in range(rnd.randrange(1, 4)):
                c.set(mx + k, y, marks)
    return lambda yy: cx + lean * ((base - yy) / float(hgt)) ** 2 * hgt * 0.1


def limb(c, x, y, ang, length, width, bark, depth, rnd, tips=None, droop=0.0):
    """A branch and its forks, as a stroke that thins toward its tip. `tips` collects where the
    twigs end, for the crown to hang leaf clusters on."""
    steps = max(2, int(length))
    px, py = x, y
    for k in range(steps):
        t = k / float(steps)
        a = ang + droop * t
        nx = px + math.cos(a)
        ny = py + math.sin(a)
        wd = max(1.0, width * (1.0 - t * 0.6))
        for j in range(int(-wd // 2), int(wd // 2) + 1):
            col = bark[2] if j < 0 else (bark[3] if j == 0 else bark[4])
            if wd < 1.6:
                col = bark[3]
            c.set(int(round(nx - math.sin(a) * j)), int(round(ny + math.cos(a) * j)), col)
        px, py = nx, ny
    if tips is not None:
        tips.append((px, py))
    if depth <= 0:
        return
    for s in (-1, 1):
        if rnd.random() < 0.85:
            limb(c, px, py, ang + s * (0.35 + rnd.random() * 0.45), length * (0.55 + rnd.random() * 0.2),
                 width * 0.6, bark, depth - 1, rnd, tips, droop)


# --- leaves ---------------------------------------------------------------------------------------

def crown(c, clusters, tones, ccx, ccy, crx, cry, seed, ragged=0.35, gaps=0.0, rim=None, bias=-0.04):
    """Draw a crown of leaf clusters. clusters: [(x, y, rx, ry)], back to front.

    Each pixel's brightness is a mix of the cluster's own lumpy normal (so every cluster is a lump
    with a lit top-left and a shadowed underside) and its place in the whole crown (so the crown
    turns as one form, lit upper-left, dark lower-right and underneath). The cluster's lower-right
    rim, where it overlaps the cluster behind, is pushed a step darker: that seam is the internal
    edge that makes it read as many lumps of leaf rather than one green blob.
    """
    rim = rim if rim is not None else tones[-1]
    for (x0, y0, rx, ry) in clusters:
        for y in range(int(y0 - ry) - 1, int(y0 + ry) + 2):
            for x in range(int(x0 - rx) - 1, int(x0 + rx) + 2):
                nx = (x - x0) / float(rx)
                ny = (y - y0) / float(ry)
                d = nx * nx + ny * ny
                # a ragged, leafy outline: the edge is eaten by a hash, more at the rim
                if d > 1.0 - ragged * hash2(x, y, seed + 7) * 0.5:
                    continue
                if gaps and hash2(x // 2, y // 2, seed + 11) < gaps * d:
                    continue
                local = -(nx * LX + ny * LY) * (1.0 - d * 0.35)       # -1..1
                gx = (x - ccx) / float(crx)
                gy = (y - ccy) / float(cry)
                glob = -(gx * LX + gy * LY) * 0.8 - max(0.0, gy) * 0.35
                v = 0.5 + bias + local * 0.30 + glob * 0.34
                v += (hash2(x, y, seed) - 0.5) * 0.10
                col = band(tones, v, x, y)
                # the seam: the lower-right edge of a cluster, over whatever is behind it
                if d > 0.62 and nx * 0.6 + ny * 0.8 > 0.55:
                    col = rim if hash2(x, y, seed + 3) < 0.7 else tones[-2]
                c.set(x, y, col)
        # one or two leaf glints on the lit side of each cluster, on the lit half of the crown only
        if rx >= 4 and (x0 - ccx) * LX + (y0 - ccy) * LY > -crx * 0.15:
            gx0 = int(x0 - rx * 0.35)
            gy0 = int(y0 - ry * 0.45)
            c.set(gx0, gy0, tones[0])
            c.set(gx0 + 1, gy0, tones[0] if rx > 7 else tones[1])
            if rx > 8:
                c.set(gx0 - 2, gy0 + 2, tones[1])


def fill_clusters(rnd, cx, cy, rx, ry, n, rmin, rmax, top_bias=0.0):
    """Leaf clusters scattered inside an ellipse, sorted back (top) to front (bottom)."""
    out = []
    tries = 0
    while len(out) < n and tries < n * 30:
        tries += 1
        a = rnd.random() * TAU
        d = rnd.random() ** 0.62
        x = cx + math.cos(a) * d * rx
        y = cy + math.sin(a) * d * ry
        if top_bias and y > cy and rnd.random() < top_bias:
            continue
        r = rmin + (rmax - rmin) * rnd.random()
        out.append((x, y, r, r * 0.86))
    out.sort(key=lambda k: k[1] + k[2] * 0.3)
    return out


def berries(c, clusters, rnd, n):
    """Dark red berries with one bone-white glint each. Never a disc of an XP orb's lime, lilac or
    pale yellow, and in twos and threes so a single berry is never mistaken for a pickup."""
    for k in range(n):
        x0, y0, rx, ry = clusters[rnd.randrange(len(clusters))]
        bx = int(x0 + (rnd.random() - 0.3) * rx)
        by = int(y0 + (rnd.random() - 0.2) * ry)
        for (dx, dy) in ((0, 0), (2, 1), (1, -1))[:rnd.randrange(2, 4)]:
            c.set(bx + dx, by + dy, RD[0])
            c.set(bx + dx + 1, by + dy, RD[1])
            c.set(bx + dx, by + dy + 1, RD[2])
            c.set(bx + dx + 1, by + dy + 1, RD[3])
        c.set(bx, by, SK[2])


# --- trees ----------------------------------------------------------------------------------------

def broadleaf(seed, w, h, crown_frac=0.62, trunk_w=10, lean=0.0, n=34, rmin=7, rmax=14,
              bark=FL, leaves=VE[1:5], gaps=0.0):
    """An oak-like broadleaf: a short, thick trunk and a wide lumpy crown, with limbs showing in the
    crown's underside where the leaves thin."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w // 2
    crown_h = h * crown_frac
    ccy = 2 + crown_h * 0.5
    crx = w * 0.5 - 3
    cry = crown_h * 0.5
    shade_pool(c, cx + w * 0.06, base, w * 0.36, 7 + w * 0.03)
    contact(c, cx, base + 1, trunk_w * 0.9)
    top = ccy + cry * 0.15
    trunk(c, cx, base, top, trunk_w, trunk_w * 0.55, bark, seed, flare=trunk_w * 0.35, lean=lean)
    # limbs into the crown, drawn before it so the leaves cover all but their roots
    for s in (-1, 1, -1, 1):
        a = -math.pi / 2 + s * (0.5 + rnd.random() * 0.5)
        limb(c, cx + s * 1, top + cry * 0.35 + rnd.random() * 6, a, crown_h * 0.28, trunk_w * 0.45, bark, 1, rnd)
    clusters = fill_clusters(rnd, cx, ccy, crx - rmax * 0.6, cry - rmax * 0.55, n, rmin, rmax)
    crown(c, clusters, leaves, cx, ccy, crx, cry, seed, gaps=gaps)
    # the darker underside, where limbs show through
    for k in range(int(w * 0.6)):
        x = int(cx - crx * 0.6 + rnd.random() * crx * 1.2)
        y = int(ccy + cry * (0.55 + rnd.random() * 0.3))
        if c.get(x, y)[3] and hash2(x, y, seed) < 0.5:
            c.set(x, y, VE[4])
    return c


def fir(seed, w, h, tiers=6):
    """A fir: tiers of drooping boughs, dark and narrow. The one tree that is a triangle."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w // 2
    shade_pool(c, cx + 3, base, w * 0.34, 6)
    contact(c, cx, base + 1, 6)
    trunk(c, cx, base, base - h * 0.30, 7, 5, FL, seed, flare=2)
    top = 3
    bottom = base - h * 0.16
    # darker than a broadleaf: a fir is a dark tree, and in a wood of round crowns it is the one
    # read by its outline
    tones = [VE[2], VE[3], VE[4], OCC]
    for i in range(tiers):
        t0 = i / float(tiers)
        ty = top + (bottom - top) * (t0 ** 0.9)
        th = (bottom - top) / tiers * 1.55
        half = 4 + (w * 0.5 - 4) * ((i + 1) / float(tiers)) ** 0.85
        for y in range(int(ty), int(ty + th) + 1):
            u = max(0.0, (y - ty) / float(th))           # 0 at the tier's top, 1 at its hem
            hw = half * (0.25 + 0.75 * u ** 0.7)
            for x in range(int(cx - hw) - 1, int(cx + hw) + 2):
                dx = (x - cx) / max(1.0, hw)
                if abs(dx) > 1.0:
                    continue
                # the hem: drooping bough tips, a scalloped lower edge
                hem = 1.0 - 0.18 * abs(math.sin(dx * 5.5 + i)) - 0.1 * hash2(x, i, seed)
                if u > hem:
                    continue
                v = 0.62 - dx * 0.42 - u * 0.30 + (hash2(x, y, seed) - 0.5) * 0.12
                col = band(tones, v, x, y)
                if u > hem - 0.1:
                    col = VE[4] if hash2(x, y, seed + 1) < 0.6 else OCC
                # needle strokes: short diagonal glints on the lit side
                if dx < 0.1 and hash2(x, y, seed + 5) < 0.07:
                    col = VE[1] if v > 0.45 else VE[2]
                c.set(x, y, col)
    c.set(cx, top - 1, VE[2])
    c.set(cx, top - 2, VE[1])
    return c


def birch(seed, w, h):
    """A birch: a slender bone-white trunk with dark lenticels and a small airy crown."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w // 2 + rnd.randrange(-2, 3)
    c.anchor_x = cx
    shade_pool(c, cx + 2, base, w * 0.3, 5, 0.4)
    contact(c, cx, base + 1, 4)
    lean = (rnd.random() - 0.5) * 2.0
    crown_h = h * 0.5
    ccy = 2 + crown_h * 0.5
    trunk(c, cx, base, ccy - 2, 5, 3, SK, seed, flare=1.5, lean=lean, marks=OCC)
    for s in (-1, 1, 1):
        limb(c, cx, ccy + crown_h * 0.15, -math.pi / 2 + s * (0.6 + rnd.random() * 0.3),
             crown_h * 0.3, 2, SK, 1, rnd)
    clusters = fill_clusters(rnd, cx, ccy, w * 0.5 - 7, crown_h * 0.5 - 6, 22, 4, 8)
    crown(c, clusters, VE[1:5], cx, ccy, w * 0.5, crown_h * 0.5, seed, ragged=0.5, gaps=0.25, bias=0.06)
    return c


def willow(seed, w, h):
    """A swamp willow: a squat dark trunk under a dome, and a curtain of strands hanging nearly to
    the water. The strands are what make it a willow at a glance; the trunk shows between them."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w // 2
    shade_pool(c, cx + 4, base, w * 0.4, 8)
    contact(c, cx, base + 1, 9)
    dome_h = h * 0.42
    ccy = 4 + dome_h * 0.5
    trunk(c, cx, base, ccy, 13, 8, LN, seed, flare=5, lean=(rnd.random() - 0.5) * 3)
    clusters = fill_clusters(rnd, cx, ccy, w * 0.5 - 12, dome_h * 0.5 - 6, 26, 7, 12)
    crown(c, clusters, [VE[2], VE[3], VE[4], OCC], cx, ccy, w * 0.5, dome_h * 0.5, seed, bias=0.06)
    # the curtain: strands from the dome's underside, swaying, longer in the middle. Gaps between
    # them on purpose - a solid curtain is a green wall, and the trunk should show through.
    for k in range(int(w * 0.62)):
        sx = cx - w * 0.47 + rnd.random() * w * 0.94
        edge = abs(sx - cx) / (w * 0.5)
        sy = ccy + dome_h * (0.25 + 0.2 * (1 - edge)) + rnd.random() * 4
        ln = h * (0.22 + 0.30 * (1 - edge ** 2)) * (0.7 + rnd.random() * 0.5)
        ph = rnd.random() * TAU
        lit = sx < cx - w * 0.15
        for j in range(int(ln)):
            x = int(round(sx + math.sin(ph + j * 0.12) * 1.5 + j * 0.04))
            y = int(sy + j)
            if y >= base - 3:
                break
            col = VE[2] if (lit and j % 6 < 2) else (VE[3] if j % 4 else VE[4])
            if j > ln - 4:
                col = VE[4] if j % 2 else OCC
            c.set(x, y, col)
    return c


def snag(seed, w, h, bark, moss=None, hanging=None, depth=3):
    """A dead tree: a trunk and bare forking limbs, no leaves. `moss` patches the bark (swamp);
    `hanging` drops strands of moss from the limbs."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w // 2
    contact(c, cx, base + 1, 7)
    top = h * 0.40
    lean = (rnd.random() - 0.5) * 4
    trunk(c, cx, base, top, 10, 5, bark, seed, flare=4, lean=lean)
    tips = []
    tx = cx + lean * 0.1 * (base - top) * 0.1
    for k in range(3):
        a = -math.pi / 2 + (k - 1) * 0.55 + (rnd.random() - 0.5) * 0.3
        limb(c, tx, top + 4 + k * 3, a, h * 0.22, 4, bark, depth - 1, rnd, tips)
    for s in (-1, 1):
        limb(c, cx, base - h * (0.32 + rnd.random() * 0.12), -math.pi / 2 + s * 1.05, h * 0.16, 3, bark, 1, rnd, tips)
    if moss:
        for k in range(int(h * 1.2)):
            x = cx + int((rnd.random() - 0.5) * 12)
            y = int(top + rnd.random() * (base - top))
            p = c.get(x, y)
            if p[3] and hash2(x, y, seed) < 0.6:
                c.set(x, y, moss[0] if x < cx else moss[1])
    if hanging:
        # Spanish moss: ragged beards off the limbs, two pixels wide at the root and thinning, so
        # it reads as moss hanging and not as rain falling past the tree.
        for (px, py) in tips:
            if rnd.random() < 0.3:
                continue
            for strand in range(3):
                ln = rnd.randrange(6, 18) - strand * 3
                sx = int(px) + strand - 1
                for j in range(max(3, ln)):
                    col = hanging[0] if j < 2 else (hanging[1] if (j + strand) % 3 else hanging[2])
                    c.set(sx + (1 if j > ln * 0.6 and strand == 2 else 0), int(py) + j, col)
    return c


def gnarled(seed, w, h):
    """A ruin-grown tree: a twisted leaning trunk, one dead limb, and a sparse dark crown in two or
    three separate masses, as if half of it had given up."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w // 2 - 4
    c.anchor_x = cx
    shade_pool(c, cx + 8, base, w * 0.34, 6)
    contact(c, cx, base + 1, 8)
    top = h * 0.45
    lean = 2.0 + rnd.random() * 1.2
    trunk(c, cx, base, top, 12, 6, LN, seed, flare=5, lean=lean)
    tx = cx + lean * (base - top) * 0.1
    tips = []
    limb(c, tx, top + 6, -math.pi / 2 - 0.9, h * 0.25, 3, ST, 2, rnd)      # the dead limb
    clusters = []
    masses = [(tx + w * 0.12, h * 0.22, w * 0.26, h * 0.14), (tx - w * 0.16, h * 0.36, w * 0.18, h * 0.11)]
    if rnd.random() < 0.6:
        masses.append((tx + w * 0.28, h * 0.42, w * 0.13, h * 0.09))
    for (mx, my, mrx, mry) in masses:
        # inside the canvas, or the crown is cut off at its edge
        mx = max(mrx + 3, min(w - mrx - 3, mx))
        my = max(mry + 3, my)
        limb(c, tx, top + 4, math.atan2(my - top, mx - tx), math.hypot(mx - tx, my - top), 3, LN, 0, rnd)
        clusters += fill_clusters(rnd, mx, my, mrx * 0.8, mry * 0.8, int(mrx * 0.5), 5, 9)
    clusters.sort(key=lambda k: k[1])
    crown(c, clusters, [VE[2], VE[3], VE[4], OCC], tx, h * 0.3, w * 0.45, h * 0.25, seed, bias=0.08)
    return c


# --- bushes ---------------------------------------------------------------------------------------

def leafy_bush(seed, w, h, n, rmin, rmax, tones=None, berry=0):
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + 3, None)
    base = h - 1
    cx = w // 2
    c.disc(cx, base, w * 0.42, 2.2, OCC)
    clusters = []
    for k in range(n):
        a = math.pi + rnd.random() * math.pi                 # the upper half of an ellipse
        d = rnd.random() ** 0.6
        x = cx + math.cos(a) * d * (w * 0.5 - rmax)
        y = base - rmax * 0.6 + math.sin(a) * d * (h - rmax * 1.2)
        r = rmin + (rmax - rmin) * rnd.random()
        clusters.append((x, y, r, r * 0.8))
    clusters.sort(key=lambda k: k[1])
    crown(c, clusters, tones or VE[1:5], cx, base - h * 0.5, w * 0.5, h * 0.5, seed, ragged=0.4)
    if berry:
        berries(c, clusters, rnd, berry)
    return c


def twig_bush(seed, w, h, wood, tufts=None):
    """A dead bush or desert scrub: a fan of bare twigs from one root, thinning to their tips."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + 3, None)
    base = h - 1
    cx = w // 2
    c.disc(cx, base, w * 0.36, 1.8, OCC)
    for k in range(9):
        a = -math.pi / 2 + (k - 4) * 0.30 + (rnd.random() - 0.5) * 0.25
        limb(c, cx + (k - 4) * 0.6, base, a, h * (0.55 + rnd.random() * 0.35), 2, wood, 2, rnd)
    if tufts:
        for k in range(6):
            x = cx + rnd.randrange(-w // 3, w // 3)
            y = base - rnd.randrange(h // 3, h - 2)
            c.set(x, y, tufts[0])
            c.set(x + 1, y, tufts[1])
    return c


def reed_tuft(seed, w, h):
    """A swamp tussock: sedge blades and two or three brown cattails."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + 3, None)
    base = h - 1
    cx = w // 2
    c.disc(cx, base, w * 0.4, 2, OCC)
    for k in range(14):
        x0 = cx + rnd.randrange(-w // 3, w // 3 + 1)
        ln = h * (0.45 + rnd.random() * 0.5)
        lean = (x0 - cx) * 0.06 + (rnd.random() - 0.5) * 0.6
        for j in range(int(ln)):
            t = j / ln
            x = int(round(x0 + lean * j * t))
            col = VE[2] if j % 4 == 0 else (VE[3] if x < cx else VE[4])
            if t > 0.8:
                col = LN[2]
            c.set(x, base - j, col)
    for k in range(rnd.randrange(2, 4)):
        x = cx + rnd.randrange(-w // 4, w // 4 + 1)
        top = base - h + 2 + rnd.randrange(0, 4)
        c.vline(x, top + 6, base - 2, LN[3])
        for j in range(5):
            c.set(x, top + j, FL[3] if j else FL[2])
            c.set(x + 1, top + j, FL[4])
    return c


def bramble(seed, w, h):
    """A ruin bramble: dark leaves on arching thorned canes, with a few dull red hips."""
    rnd = random.Random(seed)
    c = leafy_bush(seed, w, h, 9, 4, 7, tones=[VE[2], VE[3], VE[4], OCC])
    base = h - 1
    cx = w // 2
    for k in range(5):
        x0 = cx + rnd.randrange(-w // 3, w // 3 + 1)
        a = -math.pi / 2 + (rnd.random() - 0.5) * 1.6
        px, py = float(x0), float(base - 1)
        for j in range(int(h * 0.9)):
            a += 0.06 if a < -math.pi / 2 else -0.06
            px += math.cos(a)
            py += math.sin(a) + j * 0.012
            c.set(int(px), int(py), FL[3])
            if j % 4 == 2:
                c.set(int(px) + 1, int(py) - 1, FL[4])        # a thorn
    rr = random.Random(seed + 1)
    for k in range(3):
        x = cx + rr.randrange(-w // 3, w // 3)
        y = base - rr.randrange(4, h - 3)
        c.set(x, y, RD[1])
        c.set(x + 1, y, RD[2])
        c.set(x, y + 1, RD[3])
    return c


# --- the set --------------------------------------------------------------------------------------
# (id, kind, painter, trunk footprint in WORLD px or 0 for none)
#
# Biome is not here: Node2DGame names the ids each stage scatters, so this file says what the
# sprites are and the stage palette says where they grow.

SPRITES = [
    # Enchanted Forest
    ("oak-large-1", "tree", lambda: broadleaf(11, 120, 148, trunk_w=12, n=46, rmin=8, rmax=15), 28),
    ("oak-large-2", "tree", lambda: broadleaf(23, 116, 142, trunk_w=12, lean=2, n=44, rmin=8, rmax=15), 28),
    ("oak-medium-1", "tree", lambda: broadleaf(37, 92, 118, trunk_w=9, n=32, rmin=6, rmax=12), 22),
    ("oak-medium-2", "tree", lambda: broadleaf(41, 88, 112, trunk_w=9, lean=-2, n=30, rmin=6, rmax=12), 22),
    ("fir-1", "tree", lambda: fir(53, 64, 144, tiers=7), 18),
    ("fir-2", "tree", lambda: fir(59, 58, 124, tiers=6), 16),
    ("birch-1", "tree", lambda: birch(61, 52, 108), 12),
    ("birch-2", "tree", lambda: birch(67, 48, 92), 12),
    ("sapling-1", "tree", lambda: broadleaf(71, 52, 78, crown_frac=0.6, trunk_w=5, n=16, rmin=4, rmax=8), 12),
    ("bush-round-1", "bush", lambda: leafy_bush(81, 34, 22, 9, 5, 9), 0),
    ("bush-round-2", "bush", lambda: leafy_bush(83, 26, 16, 6, 4, 7), 0),
    ("bush-wide-1", "bush", lambda: leafy_bush(85, 46, 26, 13, 5, 10), 0),
    ("bush-berry-1", "bush", lambda: leafy_bush(87, 32, 20, 9, 4, 8, berry=6), 0),
    ("bush-berry-2", "bush", lambda: leafy_bush(89, 40, 24, 11, 5, 9, berry=8), 0),
    # Swamp
    ("willow-1", "tree", lambda: willow(101, 118, 130), 28),
    ("willow-2", "tree", lambda: willow(107, 104, 116), 26),
    ("swamp-snag-1", "tree", lambda: snag(113, 76, 120, LN, moss=(VE[3], VE[4]), hanging=(LI[1], LI[2], LI[3])), 20),
    ("swamp-snag-2", "tree", lambda: snag(117, 68, 100, LN, moss=(VE[3], VE[4]), hanging=(LI[1], LI[2], LI[3])), 18),
    ("reed-tuft-1", "bush", lambda: reed_tuft(121, 26, 30), 0),
    ("reed-tuft-2", "bush", lambda: reed_tuft(125, 22, 24), 0),
    ("swamp-shrub-1", "bush", lambda: leafy_bush(127, 36, 20, 10, 4, 8, tones=[VE[2], VE[3], VE[4], OCC]), 0),
    # Ruins and Cave
    ("gnarled-1", "tree", lambda: gnarled(131, 100, 116), 24),
    ("gnarled-2", "tree", lambda: gnarled(137, 92, 104), 22),
    ("dead-tree-1", "tree", lambda: snag(139, 80, 118, ASH, depth=4), 20),
    ("bramble-1", "bush", lambda: bramble(141, 34, 20), 0),
    ("bramble-2", "bush", lambda: bramble(143, 28, 16), 0),
    # Scorched Sands
    ("bleached-tree-1", "tree", lambda: snag(151, 84, 112, BLEACHED, depth=4), 18),
    ("bleached-tree-2", "tree", lambda: snag(157, 72, 94, BLEACHED, depth=3), 16),
    ("dead-bush-1", "bush", lambda: twig_bush(161, 40, 28, FL), 0),
    ("dead-bush-2", "bush", lambda: twig_bush(163, 32, 22, FL), 0),
    ("dry-scrub-1", "bush", lambda: twig_bush(167, 44, 22, GO, tufts=(LN[1], LN[2])), 0),
]


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True
    rows = []
    for (sid, kind, draw, footprint) in SPRITES:
        c = draw()
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s: %s" % (sid, bad[:4]))
            continue
        foot = FOOT if kind == "tree" else 3
        ground = (c.h - foot) * SCALE              # the contact line, in pixels of the saved PNG
        # The trunk's column, in pixels of the PNG: Node2DGame stands this column on the scattered
        # point (and mirrors it when it flips the sprite), so the collision is under the trunk.
        anchor = getattr(c, "anchor_x", c.w // 2) * SCALE + 1
        c.scaled(SCALE).save(os.path.join(OUT, sid + ".png"))
        rows.append('    {"id": "%s", "kind": "%s", "path": "res://assets/bonelight/world/trees/%s.png", '
                    '"width": %d, "height": %d, "ground": %d, "anchor_x": %d, "trunk": %d}'
                    % (sid, kind, sid, c.w * SCALE, c.h * SCALE, ground, anchor, footprint))
        print("  %-16s %-4s %3dx%-3d on screen%s" % (sid, kind, c.w * SCALE, c.h * SCALE,
                                                  ", trunk %d px" % footprint if footprint else ""))
    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/trees.py. ground is the contact line and '
                'anchor_x the trunk column, in pixels of the PNG; trunk is the width in world pixels of the only part that blocks (0: walk '
                'through it). Node2DGame anchors, sorts and collides each sprite at its ground line.",\n'
                '  "sprites": [\n%s\n  ]\n}\n' % ",\n".join(rows))
    io.open(os.path.join(OUT, "trees.json"), "w", encoding="utf-8", newline="\n").write(manifest.decode("utf-8"))
    print("%d trees and bushes%s" % (len(rows), "" if ok else " - WITH PALETTE ERRORS"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
