# -*- coding: utf-8 -*-
"""The Boo: Elim's ghost, redrawn on the Bonelight light model as the Sketchbook's miniboss.

Run: python tools/art/sprite_boo.py               -> tools/art/_boo/ (gitignored): strips + preview
     python tools/art/sprite_boo.py --sheet A.png B.png ...
                                                  -> tools/art/_ghost_sheet.png (gitignored): his
                                                     drawing, every new frame, and the in-game shots
     python tools/art/build.py                    -> the shipping strips in assets/enemies/ and
                                                     scenes/resources/BooEnemyFrames.tres

DESIGNED BY ELIM. The ghost is his drawing - `assets/enemies/booscaryspritesheet.png`, two 17x15
frames, black outline on white - and every frame below is built FROM that file, read at run time,
not traced from it once and then forgotten. Its dome head, its two tall slot eyes, its round
open mouth set a little low, its one arm thrown up in a "boo!", and the way his two frames pump
that arm up and down, are all his. That file is the source and is never edited or deleted.

APPROVED REFINEMENT, 2026-10-20. The kids' art rule in CLAUDE.md is that their drawings are never
restyled, redrawn to the contract or given derived frames. This sprite is the one exception, at
Eric's explicit request ("we can keep the animation frames and actually should tune up the drawing
a bit to fit in with the rest of the art. update the art, and also make it bigger and scarier and
make it a mini boss"). So, and only for this sprite: his drawing is tuned up to sit with the rest
of the game, made bigger and scarier, and its derived animations are kept and refined. The other
kids' sheets are untouched by this and the rule still holds for every one of them.

WHAT "TUNED UP" MEANS HERE - the contract (.ai/art-direction.md) applied to his shapes:

  BIGGER, ON THE BOSS CELL. His 17x15 drawing is grown x4 (68x60) and then smoothed, so the
  stair-steps of a 17-pixel drawing become the curves they stood for - the silhouette is HIS
  silhouette at four times the resolution, not a new one. That is a ~64px figure on the 96x96
  cell at the contract's one render scale, x2: about 128px on screen, 2.2-2.5x a skeleton's 52-58.
  The old sheet was the drawing at x3 on a 32px cell, 45px tall: SMALLER than a skeleton, on a
  miniboss. 96 rather than 48 because the elite cell's ~40px figure lands at 80px, which is a
  head taller than the horde and not the 2x the brief asked for; and 96 is already in section 1's
  table, so no new cell size and no new scale.

  LIT, NOT OUTLINED ALL ROUND. The key comes from the upper left: a 1px bone-white rim (#DCE8FF,
  reserved) on every key-facing edge, occlusion (#05070C) on every edge facing away, and the deep
  tone between. That keeps the clean dark edge his drawing is made of where the light model puts
  shadow anyway, and drops it where the light is - a selective outline, which is how this model
  draws an edge, rather than the black line all round that section 2 forbids.

  DARKER, AND TRANSLUCENT. The body is the `skin` (bone) row, pale at the crown and sinking
  through base and shade to deep at the hem, the way every enemy body comes from the deep half of
  its ramp. The lower third is see-through: ordered dither knocks holes in it that grow toward the
  hem, so the floor shows through the sheet - with hard alpha, because the contract has no partial
  alpha in it. The holes are cut after the edges are lit, so they read as thin cloth rather than as
  a silhouette with bites out of it.

  A COLD INNER LIGHT. A glow in the chest and at the back of the throat, on the `arcane` cyan row.
  It is the colour of its attack - the wail's warning ring is the same cyan (BooEnemy.cs) - which
  is what world-and-tone.md asks of an elite: its one accent belongs to the attack it telegraphs,
  so colour on it is a warning rather than a costume. It flares through the wail.

  HOLLOW EYES. His slot eyes become voids (#05070C) with their top inner corners cut down into a
  brow, each with a burning point low in it from the ARCANE element ramp - the same mark every
  creature the dark wizard took carries. The face is still his: two tall slots and an O.

  A RAGGED HEM. His rounded base is cut off and replaced with five tattered points of uneven
  length that trail behind it (the sprite faces left, so behind is +x) and sway on their own
  phases, so the hem is never a straight edge and never the same two frames running.

  NO FEET, A DETACHED SHADOW. Section 4's flyer class: no ground contact, and the occlusion shadow
  sits apart on the floor below - dithered, and shrinking as the ghost rises. Section 5 wants feet
  on row cell-2; a ghost that hovers is the one figure that must not have them.

THE FOUR ANIMATIONS, refined from the ones derived in 2026-09:

  moving  his two frames, alternating, on a slow float - his own animation, kept
  attack  THE WAIL, timed to the wind-up (6 frames at 6 fps = 1.0 s = BooEnemy.WailWindUpSeconds):
          gather low, rise, rear back with the arm thrown high, tremble, then lunge into a scream
          with the mouth gaping and the inner light at full. The ring lands as the scream does.
  hurt    a flinch away from the hit, the sheet going thinner (more see-through) for a beat and
          the eyes flaring. (The old inverted flash belonged to a two-colour sprite; this one has
          the five tones a flinch needs.)
  death   it comes apart rather than falling over - dissolving from the outside in while drifting
          up, the wail still on its face, and the last thing left is the two burning points.
"""
from __future__ import division

import math
import os
import sys

from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import bonelight as B  # noqa: E402
import matte  # noqa: E402

SOURCE = os.path.join(ROOT, "assets", "enemies", "booscaryspritesheet.png")

CELL = 96
UP = 4            # his drawing, grown this many times before smoothing
SRC_W, SRC_H = 17, 15
MARGIN = 6        # room round the grown drawing so the blur and the stretch do not clip


def _rgba(h):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), 255)


BONE = [_rgba(c) for c in B.MATERIALS["skin"]]       # hi lit base shade deep
CYAN = [_rgba(c) for c in B.MATERIALS["arcane"]]     # the cold inner light, and the wail's colour
MARK = [_rgba(c) for c in B.ELEMENTS["arcane"]]      # core hot mid edge - his mark in the eyes
OCC = _rgba(B.OCC)
RIM = _rgba(B.RIM)
CLEAR = (0, 0, 0, 0)

# Ordered dither. Everything soft on this sprite - the light bands, the inner glow, the see-through
# hem, the shadow and the death - is one of these thresholds, so the noise is a pattern that holds
# still from frame to frame rather than a random speckle that crawls.
_BAYER = [[0, 32, 8, 40, 2, 34, 10, 42], [48, 16, 56, 24, 50, 18, 58, 26],
          [12, 44, 4, 36, 14, 46, 6, 38], [60, 28, 52, 20, 62, 30, 54, 22],
          [3, 35, 11, 43, 1, 33, 9, 41], [51, 19, 59, 27, 49, 17, 57, 25],
          [15, 47, 7, 39, 13, 45, 5, 37], [63, 31, 55, 23, 61, 29, 53, 21]]


def _bayer(x, y):
    return (_BAYER[y & 7][x & 7] + 0.5) / 64.0


# --- reading his drawing ----------------------------------------------------------------------

def _source():
    """His two frames, split into what each pixel IS rather than what colour it is.

    `solid` is the whole silhouette, outline included. The black pixels split two ways: the
    outline (any black run that touches the outside) and the FEATURES (black runs sealed inside
    white) - which on both frames is exactly two eyes and one mouth. Found by connectivity, not
    listed by hand, so the face is wherever he put it.
    """
    im = Image.open(SOURCE).convert("RGBA")
    frames = []
    for i in range(2):
        solid, dark = set(), set()
        for y in range(SRC_H):
            for x in range(SRC_W):
                r, g, b, a = im.getpixel((i * SRC_W + x, y))
                if a >= 128:
                    solid.add((x, y))
                    if r + g + b <= 382:
                        dark.add((x, y))
        outside = set()
        todo = [(x, y) for x in range(-1, SRC_W + 1) for y in (-1, SRC_H)] + \
               [(x, y) for y in range(SRC_H) for x in (-1, SRC_W)]
        while todo:
            p = todo.pop()
            if p in outside or p in solid:
                continue
            if not (-1 <= p[0] <= SRC_W and -1 <= p[1] <= SRC_H):
                continue
            outside.add(p)
            x, y = p
            todo += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
        seen, features = set(), []
        for p in sorted(dark):
            if p in seen:
                continue
            comp, stack = set(), [p]
            while stack:
                q = stack.pop()
                if q in comp or q not in dark:
                    continue
                comp.add(q)
                stack += [(q[0] + 1, q[1]), (q[0] - 1, q[1]), (q[0], q[1] + 1), (q[0], q[1] - 1)]
            seen |= comp
            touches = any((x + dx, y + dy) in outside for (x, y) in comp
                          for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if not touches:
                features.append(comp)
        features.sort(key=lambda c: sum(y for _x, y in c) / len(c))
        if len(features) != 3:
            raise ValueError("frame %d: expected two eyes and a mouth, found %d sealed shapes"
                             % (i, len(features)))
        eyes = sorted(features[:2], key=lambda c: min(x for x, _y in c))
        # The arm is the raised limb on the left, x <= 4, down to the row where it joins the body.
        arm = set(p for p in solid if p[0] <= 4 and p[1] <= 10)
        frames.append({"solid": solid, "arm": arm, "eyes": eyes, "mouth": features[2]})
    return frames


def _centroid(cells):
    n = float(len(cells))
    return (sum(x for x, _y in cells) / n + 0.5, sum(y for _x, y in cells) / n + 0.5)


def _grow(cells, sx=1.0, sy=1.0, about=None, blur=1.7):
    """A set of his pixels, grown x4, smoothed into curves, posed, and returned as cell-space
    offsets from `about` (a point in his 17x15 coordinates).

    Nearest-grow then blur then threshold is what turns a 17-pixel drawing's stair-steps into the
    curves they stood for without moving any edge: a straight run stays put, a corner rounds by
    about two pixels, and a one-pixel gap in his drawing stays a four-pixel gap here.
    """
    w, h = SRC_W * UP + MARGIN * 2, SRC_H * UP + MARGIN * 2
    im = Image.new("L", (w, h), 0)
    px = im.load()
    for (x, y) in cells:
        for yy in range(UP):
            for xx in range(UP):
                px[MARGIN + x * UP + xx, MARGIN + y * UP + yy] = 255
    im = im.filter(ImageFilter.GaussianBlur(blur))
    ax, ay = about if about is not None else (0.0, 0.0)
    ax, ay = MARGIN + ax * UP, MARGIN + ay * UP
    if sx != 1.0 or sy != 1.0:
        im = im.resize((max(1, int(round(w * sx))), max(1, int(round(h * sy)))), Image.BILINEAR)
        ax, ay = ax * sx, ay * sy
    px = im.load()
    out = set()
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            if px[x, y] >= 128:
                out.add((int(math.floor(x - ax)), int(math.floor(y - ay))))
    return out


def _shift(cells, dx, dy):
    return set((x + dx, y + dy) for x, y in cells)


def _clip(cells):
    return set(p for p in cells if 0 <= p[0] < CELL and 0 <= p[1] < CELL)


# --- the pose ---------------------------------------------------------------------------------

# Where his drawing sits in the cell. His body's centre (source 8.5, 8) lands a little above the
# middle of the cell, which is where the scene's collision circle is, so what you hit is the sheet
# and not the hem trailing under it.
ANCHOR = (8.5, 8.0)
HOME = (48, 40)
GROUND = 86          # the floor, for the detached shadow
HEM_CLIP = 12.4      # in his rows: below this his rounded base is replaced by the ragged hem

# The ragged hem: (position across the base 0..1, length, half-width at the root). Five, uneven,
# and the longest is not in the middle - an even fringe is a comb.
TENDRILS = [(0.06, 9, 4.0), (0.27, 15, 5.0), (0.49, 10, 4.5), (0.70, 18, 5.5), (0.92, 8, 3.5)]

# Folds in the sheet, in his coordinates (frame A; frame B's head sits one column right). A light
# stop beside a dark stop, carried from the chest into the hem - section 4b's interior edges.
FOLDS = [[(6.4, 9.6), (7.2, 11.4), (7.6, 13.8)],
         [(11.6, 10.6), (12.0, 12.2), (13.2, 14.2)],
         [(9.6, 12.3), (9.9, 13.9)]]


def pose(src, frame=0, dx=0, dy=0, sx=1.0, sy=1.0, arm_dx=0, arm_dy=0, arm_sy=1.0,
         mouth=1.0, glow=0.55, hem=1.0, flare=0.0, phase=0.0, trans=0.0, pupil=1, squint=1.0,
         dissolve=0.0, erode=0, eyes_only=False, shadow=True, shadow_dy=None):
    f = src[frame]
    hx, hy = HOME[0] + dx, HOME[1] + dy
    lean = 1 if frame == 1 else 0

    def to_cell(cells):
        return _clip(_shift(cells, hx, hy))

    body_src = f["solid"] - f["arm"]
    body = to_cell(_grow(body_src, sx, sy, about=ANCHOR, blur=2.2))
    # Raised by STRETCHING about its root (his row 10.5, where it leaves the base) and thrown by
    # SHEARING it, never by moving it: a ghost's arm that lifts off its body is a floating mitten.
    root_y = hy + int(round((10.5 - ANCHOR[1]) * UP * sy))
    arm_h = (10.5 - min(y for _x, y in f["arm"])) * UP * sy
    reach = arm_sy * (1.0 + max(0, -arm_dy) / max(1.0, arm_h))
    grown = _shift(_grow(f["arm"], sx, sy * reach, about=(ANCHOR[0], 10.5), blur=2.2),
                   0, root_y - hy + max(0, arm_dy))
    arm = set()
    for (x, y) in grown:
        lift = max(0.0, (root_y - hy - y) / max(1.0, arm_h * reach))
        arm.add((x + int(round(arm_dx * lift)), y))
    arm = to_cell(arm)
    arm -= set(p for p in arm if p[1] >= root_y + 2)

    # His rounded base goes; the hem replaces it.
    clip_y = hy + int(round((HEM_CLIP - ANCHOR[1]) * UP * sy))
    body = set(p for p in body if p[1] < clip_y)
    row = [x for x, y in body if y == clip_y - 1]
    if not row:
        raise ValueError("no base row to hang the hem from")
    left, right = min(row), max(row)
    tips = clip_y
    for i, (at, length, hw) in enumerate(TENDRILS):
        L = length * hem * sy
        cx0 = left + (right - left) * (at + (at - 0.5) * 0.35 * flare)
        for d in range(int(L) + 1):
            t = d / max(1.0, L)
            half = max(0.8, hw * (1.0 + 0.3 * flare) * (1.0 - t) ** 0.6)
            sway = math.sin(phase * 2 * math.pi + i * 1.3) * 2.2 * t
            c = cx0 + 0.32 * d + sway                     # trails behind: the sprite faces left
            for x in range(int(math.floor(c - half)), int(math.ceil(c + half)) + 1):
                if abs(x - c) <= half + 0.25:
                    body.add((x, clip_y - 1 + d))
            tips = max(tips, clip_y + d)
        # the root of each point is filled across to its neighbours, so the hem is one cloth
    for x in range(left, right + 1):
        body.add((x, clip_y - 1))
        body.add((x, clip_y))
    body = _clip(body)

    union = body | arm
    top = min(y for _x, y in union)
    bottom = max(y for _x, y in union)

    img = {}
    if shadow:
        _shadow(img, (left + right) / 2.0 + 4, GROUND if shadow_dy is None else GROUND + shadow_dy,
                lift=(HOME[1] - hy))

    fade0 = hy + int(round((10.6 - ANCHOR[1]) * UP * sy))
    _shade(img, union, union, top, bottom, bias=0.20)
    # The glow: centred in the chest, below the mouth.
    mcx, mcy = _centroid(f["mouth"])
    gx = hx + (mcx - 0.5 - ANCHOR[0]) * UP * sx
    gy = hy + (mcy + 3.0 - ANCHOR[1]) * UP * sy
    _glow(img, body, gx, gy, 15.0 * sy, glow)
    _folds(img, body, frame, hx, hy, sx, sy, lean)
    _edges(img, union, fade0)

    # The face.
    face = set()
    marks = set()
    for k, eye in enumerate(f["eyes"]):
        ecx, ecy = _centroid(eye)
        cells = to_cell(_shift(_grow(eye, sx, sy * squint, about=(ecx, ecy), blur=1.2),
                               int(round((ecx - ANCHOR[0]) * UP * sx)),
                               int(round((ecy - ANCHOR[1]) * UP * sy))))
        if not cells:
            continue
        x0 = min(x for x, _y in cells)
        x1 = max(x for x, _y in cells)
        y0 = min(y for _x, y in cells)
        y1 = max(y for _x, y in cells)
        span = max(1, x1 - x0)
        brow = 4.0 * squint
        kept = set()
        for (x, y) in cells:
            inner = (x - x0) / float(span) if k == 0 else (x1 - x) / float(span)
            if (y - y0) >= brow * inner - 0.5:     # cut the top inner corner: a brow, not a stare
                kept.add((x, y))
        face |= kept
        for p in kept:
            img[p] = OCC
        # the burning point, low in the socket and toward the nose
        px_ = (x1 - 3) if k == 0 else (x0 + 2)
        py_ = y1 - 4
        _pupil(img, px_, py_, pupil, marks)
    mcells = to_cell(_shift(_grow(f["mouth"], sx * (0.85 + 0.15 * mouth), sy * mouth,
                                  about=(mcx, mcy), blur=1.3),
                            int(round((mcx - ANCHOR[0]) * UP * sx)),
                            int(round((mcy - ANCHOR[1]) * UP * sy))))
    for p in mcells:
        img[p] = OCC
    face |= mcells
    if mcells:
        mx = sum(x for x, _y in mcells) / float(len(mcells))
        my = max(y for _x, y in mcells) - 2.0 - 1.5 * max(0.0, mouth - 1.0)
        _throat(img, mcells, mx, my, glow)

    # See-through: holes in the lower sheet, growing toward the hem. Cut AFTER the edges are lit.
    for p in list(img.keys()):
        if p in face or p not in union:
            continue
        x, y = p
        t = max(0.0, (y - fade0) / float(max(1, tips - fade0)))
        amount = trans + (0.62 * t ** 1.3 if y > fade0 else 0.0)
        if amount > 0 and _bayer(x, y) < amount:
            del img[p]

    if erode:
        img = _erode(img, union, erode, keep=face | marks)
    if dissolve > 0:
        for p in list(img.keys()):
            if p in union and p not in marks and _bayer(p[0] + 3, p[1] + 5) < dissolve:
                del img[p]
    if eyes_only:
        img = dict((p, c) for p, c in img.items() if p in marks)
    return _to_image(img)


def _shade(img, cells, union, top, bottom, bias=0.0):
    """Five tones by where the key reaches: distance to the edge toward the upper left against
    distance to the edge away from it, plus height - pale crown, dark hem."""
    height = max(1.0, float(bottom - top))
    for (x, y) in cells:
        dk = 0
        while dk < 12 and (x - dk - 1, y - dk - 1) in union:
            dk += 1
        ds = 0
        while ds < 12 and (x + ds + 1, y + ds + 1) in union:
            ds += 1
        shape = dk / float(dk + ds + 1)
        vert = (y - top) / height
        v = 0.50 * shape + 0.55 * vert + bias + (_bayer(x, y) - 0.5) * 0.14
        if v < 0.13 and vert < 0.32:
            c = BONE[0]
        elif v < 0.30:
            c = BONE[1]
        elif v < 0.52:
            c = BONE[2]
        elif v < 0.78:
            c = BONE[3]
        else:
            c = BONE[4]
        img[(x, y)] = c


def _glow(img, cells, gx, gy, radius, strength):
    """The cold light inside it: arcane cyan, dithered out into the body so it reads as light
    behind cloth rather than as a painted disc."""
    if strength <= 0:
        return
    for (x, y) in cells:
        r = math.hypot((x - gx) * 1.15, y - gy) / radius
        if r >= 1.0:
            continue
        g = strength * (1.0 - r) ** 1.35 + (_bayer(x + 2, y + 1) - 0.5) * 0.22
        if g > 1.05:
            img[(x, y)] = CYAN[0]
        elif g > 0.72:
            img[(x, y)] = CYAN[1]
        elif g > 0.42:
            img[(x, y)] = CYAN[2]
        elif g > 0.24 and img.get((x, y)) in (BONE[3], BONE[4]):
            img[(x, y)] = CYAN[3]


def _folds(img, cells, frame, hx, hy, sx, sy, lean):
    for fold in FOLDS:
        pts = [(hx + (fx + lean - ANCHOR[0]) * UP * sx, hy + (fy - ANCHOR[1]) * UP * sy)
               for fx, fy in fold]
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
            for i in range(n + 1):
                t = i / float(n)
                x = int(round(x0 + (x1 - x0) * t))
                y = int(round(y0 + (y1 - y0) * t))
                if (x, y) in cells and img.get((x, y)) not in CYAN:
                    img[(x, y)] = BONE[4]
                    if (x - 1, y) in cells and img.get((x - 1, y)) not in CYAN:
                        img[(x - 1, y)] = BONE[2]


def _edges(img, union, fade0):
    """Rim where the edge faces the key, occlusion where it faces away, deep between. No rim on
    the see-through hem: a lit edge on thin cloth reads as an icicle, not as cloth."""
    k = -1.0 / math.sqrt(2.0)
    for (x, y) in union:
        nx = ny = 0
        for ddy in (-1, 0, 1):
            for ddx in (-1, 0, 1):
                if (ddx or ddy) and (x + ddx, y + ddy) not in union:
                    nx += ddx
                    ny += ddy
        if nx == 0 and ny == 0:
            if any((x + a, y + b) not in union for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                img[(x, y)] = BONE[4]
            continue
        d = (nx * k + ny * k) / math.hypot(nx, ny)
        if d > 0.38:
            img[(x, y)] = RIM if y < fade0 else BONE[3]
        else:
            img[(x, y)] = OCC if d < -0.38 else BONE[4]


def _pupil(img, x, y, size, marks):
    """His mark: the same arcane point every taken thing carries, here at the bottom of a void."""
    def put(points, colour):
        for p in points:
            img[p] = colour
            marks.add(p)
    if size <= 0:
        put(((x, y), (x + 1, y)), MARK[1])
        return
    put(((x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)), MARK[0])
    put(((x, y - 1), (x + 1, y - 1), (x, y + 2), (x + 1, y + 2)), MARK[1])
    put(((x - 1, y), (x + 2, y), (x - 1, y + 1), (x + 2, y + 1)), MARK[2])
    if size >= 2:
        put(((x, y - 2), (x + 1, y - 2), (x - 1, y - 1), (x + 2, y - 1),
             (x - 1, y + 2), (x + 2, y + 2), (x, y + 3), (x + 1, y + 3)), MARK[3])


def _throat(img, mouth, mx, my, glow):
    """The inner light seen at the back of the open mouth."""
    stops = [(2.6, CYAN[3], 0.3), (1.7, CYAN[2], 0.6), (1.0, CYAN[1], 0.95), (0.5, CYAN[0], 1.2)]
    for radius, colour, need in stops:
        if glow < need:
            continue
        for (x, y) in mouth:
            if math.hypot(x - mx, (y - my) * 1.2) <= radius:
                img[(x, y)] = colour


def _shadow(img, cx, cy, lift=0):
    """Detached, on the floor, softer and smaller the higher it floats."""
    rx = max(8.0, 20.0 - 0.6 * lift)
    ry = max(2.0, 4.0 - 0.1 * lift)
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            r = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if r <= 1.0 and _bayer(x, y) < (0.75 if r < 0.45 else 0.45):
                if 0 <= x < CELL and 0 <= y < CELL:
                    img[(x, y)] = OCC


def _erode(img, union, passes, keep=()):
    """Eat the silhouette from the outside in, sparing the face, which is what is left last."""
    cells = set(p for p in img if p in union)
    for _ in range(passes):
        doomed = [p for p in cells if p not in keep and any(
            (p[0] + a, p[1] + b) not in cells for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
        for p in doomed:
            cells.discard(p)
    return dict((p, c) for p, c in img.items() if p in cells or p not in union)


def _to_image(img):
    out = Image.new("RGBA", (CELL, CELL), CLEAR)
    px = out.load()
    for (x, y), c in img.items():
        if 0 <= x < CELL and 0 <= y < CELL:
            px[x, y] = c
    return out


def animations():
    src = _source()

    # moving: his own two frames, floating. Eight frames on the roster's clock, and a bob that is
    # not a straight up-down or it reads as a bounce. The hem sways on its own phase underneath.
    BOB = [0, -1, -2, -2, -1, 0, 1, 1]
    moving = [pose(src, 0 if i % 4 < 2 else 1, dy=BOB[i], phase=i / 8.0) for i in range(8)]

    # attack: THE WAIL. Six frames across the one second the ring takes to land, so the scream is
    # on its face when the ring does (BooEnemy.WailWindUpSeconds).
    attack = [
        pose(src, 0, dy=3, sx=1.06, sy=0.93, arm_dy=3, mouth=0.75, glow=0.65, hem=0.85,
             phase=0.10, squint=0.85),                                          # gather, low
        pose(src, 1, dy=-2, sx=0.96, sy=1.05, arm_dy=-4, mouth=1.05, glow=0.80, hem=1.05,
             flare=0.2, phase=0.25),                                            # rise
        pose(src, 0, dx=2, dy=-5, sx=0.95, sy=1.08, arm_dy=-9, arm_sy=1.15, mouth=1.30,
             glow=0.95, hem=1.15, flare=0.45, phase=0.40, pupil=2),             # rear back, arm high
        pose(src, 0, dx=3, dy=-4, sx=0.95, sy=1.08, arm_dy=-8, arm_sy=1.15, mouth=1.45,
             glow=1.05, hem=1.15, flare=0.55, phase=0.55, pupil=2),             # tremble
        pose(src, 1, dx=-3, dy=-1, sx=1.10, sy=0.98, arm_dx=-3, arm_dy=-5, mouth=1.75,
             glow=1.25, hem=1.30, flare=0.85, phase=0.70, pupil=2),             # lunge
        pose(src, 1, dx=-4, dy=0, sx=1.12, sy=0.97, arm_dx=-4, arm_dy=-4, mouth=1.90,
             glow=1.35, hem=1.35, flare=1.0, phase=0.85, pupil=2),              # the wail
    ]

    # hurt: a flinch back, away from the facing side, the sheet thinning for a beat.
    hurt = [
        pose(src, 0, dx=4, dy=-1, sx=0.94, sy=1.04, mouth=1.25, glow=0.35, trans=0.22,
             phase=0.2, pupil=2),
        pose(src, 0, dx=2, dy=0, sx=0.98, sy=1.01, mouth=1.10, glow=0.45, trans=0.10,
             phase=0.3),
    ]

    # death: dissolves from the outside in and drifts up, the wail still on its face, and the two
    # burning points outlast everything else.
    death = [
        pose(src, 0, dy=-2, mouth=1.4, glow=0.9, dissolve=0.10, phase=0.1, pupil=2,
             shadow_dy=0),
        pose(src, 0, dy=-5, sy=1.04, mouth=1.6, glow=0.8, dissolve=0.28, erode=1, phase=0.3,
             pupil=2),
        pose(src, 1, dy=-8, sy=1.07, mouth=1.6, glow=0.6, dissolve=0.46, erode=2, hem=1.2,
             phase=0.5, pupil=2),
        pose(src, 1, dy=-11, sy=1.10, mouth=1.5, glow=0.4, dissolve=0.64, erode=3, hem=1.3,
             phase=0.7, pupil=1, shadow=False),
        pose(src, 1, dy=-14, sy=1.12, mouth=1.4, glow=0.2, dissolve=0.82, erode=5, hem=1.4,
             phase=0.9, pupil=1, shadow=False),
        pose(src, 1, dy=-17, sy=1.12, mouth=1.4, glow=0.0, dissolve=1.0, erode=6, pupil=1,
             eyes_only=True, shadow=False),
    ]

    # The actor finish every sheet gets (matte.py): on him that is mostly the rim, softened where
    # it edges the deep half of the sheet, so the ghost reads as cloth rather than as wet glass.
    anims = [("moving", moving, True, 6.0), ("attack", attack, False, 6.0),
             ("hurt", hurt, False, 12.0), ("death", death, False, 10.0)]
    return [(name, [matte.finish(f) for f in frames], loop, speed)
            for name, frames, loop, speed in anims]


# --- checks and previews ----------------------------------------------------------------------

def density(frame):
    """Internal edges per filled pixel (art-direction section 4b), for the record."""
    px = frame.load()
    filled = edges = 0
    for y in range(CELL):
        for x in range(CELL):
            c = px[x, y]
            if not c[3]:
                continue
            filled += 1
            for dx, dy in ((1, 0), (0, 1)):
                if x + dx < CELL and y + dy < CELL:
                    d = px[x + dx, y + dy]
                    if d[3] and d != c:
                        edges += 1
    return filled, edges, edges / float(max(1, filled))


def figure_height(frame):
    box = frame.getbbox()
    return 0 if box is None else box[3] - box[1]


def strip(frames):
    sheet = Image.new("RGBA", (CELL * len(frames), CELL), CLEAR)
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * CELL, 0))
    return sheet


def _paper(w, h):
    """A stand-in for the Sketchbook's squared paper, for judging the sprite on its own ground."""
    im = Image.new("RGBA", (w, h), (226, 222, 206, 255))
    px = im.load()
    for y in range(h):
        for x in range(w):
            if x % 16 == 0 or y % 16 == 0:
                px[x, y] = (196, 204, 214, 255)
    return im


def preview(anims, path, zoom=3):
    """Every frame at x2 (the game's scale) and again at `zoom` for detail, on paper and on stone."""
    rows = len(anims)
    cols = max(len(f) for _n, f, _l, _s in anims)
    cw = CELL * zoom + 8
    W = cols * cw + 8
    H = rows * 2 * cw + 8
    sheet = Image.new("RGBA", (W, H), (11, 15, 24, 255))
    for r, (_name, frames, _loop, _speed) in enumerate(anims):
        for c, f in enumerate(frames):
            big = f.resize((CELL * zoom, CELL * zoom), Image.NEAREST)
            paper = _paper(CELL * zoom, CELL * zoom)
            paper.alpha_composite(big)
            sheet.alpha_composite(paper, (8 + c * cw, 8 + r * 2 * cw))
            stone = Image.new("RGBA", (CELL * zoom, CELL * zoom), _rgba(B.MATERIALS["stone"][2]))
            stone.alpha_composite(big)
            sheet.alpha_composite(stone, (8 + c * cw, 8 + (r * 2 + 1) * cw))
    sheet.convert("RGB").save(path)


def ghost_sheet(anims, shots, path):
    """tools/art/_ghost_sheet.png: his drawing, the new frames, and the game's own photographs."""
    pad = 16
    original = Image.open(SOURCE).convert("RGBA")
    orig_big = original.resize((original.size[0] * 12, original.size[1] * 12), Image.NEAREST)
    orig_bg = _paper(orig_big.size[0] + 32, orig_big.size[1] + 32)
    orig_bg.alpha_composite(orig_big, (16, 16))
    strips = []
    for _name, frames, _l, _s in anims:
        s = strip(frames)
        s = s.resize((s.size[0] * 2, s.size[1] * 2), Image.NEAREST)
        bg = _paper(s.size[0], s.size[1])
        bg.alpha_composite(s)
        strips.append(bg)
    shot_ims = []
    for p in shots:
        im = Image.open(p).convert("RGBA")
        scale = 560.0 / im.size[1]
        shot_ims.append(im.resize((int(im.size[0] * scale), 560), Image.BILINEAR))
    W = max([orig_bg.size[0]] + [s.size[0] for s in strips]
            + [sum(s.size[0] + pad for s in shot_ims)]) + pad * 2
    H = pad + orig_bg.size[1] + pad + sum(s.size[1] + pad for s in strips) \
        + (560 + pad if shot_ims else 0) + pad
    sheet = Image.new("RGBA", (W, H), (11, 15, 24, 255))
    y = pad
    sheet.alpha_composite(orig_bg, (pad, y))
    y += orig_bg.size[1] + pad
    for s in strips:
        sheet.alpha_composite(s, (pad, y))
        y += s.size[1] + pad
    x = pad
    for s in shot_ims:
        sheet.alpha_composite(s, (x, y))
        x += s.size[0] + pad
    sheet.convert("RGB").save(path)
    return path


def main(argv):
    anims = animations()
    if argv[:1] == ["--sheet"]:
        print("wrote %s" % ghost_sheet(anims, argv[1:], os.path.join(HERE, "_ghost_sheet.png")))
        return
    out = os.path.join(HERE, "_boo")
    if not os.path.isdir(out):
        os.makedirs(out)
    for name, frames, _loop, _speed in anims:
        strip(frames).save(os.path.join(out, "boo-%s.png" % name))
        filled, edges, ratio = density(frames[0])
        empty = [i for i, f in enumerate(frames) if not f.getbbox()]
        print("  %-7s %d frames  height %2dpx (x2 = %3dpx)  %.2f edges/px%s"
              % (name, len(frames), figure_height(frames[0]), 2 * figure_height(frames[0]), ratio,
                 "  (empty: %s)" % empty if empty else ""))
    preview(anims, os.path.join(out, "boo-preview.png"))
    print("wrote %s" % out)


if __name__ == "__main__":
    main(sys.argv[1:])
