# -*- coding: utf-8 -*-
"""Level-up screen furniture. Run: python tools/art/levelup_frames.py

Three pieces, all authored at half size and rendered at x2, which is the scale every other
Bonelight asset uses - so their pixels are the same size as the pixels in the sprites they sit
next to. A UI drawn at 1:1 next to art drawn at 2:1 is the single clearest way to make a screen
look assembled rather than designed.

  levelup-plate.png       the stone slab the whole menu sits on
  levelup-card.png        a nine-slice card frame, gold on stone
  levelup-card-hover.png  the same frame with the gold lit

Why this exists at all: the menu was translucent - cards at 55% alpha over a 62% dim over live
gameplay - so the swarm was legible *through* the text the player was trying to read. Opacity is
most of the fix. The rest is that a flat rounded rectangle reads as a debug placeholder, and this
screen is one of the two the player looks at most.

Light model per art-direction.md section 2: key from the upper left, so every raised edge is lit
on its top and left and shaded on its bottom and right. That single rule is what makes the gold
read as a bevel instead of a coloured line.
"""

import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui")

# The card frame, authored at 40x40 with a 10px border, so at x2 it is 80x80 with 20px nine-slice
# margins. The centre is a single flat tone on purpose: it is stretched across the whole card, and
# any texture in it would smear.
CARD = 40
CARD_MARGIN = 10

# The plate is the Panel node's exact pixel size, halved.
#
# That size comes from LevelUpMenu.CenterMenuPanel, not from the scene file - the scene anchors are
# overwritten at runtime. In portrait it insets 12px each side and starts at HudSafeTop (112) so
# the run HUD stays visible, giving 696x1156 on a 720x1280 viewport. Authoring at exactly half of
# that means the nine-patch does no stretching at all at the real size, so every crack lands on a
# whole pixel; at any other size it degrades to a stretch instead of breaking.
PLATE_W, PLATE_H = 348, 578
# Fixed corner size for the plate nine-patch, at x2. Matches the 8px quarried corner blocks below.
PLATE_MARGIN = 8


def card_frame(lit):
    """The option card: an opaque stone face inside a bevelled gold border.

    `lit` raises the whole gold ramp one step, which is the hover state. Brightening rather than
    recolouring keeps hover reading as "the same object, catching more light" instead of as a
    different card.
    """
    c = raster.Canvas(CARD, CARD, None)

    gold_hi = bl.tone("gold", "hi" if lit else "lit")
    gold_lit = bl.tone("gold", "lit" if lit else "base")
    gold_base = bl.tone("gold", "base" if lit else "shade")
    gold_deep = bl.tone("gold", "shade" if lit else "deep")

    face = bl.tone("stone", "deep")
    face_lit = bl.tone("stone", "shade")

    last = CARD - 1

    # Opaque face first. Everything else is drawn over its edges.
    c.rect(0, 0, last, last, face)

    # A lit lip under the top border and a dark one above the bottom, so the face reads as
    # recessed. Both are kept strictly inside the nine-slice margins: the centre band is stretched
    # across the whole card, so anything but a flat tone in there smears into a vertical streak.
    c.rect(1, 5, last - 1, 9, face_lit)
    c.rect(1, last - 9, last - 1, last - 5, OCC)

    # Outer edge: occlusion, so the card separates from the plate behind it.
    c.rect(0, 0, last, 0, OCC)
    c.rect(0, last, last, last, OCC)
    c.rect(0, 0, 0, last, OCC)
    c.rect(last, 0, last, last, OCC)

    # The gold band, three pixels thick, bevelled by the key light.
    for i in range(1, 4):
        top = gold_hi if i == 1 else (gold_lit if i == 2 else gold_base)
        bottom = gold_deep if i == 1 else (gold_base if i == 2 else gold_lit)
        c.hline(i, last - i, i, top)
        c.vline(i, i, last - i, top)
        c.hline(i, last - i, last - i, bottom)
        c.vline(last - i, i, last - i, bottom)

    # Inner shadow: the face is recessed behind the band, and one dark line sells that.
    c.hline(4, last - 4, 4, OCC)
    c.vline(4, 4, last - 4, OCC)
    c.hline(4, last - 4, last - 4, bl.tone("stone", "shade"))
    c.vline(last - 4, 4, last - 4, bl.tone("stone", "shade"))

    # Corner studs. Nine-slice keeps the corner blocks unstretched at any card size, so this is
    # the one place ornament can live and stay the shape it was drawn.
    for (cx, cy) in ((5, 5), (last - 5, 5), (5, last - 5), (last - 5, last - 5)):
        c.disc(cx, cy, 2, 2, gold_base)
        c.set(cx, cy, gold_hi if lit else gold_lit)
        # Contact shadow on the away-from-key side of each stud.
        c.set(cx + 1, cy + 1, gold_deep)

    return c


def plate(seed=11):
    """The slab behind the menu: laid cobblestone, grey, lit from the upper left.

    WHAT WAS WRONG BEFORE. The previous version scattered large tonal rectangles over a base fill
    and then speckled it. Every ingredient of "stone" was there and none of "cobble": no mortar, no
    edges, no repeating unit - so it read as a noisy dark field rather than as a floor made of
    pieces. Cobblestone is legible because of the GAPS. The stones are almost incidental; it is the
    dark grid between them that the eye reads as a pattern.

    So this lays actual stones:

      * Running bond - each course offset from the one above, the way anything laid by hand is,
        because a perfect grid reads as tile and a random scatter reads as rubble.
      * Every stone gets a mortar gap on all four sides, drawn as occlusion. That gap is the whole
        effect and it is why the stones are inset rather than tiled edge to edge.
      * Each stone is lit on its top and left and shaded on its bottom and right, per the light
        model - so the floor has relief instead of being a flat pattern.
      * Corners are clipped by a pixel. A 12-pixel rectangle reads as a brick; knocking the
        corners off is most of the difference between brick and cobble.

    GREY, not blue. The stone row is the darkest and bluest in the palette and it is what made the
    old plate look like slate at midnight. The cobbles are drawn from `skin`, which is the most
    neutral grey the contract defines, with `steel` for the brightest faces - and the mortar is
    `stone` deep and occlusion, so the dark row still does the job it is good at.
    """
    rng = random.Random(seed)

    mortar = bl.tone("stone", "deep")
    c = raster.Canvas(PLATE_W, PLATE_H, mortar)
    last_x, last_y = PLATE_W - 1, PLATE_H - 1

    # The faces a cobble can be cut from, brightest first. Weighted toward the middle so the floor
    # has a dominant tone with lighter and darker stones set into it rather than three equal bands.
    faces = [
        (bl.tone("steel", "base"), 0.10),
        (bl.tone("skin", "base"), 0.34),
        (bl.tone("skin", "shade"), 0.38),
        (bl.tone("steel", "shade"), 0.18),
    ]

    def pick_face():
        r = rng.random()
        acc = 0.0
        for tone, w in faces:
            acc += w
            if r <= acc:
                return tone
        return faces[-1][0]

    lit_edge = bl.tone("skin", "lit")
    hi_edge = bl.tone("skin", "hi")
    dark_edge = bl.tone("stone", "shade")

    course_h = 15
    y = -rng.randrange(0, course_h)
    course = 0
    while y < PLATE_H + course_h:
        # Running bond: every course starts at a different offset, and the offsets do not repeat
        # on a two-course cycle, which is what would turn it back into a grid.
        x = -rng.randrange(6, 34) - (course % 3) * 9
        while x < PLATE_W + 40:
            w = rng.randrange(17, 34)
            face = pick_face()

            # Per-stone jitter on the top and bottom edge. Uniform heights in dead-straight courses
            # is the single strongest "brick wall" tell; a cobble floor is hand-laid and no two
            # stones sit at quite the same level.
            top_jit = rng.randrange(-1, 2)
            bot_jit = rng.randrange(-1, 2)

            x0, y0 = x, y + top_jit
            x1, y1 = x + w, y + (course_h - 3) + bot_jit

            # Body, with all four corners knocked off - drawn as two overlapping rects inset on
            # opposite axes, so the corner pixels are never filled by either.
            c.rect(max(0, x0 + 1), max(0, y0), min(last_x, x1 - 1), min(last_y, y1), face)
            c.rect(max(0, x0), max(0, y0 + 1), min(last_x, x1), min(last_y, y1 - 1), face)

            # And then explicitly cleared, because at this size one square corner is enough to
            # make the whole floor read as masonry again.
            for (px, py) in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
                if 0 <= px <= last_x and 0 <= py <= last_y:
                    c.set(px, py, mortar)

            # Relief. Top and left catch the key; bottom and right fall away.
            if 0 <= y0 <= last_y:
                c.hline(max(0, x0 + 1), min(last_x, x1 - 1), y0, lit_edge)
            if 0 <= x0 <= last_x:
                c.vline(x0, max(0, y0 + 1), min(last_y, y1 - 1), lit_edge)
            if 0 <= y1 <= last_y:
                c.hline(max(0, x0 + 1), min(last_x, x1 - 1), y1, dark_edge)
            if 0 <= x1 <= last_x:
                c.vline(x1, max(0, y0 + 1), min(last_y, y1 - 1), dark_edge)

            # One bright pixel on the upper-left corner of roughly every third stone. Wear, and it
            # stops the courses from reading as perfectly uniform rows.
            if rng.random() < 0.34 and 0 <= x0 + 1 <= last_x and 0 <= y0 + 1 <= last_y:
                c.set(x0 + 1, y0 + 1, hi_edge)

            # A little pitting on the darker stones only, so the texture does not fight the faces
            # the cards will sit on.
            if face == bl.tone("skin", "shade") or face == bl.tone("steel", "shade"):
                for _ in range(rng.randrange(0, 3)):
                    px = rng.randrange(x0 + 2, max(x0 + 3, x1 - 1))
                    py = rng.randrange(y0 + 2, max(y0 + 3, y1 - 1))
                    if 0 <= px <= last_x and 0 <= py <= last_y:
                        c.set(px, py, dark_edge)

            x += w + 2
        y += course_h
        course += 1

    # A few cracks, running ACROSS the courses so they read as damage to the floor rather than as
    # one more mortar line. Occlusion core with a broken lit lip, same as before - that part was
    # right, it was just drawn on the wrong floor.
    for _ in range(4):
        x = float(rng.randrange(30, PLATE_W - 30))
        y = float(rng.randrange(20, PLATE_H - 20))
        drift = rng.choice([-0.8, -0.45, 0.45, 0.8])
        length = rng.randrange(70, 190)
        for step in range(length):
            drift += rng.uniform(-0.18, 0.18)
            x += drift * rng.uniform(0.3, 1.0)
            y += 1.0
            if not (2 <= x < PLATE_W - 2 and 2 <= y < PLATE_H - 2):
                break
            c.set(int(x), int(y), OCC)
            # Lip one step down the ramp, not the highlight. At hi it read as a bright scratch
            # laid over the floor instead of a split going into it.
            if step % 3:
                c.set(int(x) - 1, int(y), bl.tone("skin", "base"))

    # Worn bevel: the slab is a raised object, lit top-left, shaded bottom-right.
    c.hline(0, last_x, 0, bl.tone("skin", "hi"))
    c.hline(0, last_x, 1, bl.tone("skin", "lit"))
    c.vline(0, 0, last_y, bl.tone("skin", "hi"))
    c.vline(1, 0, last_y, bl.tone("skin", "lit"))
    c.hline(0, last_x, last_y, OCC)
    c.hline(0, last_x, last_y - 1, bl.tone("stone", "deep"))
    c.vline(last_x, 0, last_y, OCC)
    c.vline(last_x - 1, 0, last_y, bl.tone("stone", "deep"))

    # Corner blocks, so the slab has quarried ends rather than mitred picture-frame corners.
    for (cx, cy) in ((0, 0), (last_x - 7, 0), (0, last_y - 7), (last_x - 7, last_y - 7)):
        c.rect(cx, cy, cx + 7, cy + 7, bl.tone("skin", "shade"))
        c.rect(cx + 1, cy + 1, cx + 6, cy + 6, bl.tone("stone", "base"))
        c.set(cx + 1, cy + 1, bl.tone("stone", "hi"))

    return c


def write(canvas, name):
    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s (art-direction.md section 3):" % name)
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return False

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    path = os.path.join(OUT_DIR, name)
    canvas.scaled(2).save(path)
    print("wrote %s at %dx%d" % (
        os.path.relpath(path, ROOT).replace(os.sep, "/"), canvas.w * 2, canvas.h * 2))
    return True


def main():
    ok = True
    ok &= write(card_frame(False), "levelup-card.png")
    ok &= write(card_frame(True), "levelup-card-hover.png")
    ok &= write(plate(), "levelup-plate.png")
    if not ok:
        return 1

    print("card nine-slice margin at x2: %d" % (CARD_MARGIN * 2))
    print("plate nine-slice margin at x2: %d" % (PLATE_MARGIN * 2))
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
