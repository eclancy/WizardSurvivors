# -*- coding: utf-8 -*-
"""Contact sheet of the status marks in the running game - tools/art/_status_sheet.png (gitignored).

Run after the _UiShot captures it reads (all written to Godot's user:// folder):

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- \
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=2 --frames=1200 --timescale=2 \
      --status-showcase --shot=status-pc
  ... --status-boss --shot=status-boss
  (and the showcase again at --resolution 540x1212 ... --portrait --safe-area --shot=status-phone)
  ... --status-all --stress-swarm --shot=status-crowd-pc
  "$GODOT_BIN" --path . --resolution 540x1212 scenes/_UiShot.tscn -- ... --portrait --safe-area \
      --status-all --stress-swarm --shot=status-crowd-phone

  python tools/art/status_sheet.py

Top: each showcase enemy cropped and enlarged x3 (nearest), labelled with what it is wearing,
read from the <shot>.status.json _UiShot writes beside the picture. Below: the crowd shots at
their real size, which is the test that matters - whether a debuffed swarm is still a swarm.
"""
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "_status_sheet.png")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

ZOOM = 3
BG = (11, 15, 24)
INK = (220, 232, 255)


def load(name):
    path = os.path.join(USER, name + ".png")
    return Image.open(path).convert("RGB") if os.path.exists(path) else None


def crops(name):
    im = load(name)
    meta = os.path.join(USER, name + ".status.json")
    if im is None or not os.path.exists(meta):
        print("  (no %s - skipped)" % name)
        return []
    data = json.load(open(meta))
    out = []
    for e in data["enemies"]:
        x, y = e["x"], e["y"]
        top, bottom = e["top"], e["bottom"]
        height = bottom - top
        # Room above the head for the marks that sit there, and a body-width either side.
        box = (int(x - height * 0.95), int(y + top - height * 0.55),
               int(x + height * 0.95), int(y + bottom + height * 0.2))
        tile = im.crop(box)
        tile = tile.resize((tile.size[0] * ZOOM, tile.size[1] * ZOOM), Image.NEAREST)
        out.append(("%s: %s  (%s)" % (name.replace("status-", ""), e["name"], e["type"]), tile))
    return out


def main():
    tiles = crops("status-pc") + crops("status-phone") + crops("status-boss")[-1:]
    crowds = [c for c in (load("status-crowd-pc"), load("status-crowd-phone")) if c is not None]
    if not tiles and not crowds:
        print("nothing to compose - take the _UiShot captures first (see the docstring)")
        return 1

    pad, label = 12, 18
    cols = 4
    # Normalise tile heights per row so the grid is regular; boss crops are bigger and get their own row.
    rows = []
    for i in range(0, len(tiles), cols):
        rows.append(tiles[i:i + cols])
    grid_w = 0
    grid_h = 0
    for row in rows:
        grid_w = max(grid_w, sum(t.size[0] for _, t in row) + pad * (len(row) + 1))
        grid_h += max(t.size[1] for _, t in row) + label + pad
    crowd_w = sum(c.size[0] for c in crowds) + pad * (len(crowds) + 1)
    crowd_h = max([c.size[1] for c in crowds] or [0]) + label + pad
    W = max(grid_w, crowd_w)
    H = grid_h + crowd_h + pad * 2
    sheet = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(sheet)

    y = pad
    for row in rows:
        x = pad
        h = max(t.size[1] for _, t in row)
        for name, t in row:
            draw.text((x, y), name, fill=INK)
            sheet.paste(t, (x, y + label))
            x += t.size[0] + pad
        y += h + label + pad

    x = pad
    y += pad
    for c, title in zip(crowds, ("crowd, PC 1280x720, --status-all", "crowd, phone 540x1212 portrait")):
        draw.text((x, y), title, fill=INK)
        sheet.paste(c, (x, y + label))
        x += c.size[0] + pad

    sheet.save(OUT)
    print("wrote %s (%dx%d)" % (os.path.relpath(OUT, os.path.dirname(os.path.dirname(HERE))), W, H))
    return 0


if __name__ == "__main__":
    sys.exit(main())
