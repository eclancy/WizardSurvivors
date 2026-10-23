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
import math

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

# Water and grass, for the PLAYER CAST ONLY: Tituba's wave and Vainamoinen's reeds and seed.
# Without them both staff heads stayed full-bright through a hurt flash and through every frame
# of a death while the rest of the figure went out around them.
#
# Not in RAMPS, because a ramp is keyed by character and characters are not global: the treant
# reuses "0", "9" and "," as LICHEN (sprite_treant.PALETTE), and stepping those along the water
# and grass ramps turned its lichen water-blue as it died. A palette that redefines a character
# has to bring its own ramp for it; these two are only safe on the roster palette.
PLAYER_RAMPS = RAMPS + [
    "09KM",       # water
    "B,'`",       # grass
    # darkness - John Dee's mirror. roster.DARKNESS_RAMP: four control characters, because the
    # roster palette has no printable character left.
    "\x01\x02\x03\x04",
]


def _positions(ramps):
    pos = {}
    for r in ramps:
        for i, c in enumerate(r):
            pos[c] = (r, i)
    return pos


_POS = _positions(RAMPS)
_PLAYER_POS = _positions(PLAYER_RAMPS)


def step(rows, n, pos=None):
    """Move every pixel n stops along its own ramp. Positive darkens, negative lightens.

    Clamped at both ends rather than wrapping: a ramp that wraps turns a shadow into a highlight
    at the bottom of a death fade, which reads as the sprite flashing just as it should be going
    out.
    """
    pos = _POS if pos is None else pos
    out = []
    for line in rows:
        buf = []
        for ch in line:
            if ch in pos:
                ramp, i = pos[ch]
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


def hurt(pose, knock=2, pos=None):
    """Two frames. A flinch is a shove plus a flash, and the flash has to be a step along each
    material's own ramp rather than a wash of white - a fixed palette has no white to wash with."""
    return [
        step(shift(shear(pose, -knock * 0.8), -knock, 0), -2, pos),
        step(shift(shear(pose, -knock * 0.4), -knock // 2, 0), -1, pos),
    ]


def death(pose, pos=None):
    """Six frames. Buckle, fold, collapse, and go out from the head down.

    It darkens AND squashes at once. Darkening alone reads as the sprite fading out, which is a
    screen effect; squashing alone reads as it being stood on. Together they read as something
    losing the ability to hold itself up, which is what a death is.
    """
    out = []
    out.append(step(squash(pose, 0.94), 1, pos))
    out.append(step(shear(squash(pose, 0.82), 1.0), 1, pos))
    out.append(step(shear(squash(pose, 0.62), 2.0), 2, pos))
    out.append(to_occ(step(shear(squash(pose, 0.42), 3.0), 3, pos), 0.35))
    out.append(to_occ(step(shear(squash(pose, 0.26), 3.5), 3, pos), 0.62))
    out.append(to_occ(step(shear(squash(pose, 0.16), 4.0), 4, pos), 0.85))
    return out


def full_set(pose, lean=1.0, reach=3.0, knock=2):
    """The contract's four animations from one pose, as (name, frames, loop, speed)."""
    return [
        ("moving", moving(pose, lean), True, 10.0),
        ("attack", attack(pose, reach), False, 12.0),
        ("hurt", hurt(pose, knock), False, 12.0),
        ("death", death(pose), False, 10.0),
    ]


# How far each wizard's body rises at the passing position, in rows. The two striders carry a
# full two-row bob; the robed and the braced carry one, because a heavy or stately walk is
# mostly NOT going up and down - that is what makes it read as heavy or stately.
WALK_BOB = {"pyromancer": 2, "stormcaller": 2, "vainamoinen": 2,
            "frostweaver": 1, "geomancer": 1, "tituba": 1}

# The waist. Everything above this row is the same drawing in every frame and only rises with
# the bob; everything below it is redrawn per frame by the pose function. It is the same row the
# idle breathes from (roster.IDLE_ANCHOR), so idle and walk hinge the figure at one place.
WALK_ANCHOR = 38

# Vainamoinen's staff has a binding on row 38 itself, and a bob duplicates the anchor row - two
# rows of lift made that one binding a three-row block. Two rows lower the seam falls on bare reed.
WALK_ANCHORS = {"vainamoinen": 40}


def walk(pose_fn, bob=2, anchor=WALK_ANCHOR):
    """Eight frames of a REAL walk: the pose function redraws the lower body at each phase.

    `moving` above bobs and shears one frozen pose. On an enemy that ships, because a shambling
    thing gliding is half the read; on the player it does not, because a figure drawn mid-stride
    then slides across the ground with its legs locked, and a robed one slides like a chess
    piece. So a wizard's pose function takes a stride phase (0..1) and draws its legs, boots,
    hem or skirt for that moment, keeping everything above the waist identical - it is the same
    person in every frame, only walking.

    Phase 0 and 0.5 are the two contacts (the near foot forward, then the far one); 0.25 and
    0.75 are the passing positions, where the swinging foot is off the ground and the body is at
    its highest. The bob lifts only the rows above `anchor`, exactly as the idle does, so the
    feet and the contact shadow never leave the floor (contract section 5). The staff head is
    drawn as it is in the pose - no pulse - so it holds steady while the body moves under it.
    """
    out = []
    for i in range(8):
        t = i / 8.0
        rows = pose_fn(t)
        # Highest at passing (i = 2, 6), on the floor at contact (i = 0, 4).
        lift = int(round(bob * abs(math.sin(2.0 * math.pi * t))))
        if lift:
            rows = [rows[min(y + lift, anchor)] if y < anchor else rows[y]
                    for y in range(len(rows))]
        out.append(rows)
    return out


def player_set(pose, idle_frames, pose_fn=None):
    """The player has no `attack` - spells fire on a timer, so there is nothing to swing.

    With `pose_fn`, `moving` is a real walk drawn from the pose function's stride phases. Without
    one it falls back to the derived bob-and-lean, which is what every pose without legs to move
    still gets.
    """
    if pose_fn is not None:
        name = pose_fn.__name__
        mv = walk(pose_fn, WALK_BOB.get(name, 1), WALK_ANCHORS.get(name, WALK_ANCHOR))
    else:
        mv = moving(pose, 1.2)
    return [
        ("idle", idle_frames, True, 6.0),
        ("moving", mv, True, 10.0),
        ("hurt", hurt(pose, 2, _PLAYER_POS), False, 12.0),
        ("death", death(pose, _PLAYER_POS), False, 10.0),
    ]
