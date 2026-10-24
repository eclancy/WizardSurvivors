# -*- coding: utf-8 -*-
"""Before/after sheet for the no-area-markers pass - tools/art/_aoe_markers_sheet.png (gitignored).

"remove aoe markers from abilities, their animations should make it clear what the hitbox is", and
"does the grave ability do anything?". One row per spell that changed: three frames of one cast
before and three after, at PC size, then the same at phone size - cropped to the middle of the
screen, where the spell stage stands. Read from the _UiShot captures in Godot's user:// folder;
missing captures are skipped, not faked. Top: the Gravewell art strips, enlarged.

The captures (one Godot at a time; each is --burst frames of one cast against a held crowd). A set
is named <prefix>-<spell>-<pc|phone>, prefix aoeB taken before the change and aoeA after, e.g.

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- \\
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=0 --start-spell=gravewell \\
      --spell-stage=24,170,0 --frames=420 --burst=14,10 --shot=aoeA-gravewell-pc
  (phone: --resolution 540x1212 ... --portrait --safe-area --spell-stage=24,0,170 --shot=aoeA-gravewell-phone)

The full list of sets, with their flags, is SETS below; a capture script that takes all of them is
the loop in the commit that added this file. A start spell that rolls Legendary is tinted gold.

  python tools/art/aoe_markers_sheet.py
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(HERE, "_aoe_markers_sheet.png")
FX = os.path.join(ROOT, "assets", "bonelight", "effects")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

BG = (11, 15, 24)
INK = (220, 232, 255)
DIM = (140, 150, 170)

# (set name, label, before picks, after picks), a pick list or None for three evenly spaced. Picks
# index the burst: 0 is the first shot, k is <name>-k.png. Chosen by eye, PC and phone alike, for the
# frames that show the cast - the two sets were taken in separate runs, so the moments differ.
SETS = [
    ("gravewell", "Gravewell, level 1 - faint disc + ring  ->  a grave that opens, waits and caves in", [0, 6, 11], [1, 2, 8]),
    ("gravewell8", "Gravewell, level 8 (Amplified Impact + Titan Cataclysm)", [0, 6, 13], [1, 2, 7]),
    ("spores", "Toxic Spore Burst - swell ring + flash ring  ->  spores thrown out to the rim", [2, 4, 5], [1, 3, 10]),
    ("sporesrot", "Toxic Spore Burst, Lingering Rot - disc + rim under the cloud  ->  the cloud's own puffs", [2, 3, 7], [1, 2, 6]),
    ("sporesember", "Toxic Spore Burst, Ember Caps - disc + rim per patch  ->  flames filling each patch", [2, 3, 8], [1, 2, 6]),
    ("hollowstar", "Hollow Star - reach ring + collapse ring  ->  arms to the reach, motes dragged in, debris", [3, 7, 9], [1, 3, 10]),
    ("bramble", "Bramble Seed - ground disc + rim  ->  the roots alone", [1, 4, 8], [0, 2, 7]),
    ("kindled", "Kindled Ward - halo the size of the strike  ->  a flare to the strike radius on every strike", [1, 3, 9], [2, 4, 9]),
    ("mirefoot", "Mirefoot - stroked rim  ->  the bog, a deeper middle and a bubble", [0, 1, 4], [0, 1, 3]),
    ("meteor", "Meteor Swarm - two red telegraph rings  ->  the falling meteor, then burning rock to the radius", [0, 1, 16], [6, 7, 8]),
    ("cone", "Cone of Cold - translucent wedge + arc  ->  ice sprayed out to the reach", [5, 13, 14], [6, 9, 17]),
    ("longwinter", "The Long Winter - translucent disc + arc  ->  the same spray all the way round", [0, 6, 12], [4, 5, 7]),
    ("solarflare", "Solar Flare - squashed to a floor ellipse the hit is not  ->  the circle it hits", [7, 8, 18], [0, 6, 14]),
    ("tentacles", "Black Tentacles - dashed rim + dithered disc  ->  the rift opens to the whole slow", [2, 4, 7], [2, 6, 10]),
    ("sundering", "Sundering - floor disc  ->  the pillars, and earth thrown to the shove radius", [1, 4, 7], [1, 5, 8]),
    ("contagion", "Contagion - ring per jump  ->  spores thrown to the spread radius", [9, 12, 19], [9, 10, 22]),
    ("frostshard", "Frost Shard - Line2D ring per kill (grew past the radius)  ->  ice to the radius", [5, 11, 14], [5, 10, 14]),
    ("cataclysm", "Cataclysm - ember disc + rim per crater  ->  flames filling the crater", [4, 10, 12], [1, 6, 13]),
    ("boonring", "Saltbound Chain / Rimebriar - Line2D ring (grew past the radius)  ->  shards to the radius", [0, 4, 14], [0, 8, 14]),
    ("gravewellrun", "Gravewell in an ordinary run, level 4 - pits on empty ground  ->  pits on the swarm's path", None, [5, 10, 15]),
]

# The middle of each screen, where the player and the stage stand.
PC_CROP = (240, 90, 1040, 630)          # of 1280x720
PHONE_CROP = (0, 330, 540, 930)         # of the 540x1212 portrait capture
PC_SCALE = 0.36
PHONE_SCALE = 0.32
STRIPS = [("gravewell-pit.png", 3), ("gravewell-grasp.png", 4)]


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


def pick(frames, picks, n=3):
    if picks:
        return [frames[k] for k in picks if k < len(frames)]
    if len(frames) <= n:
        return frames
    step = (len(frames) - 1) / float(n - 1)
    return [frames[int(round(k * step))] for k in range(n)]


def shot(path, crop, scale):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    box = (min(crop[0], w), min(crop[1], h), min(crop[2], w), min(crop[3], h))
    im = im.crop(box)
    return im.resize((int(im.size[0] * scale), int(im.size[1] * scale)), Image.LANCZOS)


def row_images(name, picks, device):
    crop, scale = (PC_CROP, PC_SCALE) if device == "pc" else (PHONE_CROP, PHONE_SCALE)
    out = []
    for prefix, chosen in zip(("aoeB", "aoeA"), picks):
        frames = burst("%s-%s-%s" % (prefix, name, device))
        out.append([shot(p, crop, scale) for p in pick(frames, chosen)])
    return out


def main():
    strips = []
    for name, zoom in STRIPS:
        path = os.path.join(FX, name)
        if os.path.exists(path):
            im = Image.open(path).convert("RGBA")
            strips.append((name, im.resize((im.size[0] * zoom, im.size[1] * zoom), Image.NEAREST)))

    rows = []
    for name, label, before, after in SETS:
        pc = row_images(name, (before, after), "pc")
        phone = row_images(name, (before, after), "phone")
        if not any(pc) and not any(phone):
            print("  (no captures for %s - skipped)" % name)
            continue
        rows.append((label, pc, phone))

    def width_of(group):
        return sum(i.size[0] + 4 for i in group)

    gap = 24
    width = max([im.size[0] for _n, im in strips] + [1])
    for _l, pc, phone in rows:
        width = max(width, width_of(pc[0]) + gap + width_of(pc[1]) + 40 + width_of(phone[0]) + gap + width_of(phone[1]))
    height = sum(im.size[1] + 20 for _n, im in strips) + 30
    for _l, pc, phone in rows:
        height += max([i.size[1] for g in pc + phone for i in g] + [0]) + 30

    sheet = Image.new("RGB", (width + 20, height + 20), BG)
    draw = ImageDraw.Draw(sheet)
    y = 10
    for name, im in strips:
        draw.text((10, y), name, fill=INK)
        sheet.paste(im, (10, y + 12), im)
        y += im.size[1] + 20
    draw.text((10, y + 4), "each row: PC before | PC after      phone before | phone after", fill=DIM)
    y += 26
    for label, pc, phone in rows:
        draw.text((10, y), label, fill=INK)
        x = 10
        for k, group in enumerate(pc + phone):
            for im in group:
                sheet.paste(im, (x, y + 14))
                x += im.size[0] + 4
            x += gap if k % 2 == 0 else 40
        y += max([i.size[1] for g in pc + phone for i in g] + [0]) + 30

    sheet.save(OUT)
    print("wrote %s (%dx%d)" % (OUT, sheet.size[0], sheet.size[1]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
