# -*- coding: utf-8 -*-
"""XP orbs: three value tiers, 16x16, four frames of breath each.

The orb this replaces was `assets/organized/level/props/...blue-crystal4.png` - a world prop
cast as a pickup, which `.ai/art-direction.md` lists as a Don't by name. Before that it was
`assets/Experience_Orb.png`, a 150x150 gem squeezed to 0.2 scale whose glyph was a HEALTH CROSS
and whose four colours were all at alpha 153. The bright green was the right instinct and the
only part worth keeping: on a Bonelight floor - deep blue-greys, everything unlit - a saturated
yellow-green is about the most separable thing the palette offers.

So this is that green, drawn properly: on the grid, on the contract's `grass` ramp, at full
alpha, at the size it actually ships.

THREE TIERS, AND THEY DIFFER IN BOTH SIZE AND HUE. Size alone does not survive a crowded screen
- two orbs at different distances read as different sizes already - and hue alone does not
survive being small. Together they are legible at a glance:

    small   r4   grass   #9BD455   the common drop
    medium  r5   arcane  #C08CFF   worth stopping for
    large   r6   light   #FFF0A8   worth crossing the screen for

The escalation is deliberate: green is the floor of the game's colour language, violet is the
magic the dark wizard took, and gold-white is the same light the ward and the player's own orb
are drawn in. A large orb looks like a piece of what you are trying to get back.

WHAT THE FRAMES DO, AND WHAT THE CODE MUST STOP DOING. `XPOrb.cs` currently rotates the sprite
(`SpinSpeed`) and scales it by +-6% (`PulseStrength`). Both are continuous transforms on a 16px
pixel-art sprite rendered at x2 with nearest filtering, which is precisely the sub-pixel crawl
the whole art direction exists to prevent - the orb shimmers and its edge boils. The breath
belongs in the frames instead: the core steps one pixel and the specular walks around it, which
reads as the same "this is alive" signal and costs no filtering.
"""
import os

import bonelight as B
import pixel as P

CELL = 16

# (name, radius, element ramp, how many satellite sparks)
TIERS = [
    ("small", 4, "grass", 0),
    ("medium", 5, "arcane", 2),
    ("large", 6, "light", 3),
]

# Sparks orbit at these angles, one step per frame. Hand-placed rather than trigonometric so
# every one lands on a whole pixel: a spark computed with sin/cos rounds to a different pixel on
# different frames and reads as noise rather than as one thing moving.
SPARKS = [(-1, -1), (1, -1), (1, 1), (-1, 1)]


def _palette(elem):
    """One palette per tier. `o` is the seat, the four ramp stops are the orb.

    Element ramps are passed as literal hex rather than as (row, role) pairs: `build_palette`
    resolves those against MATERIALS only, and the element ramps are four stops with their own
    naming (core / hot / mid / edge) rather than the five material roles.
    """
    ramp = B.ELEMENTS[elem]
    return B.build_palette({
        "o": B.OCC,
        "A": ramp[0], "B": ramp[1], "C": ramp[2], "D": ramp[3],
    })


class _G(object):
    def __init__(self):
        self.g = [["."] * CELL for _ in range(CELL)]

    def set(self, x, y, ch):
        if 0 <= x < CELL and 0 <= y < CELL and ch != ".":
            self.g[y][x] = ch

    def disc(self, cx, cy, r, ch):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.4:
                    self.set(x, y, ch)

    def rows(self):
        return ["".join(r) for r in self.g]


def _orb(radius, sparks, frame):
    """One frame. Centre is a half-pixel so the disc comes out symmetric on an even cell."""
    g = _G()
    cx = cy = (CELL - 1) / 2.0
    # Breath: the body holds its size, the CORE steps. Growing the body would change the
    # silhouette every 150ms, which at pickup sizes reads as flicker rather than as light.
    core = 1.0 + (0.6 if frame in (1, 2) else 0.0)

    g.disc(cx, cy, radius + 0.7, "o")        # the seat, so it reads on a lit floor too
    g.disc(cx, cy, radius, "D")
    g.disc(cx, cy, radius - 0.9, "C")
    g.disc(cx, cy, radius - 2.0, "B")
    g.disc(cx, cy, core, "A")
    # Specular: one pixel, upper left, because the key light in this project comes from there
    # and a pickup that lights itself from a different direction than everything else reads as
    # a sticker on the floor.
    g.set(int(cx - radius * 0.45), int(cy - radius * 0.55), "A")

    # Satellites, one step per frame around the same ring. These are what make a big orb read
    # as MORE rather than just as nearer.
    for i in range(sparks):
        dx, dy = SPARKS[(frame + i * 2) % len(SPARKS)]
        sx = int(round(cx + dx * (radius + 2)))
        sy = int(round(cy + dy * (radius + 2)))
        g.set(sx, sy, "B" if i == 0 else "C")
    return g.rows()


def frames_for(radius, sparks):
    return [_orb(radius, sparks, f) for f in range(4)]


ANIMATIONS = [(name, (lambda r=r, s=s: frames_for(r, s))) for name, r, elem, s in TIERS]


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_pickups")
    if not os.path.isdir(out):
        os.makedirs(out)
    sets = []
    for name, radius, elem, sparks in TIERS:
        fr = frames_for(radius, sparks)
        pal = _palette(elem)
        P.write_strip(fr, pal, os.path.join(out, "xp-%s.png" % name), CELL)
        sets.append((name, fr))
        print("xp-%-7s r%d  %-8s %d frames" % (name, radius, elem, len(fr)))
    # one contact sheet, all three tiers, so the size ladder can be judged at once
    P.preview(sets, _palette("grass"), os.path.join(out, "_tiers.png"), zoom=12)
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
