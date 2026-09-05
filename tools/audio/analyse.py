# -*- coding: utf-8 -*-
"""Measure the set instead of trusting it.

    python tools/audio/analyse.py            # features for every sound in the registry
    python tools/audio/analyse.py --confuse  # pairwise confusion inside each category
    python tools/audio/analyse.py cast_      # only names containing this

.ai/art-direction.md section 4 does not assert that the enemy silhouettes are distinct, it
measures the overlap and reports 1.00 for the two that are the same sprite. This is the same
audit for sound, and it exists for the same reason: nobody working on this project can hear
the difference between twelve elements by reading the source, and "they sound different to
me" is not a thing a future session can verify.

Two numbers matter:

  * CENTROID and ROLLOFF place a sound on the bright/dark axis. Two elements that share a
    centroid within a few hundred Hz are competing for the same slot in the mix.
  * CONFUSION is the cosine distance between two sounds' profiles, 0.00 = identical and
    1.00 = nothing in common. A profile is 24 log-spaced spectral bands PLUS 12 envelope
    bands, because a spectrum alone cannot see rhythm: on spectrum only, reroll measured as
    a near-twin of ui_click, which it is - five copies of it, spread over 200 ms. Shape over
    time is to sound what silhouette is to a sprite, so the audit has to weigh both.

TWINS below declares the pairs that are SUPPOSED to measure alike - the hurt variants, and
the three parts of the beam. They are reported separately rather than as failures, the same
way the art audit reports Enemy and SlowEnemy scoring 1.00 as a known fact about the roster.

The pure-Python FFT below is slow and that is fine - this is an audit, not a build step.
"""
from __future__ import division

import cmath
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import build as B
import sfx
import synth as S

FRAME = 4096
FRAMES = 4
# Two sounds closer than this in spectral cosine distance will be mistaken for each other
# in a busy screen. Chosen to flag the pairs worth a listen, not to be a hard gate.
CONFUSABLE = 0.10
# How much the envelope half of the profile counts relative to the spectral half.
ENV_WEIGHT = 0.8
ENV_BANDS = 12

# Declared near-twins: pairs that exist to sound alike. Reported, never counted as failures.
TWINS = [
    ("enemy_hurt_a", "enemy_hurt_b"),
    ("enemy_hurt_a", "enemy_hurt_c"),
    ("enemy_hurt_b", "enemy_hurt_c"),
    ("spell_beam_start", "spell_beam_loop"),
    ("spell_beam_start", "spell_beam_end"),
    ("spell_beam_loop", "spell_beam_end"),
]


def is_twin(a, b):
    return (a, b) in TWINS or (b, a) in TWINS


def fft(x):
    """Iterative radix-2 Cooley-Tukey. len(x) must be a power of two."""
    n = len(x)
    if n & (n - 1):
        raise ValueError("fft length must be a power of two, got %d" % n)
    j = 0
    a = list(x)
    for i in xrange(1, n):
        bit = n >> 1
        while j & bit:
            j ^= bit
            bit >>= 1
        j |= bit
        if i < j:
            a[i], a[j] = a[j], a[i]
    length = 2
    while length <= n:
        ang = -2.0 * math.pi / length
        wl = cmath.exp(complex(0.0, ang))
        for i in xrange(0, n, length):
            w = complex(1.0, 0.0)
            half = length >> 1
            for k in xrange(half):
                u = a[i + k]
                v = a[i + k + half] * w
                a[i + k] = u + v
                a[i + k + half] = u - v
                w *= wl
        length <<= 1
    return a


_HANN = [0.5 - 0.5 * math.cos(2.0 * math.pi * i / (FRAME - 1)) for i in xrange(FRAME)]


def spectrum(buf):
    """Average magnitude spectrum over FRAMES windows spread across the sound."""
    n = len(buf)
    mags = [0.0] * (FRAME // 2)
    used = 0
    if n < FRAME:
        buf = list(buf) + [0.0] * (FRAME - n)
        n = FRAME
    step = max(1, (n - FRAME) // max(1, FRAMES - 1)) if n > FRAME else 1
    for f in xrange(FRAMES):
        start = min(f * step, n - FRAME)
        frame = [complex(buf[start + i] * _HANN[i], 0.0) for i in xrange(FRAME)]
        spec = fft(frame)
        for i in xrange(FRAME // 2):
            mags[i] += abs(spec[i])
        used += 1
    return [m / used for m in mags]


def features(buf):
    mags = spectrum(buf)
    total = sum(mags)
    bins = len(mags)
    hz = S.SR / float(FRAME)
    if total <= 1e-12:
        return dict(centroid=0.0, spread=0.0, rolloff=0.0, attack=0.0, crest=0.0, mags=mags)
    centroid = sum([mags[i] * i * hz for i in xrange(bins)]) / total
    spread = math.sqrt(sum([mags[i] * ((i * hz - centroid) ** 2) for i in xrange(bins)]) / total)
    run, rolloff = 0.0, 0.0
    for i in xrange(bins):
        run += mags[i]
        if run >= 0.85 * total:
            rolloff = i * hz
            break
    pk = S.peak(buf)
    attack = 0.0
    for i, v in enumerate(buf):
        if abs(v) >= pk * 0.9:
            attack = i / S.SR
            break
    crest = S.db(pk) - S.db(S.rms(buf))
    return dict(centroid=centroid, spread=spread, rolloff=rolloff,
                attack=attack, crest=crest, mags=mags)


def log_profile(mags, bands=24):
    """Collapse a spectrum to log-spaced bands, energy-normalised - the comparison vector."""
    hz = S.SR / float(FRAME)
    lo, hi = math.log(60.0), math.log(16000.0)
    out = [0.0] * bands
    for i in xrange(1, len(mags)):
        f = i * hz
        if f < 60.0 or f > 16000.0:
            continue
        b = int((math.log(f) - lo) / (hi - lo) * bands)
        out[min(bands - 1, b)] += mags[i]
    norm = math.sqrt(sum([v * v for v in out]))
    return [v / norm for v in out] if norm > 1e-12 else out


def env_profile(buf, bands=ENV_BANDS):
    """Coarse loudness-over-time. This is what makes a five-tick riffle measure differently
    from the single tick it is built out of."""
    n = len(buf)
    if n == 0:
        return [0.0] * bands
    out = []
    for b in xrange(bands):
        lo = (n * b) // bands
        hi = max(lo + 1, (n * (b + 1)) // bands)
        out.append(S.rms(buf[lo:hi]))
    norm = math.sqrt(sum([v * v for v in out]))
    return [v / norm for v in out] if norm > 1e-12 else out


def profile(buf, mags):
    """The comparison vector: spectral shape and temporal shape, jointly normalised."""
    vec = log_profile(mags) + [v * ENV_WEIGHT for v in env_profile(buf)]
    norm = math.sqrt(sum([v * v for v in vec]))
    return [v / norm for v in vec] if norm > 1e-12 else vec


def cosine_distance(a, b):
    return 1.0 - sum([a[i] * b[i] for i in xrange(len(a))])


def main(argv):
    want_confuse = "--confuse" in argv
    filters = [a for a in argv if not a.startswith("--")]
    entries = sfx.REGISTRY
    if filters:
        entries = [e for e in entries if any([f in e[0] for f in filters])]

    rows = []
    for name, category, target_db, fn, loops in entries:
        buf = S.normalize(S.dc_block(fn()), B.db_to_linear(target_db))
        f = features(buf)
        f["name"] = name
        f["category"] = category
        f["secs"] = len(buf) / S.SR
        f["profile"] = profile(buf, f["mags"])
        rows.append(f)
        sys.stdout.write(".")
        sys.stdout.flush()

    print("")
    print("%-22s %-8s %9s %9s %8s %7s %7s"
          % ("name", "category", "centroid", "rolloff", "spread", "attack", "crest"))
    print("-" * 78)
    for r in rows:
        print("%-22s %-8s %8.0fH %8.0fH %7.0fH %6.3fs %6.1fd"
              % (r["name"], r["category"], r["centroid"], r["rolloff"],
                 r["spread"], r["attack"], r["crest"]))

    if not want_confuse:
        return 0

    cats = {}
    for r in rows:
        cats.setdefault(r["category"], []).append(r)
    print("")
    print("Confusion (spectral + envelope cosine distance; %.2f or less is confusable)"
          % CONFUSABLE)
    worst = 0
    for cat in sorted(cats):
        items = cats[cat]
        if len(items) < 2:
            continue
        pairs = []
        for i in xrange(len(items)):
            for j in xrange(i + 1, len(items)):
                d = cosine_distance(items[i]["profile"], items[j]["profile"])
                pairs.append((d, items[i]["name"], items[j]["name"]))
        pairs.sort()
        print("")
        print("  %s - %d sounds, closest pairs:" % (cat, len(items)))
        for d, a, b in pairs[:5]:
            if d > CONFUSABLE:
                flag = ""
            elif is_twin(a, b):
                flag = "  (declared twin)"
            else:
                flag = "  <-- CONFUSABLE"
                worst += 1
            print("    %.3f  %-22s %-22s%s" % (d, a, b, flag))
    print("")
    print("%d confusable pair(s)" % worst)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
