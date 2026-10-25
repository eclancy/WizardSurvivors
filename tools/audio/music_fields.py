# -*- coding: utf-8 -*-
"""The Fallow Fields' music (.ai/campaign.md section 5): G Mixolydian, a 9/8 slip jig at dotted
quarter 116. Not run on its own: tools/audio/music.py finds this module (chapter_score_modules)
and appends PIECE to its chapters, so

    python tools/audio/music.py fields --scratch --balance   # render to _music_preview/ and weigh the voices
    python tools/audio/music.py fields                       # render into assets/music/ (once)

THE FIELD AT A RUN. A slip jig is three groups of three in every bar - a lilt that never settles on
two - and played fast it is a dance across stubble rather than a walk through it. G Mixolydian is
G major with its seventh flattened: the F natural is the field's, a folk mode, warm where the
Forest before it is Dorian and dark. Both are modes of the same seven notes (A natural minor's),
so every stinger in sfx.py fits it.

    A  G G F G | C G Dm G          the flute's hook, F natural in its second bar
    B  F C G Am | F C Dm Dm        the fiddle answers; Dm wraps to G - the Mixolydian v - I

Kept apart from the Scorched Sands, the other Mixolydian chapter, by metre and voice: the Sands are
6/8 at 104 on oud and shawm, this is 9/8 at 116 on flute and fiddle with a harp, and the Sands
wrap bVII - I (F to G) where this wraps v - I (Dm to G).

THE STEMS, as .ai/audio-direction.md section 11 has them:

  bed    a G-D drone, held strings, and the fiddle's figure in spiccato running the three groups of
         every bar - the slip jig is in the bed, so the run is already dancing before the pulse
  pulse  a frame drum in threes, the plucked bass on the roots, a harp rocking through the chord
  lead   the flute's hook in A; a solo fiddle answers in B, a second an octave under it

16 BARS, NOT 24. The design said 24, and at dotted quarter 116 a 9/8 bar is 1.55 s: 24 bars is a
37 s loop, past the 30 s the size rule allows (".ai/audio-direction.md", Size). 16 bars - A and B
of eight, the shape of a slip jig's two parts - is 24.8 s.

THE ONE NEW VOICE, `fiddle`, is checked against the element table (section 11, "No chapter
imitates an effect"): two harmonic saws through a bowed-string body, vibrato at a player's 5.6 Hz
that arrives late in the note, lowpassed under 2.4 kHz. No inharmonic partial (metal, ice), no
17 Hz tremolo (arcane), no pitch sweep (water), no bell (light).

NOT LISTENED TO. Rendered once by an agent that cannot hear it, to the generator's conventions and
the RMS targets every chapter shares. It needs ears before anything about it is called finished.
"""
from __future__ import division

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import music as M
import synth as S

GRID = M.Grid(6, 18)        # sixteenths: six to the dotted quarter, eighteen to a 9/8 bar
B_START = 8

CHORDS = {
    "G": M._ch("G2", ["B3", "D4", "G4"], ["G3", "B3", "D4", "G4"], ["G3", "D4", "G4"]),
    "F": M._ch("F2", ["C4", "F4", "A4"], ["A3", "C4", "F4", "A4"], ["F3", "C4", "F4"]),
    "C": M._ch("C3", ["C4", "E4", "G4"], ["G3", "C4", "E4", "G4"], ["C3", "G3", "C4"]),
    "Dm": M._ch("D3", ["D4", "F4", "A4"], ["A3", "D4", "F4", "A4"], ["D3", "A3", "D4"]),
    "Am": M._ch("A2", ["C4", "E4", "A4"], ["A3", "C4", "E4", "A4"], ["A3", "E4", "A4"]),
}
PROG = "G G F G C G Dm G F C G Am F C Dm Dm".split()

# The flute's hook: quarter-eighth, quarter-eighth, quarter-eighth - the slip jig's own rhythm -
# landing on the flat seventh in its second bar.
FLUTE = """D5:4 B4:2 G4:4 B4:2 D5:4 G5:2 | F5:4 E5:2 D5:4 B4:2 G4:6 |
A4:4 C5:2 F5:4 C5:2 A4:4 C5:2 | D5:4 B4:2 G4:6 .:6 |
E5:4 G5:2 E5:4 C5:2 G4:4 C5:2 | D5:4 B4:2 D5:4 G5:2 F5:4 D5:2 |
A4:4 D5:2 F5:4 D5:2 A4:4 F4:2 | G4:4 A4:2 B4:4 D5:2 G4:6 |"""

# The fiddle's answer: longer notes, a climb to the top of the tune and back, a bar's rest before
# the wrap so the hook comes in clean.
FIDDLE = """C5:6 A4:4 F4:2 A4:6 | G4:4 C5:2 E5:4 G5:2 E5:6 |
D5:4 B4:2 G4:4 B4:2 D5:6 | E5:4 C5:2 A4:4 C5:2 E5:4 A5:2 |
A5:6 G5:4 F5:2 C5:6 | E5:4 D5:2 C5:4 E5:2 G5:6 |
F5:6 E5:4 D5:2 A4:6 | D5:4 F5:2 A5:4 F5:2 .:6 |"""


def fiddle(freq, dur):
    """A solo fiddle: two saws a breath apart through a bowed body - a low resonance near the
    note and a fixed one at 1.25 kHz - with a player's vibrato that arrives late in the note, and
    a quick bow so a jig's eighths articulate. Lowpassed under 2.4 kHz, inside the music's band."""
    f = M._pitch(freq, dur, 5.6, 0.006, 0.14)
    f2 = [x * 1.003 for x in f]
    src = S.mix(S.gain(S.osc("saw", f, dur), 0.62), S.gain(S.osc("saw", f2, dur, phase=0.37), 0.38))
    low = S.bandpass(src, max(420.0, freq * 1.05), 2.6)
    body = S.bandpass(src, 1250.0, 3.2)
    warm = M._lp4(src, min(1700.0, freq * 3.0))
    v = M._lp4(S.mix(low, S.gain(body, 0.55), S.gain(warm, 0.5)), 2400.0)
    return S.env_mul(v, S.adsr(dur, a=0.05, d=0.15, s=0.85, r=min(0.25, dur * 0.3)))


def fields_bed():
    g, ch = GRID, CHORDS
    in_b = lambda bar: bar >= B_START
    ev = M._drone_runs(g, len(PROG), ["G2", "D3"], 0.2)
    ev += M.held(g, PROG, lambda c: ch[c]["pad"], M.pad_strings, lambda bar: 0.28 if in_b(bar) else 0.34, 6)
    # The fiddle's figure: an eighth on every step of all three groups, rising through the chord in
    # A and turning back on itself in B. The first of each group is the strong one (Grid.accent).
    ev += M.ost(g, PROG, lambda c: ch[c]["low"],
                lambda bar: "0.2.1.0.2.1.1.2.0." if in_b(bar) else "0.1.2.0.1.2.0.1.2.",
                M.spiccato, 1.6, 0.7)
    return ev


def fields_pulse():
    g, ch = GRID, CHORDS
    ev = M.ost(g, PROG, lambda c: ch[c]["arp"], "0.1.2.3.2.1.0.1.2.", M.harp, 5, 0.36)
    ev += M.ost(g, PROG, lambda c: [ch[c]["root"]], "0...........0.....", M.bass_pluck, 6, 0.6)
    for bar in range(len(PROG)):
        # The drum in threes: the three dotted beats, the first heaviest, and the eighths between.
        ev += M.hits(g, bar, "X.....x.....x.....", M.frame_drum, "G2", 0.75, 6)
        if bar in (B_START - 1, len(PROG) - 1):
            ev += M.hits(g, bar, "......o.x.x.xxxXXX", M.frame_drum, "D3", 0.5, 2)
        else:
            ev += M.hits(g, bar, "..o.o...o.x...o.o.", M.frame_drum, "A3", 0.32, 2)
    return ev


def fields_lead():
    g = GRID
    ev = M.line(g, 0, FLUTE, M.flute, 0.62, legato=0.5)
    ev += M.line(g, B_START, FIDDLE, fiddle, 0.58, legato=0.6)
    # The second fiddle an octave under: weight without height.
    ev += M.line(g, B_START, FIDDLE, fiddle, 0.3, legato=0.6, shift=-12)
    return ev


# The seven notes of A natural minor, as every chapter but the Emberdeep: G Mixolydian is one mode
# of them, and check_scale refuses anything else.
PIECE = M.Piece(
    "fields", 116.0, 3, 16,
    [("bed", fields_bed, 0.28), ("pulse", fields_pulse, 0.16), ("lead", fields_lead, 0.24)],
    tail_beats=3, scale=M.SEVEN,
    describe="The Fallow Fields - G Mixolydian, 9/8 slip jig at dotted quarter 116, A/B of 8 bars")
