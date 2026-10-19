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
    """The slab behind the menu: laid cobblestone, matte and quiet, lit from the upper left.

    IT IS A BACKDROP, NOT A PICTURE. Everything on this screen that matters is written on top of
    it - the heading sits directly on it, and the cards are inset into it - so its job is to read
    as "stone floor" at a glance and then get out of the way.

    WHAT WAS WRONG WITH THE LAST ONE. It laid the cobbles correctly (running bond, mortar on all
    four sides, corners knocked off - all of which is kept below) but drew them in the BRIGHT half
    of the palette: faces from `skin` base and `steel` base, a `skin` hi lip on every stone and a
    `skin` hi glint on one stone in three. That is a floor of polished, wet stones with a specular
    on each - a busy high-contrast field in exactly the place the heading had to be read, and the
    player reported the heading as completely illegible. Cracks scored across the courses added a
    second pattern on top of the first.

    So this is the same floor in the dark half:

      * Faces are two adjacent tones of `stone` - lit and base - so stone-to-stone variation is a
        murmur, not a checkerboard. No third, brighter face.
      * The lip is ONE step up (`stone` hi) and only on the top edge; the left edge stays flat.
        No glints, no pitting, no cracks: nothing on the floor brighter than a lip.
      * Mortar is `stone` deep, so the pattern still reads, at a contrast the eye registers as
        texture rather than as content.

    Measured by tools/art/ui_contrast.py: the heading ink against the
    brightest stone this floor has is about 8.6:1; against the old floor's lips it was 1.25:1. (The
    heading no longer sits on the floor at all - it has its own band - but the floor should still
    never be the brightest thing behind a word.)
    """
    rng = random.Random(seed)

    mortar = bl.tone("stone", "deep")
    c = raster.Canvas(PLATE_W, PLATE_H, mortar)
    last_x, last_y = PLATE_W - 1, PLATE_H - 1

    faces = [
        (bl.tone("stone", "lit"), 0.78),
        (bl.tone("stone", "base"), 0.22),
    ]

    def pick_face():
        r = rng.random()
        acc = 0.0
        for tone, w in faces:
            acc += w
            if r <= acc:
                return tone
        return faces[-1][0]

    lip = bl.tone("stone", "hi")
    dark_edge = bl.tone("stone", "shade")

    # Courses a little shallower and stones a little more even than before: a more uniform unit
    # is a quieter floor, and it is the variation between stones that the eye reads as busy.
    course_h = 14
    y = -rng.randrange(0, course_h)
    course = 0
    while y < PLATE_H + course_h:
        x = -rng.randrange(6, 30) - (course % 3) * 9
        while x < PLATE_W + 40:
            w = rng.randrange(20, 30)
            face = pick_face()

            # One pixel of jitter top or bottom, never both: hand-laid, but not rubble.
            top_jit = rng.choice((0, 0, 0, 1))
            bot_jit = rng.choice((0, 0, 0, -1))

            x0, y0 = x, y + top_jit
            x1, y1 = x + w, y + (course_h - 3) + bot_jit

            c.rect(max(0, x0 + 1), max(0, y0), min(last_x, x1 - 1), min(last_y, y1), face)
            c.rect(max(0, x0), max(0, y0 + 1), min(last_x, x1), min(last_y, y1 - 1), face)
            for (px, py) in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
                if 0 <= px <= last_x and 0 <= py <= last_y:
                    c.set(px, py, mortar)

            # Relief, one step each way and no further: a lip on top, a shade line underneath.
            if 0 <= y0 <= last_y:
                c.hline(max(0, x0 + 1), min(last_x, x1 - 1), y0, lip if face == faces[0][0] else face)
            if 0 <= y1 <= last_y:
                c.hline(max(0, x0 + 1), min(last_x, x1 - 1), y1, dark_edge)
            if 0 <= x1 <= last_x:
                c.vline(x1, max(0, y0 + 1), min(last_y, y1 - 1), dark_edge)

            x += w + 2
        y += course_h
        course += 1

    # The slab's own bevel, in steel shade rather than bone: an edge that is there, not a rim of
    # light round the whole menu.
    c.hline(0, last_x, 0, bl.tone("steel", "shade"))
    c.vline(0, 0, last_y, bl.tone("steel", "shade"))
    c.hline(1, last_x, 1, bl.tone("stone", "hi"))
    c.vline(1, 1, last_y, bl.tone("stone", "hi"))
    c.hline(0, last_x, last_y, OCC)
    c.hline(0, last_x, last_y - 1, bl.tone("stone", "deep"))
    c.vline(last_x, 0, last_y, OCC)
    c.vline(last_x - 1, 0, last_y, bl.tone("stone", "deep"))

    # Corner blocks, so the slab has quarried ends rather than mitred picture-frame corners.
    for (cx, cy) in ((0, 0), (last_x - 7, 0), (0, last_y - 7), (last_x - 7, last_y - 7)):
        c.rect(cx, cy, cx + 7, cy + 7, bl.tone("stone", "shade"))
        c.rect(cx + 1, cy + 1, cx + 6, cy + 6, bl.tone("stone", "lit"))
        c.hline(cx + 1, cx + 6, cy + 1, bl.tone("stone", "hi"))

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
