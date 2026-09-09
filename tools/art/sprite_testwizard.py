# -*- coding: utf-8 -*-
"""The test wizard: a supplied sprite, re-cut to the player cell and given an idle.

This one breaks the project's usual rule that art is drawn by code, and does it deliberately.
`tools/art/src/wizarddude.png` is Eric's own drawing; the job here is not to invent a wizard but
to take that exact art, put it on the grid the game expects, and derive a few frames of life from
it without repainting a pixel. Every colour in every output frame is a colour that was already in
the source.

It is also, on purpose, NOT Bonelight. Hard black outlines, saturated primaries, a cream face -
none of it is on the contract palette and none of it should be. `test_wizard` is the sandbox
character (`StartingPassiveDescription`: "Choose any starting weapon on Stage Select"), so a skin
that is instantly distinguishable from every shipping character is a feature. Do not "fix" its
palette.

WHY THE SOURCE IS CHECKED IN. It was supplied from a Desktop folder, which exists on exactly one
machine. A generator whose input is not in the repo is not a generator, it is a one-time script
that will fail for the next person who runs `python tools/art/build.py`.

THE CELL IS 48x48, matching the player row in `.ai/art-direction.md` section 1. The source art is
21x43 of content in a 40x45 canvas, anchored to the top-left with dead space on two sides, so it
is cropped to its own bounding box and re-seated: centred horizontally, feet on the contract's
anchor row. Handing Godot the raw file would have put the figure off-centre and left the sprite's
origin somewhere in the empty quarter.

THE IDLE IS DERIVED, NOT DRAWN. Four frames, built by moving bands of the source against each
other:

    frame       0     1     2     3
    body dy     0    -1    -1     0     the whole figure breathes
    hat tip dx  0     0    +1    +1     rows 0-7, leading
    brim dx     0     0     0    +1     rows 8-12, following a frame behind

The lag between tip and brim is the whole trick: shift them together and the hat is a rigid cone
that slides, shift the tip first and the hat reads as cloth with something heavy at the end of it.
One pixel is enough at this size.

The bob is one pixel and moves the whole figure, feet included. Two would read better but would
break the contract's anchor rule (feet on row cell-2, contact on cell-1, every frame), and an
anchor that drifts is how a sprite ends up appearing to skate along the floor.
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "src", "wizarddude.png")

CELL = 48
FEET_ROW = CELL - 2          # contract section 5

# Row bands, measured off the source after cropping. Read from the art rather than guessed: the
# brim is the widest run (x 1..17 at rows 9-10) and the skin starts the row after it ends.
TIP_ROWS = (0, 7)            # the pointed part, above the brim
BRIM_ROWS = (8, 12)          # the wide flat part

# (body dy, tip dx, brim dx) per frame.
IDLE = [(0, 0, 0), (-1, 0, 0), (-1, 1, 0), (0, 1, 1)]


def _load():
    im = Image.open(SOURCE).convert("RGBA")
    box = im.getbbox()
    if box is None:
        raise ValueError("%s is empty" % SOURCE)
    return im.crop(box)


def _frame(art, body_dy, tip_dx, brim_dx):
    """One idle frame: the cropped art seated in the cell, with two bands nudged sideways."""
    w, h = art.size
    out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    ox = (CELL - w) // 2
    oy = FEET_ROW - h + 1 + body_dy

    # Everything below the hat goes down first, as one piece.
    below = art.crop((0, BRIM_ROWS[1] + 1, w, h))
    out.alpha_composite(below, (ox, oy + BRIM_ROWS[1] + 1))
    # Then the two hat bands, each with its own horizontal offset. Pasted after the body so a
    # brim that leans over the shoulders still covers them rather than being covered.
    brim = art.crop((0, BRIM_ROWS[0], w, BRIM_ROWS[1] + 1))
    out.alpha_composite(brim, (ox + brim_dx, oy + BRIM_ROWS[0]))
    tip = art.crop((0, TIP_ROWS[0], w, TIP_ROWS[1] + 1))
    out.alpha_composite(tip, (ox + tip_dx, oy + TIP_ROWS[0]))
    return out


def frames():
    art = _load()
    return [_frame(art, *step) for step in IDLE]


def check(fr):
    """The anchor rule, checked here because pixel.check_anchor works on char grids, not images."""
    problems = []
    for i, im in enumerate(fr):
        box = im.getbbox()
        if box is None:
            problems.append("test wizard frame %d is empty" % i)
            continue
        bottom = box[3] - 1
        if bottom < FEET_ROW - 1 or bottom > FEET_ROW + 1:
            problems.append("test wizard frame %d: lowest pixel on row %d, expected %d +-1"
                            % (i, bottom, FEET_ROW))
    return problems


def main():
    out = os.path.join(HERE, "_testwizard")
    if not os.path.isdir(out):
        os.makedirs(out)
    fr = frames()
    for i, im in enumerate(fr):
        im.save(os.path.join(out, "testwizard-%d.png" % (i + 1)))
    sheet = Image.new("RGBA", (CELL * len(fr) + 4 * (len(fr) - 1), CELL), (30, 38, 52, 255))
    for i, im in enumerate(fr):
        sheet.alpha_composite(im, (i * (CELL + 4), 0))
    sheet.resize((sheet.size[0] * 6, CELL * 6), Image.NEAREST).save(
        os.path.join(out, "_idle.png"))
    for p in check(fr):
        print("   ANCHOR: " + p)
    print("test wizard: %d frames, %dx%d" % (len(fr), CELL, CELL))
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
