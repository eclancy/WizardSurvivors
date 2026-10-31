# -*- coding: utf-8 -*-
"""The story-mode prologue: six illustrated panels, drawn by code on the Bonelight contract.

    python tools/art/prologue.py              # every panel, written to assets/bonelight/ui/prologue/
    python tools/art/prologue.py moot throne  # only these panels
    python tools/art/prologue.py --preview    # also write tools/art/_prologue/<panel>.png

Runs under Python 2.7 and 3 (PIL only). Every random stream draws from rng.random(), never
randrange - Python 3 changed randrange, and a generator whose trees move when the interpreter
changes cannot be regenerated to fix one detail (.ai/title-screen.md, "The eyes are spread").

See .ai/prologue.md for what each panel shows and why; scripts/Prologue.cs plays them.

THE FRAME. Every layer is 960x840, drawn at a whole x2 into a 1920x1680 rect centred on the
canvas, the same move the title screen makes in each direction at once:

  - a PC canvas is 720 tall (keep_height), so it shows rows 240-600 and, at 16:9, columns
    160-800 - wider monitors more of the 960;
  - a phone canvas is 720 wide (keep_width), so it shows columns 300-660 and, at 16:9, rows
    100-740 - a Pixel 9 rows 16-824.

The 360x360 square both of them always show (columns 300-660, rows 240-600) holds every subject.
Around it the scene carries on - sky above and ground below for the phone, land either side for
the PC - and falls to occlusion toward the frame edge, so a screen wider or taller than the frame
meets the scene's black ground invisibly. Nothing is ever stretched to fit.

LAYERS. Each panel is back (opaque) + mid + fore (transparent), so Prologue.cs can drift them at
different rates. The drift is a few art pixels over the whole panel; every layer is drawn out to
the frame edge so a drift never uncovers anything.
"""

from __future__ import division, print_function

import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image

import bonelight as bl
import hero
import raster
import splashkit as sk

M = bl.MATERIALS
E = bl.ELEMENTS
OCC = bl.OCC
RIM_SOFT = bl.RIM_SOFT
S, V, W, K, G = M["stone"], M["violet"], M["wool"], M["skin"], M["gold"]
ST, L, LI, R, AR = M["steel"], M["linen"], M["lichen"], M["red"], M["arcane"]
DK, LT = E["darkness"], E["light"]

FW, FH = 960, 840
CX, CY = FW // 2, FH // 2
# What each canvas shows, in frame pixels (x0, y0, x1, y1). The tall view is the Pixel 9's.
VIEW_WIDE = (160, 240, 800, 600)
VIEW_TALL = (300, 16, 660, 824)
SAFE = (300, 240, 660, 600)

ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "assets", "bonelight", "ui", "prologue")
PREVIEW = os.path.join(HERE, "_prologue")

TH = [[raster.DITHER[y][x] / 64.0 for x in range(8)] for y in range(8)]

_RGB = {}


def rgb(c):
    if c is None or not isinstance(c, str):
        return c
    v = _RGB.get(c)
    if v is None:
        v = _RGB[c] = raster.rgb(c)
    return v


def lum(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


class Cv(raster.Canvas):
    """raster.Canvas with the colour parse cached. The frames here are 806,400 pixels a layer,
    and raster's set() re-parses a hex string on every write."""

    def set(self, x, y, c):
        if c is None:
            return
        x = int(x)
        y = int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = rgb(c)


def frame(opaque=False, w=FW, h=FH):
    return Cv(w, h, OCC if opaque else None)


def over(dst, src, x=0, y=0):
    """Composite src onto dst: opaque src pixels replace, transparent ones leave dst alone."""
    dst.img.paste(src.img, (int(x), int(y)), src.img)
    dst.px = dst.img.load()


def ri(rng, a, b):
    """randrange(a, b) from rng.random(), which is the same stream under Python 2 and 3."""
    return a + int(rng.random() * (b - a))


def pick(rng, seq):
    return seq[min(len(seq) - 1, int(rng.random() * len(seq)))]


def dith(x, y, f):
    return f > TH[y % 8][x % 8]


# --------------------------------------------------------------------------- light passes
def darken(c, t_of, colors, box=None):
    """Darken every opaque pixel through colors by a per-pixel t (splash._darken's indexing:
    None leaves a pixel alone, the last entry is reached at t = len(colors)). Only ever darker."""
    cols = [rgb(k) for k in colors]
    lums = [lum(k) if k else None for k in cols]
    n = len(cols)
    x0, y0, x1, y1 = box or (0, 0, c.w, c.h)
    px = c.px
    for y in range(max(0, y0), min(c.h, y1)):
        row = TH[y % 8]
        for x in range(max(0, x0), min(c.w, x1)):
            cur = px[x, y]
            if not cur[3]:
                continue
            t = t_of(x, y)
            if t <= 0:
                continue
            i = min(n - 1, int(t))
            idx = i if (t - i) > row[x % 8] else i - 1
            if idx >= 0 and cols[idx] is not None and lums[idx] < lum(cur):
                px[x, y] = cols[idx]


def frame_t(vig=1.0, cy=CY + 20):
    """The scene vignette plus the fall to occlusion beyond what any 16:9 screen shows.

    Inside the views it is a gentle radial darkening (the title's power-2.2 shape); past the
    16:9 edges it climbs to occlusion at the frame edge, sideways for an ultrawide monitor and
    up and down for a tall phone."""
    def t_of(x, y):
        dx = (x - CX) / 430.0
        dy = (y - cy) / 430.0
        t = (math.sqrt(dx * dx + dy * dy)) ** 2.2 * 1.6 * vig
        ex = abs(x - CX) - 296
        if ex > 0:
            t = max(t, (ex / 184.0) ** 1.15 * 2.2)
        ey = abs(y - CY) - 300
        if ey > 0:
            t = max(t, (ey / 120.0) ** 1.15 * 2.2)
        return t
    return t_of


def finish(layers, vig=1.0, cy=CY + 20):
    t_of = frame_t(vig, cy)
    for name in layers:
        darken(layers[name], t_of, [S[4], OCC])


def edge_light(c, color, toward, skip_below=None, depth=1, inner=None, box=None):
    """Light the silhouette edge of everything on c that faces `toward` (a unit-ish vector
    pointing at the light). A pixel is lit when the pixel one step toward the light is empty.
    `inner` colours the second pixel in. Run on a transparent scratch layer only: on an
    opaque canvas every pixel has a neighbour and nothing is lit (.ai/title-screen.md, bug 2)."""
    dx = 1 if toward[0] > 0.33 else (-1 if toward[0] < -0.33 else 0)
    dy = 1 if toward[1] > 0.33 else (-1 if toward[1] < -0.33 else 0)
    px = c.px
    hits = []
    bx0, by0, bx1, by1 = box or (1, 1, c.w - 1, c.h - 1)
    for y in range(max(1, int(by0)), min(c.h - 1, int(by1))):
        if skip_below is not None and y > skip_below:
            break
        for x in range(max(1, int(bx0)), min(c.w - 1, int(bx1))):
            if px[x, y][3] and not px[x + dx, y + dy][3]:
                hits.append((x, y))
    for (x, y) in hits:
        c.set(x, y, color)
        if inner:
            c.set(x - dx, y - dy, inner)


# --------------------------------------------------------------------------- land
def sky(c, y0, y1, stops):
    c.vramp(y0, y1, stops)


def stars(c, box, n, seed, colors=None):
    colors = colors or [K[0], K[1], K[2], AR[1]]
    rng = random.Random(seed)
    x0, y0, x1, y1 = box
    for _ in range(n):
        x = x0 + rng.random() * (x1 - x0)
        y = y0 + rng.random() ** 1.3 * (y1 - y0)
        col = pick(rng, colors)
        c.set(x, y, col)
        if rng.random() < 0.06:                         # a few brighter ones carry a cross
            for (ox, oy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                c.set(x + ox, y + oy, S[0])


def ridge_fn(y_base, amp, seed, freq=1.0):
    rng = random.Random(seed)
    ph = [rng.random() * 6.283 for _ in range(4)]

    def h(x):
        return y_base - amp * (0.52 * math.sin(x * 0.0105 * freq + ph[0])
                               + 0.30 * math.sin(x * 0.026 * freq + ph[1])
                               + 0.12 * math.sin(x * 0.067 * freq + ph[2])
                               + 0.06 * math.sin(x * 0.173 * freq + ph[3]))
    return h


def hills(c, h, stops, depth, lit=None, y_end=FH):
    """Fill everything below the ridge line h(x) with a dithered ramp `depth` rows deep, the
    last stop carried on to y_end. `lit` marks the crest where the slope faces the key."""
    n = len(stops)
    for x in range(c.w):
        top = int(round(h(x)))
        for y in range(max(0, top), min(c.h, y_end)):
            t = min(1.0, (y - top) / float(depth)) * (n - 1)
            i = min(n - 2, int(t))
            c.set(x, y, stops[i + 1] if dith(x, y, t - i) else stops[i])
        if lit and h(x + 1) < h(x - 1) - 0.25:
            c.set(x, top, lit)


def tuft(c, x, yb, h, rng, dark, light=None, blades=4):
    """A heather or grass tuft from one root. sk.grass draws the same thing but spends
    randrange; this one keeps to random()."""
    n = blades + ri(rng, 0, 3)
    for i in range(n):
        t = (i / float(max(1, n - 1))) * 2.0 - 1.0
        tx = x + t * h * 0.55 + (rng.random() - 0.5) * h * 0.2
        ty = yb - h * (0.5 + rng.random() * 0.55)
        c.line(x + t * h * 0.12, yb, tx, ty, dark)
    if light:
        c.line(x, yb, x + (rng.random() - 0.5) * h * 0.5, yb - h * (0.6 + rng.random() * 0.4), light)


def heath(c, y0, y1, n, seed, tones, box=None, keep_out=None, scale=1.0):
    """Heather in clumps, small and dense at the horizon, larger toward the camera. tones is
    [(dark, top), ...] picked per clump; dark sits under, top catches what light there is."""
    rng = random.Random(seed)
    x0, x1 = (box[0], box[1]) if box else (0, FW)
    for _ in range(n):
        d = rng.random() ** 1.4
        y = y0 + d * (y1 - y0)
        x = x0 + rng.random() * (x1 - x0)
        if keep_out and keep_out(x, y):
            continue
        r = (1.0 + d * 5.5 * rng.random()) * scale
        dark, top = pick(rng, tones)
        c.disc(x, y, r * 1.7, r * 0.8, dark)
        c.disc(x - r * 0.4, y - r * 0.35, r * 1.0, r * 0.45, top)


def standing_stone(c, x, yb, w, h, seed, lean=0.0, ramp=None, warm=None, toward=None,
                   broken=False):
    """A standing stone on its own scratch layer, composited onto c.

    Shaded by the key from the upper left on the stone ramp, cracked and spotted with lichen.
    warm=(ramp, strength) washes the face toward a fire in pigment (never the element ramp:
    what the fire lands on is stone); toward=(dx, dy) puts a warm edge on the side facing it."""
    rng = random.Random(seed)
    ramp = ramp or S
    pad = int(w + h * abs(lean) + 8)
    s = Cv(pad * 2 + int(w) * 2, int(h) + 12)
    ox, oy = pad + w, int(h) + 4
    j = lambda a: (rng.random() - 0.5) * a
    top_h = h * (0.55 + rng.random() * 0.2) if broken else h
    # a slanted, uneven crown: weathered stone, not a dressed headstone
    tl, tr = 0.80 + rng.random() * 0.14, 0.72 + rng.random() * 0.22
    peak = -0.25 + rng.random() * 0.5
    pts = [(ox - w * 0.5, oy),
           (ox - w * 0.52 + j(w * 0.1) + lean * top_h * 0.45, oy - top_h * 0.55),
           (ox - w * 0.46 + j(w * 0.1) + lean * top_h, oy - top_h * tl),
           (ox + w * peak + lean * top_h, oy - top_h - j(3)),
           (ox + w * 0.30 + j(w * 0.15) + lean * top_h, oy - top_h * tr),
           (ox + w * 0.46 + j(w * 0.1) + lean * top_h * 0.8, oy - top_h * 0.70),
           (ox + w * 0.52 + lean * top_h * 0.3, oy - top_h * 0.30),
           (ox + w * 0.5, oy)]
    if broken:                                        # a snapped top: a jagged cut, not a dome
        pts[3] = (ox - w * 0.05 + lean * top_h, oy - top_h * 0.82)
        pts[4] = (ox + w * 0.18 + lean * top_h, oy - top_h * 1.02)
    s.poly_shade(pts, [ramp[1], ramp[2], ramp[3], ramp[4]], ang=0.62, gamma=0.85)
    # the shadowed flank: a band down the right side, one step darker again
    for yy in range(int(oy - top_h), oy + 1):
        row = [xx for xx in range(s.w) if s.px[xx, yy][3]]
        if not row:
            continue
        for xx in range(max(row[0], row[-1] - int(w * 0.18)), row[-1] + 1):
            s.set(xx, yy, ramp[4] if (xx + yy) % 3 else OCC)
    # cracks and lichen
    for _ in range(int(h / 18) + 1):
        cy = oy - rng.random() * top_h * 0.85
        cx0 = ox - w * 0.4 + rng.random() * w * 0.3
        s.line(cx0, cy, cx0 + w * (0.25 + rng.random() * 0.35), cy + j(4), OCC)
    for _ in range(int(w * h / 90)):
        lx = ox - w * 0.45 + rng.random() * w * 0.6
        ly = oy - rng.random() * top_h * 0.8
        if s.get(int(lx), int(ly))[3]:
            s.set(lx, ly, LI[2] if rng.random() < 0.6 else LI[3])
    if warm:
        wr, strength = warm
        for yy in range(s.h):
            f = strength * (0.35 + 0.65 * ((yy - (oy - top_h)) / float(top_h)))
            for xx in range(s.w):
                if s.px[xx, yy][3] and dith(xx, yy, f):
                    s.set(xx, yy, wr[3] if dith(xx + 3, yy + 1, f * 0.5) else wr[4])
    if toward:
        edge_light(s, (toward[2] if len(toward) > 2 else G[2]), toward)
    s.hline(ox - w * 0.55, ox + w * 0.55, oy, OCC)
    s.hline(ox - w * 0.45, ox + w * 0.45, oy + 1, OCC)
    over(c, s, x - ox, yb - oy)


# --------------------------------------------------------------------------- people
def wizard_figure(c, cx, yb, h, body, seed, staff=0, orb=None, warm=None, toward=None,
                  bound=False, hat_lean=0.0, dim_orb=False):
    """A wizard of the land, far off: robe, brimmed hat, staff. Deliberately anonymous - no face,
    no beard, no colour of his own - because the rescued wizards are people whose names are the
    reward (.ai/side-events.md, "Secret wizards") and a portrait here would give them away.

    staff: -1 left, +1 right, 0 none. orb: an element ramp lit on the staff head. bound: hands
    together in front and no staff - a prisoner."""
    rng = random.Random(seed)
    u = h / 30.0
    s = Cv(int(26 * u) + 8, int(34 * u) + 8)
    ox, oy = s.w // 2, int(31 * u) + 4
    j = lambda a: (rng.random() - 0.5) * a * u
    hem = [(ox - 5.2 * u + j(0.6), oy), (ox - 2.4 * u, oy + 0.4 * u), (ox + 1.0 * u, oy - 0.2 * u),
           (ox + 5.0 * u + j(0.6), oy)]
    robe = hem[::-1] + [(ox + 2.7 * u, oy - 15 * u), (ox + 3.2 * u, oy - 17 * u),
                        (ox - 3.2 * u, oy - 17 * u), (ox - 2.7 * u, oy - 15 * u)]
    s.poly(robe, body[0])
    s.poly([(ox + 0.6 * u, oy - 15.5 * u), (ox + 2.7 * u, oy - 15 * u), (ox + 5.0 * u, oy),
            (ox + 1.6 * u, oy)], body[1])
    for k in (-1.4, 0.4, 2.2):
        s.line(ox + k * u * 0.6, oy - 13 * u, ox + k * u * 1.3, oy - 0.5 * u, body[1])
    s.disc(ox, oy - 18.6 * u, 1.7 * u, 1.9 * u, body[1])                    # head under the brim
    s.poly([(ox - 5.4 * u, oy - 20.4 * u), (ox + 5.4 * u, oy - 20.6 * u),
            (ox + 3.2 * u, oy - 21.8 * u), (ox - 3.2 * u, oy - 21.6 * u)], body[0])
    tip = (ox + (1.6 + hat_lean) * u, oy - 30.5 * u)
    s.poly([(ox - 2.8 * u, oy - 21.5 * u), (ox + 2.6 * u, oy - 21.7 * u),
            (ox + 1.4 * u + hat_lean * 0.5 * u, oy - 26 * u), tip,
            (ox - 0.4 * u + hat_lean * 0.4 * u, oy - 26.5 * u)], body[0])
    if bound:
        s.poly([(ox - 2.4 * u, oy - 16 * u), (ox + 2.6 * u, oy - 16 * u),
                (ox + 1.6 * u, oy - 10 * u), (ox - 1.2 * u, oy - 10 * u)], body[1])
        s.disc(ox + 0.2 * u, oy - 9.6 * u, 1.3 * u, 0.9 * u, body[0])
    elif staff:
        sx = ox + staff * 5.6 * u
        s.poly([(ox + staff * 2.2 * u, oy - 16.4 * u), (sx, oy - 14.6 * u),
                (sx + staff * 0.6 * u, oy - 12.6 * u), (ox + staff * 2.6 * u, oy - 12 * u)], body[0])
        for i in range(max(1, int(round(0.7 * u)))):
            s.vline(int(sx) + i, oy - 27 * u, oy, body[0])
    if toward:
        edge_light(s, toward[2] if len(toward) > 2 else (warm[2] if warm else G[2]), toward)
    if warm:
        wr = warm
        for y in range(s.h):
            for x in range(s.w):
                p = s.px[x, y]
                if p[3] and p != rgb(wr[2]) and dith(x, y, 0.22):
                    s.set(x, y, wr[4])
    s.hline(ox - 5.4 * u, ox + 5.2 * u, oy, OCC)
    over(c, s, cx - ox, yb - oy)
    if orb and staff and not bound:
        sx = cx + staff * 5.6 * u + 0.35 * u
        oy2 = yb - 27.6 * u
        if dim_orb:
            c.disc(sx, oy2, 1.4 * u, 1.4 * u, ST[3])
            c.set(sx - 0.4 * u, oy2 - 0.4 * u, ST[2])
        else:
            c.radial(sx, oy2, 4.4 * u, 4.4 * u, [orb[3], orb[3], None])
            c.radial(sx, oy2, 1.6 * u, 1.6 * u, [orb[0], orb[1], orb[2], orb[3]])


EYE = (AR[0], AR[1])


def creature(c, kind, cx, yb, h, facing=1, body=None, rim=None, eye=True, seed=0):
    """One of his, as a silhouette: the taxonomy's class attachment survives the solid fill
    (.ai/art-direction.md section 4) - the swarmer's cowl, the runner's streaming rag, the
    bruiser's horns and pauldrons, the shielder's flat plane, the caster's loose orb, the
    knight's spear - and every one wears the same mark, one or two pixels of cold arcane, the
    only light on anything he raised (.ai/world-and-tone.md). Limbs are strokes, so a large one
    has joints and weight and does not read as a box with horns on it."""
    body = body or [OCC, OCC]
    u = h / 20.0
    rng = random.Random(seed)
    s = Cv(int(34 * u) + 8, int(28 * u) + 8)
    ox, oy = s.w // 2, int(25 * u) + 3
    f = facing
    b0, b1 = body
    j = lambda a: (rng.random() - 0.5) * a

    def P(pts):
        return [(ox + f * px_ * u, oy + py_ * u) for (px_, py_) in pts]

    def limb(a, b, a2, b2, r0, r1, col=None):
        sk._stroke(s, ox + f * a * u, oy + b * u, ox + f * a2 * u, oy + b2 * u,
                   max(0.5, r0 * u), max(0.5, r1 * u), col or b0)
    stride = j(2.4)
    eyes = []
    if kind == "swarmer":
        s.poly(P([(-4.2, 0), (-2.6, -0.8), (-1.0, 0.2), (1.2, -0.6), (3.6, 0), (4.0, -6.0), (3.4, -11.0),
                  (2.2, -14.6), (3.4, -17.6), (1.0, -19.6), (-1.6, -17.2), (-3.4, -12.6), (-4.4, -6.6)]), b0)
        s.poly(P([(2.2, -14.6), (3.4, -17.6), (1.6, -16.4)]), b1)
        limb(2.6, -9.0, 5.4 + j(1), -5.0, 0.7, 0.5)
        eyes = [(1.8, -15.2)]
    elif kind == "runner":
        limb(-0.6, -7.6, -3.4 + stride, 0, 1.0, 0.7)
        limb(0.8, -7.6, 3.6 - stride, 0, 1.0, 0.7)
        s.poly(P([(-2.4, -7.0), (2.2, -7.6), (4.6, -12.8), (3.8, -15.6), (0.8, -15.0), (-2.0, -11.4)]), b0)
        s.disc(ox + f * 4.4 * u, oy - 15.6 * u, 1.9 * u, 1.7 * u, b0)
        s.poly(P([(-1.4, -13.8), (0.6, -14.6), (-6.0, -12.6), (-11.6, -14.0), (-8.6, -10.8), (-12.4, -9.4),
                  (-6.4, -9.0), (-1.8, -10.4)]), b1)
        limb(3.0, -13.0, 6.6, -9.6, 0.7, 0.5)
        eyes = [(5.2, -15.9)]
    elif kind == "bruiser":
        limb(-2.2, -7.4, -3.4 - stride * 0.4, 0, 1.6, 1.3)
        limb(2.2, -7.4, 3.4 + stride * 0.4, 0, 1.6, 1.3)
        s.poly(P([(-3.8, -6.4), (3.8, -6.4), (6.0, -12.6), (5.0, -16.2), (-5.4, -16.2), (-6.2, -12.4)]), b0)
        for sg in (-1, 1):
            s.disc(ox + f * sg * 5.2 * u, oy - 15.0 * u, 2.6 * u, 2.0 * u, b1)
            limb(sg * 6.0, -14.4, sg * 7.0, -8.0, 1.2, 1.0)
            s.disc(ox + f * sg * 7.0 * u, oy - 7.6 * u, 1.3 * u, 1.3 * u, b0)
        s.disc(ox + f * 0.8 * u, oy - 17.8 * u, 2.2 * u, 2.2 * u, b0)
        for sg in (-1, 1):
            pts = [(0.8 + sg * 1.6, -18.8), (0.8 + sg * 3.6, -20.6), (0.8 + sg * 4.0, -23.6), (0.8 + sg * 2.8, -21.2),
                   (0.8 + sg * 1.0, -19.8)]
            s.poly(P(pts), b0)
        eyes = [(1.6, -18.0), (2.6, -18.0)]
    elif kind == "shielder":
        limb(-1.4, -7.0, -2.6 - stride * 0.4, 0, 1.1, 0.9)
        limb(1.6, -7.0, 2.6 + stride * 0.4, 0, 1.1, 0.9)
        s.poly(P([(-3.2, -6.4), (3.0, -6.4), (3.6, -13.6), (2.2, -15.6), (-2.6, -15.4), (-3.6, -12.4)]), b0)
        s.disc(ox + f * 0.6 * u, oy - 17.4 * u, 2.0 * u, 2.1 * u, b0)
        s.poly(P([(0.6, -19.2), (2.8, -18.4), (0.0, -16.6)]), b0)
        s.poly(P([(3.6, -16.4), (8.6, -17.0), (8.4, -2.2), (3.8, -1.6)]), b1)      # the plane
        s.line(ox + f * 3.6 * u, oy - 16.4 * u, ox + f * 3.8 * u, oy - 1.6 * u, b0)
        eyes = [(1.6, -17.6)]
    elif kind == "caster":
        s.poly(P([(-3.8, 0), (-1.6, -0.6), (0.6, 0.2), (3.6, 0), (2.6, -9), (2.4, -14.6), (1.4, -19.6),
                  (-0.4, -23.4), (-1.6, -20.6), (-2.6, -14.0), (-3.0, -8.0)]), b0)
        limb(1.8, -15.0, 4.6, -18.6, 0.7, 0.5)
        orb_x, orb_y = ox + f * 6.0 * u, oy - 22.6 * u
        s.disc(orb_x, orb_y, 1.5 * u, 1.5 * u, AR[3])
        s.disc(orb_x - u * 0.4, orb_y - u * 0.4, 0.7 * u, 0.7 * u, AR[2])
        eyes = [(0.9, -19.0)]
    elif kind == "knight":
        limb(-1.6, -8.4, -2.4 - stride * 0.4, 0, 1.2, 1.0)
        limb(1.6, -8.4, 2.6 + stride * 0.4, 0, 1.2, 1.0)
        s.poly(P([(-3.4, -7.6), (3.4, -7.6), (4.2, -15.6), (3.0, -17.6), (-3.0, -17.6), (-4.2, -15.4)]), b0)
        s.poly(P([(-3.0, -8.6), (3.2, -8.6), (4.6, -4.6), (-4.4, -4.4)]), b1)       # tabard skirt
        s.poly(P([(-1.8, -17.4), (2.2, -17.4), (2.4, -21.4), (0.2, -23.0), (-1.8, -21.6)]), b0)
        limb(3.2, -15.6, 5.8, -11.4, 1.0, 0.8)
        s.line(ox + f * 6.0 * u, oy - 0.4 * u, ox + f * 6.4 * u, oy - 27.0 * u, b0)
        s.line(ox + f * 6.0 * u + 1, oy - 0.4 * u, ox + f * 6.4 * u + 1, oy - 27.0 * u, b0)
        s.poly(P([(6.4, -27.0), (7.4, -24.4), (6.2, -23.6), (5.4, -24.4)]), b0)
        eyes = [(1.2, -20.2)]
    elif kind == "hound":
        limb(-5.6, -6.4, -6.6 - stride * 0.5, 0, 1.0, 0.7)
        limb(-3.4, -6.4, -3.0 + stride * 0.5, 0, 1.0, 0.7)
        limb(3.6, -7.0, 2.6 + stride * 0.5, 0, 1.0, 0.7)
        limb(5.4, -7.0, 6.4 - stride * 0.5, 0, 1.0, 0.7)
        s.poly(P([(-7.4, -6.0), (-5.6, -9.4), (3.6, -10.2), (6.6, -12.0), (8.2, -10.6), (6.6, -6.6), (-5.6, -5.0)]), b0)
        s.poly(P([(6.6, -12.0), (10.6, -11.6), (11.2, -10.2), (8.2, -9.4)]), b0)
        s.poly(P([(6.8, -12.0), (7.8, -14.4), (8.2, -11.6)]), b0)
        s.poly(P([(-7.4, -6.0), (-11.6, -9.6), (-7.2, -8.0)]), b0)
        eyes = [(8.8, -11.0)]
    if rim:
        edge_light(s, rim, (-0.7, -0.7))
    if eye:
        # Small ones get the arcane row's lit stop alone: massed, its near-white hi read as a sky
        # full of stars rather than as eyes.
        small = u < 1.3
        for i, (ex, ey) in enumerate(eyes):
            s.set(ox + f * ex * u, oy + ey * u, EYE[1] if small or i else EYE[0])
            if u >= 1.6 and len(eyes) == 1:
                s.set(ox + f * ex * u + f, oy + ey * u, EYE[1])
    over(c, s, cx - ox, yb - oy)


# --------------------------------------------------------------------------- the dark wizard
def light_up(s, lx, ly, radius, ladder, power=1.4, seed=0):
    """Step pixels near a light one stop up its ladder, dithered by falloff. The light a source
    throws on cloth is the cloth's own lighter stop - pigment - never the source's ramp
    (.ai/title-screen.md: emissive ramps belong on the emitter, not on what it lights)."""
    up = dict((rgb(a), rgb(b)) for (a, b) in ladder)
    px = s.px
    for y in range(max(0, int(ly - radius)), min(s.h, int(ly + radius) + 1)):
        for x in range(max(0, int(lx - radius)), min(s.w, int(lx + radius) + 1)):
            p = px[x, y]
            if not p[3] or p not in up:
                continue
            d = math.hypot(x - lx, y - ly) / radius
            if d < 1.0 and dith(x + seed, y, (1.0 - d) ** power):
                px[x, y] = up[p]


def dark_wizard(c, cx, yb, h):
    """His first full look, and the only figure in the game allowed to SHED dark light: the
    darkness ramp as emission is his alone (.ai/world-and-tone.md, "The dark wizard is the
    exception that proves it").

    Read from the silhouette, filled black: no hat brim and no staff, which are the player's; a
    deep cowl inside a collar that stands up round his head in points, a throne's back worn as
    a coat, with a thin iron circlet over the cowl; one arm raised, its sleeve hanging off it
    like a wing, and in that hand the light he is taking. At his belt, keys. He has no feet: the
    hem goes on into the ground as shadow, and the shadow is what serves him (panel 3).

    Lit from behind by his own black sun and from the left by the orb in his hand - never by
    the key - so the rim is on the edges those face and nowhere else. A rim all the way round
    is an outline, and the contract has none."""
    u = h / 64.0
    s = Cv(int(44 * u), int(70 * u))
    ox, oy = int(23 * u), int(66 * u)

    def P(pts):
        return [(ox + a * u, oy + b * u) for (a, b) in pts]

    def L(a, b, a2, b2, col):
        s.line(ox + a * u, oy + b * u, ox + a2 * u, oy + b2 * u, col)

    # --- the collar points, behind the cowl -----------------------------------------
    for pts in ([(-7.4, -46.6), (-9.0, -55.4), (-7.0, -53.4), (-7.6, -60.6), (-5.4, -55.0),
                 (-4.8, -58.2), (-3.6, -50.0)],
                [(7.2, -46.6), (8.4, -54.2), (6.6, -52.8), (6.4, -59.4), (4.9, -54.6),
                 (4.4, -57.0), (3.4, -50.0)]):
        s.poly_shade(P(pts), [V[4], OCC], ang=1.5708, gamma=0.8)
    # --- the cloak: shoulders to the ground, the raised arm's sleeve hanging off its left -----
    hem = []
    rng = random.Random(64)
    for i in range(23):
        t = i / 22.0
        hem.append((20.4 - 40.6 * t, (1.6 if i % 2 else -0.6) + rng.random() * 0.9))
    # Sloped shoulders and sides that bow outward to the hem: drawn as straight lines from a flat
    # shoulder line it read as a cabinet with a head on it.
    cloak = P([(-4.6, -48.4), (4.6, -48.4), (7.6, -47.2), (9.8, -45.4), (11.0, -42.6), (11.6, -38.0),
               (12.4, -30.0), (13.8, -20.0), (15.8, -10.6), (18.2, -4.4), (20.0, -1.4)] + hem +
              [(-19.0, -1.8), (-16.8, -6.8), (-14.8, -14.6), (-13.2, -23.4), (-12.0, -31.4),
               (-11.2, -38.6), (-10.6, -42.6), (-9.4, -45.4), (-7.4, -47.2)])
    s.poly_shade(cloak, [V[3], V[4], V[4], OCC], ang=0.95, gamma=0.72)
    # The sleeve of the raised arm: a wing of cloth hanging off the forearm, with clear air
    # between it and his side. Joined to the body it read as a stick with a sliver beside it.
    sleeve = P([(-9.6, -46.4), (-12.8, -48.8), (-16.6, -51.8), (-19.6, -53.0), (-20.6, -49.0),
                (-20.4, -43.0), (-19.0, -36.8), (-17.2, -32.6), (-16.4, -36.6), (-15.0, -41.4),
                (-12.6, -44.4), (-10.4, -44.0)])
    s.poly_shade(sleeve, [V[3], V[4], OCC], ang=0.4, gamma=0.8)
    for (a2, b2) in [(-19.6, -40), (-18.2, -35.4), (-16.0, -40.6)]:
        s.line(ox - 19.4 * u, oy - 51.8 * u, ox + a2 * u, oy + b2 * u, V[3])
        s.line(ox - 19.0 * u, oy - 51.4 * u, ox + (a2 + 0.6) * u, oy + b2 * u, OCC)
    s.line(ox - 12.6 * u, oy - 44.4 * u, ox - 10.6 * u, oy - 45.6 * u, OCC)
    # folds: a lit ridge with its valley beside it, fanning from the shoulders to the hem
    folds = [(-6.0, -44, -15.4, -1.6), (-3.8, -43, -9.4, 0), (-8.6, -44, -17.6, -4.4), (5.4, -43, 9.6, 0),
             (7.8, -42, 14.6, -1.0), (9.6, -38, 18.0, -2.6), (-2.6, -42, -4.8, 0.4)]
    for (a, b, a2, b2) in folds:
        L(a, b, a2, b2, V[3] if a < 0 else V[4])
        L(a + 0.45, b, a2 + 0.8, b2, OCC)
    # --- the robe under it, in the opening: darker, with a dull gold edge down each side ---
    robe = P([(-1.8, -47.6), (2.0, -47.6), (2.8, -36), (4.2, -20), (6.6, 0.6), (-4.8, 0.6),
              (-3.2, -20), (-2.4, -36)])
    s.poly_shade(robe, [V[4], OCC, OCC], ang=0.8, gamma=0.9)
    for edge in ([(-1.8, -47.2), (-2.4, -36), (-3.2, -20), (-4.8, 0.4)],
                 [(2.0, -47.2), (2.8, -36), (4.2, -20), (6.6, 0.4)]):
        for (a, b), (a2, b2) in zip(edge, edge[1:]):
            L(a, b, a2, b2, G[4])
    L(-1.8, -46.8, -2.4, -36.0, G[3])
    for (a, a2) in [(-0.6, -1.4), (1.0, 2.2), (2.4, 4.6)]:
        L(a, -38, a2, -1, V[4])
    # belt and keys
    s.poly(P([(-3.4, -31.6), (4.0, -31.6), (4.1, -30.2), (-3.5, -30.2)]), ST[4])
    s.hline(ox - 3.4 * u, ox + 4.0 * u, oy - 31.6 * u, ST[3])
    s.rect(ox - 0.4 * u, oy - 31.8 * u, ox + 0.6 * u, oy - 30.0 * u, G[3])
    # --- the cowl, peaked and leaning back, the face set deep inside it ----------------
    hood = P([(-4.2, -47.0), (4.4, -47.0), (4.9, -52.4), (3.9, -58.0), (1.6, -61.6), (-0.6, -64.0),
              (-1.6, -62.6), (-3.8, -58.6), (-4.9, -53.0)])
    s.poly_shade(hood, [V[3], V[4], OCC], ang=0.6, gamma=0.7)
    L(-3.4, -58.4, -0.8, -63.2, V[3])
    s.disc(ox + 0.2 * u, oy - 53.8 * u, 2.5 * u, 3.5 * u, OCC)
    face = P([(-1.5, -55.8), (1.5, -55.8), (1.7, -53.4), (0.9, -50.8), (0.1, -50.0), (-0.8, -50.8),
              (-1.6, -53.4)])
    s.poly(face, K[4])
    L(-1.5, -53.4, -0.7, -50.6, K[3])                                  # the edge the orb finds
    L(-0.1, -54.4, 0.0, -52.8, K[3])                                   # the ridge of the nose
    s.hline(ox - 1.5 * u, ox + 1.6 * u, oy - 55.4 * u, OCC)            # brow, deep in shadow
    s.hline(ox - 1.4 * u, ox + 1.5 * u, oy - 55.8 * u, OCC)
    s.hline(ox - 0.5 * u, ox + 0.6 * u, oy - 51.7 * u, OCC)            # mouth
    # the cowl's shadow over the top of the face, so it reads as a face in a hood and not a mask
    for yy in range(int(oy - 56.0 * u), int(oy - 53.0 * u)):
        f = (oy - 53.0 * u - yy) / (3.0 * u)
        for xx in range(int(ox - 1.8 * u), int(ox + 1.9 * u) + 1):
            if s.px[xx, yy][3] and dith(xx, yy, 0.25 + f * 0.7):
                s.set(xx, yy, OCC)
    for ex in (-0.85, 0.75):
        s.set(ox + ex * u, oy - 54.6 * u, DK[0])
        s.set(ox + ex * u + (-1 if ex < 0 else 1), oy - 54.6 * u, DK[2])
    # the circlet: iron over the cowl, three points
    for k in range(int(7.4 * u)):
        x = ox - 3.6 * u + k
        y = oy - 57.4 * u + abs(k - 3.6 * u) * 0.12
        s.set(x, y, ST[3])
        s.set(x, y + 1, OCC)
    for (a, ht) in [(-2.2, 1.4), (0.1, 2.2), (2.2, 1.2)]:
        s.poly(P([(a - 0.55, -57.3), (a + 0.55, -57.3), (a + 0.05, -57.3 - ht)]), ST[3])
        s.set(ox + (a + 0.05) * u, oy - (57.3 + ht) * u + 1, ST[2])
    # --- hands: bone pale and long -------------------------------------------------
    hx, hy = ox - 19.4 * u, oy - 56.4 * u
    s.poly(P([(-17.6, -53.0), (-18.6, -52.4), (-19.8, -55.6), (-18.9, -56.2)]), K[4])   # wrist
    s.disc(hx, hy, 1.1 * u, 0.9 * u, K[3])
    for (a, ln) in [(-2.5, 2.8), (-2.05, 3.4), (-1.6, 3.5), (-1.15, 3.0), (-0.55, 2.0)]:
        x1, y1 = hx + math.cos(a) * ln * u, hy + math.sin(a) * ln * u
        s.line(hx, hy, x1, y1, K[3])
        s.set(x1, y1, K[2])
    # the other hand, out of the cloak at his side, long fingers round a ring of keys
    kx, ky = ox + 7.4 * u, oy - 27.4 * u
    s.poly(P([(6.6, -28.0), (8.6, -28.0), (8.8, -26.2), (6.6, -26.0)]), K[4])
    for (dx, ln) in [(-0.7, 2.6), (0.2, 3.2), (1.0, 2.8)]:
        s.line(kx + dx * u, ky + 1.4 * u, kx + dx * 1.15 * u, ky + (1.4 + ln) * u, K[4])
    s.line(kx - 0.7 * u, ky + 1.0 * u, kx - 0.8 * u, ky + 3.6 * u, K[3])
    rx_, ry_ = kx + 0.2 * u, ky + 5.6 * u
    s.ring(rx_, ry_, 1.2 * u, 1.2 * u, ST[3], 1)
    s.set(rx_ - 0.9 * u, ry_ - 0.6 * u, ST[2])
    for (dx, ln) in [(-0.6, 3.6), (0.5, 4.4)]:
        x0 = rx_ + dx * u
        s.vline(x0, ry_ + 1.0 * u, ry_ + ln * u, ST[3])
        s.hline(x0, x0 + 0.9 * u, ry_ + ln * u, ST[3])
        s.hline(x0, x0 + 0.6 * u, ry_ + (ln - 1.1) * u, ST[3])
    # --- light ------------------------------------------------------------------------
    # The orb lights the cloth near it a stop up. Not the face: the face is far enough inside
    # the cowl that only its edge sees the orb, and lifting it read as a grey mask.
    orb = (hx - 0.2 * u, hy - 4.6 * u)
    light_up(s, orb[0], orb[1], 22 * u, [(V[4], V[3]), (V[3], V[2]), (OCC, V[4])], power=1.8)
    shoulder = int(oy - 44 * u)
    edge_light(s, DK[1], (0, -1), box=(0, 0, s.w, shoulder + 2 * u), inner=DK[3])
    edge_light(s, DK[2], (-1, 0), box=(0, 0, ox + 1 * u, oy - 30 * u))
    edge_light(s, DK[2], (1, 0), box=(ox + 3 * u, 0, s.w, shoulder))
    edge_light(s, DK[3], (1, 0), box=(ox + 3 * u, shoulder, s.w, oy - 20 * u))
    over(c, s, cx - ox, yb - oy)
    return (cx - ox + orb[0], yb - oy + orb[1], u)


def black_sun(c, x, y, r, rays=0, seed=0, glow=True):
    """His light: a disc of nothing with the darkness ramp burning round its rim. The corona is
    the emitter - the only place the ramp is used as light in the game - and the sky it lands on
    takes the violet pigment row, as the ward's floor takes gold."""
    if glow:
        c.radial(x, y, r * 2.6, r * 2.6, [V[3], V[3], V[4], None])
        c.radial(x, y, r * 1.7, r * 1.7, [DK[3], DK[3], V[3], None])
    if rays:
        rng = random.Random(seed)
        for i in range(rays):
            a = i * 6.28318 / rays + rng.random() * 0.15
            ln = r * (1.5 + rng.random() * 1.5)
            for k in range(int(ln - r * 1.15)):
                if (k + i) % 2:
                    continue
                d = r * 1.15 + k
                c.set(x + math.cos(a) * d, y + math.sin(a) * d, DK[3] if k > 6 else DK[2])
    c.disc(x, y, r * 1.16, r * 1.16, DK[2])
    c.disc(x, y, r * 1.07, r * 1.07, DK[1])
    c.ring(x, y, r * 1.03, r * 1.03, DK[0], 1)
    c.disc(x, y, r, r, OCC)


def motes(c, src, dst, n, seed, ramps, spread=10, bow=0.0):
    """Light being drawn out of the wizards and into his hand: sparks along a bowed path,
    thinning and shrinking as they near him."""
    rng = random.Random(seed)
    for _ in range(n):
        t = rng.random() ** 0.8
        mx = src[0] + (dst[0] - src[0]) * t
        my = src[1] + (dst[1] - src[1]) * t - math.sin(t * math.pi) * bow
        mx += (rng.random() - 0.5) * spread * (1 - t)
        my += (rng.random() - 0.5) * spread * (1 - t)
        ramp = pick(rng, ramps)
        col = ramp[ri(rng, 0, 3)]
        c.set(mx, my, col)
        if t < 0.45 and rng.random() < 0.4:
            c.set(mx + 1, my, ramp[2])


def player_back(c, cx, yb, h):
    """The player, small and walking away: the title's figure from behind - indigo wool, the
    brimmed hat, the staff in his right hand burning on the light ramp, the ward's colour.
    Lit by the key from the upper left (soft rim, his robe is a dark surface) and from below by
    his own orb."""
    u = h / 32.0
    s = Cv(int(30 * u), int(40 * u))
    ox, oy = int(14 * u), int(37 * u)

    def P(pts):
        return [(ox + a * u, oy + b * u) for (a, b) in pts]
    c.radial(cx + 1 * u, yb + 0.6 * u, 8 * u, 2.2 * u, [OCC, OCC, None])
    cloak = P([(-3.6, -19), (3.6, -19), (5.6, -12), (7.2, -3), (6.8, 0.4), (3.0, 0), (0.6, 0.8),
               (-2.4, 0), (-6.4, 0.6), (-6.6, -3.6), (-5.2, -12)])
    s.poly_shade(cloak, [W[2], W[3], W[4], OCC], ang=0.785, gamma=0.7)
    for (a, b, a2, b2) in [(-1.6, -17, -3.2, 0), (1.2, -17, 2.6, 0), (3.2, -15, 5.4, -1)]:
        s.line(ox + a * u, oy + b * u, ox + a2 * u, oy + b2 * u, W[4])
    s.poly(P([(-3.8, -19.6), (3.8, -19.6), (4.4, -17.4), (-4.4, -17.4)]), W[3])     # shoulders
    s.disc(ox, oy - 21.4 * u, 2.4 * u, 2.2 * u, W[4])                            # back of the head
    brim = P([(-6.8, -22.6), (-3.0, -23.8), (3.2, -23.8), (6.8, -22.4), (3.0, -21.6), (-3.0, -21.6)])
    s.poly_shade(brim, [W[2], W[3], W[4]], ang=0.785, gamma=0.8)
    cone = P([(-3.2, -23.6), (3.4, -23.6), (2.0, -27.4), (0.6, -30.6), (-1.6, -32.4), (-1.2, -29.6),
              (-2.0, -27.0)])
    s.poly_shade(cone, [W[2], W[3], W[4], OCC], ang=0.785, gamma=0.7)
    s.hline(ox - 3.0 * u, ox + 3.2 * u, oy - 23.5 * u, OCC)
    # the staff in his right hand, our right
    sx = ox + 6.0 * u
    s.vline(sx, oy - 27 * u, oy, G[2])
    s.vline(sx + 1, oy - 27 * u, oy - 2 * u, G[3])
    s.disc(sx - 0.4 * u, oy - 13.4 * u, 1.0 * u, 0.9 * u, W[3])                   # his hand on it
    edge_light(s, RIM_SOFT, (-1, 0), box=(0, 0, ox + 1 * u, oy))
    light_up(s, sx, oy - 28 * u, 18 * u, [(W[4], W[3]), (W[3], W[2]), (OCC, W[4])], power=1.6)
    over(c, s, cx - ox, yb - oy)
    oxo, oyo = cx + 6.0 * u + 0.5, yb - 28.6 * u
    c.radial(oxo, oyo, 4.6 * u, 4.6 * u, [LT[3], LT[3], None])
    c.radial(oxo, oyo, 1.7 * u, 1.7 * u, [LT[0], LT[1], LT[2], LT[3]])


# --------------------------------------------------------------------------- the tower
def tower(c, x, yb, h, seed=0, lit=True):
    """His seat, far off: a black spire that thins as it climbs, crowned by his light."""
    rng = random.Random(seed)
    w = h * 0.11
    pts = [(x - w, yb), (x - w * 0.62, yb - h * 0.55), (x - w * 0.9, yb - h * 0.6),
           (x - w * 0.45, yb - h * 0.82), (x - w * 0.15, yb - h), (x + w * 0.2, yb - h * 0.9),
           (x + w * 0.5, yb - h * 0.8), (x + w * 0.85, yb - h * 0.62), (x + w * 0.6, yb - h * 0.55),
           (x + w * 1.05, yb)]
    c.poly(pts, OCC)
    c.line(x - w * 0.9, yb - h * 0.6, x - w * 0.45, yb - h * 0.82, V[4])
    for _ in range(4):
        wy = yb - h * (0.15 + rng.random() * 0.55)
        c.set(x + (rng.random() - 0.5) * w, wy, DK[2])
    if lit:
        black_sun(c, x - w * 0.15, yb - h - h * 0.08, max(2.0, h * 0.045))


# --------------------------------------------------------------------------- the ward
def ward(c, cx, cy, rx, ry, A=None, ground=None, pool=True):
    """The player's warded circle, as on the title (splash.vigil): the light ramp burning in
    rings and a hexagram, pigment gold where it lands on the ground."""
    A = A or LT
    GR = ground or G
    if pool:
        c.radial(cx, cy, rx * 1.4, ry * 1.5, [GR[2], GR[2], GR[3], GR[3], GR[4], GR[4], None])
    c.ring(cx, cy, rx, ry, A[3], 2)
    c.ring(cx, cy, rx * 0.96, ry * 0.95, A[2], 2)
    c.ring(cx, cy, rx * 0.84, ry * 0.8, A[3], 1)
    c.ring(cx, cy, rx * 0.63, ry * 0.6, A[3], 1)
    star = [(cx + math.cos(i * 1.0472 - 1.5708) * rx * 0.84,
             cy + math.sin(i * 1.0472 - 1.5708) * ry * 0.8) for i in range(6)]
    for i in range(6):
        c.line(star[i][0], star[i][1], star[(i + 2) % 6][0], star[(i + 2) % 6][1], A[2])
    for i in range(12):
        a = i * 6.28318 / 12.0 + 0.22
        x0 = cx + math.cos(a) * rx * 0.91
        y0 = cy + math.sin(a) * ry * 0.88
        c.vline(x0, y0 - 2, y0 + 2, A[1])
        c.set(x0, y0, A[0])
    return star


# =========================================================================== panels
def p_moot():
    """1. The circle where the wizards met, before. Every wizard's light is still in the world:
    a fire in the middle of a warded ring, and round it their staves, each a different element.
    The last time the screen holds more than one warm light."""
    back, mid, fore = frame(True), frame(), frame()
    sky(back, 0, 400, [OCC, S[4], V[4], V[3], V[2]])
    stars(back, (0, 0, FW, 330), 520, 3)
    hills(back, ridge_fn(366, 16, 11), [V[3], V[4], S[4]], 40, lit=V[2])
    hills(back, ridge_fn(392, 9, 23, 1.6), [S[4], V[4]], 30, lit=V[3])
    back.vramp(398, FH - 1, [V[4], S[4], S[4], OCC])
    fx, fy = 480, 478
    # A band of thicker stars across the sky. (Thin cloud was tried here and dithered into ruled
    # horizontal stripes across the phone's sky; the heath has no cloud.)
    rs = random.Random(12)
    for _ in range(700):
        t = rs.random()
        x = t * FW
        y = 40 + t * 230 + (rs.random() - 0.5) * 90 * (0.4 + rs.random())
        back.set(x, y, pick(rs, [S[2], S[1], V[2], K[2]]))
    sk.moon(back, 372, 286, 13)

    # the worn way in, from the camera to the gap in the ring: filled row by row and broken up
    # by dither, so it reads as trodden ground and not as a ladder
    def way(y):
        t = (840 - y) / float(840 - fy - 52)
        return fx + 10 + math.sin(t * 2.6) * 26 * (1 - t), 3 + (1 - t) * 30
    for y in range(fy + 52, FH):
        wx, wd = way(y)
        for x in range(int(wx - wd), int(wx + wd) + 1):
            e = 1.0 - abs(x - wx) / wd
            if dith(x, y, min(1.0, e * 2.2)):
                back.set(x, y, S[3] if dith(x * 3 + 1, y * 5, 0.32 * e) else S[4])
    heath(back, 400, 840, 2600, 5, [(S[4], V[3]), (V[4], V[3]), (OCC, V[4]), (S[4], LI[4])],
          keep_out=lambda x, y: y > fy + 52 and abs(x - way(y)[0]) < way(y)[1] * 0.8)
    back.radial(fx, fy + 4, 190, 52, [G[3], G[3], G[4], G[4], G[4], None])
    ward(mid, fx, fy, 64, 17, pool=False)
    # The ring of stones and the wizards inside it, sorted back to front together so the near
    # stones stand in front of the far wizards. No stone at the front centre: that is where the
    # way in comes through, and a stone there stood in front of the fire.
    items = []
    rng = random.Random(41)
    for i in range(14):
        a = i * 6.28318 / 14.0 + 0.11
        if math.sin(a) > 0.86:
            continue
        x = fx + math.cos(a) * 232
        y = fy - 4 + math.sin(a) * 60
        depth = (y - (fy - 64)) / 128.0
        items.append((y, "stone", x, depth, 900 + i))
    robes = [[V[4], OCC], [W[4], OCC], [L[4], OCC], [ST[4], OCC], [S[4], OCC], [R[4], OCC], [V[4], OCC]]
    lights = [E["fire"], E["ice"], E["lightning"], E["grass"], E["water"], E["earth"], E["arcane"]]
    for i in range(7):
        a = i * 6.28318 / 7.0 + 0.5
        x = fx + math.cos(a) * 112
        y = fy + math.sin(a) * 30
        items.append((y, "wizard", x, i, 0))
    items.sort(key=lambda it: it[0])
    for (y, kind, x, d, seed) in items:
        if kind == "stone":
            h = 34 + d * 46 + rng.random() * 16
            w = 10 + d * 12 + rng.random() * 5
            far = y < fy
            standing_stone(mid, x, y, w, h, seed, lean=(x - fx) / 2400.0 + (rng.random() - 0.5) * 0.08,
                           warm=(G, 0.85 - d * 0.5) if far else None,
                           ramp=S if far else [S[3], S[4], S[4], OCC, OCC],
                           toward=None if far else (1 if x < fx else -1, -0.6, G[3]))
        else:
            i = d
            hgt = 42 + (y - fy + 30) * 0.28
            side = -1 if x > fx else 1
            facing_fire = (fx - x, fy - y)
            n = math.hypot(*facing_fire) or 1
            wizard_figure(mid, x, y, hgt, robes[i], 300 + i, staff=side, orb=lights[i],
                          warm=G, toward=(facing_fire[0] / n, facing_fire[1] / n),
                          hat_lean=(i % 3 - 1) * 0.6)
    sk.flame(mid, fx, fy + 1, 30, LT, seed=17, halo=G)
    sk.flame(mid, fx - 7, fy + 3, 19, LT, seed=29, halo=G)
    sk.flame(mid, fx + 8, fy + 2, 22, LT, seed=31, halo=G)
    rm = random.Random(77)
    for _ in range(40):                                  # sparks going up off the fire
        mid.set(fx + (rm.random() - 0.5) * 30, fy - 30 - rm.random() ** 1.6 * 90,
                pick(rm, [LT[1], LT[2], LT[3], G[1]]))
    # The near ground: heather in occlusion, boulders along the way in, and a stone of the ring
    # right at the camera on either side.
    heath(fore, 590, 840, 700, 9, [(OCC, S[4]), (OCC, V[4])], scale=1.6,
          keep_out=lambda x, y: abs(x - fx - 10) < 20 + (y - 590) * 0.16)
    for (bx, by, bw, bh, sd) in [(392, 668, 40, 16, 1), (586, 700, 54, 20, 2), (350, 770, 70, 26, 3),
                                 (630, 800, 84, 30, 4), (430, 610, 22, 9, 5)]:
        sk.boulder(fore, bx, by, bw, bh, seed=sd, ramp=[S[1], S[1], S[2], S[2], S[3]])
    standing_stone(fore, 214, 650, 70, 210, 61, lean=0.04, ramp=[S[3], S[4], S[4], OCC, OCC],
                   toward=(1, -0.5, G[4]))
    standing_stone(fore, 770, 640, 56, 150, 63, lean=-0.05, ramp=[S[3], S[4], S[4], OCC, OCC],
                   toward=(-1, -0.4, G[4]))
    layers = dict(back=back, mid=mid, fore=fore)
    finish(layers, 0.9)
    return layers


def p_throne():
    """2. "He came here first." The same circle, under him. His first full look: the only
    figure in the game that sheds dark light. The wizards' light leaves them and goes into his
    hand; the stones go down outward from where he stands."""
    back, mid, fore = frame(True), frame(), frame()
    sky(back, 0, 420, [OCC, OCC, S[4], V[4], V[3]])
    stars(back, (0, 0, FW, 300), 160, 4, colors=[S[0], V[1], V[0]])
    sun_x, sun_y = 482, 330
    black_sun(back, sun_x, sun_y, 50, rays=40, seed=5)
    hills(back, ridge_fn(388, 12, 11), [V[4], S[4], OCC], 40, lit=V[3])
    back.vramp(400, FH - 1, [V[4], S[4], OCC, OCC])
    heath(back, 402, 840, 2200, 6, [(S[4], V[4]), (OCC, V[4]), (OCC, S[4])])
    back.radial(480, 600, 260, 66, [V[3], V[3], V[4], V[4], None])
    # stones of the ring, thrown down outward from him
    for i, (x, y, w, h, lean, br) in enumerate([(262, 470, 20, 54, -0.35, True), (338, 452, 16, 40, -0.2, False),
                                                (622, 450, 16, 42, 0.22, True), (702, 468, 21, 56, 0.4, False),
                                                (180, 520, 30, 80, -0.55, True), (790, 520, 30, 76, 0.5, True)]):
        standing_stone(back, x, y, w, h, 700 + i, lean=lean, broken=br, ramp=[S[3], S[4], S[4], OCC, OCC],
                       toward=(-0.6 if x > 480 else 0.6, -0.8, DK[3]))
    hx, hy, u = dark_wizard(mid, 480, 584, 290)
    black_sun(mid, hx, hy, 1.7 * u)
    # the wizards at his feet, their light going out of them and up into his hand
    victims = [(352, 574, -1, 0.0), (404, 590, 1, 0.5), (572, 586, -1, -0.4), (630, 570, 1, 0.3), (318, 630, 1, 0.2),
               (660, 634, -1, -0.5)]
    lights = [E["fire"], E["ice"], E["lightning"], E["grass"], E["water"], E["earth"]]
    for i, (x, y, side, lean) in enumerate(victims):
        # The streams from his right pass BEHIND him to the raised hand: in front they ran across
        # his face.
        motes(fore if x < 480 else back, (x + side * 13, y - 34), (hx, hy), 150, 400 + i, [lights[i]],
              spread=18, bow=40 + i * 6)
    for i, (x, y, side, lean) in enumerate(victims):
        wizard_figure(fore, x, y, 44 + (y - 590) * 0.3, [OCC, S[4]], 500 + i, staff=side,
                      orb=lights[i], dim_orb=True, hat_lean=lean, toward=(0, -1, DK[3]))
    # shadow running out of his hem across the ground: what he leaves becomes what serves him
    rng = random.Random(91)
    for i in range(26):
        a = math.pi * (0.04 + 0.92 * rng.random())
        ln = 70 + rng.random() * 230
        x0, y0 = 480 + math.cos(a) * 60, 584
        pts = []
        for k in range(24):
            t = k / 23.0
            pts.append((x0 + math.cos(a) * ln * t * 1.6 * (1 if a < 1.57 else 1),
                        y0 + math.sin(a) * ln * t * 0.34 + math.sin(t * 7 + i) * 3))
        for k in range(len(pts) - 1):
            wd = 2.4 * (1 - k / 23.0) + 0.6
            sk._stroke(mid, pts[k][0], pts[k][1], pts[k + 1][0], pts[k + 1][1], wd, wd * 0.8, OCC)
        if rng.random() < 0.55:
            ex, ey = pts[-1]
            mid.set(ex, ey - 2, EYE[0])
            mid.set(ex + 2, ey - 2, EYE[1])
    heath(fore, 640, 840, 700, 19, [(OCC, S[4]), (OCC, OCC)], scale=1.8)
    layers = dict(back=back, mid=mid, fore=fore)
    finish(layers, 0.8, cy=CY - 10)
    return layers


def p_tide():
    """3. His shadows over the land. A village at the foot of a hill, still lit; over the crest
    behind it, from the tower on the horizon, a tide of the things he raised comes down the
    slope toward it. Up close they are the chapters' kinds - cowl, horns, spear, shield, the
    caster's loose orb - and every one of them wears his mark in its eyes. Massed, they are
    a dark flood with a thousand of those eyes in it."""
    back, mid, fore = frame(True), frame(), frame()
    sky(back, 0, 400, [OCC, S[4], V[4], V[3], V[2], V[2]])
    stars(back, (0, 0, FW, 300), 300, 8, colors=[K[1], K[2], V[0]])
    tx, ty = 636, 356
    hills(back, ridge_fn(352, 7, 31, 0.8), [V[3], V[4], S[4]], 24, lit=V[2])
    tower(back, tx, ty, 72, seed=3)

    def crest(x):
        return 456 - (x - 160) * 0.12 + math.sin(x * 0.03) * 4 + math.sin(x * 0.011 + 1) * 8

    hills(back, crest, [V[2], V[3], V[3], V[4], S[4]], 150, lit=V[1])
    heath(back, 380, 600, 1600, 7, [(V[4], V[3]), (S[4], V[3])],
          keep_out=lambda x, y: y < crest(x) + 3)
    back.vramp(560, FH - 1, [S[4], S[4], OCC, OCC], x0=0)
    heath(back, 560, 840, 1400, 9, [(OCC, S[4]), (OCC, V[4])], scale=1.4)
    rng = random.Random(55)
    # the village at the hill's foot, lit; the houses nearest the tide already dark
    houses = []
    for k in range(16):
        hx = 300 + rng.random() * 190
        hy = 528 + rng.random() * 34 - (hx - 300) * 0.04
        houses.append((hy, hx))
    houses.sort()
    for (hy, hx) in houses:
        wd = 9 + rng.random() * 7
        ht = 6 + rng.random() * 3
        back.rect(hx - wd / 2, hy - ht, hx + wd / 2, hy, S[3])
        back.rect(hx + wd * 0.1, hy - ht, hx + wd / 2, hy, S[4])
        back.poly([(hx - wd / 2 - 2, hy - ht), (hx + wd / 2 + 2, hy - ht), (hx + 1, hy - ht - 7)], OCC)
        back.line(hx - wd / 2 - 2, hy - ht, hx + 1, hy - ht - 7, V[4])
        back.hline(hx - wd / 2, hx + wd / 2, hy + 1, OCC)
        if hx < 440 and rng.random() < 0.85:
            wx = hx - wd * 0.2
            back.radial(wx, hy - ht * 0.5, 7, 5, [G[3], G[4], None])
            back.set(wx, hy - ht * 0.5, G[1])
            back.set(wx + 1, hy - ht * 0.5, G[2])
    for _ in range(26):                                 # trees among the houses
        x = 280 + rng.random() * 230
        y = 520 + rng.random() * 50
        sk._clump(back, x, y, 6, 5, 22, rng, OCC, S[4])

    # THE TIDE: everything right of a leading edge that runs from the crest down to the village.
    def front_x(y):
        return 548 - (y - 380) * 0.55 + math.sin(y * 0.05) * 8

    kinds = ["swarmer", "swarmer", "swarmer", "runner", "bruiser", "shielder", "caster", "knight", "hound"]
    bodies = []
    for _ in range(1500):
        y = 372 + rng.random() ** 1.1 * 228
        x = front_x(y) + rng.random() ** 0.8 * (FW + 40 - front_x(y))
        if y < crest(x) - 4 and rng.random() < 0.85:
            continue
        bodies.append((y, x))
    for _ in range(90):                                 # the broken front, a few strides out
        y = 400 + rng.random() * 190
        bodies.append((y, front_x(y) - rng.random() * 26))
    bodies.sort()
    for (y, x) in bodies:
        hgt = 7 + (y - 372) * 0.09 + rng.random() * 3
        creature(mid, pick(rng, kinds), x, y, hgt, facing=-1, body=[OCC, S[4]], seed=ri(rng, 0, 99999))
    # the nearest, at the camera: a horned one, cut by the frame, looking where they are going
    # The front rank, nearest the village: a few of them big enough to name, in profile and rimmed
    # by the key. One giant figure in the foreground was tried twice - from behind a horned one is
    # two round shapes with no face, and cut by the frame a knight is a pole - and neither read.
    for i, (kind, dy, hgt) in enumerate([("hound", 0, 30), ("runner", 14, 44), ("swarmer", 34, 40),
                                         ("bruiser", 6, 58), ("caster", 40, 54), ("knight", 22, 62),
                                         ("shielder", 48, 50)]):
        y = 548 + dy
        x = front_x(y) + 8 + i * 30
        # Rimmed in stone hi, as the locked wizards' silhouettes are (.ai/side-events.md): against
        # the dark mass behind them the full soft rim drew an outline round each.
        creature(fore, kind, x, y, hgt, facing=-1, body=[OCC, S[4]], rim=S[0], seed=80 + i)
    heath(fore, 640, 840, 500, 23, [(OCC, OCC), (OCC, S[4])], scale=1.8)
    layers = dict(back=back, mid=mid, fore=fore)
    finish(layers, 0.75)
    return layers


def p_taken():
    """4. "He took them all." The wizards led away along a ridge against his light, bound, their
    staves gone, their light drawn out of them toward the tower. Silhouettes only: no faces, no
    names, nothing that tells one of the rescued from another."""
    back, mid, fore = frame(True), frame(), frame()
    sky(back, 0, 560, [OCC, S[4], V[4], V[3], V[3], V[2]])
    stars(back, (0, 0, FW, 300), 140, 13, colors=[K[2], V[0]])
    sx_, sy_ = 628, 318
    back.radial(sx_, sy_ + 120, 360, 170, [V[2], V[2], V[3], None])
    black_sun(back, sx_, sy_, 22, rays=26, seed=8)
    hills(back, ridge_fn(452, 10, 41, 0.9), [V[2], V[3], V[3], V[4]], 70, lit=V[1])
    tower(back, sx_ + 6, 446, 104, seed=4, lit=False)

    # the ridge they walk, rising left to right toward him; they stand on the skyline
    def ridge(x):
        return 572 - (x - 160) * 0.09 + math.sin(x * 0.02) * 5 + math.sin(x * 0.071) * 2

    for x in range(FW):
        top = int(ridge(x))
        for y in range(top, FH):
            mid.set(x, y, OCC if (y - top) > 3 or dith(x, y, 0.7) else S[4])
        if ridge(x + 1) < ridge(x - 1):
            mid.set(x, top, V[4])
    order = [("knight", 0), ("wiz", 0), ("bruiser", 0), ("wiz", 1), ("shielder", 0), ("wiz", 2),
             ("caster", 0), ("wiz", 3), ("swarmer", 0), ("runner", 0)]
    lights = [E["fire"], E["ice"], E["lightning"], E["grass"]]
    x = 600
    rng = random.Random(17)
    wrists = []
    for i, (kind, k) in enumerate(order):
        yb = ridge(x) + 1
        if kind == "wiz":
            hgt = 88 - k * 3
            motes(back, (x, yb - hgt * 0.6), (sx_, sy_), 170, 640 + k, [lights[k]], spread=12,
                  bow=40 + k * 20)
            # rimmed from his light, up and to the right, so the robe reads against the hills and
            # not only the hat against the sky
            wizard_figure(mid, x, yb, hgt, [OCC, S[4]], 610 + k, bound=True, hat_lean=1.4 + k * 0.3,
                          toward=(0.7, -0.7, DK[3]))
            wrists.append((x + 1, yb - hgt * 9.8 / 30.0))
        else:
            hgt = {"bruiser": 74, "knight": 86, "shielder": 66, "caster": 78, "swarmer": 56, "runner": 58}[kind]
            creature(mid, kind, x, yb, hgt, facing=1, body=[OCC, S[4]], rim=DK[3], seed=700 + i)
        x -= 52 + rng.random() * 12
    # the chain that binds them, one line of his light from wrist to wrist
    for a, b in zip(wrists, wrists[1:]):
        for k in range(48):
            t = k / 47.0
            mid.set(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t + math.sin(t * math.pi) * 10,
                    DK[2] if k % 3 else DK[1])
    heath(fore, 640, 840, 500, 29, [(OCC, OCC), (OCC, S[4])], scale=1.7)
    for i, (x0, h) in enumerate([(150, 120), (874, 140)]):
        sk.tree(fore, x0, 700, h * 2.0, h * 1.4, seed=40 + i, dark=OCC, light=OCC, vines=False)
    layers = dict(back=back, mid=mid, fore=fore)
    finish(layers, 0.75)
    return layers


def p_missed():
    """5. "You are the one he missed." The title's picture, told as story: the player from
    behind in the warded circle, alone in the broken circle at night, with the eyes in the dark
    beyond the stones. The only warm light left on screen."""
    back, mid, fore = frame(True), frame(), frame()
    sky(back, 0, 420, [OCC, OCC, S[4], V[4], V[3]])
    stars(back, (0, 0, FW, 340), 260, 21)
    tower(back, 694, 404, 34, seed=6)
    hills(back, ridge_fn(398, 12, 51), [V[4], S[4], OCC], 40, lit=V[3])
    back.vramp(404, FH - 1, [S[4], S[4], OCC, OCC])
    heath(back, 404, 840, 1800, 31, [(OCC, S[4]), (OCC, V[4])])
    cx, cy = 480, 548
    back.radial(cx, cy, 300, 90, [G[3], G[3], G[4], G[4], None])
    for i, (x, y, w, h, lean, br) in enumerate([(232, 450, 18, 46, -0.4, True), (316, 432, 14, 36, 0.1, False),
                                                (402, 424, 12, 30, -0.1, True), (560, 424, 13, 34, 0.2, False),
                                                (646, 432, 15, 40, -0.3, True), (734, 452, 19, 50, 0.45, False)]):
        standing_stone(back, x, y, w, h, 800 + i, lean=lean, broken=br, ramp=[S[2], S[3], S[4], OCC, OCC],
                       warm=(G, 0.4), toward=None)
    rng = random.Random(83)
    placed = []
    for _ in range(14):                                # his, at the edge of the light
        for _try in range(30):
            x = 180 + rng.random() * 600
            y = 404 + rng.random() * 46
            if all(abs(x - a) > 26 or abs(y - b) > 14 for (a, b) in placed) and abs(x - 480) > 40:
                break
        placed.append((x, y))
        gap = 3 + ri(rng, 0, 2)
        back.set(x, y, EYE[0])
        back.set(x + gap, y, EYE[0])
        back.set(x + 1, y, EYE[1])
        back.set(x + gap - 1, y, EYE[1])
    star = ward(mid, cx, cy, 150, 40)
    hero.wizard_hero(mid, cx, cy, h=150, staff_ramp=LT, under_ramp=LT)
    rm = random.Random(509)
    for _ in range(46):
        a = rm.random() * 6.28318
        d = 0.8 + rm.random() * 0.32
        mid.set(cx + math.cos(a) * 150 * d, cy + math.sin(a) * 40 * d - rm.random() ** 1.7 * 52,
                pick(rm, [LT[0], LT[1], LT[2], LT[3]]))
    heath(fore, 600, 840, 900, 37, [(OCC, OCC), (OCC, S[4])], scale=1.7,
          keep_out=lambda x, y: ((x - cx) / 172.0) ** 2 + ((y - cy) / 70.0) ** 2 < 1.0)
    standing_stone(fore, 186, 640, 64, 170, 91, lean=0.06, broken=True, ramp=[S[4], S[4], OCC, OCC, OCC],
                   toward=(1, -0.4, G[4]))
    standing_stone(fore, 790, 650, 74, 200, 93, lean=-0.04, ramp=[S[4], S[4], OCC, OCC, OCC],
                   toward=(-1, -0.4, G[4]))
    layers = dict(back=back, mid=mid, fore=fore)
    finish(layers, 0.9, cy=CY + 60)
    return layers


def p_road():
    """6. The road ahead. Seen from above and behind: the player small on a road that winds
    away across the heath toward everything he holds, the tower on the far horizon under his
    light. No line - the next screen is the player choosing who walks it."""
    back, mid, fore = frame(True), frame(), frame()
    sky(back, 0, 300, [OCC, S[4], V[4], V[3], V[2]])
    stars(back, (0, 0, FW, 250), 300, 33)
    sk.moon(back, 330, 262, 9)
    tx, ty = 596, 294
    hills(back, ridge_fn(286, 12, 61, 0.7), [V[3], V[4], S[4]], 26, lit=V[2])
    tower(back, tx, ty, 46, seed=9)
    hills(back, ridge_fn(306, 14, 63, 1.1), [S[4], V[4], S[4]], 30, lit=V[3])
    hills(back, ridge_fn(352, 22, 67, 1.4), [V[4], S[4], OCC], 40, lit=V[3])
    back.vramp(360, FH - 1, [S[4], S[4], OCC])
    heath(back, 350, 840, 2400, 41, [(OCC, S[4]), (OCC, V[4]), (S[4], V[3])])
    # far places on the way: a dark wood, a broken keep on a rise
    rng = random.Random(71)
    for _ in range(30):
        x = 220 + rng.random() * 140
        y = 330 + rng.random() * 26
        sk._clump(back, x, y, 9, 6, 30, rng, OCC, S[4])
    back.poly([(700, 352), (702, 318), (708, 316), (709, 300), (716, 302), (718, 324), (726, 326), (728, 352)], OCC)
    back.line(702, 318, 708, 316, V[3])
    # the road, an S from the bottom of the frame to the far hills
    road = [(500, 840), (488, 740), (452, 650), (500, 580), (560, 520), (520, 460), (480, 410), (520, 372),
            (560, 340), (590, 304)]
    pts = []
    for k in range(len(road) - 1):
        for s_ in range(20):
            t = s_ / 20.0
            p0 = road[max(0, k - 1)]
            p1, p2 = road[k], road[k + 1]
            p3 = road[min(len(road) - 1, k + 2)]
            tt = t * t
            ttt = tt * t
            pts.append((0.5 * (2 * p1[0] + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * tt
                               + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * ttt),
                        0.5 * (2 * p1[1] + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * tt
                               + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * ttt)))
    for (x, y) in pts:
        wd = 1.0 + max(0.0, (y - 300)) * 0.034
        back.disc(x, y, wd * 1.6, wd * 0.5, S[4])
    for (x, y) in pts:
        wd = 1.0 + max(0.0, (y - 300)) * 0.034
        back.disc(x - wd * 0.3, y, wd * 1.0, wd * 0.34, S[3])
    rr = random.Random(5)
    for (x, y) in pts[::2]:                            # ruts and stones in it
        wd = 1.0 + max(0.0, (y - 300)) * 0.034
        back.set(x + (rr.random() - 0.5) * wd * 2.4, y + (rr.random() - 0.5) * wd * 0.6,
                 S[4] if rr.random() < 0.7 else S[2])
    # waymarks along it, the heath's standing stones, small with distance
    for (k, side, h) in [(44, 1, 26), (84, -1, 16), (112, 1, 11), (140, -1, 8)]:
        wx, wy = pts[k]
        wd = 1.0 + max(0.0, (wy - 300)) * 0.034
        standing_stone(back, wx + side * wd * 2.6, wy, h * 0.4, h, 950 + k, lean=side * 0.04,
                       ramp=[S[2], S[3], S[4], OCC, OCC])
    # He stands ON the road: x from the spline at his row, not a number that drifted off it.
    py_ = 566
    px_ = min(pts, key=lambda p: abs(p[1] - py_))[0]
    back.radial(px_, py_ + 2, 80, 26, [G[3], G[3], G[4], G[4], None])
    player_back(mid, px_, py_, 50)
    heath(fore, 640, 840, 700, 43, [(OCC, OCC), (OCC, S[4])], scale=1.8)
    layers = dict(back=back, mid=mid, fore=fore)
    finish(layers, 0.8)
    return layers


PANELS = [("moot", p_moot), ("throne", p_throne), ("tide", p_tide), ("taken", p_taken),
          ("missed", p_missed), ("road", p_road)]


# =========================================================================== output
def audit(layers):
    ok = raster.allowed()
    bad = {}
    for c in layers.values():
        for (n, col) in c.img.getcolors(1 << 20) or []:
            if col[3] == 0:
                continue
            h = "#%02X%02X%02X" % col[:3]
            if h not in ok:
                bad[h] = bad.get(h, 0) + n
    return bad


def composite(layers):
    flat = layers["back"].img.copy()
    for n in ("mid", "fore"):
        flat.paste(layers[n].img, (0, 0), layers[n].img)
    return flat


def preview(name, layers):
    """Both views side by side at x1: the PC's 640x360 and the Pixel 9's 360x808."""
    flat = composite(layers)
    wide = flat.crop(VIEW_WIDE)
    tall = flat.crop(VIEW_TALL)
    sheet = Image.new("RGBA", (wide.size[0] + tall.size[0] + 12, tall.size[1]), (40, 40, 40, 255))
    sheet.paste(wide, (0, 0))
    sheet.paste(tall, (wide.size[0] + 12, 0))
    if not os.path.isdir(PREVIEW):
        os.makedirs(PREVIEW)
    sheet.save(os.path.join(PREVIEW, name + ".png"))


def main(argv):
    want = [a for a in argv if not a.startswith("--")]
    do_preview = "--preview" in argv
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    total_bad = 0
    for name, fn in PANELS:
        if want and name not in want:
            continue
        layers = fn()
        bad = audit(layers)
        total_bad += sum(bad.values())
        for ln, c in layers.items():
            c.img.save(os.path.join(OUT, "%s-%s.png" % (name, ln)))
        if do_preview:
            preview(name, layers)
        print("%-7s %d off-contract pixels%s" % (name, sum(bad.values()),
                                                   (" " + str(sorted(bad.items())[:4])) if bad else ""))
    return 1 if total_bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
