# -*- coding: utf-8 -*-
"""Style study: what "darker, more evil, more detail density" actually means in pixels.

Run: python tools/art/dense.py     -> tools/art/_dense/ (gitignored)

Two sprites, one of each problem, drawn to be argued with rather than shipped.

THE MEASUREMENT THIS IS BUILT AGAINST. Detail density is not a feeling and it is not the colour
count. The useful number is INTERNAL EDGES PER FILLED PIXEL: how often the eye is handed a
boundary inside the figure rather than at its outline. Measured across the existing art:

    Bonelight enemies          0.74 - 1.00
    The test wizard Eric drew  0.83, on seven colours
    My four player studies     0.46 - 0.55

So the studies are about half as dense as everything around them, and the test wizard beats all
four of them using less than half the palette. Density does not come from more colours. It comes
from more internal shapes. 0.8 is the floor here.

WHY THE STUDIES CAME OUT FLAT, since it was not carelessness. `.ai/art-direction.md` is
silhouette-first, and I read that as licence to let interiors be one flat tone with shading only
on the two edge columns of each row. That produces a clean outline around a dead middle. The
contract wants the silhouette to carry the READ; it never asked for the inside to be empty.

WHY THE ENEMIES DO NOT LOOK EVIL, which is a separate and simpler problem. Their body tones come
from the wrong half of their ramps:

    swarmer   linen.2  #46544C   35% of the figure
    runner    linen.2  #46544C   33%
    bruiser   steel.1  #B8C8DC   the second LIGHTEST steel
    shielder  steel.0  #DCE8F4   the lightest steel there is

A shielder in near-white plate cannot read as a thing the dark wizard made. `.ai/world-and-tone.md`
already says body rows come from the DEEP half of the ramp - the art simply predates the rule.

The four density techniques used below, in order of how much they buy:

1. SEPARATE EVERY OVERLAPPING FORM WITH OCC. One dark pixel between a pauldron and the arm under
   it is worth more than five tones inside either.
2. CARRY FOUR OR FIVE STOPS INSIDE THE MASS, not two at the edges. A fold is a light stop and a
   dark stop next to each other in the middle of a shape.
3. ASYMMETRIC FURNITURE. A strap on one side, a torn hem on one corner, one broken rib. Symmetry
   halves the information for free.
4. ONE HOT PIXEL. Against a body pushed into the deep stops, a single emissive pixel does more
   work than any amount of mid-tone.
"""
import os

import bonelight as B
import pixel as P

M = B.MATERIALS
E = B.ELEMENTS
OCC = B.OCC
RIM = B.RIM


def palette():
    return B.build_palette({
        "o": OCC, "W": RIM,
        # steel, all five stops, because a dense figure needs the whole ramp inside it
        "T": ("steel", "hi"), "t": ("steel", "lit"), "r": ("steel", "base"),
        "R": ("steel", "shade"), "q": ("steel", "deep"),
        # linen for wrap and tatters
        "L": ("linen", "lit"), "l": ("linen", "base"), "e": ("linen", "shade"),
        "E": ("linen", "deep"),
        # wool for the wizard robe
        "d": ("wool", "lit"), "c": ("wool", "base"), "b": ("wool", "shade"),
        "a": ("wool", "deep"),
        # bone
        "S": ("skin", "hi"), "s": ("skin", "lit"), "k": ("skin", "base"), "n": ("skin", "shade"),
        # gold
        "H": ("gold", "hi"), "G": ("gold", "lit"), "g": ("gold", "base"), "u": ("gold", "shade"),
        # violet, for the shadow the dark wizard leaves in things he made
        "v": ("violet", "base"), "V": ("violet", "lit"),
        # fire, the Pyromancer's element and the mark in a made thing's eye
        "1": E["fire"][0], "2": E["fire"][1], "3": E["fire"][2], "4": E["fire"][3],
        # arcane, the eye of a taken thing
        "Z": E["arcane"][0], "Y": E["arcane"][1], "y": E["arcane"][2],
    })


class G(object):
    def __init__(self, cell):
        self.cell = cell
        self.g = [["."] * cell for _ in range(cell)]

    def set(self, x, y, ch):
        if 0 <= x < self.cell and 0 <= y < self.cell and ch != ".":
            self.g[int(y)][int(x)] = ch

    def span(self, y, x0, x1, ch):
        for x in range(int(round(x0)), int(round(x1)) + 1):
            self.set(x, y, ch)

    def rect(self, x0, y0, x1, y1, ch):
        for y in range(int(y0), int(y1) + 1):
            self.span(y, x0, x1, ch)

    def line(self, x0, y0, x1, y1, ch):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        if n == 0:
            self.set(x0, y0, ch)
            return
        for i in range(n + 1):
            t = i / float(n)
            self.set(round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t), ch)

    def get(self, x, y):
        if 0 <= x < self.cell and 0 <= y < self.cell:
            return self.g[int(y)][int(x)]
        return "."

    def rows(self):
        return ["".join(r) for r in self.g]


def seam(g, x0, y0, x1, y1, ch="o"):
    """Technique 1: a dark line where two forms overlap. The cheapest density there is."""
    g.line(x0, y0, x1, y1, ch)


def fold(g, y, x0, x1, lit, dark):
    """Technique 2: a light stop and a dark stop adjacent, in the MIDDLE of a mass."""
    g.set(x0, y, lit)
    g.set(x0 + 1, y, dark)
    g.span(y, x0 + 2, x1, ".") if False else None


# --------------------------------------------------------------------------------------------
# A. THE SHIELDER, redrawn dark and dense. 32x32.
# --------------------------------------------------------------------------------------------

def shielder_dark():
    """Dark, hunched, horned. 32x32.

    THE FIRST ATTEMPT SCORED 0.90 AND WAS STILL WRONG, which is the most useful thing in this
    file. It banded the whole torso with evenly spaced plates, and evenly spaced anything at
    32px is a stripe pattern: it read as a radiator. Density counted by the metric, and the
    metric does not know that repetition carries no information. The rule that survives is
    technique 3 - the furniture has to be IRREGULAR to be worth its pixels.

    What makes a 32px figure read as evil, in the order it matters:

    1. POSTURE. Upright is a soldier; hunched with the head forward is a thing. Costs nothing,
       does more than any amount of shading.
    2. A BROKEN OUTLINE AT THE TOP. Horns, a crest, a torn hood - something that stops the
       silhouette resolving into a neat shape.
    3. THE BODY IN THE DEEP HALF OF ITS RAMP, so the one hot pixel in the face is the only
       thing that emits. The old shielder wore steel.0 #DCE8F4, the lightest steel there is.
    """
    g = G(32)
    # Hunched: the mass leans forward, and the shoulder line is above the head rather than
    # level with it. Every x below is derived from this lean.
    def lean(y):
        t = max(0.0, (y - 9) / 19.0)
        return 15.5 + 1.8 * t

    # torso, deep steel, widening to a heavy base
    for y in range(11, 28):
        t = (y - 11) / 16.0
        hw = 4.2 + 3.2 * t
        g.span(y, lean(y) - hw, lean(y) + hw, "R")
        lo, hi = int(round(lean(y) - hw)), int(round(lean(y) + hw))
        g.set(lo, y, "r")
        g.set(lo - 1, y, "W")
        g.set(hi, y, "q")

    # ONE chest plate with a boss, not five bands. Irregular furniture below it.
    g.rect(12, 13, 19, 18, "r")
    g.line(12, 13, 19, 13, "t")
    g.line(12, 18, 19, 18, "q")
    g.set(15, 15, "q")
    g.set(16, 15, "G")                        # the boss, off centre
    g.set(16, 16, "u")
    seam(g, 11, 19, 20, 19)
    # a skirt of three plates, uneven widths and uneven gaps
    for (py, px0, px1) in ((21, 11, 17), (23, 13, 21), (26, 12, 19)):
        g.span(py, px0, px1, "r")
        g.span(py + 1, px0, px1, "q")
        g.set(px0, py, "t")
    # one strap, one side only
    g.line(11, 14, 20, 21, "q")
    g.line(11, 15, 20, 22, "E")

    # Helm: horned, and the head sits FORWARD of the shoulders.
    g.rect(11, 4, 19, 11, "R")
    g.line(11, 4, 19, 4, "r")
    g.set(11, 5, "t")
    g.line(10, 2, 11, 5, "q")                 # horns, different lengths - technique 3
    g.line(10, 2, 10, 3, "R")
    g.line(20, 1, 19, 5, "q")
    g.line(20, 1, 20, 3, "R")
    g.rect(12, 7, 18, 10, "o")                # the visor slit, a void
    g.set(13, 8, "y")
    g.set(14, 8, "Y")                         # arcane: a TAKEN thing, not a built one
    g.set(17, 8, "y")
    g.line(15, 7, 15, 10, "q")
    seam(g, 11, 11, 19, 11)

    # Shield: narrow, tall, angled, with a spike - a plane, not a grey rectangle.
    for i, y in enumerate(range(12, 27)):
        off = 3 - abs(i - 7) * 0.30
        g.span(y, 4.4 - off * 0.5, 8.4 + off * 0.28, "q")
        g.set(int(round(4.4 - off * 0.5)), y, "R")
    g.set(6, 18, "u")
    g.set(7, 18, "G")
    g.set(6, 19, "u")
    g.line(9, 19, 11, 19, "q")                # the spike
    g.set(12, 19, "R")
    seam(g, 9, 12, 9, 26)

    # Tattered linen, torn at one corner only.
    for ty, tx0, tx1 in ((24, 13, 22), (25, 14, 21), (26, 15, 20), (27, 17, 19)):
        g.span(ty, tx0, tx1, "e")
        g.set(tx0, ty, "l")
        g.set(tx1, ty, "E")
    g.set(23, 24, "E")

    g.span(29, 12, 21, "q")
    g.span(30, 11, 22, "o")                   # contact
    return g.rows()


# --------------------------------------------------------------------------------------------
# B. THE PYROMANCER, designed from Fireball. 48x48.
# --------------------------------------------------------------------------------------------

def pyromancer():
    g = G(48)
    CX = 23.5
    # Staff FIRST: a brazier head, not an orb. The weapon decides the silhouette, so it is the
    # thing drawn first and everything else is fitted around it.
    sx = 33
    for y in range(14, 47):
        g.span(y, sx, sx + 1, "g" if y > 20 else "G")
        g.set(sx + 2, y, "u")
        if y % 4 == 1:
            g.span(y, sx - 1, sx + 2, "u")    # binding rings - technique 3, irregular spacing
    # the brazier: a cage of iron with fire inside it
    g.rect(30, 8, 37, 13, "q")
    g.rect(31, 9, 36, 12, "4")
    g.rect(32, 10, 35, 11, "3")
    g.set(33, 10, "2")
    g.set(34, 10, "1")
    for cx in (31, 33, 35):
        g.line(cx, 8, cx, 13, "R")            # the bars, over the flame
    g.line(30, 8, 37, 8, "r")
    g.line(30, 13, 37, 13, "q")
    g.set(29, 9, "3")                          # embers escaping
    g.set(38, 11, "4")
    g.set(30, 6, "2")
    g.set(36, 5, "4")

    # Robe: wool base, but scorched toward the hem - four stops carried INSIDE the mass
    for y in range(20, 46):
        t = (y - 20) / 25.0
        hw = 3.6 + 4.2 * t
        g.span(y, CX - hw, CX + hw, "b")
    for y in range(20, 46):
        t = (y - 20) / 25.0
        hw = 3.6 + 4.2 * t
        lo, hi = int(round(CX - hw)), int(round(CX + hw))
        g.set(lo, y, "c")
        g.set(lo + 1, y, "c")
        g.set(hi, y, "a")
        g.set(lo - 1, y, "W")
        # technique 2: two vertical folds inside the robe, not at its edges
        g.set(int(CX - 1), y, "a" if (y % 5) else "c")
        g.set(int(CX + 2), y, "a")
    # scorch: the hem is burnt, which is the spell showing in the costume
    for y in range(40, 46):
        t = (y - 40) / 5.0
        hw = 3.6 + 4.2 * ((y - 20) / 25.0)
        g.span(y, CX - hw, CX - hw + 2 + 3 * t, "a")
        g.set(int(round(CX - hw)), y, "4" if y % 2 else "a")
    g.span(45, CX - 7.8, CX + 7.8, "a")
    g.span(44, CX - 7.0, CX - 3.0, "4")       # embers along the hem, asymmetric
    g.set(int(CX - 5), 45, "3")
    g.span(46, CX - 7.8, CX + 7.8, "o")       # contact

    # a belt of iron plates, each separated by occ
    for i, bx in enumerate(range(-5, 6, 2)):
        g.span(30, CX + bx, CX + bx + 1, "r" if i % 2 else "R")
        g.set(CX + bx + 2, 30, "o")
    g.span(31, CX - 5, CX + 6, "q")

    # Hood: deep, with the face a void and FIRE in it rather than arcane. He is not a taken
    # thing - he is the one carrying the light.
    for y in range(10, 22):
        t = (y - 10) / 11.0
        hw = 1.4 + 4.6 * t
        g.span(y, CX - hw - 0.6, CX + hw, "b")
        g.set(int(round(CX - hw - 0.6)), y, "c")
        g.set(int(round(CX + hw)), y, "a")
    g.span(17, CX - 5.2, CX + 5.0, "a")       # the brow, overhanging
    for y in range(18, 23):
        hw = 4.0 - 0.5 * (y - 18)
        g.span(y, CX - hw, CX + hw, "o")
    g.set(int(CX - 2), 20, "2")
    g.set(int(CX + 2), 20, "3")
    # beard, lit from below by the brazier - bone at the top, ember at the tip
    for y in range(23, 33):
        t = (y - 23) / 9.0
        hw = 3.2 - 2.4 * t
        lo, hi = int(round(CX - hw)), int(round(CX + hw))
        g.span(y, lo, hi, "k")
        g.set(lo, y, "s")
        g.set(hi, y, "n")
        if t < 0.7 and hi - lo >= 2:
            g.set(int(CX), y, "n")
        if t > 0.62:
            g.set(lo, y, "4")                  # the ember light creeping up the strands
    g.span(22, CX - 4.4, CX + 4.4, "k")
    g.set(int(CX), 22, "o")
    # the hand on the staff
    g.span(31, 28, 32, "b")
    g.span(32, 28, 32, "a")
    g.set(32, 31, "s")
    g.set(32, 32, "n")
    return g.rows()


STUDIES = [("shielder-dark", shielder_dark, 32), ("pyromancer", pyromancer, 48)]


def edges_per_px(rows):
    cell = len(rows)
    filled = [(x, y) for y in range(cell) for x in range(cell) if rows[y][x] != "."]
    n = 0
    for x, y in filled:
        for dx, dy in ((1, 0), (0, 1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < cell and 0 <= ny < cell and rows[ny][nx] != "." \
                    and rows[ny][nx] != rows[y][x]:
                n += 1
    return len(filled), n, n / float(max(1, len(filled)))


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_dense")
    if not os.path.isdir(out):
        os.makedirs(out)
    pal = palette()
    for name, fn, cell in STUDIES:
        rows = fn()
        filled, edges, ratio = edges_per_px(rows)
        P.write_strip([rows], pal, os.path.join(out, name + ".png"), cell)
        flag = "ok" if ratio >= 0.8 else "SPARSE"
        print("%-14s %dx%-2d  %3d px  %3d edges  %.2f edges/px  %s"
              % (name, cell, cell, filled, edges, ratio, flag))
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
