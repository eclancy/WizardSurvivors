# -*- coding: utf-8 -*-
"""Animation builders shared by the enemy family.

Frames are derived from one base pose rather than hand-typed, which is what makes 22 frames
per enemy tractable. That is a real trade: these read as competent shambles, not as hand-drawn
animation, and hand-tuning the key frames is the obvious next refinement.

Every builder preserves the anchor (feet on cell-2) except where a beat is meant to break it -
the hurt recoil and the death collapse - because those are the two moments a body genuinely
leaves the ground.
"""
import pixel as P

# Tone-shift tables. Walking a character one step down its ramp is how a frame is darkened
# without leaving the palette - no new colours are ever introduced by an animation.
DARKER = {"V": "v", "v": "m", "m": "u", "u": "U", "U": "o",
          "S": "s", "s": "n", "n": "k", "k": "K", "K": "o",
          "J": "j", "j": "i", "i": "h", "h": "H", "H": "o",
          "R": "r", "r": "t", "t": "T", "T": "o",
          "Z": "Y", "Y": "y", "y": "o"}
# Inverting DARKER naively makes "o" a key - several ramps bottom out there - and the shared
# occlusion line would brighten into whichever ramp happened to be last. Occlusion is a single
# shared value in the contract and must never be remapped, so it is excluded from both ends.
BRIGHTER = dict((v, k) for k, v in DARKER.items() if v != "o" and k != "o")


def _map(rows, table, times=1):
    for _ in range(times):
        rows = ["".join(table.get(c, c) for c in r) for r in rows]
    return rows


def _sway(rows, cell, from_row, dx):
    """Shift only the rows below from_row - a cheap shamble without a full leg rig."""
    out = []
    for y, r in enumerate(rows):
        if y < from_row or dx == 0:
            out.append(r)
        elif dx > 0:
            out.append(("." * dx + r)[:cell])
        else:
            out.append((r[-dx:] + "." * (-dx))[:cell].ljust(cell, "."))
    return out


def walk(base, cell, leg_row=24, bob=(0, -1, -1, 0, 0, -1, -1, 0),
         sway=(0, 1, 1, 0, 0, -1, -1, 0)):
    """Eight-frame shamble: the body lifts, the legs swing under it, the anchor holds."""
    base = P.pad(base, cell, cell)
    out = []
    for dy, dx in zip(bob, sway):
        g = _sway(base, cell, leg_row, dx)
        # Bob the upper body only, so the feet stay where the contract puts them.
        upper = [r if y < leg_row else "." * cell for y, r in enumerate(g)]
        lower = [r if y >= leg_row else "." * cell for y, r in enumerate(g)]
        g = P.overlay(lower, P.shift(upper, 0, dy, cell, cell), cell, cell)
        out.append(g)
    return out


def attack(base, cell, facing=1, frames=6):
    """Wind up back, then throw the body forward. Eyes flare on the release."""
    base = P.pad(base, cell, cell)
    script = [(-1, 0), (-1, 0), (0, 0), (2 * facing, 1), (3 * facing, 1), (1 * facing, 0)]
    out = []
    for dx, flare in script[:frames]:
        g = P.shift(base, dx, 0, cell, cell)
        if flare:
            g = _map(g, BRIGHTER)
        out.append(g)
    return out


def hurt(base, cell, facing=1):
    """Two beats: blanched and knocked back, then settling. Deliberately breaks the anchor."""
    base = P.pad(base, cell, cell)
    return [
        P.shift(_map(base, BRIGHTER, 2), -2 * facing, -1, cell, cell),
        P.shift(_map(base, BRIGHTER, 1), -1 * facing, 0, cell, cell),
    ]


def death(base, cell, frames=6):
    """Six beats: buckle, sink, and darken down the ramp until only a stain is left."""
    base = P.pad(base, cell, cell)
    out = []
    for i in range(frames):
        dy = (0, 1, 2, 4, 6, 8)[i]
        dark = (0, 0, 1, 1, 2, 3)[i]
        g = P.shift(base, 0, dy, cell, cell)
        g = _map(g, DARKER, dark)
        if i >= 4:
            # Dissolve rather than thin: keying off x alone leaves vertical stripes that read
            # as a rendering glitch. A hash of both axes scatters the survivors instead.
            keep = 2 if i == 4 else 3
            g = ["".join(c if ((x * 5 + y * 3) % (keep + 1)) else "."
                         for x, c in enumerate(r)) for y, r in enumerate(g)]
        out.append(g)
    return out
