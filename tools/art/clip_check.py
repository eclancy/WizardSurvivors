# -*- coding: utf-8 -*-
"""Find world art that its own box has cut off. Run: python tools/art/clip_check.py [-v] [--root=DIR]

Exits 1 and lists every sprite, frame or decal tile whose drawing runs into the edge of the box it
is drawn in; exits 0 when there is none. -v also lists the edges it let through and why. --root
checks another copy of the assets (an older commit's, unpacked somewhere) instead of this one.

WHY THIS EXISTS (0.9.3). Reported on 0.9.2: "the art for the bushes and scorched sands all looked
like the art got cut off due to box boundaries". Every generator here draws through
`raster.Canvas.set`, which silently drops a pixel that falls outside the canvas. So a twig, a leaf
cluster, a shade pool or a flame drawn past the canvas it was given is not an error anywhere - the
generator runs, the audit passes, the game loads it - and on screen it ends in a straight line along
the box's edge. The Sands' dead bushes had lost their twigs on three sides, a third of the trees
their outer limbs or the foot of their shade, and the widest Forest bush its crown. Nothing in the
project would have noticed it again, which is what this is for.

THE RULE. Art keeps a clear border: no opaque pixel in the outermost row or column of its cell (a
sprite, one frame of a strip, one decal tile). A generator that leaves a margin can never be told
apart from one that was cut exactly at its edge, so the margin is the only test that works on the
PNG alone - and it is the one `trees.fit` guarantees.

TWO EXCEPTIONS. A GROUND edge is the bottom row of a sprite whose contact shadow is meant to end
there (`GROUND` below - the Fields' stooks and the well, the exploration props, the 32 px themed
props). A shadow that was cut there is the same fault, so a ground edge is still checked: the shape
on it must be closing, its bottom row narrower than the row above it, as an ellipse's last row is.
A shadow sliced at or above its widest row ends in a row as wide as the one over it - a flat foot -
and is reported. It is a heuristic: a slice lower down leaves a rounded foot, reads as whole and
passes. And a few edges touch on purpose - a spike's
point on the top row, one spark - each listed in `DELIBERATE` with its reason, so a new contact
has to be argued for rather than slipping through.

WHAT IS NOT CHECKED: the terrain tiles in assets/bonelight/tiles/ (not detail/) are meant to run
into their edges and meet the next tile, and the enemy and spell sheets are not world art. Scale is
read from the art: world sprites are drawn at x2, so "the row above" is two pixel rows up.
"""
from __future__ import print_function

import fnmatch
import io
import json
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# (folder, frame layout, scale, what it is). Layout: 1 for single sprites; "strip" for a horizontal
# strip of square frames; or a dict of file stem -> frame count for the few strips among singles.
SETS = [
    ("assets/bonelight/world/trees", 1, 2, "trees and bushes (trees.py)"),
    ("assets/bonelight/world/heath", {"moot-stone": 2}, 2, "the Broken Circle's stones and gorse (props_heath.py, explore_moot.py)"),
    ("assets/bonelight/world/fields", {"well": 2}, 2, "the Fallow Fields' stooks and fences, the well (props_fields.py, explore_well.py)"),
    ("assets/bonelight/world/explore", "explore.json", 2, "exploration props (explore_props.py)"),
    ("assets/bonelight/world/landmarks", 1, 2, "the flower pen (ground_detail.py)"),
    ("assets/bonelight/world/props", 1, 2, "themed props (world_props.py)"),
    ("assets/bonelight/world", 1, 2, "rocks and crystals (world_props.py)"),
    ("assets/bonelight/props", "strip", 2, "chests, flasks and stage hazards"),
    ("assets/bonelight/tiles/detail", 1, 1, "ground detail decals, one to a cell (ground_detail.py)"),
]

# Sprites whose bottom row is their ground line: (folder, file pattern, why).
GROUND = [
    ("assets/bonelight/world/fields", "*.png",
     "Node2DGame stands a Fields prop's bottom row on the ground (GroundedSprite ground = height), "
     "and WellEvent the well's"),
    ("assets/bonelight/world/explore", "*.png",
     "explore_props.py puts the contact shadow on the cell's last rows"),
    ("assets/bonelight/world/props", "*.png",
     "world_props.py's contact ellipse ends on the 32 px cell's last row"),
]

# Edges that touch on purpose, each with its reason: (path, edge, frame or None for every frame, why).
DELIBERATE = [
    ("assets/bonelight/world/fields/fence-1.png", "bottom", None,
     "the rails' shadow is a broken one-pixel line along the ground row, straight by design"),
    ("assets/bonelight/world/fields/fence-2.png", "bottom", None,
     "the rails' shadow is a broken one-pixel line along the ground row, straight by design"),
    ("assets/bonelight/props/spike-trap.png", "top", 5,
     "props_fx.SPIKE_FULL stands the back row's points on the top row; a point is where a spike ends"),
    ("assets/bonelight/props/chest-open.png", "right", 3,
     "one spark of the circling light lands on the last column; it is a single pixel, whole"),
    ("assets/bonelight/world/heath/moot-socket.png", "right", None,
     "the hollow is an ellipse whose rim ends on the last column; nothing lies past it"),
    ("assets/bonelight/world/heath/moot-socket.png", "bottom", None,
     "the hollow is an ellipse whose rim ends on the last row; nothing lies past it"),
]


def is_ground(folder, name):
    return any(folder == f and fnmatch.fnmatch(name, pat) for (f, pat, _) in GROUND)


def is_deliberate(path, edge, frame):
    return any(path == p and edge == e and (f is None or f == frame) for (p, e, f, _) in DELIBERATE)


def frames_of(root, folder, name, layout, size):
    """[(x0, y0, w, h)] for each frame of the image."""
    w, h = size
    stem = name[:-4]
    n = 1
    if layout == "strip":
        n = w // h if h and w % h == 0 else 1
    elif isinstance(layout, dict):
        n = layout.get(stem, 1)
    elif layout == "explore.json":
        manifest = json.load(io.open(os.path.join(root, folder, "explore.json"), encoding="utf-8"))
        for s in manifest.get("sprites", []):
            if s.get("id") == stem:
                fw, fh = s["frame_w"], s["frame_h"]
                return [(k * fw, 0, fw, fh) for k in range(w // fw)]
    fw = w // n
    return [(k * fw, 0, fw, h) for k in range(n)]


def row_count(alpha, x0, w, y):
    return sum(1 for x in range(x0, x0 + w) if alpha[x, y] > 0)


def col_count(alpha, x, y0, h):
    return sum(1 for y in range(y0, y0 + h) if alpha[x, y] > 0)


def check_frame(alpha, box, scale, ground):
    """[(edge, opaque pixels on it, note)] for the edges this frame's art runs into."""
    x0, y0, w, h = box
    out = []
    for edge, n in (("top", row_count(alpha, x0, w, y0)),
                    ("left", col_count(alpha, x0, y0, h)),
                    ("right", col_count(alpha, x0 + w - 1, y0, h))):
        if n:
            out.append((edge, n, ""))
    bottom = row_count(alpha, x0, w, y0 + h - 1)
    if bottom:
        if not ground:
            out.append(("bottom", bottom, ""))
        else:
            above = row_count(alpha, x0, w, y0 + h - 1 - scale) if h > scale else 0
            if bottom >= above:
                out.append(("bottom", bottom, "ground edge, but not closing: %d px against %d above" % (bottom, above)))
            else:
                out.append(("ground", bottom, "closing: %d px against %d above" % (bottom, above)))
    return out


def scan(root=ROOT):
    """[(path, frame index or None, edge, pixels, note)] over every set."""
    found = []
    for (folder, layout, scale, _) in SETS:
        full = os.path.join(root, folder)
        if not os.path.isdir(full):
            continue
        for name in sorted(os.listdir(full)):
            if not name.endswith(".png"):
                continue
            img = Image.open(os.path.join(full, name)).convert("RGBA")
            alpha = img.getchannel("A").load()
            boxes = frames_of(root, folder, name, layout, img.size)
            path = folder + "/" + name
            for i, box in enumerate(boxes):
                for (edge, n, note) in check_frame(alpha, box, scale, is_ground(folder, name)):
                    frame = i if len(boxes) > 1 else None
                    if is_deliberate(path, edge, frame):
                        edge, note = "allowed", "%s edge, deliberate" % edge
                    found.append((path, frame, edge, n, note))
    return found


def main(argv):
    verbose = "-v" in argv
    root = ROOT
    for a in argv:
        if a.startswith("--root="):
            root = os.path.abspath(a[len("--root="):])
    found = scan(root)
    cut = [f for f in found if f[2] not in ("ground", "allowed")]
    for (path, frame, edge, n, note) in found:
        if edge in ("ground", "allowed") and not verbose:
            continue
        where = path + ("" if frame is None else " frame %d" % frame)
        print("%-62s %-6s %4d px%s" % (where, edge, n, "  (" + note + ")" if note else ""))
    files = sorted(set(f[0] for f in cut))
    if cut:
        print("\n%d cut edges in %d files: the art runs into its box. Fix the generator (trees.fit is "
              "one way), never the PNG." % (len(cut), len(files)))
        return 1
    print("No art runs into its box (%d sets checked)." % len(SETS))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
