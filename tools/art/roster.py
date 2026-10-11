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
import math
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
    # Water and grass, added for the two rescued wizards' staff heads (Riptide, Mirefoot).
    "0": E["water"][0], "9": E["water"][1], "K": E["water"][2], "M": E["water"][3],
    "B": E["grass"][0], ",": E["grass"][1], "'": E["grass"][2], "`": E["grass"][3],
})


# Robe cloth per wizard. Four different MATERIAL ROWS, not four tints of one - the first pass
# came out as four distinct silhouettes all wearing the same blue, which is the same failure as
# one silhouette in four tints, just rotated. Every row here is already in the contract.
ROBES = {
    # One saturated PLAYER PIGMENT row each, keyed to the spell the character opens on. These
    # are the only saturated things in the game: the horde has no colour, so the starters being
    # the four bright figures on screen is the story rather than an indulgence - and it is also
    # what makes four starter characters instantly separable to a player who knows nothing yet.
    # The Pyromancer no longer reads this row: his coat is drawn from the DEEP end of ember inside
    # pyromancer(), because the bright end made him read as a cartoon. Kept so the four starters
    # still document their pigment rows in one place.
    "pyromancer": ("2", "1", "3"),      # ember   - true red, Cinderbreath (see pyromancer())
    "frostweaver": ("6", "5", "7"),     # azure   - true blue, Cone of Cold
    "stormcaller": ("@", "!", "#"),     # amber   - true yellow, Chain Lightning
    "geomancer": ("^", "%", "&"),       # verdant - true green, Obsidian Spike
    # THE RESCUED WEAR NO PIGMENT. The four saturated rows above belong to the starters, and the
    # people the player carries out of a cell are wearing what they were imprisoned in: material
    # rows, muted. Their colour is in what they hold - the staff head is the one saturated thing
    # on them, and it is their spell (.ai/side-events.md).
    "tituba": ("w", "v", "x"),          # violet cloth  - Riptide, the sea she was taken from
    "vainamoinen": ("c", "d", "b"),     # wool, blue-grey - Bramble Seed, the forests he sowed
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

# Two skin tones on the flesh row. "light" is what the starters were drawn with; "deep" moves every
# role one step down the row so the face is the shade tone lit by the base tone, and the features
# drop to the deep end and the occlusion colour so they still read against a darker face.
SKIN_TONES = {
    "light": {"mass": "h", "lit": "f", "far": "i", "feature": "i", "eye": "I", "cheek": "F", "whites": "F"},
    "deep":  {"mass": "i", "lit": "h", "far": "I", "feature": "I", "eye": "o", "cheek": "h", "whites": "f"},
}


def head(g, cx, y0, hw, brow="flat", gaze=0, mouth="set", eye="open", scar=None, tone="light"):
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
    k = SKIN_TONES[tone]

    # The mass, tapering to a jaw. Lit on the key side, one step down on the far side, and that is
    # the only shading the face gets.
    for i, y in enumerate((brow_y, fore_y, eye_y, cheek_y, mouth_y, jaw_y)):
        w = hw - (0.0 if i < 3 else (i - 2) * 0.5)
        lo, hi = int(round(cx - w)), int(round(cx + w))
        g.span(y, lo, hi, k["mass"])
        g.set(lo, y, k["lit"])
        g.set(hi, y, k["far"])
    g.set(cx - hw + 1, cheek_y, k["cheek"])          # cheekbone: a LIGHT pixel, so it costs nothing

    # THE BROW. One row, plus at most two pixels of angle. This is most of the expression.
    g.span(brow_y, cx - hw + 0.5, cx + hw - 0.5, k["feature"])
    if brow == "heavy":
        g.span(fore_y, cx - 2.6, cx + 2.6, k["feature"])
    elif brow == "low":
        g.set(cx - 1, fore_y, k["feature"])
        g.set(cx + 1, fore_y, k["feature"])
    elif brow == "raised":
        g.set(cx - 2, brow_y - 1, k["feature"])
        g.set(cx - 3, brow_y - 1, k["feature"])
        g.set(cx + 2, fore_y, k["feature"])

    # THE EYES. One dark pixel each; spacing and lids do the rest.
    spread = 1 if eye == "narrow" else (3 if eye == "wide" else 2)
    ex_l, ex_r = cx - spread + gaze, cx + spread + gaze
    for ex in (ex_l, ex_r):
        g.set(ex, eye_y, k["eye"])
    if eye == "wide":
        # Whites showing above the pupil is what startled looks like.
        g.set(ex_l, eye_y - 1, k["whites"])
        g.set(ex_r, eye_y - 1, k["whites"])
    elif eye == "narrow":
        g.set(ex_l - 1, eye_y, k["feature"])
        g.set(ex_r + 1, eye_y, k["feature"])
    elif eye == "shadow":
        # Set back under the brow: the lid above each eye is shaded, the eye itself is not
        # enlarged. Four pixels of flesh.deep here is an empty socket, which is the ENEMIES' face.
        g.set(ex_l, eye_y - 1, k["feature"])
        g.set(ex_r, eye_y - 1, k["feature"])

    g.set(cx, cheek_y, k["feature"])                   # the nose: one pixel

    if mouth == "set":
        g.span(mouth_y, cx - 1, cx + 1, k["feature"])
    elif mouth == "grim":
        g.span(mouth_y, cx - 2, cx + 2, k["feature"])
    elif mouth == "open":
        g.span(mouth_y, cx - 1, cx + 1, k["eye"])
        g.set(cx, mouth_y + 1, k["feature"])

    if scar is not None:
        for i_scar in range(3):
            g.set(scar, cheek_y - 1 + i_scar, k["cheek"])


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
# THE SIX WIZARDS. 48x48. Each built from its spell outward - POSTURE first, then the staff, then
# everything else fitted around the two.
#
# WHY POSTURE FIRST. The previous six were designed from the spell too, but only from the staff
# head down: every one of them was the same standing cone of robe, head on top, a vertical staff
# on the figure's right. Filled solid black they were one shape, and only the few pixels at the
# staff tip told them apart - pairwise silhouette overlap 0.58-0.80, the Pyromancer and the
# Vainamoinen at 0.80. Hue was carrying the whole read, which section 4 of the contract forbids
# for enemies and which is no better an idea for the player. So each spell now decides how the
# body STANDS - thrusting, planted, calling down, braced, swaying, walking - before it decides
# what the body holds, and the set deliberately varies height, width, which hand holds the staff,
# the staff's angle, and what is on the head (bare, crown, blown hood, rock-browed hood, head-wrap,
# hat).
#
# Two limits from the derived animations (anim_sets.py), which shift and shear the whole cell:
# nothing is drawn above row 2, because `moving` bobs the figure up two rows; and nothing is drawn
# left of column 4, because `hurt` knocks it two columns left and shears the top two more.
# =============================================================================================

# --- the posed cast's helpers -----------------------------------------------------------------
# `body` above can only draw a tapered column with a centre line, which is exactly the shape every
# wizard ended up as. These draw an outline of any shape, so a figure can lean, stride and flare.

def mass(g, keys, mid, lit, dark, rim=True):
    """A form with any outline, from (y, left, right) keyframes interpolated row by row.

    Lit on the key side and shaded on the far side like `body`, but the rim is only laid where
    the pixel outside the form is EMPTY. `body` lays it unconditionally, which was harmless on a
    lone column and is wrong on a figure whose cloak or staff sits beside it: a rim drawn over
    another form is a white seam through the middle of the figure.
    """
    last = len(keys) - 2
    for k in range(len(keys) - 1):
        (ya, la, ra), (yb, lb, rb) = keys[k], keys[k + 1]
        for y in range(int(ya), int(yb) + (1 if k == last else 0)):
            t = (y - ya) / float(max(1, yb - ya))
            lo = int(round(la + (lb - la) * t))
            hi = int(round(ra + (rb - ra) * t))
            if hi < lo:
                continue
            if rim and g.get(lo - 1, y) == ".":
                g.set(lo - 1, y, "W")
            g.span(y, lo, hi, mid)
            g.set(lo, y, lit)
            if hi - lo >= 4:
                g.set(lo + 1, y, lit)
            if hi - lo >= 2:
                g.set(hi, y, dark)
            if hi - lo >= 6:
                g.set(hi - 1, y, dark)


def fold(g, pts, dark, lit, side=(-1, 0)):
    """A crease along a polyline: a dark stop with a lit stop beside it on the key side.

    Only paints where something is already drawn, so a fold can never leak past the outline it
    belongs to. The pair of stops is the point - section 4b counts a fold as a light stop and a
    dark stop adjacent, and a lone dark line is half of one. `side` says where the light falls:
    left of a steep crease, above a flat one.
    """
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + 1):
            t = i / float(max(1, n))
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            if g.get(x, y) in (".", "W"):
                continue
            g.set(x, y, dark)
            lx, ly = x + side[0], y + side[1]
            if lit and g.get(lx, ly) not in (".", "W", dark):
                g.set(lx, ly, lit)


def shaft(g, x0, y0, x1, y1, lit, mid, dark, width=2):
    """A pole at any angle, lit on its upper-left side so a diagonal staff still reads as round.

    A steep pole is thickened sideways and a shallow one downwards; thickening a near-horizontal
    line sideways only makes it longer.
    """
    steep = abs(y1 - y0) >= abs(x1 - x0)
    dx, dy = (1, 0) if steep else (0, 1)
    n = int(max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        g.set(x, y, lit)
        if width >= 3:
            g.set(x + dx, y + dy, mid)
            g.set(x + 2 * dx, y + 2 * dy, dark)
        else:
            g.set(x + dx, y + dy, dark)


def cut(g, *pts):
    """Clear pixels back to transparent. A tear in a cloak is a hole, and so is the gap between
    two striding legs: in the silhouette test only nothing separates two masses."""
    for (x, y) in pts:
        x, y = int(round(x)), int(round(y))
        if 0 <= x < g.cell and 0 <= y < g.cell:
            g.g[y][x] = "."


def hand(g, x, y, tone="light"):
    """Two by two, lit knuckle top-left. A hand is what makes a pole HELD rather than leant on."""
    k = SKIN_TONES[tone]
    g.set(x, y, k["lit"])
    g.set(x + 1, y, k["mass"])
    g.set(x, y + 1, k["mass"])
    g.set(x + 1, y + 1, k["far"])


# --- the walk ---------------------------------------------------------------------------------
# Every wizard function takes an optional stride `phase` (0..1). None draws the approved pose,
# untouched - the idle and the portrait are built from that and must not move by a pixel. A phase
# redraws ONLY what is below the waist (anim_sets.WALK_ANCHOR): legs and boots for the two who
# have visible legs, hem, skirt, train and a peeking toe for the robed. Everything above is the
# same code path either way, so the figure is the same person in every frame of the walk.

def stride(phase):
    """(c, lift_near, lift_far) for a walk phase.

    c runs +1 -> -1 -> +1: +1 is the near foot fully forward (the pose as drawn, for the two who
    stride), -1 the far one. Each foot is off the ground only while it swings FORWARD, which is
    what makes it a walk rather than two feet sliding back and forth on ice.
    """
    a = 2.0 * math.pi * phase
    c, s = math.cos(a), math.sin(a)
    return c, max(0.0, -s), max(0.0, s)


def leg(g, hip, ankle, length, cloth, boot, hw=(2.0, 2.0, 1.5), rim=True, toe_lit=True):
    """A leg from hip to ankle with the knee bent FORWARD, and a boot pointing forward under it.

    Two equal bones; when the ankle is nearer the hip than their length the knee folds out
    toward +x (the way the figure faces), so a lifted foot bends the leg instead of shrinking it.
    Drawn with `mass`, so it is lit and shaded the way the posed legs are.
    """
    (hx, hy), (ax, ay) = hip, ankle
    ay = int(round(ay))
    dx, dy = ax - hx, ay - hy
    d = math.hypot(dx, dy)
    half = length / 2.0
    if d >= length:
        kx, ky = hx + dx / 2.0, hy + dy / 2.0
    else:
        a = math.acos(min(1.0, d / length))
        ang = math.atan2(dy, dx) - a
        kx, ky = hx + half * math.cos(ang), hy + half * math.sin(ang)
    ky = int(round(max(hy + 1, min(ay - 1, ky))))
    mid, lit, dark = cloth
    mass(g, [(hy, hx - hw[0], hx + hw[0]), (ky, kx - hw[1], kx + hw[1]),
             (ay, ax - hw[2], ax + hw[2])], mid, lit, dark, rim=rim)
    # The crease down the front of the shin, so the knee reads as a joint and not a bend in a pipe.
    g.line(kx + hw[1], ky, ax + hw[2], ay - 1, "o")
    bmid, blit, bdark = boot
    mass(g, [(ay, ax - 2, ax + 2), (ay + 1, ax - 2, ax + 4), (ay + 2, ax - 2, ax + 5)],
         bmid, blit, bdark, rim=rim)
    if toe_lit:
        g.set(ax - 2, ay, "N")
    return kx, ky


def sway(g, y0, amount, y1=None):
    """Swing everything ALREADY DRAWN on rows y0..y1 sideways, more toward the hem.

    A skirt or a robe walking is a pendulum hung from the hips: nothing at the waist, most at the
    hem. Whole rows move, so call it before anything that should stay put (a staff, a hand) is
    drawn over those rows. The contact shadow row is never touched.
    """
    y1 = g.cell - 2 if y1 is None else y1
    for y in range(int(y0), int(y1) + 1):
        dx = int(round(amount * ((y - y0 + 1) / float(y1 - y0 + 1)) ** 1.4))
        if not dx:
            continue
        row = g.g[y]
        new = ["."] * g.cell
        for x, ch in enumerate(row):
            if 0 <= x + dx < g.cell:
                new[x + dx] = ch
        g.g[y] = new


def toe(g, x, y=None, w=4, shoe=("p", "N", "o"), shadow="o"):
    """A toe peeking out under a hem: the hem lifted over it, its edge in occlusion, and two rows
    of shoe pushing out one pixel PAST the hem's outline.

    The first version was one row of shoe inside the hem, in a dark leather, and at game size it
    was simply not there. The shoe wants a lit top-left stop and it wants to break the outline -
    a toe you can only find by looking for it does not tell anyone the figure is walking.
    """
    y = g.cell - 2 if y is None else y
    lit, mid, dark = shoe
    g.span(y - 2, x, x + w - 1, shadow)
    g.span(y - 1, x, x + w - 1, mid)
    g.set(x, y - 1, lit)
    g.set(x + 1, y - 1, lit)
    g.span(y, x, x + w, mid)
    g.set(x, y, lit)
    g.set(x + w, y - 1, dark)
    g.set(x + w + 1, y, dark)


def pyromancer(phase=None):
    """Cinderbreath - a held jet of fire. So he is the one wizard ATTACKING.

    Pitched forward mid-stride with his weight on the front foot, the brazier-staff levelled
    ahead of him like a spear with its burning head out in front and up, and a short cloak flaring
    out BEHIND him. The cast stand, plant, call or walk; he is the only one moving toward
    something. Legs are visible below a working coat rather than lost under a robe, which is both
    the stride and most of what makes him read as young.

    A SOLDIER, NOT A MASCOT. The first pass of this pose read cartoony, and for the three reasons
    the title-screen review named: the head was big against a short body, the legs were stubby,
    and the coat was a flat plane of saturated red - a red shirt. So the head is the smallest in
    the cast, the body is long (collar at row 13, a long stride below), and the coat and trousers
    come from the DEEP end of their rows with the light carried by a rim and a few lit stops. The
    structure is what makes it a garment: a high collar, a leather guard on the leading shoulder,
    a bandolier, a belt, folds, and the cloak's lining showing where it turns.

    The beats that were already right survive: no white beard, brow down, jaw set, a burn scar up
    one cheek from standing too close to his own work, and dark hair cropped short. He is the
    only one of the six bare-headed.
    """
    g = G(48)
    # Ember, from the deep end: shade for the coat's bulk, base only for its lit edge.
    mid, lit, dark = "3", "2", "4"

    # THE CLOAK, first and darkest so everything lands on it. Ember deep, a full value under the
    # coat - a trailing cloth in the tone of the body it trails off is just more body.
    mass(g, [(14, 20, 24), (17, 15, 23), (21, 11, 22), (25, 8, 21), (29, 6, 20), (31, 7, 19),
             (33, 10, 16)], "4", "3", "o")
    fold(g, [(19, 17), (12, 26), (9, 31)], "o", "3")
    fold(g, [(21, 20), (16, 29)], "o", "3")
    # The lining, where the hem turns back: the one place the bright end of the row is allowed.
    for (y, x0, x1) in ((30, 7, 10), (31, 8, 12), (32, 9, 14), (33, 11, 15)):
        g.span(y, x0, x1, "2")
        g.set(x0, y, "1")
    cut(g, (6, 30), (7, 32), (13, 33), (16, 33))                  # torn and singed, not cut

    # THE BACK LEG, pushing off. Dark leather-brown from the bottom of the flesh row, not the
    # wool row: wool deep renders navy, and a saturated blue leg was half of the cartoon.
    if phase is None:
        mass(g, [(32, 18, 22), (39, 15, 19), (44, 12, 15)], "I", "i", "o")
        fold(g, [(19, 34), (16, 41)], "o", None)
        mass(g, [(44, 11, 15), (45, 10, 16), (46, 10, 16)], "J", "j", "o")
        g.set(11, 44, "N")
        g.set(15, 45, "o")
    else:
        # Walking, the two legs trade places: c = +1 is the stride as drawn, -1 is the same
        # stride with this leg out in front. Both feet sit either side of x22, under the lean.
        c, lift_near, lift_far = stride(phase)
        # The rim goes on whichever leg is BEHIND: it is the figure's back outline. On the leg
        # stepping through in front it would be a white seam between the two.
        leg(g, (20, 32), (22 - 9.5 * c, 44 - 3 * lift_far), 14,
            ("I", "i", "o"), ("J", "j", "o"), rim=c > 0)

    # THE COAT, long, leaning into the thrust - its centre line runs from x25 at the collar to
    # x21 at the hem, which is the lean the eye reads before it reads anything else.
    mass(g, [(13, 22, 28), (16, 20, 30), (22, 19, 29), (29, 18, 28), (36, 16, 27)],
         mid, lit, dark)
    fold(g, [(27, 17), (25, 24), (23, 30), (22, 35)], "4", "2")
    fold(g, [(21, 22), (20, 29), (19, 35)], "4", "2")
    fold(g, [(26, 30), (25, 35)], "o", None)
    tatters(g, [(36, 16, 20), (37, 17, 19), (36, 22, 26), (37, 24, 26)], "4", "3", "o")
    # A high collar, turned up, and a leather guard on the leading shoulder.
    g.span(13, 23, 27, "4")
    g.set(23, 13, "2")
    g.span(12, 24, 26, "4")
    mass(g, [(14, 27, 30), (16, 26, 31), (18, 27, 31)], "i", "h", "I", rim=False)
    g.line(26, 19, 31, 19, "o")
    g.set(28, 15, "u")

    # THE FRONT LEG, on top of the coat because it is the one stepping through it.
    if phase is None:
        mass(g, [(33, 23, 27), (38, 27, 31), (41, 29, 32), (44, 30, 33)], "I", "i", "o",
             rim=False)
        g.line(27, 34, 30, 38, "o")                                # the knee bend
        mass(g, [(44, 30, 34), (45, 30, 36), (46, 30, 37)], "J", "j", "o", rim=False)
        g.set(30, 44, "N")
        g.set(31, 43, "u")                                         # a buckle on the boot
    else:
        ax = 22 + 9.5 * c
        ay = 44 - 3 * lift_near
        leg(g, (25, 33), (ax, ay), 14, ("I", "i", "o"), ("J", "j", "o"), rim=c < -0.3)
        g.set(ax + 1, ay - 1, "u")                                 # the buckle walks with it

    # A bandolier from the left shoulder to the right hip, with pouches - working gear, not
    # vestments - and a belt over it.
    for k in range(12):
        bx, by = 20.0 + k * 0.75, 16 + k * 1.0
        g.set(bx, by, "u")
        g.set(bx + 1, by, "I")
    for (px, py) in ((22, 19), (25, 23)):
        g.rect(px, py, px + 1, py + 1, "i")
        g.set(px, py, "h")
        g.set(px + 1, py + 2, "o")
    g.span(29, 18, 28, "I")
    g.set(18, 29, "i")
    g.rect(22, 28, 23, 29, "G")
    g.set(22, 28, "H")
    g.span(30, 18, 28, "o")

    # THE STAFF, levelled. Butt under the rear arm, head out past the front foot and up.
    shaft(g, 18, 34, 38, 14, "G", "g", "u")
    for (rx, ry) in ((23, 29), (31, 21), (35, 17)):
        g.set(rx, ry, "u")
        g.set(rx + 1, ry - 1, "H")

    # The two arms, both on the shaft - a sleeve each with a leather bracer, and an occ line
    # under each so they sit ON the coat instead of being painted into it.
    shaft(g, 21, 16, 28, 22, "2", "3", "4", width=3)
    g.line(21, 18, 27, 24, "o")
    g.set(27, 22, "I")
    g.set(26, 21, "i")
    shaft(g, 27, 16, 33, 18, "2", "3", "4", width=3)
    g.line(27, 19, 32, 20, "o")
    g.set(32, 17, "I")
    hand(g, 29, 22)
    hand(g, 34, 17)

    # THE BRAZIER: caged fire on the end of the pole, flaring forward and up - the held jet.
    plate(g, 37, 8, 43, 12, "~", "=", "q", seam_below=False)
    g.rect(38, 9, 42, 11, "=")
    g.rect(39, 9, 41, 10, "-")
    g.set(40, 9, "+")
    for bx in (37, 40, 43):
        g.line(bx, 8, bx, 12, "R")
    g.line(37, 12, 43, 12, "q")
    g.line(37, 7, 43, 7, "r")
    for (ex, ey, ec) in ((38, 6, "="), (39, 5, "-"), (41, 5, "="), (42, 4, "~"), (40, 3, "-"),
                         (43, 6, "~"), (41, 2, "~"), (44, 4, "~")):
        g.set(ex, ey, ec)

    # THE HEAD, pushed out over the front foot, and the smallest in the cast. Bare: cropped dark
    # hair over a set face.
    mass(g, [(4, 26, 29), (5, 24, 30), (6, 24, 31)], "I", "i", "o")
    head(g, 27, 7, 3.0, brow="low", gaze=1, mouth="grim", eye="narrow", scar=25)
    for ty in (7, 8):
        g.set(24, ty, "I")
        g.set(30, ty, "o")
    g.set(25, 7, "i")
    # A short chin-strap beard along the jaw, dark - he has not lived long enough for a white
    # one. No moustache: a dark bar over a grim mouth on a face this small reads as a shout.
    g.set(24, 11, "I")
    g.set(30, 11, "o")
    g.span(12, 25, 29, "I")
    g.set(25, 12, "i")
    g.span(13, 26, 28, "4")
    contact(g, 9, 38)
    return g.rows()


def frostweaver(phase=None):
    """Cone of Cold - the spell of a man standing perfectly still while the cold goes out of him.

    So he is the stillest figure in the cast and the tallest vertical: an upright, near-symmetric
    column, a crystal crown that sets the top of the figure, and a staff planted dead straight and
    taller again. All the asymmetry is on the floor - a long train pooling away to his right
    (screen left) - because a figure that is perfectly symmetric reads as a chess piece, and the
    one place stillness can afford to give way is the ground.

    A broad shard-cut mantle across both shoulders, steel-pale, the one garment in the cast that
    is clearly TAILORED. Oldest of the six: level brow, eyes half-lidded, a beard frozen at the
    ends and no mouth to be seen.
    """
    mid, lit, dark = ROBES["frostweaver"]
    g = G(48)
    CX = 22
    # THE TRAIN, first - on the ground, one value down from the robe so it reads as cloth lying
    # behind him rather than as more of him.
    mass(g, [(32, 16, 21), (37, 13, 21), (41, 9, 21), (44, 6, 21), (46, 4, 21)], "7", "6", "8")
    fold(g, [(16, 35), (11, 42), (8, 46)], "8", "6")
    fold(g, [(18, 37), (15, 43), (13, 46)], "8", "6")
    g.span(46, 5, 15, "8")
    for (rx, ry) in ((6, 45), (9, 46), (10, 45), (14, 46)):
        g.set(rx, ry, "t")                                        # rime on the hem, steel-pale
    if phase is not None:
        # Walking, the train DRAGS: it lags a quarter-step behind the hem, so its tail is still
        # swinging one way while the robe has already turned back the other.
        c, _ln, _lf = stride(phase)
        sway(g, 38, 1.5 * math.sin(2.0 * math.pi * phase))

    # THE COLUMN. Upright, widening by two pixels over twenty-eight rows - he is not going
    # anywhere.
    mass(g, [(18, 18, 26), (28, 17, 27), (40, 16, 28), (46, 16, 28)], mid, lit, dark)
    fold(g, [(20, 27), (20, 36)], dark, lit)
    fold(g, [(25, 29), (25, 34), (26, 45)], dark, lit)
    fold(g, [(21, 39), (21, 45)], dark, lit)
    # Layered shards cut into the skirt, each with an occ seam so the interior is all edges.
    for (ly, lx0, lx1) in ((33, -4.6, 2.4), (40, -5.4, 4.6)):
        g.line(CX + lx0, ly, CX + lx1, ly - 2, lit)
        g.line(CX + lx0, ly + 1, CX + lx1, ly - 1, "o")
    for (rx, ry) in ((17, 45), (19, 46), (24, 45), (27, 46), (26, 44)):
        g.set(rx, ry, "t")
    if phase is not None:
        # A slow, stately walk: no legs, only the hem swinging a pixel toward the leading foot
        # and the toe of that foot showing under it at each contact.
        hem = 1.5 * c
        sway(g, 38, hem)
        if abs(c) > 0.5:
            toe(g, (24 if c > 0 else 16) + int(round(hem)), shoe=("t", "r", "q"))

    # THE HOOD, close-cut, behind the crown.
    mass(g, [(9, 19, 25), (11, 18, 26), (15, 17, 27), (19, 17, 27)], "7", "6", "8")

    # THE MANTLE. Shards hanging from a yoke, of uneven length, each a lit left facet and a dark
    # right one, occ between neighbours. Evenly spaced shards would be the radiator again.
    # Six pixels a side wider than the column: the one place his outline is broad.
    mass(g, [(17, 15, 29), (18, 12, 32), (20, 9, 34), (21, 9, 34)], "r", "t", "R", rim=True)
    for (xc, hw, tip) in ((10.5, 1.6, 25), (14.0, 2.0, 30), (17.5, 1.6, 25), (21.0, 1.4, 23),
                          (26.5, 1.6, 26), (30.0, 2.0, 29), (33.0, 1.4, 24)):
        for y in range(21, tip + 1):
            w = hw * (tip - y) / float(tip - 20)
            lo, hi = int(round(xc - w)), int(round(xc + w))
            g.span(y, lo, hi, "r")
            g.set(lo, y, "t")
            g.set(hi, y, "q")
        g.set(xc, tip, "R")
        g.set(int(round(xc + hw)) + 1, 22, "o")
    g.span(17, 18, 26, "T")
    fold(g, [(15, 18), (11, 21)], "q", "T")
    fold(g, [(29, 18), (33, 21)], "q", "t")

    # THE STAFF: planted dead vertical, its fan head above the crown.
    sx = 36
    for y in range(13, 47):
        g.span(y, sx, sx + 1, "r" if y > 20 else "t")
        g.set(sx + 2, y, "q")
        if y in (22, 31, 40):
            g.span(y, sx - 1, sx + 2, "T")
            g.set(sx + 2, y, "R")
    for (tipx, tipy) in ((31, 5), (36, 2), (41, 4)):
        g.line(sx + 0.5, 13, tipx, tipy, "?")
        g.line(sx + 0.5, 13, tipx + (1 if tipx > sx else -1), tipy + 1, "/")
        g.set(tipx, tipy, "<")
        g.set(tipx, tipy + 1, ">")
    g.disc(sx + 0.5, 12, 2.0, "/")
    g.disc(sx + 0.5, 12, 1.1, ">")
    g.set(sx, 12, "<")
    # The arm that holds it, out of the mantle.
    shaft(g, 30, 25, 34, 27, "5", "6", "7", width=3)
    g.line(30, 27, 34, 29, "o")
    hand(g, 35, 26)

    # THE CROWN, five points of uneven height - steel and pale azure, not ice. Ice is emissive and
    # the idle pulses everything emissive; a crown that breathes reads as a lamp on his head.
    g.span(10, 18, 26, "t")
    g.span(11, 18, 26, "R")
    g.set(18, 10, "T")
    for (kx, top) in ((18, 7), (20, 4), (22, 3), (24, 5), (26, 8)):
        g.line(kx, 10, kx, top, "t")
        g.set(kx, top, "U")
        g.set(kx + 1, top + 1, "R")
    head(g, CX, 12, 3.4, brow="flat", gaze=0, mouth="hidden", eye="narrow")
    moustache(g, CX, 17, 2.6, "k", "S", "n")
    hair(g, CX, 18, 31, 3.0, 1.0, "k", "S", "n", tip="T", parted=True)
    contact(g, 4, 38)
    return g.rows()


def stormcaller(phase=None):
    """Chain Lightning - the arc comes down from above and jumps. So he is CALLING it down.

    The forked staff thrust up overhead in his raised left arm, its prongs higher than anything in
    the cast but the Frostweaver's crown, and a cloak streaming hard off his other shoulder as if
    the storm he is calling were already here. The only figure with an arm visibly up, and the
    only one whose outline is torn - cloak and hem both ripped into tongues by his own weather.

    Mid-shout, looking up at the staff: one brow up, eyes wide, mouth open. His hood has been
    blown back off his head and streams out behind it, and his iron-grey hair with it.
    """
    mid, lit, dark = ROBES["stormcaller"]
    g = G(48)
    # THE CLOAK, streaming right in three tongues. Amber shade with an amber base edge: one step
    # under the robe, and an occ crease along each tongue so the three stay three.
    ends = {17: 28, 18: 32, 19: 36, 20: 39, 21: 41, 22: 43, 23: 44, 24: 42, 25: 39, 26: 41,
            27: 43, 28: 44, 29: 42, 30: 38, 31: 39, 32: 41, 33: 39, 34: 35, 35: 33, 36: 30}
    if phase is not None:
        # Walking into his own wind, the three tongues snap out and back, each a third of a
        # cycle behind the one above it, so the cloak ripples rather than pulses.
        ends = dict(ends)
        for k, (ya, yb) in enumerate(((17, 24), (25, 29), (30, 36))):
            flick = int(round(1.4 * math.sin(2.0 * math.pi * (phase + k / 3.0))))
            for y in range(ya, yb + 1):
                if ends[y] > 30:
                    ends[y] += flick
    for y in sorted(ends):
        g.span(y, 23, ends[y], "#")
        g.set(ends[y], y, "$")
        if ends.get(y - 1, 0) < ends[y] - 1:
            g.span(y, ends.get(y - 1, 23) + 1, ends[y], "@")    # the lit upper edge of a tongue
    fold(g, [(25, 21), (33, 23), (40, 25)], "$", "@", side=(0, -1))
    fold(g, [(25, 27), (34, 29), (40, 31)], "$", "@", side=(0, -1))
    fold(g, [(26, 32), (33, 34)], "o", "#", side=(0, -1))

    # THE ROBE, leaning into the wind - the hem is blown out to the right under the cloak.
    mass(g, [(17, 17, 25), (24, 16, 26), (34, 15, 27), (42, 14, 29), (46, 14, 31)], mid, lit, dark)
    fold(g, [(19, 24), (18, 34), (19, 45)], dark, lit)
    fold(g, [(23, 27), (24, 37), (27, 45)], dark, lit)
    g.line(25, 18, 26, 36, "$")                                    # the seam under the cloak
    # A chain of links down the robe: the spell's own shape, worn.
    for i, ly in enumerate((22, 27, 31, 36, 40)):
        lx = 21 + (i % 2) - i * 0.3
        g.span(ly, lx - 1, lx + 1, "g")
        g.set(lx, ly, "H")
        g.set(lx, ly + 1, "u")
    # The hem the storm has been at: torn into tongues of its own.
    tatters(g, [(45, 14, 18), (46, 15, 20), (44, 22, 26), (46, 23, 28), (45, 28, 32)],
            dark, mid, "o")
    cut(g, (21, 46), (22, 46), (21, 45), (29, 46), (30, 46))
    if phase is not None:
        # The torn hem flicks toward the leading foot, and that foot shows under it.
        c, _ln, _lf = stride(phase)
        hem = 2.0 * c
        sway(g, 37, hem)
        if abs(c) > 0.5:
            toe(g, (25 if c > 0 else 16) + int(round(hem)), shoe=("p", "N", "o"))

    # THE RAISED ARM. The sleeve has slid down to the elbow, so the forearm is bare - which is
    # also what makes the arm read as UP rather than as a fold in the cloth.
    shaft(g, 13, 14, 18, 20, "!", "@", "#", width=3)
    g.line(14, 16, 19, 21, "o")
    shaft(g, 12, 10, 13, 14, "f", "h", "i")
    g.set(13, 14, "O")
    g.set(14, 15, "!")


    # THE STAFF, overhead, leaning out from the hand. Steel, with the fork at its top: two prongs
    # of different length splaying from a collar, and the arc live between them.
    shaft(g, 17, 27, 10, 8, "t", "r", "q")
    hand(g, 11, 11)
    g.rect(9, 7, 11, 8, "r")
    g.set(9, 7, "T")
    g.set(11, 8, "q")
    g.line(9, 7, 5, 2, "t")
    g.line(10, 7, 6, 3, "R")
    g.line(11, 7, 15, 3, "t")
    g.line(12, 7, 15, 4, "R")
    g.set(5, 2, "T")
    g.set(15, 3, "T")
    for (ax, ay, ac) in ((7, 3, "]"), (8, 4, "["), (9, 3, ";"), (10, 4, "]"), (11, 3, "["),
                         (12, 4, "]"), (13, 3, ":"), (10, 2, ";"), (16, 2, ":")):
        g.set(ax, ay, ac)

    # THE HOOD, blown back and streaming out behind the head.
    mass(g, [(10, 19, 24), (12, 18, 28), (14, 19, 31), (16, 21, 33), (18, 22, 30)],
         "#", "@", "$", rim=True)
    fold(g, [(24, 12), (30, 15)], "$", "@", side=(0, -1))
    # Hair escaping from under it, blown the same way.
    for (hx, hy, hl) in ((25, 11, 5), (24, 13, 4), (27, 17, 4)):
        for k in range(hl):
            g.set(hx + k, hy - k * 0.4, "n" if k % 2 else "k")
    head(g, 21, 12, 3.4, brow="raised", gaze=-1, mouth="open", eye="wide")
    hair(g, 21, 18, 22, 2.4, 1.0, "n", "k", "m", sweep=2.2)
    g.span(23, 18, 24, "u")                                        # the torc at the throat
    g.set(18, 23, "H")
    contact(g, 13, 33)
    return g.rows()


def geomancer(phase=None):
    """Obsidian Spike - the shard erupts from under the target. So he is the one PLANTED.

    A man braced over a spike he has driven into the earth. The shortest and broadest figure in
    the cast: broad shoulders, legs set wide apart, leaning his weight forward onto a shard of
    obsidian he has rammed point-first into the ground in front of him, both hands on its head
    as if holding it there. The ground at its point is cracked and lit from underneath.

    THE FACE IS THE FOCAL POINT, and two earlier passes lost it. The first drew him as a wide
    robe with a hood on top and read as a cabinet; the second hunched him by sinking the head
    between two pauldrons that were each bigger than it, in the shade end of the stone row, and
    read as two dark lumps with a cabinet under them. So now: the head sits forward of the
    shoulders rather than under them, lit, in a hood cut back to frame it; the pauldrons are
    small rough stones lit on the key side; and the body is a wedge - broad at the shoulders,
    narrowing to a ragged hem, then two braced legs - so the outline is a man and not a box.

    A heavy rock brow and narrowed eyes, but readable, and a squared earth-dark beard with moss
    in it. He is the patient one; patient is not the same as hidden.
    """
    g = G(48)
    # Verdant from the shade end: the robe is heavy cloth in the dark, and the face is the light.
    mid, lit, dark = "&", "^", "*"

    # THE LEGS, braced wide, under the hem: dark leather and stone-dark boots.
    if phase is None:
        mass(g, [(38, 14, 18), (42, 12, 16), (44, 11, 15)], "I", "i", "o")
        mass(g, [(44, 9, 15), (46, 8, 15)], "J", "j", "o")
        mass(g, [(38, 25, 29), (42, 26, 30), (44, 27, 31)], "I", "i", "o", rim=False)
        mass(g, [(44, 26, 32), (46, 26, 33)], "J", "j", "o", rim=False)
        g.set(9, 45, "N")
        g.set(27, 45, "N")
    else:
        # A heavy shuffle: he keeps both hands on the spike and moves his feet under him in
        # short steps, a boot lifting barely clear of the ground and setting down a pixel or two
        # on. The front foot cannot go far - the spike is in its way - so it steps back more
        # than it steps on.
        c, lift_near, lift_far = stride(phase)
        dl, ll = int(round(-1.5 * c)), int(round(2 * lift_far))
        dr, lr = int(round(min(1.0, 1.5 * c))), int(round(2 * lift_near))
        mass(g, [(38, 14, 18), (44 - ll, 11 + dl, 15 + dl)], "I", "i", "o")
        mass(g, [(44 - ll, 9 + dl, 15 + dl), (46 - ll, 8 + dl, 15 + dl)], "J", "j", "o")
        mass(g, [(38, 25, 29), (44 - lr, 27 + dr, 31 + dr)], "I", "i", "o", rim=False)
        mass(g, [(44 - lr, 26 + dr, 32 + dr), (46 - lr, 26 + dr, 33 + dr)], "J", "j", "o",
             rim=False)
        g.set(9 + dl, 45 - ll, "N")
        g.set(27 + dr, 45 - lr, "N")

    # THE ROBE: a wedge. Broad at the shoulders, narrowing to a ragged hem above the knees.
    # Walking, the hem swings a pixel with the step; everything above row 37 is the pose.
    b = 0 if phase is None else int(round(c))
    mass(g, [(18, 13, 29), (21, 9, 32), (25, 9, 32), (31, 11, 31), (37, 12, 30),
             (40, 13 + b, 29 + b)], mid, lit, dark)
    fold(g, [(13, 23), (14, 31), (15 + b, 39)], "*", "%")
    fold(g, [(19, 29), (20, 35), (19 + b, 40)], "*", "^")
    fold(g, [(27, 28), (26, 34), (27 + b, 40)], "*", "^")
    # Torn and earth-stained at the hem: tongues of uneven length, not a straight edge.
    tatters(g, [(41, 13 + b, 16 + b), (42, 14 + b, 15 + b), (41, 19 + b, 23 + b),
                (42, 20 + b, 22 + b), (43, 21 + b, 21 + b), (41, 26 + b, 28 + b)],
            "*", "&", "o")
    cut(g, (17 + b, 40), (18 + b, 40), (24 + b, 40), (25 + b, 40))
    # A cord belt with a stone for a buckle.
    g.span(32, 11, 31, "*")
    g.span(31, 11, 31, "&")
    g.rect(15, 31, 17, 33, "N")
    g.set(15, 31, "P")
    g.set(17, 33, "J")

    # THE PAULDRONS: rough stones, each smaller than his head, lit on the key side. The left one
    # catches the light; the right one turns away from it but still carries a lit top edge, so
    # neither is a hole in the figure.
    mass(g, [(18, 9, 12), (19, 7, 14), (21, 6, 14), (23, 7, 13)], "p", "P", "N")
    g.line(7, 24, 13, 24, "o")
    g.set(11, 20, "j")
    g.set(12, 21, "j")
    g.set(8, 19, "P")
    mass(g, [(19, 29, 32), (20, 28, 34), (22, 28, 35), (24, 29, 34)], "N", "p", "j", rim=False)
    g.line(29, 19, 32, 19, "p")
    g.line(28, 25, 34, 25, "o")
    g.set(31, 21, "J")

    # THE SPIKE, driven point-down in front of him. Black glass: stone deep with a lit facet
    # down its key side and a rim outside that, which is the only thing that keeps obsidian
    # visible on a dark floor - and a broken crown of shards above his hands.
    for i, y in enumerate(range(22, 47)):
        hw = 3.4 * (1.0 - i / 25.0) + 0.4
        lo, hi = int(round(37.5 - hw)), int(round(37.5 + hw))
        if g.get(lo - 1, y) == ".":
            g.set(lo - 1, y, "W")
        g.span(y, lo, hi, "J")
        g.set(lo, y, "P" if i % 5 == 0 else "p")
        if hi - lo >= 3:
            g.set(lo + 1, y, "p" if i % 3 else "N")
            g.set(lo + 2, y, "N" if hi - lo >= 5 else "J")
            g.set(hi - 1, y, "j")
        g.set(hi, y, "o")
    g.line(36, 34, 39, 27, "o")                                    # a fracture through the glass
    g.line(35, 34, 38, 27, "P")
    for (x0, y0, x1, y1, c) in ((35, 22, 34, 16, "p"), (36, 22, 36, 14, "N"),
                                (38, 22, 39, 15, "J"), (40, 22, 42, 17, "j"),
                                (37, 21, 37, 17, "J")):
        g.line(x0, y0, x1, y1, c)
    for (wx, wy) in ((33, 16), (35, 14), (35, 15)):
        if g.get(wx, wy) == ".":
            g.set(wx, wy, "W")
    g.set(34, 16, "P")
    g.set(36, 14, "P")
    g.set(39, 15, "p")
    g.set(37, 19, "|")                                             # the one hot pixel in the rock
    # The ground it went into, split and lit from underneath: the crack runs out both ways.
    for (cx2, cy2, cc) in ((33, 46, "\\"), (34, 46, "|"), (35, 45, "}"), (36, 46, "|"),
                           (39, 46, "|"), (40, 45, "}"), (41, 46, "\\"), (42, 46, "|"),
                           (32, 45, "N"), (31, 46, "j"), (43, 45, "N"), (44, 46, "J"),
                           (42, 44, "j")):
        g.set(cx2, cy2, cc)

    # BOTH HANDS ON IT: the right arm reaches down from its pauldron, the left all the way across
    # his chest, with an occ line under the sleeve so the arm sits on the robe.
    shaft(g, 13, 26, 34, 23, "^", "&", "*", width=3)
    g.line(13, 29, 34, 26, "o")
    shaft(g, 32, 24, 35, 22, "^", "&", "*", width=3)
    hand(g, 34, 21)
    hand(g, 36, 22)

    # THE HOOD, cut back to frame the face rather than swallow it.
    mass(g, [(12, 18, 23), (13, 16, 25), (15, 15, 26), (19, 15, 26)], "&", "^", "*")
    g.line(16, 14, 18, 13, "%")
    # THE FACE: forward of the shoulders and in the light. A ledge of rock for a brow, two rows
    # of it on the key side only, so it overhangs without shutting the eyes.
    head(g, 21, 15, 3.6, brow="low", gaze=1, mouth="set", eye="narrow")
    g.span(14, 17, 21, "N")
    g.span(13, 18, 20, "p")
    g.set(17, 14, "P")
    moustache(g, 21, 20, 2.6, "N", "p", "J")
    hair(g, 21, 21, 27, 3.4, 2.4, "N", "p", "J", parted=True)
    for (mx, my) in ((19, 23), (23, 25), (18, 26)):
        g.set(mx, my, "%")
    contact(g, 7, 44)
    return g.rows()


def tituba(phase=None):
    """Riptide. Tituba, taken from Barbados and jailed at Salem on an accusation.

    Upright and graceful, and the only figure in the cast with a WAIST: a tall knotted head-wrap,
    a shawl over the shoulders falling in two tails, a narrow bodice and a skirt that flares out
    like a bell. Every other figure is a column or a cone; hers is an hourglass. The driftwood
    staff is swept out to her right side at a slant, held at arm's length, its breaking wave out
    over the water rather than over her head - the sea is to one side of her, not above.

    Level gaze, mouth set: the one person in the cast who has already been through a trial and is
    not afraid. No hood - a head-wrap - deep skin via head(tone="deep"), a gold earring.
    """
    mid, lit, dark = ROBES["tituba"]
    g = G(48)
    CX = 26
    # THE SKIRT: a bell, not a cone. It widens fast below the waist and then falls almost
    # straight, and the hem kicks out to one side.
    mass(g, [(29, 23, 29), (31, 21, 31), (34, 19, 33), (38, 17, 35), (42, 16, 36), (45, 15, 37),
             (46, 14, 38)], mid, lit, dark)
    fold(g, [(24, 31), (20, 39), (18, 46)], dark, lit)
    fold(g, [(26, 32), (25, 39), (24, 46)], dark, lit)
    fold(g, [(28, 32), (31, 40), (33, 46)], dark, lit)
    fold(g, [(22, 41), (21, 46)], "X", None)
    tatters(g, [(46, 15, 19), (46, 27, 31)], "X", "x", "o")
    g.set(36, 44, "X")
    if phase is not None:
        # The bell swings side to side from the waist, out toward the leading foot, and the
        # toe of that foot shows at the hem. The staff is drawn after, so it stays planted.
        c, _ln, _lf = stride(phase)
        hem = 2.5 * c
        sway(g, 33, hem)
        if abs(c) > 0.5:
            toe(g, (29 if c > 0 else 21) + int(round(hem)), w=3, shoe=("h", "i", "I"))

    # THE BODICE, narrow.
    mass(g, [(19, 22, 30), (23, 23, 29), (28, 23, 29)], mid, lit, dark)
    # A sash at the waist, gold-dark, the one worked thing she still has.
    g.span(28, 22, 30, "u")
    g.span(29, 22, 30, "g")
    g.set(23, 29, "G")
    g.set(29, 28, "o")

    # THE SHAWL: linen, over both shoulders, knotted at the chest, two tails of unequal length.
    mass(g, [(19, 21, 31), (21, 20, 32), (24, 21, 31), (25, 22, 30)], "e", "l", "E")
    fold(g, [(24, 19), (26, 23)], "E", "l")
    fold(g, [(29, 20), (27, 23)], "A", "e")
    g.rect(25, 24, 27, 26, "l")
    g.set(26, 25, "A")
    shaft(g, 23, 26, 21, 34, "e", "E", "A")
    shaft(g, 28, 26, 32, 33, "l", "e", "E", width=3)
    g.set(32, 34, "E")

    # THE STAFF, swept out to her side: driftwood, knotted, bleached linen. NOT an element ramp -
    # the idle pulse brightens every element pixel, and only the wave on top gives off light.
    shaft(g, 19, 45, 9, 13, "l", "e", "E", width=3)
    for (kx, ky) in ((17, 38), (14, 27), (12, 20)):
        g.span(ky, kx - 1, kx + 2, "E")
        g.set(kx - 1, ky, "L")
    # The wave: the crest thrown out away from her, a dark barrel under the lip, and NO air
    # inside it. Drawn as a map because the shape IS the read, and it took four passes: built
    # from columns it was a blue gem; with the hollow left open it was a thin hook on a stem and
    # read as a question mark, then as a crescent moon, and at game size any hole at all brought
    # the question mark back. Water is a MASS with a curl on it - the curl is carried by the
    # foam and the dark barrel, and the silhouette only keeps the notch under the lip.
    WAVE = ["....0999....",
            "..990KKK9...",
            ".9KK0MKKKK9.",
            "9K0MMMMMKKK.",
            "K0MMMMMMKKKM",
            "0M.MMMMKKKKM",
            "....MKKKKKM.",
            "..99KKKKKM..",
            ".9KKKKKMM...",
            "MKKKKKMM....",
            "..MMMM......"]
    for wy, row in enumerate(WAVE):
        for wx, ch in enumerate(row):
            g.set(4 + wx, 2 + wy, ch)
    for (fx, fy) in ((15, 3), (16, 6)):
        g.set(fx, fy, "9")
    # The arm that holds it, out from the shoulder.
    shaft(g, 21, 20, 16, 26, "v", "w", "x", width=3)
    g.line(21, 22, 17, 27, "o")
    hand(g, 14, 25, tone="deep")

    # THE HEAD-WRAP: tall, folded, the knot at the top front, lighter than the dress so the head
    # separates from the body.
    mass(g, [(4, 25, 28), (6, 23, 30), (9, 22, 30), (12, 21, 31)], "v", "V", "x")
    fold(g, [(22, 11), (29, 6)], "x", "V", side=(0, -1))
    fold(g, [(24, 12), (30, 9)], "X", "v", side=(0, -1))
    g.disc(24.5, 4.0, 1.3, "w")
    g.set(24, 3, "V")
    g.set(22, 2, "v")
    g.set(23, 3, "v")
    g.set(21, 3, "x")
    head(g, CX, 12, 3.2, brow="flat", gaze=0, mouth="set", eye="open", tone="deep")
    g.span(18, 25, 27, "I")                                        # the neck, in shadow
    g.set(22, 16, "H")                                              # a gold earring
    g.set(22, 17, "G")
    contact(g, 14, 38)
    return g.rows()


def vainamoinen(phase=None):
    """Bramble Seed. Vainamoinen, the old singer of the Kalevala, who sowed the first forests.

    So he is the SOWER, caught mid-stride: one foot out in front, the robe opened by the step,
    his free hand flung out to the side and seed falling from it - the only figure in the cast
    with a hand out and empty. The reed staff is in his OTHER hand, on the left of the sprite,
    because the sowing hand is the one that matters.

    Tallest and oldest with the Frostweaver: a tall felt hat with its point bent over by the wind,
    a white beard to the belt swept the same way, mouth open because he is singing. The kantele -
    the harp he made from a pike's jaw - slung at his hip, strings picked out in gold.
    """
    mid, lit, dark = ROBES["vainamoinen"]
    g = G(48)
    # THE BOOTS, under the robe: the back one flat, the front one out ahead.
    if phase is None:
        mass(g, [(44, 14, 18), (46, 13, 19)], "m", "n", "o", rim=False)
        mass(g, [(44, 31, 35), (45, 31, 37), (46, 32, 38)], "m", "n", "o", rim=False)

        # THE ROBE, opened by the stride. The front panel swings forward with the leading foot
        # and a gap opens between the legs at the hem.
        mass(g, [(18, 20, 28), (26, 18, 30), (36, 16, 32), (42, 15, 34), (44, 15, 35)],
             mid, lit, dark)
        for y in range(40, 45):
            h = (y - 40) * 0.7
            cut(g, *[(x, y) for x in range(int(round(24 - h)), int(round(25 + h)) + 1)])
        g.line(23, 41, 21, 44, "a")
        g.line(26, 41, 28, 44, "a")
    else:
        # Walking, the boots trade places under the robe, and the robe follows them: its hem
        # spreads to the two feet at each contact, and at passing it closes and falls straight,
        # the lifted boot hidden inside it.
        c, lift_near, lift_far = stride(phase)
        front, back = 24.5 + 9.0 * c, 24.5 - 9.0 * c
        for (bx, lift) in ((back, lift_far), (front, lift_near)):
            l = int(round(2 * lift))
            mass(g, [(44 - l, bx - 2.5, bx + 1.5), (45 - l, bx - 2.5, bx + 3.5),
                     (46 - l, bx - 1.5, bx + 4.5)], "m", "n", "o", rim=False)
        lo = min(16, min(front, back) - 1)
        hi = max(32, max(front, back) + 1.5)
        mass(g, [(18, 20, 28), (26, 18, 30), (36, 16, 32), (42, lo + 0.5, hi - 1), (44, lo, hi)],
             mid, lit, dark)
        k = abs(c)
        if k > 0.3:
            for y in range(40, 45):
                h = (y - 40) * 0.7 * k
                cut(g, *[(x, y) for x in range(int(round(24 - h)), int(round(25 + h)) + 1)])
            g.line(23, 41, 24 - 3 * k, 44, "a")
            g.line(26, 41, 25 + 3 * k, 44, "a")
    fold(g, [(22, 22), (20, 32), (18, 42)], dark, lit)
    fold(g, [(25, 26), (28, 35), (31, 43)], dark, lit)
    fold(g, [(26, 36), (24, 39)], "a", None)
    g.span(30, 19, 30, "a")                                        # the belt, a cord
    g.set(20, 30, "d")
    g.line(27, 31, 27, 34, "a")
    g.set(27, 34, "D")

    # THE SOWING ARM: a wide sleeve hanging under it, the hand open at the end.
    mass(g, [(20, 28, 31), (22, 28, 35), (24, 29, 36), (26, 31, 35), (28, 32, 33)],
         mid, lit, dark, rim=False)
    g.line(29, 23, 34, 23, "b")
    g.line(29, 25, 33, 27, "o")
    for (fx, fy, fc) in ((37, 22, "f"), (38, 22, "h"), (39, 23, "h"), (37, 23, "h"),
                         (38, 23, "i"), (40, 24, "i"), (39, 21, "f")):
        g.set(fx, fy, fc)
    # The seed, falling and spreading. Grass ramp - it is the spell, so it is allowed to glow.
    for (sx2, sy2, sc) in ((38, 26, ","), (40, 28, "'"), (37, 30, "B"), (41, 32, ","),
                           (39, 35, "`"), (42, 37, "'"), (38, 39, ",")):
        g.set(sx2, sy2, sc)

    # THE KANTELE at his hip: a gold-dark trapezoid with strings across it.
    g.rect(16, 31, 21, 37, "g")
    g.line(16, 31, 21, 31, "G")
    g.line(16, 37, 21, 37, "u")
    g.line(16, 31, 16, 37, "G")
    for k in range(4):
        g.line(17 + k, 32, 17 + k, 36, "H" if k % 2 == 0 else "u")
    g.line(22, 31, 22, 37, "o")

    # THE STAFF, in the left hand: bound reeds dried to linen, leaning forward with the stride.
    shaft(g, 11, 46, 15, 9, "l", "e", "E", width=3)
    for y in (20, 29, 38):
        x = 11 + 4 * (46 - y) / 37.0
        g.span(y, x - 1, x + 2, "u")
    # Only the living tops are grass, so only they breathe with the idle (see RAMPS).
    for (tx, ty) in ((11, 4), (14, 2), (17, 3), (19, 5)):
        g.line(15, 9, tx, ty, "'")
        g.set(tx, ty, "B")
        g.set(tx, ty + 1, ",")
    g.disc(15, 8, 1.2, "'")
    g.set(14, 7, "B")
    for (dx, dy0) in ((11, 6), (19, 7)):
        g.line(dx, dy0, dx, dy0 + 2, "`")
        g.set(dx, dy0 + 3, ",")
    shaft(g, 20, 20, 16, 25, "d", "c", "b", width=3)
    g.line(21, 22, 17, 27, "o")
    hand(g, 14, 24)

    # THE HAT: tall felt, the point bent over by the wind and hanging.
    mass(g, [(4, 23, 24), (6, 22, 25), (8, 21, 26), (10, 20, 27)], "b", "c", "a")
    g.span(11, 19, 29, "a")
    g.span(11, 19, 22, "b")
    g.set(19, 11, "c")
    g.line(25, 4, 27, 3, "b")
    g.line(27, 3, 29, 5, "a")
    g.set(29, 6, "a")
    g.set(23, 4, "c")
    fold(g, [(22, 9), (26, 7)], "a", "d", side=(0, -1))
    head(g, 24, 12, 3.4, brow="raised", gaze=1, mouth="open", eye="open")
    moustache(g, 24, 17, 2.8, "k", "S", "n")
    hair(g, 24, 18, 35, 3.4, 1.2, "k", "S", "n", sweep=3.0, tip="S", parted=True)
    contact(g, 10, 39)
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
           ("stormcaller", stormcaller), ("geomancer", geomancer),
           ("tituba", tituba), ("vainamoinen", vainamoinen)]
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
# THE EMISSIVE RAMPS, and ONLY those. This table decides what the idle pulse brightens, so
# anything listed here is something the figure is meant to be GIVING OFF LIGHT with.
#
# It used to list the four player pigment rows - ember, azure, amber, verdant - which are the
# wizards' ROBE CLOTH, and to omit four of the six element ramps entirely. So the idle stepped
# every thread of a wizard's robe one stop brighter and left his staff head alone: 70% of the
# figure's pixels changed between frame 0 and frame 1, which is not a breath, it is a strobe. The
# docstring below always said "step the emissive one stop up"; the table simply did not agree
# with it.
#
# Cloth does not glow. A wizard's own eyes do not glow either - he is a person, not something the
# dark wizard took - so on the player cast the only thing left in here is the staff head, which is
# exactly the intent.
RAMPS = [
    "+-=~",       # fire
    "<>?/",       # ice
    "[];:",       # lightning
    "{}|\\",      # earth
    "ZYyz",       # arcane
    "()_",        # poison
    "09KM",       # water - Tituba's wave
    "B,'`",       # grass - Vainamoinen's sprouting reeds, and the seed falling from his hand
]
_STEP = {}
for _r in RAMPS:
    for _i, _c in enumerate(_r):
        _STEP[_c] = _r[max(0, _i - 1)]          # one step BRIGHTER, clamped at the core

IDLE_DY = [0, -1, -1, 0]

# Where the bob stops. Everything above this row rises; everything below it - hem, feet, contact
# shadow - stays exactly where it is.
#
# This is a WIZARD_CELL row. idle() scales it to whatever cell it is handed, because main() also
# runs the 32px enemies through here and a 48px row indexed straight off the end of them.
IDLE_ANCHOR = 38


def idle(rows, anchor=None):
    """Four frames from one pose: breathe, and step the emissive one stop up.

    THE BOB ONLY MOVES THE TOP OF THE FIGURE. It used to translate the whole cell, feet and
    contact shadow included, which section 5 of the contract forbids in as many words - "a figure
    that bobs must bob its head, not its feet" - and which reads as the whole sprite jittering
    rather than as somebody breathing. Now the rows above IDLE_ANCHOR rise by one and the rows
    below hold, so the figure compresses very slightly at the waist and its feet never leave the
    floor.

    The seam duplicates one row of robe, which is invisible on cloth and is the cheapest way to
    absorb the pixel: the alternative is a one-pixel gap, and a gap is a hole.

    `anchor` defaults to IDLE_ANCHOR scaled by integer division, which is exactly 38 on the
    wizard cell - so the shipped player art does not move - and 25 on the enemy cell.
    """
    cell = len(rows)
    if anchor is None:
        anchor = IDLE_ANCHOR * cell // WIZARD_CELL
    out = []
    for f in range(4):
        dy = IDLE_DY[f]
        r = list(rows)
        if dy:
            lift = -dy
            r = [rows[min(y + lift, anchor)] if y < anchor else rows[y]
                 for y in range(cell)]
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
