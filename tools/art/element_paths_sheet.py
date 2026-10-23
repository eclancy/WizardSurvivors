# -*- coding: utf-8 -*-
"""Contact sheet of the level 4 element paths in the running game - tools/art/_element_paths_sheet.png
(gitignored). Each row is one spell: its plain level 4 form on the left, the same spell after the
branch that adds a second element on the right, two moments of each (in flight / on the crowd).

Take the captures first, one Godot at a time, PC canvas. For every SPELL / PATH pair in ROWS below:

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- \
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=0 --start-spell=SPELL \
      --demo-spell-level=4 --spell-stage=12 --frames=360 --burst=8,2 --shot=elempath-NAME-base
  ... the same with --demo-evolutions=PATH and --shot=elempath-NAME-path

(--character=0 is the Test Wizard, who opens on --start-spell; --spell-stage holds a knot of
unkillable skeletons 230 px to his right; _UiShot never writes the real save.) Chain Lightning's arc
lasts 0.09 s, so take it with --fx-lead=1 --burst=8,1; Hollow Star with --burst=8,14 to reach its
collapse; Bramble Seed with --burst=6,40 to watch it grow. Then:

  python tools/art/element_paths_sheet.py
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "_element_paths_sheet.png")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

ZOOM = 2
BG = (11, 15, 24)
INK = (220, 232, 255)
SOFT = (150, 162, 190)

# name, title, path line, crop box on the 1280x720 capture, burst frames for base, for path.
ROWS = [
    ("riptide", "Riptide (Water)", "Brackish Tide: +1 Poison - hits poison 3 dmg/s for 3 s",
     (600, 270, 920, 470), (1, 4), (1, 5)),
    ("cinder", "Cinderbreath (Fire)", "Hellbreath: +1 Darkness - hits Vulnerable +10% for 2 s",
     (520, 250, 900, 470), (5, 9), (5, 9)),
    ("star", "Hollow Star (Darkness)", "Eclipse: +1 Light - collapse Weakens 30% for 3 s",
     (580, 220, 960, 490), (3, 7), (3, 7)),
    ("chain", "Chain Lightning (Lightning)", "Storm Surge: +1 Water - forks 3 ways, hits slow 25% for 1.5 s",
     (520, 250, 960, 470), (0, 5), (0, 5)),
    ("bramble", "Bramble Seed (Grass)", "Ironbriar: +1 Metal - first hit Withers 5% max HP",
     (440, 220, 900, 500), (3, 5), (3, 5)),
    ("ward", "Kindled Ward (Light)", "Rimelight: +1 Ice - hits slow 30% for 1.5 s",
     (500, 250, 900, 470), (2, 5), (2, 5)),
    ("palisade", "Iron Palisade (Metal)", "Lightning Rod: +1 Lightning - hits stagger 0.25 s",
     (520, 250, 900, 470), (1, 5), (1, 5)),
    ("contagion", "Contagion (Poison)", "Marsh Fever: +1 Water - infections slow 25% for 3 s",
     (560, 250, 900, 470), (0, 3), (0, 3)),
]


def frame(name, index, box):
    suffix = "" if index == 0 else "-%d" % index
    path = os.path.join(USER, "elempath-%s%s.png" % (name, suffix))
    if not os.path.exists(path):
        return None
    im = Image.open(path).convert("RGB").crop(box)
    return im.resize((im.size[0] * ZOOM, im.size[1] * ZOOM), Image.NEAREST)


def main():
    pad, label = 12, 30
    built = []
    for name, title, line, box, base_frames, path_frames in ROWS:
        base = [frame(name + "-base", i, box) for i in base_frames]
        path = [frame(name + "-path", i, box) for i in path_frames]
        if any(t is None for t in base + path):
            print("  (no %s captures - skipped)" % name)
            continue
        built.append((title, line, base, path))
    if not built:
        print("nothing to compose - take the _UiShot captures first (see the docstring)")
        return 1

    W = max(sum(t.size[0] for t in b + p) for _, _, b, p in built) + pad * 6
    H = sum(max(t.size[1] for t in b + p) + label + pad for _, _, b, p in built) + pad * 2 + 20
    sheet = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((pad, pad), "Level 4 element paths - left: the plain level 4 spell, right: after the branch that adds an element (PC 1280x720, x2)", fill=INK)

    y = pad + 20
    for title, line, base, path in built:
        x = pad
        draw.text((x, y), title + "  - level 4, no branch", fill=SOFT)
        draw.text((x + sum(t.size[0] for t in base) + pad * 3, y), line, fill=INK)
        top = y + label - 12
        for t in base:
            sheet.paste(t, (x, top))
            x += t.size[0] + pad
        x += pad * 2
        for t in path:
            sheet.paste(t, (x, top))
            x += t.size[0] + pad
        y += max(t.size[1] for t in base + path) + label + pad

    sheet.save(OUT)
    print("wrote %s (%dx%d)" % (os.path.relpath(OUT, os.path.dirname(os.path.dirname(HERE))), W, H))
    return 0


if __name__ == "__main__":
    sys.exit(main())
