# -*- coding: utf-8 -*-
"""The ground. Run: python tools/art/tiles.py

Every tile every stage is fought on: eleven autotile terrains at fourteen roles each, plus four
plain floor materials. About a hundred and seventy 48x48 tiles and a `tiles.json` manifest, which
`CuratedTileCatalog.LoadMany` merges alongside the Sketchbook's.

It replaces the bought dungeon-floor pack - the single largest piece of art in the game we could
not publish, and the last of it. See `.ai/asset-licensing.md`.

THE ROLES ARE DERIVED, NOT DRAWN. An autotiler asks for fourteen variants of every terrain: the
body, four edges, four outer corners, four inner corners, and an isolated patch. Drawing 154 tiles
by hand would be 154 chances for one edge to disagree with the body it belongs to. So each terrain
supplies ONE painter for its body and one edge treatment, and every role is that body with the
exposed sides banded - which means an edge tile is the fill tile, by construction, and they can
never drift apart.

THE GROUND IS THE QUIETEST THING ON SCREEN and has to stay that way. It covers every pixel the
player is not looking at, so all of it is drawn from the deep half of its material row, the
contrast inside a tile never exceeds two stops, and nothing on it is emissive except lava - which
is a hazard, and is supposed to pull the eye. `.ai/art-direction.md` section 4c is the rule; the
practical test is whether a skeleton standing on it still reads, and a ground tile that competes
is a ground tile that has to go darker.

THEY MUST ALSO TILE. Every painter is seeded off the tile's own pixel coordinates rather than off
a random number, so the same cell always comes out the same way - and every feature wraps at the
edges, so two tiles side by side have no seam between them.
"""

import io
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "tiles")

SIZE = 48
RIM = 5          # how deep an edge band cuts into the tile
TAU = math.pi * 2.0


def ramp(mat):
    return [bl.tone(mat, r) for r in ("hi", "lit", "base", "shade", "deep")]


# Which fill variant is being drawn. Every painter reads it through hashed(), so bumping it
# reshuffles all of a terrain's features without any painter needing to know variants exist.
#
# IT EXISTS BECAUSE ONE FILL PER TERRAIN IS A GRID. With a single tile, every feature - each ice
# fracture, each lava crack, each grass tuft - lands in exactly the same place in every cell, and
# a floor of them reads as wallpaper with a 48-pixel repeat. CuratedTileCatalog collects every
# fill-role tile per terrain and the base ground layer picks between them, so three variants buys
# three times the period for three times the tiles.
VARIANT = 0


def hashed(x, y, salt=0):
    """A stable value in [0,1) for a cell. Deterministic so a tile is the same every run."""
    h = (x * 73856093) ^ (y * 19349663) ^ ((salt + VARIANT * 977) * 83492791)
    h &= 0x7FFFFFFF
    return ((h >> 7) % 10007) / 10007.0


# --- terrain bodies ------------------------------------------------------------------------------
# Each takes a canvas and fills all 48x48. Everything wraps, so tiles abut without a seam.

def body_grass(c):
    g = ramp("lichen")
    c.rect(0, 0, SIZE - 1, SIZE - 1, g[3])
    for y in range(SIZE):
        for x in range(SIZE):
            v = hashed(x, y, 1)
            if v < 0.16:
                c.set(x, y, g[4])
            elif v > 0.90:
                c.set(x, y, g[2])
    # blades: short upright strokes, never touching the tile edge sideways so they cannot clash
    for k in range(26):
        bx = int(hashed(k, 3, 2) * SIZE)
        by = int(hashed(k, 7, 3) * SIZE)
        for j in range(2 + int(hashed(k, 11, 4) * 3)):
            c.set(bx, (by - j) % SIZE, g[2] if j else g[1])


def body_sand(c):
    s = ramp("gold")
    f = ramp("flesh")
    c.rect(0, 0, SIZE - 1, SIZE - 1, s[4])
    for y in range(SIZE):
        for x in range(SIZE):
            v = hashed(x, y, 5)
            if v < 0.22:
                c.set(x, y, f[4])
            elif v > 0.88:
                c.set(x, y, s[3])
    # ripples: shallow sine rows, wrapped
    for row in range(4):
        base = row * 12 + 5
        for x in range(SIZE):
            y = int(base + math.sin((x / float(SIZE)) * TAU * 2 + row) * 2.4) % SIZE
            c.set(x, y, s[3])
            c.set(x, (y + 1) % SIZE, f[4])


def body_cobble(c):
    st = ramp("stone")
    c.rect(0, 0, SIZE - 1, SIZE - 1, st[4])
    # running bond, six across, with the courses offset and every stone jittered
    for row in range(6):
        oy = row * 8
        off = 4 if row % 2 else 0
        for col in range(7):
            ox = col * 8 - off
            jx = int(hashed(col, row, 6) * 2) - 1
            jy = int(hashed(col, row, 7) * 2) - 1
            for y in range(1, 7):
                for x in range(1, 7):
                    if (x in (1, 6) and y in (1, 6)):
                        continue
                    px, py = (ox + x + jx) % SIZE, (oy + y + jy) % SIZE
                    tone = st[2] if (x <= 2 and y <= 2) else (st[3] if y < 5 else st[4])
                    c.set(px, py, tone)


def body_dark_dirt(c):
    st = ramp("stone")
    f = ramp("flesh")
    c.rect(0, 0, SIZE - 1, SIZE - 1, st[4])
    for y in range(SIZE):
        for x in range(SIZE):
            v = hashed(x, y, 8)
            if v < 0.30:
                c.set(x, y, OCC)
            elif v > 0.92:
                c.set(x, y, f[4])
    for k in range(14):
        px = int(hashed(k, 2, 9) * SIZE)
        py = int(hashed(k, 5, 10) * SIZE)
        c.set(px, py, st[3])
        c.set((px + 1) % SIZE, py, st[3])


def body_mossy_rock(c):
    st = ramp("stone")
    g = ramp("lichen")
    body_cobble(c)
    for k in range(9):
        px = int(hashed(k, 1, 11) * SIZE)
        py = int(hashed(k, 4, 12) * SIZE)
        r = 3 + int(hashed(k, 6, 13) * 4)
        for y in range(-r, r + 1):
            for x in range(-r, r + 1):
                if x * x + y * y > r * r:
                    continue
                if hashed(px + x, py + y, 14) < 0.55:
                    c.set((px + x) % SIZE, (py + y) % SIZE, g[3] if (x + y) % 2 else g[4])
    c.set(2, 2, st[2])


def body_ice(c):
    st = ramp("steel")
    c.rect(0, 0, SIZE - 1, SIZE - 1, st[4])
    for y in range(SIZE):
        for x in range(SIZE):
            if hashed(x, y, 15) > 0.86:
                c.set(x, y, st[3])
    # FRACTURES, NOT A HATCH. The first pass ran five full-width diagonals across every tile and
    # the field came out as a stripe pattern - the same failure the roster notes record for evenly
    # spaced anything. These are short polylines that CHANGE ANGLE, which is what a crack does and
    # a hatch does not, and they stop well inside the tile so no two meet at a join.
    for k in range(3):
        x = int(hashed(k, 1, 16) * SIZE)
        y = int(hashed(k, 2, 17) * SIZE)
        ang = hashed(k, 3, 18) * TAU
        for seg in range(3):
            ang += (hashed(k, seg, 19) - 0.5) * 1.6
            run = 5 + int(hashed(k, seg, 30) * 6)
            for j in range(run):
                px = int(x + math.cos(ang) * j) % SIZE
                py = int(y + math.sin(ang) * j) % SIZE
                c.set(px, py, st[2] if j % 3 else st[3])
            x = int(x + math.cos(ang) * run)
            y = int(y + math.sin(ang) * run)
        c.set(x % SIZE, y % SIZE, st[1])


def body_water(c):
    w = ramp("wool")
    c.rect(0, 0, SIZE - 1, SIZE - 1, w[4])
    for y in range(SIZE):
        for x in range(SIZE):
            v = hashed(x, y, 19)
            if v < 0.18:
                c.set(x, y, w[3])
    # ripples: two wrapped sine bands, the lighter one shorter
    for row in range(6):
        base = row * 8 + 3
        for x in range(SIZE):
            y = int(base + math.sin((x / float(SIZE)) * TAU * 3 + row * 1.3) * 2.0) % SIZE
            c.set(x, y, w[2])
            if (x + row) % 5 < 2:
                c.set(x, (y - 1) % SIZE, w[1])


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


def body_rocky_pit(c):
    st = ramp("stone")
    body_pit(c)
    for k in range(10):
        px = int(hashed(k, 1, 27) * SIZE)
        py = int(hashed(k, 2, 28) * SIZE)
        r = 2 + int(hashed(k, 3, 29) * 2)
        for y in range(-r, r + 1):
            for x in range(-r, r + 1):
                if x * x + y * y <= r * r:
                    c.set((px + x) % SIZE, (py + y) % SIZE, st[4] if (x + y) % 2 else st[3])


def body_wall(c):
    st = ramp("stone")
    c.rect(0, 0, SIZE - 1, SIZE - 1, st[3])
    for row in range(4):
        oy = row * 12
        off = 8 if row % 2 else 0
        for col in range(4):
            ox = col * 16 - off
            for y in range(1, 11):
                for x in range(1, 15):
                    px, py = (ox + x) % SIZE, (oy + y) % SIZE
                    c.set(px, py, st[2] if y <= 2 else (st[3] if y < 8 else st[4]))
            for y in range(0, 12):
                c.set((ox) % SIZE, (oy + y) % SIZE, OCC)
        for x in range(SIZE):
            c.set(x, (oy) % SIZE, OCC)
    c.set(3, 3, st[1])


# --- plain floor materials -------------------------------------------------------------------
# No autotile roles: the base layer paints these straight, four variants each so a floor does not
# visibly repeat.

def floor_variant(mat, style, seed):
    def draw():
        c = raster.Canvas(SIZE, SIZE, None)
        r = ramp(mat)
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
        elif style == "flecked":
            for k in range(18):
                px = int(hashed(k, seed, 41) * SIZE)
                py = int(hashed(k, seed + 5, 42) * SIZE)
                c.set(px, py, r[2])
                c.set((px + 1) % SIZE, py, r[3])
        return c
    return draw


# --- role derivation ------------------------------------------------------------------------------

ROLES = [
    "fill",
    "edge_top", "edge_bottom", "edge_left", "edge_right",
    "corner_tl", "corner_tr", "corner_bl", "corner_br",
    "inner_corner_tl", "inner_corner_tr", "inner_corner_bl", "inner_corner_br",
    "patch",
]

WALL_ROLES = ["fill", "edge_top", "edge_bottom", "edge_left", "edge_right",
              "corner_tl", "corner_tr", "corner_bl", "corner_br"]

SIDES = {
    "fill": (),
    "edge_top": ("t",), "edge_bottom": ("b",), "edge_left": ("l",), "edge_right": ("r",),
    "corner_tl": ("t", "l"), "corner_tr": ("t", "r"),
    "corner_bl": ("b", "l"), "corner_br": ("b", "r"),
    "patch": ("t", "b", "l", "r"),
}

INNER = {"inner_corner_tl": ("t", "l"), "inner_corner_tr": ("t", "r"),
         "inner_corner_bl": ("b", "l"), "inner_corner_br": ("b", "r")}


def band(c, side, tone, shade, depth=RIM):
    """Darken and outline one exposed side, with a ragged inner boundary.

    Ragged rather than straight because a ruler-straight band on every tile of a shoreline reads
    as a drawn border round a shape, which is the one thing an autotiled terrain must not look
    like. The jitter is hashed off the coordinate, so two adjacent tiles agree at the join.
    """
    for i in range(SIZE):
        wobble = int(hashed(i, 0, 60 + "tblr".index(side)) * 2)
        for d in range(depth + wobble):
            t = d / float(depth + wobble)
            col = OCC if d == 0 else (shade if t < 0.55 else tone)
            if side == "t":
                c.set(i, d, col)
            elif side == "b":
                c.set(i, SIZE - 1 - d, col)
            elif side == "l":
                c.set(d, i, col)
            else:
                c.set(SIZE - 1 - d, i, col)


def notch(c, corner, tone, shade):
    """An inner corner: the terrain wraps around a hole, so only that one corner is treated."""
    v, h = corner
    for y in range(SIZE):
        for x in range(SIZE):
            px = x if h == "l" else SIZE - 1 - x
            py = y if v == "t" else SIZE - 1 - y
            d = max(0, RIM - int(math.hypot(px, py)))
            if d <= 0:
                continue
            c.set(x, y, OCC if d >= RIM - 1 else (shade if d > RIM * 0.45 else tone))


def emit_terrain(name, painter, tone, shade, roles=None, variant=0):
    global VARIANT
    VARIANT = variant
    out = []
    for role in (roles or ROLES):
        c = raster.Canvas(SIZE, SIZE, None)
        painter(c)
        if role in INNER:
            notch(c, INNER[role], tone, shade)
        else:
            for side in SIDES[role]:
                band(c, side, tone, shade)
        out.append((role, c))
    return out


# terrain -> (painter, edge tone, edge shade)
TERRAINS = [
    ("grass", body_grass, ramp("lichen")[4], OCC),
    ("sand", body_sand, ramp("flesh")[4], OCC),
    ("cobble", body_cobble, ramp("stone")[4], OCC),
    ("dark_dirt", body_dark_dirt, OCC, OCC),
    ("mossy_rock", body_mossy_rock, ramp("stone")[4], OCC),
    ("ice", body_ice, ramp("steel")[4], OCC),
    ("water", body_water, ramp("wool")[4], OCC),
    ("lava", body_lava, ramp("stone")[4], OCC),
    ("pit", body_pit, OCC, OCC),
    ("rocky_pit", body_rocky_pit, ramp("stone")[4], OCC),
    ("wall", body_wall, ramp("stone")[4], OCC),
]

HAZARDS = {"lava", "pit"}

# material -> (row, style)
MATERIALS = [
    ("sand", "gold", "flecked"),
    ("stonetile", "stone", "tiled"),
    ("graystone", "stone", "flecked"),
    ("darktile", "stone", "tiled"),
]


def main():
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    ok = True
    frames = []
    count = 0

    for name, painter, tone, shade in TERRAINS:
        roles = WALL_ROLES if name == "wall" else None
        # Variant 0 carries the whole role set; variants 1 and 2 add a fill only. Fill is the
        # role the base ground layer picks between, and therefore the only one whose repeat the
        # player ever sees across a whole screen.
        work = [(0, roles)] + [(v, ["fill"]) for v in (1, 2)]
        for variant, these in work:
          for role, c in emit_terrain(name, painter, tone, shade, these, variant):
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s/%s:" % (name, role))
                for h, n in bad[:4]:
                    print("   %s  x%d" % (h, n))
                continue
            tid = "%s_%s" % (name, role) if variant == 0 else "%s_%s_%d" % (name, role, variant)
            c.save(os.path.join(OUT_DIR, tid + ".png"))
            frames.append(
                '  {"id": "%s", "fileName": "%s.png", "path": "assets/bonelight/tiles/%s.png",\n'
                '   "placement": "floor", "usageClass": "tile", "collision": "decoration",\n'
                '   "hazard": %s, "width": 48, "height": 48,\n'
                '   "autotile": {"terrain": "%s", "role": "%s"}}'
                % (tid, tid, tid, "true" if name in HAZARDS else "false", name, role))
            count += 1
        print("  %-12s %d roles" % (name, len(roles or ROLES)))

    for mat, row, style in MATERIALS:
        for seed in range(4):
            c = floor_variant(row, style, seed)()
            bad = c.audit()
            if bad:
                ok = False
                print("OFF-CONTRACT COLOURS in %s_%d" % (mat, seed))
                continue
            tid = "floor_%s_%d" % (mat, seed)
            c.save(os.path.join(OUT_DIR, tid + ".png"))
            frames.append(
                '  {"id": "%s", "fileName": "%s.png", "path": "assets/bonelight/tiles/%s.png",\n'
                '   "placement": "floor", "materialFamily": "%s", "usageClass": "tile",\n'
                '   "collision": "decoration", "hazard": false, "width": 48, "height": 48}'
                % (tid, tid, tid, mat))
            count += 1
        print("  %-12s 4 floor variants" % mat)

    manifest = ('{\n'
                '  "_comment": "Generated by tools/art/tiles.py - the ground every stage is '
                'fought on. Ours, replacing the bought dungeon-floor pack. One line: a raw '
                'newline inside a JSON string is invalid and JsonDocument.Parse throws on it.",\n'
                '  "frames": [\n%s\n  ]\n}\n' % (",\n".join(frames)))
    io.open(os.path.join(OUT_DIR, "tiles.json"), "w", encoding="utf-8",
            newline="\n").write(manifest.decode("utf-8"))

    if not ok:
        return 1
    print("")
    print("%d tiles across %d terrains and %d materials, palette clean"
          % (count, len(TERRAINS), len(MATERIALS)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
