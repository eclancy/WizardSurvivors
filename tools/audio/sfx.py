# -*- coding: utf-8 -*-
"""Every named sound in the game, one function each, plus the registry build.py walks.

Read .ai/audio-direction.md before changing anything here - it holds the mix table, the
naming rules and the reasons behind the loudness targets. The short version:

  * A sound's loudness is set by how OFTEN it plays, not by how important it feels. XP orbs
    are the quietest thing in the game because a run collects a few thousand of them.
  * Anything that can fire more than about four times a second is built short, dark and
    dry. Anything that fires once a run can afford a tail.
  * Variants (_a/_b/_c) exist only where a sound repeats fast enough to machine-gun. They
    differ by seed and by a few Hz, never by design intent.

REGISTRY at the bottom is the single source of truth for what gets written. build.py does
not scan this module for functions - add the entry or the sound does not ship.
"""
from __future__ import division

import math

import palette as P
import synth as S


# ---------------------------------------------------------------------------
# small shared voices
# ---------------------------------------------------------------------------

def _hz(semitones, base=440.0):
    """Equal temperament from A4. The stingers sit in A natural minor, to match the score."""
    return base * (2.0 ** (semitones / 12.0))


def _bell(freq, dur, amps=None, decays=None, seed=1):
    ratios = [1.0, 2.0, 3.01, 4.17]
    return S.partials(freq, ratios, dur,
                      amps=amps or [1.0, 0.42, 0.22, 0.12],
                      decays=decays or [3.0, 4.5, 6.5, 9.0])


def _whoosh(dur, c0, c1, q=2.2, seed=3, curve=1.0):
    # A two-pole bandpass on white noise still leaks a long hiss above its centre; the lowpass
    # is what makes a whoosh air rather than steam.
    air = S.bandpass(S.osc("noise", 0, dur, seed=seed), S.sweep(dur, c0, c1, curve), q)
    air = S.lowpass(air, 5500, 0.707)
    return S.env_mul(air, S.ad(dur, dur * 0.28, 2.0))


def _tick(dur, freq, bright, seed=5):
    """bright is the centre of the click band. It was a bare highpass at 3-5 kHz, which put
    three quarters of every UI tick above 4 kHz - and ui_hover plays on every mouse move."""
    n = S.n_samples(dur)
    click = S.lowpass(S.bandpass(S.osc("noise", 0, dur, seed=seed), bright, 1.1), 5000, 0.707)
    tone = S.osc("sine", S.sweep(dur, freq * 1.6, freq, 0.6), dur)
    return S.mix(S.env_mul(click, S.decay(n, 9.0)), S.gain(S.env_mul(tone, S.decay(n, 6.0)), 0.6))


def _arp(notes, step, note_dur, seed=1, shape="bell"):
    """An ascending or descending run of bells. The only musical construct in the set."""
    items = []
    for i, semi in enumerate(notes):
        f = _hz(semi)
        v = _bell(f, note_dur, seed=seed + i) if shape == "bell" else \
            S.env_mul(S.osc(shape, f, note_dur), S.ad(note_dur, 0.006, 3.0))
        items.append((v, i * step, 1.0 / (1.0 + 0.12 * i)))
    return S.layer(items)


# ---------------------------------------------------------------------------
# elements - generated from the palette, one cast and one impact each
# ---------------------------------------------------------------------------

def _make_cast(el):
    return lambda: P.cast(el)


def _make_impact(el):
    return lambda: P.impact(el)


# ---------------------------------------------------------------------------
# spell shapes that no element table can express
# ---------------------------------------------------------------------------

def spell_explosion():
    """ArcaneExplosion, MeteorImpact - the only sounds allowed to occupy the sub band."""
    dur = 0.9
    n = S.n_samples(dur)
    boom = S.osc("sine", S.sweep(dur, 130, 34, 0.3), dur)
    debris = S.lowpass(S.osc("noise", 0, dur, seed=17), S.sweep(dur, 2600, 300, 0.5), 1.0)
    out = S.layer([
        (P.transient(1.0, 0.010), 0.0, 1.0),
        (S.env_mul(boom, S.expdecay(n, 4.5)), 0.0, 1.0),
        (S.env_mul(debris, S.decay(n, 2.0)), 0.006, 0.55),
    ])
    return S.dc_block(S.reverb(S.drive(out, 2.4), size=0.75, damp=0.5, mix=0.22))


def spell_beam_start():
    """ScorchingRay ignition. Runs straight into spell_beam_loop, so it ends mid-body."""
    dur = 0.28
    n = S.n_samples(dur)
    rise = S.bandpass(S.osc("noise", 0, dur, seed=31), S.sweep(dur, 500, 2400, 1.6), 2.0)
    tone = S.osc("saw", S.sweep(dur, 90, 220, 1.2), dur)
    out = S.mix(S.env_mul(rise, S.ramp(n, 0.1, 1.0, 1.4)),
                S.gain(S.env_mul(tone, S.ramp(n, 0.0, 0.8, 2.0)), 0.6))
    # Lowpassed after the drive, like the loop, but higher: it plays once per beam, and at the
    # loop's 4.5 kHz its rising band measured as a twin of spell_bow_draw.
    return S.lowpass(S.drive(out, 2.0), 6500, 0.707)


def spell_beam_loop():
    """Seamlessly loopable sustain. LOOP SOUND, and three things make the seam vanish:

      * every periodic component completes a whole number of cycles in 0.5 s - 220 and 222 Hz
        (110 and 111 cycles) and the two tremolos at 24 and 8 Hz (12 and 4);
      * it is synthesised at 1.0 s and only the SECOND half is kept, so the biquads have
        settled and the loop does not start with a filter warm-up that the wrap lacks;
      * build.py does not fade it. A fade on a loop IS the click.
    """
    dur = 1.0
    keep = 0.5
    fizz = S.bandpass(S.osc("noise", 0, dur, seed=37), 2100, 1.8)
    tone = S.mix(S.gain(S.osc("saw", 220.0, dur), 0.5), S.gain(S.osc("saw", 222.0, dur), 0.5))
    tone = S.lowpass(tone, 1800, 1.1)
    out = S.mix(S.gain(fizz, 0.55), S.gain(tone, 0.6))
    out = S.tremolo(out, 24.0, 0.28)
    out = S.tremolo(out, 8.0, 0.16)
    out = S.drive(out, 1.8)
    # The drive stage's harmonics are what made a held beam fizz. Filtering the whole second
    # and keeping the settled half keeps the seam exactly as clean as it was.
    out = S.lowpass(out, 4500, 0.707)
    return S.dc_block(out[S.n_samples(dur - keep):])


def spell_beam_end():
    dur = 0.34
    n = S.n_samples(dur)
    fall = S.bandpass(S.osc("noise", 0, dur, seed=41), S.sweep(dur, 2200, 420, 0.8), 2.0)
    tone = S.osc("saw", S.sweep(dur, 210, 70, 0.7), dur)
    out = S.mix(S.env_mul(fall, S.decay(n, 2.4)), S.gain(S.env_mul(tone, S.decay(n, 3.0)), 0.5))
    out = S.lowpass(out, 5000, 0.707)
    return S.dc_block(S.reverb(out, mix=0.18))


def spell_orbit():
    """SpiritualWeapon / OrbitingBlade tick. Fires on a timer for the whole run, so it is
    the second quietest thing in the set after the XP orb, and has no tail at all."""
    dur = 0.17
    n = S.n_samples(dur)
    swipe = S.bandpass(S.osc("noise", 0, dur, seed=43), S.sweep(dur, 2200, 700, 1.0), 3.2)
    swipe = S.lowpass(swipe, 4000, 0.707)
    ring = S.env_mul(S.osc("sine", 820, dur), S.expdecay(n, 12.0))
    # The ring carries more of it than it did: with the swipe pulled down out of the top it was
    # a twin of spell_bow_release, and a tone is the part the bow does not have.
    return S.mix(S.env_mul(swipe, S.decay(n, 6.0)), S.gain(ring, 0.45))


def spell_summon():
    """BlackTentacles, ThornVine, GuardianVines - something arrives out of the ground."""
    dur = 0.7
    n = S.n_samples(dur)
    earth = P.body("earth", 1.2, seed=47)
    grow = S.osc("saw", S.sweep(dur, 70, 260, 2.0), dur)
    grow = S.lowpass(grow, S.sweep(dur, 400, 1700, 1.4), 2.4)
    out = S.layer([
        (S.env_mul(earth, S.decay(len(earth), 2.0)), 0.0, 0.7),
        (S.env_mul(grow, S.ad(dur, 0.10, 1.6)), 0.05, 0.8),
    ])
    return S.dc_block(S.reverb(S.drive(out, 2.0), size=0.6, mix=0.2))


def spell_bow_draw():
    """HuntersDraw wind-up. Deliberately 0.45 s so it can telegraph the release.

    The creak sits at 620-1300 Hz, down from 900-1900: once spell_beam_start lost its top end
    the two rising bands measured as twins, and moving the creak down was the softer fix."""
    dur = 0.45
    n = S.n_samples(dur)
    creak = S.bandpass(S.osc("mnoise", S.sweep(dur, 90, 240, 1.0), dur, seed=53),
                       S.sweep(dur, 620, 1300, 1.0), 4.0)
    return S.env_mul(creak, S.ramp(n, 0.05, 1.0, 1.8))


def spell_bow_release():
    dur = 0.3
    n = S.n_samples(dur)
    snap = S.lowpass(S.highpass(S.osc("noise", 0, dur, seed=59), 1400), 4500, 0.707)
    string = S.osc("tri", S.sweep(dur, 320, 150, 0.4), dur)
    out = S.mix(S.env_mul(snap, S.decay(n, 12.0)),
                S.gain(S.env_mul(string, S.expdecay(n, 10.0)), 0.7))
    return S.mix(out, S.gain(_whoosh(dur, 800, 2400, 2.4, seed=61), 0.35))


# ---------------------------------------------------------------------------
# the player
# ---------------------------------------------------------------------------

def player_hurt():
    """Fires from Player.TakeDamage step 8, after mitigation - so it means HP actually left."""
    dur = 0.34
    n = S.n_samples(dur)
    thud = S.osc("sine", S.sweep(dur, 210, 62, 0.45), dur)
    tear = S.lowpass(S.osc("noise", 0, dur, seed=67), S.sweep(dur, 2200, 500, 0.7), 1.2)
    out = S.mix(S.env_mul(thud, S.expdecay(n, 7.0)), S.gain(S.env_mul(tear, S.decay(n, 3.0)), 0.6))
    return S.dc_block(S.drive(out, 3.0))


def player_dodge():
    """The dodge roll fully avoids the hit, so this must not sound like a hit at all."""
    dur = 0.26
    return S.gain(_whoosh(dur, 600, 2500, 3.0, seed=71, curve=0.6), 0.9)


def player_heal():
    dur = 0.6
    out = S.layer([
        (_bell(_hz(4), dur, seed=73), 0.0, 0.8),
        (_bell(_hz(11), dur * 0.8, seed=79), 0.09, 0.6),
    ])
    return S.dc_block(S.reverb(S.lowpass(out, 4200, 0.8), mix=0.26))


def player_shield_absorb():
    """Shield pool soak, step 6 of the pipeline. Bright and metallic so it reads as NOT-HP."""
    dur = 0.3
    n = S.n_samples(dur)
    chink = S.partials(1050, [1.0, 1.71, 2.46, 3.39], dur,
                       amps=[1.0, 0.6, 0.36, 0.2], decays=[6.0, 8.0, 10.0, 13.0])
    return S.mix(S.gain(P.transient(0.5, 0.005, seed=83), 1.0), S.gain(chink, 0.8))


def player_shield_break():
    dur = 0.65
    n = S.n_samples(dur)
    shatter = S.partials(S.sweep(dur, 1200, 900, 1.0), [1.0, 2.76, 5.40, 8.93, 12.1], dur,
                         amps=[1.0, 0.7, 0.5, 0.32, 0.2], decays=[3.0, 4.0, 5.5, 7.0, 9.0])
    shatter = S.lowpass(shatter, 6000, 0.707)
    debris = S.lowpass(S.highpass(S.osc("noise", 0, dur, seed=89), 2200), 5500, 0.707)
    debris = S.gate(debris, 60.0, 0.45, seed=97)
    out = S.mix(S.gain(shatter, 0.85), S.gain(S.env_mul(debris, S.decay(n, 2.4)), 0.4))
    return S.dc_block(S.reverb(out, size=0.7, mix=0.3))


def player_death():
    dur = 1.6
    n = S.n_samples(dur)
    fall = S.osc("saw", S.sweep(dur, 220, 38, 1.6), dur)
    fall = S.lowpass(fall, S.sweep(dur, 2000, 260, 1.2), 1.6)
    air = S.lowpass(S.osc("noise", 0, dur, seed=101), S.sweep(dur, 1400, 200, 1.0), 1.0)
    out = S.mix(S.env_mul(fall, S.decay(n, 1.6)), S.gain(S.env_mul(air, S.decay(n, 2.0)), 0.4))
    return S.dc_block(S.reverb(S.drive(out, 2.2), size=0.85, damp=0.35, mix=0.34))


def player_low_health():
    """A two-beat pulse, meant to be played on a repeating timer, not once. Kept narrow-band
    around 520 Hz so it cuts through the music without competing with any spell."""
    dur = 0.55
    beat = 0.2
    n = S.n_samples(beat)
    one = S.env_mul(S.osc("sine", 520, beat), S.ad(beat, 0.01, 3.0))
    two = S.env_mul(S.osc("sine", 392, beat), S.ad(beat, 0.01, 3.0))
    return S.pad(S.layer([(one, 0.0, 1.0), (two, 0.24, 0.85)]), dur)


# ---------------------------------------------------------------------------
# enemies
# ---------------------------------------------------------------------------

def _enemy_hurt(seed, freq):
    """Three of these ship. In a swarm this plays dozens of times a second, so it is short,
    band-limited and quiet - and Player-side code should still rate-limit it."""
    dur = 0.13
    n = S.n_samples(dur)
    hit = S.bandpass(S.osc("noise", 0, dur, seed=seed), S.sweep(dur, freq * 3, freq, 0.6), 2.0)
    body = S.osc("tri", S.sweep(dur, freq, freq * 0.55, 0.5), dur)
    out = S.mix(S.env_mul(hit, S.decay(n, 7.0)), S.gain(S.env_mul(body, S.expdecay(n, 11.0)), 0.55))
    # Lowpassed after the drive: the swarm-rate sound in the game must not carry a fizz.
    return S.lowpass(S.drive(out, 2.0), 3500, 0.707)


def enemy_hurt_a():
    return _enemy_hurt(103, 340)


def enemy_hurt_b():
    return _enemy_hurt(107, 300)


def enemy_hurt_c():
    return _enemy_hurt(109, 378)


def enemy_death_small():
    """Bone clatter. Enemy.StartDeath drops rewards and leaves the group in the same frame,
    so this is allowed to outlast the corpse - it is not gating anything."""
    dur = 0.45
    n = S.n_samples(dur)
    clatter = S.gate(S.bandpass(S.osc("mnoise", 3000, dur, seed=113), 1900, 3.0),
                     42.0, 0.55, seed=127)
    thud = S.osc("sine", S.sweep(dur, 180, 70, 0.5), dur)
    out = S.mix(S.env_mul(clatter, S.decay(n, 2.2)), S.gain(S.env_mul(thud, S.expdecay(n, 9.0)), 0.6))
    return S.dc_block(S.reverb(out, mix=0.14))


def enemy_death_heavy():
    dur = 0.75
    n = S.n_samples(dur)
    collapse = S.osc("sine", S.sweep(dur, 130, 40, 0.4), dur)
    rubble = S.decimate(S.lowpass(S.osc("noise", 0, dur, seed=131), S.sweep(dur, 1500, 320, 0.8)), 3)
    rubble = S.gate(rubble, 30.0, 0.6, seed=137)
    out = S.mix(S.env_mul(collapse, S.expdecay(n, 5.0)), S.gain(S.env_mul(rubble, S.decay(n, 1.8)), 0.55))
    return S.dc_block(S.reverb(S.drive(out, 2.6), size=0.65, mix=0.2))


def enemy_shoot():
    """RangedEnemy / EnemyProjectile. Dry and pitched high, so it never reads as the player."""
    dur = 0.2
    n = S.n_samples(dur)
    pop = S.osc("square", S.sweep(dur, 760, 320, 0.4), dur)
    # A raw square carries odd harmonics to Nyquist; that was half this sound above 4 kHz.
    pop = S.lowpass(pop, 3000, 0.707)
    air = S.bandpass(S.osc("noise", 0, dur, seed=139), S.sweep(dur, 2000, 900, 0.8), 2.6)
    return S.mix(S.env_mul(pop, S.decay(n, 8.0)), S.gain(S.env_mul(air, S.decay(n, 5.0)), 0.45))


def enemy_melee():
    """A contact hit landing on the player. Pairs with player_hurt; this is the attacker."""
    dur = 0.22
    n = S.n_samples(dur)
    swing = _whoosh(0.14, 1800, 600, 2.4, seed=149)
    hit = S.osc("sine", S.sweep(dur, 260, 90, 0.4), dur)
    out = S.layer([(swing, 0.0, 0.5), (S.env_mul(hit, S.expdecay(n, 10.0)), 0.05, 1.0)])
    return S.drive(out, 2.4)


def elite_spawn():
    """The gold-rim elite. A struck metal tone played backwards, so it arrives rather than
    lands - the only enemy cue with an upward gesture.

    It sits an octave and a half above boss_roar on purpose. Both are "something worse is
    here" and the first version of this shared boss_roar's register, which made every elite
    read as a boss for the half second before the sprite resolved."""
    dur = 0.9
    n = S.n_samples(dur)
    gong = S.reverse(P.body("metal", 1.0, seed=151))
    rise = S.bandpass(S.osc("noise", 0, dur, seed=153), S.sweep(dur, 800, 2600, 1.6), 2.8)
    out = S.layer([
        (S.gain(gong, 0.8), 0.0, 1.0),
        (S.env_mul(rise, S.ramp(n, 0.0, 1.0, 2.2)), 0.0, 0.45),
    ])
    return S.dc_block(S.reverb(out, size=0.8, mix=0.3))


def boss_roar():
    dur = 1.8
    n = S.n_samples(dur)
    growl = S.fm("saw", S.sweep(dur, 62, 46, 1.0), 27.0, 0.5, dur, seed=157)
    growl = S.lowpass(growl, S.sweep(dur, 1400, 380, 1.0), 1.4)
    rasp = S.bandpass(S.osc("noise", 0, dur, seed=163), S.sweep(dur, 900, 400, 1.0), 2.0)
    out = S.mix(S.env_mul(growl, S.ad(dur, 0.18, 1.4)), S.gain(S.env_mul(rasp, S.ad(dur, 0.3, 1.6)), 0.4))
    return S.dc_block(S.reverb(S.drive(out, 3.4), size=0.9, damp=0.3, mix=0.34))


def boss_death():
    dur = 2.4
    n = S.n_samples(dur)
    collapse = S.osc("saw", S.sweep(dur, 110, 26, 1.8), dur)
    collapse = S.lowpass(collapse, S.sweep(dur, 1600, 160, 1.4), 1.8)
    rubble = S.gate(S.decimate(S.lowpass(S.osc("noise", 0, dur, seed=167), 1200), 4),
                    22.0, 0.55, seed=173)
    out = S.layer([
        (P.transient(1.0, 0.012), 0.0, 1.0),
        (S.env_mul(collapse, S.decay(n, 1.5)), 0.0, 1.0),
        (S.env_mul(rubble, S.decay(n, 1.2)), 0.03, 0.5),
    ])
    return S.dc_block(S.reverb(S.drive(out, 3.0), size=0.95, damp=0.3, mix=0.36))


# ---------------------------------------------------------------------------
# pickups and progression
# ---------------------------------------------------------------------------

def pickup_xp():
    """The most-played sound in the game by an order of magnitude. Everything about it is a
    concession to that: 90 ms, one narrow band, no tail, and the quietest target in the mix.
    Vary it at PLAY time with AudioStreamPlayer2D.PitchScale, not with more files."""
    dur = 0.09
    n = S.n_samples(dur)
    # About a fourth lower than it was (760-1140 Hz, from 1050-1580), because the streak ladder in
    # SfxPlayer.PickupXp pitches it up to 1.4x on top: at the old register a long streak ended
    # above 2 kHz as a pure sine, which is the definition of a sound that grates. The 6 ms
    # attack rounds the onset off without losing the tick.
    blip = S.osc("sine", S.sweep(dur, 760, 1140, 0.5), dur)
    return S.env_mul(blip, S.ad(dur, 0.006, 3.0))


def pickup_health():
    """Deliberately NOT a bell. Every other reward in the game is one, and the audit had this
    measuring as a small chest_open; a soft filtered glow keeps HP in its own timbre."""
    dur = 0.42
    n = S.n_samples(dur)
    a = S.env_mul(S.osc("sine", _hz(0), dur), S.ad(dur, 0.05, 2.0))
    b = S.env_mul(S.osc("tri", _hz(7), dur), S.ad(dur, 0.07, 2.2))
    warm = S.mix(S.gain(a, 0.7), S.gain(b, 0.45))
    return S.dc_block(S.lowpass(warm, S.sweep(dur, 2600, 1100, 1.0), 0.9))


def pickup_magnet():
    """FortunesFavor / vacuum effects. Six accelerating blips rather than one smooth sweep:
    the event is many orbs arriving, and a glide measured as a slower pickup_health."""
    items = []
    t = 0.0
    for i in xrange(6):
        blip = S.env_mul(S.osc("sine", S.sweep(0.07, 520 + i * 170, 760 + i * 210, 0.5), 0.07),
                         S.ad(0.07, 0.003, 3.0))
        items.append((blip, t, 0.55 + 0.09 * i))
        t += 0.075 - 0.008 * i          # accelerating, so it reads as being pulled in
    return S.dc_block(S.layer(items))


def pickup_haste():
    """The speed potion (BuffItem): attack and move speed for eight seconds.

    A gust that passes you twice - two short bandpassed swishes, the second quicker and higher,
    which is how wind sounds when you are the thing moving - and then a shimmer that climbs: five
    triangle grains a step apart under a fast tremolo. "Faster" has to be its own gesture: the
    health glow is warm and still, the magnet is blips arriving, and one smooth whoosh on its own
    is player_dodge.

    It drops off about one kill in a hundred, so it may sit above the swarm, but every layer is
    lowpassed - an open whoosh is a hiss to Nyquist (.ai/audio-direction.md, "The ceiling")."""
    first = _whoosh(0.16, 650, 1800, 2.6, seed=401, curve=0.7)
    second = _whoosh(0.12, 950, 2500, 2.6, seed=409, curve=0.6)
    grains = []
    for i, semi in enumerate([7, 12, 16, 19, 24]):
        dur = 0.12
        f = _hz(semi)
        g = S.osc("tri", S.sweep(dur, f, f * 1.03, 1.0), dur)
        grains.append((S.env_mul(g, S.ad(dur, 0.004, 2.6)), 0.032 * i, 0.75 - 0.08 * i))
    shimmer = S.tremolo(S.lowpass(S.layer(grains), 4200, 0.707), 23.0, 0.45)
    out = S.layer([
        (first, 0.0, 0.85),
        (second, 0.085, 0.8),
        (shimmer, 0.13, 0.7),
    ])
    return S.dc_block(S.reverb(out, size=0.55, mix=0.2))


def event_reward():
    """A side event paid out a chest (SideEvent.Succeed), and the chest rises out of the ground as
    a ghost before it turns solid. Phantasmal, not triumphant.

    A breath that swells - noise through two slow formant bands, the sound of a voice that is not
    quite there - and resolves into one glass tone with a slow vibrato and a fifth under it. Not a
    bell run (level_up, chest_open, stage_clear are all bell runs) and not a stacked chord
    (meta_unlock, which still plays when an event grants an unlock instead, since that is the save
    changing). One a run, so it sits in the once-a-minute band, not with the stingers."""
    dur = 1.45
    n = S.n_samples(dur)
    noise = S.osc("noise", 0, dur, seed=419)
    low = S.bandpass(noise, S.sweep(dur, 480, 820, 1.0), 3.2)
    high = S.bandpass(S.osc("noise", 0, dur, seed=421), S.sweep(dur, 1250, 1900, 1.0), 4.0)
    breath = S.lowpass(S.mix(low, S.gain(high, 0.55)), 3000, 0.707)
    na = S.n_samples(0.62)
    env = S.ramp(na, 0.0, 1.0, 2.2) + S.decay(n - na, 1.6)
    breath = S.env_mul(breath, env)

    tone_dur = 0.95
    tn = S.n_samples(tone_dur)
    base = _hz(19)
    vib = [base * (1.0 + 0.007 * math.sin(2.0 * math.pi * 5.2 * i / S.SR)) for i in xrange(tn)]
    glass = S.env_mul(S.osc("sine", vib, tone_dur), S.ad(tone_dur, 0.05, 2.0))
    under = S.env_mul(S.osc("sine", _hz(12), tone_dur), S.ad(tone_dur, 0.09, 2.4))
    tone = S.lowpass(S.mix(glass, S.gain(under, 0.5)), 3800, 0.707)
    out = S.layer([
        (breath, 0.0, 0.8),
        (tone, 0.5, 0.75),
    ])
    return S.dc_block(S.reverb(out, size=0.85, mix=0.38))


def bearing_found():
    """A wayfinder taken (WayfinderPickup.Take): the Arcane Compass, the Gaoler's map or the
    bearing cairn. From then on every place the chapter hides is marked at the edge of the screen
    (.ai/side-events.md, "Finding the event").

    A needle swinging and settling: five dry ticks, each later and a little lower than the last -
    a pivot losing speed - and then one open fifth, low and soft, that holds while the arrows come
    up. Not a bell run (level_up, chest_open and stage_clear are bell runs), not the breath of
    event_reward, and the ticks DECELERATE where pickup_magnet's blips accelerate, which is the
    whole difference between "something found its bearing" and "something is being pulled in".
    Once a run, so it may have a tail; every layer is lowpassed (the ceiling, audio-direction 6)."""
    ticks = []
    t = 0.0
    gap = 0.045
    for i in range(5):
        dur = 0.05
        n = S.n_samples(dur)
        click = S.lowpass(S.bandpass(S.osc("noise", 0, dur, seed=431 + i), 1400 - 100 * i, 2.2), 2400, 0.707)
        body = S.osc("tri", S.sweep(dur, 980 - 60 * i, 760 - 50 * i, 0.6), dur)
        # The triangle's odd harmonics run to Nyquist; the lowpass after the mix is what keeps the
        # ticks a wooden click and not a hiss (audio-direction 6, "The ceiling").
        tick = S.lowpass(S.mix(S.env_mul(click, S.decay(n, 10.0)), S.gain(S.env_mul(body, S.decay(n, 7.0)), 0.5)), 2800, 0.707)
        ticks.append((tick, t, 0.8 - 0.08 * i))
        t += gap
        gap *= 1.45          # slowing down: the needle coming to rest
    settle = t + 0.04

    hold = 1.0
    hn = S.n_samples(hold)
    root = S.env_mul(S.osc("sine", _hz(-5), hold), S.ad(hold, 0.08, 2.2))
    fifth = S.env_mul(S.osc("sine", _hz(2), hold), S.ad(hold, 0.12, 2.5))
    shimmer = S.env_mul(S.osc("tri", _hz(14), hold), S.ad(hold, 0.2, 3.2))
    chord = S.lowpass(S.mix(root, S.gain(fifth, 0.7), S.gain(shimmer, 0.18)), 2600, 0.707)
    out = S.layer([
        (S.layer(ticks), 0.0, 0.9),
        (chord, settle, 0.8),
    ])
    return S.dc_block(S.reverb(out, size=0.7, mix=0.28))


def level_up():
    """A4 - C5 - E5 - A5, the A-minor triad the score sits in. The one unmissable stinger."""
    out = _arp([0, 3, 7, 12], 0.085, 0.7, seed=191)
    return S.dc_block(S.reverb(out, size=0.7, mix=0.3))


def chest_open():
    dur = 1.1
    n = S.n_samples(dur)
    creak = S.bandpass(S.osc("mnoise", S.sweep(dur * 0.4, 60, 150, 1.0), dur * 0.4, seed=193),
                       S.sweep(dur * 0.4, 700, 1500, 1.0), 5.0)
    reveal = _arp([0, 7, 12, 16], 0.07, 0.75, seed=197)
    out = S.layer([
        (S.env_mul(creak, S.ad(dur * 0.4, 0.05, 1.4)), 0.0, 0.55),
        (reveal, 0.30, 0.9),
    ])
    return S.dc_block(S.reverb(out, size=0.75, mix=0.3))


def card_appear():
    """One per card dealt in LevelUpMenu / ChestItemSelectionMenu. Three fire in sequence,
    so it has to survive being heard three times in 200 ms.

    Pure high-band flick with no pitched component at all - it used to carry a tone and
    measured as a small ui_open. The menu opening and a card landing inside it must not be
    the same gesture, because they play four hundred milliseconds apart."""
    dur = 0.18
    # Kept high on purpose - it is the one UI sound allowed above 4 kHz, because below that it
    # measures as ui_close. It is lowpassed at 7 kHz rather than left open, and plays three
    # times per level-up, not three times a second.
    paper = S.bandpass(S.osc("noise", 0, dur, seed=199), S.sweep(dur, 1800, 4600, 1.4), 3.4)
    paper = S.lowpass(S.highpass(paper, 1400, 0.7), 7000, 0.707)
    return S.env_mul(paper, S.ad(dur, 0.012, 4.0))


def card_select():
    dur = 0.55
    out = S.layer([
        (_bell(_hz(3), dur, seed=211), 0.0, 0.8),
        (_bell(_hz(10), dur * 0.9, seed=223), 0.05, 0.7),
    ])
    return S.dc_block(S.reverb(out, mix=0.24))


def reroll():
    """A riffle: five irregular ticks, each a little higher. Reads as "these are different"."""
    items = []
    for i in xrange(5):
        t = _tick(0.09, 560 + i * 110, 2400, seed=227 + i)
        items.append((t, 0.035 * i + 0.004 * (i % 2), 0.9 - 0.08 * i))
    return S.dc_block(S.layer(items))


def spell_evolve():
    """Spell evolution - rarer than a level-up, so it is allowed to be longer and brighter.

    Swell first, bells last. Led by the arpeggio it was a longer level_up, and those two fire
    within seconds of each other at the moment a spell evolves; leading with a rising arcane
    shimmer makes the ear hear a transformation with a result, not a second level."""
    dur = 1.4
    n = S.n_samples(dur)
    shimmer = S.tremolo(P.body("arcane", 2.7, seed=233), 11.0, 0.45)
    shimmer = S.env_mul(shimmer, S.ramp(len(shimmer), 0.15, 1.0, 1.6))
    bells = _arp([12, 19, 24], 0.09, 0.9, seed=229)
    out = S.layer([(shimmer, 0.0, 0.9), (bells, 0.62, 1.0)])
    return S.dc_block(S.reverb(out, size=0.8, mix=0.34))


# ---------------------------------------------------------------------------
# interface
# ---------------------------------------------------------------------------

def ui_hover():
    """Plays on every mouse move across a button. Must be nearly subliminal."""
    return _tick(0.05, 1000, 3000, seed=239)


def ui_click():
    dur = 0.11
    n = S.n_samples(dur)
    t = _tick(dur, 800, 2600, seed=241)
    body = S.env_mul(S.osc("tri", S.sweep(dur, 620, 480, 0.6), dur), S.decay(n, 7.0))
    return S.mix(t, S.gain(body, 0.5))


def ui_back():
    """The same gesture as ui_click, a fourth lower. Direction is the whole message."""
    dur = 0.12
    n = S.n_samples(dur)
    t = _tick(dur, 560, 1900, seed=251)
    body = S.env_mul(S.osc("tri", S.sweep(dur, 420, 330, 0.6), dur), S.decay(n, 7.0))
    return S.mix(t, S.gain(body, 0.5))


def ui_denied():
    """Locked stage, unaffordable upgrade. A dull buzz, never a musical interval."""
    dur = 0.24
    n = S.n_samples(dur)
    buzz = S.osc("square", 118, dur)
    buzz = S.lowpass(buzz, 900, 1.2)
    return S.env_mul(buzz, S.ad(dur, 0.006, 2.0, hold=0.09))


def ui_open():
    dur = 0.35
    return S.mix(S.gain(_whoosh(dur, 500, 2200, 2.0, seed=257), 0.7),
                 S.gain(_bell(_hz(7), dur, seed=263), 0.35))


def ui_close():
    dur = 0.3
    return S.mix(S.gain(_whoosh(dur, 2200, 500, 2.0, seed=269), 0.7),
                 S.gain(_bell(_hz(0), dur * 0.8, seed=271), 0.3))


def ui_pause():
    dur = 0.3
    n = S.n_samples(dur)
    drop = S.osc("tri", S.sweep(dur, 520, 260, 1.0), dur)
    return S.dc_block(S.env_mul(drop, S.ad(dur, 0.01, 2.4)))


# ---------------------------------------------------------------------------
# run and meta stingers
# ---------------------------------------------------------------------------

def stage_clear():
    out = _arp([0, 4, 7, 12, 16, 19], 0.11, 1.3, seed=277)
    return S.dc_block(S.reverb(out, size=0.85, mix=0.34))


def game_over():
    """Descending, unresolved, and long enough to sit under the GameOverScreen appearing."""
    dur = 2.2
    out = _arp([-12, -15, -17, -22], 0.30, 1.6, seed=281)
    drone = S.env_mul(S.osc("saw", S.sweep(dur, 55, 51, 1.0), dur), S.ad(dur, 0.4, 1.2))
    drone = S.lowpass(drone, 700, 1.4)
    out = S.layer([(out, 0.0, 1.0), (drone, 0.0, 0.45)])
    return S.dc_block(S.reverb(out, size=0.9, damp=0.35, mix=0.36))


def meta_unlock():
    """A character or stage unlocked on the save file - the one sound that means the SAVE
    changed, not the run.

    Built from sustained stacked fifths rather than the bell arpeggio every other reward
    uses. stage_clear and this fire minutes apart at the end of a winning run, and as two
    ascending bell runs they measured 0.03 apart; a swelling chord against a struck one is
    the difference between "you finished" and "you earned something"."""
    dur = 1.7
    n = S.n_samples(dur)
    voices = []
    for k, semi in enumerate([-12, -5, 0, 7, 12]):
        v = S.osc("tri", _hz(semi), dur)
        voices.append(S.gain(S.env_mul(v, S.ad(dur, 0.22 + 0.06 * k, 1.1)), 0.9 / (1 + k * 0.4)))
    chord = S.lowpass(S.mix(*voices), S.sweep(dur, 1200, 4200, 0.8), 0.9)
    cap = _bell(_hz(24), 0.9, seed=283)
    out = S.layer([(chord, 0.0, 1.0), (cap, 0.55, 0.5)])
    return S.dc_block(S.reverb(out, size=0.85, mix=0.36))


def wave_warning():
    """A wave or boss inbound. Low, slow, and deliberately musical so it is not a hit."""
    dur = 1.3
    n = S.n_samples(dur)
    horn = S.mix(S.gain(S.osc("saw", _hz(-24), dur), 0.6), S.gain(S.osc("saw", _hz(-17), dur), 0.4))
    horn = S.lowpass(horn, S.sweep(dur, 600, 1400, 1.0), 1.6)
    return S.dc_block(S.reverb(S.env_mul(horn, S.ad(dur, 0.25, 1.4)), size=0.8, mix=0.3))


# ---------------------------------------------------------------------------
# registry
# ---------------------------------------------------------------------------
# (filename stem, category, peak target in dBFS, builder, loops)
#
# The peak targets are the mix. They are not arbitrary - see the loudness table in
# .ai/audio-direction.md, which explains why the XP orb is 20 dB under the boss.

# Casts sat at -9 and impacts at -13, both above their own band in the loudness table. The
# persistent pulses (Solar Flare, Toxic Spore, Cinderbreath) now cue every pulse, which put more
# casts in a minute than the table was drawn for; both come down to where the table puts them.
CAST_DB = -11.0
IMPACT_DB = -15.0

REGISTRY = []

for _el in P.ORDER:
    REGISTRY.append(("cast_%s" % _el, "cast", CAST_DB, _make_cast(_el), False))
for _el in P.ORDER:
    REGISTRY.append(("impact_%s" % _el, "impact", IMPACT_DB, _make_impact(_el), False))

REGISTRY += [
    ("spell_explosion",     "spell",  -5.0,  spell_explosion,     False),
    ("spell_beam_start",    "spell",  -11.0, spell_beam_start,    False),
    ("spell_beam_loop",     "spell",  -14.0, spell_beam_loop,     True),
    ("spell_beam_end",      "spell",  -12.0, spell_beam_end,      False),
    ("spell_orbit",         "spell",  -22.0, spell_orbit,         False),
    ("spell_summon",        "spell",  -9.0,  spell_summon,        False),
    ("spell_bow_draw",      "spell",  -15.0, spell_bow_draw,      False),
    ("spell_bow_release",   "spell",  -10.0, spell_bow_release,   False),

    ("player_hurt",         "player", -6.0,  player_hurt,         False),
    ("player_dodge",        "player", -13.0, player_dodge,        False),
    ("player_heal",         "player", -11.0, player_heal,         False),
    ("player_shield_absorb", "player", -12.0, player_shield_absorb, False),
    ("player_shield_break", "player", -8.0,  player_shield_break,  False),
    ("player_death",        "player", -4.0,  player_death,        False),
    ("player_low_health",   "player", -14.0, player_low_health,   False),

    ("enemy_hurt_a",        "enemy",  -19.0, enemy_hurt_a,        False),
    ("enemy_hurt_b",        "enemy",  -19.0, enemy_hurt_b,        False),
    ("enemy_hurt_c",        "enemy",  -19.0, enemy_hurt_c,        False),
    ("enemy_death_small",   "enemy",  -14.0, enemy_death_small,   False),
    ("enemy_death_heavy",   "enemy",  -11.0, enemy_death_heavy,   False),
    ("enemy_shoot",         "enemy",  -14.0, enemy_shoot,         False),
    ("enemy_melee",         "enemy",  -12.0, enemy_melee,         False),
    ("elite_spawn",         "enemy",  -8.0,  elite_spawn,         False),
    ("boss_roar",           "enemy",  -4.0,  boss_roar,           False),
    ("boss_death",          "enemy",  -3.0,  boss_death,          False),

    ("pickup_xp",           "pickup", -23.0, pickup_xp,           False),
    ("pickup_health",       "pickup", -13.0, pickup_health,       False),
    ("pickup_magnet",       "pickup", -13.0, pickup_magnet,       False),
    ("pickup_haste",        "pickup", -12.0, pickup_haste,        False),
    ("event_reward",        "pickup", -8.0,  event_reward,        False),
    ("bearing_found",       "pickup", -8.0,  bearing_found,       False),
    ("level_up",            "pickup", -5.0,  level_up,            False),
    ("chest_open",          "pickup", -6.0,  chest_open,          False),
    ("card_appear",         "ui",     -15.0, card_appear,         False),
    ("card_select",         "ui",     -9.0,  card_select,         False),
    ("reroll",              "ui",     -12.0, reroll,              False),
    ("spell_evolve",        "pickup", -4.0,  spell_evolve,        False),

    ("ui_hover",            "ui",     -26.0, ui_hover,            False),
    ("ui_click",            "ui",     -16.0, ui_click,            False),
    ("ui_back",             "ui",     -16.0, ui_back,             False),
    ("ui_denied",           "ui",     -15.0, ui_denied,           False),
    ("ui_open",             "ui",     -14.0, ui_open,             False),
    ("ui_close",            "ui",     -15.0, ui_close,            False),
    ("ui_pause",            "ui",     -12.0, ui_pause,            False),

    ("stage_clear",         "meta",   -4.0,  stage_clear,         False),
    ("game_over",           "meta",   -5.0,  game_over,           False),
    ("meta_unlock",         "meta",   -4.0,  meta_unlock,         False),
    ("wave_warning",        "meta",   -7.0,  wave_warning,        False),
]
