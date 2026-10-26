# -*- coding: utf-8 -*-
"""Contact sheet of the facing family - tools/art/_facing_sheet.png (gitignored).

Stormsweep and Sleet Runes (.ai/spell-roster.md, "The facing family"). Top: both card icons, enlarged.
Then one row per capture set, read from the _UiShot captures in Godot's user:// folder: three frames
of a burst cropped 1:1 round the player at PC size (a sweep lasts a third of a second, so the row is
its beats in order), then one frame at phone size. Last, the screens the spells appear on. Missing
captures are skipped, not faked.

The captures (one Godot at a time; --no-save-writes on every run). A spell is aimed by the way the
player last WALKED, so every capture walks first and then stands - --walk-until is the frame he stops,
and the line _UiShot prints at that frame is Player.FacingDirection surviving the stop. --cast-on-stage
makes the spell's cooldown come due the frame the crowd appears, and --timescale=0.15 slows the burst
enough that its frames are a sweep's beats rather than whatever the PNG writes let through:

  "$GODOT_BIN" --path . --resolution 1280x720 scenes/_UiShot.tscn -- --no-save-writes \\
      --scene=res://scenes/node_2d_game.tscn --stage=0 --character=0 --start-spell=stormsweep \\
      --spell-stage=30,150,0 --walk=1,0 --walk-until=40 --cast-on-stage --fx-lead=3 \\
      --frames=200 --burst=12,1 --timescale=0.15 --shot=sweep-right-pc
  (phone: --resolution 540x1212 ... --portrait --safe-area --shot=sweep-right-phone)

  directions: right / left / up / down - walk that way, the crowd that way (--spell-stage=30,DX,DY).
    Stormsweep goes to the side he FACES (Player.FacingSide); walking up or down it keeps the last side.
  forms (--demo-spell-level=4 or 8, --demo-evolutions=...): sweep-voltage, sweep-iron, sweep-rapid,
    sweep-thunderclap (high_voltage,thunderclap), sweep-cardinal (iron_current,cardinal_storm);
    runes-rime, runes-flurry, runes-farwind, runes-fan (rune_flurry,sigil_fan), runes-gale
    (deep_rime,turning_gale).
  screens: facing-ui-card-<spell>-<canvas> (--start-spell=<id> --force-levelup --keep-levelup
    --mixed-deal), facing-ui-mutation-<spell>-<canvas> (--show-mutation), facing-ui-ascension-
    <spell>-<canvas> (--show-ascension), facing-ui-shop-<canvas> (--scene=res://scenes/MainMenu.tscn
    --demo-shop --press-late=ArcaneUpgradesButton).
  A start spell that rolls Legendary is tinted gold - re-take it.

  python tools/art/facing_sheet.py
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(HERE, "_facing_sheet.png")
ICONS = [os.path.join(ROOT, "assets", "bonelight", "ui", "spells", n + ".png")
         for n in ("stormsweep-lightning", "sleet-runes-ice")]
USER = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Wizard Survivors")

BG = (11, 15, 24)
INK = (220, 232, 255)
FORMS = [
    ("sweep-right", "Stormsweep level 1 - walked right, stopped: sweeps right"),
    ("sweep-left", "Stormsweep level 1 - walked left, stopped: sweeps left"),
    ("sweep-up", "Stormsweep level 1 - walked up after facing right: keeps the side"),
    ("sweep-down", "Stormsweep level 1 - walked down-left: sweeps left"),
    ("sweep-voltage", "Stormsweep level 4 - High Voltage (+1 Lightning)"),
    ("sweep-iron", "Stormsweep level 4 - Iron Current (the Metal path)"),
    ("sweep-rapid", "Stormsweep level 4 - Rapid Discharge"),
    ("sweep-thunderclap", "Stormsweep level 8 - High Voltage + Thunderclap"),
    ("sweep-cardinal", "Stormsweep level 8 - Iron Current + Cardinal Storm"),
    ("runes-right", "Sleet Runes level 4 - walked right, stopped"),
    ("runes-left", "Sleet Runes level 4 - walked left, stopped"),
    ("runes-up", "Sleet Runes level 4 - walked up, stopped"),
    ("runes-down", "Sleet Runes level 4 - walked down, stopped"),
    ("runes-rime", "Sleet Runes level 4 - Deep Rime (+1 Ice)"),
    ("runes-flurry", "Sleet Runes level 4 - Rune Flurry"),
    ("runes-farwind", "Sleet Runes level 4 - Far Wind"),
    ("runes-fan", "Sleet Runes level 8 - Rune Flurry + Sigil Fan"),
    ("runes-gale", "Sleet Runes level 8 - Deep Rime + Turning Gale"),
]
SCREENS = [
    ("card-stormsweep", "level-up card - Stormsweep"),
    ("card-sleet_runes", "level-up card - Sleet Runes"),
    ("mutation-stormsweep", "level 4 page - Stormsweep"),
    ("mutation-sleet_runes", "level 4 page - Sleet Runes"),
    ("ascension-stormsweep", "level 8 page - Stormsweep"),
    ("ascension-sleet_runes", "level 8 page - Sleet Runes"),
    ("shop", "Arcane Upgrades - New spells"),
]
CROP = (300, 150)        # half-size of the 1:1 crop round the screen centre (the player)
CROP_SCALE = 0.62
PHONE_SCALE = 0.3
SCREEN_PC = 0.42
SCREEN_PHONE = 0.32


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


def shrink(im, scale):
    return im.resize((int(im.size[0] * scale), int(im.size[1] * scale)), Image.LANCZOS)


def crop_centre(path):
    im = Image.open(path).convert("RGB")
    cx, cy = im.size[0] // 2, im.size[1] // 2
    return shrink(im.crop((cx - CROP[0], cy - CROP[1], cx + CROP[0], cy + CROP[1])), CROP_SCALE)


def pick(frames, count):
    """count frames spread evenly through a burst, first and last included."""
    if len(frames) <= count:
        return frames
    return [frames[int(round(k * (len(frames) - 1) / float(count - 1)))] for k in range(count)]


def main():
    rows = []
    icons = [Image.open(p).convert("RGBA") for p in ICONS if os.path.exists(p)]
    if icons:
        rows.append(("card icons (stormsweep-lightning, sleet-runes-ice; x3)",
                     [i.resize((i.size[0] * 3, i.size[1] * 3), Image.NEAREST) for i in icons]))

    for form, label in FORMS:
        pc = burst(form + "-pc")
        phone = burst(form + "-phone")
        if not pc and not phone:
            print("  (no captures for %s - skipped)" % form)
            continue
        ims = [crop_centre(p) for p in pick(pc, 4)]
        if phone:
            ims.append(shrink(Image.open(pick(phone, 3)[1 if len(phone) > 1 else 0]).convert("RGB"), PHONE_SCALE))
        rows.append((label, ims))

    for screen, label in SCREENS:
        ims = []
        for canvas, scale in (("pc", SCREEN_PC), ("phone", SCREEN_PHONE)):
            frames = burst("facing-ui-%s-%s" % (screen, canvas))
            if frames:
                ims.append(shrink(Image.open(frames[0]).convert("RGB"), scale))
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
