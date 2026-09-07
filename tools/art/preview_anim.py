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
BLINKS = {6: 2, 7: 2, 19: 4, 20: 4}   # frame -> blink index, held for two frames


def layer(name):
    return Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")


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

    frames[0].save(out, save_all=True, append_images=frames[1:],
                   duration=FLAME_MS, loop=0, optimize=True)
    print("wrote %s (%d frames, %.1fs loop)" % (out, len(frames), FRAMES * FLAME_MS / 1000.0))


if __name__ == "__main__":
    main()
