# -*- coding: utf-8 -*-
"""Laelaps, the hound that always caught what it hunted - a playable character. Run: python tools/art/laelaps.py

WHO HE IS. In the Greek story Laelaps was a hound fated never to fail to catch what it hunted, set on
the Teumessian fox, which was fated never to be caught; the chase could not end, so both were turned to
stone where they ran. Here the dark wizard did the same to him on the high heath, among the standing
stones of the Broken Circle, and the black hares that run there are what he was chasing. A player frees
him by finishing the chase for him: the coursing deed (AchievementDefinitions "coursing" - a hound of
the player's own light running down the hares). .ai/world-and-tone.md asks that everyone unlocked after
the four disciplines be a named legend with a life before this, held somewhere by the dark wizard; he is
one - the one who is not a wizard, which is why he is outside the final chapter's gate (UnlockCatalog).

WHAT HE LOOKS LIKE, and why. Drawn by tools/art/familiar.py's hound renderer - the same skeleton, light,
passes and matte finish as the Familiar's hound (Fig.render(finish=True)) - so the two dogs in the game
are one family of drawing, and told apart at a glance by three things:

  - NO HAT. The hat is the wizard's mark on HIS creatures; Laelaps belongs to no one.
  - A BRONZE COAT. Warm - the player's cast is the one thing allowed to be bright - but a saturated
    bronze, not the familiar's pale fawn: amber and flesh in the light, the gold row's dark stops in
    the shade (never its light ones, which the matte finish treats as a light source and would leave
    glinting on a coat).
  - STONE FEET. His paws are still the bone-grey of the stone he was held in.

His collar is plain bronze with a disc, not the familiar's gold star. His eye is the calm eye
(familiar.calm_eye; Eric, 2026-10-28, of the familiar's icon: "the eye is too prominent"), because his
first frame is also his portrait on the character select, shown at x5.

THE CELL. The player is drawn on the 48 cell (world-and-tone.md, "The player at 48x48"); a hound fills
it lengthwise, at the familiar hound's 1.3, paws on the contract's feet row. He faces right, as the
wizards do (CharacterVisuals.SpriteFacesLeft is false for him).

THE PLAYER'S ANIMATION SET (art-direction.md section 5): idle 4, moving 8, hurt 2, death 6 - written as
laelaps-idle/-moving/-hurt/-death.png and scenes/resources/LaelapsFrames.tres, with the idle's frames
also written one by one (laelaps-1..4.png), which is what the character select animates from Portrait.
His death is the story's end: he folds down and turns back to stone.
"""
from __future__ import print_function, division

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bonelight as B
import familiar as F
import spriteframes

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(ROOT, "assets", "bonelight", "characters")
OUT_REL = "assets/bonelight/characters"
FRAMES = os.path.join(ROOT, "scenes", "resources", "LaelapsFrames.tres")
PREVIEW = os.path.join(HERE, "_laelaps.png")

M = B.MATERIALS
CELL = 48
SCALE = 1.3

# The bronze: amber and flesh in the light, the gold row's dark stops in the shade.
BRONZE = [M["flesh"][1], M["amber"][2], M["amber"][3], M["gold"][3], M["gold"][4]]
# The stone he was held in, still on his feet, and all of him as he dies.
STONE_FEET = [M["skin"][1], M["skin"][2], M["skin"][3], M["stone"][0], M["stone"][2]]

# No hat: one head that wears none. Lean and long-legged like the familiar hound, a little deeper in the
# chest; the eye calm; the collar plain.
_ONE_HEAD = [(0.0, 0.0, 0.0, 0.0, False)]
LAELAPS = F.Skin(BRONZE, heads=_ONE_HEAD, leap=0.85, head_scale=1.15, heavy=0.2, paws=STONE_FEET,
                 collar_star=False, calm=True, depth=0.4)
# Turning to stone: the same hound in the stone's grey.
# Its eye is stone too: a lit eye left in a grey head reads as something still looking out of it.
STONE = F.Skin(STONE_FEET, eye=(M["stone"][2], M["stone"][3]), heads=_ONE_HEAD, leap=0.85, head_scale=1.15, heavy=0.2,
               collar_star=False, calm=True, depth=0.2)

P = F._pose


def _gallop8():
    return [F.gallop(k, 8) for k in range(8)]


IDLE = [P(tail=0.1, breathe=0.0), P(tail=0.3, breathe=0.6, ear=0.1), P(tail=0.45, breathe=1.0, ear=0.2),
        P(tail=0.3, breathe=0.5, ear=0.1)]
# Struck: he flinches down and away, head pulled back - then half recovers.
HURT = [P(crouch=1.6, head=(-0.6, 0.6, -0.25), jaw=0.3, tail=-0.4, ear=-0.6, swing=(-0.3, -0.2, 0.3, 0.2)),
        P(crouch=0.7, head=(-0.3, 0.3, -0.1), jaw=0.1, tail=-0.1, ear=-0.3)]
# Falling: he folds down onto his legs, lays his head down - and the stone comes back over him.
DEATH = [P(crouch=1.4, head=(0.2, 1.4, 0.2), tail=-0.3, ear=-0.4),
         P(crouch=3.2, head=(0.4, 2.8, 0.35), tail=-0.5, ear=-0.6),
         P(crouch=5.0, head=(0.6, 4.2, 0.5), tail=-0.6, ear=-0.7),
         P(crouch=6.2, head=(0.8, 5.0, 0.55), tail=-0.7, ear=-0.8),
         P(crouch=6.2, head=(0.8, 5.0, 0.55), tail=-0.7, ear=-0.8),
         P(crouch=6.2, head=(0.8, 5.0, 0.55), tail=-0.7, ear=-0.8)]

ANIMS = [
    ("idle", [(LAELAPS, p) for p in IDLE], True, 6.0),
    ("moving", [(LAELAPS, p) for p in _gallop8()], True, 12.0),
    ("hurt", [(LAELAPS, p) for p in HURT], False, 12.0),
    # The last two frames of the fall are the stone.
    ("death", [(LAELAPS, p) for p in DEATH[:4]] + [(STONE, p) for p in DEATH[4:]], False, 8.0),
]


def frames():
    out = []
    for name, poses, loop, speed in ANIMS:
        out.append((name, [F.hound(CELL, skin, p, SCALE).render(finish=True) for skin, p in poses], loop, speed))
    return out


def main(write=True):
    anims = frames()
    bad = 0
    for name, cvs, _l, _s in anims:
        for c in cvs:
            off = c.audit()
            if off:
                bad += 1
                print("OFF-CONTRACT COLOURS in laelaps %s: %s" % (name, off[:4]))
    filled, ratio = F.density(anims[0][1][0])
    print("  laelaps 48x48  idle %4d px  %.2f edges/px" % (filled, ratio))
    F.preview([("laelaps", CELL, anims)], PREVIEW, zoom=5)
    if bad:
        return 1
    if not write:
        return 0
    spec = []
    for name, cvs, loop, speed in anims:
        png = "laelaps-%s.png" % name
        F.write_strip(cvs, os.path.join(OUT_DIR, png), CELL)
        spec.append((name, png, len(cvs), loop, speed))
    for i, c in enumerate(anims[0][1]):
        c.img.save(os.path.join(OUT_DIR, "laelaps-%d.png" % (i + 1)))
    spriteframes.write(FRAMES, spec, OUT_REL, cell=CELL)
    print("  laelaps  %d frames -> scenes/resources/LaelapsFrames.tres; portrait laelaps-1..4.png"
          % sum(a[2] for a in spec))
    return 0


if __name__ == "__main__":
    sys.exit(main(write="--preview" not in sys.argv))
