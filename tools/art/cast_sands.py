# -*- coding: utf-8 -*-
"""The Scorched Sands' own cast (.ai/bestiary.md section 4.7). Run: python tools/art/cast_sands.py

"He hid the water. The dead still walk the trade road." The porters, guards and travellers of a
caravan that never reached water, and what feeds on them. Eleven creatures, every one a new look on
the numbers of the Cursed Dungeon creature it replaces (bestiary.md section 2); the module contract
is bestiary_kit.py's docstring.

THE SAND CLOTH ROW. Desert wrappings drawn in the grey-green `linen` row only read as sun-bleached
under the per-stage light shader (art-direction.md section 7), which is not built. Eric's decision
(2026-10-24, bestiary.md section 10 item 6) was one warm cloth row for this chapter, so the
wrapped travellers read warm today. It is a five-tone row built to the contract's rules - `shade`
rotated about -20 degrees toward indigo with its chroma held, `deep` still chromatic, nothing under
0.20 saturation - and it is recorded in art-direction.md section 3 beside the others. It lives HERE,
not in bonelight.py, because only this chapter wears it.

WHY IT DOES NOT CAMOUFLAGE. The Sands floor is the gold row's deep end (#2E2208, speckled with its
shade and base). A warm cloth on a warm floor loses the hue contrast every cool creature gets for
free, so the cloth has to win on VALUE: its mass is the row's base (#7C6040), three times the
floor's lightness, and the deep stop is kept for crevices inside the figure and never put on an
outline, where it would sit within a few points of the ground.

CHARACTERS. roster.PALETTE uses every printable character, so this chapter re-points three rows
that no enemy may ever wear - the PLAYER PIGMENT rows - at the colours it needs:

    Q 1 2 3 4   ember  -> cloth red  (art-direction.md section 3; roster.PALETTE never mapped it)
    O ! @ # $   amber  -> sand cloth (the new row)
    U 5 6 7 8   azure  -> the wind element ramp (core, hot, mid, edge, edge), the Oracle's bead

Re-pointing rather than inventing characters matters for the animation: anim_sets.RAMPS already
lists those three strings as five-stop ramps, lightest first, so the derived hurt flash and death
fade step these colours along their own rows exactly as they step steel or linen. A new character
outside RAMPS would stay full-bright through a death while the figure went out round it (the
lesson anim_sets.PLAYER_RAMPS records for water and grass). None of the pigment colours appears.

THE MARK. Everything here was TAKEN except the Bone Ballista, which he BUILT from the caravan's
bones: two pixels of cold arcane on the taken, fire in the Ballista's sight (world-and-tone.md).
"""
from __future__ import print_function

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bestiary_kit
import bonelight as B
import roster
import sprite_bosses as SB
from foes_forest import _limb

G = roster.G
FEET = 30

# --- the palette ---------------------------------------------------------------------------------
# The sand cloth row: hi / lit / base / shade / deep. Hue 46 -> 36 -> 32 -> 13 -> 344 degrees (shade
# rotated 19 toward indigo); HSL saturation 0.40 / 0.29 / 0.32 / 0.29 / 0.27; lightness 0.75 / 0.52 /
# 0.37 / 0.25 / 0.16. Pitched so a body in its base with a lit key edge sits where a steel body does
# (steel shade 0.31, base 0.51). A first cut one stop lighter (base #9A7A54) made the Thirsting the
# brightest thing in a crowd after the player - an enemy body reaching for the bright end, which
# art-direction.md section 4c forbids because the player's light has to win.
SAND = ["#D8CCA4", "#A88C62", "#7C6040", "#54362E", "#341E24"]

PALETTE = dict(roster.PALETTE)
for _ch, _col in zip("Q1234", B.MATERIALS["red"]):
    PALETTE[_ch] = _col
for _ch, _col in zip("O!@#$", SAND):
    PALETTE[_ch] = _col
for _ch, _col in zip("U5678", B.ELEMENTS["wind"] + [B.ELEMENTS["wind"][3]]):
    PALETTE[_ch] = _col

# Material shorthands, (mid, lit, dark) as body()/mass() take them, plus the deep stop.
SANDC = ("@", "!", "#")
LINEN = ("E", "e", "A")
STEEL = ("R", "r", "q")
STONE = ("N", "p", "j")
BONE = ("k", "s", "n")
RED = ("2", "1", "3")


def blob(g, discs, mid, lit, dark, rim=True):
    """Discs unioned into one mass, then lit by its own outline: the upper-left edge takes `lit`,
    the lower-right `dark`, the inside `mid`, and the rim goes on the key side only.

    roster.mass draws any outline row by row from keyframes, which is right for a figure standing
    up. A hunched back, a bowed porter or a swollen belly is a curve in both directions, and a set
    of overlapping discs gets there without a keyframe per row. Lighting it from the outline after
    the union means two discs never leave a seam of lit pixels where they overlap.
    """
    inside = set()
    for (cx, cy, r) in discs:
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.3 and 0 <= x < g.cell and 0 <= y < g.cell:
                    inside.add((x, y))
    for (x, y) in sorted(inside):
        if (x, y - 1) not in inside or (x - 1, y) not in inside:
            ch = lit
        elif (x, y + 1) not in inside or (x + 1, y) not in inside:
            ch = dark
        else:
            ch = mid
        g.set(x, y, ch)
    if rim:
        for (x, y) in sorted(inside):
            if (x - 1, y) not in inside and g.get(x - 1, y) == ".":
                g.set(x - 1, y, "W")
    return inside


def chain(g, pts, a="r", b="q"):
    """Links in alternating tones along a polyline: a chain in one tone is a wire."""
    k = 0
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + (1 if (x1, y1) == pts[-1] else 0)):
            t = i / float(max(1, n))
            g.set(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, a if k % 2 == 0 else b)
            k += 1


# =================================================================================================
# BULK - The Thirsting. A traveller from the caravan, wrapped head to foot against the sun.
# =================================================================================================

def thirsting():
    """Wrapped head to foot, and the one thing it still carries is empty.

    The attachment is the WATERSKIN: a slack red-leather bag hung off a strap at the front hip,
    sagging below the belt line and drooping at the spout, so filled black the figure is a narrow
    wrapped column with a teardrop hanging off one side. The wrapping is diagonal bands - a cloth
    wound round a body, not a robe hung on one - and the end of the head cloth trails back off
    the shoulder with daylight between it and the back.
    """
    g = G(32)
    # The head cloth's loose end, first, so the body lands over its root.
    for i in range(9):
        t = i / 8.0
        x = 12.0 - 4.2 * t
        y = 7.0 + 8.5 * t
        g.span(y, x - 1.0, x + 0.4, "#")
        g.set(x - 1.0, y, "@")
    g.set(7, 16, "#")
    g.set(8, 17, "$")

    roster.mass(g, [(4, 14, 17), (5, 13, 18), (7, 12, 19), (11, 12, 19), (13, 12, 18),
                    (15, 11, 20), (22, 10, 21), (29, 9, 21)], "@", "!", "#")

    # The head wrap: turns of cloth, each a light stop over a dark one, not parallel.
    roster.fold(g, [(12, 7), (19, 5)], "#", "!", side=(0, -1))
    roster.fold(g, [(13, 9), (19, 8)], "$", "!", side=(0, -1))
    g.set(16, 4, "!")
    g.set(15, 5, "O")
    # The face: a slit in the wrap, wider at the front.
    g.span(10, 15, 18, "o")
    g.span(11, 16, 18, "o")
    g.set(19, 10, "#")
    # The veil under it.
    roster.fold(g, [(13, 13), (18, 12)], "$", "!", side=(0, -1))

    # Body wraps, wound diagonally and at uneven spacing.
    roster.fold(g, [(11, 15), (20, 18)], "$", "!", side=(0, -1))
    roster.fold(g, [(10, 20), (17, 22), (21, 22)], "#", "!", side=(0, -1))
    roster.fold(g, [(10, 25), (16, 27)], "$", "!", side=(0, -1))
    roster.fold(g, [(14, 23), (13, 29)], "$", "!", side=(-1, 0))
    roster.fold(g, [(18, 24), (19, 29)], "#", None)

    # The far arm, a sleeve hanging close, and the near one bent at the elbow, wrapped.
    g.line(12, 16, 11, 21, "$")
    g.line(19, 15, 20, 20, "o")
    g.line(20, 15, 21, 20, "#")
    g.set(21, 15, "!")
    g.set(21, 21, "k")
    g.set(22, 21, "n")

    # The strap, shoulder to hip.
    g.line(12, 14, 19, 21, "3")
    g.line(12, 13, 19, 20, "4")

    # THE WATERSKIN: slack, hanging off the hip, sagging lower than the belt it is tied to.
    skin = [(21, 20, 22), (22, 19, 23), (23, 19, 24), (24, 19, 24), (25, 20, 24), (26, 20, 23),
            (27, 21, 23)]
    for (y, a, b) in skin:
        g.span(y, a, b, "2")
        g.set(a, y, "1")
        g.set(b, y, "3")
        g.set(a - 1, y, "o")
    for (x, y) in ((21, 22), (22, 23), (23, 25), (21, 25), (22, 26)):
        g.set(x, y, "4")
    g.set(20, 22, "Q")
    g.set(21, 21, "Q")
    g.set(20, 23, "1")
    g.set(22, 28, "3")
    g.set(23, 29, "4")
    g.set(24, 22, "W")
    g.set(25, 23, "W")

    # The hem, torn, and two wrapped feet under it.
    roster.cut(g, (9, 29), (15, 29), (16, 29))
    roster.tatters(g, [(28, 9, 12), (29, 10, 13), (29, 17, 20)], "#", "@", "$")
    g.span(30, 11, 13, "$")
    g.set(11, 30, "#")
    g.span(30, 17, 20, "#")
    g.set(17, 30, "@")
    roster.contact(g, 10, 22)
    roster.eyes(g, 10, 16, 18, "Y", "y")
    return g.rows()


# =================================================================================================
# FAST - Bone Vulture. Also the Carrion Caller's minion.
# =================================================================================================

def _wing(g, root, tip, rise, depth, fingers, side):
    """One wing, hung from a bone leading edge, with separated primaries at the tip.

    `root` and `tip` are the ends of the leading edge, which bows upward by `rise`; `depth` is how
    far the feathers hang at the root, tapering toward the tip; the outer `fingers` columns are the
    primaries, drawn every other column with daylight between - the splayed fingers are what say
    vulture and not bat. `side` is -1 for the wing on the left of the body, +1 on the right.
    """
    (rx, ry), (tx, ty) = root, tip
    n = abs(tx - rx)
    for i in range(n + 1):
        t = i / float(n)
        x = rx + side * i
        y = int(round(ry + (ty - ry) * t - rise * 4.0 * t * (1.0 - t)))
        outer = i > n - fingers
        if outer and (n - i) % 2 == 1:
            d = 2                                   # the gap between two primaries: coverts only
        elif outer:
            d = int(round(depth * 0.95 + 0.5))      # a primary, hanging long
        else:
            d = int(round(depth * (1.0 - 0.35 * t)))
            d += (0, 1, 0, -1, 1, 0, 0)[i % 7]       # a ragged trailing edge, never a ruler
        for k in range(1, d + 1):
            if k <= 2:
                ch = "e" if k == 1 else "E"
            elif k == d:
                ch = "A"
            else:
                ch = "J" if (i % 3 == 1 and not outer) else ("E" if (i + k) % 4 else "A")
            g.set(x, y + k, ch)
        if d > 3 and not outer:
            g.set(x, y + d + 1, "o") if i % 2 else None
        # the bone along the edge, bare: lit on top, shade under
        g.set(x, y, "s" if i % 5 else "S")
        if i < n - fingers + 2:
            g.set(x, y + 1, "k" if i % 3 else "n")


def vulture():
    """Broad ragged wings, bare bone through them, and nothing touching the ground.

    The Fast slot and the Carrion Caller's minion, so this is the one shape a flock is made of:
    seen head-on, banking - a shallow V of wings two thirds of the cell wide, the near one higher,
    each ending in splayed primaries, with a small dark body slung under the join and a bald bone
    head with a pale ruff standing up between the wings. Bone runs along both leading edges where
    the feathers have gone, and through the middle of the near wing as one bare strut.
    """
    g = G(32)
    _wing(g, (13, 13), (3, 11), 2.4, 6.0, 5, -1)
    _wing(g, (19, 12), (29, 7), 2.0, 6.4, 5, +1)
    # bare bone through the near wing: one strut across the feathers
    g.line(21, 14, 25, 12, "n")
    g.line(21, 15, 24, 14, "m")
    g.set(9, 14, "n")
    g.set(10, 14, "m")

    # THE BODY: a small dark teardrop under the join, the tail fanned below it.
    blob(g, [(16, 15.5, 3.2), (16, 18.5, 2.8), (16, 21, 1.8)], "E", "e", "A")
    roster.fold(g, [(15, 16), (15, 21)], "A", "e")
    roster.fold(g, [(17, 17), (17, 21)], "J", None)
    roster.tatters(g, [(23, 14, 18), (24, 13, 15), (24, 17, 19), (25, 14, 14), (25, 18, 18)],
                   "A", "E", "o")
    # talons, drawn up under the tail
    g.set(14, 22, "n")
    g.set(13, 23, "m")
    g.set(18, 22, "n")
    g.set(19, 23, "m")

    # THE RUFF, pale, and the bald head standing up out of it.
    g.span(12, 13, 19, "l")
    g.span(11, 14, 18, "e")
    g.set(13, 12, "L")
    g.set(19, 12, "E")
    g.set(16, 13, "A")
    g.disc(16.0, 8.6, 2.1, "k")
    g.set(15, 7, "s")
    g.set(14, 8, "s")
    g.set(15, 6, "S")
    g.set(18, 9, "n")
    g.set(17, 10, "n")
    g.set(16, 11, "n")
    # the hooked beak, down and forward
    g.set(17, 10, "m")
    g.set(18, 10, "n")
    g.set(18, 11, "m")
    g.set(15, 10, "o")

    # The shadow, well clear of everything above it.
    g.span(FEET, 10, 21, "o")
    g.span(FEET + 1, 12, 19, "o")
    g.set(15, 8, "Y")
    g.set(17, 8, "y")
    return g.rows()


# =================================================================================================
# HEAVY - Laden Porter. Also the caravan master (CaravanMasterEvent asks for this chapter's Heavy).
# =================================================================================================

def porter():
    """Bent double under a bale-frame bigger than he is.

    The shield plane is the FRAME: two straight poles and a load, carried on the back so it stands
    to one side of him as a tall flat rectangle - the shielder's straight-edged plane against a
    curved body, without being anything like a shield. He is drawn in the sand cloth, bowed so far
    his head hangs below the top of his own load, and the tumpline runs from his brow back to it.
    """
    g = G(32)
    # THE FRAME: poles, then the bale between them, then the bindings.
    g.rect(6, 6, 12, 22, "2")
    for y in range(6, 23):
        g.set(6, y, "1")
        g.set(7, y, "1") if y % 5 == 2 else None
        g.set(12, y, "3")
    for (y, a, b) in ((10, 6, 12), (16, 6, 12)):
        g.span(y, a, b, "4")
        g.span(y - 1, a, b, "Q")
    roster.fold(g, [(9, 7), (8, 21)], "3", "1")
    roster.fold(g, [(11, 11), (10, 21)], "4", None)
    # the rope that lashes it, crossing once
    g.line(6, 12, 12, 19, "#")
    g.line(6, 13, 12, 20, "@")
    # a rolled rug across the top, overhanging the poles
    g.span(4, 5, 13, "@")
    g.span(5, 4, 14, "#")
    g.set(4, 4, "!")
    g.set(5, 4, "!")
    g.set(14, 4, "$")
    g.set(10, 4, "$")
    g.set(10, 5, "$")
    for x in (4, 13):
        g.line(x, 6, x, 26, "p")
        g.line(x + 1, 6, x + 1, 26, "j")
        g.set(x, 27, "J")
    g.span(23, 4, 14, "j")
    g.span(22, 5, 13, "p")
    g.set(4, 5, "P")

    # THE PORTER. Legs first: the back one straight and braced, the front one bent.
    roster.leg(g, (17, 21), (15, 28), 9, ("#", "@", "$"), ("$", "#", "o"), hw=(1.8, 1.5, 1.1))
    roster.leg(g, (19, 21), (21, 28), 8, ("@", "!", "#"), ("#", "@", "o"), hw=(1.8, 1.6, 1.2))
    # The bowed back: a curve from the hips up and over to the shoulders.
    blob(g, [(16.5, 19.5, 2.6), (17.5, 17.0, 2.9), (19.5, 15.4, 2.8), (21.5, 15.6, 2.4)],
         "@", "!", "#", rim=False)
    roster.fold(g, [(16, 20), (21, 14)], "#", "!", side=(0, -1))
    roster.fold(g, [(18, 19), (22, 17)], "$", None)
    g.span(21, 15, 20, "3")                      # a sash at the hip
    g.set(15, 21, "2")
    # the straps to the frame
    g.line(14, 15, 18, 16, "4")
    g.line(14, 18, 17, 20, "4")
    # the arm, hanging forward under the body to a fist
    g.line(22, 17, 23, 22, "#")
    g.line(23, 17, 24, 22, "$")
    g.set(22, 23, "k")
    g.set(23, 23, "n")
    # THE HEAD, hanging forward and down, hooded, the face turned to the ground. Its void opens
    # DOWNWARD: with the eyes on the front of the head, the first pass read as an animal's snout.
    blob(g, [(23.5, 17.0, 2.5), (24.5, 18.5, 1.6)], "@", "!", "#", rim=False)
    g.span(19, 22, 25, "o")
    g.span(20, 23, 24, "o")
    g.set(25, 18, "$")
    g.set(22, 15, "!")
    roster.fold(g, [(21, 16), (25, 17)], "$", "!", side=(0, -1))
    # the tumpline: a short band over the brow, its ends tied into the straps, not a bowstring
    g.line(20, 14, 18, 15, "4")
    roster.contact(g, 4, 24)
    g.set(23, 19, "Y")
    g.set(25, 19, "y")
    return g.rows()


# =================================================================================================
# WALL - Caravan Guard. A caravan's hired spear, broad.
# =================================================================================================

def caravan_guard():
    """Broad, crested, and the one upright line in the chapter that is a weapon.

    The bruiser's attachment, done the desert's way: a helm with a horsehair crest bleached the
    colour of the cloth, running front to back and falling down the nape, and a lamellar mantle
    that flares out past the arms like a bell, the red lining showing at its turned edge. The spear
    stands beside him, taller than he is.
    """
    g = G(32)
    # The spear, behind the hand that holds it.
    g.line(25, 6, 25, 29, "N")
    g.line(26, 6, 26, 29, "j")
    g.set(25, 30, "J")
    g.span(5, 24, 27, "q")
    g.span(4, 24, 26, "r")
    g.span(3, 25, 26, "r")
    g.set(25, 2, "t")
    g.set(24, 4, "t")
    g.set(27, 5, "o")
    g.set(25, 6, "o")

    roster.mass(g, [(12, 12, 19), (17, 10, 21), (24, 10, 21), (29, 9, 22)], "R", "r", "q")
    # the coat skirt, split at the front, a red underskirt in the split
    g.line(16, 22, 15, 29, "o")
    g.line(17, 23, 17, 29, "2")
    g.set(17, 28, "3")
    roster.fold(g, [(12, 23), (11, 29)], "q", "r")
    roster.fold(g, [(20, 22), (21, 29)], "o", None)
    roster.cut(g, (16, 29), (17, 29))
    # the sash
    g.span(20, 10, 21, "3")
    g.span(21, 10, 21, "4")
    g.set(10, 20, "2")
    g.set(13, 20, "1")
    g.line(13, 21, 12, 24, "3")

    # THE MANTLE: flaring from the neck out over the arms. Lamellar - plates in courses, the
    # joints staggered course to course, so it is laid armour rather than a polka dot.
    for i, y in enumerate(range(11, 19)):
        t = i / 7.0
        lo = 13 - 7.2 * t
        hi = 18 + 6.8 * t
        g.span(y, lo, hi, "R")
        g.set(lo, y, "r")
        g.set(hi, y, "q")
        if g.get(lo - 1, y) == ".":
            g.set(lo - 1, y, "W")
    for c, y in enumerate((12, 14, 16)):
        for x in range(5, 27):
            if g.get(x, y) not in ("R", "r", "q"):
                continue
            if (x + c * 2) % 4 == 0:
                g.set(x, y, "q")
                g.set(x, y + 1, "q")
            elif (x + c * 2) % 4 == 1:
                g.set(x, y, "r")
    g.span(18, 6, 24, "q")
    g.span(19, 6, 24, "o")
    g.set(6, 18, "3")
    g.set(24, 18, "3")
    g.set(7, 18, "2")

    # the hand on the spear
    g.rect(23, 17, 25, 19, "R")
    g.set(23, 17, "r")
    g.set(25, 19, "q")

    # THE HELM
    g.rect(12, 5, 19, 11, "R")
    g.line(12, 5, 12, 11, "r")
    g.line(13, 6, 13, 7, "r")
    g.line(19, 5, 19, 11, "q")
    g.span(4, 13, 18, "r")
    g.span(11, 12, 19, "q")
    g.span(8, 13, 18, "o")
    g.span(9, 13, 18, "o")
    g.span(10, 14, 17, "q")
    g.set(15, 10, "R")
    g.set(19, 8, "r")                              # a cheek guard's edge
    # THE CREST: bleached horsehair, standing up along the crown and falling down the nape.
    g.span(3, 13, 19, "@")
    g.span(2, 15, 19, "!")
    g.set(20, 3, "#")
    g.set(20, 2, "@")
    g.set(14, 3, "!")
    for i, (x, y) in enumerate(((12, 3), (11, 4), (10, 5), (10, 6), (9, 7), (9, 8), (9, 9))):
        g.set(x, y, "@" if i % 3 else "!")
        g.set(x + 1, y + 1, "#") if g.get(x + 1, y + 1) == "." else None
    g.set(8, 10, "#")
    g.set(17, 3, "#")
    roster.contact(g, 9, 26)
    roster.eyes(g, 8, 14, 17, "Y", "y")
    return g.rows()


# =================================================================================================
# RANGED - Dune Oracle. A veiled seer of the road.
# =================================================================================================

def oracle():
    """Tall and narrow, veiled to the hem, and the bead she reads the road in floats beside her.

    The caster's detached orb is a GLASS BEAD in the wind ramp, the colour of what she throws,
    with transparent daylight between it and the reaching hand. A fringe of tarnished coins across
    the brow is the gold; the veil is linen, the cool cloth, so she is a material step away from
    the sand-wrapped travellers she walks among.
    """
    g = G(32)
    # the veil's train, trailing back
    g.line(11, 14, 6, 28, "A")
    g.line(12, 15, 7, 28, "E")
    g.line(12, 17, 8, 28, "A")
    g.set(5, 29, "A")
    g.set(6, 29, "E")

    roster.mass(g, [(3, 14, 16), (5, 12, 18), (9, 11, 19), (14, 11, 19), (22, 10, 20), (29, 9, 20)],
                "E", "e", "A")
    # the under-robe, showing down the front where the veil parts
    for y in range(14, 30):
        a = 16 + (y - 14) * 0.12
        g.span(y, a, a + 2, "#")
        g.set(a, y, "o")
        if y % 4 == 1:
            g.set(a + 1, y, "$")
    roster.fold(g, [(13, 12), (12, 29)], "A", "e")
    roster.fold(g, [(15, 18), (14, 29)], "A", None)
    roster.fold(g, [(19, 21), (20, 29)], "o", None)

    # the coin fringe and the void behind the veil
    for x, ch in zip(range(12, 20), "GuGgGuGg"):
        g.set(x, 7, ch)
    g.set(13, 8, "g")
    g.set(17, 8, "u")
    g.span(8, 14, 16, "o")
    g.span(9, 13, 18, "o")
    g.span(10, 14, 17, "o")
    g.span(6, 13, 17, "A")
    # the girdle, gold knots
    g.span(19, 11, 19, "u")
    g.set(12, 19, "G")
    g.set(15, 19, "g")
    g.set(15, 20, "u")
    g.set(15, 21, "G")

    # THE REACHING ARM, out and up toward the bead, ending in a bone hand.
    g.line(18, 15, 21, 12, "E")
    g.line(18, 16, 21, 13, "A")
    g.set(22, 11, "k")
    g.set(22, 12, "n")
    g.set(23, 11, "s")

    # THE BEAD, with nothing joining it to her.
    g.disc(26, 8, 2.6, "7")
    g.disc(26, 8, 1.9, "6")
    g.disc(25.6, 7.6, 0.9, "5")
    g.set(25, 7, "U")

    roster.tatters(g, [(29, 10, 14), (29, 16, 20), (28, 8, 9)], "A", "E", "o")
    roster.contact(g, 6, 21)
    roster.eyes(g, 9, 14, 17, "Y", "y")
    return g.rows()


# =================================================================================================
# TURRET - Bone Ballista. Built: it burns.
# =================================================================================================

def ballista():
    """A crossbow frame on a tripod, its arms out flat, built from the caravan's bones.

    Filled black it is a T on three legs - the bow arms the widest and heaviest thing in the
    chapter, the tripod splayed thin under them - and nothing else in the cast stands on three
    points. It is the one BUILT thing here, so its mark is fire: a skull lashed on behind the nut
    as the sight, burning.

    The bone is drawn from the row's BASE down, with the lit stop only along the key edge. The
    first pass drew the legs in the lit stop and they came out three white sticks under a thin
    line: a camera tripod, and the brightest thing on the screen. The weight belongs in the arms.
    """
    g = G(32)
    # THE TRIPOD: three long bones, knobbed at the joints, splayed. The far leg is darker.
    _limb(g, 16, 19, 17, 29, 0.9, "m", "n", "o")
    for (x0, x1) in ((14, 7), (18, 25)):
        _limb(g, x0, 18, x1, 29, 1.2, "n", "k", "m")
    for (kx, ky) in ((11, 23), (21, 23)):
        g.set(kx, ky, "k")
        g.set(kx + 1, ky, "n")
        g.set(kx, ky + 1, "m")
    for (x, ch) in ((7, "k"), (17, "n"), (25, "k")):
        g.span(30, x - 1, x + 1, "m")
        g.set(x - 1, 30, ch)
    # the hub: a pelvis, lashed with cloth
    g.disc(16, 17.5, 2.6, "n")
    g.set(14, 16, "k")
    g.set(15, 16, "s")
    g.set(17, 17, "o")
    g.set(15, 18, "o")
    g.line(14, 18, 18, 19, "#")
    g.line(14, 19, 17, 20, "$")

    # THE STOCK: a spine of vertebrae on a stone rail, running back to a steel-headed bolt.
    g.span(14, 7, 24, "p")
    g.span(15, 7, 24, "j")
    g.set(7, 14, "P")
    for (x, w) in ((8, 2), (11, 1), (13, 2), (16, 1), (18, 2), (21, 1), (23, 2)):
        g.span(13, x, x + w - 1, "k")
        g.set(x, 13, "s")
        g.set(x + w, 13, "o")
    g.span(12, 17, 26, "N")                      # the bolt, laid in its groove
    g.set(17, 12, "p")
    g.span(11, 26, 27, "t")
    g.span(12, 27, 28, "r")
    g.set(26, 13, "q")
    g.set(29, 12, "q")

    # THE ARMS: two ribs out flat from the front of the stock, thick at the root and recurved at
    # the tips - the heaviest line in the figure.
    for (sx, ex) in ((19, 4), (22, 29)):
        n = abs(ex - sx)
        for i in range(n + 1):
            t = i / float(n)
            x = sx + (ex - sx) * t
            y = 10.0 - 1.8 * t * t
            g.set(x, y, "s" if i % 5 == 1 else "k")
            g.set(x, y + 1, "k" if t < 0.5 else "n")
            g.set(x, y + 2, "n" if t < 0.75 else "m")
            if t < 0.45:
                g.set(x, y + 3, "m" if i % 3 else "o")
    g.rect(19, 8, 22, 11, "#")                   # the lashing where the ribs meet the stock
    g.set(19, 8, "!")
    g.set(20, 8, "@")
    g.line(19, 11, 22, 8, "$")
    # the string, drawn back to the nut
    g.line(4, 9, 13, 12, "j")
    g.line(29, 9, 15, 12, "j")

    # THE SIGHT: a skull lashed on behind the nut, and the fire in it is the maker's mark.
    g.disc(11.0, 8.6, 2.7, "k")
    g.set(9, 7, "s")
    g.set(8, 8, "s")
    g.set(10, 6, "S")
    g.set(13, 9, "n")
    g.set(13, 10, "n")
    g.span(11, 9, 12, "n")
    g.set(10, 12, "m")
    g.set(12, 12, "m")
    g.set(11, 12, "o")
    g.rect(9, 8, 10, 9, "o")
    g.rect(12, 8, 12, 9, "o")
    g.set(10, 8, "+")
    g.set(12, 8, "-")
    g.set(12, 9, "=")
    roster.contact(g, 6, 26)
    return g.rows()


# =================================================================================================
# CHARGER - Scorpion. The size of a dog.
# =================================================================================================

def scorpion():
    """Low and long, claws forward, the tail arched over its back.

    The tail is SEGMENTED AND SMALL - five beads stepping up and over to a hooked sting - so it is
    never the curve of a serpent; the Long Coil is an S, and nothing in its chapter may echo it.
    Filled black it is the chapter's one creature built along the ground, with a hook over it.

    The carapace is the stone row's UPPER stops. Its first pass sat in stone base and shade and on
    the Sands floor it was a black cut-out with no inside; the legs were also four straight posts,
    which is the table the forest's wolf notes warn about. A scorpion's legs splay - knees up and
    out, feet planted wide of the body, the front pair reaching forward and the back pair back.
    """
    g = G(32)
    # LEGS: the far side first, darker and shorter, then the near side over them.
    for (hx, kx, ky, fx, fy) in ((12, 9, 20, 7, 27), (15, 14, 19, 12, 27), (18, 19, 19, 19, 27),
                                 (20, 23, 19, 24, 27)):
        g.line(hx, 21, kx, ky, "j")
        g.line(kx, ky, fx, fy, "J")
    for (hx, kx, ky, fx, fy) in ((11, 7, 20, 4, 29), (14, 12, 20, 10, 30), (17, 18, 20, 17, 30),
                                 (20, 23, 20, 25, 30)):
        g.line(hx, 23, kx, ky, "N")
        g.line(hx, 24, kx, ky + 1, "j")
        g.line(kx, ky, fx, fy, "p")
        g.line(kx + 1, ky + 1, fx + 1, fy, "J")
        g.set(kx, ky, "P")
        g.set(fx, fy, "n")

    # THE BODY: plates overlapping toward the head, each with a lit lip and a dark seam.
    # Each plate's lit lip is BONE, not stone: the sun has bleached the edges of the shell, and the
    # stone row's own upper stops are still too near the Sands floor to give the shell an inside.
    for (cx, hw, top) in ((9.5, 2.0, 20), (12.5, 2.6, 19), (15.5, 2.9, 18), (18.5, 3.0, 18)):
        for y in range(top, 24):
            g.span(y, cx - hw, cx + hw, "P" if y < top + 3 else "p")
        g.span(top, cx - hw, cx + hw, "n")
        g.set(cx - hw, top, "k")
        g.set(cx - hw, top + 1, "P")
        g.line(cx + hw, top, cx + hw, 23, "J")
        g.span(23, cx - hw, cx + hw, "N")
        g.set(cx, top + 2, "P")
        g.set(cx, top + 3, "N")
    g.span(24, 9, 22, "j")
    g.set(15, 18, "s")
    # the head shield, with the mark on its brow
    g.rect(21, 18, 24, 23, "p")
    g.rect(21, 19, 23, 20, "P")
    g.span(18, 21, 23, "n")
    g.set(21, 18, "k")
    g.set(24, 18, "N")
    g.line(24, 19, 24, 23, "N")
    g.span(23, 21, 24, "j")
    g.set(25, 22, "o")

    # THE CLAWS: the far one held high, the near one low and forward, each a swollen hand with
    # two fingers and a bone edge on the fixed finger.
    for (sx, sy, ex, ey, near) in ((23, 19, 26, 15, False), (24, 22, 27, 21, True)):
        mid, lit, dark = ("p", "n", "J") if near else ("N", "P", "J")
        g.line(sx, sy, ex - 1, ey + 1, mid)
        g.line(sx, sy + 1, ex - 1, ey + 2, dark)
        g.disc(ex, ey, 1.6, mid)
        g.set(ex - 1, ey - 1, lit)
        g.set(ex, ey - 1, lit)
        g.set(ex + 1, ey + 1, dark)
        g.set(ex + 2, ey - 2, "k")
        g.set(ex + 3, ey - 2, "n")
        g.set(ex + 2, ey + 1, "s" if near else "k")
        g.set(ex + 3, ey + 1, "n")
        g.set(ex + 2, ey - 1, ".")
        g.set(ex + 2, ey, ".")
        g.set(ex + 3, ey, ".")

    # THE TAIL: five beads up and over, shrinking, then the sting.
    for (bx, by, br) in ((8.0, 18.5, 1.9), (6.4, 15.0, 1.8), (6.6, 11.3, 1.7), (8.6, 8.3, 1.6),
                         (11.6, 6.7, 1.5)):
        g.disc(bx, by, br, "p")
        g.set(bx - br + 0.5, by - 0.5, "n")
        g.set(bx - 0.4, by - br + 0.4, "P")
        g.set(bx + br - 0.4, by + 0.3, "N")
        g.set(bx + 0.3, by + br - 0.3, "J")
    g.disc(14.4, 7.0, 1.5, "k")
    g.set(14, 6, "s")
    g.set(15, 8, "n")
    g.set(16, 9, "n")
    g.set(17, 10, "m")
    g.set(16, 10, "o")

    roster.contact(g, 6, 26)
    g.set(22, 19, "Y")
    g.set(23, 19, "y")
    return g.rows()


# =================================================================================================
# EXPLODER - Swollen Cask. Faces LEFT: the stand-in sets SpriteFacesRightByDefault = false.
# =================================================================================================

def swollen_cask():
    """A caravan cask swollen past its hoops, walking - and it is full of sand, not water.

    The bulge is the attachment: the only round-bellied thing in the chapter, taller than wide like
    a cask and not a ball, its staves bowed and split, the middle hoop sprung and curling loose off
    the side. The tap is on the LEFT and it walks that way, because the exploder scene flips a
    sprite that faces left by default. What leaks from the splits is the desert itself.

    THE WOOD IS THE SAND ROW'S LOWER HALF, not stone. bestiary.md planned stone wood, as the
    bramble's and Elderbark's is; drawn that way the cask came out steel-blue on this floor and read
    as an iron-banded ball both times it was tried. The sand row's shade and deep stops are the one
    warm brown the contract holds, and against steel hoops they read as a barrel at a glance. The
    Thirsting wears the same row's upper stops, so the two stay a value step apart.
    """
    g = G(32)
    cx = 16.0
    prof = [5.0, 7.2, 8.5, 9.0, 8.6, 7.4, 5.4]
    SB.swell(g, 5, 26, prof, "#", "@", "$", cx=cx)

    def half_width(y):
        t = (y - 5) / 21.0 * (len(prof) - 1)
        i = min(len(prof) - 2, int(t))
        return prof[i] + (prof[i + 1] - prof[i]) * (t - i)

    # staves, following the swell, at uneven widths: a dark seam with a lit stop on its key side
    for frac in (-0.72, -0.30, 0.12, 0.50, 0.80):
        for y in range(6, 26):
            x = cx + frac * half_width(y)
            if g.get(x, y) in ("#", "@", "$"):
                g.set(x, y, "$")
                if g.get(x + 1, y) == "#" and (y * 3 + int(frac * 10)) % 5:
                    g.set(x + 1, y, "@")
    # grain: a few short dark strokes inside the staves, never a pattern
    for (x, y) in ((14, 7), (19, 10), (9, 12), (15, 19), (21, 19), (12, 24), (18, 24)):
        if g.get(x, y) == "#":
            g.set(x, y, "4")
    # the lid, seen just from above
    g.span(5, 12, 20, "$")
    g.span(4, 13, 19, "#")
    g.set(13, 4, "@")
    g.set(17, 4, "$")
    # HOOPS: top and bottom whole, the middle one sprung off and curling out to the right.
    for (y, a, b) in ((8, 8, 24), (22, 8, 24)):
        for x in range(a, b + 1):
            if g.get(x, y) != ".":
                g.set(x, y, "r")
                g.set(x, y + 1, "q")
        g.set(a + 1, y, "t")
    for x in range(7, 18):
        if g.get(x, 15) != ".":
            g.set(x, 15, "r")
            g.set(x, 16, "q")
    g.set(8, 15, "t")
    for (x, y, ch) in ((18, 14, "r"), (19, 14, "q"), (20, 13, "r"), (21, 13, "q"), (22, 12, "r"),
                       (23, 12, "r"), (24, 11, "q"), (25, 11, "r"), (26, 12, "r"), (26, 13, "q"),
                       (25, 13, "q")):
        g.set(x, y, ch)
    # THE SPLITS, bulging at the waist, and the mark looking out of the widest.
    g.line(11, 17, 10, 20, "o")
    g.line(20, 17, 21, 21, "o")
    g.line(17, 10, 17, 13, "o")
    g.span(11, 12, 15, "o")
    g.span(12, 12, 15, "o")
    # the tap, low on the left
    g.rect(5, 18, 7, 19, "q")
    g.set(5, 18, "r")
    g.set(4, 18, "R")
    g.set(4, 19, "o")
    # SAND, trickling from the low split and piling at its feet
    for (x, y, ch) in ((10, 21, "G"), (9, 23, "g"), (9, 25, "u"), (8, 27, "g")):
        g.set(x, y, ch)
    g.span(30, 6, 9, "u")
    g.set(7, 30, "g")
    g.set(8, 29, "u")
    # stubby legs, wrapped, the front one stepping left
    for (lx, fx) in ((12, 10), (19, 19)):
        g.line(lx, 26, fx, 29, "A")
        g.line(lx + 1, 26, fx + 1, 29, "E")
        g.span(30, fx - 1, fx + 1, "A")
        g.set(fx - 1, 30, "E")
    roster.contact(g, 6, 23)
    g.set(13, 12, "Y")
    g.set(14, 12, "y")
    return g.rows()


# =================================================================================================
# SLAMMER - Flailbearer. The caravan's guard captain.
# =================================================================================================

def flailbearer():
    """Whirling a chain flail overhead: the flail head is the highest thing in the cell.

    The slammer's rule is that the hands-up mass agrees with the ring on the ground, and here the
    thing that will land is up there to be seen - a spiked ball at the top of a chain arc over a
    sand-wrapped helm. Steel and red, like the guard he commanded, but upright and armed overhead
    where the guard is broad and crested.
    """
    g = G(32)
    roster.mass(g, [(12, 12, 19), (18, 10, 21), (24, 10, 21), (29, 9, 22)], "R", "r", "q")
    # the tabard, red, down the front, with a fold
    for y in range(13, 29):
        g.span(y, 14, 17, "3")
        g.set(14, y, "2")
    roster.fold(g, [(16, 15), (16, 28)], "4", "2")
    g.span(20, 10, 21, "q")
    g.span(21, 10, 21, "o")
    g.set(11, 20, "r")
    g.set(15, 20, "g")
    roster.fold(g, [(12, 22), (11, 29)], "q", "r")
    roster.fold(g, [(19, 22), (20, 29)], "o", None)
    roster.cut(g, (15, 29), (16, 29))

    # the lowered arm, a fist
    g.line(11, 13, 9, 20, "R")
    g.line(12, 13, 10, 20, "q")
    g.rect(8, 20, 10, 22, "R")
    g.set(8, 20, "r")
    g.line(11, 14, 12, 19, "o")

    # THE RAISED ARM and the haft
    g.line(19, 13, 22, 7, "R")
    g.line(20, 13, 23, 7, "q")
    g.line(18, 13, 21, 8, "r")
    g.rect(22, 5, 24, 7, "R")
    g.set(22, 5, "r")
    g.line(24, 3, 24, 6, "N")
    g.set(25, 3, "j")

    # THE HEAD: a steel cap wound with sand cloth, and a mail veil.
    g.rect(12, 6, 19, 12, "@")
    g.line(12, 6, 12, 12, "!")
    g.line(19, 6, 19, 12, "#")
    g.span(5, 13, 18, "@")
    g.set(13, 5, "!")
    roster.fold(g, [(12, 8), (19, 6)], "#", "!", side=(0, -1))
    g.set(15, 4, "r")
    g.set(16, 4, "q")
    g.span(9, 13, 18, "o")
    g.span(10, 14, 17, "o")
    g.span(11, 13, 18, "q")
    g.span(12, 14, 17, "R")
    g.set(15, 12, "q")

    # THE CHAIN, arcing up and over, and the ball at the end of it - highest of all.
    chain(g, [(24, 3), (22, 2), (17, 2), (13, 3)])
    g.disc(10, 5, 2.4, "R")
    g.set(9, 4, "r")
    g.set(10, 3, "t")
    g.set(11, 6, "q")
    g.set(10, 7, "q")
    g.set(11, 5, "o")
    for (x, y) in ((10, 2), (7, 5), (13, 6), (9, 8)):
        g.set(x, y, "r")
    g.set(6, 5, "W")
    roster.contact(g, 8, 23)
    roster.eyes(g, 9, 15, 17, "Y", "y")
    return g.rows()


# =================================================================================================
# SUMMONER - Carrion Caller. Calls the Bone Vultures down.
# =================================================================================================

def carrion_caller():
    """Hunched under its hood, one arm thrust up and forward in a falconer's glove with a perch.

    The perch-glove is the attachment: a long red gauntlet raised at forty-five degrees with a
    bone crossbar in the fist, a feather and a knucklebone hung off it on cords - the visible reason
    the sky fills. It is raised FORWARD, not straight up, so it never reads as the Flailbearer's
    arm. Linen, the cool cloth, and hunched: the hood's peak rides on a hump of back higher than
    the head, which hangs forward of the body - a question mark where every other figure here
    stands upright. The first pass was a slab with a flag on it; the hump has to be narrow to be a
    hump at all.
    """
    g = G(32)
    # THE CLOAK: a narrow hump of back, the hood peaked on it, the skirt falling to the ground.
    blob(g, [(13.0, 12.0, 3.2), (11.6, 15.5, 3.9), (12.4, 20.5, 4.4), (14.0, 25.0, 5.0),
             (15.5, 14.0, 2.6)], "E", "e", "A")
    for y in range(26, 30):
        a, b = 9 - (y - 26) * 0.4, 19 + (y - 26) * 0.4
        g.span(y, a, b, "E")
        g.set(a, y, "e")
        g.set(b, y, "A")
        if g.get(a - 1, y) == ".":
            g.set(a - 1, y, "W")
    # the hood's peak, laid back on the hump
    g.line(11, 9, 7, 8, "E")
    g.line(11, 10, 8, 9, "A")
    g.set(6, 8, "e")
    g.set(10, 8, "e")
    roster.fold(g, [(10, 15), (9, 29)], "A", "e")
    roster.fold(g, [(14, 19), (14, 29)], "A", "e")
    roster.fold(g, [(17, 22), (18, 29)], "o", None)
    roster.fold(g, [(10, 11), (14, 10)], "A", "e", side=(0, -1))

    # THE HEAD, hung forward of the body and below the hump, the hood's lip over it.
    blob(g, [(18.5, 14.5, 2.5)], "E", "e", "A", rim=False)
    g.span(13, 17, 20, "A")
    g.span(14, 19, 21, "o")
    g.span(15, 19, 21, "o")
    g.set(18, 16, "A")
    g.set(21, 13, "A")
    g.line(15, 13, 16, 17, "o")                  # where the hood folds in under the hump

    # a red sash and the other hand clutching the cloak shut
    g.span(22, 9, 18, "3")
    g.span(23, 9, 18, "4")
    g.set(10, 22, "2")
    g.set(14, 22, "1")
    g.line(12, 23, 11, 26, "3")
    g.set(16, 20, "k")
    g.set(17, 20, "n")

    # THE PERCH-ARM: up and forward, the gauntlet red and long, the perch in the fist.
    g.line(17, 18, 19, 17, "e")
    g.line(17, 19, 20, 18, "A")
    for i in range(7):
        x, y = 20.4 + i * 0.75, 17.0 - i * 1.35
        g.set(x - 1, y, "1")
        g.set(x, y, "2")
        g.set(x + 1, y, "3")
    g.rect(25, 6, 26, 8, "2")
    g.set(25, 6, "1")
    g.set(26, 8, "3")
    g.set(27, 7, "4")
    g.span(5, 21, 29, "k")
    g.set(21, 5, "s")
    g.set(22, 5, "s")
    g.set(29, 5, "n")
    g.span(6, 22, 23, "n")
    g.set(28, 6, "n")
    # hung from the perch on cords: a feather, and a knucklebone
    g.line(22, 7, 22, 9, "j")
    g.set(22, 10, "E")
    g.set(22, 11, "A")
    g.line(28, 7, 28, 8, "j")
    g.set(28, 9, "s")
    g.set(29, 9, "n")

    roster.tatters(g, [(29, 8, 11), (29, 16, 20), (30, 9, 10)], "A", "E", "o")
    roster.contact(g, 7, 21)
    g.set(19, 15, "Y")
    g.set(21, 15, "y")
    return g.rows()


# =================================================================================================
# MINIBOSS - The Toll-Keeper. 48 x 48. His jailer on the trade road.
# =================================================================================================

def toll_keeper():
    """A toll-chain across the chest, a strongbox hung low at one side, and a hood over the helm.

    Filled black: a tall hooded figure, broad at the shoulder, with a heavy square mass hanging
    off one hip almost to the ground - asymmetric and bottom-heavy on one side, which nothing else
    in the chapter is, and which the Long Coil's S cannot be mistaken for. The Warden's chains hang
    from its wrists; this one's chain is worn ACROSS the body, a bandolier of links with the tolls
    hung off it, and the strongbox's lock is the gold.
    """
    g = G(48)
    F = 46
    # LEGS, short and wide under the coat
    for (lx0, lx1, key) in ((16, 21, "r"), (26, 31, "R")):
        g.rect(lx0, 38, lx1, F, "R")
        g.line(lx0, 38, lx0, F, key)
        g.line(lx1, 38, lx1, F, "q")
        g.span(42, lx0, lx1, "q")
        g.span(F, lx0 - 1, lx1 + 2, "q")
        g.span(F - 1, lx0 - 1, lx1 + 2, "R")
        g.set(lx0 - 1, F - 1, "r")
    g.rect(22, 39, 25, F, "o")

    # THE MANTLE behind, sand cloth, falling down the back
    roster.mass(g, [(12, 14, 20), (18, 9, 20), (30, 8, 18), (39, 9, 16)], "@", "!", "#")
    roster.fold(g, [(12, 19), (10, 38)], "$", "!")
    roster.fold(g, [(15, 22), (13, 37)], "#", "!")

    # THE BODY: a brigandine coat, plated, narrowing at the belt, a red skirt under it.
    roster.mass(g, [(14, 15, 32), (24, 15, 31), (31, 16, 30)], "R", "r", "q")
    roster.mass(g, [(32, 15, 31), (39, 13, 33)], "3", "2", "4", rim=False)
    roster.fold(g, [(19, 33), (18, 39)], "4", "2")
    roster.fold(g, [(26, 33), (27, 39)], "4", "2")
    # Two seams in the brigandine and a handful of rivets, no more: the chain has to be the
    # loudest thing on the chest, and the first pass's ten rivets buried it.
    for (y, a, b) in ((19, 16, 31), (26, 17, 30)):
        for x in range(a, b + 1):
            if g.get(x, y) == "R":
                g.set(x, y, "q")
                if g.get(x, y - 1) == "R" and x % 3:
                    g.set(x, y - 1, "r")
    SB.rivets(g, [(28, 16), (17, 22), (29, 23), (20, 28)], "t", "q")
    plate_belt = [(31, 15, 31)]
    for (y, a, b) in plate_belt:
        g.span(y, a, b, "q")
        g.span(y - 1, a, b, "R")
        g.set(a, y - 1, "r")

    # SHOULDERS: plain rounded caps, no spikes - that is the Warden's.
    for (cx, w) in ((14.5, 4.5), (32.0, 4.0)):
        for i, y in enumerate(range(13, 20)):
            hw = w * (1.0 - (i / 7.0) ** 2 * 0.6)
            g.span(y, cx - hw, cx + hw, "R" if i > 1 else "r")
            g.set(cx - hw, y, "r")
            g.set(cx + hw, y, "q")
        g.span(20, cx - w + 1, cx + w - 1, "o")
    # arms: the far one hanging, the near one on the box chain
    g.rect(10, 20, 13, 31, "R")
    g.line(10, 20, 10, 31, "r")
    g.line(13, 20, 13, 31, "q")
    g.rect(9, 31, 13, 34, "q")
    g.line(9, 31, 13, 31, "R")
    g.line(14, 20, 14, 30, "o")
    g.line(34, 20, 36, 28, "R")
    g.line(35, 20, 37, 28, "q")
    g.line(33, 21, 35, 28, "r")
    g.rect(35, 28, 38, 31, "R")
    g.set(35, 28, "r")
    g.line(33, 20, 33, 28, "o")

    # THE TOLL-CHAIN across the chest, shoulder to hip, tolls hung from it
    # Links three pixels long, each a lit stop over a dark one with occ between them, so the
    # chain reads as iron links at x2.7 rather than as a dotted line.
    for i in range(7):
        x, y = 13.0 + i * 2.75, 15.0 + i * 2.3
        g.set(x, y, "t")
        g.set(x + 1, y, "r")
        g.set(x + 1, y + 1, "r")
        g.set(x, y + 1, "q")
        g.set(x + 2, y + 1, "q")
        g.set(x + 1, y + 2, "o")
        g.set(x + 2, y + 2, "o")
    # the tolls: coins strung on the chain, the gold that is not the lock
    for (x, y) in ((18, 22), (25, 27)):
        g.set(x, y, "G")
        g.set(x + 1, y, "g")
        g.set(x, y + 1, "g")
        g.set(x + 1, y + 1, "u")

    # THE STRONGBOX, hung low on the near side from the belt
    chain(g, [(36, 31), (37, 34)], "r", "q")
    g.rect(31, 35, 42, 43, "R")
    g.line(31, 35, 42, 35, "r")
    g.line(31, 35, 31, 43, "r")
    g.line(42, 35, 42, 43, "q")
    g.line(31, 43, 42, 43, "q")
    g.span(44, 31, 42, "o")
    g.span(37, 32, 41, "q")
    for x in (34, 39):
        g.line(x, 36, x, 42, "q")
    g.rect(36, 38, 37, 41, "u")
    g.set(36, 38, "H")
    g.set(37, 38, "G")
    g.set(36, 39, "G")
    g.set(37, 40, "o")
    SB.rivets(g, [(32, 36), (41, 36), (32, 41), (41, 41)], "t", "q")

    # THE HEAD: a sand hood, peaked, over a steel cap, the void under it.
    roster.mass(g, [(3, 22, 23), (5, 20, 25), (8, 18, 27), (13, 17, 28)], "@", "!", "#")
    roster.fold(g, [(21, 4), (18, 12)], "$", "!")
    g.span(8, 19, 26, "R")
    g.span(9, 19, 26, "q")
    g.set(19, 8, "r")
    g.span(10, 20, 26, "o")
    g.span(11, 20, 26, "o")
    g.span(12, 21, 25, "o")
    g.span(13, 22, 25, "q")
    g.set(26, 12, "#")
    roster.contact(g, 13, 43, y=F)
    roster.eyes(g, 11, 21, 24, "Y", "y")
    return g.rows()


# =================================================================================================

CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    #
    # THE MINIBOSS IS LISTED FIRST. bestiary_kit.report's contact sheet takes its cell size from the
    # first creature in the list, so a 32 cell first and the 48-cell jailer after it raises "row
    # wider than cell" before anything is written. With the 48 first, the 32s pad into it.
    ("sands-toll-keeper", toll_keeper, 48, (0.5, 4.2, 1), "SandsTollKeeperFrames"),
    ("sands-thirsting", thirsting, 32, (0.8, 2.4, 2), "SandsThirstingFrames"),
    ("sands-vulture", vulture, 32, (0.5, 2.6, 3), "SandsVultureFrames"),
    ("sands-porter", porter, 32, (0.6, 2.6, 1), "SandsPorterFrames"),
    ("sands-caravan-guard", caravan_guard, 32, (0.7, 3.6, 1), "SandsCaravanGuardFrames"),
    ("sands-oracle", oracle, 32, (0.9, 2.2, 3), "SandsOracleFrames"),
    ("sands-ballista", ballista, 32, (0.2, -2.4, 1), "SandsBallistaFrames"),
    ("sands-scorpion", scorpion, 32, (1.4, 4.4, 2), "SandsScorpionFrames"),
    ("sands-swollen-cask", swollen_cask, 32, (1.1, -2.0, -3), "SandsSwollenCaskFrames"),
    ("sands-flailbearer", flailbearer, 32, (0.6, 4.0, 1), "SandsFlailbearerFrames"),
    ("sands-carrion-caller", carrion_caller, 32, (0.9, 2.6, 2), "SandsCarrionCallerFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    bestiary_kit.run(CAST, PALETTE, "sands")
