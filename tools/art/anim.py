# -*- coding: utf-8 -*-
"""Emit the Vigil title screen as animated layers.

Run: python tools/art/anim.py

A single baked PNG cannot animate a part of itself, so the screen comes apart into seven
layers and four of them get frame sequences. Read .ai/title-screen.md before changing any of
this; the rule that governs all of it is that every state is a GENERATED FRAME with a closed
palette. Nothing is tinted, faded or cross-dissolved, because a blended pixel is not one of
the 66 colours the contract allows and the palette audit would stop meaning anything.

What animates, and off which clock:

  eyes        blink, one pair at a time, on their own irregular timer
  ward        a slow size pulse in the light pools
  flamesA/B   the six flames, four phases - and these two MUST advance together, because
              they are the same six flames with the figure standing between them
  figure      the rim and bounce flicker, phase-LOCKED to the flames: driven separately it
              reads as two unrelated animations rather than as one light source

The flat splash.py output is unaffected - vigil() with layers=False still renders the
shipping PNG byte for byte.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import splash

ROOT = splash.ROOT
OUT = os.path.join(ROOT, "assets", "bonelight", "ui", "title")

# Which eye pair is shut in each blink frame. Frame 0 is all-open and is what shows nearly all
# the time. Indices run 0-8 through the treeline set and 9-15 through the branches, so the
# spread below deliberately takes some of each - sixteen eyes blinking on one clock is a
# lighthouse, not a wood.
BLINKS = [None, 2, 6, 10, 0, 13]
GAINS = [1.00, 0.95, 0.90]          # ward pulse: pool radius, never opacity
FLICKER = [1.00, 0.80, 0.52, 0.34]  # figure edge light, one per flame phase


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    written = []
    bad = 0
    for k in range(6):
        cv = splash.vigil(layers=True, blink=BLINKS[k], gain=GAINS[k % 3],
                          phase=k % 4, flicker=FLICKER[k % 4])
        take = [("eyesA", "eyes-far-%d" % k), ("eyesB", "eyes-near-%d" % k),
                ("ward", "ward-%d" % (k % 3)),
                ("flamesA", "flames-far-%d" % (k % 4)),
                ("flamesB", "flames-near-%d" % (k % 4)),
                ("figure", "figure-%d" % (k % 4))]
        if k == 0:
            take += [("backA", "back-a"), ("backB", "back-b"), ("fore", "fore")]
        for stage, name in take:
            c = cv[stage]
            bad += len(c.audit())
            c.save(os.path.join(OUT, name + ".png"))
            written.append(name)
        print("pass %d: blink=%s gain=%.2f phase=%d flicker=%.2f"
              % (k, BLINKS[k], GAINS[k % 3], k % 4, FLICKER[k % 4]))
    print("wrote %d layers to %s" % (len(set(written)), OUT))
    print("off-contract colours across every layer: %d" % bad)


if __name__ == "__main__":
    main()
