# -*- coding: utf-8 -*-
"""The Sunken Cave's secrets (.ai/achievements-and-secrets.md 8.4). Run: python tools/art/secret_cave.py

    world/cave/geode-heart.png             the Geode Heart, five frames left to right: whole,
                                           hairline, cracked, splitting, shattered (Breakable's strip)
    world/cave/geode-shards.png            six shards for the shatter's ring (GeodeHeart.cs throws them)
    world/cave/crystal-blue-cracked.png    the cave's own small clusters (world_props.gem_cluster),
    world/cave/crystal-violet-cracked.png  with hairline cracks lit from inside: what the nearest of the
                                           far tell's crystals become (SecretFarTell.NearTextures)
    tools/art/_secret_cave_preview.png     (gitignored) on a strip of cave floor, for looking at

"A crystal three men tall, grown up through the cavern floor. The small ones near it are cracking."
Eric's giant crystal, and the first spell vault: Frost Shard is learned by shattering the biggest thing
in the cave.

THE SAME STUFF AS THE SMALL ONES, HUGE. The far tell is the cave's own crystal-blue and crystal-violet
growing commoner toward it, so the heart is drawn from the same two element ramps - water for the great
spire, arcane for the companions round its foot - and its cracks are the cracked clusters' cracks. The
heart is what the clusters were leading to.

CELL. 96 x 112, saved at x2: the spire is a hundred art pixels from foot to tip, two hundred on screen,
"three men tall" beside the wizard's eighty (.ai/art-direction.md section 1: a boss is a bigger cell,
never a bigger scale). The bottom rows are the ground: the contact shadow's middle is FOOT_Y, and
GeodeHeart.cs anchors the strip there (Breakable.SheetOffset), its collision a body round the foot.

LIGHT. Key from the upper left, so each prism's left face is lit, its front face base and its right face
in shade, dithered along the key axis (raster.poly_shade) so the faces turn rather than stack. Crystals
are the one emissive thing a floor may hold (world_props), and the element ramps are never stepped by the
matte finish - but the great spire is mostly its mid and edge stops, so the white-hot core stays a point
at the tip and along the lit ridge, and a thing this big reads as a crystal rather than as a spell.

THE HEART. Deep in the spire's front face, a lens of near-black - arcane material's deep - with gold's
shade and deep at its middle: the light he missed, locked in stone. The breathing glint itself is drawn
in code on top (SecretSite.DrawGlint), because a near tell must breathe and a sprite cannot.

THE STAGES. Cracks are bright, not dark: a fracture inside a crystal catches the light. Hairline is three
short lit lines; cracked runs them across the faces, chips a notch out of an edge and drops two chips at
the foot; splitting opens a dark seam down the spire with the halves a pixel apart and the gold of the
heart showing in it; shattered is the stump - the broken prism a fifth of its height, hollow at the top
where the light was, and shards lying round it.
"""
from __future__ import division, print_function

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import raster
import world_props

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "cave")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_cave_preview.png")
SCALE = 2

W, H = 96, 112
CX = 48
FOOT_Y = 104          # the contact shadow's middle: where it stands
SHARD = 12            # one shard's cell

WATER = bl.ELEMENTS["water"]     # core, hot, mid, edge
ICE = bl.ELEMENTS["ice"]
ARC_E = bl.ELEMENTS["arcane"]


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


ST = ramp("stone")
AR = ramp("arcane")          # the arcane cyan MATERIAL: the crystal's body where it is not lit
GO = ramp("gold")


# --- a prism --------------------------------------------------------------------------------------

class Prism(object):
    """A pointed crystal column in three-quarter view, standing at (fx, fy).

    Four edges run up from the foot - the left edge, the lit ridge, the far ridge and the right edge -
    to a shoulder, and the three faces between them close to the tip. Leaning is a shear: every point
    at height h is moved lean * h / height to the side.
    """

    def __init__(self, fx, fy, height, half, lean=0.0, cap=0.3, ridge=(-0.3, 0.38)):
        self.fx, self.fy, self.height, self.half, self.lean = fx, fy, float(height), half, lean
        self.sy = fy - height * (1.0 - cap)            # the shoulder
        self.ty = fy - height                          # the tip
        self.ridge = ridge

    def shear(self, x, y):
        h = (self.fy - y) / self.height
        return (x + self.lean * h, y)

    def bottom(self, u):
        """A point along the foot, u from -1 (left edge) to 1 (right edge); the ridges stand nearer."""
        dip = 1.5 if self.ridge[0] <= u <= self.ridge[1] else 0.0
        return self.shear(self.fx + u * self.half, self.fy + dip)

    def shoulder(self, u):
        dip = 1.0 if self.ridge[0] <= u <= self.ridge[1] else 0.0
        return self.shear(self.fx + u * self.half * self.taper, self.sy + dip)

    taper = 0.86

    def tip(self):
        return self.shear(self.fx + self.half * 0.08, self.ty)

    def faces(self):
        r0, r1 = self.ridge
        left = [self.bottom(-1), self.bottom(r0), self.shoulder(r0), self.tip(), self.shoulder(-1)]
        front = [self.bottom(r0), self.bottom(r1), self.shoulder(r1), self.tip(), self.shoulder(r0)]
        right = [self.bottom(r1), self.bottom(1), self.shoulder(1), self.tip(), self.shoulder(r1)]
        return left, front, right

    def outline(self):
        return [self.bottom(-1), self.bottom(self.ridge[0]), self.bottom(self.ridge[1]), self.bottom(1),
                self.shoulder(1), self.tip(), self.shoulder(-1)]


def draw_prism(c, p, el, body=True):
    """el is an element ramp (core, hot, mid, edge). The lit face is mid into hot near its ridge; the
    front face edge into mid; the shaded face the arcane material's shade into deep."""
    left, front, right = p.faces()
    c.poly_shade(left, [el[1], el[2], el[2], el[3]], ang=-0.35, gamma=0.9)
    c.poly_shade(front, [el[2], el[3], el[3], AR[3]], ang=-0.35, gamma=1.0)
    c.poly_shade(right, [el[3], AR[3], AR[4]], ang=-0.35, gamma=1.0)
    # The edges: the lit ridge is the crown of the lit plane, the far ridge a stop down, the right edge
    # against the floor in occlusion (no outline - the contact line).
    lx0, ly0 = p.bottom(p.ridge[0])
    lx1, ly1 = p.shoulder(p.ridge[0])
    tx, ty = p.tip()
    c.line(lx0, ly0 - 1, lx1, ly1, el[1])
    c.line(lx1, ly1, tx, ty, el[0])
    rx0, ry0 = p.bottom(p.ridge[1])
    rx1, ry1 = p.shoulder(p.ridge[1])
    c.line(rx0, ry0 - 1, rx1, ry1, el[2])
    ex0, ey0 = p.bottom(1)
    ex1, ey1 = p.shoulder(1)
    c.line(ex0, ey0, ex1, ey1, OCC)
    c.line(ex1, ey1, tx, ty, AR[4])
    c.set(int(round(tx)), int(round(ty)), el[0])


# --- the heart ------------------------------------------------------------------------------------

SPIRE = Prism(CX - 1, FOOT_Y - 1, 98, 18, lean=6.0, cap=0.38)
LEFT = Prism(CX - 22, FOOT_Y + 1, 44, 8, lean=-12.0, cap=0.32, ridge=(-0.25, 0.4))
RIGHT = Prism(CX + 21, FOOT_Y, 34, 7, lean=10.0, cap=0.34, ridge=(-0.2, 0.45))
FRONT = Prism(CX + 6, FOOT_Y + 4, 16, 5, lean=4.0, cap=0.4)
BACK = Prism(CX - 10, FOOT_Y - 6, 26, 6, lean=-4.0, cap=0.35)

# The heart's middle in the strip's own pixels: GeodeHeart.cs puts the glint here (HEART_X, HEART_Y).
HEART_X, HEART_Y = 49, 52


def collar(c):
    """The cavern floor it broke up through: slabs of stone heaved round the foot, lit on the left."""
    c.disc(CX, FOOT_Y + 1, 36, 5.2, OCC)
    for k, (dx, dy, rx, ry) in enumerate(((-27, 1, 8, 3.2), (-15, 4, 9, 3.0), (2, 5, 10, 2.8),
                                          (18, 3, 9, 3.0), (29, 0, 7, 2.8), (-31, -2, 5, 2.4))):
        x, y = CX + dx, FOOT_Y + dy
        c.disc(x, y - 1, rx, ry, ST[2])
        c.disc(x - rx * 0.25, y - 1.8, rx * 0.6, ry * 0.55, ST[1])
        c.hline(x - rx + 1, x + rx - 1, y + ry - 1, OCC)
        c.set(x - rx * 0.4, y - ry * 0.7, ST[0])


def heart_core(c, glow):
    """The cloud deep in the spire, and the warm in its middle (gold's shade and deep). An inclusion, not
    an eye: no rim round it, a dithered thickening from the crystal's own shade to occlusion, ragged at
    its edge, so it reads as something inside the stone rather than a hole in its face."""
    rx, ry = 7.0, 15.0
    for y in range(int(HEART_Y - ry), int(HEART_Y + ry) + 1):
        for x in range(int(HEART_X - rx) - 1, int(HEART_X + rx) + 2):
            # Ragged: the edge wanders with height, so the cloud has no clean outline.
            wobble = 1.0 + 0.18 * math.sin(y * 0.9 + x * 0.3)
            d = math.hypot((x - HEART_X) / (rx * wobble), (y - HEART_Y) / ry)
            if d > 1.0:
                continue
            th = raster.DITHER[y % 8][x % 8] / 64.0
            if d > 0.72:
                tone = AR[3] if (1.0 - d) / 0.28 < th else AR[4]
            elif d > 0.38:
                tone = AR[4] if (0.72 - d) / 0.34 < th else OCC
            else:
                tone = OCC
            c.set(x, y, tone)
    for (x, y) in ((HEART_X, HEART_Y - 1), (HEART_X - 1, HEART_Y), (HEART_X, HEART_Y), (HEART_X + 1, HEART_Y + 1),
                   (HEART_X, HEART_Y + 1)):
        c.set(x, y, GO[4])
    c.set(HEART_X, HEART_Y, GO[3])
    if glow:
        for (x, y) in ((HEART_X - 1, HEART_Y - 1), (HEART_X + 1, HEART_Y), (HEART_X, HEART_Y + 2),
                       (HEART_X, HEART_Y - 2)):
            c.set(x, y, GO[3])
        c.set(HEART_X, HEART_Y, GO[2])


def veins(c):
    """Internal planes catching the light: a few long lines of the ice ramp's edge through the spire."""
    for (x0, y0, x1, y1) in ((38, 92, 44, 30), (55, 96, 58, 40), (42, 70, 50, 24)):
        c.line(x0, y0, x1, y1, ICE[3])


def whole(c):
    collar(c)
    for p, el in ((BACK, ARC_E), (LEFT, ARC_E), (SPIRE, WATER), (RIGHT, WATER)):
        draw_prism(c, p, el)
    veins(c)
    heart_core(c, glow=False)
    draw_prism(c, FRONT, ARC_E)


# Cracks, bright: (x0, y0, x1, y1) in strip pixels, each stage adding to the last.
HAIRLINE = [(36, 60, 41, 50), (58, 40, 62, 33), (52, 82, 55, 76)]
CRACKED = HAIRLINE + [(41, 50, 45, 42), (45, 42, 44, 31), (62, 33, 61, 22), (55, 76, 60, 64), (60, 64, 58, 56),
                      (34, 82, 38, 72), (27, 92, 23, 84), (69, 88, 72, 81)]


def crack(c, x0, y0, x1, y1):
    """A fracture: the lit line, and its shadow a pixel down and to the right."""
    c.line(x0 + 1, y0 + 1, x1 + 1, y1 + 1, AR[4])
    c.line(x0, y0, x1, y1, ICE[1])


def chips(c, spots):
    for (x, y, el) in spots:
        world_props.crystal(c, x, y, 4, 1.8, el, lean=1)


def hairline(c):
    whole(c)
    for seg in HAIRLINE:
        crack(c, *seg)


def cracked(c):
    whole(c)
    for seg in CRACKED:
        crack(c, *seg)
    # A notch out of the spire's lit edge, where a piece has come away.
    for (x, y) in ((33, 70), (33, 71), (34, 71), (32, 72), (33, 72), (34, 72), (33, 73)):
        c.set(x, y, OCC)
    c.set(35, 70, ICE[1])
    chips(c, ((26, FOOT_Y + 5, "water"), (66, FOOT_Y + 6, "arcane")))


def clear(c, x, y):
    if 0 <= x < c.w and 0 <= y < c.h:
        c.px[x, y] = (0, 0, 0, 0)


def splitting(c):
    cracked(c)
    heart_core(c, glow=True)
    # The seam: down the spire's front from below the tip to the foot - two pixels of dark with the lit
    # lip of the left half beside it, and the right half pushed two pixels over - opening wider at the
    # heart, where the warm shows through it.
    seam = [(53, 18), (51, 30), (52, 40), (49, 46), (50, 58), (47, 68), (49, 80), (46, 92), (47, 101)]
    for i in range(len(seam) - 1):
        (x0, y0), (x1, y1) = seam[i], seam[i + 1]
        steps = max(abs(x1 - x0), abs(y1 - y0))
        for k in range(steps + 1):
            x = int(round(x0 + (x1 - x0) * k / float(steps)))
            y = int(round(y0 + (y1 - y0) * k / float(steps)))
            wide = 3 if abs(y - HEART_Y) <= 8 else 2
            moved = [c.get(x + j, y) for j in range(1, 8)]
            for j in range(wide):
                c.set(x + j, y, OCC)
            for j, p in enumerate(moved):
                if p[3]:
                    c.set(x + wide + j, y, p)
            if c.get(x - 1, y)[3]:
                c.set(x - 1, y, ICE[1])
            # The warm of the heart, showing in the seam where it passes it.
            if abs(y - HEART_Y) <= 6:
                for j in range(wide):
                    c.set(x + j, y, GO[3] if abs(y - HEART_Y) > 2 or j != 1 else GO[2])
    # More of it on the ground.
    chips(c, ((20, FOOT_Y + 3, "water"), (74, FOOT_Y + 4, "water"), (40, FOOT_Y + 7, "arcane")))


# The stump's broken top: (x, height above the foot) at its teeth, joined straight - a few spikes of
# different heights where the spire snapped, the tallest on the lit side.
BREAK = [(28, 14), (32, 30), (36, 22), (40, 44), (44, 27), (48, 34), (52, 20), (56, 31), (61, 17), (65, 24), (70, 12)]


def break_height(x):
    for (x0, h0), (x1, h1) in zip(BREAK, BREAK[1:]):
        if x0 <= x <= x1:
            return h0 + (h1 - h0) * (x - x0) / float(x1 - x0)
    return 0.0


def shattered(c):
    collar(c)
    # The stump: the spire drawn whole, then everything above the break taken away. Its broken edge is
    # lit along the top, and a dark hollow sits a little below it, where the light was.
    stump = raster.Canvas(W, H, None)
    draw_prism(stump, SPIRE, WATER)
    veins(stump)
    for x in range(W):
        top = FOOT_Y - break_height(x)
        for y in range(H):
            p = stump.get(x, y)
            if not p[3]:
                continue
            if y < top:
                continue
            if y < top + 1.5:
                p = raster.rgb(ICE[1] if x < CX + 4 else WATER[2])
            c.set(x, y, p)
    c.disc(CX + 2, FOOT_Y - 17, 9, 2.6, AR[4])
    c.disc(CX + 2, FOOT_Y - 17, 6, 1.6, OCC)
    c.set(CX + 2, FOOT_Y - 17, GO[4])
    # The two companions still stand; the rest lies round the foot.
    for p, el in ((LEFT, ARC_E), (RIGHT, WATER)):
        draw_prism(c, p, el)
    for seg in ((26, 96, 22, 88), (68, 92, 71, 84)):
        crack(c, *seg)
    chips(c, ((14, FOOT_Y + 2, "water"), (22, FOOT_Y + 6, "water"), (35, FOOT_Y + 7, "arcane"),
              (58, FOOT_Y + 7, "water"), (72, FOOT_Y + 5, "arcane"), (80, FOOT_Y + 2, "water")))
    for (x, y, l) in ((30, FOOT_Y + 4, 6), (63, FOOT_Y + 5, 5), (48, FOOT_Y + 8, 7)):
        c.line(x, y, x + l, y - 1, WATER[3])
        c.line(x, y - 1, x + l - 1, y - 2, WATER[2])


STAGES = [whole, hairline, cracked, splitting, shattered]


# --- shards ---------------------------------------------------------------------------------------

def shard(k):
    """One shard for the shatter's ring: a sliver of the spire, lit edge and shaded face."""
    c = raster.Canvas(SHARD, SHARD, None)
    shapes = [
        [(2, 9), (6, 1), (9, 4), (7, 10)],
        [(1, 6), (10, 3), (11, 6), (3, 9)],
        [(3, 10), (5, 2), (8, 8)],
        [(2, 4), (9, 2), (10, 9), (4, 10)],
        [(4, 11), (6, 1), (8, 11)],
        [(1, 8), (6, 3), (11, 5), (7, 9)],
    ]
    pts = shapes[k % len(shapes)]
    el = WATER if k % 3 else ARC_E
    c.poly_shade(pts + [pts[0]], [el[1], el[2], el[3]], ang=-0.7)
    c.line(pts[0][0], pts[0][1], pts[1][0], pts[1][1], el[0])
    return c


# --- the cracked clusters -------------------------------------------------------------------------

def cracked_cluster(element):
    """world_props' gem cluster, the same pixels, with two hairline cracks lit from inside."""
    c = world_props.gem_cluster(element)()
    e = bl.ELEMENTS[element]
    for (x0, y0, x1, y1) in ((14, 22, 17, 15), (16, 18, 18, 13), (22, 24, 23, 19)):
        c.line(x0 + 1, y0 + 1, x1 + 1, y1 + 1, OCC)
        c.line(x0, y0, x1, y1, e[0])
    return c


# --- out ------------------------------------------------------------------------------------------

def audit(name, c):
    bad = c.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:5]))
        return False
    return True


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True

    strip = Image.new("RGBA", (W * SCALE * len(STAGES), H * SCALE), (0, 0, 0, 0))
    for i, stage in enumerate(STAGES):
        c = raster.Canvas(W, H, None)
        stage(c)
        ok = audit("geode-heart frame %d (%s)" % (i, stage.__name__), c) and ok
        strip.alpha_composite(c.scaled(SCALE), (i * W * SCALE, 0))
    strip.save(os.path.join(OUT, "geode-heart.png"))

    shards = Image.new("RGBA", (SHARD * SCALE * 6, SHARD * SCALE), (0, 0, 0, 0))
    for k in range(6):
        c = shard(k)
        ok = audit("geode shard %d" % k, c) and ok
        shards.alpha_composite(c.scaled(SCALE), (k * SHARD * SCALE, 0))
    shards.save(os.path.join(OUT, "geode-shards.png"))

    clusters = []
    for name, element in (("crystal-blue-cracked", "water"), ("crystal-violet-cracked", "arcane")):
        c = cracked_cluster(element)
        ok = audit(name, c) and ok
        img = c.scaled(SCALE)
        img.save(os.path.join(OUT, name + ".png"))
        clusters.append(img)

    print("  geode heart  %d frames at %dx%d, 6 shards, 2 cracked clusters" % (len(STAGES), W * SCALE, H * SCALE))

    # The preview: the strip on the cave's floor tone, then the shards and the clusters beside their
    # uncracked selves, three times up.
    pad = 10
    pw = strip.size[0] + pad * 2
    ph = strip.size[1] + 64 * 2 + pad * 4
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(ST[3]))
    sheet.alpha_composite(strip, (pad, pad))
    y = strip.size[1] + pad * 2
    sheet.alpha_composite(shards, (pad, y))
    x = pad + shards.size[0] + pad
    for name in ("crystal-blue", "crystal-violet"):
        plain = Image.open(os.path.join(ROOT, "assets", "bonelight", "world", name + ".png")).convert("RGBA")
        sheet.alpha_composite(plain, (x, y))
        x += plain.size[0] + 4
    for img in clusters:
        sheet.alpha_composite(img, (x, y))
        x += img.size[0] + 4
    sheet.resize((sheet.size[0] * 2, sheet.size[1] * 2), Image.NEAREST).save(PREVIEW)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
