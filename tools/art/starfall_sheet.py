# -*- coding: utf-8 -*-
"""Contact sheet of Starfall - tools/art/_starfall_sheet.png (gitignored).

Top: the card icon, enlarged. Then one row per form of the spell, read from the _UiShot captures in
Godot's user:// folder: two frames of the rain at PC size, a 1:1 crop round the player from the
first of them (the stars are small, and a shrunk frame hides exactly what is being judged), and one
frame at phone size. Last, the screens the spell appears on: the level-up offer, the level 4
mutation page, the level 8 ascension page and the shop. Missing captures are skipped, not faked.

The rain (one Godot at a time). _UiShot sets SaveManager.WritesDisabled only in its own _Ready, which is
after the save has loaded - and a load that migrates writes straight back - so pass --no-save-writes,
which disables writes before the load:

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- --no-save-writes \\
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=0 --start-spell=starfall \\
      --demo-spell-level=4 --demo-evolutions=starfall_frostfall \\
      --spell-stage=36,40,20 --fx-lead=150 --frames=400 --burst=3,25 --shot=starfall-frostfall-pc
  (phone: --resolution 540x1212 ... --portrait --safe-area --shot=starfall-frostfall-phone)

  forms: base (level 1), clearnight / frostfall / downpour (level 4, starfall_clear_night /
  _frostfall / _downpour), starburst (level 8, clear_night + starburst), constellation (level 8,
  frostfall + constellation). A start spell that rolls Legendary is tinted gold - re-take it.

  python tools/art/starfall_sheet.py
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(HERE, "_starfall_sheet.png")
ICON = os.path.join(ROOT, "assets", "bonelight", "ui", "spells", "starfall-light.png")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

BG = (11, 15, 24)
INK = (220, 232, 255)
FORMS = [
    ("base", "level 1 - the drizzle"),
    ("clearnight", "level 4 - Clear Night (+1 Light, +2 stars)"),
    ("frostfall", "level 4 - Frostfall (+1 Ice, slows)"),
    ("downpour", "level 4 - Downpour (40% more often, 15% smaller)"),
    ("starburst", "level 8 - Clear Night + Starburst (+1 Light): the stars explode"),
    ("constellation", "level 8 - Frostfall + Constellation: the stars link"),
]
SCREENS = [
    ("card", "the level-up offer"),
    ("mutation", "level 4 mutation page"),
    ("ascension", "level 8 ascension page"),
    ("shop", "Arcane Upgrades - New spells"),
]
PC_SCALE = 0.42
PHONE_SCALE = 0.3
CROP = (300, 210)  # half-size of the 1:1 crop round the screen centre (the player)


def burst(name):
    """The frames of one capture set, in order: <name>.png, then <name>-1.png ..."""
    frames = []
    first = os.path.join(USER, name + ".png")
    if not os.path.exists(first):
        return frames
    frames.append(first)
    i = 1
    while os.path.exists(os.path.join(USER, "%s-%d.png" % (name, i))):
        frames.append(os.path.join(USER, "%s-%d.png" % (name, i)))
        i += 1
    return frames


def shrink(path, scale):
    im = Image.open(path).convert("RGB")
    return im.resize((int(im.size[0] * scale), int(im.size[1] * scale)), Image.LANCZOS)


def crop_centre(path):
    im = Image.open(path).convert("RGB")
    cx, cy = im.size[0] // 2, im.size[1] // 2
    return im.crop((cx - CROP[0], cy - CROP[1], cx + CROP[0], cy + CROP[1]))


def main():
    rows = []
    if os.path.exists(ICON):
        icon = Image.open(ICON).convert("RGBA")
        rows.append(("card icon (starfall-light.png, x3)", [icon.resize((icon.size[0] * 3, icon.size[1] * 3), Image.NEAREST)]))

    for form, label in FORMS:
        pc = burst("starfall-%s-pc" % form)
        phone = burst("starfall-%s-phone" % form)
        if not pc and not phone:
            print("  (no captures for %s - skipped)" % form)
            continue
        ims = []
        if pc:
            ims.append(crop_centre(pc[-1]))
            ims += [shrink(p, PC_SCALE) for p in pc[:2]]
        if phone:
            ims.append(shrink(phone[-1], PHONE_SCALE))
        rows.append((label, ims))

    for screen, label in SCREENS:
        ims = []
        for canvas, scale in (("pc", PC_SCALE * 1.2), ("phone", PHONE_SCALE * 1.2)):
            frames = burst("starfall-ui-%s-%s" % (screen, canvas))
            if frames:
                ims.append(shrink(frames[0], scale))
        if not ims:
            print("  (no captures for the %s - skipped)" % screen)
            continue
        rows.append((label, ims))

    width = max([sum(i.size[0] + 8 for i in ims) for _l, ims in rows] + [200])
    height = sum(max(i.size[1] for i in ims) + 26 for _l, ims in rows) + 10

    sheet = Image.new("RGB", (width + 20, height + 20), BG)
    draw = ImageDraw.Draw(sheet)
    y = 10
    for label, ims in rows:
        draw.text((10, y), label, fill=INK)
        x = 10
        for im in ims:
            if im.mode == "RGBA":
                sheet.paste(im, (x, y + 14), im)
            else:
                sheet.paste(im, (x, y + 14))
            x += im.size[0] + 8
        y += max(i.size[1] for i in ims) + 26

    sheet.save(OUT)
    print("wrote %s (%dx%d)" % (OUT, sheet.size[0], sheet.size[1]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
