# -*- coding: utf-8 -*-
"""The moot's stones, for the Broken Circle's side event (MootFiresEvent). Run: python tools/art/explore_moot.py

Writes assets/bonelight/world/heath/moot-stone.png (two frames side by side: the ward-rune dark, and
lit), moot-socket.png (the empty hole where the ninth stone stood) and moot.json, each authored 1:1
and saved at x2 like every world sprite, with each sprite's ground line and foot column in pixels of
the PNG so the event and the art agree without a constant in either.

"The circle where the wizards met." Eight stones still stand where the moot was held; the ninth
walks (the boss). These are not the heath's wild stones (props_heath.py): they were SET - dressed
square-shouldered, taller, all of one height - and each has a ward-rune cut into its face, a ring
with a stroke through it, the mark of the wizards' circle. Dark, the rune is a groove with a lit lip.
Lit, its strokes burn in the light ramp: the ward is the wizards' and not his, so it is the one warm
light the event makes (its far tell, while it waits, is his - cold - like every event's).

The socket is a decal on the ground: torn turf round a dark hollow, roots in its sides, where a
stone was pulled out. The boss's own root-ball is the other half of it.
"""
from __future__ import print_function

import io
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bonelight as bl
import raster
import trees as T

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "heath")
SCALE = 2
FOOT = 4

ST = T.ST
SK = T.SK
LN = T.LN
VE = T.VE
VI = T.VI
LI = T.ramp("lichen")
LIGHT = bl.ELEMENTS["light"]       # core, hot, mid, edge
GO = T.GO

W, H = 26, 56


def stone(lit):
    # On trees.Bleed: the shade pool reaches past the 26 px box on the right, where a plain canvas
    # sliced it off along the frame's edge (tools/art/clip_check.py). main() fits both frames to one box.
    c = T.Bleed(W, H)
    base = H - FOOT - 1
    cx = W / 2.0
    T.contact(c, cx + 1, base + 1, 11)
    T.shade_pool(c, cx + 3, base + 1, 13, 2.5, density=0.5)
    granite = [SK[3], SK[4], ST[0], ST[1], ST[2], ST[3], ST[4]]
    rows = []
    for y in range(4, base + 1):
        t = (y - 4) / float(base - 4)
        hw = 8.0 + 1.6 * t + (0.6 if t > 0.92 else 0.0)
        rows.append((y, int(T.round2(cx - hw)), int(T.round2(cx + hw))))
    for (y, l, r) in rows:
        a1 = l + 5
        a2 = r - 4
        for x in range(l, r + 1):
            if x < a1:
                col = granite[1] if (x * 3 + y) % 7 else granite[0]
            elif x < a2:
                col = granite[2] if (x + 2 * y) % 5 else granite[1]
            else:
                col = granite[4] if (x + y) % 4 else granite[5]
            if T.hash2(x, y, 517) < 0.06:
                col = granite[0] if x < a2 else granite[2]
            c.set(x, y, col)
        c.set(a2, y, granite[1] if y % 5 else granite[2])
        c.set(a2 + 1, y, granite[5])
        c.set(r, y, granite[6])
    # The dressed top: square-shouldered, a lit sliver of its top face.
    for x in range(rows[0][1] + 1, rows[0][2]):
        c.set(x, 4, granite[0] if x % 3 else granite[1])
        c.set(x, 5, granite[1])
    c.set(rows[0][1], 4, (0, 0, 0, 0))
    c.set(rows[0][2], 4, (0, 0, 0, 0))
    # Lichen on the weather side, and a crack.
    for (x, y) in ((18, 12), (19, 30), (17, 41)):
        c.set(x, y, LI[2])
        c.set(x + 1, y, LI[3])
        c.set(x, y + 1, LI[3])
        c.set(x - 1, y, LI[1])
    for (x, y) in ((8, 36), (9, 37), (9, 38), (10, 39)):
        c.set(x, y, ST[4])
    # THE RUNE: a ring with a stroke through it and two short ticks, cut into the front face.
    rx, ry, rr = 13, 20, 4.2
    pts = []
    for k in range(0, 360, 12):
        a = math.radians(k)
        pts.append((int(T.round2(rx + rr * math.cos(a))), int(T.round2(ry + rr * 1.1 * math.sin(a)))))
    for y in range(ry - 9, ry + 10):
        pts.append((rx, y))
    for (x, y) in ((rx - 3, ry + 8), (rx - 2, ry + 9), (rx + 3, ry + 8), (rx + 2, ry + 9),
                   (rx - 2, ry - 8), (rx + 2, ry - 8)):
        pts.append((x, y))
    pts = sorted(set(pts))
    for (x, y) in pts:
        if lit:
            c.set(x, y, LIGHT[1] if (x + y) % 3 else LIGHT[0])
        else:
            c.set(x, y, ST[4])
            if (x, y - 1) not in pts:
                c.set(x, y - 1, granite[0])          # the groove's lit lip
    if lit:
        # The light bleeds into the stone round the cut: the light ramp's mid and edge on the faces.
        for (x, y) in pts:
            for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if (x + dx, y + dy) not in pts and c.get(x + dx, y + dy)[3]:
                    c.set(x + dx, y + dy, LIGHT[2] if (x + dx + y) % 2 else GO[1])
    # Heather at its foot.
    rnd = T.Rng(523)
    for x in range(3, W - 3):
        if rnd.random() < 0.6:
            h = rnd.randrange(1, 4)
            for j in range(h):
                c.set(x, base - j, VE[4] if j < h - 1 else (VI[1] if x % 2 else VI[2]))
    return c


def socket():
    """The ninth stone's place: a hollow in the heath where a stone was pulled up, torn turf round
    it and roots in its sides. Lying on the ground, so it carries no rim and no rise."""
    c = raster.Canvas(30, 18, None)
    cx, cy = 15.0, 9.0
    for y in range(18):
        for x in range(30):
            nx, ny = (x - cx) / 14.0, (y - cy) / 8.0
            d = nx * nx + ny * ny
            if d > 1.0:
                continue
            if d > 0.72:
                col = LN[3] if (x + y) % 3 else ST[3]            # the torn turf lip
                if d > 0.86 and T.hash2(x, y, 531) < 0.4:
                    col = VI[3]
            elif d > 0.5:
                col = ST[4] if y > cy else LN[4]
            else:
                col = OCC if y > cy - 2 else ST[4]
            c.set(x, y, col)
    for (x0, y0, x1, y1) in ((6, 6, 9, 9), (22, 5, 20, 9), (12, 3, 13, 7), (19, 12, 17, 10)):
        n = max(abs(x1 - x0), abs(y1 - y0))
        for i in range(n + 1):
            t = i / float(n)
            c.set(int(T.round2(x0 + (x1 - x0) * t)), int(T.round2(y0 + (y1 - y0) * t)), SK[4])
    for (x, y) in ((3, 9), (26, 8), (8, 15), (21, 15)):
        c.set(x, y, LN[2])
        c.set(x + 1, y, LN[3])
    return c


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    dark, lit = stone(False), stone(True)
    for name, cv in (("dark", dark), ("lit", lit)):
        bad = cv.audit()
        if bad:
            print("OFF-CONTRACT COLOURS in the %s moot stone: %s" % (name, bad[:4]))
            return 1
    # Both frames fitted to the one box that holds either (trees.fit grows a box, never shrinks it),
    # and the manifest's numbers moved with the box, so the foot and the rune stay where they were.
    fd, oxd, oyd = T.fit(dark)
    fl, oxl, oyl = T.fit(lit)
    ox, oy = max(oxd, oxl), max(oyd, oyl)
    fw = max(fd.w - oxd, fl.w - oxl) + ox
    fh = max(fd.h - oyd, fl.h - oyl) + oy
    strip = raster.Canvas(fw * 2, fh, None)
    strip.img.paste(fd.img, (ox - oxd, oy - oyd))
    strip.img.paste(fl.img, (fw + ox - oxl, oy - oyl))
    strip.scaled(SCALE).save(os.path.join(OUT, "moot-stone.png"))
    sock = socket()
    bad = sock.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in the socket: %s" % bad[:4])
        return 1
    sock.scaled(SCALE).save(os.path.join(OUT, "moot-socket.png"))
    manifest = (
        '{\n'
        '  "_comment": "Generated by tools/art/explore_moot.py - the moot\'s stones for MootFiresEvent. '
        'stone: two frames side by side (rune dark, rune lit); ground is the contact line and anchor_x '
        'the foot column of a frame, rune_y the rune\'s centre, in pixels of the PNG.",\n'
        '  "stone": {"path": "res://assets/bonelight/world/heath/moot-stone.png", "frames": 2, '
        '"frame_w": %d, "frame_h": %d, "ground": %d, "anchor_x": %d, "rune_y": %d},\n'
        '  "socket": {"path": "res://assets/bonelight/world/heath/moot-socket.png", "width": %d, "height": %d}\n'
        '}\n' % (fw * SCALE, fh * SCALE, (H - FOOT + oy) * SCALE, (W // 2 + ox) * SCALE + 1, (20 + oy) * SCALE,
                 sock.w * SCALE, sock.h * SCALE))
    T.write_text(os.path.join(OUT, "moot.json"), manifest)
    print("  moot-stone   %dx%d a frame on screen, 2 frames" % (fw * SCALE, fh * SCALE))
    print("  moot-socket  %dx%d on screen" % (sock.w * SCALE, sock.h * SCALE))
    from PIL import Image
    prev = Image.new("RGBA", (fw * 2 + sock.w + 10, max(fh, sock.h + 4)), (34, 28, 51, 255))
    prev.alpha_composite(strip.img, (0, 0))
    prev.alpha_composite(sock.img, (fw * 2 + 6, max(fh, sock.h + 4) - sock.h - 4))
    prev.resize((prev.size[0] * 6, prev.size[1] * 6), Image.NEAREST).save(os.path.join(HERE, "_heath_moot.png"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
