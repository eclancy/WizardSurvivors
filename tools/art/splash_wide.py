# -*- coding: utf-8 -*-
"""Vigil, composed for a landscape screen. Run: python tools/art/splash_wide.py

The portrait title (splash.vigil) is a 360x640 composition grown to 360x840 for tall phones. A PC
now gets a 1280x720 landscape canvas (DisplayProfile; stretch aspect keep_height, so the height is
always 720 and a wider monitor gets more WIDTH), and centring that tall picture in it cut the
wordmark off the top and the ward and the prompt off the bottom. This is the same scene drawn for
the other shape - not a crop of the portrait one, because a wide frame wants a different layout:

  - the figure and his ward stand right of centre;
  - the wordmark and, under it, the menu buttons take the left third, on the dark wood;
  - PRESS ANY BUTTON sits low and central, under the ward;
  - the Arcane Energy readout sits top right in the canopy, Options bottom left, the preset and
    next-run readout bottom right - every one of them on a pocket of dark the art keeps for it.

The core is 640x360, drawn at x2 into 1280x720, and it is centred in a 960x360 frame (1920x720 at
x2) so a 21:9 monitor still sees picture rather than ground colour - the same move as the tall
portrait frame, turned on its side. Both side bands fade to occlusion at the frame edge.

Every primitive is shared with the portrait screen (splashkit, hero, splash's _frond/_big_leaf),
and the layer split is the same nine stages, so anim.py emits the same 30-frame set for it.
The figure is hero.wizard_hero at the same size as on the phone.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import hero
import raster
import splash
import splashkit as sk

M, E, OCC = splash.M, splash.E, splash.OCC
STAGES = splash.STAGES

CORE_W, CORE_H = 640, 360
WW, WH = 960, 360
C0 = (WW - CORE_W) // 2            # 160: the core's left edge in the wide frame

SHIPPING = "assets/bonelight/ui/title-screen-wide.png"

# --- where things are, in WIDE-frame pixels. TitleScreen and MainMenu read the same numbers off the
# core (core x = wide x - C0, and core (320, 180) is the centre of the screen); keep them in step.
WARD_X, WARD_Y = C0 + 430, 256     # ward centre, and the figure's feet
WORD_X, WORD_Y = C0 + 175, 14      # wordmark centre and cap line
# The wordmark's whole-pixel scales, WIZARD then SURVIVORS. The phone's (3, 2) came out about 380
# canvas units wide on a 1280 screen and read as small for a PC title; (5, 3) is 1.67x and 1.5x,
# 620 and 558 units, and still leaves the staff clear of it.
WORD_SCALES = (5, 3)
WORD_GAP = 12
WORD_BOTTOM = WORD_Y + 12 * WORD_SCALES[0] + WORD_GAP + 12 * WORD_SCALES[1] + 2
WORD_HALF = 156                    # half the WIZARD line, 310 wide
HORIZON = 200
# Rectangles (x0, y0, x1, y1) the layout puts UI on. The art keeps each one dark and quiet.
WORD_BOX = (WORD_X - WORD_HALF, WORD_Y, WORD_X + WORD_HALF, WORD_BOTTOM)
MENU_BOX = (C0 + 60, 134, C0 + 290, 240)      # Start Run and the three buttons, under the wordmark
ARCANE_BOX = (C0 + 430, 8, C0 + 630, 34)      # Arcane Energy readout
OPTIONS_BOX = (C0 + 12, 292, C0 + 126, 324)   # Options
DETAIL_BOX = (C0 + 470, 312, C0 + 632, 352)   # preset / next-run readout
PROMPT_BOX = (C0 + 177, 322, C0 + 462, 346)   # PRESS ANY BUTTON (see PROMPT_BOTTOM)
VERSION_BOX = (C0 + 4, 338, C0 + 48, 356)     # the version label, bottom-left of a 16:9 screen
PROMPT_BOTTOM = 346                           # TitleScreen.LandscapePromptBottomRow must agree


def _box_t(x, y, box, strength, soft):
    """A soft-edged pocket of darkness over a UI box: full `strength` inside, fading out over
    `soft` pixels round it. Measured to the box edge, so a long box is not a long ellipse."""
    x0, y0, x1, y1 = box
    dx = max(x0 - x, 0, x - x1)
    dy = max(y0 - y, 0, y - y1)
    d = math.sqrt(dx * dx + dy * dy)
    return strength * min(1.0, max(0.0, 1.0 - d / float(soft)))


def ui_pocket_t(x, y):
    # Wide soft edges and only as dark as each needs. The first pass held every box at full
    # strength with a short fade, and with the menu closed the empty ones read as black panels
    # printed on the picture. The prompt is the one that has to be fully dark: it is the only
    # bitmap type, and the only thing here on screen when the menu is not.
    return max(_box_t(x, y, PROMPT_BOX, 3.3, 18), _box_t(x, y, DETAIL_BOX, 2.6, 18),
               _box_t(x, y, OPTIONS_BOX, 2.4, 16), _box_t(x, y, VERSION_BOX, 3.0, 10),
               _box_t(x, y, ARCANE_BOX, 2.4, 14), _box_t(x, y, MENU_BOX, 2.6, 14))


def edge_t(x, y):
    """The wide frame's vignette: next to nothing across the core, closing to occlusion over the
    two side bands, so a monitor wider than 24:9 still meets the Ground colour invisibly."""
    nx = (x - WW / 2.0) / (CORE_W / 2.0 + 40.0)
    ny = (y - WH / 2.0) / (WH / 2.0 + 70.0)
    t = 1.6 * (nx * nx + ny * ny) ** 1.4
    over = (abs(x - WW / 2.0) - CORE_W / 2.0) / float(C0)
    if over > 0:
        t += 3.0 * over ** 1.3
    return t


# anim.py walks the blink index through 2, 6, 10, 0 and 13 - pairs of the portrait-sized set.
# The extra pairs out in the side bands take their own shut pair on the same frames, far from the
# first, so each blink frame closes two pairs in different parts of the wood rather than the new
# ones never blinking at all. Treeline extras are 21-24 (left) and 25-28 (right); branch extras
# 31-33 (left) and 34-35 (right).
_EXTRA_SHUT = {2: 22, 6: 32, 10: 26, 0: 34, 13: 24}


def _extra(blink, base):
    return _EXTRA_SHUT.get(blink)


# Every pair placed in this render, on both eye layers. A pair keeps EYE_SPACING (an ellipse, x and
# y) clear of all of them. 2026-10-29, Eric: "the main screen background awkwardly has eyes that sit
# on top of each other. the red eyes should be spread out more randomly like on the mobile screen."
# Nothing kept two pairs apart, and the extra pairs left of the figure were drawn into strips 16 and
# 66 columns wide, so they stood in columns and ran into each other. Reset by vigil_wide.
_placed = []
EYE_SPACING = (28.0, 22.0)
# The strip of wood between the menu column and the staff is the only stretch left of the figure a
# 16:9 or 3:2 screen shows, and it is narrow: every region that reaches it fed it, and five pairs
# there read as one column. It holds two, across both layers.
GAP_MAX = 2


def _in_gap(x):
    return MENU_BOX[2] + 8 < x < WARD_X - 50


def _eyes(c, x0, x1, y0, y1, n, seed, avoid, shut, base, size=3):
    """splash.eyes_in_the_dark, with an x range, a list of boxes to keep out of, and a size:
    2 for eyes deep in the wood, 3 as on the phone, 4 for the nearer pairs in the branches.

    Positions come from rng.random(), not randrange: Python 3 changed randrange, and rng.random()
    is the same stream under 2.7 and 3, so either interpreter places these eyes where the other
    did (a + int(random() * (b - a)) is what 2.7's randrange was)."""
    ramp = [E["fire"][2], E["fire"][3], E["fire"][3], None]
    rng = random.Random(seed)
    placed = 0
    sx, sy = EYE_SPACING
    tries = 0
    while placed < n:
        x = x0 + int(rng.random() * (x1 - x0))
        y = y0 + int(rng.random() * (y1 - y0))
        gap = size + int(rng.random() * 3)
        tries += 1
        if tries % 200 == 0:
            sx, sy = sx * 0.85, sy * 0.85      # a crowded region relaxes rather than hangs
        if any(b[0] - 8 <= x <= b[2] + 8 and b[1] - 8 <= y <= b[3] + 8 for b in avoid):
            continue
        if any(((x - px) / sx) ** 2 + ((y - py) / sy) ** 2 < 1.0 for px, py in _placed):
            continue
        if _in_gap(x) and sum(1 for px, _ in _placed if _in_gap(px)) >= GAP_MAX:
            continue
        # Recorded before the blink skip, so a pair that is shut on this frame still holds its
        # place and every frame of the blink puts the others in the same spots.
        _placed.append((x, y))
        placed += 1
        if shut is not None and base + placed - 1 == shut:
            continue
        for ox in (0, gap):
            if size <= 2:
                # far back: no hot core, just the dull red, so they sit behind the treeline set
                c.radial(x + ox, y, 2, 2, [ramp[2], ramp[3], None])
                c.set(x + ox, y, ramp[1])
            else:
                c.radial(x + ox, y, size, size, [ramp[1], ramp[2], ramp[3], None])
                c.set(x + ox, y, ramp[0])
                if size >= 4:
                    c.set(x + ox + 1, y, ramp[0])


def vigil_wide(beard="mane-spear", layers=False, gain=1.0, phase=0, flicker=1.0, blink=None):
    WARD = E["light"]
    GROUND = M["gold"]
    FAR_TONES = [(M["stone"][4], M["stone"][3]), (M["violet"][4], M["violet"][3]),
                 (M["stone"][4], M["lichen"][3]), (M["wool"][4], M["stone"][3]),
                 (M["violet"][4], M["stone"][3]), (M["arcane"][4], M["stone"][3])]
    MID_TONES = [(M["stone"][4], M["violet"][4]), (M["violet"][4], M["stone"][4]),
                 (M["stone"][4], M["lichen"][4]), (M["wool"][4], M["violet"][4]),
                 (M["violet"][4], M["violet"][3]), (M["stone"][4], M["stone"][3])]
    NEAR_TONES = [(OCC, M["stone"][4]), (OCC, M["violet"][4]), (M["violet"][4], M["stone"][4]),
                  (OCC, M["lichen"][4]), (M["wool"][4], M["stone"][4]), (OCC, M["stone"][4])]
    tr = random.Random(9631)

    def tone(pool):
        return pool[tr.randrange(len(pool))]

    # Same trick as vigil(): flat mode points every stage at one canvas, so the draw order is the
    # composition and the layers composite back to it exactly.
    if layers:
        cv = dict((n, raster.Canvas(WW, WH)) for n in STAGES)
        cv["backA"] = raster.Canvas(WW, WH, OCC)
    else:
        one = raster.Canvas(WW, WH, OCC)
        cv = dict((n, one) for n in STAGES)

    def T(n):
        return cv[n]

    # ---------------------------------------------------------------- backA: sky and far wood
    c = T("backA")
    c.vramp(0, HORIZON + 6, [M["stone"][4], M["violet"][4], M["violet"][3], M["violet"][2],
                             M["stone"][3]])
    sk.starfield(c, 6, 150, 150, 11)

    rng = random.Random(17)
    far = []
    x = -20
    while x < WW + 20:
        h = 60 + rng.randrange(0, 90)
        # under the wordmark the far trees stay low, so the letters keep a plain sky behind them
        if WORD_X - WORD_HALF - 10 < x < WORD_X + WORD_HALF + 10:
            h = min(h, HORIZON - WORD_BOTTOM - 6)
        far.append((x, h, 50 + h * 0.5, rng.randrange(9999)))
        x += 16 + rng.randrange(0, 22)
    for (tx, th, sp, sd) in sorted(far, key=lambda t: -t[1]):
        td, tl = tone(FAR_TONES)
        sk.tree(c, tx, HORIZON, th, sp, seed=sd, dark=td, light=tl, density=2.2)
    # the middle depth, as on the phone: a third plane between the pale far wood and the near
    mr = random.Random(4421)
    x = -10
    while x < WW + 10:
        h = 70 + mr.randrange(0, 60)
        if WORD_X - WORD_HALF - 20 < x < WORD_X + WORD_HALF + 20:
            h = min(h, HORIZON + 6 - WORD_BOTTOM - 6)
        td, tl = MID_TONES[mr.randrange(len(MID_TONES))]
        sk.tree(c, x, HORIZON + 6, h, h * 0.6, seed=mr.randrange(9999), dark=td, light=tl,
                density=2.0)
        x += 30 + mr.randrange(0, 40)
    for _ in range(260):
        side = abs(mr.random() * 2.0 - 1.0) ** 0.6
        bx = WW // 2 + (1 if mr.random() < 0.5 else -1) * int(side * (WW // 2 + 10))
        by = 120 + int(mr.random() * 90)
        if WORD_X - WORD_HALF - 20 < bx < WORD_X + WORD_HALF + 20 and by < WORD_BOTTOM + 16:
            continue
        br = mr.randrange(6, 16)
        td, tl = MID_TONES[mr.randrange(len(MID_TONES))]
        sk.brush(c, bx, by, br, br * 0.7, seed=mr.randrange(9999), dark=td, light=tl,
                 density=1.3)
    rb = random.Random(211)
    for _ in range(420):                                  # undergrowth along the treeline
        bx = rb.randrange(-12, WW + 12)
        by = HORIZON - 26 + rb.randrange(0, 26)
        br = rb.randrange(5, 15)
        td, tl = tone(FAR_TONES)
        sk.brush(c, bx, by, br, br * 0.62, seed=rb.randrange(9999), dark=td, light=tl,
                 density=1.3)
    for _ in range(50):                                   # a broken horizon, not a ruled one
        bx = rb.randrange(-10, WW + 10)
        br = rb.randrange(12, 24)
        td, tl = tone(FAR_TONES)
        sk.brush(c, bx, HORIZON - 20 + rb.randrange(0, 12), br, br * 0.85,
                 seed=rb.randrange(9999), dark=td, light=tl, density=1.25)
    for _ in range(140):                                  # near-black at the foot of the trunks
        bx = rb.randrange(-14, WW + 14)
        br = rb.randrange(6, 16)
        td, tl = tone(NEAR_TONES)
        sk.brush(c, bx, HORIZON - 22 + rb.randrange(0, 20), br, br * 0.55,
                 seed=rb.randrange(9999), dark=td, light=tl, density=1.5)
    for _ in range(300):                                  # darker leaf mass down in the wood
        bx = rb.randrange(-16, WW + 16)
        by = 120 + int(rb.random() ** 0.7 * 86)
        br = rb.randrange(5, 15)
        td, tl = tone(NEAR_TONES)
        sk.brush(c, bx, by, br, br * 0.62, seed=rb.randrange(9999), dark=td, light=tl,
                 density=1.15)
    del _placed[:]
    # The treeline set across the core - the part every screen shows - as the phone's spread
    # across its whole width. Drawn across all 960 columns, a third of them landed in the side
    # bands that only an ultrawide sees, and a 3:2 screen was left with a handful.
    # Seven, not the twelve it had across the whole frame: the menu column, the wordmark and the
    # figure take most of the core, and the dark wood left over holds about as many pairs as the
    # phone's density puts there (.ai/title-screen.md: "eyes are cheap and easy to overspend").
    _eyes(T("eyesA"), C0 - 10, C0 + CORE_W - 10, 150, 206, 7, 5,
          [MENU_BOX, (WARD_X - 60, 30, WARD_X + 60, 260)], blink, 0)
    # More of them out in the side bands an ultrawide monitor shows, left and right, so a wide
    # screen still meets eyes at both edges. Deeper ones smaller and dimmer than the treeline set,
    # so they read as further back rather than as more of the same.
    _eyes(T("eyesA"), 4, C0 + 30, 112, 236, 4, 41, [MENU_BOX, WORD_BOX], _extra(blink, 21),
          21, size=2)
    _eyes(T("eyesA"), C0 + CORE_W - 30, WW - 12, 112, 236, 4, 43, [], _extra(blink, 25),
          25, size=2)

    # ---------------------------------------------------------------- backB: ground and canopy
    c = T("backB")
    splash_mist = random.Random(4)
    for y in range(HORIZON - 12, HORIZON + 14):
        tt = 1.0 - abs((y - (HORIZON - 12)) / 26.0 - 0.5) * 2.0
        for x in range(WW):
            if splash_mist.random() < 0.10 * tt:
                c.set(x, y, M["stone"][3])
    y, step = HORIZON - 3, 3
    while y < WH:
        hh = min(step, WH - y)
        t = (y - HORIZON) / float(WH - HORIZON)
        c.rect(0, y, WW - 1, y + hh, M["stone"][3] if t < 0.35 else M["stone"][2])
        c.hline(0, WW - 1, y, M["stone"][4])
        for _ in range(int(12 + t * 70)):
            c.set(rng.randrange(WW), y + rng.randrange(1, max(2, hh)),
                  M["stone"][1] if rng.random() < 0.35 else M["stone"][4])
        y += hh
        step += 1
    rg = random.Random(307)
    for _ in range(2600):                                 # grass, thickening toward the camera
        gx = rg.randrange(-6, WW + 6)
        gy = HORIZON + 4 + int(rg.random() ** 0.75 * (WH - HORIZON - 4))
        t = (gy - HORIZON) / float(WH - HORIZON)
        inside = ((gx - WARD_X) / 170.0) ** 2 + ((gy - WARD_Y) / 54.0) ** 2 < 1.0
        if inside and rg.random() < 0.97:
            continue
        sk.grass(c, gx, gy, 3 + t * 9, seed=rg.randrange(9999),
                 dark=M["lichen"][4] if rg.random() < 0.7 else M["stone"][4],
                 light=M["lichen"][3] if t < 0.6 else M["lichen"][2])
    for (bx, by, bw, bh, bs) in [(WARD_X - 250, 236, 30, 9, 2), (WARD_X + 200, 228, 22, 7, 6),
                                 (C0 - 40, 262, 34, 11, 9), (C0 + 640 + 30, 250, 26, 8, 13)]:
        sk.boulder(c, bx, by, bw, bh, seed=bs)
    # near trees at the edges of the core and of the wide frame, in flat occlusion: you are
    # looking out at the clearing from inside the wood
    for (tx, ty, th, sp, sd) in [(C0 + 6, 254, 230, 170, 71), (C0 + 640, 248, 220, 160, 83),
                                 (40, 262, 250, 180, 89), (WW - 36, 258, 240, 170, 97),
                                 (C0 - 60, 236, 200, 150, 61), (C0 + 700, 232, 190, 140, 67)]:
        sk.tree(c, tx, ty, th, sp, seed=sd, dark=OCC, light=M["stone"][4], density=2.6)
    # and leaf mass banked up the sides of the clearing between those trunks, so the wood is a
    # wall at both ends of the frame rather than a few trees standing on a lawn
    nb = random.Random(353)
    for _ in range(260):
        side = -1 if nb.random() < 0.5 else 1
        u = nb.random()
        bx = WW // 2 + side * (WW // 2 - int(u ** 1.4 * (C0 + 70))) + nb.randrange(-10, 11)
        by = 110 + int(nb.random() * 150)
        br = nb.randrange(8, 22)
        td, tl = tone(NEAR_TONES)
        sk.brush(c, bx, by, br, br * 0.75, seed=nb.randrange(9999), dark=td, light=tl,
                 density=1.4)
    # boughs reaching in over the top, and the canopy round the break the wordmark and the
    # figure stand in
    for (bx, by, ba, bl, bw, bs) in [(C0 - 18, 10, 0.25, 70, 5.0, 101), (C0 + 660, 4, 2.90, 66, 5.0, 113),
                                     (-16, 60, 0.10, 80, 5.2, 107), (WW + 16, 50, 3.05, 80, 5.2, 127),
                                     (C0 + 300, -14, 1.30, 40, 3.6, 131), (C0 + 20, -10, 0.70, 60, 4.4, 137),
                                     (C0 + 470, -16, 1.75, 48, 4.0, 139), (C0 + 600, -12, 2.30, 60, 4.6, 149),
                                     (60, -14, 0.90, 60, 4.6, 157), (WW - 60, -14, 2.20, 60, 4.6, 163)]:
        sk.bough(c, bx, by, ba, bl, bw, seed=bs, dark=OCC, light=M["stone"][4], leaf_r=15,
                 density=1.6)
    sk.canopy(c, -10, 175, (C0 + 255, 86, 265, 104), 700, seed=151, dark=OCC,
              light=M["stone"][4], width=WW, density=1.25)
    # Five, numbered from 9 so the blink frames anim.py walks (2, 6, 10, 0, 13) each close one pair
    # in view: 0, 2 and 6 in the treeline set above, 10 and 13 here.
    _eyes(T("eyesB"), C0 - 10, C0 + CORE_W - 10, 60, 150, 5, 71,
          [MENU_BOX, ARCANE_BOX, WORD_BOX,
           (WARD_X - 60, 30, WARD_X + 60, 260)], blink, 9)
    # and nearer, larger ones up in the branches of the side bands, as above (one strip beside the
    # staff was 16 columns wide and stacked its two pairs into one blob)
    _eyes(T("eyesB"), 10, C0 + 30, 50, 150, 3, 47, [WORD_BOX, MENU_BOX],
          _extra(blink, 31), 31, size=4)
    _eyes(T("eyesB"), C0 + CORE_W - 30, WW - 12, 50, 150, 2, 53, [],
          _extra(blink, 34), 34, size=4)

    # ---------------------------------------------------------------- the ward, as vigil() draws it
    cxp, cyp = WARD_X, WARD_Y
    A = WARD
    w = T("ward")
    w.radial(cxp, cyp, 208 * gain, 62 * gain,
             [GROUND[2], GROUND[2], GROUND[3], GROUND[3], GROUND[4], GROUND[4], None])
    w.ring(cxp, cyp, 152, 41, A[3], 2)
    w.ring(cxp, cyp, 146, 39, A[2] if gain > 0.93 else A[3], 2)
    w.ring(cxp, cyp, 128, 33, A[3], 1)
    w.ring(cxp, cyp, 96, 25, A[3], 1)
    star = []
    for i in range(6):
        a = i * 6.28318 / 6.0 - 1.5708
        star.append((cxp + math.cos(a) * 128, cyp + math.sin(a) * 33))
    for i in range(6):
        w.line(star[i][0], star[i][1], star[(i + 2) % 6][0], star[(i + 2) % 6][1],
               A[2] if gain > 0.86 else A[3])

    def ward_flames(front):
        for i, (nx, ny) in enumerate(star):
            depth = (ny - cyp) / 33.0
            if (depth > -0.1) != front:
                continue
            sk.flame(T("flamesB" if front else "flamesA"), nx, ny + 1, 29 + depth * 9, A,
                     seed=311 + i * 7 + phase * 97, halo=GROUND)
    for i in range(14):
        a = i * 6.28318 / 14.0 + 0.22
        rx0 = cxp + math.cos(a) * 139
        ry0 = cyp + math.sin(a) * 36
        w.radial(rx0, ry0, 7, 6, [A[2], A[3], None])
        w.vline(int(rx0), ry0 - 3, ry0 + 3, A[1])
        w.hline(rx0 - 2, rx0 + 2, ry0 - 1, A[1])
        w.set(int(rx0), int(ry0), A[0] if gain > 0.96 else A[1])
    ward_flames(False)
    hero.wizard_hero(T("figure"), cxp, cyp, h=150, staff_ramp=A, under_ramp=A, beard=beard,
                     flicker=flicker)
    ward_flames(True)
    rm = random.Random(509)
    for _ in range(52):
        a = rm.random() * 6.28318
        d = 0.8 + rm.random() * 0.32
        T("flamesB").disc(cxp + math.cos(a) * 150 * d,
                          cyp + math.sin(a) * 40 * d - rm.random() ** 1.7 * 52, 1, 1,
                          [A[0], A[1], A[2], A[3]][rm.randrange(4)])

    # ---------------------------------------------------------------- fore: the wood closing in
    f = T("fore")
    rf = random.Random(401)
    for _ in range(900):                                  # foreground grass in occlusion
        gx = rf.randrange(-8, WW + 8)
        gy = 300 + int(rf.random() ** 0.5 * (WH + 16 - 300))
        if ((gx - cxp) / 168.0) ** 2 + ((gy - (cyp + 6)) / 74.0) ** 2 < 1.0:
            continue
        sk.grass(f, gx, gy, 8 + (gy - 300) * 0.4, seed=rf.randrange(9999),
                 dark=OCC, light=M["stone"][4])
    _undergrowth_wide(f, rf)

    # ---------------------------------------------------------------- light, then the type
    # The wide vignette and the UI pockets are darken-only functions of position, so running them
    # on every layer separately gives exactly what running them on the composite would.
    seen = []
    for n in (STAGES if layers else ("backA",)):
        cc = cv[n]
        if any(cc is s for s in seen):
            continue
        seen.append(cc)
        splash._darken_rows(cc, 0, WH - 1, lambda x, y: max(edge_t(x, y), ui_pocket_t(x, y)),
                            [None, None, M["stone"][4], OCC], only_opaque=layers)
    sk.wordmark(T("backB"), WORD_X, WORD_Y,
                [M["skin"][0], M["skin"][0], M["skin"][1], M["skin"][2], M["skin"][3]],
                [M["gold"][0], M["gold"][1], M["gold"][2]], halo=OCC, gap=WORD_GAP,
                scales=WORD_SCALES)
    if layers:
        return cv
    return cv["backA"]


def _undergrowth_wide(f, rng):
    """Bushes and fern banked up both sides and along the bottom, and big near leaves in the
    corners - the phone's undergrowth (splash._undergrowth), laid out for a wide frame. Toned by
    how much ward light reaches each one rather than darkened afterwards; the UI pockets are
    applied later, with everything else."""
    def lit(x, y):
        e = math.sqrt(((x - WARD_X) / 230.0) ** 2 + ((y - WARD_Y) / 110.0) ** 2)
        return 1.0 - min(1.0, max(0.0, (e - 0.85) / 1.3))

    TIERS = [
        ([OCC, M["stone"][4]], [M["stone"][3], M["violet"][4]]),
        ([M["stone"][4], M["violet"][4], M["wool"][4]],
         [M["lichen"][4], M["gold"][4], M["violet"][3], M["stone"][3]]),
        ([M["stone"][3], M["violet"][3], M["wool"][4]],
         [M["lichen"][3], M["gold"][3], M["lichen"][4]]),
    ]

    def tier(x, y):
        return TIERS[min(2, int(lit(x, y) * 3.0))]

    clumps = []
    for _ in range(360):                       # banked up both sides of the wide frame
        side = -1 if rng.random() < 0.5 else 1
        u = rng.random()
        y = 210 + int(u ** 0.8 * 160)
        reach = 60 + u * 230
        x = WW // 2 + side * (WW // 2 - int(rng.random() ** 1.1 * reach)) + rng.randrange(-14, 15)
        clumps.append((x, y, 8 + u * 18))
    # Pressing in on the clearing from the left, under the menu, and closing the gap between
    # the ward and the right-hand wall - the lawn on the left of the figure was the one open
    # direction left, and an open direction is what stops a clearing feeling small.
    for _ in range(170):
        u = rng.random()
        clumps.append((C0 - 10 + int(u ** 0.9 * 200) + rng.randrange(-8, 9),
                       236 + int(rng.random() * 100) + int(u * 10), 7 + rng.random() * 13))
    for _ in range(70):
        clumps.append((WARD_X + 150 + rng.randrange(0, 110), 230 + rng.randrange(0, 100),
                       7 + rng.random() * 12))
    for _ in range(140):                       # a low bank right along the bottom
        clumps.append((rng.randrange(-10, WW + 10), 340 + rng.randrange(0, 30),
                       10 + rng.random() * 12))
    for (x, y, r) in sorted(clumps, key=lambda q: q[1]):
        if ((x - WARD_X) / 175.0) ** 2 + ((y - WARD_Y) / 70.0) ** 2 < 1.0:
            continue                             # nothing in front of the ward
        darks, lights = tier(x, y - r * 0.5)
        sk.brush(f, x, y, r, r * 0.72, seed=rng.randrange(9999),
                 dark=darks[rng.randrange(len(darks))], light=lights[rng.randrange(len(lights))],
                 density=1.6)
    base = WH + 12
    for (fx, fa, fl, fb, fs) in [(-20, -1.10, 130, 0.40, 805), (WW + 20, -2.04, 126, -0.40, 815),
                                 (C0 - 30, -1.30, 120, 0.50, 803), (C0 + 670, -1.86, 118, -0.50, 813),
                                 (40, -0.80, 110, 0.30, 801), (WW - 40, -2.36, 104, -0.30, 811),
                                 (C0 + 20, -0.95, 96, 0.60, 807), (C0 + 620, -2.20, 92, -0.60, 817),
                                 (C0 + 140, -1.05, 60, 0.40, 821), (C0 + 520, -2.10, 64, -0.40, 823)]:
        tx = fx + math.cos(fa) * fl * 0.6
        ty = base + math.sin(fa) * fl * 0.6
        lights = tier(tx, ty)[1]
        splash._frond(f, fx, base, fa, fl, fb, fs, lights[fs % len(lights)])
    fg = raster.Canvas(WW, WH)
    for (x, y, a, ln, wd, bd) in [
            (C0 - 30, 392, -0.70, 120, 40, 0.30), (C0 - 40, 370, -1.10, 100, 32, 0.45),
            (C0 + 670, 390, -2.48, 122, 40, -0.35), (C0 + 680, 366, -2.05, 96, 30, -0.45),
            (-20, 380, -0.50, 130, 44, 0.20), (WW + 20, 378, -2.64, 130, 44, -0.20),
            (C0 + 200, 404, -1.55, 64, 26, 0.30), (C0 + 300, 408, -1.68, 58, 24, -0.30),
            (C0 + 430, 402, -1.40, 66, 26, -0.25)]:
        splash._big_leaf(fg, x, y, a, ln, wd, OCC, M["stone"][4], bend=bd)
    hits = []
    for y in range(1, fg.h):
        for x in range(fg.w):
            if fg.px[x, y][3] and not fg.px[x, y - 1][3] and rng.random() < 0.85:
                hits.append((x, y))
    for (x, y) in hits:
        fg.px[x, y] = raster.rgb(M["violet"][3] if lit(x, y) > 0.3 and rng.random() < 0.5
                                 else M["stone"][3])
    f.img.paste(fg.img, (0, 0), fg.img)
    f.px = f.img.load()


def main():
    c = vigil_wide()
    bad = c.audit()
    c.save(os.path.join(splash.ROOT, SHIPPING))
    out = splash.OUT
    if not os.path.isdir(out):
        os.makedirs(out)
    c.scaled(2).save(os.path.join(out, "wide@2x.png"))
    print("vigil-wide       %s  off-contract colours: %d%s" % (
        c.img.size, len(bad), ("  " + str(bad[:4]) if bad else "")))
    print("shipped %s" % SHIPPING)


if __name__ == "__main__":
    main()
