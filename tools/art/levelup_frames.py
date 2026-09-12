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
    """The slab behind the menu: quarried stone, a worn bevel, and a few hairline cracks.

    Drawn from the top of the stone ramp rather than the bottom. The first version used deep and
    shade for everything and came out near-black - no texture survived, and the card faces, which
    are also dark, vanished into it. The slab has to be the LIGHTEST thing on the screen after the
    gold, so the cards read as recessed panels sitting on it and the text has something to sit
    against. Bonelight is a dark palette; that does not mean every layer is dark.
    """
    rng = random.Random(seed)
    c = raster.Canvas(PLATE_W, PLATE_H, bl.tone("stone", "base"))
    last_x, last_y = PLATE_W - 1, PLATE_H - 1

    # Broad tonal variation so the slab is not one flat field. Blocks rather than noise: this is
    # quarried stone, and stone breaks in planes.
    for _ in range(110):
        w = rng.randrange(24, 90)
        h = rng.randrange(18, 70)
        x = rng.randrange(-20, PLATE_W)
        y = rng.randrange(-20, PLATE_H)
        tone = bl.tone("stone", "lit") if rng.random() < 0.45 else bl.tone("stone", "shade")
        c.rect(max(0, x), max(0, y), min(last_x, x + w), min(last_y, y + h), tone)

    # Grain. Bright specks toward the upper left where the key falls, dark pitting toward the
    # lower right - the same directional rule the sprites follow, at the scale of a rock face.
    for _ in range(4200):
        x = rng.randrange(0, PLATE_W)
        y = rng.randrange(0, PLATE_H)
        toward_dark = (x / float(PLATE_W) + y / float(PLATE_H)) * 0.5
        if rng.random() < toward_dark:
            c.set(x, y, bl.tone("stone", "shade") if rng.random() < 0.7 else bl.tone("stone", "deep"))
        elif rng.random() < 0.35:
            c.set(x, y, bl.tone("stone", "hi") if rng.random() < 0.25 else bl.tone("stone", "lit"))

    # A few cracks. Deliberately few and thin - the brief is "a few light cracks", and a slab
    # covered in them reads as rubble. Each is a drunk walk with an occlusion core and a lit lip
    # on its upper-left side, which is what gives a crack depth rather than making it a scratch.
    for _ in range(5):
        x = float(rng.randrange(30, PLATE_W - 30))
        y = float(rng.randrange(20, PLATE_H - 20))
        drift = rng.choice([-0.8, -0.45, 0.45, 0.8])
        length = rng.randrange(60, 170)
        for step in range(length):
            drift += rng.uniform(-0.18, 0.18)
            x += drift * rng.uniform(0.3, 1.0)
            y += 1.0
            if not (2 <= x < PLATE_W - 2 and 2 <= y < PLATE_H - 2):
                break
            c.set(int(x), int(y), OCC)
            # The lip catches the key. Skipped intermittently so it is a broken highlight, not a
            # parallel line, which would read as a seam between two slabs instead of a fissure.
            if step % 3:
                c.set(int(x) - 1, int(y), bl.tone("stone", "hi"))

    # Worn bevel: the slab is a raised object, lit top-left, shaded bottom-right.
    c.hline(0, last_x, 0, bl.tone("stone", "hi"))
    c.hline(0, last_x, 1, bl.tone("stone", "lit"))
    c.vline(0, 0, last_y, bl.tone("stone", "hi"))
    c.vline(1, 0, last_y, bl.tone("stone", "lit"))
    c.hline(0, last_x, last_y, OCC)
    c.hline(0, last_x, last_y - 1, bl.tone("stone", "deep"))
    c.vline(last_x, 0, last_y, OCC)
    c.vline(last_x - 1, 0, last_y, bl.tone("stone", "deep"))

    # Corner blocks, so the slab has quarried ends rather than mitred picture-frame corners.
    for (cx, cy) in ((0, 0), (last_x - 7, 0), (0, last_y - 7), (last_x - 7, last_y - 7)):
        c.rect(cx, cy, cx + 7, cy + 7, bl.tone("stone", "lit"))
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
