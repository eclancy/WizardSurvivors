# -*- coding: utf-8 -*-
"""Contact sheet of the in-run loadout strip - tools/art/_hud_loadout_sheet.png (gitignored).

Run after the _UiShot captures it reads (all written to Godot's user:// folder), one Godot at a
time. The strip is scripts/LoadoutHud.cs; .ai/technical-architecture.md, "The in-run HUD".

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- \
      --scene=res://scenes/node_2d_game.tscn --stage=1 --frames=600 --timescale=3 \
      --give-relics=blank_folio --hud-milestones \
      --grant-boons=lantern_oil,mossgrown_charm,thornseed,iron_rivets --shot=hudload-pc-dungeon
  ... --resolution 1920x1080 ... --stage=8 --fill-book --grant-boons=lantern_oil,mossgrown_charm \
      --shot=hudload-pc-sketch
  ... --resolution 540x1212 --portrait --safe-area --touch-controls ... --stage=3 \
      --give-relics=blank_folio --hud-milestones --grant-boons=... --shot=hudload-phone-swamp
  ... --resolution 1280x720 ... --stage=3 --shot=hudload-pc-early        (a fresh book: one spell)
  ... --resolution 540x1212 --portrait --safe-area --touch-controls ... --stage=1 --fill-book \
      --shot=hudload-phone-dungeon

  python tools/art/hud_loadout_sheet.py

Top: every shot at a common height, so the strip can be judged against the whole field - is it
out of the way of the action, the swarm, the stick zone. Below: the HUD corner of each, cropped
and enlarged x3 (nearest), which is where the icons, rims, numerals and chips are read.
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "_hud_loadout_sheet.png")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

ZOOM = 3
ROW_HEIGHT = 420
BG = (11, 15, 24)
INK = (220, 232, 255)
GAP = 12

# (shot name, label, HUD corner crop in screen pixels)
# The PC corner: the health bar, XP bar and the strip's column (250 canvas units wide, 84..~200).
# At 1920x1080 the canvas is scaled x1.5. The phone corner: the top of the screen down past the
# touch band's floor (176 canvas units + the safe-area top, at 0.75 screen px per unit).
SHOTS = [
    ("hudload-pc-dungeon", "Cursed Dungeon, 1280x720 - full book, 3 milestones, 9 tags", (0, 0, 270, 200)),
    ("hudload-pc-sketch", "The Sketchbook, 1920x1080 - full book on white paper", (0, 0, 405, 300)),
    ("hudload-pc-early", "Blighted Swamp, 1280x720 - a fresh book, one spell", (0, 0, 270, 200)),
    ("hudload-phone-swamp", "Blighted Swamp, Pixel 9 - full book, 3 milestones, touch band", (0, 0, 540, 215)),
    ("hudload-phone-dungeon", "Cursed Dungeon, Pixel 9 - full book, touch band", (0, 0, 540, 215)),
]


def load(name):
    path = os.path.join(USER, name + ".png")
    return Image.open(path).convert("RGB") if os.path.exists(path) else None


def main():
    shots = []
    for name, label, box in SHOTS:
        img = load(name)
        if img is None:
            print("missing: %s (run the _UiShot line for it first)" % name)
            continue
        shots.append((img, label, box))
    if not shots:
        print("nothing to lay out")
        return 1

    # Row one: every shot at ROW_HEIGHT, keeping its aspect.
    scaled = []
    for img, label, box in shots:
        w = int(img.size[0] * ROW_HEIGHT / float(img.size[1]))
        scaled.append(img.resize((w, ROW_HEIGHT), Image.LANCZOS))
    # Row two: the corners, enlarged with nearest so the pixel art stays pixel art.
    corners = []
    for img, label, box in shots:
        crop = img.crop(box)
        corners.append(crop.resize((crop.size[0] * ZOOM, crop.size[1] * ZOOM), Image.NEAREST))

    width = max(sum(s.size[0] for s in scaled), sum(c.size[0] for c in corners)) + GAP * (len(shots) + 1)
    corner_height = max(c.size[1] for c in corners)
    height = GAP + 16 + ROW_HEIGHT + GAP + 16 + corner_height + GAP
    sheet = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(sheet)

    x = GAP
    y = GAP
    for (img, label, box), s in zip(shots, scaled):
        draw.text((x, y), label, fill=INK)
        sheet.paste(s, (x, y + 16))
        x += s.size[0] + GAP
    y += 16 + ROW_HEIGHT + GAP
    x = GAP
    for (img, label, box), c in zip(shots, corners):
        draw.text((x, y), "x%d: %s" % (ZOOM, label.split(" - ")[0]), fill=INK)
        sheet.paste(c, (x, y + 16))
        x += c.size[0] + GAP

    sheet.save(OUT)
    print("wrote %s (%dx%d, %d shots)" % (OUT, width, height, len(shots)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
