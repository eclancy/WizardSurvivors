# -*- coding: utf-8 -*-
"""The Court's set (.ai/endgame-castle.md section 5, piece C7): a court dance with no dancers. Scored
here and rendered by tools/audio/music.py, which finds this module by its name and appends PIECE to
its chapters - run music.py, never this:

    python tools/audio/music.py court --scratch --balance   # iterate, into tools/audio/_music_preview/
    python tools/audio/music.py court                       # render into assets/music/, once

A MINUET IN 3/4 AT 112, A MINOR WITH ITS LEADING NOTE. "A court with no one left in it": the halls of
his house, a great hall laid for a feast for a court that is not coming. Its music is the dance that
court would have danced - a minuet, the court dance, in binary form (two halves of eight bars), on
the court's instruments: a harpsichord's continuo, a consort of viols, a recorder, a theorbo's bass.

THE DANCERS ARE THE DRUM. A minuet's step - the pas de menuet - takes two bars of 3/4, and its
weight falls on the first and third beats of the first bar and the second of the next: three steps
of two beats across two bars of three, a hemiola. Here a tambour plays exactly those steps, all the
way through, while the harpsichord and the strings keep the 3/4. So the cross-rhythm a ballroom full
of feet would make is in the music and nobody is making it. Nothing tells the player this; it should
only feel as though the dance is a beat out from where it should be.

HIS NOTES. The road's chapters are modes of the seven notes (audio-direction section 11), and the
Emberdeep stepped outside with the boss loop's G sharp and B flat because it was the end. The castle
is his house and the end now, so its rooms take his notes up: the Court uses the G sharp - the
leading note, E major as the dominant - which is also simply how a minuet in A minor is written. The
Throne takes the B flat too. The Last Bridge, still outside his walls, keeps the world's seven.

    A  Am E Am Dm | G C F E          the recorder's minuet: the circle of fifths down to a half close
    B  C G Am Em | F Dm E E          a viol answers; E wraps to Am, the dominant home, V - i

THE STEMS (audio-direction section 11):
  bed    the viol consort held, and the harpsichord's continuo in broken eighths on every bar
  pulse  the theorbo's bass, the strings' pah-pah on beats two and three, and the tambour on the
         dancers' steps, with a roll into each half
  lead   the recorder (A); a treble viol (B), the recorder a ghost an octave over it

Two new voices, both checked against the element table (section 11, "No chapter imitates an
effect"): the harpsichord is Karplus-Strong with an octave string, harmonic, lowpassed under 3 kHz -
no inharmonic partial (metal, ice), no bell (light); the viol is two harmonic oscillators through a
bowed body, with no vibrato to speak of and no tremolo (arcane) or pitch sweep (water).

16 BARS is 25.7 s, inside the 30 s Size rule. NOT YET HEARD: composed, measured (--balance, --report,
analyse.py --music, mixsim.py --music) and rendered by an agent that cannot listen.
"""
from __future__ import division

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import music as M
import synth as S

GRID = M.Grid(4, 12)       # sixteenths: four to the quarter, twelve to a 3/4 bar
B = 8
SCALE = M.SEVEN | M.pitch_classes(["G#"])

CHORDS = {
    # root (the theorbo), pad (the viols), arp (the harpsichord's broken chord), low (pah-pah)
    "Am": M._ch("A2", ["A3", "C4", "E4"], ["A3", "C4", "E4", "A4"], ["C4", "E4"]),
    "E": M._ch("E3", ["G#3", "B3", "E4"], ["G#3", "B3", "E4", "G#4"], ["G#3", "B3"]),
    "Dm": M._ch("D3", ["A3", "D4", "F4"], ["A3", "D4", "F4", "A4"], ["D4", "F4"]),
    "G": M._ch("G2", ["G3", "B3", "D4"], ["G3", "B3", "D4", "G4"], ["B3", "D4"]),
    "C": M._ch("C3", ["G3", "C4", "E4"], ["G3", "C4", "E4", "G4"], ["C4", "E4"]),
    "F": M._ch("F2", ["A3", "C4", "F4"], ["A3", "C4", "F4", "A4"], ["A3", "C4"]),
    "Em": M._ch("E3", ["G3", "B3", "E4"], ["G3", "B3", "E4", "G4"], ["G3", "B3"]),
}
PROG = "Am E Am Dm G C F E C G Am Em F Dm E E".split()

# The minuet: quarters and eighths, the circle of fifths, a half close on E with a beat's rest.
RECORDER = """E5:4 A4:2 B4:2 C5:2 D5:2 | E5:4 G#4:4 B4:4 | C5:2 B4:2 A4:4 E5:4 |
F5:4 D5:2 E5:2 F5:2 D5:2 | B4:4 D5:2 C5:2 B4:2 G4:2 | C5:4 E5:4 G5:4 |
A5:4 F5:2 D5:2 C5:2 B4:2 | B4:8 .:4 |"""

# The viol's answer: lower and longer, a sequence down, the last bar at rest before the wrap.
VIOL = """E5:4 C5:2 D5:2 E5:4 | D5:4 B4:2 C5:2 D5:4 | C5:4 A4:2 B4:2 C5:4 | B4:8 G4:4 |
A4:4 C5:2 B4:2 A4:4 | D5:4 F5:2 E5:2 D5:4 | C5:4 B4:4 G#4:4 | B4:4 E4:4 .:4 |"""


def harpsichord(freq, dur):
    """The court's continuo: a string plucked by a quill and damped when the key lifts. Karplus-
    Strong with a brighter excitation than the harp's (0.8 against 0.45) and a second string an
    octave up at a third of the level - the 4-foot stop - both harmonic, lowpassed four-pole under
    3 kHz, and cut off at the written length by the damper rather than left to ring."""
    v = M._karplus(freq, dur, 0.996, 0.8, M._seed(freq, dur, 51))
    v2 = M._karplus(freq * 2.0, dur, 0.994, 0.75, M._seed(freq, dur, 53))
    v = S.mix(v, S.gain(v2, 0.32))
    v = M._lp4(v, min(3000.0, freq * 8.0))
    v = S.highpass(v, max(70.0, freq * 0.7))
    return M._end_fade(M._soft_attack(v, 0.002), min(0.04, dur * 0.3))


def viol(freq, dur):
    """A viol of the consort, bowed: a saw and a wide pulse through a bowed body - a resonance
    near the note and a fixed nasal one at 900 Hz, which is the gamba's colour where the Fields'
    fiddle is brighter at 1.25 kHz - with the faintest late vibrato (a viol player's is an ornament,
    not a habit) and a bow that swells a little into each note. Lowpassed under 2.2 kHz."""
    f = M._pitch(freq, dur, 5.0, 0.0022, 0.3)
    src = S.mix(S.gain(S.osc("saw", f, dur), 0.6),
                S.gain(S.osc("pulse", f, dur, duty=0.4, phase=0.2), 0.4))
    low = S.bandpass(src, max(300.0, freq * 1.1), 2.2)
    nasal = S.bandpass(src, 900.0, 2.6)
    warm = M._lp4(src, min(1400.0, freq * 2.6))
    v = M._lp4(S.mix(low, S.gain(nasal, 0.5), S.gain(warm, 0.55)), 2200.0)
    return S.env_mul(v, S.adsr(dur, a=0.07, d=0.12, s=0.85, r=min(0.2, dur * 0.3)))


def bed():
    g, ch = GRID, CHORDS
    in_b = lambda bar: bar >= B
    ev = M.held(g, PROG, lambda c: ch[c]["pad"], M.pad_strings,
                lambda bar: 0.3 if in_b(bar) else 0.34, 4)
    # The continuo, broken in eighths: up and back in A, turned over in B.
    ev += M.ost(g, PROG, lambda c: ch[c]["arp"],
                lambda bar: "0.2.1.3.1.2." if in_b(bar) else "0.1.2.3.2.1.", harpsichord, 3, 0.56)
    return ev


def pulse():
    g, ch = GRID, CHORDS
    ev = M.ost(g, PROG, lambda c: [ch[c]["root"]], "0...........", M.bass_pluck, 6, 0.62)
    # Pah-pah: the strings on beats two and three, the two inner tones of the chord.
    for k in xrange(2):
        ev += M.ost(g, PROG, lambda c, k=k: [ch[c]["low"][k]], "....0...0...", M.pizz, 3,
                    0.42 - 0.05 * k)
    for bar in xrange(len(PROG)):
        if bar in (B - 1, len(PROG) - 1):
            # The roll into each half - and the only place the drum plays in three.
            ev += M.hits(g, bar, "X...x.x.xxXX", M.frame_drum, "D3", 0.7, 2)
        elif bar % 2 == 0:
            # The dancers' steps: beats one and three of the first bar ...
            ev += M.hits(g, bar, "X.......x...", M.frame_drum, "D3", 0.8, 4)
        else:
            # ... and beat two of the second: three steps of two beats across two bars of three.
            ev += M.hits(g, bar, "....x.......", M.frame_drum, "D3", 0.8, 4)
            ev += M.hits(g, bar, "..........o.", M.frame_drum, "A3", 0.5, 2)
    return ev


def lead():
    g = GRID
    ev = M.line(g, 0, RECORDER, M.flute, 0.62, legato=0.5)
    ev += M.line(g, B, VIOL, viol, 0.6, legato=0.6)
    # In B the recorder shadows the viol an octave up, very quietly: the tune remembered.
    ev += M.line(g, B, VIOL, M.flute, 0.2, legato=0.5, shift=12)
    return ev


PIECE = M.Piece(
    "court", 112.0, 3, 16,
    [("bed", bed, 0.3), ("pulse", pulse, 0.18), ("lead", lead, 0.26)],
    tail_beats=6, scale=SCALE, room=(0.86, 0.5),
    describe="The Court - A minor with its leading note, a 3/4 minuet at 112, A/B of 8 bars")
