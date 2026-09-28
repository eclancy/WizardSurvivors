# -*- coding: utf-8 -*-
"""Original spell art: card icons, and the projectile sprites that go with them.
Run: python tools/art/spell_icons.py

Three spell icons in the game were already drawn for this project rather than taken from the third
party pack - they are the ones named ui-derived-spell-icon-*. This generator is where any further
ones come from, so the set stops growing by hand.

A spell needs two pictures and they are not the same picture. The card icon is looked at while the
game is paused, square, on a dark plate, and it can carry detail. The projectile is looked at for
half a second, in motion, rotated to its heading, with forty enemies behind it - so it is drawn
wide, facing +X (ElementalBolt sets Rotation from the travel direction), and it spends most of its
pixels on the leading edge, which is the only part the eye has time to read.

OUTPUT LOCATION, and why it is not assets/bonelight/. Every SpellData .tres points its Icon at
assets/organized/ui/, and the level-up card, the spellbook and the HUD all read that field. Writing
somewhere tidier would mean moving twenty existing icons at the same time, which is the art
replacement manifest's job and not this one. So a new icon lands beside the icons it sits next to,
under the same ui-derived-spell-icon- name the other three use.

SIZE. Authored at 32 and rendered at x2, giving a 64px file - the contract's 32x32 UI cell for an
icon that IS the content rather than a glyph labelling a control. The existing spell icons are a
mix of 32 and 64 and the card scales either, so the contract wins.

Light model per art-direction.md section 2: key from the upper left. On a wave that means the face
climbing toward the curl is lit and the trough under it is shaded, which is most of what makes a
lump of blue read as water.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# WHERE THESE GO, and why it was moved.
#
# They used to be written into assets/organized/ui/ under a "ui-derived-spell-icon-" prefix, which
# was true once - the first ones really were cut down from a bought GUI pack - and has not been
# true since they were all redrawn. The cost of leaving them there was not tidiness: that
# directory is the bought art, all of it is licensed for use and not for redistribution, and the
# removal that has to happen before this repo can be public is `rm -r assets/organized`. Twenty-six
# pieces of our own art were sitting in the blast radius. See .ai/asset-licensing.md.
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "spells")

CELL = 32


def riptide():
    """A breaking wave, curl at the top right, undertow dragging left along the floor.

    Two shapes, not one. A wave drawn as a single mass reads as a blue hill; what makes it a wave
    is the gap under the curl - the lip throws forward and there is air beneath it. So the crest is
    drawn as a body plus a separate lip, with occlusion between them.

    Foam is water.core, the brightest tone in the element ramp, and it is used sparingly: a rim
    along the lip and a few broken flecks where the lip is about to fall. Foam everywhere would
    flatten the whole icon to one value, which at 32 pixels is the difference between a wave and a
    smear.
    """
    c = raster.Canvas(CELL, CELL, None)

    deep = bl.tone("azure", "deep")
    shade = bl.tone("azure", "shade")
    base = bl.tone("azure", "base")
    lit = bl.tone("azure", "lit")
    hi = bl.tone("azure", "hi")
    foam = bl.ELEMENTS["water"][0]

    # The body: a swell rising left to right, sitting on the floor of the cell.
    body = [
        (1, 29), (2, 24), (5, 19), (10, 14), (16, 10),
        (22, 9), (26, 11), (28, 15), (27, 20), (24, 24),
        (24, 29), (1, 29),
    ]
    c.poly_shade(body, [hi, lit, base, shade, deep], ang=-0.7854, bias=0.15)

    # The lip: the part that has already thrown forward, curling back over the trough.
    lip = [
        (16, 10), (22, 9), (27, 12), (28, 17), (25, 19),
        (26, 15), (23, 12), (18, 12), (16, 10),
    ]
    c.poly_shade(lip, [foam, hi, lit, base, shade], ang=-0.7854, bias=0.35)

    # The air under the lip. This is the shape that makes it a wave rather than a hill, so it is
    # drawn as occlusion rather than as a darker blue - a hole reads as a hole only if it is the
    # darkest thing in the icon.
    hollow = [(20, 14), (25, 15), (26, 18), (24, 20), (21, 18), (20, 14)]
    c.poly(hollow, OCC)

    # Foam along the leading edge of the lip, broken rather than continuous.
    for (x, y) in [(17, 10), (19, 9), (21, 9), (23, 10), (25, 11), (27, 13), (28, 16)]:
        c.set(x, y, foam)
    for (x, y) in [(18, 11), (22, 10), (26, 12)]:
        c.set(x, y, hi)

    # Spray: three flecks thrown clear of the curl, ahead of it and above it. They also stop the
    # icon from ending in a hard silhouette edge on the right, which at this size looks cut off.
    for (x, y) in [(29, 10), (26, 6), (21, 5)]:
        c.set(x, y, foam)
    c.set(30, 12, hi)
    c.set(23, 7, lit)

    # The undertow: two drag lines along the floor running back the other way, which is what the
    # spell actually does - it travels through the crowd rather than landing on it.
    c.hline(2, 12, 27, lit)
    c.hline(4, 9, 25, hi)
    c.hline(6, 18, 29, base)

    # Occlusion under the whole mass, one row, so the icon sits on the card rather than floating.
    c.hline(1, 24, 30, OCC)
    c.set(0, 29, OCC)
    c.set(25, 29, OCC)

    return c


def cinderbreath():
    """A cone of fire thrown from the lower left toward the upper right.

    The icon has to say "sustained" rather than "explosion", and the thing that carries that is the
    NOZZLE - a narrow bright root the flame comes out of. A cone with no root is a blast; a cone
    with a root is something being poured. So the icon spends real pixels on a tiny throat at the
    bottom left, which is most of the difference between this and the existing explosion icons.

    Colour runs the other way to intuition: white-hot at the root, orange in the body, dark red at
    the ragged leading edge, because a flame is coolest where it is thinnest.
    """
    c = raster.Canvas(CELL, CELL, None)

    edge = bl.ELEMENTS["fire"][3]
    mid = bl.ELEMENTS["fire"][2]
    hot = bl.ELEMENTS["fire"][1]
    core = bl.ELEMENTS["fire"][0]
    iron = bl.tone("steel", "shade")
    iron_lit = bl.tone("steel", "lit")

    # The envelope, widening toward the upper right. Drawn as one polygon and then eaten into by
    # the brighter layers, rather than three separate cones - overlapping cones leave seams.
    # Apex at the nozzle, widest at the far cap - a cone, not a lens. The first pass tapered at both
    # ends, which read as a leaf: the whole point is that it gets WIDER the further it is thrown.
    outer = [(7, 27), (15, 14), (22, 6), (26, 3), (30, 6), (31, 11), (29, 16), (24, 20), (16, 25), (7, 27)]
    c.poly(outer, edge)

    body = [(8, 26), (15, 15), (21, 8), (25, 6), (28, 9), (28, 14), (25, 17), (19, 21), (8, 26)]
    c.poly(body, mid)

    inner = [(8, 26), (14, 17), (19, 11), (23, 9), (24, 13), (22, 16), (17, 19), (8, 26)]
    c.poly(inner, hot)

    # The white core, short - it never reaches the leading edge.
    throat = [(8, 26), (13, 19), (17, 14), (19, 13), (18, 17), (14, 20), (8, 26)]
    c.poly(throat, core)

    # Broken flecks past the leading edge: fire sheds, and a hard silhouette edge reads as a wedge.
    for (x, y) in [(29, 2), (31, 14), (26, 22), (19, 27)]:
        c.set(x, y, edge)
    for (x, y) in [(28, 4), (28, 18)]:
        c.set(x, y, mid)

    # The nozzle, overlapping the apex so the flame is coming OUT of something rather than floating
    # near it. Two pixels of lit steel and a bright throat is the whole "sustained" read.
    c.rect(2, 26, 7, 30, iron)
    c.hline(2, 7, 26, iron_lit)
    c.vline(2, 26, 30, iron_lit)
    c.set(7, 27, core)
    c.set(7, 28, hot)
    c.set(6, 28, core)

    return c



def mirefoot():
    """A pool of bog with scum on it and two reeds standing out of it.

    Read from the top down, like the game is. The trick at 32 pixels is that standing water is only
    legible if something BREAKS it - a flat ellipse of dark blue is a hole, not a puddle. So the
    reeds and the scum are not decoration, they are what tells the eye which way is up.
    """
    c = raster.Canvas(CELL, CELL, None)

    water_deep = bl.tone("azure", "deep")
    water = bl.tone("azure", "shade")
    water_lit = bl.tone("azure", "base")
    scum = bl.tone("lichen", "base")
    scum_lit = bl.tone("lichen", "lit")
    reed = bl.tone("verdant", "shade")
    reed_lit = bl.tone("verdant", "lit")

    # The pool: a lopsided blob, wider than tall, sitting low in the cell.
    pool = [(4, 20), (9, 15), (17, 13), (25, 15), (29, 20), (26, 26), (17, 29), (8, 26), (4, 20)]
    c.poly(pool, water)

    inner = [(7, 20), (12, 17), (18, 16), (24, 18), (26, 21), (23, 25), (16, 27), (9, 24), (7, 20)]
    c.poly(inner, water_deep)

    # Key light from the upper left catches the near rim only.
    for (x, y) in [(6, 19), (8, 17), (11, 15), (15, 14), (19, 14)]:
        c.set(x, y, water_lit)

    # Scum: broken patches, never a ring, or it reads as a tyre.
    for (x, y) in [(10, 21), (11, 22), (14, 24), (15, 24), (20, 20), (21, 21), (22, 19)]:
        c.set(x, y, scum)
    for (x, y) in [(11, 21), (15, 23), (21, 20)]:
        c.set(x, y, scum_lit)

    # Two reeds. Different heights, or they read as a gate.
    c.vline(12, 4, 16, reed)
    c.set(12, 5, reed_lit)
    c.set(13, 8, reed)
    c.vline(23, 8, 18, reed)
    c.set(23, 9, reed_lit)
    c.set(22, 12, reed)

    c.hline(6, 26, 30, OCC)

    return c


def kindled_ward():
    """A made light: a bright core inside a halo, with four sparks coming off it.

    No lamp, no lantern body, no face. It is the thing the world-and-tone doc calls a ward - a piece
    of magic the player made and left burning - and giving it a housing would make it an object
    somebody built rather than something conjured. The whole icon is light.
    """
    c = raster.Canvas(CELL, CELL, None)

    edge = bl.tone("amber", "deep")
    outer = bl.tone("amber", "shade")
    mid = bl.tone("amber", "base")
    hot = bl.tone("amber", "lit")
    core = bl.ELEMENTS["light"][0]

    cx = cy = 15.5

    c.disc(cx, cy, 12.0, 12.0, edge)
    c.disc(cx, cy, 9.5, 9.5, outer)
    c.disc(cx, cy, 7.0, 7.0, mid)
    c.disc(cx, cy, 4.5, 4.5, hot)
    c.disc(cx, cy, 2.4, 2.4, core)

    # Four sparks on the diagonals, detached from the body so the halo reads as light rather than
    # as a solid ball with a rim.
    for (x, y) in [(5, 5), (26, 5), (5, 26), (26, 26)]:
        c.set(x, y, hot)
    for (x, y) in [(6, 6), (25, 6), (6, 25), (25, 25)]:
        c.set(x, y, mid)
    c.set(15, 1, hot)
    c.set(15, 30, mid)
    c.set(1, 15, mid)
    c.set(30, 15, hot)

    return c


def gravewell():
    """A pit in the ground, rim lit on the upper left, nothing but occlusion inside it.

    The inside is the darkest value in the palette and the rim is one of the lightest, because at
    this size contrast is the only thing that says "hole" rather than "disc". Clods thrown out
    around the lip stop it reading as a drawn circle.
    """
    c = raster.Canvas(CELL, CELL, None)

    # Deliberately the LIGHT end of the stone ramp. The first pass built the lip out of shade and
    # deep, which is what soil actually is, and the icon vanished: the card behind it is near-black,
    # so a dark rim around a black hole is a black square. On a dark plate the earth has to be lit
    # for the hole to be dark by comparison.
    # The EARTH element ramp, not the stone material ramp. Stone tops out at #3A4658 - it is a
    # shadow colour, built for masonry sitting in the dark - so a lip drawn from it disappeared
    # against the near-black card twice over. Earth runs warm and light and is what soil looks like
    # when something is lighting it.
    soil = bl.ELEMENTS["earth"][2]
    soil_lit = bl.ELEMENTS["earth"][1]
    soil_hi = bl.ELEMENTS["earth"][0]
    soil_shade = bl.ELEMENTS["earth"][3]
    gloom = bl.ELEMENTS["darkness"][3]

    # The lip: a squashed ellipse, because the camera looks down at an angle.
    c.disc(15.5, 17.0, 13.0, 10.0, soil)

    # Lit on the upper-left arc, shaded on the lower right - a raised bank, not a flat ring.
    for deg in range(0, 360, 3):
        a = math.radians(deg)
        facing = math.cos(a - math.radians(225.0))
        tone = soil_hi if facing > 0.6 else (soil_lit if facing > 0.0 else soil_shade)
        c.set(15.5 + math.cos(a) * 12.4, 17.0 + math.sin(a) * 9.4, tone)
        c.set(15.5 + math.cos(a) * 11.3, 17.0 + math.sin(a) * 8.5, tone)

    # The hole. One thin ring of gloom where the bank turns under, then occlusion.
    c.disc(15.5, 17.6, 9.0, 6.6, gloom)
    c.disc(15.5, 18.0, 7.8, 5.6, OCC)

    # Clods thrown clear of the lip.
    for (x, y) in [(2, 14), (29, 15), (7, 28), (24, 28), (16, 4)]:
        c.set(x, y, soil_lit)
    for (x, y) in [(3, 15), (28, 16), (8, 29), (23, 29)]:
        c.set(x, y, soil_shade)

    return c


SURGE_W = 24
SURGE_H = 14


def riptide_surge():
    """The Riptide projectile: a crescent of water travelling right, foam on its leading edge.

    Wide rather than square, because the spell pierces - it is a thing passing THROUGH a line of
    enemies, and a round bolt would read as a thing landing on one. The tail is a wake, not a body:
    it thins and breaks up toward the left so the sprite has a direction even when it is standing
    still on the first frame.
    """
    c = raster.Canvas(SURGE_W, SURGE_H, None)

    deep = bl.tone("azure", "deep")
    shade = bl.tone("azure", "shade")
    base = bl.tone("azure", "base")
    lit = bl.tone("azure", "lit")
    hi = bl.tone("azure", "hi")
    foam = bl.ELEMENTS["water"][0]

    # The crescent. Front edge bulges right, the concave back edge is what makes it a wave front
    # rather than a leaf.
    crest = [
        (10, 1), (16, 2), (20, 5), (21, 7), (20, 9), (16, 12), (10, 13),
        (14, 9), (15, 7), (14, 5), (10, 1),
    ]
    c.poly_shade(crest, [hi, lit, base, shade, deep], ang=-0.7854, bias=0.1)

    # The wake, thinning to the left. Three broken lines rather than a tapered mass: at this size a
    # taper turns to mush after the x2, and three separated rows still read as speed.
    c.hline(2, 12, 6, base)
    c.hline(4, 11, 4, shade)
    c.hline(4, 11, 9, shade)
    c.hline(0, 6, 7, shade)
    c.set(1, 5, deep)
    c.set(1, 8, deep)

    # Foam on the leading edge only, and a highlight along the upper face where the key light is.
    for (x, y) in [(15, 2), (18, 3), (20, 5), (21, 7), (20, 9), (18, 11), (15, 12)]:
        c.set(x, y, foam)
    for (x, y) in [(12, 2), (13, 3), (16, 3), (17, 4)]:
        c.set(x, y, hi)

    return c


def unknown():
    """The fallback every spell without art of its own falls through to.

    It replaces `ui-png-skills-icon-2.png`, a bought GUI-pack icon that was `DefaultSpellIcon` -
    which meant the one icon guaranteed to be on screen for any content gap was the one piece of
    art we are least allowed to publish.

    Drawn as a sealed rune rather than a question mark: a question mark says "error", and this
    appears when content is missing rather than when something has gone wrong.
    """
    c = raster.Canvas(CELL, CELL)
    core, hot, mid, edge = bl.ELEMENTS["arcane"]
    cx = cy = CELL / 2.0 - 0.5

    c.disc(cx, cy, 13.0, 13.0, bl.OCC)
    c.ring(cx, cy, 12.0, 12.0, edge, 2)
    c.ring(cx, cy, 9.0, 9.0, mid, 1)

    # Three bars and a dot: enough shape to be a mark, not enough to be a letter.
    for dy, tone in ((-4, mid), (0, hot), (4, mid)):
        c.hline(cx - 4, cx + 4, cy + dy, tone)
    c.disc(cx, cy, 1.6, 1.6, core)
    return c


ICONS = [
    ("riptide-water", riptide, CELL),
    ("cinderbreath-fire", cinderbreath, CELL),
    ("mirefoot-bog", mirefoot, CELL),
    ("kindled-ward-light", kindled_ward, CELL),
    ("gravewell-pit", gravewell, CELL),
    ("unknown", unknown, CELL),
]

# The projectile sprites live with the other Bonelight art, not in the third party effects folder -
# nothing but the .tscn reads them, so there is no legacy path to be compatible with.
FX_DIR = os.path.join(ROOT, "assets", "bonelight", "effects")
FX = [
    ("riptide-surge", riptide_surge),
]


def write(canvas, name, cell):
    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s (art-direction.md section 3):" % name)
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return False

    path = os.path.join(OUT_DIR, name + ".png")
    canvas.scaled(2).save(path)
    print("  %-40s %dx%d" % (name, cell * 2, cell * 2))
    return True


def write_fx(canvas, name):
    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s (art-direction.md section 3):" % name)
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return False

    if not os.path.isdir(FX_DIR):
        os.makedirs(FX_DIR)
    path = os.path.join(FX_DIR, name + ".png")
    canvas.scaled(2).save(path)
    print("  %-40s %dx%d" % (name, canvas.w * 2, canvas.h * 2))
    return True


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    ok = True
    for name, fn, cell in ICONS:
        ok &= write(fn(), name, cell)
    for name, fn in FX:
        ok &= write_fx(fn(), name)
    if not ok:
        return 1
    print("")
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
