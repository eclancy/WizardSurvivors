# -*- coding: utf-8 -*-
"""The application icon: what the Godot project manager, the taskbar, the .exe and the phone show.

Run: python tools/art/app_icon.py
  -> icon.png and icon.ico at the repo root, and the three Android launcher layers under
     assets/bonelight/ui/icons/ (export_presets.cfg names them)

IT IS DESIGN E OF `icon_concepts.py`, "the burning ward": the staff's orb burning at the centre of
the summoning ward, the same ward the title screen's wizard stands in. Chosen by the user on
2026-10-16 from six drawn candidates (the sheet that script writes), with B ("WS in the ward") and
F ("Surrounded") kept as the liked alternates. To change the icon, change SHIPPING below; to change
the drawing, edit icon_concepts.py. Nothing is drawn here.

Before this the icon was a 192px crop of the title screen wizard. It was the best picture the
project owns, but on a phone the launcher's mask shows only the middle two thirds of an icon, and
the crop lost the staff orb and the ward to it - the parts that made it his.

Why each output is what it is:
  - icon.ico carries the HAND-DRAWN 16 and 24, not reductions. Windows picks an entry by size
    rather than rescaling, and 24 is what a Windows 11 taskbar shows at 100%. PIL's own ICO writer
    resamples one image into every size, which would throw the hand-drawn ones away, so the file
    is written here, PNG-compressed entries in the standard ICONDIR layout.
  - The Android layers are the 108-unit adaptive canvas at x4 (432, Godot's size): the ward on its
    own night as the background, the orb and ring as the foreground, the ring inside the 72-unit
    circle every launcher mask keeps.
  - Every enlargement is an integer factor with nearest sampling, so no pixel becomes a fraction
    of another.
"""
import io
import os
import struct
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import icon_concepts  # noqa: E402

SHIPPING = "E"

OUT_PNG = os.path.join(ROOT, "icon.png")
OUT_ICO = os.path.join(ROOT, "icon.ico")
ICON_DIR = os.path.join(ROOT, "assets", "bonelight", "ui", "icons")
OUT_ANDROID_MAIN = os.path.join(ICON_DIR, "android-icon-main.png")
OUT_ANDROID_FG = os.path.join(ICON_DIR, "android-icon-foreground.png")
OUT_ANDROID_BG = os.path.join(ICON_DIR, "android-icon-background.png")


def up(img, factor):
    return img.resize((img.size[0] * factor, img.size[1] * factor), Image.NEAREST)


def write_ico(path, images):
    """An .ico whose entries are the given images as they are, each stored as a PNG."""
    blobs = []
    for im in images:
        buf = io.BytesIO()
        im.save(buf, "PNG")
        blobs.append(buf.getvalue())
    out = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    for im, blob in zip(images, blobs):
        w, h = im.size
        out += struct.pack("<BBBBHHII", w % 256, h % 256, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    with open(path, "wb") as f:
        f.write(out + b"".join(blobs))


def main():
    d = icon_concepts.build(SHIPPING)
    if d["off"]:
        raise SystemExit("design %s has off-contract colours: %s" % (SHIPPING, d["off"][:4]))
    tile, sizes = d["tile"], d["sizes"]

    # 64 -> 256 for the project manager and the window; the rounded tile, as on the taskbar.
    big = up(tile, 4)
    big.save(OUT_PNG)
    print("wrote %s  %dx%d" % (os.path.relpath(OUT_PNG, ROOT), big.size[0], big.size[1]))

    ico = [sizes[16], sizes[24], sizes[32], sizes[48], tile, up(tile, 2), big]
    write_ico(OUT_ICO, ico)
    print("wrote %s  %s" % (os.path.relpath(OUT_ICO, ROOT), ", ".join(str(i.size[0]) for i in ico)))

    if not os.path.isdir(ICON_DIR):
        os.makedirs(ICON_DIR)
    # The legacy launcher icon (pre-adaptive Android, and some settings screens): the square
    # master, unrounded - the launcher applies its own shape.
    up(d["master"], 3).save(OUT_ANDROID_MAIN)
    up(d["fg"], 4).save(OUT_ANDROID_FG)
    up(d["bg"], 4).save(OUT_ANDROID_BG)
    for p in (OUT_ANDROID_MAIN, OUT_ANDROID_FG, OUT_ANDROID_BG):
        print("wrote %s" % os.path.relpath(p, ROOT))


if __name__ == "__main__":
    main()
