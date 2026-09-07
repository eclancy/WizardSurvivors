# -*- coding: utf-8 -*-
"""Seven candidate title screens, authored 360x640 and rendered at x2 to fill the 720x1280
viewport exactly. Run: python tools/art/splash.py

The old title art was a 1024x1024 square dropped into a 9:16 portrait viewport with
TextureRect stretch_mode = Keep, so it was displayed at native size, anchored top-left, and
ran off the right edge with 256px of nothing under it. Authoring at half the viewport and
scaling by a whole 2 is the same rule the rest of the art now follows.

Each screen changes camera, palette emphasis and subject - not decoration. They are meant to
be genuinely choosable against each other, not seven passes at one idea.
"""

import math
import os
import shutil
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import font
import hero
import raster
import splashkit as sk

W, H = 360, 640
M = bl.MATERIALS
E = bl.ELEMENTS
OCC = bl.OCC
RIM = bl.RIM

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_splash")
# The candidate the game actually uses. Change the index in main() to switch screens.
SHIPPING = "assets/bonelight/ui/title-screen.png"
SHIPPING_PROMPT = "assets/bonelight/ui/title-prompt.png"

def sprite(rel, frame=0, cell=32, scale=2):
    img = Image.open(os.path.join(ROOT, rel)).convert("RGBA")
    f = img.crop((frame * cell, 0, frame * cell + cell, cell))
    return f.resize((cell * scale, cell * scale), Image.NEAREST)

def mist(c, y0, y1, color, density=0.18, seed=1):
    """Horizontal haze. Sparse single pixels rather than an alpha wash, because the palette
    has no intermediate values to wash with."""
    rng = random.Random(seed)
    for y in range(int(y0), int(y1)):
        t = 1.0 - abs((y - y0) / float(max(1, y1 - y0)) - 0.5) * 2.0
        for x in range(W):
            if rng.random() < density * t:
                c.set(x, y, color)

def eyes_in_the_dark(c, y0, y1, n, seed, ramp=None, avoid=None):
    """Pairs of lit eyes at the edge of the light. Cheapest possible way to say 'there are
    more of them out there' without drawing more of them.

    `avoid` is an (x0, y0, x1, y1) box they will not be placed in - the corners of the frame
    are where a stray pair is most conspicuous and least useful.
    """
    ramp = ramp or E["fire"]
    rng = random.Random(seed)
    placed = 0
    while placed < n:
        x = rng.randrange(12, W - 20)
        y = rng.randrange(int(y0), int(y1))
        if avoid and avoid[0] <= x <= avoid[2] and avoid[1] <= y <= avoid[3]:
            continue
        placed += 1
        gap = rng.randrange(3, 6)
        for ox in (0, gap):
            c.radial(x + ox, y, 3, 3, [ramp[1], ramp[2], ramp[3], None])
            c.set(x + ox, y, ramp[0])

# ---------------------------------------------------------------- 1. VIGIL
def vigil(beard="mane-spear", ward=None, ground=None, wood="open"):
    """Wide, quiet, bottom-heavy. You, from behind, inside a ring of lit wards, in the beat
    before it starts. Sells preparation rather than the fight - and it is the only screen
    where the player character is the largest thing on frame."""
    # The colour of this wizard magic, in one place. Light rather than Arcane: see the
    # ward block below for why the purple lost.
    WARD = ward or E["light"]
    # The pigment row the ward's light comes up in on the floor and inside each flame's bloom.
    # It is a MATERIAL row, never an element ramp - see the pools below - but it has to be
    # picked to agree with WARD. Swap one without the other and the magic is one colour while
    # the floor it is lighting is another.
    GROUND = ground or M["gold"]
    # How closed-in the wood is. Three different levers, not one slider: fill the base of the
    # trees so the trunks come out of a hedge (thicket), put a second rank of nearer trunks
    # behind the clearing so there is no line of sight out (rank), or bring the near walls in
    # from the frame edges so the clearing is narrower (walls). They do not stack - two of
    # them at once fills the frame with black and the wood stops having depth.
    #
    # Note where the useful band is. The flagstone floor is drawn AFTER all of this from
    # y=428 down, so undergrowth below that line is painted over and wasted; everything here
    # aims at roughly y 388-428.
    # Near-blacks with different hue casts, all already in the contract. A dark mass painted
    # in one colour is a hole; the same mass painted in six near-blacks that differ by a few
    # points of hue reads as depth, because the eye takes the variation as things at different
    # distances rather than as noise. FAR is a step lighter and cooler than NEAR - atmospheric
    # perspective, which is the only depth cue left once everything in shot is silhouette.
    FAR_TONES = [(M["stone"][4], M["stone"][3]), (M["violet"][4], M["violet"][3]),
                 (M["stone"][4], M["lichen"][3]), (M["wool"][4], M["stone"][3]),
                 (M["violet"][4], M["stone"][3]), (M["arcane"][4], M["stone"][3])]
    NEAR_TONES = [(OCC, M["stone"][4]), (OCC, M["violet"][4]), (M["violet"][4], M["stone"][4]),
                  (OCC, M["lichen"][4]), (M["wool"][4], M["stone"][4]), (OCC, M["stone"][4])]
    # The last few units before the ward. Flat silhouette, no lit face at all - the nearest
    # plane in the frame is the one thing that gets no light, which is what pushes it in front
    # of everything else.
    FLOOR_TONES = [(OCC, OCC), (OCC, OCC), (OCC, M["stone"][4]), (M["violet"][4], OCC)]

    WOODS = {
        "current": dict(under=120, ur=(5, 17), uy=(414, 26), ud=1.2,
                        leaf=150, lr=(6, 20), ly=(300, 140),
                        roots=0, rank=0, side=90, side_in=128, canopy_d=1.25,
                        near=[(-14, 470, 176, 71), (378, 442, 164, 83)],
                        vary=False, creep=0, reach=0, saplings=[],
                        cedge=0.55, cnear=64, cfar=148, keep=(172, 84, 570), cbias=0.6,
                        fscale=1.0, uh=0.5, lh=0.62, crad=(13, 13), csq=(0.55, 0.55),
                        humps=0),
        # The one that ships. It is "current" - the open composition, which is the one that
        # had depth - plus exactly two things: the near-black hue variation, which costs no
        # density at all, and a couple of dozen taller clumps to stop the horizon being a
        # ruled line. No creep. Closing the clearing in is what flattened it: with the wood
        # brought down onto the lawn there was no distance left between the trees and him
        # for the eye to read.
        "open": dict(under=205, ur=(6, 19), uy=(408, 30), ud=1.3,
                     leaf=175, lr=(6, 21), ly=(300, 142),
                     roots=55, rank=0, side=95, side_in=128, canopy_d=1.25,
                     near=[(-14, 470, 176, 71), (378, 442, 164, 83)],
                     vary=True, creep=0, reach=0, saplings=[],
                     cedge=0.55, cnear=64, cfar=148, keep=(172, 84, 570), cbias=0.6,
                     fscale=1.0, uh=0.62, lh=0.62, crad=(13, 13), csq=(0.55, 0.55),
                     humps=26),
        "thicket": dict(under=360, ur=(11, 30), uy=(378, 48), ud=1.7,
                        leaf=250, lr=(10, 29), ly=(302, 142),
                        roots=195, rank=0, side=150, side_in=116, canopy_d=1.28,
                        near=[(-14, 470, 176, 71), (378, 442, 164, 83)],
                        vary=True, creep=560, reach=176,
                        cedge=1.0, cnear=152, cfar=74, keep=(168, 70, 572), cbias=1.9,
                        fscale=1.24, uh=0.78, lh=0.92, crad=(26, 5), csq=(0.95, 0.22),
                        humps=0,
                        # short at the front, taller the further back they stand
                        saplings=[(24, 500, 40, 26, 211), (80, 478, 60, 38, 223),
                                  (128, 456, 92, 56, 251), (236, 458, 88, 54, 257),
                                  (296, 480, 56, 36, 263), (338, 502, 38, 24, 269)]),
        "rank": dict(under=210, ur=(7, 21), uy=(404, 32), ud=1.45,
                     leaf=200, lr=(7, 22), ly=(298, 146),
                     roots=95, rank=11, side=100, side_in=126, canopy_d=1.25,
                     near=[(-14, 470, 176, 71), (378, 442, 164, 83)],
                     vary=True, creep=115, reach=84,
                     cedge=0.55, cnear=64, cfar=148, keep=(172, 84, 570), cbias=0.6,
                     fscale=1.0, uh=0.5, lh=0.62, crad=(13, 13), csq=(0.55, 0.55),
                     humps=0,
                     saplings=[(42, 458, 84, 52, 227), (320, 472, 92, 56, 229)]),
        "walls": dict(under=180, ur=(6, 19), uy=(400, 34), ud=1.35,
                      leaf=205, lr=(7, 21), ly=(298, 146),
                      roots=80, rank=0, side=185, side_in=98, canopy_d=1.4,
                      near=[(4, 500, 190, 71), (356, 470, 178, 83),
                            (-34, 424, 150, 91), (400, 404, 142, 97)],
                      vary=True, creep=185, reach=116,
                      cedge=0.55, cnear=64, cfar=148, keep=(172, 84, 570), cbias=0.6,
                      fscale=1.0, uh=0.5, lh=0.62, crad=(13, 13), csq=(0.55, 0.55),
                      humps=0,
                      saplings=[(36, 480, 104, 62, 233), (328, 488, 98, 58, 239),
                                (86, 452, 66, 40, 241)]),
    }
    wd = WOODS[wood]
    _tone_rng = random.Random(9631)

    def tone(pool, fallback):
        """One (dark, light) pair per clump. Presets that do not vary keep the old fixed pair."""
        return pool[_tone_rng.randrange(len(pool))] if wd["vary"] else fallback

    c = raster.Canvas(W, H, OCC)
    # Brighter than it was, and brightest in the middle band rather than at the top: the
    # trees are near-black silhouettes and they need something behind them to be seen
    # against, which the old near-occlusion sky was not giving them.
    c.vramp(0, 430, [M["stone"][4], M["violet"][4], M["violet"][3], M["violet"][2],
                     M["stone"][3]])
    sk.starfield(c, 8, 260, 80, 11)

    # The wood, in two depths. Far trees are stone shade against the sky and stay clear of the
    # centre so the wordmark and the figure both keep their air; the near pair at the frame
    # edges is drawn later, in flat occlusion, and closes the canopy over the top corners.
    rng = random.Random(17)
    far = [(26, 254, 116, 3), (322, 236, 108, 8), (352, 176, 88, 12), (68, 152, 84, 5),
           (280, 146, 80, 19), (212, 120, 70, 23), (110, 108, 64, 31), (248, 88, 56, 37),
           (146, 74, 50, 41), (184, 60, 42, 47), (2, 168, 84, 53), (356, 132, 72, 59),
           (128, 96, 58, 61), (300, 104, 62, 67), (44, 118, 66, 73), (166, 88, 54, 79),
           (232, 156, 82, 89), (94, 140, 76, 97)]
    for (tx, th, sp, sd) in sorted(far, key=lambda t: -t[1]):
        td, tl = tone(FAR_TONES, (M["stone"][4], M["stone"][3]))
        sk.tree(c, tx, 436, th * wd["fscale"], sp * wd["fscale"], seed=sd,
                dark=td, light=tl, density=2.2)
    # A second rank, nearer and a step darker, standing on a lower baseline so it reads as
    # in front of the first. Trunks are what close a sightline; leaves only muffle it.
    for i in range(wd["rank"]):
        rx0 = -10 + i * (W + 20) // max(1, wd["rank"] - 1)
        rh = 118 + ((i * 37) % 46)
        td, tl = tone(NEAR_TONES, (OCC, M["stone"][4]))
        sk.tree(c, rx0 + ((i * 53) % 19) - 9, 448, rh, rh * 0.62, seed=401 + i * 13,
                dark=td, light=tl, density=2.0)
    # Undergrowth closing the gap between the trunks and the ground. Without it the wood ends
    # in a clean line of bare stems and the clearing reads as a park.
    rb = random.Random(211)
    for _ in range(wd["under"]):
        bx = rb.randrange(-12, W + 12)
        by = wd["uy"][0] + rb.randrange(0, wd["uy"][1])
        br = rb.randrange(*wd["ur"])
        td, tl = tone(FAR_TONES, (M["stone"][4], M["stone"][3]))
        sk.brush(c, bx, by, br, br * wd["uh"], seed=rb.randrange(9999),
                 dark=td, light=tl, density=wd["ud"])
    # A few noticeably taller clumps along the treeline. Raising the whole undergrowth band
    # only moves the flat line up; what breaks a horizon is a handful of things standing above
    # the rest of it. Twenty-odd is enough - this is silhouette variation, not density, and
    # filling the band is what cost the depth last time.
    for _ in range(wd["humps"]):
        bx = rb.randrange(-10, W + 10)
        by = 412 + rb.randrange(0, 16)
        br = rb.randrange(16, 31)
        td, tl = tone(FAR_TONES, (M["stone"][4], M["stone"][3]))
        sk.brush(c, bx, by, br, br * 0.85, seed=rb.randrange(9999),
                 dark=td, light=tl, density=1.25)
    # Near-black at the very bottom of the trunks, on top of the lit undergrowth. This is
    # what makes the wood read as having no floor you could walk out across.
    for _ in range(wd["roots"]):
        bx = rb.randrange(-14, W + 14)
        by = 408 + rb.randrange(0, 24)
        br = rb.randrange(8, 22)
        td, tl = tone(NEAR_TONES, (OCC, M["stone"][4]))
        sk.brush(c, bx, by, br, br * 0.55, seed=rb.randrange(9999),
                 dark=td, light=tl, density=1.5)
    # A second, darker layer of leaf mass down in the wood. The trunks were reading as
    # separate objects with sky between them; this fills the gaps so it reads as depth.
    for _ in range(wd["leaf"]):
        bx = rb.randrange(-16, W + 16)
        by = wd["ly"][0] + int(rb.random() ** 0.7 * wd["ly"][1])
        br = rb.randrange(*wd["lr"])
        td, tl = tone(NEAR_TONES, (OCC, M["stone"][4]))
        sk.brush(c, bx, by, br, br * wd["lh"], seed=rb.randrange(9999),
                 dark=td, light=tl, density=1.15)
    eyes_in_the_dark(c, 330, 450, 9, 5,
                     [E["fire"][2], E["fire"][3], E["fire"][3], None])
    mist(c, 396, 452, M["stone"][3], 0.10, 4)

    # ground: flagstone bands that widen toward the camera
    y = 428
    step = 5
    while y < H:
        hh = min(step, H - y)
        t = (y - 428) / float(H - 428)
        c.rect(0, y, W - 1, y + hh, M["stone"][3] if t < 0.35 else M["stone"][2])
        c.hline(0, W - 1, y, M["stone"][4])
        for _ in range(int(5 + t * 26)):
            c.set(rng.randrange(W), y + rng.randrange(1, max(2, hh)),
                  M["stone"][1] if rng.random() < 0.35 else M["stone"][4])
        y += hh
        step += 1

    # Grass, thickening toward the camera because the blades subtend more of the frame as
    # they get nearer. Thinned inside the ward circle so the runes still read through it.
    rg = random.Random(307)
    for _ in range(1050):
        gx = rg.randrange(-6, W + 6)
        gy2 = 434 + int(rg.random() ** 0.75 * (H - 434))
        t = (gy2 - 434) / float(H - 434)
        inside = ((gx - 180) / 170.0) ** 2 + ((gy2 - 568) / 54.0) ** 2 < 1.0
        if inside and rg.random() < 0.97:
            continue
        sk.grass(c, gx, gy2, 3 + t * 9, seed=rg.randrange(9999),
                 dark=M["lichen"][4] if rg.random() < 0.7 else M["stone"][4],
                 light=M["lichen"][3] if t < 0.6 else M["lichen"][2])

    # Brush banked up the left and right edges of the clearing floor, so the ground is walled
    # in as well as roofed in. Without it the sides of the frame are the one open direction
    # left and the whole enclosure leaks out of them.
    for _ in range(wd["side"]):
        side = -1 if rg.random() < 0.5 else 1
        gx = 180 + side * (wd["side_in"] + rg.randrange(0, 70))
        gy2 = 440 + int(rg.random() ** 0.8 * 200)
        br = rg.randrange(7, 22)
        td, tl = tone(NEAR_TONES, (OCC, M["stone"][4]))
        sk.brush(c, gx, gy2, br, br * 0.6, seed=rg.randrange(9999),
                 dark=td, light=tl, density=1.3)

    # Foliage creeping down out of the treeline and onto the grass, toward him. Everything in
    # the treeline block above is painted over by the flagstone floor, which is drawn after it,
    # so encroachment has to be its own pass HERE - after the floor and the grass. This is the
    # only foliage in the frame actually standing on the lawn.
    #
    # It thins toward the middle on purpose. A band of equal density straight across would wall
    # him off; leaving a corridor open down the centre is what makes the sides read as closing
    # IN rather than as a hedge parked behind him.
    for _ in range(wd["creep"]):
        # cbias BELOW 1 pushes t toward 1, which puts most clumps at the BOTTOM of the reach -
        # down where the ward exclusion then throws them away, so the middle of the band stays
        # bare however high the count goes. Above 1 pushes them up against the treeline, which
        # is above the keep-out and is the only way the band actually fills across.
        t = rg.random() ** wd["cbias"]
        gy2 = 430 + int(t * wd["reach"])
        # cedge below 1 biases toward the frame edges and leaves a corridor down the middle;
        # at 1 the spread is flat and the band fills right across. That one exponent is the
        # difference between "the wood is closing in from the sides" and "the wood has taken
        # the clearing back".
        edge = abs(rg.random() * 2.0 - 1.0) ** wd["cedge"]
        gx = 180 + (1 if rg.random() < 0.5 else -1) * int(edge * (wd["cnear"] + t * wd["cfar"]))
        ex, ey, ec = wd["keep"]
        if ((gx - 180) / float(ex)) ** 2 + ((gy2 - ec) / float(ey)) ** 2 < 1.0:
            continue                               # the ward keeps its clearance
        # Big and standing up at the treeline, small and lying flat by the time it reaches
        # him. This is the whole depth cue: a band of same-sized clumps from the wood to the
        # ward reads as gravel however many of them there are, because nothing in it tells the
        # eye which end is further away. Size does that, and the height-to-width ratio does it
        # again - far mass stands up, near scrub hugs the ground.
        fr, nr = wd["crad"]
        fs, ns = wd["csq"]
        br = int((fr + (nr - fr) * t) * (0.72 + rg.random() * 0.56))
        sq = fs + (ns - fs) * t
        # ...and its POOL by depth as well. Size alone is not enough: a clump at the treeline
        # drawn from the same near-blacks as one at his feet reads as the same distance away
        # however small it is. Far mass gets the lighter, cooler pool, the middle gets the
        # near one, and the last stretch is flat occlusion.
        if t < 0.30 + rg.random() * 0.16:
            pool = FAR_TONES
        elif t < 0.70 + rg.random() * 0.12:
            pool = NEAR_TONES
        else:
            pool = FLOOR_TONES
        td, tl = tone(pool, (OCC, M["stone"][4]))
        sk.brush(c, gx, gy2, br, max(2, br * sq), seed=rg.randrange(9999),
                 dark=td, light=tl, density=1.4)
    # and a few saplings actually rooted on the grass, so the encroachment has stems in it
    for (sx0, sy0, sh, ssp, ssd) in wd["saplings"]:
        td, tl = tone(NEAR_TONES, (OCC, M["stone"][4]))
        sk.tree(c, sx0, sy0, sh, ssp, seed=ssd, dark=td, light=tl, density=1.7)

    # scattered rocks, so the clearing has a floor rather than a backdrop
    for (bx, by, bw, bh, bs) in [(46, 470, 30, 9, 2), (296, 462, 22, 7, 6),
                                 (338, 500, 34, 11, 9), (18, 520, 26, 8, 13)]:
        sk.boulder(c, bx, by, bw, bh, seed=bs)

    # The near pair, in flat occlusion so they read as being between us and everything else.
    # They stand off the edges of the frame and are mostly cropped, which is the point: you
    # are looking out at the clearing from inside the wood.
    for (tx, th, sp, sd) in wd["near"]:
        sk.tree(c, tx, 486, th, sp, seed=sd, dark=OCC, light=M["stone"][4], density=1.9)

    # Boughs reaching in from off the top corners, then a canopy of loose leaf mass filling
    # the sky around them. CLEAR is the one hole left in it - the break you are looking up
    # through - and it is sized and placed to hold the wordmark, which does not move.
    CLEAR = (180, 108, 150, 66)
    for (bx, by, ba, bl, bw, bs) in [(-18, 26, 0.30, 62, 5.0, 101), (-10, -12, 0.62, 56, 4.4, 107),
                                     (376, 34, 2.84, 60, 5.0, 113), (368, -8, 2.52, 54, 4.4, 127),
                                     (-16, 150, -0.22, 52, 3.6, 131), (374, 158, 3.36, 50, 3.6, 137),
                                     (-14, 250, -0.55, 46, 3.2, 139), (372, 262, 3.70, 44, 3.2, 149),
                                     (-12, 348, -0.80, 40, 2.8, 157), (374, 356, 3.95, 38, 2.8, 163)]:
        sk.bough(c, bx, by, ba, bl, bw, seed=bs, dark=OCC, light=M["stone"][4],
                 leaf_r=17, density=1.6)
    sk.canopy(c, 0, 400, CLEAR, 250, seed=151, dark=OCC, light=M["stone"][4], width=W,
              density=wd["canopy_d"])
    # A second set of eyes up in the branches, after the canopy - drawn before it the leaf mass
    # buries them. Things above him as well as around him.
    eyes_in_the_dark(c, 172, 330, 7, 71,
                     [E["fire"][2], E["fire"][3], E["fire"][3], None],
                     avoid=(0, 0, 148, 268))

    # The ward circle. It is the second light source in the frame after the orb, and it is
    # what the composition is actually about, so it burns on an ELEMENT ramp - the same one
    # the orb uses - rather than on a material row. Material tones are pigment; this is meant
    # to be emitting, and on the material row it was barely visible.
    #
    # WARD is Light, not Arcane. The world is cold blue-grey stone and violet, so a purple
    # ward sat in the same hue family as everything around it and had to shout to be seen;
    # a warm gold-white one is the only high-contrast option the palette offers, and it
    # reads as protective rather than merely magical. Change this one name and the orb, the
    # rings, the motes and the light on the figure all move together.
    #
    # 568, not 578: the ward got much brighter, and at the old height its lower arc ran
    # straight through the PRESS ANY KEY band at rows 616-636.
    cxp, cyp = 180, 568
    A = WARD
    # The light it throws on the ground, as three graduated pools. Painting this straight on
    # the element ramp filled the floor with a flat slab of saturated colour that read as a
    # rug rather than as light: what the ward lands on is stone, so pigment tones come up
    # under it - GROUND, picked to match the light - and only the rings themselves emit.
    c.radial(cxp, cyp, 208, 62, [GROUND[4], GROUND[4], None])
    c.radial(cxp, cyp, 170, 47, [GROUND[3], GROUND[4], None])
    c.radial(cxp, cyp, 122, 33, [GROUND[2], GROUND[3], None])
    c.ring(cxp, cyp, 152, 41, A[3], 2)
    c.ring(cxp, cyp, 146, 39, A[2], 2)
    c.ring(cxp, cyp, 128, 33, A[3], 1)
    c.ring(cxp, cyp, 96, 25, A[3], 1)
    star = []
    for i in range(6):
        a = i * 6.28318 / 6.0 - 1.5708
        star.append((cxp + math.cos(a) * 128, cyp + math.sin(a) * 33))
    for i in range(6):                                    # two triangles, one hexagram
        c.line(star[i][0], star[i][1], star[(i + 2) % 6][0], star[(i + 2) % 6][1], A[2])
    # The six points of the star each burn. These were small radial lamps and read as tealights;
    # the ward is supposed to be holding something off, so they are flames now, z-sorted around
    # the figure like the candles used to be so the far side does not stand in front of him.
    def ward_flames(front):
        for i, (nx, ny) in enumerate(star):
            depth = (ny - cyp) / 33.0
            if (depth > -0.1) != front:
                continue
            sk.flame(c, nx, ny + 1, 29 + depth * 9, A, seed=311 + i * 7,
                     halo=GROUND)
    for i in range(14):                                   # rune ticks around the outer band
        a = i * 6.28318 / 14.0 + 0.22
        rx0 = cxp + math.cos(a) * 139
        ry0 = cyp + math.sin(a) * 36
        c.radial(rx0, ry0, 7, 6, [A[2], A[3], None])
        c.vline(int(rx0), ry0 - 3, ry0 + 3, A[1])
        c.hline(rx0 - 2, rx0 + 2, ry0 - 1, A[1])
        c.set(int(rx0), int(ry0), A[0])

    # No candles on the ring any more. Nine orange flames around a gold-white ward put two
    # warm light sources in the same place competing for the same job, and the ward is now
    # bright enough to do it alone. The only fire left on frame is in the treeline.
    ward_flames(False)
    hero.wizard_hero(c, cxp, cyp, h=150, staff_ramp=A, under_ramp=A, beard=beard)
    ward_flames(True)

    # motes lifting off the ring, after the figure so they drift in front of him too
    rm = random.Random(509)
    for _ in range(52):
        a = rm.random() * 6.28318
        d = 0.8 + rm.random() * 0.32
        c.disc(cxp + math.cos(a) * 150 * d,
               cyp + math.sin(a) * 40 * d - rm.random() ** 1.7 * 52, 1, 1,
               [A[0], A[1], A[2], A[3]][rm.randrange(4)])

    # Grass in front of him, in flat occlusion. It is the closest thing in the frame, so it
    # gets no light at all - and a foreground plane the figure sits behind is what turns a
    # backdrop into somewhere he is standing.
    rf = random.Random(401)
    for _ in range(430):
        gx = rf.randrange(-8, W + 8)
        gy2 = 582 + int(rf.random() ** 0.5 * (H + 16 - 582))
        # Nothing in front of the ward. The foreground grass used to run straight across the
        # near arc of the circle, which put the brightest thing in the frame behind a hedge.
        if ((gx - 180) / 168.0) ** 2 + ((gy2 - 574) / 74.0) ** 2 < 1.0:
            continue
        sk.grass(c, gx, gy2, 10 + (gy2 - 582) * 0.44, seed=rf.randrange(9999),
                 dark=OCC, light=M["stone"][4])
    for _ in range(70):
        side = -1 if rf.random() < 0.5 else 1
        gx = 180 + side * (150 + rf.randrange(0, 62))
        gy2 = 556 + int(rf.random() ** 0.7 * 100)
        # the hexagram runs to x 52..308, and brush over its points hid two of the six
        if ((gx - 180) / 172.0) ** 2 + ((gy2 - 574) / 76.0) ** 2 < 1.0:
            continue
        br = rf.randrange(9, 26)
        sk.brush(c, gx, gy2, br, br * 0.55, seed=rf.randrange(9999),
                 dark=OCC, light=M["stone"][4], density=1.4)

    # The vignette is scene light, so it runs before the type, never after. Running it
    # last dithered frame-edge darkening straight over the wordmark, which is what made
    # two of these screens unreadable.
    c.vignette([None, None, M["stone"][4], OCC], 2.2)
    sk.wordmark(c, 180, 78,
                [M["skin"][0], M["skin"][0], M["skin"][1], M["skin"][2], M["skin"][3]],
                [M["gold"][0], M["gold"][1], M["gold"][2]],
                halo=OCC)
    # No PRESS ANY KEY baked in. It ships as its own transparent texture (prompt_asset below)
    # so the scene can fade and breathe it, which a pixel painted into the background cannot do.
    return c, "vigil", "Vigil"

# ---------------------------------------------------------- 2. BONELIGHT MOON
def bonelight_moon():
    """A poster, not a scene: one huge light source, one figure, one ridge. Fewest colours of
    the seven and the highest value contrast - the direction's name, drawn."""
    c = raster.Canvas(W, H, OCC)
    c.vramp(0, 500, [OCC, M["stone"][4], M["wool"][4], M["violet"][4], M["stone"][4], OCC])
    sk.starfield(c, 6, 420, 130, 3)
    sk.moon(c, 180, 262, 108)

    ridge = []
    rng = random.Random(9)
    yb = 452
    for x in range(0, W + 8, 8):
        yb += rng.randrange(-3, 4)
        yb = max(440, min(468, yb))
        ridge.append((x, yb))
    c.poly(ridge + [(W, H), (0, H)], OCC)

    def ridge_y(x):
        return ridge[max(0, min(len(ridge) - 1, int(x) // 8))][1]

    rng2 = random.Random(21)
    for i in range(17):
        x = 10 + i * 21 + rng2.randrange(-4, 5)
        if abs(x - 180) < 30:
            continue
        sk.enemy_sil(c, x, ridge_y(x) + 4, sk.ENEMY_KINDS[i % 4],
                     20 + rng2.randrange(0, 8), OCC, rim=M["skin"][3])

    sk.wizard_sil(c, 178, 474, 196, OCC, rim=RIM, staff_ramp=E["arcane"])
    mist(c, 452, 486, M["stone"][3], 0.12, 8)

    # The vignette is scene light, so it runs before the type, never after. Running it
    # last dithered frame-edge darkening straight over the wordmark, which is what made
    # two of these screens unreadable.
    c.vignette([None, None, None, OCC], 2.6)
    sk.wordmark(c, 180, 512,
                [M["skin"][0], M["skin"][0], M["skin"][1], M["skin"][2]],
                [M["skin"][2], M["skin"][3]],
                halo=OCC)
    sk.caption(c, "PRESS ANY KEY", 180, 616, M["skin"][3], 2, 1)
    return c, "bonelight-moon", "Bonelight Moon"

# ------------------------------------------------------------- 3. ENCIRCLED
def encircled():
    """The game's own camera, using the game's own shipping sprites at their shipping scale.
    The only screen that promises exactly what the first thirty seconds look like."""
    c = raster.Canvas(W, H, OCC)
    sk.stone_floor(c, 0, H - 1, 32, 4)
    # knock the flagstone contrast down: at full variance the floor reads as a chessboard and
    # competes with the actors it is supposed to sit behind
    c.vignette([None, M["stone"][4]], 0.9)

    px, py = 180, 340
    c.radial(px, py, 104, 104, [None, None, M["arcane"][4], M["arcane"][4], None])
    c.ring(px, py, 78, 78, M["arcane"][3], 2)
    c.ring(px, py, 62, 62, M["arcane"][4], 1)

    rng = random.Random(77)
    kinds = ["swarmer", "runner", "bruiser", "shielder"]
    frames = dict((k, sprite("assets/bonelight/enemies/" + k + "-moving.png", 0)) for k in kinds)
    frames["sentry"] = sprite("assets/bonelight/enemies/skullsentry-moving.png", 0)

    ring = []
    for i in range(30):
        a = i * 6.28318 / 30.0 + rng.random() * 0.14
        r = 96 + rng.randrange(0, 96)
        k = kinds[i % 4] if i % 8 else "sentry"
        ey = int(py + math.sin(a) * r * 1.5)
        # Nothing in the type bands: a sprite clipped by the frame edge behind the
        # prompt reads as a rendering fault rather than as a crowd.
        ex = int(px + math.cos(a) * r * 1.05)
        if ey > 552 or ey < 154 or ex < 34 or ex > W - 34:
            continue
        ring.append((ex, ey, k))
    for (ex, ey, k) in sorted(ring, key=lambda t: t[1]):
        c.disc(ex, ey + 26, 17, 5, OCC)
        c.blit(frames[k], ex - 32, ey - 32)

    c.disc(px, py + 24, 20, 6, OCC)
    c.blit(sprite("assets/bonelight/characters/wizard-idle-1.png", 0), px - 32, py - 34)

    for (mx, my, ang) in [(236, 302, 0.35), (112, 286, 3.6), (198, 206, -1.35),
                          (150, 420, 2.3)]:
        for t in range(16):
            c.set(int(mx + math.cos(ang) * t * 3), int(my + math.sin(ang) * t * 3),
                  E["arcane"][3] if t > 7 else E["arcane"][2])
        c.radial(mx, my, 8, 8, [E["arcane"][0], E["arcane"][1], E["arcane"][2], None])

    c.vramp(0, 154, [OCC, OCC, M["stone"][4], None])
    c.vramp(566, 639, [None, M["stone"][4], OCC])
    # The vignette is scene light, so it runs before the type, never after. Running it
    # last dithered frame-edge darkening straight over the wordmark, which is what made
    # two of these screens unreadable.
    c.vignette([None, None, M["stone"][4], OCC], 1.9)
    sk.wordmark(c, 180, 34,
                [M["skin"][0], M["skin"][0], M["skin"][1], M["skin"][2]],
                [E["arcane"][1], E["arcane"][2]],
                halo=OCC)
    sk.caption(c, "PRESS ANY KEY", 180, 608, M["skin"][2], 2, 1)
    return c, "encircled", "Encircled"

# ----------------------------------------------------------- 4. SANCTUM GATE
def sanctum_gate():
    """Architecture instead of a character. Symmetrical, still, backlit - the only screen
    where the wizard is a hole in the light rather than a lit object."""
    c = raster.Canvas(W, H, M["stone"][4])
    rng = random.Random(13)
    bh, bw = 16, 40
    for row in range(0, H // bh + 1):
        off = (bw // 2) if row % 2 else 0
        for col in range(-1, W // bw + 2):
            x0, y0 = col * bw + off, row * bh
            v = rng.random()
            c.rect(x0 + 1, y0 + 1, x0 + bw - 2, y0 + bh - 2,
                   M["stone"][3] if v < 0.7 else M["stone"][4])
            c.hline(x0, x0 + bw - 1, y0, M["stone"][4])
            c.vline(x0, y0, y0 + bh - 1, M["stone"][4])
            c.hline(x0 + 1, x0 + bw - 2, y0 + 1, M["stone"][2])
    for _ in range(900):
        x = rng.randrange(W)
        y = rng.randrange(320, H)
        if rng.random() < ((y - 320) / 320.0) * (1.0 - abs(x - 180) / 200.0 * 0.7):
            c.set(x, y, M["lichen"][3] if rng.random() < 0.5 else M["lichen"][4])

    ax, ay, aw, ah = 180, 380, 96, 118

    c.rect(ax - aw, ay, ax + aw, 566, M["arcane"][4])
    c.disc(ax, ay, aw, ah, M["arcane"][4])
    c.rect(ax - aw + 16, ay + 20, ax + aw - 16, 566, M["arcane"][3])
    c.disc(ax, ay + 10, aw - 16, ah - 16, M["arcane"][3])
    c.radial(ax, ay + 44, aw - 26, 190,
             [E["arcane"][1], E["arcane"][2], E["arcane"][3], M["arcane"][3], None])
    sk.wizard_sil(c, ax + 2, 562, 158, OCC)

    for t in range(0, 181, 6):
        a = math.radians(t)
        for r in range(0, 26):
            c.set(int(ax - math.cos(a) * (aw + r)), int(ay - math.sin(a) * (ah + r)),
                  M["stone"][2] if (t // 6) % 2 else M["stone"][3])
    for t in range(0, 181, 6):
        a = math.radians(t)
        c.set(int(ax - math.cos(a) * (aw + 26)), int(ay - math.sin(a) * (ah + 26)), M["stone"][1])
        c.set(int(ax - math.cos(a) * aw), int(ay - math.sin(a) * ah), OCC)
    c.rect(ax - aw - 26, ay, ax - aw - 1, 580, M["stone"][3])
    c.rect(ax + aw + 1, ay, ax + aw + 26, 580, M["stone"][4])
    c.vline(ax - aw - 26, ay, 580, M["stone"][1])
    c.vline(ax - aw - 1, ay, 580, OCC)
    c.vline(ax + aw + 1, ay, 580, OCC)
    for i, yy in enumerate(range(566, 606, 10)):
        c.rect(ax - aw - 26 - i * 16, yy, ax + aw + 26 + i * 16, yy + 9,
               M["stone"][3 if i % 2 else 2])
        c.hline(ax - aw - 26 - i * 16, ax + aw + 26 + i * 16, yy, M["stone"][1])

    sk.brazier(c, 38, 484, 42)
    sk.brazier(c, 322, 484, 42)

    # the lintel: corbels under it and a cast shadow below, so it is built into the wall
    # rather than floating in front of it
    c.rect(20, 236, 339, 250, OCC)
    for x in (26, 180, 333):
        c.poly([(x - 12, 232), (x + 12, 232), (x + 7, 250), (x - 7, 250)], M["stone"][4])
    c.poly([(18, 146), (341, 146), (341, 236), (18, 236)], M["stone"][3])
    c.poly([(24, 152), (335, 152), (335, 230), (24, 230)], M["stone"][2])
    c.hline(24, 335, 152, M["stone"][1])
    c.vline(24, 152, 230, M["stone"][1])
    c.hline(18, 341, 237, OCC)
    for _ in range(260):
        c.set(rng.randrange(24, 336), rng.randrange(152, 231),
              M["stone"][3] if rng.random() < 0.6 else M["stone"][1])
    # The vignette is scene light, so it runs before the type, never after. Running it
    # last dithered frame-edge darkening straight over the wordmark, which is what made
    # two of these screens unreadable.
    c.vignette([None, None, M["stone"][4], OCC], 2.0)
    sk.wordmark(c, 180, 160,
                [M["gold"][0], M["gold"][1], M["gold"][1], M["gold"][2], M["gold"][3]],
                [M["stone"][0], M["skin"][3]],
                shadow=OCC)
    c.vramp(598, 639, [None, OCC])
    sk.caption(c, "PRESS ANY KEY", 180, 614, M["gold"][0], 2, 1)
    return c, "sanctum-gate", "Sanctum Gate"

# ---------------------------------------------------------- 5. TWELVE SIGILS
def twelve_sigils():
    """The colour-forward one. All twelve element ramps on frame at once against near-black,
    which is the direction's central claim - a dark game that is not a desaturated one -
    stated as loudly as it can be stated."""
    c = raster.Canvas(W, H, OCC)
    c.vramp(0, H - 1, [OCC, M["violet"][4], M["violet"][3], M["violet"][4], OCC])
    sk.starfield(c, 0, H, 60, 31, [M["violet"][1], M["arcane"][2]])

    cx, cy = 180, 318
    c.radial(cx, cy, 172, 172, [None, M["violet"][3], M["violet"][4], None])
    c.ring(cx, cy, 128, 128, M["violet"][2], 1)
    c.ring(cx, cy, 80, 80, M["violet"][3], 1)

    pos = []
    for i in range(12):
        a = -1.5708 + i * 6.28318 / 12.0
        pos.append((cx + math.cos(a) * 128, cy + math.sin(a) * 128))
    for i in range(12):
        c.line(pos[i][0], pos[i][1], pos[(i + 5) % 12][0], pos[(i + 5) % 12][1],
               M["violet"][3])
    for i in range(12):
        sk.sigil(c, pos[i][0], pos[i][1], i, scale=3, glow=True, bright=1)

    c.radial(cx, cy, 64, 44, [E["arcane"][3], M["violet"][3], M["violet"][4], None])
    c.poly([(cx - 62, cy), (cx, cy - 31), (cx + 62, cy), (cx, cy + 31)], M["violet"][4])
    c.poly([(cx - 54, cy), (cx, cy - 26), (cx + 54, cy), (cx, cy + 26)], E["arcane"][3])
    c.disc(cx, cy, 24, 24, E["arcane"][2])
    c.disc(cx, cy, 15, 15, E["arcane"][1])
    c.disc(cx - 3, cy - 3, 7, 7, E["arcane"][0])
    c.poly([(cx - 62, cy), (cx, cy - 31), (cx + 62, cy)], None)
    c.line(cx - 62, cy, cx, cy - 31, M["violet"][1])
    c.line(cx + 62, cy, cx, cy - 31, M["violet"][1])
    c.line(cx - 62, cy, cx, cy + 31, M["violet"][2])
    c.line(cx + 62, cy, cx, cy + 31, M["violet"][2])

    sk.wizard_back(c, 180, 594, s=3)

    # The vignette is scene light, so it runs before the type, never after. Running it
    # last dithered frame-edge darkening straight over the wordmark, which is what made
    # two of these screens unreadable.
    c.vignette([None, None, M["violet"][4], OCC], 2.2)
    sk.wordmark(c, 180, 36,
                [E["light"][0], E["light"][1], E["light"][2], M["gold"][1], M["gold"][2]],
                [E["arcane"][1], E["arcane"][2], E["arcane"][3]],
                halo=OCC)
    sk.caption(c, "TWELVE ELEMENTS. ONE STAFF.", 180, 148, M["violet"][1], 1, 1)
    sk.caption(c, "PRESS ANY KEY", 180, 618, M["violet"][1], 2, 1)
    return c, "twelve-sigils", "Twelve Sigils"

# -------------------------------------------------------------- 6. THE SPIRE
def the_spire():
    """Uses the portrait aspect as the subject rather than cropping a landscape into it: a
    bone spire climbing the whole 640, climbers spiralling up it, and you at the foot."""
    c = raster.Canvas(W, H, OCC)
    c.vramp(0, H - 1, [OCC, M["violet"][4], M["violet"][3], M["stone"][4],
                       M["stone"][3], M["stone"][4]])
    sk.starfield(c, 0, 300, 60, 41, [M["skin"][3], M["violet"][1]])

    cx = 180
    rng = random.Random(19)

    def half_width(y):
        return 10 + 48 * pow(max(0.0, (y - 110)) / 530.0, 0.85)

    # the shaft, with a ragged bone edge rather than a ruled taper
    left, right = [], []
    y = 110
    while y <= H:
        j = rng.randrange(-3, 4)
        hw = half_width(y)
        left.append((cx - hw + j, y))
        right.append((cx + hw - rng.randrange(-3, 4), y))
        y += 12
    c.poly_shade(left + list(reversed(right)),
                 [M["skin"][1], M["skin"][2], M["skin"][3], M["skin"][4]],
                 ang=0.0, gamma=1.2)

    # Stacked skulls up the spire. They are sized off the shaft rather than off a fixed
    # number so they read as the structure itself - undersized, they looked like decals
    # stuck on a grey column.
    y = 566
    while y > 130:
        hw = half_width(y)
        size = hw * 2.05
        if size < 15:
            break
        off = int(rng.randrange(-4, 5) * (size / 100.0))
        sk.skull(c, cx + off, y, size, size * 0.98, M["skin"],
                 socket=E["darkness"] if size > 30 else None, teeth=size > 40)
        y -= int(size * 0.80)

    # ribs jutting out of the shaft
    for i in range(15):
        yy = 150 + i * 32
        hw = half_width(yy)
        L = int(20 + 42 * (yy / float(H)))
        c.line(cx - hw + 4, yy, cx - hw - L, yy + 14, M["skin"][2])
        c.line(cx - hw + 4, yy + 1, cx - hw - L, yy + 15, M["skin"][3])
        c.line(cx + hw - 4, yy + 10, cx + hw + L, yy + 24, M["skin"][4])
        c.line(cx + hw - 4, yy + 11, cx + hw + L, yy + 25, M["stone"][4])

    mist(c, 300, 372, M["violet"][3], 0.16, 6)
    mist(c, 470, 540, M["stone"][3], 0.14, 7)

    for i in range(11):
        yy = 176 + i * 40
        x = cx + math.cos(i * 0.95) * (half_width(yy) + 12)
        sk.enemy_sil(c, x, yy, sk.ENEMY_KINDS[i % 4], 18 + int(yy / 40.0), OCC,
                     rim=M["skin"][1])

    c.radial(cx, 118, 52, 62, [None, E["darkness"][3], M["violet"][3], None])
    c.radial(cx, 112, 18, 26, [E["darkness"][0], E["darkness"][1], E["darkness"][2],
                               E["darkness"][3], None])

    # foreground ground, so the figure at the foot of it has something to stand on
    c.poly([(0, 612), (110, 596), (240, 604), (W, 590), (W, H), (0, H)], M["stone"][4])
    c.line(0, 612, 110, 596, M["stone"][2])
    c.line(110, 596, 240, 604, M["stone"][2])
    c.line(240, 604, W, 590, M["stone"][2])
    sk.wizard_back(c, 100, 636, s=4)

    # the sky at the top is the lightest part of this frame, so the wordmark gets its own
    # dark ground rather than relying on a halo
    c.vramp(0, 132, [OCC, OCC, M["violet"][4], None])
    # The vignette is scene light, so it runs before the type, never after. Running it
    # last dithered frame-edge darkening straight over the wordmark, which is what made
    # two of these screens unreadable.
    c.vignette([None, None, M["stone"][4], OCC], 2.4)
    sk.wordmark(c, 180, 22,
                [M["skin"][0], M["skin"][1], M["skin"][2], M["skin"][3]],
                [E["darkness"][1], E["darkness"][2]],
                halo=OCC)
    sk.caption(c, "PRESS ANY KEY", 244, 618, M["skin"][1], 2, 1)
    return c, "the-spire", "The Spire"

# ------------------------------------------------------------- 7. GRAVE BLOOM
def grave_bloom():
    """A macro shot. One buried skull the size of a building, arcane growth erupting out of
    its sockets, and the player standing on its brow at the scale of a bird. Close, organic,
    and the creepiest of the seven."""
    c = raster.Canvas(W, H, OCC)
    c.vramp(0, 360, [OCC, M["violet"][4], M["violet"][3], M["lichen"][4], M["lichen"][3]])
    sk.starfield(c, 0, 190, 50, 61, [M["violet"][1]])
    c.radial(180, 300, 220, 190, [None, None, M["lichen"][4], M["violet"][4], None])

    sk.skull(c, 180, 336, 316, 320, M["skin"], socket=E["poison"], teeth=False)

    # soil: an irregular line, built from a random walk, not a ruled rectangle
    rng = random.Random(23)
    walk = []
    y = 470
    for x in range(-8, W + 12, 6):
        y += rng.randrange(-5, 6)
        y = max(446, min(492, y))
        walk.append((x, y))
    c.poly(walk + [(W + 12, H), (-8, H)], M["stone"][3])
    for i in range(len(walk) - 1):
        c.line(walk[i][0], walk[i][1], walk[i + 1][0], walk[i + 1][1], M["stone"][1])
    for _ in range(5200):
        x = rng.randrange(W)
        y = rng.randrange(452, H)
        if y > walk[max(0, min(len(walk) - 1, (x + 8) // 6))][1]:
            c.set(x, y, [M["stone"][2], M["stone"][4], M["lichen"][4],
                         M["stone"][3]][rng.randrange(4)])
    # clumps of turned earth resting against the jaw
    for _ in range(26):
        x = rng.randrange(0, W)
        yy = walk[max(0, min(len(walk) - 1, (x + 8) // 6))][1]
        r = rng.randrange(4, 13)
        c.disc(x, yy + r - 2, r, r * 0.7, M["stone"][3])
        c.disc(x - 1, yy + r - 3, r * 0.6, r * 0.4, M["stone"][2])

    def tendril(x0, y0, ang, length, seed):
        r = random.Random(seed)
        x, y = float(x0), float(y0)
        a = ang
        for i in range(length):
            a += (r.random() - 0.5) * 0.30
            x += math.cos(a) * 2.4
            y += math.sin(a) * 2.4
            th = 2 if i < length * 0.4 else 1
            c.disc(x, y, th, th, M["lichen"][1])
            c.set(int(x), int(y - th), M["lichen"][0])
            if i % 9 == 4 and i > 8:
                # leaf, not a bead: a small triangle off the stem reads as growth
                d = 1 if r.random() < 0.5 else -1
                c.poly([(x, y), (x + math.cos(a + d * 1.2) * 9, y + math.sin(a + d * 1.2) * 9),
                        (x + math.cos(a + d * 0.4) * 11, y + math.sin(a + d * 0.4) * 11)],
                       M["lichen"][2])
                c.line(x, y, x + math.cos(a + d * 0.8) * 10, y + math.sin(a + d * 0.8) * 10,
                       M["lichen"][1])
        c.radial(x, y, 9, 9, [E["poison"][0], E["poison"][1], E["poison"][2],
                              E["poison"][3], None])

    for i, (sx, sy, a) in enumerate([(126, 328, -2.4), (128, 330, -1.6), (238, 330, -0.8),
                                     (236, 328, -1.6), (124, 330, -2.9), (240, 330, -0.2)]):
        tendril(sx, sy, a, 44 + i * 6, 100 + i)

    # what else is in this ground: ribs, a broken marker, two shapes on the ridge
    for (bx, by, ba, bl) in [(46, 512, -0.5, 26), (300, 528, 0.4, 22), (86, 566, -0.2, 18),
                             (268, 588, 0.6, 20), (150, 552, 0.1, 16)]:
        ex2 = bx + math.cos(ba) * bl
        ey2 = by + math.sin(ba) * bl
        c.line(bx, by, ex2, ey2, M["skin"][3])
        c.line(bx, by + 1, ex2, ey2 + 1, M["skin"][4])
        c.disc(bx, by, 3, 3, M["skin"][2])
        c.disc(ex2, ey2, 3, 3, M["skin"][3])
    for (gx, gy, gw, gh) in [(66, 528, 26, 40), (322, 546, 22, 34)]:
        c.poly([(gx - gw // 2, gy), (gx + gw // 2, gy - 4), (gx + gw // 2 - 2, gy - gh),
                (gx, gy - gh - 5), (gx - gw // 2 + 2, gy - gh + 2)], M["stone"][4])
        c.line(gx - gw // 2, gy, gx - gw // 2 + 2, gy - gh + 2, M["stone"][2])
    for (ex3, ey3, kind) in [(120, 502, "swarmer"), (250, 494, "bruiser")]:
        sk.enemy_sil(c, ex3, ey3, kind, 22, OCC, rim=M["lichen"][1])

    sk.wizard_back(c, 176, 214, s=1)

    # The vignette is scene light, so it runs before the type, never after. Running it
    # last dithered frame-edge darkening straight over the wordmark, which is what made
    # two of these screens unreadable.
    c.vignette([None, None, M["stone"][4], OCC], 2.0)
    sk.wordmark(c, 180, 34,
                [M["skin"][0], M["skin"][1], M["skin"][2], M["skin"][3]],
                [E["poison"][1], E["poison"][2], E["poison"][3]],
                halo=OCC)
    sk.caption(c, "SOMETHING DOWN THERE IS STILL GROWING", 180, 146, M["lichen"][0], 1, 1)
    sk.caption(c, "PRESS ANY KEY", 180, 610, M["lichen"][0], 2, 1)
    return c, "grave-bloom", "Grave Bloom"

SCREENS = [vigil, bonelight_moon, encircled, sanctum_gate, twelve_sigils, the_spire, grave_bloom]

def prompt_asset():
    """PRESS ANY KEY as its own transparent strip, in the same pixel face as the screens.

    It is a separate texture rather than part of the background so the title scene can fade it
    up and then breathe it. It is also not a Label: there is no Godot font resource for this
    face, and stamping the glyphs here keeps the prompt and the wordmark in the same hand.

    The strip is the full 360 wide so the scene can centre it with no horizontal arithmetic,
    and 20 tall covering rows 616-636 of the composition - where the baked-in caption sat.
    """
    c = raster.Canvas(W, 20)
    sk.caption(c, "PRESS ANY KEY", 180, 2, M["skin"][1], 2, 1)
    return c

def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    made = []
    for i, fn in enumerate(SCREENS):
        c, slug, title = fn()
        p = os.path.join(OUT, "%d-%s.png" % (i + 1, slug))
        c.save(p)
        c.scaled(2).save(os.path.join(OUT, "%d-%s@2x.png" % (i + 1, slug)))
        bad = c.audit()
        made.append((title, p, bad))
        print("%-16s %s  off-contract colours: %d%s" % (
            slug, c.img.size, len(bad), ("  " + str(bad[:4]) if bad else "")))

    # Vigil is the chosen screen. Copying it here rather than exporting by hand means a
    # palette change or a sprite change re-renders the shipping asset too.
    shutil.copyfile(made[0][1], os.path.join(ROOT, SHIPPING))
    prompt_asset().save(os.path.join(ROOT, SHIPPING_PROMPT))
    print("shipped %s + %s" % (SHIPPING, SHIPPING_PROMPT))

    sheet = Image.new("RGBA", (W * 7 + 8 * 8, H + 16), raster.rgb(OCC))
    for i in range(len(SCREENS)):
        sheet.paste(Image.open(made[i][1]), (8 + i * (W + 8), 8))
    sheet.save(os.path.join(OUT, "_contact.png"))
    print("wrote %s" % OUT)

if __name__ == "__main__":
    main()
