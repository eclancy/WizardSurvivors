# -*- coding: utf-8 -*-
"""Stat glyphs for the level-up cards, the chest pick and anything else that lists a number.

Run: python tools/art/stat_icons.py

WHY THEY EXIST. Every stat line on a card was a row of words in one ink - "DMG 20   CD 5.5s
RNG 480" - and the only thing telling a damage number from a cooldown was the abbreviation in
front of it. On a phone, at a glance, mid-fight, that is reading, and reading is what the player
has least time for. A glyph in front of each number is recognised rather than read: a sword is
damage before the word is parsed. Colour stays as a SECOND cue (the element tint on the icon,
the rubric ink on an upgrade) but meaning no longer depends on it, which is also what makes the
cards usable by a player who cannot tell the element hues apart.

THE CELL IS 12x12, rendered at x2 to 24px. That is the size of a capital letter at the Body role
(20-24px), so a glyph sits on the line like a word rather than towering over it. ui_icons.py's
24x24 glyphs label a whole button; these label one number, and a 48px sword beside "20" would make
the number the small thing.

EVERY GLYPH IS OUTLINED IN OCCLUSION, automatically (`_outline`), and that is what lets one set
serve both registers: the cards are pale parchment, the boon plaque is dark violet enamel and the
chest pick is stone. A dark-rimmed glyph with a lit interior holds its shape on all three without
a second set. Light model as everywhere else: key from the upper left, so a lit edge faces up and
left.

Authoring is ASCII rows for anything with a drawn silhouette and a few lines of geometry for the
round ones (rings, a clock face). Every colour is a contract colour; `write` audits it.

THE NAMES ARE THE C# KEYS. StatIcons.cs maps a stat line to one of these names and loads
res://assets/bonelight/ui/stats/stat-<name>.png. Rename one here and rename it there.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "stats")
PREVIEW = os.path.join(ROOT, "tools", "art", "_stat_icons_preview.png")

CELL = 12

E = bl.ELEMENTS


def T(material, role):
    return bl.tone(material, role)


# Shared ink letters. A glyph's own palette may add to these or override them.
BASE = {
    "H": T("steel", "hi"),
    "L": T("steel", "lit"),
    "B": T("steel", "base"),
    "S": T("steel", "shade"),
    "Y": T("gold", "hi"),
    "G": T("gold", "lit"),
    "D": T("gold", "base"),
    "E": T("gold", "shade"),
    "k": T("stone", "shade"),
}


def _grid(rows, extra=None):
    pal = dict(BASE)
    if extra:
        pal.update(extra)
    assert len(rows) == CELL, "a glyph is %d rows, not %d" % (CELL, len(rows))
    c = raster.Canvas(CELL, CELL, None)
    for y, row in enumerate(rows):
        assert len(row) == CELL, "row %d is %d wide: %r" % (y, len(row), row)
        for x, ch in enumerate(row):
            if ch != ".":
                c.set(x, y, pal[ch])
    return c


def _outline(c):
    """Rim every opaque pixel in occlusion, on its four sides.

    Four-neighbour, not eight: an eight-neighbour rim fills the inside corner of every diagonal
    and turns a sword's blade into a staircase of black. Run once, after the glyph is drawn - a
    glyph must leave a clear pixel on each side of the cell for its rim.
    """
    solid = set((x, y) for y in range(c.h) for x in range(c.w) if c.get(x, y)[3] > 0)
    for (x, y) in solid:
        for (nx, ny) in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < c.w and 0 <= ny < c.h and (nx, ny) not in solid:
                c.set(nx, ny, OCC)
    return c


# --- the glyphs -----------------------------------------------------------------------------

def damage():
    """A sword, point up and right. The blade is lit on its upper-left face."""
    return _grid([
        "............",
        ".........HL.",
        "........HLS.",
        ".......HLS..",
        "......HLS...",
        ".....HLS....",
        "..G.HLS.....",
        "...GLS......",
        "....GG......",
        "...D..G.....",
        "..Y....G....",
        "............",
    ])


def cooldown():
    """An hourglass, sand running. Gold caps, dark glass, amber sand."""
    return _grid([
        "............",
        ".GGGGGGGGGG.",
        "..HkkkkkkS..",
        "..HAAAAAAS..",
        "...HAAAAS...",
        "....HAAS....",
        "....HkAS....",
        "...HkkAkS...",
        "..HkkAAAkS..",
        "..HAAAAAAS..",
        ".DDDDDDDDDD.",
        "............",
    ], {"A": T("amber", "lit")})


def projectiles():
    """Three darts in flight: more than one of the thing you throw."""
    return _grid([
        "............",
        "............",
        ".........H..",
        ".rrDDDDDDHL.",
        ".........S..",
        ".......H....",
        ".rrDDDDHL...",
        ".......S....",
        ".........H..",
        ".rrDDDDDDHL.",
        ".........S..",
        "............",
    ], {"r": T("ember", "base")})


def pierce():
    """An arrow driven clean through a board."""
    return _grid([
        "............",
        ".....Pp.....",
        ".....Pp.....",
        ".....Pp.....",
        ".....Pp..H..",
        ".rrDDPpDDHL.",
        ".....Pp..S..",
        ".....Pp.....",
        ".....Pp.....",
        ".....Pp.....",
        ".....Pp.....",
        "............",
    ], {"r": T("ember", "base"), "P": E["earth"][2], "p": E["earth"][3]})


def knockback():
    """Two chevrons shoving right."""
    return _grid([
        "............",
        "............",
        ".H...H......",
        ".LH..LH.....",
        "..LH..LH....",
        "...LH..LH...",
        "...SL..SL...",
        "..SL..SL....",
        ".SL..SL.....",
        ".S...S......",
        "............",
        "............",
    ])


def crit():
    """A four-pointed star: the hit that lands harder than it should."""
    return _grid([
        "............",
        ".....GG.....",
        ".....GG.....",
        "....GYYG....",
        "...GYWWYG...",
        ".GGYWWWWYGG.",
        ".GDYWWWWYDG.",
        "...DYWWYD...",
        "....DYYD....",
        ".....DD.....",
        ".....DD.....",
        "............",
    ], {"W": T("gold", "hi"), "Y": T("amber", "hi")})


def burn():
    """A flame."""
    f = E["fire"]
    return _grid([
        "............",
        ".....c......",
        ".....cc.....",
        "....chc.....",
        "...chhc..c..",
        "...chhhc.cc.",
        "..chhyhhcc..",
        "..chyyyhhc..",
        "..chyyyyhc..",
        "...chyyhc...",
        "....cccc....",
        "............",
    ], {"c": f[3], "h": f[2], "y": f[1]})


def poison():
    """A drop of venom, with a bubble of light on its upper left."""
    p = E["poison"]
    return _grid([
        "............",
        ".....p......",
        ".....p......",
        "....pmp.....",
        "....pmp.....",
        "...pmmmp....",
        "..pmhmmmp...",
        "..pmhmmmp...",
        "..pmmmmmp...",
        "...pmmmp....",
        "....ppp.....",
        "............",
    ], {"p": p[3], "m": p[2], "h": p[0]})


def freeze():
    """A snowflake: eight arms with a barb at each axis tip."""
    c = raster.Canvas(CELL, CELL, None)
    ice = E["ice"]
    cx = cy = 5
    for d in range(1, 5):
        for (x, y) in ((cx, cy - d), (cx, cy + d), (cx - d, cy), (cx + d, cy)):
            c.set(x, y, ice[2] if d < 4 else ice[3])
    for d in range(1, 4):
        for (x, y) in ((cx - d, cy - d), (cx + d, cy - d), (cx - d, cy + d), (cx + d, cy + d)):
            c.set(x, y, ice[2] if d < 3 else ice[3])
    for (x, y) in ((4, 2), (6, 2), (4, 8), (6, 8), (2, 4), (2, 6), (8, 4), (8, 6)):
        c.set(x, y, ice[3])
    c.set(cx, cy, ice[0])
    # Shift right and down by half the spare pixel so the flake is centred in the 12 cell.
    out = raster.Canvas(CELL, CELL, None)
    out.blit(c.img, 1, 1)
    return out


def slow():
    """A snail: spiral shell, a body on the ground, stalks up. Slow, and nothing else."""
    return _grid([
        "............",
        "............",
        "......EEE...",
        ".....EDDDE..",
        ".s.sEDEEDDE.",
        "..s.EDEDEDE.",
        "..sbEDDEEDE.",
        "..bbbEDDDE..",
        ".bbbbbbbbbb.",
        "............",
        "............",
        "............",
    ], {"E": E["earth"][3], "D": E["earth"][2], "s": T("linen", "hi"), "b": T("linen", "lit")})


def root():
    """A sprout above the ground line and its roots spreading below it: held in place."""
    v = T("verdant", "lit")
    w = T("verdant", "base")
    return _grid([
        "............",
        "...vv..vv...",
        "...vww.wv...",
        "....vwww....",
        "......w.....",
        ".EEEEEEEEEE.",
        "......e.....",
        ".....e.e....",
        "....e..e.e..",
        "...e...e..e.",
        "..e...e.....",
        "............",
    ], {"v": v, "w": w, "E": E["earth"][3], "e": E["earth"][2]})


def shield():
    """A heater shield: gold rim, steel field lit on its left half."""
    return _grid([
        "............",
        ".GGGGGGGGGG.",
        ".GHLLLLBBSG.",
        ".GHLLLLBBSG.",
        ".GHLLLLBBSG.",
        ".GHLLLLBBSG.",
        "..GLLLLBBG..",
        "..GHLLLBSG..",
        "...GLLBSG...",
        "....GLBG....",
        ".....DD.....",
        "............",
    ])


def heart():
    """A heart, for health. Ember, the warmest row the palette has."""
    return _grid([
        "............",
        "............",
        "..RRR..RRR..",
        ".RwwRRRRRRR.",
        ".RwRRRRRRRd.",
        ".RRRRRRRRRd.",
        "..RRRRRRRd..",
        "...RRRRRd...",
        "....RRRd....",
        ".....Rd.....",
        "............",
        "............",
    ], {"R": T("ember", "base"), "w": T("ember", "hi"), "d": T("ember", "shade")})


def regen():
    """The heart with a cross of light in it: health coming back, not health held."""
    return _grid([
        "............",
        "............",
        "..RRR..RRR..",
        ".RwwRRRRRRR.",
        ".RwRRWWRRRd.",
        ".RRRWWWWRRd.",
        "..RRRWWRRd..",
        "...RRRRRd...",
        "....RRRd....",
        ".....Rd.....",
        "............",
        "............",
    ], {"R": T("ember", "base"), "w": T("ember", "hi"), "d": T("ember", "shade"),
        "W": T("gold", "hi")})


def magnet():
    """A horseshoe magnet: pulls things to you."""
    return _grid([
        "............",
        "...RRRRRR...",
        "..RwRRRRRd..",
        ".RwRR..RRRd.",
        ".RwR....RRd.",
        ".RwR....RRd.",
        ".RRR....RRd.",
        ".HHH....HHL.",
        ".LLL....LLB.",
        "............",
        "............",
        "............",
    ], {"R": T("ember", "base"), "w": T("ember", "hi"), "d": T("ember", "shade")})


def luck():
    """A four-leaf clover."""
    return _grid([
        "............",
        "..vvv.vvv...",
        ".vwwvvvwwv..",
        ".vwvvvvwvv..",
        "..vvvvvvv...",
        ".vvvvvvvvv..",
        ".vwvvvvvvv..",
        "..vvv.vvv...",
        "......s.....",
        ".......s....",
        "........s...",
        "............",
    ], {"v": T("verdant", "base"), "w": T("verdant", "lit"), "s": T("verdant", "shade")})


def xp():
    """A cut gem, the same object the XP orb is."""
    a = E["arcane"]
    return _grid([
        "............",
        "...mmmmmm...",
        "..mhhmmmme..",
        ".mhhmmmmmee.",
        ".eeeeeeeeee.",
        "..mhmmmmme..",
        "...mhmmme...",
        "....mmme....",
        ".....me.....",
        "............",
        "............",
        "............",
    ], {"m": a[2], "h": a[0], "e": a[3]})


def thorns():
    """A ring of thorns: it hurts whoever touches you."""
    c = raster.Canvas(CELL, CELL, None)
    stem = T("verdant", "base")
    lit = T("verdant", "lit")
    spike = T("linen", "hi")
    cx = cy = 5.5
    for y in range(CELL):
        for x in range(CELL):
            r = math.hypot(x - cx, y - cy)
            if 2.4 <= r <= 3.6:
                c.set(x, y, lit if (x - cx) + (y - cy) < 0 else stem)
    for (x, y) in ((5, 1), (10, 5), (6, 10), (1, 6), (2, 2), (9, 2), (2, 9), (9, 9)):
        c.set(x, y, spike)
    return c


def area():
    """Four arrows pushing out to the corners from a centre: the effect grows.

    Not concentric rings - that is a target, and the reticle beside it on a card would read as the
    same glyph. A ring with arrows on it was tried and the arrowheads fused into the ring at this
    size, which drew a cog. Arrows to the corners keep clear of each other and say "bigger".
    """
    return _grid([
        "............",
        ".GGG....GGG.",
        ".GG......GG.",
        ".G.G....G.G.",
        "....G..G....",
        ".....YY.....",
        ".....YD.....",
        "....D..D....",
        ".D.D....D.D.",
        ".DD......DD.",
        ".DDD....DDD.",
        "............",
    ])


def range_():
    """A reticle: how far away the spell will reach for a target."""
    c = raster.Canvas(CELL, CELL, None)
    cx = cy = 5.5
    for y in range(CELL):
        for x in range(CELL):
            r = math.hypot(x - cx, y - cy)
            lit = (x - cx) + (y - cy) < 0
            if 3.6 <= r <= 4.6:
                c.set(x, y, T("steel", "lit") if lit else T("steel", "base"))
    for i in (0, 1, 2, 9, 10, 11):
        c.set(i, 5, T("steel", "hi"))
        c.set(i, 6, T("steel", "hi"))
        c.set(5, i, T("steel", "hi"))
        c.set(6, i, T("steel", "hi"))
    for (x, y) in ((5, 5), (6, 5), (5, 6), (6, 6)):
        c.set(x, y, T("ember", "base"))
    # The cross hairs reach the cell edge so they read as cross hairs; trim the outermost pixel so
    # the rim has somewhere to go.
    for i in (0, 11):
        for j in (5, 6):
            c.set(i, j, (0, 0, 0, 0))
            c.set(j, i, (0, 0, 0, 0))
    return c


def duration():
    """A clock face: how long a thing lasts once it is down."""
    c = raster.Canvas(CELL, CELL, None)
    cx = cy = 5.5
    for y in range(CELL):
        for x in range(CELL):
            r = math.hypot(x - cx, y - cy)
            if r <= 3.7:
                c.set(x, y, T("skin", "hi"))
            elif r <= 4.9:
                c.set(x, y, T("gold", "lit") if (x - cx) + (y - cy) < 0 else T("gold", "base"))
    # Hands at ten past two, in occlusion.
    for (x, y) in ((5, 3), (5, 4), (5, 5), (6, 5), (7, 5), (6, 6)):
        c.set(x, y, OCC)
    return c


def speed():
    """A boot with the air streaming off it: faster on your feet, or faster in flight."""
    return _grid([
        "............",
        "............",
        ".....lllm...",
        ".....lmmd...",
        ".....lmmd...",
        ".HH..lmmd...",
        ".....lmmmmd.",
        ".HHH.lmmmmd.",
        ".....dddddd.",
        "............",
        "............",
        "............",
    ], {"l": E["earth"][1], "m": E["earth"][2], "d": E["earth"][3]})


def chain():
    """Two links of chain: it carries on to the next one."""
    c = raster.Canvas(CELL, CELL, None)

    def link(cx, cy):
        for y in range(CELL):
            for x in range(CELL):
                dx, dy = x - cx, y - cy
                # A rounded square ring, two pixels thick.
                r = max(abs(dx), abs(dy)) + 0.35 * min(abs(dx), abs(dy))
                if 1.6 <= r <= 3.0:
                    lit = dx + dy < 0
                    c.set(x, y, T("steel", "lit") if lit else T("steel", "base"))

    link(3.5, 3.5)
    link(7.5, 7.5)
    # Where the second link passes over the first, re-draw it so the two read as linked.
    for (x, y) in ((5, 6), (6, 5)):
        c.set(x, y, T("steel", "hi"))
    return c


def evasion():
    """A swerve: an arrow that bends round where you were standing."""
    return _grid([
        "............",
        "............",
        "....HHHH....",
        "...H....L...",
        "..H......L..",
        "..L......B.H",
        ".........BBL",
        ".........BS.",
        "..o.........",
        ".ooo........",
        "..o.........",
        "............",
    ], {"o": T("steel", "shade")})


def generic():
    """A gold lozenge, for a stat with no glyph of its own. Better a mark than a gap."""
    return _grid([
        "............",
        ".....YG.....",
        "....YGGD....",
        "...YGGGGD...",
        "..YGGGGGGD..",
        ".YGGGGGGGGD.",
        ".GGGGGGGGDE.",
        "..GGGGGGDE..",
        "...GGGGDE...",
        "....GGDE....",
        ".....DE.....",
        "............",
    ])


# --- the results screen's tallies -----------------------------------------------------------
# Not stats either: the counts the results screen (GameOverScreen) leads its numbers with. A run's
# kills had no picture anywhere, and "Enemies slain" beside a sword read as damage.

def kills():
    """A skull, bone lit from the upper left: the dead, counted. Bone, because the horde is the
    dead and has no faction colour of its own (.ai/world-and-tone.md)."""
    return _grid([
        "............",
        "...hhhhhh...",
        "..hlllllbb..",
        ".hllllllbbs.",
        ".hlkklkkbbs.",
        ".hlkklkkbbs.",
        ".hlllkllbbs.",
        "..bllkkbbs..",
        "...blblbs...",
        "...bsbsbs...",
        "............",
        "............",
    ], {"h": T("skin", "hi"), "l": T("skin", "lit"), "b": T("skin", "base"), "s": T("skin", "shade")})


def elite():
    """The skull again, horned in gold: one of the strong ones. Same face, so the two tallies read
    as the same kind of count; the horns are the difference, and they are the gold of a thing worth
    more."""
    return _grid([
        "............",
        ".G........D.",
        ".GG......DD.",
        "..GhhhhhhD..",
        ".hllllllbbs.",
        ".hlkklkkbbs.",
        ".hlkklkkbbs.",
        ".hlllkllbbs.",
        "..bllkkbbs..",
        "...blblbs...",
        "...bsbsbs...",
        "............",
    ], {"h": T("skin", "hi"), "l": T("skin", "lit"), "b": T("skin", "base"), "s": T("skin", "shade")})


def chest():
    """A treasure chest, gold-banded wood, lid shut: a chest opened in the run. The wood is the
    earth ramp's dark half so the gold bands sit a full step above it (relic_icons._casket)."""
    w = E["earth"]
    return _grid([
        "............",
        "............",
        "..wwwwwwww..",
        ".wmmGmmGmmd.",
        ".wmmGmmGmmd.",
        ".GGGGYGGGGD.",
        ".wmmGkkGmmd.",
        ".wmmGkkGmmd.",
        ".wmmGGGGmmd.",
        ".dddDddDddd.",
        "............",
        "............",
    ], {"w": w[2], "m": w[3], "d": T("gold", "deep")})


# --- the codex marks ------------------------------------------------------------------------
# Not stats: the three marks the Spellbook and Deeds lists put in front of a reward, a finished
# deed and a page not yet found (.ai/level-up-screen.md, section 4b). Same cell, same rim, so
# they sit on a line exactly like the stat glyphs do. C# loads them by name, not by KeyFor.

def energy():
    """Arcane Energy, the meta currency: a lit orb of the arcane ramp. Round, where the XP gem is
    cut - the two are the same light, and the shape is what tells a run's XP from the purse."""
    a = E["arcane"]
    c = raster.Canvas(CELL, CELL, None)
    cx, cy = 5.5, 5.5
    for y in range(CELL):
        for x in range(CELL):
            dx, dy = x - cx, y - cy
            d = math.sqrt(dx * dx + dy * dy)
            if d > 4.3:
                continue
            # Key from the upper left: the far rim falls into the edge tone, the near one lifts.
            facing = (dx + dy) / 2.0
            if d > 3.3 and facing > 0:
                tone = a[3]
            elif facing > 1.2:
                tone = a[2]
            else:
                tone = a[1]
            c.set(x, y, tone)
    # The hot spot, two pixels up and left of centre.
    for (x, y) in ((4, 3), (3, 4), (4, 4)):
        c.set(x, y, a[0])
    return c


def done():
    """A tick, for a deed already done. Gold, the colour of a thing earned."""
    return _grid([
        "............",
        "............",
        ".........YY.",
        "........YGD.",
        ".......YGD..",
        "..YY..YGD...",
        "..GGDYGD....",
        "...GGGD.....",
        "....GD......",
        "............",
        "............",
        "............",
    ])


def lock():
    """A padlock, for a page of the book not yet found. Steel shackle, gold body, dark keyhole."""
    return _grid([
        "............",
        "....LLLB....",
        "...L....B...",
        "...L....B...",
        "...L....B...",
        "..YGGGGGGD..",
        "..GGGGGGGD..",
        "..GGGkkGGD..",
        "..GGGkkGGD..",
        "..GGGGGGGD..",
        "..DDDDDDDE..",
        "............",
    ])


def todo():
    """An empty socket, for a requirement not yet met - the ascension browser's checklist ticks a met
    gate with `done` and marks the rest with this. A ring with nothing in it, not a cross: a cross
    says "wrong", and an unmet gate is only "not yet". Steel, lit from the upper left like the rest."""
    return _grid([
        "............",
        "............",
        "....LLLB....",
        "...L....B...",
        "..L......S..",
        "..L......S..",
        "..L......S..",
        "..B......S..",
        "...B....S...",
        "....BSSS....",
        "............",
        "............",
    ])


def sealed():
    """A sealed medallion, for an ascension whose requirements have never been met - the browser
    shows this where the path's icon will be, and "???" for its name (.ai/level-up-screen.md 4e).
    Not `lock`: lock says "you have not got this yet", which a seen path that is merely out of
    reach also is. A path the player has never been allowed to read is something else - unknown -
    so it is a gold seal with a question cut into it. Gold, the Ascension's colour."""
    return _grid([
        "............",
        "....YYYG....",
        "...YGGGGD...",
        "..YGkkkGGD..",
        "..YkGGGkGD..",
        "..GGGGkGGD..",
        "..GGGkGGGD..",
        "..GGGGGGGD..",
        "...GGkGGD...",
        "....DDDE....",
        "............",
        "............",
    ])


def eye():
    """An open eye, for a hidden achievement the player has SEEN but not yet found - the achievements
    page's "Seen" tag (.ai/achievements-and-secrets.md 4.3). Not `sealed` (never met, unknown) and
    not `todo` (a requirement): it says only "you have been near this". Steel lids and white, an
    arcane-blue iris - the page's Seen ink - and a dark pupil; lit from the upper left like the rest."""
    return _grid([
        "............",
        "............",
        "....LLLL....",
        "..LLHHHHBB..",
        ".LHHAAAAHHB.",
        ".LHAAkkAaHS.",
        ".LHAAkkaaHS.",
        ".BHHAaaaHHS.",
        "..BBHHHHSS..",
        "....SSSS....",
        "............",
        "............",
    ], {"A": T("arcane", "lit"), "a": T("arcane", "base")})


GLYPHS = [
    ("damage", damage),
    ("cooldown", cooldown),
    ("range", range_),
    ("projectiles", projectiles),
    ("area", area),
    ("pierce", pierce),
    ("chain", chain),
    ("crit", crit),
    ("knockback", knockback),
    ("freeze", freeze),
    ("slow", slow),
    ("root", root),
    ("burn", burn),
    ("poison", poison),
    ("duration", duration),
    ("speed", speed),
    ("shield", shield),
    ("heart", heart),
    ("regen", regen),
    ("magnet", magnet),
    ("luck", luck),
    ("xp", xp),
    ("thorns", thorns),
    ("evasion", evasion),
    ("generic", generic),
    ("energy", energy),
    ("done", done),
    ("lock", lock),
    ("todo", todo),
    ("sealed", sealed),
    ("eye", eye),
    ("kills", kills),
    ("elite", elite),
    ("chest", chest),
]


def write(canvas, name):
    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s (art-direction.md section 3):" % name)
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return False
    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    canvas.scaled(2).save(os.path.join(OUT_DIR, "stat-%s.png" % name))
    return True


def preview(rendered):
    """A contact sheet: every glyph at x2 and x4 on parchment, enamel and stone - the three
    grounds it has to work on - so a glyph that only reads on one of them is caught here."""
    from PIL import Image
    grounds = [T("flesh", "hi"), T("violet", "deep"), T("stone", "shade")]
    pad = 6
    per_row = 5
    block_w = (24 + 48 + pad * 3) * len(grounds)
    block_h = 48 + pad * 2
    rows = (len(rendered) + per_row - 1) // per_row
    sheet = Image.new("RGBA", (block_w * per_row, block_h * rows), (0, 0, 0, 255))
    for i, (name, canvas) in enumerate(rendered):
        bx = (i % per_row) * block_w
        by = (i // per_row) * block_h
        x2 = canvas.scaled(2)
        x4 = canvas.scaled(4)
        for gi, ground in enumerate(grounds):
            gx = bx + gi * (24 + 48 + pad * 3)
            sheet.paste(Image.new("RGBA", (24 + 48 + pad * 3, block_h - 2), raster.rgb(ground)), (gx, by))
            sheet.paste(x2, (gx + pad, by + pad + 12), x2)
            sheet.paste(x4, (gx + pad * 2 + 24, by + pad), x4)
    sheet.save(PREVIEW)


def main():
    ok = True
    rendered = []
    for name, fn in GLYPHS:
        canvas = _outline(fn())
        ok &= write(canvas, name)
        rendered.append((name, canvas))
        print("  stat-%-12s 24x24" % name)
    if not ok:
        return 1
    preview(rendered)
    print("preview: %s" % os.path.relpath(PREVIEW, ROOT).replace(os.sep, "/"))
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
