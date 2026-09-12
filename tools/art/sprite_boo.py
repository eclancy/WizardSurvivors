# -*- coding: utf-8 -*-
"""Boo: Eric's own two-colour ghost, re-seated and given the animations it never had.

Run: python tools/art/sprite_boo.py     -> tools/art/_boo/ (gitignored)

THIS EXTENDS HIS DRAWING, IT DOES NOT REPLACE IT. `assets/enemies/booscaryspritesheet.png` is two
17x15 frames in pure black and white with a hard outline, and every frame this file writes is
built from those two. No pixel here is a colour that was not already in the source.

WHY IT IS NOT ON THE BONELIGHT PALETTE, deliberately. Boo is black and white with an outline,
which `.ai/art-direction.md` forbids for actors - and it is *supposed* to look like that. It is a
sheet ghost. Forcing it onto the contract's five-tone ramps would make it a grey blob and throw
away the one thing that makes it read instantly in a crowd. Treated as a deliberate exception,
the way the test wizard is, rather than as art that has not been converted yet.

THE CELL IS 32x32 AND THE SCENE SCALE BECOMES 3. The source is 17x15 drawn at `scale = 6.75`,
which lands at about 115px and is a fractional scale under nearest filtering - the exact failure
the grid rules exist to prevent.

The obvious fix, seating it on the 48x48 elite cell at the contract's x2, is wrong: 17x15 inside
48x48 is mostly empty cell, and at x2 Boo would render at 34px - smaller than a basic enemy,
when it is a miniboss. The cell has to fit the ART, not the other way round. 32x32 holds the
source plus the room the lunge and the death drift need, and x3 puts it at 96px - near the size
it already had, and an integer scale at last.

What this does NOT do is upscale the source. Section 1 of the contract is explicit that art is
authored 1:1 with no integer upscales in source files, so Boo stays 17x15 in a larger cell and
the scene carries the scale.

NO GROUND CONTACT, and that is the one contract rule this sprite is allowed to break. Section 5
wants feet on row `cell-2`; Boo has no feet. It hovers, its idle is a float, and anchoring it to
the floor would be drawing a ghost standing up.

THE FOUR ANIMATIONS, each derived from what a sheet ghost can actually do:

  moving  the two source frames alternating on a slow float, so his own animation survives
  attack  a lunge - stretch toward the target, then snap back
  hurt    INVERTED. Two colours means there is no lighter tone to flash to, so the flash is
          black-on-white becoming white-on-black. It costs no new colour and at two frames it
          reads as a hit rather than as a different sprite
  death   it unravels. A ghost should not topple like a body, so this erodes the silhouette
          from the outside in while drifting upward, and what is left last is the face
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SOURCE = os.path.join(ROOT, "assets", "enemies", "booscaryspritesheet.png")

CELL = 32
SRC_W, SRC_H = 17, 15

BLACK = (0, 0, 0, 255)
WHITE = (255, 255, 255, 255)


def _load():
    """The two source frames, cleaned to hard alpha.

    The sheet carries a handful of pixels at alpha 1-17 - resampling debris from whatever it was
    exported from. The contract allows no partial alpha, and more practically a stray alpha-2
    pixel becomes a visible grey speck the moment anything composites it.
    """
    im = Image.open(SOURCE).convert("RGBA")
    out = []
    for i in range(2):
        f = im.crop((i * SRC_W, 0, (i + 1) * SRC_W, SRC_H))
        px = f.load()
        for y in range(SRC_H):
            for x in range(SRC_W):
                r, g, b, a = px[x, y]
                if a < 128:
                    px[x, y] = (0, 0, 0, 0)
                else:
                    px[x, y] = WHITE if (r + g + b) > 382 else BLACK
        out.append(f)
    return out


def _seat(art, dx=0, dy=0):
    """Centre the source in the cell, with an optional offset, clipping at the cell edge.

    The clipping is not defensive padding - the death drifts the ghost up out of frame on
    purpose, so the last frames genuinely hang over the top edge. `alpha_composite` refuses a
    negative destination outright, so the source is cropped to whatever still lands inside.
    """
    out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    x = (CELL - art.size[0]) // 2 + dx
    y = (CELL - art.size[1]) // 2 + dy
    cx0, cy0 = max(0, -x), max(0, -y)
    cx1 = min(art.size[0], CELL - x)
    cy1 = min(art.size[1], CELL - y)
    if cx1 <= cx0 or cy1 <= cy0:
        return out
    out.alpha_composite(art.crop((cx0, cy0, cx1, cy1)), (max(0, x), max(0, y)))
    return out


def _stretch(art, sx, sy):
    """Scale the source before seating it. Nearest only - this is pixel art being posed, not
    resampled, and a smooth scale would invent tones the sprite does not own."""
    w = max(1, int(round(art.size[0] * sx)))
    h = max(1, int(round(art.size[1] * sy)))
    return art.resize((w, h), Image.NEAREST)


def _invert(frame):
    """Swap black and white, leaving transparency alone. The flinch flash for a two-colour
    sprite: there is no lighter tone to step to, so the whole thing reverses."""
    out = frame.copy()
    px = out.load()
    for y in range(out.size[1]):
        for x in range(out.size[0]):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = BLACK if (r + g + b) > 382 else WHITE
    return out


def _erode(frame, passes):
    """Eat the silhouette from the outside in. A pixel with a transparent neighbour goes.

    This is the death. A ghost that falls over is a costume; a ghost that comes apart at its edges
    is a ghost. Because erosion works inward, the last thing left is whatever is furthest from the
    outline - which on this drawing is the face.
    """
    out = frame.copy()
    for _ in range(passes):
        px = out.load()
        doomed = []
        for y in range(out.size[1]):
            for x in range(out.size[0]):
                if not px[x, y][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < out.size[0] and 0 <= ny < out.size[1]) or not px[nx, ny][3]:
                        doomed.append((x, y))
                        break
        for x, y in doomed:
            px[x, y] = (0, 0, 0, 0)
    return out


def animations():
    a, b = _load()

    # moving: his own two frames, floating. Eight frames so it shares a clock with every other
    # enemy in the roster, and a bob that is not a straight up-down or it reads as a bounce.
    BOB = [0, -1, -2, -2, -1, 0, 1, 1]
    moving = [_seat(a if i % 4 < 2 else b, 0, BOB[i]) for i in range(8)]

    # attack: gather, then lunge. The stretch is on ONE axis at a time - squashing while moving
    # forward is what makes a lunge read as thrown weight rather than as a zoom.
    attack = [
        _seat(_stretch(a, 1.0, 0.90), 0, 2),
        _seat(_stretch(a, 0.92, 1.10), 0, -1),
        _seat(_stretch(b, 1.25, 0.86), 5, 1),
        _seat(_stretch(b, 1.15, 0.92), 7, 0),
        _seat(_stretch(a, 1.0, 1.0), 3, 0),
        _seat(a, 0, 0),
    ]

    hurt = [_invert(_seat(a, -3, 0)), _invert(_seat(a, -1, 0))]

    death = [
        _seat(_erode(a, 0), 0, -1),
        _seat(_erode(a, 1), 0, -3),
        _seat(_erode(b, 2), 0, -5),
        _seat(_erode(b, 3), 1, -6),
        _seat(_erode(b, 4), -1, -8),
        _seat(_erode(b, 5), 0, -10),
    ]

    return [("moving", moving, True, 6.0), ("attack", attack, False, 12.0),
            ("hurt", hurt, False, 12.0), ("death", death, False, 10.0)]


def main():
    out = os.path.join(HERE, "_boo")
    if not os.path.isdir(out):
        os.makedirs(out)
    for name, frames, _loop, _speed in animations():
        sheet = Image.new("RGBA", (CELL * len(frames), CELL), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            sheet.alpha_composite(f, (i * CELL, 0))
        sheet.save(os.path.join(out, "boo-%s.png" % name))
        empty = [i for i, f in enumerate(frames) if not f.getbbox()]
        print("  %-7s %d frames%s" % (name, len(frames),
                                      "  (empty: %s)" % empty if empty else ""))
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
