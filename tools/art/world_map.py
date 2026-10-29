# -*- coding: utf-8 -*-
"""The world map: the drawn world the wizard walks between chapters. Run: python tools/art/world_map.py

Writes, under assets/bonelight/ui/worldmap/:

    world.png         the world, 1680x840 at 1:1 (the screen draws it at a whole number of screen
                      pixels per art pixel, x2 on a 1:1 canvas - .ai/art-direction.md section 6a)
    sketchbook.png    the page that lies off the world's east edge, and the pencil line to it
    shroud-<stop>.png the bank of his fog laid over a locked place, one per stop, sized to it
    shadow.png        the wizard's contact shadow on the map
    ring-open.png, ring-locked.png, ring-current.png   the mark under each stop's stand point
    mark-*.png        the compass strip's marks (16x16 art, saved x2 like every UI icon)
    secret.png, keepsake.png   the card's icons for a stage secret and a keepsake (32x32, saved x2)
    arrow-left.png, arrow-right.png   the step plates' chevrons (saved x2)
    worldmap.json     the stops, their stand points, focus points and shrouds, and the route

The design is .ai/world-map.md. The geometry the screen uses comes from worldmap.json and nowhere
else, so moving a place is a line in PLACES or WAYPOINTS below and a re-run.

THE KIDS' ART IS NEVER OPENED HERE. The Sketchbook's page is drawn by this file; the drawings on it
are theirs, laid on the page at runtime from their own files (WorldMapView), untouched. The page's
JSON names those files; this generator does not read a pixel of them.

Python 2.7 and 3 both. Every random choice comes from rng.random() on a seeded Random, so a re-run
writes the same bytes.
"""

from __future__ import division, print_function

import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import raster

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "worldmap")
PREVIEW_DIR = os.path.join(ROOT, "tools", "art", "_worldmap")

W, H = 1680, 840
OCC = bl.OCC
E = bl.ELEMENTS
DITHER = raster.BAYER8


def T(mat, role):
    return bl.tone(mat, role)


# ---------------------------------------------------------------------------------------------
# THE PLACES. Each stop: its id (a StageCatalog id, or boss_rush, or the Sketchbook's stage_8),
# the stand point on the road where the wizard waits (his feet), the focus the camera frames (the
# landmark's middle), and the radii of the fog laid over it while it is locked. West to east in
# stop order; .ai/world-map.md section 1 says why each is where it is.
# ---------------------------------------------------------------------------------------------
PLACES = [
    # id,          stand,        focus,        shroud rx, ry
    ("stage_0",  (204, 486), (150, 424), 96, 72),   # Enchanted Forest - the west wood
    ("stage_9",  (370, 574), (362, 646), 92, 56),   # The Fallow Fields - south of the road
    ("stage_10", (522, 364), (520, 298), 74, 52),   # The Broken Circle - up on the heath
    ("stage_2",  (664, 532), (690, 466), 62, 50),   # Sunken Cave - under the scarp
    ("stage_1",  (800, 642), (834, 574), 66, 60),   # Cursed Dungeon - the keep on the crag
    ("stage_3",  (976, 600), (1004, 670), 88, 56),  # Blighted Swamp - the delta bog
    ("stage_4",  (1060, 428), (1066, 360), 78, 56), # Mystic Ruins - the plateau
    ("stage_5",  (1200, 218), (1206, 150), 92, 60), # Frozen Waste - under the northern peaks
    ("stage_6",  (1340, 542), (1378, 626), 90, 58), # Scorched Sands - past the pass
    ("stage_7",  (1470, 532), (1508, 470), 70, 56), # The Emberdeep - the black gate
    ("boss_rush", (1572, 350), (1576, 300), 46, 30),# the seat, at the summit
    ("stage_8",  (1850, 474), (1850, 420), 0, 0),   # The Sketchbook - off the world, on the page
]

# The road between the stand points, in order. Each list runs from one stand point to the next
# (exclusive of both); the spline goes through every point.
WAYPOINTS = [
    [(258, 518), (312, 548)],                               # forest -> fields
    [(424, 540), (452, 474), (484, 410)],                   # fields -> up onto the heath
    [(566, 402), (602, 460), (628, 506)],                   # heath -> down the scarp to the cave
    [(704, 562), (744, 604), (772, 632)],                   # cave -> the keep
    [(852, 632), (900, 612), (938, 602)],                   # keep -> over the bridge to the bog
    [(1000, 560), (1022, 508), (1042, 462)],                # bog -> up onto the plateau
    [(1090, 372), (1122, 318), (1152, 270), (1176, 240)],   # plateau -> north into the ice
    [(1230, 262), (1250, 330), (1268, 400), (1298, 466), (1320, 512)],  # ice -> the pass -> sands
    [(1392, 548), (1432, 544)],                             # sands -> the gate
    [(1496, 520), (1510, 494), (1515, 462), (1540, 434), (1522, 408), (1550, 386), (1564, 366)],  # through the gate -> the summit
    [(1600, 384), (1632, 410), (1660, 432), (1680, 444), (1720, 456), (1780, 466)],  # down, off the edge
]

# The page's place in map space: its image is drawn with its top-left here. It starts at the
# world's east edge, so the pencil line can leave the world from the road's last point.
PAGE_ORIGIN = (1680, 300)
PAGE_SIZE = (290, 300)
# The page itself inside that image.
PAGE_RECT = (96, 40, 280, 268)
# The kids' drawings laid on the page at runtime, unmodified, from their own files.
PAGE_DRAWINGS = [
    # file, top-left in MAP space
    ("res://assets/kidsart/doodle-sun.png", (1858, 348)),
    ("res://assets/kidsart/kid-wizard-moving.png", (1796, 392)),
]


def rng_for(seed):
    return random.Random(seed)


# ---------------------------------------------------------------------------------------------
# A FAST GRID. raster.Canvas converts a hex string on every write, and the world is 1.4 million
# pixels written several times over; this caches the conversion and writes the image once.
# ---------------------------------------------------------------------------------------------
_RGB = {}


def rgba(c):
    if c is None:
        return (0, 0, 0, 0)
    if isinstance(c, tuple):
        return c
    v = _RGB.get(c)
    if v is None:
        h = c.lstrip("#")
        v = (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)
        _RGB[c] = v
    return v


def lum(c):
    c = rgba(c)
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


class Grid(object):
    def __init__(self, w, h, fill=None):
        self.w = w
        self.h = h
        self.pix = [rgba(fill)] * (w * h)

    def set(self, x, y, c):
        x = int(x)
        y = int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            self.pix[y * self.w + x] = rgba(c)

    def get(self, x, y):
        x = int(x)
        y = int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.pix[y * self.w + x]
        return (0, 0, 0, 0)

    def image(self):
        img = Image.new("RGBA", (self.w, self.h))
        img.putdata(self.pix)
        return img

    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                self.set(x, y, c)

    def disc(self, cx, cy, rx, ry, c):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                nx = (x - cx) / float(max(rx, 0.5))
                ny = (y - cy) / float(max(ry, 0.5))
                if nx * nx + ny * ny <= 1.0:
                    self.set(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
        dx = abs(x1 - x0)
        dy = -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        while True:
            self.set(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def poly(self, pts, c):
        ys = [p[1] for p in pts]
        n = len(pts)
        for y in range(int(min(ys)), int(max(ys)) + 1):
            xs = []
            for i in range(n):
                ax, ay = pts[i]
                bx, by = pts[(i + 1) % n]
                if ay == by:
                    continue
                if min(ay, by) <= y < max(ay, by):
                    xs.append(ax + (y - ay) * (bx - ax) / float(by - ay))
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                for x in range(int(math.floor(xs[i])), int(math.ceil(xs[i + 1])) + 1):
                    self.set(x, y, c)


def dith(x, y, f):
    """True where an ordered dither of strength f (0..1) puts the second tone."""
    return f > DITHER[int(y) % 8][int(x) % 8] / 64.0


# ---------------------------------------------------------------------------------------------
# NOISE. Value noise on a seeded lattice, summed over octaves.
# ---------------------------------------------------------------------------------------------
class Noise(object):
    def __init__(self, seed, cell):
        r = rng_for(seed)
        self.cell = float(cell)
        self.n = 97
        self.v = [r.random() for _ in range(self.n * self.n)]

    def at(self, x, y):
        fx = x / self.cell
        fy = y / self.cell
        ix = int(math.floor(fx))
        iy = int(math.floor(fy))
        tx = fx - ix
        ty = fy - iy
        tx = tx * tx * (3 - 2 * tx)
        ty = ty * ty * (3 - 2 * ty)
        n = self.n
        a = self.v[(iy % n) * n + (ix % n)]
        b = self.v[(iy % n) * n + ((ix + 1) % n)]
        c = self.v[((iy + 1) % n) * n + (ix % n)]
        d = self.v[((iy + 1) % n) * n + ((ix + 1) % n)]
        return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty


class Fbm(object):
    def __init__(self, seed, cell, octaves=3):
        self.layers = [Noise(seed + i * 101, cell / (2 ** i)) for i in range(octaves)]

    def at(self, x, y):
        total = 0.0
        amp = 1.0
        norm = 0.0
        for layer in self.layers:
            total += layer.at(x, y) * amp
            norm += amp
            amp *= 0.5
        return total / norm


class Sampled(object):
    """A smooth field sampled on a coarse grid and read back bilinearly: the broad noises vary over
    tens of pixels, so evaluating every octave at every pixel is wasted work."""

    def __init__(self, field, step, w, h):
        self.step = step
        self.cols = w // step + 2
        self.rows = h // step + 2
        self.v = [field.at(c * step, r * step) for r in range(self.rows) for c in range(self.cols)]

    def at(self, x, y):
        fx = x / float(self.step)
        fy = y / float(self.step)
        ix = min(self.cols - 2, max(0, int(fx)))
        iy = min(self.rows - 2, max(0, int(fy)))
        tx = fx - ix
        ty = fy - iy
        c = self.cols
        a = self.v[iy * c + ix]
        b = self.v[iy * c + ix + 1]
        d = self.v[(iy + 1) * c + ix]
        e = self.v[(iy + 1) * c + ix + 1]
        top = a + (b - a) * tx
        return top + ((d + (e - d) * tx) - top) * ty


WARP_X = Fbm(11, 90)
WARP_Y = Fbm(23, 90)
GRAIN = Fbm(37, 14, 2)
BROAD = Sampled(Fbm(41, 60), 6, 1680, 840)

# ---------------------------------------------------------------------------------------------
# THE ROUTE. A Catmull-Rom spline through every stand point and waypoint, sampled about every two
# pixels. The screen walks the wizard along these points by arc length.
# ---------------------------------------------------------------------------------------------


def catmull(p0, p1, p2, p3, t):
    t2 = t * t
    t3 = t2 * t
    return (
        0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2
               + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3),
        0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2
               + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3),
    )


def build_route():
    """(points, stop_index): the sampled road, and the sample index of every stop's stand point."""
    control = []
    stop_at = {}
    for i, place in enumerate(PLACES):
        stop_at[place[0]] = len(control)
        control.append(place[1])
        if i < len(WAYPOINTS):
            control.extend(WAYPOINTS[i])
    points = []
    stop_index = {}
    marks = dict((v, k) for k, v in stop_at.items())
    for i in range(len(control) - 1):
        p0 = control[max(0, i - 1)]
        p1 = control[i]
        p2 = control[i + 1]
        p3 = control[min(len(control) - 1, i + 2)]
        if i in marks:
            stop_index[marks[i]] = len(points)
        seg = math.hypot(p2[0] - p1[0], p2[1] - p1[1])
        steps = max(2, int(seg / 2.0))
        for s in range(steps):
            points.append(catmull(p0, p1, p2, p3, s / float(steps)))
    stop_index[marks[len(control) - 1]] = len(points)
    points.append(control[-1])
    return points, stop_index


ROUTE, ROUTE_STOPS = build_route()


def near_route(x, y, radius, upto_x=None):
    """True if (x, y) is within radius of the road. Coarse: every fourth sample."""
    r2 = radius * radius
    for i in range(0, len(ROUTE), 2):
        px, py = ROUTE[i]
        if upto_x is not None and px > upto_x:
            continue
        dx = px - x
        dy = py - y
        if dx * dx + dy * dy <= r2:
            return True
    return False


# A coarse occupancy of the road, so per-pixel tests do not walk the whole route.
ROAD_CELL = 16
_road_cells = {}
for _i, (_x, _y) in enumerate(ROUTE):
    _road_cells.setdefault((int(_x) // ROAD_CELL, int(_y) // ROAD_CELL), []).append((_x, _y))


def road_distance(x, y):
    best = 1e9
    cx, cy = int(x) // ROAD_CELL, int(y) // ROAD_CELL
    for gy in range(cy - 2, cy + 3):
        for gx in range(cx - 2, cx + 3):
            for (px, py) in _road_cells.get((gx, gy), ()):
                d = (px - x) * (px - x) + (py - y) * (py - y)
                if d < best:
                    best = d
    return math.sqrt(best)


# ---------------------------------------------------------------------------------------------
# THE LAND. Each pixel belongs to the nearest biome seed, measured through a warp so borders
# wander. Sea is the south and east coast, by its own line.
# ---------------------------------------------------------------------------------------------
SEEDS = [
    ("forest", [(40, 300), (110, 460), (60, 640), (190, 360), (230, 270), (50, 150), (160, 190),
                (150, 600), (240, 430), (90, 770), (220, 700), (290, 200)]),
    ("fields", [(330, 650), (410, 660), (300, 720), (392, 600), (450, 720), (300, 590)]),
    ("heath", [(470, 290), (560, 320), (520, 230), (600, 370), (430, 380), (480, 470), (380, 300),
               (420, 200)]),
    ("hills", [(690, 420), (730, 520), (640, 570), (760, 450), (700, 330), (620, 470), (560, 520),
               (500, 560), (700, 230), (850, 290), (950, 240), (790, 370), (900, 410), (600, 180),
               (1010, 150)]),
    ("crag", [(836, 572), (872, 618)]),
    ("lowland", [(905, 560), (760, 700), (880, 700), (930, 480), (820, 470), (980, 520)]),
    ("swamp", [(1000, 676), (1068, 700), (940, 700), (1110, 650), (1000, 740)]),
    ("plateau", [(1066, 360), (1000, 420), (1130, 430), (990, 320), (1150, 380)]),
    ("snow", [(1206, 150), (1150, 210), (1290, 190), (1100, 130), (1240, 250), (1330, 120)]),
    ("rock", [(820, 70), (640, 60), (1000, 80), (440, 60), (320, 60), (1400, 90), (1200, 50),
              (1220, 360), (1300, 390), (1400, 350), (1460, 240), (1160, 300)]),
    ("desert", [(1372, 610), (1300, 590), (1420, 700), (1300, 700), (1240, 640), (1440, 560)]),
    ("basalt", [(1520, 450), (1580, 350), (1620, 550), (1500, 620), (1620, 250), (1560, 700),
                (1660, 400)]),
]

SEED_LIST = []
for _name, _pts in SEEDS:
    for _p in _pts:
        SEED_LIST.append((_p[0], _p[1], _name))


def coast_y(x):
    """The south coast: sea below this line. The forest runs to the bottom edge in the west."""
    if x < 250:
        return 9999
    t = min(1.0, (x - 250) / 120.0)
    base = 860 - t * 120
    return base + (WARP_X.at(x * 2, 9000) - 0.5) * 40


def is_sea(x, y):
    if y > coast_y(x):
        return True
    # The east: below the Emberdeep's cliffs.
    edge = 1650 + (WARP_Y.at(9000, y * 2) - 0.5) * 30
    if x > edge and y > 470:
        return True
    return False


def biome_pair(x, y):
    """(nearest biome, the next nearest OTHER biome, how far toward it 0..0.5): the second lets a
    border be a dithered band rather than a ruled line between two territories."""
    wx = x + (WARP_X.at(x, y) - 0.5) * 70
    wy = y + (WARP_Y.at(x, y) - 0.5) * 70
    best = None
    bd = 1e18
    near = {}
    for sx, sy, name in SEED_LIST:
        d = (sx - wx) * (sx - wx) + (sy - wy) * (sy - wy)
        if d < near.get(name, 1e18):
            near[name] = d
        if d < bd:
            bd = d
            best = name
    other = None
    od = 1e18
    for name, d in near.items():
        if name != best and d < od:
            od = d
            other = name
    gap = math.sqrt(od) - math.sqrt(bd)
    band = 14.0
    mix = max(0.0, 0.5 - gap / (2 * band))
    return best, other, mix


def biome_at(x, y):
    if is_sea(x, y):
        return "sea"
    return biome_pair(x, y)[0]


# ---------------------------------------------------------------------------------------------
# GROUND. Every biome has a ramp of four of its own tones, dark to light, and a pixel's place on it
# is the LIGHT on the land there: a height field, hill-shaded from the key's direction (upper left).
# Without it the regions were flat patches of colour - a map of territories, not a picture of land.
# Everything sits in the deep half of each material; the places light themselves.
# ---------------------------------------------------------------------------------------------
GROUND = {
    "forest": [OCC, T("stone", "deep"), T("lichen", "deep"), T("lichen", "shade")],
    "fields": [T("stone", "deep"), T("flesh", "deep"), T("gold", "deep"), T("gold", "shade")],
    "heath": [T("stone", "deep"), T("violet", "deep"), T("violet", "shade"), T("violet", "base")],
    "hills": [T("stone", "deep"), T("stone", "shade"), T("lichen", "deep"), T("lichen", "shade")],
    "crag": [T("stone", "deep"), T("stone", "shade"), T("stone", "base"), T("stone", "lit")],
    "lowland": [T("stone", "deep"), T("lichen", "deep"), T("linen", "deep"), T("linen", "shade")],
    "swamp": [T("stone", "deep"), T("linen", "deep"), T("linen", "shade"), T("linen", "base")],
    "plateau": [T("stone", "deep"), T("stone", "shade"), T("stone", "base"), T("skin", "deep")],
    "snow": [T("stone", "shade"), T("steel", "shade"), T("steel", "base"), T("skin", "shade")],
    "rock": [T("stone", "deep"), T("stone", "shade"), T("stone", "base"), T("stone", "lit")],
    "desert": [T("flesh", "deep"), T("gold", "deep"), T("gold", "shade"), T("gold", "base")],
    "basalt": [OCC, T("stone", "deep"), T("violet", "deep"), T("violet", "shade")],
    "sea": [T("stone", "deep"), T("wool", "deep"), T("wool", "deep"), T("wool", "shade")],
}

# base height, relief amplitude, and where on its ramp flat ground of the biome sits (0..1)
RELIEF = {
    "sea": (0.00, 0.00, 0.30),
    "swamp": (0.10, 0.03, 0.40),
    "lowland": (0.20, 0.10, 0.50),
    "fields": (0.22, 0.05, 0.50),
    "forest": (0.30, 0.14, 0.45),
    "desert": (0.26, 0.10, 0.42),
    "hills": (0.42, 0.34, 0.50),
    "heath": (0.55, 0.20, 0.50),
    "crag": (0.60, 0.25, 0.45),
    "plateau": (0.62, 0.06, 0.48),
    "basalt": (0.40, 0.18, 0.40),
    "snow": (0.72, 0.16, 0.55),
    "rock": (0.85, 0.40, 0.42),
}

HILL = Fbm(53, 46, 3)
RELIEF_STEP = 4


def height_field(biomes):
    """Heights on a 4-pixel grid, blurred so a biome border is a slope rather than a step."""
    cols = W // RELIEF_STEP + 2
    rows = H // RELIEF_STEP + 2
    hgt = [0.0] * (cols * rows)
    for r in range(rows):
        for c in range(cols):
            x = min(W - 1, c * RELIEF_STEP)
            y = min(H - 1, r * RELIEF_STEP)
            base, amp, _ = RELIEF[biomes[y * W + x]]
            hgt[r * cols + c] = base + (HILL.at(x, y) - 0.5) * 2 * amp
    for _ in range(3):
        out = list(hgt)
        for r in range(1, rows - 1):
            for c in range(1, cols - 1):
                acc = 0.0
                for dr in (-1, 0, 1):
                    for dc in (-1, 0, 1):
                        acc += hgt[(r + dr) * cols + c + dc]
                out[r * cols + c] = acc / 9.0
        hgt = out
    return hgt, cols, rows


def sample(hgt, cols, rows, x, y):
    fx = x / float(RELIEF_STEP)
    fy = y / float(RELIEF_STEP)
    ix = min(cols - 2, max(0, int(fx)))
    iy = min(rows - 2, max(0, int(fy)))
    tx = fx - ix
    ty = fy - iy
    a = hgt[iy * cols + ix]
    b = hgt[iy * cols + ix + 1]
    c = hgt[(iy + 1) * cols + ix]
    d = hgt[(iy + 1) * cols + ix + 1]
    top = a + (b - a) * tx
    return top + ((c + (d - c) * tx) - top) * ty


def paint_ground(g, biomes, others, mixes):
    hgt, cols, rows = height_field(biomes)
    for y in range(H):
        row = y * W
        for x in range(W):
            b = biomes[row + x]
            # Near a border, some pixels take the neighbour's ramp: a dithered band, not a line.
            if mixes[row + x] > 0 and others[row + x] is not None and dith(x + 3, y + 5, mixes[row + x]):
                b = others[row + x]
            tones = GROUND[b]
            flat = RELIEF[b][2]
            if b == "sea":
                light = flat + (BROAD.at(x, y) - 0.5) * 0.3
            else:
                # The key comes from the upper left: ground that faces it is lit.
                slope = sample(hgt, cols, rows, x - 3, y - 3) - sample(hgt, cols, rows, x + 3, y + 3)
                light = flat + slope * 5.0 + (BROAD.at(x, y) - 0.5) * 0.25 + (GRAIN.at(x, y) - 0.5) * 0.22
            t = max(0.0, min(2.999, light * 3.0))
            i = int(t)
            c = tones[i + 1] if dith(x, y, t - i) else tones[i]
            if b == "sea" and GRAIN.at(x, y) > 0.68 and (x + int(y * 1.7)) % 9 == 0:
                c = tones[3]
            g.pix[row + x] = rgba(c)


def shore(g, biomes):
    """A dark line where land meets sea, and a paler lip on the land side lit from above."""
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if biomes[y * W + x] != "sea":
                continue
            up = biomes[(y - 1) * W + x]
            left = biomes[y * W + x - 1]
            if up != "sea" or left != "sea":
                g.set(x, y, OCC)
                if up != "sea":
                    g.set(x, y - 1, T("stone", "base") if up not in ("snow",) else T("skin", "base"))


# ---------------------------------------------------------------------------------------------
# WATER: the heath's stream into the cave, the river out of the hills past the keep into the bog.
# ---------------------------------------------------------------------------------------------
STREAM = [(575, 270), (600, 292), (628, 330), (650, 372), (668, 410), (682, 436), (689, 452)]
# Out of the hills below the scarp - the cave's water coming back up - west of the keep's crag,
# under the road's one bridge, and out across the bog to the sea.
RIVER = [(758, 470), (766, 510), (772, 552), (774, 592), (778, 628), (790, 662), (826, 684),
         (880, 692), (930, 700), (968, 712), (996, 734), (1012, 772), (1020, 840)]
DRY_RIVER = [(1250, 520), (1290, 560), (1330, 600), (1350, 650), (1336, 700), (1350, 760)]
WATER = T("wool", "deep")
WATER_LIT = T("wool", "shade")


def spline(pts, step=1.0):
    out = []
    for i in range(len(pts) - 1):
        p0 = pts[max(0, i - 1)]
        p1 = pts[i]
        p2 = pts[i + 1]
        p3 = pts[min(len(pts) - 1, i + 2)]
        seg = math.hypot(p2[0] - p1[0], p2[1] - p1[1])
        n = max(2, int(seg / step))
        for s in range(n):
            out.append(catmull(p0, p1, p2, p3, s / float(n)))
    out.append(pts[-1])
    return out


def draw_water_path(g, pts, w0, w1, water=WATER, lit=WATER_LIT, bank=OCC):
    path = spline(pts)
    n = len(path)
    # Banks first, then water over them, so the bank is a rim and never inside the stream.
    for pass_ in (0, 1):
        for i, (x, y) in enumerate(path):
            w = w0 + (w1 - w0) * i / float(max(1, n - 1))
            r = w / 2.0 + (1.0 if pass_ == 0 else 0.0)
            for yy in range(int(y - r) - 1, int(y + r) + 2):
                for xx in range(int(x - r) - 1, int(x + r) + 2):
                    if (xx - x) ** 2 + (yy - y) ** 2 <= r * r:
                        if pass_ == 0:
                            g.set(xx, yy + 1, bank)
                        else:
                            c = lit if ((xx * 3 + yy * 5 + i) % 11 == 0) else water
                            g.set(xx, yy, c)
    return path


def lake(g, cx, cy, rx, ry, seed):
    r = rng_for(seed)
    g.disc(cx + 1, cy + 2, rx + 1, ry + 1, OCC)
    g.disc(cx, cy, rx, ry, WATER)
    for _ in range(int(rx * ry / 12) + 2):
        x = cx + (r.random() - 0.5) * rx * 1.4
        y = cy + (r.random() - 0.5) * ry * 1.2
        g.set(x, y, WATER_LIT)
        g.set(x + 1, y, WATER_LIT)


# ---------------------------------------------------------------------------------------------
# THE ROAD. Drawn after the land and the water, before anything that stands on it.
# ---------------------------------------------------------------------------------------------
def draw_road(g, biomes):
    on_map = [p for p in ROUTE if p[0] < W - 2]
    for (x, y) in on_map:
        for yy in range(int(y) - 3, int(y) + 4):
            for xx in range(int(x) - 3, int(x) + 4):
                d = math.hypot(xx - x, yy - y)
                if d > 2.6:
                    continue
                cur = g.get(xx, yy)
                b = biomes[min(H - 1, max(0, yy)) * W + min(W - 1, max(0, xx))]
                if cur == rgba(WATER) or cur == rgba(WATER_LIT):
                    # A bridge: planks across, lit on their top.
                    g.set(xx, yy, T("gold", "shade") if (xx + yy) % 3 else T("gold", "deep"))
                    continue
                bright = b in ("snow",)
                # On snow the road is the trodden dark of it, not a pale line on a pale field.
                core = T("stone", "base") if bright else T("linen", "base")
                edge = T("steel", "shade") if bright else T("linen", "shade")
                if d <= 0.7 and not bright and dith(xx, yy, 0.45):
                    g.set(xx, yy, T("linen", "lit"))
                elif d <= 1.5:
                    g.set(xx, yy, core)
                else:
                    g.set(xx, yy, edge)
    # The lit lip on the road's upper-left side and its shadow on the lower-right, after the fill.
    road_tones = set(rgba(c) for c in (T("linen", "lit"), T("linen", "base"), T("linen", "shade"),
                                       T("steel", "shade"), T("stone", "base"), T("gold", "shade"),
                                       T("gold", "deep")))
    for (x, y) in on_map:
        sx, sy = int(round(x + 1.8)), int(round(y + 3.0))
        cur = g.get(sx, sy)
        if cur not in road_tones and lum(cur) > lum(T("stone", "deep")):
            g.set(sx, sy, T("stone", "deep"))
    # Dust in the ruts: a lit fleck now and then, never a dashed line.
    r = rng_for(77)
    for (x, y) in on_map[::3]:
        if r.random() < 0.18:
            c = g.get(x, y)
            if c == rgba(T("linen", "base")):
                g.set(x, y, T("linen", "lit"))


# ---------------------------------------------------------------------------------------------
# SMALL FURNITURE: trees, bushes, stones. Lit from the upper left, contact shadow lower right.
# ---------------------------------------------------------------------------------------------
_LOBE_RNG = rng_for(4242)


def crown(g, cx, cy, r, mat="lichen", dark=False):
    """A tree from above and a little south: a crown of clumped lobes, each lit on its own upper
    left, so a wood reads as foliage. One smooth disc per tree read as a ball pit."""
    ramp = bl.MATERIALS[mat]
    rnd = _LOBE_RNG
    lobes = [(cx, cy, r * 0.62)]
    n = 3 + int(rnd.random() * 3)
    for k in range(n):
        a = rnd.random() * 2 * math.pi
        d = r * (0.35 + rnd.random() * 0.25)
        lobes.append((cx + math.cos(a) * d, cy + math.sin(a) * d * 0.85, r * (0.4 + rnd.random() * 0.22)))
    for (lx, ly, lr) in lobes:
        g.disc(lx + 2, ly + 2, lr, lr * 0.9, OCC)
    lobes.sort(key=lambda l: l[1] + l[0] * 0.3)
    body = ramp[4] if dark else ramp[3]
    mid = ramp[3] if dark else ramp[2]
    top = ramp[2] if dark else ramp[1]
    for (lx, ly, lr) in lobes:
        for yy in range(int(ly - lr) - 1, int(ly + lr) + 2):
            for xx in range(int(lx - lr) - 1, int(lx + lr) + 2):
                nx = (xx - lx) / lr
                ny = (yy - ly) / (lr * 0.9)
                d = nx * nx + ny * ny
                if d > 1.0:
                    continue
                k = -(nx + ny) * 0.75 + (GRAIN.at(xx * 5, yy * 5) - 0.5) * 0.7
                c = body
                if k > 0.15:
                    c = mid if dith(xx, yy, 0.85) else body
                if k > 0.75 and d > 0.15:
                    c = top if dith(xx, yy, 0.7) else mid
                if d > 0.72 and (nx + ny) > 0.4:
                    c = ramp[4]
                g.set(xx, yy, c)


def bare_tree(g, x, y, h, seed, mat="stone"):
    r = rng_for(seed)
    g.line(x + 1, y + 1, x + 1 + h // 3, y + 1, OCC)
    g.line(x, y, x, y - h, T(mat, "shade"))
    g.set(x - 1, y - h // 2, T(mat, "base"))
    for k in range(3):
        by = y - h + k * (h // 4) + 1
        side = -1 if r.random() < 0.5 else 1
        ln = 2 + int(r.random() * 3)
        g.line(x, by, x + side * ln, by - ln // 2 - 1, T(mat, "shade"))


def standing_stone(g, x, y, h, w=3):
    """A granite slab, pale under the moon: lit left face, body, shaded right edge, a dark foot and
    occlusion on the ground lower right. Drawn in stone it vanished into the heather."""
    g.rect(x + 1, y + 1, x + w + 2, y + 2, OCC)
    g.rect(x, y - h, x + w - 1, y, T("skin", "shade"))
    g.rect(x, y - h, x, y, T("skin", "base"))
    g.rect(x + w - 1, y - h + 1, x + w - 1, y, T("skin", "deep"))
    g.rect(x + 1, y - 2, x + w - 1, y, T("skin", "deep"))
    g.set(x, y - h, T("skin", "lit"))
    g.set(x + 1, y - h, T("skin", "base"))
    g.set(x + w - 1, y - h, T("skin", "shade"))
    g.set(x + 1, y - h // 2, T("lichen", "shade"))


def bush(g, x, y, r, mat="lichen"):
    g.disc(x + 1, y + 1, r, r * 0.7, OCC)
    g.disc(x, y, r, r * 0.7, T(mat, "shade"))
    g.set(x - 1, y - 1, T(mat, "base"))
    if r > 1.5:
        g.set(x - 1, y - 2, T(mat, "base"))
        g.set(x, y - 1, T(mat, "base"))


def _ridge(r, ax, ay, bx, by, rough, depth=4):
    """Midpoint displacement between two points: a jagged ridge line, as a list of points. The
    displacement is sideways only, so the line stays monotone in y and a row crosses it once."""
    pts = [(ax, ay), (bx, by)]
    amp = rough
    for _ in range(depth):
        out = [pts[0]]
        for i in range(len(pts) - 1):
            (x0, y0), (x1, y1) = pts[i], pts[i + 1]
            out.append(((x0 + x1) / 2.0 + (r.random() - 0.5) * amp, (y0 + y1) / 2.0))
            out.append((x1, y1))
        pts = out
        amp *= 0.55
    return pts


def _x_at(pts, y):
    """Where a monotone-in-y polyline crosses row y."""
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        if min(y0, y1) <= y <= max(y0, y1) and y0 != y1:
            return x0 + (y - y0) * (x1 - x0) / (y1 - y0)
    return None


def peak(g, cx, by, w, h, snow=0.35, seed=0):
    """A mountain from the south, as overworld maps draw them: a jagged ridge on each side of the
    summit, the face toward the key lit and the other in shade, split by a crooked spine, gullies
    on the lit face, a snow cap with a ragged foot, contact dark where it meets the ground."""
    r = rng_for(seed)
    tx = cx + (r.random() - 0.5) * w * 0.25
    ty = by - h
    lx = cx - w / 2.0
    rx = cx + w / 2.0
    left = _ridge(r, tx, ty, lx, by, h * 0.22)
    right = _ridge(r, tx, ty, rx, by, h * 0.22)
    spine = _ridge(r, tx, ty, tx + w * 0.14, by, w * 0.18, 3)
    gullies = [_ridge(r, tx - w * (0.08 + 0.1 * k), ty + h * (0.25 + 0.12 * k), tx - w * (0.2 + 0.08 * k), by,
                      w * 0.12, 3) for k in range(2)]
    for y in range(int(ty), int(by) + 1):
        xl = _x_at(left, y)
        xr = _x_at(right, y)
        xs = _x_at(spine, y)
        if xl is None or xr is None:
            continue
        if xs is None:
            xs = (xl + xr) / 2.0
        t = (y - ty) / float(max(1, by - ty))
        gx = [_x_at(gl, y) for gl in gullies]
        for x in range(int(math.floor(xl)), int(math.ceil(xr)) + 1):
            lit = x < xs
            cap = snow + (GRAIN.at(x * 6, y * 2) - 0.5) * 0.22
            if t < cap:
                c = (T("skin", "lit") if dith(x, y, 0.5 - t) else T("skin", "base")) if lit else T("skin", "shade")
                if not lit and x > xs + 2 and dith(x, y, 0.4):
                    c = T("steel", "shade")
            elif lit:
                c = T("stone", "lit") if dith(x, y, 0.5 - t * 0.6) else T("stone", "base")
                if x <= xl + 1.2:
                    c = T("stone", "lit")
                for gxx in gx:
                    if gxx is not None and abs(x - gxx) < 0.8:
                        c = T("stone", "shade")
            else:
                c = T("stone", "shade") if dith(x, y, 0.75 - t * 0.5) else T("stone", "deep")
                if x >= xr - 1.2 or t > 0.88:
                    c = OCC
            g.set(x, y, c)
    for (x, y) in right[len(right) // 2:]:
        g.set(x + 1, y + 1, OCC)


# ---------------------------------------------------------------------------------------------
# THE PLACES' LANDMARKS
# ---------------------------------------------------------------------------------------------
def scatter_forest(g, biomes):
    """The wood: crowns packed back to front, kept off the road. Elderbark's clearing at the focus."""
    r = rng_for(101)
    trees = []
    for _ in range(2600):
        x = r.random() * 330
        y = r.random() * H
        if x >= W or y >= H:
            continue
        if biomes[int(y) * W + int(x)] != "forest":
            continue
        if road_distance(x, y) < 7:
            continue
        if math.hypot(x - 150, (y - 430) * 1.3) < 34:
            continue
        trees.append((y, x, 4 + r.random() * 4.5))
    # A few outliers into the fields and lowland: hedgerow trees.
    for _ in range(260):
        x = r.random() * W
        y = r.random() * H
        b = biomes[int(y) * W + int(x)]
        if b not in ("lowland", "hills", "fields") or road_distance(x, y) < 8:
            continue
        if r.random() < (0.25 if b == "fields" else 0.6):
            trees.append((y, x, 3 + r.random() * 2.5))
    trees.sort()
    for y, x, rad in trees:
        # Mostly the dark crown: the wood is his, and a canopy of lit crowns read as bubbles.
        dark = x < 90 or r.random() < 0.6
        crown(g, x, y, rad, "lichen", dark=dark)


def elderbark(g):
    """Elderbark: one crown far larger than the wood, gnarled, darker, and his mark in it."""
    cx, cy = 150, 418
    # Roots in the clearing.
    for k in range(7):
        a = k / 7.0 * 2 * math.pi
        g.line(cx, cy + 14, cx + math.cos(a) * 30, cy + 16 + math.sin(a) * 14, T("stone", "shade"))
    g.rect(cx - 3, cy + 2, cx + 3, cy + 16, T("stone", "shade"))
    g.rect(cx - 3, cy + 2, cx - 2, cy + 16, T("stone", "base"))
    for i, (dx, dy, rr) in enumerate([(-12, -4, 15), (12, -2, 14), (0, -14, 16), (-2, 4, 15), (10, -16, 10),
                                       (-14, -16, 10)]):
        crown(g, cx + dx, cy + dy, rr, "lichen", dark=(i % 2 == 0))
    # Two points of his mark in the dark of the crown.
    g.set(cx - 3, cy - 4, T("arcane", "lit"))
    g.set(cx + 3, cy - 4, T("arcane", "lit"))


def fields(g, biomes):
    """Hedged fields, furrows, stooks, a millpond, the well, an empty farmhouse."""
    r = rng_for(202)
    # Field plots on a loose grid, each its own tone and furrow direction.
    for gy in range(560, 840, 30):
        for gx in range(250, 520, 38):
            x0 = gx + int((r.random() - 0.5) * 10)
            y0 = gy + int((r.random() - 0.5) * 8)
            x1 = x0 + 30 + int(r.random() * 12)
            y1 = y0 + 22 + int(r.random() * 8)
            pick = r.random()
            # Mostly dark: a harvested field under no light (campaign.md, "gold and linen from the
            # deep half"). One plot in five is stubble still catching a little light.
            tone = (T("gold", "shade") if pick < 0.2 else T("gold", "deep") if pick < 0.6
                    else T("flesh", "deep") if pick < 0.85 else T("linen", "deep"))
            furrow = T("flesh", "deep") if tone != T("flesh", "deep") else T("stone", "deep")
            vertical = r.random() < 0.5
            for y in range(y0, y1):
                for x in range(x0, x1):
                    if not (0 <= x < W and 0 <= y < H) or biomes[y * W + x] != "fields":
                        continue
                    if road_distance(x, y) < 4:
                        continue
                    on = (x if vertical else y) % 3 == 0
                    g.set(x, y, furrow if on and dith(x, y, 0.8) else tone)
            # Hedgerows along the bottom and right edges: a dark line with a lit top, and a tree now
            # and then, never a row of evenly spaced dots.
            for x in range(x0, x1 + 1):
                if 0 <= x < W and 0 <= y1 < H and biomes[y1 * W + x] == "fields" and road_distance(x, y1) > 5:
                    g.set(x, y1 + 1, OCC)
                    g.set(x, y1, T("lichen", "deep"))
                    g.set(x, y1 - 1, T("lichen", "shade") if dith(x, y1, 0.5) else T("lichen", "deep"))
            for y in range(y0, y1 + 1):
                if 0 <= x1 < W and 0 <= y < H and biomes[y * W + x1] == "fields" and road_distance(x1, y) > 5:
                    g.set(x1 + 1, y, OCC)
                    g.set(x1, y, T("lichen", "deep"))
                    g.set(x1 - 1, y, T("lichen", "shade") if dith(x1, y, 0.4) else T("lichen", "deep"))
            if r.random() < 0.5:
                tx = x0 + int(r.random() * (x1 - x0))
                if 0 <= tx < W and 0 <= y1 < H and biomes[y1 * W + tx] == "fields" and road_distance(tx, y1) > 7:
                    crown(g, tx, y1 - 2, 3 + r.random() * 1.5, "lichen")
            # Stooks in a stubble field.
            if tone == T("gold", "shade") and r.random() < 0.6:
                for k in range(4):
                    sx = x0 + 5 + int(r.random() * (x1 - x0 - 10))
                    sy = y0 + 5 + int(r.random() * (y1 - y0 - 10))
                    if road_distance(sx, sy) > 6 and biomes[min(H - 1, sy) * W + min(W - 1, sx)] == "fields":
                        g.set(sx + 1, sy + 1, OCC)
                        g.set(sx, sy, T("gold", "base"))
                        g.set(sx, sy - 1, T("gold", "lit"))
                        g.set(sx + 1, sy, T("gold", "shade"))
    # The millpond.
    lake(g, 300, 668, 17, 9, 203)
    # The farmstead, empty: a yard, the house and a barn, dark windows, a gate left open.
    yx, yy = 300, 604
    for y in range(yy, yy + 36):
        for x in range(yx, yx + 54):
            if road_distance(x, y) > 5:
                g.set(x, y, T("flesh", "deep") if dith(x, y, 0.5) else T("gold", "deep"))
    for x in range(yx, yx + 54, 2):
        g.set(x, yy, T("linen", "shade"))
        g.set(x, yy + 35, T("linen", "shade"))
    for y in range(yy, yy + 36, 2):
        g.set(yx, y, T("linen", "shade"))

    def house(hx, hy, w, h, roof):
        g.rect(hx + 2, hy + 2, hx + w + 2, hy + h + 2, OCC)
        g.rect(hx, hy, hx + w, hy + h, T("linen", "base"))
        g.rect(hx, hy, hx + 1, hy + h, T("linen", "lit"))
        g.rect(hx + w, hy, hx + w, hy + h, T("linen", "shade"))
        # Roof: a pitched gable, lit on the near slope.
        peak_h = h * 0.9
        g.poly([(hx - 2, hy + 1), (hx + w / 2.0, hy - peak_h), (hx + w + 2, hy + 1)], T(roof, "shade"))
        g.poly([(hx - 2, hy + 1), (hx + w / 2.0, hy - peak_h), (hx + w / 2.0, hy + 1)], T(roof, "base"))
        g.line(hx - 2, hy + 1, hx + w / 2.0, hy - peak_h, T(roof, "hi"))
        for wx in range(hx + 3, hx + w - 1, 5):
            g.rect(wx, hy + 3, wx + 1, hy + 4, OCC)
        g.rect(hx + w // 2 - 1, hy + h - 4, hx + w // 2 + 1, hy + h, OCC)

    house(306, 616, 20, 11, "red")
    house(334, 622, 14, 9, "linen")
    # The well, in the middle of the fields: a stone ring round a dark shaft, a roof on two posts.
    wx, wy = 362, 652
    g.disc(wx + 1, wy + 2, 10, 5, OCC)
    g.disc(wx, wy, 9, 5, T("stone", "base"))
    g.disc(wx, wy, 6, 3, OCC)
    for k in range(-9, 0):
        g.set(wx + k, wy - int(math.sqrt(max(0, 1 - (k / 9.0) ** 2)) * 5), T("stone", "lit"))
    g.line(wx - 7, wy, wx - 7, wy - 12, T("linen", "shade"))
    g.line(wx + 7, wy, wx + 7, wy - 12, T("linen", "deep"))
    g.poly([(wx - 10, wy - 11), (wx, wy - 17), (wx + 10, wy - 11)], T("red", "shade"))
    g.poly([(wx - 10, wy - 11), (wx, wy - 17), (wx, wy - 11)], T("red", "base"))
    g.line(wx - 10, wy - 11, wx, wy - 17, T("red", "hi"))
    g.line(wx - 5, wy - 9, wx + 5, wy - 9, T("linen", "deep"))


def broken_circle(g, biomes):
    """The heath: heather tufts, granite, the tarn, eight stones and a gap where the ninth stood."""
    r = rng_for(303)
    for _ in range(1600):
        x = 360 + r.random() * 340
        y = 180 + r.random() * 360
        if biomes[int(y) * W + int(x)] != "heath" or road_distance(x, y) < 5:
            continue
        g.set(x, y, T("violet", "base") if r.random() < 0.7 else T("violet", "lit"))
        g.set(x + 1, y + 1, T("violet", "deep"))
    for _ in range(50):
        x = 380 + r.random() * 300
        y = 200 + r.random() * 320
        if biomes[int(y) * W + int(x)] != "heath" or road_distance(x, y) < 8:
            continue
        rr = 2 + r.random() * 3
        g.disc(x + 1, y + 1, rr, rr * 0.6, OCC)
        g.disc(x, y, rr, rr * 0.6, T("stone", "base"))
        g.set(x - 1, y - 1, T("stone", "lit"))
    lake(g, 575, 268, 15, 8, 304)
    cx, cy = 520, 300
    # The moot's floor: the turf inside the ring is worn.
    g.disc(cx, cy, 34, 18, T("violet", "deep"))
    g.disc(cx, cy, 30, 16, T("violet", "shade"))
    for yy in range(cy - 16, cy + 17):
        for xx in range(cx - 30, cx + 31):
            if ((xx - cx) / 30.0) ** 2 + ((yy - cy) / 16.0) ** 2 <= 1 and dith(xx, yy, 0.3):
                g.set(xx, yy, T("violet", "deep"))
    g.disc(cx, cy, 7, 4, T("stone", "shade"))
    g.disc(cx, cy - 1, 6, 3, T("stone", "base"))
    g.set(cx - 3, cy - 2, T("stone", "lit"))
    stones = []
    for k in range(9):
        if k == 2:
            continue  # the ninth stone is gone - it walks
        a = -math.pi / 2 + k / 9.0 * 2 * math.pi
        stones.append((cy + math.sin(a) * 16, cx + math.cos(a) * 30))
    stones.sort()
    for sy, sx in stones:
        standing_stone(g, int(sx) - 2, int(sy) + 4, 11, 4)
    # A fallen stone where the gap is.
    a = -math.pi / 2 + 2 / 9.0 * 2 * math.pi
    fx, fy = cx + math.cos(a) * 42, cy + math.sin(a) * 22 + 6
    g.rect(fx, fy + 1, fx + 8, fy + 1, OCC)
    g.rect(fx, fy - 2, fx + 7, fy, T("stone", "base"))
    g.rect(fx, fy - 2, fx + 7, fy - 2, T("stone", "lit"))


def cave(g):
    """The scarp the heath ends on, and the cave mouth the stream goes into."""
    # The scarp: where the heath ends, a rock face drops to the cave country. Its top is a ragged
    # lip, its face is vertical rock - striated, lit where it turns toward the key - and its foot
    # is in shadow. A face, not a band: its height varies along it.
    r = rng_for(321)
    lip = spline([(600, 382), (626, 404), (648, 424), (668, 436), (700, 440), (728, 434), (752, 422),
                  (772, 410)], 1.0)
    cols = {}
    for (x, y) in lip:
        xi = int(round(x))
        if xi not in cols or y < cols[xi]:
            cols[xi] = y
    for xi in sorted(cols):
        top = cols[xi] + (GRAIN.at(xi * 6, 50) - 0.5) * 3
        height = 16 + (GRAIN.at(xi * 2, 90) - 0.5) * 14
        for k in range(int(height) + 1):
            y = top + k
            t = k / height
            stria = (xi * 7 + int(GRAIN.at(xi * 9, y * 0.5) * 9)) % 5 == 0
            if t < 0.15:
                c = T("stone", "lit")
            elif stria:
                c = T("stone", "deep")
            elif t < 0.55:
                c = T("stone", "base") if dith(xi, y, 0.65 - t) else T("stone", "shade")
            else:
                c = T("stone", "shade") if dith(xi, y, 1.0 - t) else T("stone", "deep")
            g.set(xi, y, c)
        g.set(xi, top + height + 1, OCC)
        g.set(xi, top + height + 2, OCC if dith(xi, 0, 0.5) else T("stone", "deep"))
        g.set(xi, top - 1, T("stone", "base"))
    # Scree at the foot.
    for _ in range(60):
        x = 610 + r.random() * 160
        y0 = cols.get(int(x), 430) + 18 + r.random() * 12
        g.set(x, y0, T("stone", "base"))
        g.set(x + 1, y0 + 1, OCC)
    # The mouth: an arch cut into the face, dark all the way in, its lit rim on the upper left.
    mx, my = 690, 452
    for yy in range(my - 12, my + 10):
        for xx in range(mx - 13, mx + 14):
            nx = (xx - mx) / 13.0
            ny = (yy - my) / 12.0
            if ny > 0.7:
                ny = 0.7
            d = nx * nx + ny * ny
            if d <= 1.0:
                g.set(xx, yy, T("stone", "base") if (nx + ny) < -0.4 else T("stone", "shade"))
            if d <= 0.62 and yy >= my - 8:
                g.set(xx, yy, OCC)
    for k in range(-10, 1):
        g.set(mx + k, my - 9 + (k * k) // 14, T("stone", "lit"))
    # Drips of the stream going in.
    g.set(mx - 1, my + 3, WATER_LIT)
    g.set(mx + 1, my + 5, WATER_LIT)
    # The stream falls over the lip on its way in, so it is drawn again over the cliff face.
    draw_water_path(g, STREAM[2:], 2, 3)


def tower(g, tx, ty, w, h):
    """A square tower seen from the south: lit left face, shaded right, crenels, a slit window."""
    g.rect(tx + 1, ty + 1, tx + w + 1, ty + h + 1, OCC)
    g.rect(tx, ty, tx + w - 1, ty + h, T("stone", "base"))
    g.rect(tx, ty, tx, ty + h, T("stone", "lit"))
    g.rect(tx + w - 1, ty + 1, tx + w - 1, ty + h, T("stone", "shade"))
    g.rect(tx + 1, ty + h - 2, tx + w - 1, ty + h, T("stone", "shade"))
    for x in range(tx, tx + w, 2):
        g.set(x, ty - 1, T("stone", "lit") if x < tx + w // 2 else T("stone", "base"))
    g.set(tx + w // 2, ty + h // 3, OCC)
    g.set(tx + w // 2, ty + h // 3 + 1, OCC)


def keep(g):
    """The Cursed Dungeon: a keep on a crag, its walls across the road's one way through."""
    cx, cy = 834, 574
    # The crag.
    g.disc(cx + 4, cy + 14, 46, 26, OCC)
    g.disc(cx, cy + 10, 44, 24, T("stone", "shade"))
    for yy in range(cy - 14, cy + 34):
        for xx in range(cx - 44, cx + 44):
            nx = (xx - cx) / 44.0
            ny = (yy - cy - 10) / 24.0
            if nx * nx + ny * ny > 1:
                continue
            k = -(nx + ny) + (GRAIN.at(xx * 4, yy * 4) - 0.5) * 0.8
            if k > 0.0:
                g.set(xx, yy, T("stone", "base") if dith(xx, yy, 0.4 + k * 0.5) else T("stone", "shade"))
            if k > 0.9 and dith(xx, yy, 0.5):
                g.set(xx, yy, T("stone", "lit"))
            elif k < -0.7:
                g.set(xx, yy, T("stone", "deep"))
            # Fissures in the rock.
            if (xx * 3 + yy * 7) % 23 == 0 and GRAIN.at(xx * 7, yy * 7) > 0.55:
                g.set(xx, yy, OCC)
                g.set(xx + 1, yy + 1, OCC)
    # The curtain wall: its top, then its front face.
    x0, x1 = cx - 28, cx + 28
    top, front = cy - 8, cy + 12
    g.rect(x0, top, x1, front, T("stone", "deep"))
    g.rect(x0, top, x1, top + 1, T("stone", "base"))
    g.rect(x0, front - 8, x1, front, T("stone", "base"))
    g.rect(x0, front - 8, x0 + 1, front, T("stone", "lit"))
    g.rect(x0, front - 1, x1, front, T("stone", "shade"))
    for x in range(x0, x1 + 1, 3):
        g.set(x, front - 9, T("stone", "lit"))
        g.set(x + 1, front - 9, T("stone", "base"))
    # The towers: the back two first, the front two over the wall.
    for tx, ty in ((x0 - 3, top - 8), (x1 - 4, top - 8)):
        tower(g, tx, ty, 8, 14)
    # The keep: tall, in the middle, behind the front wall.
    kx, ky = cx - 9, cy - 32
    g.rect(kx + 2, ky + 2, kx + 20, ky + 34, OCC)
    g.rect(kx, ky, kx + 17, ky + 32, T("stone", "base"))
    g.rect(kx, ky, kx + 1, ky + 32, T("stone", "lit"))
    g.rect(kx + 15, ky + 1, kx + 17, ky + 32, T("stone", "shade"))
    for x in range(kx, kx + 18, 3):
        g.set(x, ky - 1, T("stone", "lit"))
        g.set(x + 1, ky - 1, T("stone", "base"))
    for wy in (ky + 6, ky + 15, ky + 23):
        for wx in (kx + 5, kx + 11):
            g.set(wx, wy, OCC)
            g.set(wx, wy + 1, OCC)
    # One cell window that is not dark: somebody is kept here.
    g.set(kx + 11, ky + 23, E["light"][3])
    for tx, ty in ((x0 - 3, front - 16), (x1 - 4, front - 16)):
        tower(g, tx, ty, 8, 18)
    # The gate, on the road side.
    g.rect(cx - 4, front - 6, cx + 3, front, OCC)
    g.rect(cx - 3, front - 7, cx + 2, front - 7, OCC)
    g.line(cx - 4, front - 7, cx - 2, front - 8, T("stone", "lit"))
    # A tattered banner on the keep.
    g.line(kx + 8, ky - 1, kx + 8, ky - 10, T("stone", "shade"))
    g.rect(kx + 9, ky - 10, kx + 13, ky - 6, T("red", "base"))
    g.set(kx + 9, ky - 10, T("red", "hi"))
    g.set(kx + 13, ky - 6, T("red", "shade"))


def swamp(g, biomes):
    """The delta bog: black pools, reeds, drowned trees, and his lanterns in the reeds."""
    r = rng_for(404)
    for _ in range(80):
        x = 880 + r.random() * 260
        y = 600 + r.random() * 200
        if biomes[int(y) * W + int(x)] != "swamp" or road_distance(x, y) < 10:
            continue
        # A pool is a few ellipses run together, black water with a skin of the sky on it - never
        # one clean ellipse, which reads as a coin.
        parts = []
        for k in range(2 + int(r.random() * 3)):
            parts.append((x + (r.random() - 0.5) * 14, y + (r.random() - 0.5) * 6,
                          3 + r.random() * 7, 1.5 + r.random() * 3))
        for (px, py, prx, pry) in parts:
            g.disc(px, py + 1, prx, pry, OCC)
        for (px, py, prx, pry) in parts:
            g.disc(px, py, prx, pry, T("stone", "deep"))
        for (px, py, prx, pry) in parts:
            for yy in range(int(py - pry), int(py + pry) + 1):
                for xx in range(int(px - prx), int(px + prx) + 1):
                    nx = (xx - px) / prx
                    ny = (yy - py) / pry
                    if nx * nx + ny * ny < 0.55 and dith(xx, yy, 0.5):
                        g.set(xx, yy, WATER)
            g.set(px - prx * 0.4, py - pry * 0.3, WATER_LIT)
    for _ in range(900):
        x = 880 + r.random() * 260
        y = 580 + r.random() * 220
        if biomes[int(y) * W + int(x)] != "swamp" or road_distance(x, y) < 4:
            continue
        if g.get(x, y) == rgba(WATER):
            continue
        g.set(x, y, T("linen", "base"))
        g.set(x, y - 1, T("linen", "lit") if r.random() < 0.3 else T("linen", "base"))
        g.set(x + 1, y + 1, T("linen", "deep"))
    for _ in range(14):
        x = 900 + r.random() * 230
        y = 600 + r.random() * 160
        if biomes[int(y) * W + int(x)] == "swamp" and road_distance(x, y) > 8:
            bare_tree(g, int(x), int(y), 6 + int(r.random() * 5), int(r.random() * 1e6), "linen")
    # The lanterns: cold light, his. A point, a cross of its own colour, and a little lit ground.
    for (lx, ly) in [(966, 650), (1012, 640), (1046, 690), (990, 700), (1080, 664), (936, 676),
                     (1024, 724)]:
        g.set(lx, ly + 1, OCC)
        g.line(lx, ly, lx, ly - 3, T("stone", "shade"))
        g.set(lx, ly - 4, T("arcane", "hi"))
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            g.set(lx + dx, ly - 4 + dy, T("arcane", "lit"))
        for dx, dy in ((-2, 0), (2, 0), (0, -2)):
            g.set(lx + dx, ly - 4 + dy, T("arcane", "base"))


def ruins(g, biomes):
    """The plateau: a broken ring wall, spires snapped off, fallen columns, the old ward's lines."""
    cx, cy = 1066, 362
    r = rng_for(505)
    # The ward circle cut in the ground, broken.
    for k in range(120):
        a = k / 120.0 * 2 * math.pi
        if 1.1 < a < 1.7 or 3.9 < a < 4.4:
            continue
        g.set(cx + math.cos(a) * 34, cy + math.sin(a) * 18, T("gold", "shade"))
        g.set(cx + math.cos(a) * 30, cy + math.sin(a) * 16, T("gold", "deep"))
    # Rubble.
    for _ in range(260):
        x = cx + (r.random() - 0.5) * 120
        y = cy + (r.random() - 0.5) * 80
        if biomes[int(y) * W + int(x)] != "plateau" or road_distance(x, y) < 5:
            continue
        g.set(x, y, T("skin", "deep"))
        if r.random() < 0.3:
            g.set(x - 1, y, T("skin", "shade"))
    # The ring wall, in broken arcs.
    for k in range(70):
        a = k / 70.0 * 2 * math.pi
        if math.sin(a * 3 + 1) > 0.55:
            continue
        x = cx + math.cos(a) * 46
        y = cy + math.sin(a) * 26
        g.rect(x, y - 3, x + 2, y, T("skin", "shade"))
        g.set(x, y - 3, T("skin", "base"))
        g.set(x + 1, y + 1, OCC)
    # Spires, snapped at different heights.
    for (sx, sy, h) in [(1048, 352, 40), (1080, 344, 26), (1062, 378, 16), (1096, 370, 33), (1034, 380, 12),
                        (1070, 340, 20)]:
        g.rect(sx + 1, sy + 1, sx + 7, sy + 2, OCC)
        g.rect(sx, sy - h, sx + 4, sy, T("skin", "shade"))
        g.rect(sx, sy - h, sx + 1, sy, T("skin", "base"))
        g.rect(sx + 4, sy - h + 1, sx + 4, sy, T("skin", "deep"))
        # Snapped: a ragged top, lit on the side the key comes from.
        g.set(sx, sy - h, T("skin", "lit"))
        g.set(sx + 1, sy - h - 1, T("skin", "base"))
        g.set(sx + 3, sy - h + 1, T("skin", "deep"))
        g.set(sx + 4, sy - h + 2, T("skin", "deep"))
        g.set(sx + 2, sy - h + 5, OCC)
        g.set(sx + 2, sy - h + 6, OCC)
    # Fallen columns.
    for (fx, fy, ln) in [(1018, 388, 14), (1102, 384, 10)]:
        g.rect(fx, fy + 1, fx + ln, fy + 1, OCC)
        g.rect(fx, fy - 2, fx + ln, fy, T("skin", "shade"))
        g.rect(fx, fy - 2, fx + ln, fy - 2, T("skin", "base"))


def frozen(g, biomes):
    """The ice field under the peaks, wind ridges, and somebody under the ice."""
    cx, cy = 1206, 152
    r = rng_for(606)
    # The ice: a frozen lake, its far rim a lip of snow, its near edge a step down in shadow.
    g.disc(cx + 2, cy + 3, 49, 24, OCC)
    g.disc(cx, cy, 48, 23, T("skin", "base"))
    g.disc(cx, cy + 1, 45, 21, T("steel", "shade"))
    for yy in range(cy - 21, cy + 22):
        for xx in range(cx - 45, cx + 46):
            nx = (xx - cx) / 45.0
            ny = (yy - cy - 1) / 21.0
            if nx * nx + ny * ny > 1:
                continue
            k = -(nx + ny) + (GRAIN.at(xx * 2, yy * 2) - 0.5) * 0.6
            if k > 0.2 and dith(xx, yy, 0.55):
                g.set(xx, yy, T("steel", "base"))
            if k > 0.9 and dith(xx, yy, 0.35):
                g.set(xx, yy, T("steel", "lit"))
    # Cracks that change angle.
    for k in range(7):
        x = cx + (r.random() - 0.5) * 60
        y = cy + (r.random() - 0.5) * 26
        ang = r.random() * 2 * math.pi
        for seg in range(3):
            ang += (r.random() - 0.5) * 1.6
            run = 3 + int(r.random() * 5)
            for j in range(run):
                px = x + math.cos(ang) * j
                py = y + math.sin(ang) * j * 0.6
                if ((px - cx) / 40.0) ** 2 + ((py - cy) / 20.0) ** 2 < 0.9:
                    g.set(px, py, T("steel", "lit") if j % 3 else T("steel", "deep"))
            x += math.cos(ang) * run
            y += math.sin(ang) * run * 0.6
    # A wizard lies under the ice: a figure laid out flat, seen through it - his robe the player's
    # wool, his pointed hat, his staff beside him. Dim: there is ice between him and the light.
    fx, fy = cx - 8, cy + 2
    g.rect(fx, fy - 2, fx + 13, fy + 2, T("wool", "deep"))
    g.rect(fx + 2, fy - 2, fx + 11, fy - 2, T("wool", "shade"))
    g.rect(fx + 14, fy - 1, fx + 16, fy + 1, T("skin", "deep"))
    g.line(fx + 17, fy, fx + 22, fy - 2, T("wool", "deep"))
    g.set(fx + 22, fy - 2, T("wool", "shade"))
    g.line(fx - 2, fy + 4, fx + 18, fy + 4, T("steel", "deep"))
    g.set(fx - 3, fy + 4, T("steel", "lit"))
    # Drifts: long low banks the wind has laid, lit along their tops with their lee in shadow, and
    # rocks breaking through with snow on their heads.
    for k in range(46):
        x = 1080 + r.random() * 280
        y = 80 + r.random() * 190
        if biomes[int(y) * W + int(x)] != "snow" or road_distance(x, y) < 8:
            continue
        if ((x - cx) / 46.0) ** 2 + ((y - cy) / 26.0) ** 2 < 1:
            continue
        ln = 12 + int(r.random() * 22)
        bow = 1.5 + r.random() * 2.5
        for j in range(ln):
            t = j / float(ln)
            yy = y - math.sin(t * math.pi) * bow
            if road_distance(x + j, yy) < 4:
                continue
            g.set(x + j, yy, T("skin", "base"))
            g.set(x + j, yy - 1, T("skin", "lit") if (0.2 < t < 0.6) else T("skin", "shade"))
            g.set(x + j, yy + 1, T("steel", "shade"))
            if dith(x + j, yy + 2, 0.5):
                g.set(x + j, yy + 2, T("stone", "shade"))
    for k in range(18):
        x = 1090 + r.random() * 260
        y = 90 + r.random() * 170
        if biomes[int(y) * W + int(x)] != "snow" or road_distance(x, y) < 8:
            continue
        rr = 2 + r.random() * 3
        g.disc(x + 1, y + 1, rr, rr * 0.7, OCC)
        g.disc(x, y, rr, rr * 0.7, T("stone", "base"))
        g.disc(x - 0.5, y - rr * 0.35, rr * 0.8, rr * 0.35, T("skin", "base"))
        g.set(x - 1, y - rr * 0.5, T("skin", "lit"))


def mountains(g, biomes):
    """The northern wall of peaks, and the range between the ice and the sands with its pass."""
    r = rng_for(707)
    peaks = []

    def chain(x0, x1, base_y, wander, hmin, hmax, rows, skip=None):
        # A range is ranks of peaks, the back rank higher and smaller, each peak a different size
        # and spacing: evenly spaced anything reads as a row of stamps.
        for rank in range(rows):
            x = x0 + r.random() * 20
            while x < x1:
                if skip is None or abs(x - skip[0]) > skip[1]:
                    by = base_y(x) + rank * 16 + (r.random() - 0.5) * wander
                    h = hmin + r.random() * (hmax - hmin) - rank * 8
                    w = h * (1.0 + r.random() * 0.8)
                    peaks.append((by, x, w, h))
                x += 16 + r.random() * 34

    # The north wall, where the land rises into the dark.
    chain(260, 1520, lambda x: 52 + math.sin(x / 90.0) * 14, 22, 40, 84, 3)
    # The range between the ice and the sands, with the pass the road takes.
    chain(1120, 1470, lambda x: 350 + math.sin(x / 50.0) * 18, 22, 32, 64, 2, skip=(1262, 30))
    peaks.sort()
    for by, x, w, h in peaks:
        if road_distance(x, by) < 22 or road_distance(x, by - h * 0.5) < 18:
            continue
        snow = 0.32 if (by < 160 or x > 1100) else 0.2
        peak(g, x, by, w, h, snow, seed=int(x * 7 + by))


def groves(g, biomes):
    """Woods in the hill country and the lowland: clusters of crowns, so the land between places
    has things standing on it, and lone trees where the clusters thin out."""
    r = rng_for(717)
    centres = []
    for _ in range(40):
        x = 420 + r.random() * 1060
        y = 120 + r.random() * 640
        b = biomes[int(y) * W + int(x)]
        if b in ("hills", "lowland", "heath") and road_distance(x, y) > 26:
            centres.append((x, y, 8 + r.random() * 18))
    trees = []
    for (cx, cy, rad) in centres:
        n = int(rad * rad / 9)
        for _ in range(n):
            a = r.random() * 2 * math.pi
            d = math.sqrt(r.random()) * rad
            x = cx + math.cos(a) * d
            y = cy + math.sin(a) * d * 0.7
            b = biomes[min(H - 1, int(y)) * W + min(W - 1, int(x))]
            if b in ("hills", "lowland", "heath") and road_distance(x, y) > 7:
                trees.append((y, x, 3 + r.random() * 2.5, b == "heath"))
    trees.sort()
    for (y, x, rad, heath) in trees:
        crown(g, x, y, rad, "lichen", dark=heath or r.random() < 0.4)


def sands(g, biomes):
    """Dunes, a dry riverbed where he hid the water, cracked earth, a broken cart on the trade road."""
    r = rng_for(808)
    draw_water_path(g, DRY_RIVER, 3, 5, water=T("flesh", "deep"), lit=T("gold", "deep"), bank=OCC)
    # Crescent dunes: a long windward back lit from the upper left, a sharp crest, and the slip
    # face below it falling into shadow. Sorted back to front so a nearer dune covers a farther one.
    dunes = []
    for _ in range(60):
        x = 1220 + r.random() * 260
        y = 470 + r.random() * 300
        if biomes[int(y) * W + int(x)] != "desert" or road_distance(x, y) < 12:
            continue
        dunes.append((y, x, 16 + r.random() * 22, 2 + r.random() * 3))
    dunes.sort()
    for (y, x, ln, bow) in dunes:
        for j in range(int(ln)):
            t = j / ln
            xx = x + j
            yy = y - math.sin(t * math.pi) * bow + math.sin(t * 6.0) * 0.6
            if road_distance(xx, yy) < 4 or biomes[min(H - 1, int(yy)) * W + min(W - 1, int(xx))] != "desert":
                continue
            # The windward back, lit, fading up into the sand; the crest; the slip face below.
            for k in range(1, 5):
                if dith(xx, yy - k, 0.9 - k * 0.22):
                    g.set(xx, yy - k, T("gold", "shade"))
            g.set(xx, yy, T("gold", "base") if t < 0.7 else T("gold", "shade"))
            g.set(xx, yy + 1, T("flesh", "deep"))
            if dith(xx, yy + 2, 0.6 - t * 0.3):
                g.set(xx, yy + 2, OCC)
    # Cracked earth: short angular cracks in occlusion.
    for _ in range(40):
        x = 1300 + r.random() * 160
        y = 560 + r.random() * 160
        if biomes[int(y) * W + int(x)] != "desert" or road_distance(x, y) < 5:
            continue
        ang = r.random() * 2 * math.pi
        for seg in range(3):
            ang += (r.random() - 0.5) * 1.8
            run = 2 + int(r.random() * 4)
            for j in range(run):
                g.set(x + math.cos(ang) * j, y + math.sin(ang) * j, T("gold", "deep"))
            x += math.cos(ang) * run
            y += math.sin(ang) * run
    # The oasis he dried: a cracked pan where the water was, dead palms round it.
    ox, oy = 1384, 628
    g.disc(ox + 1, oy + 2, 28, 12, OCC)
    g.disc(ox, oy, 27, 11, T("flesh", "shade"))
    for yy in range(oy - 11, oy + 12):
        for xx in range(ox - 27, ox + 28):
            nx = (xx - ox) / 27.0
            ny = (yy - oy) / 11.0
            if nx * nx + ny * ny > 1:
                continue
            if -(nx + ny) < -0.3 and dith(xx, yy, 0.6):
                g.set(xx, yy, T("flesh", "deep"))
            # The mud cracked into plates.
            if ((xx + int(GRAIN.at(xx * 3, yy * 9) * 7)) % 7 == 0) or ((yy * 3 + int(GRAIN.at(xx * 9, yy * 3) * 9)) % 8 == 0):
                g.set(xx, yy, T("gold", "deep"))
    g.disc(ox - 3, oy + 1, 5, 2, T("stone", "deep"))
    for (px, py, lean) in ((ox - 30, oy - 4, -1), (ox - 18, oy - 12, 1), (ox + 8, oy - 13, -1), (ox + 26, oy - 6, 1),
                           (ox + 20, oy + 10, 1)):
        g.line(px + 2, py + 1, px + 8, py + 1, OCC)
        for k in range(12):
            g.set(px + (k * lean) // 5, py - k, T("linen", "shade") if k % 3 else T("linen", "deep"))
        tx, ty = px + (12 * lean) // 5, py - 12
        for (dx, dy) in ((-5, 2), (-3, -1), (0, -3), (3, -1), (5, 2), (-6, 3), (6, 3)):
            g.line(tx, ty, tx + dx, ty + dy, T("linen", "deep"))
        g.set(tx, ty, T("linen", "base"))
    # The caravan road: a faint track west to east across the basin.
    for k in range(0, 190, 3):
        tx = 1270 + k
        ty = 676 - k * 0.2 + math.sin(k / 17.0) * 4
        if road_distance(tx, ty) > 6 and dith(tx, ty, 0.7):
            g.set(tx, ty, T("gold", "shade"))
            g.set(tx + 1, ty, T("gold", "shade"))
    # The broken cart on it.
    cx, cy = 1320, 668
    g.rect(cx + 1, cy + 1, cx + 16, cy + 4, OCC)
    g.rect(cx, cy - 6, cx + 14, cy, T("gold", "shade"))
    g.rect(cx, cy - 6, cx + 14, cy - 6, T("gold", "base"))
    g.rect(cx + 2, cy - 4, cx + 12, cy - 4, T("gold", "deep"))
    g.disc(cx + 3, cy + 1, 3, 3, T("stone", "shade"))
    g.disc(cx + 3, cy + 1, 1, 1, OCC)
    g.line(cx + 14, cy - 3, cx + 21, cy - 7, T("gold", "deep"))
    g.line(cx + 11, cy + 2, cx + 14, cy + 4, T("stone", "shade"))
    # The dead still walk the trade road: a file of bent figures and a pack beast, each with his mark.
    for k in range(5):
        fx = 1404 + k * 11
        fy = 664 - k * 3
        if k == 2:
            # The beast: a long back, a hung head, a load.
            g.rect(fx - 2, fy - 6, fx + 7, fy - 3, T("linen", "deep"))
            g.rect(fx, fy - 9, fx + 5, fy - 6, T("gold", "deep"))
            g.set(fx + 8, fy - 5, T("linen", "deep"))
            g.set(fx + 9, fy - 4, T("linen", "deep"))
            for lx in (fx - 1, fx + 1, fx + 4, fx + 6):
                g.line(lx, fy - 2, lx, fy, T("linen", "deep"))
            g.set(fx + 9, fy - 5, T("arcane", "lit"))
            g.line(fx - 2, fy + 1, fx + 8, fy + 1, OCC)
            continue
        g.rect(fx, fy - 7, fx + 2, fy, T("linen", "deep"))
        g.set(fx + 1, fy - 8, T("linen", "shade"))
        g.set(fx + 2, fy - 6, T("linen", "shade"))
        g.set(fx + 2, fy - 7, T("arcane", "lit"))
        g.line(fx + 1, fy + 1, fx + 4, fy + 1, OCC)


def lava_runnels(g, seed, cx, top, count, width):
    """Lava down the cone and onto the plain: angular runs (a crust splitting, tiles.py's rule),
    an edge of fire's edge tone, a hot thread, and a little of its light on the ground beside it."""
    r = rng_for(seed)
    for k in range(count):
        x = cx + (r.random() - 0.5) * 26
        y = top + 10.0
        ang = math.pi / 2 + (r.random() - 0.5) * 1.1
        for seg in range(10):
            ang += (r.random() - 0.5) * 0.7
            ang = max(math.pi / 2 - 0.7, min(math.pi / 2 + 0.7, ang))
            run = 8 + int(r.random() * 12)
            wide = width * (1.0 - seg / 14.0)
            for j in range(run):
                px = x + math.cos(ang) * j
                py = y + math.sin(ang) * j
                if px >= W - 4 or py >= H - 4:
                    break
                if road_distance(px, py) < 5:
                    continue
                for dx in range(-2, 3):
                    for dy in range(-2, 3):
                        d = math.hypot(dx, dy)
                        if d <= wide:
                            g.set(px + dx, py + dy, E["fire"][2] if d < wide * 0.45 else E["fire"][3])
                        elif d <= wide + 1.5 and dith(px + dx, py + dy, 0.3):
                            cur = g.get(px + dx, py + dy)
                            if lum(cur) < lum(E["fire"][3]):
                                g.set(px + dx, py + dy, E["fire"][3])
                if j % 5 == 2:
                    g.set(px, py, E["fire"][1])
            x += math.cos(ang) * run
            y += math.sin(ang) * run


def emberdeep(g, biomes):
    """Black ground and lava; the gate at the foot; the volcano; his seat at the summit."""
    r = rng_for(909)
    cx, top, base = 1576, 292, 446
    # Lava on the plain first, then the cone over its upper reaches, then lava down the cone.
    lava_runnels(g, 910, cx, top + 40, 3, 1.8)
    # The cone: a broad mountain of black rock, its flank toward the key catching it in violet -
    # the Emberdeep's own ground (tiles.py's basalt: dark stone with violet in it).
    left_edge = _ridge(r, cx - 22, top + 6, cx - 112, base, 26)
    right_edge = _ridge(r, cx + 22, top + 6, cx + 112, base, 26)
    spine = _ridge(r, cx - 4, top + 8, cx + 18, base, 30, 3)
    for y in range(top + 6, base + 1):
        xl = _x_at(left_edge, y)
        xr = _x_at(right_edge, y)
        xs = _x_at(spine, y)
        if xl is None or xr is None or xs is None:
            continue
        t = (y - top) / float(base - top)
        for x in range(int(xl), int(xr) + 1):
            if road_distance(x, y) < 3.6:
                continue
            if x < xs:
                c = T("violet", "base") if dith(x, y, 0.45 - t * 0.5) else T("violet", "shade")
                if dith(x, y, t * 0.9 - 0.1):
                    c = T("stone", "shade")
                if x <= xl + 1.5:
                    c = T("violet", "lit") if t < 0.5 else T("violet", "base")
            else:
                c = T("stone", "deep") if dith(x, y, 0.75 - t * 0.4) else OCC
            g.set(x, y, c)
    # The foot of the cone, ragged: it meets the plain in scree and its own shadow.
    for x in range(cx - 112, cx + 113):
        fy = base + int((GRAIN.at(x * 5, 77) - 0.5) * 10)
        for y in range(base - 4, fy + 1):
            if road_distance(x, y) < 3.6:
                continue
            g.set(x, y, T("stone", "deep") if dith(x, y, 0.5) else OCC)
        if dith(x, fy, 0.4) and road_distance(x, fy + 1) > 3.6:
            g.set(x, fy + 1, T("violet", "shade"))
    lava_runnels(g, 911, cx, top, 4, 1.5)
    # The seat: the caldera's lip, and inside it the only dark light in the world.
    g.disc(cx, top + 6, 30, 12, T("stone", "shade"))
    for xx in range(cx - 30, cx + 31):
        nx = (xx - cx) / 30.0
        yy = top + 6 - int(math.sqrt(max(0, 1 - nx * nx)) * 12)
        g.set(xx, yy, T("violet", "lit") if nx < 0.1 else T("stone", "base"))
    g.disc(cx, top + 8, 25, 8, OCC)
    for yy in range(top - 2, top + 18):
        for xx in range(cx - 26, cx + 27):
            nx = (xx - cx) / 25.0
            ny = (yy - top - 8) / 8.0
            d = nx * nx + ny * ny
            if d > 1:
                continue
            if d < 0.1:
                g.set(xx, yy, E["darkness"][1] if dith(xx, yy, 0.6) else E["darkness"][2])
            elif d < 0.38:
                g.set(xx, yy, E["darkness"][2] if dith(xx, yy, 0.62 - d) else E["darkness"][3])
            elif d < 0.8 and dith(xx, yy, 0.8 - d):
                g.set(xx, yy, E["darkness"][3])
    g.set(cx, top + 8, E["darkness"][0])
    g.set(cx - 1, top + 8, E["darkness"][0])
    # Dark light rising off it: a few motes above the rim.
    for (mx, my) in ((cx - 8, top - 6), (cx + 5, top - 10), (cx - 2, top - 15), (cx + 11, top - 3)):
        g.set(mx, my, E["darkness"][2])
    # The gate at the foot: two black pillars that burn at the top - built, so fire, not his mark -
    # and a lintel across the road.
    gx, gy = 1511, 496
    for px in (gx - 15, gx + 11):
        g.rect(px + 2, gy + 1, px + 8, gy + 3, OCC)
        g.rect(px, gy - 26, px + 5, gy, T("stone", "deep"))
        g.rect(px, gy - 26, px + 1, gy, T("violet", "shade"))
        g.rect(px + 5, gy - 25, px + 5, gy, OCC)
        for k in range(gy - 22, gy, 6):
            g.rect(px, k, px + 5, k, OCC)
        # The brazier on top.
        g.rect(px - 1, gy - 28, px + 6, gy - 27, T("stone", "shade"))
        g.rect(px + 1, gy - 31, px + 4, gy - 29, E["fire"][2])
        g.set(px + 2, gy - 32, E["fire"][1])
        g.set(px + 3, gy - 33, E["fire"][0])
        g.set(px + 1, gy - 31, E["fire"][3])
        g.set(px + 4, gy - 30, E["fire"][3])
    g.rect(gx - 16, gy - 27, gx + 17, gy - 25, T("stone", "deep"))
    g.rect(gx - 16, gy - 27, gx + 17, gy - 27, T("violet", "shade"))
    # Basalt boulders.
    for _ in range(60):
        x = 1430 + r.random() * 250
        y = 200 + r.random() * 520
        if biomes[int(y) * W + int(x)] != "basalt" or road_distance(x, y) < 6:
            continue
        rr = 1.5 + r.random() * 3
        g.disc(x + 1, y + 1, rr, rr * 0.7, OCC)
        g.disc(x, y, rr, rr * 0.7, T("violet", "deep"))
        g.set(x - 1, y - 1, T("violet", "shade"))
        g.set(x - 1, y, T("violet", "shade"))


# ---------------------------------------------------------------------------------------------
# THE EDGE OF THE WORLD: every side falls into the dark, by the vignette's darken-only rule.
# ---------------------------------------------------------------------------------------------
FALL = [T("stone", "deep"), OCC]


def edge_falloff(g):
    band = 40
    for y in range(H):
        for x in range(W):
            d = min(x, y, W - 1 - x, H - 1 - y)
            if d >= band:
                continue
            wob = (WARP_X.at(x * 3, y * 3) - 0.5) * 16
            t = 1.0 - (d + wob) / float(band)
            if t <= 0:
                continue
            t = min(1.0, t)
            want = OCC if t > 0.7 or dith(x, y, t * 1.6 - 0.35) else (T("stone", "deep") if dith(x, y, t * 1.5) else None)
            if want is None:
                continue
            cur = g.pix[y * W + x]
            if lum(want) < lum(cur):
                g.pix[y * W + x] = rgba(want)


# ---------------------------------------------------------------------------------------------
# THE PAGE, off the world's east edge, and the pencil line that carries the road to it.
# ---------------------------------------------------------------------------------------------
def page():
    pw, ph = PAGE_SIZE
    g = Grid(pw, ph)
    x0, y0, x1, y1 = PAGE_RECT
    # Its shadow on the dark, offset down and right.
    g.rect(x0 + 4, y0 + 5, x1 + 4, y1 + 5, OCC)
    paper = T("skin", "lit")
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            # A sheet lit from the upper left: the far corner falls a step.
            t = ((x - x0) / float(x1 - x0) + (y - y0) / float(y1 - y0)) / 2.0
            c = paper if not dith(x, y, (t - 0.72) * 1.6) else T("skin", "base")
            # Squared ruling, faint: grey-blue on the white, a step darker where the page turns away.
            if (x - x0) % 8 == 0 or (y - y0) % 8 == 0:
                c = T("skin", "base") if c == paper else T("skin", "shade")
            g.set(x, y, c)
    # The margin, in red.
    g.rect(x0 + 18, y0, x0 + 18, y1, T("red", "hi"))
    # Torn left edge: the page came out of a workbook.
    r = rng_for(1001)
    for y in range(y0, y1 + 1):
        bite = int(r.random() * 3)
        for x in range(x0, x0 + bite):
            g.set(x, y, None)
    # The curl at the lower right corner, and the tape at the top.
    for k in range(10):
        for j in range(10 - k):
            g.set(x1 - j, y1 - k, None if j < 10 - k - 1 else T("skin", "shade"))
    g.poly([(x1 - 10, y1), (x1, y1 - 10), (x1 - 9, y1 - 9)], T("skin", "base"))
    for (tx, ty) in ((x0 + 60, y0 - 4), (x1 - 40, y0 - 3)):
        g.rect(tx, ty, tx + 18, ty + 7, T("gold", "lit"))
        g.rect(tx, ty, tx + 18, ty, T("gold", "hi"))
        g.rect(tx, ty + 7, tx + 18, ty + 7, T("gold", "base"))
    # The pencil line: from the world's edge (x 0 here is the world's x 1680) along the road's last
    # points, onto the page, and to the stand point. Graphite, hand-wobbled.
    last = [p for p in ROUTE if p[0] >= PAGE_ORIGIN[0] - 2]
    stand = PLACES[-1][1]
    pts = [(p[0] - PAGE_ORIGIN[0], p[1] - PAGE_ORIGIN[1]) for p in last]
    pts.append((stand[0] - PAGE_ORIGIN[0], stand[1] - PAGE_ORIGIN[1]))
    for i in range(len(pts) - 1):
        ax, ay = pts[i]
        bx, by = pts[i + 1]
        steps = int(max(1, math.hypot(bx - ax, by - ay)))
        for s in range(steps):
            t = s / float(steps)
            x = ax + (bx - ax) * t
            y = ay + (by - ay) * t + math.sin((x) / 9.0) * 0.6
            on_paper = x0 <= x <= x1 and y0 <= y <= y1
            ink = T("stone", "shade") if on_paper else T("skin", "shade")
            g.set(x, y, ink)
            if int(x) % 5 != 0:
                g.set(x, y + 1, ink)
    # A little pencil lying on the page, sharpened, where the line ends.
    px, py = x0 + 120, y1 - 30
    for k in range(26):
        g.set(px + k, py - k // 3, T("gold", "base"))
        g.set(px + k, py - k // 3 + 1, T("gold", "shade"))
        g.set(px + k + 1, py - k // 3 + 2, OCC)
    g.set(px - 1, py + 1, T("stone", "deep"))
    g.set(px - 2, py + 1, T("stone", "deep"))
    g.set(px, py, T("flesh", "lit"))
    return g


# ---------------------------------------------------------------------------------------------
# THE SMALL SPRITES
# ---------------------------------------------------------------------------------------------
def shroud(rx, ry, seed):
    """His fog over a locked place: a bank of dark, its edge broken by dither, lit a little on top."""
    w = int(rx * 2 + 8)
    h = int(ry * 2 + 8)
    g = Grid(w, h)
    r = rng_for(seed)
    blobs = []
    for _ in range(14):
        a = r.random() * 2 * math.pi
        d = math.sqrt(r.random()) * 0.6
        blobs.append((w / 2.0 + math.cos(a) * rx * d, h / 2.0 + math.sin(a) * ry * d,
                      rx * (0.35 + r.random() * 0.3), ry * (0.35 + r.random() * 0.3)))
    noise = Fbm(seed, 10, 2)
    for y in range(h):
        for x in range(w):
            dens = 0.0
            for (bx, by, brx, bry) in blobs:
                nx = (x - bx) / brx
                ny = (y - by) / bry
                dens = max(dens, 1.0 - math.sqrt(nx * nx + ny * ny))
            dens += (noise.at(x, y) - 0.5) * 0.5
            if dens <= 0:
                continue
            # A MIST, NOT A HOLE: it never covers more than about three pixels in four, so the place
            # shows through it, held. Solid occlusion here read as a pit in the map.
            if not dith(x, y, min(0.78, dens * 1.8)):
                continue
            # The top of the bank catches the key a little; its heart is the darkest.
            lit = (y < h * 0.55) and dith(x + 3, y + 1, 0.55 - dens)
            c = T("stone", "shade") if lit else (T("stone", "deep") if dens < 0.5 or dith(x, y, 0.5) else OCC)
            g.set(x, y, c)
    return g


def ring(state):
    """The mark under a stand point: a small worn circle on the ground."""
    g = Grid(20, 10)
    if state == "open":
        out, inner = T("gold", "shade"), T("gold", "base")
    elif state == "current":
        out, inner = T("gold", "base"), T("gold", "lit")
    else:
        out, inner = T("stone", "deep"), T("stone", "shade")
    for y in range(10):
        for x in range(20):
            nx = (x - 9.5) / 9.5
            ny = (y - 4.5) / 4.5
            d = nx * nx + ny * ny
            if 0.55 <= d <= 1.0:
                g.set(x, y, inner if (ny < 0 or nx < -0.6) else out)
    return g


def shadow():
    g = Grid(22, 7)
    for y in range(7):
        for x in range(22):
            nx = (x - 10.5) / 10.5
            ny = (y - 3) / 3.0
            if nx * nx + ny * ny <= 1:
                g.set(x, y, OCC)
    return g


def mark(kind):
    """The compass marks, 16x16: a lozenge in the stop's state, or the seat and the page."""
    g = Grid(16, 16)
    if kind in ("open", "cleared", "locked"):
        fill = {"open": T("gold", "base"), "cleared": T("gold", "lit"), "locked": T("stone", "base")}[kind]
        lit = {"open": T("gold", "lit"), "cleared": T("gold", "hi"), "locked": T("stone", "lit")}[kind]
        dark = {"open": T("gold", "shade"), "cleared": T("gold", "base"), "locked": T("stone", "shade")}[kind]
        for y in range(16):
            for x in range(16):
                d = abs(x - 7.5) + abs(y - 7.5)
                if d <= 7.6:
                    g.set(x, y, OCC)
                if d <= 6.2:
                    g.set(x, y, lit if (x + y) < 15 else dark)
                if d <= 4.6:
                    g.set(x, y, fill)
        if kind == "cleared":
            # A tick, dark in the light.
            for (x, y) in ((5, 8), (6, 9), (7, 10), (8, 9), (9, 8), (10, 7), (11, 6)):
                g.set(x, y, OCC)
        if kind == "locked":
            g.rect(6, 7, 9, 10, T("stone", "deep"))
            g.set(6, 6, T("stone", "deep"))
            g.set(9, 6, T("stone", "deep"))
            g.rect(6, 5, 9, 5, T("stone", "deep"))
    elif kind == "rush":
        # The seat: a dark crown on a lit plinth.
        for y in range(16):
            for x in range(16):
                d = abs(x - 7.5) + abs(y - 7.5)
                if d <= 7.6:
                    g.set(x, y, OCC)
                if d <= 6.2:
                    g.set(x, y, E["darkness"][3])
        for x in range(4, 12):
            g.set(x, 10, E["darkness"][1])
            g.set(x, 9, E["darkness"][2])
        for x in (4, 7, 8, 11):
            g.set(x, 6, E["darkness"][1])
            g.set(x, 7, E["darkness"][2])
            g.set(x, 8, E["darkness"][2])
    elif kind == "page":
        g.rect(2, 1, 13, 14, OCC)
        g.rect(3, 2, 12, 13, T("skin", "lit"))
        for y in range(3, 13, 3):
            g.rect(4, y, 11, y, T("wool", "lit"))
        g.rect(5, 2, 5, 13, T("red", "hi"))
        g.set(12, 13, T("skin", "base"))
        g.set(11, 13, T("skin", "base"))
        g.set(12, 12, T("skin", "base"))
    return g


def secret_icon(keepsake):
    """The card's icon for a stage secret (a warm glint, the tell of section 2 of stage-secrets.md)
    or a keepsake (a small locket on its chain). 32x32."""
    g = Grid(32, 32)
    if not keepsake:
        cx, cy = 15.5, 15.5
        for y in range(32):
            for x in range(32):
                dx = abs(x - cx)
                dy = abs(y - cy)
                # A four-point glint.
                if (dx < 1.6 and dy < 12) or (dy < 1.6 and dx < 12) or (dx + dy < 5.5):
                    g.set(x, y, OCC)
                if (dx < 0.6 and dy < 10.5) or (dy < 0.6 and dx < 10.5) or (dx + dy < 4):
                    g.set(x, y, E["light"][3] if dx + dy > 6 else E["light"][2])
                if dx + dy < 2.2:
                    g.set(x, y, E["light"][0])
        for (x, y) in ((7, 7), (24, 7), (7, 24), (24, 24)):
            g.set(x, y, E["light"][3])
    else:
        # The chain, then the locket: gold, lit from the upper left, a stone set in it.
        for k in range(10):
            a = math.pi * (0.15 + 0.7 * k / 9.0)
            x = 16 + math.cos(a) * 11
            y = 12 - math.sin(a) * 9
            g.set(x, y, T("gold", "base") if k % 2 else T("gold", "shade"))
        g.disc(16, 20, 8, 8, OCC)
        g.disc(16, 20, 7, 7, T("gold", "shade"))
        for y in range(13, 28):
            for x in range(9, 24):
                nx = (x - 16) / 7.0
                ny = (y - 20) / 7.0
                if nx * nx + ny * ny <= 1 and -(nx + ny) > 0.2:
                    g.set(x, y, T("gold", "base"))
                if nx * nx + ny * ny <= 1 and -(nx + ny) > 0.9:
                    g.set(x, y, T("gold", "lit"))
        g.disc(16, 20, 3, 3, T("red", "lit"))
        g.set(15, 19, T("red", "hi"))
        g.set(16, 11, T("gold", "lit"))
        g.rect(15, 11, 17, 13, T("gold", "base"))
    return g


def arrow(direction):
    """The step plates' chevron, 12x20."""
    g = Grid(12, 20)
    for y in range(20):
        t = abs(y - 9.5) / 9.5
        for x in range(12):
            u = x if direction == "right" else 11 - x
            edge = 2 + (1 - t) * 7
            if edge - 3.2 <= u <= edge:
                lit = y < 10
                g.set(x, y, T("gold", "lit") if lit else T("gold", "base"))
            elif edge < u <= edge + 1.2:
                g.set(x, y, OCC)
    return g


# ---------------------------------------------------------------------------------------------
# OUTPUT
# ---------------------------------------------------------------------------------------------
def save(g, name, scale=1):
    img = g.image()
    if scale != 1:
        img = img.resize((g.w * scale, g.h * scale), Image.NEAREST)
    path = os.path.join(OUT_DIR, name)
    img.save(path)
    return path


def audit(g, name):
    ok = raster.allowed()
    bad = {}
    for p in g.pix:
        if p[3] == 0:
            continue
        h = "#%02X%02X%02X" % (p[0], p[1], p[2])
        if h not in ok:
            bad[h] = bad.get(h, 0) + 1
    if bad:
        print("  %s: %d off-contract colours: %s" % (name, len(bad), sorted(bad.items(), key=lambda kv: -kv[1])[:6]))
        sys.exit(1)


def arc_lengths(points):
    out = [0.0]
    for i in range(1, len(points)):
        out.append(out[-1] + math.hypot(points[i][0] - points[i - 1][0], points[i][1] - points[i - 1][1]))
    return out


def write_json():
    stops = []
    for (sid, stand, focus, rx, ry) in PLACES:
        stops.append({
            "id": sid,
            "route_index": ROUTE_STOPS[sid],
            "stand": [stand[0], stand[1]],
            "focus": [focus[0], focus[1]],
            "shroud": [rx, ry],
            "shroud_image": ("shroud-%s.png" % sid) if rx > 0 else "",
            "on_map": stand[0] < W,
        })
    data = {
        "_generated_by": "tools/art/world_map.py - do not edit by hand; edit the generator and re-run",
        "size": [W, H],
        "route": [[round(p[0], 1), round(p[1], 1)] for p in ROUTE],
        "stops": stops,
        "page": {
            "image": "sketchbook.png",
            "origin": [PAGE_ORIGIN[0], PAGE_ORIGIN[1]],
            "rect": [PAGE_ORIGIN[0] + PAGE_RECT[0], PAGE_ORIGIN[1] + PAGE_RECT[1],
                     PAGE_ORIGIN[0] + PAGE_RECT[2], PAGE_ORIGIN[1] + PAGE_RECT[3]],
            "drawings": [{"texture": f, "at": [p[0], p[1]]} for (f, p) in PAGE_DRAWINGS],
        },
    }
    # One key per line and one stop per line, the route on one line of its own: readable in a diff
    # where it matters (the stops) and not 2500 lines of coordinates where it does not.
    lines = ["{"]
    keys = sorted(data.keys())
    for i, k in enumerate(keys):
        tail = "," if i < len(keys) - 1 else ""
        v = data[k]
        if k == "stops":
            lines.append(' "stops": [')
            for j, s in enumerate(v):
                lines.append("  " + json.dumps(s, sort_keys=True) + ("," if j < len(v) - 1 else ""))
            lines.append(" ]" + tail)
        else:
            lines.append(" " + json.dumps(k) + ": " + json.dumps(v, sort_keys=True, separators=(",", ":")) + tail)
    lines.append("}")
    with open(os.path.join(OUT_DIR, "worldmap.json"), "w") as fh:
        fh.write("\n".join(lines) + "\n")


def check_places():
    """The wizard waits at the door, never inside the fog: every stand point outside its shroud."""
    ok = True
    for (sid, stand, focus, rx, ry) in PLACES:
        if rx <= 0:
            continue
        d = ((stand[0] - focus[0]) / float(rx)) ** 2 + ((stand[1] - focus[1]) / float(ry)) ** 2
        if d < 0.8:
            print("  %s: the stand point is inside the shroud (%.2f)" % (sid, d))
            ok = False
    if not ok:
        sys.exit(1)


def main():
    check_places()
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    print("world_map: land")
    # Decided on a 2-pixel grid: the nearest-seed search is the slow part, and a border that steps
    # in pairs of pixels is still a border drawn at the art's own grain.
    biomes = [None] * (W * H)
    others = [None] * (W * H)
    mixes = [0.0] * (W * H)
    for y in range(0, H, 2):
        for x in range(0, W, 2):
            b, o, m = biome_pair(x, y)
            for yy in (y, y + 1):
                for xx in (x, x + 1):
                    if xx < W and yy < H:
                        biomes[yy * W + xx] = b
                        others[yy * W + xx] = o
                        mixes[yy * W + xx] = m
    # The sea's line is cheap, so it is exact.
    for y in range(H):
        for x in range(W):
            if is_sea(x, y):
                biomes[y * W + x] = "sea"
                mixes[y * W + x] = 0.0
    g = Grid(W, H, OCC)
    paint_ground(g, biomes, others, mixes)
    shore(g, biomes)
    print("world_map: water")
    draw_water_path(g, STREAM, 2, 3)
    draw_water_path(g, RIVER, 4, 8)
    # Where the river comes up: a spring pool under the scarp, the cave's water returning.
    lake(g, 758, 468, 8, 4, 99)
    print("world_map: mountains")
    mountains(g, biomes)
    groves(g, biomes)
    print("world_map: road")
    draw_road(g, biomes)
    print("world_map: places")
    scatter_forest(g, biomes)
    elderbark(g)
    fields(g, biomes)
    broken_circle(g, biomes)
    cave(g)
    keep(g)
    swamp(g, biomes)
    ruins(g, biomes)
    frozen(g, biomes)
    sands(g, biomes)
    emberdeep(g, biomes)
    edge_falloff(g)
    audit(g, "world")
    save(g, "world.png")

    p = page()
    audit(p, "sketchbook")
    save(p, "sketchbook.png")

    for (sid, stand, focus, rx, ry) in PLACES:
        if rx <= 0:
            continue
        s = shroud(rx, ry, 1200 + len(sid) * 31 + int(focus[0]))
        audit(s, "shroud-" + sid)
        save(s, "shroud-%s.png" % sid)
    save(shadow(), "shadow.png")
    for state in ("open", "locked", "current"):
        save(ring(state), "ring-%s.png" % state)
    for kind in ("open", "cleared", "locked", "rush", "page"):
        m = mark(kind)
        audit(m, "mark-" + kind)
        save(m, "mark-%s.png" % kind, 2)
    for keep_ in (False, True):
        icon = secret_icon(keep_)
        audit(icon, "secret")
        save(icon, "keepsake.png" if keep_ else "secret.png", 2)
    for d in ("left", "right"):
        save(arrow(d), "arrow-%s.png" % d, 2)
    write_json()

    if not os.path.isdir(PREVIEW_DIR):
        os.makedirs(PREVIEW_DIR)
    # A preview with the road's stops ringed, for checking placement by eye (gitignored dir).
    prev = g.image().resize((W, H), Image.NEAREST)
    prev.save(os.path.join(PREVIEW_DIR, "world-1x.png"))
    print("world_map: %d route points, %d stops" % (len(ROUTE), len(PLACES)))


if __name__ == "__main__":
    main()
