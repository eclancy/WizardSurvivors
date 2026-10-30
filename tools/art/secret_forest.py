# -*- coding: utf-8 -*-
"""The Enchanted Forest's secrets (.ai/achievements-and-secrets.md 8.1). Run: python tools/art/secret_forest.py

    world/forest/first-tree-clearing.png   F4, the First Tree's clearing, two frames left to right: barren
                                           (the bed cracked dry) and planted (the bed turned over). The dry
                                           ground, the ring of nine stumps and the seed-bed, as one picture
    world/forest/first-tree-sapling.png    the sapling, six frames: what grows in the bed over six seconds
    world/forest/pale-hart.png             F5, the Pale Hart (the spell vault): ten 48-cell frames - standing,
                                           breathing, gathering, stretched and landing, facing right, then the
                                           same five facing left
    world/forest/high-seat.png             the hunter's high seat it leads to, two frames: with its bow of
                                           light hanging from the front post, and with only the peg
    world/forest/hoofprints-1.png, -2.png  its far tell: cloven prints pressed into the moss, and bracken bent
    world/forest/bracken-bent-1.png, -2.png  over, both drawn pointing right (SecretFarTell.LeanToward turns them)
    tools/art/_secret_forest_preview.png   (gitignored) on the forest's turf, for looking at

THE FIRST TREE WAS DRAWN IN CODE, AND OFF THE CONTRACT. Until the secrets' pixel-art pass (2026-10-30,
the spells' contract of .ai/art-direction.md sections 1-3 and 6 brought to the secrets) FirstTree.cs drew
its clearing with DrawCircle and DrawColoredPolygon: a translucent disc of dry ground, antialiased
trapezoid stumps with rings drawn by DrawArc, a lerp-free but off-palette brown, a sapling crowned with
smooth circles. Everything here is the same picture, at the same size and in the same places - the ring
at FirstTree.RingRadius flattened to 0.62, the stump sizes from the same two little sequences, the bed at
BedRadius flattened by half, the sapling's height 10 + 35t art pixels - redrawn as pixel art in the
palette's rows: dead wood in gold's lower stops with flesh heartwood on the cut, dry ground in linen
thinned by the Bayer matrix (never a translucent disc), living leaves in verdant (the ground's own living
green, .ai/terrain.md section 4).

Saved at x2, like every world sprite. FirstTree.cs draws the clearing with its middle - the bed's middle -
on the node, and the sapling with its foot there, both on the world's 2-unit grid (SpellInk.AlignToWorldGrid).
The breathing glint is drawn in code on top (SecretSite.DrawGlint): a near tell must breathe.
"""
from __future__ import division, print_function

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "forest")
PREVIEW = os.path.join(ROOT, "tools", "art", "_secret_forest_preview.png")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


GO = ramp("gold")
FL = ramp("flesh")
LN = ramp("linen")
VE = ramp("verdant")
LI = ramp("lichen")
ST = ramp("stone")


def th(x, y):
    """The Bayer threshold of a pixel, 0 to 1: the one gradient the contract allows."""
    return raster.DITHER[y % 8][x % 8] / 64.0


def hashed(k, salt):
    """A fixed pseudo-random 0..1 from two integers: the same on Python 2 and 3."""
    v = (k * 2654435761 + salt * 40503 + 977) % 4294967296
    v = (v ^ (v >> 13)) * 1274126177 % 4294967296
    return (v % 100003) / 100003.0


def blob(c, cx, cy, rx, ry, tones, salt=0):
    """A rounded mass lit from the upper left: tones are (lit, base, shade, deep). Dithered between stops
    along the key axis, so it turns rather than stacks; its lower-right rim is the deepest stop."""
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            nx = (x - cx) / float(rx)
            ny = (y - cy) / float(ry)
            d = math.sqrt(nx * nx + ny * ny)
            if d > 1.0:
                continue
            # 1 on the key side, -1 away from it, pulled down at the rim.
            v = (-nx - ny) / 1.4142 * 0.9 - (0.35 if d > 0.82 else 0.0) + 0.15
            v += (hashed(x * 31 + y, salt) - 0.5) * 0.1
            t = (v + 1.0) / 2.0 * (len(tones) - 1)
            i = max(0, min(len(tones) - 2, int(t)))
            f = t - i
            # tones run light to dark; t runs dark to light
            hi_i = len(tones) - 1 - i
            lo = tones[hi_i]
            up = tones[hi_i - 1]
            c.set(x, y, up if f > th(x, y) else lo)


# --- F4, the First Tree's clearing ------------------------------------------------------------------

CW, CH = 220, 140
CCX, CCY = 110, 78          # the bed's middle: FirstTree.cs puts it on the node
RING_RX, RING_RY = 85, 53   # FirstTree.RingRadius (170) at x2, flattened to 0.62
BED_RX, BED_RY = 23, 11     # FirstTree.BedRadius (46) at x2, flattened by half
GROUND_RX, GROUND_RY = 105, 63
STUMPS = 9


def dry_ground(c):
    """The clearing: a worn patch of dry earth, thick in the middle and thinning to nothing at its edge,
    so the ring reads as a place and not as scatter - and with no edge a player could learn to stand on."""
    for y in range(CCY - GROUND_RY, CCY + GROUND_RY + 1):
        for x in range(CCX - GROUND_RX, CCX + GROUND_RX + 1):
            d = math.hypot((x - CCX) / float(GROUND_RX), (y - CCY) / float(GROUND_RY))
            if d > 1.0:
                continue
            cover = 0.8 * (1.0 - d * d) + 0.12 * math.sin(x * 0.37 + y * 0.21) + 0.08 * math.sin(x * 0.11 - y * 0.53)
            if cover > th(x, y):
                c.set(x, y, LN[3])
                if cover > 0.7 and hashed(x * 7 + y * 131, 3) < 0.05:
                    c.set(x, y, LN[4])
    # Clods and pebbles, only where the ground is bare.
    for k in range(26):
        a = hashed(k, 11) * math.pi * 2
        r = math.sqrt(hashed(k, 13)) * 0.85
        x = int(CCX + math.cos(a) * GROUND_RX * r)
        y = int(CCY + math.sin(a) * GROUND_RY * r)
        if k % 3 == 0:
            c.set(x, y, ST[1])
            c.set(x + 1, y, ST[2])
            c.set(x, y + 1, OCC)
            c.set(x + 1, y + 1, OCC)
        else:
            c.set(x, y, GO[4])
            c.set(x + 1, y, GO[4] if k % 2 else FL[4])


def stump(c, fx, fy, w, h):
    """A cut trunk flaring to its roots, its top showing the rings - wood, not rock. Lit on the left."""
    half = w // 2
    top = fy - h
    c.hline(fx - half - 4, fx + half + 4, fy + 1, OCC)
    # The roots: four rows flaring out from the trunk to the ground, the far side in shadow.
    for k in range(4):
        y = fy - k
        spread = 4 - k
        for x in range(fx - half - spread, fx + half + spread + 1):
            if x < fx - half + 1:
                tone = GO[2] if k > 0 and (x + y) % 2 == 0 else GO[3]
            elif x > fx + half - 2:
                tone = GO[4]
            else:
                tone = GO[3]
            c.set(x, y, tone)
        # a gap between the root on the left and the next, so they read as roots
        c.set(fx - half - spread + 2, y, GO[4] if k < 2 else None)
    # The trunk, a pixel narrower at the cut than at the ground.
    for y in range(top, fy - 3):
        inset = 1 if y < top + h * 0.4 else 0
        x0, x1 = fx - half + inset, fx + half - inset
        for x in range(x0, x1 + 1):
            u = (x - x0) / float(max(1, x1 - x0))
            if u < 0.22:
                tone = GO[2] if u < 0.1 or th(x, y) < 0.45 else GO[3]
            elif u < 0.72:
                tone = GO[3]
            else:
                tone = GO[4] if u > 0.85 or th(x, y) < 0.5 else GO[3]
            c.set(x, y, tone)
        # bark furrows
        for x in range(x0 + 2, x1 - 1, 3):
            if (y + x) % 5 != 0:
                c.set(x, y, GO[4])
    # The cut: heartwood, one ring and a dark heart; the lip catches the key on its upper left.
    crx = max(2, half - 1)
    cry = max(2, int(round(half * 0.45)))
    c.disc(fx, top, crx, cry, FL[2])
    for y in range(top - cry, top + cry + 1):
        for x in range(fx - crx, fx + crx + 1):
            nx = (x - fx) / float(crx)
            ny = (y - top) / float(cry)
            d = math.sqrt(nx * nx + ny * ny)
            if 0.78 < d <= 1.0:
                c.set(x, y, FL[1] if nx + ny < -0.3 else (GO[3] if nx + ny > 0.4 else FL[2]))
    if crx >= 4:
        c.ring(fx, top, crx * 0.55, max(1.2, cry * 0.55), FL[3])
    c.set(fx, top, GO[4])
    c.set(fx + 1, top, GO[4])
    # a radial check in the cut, the dry crack every old stump has
    c.line(fx + 1, top, fx + crx - 1, top + 1, FL[3])


def seed_bed(c, planted):
    """A patch of dark earth, cracked dry until the sapling is in it; turned over once it is."""
    for y in range(CCY - BED_RY, CCY + BED_RY + 1):
        for x in range(CCX - BED_RX, CCX + BED_RX + 1):
            nx = (x - CCX) / float(BED_RX)
            ny = (y - CCY) / float(BED_RY)
            d = math.sqrt(nx * nx + ny * ny)
            if d > 1.0:
                continue
            tone = LN[4]
            # its lip: the far edge lit, the near edge sunk
            if d > 0.8:
                tone = LN[3] if ny < -0.2 else OCC if ny > 0.4 else LN[4]
            elif hashed(x * 13 + y * 7, 21) < 0.08:
                tone = FL[4]
            c.set(x, y, tone)
    if not planted:
        # FirstTree's old cracks, at the same places (its px halved): dry, so the light stays dim in it.
        for (x0, y0, x1, y1) in ((-15, -2, -4, 1), (-4, 1, 3, -4), (3, -4, 14, 2), (-2, 1, -1, 7)):
            c.line(CCX + x0, CCY + y0, CCX + x1, CCY + y1, OCC)
    else:
        # Turned earth: a low mound lit on its upper left, a dark ring where the spade went in.
        blob(c, CCX, CCY - 1, 10, 4.5, [FL[3], FL[4], LN[4], OCC], salt=5)
        c.ring(CCX, CCY, 12, 6, OCC)


def stump_spots():
    """The nine stumps, as FirstTree.cs placed them: (foot x, foot y, width, height) in art pixels."""
    spots = []
    for i in range(STUMPS):
        angle = i * math.pi * 2 / STUMPS + 0.3
        x = int(round(CCX + math.cos(angle) * RING_RX))
        y = int(round(CCY + math.sin(angle) * RING_RY))
        w = int((26 + (i * 7 % 3) * 6) / 2)
        h = int((20 + (i * 5 % 4) * 5) / 2)
        spots.append((x, y, w, h))
    return sorted(spots, key=lambda s: s[1])


def clearing(planted):
    c = raster.Canvas(CW, CH, None)
    dry_ground(c)
    spots = stump_spots()
    # The bed sits among the stumps: those behind it are drawn first.
    for s in spots:
        if s[1] <= CCY:
            stump(c, *s)
    seed_bed(c, planted)
    for s in spots:
        if s[1] > CCY:
            stump(c, *s)
    return c


# --- the sapling --------------------------------------------------------------------------------------

SW, SH = 48, 60
SFX, SFY = 24, 57                     # its foot: FirstTree.cs stands it on the bed's middle
STEM = [3, 8, 15, 22, 29, 35]         # 10 + 35t art pixels at the end of each second of its growth
CROWN = [0, 0, 0, 6, 9, 12]


def leaf(c, x, y, side, big=False):
    """A leaf off the stem: a little lit blade of verdant pointing up and out."""
    pts = [(0, 0), (side * 1, -1), (side * 2, -1), (side * 3, -2)]
    if big:
        pts += [(side * 4, -2), (side * 2, -2), (side * 3, -3)]
    for (dx, dy) in pts:
        c.set(x + dx, y + dy, VE[2] if dy < -1 or side < 0 else VE[3])
    c.set(x + side * 1, y, VE[4])
    c.set(x + side * 2, y, VE[4] if big else None)


def sapling(k):
    c = raster.Canvas(SW, SH, None)
    h = STEM[k]
    c.hline(SFX - 3, SFX + 3, SFY + 1, OCC)
    wide = k >= 3
    for y in range(SFY - h, SFY + 1):
        c.set(SFX - 1, y, GO[2] if (y % 4) else GO[1])
        c.set(SFX, y, GO[3])
        if wide and y > SFY - h + 4:
            c.set(SFX + 1, y, GO[4])
    if k == 0:
        # A sprout: two seed leaves at the top of a short stem.
        leaf(c, SFX - 1, SFY - h, -1)
        leaf(c, SFX, SFY - h, 1)
        return c
    # Leaves along the stem, alternating, two more as it grows.
    for j in range(1 + min(k, 3)):
        y = SFY - 3 - int((h - 4) * (j + 1) / float(min(k, 3) + 2))
        leaf(c, SFX - 1 if j % 2 == 0 else SFX + (1 if wide else 0), y, -1 if j % 2 == 0 else 1, big=k >= 2)
    if CROWN[k] == 0:
        leaf(c, SFX - 1, SFY - h, -1)
        leaf(c, SFX, SFY - h - 1, 1)
        return c
    r = CROWN[k]
    cy = SFY - h
    tones = [VE[1], VE[2], VE[3], VE[4]]
    # The crown: three lobes, the back one darker, lit on the upper left.
    blob(c, SFX + r * 0.45, cy + r * 0.2, r * 0.6, r * 0.5, [VE[2], VE[3], VE[4], OCC], salt=k)
    blob(c, SFX, cy, r, r * 0.8, tones, salt=k + 10)
    blob(c, SFX - r * 0.4, cy - r * 0.3, r * 0.55, r * 0.45, tones, salt=k + 20)
    return c


# --- F5, the Pale Hart ----------------------------------------------------------------------------------
#
# "A hart of warm light at the edge of a clearing, the only warm thing that moves and is not you." It is
# the light he missed, in the one shape a wood would make of it: drawn wholly from the warm rows - gold
# and the Light element's ramp - with no rim and no outline (it is a light, not a body the key falls on),
# its underside and far legs a stop or two down the gold row so it still has a form. Everything else in
# the Forest is cold; this is why it reads in a crowd.

LT = bl.ELEMENTS["light"]          # core, hot, mid, edge
HW, HH = 48, 48                    # the elite cell (.ai/art-direction.md section 1): 96 px on screen
HFOOT = 45                         # its hooves stand on this row; the contact line is the row under it

BODY = [GO[0], LT[2], GO[1], GO[2]]


def limb(c, pts, near):
    """A leg as a run of segments two pixels wide: the near legs lit on their front edge, the far legs a
    stop down. pts run from the hip to the hoof."""
    a, b = (LT[2], GO[1]) if near else (GO[1], GO[2])
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        c.line(x0, y0, x1, y1, a)
        c.line(x0 + 1, y0, x1 + 1, y1, b)
    hx, hy = pts[-1]
    c.set(hx, hy, GO[2])
    c.set(hx + 1, hy, GO[3])


# Poses, facing right: (body centre, body tilt in px across its length, head anchor, four legs from the
# hip, each a list of points: far hind, far fore, near hind, near fore).
POSES = {
    # standing, watching: straight legs, head up
    "stand0": ((22, 27), 0, (34, 12),
               [[(15, 30), (14, 37), (15, HFOOT)], [(28, 31), (28, 38), (29, HFOOT)],
                [(13, 30), (12, 37), (13, HFOOT)], [(30, 31), (31, 38), (31, HFOOT)]]),
    # the same, the head a pixel lower and the tail flicked: the breath of a waiting animal
    "stand1": ((22, 27), 0, (34, 13),
               [[(15, 30), (14, 37), (15, HFOOT)], [(28, 31), (28, 38), (29, HFOOT)],
                [(13, 30), (12, 37), (13, HFOOT)], [(30, 31), (31, 38), (31, HFOOT)]]),
    # gathering: haunches down, forelegs off the ground and folded
    "leap0": ((22, 27), -3, (35, 10),
              [[(15, 31), (12, 37), (16, HFOOT)], [(29, 29), (33, 33), (31, 37)],
               [(13, 31), (10, 38), (14, HFOOT)], [(31, 29), (35, 32), (34, 36)]]),
    # stretched out in the air, every hoof off the ground
    "leap1": ((23, 22), 0, (37, 9),
              [[(16, 25), (10, 30), (4, 33)], [(30, 25), (36, 30), (42, 31)],
               [(14, 25), (8, 29), (2, 31)], [(32, 25), (38, 28), (44, 29)]]),
    # landing: forelegs reaching down, hind legs still behind
    "leap2": ((23, 25), 3, (36, 13),
              [[(16, 28), (11, 31), (6, 34)], [(30, 30), (32, 37), (33, HFOOT)],
               [(14, 28), (9, 32), (5, 36)], [(32, 30), (35, 37), (36, HFOOT)]]),
}
HART_POSES = ["stand0", "stand1", "leap0", "leap1", "leap2"]


def hart(pose):
    c = raster.Canvas(HW, HH, None)
    (bx, by), tilt, (hx, hy), legs = POSES[pose]
    airborne = pose == "leap1"
    if not airborne:
        c.hline(bx - 11, bx + 11, HFOOT + 1, OCC)
    # the far legs first, behind the body
    limb(c, legs[0], False)
    limb(c, legs[1], False)
    # the body: a long lit mass; tilt lifts the chest (negative) or the haunch (positive)
    for k in range(-11, 12):
        cy = by - tilt * k / 11.0
        blob(c, bx + k, cy, 1.6, 5.2 - abs(k) * 0.12, BODY, salt=7)
    blob(c, bx - 8, by - tilt * -8 / 11.0 - 1, 5, 5.5, BODY, salt=8)       # the haunch
    blob(c, bx + 8, by - tilt * 8 / 11.0 - 1, 5, 5.8, BODY, salt=9)        # the chest
    # the neck, rising forward to the head
    nx, ny = bx + 9, by - tilt * 9 / 11.0 - 4
    for k in range(6):
        t = k / 5.0
        blob(c, nx + (hx - 2 - nx) * t, ny + (hy + 4 - ny) * t, 2.6, 2.6, BODY, salt=11 + k)
    # the head: a wedge to the muzzle, an ear, the eye a dark point
    blob(c, hx, hy + 2, 3.2, 2.6, BODY, salt=21)
    blob(c, hx + 3, hy + 3, 2.2, 1.6, BODY, salt=22)
    c.set(hx + 5, hy + 3, GO[1])
    c.set(hx + 5, hy + 4, GO[2])
    c.set(hx - 2, hy, GO[1])
    c.set(hx - 3, hy - 1, LT[2])
    c.set(hx + 1, hy + 1, GO[3])
    # the antlers: two beams sweeping back and up, three tines each, the tips the light ramp's hot
    # The far beam sweeps back, the near one stands up a little forward of it, so the pair spreads in a V.
    for side, sweep in ((1, -1), (0, 1)):
        tone = LT[1] if side == 0 else GO[1]
        tip = LT[1] if side == 0 else LT[2]
        bx0, by0 = hx - 1, hy - 2
        if sweep < 0:
            beam = [(bx0, by0), (bx0 - 3, by0 - 3), (bx0 - 5, by0 - 6), (bx0 - 6, by0 - 10)]
        else:
            beam = [(bx0, by0), (bx0, by0 - 3), (bx0 + 1, by0 - 7), (bx0, by0 - 11)]
        for (x0, y0), (x1, y1) in zip(beam, beam[1:]):
            c.line(x0, y0, x1, y1, tone)
        for (tx, ty), (dx, dy) in zip(beam[1:], ((2, -1), (3, -1), (2, -2))):
            c.line(tx, ty, tx + dx, ty + dy, tone)
            c.set(tx + dx, ty + dy, tip)
    # the tail, a pale flag; up in the second breath and in the leap
    tx, ty = bx - 12, by - 3 - tilt
    c.set(tx, ty, GO[0])
    c.set(tx - 1, ty - (1 if pose in ("stand1", "leap0", "leap1") else 0), GO[0])
    # the near legs, over the body
    limb(c, legs[2], True)
    limb(c, legs[3], True)
    return c


def hart_strip():
    """Ten frames: the five poses facing right, then the same five facing left (mirrored)."""
    frames = [hart(p) for p in HART_POSES]
    right = [f.scaled(SCALE) for f in frames]
    left = [img.transpose(Image.FLIP_LEFT_RIGHT) for img in right]
    out = Image.new("RGBA", (HW * SCALE * len(right) * 2, HH * SCALE), (0, 0, 0, 0))
    for i, img in enumerate(right + left):
        out.alpha_composite(img, (i * HW * SCALE, 0))
    return frames, out


# The hunter's high seat: a raised hide on four slanted legs, a ladder up the front, a railed box with a
# pent roof - weathered boards in linen, posts in gold's lower stops. Its bow of light hangs from the
# front post; the second frame is the seat with only the peg, once the light has gone to him.
SEAT_W, SEAT_H = 44, 68
SEAT_FX, SEAT_FY = 22, 65            # the middle of its feet: PaleHart.cs stands it on the path's last stand
BOW_X, BOW_Y = 35, 30                # the bow's grip: the glint and the light leave from here


def post(c, x0, y0, x1, y1, near):
    a, b = (GO[2], GO[3]) if near else (GO[3], GO[4])
    c.line(x0, y0, x1, y1, a)
    c.line(x0 + 1, y0, x1 + 1, y1, b)


def board(c, x0, x1, y, tone_top, tone):
    c.hline(x0, x1, y, tone_top)
    c.hline(x0, x1, y + 1, tone)


def high_seat(with_bow):
    c = raster.Canvas(SEAT_W, SEAT_H, None)
    # contact: the four feet
    for fx in (6, 13, 31, 38):
        c.hline(fx - 1, fx + 2, SEAT_FY + 1, OCC)
    # the back legs, behind everything
    post(c, 13, SEAT_FY, 15, 34, False)
    post(c, 31, SEAT_FY, 29, 34, False)
    # cross-brace between the back legs
    c.line(14, 56, 30, 44, GO[4])
    c.line(14, 44, 30, 56, GO[4])
    # the box: floor, front and side boards, rail
    for y in range(26, 36, 2):
        board(c, 9, 35, y, LN[2] if y != 34 else LN[3], LN[3])
    c.hline(9, 35, 36, OCC)
    c.vline(9, 18, 35, GO[3])
    c.vline(35, 18, 35, GO[4])
    board(c, 9, 35, 18, LN[1], LN[2])
    # corner posts up to the roof
    c.vline(10, 9, 25, GO[2])
    c.vline(34, 9, 25, GO[4])
    # the pent roof, sloping down to the back (right), lit along its front edge
    for k in range(6):
        c.hline(6 + k // 3, 38 - k // 3, 6 + k, LN[1] if k == 0 else (LN[2] if k < 3 else LN[3]))
    c.hline(6, 38, 12, OCC)
    # the front legs, over the box's underside
    post(c, 6, SEAT_FY, 11, 36, True)
    post(c, 38, SEAT_FY, 33, 36, True)
    # the ladder: two rails and rungs, leaning against the front
    c.line(16, SEAT_FY, 18, 37, GO[2])
    c.line(24, SEAT_FY, 26, 37, GO[3])
    for y in range(40, SEAT_FY, 5):
        t = (SEAT_FY - y) / float(SEAT_FY - 37)
        x0 = int(16 + 2 * t)
        c.hline(x0 + 1, x0 + 7, y, GO[2])
        c.hline(x0 + 1, x0 + 7, y + 1, GO[4])
    # the peg on the front post
    c.set(36, 22, GO[1])
    c.set(37, 22, GO[3])
    if with_bow:
        # A short bow of light hanging by its string from the peg: the stave the Light ramp's edge and
        # mid, lit along its back, the string a single line of hot.
        stave = [(36, 23), (38, 24), (40, 27), (41, 30), (40, 33), (38, 36), (36, 37)]
        for (x0, y0), (x1, y1) in zip(stave, stave[1:]):
            c.line(x0, y0, x1, y1, LT[3])
            c.line(x0 - 1, y0, x1 - 1, y1, LT[2])
        c.line(35, 23, 35, 37, LT[1])
        c.set(BOW_X + 4, BOW_Y, LT[1])
        c.set(BOW_X + 4, BOW_Y + 1, LT[1])
    return c


# The far tell: hoofprints pressed into the moss, and bracken bent one way - both drawn facing RIGHT,
# which SecretFarTell turns toward the site (LeanToward): the prints walk to the clearing, the bracken was
# pushed over by what went that way.
def hoofprints(k):
    c = raster.Canvas(28, 12, None)
    # a cloven print: two little crescents, the dark of the pressed moss and its lit far lip
    def print_(x, y):
        for (dx, dy) in ((0, 0), (1, 0), (0, 1), (1, 1), (2, 1)):
            c.set(x + dx, y + dy, OCC)
            c.set(x + dx, y + dy + 3, OCC)
        c.set(x + 2, y, LI[2])
        c.set(x + 3, y + 3, LI[2])
        c.set(x - 1, y + 1, LI[4])
        c.set(x - 1, y + 4, LI[4])
    steps = ((2, 2), (10, 5), (17, 1), (24, 4)) if k == 0 else ((3, 4), (12, 1), (20, 5))
    for (x, y) in steps:
        print_(x, y)
    return c


def bracken(k):
    """A frond of bracken, broken over to the right, browning: the gold row's lower stops on a verdant
    stem."""
    c = raster.Canvas(26, 24, None)
    base_x, base_y = 6 + k * 2, 22
    c.hline(base_x - 2, base_x + 3, base_y + 1, OCC)
    # the stem rises, then bends hard over to the right and down
    stem = [(base_x, base_y), (base_x + 1, base_y - 7), (base_x + 3, base_y - 12), (base_x + 8, base_y - 15),
            (base_x + 14, base_y - 13), (base_x + 18 - k, base_y - 9)]
    for (x0, y0), (x1, y1) in zip(stem, stem[1:]):
        c.line(x0, y0, x1, y1, VE[4])
    # pinnae hanging off the bent part, rust and dead gold, lit on top
    for i, (x, y) in enumerate(stem[2:]):
        for j in range(3 + (i % 2)):
            tone = GO[2] if j == 0 else (GO[3] if (i + j) % 2 else FL[3])
            c.set(x - 1 + j, y + 1 + j, tone)
            c.set(x + 1 + j, y + 1 + j // 2, GO[3] if j else GO[2])
    # a second, upright frond behind, still green, so the bent one reads as bent
    for y in range(base_y - 8 + k, base_y + 1):
        c.set(base_x - 3, y, VE[3])
        if y % 2 == 0:
            c.set(base_x - 4, y, VE[3])
            c.set(base_x - 2, y - 1, VE[2])
    return c


# --- out ----------------------------------------------------------------------------------------------

def audit(name, c):
    bad = c.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:5]))
        return False
    return True


def strip(frames, w, h):
    img = Image.new("RGBA", (w * SCALE * len(frames), h * SCALE), (0, 0, 0, 0))
    for i, c in enumerate(frames):
        img.alpha_composite(c.scaled(SCALE), (i * w * SCALE, 0))
    return img


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ok = True
    sheets = []

    frames = [clearing(False), clearing(True)]
    for i, c in enumerate(frames):
        ok = audit("first-tree-clearing frame %d" % i, c) and ok
    img = strip(frames, CW, CH)
    img.save(os.path.join(OUT, "first-tree-clearing.png"))
    sheets.append(img)

    frames = [sapling(k) for k in range(len(STEM))]
    for i, c in enumerate(frames):
        ok = audit("first-tree-sapling frame %d" % i, c) and ok
    img = strip(frames, SW, SH)
    img.save(os.path.join(OUT, "first-tree-sapling.png"))
    sheets.append(img)
    print("  first tree   clearing 2 frames at %dx%d, sapling %d frames at %dx%d"
          % (CW * SCALE, CH * SCALE, len(STEM), SW * SCALE, SH * SCALE))

    frames, img = hart_strip()
    for p, c in zip(HART_POSES, frames):
        ok = audit("pale-hart %s" % p, c) and ok
    img.save(os.path.join(OUT, "pale-hart.png"))
    sheets.append(img)

    frames = [high_seat(True), high_seat(False)]
    for i, c in enumerate(frames):
        ok = audit("high-seat frame %d" % i, c) and ok
    img = strip(frames, SEAT_W, SEAT_H)
    img.save(os.path.join(OUT, "high-seat.png"))
    sheets.append(img)

    row = []
    for name, c in (("hoofprints-1", hoofprints(0)), ("hoofprints-2", hoofprints(1)),
                    ("bracken-bent-1", bracken(0)), ("bracken-bent-2", bracken(1))):
        ok = audit(name, c) and ok
        one = c.scaled(SCALE)
        one.save(os.path.join(OUT, name + ".png"))
        row.append(one)
    tells = Image.new("RGBA", (sum(r.size[0] + 8 for r in row), max(r.size[1] for r in row)), (0, 0, 0, 0))
    x = 0
    for r in row:
        tells.alpha_composite(r, (x, 0))
        x += r.size[0] + 8
    sheets.append(tells)
    print("  pale hart    %d frames at %dx%d, high seat 2 frames at %dx%d, 2 prints, 2 bracken"
          % (len(HART_POSES) * 2, HW * SCALE, HH * SCALE, SEAT_W * SCALE, SEAT_H * SCALE))

    # The preview: each sheet on the forest's turf (lichen shade), stacked.
    pad = 10
    pw = max(s.size[0] for s in sheets) + pad * 2
    ph = sum(s.size[1] for s in sheets) + pad * (len(sheets) + 1)
    sheet = Image.new("RGBA", (pw, ph), raster.rgb(LI[3]))
    y = pad
    for s in sheets:
        sheet.alpha_composite(s, (pad, y))
        y += s.size[1] + pad
    sheet.save(PREVIEW)
    print("preview: tools/art/_secret_forest_preview.png")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
