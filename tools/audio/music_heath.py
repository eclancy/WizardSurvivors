# -*- coding: utf-8 -*-
"""The Broken Circle's set (.ai/campaign.md section 6). Scored here and rendered by tools/audio/music.py,
which finds this module by its name and appends PIECE to its chapters - run music.py, never this:

    python tools/audio/music.py heath --scratch --balance    # iterate, into tools/audio/_music_preview/
    python tools/audio/music.py heath                        # render into assets/music/, once

D DORIAN IN 5/4, COUNTED 3+2, AT 138. A high heath where the wizards met: the moot's music is old,
open and a little uneven, and the uneven bar is what says so - three beats and then two, so every bar
lands a beat early, like a stride over rough ground. The Forest is also Dorian, but 6/8 at 112 on
flute and harp, two chapters away; the metre and the weight keep them apart (campaign.md).

  A  Dm Dm G G Am C        two horns in fifths state the hook, a dotted-quarter-eighth-quarter on
                           the three and a dotted-quarter-eighth on the two, every bar
  B  F C G Dm Am C         a solo voice answers, long notes over the same stride; a bar's rest
                           before the wrap
  C wraps to Dm: bVII - i, the Dorian cadence. The Forest wraps v - i and the Sands bVII - I in
  Mixolydian; this one is minor and modal and its own.

THE STEMS (audio-direction section 11):
  bed    a D-A drone, held strings, a low male choir under them, and the HORN CALL as the ostinato -
         a short call on the 3+2 in every bar, low, so the bed alone already strides. A high choir
         joins in B: a change of colour, not of level.
  pulse  taiko on the strong beats of the 3+2, frame drums filling the groups, a plucked bass on
         the roots, and a harp walking the chord in eighths; a frame-drum roll into each section.
  lead   two horns in fifths (A); a wordless solo voice (B).

TWELVE BARS, NOT TWENTY. Section 6 asked for 20 bars; at 138 in 5/4 that is a 43.5 s loop, and
section 11's Size rule keeps every loop at 30 s or under because each rendered minute is 2.5 MB of
git history. Twelve bars is 26.1 s, two sections of 13 s - the A/B change the rule asks for.

Every voice here is one another chapter already uses (horn, voice, chant, choir, strings, drone,
taiko, frame drum, harp, plucked bass), all harmonic, so nothing imitates an element's effect
(section 11, "No chapter imitates an effect"). Nothing under E2: the drone is A2 and D3, not D2.

NOT YET HEARD. Composed, measured (--balance, --report, analyse.py --music) and rendered without
anyone listening, like the 2026-10-22 set. The first listen will move things.
"""
from __future__ import division

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import music as M

# 5/4 in sixteenths: twenty steps a bar, the strong steps the 3+2's two downbeats.
GRID = M.Grid(4, 20, groups=[0, 12])

CHORDS = {
    # root (the bass), pad (the held voicing), arp (the harp's walk), low (the horn call and choir)
    "Dm": M._ch("D3", ["D4", "F4", "A4"], ["A3", "D4", "F4", "A4"], ["D3", "A3", "D4", "F4"]),
    "G": M._ch("G2", ["B3", "D4", "G4"], ["G3", "B3", "D4", "G4"], ["G3", "D4", "G4", "B3"]),
    "Am": M._ch("A2", ["C4", "E4", "A4"], ["A3", "C4", "E4", "A4"], ["A3", "E4", "A4", "C4"]),
    "C": M._ch("C3", ["C4", "E4", "G4"], ["G3", "C4", "E4", "G4"], ["C3", "G3", "C4", "E4"]),
    "F": M._ch("F2", ["C4", "F4", "A4"], ["A3", "C4", "F4", "A4"], ["F3", "C4", "F4", "A3"]),
}
PROG = "Dm Dm G G Am C F C G Dm Am C".split()
B = 6

# The hook: a dotted quarter, an eighth and a quarter on the three, a dotted quarter and an eighth
# on the two - the same stride in every bar. Horn 2 is a fifth under it, or a fourth where a fifth
# would leave the mode (F over B), so the pair stays in the seven notes.
HORN_1 = """D5:6 C5:2 D5:4 A4:6 C5:2 | F5:6 E5:2 D5:4 A4:8 | D5:6 C5:2 B4:4 G4:6 B4:2 |
D5:6 E5:2 D5:4 B4:8 | C5:6 B4:2 A4:4 E5:6 D5:2 | E5:6 D5:2 C5:4 .:8 |"""
HORN_2 = """G4:6 F4:2 G4:4 D4:6 F4:2 | C5:6 A4:2 G4:4 D4:8 | G4:6 F4:2 E4:4 C4:6 E4:2 |
G4:6 A4:2 G4:4 E4:8 | F4:6 E4:2 D4:4 A4:6 G4:2 | A4:6 G4:2 F4:4 .:8 |"""
VOICE = """A4:8 C5:4 F5:8 | E5:8 D5:4 C5:8 | D5:8 B4:4 G4:8 |
A4:8 F4:4 D4:4 E4:4 | C5:12 B4:4 A4:4 | G4:12 .:8 |"""


def bed():
    g, ch = GRID, CHORDS
    in_b = lambda bar: bar >= B
    ev = M._drone_runs(g, len(PROG), ["A2", "D3"], 0.22)
    # The strings step back in B so the high choir arrives without the bed getting louder.
    ev += M.held(g, PROG, lambda c: ch[c]["pad"], M.pad_strings,
                 lambda bar: 0.34 if in_b(bar) else 0.42, 10)
    # The low male choir under everything, both sections: the moot's voices.
    ev += M.held(g, PROG, lambda c: ch[c]["low"][:2], M.chant, 0.3, 10, spread=0.03)
    ev += M.held(g, PROG, lambda c: ch[c]["pad"], M.choir, 0.24, 10, spread=0.03, only=in_b)
    # THE HORN CALL: on the 3+2, low, every bar - a rising call in A, its turn in B.
    ev += M.ost(g, PROG, lambda c: ch[c]["low"],
                lambda bar: "1.....2.3...1...2..." if in_b(bar) else "0.....1.2...1...0...",
                M.horn, 3, 0.68)
    return ev


def pulse():
    g, ch = GRID, CHORDS
    ev = M.ost(g, PROG, lambda c: ch[c]["arp"],
               lambda bar: "0.2.1.3.2.1.0.2.3.1." if bar >= B else "0.1.2.3.2.1.0.1.2.1.",
               M.harp, 6, 0.36)
    ev += M.ost(g, PROG, lambda c: [ch[c]["root"]], "0.....0.....0...0...", M.bass_pluck, 5, 0.62)
    for bar in range(len(PROG)):
        # The taiko on the two downbeats of the 3+2, a lighter stroke leading into the two.
        ev += M.hits(g, bar, "X...........X.......", M.taiko, "A2", 0.8, 8)
        ev += M.hits(g, bar, "..........o.........", M.taiko, "D3", 0.6, 4)
        if bar in (B - 1, len(PROG) - 1):
            # A roll into the next section (and across the seam into the first).
            ev += M.hits(g, bar, "....o...o.xxx.xxxXXX", M.frame_drum, "D3", 0.8, 2)
        else:
            ev += M.hits(g, bar, "....x...o.....x...o." if bar % 2 else "....x...o.o...x.o.o.",
                         M.frame_drum, "A3", 0.7, 2)
    return ev


def lead():
    g = GRID
    ev = M.line(g, 0, HORN_1, M.horn, 0.6, legato=0.5)
    ev += M.line(g, 0, HORN_2, M.horn, 0.42, legato=0.5)
    ev += M.line(g, B, VOICE, M.voice, 0.62, legato=0.5)
    return ev


PIECE = M.Piece(
    "heath", 138.0, 5, 12,
    [("bed", bed, 0.3), ("pulse", pulse, 0.16), ("lead", lead, 0.26)],
    tail_beats=5, scale=M.SEVEN,
    describe="The Broken Circle - D Dorian, 5/4 (3+2) at 138, A/B of 6 bars")
