# -*- coding: utf-8 -*-
"""Six DRAWN application-icon concepts, to choose between. Nothing here ships.

Run: python tools/art/icon_concepts.py   -> tools/art/_icon_concepts/ (gitignored)

`app_icon.py` ships a crop of the title screen. That is the right icon for the project manager
and the 256px .exe, and the wrong one for the two places an icon is actually decided: a Windows
taskbar at 16-24px, and an Android adaptive icon, where the launcher masks a 108-unit canvas to
a circle or a squircle and shows only the middle 72 units. A crop has no say in either - its
subject is wherever the title happened to put him. These six are drawn for those two places.

THE RULES EVERY DESIGN HERE FOLLOWS

- **Pixel art on a 64x64 grid, Bonelight colours only.** Every pixel is one of the contract's
  tones (`raster.allowed()`); the masters are checked with the same audit the title uses. A
  gradient is a Bayer dither between two adjacent tones, never a blend.
- **A separate, hand-typed 16x16 for each design.** 64 -> 16 is a 4:1 area reduction and any
  automatic reduction of it - BOX, nearest, anything - averages a two-pixel gold band into the
  hat it sits on and a one-pixel eye into the dark round it. The 16 is the size the icon is
  judged at (see `app_icon.py`), so it is drawn, cell by cell, in `SMALL16`. The 24 - what a
  Windows 11 taskbar shows at 100% scaling - is hand-drawn too (`SMALL24`), because 64 -> 24 is
  a non-integer reduction and came out soft in every design. 48 and 32 are BOX-reduced from the
  master and snapped back onto the palette.
- **Foreground and background are separate layers**, because that is what Android takes. The
  background is drawn centred at any size (64 for the master, 108 for the adaptive canvas), and
  the foreground is the 64 master's subject, centred on the 108 canvas - so it lands inside the
  middle 72 units, and everything that matters sits within a radius of ~32 of the centre, which
  is what both masks keep.
- On Windows the master is a rounded night tile, so it holds its edge on a light taskbar and
  simply melts into a dark one - which is how a taskbar icon should behave.

THE SIX (keys are what the sheet labels the rows)

A  Hat-crowned W   the W's middle peak rises into the pointed hat, band in ward gold
B  WS in the ward  the wordmark's W and S, in the title's gold summoning ring
C  Staff S         an S wound round the lit staff - slab-cut ends so it is a LETTER, not a snake
D  Hat and light   the hat, two lit eyes under the brim, warm light from beneath
E  The burning ward  the staff orb at the centre of the ring of fire and runes
F  Surrounded      one warm light, red eyes closing in from the dark

Writes, per design, `<key>_64.png` (the Windows master tile), `<key>_16.png` (the hand drawing),
`<key>_fg.png` (the Android foreground, 108x108) and `<key>_bg.png`; then `sheet.png`, the full
comparison, and `sheet_16.png`, just the six 16s at 1x and x8.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bonelight as bl
import raster

OUT = os.path.join(HERE, "_icon_concepts")

M = bl.MATERIALS
E = bl.ELEMENTS
OCC = bl.OCC
RIM = bl.RIM
WARD = E["light"]          # the title's ward ramp: core / hot / mid / edge
FIRE = E["fire"]

S = 64                     # master grid
ADAPT = 108                # Android adaptive canvas, in units = pixels here
VIEW = 72                  # what the launcher mask can show of it


# ----------------------------------------------------------------------------- helpers
def rgb(h):
    return raster.rgb(h)


def blank(n=S):
    return raster.Canvas(n, n)


def mask_of(draw_fn, n=S):
    """Rasterise shapes into a set of pixels. `draw_fn(d)` draws in white on an ImageDraw."""
    im = Image.new("L", (n, n), 0)
    draw_fn(ImageDraw.Draw(im))
    px = im.load()
    return set((x, y) for y in range(n) for x in range(n) if px[x, y] > 127)


def stroke(d, pts, w):
    """A thick polyline with round joints - ImageDraw's own joints leave notches."""
    d.line(pts, fill=255, width=int(w))
    r = (w - 1) / 2.0
    for (x, y) in pts:
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)


def paint(c, mask, fn):
    for (x, y) in mask:
        col = fn(x, y)
        if col is not None:
            c.set(x, y, col)


def edge_of(mask, steps=((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1))):
    out = set()
    for (x, y) in mask:
        for (dx, dy) in steps:
            n = (x + dx, y + dy)
            if n not in mask:
                out.add(n)
    return out


def halo(c, mask, col, diagonal=True):
    steps = [(-1, 0), (1, 0), (0, -1), (0, 1)]
    if diagonal:
        steps += [(-1, -1), (1, -1), (-1, 1), (1, 1)]
    for (x, y) in edge_of(mask, steps):
        c.set(x, y, col)


def drop(c, mask, dx, dy, col):
    for (x, y) in mask:
        if (x + dx, y + dy) not in mask:
            c.set(x + dx, y + dy, col)


def keyrim(c, mask, col, skip=None):
    """The 1px key-facing edge: a mask pixel whose up or left neighbour is outside the mask.
    The key comes from the upper left (art-direction section 2)."""
    for (x, y) in mask:
        if skip and skip(x, y):
            continue
        if (x - 1, y) not in mask or (x, y - 1) not in mask:
            c.set(x, y, col)


def rows_ramp(colors, y0, y1):
    """Colour by row, top to bottom, in solid bands - the wordmark's lettering gradient."""
    n = len(colors)

    def f(x, y):
        t = (y - y0) / float(max(1, y1 - y0))
        return colors[max(0, min(n - 1, int(t * n)))]
    return f


def dith_ramp(colors, y0, y1):
    """Like rows_ramp but dithered across each band boundary."""
    n = len(colors)

    def f(x, y):
        t = max(0.0, min(1.0, (y - y0) / float(max(1, y1 - y0)))) * (n - 1)
        i = min(n - 2, int(t))
        fr = t - i
        return colors[i + 1] if fr > raster.DITHER[y % 8][x % 8] / 64.0 else colors[i]
    return f


def radial(c, cx, cy, r, colors, ry=None):
    c.radial(cx, cy, r, ry or r, colors)


def grid(c, rows, legend, ox=0, oy=0):
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            col = legend.get(ch)
            if col is not None:
                c.set(ox + x, oy + y, col)


def night(c, n, warm=None):
    """The shared background: a dark night, a shade lighter in the middle, fading to occlusion
    at the edge. `warm` adds a pool of ward light on top, centred and sized in units of n."""
    m = (n - 1) / 2.0
    c.rect(0, 0, n - 1, n - 1, OCC)
    radial(c, m, m, n * 0.70, [M["violet"][3], M["violet"][3], M["violet"][4], M["stone"][4], OCC])
    if warm:
        if len(warm) == 3:
            warm = (0,) + tuple(warm)
        dx, dy, r, cols = warm
        radial(c, m + dx * n / 64.0, m + dy * n / 64.0, r * n / 64.0, cols)


def tile_mask(n, r):
    """A rounded square: True inside."""
    def inside(x, y):
        cx = r - 0.5 if x < r else (n - r - 0.5 if x >= n - r else None)
        cy = r - 0.5 if y < r else (n - r - 0.5 if y >= n - r else None)
        if cx is None or cy is None:
            return True
        return (x - cx) ** 2 + (y - cy) ** 2 <= (r + 0.15) ** 2
    return inside


def apply_tile(img, r):
    img = img.copy()
    px = img.load()
    n = img.size[0]
    inside = tile_mask(n, r)
    for y in range(n):
        for x in range(n):
            if not inside(x, y):
                px[x, y] = (0, 0, 0, 0)
    return img


_PAL = None


def snap(img):
    """Reduce to the contract: every pixel moved to its nearest allowed colour. Used only on
    the automatic reductions (48 / 32 / 24), so they audit clean like everything else."""
    global _PAL
    if _PAL is None:
        _PAL = [rgb(h)[:3] for h in raster.allowed().keys()]
    img = img.copy()
    px = img.load()
    cache = {}
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            p = px[x, y]
            if p[3] < 128:
                px[x, y] = (0, 0, 0, 0)
                continue
            k = p[:3]
            if k not in cache:
                cache[k] = min(_PAL, key=lambda q: (q[0] - k[0]) ** 2 * 2 + (q[1] - k[1]) ** 2 * 4
                               + (q[2] - k[2]) ** 2 * 3)
            px[x, y] = cache[k] + (255,)
    return img


# ============================================================================ the designs
# Each design is (background(canvas, n), foreground() -> 64 canvas). The foreground is drawn on
# a transparent 64 canvas and centred on whatever background it is composited over.

# ---------------------------------------------------------------- A. Hat-crowned W
def bg_a(c, n):
    night(c, n)


def fg_a():
    c = blank()
    # The W is the wordmark's: two straight sides and a middle that rises from the two valleys.
    # Here the middle keeps rising, past the tops of the sides, and becomes the hat - so the
    # brim sits where the peak would have been and the crown is the letter going on upward.
    def letter(d):
        stroke(d, [(10, 17), (10, 43), (20, 58)], 9)
        stroke(d, [(53, 17), (53, 43), (43, 58)], 9)
        stroke(d, [(20, 58), (31.5, 37)], 8)
        stroke(d, [(43, 58), (31.5, 37)], 8)
    w = mask_of(letter)
    w = set(p for p in w if p[1] <= 60)
    # flat slab cuts across the tops of the two sides, as in the wordmark's W
    w = set(p for p in w if p[1] >= 14)

    def hat(d):
        d.polygon([(21, 33), (42, 33), (39, 22), (37, 14), (38, 8), (42, 3), (44, 2), (39, 2),
                   (34, 6), (31, 13), (26, 24)], fill=255)
    h = mask_of(hat)

    def brim(d):
        d.polygon([(19, 32), (44, 32), (46, 34), (43, 37), (20, 37), (17, 34)], fill=255)
    b = mask_of(brim)
    band = set(p for p in h if 27 <= p[1] <= 31)
    allm = w | h | b
    halo(c, allm, OCC)
    drop(c, allm, 1, 2, OCC)
    skin = M["skin"]
    letter_col = rows_ramp([skin[0], skin[0], skin[1], skin[1], skin[2], skin[3]], 14, 60)
    paint(c, w, letter_col)
    # The hat is his: the title figure's blue wool, lit from the upper left with the bone rim
    # on its key side. Bone all through, the crown read as a sail or a chef's hat.
    wool = M["wool"]
    c.poly_shade([(21, 33), (42, 33), (39, 22), (37, 14), (38, 8), (42, 3), (44, 2), (39, 2),
                  (34, 6), (31, 13), (26, 24)], [wool[0], wool[1], wool[1], wool[2], wool[3]],
                 ang=0.35)
    paint(c, b, rows_ramp([wool[1], wool[2], wool[2], wool[3]], 32, 37))
    keyrim(c, h | b, RIM)
    gold = M["gold"]
    paint(c, band, rows_ramp([gold[0], gold[1], gold[1], gold[2], gold[3]], 27, 31))
    # the brim's shadow on the legs below it
    for x in range(16, 48):
        if (x, 38) in w:
            c.set(x, 38, skin[3])
        if (x, 39) in w:
            c.set(x, 39, skin[2])
    return c


# ---------------------------------------------------------------- B. WS in the ward
import font


def bg_b(c, n):
    night(c, n, warm=(0, 31, [M["gold"][4], M["gold"][4], M["violet"][4], None]))


def ward_ring(c, cx, cy, r, ticks=14, bright=True):
    A = WARD
    c.ring(cx, cy, r, r, A[3], 2)
    c.ring(cx, cy, r - 2, r - 2, A[2] if bright else A[3], 1)
    c.ring(cx, cy, r - 6, r - 6, A[3], 1)
    for i in range(ticks):
        a = i * 6.28318 / ticks + 0.22
        x = int(round(cx + math.cos(a) * (r - 4)))
        y = int(round(cy + math.sin(a) * (r - 4)))
        c.set(x, y, A[1])


def fg_b():
    c = blank()
    ward_ring(c, 31.5, 31.5, 30)
    # W and S in the title's own display face, at x2, set as the wordmark sets them
    W, Sg = font.DISPLAY["W"], font.DISPLAY["S"]
    x0 = 32 - (11 * 2 + 2 + 9 * 2) // 2
    y0 = 20
    wm = set()
    sm = set()
    for (g, ox, m) in ((W, x0, wm), (Sg, x0 + 24, sm)):
        for ry, row in enumerate(g):
            for rx, ch in enumerate(row):
                if ch == "#":
                    for sy in range(2):
                        for sx in range(2):
                            m.add((ox + rx * 2 + sx, y0 + ry * 2 + sy))
    allm = wm | sm
    halo(c, allm, OCC)
    drop(c, allm, 1, 2, OCC)
    skin, gold = M["skin"], M["gold"]
    paint(c, wm, rows_ramp([skin[0], skin[0], skin[1], skin[2], skin[3]], y0, y0 + 23))
    paint(c, sm, rows_ramp([gold[0], gold[1], gold[1], gold[2]], y0, y0 + 23))
    return c


# ---------------------------------------------------------------- C. Staff S
def bg_c(c, n):
    night(c, n, warm=(15.5, -22.5, 20, [M["gold"][3], M["gold"][3], M["gold"][4], M["violet"][4], None]))


def orb(c, cx, cy, r, glow=None):
    A = WARD
    if glow:
        radial(c, cx, cy, glow, [A[3], A[3], M["gold"][2], M["gold"][3], M["gold"][3], None])
    radial(c, cx, cy, r, [A[0], A[0], A[1], A[2], A[3]])


def glyph_mask(ch, x0, y0, scale):
    m = set()
    for ry, row in enumerate(font.DISPLAY[ch]):
        for rx, cell in enumerate(row):
            if cell == "#":
                for sy in range(scale):
                    for sx in range(scale):
                        m.add((x0 + rx * scale + sx, y0 + ry * scale + sy))
    return m


STAFF_C = [(17, 61), (43, 16)]


def fg_c():
    c = blank()
    gold, skin = M["gold"], M["skin"]
    # The S is the wordmark's own, at x3. A drawn S curve with any taper at all, wound round a
    # pole, is a snake on a stick - so this one is the slab letter, square-cut, with the two
    # short vertical spurs the display face gives it, and it is lit like type, not like scales.
    #
    # The staff runs on the DIAGONAL, as he holds it. Upright through the middle of an S it is
    # a dollar sign at every size, and that read wins over everything else in the picture.
    sm = glyph_mask("S", 18, 17, 3)

    def staffm(d):
        stroke(d, STAFF_C, 3)
    st = mask_of(staffm)

    def claw(d):
        stroke(d, [(43, 16), (41, 11), (42, 6)], 2)
        stroke(d, [(43, 16), (48, 15), (52, 11)], 2)
    cl = mask_of(claw)
    # over-under: the S passes in front of the staff at its two bowls and behind it at the spine
    spine = lambda x, y: 31 <= y <= 39
    halo(c, st | cl | sm, OCC)
    scol = rows_ramp([skin[0], skin[0], skin[1], skin[1], skin[2], skin[3]], 17, 52)
    paint(c, sm, scol)

    def staff_col(x, y):
        # lit on the key side (up-left of the shaft), shaded on the other
        lx = (x - STAFF_C[0][0]) * 48 - (y - STAFF_C[0][1]) * (-26)
        return gold[1] if lx < -20 else (gold[2] if lx < 30 else gold[3])
    st_front = set(p for p in st if spine(*p))
    halo(c, st_front, OCC, diagonal=False)
    paint(c, st_front | (st - sm) | cl, staff_col)
    keyrim(c, cl, gold[0])
    orb(c, 47, 9, 4.3, glow=9.5)
    return c


# ---------------------------------------------------------------- D. Hat and light
def bg_d(c, n):
    night(c, n, warm=(30, 34, [M["gold"][3], M["gold"][3], M["gold"][4], M["gold"][4],
                               M["violet"][4], None]))


HAT_D = [(15, 33), (48, 33), (41, 20), (38, 12), (40, 6), (46, 1), (48, 1), (42, 0),
         (35, 4), (31, 10), (24, 21)]


def fg_d():
    c = blank()
    wool, gold, skin = M["wool"], M["gold"], M["skin"]
    cn = mask_of(lambda d: d.polygon(HAT_D, fill=255))
    br = mask_of(lambda d: d.ellipse((3, 29, 60, 40), fill=255))
    # The face is a shadow under the brim, as on the title figure, and the two eyes are the
    # only thing in it. The beard hangs out of the bottom of it and catches the light from
    # below - that light is the whole reason the bottom of the icon is warm.
    fc = mask_of(lambda d: d.ellipse((17, 33, 46, 50), fill=255)) - br

    def beard(d):
        d.polygon([(18, 45), (45, 45), (44, 50), (40, 55), (35, 59), (32, 63), (31, 63),
                   (28, 59), (23, 55), (19, 50)], fill=255)
    bd = mask_of(beard)
    halo(c, cn | br | bd | fc, OCC)
    paint(c, fc, lambda x, y: OCC)
    # lit from below: the bottom of the beard is the warm end
    paint(c, bd, dith_ramp([skin[3], skin[2], skin[2], skin[1], gold[1], gold[0]], 45, 63))
    for x in range(18, 46):                       # the beard's top breaks into the shadow
        if (x * 5 + 1) % 3 != 0:
            c.set(x, 45, OCC)
        if (x * 7) % 5 == 0:
            c.set(x, 46, OCC)
    c.poly_shade(HAT_D, [wool[1], wool[2], wool[2], wool[3], wool[3]], ang=0.35)
    paint(c, br, dith_ramp([wool[2], wool[2], wool[3], wool[4]], 29, 40))
    for (x, y) in br:                             # brim underside, lit gold from below
        if (x, y + 1) not in br and 8 < x < 55:
            c.set(x, y, gold[2] if 16 < x < 47 else gold[3])
    band = set(p for p in cn if 28 <= p[1] <= 31)
    paint(c, band, rows_ramp([gold[0], gold[1], gold[2], gold[3]], 28, 31))
    keyrim(c, cn, RIM, skip=lambda x, y: 28 <= y <= 31)
    keyrim(c, br, RIM, skip=lambda x, y: (x, y - 1) in cn)
    for ex in (25, 36):                           # two lit eyes in the dark
        c.rect(ex, 42, ex + 2, 42, WARD[1])
        c.set(ex + 1, 42, WARD[0])
        c.rect(ex, 43, ex + 2, 43, WARD[3])
    return c


# ---------------------------------------------------------------- E. The burning ward
def bg_e(c, n):
    night(c, n, warm=(0, 31, [M["gold"][3], M["gold"][4], M["gold"][4], M["violet"][4], None]))


# A flame in the ward's ramp, drawn rather than computed: at 64 a flame is nine pixels tall
# and a disc stack comes out as a blob. The white core low and the gold edge high is what
# makes it read as burning upward.
FLAME9 = [
    "...e...",
    "...e...",
    "..eme..",
    "..ehe..",
    ".emhme.",
    ".ehwhe.",
    "emhwhme",
    ".ehwhe.",
    "..eme..",
]


def stamp_flame(c, x, yb):
    leg = {"e": WARD[3], "m": WARD[2], "h": WARD[1], "w": WARD[0]}
    grid(c, FLAME9, leg, int(round(x)) - 3, int(round(yb)) - 8)


def fg_e():
    c = blank()
    cx = cy = 31.5
    A = WARD
    c.ring(cx, cy, 29, 29, A[3], 2)
    c.ring(cx, cy, 27, 27, A[2], 1)
    c.ring(cx, cy, 21, 21, A[3], 1)
    for i in range(12):                           # runes between the two rings
        a = i * 6.28318 / 12 + 0.26
        x = cx + math.cos(a) * 24
        y = cy + math.sin(a) * 24
        c.vline(int(round(x)), y - 1, y + 1, A[2])
        c.set(int(round(x)), int(round(y)), A[1])
    star = [(cx + math.cos(i * 6.28318 / 6 - 1.5708) * 21, cy + math.sin(i * 6.28318 / 6 - 1.5708) * 21)
            for i in range(6)]
    for i in range(6):
        c.line(star[i][0], star[i][1], star[(i + 2) % 6][0], star[(i + 2) % 6][1], M["gold"][2])
    orb(c, cx, cy, 6.5, glow=13)
    for (x, y) in star:
        stamp_flame(c, x, y + 1)
    return c


# ---------------------------------------------------------------- F. Surrounded
def bg_f(c, n):
    night(c, n, warm=(0, 20, [M["gold"][3], M["gold"][3], M["gold"][4], M["gold"][4],
                              M["violet"][4], None]))


EYES_F = [  # (x, y, gap, near) - the nearer pairs are bigger and further in
    (9, 17, 7, 1), (42, 13, 7, 1), (11, 45, 7, 1), (40, 47, 7, 1),
    (4, 31, 4, 0), (56, 33, 4, 0), (29, 5, 4, 0), (30, 58, 4, 0),
]


def fg_f():
    c = blank()
    orb(c, 31.5, 31.5, 4.6, glow=10)
    for (x, y, gap, near) in EYES_F:
        for k, ox in enumerate((0, gap)):
            ex = x + ox
            if near:
                # 4x2 and slanted in toward the pair's middle - the brow is what makes two
                # lights a face rather than two embers. Smaller than this and the launcher's
                # 48px reduction leaves a dark disc with a few warm specks in it.
                radial(c, ex + 1.5, y + 0.5, 4.4, [M["red"][1], M["red"][1], M["red"][2], None])
                c.rect(ex, y, ex + 3, y + 1, FIRE[2])
                inner = 3 if k == 0 else 0
                outer = 0 if k == 0 else 3
                c.set(ex + 1, y + 1, FIRE[1])
                c.set(ex + 2, y + 1, FIRE[1])
                c.set(ex + outer, y, FIRE[3])
                c.set(ex + inner, y, M["red"][1])
                c.set(ex + outer, y - 1, FIRE[3])   # outer end raised: a scowl, not a worry
            else:
                radial(c, ex + 0.5, y + 0.5, 2.2, [M["red"][1], M["red"][2], None])
                c.rect(ex, y, ex + 1, y, FIRE[2])
    return c


DESIGNS = [
    ("A", "Hat-crowned W", bg_a, fg_a),
    ("B", "WS in the ward", bg_b, fg_b),
    ("C", "Staff S", bg_c, fg_c),
    ("D", "Hat and light", bg_d, fg_d),
    ("E", "The burning ward", bg_e, fg_e),
    ("F", "Surrounded", bg_f, fg_f),
]

# =========================================================================== hand-drawn 16s
# One character per pixel. Each design's legend maps characters to contract colours; a space
# is transparent (the tile's cut corners). These are DRAWN, not derived - see the module notes.
#
# The legend below is shared, so the same letter means the same colour in all six drawings and
# they can be compared cell for cell.
L16 = {
    "n": M["violet"][4],   # night
    "v": M["violet"][3],   # night, a shade lighter
    "o": OCC,
    "W": M["skin"][0], "L": M["skin"][1], "B": M["skin"][2], "D": M["skin"][3],
    "H": M["wool"][0], "h": M["wool"][1], "u": M["wool"][2], "U": M["wool"][3],
    "G": M["gold"][0], "g": M["gold"][1], "y": M["gold"][2], "q": M["gold"][3], "k": M["gold"][4],
    "w": WARD[0], "1": WARD[1], "2": WARD[2], "3": WARD[3],
    "f": FIRE[1], "r": FIRE[2], "e": FIRE[3], "R": M["red"][1],
    "*": RIM,
}

SMALL16 = {
    "A": [
        " nnnnnnnnnHnnnn ",
        "nnnnnnnnnHnnnnnn",
        "nnnnnnnnHhnnnnnn",
        "nnnnnnnHhunnnnnn",
        "nWWnnnnHhunnnWWn",
        "nWWnnnHhhunnnWWn",
        "nWWnnnGgggnnnWWn",
        "nWWnHhhhhuuunWWn",
        "nWWnnnnLLnnnnWWn",
        "nLLnnnLLLLnnnLLn",
        "nLLnnLLnnLLnnLLn",
        "nBBnnBBnnBBnnBBn",
        "nBBnBBnnnnBBnBBn",
        "nnDDDnnnnnnDDDnn",
        "nnnDnnnnnnnnDnnn",
        " nnnnnnnnnnnnnn ",
    ],
    "B": [
        " nnnn122222nnnn ",
        "nnn12kkkkkk33nnn",
        "nn1kkkkkkkkkk3nn",
        "n1kkkkkkkkkkkk3n",
        "n2kkkkkkkkkkkk3n",
        "2kkWkkkWkkGGGkk3",
        "2kkWkkkWkGkkkkk3",
        "2kkLkLkLkGkkkkk3",
        "3kkLkLkLkkggkkk3",
        "3kkBBkBBkkkkykk3",
        "3kkBkkkBkyyykkk3",
        "n3kkkkkkkkkkkk3n",
        "n3kkkkkkkkkkkk3n",
        "nn3kkkkkkkkkk3nn",
        "nnn33kkkkkk33nnn",
        " nnnn333333nnnn ",
    ],
    "C": [
        " nnnnnnnnnnkqqk ",
        "nnnnnnnnnnnqw1qn",
        "nnnnnnnnnnnq12qn",
        "nnnnnnnnnnngqknn",
        "nnnnWWWWWngnnnnn",
        "nnnWWnnnWWnnnnnn",
        "nnnLLnnngnnnnnnn",
        "nnnnLLLgLnnnnnnn",
        "nnnnnngnBBnnnnnn",
        "nnnBBgnnBBnnnnnn",
        "nnnnDDDDDnnnnnnn",
        "nnnynnnnnnnnnnnn",
        "nnynnnnnnnnnnnnn",
        "nynnnnnnnnnnnnnn",
        "ynnnnnnnnnnnnnnn",
        " nnnnnnnnnnnnnn ",
    ],
    "D": [
        " nnnnnnnnnnHnnn ",
        "nnnnnnnnnnHnnnnn",
        "nnnnnnnnnHhnnnnn",
        "nnnnnnnnHhunnnnn",
        "nnnnnnnHhhunnnnn",
        "nnnnnnHhhuuunnnn",
        "nnnnnGggggggnnnn",
        "nn*HhhhhhhuuuUnn",
        "nnnqyyyyyyyyqnnn",
        "nnnnoooooooonnnn",
        "nnnnoww1oww1onnn",
        "nnnnnBLLLLLBnnnn",
        "nnnnnnLLLLLnnnnn",
        "nnnnnkgGGGgknnnn",
        "nnnnnkkgGgkknnnn",
        " nnnkkkkgkkkkn  ",
    ],
    "E": [
        " nnnn333333nnnn ",
        "nnn33nn22nn33nnn",
        "nn3nnnn11nnnn3nn",
        "n3nnnnynnynnnn3n",
        "n3n2nnynnynn2n3n",
        "3nn1yyyyyyyy1nn3",
        "3nnynyn11nynynn3",
        "3nnnyn1ww1nynnn3",
        "3nnnyn1ww1nynnn3",
        "3nn2nyn11nyn2nn3",
        "3nn1yyyyyyyy1nn3",
        "n3nnnnynnynnnn3n",
        "n3nnnny22ynnnn3n",
        "nn3nnnn11nnnn3nn",
        "nnn33nnnnnn33nnn",
        " nnnn333333nnnn ",
    ],
    "F": [
        " nnnnnnnnnnnnnn ",
        "nnnnnnnnnnnnnnnn",
        "nnrnrnnnnnnrnrnn",
        "nnnnnnnnnnnnnnnn",
        "nnnnnnnnnnnnnnnn",
        "nnnnnnkkkknnnnnn",
        "nnnnnkq22qknnnnn",
        "enenkq2ww2qknene",
        "nnnnkq2ww2qknnnn",
        "nnnnnkq22qknnnnn",
        "nnnnnnkkkknnnnnn",
        "nnnnnnnnnnnnnnnn",
        "nnnnnnnnnnnnnnnn",
        "nnnrnrnnnnnnrnrn",
        "nnnnnnnnnnnnnnnn",
        " nnnnnnnnnnnnnn ",
    ],
}
SMALL16_LEGEND = L16
SMALL24 = {
    "A": [
        "  nnnnnnnnnnnnnHnnnnnn  ",
        " nnnnnnnnnnnnnHnnnnnnnn ",
        "nnnnnnnnnnnnnHhnnnnnnnnn",
        "nnnnnnnnnnnn*hunnnnnnnnn",
        "nnnnnnnnnnn*hhunnnnnnnnn",
        "nnnnnnnnnnn*hhuunnnnnnnn",
        "nnWWWnnnnn*hhhuunnnWWWnn",
        "nnWWWnnnnn*hhhuunnnWWWnn",
        "nnWWWnnnnGGGGGggnnnWWWnn",
        "nnWWWn*hhhhhhhhuuunWWWnn",
        "nnLLLnnUUUUUUUUUUnnLLLnn",
        "nnLLLnnnnnBLLBnnnnnLLLnn",
        "nnLLLnnnnnLLLLnnnnnLLLnn",
        "nnLLLnnnnLLLLLLnnnnLLLnn",
        "nnLLLnnnnLLnnLLnnnnLLLnn",
        "nnBBBnnnBBBnnBBBnnnBBBnn",
        "nnBBBnnnBBBnnBBBnnnBBBnn",
        "nnnBBBnBBBnnnnBBBnBBBnnn",
        "nnnnBBBBBBnnnnBBBBBBnnnn",
        "nnnnDDDDDnnnnnnDDDDDnnnn",
        "nnnnnDDDDnnnnnnDDDDnnnnn",
        "nnnnnnDDnnnnnnnnDDnnnnnn",
        " nnnnnnnnnnnnnnnnnnnnnn ",
        "  nnnnnnnnnnnnnnnnnnnn  ",
    ],
    "B": [
        "  nnnnnn22222222nnnnnn  ",
        " nnnn222kkkkkkkk222nnnn ",
        "nnnn2kkkkkkkkkkkkkk3nnnn",
        "nnn2kkkkkkkkkkkkkkkk3nnn",
        "nn2kkkkkkkkkkkkkkkkkk3nn",
        "n2kkkkkkkkkkkkkkkkkkkk3n",
        "n2kkkkkkkkkkkkkkkkkkkk3n",
        "2kkkkkkkkkkkkkkkkkkkkkk3",
        "2kkkkWWkkkWWkkGGGGkkkkk3",
        "2kkkkWWkkkWWkGGkkGGkkkk3",
        "2kkkkLLkkkLLkGGkkkkkkkk3",
        "2kkkkLLkLkLLkkgggkkkkkk3",
        "2kkkkLLkLkLLkkkgggkkkkk3",
        "2kkkkBBBBBBBkkkkkggkkkk3",
        "2kkkkBBBkBBBkyykkyykkkk3",
        "2kkkkkBkkkBkkkyyyykkkkk3",
        "2kkkkkkkkkkkkkkkkkkkkkk3",
        "n2kkkkkkkkkkkkkkkkkkkk3n",
        "n2kkkkkkkkkkkkkkkkkkkk3n",
        "nn3kkkkkkkkkkkkkkkkkk3nn",
        "nnn3kkkkkkkkkkkkkkkk3nnn",
        "nnnn3kkkkkkkkkkkkkk3nnnn",
        " nnnn333kkkkkkkk333nnnn ",
        "  nnnnnn33333333nnnnnn  ",
    ],
    "C": [
        "  nnnnnnnnnnnnnnkkkkkk  ",
        " nnnnnnnnnnnnnnkkk212kk ",
        "nnnnnnnnnnnnnnnkkk1w1kkk",
        "nnnnnnnnnnnnnnnkgk212kyk",
        "nnnnnnnnnnnnnnnkkgkkkgkk",
        "nnnnnnnnnnnnnnnkkkgykkkk",
        "nnnnnnnnnnnnnnnnkgykkkkn",
        "nnnnnnnnnnnnnnnngykkkknn",
        "nnnnnnnnWWWWWWWgynnnnnnn",
        "nnnnnnnWWWWWWWWWnnnnnnnn",
        "nnnnnnnWWnnnngWWnnnnnnnn",
        "nnnnnnnWWnnngynnnnnnnnnn",
        "nnnnnnnLLLngynnnnnnnnnnn",
        "nnnnnnnnLLgyLLnnnnnnnnnn",
        "nnnnnnnnngyLLLLnnnnnnnnn",
        "nnnnnnnngynnnLLLnnnnnnnn",
        "nnnnnnngynnnnnBBnnnnnnnn",
        "nnnnnngBBnnnnnBBnnnnnnnn",
        "nnnnngyDDDDDDDDDnnnnnnnn",
        "nnnngynnDDDDDDDnnnnnnnnn",
        "nnngynnnnnnnnnnnnnnnnnnn",
        "nngynnnnnnnnnnnnnnnnnnnn",
        " gynnnnnnnnnnnnnnnnnnnn ",
        "  nnnnnnnnnnnnnnnnnnnn  ",
    ],
    "D": [
        "  nnnnnnnnnnnnnnHnnnnn  ",
        " nnnnnnnnnnnnnnHnnnnnnn ",
        "nnnnnnnnnnnnnnHhnnnnnnnn",
        "nnnnnnnnnnnnn*Hunnnnnnnn",
        "nnnnnnnnnnnn*hhunnnnnnnn",
        "nnnnnnnnnnn*hhhunnnnnnnn",
        "nnnnnnnnnn*hhhuunnnnnnnn",
        "nnnnnnnnn*hhhhuuunnnnnnn",
        "nnnnnnnn*hhhhuuuunnnnnnn",
        "nnnnnnnGGGGGGGGgggnnnnnn",
        "nnn*Hhhhhhhhhhhhuuuuunnn",
        "n*hhhhhuuuuuuuuuuuuUUUUn",
        "nnnUyyyyyyyyyyyyyyyyUnnn",
        "nnnnnoooooooooooooonnnnn",
        "nnnnnoo1woooooow1onnnnnn",
        "nnnnnnoDoDDoDDoDoDnnnnnn",
        "nnnnnnDBBBBBBBBBBDnnnnnn",
        "nnnnnnnDBBLLLLBBDnnnnnnn",
        "nnnnnnnBLLLLLLLLBnnnnnnn",
        "nnnnnnnkBLLggLLBknnnnnnn",
        "nnnnnnkkkLgGGgLkkknnnnnn",
        "nnnnnkkkkkgGGgkkkkknnnnn",
        " nnnkkkkkkkGGkkkkkkknnn ",
        "  nnkkkkkkkggkkkkkkknn  ",
    ],
    "E": [
        "  nnnnnn22222222nnnnnn  ",
        " nnnn222nnn3nnnn222nnnn ",
        "nnnn2nnnnnn23nnnnnn3nnnn",
        "nnn2nnnnnnn11nnnnnnn3nnn",
        "nn2nnnnnnnnyynnnnnnnn3nn",
        "n2nn3nnnnnynnynnnnn3nn3n",
        "n2nn1nnnnnynnynnnnn1nn3n",
        "2nnnwyyyyyyyyyyyyyywnnn3",
        "2nnnnynnnynnnnynnnynnnn3",
        "2nnnnynnynnqqnnynnynnnn3",
        "2nnnnnyynn2112nnyynnnnn3",
        "2nnnnnyynq1ww1qnyynnnnn3",
        "2nnnnnyynq1ww1qnyynnnnn3",
        "2nnnnnyynn2112nnyynnnnn3",
        "2nnn3ynnynnqqnnynny3nnn3",
        "2nnn1ynnnynnnnynnny1nnn3",
        "2nnnwyyyyyyyyyyyyyywnnn3",
        "n2nnnnnnnnynnynnnnnnnn3n",
        "n2nnnnnnnny3nynnnnnnnn3n",
        "nn3nnnnnnnn23nnnnnnnn3nn",
        "nnn3nnnnnnn11nnnnnnn3nnn",
        "nnnn3nnnnnnnnnnnnnn3nnnn",
        " nnnn333nnnnnnnn333nnnn ",
        "  nnnnnn33333333nnnnnn  ",
    ],
    "F": [
        "  nnnnnnnnnnnnnnnnnnnn  ",
        " nnnnnnnnnenennnnnnnnnn ",
        "nnnnnnnnnnnnnnnnnnnnnnnn",
        "nnnnnnnnnnnnnnnnnnnnnnnn",
        "nnnnnnnnnnnnnnnnRnnnRnnn",
        "nnnRnnnRnnnnnnnnrfnfrnnn",
        "nnnrfnfrnnnkknnnnnnnnnnn",
        "nnnnnnnnnkkkkkknnnnnnnnn",
        "nnnnnnnnkkkkkkkknnnnnnnn",
        "nnnnnnnkkkqqqqkkknnnnnnn",
        "nnnnnnnkkq2112qkknnnnnnn",
        "nnnnnnkkkq1ww1qkkknnnene",
        "enennnkkkq1ww1qkkknnnnnn",
        "nnnnnnnkkq2112qkknnnnnnn",
        "nnnnnnnkkkqqqqkkknnnnnnn",
        "nnnnnnnnkkkkkkkknnnnnnnn",
        "nnnnRnnnRkkkkkknRnnnRnnn",
        "nnnnrfnfrnnkknnnrfnfrnnn",
        "nnnnnnnnnnnnnnnnnnnnnnnn",
        "nnnnnnnnnnnnnnnnnnnnnnnn",
        "nnnnnnnnnnnnnnnnnnnnnnnn",
        "nnnnnnnnnnnnnnnnnnnnnnnn",
        " nnnnnnnnnnenennnnnnnnn ",
        "  nnnnnnnnnnnnnnnnnnnn  ",
    ],
}


# ============================================================================== composition
TILE_R64 = 10
TILE_R16 = 3


def compose_master(bg, fg):
    c = blank()
    bg(c, S)
    out = c.img.copy()
    out.alpha_composite(fg.img)
    return out


def compose_adaptive(bg, fg):
    c = blank(ADAPT)
    bg(c, ADAPT)
    bgi = c.img.copy()
    fgi = Image.new("RGBA", (ADAPT, ADAPT), (0, 0, 0, 0))
    off = (ADAPT - S) // 2
    fgi.paste(fg.img, (off, off))
    full = bgi.copy()
    full.alpha_composite(fgi)
    return bgi, fgi, full


def mask_shape(img, shape):
    """Mask a square image to a circle or a rounded square (Android's two common masks)."""
    n = img.size[0]
    m = Image.new("L", (n * 4, n * 4), 0)
    d = ImageDraw.Draw(m)
    if shape == "circle":
        d.ellipse((0, 0, n * 4 - 1, n * 4 - 1), fill=255)
    else:
        r = int(n * 4 * 0.22)
        d.rounded_rectangle((0, 0, n * 4 - 1, n * 4 - 1), r, fill=255) \
            if hasattr(d, "rounded_rectangle") else _rrect(d, n * 4, r)
    m = m.resize((n, n), Image.BOX)
    out = img.copy()
    out.putalpha(m)
    return out


def _rrect(d, n, r):
    d.rectangle((r, 0, n - 1 - r, n - 1), fill=255)
    d.rectangle((0, r, n - 1, n - 1 - r), fill=255)
    for (x, y) in ((0, 0), (n - 1 - 2 * r, 0), (0, n - 1 - 2 * r), (n - 1 - 2 * r, n - 1 - 2 * r)):
        d.ellipse((x, y, x + 2 * r, y + 2 * r), fill=255)


def small_from(master, size):
    return snap(master.resize((size, size), Image.BOX))


def make_16(key, master):
    if key in SMALL16:
        rows, legend = SMALL16[key], SMALL16_LEGEND
        for i, row in enumerate(rows):
            if len(row) != 16:
                raise SystemExit("%s 16px row %d is %d wide" % (key, i, len(row)))
        c = blank(16)
        grid(c, rows, legend)
        return c.img
    return apply_tile(small_from(master, 16), TILE_R16)


def make_24(key, master):
    if key in SMALL24:
        rows, legend = SMALL24[key], SMALL16_LEGEND
        for i, row in enumerate(rows):
            if len(row) != 24:
                raise SystemExit("%s 24px row %d is %d wide" % (key, i, len(row)))
        c = blank(24)
        grid(c, rows, legend)
        return c.img
    return apply_tile(small_from(master, 24), 4)


# ================================================================================ the sheet
INK = (210, 214, 224, 255)
DIM = (120, 126, 140, 255)
SHEET_BG = (30, 32, 38, 255)


def label(d, xy, text, fill=INK):
    d.text(xy, text, fill=fill)


def wallpaper(w, h, dark):
    im = Image.new("RGBA", (w, h))
    px = im.load()
    for y in range(h):
        for x in range(w):
            t = (x + y) / float(w + h)
            if dark:
                px[x, y] = (int(22 + 30 * t), int(30 + 20 * t), int(52 + 30 * t), 255)
            else:
                px[x, y] = (int(214 - 20 * t), int(222 - 14 * t), int(232 - 8 * t), 255)
    return im


def taskbar(icon24, dark):
    w, h = 300, 48
    im = Image.new("RGBA", (w, h), (32, 32, 32, 255) if dark else (238, 238, 238, 255))
    d = ImageDraw.Draw(im)
    others = [(96, 156, 220), (230, 180, 60), (90, 180, 110), (200, 90, 90)]
    x = 22
    for i in range(6):
        if i == 3:
            im.alpha_composite(icon24, (x, 12))
            # the open-app pip under the running icon
            d.rectangle((x + 8, 43, x + 15, 44), fill=(120, 170, 230) if dark else (0, 95, 184))
        else:
            col = others[i % len(others)]
            _rr = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
            dd = ImageDraw.Draw(_rr)
            dd.rectangle((2, 2, 21, 21), fill=col + (255,))
            im.alpha_composite(_rr, (x, 12))
        x += 44
    return im


def build_sheet(results):
    row_h = 360
    width = 1520
    head = 36
    sheet = Image.new("RGBA", (width, head + row_h * len(results)), SHEET_BG)
    d = ImageDraw.Draw(sheet)
    label(d, (12, 12), "Wizard Survivors - icon concepts.  Left: 64px master x4.  Middle: 48/32/24/16 at 1x"
                       " with x4 beside each, Windows taskbar at 24px.  Right: Android adaptive "
                       "(108-unit canvas, 72 visible), circle and rounded-square masks, x2 and ~48px.")
    for i, r in enumerate(results):
        y0 = head + i * row_h
        d.line((0, y0, width, y0), fill=(54, 58, 68, 255))
        label(d, (12, y0 + 8), "%s   %s" % (r["key"], r["name"]))
        sheet.alpha_composite(r["master"].resize((256, 256), Image.NEAREST), (12, y0 + 28))
        label(d, (12, y0 + 290), "64 master, x4", DIM)
        # small sizes, each at 1x and at x4
        x = 290
        for size in (48, 32, 24, 16):
            im = r["sizes"][size]
            sheet.alpha_composite(im, (x, y0 + 28))
            label(d, (x, y0 + 10), "%d" % size, DIM)
            x += size + 8
            sheet.alpha_composite(im.resize((size * 4, size * 4), Image.NEAREST), (x, y0 + 28))
            x += size * 4 + 18
        # taskbars, at 1x - the honest test
        sheet.alpha_composite(taskbar(r["sizes"][24], True), (290, y0 + 250))
        sheet.alpha_composite(taskbar(r["sizes"][24], False), (600, y0 + 250))
        label(d, (290, y0 + 302), "taskbar, 24px at 1x (dark / light)", DIM)
        # Android
        ax = 1020
        for j, dark in enumerate((True, False)):
            sy = y0 + 20 + j * 168
            strip = wallpaper(486, 160, dark)
            view = r["adaptive"].crop(((ADAPT - VIEW) // 2, (ADAPT - VIEW) // 2,
                                       (ADAPT + VIEW) // 2, (ADAPT + VIEW) // 2))
            big = view.resize((VIEW * 2, VIEW * 2), Image.NEAREST)
            small = view.resize((48, 48), Image.BOX)
            strip.alpha_composite(mask_shape(big, "circle"), (8, 8))
            strip.alpha_composite(mask_shape(big, "rrect"), (160, 8))
            strip.alpha_composite(mask_shape(small, "circle"), (318, 56))
            strip.alpha_composite(mask_shape(small, "rrect"), (382, 56))
            # a neighbouring launcher icon at the same small size, for scale
            nb = Image.new("RGBA", (48, 48), (90, 150, 220, 255))
            strip.alpha_composite(mask_shape(nb, "circle"), (434, 56))
            sheet.alpha_composite(strip, (ax, sy))
        label(d, (ax, y0 + 340), "Android: circle / squircle at x2, then at ~48px (dark / light wallpaper)", DIM)
    return sheet


def build_sheet16(results):
    z = 8
    cell = 16 * z
    width = 24 + len(results) * (16 + 12 + cell + 28)
    sheet = Image.new("RGBA", (width, cell + 70), SHEET_BG)
    d = ImageDraw.Draw(sheet)
    x = 16
    for r in results:
        im = r["sizes"][16]
        label(d, (x, 10), "%s  %s" % (r["key"], r["name"]))
        sheet.alpha_composite(im, (x, 40))
        # and on a light square, the way a light taskbar shows it
        lt = Image.new("RGBA", (20, 20), (238, 238, 238, 255))
        lt.alpha_composite(im, (2, 2))
        sheet.alpha_composite(lt, (x - 2, 64))
        sheet.alpha_composite(im.resize((cell, cell), Image.NEAREST), (x + 28, 40))
        x += 16 + 12 + cell + 28
    return sheet


def build(key):
    """Everything one design produces. app_icon.py calls this for the shipping design."""
    for k, name, bg, fgf in DESIGNS:
        if k == key:
            break
    else:
        raise SystemExit("no icon design %r" % key)
    fg = fgf()
    master = compose_master(bg, fg)
    bad = raster.Canvas(S, S)
    bad.img = master
    bad.px = master.load()
    off = bad.audit()
    tile = apply_tile(master, TILE_R64)
    bgi, fgi, full = compose_adaptive(bg, fg)
    sizes = {
        64: tile,
        48: apply_tile(small_from(master, 48), 8),
        32: apply_tile(small_from(master, 32), 5),
        24: make_24(key, master),
        16: make_16(key, master),
    }
    return {"name": name, "master": master, "tile": tile, "sizes": sizes,
            "bg": bgi, "fg": fgi, "adaptive": full, "off": off}


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    results = []
    for key, name, bg, fgf in DESIGNS:
        b = build(key)
        tile, sizes, fgi, bgi, full, off = b["tile"], b["sizes"], b["fg"], b["bg"], b["adaptive"], b["off"]
        tile.save(os.path.join(OUT, "%s_64.png" % key))
        sizes[16].save(os.path.join(OUT, "%s_16.png" % key))
        sizes[24].save(os.path.join(OUT, "%s_24.png" % key))
        fgi.save(os.path.join(OUT, "%s_fg.png" % key))
        bgi.save(os.path.join(OUT, "%s_bg.png" % key))
        print("%s  %-18s off-contract colours: %d%s" % (key, name, len(off),
                                                        "" if not off else "  " + str(off[:4])))
        results.append({"key": key, "name": name, "master": tile, "sizes": sizes,
                        "adaptive": full})
    build_sheet(results).save(os.path.join(OUT, "sheet.png"))
    build_sheet16(results).save(os.path.join(OUT, "sheet_16.png"))
    print("wrote %s" % os.path.join(OUT, "sheet.png"))
    print("wrote %s" % os.path.join(OUT, "sheet_16.png"))


if __name__ == "__main__":
    main()
