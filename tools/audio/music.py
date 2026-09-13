# -*- coding: utf-8 -*-
"""Procedural music on top of synth.py - a sequencer, a few instruments, and one boss loop.

    python tools/audio/music.py <output directory>

This is a PROTOTYPE, deliberately rendered outside assets/ until someone has heard it. Read
the size warning in .ai/audio-direction.md before committing anything this writes: a rendered
minute is 5.3 MB at 44100 Hz mono, LFS is deliberately off in this repo (see .gitattributes),
and unlike a sprite a music file gets re-rendered on every tuning pass. Iterate in a scratch
directory and commit only a settled track.

Three things here are worth keeping even if the music itself is thrown away:

  * STEMS, not a mix. The loop renders as three independent layers of constant density -
    bed, pulse and lead - which a game can crossfade by intensity. They share a tempo and a
    key by construction, which is the one thing procedural music is genuinely better at than
    a licensed track. Nothing in the arrangement builds or drops, because a layer that has
    its own arc cannot be muxed against the others.
  * WRAPPED TAILS. render() deliberately runs past the end of the loop and folds the overhang
    back onto the beginning, so a reverb tail or a held pad crosses the loop seam instead of
    being cut at it. That is the only way a loop this long does not tick once a bar.
  * A NOTE CACHE. Music repeats notes; synthesis is expensive. Caching by
    (instrument, frequency, duration) turns ~450 note events into about 30 renders.

The key is A natural minor, which is not an arbitrary choice: the stingers in sfx.py are
already in it (level_up is A-C-E-A, game_over descends through it), so the score and the
interface agree. Nothing here can be matched to the two existing .mp3 tracks - there is no
MP3 decoder on this machine, so their key and tempo are unmeasurable.
"""
from __future__ import division

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import synth as S

BPM = 138.0
BEAT = 60.0 / BPM
BEATS_PER_BAR = 4
BARS = 24
TOTAL_BEATS = BARS * BEATS_PER_BAR          # 96 beats, about 41.7 s
TAIL_BEATS = 8                              # rendered past the end, then wrapped

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
# instruments
# ---------------------------------------------------------------------------

def pad(freq, dur):
    """Three detuned saws opening slowly through a lowpass. The whole harmonic bed."""
    a = S.osc("saw", freq, dur)
    b = S.osc("saw", freq * 1.006, dur, phase=0.37)
    c = S.osc("saw", freq * 0.994, dur, phase=0.71)
    v = S.mix(S.gain(a, 0.40), S.gain(b, 0.32), S.gain(c, 0.32))
    v = S.lowpass(v, S.sweep(dur, 320, 1500, 1.0), 1.3)
    return S.env_mul(v, S.adsr(dur, a=0.50, d=0.5, s=0.78, r=0.9))


def bass(freq, dur):
    """Square sub with a fast filter envelope and enough drive to survive a phone speaker."""
    v = S.mix(S.gain(S.osc("square", freq, dur), 0.6), S.gain(S.osc("sine", freq, dur), 0.7))
    v = S.lowpass(v, S.sweep(dur, 1400, 300, 2.2), 1.6)
    return S.drive(S.env_mul(v, S.ad(dur, 0.006, 2.2)), 2.4)


def pluck(freq, dur):
    """The arpeggio voice. Short, filtered, and dry - it is the rhythm, not the harmony."""
    v = S.osc("saw", freq, dur)
    v = S.lowpass(v, S.sweep(dur, 2600, 600, 2.6), 2.4)
    return S.env_mul(v, S.ad(dur, 0.004, 3.2))


def lead(freq, dur):
    """A slow triangle-and-saw voice with a little vibrato. The only thing that sings."""
    f = S.fm("tri", freq, 5.2, 0.006, dur)
    v = S.mix(S.gain(f, 0.7), S.gain(S.osc("saw", freq * 1.004, dur), 0.25))
    v = S.lowpass(v, S.sweep(dur, 900, 2400, 0.8), 1.4)
    return S.env_mul(v, S.adsr(dur, a=0.09, d=0.2, s=0.7, r=min(0.5, dur * 0.4)))


def kick(freq, dur):
    n = S.n_samples(dur)
    body = S.osc("sine", S.sweep(dur, 128, 42, 0.3), dur)
    return S.drive(S.env_mul(body, S.expdecay(n, 7.0)), 2.0)


def clank(freq, dur):
    """Backbeat. Filtered noise plus an inharmonic ring - the metal element, used as a snare."""
    n = S.n_samples(dur)
    air = S.bandpass(S.osc("noise", 0, dur, seed=311), S.sweep(dur, 2600, 1300, 0.8), 1.6)
    ring = S.partials(430.0, [1.0, 1.71, 2.46, 3.39], dur,
                      amps=[1.0, 0.6, 0.4, 0.24], decays=[6.0, 8.0, 10.0, 13.0])
    return S.mix(S.env_mul(air, S.decay(n, 4.0)), S.gain(ring, 0.45))


def hat(freq, dur):
    n = S.n_samples(dur)
    return S.env_mul(S.highpass(S.osc("noise", 0, dur, seed=313), 6500), S.decay(n, 12.0))


# ---------------------------------------------------------------------------
# sequencer
# ---------------------------------------------------------------------------

_CACHE = {}


def _voice(instrument, freq, dur):
    """Render one note, or return the one already rendered. Velocity is applied by the caller
    so it never fragments the key - a note at two dynamics is one synthesis, not two."""
    key = (instrument.__name__, round(freq, 3), round(dur, 4))
    if key not in _CACHE:
        _CACHE[key] = instrument(freq, dur)
    return _CACHE[key]


def render(events, total_beats=TOTAL_BEATS, tail_beats=TAIL_BEATS):
    """events: (beat, instrument, note name or None, duration in beats, velocity).

    Renders total_beats + tail_beats, then folds the overhang back onto the head so anything
    still ringing at the loop point continues across the seam instead of being truncated.
    """
    loop_n = S.n_samples(total_beats * BEAT)
    full_n = S.n_samples((total_beats + tail_beats) * BEAT)
    buf = [0.0] * full_n
    for beat, instrument, name, dur_beats, vel in events:
        freq = note(name) if name else 0.0
        v = _voice(instrument, freq, dur_beats * BEAT)
        off = S.n_samples(beat * BEAT)
        for i in xrange(len(v)):
            j = off + i
            if j < full_n:
                buf[j] += v[i] * vel
    out = buf[:loop_n]
    for i in xrange(loop_n, full_n):
        out[(i - loop_n) % loop_n] += buf[i]
    return out


# ---------------------------------------------------------------------------
# the boss loop
# ---------------------------------------------------------------------------
# Twelve chord slots of eight beats each. A minor with a Neapolitan Bb and a harmonic-minor
# E: the bII and the raised leading note are where the menace lives, and the second half
# moves twice as fast harmonically so a 42-second loop does not sit still.

SLOTS = [
    ("A",  "A2",  ["A3", "C4", "E4"]),
    ("A",  "A2",  ["A3", "C4", "E4"]),
    ("Bb", "Bb2", ["Bb3", "D4", "F4"]),
    ("Bb", "Bb2", ["Bb3", "D4", "F4"]),
    ("A",  "A2",  ["A3", "C4", "E4"]),
    ("A",  "A2",  ["A3", "C4", "E4"]),
    ("F",  "F2",  ["F3", "A3", "C4"]),
    ("E",  "E2",  ["E3", "G#3", "B3"]),
    ("A",  "A2",  ["A3", "C4", "E4"]),
    ("Bb", "Bb2", ["Bb3", "D4", "F4"]),
    ("F",  "F2",  ["F3", "A3", "C4"]),
    ("E",  "E2",  ["E3", "G#3", "B3"]),
]
SLOT_BEATS = 8

# Bar 9 onward, over the chord of the moment. Beats are absolute.
MOTIF = [
    (32, "A4", 2.0), (34, "C5", 1.0), (35, "B4", 1.0), (36, "A4", 3.5),
    (40, "E5", 2.0), (42, "D5", 1.0), (43, "C5", 1.0), (44, "B4", 3.5),
    (48, "A4", 3.0), (51, "C5", 1.0), (52, "F4", 3.5),
    (56, "G#4", 3.5), (60, "B4", 2.0), (62, "E4", 2.0),
    (64, "A4", 2.0), (66, "E4", 1.0), (67, "F4", 1.0), (68, "E4", 3.5),
    (72, "D5", 2.0), (74, "Bb4", 2.0), (76, "F4", 3.5),
    (80, "A4", 2.0), (82, "C5", 2.0), (84, "A4", 3.5),
    (88, "G#4", 4.0), (92, "E4", 4.0),
]


# Each pad chord runs a beat past its slot. That overlap is doing two jobs: it crossfades
# one chord into the next instead of rearticulating on a hard edge, and - because render()
# folds the overhang back onto the head - the LAST chord covers the first chord’s attack
# across the loop seam. Without it the bed measured 7.6 dB down in its final half second
# against the body of the loop, which is not a click but an audible breath every 42 seconds.
PAD_OVERLAP_BEATS = 1.0


def bed_events():
    """Pad only. Constant density - this layer plays whenever the boss is alive."""
    ev = []
    for k, (_name, _root, voicing) in enumerate(SLOTS):
        start = k * SLOT_BEATS
        for v, pitch in enumerate(voicing):
            ev.append((start, pad, pitch, SLOT_BEATS + PAD_OVERLAP_BEATS, 0.5 - 0.06 * v))
    return ev


def pulse_events():
    """Bass eighths and percussion. Fade this in with enemy pressure."""
    ev = []
    for k, (_name, root, _voicing) in enumerate(SLOTS):
        start = k * SLOT_BEATS
        for e in xrange(SLOT_BEATS * 2):                 # eighth notes
            beat = start + e * 0.5
            accent = 1.0 if e % 4 == 0 else (0.72 if e % 2 == 0 else 0.55)
            ev.append((beat, bass, root, 0.46, 0.55 * accent))
    for bar in xrange(BARS):
        b = bar * BEATS_PER_BAR
        ev.append((b + 0.0, kick, "A1", 0.5, 0.95))
        ev.append((b + 2.0, kick, "A1", 0.5, 0.85))
        if bar % 4 == 3:
            ev.append((b + 3.5, kick, "A1", 0.5, 0.7))
        ev.append((b + 1.0, clank, "A4", 0.5, 0.5))
        ev.append((b + 3.0, clank, "A4", 0.5, 0.5))
        for h in xrange(8):                              # eighth-note hats
            ev.append((b + h * 0.5, hat, "A6", 0.1, 0.20 if h % 2 else 0.30))
    return ev


def lead_events():
    """Arpeggio and motif. Fade this in for the boss proper, or for low HP."""
    ev = []
    for k, (_name, _root, voicing) in enumerate(SLOTS):
        start = k * SLOT_BEATS
        ladder = voicing + [voicing[1]]                  # up and part-way back
        for e in xrange(SLOT_BEATS * 2):
            beat = start + e * 0.5
            ev.append((beat, pluck, ladder[e % len(ladder)], 0.45, 0.30))
    for beat, pitch, dur in MOTIF:
        ev.append((beat, lead, pitch, dur, 0.62))
    return ev


STEMS = [
    ("bed", bed_events, 0.30, -14.0),
    ("pulse", pulse_events, 0.10, -9.0),
    ("lead", lead_events, 0.22, -12.0),
]


def build(out_dir):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    rendered = []
    for name, fn, reverb_mix, target_db in STEMS:
        buf = render(fn())
        if reverb_mix > 0.0:
            buf = S.reverb(buf, size=0.85, damp=0.4, mix=reverb_mix)
        buf = S.normalize(S.dc_block(buf), 10.0 ** (target_db / 20.0))
        rendered.append((name, buf))
        print("  stem %-6s %6.2f s  peak %5.1f dBFS" % (name, len(buf) / S.SR, S.db(S.peak(buf))))

    full = S.dc_block(S.mix(*[b for _n, b in rendered]))
    full = S.normalize(full, 10.0 ** (-3.0 / 20.0))
    rendered.append(("full", full))

    for name, buf in rendered:
        # No fades anywhere: every one of these is a loop, and a fade on a loop is the click.
        p44 = os.path.join(out_dir, "boss_%s_44k.wav" % name)
        S.write_wav(p44, buf)
        p22 = os.path.join(out_dir, "boss_%s_22k.wav" % name)
        S.write_wav(p22, S.halve_rate(buf), sr=S.SR // 2)
        print("  wrote %-22s %5.1f MB   %-22s %5.1f MB"
              % (os.path.basename(p44), os.path.getsize(p44) / 1048576.0,
                 os.path.basename(p22), os.path.getsize(p22) / 1048576.0))
    return rendered


def main(argv):
    out_dir = argv[0] if argv else os.path.join(HERE, "_music_preview")
    print("boss loop: %d bars at %.0f BPM = %.2f s, A minor" % (BARS, BPM, TOTAL_BEATS * BEAT))
    build(out_dir)
    print("%d unique note renders cached" % len(_CACHE))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
