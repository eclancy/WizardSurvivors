# -*- coding: utf-8 -*-
"""The Gleaning Bell: the rare pickup that calls every XP orb on the map to the player.

Run: python tools/art/sprite_bell.py      (writes assets/bonelight/pickups/gleaning-bell.png)
     python tools/art/build.py            (writes it with everything else)

WHAT IT IS. In an English village the gleaning bell was rung morning and evening to say when the
poor could go into the reaped fields and gather what the harvest had left behind. That is the whole
mechanic: the XP the player walked past, called home. It is a found object of the land, not a spell
and not a relic, which is the fiction's rule for things lying in a held place (.ai/world-and-tone.md:
everything there "used to belong to someone").

THE CONTRACT. A pickup is a 16x16 cell, filling it, rendered at x2 (.ai/art-direction.md section 1),
drawn 1:1 here and scaled by the node. It is an object in the world, so it is drawn on a material row
and touches an element ramp only where it genuinely glows (the rule tools/art/props_fx.py states for
the chest and the flasks):

    body   gold      the trim-and-lantern row: bronze, lit from the upper left
    light  grass     the XP orb's own ramp: the clapper, the light under the lip, the motes

THE GREEN IS THE POINT. Nothing else on the floor is gold AND green. The small orb is grass, the
medium arcane, the large light; the flasks are glass; the chest is wood and iron. A bell with the
small orb's light inside it, and two of those motes falling into it, says "this is about the orbs"
before the player has ever touched one. It also keeps it clear of the secrets' tell
(.ai/stage-secrets.md section 2), which is warm gold LIGHT that breathes - the bell's gold is pigment,
and its light is cold green.

DRAWN AS THE ICON OF A BELL, not as a bell lying in the grass. At 32 screen pixels in a swarm the
shape has to say "bell" at a glance, so it is the shape everyone already reads as one: a loop on top,
a broad dome, straight flanks, a flare, and the clapper showing under the lip. A first pass with a
small knob and a body that widened a pixel a row read as a cone, or a hat. The flanks are shaded in
fixed columns rather than per row for the same reason - shading by each row's own width drew a
diagonal staircase down the right side that made the cone again.

WHAT THE FRAMES DO. Like the orbs, the breath is in the frames, not the transform (sprite_pickups.py
says why): the clapper and the light under the lip step one stop brighter on frames 1 and 2, and two
motes walk in toward the lip, one pixel-placed step a frame, out of phase so the pair is never
mirrored.
"""
from __future__ import print_function

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as B
import pixel as P

CELL = 16
FRAMES = 4

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_PICKUPS = os.path.join(ROOT, "assets", "bonelight", "pickups")
PNG = "gleaning-bell.png"

# The bell's silhouette, one inclusive x-span per row, symmetric about x = 7.5 (the light is not).
BODY = {
    2: (6, 9),    # the crown, where the loop meets it
    3: (4, 11),   # the dome, broad at once
    4: (3, 12),
    5: (3, 12),   # straight flanks
    6: (3, 12),
    7: (3, 12),
    8: (3, 12),
    9: (3, 12),   # the cast band, a raised ring
    10: (2, 13),  # the flare
    11: (1, 14),
    12: (1, 14),  # the lip
}
# The hanging loop above the crown: its top bar, then its two sides with the hole between.
LOOP = [(7, 0, "l"), (8, 0, "s"), (6, 1, "l"), (9, 1, "s")]
BAND_ROW = 9
LIP_ROW = 12
# The crown of the lit plane: four hi pixels where the dome turns to the key. A plane, not a glint
# (.ai/art-direction.md, "Matte, not wet").
CROWN_HI = [(5, 3), (6, 3), (4, 4), (5, 4)]

# Two motes falling in toward the lip, one step a frame. Hand-placed so every one lands on a whole
# pixel (sprite_pickups.SPARKS gives the reason).
MOTE_LEFT = [(1, 1), (1, 4), (0, 7), (0, 10)]
MOTE_RIGHT = [(14, 1), (14, 4), (15, 7), (15, 10)]
# Fainter far off, brighter as it nears the light it is joining.
MOTE_TONE = ["D", "C", "C", "B"]


def palette():
    gold = B.MATERIALS["gold"]
    grass = B.ELEMENTS["grass"]
    return B.build_palette({
        "o": B.OCC,
        "h": gold[0], "l": gold[1], "b": gold[2], "s": gold[3], "d": gold[4],
        "A": grass[0], "B": grass[1], "C": grass[2], "D": grass[3],
    })


def _body_tone(x, y):
    """Bronze lit from the upper left, in fixed columns: a cylinder, lit left, falling away right."""
    if y == BAND_ROW:
        # The band stands proud, one stop lighter than the body under it, so it reads as a ridge
        # turning in the light rather than as a stripe of colour.
        if x <= 6:
            return "l"
        return "b" if x <= 10 else "s"
    if y == LIP_ROW:
        # A thick rolled edge: its top catches the key along the left, its right falls away.
        if x <= 3:
            return "l"
        if x <= 10:
            return "b"
        return "s" if x <= 12 else "d"
    if x <= 4:
        return "l"
    if x <= 8:
        return "b"
    if x <= 10:
        return "s"
    return "d"


def frame(i):
    g = [["."] * CELL for _ in range(CELL)]

    def put(x, y, ch):
        if 0 <= x < CELL and 0 <= y < CELL:
            g[y][x] = ch

    for (x, y, ch) in LOOP:
        put(x, y, ch)
    for y, (x0, x1) in sorted(BODY.items()):
        for x in range(x0, x1 + 1):
            put(x, y, _body_tone(x, y))
    for (x, y) in CROWN_HI:
        put(x, y, "h")

    # The light inside, escaping: a line between the lip and the ground, and the clapper hanging
    # into it - the brightest thing in the cell, one stop up on the breath frames.
    bright = i in (1, 2)
    for x in range(2, 14):
        put(x, 13, "B" if bright else "C")
    put(7, 13, "A" if bright else "B")
    put(8, 13, "A" if bright else "B")
    # The seat: the contact line under the bell, with the clapper's foot in the middle of it.
    for x in range(3, 13):
        put(x, 14, "o")
    put(7, 14, "B" if bright else "C")
    put(8, 14, "C" if bright else "D")

    for path, offset in ((MOTE_LEFT, 0), (MOTE_RIGHT, 2)):
        k = (i + offset) % len(path)
        x, y = path[k]
        put(x, y, MOTE_TONE[k])
    return ["".join(r) for r in g]


def frames():
    return [frame(i) for i in range(FRAMES)]


def write(out_dir=OUT_PICKUPS):
    """The strip the game loads (GleaningBell.cs): four 16x16 cells side by side, 1:1."""
    path = os.path.join(out_dir, PNG)
    P.write_strip(frames(), palette(), path, CELL)
    return path


def main():
    path = write()
    print("gleaning-bell  %d frames  %s" % (FRAMES, os.path.relpath(path, ROOT)))
    preview_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_pickups")
    if not os.path.isdir(preview_dir):
        os.makedirs(preview_dir)
    P.preview([("bell", frames())], palette(), os.path.join(preview_dir, "_bell.png"), zoom=12)
    return 0


if __name__ == "__main__":
    sys.exit(main())
