# -*- coding: utf-8 -*-
"""The player wizard: 32x32 cell, Bonelight, 4-frame idle.

Authored as separate pieces - head, robe, staff, orb - so the idle can bob the head and pulse
the orb without retyping the robe, and so no column offset is ever counted by hand.

Contract (.ai/art-direction.md): feet on row 30, occ contact on row 31, rim on the key-facing
(upper-left) edge only, no outline.
"""
import bonelight as B
import pixel as P

CELL = 32
STAFF_X = 27  # shaft centre column; the orb and hand are centred on it

PALETTE = B.build_palette({
    "o": B.OCC,
    "W": B.RIM,
    "D": ("wool", "hi"),    "d": ("wool", "lit"),   "c": ("wool", "base"),
    "b": ("wool", "shade"), "a": ("wool", "deep"),
    "S": ("skin", "lit"),   "s": ("skin", "base"),  "k": ("skin", "shade"),
    "H": ("gold", "hi"),    "G": ("gold", "lit"),   "g": ("gold", "base"),
    "Z": ("arcane", "hi"),  "Y": ("arcane", "lit"), "y": ("arcane", "base"),
})


def _row(marks):
    """One row from {column: char}, so no offset is ever counted by hand."""
    r = ["."] * CELL
    for x, ch in marks.items():
        if 0 <= x < CELL:
            r[x] = ch
    return "".join(r)


# Hat, brim and face. Bobs as one piece on the idle.
HEAD = [
    "", "",
    "..............oo................",
    ".............oWao...............",
    ".............oWcao..............",
    "............oWdccao.............",
    "............oWdccbao............",
    "...........oWdcccbao............",
    "...........oWdccccbao...........",
    "..........oWdcccccbao...........",
    "..........oWdccccccbao..........",
    ".........oWdcccccccbao..........",
    "........oHHHHHHHHHHHHGgo........",
    "........oggggggggggggggo........",
    "..........okkkkkkkkkko..........",
    "..........oSSkkSskksko..........",
    "..........oSSSssskkkko..........",
    "...........oSssskkkko...........",
    "............oSsskkko............",
]

# Robe. Never moves - the anchor lives here.
BODY = [
    "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "",
    "...........oWdccccbao...........",
    "..........oWdcccccbbao..........",
    "..........oWdcgcccbbao..........",
    ".........oWdccgccccbao..........",
    ".........oWdccgccccbao..........",
    "........oWdcccgcccccbao.........",
    "........oWdcccgcccccbao.........",
    ".......oWdccccgccccccbao........",
    ".......oWdccccgccccccbao........",
    "......oWdcccccgcccccccbao.......",
    "......oWdcccccgcccccccbao.......",
    ".....oHHHHHHHHHHHHHHHHHGgo......",
    ".....oggggggggggggggggggo.......",
    "......oooooooooooooooooo........",
]


def staff():
    """Shaft down to the contact row, with the sleeve cuff gripping it."""
    x = STAFF_X
    rows = []
    for y in range(CELL):
        if 17 <= y <= 30:
            rows.append(_row({x - 1: "o", x: "g", x + 1: "o"}))
        elif y == 31:
            rows.append(_row({x - 1: "o", x: "o", x + 1: "o"}))
        else:
            rows.append("." * CELL)
    # Sleeve reaches back to the robe edge (x=21) so the staff is held, not floating.
    rows[21] = _row({22: "o", 23: "d", 24: "c", 25: "S", 26: "s", x: "g", x + 1: "o"})
    rows[22] = _row({22: "o", 23: "c", 24: "b", 25: "s", 26: "k", x: "g", x + 1: "o"})
    return rows


def orb(bright):
    """Arcane light at the staff head. Pulses across the idle, and is the one emissive here."""
    core, ring = ("Z", "Y") if bright else ("Y", "y")
    x = STAFF_X
    rows = ["." * CELL] * CELL
    rows[12] = _row({x: "o"})
    rows[13] = _row({x - 1: "o", x: ring, x + 1: "o"})
    rows[14] = _row({x - 2: "o", x - 1: ring, x: core, x + 1: ring, x + 2: "o"})
    rows[15] = _row({x - 1: "o", x: ring, x + 1: "o"})
    rows[16] = _row({x: "o"})
    return rows


def frames():
    """Four-beat idle: the head lifts a pixel on the off-beats, the orb pulses across all four."""
    out = []
    for bob, bright in ((0, False), (-1, True), (0, True), (-1, False)):
        g = P.pad(BODY, CELL, CELL)
        g = P.overlay(g, P.shift(P.pad(HEAD, CELL, CELL), 0, bob, CELL, CELL), CELL, CELL)
        g = P.overlay(g, staff(), CELL, CELL)
        g = P.overlay(g, orb(bright), CELL, CELL)
        out.append(g)
    return out


ANIMATIONS = [("idle", frames)]
