# -*- coding: utf-8 -*-
"""Before/after sheet for Meteor Swarm's look - tools/art/_meteor_look_sheet.png (gitignored).

"meteor swarm should look like the meteors on the web based demo version. it does not look as good
on this version" (2026-10-25). Three bands:

  1. THE WEB DEMO, re-drawn here from the numbers in SurviveDemo.tsx (the canvas mini-game on the
     site): its ground, the wizard, a few runners, and one level 2 volley - four meteors, 0.12 s apart,
     each a 4/4/3/3 px fire-ramp streak from 60 left and 140 up, landing at t 0.7 as the meteor-impact
     crater at 0.9 scale. Shown x2, as the page shows its 480x320 canvas. Its red dotted ellipse is
     drawn because the demo draws it; the game deliberately does not (no area markers on the
     player's spells, .ai/art-direction.md section 3).
  2. BEFORE (main): _UiShot captures named mtrB-<pc|phone>.
  3. AFTER: mtrA-<pc|phone>, and mtrA-<pc|phone>-8 (level 8, Meteor Shower + Orbital Bombardment).

The demo's sprites are read from the site's repo if it sits beside this one; without it the band is
skipped, not faked. Captures (one Godot at a time; a burst of one volley against a held crowd):

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- \\
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=0 --start-spell=meteor_swarm \\
      --spell-stage=24,230,0 --frames=300 --burst=30,3 --shot=mtrA-pc
  (phone: --resolution 540x1212 ... --portrait --safe-area --spell-stage=24,0,170 --shot=mtrA-phone;
   level 8: --demo-spell-level=8 --demo-evolutions=meteor_cluster,meteor_orbital_bombardment)

  python tools/art/meteor_look_sheet.py
"""
import math
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(HERE, "_meteor_look_sheet.png")
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")
SITE = os.path.join(os.path.dirname(ROOT), "EricClancyEngineeringAndCrafts", "public", "games",
                    "wizard-survivors", "sprites")

BG = (11, 15, 24)
INK = (220, 232, 255)
DIM = (140, 150, 170)

W, H = 480, 320
FIRE_RAMP = ["#B8381A", "#E87A1A", "#FFC848", "#FFF4D0"]


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# --- the demo, from SurviveDemo.tsx ---------------------------------------------------------------

def paint_ground():
    """paintGround(): dithered lichen stone, moss tufts, the dim gold ring, the moonlit falloff."""
    im = Image.new("RGB", (W, H), hex_rgb("#1E3640"))
    px = im.load()
    seed = [7]

    def rnd():
        seed[0] = (seed[0] * 16807) % 2147483647
        return seed[0] / 2147483647.0

    specks = ["#24404A", "#1E3640", "#0B0F18", "#2A4E58"]
    for _ in range(2600):
        c = hex_rgb(specks[int(math.floor(rnd() * len(specks)))])
        x = int(math.floor(rnd() * W))
        y = int(math.floor(rnd() * H))
        px[x, y] = c
    d = ImageDraw.Draw(im)
    for _ in range(26):
        x = int(math.floor(rnd() * W))
        y = int(math.floor(rnd() * H))
        for b in range(5):
            bx = x + b * 2 - 4
            h = 2 + int(math.floor(rnd() * 4))
            c = hex_rgb("#3A6470" if b % 2 else "#5A8A94")
            d.rectangle([bx, y - h, bx, y - 1], fill=c)
        d.rectangle([x - 5, y, x + 4, y], fill=hex_rgb("#05070C"))
    cx, cy = W / 2.0, H / 2.0 + 6
    for r, colour in ((46, "#543F10"), (44, "#8A6A1E"), (34, "#543F10")):
        a = 0.0
        while a < math.pi * 2:
            x, y = int(round(cx + math.cos(a) * r)), int(round(cy + math.sin(a) * r * 0.5))
            if 0 <= x < W and 0 <= y < H:
                px[x, y] = hex_rgb(colour)
            a += 0.012
    for i in range(6):
        a = (i / 6.0) * math.pi * 2 + 0.3
        x, y = int(round(cx + math.cos(a) * 39)), int(round(cy + math.sin(a) * 19.5))
        d.rectangle([x, y, x + 1, y], fill=hex_rgb("#D8B04A"))
    # The radial falloff: transparent at 80 px, rgba(5,7,12,0.45) at 300.
    for y in range(H):
        for x in range(W):
            dd = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            a = min(1.0, max(0.0, (dd - 80.0) / 220.0)) * 0.45
            if a > 0:
                r, g, b = px[x, y]
                px[x, y] = (int(r * (1 - a) + 5 * a), int(g * (1 - a) + 7 * a), int(b * (1 - a) + 12 * a))
    return im


def frame(canvas, img, size, index, x, y, scale=1.0):
    """frame(): cell `index` of a strip, centred on (x, y), nearest-scaled."""
    count = max(1, int(round(img.size[0] / float(size))))
    i = min(count - 1, max(0, int(math.floor(index))))
    cell = img.crop((i * size, 0, i * size + size, size))
    s = int(round(size * scale))
    if s != size:
        cell = cell.resize((s, s), Image.NEAREST)
    canvas.paste(cell, (int(round(x)) - s // 2, int(round(y)) - s // 2), cell)


def demo_frame(sheets, t_cast):
    """One frame of the demo, t_cast seconds after a level 2 volley was cast."""
    im = paint_ground()
    d = ImageDraw.Draw(im, "RGBA")
    wizard = (240, 166)
    foes = [(318, 150), (334, 176), (352, 158), (300, 192), (366, 196), (322, 128)]
    # 2 + level meteors, each on a foe with the demo's scatter (fixed here so the sheet is stable).
    meteors = [(322, 152), (348, 162), (305, 189), (330, 133)]
    # Telegraphs under everything: the flickering dotted ellipse (the demo's, not the game's).
    for i, (mx, my) in enumerate(meteors):
        t = t_cast - i * 0.12
        if 0 < t < 0.7:
            colour = hex_rgb("#D8242E") if int(math.floor(t * 12)) % 2 else hex_rgb("#8A1220")
            a = 0.0
            while a < math.pi * 2:
                x, y = int(round(mx + math.cos(a) * 24)), int(round(my + math.sin(a) * 12))
                d.rectangle([x, y, x + 1, y], fill=colour)
                a += 0.09
    for (fx, fy) in foes:
        d.rectangle([fx - 7, fy + 5, fx + 6, fy + 7], fill=(5, 7, 12, 140))
    d.rectangle([wizard[0] - 9, wizard[1] + 2, wizard[0] + 8, wizard[1] + 4], fill=(5, 7, 12, 140))
    bodies = [(fy, "foe", fx, fy) for (fx, fy) in foes] + [(wizard[1], "wiz", wizard[0], wizard[1])]
    for _, kind, x, y in sorted(bodies):
        if kind == "foe":
            frame(im, sheets["runner"], 32, int(t_cast * 10) % 8, x, y - 8)
        else:
            frame(im, sheets["idle"], 48, int(t_cast * 6) % 4, x, y - 20)
    d = ImageDraw.Draw(im, "RGBA")
    for i, (mx, my) in enumerate(meteors):
        t = t_cast - i * 0.12
        if 0.25 < t < 0.7:
            k = (t - 0.25) / 0.45
            hx = mx - 60 * (1 - k)
            hy = my - 140 * (1 - k)
            for j in range(4):
                s = 4 - (j >> 1)
                x, y = int(round(hx - j * 3)), int(round(hy - j * 7))
                d.rectangle([x, y, x + s - 1, y + s - 1], fill=hex_rgb(FIRE_RAMP[3 - j]))
        elif 0.7 <= t < 1.05:
            frame(im, sheets["impact"], 64, 0, mx, my - 6, 0.9)
    return im.resize((W * 2, H * 2), Image.NEAREST)


def demo_band():
    paths = {"runner": "runner-moving.png", "idle": "stormcaller-idle.png", "impact": "meteor-impact.png"}
    sheets = {}
    for key, name in paths.items():
        p = os.path.join(SITE, name)
        if not os.path.exists(p):
            return None
        sheets[key] = Image.open(p).convert("RGBA")
    frames = []
    for t in (0.42, 0.55, 0.68, 0.80, 0.95):
        im = demo_frame(sheets, t)
        # The middle of the canvas, where the volley falls.
        frames.append((im.crop((340, 0, 860, 520)), "t %.2f s" % t))
    return frames


# --- the game captures ----------------------------------------------------------------------------

# Picks index the burst (0 is the first shot, k is <name>-k.png), chosen by eye: the streak in the
# sky, the volley landing, the crater and debris. A missing capture set is skipped.
PC_CROP = (500, 20, 1120, 560)        # of 1280x720: the player at 640,360, the crowd 230 px east
PHONE_CROP = (40, 330, 500, 860)      # of the 540x1212 portrait capture: the crowd 170 px south
SETS = [
    ("mtrB-pc", "BEFORE, PC 1280x720 - the crater sprite slid straight down, a sparse spray on landing", PC_CROP, [26, 27, 28, 29, 4]),
    ("mtrA-pc", "AFTER, PC 1280x720 - the streak, the volley one after another, the crater flash and rock", PC_CROP, [0, 1, 2, 3, 4]),
    ("mtrB-phone", "BEFORE, phone 540x1212 portrait", PHONE_CROP, [4, 5, 6, 7, 8]),
    ("mtrA-phone", "AFTER, phone 540x1212 portrait", PHONE_CROP, [0, 1, 2, 3, 4]),
    ("mtrA-pc-8", "AFTER, level 8 Meteor Shower + Orbital Bombardment (19 meteors a volley, +30% area, the evolution tint) - PC", PC_CROP, [1, 2, 11, 13, 20]),
    ("mtrA-phone-8", "AFTER, level 8 Meteor Shower + Orbital Bombardment - phone", PHONE_CROP, [1, 2, 4, 13, 21]),
    ("mtrA-pc-magma", "AFTER, level 4 Magma Craters - the crater burns for 4 s, under the pack - PC", PC_CROP, [2, 3, 8, 12, 16]),
]
PICKS_FILE = os.path.join(HERE, "_meteor_look_picks.txt")


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


def picks_override():
    """Optional `name: a,b,c,d` lines, so picks can be tuned without editing this file."""
    out = {}
    if os.path.exists(PICKS_FILE):
        for line in open(PICKS_FILE):
            if ":" in line:
                k, v = line.split(":", 1)
                out[k.strip()] = [int(x) for x in v.split(",") if x.strip()]
    return out


def main():
    rows = []
    demo = demo_band()
    if demo:
        rows.append(("THE WEB DEMO (SurviveDemo.tsx, redrawn from its numbers, shown x2) - its red ring is "
                     "not reproduced in the game", [im for im, _ in demo], [lab for _, lab in demo], 0.5))
    override = picks_override()
    for name, label, crop, picks in SETS:
        frames = burst(name)
        if not frames:
            continue
        picks = override.get(name, picks)
        if picks is None:
            n = len(frames)
            picks = [int(round(i * (n - 1) / 4.0)) for i in range(5)]
        ims, labs = [], []
        for p in picks:
            if 0 <= p < len(frames):
                ims.append(Image.open(frames[p]).convert("RGB").crop(crop))
                labs.append("#%d" % p)
        rows.append((label, ims, labs, 0.5 if "phone" not in name else 0.55))

    margin, gap, title_h = 12, 6, 18
    width = 0
    heights = []
    for label, ims, labs, scale in rows:
        w = sum(int(im.size[0] * scale) for im in ims) + gap * (len(ims) - 1)
        width = max(width, w)
        heights.append(max(int(im.size[1] * scale) for im in ims) + title_h + 14)
    sheet = Image.new("RGB", (width + margin * 2, sum(heights) + margin * 2), BG)
    d = ImageDraw.Draw(sheet)
    y = margin
    for (label, ims, labs, scale), h in zip(rows, heights):
        d.text((margin, y), label, fill=INK)
        x = margin
        for im, lab in zip(ims, labs):
            w2, h2 = int(im.size[0] * scale), int(im.size[1] * scale)
            sheet.paste(im.resize((w2, h2), Image.BILINEAR), (x, y + title_h))
            d.text((x + 3, y + title_h + 2), lab, fill=DIM)
            x += w2 + gap
        y += h
    sheet.save(OUT)
    print("wrote %s (%dx%d, %d rows)" % (OUT, sheet.size[0], sheet.size[1], len(rows)))


if __name__ == "__main__":
    main()
