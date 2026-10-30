# -*- coding: utf-8 -*-
"""The ground. Run: python tools/art/tiles.py

Every tile every stage is fought on: the autotile terrains at fourteen roles each, the plain floor
materials, and the Cursed Dungeon's jail walls (`jail_walls.py`, with the shadows they throw). About six hundred 48x48 tiles
and a `tiles.json` manifest, which `CuratedTileCatalog.LoadMany` merges alongside the
Sketchbook's. The small things lying ON the ground - flowers, lilypads, reeds, pebbles - are
`ground_detail.py`, not this. Water is not drawn here either: its shape is computed
(scripts/WaterShape.cs) and its surface comes from `water.py`; tiles.json keeps one `water_fill`
tile so the manifest still says water blocks.

It replaces the bought dungeon-floor pack - the single largest piece of art in the game we could
not publish, and the last of it. See `.ai/asset-licensing.md`.

THE ROLES ARE DERIVED, NOT DRAWN. An autotiler asks for fourteen variants of every terrain: the
body, four edges, four outer corners, four inner corners, and an isolated patch. Drawing them by
hand would be hundreds of chances for one edge to disagree with the body it belongs to. So each
terrain supplies ONE painter for its body and one edge PROFILE, and every role is that body with
a coverage mask cut out of it - an edge tile is the fill tile, by construction, and they can never
drift apart.

THE GROUND IS THE QUIETEST THING ON SCREEN and has to stay that way. It covers every pixel the
player is not looking at, so all of it is drawn from the deep half of its material row, the
contrast inside a tile never exceeds two stops, and nothing on it is emissive except lava - which
is a hazard, and is supposed to pull the eye. `.ai/art-direction.md` section 4c is the rule; the
practical test is whether a skeleton standing on it still reads, and a ground tile that competes
is a ground tile that has to go darker.

THEY MUST ALSO TILE, AND AGAINST EACH OTHER. The base layer mixes a terrain's fill variants freely
(LevelTilePainter hashes one per cell), so a feature that crossed a tile edge would be cut off
wherever the neighbour is a different variant - a straight seam. Every feature here is therefore
kept off the tile's outer pixels (`inside()`), and the only things that touch an edge are
per-pixel specks, which have no shape to cut.

THE NATURAL-GROUND PASS (2026-10, `.ai/terrain.md` section 8). The Forest was reported as having
"large dark patches of something unrecognisable that don't blend with the grass". They were
`mossy_rock`: cobbles in `stone.deep` with an `occ` outline, two value steps under the grass,
stamped in staircase blobs with square corners. Three changes, all in this file:

  1. NO OUTLINES ON WALKABLE GROUND. Every terrain you can walk across now fades into whatever it
     lies on through a dithered coverage mask (the trick water already used): only the two
     hazards keep a crisp `occ` lip, because a lava edge that faded would lie about where the
     damage starts.
  2. ROUNDED, WANDERING EDGES. Corners are arcs, not right angles, and every edge wanders by a
     few pixels along its length - with two or three variants of every edge and corner, chosen
     per cell, so a long shore is not one scallop repeated every 48 px.
  3. BODIES YOU CAN NAME, AT A VALUE NEAR THE GROUND. Moss cushions and leaf litter instead of
     black cobble; a lighter meadow; bare earth that has pebbles in it; and three terrains whose
     bodies are partly transparent (overgrowth, scree) so they lie ON a floor rather than
     replacing it. Per-pixel static - the old 20% `occ` speckle on every floor - is gone too: a
     field of independent pixels reads as sandpaper, which section 3 of terrain.md found for
     water long before it was noticed on the ground.
"""

import io
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import jail_walls
import raster
import tile_atlas
import water

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "tiles")

SIZE = 48
TAU = math.pi * 2.0
CLEAR = (0, 0, 0, 0)


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


# Which variant is being drawn. Every painter reads it through hashed(), so bumping it reshuffles
# all of a terrain's features without any painter needing to know variants exist.
#
# IT EXISTS BECAUSE ONE TILE PER ROLE IS A GRID. With a single fill, every feature lands in exactly
# the same place in every cell and a floor of them reads as wallpaper with a 48-pixel repeat; with
# a single edge, a straight shore is one wiggle repeated. CuratedTileCatalog keeps every variant of
# a (terrain, role) and LevelTilePainter hashes one per cell.
VARIANT = 0

# Variants per role. Fill is what covers most of a stage, so it gets the most.
FILL_VARIANTS = 6
EDGE_VARIANTS = 3
CORNER_VARIANTS = 2


def hashed(x, y, salt=0):
    """A stable value in [0,1) for a cell. Deterministic so a tile is the same every run."""
    h = (x * 73856093) ^ (y * 19349663) ^ ((salt + VARIANT * 977) * 83492791)
    h &= 0x7FFFFFFF
    return ((h >> 7) % 10007) / 10007.0


def inside(k, salt, margin):
    """A coordinate for feature k that keeps `margin` pixels clear of both tile edges."""
    return margin + int(hashed(k, salt, salt * 3 + 1) * (SIZE - 2 * margin))


# --- drawing helpers -----------------------------------------------------------------------------
# Small vocabulary, used by every body.
#
# THE MARGIN PROBLEM, AND THE SHARED LAYER. A feature that crosses a tile edge is cut wherever the
# neighbouring cell is a different variant - a seam. Keeping every feature inside the tile avoids
# that, and makes a different failure: a bare margin round every tile, which over a patch of moss
# or overgrowth is a visible grid of rosettes. So a share of each body's features is SHARED - drawn
# as variant 0 whichever variant is being painted, at any position, wrapping round the tile - and
# the rest are the variant's own, kept inside. Shared features are identical in every variant, so
# where one wraps off the right edge, every possible neighbour has the rest of it on its left.


class Wrap(object):
    """A canvas view that wraps coordinates round the tile, for the shared layer."""

    def __init__(self, c):
        self.c = c

    def set(self, x, y, col):
        self.c.set(int(x) % SIZE, int(y) % SIZE, col)

    def get(self, x, y):
        return self.c.get(int(x) % SIZE, int(y) % SIZE)


def scatter(c, n, salt, margin, draw, shared=0.4):
    """Call draw(canvas, x, y, k) for n features: `shared` of them in the shared layer, the rest
    the variant's own and kept `margin` clear of the edges. See the note above."""
    global VARIANT
    own = VARIANT
    for k in range(n):
        if k < int(round(n * shared)):
            VARIANT = 0
            x = int(hashed(k, salt, salt * 3 + 1) * SIZE)
            y = int(hashed(k + 50, salt + 1, salt * 3 + 4) * SIZE)
            draw(Wrap(c), x, y, k)
            VARIANT = own
        else:
            draw(c, inside(k, salt, margin), inside(k + 50, salt + 1, margin), k)

def specks(c, density, tone, salt):
    """Single pixels. The only thing allowed to touch a tile edge - it has no shape to cut."""
    for y in range(SIZE):
        for x in range(SIZE):
            if hashed(x, y, salt) < density:
                c.set(x, y, tone)


def clumps(c, n, rmin, rmax, tone, salt, fill=0.55, squash=1.0, shared=0.4):
    """Soft blotches: a disc whose pixels are kept with probability `fill`, thinning at the rim.
    Scattered through scatter(), so some sit across the tile edges without making a seam."""
    def one(cv, cx, cy, k):
        r = rmin + hashed(k, 1, salt) * (rmax - rmin)
        ry = r * squash
        for y in range(int(cy - ry), int(cy + ry) + 1):
            for x in range(int(cx - r), int(cx + r) + 1):
                nx = (x - cx) / max(0.5, r)
                ny = (y - cy) / max(0.5, ry)
                d = nx * nx + ny * ny
                if d > 1.0:
                    continue
                # Solid in the middle, broken only towards the rim: a blotch with a dithered
                # edge reads as a patch of shade; one speckled all through read as mould.
                keep = min(1.0, (1.0 - d) * 2.2) * (0.55 + fill * 0.8)
                if hashed(x % SIZE, y % SIZE, salt + 4) < keep:
                    cv.set(x, y, tone)
    scatter(c, n, salt + 2, int(rmax) + 2, one, shared)


def tuft(c, x, y, dark, mid, light):
    """A grass tuft seen from above and a little in front: three blades and a dark root."""
    # Two blades and a lit tip, with the shadow on ONE side only. A symmetric tuft with a dark
    # root under it read as a printed glyph, and a field of them as a patterned carpet.
    c.set(x, y, mid)
    c.set(x - 1, y - 1, mid)
    c.set(x + 1, y - 2, light)
    c.set(x + 1, y - 1, mid)
    if dark:
        c.set(x + 1, y, dark)


def pebble(c, x, y, r, dark=OCC):
    """A stone lying on the ground, lit from the upper left, with its contact shadow."""
    st = ramp("stone")
    c.set(x, y + 1, dark)
    c.set(x + 1, y + 1, dark)
    c.set(x, y, st[2])
    c.set(x + 1, y, st[3])
    c.set(x, y - 1, st[1])
    if r > 1:
        c.set(x - 1, y, st[2])
        c.set(x - 1, y + 1, dark)
        c.set(x + 1, y - 1, st[2])
        c.set(x - 1, y - 1, st[0] if r > 2 else st[1])


def leaf(c, x, y, body, vein, turn):
    """A fallen leaf: a 2x3 lozenge with a dark midrib, turned one of four ways."""
    pts = [(0, -1), (0, 0), (0, 1), (1, 0)] if turn % 2 else [(-1, 0), (0, 0), (1, 0), (0, -1)]
    for dx, dy in pts:
        c.set(x + dx, y + dy, body)
    c.set(x, y, vein)


def crack(c, x, y, length, tone, salt):
    """A short angular crack, turning at each joint - a crack changes angle, a hatch does not."""
    ang = hashed(x, y, salt) * TAU
    px, py = float(x), float(y)
    for j in range(length):
        if j % 3 == 0:
            ang += (hashed(j, x, salt + 1) - 0.5) * 1.8
        px += math.cos(ang)
        py += math.sin(ang)
        ix, iy = int(px), int(py)
        if 1 <= ix < SIZE - 1 and 1 <= iy < SIZE - 1:
            c.set(ix, iy, tone)


# --- terrain bodies ------------------------------------------------------------------------------
# Each takes a canvas and fills it. Opaque unless it says otherwise.

def body_grass(c):
    """The Forest's ground. Calm teal turf with tufts, and no static.

    The old body was 16% `deep` and 10% `base` pixels hashed independently over `shade`: a
    field of television static that read as sandpaper at every zoom. This is one tone with soft
    blotches of its neighbours and a scatter of drawn tufts - the texture is in shapes the eye can
    name, not in noise. Variants 3-5 carry one extra thing each (clover, a fallen leaf or two,
    a pebble), so a long run of turf has incidents in it without the detail layer.
    """
    g = ramp("lichen")
    v = ramp("verdant")
    c.rect(0, 0, SIZE - 1, SIZE - 1, g[3])
    specks(c, 0.018, g[2], 111)
    specks(c, 0.012, g[4], 113)
    scatter(c, 9, 121, 3, lambda cv, x, y, k: tuft(cv, x, y, None if k % 3 else g[4], g[2], g[1] if k % 4 == 0 else g[2]))
    if VARIANT == 3:
        for k in range(4):
            x, y = inside(k, 131, 4), inside(k + 9, 133, 4)
            c.set(x, y, v[3])
            c.set(x + 1, y, v[3])
            c.set(x, y - 1, v[3])
            c.set(x + 1, y + 1, v[4])
    elif VARIANT == 4:
        f = ramp("flesh")
        gd = ramp("gold")
        for k in range(2):
            leaf(c, inside(k, 141, 4), inside(k + 5, 143, 4), gd[3] if k else f[3], f[4], k)
    elif VARIANT == 5:
        pebble(c, inside(1, 151, 5), inside(2, 153, 5), 2)


def body_meadow(c):
    """A sunlit clearing in the Forest: the grass one step lighter, with clover in it.

    The light patch to the moss's dark one, so the woodland floor has both directions of
    variation and neither is the only thing that is not grass.
    """
    g = ramp("lichen")
    v = ramp("verdant")
    c.rect(0, 0, SIZE - 1, SIZE - 1, g[2])
    specks(c, 0.02, g[1], 203)
    specks(c, 0.015, g[3], 205)
    scatter(c, 10, 211, 3, lambda cv, x, y, k: tuft(cv, x, y, None if k % 3 else g[3], g[1], g[1]))
    for k in range(5):
        x, y = inside(k, 221, 4), inside(k + 7, 223, 4)
        c.set(x, y, v[3])
        c.set(x - 1, y, v[3])
        c.set(x, y - 1, v[2] if k % 2 else v[3])
        c.set(x, y + 1, v[4])


def body_moss(c):
    """Forest floor under the trees: moss cushions and leaf litter on dark loam.

    THIS REPLACES THE FOREST'S `mossy_rock`, which is what "large dark patches of something
    unrecognisable" were. It is still the darker patch - a wood needs shade - but darker by one
    step rather than two, and full of things a player can name: rounded moss cushions lit on
    their upper-left, fallen leaves in three autumn tones, a twig. On `verdant` for the moss,
    because `.ai/terrain.md` section 4 found that anything living drawn on the ground's own ramp
    is a smudge.
    """
    g = ramp("lichen")
    v = ramp("verdant")
    f = ramp("flesh")
    gd = ramp("gold")
    rd = ramp("red")
    # On the grass tone itself, with the darkness carried by the cushions and the hollows between
    # them: a `deep` bed under everything was the first try, and in the game it was still a dark
    # patch - just a softer-edged one.
    c.rect(0, 0, SIZE - 1, SIZE - 1, g[3])
    clumps(c, 10, 1, 3, g[4], 301, fill=0.5)
    specks(c, 0.02, g[4], 303)
    # Moss cushions: a dark mound, a lit crescent upper-left, a contact pixel lower-right.
    def cushion(cv, cx, cy, k):
        r = 2 + int(hashed(k, 1, 311) * 3)
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                dx, dy = x - cx, y - cy
                d2 = dx * dx + dy * dy
                if d2 > r * r + 1:
                    continue
                tone = v[4]
                if dx + dy < -r * 0.6:
                    tone = v[3]
                if dx + dy < -r * 1.2 and d2 > (r - 1) * (r - 1):
                    tone = v[2]
                if dx + dy > r * 1.1 and d2 > (r - 1) * (r - 1):
                    tone = g[4]
                cv.set(x, y, tone)
    scatter(c, 10, 313, 6, cushion)
    # Leaf litter, in the three muted autumn rows.
    scatter(c, 7, 321, 3, lambda cv, x, y, k: leaf(cv, x, y, (gd[3], f[3], rd[2])[k % 3], f[4], k))
    # One twig.
    x, y = inside(0, 331, 8), inside(1, 333, 8)
    for j in range(5 + int(hashed(0, 2, 335) * 4)):
        c.set(x + j, y + (j // 3), gd[4])
    c.set(x + 2, y - 1, gd[4])


def body_dirt(c):
    """Bare earth: a worn patch in the Forest, grey-brown, with stones in it.

    On `linen` with clods of `flesh` and `gold`, not on `gold` itself: a saturated brown patch on
    teal turf was exactly the kind of jump this pass exists to remove. Earth under a cold key
    light is a muted grey-brown, and that keeps it within a step of the grass round it.
    """
    gd = ramp("gold")
    f = ramp("flesh")
    ln = ramp("linen")
    c.rect(0, 0, SIZE - 1, SIZE - 1, ln[3])
    clumps(c, 9, 1, 3, ln[4], 401, fill=0.3)
    clumps(c, 3, 2, 3, f[4], 403, fill=0.0)
    clumps(c, 3, 1, 2, gd[4], 404, fill=0.2)
    specks(c, 0.015, ln[2], 405)
    specks(c, 0.01, OCC, 407)
    for k in range(4):
        pebble(c, inside(k, 411, 4), inside(k + 3, 413, 4), 1 + (k % 2))


def body_dark_dirt(c):
    """Darker soil - peat in the Swamp, earth between the stones in the Ruins and the Sands.

    Was `stone.deep` with 30% `occ` pixels: a black, faintly purple static that read as a hole
    rather than as ground. Now it is loam with clods and stones in it.
    """
    f = ramp("flesh")
    gd = ramp("gold")
    ln = ramp("linen")
    # On `linen.deep` with the warm rows only in the clods: on `flesh.deep` it was a purple patch,
    # and on the Swamp's grey-green mud that read as a stain rather than as peat.
    c.rect(0, 0, SIZE - 1, SIZE - 1, ln[4])
    clumps(c, 6, 2, 5, f[4], 501, fill=0.45)
    clumps(c, 5, 1, 3, gd[4], 502, fill=0.5)
    clumps(c, 4, 1, 2, ln[3], 503, fill=0.45)
    specks(c, 0.012, OCC, 505)
    specks(c, 0.01, f[3], 507)
    for k in range(3):
        pebble(c, inside(k, 511, 4), inside(k + 3, 513, 4), 1 + (k % 2))


def body_overgrowth(c):
    """Grass and moss growing over a floor - PARTLY TRANSPARENT, so it lies on what is under it.

    The Ruins and the Cave stamped opaque `grass` - the Forest's turf, a full value step above
    their black stone - as hard-edged rectangles. A lawn on a dungeon floor. This replaces it
    there: tufts and moss clumps over clear pixels, so the stone shows between the stems and the
    patch belongs to the floor it grew on.
    """
    g = ramp("lichen")
    v = ramp("verdant")
    clumps(c, 11, 3, 7, g[4], 601, fill=0.8, shared=0.5)
    clumps(c, 8, 2, 5, v[4], 603, fill=0.75, shared=0.5)
    clumps(c, 6, 2, 4, g[3], 605, fill=0.7, shared=0.5)
    scatter(c, 18, 611, 3, lambda cv, x, y, k: tuft(cv, x, y, g[4], g[3], g[2] if k % 3 else v[3]), 0.5)


def body_scree(c):
    """Loose stones - PARTLY TRANSPARENT. This is `rocky_pit`, which is walkable and not a hazard.

    It used to be the pit body (flat `occ`) with rocks on it, outlined like a pit: a hole you
    could walk across, which is the one thing a terrain must never look like. Now it is a spill
    of stones lying on whatever floor it is stamped on.
    """
    st = ramp("stone")
    for k in range(16):
        x, y = inside(k, 701, 2), inside(k + 40, 703, 2)
        r = 1 + int(hashed(k, 2, 705) * 3)
        pebble(c, x, y, r)
        if r > 2:
            c.set(x + 1, y, st[2])
            c.set(x, y + 1, st[3])
            c.set(x + 2, y + 1, OCC)
    specks(c, 0.025, st[3], 707)


def body_sand(c):
    s = ramp("gold")
    f = ramp("flesh")
    c.rect(0, 0, SIZE - 1, SIZE - 1, s[4])
    clumps(c, 5, 3, 6, f[4], 801, fill=0.4)
    specks(c, 0.02, s[3], 803)
    ripples(c, s[3], f[4], 805, 3)


def ripples(c, crest, trough, salt, n=6):
    """Wind ripples: short shallow arcs, never a full-width line - those lined up into stripes."""
    for k in range(n):
        x0, y0 = inside(k, salt, 6), inside(k + 13, salt + 1, 4)
        run = 6 + int(hashed(k, 3, salt + 2) * 9)
        for j in range(run):
            x = x0 - run // 2 + j
            if not (1 <= x < SIZE - 1):
                continue
            y = y0 + int(round(math.sin(j / float(run) * math.pi) * -1.5))
            c.set(x, y, crest)
            c.set(x, y + 1, trough)


def body_cobble(c):
    """Old paving - PARTLY TRANSPARENT between the stones, so the floor it was laid on shows.

    The joints used to be painted `stone.deep`, which on the Scorched Sands made every patch of
    paving a near-black slab on bright sand. Now the gaps are clear: on sand the sand runs between
    the setts, on the Ruins' stone the stone does.
    """
    global VARIANT
    st = ramp("stone")
    # running bond, six across, with the courses offset and every stone jittered
    for row in range(6):
        oy = row * 8
        off = 4 if row % 2 else 0
        for col in range(7):
            ox = col * 8 - off
            # A stone that wraps over the tile edge must be the same in every variant, or it is cut
            # wherever the neighbour is a different one - so those are hashed as variant 0.
            wraps = ox < 0 or ox + 8 > SIZE
            keep, VARIANT = VARIANT, (0 if wraps else VARIANT)
            jx = int(hashed(col, row, 6) * 2) - 1
            jy = int(hashed(col, row, 7) * 2) - 1
            # A few stones missing: broken paving reads as old, a perfect grid as new.
            missing = hashed(col, row, 9) < 0.14
            VARIANT = keep
            if missing:
                continue
            for y in range(1, 7):
                for x in range(1, 7):
                    if (x in (1, 6) and y in (1, 6)):
                        continue
                    px, py = (ox + x + jx) % SIZE, (oy + y + jy) % SIZE
                    tone = st[2] if (x <= 2 and y <= 2) else (st[3] if y < 5 else st[4])
                    c.set(px, py, tone)


def body_mossy_rock(c):
    """Flat stones half sunk in moss - the Swamp's stepping ground.

    Still cobble underneath, but with the moss over it in `verdant` rather than in the ground's
    own `lichen`, on a `lichen.deep` bed rather than on `stone.deep`, so the patch is a step from
    the mud round it rather than a black hole in it.
    """
    g = ramp("lichen")
    v = ramp("verdant")
    st = ramp("stone")
    c.rect(0, 0, SIZE - 1, SIZE - 1, g[4])
    for k in range(6):
        r = 3 + int(hashed(k, 1, 901) * 3)
        cx, cy = inside(k, 903, r + 2), inside(k + 9, 905, r + 2)
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r - 1, cx + r + 2):
                dx, dy = x - cx, (y - cy) * 1.3
                if dx * dx + dy * dy > r * r:
                    continue
                tone = st[2]
                if dx + dy < -r * 0.7:
                    tone = st[1]
                elif dx + dy > r * 0.7:
                    tone = st[3]
                c.set(x, y, tone)
        c.set(cx + r, cy + r // 2 + 1, OCC)
    clumps(c, 6, 2, 5, v[4], 911, fill=0.6)
    clumps(c, 3, 1, 3, v[3], 913, fill=0.5)


def body_ice(c):
    st = ramp("steel")
    c.rect(0, 0, SIZE - 1, SIZE - 1, st[4])
    clumps(c, 4, 3, 7, st[3], 15, fill=0.35)
    # FRACTURES, NOT A HATCH. The first pass ran five full-width diagonals across every tile and
    # the field came out as a stripe pattern - the same failure the roster notes record for evenly
    # spaced anything. These are short polylines that CHANGE ANGLE, which is what a crack does and
    # a hatch does not, and they stop well inside the tile so no two meet at a join.
    for k in range(3):
        x = inside(k, 16, 10)
        y = inside(k, 17, 10)
        ang = hashed(k, 3, 18) * TAU
        for seg in range(3):
            ang += (hashed(k, seg, 19) - 0.5) * 1.6
            run = 4 + int(hashed(k, seg, 30) * 4)
            for j in range(run):
                px = int(x + math.cos(ang) * j)
                py = int(y + math.sin(ang) * j)
                if 1 <= px < SIZE - 1 and 1 <= py < SIZE - 1:
                    c.set(px, py, st[2] if j % 3 else st[3])
            x = int(x + math.cos(ang) * run)
            y = int(y + math.sin(ang) * run)
        if 1 <= x < SIZE - 1 and 1 <= y < SIZE - 1:
            c.set(x, y, st[1])


def body_water(c):
    """Water's one tile, a crop of the forest surface from water.py.

    WATER IS NOT DRAWN FROM TILES ANY MORE (2026-10, `.ai/terrain.md` section 3): its shape comes
    from scripts/WaterShape.cs and its surface from tools/art/water.py through a shader. This tile
    exists so tiles.json still says that water is a terrain and that it BLOCKS - which is what
    TerrainStreamer's flags, the flow field and every placement check read - and nothing paints it.
    """
    src = water.fill_tile()
    for y in range(SIZE):
        for x in range(SIZE):
            c.set(x, y, src.get(x, y))


def body_lava(c):
    st = ramp("stone")
    f = bl.ELEMENTS["fire"]
    c.rect(0, 0, SIZE - 1, SIZE - 1, OCC)
    for y in range(SIZE):
        for x in range(SIZE):
            if hashed(x, y, 20) > 0.60:
                c.set(x, y, st[4])
    # CRACKS, and they have to be ANGULAR. Sweeping the heading with a sine as the crack advanced
    # made every one of them curl into a spiral, and a field of orange spirals reads as something
    # alive rather than as a crust splitting. Straight segments that turn sharply at each joint.
    #
    # This is the one emissive ground in the game, and that is deliberate: lava is a hazard, so it
    # is allowed - required, really - to pull the eye that everything else on the floor gives up.
    for k in range(4):
        x = int(hashed(k, 1, 21) * SIZE)
        y = int(hashed(k, 2, 22) * SIZE)
        ang = hashed(k, 3, 23) * TAU
        for seg in range(3):
            ang += (hashed(k, seg, 24) - 0.5) * 1.9
            run = 6 + int(hashed(k, seg, 25) * 7)
            for j in range(run):
                px = int(x + math.cos(ang) * j) % SIZE
                py = int(y + math.sin(ang) * j) % SIZE
                c.set(px, py, f[3])
                if j % 4 == 0:
                    c.set(px, py, f[2])
            x = int(x + math.cos(ang) * run)
            y = int(y + math.sin(ang) * run)
    for k in range(6):
        c.set(int(hashed(k, 9, 26) * SIZE), int(hashed(k, 8, 27) * SIZE), f[1])


def body_pit(c):
    st = ramp("stone")
    c.rect(0, 0, SIZE - 1, SIZE - 1, OCC)
    for y in range(SIZE):
        for x in range(SIZE):
            if hashed(x, y, 26) > 0.955:
                c.set(x, y, st[4])


# The maze walls are not here: they are tools/art/jail_walls.py, drawn per role with their dressing
# (emit_walls below).


# --- plain floor materials -------------------------------------------------------------------
# No autotile roles: the base layer paints these straight, several variants each so a floor does
# not visibly repeat.

def flagstone(c, r, seed):
    """The Cursed Dungeon floor: laid flags, one full step LIGHTER than every other floor.

    Every other floor is built on the material's `deep` tone, which is right for open ground and
    wrong for a maze. Its walls are `void` (below), and a `deep` floor sits two points of value
    above `occ` - the corridors and the rock between them were the same near-black, and the stage
    was reported as hard to read. So this one stands on `base`, with the mortar in `deep` and no
    `occ` anywhere, which leaves the darkest thing on the floor visibly lighter than the wall it
    runs into. Still the stone row and still no more than two steps either side of the body, so a
    skeleton on it reads exactly as the contract wants.

    Courses are 24 px and sit at the SAME rows in every variant - only the joints and the speckle
    move - because the ground layer mixes variants freely, and a course that shifted per variant
    drew a seam down every tile boundary. The joints shift per course and per variant, so the
    flags do not line up into a grid.
    """
    c.rect(0, 0, SIZE - 1, SIZE - 1, r[2])
    for y in range(SIZE):
        for x in range(SIZE):
            v = hashed(x, y, 140 + seed)
            if v < 0.10:
                c.set(x, y, r[3])
            elif v > 0.985:
                c.set(x, y, r[1])
    for row in range(2):
        top = row * 24
        # mortar along the top of the course, a shaded lip above it and a lit lip below it: the
        # key is upper-left, so each flag catches light on its top edge and drops into its joint
        for x in range(SIZE):
            c.set(x, top, r[4])
            c.set(x, (top - 1) % SIZE, r[3])
            if hashed(x, row, 150 + seed) < 0.6:
                c.set(x, (top + 1) % SIZE, r[1])
        first = (row * 12 + int(hashed(row, seed, 151) * 10)) % SIZE
        for jx in (first, (first + 24) % SIZE):
            for y in range(1, 24):
                py = (top + y) % SIZE
                c.set(jx, py, r[4])
                c.set((jx + 1) % SIZE, py, r[1] if y > 1 else r[2])
    # Wear: one variant in three has a cracked flag, one in three a seam of moss in a joint.
    if seed % 3 == 1:
        crack(c, 10 + int(hashed(seed, 1, 152) * 28), 6 + int(hashed(seed, 2, 153) * 12), 9, r[4], 154)
    elif seed % 3 == 2:
        g = ramp("lichen")
        x = (int(hashed(seed, 3, 155) * 10) + 12) % SIZE
        for y in range(26, 26 + 6 + int(hashed(seed, 4, 156) * 8)):
            c.set(x, y, g[4] if y % 2 else g[3])


def natural(c, r, seed, accent, base=3):
    """An open floor that reads as ground rather than as noise.

    THE OLD FLOOR WAS STATIC: `deep` with 20% `occ` and 10% `shade` pixels hashed independently,
    and on the Ruins, the Cave and the Frozen Waste that came out as a black sandpaper with the
    blob terrains stamped on it as bright rectangles. This is one tone with soft blotches a step
    either side, a few drawn incidents (a crack, a pebble) and almost no single pixels.

    `accent` names one extra row for the blotches, which is all that separates the floors:
    sand is gold with flesh in it, mud is linen with peat, basalt is stone with violet. `base`
    is which stop of the row the floor stands on - `shade` for most, `deep` for sand, whose
    shade is a full step brighter than any other floor's.
    """
    a = ramp(accent) if accent else r
    other = r[3] if base == 4 else r[4]
    # Sand is the bright floor, so it gets half the incident: at the same counts it read as
    # sandpaper again, just yellow.
    calm = base == 4
    c.rect(0, 0, SIZE - 1, SIZE - 1, r[base])
    clumps(c, 5 if calm else 10, 1, 3, other, 40 + seed, fill=0.15 if calm else 0.35)
    clumps(c, 3 if calm else 6, 1, 2, a[4] if accent else r[2], 50 + seed, fill=0.3)
    specks(c, 0.004 if calm else 0.012, r[2], 60 + seed)
    specks(c, 0.008, OCC, 62 + seed)
    # Incidents on some variants only. One on every tile is a pattern with a 48 px period, which
    # is the failure this whole file keeps re-learning. Sand does not crack.
    if seed % 3 == 0 and not calm:
        crack(c, inside(seed, 70, 8), inside(seed + 3, 71, 8), 5 + int(hashed(seed, 1, 72) * 5), OCC, 73 + seed)
    if seed % 2 == 1:
        pebble(c, inside(seed * 3, 74, 5), inside(seed * 3 + 1, 75, 5), 1 + seed % 3 // 2)


def floor_variant(mat, style, seed, accent=None):
    def draw():
        global VARIANT
        VARIANT = 0
        c = raster.Canvas(SIZE, SIZE, None)
        r = ramp(mat)
        if style == "void":
            # Maze walls. Flat occlusion and nothing else: rock the player cannot enter, drawn as
            # the absence of a floor. `occ` rather than #000 because the contract has no pure black
            # - occ is its darkest tone and the one every contact shadow already uses.
            c.rect(0, 0, SIZE - 1, SIZE - 1, OCC)
            return c
        if style == "flagstone":
            flagstone(c, r, seed)
            return c
        if style == "natural":
            natural(c, r, seed, accent, 4 if mat == "gold" else 3)
            if mat == "gold":
                # Two, one stop up: six in the base tone read as a printed carpet of brackets.
                ripples(c, r[3], r[4], 90 + seed, 2)
            return c
        # The old darktile. No palette paints it any more (Volcanic is basalt and the Swamp is mud
        # now), but StageEnvironmentCatalog still names its PNGs as a background texture.
        c.rect(0, 0, SIZE - 1, SIZE - 1, r[4])
        for y in range(SIZE):
            for x in range(SIZE):
                v = hashed(x, y, 40 + seed)
                if v < 0.20:
                    c.set(x, y, OCC)
                elif v > 0.90:
                    c.set(x, y, r[3])
        if style == "tiled":
            for k in (0, 24):
                for x in range(SIZE):
                    c.set(x, (k + seed * 3) % SIZE, OCC)
                for y in range(SIZE):
                    c.set((k + seed * 3) % SIZE, y, OCC)
            for k in (1, 25):
                for x in range(SIZE):
                    c.set(x, (k + seed * 3) % SIZE, r[3])
        return c
    return draw


# --- coverage: how much of a role tile is terrain --------------------------------------------------
#
# Every role is the body with a coverage mask cut out of it. Coverage is a function of a signed
# distance `e` from the terrain's boundary inside the tile (positive = inside the terrain), and
# the edge profile decides where that boundary sits, how wide the fade across it is, how round the
# corners are and how much the line wanders.

ROLES = [
    "fill",
    "edge_top", "edge_bottom", "edge_left", "edge_right",
    "corner_tl", "corner_tr", "corner_bl", "corner_br",
    "inner_corner_tl", "inner_corner_tr", "inner_corner_bl", "inner_corner_br",
    "patch",
]

SIDES = {
    "fill": (),
    "edge_top": ("t",), "edge_bottom": ("b",), "edge_left": ("l",), "edge_right": ("r",),
    "corner_tl": ("t", "l"), "corner_tr": ("t", "r"),
    "corner_bl": ("b", "l"), "corner_br": ("b", "r"),
    "patch": ("t", "b", "l", "r"),
}

INNER = {"inner_corner_tl": ("t", "l"), "inner_corner_tr": ("t", "r"),
         "inner_corner_bl": ("b", "l"), "inner_corner_br": ("b", "r")}

# b: where the boundary (half coverage) sits, in pixels in from the exposed side.
# w: how wide the fade across it is; 0 is a hard edge.
# r: outer-corner radius.  amp: how far the line wanders either way along an edge.
PROFILES = {
    # Ground lying on ground. Wide and soft: these are the transitions that were jarring.
    "soft": dict(b=15.0, w=22.0, r=30.0, amp=6.0),
    # Hazards. Crisp, with an `occ` lip - a lava edge that faded into the grass would be lying
    # about where the damage starts. Rounded all the same: the staircase was the other half of
    # what made every blob look stamped.
    "hard": dict(b=5.0, w=0.0, r=14.0, amp=1.5),
}

# Terrain -> profile. Anything not listed is "soft".
PROFILE_OF = {"lava": "hard", "pit": "hard"}

# Terrain -> a function edge(c, role) that cuts and draws the role's edge itself, in place of a
# profile's mask. For ground somebody LAID - a marble floor, oak boards, a carpet runner (the Court's,
# terrain_court.py) - whose edge is a straight, square, drawn border rather than a fade: a carpet that
# dithered into the flags under it would read as a stain. A chapter module adds its own entries here
# (`tiles.EDGE_OF[name] = fn`); every terrain not listed keeps its profile.
EDGE_OF = {}


def wander(side, i, amp):
    """How far the boundary wanders at position i along a side, in pixels.

    Sums of sin(pi * n * i / 48): every term is ZERO at both ends of the tile, so any variant of
    any edge meets any variant of the next one exactly - the joins are pinned and only the middle
    of each tile wanders. Phase and mix change per variant, which is what lets a long straight
    shore be three different wiggles rather than one wiggle repeated.
    """
    si = "tblr".index(side)
    a1 = hashed(si, 1, 1700) * 2.0 - 1.0
    a2 = hashed(si, 2, 1701) * 2.0 - 1.0
    a3 = hashed(si, 3, 1702) * 2.0 - 1.0
    t = math.pi * i / float(SIZE - 1)
    return amp * (0.6 * a1 * math.sin(t) + 0.3 * a2 * math.sin(2 * t) + 0.25 * a3 * math.sin(3 * t))


def side_depth(side, x, y):
    """Pixels in from `side`, and the position along it."""
    if side == "t":
        return y + 0.5, x
    if side == "b":
        return SIZE - 0.5 - y, x
    if side == "l":
        return x + 0.5, y
    return SIZE - 0.5 - x, y


ADJACENT = [("t", "l"), ("t", "r"), ("b", "l"), ("b", "r")]


def signed_distance(role, x, y, prof):
    """Signed distance from the terrain boundary at (x, y); positive is inside the terrain."""
    if role in INNER:
        v, h = INNER[role]
        dv, iv = side_depth(v, x, y)
        dh, ih = side_depth(h, x, y)
        # The ground is a quarter-disc round the corner, of the same radius the edges either side
        # of it put their boundary at - so at the top row it meets the tile above's left edge,
        # and at the left column the tile beside it's top edge.
        bv = prof["b"] + wander(v, iv, prof["amp"])
        bh = prof["b"] + wander(h, ih, prof["amp"])
        # Near the top row the neighbour that matters is the tile above, whose boundary is the
        # h side's; near the left column it is the v side's. Blend between them by which is near.
        wsum = dv + dh
        b = (bv * dv + bh * dh) / wsum if wsum > 0 else prof["b"]
        return math.hypot(dv, dh) - b

    sides = SIDES[role]
    if not sides:
        return 99.0
    es = {}
    for s in sides:
        d, i = side_depth(s, x, y)
        es[s] = d - (prof["b"] + wander(s, i, prof["amp"]))
    e = min(es.values())
    r = prof["r"]
    for a, b in ADJACENT:
        if a in es and b in es and es[a] < r and es[b] < r:
            e = min(e, r - math.hypot(r - es[a], r - es[b]))
    return e


def apply_mask(c, role, prof, rim_tone):
    """Cut the role's coverage out of a painted body.

    Soft profiles DITHER, in 2x2 blocks: per pixel the fade came out furry and sand-blasted, and
    the same coverage in blocks reads as an irregular edge. Hard profiles cut at the boundary and
    draw a lip inside it.
    """
    w = prof["w"]
    for y in range(SIZE):
        for x in range(SIZE):
            e = signed_distance(role, x, y, prof)
            if e >= w * 0.5 + 0.5 and w > 0:
                continue
            if w <= 0:
                if e < 0:
                    c.set(x, y, CLEAR)
                elif e < 1.2:
                    c.set(x, y, OCC)
                elif e < 2.6:
                    c.set(x, y, rim_tone)
                continue
            cover = (e + w * 0.5) / w
            if cover <= 0:
                c.set(x, y, CLEAR)
                continue
            # The threshold is per 2x2 block, so the edge breaks up in blocks, not in pixels.
            if hashed(x // 2, y // 2, 1800) * 0.92 + 0.04 > cover:
                c.set(x, y, CLEAR)


def variants_for(role):
    if role == "fill":
        return FILL_VARIANTS
    if role.startswith("edge_"):
        return EDGE_VARIANTS
    return CORNER_VARIANTS


def emit_terrain(name, painter, rim_tone, roles):
    global VARIANT
    out = []
    for role in roles:
        for variant in range(variants_for(role)):
            VARIANT = variant
            c = raster.Canvas(SIZE, SIZE, None)
            painter(c)
            if role != "fill":
                if name in EDGE_OF:
                    EDGE_OF[name](c, role)
                else:
                    prof = PROFILES[PROFILE_OF.get(name, "soft")]
                    apply_mask(c, role, prof, rim_tone)
            out.append((role, variant, c))
            if name == "water":
                break
    return out


# terrain -> (painter, rim tone for a hard edge)
TERRAINS = [
    ("grass", body_grass, None),
    ("meadow", body_meadow, None),
    ("moss", body_moss, None),
    ("dirt", body_dirt, None),
    ("overgrowth", body_overgrowth, None),
    ("sand", body_sand, None),
    ("cobble", body_cobble, None),
    ("dark_dirt", body_dark_dirt, None),
    ("mossy_rock", body_mossy_rock, None),
    ("ice", body_ice, None),
    ("water", body_water, None),
    ("lava", body_lava, ramp("stone")[4]),
    ("pit", body_pit, ramp("stone")[4]),
    ("rocky_pit", body_scree, None),
]

# The maze walls: the `wall` terrain, every role, plain variants and dressed ones (jail_walls.py).
WALL_TERRAIN = "wall"
# The shadow a wall throws on the floor beside it: transparent tiles the painter lays on floor cells.
WALL_SHADOW_TERRAIN = "wall_shadow"

HAZARDS = {"lava", "pit"}

# Terrains the player cannot walk into. A hazard hurts you for standing in it; a blocker stops you
# entering at all, and the two are independent - water is not a hazard and lava is not a blocker.
# LevelTilePainter reads this out of the manifest and gives the matching tiles a collision polygon.
BLOCKERS = {"water"}

# material -> (row, style, seeds, accent row)
MATERIALS = [
    ("sand", "gold", "natural", 6, "flesh"),
    ("stonetile", "stone", "flagstone", 6, None),
    ("graystone", "stone", "natural", 6, None),
    # Swamp ground: grey-green peat with darker hollows. Was `darktile`, a near-black square grid.
    ("mud", "linen", "natural", 6, "flesh"),
    # Emberdeep ground: dark stone with violet in it. Was `darktile` too.
    ("basalt", "stone", "natural", 6, "violet"),
    # No palette paints darktile now; kept because StageEnvironmentCatalog names its PNGs.
    ("darktile", "stone", "tiled", 4, None),
    # The old maze wall: a flat `occ` tile. The jail walls (jail_walls.py) replaced it on the Cursed
    # Dungeon; LevelTilePainter still paints it if the `wall` terrain is ever missing a role, so a
    # wall is never painted as floor.
    ("void", "stone", "void", 1, None),
]


# A CHAPTER'S GROUND IN ITS OWN FILE (.ai/campaign.md). A chapter added after the first nine draws
# its terrains and floor materials in tools/art/terrain_<chapter>.py, which may `import tiles` for
# the helpers above and defines TERRAINS and/or MATERIALS in the same shapes as the lists here. They
# are appended below, so two chapters drawn in parallel never edit this file. A chapter may not add a
# hazard or a blocker: either has to agree with three other places (.ai/terrain.md) that no chapter
# owns, so a module that sets HAZARDS or BLOCKERS stops the run.
def chapter_ground_modules():
    """The chapter ground modules present, tools/art/terrain_<chapter>.py, in name order."""
    import glob
    here = os.path.dirname(os.path.abspath(__file__))
    return sorted(os.path.splitext(os.path.basename(p))[0]
                  for p in glob.glob(os.path.join(here, "terrain_*.py")))


def _add_chapter_ground():
    # Run as a script this module is __main__; a chapter module's `import tiles` must get THIS copy
    # (the one whose lists are being extended), not a second one loaded from disk.
    sys.modules.setdefault("tiles", sys.modules[__name__])
    names = set([t[0] for t in TERRAINS] + [m[0] for m in MATERIALS])
    for mod_name in chapter_ground_modules():
        mod = __import__(mod_name)
        if getattr(mod, "HAZARDS", None) or getattr(mod, "BLOCKERS", None):
            raise ValueError("tools/art/%s.py adds a hazard or blocker; see .ai/campaign.md" % mod_name)
        for entry in list(getattr(mod, "TERRAINS", [])) + list(getattr(mod, "MATERIALS", [])):
            if entry[0] in names:
                raise ValueError("tools/art/%s.py redefines ground %r" % (mod_name, entry[0]))
            names.add(entry[0])
        TERRAINS.extend(getattr(mod, "TERRAINS", []))
        MATERIALS.extend(getattr(mod, "MATERIALS", []))


_add_chapter_ground()


def wall_modules():
    """The wall sets drawn here: the Cursed Dungeon's jail walls (jail_walls.py, the `wall` terrain, and
    the shadows every set's walls throw) and any chapter's own, tools/art/<chapter>_walls.py - the
    Court's court_walls.py, say (.ai/endgame-castle.md 11.3). A chapter's module defines WALL_TERRAIN
    (its tiles.json terrain id, which its StageEnvironmentProfile.WallSet names) and the same
    variants(role), paint(...) and STUB_SIDES the jail's does, so it is drawn by the same code, role
    for role. The grid's wall cells stay `wall` whatever set paints them."""
    import glob
    here = os.path.dirname(os.path.abspath(__file__))
    mods = [jail_walls]
    for p in sorted(glob.glob(os.path.join(here, "*_walls.py"))):
        name = os.path.splitext(os.path.basename(p))[0]
        if name != "jail_walls":
            mods.append(__import__(name))
    return mods


def emit_walls(frames, mod=jail_walls):
    """One wall set's tiles (`mod`, see wall_modules): the jail walls and the shadows they throw on the
    floor, or a chapter's own set (no shadows: every set's walls throw the jail's).

    Every one of the fourteen roles, because the maze's two-tile walls produce inner corners as
    well as edges and the painter asks for the role the autotiler resolved - plus three for the
    one-tile stubs the spawn clearing leaves (jail_walls.STUB_SIDES). A dressed variant
    carries its dressing's name and weight in the manifest: LevelTilePainter paints a role's plain
    variants unless a cell rolls a dressed one, never two dressed side by side, and picks among the
    dressed ones by weight. Wall tiles carry no collision of their own - the painter gives each one
    the full-cell square on the terrain layer, as it did the void tile - and are not `blocking`
    terrain: walls are their own flag (TerrainStreamer.FlagWall), not water.

    Returns (tiles written, any failed the palette audit). Tiles left over from an older set (the
    outlined `wall` terrain had different variant counts) are deleted with their .import, so the
    atlas and the manifest never pick up a stale one.
    """
    written = set()
    bad_any = False
    n = 0

    def write(tid, c, terrain, role, dressing=None, weight=1):
        bad = c.audit()
        if bad:
            print("OFF-CONTRACT COLOURS in %s:" % tid)
            for h, k in bad[:4]:
                print("   %s  x%d" % (h, k))
            return False
        c.save(os.path.join(OUT_DIR, tid + ".png"))
        written.add(tid + ".png")
        extra = ''
        if dressing:
            extra = ',\n   "dressing": "%s", "weight": %d' % (dressing, weight)
        frames.append(
            '  {"id": "%s", "fileName": "%s.png", "path": "assets/bonelight/tiles/%s.png",\n'
            '   "placement": "floor", "usageClass": "tile", "collision": "decoration",\n'
            '   "hazard": false, "blocking": false, "width": 48, "height": 48,\n'
            '   "autotile": {"terrain": "%s", "role": "%s"}%s}'
            % (tid, tid, tid, terrain, role, extra))
        return True

    terrain = getattr(mod, "WALL_TERRAIN", WALL_TERRAIN)
    shadows = mod.SHADOW_ROLES if mod is jail_walls else []
    dressed = 0
    for role in ROLES + sorted(mod.STUB_SIDES):
        sides = mod.STUB_SIDES.get(role) or SIDES.get(role, ())
        for v, (dressing, plain_index, weight) in enumerate(mod.variants(role)):
            c = raster.Canvas(SIZE, SIZE, None)
            mod.paint(c, role, dressing, plain_index, sides, INNER.get(role))
            tid = "%s_%s" % (terrain, role) if v == 0 else "%s_%s_%d" % (terrain, role, v)
            if write(tid, c, terrain, role, dressing, weight):
                n += 1
                dressed += 1 if dressing else 0
            else:
                bad_any = True
    for role in shadows:
        c = raster.Canvas(SIZE, SIZE, None)
        mod.shadow(c, role)
        if write("%s_%s" % (WALL_SHADOW_TERRAIN, role), c, WALL_SHADOW_TERRAIN, role):
            n += 1
        else:
            bad_any = True

    # wall_shadow_* starts with "wall_" and is written above, so it is never stale; a chapter's set
    # (court_wall_*) has its own prefix and is never swept up by the jail's.
    stale = [f for f in os.listdir(OUT_DIR)
             if f.startswith(terrain + "_") and f.endswith(".png") and f not in written
             and not any(f.startswith(other + "_") for other in _longer_wall_prefixes(terrain))]
    for f in stale:
        os.remove(os.path.join(OUT_DIR, f))
        if os.path.exists(os.path.join(OUT_DIR, f + ".import")):
            os.remove(os.path.join(OUT_DIR, f + ".import"))
    print("  %-12s %d tiles over %d roles (%d dressed), %d shadows%s"
          % (terrain, n - len(shadows), len(ROLES) + len(mod.STUB_SIDES), dressed,
             len(shadows),
             (", %d stale removed" % len(stale)) if stale else ""))
    return n, bad_any


def _longer_wall_prefixes(terrain):
    """Wall terrains that start with this one's name (none today: `wall_shadow` is written with the
    jail's set), so one set's stale sweep can never delete another's tiles."""
    return [getattr(m, "WALL_TERRAIN", WALL_TERRAIN) for m in wall_modules()
            if getattr(m, "WALL_TERRAIN", WALL_TERRAIN) != terrain
            and getattr(m, "WALL_TERRAIN", WALL_TERRAIN).startswith(terrain + "_")]


def main(only=None):
    """`only`: names of terrains/materials to redraw while iterating. The manifest is written only
    on a full run, so a partial one can never drop the rest of the ground out of tiles.json."""
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    ok = True
    frames = []
    count = 0

    for mod in wall_modules():
        if not only or getattr(mod, "WALL_TERRAIN", WALL_TERRAIN) in only:
            n, bad_walls = emit_walls(frames, mod)
            ok = ok and not bad_walls
            count += n

    for name, painter, rim in TERRAINS:
        if only and name not in only:
            continue
        # Water keeps one fill tile only: see body_water.
        roles = ["fill"] if name == "water" else ROLES
        n = 0
        for role, variant, c in emit_terrain(name, painter, rim, roles):
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s/%s:" % (name, role))
                for h, k in bad[:4]:
                    print("   %s  x%d" % (h, k))
                continue
            tid = "%s_%s" % (name, role) if variant == 0 else "%s_%s_%d" % (name, role, variant)
            c.save(os.path.join(OUT_DIR, tid + ".png"))
            frames.append(
                '  {"id": "%s", "fileName": "%s.png", "path": "assets/bonelight/tiles/%s.png",\n'
                '   "placement": "floor", "usageClass": "tile", "collision": "%s",\n'
                '   "hazard": %s, "blocking": %s, "width": 48, "height": 48,\n'
                '   "autotile": {"terrain": "%s", "role": "%s"}}'
                % (tid, tid, tid,
                   "blocking" if name in BLOCKERS else "decoration",
                   "true" if name in HAZARDS else "false",
                   "true" if name in BLOCKERS else "false",
                   name, role))
            count += 1
            n += 1
        print("  %-12s %d tiles over %d roles" % (name, n, len(roles)))

    for mat, row, style, seeds, accent in MATERIALS:
        if only and mat not in only:
            continue
        for seed in range(seeds):
            c = floor_variant(row, style, seed, accent)()
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s_%d" % (mat, seed))
                for h, k in bad[:4]:
                    print("   %s  x%d" % (h, k))
                continue
            tid = "floor_%s_%d" % (mat, seed)
            c.save(os.path.join(OUT_DIR, tid + ".png"))
            frames.append(
                '  {"id": "%s", "fileName": "%s.png", "path": "assets/bonelight/tiles/%s.png",\n'
                '   "placement": "floor", "materialFamily": "%s", "usageClass": "tile",\n'
                '   "collision": "decoration", "hazard": false, "width": 48, "height": 48}'
                % (tid, tid, tid, mat))
            count += 1
        print("  %-12s %d floor variants" % (mat, seeds))

    if only:
        print("partial run (%s): tiles.json left alone" % ", ".join(only))
        # The tiles it did redraw are in the atlas the game draws from, so it is repacked anyway.
        tile_atlas.main()
        return 0 if ok else 1

    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/tiles.py - the ground every stage is '
                'fought on. Ours, replacing the bought dungeon-floor pack. One line: a raw '
                'newline inside a JSON string is invalid and JsonDocument.Parse throws on it.",\n'
                '  "frames": [\n%s\n  ]\n}\n' % (",\n".join(frames)))
    # Text on Python 3 already; bytes on 2.7, where io.open wants unicode.
    io.open(os.path.join(OUT_DIR, "tiles.json"), "w", encoding="utf-8",
            newline="\n").write(manifest if isinstance(manifest, type(u"")) else manifest.decode("utf-8"))
    # The game draws the ground from one packed texture, not from these files one by one.
    tile_atlas.main()

    if not ok:
        return 1
    print("")
    print("%d tiles across %d terrains, the jail walls and %d materials, palette clean"
          % (count, len(TERRAINS), len(MATERIALS)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:] or None))
