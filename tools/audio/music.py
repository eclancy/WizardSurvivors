# -*- coding: utf-8 -*-
"""Generated music: a sequencer, a small orchestra, and a loop per chapter.

    python tools/audio/music.py                   # render every finished piece into assets/music/
    python tools/audio/music.py forest            # only pieces whose name contains this
    python tools/audio/music.py forest --scratch  # render to tools/audio/_music_preview/ instead
    python tools/audio/music.py --report          # measure what is on disk: seam, loudness, size
    python tools/audio/music.py --journey         # write an audition reel of a whole run's arc

The generators are the source and the .wav files are output, exactly as for assets/sfx/: edit a
piece here and re-run, never edit a rendered file. Every random draw is seeded, so a re-render is
byte-identical and a changed file always means changed code.

The contract this file works to is section 11 of .ai/audio-direction.md. In short:

  * 22050 Hz MONO, rendered natively at that rate. Music is pads, plucks and voices in the
    300 Hz - 2 kHz band, and nothing in it wants the octave above 11 kHz that 44100 would buy -
    the ceiling rule forbids most of it anyway. Halving the rate halves the git weight of every
    tuning pass, in a repo with LFS deliberately off (.gitattributes says why).
  * STEMS, not a mix. A chapter renders as three layers of constant density - bed, pulse and
    lead - that MusicPlayer crossfades by how hard the run is pressing. They share a tempo, a
    key and a sample count by construction, which is the one thing generated music is
    genuinely better at than a licensed track. Variation lives INSIDE every stem (an A and a B
    section with different harmony and timbre), never as a build or a drop, because a layer with
    its own arc cannot be muxed against the others.
  * THE FILE IS THE MIX. Each stem is normalised to an RMS target in dBFS, and the game plays it
    at unity, the same discipline tools/audio/build.py applies to effects. Rebalancing the music
    against the effects is one number in STEM_RMS_DB and a re-render.
  * SEAMLESS LOOPS. render() runs past the loop end and folds the overhang back onto the head,
    so a held note or a ringing string crosses the seam instead of being cut at it. Every
    filter and the reverb are run "loop-aware" (loop_fx below): primed with the end of the loop
    so they start in the state the end leaves them in. Without that the head of every loop opens
    with a reverb that has not filled yet - a breath every minute.
  * NOTHING BELOW 80 Hz, NOTHING SHRILL. Every stem passes MASTER_HIGHPASS and MASTER_LOWPASS.
    The sub band belongs to explosions and the boss (audio-direction section 6), and the 2-5 kHz
    band is where the element voices live.

The key choice is not arbitrary. The stingers in sfx.py are in A natural minor (level_up is
A-C-E-A, game_over descends through it). The Forest is in D Dorian, which is the same seven
notes - so the score and the interface never disagree, and the B natural that makes Dorian
Dorian is the one colour the forest owns. The boss loop stays in A minor.
"""
from __future__ import division

import math
import os
import random
import sys
import wave
import array

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import synth as S

MUSIC_SR = 22050
OUT = os.path.join(ROOT, "assets", "music")
SCRATCH = os.path.join(HERE, "_music_preview")

# The frequency budget, enforced on every stem. 4th-order (two biquad passes) both ways.
MASTER_HIGHPASS = 80.0
MASTER_LOWPASS = 4200.0

_PITCH_CLASS = {
    "C": -9, "C#": -8, "Db": -8, "D": -7, "D#": -6, "Eb": -6, "E": -5,
    "F": -4, "F#": -3, "Gb": -3, "G": -2, "G#": -1, "Ab": -1,
    "A": 0, "A#": 1, "Bb": 1, "B": 2,
}


def note(name):
    """Scientific pitch notation to Hz. note("A4") is 440."""
    i = len(name)
    while i > 0 and (name[i - 1].isdigit() or name[i - 1] == "-"):
        i -= 1
    return 440.0 * (2.0 ** ((_PITCH_CLASS[name[:i]] + (int(name[i:]) - 4) * 12) / 12.0))


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def _lp4(buf, cutoff, q=0.707):
    """24 dB/oct lowpass. A single biquad leaves a saw's upper harmonics audible above it,
    which is how a pad drifts shrill without anyone choosing that."""
    return S.lowpass(S.lowpass(buf, cutoff, q), cutoff, q)


def _hp4(buf, cutoff, q=0.707):
    return S.highpass(S.highpass(buf, cutoff, q), cutoff, q)


def _seed(freq, dur, salt):
    return int(freq * 1000.0) * 31 + int(dur * 1000.0) * 7 + salt


def _end_fade(buf, seconds):
    """Every voice reaches exactly zero at its end, so the note cache can never cut one off
    mid-cycle - the voice's own length is the only thing that ends it."""
    n = len(buf)
    k = min(n, S.n_samples(seconds))
    out = list(buf)
    for i in xrange(k):
        out[n - 1 - i] *= i / float(max(1, k))
    return out


def _soft_attack(buf, seconds):
    n = min(len(buf), S.n_samples(seconds))
    out = list(buf)
    for i in xrange(n):
        out[i] *= i / float(max(1, n))
    return out


def loop_fx(buf, fn, prime_seconds):
    """Run fn over a loop as if the loop had already been playing.

    Prepends the last prime_seconds of the loop, processes, and drops the prefix again. An IIR
    filter or a reverb started from silence spends its first moments settling, and on a loop
    that settling is heard once per cycle at exactly the seam."""
    p = min(len(buf), S.n_samples(prime_seconds))
    y = fn(buf[-p:] + buf)
    return y[p:p + len(buf)]


_HALL_COMB = [1116, 1188, 1277, 1356, 1422, 1491]
_HALL_ALLPASS = [556, 441, 341, 225]


def hall(buf, size=0.8, damp=0.45, mix=0.3, scale=1.35):
    """synth.reverb with its delay lengths scaled for the sample rate.

    synth.reverb counts its combs in samples tuned for 44100 Hz. At 22050 the same counts are
    twice as long in time and the comb flutter becomes audible, so the lengths are rescaled here
    rather than changing synth.py, whose effects must stay byte-identical."""
    k = scale * S.SR / 44100.0
    combs = [max(8, int(c * k)) for c in _HALL_COMB]
    alls = [max(8, int(a * k)) for a in _HALL_ALLPASS]
    n = len(buf)
    wet = [0.0] * n
    fb = 0.28 + 0.6 * max(0.0, min(1.0, size))
    for length in combs:
        line = [0.0] * length
        idx = 0
        store = 0.0
        for i in xrange(n):
            y = line[idx]
            store = y * (1.0 - damp) + store * damp
            line[idx] = buf[i] + store * fb
            idx += 1
            if idx >= length:
                idx = 0
            wet[i] += y
    inv = 1.0 / len(combs)
    for i in xrange(n):
        wet[i] *= inv
    for length in alls:
        line = [0.0] * length
        idx = 0
        for i in xrange(n):
            y = line[idx]
            line[idx] = wet[i] + y * 0.5
            wet[i] = y - wet[i]
            idx += 1
            if idx >= length:
                idx = 0
    return [buf[i] * (1.0 - mix) + wet[i] * mix for i in xrange(n)]


# ---------------------------------------------------------------------------
# instruments
# ---------------------------------------------------------------------------
# Every instrument returns a buffer that the sequencer peak-normalises, so a velocity in an
# event list means the same thing on every voice.

# The bed's held voices release over almost exactly the half-bar a chord overlaps the next
# (FOREST_PAD_OVERLAP, 0.94 s), and attack in about half that. A longer release starts the old
# chord fading BEFORE the new one arrives, and the bed dips at every change - the first render
# measured the loop's head 4.8 dB under its tail for exactly that reason.
BOW_ATTACK = 0.5
BOW_RELEASE = 1.0


def strings(freq, dur):
    """The forest's bed. Four detuned saws, slow bow, dark. The detune is what makes it an
    ensemble rather than an organ; the 4-pole lowpass is what keeps it out of the casts' band."""
    det = [(1.0, 0.0), (1.0041, 0.31), (0.9963, 0.62), (1.0087, 0.83)]
    v = S.mix(*[S.gain(S.osc("saw", freq * d, dur, phase=p), 0.28) for d, p in det])
    top = min(1400.0, freq * 5.0)
    v = _lp4(v, S.sweep(dur, top * 0.55, top, 0.6))
    v = S.highpass(v, max(60.0, freq * 0.6))
    return S.env_mul(v, S.adsr(dur, a=BOW_ATTACK, d=0.6, s=0.85, r=min(BOW_RELEASE, dur * 0.3)))


def choir(freq, dur):
    """A low "ah". A vibrato saw through two formant bandpasses (650 and 1080 Hz), which puts
    the whole voice in the band the music owns. Only sings in the B sections."""
    src = S.fm("saw", freq, 4.6, [0.0, 0.0025, 0.004], dur, seed=7)
    src2 = S.osc("pulse", freq * 1.003, dur, duty=0.32, phase=0.4)
    raw = S.mix(S.gain(src, 0.7), S.gain(src2, 0.3))
    f1 = S.bandpass(raw, 650.0, 4.0)
    f2 = S.bandpass(raw, 1080.0, 5.0)
    body = _lp4(raw, 380.0)
    v = S.mix(f1, S.gain(f2, 0.55), S.gain(body, 0.35))
    v = _lp4(v, 2200.0)
    return S.env_mul(v, S.adsr(dur, a=BOW_ATTACK + 0.2, d=0.5, s=0.9,
                                r=min(BOW_RELEASE, dur * 0.3)))


def drone(freq, dur):
    """The root under the bed, so the bed alone - the menu - still has a floor."""
    v = S.mix(S.gain(S.osc("tri", freq, dur), 0.7),
              S.gain(S.osc("sine", freq * 2.0, dur, phase=0.25), 0.22),
              S.gain(S.osc("saw", freq * 1.002, dur, phase=0.5), 0.10))
    v = _lp4(v, 520.0)
    return S.env_mul(v, S.adsr(dur, a=BOW_ATTACK + 0.3, d=0.4, s=1.0,
                                r=min(BOW_RELEASE, dur * 0.3)))


def _karplus(freq, dur, rho, bright, seed):
    """Karplus-Strong with a Jaffe-Smith allpass for the fractional part of the delay.

    Rounding the delay line to whole samples puts a 600 Hz note 24 cents flat at 22050 Hz,
    which in a harp ostinato is the difference between a string and an out-of-tune one."""
    n = S.n_samples(dur)
    p = S.SR / freq - 0.5          # the two-point average adds half a sample of delay
    length = int(p)
    frac = p - length
    if frac < 0.15:                # keep the allpass coefficient away from its unstable edge
        length -= 1
        frac += 1.0
    c = (1.0 - frac) / (1.0 + frac)
    rng = random.Random(seed)
    line = [rng.uniform(-1.0, 1.0) for _ in xrange(length)]
    # Lowpassed excitation: a dark pluck has a soft attack, which is what the mix needs.
    y = 0.0
    for _ in xrange(2):
        for i in xrange(length):
            y += bright * (line[i] - y)
            line[i] = y
    m = sum(line) / length
    line = [v - m for v in line]
    out = [0.0] * n
    idx = 0
    prev = 0.0
    ax = ay = 0.0
    for i in xrange(n):
        x = line[idx]
        out[i] = x
        avg = rho * 0.5 * (x + prev)
        prev = x
        ap = c * avg + ax - c * ay
        ax = avg
        ay = ap
        line[idx] = ap
        idx += 1
        if idx >= length:
            idx = 0
    return out


def harp(freq, dur):
    """The ostinato. A plucked string, lowpassed, with a 4 ms attack so it never ticks."""
    v = _karplus(freq, dur, 0.9965, 0.45, _seed(freq, dur, 1))
    v = _lp4(v, min(2400.0, freq * 6.0))
    return _end_fade(_soft_attack(v, 0.004), dur * 0.25)


def bass_pluck(freq, dur):
    """A plucked low string with a sine under it. Never below F2 (87 Hz) - see the budget."""
    v = _karplus(freq, dur, 0.995, 0.22, _seed(freq, dur, 2))
    sub = S.env_mul(S.osc("sine", freq, dur), S.expdecay(S.n_samples(dur), 3.0))
    v = S.mix(v, S.gain(sub, 0.5))
    v = _lp4(v, 750.0)
    return _end_fade(_soft_attack(v, 0.006), dur * 0.3)


def flute(freq, dur):
    """The A-section voice: a low wooden flute. Mostly sine, a breath of bandpassed noise,
    vibrato that arrives late in the note the way a player's does."""
    tone = S.fm("sine", freq, 5.0, [0.0, 0.0, 0.0045], dur, seed=3)
    tri = S.osc("tri", freq, dur, phase=0.1)
    oct2 = S.osc("sine", freq * 2.0, dur, phase=0.3)
    v = S.mix(tone, S.gain(tri, 0.18), S.gain(oct2, 0.07))
    breath = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 3)),
                        min(1800.0, freq * 2.0), 2.0)
    v = S.mix(v, S.gain(breath, 0.35))
    v = _lp4(v, 2000.0)
    return S.env_mul(v, S.adsr(dur, a=0.12, d=0.2, s=0.8, r=min(0.35, dur * 0.3)))


def horn(freq, dur):
    """The B-section voice: a horn, whose filter opens with its own loudness - that tracking
    is most of what reads as brass rather than as a filtered saw."""
    n = S.n_samples(dur)
    env = S.adsr(dur, a=0.16, d=0.3, s=0.82, r=min(0.45, dur * 0.35))
    f = S.fm("saw", freq, 4.8, [0.0, 0.002, 0.0035], dur, seed=4)
    sq = S.osc("square", freq * 0.998, dur, phase=0.2)
    v = S.mix(S.gain(f, 0.7), S.gain(sq, 0.3))
    cutoff = [min(1800.0, freq * (1.4 + 2.6 * e)) for e in env]
    v = _lp4(v, cutoff, 0.8)
    return S.env_mul(v, env[:n])


def frame_drum(freq, dur):
    """A skin drum. The pitch sweeps DOWN TO freq, never under it, so a drum written on G2
    stays at 98 Hz and out of the sub band."""
    n = S.n_samples(dur)
    body = S.osc("sine", S.sweep(dur, freq * 1.5, freq, 0.25), dur)
    body = S.env_mul(body, S.expdecay(n, 9.0))
    skin = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 5)), 700.0, 1.2)
    skin = S.env_mul(S.lowpass(skin, 1500.0), S.expdecay(n, 30.0))
    v = S.mix(body, S.gain(skin, 0.55))
    return _end_fade(_soft_attack(v, 0.002), 0.05)


# --- the boss loop's voices (the 2026 prototype, brought inside the budget) ---

def pad(freq, dur):
    """Three detuned saws opening slowly through a lowpass. The whole harmonic bed."""
    a = S.osc("saw", freq, dur)
    b = S.osc("saw", freq * 1.006, dur, phase=0.37)
    c = S.osc("saw", freq * 0.994, dur, phase=0.71)
    v = S.mix(S.gain(a, 0.40), S.gain(b, 0.32), S.gain(c, 0.32))
    v = _lp4(v, S.sweep(dur, 320, 1500, 1.0), 1.0)
    return S.env_mul(v, S.adsr(dur, a=0.50, d=0.5, s=0.78, r=0.9))


def bass(freq, dur):
    """Square bass with a fast filter envelope. The prototype drove it LAST, which is the
    "drive with nothing after it" hiss the ceiling rule forbids; the lowpass now follows."""
    v = S.mix(S.gain(S.osc("square", freq, dur), 0.6), S.gain(S.osc("sine", freq, dur), 0.7))
    v = S.lowpass(v, S.sweep(dur, 1400, 300, 2.2), 1.6)
    v = S.drive(S.env_mul(v, S.ad(dur, 0.006, 2.2)), 2.4)
    return _lp4(v, 1600.0)


def pluck(freq, dur):
    """The boss arpeggio. Short, filtered, dry - it is the rhythm, not the harmony."""
    v = S.osc("saw", freq, dur)
    v = S.lowpass(v, S.sweep(dur, 2600, 600, 2.6), 2.4)
    v = _lp4(v, 3000.0)
    return _end_fade(S.env_mul(v, S.ad(dur, 0.004, 3.2)), 0.01)


def lead(freq, dur):
    """A slow triangle-and-saw voice with a little vibrato. The only thing that sings."""
    f = S.fm("tri", freq, 5.2, 0.006, dur)
    v = S.mix(S.gain(f, 0.7), S.gain(S.osc("saw", freq * 1.004, dur), 0.25))
    v = _lp4(v, S.sweep(dur, 900, 2200, 0.8), 1.0)
    return S.env_mul(v, S.adsr(dur, a=0.09, d=0.2, s=0.7, r=min(0.5, dur * 0.4)))


def kick(freq, dur):
    """Swept 150 -> 85 Hz, not the prototype's 128 -> 42: the sub band is not the music's."""
    n = S.n_samples(dur)
    body = S.osc("sine", S.sweep(dur, 150, 85, 0.3), dur)
    v = S.drive(S.env_mul(body, S.expdecay(n, 7.0)), 2.0)
    return _end_fade(_lp4(v, 1200.0), 0.02)


def snare(freq, dur):
    """Backbeat. The prototype used the metal element's inharmonic ring here, which would have
    put impact_metal on every second beat of the boss fight. Now a band of noise over a tone."""
    n = S.n_samples(dur)
    air = S.bandpass(S.osc("noise", 0, dur, seed=311), S.sweep(dur, 2000, 1100, 0.8), 1.4)
    air = S.env_mul(_lp4(air, 3200.0), S.decay(n, 5.0))
    tone = S.env_mul(S.osc("tri", S.sweep(dur, 230, 180, 0.5), dur), S.expdecay(n, 14.0))
    return _end_fade(S.mix(air, S.gain(tone, 0.6)), 0.02)


def shaker(freq, dur):
    """In place of the prototype's hat, which was noise highpassed at 6.5 kHz with nothing
    above - a hiss to Nyquist on every eighth note. A band, not an open highpass."""
    n = S.n_samples(dur)
    v = S.bandpass(S.osc("noise", 0, dur, seed=313), 3600.0, 1.6)
    return _end_fade(S.env_mul(_lp4(v, 4200.0), S.decay(n, 6.0)), 0.01)


# ---------------------------------------------------------------------------
# the sequencer
# ---------------------------------------------------------------------------

_CACHE = {}


def _voice(instrument, freq, dur):
    """Render one note, or return the one already rendered. Velocity is applied by the caller so
    it never fragments the key - a note at two dynamics is one synthesis, not two."""
    key = (instrument.__name__, round(freq, 3), round(dur, 4))
    if key not in _CACHE:
        _CACHE[key] = S.normalize(S.dc_block(instrument(freq, dur)), 1.0)
    return _CACHE[key]


class Piece(object):
    """One loop: its clock, and a list of (stem name, event function, reverb mix).

    beat_seconds is the counted pulse - a dotted quarter for the forest's 6/8, a quarter for the
    boss's 4/4. Events are (beat, instrument, note name or None, length in beats, velocity)."""

    def __init__(self, name, bpm, beats_per_bar, bars, stems, tail_beats=8, describe=""):
        self.name = name
        self.bpm = bpm
        self.beat = 60.0 / bpm
        self.beats_per_bar = beats_per_bar
        self.bars = bars
        self.total_beats = bars * beats_per_bar
        self.tail_beats = tail_beats
        self.stems = stems
        self.describe = describe

    def seconds(self):
        return self.total_beats * self.beat


def render(piece, events):
    """Renders total_beats + tail_beats, then folds the overhang back onto the head, so anything
    still ringing at the loop point continues across the seam instead of being truncated."""
    loop_n = S.n_samples(piece.total_beats * piece.beat)
    full_n = S.n_samples((piece.total_beats + piece.tail_beats) * piece.beat)
    buf = [0.0] * full_n
    for beat, instrument, name, dur_beats, vel in events:
        freq = note(name) if name else 0.0
        v = _voice(instrument, freq, dur_beats * piece.beat)
        off = S.n_samples(beat * piece.beat)
        end = min(full_n, off + len(v))
        for j in xrange(off, end):
            buf[j] += v[j - off] * vel
    out = buf[:loop_n]
    for i in xrange(loop_n, full_n):
        out[(i - loop_n) % loop_n] += buf[i]
    return out


def master(buf, reverb_mix):
    """The budget, applied to every stem: highpass at 80 Hz, lowpass at 4.2 kHz, then the room.
    All three loop-aware, so the seam sees a filter and a reverb already in their steady state."""
    buf = loop_fx(buf, lambda b: _lp4(_hp4(b, MASTER_HIGHPASS), MASTER_LOWPASS), 1.0)
    if reverb_mix > 0.0:
        buf = loop_fx(buf, lambda b: hall(b, size=0.82, damp=0.5, mix=reverb_mix), 8.0)
    return S.dc_block(buf)


def normalize_rms(buf, target_db):
    r = S.rms(buf)
    if r <= 1e-9:
        return list(buf)
    g = (10.0 ** (target_db / 20.0)) / r
    return [v * g for v in buf]


# ---------------------------------------------------------------------------
# the Forest (chapter 1) - D Dorian, 6/8, dotted quarter = 64
# ---------------------------------------------------------------------------
# 32 bars of 6/8 at 64 dotted-quarters a minute is exactly 60 seconds. Two sections of 16 bars:
#
#   A  Dm Dm C C | F F G G | Dm Dm Am Am | G G Am Am      strings, flute, harp up-and-back
#   B  F F C C | G G Dm Dm | F F G G | Am Am Am Am        strings + choir, horns, harp turned
#
# G major is the Dorian chord, the B natural that the stingers' A minor shares and D minor does
# not have. It is the one bright colour in the wood and both sections lean on it before they
# turn back. The loop ends on A minor and returns to D minor: a modal v - i, a cadence without a
# leading note, which is why it does not sound like an ending every sixty seconds.

# The voicings sit from B3 to A4 and the harp from G3 to A4 on purpose. The first render voiced
# both an octave lower, and analyse.py --music measured 70% of the bed's power under 300 Hz -
# in the low band, where the thumps under every impact live. The register the music owns is
# 300 Hz to 2 kHz (audio-direction.md section 6); the bass and the drone are the only voices
# allowed under it, and both are kept quiet.
FOREST_CHORDS = {
    # name: (bass root, pad voicing, harp tones low to high)
    "Dm": ("D3", ["D4", "F4", "A4"], ["A3", "D4", "F4", "A4"]),
    "C": ("C3", ["C4", "E4", "G4"], ["G3", "C4", "E4", "G4"]),
    "F": ("F2", ["C4", "F4", "A4"], ["A3", "C4", "F4", "A4"]),
    "G": ("G2", ["B3", "D4", "G4"], ["G3", "B3", "D4", "G4"]),
    "Am": ("A2", ["C4", "E4", "A4"], ["A3", "C4", "E4", "A4"]),
}

FOREST_BARS = (["Dm", "Dm", "C", "C", "F", "F", "G", "G",
                "Dm", "Dm", "Am", "Am", "G", "G", "Am", "Am"]
               + ["F", "F", "C", "C", "G", "G", "Dm", "Dm",
                  "F", "F", "G", "G", "Am", "Am", "Am", "Am"])
FOREST_B = 16                      # first bar of the B section
EIGHTHS = 6                        # per bar
EIGHTH_BEATS = 1.0 / 3.0           # one eighth, in dotted-quarter beats


def _eighth(bar, e):
    return bar * 2 + e * EIGHTH_BEATS


def _chord_runs(bars):
    """[(first bar, length in bars, chord)] - consecutive equal chords held as one."""
    runs = []
    for i, c in enumerate(bars):
        if runs and runs[-1][2] == c:
            runs[-1] = (runs[-1][0], runs[-1][1] + 1, c)
        else:
            runs.append((i, 1, c))
    return runs


# A held chord runs half a bar past its run. That overlap crossfades one chord into the next
# instead of rearticulating on a hard edge, and - because render() folds the overhang onto the
# head - the last chord covers the first chord's attack across the loop seam.
FOREST_PAD_OVERLAP = 1.0           # beats


def forest_bed():
    ev = []
    for start, length, c in _chord_runs(FOREST_BARS):
        root, voicing, _harp = FOREST_CHORDS[c]
        beat = start * 2
        dur = length * 2 + FOREST_PAD_OVERLAP
        in_b = start >= FOREST_B
        for k, p in enumerate(voicing):
            # The strings step back in B so the choir can arrive without the bed getting louder:
            # a change of colour, not of level.
            ev.append((beat, strings, p, dur, (0.42 if in_b else 0.55) - 0.05 * k))
            if in_b:
                ev.append((beat, choir, p, dur, 0.34 - 0.04 * k))
        ev.append((beat, drone, root, dur, 0.30))
    return ev


HARP_PATTERN_A = [0, 1, 2, 3, 2, 1]        # up and back
HARP_PATTERN_B = [0, 2, 1, 3, 1, 2]        # the same tones, turned
HARP_ACCENT = [0.9, 0.5, 0.62, 0.78, 0.58, 0.48]
HARP_RING = 4.0 * EIGHTH_BEATS             # each pluck rings under the next three


def forest_pulse():
    ev = []
    for bar, c in enumerate(FOREST_BARS):
        root, _voicing, tones = FOREST_CHORDS[c]
        in_b = bar >= FOREST_B
        pattern = HARP_PATTERN_B if in_b else HARP_PATTERN_A
        for e in xrange(EIGHTHS):
            ev.append((_eighth(bar, e), harp, tones[pattern[e]], HARP_RING, 0.5 * HARP_ACCENT[e]))
        # Bass on both beats of the bar, the second one softer.
        ev.append((_eighth(bar, 0), bass_pluck, root, 2.0, 0.6))
        ev.append((_eighth(bar, 3), bass_pluck, root, 1.0, 0.32))
        # The drum. A section: the two beats and, every other bar, a pickup into the next.
        # B section: one more stroke on the third eighth, so B moves a little faster.
        # Last bar of each section: a three-stroke roll into the next, on a higher skin.
        if bar in (FOREST_B - 1, len(FOREST_BARS) - 1):
            ev.append((_eighth(bar, 0), frame_drum, "G2", 1.0, 0.6))
            for k, vel in enumerate([0.32, 0.42, 0.55]):
                ev.append((_eighth(bar, 3 + k), frame_drum, "C3", 0.6, vel))
            continue
        ev.append((_eighth(bar, 0), frame_drum, "G2", 1.0, 0.6))
        ev.append((_eighth(bar, 3), frame_drum, "G2", 1.0, 0.45))
        if bar % 2 == 1:
            ev.append((_eighth(bar, 5), frame_drum, "G2", 0.6, 0.24))
        if in_b:
            ev.append((_eighth(bar, 2), frame_drum, "G2", 0.6, 0.22))
    return ev


# (bar, eighth, note, length in eighths). A: the flute's tune, four phrases of four bars.
FOREST_THEME_A = [
    (0, 0, "A4", 3), (0, 3, "D5", 2), (0, 5, "C5", 1), (1, 0, "A4", 6),
    (2, 0, "G4", 3), (2, 3, "A4", 2), (2, 5, "G4", 1), (3, 0, "E4", 6),
    (4, 0, "F4", 3), (4, 3, "A4", 2), (4, 5, "C5", 1), (5, 0, "A4", 3), (5, 3, "G4", 3),
    (6, 0, "B4", 3), (6, 3, "D5", 2), (6, 5, "B4", 1), (7, 0, "A4", 6),
    (8, 0, "D5", 3), (8, 3, "F5", 2), (8, 5, "E5", 1), (9, 0, "D5", 3), (9, 3, "A4", 3),
    (10, 0, "C5", 3), (10, 3, "E5", 2), (10, 5, "D5", 1), (11, 0, "C5", 3), (11, 3, "A4", 3),
    (12, 0, "B4", 3), (12, 3, "A4", 2), (12, 5, "G4", 1), (13, 0, "D5", 6),
    (14, 0, "C5", 3), (14, 3, "B4", 2), (14, 5, "A4", 1), (15, 0, "A4", 6),
]

# B: the horns' answer - broader, longer notes, and two bars of rest before the loop turns, so
# the seam is a breath the tune takes rather than a cut.
FOREST_THEME_B = [
    (16, 0, "C5", 6), (17, 0, "A4", 3), (17, 3, "C5", 3),
    (18, 0, "E5", 6), (19, 0, "D5", 3), (19, 3, "C5", 3),
    (20, 0, "D5", 3), (20, 3, "B4", 3), (21, 0, "G4", 6),
    (22, 0, "A4", 3), (22, 3, "D5", 3), (23, 0, "F5", 4), (23, 4, "E5", 2),
    (24, 0, "F5", 3), (24, 3, "E5", 2), (24, 5, "D5", 1), (25, 0, "C5", 6),
    (26, 0, "D5", 3), (26, 3, "E5", 2), (26, 5, "D5", 1), (27, 0, "B4", 6),
    (28, 0, "C5", 3), (28, 3, "B4", 2), (28, 5, "A4", 1), (29, 0, "A4", 6),
]


def _down_octave(name):
    i = len(name)
    while i > 0 and name[i - 1].isdigit():
        i -= 1
    return name[:i] + str(int(name[i:]) - 1)


def forest_lead():
    ev = []
    for bar, e, p, length in FOREST_THEME_A:
        # Legato: each note overlaps the next by a sixteenth, so the flute never stops for air
        # between two notes of one phrase.
        ev.append((_eighth(bar, e), flute, p, length * EIGHTH_BEATS + 0.15, 0.62))
    for bar, e, p, length in FOREST_THEME_B:
        d = length * EIGHTH_BEATS + 0.15
        ev.append((_eighth(bar, e), horn, p, d, 0.58))
        # The second horn an octave down: weight without height.
        ev.append((_eighth(bar, e), horn, _down_octave(p), d, 0.36))
    return ev


FOREST = Piece(
    "forest", 64.0, 2, 32,
    [("bed", forest_bed, 0.32), ("pulse", forest_pulse, 0.18), ("lead", forest_lead, 0.26)],
    tail_beats=6,
    describe="Enchanted Forest - D Dorian, 6/8 at 64, A/B of 16 bars, 60 s")


# ---------------------------------------------------------------------------
# the boss loop - shared by every chapter until each has its own
# ---------------------------------------------------------------------------
# Twelve chord slots of eight beats each. A minor with a Neapolitan Bb and a harmonic-minor E:
# the bII and the raised leading note are where the menace lives, and the second half moves
# twice as fast harmonically so a 42-second loop does not sit still. Composition unchanged from
# the prototype; the voices above were brought inside the budget (kick, snare, shaker, bass).

BOSS_SLOTS = [
    ("A", "A2", ["A3", "C4", "E4"]),
    ("A", "A2", ["A3", "C4", "E4"]),
    ("Bb", "Bb2", ["Bb3", "D4", "F4"]),
    ("Bb", "Bb2", ["Bb3", "D4", "F4"]),
    ("A", "A2", ["A3", "C4", "E4"]),
    ("A", "A2", ["A3", "C4", "E4"]),
    ("F", "F2", ["F3", "A3", "C4"]),
    ("E", "E2", ["E3", "G#3", "B3"]),
    ("A", "A2", ["A3", "C4", "E4"]),
    ("Bb", "Bb2", ["Bb3", "D4", "F4"]),
    ("F", "F2", ["F3", "A3", "C4"]),
    ("E", "E2", ["E3", "G#3", "B3"]),
]
BOSS_SLOT_BEATS = 8
BOSS_BARS = 24

BOSS_MOTIF = [
    (32, "A4", 2.0), (34, "C5", 1.0), (35, "B4", 1.0), (36, "A4", 3.5),
    (40, "E5", 2.0), (42, "D5", 1.0), (43, "C5", 1.0), (44, "B4", 3.5),
    (48, "A4", 3.0), (51, "C5", 1.0), (52, "F4", 3.5),
    (56, "G#4", 3.5), (60, "B4", 2.0), (62, "E4", 2.0),
    (64, "A4", 2.0), (66, "E4", 1.0), (67, "F4", 1.0), (68, "E4", 3.5),
    (72, "D5", 2.0), (74, "Bb4", 2.0), (76, "F4", 3.5),
    (80, "A4", 2.0), (82, "C5", 2.0), (84, "A4", 3.5),
    (88, "G#4", 4.0), (92, "E4", 4.0),
]


def boss_events():
    """One stem, everything at once: the boss is the top of the run's intensity, so there is
    nothing to crossfade it against. Its lowest note is E2, 82 Hz - just inside the budget."""
    ev = []
    for k, (_name, root, voicing) in enumerate(BOSS_SLOTS):
        start = k * BOSS_SLOT_BEATS
        for v, pitch in enumerate(voicing):
            ev.append((start, pad, pitch, BOSS_SLOT_BEATS + 1.0, 0.5 - 0.06 * v))
        for e in xrange(BOSS_SLOT_BEATS * 2):
            beat = start + e * 0.5
            accent = 1.0 if e % 4 == 0 else (0.72 if e % 2 == 0 else 0.55)
            ev.append((beat, bass, root, 0.46, 0.42 * accent))
        ladder = voicing + [voicing[1]]
        for e in xrange(BOSS_SLOT_BEATS * 2):
            ev.append((start + e * 0.5, pluck, ladder[e % len(ladder)], 0.45, 0.20))
    for bar in xrange(BOSS_BARS):
        b = bar * 4
        ev.append((b + 0.0, kick, None, 0.5, 0.75))
        ev.append((b + 2.0, kick, None, 0.5, 0.65))
        if bar % 4 == 3:
            ev.append((b + 3.5, kick, None, 0.5, 0.5))
        ev.append((b + 1.0, snare, None, 0.5, 0.42))
        ev.append((b + 3.0, snare, None, 0.5, 0.42))
        for h in xrange(8):
            ev.append((b + h * 0.5, shaker, None, 0.12, 0.08 if h % 2 else 0.12))
    for beat, pitch, dur in BOSS_MOTIF:
        ev.append((beat, lead, pitch, dur, 0.55))
    return ev


BOSS = Piece("boss", 138.0, 4, BOSS_BARS, [("loop", boss_events, 0.22)],
             tail_beats=8, describe="the boss loop - A minor, 4/4 at 138, 24 bars, 42 s")


PIECES = [FOREST, BOSS]

# The file is the mix: each stem's RMS in dBFS, as the game plays it at unity on the Music bus
# with the slider at full. Set by tools/audio/mixsim.py --music, which measures every cast
# against the music under it. The first render sat 10 dB under the effects (bed -30, full set
# -26) and measured a median cast only +5.6 dB over the late-game music, the worst tenth +0.3;
# the music was costing a cast 2.4 dB of its headroom over the swarm, 4 dB in the sparse early
# game. Three dB down from there is the setting. The boss loop sits level with the full forest
# set, so the swap at the boss is a change of music and not a jump in loudness.
STEM_RMS_DB = {
    ("forest", "bed"): -33.0,
    ("forest", "pulse"): -34.0,
    ("forest", "lead"): -35.0,
    ("boss", "loop"): -29.5,
}
PEAK_CEILING_DB = -3.0


def file_name(piece, stem):
    return "%s_%s.wav" % (piece.name, stem) if piece.name != "boss" else "boss_loop.wav"


# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------

def build_piece(piece, out_dir):
    print("%s" % piece.describe)
    rendered = []
    for stem, fn, reverb_mix in piece.stems:
        buf = master(render(piece, fn()), reverb_mix)
        buf = normalize_rms(buf, STEM_RMS_DB[(piece.name, stem)])
        pk = S.db(S.peak(buf))
        if pk > PEAK_CEILING_DB:
            raise SystemExit("%s/%s peaks at %.1f dBFS, over the %.1f ceiling - lower its RMS "
                             "target or soften its transients" % (piece.name, stem, pk,
                                                                  PEAK_CEILING_DB))
        path = os.path.join(out_dir, file_name(piece, stem))
        # No fades anywhere: every one of these is a loop, and a fade on a loop is the click.
        S.write_wav(path, buf, sr=S.SR)
        rendered.append((stem, buf))
        print("  %-26s %6.2f s  rms %6.1f  peak %6.1f dBFS  %5.2f MB"
              % (os.path.basename(path), len(buf) / S.SR, S.db(S.rms(buf)), pk,
                 os.path.getsize(path) / 1048576.0))
    return rendered


def read_wav(path):
    w = wave.open(path, "rb")
    try:
        if w.getnchannels() != 1 or w.getsampwidth() != 2:
            raise SystemExit("%s: expected 16-bit mono" % path)
        sr = w.getframerate()
        raw = array.array("h")
        raw.fromstring(w.readframes(w.getnframes()))
    finally:
        w.close()
    return [v / 32768.0 for v in raw], sr


# ---------------------------------------------------------------------------
# measurement
# ---------------------------------------------------------------------------

def seam(buf, sr):
    """How the wrap point compares to the rest of the loop. Three numbers:

      step     |x[0] - x[-1]| against the 99.9th percentile sample-to-sample step in the body.
               Under 1.0 means the seam is a smaller jump than the loop makes on its own.
      click    energy of the first difference in a 4 ms window centred on the seam, against the
               99th percentile of the same window everywhere else. A click is a burst of
               high-frequency energy; under 1.0 means the seam has less of it than the music.
      dip      RMS of the one second centred on the seam against the median one-second window
               of the loop, in dB. Negative is the loop going quiet as it wraps: not a click, a
               breath once a minute. The prototype's boss bed measured its tail 7.6 dB under its
               body before its chords were made to overlap the seam. A stem with a written rest
               there (the forest lead) reads very negative and is meant to.
    """
    n = len(buf)
    steps = sorted([abs(buf[i] - buf[i - 1]) for i in xrange(1, n)])
    p999 = steps[int(len(steps) * 0.999)]
    jump = abs(buf[0] - buf[-1])
    w = max(4, int(0.004 * sr))
    diffs = [buf[i] - buf[i - 1] for i in xrange(n)]       # i = 0 wraps onto the tail: the seam
    energies = []
    for start in xrange(w, n - w, w):
        energies.append(sum([d * d for d in diffs[start:start + w]]))
    energies.sort()
    p99 = energies[int(len(energies) * 0.99)]
    around = [diffs[i % n] for i in xrange(-w // 2, w // 2)]
    seam_e = sum([d * d for d in around])
    half = int(0.5 * sr)
    windows = sorted([S.rms(buf[k:k + 2 * half]) for k in xrange(0, n - 2 * half, half)])
    median = windows[len(windows) // 2]
    at_seam = S.rms(buf[-half:] + buf[:half])
    dip = S.db(at_seam) - S.db(median)
    return jump / max(1e-9, p999), seam_e / max(1e-12, p99), dip


def report(paths):
    print("%-24s %6s %6s %7s %7s %6s  %6s %6s %7s"
          % ("file", "secs", "MB", "rms", "peak", "crest", "step", "click", "dip"))
    print("-" * 86)
    total = 0
    worst = 0.0
    for path in paths:
        buf, sr = read_wav(path)
        size = os.path.getsize(path)
        total += size
        step, click, dip = seam(buf, sr)
        worst = max(worst, step, click)
        pk, rm = S.db(S.peak(buf)), S.db(S.rms(buf))
        print("%-24s %6.2f %6.2f %7.1f %7.1f %6.1f  %6.2f %6.2f %+6.1fd"
              % (os.path.basename(path), len(buf) / float(sr), size / 1048576.0, rm, pk, pk - rm,
                 step, click, dip))
    print("-" * 86)
    print("%d files, %.2f MB in git. QOA (Godot's compress/mode=2) is ~3.2 bits a sample, so "
          "the export carries about %.2f MB." % (len(paths), total / 1048576.0,
                                                 total / 1048576.0 * 3.2 / 16.0))
    if worst >= 1.0:
        print("WARNING: a seam measures at or above the loop's own body - listen to that wrap")
    return 0


def journey(src_dir, out_dir):
    """An audition reel of one run's arc: bed alone, then pulse, then lead, then the boss, with
    the crossfades MusicPlayer uses. For listening to; nothing loads it."""
    bed, sr = read_wav(os.path.join(src_dir, "forest_bed.wav"))
    pulse, _ = read_wav(os.path.join(src_dir, "forest_pulse.wav"))
    lead_s, _ = read_wav(os.path.join(src_dir, "forest_lead.wav"))
    boss, _ = read_wav(os.path.join(src_dir, "boss_loop.wav"))
    n_loop = len(bed)
    total = int(n_loop * 3.5)
    boss_at = n_loop * 3
    fade = int(4.0 * sr)
    out = [0.0] * total

    def curve(i, on_at, off_at=None):
        g = 0.0 if i < on_at else min(1.0, (i - on_at) / float(fade))
        if off_at is not None and i >= off_at:
            g *= max(0.0, 1.0 - (i - off_at) / float(fade))
        return math.sin(g * math.pi * 0.5)

    for i in xrange(total):
        j = i % n_loop
        set_gain = curve(i, 0, boss_at)
        v = bed[j] + pulse[j] * curve(i, n_loop) + lead_s[j] * curve(i, n_loop * 2)
        out[i] = v * set_gain + boss[(i - boss_at) % len(boss)] * curve(i, boss_at)
    path = os.path.join(out_dir, "forest_journey.wav")
    S.write_wav(path, out, sr=sr)
    print("wrote %s - bed 0:00, +pulse %.0f s, +lead %.0f s, boss %.0f s"
          % (os.path.relpath(path, ROOT), n_loop / float(sr), 2 * n_loop / float(sr),
             boss_at / float(sr)))


def main(argv):
    S.SR = MUSIC_SR
    names = [a for a in argv if not a.startswith("--")]
    out_dir = SCRATCH if "--scratch" in argv else OUT
    if "--report" in argv:
        files = sorted([os.path.join(out_dir, f) for f in os.listdir(out_dir)
                        if f.endswith(".wav") and not f.endswith("_journey.wav")])
        return report(files)
    if "--journey" in argv:
        if not os.path.isdir(SCRATCH):
            os.makedirs(SCRATCH)
        journey(out_dir, SCRATCH)
        return 0
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    for piece in PIECES:
        if names and not any([n in piece.name for n in names]):
            continue
        build_piece(piece, out_dir)
    print("%d unique note renders cached" % len(_CACHE))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
