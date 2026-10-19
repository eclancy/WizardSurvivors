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


def _rivets(c, inset, size, colour, shadow, glint=None):
    """Four iron rivets, one per corner block. Nine-slice never stretches a corner, so this is the
    one place a fixed-size detail can live and keep its shape at any control size.

    `glint` is the pixel of light on each head; steel hi unless the caller wants it matte."""
    last = size - 1
    glint = glint or bl.tone("steel", "hi")
    for (cx, cy) in ((inset, inset), (last - inset, inset),
                     (inset, last - inset), (last - inset, last - inset)):
        c.disc(cx, cy, 1, 1, colour)
        c.set(cx, cy - 1, glint)
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

    # Matte rivets: steel base with a lit (not hi) catch. A panel is the ground text is read on,
    # and four bone-white specks at its corners were the brightest thing in most menus - the same
    # complaint, at a smaller scale, that retired the shiny level-up floor (levelup_frames.plate).
    _rivets(c, 5, PANEL, bl.tone("steel", "base"), OCC, glint=bl.tone("steel", "lit"))
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

    # Light in both states, for the reason torn_page_card gives: the state belongs in the border,
    # never under the words. The card still reads as a block ON the page by its ruled border,
    # which is heavier than the page's own; the lit card's inner rule is gold.
    face = bl.tone("flesh", "hi")
    ink = bl.tone("flesh", "deep")
    ink_soft = bl.tone("gold", "base") if lit else bl.tone("flesh", "shade")

    c.rect(0, 0, last, last, face)

    # Fibre in the corner blocks only - the edge bands stretch, so anything varying along them
    # smears the length of the card.
    rng = random.Random(31 if lit else 32)
    for _ in range(200):
        x, y = rng.randrange(0, PANEL), rng.randrange(0, PANEL)
        if not _corner(x, y, PANEL, PANEL_MARGIN):
            continue
        if rng.random() < 0.30:
            c.set(x, y, bl.tone("flesh", "lit"))

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

    # ONE FACE FOR BOTH STATES, and it is the lightest parchment. The resting page used to be one
    # step down (flesh lit, #D8A484) and only the hovered page was light, which put every word on
    # three cards out of four on the darker ground: the gold ink there measured 4.6:1, the body ink
    # 6.4:1, against 7.5 and 10.6 on the light face. The state now lives in the furniture, not
    # under the text: the lit page's rule turns to gold and its leaf brightens, and the focus ring
    # (MenuFocusHighlight) does the rest - so selecting a card never changes how readable it is.
    face = bl.tone("flesh", "hi")
    fibre = bl.tone("flesh", "base")
    rule = bl.tone("gold", "lit") if lit else bl.tone("flesh", "lit")
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
            c.set(x, y, bl.tone("flesh", "lit"))

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


# --- the boon reliquary --------------------------------------------------------------------------
#
# A boon is not a spell, and for as long as both were torn pages the only thing telling them apart
# was a line of gold ink. A spell is WRITTEN - it is a page out of the book. A boon is CARRIED - a
# charm, a coin, a pearl, a flask, every one of its icons is an object drawn on the material rows
# rather than an effect drawn on an element ramp (BoonCatalog.IconPath says why). So the card is
# the thing you would keep such an object in: a small reliquary plaque. Dark violet enamel in a
# bevelled gold bezel, chamfered corners, a diapered field, and the relic itself behind a pale
# glass window.
#
# WHY IT READS AS A DIFFERENT KIND OF OBJECT AT A GLANCE, in order of how much each does:
#   1. VALUE. The pages are the lightest thing on the screen and the plaque is one of the darkest.
#      Squint at the level-up screen and a boon is the dark card - no reading involved.
#   2. EDGE. Paper tears and is soft; this is machined - a straight bevel, cut corners. Same
#      upper-left key as every other bevel in this file, so it is metal catching the same light.
#   3. THE WINDOW. The relic sits behind glass, which no page has.
#
# WHY VIOLET. It is the `violet` row (robe violet) already in the contract, so no new material.
# Stone would have been the obvious dark, and it is wrong here: the panel around the cards is
# stone, and a stone card on a stone plate disappears into the furniture - the exact confusion the
# footer comment in LevelUpMenu warns against. Violet enamel is dark like stone and is not stone.
#
# NINE-SLICE AND TILING. LevelUpMenu draws this with AxisStretch = Tile, like the pages, because
# the field carries a diaper pattern and a stretched diaper turns into smeared diagonals. The
# pattern lives ONLY in the 24x24 centre block and has a period of 12, which divides 24, so the
# tiles meet seamlessly. The bezel is lines parallel to each edge - constant along the band - so
# it is identical whether the band is tiled or stretched. All the irregular furniture (chamfers,
# studs) is in the four corner blocks, which are never scaled.
BOON_CHAMFER = 4
BOON_DIAPER = 12


def _boon_layer(x, y, size, chamfer):
    """Distance from the plaque's outer edge, and which way that edge faces.

    Returns (depth, side) where side is 'lit' for an edge facing the upper-left key, 'dark' for
    one facing away, and 'mid' for the two corners that face neither (top-right, bottom-left).
    A chamfered corner is measured along its diagonal, so the bezel follows the cut.
    """
    last = size - 1
    candidates = [
        (y, "lit"), (x, "lit"),
        (last - y, "dark"), (last - x, "dark"),
        (x + y - chamfer, "lit"),
        (last - x + last - y - chamfer, "dark"),
        (last - x + y - chamfer, "mid"),
        (x + last - y - chamfer, "mid"),
    ]
    return min(candidates, key=lambda c: c[0])


def boon_plaque(lit=False):
    """The boon card: a reliquary plaque of violet enamel in a gold bezel.

    `lit` is the hover/focus/upgrade state: the enamel one step up its ramp and the gold one step
    brighter. It is the same plaque in more light, never a different object, for the same reason
    as the torn page's two states.

    The outermost two pixels are DARK on purpose (occlusion, then deep enamel) and the gold starts
    inside them. MenuFocusHighlight draws its lit-gold ring a few pixels inside the edge of
    whatever is selected, and on a card whose outer edge was gold the ring would land on gold and
    vanish - which is the bug .ai/controller-and-focus.md already paid for once with the stone
    cards. A dark lip under the ring keeps it a light around the card.
    """
    c = raster.Canvas(PANEL, PANEL, None)
    last = PANEL - 1

    face = bl.tone("violet", "base" if lit else "shade")
    lattice = bl.tone("violet", "shade" if lit else "deep")
    boss = bl.tone("violet", "lit" if lit else "base")
    groove = bl.tone("violet", "shade" if lit else "deep")
    gold_lit = bl.tone("gold", "hi" if lit else "lit")
    gold_mid = bl.tone("gold", "lit" if lit else "base")
    gold_dark = bl.tone("gold", "base" if lit else "shade")
    lip = bl.tone("violet", "deep")
    clear = (0, 0, 0, 0)

    for y in range(PANEL):
        for x in range(PANEL):
            depth, side = _boon_layer(x, y, PANEL, BOON_CHAMFER)
            if depth < 0:
                c.set(x, y, clear)
            elif depth == 0:
                c.set(x, y, OCC)
            elif depth == 1:
                c.set(x, y, lip)
            elif depth == 2:
                # The bevel's outer face carries the light: lit toward the key, dark away from it.
                c.set(x, y, {"lit": gold_lit, "dark": gold_dark, "mid": gold_mid}[side])
            elif depth == 3:
                c.set(x, y, gold_mid if side == "lit" else gold_dark)
            elif depth == 4:
                # The enamel is set BELOW the bezel, so its inner shadow falls on the key side.
                c.set(x, y, OCC if side != "dark" else lattice)
            elif depth == 7:
                # A champleve groove: one line cut into the enamel, inside the bezel. It is what the
                # page's ink rule is to the page - a measured field for the writing.
                c.set(x, y, groove)
            else:
                c.set(x, y, face)

    # The diaper, centre block only (see the module note on tiling). Diagonal lattice on a 12-pixel
    # period, with a raised boss in the middle of each diamond.
    for y in range(PANEL_MARGIN, PANEL - PANEL_MARGIN):
        for x in range(PANEL_MARGIN, PANEL - PANEL_MARGIN):
            u, v = x - PANEL_MARGIN, y - PANEL_MARGIN
            if (u + v) % BOON_DIAPER == 0 or (u - v) % BOON_DIAPER == 0:
                c.set(x, y, lattice)
            elif (u % BOON_DIAPER, v % BOON_DIAPER) in ((BOON_DIAPER // 2, 0), (0, BOON_DIAPER // 2)):
                # The two diamond centres per period (the lattice crosses itself at 0,0 and 6,6).
                c.set(x, y, boss)

    # Four gold studs in the corner blocks, where the bezel would have been pinned to the enamel.
    # Not _rivets: that one is iron and catches its light in steel. These are gold all through.
    for (cx, cy) in ((9, 9), (last - 9, 9), (9, last - 9), (last - 9, last - 9)):
        c.disc(cx, cy, 1, 1, gold_mid)
        c.set(cx - 1, cy - 1, gold_lit)
        c.set(cx, cy - 1, gold_lit)
        c.set(cx + 1, cy + 1, OCC)
    return c


def boon_window():
    """The glass the relic sits behind: a small gold-rimmed window, nine-sliced around the icon.

    PALE, because the boon icons were drawn for parchment - several are a dark flask or a dark
    seed, and on violet enamel they would simply be gone. Bone `lit`, a cold pale that reads as
    glass rather than paper; the warm parchment would have put the page back inside the plaque.
    Stretched rather than tiled: its centre is one flat tone, and its bands are lines parallel to
    their edge, so stretching is lossless.
    """
    size = 24
    c = raster.Canvas(size, size, None)
    last = size - 1
    glass = bl.tone("skin", "lit")

    c.rect(0, 0, last, last, glass)
    # Rim: occlusion outside, then a one-pixel gold bezel lit from the upper left.
    c.rect(0, 0, last, 0, OCC)
    c.rect(0, last, last, last, OCC)
    c.rect(0, 0, 0, last, OCC)
    c.rect(last, 0, last, last, OCC)
    _bevel(c, 1, 1, last - 1, last - 1, bl.tone("gold", "lit"), bl.tone("gold", "base"),
           bl.tone("gold", "shade"), 2, raised=True)
    # The glass is recessed: shadow on the key side, a glint on the far one.
    c.hline(3, last - 3, 3, bl.tone("skin", "base"))
    c.vline(3, 3, last - 3, bl.tone("skin", "base"))
    c.hline(4, last - 3, last - 3, bl.tone("skin", "hi"))
    c.vline(last - 3, 4, last - 3, bl.tone("skin", "hi"))
    # Corner clear so the window is not a hard square inside a chamfered plaque.
    for (x, y) in ((0, 0), (last, 0), (0, last), (last, last)):
        c.set(x, y, (0, 0, 0, 0))
    return c


def boon_clasp():
    """The bail a charm hangs from, pinned at the top centre of the plaque.

    A separate sprite rather than part of the plaque because the plaque's top edge is a band, and a
    band may only hold what is constant along it. LevelUpMenu places this over the bezel. It is the
    one thing on the card that says "you will carry this" before a word of it is read.

    THE PNG IS 48x36 AND LevelUpMenu.AddBoonClasp HARD-CODES THAT. Change the size here and change
    the offsets there.
    """
    # 24x18 authored. The first cut was 16x12 and a photograph of the level-up screen showed it as
    # a speck: the menu canvas is scaled down on a 1280x720 window, so a detail has to be drawn
    # big enough to survive that, not just big enough at 1:1.
    w, h = 24, 18
    c = raster.Canvas(w, h, None)
    gold_hi = bl.tone("gold", "hi")
    gold_lit = bl.tone("gold", "lit")
    gold_base = bl.tone("gold", "base")
    gold_shade = bl.tone("gold", "shade")

    # The ring: a two-pixel gold loop, each pixel lit by which way it faces the upper-left key.
    cx, cy = 11.5, 5.5
    for y in range(0, 12):
        for x in range(4, 20):
            dx, dy = x - cx, y - cy
            r = (dx * dx + dy * dy) ** 0.5
            if 3.6 <= r <= 5.9:
                facing = -(dx + dy) / (r * 1.4142)
                if facing > 0.6:
                    tone = gold_hi
                elif facing > 0.0:
                    tone = gold_lit
                elif facing > -0.6:
                    tone = gold_base
                else:
                    tone = gold_shade
                c.set(x, y, tone)

    # The mount: a bevelled gold plate under the ring, with a violet stone set in it.
    c.rect(3, 11, 20, 16, gold_base)
    c.hline(3, 20, 11, gold_lit)
    c.vline(3, 11, 16, gold_lit)
    c.set(3, 11, gold_hi)
    c.set(4, 11, gold_hi)
    c.hline(3, 20, 16, gold_shade)
    c.vline(20, 11, 16, gold_shade)
    c.hline(3, 20, 17, OCC)
    # The stone, with an occlusion seat so it reads as set INTO the plate.
    c.rect(9, 12, 14, 15, OCC)
    c.rect(10, 12, 14, 14, bl.tone("violet", "base"))
    c.set(10, 12, bl.tone("violet", "hi"))
    c.set(11, 12, bl.tone("violet", "lit"))
    c.set(10, 13, bl.tone("violet", "lit"))
    c.hline(12, 14, 14, bl.tone("violet", "shade"))
    c.set(14, 13, bl.tone("violet", "deep"))
    # Two pins holding the plate to the bezel.
    c.set(5, 13, gold_shade)
    c.set(18, 13, gold_shade)
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
    print("boon reliquary:")
    ok &= write(boon_plaque(False), "ui-boon-plaque.png")
    ok &= write(boon_plaque(True), "ui-boon-plaque-lit.png")
    ok &= write(boon_window(), "ui-boon-window.png")
    ok &= write(boon_clasp(), "ui-boon-clasp.png")

    if not ok:
        return 1

    print("")
    print("nine-slice margins at x2 - button %d, panel %d" % (BTN_MARGIN * 2, PANEL_MARGIN * 2))
    print("palette clean: every pixel is a colour art-direction.md defines")
    return 0


if __name__ == "__main__":
    sys.exit(main())
