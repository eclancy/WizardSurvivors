# -*- coding: utf-8 -*-
"""The Broken Circle's ground (.ai/campaign.md section 6). Drawn by tools/art/tiles.py, which finds
this module by its name and appends TERRAINS and MATERIALS to its own - run tiles.py, never this.

A high heath of heather and granite, ringed by standing stones, with peat where the heather has
been stripped and tarns in the hollows. Three pieces of ground, each with its own job:

  heather   the floor (a MATERIAL, so the base layer paints it everywhere): violet's deep half, with
            peat showing through in soft linen clumps. The natural floor style, as the Ruins' grey
            stone and the Swamp's mud are; what makes it read as heather rather than as violet
            stone is the detail layer's heather tufts (detail_heath.py), placed in drifts.
  peat      bare peat, a blob terrain: wet, near-black, fibrous, with standing water in its hollows
            catching the key. Darker than the heather by a step, never a hole: it is linen's deep
            stop and stone's, with gold's deep end for the fibre, so it reads as ground.
  granite   the moor's bones breaking through, a blob terrain that is PARTLY TRANSPARENT the way
            scree and paving are: flat slabs with the heather showing between them. Granite is the
            one speckled stone, so every slab carries the pale feldspar fleck, and lichen on its top.

Both terrains are walkable and neither is a hazard: this module sets no HAZARDS and no BLOCKERS
(tiles.py refuses one that does). The only blocker on the heath is water, the tarns, which is
drawn from its shape, not from here.

Nothing here reaches the bright end of a ramp, and nothing is emissive: the heath is the quietest
thing on screen, and the lightning the steward throws has to be the brightest.
"""
import math

import tiles
from tiles import OCC, SIZE, clumps, crack, hashed, inside, pebble, ramp, scatter, specks


def body_peat(c):
    """Bare peat: wet, black and fibrous, with water standing in its hollows.

    Linen's deep stop for the body and stone's for the wet hollows, so the patch is a step darker
    than the heather round it and green-black where the heather is violet-black - the two differ in
    hue as well as value, which is what lets a darker patch read as a different ground rather than
    as a shadow. Gold's deep end is the fibre. A few hollows hold water: a lichen pool with one lit
    pixel on its upper-left lip, the only thing in the tile that catches the key.
    """
    ln = ramp("linen")
    st = ramp("stone")
    gd = ramp("gold")
    li = ramp("lichen")
    # A step darker than the heather and a different hue (green-black against violet-black), never a
    # hole: the first in-game pass stood on stone's deep stop with gold's shade in it, and read as the
    # "large dark patches of something unrecognisable" .ai/terrain.md section 8 records, with embers
    # in them. Linen's deep stop for the body; stone's only in the wettest hollows; the fibre in
    # gold's deep end, which stays brown.
    c.rect(0, 0, SIZE - 1, SIZE - 1, ln[4])
    clumps(c, 6, 2, 4, gd[4], 1101, fill=0.5, squash=0.6)
    clumps(c, 5, 1, 3, st[4], 1103, fill=0.45)
    clumps(c, 5, 1, 3, ln[3], 1105, fill=0.4)
    specks(c, 0.006, OCC, 1107)

    # Fibre: short horizontal drags, as cut peat shows its layers.
    def fibre(cv, x, y, k):
        run = 3 + int(hashed(k, 3, 1113) * 4)
        for j in range(run):
            cv.set(x + j, y, gd[4] if j % 3 else ln[3])
    scatter(c, 6, 1111, 6, fibre, shared=0.4)

    # Standing water in a hollow: a small pool, lit on its upper-left lip.
    def pool(cv, x, y, k):
        w = 3 + int(hashed(k, 1, 1121) * 3)
        for dy in range(2):
            for dx in range(w - dy):
                cv.set(x + dx + dy, y + dy, li[4] if (dx + dy) % 3 else li[3])
        cv.set(x, y - 1, OCC)
        cv.set(x + 1, y - 1, OCC)
        cv.set(x + 1, y, li[2])
    scatter(c, 2 + (tiles.VARIANT % 2), 1123, 7, pool, shared=0.0)


def body_granite(c):
    """A granite outcrop - flat slabs of the moor's bones breaking through the heather. PARTLY
    TRANSPARENT: the floor shows between the slabs, so the patch lies on the heath.

    Each slab is lit on its upper-left edge and drops into an occ lip on its lower right, so it
    reads as a raised flat stone rather than as a stain. Granite is speckled - pale feldspar in
    bone's deep stop, dark mica in stone's - and that speckle is what names it at a glance against
    the Ruins' worked stone. A crust of lichen sits on some tops. Kept inside two stops of the
    floor (stone's base and lit, its hi only on an edge): the outcrop is ground, not a prop.
    """
    st = ramp("stone")
    sk = ramp("skin")
    li = ramp("lichen")

    def slab(cv, cx, cy, k):
        rx = 6.0 + hashed(k, 1, 1201) * 6.0
        ry = 3.5 + hashed(k, 2, 1203) * 3.0
        tilt = (hashed(k, 3, 1205) - 0.5) * 0.5
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                nx = (x - cx) / rx
                ny = (y - cy - nx * tilt * rx) / ry
                # A squarish superellipse with its corners knocked off: a split slab, not a pebble.
                d = abs(nx) ** 3 + abs(ny) ** 3
                if d > 1.0:
                    continue
                k_light = -(nx + ny)
                if d > 0.62 and k_light > 0.55:
                    tone = sk[4] if d > 0.8 else st[0]
                elif d > 0.55 and k_light < -0.5:
                    tone = OCC if d > 0.82 else st[2]
                else:
                    tone = st[1]
                    h = hashed(x % SIZE, y % SIZE, 1207 + k)
                    if h < 0.09:
                        tone = sk[4]                    # feldspar
                    elif h < 0.15:
                        tone = st[3]                    # mica
                    elif h < 0.22:
                        tone = st[0]
                cv.set(x, y, tone)
        # A crack across the larger slabs, and lichen on some tops.
        if rx > 7.0:
            ang = hashed(k, 4, 1209) * math.pi
            for j in range(-int(rx * 0.6), int(rx * 0.6)):
                px, py = cx + math.cos(ang) * j, cy + math.sin(ang) * j * 0.5
                if cv.get(int(px), int(py)) not in ((0, 0, 0, 0),):
                    cv.set(int(px), int(py), st[3])
        if hashed(k, 5, 1211) < 0.6:
            lx, ly = int(cx - rx * 0.3), int(cy - ry * 0.3)
            for (dx, dy, t) in ((0, 0, 2), (1, 0, 3), (0, 1, 3), (-1, 0, 3), (1, -1, 2), (2, 0, 4)):
                cv.set(lx + dx, ly + dy, li[t])
    scatter(c, 4, 1221, 9, slab, shared=0.4)
    # Grit between the slabs.
    for k in range(5):
        pebble(c, inside(k, 1231, 3), inside(k + 5, 1233, 3), 1)


TERRAINS = [
    ("peat", body_peat, None),
    ("granite", body_granite, None),
]

# The floor: heather on peat. (material, row, style, seeds, accent row) - see tiles.MATERIALS.
MATERIALS = [
    ("heather", "violet", "natural", 6, "linen"),
]
