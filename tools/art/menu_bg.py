# -*- coding: utf-8 -*-
"""The main menu background. Run: python tools/art/menu_bg.py

Authored 360x640 and rendered at x2 to fill the 720x1280 viewport exactly, the same rule the
title screen and every other Bonelight asset follows.

It is deliberately NOT a second title screen. The title is a *scene* - a wizard, a forest, a
summoning circle, warm gold against cold indigo - and it earns that because you look at it once
per session with nothing else on screen. The menu is looked at constantly, always with buttons and
text on top of it, so a second illustration would compete with the UI and make the title's one
moment ordinary.

So this is a field rather than a picture: no subject, no horizon, no silhouette. Pure geometry -
an abstracted sigil, a ley lattice, drifting motes - carried by arcane cyan and violet, which is
the register the title screen does not use. Everything is held dark and low-contrast in the middle
third, because that is where the buttons sit.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

W, H = 360, 640
M = bl.MATERIALS
E = bl.ELEMENTS
OCC = bl.OCC

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "bonelight", "ui", "menu-background.png")

# The sigil sits high so its brightest ring clears the button stack rather than sitting behind it.
# Checked against a real screenshot, not reasoned about: at 232 it crowded the primary button.
CX, CY = W // 2, 188


def ground(c):
    """A vertical wash from near-black at the crown to a faintly warmer floor."""
    c.vramp(0, H, [
        OCC,
        bl.tone("stone", "deep"),
        bl.tone("stone", "shade"),
        bl.tone("stone", "deep"),
        OCC,
    ])


def sigil(c):
    """Concentric rings, abstracted from the title's summoning circle into pure geometry.

    Drawn as rings rather than a filled disc so the middle stays dark enough to read text over.
    """
    for radius, tone, thick in (
        (150, bl.tone("arcane", "deep"), 1),
        (118, bl.tone("arcane", "shade"), 1),
        (74, bl.tone("arcane", "shade"), 2),
        (46, bl.tone("arcane", "base"), 1),
    ):
        c.ring(CX, CY, radius, radius, tone, thick)

    # Twelve spokes, one per element. Short, so they read as tick marks on a dial rather than as
    # a wheel - a full wheel would draw the eye to the centre, which is where the UI goes.
    for i in range(12):
        a = math.pi * 2.0 * i / 12.0 - math.pi / 2.0
        x0, y0 = CX + math.cos(a) * 120, CY + math.sin(a) * 120
        x1, y1 = CX + math.cos(a) * 148, CY + math.sin(a) * 148
        c.line(x0, y0, x1, y1, bl.tone("arcane", "shade"))

    # A soft core. The only place in the frame that gets a lit tone.
    c.radial(CX, CY, 40, 40, [
        bl.tone("arcane", "base"),
        bl.tone("arcane", "shade"),
        bl.tone("arcane", "deep"),
        None,
    ])


def lattice(c, seed=7):
    """Faint diagonal ley lines. Sparse and dithered so they suggest structure without becoming
    a pattern the eye starts reading as content."""
    rng = random.Random(seed)
    for i in range(14):
        x = rng.randrange(-H, W + H)
        tone = bl.tone("stone", "shade") if i % 3 else bl.tone("arcane", "deep")
        for step in range(0, H, 2):
            # Every other pixel, so the line is a suggestion rather than a rule.
            if (step // 2) % 3 == 0:
                continue
            c.set(x + step, step, tone)


def motes(c, seed=3):
    """Scattered single pixels. Density falls off away from the sigil, so the field has a centre
    of gravity without needing a second light source."""
    rng = random.Random(seed)
    for _ in range(520):
        x = rng.randrange(0, W)
        y = rng.randrange(0, H)
        d = math.sqrt((x - CX) ** 2 + (y - CY) ** 2) / float(H)
        if rng.random() < d * 1.15:
            continue
        roll = rng.random()
        if roll < 0.10:
            c.set(x, y, bl.tone("arcane", "lit"))
        elif roll < 0.40:
            c.set(x, y, bl.tone("arcane", "base"))
        else:
            c.set(x, y, bl.tone("arcane", "deep"))


def main():
    c = raster.Canvas(W, H, OCC)
    ground(c)
    lattice(c)
    sigil(c)
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
