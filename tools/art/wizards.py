# -*- coding: utf-8 -*-
"""Wizard silhouette options for the player sprite. Exploration, not a shipping generator.

Run: python tools/art/wizards.py     -> tools/art/_wizards/ (gitignored)

The sprite this replaces was two stacked triangles - a cone hat on a cone robe - with a blank
grey oval for a face and a bare stick held at arm's length. Nothing in it was doing the one job
a 32px actor has, which is to be recognised by outline alone in a crowd of thirty.

Four things every option below does that the old one did not, because they are what the title
screen learned the hard way:

1. THE FACE IS DARK. A lit oval under a hat brim reads as a bald head; a void with two points
   of light in it reads as a face you cannot see, which is the whole idea. The brim is a light
   blocker, so the pixels under it are `occ` and the eyes are the element ramp.
2. THE BEARD IS THE SECOND SILHOUETTE. Below the shadow it is the only bone-white mass on the
   figure, so it carries the read at distance. It needs internal structure - a moustache that
   overhangs, a dark split, strands that taper - or it is a bib.
3. THE SIGNATURE SHAPE IS WIDER THAN THE BODY. Whatever sits on his head has to break the
   body's outline, or the whole figure is one taper and there is nothing to recognise.
4. THE HEM IS NOT A FLAT BAR. A straight line across the bottom reads as a shelf he is standing
   on rather than as cloth.

DRAW ORDER IS FIXED AND MATTERS: hat, then robe, then the face void, then the beard, then the
staff, and the eyes LAST. The first pass of this file drew the eyes before the robe and the
void was re-stamped over them afterwards, so all four options came out blind and the single
most important feature of the design was invisible.

All four are drawn from scratch. Format density - a small figure carrying one oversized
signature, read against a dark ground - is the reference; no sprite, character or asset from
another game is reproduced here.
"""
import os

import bonelight as B
import pixel as P

CELL = 32

PALETTE = B.build_palette({
    "o": B.OCC,
    "W": B.RIM,
    "D": ("wool", "hi"),    "d": ("wool", "lit"),   "c": ("wool", "base"),
    "b": ("wool", "shade"), "a": ("wool", "deep"),
    "S": ("skin", "hi"),    "s": ("skin", "lit"),   "k": ("skin", "base"),
    "n": ("skin", "shade"), "m": ("skin", "deep"),
    "H": ("gold", "hi"),    "G": ("gold", "lit"),   "g": ("gold", "base"),
    "u": ("gold", "shade"),
    "T": ("steel", "lit"),  "t": ("steel", "base"), "r": ("steel", "shade"),
    "Z": ("arcane", "hi"),  "Y": ("arcane", "lit"), "y": ("arcane", "base"),
})


class Grid(object):
    """A character canvas. Every option is composed from spans, never typed as literal rows.

    Typing 32-character strings by hand is how the old sprite ended up symmetric in the boring
    way: it is easy to centre a shape and hard to give it a lean, a droop or a sweep.
    """

    def __init__(self, w=CELL, h=CELL):
        self.w, self.h = w, h
        self.g = [["."] * w for _ in range(h)]

    def set(self, x, y, ch):
        if 0 <= x < self.w and 0 <= y < self.h and ch != ".":
            self.g[y][x] = ch

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.g[y][x]
        return "."

    def span(self, y, x0, x1, ch):
        for x in range(int(x0), int(x1) + 1):
            self.set(x, y, ch)

    def rect(self, x0, y0, x1, y1, ch):
        for y in range(int(y0), int(y1) + 1):
            self.span(y, x0, x1, ch)

    def line(self, x0, y0, x1, y1, ch):
        steps = max(abs(x1 - x0), abs(y1 - y0))
        if steps == 0:
            self.set(x0, y0, ch)
            return
        for i in range(steps + 1):
            t = i / float(steps)
            self.set(int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t)), ch)

    def disc(self, cx, cy, r, ch):
        for y in range(int(cy - r), int(cy + r) + 1):
            for x in range(int(cx - r), int(cx + r) + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.2:
                    self.set(x, y, ch)

    def rows(self):
        return ["".join(r) for r in self.g]


def _taper(g, y0, y1, top, bot, ch, lean=0.0, cx0=15.5):
    """A body: half-width walks from `top` to `bot` down the rows, with an optional lean."""
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = top + (bot - top) * t
        cx = cx0 + lean * t * t
        g.span(y, round(cx - hw), round(cx + hw), ch)


def _shade(g, y0, y1, ch_lit, ch_mid, ch_dark, rim=True):
    """Light the mass that is already down: key from the upper left, one tone per band.

    Walks each row's own extent rather than a fixed box, so a swept hem or a leaning crown gets
    shaded along its actual edge instead of along a rectangle that no longer fits it.
    """
    for y in range(y0, y1 + 1):
        xs = [x for x in range(g.w) if g.g[y][x] == ch_mid]
        if not xs:
            continue
        lo, hi = min(xs), max(xs)
        width = hi - lo
        g.set(lo, y, ch_lit)
        if width >= 3:
            g.set(lo + 1, y, ch_lit)
        if width >= 2:
            g.set(hi, y, ch_dark)
        if width >= 5:
            g.set(hi - 1, y, ch_dark)
        if rim:
            g.set(lo - 1, y, "W")


def _void(g, y0, y1, hw0, hw1, cx=15.5):
    """The shadow under the brim. Tapered, because a rectangle of black reads as a letterbox.

    Returns the (left, right) extent of the widest row so the eyes can be placed against the
    actual cavity rather than against a number typed twice.
    """
    extent = None
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        lo, hi = int(round(cx - hw)), int(round(cx + hw))
        g.span(y, lo, hi, "o")
        if extent is None:
            extent = (lo, hi)
    return extent


def _beard(g, cx, y0, y1, hw0, hw1, tache=True, sweep=0.0):
    """A beard with a moustache over it, a dark split down it, and strands that taper.

    Flat fill is what made the first pass read as a grey bib. Three things fix it and all three
    are cheap: the moustache is one row WIDER than the beard under it and separated from it by
    an occ line, so it overhangs; a column of shade runs down the middle, so the mass has two
    halves instead of one; and the lit tone is confined to the key-facing side.
    """
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        c = cx + sweep * t
        lo, hi = int(round(c - hw)), int(round(c + hw))
        g.span(y, lo, hi, "k")
        g.set(lo, y, "s")                       # key-facing strand
        if hi - lo >= 3:
            g.set(lo + 1, y, "s")
            g.set(hi, y, "n")
        if hi - lo >= 5:
            g.set(hi - 1, y, "n")
        # the split: one column of shade, kept off the tip so the point stays solid
        if t < 0.78 and hi - lo >= 3:
            g.set(int(round(c)), y, "n")
    if tache:
        w = hw0 + 1.4
        lo, hi = int(round(cx - w)), int(round(cx + w))
        g.span(y0 - 1, lo, hi, "k")
        g.set(lo, y0 - 1, "S")
        g.set(lo + 1, y0 - 1, "s")
        g.set(hi, y0 - 1, "n")
        g.set(int(round(cx)), y0 - 1, "o")      # the philtrum, and the overhang line
        g.set(int(round(cx)), y0, "o")


def _contact(g, x0, x1, y=31):
    """The occ line the figure stands on. Contract section 5: feet row 30, contact row 31."""
    g.span(y, x0, x1, "o")


def _eyes(g, y, x_left, x_right):
    """Last thing drawn, always. Two points of arcane light in the void, uneven by one tone so
    the face has a key side like everything else."""
    g.set(x_left, y, "Z")
    g.set(x_right, y, "Y")


def _orb(g, cx, cy, r=2):
    """The staff light. Big enough to be a light source, not a sparkle on a stick."""
    g.disc(cx, cy, r + 1, "y")
    g.disc(cx, cy, r, "Y")
    g.disc(cx, cy, r - 1, "Z")


# --------------------------------------------------------------------------------------------
# A. CONCLAVE - the brim is the whole silhouette
# --------------------------------------------------------------------------------------------

def conclave():
    g = Grid()
    # Crown: a cone with a kink two rows below the point and a lean to the left. A straight
    # cone is a traffic cone; the kink is the difference between a hat and a shape.
    g.set(18, 1, "c")
    g.set(18, 2, "c")
    for y in range(3, 11):
        t = (y - 3) / 7.0
        hw = 1.0 + 5.0 * t
        cx = 18 - 3.2 * t
        g.span(y, round(cx - hw), round(cx + hw), "c")
    _shade(g, 1, 10, "d", "c", "b")
    # Robe first, so the brim and the face sit on top of the shoulders.
    _taper(g, 16, 30, 4.5, 9.5, "c")
    _shade(g, 16, 30, "d", "c", "b")
    # Brim: three rows, the outer ones dropping at the ends, so it droops rather than sticking
    # out like a shelf. It is 24 wide against a 19-wide hem, which is the whole silhouette.
    g.span(11, 5, 26, "b")
    g.span(12, 3, 28, "a")
    g.span(11, 6, 13, "c")
    g.set(2, 12, "a")
    g.set(29, 12, "a")
    g.set(5, 11, "W")
    g.set(3, 12, "W")
    _void(g, 13, 17, 4.5, 3.0)
    _beard(g, 15.5, 18, 23, 3.6, 0.8)
    # Hem: stepped, with a gold band that stops short of the edges.
    g.span(30, 6, 25, "a")
    g.span(29, 6, 10, "b")
    g.span(29, 21, 25, "b")
    g.span(30, 11, 20, "g")
    _contact(g, 6, 25)
    # Staff, angled across the body, gripped by a sleeve that reaches it.
    g.line(25, 8, 22, 29, "g")
    g.line(26, 8, 23, 29, "u")
    g.span(20, 21, 24, "c")
    g.span(21, 21, 23, "b")
    g.set(24, 20, "s")
    g.set(24, 21, "n")
    _orb(g, 25, 6, 2)
    _eyes(g, 15, 13, 18)
    return g.rows()


# --------------------------------------------------------------------------------------------
# B. VIGIL - hooded, no hat. The title screen figure brought down to 32px.
# --------------------------------------------------------------------------------------------

def vigil():
    g = Grid()
    # Hood: a peak that falls straight into the shoulders, so head and body are one unbroken
    # mass and the only hole in it is the face.
    g.set(17, 2, "c")
    for y in range(3, 17):
        t = (y - 3) / 13.0
        hw = 1.2 + 6.0 * t
        cx = 17 - 1.8 * t
        g.span(y, round(cx - hw), round(cx + hw), "c")
    # Cloak, swept right so the outline is not a mirror of itself.
    _taper(g, 15, 30, 5.5, 9.0, "c", lean=1.8)
    _shade(g, 2, 30, "d", "c", "b")
    # The brow overhangs the cavity by a row - that is what makes it a hood and not a hole.
    g.span(9, 11, 20, "b")
    _void(g, 10, 15, 4.2, 3.2, cx=15.0)
    _beard(g, 15.0, 16, 24, 4.2, 0.8)
    g.span(30, 7, 26, "a")
    g.span(29, 22, 26, "b")
    g.span(28, 24, 26, "b")
    g.span(29, 7, 10, "b")
    _contact(g, 7, 26)
    # Staff on the left, tall, orb above the head - the one option lit from overhead.
    g.line(6, 4, 8, 30, "g")
    g.line(7, 4, 9, 30, "u")
    g.span(20, 9, 11, "c")
    g.span(21, 9, 10, "b")
    g.set(8, 20, "s")
    _orb(g, 6, 4, 2)
    _eyes(g, 12, 13, 17)
    return g.rows()


# --------------------------------------------------------------------------------------------
# C. ARCHMAGUS - the heaviest read. Crowned, pauldroned, beard to the hem.
# --------------------------------------------------------------------------------------------

def archmagus():
    g = Grid()
    # A stepped crown rather than a cone: three tiers with the point broken off and a finial.
    g.span(3, 14, 18, "b")
    g.rect(13, 4, 19, 5, "c")
    g.rect(12, 6, 20, 8, "c")
    g.rect(11, 9, 21, 10, "c")
    _shade(g, 3, 10, "d", "c", "b")
    g.span(2, 15, 17, "g")
    g.set(16, 1, "H")
    # Robe and pauldrons before the brim, so the brim overhangs both.
    _taper(g, 15, 30, 6.0, 9.5, "c")
    _shade(g, 15, 30, "d", "c", "b")
    # Pauldrons in dark wool, not steel: at 32px a steel plate sits at the same value as the
    # beard, and the two merged into one grey band straight across the chest. The gold rivet
    # is the only bright pixel on them, which is enough to say "armoured".
    g.span(16, 6, 10, "b")
    g.span(17, 6, 11, "b")
    g.span(18, 7, 11, "a")
    g.span(16, 21, 25, "a")
    g.span(17, 20, 25, "a")
    g.span(18, 20, 24, "a")
    g.set(6, 16, "d")
    g.set(7, 16, "G")
    g.set(24, 17, "u")
    g.set(5, 16, "W")
    # Brim: gold band over a wide dark lip.
    g.span(11, 8, 24, "G")
    g.span(12, 7, 25, "u")
    g.set(8, 11, "H")
    g.set(7, 12, "W")
    _void(g, 13, 16, 4.0, 3.4)
    # The beard runs almost to the hem. The length is the age, and it is the one option where
    # the bone mass outweighs the robe.
    _beard(g, 15.5, 17, 27, 4.2, 1.0)
    g.span(30, 6, 25, "a")
    g.span(29, 6, 9, "b")
    g.span(29, 22, 25, "b")
    g.span(29, 11, 20, "g")
    _contact(g, 6, 25)
    # Staff held out, orb level with his face: the light source is at head height.
    g.line(27, 9, 27, 30, "g")
    g.line(28, 9, 28, 30, "u")
    g.span(19, 25, 27, "r")
    g.set(26, 19, "s")
    _orb(g, 27, 7, 3)
    _eyes(g, 14, 13, 18)
    return g.rows()


# --------------------------------------------------------------------------------------------
# D. STORMBOUND - no staff. The light is in his hand and the cloak is doing the talking.
# --------------------------------------------------------------------------------------------

def stormbound():
    g = Grid()
    # Cloak first and widest: it flares out behind him to the right and is the silhouette.
    for y in range(16, 30):
        t = (y - 16) / 13.0
        g.span(y, round(17 + 1 * t), round(19 + 10 * t), "b")
    _shade(g, 16, 29, "c", "b", "a", rim=False)
    # Body, leaning into the wind.
    _taper(g, 15, 30, 4.5, 7.0, "c", lean=-1.2, cx0=14.5)
    _shade(g, 15, 30, "d", "c", "b")
    # Hat blown back: the crown trails to the right, the brim survives only on the windward
    # side. Asymmetry is the whole point of this one.
    # The crown has to sit ON the head. Centred on x=20 while the face was at x=14, it came out
    # floating in the air to the right of the figure with daylight under it.
    for i, y in enumerate(range(5, 11)):
        g.span(y, 10 + i, 19 - i * 0.4, "c")
    g.line(24, 4, 19, 8, "b")
    g.line(23, 4, 18, 8, "c")
    _shade(g, 4, 10, "d", "c", "b")
    g.span(10, 8, 20, "b")
    g.span(11, 7, 19, "a")
    g.set(8, 10, "W")
    g.set(7, 11, "W")
    _void(g, 12, 16, 3.8, 2.8, cx=14.0)
    # Beard blown the same way as the cloak - two directions of wind and the figure stops
    # holding together.
    _beard(g, 14.0, 17, 22, 3.2, 0.8, sweep=2.6)
    # Raised arm, and the light held in the bare hand rather than on a stick.
    g.span(18, 5, 9, "c")
    g.span(19, 5, 9, "b")
    g.set(5, 18, "W")
    g.span(17, 5, 6, "s")
    _orb(g, 4, 15, 2)
    g.set(8, 11, "Y")
    g.set(10, 8, "y")
    g.span(30, 8, 29, "a")
    g.span(29, 25, 29, "b")
    _contact(g, 8, 29)
    _eyes(g, 14, 12, 16)
    return g.rows()


OPTIONS = [
    ("conclave", conclave),
    ("vigil", vigil),
    ("archmagus", archmagus),
    ("stormbound", stormbound),
]


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_wizards")
    if not os.path.isdir(out):
        os.makedirs(out)
    sets = []
    problems = []
    for name, fn in OPTIONS:
        rows = fn()
        problems += P.check_anchor(name, [rows], CELL)
        sets.append((name, [rows]))
        P.write_strip([rows], PALETTE, os.path.join(out, name + ".png"), CELL)
        print("%-11s ok" % name)
    P.preview(sets, PALETTE, os.path.join(out, "_gallery.png"), zoom=10)
    if problems:
        print("")
        print("ANCHOR PROBLEMS:")
        for p in problems:
            print("   " + p)
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
