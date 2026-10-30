# -*- coding: utf-8 -*-
"""The Court's building (.ai/endgame-castle.md section 5). Run: python tools/art/court_layout.py

    assets/bonelight/layouts/court_layout.json   the building, cell by cell, and what is in it
    tools/art/_court_layout_map.png              a map of it, for looking at (not committed)

ONE BUILDING, THE SAME EVERY RUN (5.1). The Court is the first stage whose ground is read from a file
rather than a function: LevelGenerator.Authored.cs reads the grid this writes, and every cell outside
it is wall. A building does not move, so a player who has found the Great Hall can find it again,
which turns a labyrinth into a place - and makes every room's achievement free of luck.

WHAT IS HAND-PLACED AND WHAT IS NOT. The rooms are placed by hand, below, to the plan in 5.1: the
player comes up the stair from the cells in the SOUTH-WEST corner and the throne room doors are in
the NORTH-EAST, so the whole chapter is walked up and to the right, the bridge's direction. The halls
are a loose ring round the Well Court with spurs out to the rooms (5.2, "few junctions"), each wing's
halls with their own carpet runner. What is GENERATED, from a fixed seed (rng.random() only, so the
output is the same on Python 2 and 3), is everything that follows from that: the runners laid down
the middle of each hall, the glyph over every door, the signs at every junction naming what lies down
each branch, where the furniture stands, and which wall faces carry a portrait.

THE GRID'S RULES, which the C# side relies on and this file checks:
  - walls are at least two tiles thick everywhere (the wall art has a top and a face; a one-tile wall
    is a row of stubs), and a door is a gap through one;
  - a laid floor (marble, boards) and a carpet never touch a wall: the cell at the foot of a wall is
    the plain flags, which is where the wall's shadow is drawn (TerrainStreamer only shades the base
    floor) - so every room reads as a laid floor inside a border of stone;
  - a hidden way is a REAL GAP in a wall with a tapestry hung over it (5.5): floor, which the flow field
    knows, so the tell is enemies walking out of the wall. No wall ever changes at runtime.

AND IT CHECKS ITSELF (section 9.3), and refuses to write a building that fails:
  - every room is reachable from the start without a hidden way;
  - every hidden room is reachable with one - and is NOT reachable without it;
  - the throne room doors are reachable;
  - every door glyph and every junction sign names a real room.
"""
from __future__ import division, print_function

import io
import json
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "layouts")
OUT = os.path.join(OUT_DIR, "court_layout.json")
PREVIEW = os.path.join(ROOT, "tools", "art", "_court_layout_map.png")

SEED = 20261030
W = H = 192                       # 6 x 6 chunks of 32 tiles (5.1)

WALL = "#"
FLOOR = "."
# The grid's characters, and the tiles.json terrain each is painted with. Floor is the base ground
# (the castle's flags, the palette's GroundMaterial `stonetile`); every other floor is an overlay
# terrain that fades nothing - laid floors and carpets have drawn edges (terrain_court.py).
LEGEND = {
    WALL: "wall",
    FLOOR: "",
    "m": "court_marble",
    "b": "court_boards",
    "c": "cobble",
    "R": "runner_red",
    "U": "runner_blue",
    "G": "runner_green",
    "Y": "runner_gold",
}
RUNNER_OF = {"red": "R", "blue": "U", "green": "G", "gold": "Y"}

# ------------------------------------------------------------------------------------------------
# THE PLAN. Rectangles are (x, y, w, h) in cells, x east and y south, (0, 0) the north-west corner.

# The halls: 8 wide (5.2 - on a phone both walls and the swarm between them are on screen at once).
# A loop round the Well Court (its four sides), and spurs out to the wings. Each has its wing, whose
# runner is laid down its middle.
HALLS = [
    # id,            rect,                   wing,    axis
    ("spine",        (10, 52, 136, 8),       "gold",  "h"),   # the north hall, from the Solar's end to the throne
    ("throne_hall",  (138, 44, 8, 16),       "gold",  "v"),   # up into the throne room's vestibule
    ("loop_west",    (58, 52, 8, 92),        "blue",  "v"),
    ("blue_spur",    (24, 94, 34, 8),        "blue",  "h"),   # between the Library and the Chapel
    ("loop_east",    (126, 52, 8, 120),      "red",   "v"),   # and on south to the Keepsake Doors
    ("south_street", (8, 136, 126, 8),       "green", "h"),
    ("green_spur",   (32, 144, 8, 40),       "green", "v"),   # down to the stair from the cells
]

# The rooms (5.4). `counted`: one of the thirteen ("Every Room"); the start and the throne room's
# vestibule are places, not rooms. `floor` is the room's laid floor (None: the plain flags).
ROOMS = [
    # id,                 name,                    kind,           rect,                  wing,    floor, hidden, counted, glyph
    ("great_hall",        "The Great Hall",        "great_hall",   (136, 64, 46, 48),     "red",   "m",   False, True,  "cup"),
    ("kitchens",          "The Kitchens",          "kitchens",     (136, 114, 52, 26),    "red",   None,  False, True,  "pot"),
    ("minstrels_gallery", "The Minstrels' Gallery", "minstrels",   (150, 52, 38, 10),     "red",   "b",   False, True,  "lute"),
    ("chapel",            "The Chapel",            "chapel",       (8, 104, 48, 30),      "blue",  "m",   False, True,  "bell"),
    ("library",           "The Library",           "library",      (8, 62, 48, 30),       "blue",  "b",   False, True,  "book"),
    ("armoury",           "The Armoury",           "armoury",      (8, 146, 22, 20),      "green", None,  False, True,  "sword"),
    ("kennels",           "The Kennels",           "kennels",      (42, 146, 24, 38),     "green", None,  False, True,  "collar"),
    ("map_room",          "The Map Room",          "map_room",     (72, 146, 48, 34),     "green", "b",   False, True,  "compass"),
    ("treasury",          "The Treasury",          "treasury",     (78, 16, 36, 34),      "gold",  "m",   False, True,  "key"),
    ("gallery",           "The Gallery",           "gallery",      (68, 62, 56, 14),      None,    "b",   False, True,  "frame"),
    ("solar",             "The Solar",             "solar",        (8, 8, 40, 42),        "gold",  "b",   True,  True,  "moon"),
    ("keepsake_doors",    "The Keepsake Doors",    "keepsake",     (134, 164, 52, 8),     None,    "m",   False, True,  "arch"),
    ("well_court",        "The Well Court",        "well_court",   (68, 78, 56, 56),      None,    "c",   False, True,  "well"),
    ("start",             "The Stair",             "start",        (10, 170, 20, 14),     "green", None,  False, False, "stair"),
    ("throne_doors",      "The Throne Room Doors", "throne_doors", (136, 10, 50, 34),     "gold",  "m",   False, False, "crown"),
]

# The doors: a gap through a wall between a room and a hall (or a room and a room). (room, rect,
# what it opens onto). A door is 4 cells - 192 px, two of the widest body in the game - so the swarm
# is never queued at one.
DOORS = [
    ("great_hall",        (134, 86, 2, 4),   "loop_east"),
    ("kitchens",          (134, 124, 2, 4),  "loop_east"),
    ("chapel",            (30, 102, 4, 2),   "blue_spur"),
    ("library",           (30, 92, 4, 2),    "blue_spur"),
    ("library",           (30, 60, 4, 2),    "spine"),
    ("armoury",           (17, 144, 4, 2),   "south_street"),
    ("kennels",           (40, 160, 2, 4),   "green_spur"),
    ("map_room",          (94, 144, 4, 2),   "south_street"),
    ("treasury",          (94, 50, 4, 2),    "spine"),
    ("gallery",           (74, 60, 4, 2),    "spine"),
    ("gallery",           (114, 60, 4, 2),   "spine"),
    ("gallery",           (94, 76, 4, 2),    "well_court"),
    ("well_court",        (66, 104, 2, 4),   "loop_west"),
    ("well_court",        (124, 104, 2, 4),  "loop_east"),
    ("well_court",        (94, 134, 4, 2),   "south_street"),
    ("start",             (30, 175, 2, 4),   "green_spur"),
    ("throne_doors",      (138, 44, 8, 0),   "throne_hall"),   # the hall runs straight in: no wall to cut
    ("keepsake_doors",    (134, 164, 0, 8),  "loop_east"),     # likewise
]

# The back stair (5.4): the Minstrels' Gallery is reached only from the Kitchens, up a stair in the
# house's east wall. A passage, not a hall: no runner, no junction sign.
PASSAGES = [
    ("back_stair", (184, 52, 4, 62)),
]

# The hidden ways (5.5): a real gap in a wall with a tapestry over it. (room, rect, the hall side).
HIDDEN = [
    ("solar", (14, 50, 3, 2), "spine"),
]

# The Keepsake Doors (5.4): ten alcoves off the north side of their corridor, each behind a door in
# the corridor's wall - one door for each chapter keepsake (CourtRooms.KeepsakeDoorChapters).
ALCOVES = 10
ALCOVE_X0, ALCOVE_PITCH, ALCOVE_W = 136, 5, 3
ALCOVE_Y, ALCOVE_DEPTH = 156, 6           # the alcove; its mouth is the wall below it, rows 162-163

# The throne room doors, in the vestibule's north wall: 8 cells wide, on the wall's face.
THRONE_DOORS = (157, 8, 8, 2)

# ------------------------------------------------------------------------------------------------


def rect_cells(r):
    x, y, w, h = r
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            yield xx, yy


class Grid(object):
    def __init__(self):
        self.c = [[WALL] * W for _ in range(H)]

    def get(self, x, y):
        if 0 <= x < W and 0 <= y < H:
            return self.c[y][x]
        return WALL

    def set(self, x, y, ch):
        if 0 <= x < W and 0 <= y < H:
            self.c[y][x] = ch

    def carve(self, r, ch=FLOOR):
        for x, y in rect_cells(r):
            self.set(x, y, ch)

    def walkable(self, x, y):
        return self.get(x, y) != WALL


def inset(r, k):
    x, y, w, h = r
    return (x + k, y + k, w - 2 * k, h - 2 * k)


def centre(r):
    x, y, w, h = r
    return (x + w / 2.0, y + h / 2.0)


def build_grid():
    g = Grid()
    for _, r, _, _ in HALLS:
        g.carve(r)
    for _, r in PASSAGES:
        g.carve(r)
    for room in ROOMS:
        g.carve(room[3])
    for _, r, _ in DOORS:
        g.carve(r)
    for _, r, _ in HIDDEN:
        g.carve(r)
    # The alcoves and their mouths.
    for k in range(ALCOVES):
        x = ALCOVE_X0 + k * ALCOVE_PITCH
        g.carve((x, ALCOVE_Y, ALCOVE_W, ALCOVE_DEPTH))
        g.carve((x, ALCOVE_Y + ALCOVE_DEPTH, ALCOVE_W, 2))
    return g


def lay_floors(g):
    """The laid floors, one cell in from every wall (see the module note on shadows), and the runners
    down the middle of every hall, two cells wide (a one-cell carpet has no edge tile to draw)."""
    def laid(r, ch):
        for x, y in rect_cells(inset(r, 1)):
            if g.get(x, y) != FLOOR:
                continue
            # One cell clear of any wall, including the jambs of a door.
            if any(g.get(x + dx, y + dy) == WALL for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                continue
            g.set(x, y, ch)

    for room in ROOMS:
        if room[5]:
            laid(room[3], room[5])

    # The cross halls' runners first and the long east-west halls' over them, so where two wings meet
    # the spine and the south street run through and the crossing runner stops at their edge.
    runners = set(RUNNER_OF.values())
    for hid, r, wing, axis in sorted(HALLS, key=lambda h: 0 if h[3] == "v" else 1):
        ch = RUNNER_OF[wing]
        x, y, w, h = r
        if axis == "h":
            mid = y + h // 2
            for xx in range(x + 2, x + w - 2):
                for yy in (mid - 1, mid):
                    if g.get(xx, yy) == FLOOR or g.get(xx, yy) in runners:
                        g.set(xx, yy, ch)
        else:
            mid = x + w // 2
            for yy in range(y + 2, y + h - 2):
                for xx in (mid - 1, mid):
                    if g.get(xx, yy) == FLOOR:
                        g.set(xx, yy, ch)

    # A runner never touches a wall either: trim any cell of one that does (a hall's end against a
    # door jamb). And a runner one cell wide anywhere (a trim, a junction's corner) would draw as a
    # patch, so those go too.
    changed = True
    while changed:
        changed = False
        for y in range(H):
            for x in range(W):
                ch = g.get(x, y)
                if ch in (WALL, FLOOR):
                    continue
                near_wall = any(g.get(x + dx, y + dy) == WALL for dx in (-1, 0, 1) for dy in (-1, 0, 1))
                # The autotiler has a piece for a cell only if it has a neighbour of its own carpet on
                # at least one side north-south AND one side east-west; anything less is a `patch`.
                ns = g.get(x, y - 1) == ch or g.get(x, y + 1) == ch
                ew = g.get(x - 1, y) == ch or g.get(x + 1, y) == ch
                if near_wall or not ns or not ew:
                    g.set(x, y, FLOOR)
                    changed = True


# ------------------------------------------------------------------------------------------------
# Reachability.

def flood(g, start, blocked=()):
    """Cells reachable from `start` over walkable cells, never entering a cell in `blocked`."""
    blocked = set(blocked)
    seen = {start}
    todo = [start]
    while todo:
        x, y = todo.pop()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n in seen or n in blocked or not g.walkable(*n):
                continue
            seen.add(n)
            todo.append(n)
    return seen


def hidden_cells():
    out = set()
    for _, r, _ in HIDDEN:
        out.update(rect_cells(r))
    return out


# ------------------------------------------------------------------------------------------------
# Glyphs: over every door, and on a sign at every junction naming what lies down each branch.

def room_by_id(rid):
    for room in ROOMS:
        if room[0] == rid:
            return room
    return None


def door_plaques(g):
    """One carved glyph beside each door, on the wall's face where there is one (5.2). A door in a
    horizontal wall has a face either side of it on the wall's lower row; a door in a vertical wall
    has the face of the wall's end just north of it. Positions are cell centres, in cells."""
    out = []
    for rid, r, onto in DOORS:
        x, y, w, h = r
        if w == 0 or h == 0:
            continue                               # an open archway: its hall's sign says it
        room = room_by_id(rid)
        if w > h:
            # Through a horizontal wall: rows y, y + 1; the lower is the face (floor south of it).
            row = y + 1
            for cx in (x - 1, x + w):
                if g.get(cx, row) == WALL and g.walkable(cx, row + 1):
                    out.append({"room": rid, "glyph": room[8], "at": [cx + 0.5, row + 0.5], "face": True})
        else:
            # Through a vertical wall: the cells north of the gap are the end of the wall, a face.
            row = y - 1
            if g.get(x, row) == WALL and g.get(x + w - 1, row) == WALL:
                out.append({"room": rid, "glyph": room[8], "at": [x + w / 2.0, row + 0.5], "face": True})
    return out


def junctions():
    """Where two halls cross or meet: the overlap of their rectangles."""
    out = []
    for i, (a, ra, _, axa) in enumerate(HALLS):
        for b, rb, _, axb in HALLS[i + 1:]:
            if axa == axb:
                continue
            x0 = max(ra[0], rb[0])
            y0 = max(ra[1], rb[1])
            x1 = min(ra[0] + ra[2], rb[0] + rb[2])
            y1 = min(ra[1] + ra[3], rb[1] + rb[3])
            if x1 > x0 and y1 > y0:
                out.append(((x0, y0, x1 - x0, y1 - y0), a, b))
    return out


def door_cells_onto_halls(g):
    """For each door, the hall cells just outside it: (cell -> room id)."""
    out = {}
    hall_cells = set()
    for _, r, _, _ in HALLS:
        hall_cells.update(rect_cells(r))
    for rid, r, onto in DOORS:
        x, y, w, h = r
        if w == 0 or h == 0:
            # An open archway: the room begins where the hall ends.
            for cx, cy in rect_cells((x - 1, y - 1, max(w, 0) + 2, max(h, 0) + 2)):
                if (cx, cy) in hall_cells:
                    out[(cx, cy)] = rid
            continue
        for cx, cy in rect_cells((x - 1, y - 1, w + 2, h + 2)):
            if (cx, cy) in hall_cells:
                out[(cx, cy)] = rid
    return out, hall_cells


def junction_signs(g):
    """At every junction, for each branch leaving it, the rooms that branch leads to (nearest first,
    three at most), on a two-cell plaque on a wall beside the branch's mouth, with an arrow."""
    doors_at, hall_cells = door_cells_onto_halls(g)
    used = set()
    out = []
    for jr, a, b in junctions():
        jx, jy, jw, jh = jr
        jcells = set(rect_cells(jr))
        branches = {
            "n": [(x, jy - 1) for x in range(jx, jx + jw)],
            "s": [(x, jy + jh) for x in range(jx, jx + jw)],
            "w": [(jx - 1, y) for y in range(jy, jy + jh)],
            "e": [(jx + jw, y) for y in range(jy, jy + jh)],
        }
        # Where each branch's sign may go: a pair of wall cells (x, y), (x + 1, y), faces first.
        spots = {
            "n": [(jx - 2, jy - 1), (jx + jw, jy - 1)],
            "e": [(jx + jw, jy - 1), (jx + jw, jy + jh), (jx + jw + 2, jy - 1)],
            "w": [(jx - 2, jy - 1), (jx - 2, jy + jh), (jx - 4, jy - 1)],
            "s": [(jx - 2, jy + jh), (jx + jw, jy + jh)],
        }
        for d in ("n", "e", "s", "w"):
            mouth = [c for c in branches[d] if c in hall_cells]
            if len(mouth) < jw // 2 and len(mouth) < jh // 2:
                continue
            if not mouth:
                continue
            # Walk the halls down the branch, never back through the junction: the rooms whose doors
            # open onto them, by how far down.
            dist = {c: 1 for c in mouth}
            frontier = list(mouth)
            found = []
            while frontier:
                nxt = []
                for c in frontier:
                    rid = doors_at.get(c)
                    if rid and rid not in [f[1] for f in found]:
                        found.append((dist[c], rid))
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        n = (c[0] + dx, c[1] + dy)
                        if n in dist or n in jcells or n not in hall_cells:
                            continue
                        dist[n] = dist[c] + 1
                        nxt.append(n)
                frontier = nxt
            found.sort()
            rooms = []
            for _, rid in found:
                room = room_by_id(rid)
                if room is None or room[6]:
                    continue                       # a hidden room is never signposted
                if rid not in rooms:
                    rooms.append(rid)
            if not rooms:
                continue
            for sx, sy in spots[d]:
                pair = ((sx, sy), (sx + 1, sy))
                if any(p in used for p in pair):
                    continue
                if not all(g.get(*p) == WALL for p in pair):
                    continue
                face = all(g.walkable(p[0], p[1] + 1) for p in pair)
                cap = all(g.walkable(p[0], p[1] - 1) for p in pair)
                if not (face or cap):
                    continue
                used.update(pair)
                out.append({"junction": [jx, jy, jw, jh], "dir": d, "rooms": rooms[:3],
                            "glyphs": [room_by_id(r)[8] for r in rooms[:3]],
                            "at": [sx + 1.0, sy + 0.5], "face": face})
                break
    return out


# ------------------------------------------------------------------------------------------------
# Furniture: what stands in each room. Cell-centre positions; CourtRules places the sprites.

def furniture(rng):
    out = []

    def put(kind, room, x, y, **extra):
        item = {"kind": kind, "room": room, "at": [x, y]}
        item.update(extra)
        out.append(item)

    # The Great Hall: three long tables laid for a feast, four platters on each - the twelve fowl.
    gx, gy, gw, gh = room_by_id("great_hall")[3]
    for t, tx in enumerate((gx + 9, gx + 23, gx + 37)):
        put("banquet_table", "great_hall", tx, gy + 24.0, index=t)
        for k in range(4):
            put("platter", "great_hall", tx, gy + 14.5 + k * 6.5, index=t * 4 + k)
    # The high table across the north end, on its dais.
    put("high_table", "great_hall", gx + gw / 2.0, gy + 5.0)

    # The Chapel: the altar at the north, two banks of pews.
    cx, cy, cw, ch = room_by_id("chapel")[3]
    put("altar", "chapel", cx + cw / 2.0, cy + 4.5)
    for row in range(5):
        for side in (-1, 1):
            put("pew", "chapel", cx + cw / 2.0 + side * 9.0, cy + 11.0 + row * 3.5)

    # The Library: shelves along the north wall, a lectern in the middle, more shelves in rows.
    lx, ly, lw, lh = room_by_id("library")[3]
    for k in range(6):
        put("shelf", "library", lx + 4.5 + k * 7.5, ly + 1.0, wall=True)
    for row in range(2):
        for k in range(3):
            put("shelf", "library", lx + 10.0 + k * 14.0, ly + 13.0 + row * 9.0)
    put("lectern", "library", lx + lw / 2.0, ly + 9.0)

    # The Armoury: racks of arms on the north wall and down the west.
    ax, ay, aw, ah = room_by_id("armoury")[3]
    for k in range(3):
        put("rack", "armoury", ax + 4.0 + k * 7.0, ay + 1.0, wall=True)
    for k in range(2):
        put("rack", "armoury", ax + 6.0 + k * 10.0, ay + 12.0)

    # The Kennels: empty kennels along the east wall, a broken chain.
    kx, ky, kw, kh = room_by_id("kennels")[3]
    for k in range(5):
        put("kennel", "kennels", kx + kw - 3.0, ky + 4.0 + k * 7.0)
    put("straw", "kennels", kx + 8.0, ky + 12.0)
    put("straw", "kennels", kx + 11.0, ky + 27.0)

    # The Map Room: the table map of the house, in the middle.
    mx, my, mw, mh = room_by_id("map_room")[3]
    put("map_table", "map_room", mx + mw / 2.0, my + mh / 2.0)

    # The Treasury: coffers along the walls, the strongbox in the middle.
    tx, ty, tw, th = room_by_id("treasury")[3]
    for k in range(4):
        put("coffer", "treasury", tx + 5.0 + k * 8.5, ty + 1.0, wall=True)
    put("strongbox", "treasury", tx + tw / 2.0, ty + th / 2.0)

    # The Gallery: portraits on its north wall's face - one for each wizard he took (CourtRooms
    # assigns them), the rest empty frames.
    px, py, pw, ph = room_by_id("gallery")[3]
    for k in range(9):
        put("portrait", "gallery", px + 4.5 + k * 6.0, py - 0.5, slot=k, wall=True)

    # The Minstrels' Gallery: a harp and music stands on the balcony.
    sx, sy, sw, sh = room_by_id("minstrels_gallery")[3]
    put("harp", "minstrels_gallery", sx + 10.0, sy + 4.0)
    put("music_stand", "minstrels_gallery", sx + 15.0, sy + 5.0)
    put("music_stand", "minstrels_gallery", sx + 19.0, sy + 5.0)
    put("chest_spot", "minstrels_gallery", sx + 28.0, sy + 5.0)

    # The Solar: his bed and his desk.
    ox, oy, ow, oh = room_by_id("solar")[3]
    put("bed", "solar", ox + 8.0, oy + 6.0)
    put("desk", "solar", ox + ow - 10.0, oy + 8.0)
    put("chest_spot", "solar", ox + ow / 2.0, oy + oh / 2.0)

    # The Kitchens: ovens against the north wall with their vents in front, the larder to the east.
    kix, kiy, kiw, kih = room_by_id("kitchens")[3]
    for k in range(3):
        put("oven", "kitchens", kix + 8.0 + k * 13.0, kiy + 1.0, wall=True)
        put("vent", "kitchens", kix + 8.0 + k * 13.0, kiy + 4.5)
    put("larder", "kitchens", kix + kiw - 8.0, kiy + kih - 4.0)
    put("flask_spot", "kitchens", kix + kiw - 11.0, kiy + kih - 3.0)
    put("flask_spot", "kitchens", kix + kiw - 5.0, kiy + kih - 3.0)
    put("work_table", "kitchens", kix + 18.0, kiy + 14.0)
    put("stair", "kitchens", 186.0, kiy + 1.5, facing="n")
    put("stair", "minstrels_gallery", 186.0, sy + 8.5, facing="s")

    # The Well Court: the well, open to the sky.
    wx, wy, ww, wh = room_by_id("well_court")[3]
    put("well", "well_court", wx + ww / 2.0, wy + wh / 2.0)

    # The stair from the cells, at the start.
    stx, sty, stw, sth = room_by_id("start")[3]
    put("stair", "start", stx + stw / 2.0, sty + sth - 1.5, facing="s")

    # The throne room doors.
    dx, dy, dw, dh = THRONE_DOORS
    put("throne_doors", "throne_doors", dx + dw / 2.0, dy + dh * 1.0, wall=True)

    # The Keepsake Doors: a door in each alcove's mouth, and in each alcove a plinth for the keepsake.
    for k in range(ALCOVES):
        x = ALCOVE_X0 + k * ALCOVE_PITCH
        put("keepsake_door", "keepsake_doors", x + ALCOVE_W / 2.0, ALCOVE_Y + ALCOVE_DEPTH + 2.0, index=k, wall=True)
        put("reliquary", "keepsake_doors", x + ALCOVE_W / 2.0, ALCOVE_Y + 2.0, index=k)

    # A few hall furnishings, generated: a bench or an urn now and then against a hall wall. Never in
    # a junction, a door's mouth or a runner.
    return out


# ------------------------------------------------------------------------------------------------
# The self-check (9.3).

def check(g, start, plaques, signs):
    failures = []
    hidden = hidden_cells()
    reach = flood(g, start, blocked=hidden)
    reach_all = flood(g, start)

    for room in ROOMS:
        rid, name, kind, r = room[0], room[1], room[2], room[3]
        c = (int(centre(r)[0]), int(centre(r)[1]))
        if room[6]:
            if c not in reach_all:
                failures.append("hidden room %s cannot be reached even through its hidden way" % rid)
            if c in reach:
                failures.append("hidden room %s can be reached WITHOUT its hidden way: it is not hidden" % rid)
        elif c not in reach:
            failures.append("room %s cannot be reached from the start without a hidden way" % rid)

    dx, dy, dw, dh = THRONE_DOORS
    front = (dx + dw // 2, dy + dh)
    if front not in reach:
        failures.append("the throne room doors (%d, %d) cannot be reached from the start" % front)

    ids = set(r[0] for r in ROOMS)
    glyph_of = dict((r[8], r[0]) for r in ROOMS)
    for p in plaques:
        if p["room"] not in ids or glyph_of.get(p["glyph"]) != p["room"]:
            failures.append("a door glyph %r names no real room (%r)" % (p["glyph"], p["room"]))
    for s in signs:
        for gl, rid in zip(s["glyphs"], s["rooms"]):
            if rid not in ids or glyph_of.get(gl) != rid:
                failures.append("a junction sign glyph %r names no real room (%r)" % (gl, rid))
    for rid, _, _ in DOORS:
        if rid not in ids:
            failures.append("a door opens onto %r, which is not a room" % rid)
    if len(set(r[8] for r in ROOMS)) != len(ROOMS):
        failures.append("two rooms share a glyph")

    # Walls at least two thick: no wall cell with walkable floor on both sides of it.
    thin = 0
    for y in range(H):
        for x in range(W):
            if g.get(x, y) != WALL:
                continue
            if (g.walkable(x - 1, y) and g.walkable(x + 1, y)) or (g.walkable(x, y - 1) and g.walkable(x, y + 1)):
                thin += 1
    if thin:
        failures.append("%d wall cells are one tile thick" % thin)

    # Every walkable cell belongs to the house: nothing cut off from the start, even with every hidden
    # way open (a sealed pocket is somewhere nothing can ever be placed or reach).
    pockets = sum(1 for y in range(H) for x in range(W) if g.walkable(x, y) and (x, y) not in reach_all)
    if pockets:
        failures.append("%d walkable cells are cut off from the start" % pockets)

    # The building fits its 6 x 6 chunks with wall all round it.
    for x in range(W):
        if g.walkable(x, 0) or g.walkable(x, H - 1):
            failures.append("floor on the building's north or south edge")
            break
    for y in range(H):
        if g.walkable(0, y) or g.walkable(W - 1, y):
            failures.append("floor on the building's east or west edge")
            break

    counted = [r for r in ROOMS if r[7]]
    if len(counted) != 13:
        failures.append("%d counted rooms, not thirteen" % len(counted))
    return failures, reach


# ------------------------------------------------------------------------------------------------

def write_json(g, start, plaques, signs, items):
    rows = ["".join(g.c[y]) for y in range(H)]
    doc = {
        "_comment": "Generated by tools/art/court_layout.py - the Court, one fixed building (.ai/endgame-castle.md 5). "
                    "Read by LevelGenerator.Authored.cs (the grid) and CourtLayout.cs (the rest). Cells are (x east, y south) "
                    "from the north-west corner; `origin` is the cell that is world tile (0, 0), where the run starts.",
        "format": 1,
        "name": "court",
        "width": W,
        "height": H,
        "origin": [start[0], start[1]],
        "outside": WALL,
        "legend": LEGEND,
        "rows": rows,
        "rooms": [
            {"id": r[0], "name": r[1], "kind": r[2], "rect": list(r[3]), "wing": r[4] or "",
             "hidden": r[6], "counted": r[7], "glyph": r[8]}
            for r in ROOMS
        ],
        "halls": [{"id": h[0], "rect": list(h[1]), "wing": h[2], "axis": h[3]} for h in HALLS],
        "doors": [{"room": d[0], "rect": list(d[1]), "onto": d[2]} for d in DOORS],
        "passages": [{"id": p[0], "rect": list(p[1])} for p in PASSAGES],
        "hidden": [{"room": h[0], "rect": list(h[1]), "onto": h[2]} for h in HIDDEN],
        "alcoves": [{"index": k, "rect": [ALCOVE_X0 + k * ALCOVE_PITCH, ALCOVE_Y, ALCOVE_W, ALCOVE_DEPTH],
                     "mouth": [ALCOVE_X0 + k * ALCOVE_PITCH, ALCOVE_Y + ALCOVE_DEPTH, ALCOVE_W, 2]}
                    for k in range(ALCOVES)],
        "throneDoors": list(THRONE_DOORS),
        "plaques": plaques,
        "signs": signs,
        "furniture": items,
    }
    text = json.dumps(doc, indent=1, sort_keys=True)
    # One row of the grid per line, which is what a diff of the building should show.
    text = text if isinstance(text, type(u"")) else text.decode("utf-8")
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    io.open(OUT, "w", encoding="utf-8", newline="\n").write(text + u"\n")


PREVIEW_COLOURS = {
    WALL: (12, 15, 24),
    FLOOR: (70, 78, 96),
    "m": (122, 126, 140),
    "b": (96, 74, 38),
    "c": (78, 96, 90),
    "R": (150, 46, 56),
    "U": (58, 80, 170),
    "G": (54, 120, 110),
    "Y": (196, 156, 60),
}


def write_preview(g, start, plaques, signs, items):
    from PIL import Image, ImageDraw, ImageFont
    s = 5
    img = Image.new("RGB", (W * s, H * s), PREVIEW_COLOURS[WALL])
    d = ImageDraw.Draw(img)
    for y in range(H):
        for x in range(W):
            col = PREVIEW_COLOURS.get(g.get(x, y), (255, 0, 255))
            if col != PREVIEW_COLOURS[WALL]:
                d.rectangle([x * s, y * s, x * s + s - 1, y * s + s - 1], fill=col)
    for _, r, _ in HIDDEN:
        x, y, w, h = r
        d.rectangle([x * s, y * s, (x + w) * s - 1, (y + h) * s - 1], fill=(170, 60, 170))
    for p in plaques:
        x, y = p["at"]
        d.rectangle([int(x * s) - 2, int(y * s) - 2, int(x * s) + 2, int(y * s) + 2], fill=(240, 220, 120))
    for sg in signs:
        x, y = sg["at"]
        d.rectangle([int(x * s) - 4, int(y * s) - 2, int(x * s) + 4, int(y * s) + 2], fill=(120, 230, 230))
    for it in items:
        x, y = it["at"]
        col = (230, 150, 90) if it["kind"] == "platter" else (200, 200, 200)
        d.rectangle([int(x * s) - 1, int(y * s) - 1, int(x * s) + 1, int(y * s) + 1], fill=col)
    font = ImageFont.load_default()
    for room in ROOMS:
        x, y, w, h = room[3]
        d.rectangle([x * s, y * s, (x + w) * s - 1, (y + h) * s - 1], outline=(230, 230, 230))
        label = room[1].replace("The ", "") + (" (hidden)" if room[6] else "")
        d.text((x * s + 3, y * s + 3), label, fill=(255, 255, 255), font=font)
    sx, sy = start
    d.ellipse([sx * s - 6, sy * s - 6, sx * s + 6, sy * s + 6], outline=(255, 255, 255))
    img.save(PREVIEW)


def main():
    rng = random.Random(SEED)
    g = build_grid()
    lay_floors(g)
    sr = room_by_id("start")[3]
    start = (sr[0] + sr[2] // 2, sr[1] + sr[3] // 2)
    plaques = door_plaques(g)
    signs = junction_signs(g)
    items = furniture(rng)
    failures, reach = check(g, start, plaques, signs)

    walk = sum(1 for y in range(H) for x in range(W) if g.walkable(x, y))
    print("  court: %dx%d cells, %d walkable (%.0f%%), %d rooms (%d counted), %d doors, %d hidden ways"
          % (W, H, walk, 100.0 * walk / (W * H), len(ROOMS), sum(1 for r in ROOMS if r[7]), len(DOORS), len(HIDDEN)))
    print("  %d door glyphs, %d junction signs, %d pieces of furniture" % (len(plaques), len(signs), len(items)))
    write_preview(g, start, plaques, signs, items)
    print("  map: tools/art/_court_layout_map.png")
    if failures:
        print("THE COURT FAILS ITS OWN CHECK; court_layout.json NOT written:")
        for f in failures:
            print("   - " + f)
        return 1
    print("  checked: every room reachable from the start without a hidden way; the Solar only with one; "
          "the throne room doors reachable; every glyph names a real room; walls two thick; no pockets")
    write_json(g, start, plaques, signs, items)
    print("wrote assets/bonelight/layouts/court_layout.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())
