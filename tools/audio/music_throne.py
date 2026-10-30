# -*- coding: utf-8 -*-
"""The Throne's set (.ai/endgame-castle.md section 6, piece C7): the heaviest piece in the game, whose
pulse drops out in his second phase. Scored here and rendered by tools/audio/music.py, which finds
this module by its name and appends PIECE to its chapters - run music.py, never this:

    python tools/audio/music.py throne --scratch --balance   # iterate, into tools/audio/_music_preview/
    python tools/audio/music.py throne                       # render into assets/music/, once
    python tools/audio/music.py --journey --scratch          # includes throne_journey.wav, below

A MINOR WITH HIS B FLAT AND G SHARP, 4/4 AT 104, IN HALF TIME. The boss loop's own language - the
Neapolitan B flat and the dominant's G sharp, the notes the Emberdeep first borrowed because it was
the end (audio-direction section 11) - in the place it was always pointing at. The slowest beat of
any chapter, with the drums in half time over a sixteenth-note motor, and a bass that walks down the
lament (A, G, F, E) to E2, the lowest note the budget allows: weight is register and breadth, never
level. Every stem is held to the same RMS as every chapter's; the heaviest piece is not the loudest.

    A  Am Am/G F E | Dm Bb          low brass in octaves: the lament, then the bII
    B  Dm Am Bb F | Dm E            the choir chants over the brass; E wraps to Am, V - i

THE PULSE DROPS OUT IN HIS SECOND PHASE (6.4: "the hall goes dark but for him and the player"). That
is a fader, not a file: the set is three stems like every chapter, and every bit of drive is in the
pulse - the taiko, the snare, the square bass on a 3-3-2 and the sixteenth-note motor in the low
strings - so fading the pulse alone takes the floor out from under the fight and leaves the bed and
the lead complete without it: the choir, the held strings and the low brass pedal, a slow tread in
the low strings (a dotted quarter and an eighth, twice a bar - moving, never driving), and his theme.
So the second phase's version is bed + lead, the stems already rendered; the game switches by
MusicPlayer.SetLayerTargets(pulse: 0, lead: 1) when phase 2 begins, held against Node2DGame's
per-quarter-second layer rule until phase 3 brings the pulse back. throne_journey.wav (--journey)
plays exactly that, so it can be heard before it is wired.

UNLIKE EVERY OTHER CHAPTER, THE THRONE HAS NO BOSS LOOP. The whole chapter is the fight - the
Muster, the procession of wardens in pairs, then him - so the set should play from the first second
and never hand over to boss_loop.wav: bed alone under the Muster's seventeen level-ups (the run is
paused and the music is not), the pulse with the household guard and the procession, the lead from
the first warden, all three for his first phase, the pulse out for his second, back for his third,
and everything fading as his light goes out.

THE STEMS (audio-direction section 11):
  bed    full choir and strings held, a low brass pedal on root and fifth, the slow tread
  pulse  taiko in half time, snare on three with a roll into each section, the boss loop's square
         bass on a 3-3-2, and a sixteenth-note motor of spiccato low strings
  lead   low brass in octaves (A); the choir chanting over brass an octave under it (B)

Every voice is one the set already uses (choir, chant, strings, spiccato, horn, brass, the boss
loop's bass, taiko, snare), all harmonic, so nothing imitates an element (section 11). 12 bars is
27.7 s, inside the 30 s Size rule. NOT YET HEARD: composed, measured (--balance, --report,
analyse.py --music, mixsim.py --music) and rendered by an agent that cannot listen.
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

GRID = M.Grid(4, 16)
B = 6
SCALE = M.SEVEN | M.pitch_classes(["G#", "Bb"])

CHORDS = {
    # root (the bass's floor), pad (choir and strings), arp (the bass's pair), low (tread and motor)
    "Am": M._ch("A2", ["A3", "C4", "E4"], ["A2", "A3"], ["A2", "E3", "A3"]),
    # The lament's second step: A minor over G in the bass.
    "AmG": M._ch("G2", ["A3", "C4", "E4"], ["G2", "G3"], ["G2", "E3", "A3"]),
    "F": M._ch("F2", ["A3", "C4", "F4"], ["F2", "F3"], ["F2", "C3", "F3"]),
    # E2, 82 Hz: the floor of the budget, and the bottom of the lament.
    "E": M._ch("E2", ["G#3", "B3", "E4"], ["E2", "E3"], ["E2", "B2", "E3"]),
    "Dm": M._ch("D3", ["A3", "D4", "F4"], ["D3", "A2"], ["D3", "A3", "D4"]),
    "Bb": M._ch("Bb2", ["Bb3", "D4", "F4"], ["Bb2", "Bb3"], ["Bb2", "F3", "Bb3"]),
}
PROG = "Am AmG F E Dm Bb Dm Am Bb F Dm E".split()

# His theme, in the low brass: the lament's descent, then the B flat.
BRASS = """A3:4 A3:2 C4:2 E4:8 | E4:4 D4:2 C4:2 G3:8 | F4:6 E4:2 C4:8 | B3:4 G#3:4 E3:8 |
D4:4 F4:4 A4:8 | Bb4:6 A4:2 F4:4 D4:4 |"""

# The choir's chant over it in B, and a bar's rest before the brass comes back at the wrap.
CHANT = """A4:8 F4:8 | E4:8 A4:8 | D5:8 Bb4:4 A4:4 | A4:8 C5:8 | F4:6 E4:2 D4:8 | G#4:8 .:8 |"""


def bed():
    g, ch = GRID, CHORDS
    in_b = lambda bar: bar >= B
    ev = M.held(g, PROG, lambda c: ch[c]["pad"], M.choir, 0.42, 16, spread=0.03)
    ev += M.held(g, PROG, lambda c: ch[c]["pad"], M.pad_strings,
                 lambda bar: 0.34 if in_b(bar) else 0.38, 8)
    # The low brass pedal on root and fifth: what is left under the hall when the drums go.
    ev += M.held(g, PROG, lambda c: ch[c]["low"][:2], M.horn, 0.3, 8, spread=0.04)
    # The tread: a dotted quarter and an eighth, twice a bar - it walks, it does not drive.
    ev += M.ost(g, PROG, lambda c: ch[c]["low"],
                lambda bar: "0.....1.0...2.1." if in_b(bar) else "0.....1.0.....1.", M.spiccato, 2.0,
                1.0)
    return ev


def pulse():
    g, ch = GRID, CHORDS
    # The boss loop's square bass on a 3-3-2, root and octave: heavier than the Emberdeep's eighths.
    ev = M.ost(g, PROG, lambda c: ch[c]["arp"], "0..1..0.0..1..0.", M.bass, 2.5, 0.42)
    # The motor: sixteenths in the low strings, the whole drive of the piece.
    ev += M.ost(g, PROG, lambda c: ch[c]["low"], "0001000100010002", M.spiccato, 1.0, 1.0,
                accents="X-o-x-o-X-o-x-o-")
    for bar in xrange(len(PROG)):
        ev += M.hits(g, bar, "X.....x.X.....x." if bar % 2 else "X.......X..x..x.", M.taiko, "A2", 0.65, 8)
        if bar in (B - 1, len(PROG) - 1):
            ev += M.hits(g, bar, "....xoooxxxxXXXX", M.snare, None, 1.1, 1)
        else:
            ev += M.hits(g, bar, "........X.......", M.snare, None, 1.3, 4)
    return ev


def lead():
    g = GRID
    ev = M.line(g, 0, BRASS, M.brass, 0.64, legato=0.4)
    ev += M.line(g, 0, BRASS, M.brass, 0.4, legato=0.4, shift=-12)
    ev += M.line(g, B, CHANT, M.chant, 0.6, legato=0.6)
    ev += M.line(g, B, CHANT, M.brass, 0.34, legato=0.4, shift=-12)
    return ev


# --- the audition reel ---------------------------------------------------------------------------
# What the game will do with this set, which no other chapter does: bed alone (the Muster), the
# pulse in (the procession), the lead in (the wardens, then him), the pulse OUT over 8 s (his second
# phase, MusicPlayer's layer fade-out), the pulse back over 4 s (his third), and the end fade. The
# chapter reels in music.py cross to the shared boss loop instead, which the Throne never plays.

def journey(src_dir, out_dir):
    names = [M.file_name(PIECE, s) for s in ("bed", "pulse", "lead")]
    stems = [M._load(src_dir, n) for n in names]
    if None in stems:
        return None
    bed_s, pulse_s, lead_s = stems
    sr = M.MUSIC_SR
    n = len(bed_s)
    pulse_in, lead_in = n, 2 * n
    dark_at = 3 * n                     # his second phase: the pulse fades out over 8 s
    light_at = int(4.5 * n)             # his third: back over 4 s
    end_at = int(5.5 * n)
    total = end_at + int(M.END_FADE * sr)
    fade_out = int(8.0 * sr)
    fade_in = int(M.LAYER_FADE * sr)
    out = [0.0] * total
    for i in xrange(total):
        j = i % n
        if i < dark_at:
            p = M._fader(i, sr, pulse_in, M.LAYER_FADE)
        elif i < light_at:
            p = math.cos(min(1.0, (i - dark_at) / float(fade_out)) * math.pi * 0.5)
        else:
            p = math.sin(min(1.0, (i - light_at) / float(fade_in)) * math.pi * 0.5)
        v = bed_s[j] + pulse_s[j] * p + lead_s[j] * M._fader(i, sr, lead_in, M.LAYER_FADE)
        out[i] = v * M._fader(i, sr, 0, 0.0, end_at, M.END_FADE)
    path = os.path.join(out_dir, "throne_journey.wav")
    S.write_wav(path, out, sr=sr)
    print("wrote %s - bed 0:00, +pulse %.0f s, +lead %.0f s, pulse out (phase 2) %.0f s, "
          "back (phase 3) %.0f s; %.0f s" % (os.path.relpath(path, M.ROOT), pulse_in / float(sr),
                                             lead_in / float(sr), dark_at / float(sr),
                                             light_at / float(sr), total / float(sr)))
    return path


PIECE = M.Piece(
    "throne", 104.0, 4, 12,
    [("bed", bed, 0.3), ("pulse", pulse, 0.18), ("lead", lead, 0.26)],
    tail_beats=8, scale=SCALE, room=(0.92, 0.45),
    describe="The Throne - A minor with bII and V, 4/4 at 104 in half time, A/B of 6 bars")
PIECE.journey = journey
