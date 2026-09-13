# -*- coding: utf-8 -*-
"""The main menu background. Run: python tools/art/menu_bg.py

Authored 360x640 and rendered at x2 to fill the 720x1280 viewport exactly, the same rule the title
screen and every other Bonelight asset follows.

IT IS DELIBERATELY NOT A SECOND TITLE SCREEN. The title is a *scene* - a wizard, a forest, a
summoning circle - and it earns that because you look at it once per session with nothing else on
it. The menu is looked at constantly, always with buttons and text on top, so a second
illustration would compete with the UI and make the title's one moment ordinary.

So this is a PLACE rather than a picture: the wall you are standing in front of. No subject, no
horizon, no silhouette.

WHY IT WAS REDRAWN. The first version was an abstracted sigil on a ley lattice in arcane cyan -
geometry floating in a void. Against the carved-stone buttons that now sit on it, it read as a
sci-fi HUD: thin bright curves, no material, no weight, nothing that could be *carved*. The brief
is medieval and arcane, and the arcane half was doing all the work.

This is masonry. Coursed blocks with mortar between them, lit from the upper left like everything
else in the game, with a circle cut INTO the wall rather than drawn on top of it - the groove is
occlusion with arcane light sitting in the bottom of it, which is what makes it read as carved
instead of as a decal. The ring sits high so its brightest arc clears the button stack; the lower
third is held dark and quiet because that is where the buttons live and they need somewhere to sit.

The arcane light is still here and still the only cold light in the frame. It is just coming out of
something now.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

W, H = 360, 640
E = bl.ELEMENTS
OCC = bl.OCC

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "ui", "menu-background.png")

# The circle sits high so its brightest arc clears the button stack rather than sitting behind it.
# Checked against a real screenshot, not reasoned about: at 232 it crowded the primary button.
CX, CY = W // 2, 190
RING_R = 118

COURSE = 26          # block height, including the mortar line under it
BLOCK_W = 58         # nominal block width; jittered per block so no two courses align


def ground(c):
    """A vertical wash: the wall falls away into the dark at the bottom of the frame."""
    c.vramp(0, H, [
        bl.tone("stone", "shade"),
        bl.tone("stone", "shade"),
        bl.tone("stone", "deep"),
        bl.tone("stone", "deep"),
        OCC,
    ])


def masonry(c, seed=17):
    """Coursed stone. Mortar in occlusion, each block given its own tone and its own lit top edge.

    The two rules that make this read as a wall rather than as a grid:

      COURSES ARE OFFSET. Every row starts at a different phase, so vertical joints never line up
      between rows. Aligned joints read as tiling - which is exactly what it would be.

      EVERY BLOCK IS LIT ON TOP AND LEFT. Same key as every sprite in the game. A grid of
      rectangles with no bevel is a chessboard; one pixel of light on two edges makes it stone.
    """
    rng = random.Random(seed)
    for course, y in enumerate(range(-COURSE, H + COURSE, COURSE)):
        # Depth: blocks toward the bottom of the frame sit in shadow, so the wall recedes.
        depth = max(0.0, min(1.0, y / float(H)))
        x = -rng.randrange(0, BLOCK_W)
        while x < W:
            w = BLOCK_W + rng.randrange(-16, 17)
            x0, x1 = x, min(W - 1, x + w - 2)
            y0, y1 = y, min(H - 1, y + COURSE - 2)
            # The last course runs past the bottom of the canvas, so y0 can exceed y1 once the
            # clamp above has bitten. Skip it rather than ask for an empty random range.
            if y1 >= 0 and x1 > x0 and y1 >= max(0, y0):
                roll = rng.random()
                if depth > 0.72:
                    face = OCC if roll < 0.5 else bl.tone("stone", "deep")
                elif depth > 0.42:
                    face = bl.tone("stone", "deep") if roll < 0.62 else bl.tone("stone", "shade")
                else:
                    face = bl.tone("stone", "shade") if roll < 0.68 else bl.tone("stone", "base")
                c.rect(max(0, x0), max(0, y0), x1, y1, face)

                # Lit top and left edge, shaded bottom - the key light, at the scale of a wall.
                if depth < 0.80:
                    lip = bl.tone("stone", "base") if depth < 0.45 else bl.tone("stone", "shade")
                    if y0 >= 0:
                        c.hline(max(0, x0), x1, y0, lip)
                    if x0 >= 0:
                        c.vline(x0, max(0, y0), y1, lip)
                c.hline(max(0, x0), x1, y1, OCC)

                # Pitting, so a block face is not a flat rectangle. Sparse: this is dressed stone,
                # not rubble.
                for _ in range(rng.randrange(0, 5)):
                    px = rng.randrange(max(0, x0), x1 + 1)
                    py = rng.randrange(max(0, y0), y1 + 1)
                    c.set(px, py, OCC if rng.random() < 0.6 else bl.tone("stone", "deep"))

            # The mortar joint to the right of this block.
            c.vline(min(W - 1, x + w - 1), max(0, y), min(H - 1, y + COURSE - 1), OCC)
            x += w


def carved_ring(c):
    """The circle cut into the wall: a groove with cold light standing in the bottom of it.

    Three passes, and the order is the whole trick. An occlusion groove first, so there is a hole.
    Then the light INSIDE the groove, one ring in from its outer edge, so the glow is coming from
    within the cut rather than sitting on the surface. Then a lit lip on the upper-left arc only,
    because that is the edge the key would catch on a channel chiselled into a wall.
    """
    # The groove: wide and dark. A thin bright circle is a drawn line; a wide dark channel with
    # light only where light could actually collect is a cut.
    c.ring(CX, CY, RING_R, RING_R, OCC, thick=9)
    c.ring(CX, CY, RING_R - 34, RING_R - 34, OCC, thick=7)

    # THE LIGHT POOLS IN THE LOWER ARC ONLY. Key is upper left, so the far wall of the channel -
    # the bottom-right of the circle - is the part facing the source, and the near wall is in its
    # own shadow. Lighting the whole ring evenly is what made the first pass read as neon tube
    # rather than as something glowing inside a groove.
    #
    # Cold `arcane` from the MATERIAL rows, not the element ramp. The element ramp is violet and
    # belongs to spell effects and the mark in an enemy's eye; a whole wall of it made the menu
    # read as lit by magic rather than as a dark room with a little magic in it.
    glow_mid = bl.tone("arcane", "base")
    glow_deep = bl.tone("arcane", "shade")
    for deg in range(0, 360):
        a = math.radians(deg)
        # 1 at the bottom-right of the circle, 0 at the top-left.
        facing = (math.sin(a) + math.cos(a - math.pi / 4.0)) * 0.5
        if facing <= 0.05:
            continue
        for (radius, thick) in ((RING_R - 3, 3), (RING_R - 36, 2)):
            for t in range(thick):
                x = int(CX + math.cos(a) * (radius - t))
                y = int(CY + math.sin(a) * (radius - t))
                c.set(x, y, glow_mid if facing > 0.62 and t == 0 else glow_deep)

    # The chiselled lip, upper-left arc only - the edge the key catches.
    for deg in range(150, 330):
        a = math.radians(deg)
        c.set(int(CX + math.cos(a) * (RING_R + 5)), int(CY + math.sin(a) * (RING_R + 5)),
              bl.tone("stone", "base"))
        c.set(int(CX + math.cos(a) * (RING_R + 6)), int(CY + math.sin(a) * (RING_R + 6)),
              bl.tone("stone", "shade"))

    # Radial glyph strokes between the two rings. Irregular lengths and an irregular gap, because
    # evenly spaced marks at even lengths read as a dial rather than as writing. Cut, not lit: they
    # are chisel marks with only an occasional catch of light in them.
    rng = random.Random(5)
    deg = 0
    while deg < 360:
        a = math.radians(deg)
        inner = RING_R - 30
        outer = inner + rng.randrange(6, 22)
        for r in range(inner, outer):
            x = int(CX + math.cos(a) * r)
            y = int(CY + math.sin(a) * r)
            c.set(x, y, OCC)
            if rng.random() < 0.22:
                c.set(x, y - 1, glow_deep)
        deg += rng.randrange(11, 26)

    # The hollow at the centre: the deepest cut, and the only place the light gets bright. Small,
    # because it is the one thing on the screen allowed to be bright and it is competing with
    # nothing - a large one would pull the eye off the buttons.
    c.radial(CX, CY, 18, 18, [
        bl.tone("arcane", "lit"),
        bl.tone("arcane", "base"),
        bl.tone("arcane", "shade"),
        bl.tone("stone", "deep"),
        None,
    ])


def motes(c, seed=3):
    """Dust in the air, thickest where the light is. Nothing moves on this screen, so the motes are
    what keep it from looking like a flat asset."""
    rng = random.Random(seed)
    for _ in range(220):
        x = rng.randrange(0, W)
        y = rng.randrange(0, H)
        d = math.hypot(x - CX, y - CY) / float(RING_R + 90)
        if rng.random() > max(0.06, 1.0 - d):
            continue
        c.set(x, y, bl.tone("arcane", "shade") if rng.random() < 0.7 else bl.tone("stone", "base"))


def main():
    c = raster.Canvas(W, H, OCC)
    ground(c)
    masonry(c)
    carved_ring(c)
    motes(c)
    # Pushes the frame edges toward black so centred UI has somewhere quiet to sit.
    c.vignette([None, bl.tone("stone", "deep"), OCC], power=1.35)

    bad = c.audit()
    if bad:
        print("OFF-CONTRACT COLOURS (art-direction.md section 3):")
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return 1

    out_dir = os.path.dirname(OUT)
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    c.scaled(2).save(OUT)
    print("wrote %s at %dx%d" % (os.path.relpath(OUT, ROOT).replace(os.sep, "/"), W * 2, H * 2))
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
