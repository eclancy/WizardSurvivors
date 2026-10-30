# -*- coding: utf-8 -*-
"""The Last Bridge's set (.ai/endgame-castle.md section 3, piece C7): a march under wind. Scored here
and rendered by tools/audio/music.py, which finds this module by its name and appends PIECE to its
chapters - run music.py, never this:

    python tools/audio/music.py bridge --scratch --balance   # iterate, into tools/audio/_music_preview/
    python tools/audio/music.py bridge                       # render into assets/music/, once

A MARCH IN 2/4 AT 120, A AEOLIAN. "His seat is across the water": a long stone bridge over the dark
sea, the gate's garrison coming down it at you, the span falling in behind. The level is a walk that
must not stop, so its music is the one thing in the game that walks - 2/4 at a quick-march 120, the
only piece in the set in a march metre, with the march's own figure (a dotted eighth, a sixteenth,
two eighths) in the low strings of every bar. A Aeolian, a mode of the seven notes like every chapter
on the road: the bridge is still outside his walls, so it keeps the world's notes and leaves his
G sharp and B flat to the Court and the Throne.

    A  Am Am G G | F F G G | Am Am Em Em      two horns in octaves state the march
    B  F F C C | Dm Dm Am Am | F F G G        fifes answer high, a horn an octave under them
    G wraps to Am: bVI - bVII - i, the march's own cadence

UNDER WIND. The bed carries the wind the bridge is crossed in: long gusts, swelling and going at
uneven places across the loop, written as notes so they fold across the seam like everything else.
They are the one new construct here and the reason for the rule below - the wind ELEMENT is air with
no bass (cast_wind is highpassed at 520 Hz and tops out near 2.8 kHz), so the bridge's wind is the
opposite: all bass, a moan of noise between 110 and 650 Hz whose band rises and falls inside each
gust. It is the sea wind far below the parapet, not a whistle past the ear, and it never enters the
band where the wind element's identity lives.

THE STEMS (audio-direction section 11):
  bed    held low strings, the march figure in spiccato on every bar, and the gusts
  pulse  the garrison's side drum on the march rhythm with a roll into each section, a bass drum on
         the beat, the bass plucked on the beats and pizzicato on the off-beats (oom-pah)
  lead   two horns in octaves (A); two fifes over a horn an octave under them (B)

24 BARS OF 2/4 - twelve of 4/4 - is 24.0 s, inside the 30 s Size rule, in three four-bar phrases a
section. NOT YET HEARD: composed, measured (--balance, --report, analyse.py --music, mixsim.py
--music) and rendered by an agent that cannot listen. The first listen will move things.
"""
from __future__ import division

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import music as M
import synth as S

GRID = M.Grid(4, 8)        # sixteenths: four to the quarter, eight to a 2/4 bar
B = 12

CHORDS = {
    # root (the bass), pad (the held strings), arp (the off-beat pizzicato), low (the march figure)
    "Am": M._ch("A2", ["A3", "C4", "E4"], ["C4", "E4", "A4"], ["A2", "E3", "A3"]),
    "G": M._ch("G2", ["G3", "B3", "D4"], ["B3", "D4", "G4"], ["G2", "D3", "G3"]),
    "F": M._ch("F2", ["F3", "A3", "C4"], ["A3", "C4", "F4"], ["F2", "C3", "F3"]),
    "Em": M._ch("E3", ["G3", "B3", "E4"], ["G3", "B3", "E4"], ["E3", "B3", "E4"]),
    "C": M._ch("C3", ["G3", "C4", "E4"], ["G3", "C4", "E4"], ["C3", "G3", "C4"]),
    "Dm": M._ch("D3", ["A3", "D4", "F4"], ["A3", "D4", "F4"], ["D3", "A3", "D4"]),
}
PROG = ("Am Am G G F F G G Am Am Em Em "
        "F F C C Dm Dm Am Am F F G G").split()

# The march, in the horns: every bar opens on the dotted figure the strings play under it.
HORNS = """A4:3 A4:1 C5:2 E5:2 | E5:3 D5:1 C5:4 | B4:3 B4:1 D5:2 B4:2 | D5:6 .:2 |
C5:3 C5:1 F5:2 E5:2 | D5:3 C5:1 A4:4 | B4:3 C5:1 D5:2 E5:2 | D5:4 B4:2 G4:2 |
A4:3 A4:1 C5:2 E5:2 | A5:4 E5:4 | G4:3 G4:1 B4:2 E5:2 | E5:6 .:2 |"""

# The fifes' answer, high and in longer notes, resting a half bar before the wrap so the horns come
# back in clean.
FIFES = """C6:4 A5:4 | F5:6 A5:2 | G5:4 E5:4 | C5:6 E5:2 |
F5:4 A5:4 | D6:6 C6:2 | A5:4 E5:4 | C6:6 B5:2 |
A5:4 F5:2 A5:2 | C6:4 A5:2 F5:2 | B5:3 A5:1 G5:2 D5:2 | G5:4 .:4 |"""

# The gusts: (bar, step, length in bars, level). Uneven on purpose - wind does not keep time - and
# overlapping, so the bed is never without air; the last runs past the loop and render() folds it
# onto the head, so the wind crosses the seam instead of stopping at it.
GUSTS = [(0, 0, 3.5, 0.55), (3, 4, 3.0, 0.44), (6, 2, 4.0, 0.68), (9, 4, 3.0, 0.48),
         (12, 0, 3.5, 0.64), (15, 4, 3.0, 0.48), (18, 0, 4.0, 0.74), (21, 2, 3.5, 0.53)]


def gust(freq, dur):
    """The wind the bridge is crossed under: noise in a band that rises from 180 to 420 Hz and falls
    back inside the gust - a moan - lowpassed four-pole at 650 Hz and highpassed at 110, swelling for
    two fifths of its length and dying over the last half. The wind element is the opposite construct
    by design (air with no bass, highpassed at 520 Hz, bands sweeping to 2.8 kHz), and nothing here
    is struck, so it cannot read as earth's rubble either: it has no attack at all."""
    n = S.n_samples(dur)
    band = [180.0 + 240.0 * math.sin(math.pi * i / max(1, n - 1)) for i in xrange(0, n, 64)]
    air = S.bandpass(S.osc("noise", 0, dur, seed=M._seed(freq, dur, 41)), band, 0.8)
    air = M._hp4(M._lp4(air, 650.0), 110.0)
    return S.env_mul(air, S.adsr(dur, a=dur * 0.4, d=0.1, s=0.9, r=dur * 0.48))


def field_drum(freq, dur):
    """The garrison's side drum: a deep shell under its wires. The wires are a band of noise at
    1.1 kHz, lowpassed four-pole at 2.3 kHz and dying in a seventh of a second - a rattle, not the
    boss loop's snare crack - and the shell a triangle falling 190 to 150 Hz. Banded, never an open
    highpass (audio-direction 6), and nothing in it rings: no element is a drum. At 1.3 and 2.8 kHz
    the pulse put 3% of its power in the 2-5 kHz band the element voices own; every other stem in
    the set keeps it at 2% or under."""
    n = S.n_samples(dur)
    wires = S.bandpass(S.osc("noise", 0, dur, seed=M._seed(freq, dur, 43)), 1100.0, 0.9)
    wires = S.env_mul(M._lp4(wires, 2300.0), M._ring(n, 0.07))
    shell = S.env_mul(S.osc("tri", S.sweep(dur, 190.0, 150.0, 0.5), dur), S.expdecay(n, 18.0))
    return M._end_fade(M._soft_attack(S.mix(wires, S.gain(shell, 0.7)), 0.001), min(0.02, dur * 0.2))


def bed():
    g, ch = GRID, CHORDS
    in_b = lambda bar: bar >= B
    ev = M.held(g, PROG, lambda c: ch[c]["pad"], M.pad_strings,
                lambda bar: 0.32 if in_b(bar) else 0.38, 4)
    # The march figure on every bar: a dotted eighth, a sixteenth, two eighths; turned in B.
    ev += M.ost(g, PROG, lambda c: ch[c]["low"],
                lambda bar: "0..12.1." if in_b(bar) else "0..10.2.", M.spiccato, 1.6, 0.66)
    for bar, step, bars, vel in GUSTS:
        ev.append((g.at(bar, step), gust, None, g.steps(int(bars * g.steps_per_bar)), vel))
    return ev


def pulse():
    g, ch = GRID, CHORDS
    # Oom-pah: the bass plucked on the beats, pizzicato from the chord on the off-beats.
    ev = M.ost(g, PROG, lambda c: [ch[c]["root"]], "0...0...", M.bass_pluck, 3, 0.62)
    ev += M.ost(g, PROG, lambda c: ch[c]["arp"], "..0...1.", M.pizz, 2, 0.55)
    for bar in xrange(len(PROG)):
        ev += M.hits(g, bar, "X...x...", M.taiko, "A2", 0.5, 6)
        if bar in (B - 1, len(PROG) - 1):
            # A roll into the next section, and across the seam into the first.
            ev += M.hits(g, bar, "xoxoxxXX", field_drum, None, 1.2, 1)
        else:
            ev += M.hits(g, bar, "X..xx.x." if bar % 2 == 0 else "x.o.x-xo", field_drum, None, 1.25, 2)
    return ev


def lead():
    g = GRID
    ev = M.line(g, 0, HORNS, M.horn, 0.6, legato=0.4)
    # The second horn an octave down: the march has weight before it has height.
    ev += M.line(g, 0, HORNS, M.horn, 0.34, legato=0.4, shift=-12)
    ev += M.line(g, B, FIFES, M.flute, 0.58, legato=0.4)
    ev += M.line(g, B, FIFES, M.horn, 0.28, legato=0.4, shift=-12)
    return ev


PIECE = M.Piece(
    "bridge", 120.0, 2, 24,
    [("bed", bed, 0.24), ("pulse", pulse, 0.14), ("lead", lead, 0.2)],
    tail_beats=8, scale=M.SEVEN, room=(0.72, 0.55),
    describe="The Last Bridge - A Aeolian, a 2/4 march at 120, A/B of 12 bars")
