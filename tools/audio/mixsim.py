# -*- coding: utf-8 -*-
"""Render what a real fight actually sounds like, and measure it.

    python tools/audio/mixsim.py                     # every scenario, both policies
    python tools/audio/mixsim.py --scenario boss     # one scenario
    python tools/audio/mixsim.py --policy tiered     # one policy
    python tools/audio/mixsim.py --out DIR           # where the .wav files go

analyse.py answers "can these two sounds be told apart". It cannot answer the question that
actually decides whether a game sounds good, which is "when forty of them fire at once, can
you still hear the one that matters". This does.

It replays a synthetic minute of a run through a model of SfxPlayer - the same throttles, the
same voice pool, the same stealing rule - mixes the result, writes it as a .wav you can listen
to, and reports the numbers that matter:

  * PEAK and CLIPPING, because the sum of many quiet sounds is not a quiet sound.
  * CREST FACTOR, which is the difference between a mix with punch and a wall.
  * HEADROOM, per critical event: how far that boss roar rose above the swarm that was already
    playing. This is the number the whole exercise exists for. Below about 6 dB a sound is
    present but not noticed; below 3 dB it may as well not have played.

THE DRIFT HAZARD. A simulator that quietly stops matching the runtime is worse than no
simulator, because it launders a guess into a measurement. So the throttle windows and pool
sizes are PARSED OUT OF scripts/SfxPlayer.cs rather than copied here, and this file refuses to
run if it cannot find every constant it expects. If you rename one, this breaks loudly. That is
the intended behaviour.

What is modelled and what is not, stated plainly so nobody over-trusts the output:
  * Modelled: per-sound levels as shipped, throttle windows, voice count, voice stealing,
    pitch jitter, 2D distance attenuation, the tier policy and the duck.
  * Approximated: Godot's 2D attenuation curve, as (1 - d/max) ** attenuation. Close enough to
    rank two policies against each other, not exact.
  * Not modelled: the music bus, reverb tails interacting across voices, or the listener.
"""
from __future__ import division

import array
import math
import os
import random
import re
import sys
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import synth as S

SFX_DIR = os.path.join(ROOT, "assets", "sfx")
RUNTIME_SOURCE = os.path.join(ROOT, "scripts", "SfxPlayer.cs")

# Every sound that can fire many times a second. These are the ones a policy has to manage;
# everything else is rare enough to look after itself.
SWARM = set(["enemy_hurt_a", "enemy_hurt_b", "enemy_hurt_c", "pickup_xp", "spell_orbit"])
for _el in ["fire", "ice", "arcane", "darkness", "light", "grass",
            "earth", "wind", "lightning", "poison", "metal", "water"]:
    SWARM.add("impact_" + _el)

# Duck authority is deliberately narrow. player_hurt is loud and important and is NOT here: in a
# swarm it fires several times a second, and a duck that retriggers before it releases turns the
# mix into a pump and takes away hit feedback at the exact moment the player is dying.
CRITICAL = set([
    "boss_roar", "boss_death", "player_death", "elite_spawn",
    "level_up", "spell_evolve", "stage_clear", "meta_unlock", "game_over", "wave_warning",
])


def tier(name):
    if name in CRITICAL:
        return "critical"
    if name in SWARM:
        return "swarm"
    return "normal"


# ---------------------------------------------------------------------------
# runtime constants, parsed rather than copied
# ---------------------------------------------------------------------------

WANTED_CONSTANTS = [
    "PositionalVoices", "GlobalVoices", "MaxAudibleDistance",
    "EnemyHurtGapMs", "EnemyDeathGapMs", "ImpactGapMs", "CastGapMs",
    "DuckDb", "DuckReleaseSeconds",
]


def runtime_constants():
    if not os.path.isfile(RUNTIME_SOURCE):
        raise SystemExit("cannot find %s - run this from the repo root" % RUNTIME_SOURCE)
    text = open(RUNTIME_SOURCE).read()
    out = {}
    for name in WANTED_CONSTANTS:
        m = re.search(r"const\s+\w+\s+" + name + r"\s*=\s*(-?[0-9.]+)f?\s*;", text)
        if not m:
            raise SystemExit(
                "mixsim: could not find constant %s in scripts/SfxPlayer.cs.\n"
                "The simulator refuses to guess - if the constant was renamed, update\n"
                "WANTED_CONSTANTS here so the model cannot drift from the runtime." % name)
        out[name] = float(m.group(1))
    return out


THROTTLE_KEYS = {}
for _n in ["enemy_hurt_a", "enemy_hurt_b", "enemy_hurt_c"]:
    THROTTLE_KEYS[_n] = ("enemy_hurt", "EnemyHurtGapMs")
for _n in ["enemy_death_small", "enemy_death_heavy"]:
    THROTTLE_KEYS[_n] = (_n, "EnemyDeathGapMs")


def throttle_for(name, consts):
    if name in THROTTLE_KEYS:
        key, const = THROTTLE_KEYS[name]
        return key, consts[const] / 1000.0
    if name.startswith("impact_"):
        return name, consts["ImpactGapMs"] / 1000.0
    if name.startswith("cast_"):
        return name, consts["CastGapMs"] / 1000.0
    return name, 0.0


# ---------------------------------------------------------------------------
# sounds
# ---------------------------------------------------------------------------

_sounds = {}
_pitched = {}


def load(name):
    if name not in _sounds:
        path = os.path.join(SFX_DIR, name + ".wav")
        if not os.path.isfile(path):
            raise SystemExit("missing %s - run python tools/audio/build.py first" % path)
        w = wave.open(path, "rb")
        raw = array.array("h")
        raw.fromstring(w.readframes(w.getnframes()))
        w.close()
        _sounds[name] = [v / 32768.0 for v in raw]
    return _sounds[name]


def pitched(name, pitch):
    """Resample for playback rate. Pitch is quantised so the cache actually hits."""
    q = round(pitch, 2)
    key = (name, q)
    if key not in _pitched:
        src = load(name)
        if abs(q - 1.0) < 0.005:
            _pitched[key] = src
        else:
            n = int(len(src) / q)
            out = [0.0] * n
            for i in xrange(n):
                x = i * q
                j = int(x)
                if j >= len(src) - 1:
                    out[i] = src[-1]
                else:
                    f = x - j
                    out[i] = src[j] * (1.0 - f) + src[j + 1] * f
            _pitched[key] = out
    return _pitched[key]


def distance_gain(distance, consts):
    """Approximates Godot 2D attenuation at Attenuation = 1.0. See the header note."""
    d = max(0.0, min(distance, consts["MaxAudibleDistance"]))
    return max(0.0, 1.0 - d / consts["MaxAudibleDistance"])


# ---------------------------------------------------------------------------
# scenarios
# ---------------------------------------------------------------------------
# An event is (time, sound name, distance in pixels, positional?).

class Scenario(object):
    def __init__(self, key, seconds, describe):
        self.key = key
        self.seconds = seconds
        self.describe = describe


def _poisson_times(rate_at, seconds, rng):
    """Event times whose rate varies over the run. rate_at(t) returns events per second."""
    times, t = [], 0.0
    while t < seconds:
        rate = max(0.01, rate_at(t))
        t += rng.expovariate(rate)
        if t < seconds:
            times.append(t)
    return times


def build_events(key, seconds, rng):
    ev = []

    def ramp(a, b):
        return lambda t: a + (b - a) * (t / seconds)

    if key == "earlygame":
        hit_rate, death_rate, xp_rate, casts = ramp(3, 10), ramp(0.6, 2.0), ramp(1.5, 4.0), 2
    elif key == "lategame":
        hit_rate, death_rate, xp_rate, casts = ramp(25, 60), ramp(4, 12), ramp(8, 24), 6
    else:  # boss
        hit_rate, death_rate, xp_rate, casts = ramp(18, 40), ramp(2, 7), ramp(5, 14), 5

    for t in _poisson_times(hit_rate, seconds, rng):
        name = ["enemy_hurt_a", "enemy_hurt_b", "enemy_hurt_c"][rng.randint(0, 2)]
        ev.append((t, name, rng.uniform(30, 520), True))
    for t in _poisson_times(death_rate, seconds, rng):
        heavy = rng.random() < 0.18
        ev.append((t, "enemy_death_heavy" if heavy else "enemy_death_small",
                   rng.uniform(30, 520), True))
    for t in _poisson_times(xp_rate, seconds, rng):
        ev.append((t, "pickup_xp", rng.uniform(0, 90), True))

    # Impacts follow hits closely - this is the block that is not wired yet and that the whole
    # priority question is really about.
    for t in _poisson_times(hit_rate, seconds, rng):
        el = ["fire", "ice", "arcane", "lightning", "earth", "poison"][rng.randint(0, 5)]
        ev.append((t + 0.004, "impact_" + el, rng.uniform(30, 520), True))

    # Casts, on cooldowns, from the player - so distance zero.
    elements = ["fire", "ice", "arcane", "lightning", "earth", "wind"]
    for i in xrange(casts):
        cooldown = 0.55 + 0.25 * i
        t = rng.uniform(0, cooldown)
        while t < seconds:
            ev.append((t, "cast_" + elements[i % len(elements)], 0.0, True))
            t += cooldown

    for t in _poisson_times(lambda t: 0.35, seconds, rng):
        ev.append((t, "player_hurt", 0.0, False))

    ev.append((seconds * 0.33, "level_up", 0.0, False))
    ev.append((seconds * 0.78, "level_up", 0.0, False))
    if key == "boss":
        ev.append((2.0, "boss_roar", 260.0, True))
        ev.append((seconds * 0.55, "boss_roar", 240.0, True))
        ev.append((seconds - 4.0, "boss_death", 220.0, True))
    if key == "lategame":
        ev.append((seconds * 0.45, "elite_spawn", 380.0, True))

    ev.sort(key=lambda e: e[0])
    return ev


SCENARIOS = [
    Scenario("earlygame", 45.0, "first two minutes: sparse, one or two spells"),
    Scenario("boss", 60.0, "a boss fight over a working swarm"),
    Scenario("lategame", 45.0, "the worst case: six spells, sixty hits a second"),
]


# ---------------------------------------------------------------------------
# the mixer
# ---------------------------------------------------------------------------

# Read from scripts/SfxPlayer.cs at run time, like the pool sizes - see runtime_constants().
# These module-level values are only the fallback for a bare import.
DUCK_DB = -6.0
DUCK_RELEASE = 0.30
# Ceiling for the modelled bus limiter, in dBFS. The first run of this simulator found that the
# summed mix clips in EVERY scenario - even the sparse early game - because the registry levels
# in tools/audio/sfx.py were each set in isolation and nothing had ever measured their sum.
LIMIT_DB = -1.0
LIMIT_ATTACK = 0.003
LIMIT_RELEASE = 0.12


def soft_limit(buf, ceiling_db=LIMIT_DB):
    """A feed-forward limiter, standing in for an AudioEffectLimiter on the SFX bus.

    Not a substitute for getting the levels right - a limiter that is working hard flattens the
    crest factor, which is exactly the punch the mix is supposed to have. It is here so the
    simulator can show how much gain reduction the mix is actually asking for.
    """
    ceiling = 10.0 ** (ceiling_db / 20.0)
    n = len(buf)
    out = [0.0] * n
    env = 0.0
    # Instant attack, slow release. A first version used a 3 ms attack and reported 0.1 dB of
    # gain reduction against a mix peaking at +2.5 dBFS: the envelope simply never caught up
    # with a single-sample transient. A real limiter solves that with lookahead; the honest
    # cheap model is to let the envelope jump, which is what a brickwall does anyway.
    r = math.exp(-1.0 / (LIMIT_RELEASE * S.SR))
    worst = 1.0
    for i in xrange(n):
        x = abs(buf[i])
        env = x if x > env else x + r * (env - x)
        g = ceiling / env if env > ceiling else 1.0
        worst = min(worst, g)
        out[i] = buf[i] * g
    return out, S.db(worst)


def simulate(events, consts, policy, seconds, seed=5):
    duck_db = consts.get("DuckDb", DUCK_DB)
    duck_release = consts.get("DuckReleaseSeconds", DUCK_RELEASE)
    rng = random.Random(seed)
    total = S.n_samples(seconds + 3.0)
    mix = [0.0] * total
    swarm_only = [0.0] * total          # for the headroom measurement
    voices = [0.0] * int(consts["PositionalVoices"])   # time each voice frees up
    voice_tier = ["swarm"] * len(voices)
    flat = [0.0] * int(consts["GlobalVoices"])
    next_voice = 0
    last_played = {}
    duck_until = 0.0
    stats = dict(played=0, throttled=0, stolen=0, dropped=0)
    criticals = []

    for t, name, distance, positional in events:
        key, gap = throttle_for(name, consts)
        if gap > 0.0 and t - last_played.get(key, -99.0) < gap:
            stats["throttled"] += 1
            continue
        last_played[key] = t

        this_tier = tier(name)
        pitch = 1.0 + rng.uniform(-0.06, 0.06) if this_tier == "swarm" else 1.0
        buf = pitched(name, pitch)
        gain = distance_gain(distance, consts) if positional else 1.0
        if policy == "tiered" and this_tier == "critical":
            # Criticals keep their position for panning but ignore distance attenuation. The
            # first run measured elite_spawn at -20 dB UNDER the swarm, because it happens to
            # spawn across the screen: a rule that says "this is the sound that matters" and
            # then halves it for being 380 px away is not a rule at all.
            gain = 1.0

        if positional:
            free = [i for i, until in enumerate(voices) if until <= t]
            if free:
                slot = free[0]
            elif policy == "tiered":
                # Swarm sounds drop themselves rather than stealing. Losing the 25th
                # simultaneous hit costs nothing; stealing a cast to play it costs a lot.
                if this_tier == "swarm":
                    stats["dropped"] += 1
                    continue
                lower = [i for i, tr in enumerate(voice_tier)
                         if (tr == "swarm") or (tr == "normal" and this_tier == "critical")]
                if not lower:
                    stats["dropped"] += 1
                    continue
                slot = min(lower, key=lambda i: voices[i])
                stats["stolen"] += 1
            else:
                slot = next_voice % len(voices)
                next_voice += 1
                stats["stolen"] += 1
            voices[slot] = t + len(buf) / S.SR
            voice_tier[slot] = this_tier
        else:
            free = [i for i, until in enumerate(flat) if until <= t]
            slot = free[0] if free else 0
            flat[slot] = t + len(buf) / S.SR

        if policy == "tiered" and this_tier == "critical":
            duck_until = max(duck_until, t + len(buf) / S.SR)

        offset = S.n_samples(t)
        contribution = None
        if this_tier == "critical":
            contribution = [0.0] * len(buf)
        for i in xrange(len(buf)):
            j = offset + i
            if j >= total:
                break
            v = buf[i] * gain
            if policy == "tiered" and this_tier != "critical":
                # Scripted duck: a fixed attenuation with an attack and a release, rather than a
                # compressor, because a fixed curve is exactly reproducible here and a
                # compressor is not. Never ducks to silence - losing hit feedback during a boss
                # roar is worse than the noise it was fixing.
                now = j / S.SR
                if now < duck_until:
                    v *= 10.0 ** (duck_db / 20.0)
                elif now < duck_until + duck_release:
                    k = (now - duck_until) / duck_release
                    v *= 10.0 ** ((duck_db * (1.0 - k)) / 20.0)
            mix[j] += v
            if this_tier == "swarm":
                swarm_only[j] += v
            if contribution is not None:
                contribution[i] = v
        stats["played"] += 1
        if contribution is not None:
            criticals.append((t, name, contribution))

    return mix, swarm_only, stats, criticals


def headroom(mix, swarm_only, criticals):
    """For each critical event: how far its own contribution rose above the swarm underneath."""
    rows = []
    for t, name, contribution in criticals:
        start = S.n_samples(t)
        span = min(len(contribution), S.n_samples(0.5))
        end = min(len(mix), start + span)
        if end <= start:
            continue
        own = S.rms(contribution[:end - start])
        bed = S.rms(swarm_only[start:end])
        rows.append((t, name, S.db(own) - S.db(bed) if bed > 1e-9 else 99.0))
    return rows


def report(label, mix, swarm_only, stats, criticals):
    pk, rm = S.peak(mix), S.rms(mix)
    clipped = len([v for v in mix if abs(v) > 1.0])
    print("  %-9s peak %6.1f dBFS  rms %6.1f dBFS  crest %4.1f dB  clipped %d"
          % (label, S.db(pk), S.db(rm), S.db(pk) - S.db(rm), clipped))
    limited, reduction = soft_limit(mix)
    print("            with a limiter at %.0f dBFS: peak %5.1f, crest %4.1f dB, "
          "worst gain reduction %.1f dB"
          % (LIMIT_DB, S.db(S.peak(limited)), S.db(S.peak(limited)) - S.db(S.rms(limited)),
             reduction))
    print("            played %d, throttled %d, stolen %d, dropped %d"
          % (stats["played"], stats["throttled"], stats["stolen"], stats["dropped"]))
    rows = headroom(mix, swarm_only, criticals)
    if rows:
        worst = min(rows, key=lambda r: r[2])
        print("            critical headroom: " + ", ".join(
            ["%s %+.1f dB" % (n, h) for _t, n, h in rows]))
        if worst[2] < 6.0:
            print("            WARNING: %s is only %+.1f dB over the swarm - it will not be noticed"
                  % (worst[1], worst[2]))
    return rows


def main(argv):
    consts = runtime_constants()
    out_dir = os.path.join(HERE, "_mixsim")
    if "--out" in argv:
        out_dir = argv[argv.index("--out") + 1]
    want_scenarios = [s for s in SCENARIOS
                      if "--scenario" not in argv or s.key == argv[argv.index("--scenario") + 1]]
    policies = ["current", "tiered"]
    if "--policy" in argv:
        policies = [argv[argv.index("--policy") + 1]]
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)

    print("runtime constants from scripts/SfxPlayer.cs: "
          + ", ".join(["%s=%g" % (k, consts[k]) for k in WANTED_CONSTANTS]))
    for sc in SCENARIOS:
        if sc not in want_scenarios:
            continue
        rng = random.Random(hash(sc.key) & 0xffff)
        events = build_events(sc.key, sc.seconds, rng)
        print("")
        print("%s - %s (%.0f s, %d events)" % (sc.key, sc.describe, sc.seconds, len(events)))
        for policy in policies:
            mix, swarm_only, stats, criticals = simulate(events, consts, policy, sc.seconds)
            report(policy, mix, swarm_only, stats, criticals)
            path = os.path.join(out_dir, "mix_%s_%s.wav" % (sc.key, policy))
            # Written without normalising: the whole point is what the mix ACTUALLY does, and
            # normalising away a clipping problem would hide the thing being measured.
            limited, _reduction = soft_limit(mix)
            S.write_wav(path, [max(-1.0, min(1.0, v)) for v in limited])
            print("            wrote %s" % os.path.relpath(path, ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
