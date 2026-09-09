# -*- coding: utf-8 -*-
"""Composite the title-screen layers into an animated GIF, for looking at the motion.

Run: python tools/art/preview_anim.py [out.gif]

This is a preview, not an asset - it exists so the animation can be judged without launching
the game, and it writes outside assets/ on purpose. It reproduces what scripts/TitleScreen.cs
does at runtime: one clock for the flames and the figure, a slower one for the ward, and the
occasional blink. If the GIF and the running game disagree, the GIF is the one that is wrong.
"""

import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "assets", "bonelight", "ui", "title")

FLAME_MS = 130          # FlameFrameSeconds
WARD_STEPS = [0, 1, 2, 1]
WARD_EVERY = 7          # WardPulseSeconds / FlameFrameSeconds, near enough
FRAMES = 32             # 4.2s, which is close to two full ward breaths
BLINKS = {2: 1, 3: 1, 7: 4, 8: 4, 11: 3, 12: 3, 15: 2, 16: 2,
          20: 5, 21: 5, 24: 1, 25: 1, 28: 3, 29: 3}  # frame -> pair, held two frames


def layer(name):
    return Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")


def palette(frames):
    """Every distinct colour across every frame, as one ordered list.

    The composite uses 39 of the contract's 66 colours and not one of them may be
    approximated, so the GIF has to carry them all in its own palette. 256 is the format's
    ceiling and we are nowhere near it.
    """
    seen = set()
    for f in frames:
        for _, c in f.getcolors(1 << 20):
            seen.add(c)
    pal = sorted(seen)
    if len(pal) > 256:
        raise ValueError("%d colours: too many for a GIF palette" % len(pal))
    return pal


def indexed(im, pal, idx):
    """Map an RGB frame onto `pal` by exact lookup - no quantiser, no dithering.

    This function is the whole reason the preview stopped being trustworthy. Left to itself,
    Pillow converts an RGB frame to P mode for GIF by snapping it to the 216-colour WEB
    palette - every channel a multiple of 51 - and error-diffusion dithering the difference.
    Not one Bonelight colour survives that: #05070C and #0B0F18 both collapse toward black,
    the mid-tones are dragged toward grey, and a 39-colour frame came back with 85 in it.

    Worse, the diffusion is computed per frame and independently, so a pixel that is identical
    in two consecutive frames still lands on different palette entries in each. That turns a
    four-phase flame loop into a picture where every pixel crawls - which reads as a grey,
    flickering screen rather than as four things moving on a still one. The art was never
    wrong; the export was.

    An exact index lookup is lossless, identical frame to frame, and cheap at this size.
    """
    out = Image.new("P", im.size)
    flat = []
    for c in pal:
        flat.extend(c)
    flat.extend([0] * (768 - len(flat)))
    out.putpalette(flat)
    out.putdata([idx[px] for px in im.getdata()])
    return out


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "_title-anim.gif")
    cache = {}

    def L(name):
        if name not in cache:
            cache[name] = layer(name)
        return cache[name]

    frames = []
    for i in range(FRAMES):
        f = i % 4
        w = WARD_STEPS[(i // WARD_EVERY) % len(WARD_STEPS)]
        e = BLINKS.get(i, 0)
        stack = ["back-a", "eyes-far-%d" % e, "back-b", "eyes-near-%d" % e,
                 "ward-%d" % w, "flames-far-%d" % f, "figure-%d" % f,
                 "flames-near-%d" % f, "fore"]
        im = L(stack[0]).copy()
        for n in stack[1:]:
            im = Image.alpha_composite(im, L(n))
        frames.append(im.convert("RGB"))

    pal = palette(frames)
    idx = dict((c, i) for i, c in enumerate(pal))
    frames = [indexed(f, pal, idx) for f in frames]
    frames[0].save(out, save_all=True, append_images=frames[1:],
                   duration=FLAME_MS, loop=0, optimize=True)
    print("wrote %s (%d frames, %.1fs loop, %d exact colours)"
          % (out, len(frames), FRAMES * FLAME_MS / 1000.0, len(pal)))


if __name__ == "__main__":
    main()
