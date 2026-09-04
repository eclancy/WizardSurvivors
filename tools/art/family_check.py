# -*- coding: utf-8 -*-
"""The contract's acceptance test for a sprite family (.ai/art-direction.md section 4).

Top row is every body filled with one flat colour. If you cannot name the class from that row
alone, the sprite fails and no amount of shading will save it. Bottom row is the finished tones
for reference. Run after any silhouette change.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from PIL import Image
import bonelight as B
import pixel as P

FILL = "#E8ECF6"


def silhouette_palette(pal):
    """Flat fill for every real colour; '.' stays transparent, which is the whole point."""
    return dict((k, (None if v is None else FILL)) for k, v in pal.items())


def sheet(bases, pal, path, zoom=6, floor=None):
    floor = floor or B.tone("stone", "base")
    bg = tuple(int(floor[i:i + 2], 16) for i in (1, 3, 5))
    grids = [P.pad(g, 32, 32) for _, g in bases]
    cell = 32 * zoom + 12
    im = Image.new("RGBA", (len(grids) * cell + 12, 2 * cell + 12), bg + (255,))
    sil = silhouette_palette(pal)
    for col, g in enumerate(grids):
        for row, p in enumerate((sil, pal)):
            s = P.render(g, p).resize((32 * zoom, 32 * zoom), Image.NEAREST)
            im.alpha_composite(s, (12 + col * cell, 12 + row * cell))
    im.convert("RGB").save(path)
    return P.check_anchor("family", grids, 32)
