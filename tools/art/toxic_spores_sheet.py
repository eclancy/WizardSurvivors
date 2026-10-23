# -*- coding: utf-8 -*-
"""Contact sheet of Toxic Spore Burst - tools/art/_toxic_spores_sheet.png (gitignored).

Top: every strip tools/art/spore_mushrooms.py writes, enlarged (nearest). Below: one row per form of
the spell, read from the _UiShot captures in Godot's user:// folder - four frames of one cast at PC
size, then four at phone size. Missing captures are skipped, not faked.

The captures (one Godot at a time; each set is --burst frames of a single cast against a held crowd):

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- \\
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=0 --start-spell=toxic_spore_burst \\
      --demo-spell-level=4 --demo-evolutions=toxic_spore_ember_caps \\
      --spell-stage=30,230,0 --frames=240 --burst=16,6 --shot=spores-ember-pc
  (phone: --resolution 540x1212 ... --portrait --safe-area --spell-stage=30,0,230 --shot=spores-ember-phone)

  forms: base (level 1), rot / ember / giant (level 4, toxic_spore_lingering_rot / _ember_caps /
  _giant_caps), doomcap (level 8, lingering_rot + doomcap), bloom (level 8, ember_caps +
  mycelial_bloom). A start spell that rolls Legendary is tinted gold - re-take it.

  python tools/art/toxic_spores_sheet.py
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(HERE, "_toxic_spores_sheet.png")
FX = os.path.join(ROOT, "assets", "bonelight", "effects")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

BG = (11, 15, 24)
INK = (220, 232, 255)
STRIPS = [
    ("spore-mushroom-poison.png", 4), ("spore-mushroom-fire.png", 4),
    ("spore-giantcap-poison.png", 3), ("spore-giantcap-fire.png", 3),
    ("spore-doomcap-poison.png", 2), ("spore-doomcap-fire.png", 2),
    ("spore-puff-poison.png", 4), ("spore-puff-fire.png", 4), ("spore-ember.png", 4),
]
FORMS = [
    ("base", "level 1"),
    ("rot", "level 4 - Lingering Rot (+1 Poison)"),
    ("ember", "level 4 - Ember Caps (Poison -> Fire)"),
    ("giant", "level 4 - Giant Caps"),
    ("doomcap", "level 8 - Lingering Rot + Doomcap"),
    ("bloom", "level 8 - Ember Caps + Mycelial Bloom (+1 Grass)"),
]
PC_SCALE = 0.42
PHONE_SCALE = 0.3


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


def pick(frames, n=4):
    if len(frames) <= n:
        return frames
    step = (len(frames) - 1) / float(n - 1)
    return [frames[int(round(k * step))] for k in range(n)]


def shrink(path, scale):
    im = Image.open(path).convert("RGB")
    return im.resize((int(im.size[0] * scale), int(im.size[1] * scale)), Image.LANCZOS)


def main():
    rows = []
    for name, zoom in STRIPS:
        path = os.path.join(FX, name)
        if not os.path.exists(path):
            print("  (no %s - run spore_mushrooms.py)" % name)
            continue
        im = Image.open(path).convert("RGBA")
        rows.append((name, im.resize((im.size[0] * zoom, im.size[1] * zoom), Image.NEAREST)))

    shots = []
    for form, label in FORMS:
        pc = [shrink(p, PC_SCALE) for p in pick(burst("spores-%s-pc" % form))]
        phone = [shrink(p, PHONE_SCALE) for p in pick(burst("spores-%s-phone" % form))]
        if not pc and not phone:
            print("  (no captures for %s - skipped)" % form)
            continue
        shots.append((label, pc, phone))

    width = max([im.size[0] for _n, im in rows] + [1])
    for _l, pc, phone in shots:
        width = max(width, sum(i.size[0] + 6 for i in pc) + 20 + sum(i.size[0] + 6 for i in phone))
    height = sum(im.size[1] + 22 for _n, im in rows) + 10
    for _l, pc, phone in shots:
        height += max([i.size[1] for i in pc + phone] + [0]) + 26

    sheet = Image.new("RGB", (width + 20, height + 20), BG)
    draw = ImageDraw.Draw(sheet)
    y = 10
    for name, im in rows:
        draw.text((10, y), name, fill=INK)
        sheet.paste(im, (10, y + 14), im)
        y += im.size[1] + 22
    y += 10
    for label, pc, phone in shots:
        draw.text((10, y), label, fill=INK)
        x = 10
        for im in pc:
            sheet.paste(im, (x, y + 14))
            x += im.size[0] + 6
        x += 20
        for im in phone:
            sheet.paste(im, (x, y + 14))
            x += im.size[0] + 6
        y += max([i.size[1] for i in pc + phone] + [0]) + 26

    sheet.save(OUT)
    print("wrote %s (%dx%d)" % (OUT, sheet.size[0], sheet.size[1]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
