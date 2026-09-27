# -*- coding: utf-8 -*-
"""The application icon: what the Godot project manager, the taskbar and the .exe show.

Run: python tools/art/app_icon.py   -> icon.png (256x256) and icon.ico at the repo root

It replaced the stock Godot robot, which had been sitting in `config/icon` since the project was
created and is the single most visible piece of art in the project for anyone who has not opened
the game yet.

DRAWN FOR 16 PIXELS, NOT FOR 256. That is the whole design constraint and it is the one most app
icons fail. The project manager shows this small, the taskbar smaller, and at that size an icon is
not a picture - it is a silhouette and at most two tones. Anything with a face, a pose or a scene
in it becomes mud. So the mark is two shapes only:

    a pointed hat, in the dark
    a light underneath it

Which is also, exactly, what `.ai/world-and-tone.md` says the game is about: the player is alone
in a drained world and carries the only light in it. The hat says wizard, the glow says the rest,
and the two of them together are legible as a thumbnail because one is a hard black triangle and
the other is the brightest thing in the palette.

WHY IT IS PIXEL ART AND NOT A SMOOTH LOGO. Everything else in this project is generated pixel art
on the Bonelight palette (`.ai/art-direction.md` section 3) and an icon in another idiom would be
the first thing a player sees and the only thing that does not look like the game. It is drawn on
a 64x64 logical grid and scaled by an integer factor, so no pixel is ever a fraction of another.
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import bonelight as B

CELL = 64
SCALE = 4                     # 64 -> 256, integer, nearest
OUT_PNG = os.path.join(ROOT, "icon.png")
OUT_ICO = os.path.join(ROOT, "icon.ico")

# Every colour comes out of the contract table rather than being picked here.
OCC = B.OCC
FIRE = B.ELEMENTS["fire"]           # core / hot / mid / edge
ARCANE = B.ELEMENTS["arcane"]
STONE = B.MATERIALS["stone"]        # hi / lit / base / shade / deep
VIOLET = B.MATERIALS["violet"]


def rgb(hex_string):
    h = hex_string.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


# PIL on this machine is old enough to have no rounded_rectangle and no width= on arc, and both
# would have been the wrong tool anyway: they antialias, and an antialiased edge on a 64px grid
# scaled up by 4 is a smear of colours that are not in the palette. Everything here is spans.

def span(d, y, x0, x1, colour):
    if x1 < x0:
        return
    d.rectangle([int(round(x0)), int(y), int(round(x1)), int(y)], fill=colour)


def rounded(d, inset, radius, colour):
    """A rounded square, filled row by row, with no antialiasing anywhere."""
    lo, hi = inset, CELL - 1 - inset
    for y in range(lo, hi + 1):
        cut = 0
        if y < lo + radius:
            dy = lo + radius - y
            cut = radius - int((radius * radius - dy * dy) ** 0.5)
        elif y > hi - radius:
            dy = y - (hi - radius)
            cut = radius - int((radius * radius - dy * dy) ** 0.5)
        span(d, y, lo + cut, hi - cut, colour)


def disc(d, cx, cy, rx, ry, colour):
    """A filled ellipse, row by row. Same reason as rounded()."""
    for y in range(int(cy - ry), int(cy + ry) + 1):
        t = (y - cy) / float(max(0.001, ry))
        if abs(t) > 1.0:
            continue
        half = rx * (1.0 - t * t) ** 0.5
        span(d, y, cx - half, cx + half, colour)


def rounded_field(d):
    """The plate everything sits on: deep stone with a hard occlusion border.

    A bordered field rather than a free-floating figure, because an icon with transparent corners
    disappears against a dark taskbar - and this game is dark enough that the figure alone would
    be a black hat on black.
    """
    rounded(d, 0, 11, rgb(OCC))
    rounded(d, 1, 10, rgb(STONE[3]))
    rounded(d, 3, 9, rgb(STONE[4]))


def light(d):
    """The ward, BEHIND the hat: concentric discs from the fire ramp out to a white-hot core.

    Wider than the hat brim on purpose, so it reads as light coming from behind an object rather
    than as something the object is sitting in.
    """
    cx, cy = 32, 41
    for radius, tone in ((23, FIRE[3]), (17, FIRE[2]), (11, FIRE[1]), (5, FIRE[0])):
        disc(d, cx, cy, radius, radius * 0.86, rgb(tone))


def hat(d):
    """The entire figure, and it is only a hat.

    TWO PASSES WERE THROWN AWAY BEFORE THIS ONE, and both failed the same way - by drawing more.
    The first put the eyes inside the glow and produced a jack-o-lantern. The second added a head
    and shoulders, which merged with the crown into one black triangle that swallowed the brim and
    read as a tree.

    At sixteen pixels an icon is a silhouette and a silhouette is one shape. So this is one shape:
    a bent cone on a wide brim, with the light behind it. No head, no body, no eyes. The brim is
    the widest dark thing and the crown BENDS - a symmetric cone is a traffic cone, and those two
    facts are the whole difference between a wizard and an arrow.
    """
    # crown: short, and clearly leaning. Short matters - a tall crown reaches the plate edge and
    # the bend stops being visible against it.
    for i in range(25):
        t = i / 24.0
        y = 9 + i
        half = 1.5 + 9.0 * (t ** 1.25)
        lean = 9.0 * ((1.0 - t) ** 1.9)
        span(d, y, 32 - half + lean, 32 + half + lean, rgb(OCC))

    # the band: the one piece of interior detail, and it costs nothing at 16px because it sits
    # inside a mass that is solid there anyway.
    for y in (29, 30, 31):
        span(d, y, 22, 42, rgb(VIOLET[3]))
    span(d, 29, 22, 42, rgb(VIOLET[1]))

    # brim: wide, flat, and slightly turned up at the ends so it is a hat and not a table
    span(d, 33, 15, 49, rgb(OCC))
    span(d, 34, 12, 52, rgb(OCC))
    span(d, 35, 11, 53, rgb(OCC))
    span(d, 36, 13, 51, rgb(OCC))
    span(d, 32, 11, 14, rgb(OCC))
    span(d, 32, 50, 53, rgb(OCC))

    # A warm bounce along the underside of the brim, where the light below it would actually
    # catch. The contract asks actors for a key-facing rim; here it is also the only thing keeping
    # the brim from dissolving into the glow it overlaps.
    span(d, 37, 14, 50, rgb(FIRE[3]))
    span(d, 36, 11, 12, rgb(FIRE[2]))
    span(d, 36, 52, 53, rgb(FIRE[2]))


def ground(d):
    """A dark base under the glow, so the light is standing on something and not floating."""
    for i in range(7):
        y = 53 + i
        half = 20.0 - 1.4 * i
        span(d, y, 32 - half, 32 + half, rgb(OCC))


def draw():
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rounded_field(d)
    light(d)
    ground(d)
    hat(d)
    return img


def main():
    logical = draw()
    big = logical.resize((CELL * SCALE, CELL * SCALE), Image.NEAREST)
    big.save(OUT_PNG)
    print("wrote %s  %dx%d" % (os.path.relpath(OUT_PNG, ROOT), big.size[0], big.size[1]))

    # The .ico is for the exported Windows binary (project.godot config/windows_native_icon).
    # Godot needs it to exist at export time; the project manager reads the PNG above.
    big.save(OUT_ICO, sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("wrote %s  6 sizes" % os.path.relpath(OUT_ICO, ROOT))

    # The honest check: look at it small. If the hat and the light are not both readable here, the
    # icon has failed at the only size that matters.
    preview = Image.new("RGBA", (16 + 32 + 64 + 24, 72), (24, 26, 34, 255))
    x = 6
    for size in (16, 32, 64):
        preview.alpha_composite(logical.resize((size, size), Image.BOX), (x, 6))
        x += size + 6
    preview.save(os.path.join(HERE, "_app_icon_small.png"))
    print("wrote tools/art/_app_icon_small.png - check the 16px one")


if __name__ == "__main__":
    main()
