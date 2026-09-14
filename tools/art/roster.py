# -*- coding: utf-8 -*-
"""The full cast, redrawn dense: four wizards designed from their spells, nine enemies.

Run: python tools/art/roster.py     -> tools/art/_roster/ (gitignored)

A style proposal, not a shipping generator. Nothing here is wired and `.ai/art-direction.md` is
deliberately not edited - if this direction is right, the contract gets a written amendment
rather than art quietly drawn against it.

THE TARGET IS A NUMBER. Detail density measured as INTERNAL EDGES PER FILLED PIXEL: how often
the eye is handed a boundary inside the figure rather than at its outline. The shipping enemies
run 0.74-1.00, the test wizard Eric drew runs 0.83 on seven colours, and four player studies I
drew to a silhouette-first reading came in at 0.46-0.55. Floor here is 0.80, and main() prints
the number for every sprite so a regression is visible rather than argued about.

ONE FAILURE IS WORTH MORE THAN THE TARGET. An earlier shielder scored 0.90 and was still wrong:
it banded the torso with evenly spaced plates and read as a radiator. Evenly spaced anything at
32px is a stripe pattern, and the metric cannot tell that repetition carries no information.
Every helper below that lays down furniture takes irregular input for that reason.

THE TWO CASTS ARE BUILT ON OPPOSITE RULES.

  Enemies wear the DEEP half of their ramp and carry exactly one emissive pixel-pair: cold
  arcane if the thing was TAKEN, fire if it was BUILT. The shipping art breaks this badly -
  shielder wears steel.0 #DCE8F4, the lightest steel in the palette - which is most of why the
  horde does not read as the dark wizard's.

  Wizards are designed FROM THE SPELL rather than from an archetype. The staff head, the robe
  cut, the stance and the trim all come out of what the character casts, so two wizards are
  never one figure in a different colour. That is also what retires the modulate-per-character
  stopgap the contract forbids.
"""
import os

import bonelight as B
import pixel as P

M = B.MATERIALS
E = B.ELEMENTS
OCC = B.OCC
RIM = B.RIM

ENEMY_CELL = 32
WIZARD_CELL = 48

PALETTE = B.build_palette({
    "o": OCC, "W": RIM,
    "T": ("steel", "hi"), "t": ("steel", "lit"), "r": ("steel", "base"),
    "R": ("steel", "shade"), "q": ("steel", "deep"),
    "L": ("linen", "hi"), "l": ("linen", "lit"), "e": ("linen", "base"),
    "E": ("linen", "shade"), "A": ("linen", "deep"),
    "D": ("wool", "hi"), "d": ("wool", "lit"), "c": ("wool", "base"),
    "b": ("wool", "shade"), "a": ("wool", "deep"),
    "S": ("skin", "hi"), "s": ("skin", "lit"), "k": ("skin", "base"),
    "n": ("skin", "shade"), "m": ("skin", "deep"),
    "H": ("gold", "hi"), "G": ("gold", "lit"), "g": ("gold", "base"), "u": ("gold", "shade"),
    "V": ("violet", "hi"), "v": ("violet", "lit"), "w": ("violet", "base"),
    "x": ("violet", "shade"), "X": ("violet", "deep"),
    "P": ("stone", "hi"), "p": ("stone", "lit"), "N": ("stone", "base"),
    "j": ("stone", "shade"), "J": ("stone", "deep"),
    # warm skin, for faces that are LIT. The bone row above stays for beards and for the
    # skullsentry; a hero should not be drawn in the same tones as a skull.
    "F": ("flesh", "hi"), "f": ("flesh", "lit"), "h": ("flesh", "base"),
    "i": ("flesh", "shade"), "I": ("flesh", "deep"),
    # The four PLAYER PIGMENT rows. Saturated, primary, and reserved for the starter cast -
    # nothing the dark wizard made or took may wear them.
    "Q": ("ember", "hi"), "1": ("ember", "lit"), "2": ("ember", "base"),
    "3": ("ember", "shade"), "4": ("ember", "deep"),
    "U": ("azure", "hi"), "5": ("azure", "lit"), "6": ("azure", "base"),
    "7": ("azure", "shade"), "8": ("azure", "deep"),
    "O": ("amber", "hi"), "!": ("amber", "lit"), "@": ("amber", "base"),
    "#": ("amber", "shade"), "$": ("amber", "deep"),
    "C": ("verdant", "hi"), "%": ("verdant", "lit"), "^": ("verdant", "base"),
    "&": ("verdant", "shade"), "*": ("verdant", "deep"),
    # element ramps, four stops each: core / hot / mid / edge
    "+": E["fire"][0], "-": E["fire"][1], "=": E["fire"][2], "~": E["fire"][3],
    "<": E["ice"][0], ">": E["ice"][1], "?": E["ice"][2], "/": E["ice"][3],
    "[": E["lightning"][0], "]": E["lightning"][1], ";": E["lightning"][2], ":": E["lightning"][3],
    "{": E["earth"][0], "}": E["earth"][1], "|": E["earth"][2], "\\": E["earth"][3],
    "Z": E["arcane"][0], "Y": E["arcane"][1], "y": E["arcane"][2], "z": E["arcane"][3],
    "(": E["poison"][1], ")": E["poison"][2], "_": E["poison"][3],
})


# Robe cloth per wizard. Four different MATERIAL ROWS, not four tints of one - the first pass
# came out as four distinct silhouettes all wearing the same blue, which is the same failure as
# one silhouette in four tints, just rotated. Every row here is already in the contract.
ROBES = {
    # One saturated PLAYER PIGMENT row each, keyed to the spell the character opens on. These
    # are the only saturated things in the game: the horde has no colour, so the starters being
    # the four bright figures on screen is the story rather than an indulgence - and it is also
    # what makes four starter characters instantly separable to a player who knows nothing yet.
    "pyromancer": ("2", "1", "3"),      # ember   - true red, Fireball
    "frostweaver": ("6", "5", "7"),     # azure   - true blue, Cone of Cold
    "stormcaller": ("@", "!", "#"),     # amber   - true yellow, Chain Lightning
    "geomancer": ("^", "%", "&"),       # verdant - true green, Obsidian Spike
}


class G(object):
    def __init__(self, cell):
        self.cell = cell
        self.g = [["."] * cell for _ in range(cell)]

    def set(self, x, y, ch):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.cell and 0 <= y < self.cell and ch != ".":
            self.g[y][x] = ch

    def get(self, x, y):
        x, y = int(round(x)), int(round(y))
        return self.g[y][x] if 0 <= x < self.cell and 0 <= y < self.cell else "."

    def span(self, y, x0, x1, ch):
        for x in range(int(round(x0)), int(round(x1)) + 1):
            self.set(x, y, ch)

    def rect(self, x0, y0, x1, y1, ch):
        for y in range(int(round(y0)), int(round(y1)) + 1):
            self.span(y, x0, x1, ch)

    def line(self, x0, y0, x1, y1, ch):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        if n == 0:
            self.set(x0, y0, ch)
            return
        for i in range(n + 1):
            t = i / float(n)
            self.set(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, ch)

    def disc(self, cx, cy, r, ch):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.3:
                    self.set(x, y, ch)

    def rows(self):
        return ["".join(r) for r in self.g]


# --- the density toolkit --------------------------------------------------------------------
# Every one of these exists to put boundaries INSIDE a mass. A helper that only touches the
# outline is a helper that produces the flat interiors this file is a reaction to.

def body(g, y0, y1, hw0, hw1, mid, lit, dark, cx=None, lean=0.0, rim=True, folds=()):
    """A tapered mass, lit on the key side, with fold columns carried down its MIDDLE.

    `folds` is a tuple of offsets from the centre. They are the difference between a silhouette
    with something inside it and a silhouette with nothing inside it, and they cost one column
    each.
    """
    cx = g.cell / 2.0 - 0.5 if cx is None else cx
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        c = cx + lean * t * t
        lo, hi = int(round(c - hw)), int(round(c + hw))
        g.span(y, lo, hi, mid)
        g.set(lo, y, lit)
        if hi - lo >= 4:
            g.set(lo + 1, y, lit)
        g.set(hi, y, dark)
        if hi - lo >= 6:
            g.set(hi - 1, y, dark)
        if rim:
            g.set(lo - 1, y, "W")
        for i, off in enumerate(folds):
            fx = c + off
            if lo + 1 < fx < hi - 1:
                g.set(fx, y, dark if (y + i) % 4 else lit)


def plate(g, x0, y0, x1, y1, mid, lit, dark, seam_below=True):
    """A hard object: lit top edge, dark bottom, and occ under it so it sits ON what it covers."""
    g.rect(x0, y0, x1, y1, mid)
    g.line(x0, y0, x1, y0, lit)
    g.line(x0, y1, x1, y1, dark)
    g.line(x0, y0, x0, y1, lit)
    if seam_below:
        g.line(x0, y1 + 1, x1, y1 + 1, "o")


def tatters(g, rows_spec, mid, lit, dark):
    """A ragged hem. `rows_spec` is hand-written (y, x0, x1) so the tear is irregular - an
    evenly stepped hem is a staircase, which is the stripe failure wearing a different hat."""
    for (y, x0, x1) in rows_spec:
        g.span(y, x0, x1, mid)
        g.set(x0, y, lit)
        g.set(x1, y, dark)


def void(g, y0, y1, hw0, hw1, cx=None):
    """The face. Tapered, because a rectangle of black reads as a letterbox."""
    cx = g.cell / 2.0 - 0.5 if cx is None else cx
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        g.span(y, cx - hw, cx + hw, "o")


def face(g, cx, y0, y1, hw, brow="i"):
    """A face you can SEE. Warm skin, a brow shadow, and ordinary dark eyes.

    This is the single change that stops a starter reading as a villain. The enemies wear a void
    with two emissive points in it because that is the mark the dark wizard leaves in what he
    took - giving the player the same face said the opposite of what the story means. The hood
    still overhangs; the light still gets in under it.
    """
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        w = hw - 0.8 * t
        lo, hi = int(round(cx - w)), int(round(cx + w))
        g.span(y, lo, hi, "h")
        g.set(lo, y, "f")
        g.set(hi, y, "i")
    g.span(y0, cx - hw, cx + hw, brow)          # the brim's shadow, not a void
    g.set(cx, y0 + 2, "i")                      # the nose, one pixel
    g.set(cx, y0 + 3, "i")


def eyes(g, y, xl, xr, hot, cool):
    """Always last. Drawing these before the mass that overlaps them is how a whole set of
    studies once came out blind."""
    g.set(xl, y, hot)
    g.set(xr, y, cool)


def beard(g, cx, y0, y1, hw0, hw1, sweep=0.0, tip=None):
    """Moustache over a split mass with tapering strands. Flat fill reads as a bib."""
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        c = cx + sweep * t
        lo, hi = int(round(c - hw)), int(round(c + hw))
        g.span(y, lo, hi, "k")
        g.set(lo, y, "s")
        if hi - lo >= 3:
            g.set(hi, y, "n")
        if t < 0.74 and hi - lo >= 3:
            g.set(c, y, "n")
        if tip and t > 0.6:
            g.set(lo, y, tip)
    w = hw0 + 1.5
    g.span(y0 - 1, cx - w, cx + w, "k")
    g.set(cx - w, y0 - 1, "S")
    g.set(cx + w, y0 - 1, "n")
    g.set(cx, y0 - 1, "o")
    g.set(cx, y0, "o")


# --- the player cast's faces ------------------------------------------------------------------
# Enemies keep `face`/`eyes` above: a void with two emissive points is the mark of something the
# dark wizard took, and it is supposed to be the same on all of them. The PLAYERS are the opposite
# case - four people - so they get structure and expression instead.

def head(g, cx, y0, hw, brow="flat", gaze=0, mouth="set", eye="open", scar=None):
    """A face. Six rows: brow, forehead, eyes, cheek, mouth, jaw.

    KEPT DELIBERATELY LIGHT. An earlier version modelled a whole skull here - brow ridge, socket,
    cheekbone, jaw shadow, nose shadow - and every one of those is a dark pixel on a face eight
    pixels wide. Together they shut it: the geomancer came out as a brown blob and the pyromancer
    as a dark band where his eyes should be. Below about ten pixels, detail and legibility fight,
    and legibility has to win or there is no character to read at all.

    So the dark pixels are rationed to four features - brow, eyes, nose, mouth - and EXPRESSION
    COMES FROM WHERE THEY SIT, not from how much shadow surrounds them. The eye reads the angle of
    a brow and the spacing of two pupils long before it reads shading.
    """
    brow_y, fore_y, eye_y, cheek_y, mouth_y, jaw_y = (y0, y0 + 1, y0 + 2, y0 + 3, y0 + 4, y0 + 5)

    # The mass, tapering to a jaw. Lit on the key side, one step down on the far side, and that is
    # the only shading the face gets.
    for i, y in enumerate((brow_y, fore_y, eye_y, cheek_y, mouth_y, jaw_y)):
        w = hw - (0.0 if i < 3 else (i - 2) * 0.5)
        lo, hi = int(round(cx - w)), int(round(cx + w))
        g.span(y, lo, hi, "h")
        g.set(lo, y, "f")
        g.set(hi, y, "i")
    g.set(cx - hw + 1, cheek_y, "F")          # cheekbone: a LIGHT pixel, so it costs nothing

    # THE BROW. One row, plus at most two pixels of angle. This is most of the expression.
    g.span(brow_y, cx - hw + 0.5, cx + hw - 0.5, "i")
    if brow == "heavy":
        g.span(fore_y, cx - 2.6, cx + 2.6, "i")
    elif brow == "low":
        g.set(cx - 1, fore_y, "i")
        g.set(cx + 1, fore_y, "i")
    elif brow == "raised":
        g.set(cx - 2, brow_y - 1, "i")
        g.set(cx - 3, brow_y - 1, "i")
        g.set(cx + 2, fore_y, "i")

    # THE EYES. One dark pixel each; spacing and lids do the rest.
    spread = 1 if eye == "narrow" else (3 if eye == "wide" else 2)
    ex_l, ex_r = cx - spread + gaze, cx + spread + gaze
    for ex in (ex_l, ex_r):
        g.set(ex, eye_y, "I")
    if eye == "wide":
        # Whites showing above the pupil is what startled looks like.
        g.set(ex_l, eye_y - 1, "F")
        g.set(ex_r, eye_y - 1, "F")
    elif eye == "narrow":
        g.set(ex_l - 1, eye_y, "i")
        g.set(ex_r + 1, eye_y, "i")
    elif eye == "shadow":
        # Set back under the brow: the lid above each eye is shaded, the eye itself is not
        # enlarged. Four pixels of flesh.deep here is an empty socket, which is the ENEMIES' face.
        g.set(ex_l, eye_y - 1, "i")
        g.set(ex_r, eye_y - 1, "i")

    g.set(cx, cheek_y, "i")                   # the nose: one pixel

    if mouth == "set":
        g.span(mouth_y, cx - 1, cx + 1, "i")
    elif mouth == "grim":
        g.span(mouth_y, cx - 2, cx + 2, "i")
    elif mouth == "open":
        g.span(mouth_y, cx - 1, cx + 1, "I")
        g.set(cx, mouth_y + 1, "i")

    if scar is not None:
        for k in range(3):
            g.set(scar, cheek_y - 1 + k, "F")


def hair(g, cx, y0, y1, hw0, hw1, mid, lit, dark, sweep=0.0, tip=None, parted=False):
    """A mass of hair or beard. Same shape language as the old `beard`, but it takes its colours.

    The old one hardcoded the bone ramp, so all four wizards wore the same white wedge. Colour is
    half of what separates a young pyromancer from an ancient frostweaver, and it cost three
    parameters.
    """
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        c = cx + sweep * t
        lo, hi = int(round(c - hw)), int(round(c + hw))
        g.span(y, lo, hi, mid)
        g.set(lo, y, lit)
        if hi - lo >= 3:
            g.set(hi, y, dark)
        # A parting down the middle, so the mass has two halves rather than being a bib.
        if parted and t < 0.8 and hi - lo >= 3:
            g.set(c, y, dark)
        if tip and t > 0.62:
            g.set(lo, y, tip)
            if hi - lo >= 4:
                g.set(hi, y, tip)


def moustache(g, cx, y, hw, mid, lit, dark):
    """The bar above a beard. Without it a beard starts at the chin and reads as a bib."""
    g.span(y, cx - hw, cx + hw, mid)
    g.set(cx - hw, y, lit)
    g.set(cx + hw, y, dark)
    g.set(cx, y, dark)


def contact(g, x0, x1, y=None):
    y = g.cell - 2 if y is None else y
    g.span(y + 1, x0, x1, "o")


# =============================================================================================
# THE FOUR WIZARDS. 48x48. Each built from its spell outward - staff head first, because the
# weapon decides the silhouette and everything else is fitted around it.
# =============================================================================================

def pyromancer():
    """Fireball. A brazier on a pole: caged fire, not a polished orb. Upright and burning."""
    mid, lit, dark = ROBES["pyromancer"]
    g = G(48)
    CX, sx = 23.5, 33
    for y in range(15, 47):
        g.span(y, sx, sx + 1, "g" if y > 21 else "G")
        g.set(sx + 2, y, "u")
        if y in (19, 26, 36, 43):
            g.span(y, sx - 1, sx + 2, "u")
    plate(g, 30, 8, 37, 13, "~", "=", "q", seam_below=False)
    g.rect(31, 9, 36, 12, "=")
    g.rect(32, 10, 35, 11, "-")
    g.set(33, 10, "+")
    for bx in (31, 33, 35):
        g.line(bx, 8, bx, 13, "R")
    g.line(30, 13, 37, 13, "q")
    for (ex, ey, ec) in ((29, 9, "="), (38, 11, "~"), (30, 5, "-"), (36, 4, "~"), (34, 2, "=")):
        g.set(ex, ey, ec)
    body(g, 20, 45, 3.6, 7.8, mid, lit, dark, cx=CX, folds=(-1.5, 2.0))
    for y in range(40, 46):
        t = (y - 40) / 5.0
        hw = 3.6 + 4.2 * ((y - 20) / 25.0)
        g.span(y, CX - hw, CX - hw + 1 + 3 * t, dark)
        g.set(CX - hw, y, "~" if y % 2 else dark)
    g.span(44, CX - 6.8, CX - 2.6, "~")
    g.set(CX - 4, 45, "=")
    for i, bx in enumerate(range(-5, 6, 2)):
        g.span(30, CX + bx, CX + bx + 1, "r" if i % 2 else "R")
        g.set(CX + bx + 2, 30, "o")
    g.span(31, CX - 5, CX + 6, "q")
    # A bandolier from the left shoulder to the right hip, buckled. He is the only one of the four
    # wearing working gear rather than vestments, which is most of what makes him read as young.
    for k in range(11):
        bx, by = CX - 5.4 + k * 0.95, 22 + k * 0.8
        g.set(bx, by, "n")
        g.set(bx + 1, by, "m")
        if k % 3 == 1:
            g.set(bx, by + 1, "u")
    g.rect(CX + 3, 29, CX + 5, 31, "u")
    g.set(CX + 4, 30, "H")
    body(g, 10, 21, 1.4, 6.0, mid, lit, dark, cx=CX - 0.6, rim=False)
    g.span(17, CX - 5.2, CX + 5.0, dark)
    # THE YOUNGEST AND THE ANGRIEST. No white beard - he has not lived long enough for one, and
    # that alone separates him from the other three at a glance. Brow down, jaw set, and a burn
    # scar up one cheek from standing too close to his own work.
    head(g, CX, 18, 3.6, brow="low", gaze=0, mouth="grim", eye="narrow", scar=CX - 3)
    # Cropped dark hair and a short chin-strap, singed at the ends.
    for (tx, ty0, ty1) in ((CX - 3.6, 16, 19), (CX + 3.4, 16, 18)):
        for ty in range(ty0, ty1 + 1):
            g.set(tx, ty, "m")
            g.set(tx + (0.9 if tx > CX else -0.9), ty, "n")
    hair(g, CX, 24, 27, 2.6, 1.6, "m", "n", "o", tip="~")
    moustache(g, CX, 23, 2.0, "m", "n", "o")
    g.span(46, CX - 7.8, CX + 7.8, dark)
    contact(g, CX - 7.8, CX + 7.8)
    return g.rows()


def frostweaver():
    """Cone of Cold. The staff head is a splayed fan of crystal - the cone itself, held. The
    robe is cut in hard layered shards rather than folds, and rime climbs it from the hem."""
    mid, lit, dark = ROBES["frostweaver"]
    g = G(48)
    CX, sx = 23.5, 34
    for y in range(18, 47):
        g.span(y, sx, sx + 1, "r" if y > 24 else "t")
        g.set(sx + 2, y, "q")
        if y in (22, 31, 40):
            g.span(y, sx - 1, sx + 2, ">")
    # the fan: three splayed blades, uneven, springing from one root
    for (tipx, tipy) in ((28, 4), (34, 2), (39, 6)):
        g.line(sx + 0.5, 18, tipx, tipy, "?")
        g.line(sx + 0.5, 18, tipx + (1 if tipx > sx else -1), tipy + 1, "/")
        g.set(tipx, tipy, "<")
        g.set(tipx, tipy + 1, ">")
    g.disc(sx + 0.5, 17, 2.2, "/")
    g.disc(sx + 0.5, 17, 1.2, ">")
    g.set(sx, 17, "<")
    # robe: layered shards. Each layer is a plate with an occ seam, so the interior is all edges.
    body(g, 20, 45, 3.6, 7.6, mid, lit, dark, cx=CX, folds=(-2.0, 1.5))
    for (ly, lx0, lx1) in ((26, -4.6, 3.4), (32, -5.8, 4.6), (38, -6.8, 5.8)):
        g.line(CX + lx0, ly, CX + lx1, ly - 2, lit)
        g.line(CX + lx0, ly + 1, CX + lx1, ly - 1, "o")
    # rime climbing the hem, asymmetric
    for (ry, rx0, rx1) in ((44, -7.0, -3.0), (43, -6.4, -5.0), (45, -2.0, 1.0), (44, 3.6, 5.8)):
        g.span(ry, CX + rx0, CX + rx1, "/")
        g.set(CX + rx0, ry, "?")
    # icicles under the sleeve
    for (ix, iy) in ((16, 32), (18, 34), (31, 33)):
        g.line(ix, iy, ix, iy + 2, "/")
        g.set(ix, iy + 2, ">")
    body(g, 10, 21, 1.6, 5.8, mid, lit, dark, cx=CX - 0.4, rim=False)
    g.span(17, CX - 5.0, CX + 4.8, dark)
    # a crystal crown on the hood, three points of uneven height
    for cx2, top in ((-3.4, 9), (-0.4, 7), (2.6, 10)):
        g.line(CX + cx2, 12, CX + cx2, top, "?")
        g.set(CX + cx2, top, "<")
        g.set(CX + cx2 + 1, top + 1, "/")
    # THE OLDEST AND THE STILLEST. Level brow, eyes half-lidded, mouth hidden under a beard that
    # reaches his chest and has frozen at the ends. He is the only one of the four who is not
    # doing anything.
    # A shard-cut mantle over both shoulders, layered - the one garment that is clearly TAILORED
    # rather than draped, which suits the only member of the cast standing perfectly still.
    for (my, mx0, mx1) in ((25, -7.4, 7.2), (27, -8.0, 7.8), (29, -7.0, 6.8)):
        g.span(my, CX + mx0, CX + mx1, lit)
        g.span(my + 1, CX + mx0, CX + mx1, dark)
        g.set(CX + mx0, my, "?")
        g.set(CX + mx1, my + 1, "o")
    head(g, CX, 18, 3.6, brow="flat", gaze=0, mouth="hidden", eye="narrow")
    moustache(g, CX, 23, 2.6, "k", "S", "n")
    hair(g, CX, 24, 33, 3.0, 1.0, "k", "S", "n", tip=">", parted=True)
    g.span(31, 29, 33, mid)
    g.span(32, 29, 33, dark)
    g.set(33, 31, "s")
    g.span(46, CX - 7.6, CX + 7.6, dark)
    contact(g, CX - 7.6, CX + 7.6)
    return g.rows()


def stormcaller():
    """Chain Lightning. Two prongs with an arc jumping between them, and the arc repeats down
    the robe as a chain of links. The only wizard leaning, because the strike comes off him."""
    mid, lit, dark = ROBES["stormcaller"]
    g = G(48)
    CX, sx = 22.0, 33
    for y in range(16, 47):
        g.span(y, sx, sx + 1, "r" if y > 22 else "t")
        g.set(sx + 2, y, "q")
    # the fork, prongs of different length, with the arc live between them
    g.line(sx, 16, 29, 7, "t")
    g.line(sx + 1, 16, 38, 5, "t")
    g.set(29, 7, "r")
    g.set(38, 5, "r")
    for (ax, ay, ac) in ((31, 8, "]"), (33, 7, "["), (35, 6, "]"), (34, 9, ";"), (32, 10, ":")):
        g.set(ax, ay, ac)
    g.set(33, 5, "]")
    # body, leaning away from the strike
    body(g, 19, 45, 3.6, 7.4, mid, lit, dark, cx=CX, lean=-1.6, folds=(-1.6, 1.8))
    # a chain of links down the robe: the spell's own shape, worn
    for i, ly in enumerate(range(24, 43, 4)):
        lx = CX - 1.4 * ((ly - 24) / 19.0) ** 2 * 4
        g.span(ly, lx - 1, lx + 1, "g")
        g.set(lx, ly, "H")
        g.set(lx, ly + 1, "u")
        if i % 2:
            g.set(lx + 2, ly, "]")
    # jagged hem - torn by the discharge, not cut
    tatters(g, [(44, 15, 19), (45, 13, 21), (44, 22, 26), (45, 24, 29), (43, 27, 30)],
            dark, mid, "o")
    body(g, 9, 20, 1.4, 5.6, mid, lit, dark, cx=CX - 1.0, rim=False)
    g.span(16, CX - 5.0, CX + 4.4, dark)
    # hair blown up off the brow, which is the static charge showing
    for (hx, hy) in ((-4.0, 8), (-2.2, 7), (-0.4, 6), (1.4, 7), (3.0, 8)):
        g.line(CX + hx, 10, CX + hx, hy, "n")
        g.set(CX + hx, hy, "s")
    # MID-SHOUT. One brow up, eyes wide, mouth open, and looking off to the side rather than out
    # of the screen - he is the only one of the four caught in the middle of a cast.
    # A torc at the throat and a hem the storm has been at. Nothing on him is tidy.
    g.span(23, CX - 4.0, CX + 3.0, "u")
    g.span(24, CX - 4.4, CX + 3.4, "g")
    g.set(CX - 4.4, 24, "H")
    g.set(CX + 3.4, 23, "o")
    for (rx, ry) in ((CX - 7.0, 43), (CX - 4.0, 45), (CX + 1.0, 44), (CX + 5.0, 45)):
        g.set(rx, ry, "]")
        g.set(rx + 1, ry + 1, ";")
    head(g, CX - 0.6, 17, 3.4, brow="raised", gaze=-1, mouth="open", eye="wide")
    # Hair blown up and back by his own weather. Iron grey, not white, and swept hard enough that
    # the silhouette leans even though the body does not.
    # Hair escaping from UNDER the brim on both sides and blown back, rather than floating above
    # the hat - which is where the first pass put it, so it read as a separate grey cloud.
    for (hx, hy, hl, dx, dy) in ((-4.4, 19, 3, -0.8, -0.7), (-4.0, 21, 2, -0.9, -0.5),
                                 (3.8, 19, 3, 0.8, -0.7)):
        for k in range(hl):
            g.set(CX - 0.6 + hx + dx * k, hy + dy * k, "n" if k % 2 else "k")
    hair(g, CX - 0.6, 23, 29, 2.4, 0.9, "n", "k", "m", sweep=-1.6, tip="]")
    g.span(30, 27, 32, mid)
    g.span(31, 27, 32, dark)
    g.set(32, 30, "s")
    g.span(46, CX - 7.4, CX + 7.4, dark)
    contact(g, CX - 7.4, CX + 7.4)
    return g.rows()


def geomancer():
    """Obsidian Spike. A raw black spike instead of a shaft, and the only wizard built LOW and
    BROAD - the spell erupts from under the target, so he is the one planted in the ground."""
    mid, lit, dark = ROBES["geomancer"]
    g = G(48)
    CX = 23.5
    # the spike: short, thick, faceted, held low
    # Wider, and with a lit facet down one side. The first pass was 2px of near-black and read
    # as a stick rather than as a shard of rock big enough to be the weapon.
    for i, y in enumerate(range(20, 47)):
        hw = 1.6 + 1.4 * (i / 26.0)
        g.span(y, 33 - hw, 33 + hw, "N")
        g.set(33 - hw, y, "p")
        g.set(33 - hw + 1, y, "N" if i % 5 else "p")
        g.set(33 + hw, y, "o")
        g.set(33 + hw - 1, y, "j")
    g.line(33, 19, 30, 12, "j")
    g.line(34, 19, 37, 11, "j")
    g.line(32, 19, 32, 9, "j")
    g.line(33, 19, 33, 8, "N")
    g.line(34, 18, 34, 11, "J")
    g.set(33, 8, "p")
    g.set(33, 9, "P")
    g.line(32, 13, 34, 16, "o")
    g.set(31, 12, "|")
    g.set(36, 11, "\\")
    # low broad body: 2.0:1 where the others are 2.4-2.6, and it is a deliberate outlier
    body(g, 22, 45, 5.0, 9.4, mid, lit, dark, cx=CX, folds=(-2.4, 2.2))
    # stone plates laid on the shoulders and skirt, uneven
    plate(g, 13, 23, 19, 26, "N", "p", "j")
    plate(g, 27, 24, 33, 27, "j", "N", "J")
    plate(g, 15, 33, 22, 36, "N", "p", "j")
    plate(g, 25, 35, 31, 38, "j", "N", "J")
    for (gx, gy) in ((16, 24), (29, 25), (18, 34), (27, 36)):
        g.set(gx, gy, "}")
    # a heavy hem that sits on the ground rather than floating above it
    g.span(45, CX - 9.4, CX + 9.4, "j")
    g.span(44, CX - 9.0, CX - 5.0, "N")
    g.span(44, CX + 4.0, CX + 9.0, "N")
    body(g, 12, 23, 2.2, 6.2, mid, lit, dark, cx=CX, rim=False)
    g.span(19, CX - 5.4, CX + 5.2, dark)
    # a brow of raw rock over the face, asymmetric
    g.span(18, CX - 5.0, CX - 1.0, "N")
    g.span(17, CX - 4.0, CX - 2.0, "p")
    # A SLAB OF A FACE under a slab of rock. The brow is two rows thick and the eyes are lost
    # beneath it - two catchlights and nothing else - which is the one face in the cast you cannot
    # read, and that is the point: he is the patient one.
    head(g, CX, 20, 3.8, brow="heavy", gaze=0, mouth="set", eye="shadow")
    # A squared-off beard, earth-dark rather than white, with moss caught in it.
    moustache(g, CX, 26, 2.8, "N", "p", "J")
    hair(g, CX, 27, 36, 3.6, 2.6, "N", "p", "J", parted=True)
    for (mx, my) in (CX - 2, 31), (CX + 2, 33), (CX - 3, 34):
        g.set(mx, my, "&")
    contact(g, CX - 9.4, CX + 9.4)
    return g.rows()


# =============================================================================================
# THE ENEMIES. 32x32. Deep half of every ramp, one emissive pair, irregular furniture.
# Arcane eye = TAKEN. Fire eye = BUILT. That distinction is the whole colour language.
# =============================================================================================

def swarmer():
    """The buried, in linen. Narrow cowl, no visible limbs - the cheapest body in the roster
    and the one that has to read at the smallest size on the most crowded screen."""
    g = G(32)
    body(g, 9, 28, 3.0, 5.8, "E", "e", "A", folds=(-1.4, 1.6))
    g.span(12, 12, 19, "A")
    void(g, 10, 14, 2.8, 2.0)
    tatters(g, [(26, 11, 15), (27, 10, 14), (26, 17, 20), (28, 12, 18), (27, 19, 21)],
            "A", "E", "o")
    for (wy, wx0, wx1) in ((17, 12, 19), (21, 11, 20)):
        g.span(wy, wx0, wx1, "e")
        g.span(wy + 1, wx0, wx1, "o")
        g.set(wx0, wy, "l")
    g.set(13, 24, "E")
    contact(g, 11, 20)
    eyes(g, 12, 13, 18, "Y", "y")
    return g.rows()


def runner():
    """Pitched hard forward, cloak streaming behind. Speed reads as a diagonal, so nothing on
    this one is vertical.

    Two failures in the previous pass, and they are the same failure twice. The cloak was drawn
    in the SAME tone as the body, so the figure disappeared into its own clothing and the whole
    sprite read as one dark wedge - a trailing cloth has to be a different value from the thing
    it trails off, or there is no thing. And the claws were loose pixels floating beside the
    body: a claw needs an arm attached to it or the eye files it as dirt on the screen.

    So: the cloak is linen.deep behind a linen.shade body with an occ seam between them, and
    both arms are limbs that start at a shoulder and end in a hand.
    """
    g = G(32)
    # CLOAK FIRST, and darkest, so everything else lands on top of it.
    for i, y in enumerate(range(11, 27)):
        t = i / 15.0
        x0 = 2.0 + i * 0.62
        x1 = 11.0 + i * 0.30 - 3.0 * t
        g.span(y, x0, x1, "A")
        g.set(x0, y, "E")                      # a lit torn edge
        if i % 3 == 1:
            g.set(x0 - 1, y, "E")              # the tear, irregular
        if i % 4 == 2:
            g.span(y, x1 - 2, x1, "o")         # a fold inside the cloth
    g.line(1, 12, 5, 10, "A")
    g.line(1, 19, 4, 17, "A")

    # BODY on top of it, one full step lighter, with its own folds.
    body(g, 10, 28, 2.8, 4.4, "E", "e", "A", lean=2.4, folds=(-1.0, 1.2))
    for y in range(12, 27):                    # the seam that separates body from cloak
        g.set(12 + (y - 12) * 0.30, y, "o")

    # ARMS: shoulder, forearm, hand. One reaching, one trailing.
    g.line(17, 16, 21, 19, "e")
    g.line(17, 17, 21, 20, "A")
    g.line(21, 19, 24, 20, "h")
    for (cx2, cy2) in ((25, 19), (25, 21), (24, 22)):
        g.set(cx2, cy2, "i")                   # fingers, on the hand
    g.line(13, 18, 10, 23, "A")
    g.line(13, 19, 11, 24, "E")
    g.set(10, 24, "h")
    g.set(9, 25, "i")

    # HOOD: a cowl with a brow, not a block.
    g.span(10, 12, 19, "A")
    g.span(11, 11, 19, "E")
    g.span(12, 11, 18, "A")
    g.set(11, 11, "e")
    void(g, 12, 15, 2.6, 1.6, cx=14.8)
    tatters(g, [(27, 15, 20), (28, 16, 22), (26, 20, 23)], "A", "E", "o")
    contact(g, 14, 23)
    eyes(g, 13, 13, 17, "Y", "y")
    return g.rows()

def bruiser():
    """The buried who were soldiers. Broad, horned, and the heaviest silhouette in the basic
    four - it has to be readable as a wall from across the arena."""
    g = G(32)
    body(g, 11, 28, 4.6, 7.4, "R", "r", "q", folds=(-2.4, 2.0))
    plate(g, 11, 13, 20, 17, "r", "t", "q")
    g.set(15, 15, "q")
    g.set(16, 15, "G")
    plate(g, 10, 20, 17, 22, "R", "r", "q")
    plate(g, 14, 24, 22, 26, "q", "R", "o")
    g.rect(12, 5, 19, 11, "R")
    g.line(12, 5, 19, 5, "r")
    g.line(11, 3, 12, 6, "q")
    g.line(20, 2, 19, 6, "q")
    g.set(11, 3, "R")
    g.set(20, 2, "R")
    g.rect(13, 7, 18, 10, "o")
    g.line(15, 7, 15, 10, "q")
    g.line(12, 11, 19, 11, "o")
    tatters(g, [(27, 12, 17), (28, 11, 19), (26, 18, 21)], "A", "E", "o")
    contact(g, 10, 22)
    eyes(g, 8, 14, 17, "Y", "y")
    return g.rows()


def shielder():
    """Hunched behind a plane of iron. The shield is a separate object, not a pattern on the
    body, so it gets its own occ seam down the side of the figure."""
    g = G(32)

    def lean_at(y):
        return 15.5 + 1.8 * max(0.0, (y - 9) / 19.0)

    body(g, 11, 28, 4.2, 7.2, "R", "r", "q", lean=1.8, folds=(2.0,))
    plate(g, 12, 13, 19, 18, "r", "t", "q")
    g.set(16, 15, "G")
    g.set(16, 16, "u")
    for (py, px0, px1) in ((21, 11, 17), (23, 13, 21), (26, 12, 19)):
        g.span(py, px0, px1, "r")
        g.span(py + 1, px0, px1, "q")
        g.set(px0, py, "t")
    g.line(11, 14, 20, 21, "q")
    g.rect(11, 4, 19, 11, "R")
    g.line(11, 4, 19, 4, "r")
    g.line(10, 2, 11, 5, "q")
    g.line(20, 1, 19, 5, "q")
    g.rect(12, 7, 18, 10, "o")
    g.line(15, 7, 15, 10, "q")
    g.line(11, 11, 19, 11, "o")
    for i, y in enumerate(range(12, 27)):
        off = 3 - abs(i - 7) * 0.30
        g.span(y, 4.4 - off * 0.5, 8.4 + off * 0.28, "q")
        g.set(4.4 - off * 0.5, y, "R")
    g.set(7, 18, "G")
    g.set(6, 19, "u")
    g.line(9, 19, 11, 19, "q")
    g.line(9, 12, 9, 26, "o")
    tatters(g, [(24, 13, 22), (25, 14, 21), (26, 15, 20), (27, 17, 19)], "A", "E", "o")
    contact(g, 11, 22)
    eyes(g, 8, 13, 17, "Y", "y")
    return g.rows()


def skullsentry():
    """BUILT, not taken - so it burns rather than stares. A floating skull with an iron collar,
    and the only enemy in the roster allowed the fire ramp."""
    g = G(32)
    g.disc(15.5, 13, 6.4, "n")
    g.disc(15.5, 13, 5.4, "k")
    g.disc(15.5, 12, 4.0, "s")
    g.rect(12, 11, 14, 14, "o")
    g.rect(17, 11, 19, 14, "o")
    g.line(15, 15, 16, 17, "n")
    for tx in range(12, 20, 2):
        g.set(tx, 18, "S")
        g.set(tx + 1, 18, "m")
    g.line(10, 13, 11, 9, "n")
    g.line(21, 12, 20, 8, "n")
    plate(g, 11, 19, 20, 21, "R", "r", "q")
    for (rx, ry) in ((12, 20), (16, 20), (19, 20)):
        g.set(rx, ry, "t")
    for (fy, fx0, fx1) in ((23, 13, 18), (25, 12, 17), (27, 14, 19)):
        g.span(fy, fx0, fx1, "~")
        g.set(fx0, fy, "=")
    g.set(15, 29, "~")
    g.set(18, 28, "=")
    eyes(g, 12, 13, 18, "+", "-")
    g.set(13, 13, "=")
    g.set(18, 13, "~")
    return g.rows()


def lunger():
    """Coiled and low. The only enemy that stops dead then moves fastest, so it is drawn
    mid-crouch with everything gathered - the pose IS the telegraph.

    The previous pass had a flat block for a hood and four loose diagonal pixels for claws. A
    crouch only reads if the legs are folded UNDER the mass rather than implied by it, and a claw
    only reads on the end of an arm.
    """
    g = G(32)
    # the coiled legs, folded under and visible - this is what makes it a crouch
    for (ly, lx0, lx1) in ((24, 7, 12), (25, 6, 13), (26, 6, 11), (24, 19, 24),
                           (25, 18, 25), (26, 20, 25)):
        g.span(ly, lx0, lx1, "A")
        g.set(lx0, ly, "E")
    g.line(9, 23, 8, 26, "o")
    g.line(22, 23, 24, 26, "o")

    # the mass, compressed and wide - a crouch is short and broad, never a tapering column
    body(g, 15, 27, 5.4, 6.6, "E", "e", "A", lean=0.8, folds=(-2.2, 1.8))
    for (sy, sx0, sx1) in ((18, 10, 21), (22, 9, 22)):
        g.span(sy, sx0, sx1, "A")
        g.span(sy + 1, sx0, sx1, "o")
        g.set(sx0, sy, "e")

    # ARMS gathered in front, each ending in a hand with fingers on it
    for (sgn, sx2) in ((-1, 11), (1, 20)):
        g.line(sx2, 17, sx2 + sgn * 3, 21, "E")
        g.line(sx2, 18, sx2 + sgn * 3, 22, "A")
        hx = sx2 + sgn * 3
        g.set(hx, 21, "h")
        g.set(hx, 22, "h")
        for k in range(3):
            g.set(hx + sgn * (1 + k * 0.6), 20 + k, "i")

    # HOOD: a cowl with a brow ridge and a fold, thrust forward and DOWN
    g.span(9, 12, 19, "A")
    g.span(10, 11, 20, "E")
    g.span(11, 10, 20, "A")
    g.span(12, 10, 19, "A")
    g.set(11, 10, "e")
    g.set(10, 11, "e")
    g.line(13, 13, 18, 13, "o")               # the brow, overhanging
    void(g, 11, 14, 3.0, 2.0, cx=14.6)
    tatters(g, [(28, 10, 16), (29, 12, 20), (28, 19, 23)], "A", "E", "o")
    contact(g, 6, 25, y=28)
    eyes(g, 12, 12, 17, "Y", "y")
    return g.rows()

def slammer():
    """Arms up, mid-wind-up. The tell is a ring on the ground and the body has to agree with
    it - so the mass is top-heavy and the hands are the highest thing in the cell."""
    g = G(32)
    body(g, 13, 28, 5.0, 7.6, "R", "r", "q", folds=(-2.6, 2.2))
    for (ax, sgn) in ((8, -1), (23, 1)):
        g.line(ax, 14, ax + sgn * 1, 8, "R")
        g.line(ax + sgn, 14, ax + sgn * 2, 8, "q")
        g.rect(ax + (0 if sgn < 0 else -1), 5, ax + (2 if sgn < 0 else 1), 8, "q")
        g.line(ax + (0 if sgn < 0 else -1), 5, ax + (2 if sgn < 0 else 1), 5, "R")
    plate(g, 11, 15, 20, 19, "r", "t", "q")
    g.set(15, 17, "G")
    plate(g, 12, 22, 21, 24, "q", "R", "o")
    g.rect(12, 8, 19, 13, "R")
    g.line(12, 8, 19, 8, "r")
    g.rect(13, 10, 18, 12, "o")
    g.line(12, 13, 19, 13, "o")
    g.line(11, 7, 12, 9, "q")
    g.line(20, 6, 19, 9, "q")
    tatters(g, [(26, 12, 18), (27, 11, 20), (28, 13, 19)], "A", "E", "o")
    contact(g, 10, 22)
    eyes(g, 11, 14, 17, "Y", "y")
    return g.rows()


def exploder():
    """Distended and unstable - the one enemy you must find BEFORE it arrives, so it is the
    only silhouette in the roster that bulges instead of tapering, and it leaks."""
    g = G(32)
    g.disc(15.5, 19, 8.2, "A")
    g.disc(15.5, 19, 7.0, "E")
    g.disc(14.0, 17, 4.4, "e")
    g.set(12, 15, "l")
    # the seams of a thing about to come apart, all different lengths
    for (x0, y0, x1, y1) in ((10, 14, 13, 22), (17, 13, 19, 23), (12, 24, 21, 25),
                             (20, 15, 22, 20)):
        g.line(x0, y0, x1, y1, "o")
    for (px, py) in ((11, 20), (19, 18), (15, 25), (21, 22)):
        g.set(px, py, ")")
        g.set(px + 1, py, "_")
    g.disc(15.5, 10, 3.6, "A")
    g.disc(15.5, 10, 2.6, "E")
    g.rect(13, 9, 18, 11, "o")
    g.line(12, 7, 13, 5, "E")
    g.line(19, 7, 18, 4, "E")
    for (ly, lx) in ((27, 12), (28, 16), (27, 20)):
        g.set(lx, ly, "_")
    contact(g, 9, 22)
    eyes(g, 10, 13, 18, "(", ")")
    return g.rows()


def summoner():
    """A caster, and the hardest to keep distinct - it is the second robed figure in the roster
    after the swarmer. Separated by being TALLER, by a horned crown, and by the sigil it holds,
    which is the thing the swarmer has no equivalent of."""
    g = G(32)
    body(g, 8, 28, 2.8, 6.6, "x", "w", "X", folds=(-1.6, 1.8))
    g.line(9, 4, 11, 9, "X")
    g.line(22, 3, 20, 9, "X")
    g.set(9, 4, "x")
    g.set(22, 3, "x")
    g.span(11, 11, 20, "X")
    void(g, 9, 13, 2.8, 2.0)
    for (sy, sx0, sx1) in ((16, 11, 20), (20, 10, 21), (24, 11, 20)):
        g.span(sy, sx0, sx1, "w")
        g.span(sy + 1, sx0, sx1, "o")
        g.set(sx0, sy, "v")
    # the sigil: a ring it is holding open, the visible reason it is dangerous
    g.disc(24, 18, 3.4, "z")
    g.disc(24, 18, 2.4, "y")
    g.disc(24, 18, 1.2, "Y")
    g.set(24, 18, "Z")
    g.line(21, 19, 23, 18, "x")
    tatters(g, [(27, 11, 16), (28, 10, 19), (26, 18, 21)], "X", "x", "o")
    contact(g, 10, 21)
    eyes(g, 11, 13, 18, "Y", "y")
    return g.rows()


def hexer():
    """The only enemy that attacks from RANGE, and the roster's third robed figure - so almost
    every decision here is about not being the swarmer or the summoner.

    Three separators, because one is never enough at 32px:

      WOOL. Nothing else in the cast wears it. Swarmer, runner, lunger and exploder are linen;
      bruiser, shielder and slammer are steel; the summoner is violet. The wool row was sitting
      unused in the palette and it is a full material step away from all of them.

      THE DETACHED ORB, finally drawn as section 4 actually specifies it. The contract's Caster
      row says "a DETACHED floating orb, offset from the head" and nothing in the game has ever
      had one - the summoner holds a sigil ring down at hand height instead. So the hexer gets the
      real thing: an orb up beside the head with an occ gap under it, nothing connecting it to the
      hand that is reaching for it. A floating object with daylight under it is the strongest
      silhouette cue in the taxonomy and it was going spare.

      A SPLIT MITRE, not a cowl. Two peaks with a notch between them. The swarmer and the
      summoner both come to a single point; this one forks, which survives being filled solid.

    The macabre detail is the open robe front: the cloth parts down the middle and there are ribs
    behind it. That is also where most of the density comes from - a vertical slit with bone
    crossing it is boundaries all the way down.
    """
    g = G(32)
    # the crook it braces on, drawn FIRST so the body lands over the hand that holds it
    g.line(7, 9, 8, 29, "m")
    g.line(8, 9, 9, 29, "n")
    for (hx, hy) in ((7, 8), (8, 7), (9, 7), (10, 8), (10, 9)):
        g.set(hx, hy, "n")
    g.set(8, 6, "k")

    body(g, 8, 28, 2.6, 5.8, "b", "c", "a", folds=(-1.6, 1.8))

    # THE OPEN FRONT: cloth parted, ribs behind it. Irregular rib spacing, irregular lengths.
    for y in range(17, 26):
        g.span(y, 14.5, 16.5, "o")
    for (ry, rx0, rx1) in ((18, 13, 17), (20, 13, 18), (22, 14, 17), (24, 14, 18)):
        g.span(ry, rx0, rx1, "n")
        g.set(rx0, ry, "k")
        g.set(rx1, ry, "m")
    for (fy, fx0, fx1) in ((16, 11, 20), (21, 10, 21)):
        g.span(fy, fx0, fx1, "a")
        g.span(fy + 1, fx0, fx1, "o")
        g.set(fx0, fy, "c")

    # THE CORD: a knotted belt, one row plus a hanging tail. Wool over wool needs a hard edge.
    g.span(26, 11, 20, "a")
    g.set(12, 26, "d")
    g.line(13, 27, 13, 29, "a")
    g.set(13, 29, "D")

    # THE REACHING ARM, up and out to the right. It ends at a hand and then it stops.
    g.line(18, 15, 21, 13, "b")
    g.line(18, 16, 21, 14, "a")
    g.set(22, 13, "h")
    g.set(22, 12, "h")
    g.set(23, 12, "i")
    g.set(23, 13, "i")

    # THE SPLIT MITRE. Two peaks, a notch, and the left one is taller - symmetry reads as a hat.
    g.span(7, 12, 19, "a")
    g.span(6, 12, 18, "b")
    g.span(5, 12, 15, "a")
    g.span(4, 12, 14, "a")
    g.span(5, 17, 19, "a")
    g.set(13, 3, "b")
    g.set(12, 4, "c")
    g.set(18, 4, "b")
    g.set(16, 6, "o")                            # the notch between the peaks
    g.set(16, 7, "o")
    void(g, 8, 12, 2.8, 1.8)
    g.span(8, 12, 19, "a")                       # the brim shadow over the void

    # THE DETACHED ORB. Offset from the head, with occ under it and nothing joining the two.
    # r=2.6, not 2.8. At 2.8 the disc test keeps the corner cells and the orb comes out a SQUARE -
    # a floating cube is a crate, and a crate is not a threat.
    g.disc(25, 8, 2.6, "z")
    g.disc(25, 8, 1.9, "y")
    g.disc(25, 8, 0.9, "Y")
    g.set(25, 8, "Z")
    # THE GAP IS TRANSPARENT, not occ. Filling it with occ looked right in colour and failed the
    # section 4 test outright: occ is a drawn pixel, so under "fill the sprite solid black" the
    # orb was joined to the hand by a stem and the attachment stopped being detached at all. The
    # only thing that separates two masses in a silhouette is nothing.

    tatters(g, [(28, 11, 16), (29, 12, 19), (28, 18, 21), (29, 9, 11)], "a", "b", "o")
    contact(g, 9, 21)
    eyes(g, 10, 13, 18, "Y", "y")
    return g.rows()


WIZARDS = [("pyromancer", pyromancer), ("frostweaver", frostweaver),
           ("stormcaller", stormcaller), ("geomancer", geomancer)]
ENEMIES = [("swarmer", swarmer), ("runner", runner), ("bruiser", bruiser),
           ("shielder", shielder), ("skullsentry", skullsentry), ("lunger", lunger),
           ("slammer", slammer), ("exploder", exploder), ("summoner", summoner),
           ("hexer", hexer)]


# --- the idle ---------------------------------------------------------------------------------
# Two things move on every sprite in the cast, and they are deliberately the same two: the figure
# breathes, and whatever it is carrying or staring with breathes in step. The title screen cost
# that lesson - a light that holds still while the thing it lights moves reads as a decal.
#
# On a wizard the emissive is the staff head and the eyes. On an enemy it is the one pixel-pair
# of the dark wizard's mark. Pulsing it is not decoration: it is the only thing on an enemy drawn
# in the bright half of any ramp, so it is the only thing that CAN move without the body lying
# about how lit it is.

# Each element ramp in the palette, brightest first, so a pulse is one step along its own ramp
# and can never leave the contract.
RAMPS = ["1234", "5678", "!@#$", "%^&*", "ZYyz", "()_"]
_STEP = {}
for _r in RAMPS:
    for _i, _c in enumerate(_r):
        _STEP[_c] = _r[max(0, _i - 1)]          # one step BRIGHTER, clamped at the core

IDLE_DY = [0, -1, -1, 0]


def idle(rows):
    """Four frames from one pose: shift the whole cell, then step the emissive one stop up.

    The bob is a single pixel and takes the feet with it. Two reads better and breaks the
    contract's anchor rule, and an anchor that drifts is how a sprite ends up skating.
    """
    cell = len(rows)
    out = []
    for f in range(4):
        dy = IDLE_DY[f]
        blank = "." * cell
        r = list(rows)
        if dy:
            r = r[-dy:] + [blank] * -dy
        if f in (1, 2):
            r = ["".join(_STEP.get(ch, ch) for ch in line) for line in r]
        out.append(r)
    return out


def density(rows):
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
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_roster")
    if not os.path.isdir(out):
        os.makedirs(out)
    sparse = []
    for group, items, cell in (("WIZARDS", WIZARDS, WIZARD_CELL),
                               ("ENEMIES", ENEMIES, ENEMY_CELL)):
        print("=== %s (%dx%d) ===" % (group, cell, cell))
        sets = []
        for name, fn in items:
            rows = fn()
            filled, edges, ratio = density(rows)
            fr = idle(rows)
            P.write_strip(fr, PALETTE, os.path.join(out, name + ".png"), cell)
            sets.append((name, fr))
            flag = "" if ratio >= 0.80 else "  SPARSE"
            if ratio < 0.80:
                sparse.append(name)
            print("  %-12s %3d px  %3d edges  %.2f edges/px%s" % (name, filled, edges, ratio, flag))
        P.preview(sets, PALETTE, os.path.join(out, "_%s.png" % group.lower()),
                  zoom=8, cell=cell)
    if sparse:
        print("")
        print("below the 0.80 floor: %s" % ", ".join(sparse))
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
