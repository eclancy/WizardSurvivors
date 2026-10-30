# -*- coding: utf-8 -*-
"""Measure every menu ink against the surface it is actually drawn on.

Run: python tools/art/ui_contrast.py            (the art in the working tree)
     python tools/art/ui_contrast.py --rev HEAD  (the art as committed at a revision)

Writes the table to tools/art/_menu_contrast.txt as well as printing it, and exits non-zero if
any pair misses its target.

WHY THIS EXISTS. "Darken the text" is the obvious fix for a screen that is hard to read, and on
this project it has been the wrong one twice: the level 4 page was unreadable because its ink
was landing on stone brick, not because the ink was pale (.ai/level-up-screen.md, section 1), and
the level-up heading was unreadable because of the floor under it, not its colour. So the ground
is SAMPLED from the generated PNGs, never assumed - change a generator and this re-measures what
it drew.

THE INKS ARE COPIED from the C# that uses them (LevelUpMenu: Ink, InkSoft, InkGold, Rubric, the
three Boon inks, HeadingInk; BonelightSkin's button text; StatIcons.ReadableOn for the chips).
There is no shared source between Python and C# here, so a change to one of those constants must
be copied below - the comment on each C# constant says so.

Targets are WCAG 2: 4.5:1 for body text, 3:1 for large text (a heading, or 24px bold and up).
"""

import io
import os
import subprocess
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UI = "assets/bonelight/ui/"
OUT = os.path.join(ROOT, "tools", "art", "_menu_contrast.txt")

BODY, LARGE = 4.5, 3.0


def f2h(r, g, b):
    return "#%02X%02X%02X" % (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))


def h2rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def lum(rgb):
    def ch(v):
        v = v / 255.0
        return v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
    r, g, b = rgb[:3]
    return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b)


def ratio(a, b):
    la, lb = lum(a), lum(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


# --- the inks, mirrored from C# ---------------------------------------------------------------
INK = h2rgb(f2h(0.235, 0.141, 0.212))
INK_SOFT = h2rgb(f2h(0.204, 0.173, 0.298))
INK_GOLD = h2rgb(f2h(0.329, 0.247, 0.063))
RUBRIC = h2rgb(f2h(0.416, 0.125, 0.157))
BOON_GOLD = h2rgb(f2h(1.0, 0.941, 0.753))
BOON_GOLD_SOFT = h2rgb(f2h(0.847, 0.690, 0.290))
BOON_INK = h2rgb(f2h(0.933, 0.949, 0.980))
HEADING = h2rgb(f2h(1.0, 0.95, 0.86))
BUTTON_TEXT = h2rgb(f2h(0.94, 0.93, 0.88))
BAND = h2rgb(f2h(0.043, 0.059, 0.094))
# ChestItemSelectionMenu: the rarity inks, the gain ink, and the set box the tiers are written in.
CHEST_COMMON = h2rgb(f2h(0.275, 0.329, 0.298))
CHEST_UNCOMMON = h2rgb(f2h(0.145, 0.329, 0.086))  # also InkGain
CHEST_RARE = h2rgb(f2h(0.102, 0.243, 0.541))
CHEST_RELIC = h2rgb(f2h(0.306, 0.267, 0.439))
CHEST_SET_BOX = h2rgb(f2h(0.910, 0.788, 0.675))
# BonelightSkin.StyleButton(..., Emphasis.Primary): the chest's Claim plate.
PRIMARY_TEXT = h2rgb(f2h(1.0, 0.98, 0.90))
DARK = h2rgb(f2h(0.06, 0.06, 0.07))
WHITE = (255, 255, 255)

# ElementColors.GetColor, for the chips.
ELEMENTS = [
    ("Fire", (0.9, 0.35, 0.2)), ("Ice", (0.5, 0.8, 1.0)), ("Arcane", (0.6, 0.4, 0.9)),
    ("Darkness", (0.3, 0.2, 0.4)), ("Light", (1.0, 0.95, 0.6)), ("Grass", (0.35, 0.75, 0.3)),
    ("Earth", (0.6, 0.45, 0.3)), ("Wind", (0.75, 0.9, 0.85)), ("Lightning", (0.95, 0.9, 0.3)),
    ("Poison", (0.5, 0.75, 0.2)), ("Metal", (0.7, 0.7, 0.75)), ("Water", (0.25, 0.5, 0.9)),
]


def load(rel, rev):
    if rev:
        data = subprocess.check_output(["git", "show", "%s:%s" % (rev, rel)], cwd=ROOT)
        return Image.open(io.BytesIO(data)).convert("RGBA")
    return Image.open(os.path.join(ROOT, rel)).convert("RGBA")


def centre(img):
    return img.getpixel((img.size[0] // 2, img.size[1] // 2))[:3]


def dominant(img, box=None):
    region = img.crop(box) if box else img
    counts = {}
    for p in region.getdata():
        if p[3] == 0:
            continue
        counts[p[:3]] = counts.get(p[:3], 0) + 1
    return max(counts.items(), key=lambda kv: kv[1])[0]


def brightest(img, box=None, min_share=0.01):
    """The brightest colour that covers at least `min_share` of the region - the worst ground a
    light ink can land on, ignoring single-pixel specks."""
    region = img.crop(box) if box else img
    counts = {}
    total = 0
    for p in region.getdata():
        if p[3] == 0:
            continue
        counts[p[:3]] = counts.get(p[:3], 0) + 1
        total += 1
    common = [c for c, n in counts.items() if n >= total * min_share]
    return max(common, key=lum)


def chip_ground(element, card):
    r, g, b = element
    cr, cg, cb = [v / 255.0 for v in card]
    w = 0.88
    return h2rgb(f2h(cr + (r - cr) * w, cg + (g - cg) * w, cb + (b - cb) * w))


def old_chip_ink(element):
    r, g, b = element
    return DARK if (r * 0.299 + g * 0.587 + b * 0.114) > 0.62 else WHITE


def new_chip_ink(ground):
    return DARK if ratio(DARK, ground) >= ratio(WHITE, ground) else WHITE


# --- the Spellbook and Deeds lists (scripts/CodexRows.cs, .ai/level-up-screen.md section 4b) ----
# Copied from CodexRows.Vellum and CodexRows.Stone, and MainMenu.StyleQuietPageButton's pill.
VELLUM_INK = h2rgb(f2h(0.235, 0.141, 0.212))
VELLUM_INK_SOFT = h2rgb(f2h(0.204, 0.173, 0.298))
VELLUM_INK_META = h2rgb(f2h(0.431, 0.267, 0.314))
VELLUM_REWARD = h2rgb(f2h(0.329, 0.247, 0.063))
VELLUM_FLAG = h2rgb(f2h(0.416, 0.125, 0.157))
VELLUM_PILL_HOVER = h2rgb(f2h(0.847 * 0.35 + 0.949 * 0.65, 0.643 * 0.35 + 0.863 * 0.65, 0.518 * 0.35 + 0.769 * 0.65))
STONE_FACE = h2rgb(f2h(0.118, 0.149, 0.204))
STONE_INK = h2rgb(f2h(1.0, 0.95, 0.86))
STONE_INK_SOFT = h2rgb(f2h(0.831, 0.863, 0.918))
STONE_INK_META = h2rgb(f2h(0.604, 0.643, 0.722))
STONE_REWARD = h2rgb(f2h(0.847, 0.690, 0.290))
STONE_DONE = h2rgb(f2h(0.608, 0.831, 0.333))
STONE_FLAG = h2rgb(f2h(0.95, 0.72, 0.45))
STONE_HEADING = h2rgb(f2h(1.0, 0.941, 0.753))
# The achievements page (scripts/AchievementsPage.cs, .ai/achievements-and-secrets.md 4.3): the Seen
# tag's ink, and a sealed secret's silhouette (CodexRows.BuildSilhouette) - its lit rim is the shape's
# only edge on the dark panel, so it is held to the graphic target.
STONE_SEEN = h2rgb(f2h(0.416, 0.722, 0.910))          # arcane.lit
STONE_SIL_KEY_RIM = h2rgb(f2h(0.604, 0.643, 0.722))   # skin.base
STONE_SIL_RIM = h2rgb(f2h(0.384, 0.431, 0.518))       # skin.shade
VELLUM_SEEN = h2rgb(f2h(0.090, 0.255, 0.369))         # arcane.shade


def chip_fill(rgb):
    """CodexRows.ChipFill: lighten (dark ink) or darken (white ink) by 5% until 4.6:1."""
    r, g, b = rgb
    for _ in range(10):
        ground = h2rgb(f2h(r, g, b))
        ink = new_chip_ink(ground)
        if ratio(ink, ground) >= 4.6:
            break
        if ink == WHITE:
            r, g, b = r * 0.95, g * 0.95, b * 0.95
        else:
            r, g, b = r + (1 - r) * 0.05, g + (1 - g) * 0.05, b + (1 - b) * 0.05
    return h2rgb(f2h(r, g, b))


def menu_pairs(add, stone_ground, vellum_ground, rev):
    """The main menu's Arcane Energy caption (MainMenu.EnsureMenuEnergyReadout), and the subtitles
    under the Spell Index and Unlocks headings.

    The caption under the Arcane Upgrades button sits on the title art itself, so its ground is
    sampled from both title renders - the brightest colour covering 4% of the box it occupies, the
    same rule _UiShot --ink-report uses. The boxes are in file pixels: on the tall art (360x840,
    drawn x2) composition rows 525-547 left of centre; on the wide art the foot of MENU_BOX, core
    (62-174, 227-238), +160 for the wide frame. Its 5px outline is left out, so this is the floor.
    """
    tall = load(UI + "title-screen.png", rev)
    wide = load(UI + "title-screen-wide.png", rev)
    add("energy caption on title art (phone)", brightest(tall, (66, 362, 178, 374), 0.04),
        "Stone.Reward, no outline", STONE_REWARD, BODY)
    add("energy caption on title art (PC)", brightest(wide, (222, 227, 334, 239), 0.04),
        "Stone.Reward, no outline", STONE_REWARD, BODY)
    # The shop's own header (the purse on its heading band) is measured in shop_pairs.
    add("Unlocks header (stone panel)", stone_ground, "Stone.InkSoft (subtitle)", STONE_INK_SOFT, BODY)
    add("Spell Index header (vellum page)", vellum_ground, "Vellum.InkSoft (subtitle)", VELLUM_INK_SOFT, BODY)


def codex_pairs(add, vellum_ground, stone_ground):
    """A row's face is the page itself on the vellum (CodexRows.Vellum.Face == flesh.hi), and a
    quiet row has no face at all - so on both registers the row inks are measured against the
    ground they actually land on: the row face and the bare panel. The pause screen is on the stone
    panel too since 2026-10-27 (it had a flat panel of its own), so the bare panel covers it."""
    for name, g in (("codex row on vellum", vellum_ground),):
        add(name, g, "Ink (name, heading)", VELLUM_INK, BODY)
        add(name, g, "InkSoft (description)", VELLUM_INK_SOFT, BODY)
        add(name, g, "InkMeta (counts, hints)", VELLUM_INK_META, BODY)
        add(name, g, "Reward", VELLUM_REWARD, BODY)
        add(name, g, "Flag (set aside)", VELLUM_FLAG, BODY)
        add(name, g, "Seen (seen tag)", VELLUM_SEEN, BODY)
    add("Set aside pill, hovered", VELLUM_PILL_HOVER, "Ink", VELLUM_INK, BODY)
    for name, g in (("codex row on stone (face)", STONE_FACE), ("codex quiet row (stone panel)", stone_ground)):
        add(name, g, "Heading (section)", STONE_HEADING, LARGE)
        add(name, g, "Ink (name)", STONE_INK, BODY)
        add(name, g, "InkSoft (description)", STONE_INK_SOFT, BODY)
        add(name, g, "InkMeta (counts, done reward)", STONE_INK_META, BODY)
        add(name, g, "Reward", STONE_REWARD, BODY)
        add(name, g, "Done", STONE_DONE, BODY)
        add(name, g, "Flag (set aside)", STONE_FLAG, BODY)
        add(name, g, "Seen (seen tag)", STONE_SEEN, BODY)
        add(name, g, "sealed glyph lit rim (graphic)", STONE_SIL_KEY_RIM, GRAPHIC)
    # The eye glyph's iris is the Seen ink itself (tools/art/stat_icons.py, arcane lit), so the tag's
    # glyph and its word read as one mark. A sealed row is quiet - no face - so its silhouette sits on
    # the bare panel, and its far rim, the quieter edge, still clears the graphic floor there.
    add("sealed glyph (stone panel)", stone_ground, "far rim (skin.shade)", STONE_SIL_RIM, GRAPHIC)
    for el, rgb in ELEMENTS:
        ground = chip_fill(rgb)
        add("codex chip %s" % el, ground, "WCAG pick", new_chip_ink(ground), BODY)
    # The pause screen's own (Node2DGame.EnsureEscapeMenuUi): "Paused" and the chapter line on the
    # heading band, the tabs' resting ink on the bare panel, an inactive element badge (stone deep).
    add("pause heading band (stone deep)", BAND, "Paused (Stone.Heading)", STONE_HEADING, LARGE)
    add("pause heading band (stone deep)", BAND, "chapter, wizard (Stone.InkSoft)", STONE_INK_SOFT, BODY)
    add("pause tabs (stone panel)", stone_ground, "resting tab (Stone.InkMeta)", STONE_INK_META, LARGE)
    add("pause inactive element badge (stone deep)", BAND, "element, count (Stone.InkMeta)", STONE_INK_META, BODY)


# --- the ascension browser and the element card (LevelUpMenu.BuildAscensionRow, ElementTagPopover) --
# Copied from LevelUpMenu.AscensionReadyFace / AscensionUnmetBand and AscensionReadyGlow's badge.
ASC_READY_FACE = h2rgb(f2h(1.0, 0.941, 0.753))      # gold hi: a ready path's face
ASC_UNMET_BAND = h2rgb(f2h(0.896, 0.789, 0.708))    # the rubric at a tenth over the page
ASC_BADGE = h2rgb(f2h(0.847, 0.690, 0.290))         # gold lit: the "N ready" badge
BOON_ENAMEL = h2rgb(f2h(0.133, 0.110, 0.200))       # LevelUpMenu.ChipBackground(onBoon)


def ascension_pairs(add, vellum_ground):
    """A path that is not ready has no face (it is the bare vellum page) and writes everything in
    the meta ink - that is what greys it - except its unmet gates, which sit on the rubric band in
    the Flag ink. A ready path is a gold face with full inks. Grey is a lighter ink, never one
    below the floor."""
    add("ascension greyed path (vellum page)", vellum_ground, "InkMeta (name, line, met gate)", VELLUM_INK_META, BODY)
    add("ascension unmet band", ASC_UNMET_BAND, "Flag (needs 3 more...)", VELLUM_FLAG, BODY)
    for ink_name, ink in (("Ink (name)", VELLUM_INK), ("InkSoft (what it does)", VELLUM_INK_SOFT),
                          ("InkMeta (met gate, spell)", VELLUM_INK_META), ("Reward (Ready, waiting)", VELLUM_REWARD)):
        add("ascension ready path (gold face)", ASC_READY_FACE, ink_name, ink, BODY)
    add("ascension ready badge", ASC_BADGE, "count", DARK, BODY)
    # The element card is a CodexRows.Stone face; its inks are the stone register's.
    for ink_name, ink in (("Heading (reached tier)", STONE_HEADING), ("Ink (tier effect)", STONE_INK),
                          ("InkSoft (you hold)", STONE_INK_SOFT), ("InkMeta (far tier, notes)", STONE_INK_META),
                          ("Reward (taking this)", STONE_REWARD), ("Done (reaches a tier)", STONE_DONE),
                          ("Flag (next tier, N more)", STONE_FLAG)):
        add("element card (stone face)", STONE_FACE, ink_name, ink, BODY)
    # The level-up chips on a boon card sit on the enamel, not the page.
    for el, rgb in ELEMENTS:
        ground = levelup_chip(rgb, BOON_ENAMEL)
        add("chip %s on boon" % el, ground, "WCAG pick", new_chip_ink(ground), BODY)


# --- the ascension tree (LevelUpMenu.Ascensions.cs, AscensionForkArrows, .ai/level-up-screen.md 4e) --
# Copied from LevelUpMenu.PathSeenDim / PathSealedDim (a SelfModulate on the plate's frame only, so
# it multiplies the ground and never the ink) and AscensionForkArrows.OpenLine / ClosedLine.
PATH_SEEN_DIM = (0.78, 0.78, 0.84)
PATH_SEALED_DIM = (0.56, 0.56, 0.64)
FORK_OPEN = h2rgb(f2h(0.541, 0.416, 0.118))         # gold base
FORK_CLOSED = h2rgb(f2h(0.431, 0.267, 0.314))       # flesh shade, CodexRows.Vellum.InkMeta


def multiply(rgb, factors):
    return tuple(int(round(c * f)) for c, f in zip(rgb, factors))


def tree_pairs(add, rev, vellum_ground):
    """The paths are obsidian plates: lit when ready, resting and dimmed when seen but out of reach,
    dimmer still when sealed. The dim is a SelfModulate on the frame, so only the ground darkens.
    The node is the page with a dark edge (gold face when ready); the fork is a graphic (3:1)."""
    resting = centre(load(UI + "ui-obsidian-plate.png", rev))
    lit = centre(load(UI + "ui-obsidian-plate-lit.png", rev))
    for name, ground in (("tree path, ready (lit plate)", lit),
                         ("tree path, seen (dimmed plate)", multiply(resting, PATH_SEEN_DIM)),
                         ("tree path, sealed (dimmed plate)", multiply(resting, PATH_SEALED_DIM))):
        add(name, ground, "Title (name, ???)", ASC_TITLE, BODY)
        add(name, ground, "Ink (what it does)", ASC_INK, BODY)
        add(name, ground, "Soft (sealed line, element)", ASC_SOFT, BODY)
        add(name, ground, "Advice (gold lit)", ASC_ADVICE, BODY)
    add("tree node (vellum page)", vellum_ground, "Ink (spell name)", VELLUM_INK, BODY)
    add("tree node (vellum page)", vellum_ground, "edge (Ink, graphic)", VELLUM_INK, GRAPHIC)
    add("tree node, ready (gold face)", ASC_READY_FACE, "edge (InkGold, graphic)", INK_GOLD, GRAPHIC)
    add("tree node, ready (gold face)", ASC_READY_FACE, "pip ring (gold base)", PIP_GOLD_BASE, GRAPHIC)
    add("tree node (vellum page)", vellum_ground, "pip ring (gold base)", PIP_GOLD_BASE, GRAPHIC)
    add("tree fork (vellum page)", vellum_ground, "open line (gold base)", FORK_OPEN, GRAPHIC)
    add("tree fork (vellum page)", vellum_ground, "closed line (meta, dashed)", FORK_CLOSED, GRAPHIC)
    add("tree fork, sealed glyph on plate", multiply(resting, PATH_SEALED_DIM), "glyph gold lit", ASC_ADVICE, GRAPHIC)


def report_pairs(add, stone_ground):
    """ReportBugScreen: CodexRows.Stone inks on the stone panel, and in its text fields and the
    "Sent with it" block, which are the stone row face."""
    for ink_name, ink in (("Heading (title, Sent with it)", STONE_HEADING), ("Ink (field labels)", STONE_INK),
                          ("InkSoft (intro, status)", STONE_INK_SOFT), ("InkMeta (counter, promise)", STONE_INK_META),
                          ("Flag (error)", STONE_FLAG), ("Done (Thank you!)", STONE_DONE)):
        add("report a bug (stone panel)", stone_ground, ink_name, ink, BODY)
    for ink_name, ink in (("Ink (typed text)", STONE_INK), ("InkMeta (placeholder)", STONE_INK_META),
                          ("InkSoft (what gets sent)", STONE_INK_SOFT)):
        add("report a bug field (stone face)", STONE_FACE, ink_name, ink, BODY)


# --- the card families (scripts/CardFamilyArt.cs, .ai/level-up-screen.md "The card families") ----
# Copied from CardFamilyArt: change one there, change it here.
# Every spell card is the parchment page and writes in the page's inks (INK, INK_SOFT, RUBRIC,
# INK_GOLD above); the upgrade and mutation pages add a mark round the edge, measured as a graphic.
UPGRADE_RULE = h2rgb(f2h(0.416, 0.125, 0.157))     # red lit: the upgrade page's rubric
# lichen lit: the mutation page's rule and the stained fibre line of its tear. (The stain's inner
# pixel, lichen hi, is a soft edge at 2.9:1 and is not the mark; the line it hugs is.)
MUTATION_RULE = h2rgb(f2h(0.227, 0.392, 0.439))
MUTATION_CHIP = h2rgb(f2h(0.165, 0.306, 0.345))    # lichen base
MUTATION_HEADING = h2rgb(f2h(0.086, 0.157, 0.180)) # lichen deep
ASC_TITLE = h2rgb(f2h(1.0, 0.941, 0.753))
ASC_INK = h2rgb(f2h(0.933, 0.949, 0.980))
ASC_SOFT = h2rgb(f2h(0.416, 0.722, 0.910))         # arcane lit
ASC_ADVICE = h2rgb(f2h(0.847, 0.690, 0.290))       # gold lit
ASC_CHIP = h2rgb(f2h(0.090, 0.255, 0.369))         # arcane shade
ASC_HEADING = h2rgb(f2h(0.043, 0.141, 0.212))      # arcane deep
APO_TITLE = h2rgb(f2h(1.0, 0.784, 0.282))          # fire hot
APO_SOFT = h2rgb(f2h(0.910, 0.478, 0.102))         # fire mid
APO_CHIP = h2rgb(f2h(0.165, 0.055, 0.078))         # red shade
APO_HEADING = h2rgb(f2h(0.416, 0.125, 0.157))      # red lit
CHIP_INK = h2rgb(f2h(0.933, 0.949, 0.980))
LOCK_BAND = h2rgb(f2h(0.416, 0.125, 0.157))
LOCK_INK = h2rgb(f2h(0.933, 0.949, 0.980))
# The Ascend strip's button inks (LevelUpMenu.BuildAscendStrip).
STRIP_GOLD = h2rgb(f2h(1.0, 0.88, 0.50))
STRIP_EMBER = h2rgb(f2h(1.0, 0.70, 0.42))
# LevelPips: ring and held fill, on the upgrade page only. A graphic, so 3:1 (WCAG 1.4.11).
PIP_GOLD_BASE = h2rgb(f2h(0.541, 0.416, 0.118))
PIP_OLD = h2rgb(f2h(0.72, 0.54, 0.12))             # the gold leaf the pips used before
GRAPHIC = 3.0


def blend(ink, ground, alpha):
    return tuple(int(round(g + (i - g) * alpha)) for i, g in zip(ink, ground))


def family_pairs(add, rev):
    """Each family's inks on its own frame, in both states (the frame's centre is the face, and the
    face is the same in both - this proves it), the marks round the upgrade and mutation pages as
    graphics, the evolution page's headings on the vellum page, the strip, the lock stamp, the
    synergy chips and the pips."""
    page = centre(load(UI + "ui-spell-page.png", rev))
    vellum_page = centre(load(UI + "ui-page.png", rev))
    for lit in ("", "-lit"):
        for mark, label in (("upgrade", "upgrade page"), ("mutation", "mutation page")):
            g = centre(load(UI + "ui-spell-page-%s%s.png" % (mark, lit), rev))
            name = "%s%s" % (label, ", lit" if lit else "")
            add(name, g, "Ink (name, description)", INK, BODY)
            add(name, g, "InkSoft (type line, element line)", INK_SOFT, BODY)
            add(name, g, "Rubric (deltas, advice)", RUBRIC, BODY)
            add(name, g, "InkGold (stays attuned)", INK_GOLD, BODY)
        g = centre(load(UI + "ui-spell-page-upgrade%s.png" % lit, rev))
        add("upgrade page%s" % (", lit" if lit else ""), g, "rubric rule", UPGRADE_RULE, GRAPHIC)
        add("upgrade page%s" % (", lit" if lit else ""), g, "pip ring/held (gold base)", PIP_GOLD_BASE, GRAPHIC)
        g = centre(load(UI + "ui-spell-page-mutation%s.png" % lit, rev))
        add("mutation page%s" % (", lit" if lit else ""), g, "teal rule, stained tear (lichen lit)", MUTATION_RULE, GRAPHIC)
        for plate, title, soft, label in (("obsidian", ASC_TITLE, ASC_SOFT, "ascension"),
                                          ("ember", APO_TITLE, APO_SOFT, "apotheosis")):
            g = centre(load(UI + "ui-%s-plate%s.png" % (plate, lit), rev))
            name = "%s plate%s" % (label, ", lit" if lit else "")
            add(name, g, "Title (name)", title, BODY)
            add(name, g, "Ink (description)", ASC_INK, BODY)
            add(name, g, "Soft (element line)", soft, BODY)
            add(name, g, "Advice (gold lit)", ASC_ADVICE, BODY)
            add(name, g, "strip button text", STRIP_GOLD if plate == "obsidian" else STRIP_EMBER, BODY)
    add("synergy chip, mutation", MUTATION_CHIP, "ChipInk", CHIP_INK, BODY)
    add("synergy chip, ascension", ASC_CHIP, "ChipInk", CHIP_INK, BODY)
    add("synergy chip, apotheosis", APO_CHIP, "ChipInk", CHIP_INK, BODY)
    add("lock stamp band", LOCK_BAND, "LockInk", LOCK_INK, BODY)
    add("evolution page heading (vellum)", vellum_page, "mutation heading", MUTATION_HEADING, LARGE)
    add("evolution page heading (vellum)", vellum_page, "ascension heading", ASC_HEADING, LARGE)
    add("evolution page heading (vellum)", vellum_page, "apotheosis heading", APO_HEADING, LARGE)
    add("evolution page subtitle (vellum)", vellum_page, "InkSoft", INK_SOFT, BODY)
    held = centre(load(UI + "ui-spell-page-upgrade.png", rev))
    add("penknife: protected spell (upgrade page, 0.7)", held, "Ink faded", blend(INK, held, 0.7), BODY)
    add("penknife: held spell (upgrade page)", held, "InkSoft (level, elements)", INK_SOFT, BODY)


# --- the in-run loadout strip (scripts/LoadoutHud.cs, .ai/technical-architecture.md "The in-run HUD") --
# Copied from LoadoutHud: NumeralInk / NumeralOutline (the health readout's), and the slot backing.
HUD_NUMERAL = h2rgb(f2h(1.0, 0.94, 0.86))
HUD_OUTLINE_ALPHA = 0.9
HUD_SLOT_BACKING = h2rgb(f2h(0.043, 0.059, 0.094))
HUD_SLOT_ALPHA = 0.85
# The milestone rims (graphics, and a second cue - the pause screen names the branch).
HUD_RIMS = (("held (steel base)", h2rgb(f2h(0.416, 0.494, 0.596))), ("mutated (gold lit)", h2rgb(f2h(0.847, 0.690, 0.290))),
            ("ascended (arcane lit)", h2rgb(f2h(0.416, 0.722, 0.910))), ("apotheosis (fire mid)", h2rgb(f2h(0.910, 0.478, 0.102))))
# The grounds the strip floats over: the darkest stage floor (floor stone deep) and the brightest
# (the Sketchbook's paper, taken as white - the worst case for a dark backing gone translucent).
HUD_GROUNDS = (("dark floor", h2rgb("#0B0F18")), ("white paper", WHITE))


def hud_pairs(add):
    """The loadout strip's chips are opaque and use the codex chip's face and ink, so they are the
    codex numbers again, listed under their own name so a change to either shows up here. The level
    numeral is outlined in black at 0.9, so its ground is that outline over the brightest icon pixel
    (white). The rims are measured against the slot backing as it lands on each ground."""
    for el, rgb in ELEMENTS:
        ground = chip_fill(rgb)
        add("HUD loadout chip %s" % el, ground, "WCAG pick", new_chip_ink(ground), BODY)
    add("HUD level numeral (outline over white)", blend((0, 0, 0), WHITE, HUD_OUTLINE_ALPHA), "NumeralInk", HUD_NUMERAL, BODY)
    for ground_name, ground in HUD_GROUNDS:
        backing = blend(HUD_SLOT_BACKING, ground, HUD_SLOT_ALPHA)
        for rim_name, rim in HUD_RIMS:
            add("HUD slot backing on %s" % ground_name, backing, "rim, " + rim_name, rim, GRAPHIC)


# --- the Arcane Upgrades shop (scripts/MainMenu.Shop.cs, .ai/level-up-screen.md "Arcane Upgrades") --
# Copied from MainMenu.Shop.cs: change one there, change it here. The pages write in the page inks
# above (INK, INK_SOFT, RUBRIC, INK_GOLD); these are the shop's own.
SHOP_INK_META = h2rgb(f2h(0.431, 0.267, 0.314))     # flesh shade: "Locked"
PLATE_COST = h2rgb(f2h(1.0, 0.941, 0.753))          # gold hi: the price on a plate that can be pressed
PLATE_ACTION = h2rgb(f2h(1.0, 0.98, 0.90))          # Primary text: "Buy", "Unlock"
PLATE_DIM = h2rgb(f2h(0.604, 0.643, 0.722))         # skin base: price and "Need N more" when short
PURSE = h2rgb(f2h(0.847, 0.690, 0.290))             # gold lit: the purse on the heading band
PURSE_SPENT = h2rgb(f2h(0.95, 0.72, 0.45))          # Stone.Flag: "-90" rising off the purse
MASTERED_RULE = h2rgb("#543F10")                    # gold shade: the gilded page's outer rule
SEAL_EDGE = h2rgb("#543F10")                        # gold shade: the seal's darkest ring


def shop_pairs(add, rev):
    """Every ink the shop writes, against the ground it is written on: the three pages it uses (a
    new spell's, the rubricated upgrade page, the gilded mastered page) in both states, the buy
    plate's two frames, the heading band and the stone panel the sections sit on. The gilded
    rule and the seal are graphics (3:1). Measured on the photographs too: _UiShot --ink-report."""
    for name, png in (("shop spell page", "ui-spell-page"), ("shop upgrade page", "ui-spell-page-upgrade"),
                      ("shop mastered page", "ui-spell-page-mastered")):
        for lit in ("", "-lit"):
            g = centre(load(UI + png + lit + ".png", rev))
            label = name + (", lit" if lit else "")
            add(label, g, "Ink (name, value, description)", INK, BODY)
            add(label, g, "InkSoft (stat, each level, level)", INK_SOFT, BODY)
            add(label, g, "Rubric (next value)", RUBRIC, BODY)
            add(label, g, "InkGold (Mastered, Owned)", INK_GOLD, BODY)
            add(label, g, "InkMeta (Locked)", SHOP_INK_META, BODY)
            add(label, g, "pip ring/held (gold base)", PIP_GOLD_BASE, GRAPHIC)
    for lit in ("", "-lit"):
        g = centre(load(UI + "ui-spell-page-mastered%s.png" % lit, rev))
        add("shop mastered page%s" % (", lit" if lit else ""), g, "gilded outer rule (gold shade)", MASTERED_RULE, GRAPHIC)
        add("shop mastered page%s" % (", lit" if lit else ""), g, "seal edge (gold shade)", SEAL_EDGE, GRAPHIC)
    lit_plate = centre(load(UI + "ui-button-hover.png", rev))
    dim_plate = centre(load(UI + "ui-button-disabled.png", rev))
    add("shop buy plate (lit stone)", lit_plate, "price (gold hi)", PLATE_COST, BODY)
    add("shop buy plate (lit stone)", lit_plate, "Buy / Unlock", PLATE_ACTION, BODY)
    add("shop buy plate, short (disabled)", dim_plate, "price, Need N more (skin base)", PLATE_DIM, BODY)
    add("shop heading band (stone deep)", BAND, "heading", HEADING, LARGE)
    add("shop heading band (stone deep)", BAND, "purse (gold lit)", PURSE, BODY)
    add("shop heading band (stone deep)", BAND, "caption (Stone.InkSoft)", STONE_INK_SOFT, BODY)
    add("shop heading band (stone deep)", BAND, "spent (Stone.Flag)", PURSE_SPENT, BODY)
    panel = centre(load(UI + "ui-panel.png", rev))
    add("shop sections (stone panel)", panel, "section heading (Stone.Heading)", STONE_HEADING, LARGE)
    add("shop sections (stone panel)", panel, "tally, intro (Stone.InkMeta)", STONE_INK_META, BODY)
    add("shop sections (stone panel)", panel, "section note (Stone.InkSoft)", STONE_INK_SOFT, BODY)

# --- the results screen (scripts/GameOverScreen.cs, .ai/level-up-screen.md section 6) ------------
# It writes in CodexRows.Stone (the STONE_* inks above) on the stone panel, the stone row face and
# LevelUpMenu's heading band; these are the colours of its own. Copied from GameOverScreen.
RESULT_VICTORY = h2rgb(f2h(1.0, 0.86, 0.42))
RESULT_DEFEAT = h2rgb(f2h(0.85, 0.32, 0.34))
RESULT_MUTATION = h2rgb(f2h(0.847, 0.690, 0.290))    # gold lit, LoadoutHud's mutated rim
RESULT_ASCENSION = h2rgb(f2h(0.416, 0.722, 0.910))   # arcane lit, the ascended rim
RESULT_APOTHEOSIS = h2rgb(f2h(0.910, 0.478, 0.102))  # fire mid, the Apotheosis rim
RESULT_HELD_RIM = h2rgb(f2h(0.416, 0.494, 0.596))    # steel base
RESULT_MARK_INK = h2rgb(f2h(0.06, 0.06, 0.07))
RESULT_WELL = h2rgb(f2h(0.043, 0.059, 0.094))        # the icon well, stone deep
STONE_EDGE = h2rgb(f2h(0.227, 0.275, 0.345))         # CodexRows.Stone.Edge / Rule / BarTrack


def results_pairs(add, panel_ground):
    """The outcome word and its line on the band; every row ink on the row face (headline tiles,
    reward cards, spell rows, stat tiles, damage rows, pills) and on the bare panel (section and
    group headings, the tabs, the quiet lines); the milestone marks; and the graphics - a new
    card's gold edge, the icon wells' rims, the bars - at 3:1."""
    add("results band (stone deep)", BAND, "Victory (34px)", RESULT_VICTORY, LARGE)
    add("results band (stone deep)", BAND, "Defeat (34px)", RESULT_DEFEAT, LARGE)
    add("results band (stone deep)", BAND, "InkSoft (chapter, wizard)", STONE_INK_SOFT, BODY)
    for ink_name, ink in (("Ink (values, names)", STONE_INK), ("InkSoft (sources, level)", STONE_INK_SOFT),
                          ("InkMeta (tile labels)", STONE_INK_META), ("Reward (kind line, energy, max)", STONE_REWARD),
                          ("Flag (New seal)", STONE_FLAG)):
        add("results row (stone face)", STONE_FACE, ink_name, ink, BODY)
    add("results breakdown toggle, hovered", blend(h2rgb("#D8B04A"), STONE_FACE, 0.08), "Ink", STONE_INK, BODY)
    for ink_name, ink, target in (("Heading (Rewards, group titles)", STONE_HEADING, LARGE),
                                  ("Heading (chosen tab)", STONE_HEADING, LARGE),
                                  ("InkMeta (other tab, 26px)", STONE_INK_META, LARGE),
                                  ("InkMeta (quiet lines, counts)", STONE_INK_META, BODY),
                                  ("Flag (N new)", STONE_FLAG, BODY)):
        add("results on the stone panel", panel_ground, ink_name, ink, target)
    for mark_name, mark in (("Mutation", RESULT_MUTATION), ("Ascension", RESULT_ASCENSION),
                            ("Apotheosis", RESULT_APOTHEOSIS)):
        add("results milestone mark (%s)" % mark_name, mark, "MarkInk", RESULT_MARK_INK, BODY)
    add("results new card edge on panel", panel_ground, "gold lit edge (graphic)", RESULT_MUTATION, GRAPHIC)
    for rim_name, rim in (("held", RESULT_HELD_RIM), ("mutated", RESULT_MUTATION), ("ascended", RESULT_ASCENSION),
                          ("apotheosis", RESULT_APOTHEOSIS)):
        add("results icon well (stone deep)", RESULT_WELL, "rim, " + rim_name, rim, GRAPHIC)
    add("results bar track (stone hi)", STONE_EDGE, "fill, gold lit (graphic)", STONE_REWARD, GRAPHIC)
    add("results bar track (stone hi)", STONE_EDGE, "fill, InkMeta - Other (graphic)", STONE_INK_META, GRAPHIC)


def levelup_chip(rgb, card):
    """A level-up card's chip: the element at 88% over the card (ChipBackground), then nudged by
    CodexRows.ChipFill like every other chip."""
    g = chip_ground(rgb, card)
    return chip_fill(tuple(v / 255.0 for v in g))


def measure(rev=None):
    rows = []

    def add(surface, ground, ink_name, ink, target):
        rows.append((surface, "#%02X%02X%02X" % ground, ink_name, ratio(ink, ground), target))

    page = load(UI + "ui-spell-page.png", rev)
    page_lit = load(UI + "ui-spell-page-lit.png", rev)
    card = load(UI + "ui-page-card.png", rev)
    vellum = load(UI + "ui-page.png", rev)
    boon = load(UI + "ui-boon-plaque.png", rev)
    boon_lit = load(UI + "ui-boon-plaque-lit.png", rev)
    plate = load(UI + "levelup-plate.png", rev)
    button = load(UI + "ui-button.png", rev)
    panel = load(UI + "ui-panel.png", rev)

    for name, img in (("spell page, resting", page), ("spell page, lit", page_lit),
                      ("evolution card, resting", card), ("vellum page", vellum)):
        g = centre(img)
        add(name, g, "Ink (title, stats)", INK, BODY)
        add(name, g, "InkSoft (type, subtitle)", INK_SOFT, BODY)
        add(name, g, "InkGold (ascension title)", INK_GOLD, LARGE)
        add(name, g, "Rubric (upgrade lines)", RUBRIC, BODY)

    w, h = boon.size
    for name, img in (("boon plaque, resting", boon), ("boon plaque, lit", boon_lit)):
        g = dominant(img, (w // 4, h // 4, 3 * w // 4, 3 * h // 4))
        add(name, g, "BoonInkGold (name)", BOON_GOLD, LARGE)
        add(name, g, "BoonInkGoldSoft (BOON line)", BOON_GOLD_SOFT, BODY)
        add(name, g, "BoonInk (effect)", BOON_INK, BODY)

    pw, ph = plate.size
    # The heading's row of the floor: the top band of the plate, inside its bevel.
    heading_box = (8, 8, pw - 8, 72)
    if rev:
        # Before the band: the heading was drawn on the floor itself.
        add("heading on the floor (brightest stone)", brightest(plate, heading_box), "Heading", HEADING, LARGE)
        add("heading on the floor (typical stone)", dominant(plate, heading_box), "Heading", HEADING, LARGE)
    else:
        add("heading band (stone deep)", BAND, "Heading", HEADING, LARGE)
        add("floor, brightest stone (no text on it)", brightest(plate, heading_box), "Heading", HEADING, LARGE)

    # The chest screen writes on the same two pages, and inside them on an opaque set box.
    for name, ground in (("chest page, resting", centre(page)), ("chest page, lit", centre(page_lit)),
                         ("chest set box", CHEST_SET_BOX)):
        add(name, ground, "Ink (name, effect, tiers)", INK, BODY)
        add(name, ground, "InkSoft (tier, count)", INK_SOFT, BODY)
        add(name, ground, "rarity Common", CHEST_COMMON, BODY)
        add(name, ground, "rarity Uncommon / gain", CHEST_UNCOMMON, BODY)
        add(name, ground, "rarity Rare", CHEST_RARE, BODY)
        add(name, ground, "rarity Relic", CHEST_RELIC, BODY)
    add("claim plate (primary button)", centre(load(UI + "ui-button-hover.png", rev)), "primary text", PRIMARY_TEXT, BODY)

    add("stone button", centre(button), "button text", BUTTON_TEXT, BODY)
    add("stone panel", centre(panel), "button text", BUTTON_TEXT, BODY)

    if not rev:
        codex_pairs(add, centre(vellum), centre(panel))
        menu_pairs(add, centre(panel), centre(vellum), rev)
        ascension_pairs(add, centre(vellum))
        report_pairs(add, centre(panel))
        family_pairs(add, rev)
        hud_pairs(add)
        tree_pairs(add, rev, centre(vellum))
        shop_pairs(add, rev)
        results_pairs(add, centre(panel))
    else:
        # Before the families: the evolution cards were vellum cards on the vellum page, the level 8
        # title in InkGold and the pips in gold leaf.
        add("evolution page heading (vellum), before", centre(vellum), "InkGold (ascension heading)", INK_GOLD, LARGE)
        add("spell page, before", centre(page), "pip ring/held (old gold leaf)", PIP_OLD, 3.0)

    page_ground = centre(page)
    for el, rgb in ELEMENTS:
        if rev:
            # The chip was measured against the element alone, on the darker resting page.
            ground = chip_ground(rgb, page_ground)
            add("chip %s" % el, ground, "old rule", old_chip_ink(rgb), BODY)
        else:
            ground = levelup_chip(rgb, page_ground)
            add("chip %s" % el, ground, "WCAG pick", new_chip_ink(ground), BODY)
    return rows


def main():
    rev = None
    if "--rev" in sys.argv:
        rev = sys.argv[sys.argv.index("--rev") + 1]
    rows = measure(rev)
    lines = ["menu contrast - %s" % (("art at " + rev) if rev else "working tree"), ""]
    lines.append("%-40s %-8s %-28s %6s  %s" % ("surface", "ground", "ink", "ratio", "target"))
    failures = 0
    for surface, ground, ink, r, target in rows:
        ok = r >= target
        failures += 0 if ok else 1
        lines.append("%-40s %-8s %-28s %5.2f:1  %.1f %s" % (surface, ground, ink, r, target, "" if ok else "FAIL"))
    lines.append("")
    lines.append("%d of %d below target" % (failures, len(rows)))
    text = "\n".join(lines)
    print(text)
    if not rev:
        with open(OUT, "w") as f:
            f.write(text + "\n")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
