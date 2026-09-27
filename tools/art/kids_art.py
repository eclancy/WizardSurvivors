# -*- coding: utf-8 -*-
"""Slice the kids' sprite sheets into Godot strips. THE DRAWINGS THEMSELVES ARE NOT TOUCHED.

Run: python tools/art/kids_art.py
Shipping build: python tools/art/build.py

WHAT THIS IS. Twelve sprite sheets drawn by Eric's kids, wired into a chapter of their own. They
are the only art in the project that is not generated, and the only art that is deliberately
OFF-CONTRACT: `.ai/art-direction.md` governs the Bonelight cast, and none of it applies here. No
palette audit, no material ramps, no occlusion rule, no cell size. The precedent is
`assets/testwizard/`, which keeps Eric's own drawing in its own directory for exactly this reason
- the path says "this is not generated art" before anyone opens the file.

THE ONE RULE THIS FILE OBEYS: **not one pixel of the drawings is altered.** Everything below is
cropping, repacking and transparency - operations that move pixels between files without changing
any of them. Specifically it does NOT recolour, does not resample, does not fill, does not derive
hurt or death frames, and does not scale. A generated `death` animation would be this tool
inventing frames the kids did not draw and putting them on screen under their name, and the
engine already has the right answer for a sheet without one: `Enemy.StartDeath` frees immediately
when `SpriteFrames` has no `death`, so these enemies simply vanish when killed. That is a real
behaviour the game already supports, not a compromise.

THREE THINGS IT HAS TO DO, and why each is only a move rather than an edit:

  FRAME BOUNDARIES ARE HAND-WRITTEN. The sheets are not on a uniform grid - they were drawn by
  eye, so the frames are different widths and some touch. Auto-detecting blank columns found the
  right answer for eleven of the twelve and split the vampire wrong, because its two coffins are
  adjacent with no gap between them. So the table below is the measured answer with that one
  corrected by hand, which is both more honest and more stable than a detector that is right most
  of the time.

  THE STRIP IS SQUARE-CELLED. `spriteframes.write` addresses each frame as Rect2(f*cell, 0, cell,
  cell) - a square - because that is what every other SpriteFrames in this project is. So each
  frame is centred horizontally in a square cell and the sheet's content rows are bottom-aligned
  in it. The vertical window is computed ONCE per sheet and shared by all its frames, which is
  what keeps a jump a jump: blobby leaves the ground in frame two, and per-frame alignment would
  have quietly put it back down.

  ROCKEM IS A JPEG ON WHITE. It is the only one without an alpha channel, so its background is
  keyed out. The threshold is 224 and the rock itself is mid-grey around 150, so there is no
  chance of eating the drawing - but it is worth saying that JPEG ringing means the edge pixels
  are not pure white, which is why it is a threshold and not an equality test.
"""
import io
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import spriteframes

SRC = os.path.join(ROOT, "assets", "kidsart", "source")
OUT = os.path.join(ROOT, "assets", "kidsart")
RES = os.path.join(ROOT, "scenes", "resources")
ASSET_DIR = "assets/kidsart"

WHITE_KEY = 224


# name -> (SpriteFrames stem, [(source file, animation, loop, fps, [(x0, x1), ...])])
#
# The x ranges are inclusive and were measured off the sheets, not guessed. Anything not listed
# here is a prop rather than an actor and is handled by PROPS below.
SHEETS = [
    ("kid-smiler", "KidSmilerFrames", [
        ("SmiLeRspritesheet.png", "moving", True, 6.0, [(1, 21), (23, 43)]),
    ]),
    ("kid-oneeye", "KidOneEyeFrames", [
        ("oneeyedudespritesheet.png", "moving", True, 6.0, [(1, 17), (19, 35)]),
    ]),
    ("kid-zombie", "KidZombieFrames", [
        ("zombiebrainsspritesheet.png", "moving", True, 5.0, [(1, 11), (13, 24), (26, 37)]),
    ]),
    # Four frames: two coffins and then two vampires. The coffins touch, so the split at 8 is
    # hand-placed - a blank-column detector reads the pair as one 14-wide frame.
    ("kid-vampire", "KidVampireFrames", [
        ("vampiredudespritesheet.png", "moving", True, 5.0, [(1, 7), (8, 14), (19, 26), (29, 36)]),
    ]),
    ("kid-pushy", "KidPushyFrames", [
        ("pushydudespritesheet.png", "moving", True, 7.0, [(0, 21), (23, 44), (46, 67), (69, 90)]),
    ]),
    # The only actor built from two files, and the only one with an attack. "blobbyjump" is the
    # squash-and-stretch on the spot and "blobbymorejump" is the leap, which maps exactly onto
    # LungerEnemy: walk, then dash. The kids drew a lunger without being asked for one.
    ("kid-blobby", "KidBlobbyFrames", [
        ("blobbyjump.png", "moving", True, 6.0, [(1, 20), (22, 31), (36, 52)]),
        ("blobbymorejump.png", "attack", False, 8.0, [(2, 20), (22, 40)]),
    ]),
    ("kid-rockem", "KidRockemFrames", [
        ("Rockem.jpg", "moving", True, 4.0, [(7, 29), (36, 64)]),
    ]),
    ("kid-wizard", "KidWizardFrames", [
        ("wizarddude.png", "moving", True, 4.0, [(0, 20)]),
    ]),
    ("kid-missile", "KidMissileFrames", [
        ("magicmissel.png", "moving", True, 10.0, [(1, 25), (27, 51)]),
    ]),
]

# Not actors. Scattered across the chapter as decor, one PNG per drawing, so both the dog and
# both faces of the message turn up in the world rather than only the first of each.
PROPS = [
    ("kid-dog", "dogcompanionspritesheet.png", [(0, 26), (28, 54)]),
    ("kid-message", "friendlymessagespritesheet.png", [(7, 22), (25, 40)]),
]


def load(name):
    """Open a sheet as RGBA, keying white out of the one that has no alpha of its own."""
    img = Image.open(os.path.join(SRC, name))
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    if not name.lower().endswith(".jpg"):
        return img

    px = img.load()
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            r, g, b, _a = px[x, y]
            if r >= WHITE_KEY and g >= WHITE_KEY and b >= WHITE_KEY:
                px[x, y] = (0, 0, 0, 0)
    return img


def content_rows(img, ranges):
    """The first and last row with anything on it, across every frame of a sheet.

    Shared by all frames on purpose. Cropping each frame to its own rows would flatten the jump
    in blobbyjump, which is three frames whose whole difference is vertical position.
    """
    px = img.load()
    top, bottom = None, None
    for y in range(img.size[1]):
        used = False
        for (x0, x1) in ranges:
            for x in range(x0, x1 + 1):
                if px[x, y][3] > 8:
                    used = True
                    break
            if used:
                break
        if used:
            top = y if top is None else top
            bottom = y
    return (0, img.size[1] - 1) if top is None else (top, bottom)


def build_strip(img, ranges, cell):
    """One horizontal strip of square cells, each frame centred and the rows bottom-aligned."""
    top, bottom = content_rows(img, ranges)
    height = bottom - top + 1
    strip = Image.new("RGBA", (cell * len(ranges), cell), (0, 0, 0, 0))
    for i, (x0, x1) in enumerate(ranges):
        frame = img.crop((x0, top, x1 + 1, bottom + 1))
        dx = i * cell + (cell - frame.size[0]) // 2
        # One pixel of floor margin, so nothing sits flush against the cell edge where a filtered
        # neighbour could bleed into it.
        dy = cell - height - 1
        strip.alpha_composite(frame, (dx, max(0, dy)))
    return strip


def cell_for(img, ranges):
    """Square, sized to whichever is bigger: the widest frame or the sheet's content height."""
    top, bottom = content_rows(img, ranges)
    widest = max(x1 - x0 + 1 for (x0, x1) in ranges)
    return max(widest, bottom - top + 1) + 2


def paper_tile(variant=0):
    """The ground the chapter is fought on: a page of squared paper.

    THIS ONE IS GENERATED AND THE DRAWINGS ARE NOT, which is worth being loud about. Everything
    else this file writes is the kids' pixels moved between files. This is a background drawn
    here, and it exists because their art was made on white and needs something to sit on that
    says why: every other chapter is a place, and this one is a page.

    It is also the only ground in the game that is pale. That is the point - the chapter should be
    visibly not part of the campaign from the first frame, before anything is read.
    """
    size = 48
    img = Image.new("RGBA", (size, size), (214, 203, 176, 255))
    px = img.load()
    # Four variants rather than one. The ground layer picks between them per cell, and a single
    # tile repeated across a 181x181 grid is a visible lattice - which on squared paper is
    # especially bad, because the ruling makes the repeat easy to count.
    v = variant * 7

    # Fibre: two tones of speckle, sparse. Flat cream tiles at 48px and the seams between them
    # become a visible grid of their own, which reads as a chessboard rather than as paper.
    for y in range(size):
        for x in range(size):
            n = (x * 7 + y * 13 + (x * y) % 11 + v * 3) % 17
            if n == 0:
                px[x, y] = (205, 193, 164, 255)
            elif n == 5:
                px[x, y] = (222, 212, 188, 255)

    # Ruling: faint blue, every 12px, and deliberately NOT at the tile edge - a line on the seam
    # doubles up against the next tile and comes out twice as dark as the others.
    rule = (150, 160, 186, 255)
    for k in (5, 17, 29, 41):
        for i in range(size):
            px[k, i] = rule
            px[i, k] = rule

    # A pencil smudge, off-centre, so the tiling has something to break its own rhythm on.
    for (sx, sy, sw) in ((9 + v, 34, 6), (33, 11 + v, 4), (24, 26, 3 + variant)):
        for i in range(sw):
            px[(sx + i) % size, sy] = (176, 166, 144, 255)
            px[(sx + i) % size, (sy + 1) % size] = (192, 182, 158, 255)

    name = "paper-tile.png" if variant == 0 else "paper-tile-%d.png" % variant
    img.save(os.path.join(OUT, name))
    return name


PAPER_VARIANTS = 4


def paper_ground():
    """The paper tiles, plus a tile manifest so the terrain painter can use them as ground.

    The painter reads curated packs through a metadata.json, and the right way in for a tile of
    ours is a manifest of our own rather than an edit to the bought pack's - mixing our art into
    somebody else's inventory file is how an asset update silently deletes it. CuratedTileCatalog
    merges both.
    """
    names = [paper_tile(i) for i in range(PAPER_VARIANTS)]
    frames = []
    for i, name in enumerate(names):
        frames.append(
            '  {"id": "kidpaper_%d", "fileName": "%s", "path": "%s/%s",\n'
            '   "placement": "floor", "materialFamily": "paper", "usageClass": "tile",\n'
            '   "collision": "decoration", "hazard": false, "width": 48, "height": 48}'
            % (i, name, ASSET_DIR, name))

    manifest = (
        '{\n'
        # ONE LINE, however long it gets. A raw newline inside a JSON string is invalid and
        # JsonDocument.Parse throws on it, which takes the whole ground layer down with it.
        '  "_comment": "Generated by tools/art/kids_art.py - the Sketchbook ground. The only '
        'tile manifest in this project that is not a bought pack, kept separate so that updating '
        'a pack cannot take it with it. CuratedTileCatalog.LoadMany merges the two.",\n'
        '  "frames": [\n%s\n  ]\n}\n' % (",\n".join(frames)))
    path = os.path.join(OUT, "tiles.json")
    io.open(path, "w", encoding="utf-8", newline="\n").write(manifest.decode("utf-8")
                                                              if hasattr(manifest, "decode") else manifest)
    print("%-14s generated  -> %d paper tiles + tiles.json (NOT the kids' art - the page)"
          % ("paper", PAPER_VARIANTS))


def main():
    if not os.path.isdir(SRC):
        print("no source directory at %s" % SRC)
        return
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    total = 0
    for slug, sheet_name, anims in SHEETS:
        # One cell size for the whole SpriteFrames, because the .tres addresses every animation
        # with the same square. Taken as the largest any of its animations needs.
        loaded = [(load(src), anim, loop, fps, ranges) for (src, anim, loop, fps, ranges) in anims]
        cell = max(cell_for(img, ranges) for (img, _a, _l, _f, ranges) in loaded)

        spec = []
        for (img, anim, loop, fps, ranges) in loaded:
            png = "%s-%s.png" % (slug, anim)
            build_strip(img, ranges, cell).save(os.path.join(OUT, png))
            spec.append((anim, png, len(ranges), loop, fps))

        spriteframes.write(os.path.join(RES, "%s.tres" % sheet_name), spec, ASSET_DIR, cell=cell)
        frames = sum(a[2] for a in spec)
        total += frames
        print("%-14s cell %2d  %2d frames  -> %s.tres" % (slug, cell, frames, sheet_name))

    for slug, src, ranges in PROPS:
        img = load(src)
        top, bottom = content_rows(img, ranges)
        for i, (x0, x1) in enumerate(ranges):
            png = "%s-%d.png" % (slug, i + 1)
            img.crop((x0, top, x1 + 1, bottom + 1)).save(os.path.join(OUT, png))
            print("%-14s prop      -> %s" % (slug, png))
            total += 1

    paper_ground()
    print("%d frames from 12 drawings, none of them altered" % total)


if __name__ == "__main__":
    main()
