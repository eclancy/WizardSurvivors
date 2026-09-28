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
import math
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
    # Two faces, and the animation IS the character: it chases you grinning and then it does not.
    # Slow on purpose - at a walk-cycle frame rate the two faces blur into one flickering blob,
    # and the whole point is that you can see it change its mind about you.
    ("kid-message", "KidMessageFrames", [
        ("friendlymessagespritesheet.png", "moving", True, 1.6, [(7, 22), (25, 40)]),
    ]),
    ("kid-dog", "KidDogFrames", [
        ("dogcompanionspritesheet.png", "moving", True, 7.0, [(0, 26), (28, 54)]),
    ]),
]

# Nothing is decor. The dog and the message were both scattered as scenery in the first pass,
# and both were wrong: the message is a creature with two expressions and the dog is a friendly
# that fights, so putting either on the floor as a picture wasted the one thing each of them is.
# The chapter has no props at all now, which suits a page better than a page with clip art on it.
PROPS = []


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
    """The ground the chapter is fought on: a page of RULED writing paper.

    THIS ONE IS GENERATED AND THE DRAWINGS ARE NOT, which is worth being loud about. Everything
    else this file writes is the kids' pixels moved between files. The page and the doodles on it
    are drawn here, and they exist because their art was made on white and needs something to sit
    on that says why: every other chapter is a place, and this one is a page.

    IT IS RULED, NOT SQUARED, and the spacing is deliberately wide. The first pass was graph
    paper with a line every twelve pixels in both directions, which at a 48px tile is four
    squares across - a fine mesh that read as texture rather than as paper, and on a screen full
    of enemies it was visual noise. Horizontal rules every twenty-four pixels means two per tile:
    the same page, twice the size, and unmistakably something you would write on.

    It is also the only pale ground in the game. That is the point - the chapter should be
    visibly not part of the campaign from the first frame, before anything is read.
    """
    size = 48
    img = Image.new("RGBA", (size, size), (221, 213, 192, 255))
    px = img.load()
    v = variant * 7

    # Fibre, in pairs rather than single pixels. One-pixel speckle at this size is film grain;
    # two-pixel flecks read as the tooth of paper.
    for y in range(size):
        for x in range(size):
            n = (x * 7 + y * 13 + (x * y) % 11 + v * 3) % 23
            if n == 0:
                px[x, y] = (210, 201, 178, 255)
                px[(x + 1) % size, y] = (213, 204, 182, 255)
            elif n == 9:
                px[x, y] = (231, 225, 208, 255)

    # The rules: two per tile, at the SAME rows in every variant. Offsetting them per variant
    # was tried and it is the one thing this tile must never do - the variants are shuffled
    # across the field, so staggered rules break every line into strips and the page reads as
    # torn paper taped together. Only the fibre and the smudges vary; the ruling is the grid.
    rule = (146, 158, 188, 255)
    ghost = (196, 197, 196, 255)
    for y in (7, 31):
        for x in range(size):
            px[x, y] = rule
            px[x, (y + 1) % size] = ghost

    # A pencil smudge and a crease, off the rules so they do not read as more ruling.
    for (sx, sy, sw) in ((9 + v, 18, 7), (33, 41, 5), (24 + variant, 12, 4)):
        for i in range(sw):
            px[(sx + i) % size, sy] = (198, 189, 166, 255)
            px[(sx + i) % size, (sy + 1) % size] = (208, 200, 178, 255)

    name = "paper-tile.png" if variant == 0 else "paper-tile-%d.png" % variant
    img.save(os.path.join(OUT, name))
    return name


# --- what is drawn on the page ------------------------------------------------------------------
# Doodles, scattered as the chapter's decor. Drawn here rather than by the kids because they are
# the PAGE rather than the cast - the margin of a workbook somebody kept coming back to.
#
# They are big on purpose. Scattered at 48 logical pixels rendered at x2 they are a hundred
# across, which is larger than any enemy in the chapter, and that is the right way round: a
# drawing on the page should read as something the page has on it, not as another sprite.

INK = (64, 74, 104, 255)        # biro blue
INK_SOFT = (108, 118, 146, 255)
PENCIL = (118, 112, 100, 255)
RED = (150, 62, 62, 255)

DOODLE = 48


def _d():
    return Image.new("RGBA", (DOODLE, DOODLE), (0, 0, 0, 0))


def _line(px, x0, y0, x1, y1, col):
    n = int(max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        x, y = int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t))
        if 0 <= x < DOODLE and 0 <= y < DOODLE:
            px[x, y] = col


def _circle(px, cx, cy, r, col, step=8):
    for d in range(0, 360, step // 2):
        a = math.radians(d)
        x, y = int(round(cx + math.cos(a) * r)), int(round(cy + math.sin(a) * r))
        if 0 <= x < DOODLE and 0 <= y < DOODLE:
            px[x, y] = col


def doodle_stick_figure():
    img = _d(); px = img.load()
    _circle(px, 24, 10, 6, INK)
    _line(px, 24, 16, 24, 32, INK)
    _line(px, 24, 20, 13, 26, INK)
    _line(px, 24, 20, 35, 26, INK)
    _line(px, 24, 32, 15, 44, INK)
    _line(px, 24, 32, 33, 44, INK)
    px[21, 9] = INK; px[27, 9] = INK
    _line(px, 21, 13, 27, 13, INK)
    return img


def doodle_sun():
    img = _d(); px = img.load()
    _circle(px, 24, 24, 10, RED)
    _circle(px, 24, 24, 9, RED)
    for d in range(0, 360, 30):
        a = math.radians(d)
        _line(px, 24 + math.cos(a) * 13, 24 + math.sin(a) * 13,
              24 + math.cos(a) * 21, 24 + math.sin(a) * 21, RED)
    return img


def doodle_spiral():
    img = _d(); px = img.load()
    for i in range(240):
        t = i / 239.0
        a = t * math.pi * 6
        r = 2 + t * 20
        x, y = int(round(24 + math.cos(a) * r)), int(round(24 + math.sin(a) * r))
        if 0 <= x < DOODLE and 0 <= y < DOODLE:
            px[x, y] = INK_SOFT if i % 3 else INK
    return img


def doodle_scribble():
    """A block scribbled out. Everybody has done this to a page."""
    img = _d(); px = img.load()
    for k in range(13):
        y = 8 + k * 2.6
        _line(px, 6, y, 41, y + 2, PENCIL)
        _line(px, 41, y + 2, 6, y + 5, PENCIL)
    _line(px, 5, 7, 42, 7, INK)
    _line(px, 5, 41, 42, 41, INK)
    _line(px, 5, 7, 5, 41, INK)
    _line(px, 42, 7, 42, 41, INK)
    return img


def doodle_star():
    img = _d(); px = img.load()
    pts = []
    for k in range(5):
        a = -math.pi / 2 + k * math.pi * 4 / 5
        pts.append((24 + math.cos(a) * 20, 24 + math.sin(a) * 20))
    for k in range(5):
        _line(px, pts[k][0], pts[k][1], pts[(k + 1) % 5][0], pts[(k + 1) % 5][1], INK)
    return img


def doodle_writing():
    """Four lines of fake handwriting. The most paper-ish thing here and the least a picture."""
    img = _d(); px = img.load()
    for row in range(4):
        y = 10 + row * 10
        x = 5
        while x < 42:
            run = 3 + (x + row) % 5
            for i in range(run):
                yy = int(y + math.sin((x + i) * 0.9) * 1.6)
                if 0 <= yy < DOODLE:
                    px[x + i, yy] = INK
                    if (x + i) % 3 == 0 and yy + 1 < DOODLE:
                        px[x + i, yy + 1] = INK_SOFT
            x += run + 2
    return img


def doodle_house():
    img = _d(); px = img.load()
    _line(px, 8, 42, 8, 22, INK); _line(px, 40, 42, 40, 22, INK)
    _line(px, 8, 42, 40, 42, INK)
    _line(px, 5, 22, 24, 7, INK); _line(px, 24, 7, 43, 22, INK)
    _line(px, 5, 22, 43, 22, INK)
    _line(px, 20, 42, 20, 31, INK); _line(px, 28, 42, 28, 31, INK)
    _line(px, 20, 31, 28, 31, INK)
    _line(px, 12, 27, 17, 27, INK_SOFT); _line(px, 12, 27, 12, 32, INK_SOFT)
    _line(px, 17, 27, 17, 32, INK_SOFT); _line(px, 12, 32, 17, 32, INK_SOFT)
    return img


def doodle_flower():
    img = _d(); px = img.load()
    _line(px, 24, 44, 24, 24, PENCIL)
    _line(px, 24, 34, 16, 30, PENCIL)
    _line(px, 24, 38, 33, 34, PENCIL)
    for k in range(6):
        a = k * math.pi / 3
        _circle(px, 24 + math.cos(a) * 8, 20 + math.sin(a) * 8, 5, RED)
    _circle(px, 24, 20, 4, INK)
    return img


DOODLES = [
    ("doodle-stick-figure", doodle_stick_figure),
    ("doodle-sun", doodle_sun),
    ("doodle-spiral", doodle_spiral),
    ("doodle-scribble", doodle_scribble),
    ("doodle-star", doodle_star),
    ("doodle-writing", doodle_writing),
    ("doodle-house", doodle_house),
    ("doodle-flower", doodle_flower),
]


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
    for name, fn in DOODLES:
        fn().resize((DOODLE * 2, DOODLE * 2), Image.NEAREST).save(
            os.path.join(OUT, name + ".png"))
    print("%-14s generated  -> %d paper tiles + %d doodles + tiles.json (the page, not the art)"
          % ("paper", PAPER_VARIANTS, len(DOODLES)))


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
