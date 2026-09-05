# -*- coding: utf-8 -*-
"""Mono synthesis primitives for the Wizard Survivors SFX set.

Python 2.7, standard library only - this machine has no numpy, no ffmpeg and no audio
encoder, so everything here is written against lists of floats and the stdlib wave module.
That constraint is the reason the set is synthesised rather than recorded, and it is not a
temporary state of affairs: see .ai/decisions/2026-09-05-procedural-sfx.md.

Three rules the rest of the pipeline depends on:

  * Everything is MONO. Godot's AudioStreamPlayer2D silently refuses to pan a stereo stream,
    and every gameplay sound in this project is positional. Stereo would cost us the one
    spatial cue a top-down swarm game actually has.
  * Everything is float in [-1, 1] until write_wav, which is the only place quantisation
    happens. Intermediate clipping is the caller's business; write_wav will hard-limit and
    say so rather than wrapping.
  * Every buffer written to disk gets a short fade at both ends. A waveform that starts or
    ends off zero is a click, and a click at 60 sounds/second in a swarm is a buzz.

Randomness is drawn from seeded random.Random instances, never the module-level RNG, so
re-running the build produces byte-identical files. Do not introduce time- or
address-dependent randomness anywhere in this package.
"""
from __future__ import division

import array
import math
import os
import random
import struct
import wave

SR = 44100
TWO_PI = 2.0 * math.pi

# Chamberlin/biquad coefficients are recomputed once per block rather than per sample.
# 64 samples is 1.45 ms - fast enough that a filter sweep sounds continuous, cheap enough
# that a 1 s sound does not spend a second in math.sin.
FILTER_BLOCK = 64


# ---------------------------------------------------------------------------
# lengths and control signals
# ---------------------------------------------------------------------------

def n_samples(dur):
    return int(round(dur * SR))


def silence(dur):
    return [0.0] * n_samples(dur)


def resample(seq, n):
    """Stretch or squash a control curve to exactly n points, linearly."""
    m = len(seq)
    if m == n:
        return list(seq)
    if m == 0:
        return [0.0] * n
    if m == 1:
        return [float(seq[0])] * n
    out = [0.0] * n
    for i in xrange(n):
        x = (i / (n - 1)) * (m - 1) if n > 1 else 0.0
        j = int(x)
        if j >= m - 1:
            out[i] = float(seq[m - 1])
        else:
            f = x - j
            out[i] = float(seq[j]) * (1.0 - f) + float(seq[j + 1]) * f
    return out


def _ctl(x, n):
    """Accept a scalar or a curve of any length; return a curve of length n."""
    if isinstance(x, (list, tuple)):
        return resample(x, n)
    return [float(x)] * n


def ramp(n, a, b, curve=1.0):
    """a -> b over n samples. curve > 1 lingers near a, curve < 1 leaves it fast."""
    if n <= 0:
        return []
    if n == 1:
        return [float(b)]
    out = [0.0] * n
    for i in xrange(n):
        x = i / (n - 1)
        out[i] = a + (b - a) * (x ** curve)
    return out


def decay(n, curve=3.0, start=1.0, end=0.0):
    """Percussive fall: drops hard immediately, then trails. curve is the hardness."""
    if n <= 0:
        return []
    if n == 1:
        return [float(end)]
    out = [0.0] * n
    span = start - end
    for i in xrange(n):
        x = i / (n - 1)
        out[i] = end + span * ((1.0 - x) ** curve)
    return out


def expdecay(n, k=6.0):
    """exp(-k*x) over n samples, ending exactly at zero so it never clicks."""
    if n <= 0:
        return []
    if n == 1:
        return [0.0]
    out = [0.0] * n
    base = math.exp(-k)
    scale = 1.0 / (1.0 - base) if base < 1.0 else 1.0
    for i in xrange(n):
        x = i / (n - 1)
        out[i] = (math.exp(-k * x) - base) * scale
    return out


def ad(dur, attack=0.002, curve=3.0, hold=0.0):
    """The workhorse one-shot envelope: linear attack, optional hold, percussive decay."""
    n = n_samples(dur)
    na = min(n, n_samples(attack))
    nh = min(n - na, n_samples(hold))
    nd = n - na - nh
    return ramp(na, 0.0, 1.0) + [1.0] * nh + decay(nd, curve)


def adsr(dur, a=0.01, d=0.08, s=0.5, r=0.2):
    n = n_samples(dur)
    na, nd, nr = n_samples(a), n_samples(d), n_samples(r)
    ns = max(0, n - na - nd - nr)
    if na + nd + nr > n:  # squeeze rather than overrun
        scale = n / float(max(1, na + nd + nr))
        na, nd, nr = int(na * scale), int(nd * scale), int(nr * scale)
        ns = max(0, n - na - nd - nr)
    env = ramp(na, 0.0, 1.0) + ramp(nd, 1.0, s, 2.0) + [s] * ns + decay(nr, 2.0, s, 0.0)
    return resample(env, n) if len(env) != n else env


def sweep(dur, a, b, curve=1.0):
    """A pitch (or cutoff) curve of the right length for a buffer of dur seconds."""
    return ramp(n_samples(dur), a, b, curve)


# ---------------------------------------------------------------------------
# oscillators
# ---------------------------------------------------------------------------

def osc(shape, freq, dur, duty=0.5, phase=0.0, seed=1):
    """One oscillator. freq may be a scalar or a per-sample curve.

    Shapes: sine, tri, saw, square, pulse (uses duty), noise, mnoise (metallic noise -
    white noise held at freq, so it has a pitch without a harmonic series).
    """
    n = n_samples(dur)
    out = [0.0] * n
    if shape == "noise":
        rng = random.Random(seed)
        for i in xrange(n):
            out[i] = rng.uniform(-1.0, 1.0)
        return out
    f = _ctl(freq, n)
    if shape == "mnoise":
        rng = random.Random(seed)
        ph = phase
        held = rng.uniform(-1.0, 1.0)
        for i in xrange(n):
            ph += f[i] / SR
            if ph >= 1.0:
                ph -= math.floor(ph)
                held = rng.uniform(-1.0, 1.0)
            out[i] = held
        return out
    ph = phase
    for i in xrange(n):
        ph += f[i] / SR
        if ph >= 1.0 or ph < 0.0:
            ph -= math.floor(ph)
        if shape == "sine":
            out[i] = math.sin(TWO_PI * ph)
        elif shape == "tri":
            out[i] = 1.0 - 4.0 * abs(ph - 0.5)
        elif shape == "saw":
            out[i] = 2.0 * ph - 1.0
        elif shape == "square":
            out[i] = 1.0 if ph < 0.5 else -1.0
        elif shape == "pulse":
            out[i] = 1.0 if ph < duty else -1.0
        else:
            raise ValueError("unknown shape: %s" % shape)
    return out


def fm(carrier_shape, base, mod_freq, index, dur, mod_shape="sine", seed=1):
    """Frequency modulation as a frequency curve, so it composes with the filters."""
    n = n_samples(dur)
    m = osc(mod_shape, mod_freq, dur, seed=seed)
    b = _ctl(base, n)
    idx = _ctl(index, n)
    f = [max(1.0, b[i] * (1.0 + idx[i] * m[i])) for i in xrange(n)]
    return osc(carrier_shape, f, dur, seed=seed)


def partials(base, ratios, dur, amps=None, shape="sine", decays=None):
    """An inharmonic or harmonic stack. Ratios that are not integers ring like metal."""
    n = n_samples(dur)
    out = [0.0] * n
    if amps is None:
        amps = [1.0 / (i + 1) for i in xrange(len(ratios))]
    for k, r in enumerate(ratios):
        v = osc(shape, [b * r for b in _ctl(base, n)], dur, seed=k + 1)
        env = expdecay(n, decays[k] if decays else 5.0)
        a = amps[k]
        for i in xrange(n):
            out[i] += v[i] * env[i] * a
    return out


# ---------------------------------------------------------------------------
# filters
# ---------------------------------------------------------------------------

def _biquad_coeffs(kind, f0, q):
    f0 = max(20.0, min(f0, SR * 0.47))
    w0 = TWO_PI * f0 / SR
    c, s = math.cos(w0), math.sin(w0)
    alpha = s / (2.0 * max(0.05, q))
    if kind == "lp":
        b0, b1, b2 = (1.0 - c) / 2.0, 1.0 - c, (1.0 - c) / 2.0
    elif kind == "hp":
        b0, b1, b2 = (1.0 + c) / 2.0, -(1.0 + c), (1.0 + c) / 2.0
    elif kind == "bp":
        b0, b1, b2 = alpha, 0.0, -alpha
    elif kind == "notch":
        b0, b1, b2 = 1.0, -2.0 * c, 1.0
    else:
        raise ValueError("unknown filter kind: %s" % kind)
    a0, a1, a2 = 1.0 + alpha, -2.0 * c, 1.0 - alpha
    return b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0


def biquad(buf, kind, cutoff, q=0.707):
    """RBJ biquad, stable to Nyquist, coefficients refreshed every FILTER_BLOCK samples."""
    n = len(buf)
    if n == 0:
        return []
    c = _ctl(cutoff, n)
    qq = _ctl(q, n)
    out = [0.0] * n
    x1 = x2 = y1 = y2 = 0.0
    b0 = b1 = b2 = a1 = a2 = 0.0
    for i in xrange(n):
        if i % FILTER_BLOCK == 0:
            b0, b1, b2, a1, a2 = _biquad_coeffs(kind, c[i], qq[i])
        x0 = buf[i]
        y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x0
        y2, y1 = y1, y0
        out[i] = y0
    return out


def lowpass(buf, cutoff, q=0.707):
    return biquad(buf, "lp", cutoff, q)


def highpass(buf, cutoff, q=0.707):
    return biquad(buf, "hp", cutoff, q)


def bandpass(buf, cutoff, q=4.0):
    return biquad(buf, "bp", cutoff, q)


# ---------------------------------------------------------------------------
# shaping and texture
# ---------------------------------------------------------------------------

def drive(buf, amount=3.0):
    """Soft saturation. Adds odd harmonics and, more usefully, compresses the transient."""
    a = max(0.001, amount)
    k = math.tanh(a)
    return [math.tanh(a * v) / k for v in buf]


def fold(buf, amount=1.0):
    """Wavefolding - harsher and more inharmonic than drive. Use sparingly, it is loud."""
    out = []
    for v in buf:
        x = v * amount
        while x > 1.0 or x < -1.0:
            if x > 1.0:
                x = 2.0 - x
            if x < -1.0:
                x = -2.0 - x
        out.append(x)
    return out


def bitcrush(buf, bits=6):
    levels = float(2 ** max(1, bits) - 1)
    return [round(v * levels) / levels for v in buf]


def decimate(buf, factor):
    """Sample-and-hold downsampling. The single most retro-sounding thing in this file."""
    f = max(1, int(factor))
    if f == 1:
        return list(buf)
    out = [0.0] * len(buf)
    held = 0.0
    for i in xrange(len(buf)):
        if i % f == 0:
            held = buf[i]
        out[i] = held
    return out


def ringmod(buf, freq, mix=1.0, shape="sine"):
    n = len(buf)
    m = osc(shape, freq, n / SR)
    m = resample(m, n)
    return [buf[i] * (m[i] * mix + (1.0 - mix)) for i in xrange(n)]


def tremolo(buf, rate, depth=0.5, shape="sine"):
    n = len(buf)
    m = resample(osc(shape, rate, n / SR), n)
    return [buf[i] * (1.0 - depth + depth * 0.5 * (m[i] + 1.0)) for i in xrange(n)]


def gate(buf, rate, duty=0.5, seed=None):
    """Chops the buffer. With a seed, chops it irregularly - that is crackle, not rhythm."""
    n = len(buf)
    period = max(1, int(SR / max(1.0, rate)))
    rng = random.Random(seed) if seed is not None else None
    out = [0.0] * n
    open_now = True
    for i in xrange(n):
        if i % period == 0:
            open_now = (rng.random() < duty) if rng else ((i // period) % 2 == 0)
        out[i] = buf[i] if open_now else 0.0
    return out


# ---------------------------------------------------------------------------
# space
# ---------------------------------------------------------------------------

def delay(buf, time, feedback=0.3, mix=0.3, taps=6):
    d = max(1, n_samples(time))
    n = len(buf)
    out = list(buf)
    for t in xrange(1, taps + 1):
        g = mix * (feedback ** (t - 1))
        if g < 0.001:
            break
        off = d * t
        if off >= n:
            break
        for i in xrange(off, n):
            out[i] += buf[i - off] * g
    return out


_COMB = [1116, 1188, 1277, 1356, 1422, 1491]
_ALLPASS = [556, 441, 341, 225]


def reverb(buf, size=0.7, damp=0.4, mix=0.25):
    """Schroeder reverb. Small, cheap, and only ever used to give a tail somewhere to go."""
    n = len(buf)
    if n == 0:
        return []
    wet = [0.0] * n
    fb = 0.28 + 0.6 * max(0.0, min(1.0, size))
    for length in _COMB:
        buffer = [0.0] * length
        idx = 0
        store = 0.0
        for i in xrange(n):
            y = buffer[idx]
            store = y * (1.0 - damp) + store * damp
            buffer[idx] = buf[i] + store * fb
            idx += 1
            if idx >= length:
                idx = 0
            wet[i] += y
    for i in xrange(n):
        wet[i] /= len(_COMB)
    for length in _ALLPASS:
        buffer = [0.0] * length
        idx = 0
        for i in xrange(n):
            y = buffer[idx]
            v = wet[i] + y * 0.5
            buffer[idx] = v
            wet[i] = y - wet[i]
            idx += 1
            if idx >= length:
                idx = 0
    return [buf[i] * (1.0 - mix) + wet[i] * mix for i in xrange(n)]


# ---------------------------------------------------------------------------
# combination
# ---------------------------------------------------------------------------

def gain(buf, g):
    if isinstance(g, (list, tuple)):
        c = _ctl(g, len(buf))
        return [buf[i] * c[i] for i in xrange(len(buf))]
    return [v * g for v in buf]


def env_mul(buf, env):
    e = _ctl(env, len(buf))
    return [buf[i] * e[i] for i in xrange(len(buf))]


def mix(*bufs):
    n = max([len(b) for b in bufs]) if bufs else 0
    out = [0.0] * n
    for b in bufs:
        for i in xrange(len(b)):
            out[i] += b[i]
    return out


def layer(items):
    """[(buf, offset_seconds, gain), ...] -> one buffer long enough to hold them all."""
    end = 0
    for buf, off, _g in items:
        end = max(end, n_samples(off) + len(buf))
    out = [0.0] * end
    for buf, off, g in items:
        o = n_samples(off)
        for i in xrange(len(buf)):
            out[o + i] += buf[i] * g
    return out


def concat(*bufs):
    out = []
    for b in bufs:
        out.extend(b)
    return out


def pad(buf, dur):
    """Extend (never truncate) to at least dur seconds, so a tail has room to ring."""
    n = n_samples(dur)
    return list(buf) + [0.0] * max(0, n - len(buf))


def reverse(buf):
    return list(reversed(buf))


def fade(buf, fin=0.002, fout=0.004):
    n = len(buf)
    if n == 0:
        return []
    out = list(buf)
    ni = min(n, n_samples(fin))
    no = min(n - ni, n_samples(fout))
    for i in xrange(ni):
        out[i] *= i / float(max(1, ni))
    for i in xrange(no):
        out[n - 1 - i] *= i / float(max(1, no))
    return out


def dc_block(buf):
    if not buf:
        return []
    m = sum(buf) / len(buf)
    return [v - m for v in buf]


def peak(buf):
    return max([abs(v) for v in buf]) if buf else 0.0


def rms(buf):
    if not buf:
        return 0.0
    return math.sqrt(sum([v * v for v in buf]) / len(buf))


def db(x):
    return -99.0 if x <= 1e-9 else 20.0 * math.log10(x)


def normalize(buf, target=0.9):
    p = peak(buf)
    if p <= 1e-9:
        return list(buf)
    return [v * (target / p) for v in buf]


# ---------------------------------------------------------------------------
# output
# ---------------------------------------------------------------------------

def write_wav(path, buf, sr=SR):
    """16-bit mono PCM. Returns (peak_dbfs, rms_dbfs, seconds, clipped_samples)."""
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    clipped = 0
    data = array.array("h")
    for v in buf:
        if v > 1.0:
            v = 1.0
            clipped += 1
        elif v < -1.0:
            v = -1.0
            clipped += 1
        data.append(int(round(v * 32767.0)))
    w = wave.open(path, "wb")
    try:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(data.tostring())
    finally:
        w.close()
    return db(peak(buf)), db(rms(buf)), len(buf) / float(sr), clipped
