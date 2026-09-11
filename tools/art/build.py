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
import roster
import sprite_pickups
import sprite_player
import sprite_testwizard
import sprite_skullsentry
import spriteframes

OUT_CHARS = os.path.join(ROOT, "assets", "bonelight", "characters")
OUT_ENEMIES = os.path.join(ROOT, "assets", "bonelight", "enemies")
OUT_PICKUPS = os.path.join(ROOT, "assets", "bonelight", "pickups")
# Deliberately NOT under assets/bonelight/: the test wizard is Eric's own drawing and is off
# contract on purpose. Its own directory keeps that visible from the path alone.
OUT_TESTWIZ = os.path.join(ROOT, "assets", "testwizard")


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


def build_testwizard():
    """The supplied test-wizard skin, re-seated on the player cell with a derived idle.

    Numbered single frames, not a strip: CharacterData.Portrait points at frame one and
    CharacterVisuals.ResolveIdleFrame walks "-2", "-3", "-4" off it.
    """
    frames = sprite_testwizard.frames()
    problems = sprite_testwizard.check(frames)
    if not os.path.isdir(OUT_TESTWIZ):
        os.makedirs(OUT_TESTWIZ)
    paths = []
    for i, im in enumerate(frames):
        path = os.path.join(OUT_TESTWIZ, "testwizard-%d.png" % (i + 1))
        im.save(path)
        paths.append(path)
    return paths, problems


# Which of the roster ships, and which is still waiting on frames. This list is the whole
# judgement call and it belongs where the build can see it rather than in a commit message.
#
# The five existing Bonelight enemies - swarmer, runner, bruiser, shielder, skullsentry - are
# NOT here. They already carry full 22-frame sets (moving 8, attack 6, hurt 2, death 6) and the
# roster versions are four-frame idles, so shipping them would trade three animations for a
# nicer standing pose. That is a regression however much better the pose is. They ship when
# their full sets are drawn, which is about 110 frames and the next piece of work.
ROSTER_FOES = ["lunger", "slammer", "exploder", "summoner"]


def build_roster():
    """The four starters, and the four enemies that had no sheet of their own.

    The enemies here get their frames as `moving`, which is what Enemy.cs plays, and have no
    `death` - that is the documented fallback in CLAUDE.md, where Enemy.StartDeath frees the node
    immediately rather than waiting on an animation that does not exist.
    """
    made = []
    for name, fn in roster.WIZARDS:
        frames = roster.idle(fn())
        # Numbered single frames, not a strip: CharacterData.Portrait points at frame one and
        # CharacterVisuals.ResolveIdleFrame walks "-2", "-3", "-4" off it.
        P.write_frames(frames, roster.PALETTE, os.path.join(OUT_CHARS, name + "-%d.png"),
                       roster.WIZARD_CELL)
        made.append(("wizard", name, len(frames)))
    for name, fn in roster.ENEMIES:
        if name not in ROSTER_FOES:
            continue
        frames = roster.idle(fn())
        png = "%s-moving.png" % name
        P.write_strip(frames, roster.PALETTE, os.path.join(OUT_ENEMIES, png),
                      roster.ENEMY_CELL)
        spriteframes.write(
            os.path.join(ROOT, "scenes", "resources", "%sEnemyFrames.tres" % name.capitalize()),
            [("moving", png, len(frames), True, 6.0)], "assets/bonelight/enemies",
            cell=roster.ENEMY_CELL)
        made.append(("enemy", name, len(frames)))
    return made


def build_pickups():
    """The three XP orb tiers, plus the SpriteFrames that indexes them.

    The .tres is generated here rather than hand-written for the same reason every other one is:
    three animations x four frames is twelve AtlasTexture blocks with hand-counted Rect2 offsets,
    and a cell-size change would silently desync every one of them.
    """
    out = []
    anims = []
    for tname, radius, elem, sparks in sprite_pickups.TIERS:
        frames = sprite_pickups.frames_for(radius, sparks)
        png = "xp-%s.png" % tname
        P.write_strip(frames, sprite_pickups._palette(elem),
                      os.path.join(OUT_PICKUPS, png), sprite_pickups.CELL)
        anims.append((tname, png, len(frames), True, 6.0))
        out.append((tname, png, len(frames)))
    spriteframes.write(os.path.join(ROOT, "scenes", "resources", "XPOrbFrames.tres"),
                       anims, "assets/bonelight/pickups", cell=sprite_pickups.CELL)
    return out


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

    tw_paths, problems = build_testwizard()
    all_problems += problems
    print("test wizard: %d idle frames" % len(tw_paths))
    for p in tw_paths:
        print("   " + os.path.relpath(p, ROOT).replace(os.sep, "/"))

    for kind, name, n in build_roster():
        print("%-7s %-12s %d frames" % (kind, name, n))

    pickup_paths = build_pickups()
    print("xp orbs:")
    for tname, png, n in pickup_paths:
        print("   %-8s %d frames  assets/bonelight/pickups/%s" % (tname, n, png))
    print("   scenes/resources/XPOrbFrames.tres")

    if all_problems:
        print("")
        print("ANCHOR PROBLEMS (contract section 5):")
        for p in all_problems:
            print("   " + p)
        sys.exit(1)
    print("")
    print("all frames anchored correctly")
