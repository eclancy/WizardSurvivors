# -*- coding: utf-8 -*-
"""The element sound palette - the audio counterpart of bonelight.MATERIALS.

.ai/art-direction.md section 3 states the visual rule for the twelve elements:

    The core of every element's effect is white-hot. Identity lives in the mid and edge.

This module is the same rule in time instead of space:

    The first 8 ms of every element sound is the same white transient.
    Identity lives in the body and the tail.

So a cast always reads as "a spell went off" on the attack, and the hue answers "which one"
a beat later - which is what lets twenty spells share one attention budget in a screen with
sixty enemies on it. The transient is deliberately shared and deliberately bright; it is the
audio equivalent of the reserved rim colour, and like the rim it is never element-tinted.

ELEMENTS below is a budget, not a suggestion, in exactly the way the colour table is. A new
element means a new row added here, reviewed, and then used - never numbers typed inline at
a call site. The rows are ordered to match the colour table in the art contract.
"""
from __future__ import division

import synth as S

# Ordering matches the element table in .ai/art-direction.md section 3.
ORDER = [
    "fire", "ice", "arcane", "darkness", "light", "grass",
    "earth", "wind", "lightning", "poison", "metal", "water",
]

# f0/f1   - the body's pitch or filter-centre sweep, in Hz (f0 -> f1 over the body)
# c0/c1   - the cutoff sweep of the element's shaping filter
# q       - filter resonance; high q is a narrow, whistling, "tuned" element
# dur     - body length in seconds, before the tail
# tail    - reverb mix, 0 = dry and close, high = distant and large
# grit    - saturation amount; 0 disables the drive stage
ELEMENTS = {
    "fire":      dict(f0=110, f1=48,   c0=2800, c1=380,  q=1.1, dur=0.42, tail=0.14, grit=4.0),
    "ice":       dict(f0=1500, f1=1750, c0=4200, c1=6200, q=2.2, dur=0.34, tail=0.30, grit=0.0),
    "arcane":    dict(f0=330, f1=760,  c0=1500, c1=2900, q=1.4, dur=0.52, tail=0.36, grit=0.0),
    "darkness":  dict(f0=190, f1=62,   c0=1100, c1=180,  q=1.6, dur=0.60, tail=0.34, grit=1.8),
    "light":     dict(f0=880, f1=880,  c0=6000, c1=3200, q=0.8, dur=0.75, tail=0.30, grit=0.0),
    "grass":     dict(f0=300, f1=214,  c0=2000, c1=700,  q=1.8, dur=0.28, tail=0.10, grit=1.6),
    "earth":     dict(f0=150, f1=44,   c0=900,  c1=260,  q=1.0, dur=0.40, tail=0.12, grit=5.0),
    "wind":      dict(f0=760, f1=980,  c0=700,  c1=2600, q=2.6, dur=0.62, tail=0.22, grit=0.0),
    "lightning": dict(f0=6200, f1=1300, c0=3400, c1=2200, q=1.5, dur=0.24, tail=0.16, grit=3.2),
    "poison":    dict(f0=232, f1=176,  c0=880,  c1=520,  q=3.0, dur=0.50, tail=0.18, grit=1.4),
    "metal":     dict(f0=620, f1=612,  c0=5200, c1=2600, q=1.0, dur=0.85, tail=0.28, grit=0.0),
    "water":     dict(f0=520, f1=1450, c0=1300, c1=430,  q=1.2, dur=0.36, tail=0.20, grit=0.0),
}

# Inharmonic ratio sets. Integer ratios ring like a tuned instrument; these do not, which is
# what makes metal sound struck and ice sound cracked rather than played.
_BELL = [1.0, 2.00, 3.01, 4.17, 5.43]
_METAL = [1.0, 1.71, 2.46, 3.39, 4.51, 5.88]
_ICE = [1.0, 2.76, 5.40, 8.93]


def transient(level=1.0, length=0.008, seed=7):
    """The shared white attack. Identical for every element by contract - do not tint it."""
    n = S.n_samples(length)
    body = S.osc("noise", 0, length, seed=seed)
    body = S.highpass(body, 2600, 0.7)
    click = S.osc("sine", S.sweep(length, 3200, 900, 0.5), length)
    out = S.mix(S.env_mul(body, S.decay(n, 4.0)), S.gain(S.env_mul(click, S.decay(n, 8.0)), 0.55))
    return S.gain(out, level)


# ---------------------------------------------------------------------------
# per-element bodies
# ---------------------------------------------------------------------------

def _fire(p, dur, seed):
    n = S.n_samples(dur)
    roar = S.bandpass(S.osc("noise", 0, dur, seed=seed), S.sweep(dur, p["c0"], p["c1"], 0.6), p["q"])
    roar = S.gate(roar, 95.0, 0.86, seed=seed + 1)          # the crackle in a flame
    thump = S.osc("sine", S.sweep(dur, p["f0"], p["f1"], 0.5), dur)
    out = S.mix(S.env_mul(roar, S.decay(n, 2.2)), S.gain(S.env_mul(thump, S.expdecay(n, 7.0)), 0.7))
    return S.drive(out, p["grit"])


def _ice(p, dur, seed):
    n = S.n_samples(dur)
    shards = S.partials(S.sweep(dur, p["f0"], p["f1"], 1.0), _ICE, dur,
                        amps=[1.0, 0.6, 0.34, 0.2], decays=[7.0, 11.0, 15.0, 20.0])
    frost = S.bandpass(S.osc("noise", 0, dur, seed=seed), S.sweep(dur, p["c0"], p["c1"], 0.7), p["q"])
    return S.mix(S.gain(shards, 0.8), S.gain(S.env_mul(frost, S.decay(n, 5.0)), 0.45))


def _arcane(p, dur, seed):
    n = S.n_samples(dur)
    f = S.sweep(dur, p["f0"], p["f1"], 0.7)
    a = S.osc("tri", f, dur)
    b = S.osc("tri", [v * 1.007 for v in f], dur, phase=0.31)   # detune, not chorus
    voice = S.mix(S.gain(a, 0.55), S.gain(b, 0.55))
    voice = S.tremolo(voice, 17.0, 0.4)
    voice = S.lowpass(voice, S.sweep(dur, p["c0"], p["c1"], 1.0), p["q"])
    return S.env_mul(voice, S.ad(dur, 0.012, 2.0))


def _darkness(p, dur, seed):
    """Built forwards and then reversed - a swell that arrives instead of a hit that decays."""
    n = S.n_samples(dur)
    sub = S.osc("saw", S.sweep(dur, p["f1"], p["f0"], 1.0), dur)
    air = S.osc("noise", 0, dur, seed=seed)
    voice = S.mix(S.gain(sub, 0.7), S.gain(air, 0.35))
    voice = S.lowpass(voice, S.sweep(dur, p["c1"], p["c0"], 1.4), p["q"])
    voice = S.reverse(S.env_mul(voice, S.decay(n, 2.0)))
    return S.drive(voice, p["grit"])


def _light(p, dur, seed):
    n = S.n_samples(dur)
    bell = S.partials(p["f0"], _BELL, dur, amps=[1.0, 0.5, 0.3, 0.18, 0.1],
                      decays=[3.0, 4.5, 6.0, 8.0, 10.0])
    fifth = S.osc("sine", p["f0"] * 1.5, dur)
    out = S.mix(S.gain(bell, 0.8), S.gain(S.env_mul(fifth, S.expdecay(n, 4.0)), 0.3))
    return S.lowpass(out, S.sweep(dur, p["c0"], p["c1"], 1.0), p["q"])


def _grass(p, dur, seed):
    n = S.n_samples(dur)
    pluck = S.osc("saw", S.sweep(dur, p["f0"], p["f1"], 0.4), dur)
    pluck = S.lowpass(pluck, S.sweep(dur, p["c0"], p["c1"], 1.2), p["q"])
    knock = S.env_mul(S.osc("mnoise", 1200, dur, seed=seed), S.decay(n, 14.0))
    out = S.mix(S.env_mul(pluck, S.decay(n, 3.2)), S.gain(knock, 0.4))
    return S.drive(out, p["grit"])


def _earth(p, dur, seed):
    n = S.n_samples(dur)
    thud = S.osc("sine", S.sweep(dur, p["f0"], p["f1"], 0.35), dur)
    grit = S.lowpass(S.osc("noise", 0, dur, seed=seed), S.sweep(dur, p["c0"], p["c1"], 0.8), p["q"])
    grit = S.decimate(grit, 3)                               # rubble, not hiss
    out = S.mix(S.env_mul(thud, S.expdecay(n, 5.0)), S.gain(S.env_mul(grit, S.decay(n, 3.0)), 0.5))
    return S.drive(out, p["grit"])


def _wind(p, dur, seed):
    n = S.n_samples(dur)
    # Two overlapping bandpasses moving in opposite directions: a gust has no pitch centre.
    air = S.osc("noise", 0, dur, seed=seed)
    a = S.bandpass(air, S.sweep(dur, p["c0"], p["c1"], 0.8), p["q"])
    b = S.bandpass(air, S.sweep(dur, p["f1"], p["f0"], 1.4), p["q"] * 1.6)
    out = S.mix(S.gain(a, 0.8), S.gain(b, 0.45))
    return S.env_mul(out, S.ad(dur, 0.09, 1.6))


def _lightning(p, dur, seed):
    n = S.n_samples(dur)
    zap = S.bandpass(S.osc("noise", 0, dur, seed=seed), S.sweep(dur, p["f0"], p["f1"], 2.0), p["q"])
    crackle = S.gate(S.highpass(S.osc("noise", 0, dur, seed=seed + 5), p["c0"]), 220.0, 0.5,
                     seed=seed + 9)
    out = S.mix(S.env_mul(zap, S.decay(n, 3.0)), S.gain(S.env_mul(crackle, S.decay(n, 1.6)), 0.55))
    return S.drive(out, p["grit"])


def _poison(p, dur, seed):
    n = S.n_samples(dur)
    gurgle = S.fm("sine", S.sweep(dur, p["f0"], p["f1"], 1.0), 9.0, 0.42, dur, seed=seed)
    gurgle = S.bandpass(gurgle, S.sweep(dur, p["c0"], p["c1"], 1.0), p["q"])
    bubbles = S.gate(S.osc("sine", S.sweep(dur, 700, 320, 1.0), dur), 26.0, 0.4, seed=seed + 3)
    out = S.mix(S.env_mul(gurgle, S.ad(dur, 0.02, 2.2)), S.gain(S.env_mul(bubbles, S.decay(n, 2.0)), 0.3))
    return S.drive(out, p["grit"])


def _metal(p, dur, seed):
    n = S.n_samples(dur)
    struck = S.partials(S.sweep(dur, p["f0"], p["f1"], 1.0), _METAL, dur,
                        amps=[1.0, 0.72, 0.5, 0.36, 0.24, 0.15],
                        decays=[2.2, 3.0, 4.0, 5.0, 6.5, 8.0])
    struck = S.ringmod(struck, 137.0, 0.35)
    return S.lowpass(struck, S.sweep(dur, p["c0"], p["c1"], 1.0), p["q"])


def _water(p, dur, seed):
    n = S.n_samples(dur)
    droplet = S.osc("sine", S.sweep(dur, p["f0"], p["f1"], 2.4), dur)
    wash = S.lowpass(S.osc("noise", 0, dur, seed=seed), S.sweep(dur, p["c0"], p["c1"], 0.9), p["q"])
    return S.mix(S.env_mul(droplet, S.expdecay(n, 9.0)), S.gain(S.env_mul(wash, S.decay(n, 2.6)), 0.5))


BODIES = {
    "fire": _fire, "ice": _ice, "arcane": _arcane, "darkness": _darkness,
    "light": _light, "grass": _grass, "earth": _earth, "wind": _wind,
    "lightning": _lightning, "poison": _poison, "metal": _metal, "water": _water,
}


def body(element, dur_scale=1.0, seed=11):
    """The element's voice on its own, without the shared transient or the tail."""
    p = ELEMENTS[element]
    return BODIES[element](p, p["dur"] * dur_scale, seed)


def _finish(buf, tail_mix):
    buf = S.reverb(buf, size=0.55, damp=0.45, mix=tail_mix)
    return S.dc_block(buf)


def cast(element, seed=11):
    """A spell leaving the caster: full transient, full body, full tail."""
    p = ELEMENTS[element]
    voice = body(element, 1.0, seed)
    out = S.layer([(transient(0.85), 0.0, 1.0), (voice, 0.004, 0.9)])
    return _finish(S.pad(out, p["dur"] + 0.18), p["tail"])


def impact(element, seed=23):
    """A spell landing: harder transient, body squeezed to 55%, a low thump under it.

    Impacts fire far more often than casts - one cast can produce a dozen - so they are
    shorter, darker and quieter by design. The mix table in .ai/audio-direction.md holds
    them about 4 dB under their cast.
    """
    p = ELEMENTS[element]
    dur = p["dur"] * 0.55
    voice = body(element, 0.55, seed)
    n = S.n_samples(dur)
    thump = S.env_mul(S.osc("sine", S.sweep(dur, 210, 70, 0.4), dur), S.expdecay(n, 9.0))
    out = S.layer([
        (transient(1.0, 0.006, seed=seed), 0.0, 1.0),
        (S.env_mul(voice, S.decay(len(voice), 2.4)), 0.003, 0.85),
        (thump, 0.0, 0.5),
    ])
    return _finish(S.pad(out, dur + 0.10), p["tail"] * 0.6)
