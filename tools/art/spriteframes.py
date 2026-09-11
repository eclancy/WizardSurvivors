# -*- coding: utf-8 -*-
"""Write a Godot SpriteFrames .tres for a set of horizontal strips.

Generated rather than hand-edited because a 22-frame set is 22 AtlasTexture blocks with
hand-counted Rect2 offsets, and the audit found 240 of those already in the project. Every one
this writes is derived from the strip it points at, so a cell-size change cannot desync them.
"""
import io
import os
import re


def _existing_uid(path):
    """The uid already on this resource, if it has one.

    REGENERATING A .tres MUST NOT DROP ITS uid. Scenes reference resources by uid, not only by
    path - scenes/enemy.tscn holds `uid="uid://c5k1do2ynymdr"` for EnemyFrames.tres - and a
    rewrite that omits it breaks that reference. The failure is not a clean load error either:
    the game ran, spawned, and then died in a C# finalizer with

        FATAL: Condition "gchandle.is_released()" is true
        Godot.GodotObject.Finalize() -> godotsharp_internal_refcounted_disposed

    which looks like a Mono GC bug and is really a dangling resource reference. Measured: 0 of 5
    headless runs crashed with the uid intact, 5 of 5 without it.

    Only EnemyFrames.tres carries one today, which is exactly why regenerating the other four
    enemy sheets looked harmless and that one did not.
    """
    if not os.path.exists(path):
        return None
    with io.open(path, encoding="utf-8") as f:
        head = f.readline()
    m = re.search(r'uid="(uid://[a-z0-9]+)"', head)
    return m.group(1) if m else None


def write(path, anims, asset_dir, cell=32):
    """anims: [(name, png_filename, frame_count, loop, speed)] - order is preserved."""
    steps = len(anims) + sum(a[2] for a in anims) + 1
    uid = _existing_uid(path)
    out = ['[gd_resource type="SpriteFrames" load_steps=%d format=3%s]'
           % (steps, ' uid="%s"' % uid if uid else ""), ""]
    for i, (_n, png, _c, _l, _s) in enumerate(anims):
        out.append('[ext_resource type="Texture2D" path="res://%s/%s" id="tex_%d"]'
                   % (asset_dir, png, i))
    out.append("")
    for i, (name, _png, count, _l, _s) in enumerate(anims):
        for f in range(count):
            out.append('[sub_resource type="AtlasTexture" id="Atlas_%s_%d"]' % (name, f))
            out.append('atlas = ExtResource("tex_%d")' % i)
            out.append("region = Rect2(%d, 0, %d, %d)" % (f * cell, cell, cell))
            out.append("")
    out.append("[resource]")
    parts = []
    for name, _png, count, loop, speed in anims:
        frames = ", ".join('{"duration": 1.0, "texture": SubResource("Atlas_%s_%d")}' % (name, f)
                           for f in range(count))
        parts.append('{"frames": [%s], "loop": %s, "name": &"%s", "speed": %.1f}'
                     % (frames, "true" if loop else "false", name, speed))
    out.append("animations = [" + ", ".join(parts) + "]")

    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    io.open(path, "w", encoding="utf-8", newline="\n").write(u"\n".join(out) + u"\n")
    return sum(a[2] for a in anims)
