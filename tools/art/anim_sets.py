# -*- coding: utf-8 -*-
"""Derive the full animation contract from one drawn pose.

`.ai/art-direction.md` section 5 asks every actor for `moving` 8 + `attack` 6 + `hurt` 2 +
`death` 6, and the player for moving/hurt/death. Across the roster that is about 260 frames,
which is not a thing to hand-place - and hand-placing them is also the wrong shape of work,
because 260 independently drawn frames drift. Deriving them from the pose keeps every frame of a
sprite made of the same pixels as every other, which is what makes a cheap animation read as one
object moving rather than as a flicker book.

WHAT DERIVATION CAN AND CANNOT DO, stated honestly. These are transforms of a standing pose:
shifts, shears, squashes and steps along a ramp. They buy weight, timing and readable intent.
They do not buy a new silhouette - a windup that raises a weapon the pose is not holding cannot
come out of this file, and a death that falls apart into pieces cannot either. Where a sprite
needs that, it needs drawing, and this gives it something honest to ship in the meantime.

THE ONE RULE EVERY TRANSFORM OBEYS: a pixel may only move along ITS OWN material ramp. Stepping
`linen.base` toward white gives `linen.lit`, never `#FFFFFF`. That is what keeps a flinch flash
and a death fade inside the contract's colours instead of inventing tones between them, which on
a fixed palette is the whole game.
"""

# Every ramp in the roster palette, lightest to darkest. Explicit rather than derived from the
# palette dict, because the dict is keyed by character and has no order in it - and because a
# wrong ordering here would silently make flinches darken and deaths brighten.
RAMPS = [
    "TtrRq",      # steel
    "LleEA",      # linen
    "Ddcba",      # wool
    "Ssknm",      # skin / bone
    "Ffhi I".replace(" ", ""),   # flesh
    "HGgu",       # gold
    "Vvwx X".replace(" ", ""),   # violet
    "PpNjJ",      # stone
    "Q1234",      # ember
    "U5678",      # azure
    "O!@#$",      # amber
    "C%^&*",      # verdant
    "+-=~",       # fire
    "<>?/",       # ice
    "[];:",       # lightning
    "{}|\\",      # earth
    "ZYyz",       # arcane
    "()_",        # poison
]

_POS = {}
for _r in RAMPS:
    for _i, _c in enumerate(_r):
        _POS[_c] = (_r, _i)


def step(rows, n):
    """Move every pixel n stops along its own ramp. Positive darkens, negative lightens.

    Clamped at both ends rather than wrapping: a ramp that wraps turns a shadow into a highlight
    at the bottom of a death fade, which reads as the sprite flashing just as it should be going
    out.
    """
    out = []
    for line in rows:
        buf = []
        for ch in line:
            if ch in _POS:
                ramp, i = _POS[ch]
                buf.append(ramp[max(0, min(len(ramp) - 1, i + n))])
            else:
                buf.append(ch)
        out.append("".join(buf))
    return out


def to_occ(rows, amount):
    """Push `amount` (0..1) of the figure into occlusion, from the top down.

    Used by `death`: the sprite does not fade uniformly, it goes out from the head, which reads
    as collapsing rather than as dissolving.
    """
    cell = len(rows)
    cut = int(cell * amount)
    out = []
    for y, line in enumerate(rows):
        if y < cut:
            out.append("".join("o" if ch != "." else "." for ch in line))
        else:
            out.append(line)
    return out


def shift(rows, dx, dy):
    cell = len(rows)
    blank = "." * cell
    out = []
    for line in rows:
        if dx > 0:
            line = blank[:dx] + line[:-dx] if dx < cell else blank
        elif dx < 0:
            line = line[-dx:] + blank[:-dx]
        out.append(line)
    if dy > 0:
        out = [blank] * dy + out[:-dy]
    elif dy < 0:
        out = out[-dy:] + [blank] * -dy
    return out


def shear(rows, amount, pivot=None):
    """Lean the figure: each row moves sideways in proportion to its height above `pivot`.

    This is what a windup and a lunge are made of. A whole-sprite shift slides; a shear leans,
    and leaning is the difference between a thing moving and a thing being moved.
    """
    cell = len(rows)
    pivot = cell - 2 if pivot is None else pivot
    blank = "." * cell
    out = []
    for y, line in enumerate(rows):
        dx = int(round(amount * (pivot - y) / float(max(1, pivot))))
        if dx > 0:
            line = blank[:dx] + line[:-dx] if dx < cell else blank
        elif dx < 0:
            line = line[-dx:] + blank[:-dx]
        out.append(line)
    return out


def squash(rows, factor, baseline=None):
    """Compress vertically toward the baseline. factor 1.0 is untouched, 0.5 is half height."""
    cell = len(rows)
    baseline = cell - 2 if baseline is None else baseline
    blank = "." * cell
    out = [blank] * cell
    for y in range(cell):
        src = int(round(baseline - (baseline - y) / float(max(0.05, factor))))
        if 0 <= src < cell:
            merged = []
            for a, b in zip(out[y], rows[src]):
                merged.append(b if b != "." else a)
            out[y] = "".join(merged)
    return out


# --- the four animations ----------------------------------------------------------------------

def moving(pose, lean=1.0):
    """Eight frames. A walk is weight passing from one side to the other, so the figure bobs on
    a two-beat while it leans on a four-beat - the two cycles going in and out of phase is what
    stops eight frames reading as four frames played twice."""
    BOB = [0, -1, -2, -1, 0, -1, -2, -1]
    LEAN = [0.0, 0.5, 1.0, 0.5, 0.0, -0.5, -1.0, -0.5]
    out = []
    for i in range(8):
        f = shift(pose, 0, BOB[i])
        if LEAN[i]:
            f = shear(f, LEAN[i] * lean)
        out.append(f)
    return out


def attack(pose, reach=3.0):
    """Six frames: gather, gather, STRIKE, strike, recover, recover.

    The windup leans AWAY and squashes slightly - a thing gathering itself gets shorter - and the
    strike leans hard into the blow and stretches. The asymmetry is the point: two slow frames
    back and one fast frame forward is what makes six frames feel like a hit rather than a sway.
    """
    out = []
    out.append(shear(squash(pose, 0.97), -reach * 0.30))
    out.append(shear(squash(pose, 0.93), -reach * 0.55))
    out.append(shear(squash(pose, 1.04), reach * 1.00))
    out.append(shear(squash(pose, 1.02), reach * 0.85))
    out.append(shear(pose, reach * 0.35))
    out.append(pose)
    return out


def hurt(pose, knock=2):
    """Two frames. A flinch is a shove plus a flash, and the flash has to be a step along each
    material's own ramp rather than a wash of white - a fixed palette has no white to wash with."""
    return [
        step(shift(shear(pose, -knock * 0.8), -knock, 0), -2),
        step(shift(shear(pose, -knock * 0.4), -knock // 2, 0), -1),
    ]


def death(pose):
    """Six frames. Buckle, fold, collapse, and go out from the head down.

    It darkens AND squashes at once. Darkening alone reads as the sprite fading out, which is a
    screen effect; squashing alone reads as it being stood on. Together they read as something
    losing the ability to hold itself up, which is what a death is.
    """
    out = []
    out.append(step(squash(pose, 0.94), 1))
    out.append(step(shear(squash(pose, 0.82), 1.0), 1))
    out.append(step(shear(squash(pose, 0.62), 2.0), 2))
    out.append(to_occ(step(shear(squash(pose, 0.42), 3.0), 3), 0.35))
    out.append(to_occ(step(shear(squash(pose, 0.26), 3.5), 3), 0.62))
    out.append(to_occ(step(shear(squash(pose, 0.16), 4.0), 4), 0.85))
    return out


def full_set(pose, lean=1.0, reach=3.0, knock=2):
    """The contract's four animations from one pose, as (name, frames, loop, speed)."""
    return [
        ("moving", moving(pose, lean), True, 10.0),
        ("attack", attack(pose, reach), False, 12.0),
        ("hurt", hurt(pose, knock), False, 12.0),
        ("death", death(pose), False, 10.0),
    ]


def player_set(pose, idle_frames):
    """The player has no `attack` - spells fire on a timer, so there is nothing to swing."""
    return [
        ("idle", idle_frames, True, 6.0),
        ("moving", moving(pose, 1.2), True, 10.0),
        ("hurt", hurt(pose, 2), False, 12.0),
        ("death", death(pose), False, 10.0),
    ]
