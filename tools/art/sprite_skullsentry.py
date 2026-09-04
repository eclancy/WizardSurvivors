# -*- coding: utf-8 -*-
"""SkullSentry: 32x32 cell, Bonelight. A rooted floating turret that volleys fire bolts.

Silhouette class is Flyer (.ai/art-direction.md section 4): no ground contact, and the occ
shadow sits detached below the body. That gap is the class read - it is the only enemy whose
shadow does not touch it.

RangedEnemy drives Speed 0 / Rooted, a 0.95s wind-up then a 3-bolt volley, so "moving" is a
hover and "attack" is the wind-up flare rather than a lunge.
"""
import bonelight as B
import pixel as P

CELL = 32

PALETTE = B.build_palette({
    "o": B.OCC,
    "W": B.RIM,
    "S": ("skin", "hi"),   "s": ("skin", "lit"),  "n": ("skin", "base"),
    "k": ("skin", "shade"), "K": ("skin", "deep"),
    # fire ramp drives the sockets: core / hot / mid / edge
    "w": B.ELEMENTS["fire"][0], "X": B.ELEMENTS["fire"][1],
    "F": B.ELEMENTS["fire"][2], "f": B.ELEMENTS["fire"][3],
})

# Cranium, sockets hollow. The embers are overlaid separately so they can flare.
SKULL = [
    "", "", "", "", "", "", "", "",
    "..........oooooooo..............",
    ".........oWssnnnnko.............",
    "........oWsssnnnnnko............",
    "........oWssnnnnnnnko...........",
    ".......oWsssnnnnnnnnko..........",
    ".......oWssooonnooonko..........",
    ".......oWsoooooKoooooko.........",
    ".......oWsoooooKoooooko.........",
    ".......oWssoooonoooonko.........",
    "........oWsssnnKnnnnko..........",
    "........oWssnnoKonnnko..........",
    ".........oWssnnnnnnko...........",
    ".........oWsnkkkkknko...........",
    "..........oSoSoSoSoko...........",
    "..........oooooooooo............",
]


def embers(level):
    """Socket light. level 0 = banked, 1 = lit, 2 = flaring (wind-up), 3 = discharge."""
    inner, outer = {
        0: ("F", "f"),
        1: ("X", "F"),
        2: ("w", "X"),
        3: ("w", "w"),
    }[level]
    rows = ["." * CELL] * CELL
    for y in (14, 15):
        r = ["."] * CELL
        for x in (9, 10, 11):
            r[x] = outer if x != 10 else inner
        for x in (16, 17, 18):
            r[x] = outer if x != 17 else inner
        rows[y] = "".join(r)
    return rows


def shadow(tightness):
    """Detached ground shadow. Shrinks as the skull rises - the only cue it is airborne."""
    width = {0: 13, 1: 11, 2: 9}[tightness]
    half = width // 2
    rows = ["." * CELL] * CELL
    wide = ["."] * CELL
    for x in range(15 - half, 15 + half + 1):
        wide[x] = "o"
    narrow = ["."] * CELL
    for x in range(15 - half + 2, 15 + half - 1):
        narrow[x] = "o"
    rows[29] = "".join(narrow)
    rows[30] = "".join(wide)
    return rows


HOVER = [0, -1, -2, -2, -1, 0, 1, 1]


def moving():
    """Eight-frame hover. Body rises and falls; the shadow tightens as it lifts."""
    out = []
    for i, dy in enumerate(HOVER):
        tight = 2 if dy <= -2 else (1 if dy < 0 else 0)
        g = P.shift(P.pad(SKULL, CELL, CELL), 0, dy, CELL, CELL)
        g = P.overlay(g, P.shift(embers(0 if i % 4 else 1), 0, dy, CELL, CELL), CELL, CELL)
        g = P.overlay(g, shadow(tight), CELL, CELL)
        out.append(g)
    return out


def attack():
    """Wind-up: the sockets climb to white-hot, the skull sinks, then snaps back on release."""
    out = []
    for dy, level, tight in ((0, 1, 0), (1, 2, 0), (1, 2, 0), (2, 3, 0), (-1, 3, 1), (0, 2, 0)):
        g = P.shift(P.pad(SKULL, CELL, CELL), 0, dy, CELL, CELL)
        g = P.overlay(g, P.shift(embers(level), 0, dy, CELL, CELL), CELL, CELL)
        g = P.overlay(g, shadow(tight), CELL, CELL)
        out.append(g)
    return out


def hurt():
    """Two frames: knocked back and blanched, then settling."""
    out = []
    for dx, wash in ((2, True), (1, False)):
        g = P.pad(SKULL, CELL, CELL)
        if wash:
            g = [r.replace("n", "S").replace("k", "s") for r in g]
        g = P.shift(g, dx, 0, CELL, CELL)
        g = P.overlay(g, P.shift(embers(2), dx, 0, CELL, CELL), CELL, CELL)
        g = P.overlay(g, shadow(0), CELL, CELL)
        out.append(g)
    return out


def death():
    """Six frames: the light goes out, the jaw drops, the cranium splits and falls."""
    out = []
    # 0-1: embers gutter and die
    for level in (1, 0):
        g = P.pad(SKULL, CELL, CELL)
        g = P.overlay(g, embers(level), CELL, CELL)
        g = P.overlay(g, shadow(0), CELL, CELL)
        out.append(g)
    # 2-3: falls, sockets dark
    for dy in (2, 4):
        g = P.shift(P.pad(SKULL, CELL, CELL), 0, dy, CELL, CELL)
        out.append(P.overlay(g, shadow(0), CELL, CELL))
    # 4-5: splits apart and thins to a scatter of bone
    crack = [r.replace("n", ".").replace("s", "n") for r in P.pad(SKULL, CELL, CELL)]
    out.append(P.overlay(P.shift(crack, 0, 5, CELL, CELL), shadow(0), CELL, CELL))
    dust = [r.replace("n", ".").replace("S", ".").replace("W", ".") for r in crack]
    out.append(P.overlay(P.shift(dust, 0, 6, CELL, CELL), shadow(0), CELL, CELL))
    return out


ANIMATIONS = [("moving", moving), ("attack", attack), ("hurt", hurt), ("death", death)]
