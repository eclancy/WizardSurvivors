# -*- coding: utf-8 -*-
"""Raster helpers for large Bonelight compositions (splash screens, banners).

The sprite generators author on character grids because a 32x32 cell can be typed out. A
360x640 splash cannot be, so this module draws with shapes instead - but under the same
constraint as the grids: every pixel it writes is a colour that exists in
.ai/art-direction.md section 3. audit() reports anything that is not, rather than trusting the
drawing code to have behaved.

Gradients are ordered-dithered between two adjacent palette tones. That is deliberate: a true
blend would invent hundreds of off-contract colours, and the audit that produced the contract
found 935 colours in the old art as its headline failure.
"""

import math

from PIL import Image

import bonelight as bl

# Ordered 8x8 Bayer threshold matrix, values 0..63.
BAYER8 = [
    [0, 32, 8, 40, 2, 34, 10, 42],
    [48, 16, 56, 24, 50, 18, 58, 26],
    [12, 44, 4, 36, 14, 46, 6, 38],
    [60, 28, 52, 20, 62, 30, 54, 22],
    [3, 35, 11, 43, 1, 33, 9, 41],
    [51, 19, 59, 27, 49, 17, 57, 25],
    [15, 47, 7, 39, 13, 45, 5, 37],
    [63, 31, 55, 23, 61, 29, 53, 21],
]


def rgb(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


def allowed():
    """Every colour the contract permits, as a hex -> name map, for audit()."""
    out = {bl.OCC.upper(): "occlusion", bl.RIM.upper(): "rim/key", bl.AMBIENT.upper(): "ambient"}
    for mat, tones in bl.MATERIALS.items():
        for i, t in enumerate(tones):
            out.setdefault(t.upper(), mat + "." + bl.ROLES[i])
    for el, tones in bl.ELEMENTS.items():
        for i, t in enumerate(tones):
            out.setdefault(t.upper(), el + "." + ["core", "hot", "mid", "edge"][i])
    return out


class Canvas(object):
    def __init__(self, w, h, bg=None):
        self.w = w
        self.h = h
        self.img = Image.new("RGBA", (w, h), rgb(bg) if bg else (0, 0, 0, 0))
        self.px = self.img.load()

    # --- primitives ---------------------------------------------------------
    def set(self, x, y, c):
        if c is None:
            return
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = rgb(c) if isinstance(c, str) else c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return (0, 0, 0, 0)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                self.set(x, y, c)

    def hline(self, x0, x1, y, c):
        for x in range(int(min(x0, x1)), int(max(x0, x1)) + 1):
            self.set(x, y, c)

    def vline(self, x, y0, y1, c):
        for y in range(int(min(y0, y1)), int(max(y0, y1)) + 1):
            self.set(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
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

    def disc(self, cx, cy, rx, ry, c):
        for y in range(int(cy - ry), int(cy + ry) + 1):
            for x in range(int(cx - rx), int(cx + rx) + 1):
                nx = (x - cx) / float(rx)
                ny = (y - cy) / float(ry)
                if nx * nx + ny * ny <= 1.0:
                    self.set(x, y, c)

    def ring(self, cx, cy, rx, ry, c, thick=1):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                nx = (x - cx) / float(rx)
                ny = (y - cy) / float(ry)
                d = math.sqrt(nx * nx + ny * ny)
                if 1.0 - (thick / float(rx)) <= d <= 1.0:
                    self.set(x, y, c)

    def poly(self, pts, c):
        """Scanline fill of a closed polygon given as [(x, y), ...]."""
        if len(pts) < 3:
            return
        ys = [p[1] for p in pts]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            xs = []
            n = len(pts)
            for i in range(n):
                ax, ay = pts[i]
                bx, by = pts[(i + 1) % n]
                if ay == by:
                    continue
                if min(ay, by) <= y < max(ay, by):
                    xs.append(ax + (y - ay) * (bx - ax) / float(by - ay))
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                self.hline(int(math.floor(xs[i])), int(math.ceil(xs[i + 1])), y, c)

    def poly_shade(self, pts, colors, ang=-0.7854, bias=0.0, gamma=1.0):
        """Fill a polygon with a directional dithered ramp instead of flat colour.

        Nested flat polygons were the first attempt and they read as stacked plates - the
        boundary between two tones is a hard edge exactly where a form should be turning.
        Projecting each pixel onto the key axis and dithering between adjacent tones gives a
        turning form while still spending only palette colours.

        colors run light-to-dark along the axis, so gamma BELOW 1 biases the fill toward the
        dark end and gamma above 1 toward the light. It reads backwards; it is worth checking
        against a render rather than reasoning about it.
        """
        import math as _m
        if len(pts) < 3:
            return
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        ux, uy = _m.cos(ang), _m.sin(ang)
        proj = [x * ux + y * uy for (x, y) in zip(xs, ys)]
        lo, hi = min(proj), max(proj)
        span = max(1e-6, hi - lo)
        n = len(colors)
        for y in range(int(min(ys)), int(max(ys)) + 1):
            xints = []
            m = len(pts)
            for i in range(m):
                ax, ay = pts[i]
                bx, by = pts[(i + 1) % m]
                if ay == by:
                    continue
                if min(ay, by) <= y < max(ay, by):
                    xints.append(ax + (y - ay) * (bx - ax) / float(by - ay))
            xints.sort()
            for k in range(0, len(xints) - 1, 2):
                for x in range(int(_m.floor(xints[k])), int(_m.ceil(xints[k + 1])) + 1):
                    t = ((x * ux + y * uy) - lo) / span
                    t = min(1.0, max(0.0, pow(min(1.0, max(0.0, t + bias)), gamma))) * (n - 1)
                    i = min(n - 2, int(t))
                    f = t - i
                    th = BAYER8[y % 8][x % 8] / 64.0
                    self.set(x, y, colors[i + 1] if f > th else colors[i])

    # --- dithered blends ----------------------------------------------------
    def vramp(self, y0, y1, colors, x0=0, x1=None):
        """Vertical ordered-dither gradient through colors, top to bottom."""
        if x1 is None:
            x1 = self.w - 1
        n = len(colors)
        span = max(1, y1 - y0)
        for y in range(int(y0), int(y1) + 1):
            t = (y - y0) / float(span) * (n - 1)
            i = min(n - 2, int(t))
            f = t - i
            for x in range(int(x0), int(x1) + 1):
                th = BAYER8[y % 8][x % 8] / 64.0
                self.set(x, y, colors[i + 1] if f > th else colors[i])

    def radial(self, cx, cy, rx, ry, colors):
        """Dithered radial ramp, centre to edge. A trailing None fades into what is under it."""
        n = len(colors)
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                nx = (x - cx) / float(rx)
                ny = (y - cy) / float(ry)
                d = math.sqrt(nx * nx + ny * ny)
                if d > 1.0:
                    continue
                t = d * (n - 1)
                i = min(n - 2, int(t))
                f = t - i
                th = BAYER8[y % 8][x % 8] / 64.0
                self.set(x, y, colors[i + 1] if f > th else colors[i])

    def vignette(self, colors, power=1.6):
        """Darken toward the frame edge through colors, edge-most last. None leaves a pixel alone."""
        cx = self.w / 2.0
        cy = self.h / 2.0
        maxd = math.sqrt(cx * cx + cy * cy)
        n = len(colors)
        for y in range(self.h):
            for x in range(self.w):
                dx = (x - cx)
                dy = (y - cy)
                d = math.sqrt(dx * dx + dy * dy) / maxd
                t = pow(d, power) * n
                i = int(t)
                if i >= n:
                    i = n - 1
                f = t - i
                th = BAYER8[y % 8][x % 8] / 64.0
                idx = i if f > th else i - 1
                if 0 <= idx < n:
                    self.set(x, y, colors[idx])

    # --- io -----------------------------------------------------------------
    def blit(self, img, x, y, tint=None):
        src = img.load()
        for sy in range(img.size[1]):
            for sx in range(img.size[0]):
                p = src[sx, sy]
                if len(p) < 4 or p[3] == 0:
                    continue
                self.set(x + sx, y + sy, tint if tint else p)

    def scaled(self, factor):
        return self.img.resize((self.w * factor, self.h * factor), Image.NEAREST)

    def save(self, path):
        self.img.save(path)

    def audit(self):
        """Return a sorted list of (hex, count) for colours the contract does not define."""
        ok = allowed()
        bad = {}
        for y in range(self.h):
            for x in range(self.w):
                p = self.px[x, y]
                if p[3] == 0:
                    continue
                h = "#%02X%02X%02X" % (p[0], p[1], p[2])
                if h not in ok:
                    bad[h] = bad.get(h, 0) + 1
        return sorted(bad.items(), key=lambda kv: -kv[1])
