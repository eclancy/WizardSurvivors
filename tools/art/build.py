# -*- coding: utf-8 -*-
"""Generate every Bonelight sprite into assets/bonelight/.

Run from the repo root:  python tools/art/build.py

Layouts differ by consumer and both are load-bearing:
  * The player is numbered single frames ("...-1.png", "-2"...), because
    CharacterVisuals.ResolveIdleFrame walks siblings off the portrait it is handed.
  * Enemies are one horizontal strip per animation, because that is what the AtlasTexture
    regions in a SpriteFrames .tres address.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import pixel as P
import sprite_player
import sprite_skullsentry

OUT_CHARS = os.path.join(ROOT, "assets", "bonelight", "characters")
OUT_ENEMIES = os.path.join(ROOT, "assets", "bonelight", "enemies")


def build_player():
    frames = sprite_player.frames()
    problems = P.check_anchor("player idle", frames, sprite_player.CELL)
    pattern = os.path.join(OUT_CHARS, "wizard-idle-%d.png")
    paths = P.write_frames(frames, sprite_player.PALETTE, pattern, sprite_player.CELL)
    return paths, problems


def build_skullsentry():
    paths, problems = [], []
    for name, fn in sprite_skullsentry.ANIMATIONS:
        frames = fn()
        problems += P.check_anchor("skullsentry " + name, frames, sprite_skullsentry.CELL)
        path = os.path.join(OUT_ENEMIES, "skullsentry-%s.png" % name)
        P.write_strip(frames, sprite_skullsentry.PALETTE, path, sprite_skullsentry.CELL)
        paths.append((name, path, len(frames)))
    return paths, problems


if __name__ == "__main__":
    all_problems = []

    player_paths, problems = build_player()
    all_problems += problems
    print("player idle: %d frames" % len(player_paths))
    for p in player_paths:
        print("   " + os.path.relpath(p, ROOT).replace(os.sep, "/"))

    sentry_paths, problems = build_skullsentry()
    all_problems += problems
    print("skullsentry:")
    for name, path, n in sentry_paths:
        print("   %-8s %d frames  %s" % (name, n, os.path.relpath(path, ROOT).replace(os.sep, "/")))

    if all_problems:
        print("")
        print("ANCHOR PROBLEMS (contract section 5):")
        for p in all_problems:
            print("   " + p)
        sys.exit(1)
    print("")
    print("all frames anchored correctly")
