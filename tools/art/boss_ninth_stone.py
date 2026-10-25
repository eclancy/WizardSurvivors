# -*- coding: utf-8 -*-
"""The Ninth Stone, the Broken Circle's boss (.ai/campaign.md section 6). Run: python tools/art/boss_ninth_stone.py

Writes assets/bonelight/enemies/heath-ninth-stone-{moving,attack,hurt,death}.png on the 96 boss cell,
scenes/resources/HeathNinthStoneFrames.tres, and heath-ninth-stone-fallen.png - the stone each of its
closes leaves standing round the player (NinthStoneBoss.MakeStandingStone), authored 1:1 and saved
at x2 like a world sprite, because it is placed in the world at scale 1. Also a look at the pose on
the heath's floor in tools/art/_heath_boss.png (gitignored).

"There were nine stones. There are eight, and one that walks." A standing stone of the moot, torn
out of the heath with its root-ball still hanging under it, a split down its face and his mark in
the split. It is a TAKEN thing - one of the circle's own stones - so the mark is cold arcane, two
pixels, in the dark of the split.

THE SILHOUETTE IS ITS OWN (sprite_bosses.py's rule): a tall, broad, round-shouldered slab, notched at
the crown where the split opens, standing on a ragged ball of earth wider than its foot with roots
hanging out of it. The Archivist is a THIN slab with detached tablets beside it and the Still Warden
a wedge frozen into the floor; this is a heavy one that has been pulled up and walks on what it was
pulled up with. Nothing in the heath's cast is a tall slab (cast_heath.py), so it is the one.

Granite, as every stone on the heath is (props_heath.py): bone's lower stops for the lit face, stone
for the shaded one, feldspar flecks, lichen on its weather side, heather still clinging to its
shoulders. The fallen stones it drops are the same granite with raw earth at their feet, so they read
as its stones and never as the decor standing stones, which have heather grown round them.
"""
from __future__ import print_function

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import anim_sets
import bonelight as B
import pixel as P
import raster
import roster
import spriteframes
import sprite_bosses as SB
import trees as T

ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_ENEMIES = os.path.join(ROOT, "assets", "bonelight", "enemies")
STEM = "heath-ninth-stone"
SHEET = "HeathNinthStoneFrames"
CELL = 96
FEET = CELL - 2
CX = 47.5

PALETTE = dict(SB.PALETTE)
for _ch, _hex in zip("U5678", B.MATERIALS["lichen"]):
    PALETTE[_ch] = _hex
MOSS_HI, MOSS_LIT, MOSS, MOSS_SH, MOSS_DP = "U", "5", "6", "7", "8"

# How it moves (build.py's BOSS_FEEL, for this boss): a stone lurching on its root-ball sways a
# little and does not stagger at all - lean, reach, knock.
FEEL = (0.25, 4.0, 0)

G = roster.G


def clear(g, x, y):
    """Back to transparent. G.set ignores ".", so a cut has to go through roster.cut."""
    roster.cut(g, (x, y))

# Granite, lit to dark, in the roster palette's characters: bone shade and deep, stone hi to deep.
GR = ["n", "m", "P", "p", "N", "j", "J"]


def granite_mass(g, rows, seed):
    """Fill a stone from (y, left, right) rows: a lit left face, a front, a shaded right face, with an
    arris (a lit stop over a dark one) where the faces meet, feldspar and mica flecks through it."""
    for (y, l, r) in rows:
        w = max(1, r - l)
        a1 = l + max(2, int(w * 0.30))
        a2 = l + max(3, int(w * 0.70))
        for x in range(l, r + 1):
            if x < a1:
                ch = GR[1] if (x * 3 + y) % 7 else GR[0]
            elif x < a2:
                ch = GR[2] if (x + y * 2) % 5 else GR[1]
            else:
                ch = GR[4] if (x + y) % 4 else GR[5]
            h = T.hash2(x, y, seed)
            if h < 0.06:
                ch = GR[0] if x < a2 else GR[2]
            elif h < 0.11:
                ch = GR[5] if x < a2 else GR[6]
            g.set(x, y, ch)
        if a2 < r - 1:
            g.set(a2, y, GR[2] if y % 6 else GR[1])
            g.set(a2 + 1, y, GR[5])
        g.set(r, y, GR[6])


def outline(y0, y1, profile, cx, lean=0.0):
    n = len(profile) - 1
    rows = []
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0)) * n
        i = min(n - 1, int(t))
        hw = profile[i] + (profile[i + 1] - profile[i]) * (t - i)
        sh = lean * (1.0 - (y - y0) / float(max(1, y1 - y0)))
        rows.append((y, int(round(cx - hw + sh)), int(round(cx + hw + sh))))
    return rows


def ninth_stone():
    """The pose: the stone, its split crown, the mark, the root-ball it walks on."""
    g = G(CELL)

    # THE ROOT-BALL: peat and earth torn up with it, ragged and wider than its foot, roots hanging
    # to the ground. Peat-dark (stone's deep end and gold's), not the linen of the first pass, which
    # read as a green cushion.
    SB.swell(g, 70, 89, [19.0, 24.0, 27.0, 25.0, 18.0], "j", "N", "J", cx=CX + 1, wobble=1)
    for (x, y, r, ch) in ((29, 81, 3.5, "A"), (61, 83, 3.5, "E"), (44, 87, 4.0, "J"), (37, 76, 2.5, "A"),
                          (57, 77, 3.0, "A"), (67, 75, 2.5, "J"), (25, 77, 2.5, "J"), (50, 81, 2.0, "u")):
        g.disc(x, y, r, ch)
    for (x, y) in ((28, 76), (35, 84), (48, 79), (58, 86), (64, 80), (40, 73), (53, 73), (70, 81)):
        g.set(x, y, "p")
        g.set(x + 1, y + 1, "o")
    # Clods breaking off the ball's edge, and ragged lumps out of its outline.
    for (x, y, r) in ((20, 79, 2.0), (75, 77, 2.2), (23, 86, 1.6), (73, 86, 1.8)):
        g.disc(x, y, r, "j")
        g.set(x - 1, y - 1, "N")
    # Roots, pale and thin, hanging out of the ball to the ground.
    for (x0, y0, x1, y1) in ((26, 82, 20, 93), (33, 87, 30, 93), (52, 88, 56, 93), (63, 85, 71, 93),
                             (41, 88, 38, 93), (70, 80, 77, 90), (24, 75, 17, 84)):
        g.line(x0, y0, x1, y1, "m")
        g.line(x0 + 1, y0, x1 + 1, y1 - 1, "n")
        g.set(x1, y1, "n")
    # Turf still on the ball, with heather in it.
    for (x, y) in ((29, 71), (62, 71), (46, 70), (38, 72)):
        g.rect(x - 2, y, x + 2, y + 1, "A")
        g.set(x - 1, y - 1, "v")
        g.set(x + 1, y - 1, "w")
        g.set(x, y - 2, "V")
        g.set(x + 2, y - 1, "x")

    # THE STONE: tall, broad, round-shouldered, leaning a little into its walk; its right shoulder
    # broken off in a flat fracture.
    rows = outline(8, 74, [12.0, 17.5, 20.0, 21.0, 21.5, 21.0, 20.0, 19.0], CX, lean=2.0)
    granite_mass(g, rows, 311)
    for (y, l, r) in rows[:5]:                     # the round left shoulder
        for k in range(5 - (y - 8)):
            clear(g, l + k, y)
    for (y, l, r) in rows[:16]:                    # the broken right shoulder: a fracture plane
        cut_at = r - max(0, 9 - (y - 8) * 0.6)
        for xx in range(int(cut_at), r + 1):
            clear(g, xx, y)
        if cut_at <= r:
            g.set(int(cut_at) - 1, y, GR[0])
            g.set(int(cut_at) - 2, y, GR[1])
    # BIG PLANES: two long seams down the faces, so it is a few great broken faces of rock and not an
    # even stipple - the read of a stone at this size is its planes.
    for pts in ([(33, 18), (31, 40), (34, 66)], [(62, 24), (64, 46), (61, 70)]):
        roster.fold(g, pts, "J", GR[1])

    # THE SPLIT: down its face from a cleft in the crown, opening into the dark where the mark is.
    split = []
    x = CX + 3.0
    for y in range(8, 60):
        x += math.sin(y * 0.55) * 0.6 + (0.25 if y % 7 == 0 else -0.1)
        w = 0.6 if y < 14 else (1.6 if y < 26 else (2.4 if y < 46 else max(0.4, 2.4 - (y - 46) * 0.18)))
        split.append((y, x, w))
    for (y, sx, w) in split:
        for xx in range(int(round(sx - w)), int(round(sx + w)) + 1):
            g.set(xx, y, "o")
        g.set(sx - w - 1, y, GR[0])          # its lit lip
        g.set(sx + w + 1, y, GR[6])          # its shaded lip
    # The cleft: a V cut into the crown where the split opens it, daylight through it, so the
    # silhouette has two horns of rock at the top rather than one rounded end.
    top_x = split[0][1]
    for y in range(6, 22):
        half = max(0.0, (22 - y) * 0.42)
        for xx in range(int(round(top_x - half)), int(round(top_x + half)) + 1):
            clear(g, xx, y)
        if half > 0.5 and y >= 8:
            g.set(top_x - half - 1, y, GR[0])
            g.set(top_x + half + 1, y, GR[5])
    # Weathering cracks across the faces, and lichen on the weather side.
    for (x0, y0, x1, y1) in ((28, 30, 35, 33), (55, 34, 60, 31), (34, 52, 42, 54), (55, 60, 62, 63),
                             (37, 64, 41, 68)):
        g.line(x0, y0, x1, y1, "J")
        g.line(x0, y0 - 1, x1, y1 - 1, GR[1])
    for (x, y, r) in ((60, 30, 3), (64, 44, 3), (60, 56, 2), (29, 46, 2), (66, 66, 2), (40, 22, 2)):
        g.disc(x, y, r, MOSS)
        g.set(x - 1, y - 1, MOSS_LIT)
        g.set(x - r, y, MOSS_LIT)
        g.set(x + r, y, MOSS_SH)
        g.set(x, y + r, MOSS_DP)
    # Heather still on its shoulders.
    for (x, y) in ((32, 13), (35, 11), (38, 12), (57, 17), (60, 19)):
        g.set(x, y, "v")
        g.set(x + 1, y, "w")
        g.set(x, y + 1, "A")
        g.set(x + 1, y + 1, "x")

    roster.contact(g, 20, 78, y=FEET)
    # The rim, on the key side of every run.
    for y in range(2, FEET + 1):
        for x in range(1, CELL):
            if g.get(x, y) not in (".", "W") and g.get(x - 1, y) == ".":
                run = 0
                while x + run < CELL and g.get(x + run, y) not in (".", "W"):
                    run += 1
                if run >= 3 and g.get(x, y) != "o":
                    g.set(x - 1, y, "W")
                break
    # THE MARK, in the split: two pixels, cold arcane, where the split is widest.
    my = 38
    mx = int(round([sx for (y, sx, w) in split if y == my][0]))
    SB.mark(g, mx - 1, my, "Y", "y")
    return g.rows()


FALLEN_FOOT = 4        # authored rows below the contact line (8 at x2; NinthStoneBoss reads 8)


def fallen_stone():
    """The stone a close leaves standing: squat granite thrown up out of the heath, raw earth and
    clods round its foot. 50 px wide at x2 so the footprint (radius 50) sits under it."""
    c = raster.Canvas(52, 44, None)
    base = 44 - FALLEN_FOOT - 1
    cx = 26.0
    T.contact(c, cx + 1, base + 1, 22)
    rows = []
    for y in range(6, base + 1):
        t = (y - 6) / float(base - 6)
        hw = 12.0 + 7.0 * math.sin(min(1.0, t * 1.4) * math.pi * 0.5) + (2.0 if t > 0.85 else 0.0)
        sh = 1.5 * (1.0 - t)
        rows.append((y, int(round(cx - hw + sh)), int(round(cx + hw + sh))))
    sk = T.SK
    st = T.ST
    tones = [sk[3], sk[4], st[0], st[1], st[2], st[3], st[4]]
    for (y, l, r) in rows:
        w = max(1, r - l)
        a1 = l + int(w * 0.3)
        a2 = l + int(w * 0.7)
        for x in range(l, r + 1):
            if x < a1:
                col = tones[1] if (x * 3 + y) % 7 else tones[0]
            elif x < a2:
                col = tones[2] if (x + 2 * y) % 5 else tones[1]
            else:
                col = tones[4] if (x + y) % 4 else tones[5]
            if T.hash2(x, y, 401) < 0.07:
                col = tones[0] if x < a2 else tones[2]
            c.set(x, y, col)
        c.set(r, y, tones[6])
    for (y, l, r) in rows[:2]:
        for k in range(3 - (y - 6)):
            c.set(l + k, y, (0, 0, 0, 0))
            c.set(r - k, y, (0, 0, 0, 0))
    # Raw earth thrown up round its foot: peat clods, linen-dark, a little gold-brown.
    ln = T.LN
    gd = T.GO
    for k in range(14):
        x = int(cx - 22 + (k * 37 % 44))
        y = base - (k * 13 % 4)
        c.set(x, y, ln[4] if k % 3 else gd[4])
        c.set(x + 1, y, ln[3])
        c.set(x, y - 1, ln[3] if k % 2 else gd[4])
    c.anchor_x = int(cx)
    return c


def main():
    pose = ninth_stone()
    filled, _edges, ratio = roster.density(pose)
    problems = P.check_anchor("%s pose" % STEM, [pose], CELL)
    lean, reach, knock = FEEL
    spec = []
    for anim, frames, loop, speed in anim_sets.full_set(pose, lean=lean, reach=reach, knock=knock):
        png = "%s-%s.png" % (STEM, anim)
        P.write_strip(frames, PALETTE, os.path.join(OUT_ENEMIES, png), CELL)
        spec.append((anim, png, len(frames), loop, speed))
    spriteframes.write(os.path.join(ROOT, "scenes", "resources", SHEET + ".tres"), spec,
                       "assets/bonelight/enemies", cell=CELL)
    print("  %-22s %dx%d  %4d px  %.2f edges/px%s" % (STEM, CELL, CELL, filled, ratio,
                                                     "" if ratio >= 0.80 else "   SPARSE"))
    c = fallen_stone()
    bad = c.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in the fallen stone: %s" % bad[:4])
        return 1
    c.scaled(2).save(os.path.join(OUT_ENEMIES, "%s-fallen.png" % STEM))
    print("  %s-fallen  %dx%d on screen, contact line %d rows from the bottom"
          % (STEM, c.w * 2, c.h * 2, FALLEN_FOOT * 2))
    # A look at it on the heath's floor.
    preview = os.path.join(HERE, "_heath_boss.png")
    from PIL import Image
    im = Image.new("RGBA", (CELL * 2 + 120, CELL), (34, 28, 51, 255))
    im.alpha_composite(P.render(P.pad(pose, CELL, CELL), PALETTE), (0, 0))
    sil = P.render(P.pad(pose, CELL, CELL), PALETTE)
    black = Image.new("RGBA", sil.size, (0, 0, 0, 255))
    pale = Image.new("RGBA", sil.size, (210, 210, 200, 255))
    pale.paste(black, (0, 0), sil.split()[3])
    im.alpha_composite(pale, (CELL, 0))
    im.alpha_composite(c.img, (CELL * 2 + 10, CELL - c.h))
    im.resize((im.size[0] * 4, im.size[1] * 4), Image.NEAREST).save(preview)
    print("preview: %s" % os.path.relpath(preview, ROOT).replace(os.sep, "/"))
    if problems:
        print("ANCHOR PROBLEMS: %s" % problems)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
