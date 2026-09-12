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

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import anim_sets
import sprite_boo
import sprite_treant
import sprite_warden
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


# Every enemy in the roster now ships with the full animation contract - `moving` 8, `attack` 6,
# `hurt` 2, `death` 6 - derived from its pose by tools/art/anim_sets.py. The five that were held
# back last time were held back because swapping a 22-frame set for a four-frame idle trades
# three animations for a nicer standing pose; that reason is gone now that they have 22 frames of
# their own.
#
# The .tres NAMES are fixed by the scenes that reference them and are not free to be tidier:
# EnemyFrames is the swarmer, FastEnemyFrames the runner, SlowEnemyFrames the bruiser,
# TankEnemyFrames the shielder. Renaming those is a scene edit, not an art one.
FOE_SHEETS = [
    ("swarmer", "EnemyFrames"),
    ("runner", "FastEnemyFrames"),
    ("bruiser", "SlowEnemyFrames"),
    ("shielder", "TankEnemyFrames"),
    ("skullsentry", "SkullSentryFrames"),
    ("lunger", "LungerEnemyFrames"),
    ("slammer", "SlammerEnemyFrames"),
    ("exploder", "ExploderEnemyFrames"),
    ("summoner", "SummonerEnemyFrames"),
    ("hexer", "HexerEnemyFrames"),
]

# How hard each one leans, swings and flinches. A swarmer and a slammer should not walk the same
# way, and the pose alone cannot say so - these three numbers are where an enemy's weight lives.
FOE_FEEL = {
    "swarmer": (0.8, 2.4, 2), "runner": (1.6, 3.4, 3), "bruiser": (0.7, 3.6, 1),
    "shielder": (0.5, 2.8, 1), "skullsentry": (1.2, 3.0, 2), "lunger": (1.8, 4.4, 3),
    "slammer": (0.6, 4.0, 1), "exploder": (1.1, 2.0, 3), "summoner": (0.9, 2.6, 2),
    # A caster does not swing, so its `attack` is a short gather-and-thrust rather than a
    # blow - and it is the frailest thing in the roster, so it flinches hardest.
    "hexer": (0.9, 2.2, 3),
}


def build_roster():
    """The whole cast: four starters and nine enemies, each with its full set of animations."""
    made = []
    poses = dict(roster.ENEMIES)
    for name, sheet in FOE_SHEETS:
        pose = poses[name]()
        lean, reach, knock = FOE_FEEL[name]
        anims = anim_sets.full_set(pose, lean=lean, reach=reach, knock=knock)
        spec = []
        for anim, frames, loop, speed in anims:
            png = "%s-%s.png" % (name, anim)
            P.write_strip(frames, roster.PALETTE, os.path.join(OUT_ENEMIES, png),
                          roster.ENEMY_CELL)
            spec.append((anim, png, len(frames), loop, speed))
        spriteframes.write(os.path.join(ROOT, "scenes", "resources", sheet + ".tres"),
                           spec, "assets/bonelight/enemies", cell=roster.ENEMY_CELL)
        made.append(("enemy", name, sum(len(f) for _a, f, _l, _s in anims), sheet))

    for name, fn in roster.WIZARDS:
        pose = fn()
        idle = roster.idle(pose)
        # Numbered single frames for the idle, because CharacterData.Portrait points at frame one
        # and CharacterVisuals.ResolveIdleFrame walks "-2", "-3", "-4" off it. That is the only
        # path the player art currently travels.
        P.write_frames(idle, roster.PALETTE, os.path.join(OUT_CHARS, name + "-%d.png"),
                       roster.WIZARD_CELL)
        # AND a full SpriteFrames beside it, which nothing reads yet. Player.cs plays only
        # `idle` and never switches animation - it flips H and that is all - so moving, hurt and
        # death are drawn and waiting on a code change rather than on art. Shipping the art first
        # means that change is a wiring job with nothing to draw.
        anims = anim_sets.player_set(pose, idle)
        spec = []
        for anim, frames, loop, speed in anims:
            png = "%s-%s.png" % (name, anim)
            P.write_strip(frames, roster.PALETTE, os.path.join(OUT_CHARS, png),
                          roster.WIZARD_CELL)
            spec.append((anim, png, len(frames), loop, speed))
        spriteframes.write(os.path.join(ROOT, "scenes", "resources",
                                        "%sFrames.tres" % name.capitalize()),
                           spec, "assets/bonelight/characters", cell=roster.WIZARD_CELL)
        made.append(("wizard", name, sum(len(f) for _a, f, _l, _s in anims),
                     "%sFrames" % name.capitalize()))
    return made


def build_boo():
    """Eric's ghost, re-seated on a 32px cell and given the three animations it never had.

    Its own two-colour art, extended rather than replaced, and deliberately off the Bonelight
    palette - see sprite_boo.py. Output lives beside the source in assets/enemies/ rather than
    under assets/bonelight/, so the exception is visible from the path.
    """
    out_dir = os.path.join(ROOT, "assets", "enemies")
    spec = []
    for name, frames, loop, speed in sprite_boo.animations():
        png = "boo-%s.png" % name
        sheet = Image.new("RGBA", (sprite_boo.CELL * len(frames), sprite_boo.CELL), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            sheet.alpha_composite(f, (i * sprite_boo.CELL, 0))
        sheet.save(os.path.join(out_dir, png))
        spec.append((name, png, len(frames), loop, speed))
    spriteframes.write(os.path.join(ROOT, "scenes", "resources", "BooEnemyFrames.tres"),
                       spec, "assets/enemies", cell=sprite_boo.CELL)
    return sum(a[2] for a in spec)


def build_treant():
    """Elderbark, on its own 96x96 sheet at last, with the full animation contract.

    Boss feel: barely leans, swings enormously, and hardly flinches. An immense thing that
    recoils like a swarmer stops being immense.
    """
    pose = sprite_treant.treant()
    anims = anim_sets.full_set(pose, lean=0.35, reach=5.5, knock=1)
    spec = []
    for anim, frames, loop, speed in anims:
        png = "treant-%s.png" % anim
        P.write_strip(frames, sprite_treant.PALETTE,
                       os.path.join(OUT_ENEMIES, png), sprite_treant.CELL)
        spec.append((anim, png, len(frames), loop, speed))
    spriteframes.write(os.path.join(ROOT, "scenes", "resources", "ForestTreantBossFrames.tres"),
                       spec, "assets/bonelight/enemies", cell=sprite_treant.CELL)
    return sum(a[2] for a in spec)


def build_warden():
    """The Warden, the recurring miniboss, on the 48x48 elite cell.

    Feel: barely leans, swings hard, and hardly flinches. Something wearing a door does not
    stagger when it is hit, and a miniboss that recoils like a swarmer stops being one.
    """
    pose = sprite_warden.warden()
    anims = anim_sets.full_set(pose, lean=0.5, reach=4.2, knock=1)
    spec = []
    for anim, frames, loop, speed in anims:
        png = "warden-%s.png" % anim
        P.write_strip(frames, sprite_warden.PALETTE,
                      os.path.join(OUT_ENEMIES, png), sprite_warden.CELL)
        spec.append((anim, png, len(frames), loop, speed))
    spriteframes.write(os.path.join(ROOT, "scenes", "resources", "WardenEnemyFrames.tres"),
                       spec, "assets/bonelight/enemies", cell=sprite_warden.CELL)
    return sum(a[2] for a in spec)


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

    total = 0
    for kind, name, n, sheet in build_roster():
        print("%-7s %-12s %3d frames -> %s.tres" % (kind, name, n, sheet))
        total += n
    print("   %d frames across the roster" % total)

    print("boo          %3d frames -> BooEnemyFrames.tres" % build_boo())
    print("treant       %3d frames -> ForestTreantBossFrames.tres" % build_treant())
    print("warden       %3d frames -> WardenEnemyFrames.tres" % build_warden())

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
