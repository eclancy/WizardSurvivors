# -*- coding: utf-8 -*-
"""Contact sheet of the Familiar - tools/art/_familiar_sheet.png (gitignored).

Top: the card icon, enlarged, and the seven forms' idle frames from tools/art/familiar.py at x2.
Then one row per capture set, read from the _UiShot captures in Godot's user:// folder: a 1:1 crop
round the player from the PC capture (the hounds are small, and a shrunk frame hides exactly what is
being judged), two PC frames shrunk, and one phone frame. Last, the screens the spell appears on: the
level-up offer, the level 4 mutation page, the level 8 ascension page and the shop. Missing captures
are skipped, not faked.

The captures (one Godot at a time; --no-save-writes disables saving before the save loads, which
_UiShot's own switch is too late for):

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- --no-save-writes \\
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=0 --start-spell=familiar \\
      --demo-spell-level=4 --demo-evolutions=familiar_great_hound \\
      --spell-stage=16,220,0 --fx-lead=150 --frames=400 --burst=4,10 --shot=familiar-greathound-pc
  (phone: --resolution 540x1212 ... --portrait --safe-area --shot=familiar-greathound-phone)

  sets: heel (level 1, the crowd placed 900 px off so the hound stays at heel), base (level 1),
  greathound / rimehound / littermates (level 4: familiar_great_hound / _rimehound / _littermates),
  cerberus (level 8, great_hound + cerberus), griffin (level 8, littermates + griffin),
  cerberus-rime and griffin-rime (level 8 after rimehound). A start spell that rolls Legendary is
  tinted gold - re-take it.
  screens: familiar-ui-card-<pc|phone> (--force-levelup --keep-levelup --mixed-deal: the Familiar's
  upgrade card beside a new spell and a boon), familiar-ui-mutation-* (--show-mutation),
  familiar-ui-ascension-* (--show-ascension), familiar-ui-shop-* (--scene=res://scenes/MainMenu.tscn
  --demo-shop --press-late=ArcaneUpgradesButton, and --scroll=900 on the PC, --scroll=1250 on the
  phone, to bring the Familiar - the last spell for sale - into view).

  python tools/art/familiar_sheet.py
"""
from __future__ import print_function

import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(HERE, "_familiar_sheet.png")
ICON = os.path.join(ROOT, "assets", "bonelight", "ui", "spells", "familiar-arcane.png")
CHAR = os.path.join(ROOT, "assets", "bonelight", "characters")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

BG = (11, 15, 24)
INK = (220, 232, 255)
FORMS = ["hound", "greathound", "rimehound", "cerberus", "cerberus-rime", "griffin", "griffin-rime"]
SETS = [
    ("heel", "level 1 - at heel, nothing in range"),
    ("base", "level 1 - the hound in a crowd"),
    ("greathound", "level 4 - Great Hound (+1 Arcane, jaws 60% wider)"),
    ("rimehound", "level 4 - Rimehound (+1 Ice, bites slow)"),
    ("littermates", "level 4 - Littermates (+1 hound)"),
    ("cerberus", "level 8 - Great Hound + Cerberus (three heads, three bites)"),
    ("griffin", "level 8 - Littermates + Griffin (it swoops)"),
    ("cerberus-rime", "level 8 - Rimehound + Cerberus"),
    ("griffin-rime", "level 8 - Rimehound + Griffin"),
]
SCREENS = [
    ("card", "the level-up offer"),
    ("mutation", "level 4 mutation page"),
    ("ascension", "level 8 ascension page"),
    ("shop", "Arcane Upgrades - New spells"),
]
PC_SCALE = 0.42
PHONE_SCALE = 0.3
CROP = (260, 170)


def burst(name):
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
    cx, cy = im.size[0] // 2 + 60, im.size[1] // 2
    return im.crop((cx - CROP[0], cy - CROP[1], cx + CROP[0], cy + CROP[1]))


def main():
    rows = []
    top = []
    if os.path.exists(ICON):
        icon = Image.open(ICON).convert("RGBA")
        top.append(icon.resize((icon.size[0] * 2, icon.size[1] * 2), Image.NEAREST))
    for form in FORMS:
        path = os.path.join(CHAR, "familiar-%s-idle.png" % form)
        if os.path.exists(path):
            strip = Image.open(path).convert("RGBA")
            cell = strip.size[1]
            first = strip.crop((0, 0, cell, cell))
            top.append(first.resize((cell * 2, cell * 2), Image.NEAREST))
    if top:
        rows.append(("card icon, and every form's idle frame at x2", top))

    for name, label in SETS:
        pc = burst("familiar-%s-pc" % name)
        phone = burst("familiar-%s-phone" % name)
        if not pc and not phone:
            print("  (no captures for %s - skipped)" % name)
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
            frames = burst("familiar-ui-%s-%s" % (screen, canvas))
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
