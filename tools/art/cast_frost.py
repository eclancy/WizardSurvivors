# -*- coding: utf-8 -*-
"""The Frozen Waste's own cast (.ai/bestiary.md section 4.6). Run: python tools/art/cast_frost.py

"A wizard lies under the ice, and the cold stands guard." The thanes, sled hounds and drum callers
of the north, frozen and taken. Eleven creatures, each one drawn pose derived to the contract's four
animations by bestiary_kit. (The Rime Guard, the Still Warden's halberdier, is already this
chapter's and is not redrawn here.)

THE ICE RULE (bestiary.md 4.6), and the reason for most of what follows. The player's Freeze tints a
frozen body pale blue (Enemy.FrozenLook multiplies it by 0.80 / 1.04 / 1.40) and marks it. A
creature drawn as a pale blue shell of ice would look frozen before anything froze it, and a frozen
one would look as though nothing had changed. So ice here is the Still Warden's ice: DARK STONE
PLANES, with the ice ramp only on their EDGES - the lit edge of a shard, the top of a crust, the tip
of an icicle - and always as an attachment (a crust, spikes, a floe, fists, a block of keys), never a
whole body. Every body underneath is a material row: wool for the furs (from its deep half, so the
tint has room to show), linen, bone, steel. tools/art/_bestiary_contact.py lays each creature beside a
copy under the Freeze tint, to check that the two never look alike.

Rows: steel, wool (blue-grey furs), bone (whalebone), stone, linen; the ice element ramp for edges
only; the arcane mark in every taken thing's face and fire in the one thing he built, the totem.
The chapter's boss is a wedge (broad frozen base, armoured shoulders), so nothing here is a wedge.
"""
from __future__ import print_function

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bestiary_kit
import roster
import sprite_bosses as SB
from cast_ruins import main, poly, rim, shade

G = roster.G
FEET = 30
PALETTE = dict(roster.PALETTE)

# The ice ramp, edge to core: "/" #2E7FC4, "?" #7FDCF8, ">" #C8F4FF, "<" #F4FEFF.


def shard(g, x, y, dx, dy, half, mid="j", lit="?", dark="J", tip=">", body="N"):
    """An ice shard: a dark stone plane with only its OUTER key-side edge lit in ice, and an ice tip.
    The plane stays dark - the Still Warden's rule - and the lit edge stops two thirds of the way
    down, so a cluster of shards reads as dark crystal catching light and not as a pale shell."""
    n = int(max(abs(dx), abs(dy)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        px, py = x + dx * t, y + dy * t
        w = half * (1.0 - t)
        g.span(py, px - w, px + w, mid if t > 0.25 else body)
        g.set(px - w, py, lit if t > 0.4 else "p")
        g.set(px + w, py, dark)
    g.set(x + dx, y + dy, tip)


def rime(g, chars, ice="?", x0=0, y0=0, x1=None, y1=None, every=1):
    """Ice along the TOP edge of a mass only, where light would catch it."""
    x1 = g.cell - 1 if x1 is None else x1
    y1 = g.cell - 1 if y1 is None else y1
    hits = []
    for x in range(x0, x1 + 1):
        if x % every:
            continue
        for y in range(y0, y1 + 1):
            ch = g.get(x, y)
            if ch in chars and g.get(x, y - 1) in (".", "W"):
                hits.append((x, y))
                break
    for x, y in hits:
        g.set(x, y, ice)


# --- the cast ---------------------------------------------------------------------------------

def rimebound():
    """BULK. A traveller the cold caught, hunched in furs under a wool hood, with a jagged crust of ice
    ridged up over the shoulders and back. The crust is dark stone shards with lit edges - an
    attachment, not a shell - and it is the read: a hunched lump with a broken crest, which no other
    body here has.

    THE FURS ARE LINEN, the hood wool. In wool all over it was the chapter's bluest body, and blue
    under the Freeze tint only gets bluer: side by side on the contact sheet a frozen Rimebound and a
    live one were hard to tell apart, and this is the body the player freezes most."""
    g = G(32)
    # the body, hunched forward: head out in front of the crust
    roster.mass(g, [(12, 10, 19), (17, 8, 21), (23, 8, 21), (27, 9, 21)], "E", "e", "A")
    roster.fold(g, [(11, 17), (10, 22), (10, 26)], "A", "e")
    roster.fold(g, [(16, 18), (17, 23), (16, 27)], "A", "e")
    roster.fold(g, [(19, 19), (20, 25)], "A", None)
    roster.fold(g, [(13, 20), (14, 24)], "A", "l")
    roster.tatters(g, [(27, 8, 12), (28, 9, 15), (27, 14, 21), (28, 17, 20)], "A", "E", "o")
    # wrapped feet, wool, peeking out under the fur
    g.span(29, 10, 13, "b")
    g.span(FEET, 9, 14, "a")
    g.set(10, 29, "c")
    g.span(29, 17, 20, "b")
    g.span(FEET, 17, 21, "a")
    g.set(17, 29, "c")
    # a fur sleeve, the arm hugged in against the cold
    g.line(16, 17, 20, 21, "E")
    g.line(17, 17, 21, 21, "A")
    g.set(21, 22, "n")
    g.set(20, 22, "k")

    # THE HOOD, pushed forward out from under the crust, fur-trimmed in linen
    roster.mass(g, [(10, 16, 21), (12, 15, 24), (16, 16, 23)], "b", "c", "a")
    roster.void(g, 12, 15, 2.0, 1.4, cx=21.0)
    for (x, y) in ((18, 11), (19, 11), (20, 11), (21, 11), (22, 11), (23, 12), (24, 13), (24, 14),
                   (23, 16), (19, 16)):
        g.set(x, y, "e" if (x + y) % 2 else "E")  # the ruff, broken
    g.set(18, 12, "l")

    # THE CRUST: a ridge of dark plates over the shoulders, shards standing up and back off it
    poly(g, [(6, 18), (8, 12), (12, 9), (17, 9), (19, 12), (15, 14), (10, 17)], "N")
    shade(g, "N", "p", "J", x1=19, y1=18)
    for (x, y, dx, dy, h) in ((8, 13, -2, -5, 1.6), (11, 10, -1, -7, 2.0), (14, 9, 1, -6, 1.8),
                              (17, 10, 2, -4, 1.4), (7, 16, -3, -2, 1.2)):
        shard(g, x, y, dx, dy, h)
    roster.fold(g, [(9, 15), (13, 12), (16, 12)], "J", "p")
    g.line(10, 17, 16, 14, "o")                   # where the crust sits on the fur

    rim(g, 2, 30)
    roster.contact(g, 7, 22)
    roster.eyes(g, 14, 20, 23, "Y", "y")
    return g.rows()


def sled_hound():
    """FAST, and the Drum-Caller's minion. A sled dog run thin: lean, long-legged, mid-stride, the
    snapped trace of its harness streaming out behind - the thing a fast body leaves behind is what
    says it is fast. Linen fur with a pale bone face and belly, a strap and a steel buckle."""
    g = G(32)
    # THE TRACE: a two-ply strap off the harness, snapped, streaming back in a wave and fraying
    for i, (x, y) in enumerate(((12, 16), (11, 15), (10, 15), (9, 15), (8, 14), (7, 13), (6, 13),
                                (5, 14))):
        g.set(x, y, "e" if i % 3 else "l")
        g.set(x, y + 1, "E")
        g.set(x, y + 2, "A")
    g.set(4, 13, "e")
    g.set(4, 15, "E")
    g.set(5, 16, "A")

    # LEGS, mid-gallop: hind pair pushing back, fore pair reaching forward. Near legs a stop lighter.
    for (pts, mid, lit, h) in ((((11, 21), (9, 25), (7, 29)), "A", "E", 0.9), (((14, 21), (14, 25), (12, 29)), "E", "e", 1.0),
                               (((20, 21), (23, 24), (26, 27)), "A", "E", 0.9), (((22, 21), (23, 25), (22, 29)), "E", "e", 1.0)):
        SB.limb(g, list(pts), h, mid, lit, "o")
    g.span(FEET, 6, 8, "A")
    g.span(FEET, 11, 13, "n")
    g.span(FEET, 21, 23, "n")
    g.span(27, 26, 28, "A")
    g.set(12, FEET, "k")
    g.set(22, FEET, "k")

    # THE MASSES: a haunch, a deep chest, a tucked waist between them
    g.disc(11.5, 18.0, 3.8, "E")
    g.disc(20.5, 17.5, 4.0, "E")
    for x in range(12, 21):
        t = (x - 12) / 8.0
        top = 14.6 + 0.8 * (1 - abs(t - 0.5) * 2)
        bot = 20.6 - 1.4 * (1 - abs(t - 0.5) * 2)
        for y in range(int(round(top)), int(round(bot)) + 1):
            g.set(x, y, "E")
    shade(g, "E", "e", "A")
    # the pale belly, chest and throat; the dark saddle down the back, broken by the strap
    for (x, y) in ((19, 20), (20, 21), (21, 21), (22, 20), (23, 19), (15, 19), (16, 19), (17, 19),
                   (18, 20), (23, 18), (24, 17)):
        g.set(x, y, "n")
    for (x, y) in ((21, 20), (22, 19), (24, 16)):
        g.set(x, y, "k")
    for x in range(10, 20):
        col = [y for y in range(12, 22) if g.get(x, y) not in (".", "W")]
        if col:
            g.set(x, col[0] + 1, "A")
            if x % 3 == 0:
                g.set(x, col[0] + 2, "A")
    roster.fold(g, [(9, 18), (11, 21)], "A", None)

    # THE HARNESS: a strap round the chest and one along the back, a steel buckle where they cross
    g.line(17, 14, 19, 22, "A")
    g.line(12, 15, 18, 15, "A")
    g.set(18, 15, "T")
    g.set(18, 16, "t")
    g.set(19, 16, "R")

    # tail, low, a short brush
    g.line(8, 17, 6, 20, "E")
    g.set(7, 17, "e")
    g.set(5, 21, "A")

    # HEAD: up and forward, ears pricked, a pale mask
    g.line(22, 16, 23, 13, "E")                   # the neck
    g.line(23, 16, 24, 13, "E")
    g.disc(24.5, 12.5, 2.6, "E")
    shade(g, "E", "e", "A", x0=21, y0=8, x1=29, y1=16)
    for (x, y) in ((26, 12), (27, 13), (28, 13), (27, 14), (28, 14), (26, 14)):
        g.set(x, y, "n")                          # muzzle, pale
    g.set(26, 13, "k")
    g.set(29, 14, "m")
    g.set(27, 15, "A")
    g.set(25, 14, "k")
    for (ex, ey) in ((23, 9), (25, 9)):
        g.set(ex, ey, "E")
        g.set(ex, ey + 1, "E")
        g.set(ex + 1, ey + 1, "A")
    g.set(23, 8, "e")
    g.set(25, 8, "E")

    rim(g, 8, 29, min_run=3)
    g.span(FEET + 1, 7, 25, "o")
    roster.eyes(g, 12, 24, 26, "Y", "y")
    return g.rows()


def floe_bearer():
    """HEAVY. A hunched figure carrying a slab of sea ice before it: the floe is a flat plane on the
    side it faces - the shield plane - in dark stone, its edges lit in ice and a fracture running
    through it. The figure behind is wool furs, wrapped round the slab's edge."""
    g = G(32)
    # the figure, hunched behind its burden. LINEN furs, not wool: the Rimebound is the chapter's
    # wool body, and two hunched wool figures would separate by shape alone.
    roster.mass(g, [(10, 9, 15), (15, 6, 18), (22, 6, 18), (28, 7, 17)], "E", "e", "A")
    roster.fold(g, [(9, 14), (8, 20), (9, 26)], "A", "e")
    roster.fold(g, [(12, 16), (12, 22), (13, 27)], "A", "e")
    roster.fold(g, [(10, 18), (11, 21)], "A", "l")
    roster.tatters(g, [(28, 6, 11), (29, 7, 14), (28, 13, 17)], "A", "E", "o")
    g.span(FEET, 7, 10, "a")
    g.set(7, FEET, "b")
    g.span(20, 7, 15, "b")                        # a wool sash across the furs
    g.span(21, 7, 15, "a")
    g.set(7, 20, "c")
    # hood, bowed, wool, with a fur ruff
    roster.mass(g, [(5, 10, 14), (8, 9, 17), (12, 10, 17)], "b", "c", "a")
    roster.void(g, 8, 11, 1.8, 1.2, cx=15.0)
    for (x, y) in ((11, 6), (12, 5), (13, 5), (14, 6), (15, 7), (16, 8), (17, 9)):
        g.set(x, y, "E" if x % 2 else "e")
    g.set(10, 6, "l")

    # THE FLOE: a slab of sea ice, tilted. Dark planes; the ice only on the top edge and one corner
    poly(g, [(17, 7), (27, 9), (26, 29), (16, 28)], "N")
    poly(g, [(17, 7), (22, 8), (21, 17), (16.5, 18)], "p")      # the upper-left face, catching the key
    poly(g, [(22, 18), (26.5, 19), (26, 29), (21, 28.5)], "j")  # the lower-right, in shade
    g.line(17, 7, 26, 9, "?")                     # the top edge, lit
    g.line(17, 7, 17, 12, "?")                    # the corner where the key strikes
    g.line(17, 13, 16, 28, "P")
    g.line(27, 10, 26, 29, "J")
    g.line(16, 28, 26, 29, "J")
    g.set(17, 7, ">")
    g.set(26, 10, "/")
    # the fracture, and two smaller cracks off it - occ, with a dark ice lip
    for pts in (((18, 14), (21, 17), (22, 21), (25, 25)), ((21, 17), (24, 15)), ((19, 23), (22, 21))):
        roster.fold(g, list(pts), "o", "/")
    g.set(21, 16, "?")
    g.set(24, 12, "J")
    g.set(25, 13, "j")
    g.set(19, 25, "j")

    # mittened hands wrapped round the floe's near edge
    g.rect(15, 12, 17, 14, "b")
    g.set(15, 12, "c")
    g.span(14, 15, 17, "a")
    g.rect(15, 22, 17, 24, "b")
    g.set(15, 22, "c")
    g.span(24, 15, 17, "a")

    rim(g, 5, 30, xmax=16)
    roster.contact(g, 6, 27)
    roster.eyes(g, 10, 14, 16, "Y", "y")
    return g.rows()


def thane():
    """WALL. A northern lord with frost in his beard: the textbook bruiser - a horned helm and a fur
    mantle breaking the shoulder line - in steel mail under blue-grey fur, his beard bone-pale with
    ice at its tips."""
    g = G(32)
    # LEGS, planted, wrapped
    for (lx0, lx1) in ((10, 13), (17, 20)):
        g.rect(lx0, 25, lx1, FEET, "a")
        g.line(lx0, 25, lx0, FEET, "b")
        g.span(28, lx0, lx1, "E")                 # the leg-wrapping, crossed once
        g.set(lx0 + 1, 27, "E")
        g.span(FEET, lx0 - 1, lx1 + 1, "q")
        g.set(lx0 - 1, FEET, "R")
    # THE BODY: steel mail, broad, belted
    roster.mass(g, [(13, 9, 22), (20, 8, 22), (26, 9, 21)], "R", "r", "q")
    SB.ridges(g, 14, 25, "R", "q", "r", step=2, jitter=7)
    g.span(22, 8, 22, "o")
    g.span(21, 8, 22, "a")
    g.set(15, 21, "g")
    g.set(16, 21, "G")
    g.set(16, 22, "u")
    roster.tatters(g, [(25, 8, 13), (26, 9, 22), (25, 18, 22)], "R", "r", "o")

    # THE MANTLE: shaggy fur out past the shoulders, its lower edge in tufts
    roster.mass(g, [(10, 9, 21), (12, 5, 26), (15, 4, 27)], "b", "c", "a")
    for (x, y0, y1) in ((4, 15, 17), (5, 15, 18), (7, 15, 17), (9, 15, 19), (11, 15, 16),
                        (20, 15, 17), (22, 15, 19), (24, 15, 18), (26, 15, 17), (27, 15, 16)):
        for y in range(y0, y1 + 1):
            g.set(x, y, "a" if y == y1 else "b")
    for (x, y) in ((6, 13), (8, 12), (23, 12), (25, 13), (10, 14), (21, 14)):
        g.set(x, y, "c")
    g.set(5, 12, "d")
    roster.fold(g, [(8, 12), (9, 16)], "a", None)
    roster.fold(g, [(23, 12), (22, 16)], "a", None)

    # THE BEARD: bone, long, split, with ice in its tips
    roster.hair(g, 15.5, 11, 19, 3.0, 2.0, "n", "k", "m", parted=True)
    g.set(13, 19, "?")
    g.set(17, 19, ">")
    g.set(15, 20, "?")
    g.set(18, 18, "/")
    g.set(12, 17, "/")
    g.span(10, 13, 18, "k")                       # the moustache
    g.set(15, 10, "m")

    # THE HELM: steel, a nose-guard, two horns sweeping up and out
    roster.mass(g, [(4, 12, 19), (9, 11, 20)], "R", "r", "q")
    g.span(4, 13, 18, "r")
    g.span(3, 14, 17, "R")
    g.span(7, 12, 19, "o")                        # the eye-line under the brow
    g.span(8, 12, 19, "o")
    g.line(15, 5, 15, 9, "r")                     # the nose-guard
    g.set(16, 6, "q")
    for (pts, lit, mid) in ((((11, 6), (9, 4), (7, 2)), "k", "n"), (((20, 6), (22, 4), (24, 2)), "n", "m")):
        SB.limb(g, list(pts), 0.8, mid, lit, "m")
    g.set(7, 2, "s")
    g.set(24, 2, "k")

    rim(g, 2, 30)
    roster.contact(g, 8, 23)
    roster.eyes(g, 8, 13, 18, "Y", "y")
    return g.rows()


def rime_seer():
    """RANGED. A seer wrapped in furs, tall and narrow, a fur-trimmed hood and strings of bone beads,
    and an ice shard hanging DETACHED at her shoulder - the caster's weapon in the chapter's element,
    the same ice as the bolt it throws."""
    g = G(32)
    roster.mass(g, [(11, 11, 17), (18, 10, 18), (24, 9, 19), (28, 8, 20)], "b", "c", "a")
    roster.fold(g, [(11, 16), (10, 22), (10, 27)], "a", "c")
    roster.fold(g, [(17, 17), (17, 23), (18, 27)], "a", "c")
    roster.tatters(g, [(28, 8, 13), (29, 9, 19), (28, 17, 20)], "a", "b", "o")
    # the fur wrap: a linen band across the body, and its trim at the hem
    for (y, x0, x1) in ((19, 10, 18), (20, 10, 18)):
        for x in range(x0, x1 + 1):
            if g.get(x, y) in ("b", "c", "a"):
                g.set(x, y, "E" if y == 19 else "A")
    g.set(10, 19, "e")
    for x in range(9, 20, 2):
        if g.get(x, 27) != ".":
            g.set(x, 27, "E")
    # BONE BEADS hanging in strings from the hood
    for (x, y0, y1) in ((12, 11, 16), (16, 11, 14)):
        for y in range(y0, y1 + 1):
            g.set(x, y, "k" if (y - y0) % 2 == 0 else "m")

    # THE HOOD, tall, with a thick fur ruff round the face
    roster.mass(g, [(4, 12, 15), (6, 11, 17), (11, 11, 18)], "b", "c", "a")
    roster.void(g, 7, 10, 1.8, 1.2, cx=15.0)
    for (x, y) in ((12, 6), (12, 7), (12, 8), (12, 9), (13, 10), (14, 11), (15, 11), (16, 11),
                   (17, 10), (17, 9), (17, 8), (16, 6), (13, 5), (14, 5)):
        g.set(x, y, "e" if (x + y) % 3 else "l")
    g.set(15, 5, "E")

    # THE ARM, raised toward the shard, a mitten
    g.line(17, 14, 21, 12, "b")
    g.line(17, 15, 21, 13, "a")
    g.rect(21, 11, 22, 12, "c")
    g.set(22, 12, "a")

    rim(g, 4, 30)
    roster.contact(g, 8, 21)
    roster.eyes(g, 8, 14, 16, "Y", "y")
    # THE SHARD, detached: a long diamond of ice, core down its middle
    for (y, x0, x1) in ((3, 25, 25), (4, 24, 25), (5, 24, 26), (6, 23, 26), (7, 23, 26),
                        (8, 24, 26), (9, 24, 25), (10, 25, 25)):
        g.span(y, x0, x1, "?")
        g.set(x0, y, ">")
        g.set(x1, y, "/")
    for y in (5, 6, 7):
        g.set(25, y, "<")
    g.set(24, 4, "<")
    return g.rows()


def totem():
    """TURRET, and BUILT: a totem he carved from a whale's jaw and set in the ice. A tall bone post,
    the jaw's two tips curving out at its head, a carved face with fire for eyes, lashings of hide.
    Not a cairn (the Thaw's wayfinder) and not a brazier (stage secret I3): one post, standing alone."""
    g = G(32)
    # THE MOUND it is set in: dark ice, rimed along its top
    poly(g, [(7, 31), (9, 27), (13, 25.5), (19, 25.5), (23, 27), (25, 31)], "j")
    shade(g, "j", "N", "J")
    g.span(FEET, 7, 24, "J")
    rime(g, ("j", "N", "J"), x0=8, x1=14, y0=24, y1=30, every=2)
    g.set(12, 26, ">")
    for (x, y) in ((10, 29), (14, 28), (20, 29), (17, 27)):
        g.set(x, y, "/")

    # THE POST: whalebone, tapering, notched, with a lean
    for y in range(8, 28):
        t = (y - 8) / 19.0
        x0 = 13.4 - 0.6 * t
        x1 = 17.6 + 0.8 * t
        g.span(y, x0, x1, "n")
        g.set(x0, y, "k")
        g.set(x0 + 1, y, "k" if y % 5 else "n")
        g.set(x1, y, "m")
    for (y, x0, x1) in ((19, 13, 17), (24, 13, 18)):     # hide lashings, two, uneven
        g.span(y, x0, x1, "A")
        g.span(y + 1, x0, x1, "E")
        g.set(x0, y + 1, "e")
    g.set(17, 21, "A")
    g.line(18, 21, 19, 23, "A")
    for (x, y) in ((14, 22), (16, 17), (15, 26)):      # pitting in the bone
        g.set(x, y, "m")

    # THE JAW TIPS: two prongs out of the post's head, curving out and up, unequal
    SB.limb(g, [(14, 9), (11, 6), (9, 3)], 0.9, "n", "k", "m")
    SB.limb(g, [(17, 9), (20, 7), (21, 4), (20, 2)], 0.9, "m", "n", "J")
    g.set(9, 3, "s")
    g.set(10, 3, "k")
    g.set(20, 2, "k")
    g.span(8, 13, 18, "n")
    g.span(9, 13, 18, "m")

    # THE FACE: a carved recess with the fire in it, and a cut mouth
    g.rect(13, 11, 18, 14, "J")
    g.span(11, 13, 18, "o")
    g.set(13, 14, "m")
    g.span(16, 14, 17, "o")
    g.set(15, 17, "J")

    rim(g, 2, 30)
    g.span(FEET + 1, 8, 23, "o")
    roster.eyes(g, 12, 14, 17, "+", "-")
    g.set(14, 13, "~")
    g.set(17, 13, "=")
    return g.rows()


def ice_ram():
    """CHARGER. A mountain ram, head down, crouched to charge: a stocky fleece body on thin legs and
    a great curled horn around its ear, iced along the outer edge of the curl. Not the hound's lean
    line - this is the one heavy-headed quadruped in the chapter."""
    g = G(32)
    # LEGS, thin and braced: the hind pair driving, the fore pair planted under the lowered head
    for (pts, mid, lit) in ((((9, 22), (7, 26), (7, 29)), "A", "E"), (((12, 23), (12, 26), (11, 29)), "E", "e"),
                            (((18, 23), (19, 26), (19, 29)), "A", "E"), (((21, 22), (22, 26), (22, 29)), "E", "e")):
        SB.limb(g, list(pts), 0.8, mid, lit, "o")
    for x in (7, 11, 19, 22):
        g.span(FEET, x - 1, x + 1, "J")
        g.set(x - 1, FEET, "j")

    # THE FLEECE: a high rump sloping down to the shoulders - the whole body leans into the head.
    # Linen's LIT half, the palest fleece the deep-half rule allows: in the dark half it was a blob.
    g.disc(11, 17.5, 5.0, "e")
    g.disc(18.5, 19.5, 4.4, "e")
    poly(g, [(10, 13), (19, 15.5), (22, 20), (20, 24), (9, 23)], "e")
    shade(g, "e", "l", "E")
    # curls as SCALLOPS - a lit arc over its own shadow, rows offset - not dots, which read as spots
    for (x, y) in ((8, 14), (12, 13), (16, 15), (10, 17), (14, 17), (18, 18), (7, 19), (12, 20),
                   (16, 21), (20, 21), (9, 22), (13, 23)):
        if g.get(x, y) in ("e", "l") and g.get(x + 2, y) in ("e", "l", "E"):
            g.set(x, y, "E")
            g.set(x + 1, y - 1, "l")
            g.set(x + 2, y, "E")
            g.set(x + 1, y, "A")
    for x in range(7, 22):
        col = [y for y in range(10, 26) if g.get(x, y) not in (".", "W")]
        if col and x % 3 == 1:
            g.set(x, col[-1] + 1, "E")            # the ragged fringe under the belly

    # THE HEAD, low and thrust forward out from under the horn: a long pale face, the muzzle down
    poly(g, [(20, 17), (24, 18), (28.5, 24), (28, 27), (25.5, 27), (20, 22)], "n")
    shade(g, "n", "k", "m", x0=19, y0=16, x1=29, y1=28)
    g.line(22, 19, 27, 25, "k")                   # the bridge of the nose, catching the key
    g.set(27, 26, "J")                            # nostril
    g.span(27, 25, 27, "m")
    g.line(20, 19, 20, 23, "o")                   # the seam where the head meets the fleece

    # THE HORN: a heavy curl round the ear, above and behind the face, its outer edge iced
    # The curl's centre is OPEN - daylight through the coil - so the horn survives being filled
    # black; a solid curl was a bump on a blob.
    curl = [(19, 17), (18, 13), (19, 10), (22, 9), (25, 10), (26, 13), (25, 16), (23, 17.5)]
    for i in range(len(curl) - 1):
        r = 1.8 - 0.8 * i / float(len(curl) - 1)
        SB.limb(g, [curl[i], curl[i + 1]], r, "k", "s", "n")
    for (x, y) in ((22, 12), (22, 13), (23, 13), (21, 13), (22, 14)):
        g.set(x, y, ".")
    g.set(23, 14, "m")
    g.set(21, 12, "m")
    for (x, y) in ((19, 12), (20, 10), (24, 10), (25, 13)):
        g.set(x, y, "m")                          # the ridges across the horn
    for (x, y) in ((17, 11), (18, 9), (20, 8), (22, 7)):
        if g.get(x, y) == ".":
            g.set(x, y, "?")                      # ice on the outside of the curl, its upper arc
    g.set(21, 7, ">")
    g.set(27, 12, "/")
    g.set(27, 14, "/")

    rim(g, 7, 30, min_run=3)
    g.span(FEET + 1, 6, 24, "o")
    roster.eyes(g, 21, 23, 25, "Y", "y")
    return g.rows()


def frostburst():
    """EXPLODER. A body frozen from the inside, swollen and splitting: the one round shape in the
    chapter, ice shards pushing out through every seam. Wool under the ice; dark shards with lit
    edges. Its stand-in faces left (SpriteFacesRightByDefault = false), so its face looks left."""
    g = G(32)
    # stubby legs
    g.rect(11, 26, 13, 29, "a")
    g.span(FEET, 10, 13, "a")
    g.set(11, 26, "b")
    g.rect(18, 26, 20, 29, "a")
    g.span(FEET, 18, 21, "a")
    # THE SWELL
    SB.swell(g, 10, 27, [4.0, 7.0, 8.4, 8.6, 7.6, 5.2], "b", "c", "a", cx=15.5)
    g.disc(12.0, 15.5, 2.2, "c")
    g.set(11, 14, "d")
    roster.fold(g, [(20, 13), (22, 19), (20, 25)], "a", None)
    # THE SEAMS, splitting open
    for pts in (((9, 13), (12, 17), (10, 21)), ((16, 11), (17, 15), (15, 19), (16, 23)),
                ((21, 15), (19, 20)), ((12, 24), (15, 25), (19, 23))):
        roster.fold(g, list(pts), "o", "c")
    # ICE SHARDS out through the seams, every one a different length and angle
    for (x, y, dx, dy, h) in ((10, 15, -5, -3, 1.6), (17, 12, 2, -8, 1.8), (21, 16, 6, -4, 1.6),
                              (16, 20, 5, 4, 1.4), (11, 21, -6, 2, 1.4), (14, 25, -2, 4, 1.0),
                              (22, 20, 4, 1, 1.0), (13, 13, -1, -5, 1.2)):
        shard(g, x, y, dx, dy, h)

    # THE HEAD, small, sunk into the swell on the left - it faces left
    roster.mass(g, [(7, 10, 15), (11, 10, 15)], "b", "c", "a")
    roster.void(g, 8, 10, 1.6, 1.2, cx=12.0)

    rim(g, 2, 30)
    roster.contact(g, 8, 22)
    roster.eyes(g, 9, 11, 13, "Y", "y")
    return g.rows()


def hailbringer():
    """SLAMMER. A giant of the north raising two ice-clubbed fists: the fists are the highest things
    in the cell, each a block of dark ice with lit edges, because the slam is them coming down. The
    body under them is broad and hunched, wool furs and linen wraps, its head sunk low."""
    g = G(32)
    # LEGS, short and thick
    for (lx0, lx1) in ((9, 13), (18, 22)):
        g.rect(lx0, 25, lx1, FEET, "A")
        g.line(lx0, 25, lx0, FEET, "E")
        g.span(27, lx0, lx1, "e")
        g.set(lx1, 27, "E")
        g.span(FEET, lx0 - 1, lx1 + 1, "a")
    # THE TORSO, massive, hunched. Linen furs with wool wraps - the Rimebound is the wool body.
    roster.mass(g, [(12, 9, 22), (17, 7, 24), (22, 8, 23), (26, 9, 22)], "E", "e", "A")
    roster.fold(g, [(10, 15), (9, 21), (11, 25)], "A", "e")
    roster.fold(g, [(21, 15), (22, 21), (20, 25)], "A", "e")
    roster.fold(g, [(14, 17), (16, 20), (15, 24)], "A", "l")
    g.span(22, 8, 23, "b")                        # a wool wrap at the waist
    g.span(23, 8, 23, "a")
    g.set(8, 22, "c")
    g.set(14, 22, "c")
    roster.tatters(g, [(26, 9, 14), (27, 10, 13), (26, 17, 22), (27, 18, 21)], "A", "E", "o")
    # HEAD, sunk between the shoulders, a wool hood
    roster.mass(g, [(9, 13, 18), (13, 12, 19)], "b", "c", "a")
    roster.void(g, 10, 13, 2.0, 1.4, cx=15.5)
    g.span(9, 13, 18, "a")

    # ARMS, raised, and the FISTS in ice - the highest things in the cell, because the slam is them
    for (x0, x1, lit) in ((7, 9, "e"), (22, 24, "E")):
        g.rect(x0, 9, x1, 15, "E")
        g.line(x0, 9, x0, 15, lit)
        g.line(x1, 9, x1, 15, "A")
        g.span(12, x0, x1, "b")                   # a wool wrap at the forearm
    for (cx, w) in ((7.5, 5), (23.5, 5)):
        poly(g, [(cx - w, 9.5), (cx - w + 1, 3), (cx - 1, 2), (cx + w - 1, 2.5), (cx + w, 8.5), (cx + 1, 10)], "N")
        shade(g, "N", "p", "J", x0=int(cx - w - 1), y0=1, x1=int(cx + w + 1), y1=11)
        g.line(cx - w + 1, 3, cx - 1, 2, "?")     # the lit top edge only
        g.line(cx - w, 8, cx - w + 1, 4, "p")
        g.line(cx + w - 1, 3, cx + w, 8, "J")
        g.set(cx - 1, 2, ">")
        roster.fold(g, [(cx - 2, 4), (cx, 6), (cx + 2, 5)], "J", "P")
        roster.fold(g, [(cx + 1, 7), (cx + 3, 8)], "J", None)
        g.set(cx + 2, 3, "/")
        g.span(10, cx - w + 1, cx + w - 1, "J")

    rim(g, 2, 30)
    roster.contact(g, 7, 24)
    roster.eyes(g, 12, 14, 17, "Y", "y")
    return g.rows()


def drum_caller():
    """SUMMONER. A drummer calling the hounds: wool robes hung with bone, a fur hood, a round frame
    drum held at hand height and a beater raised to strike it. The drum is the read - a flat disc
    seen at an angle, which no other body in the chapter carries."""
    g = G(32)
    roster.mass(g, [(11, 10, 16), (18, 9, 17), (24, 8, 18), (28, 7, 19)], "b", "c", "a")
    roster.fold(g, [(10, 16), (9, 22), (9, 27)], "a", "c")
    roster.fold(g, [(14, 17), (14, 23), (15, 27)], "a", "c")
    roster.tatters(g, [(28, 7, 12), (29, 8, 17), (28, 15, 19)], "a", "b", "o")
    # bone charms on a cord at the chest
    for (x, y, ch) in ((10, 13, "m"), (11, 14, "k"), (12, 14, "m"), (13, 14, "k"), (14, 13, "m"),
                       (12, 15, "k"), (12, 16, "n")):
        g.set(x, y, ch)
    # THE HOOD, fur-trimmed
    roster.mass(g, [(4, 11, 14), (6, 10, 16), (11, 10, 17)], "b", "c", "a")
    roster.void(g, 7, 10, 1.8, 1.2, cx=14.2)
    for (x, y) in ((11, 6), (11, 7), (11, 8), (11, 9), (12, 10), (13, 11), (14, 11), (15, 11),
                   (16, 10), (16, 9), (16, 7), (13, 5)):
        g.set(x, y, "e" if (x + y) % 3 else "l")

    # THE BEATER, raised in the far hand, over the head
    g.line(9, 13, 7, 9, "b")
    g.set(7, 8, "c")
    SB.limb(g, [(7, 8), (11, 3)], 0.4, "m", "n", "J")
    g.set(11, 3, "k")
    g.set(12, 3, "n")
    g.set(11, 2, "s")

    # THE DRUM: a frame drum at an angle, bone rim, hide head, held out in the near hand
    for y in range(11, 26):
        t = (y - 18.0) / 7.5
        if abs(t) > 1:
            continue
        hw = 3.6 * (1 - t * t) ** 0.5
        g.span(y, 22 - hw, 22 + hw, "e")
        g.set(22 - hw, y, "k")
        g.set(22 + hw, y, "m")
    g.line(20, 12, 19, 16, "l")                   # light across the hide
    g.set(21, 13, "L")
    for (x, y) in ((22, 16), (23, 17), (22, 19), (21, 18)):
        g.set(x, y, "A")                          # a painted mark on the head
    g.set(22, 18, "m")
    g.span(11, 21, 23, "n")
    g.span(25, 21, 23, "m")
    g.line(25, 13, 25, 23, "n")                   # the rim's depth, seen past the edge
    g.set(26, 18, "m")
    # the near arm, reaching to the drum's frame
    g.line(16, 17, 19, 19, "b")
    g.line(16, 18, 19, 20, "a")
    g.set(19, 20, "k")

    rim(g, 2, 30)
    roster.contact(g, 7, 20)
    roster.eyes(g, 8, 13, 15, "Y", "y")
    return g.rows()


def frostkeeper():
    """MINIBOSS, on the 48 cell. His jailer in the north: broad, in steel under a heavy fur cloak, a
    great-helm hung with icicles; a ring of keys frozen into a block of ice at its belt, and chains
    hanging RIGID from both forearms, icicles down their length. Nothing like the Rime Guard's
    halberd and shield: it carries nothing, and what it guards is at its hip."""
    g = G(48)
    F = 46
    # THE CLOAK, fur, hanging behind to the floor and flaring past the legs, broken at its hem
    roster.mass(g, [(16, 14, 33), (26, 12, 35), (36, 12, 35), (F, 12, 35)], "b", "c", "a")
    roster.fold(g, [(14, 24), (13, 34), (12, 44)], "a", "c")
    roster.fold(g, [(34, 26), (35, 35), (35, 44)], "a", None)
    roster.tatters(g, [(44, 12, 15), (45, 12, 14), (44, 32, 35), (45, 33, 35)], "a", "b", "o")
    # the fur's lie: short strokes down the visible cloak, uneven, so it is pelt and not felt
    for (x, y) in ((11, 30), (12, 37), (11, 41), (13, 27), (34, 30), (36, 38), (35, 41), (37, 33)):
        g.set(x, y, "c")
        g.set(x, y + 1, "a")

    # LEGS: steel greaves, squared
    for (lx0, lx1, lit) in ((16, 21, "r"), (26, 31, "R")):
        g.rect(lx0, 36, lx1, F, "R")
        g.line(lx0, 36, lx0, F, lit)
        g.line(lx1, 36, lx1, F, "q")
        g.span(40, lx0, lx1, "q")
        g.span(41, lx0 + 1, lx1 - 1, "r")         # the knee-cop's lower lip
        g.set(lx0 + 2, 43, "q")
        g.set(lx1 - 1, 38, "t")
        g.span(F, lx0 - 1, lx1 + 1, "q")
        g.set(lx0 - 1, F, "r")
    g.rect(22, 37, 25, F, "o")

    # THE BODY: a steel breastplate, ridged, narrowing to a waist
    SB.swell(g, 15, 36, [9.5, 10.0, 8.0, 7.0, 8.5], "R", "r", "q", cx=23.5)
    SB.ridges(g, 17, 34, "R", "q", "r", step=4, jitter=6)
    SB.rivets(g, [(18, 19), (29, 19), (17, 27), (30, 27)], "t", "q")
    # the belt
    g.span(32, 15, 32, "q")
    g.span(31, 15, 32, "R")
    g.set(15, 31, "r")

    # THE KEY BLOCK at the hip: a cube of dark ice, rimed, with keys frozen inside it
    poly(g, [(27, 30), (35, 29), (36, 38), (28, 39)], "j")
    poly(g, [(27, 30), (35, 29), (35.5, 31), (27.3, 32)], "N")
    g.line(27, 30, 35, 29, "?")
    g.line(27, 30, 28, 39, "?")
    g.line(35, 30, 36, 38, "/")
    g.line(28, 39, 36, 38, "J")
    g.set(27, 30, ">")
    # the keys inside, a ring and three bits, seen through the ice
    for (x, y, ch) in ((30, 33, "u"), (31, 32, "g"), (32, 33, "u"), (31, 34, "u"),
                       (31, 35, "g"), (31, 36, "u"), (32, 36, "u"), (29, 35, "u"), (28, 36, "g"),
                       (33, 34, "g"), (34, 35, "u"), (34, 36, "u")):
        g.set(x, y, ch)
    g.set(31, 32, "G")
    g.set(33, 31, "/")
    g.set(34, 37, "/")
    g.line(26, 31, 26, 33, "q")                   # the strap it hangs from
    g.set(27, 31, "q")

    # ARMS, heavy, held out from the body so there is daylight between; gauntlets; and the CHAINS
    # hanging RIGID from them in open air, icicled - the silhouette's two hanging lines
    for (x0, x1, lit) in ((6, 10, "r"), (37, 41, "R")):
        g.rect(x0, 20, x1, 31, "R")
        g.line(x0, 20, x0, 31, lit)
        g.line(x1, 20, x1, 31, "q")
        g.span(25, x0, x1, "q")
        g.span(26, x0, x1, "o")
        g.rect(x0, 31, x1, 35, "R")               # the gauntlet
        g.span(31, x0, x1, lit)
        g.span(34, x0 + 1, x1, "q")
        g.span(35, x0, x1, "o")
    for (cx, y0, y1) in ((7, 36, 43), (39, 36, 42)):
        for y in range(y0, y1 + 1):
            g.set(cx, y, "r" if y % 2 else "q")
            g.set(cx + 1, y, "R" if y % 2 else "q")
        for (yy, dx) in ((y0 + 2, -1), (y0 + 4, 2), (y0 + 6, -1)):
            g.set(cx + dx, yy, "?")               # icicles growing off the links
            g.set(cx + dx, yy + 1, "/")
        g.set(cx, y1 + 1, "?")                    # the icicle each chain ends in
        g.set(cx + 1, y1 + 1, "/")
        g.set(cx, y1 + 2, ">")

    # PAULDRONS, broad, with icicles hanging from their rims
    for (cx, w, lit) in ((12.5, 6.0, "r"), (35.0, 6.0, "R")):
        for y in range(13, 22):
            t = (y - 13) / 9.0
            hw = w * (1.0 - (t - 0.5) ** 2 * 1.4)
            g.span(y, cx - hw, cx + hw, "R")
            g.set(cx - hw, y, lit)
            g.set(cx + hw, y, "q")
        g.span(21, cx - w + 1, cx + w - 1, "q")
        g.span(22, cx - w + 1, cx + w - 1, "o")
    SB.ridges(g, 13, 21, "R", "q", "r", step=3, jitter=5)
    for (x, n) in ((9, 3), (12, 2), (15, 3), (32, 2), (35, 3), (38, 2)):
        for k in range(n):
            g.set(x, 22 + k, "?" if k < n - 1 else ">")
        g.set(x + 1, 22, "/")

    # THE GREAT-HELM: flat-topped, a visor slit, icicles along its brow
    g.rect(18, 4, 29, 14, "R")
    g.line(18, 4, 29, 4, "r")
    g.line(18, 4, 18, 14, "r")
    g.line(29, 5, 29, 14, "q")
    SB.ridges(g, 5, 8, "R", "q", "r", step=2, jitter=9)
    g.span(10, 19, 28, "o")
    g.span(11, 20, 27, "o")
    g.line(23, 12, 23, 14, "q")
    for (x, y) in ((20, 13), (26, 13)):
        g.set(x, y, "t")
    for (x, n) in ((19, 2), (21, 3), (24, 1), (27, 2)):
        for k in range(n):
            g.set(x, 12 + k, "?" if k < n - 1 else ">")
    g.span(3, 20, 27, "R")                        # a crest ridge, rimed
    rime(g, ("R", "r"), x0=18, x1=29, y0=2, y1=5, every=3)
    g.span(15, 20, 27, "q")

    rim(g, 2, F, xmax=34)
    roster.contact(g, 10, 38, y=F)
    roster.eyes(g, 10, 21, 26, "Y", "y")
    return g.rows()


CAST = [
    # stem, pose, cell, (lean, reach, knock), SpriteFrames name
    ("frost-rimebound", rimebound, 32, (0.8, 2.4, 2), "FrostRimeboundFrames"),
    ("frost-sled-hound", sled_hound, 32, (1.8, 3.4, 2), "FrostSledHoundFrames"),
    ("frost-floe-bearer", floe_bearer, 32, (0.5, 2.8, 1), "FrostFloeBearerFrames"),
    ("frost-thane", thane, 32, (0.7, 3.6, 1), "FrostThaneFrames"),
    ("frost-rime-seer", rime_seer, 32, (0.9, 2.2, 3), "FrostRimeSeerFrames"),
    ("frost-totem", totem, 32, (0.1, 2.0, 1), "FrostTotemFrames"),
    ("frost-ice-ram", ice_ram, 32, (1.6, 4.4, 2), "FrostIceRamFrames"),
    ("frost-frostburst", frostburst, 32, (1.1, 2.0, 2), "FrostFrostburstFrames"),
    ("frost-hailbringer", hailbringer, 32, (0.6, 3.6, 1), "FrostHailbringerFrames"),
    ("frost-drum-caller", drum_caller, 32, (0.9, 2.6, 2), "FrostDrumCallerFrames"),
    ("frost-frostkeeper", frostkeeper, 48, (0.5, 4.0, 1), "FrostFrostkeeperFrames"),
]


def build():
    return bestiary_kit.build_cast(CAST, PALETTE)


if __name__ == "__main__":
    main(CAST, PALETTE, "frost")
