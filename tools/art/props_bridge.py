# -*- coding: utf-8 -*-
"""The Last Bridge's standing stone (.ai/endgame-castle.md 3.7). Run: python tools/art/props_bridge.py

Writes assets/bonelight/world/bridge/bridge_sheet.png - every piece on one sheet, authored 1:1 and saved
at x2 like every world sprite - and bridge.json beside it: each piece's region on the sheet and its
ANCHOR, the pixel that stands on the point BridgeFurniture (scripts/BridgeRules.cs) places it at. One
sheet, so the whole parapet of a screen is one texture and batches.

THE PIECES, and the line each stands on (the shores are BridgeBand's, LevelGenerator.Band.cs):

  parapet_nw     the top-left shore, "/": the drop behind it. Shows its INNER face, toward the deck.
  parapet_se     the bottom-right shore, "/": the drop in front. Shows its OUTER face and below it the
                 bridge's flank going down into the mist.
  parapet_ne     a jog across the bridge, "\", with the drop north-east (where the deck narrows): inner face.
  parapet_sw     a jog with the drop south-west (where it widens again): outer face and flank.
     (+ _banner, _breach variants of the long ones)
  turret         a small square turret at every corner where the shore turns.
  tower_nw/_se   a gatehouse's towers, standing in the drop either side of the narrows.
  castle_wall    his castle's wall across the far end of the gate yard, "\", the drop beyond it.
  gate_shut/open the castle gate in the middle of that wall: the way home once the Constable falls.

A PARAPET AT 45 DEGREES IS NOT TILES. A wall drawn as tiles on a square grid at 45 degrees is a
staircase (ruled out, 3.2). These are drawn as what they are - boxes in three-quarter view - by a small
projector here: a point (x, y) on the ground at height z is drawn at (x, y - z), a face is shaded by
which way it faces, and only faces turned toward the viewer (south, south-east, south-west) are drawn.
Each "/" or "\" piece is one step of its line, 24 art px (48 world) in x and in y, so consecutive pieces
meet exactly and the wall runs on unbroken.

THE LIGHT. The key is upper-left (.ai/art-direction.md section 2): tops are lit, a face looking south-west
or south is half-lit, a face looking south-east is in shadow, and the arris toward the key catches it.
Stone's row only, from its lit stop down, and `occ` at contact lines and in the drop: the bridge is his
built stone, cold, with nothing warm on it. His light - the arcane row round a pale core, the cold mark his
sconces and creatures carry (.ai/world-and-tone.md) - burns in the towers' slits and the gate when it opens.
His banners are dyed cloth, violet's deep half: pigment, not light.
"""
from __future__ import division, print_function

import io
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, "assets", "bonelight", "world", "bridge")
SCALE = 2


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


ST = ramp("stone")
AR = ramp("arcane")
VI = ramp("violet")
STL = ramp("steel")

R2 = math.sqrt(0.5)
STEP = 24                      # one step of a 45-degree line, art px, in x and in y
STEP_LEN = STEP * math.sqrt(2.0)


def h01(a, b, salt):
    v = (a * 73856093) ^ (b * 19349663) ^ (salt * 83492791)
    v &= 0x7FFFFFFF
    v = ((v ^ (v >> 13)) * 1274126177) & 0x7FFFFFFF
    return ((v >> 7) % 10007) / 10007.0


class Piece(object):
    """A canvas with the anchor at (ox, oy): every coordinate below is relative to the anchor."""

    def __init__(self, w, h, ox, oy):
        self.c = raster.Canvas(w, h, None)
        self.ox, self.oy = ox, oy

    def put(self, x, y, col):
        self.c.set(int(math.floor(x)) + self.ox, int(math.floor(y)) + self.oy, col)

    def get(self, x, y):
        return self.c.get(int(math.floor(x)) + self.ox, int(math.floor(y)) + self.oy)

    def face(self, O, U, width, v0, v1, shade, clip=None):
        """A vertical face: ground point O (x, y) at height O[2], running along the ground vector U for
        `width`, from height v0 to v1 (absolute). shade(u, v) -> colour or None. Drawn pixel by pixel by
        inverting the projection, so masonry can be laid in the face's own (u, v)."""
        ox, oy, oz = O
        ux, uy = U
        corners = [(ox + ux * a, oy + uy * a - z) for a in (0.0, width) for z in (v0, v1)]
        xs = [p[0] for p in corners]
        ys = [p[1] for p in corners]
        for sy in range(int(math.floor(min(ys))) - 1, int(math.ceil(max(ys))) + 1):
            for sx in range(int(math.floor(min(xs))) - 1, int(math.ceil(max(xs))) + 1):
                px, py = sx + 0.5, sy + 0.5
                if abs(ux) < 1e-6:
                    continue
                u = (px - ox) / ux
                if u < 0.0 or u >= width:
                    continue
                v = oy + uy * u - py
                if v < v0 or v >= v1:
                    continue
                if clip is not None and not clip(px, py, u, v):
                    continue
                col = shade(u, v)
                if col is not None:
                    self.put(sx, sy, col)

    def top(self, poly, z, shade):
        """A flat top at height z over the ground polygon `poly`; shade(gx, gy) -> colour."""
        pts = [(x, y - z) for x, y in poly]
        ys = [p[1] for p in pts]
        n = len(pts)
        for sy in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
            py = sy + 0.5
            xs = []
            for i in range(n):
                ax, ay = pts[i]
                bx, by = pts[(i + 1) % n]
                if ay == by:
                    continue
                if min(ay, by) <= py < max(ay, by):
                    xs.append(ax + (py - ay) * (bx - ax) / (by - ay))
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                for sx in range(int(math.ceil(xs[i] - 0.5)), int(math.floor(xs[i + 1] - 0.5)) + 1):
                    col = shade(sx + 0.5, py + z)
                    if col is not None:
                        self.put(sx, sy, col)

    def finish(self):
        """Crop to what was drawn; returns (canvas, anchor x, anchor y) in art px."""
        img = self.c.img
        bbox = img.getbbox()
        if bbox is None:
            return self.c, self.ox, self.oy
        x0, y0, x1, y1 = bbox
        out = raster.Canvas(x1 - x0, y1 - y0, None)
        for y in range(y0, y1):
            for x in range(x0, x1):
                out.set(x - x0, y - y0, self.c.get(x, y))
        return out, self.ox - x0, self.oy - y0


# --- masonry ---------------------------------------------------------------------------------------

def masonry(face_tone, joint_tone, course=4, block=7, along_px=1.0, seed=0):
    """Cut stone laid in courses, in a face's own (u, v). along_px: screen px per unit of u, so a
    diagonal face's joints are as far apart on screen as a square one's."""
    def shade(u, v):
        row = int(math.floor(v / course))
        if v - row * course < 1.0:
            return joint_tone
        x = u * along_px + (row % 2) * (block // 2) + h01(row, seed, 501) * 2
        if x % block < 1.0:
            return joint_tone
        return face_tone
    return shade


def flank(face_tone, joint_tone, depth, along_px=1.0, seed=0):
    """The bridge's side going down into the drop below the deck: masonry fading through the deep stop
    to occlusion, dithered, so it sinks into the mist rather than ending at a line."""
    stone = masonry(face_tone, joint_tone, 5, 9, along_px, seed)

    def shade(u, v):
        k = -v / float(depth)                 # 0 at the deck, 1 at the bottom
        b = raster.BAYER8[int(math.floor(-v)) % 8][int(math.floor(u * along_px)) % 8] / 64.0
        if k > 0.85 or k > 0.55 + b * 0.4:
            return OCC if k < 0.95 or b < 0.5 else None
        if k > 0.3 + b * 0.25:
            return ST[4]
        return stone(u, v)
    return shade


def lit_top(gx, gy, edge_nw=None):
    return ST[1]


# --- the long parapets ---------------------------------------------------------------------------

H = 8            # the wall's height above the deck, art px (16 world)
T = 5            # its thickness
M = 6            # a merlon above the wall
DROP = 26        # how far the flank shows below the deck before the mist takes it


def slash(outer, variant):
    """A "/" step: from the anchor (0, 0) up-right to (24, -24). outer: the drop is south-east of the
    line (the bottom-right shore), so the outer face and the flank show; otherwise the drop is
    north-west (the top-left shore) and the inner face shows."""
    p = Piece(80, 90, 28, 52)
    D = (R2, -R2)
    N = (R2, R2)                           # toward the south-east
    off = T if outer else -T               # where the footprint lies, across the line
    # The footprint: the line and the line moved `off` across.
    a0 = (0.0, 0.0)
    a1 = (STEP, -STEP)
    b0 = (N[0] * off, N[1] * off)
    b1 = (STEP + N[0] * off, -STEP + N[1] * off)
    face_line = b0 if outer else a0        # the visible face is on the south-east side of the wall
    breach = variant == "breach"
    height = H - 3 if breach else H

    def coping(gx, gy):
        # The arris toward the key (the north-west edge) catches it; the rest is lit stone.
        d = (gx - a0[0]) * N[0] + (gy - a0[1]) * N[1]      # distance across, from the line
        nw_edge = min(0.0, off) if True else 0.0
        if d - nw_edge < 1.0:
            return ST[0]
        return ST[1]

    p.top([a0, a1, b1, b0], height, coping)
    se = masonry(ST[3], ST[4], 4, 7, R2 * 2, 11)
    if outer:
        p.face((face_line[0], face_line[1], 0.0), D, STEP_LEN, -DROP, 0.0, flank(ST[3], ST[4], DROP, R2 * 2, 13))
        p.face((face_line[0], face_line[1], 0.0), D, STEP_LEN, 0.0, 1.0, lambda u, v: ST[2])   # string course at the deck
    p.face((face_line[0], face_line[1], 0.0), D, STEP_LEN, 0.0 if not outer else 1.0, height,
           lambda u, v: (OCC if (not outer and v < 1.0) else se(u, v)))
    if breach:
        # Fallen merlons: a few blocks of rubble on the coping and a dark notch in it.
        for k in range(5):
            gx = 6 + k * 3 + int(h01(k, 1, 601) * 2)
            gy = -gx + (off * 0.5)
            p.put(gx, gy - height - 1, ST[2])
            p.put(gx + 1, gy - height - 1, ST[3])
            p.put(gx, gy - height, OCC)
        return p.finish()

    # A merlon on the middle of the step; the gaps between steps' merlons are the crenels.
    m0, m1 = STEP_LEN * 0.22, STEP_LEN * 0.72
    q0 = (a0[0] + D[0] * m0, a0[1] + D[1] * m0)
    q1 = (a0[0] + D[0] * m1, a0[1] + D[1] * m1)
    r0 = (q0[0] + N[0] * off, q0[1] + N[1] * off)
    r1 = (q1[0] + N[0] * off, q1[1] + N[1] * off)
    south_west_end = (q0 if not outer else q0) if off > 0 else r0
    # The merlon's south-west end, across the wall's thickness (facing south-west: half-lit).
    start = q0 if off > 0 else r0
    p.face((start[0], start[1], 0.0), N, T, height, height + M, masonry(ST[2], ST[3], 3, 9, R2, 21))
    # Its south-east face.
    se_line = r0 if outer else q0
    p.face((se_line[0], se_line[1], 0.0), D, m1 - m0, height, height + M, masonry(ST[3], ST[4], 3, 6, R2 * 2, 23))
    p.top([q0, q1, r1, r0], height + M, lambda gx, gy: ST[1])
    # Its lit arris, the north-west edge of its top.
    nw0, nw1 = (q0, q1) if off > 0 else (r0, r1)
    for k in range(int(m1 - m0) + 1):
        f = k / max(1.0, (m1 - m0))
        p.put(nw0[0] + (nw1[0] - nw0[0]) * f, nw0[1] + (nw1[1] - nw0[1]) * f - height - M, ST[0])

    if variant == "banner":
        # His banner hung from the coping down the outer face and over the flank: violet's deep half,
        # a darker chevron, a notched foot. Cloth, so it hangs straight down the screen.
        cx = (b0[0] + b1[0]) * 0.5 + 1
        cy = (b0[1] + b1[1]) * 0.5
        top_y = cy - height + 1
        for dy in range(0, 22):
            for dx in range(-3, 4):
                y = top_y + dy
                if dy > 18 and (abs(dx) < 3 - (dy - 18)):
                    continue                    # the swallowtail notch
                tone = VI[3] if dx < 0 else VI[4]
                if dx == -3:
                    tone = VI[2]
                if 6 <= dy <= 11 and abs(dx) <= (11 - dy) // 2:
                    tone = VI[4] if dx < 0 else OCC
                p.put(cx + dx, y, tone)
        for dx in range(-4, 5):
            p.put(cx + dx, top_y - 1, ST[4])    # the pole it hangs from
    return p.finish()


def backslash(outer, tall=False):
    """A "\" step: from the anchor (0, 0) down-right to (24, 24). outer: the drop is south-west of the
    line, so the outer face and the flank show; otherwise the drop is north-east and the inner face
    shows. tall: the castle wall."""
    height = 60 if tall else H
    thick = 9 if tall else T
    merlon = 9 if tall else M
    p = Piece(90, 140 if tall else 90, 34, 104 if tall else 52)
    D = (R2, R2)
    N = (-R2, R2)                          # toward the south-west
    off = thick if outer else -thick
    a0 = (0.0, 0.0)
    a1 = (STEP, STEP)
    b0 = (N[0] * off, N[1] * off)
    b1 = (STEP + N[0] * off, STEP + N[1] * off)
    face_line = b0 if outer else a0         # the visible face is the wall's south-west side
    p.top([a0, a1, b1, b0], height, lambda gx, gy: ST[1])
    sw = masonry(ST[2], ST[3], 4 if not tall else 5, 7 if not tall else 9, R2 * 2, 31)
    if outer:
        p.face((face_line[0], face_line[1], 0.0), D, STEP_LEN, -DROP, 0.0, flank(ST[2], ST[3], DROP, R2 * 2, 33))
    p.face((face_line[0], face_line[1], 0.0), D, STEP_LEN, 0.0, height,
           lambda u, v: (OCC if (not outer and v < 1.0) else sw(u, v)))
    if tall:
        # An arrow slit with his light in it, on some steps of the wall: occ round a cold core.
        mid = STEP_LEN * 0.5
        for v in range(30, 38):
            for du in (-1, 0):
                u = mid + du * 1.4
                x = face_line[0] + D[0] * u
                y = face_line[1] + D[1] * u - v
                p.put(x, y, AR[2] if (32 <= v <= 35 and du == 0) else OCC)
        p.put(face_line[0] + D[0] * mid, face_line[1] + D[1] * mid - 34, AR[1])
    m0, m1 = STEP_LEN * 0.22, STEP_LEN * 0.72
    q0 = (D[0] * m0, D[1] * m0)
    q1 = (D[0] * m1, D[1] * m1)
    r0 = (q0[0] + N[0] * off, q0[1] + N[1] * off)
    r1 = (q1[0] + N[0] * off, q1[1] + N[1] * off)
    # The merlon's south-east end (facing south-east: in shadow), across the thickness.
    se_start = q1 if off < 0 else r1
    p.face((se_start[0] - N[0] * thick if off < 0 else se_start[0], 0.0, 0.0) if False else
           ((q1[0] if off > 0 else r1[0]), (q1[1] if off > 0 else r1[1]), 0.0),
           N, thick, height, height + merlon, masonry(ST[3], ST[4], 3, 9, R2, 35))
    sw_line = r0 if outer else q0
    p.face((sw_line[0], sw_line[1], 0.0), D, m1 - m0, height, height + merlon, masonry(ST[2], ST[3], 3, 6, R2 * 2, 37))
    p.top([q0, q1, r1, r0], height + merlon, lambda gx, gy: ST[1])
    return p.finish()


# --- towers, turrets and the gate ----------------------------------------------------------------

def box(p, x0, y0, w, d, z0, z1, south, top, clip=None):
    """An axis-aligned block: its south face and its top (the only parts three-quarter view shows)."""
    p.face((x0, y0 + d, 0.0), (1.0, 0.0), w, z0, z1, south, clip)
    p.top([(x0, y0), (x0 + w, y0), (x0 + w, y0 + d), (x0, y0 + d)], z1, top)


def crenellated_top(p, x0, y0, w, d, z, merlon=5, gap=3, mh=5):
    """A tower's top: a walled platform. The floor is in its rim's shadow; merlons stand on the south
    rim (seen from the front) and the north rim shows as a lit line of merlon tops."""
    p.top([(x0, y0), (x0 + w, y0), (x0 + w, y0 + d), (x0, y0 + d)], z,
          lambda gx, gy: ST[3] if (x0 + 2 <= gx <= x0 + w - 2 and y0 + 2 <= gy <= y0 + d - 2) else ST[1])
    x = x0
    k = 0
    while x < x0 + w - 1:
        mw = min(merlon, x0 + w - x)
        box(p, x, y0, mw, 2, z, z + mh, masonry(ST[2], ST[3], 3, 9, 1.0, 41 + k), lambda gx, gy: ST[1])
        x += merlon + gap
        k += 1
    x = x0
    k = 0
    while x < x0 + w - 1:
        mw = min(merlon, x0 + w - x)
        box(p, x, y0 + d - 2, mw, 2, z, z + mh, masonry(ST[2], ST[3], 3, 9, 1.0, 51 + k), lambda gx, gy: ST[1])
        for j in range(int(mw)):
            p.put(x + j, y0 + d - 2 - z - mh, ST[0])
        x += merlon + gap
        k += 1


def slit(p, x, ground_y, v0, v1):
    """An arrow slit in a south face with his cold light in it."""
    for v in range(v0, v1):
        for dx in (0, 1):
            p.put(x + dx, ground_y - v, AR[2] if (v0 + 2 <= v < v1 - 2 and dx == 0) else OCC)
    p.put(x, ground_y - (v0 + v1) // 2, AR[1])


def tower(nw):
    """A gatehouse tower rising out of the drop beside the narrows. nw: it stands north-west of the
    passage, anchored by its south-east corner; otherwise south-east, anchored by its north-west corner.
    Its foot goes down into the mist; on the north-west tower that part is cut where it would lie over
    the deck (the shore is x + y = 0 through the anchor)."""
    W, Ht, F = 36, 50, 34
    p = Piece(120, 160, 70, 100)
    x0, y0 = (-W, -W) if nw else (0, 0)
    south = masonry(ST[2], ST[3], 4, 8, 1.0, 61 if nw else 63)
    base = flank(ST[2], ST[3], F, 1.0, 65)

    def foot_clip(px, py, u, v):
        return not nw or px + py < -1.0

    p.face((x0, y0 + W, 0.0), (1.0, 0.0), W, -F, 0.0, base, foot_clip)
    p.face((x0, y0 + W, 0.0), (1.0, 0.0), W, 0.0, Ht, south)
    # A string course a third of the way up, and the slits with his light.
    p.face((x0, y0 + W, 0.0), (1.0, 0.0), W, 17.0, 18.0, lambda u, v: ST[1])
    slit(p, x0 + W // 2 - 1, y0 + W, 24, 33)
    slit(p, x0 + 7, y0 + W, 36, 42)
    slit(p, x0 + W - 9, y0 + W, 36, 42)
    crenellated_top(p, x0, y0, W, W, Ht)
    return p.finish()


def turret():
    """A small square turret on a corner of the parapet, where the shore turns."""
    W, Ht = 12, 16
    p = Piece(40, 50, 20, 34)
    x0, y0 = -W // 2, -W // 2
    p.face((x0, y0 + W, 0.0), (1.0, 0.0), W, 0.0, 1.0, lambda u, v: OCC)
    p.face((x0, y0 + W, 0.0), (1.0, 0.0), W, 1.0, Ht, masonry(ST[2], ST[3], 4, 6, 1.0, 71))
    p.top([(x0, y0), (x0 + W, y0), (x0 + W, y0 + W), (x0, y0 + W)], Ht,
          lambda gx, gy: ST[0] if gy < y0 + 1 or gx < x0 + 1 else ST[1])
    for k, x in enumerate((x0, x0 + W - 4)):
        box(p, x, y0 + W - 3, 4, 3, Ht, Ht + 4, masonry(ST[2], ST[3], 3, 9, 1.0, 73 + k), lambda gx, gy: ST[1])
        box(p, x, y0, 4, 3, Ht, Ht + 4, masonry(ST[2], ST[3], 3, 9, 1.0, 75 + k), lambda gx, gy: ST[1])
    return p.finish()


def gate(open_):
    """The castle gate: a "\" section of wall three steps either side of the anchor (its middle, on the
    wall's line), its face toward the gate yard, with a tower at each end and the arch in the middle.
    Shut, the portcullis is down: iron bars on the dark. Open, the way through is dark and his light
    lies on its floor - the way home."""
    Hw, thick = 64, 10
    p = Piece(240, 240, 120, 150)
    D = (R2, R2)
    N = (-R2, R2)
    half = STEP_LEN * 3.0
    a0 = (-D[0] * half, -D[1] * half)
    length = half * 2
    # The wall: top, then its south-west face, with the arch cut in the middle.
    a1 = (a0[0] + D[0] * length, a0[1] + D[1] * length)
    b0 = (a0[0] - N[0] * thick, a0[1] - N[1] * thick)
    b1 = (a1[0] - N[0] * thick, a1[1] - N[1] * thick)
    p.top([a0, a1, b1, b0], Hw, lambda gx, gy: ST[1])
    stone = masonry(ST[2], ST[3], 5, 9, R2 * 2, 81)
    mid = half
    arch_w, arch_h = 30.0, 40.0

    def wall_face(u, v):
        du = (u - mid) / (arch_w * 0.5)
        if abs(du) < 1.0:
            spring = arch_h - 8.0
            top = spring + 8.0 * math.sqrt(max(0.0, 1.0 - du * du))
            if v < top:
                if open_:
                    if v < 3.0:
                        return AR[3] if (int(u) + int(v)) % 2 else AR[4]   # his light on the floor
                    if v > top - 4.0:
                        return STL[3] if int(u * 1.4) % 3 else OCC         # the portcullis, raised
                    return OCC
                bar_u = int(math.floor(u * 1.4)) % 4 == 0
                bar_v = int(math.floor(v)) % 6 == 0
                if bar_u or bar_v:
                    return STL[3] if (bar_u and not bar_v) else STL[4]
                return OCC
            if v < top + 3.0:
                return ST[1] if du < 0 else ST[2]              # the voussoirs round the arch
        if v < 1.0:
            return OCC
        return stone(u, v)

    p.face((a0[0], a0[1], 0.0), D, length, 0.0, Hw, wall_face)
    # Merlons along the top.
    k = 0
    u = 3.0
    while u + 6.0 < length:
        q0 = (a0[0] + D[0] * u, a0[1] + D[1] * u)
        q1 = (a0[0] + D[0] * (u + 6.0), a0[1] + D[1] * (u + 6.0))
        r0 = (q0[0] - N[0] * thick, q0[1] - N[1] * thick)
        r1 = (q1[0] - N[0] * thick, q1[1] - N[1] * thick)
        p.face((q1[0] - N[0] * thick, q1[1] - N[1] * thick, 0.0), N, thick, Hw, Hw + 8, masonry(ST[3], ST[4], 3, 9, R2, 83 + k))
        p.face((q0[0], q0[1], 0.0), D, 6.0, Hw, Hw + 8, masonry(ST[2], ST[3], 3, 6, R2 * 2, 85 + k))
        p.top([q0, q1, r1, r0], Hw + 8, lambda gx, gy: ST[1])
        u += 11.0
        k += 1
    # A tower at each end, a little forward of the wall.
    for e, end in enumerate((a0, a1)):
        W, Ht = 26, 84
        x0, y0 = int(round(end[0] - W * 0.5)), int(round(end[1] - W * 0.5))
        box(p, x0, y0, W, W, 0.0, Ht, masonry(ST[2], ST[3], 5, 8, 1.0, 91 + e), lambda gx, gy: ST[1])
        p.face((x0, y0 + W, 0.0), (1.0, 0.0), W, 0.0, 1.0, lambda u, v: OCC)
        slit(p, int(x0 + W * 0.5) - 1, int(y0 + W), 46, 56)
        crenellated_top(p, x0, y0, W, W, Ht)
    return p.finish()


# --- the sheet -----------------------------------------------------------------------------------

def pieces():
    out = []
    out.append(("parapet_nw", slash(False, "plain")))
    out.append(("parapet_nw_breach", slash(False, "breach")))
    out.append(("parapet_se", slash(True, "plain")))
    out.append(("parapet_se_breach", slash(True, "breach")))
    out.append(("parapet_se_banner", slash(True, "banner")))
    out.append(("parapet_ne", backslash(False)))
    out.append(("parapet_sw", backslash(True)))
    out.append(("turret", turret()))
    out.append(("tower_nw", tower(True)))
    out.append(("tower_se", tower(False)))
    out.append(("castle_wall", backslash(False, tall=True)))
    out.append(("gate_shut", gate(False)))
    out.append(("gate_open", gate(True)))
    return out


# Where a piece sorts, if not at its anchor: world px above it (the gate sorts at its upper-left end, so
# everything in the yard in front of any part of it is drawn in front of it).
SORT_DY = {"gate_shut": -int(STEP * 3 * SCALE), "gate_open": -int(STEP * 3 * SCALE)}


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    made = pieces()
    ok = True
    for name, (c, ax, ay) in made:
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in %s: %s" % (name, bad[:4]))
    # Shelf-pack in rows, a pixel of clear between pieces (and the atlas regions never bleed).
    width = 512
    x = y = row_h = 0
    places = []
    for name, (c, ax, ay) in made:
        w, h = c.w * SCALE, c.h * SCALE
        if x + w > width:
            x = 0
            y += row_h + 2
            row_h = 0
        places.append((name, c, ax, ay, x, y))
        x += w + 2
        row_h = max(row_h, h)
    height = y + row_h
    sheet = raster.Canvas(width, height, None)
    entries = []
    for name, c, ax, ay, px, py in places:
        big = c.scaled(SCALE)
        sheet.img.paste(big, (px, py))
        sheet.px = sheet.img.load()
        entries.append('    "%s": {"x": %d, "y": %d, "w": %d, "h": %d, "ax": %d, "ay": %d, "sort_dy": %d}'
                       % (name, px, py, c.w * SCALE, c.h * SCALE, ax * SCALE, ay * SCALE, SORT_DY.get(name, 0)))
        print("  bridge/%-18s %3dx%-3d on screen, anchor (%d, %d)" % (name, c.w * SCALE, c.h * SCALE, ax * SCALE, ay * SCALE))
    sheet.save(os.path.join(OUT, "bridge_sheet.png"))
    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/props_bridge.py - the Last Bridge\'s parapets, turrets, '
                'gatehouse towers and castle wall on one sheet. x, y, w, h: the region in pixels of the sheet; ax, ay: '
                'the pixel of it that stands on the point BridgeFurniture places it at; sort_dy: where it sorts, '
                'in world px from that point.",\n'
                '  "sheet": "bridge_sheet.png",\n'
                '  "pieces": {\n%s\n  }\n}\n' % ",\n".join(entries))
    io.open(os.path.join(OUT, "bridge.json"), "w", encoding="utf-8", newline="\n").write(
        manifest if isinstance(manifest, type(u"")) else manifest.decode("utf-8"))
    print("%d pieces on a %dx%d sheet%s" % (len(made), width, height, "" if ok else " - WITH PALETTE ERRORS"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
