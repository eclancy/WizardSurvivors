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

    add("stone button", centre(button), "button text", BUTTON_TEXT, BODY)
    add("stone panel", centre(panel), "button text", BUTTON_TEXT, BODY)

    page_ground = centre(page)
    for el, rgb in ELEMENTS:
        if rev:
            # The chip was measured against the element alone, on the darker resting page.
            ground = chip_ground(rgb, page_ground)
            add("chip %s" % el, ground, "old rule", old_chip_ink(rgb), BODY)
        else:
            ground = chip_ground(rgb, page_ground)
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
