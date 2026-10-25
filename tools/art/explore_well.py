# -*- coding: utf-8 -*-
"""The Fallow Fields' side event art: the well (.ai/campaign.md section 5). Run: python tools/art/explore_well.py

    world/fields/well.png       the well, two frames: clean, and fouled
    ui/poi/poi-well.png         the arrow's pictogram for it (see the note at GLYPH below)
    tools/art/_explore_well_preview.png   (gitignored) for looking at

"The well is the last water he has not fouled." A round well-head of field stone (steel's grey-blue stops: the floor-stone row is near black and
vanished on the dark stubble, the cairn's lesson in explore_props.py), a windlass on two
posts over it and a bucket on its rope - the village's water, and in it the coins the village threw
in. WellEvent stands it in the stubble from the first second of a run and asks the player to keep
it clean.

Like the stooks (props_fields.py), authored 1:1 on its cell and saved at x2, its contact shadow on
the last row: the bottom edge of the PNG is the ground line and the middle column stands on the
site. WellEvent reads it that way, and its collision footprint is the drum's foot.

COLD WATER, NOT LIGHT. The well's water is stone's deep with steel's lit stop as the one glint on it
- dark water under no light - and the fouled frame turns it to lichen's deep with occlusion
scumming over it. Nothing here is emissive: the event's light is code (SideEvent's far tell and
WellEvent's glow), so the sprite never competes with an XP orb.
"""
from __future__ import division, print_function

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_WORLD = os.path.join(ROOT, "assets", "bonelight", "world", "fields")
OUT_UI = os.path.join(ROOT, "assets", "bonelight", "ui", "poi")
PREVIEW = os.path.join(ROOT, "tools", "art", "_explore_well_preview.png")
SCALE = 2
CELL = 48


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


ST = ramp("stone")
STL = ramp("steel")
GO = ramp("gold")
LN = ramp("linen")
LI = ramp("lichen")
FL = ramp("flesh")

CX = 24
RIM_Y = 30          # the centre of the well-head's top ellipse
RIM_RX, RIM_RY = 15, 5
DRUM_FOOT = 45      # the drum's foot: the ground line is the row under it


def stone_course(c, y0, y1, salt):
    """The drum's face below the rim: courses of field stones, lit toward the key, mortar dark."""
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = RIM_RX - 0.5 + t * 0.8
        x0, x1 = int(round(CX - hw)), int(round(CX + hw))
        for x in range(x0, x1 + 1):
            u = (x - CX) / hw                           # -1 at the lit (left) edge, +1 at the far
            row = (y - y0) // 4
            joint = (x + row * 3 + salt) % 6 == 0
            if (y - y0) % 4 == 3 or joint:
                tone = STL[4] if u > -0.6 else STL[3]
            elif u < -0.55:
                tone = STL[1]
            elif u < 0.35:
                tone = STL[2]
            else:
                tone = STL[3]
            c.set(x, y, tone)
        c.set(x0 - 1, y, OCC if y > y0 + 1 else STL[3])


def well(fouled):
    c = raster.Canvas(CELL, CELL, None)
    # the contact shadow on the last two rows
    c.disc(CX, CELL - 1.5, 17, 1.8, OCC)
    # THE POSTS behind the drum, up to the windlass.
    for px in (CX - 13, CX + 12):
        for y in range(9, DRUM_FOOT - 2):
            c.set(px, y, GO[2] if px < CX else GO[3])
            c.set(px + 1, y, GO[3] if px < CX else GO[4])
        c.set(px, 8, FL[2])
        c.set(px + 1, 8, GO[3])
    # THE WINDLASS: a roller between the posts with rope wound on it, and the crank.
    for x in range(CX - 12, CX + 12):
        c.set(x, 11, GO[2])
        c.set(x, 12, GO[3])
        c.set(x, 13, GO[4])
    for x in range(CX - 5, CX + 4):
        c.set(x, 11, LN[1] if x % 2 else LN[2])         # the rope wound round it
        c.set(x, 12, LN[2] if x % 2 else LN[3])
    for (x, y, tone) in ((CX + 14, 12, STL[2]), (CX + 15, 12, STL[3]), (CX + 15, 13, STL[3]),
                         (CX + 15, 14, STL[2]), (CX + 16, 15, STL[1]), (CX + 17, 15, STL[2])):
        c.set(x, y, tone)                               # the iron crank
    # THE ROPE down to the bucket, which hangs over the water.
    for y in range(14, 21):
        c.set(CX, y, LN[2] if y % 2 else LN[3])
    for y in range(21, 26):
        hw = 3 if y < 25 else 2
        for x in range(CX - hw, CX + hw + 1):
            c.set(x, y, GO[2] if x < CX - 1 else (GO[3] if x < CX + 2 else GO[4]))
        c.set(CX - hw - 1 if y == 21 else CX - hw, y, GO[1] if y == 21 else GO[2])
    c.hline(CX - 3, CX + 3, 22, STL[3])                 # the bucket's iron hoop
    c.hline(CX - 3, CX + 3, 21, GO[1])
    # THE DRUM: the far half of the rim first, then the water, then the near rim over it.
    stone_course(c, RIM_Y + 1, DRUM_FOOT, 1)
    for y in range(RIM_Y - RIM_RY, RIM_Y + RIM_RY + 1):
        for x in range(CX - RIM_RX, CX + RIM_RX + 1):
            nx = (x - CX) / float(RIM_RX)
            ny = (y - RIM_Y) / float(RIM_RY)
            d = nx * nx + ny * ny
            if d > 1.0:
                continue
            inner = (x - CX) ** 2 / float((RIM_RX - 3) ** 2) + (y - RIM_Y - 0.5) ** 2 / float((RIM_RY - 1.6) ** 2)
            if inner <= 1.0:
                # the water
                if fouled:
                    tone = LI[4] if (x * 3 + y) % 5 else OCC
                    if (x + y * 2) % 7 == 0:
                        tone = LI[3]                    # scum
                else:
                    tone = ST[4] if ny < 0.1 else OCC
            else:
                # the rim's top: lit on the key side, a stop down on the far side
                tone = STL[1] if nx + ny * 0.6 < -0.3 else (STL[2] if nx < 0.5 else STL[3])
                if (x * 5 + y) % 7 == 0:
                    tone = STL[3]
            c.set(x, y, tone)
    if not fouled:
        # the one glint on the clean water, and a coin's gleam far down
        c.set(CX - 4, RIM_Y - 1, STL[1])
        c.set(CX - 3, RIM_Y - 1, STL[2])
        c.set(CX + 3, RIM_Y + 1, GO[3])
    else:
        c.set(CX - 4, RIM_Y - 1, LI[3])
        for (x, y) in ((CX + 6, RIM_Y + 4), (CX - 9, RIM_Y + 3), (CX + 10, RIM_Y + 2)):
            c.set(x, y, LI[4])                          # the foulness slopped over the rim
            c.set(x, y + 1, OCC)
    # The bucket's lower half shows over the far rim; the rope crosses the near.
    for y in range(RIM_Y - RIM_RY + 1, RIM_Y - 1):
        c.set(CX, y, LN[3])
    # Moss in the drum's joints on its shaded side, and a stone missing.
    for (x, y) in ((CX + 9, 37), (CX + 12, 41), (CX + 5, 43), (CX - 10, 40)):
        c.set(x, y, LI[3])
        c.set(x, y + 1, LI[4])
    c.set(CX + 2, 38, OCC)
    c.set(CX + 3, 38, OCC)
    c.set(CX + 2, 39, STL[4])
    # The key rim down the drum's lit side, where nothing stands beside it.
    for y in range(RIM_Y - RIM_RY + 1, DRUM_FOOT + 1):
        for x in range(0, CX):
            if c.get(x, y)[3]:
                if not c.get(x - 1, y)[3]:
                    c.set(x - 1, y, bl.RIM)
                break
    return c


# THE ARROW'S PICTOGRAM, 12x12 in explore_props.py's style and palette rules. PoiPointer draws the
# glyphs ExploreArt.Glyphs lists, and adding "well" to that list is a shared edit this chapter does
# not own (.ai/campaign.md section 7), so until the integrator adds it the well's arrow carries the
# badge with no pictogram in it - it degrades, it does not break.
GLYPH = [
    "............",
    "..y......y..",
    "..yGGGGGGy..",
    "..y..s...y..",
    "..y..z...y..",
    "...sSSSSs...",
    "..sSzzzzSS..",
    "..SzQQQQzZ..",
    "..SSzzzzZZ..",
    "..zSSSSZZz..",
    "...zzzzzz...",
    "............",
]
GLYPH_PAL = {
    "s": STL[1], "S": STL[2], "z": STL[3], "Z": STL[4],
    "G": GO[1], "y": GO[2],
    "Q": bl.ELEMENTS["water"][2],
}


def glyph():
    c = raster.Canvas(12, 12, None)
    for y, row in enumerate(GLYPH):
        for x, ch in enumerate(row):
            if ch != ".":
                c.set(x, y, GLYPH_PAL[ch])
    return c


def main():
    for d in (OUT_WORLD, OUT_UI):
        if not os.path.isdir(d):
            os.makedirs(d)
    ok = True
    strip = Image.new("RGBA", (CELL * SCALE * 2, CELL * SCALE), (0, 0, 0, 0))
    for i, fouled in enumerate((False, True)):
        c = well(fouled)
        bad = c.audit()
        if bad:
            ok = False
            print("OFF-CONTRACT COLOURS in well frame %d: %s" % (i, bad[:4]))
        strip.alpha_composite(c.scaled(SCALE), (i * CELL * SCALE, 0))
    strip.save(os.path.join(OUT_WORLD, "well.png"))
    g = glyph()
    bad = g.audit()
    if bad:
        ok = False
        print("OFF-CONTRACT COLOURS in the well glyph: %s" % bad[:4])
    g.scaled(SCALE).save(os.path.join(OUT_UI, "poi-well.png"))
    print("  well       2 frames at %dpx, glyph poi-well" % (CELL * SCALE))
    sheet = Image.new("RGBA", (strip.size[0] + 60, strip.size[1] + 20), raster.rgb(GO[4]))
    sheet.alpha_composite(strip, (10, 10))
    sheet.alpha_composite(g.scaled(SCALE * 2), (strip.size[0] + 12, 10))
    sheet.resize((sheet.size[0] * 3, sheet.size[1] * 3), Image.NEAREST).save(PREVIEW)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
