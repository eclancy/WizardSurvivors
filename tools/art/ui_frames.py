# -*- coding: utf-8 -*-
"""The menu furniture: carved stone and iron, plus vellum for the book screens.

Run: python tools/art/ui_frames.py

Everything is authored at half size and rendered at x2, the rule every other Bonelight asset
follows, so a UI pixel is the same size as a pixel in the sprites beside it. A UI drawn at 1:1
next to art drawn at 2:1 is the clearest single way to make a screen look assembled rather than
designed.

TWO REGISTERS, AND A RULE THAT BINDS THEM.

  CARVED STONE AND IRON is the default, and it covers almost everything: the main menu, character
  and stage select, the pause menu, game over, the chest pick. It extends what levelup_frames.py
  already established for the level-up screen, so that screen stops being the one good-looking
  outlier and becomes the style guide.

  ILLUMINATED MANUSCRIPT is reserved, and it is for the screens where the player is reading ABOUT
  magic rather than pressing a button: the mutation and ascension choices, the spellbook, any
  future codex. Vellum, ink rules, gold-leaf corners.

  The rule: MANUSCRIPT IS ALWAYS INSIDE STONE. Frame, chrome and buttons are stone and iron on
  every screen; vellum only ever appears as the page WITHIN that frame. So opening the spellbook
  reads as opening a book, not as changing application. Without that rule this is two games.

WHY VELLUM IS THE `flesh` RAMP. It is already in the contract - #F2DCC4 through #3C2436 - and its
light end is exactly aged parchment. No new material row, no amendment to section 3 needed, which
is the difference between a style that fits the palette and one that fights it.

NINE-SLICE, AND WHAT THAT FORBIDS. Every frame here is stretched in its middle band and never in
its corners, so the centre of each is a single flat tone on purpose: anything textured there
smears into a vertical or horizontal streak the moment the control is wider than it was drawn.
All ornament lives in the corner blocks. This is the same constraint levelup-card.png documents,
and it is the one that catches people out.

Light model per art-direction.md section 2: key from the upper left. Every raised edge is lit on
its top and left and shaded on its bottom and right; every recessed one is the exact inverse. That
single rule is what makes iron read as a bevel rather than as a coloured outline, and it is what
tells a pressed button from a resting one without changing its colour.
"""

import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import raster

OCC = bl.OCC
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "ui")

# Buttons: 40x40 authored, 10px border -> 80x80 at x2 with 20px nine-slice margins.
BTN = 40
BTN_MARGIN = 10

# Panels are bigger because they carry a heavier frame and deeper corner blocks: 48x48 authored,
# 12px border -> 96x96 at x2 with 24px margins.
PANEL = 48
PANEL_MARGIN = 12


def _bevel(c, x0, y0, x1, y1, lit, mid, dark, thickness=1, raised=True):
    """A rectangular bevel, `thickness` pixels deep, lit from the upper left.

    `raised=False` swaps the two sides, which is the entire difference between a button at rest
    and a button being pressed - and is why a pressed state needs no new colour.
    """
    top, bottom = (lit, dark) if raised else (dark, lit)
    for i in range(thickness):
        near = top if i == 0 else mid
        far = bottom if i == 0 else mid
        c.hline(x0 + i, x1 - i, y0 + i, near)
        c.vline(x0 + i, y0 + i, y1 - i, near)
        c.hline(x0 + i, x1 - i, y1 - i, far)
        c.vline(x1 - i, y0 + i, y1 - i, far)


def _corner(x, y, size, margin):
    """True if (x, y) is inside one of the four corner blocks - the ONLY part of a nine-slice that
    is never stretched.

    The first version of this file tested for the centre square instead, which let grain into the
    edge bands. Those are stretched along one axis, so every speck in them came out as a streak
    the length of the control. An edge band may only contain detail that is CONSTANT along the
    axis it stretches on - which in practice means lines parallel to the edge, and nothing else.
    """
    return (x < margin or x >= size - margin) and (y < margin or y >= size - margin)


def _rivets(c, inset, size, colour, shadow):
    """Four iron rivets, one per corner block. Nine-slice never stretches a corner, so this is the
    one place a fixed-size detail can live and keep its shape at any control size."""
    last = size - 1
    for (cx, cy) in ((inset, inset), (last - inset, inset),
                     (inset, last - inset), (last - inset, last - inset)):
        c.disc(cx, cy, 1, 1, colour)
        c.set(cx, cy - 1, bl.tone("steel", "hi"))
        c.set(cx + 1, cy + 1, shadow)


def stone_button(state="normal"):
    """An iron-bound stone button. Four states, one geometry.

    The states differ only in light, never in shape or size: `hover` raises the iron and gold one
    step up their ramps, `pressed` inverts the bevel so the face sits below its frame, `disabled`
    drops the gold entirely and flattens the bevel. A state that changed the button's size would
    make the row it sits in twitch as the pointer crossed it.
    """
    c = raster.Canvas(BTN, BTN, None)
    last = BTN - 1

    lit_state = state == "hover"
    pressed = state == "pressed"
    dead = state == "disabled"

    face = bl.tone("stone", "shade" if lit_state else "deep")
    iron_hi = bl.tone("steel", "lit" if lit_state else "base")
    iron_mid = bl.tone("steel", "base" if lit_state else "shade")
    iron_lo = bl.tone("steel", "shade" if lit_state else "deep")

    if dead:
        face = bl.tone("stone", "deep")
        iron_hi = bl.tone("stone", "base")
        iron_mid = bl.tone("stone", "shade")
        iron_lo = bl.tone("stone", "deep")

    # Face first; every edge below is drawn over it.
    c.rect(0, 0, last, last, face)

    # Outer occlusion so the button separates from whatever it sits on.
    c.rect(0, 0, last, 0, OCC)
    c.rect(0, last, last, last, OCC)
    c.rect(0, 0, 0, last, OCC)
    c.rect(last, 0, last, last, OCC)

    # The iron band, two pixels, bevelled.
    _bevel(c, 1, 1, last - 1, last - 1, iron_hi, iron_mid, iron_lo, 2, raised=not pressed)

    # A thin gold inlay inside the iron - the one warm note, and the thing that makes this read as
    # a made object rather than a slab. Dropped entirely when disabled, because a greyed-out
    # control should lose its jewellery, not just its contrast.
    if not dead:
        gold_top = bl.tone("gold", "hi" if lit_state else "lit")
        gold_bot = bl.tone("gold", "base" if lit_state else "shade")
        top, bot = (gold_bot, gold_top) if pressed else (gold_top, gold_bot)
        c.hline(3, last - 3, 3, top)
        c.vline(3, 3, last - 3, top)
        c.hline(3, last - 3, last - 3, bot)
        c.vline(last - 3, 3, last - 3, bot)

    # Inner shadow: the face is recessed behind the inlay.
    inner = bl.tone("stone", "shade") if pressed else OCC
    c.hline(4, last - 4, 4, inner)
    c.vline(4, 4, last - 4, inner)
    c.hline(4, last - 4, last - 4, bl.tone("stone", "shade") if not pressed else OCC)
    c.vline(last - 4, 4, last - 4, bl.tone("stone", "shade") if not pressed else OCC)

    if not dead:
        _rivets(c, 6, BTN, iron_hi, OCC)

    return c


def stone_panel(inset=False):
    """The slab a group of controls sits on.

    `inset` inverts the bevel and darkens the face, which turns the same frame into a recess -
    for a scrolling list, where the content should read as sunk into the panel rather than
    floating on it.
    """
    c = raster.Canvas(PANEL, PANEL, None)
    last = PANEL - 1
    rng = random.Random(7 if not inset else 8)

    face = bl.tone("stone", "deep" if inset else "shade")
    c.rect(0, 0, last, last, face)

    # Quarried grain. Confined to the corner blocks and the border bands - the centre stays flat
    # because nine-slice stretches it, and stretched grain is a streak.
    for _ in range(320):
        x = rng.randrange(0, PANEL)
        y = rng.randrange(0, PANEL)
        if not _corner(x, y, PANEL, PANEL_MARGIN):
            continue
        toward_dark = (x / float(PANEL) + y / float(PANEL)) * 0.5
        if rng.random() < toward_dark:
            c.set(x, y, bl.tone("stone", "deep"))
        elif rng.random() < 0.4:
            c.set(x, y, bl.tone("stone", "base" if not inset else "shade"))

    c.rect(0, 0, last, 0, OCC)
    c.rect(0, last, last, last, OCC)
    c.rect(0, 0, 0, last, OCC)
    c.rect(last, 0, last, last, OCC)

    _bevel(c, 1, 1, last - 1, last - 1,
           bl.tone("stone", "hi"), bl.tone("stone", "lit"), bl.tone("stone", "deep"),
           2, raised=not inset)

    # Quarried corner blocks. All the panel's weight lives here because this is the only region a
    # nine-slice keeps at its authored size, whatever the control grows to.
    for (cx, cy) in ((2, 2), (last - 9, 2), (2, last - 9), (last - 9, last - 9)):
        c.rect(cx, cy, cx + 7, cy + 7, bl.tone("stone", "base" if not inset else "shade"))
        c.hline(cx, cx + 7, cy, bl.tone("stone", "hi"))
        c.vline(cx, cy, cy + 7, bl.tone("stone", "hi"))
        c.hline(cx, cx + 7, cy + 7, OCC)
        c.vline(cx + 7, cy, cy + 7, OCC)

    _rivets(c, 5, PANEL, bl.tone("steel", "lit"), OCC)
    return c


def vellum_page(lit=False):
    """A page of the wizard's own book: aged parchment, an ink rule, gold-leaf corners.

    This is the reserved register - spellbook, mutations, codex - and it is deliberately the only
    LIGHT surface in the game. That is the point: every other screen is stone in the dark, so a
    page of vellum reads as something brought out and opened. It is also why it never forms the
    outer frame of a screen; a bright edge-to-edge rectangle would fight every sprite on screen.
    """
    c = raster.Canvas(PANEL, PANEL, None)
    last = PANEL - 1
    rng = random.Random(21)

    page = bl.tone("flesh", "hi")
    shade = bl.tone("flesh", "lit")
    ink = bl.tone("flesh", "deep")

    c.rect(0, 0, last, last, page)

    # Foxing and fibre, kept out of the stretched centre band for the usual reason.
    for _ in range(260):
        x = rng.randrange(0, PANEL)
        y = rng.randrange(0, PANEL)
        if not _corner(x, y, PANEL, PANEL_MARGIN):
            continue
        if rng.random() < 0.35:
            c.set(x, y, shade)

    # The edge is torn and dark, not cut: parchment browns where it has been handled.
    c.rect(0, 0, last, 0, ink)
    c.rect(0, last, last, last, ink)
    c.rect(0, 0, 0, last, ink)
    c.rect(last, 0, last, last, ink)
    c.hline(1, last - 1, 1, bl.tone("flesh", "base"))
    c.vline(1, 1, last - 1, bl.tone("flesh", "base"))

    # The scribe's rule: two ink lines inside the margin, the inner one thinner.
    c.hline(4, last - 4, 4, ink)
    c.vline(4, 4, last - 4, ink)
    c.hline(4, last - 4, last - 4, ink)
    c.vline(last - 4, 4, last - 4, ink)
    c.hline(6, last - 6, 6, bl.tone("flesh", "base"))
    c.hline(6, last - 6, last - 6, bl.tone("flesh", "base"))

    # Gold leaf at the corners. Four small illuminated marks, brighter when the page is the active
    # choice - the same "catching more light" idea the level-up card uses for hover.
    leaf = bl.tone("gold", "hi" if lit else "lit")
    leaf_dark = bl.tone("gold", "base" if lit else "shade")
    for (cx, cy, dx, dy) in ((6, 6, 1, 1), (last - 6, 6, -1, 1),
                             (6, last - 6, 1, -1), (last - 6, last - 6, -1, -1)):
        c.set(cx, cy, leaf)
        c.set(cx + dx, cy, leaf)
        c.set(cx, cy + dy, leaf)
        c.set(cx + dx * 2, cy, leaf_dark)
        c.set(cx, cy + dy * 2, leaf_dark)
        c.set(cx + dx, cy + dy, leaf_dark)

    return c


def divider():
    """A thin iron rule, for separating sections inside a panel. Nine-sliced horizontally only, so
    it is authored wide enough that its ends keep their caps."""
    c = raster.Canvas(24, 6, None)
    c.hline(0, 23, 1, bl.tone("steel", "base"))
    c.hline(0, 23, 2, bl.tone("steel", "shade"))
    c.hline(0, 23, 3, OCC)
    for x in (0, 1, 22, 23):
        c.set(x, 1, bl.tone("steel", "lit"))
    c.set(11, 1, bl.tone("gold", "lit"))
    c.set(12, 1, bl.tone("gold", "lit"))
    c.set(11, 2, bl.tone("gold", "shade"))
    c.set(12, 2, bl.tone("gold", "shade"))
    return c


def write(canvas, name):
    bad = canvas.audit()
    if bad:
        print("OFF-CONTRACT COLOURS in %s (art-direction.md section 3):" % name)
        for h, n in bad[:10]:
            print("   %s  x%d" % (h, n))
        return False

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    path = os.path.join(OUT_DIR, name)
    canvas.scaled(2).save(path)
    print("  %-26s %dx%d" % (name, canvas.w * 2, canvas.h * 2))
    return True


def main():
    ok = True
    print("stone and iron:")
    ok &= write(stone_button("normal"), "ui-button.png")
    ok &= write(stone_button("hover"), "ui-button-hover.png")
    ok &= write(stone_button("pressed"), "ui-button-pressed.png")
    ok &= write(stone_button("disabled"), "ui-button-disabled.png")
    ok &= write(stone_panel(False), "ui-panel.png")
    ok &= write(stone_panel(True), "ui-panel-inset.png")
    ok &= write(divider(), "ui-divider.png")
    print("illuminated manuscript:")
    ok &= write(vellum_page(False), "ui-page.png")
    ok &= write(vellum_page(True), "ui-page-lit.png")

    if not ok:
        return 1

    print("")
    print("nine-slice margins at x2 - button %d, panel %d" % (BTN_MARGIN * 2, PANEL_MARGIN * 2))
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
