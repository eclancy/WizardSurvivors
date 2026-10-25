# -*- coding: utf-8 -*-
"""The Fallow Fields' own cast (.ai/campaign.md section 5). Run: python tools/art/cast_fields.py

"He takes a tithe of everything that grows here." The farms beyond the wood: the gleaners and field
hands, the cunning woman, the oxen, boars and rooks, taken while they worked - and the iron
weathercock he set over them, which is the one thing here he BUILT, and so the one thing that burns.

THE FLOOR DECIDES THE PALETTE. The Fields stand on stubble: gold's deep stop with linen's deep in
it (tools/art/terrain_fields.py). A body in gold's deep half is a hole in that floor, so the gold
things here - the sheaf, the rick, the seed, the grain at the sack's seams - carry their weight in
gold's base and shade with a lit stop on the key side, and the people of the land wear LINEN, the
grey-green that reads on a warm floor by hue before value. Stone is the beasts' dark (the rook's
feathers, the boar's mane, the ox's yoke); bone is what is pale and hard (beak, horns, tusks);
steel is what was forged (the fork's tines, the reeve's scale, the weathercock).

NOTHING ECHOES THE HAYWARD. The chapter's boss is a tall figure behind an upright rectangle of woven
hurdle. So nothing here holds a flat plane upright: the ox's yoke is a beam across the neck, the
reeve's attachment hangs (a balance scale), and the rick is round.

ONE ATTACHMENT EACH, no two alike in the chapter, and each survives being filled black: a fan of
grain ears above a stooped back (Gleaner); ragged fingered wings and a heavy beak (Rook); a beam
crossing a quadruped's neck (Ox); a rounded thatched dome with a fork handle through it (Rick); a
straw doll floating at a raised hand (Cunning Woman); a cockerel and cross-arms on a post
(Weathercock); a bristled hump and forward tusks, low to the ground (Boar); a sack's tied neck and
swell, facing left (Tithe Sack); a fork's tines raised highest (Haymaker); one arm flung wide over a
hip basket (Sower); a hanging balance scale on the 48 cell (Tithe-Reeve).

THE PALETTE CHARACTERS. The roster palette spends every printable character, and its gold row has
only four stops (H G g u - no deep). The four PLAYER PIGMENT rows are reserved to the player cast,
so no enemy may wear them, which frees their characters to mean something else here (as in
cast_forest.py and cast_cave.py): the amber characters are gold's full five-stop row, the ember
characters red cloth, the azure characters lichen. The chapter's bolt element, earth, is already in
the roster palette ({ } | \\), so the corn dolly is the colour of what the cunning woman throws.
anim_sets steps a pixel along the ramp its CHARACTER belongs to, so a hurt flash or a death fade
still walks each redefined row in the right order.
"""
from __future__ import print_function

import math
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
# The generic drawing helpers live with the chapters drawn before; they paint, they do not decide.
from cast_forest import blob, rim, twig, inside
from cast_ruins import poly, shade
from cast_sands import blob as discs

G = roster.G
mass = roster.mass
fold = roster.fold
cut = roster.cut
eyes = roster.eyes
contact = roster.contact

PALETTE = dict(roster.PALETTE)
# Gold's whole row in the amber characters (hi, lit, base, shade, deep): the roster's own gold
# characters stop at shade, and stubble country needs the deep stop.
for _ch, _hex in zip("O!@#$", B.MATERIALS["gold"]):
    PALETTE[_ch] = _hex
for _ch, _hex in zip("Q1234", B.MATERIALS["red"]):
    PALETTE[_ch] = _hex
for _ch, _hex in zip("U5678", B.MATERIALS["lichen"]):
    PALETTE[_ch] = _hex

GOLD_HI, GOLD_LIT, GOLD, GOLD_SH, GOLD_DP = "O", "!", "@", "#", "$"
RED_HI, RED_LIT, RED, RED_SH, RED_DP = "Q", "1", "2", "3", "4"
MOSS_HI, MOSS_LIT, MOSS, MOSS_SH, MOSS_DP = "U", "5", "6", "7", "8"
EARTH_CORE, EARTH_HOT, EARTH_MID, EARTH_EDGE = "{", "}", "|", "\\"
HAY = GOLD + GOLD_SH + GOLD_DP + GOLD_LIT

CELL = 32
FEET = CELL - 2


# --- local helpers ------------------------------------------------------------------------------

def stalk(g, x0, y0, x1, y1, tone, lit=None):
    """One straw: a one-pixel line, with a lit pixel every few along it so a bundle of them reads as
    separate stalks rather than as a filled wedge."""
    n = int(max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        g.set(x, y, lit if (lit and i % 4 == 1) else tone)


def ear(g, x, y, dx, dy):
    """A head of grain at the end of a stalk pointing (dx, dy): a fat three-pixel spike lit on the key
    side, two pixels wide at its middle with its shadow on the far side, and one awn past the tip.
    One pixel wide it was a stick, and a fan of sticks read as a broom."""
    sx, sy = (1, 0) if abs(dy) >= abs(dx) else (0, 1)
    g.set(x, y, GOLD)
    g.set(x + sx, y + sy, GOLD_SH)
    g.set(x + dx, y + dy, GOLD_LIT)
    g.set(x + dx + sx, y + dy + sy, GOLD)
    g.set(x + dx - sx, y + dy - sy, GOLD_SH)
    g.set(x + 2 * dx, y + 2 * dy, GOLD_LIT if dx == 0 else GOLD)
    g.set(x + 3 * dx, y + 3 * dy, GOLD_SH)


def seeds(g, pts):
    """Grain in the air or spilling: single bright pixels with a dark one under the larger."""
    for k, (x, y) in enumerate(pts):
        g.set(x, y, GOLD_LIT if k % 3 else GOLD_HI)


def straw_texture(g, x0, y0, x1, y1, every=3, salt=0):
    """Thatch: short wavy courses of lit straw over dark, at uneven spacing, only inside the form."""
    y = y0
    k = salt
    while y <= y1:
        for x in range(x0, x1 + 1):
            yy = y + (1 if (x * 5 + k) % 7 < 2 else 0)
            if inside(g, x, yy) and (x + k) % 5 != 0:
                g.set(x, yy, GOLD if (x + k) % 3 else GOLD_LIT)
                if inside(g, x, yy + 1):
                    g.set(x, yy + 1, GOLD_DP)
        y += every + (k % 2)
        k += 3


# =================================================================================================
# THE ELEVEN
# =================================================================================================

def gleaner():
    """BULK. A field hand stooped under her gleanings: a bound sheaf on her back, its ears splayed
    above her head in a fan.

    The fan is the attachment, and it is drawn as STALKS - each a line from the band to an ear, the
    floor showing between them - because a sheaf drawn as a filled wedge reads as a broom. The
    bundle is an hourglass: ears fanned above the band, cut butts splayed below it behind her back.
    She is stooped low and forward, head under a kerchief, one arm hanging with a few stalks she has
    picked up: the people of the land in linen, the gold only in what she gathered.
    """
    g = G(CELL)
    band = (11.0, 13.0)
    # THE SHEAF: ears fanned high above her head. Ends at row 5 at the highest, so the heads with
    # their awns stay clear of the two rows the walk's bob lifts into.
    for (ex, ey, dx, dy, tone) in ((4, 10, -1, -1, GOLD_SH), (5, 7, -1, -1, GOLD), (7, 6, 0, -1, GOLD_SH),
                                   (10, 5, 0, -1, GOLD), (13, 5, 0, -1, GOLD_SH), (16, 6, 1, -1, GOLD),
                                   (18, 8, 1, -1, GOLD_SH), (8, 7, -1, -1, GOLD_DP), (11, 6, 0, -1, GOLD_DP)):
        stalk(g, band[0], band[1], ex, ey, tone, GOLD_LIT if tone != GOLD_DP else None)
        ear(g, ex, ey, dx, dy)
    # THE BODY: stooped, the back a hump under the sheaf, falling away to a long skirt.
    discs(g, [(13.0, 15.5, 4.6), (12.5, 20.5, 5.4), (13.0, 25.0, 6.0), (17.0, 17.0, 4.2)], "E", "e", "A")
    # The skirt to the ground, ragged.
    mass(g, [(24, 7, 19), (29, 6, 20)], "E", "e", "A")
    for (x, d) in zip(range(6, 21), [1, 0, 1, 2, 1, 0, 1, 1, 2, 0, 1, 2, 1, 0, 1]):
        for k in range(1, d + 1):
            g.set(x, 29 + k, "A" if k < d else "E")
    cut(g, (9, 30), (10, 30), (16, 30))
    fold(g, [(9, 18), (8, 24), (9, 29)], "A", "e")
    fold(g, [(13, 20), (12, 25), (12, 29)], "A", "e")
    # THE APRON, paler, down her front, a pocket bulging with ears.
    for y in range(19, 29):
        x0 = 16 + (1 if y < 22 else 0)
        g.span(y, x0, x0 + 3, "e")
        g.set(x0, y, "l")
        g.set(x0 + 3, y, "E")
    g.span(23, 16, 19, "A")
    for (x, y, ch) in ((17, 22, GOLD), (18, 21, GOLD_LIT), (19, 22, GOLD_SH), (17, 21, GOLD_SH)):
        g.set(x, y, ch)
    # THE BUNDLE lying down her back from the band to the cut butts: three straws wide, lit on the
    # key side, so the sheaf is an object she carries and not a crest she grew.
    for (x0, y0, x1, y1, tone) in ((10, 13, 4, 21, GOLD), (11, 13, 5, 22, GOLD_SH), (12, 14, 7, 22, GOLD_DP),
                                   (9, 13, 3, 20, GOLD_LIT)):
        stalk(g, x0, y0, x1, y1, tone, GOLD_LIT if tone == GOLD else None)
    for (x, y) in ((3, 20), (4, 21), (5, 22), (7, 22)):
        g.set(x, y, GOLD_HI if (x + y) % 2 else GOLD_LIT)   # the cut ends, catching the key
    # THE BAND that ties the sheaf, across her back.
    for (x, y, ch) in ((9, 12, "l"), (10, 13, "e"), (11, 14, "A"), (12, 14, "e"), (10, 12, "L"), (11, 13, "l"),
                       (13, 15, "A")):
        g.set(x, y, ch)
    # THE HEAD, forward and low under a kerchief.
    discs(g, [(21.5, 14.0, 3.0), (22.5, 16.0, 2.4)], "e", "l", "E", rim=False)
    roster.void(g, 15, 16, 1.2, 1.0, cx=24.0)
    g.span(14, 22, 25, "E")                             # the kerchief's brim, shading the face
    g.set(20, 12, "L")
    g.set(19, 14, "A")                                  # the knot of the kerchief at the nape
    g.set(18, 15, "e")
    # THE ARM, hanging forward, a handful of gleaned stalks in it.
    _limb(g, 19, 18, 22, 24, 1.0, "E", "e", "A")
    g.rect(22, 24, 23, 25, "n")
    g.set(22, 24, "k")
    for (x0, y0, x1, y1) in ((23, 24, 25, 19), (22, 25, 24, 20), (23, 25, 26, 21)):
        stalk(g, x0, y0, x1, y1, GOLD_SH, GOLD)
    for (x, y) in ((25, 18), (24, 19), (26, 20)):
        g.set(x, y, GOLD_LIT)
    # A bare foot under the hem.
    g.span(FEET, 19, 21, "n")
    g.set(19, FEET, "k")
    rim(g, 3, 29, skip="o" + HAY)
    contact(g, 4, 21)
    eyes(g, 15, 23, 25, "Y", "y")
    return g.rows()


def rook():
    """FAST, and the Sower's minion. A rook the size of a dog.

    A flyer, by section 4's rule: the wings are wider than anything else on it and nothing touches
    the ground. A ROOK, not a bat and not a vulture: feathered wings with the primaries splayed like
    fingers - ragged, every other one missing - and a heavy pale beak, the bare grey-white face a rook
    has at the base of it. Stone in its dark stops with a cold lit sheen along the coverts; the beak
    is bone, the brightest thing on it after the eye.
    """
    g = G(CELL)

    def wing(root, tip, rise, depth, fingers, side, near):
        (rx, ry), (tx, ty) = root, tip
        n = abs(tx - rx)
        for i in range(n + 1):
            t = i / float(n)
            x = rx + side * i
            y = int(round(ry + (ty - ry) * t - rise * 4.0 * t * (1.0 - t)))
            outer = i > n - fingers
            if outer and (n - i) % 2 == 1:
                d = 1
            elif outer:
                d = int(round(depth * 0.9 + 1))
            else:
                d = int(round(depth * (1.0 - 0.4 * t))) + (0, 1, 0, -1, 1, 0)[i % 6]
            for k in range(1, d + 1):
                if k == 1:
                    ch = "p" if near else "N"
                elif k == d:
                    ch = "J"
                elif (i + k) % 3 == 0:
                    ch = "N" if near else "j"
                else:
                    ch = "j"
                g.set(x, y + k, ch)
            g.set(x, y, "N" if near else "j")
            if i % 4 == 2 and not outer:
                g.set(x, y + 1, "P" if near else "N")

    # The far wing first, up behind; the near wing over it, lower and larger.
    wing((18, 11), (27, 3), 1.6, 5.0, 4, +1, False)
    wing((14, 12), (3, 4), 2.2, 6.0, 5, -1, True)
    # THE BODY: a dark teardrop, gliding, the tail fanned behind and below.
    discs(g, [(15.0, 15.5, 3.4), (19.0, 15.0, 3.0), (11.5, 16.5, 2.2)], "j", "N", "J")
    for (x, y, ch) in ((7, 17, "j"), (8, 18, "J"), (6, 18, "N"), (7, 19, "J"), (9, 18, "j"), (8, 19, "N"),
                       (6, 20, "J"), (9, 19, "J")):
        g.set(x, y, ch)                                 # the tail, wedge-ended and ragged
    fold(g, [(13, 15), (17, 17)], "J", "N")
    g.set(16, 13, "p")
    g.set(18, 13, "P")
    # The legs, drawn up, and the feet.
    g.set(15, 19, "J")
    g.set(16, 20, "n")
    g.set(17, 20, "m")
    # THE HEAD and THE BEAK: heavy, pale, longer than the head.
    discs(g, [(22.5, 13.5, 2.4)], "j", "N", "J", rim=False)
    g.set(21, 11, "p")
    for (x, y, ch) in ((24, 13, "k"), (25, 13, "s"), (26, 14, "k"), (27, 14, "s"), (28, 15, "n"),
                       (25, 14, "n"), (26, 15, "m"), (27, 15, "n"), (24, 14, "k"), (23, 15, "s"),
                       (24, 15, "n"), (29, 15, "m")):
        g.set(x, y, ch)
    g.set(23, 12, "J")
    # THE DETACHED SHADOW, well clear.
    g.span(FEET, 10, 21, "o")
    g.span(FEET + 1, 12, 19, "o")
    rim(g, 3, 22, skip="o")
    eyes(g, 13, 22, 21, "Y", "y")
    return g.rows()


def yoked_ox():
    """HEAVY. A plough ox under its yoke, shouldering forward, head down.

    The yoke is the attachment: a beam across the neck, seen in three-quarter so it crosses the
    body as a near-upright bar - the far end up past the withers, the near end down past the
    dewlap - with its bow round the neck. It is a DOUBLE yoke, and the near bow hangs empty: the ox
    that pulled beside this one is gone. Masses first, as foes_forest's lessons say: a heavy
    hindquarter, a barrel, a hump of shoulder bigger than both, the head low; legs paired close
    under each mass. Linen hide, lit along the back; bone horns; the yoke in stone with a lit edge.
    """
    g = G(CELL)
    # FAR LEGS, a stop darker, behind everything.
    for (x0, y0, x1, kx, ky) in ((8, 22, 9, 8, 26), (19, 22, 20, 19, 26)):
        twig(g, [(x0, y0), (kx, ky), (x1, FEET)], "A", "o")
        g.span(FEET, x1 - 1, x1 + 1, "J")
    # THE YOKE'S FAR HALF, behind the body: the beam runs away from the viewer past the withers, so
    # the hump hides its root and only the end stands up behind it, in the beam's shade tones.
    roster.shaft(g, 23, 6, 22, 13, "N", "j", "J", width=3)
    for (x, y, ch) in ((22, 5, "p"), (23, 5, "N"), (24, 5, "j"), (25, 6, "J")):
        g.set(x, y, ch)
    g.set(23, 7, "q")                                   # the far peg
    # THE BODY: hindquarter, barrel, the hump of the shoulder bigger than both, the neck.
    discs(g, [(8.5, 18.0, 4.6), (13.0, 18.5, 5.6), (17.5, 16.5, 6.0), (21.5, 18.5, 4.0)], "E", "e", "A")
    fold(g, [(10, 15), (11, 22)], "A", "e")
    fold(g, [(15, 13), (16, 22)], "A", "e")
    for (x, y) in ((6, 15), (12, 14), (16, 11), (14, 12)):
        g.set(x, y, "l")                                # the light along the spine
    g.span(23, 8, 18, "A")                              # the belly's shadow
    g.set(13, 19, "e")
    g.set(9, 20, "e")
    # The dewlap, hanging under the neck.
    for (x, y, ch) in ((21, 22, "E"), (22, 23, "E"), (21, 23, "A"), (23, 23, "A"), (22, 24, "A")):
        g.set(x, y, ch)
    # NEAR LEGS: thick, jointed, hooves.
    for (hx, hy, kx, ky, fx, w) in ((10, 21, 9, 25, 10, 1.6), (17, 21, 18, 25, 17, 1.6)):
        _limb(g, hx, hy, kx, ky, w, "E", "e", "A")
        _limb(g, kx, ky, fx, FEET - 1, w * 0.9, "E", "e", "A")
        g.span(FEET, fx - 1, fx + 1, "J")
        g.set(fx - 1, FEET, "N")
    # The tail, hanging, a tuft.
    twig(g, [(4, 15), (3, 20), (4, 23)], "E", "A")
    g.set(3, 24, "A")
    g.set(4, 24, "e")
    # THE HEAD, low and forward, broad brow, the muzzle paler.
    discs(g, [(25.0, 19.5, 3.2), (27.8, 22.0, 2.3)], "E", "e", "A", rim=False)
    for (x, y, ch) in ((28, 22, "e"), (29, 22, "E"), (29, 21, "e"), (28, 23, "A"), (29, 23, "o"),
                       (27, 21, "l")):
        g.set(x, y, ch)
    g.set(23, 21, "A")
    # HORNS, bone, sweeping out and forward from the poll, the near one long.
    for (x, y, ch) in ((25, 16, "n"), (26, 15, "k"), (27, 14, "s"), (28, 13, "s"), (29, 12, "S"),
                       (27, 15, "n"), (28, 14, "n"), (24, 16, "m"), (23, 15, "n"), (23, 14, "k"),
                       (24, 13, "s")):
        g.set(x, y, ch)
    # The ear, flat out under the horn.
    g.set(22, 17, "A")
    g.set(21, 17, "e")
    # THE YOKE'S NEAR HALF: a beam across the neck. The camera looks down from the south, so a beam
    # square to an ox walking east runs up and down the screen - its far half behind the withers
    # (drawn first, above), this half over the neck and down past the chest toward the viewer, lit.
    roster.shaft(g, 22, 12, 21, 23, "P", "p", "j", width=3)
    g.span(12, 22, 24, "P")                             # where it lies on the neck: its lit top
    for (x, y, ch) in ((21, 24, "p"), (22, 24, "j"), (23, 24, "J")):
        g.set(x, y, ch)
    g.set(22, 22, "t")                                  # the near peg
    g.set(23, 23, "q")
    # The bow round the neck: bent wood, bone-pale, from the beam round under the throat.
    for (x, y, ch) in ((25, 13, "k"), (25, 14, "n"), (25, 15, "n"), (25, 16, "m"), (25, 19, "n"),
                       (24, 20, "m")):
        g.set(x, y, ch)
    # The far ox's bow, empty, hanging at the near end - the one who pulled beside it is gone.
    for (x, y, ch) in ((20, 24, "k"), (19, 25, "n"), (19, 26, "m"), (20, 27, "n"), (21, 27, "m"),
                       (22, 27, "n"), (23, 26, "m"), (24, 25, "n"), (24, 24, "k")):
        g.set(x, y, ch)
    rim(g, 5, 29, skip="o")
    contact(g, 6, 24)
    eyes(g, 18, 25, 26, "Y", "y")
    return g.rows()


def rick():
    """WALL. A rick of black hay with the shape of a man inside it, walking.

    Round, where the Hayward is a rectangle: a thatched dome as wide as a man is tall, tied with
    a rope girdle, straw hanging in a ragged skirt to the knees - and a fork DRIVEN THROUGH it, the
    ash handle standing out of the top at a slant and the iron tines out of its far side. Under the
    skirt, the man's legs; in a gap in the hay, his face. The hay is gone black from the inside, so
    it carries gold's shade with linen's deep in streaks, and only the lit courses of thatch catch
    the key.
    """
    g = G(CELL)
    # THE FORK first, so the hay closes over its middle: handle out of the top, tines out low-left.
    roster.shaft(g, 26, 3, 10, 19, "l", "e", "A", width=2)
    g.set(27, 2, "e")
    for (x, y, ch) in ((6, 21, "t"), (5, 22, "r"), (7, 22, "t"), (6, 23, "r"), (8, 23, "t"), (7, 24, "R"),
                       (9, 21, "R"), (8, 20, "r"), (5, 23, "R"), (4, 23, "T")):
        g.set(x, y, ch)
    # THE LEGS under the skirt: a man's, in dark breeches, heavy boots.
    for (lx, tone, lit) in ((12, "A", "E"), (18, "E", "e")):
        g.rect(lx, 25, lx + 2, FEET - 1, tone)
        g.line(lx, 25, lx, FEET - 1, lit)
        g.span(FEET, lx - 1, lx + 3, "J")
        g.set(lx - 1, FEET, "N")
    # THE RICK: a dome on a drum, hand-profiled so it has a shoulder and does not taper to a cone.
    SB.swell(g, 4, 25, [2.5, 6.5, 9.0, 10.2, 10.6, 10.4, 9.8, 9.2], GOLD_SH, GOLD, GOLD_DP, cx=15.5)
    # Thatch courses: wavy lines of straw, lit only on the key side of the dome and gone dark on
    # the far side, so the rick reads as round and as BLACK hay rather than as a sunlit stack.
    k = 0
    for y0 in (7, 10, 13, 16, 21, 24):
        for x in range(5, 27):
            yy = y0 + (1 if (x * 5 + k) % 7 < 2 else 0)
            if not inside(g, x, yy) or (x + k) % 6 == 0:
                continue
            if x < 12:
                tone = GOLD_LIT if (x + k) % 4 == 0 else GOLD
            elif x < 19:
                tone = GOLD if (x + k) % 3 else GOLD_SH
            else:
                tone = GOLD_SH if (x + k) % 3 else GOLD_DP
            g.set(x, yy, tone)
            if inside(g, x, yy + 1):
                g.set(x, yy + 1, GOLD_DP if x < 19 else "A")
        k += 3
    # Black streaks down it - the hay rotting from inside.
    for (x, y0, y1) in ((9, 12, 23), (21, 9, 22), (14, 15, 25), (24, 14, 24), (17, 6, 11), (11, 19, 25)):
        for y in range(y0, y1):
            if inside(g, x, y) and (x + y) % 3:
                g.set(x, y, "A" if (x + y) % 4 else "o")
    # The finial: a twist of straw tied off at the crown.
    for (x, y, ch) in ((15, 2, GOLD_LIT), (16, 2, GOLD), (15, 3, GOLD), (16, 3, GOLD_DP), (14, 4, GOLD_LIT)):
        g.set(x, y, ch)
    # The rope girdle.
    for x in range(6, 26):
        y = 18 + (1 if 9 < x < 20 else 0)
        if inside(g, x, y):
            g.set(x, y, "e" if x % 3 else "l")
            g.set(x, y + 1, "A")
    # The ragged skirt of hanging straw, to the knees.
    for (x, d) in zip(range(6, 26), [1, 2, 0, 3, 1, 2, 2, 0, 1, 3, 2, 1, 0, 2, 3, 1, 2, 0, 1, 2]):
        for k in range(1, d + 1):
            g.set(x, 25 + k, GOLD_SH if k < d else GOLD_DP)
    # THE FACE in a gap in the hay: a ragged slit, straw hanging across it.
    for (y, x0, x1) in ((11, 18, 21), (12, 17, 22), (13, 18, 21), (14, 19, 20)):
        g.span(y, x0, x1, "o")
    for (x, y, ch) in ((18, 10, GOLD), (19, 10, GOLD_LIT), (20, 10, GOLD), (21, 10, GOLD_SH),
                       (19, 11, GOLD_SH), (20, 13, GOLD_DP)):
        g.set(x, y, ch)                                 # straw over and across the gap
    rim(g, 2, 28, skip="o")
    contact(g, 7, 22)
    eyes(g, 12, 18, 21, "Y", "y")
    return g.rows()


def cunning_woman():
    """RANGED. The village's healer, taken: tall, a shawl over her head, one hand raised.

    A corn dolly floats detached at her raised hand - a little plaited figure of straw, the harvest's
    last sheaf woven into a person - and it is what she throws, so it is drawn in the EARTH ramp,
    the chapter's bolt (.ai/bestiary.md section 6). Upright and narrow where the Gleaner stoops; a
    fringed shawl breaking the shoulder line; a bunch of dried herbs at her belt, the one thing of
    hers she kept.
    """
    g = G(CELL)
    # THE SKIRT and BODY, tall and narrow, linen.
    mass(g, [(12, 12, 18), (17, 11, 19), (23, 10, 20), (29, 9, 21)], "E", "e", "A")
    fold(g, [(13, 15), (12, 22), (12, 29)], "A", "e")
    fold(g, [(16, 16), (16, 23), (17, 29)], "A", "e")
    for (x, d) in zip(range(9, 22), [1, 0, 1, 2, 1, 1, 0, 2, 1, 0, 1, 1, 0]):
        for k in range(1, d + 1):
            g.set(x, 29 + k, "A")
    cut(g, (15, 30))
    # THE SHAWL over head and shoulders, paler linen, fringed along its lower edge.
    mass(g, [(4, 13, 17), (7, 11, 19), (11, 9, 20), (15, 9, 20)], "e", "l", "E")
    for x in range(9, 21):
        if x % 2:
            g.set(x, 16, "e")
            g.set(x, 17, "E")
        else:
            g.set(x, 16, "E")
    fold(g, [(12, 8), (11, 14)], "E", "l")
    fold(g, [(17, 9), (18, 15)], "E", None)
    g.set(13, 5, "L")
    # The face, deep in the shawl.
    roster.void(g, 7, 10, 1.6, 1.2, cx=16.0)
    # HER BELT and the herbs: dried stems in lichen and gold, bound with a cord.
    g.span(19, 10, 20, "A")
    g.set(14, 19, GOLD)
    for (x, y, ch) in ((10, 20, MOSS), (9, 21, MOSS_LIT), (11, 21, MOSS_SH), (10, 22, GOLD_SH), (9, 23, MOSS),
                       (11, 23, GOLD), (10, 24, MOSS_SH), (10, 21, "A")):
        g.set(x, y, ch)
    # THE RAISED ARM, up toward the dolly; the hand open.
    _limb(g, 19, 11, 23, 8, 1.0, "e", "l", "E")
    for (x, y, ch) in ((23, 7, "k"), (24, 7, "n"), (24, 6, "k"), (23, 8, "n")):
        g.set(x, y, ch)
    # THE CORN DOLLY, detached: a little person of plaited straw - a round head, arms out, a tied
    # waist and a flaring skirt whose straw ends are left loose. Hot core where it is lit.
    dolly = [
        "..}|..",     # y 3   the head
        ".|{}\\.",    # y 4
        "..\\...",    # y 5   the neck tie
        "\\|}|\\\\",  # y 6   the arms, out
        "..}|..",     # y 7   the body
        "..\\\\..",   # y 8   the waist tie
        ".|}|}.",     # y 9   the skirt, flaring
        "\\|}|}\\",   # y 10
        "\\.\\.\\.",  # y 11  loose straw ends
    ]
    for dy, row in enumerate(dolly):
        for dx, ch in enumerate(row):
            if ch != ".":
                g.set(25 + dx, 3 + dy, ch)
    rim(g, 3, 29, skip="o" + EARTH_CORE + EARTH_HOT + EARTH_MID + EARTH_EDGE)
    contact(g, 9, 21)
    eyes(g, 8, 15, 17, "Y", "y")
    return g.rows()


def weathercock():
    """TURRET. An iron cockerel he set on a post in the middle of the field. BUILT, so it burns.

    The top of the post is the attachment: the vane's cross-arms - one across, one foreshortened
    toward the viewer - and above them the cockerel, its tail a fan of sickle feathers taller than
    its body. Steel in its dark stops, worked and riveted; the post is weathered stone-grey timber in
    a cairn of field stones. Its eye is the only fire in the chapter, with a seam of it under the
    wing where the plates do not meet.
    """
    g = G(CELL)
    # THE CAIRN at the foot.
    discs(g, [(12.0, 27.5, 2.6), (17.5, 27.0, 3.0), (15.0, 25.5, 2.4), (20.5, 28.5, 1.8)], "N", "p", "J")
    for (x, y) in ((11, 26), (16, 25), (19, 26)):
        g.set(x, y, "P")
    # THE POST.
    for y in range(9, 26):
        g.set(14, y, "p")
        g.set(15, y, "N")
        g.set(16, y, "j")
        if y % 5 == 2:
            g.set(15, y, "J")
    g.set(14, 24, "J")
    # THE CROSS-ARMS: east-west across, north-south foreshortened, each tipped with a letter-plate.
    g.span(15, 5, 25, "R")
    g.span(14, 5, 25, "r")
    g.set(5, 14, "t")
    g.line(12, 18, 18, 12, "R")
    g.line(12, 17, 18, 11, "r")
    for (x, y, ch) in ((4, 13, "t"), (4, 14, "r"), (4, 15, "R"), (3, 14, "R"),
                       (26, 13, "r"), (26, 14, "R"), (26, 15, "q"), (27, 14, "q"),
                       (19, 10, "t"), (19, 11, "R"), (20, 10, "R"),
                       (11, 18, "r"), (11, 19, "q"), (10, 19, "R")):
        g.set(x, y, ch)
    g.disc(15.0, 14.5, 1.2, "R")
    g.set(14, 14, "t")
    # THE SPINDLE.
    g.line(15, 9, 15, 12, "r")
    g.set(16, 10, "q")
    # THE COCKEREL: a deep chest, a raised head with comb and wattle, and the tail fan behind.
    for (x0, y0, x1, y1) in ((10, 8, 5, 2), (10, 9, 4, 4), (11, 9, 5, 7), (11, 8, 8, 2)):
        stalk(g, x0, y0, x1, y1, "R", "r")
    for (x, y) in ((5, 2), (4, 4), (5, 7), (8, 2)):
        g.set(x, y, "t")
    discs(g, [(15.0, 7.5, 3.4), (12.0, 7.5, 2.4)], "R", "r", "q")
    discs(g, [(19.0, 5.0, 1.8)], "R", "r", "q", rim=False)
    for (x, y, ch) in ((19, 2, "r"), (20, 2, "t"), (18, 3, "r"), (21, 3, "R"),     # the comb
                       (21, 5, "t"), (22, 5, "r"), (22, 6, "R"),                   # the beak
                       (20, 7, "q"), (20, 8, "R")):                                # the wattle
        g.set(x, y, ch)
    for (x, y) in ((13, 6), (15, 5), (12, 8)):
        g.set(x, y, "t")                                # rivets along the wing plate
    fold(g, [(13, 7), (17, 9)], "q", "r")
    g.set(15, 9, "~")                                   # the fire in the seam under the wing
    g.set(16, 9, "=")
    rim(g, 2, 28, skip="o", min_run=2)
    contact(g, 9, 22)
    eyes(g, 4, 19, 20, "+", "-")
    return g.rows()


def boar():
    """CHARGER. An orchard boar, head down to charge.

    Low to the ground, the opposite of the ox: humped bristled shoulders far higher than the rump,
    a long wedge of a head carried at the knee, and the tusks curling up forward of the snout -
    bone, the pale point everything else leads to. Linen in its deep stops for the hide; the mane of
    bristles along the hump is STONE, so the boar and the ox differ by material as well as by shape.
    """
    g = G(CELL)
    # FAR LEGS.
    for (x0, x1) in ((8, 9), (17, 18)):
        twig(g, [(x0, 23), (x1, FEET)], "A", "o")
    # THE BODY: a small low rump, a deep barrel, and the shoulders massive and high in front - a
    # boar's weight is all forward. Masses, not a ridge of spikes: the spikes came out a stegosaurus.
    discs(g, [(8.0, 21.5, 3.6), (12.5, 20.5, 4.9), (17.0, 18.5, 5.6), (20.0, 19.5, 4.4)], "A", "E", "o")
    for (x, y) in ((7, 18), (11, 16), (15, 13), (18, 13)):
        g.set(x, y, "E")
    g.set(14, 14, "e")
    # THE MANE: a fringe of bristles along the top line of the hump, stone, one or two pixels high
    # and uneven - a crest, not plates.
    for x in range(10, 24):
        top = next((y for y in range(32) if inside(g, x, y)), None)
        if top is None:
            continue
        h = (1, 2, 1, 2, 2, 1, 2, 1, 1, 2, 1, 2, 1, 1)[x - 10]
        for k in range(1, h + 1):
            g.set(x, top - k, ("p" if x < 17 else "N") if k == h else "j")
        g.set(x, top, "J" if x % 3 else "N")
    # Bristle down the flank and shoulder: stone hatching that follows the hair.
    for (x, y) in ((10, 19), (13, 18), (16, 17), (19, 16), (14, 21), (18, 20), (11, 22), (21, 18)):
        g.set(x, y, "j")
        g.set(x + 1, y + 1, "J")
    # NEAR LEGS, short and thick, cloven.
    for (hx, fx) in ((10, 10), (18, 19)):
        _limb(g, hx, 23, fx, FEET - 1, 1.4, "A", "E", "o")
        g.span(FEET, fx - 1, fx + 1, "J")
        g.set(fx, FEET, "o")
    # The tail, thin, with a tuft.
    twig(g, [(5, 19), (4, 21)], "E", "A")
    g.set(3, 22, "A")
    g.set(4, 22, "E")
    # THE HEAD: a long wedge carried low, from the shoulders to a snout near the ground.
    poly(g, [(21, 15), (25, 18), (29, 23), (29, 26), (25, 27), (21, 24)], "A")
    for (x, y, ch) in ((22, 16, "E"), (23, 17, "e"), (24, 18, "E"), (25, 19, "e"), (26, 20, "E"),
                       (27, 21, "E"), (28, 22, "e")):
        g.set(x, y, ch)                                 # the lit ridge of the face
    g.line(22, 25, 27, 26, "o")                         # the jaw line
    for (x, y, ch) in ((29, 23, "n"), (30, 24, "m"), (29, 25, "m"), (30, 25, "o"), (29, 24, "k")):
        g.set(x, y, ch)                                 # the snout's disc
    # The ear, laid back.
    for (x, y, ch) in ((21, 14, "E"), (20, 13, "e"), (22, 15, "A")):
        g.set(x, y, ch)
    # THE TUSKS, curling up and forward of the snout, the near one long.
    for (x, y, ch) in ((26, 25, "n"), (27, 24, "k"), (28, 23, "s"), (28, 22, "S"), (29, 21, "s"),
                       (24, 24, "k"), (25, 23, "s")):
        g.set(x, y, ch)
    rim(g, 11, 29, skip="o")
    contact(g, 6, 22)
    eyes(g, 19, 23, 22, "Y", "y")
    return g.rows()


def tithe_sack():
    """EXPLODER. A grain sack of the tithe, swollen past its seams, on two stumps.

    The only swollen thing in the Fields, and the swell is the attachment, with the tied neck on top
    - the gathered cloth flaring above a rope - which no other body here has. Hessian in linen, lit
    upper-left; the side seam splitting, and GOLD where it splits, the grain he took pressing out.
    It FACES LEFT, like every exploder before it, because its stand-in says
    SpriteFacesRightByDefault = false.
    """
    g = G(CELL)
    # THE STUMPS, short and thick, under the swell.
    for (lx, tone) in ((12, "A"), (18, "E")):
        g.rect(lx, 25, lx + 2, FEET - 1, tone)
        g.set(lx, 25, "e")
        g.span(FEET, lx - 1, lx + 3, "J")
    # THE SACK: a hand-profiled swell, widest low, the neck gathered at the top.
    SB.swell(g, 7, 26, [3.0, 6.5, 9.0, 10.4, 10.8, 10.4, 9.0], "e", "l", "E", cx=16.0)
    # Its roundness: the far lower side falls into shade, the key side lifts - a swell, not a slab.
    for y in range(9, 27):
        for x in range(5, 28):
            if g.get(x, y) != "e":
                continue
            u, v = (x - 16.0) / 10.6, (y - 18.0) / 9.5
            k = u * 0.75 + v * 0.65
            if k > 0.62:
                g.set(x, y, "E" if (x + y) % 5 else "A")
            elif k > 0.45 and (x + y) % 2:
                g.set(x, y, "E")
            elif k < -0.55 and (x * 3 + y) % 4 == 0:
                g.set(x, y, "l")
    # The stress creases running out from the tie, where the swell pulls the cloth tight.
    fold(g, [(14, 9), (11, 13), (10, 17)], "E", "l")
    fold(g, [(18, 9), (21, 12), (23, 16)], "E", "l")
    fold(g, [(16, 10), (16, 13)], "E", "l")
    # The sack's bottom corners, dog-eared out either side - every full sack has them.
    for (x, y, ch) in ((6, 24, "l"), (5, 25, "e"), (6, 25, "E"), (26, 24, "E"), (27, 25, "A"), (26, 25, "A")):
        g.set(x, y, ch)
    # Hessian weave: a few lit and dark threads, uneven.
    for (x, y) in ((10, 12), (13, 11), (19, 11), (11, 17), (22, 15), (14, 21), (20, 20), (9, 22), (17, 14),
                   (12, 24), (23, 21), (8, 19)):
        g.set(x, y, "E")
        g.set(x + 1, y, "l" if (x + y) % 2 else "e")
    # A patch sewn over an old tear, low on the far side.
    g.rect(20, 21, 23, 24, "A")
    for (x, y) in ((20, 21), (22, 21), (23, 23), (20, 24), (21, 23)):
        g.set(x, y, "e")                                # the stitches
    # The stencilled tithe mark, a cross in a ring, faded.
    for (x, y) in ((17, 16), (18, 17), (19, 18), (19, 16), (17, 18), (18, 15), (18, 19), (16, 17), (20, 17)):
        g.set(x, y, "A" if (x + y) % 2 else "E")
    # THE SPLITTING SEAM down its left (its front), gold pressing out of it, grain trickling.
    for (y, x) in ((11, 9), (13, 8), (15, 7), (17, 7), (19, 7), (21, 7), (23, 8)):
        g.set(x, y, "o")
        g.set(x + 1, y, GOLD_DP)
        g.set(x, y + 1, GOLD if y % 4 == 1 else GOLD_LIT)
        g.set(x + 1, y + 1, GOLD_SH)
    seeds(g, [(5, 16), (6, 20), (5, 24), (6, 27), (4, 21)])
    # THE NECK: the gathered cloth above the rope, flaring, its folds catching the key.
    mass(g, [(3, 12, 20), (5, 13, 19), (7, 14, 18)], "e", "l", "E")
    for (x, y) in ((13, 3), (15, 4), (17, 3), (19, 4)):
        g.set(x, y, "E")
    for x in range(13, 20):
        g.set(x, 8, "A" if x % 2 else "e")              # the rope, wound twice
        g.set(x, 7, "l" if x % 3 == 0 else "A")
    for (x, y, ch) in ((20, 8, "e"), (21, 9, "A"), (21, 10, "e"), (22, 11, "A")):
        g.set(x, y, ch)                                 # the rope's loose end
    rim(g, 3, 29, skip="o" + HAY)
    contact(g, 8, 22)
    eyes(g, 13, 11, 13, "Y", "y")
    return g.rows()


def haymaker():
    """SLAMMER. A field hand driving a hay-fork down two-handed: the tines are the highest thing.

    Wound back over the head, both arms up, the iron tines raised behind - the ring is the fork
    coming down. Upright and square under a broad straw hat, where the Gleaner stoops and the Sower
    strides: the three people of the field are told apart by stance before tool. The tines are steel
    in their lit stops, the brightest metal here.
    """
    g = G(CELL)
    # THE FORK first: the ash handle from behind the shoulder up over the head, and the head of the
    # fork raised behind and above everything - an iron ferrule and three long tines fanned out.
    roster.shaft(g, 22, 15, 10, 7, "l", "e", "A", width=2)
    for (x, y, ch) in ((9, 6, "r"), (8, 6, "R"), (9, 7, "q"), (8, 5, "r"), (7, 6, "q")):
        g.set(x, y, ch)                                 # the ferrule
    for (pts, lit) in (([(7, 5), (5, 4), (3, 3), (2, 3)], "t"),
                       ([(8, 4), (7, 3), (6, 2)], "T"),
                       ([(9, 5), (10, 4), (11, 3), (11, 2)], "t")):
        for i, (x, y) in enumerate(pts):
            g.set(x, y, lit if i else "r")
            if g.get(x, y + 1) == ".":
                g.set(x, y + 1, "q")
        g.set(pts[-1][0], pts[-1][1], "T")              # the points catch the key
    # LEGS, apart and planted.
    for (lx, tone, lit) in ((11, "J", "N"), (17, "j", "N")):
        g.rect(lx, 24, lx + 2, FEET - 1, tone)
        g.line(lx, 24, lx, FEET - 1, lit)
        g.span(FEET, lx - 1, lx + 3, "J")
        g.set(lx - 1, FEET, "N")
    g.rect(14, 26, 16, FEET, "o")
    # THE SMOCK: broad at the shoulder, belted, linen.
    SB.swell(g, 12, 25, [6.0, 6.2, 5.4, 5.0, 5.8], "E", "e", "A", cx=15.0)
    fold(g, [(12, 14), (11, 19), (12, 24)], "A", "e")
    fold(g, [(18, 14), (19, 20), (18, 24)], "A", None)
    for (x, y) in ((14, 14), (15, 16), (14, 18)):
        g.set(x, y, "l")                                # smocking stitches down the front
    g.span(20, 9, 21, "A")                              # the cord belt
    g.set(16, 20, GOLD_SH)
    # THE HEAD under a broad straw hat: the brim is gold's lit stop over its shade. Drawn before the
    # arms, so the hands grip the handle in front of the hat's brim.
    g.rect(13, 10, 17, 12, "A")
    roster.void(g, 10, 12, 1.4, 1.0, cx=15.5)
    g.span(9, 11, 20, GOLD_SH)
    g.span(8, 12, 19, GOLD)
    g.set(11, 9, GOLD)
    g.span(7, 13, 17, GOLD_LIT)
    g.span(6, 14, 16, GOLD)
    g.set(13, 8, GOLD_LIT)
    g.set(18, 8, GOLD_DP)
    g.set(16, 7, GOLD_DP)                               # the crown's band
    # ARMS: both up and bent, gripping the handle; daylight between each arm and the hat.
    _limb(g, 9, 13, 12, 9, 1.4, "E", "e", "A")
    _limb(g, 21, 13, 19, 12, 1.4, "E", "e", "A")
    for (x, y) in ((12, 8), (18, 11)):
        g.rect(x, y, x + 1, y + 1, "n")
        g.set(x, y, "k")
    g.set(10, 11, "o")
    rim(g, 2, 29, skip="o")
    contact(g, 8, 22)
    eyes(g, 11, 14, 16, "Y", "y")
    return g.rows()


def sower():
    """SUMMONER, who calls down the Harvest Rooks. A sower casting seed from a basket at the hip.

    One arm flung wide, the hand open at the end of the throw, and the seed in the air in an arc in
    front of it - the gesture is the attachment, and the seed-lip, the kidney-shaped basket slung at
    the hip on a strap, is the second read. Striding, mid-cast. Linen, with the gold only in the seed,
    because the seed is what the rooks come down for.
    """
    g = G(CELL)
    # THE BACK LEG and THE FRONT LEG, striding.
    _limb(g, 12, 21, 9, FEET - 1, 1.5, "A", "E", "o")
    g.span(FEET, 7, 10, "J")
    _limb(g, 16, 21, 19, FEET - 1, 1.5, "E", "e", "A")
    g.span(FEET, 18, 21, "J")
    g.set(18, FEET, "N")
    # THE BODY: a short coat, belted, leaning into the throw.
    mass(g, [(9, 12, 17), (14, 11, 18), (19, 11, 18), (23, 10, 19)], "E", "e", "A")
    fold(g, [(13, 11), (12, 17), (13, 22)], "A", "e")
    fold(g, [(16, 12), (17, 22)], "A", None)
    for (x, d) in zip(range(10, 20), [1, 2, 1, 0, 1, 1, 2, 0, 1, 1]):
        for k in range(1, d + 1):
            g.set(x, 23 + k, "A")
    # THE STRAP across the chest to THE SEED-LIP at the far hip.
    g.line(17, 10, 9, 18, "A")
    g.line(17, 11, 10, 18, "e")
    for (y, x0, x1) in ((16, 4, 10), (17, 3, 11), (18, 3, 11), (19, 4, 10), (20, 5, 9)):
        g.span(y, x0, x1, GOLD_SH)
        g.set(x0, y, GOLD)
        g.set(x1, y, GOLD_DP)
    g.span(16, 4, 10, GOLD)                             # the rim, lit
    g.span(15, 5, 9, GOLD_LIT)
    for (x, y) in ((5, 18), (7, 19), (9, 17), (6, 17)):
        g.set(x, y, GOLD_DP)                            # the weave
    for (x, y) in ((6, 14), (8, 14), (7, 13)):
        g.set(x, y, GOLD_LIT)                           # seed heaped in it
    # The near hand down in the basket.
    _limb(g, 12, 12, 10, 16, 1.0, "E", "e", "A")
    # THE FLUNG ARM, out wide and up, the hand open at the end of the throw.
    _limb(g, 17, 10, 25, 8, 1.2, "e", "l", "E")
    for (x, y, ch) in ((26, 7, "k"), (27, 7, "s"), (26, 8, "n"), (27, 6, "n"), (28, 8, "k")):
        g.set(x, y, ch)
    # THE SEED, in the air: an arc thrown forward and falling.
    seeds(g, [(29, 6), (30, 9), (28, 11), (30, 13), (27, 13), (29, 16), (26, 16), (28, 19), (30, 18),
              (25, 20)])
    # THE HEAD in a close hood, turned to the throw.
    discs(g, [(15.5, 6.5, 2.8)], "E", "e", "A")
    g.set(14, 4, "l")
    roster.void(g, 6, 8, 1.2, 1.0, cx=16.6)
    rim(g, 3, 29, skip="o" + HAY)
    contact(g, 7, 21)
    eyes(g, 7, 16, 18, "Y", "y")
    return g.rows()


def tithe_reeve():
    """MINIBOSS, on the 48 cell. His jailer on the land: the one who counts what is owed.

    A long hooded coat to the ground, and the attachment HANGS: a balance scale held out at arm's
    length, its beam tipped, the near pan heavy with gold - the tithe being weighed - and the far pan
    empty. A bundle of notched tally-sticks at his belt, which is how a reeve kept account. Linen
    coat in its deep stops; the scale steel; the gold in the pan and on the buckle the only warm
    things on him, and they are not his.
    """
    c = 48
    feet = c - 2
    g = G(c)
    # THE COAT: long, hooded, split at the hem, a little stoop in the shoulders.
    mass(g, [(14, 16, 28), (20, 14, 30), (28, 13, 31), (36, 12, 32), (44, 11, 33)], "E", "e", "A")
    for pts in ([(17, 16), (15, 28), (14, 44)], [(22, 18), (21, 30), (20, 44)], [(27, 19), (28, 32), (29, 44)]):
        fold(g, pts, "A", "e")
    for (x, d) in zip(range(11, 34), [1, 2, 1, 0, 1, 2, 2, 1, 0, 1, 0, 2, 1, 1, 0, 1, 2, 1, 0, 1, 1, 2, 1]):
        for k in range(1, d + 1):
            g.set(x, 44 + k, "A" if k < d else "E")
    cut(g, (21, 45), (22, 45), (21, 44))
    # boots under the hem
    for (bx, tone) in ((15, "J"), (25, "j")):
        g.span(feet, bx, bx + 4, tone)
        g.set(bx, feet, "N")
    # Buttons down the front, and the lapel.
    for y in (21, 25, 29, 33):
        g.set(24, y, "r")
        g.set(24, y + 1, "q")
    fold(g, [(22, 17), (23, 24)], "A", "l")
    # THE BELT, a gold buckle, and THE TALLY-STICKS hung at the hip.
    g.span(30, 13, 31, "J")
    g.span(31, 13, 31, "o")
    g.rect(22, 29, 24, 31, GOLD_SH)
    g.set(22, 29, GOLD_LIT)
    g.set(23, 30, "o")
    # Bone-pale wood in its shade stops, so the sticks read as a bundle without being the brightest
    # thing on him; the lit stop only on their cut tops.
    for (x0, x1, tone) in ((13, 12, "k"), (15, 15, "n"), (17, 18, "k"), (16, 17, "n"), (14, 14, "n")):
        for y in range(31, 41):
            x = x0 + (x1 - x0) * (y - 31) / 9.0
            g.set(x, y, tone if y % 3 else "m")         # notches cut in each stick
        g.set(x0, 31, "s")
    g.span(33, 12, 18, "A")                             # the cord binding them
    g.set(12, 33, "e")
    # THE HOOD.
    discs(g, [(22.0, 10.0, 5.2), (20.5, 12.5, 4.6)], "E", "e", "A")
    g.set(18, 6, "l")
    g.set(17, 7, "l")
    roster.void(g, 9, 14, 2.6, 1.8, cx=24.0)
    # THE FAR ARM, hanging, the hand in shadow.
    _limb(g, 15, 18, 12, 28, 1.8, "A", "E", "o")
    g.rect(11, 28, 13, 30, "m")
    # THE NEAR ARM, held out at length, the hand gripping the scale's hanger.
    _limb(g, 28, 18, 36, 20, 1.6, "E", "e", "A")
    g.rect(36, 19, 38, 21, "n")
    g.set(36, 19, "k")
    # THE SCALE: a hanger from the fist, a beam tipped down toward the heavy pan, chains to two pans.
    g.line(37, 22, 37, 23, "r")
    g.set(37, 24, "t")                                  # the pivot
    g.line(28, 28, 46, 22, "R")                         # the beam
    g.line(28, 27, 46, 21, "r")
    g.set(28, 27, "t")
    g.set(46, 21, "t")
    for (x0, y0, x1, y1) in ((28, 29, 25, 36), (28, 29, 31, 36), (46, 23, 43, 29), (46, 23, 47, 29)):
        n = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(n + 1):
            t = i / float(n)
            g.set(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, "r" if i % 2 else "q")
    # The near pan, low, heaped with coin - the tithe - and the far pan high and empty.
    for (y, x0, x1, tone) in ((37, 24, 32, "r"), (38, 25, 31, "R"), (39, 26, 30, "q")):
        g.span(y, x0, x1, tone)
    g.set(24, 37, "t")
    for (x, y, ch) in ((26, 36, GOLD), (27, 35, GOLD_LIT), (28, 36, GOLD_SH), (29, 35, GOLD), (30, 36, GOLD_SH),
                       (28, 34, GOLD_HI), (27, 36, GOLD_DP), (25, 36, GOLD_SH)):
        g.set(x, y, ch)
    for (y, x0, x1, tone) in ((30, 42, 47, "r"), (31, 43, 46, "q")):
        g.span(y, x0, x1, tone)
    g.set(42, 30, "t")
    rim(g, 4, 46, skip="o")
    contact(g, 12, 33, y=feet)
    eyes(g, 12, 23, 26, "Y", "y")
    return g.rows()


CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    ("fields-gleaner", gleaner, 32, (0.7, 2.2, 2), "FieldsGleanerFrames"),
    ("fields-rook", rook, 32, (0.8, 3.0, 3), "FieldsRookFrames"),
    ("fields-yoked-ox", yoked_ox, 32, (0.5, 3.0, 1), "FieldsYokedOxFrames"),
    ("fields-rick", rick, 32, (0.6, 2.8, 1), "FieldsRickFrames"),
    ("fields-cunning-woman", cunning_woman, 32, (0.9, 2.2, 3), "FieldsCunningWomanFrames"),
    ("fields-weathercock", weathercock, 32, (0.3, 1.6, 1), "FieldsWeathercockFrames"),
    ("fields-boar", boar, 32, (1.4, 3.6, 3), "FieldsBoarFrames"),
    # Faces left (see tithe_sack), so it leans into its strike and is knocked back the other way.
    ("fields-tithe-sack", tithe_sack, 32, (1.1, -2.0, -3), "FieldsTitheSackFrames"),
    ("fields-haymaker", haymaker, 32, (0.6, 4.0, 1), "FieldsHaymakerFrames"),
    ("fields-sower", sower, 32, (0.9, 2.6, 2), "FieldsSowerFrames"),
    ("fields-tithe-reeve", tithe_reeve, 48, (0.5, 4.2, 1), "FieldsTitheReeveFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    # Largest cell first: bestiary_kit.report sizes its contact sheet by the first creature's cell.
    bestiary_kit.run(sorted(CAST, key=lambda c: -c[2]), PALETTE, "fields")
