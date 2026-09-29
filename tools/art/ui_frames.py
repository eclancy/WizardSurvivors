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


def vellum_card(lit=False):
    """A block of illuminated text on the page: darker vellum, an ink rule, gold-leaf corners.

    The page alone was not enough. A parchment panel with ordinary white UI text on it reads as a
    beige dialog box, not as a page out of a spellbook - what makes a manuscript look like a
    manuscript is the FURNITURE around the writing: a ruled block, a heavier ink border, and gold
    at the corners where a scribe would have illuminated it.

    The card face is one step down the parchment ramp from the page it sits on, so it reads as a
    panel ON the page rather than as a hole in it.
    """
    c = raster.Canvas(PANEL, PANEL, None)
    last = PANEL - 1

    face = bl.tone("flesh", "hi" if lit else "lit")
    ink = bl.tone("flesh", "deep")
    ink_soft = bl.tone("flesh", "shade")

    c.rect(0, 0, last, last, face)

    # Fibre in the corner blocks only - the edge bands stretch, so anything varying along them
    # smears the length of the card.
    rng = random.Random(31 if lit else 32)
    for _ in range(200):
        x, y = rng.randrange(0, PANEL), rng.randrange(0, PANEL)
        if not _corner(x, y, PANEL, PANEL_MARGIN):
            continue
        if rng.random() < 0.30:
            c.set(x, y, bl.tone("flesh", "lit" if lit else "base"))

    # The scribe's ruled border: a heavy ink line with a lighter one inside it.
    for (inset, tone) in ((1, ink), (3, ink_soft)):
        c.hline(inset, last - inset, inset, tone)
        c.hline(inset, last - inset, last - inset, tone)
        c.vline(inset, inset, last - inset, tone)
        c.vline(last - inset, inset, last - inset, tone)

    # GOLD LEAF at the corners - an L of it, the way an illuminated initial is cornered. Brighter
    # when the card is the one being chosen, which is the same "catching more light" idea the
    # stone cards use for hover.
    leaf = bl.tone("gold", "hi" if lit else "lit")
    leaf_deep = bl.tone("gold", "base" if lit else "shade")
    for (cx, cy, dx, dy) in ((5, 5, 1, 1), (last - 5, 5, -1, 1),
                             (5, last - 5, 1, -1), (last - 5, last - 5, -1, -1)):
        for k in range(4):
            c.set(cx + dx * k, cy, leaf if k < 2 else leaf_deep)
            c.set(cx, cy + dy * k, leaf if k < 2 else leaf_deep)
        c.set(cx + dx, cy + dy, leaf_deep)

    return c


# A torn page needs a DECKLE, and a deckle is the one thing the nine-slice rule above forbids:
# ragged variation ALONG a stretched band smears into a streak. The way round it is not to avoid
# the variation but to stop the band stretching - LevelUpMenu sets AxisStretchHorizontal and
# AxisStretchVertical to Tile on this frame, so the edge bands REPEAT instead, and a pattern whose
# period is the band length tiles seamlessly at any card size.
#
# WHAT THE PERIOD HAS TO BE. The first attempt used an eight-pixel sawtooth and rendered a postage
# stamp: at that rate the eye reads perforation, because a regular period IS what perforation is.
# These are the full 24-pixel band, authored by hand so the bite arrives in RUNS of uneven length -
# two flat pixels, a step, four flat, a deeper notch - which is how paper actually tears. Each
# table ends within one pixel of where it starts, so the join between two tiles is just another
# step in the edge. Four different tables, or the card comes out symmetrical and looks cut.
TEAR_TOP = [0, 0, 1, 1, 0, 1, 2, 2, 1, 1, 0, 0, 1, 2, 3, 2, 1, 1, 2, 1, 0, 0, 1, 0]
TEAR_BOTTOM = [1, 0, 0, 1, 2, 2, 1, 0, 0, 1, 1, 2, 3, 3, 2, 1, 0, 1, 1, 0, 0, 1, 2, 1]
TEAR_LEFT = [0, 1, 1, 2, 2, 1, 0, 0, 1, 1, 0, 1, 2, 2, 3, 2, 1, 0, 0, 1, 1, 0, 0, 0]
TEAR_RIGHT = [2, 1, 1, 0, 0, 1, 1, 2, 2, 1, 0, 0, 1, 1, 2, 3, 2, 1, 1, 0, 0, 1, 1, 2]


def _tear(table, i, size, margin, rng):
    """How many pixels the tear eats in at position `i` along one edge.

    Corner blocks are never stretched or tiled, so they get an extra pixel of jitter - that is the
    only place a nine-slice allows genuine irregularity, and it is where the eye looks first. The
    jitter is kept to one pixel so the corner and the band beside it stay the same edge.
    """
    depth = table[(i - margin) % len(table)]
    if i < margin or i >= size - margin:
        return max(0, min(4, depth + rng.choice((-1, 0, 0, 1))))
    return depth


def torn_page_card(lit=False):
    """A spell written on a page torn out of a book: a deckled edge, a ruled text block, gold leaf.

    The level-up cards were carved stone inside a gold border - handsome, and the wrong object. A
    spell is a PAGE, and the three on offer are three pages out of the same book, so the card has
    to have a torn edge rather than a machined one.

    WHAT MAKES IT READ AS TORN rather than merely jagged is the fibre line: one row of darker
    parchment immediately inside the bite, which is the paper's own thickness catching the light.
    Take it out and the edge looks like a rectangle drawn badly.
    """
    c = raster.Canvas(PANEL, PANEL, None)
    last = PANEL - 1

    face = bl.tone("flesh", "hi" if lit else "lit")
    fibre = bl.tone("flesh", "base" if lit else "shade")
    rule = bl.tone("flesh", "shade" if lit else "base")
    clear = (0, 0, 0, 0)

    c.rect(0, 0, last, last, face)

    # Deterministic per state, so the two frames are the same page in two lights rather than two
    # different pages that swap under the pointer.
    rng = random.Random(71 if lit else 72)
    top = [_tear(TEAR_TOP, x, PANEL, PANEL_MARGIN, rng) for x in range(PANEL)]
    bottom = [_tear(TEAR_BOTTOM, x, PANEL, PANEL_MARGIN, rng) for x in range(PANEL)]
    left = [_tear(TEAR_LEFT, y, PANEL, PANEL_MARGIN, rng) for y in range(PANEL)]
    right = [_tear(TEAR_RIGHT, y, PANEL, PANEL_MARGIN, rng) for y in range(PANEL)]

    for x in range(PANEL):
        for d in range(top[x]):
            c.set(x, d, clear)
        c.set(x, top[x], fibre)
        for d in range(bottom[x]):
            c.set(x, last - d, clear)
        c.set(x, last - bottom[x], fibre)

    for y in range(PANEL):
        for d in range(left[y]):
            c.set(d, y, clear)
        c.set(left[y], y, fibre)
        for d in range(right[y]):
            c.set(last - d, y, clear)
        c.set(last - right[y], y, fibre)

    # Fibre flecks, corner blocks only - see _corner. The centre and the bands stay flat.
    for _ in range(160):
        x, y = rng.randrange(0, PANEL), rng.randrange(0, PANEL)
        if not _corner(x, y, PANEL, PANEL_MARGIN):
            continue
        if c.get(x, y)[3] == 0:
            continue
        if rng.random() < 0.28:
            c.set(x, y, bl.tone("flesh", "lit" if lit else "base"))

    # The scribe's text block: ONE light rule, well inside the tear, so the writing sits in a
    # measured column while the paper around it stays ragged. The first pass drew two heavy ink
    # lines here and the card came back looking framed, which is the thing a torn page is not.
    # Parallel to the edge and so constant along the band, which is what lets it survive tiling.
    for inset in (6,):
        c.hline(inset, last - inset, inset, rule)
        c.hline(inset, last - inset, last - inset, rule)
        c.vline(inset, inset, last - inset, rule)
        c.vline(last - inset, inset, last - inset, rule)

    # Gold leaf where a scribe would have illuminated the corners, sitting just inside the rule.
    leaf = bl.tone("gold", "hi" if lit else "lit")
    leaf_deep = bl.tone("gold", "base" if lit else "shade")
    for (cx, cy, dx, dy) in ((7, 7, 1, 1), (last - 7, 7, -1, 1),
                             (7, last - 7, 1, -1), (last - 7, last - 7, -1, -1)):
        for k in range(5):
            c.set(cx + dx * k, cy, leaf if k < 3 else leaf_deep)
            c.set(cx, cy + dy * k, leaf if k < 3 else leaf_deep)
        c.set(cx + dx, cy + dy, leaf_deep)

    return c


def page_rule():
    """A ruled divider for the page: an ink line with a gold lozenge at its centre.

    Horizontal nine-slice only, so the ends keep their caps at any width.
    """
    c = raster.Canvas(24, 7, None)
    ink = bl.tone("flesh", "deep")
    c.hline(0, 23, 3, ink)
    c.hline(2, 21, 2, bl.tone("flesh", "shade"))
    for x in (0, 1, 22, 23):
        c.set(x, 3, bl.tone("flesh", "base"))
    # the lozenge
    c.set(11, 1, bl.tone("gold", "hi"))
    c.set(12, 1, bl.tone("gold", "hi"))
    c.rect(10, 2, 13, 4, bl.tone("gold", "lit"))
    c.set(11, 5, bl.tone("gold", "shade"))
    c.set(12, 5, bl.tone("gold", "shade"))
    c.set(10, 3, bl.tone("gold", "hi"))
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
    ok &= write(vellum_card(False), "ui-page-card.png")
    ok &= write(vellum_card(True), "ui-page-card-lit.png")
    ok &= write(page_rule(), "ui-page-rule.png")
    ok &= write(torn_page_card(False), "ui-spell-page.png")
    ok &= write(torn_page_card(True), "ui-spell-page-lit.png")

    if not ok:
        return 1

    print("")
    print("nine-slice margins at x2 - button %d, panel %d" % (BTN_MARGIN * 2, PANEL_MARGIN * 2))
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
