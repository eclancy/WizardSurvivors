# -*- coding: utf-8 -*-
"""Elderbark, the Treant: a 96x96 boss, dark and macabre.

Run: python tools/art/sprite_treant.py     -> tools/art/_treant/ (gitignored)

Replaces the orc sheet at x6, which `.ai/art-direction.md` calls the worst grid offender in the
project and which its own Don't list forbids twice over - a boss may not be a scaled basic enemy,
and 6x a 32px cell is not a boss, it is a big orc. It also unblocks cutting the orc art: while
ForestTreantBoss.tscn pointed at OrcEnemyFrames, removing the orc took a boss with it.

WHAT IT IS, from world-and-tone.md rather than from taste. The horde is the land taken, and a
treant is the largest single piece of land there is to take - so this is not a monster that lives
in the forest, it is the forest with something else looking out of it. That decides every
choice below:

  IT WEARS THE WOOD'S OWN MATERIALS. stone for bark and lichen for moss, both from the deep half
  of their ramps, because the contract's enemy rule is that nothing the dark wizard holds gets to
  be brightly lit. There is no wood row in the palette and there does not need to be one - bark
  reads from VERTICAL STRIATION, not from hue.

  THE FACE IS A HOLLOW, not a carved expression. A knot in the trunk that happens to be a socket,
  with the same cold arcane mark every taken thing carries. Two pixels. On a 96px figure that
  restraint is what makes it read as something looking OUT of the tree rather than as a tree with
  a face painted on.

  THE MACABRE IS STRUCTURAL. Bone caught in the bark and grown over, roots that end in fingers,
  a ribcage hollow where the trunk splits. Nothing bleeding, nothing rotting - the world doc is
  explicit that this fiction is cold and occupied rather than gory, so the horror is that the
  tree has been eating and the evidence is load-bearing.

DENSITY. Section 4b asks for 0.80 internal edges per filled pixel and a 96x96 cell is the hardest
place to hit it - a big silhouette has a lot of interior to fill and nothing is free. Bark
striation does most of the work here, which is the honest reason bark was chosen as the texture:
it is vertical lines, and vertical lines are boundaries.
"""
import os

import bonelight as B
import pixel as P
import roster

CELL = 96
FEET = CELL - 2
CX = 47.5

# The roster palette plus lichen, which it has no need of and this does. Single characters, and
# that is not a style choice: the grid holds ONE character per cell, so a key like "lichen_base"
# writes eleven and the row comes out wider than the cell. Third time that trap has been stepped
# in - "E2" and the player pigment rows were the others.
PALETTE = dict(roster.PALETTE)
PALETTE.update(B.build_palette({
    "0": ("lichen", "lit"), "9": ("lichen", "base"), ",": ("lichen", "shade"),
}))
G = roster.G


def _bark(g, y0, y1, hw0, hw1, cx=CX, lean=0.0):
    """The trunk: a tapering mass with vertical grain running the whole way down it.

    The grain is the density. Five columns of alternating shade and deep, placed at irregular
    offsets - evenly spaced grain is a fence, and a fence is the stripe failure section 4b warns
    about wearing a different texture.
    """
    # THE BODY TONE IS stone.base, NOT stone.shade. The first pass drew this trunk in `j` and
    # `J` - #141A26 and #0B0F18 - which sit within a few points of occ #05070C, and the whole
    # boss came out as a silhouette with nothing visible inside it. Section 4c of the contract
    # says deep is the direction and invisible is not, and this is the sprite that proved it.
    # The dark stops are still here; they are the GRAIN and the shadow edge, not the body.
    GRAIN = (-9.5, -5.0, -1.5, 3.0, 7.5, 11.0)
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        c = cx + lean * t * t
        lo, hi = int(round(c - hw)), int(round(c + hw))
        g.span(y, lo, hi, "N")
        g.set(lo, y, "P")
        g.set(lo + 1, y, "p")
        g.set(hi, y, "J")
        g.set(hi - 1, y, "j")
        for i, off in enumerate(GRAIN):
            gx = c + off
            if lo + 2 < gx < hi - 2:
                # the grain wanders: a straight line is a plank, a wavering one is a tree
                wob = 1 if (y + i * 3) % 7 == 0 else 0
                g.set(gx + wob, y, "j" if (y + i) % 3 else "p")


def _limb(g, x0, y0, x1, y1, thick, taper=True):
    """A branch. Thins as it goes, and every branch ends in something that could grab."""
    n = int(max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(n + 1):
        t = i / float(max(1, n))
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        w = thick * (1.0 - 0.65 * t) if taper else thick
        g.span(y, x - w, x + w, "N")
        g.set(x - w, y, "p")
        g.set(x + w, y, "j")


def _claw(g, x, y, sgn, size=4):
    """Three twigs off a branch end, uneven. A tree that has been taken grabs."""
    for i, (ax, ay) in enumerate(((sgn * size, -size), (sgn * size * 1.3, 1),
                                  (sgn * size * 0.7, size))):
        _limb(g, x, y, x + ax, y + ay, 1.0)
        g.set(x + ax, y + ay, "p")


def _moss(g, spots):
    """Lichen, hand-placed. It grows where water sits - one side, in patches, never a trim."""
    for (mx, my, mw, mh) in spots:
        for y in range(my, my + mh):
            for x in range(mx, mx + mw):
                if (x * 7 + y * 13) % 5 < 3 and g.get(x, y) in ("j", "N", "J", "p", "P"):
                    g.set(x, y, "9" if (x + y) % 3 else "0")


def _bone(g, spots):
    """Caught in the bark and grown over. The evidence, and the only warm-ish tone on the
    figure - which is why there are five pixels of it and not fifty."""
    for (bx, by, bw) in spots:
        g.span(by, bx, bx + bw, "n")
        g.set(bx, by, "k")
        g.set(bx + bw, by + 1, "o")


def treant():
    """A boss silhouette is MASS, and the first pass drew a web.

    Three things were wrong once the tones read. The face hollow was a nine-pixel wedge that ate
    the middle of the trunk, so the boss was a dark hole with bark around it. The vertical split
    ran two thirds of the height at four pixels wide, which cut the mass in half again. And the
    branches were thin - a dozen of them at three pixels, converging, which at 96px reads as
    grass rather than as limbs.

    Fewer branches, much thicker. A smaller socket high on the trunk. A narrower split low down.
    The trunk has to survive being filled solid and still say "treant" - that is the section 4
    test, and a web fails it.
    """
    g = G(CELL)

    # ROOTS: four, heavy, splayed. The silhouette starts at the ground.
    for (rx, ry, tx, ty, th) in ((32, 76, 10, 93, 7.0), (40, 78, 24, 94, 6.0),
                                 (56, 78, 72, 94, 6.5), (62, 76, 86, 92, 5.5)):
        _limb(g, rx, ry, tx, ty, th)
    for (fx, fy, sgn) in ((11, 93, -1), (25, 94, -1), (71, 94, 1), (85, 92, 1)):
        _claw(g, fx, fy, sgn, 4)

    # TRUNK: columnar and wide. Immense does not taper.
    _bark(g, 22, 82, 18.0, 22.0)

    # SHOULDER MASS, so the arms come out of a body rather than out of a pole
    _bark(g, 26, 40, 24.0, 20.0)

    # ARMS: two, thick, asymmetric, each ending in a grasping hand. Two heavy limbs read as a
    # boss; eight thin ones read as undergrowth.
    _limb(g, 28, 34, 6, 22, 8.0)
    _claw(g, 7, 22, -1, 7)
    _limb(g, 68, 32, 90, 20, 8.5)
    _claw(g, 89, 21, 1, 7)
    # one lower limb, dragging
    _limb(g, 66, 52, 84, 64, 5.0)
    _claw(g, 83, 65, 1, 5)

    # CROWN: four broken stubs, not a canopy. Snapped off rather than grown.
    for (bx, by, tx2, ty2, th2) in ((36, 24, 26, 8, 5.0), (46, 22, 44, 4, 5.5),
                                    (56, 24, 68, 10, 4.5), (52, 23, 58, 12, 3.5)):
        _limb(g, bx, by, tx2, ty2, th2)
    for (cx2, cy2, sg) in ((26, 8, -1), (44, 4, 1), (68, 10, 1)):
        _claw(g, cx2, cy2, sg, 5)

    # THE SPLIT: narrow, low, and it is where the ribs are. Two pixels wide is a wound in a
    # trunk; five is a trunk in two pieces.
    for y in range(58, 80):
        w = 1.2 + 1.4 * (1.0 - abs(y - 69) / 11.0)
        g.span(y, CX - w, CX + w, "o")
    for ry in range(60, 78, 4):
        g.span(ry, CX - 6.0, CX - 2.5, "n")
        g.span(ry, CX + 2.5, CX + 6.0, "n")
        g.set(CX - 6.0, ry, "k")
        g.set(CX + 6.0, ry, "m")
        g.span(ry + 1, CX - 5.5, CX + 5.5, "o")

    # THE FACE: a socket, high on the trunk, small enough that the eyes are the subject.
    for y in range(38, 48):
        t = (y - 38) / 9.0
        w = 6.5 - 2.0 * t
        g.span(y, CX - w, CX + w, "o")
    g.span(37, CX - 7.0, CX + 6.0, "j")          # the brow, overhanging
    g.span(36, CX - 5.0, CX + 4.0, "P")
    g.set(CX - 7.0, 37, "p")

    _moss(g, [(26, 56, 10, 12), (22, 72, 8, 7), (62, 44, 8, 10),
              (68, 66, 9, 8), (34, 26, 7, 6), (58, 28, 6, 5)])
    _bone(g, [(28, 64, 5), (60, 70, 4), (30, 48, 4), (64, 54, 4), (38, 80, 6), (56, 84, 5)])

    g.span(FEET + 1, 8, 88, "o")
    g.set(CX - 3, 42, "Z")
    g.set(CX + 3, 42, "Y")
    return g.rows()


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_treant")
    if not os.path.isdir(out):
        os.makedirs(out)
    pose = treant()
    filled, edges, ratio = roster.density(pose)
    P.write_strip([pose], PALETTE, os.path.join(out, "treant-pose.png"), CELL)
    print("treant %dx%d  %d px  %d edges  %.2f edges/px%s"
          % (CELL, CELL, filled, edges, ratio, "" if ratio >= 0.80 else "   SPARSE"))
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
