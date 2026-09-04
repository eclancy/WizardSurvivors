# -*- coding: utf-8 -*-
"""Write a Godot SpriteFrames .tres for a set of horizontal strips.

Generated rather than hand-edited because a 22-frame set is 22 AtlasTexture blocks with
hand-counted Rect2 offsets, and the audit found 240 of those already in the project. Every one
this writes is derived from the strip it points at, so a cell-size change cannot desync them.
"""
import io
import os


def write(path, anims, asset_dir, cell=32):
    """anims: [(name, png_filename, frame_count, loop, speed)] - order is preserved."""
    steps = len(anims) + sum(a[2] for a in anims) + 1
    out = ['[gd_resource type="SpriteFrames" load_steps=%d format=3]' % steps, ""]
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
