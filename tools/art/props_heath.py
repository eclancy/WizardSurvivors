# -*- coding: utf-8 -*-
"""The Broken Circle's standing things (.ai/campaign.md section 6). Run: python tools/art/props_heath.py

Writes assets/bonelight/world/heath/<id>.png (authored 1:1, saved at x2 like every world sprite) and
heath.json beside them, in trees.json's shape: each sprite's ground line and anchor column in pixels
of the PNG, and the width in world pixels of the only part that blocks. Node2DGame's Heath case in
BuildDecorProps reads the manifest and scatters them through the same grounded-decor path as the
trees, so they sort at their feet and stop the wizard's boots, not his chest (.ai/terrain.md 10).

THE STONES OF THE MOOT. "There were nine stones. There are eight, and one that walks." The heath is
littered with what is left of the circles the wizards met in: stones standing, leaning, broken off at
the stump, and fallen flat in the heather. They are granite - speckled, the one pale stone on the
heath - lit on the face the key reaches, with lichen crusting their weather side. None of them has a
split face or a mark: that is the Ninth Stone's, and a decor stone that carried either would be the
boss standing still.

GORSE. The heath's own bush: dark, spined, low, starred with flowers in gold's lower stops (pigment,
not light - gold flowers come several to a bush, never one bright dot alone).

Like world_props.py: seen a little from above, never the subject, so no actor rim, a heavy contact
shadow, and nothing above two stops brighter than the floor except the stone's lit edge.
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
FOOT = 4          # authored rows below the ground line: room for the contact shadow

ST = T.ST
SK = T.SK
LI = T.ramp("lichen")
VE = T.VE
VI = T.VI
GO = T.GO

# Granite, lit to dark: the bone row's shade and deep stops for the pale lit face, then stone's hi
# to its base. In stone's own row, the first pass, a standing stone on the heather was a dark post.
GRANITE = [SK[3], SK[4], ST[0], ST[1], ST[2]]


def hash2(x, y, s):
    return T.hash2(x, y, s)


def stone_body(c, outline, seed, top_rows=2):
    """Fill a standing stone from its outline - a list of (y, left, right) rows - lit on the left
    face, shaded on the right, with granite's speckle, a lit sliver of its top surface, weathering
    cracks and lichen. Returns nothing; draws into c."""
    rnd = random.Random(seed)
    rows = {}
    for (y, l, r) in outline:
        rows[y] = (l, r)
    ys = sorted(rows)
    y0, y1 = ys[0], ys[-1]
    for y in ys:
        l, r = rows[y]
        w = max(1, r - l)
        # Three faces in three-quarter view: the lit face on the left, the front, and the shaded
        # face on the right, with an arris (a lit stop over a dark one) where two faces meet.
        a1 = l + max(1, int(w * 0.34))
        a2 = l + max(2, int(w * 0.72))
        for x in range(l, r + 1):
            if x < a1:
                tone = GRANITE[1] if (x + y) % 4 else GRANITE[0]
            elif x < a2:
                tone = GRANITE[2] if (x * 3 + y) % 5 else GRANITE[1]
            else:
                tone = GRANITE[3] if (x + y * 2) % 4 else GRANITE[4]
            h = hash2(x, y, seed)
            if h < 0.08:
                tone = GRANITE[0] if x < a2 else GRANITE[2]        # feldspar
            elif h < 0.15:
                tone = ST[3] if x < a2 else ST[4]                   # mica
            c.set(x, y, tone)
        if a2 < r:
            c.set(a2, y, GRANITE[1] if y % 5 else GRANITE[2])
            c.set(a2 + 1, y, ST[3])
        c.set(l, y, GRANITE[0] if y < y1 - 2 else GRANITE[1])   # the lit edge
        c.set(r, y, ST[4])
    # The top surface, seen a little from above: a lit sliver along the first rows.
    for y in ys[:top_rows]:
        l, r = rows[y]
        for x in range(l + 1, r):
            c.set(x, y, GRANITE[1] if (x + y) % 3 else GRANITE[0])
    # Weathering: two or three short cracks that change angle, in the stone's deepest stop.
    for k in range(rnd.randrange(2, 4)):
        y = rnd.randrange(y0 + 4, y1 - 3)
        l, r = rows[y]
        x = rnd.randrange(l + 2, max(l + 3, r - 1))
        for j in range(rnd.randrange(3, 7)):
            x += rnd.choice((1, 1, 0))
            y += rnd.choice((0, 1, 1, -1))
            if y in rows and rows[y][0] < x < rows[y][1]:
                c.set(x, y, OCC if j % 3 == 0 else ST[4])
    # Lichen on the weather (right) side and a crust on the lit face.
    for k in range(rnd.randrange(3, 6)):
        y = rnd.randrange(y0 + 2, y1 - 2)
        l, r = rows[y]
        cx = rnd.choice((r - 2, r - 3, l + 2, (l + r) // 2))
        for (dx, dy, tone) in ((0, 0, LI[2]), (1, 0, LI[3]), (0, 1, LI[3]), (-1, 0, LI[2]),
                               (0, -1, LI[1]), (1, 1, GO[3])):
            if (y + dy) in rows and rows[y + dy][0] < cx + dx < rows[y + dy][1]:
                c.set(cx + dx, y + dy, tone)


def heather_at_foot(c, x0, x1, base, seed):
    """Heather grown up round a stone's foot: a few low violet-topped sprigs over the contact."""
    rnd = random.Random(seed)
    for x in range(x0, x1 + 1):
        if rnd.random() < 0.55:
            h = rnd.randrange(1, 4)
            for j in range(h):
                c.set(x, base - j, VE[4] if j < h - 1 else (VI[1] if x % 2 else VI[2]))


def outline_rows(y0, y1, widths, cx, lean=0.0, jag=None):
    """(y, left, right) rows from a hand-written width profile, top to bottom, leaning toward +x at
    the top by `lean` pixels. `jag` breaks the top row edge for a broken stone."""
    n = len(widths) - 1
    out = []
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0)) * n
        i = min(n - 1, int(t))
        hw = widths[i] + (widths[i + 1] - widths[i]) * (t - i)
        sh = lean * (1.0 - (y - y0) / float(max(1, y1 - y0)))
        out.append((y, int(round(cx - hw + sh)), int(round(cx + hw + sh))))
    if jag:
        out = [(y, l, r) for (y, l, r) in out if not (y < y0 + jag[0] and jag[1] <= (l + r) // 2)]
    return out


def standing(w, h, widths, lean, seed, jag=None):
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w / 2.0
    T.contact(c, cx + 1, base + 1, w * 0.42)
    T.shade_pool(c, cx + 3, base + 1, w * 0.55, 3.0, density=0.5)
    rows = outline_rows(2, base, widths, cx - 0.5, lean, jag)
    stone_body(c, rows, seed)
    if jag:
        # The break: a ragged top, its fresh face a little paler (the stone was broken, not worn).
        top = rows[0][0]
        for (y, l, r) in rows[:3]:
            for x in range(l + 1, r):
                if hash2(x, y, seed + 9) < 0.5:
                    c.set(x, y, GRANITE[0] if y == top else GRANITE[1])
    heather_at_foot(c, int(cx - w * 0.42), int(cx + w * 0.42), base, seed + 3)
    c.anchor_x = int(cx)
    return c


def fallen(w, h, seed):
    """A stone lying in the heather, half sunk: its long face lit along the top, its end toward the
    viewer showing the speckled section."""
    c = raster.Canvas(w, h + FOOT, None)
    base = h - 1
    cx = w / 2.0
    T.contact(c, cx + 1, base + 1, w * 0.46)
    rows = []
    for y in range(3, base + 1):
        t = (y - 3) / float(base - 3)
        hw = w * 0.42 * (0.78 + 0.22 * math.sin(t * math.pi * 0.9))
        rows.append((y, int(round(cx - hw)), int(round(cx + hw))))
    # Drawn as a stone on its side: lit from the TOP (its upper face), so swap the light to rows.
    rnd = random.Random(seed)
    for (y, l, r) in rows:
        t = (y - 3) / float(base - 3)
        for x in range(l, r + 1):
            if t < 0.35:
                tone = GRANITE[1] if (x + y) % 4 else GRANITE[0]
            elif t < 0.75:
                tone = GRANITE[2] if (x * 3 + y) % 5 else GRANITE[1]
            else:
                tone = GRANITE[3] if (x + y) % 3 else GRANITE[4]
            if x > r - 3:
                tone = GRANITE[3]
            hh = hash2(x, y, seed)
            if hh < 0.08:
                tone = GRANITE[0] if t < 0.75 else GRANITE[2]
            elif hh < 0.14:
                tone = ST[3]
            c.set(x, y, tone)
        c.set(l, y, GRANITE[1])
        c.set(r, y, ST[4])
    for x in range(rows[0][1] + 1, rows[0][2]):
        c.set(x, 3, GRANITE[0] if x % 3 else GRANITE[1])
    for k in range(3):
        x = rnd.randrange(int(cx - w * 0.3), int(cx + w * 0.3))
        y = rnd.randrange(5, base - 1)
        for (dx, dy, tone) in ((0, 0, LI[2]), (1, 0, LI[3]), (-1, 0, LI[1]), (0, 1, LI[3])):
            c.set(x + dx, y + dy, tone)
    heather_at_foot(c, 1, w - 2, base, seed + 5)
    c.anchor_x = int(cx)
    return c


def gorse(seed, w, h):
    """Gorse: a low dark mound of spines, starred with gold flowers in clumps."""
    rnd = random.Random(seed)
    c = raster.Canvas(w, h + 3, None)
    base = h - 1
    cx = w // 2
    c.disc(cx, base, w * 0.42, 2.0, OCC)
    for x in range(1, w - 1):
        t = (x - cx) / (w * 0.5)
        top = base - int(round((h - 2) * math.sqrt(max(0.0, 1.0 - t * t)))) + rnd.randrange(0, 2)
        for y in range(top, base + 1):
            hv = hash2(x, y, seed)
            if y == top:
                tone = VE[3] if t < 0 else VE[4]
            elif hv < 0.16:
                tone = OCC
            elif hv < 0.72:
                tone = VE[4]
            elif t < -0.1 and y < top + 3:
                tone = VE[2]
            else:
                tone = VE[3]
            c.set(x, y, tone)
        # spines standing off the top
        if rnd.random() < 0.5:
            c.set(x, top - 1, VE[3] if t < 0 else VE[4])
    # Flowers, in clumps of three to five, gold's lit and base stops.
    for k in range(rnd.randrange(4, 7)):
        fx = rnd.randrange(3, w - 3)
        fy = base - rnd.randrange(2, h - 2)
        for j in range(rnd.randrange(3, 6)):
            x, y = fx + rnd.randrange(-2, 3), fy + rnd.randrange(-1, 2)
            if c.get(x, y)[3]:
                c.set(x, y, GO[1] if (x + y) % 3 else GO[2])
    c.anchor_x = cx
    return c


# (id, kind, painter, footprint in WORLD px or 0 for none) - as trees.SPRITES.
SPRITES = [
    ("standing-stone-1", "tree", lambda: standing(22, 46, [3.6, 5.2, 6.0, 6.4, 6.8, 7.4], 1.0, 211), 26),
    ("standing-stone-2", "tree", lambda: standing(28, 38, [5.0, 7.0, 8.4, 9.0, 9.6], 3.0, 223), 34),
    ("standing-stone-3", "tree", lambda: standing(24, 26, [6.4, 7.6, 8.2, 8.6], 0.0, 227, jag=(3, 0)), 30),
    ("fallen-stone-1", "tree", lambda: fallen(40, 14, 229), 60),
    ("fallen-stone-2", "tree", lambda: fallen(30, 12, 233), 44),
    ("gorse-1", "bush", lambda: gorse(241, 30, 16), 0),
    ("gorse-2", "bush", lambda: gorse(251, 22, 13), 0),
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
        anchor = getattr(c, "anchor_x", c.w // 2) * SCALE + 1
        c.scaled(SCALE).save(os.path.join(OUT, sid + ".png"))
        rows.append('    {"id": "%s", "kind": "%s", "path": "res://assets/bonelight/world/heath/%s.png", '
                    '"width": %d, "height": %d, "ground": %d, "anchor_x": %d, "trunk": %d}'
                    % (sid, kind, sid, c.w * SCALE, c.h * SCALE, ground, anchor, footprint))
        print("  %-18s %-4s %3dx%-3d on screen%s" % (sid, kind, c.w * SCALE, c.h * SCALE,
                                                  ", blocks %d px" % footprint if footprint else ""))
    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/props_heath.py - the Broken Circle\'s standing stones '
                'and gorse, in trees.json\'s shape: ground is the contact line and anchor_x the column '
                'that stands on the placement point, in pixels of the PNG; trunk is the width in world '
                'pixels of the only part that blocks (0: walk through it).",\n'
                '  "sprites": [\n%s\n  ]\n}\n' % ",\n".join(rows))
    io.open(os.path.join(OUT, "heath.json"), "w", encoding="utf-8", newline="\n").write(manifest.decode("utf-8"))
    print("%d heath sprites%s" % (len(rows), "" if ok else " - WITH PALETTE ERRORS"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
