# -*- coding: utf-8 -*-
"""Generated music: a sequencer, a small orchestra, a menu theme and a loop per chapter.

    python tools/audio/music.py                      # render every piece into assets/music/
    python tools/audio/music.py forest cave          # only pieces whose name contains one of these
    python tools/audio/music.py forest --scratch     # render to tools/audio/_music_preview/ instead
    python tools/audio/music.py --report [--scratch] # measure what is on disk: seam, loudness, size
    python tools/audio/music.py --journey [--scratch]  # one audition reel per chapter, plus a sampler
    python tools/audio/music.py cave --balance       # how loud each voice is inside each stem

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
  * ACTION, NOT AMBIENCE (2026-10-22). Every chapter runs at 104-168 to the counted beat, and
    every stem moves: the bed carries an ostinato under its chords, so even the opening seconds
    of a run, before the pulse is up, are already going somewhere; the pulse is drums and a
    driving bass; the lead is a hook short enough to be sung back. Loops are 24-30 s - two
    sections - because a minute of mono costs 2.5 MB of git history for every render.
  * THE FILE IS THE MIX. Each stem is normalised to an RMS target in dBFS, and the game plays it
    at unity, the same discipline tools/audio/build.py applies to effects. Rebalancing the music
    against the effects is one number in STEM_RMS_DB and a re-render.
  * SEAMLESS LOOPS. render() runs past the loop end and folds the overhang back onto the head,
    so a held note or a ringing string crosses the seam instead of being cut at it. Every
    filter and the reverb are run "loop-aware" (loop_fx below): primed with the end of the loop
    so they start in the state the end leaves them in. Without that the head of every loop opens
    with a reverb that has not filled yet - a breath every cycle.
  * NOTHING BELOW 80 Hz, NOTHING SHRILL. Every stem passes MASTER_HIGHPASS and MASTER_LOWPASS.
    The sub band belongs to explosions and the boss (audio-direction section 6), and the 2-5 kHz
    band is where the element voices live.
  * ONE SET OF SEVEN NOTES. The stingers in sfx.py are in A natural minor (level_up is A-C-E-A,
    game_over descends through it), and every piece here is a mode of those same seven notes -
    the mode is what carries a chapter's mood, so the interface never disagrees with the score.
    build_piece() refuses a note outside a piece's declared scale. Only the Emberdeep steps
    outside, with the boss loop's G sharp and B flat, because it is the end.
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
_NAMES = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B"]


def _split(name):
    i = len(name)
    while i > 0 and (name[i - 1].isdigit() or name[i - 1] == "-"):
        i -= 1
    return name[:i], int(name[i:])


def note(name):
    """Scientific pitch notation to Hz. note("A4") is 440."""
    pc, octave = _split(name)
    return 440.0 * (2.0 ** ((_PITCH_CLASS[pc] + (octave - 4) * 12) / 12.0))


def midi(name):
    pc, octave = _split(name)
    return 69 + _PITCH_CLASS[pc] + (octave - 4) * 12


def name_of(m):
    return "%s%d" % (_NAMES[m % 12], m // 12 - 1)


def transpose(name, semitones):
    return name_of(midi(name) + semitones)


def pitch_classes(names):
    return set([(_PITCH_CLASS[n] + 9) % 12 for n in names])


# A natural minor's seven notes. Every chapter is a mode of exactly these.
SEVEN = pitch_classes(["A", "B", "C", "D", "E", "F", "G"])


def scale_step(name, steps, pcs):
    """Move a note by a number of steps of a scale, so one riff can follow every chord of a
    progression without ever leaving the mode."""
    m = midi(name)
    d = 1 if steps > 0 else -1
    while steps != 0:
        m += d
        if m % 12 in pcs:
            steps -= d
    return name_of(m)


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


def _ring(n, tau):
    """A struck note's decay in real time - exp(-t / tau) - so a short written note and a long
    one ring at the same rate and only the written length cuts them."""
    k = 1.0 / max(1.0, tau * S.SR)
    return [math.exp(-i * k) for i in xrange(n)]


def _pitch(freq, dur, rate=5.0, depth=0.0, delay=0.15, bend_cents=0.0, bend_time=0.0):
    """A pitch curve: an optional bend up into the note from bend_cents below, and vibrato that
    arrives after `delay` seconds and grows over a quarter of a second, the way a player's does."""
    n = S.n_samples(dur)
    out = [0.0] * n
    nd = S.n_samples(delay)
    grow = max(1, S.n_samples(0.25))
    nb = S.n_samples(bend_time)
    w = 2.0 * math.pi * rate / S.SR
    for i in xrange(n):
        f = freq
        if i < nb:
            x = 1.0 - i / float(nb)
            f *= 2.0 ** (bend_cents * x * x / 1200.0)
        if depth > 0.0 and i > nd:
            f *= 1.0 + depth * min(1.0, (i - nd) / float(grow)) * math.sin(w * (i - nd))
        out[i] = f
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
#
# None of them may borrow an element's construct (audio-direction section 4 and 11): no
# inharmonic ring (metal, ice), no bell stack on G5 (light), no rising droplet (water), no 9 Hz
# gurgle (poison), no 17 Hz tremolo on detuned triangles (arcane). A chapter that did would put a
# fake cue under the real one. Every struck or plucked voice below is harmonic for that reason.

# The menu theme's held voices release over almost exactly the half-bar a chord overlaps the
# next, and attack in about half that. A longer release starts the old chord fading BEFORE the
# new one arrives, and the bed dips at every change - the first forest render measured the loop's
# head 4.8 dB under its tail for exactly that reason.
BOW_ATTACK = 0.5
BOW_RELEASE = 1.0

# The chapters' held voices. At 104-168 a chord can last under two seconds, and a half-second
# bow would still be arriving when the harmony moved on.
PAD_ATTACK = 0.16
PAD_RELEASE = 0.5


def strings(freq, dur):
    """The slow ensemble - the menu theme's bed. Four detuned saws, slow bow, dark. The detune is
    what makes it an ensemble rather than an organ; the 4-pole lowpass is what keeps it out of
    the casts' band."""
    det = [(1.0, 0.0), (1.0041, 0.31), (0.9963, 0.62), (1.0087, 0.83)]
    v = S.mix(*[S.gain(S.osc("saw", freq * d, dur, phase=p), 0.28) for d, p in det])
    top = min(1400.0, freq * 5.0)
    v = _lp4(v, S.sweep(dur, top * 0.55, top, 0.6))
    v = S.highpass(v, max(60.0, freq * 0.6))
    return S.env_mul(v, S.adsr(dur, a=BOW_ATTACK, d=0.6, s=0.85, r=min(BOW_RELEASE, dur * 0.3)))


def pad_strings(freq, dur):
    """The chapters' held strings: the same ensemble with a quicker bow."""
    det = [(1.0, 0.0), (1.0041, 0.31), (0.9963, 0.62), (1.0087, 0.83)]
    v = S.mix(*[S.gain(S.osc("saw", freq * d, dur, phase=p), 0.28) for d, p in det])
    top = min(1500.0, freq * 5.0)
    v = _lp4(v, S.sweep(dur, top * 0.6, top, 0.6))
    v = S.highpass(v, max(60.0, freq * 0.6))
    return S.env_mul(v, S.adsr(dur, a=PAD_ATTACK, d=0.4, s=0.85, r=min(PAD_RELEASE, dur * 0.3)))


def spiccato(freq, dur):
    """Short bowed strokes: the ostinato that drives every chapter's bed. Three detuned saws
    with a bite of attack and a filter that closes as the stroke dies - the chug under every
    chase cue, kept in the band the music owns by a 4-pole lowpass under 2.2 kHz."""
    det = [(1.0, 0.0), (1.006, 0.37), (0.9952, 0.71)]
    v = S.mix(*[S.gain(S.osc("saw", freq * d, dur, phase=p), 0.33) for d, p in det])
    top = min(2200.0, freq * 6.0)
    v = _lp4(v, S.sweep(dur, top, max(300.0, top * 0.3), 0.5))
    v = S.highpass(v, max(70.0, freq * 0.7))
    v = S.env_mul(v, S.ad(dur, 0.006, 2.2))
    return _end_fade(v, min(0.015, dur * 0.2))


def choir(freq, dur):
    """A low "ah". A vibrato saw through two formant bandpasses (650 and 1080 Hz), which puts
    the whole voice in the band the music owns. Held, as a pad."""
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


def chant(freq, dur):
    """The choir singing a tune: the same formants with a quick onset, so a melody in it
    articulates instead of swelling. The first formant follows a high note up rather than
    sitting under its fundamental."""
    src = S.fm("saw", freq, 5.0, [0.0, 0.0025, 0.004], dur, seed=9)
    src2 = S.osc("pulse", freq * 1.003, dur, duty=0.32, phase=0.4)
    raw = S.mix(S.gain(src, 0.7), S.gain(src2, 0.3))
    f1 = S.bandpass(raw, max(650.0, freq * 1.1), 4.0)
    f2 = S.bandpass(raw, 1080.0, 5.0)
    body = _lp4(raw, max(380.0, freq * 1.5))
    v = S.mix(f1, S.gain(f2, 0.55), S.gain(body, 0.4))
    v = _lp4(v, 2200.0)
    return S.env_mul(v, S.adsr(dur, a=0.08, d=0.25, s=0.85, r=min(0.35, dur * 0.3)))


def drone(freq, dur):
    """The root under the bed, so the bed alone still has a floor."""
    v = S.mix(S.gain(S.osc("tri", freq, dur), 0.7),
              S.gain(S.osc("sine", freq * 2.0, dur, phase=0.25), 0.22),
              S.gain(S.osc("saw", freq * 1.002, dur, phase=0.5), 0.10))
    v = _lp4(v, 520.0)
    return S.env_mul(v, S.adsr(dur, a=BOW_ATTACK + 0.3, d=0.4, s=1.0,
                                r=min(BOW_RELEASE, dur * 0.3)))


def organ(freq, dur):
    """The swamp's reed organ: two reeds 1.6 Hz apart, so every chord beats slowly the way an
    old pump organ's does. Amplitude beating, not the poison element's 9 Hz pitch gurgle."""
    a = S.osc("pulse", freq, dur, duty=0.3)
    b = S.osc("pulse", freq + 1.6, dur, duty=0.3, phase=0.4)
    c = S.osc("sine", freq * 2.0, dur, phase=0.2)
    v = S.mix(S.gain(a, 0.45), S.gain(b, 0.45), S.gain(c, 0.15))
    v = _lp4(v, min(1500.0, freq * 4.0))
    v = S.highpass(v, max(70.0, freq * 0.6))
    return S.env_mul(v, S.adsr(dur, a=0.05, d=0.2, s=0.8, r=min(0.3, dur * 0.3)))


def glass(freq, dur):
    """The frozen waste's pad: a sine with a soft octave and twelfth. Every partial is harmonic,
    which is the whole difference from the ice element's cracked 1 : 2.76 : 5.40 stack, and the
    shimmer is a slow 2.6 Hz swell, not a tremolo anyone would count."""
    v = S.mix(S.osc("sine", freq, dur),
              S.gain(S.osc("tri", freq * 2.0, dur, phase=0.3), 0.22),
              S.gain(S.osc("sine", freq * 3.0, dur, phase=0.6), 0.08))
    v = S.tremolo(v, 2.6, 0.16)
    v = _lp4(v, 2200.0)
    return S.env_mul(v, S.adsr(dur, a=0.3, d=0.4, s=0.85, r=min(0.6, dur * 0.3)))


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


def pizz(freq, dur):
    """Pizzicato: a plucked string that dies fast. Dry, round, never above 1.8 kHz."""
    v = _karplus(freq, dur, 0.985, 0.35, _seed(freq, dur, 12))
    v = _lp4(v, min(1800.0, freq * 5.0))
    return _end_fade(_soft_attack(v, 0.003), dur * 0.4)


def oud(freq, dur):
    """The sands' plucked string: brighter excitation than the harp and a wooden body
    resonance at 900 Hz - the bowl of a lute, not the frame of a harp."""
    v = _karplus(freq, dur, 0.994, 0.62, _seed(freq, dur, 11))
    v = S.mix(v, S.gain(S.bandpass(v, 900.0, 1.2), 0.5))
    v = _lp4(v, min(2600.0, freq * 7.0))
    return _end_fade(_soft_attack(v, 0.003), dur * 0.25)


def music_box(freq, dur):
    """A music-box tine, struck. Almost a pure sine - its octave dies in a few milliseconds - with
    a soft tick of comb on the attack, lowpassed at 2 kHz. Not a bell: the light element is an
    inharmonic stack that rings, and this is one partial that fades in about a third of a second."""
    n = S.n_samples(dur)
    v = S.mix(S.osc("sine", freq, dur),
              S.gain(S.env_mul(S.osc("sine", freq * 2.0, dur, phase=0.25), _ring(n, 0.04)), 0.18))
    v = S.env_mul(v, _ring(n, 0.32))
    tick = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 15)), 1800.0, 2.0)
    v = S.mix(v, S.gain(S.env_mul(tick, _ring(n, 0.006)), 0.12))
    v = _lp4(v, 2000.0)
    return _end_fade(_soft_attack(v, 0.0015), min(0.03, dur * 0.2))


def bell_pluck(freq, dur):
    """The ruins' soft bell: harmonic partials 1, 2, 3, a quick decay, lowpassed at 1.6 kHz. It
    neither rings nor stacks, and it is never written on G5, the light element's pitch."""
    n = S.n_samples(dur)
    v = S.mix(S.osc("sine", freq, dur),
              S.gain(S.osc("sine", freq * 2.0, dur, phase=0.2), 0.3),
              S.gain(S.osc("sine", freq * 3.0, dur, phase=0.4), 0.1))
    v = S.env_mul(v, _ring(n, 0.22))
    v = _lp4(v, 1600.0)
    return _end_fade(_soft_attack(v, 0.002), min(0.03, dur * 0.2))


def flute(freq, dur):
    """A low wooden flute. Mostly sine, a breath of bandpassed noise, vibrato that arrives late
    in the note the way a player's does."""
    tone = S.fm("sine", freq, 5.0, [0.0, 0.0, 0.0045], dur, seed=3)
    tri = S.osc("tri", freq, dur, phase=0.1)
    oct2 = S.osc("sine", freq * 2.0, dur, phase=0.3)
    v = S.mix(tone, S.gain(tri, 0.18), S.gain(oct2, 0.07))
    breath = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 3)),
                        min(1800.0, freq * 2.0), 2.0)
    v = S.mix(v, S.gain(breath, 0.35))
    v = _lp4(v, 2000.0)
    return S.env_mul(v, S.adsr(dur, a=0.06, d=0.2, s=0.8, r=min(0.3, dur * 0.3)))


def bass_flute(freq, dur):
    """The swamp's lead: a breathy low flute that bends up into every note from 90 cents under.
    A small, slow rise - the water element's droplet is a fast sweep over more than an octave."""
    f = _pitch(freq, dur, 4.8, 0.005, 0.25, bend_cents=-90.0, bend_time=0.14)
    v = S.mix(S.osc("sine", f, dur), S.gain(S.osc("tri", f, dur, phase=0.1), 0.25))
    breath = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 13)),
                        min(1400.0, freq * 2.0), 1.6)
    v = S.mix(v, S.gain(breath, 0.4))
    v = _lp4(v, 1800.0)
    return S.env_mul(v, S.adsr(dur, a=0.08, d=0.2, s=0.8, r=min(0.3, dur * 0.3)))


def voice(freq, dur):
    """The cave's wordless solo voice: an "oh", its two formants following the note so a high
    line does not sing through a filter tuned under it."""
    f = _pitch(freq, dur, 5.6, 0.007, 0.18)
    src = S.mix(S.gain(S.osc("saw", f, dur), 0.6), S.gain(S.osc("tri", f, dur, phase=0.2), 0.4))
    f1 = S.bandpass(src, max(520.0, freq * 1.02), 3.5)
    f2 = S.bandpass(src, max(900.0, freq * 1.6), 4.5)
    body = _lp4(src, min(900.0, freq * 2.2))
    v = S.mix(f1, S.gain(f2, 0.6), S.gain(body, 0.5))
    v = _lp4(v, 2000.0)
    return S.env_mul(v, S.adsr(dur, a=0.08, d=0.2, s=0.85, r=min(0.35, dur * 0.3)))


def reed(freq, dur):
    """A double reed - the sands' shawm and the swamp's answer. A narrow pulse, which is what a
    reed's spectrum looks like, through a nasal formant at 1.15 kHz, then lowpassed, so the
    buzz stays in the music's band."""
    f = _pitch(freq, dur, 5.4, 0.005, 0.12)
    src = S.mix(S.gain(S.osc("pulse", f, dur, duty=0.24), 0.7),
                S.gain(S.osc("saw", f, dur, phase=0.3), 0.3))
    nasal = S.bandpass(src, 1150.0, 3.0)
    body = _lp4(src, min(1600.0, freq * 3.0))
    v = _lp4(S.mix(S.gain(nasal, 0.8), S.gain(body, 0.6)), 2400.0)
    return S.env_mul(v, S.adsr(dur, a=0.04, d=0.15, s=0.85, r=min(0.2, dur * 0.3)))


def bassoon(freq, dur):
    """The dungeon's bass reed, short and dry: the reed's pulse through a formant near 480 Hz,
    lowpassed at 1.4 kHz. It plays the roots the drone does not."""
    src = S.mix(S.gain(S.osc("pulse", freq, dur, duty=0.3), 0.7),
                S.gain(S.osc("saw", freq * 1.002, dur, phase=0.2), 0.3))
    formant = S.bandpass(src, 480.0, 2.5)
    body = _lp4(src, min(900.0, freq * 4.0))
    v = _lp4(S.mix(formant, S.gain(body, 0.7)), 1400.0)
    return _end_fade(S.env_mul(v, S.ad(dur, 0.012, 1.6, hold=dur * 0.3)), min(0.02, dur * 0.2))


def horn(freq, dur):
    """A horn, whose filter opens with its own loudness - that tracking is most of what reads as
    brass rather than as a filtered saw."""
    n = S.n_samples(dur)
    env = S.adsr(dur, a=0.12, d=0.3, s=0.82, r=min(0.4, dur * 0.35))
    f = S.fm("saw", freq, 4.8, [0.0, 0.002, 0.0035], dur, seed=4)
    sq = S.osc("square", freq * 0.998, dur, phase=0.2)
    v = S.mix(S.gain(f, 0.7), S.gain(sq, 0.3))
    cutoff = [min(1800.0, freq * (1.4 + 2.6 * e)) for e in env]
    v = _lp4(v, cutoff, 0.8)
    return S.env_mul(v, env[:n])


def brass(freq, dur):
    """Low brass with a bite: the horn's loudness-tracking filter, but saw-heavy and quick to
    speak, so a rhythm written in it lands on the beat."""
    env = S.adsr(dur, a=0.035, d=0.22, s=0.72, r=min(0.22, dur * 0.3))
    f = S.fm("saw", freq, 5.0, [0.0, 0.0015, 0.003], dur, seed=6)
    b = S.osc("saw", freq * 1.004, dur, phase=0.5)
    v = S.mix(S.gain(f, 0.6), S.gain(b, 0.4))
    cutoff = [min(2000.0, freq * (1.3 + 3.4 * e)) for e in env]
    v = _lp4(v, cutoff, 0.9)
    return S.env_mul(v, env)


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


def taiko(freq, dur):
    """The big drum. Swept down to freq and never under it, driven for weight - and then
    lowpassed, because a drive with nothing after it is a hiss (audio-direction, the ceiling)."""
    n = S.n_samples(dur)
    body = S.osc("sine", S.sweep(dur, freq * 1.8, freq, 0.22), dur)
    body = S.env_mul(body, S.expdecay(n, 6.5))
    skin = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 7)), 450.0, 1.0)
    skin = S.env_mul(S.lowpass(skin, 1300.0), S.expdecay(n, 20.0))
    v = S.drive(S.mix(body, S.gain(skin, 0.45)), 1.8)
    v = _lp4(v, 1800.0)
    return _end_fade(_soft_attack(v, 0.002), 0.05)


def dull_drum(freq, dur):
    """A slack, damp skin - the swamp's drum. Mostly body; the skin noise lowpassed to a thud."""
    n = S.n_samples(dur)
    body = S.osc("sine", S.sweep(dur, freq * 1.25, freq, 0.3), dur)
    body = S.env_mul(body, S.expdecay(n, 11.0))
    skin = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 14)), 380.0, 1.0)
    skin = S.env_mul(_lp4(skin, 700.0), S.expdecay(n, 22.0))
    v = S.mix(body, S.gain(skin, 0.6))
    return _end_fade(_soft_attack(v, 0.003), 0.04)


def hand_drum(freq, dur):
    """The low stroke of a hand drum: a short swept body under a skin band at 1.1 kHz."""
    n = S.n_samples(dur)
    body = S.osc("sine", S.sweep(dur, freq * 1.3, freq, 0.3), dur)
    body = S.env_mul(body, S.expdecay(n, 14.0))
    skin = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 8)), 1100.0, 1.5)
    skin = S.env_mul(_lp4(skin, 2400.0), S.expdecay(n, 40.0))
    return _end_fade(_soft_attack(S.mix(body, S.gain(skin, 0.7)), 0.0015), 0.03)


def tek(freq, dur):
    """The rim stroke of a hand drum: a short band of noise over a quick tone. Banded and
    lowpassed - never an open highpass - and too short and tonal to read as the grass knock."""
    n = S.n_samples(dur)
    air = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 9)), 1700.0, 2.2)
    air = S.env_mul(_lp4(air, 3000.0), S.expdecay(n, 30.0))
    tone = S.env_mul(S.osc("tri", freq, dur), S.expdecay(n, 25.0))
    return _end_fade(S.mix(air, S.gain(tone, 0.4)), 0.01)


def brush(freq, dur):
    """A brushed snare: a wide band of noise at 1.4 kHz with a soft 8 ms onset - a swish, not
    a crack, and below the casts' band."""
    v = S.bandpass(S.osc("noise", 0, dur, seed=_seed(freq, dur, 10) + 1), 1400.0, 0.9)
    v = _lp4(v, 2800.0)
    return _end_fade(S.env_mul(v, S.ad(dur, 0.008, 2.0)), 0.01)


def rattle(freq, dur):
    """A seed rattle for the eighth-note drive. A band at 2.2 kHz, lowpassed at 3.2 - the boss
    loop's shaker sits at 3.6 kHz, and a chapter plays for fifteen minutes, not two."""
    n = S.n_samples(dur)
    v = S.bandpass(S.osc("noise", 0, dur, seed=317), 2200.0, 1.4)
    v = _lp4(v, 3200.0)
    return _end_fade(S.env_mul(v, S.decay(n, 5.0)), 0.008)


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


class Grid(object):
    """A piece's step grid. Events are written as (bar, step) and land on the piece's beats.

    steps_per_beat counts the smallest written note in one counted beat: four sixteenths in a
    quarter (4/4 and 3/4), six in a dotted quarter (6/8), two in an eighth (7/8, where the
    eighth is the beat). `groups` marks the strong steps of a bar when they are not simply the
    beats - [0, 4, 8] is 7/8 counted 2+2+3."""

    def __init__(self, steps_per_beat, steps_per_bar, groups=None):
        self.steps_per_beat = steps_per_beat
        self.steps_per_bar = steps_per_bar
        self.groups = groups

    def at(self, bar, step=0):
        return (bar * self.steps_per_bar + step) / float(self.steps_per_beat)

    def steps(self, n):
        return n / float(self.steps_per_beat)

    def accent(self, step):
        if self.groups is not None:
            strong = step in self.groups
        else:
            strong = step % self.steps_per_beat == 0
        if strong:
            return 1.0
        return 0.8 if step % 2 == 0 else 0.66


class Piece(object):
    """One loop: its clock, and a list of (stem name, event function, reverb mix).

    beat_seconds is the counted pulse - a dotted quarter for 6/8, a quarter for 4/4 and 3/4, an
    eighth for 7/8. Events are (beat, instrument, note name or None, length in beats, velocity).
    `scale` is the set of pitch classes the piece may use; build_piece() refuses any other."""

    def __init__(self, name, bpm, beats_per_bar, bars, stems, tail_beats=8, describe="",
                 scale=None, room=(0.82, 0.5)):
        self.name = name
        self.bpm = bpm
        self.beat = 60.0 / bpm
        self.beats_per_bar = beats_per_bar
        self.bars = bars
        self.total_beats = bars * beats_per_bar
        self.tail_beats = tail_beats
        self.stems = stems
        self.describe = describe
        self.scale = scale
        self.room = room

    def seconds(self):
        return self.total_beats * self.beat


def render(piece, events, parts=None):
    """Renders total_beats + tail_beats, then folds the overhang back onto the head, so anything
    still ringing at the loop point continues across the seam instead of being truncated.

    With `parts`, also sums each instrument on its own, for --balance."""
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
        if parts is not None:
            pb = parts.setdefault(instrument.__name__, [0.0] * full_n)
            for j in xrange(off, end):
                pb[j] += v[j - off] * vel
    out = buf[:loop_n]
    for i in xrange(loop_n, full_n):
        out[(i - loop_n) % loop_n] += buf[i]
    return out


def master(buf, reverb_mix, room=(0.82, 0.5)):
    """The budget, applied to every stem: highpass at 80 Hz, lowpass at 4.2 kHz, then the room.
    All three loop-aware, so the seam sees a filter and a reverb already in their steady state."""
    buf = loop_fx(buf, lambda b: _lp4(_hp4(b, MASTER_HIGHPASS), MASTER_LOWPASS), 1.0)
    if reverb_mix > 0.0:
        buf = loop_fx(buf, lambda b: hall(b, size=room[0], damp=room[1], mix=reverb_mix), 8.0)
    return S.dc_block(buf)


def normalize_rms(buf, target_db):
    r = S.rms(buf)
    if r <= 1e-9:
        return list(buf)
    g = (10.0 ** (target_db / 20.0)) / r
    return [v * g for v in buf]


# ---------------------------------------------------------------------------
# the score notation
# ---------------------------------------------------------------------------
# Four ways of writing events, so that a chapter's score reads as music rather than as tuples:
#
#   line()    a melody: "D5:4 A4:2 | F5:4 ..." - note:steps, "." a rest, "|" a barline that is
#             CHECKED, so a bar that does not add up fails at render rather than in the ear
#   held()    each chord of a progression held for its run of bars, overlapping the next
#   ost()     an ostinato: one character per step, a digit indexing the bar's chord tones
#   riff()    a figure in scale degrees from each bar's root, "0 . 1 -1", so one riff follows
#             every chord without leaving the mode
#   hits()    a drum pattern: one character per step, X x o - for four dynamics
#
# A chord is a dict: root (the bass), pad (the held voicing), arp and low (two sets of tones for
# the ostinati). A progression is one chord name per bar.

_VEL = {"X": 1.0, "x": 0.72, "o": 0.5, "-": 0.34}


def _ch(root, pad, arp=None, low=None):
    return {"root": root, "pad": pad, "arp": arp or [], "low": low or []}


def _pattern(g, text):
    p = text.replace(" ", "")
    if len(p) != g.steps_per_bar:
        raise ValueError("pattern %r has %d steps, the bar has %d" % (text, len(p), g.steps_per_bar))
    return p


def line(g, bar, text, inst, vel, legato=0.0, shift=0):
    ev = []
    start = pos = bar * g.steps_per_bar
    for tok in text.split():
        if tok == "|":
            if (pos - start) % g.steps_per_bar:
                raise ValueError("a bar of %s does not add up: %r" % (inst.__name__, text))
            continue
        name, n = tok.split(":")
        n = int(n)
        if name != ".":
            b, s = divmod(pos, g.steps_per_bar)
            p = transpose(name, shift) if shift else name
            ev.append((g.at(b, s), inst, p, g.steps(n + legato),
                       vel * (1.0 if s % g.steps_per_beat == 0 else 0.9)))
        pos += n
    if (pos - start) % g.steps_per_bar:
        raise ValueError("%s's line does not end on a barline: %r" % (inst.__name__, text))
    return ev


def _chord_runs(bars):
    """[(first bar, length in bars, chord)] - consecutive equal chords held as one."""
    runs = []
    for i, c in enumerate(bars):
        if runs and runs[-1][2] == c:
            runs[-1] = (runs[-1][0], runs[-1][1] + 1, c)
        else:
            runs.append((i, 1, c))
    return runs


def held(g, prog, part, inst, vel, overlap_steps, spread=0.05, only=None):
    """Each chord held for its run of equal bars and overlapping the next by overlap_steps, so
    one chord crossfades into the next instead of rearticulating on a hard edge - and, because
    render() folds the overhang onto the head, the last chord covers the first one's attack
    across the seam. `vel` may be a function of the run's first bar (a section's colour)."""
    ev = []
    for start, length, c in _chord_runs(prog):
        if only is not None and not only(start):
            continue
        dur = g.steps(length * g.steps_per_bar + overlap_steps)
        v = vel(start) if callable(vel) else vel
        for k, p in enumerate(part(c)):
            ev.append((g.at(start), inst, p, dur, v - spread * k))
    return ev


def ost(g, prog, tones, pattern, inst, length_steps, vel, accents=None, first=0):
    """`first` is the bar the progression slice starts on, for an ostinato in one section."""
    ev = []
    for k, c in enumerate(prog):
        bar = first + k
        pat = _pattern(g, pattern(bar) if callable(pattern) else pattern)
        acc = _pattern(g, accents) if accents else None
        tt = tones(c)
        for s, ch in enumerate(pat):
            if ch == ".":
                continue
            a = _VEL[acc[s]] if acc else g.accent(s)
            ev.append((g.at(bar, s), inst, tt[int(ch)], g.steps(length_steps), vel * a))
    return ev


def riff(g, prog, root, pcs, text, inst, length_steps, vel):
    toks = text.split()
    if len(toks) != g.steps_per_bar:
        raise ValueError("riff %r has %d steps, the bar has %d" % (text, len(toks), g.steps_per_bar))
    ev = []
    for bar, c in enumerate(prog):
        r = root(c)
        for s, t in enumerate(toks):
            if t == ".":
                continue
            ev.append((g.at(bar, s), inst, scale_step(r, int(t), pcs), g.steps(length_steps),
                       vel * g.accent(s)))
    return ev


def seq(g, bar, text, inst, length_steps, vel):
    """Literal notes, one token per step - for a figure that ignores the harmony on purpose."""
    toks = text.split()
    if len(toks) != g.steps_per_bar:
        raise ValueError("seq %r has %d steps, the bar has %d" % (text, len(toks), g.steps_per_bar))
    return [(g.at(bar, s), inst, t, g.steps(length_steps), vel * g.accent(s))
            for s, t in enumerate(toks) if t != "."]


def hits(g, bar, pattern, inst, pitch, vel, length_steps):
    return [(g.at(bar, s), inst, pitch, g.steps(length_steps), vel * _VEL[ch])
            for s, ch in enumerate(_pattern(g, pattern)) if ch != "."]


def _drone_runs(g, bars, pitches, vel, runs=2):
    """A drone in `runs` overlapping notes rather than one: a single note the length of the loop
    puts its own attack and release on the seam, where a chord change elsewhere would hide them."""
    ev = []
    span = bars // runs
    for r in xrange(runs):
        for p in pitches:
            ev.append((g.at(r * span), drone, p, g.steps((span + 1) * g.steps_per_bar), vel))
    return ev


# ---------------------------------------------------------------------------
# the menu theme - A Aeolian, 4/4, quarter = 72, 8 bars, one stem
# ---------------------------------------------------------------------------
# Calm, and dark: the last quiet moment before a shift that has no relief coming
# (world-and-tone.md). Nothing in any chapter is this slow. One stem, because the menu has no
# intensity to follow; MusicPlayer plays it as a solo loop and crossfades it against a run's set.
#
#   Am F C G | Am F Dm Em      the horn's theme over a harp, strings and a choir in the second
#                              half; the Em wraps to Am, a modal v - i with no leading note

MENU_GRID = Grid(4, 16)
MENU_CHORDS = {
    "Am": _ch("A2", ["C4", "E4", "A4"], ["A3", "C4", "E4", "A4"]),
    "F": _ch("F2", ["C4", "F4", "A4"], ["A3", "C4", "F4", "A4"]),
    "C": _ch("C3", ["C4", "E4", "G4"], ["G3", "C4", "E4", "G4"]),
    "G": _ch("G2", ["B3", "D4", "G4"], ["G3", "B3", "D4", "G4"]),
    "Dm": _ch("D3", ["D4", "F4", "A4"], ["A3", "D4", "F4", "A4"]),
    "Em": _ch("E3", ["E4", "G4", "B4"], ["G3", "B3", "E4", "G4"]),
}
MENU_PROG = "Am F C G Am F Dm Em".split()
MENU_THEME = """A4:8 E4:4 A4:4 | C5:12 A4:4 | G4:8 E4:4 G4:4 | D4:16 |
A4:8 E4:4 A4:4 | C5:8 D5:4 C5:4 | A4:8 F4:4 A4:4 | B4:8 G4:4 E4:4 |"""


def menu_theme():
    g, ch = MENU_GRID, MENU_CHORDS
    ev = held(g, MENU_PROG, lambda c: ch[c]["pad"], strings, 0.46, 8)
    ev += held(g, MENU_PROG, lambda c: [ch[c]["root"]], drone, 0.28, 8)
    ev += held(g, MENU_PROG, lambda c: ch[c]["pad"], choir, 0.26, 8, spread=0.03,
               only=lambda bar: bar >= 4)
    ev += ost(g, MENU_PROG, lambda c: ch[c]["arp"], "0.1.2.3.2.1.2.1.", harp, 6, 0.3)
    ev += line(g, 0, MENU_THEME, horn, 0.46, legato=1.0)
    for bar in xrange(len(MENU_PROG)):
        # A heartbeat, felt more than heard.
        ev += hits(g, bar, "X.o.............", frame_drum, "G2", 0.3, 4)
    return ev


MENU = Piece("menu", 72.0, 4, 8, [("theme", menu_theme, 0.32)], tail_beats=8, scale=SEVEN,
             describe="the menu theme - A Aeolian, 4/4 at 72, 8 bars")


# ---------------------------------------------------------------------------
# 1. the Enchanted Forest - D Dorian, 6/8, dotted quarter = 112, 24 bars
# ---------------------------------------------------------------------------
# The forest as a chase rather than a walk: the same mode, the same harp, flute and horns, at
# nearly twice the old 64. A gallop in the strings (eighth, two sixteenths, eighth) under every
# bar; the harp up-and-back in eighths; frame drums on both beats.
#
#   A  Dm Dm C C | F F G G | Dm Dm Am Am      the flute's hook, three 4-bar phrases
#   B  F G Am Am | F G Dm Dm | F G Am Am      the horns answer; a bar's rest before the wrap
#
# G major is the Dorian chord - the B natural that D minor does not have - and both sections
# lean on it. The loop ends on A minor and returns to D minor, a modal v - i.

FOREST_GRID = Grid(6, 12)
FOREST_CHORDS = {
    "Dm": _ch("D3", ["D4", "F4", "A4"], ["A3", "D4", "F4", "A4"], ["D3", "A3", "D4"]),
    "C": _ch("C3", ["C4", "E4", "G4"], ["G3", "C4", "E4", "G4"], ["C3", "G3", "C4"]),
    "F": _ch("F2", ["C4", "F4", "A4"], ["A3", "C4", "F4", "A4"], ["F3", "C4", "F4"]),
    "G": _ch("G2", ["B3", "D4", "G4"], ["G3", "B3", "D4", "G4"], ["G3", "D4", "G4"]),
    "Am": _ch("A2", ["C4", "E4", "A4"], ["A3", "C4", "E4", "A4"], ["A3", "E4", "A4"]),
}
FOREST_PROG = ("Dm Dm C C F F G G Dm Dm Am Am "
               "F G Am Am F G Dm Dm F G Am Am").split()
FOREST_B = 12
FOREST_FLUTE = """D5:4 A4:2 D5:4 E5:2 | F5:4 E5:2 D5:6 | E5:4 C5:2 E5:4 G5:2 | E5:4 D5:2 C5:6 |
C5:4 A4:2 C5:4 F5:2 | F5:4 E5:2 C5:6 | B4:4 G4:2 B4:4 D5:2 | D5:4 E5:2 D5:4 B4:2 |
D5:4 A4:2 D5:4 E5:2 | F5:4 E5:2 D5:4 F5:2 | E5:4 C5:2 A4:4 C5:2 | E5:8 .:4 |"""
FOREST_HORNS = """A4:4 C5:2 F5:6 | G5:4 F5:2 D5:6 | E5:12 | .:6 E5:2 F5:2 G5:2 |
A5:4 G5:2 F5:6 | B4:4 D5:2 G5:6 | F5:4 E5:2 D5:6 | E5:4 D5:2 A4:6 |
C5:4 F5:2 A5:6 | G5:4 F5:2 D5:6 | E5:12 | .:12 |"""


def forest_bed():
    g, ch = FOREST_GRID, FOREST_CHORDS
    in_b = lambda bar: bar >= FOREST_B
    # The strings step back in B so the choir arrives without the bed getting louder: a change
    # of colour, not of level.
    ev = held(g, FOREST_PROG, lambda c: ch[c]["pad"], pad_strings,
              lambda bar: 0.36 if in_b(bar) else 0.46, 6)
    ev += held(g, FOREST_PROG, lambda c: ch[c]["pad"], choir, 0.3, 12, spread=0.04, only=in_b)
    ev += held(g, FOREST_PROG, lambda c: [ch[c]["root"]], drone, 0.24, 12)
    ev += ost(g, FOREST_PROG, lambda c: ch[c]["low"],
              lambda bar: "0.121.0.121." if in_b(bar) else "0.112.0.112.", spiccato, 1.6, 0.62)
    return ev


def forest_pulse():
    g, ch = FOREST_GRID, FOREST_CHORDS
    ev = ost(g, FOREST_PROG, lambda c: ch[c]["arp"],
             lambda bar: "0.2.1.3.1.2." if bar >= FOREST_B else "0.1.2.3.2.1.", harp, 8, 0.42)
    ev += ost(g, FOREST_PROG, lambda c: [ch[c]["root"]], "0.....0...0.", bass_pluck, 5, 0.6)
    for bar in xrange(len(FOREST_PROG)):
        ev += hits(g, bar, "X........x..", frame_drum, "G2", 0.75, 6)
        if bar in (FOREST_B - 1, len(FOREST_PROG) - 1):
            ev += hits(g, bar, "......oxxxXX", frame_drum, "D3", 0.5, 2)
        else:
            ev += hits(g, bar, "......X.....", frame_drum, "D3", 0.55, 4)
            ev += hits(g, bar, "..o.x...o.x." if bar % 2 else "..o.o...o.xo", frame_drum, "A3",
                       0.32, 2)
    return ev


def forest_lead():
    g = FOREST_GRID
    ev = line(g, 0, FOREST_FLUTE, flute, 0.62, legato=0.5)
    ev += line(g, FOREST_B, FOREST_HORNS, horn, 0.58, legato=0.5)
    # The second horn an octave down: weight without height.
    ev += line(g, FOREST_B, FOREST_HORNS, horn, 0.34, legato=0.5, shift=-12)
    return ev


FOREST = Piece(
    "forest", 112.0, 2, 24,
    [("bed", forest_bed, 0.28), ("pulse", forest_pulse, 0.16), ("lead", forest_lead, 0.24)],
    tail_beats=4, scale=SEVEN,
    describe="Enchanted Forest - D Dorian, 6/8 at dotted quarter 112, A/B of 12 bars")


# ---------------------------------------------------------------------------
# 2. the Cursed Dungeon - B Locrian over a B drone, 4/4, quarter = 132, 16 bars
# ---------------------------------------------------------------------------
# A key whose home chord cannot come to rest: the prison. The bed's low strings saw at the two
# notes a semitone apart, B and C, in sixteenths, and never stop; the chords move above a drone
# that never moves. A bass reed and a muffled drum in a long room push it; brass states the hook
# with a "da-da DUM" pickup on every bar, and a male choir chants the answer.
#
#   A  Bdim C Bdim G | Bdim C Dm C
#   B  F Em Dm C | F G Am C            C wraps to Bdim: the Locrian fall, a semitone down

DUNGEON_GRID = Grid(4, 16)
DUNGEON_CHORDS = {
    "Bdim": _ch("B2", ["B3", "D4", "F4"]),
    "C": _ch("C3", ["C4", "E4", "G4"]),
    "G": _ch("G2", ["B3", "D4", "G4"]),
    "Dm": _ch("D3", ["D4", "F4", "A4"]),
    "Em": _ch("E3", ["E4", "G4", "B4"]),
    "F": _ch("F3", ["C4", "F4", "A4"]),
    "Am": _ch("A2", ["C4", "E4", "A4"]),
}
DUNGEON_PROG = "Bdim C Bdim G Bdim C Dm C F Em Dm C F G Am C".split()
DUNGEON_B = 8
DUNGEON_SAW_A = "B2 . B3 C4 B3 . B3 C4 B2 . B3 C4 B3 . C4 B3"
DUNGEON_SAW_B = "B2 . B3 . C4 . B3 . B2 . F3 . C4 B3 C4 ."
DUNGEON_BRASS = """B3:3 B3:1 B3:2 C4:2 D4:4 F4:4 | E4:3 E4:1 D4:2 E4:2 C4:8 |
B3:3 B3:1 B3:2 C4:2 D4:4 F4:4 | G4:3 G4:1 F4:2 D4:2 B3:8 |
F4:3 F4:1 E4:2 D4:2 C4:4 B3:4 | C4:3 C4:1 D4:2 E4:2 G4:8 |
A4:3 A4:1 G4:2 F4:2 D4:4 F4:4 | E4:4 C4:4 .:8 |"""
DUNGEON_CHANT = """F4:8 A4:4 G4:4 | G4:8 E4:8 | F4:8 D4:4 E4:4 | C4:12 .:4 |
A4:8 C5:4 B4:4 | B4:8 G4:8 | C5:6 B4:2 A4:8 | G4:8 .:8 |"""


def dungeon_bed():
    g, ch = DUNGEON_GRID, DUNGEON_CHORDS
    ev = held(g, DUNGEON_PROG, lambda c: ch[c]["pad"], pad_strings, 0.4, 8)
    ev += held(g, DUNGEON_PROG, lambda c: [transpose(p, -12) for p in ch[c]["pad"]], choir, 0.3,
               16, spread=0.04, only=lambda bar: bar >= DUNGEON_B)
    ev += _drone_runs(g, len(DUNGEON_PROG), ["B2"], 0.3)
    for bar in xrange(len(DUNGEON_PROG)):
        ev += seq(g, bar, DUNGEON_SAW_B if bar >= DUNGEON_B else DUNGEON_SAW_A, spiccato, 1.0, 1.0)
    return ev


def dungeon_pulse():
    g, ch = DUNGEON_GRID, DUNGEON_CHORDS
    ev = ost(g, DUNGEON_PROG, lambda c: [ch[c]["root"]], "0..0..0...0..0..", bassoon, 2, 0.6)
    for bar in xrange(len(DUNGEON_PROG)):
        ev += hits(g, bar, "X..x..x...X.x...", taiko, "B2", 0.7, 6)
        ev += hits(g, bar, "....X.......X...", snare, None, 1.0, 3)
        if bar in (DUNGEON_B - 1, len(DUNGEON_PROG) - 1):
            ev += hits(g, bar, "........x.x.xxXX", frame_drum, "E3", 0.5, 2)
    return ev


def dungeon_lead():
    g = DUNGEON_GRID
    ev = line(g, 0, DUNGEON_BRASS, brass, 0.62, legato=0.3)
    ev += line(g, DUNGEON_B, DUNGEON_CHANT, chant, 0.58, legato=0.6)
    ev += line(g, DUNGEON_B, DUNGEON_CHANT, brass, 0.3, legato=0.3, shift=-12)
    return ev


DUNGEON = Piece(
    "dungeon", 132.0, 4, 16,
    [("bed", dungeon_bed, 0.28), ("pulse", dungeon_pulse, 0.3), ("lead", dungeon_lead, 0.24)],
    tail_beats=8, scale=SEVEN, room=(0.88, 0.45),
    describe="Cursed Dungeon - B Locrian over a B drone, 4/4 at 132, A/B of 8 bars")


# ---------------------------------------------------------------------------
# 3. the Sunken Cave - A Aeolian, 3/4, quarter = 160, 24 bars
# ---------------------------------------------------------------------------
# The boss is a choir, so the cave sings before it does: the choir is forward in the bed, the
# lead is a wordless solo voice, and everything sits in a long cavern reverb. The drive is a
# fast 3/4 - a dark scherzo - with the low strings rolling up the chord a beat at a time in
# sixteenth tremolo, a hand drum, and pizzicato arpeggios in threes against the bar's twos.
#
#   A  Am Am F F | C C G G | Am Am Em Em
#   B  F F G G | Am Am C C | Dm Dm Em Em      Em wraps to Am

CAVE_GRID = Grid(4, 12)
CAVE_CHORDS = {
    "Am": _ch("A2", ["C4", "E4", "A4"], ["A3", "C4", "E4"], ["A2", "E3", "A3"]),
    "F": _ch("F2", ["C4", "F4", "A4"], ["A3", "C4", "F4"], ["F2", "C3", "F3"]),
    "C": _ch("C3", ["C4", "E4", "G4"], ["G3", "C4", "E4"], ["C3", "G3", "C4"]),
    "G": _ch("G2", ["B3", "D4", "G4"], ["G3", "B3", "D4"], ["G2", "D3", "G3"]),
    "Em": _ch("E3", ["E4", "G4", "B4"], ["G3", "B3", "E4"], ["E3", "B3", "E4"]),
    "Dm": _ch("D3", ["D4", "F4", "A4"], ["A3", "D4", "F4"], ["D3", "A3", "D4"]),
}
CAVE_PROG = ("Am Am F F C C G G Am Am Em Em "
             "F F G G Am Am C C Dm Dm Em Em").split()
CAVE_B = 12
CAVE_VOICE = """E5:8 A4:4 | C5:4 B4:4 A4:4 | C5:8 F5:4 | E5:12 |
G5:8 E5:4 | D5:4 C5:4 E5:4 | D5:12 | B4:4 C5:4 D5:4 |
E5:8 A4:4 | C5:4 B4:4 A4:4 | G4:8 B4:4 | E4:8 .:4 |
F5:4 E5:2 D5:2 C5:4 | A4:8 C5:4 | D5:4 C5:2 B4:2 D5:4 | G5:8 F5:4 |
E5:4 D5:2 C5:2 E5:4 | A5:8 G5:4 | E5:8 C5:4 | D5:4 E5:4 G5:4 |
F5:8 E5:4 | D5:4 C5:4 A4:4 | B4:12 | .:12 |"""


def cave_bed():
    g, ch = CAVE_GRID, CAVE_CHORDS
    ev = held(g, CAVE_PROG, lambda c: ch[c]["pad"], choir, 0.4, 12, spread=0.04)
    ev += held(g, CAVE_PROG, lambda c: ch[c]["pad"], pad_strings, 0.26, 6)
    ev += ost(g, CAVE_PROG, lambda c: ch[c]["low"], "000011112222", spiccato, 1.0, 0.95,
              accents="Xooox-o-x-o-")
    return ev


def cave_pulse():
    g, ch = CAVE_GRID, CAVE_CHORDS
    ev = ost(g, CAVE_PROG, lambda c: [ch[c]["root"]], "0.......0...", bass_pluck, 6, 0.62)
    ev += ost(g, CAVE_PROG, lambda c: ch[c]["arp"], "0.1.2.0.1.2.", pizz, 3, 0.46)
    for bar in xrange(len(CAVE_PROG)):
        ev += hits(g, bar, "X.....x.....", hand_drum, "A2", 1.0, 4)
        if bar in (CAVE_B - 1, len(CAVE_PROG) - 1):
            ev += hits(g, bar, "....x.xoxxXX", tek, "E4", 0.9, 1)
        else:
            ev += hits(g, bar, "....x...x.xo", tek, "E4", 0.9, 1)
    return ev


def cave_lead():
    return line(CAVE_GRID, 0, CAVE_VOICE, voice, 0.62, legato=0.4)


CAVE = Piece(
    "cave", 160.0, 3, 24,
    [("bed", cave_bed, 0.38), ("pulse", cave_pulse, 0.26), ("lead", cave_lead, 0.34)],
    tail_beats=6, scale=SEVEN, room=(0.93, 0.42),
    describe="Sunken Cave - A Aeolian, 3/4 at 160, A/B of 12 bars")


# ---------------------------------------------------------------------------
# 4. the Blighted Swamp - E Phrygian, 7/8 (2+2+3), eighth = 280, 16 bars
# ---------------------------------------------------------------------------
# A lurch: the bar is one eighth short, so the step never lands even. The reed organ pumps a
# chord on each of the three groups and beats slowly against itself; a plucked bass worries at
# the Phrygian half step (E-F) in eighths; a dull, slack drum marks the groups. The bass flute
# bends up into every note; a reed answers in B.
#
#   A  Em F Em Dm | Em F G F
#   B  Am G F Em | Am G F F         F wraps to Em: the Phrygian cadence, bII - i

SWAMP_GRID = Grid(2, 14, groups=[0, 4, 8])
SWAMP_CHORDS = {
    "Em": _ch("E3", ["E4", "G4", "B4"]),
    "F": _ch("F3", ["C4", "F4", "A4"]),
    "G": _ch("G2", ["D4", "G4", "B4"]),
    "Dm": _ch("D3", ["D4", "F4", "A4"]),
    "Am": _ch("A2", ["C4", "E4", "A4"]),
}
SWAMP_PROG = "Em F Em Dm Em F G F Am G F Em Am G F F".split()
SWAMP_B = 8
SWAMP_RIFF = "0 . 0 . 1 . 0 . 0 . -1 0 1 ."
SWAMP_FLUTE = """E4:4 F4:4 E4:6 | A4:4 G4:4 F4:6 | E4:4 G4:4 B4:6 | A4:4 F4:4 D4:6 |
E4:4 F4:4 E4:6 | C5:4 B4:4 A4:6 | B4:4 G4:4 D5:6 | C5:4 A4:2 F4:2 .:6 |"""
SWAMP_REED = """E5:4 D5:4 C5:6 | B4:4 D5:4 G4:6 | A4:4 C5:4 F5:6 | E5:14 |
E5:4 F5:4 E5:6 | D5:4 B4:4 G4:6 | A4:4 G4:4 F4:6 | E4:6 .:8 |"""


def swamp_bed():
    g, ch = SWAMP_GRID, SWAMP_CHORDS
    ev = held(g, SWAMP_PROG, lambda c: ch[c]["pad"], pad_strings, 0.3, 7)
    # The organ pumps the chord once per group - 2, 2, 3 - each stroke tied a step into the next.
    for bar, c in enumerate(SWAMP_PROG):
        for start, length, accent in ((0, 4, 1.0), (4, 4, 0.72), (8, 6, 0.86)):
            for k, p in enumerate(ch[c]["pad"]):
                ev.append((g.at(bar, start), organ, p, g.steps(length + 1),
                           (0.42 - 0.04 * k) * accent))
    return ev


def swamp_pulse():
    g, ch = SWAMP_GRID, SWAMP_CHORDS
    ev = riff(g, SWAMP_PROG, lambda c: ch[c]["root"], SEVEN, SWAMP_RIFF, bass_pluck, 2, 0.62)
    for bar in xrange(len(SWAMP_PROG)):
        ev += hits(g, bar, "X...x...X.x.x.", dull_drum, "A2", 0.7, 4)
        if bar in (SWAMP_B - 1, len(SWAMP_PROG) - 1):
            ev += hits(g, bar, "........xxxXXX", dull_drum, "D3", 0.5, 2)
        else:
            ev += hits(g, bar, "..x...x...x..x", brush, None, 0.65, 2)
    return ev


def swamp_lead():
    g = SWAMP_GRID
    ev = line(g, 0, SWAMP_FLUTE, bass_flute, 0.64, legato=0.5)
    ev += line(g, SWAMP_B, SWAMP_REED, reed, 0.56, legato=0.4)
    return ev


SWAMP = Piece(
    "swamp", 280.0, 7, 16,
    [("bed", swamp_bed, 0.3), ("pulse", swamp_pulse, 0.16), ("lead", swamp_lead, 0.24)],
    tail_beats=14, scale=SEVEN,
    describe="Blighted Swamp - E Phrygian, 7/8 (2+2+3) at eighth 280, A/B of 8 bars")


# ---------------------------------------------------------------------------
# 5. the Mystic Ruins - F Lydian, 4/4, quarter = 128, 16 bars
# ---------------------------------------------------------------------------
# Ancient wonder, and its loss; the raised fourth (B natural over F, the G major chord) is the
# wonder. An adventure cue: harp arpeggios in sixteenths, strings driving a 3-3-2 tresillo,
# a full kit of kick, snare, toms and rattle, a soft plucked bell on the off-beats, and the horn
# hook leaping a fourth and a fifth.
#
#   A  F G Am G | F G C C
#   B  Dm C F G | Dm C G G          G wraps to F: Lydian's II - I, a step down into the wonder

RUINS_GRID = Grid(4, 16)
RUINS_CHORDS = {
    "F": _ch("F2", ["C4", "F4", "A4"], ["F3", "A3", "C4", "E4"], ["F3", "C4", "F4"]),
    "G": _ch("G2", ["B3", "D4", "G4"], ["G3", "B3", "D4", "G4"], ["G3", "D4", "G4"]),
    "Am": _ch("A2", ["C4", "E4", "A4"], ["A3", "C4", "E4", "A4"], ["A3", "E4", "A4"]),
    "C": _ch("C3", ["C4", "E4", "G4"], ["G3", "C4", "E4", "G4"], ["C3", "G3", "C4"]),
    "Dm": _ch("D3", ["D4", "F4", "A4"], ["A3", "D4", "F4", "A4"], ["D3", "A3", "D4"]),
}
RUINS_PROG = "F G Am G F G C C Dm C F G Dm C G G".split()
RUINS_B = 8
RUINS_HORN_A = """C5:4 F5:4 E5:2 F5:2 G5:4 | G5:6 D5:2 B4:8 | C5:4 E5:4 D5:2 C5:2 A4:4 |
B4:6 G4:2 D5:8 | C5:4 F5:4 E5:2 F5:2 A5:4 | D5:4 G5:4 A5:4 G5:4 | C5:4 E5:4 G5:4 E5:4 |
E5:8 .:8 |"""
RUINS_HORN_B = """A4:8 F4:4 A4:4 | G4:8 E4:4 G4:4 | C5:8 A4:4 C5:4 | B4:12 D5:4 |
F5:8 E5:4 D5:4 | E5:8 C5:4 G4:4 | D5:6 E5:2 D5:4 B4:4 | G4:8 .:8 |"""


def ruins_bed():
    g, ch = RUINS_GRID, RUINS_CHORDS
    ev = held(g, RUINS_PROG, lambda c: ch[c]["pad"], pad_strings, 0.42, 8)
    ev += held(g, RUINS_PROG, lambda c: ch[c]["pad"], choir, 0.28, 16, spread=0.04,
               only=lambda bar: bar >= RUINS_B)
    ev += ost(g, RUINS_PROG, lambda c: ch[c]["low"], "0..1..2.0..1..2.", spiccato, 2.0, 0.6)
    return ev


def ruins_pulse():
    g, ch = RUINS_GRID, RUINS_CHORDS
    ev = ost(g, RUINS_PROG, lambda c: ch[c]["arp"], "0123212301232123", harp, 4, 0.3)
    ev += ost(g, RUINS_PROG, lambda c: [ch[c]["root"]], "0.....0.0...0...", bass_pluck, 4, 0.55)
    # The bell an octave over the chord's third tone, on the last off-beat of each half bar.
    ev += ost(g, RUINS_PROG, lambda c: [transpose(ch[c]["arp"][2], 12)], "......0.......0.",
              bell_pluck, 4, 0.3)
    for bar in xrange(len(RUINS_PROG)):
        ev += hits(g, bar, "X.....x.X.......", kick, None, 0.55, 4)
        ev += hits(g, bar, "x.o.x.o.x.o.x.o.", rattle, None, 0.45, 1)
        if bar in (RUINS_B - 1, len(RUINS_PROG) - 1):
            ev += hits(g, bar, "....X...x.x.xXXX", frame_drum, "D3", 0.55, 2)
        else:
            ev += hits(g, bar, "....X.......X...", snare, None, 1.0, 3)
    return ev


def ruins_lead():
    g = RUINS_GRID
    ev = line(g, 0, RUINS_HORN_A, horn, 0.62, legato=0.5)
    ev += line(g, RUINS_B, RUINS_HORN_B, horn, 0.58, legato=0.5)
    ev += line(g, RUINS_B, RUINS_HORN_B, flute, 0.34, legato=0.5)
    return ev


RUINS = Piece(
    "ruins", 128.0, 4, 16,
    [("bed", ruins_bed, 0.28), ("pulse", ruins_pulse, 0.16), ("lead", ruins_lead, 0.24)],
    tail_beats=8, scale=SEVEN,
    describe="Mystic Ruins - F Lydian, 4/4 at 128, A/B of 8 bars")


# ---------------------------------------------------------------------------
# 6. the Frozen Waste - C Ionian in open fifths, 4/4 at 144 in half time, 16 bars
# ---------------------------------------------------------------------------
# Stillness at speed: a major key with the warmth taken out. No chord has a third. The bed ticks
# root-fifth in eighths under a glass pad; the pulse is a heartbeat - lub-dub on the one - with a
# brush on the three, which halves the felt tempo while the eighths keep it running; sparse low
# plucks. A spare flute states a hook that rises a fifth and settles.
#
#   A  C C A A | F F G G
#   B  A G F C | A G D G            G wraps to C

FROZEN_GRID = Grid(4, 16)
FROZEN_CHORDS = {
    "C": _ch("C3", ["C4", "G4", "C5"], None, ["C4", "G4"]),
    "G": _ch("G2", ["D4", "G4", "D5"], None, ["G3", "D4"]),
    "A": _ch("A2", ["E4", "A4", "E5"], None, ["A3", "E4"]),
    "F": _ch("F2", ["C4", "F4", "C5"], None, ["F3", "C4"]),
    "D": _ch("D3", ["D4", "A4", "D5"], None, ["D4", "A4"]),
}
FROZEN_PROG = "C C A A F F G G A G F C A G D G".split()
FROZEN_B = 8
FROZEN_FLUTE = """G4:4 D5:4 C5:8 | G4:4 D5:4 E5:4 D5:4 | A4:4 E5:4 D5:8 | C5:4 B4:4 A4:8 |
F4:4 C5:4 A4:8 | G4:4 A4:4 C5:8 | D5:4 G5:4 F5:4 D5:4 | D5:8 .:8 |
E5:8 A5:8 | G5:8 D5:8 | F5:8 C5:4 F5:4 | E5:12 .:4 |
A5:8 G5:4 E5:4 | D5:8 B4:8 | A4:8 D5:8 | G4:8 .:8 |"""


def frozen_bed():
    g, ch = FROZEN_GRID, FROZEN_CHORDS
    ev = held(g, FROZEN_PROG, lambda c: ch[c]["pad"], glass, 0.38, 8)
    ev += held(g, FROZEN_PROG, lambda c: [ch[c]["root"], ch[c]["low"][0]], pad_strings, 0.28, 8)
    ev += ost(g, FROZEN_PROG, lambda c: ch[c]["low"], "0.1.0.1.0.1.0.1.", spiccato, 1.6, 1.0)
    return ev


def frozen_pulse():
    g, ch = FROZEN_GRID, FROZEN_CHORDS
    ev = ost(g, FROZEN_PROG, lambda c: [ch[c]["root"]], "0.....0...0.....", bass_pluck, 5, 0.6)
    for bar in xrange(len(FROZEN_PROG)):
        ev += hits(g, bar, "X..x......X..x.." if bar % 2 else "X..x............",
                   taiko, "G2", 0.65, 6)
        if bar in (FROZEN_B - 1, len(FROZEN_PROG) - 1):
            ev += hits(g, bar, "........x.x.xxXX", brush, None, 0.8, 2)
        else:
            ev += hits(g, bar, "........X.......", brush, None, 0.95, 4)
            ev += hits(g, bar, "..o...o...o...o.", brush, None, 0.5, 1)
    return ev


def frozen_lead():
    g = FROZEN_GRID
    ev = line(g, 0, FROZEN_FLUTE, flute, 0.64, legato=0.5)
    # In B the glass doubles the flute an octave down, very quietly: the line's own shadow.
    ev += line(g, FROZEN_B, FROZEN_FLUTE.split("|", 8)[-1], glass, 0.22, legato=0.5, shift=-12)
    return ev


FROZEN = Piece(
    "frozen", 144.0, 4, 16,
    [("bed", frozen_bed, 0.3), ("pulse", frozen_pulse, 0.16), ("lead", frozen_lead, 0.26)],
    tail_beats=8, scale=SEVEN, room=(0.86, 0.6),
    describe="Frozen Waste - C Ionian in open fifths, 4/4 at 144 (half time), A/B of 8 bars")


# ---------------------------------------------------------------------------
# 7. the Scorched Sands - G Mixolydian, 6/8, dotted quarter = 104, 24 bars
# ---------------------------------------------------------------------------
# Heat and distance; the flat seventh (F natural) is the desert's, and the hook lands on it in
# its first bar. A drone fifth, an oud riff in scale degrees from each bar's root, a hand drum's
# dum and tek, a rattle, and a lowpassed shawm.
#
#   A  G G F G | G G Dm F | C F G G
#   B  F F C C | Dm Dm G G | F F F F    F wraps to G: bVII - I, the desert's cadence

SANDS_GRID = Grid(6, 12)
SANDS_CHORDS = {
    "G": _ch("G2", ["B3", "D4", "G4"], None, ["G3"]),
    "F": _ch("F2", ["C4", "F4", "A4"], None, ["F3"]),
    "C": _ch("C3", ["C4", "E4", "G4"], None, ["C4"]),
    "Dm": _ch("D3", ["D4", "F4", "A4"], None, ["D4"]),
}
SANDS_PROG = ("G G F G G G Dm F C F G G "
              "F F C C Dm Dm G G F F F F").split()
SANDS_B = 12
SANDS_RIFF = "0 . 0 1 2 . 0 . -1 0 1 ."
SANDS_SHAWM = """D5:4 B4:2 D5:4 F5:2 | E5:2 D5:2 C5:2 B4:6 | C5:4 A4:2 C5:4 F5:2 | D5:12 |
G5:4 F5:2 E5:4 D5:2 | C5:2 B4:2 A4:2 B4:6 | A4:4 D5:2 F5:4 E5:2 | F5:4 E5:2 C5:6 |
E5:4 G5:2 E5:4 C5:2 | A4:4 C5:2 F5:4 E5:2 | D5:4 B4:2 G4:6 | D5:6 .:6 |
A4:6 C5:6 | F5:6 E5:4 D5:2 | E5:8 C5:4 | G4:12 |
A4:6 D5:6 | F5:4 E5:2 D5:6 | B4:6 D5:6 | G5:8 F5:4 |
F5:4 E5:2 F5:4 G5:2 | C5:4 A4:2 C5:4 D5:2 | F5:6 E5:4 D5:2 | C5:6 .:6 |"""


def sands_bed():
    g, ch = SANDS_GRID, SANDS_CHORDS
    ev = _drone_runs(g, len(SANDS_PROG), ["G2", "D3"], 0.24)
    ev += held(g, SANDS_PROG, lambda c: ch[c]["pad"], pad_strings, 0.3, 6)
    ev += riff(g, SANDS_PROG, lambda c: ch[c]["low"][0], SEVEN, SANDS_RIFF, oud, 3, 0.62)
    return ev


def sands_pulse():
    g, ch = SANDS_GRID, SANDS_CHORDS
    ev = ost(g, SANDS_PROG, lambda c: [ch[c]["root"]], "0.....0.....", bass_pluck, 5, 0.42)
    for bar in xrange(len(SANDS_PROG)):
        ev += hits(g, bar, "X.....X..x..", hand_drum, "G2", 0.8, 4)
        ev += hits(g, bar, "x.o.o.x.o.o.", rattle, None, 0.4, 1)
        if bar in (SANDS_B - 1, len(SANDS_PROG) - 1):
            ev += hits(g, bar, "..x.x.xxxxXX", tek, "D4", 0.85, 1)
        else:
            ev += hits(g, bar, "..x.x...x.xo", tek, "D4", 0.85, 1)
    return ev


def sands_lead():
    g = SANDS_GRID
    ev = line(g, 0, SANDS_SHAWM, reed, 0.6, legato=0.4)
    # In B a second reed an octave down: the line doubled, the way a pair of shawms play.
    ev += line(g, SANDS_B, SANDS_SHAWM.split("|", 12)[-1], reed, 0.3, legato=0.4, shift=-12)
    return ev


SANDS = Piece(
    "sands", 104.0, 2, 24,
    [("bed", sands_bed, 0.26), ("pulse", sands_pulse, 0.14), ("lead", sands_lead, 0.22)],
    tail_beats=4, scale=SEVEN,
    describe="Scorched Sands - G Mixolydian, 6/8 at dotted quarter 104, A/B of 12 bars")


# ---------------------------------------------------------------------------
# 8. the Emberdeep - A minor with the Neapolitan B flat and E major, 4/4, quarter = 150
# ---------------------------------------------------------------------------
# The descent and the end, and the only chapter outside the seven notes: the boss loop's own
# language - the bII and the raised leading note - in the fastest 4/4 in the game. A full
# choir; low strings in a sixteenth-note motor; taiko, a four-on-the-floor march kick, a snare
# that rolls into each section; the boss loop's square bass in octaves; low brass.
#
#   A  Am Am Bb Bb | Am F E E
#   B  Dm Bb F E | Dm Bb E E        E wraps to Am: the harmonic V - i, with a leading note

EMBER_GRID = Grid(4, 16)
EMBER_SCALE = SEVEN | pitch_classes(["G#", "Bb"])
EMBER_CHORDS = {
    "Am": _ch("A2", ["A3", "C4", "E4"], ["A2", "A3"], ["A2", "E3", "A3"]),
    "Bb": _ch("Bb2", ["Bb3", "D4", "F4"], ["Bb2", "Bb3"], ["Bb2", "F3", "Bb3"]),
    "F": _ch("F2", ["A3", "C4", "F4"], ["F2", "F3"], ["F2", "C3", "F3"]),
    "E": _ch("E3", ["G#3", "B3", "E4"], ["E2", "E3"], ["E3", "B3", "E4"]),
    "Dm": _ch("D3", ["A3", "D4", "F4"], ["D3", "A2"], ["D3", "A3", "D4"]),
}
EMBER_PROG = "Am Am Bb Bb Am F E E Dm Bb F E Dm Bb E E".split()
EMBER_B = 8
EMBER_BRASS = """A3:4 A3:2 C4:2 E4:8 | D4:4 C4:2 B3:2 A3:8 | Bb3:4 Bb3:2 D4:2 F4:8 |
F4:4 D4:2 C4:2 Bb3:8 | A3:4 C4:2 E4:2 A4:8 | A4:4 G4:2 F4:2 C4:8 | G#4:8 E4:4 B3:4 | E4:8 .:8 |"""
EMBER_CHOIR = """F4:8 A4:8 | Bb4:8 F4:8 | A4:8 C5:8 | B4:8 G#4:8 |
A4:6 F4:2 D5:8 | D5:6 C5:2 Bb4:8 | G#4:8 B4:8 | E4:8 .:8 |"""


def ember_bed():
    g, ch = EMBER_GRID, EMBER_CHORDS
    ev = held(g, EMBER_PROG, lambda c: ch[c]["pad"], choir, 0.42, 16, spread=0.03)
    ev += held(g, EMBER_PROG, lambda c: ch[c]["pad"], pad_strings, 0.36, 8)
    ev += ost(g, EMBER_PROG, lambda c: ch[c]["low"], "0.010.010.020.01", spiccato, 1.0, 1.0)
    return ev


def ember_pulse():
    g, ch = EMBER_GRID, EMBER_CHORDS
    ev = ost(g, EMBER_PROG, lambda c: ch[c]["arp"], "0.1.0.1.0.1.0.1.", bass, 1.8, 0.42)
    # The boss loop's arpeggio voice, climbing the chord on the off-sixteenths: the descent
    # already speaks the fight's language before the fight.
    ev += ost(g, EMBER_PROG, lambda c: ch[c]["pad"], ".0.1.2.1.0.1.2.1", pluck, 1.8, 0.5)
    for bar in xrange(len(EMBER_PROG)):
        ev += hits(g, bar, "X...X...X...X...", kick, None, 0.6, 4)
        ev += hits(g, bar, "X......xX...x.x.", taiko, "A2", 0.8, 6)
        if bar in (EMBER_B - 1, len(EMBER_PROG) - 1):
            ev += hits(g, bar, "....xoooxxxxXXXX", snare, None, 0.9, 1)
        else:
            ev += hits(g, bar, "....X.......X.o.", snare, None, 1.0, 3)
    return ev


def ember_lead():
    g = EMBER_GRID
    ev = line(g, 0, EMBER_BRASS, brass, 0.64, legato=0.3)
    ev += line(g, EMBER_B, EMBER_CHOIR, chant, 0.58, legato=0.6)
    ev += line(g, EMBER_B, EMBER_CHOIR, brass, 0.32, legato=0.3, shift=-12)
    return ev


EMBER = Piece(
    "emberdeep", 150.0, 4, 16,
    [("bed", ember_bed, 0.26), ("pulse", ember_pulse, 0.14), ("lead", ember_lead, 0.22)],
    tail_beats=8, scale=EMBER_SCALE, room=(0.86, 0.5),
    describe="The Emberdeep - A minor with bII and V, 4/4 at 150, A/B of 8 bars")


# ---------------------------------------------------------------------------
# 9. the Sketchbook - C major, 3/4, quarter = 168, 24 bars
# ---------------------------------------------------------------------------
# The kids' chapter, the one place he has not been. A toy waltz, quick on its feet: pizzicato
# oom-pah-pah, soft strings, a brushed drum, the harp in eighths, and the tune on a music box -
# a toy taken seriously by whoever owns it. Nothing in it mocks the drawings, and nothing
# pretends to be what they drew. In B a soft flute takes the tune and the music box counts the
# beats above it.
#
#   A  C C F F | G G C C | Am F G G
#   B  F F C C | Dm G Em Am | F G C G       G wraps to C: the only perfect cadence in the set

SKETCH_GRID = Grid(4, 12)
SKETCH_CHORDS = {
    "C": _ch("C3", ["C4", "E4", "G4"], ["C5", "E5", "G5"], ["E4", "G4"]),
    "F": _ch("F2", ["C4", "F4", "A4"], ["C5", "F5", "A5"], ["F4", "A4"]),
    "G": _ch("G2", ["B3", "D4", "G4"], ["B4", "D5", "G5"], ["D4", "G4"]),
    "Am": _ch("A2", ["C4", "E4", "A4"], ["C5", "E5", "A5"], ["C4", "E4"]),
    "Dm": _ch("D3", ["D4", "F4", "A4"], ["D5", "F5", "A5"], ["F4", "A4"]),
    "Em": _ch("E3", ["E4", "G4", "B4"], ["E5", "G5", "B5"], ["G4", "B4"]),
}
SKETCH_PROG = ("C C F F G G C C Am F G G "
               "F F C C Dm G Em Am F G C G").split()
SKETCH_B = 12
SKETCH_BOX = """C5:4 E5:4 G5:4 | C6:8 G5:4 | A5:4 G5:2 F5:2 A5:4 | C6:8 A5:4 |
B5:4 A5:2 G5:2 D5:4 | G5:8 F5:4 | E5:4 G5:4 C6:4 | G5:8 .:4 |
A5:4 C6:2 B5:2 A5:4 | F5:4 A5:2 G5:2 F5:4 | D5:4 G5:4 B5:4 | G5:8 .:4 |"""
SKETCH_FLUTE = """A4:8 C5:4 | F5:8 E5:4 | E5:4 D5:4 C5:4 | G4:12 |
F5:8 E5:4 | D5:4 B4:4 G4:4 | E5:8 G5:4 | A5:8 E5:4 |
F5:4 E5:4 D5:4 | D5:4 C5:4 B4:4 | C5:12 | B4:4 D5:4 .:4 |"""


def sketch_bed():
    g, ch = SKETCH_GRID, SKETCH_CHORDS
    ev = held(g, SKETCH_PROG, lambda c: ch[c]["pad"], pad_strings, 0.32, 6)
    ev += ost(g, SKETCH_PROG, lambda c: [ch[c]["root"]], "0...........", pizz, 4, 0.56)
    for k in xrange(2):
        ev += ost(g, SKETCH_PROG, lambda c, k=k: [ch[c]["low"][k]], "....0...0...", pizz, 3,
                  0.42 - 0.04 * k)
    return ev


def sketch_pulse():
    g, ch = SKETCH_GRID, SKETCH_CHORDS
    ev = ost(g, SKETCH_PROG, lambda c: ch[c]["pad"], "0.1.2.1.2.1.", harp, 4, 0.3)
    for bar in xrange(len(SKETCH_PROG)):
        ev += hits(g, bar, "X...........", frame_drum, "G2", 0.62, 4)
        if bar in (SKETCH_B - 1, len(SKETCH_PROG) - 1):
            ev += hits(g, bar, "....x.xxxxXX", brush, None, 0.44, 1)
        else:
            ev += hits(g, bar, "....x...x...", brush, None, 0.46, 3)
            ev += hits(g, bar, "..o...o...o.", tek, "C5", 0.5, 1)
    return ev


def sketch_lead():
    g, ch = SKETCH_GRID, SKETCH_CHORDS
    ev = line(g, 0, SKETCH_BOX, music_box, 0.72)
    ev += line(g, SKETCH_B, SKETCH_FLUTE, flute, 0.58, legato=0.5)
    ev += ost(g, SKETCH_PROG[SKETCH_B:], lambda c: ch[c]["arp"], "0...1...2...", music_box, 4,
              0.3, first=SKETCH_B)
    return ev


SKETCH = Piece(
    "sketchbook", 168.0, 3, 24,
    [("bed", sketch_bed, 0.24), ("pulse", sketch_pulse, 0.14), ("lead", sketch_lead, 0.22)],
    tail_beats=6, scale=SEVEN,
    describe="The Sketchbook - C major, 3/4 at 168, A/B of 12 bars")


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


# The menu, then the chapters in StageCatalog order (index 0..8), then the shared boss loop.
CHAPTERS = [FOREST, DUNGEON, CAVE, SWAMP, RUINS, FROZEN, SANDS, EMBER, SKETCH]
PIECES = [MENU] + CHAPTERS + [BOSS]

# The file is the mix: each stem's RMS in dBFS, as the game plays it at unity on the Music bus
# with the slider at full. Set by tools/audio/mixsim.py --music, which measures every cast
# against the music under it. The forest's first render sat 10 dB under the effects (bed -30,
# full set -26) and measured a median cast only +5.6 dB over the late-game music, the worst
# tenth +0.3; the music was costing a cast 2.4 dB of its headroom over the swarm. Three dB down
# from there is the setting, and the action-packed rewrite (2026-10-22) kept it: the extra energy
# is rhythm, not level. The boss loop sits level with a full chapter set, so the swap at the boss
# is a change of music and not a jump in loudness.
_CHAPTER_RMS = {"bed": -33.0, "pulse": -34.0, "lead": -35.0}
STEM_RMS_DB = {("menu", "theme"): -32.0, ("boss", "loop"): -29.5}
for _p in CHAPTERS:
    for _stem in _CHAPTER_RMS:
        STEM_RMS_DB[(_p.name, _stem)] = _CHAPTER_RMS[_stem]
PEAK_CEILING_DB = -3.0


def file_name(piece, stem):
    return "%s_%s.wav" % (piece.name, stem) if piece.name != "boss" else "boss_loop.wav"


# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------

# E2, 82 Hz: the lowest note anything may be written on. The master highpass is at 80, and a
# note under it would be filtered to a ghost of itself while still costing the sub band.
LOWEST_NOTE = midi("E2")


def check_scale(piece, stem, events):
    """A wrong note in a generated score is invisible until someone hears it, and nobody here
    can. Every pitch is checked against the piece's declared scale, and against the floor of the
    frequency budget, before anything renders."""
    if piece.scale is None:
        return
    names = set([name for _b, _i, name, _d, _v in events if name])
    bad = sorted([n for n in names if midi(n) % 12 not in piece.scale])
    if bad:
        raise SystemExit("%s/%s: %s outside the piece's scale" % (piece.name, stem, ", ".join(bad)))
    low = sorted([n for n in names if midi(n) < LOWEST_NOTE])
    if low:
        raise SystemExit("%s/%s: %s under E2, in the sub band" % (piece.name, stem, ", ".join(low)))


def build_piece(piece, out_dir, balance=False):
    print("%s, %.2f s" % (piece.describe, piece.seconds()))
    rendered = []
    for stem, fn, reverb_mix in piece.stems:
        events = fn()
        check_scale(piece, stem, events)
        parts = {} if balance else None
        raw = render(piece, events, parts)
        buf = master(raw, reverb_mix, piece.room)
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
        print("  %-26s %6.2f s  rms %6.1f  peak %6.1f dBFS  %5.2f MB  %d events"
              % (os.path.basename(path), len(buf) / S.SR, S.db(S.rms(buf)), pk,
                 os.path.getsize(path) / 1048576.0, len(events)))
        if balance:
            whole = S.db(S.rms(raw))
            for name in sorted(parts, key=lambda k: -S.rms(parts[k])):
                print("      %-12s %+6.1f dB of the stem, before the room"
                      % (name, S.db(S.rms(parts[name][:len(raw)])) - whole))
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


def is_reel(filename):
    """The audition reels --journey writes next to the scratch renders. Never measured as music."""
    return filename.endswith("_journey.wav") or filename.endswith("_sampler.wav")


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
               breath once a cycle. The prototype's boss bed measured its tail 7.6 dB under its
               body before its chords were made to overlap the seam. A lead with a written rest
               there reads negative and is meant to.
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
    print("%-26s %6s %6s %7s %7s %6s  %6s %6s %7s"
          % ("file", "secs", "MB", "rms", "peak", "crest", "step", "click", "dip"))
    print("-" * 88)
    total = 0
    samples = 0
    worst = 0.0
    for path in paths:
        buf, sr = read_wav(path)
        size = os.path.getsize(path)
        total += size
        samples += len(buf)
        step, click, dip = seam(buf, sr)
        worst = max(worst, step, click)
        pk, rm = S.db(S.peak(buf)), S.db(S.rms(buf))
        print("%-26s %6.2f %6.2f %7.1f %7.1f %6.1f  %6.2f %6.2f %+6.1fd"
              % (os.path.basename(path), len(buf) / float(sr), size / 1048576.0, rm, pk, pk - rm,
                 step, click, dip))
    print("-" * 88)
    # QOA (Godot's compress/mode=2) packs 20 samples into a 64-bit slice plus an 8-byte frame
    # header per 5120 samples and a 16-byte LMS state per channel: 3.2 bits a sample, near enough.
    print("%d files, %.2f MB in git (%.1f decimal MB). QOA (compress/mode=2) carries ~3.2 bits a "
          "sample, so the export holds about %.2f MB." % (len(paths), total / 1048576.0,
                                                          total / 1e6, samples * 3.2 / 8.0 / 1048576.0))
    if worst >= 1.0:
        print("WARNING: a seam measures at or above the loop's own body - listen to that wrap")
    return 0


# ---------------------------------------------------------------------------
# audition reels
# ---------------------------------------------------------------------------
# Nobody working on this can hear a render, so the reels are for whoever can. Each mirrors what
# MusicPlayer does: equal-power fades, a layer in over 4 s, the boss crossfaded in over 3, the
# end faded over 2.5.

LAYER_FADE = 4.0
BOSS_FADE = 3.0
END_FADE = 2.5


def _fader(i, sr, on_at, fade, off_at=None, off_fade=None):
    """The equal-power fader MusicPlayer uses, as a gain at sample i."""
    g = 0.0 if i < on_at else min(1.0, (i - on_at) / float(max(1, int(fade * sr))))
    if off_at is not None and i >= off_at:
        g *= max(0.0, 1.0 - (i - off_at) / float(max(1, int(off_fade * sr))))
    return math.sin(g * math.pi * 0.5)


def _load(src_dir, name):
    for d in (src_dir, OUT):
        path = os.path.join(d, name)
        if os.path.isfile(path):
            return read_wav(path)[0]
    return None


def chapter_journey(piece, src_dir, out_dir, boss):
    """bed alone for one loop, + pulse for one, + lead for a loop and a half, then the boss."""
    bed, pulse, lead_s = [_load(src_dir, file_name(piece, s)) for s in ("bed", "pulse", "lead")]
    if bed is None or pulse is None or lead_s is None:
        return None
    sr = MUSIC_SR
    n = len(bed)
    boss_at = int(n * 3.5)
    end_at = boss_at + int(14.0 * sr)
    total = end_at + int(END_FADE * sr)
    out = [0.0] * total
    for i in xrange(total):
        j = i % n
        v = (bed[j] + pulse[j] * _fader(i, sr, n, LAYER_FADE)
             + lead_s[j] * _fader(i, sr, 2 * n, LAYER_FADE))
        v *= _fader(i, sr, 0, 0.0, boss_at, BOSS_FADE)
        if boss is not None and i >= boss_at:
            v += boss[(i - boss_at) % len(boss)] * _fader(i, sr, boss_at, BOSS_FADE, end_at, END_FADE)
        out[i] = v
    path = os.path.join(out_dir, "%s_journey.wav" % piece.name)
    S.write_wav(path, out, sr=sr)
    return path, n / float(sr), boss_at / float(sr), total / float(sr)


def menu_journey(src_dir, out_dir):
    """The title twice through, then a run starting: the menu out and the forest's bed in, over
    the 3 s MusicPlayer uses."""
    theme = _load(src_dir, file_name(MENU, "theme"))
    bed = _load(src_dir, file_name(FOREST, "bed"))
    if theme is None:
        return None
    sr = MUSIC_SR
    run_at = 2 * len(theme)
    total = run_at + int(12.0 * sr)
    out = [0.0] * total
    for i in xrange(total):
        v = theme[i % len(theme)] * _fader(i, sr, 0, 0.0, run_at, BOSS_FADE)
        if bed is not None and i >= run_at:
            v += bed[(i - run_at) % len(bed)] * _fader(i, sr, run_at, BOSS_FADE)
        out[i] = v
    path = os.path.join(out_dir, "menu_journey.wav")
    S.write_wav(path, out, sr=sr)
    return path, len(theme) / float(sr), run_at / float(sr), total / float(sr)


def sampler(src_dir, out_dir, seconds=14.0, fade=2.0):
    """The menu, then every chapter with all three stems up, 14 s each, crossfaded: the whole
    set side by side, for comparing one chapter against the next."""
    sr = MUSIC_SR
    clips = []
    theme = _load(src_dir, file_name(MENU, "theme"))
    if theme is not None:
        clips.append(theme)
    for piece in CHAPTERS:
        stems = [_load(src_dir, file_name(piece, s)) for s in ("bed", "pulse", "lead")]
        if None in stems:
            continue
        clips.append([stems[0][i] + stems[1][i] + stems[2][i] for i in xrange(len(stems[0]))])
    seg = int(seconds * sr)
    nf = int(fade * sr)
    total = seg * len(clips) + nf
    out = [0.0] * total
    for k, clip in enumerate(clips):
        start = k * seg
        for i in xrange(seg + nf):
            g_in = 1.0 if k == 0 else math.sin(min(1.0, i / float(nf)) * math.pi * 0.5)
            g_out = 1.0 if i < seg else math.cos(min(1.0, (i - seg) / float(nf)) * math.pi * 0.5)
            out[start + i] += clip[i % len(clip)] * g_in * g_out
    path = os.path.join(out_dir, "all_chapters_sampler.wav")
    S.write_wav(path, out, sr=sr)
    return path, len(clips), total / float(sr)


def journeys(src_dir, out_dir):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    boss = _load(src_dir, file_name(BOSS, "loop"))
    r = menu_journey(src_dir, out_dir)
    if r:
        print("wrote %s - the theme twice (%.1f s a loop), a run starting at %.0f s; %.0f s"
              % (os.path.relpath(r[0], ROOT), r[1], r[2], r[3]))
    for piece in CHAPTERS:
        r = chapter_journey(piece, src_dir, out_dir, boss)
        if r:
            print("wrote %s - bed 0:00, +pulse %.0f s, +lead %.0f s, boss %.0f s; %.0f s"
                  % (os.path.relpath(r[0], ROOT), r[1], 2 * r[1], r[2], r[3]))
    r = sampler(src_dir, out_dir)
    print("wrote %s - %d pieces, 14 s each; %.0f s" % (os.path.relpath(r[0], ROOT), r[1], r[2]))


def main(argv):
    S.SR = MUSIC_SR
    names = [a for a in argv if not a.startswith("--")]
    out_dir = SCRATCH if "--scratch" in argv else OUT
    if "--report" in argv:
        files = sorted([os.path.join(out_dir, f) for f in os.listdir(out_dir)
                        if f.endswith(".wav") and not is_reel(f)
                        and (not names or any([n in f for n in names]))])
        return report(files)
    if "--journey" in argv:
        journeys(out_dir, SCRATCH)
        return 0
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    for piece in PIECES:
        if names and not any([n in piece.name for n in names]):
            continue
        build_piece(piece, out_dir, balance="--balance" in argv)
    print("%d unique note renders cached" % len(_CACHE))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
