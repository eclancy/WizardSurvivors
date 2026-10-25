# -*- coding: utf-8 -*-
"""The shared half of drawing a chapter's cast (.ai/bestiary.md, Phase 2).

WHY THIS EXISTS. Each chapter's creatures are drawn in a module of their own,
tools/art/cast_<chapter>.py, so several people can draw several chapters at once without two of
them ever editing the same file. This is what those modules share: writing a creature's animation
strips and its SpriteFrames .tres, reporting its size and detail density, and finding the chapter
modules so tools/art/build.py runs them all without a line per chapter. Nobody drawing a chapter
edits this file or build.py.

A CHAPTER MODULE defines its poses and one list, and runs itself:

    import bestiary_kit
    import roster

    def drowned():
        g = roster.G(32)
        ...                                   # the one drawn pose, to .ai/art-direction.md
        return g.rows()

    CAST = [
        # stem, pose, cell, (lean, reach, knock), SpriteFrames name
        ("cave-drowned", drowned, 32, (0.8, 2.4, 2), "CaveDrownedFrames"),
    ]
    PALETTE = roster.PALETTE                  # optional; roster.PALETTE when absent

    def build():
        return bestiary_kit.build_cast(CAST, PALETTE)

    if __name__ == "__main__":
        bestiary_kit.run(CAST, PALETTE, "cave")

That writes, per creature:
    assets/bonelight/enemies/<stem>-moving.png, -attack, -hurt, -death   (one strip each)
    scenes/resources/<SpriteFrames name>.tres                            (uid kept on rewrite)
and `run` also prints each pose's size and edges per pixel and writes a contact sheet to
tools/art/_cast/<chapter>.png (gitignored) for looking at before anything is wired.

The four animations are derived from the one pose by anim_sets.full_set, exactly as every other
enemy's are. `feel` is where a creature's weight lives: lean (how far its walk sways), reach (how far
its attack leans into the blow) and knock (how far a hit staggers it). build.py's FOE_FEEL and
FOREST_FEEL are the reference values.

NAMES ARE CHECKED, because a stem or a sheet name that collides with one that already exists silently
overwrites another creature's art: every stem carries its chapter's prefix and every sheet name is
<Prefix><Name>Frames, matching the scene it belongs to (scenes/<Prefix><Name>.tscn).
"""
from __future__ import print_function

import glob
import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import anim_sets
import pixel as P
import roster
import spriteframes

OUT_ENEMIES = os.path.join("assets", "bonelight", "enemies")
OUT_FRAMES = os.path.join("scenes", "resources")

# Stem prefix -> SpriteFrames / scene prefix, one per chapter that draws a cast. The Cursed Dungeon
# draws nothing (it owns the old roster) and the Sketchbook is the kids' and is never generated.
PREFIXES = {
    "forest-": "Forest",
    "cave-": "Cave",
    "swamp-": "Swamp",
    "ruins-": "Ruins",
    "frost-": "Frost",
    "sands-": "Sands",
    "ember-": "Ember",
}

CELLS = (32, 48)

# The density smell test from .ai/art-direction.md section 4b. Printed, never enforced: a shielder
# once scored 0.90 and read as a radiator, so where the number and the read disagree the read wins.
DENSITY_FLOOR = 0.80


def check_names(stem, sheet, cell):
    """Raise on a name that could overwrite another creature's art, or a cell not in the contract."""
    prefix = None
    for p in PREFIXES:
        if stem.startswith(p) and len(stem) > len(p):
            prefix = p
    if prefix is None:
        raise ValueError("stem %r has no chapter prefix (%s)" % (stem, ", ".join(sorted(PREFIXES))))
    if not (sheet.startswith(PREFIXES[prefix]) and sheet.endswith("Frames")):
        raise ValueError("sheet %r for %r must be %s<Name>Frames" % (sheet, stem, PREFIXES[prefix]))
    if cell not in CELLS:
        raise ValueError("%r is on a %d cell; creatures are 32 (basic) or 48 (miniboss)" % (stem, cell))


def write_creature(stem, rows, cell, feel, sheet, palette, root=ROOT):
    """Writes one creature's four strips and its SpriteFrames. Returns (frame count, anchor problems)."""
    check_names(stem, sheet, cell)
    # THE POSE is anchored, not the derived frames. anim_sets lifts the whole figure on the bob
    # frames of `moving` and collapses it through `death`, so every derived enemy set "fails"
    # check_anchor on those two - measured on the roster's ten, the forest's three, the Warden and
    # the Rime Guard - which is why build.py has never run it on one. The pose is the part the
    # drawing controls, and a pose with its feet on the contract's row derives a set that sits on it.
    problems = P.check_anchor("%s pose" % stem, [rows], cell)
    lean, reach, knock = feel
    anims = anim_sets.full_set(rows, lean=lean, reach=reach, knock=knock)
    out_dir = os.path.join(root, OUT_ENEMIES)
    spec = []
    for anim, frames, loop, speed in anims:
        png = "%s-%s.png" % (stem, anim)
        P.write_strip(frames, palette, os.path.join(out_dir, png), cell)
        spec.append((anim, png, len(frames), loop, speed))
    spriteframes.write(os.path.join(root, OUT_FRAMES, sheet + ".tres"), spec,
                       OUT_ENEMIES.replace(os.sep, "/"), cell=cell)
    return sum(a[2] for a in spec), problems


def build_cast(cast, palette=None, root=ROOT):
    """Writes every creature in a chapter's CAST. Returns ([(stem, frames, sheet)], problems)."""
    palette = palette or roster.PALETTE
    made, problems = [], []
    seen = set()
    for stem, pose_fn, cell, feel, sheet in cast:
        if stem in seen:
            raise ValueError("stem %r appears twice in one cast" % stem)
        seen.add(stem)
        n, p = write_creature(stem, pose_fn(), cell, feel, sheet, palette, root)
        made.append((stem, n, sheet))
        problems += p
    return made, problems


def report(cast, palette=None, chapter="cast"):
    """Prints each pose's footprint and density, and writes a contact sheet of the poses."""
    palette = palette or roster.PALETTE
    out = os.path.join(HERE, "_cast")
    if not os.path.isdir(out):
        os.makedirs(out)
    sets = []
    for stem, pose_fn, cell, _feel, _sheet in cast:
        rows = pose_fn()
        filled, _edges, ratio = roster.density(rows)
        xs = [x for y in range(cell) for x in range(cell) if rows[y][x] != "."]
        ys = [y for y in range(cell) for x in range(cell) if rows[y][x] != "."]
        size = "%2dw x %2dh" % (max(xs) - min(xs) + 1, max(ys) - min(ys) + 1) if xs else "empty"
        print("  %-24s %dx%d  %s  %4d px  %.2f edges/px%s"
              % (stem, cell, cell, size, filled, ratio, "" if ratio >= DENSITY_FLOOR else "   SPARSE"))
        sets.append((stem, [rows]))
    if sets:
        path = os.path.join(out, "%s.png" % chapter)
        P.preview(sets, palette, path, zoom=6)
        print("contact sheet: %s" % os.path.relpath(path, ROOT).replace(os.sep, "/"))


def run(cast, palette=None, chapter="cast"):
    """A chapter module's __main__: report, write, and fail loudly on an anchor problem."""
    report(cast, palette, chapter)
    made, problems = build_cast(cast, palette)
    for stem, n, sheet in made:
        print("  %-24s %3d frames -> scenes/resources/%s.tres" % (stem, n, sheet))
    if problems:
        print("")
        print("ANCHOR PROBLEMS (contract section 5):")
        for p in problems:
            print("   " + p)
        sys.exit(1)
    print("all frames anchored correctly")


def chapter_modules():
    """The chapter modules present, tools/art/cast_<chapter>.py, in name order."""
    paths = glob.glob(os.path.join(HERE, "cast_*.py"))
    return sorted(os.path.splitext(os.path.basename(p))[0] for p in paths)


def build_all(root=ROOT):
    """Runs every chapter module's CAST, for build.py. Returns ([(module, stem, frames, sheet)], problems)."""
    made, problems = [], []
    for name in chapter_modules():
        module = importlib.import_module(name)
        cast = getattr(module, "CAST", None)
        if cast is None:
            raise ValueError("tools/art/%s.py has no CAST list (see bestiary_kit.py)" % name)
        m, p = build_cast(cast, getattr(module, "PALETTE", None), root)
        made += [(name, stem, n, sheet) for stem, n, sheet in m]
        problems += p
    return made, problems


if __name__ == "__main__":
    mods = chapter_modules()
    print("chapter cast modules: %s" % (", ".join(mods) if mods else "none yet"))
