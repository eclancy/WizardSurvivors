# -*- coding: utf-8 -*-
"""The Familiar: the wizard's hound, and what it becomes. Run: python tools/art/familiar.py

WHAT IT IS (.ai/spell-roster.md, "Familiar"). A hound the wizard conjures out of his own light, that
runs at his heel and bites what comes close. It is the one figure on screen besides the wizard that
is on his side, so every choice below is about reading as HIS, at phone size, in a crowd that already
holds four kinds of dog: the Dire Wolf, the Bog Hound, the Sled Hound and the Chainhound, every one of
them a dark grey-green body with two points of the cold arcane mark for eyes (foes_forest.py says why
a wolf with warm eyes would read as wildlife). So the familiar is the inverse, point for point:

  - WARM FUR, not deep linen. The `flesh` row - "anything alive" (art-direction.md section 3) - lit
    from the bright end, because the player's cast is the one thing allowed to be bright.
  - THE WIZARD'S HAT. A small pointed hat in his own indigo wool with a gold band and a gold star,
    swept back like his. It is the silhouette-breaking attachment section 4 asks every class for:
    filled solid black, a dog in a pointed hat is still the wizard's dog, and no enemy wears one.
  - WARM EYES. Amber, the player's light - never the violet every taken thing stares with.
  - A GOLD STAR on the collar. The one hot pixel on the body.
  - THE HEAD UP. "A level neck is a dog" (foes_forest.wolf); the wolves stalk head-low, this one
    carries its head high and its tail up.

It is not a mascot (world-and-tone.md, tone rule 1): a lean sighthound, long in the leg and the
muzzle, deep in the chest - built to run things down - with the hat worn the way the wizard wears his.

THE FORMS. Every one is posed from one skeleton per body plan and drawn by the same renderer:

  hound          32 cell   the base familiar
  greathound     48 cell   level 4, Great Hound: heavier, a mantle over the shoulders, bigger jaws
  rimehound      32 cell   level 4, Rimehound (the Ice path): frost-blue fur and a ruff of ice
  cerberus       64 cell   level 8, Cerberus: three heads on one great body, the hat on the middle one
  cerberus-rime  64 cell   the same, after the Ice path
  griffin        64 cell   level 8, Griffin: eagle head, the wizard's indigo wings, a lion's quarters;
                           it flies, so its shadow sits detached below it (section 4's Flyer row)
  griffin-rime   64 cell   the same, after the Ice path

Each has four strips: idle (4), run (6), pounce (6 - the leap and the bite, or the griffin's swoop)
and howl (6 - the flourish on summoning and on a level). The game sets pounce and howl frame by frame
from its own clock (scripts/Familiar.cs), so a leap's frames always match where the leap is.

THE RENDERER. A frame is built from parts - ellipsoid blobs, tapered capsules, shaded polygons -
each lit by the contract's one light (key from the upper left), quantised to its material's five
tones with a small ordered dither so a turning form is a few stops rather than bands. Three passes
then do what section 2 and 4b ask: a part in front casts a one-pixel deep shadow onto the part
behind it (every overlapping form separated - the biggest single source of internal edges), the
key-facing silhouette edge gets the bone-white rim, and the occ contact line sits on row cell-1.
Fur gets a lay of darker flecks keyed to the part's own coordinates, so it moves with the body
instead of boiling. Every pixel is checked against the palette (raster.Canvas.audit).
"""
from __future__ import print_function, division

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bonelight as B
import raster
import spriteframes

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "characters")
OUT_REL = "assets/bonelight/characters"
FRAMES_DIR = os.path.join(ROOT, "scenes", "resources")
PREVIEW_DIR = os.path.join(HERE, "_familiar")

M = B.MATERIALS
E = B.ELEMENTS
RIM = B.RIM
OCC = B.OCC
BAYER = raster.BAYER8

# The key light: upper left, tipped toward the viewer so a form's middle is lit, not its edge.
_L = (-1.0, -1.0, 1.25)
_LN = math.sqrt(sum(c * c for c in _L))
LIGHT = (_L[0] / _LN, _L[1] / _LN, _L[2] / _LN)

# --- materials (five tones each, hi..deep) -----------------------------------------------------
FAWN = M["flesh"]
WOOL = M["wool"]
GOLD = M["gold"]
FROST = M["azure"]
EMBER = M["ember"]
BONE = M["skin"]
STONE = M["stone"]
AMBER = M["amber"]

ARC = E["arcane"]
ICE = E["ice"]
LIGHT_E = E["light"]


def hsh(x, y, salt=0):
    """A stable hash of a pixel in a part's own coordinates: fur that moves with the body."""
    h = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return ((h ^ (h >> 16)) & 0xffff) / 65535.0


def rot(px, py, a):
    c, s = math.cos(a), math.sin(a)
    return (px * c - py * s, px * s + py * c)


def ik(root, target, a, b, bend):
    """Two-bone IK: the joint between root and target for bones a and b, bent to `bend` (+1/-1)."""
    dx, dy = target[0] - root[0], target[1] - root[1]
    d = math.sqrt(dx * dx + dy * dy)
    d = max(1e-3, min(d, a + b - 1e-3))
    # angle at the root between the root-target line and the first bone
    cosang = (a * a + d * d - b * b) / (2 * a * d)
    ang = math.acos(max(-1.0, min(1.0, cosang)))
    base = math.atan2(dy, dx)
    j = base + bend * ang
    return (root[0] + math.cos(j) * a, root[1] + math.sin(j) * a)


class Fig(object):
    """One frame under construction: parts painted back to front, then lit, separated and rimmed."""

    def __init__(self, cell):
        self.cell = cell
        self.cells = {}
        self.part = 0
        self.shadow = set()

    # --- painting ------------------------------------------------------------------------------
    def _put(self, x, y, ramp, tone, lx, ly, kind, far=False):
        if 0 <= x < self.cell and 0 <= y < self.cell:
            self.cells[(x, y)] = (ramp, tone, self.part, lx, ly, "far" if far and kind != "glow" else kind)

    @staticmethod
    def tone_of(inten, bias, x, y, dither=0.18):
        v = inten - bias * 0.32 + (BAYER[y % 8][x % 8] / 64.0 - 0.5) * dither
        if v > 0.90:
            return 0
        if v > 0.68:
            return 1
        if v > 0.34:
            return 2
        if v > -0.06:
            return 3
        return 4

    def blob(self, cx, cy, rx, ry, ramp, bias=0.0, kind="body", angle=0.0):
        """An ellipsoid seen side on: lit by its own normal."""
        self.part += 1
        r = max(rx, ry) + 1
        for y in range(int(math.floor(cy - r)), int(math.ceil(cy + r)) + 1):
            for x in range(int(math.floor(cx - r)), int(math.ceil(cx + r)) + 1):
                dx, dy = rot(x + 0.5 - cx, y + 0.5 - cy, -angle)
                nx, ny = dx / rx, dy / ry
                d2 = nx * nx + ny * ny
                if d2 > 1.0:
                    continue
                nz = math.sqrt(max(0.0, 1.0 - d2))
                wx, wy = rot(nx, ny, angle)
                inten = wx * LIGHT[0] + wy * LIGHT[1] + nz * LIGHT[2]
                self._put(x, y, ramp, self.tone_of(inten, bias, x, y), int(round(dx)), int(round(dy)), kind, bias >= 0.6)

    def limb(self, pts, radii, ramp, bias=0.0, kind="body"):
        """A tapered capsule through `pts`: legs, necks, tails. Lit across its own width."""
        self.part += 1
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        rmax = max(radii) + 1
        ax0, ay0 = pts[0]
        for y in range(int(math.floor(min(ys) - rmax)), int(math.ceil(max(ys) + rmax)) + 1):
            for x in range(int(math.floor(min(xs) - rmax)), int(math.ceil(max(xs) + rmax)) + 1):
                px, py = x + 0.5, y + 0.5
                best = None
                for i in range(len(pts) - 1):
                    (x0, y0), (x1, y1) = pts[i], pts[i + 1]
                    vx, vy = x1 - x0, y1 - y0
                    ll = vx * vx + vy * vy
                    t = 0.0 if ll < 1e-6 else max(0.0, min(1.0, ((px - x0) * vx + (py - y0) * vy) / ll))
                    cxp, cyp = x0 + vx * t, y0 + vy * t
                    rr = radii[i] + (radii[i + 1] - radii[i]) * t
                    ddx, ddy = px - cxp, py - cyp
                    dist = math.sqrt(ddx * ddx + ddy * ddy)
                    if dist <= rr + 0.15 and (best is None or dist / max(rr, 0.3) < best[0]):
                        best = (dist / max(rr, 0.3), ddx, ddy, dist, i, t)
                if best is None:
                    continue
                q, ddx, ddy, dist, i, t = best
                q = min(1.0, q)
                if dist > 1e-4:
                    nx, ny = ddx / dist * q, ddy / dist * q
                else:
                    nx, ny = 0.0, 0.0
                nz = math.sqrt(max(0.0, 1.0 - q * q))
                inten = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
                self._put(x, y, ramp, self.tone_of(inten, bias, x, y), int(round(px - ax0)), int(round(py - ay0)), kind, bias >= 0.6)

    def poly(self, pts, ramp, bias=0.0, kind="cloth", lean=(0.0, -0.35)):
        """A flat-ish form (a hat cone, a wing, a mantle): lit across each row like a cylinder."""
        self.part += 1
        ys = [p[1] for p in pts]
        n = len(pts)
        ax0, ay0 = pts[0]
        for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
            yc = y + 0.5
            xints = []
            for i in range(n):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
                if y0 == y1:
                    continue
                if min(y0, y1) <= yc < max(y0, y1):
                    xints.append(x0 + (yc - y0) * (x1 - x0) / (y1 - y0))
            xints.sort()
            for k in range(0, len(xints) - 1, 2):
                xa, xb = xints[k], xints[k + 1]
                w = max(1e-3, xb - xa)
                for x in range(int(math.floor(xa)), int(math.ceil(xb))):
                    xc = x + 0.5
                    if xc < xa - 0.15 or xc > xb + 0.15:
                        continue
                    u = (xc - xa) / w * 2.0 - 1.0
                    nx = u * 0.85 + lean[0]
                    ny = lean[1]
                    nz = math.sqrt(max(0.05, 1.0 - min(0.95, nx * nx + ny * ny)))
                    inten = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
                    self._put(x, y, ramp, self.tone_of(inten, bias, x, y), int(round(xc - ax0)), int(round(yc - ay0)), kind, bias >= 0.6)

    def dot(self, x, y, colour, kind="detail"):
        """One unlit pixel: an eye, a nose, a star. Never separated, never rimmed."""
        self.part += 1
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.cell and 0 <= y < self.cell:
            self.cells[(x, y)] = (None, colour, self.part, 0, 0, kind)

    def contact(self, x0, x1, row=None):
        row = self.cell - 1 if row is None else row
        for x in range(int(round(x0)), int(round(x1)) + 1):
            if 0 <= x < self.cell:
                self.shadow.add((x, row))

    # --- the three passes ----------------------------------------------------------------------
    def render(self):
        c = raster.Canvas(self.cell, self.cell)
        cells = self.cells
        out = {}
        for (x, y), (ramp, tone, part, lx, ly, kind) in cells.items():
            if ramp is None:
                out[(x, y)] = tone
                continue
            t = tone
            if kind in ("fur", "far") and t in (1, 2):
                # The lay of the coat: short broken strokes running back and down the body.
                if (lx * 2 + ly * 3) % 7 == 0 and hsh(lx, ly, 3) < 0.7:
                    t += 1
            elif kind == "cloth" and t in (1, 2, 3):
                # A fold every few columns, a dark stop beside a light one (section 4b).
                if (lx + ly // 3) % 5 == 0:
                    t = min(4, t + 1)
            # A form in front casts a pixel of shadow onto the form behind it, down and right of
            # its edge - the key is upper left. This is every overlapping form separated.
            if kind not in ("detail", "glow"):
                for dx, dy in ((-1, 0), (0, -1)):
                    n = cells.get((x + dx, y + dy))
                    if n is not None and n[2] > part and n[5] not in ("detail", "glow"):
                        t = 4 if t >= 2 else 3
                        break
            out[(x, y)] = ramp[min(4, max(0, t))]
        # The rim: bone-white on the key-facing silhouette edge, the one place light touches.
        for (x, y), (ramp, tone, part, lx, ly, kind) in cells.items():
            # Only the near side, and only where the form faces the key: a rim down the back edge of
            # every leg was a white comb, and the far legs are in the body's own shadow.
            if ramp is None or kind in ("glow", "norim", "far") or tone > 2:
                continue
            if (x - 1, y) not in cells or ((x, y - 1) not in cells and (x - 1, y - 1) not in cells):
                if (x, y + 1) in cells or (x + 1, y) in cells:
                    out[(x, y)] = RIM
        for p in self.shadow:
            if p not in out:
                out[p] = OCC
        for (x, y), col in out.items():
            c.set(x, y, col)
        return c


# =============================================================================================
# THE HOUND. Designed on the 32 cell, facing right, paws on row 30. `s` scales it onto a larger
# cell (the Great Hound) about the ground line.
# =============================================================================================

class Skin(object):
    """What a hound is made of, so one skeleton draws the fawn hound and the frost one."""

    def __init__(self, fur, eye=(AMBER[0], AMBER[1]), frost=False, mantle=False, heavy=0.0, heads=None,
                 head_scale=1.0, leap=1.0):
        self.fur = fur
        self.eye = eye
        self.frost = frost
        self.mantle = mantle
        self.heavy = heavy
        # (neck dx, neck dy, head turn, bias, wears the hat), far to near. None: one head.
        self.heads = heads
        self.head_scale = head_scale
        # How high it leaves the ground on a pounce: a heavy beast leaps lower, and keeps its hat in the cell.
        self.leap = leap


HOUND = Skin(FAWN)
RIME = Skin(FROST, frost=True)
GREAT = Skin(FAWN, mantle=True, heavy=1.0)
# Three heads on one neck root: the far one high and back, the near one low and forward. The hat is
# on the middle head, which is the one the eye goes to.
_THREE = [(-0.6, -7.6, -0.3, 0.35, True), (2.6, -1.2, 0.05, 0.15, False), (4.0, 5.0, 0.42, -0.05, False)]
CERB = Skin(FAWN, mantle=True, heavy=1.4, heads=_THREE, head_scale=0.82, leap=0.45)
CERB_RIME = Skin(FROST, frost=True, mantle=True, heavy=1.4, heads=_THREE, head_scale=0.82, leap=0.45)


def _pose(**kw):
    p = dict(swing=(0.0, 0.0, 0.0, 0.0), lift=(0.0, 0.0, 0.0, 0.0), bob=0.0, stretch=0.0,
             head=(0.0, 0.0, 0.0), jaw=0.0, tail=0.0, hat=0.0, ear=0.0, sit=0.0, breathe=0.0,
             air=0.0, crouch=0.0, breath=0)
    p.update(kw)
    return p


def hound(cell, skin, p, s=1.0):
    """One hound frame. p: a _pose. Legs are (front near, front far, hind near, hind far)."""
    g = Fig(cell)
    G = 30.0
    gy = cell - 2.0
    ox = (cell - 32.0 * s) / 2.0

    def T(x, y):
        return (x * s + ox, (y - G) * s + gy)

    def R(r):
        return r * s

    fur = skin.fur
    heavy = skin.heavy
    air = p["air"] * skin.leap
    rise = air + p["bob"] - p["crouch"]
    sit = p["sit"]
    st = p["stretch"]
    # The two masses. A sitting hound drops its haunch onto its hocks and lifts its chest.
    hx, hy = 9.6 - st * 0.5 + sit * 1.0, 19.2 - rise + sit * 4.2
    cx, cy = 20.2 + st * 0.5, 18.8 - rise - sit * 1.6
    sw = p["swing"]
    li = p["lift"]

    def front_leg(k, bias):
        shoulder = (cx + 0.6, cy + 1.6)
        paw = (shoulder[0] + 0.8 + sw[k] * 4.2, G - li[k] - max(0.0, air - 2.5) * 0.55)
        if sit:
            paw = (shoulder[0] + 1.6, G)
        elbow = ik(shoulder, paw, 5.2 + heavy * 0.3, 5.6, -1)
        pw = T(*paw)
        g.limb([T(*shoulder), T(*elbow), pw], [R(1.9 + heavy * 0.5), R(1.3 + heavy * 0.3), R(1.05 + heavy * 0.2)], fur, bias, "fur")
        g.blob(pw[0] + R(0.7), pw[1] + R(0.1), R(1.5 + heavy * 0.2), R(0.85), fur, bias + 0.4, "body")
        return pw

    def hind_leg(k, bias):
        hip = (hx - 0.4, hy + 1.6)
        paw = (hip[0] - 0.4 + sw[k] * 4.2, G - li[k] - max(0.0, air - 2.5) * 0.55)
        if sit:
            paw = (hip[0] + 3.4, G)
        hock = (paw[0] - 1.7, paw[1] - 2.7)
        if sit:
            hock = (hip[0] - 1.2, G - 0.9)
        knee = ik(hip, hock, 4.4 + heavy * 0.4, 4.0, 1)
        pw = T(*paw)
        g.limb([T(*hip), T(*knee)], [R(2.6 + heavy * 0.6), R(1.5 + heavy * 0.3)], fur, bias, "fur")
        g.limb([T(*knee), T(*hock), pw], [R(1.4 + heavy * 0.3), R(1.0 + heavy * 0.2), R(1.0)], fur, bias + 0.15, "fur")
        g.blob(pw[0] + R(0.7), pw[1] + R(0.1), R(1.5 + heavy * 0.2), R(0.85), fur, bias + 0.4, "body")
        return pw

    paws = []
    # FAR SIDE first, a stop darker: the legs on the other side of the body, and the tail.
    tail_a = p["tail"]
    base = (hx - 3.6, hy - 1.6)
    tp = [base]
    for i, (dx, dy) in enumerate(((-2.2, -2.0), (-1.4, -2.8), (-0.2, -2.6))):
        a = tail_a * (0.5 + i * 0.35)
        rx, ry = rot(dx, dy, a)
        tp.append((tp[-1][0] + rx, tp[-1][1] + ry))
    g.limb([T(*q) for q in tp], [R(1.4 + heavy * 0.3), R(1.1), R(0.8), R(0.5)], fur, 0.25, "fur")
    paws.append(hind_leg(3, 1.25))
    paws.append(front_leg(1, 1.25))

    # THE BODY: haunch, barrel and chest, the waist tucked up under a level back.
    breathe = p["breathe"]
    g.blob(*T(hx, hy), rx=R(4.6 + heavy * 0.8), ry=R(4.2 + heavy * 0.6), ramp=fur, bias=0.0, kind="fur")
    mid = ((hx + cx) / 2.0, (hy + cy) / 2.0 - 1.0)
    g.limb([T(hx + 1.0, hy - 0.6), T(*mid), T(cx - 1.0, cy - 0.4)],
           [R(3.6 + heavy * 0.7), R(2.7 + heavy * 0.8), R(3.9 + heavy * 0.7)], fur, 0.0, "fur")
    g.blob(*T(cx, cy), rx=R(5.0 + heavy * 0.9), ry=R(4.8 + breathe * 0.3 + heavy * 0.8), ramp=fur, bias=-0.1, kind="fur")

    # NEAR SIDE legs.
    paws.append(hind_leg(2, 0.0))

    # THE NECK goes UP off the chest - the wolves' goes down - and the head is carried high.
    # Cerberus carries three: the far one first, a stop darker, the hat on the middle one.
    hdx, hdy, ha = p["head"]
    heads = skin.heads or [(0.0, 0.0, 0.0, 0.0, True)]
    hc = None
    for i, (ndx, ndy, nda, nbias, hat) in enumerate(heads):
        if i == len(heads) - 1:
            if skin.mantle:
                _mantle(g, T, R, hx, hy, cx, cy, p)
            if skin.frost:
                _frost_ruff(g, T, R, hx, hy, cx, cy, (cx + 5.4 + hdx, cy - 8.6 + hdy))
        spread = p.get("spread", 0.0)
        neck0 = (cx + 2.2 + ndx * 0.3, cy - 2.6 + ndy * 0.25)
        hci = (cx + 5.4 + hdx + ndx * (1.0 + spread * 0.25), cy - 8.6 + hdy + ndy * (1.0 + spread * 0.2))
        g.limb([T(*neck0), T((neck0[0] + hci[0]) / 2.0 + 0.4, (neck0[1] + hci[1]) / 2.0), T(hci[0] - 0.6, hci[1] + 0.6)],
               [R(3.0 + heavy * 0.5), R(2.4 + heavy * 0.35), R(2.1 + heavy * 0.25)], fur, nbias - 0.05, "fur")
        # THE COLLAR at the base of each neck, with the gold star hanging from it.
        col0 = (neck0[0] + 0.2 + (hci[0] - neck0[0]) * 0.18, neck0[1] - 0.6 + (hci[1] - neck0[1]) * 0.18)
        col1 = (col0[0] + 1.8, col0[1] + 0.8)
        g.limb([T(*col0), T(*col1)], [R(0.9), R(0.8)], GOLD, nbias - 0.2, "body")
        star = T(col1[0] + 0.1, col1[1] + 1.5)
        g.dot(star[0], star[1], GOLD[0])
        g.dot(star[0], star[1] + 1, LIGHT_E[0] if not skin.frost else ICE[0])
        _head(g, T, R, hci, ha + nda * (1.0 + spread * 0.4), p, skin, hat=hat, bias=nbias)
        if hat:
            hc = hci
    paws.append(front_leg(0, 0.0))

    # THE CONTACT LINE. It stays on the ground when the hound leaves it, and shrinks.
    xs = [q[0] for q in paws]
    lo, hi = min(xs) - R(1.0), max(xs) + R(2.0)
    if air > 0.5:
        mid_x = (lo + hi) / 2.0
        half = (hi - lo) / 2.0 * max(0.35, 1.0 - air * 0.09)
        lo, hi = mid_x - half, mid_x + half
    g.contact(lo, hi)
    return g


def _head(g, T, R, hc, ha, p, skin, hat=True, bias=0.0):
    """Skull, long muzzle, jaw, ear, eye - and the hat. Rotated about the skull by `ha`."""
    fur = skin.fur
    jaw = p["jaw"]
    b = bias
    hs = skin.head_scale
    R0 = R

    def R(r):
        return R0(r * hs)

    def H(dx, dy):
        rx, ry = rot(dx * hs, dy * hs, ha)
        return T(hc[0] + rx, hc[1] + ry)

    # The ear hangs back under the brim, on the far side of the skull from the light.
    ear_a = p["ear"]
    e0 = H(-1.0, -1.6)
    ex, ey = rot(-2.0, 2.6, ear_a)
    e1 = H(-1.0 + ex, -1.6 + ey)
    g.limb([e0, e1], [R(1.25), R(0.85)], fur, 0.8 + b, "fur")
    # The lower jaw first, so the muzzle sits over it; it swings down to open.
    j0 = (0.6, 1.4)
    jx, jy = rot(3.6, 0.6, jaw * 0.75)
    g.limb([H(*j0), H(j0[0] + jx, j0[1] + jy)], [R(1.1), R(0.7)], fur, 0.5 + b, "fur")
    if jaw > 0.25:
        # The inside of the open mouth: teeth along both jaws, the tongue between.
        tx, ty = rot(2.6, 0.5, jaw * 0.4)
        g.dot(*H(j0[0] + tx, j0[1] + ty - 0.2), colour=EMBER[1])
        g.dot(*H(3.2, 1.1), colour=BONE[0])
        g.dot(*H(j0[0] + jx - 0.4, j0[1] + jy - 0.6), colour=BONE[1])
    g.blob(*H(0.0, 0.0), rx=R(3.0), ry=R(2.6), ramp=fur, bias=b - 0.1, kind="fur", angle=ha)
    g.limb([H(1.6, -0.2), H(3.6, 0.3), H(5.0, 0.9)], [R(1.85), R(1.5), R(1.05)], fur, b, "fur")
    g.dot(*H(5.4, 0.7), colour=OCC)
    # The eye: a warm point in a dark socket, under a brow. Amber is the player's light; the
    # enemies' eyes are the cold mark, and this is the one place the difference is spelled out.
    g.dot(*H(0.9, -1.4), colour=fur[4])
    g.dot(*H(1.8, -1.3), colour=fur[3])
    g.dot(*H(0.9, -0.5), colour=OCC)
    g.dot(*H(1.7, -0.5), colour=skin.eye[1])
    g.dot(*H(1.4, 0.4), colour=fur[4])
    if p.get("breath") and skin.frost:
        for i, (dx, dy) in enumerate(((6.6, 0.4), (7.6, -0.4), (8.4, 0.6))):
            g.dot(*H(dx, dy), colour=ICE[i % 2], kind="glow")
    if hat:
        _hat(g, H, R, p)


def _hat(g, H, R, p):
    """The wizard's own hat, small: indigo wool, a gold band, a gold star, the tip swept back."""
    tilt = p["hat"]

    def Hh(dx, dy):
        rx, ry = rot(dx, dy, tilt)
        return H(rx - 0.4, ry - 2.2)

    flap = math.sin(p.get("flap", 0.0)) * 0.6
    cone = [Hh(-2.6, 0.2), Hh(2.4, 0.2), Hh(1.0, -3.4), Hh(-0.6 - flap, -6.4), Hh(-2.6 - flap * 1.5, -7.6),
            Hh(-3.4 - flap * 1.6, -6.6), Hh(-1.9, -4.2)]
    g.poly(cone, WOOL, 0.35, "cloth", lean=(0.0, -0.35))
    # The brim, wider than the cone and flat.
    b0 = Hh(-3.6, 0.6)
    b1 = Hh(3.3, 0.6)
    g.limb([b0, b1], [R(0.8), R(0.8)], WOOL, 0.25, "body")
    # The band and the star.
    g.limb([Hh(-2.2, -0.5), Hh(2.0, -0.5)], [R(0.55), R(0.55)], GOLD, -0.4, "norim")
    sx, sy = Hh(-0.4, -2.4)
    g.dot(sx, sy, GOLD[0])
    g.dot(sx + 1, sy, LIGHT_E[1])
    g.dot(sx, sy - 1, GOLD[1])


def _mantle(g, T, R, hx, hy, cx, cy, p):
    """The Great Hound's mantle: indigo wool over the shoulders, gold at the hem, flaring back."""
    flare = p.get("flare", 0.0)
    pts = [T(cx + 2.6, cy - 4.4), T(cx + 3.4, cy - 1.0), T(cx + 0.4, cy + 2.0), T(hx + 3.0, hy + 0.8 + flare * 0.5),
           T(hx - 1.6 - flare, hy - 1.2 - flare * 0.6), T(hx + 1.2, hy - 4.0), T(cx - 1.4, cy - 5.0)]
    g.poly(pts, WOOL, 0.15, "cloth", lean=(-0.1, -0.5))
    g.limb([T(cx + 0.4, cy + 2.0), T(hx + 3.0, hy + 0.8 + flare * 0.5), T(hx - 1.6 - flare, hy - 1.2 - flare * 0.6)],
           [R(0.55), R(0.55), R(0.5)], GOLD, -0.2, "norim")


def _frost_ruff(g, T, R, hx, hy, cx, cy, hc):
    """The Rimehound's ruff: shards of ice standing up along the neck and back."""
    spikes = [(cx + 2.0, cy - 3.6, -0.5, 3.2), (cx - 0.2, cy - 4.4, -0.8, 2.8), (cx - 2.8, cy - 4.2, -1.0, 2.2),
              (hx + 2.4, hy - 4.0, -1.1, 1.8), (hc[0] - 2.6, hc[1] + 0.4, -0.3, 2.4)]
    for (bx, by, lean, ln) in spikes:
        tip = (bx + lean, by - ln)
        g.poly([T(bx - 0.9, by), T(bx + 0.9, by), T(*tip)], [ICE[0], ICE[1], ICE[2], ICE[3], FROST[3]], -0.4, "body",
               lean=(-0.2, -0.6))


# --- the four animations ------------------------------------------------------------------------

def gallop(k, n=6):
    """The rotary gallop: fronts and hinds half a cycle apart, each pair a little offset."""
    ph = 2 * math.pi * k / n
    sw, li = [], []
    for off in (0.0, 0.75, math.pi, math.pi + 0.75):
        a = ph + off
        sw.append(math.sin(a) * 1.15)
        li.append(max(0.0, math.cos(a)) * 2.6)
    return _pose(swing=tuple(sw), lift=tuple(li), stretch=math.sin(ph) * 1.6,
                 bob=max(0.0, -math.sin(ph)) * 1.2, head=(1.2, 1.2, 0.12 + math.sin(ph) * 0.05),
                 tail=-0.9 + math.sin(ph) * 0.15, hat=-0.35, ear=-0.6, flap=ph, flare=1.0 + math.sin(ph) * 0.5)


HOUND_ANIMS = [
    ("idle", [_pose(tail=0.0, breathe=0.0, hat=-0.08),
              _pose(tail=0.28, breathe=0.6, hat=-0.08, flap=0.6),
              _pose(tail=0.5, breathe=1.0, hat=-0.06, ear=0.2, flap=1.2),
              _pose(tail=0.28, breathe=0.5, hat=-0.08, flap=0.6)], True, 6.0),
    ("run", [gallop(k) for k in range(6)], True, 12.0),
    ("pounce", [
        _pose(crouch=2.2, swing=(0.5, 0.4, 0.8, 0.7), head=(0.8, 2.4, 0.15), tail=-0.6, hat=-0.3, ear=-0.4),
        _pose(air=2.5, stretch=2.0, swing=(1.3, 1.1, -1.2, -1.0), lift=(2.5, 2.0, 1.0, 0.6), head=(1.6, 1.0, 0.1),
              jaw=0.4, tail=-1.1, hat=-0.5, ear=-0.8, flap=1.0, flare=1.6, spread=0.6),
        _pose(air=6.0, stretch=2.6, swing=(1.5, 1.3, -1.3, -1.2), lift=(1.0, 0.8, 0.4, 0.2), head=(2.2, 1.0, 0.18),
              jaw=1.0, tail=-1.3, hat=-0.6, ear=-1.0, flap=2.0, flare=2.0, spread=1.0),
        _pose(air=3.0, stretch=2.0, swing=(1.4, 1.2, -1.0, -0.9), lift=(0.4, 0.2, 0.6, 0.4), head=(2.0, 2.2, 0.3),
              jaw=1.0, tail=-1.0, hat=-0.45, ear=-0.8, flap=3.0, flare=1.6, spread=1.3),
        _pose(crouch=1.2, stretch=-0.6, swing=(0.9, 0.7, 0.2, 0.0), head=(1.8, 3.4, 0.42), jaw=0.0, tail=-0.4,
              hat=0.15, ear=0.3, flap=4.0, flare=0.6, breath=1, spread=1.5),
        _pose(crouch=0.4, swing=(0.6, 0.4, 0.0, -0.1), head=(1.2, 2.2, 0.05), jaw=0.15, tail=-0.2, hat=-0.2,
              ear=-0.3, flap=5.0, breath=1, spread=0.6),
    ], False, 14.0),
    ("howl", [
        _pose(tail=0.2, hat=-0.08),
        _pose(sit=0.6, tail=0.4, head=(-0.2, -0.6, -0.1), hat=-0.12),
        _pose(sit=1.0, tail=0.5, head=(-0.6, -1.4, -0.5), jaw=0.2, hat=-0.3, ear=-0.3),
        _pose(sit=1.0, tail=0.6, head=(-1.0, -2.0, -0.95), jaw=0.6, hat=-0.55, ear=-0.6, flap=1.0, breath=1),
        _pose(sit=1.0, tail=0.7, head=(-1.0, -2.2, -1.0), jaw=0.8, hat=-0.62, ear=-0.7, flap=2.0, breath=1),
        _pose(sit=0.6, tail=0.4, head=(-0.4, -1.0, -0.4), jaw=0.2, hat=-0.25, ear=-0.2),
    ], False, 8.0),
]


# =============================================================================================
# THE GRIFFIN. Level 8: the familiar takes wing. An eagle's head and forequarters in gold, a lion's
# haunch in the hound's own fawn, and the wizard's indigo for wings, edged in gold - the hat stays
# on. Designed on the 64 cell, facing right. It never touches the ground, so its contact shadow
# sits detached on the bottom rows and shrinks as it climbs (section 4's Flyer row).
# =============================================================================================

GRIFF = dict(fur=FAWN, feather=GOLD, wing=WOOL, frost=False)
GRIFF_RIME = dict(fur=FROST, feather=[ICE[0], ICE[1], FROST[1], FROST[2], FROST[3]], wing=FROST, frost=True)


def _gpose(**kw):
    p = dict(wing=-2.0, wing_far=None, alt=0.0, tilt=0.0, beak=0.0, head=(0.0, 0.0, 0.0), talon=0.0,
             tail=0.0, hat=-0.2, flap=0.0, legs=0.0)
    p.update(kw)
    return p


def _wing(g, root, ang, length, chord, ramp, bias, edge_bias):
    """A wing seen side on: a gold leading edge, primaries splayed at the tip, coverts at the root."""
    dx, dy = math.cos(ang), math.sin(ang)
    px, py = -dy, dx
    if px > 0:
        px, py = -px, -py
    # The arm: root to wrist, bowed toward the leading side. The hand carries the primaries.
    wrist = (root[0] + dx * length * 0.46 - px * chord * 0.16, root[1] + dy * length * 0.46 - py * chord * 0.16)
    # The inner wing - coverts and secondaries - with a scalloped trailing edge, three feathers deep.
    inner = [root, wrist]
    for i in range(4):
        t = 0.46 - i * 0.14
        w = chord * (0.95 if i % 2 == 0 else 0.78)
        inner.append((root[0] + dx * length * t + px * w, root[1] + dy * length * t + py * w))
    inner.append((root[0] + px * chord * 0.55, root[1] + py * chord * 0.55))
    g.poly(inner, ramp, bias, "cloth", lean=(-0.15, -0.45))
    # Five primaries, fanned from the hand: the outermost longest and nearest the leading edge.
    for k in range(5):
        f = k / 4.0
        base = (wrist[0] + dx * length * 0.05 * (1 - f), wrist[1] + dy * length * 0.05 * (1 - f))
        a = ang + (0.12 + f * 0.62) * (1 if (px * math.cos(ang + 1.0) + py * math.sin(ang + 1.0)) > (px * math.cos(ang - 1.0) + py * math.sin(ang - 1.0)) else -1)
        ln = length * (0.62 - f * 0.17)
        tip = (base[0] + math.cos(a) * ln, base[1] + math.sin(a) * ln)
        mid = (base[0] + math.cos(a) * ln * 0.55, base[1] + math.sin(a) * ln * 0.55)
        g.limb([base, mid, tip], [2.1, 1.6, 0.5], ramp, bias + 0.15 * (k % 2), "cloth")
    g.limb([root, wrist, (wrist[0] + dx * length * 0.2, wrist[1] + dy * length * 0.2)], [0.9, 0.8, 0.5], GOLD, edge_bias, "norim")


def griffin(look, p):
    cell = 64
    g = Fig(cell)
    fur, feather, wingm = look["fur"], look["feather"], look["wing"]
    alt = p["alt"]
    tilt = p["tilt"]
    pivot = (31.0, 30.0)

    def T(x, y):
        rx, ry = rot(x - pivot[0], y - pivot[1], tilt)
        return (pivot[0] + rx, pivot[1] + ry - alt)

    wf = p["wing_far"] if p["wing_far"] is not None else p["wing"] + 0.18
    # Far wing, far legs, tail: behind the body and a stop darker.
    _wing(g, T(34.0, 26.0), wf + tilt, 26.0, 10.0, wingm, 1.0, 0.8)
    ta = p["tail"]
    tp = [T(15.0, 31.0)]
    for i, (dx, dy) in enumerate(((-4.0, 1.5), (-4.0, 1.0), (-3.0, -1.0), (-1.5, -2.5))):
        rx, ry = rot(dx, dy, ta * (0.4 + i * 0.25))
        tp.append((tp[-1][0] + rx, tp[-1][1] + ry))
    g.limb(tp, [1.6, 1.3, 1.1, 0.9, 0.8], fur, 0.3, "fur")
    g.blob(tp[-1][0], tp[-1][1] - 0.5, 2.2, 1.8, feather, 0.3, "fur")
    legs = p["legs"]
    talon = p["talon"]

    def hind(bias, off):
        hip = T(18.0 + off, 35.0)
        knee = T(22.0 + off - legs * 2.0, 39.5 + legs)
        paw = T(16.5 + off - legs * 3.0, 42.0 + legs * 0.5)
        g.limb([hip, knee, paw], [3.0, 1.8, 1.4], fur, bias, "fur")
        g.blob(paw[0] - 0.5, paw[1] + 0.3, 1.9, 1.1, fur, bias + 0.3, "body")

    def fore(bias, off):
        sh = T(36.0 + off, 34.0)
        kn = T(38.5 + off + talon * 3.0, 38.5 - talon * 0.5)
        ft = T(40.5 + off + talon * 6.0, 41.0 - talon * 1.0)
        g.limb([sh, kn, ft], [2.4, 1.4, 1.1], feather, bias, "fur")
        for (ex, ey) in ((1.6, 1.4), (2.4, 0.2), (0.4, 1.9)):
            c = STONE[4] if bias > 0.5 else STONE[3]
            g.dot(ft[0] + ex * (0.6 + talon * 0.5), ft[1] + ey * (0.8 - talon * 0.3), c)
            g.dot(ft[0] + ex * (0.6 + talon * 0.5) + 0.6, ft[1] + ey * (0.8 - talon * 0.3) + 0.8, OCC)

    hind(1.2, 2.0)
    fore(1.2, 2.0)
    # The body: a lion's haunch, the barrel, an eagle's breast.
    g.blob(*T(19.0, 31.5), rx=7.0, ry=6.0, ramp=fur, bias=0.0, kind="fur")
    g.limb([T(22.0, 30.0), T(28.0, 30.5), T(32.0, 30.5)], [5.6, 5.0, 5.8], fur, 0.0, "fur")
    g.blob(*T(35.0, 30.0), rx=7.0, ry=6.6, ramp=feather, bias=-0.05, kind="fur")
    hind(0.0, 0.0)
    fore(0.0, 0.0)
    # Neck and head, eagle: a hooked amber beak, a dark brow over an amber eye.
    hdx, hdy, ha = p["head"]
    hc = (44.0 + hdx, 20.5 + hdy)
    g.limb([T(37.0, 26.0), T(41.0, 23.0), T(hc[0] - 1.0, hc[1] + 1.0)], [5.0, 4.2, 3.6], feather, -0.1, "fur")

    def H(dx, dy):
        rx, ry = rot(dx, dy, ha)
        return T(hc[0] + rx, hc[1] + ry)

    g.blob(*H(0.0, 0.0), rx=4.6, ry=4.0, ramp=feather, bias=-0.15, kind="fur", angle=ha + tilt)
    beak = p["beak"]
    by = beak * 2.2
    g.poly([H(2.6, 1.8 + by * 0.2), H(6.6, 2.2 + by), H(5.6, 3.6 + by), H(3.0, 3.0 + by * 0.6)], AMBER, 0.6, "body")
    g.poly([H(2.6, -1.0), H(6.6, -0.4), H(8.6, 1.0), H(8.4, 3.0), H(7.2, 2.0), H(3.0, 1.8)], AMBER, -0.1, "body")
    g.dot(*H(8.2, 3.2), colour=AMBER[3])
    if beak > 0.3:
        g.dot(*H(5.0, 2.6 + by * 0.7), colour=EMBER[1])
    g.dot(*H(1.2, -2.2), colour=OCC)
    g.dot(*H(2.2, -2.0), colour=OCC)
    g.dot(*H(3.2, -1.6), colour=feather[4])
    g.dot(*H(1.6, -1.0), colour=AMBER[1])
    g.dot(*H(2.4, -1.0), colour=AMBER[0])
    g.dot(*H(1.6, -0.2), colour=OCC)
    # The collar and its star, on the gold of the neck.
    g.limb([T(37.6, 24.6), T(40.6, 26.4)], [1.1, 1.0], WOOL, -0.1, "body")
    g.dot(*T(40.0, 28.4), colour=GOLD[0])
    g.dot(*T(40.0, 29.4), colour=LIGHT_E[0] if not look["frost"] else ICE[0])
    # The near wing, over everything but the head.
    _wing(g, T(32.0, 26.5), p["wing"] + tilt, 29.0, 11.0, wingm, 0.0, -0.2)

    def Hh(dx, dy):
        return H(dx * 1.25 - 0.8, dy * 1.25 - 3.4)

    _hat(g, Hh, lambda r: r * 1.3, dict(hat=p["hat"], flap=p["flap"]))
    if look["frost"] and p["beak"] > 0.3:
        for i, (dx, dy) in enumerate(((9.6, 2.0), (11.0, 1.4), (12.2, 2.6))):
            g.dot(*H(dx, dy), colour=ICE[i % 2], kind="glow")
    # The detached shadow: on the ground, never touching the bird, narrower the higher it is.
    lift = 62.0 - (44.0 - alt)
    half = max(5.0, 14.0 - lift * 0.35)
    g.contact(31.0 - half, 31.0 + half, row=62)
    g.contact(31.0 - half + 2, 31.0 + half - 2, row=63)
    return g


def _wcycle(k, n, lo=-2.05, hi=2.55):
    """A wingbeat: up (pointing up and back), back, down and back, and round again."""
    t = k / float(n)
    return lo + (hi - lo) * (0.5 - 0.5 * math.cos(2 * math.pi * t))


GRIFFIN_ANIMS = [
    ("idle", [_gpose(wing=_wcycle(k, 4), alt=[0.0, -1.0, -2.0, -1.0][k], tail=math.sin(k * 1.57) * 0.3, flap=k)
              for k in range(4)], True, 7.0),
    ("run", [_gpose(wing=_wcycle(k, 6, -1.95, 2.7), alt=[0.0, -1.0, -2.0, -2.0, -1.0, 0.0][k], tilt=0.12,
                    tail=-0.3, hat=-0.45, flap=k, legs=1.0) for k in range(6)], True, 12.0),
    ("pounce", [
        _gpose(wing=-1.95, alt=1.0, tilt=-0.05, hat=-0.3, legs=0.5),
        _gpose(wing=2.95, alt=-4.0, tilt=0.38, hat=-0.6, legs=1.0, tail=-0.5, head=(0.5, 1.0, 0.2)),
        _gpose(wing=3.05, alt=-9.0, tilt=0.45, hat=-0.7, talon=0.6, legs=1.0, tail=-0.6, head=(0.8, 1.5, 0.25), beak=0.6),
        _gpose(wing=-1.7, alt=-10.0, tilt=0.1, hat=-0.4, talon=1.0, beak=1.0, tail=-0.3, head=(1.0, 0.5, 0.1)),
        _gpose(wing=2.4, alt=-6.0, tilt=-0.2, hat=-0.2, talon=0.4, beak=0.4, legs=0.6, head=(0.0, -0.5, -0.15)),
        _gpose(wing=-2.0, alt=-2.0, tilt=-0.08, hat=-0.25, legs=0.6),
    ], False, 14.0),
    ("howl", [
        _gpose(wing=-2.0),
        _gpose(wing=-1.8, alt=-1.0, head=(-0.5, -1.0, -0.2), beak=0.2),
        _gpose(wing=-1.55, alt=-2.0, head=(-1.0, -2.0, -0.45), beak=0.7, hat=-0.45, flap=1),
        _gpose(wing=-1.45, wing_far=-1.1, alt=-2.5, head=(-1.2, -2.4, -0.55), beak=1.0, hat=-0.55, flap=2, tail=0.4),
        _gpose(wing=-1.5, wing_far=-1.15, alt=-2.0, head=(-1.2, -2.2, -0.5), beak=0.8, hat=-0.5, flap=3, tail=0.3),
        _gpose(wing=-1.9, alt=-1.0, head=(-0.4, -0.8, -0.15), beak=0.2),
    ], False, 8.0),
]


FORMS = [
    # name, cell, skin, scale
    ("hound", 32, HOUND, 1.0),
    ("rimehound", 32, RIME, 1.0),
    ("greathound", 48, GREAT, 1.42),
    ("cerberus", 64, CERB, 1.62),
    ("cerberus-rime", 64, CERB_RIME, 1.62),
    ("griffin", 64, GRIFF, 1.0),
    ("griffin-rime", 64, GRIFF_RIME, 1.0),
]


def portrait(skin=HOUND, cell=32):
    """The hound's head at card size, for the spell icon (spell_icons.familiar_icon). Drawn with the
    sprites' own materials, light and passes, but to a portrait's proportions: scaled up from the
    sprite, the long muzzle read as a slab under a hat. A rounder skull, the ear hanging clear of it,
    a black nose, a two-pixel eye and the mouth a little open are what say "dog" at 32 pixels."""
    g = Fig(cell)
    fur = skin.fur
    # Neck and chest, out of the bottom of the frame.
    g.limb([(13.0, 22.0), (10.0, 27.0), (8.0, 33.0)], [4.6, 5.6, 6.6], fur, 0.1, "fur")
    # The ear hangs back, clear of the skull, a stop darker.
    g.limb([(10.0, 13.5), (6.6, 18.0), (5.6, 22.5)], [2.6, 2.4, 1.9], fur, 0.7, "fur")
    # Lower jaw first, a little open, with the tongue in it.
    g.limb([(15.0, 21.0), (20.0, 23.2), (24.0, 23.6)], [2.4, 1.8, 1.3], fur, 0.45, "fur")
    g.blob(13.0, 16.5, 6.4, 5.6, fur, -0.1, "fur")
    g.limb([(16.0, 16.0), (21.0, 17.6), (26.0, 19.2)], [4.0, 3.2, 2.3], fur, 0.0, "fur")
    for (x, y, c) in ((19, 21, EMBER[1]), (20, 21, EMBER[1]), (21, 22, EMBER[2]), (22, 21, BONE[0]), (17, 20, BONE[1])):
        g.dot(x, y, c)
    # Nose, eye, brow.
    for (x, y) in ((26, 18), (27, 18), (26, 19), (27, 19)):
        g.dot(x, y, OCC)
    g.dot(27, 17, fur[2])
    for (x, y) in ((14, 13), (15, 13), (16, 13)):
        g.dot(x, y, fur[4])
    g.dot(15, 14, skin.eye[0])
    g.dot(16, 14, skin.eye[1])
    g.dot(15, 15, skin.eye[1])
    g.dot(16, 15, OCC)
    # The collar and its star.
    g.limb([(6.0, 26.0), (15.0, 27.5)], [1.2, 1.2], GOLD, -0.3, "body")
    g.dot(13, 29, GOLD[0])
    g.dot(14, 29, LIGHT_E[0])
    g.dot(13, 30, GOLD[1])
    # The hat, tipped back, its tip swept down behind.
    g.poly([(5.0, 11.0), (20.0, 11.0), (15.0, 5.0), (10.0, 1.5), (5.0, 1.0), (2.0, 3.0), (6.0, 4.5)], WOOL, 0.3,
           "cloth", lean=(0.0, -0.35))
    g.limb([(3.0, 11.5), (21.0, 11.5)], [1.4, 1.4], WOOL, 0.2, "body")
    g.limb([(6.0, 9.6), (18.0, 9.6)], [0.9, 0.9], GOLD, -0.4, "norim")
    g.dot(11, 6, GOLD[0])
    g.dot(12, 6, LIGHT_E[0])
    g.dot(11, 5, GOLD[1])
    g.dot(10, 6, GOLD[1])
    return g.render()


def frames_for(cell, skin, scale):
    out = []
    if isinstance(skin, dict):
        for anim, poses, loop, speed in GRIFFIN_ANIMS:
            out.append((anim, [griffin(skin, p).render() for p in poses], loop, speed))
        return out
    for anim, poses, loop, speed in HOUND_ANIMS:
        out.append((anim, [hound(cell, skin, p, scale).render() for p in poses], loop, speed))
    return out


def density(canvas):
    w, h = canvas.w, canvas.h
    filled = 0
    edges = 0
    for y in range(h):
        for x in range(w):
            p = canvas.get(x, y)
            if not p[3]:
                continue
            filled += 1
            for dx, dy in ((1, 0), (0, 1)):
                q = canvas.get(x + dx, y + dy)
                if q[3] and q[:3] != p[:3]:
                    edges += 1
    return filled, edges / float(max(1, filled))


def write_strip(frames, path, cell):
    from PIL import Image
    sheet = Image.new("RGBA", (cell * len(frames), cell), (0, 0, 0, 0))
    for i, c in enumerate(frames):
        sheet.alpha_composite(c.img, (i * cell, 0))
    sheet.save(path)


def preview(sets, path, zoom=5):
    from PIL import Image
    bg = (30, 38, 52, 255)
    rows = []
    for name, cell, anims in sets:
        for anim, frames, _l, _s in anims:
            rows.append((name + " " + anim, cell, frames))
    W = max(len(f) * (c * zoom + 6) for _, c, f in rows) + 6
    H = sum(c * zoom + 6 for _, c, _f in rows) + 6
    sheet = Image.new("RGBA", (W, H), bg)
    y = 6
    for name, cell, frames in rows:
        for i, c in enumerate(frames):
            im = c.img.resize((cell * zoom, cell * zoom), Image.NEAREST)
            sheet.alpha_composite(im, (6 + i * (cell * zoom + 6), y))
        y += cell * zoom + 6
    sheet.save(path)


def main(write=True, only=None):
    if not os.path.isdir(PREVIEW_DIR):
        os.makedirs(PREVIEW_DIR)
    sets = []
    bad_total = 0
    for name, cell, skin, scale in FORMS:
        if only and name not in only:
            continue
        anims = frames_for(cell, skin, scale)
        sets.append((name, cell, anims))
        for anim, frames, loop, speed in anims:
            for c in frames:
                bad = c.audit()
                if bad:
                    bad_total += 1
                    print("OFF-CONTRACT COLOURS in %s %s: %s" % (name, anim, bad[:4]))
        filled, ratio = density(anims[0][1][0])
        print("  familiar-%-14s %dx%d  idle %4d px  %.2f edges/px%s"
              % (name, cell, cell, filled, ratio, "" if ratio >= 0.80 else "   SPARSE"))
    preview(sets, os.path.join(PREVIEW_DIR, "familiar-forms.png"))
    preview(sets, os.path.join(PREVIEW_DIR, "familiar-forms-x2.png"), zoom=2)
    if bad_total:
        return 1
    if not write:
        return 0
    for name, cell, anims in sets:
        spec = []
        for anim, frames, loop, speed in anims:
            png = "familiar-%s-%s.png" % (name, anim)
            write_strip(frames, os.path.join(OUT_DIR, png), cell)
            spec.append((anim, png, len(frames), loop, speed))
        sheet = frames_name(name)
        spriteframes.write(os.path.join(FRAMES_DIR, sheet + ".tres"), spec, OUT_REL, cell=cell)
        print("  %-24s %3d frames -> scenes/resources/%s.tres" % ("familiar-" + name, sum(a[2] for a in spec), sheet))
    return 0


def frames_name(form):
    """familiar-cerberus-rime -> FamiliarCerberusRimeFrames: the name scripts/Familiar.cs loads by."""
    return "Familiar" + "".join(part.capitalize() for part in form.split("-")) + "Frames"


if __name__ == "__main__":
    _only = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--only=")]
    sys.exit(main(write="--preview" not in sys.argv, only=_only[0].split(",") if _only else None))
