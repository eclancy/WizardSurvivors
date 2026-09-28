# -*- coding: utf-8 -*-
"""The application icon: what the Godot project manager, the taskbar and the .exe show.

Run: python tools/art/app_icon.py   -> icon.png and icon.ico at the repo root

IT IS A CROP OF THE TITLE SCREEN. The wizard on `assets/bonelight/ui/title-screen.png` is the
best single image this project owns - a bearded figure in a blue hat holding a lit staff, standing
inside a burning ward - and the icon is that, framed. Nothing here draws anything.

That matters for two reasons beyond the obvious one. An icon drawn separately is a second answer
to "what does this game look like", and the two drift the moment either is touched; this cannot
drift, because it IS the title screen. And the title art is itself generated, by `splash.py` and
`anim.py`, so re-running the pipeline updates the icon for free.

**This file only READS the title PNG.** `.ai/title-screen.md` is explicit that the art is
generated and the PNG is never to be hand-edited; reframing a copy of it into an icon does not
touch it, and if the framing wants changing, the four numbers below are what to change.

THE FRAME WAS CHOSEN AT 16 PIXELS, which is where a project manager and a taskbar actually show
it. Four crops were compared side by side at 48, 32 and 16: the whole figure, a wider one taking
in the ward circle and the candles, this tight one, and a head-and-shoulders portrait of the hat
and beard. The portrait is the most legible small and the least recognisable - at 16px it is a
dark triangle over a grey smudge and could be any wizard in any game. This frame keeps the thing
that makes the picture his, the lit staff held up beside him, and at 16px it still reads as a
small blue figure carrying a light.

The crop is square so neither axis is stretched, and it is scaled by an INTEGER factor with
nearest sampling, so no pixel of the original ever becomes a fraction of another.
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

TITLE = os.path.join(ROOT, "assets", "bonelight", "ui", "title-screen.png")
OUT_PNG = os.path.join(ROOT, "icon.png")
OUT_ICO = os.path.join(ROOT, "icon.ico")

# The wizard's square, in title-screen pixels: left, top, side.
#
# The title is 360x640 and the figure layer's own bounding box is x 105-295, y 310-534, so this
# sits just inside him at the sides and a little below him at the bottom - the staff orb lands in
# the top-left corner of the frame and the lit ground is the bottom edge. Cropping to the figure's
# exact bounds instead centres a lot of empty sky.
CROP_X = 104
CROP_Y = 334
CROP_SIZE = 192

# 192 -> 384. An integer factor is the whole point: 1.33x to reach a round 256 would smear a
# pixel-art source, and Godot scales the icon down for display anyway.
SCALE = 2

# What the .ico carries. Windows picks one of these rather than rescaling, so the small ones earn
# their place even though the 256 is the one anybody looks at.
ICO_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def crop_wizard():
    if not os.path.exists(TITLE):
        raise SystemExit("no title art at %s - run tools/art/build.py first" % TITLE)

    title = Image.open(TITLE).convert("RGBA")
    box = (CROP_X, CROP_Y, CROP_X + CROP_SIZE, CROP_Y + CROP_SIZE)
    if box[2] > title.size[0] or box[3] > title.size[1]:
        raise SystemExit("crop %s falls outside the %dx%d title art"
                         % (box, title.size[0], title.size[1]))
    return title.crop(box)


def main():
    wizard = crop_wizard()

    big = wizard.resize((CROP_SIZE * SCALE, CROP_SIZE * SCALE), Image.NEAREST)
    big.save(OUT_PNG)
    print("wrote %s  %dx%d  (title crop at %dx)"
          % (os.path.relpath(OUT_PNG, ROOT), big.size[0], big.size[1], SCALE))

    # Built from a BOX-reduced 256 rather than from the 384 above. BOX area-averages, which is the
    # right reduction for pixel art going small - nearest at these ratios drops whole features,
    # and the staff orb is only a few pixels across by the time it reaches 16.
    base = wizard.resize((256, 256), Image.BOX)
    base.save(OUT_ICO, sizes=ICO_SIZES)
    print("wrote %s  %d sizes" % (os.path.relpath(OUT_ICO, ROOT), len(ICO_SIZES)))

    # The honest check, and the one the framing was decided on: look at it small. If the staff
    # light and the figure under it are not both readable at 16, the frame is wrong.
    zoom = 7
    sizes = (48, 32, 16)
    width = 12 + sum(s * zoom + 12 for s in sizes)
    preview = Image.new("RGBA", (width, 48 * zoom + 24), (34, 36, 44, 255))
    x = 12
    for size in sizes:
        small = wizard.resize((size, size), Image.BOX).resize((size * zoom, size * zoom), Image.NEAREST)
        preview.alpha_composite(small, (x, 12))
        x += size * zoom + 12
    preview.save(os.path.join(HERE, "_app_icon_small.png"))
    print("wrote tools/art/_app_icon_small.png - check the 16px one")


if __name__ == "__main__":
    main()
