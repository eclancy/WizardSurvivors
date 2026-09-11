# -*- coding: utf-8 -*-
"""Grid -> PNG helpers for Bonelight sprite generation.

A sprite is authored as a list of strings, one character per pixel, keyed through a palette
built by bonelight.build_palette. Animation frames are composed from a base grid plus small
per-frame deltas (a shift, or an overlay patch) rather than being retyped, which is what keeps
a 22-frame set editable.
"""
import os
from PIL import Image


def pad(rows, w, h):
    """Pad/normalise an authored grid to exactly w x h characters."""
    out = []
    for r in rows[:h]:
        if len(r) > w:
            raise ValueError("row wider than cell (%d > %d): %r" % (len(r), w, r))
        out.append(r + "." * (w - len(r)))
    while len(out) < h:
        out.append("." * w)
    return out


def shift(rows, dx, dy, w, h):
    """Translate a grid, discarding anything pushed outside the cell."""
    blank = "." * w
    out = []
    for y in range(h):
        src = y - dy
        if src < 0 or src >= len(rows):
            out.append(blank)
            continue
        r = rows[src]
        if dx > 0:
            r = "." * dx + r[:w - dx]
        elif dx < 0:
            r = r[-dx:] + "." * (-dx)
        out.append(r[:w].ljust(w, "."))
    return out


def overlay(base, patch, w, h):
    """Draw patch over base; '.' in the patch means transparent (keep base)."""
    base = pad(base, w, h)
    patch = pad(patch, w, h)
    out = []
    for y in range(h):
        row = []
        for x in range(w):
            c = patch[y][x]
            row.append(c if c != "." else base[y][x])
        out.append("".join(row))
    return out


def render(rows, pal):
    h = len(rows)
    w = max(len(r) for r in rows)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = im.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            c = pal.get(ch)
            if not c:
                continue
            px[x, y] = (int(c[1:3], 16), int(c[3:5], 16), int(c[5:7], 16), 255)
    return im


def content_bounds(rows):
    """(top, bottom, left, right) of non-transparent pixels, or None."""
    top, bot, left, right = None, None, None, None
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == ".":
                continue
            if top is None:
                top = y
            bot = y
            left = x if left is None else min(left, x)
            right = x if right is None else max(right, x)
    return None if top is None else (top, bot, left, right)


def check_anchor(name, frames, cell, feet_row=None):
    """Contract section 5: feet on row cell-2, occ contact on cell-1, every frame.

    Returns a list of complaints rather than raising, so a whole set can be checked at once.
    """
    if feet_row is None:
        feet_row = cell - 2
    problems = []
    for i, rows in enumerate(frames):
        b = content_bounds(rows)
        if b is None:
            problems.append("%s frame %d is empty" % (name, i))
            continue
        bottom = b[1]
        if bottom < feet_row or bottom > feet_row + 1:
            problems.append(
                "%s frame %d: lowest pixel on row %d, expected %d or %d"
                % (name, i, bottom, feet_row, feet_row + 1))
        if b[3] >= cell or b[2] < 0:
            problems.append("%s frame %d: content outside the cell horizontally" % (name, i))
    return problems


def write_strip(frames, pal, path, cell):
    """One horizontal strip per animation, no padding - the layout every .tres expects."""
    n = len(frames)
    sheet = Image.new("RGBA", (cell * n, cell), (0, 0, 0, 0))
    for i, rows in enumerate(frames):
        sheet.alpha_composite(render(pad(rows, cell, cell), pal), (i * cell, 0))
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    sheet.save(path)
    return sheet


def write_frames(frames, pal, path_pattern, cell, start=1):
    """Numbered single-frame PNGs: '<base>-1.png', '-2'... the layout CharacterVisuals walks."""
    paths = []
    for i, rows in enumerate(frames):
        p = path_pattern % (start + i)
        d = os.path.dirname(p)
        if d and not os.path.isdir(d):
            os.makedirs(d)
        render(pad(rows, cell, cell), pal).save(p)
        paths.append(p)
    return paths


def preview(sets, pal, path, zoom=6, bg=(11, 15, 24), cell=None):
    """Contact sheet: one row per animation, for eyeballing before wiring anything up.

    `cell` was hardcoded to 32, which silently CROPPED any set authored at another size - the
    48x48 player studies came out with their feet and their staff heads cut off. It defaults to
    the width of the first frame now, so a set declares its own cell by being that size, and an
    explicit value still wins.
    """
    cols = max(len(f) for _, f in sets)
    if cell is None:
        first = sets[0][1][0]
        cell = max(len(r) for r in first) if first else 32
    W = cols * (cell * zoom + 6) + 6
    H = len(sets) * (cell * zoom + 6) + 6
    sheet = Image.new("RGBA", (W, H), bg + (255,))
    for r, (_name, frames) in enumerate(sets):
        for c, rows in enumerate(frames):
            im = render(pad(rows, cell, cell), pal)
            im = im.resize((cell * zoom, cell * zoom), Image.NEAREST)
            sheet.alpha_composite(im, (6 + c * (cell * zoom + 6), 6 + r * (cell * zoom + 6)))
    sheet.convert("RGB").save(path)
    return sheet
