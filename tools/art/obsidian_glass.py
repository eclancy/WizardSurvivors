# -*- coding: utf-8 -*-
"""Black volcanic glass: the one way Obsidian Spike's glass is drawn, on its icon and in the world.

Imported by spell_icons_core.obsidian_spike (the 32x32 icon, which evolution_icons.py redraws for
every one of the spell's evolutions) and spell_fx.obsidian_spike (the eruption sheet the game plays),
so the card and the spike in the crowd are the same object, faceted and lit the same way.

WHY IT IS GLASS AND NOT ROCK. The first spike was three tan triangles in the EARTH ramp - an earth
colour standing in for black, because "obsidian is black and the card is near-black, so a faithful
obsidian spike is an invisible spike". That stopped being true when the card became a parchment
page, and it was never what made the thing dull: a flat wedge reads as a flat wedge in any colour.
Eric, 2026-10-28: "obsidian spike icon and in game sprite doesn't look very cool, update".

Glass reads as glass on three things, and each is a rule here:

  1. FACETS WITH HARD EDGES. Two planes meeting at a ridge, the key-facing one lit and the other in
     shadow, with no blend between them. A blade, not a cone.
  2. A SPECULAR THAT IS A LINE, NOT A PATCH. The ridge catches the cold key (upper left, art-direction
     section 2) as a thin streak of the palette's brightest cold tones - bone hi and steel hi - which
     is what separates glass from stone. Glass also shows a reflection band: a stripe of its hi down
     the lit plane, parallel to the ridge.
  3. LIGHT FROM BELOW. The ground it came out of is still glowing (the Earth ramp: the spell's
     element is emissive, art-direction section 3), and black glass mirrors it: the foot of every
     blade picks up the eruption's heat, the shadow plane in the ramp's edge and the lit plane in its
     mid. That is the "volcanic" half, and it is what lifts a black blade off a dark floor.

The body is the `violet` material row, the one dark row that keeps its chroma all the way down
(art-direction: "nothing dark is allowed to be grey"), so it separates by hue from the teal and slate
floors it stands on rather than by a brightness it does not have. Its darkest stop is the shared
`occ`, only in the notches where one plane folds under another.

Everything here is a primitive call (poly, line, set), so evolution_icons.XCanvas can redraw it
through a Place at any size without resampling a pixel.
"""

import bonelight as bl

OCC = bl.OCC
GLASS = bl.MATERIALS["violet"]          # hi, lit, base, shade, deep
SPEC = bl.MATERIALS["skin"][0]          # bone hi: the hottest cold white in the contract
SPEC2 = bl.MATERIALS["steel"][0]        # steel hi: the streak below it
SHEEN = bl.MATERIALS["wool"][1]         # wool lit: the indigo of the sky in the reflection band


def lerp(a, b, t):
    return a + (b - a) * t


def blade(c, apex, foot_l, foot_r, ridge, gy, heat=1.0, ramp=None, lean_l=0.0, lean_r=0.0,
          chip=None, band=True, ripples=True, spec=0.6):
    """One faceted blade of glass standing on the ground line `gy`.

    apex   (x, y) of the point.
    foot_l / foot_r / ridge: x where the left edge, the right edge and the ridge meet the ground.
    lean_l / lean_r: how far the left / right edge bows out between point and foot, in pixels -
           a knapped edge rather than a ruled one; negative pinches it in, toward a needle.
    chip   0..1 down the lit edge: a conchoidal chip taken out of it there, or None.
    heat   0..1, how strongly the glowing ground lights the foot (0 for a cooled blade).
    ramp   the emissive ramp of that glow (core, hot, mid, edge): the spell's element.
    spec   0..1, how far down the ridge the specular streak runs.
    """
    ax, ay = apex
    h = gy - ay
    if h < 1:
        return
    hot_glow = heat > 0.0 and ramp is not None

    # The silhouette: three points down each edge, bowed by the lean, so the blade never reads as
    # a ruled triangle.
    def edge(foot, lean, sign):
        return [(lerp(ax, foot, t) + sign * lean * w, lerp(ay, gy, t))
                for (t, w) in ((0.22, 0.45), (0.5, 1.0), (0.78, 0.6))]
    L = edge(foot_l, lean_l, -1)
    R = edge(foot_r, lean_r, 1)

    # The shadow plane: shade, a bevel of base where it turns toward the ridge, deep along its edge.
    c.poly([apex] + R + [(foot_r, gy), (ridge, gy)], GLASS[3])
    if h >= 6:
        c.poly([apex, (ridge + 2, gy), (ridge, gy)], GLASS[2])
    outline = [apex] + R + [(foot_r, gy)]
    for i in range(len(outline) - 1):
        (x0, y0), (x1, y1) = outline[i], outline[i + 1]
        c.line(x0, y0 + 1, x1, y1, GLASS[4])

    # The lit plane: lit, with a wedge of hi down the ridge (the plane catching the key full on)
    # and the sky's reflection as a thin stripe inside it. Its key-facing edge is hi.
    c.poly([apex, (ridge, gy)] + [(foot_l, gy)] + list(reversed(L)), GLASS[1])
    # A fold low on the lit plane: the facet under it turned away from the key, a stop darker. Two
    # facets on one side is what keeps the lit plane from reading as one flat sheet (section 4b).
    if h >= 10:
        fold = (lerp(ax, ridge, 0.84), lerp(ay, gy, 0.84))
        c.poly([L[1], fold, (lerp(ridge, foot_l, 0.4), gy), (foot_l, gy), L[2]], GLASS[2])
        c.line(L[1][0] + 1, L[1][1], fold[0], fold[1], GLASS[0])
    if band and h >= 8:
        c.poly([apex, (lerp(ax, ridge, 0.78), lerp(ay, gy, 0.78)),
                (lerp(ax, ridge, 0.62) - 2, lerp(ay, gy, 0.62))], GLASS[0])
        x0, y0 = lerp(ax, ridge, 0.24) - 1, lerp(ay, gy, 0.24)
        x1, y1 = lerp(ax, ridge, 0.5) - 2, lerp(ay, gy, 0.5)
        c.line(x0, y0, x1, y1, SHEEN)
    top = [apex] + L[:2]
    for i in range(len(top) - 1):
        (x0, y0), (x1, y1) = top[i], top[i + 1]
        c.line(x0, y0, x1, y1, GLASS[0])

    # The conchoidal chip: a scoop out of the lit edge, its lower lip catching the light.
    if chip is not None and h >= 10:
        cx, cy = lerp(L[0][0], L[2][0], chip), lerp(L[0][1], L[2][1], chip)
        c.set(cx, cy, GLASS[3])
        c.set(cx + 1, cy, GLASS[2])
        c.set(cx, cy + 1, GLASS[3])
        c.set(cx + 1, cy + 2, SPEC2)

    # Ripples on the shadow plane: the curved fracture lines obsidian breaks in.
    if ripples and h >= 12:
        for t in (0.4, 0.64):
            x0, y0 = lerp(ax, ridge, t) + 2, lerp(ay, gy, t)
            c.set(x0, y0, GLASS[2])
            c.set(x0 + 1, y0 + 1, GLASS[2])
            c.set(x0 + 1, y0 + 2, GLASS[1])

    # The ridge: the key's specular, bone-white at the point and cooling down the edge.
    c.line(ax, ay, ridge, gy, GLASS[0])
    run = int(h * spec)
    for i in range(run):
        t = i / float(h)
        c.set(lerp(ax, ridge, t), lerp(ay, gy, t), SPEC if i < max(2, run * 0.45) else SPEC2)
    c.set(ax, ay, SPEC)

    if not hot_glow:
        return
    core, hot, mid, edge_tone = ramp
    # The glow on the shadow plane's outer edge: the bounce off the ground, which is what lifts a
    # black blade off a dark floor.
    rim = [R[1], R[2], (foot_r, gy)]
    for i in range(len(rim) - 1):
        (x0, y0), (x1, y1) = rim[i], rim[i + 1]
        c.line(x0, y0 + 1, x1, y1, edge_tone if i == 0 else mid)
    # Light from below: the foot mirrors the ground, brightest on the ground line and gone a few rows
    # up. The lit plane takes it a stop hotter than the shadow plane, and each row up is narrower, so
    # the glow rises as a lick, not a band.
    rows = min(max(1, int(h * 0.25)), 1 + int(round(2 * heat)))
    lit_tones = [hot if heat > 0.66 else mid, mid, edge_tone]
    dark_tones = [mid if heat > 0.66 else edge_tone, edge_tone, None]
    for j in range(rows):
        y = gy - j
        t = j / float(h)
        xl = lerp(foot_l, ax, t)
        xr = lerp(foot_r, ax, t)
        xm = lerp(ridge, ax, t)
        inset = 1 + j
        c.hline(xl + inset, xm - 1, y, lit_tones[j])
        if dark_tones[j]:
            c.hline(xm + 1, xr - inset, y, dark_tones[j])


def crack(c, path, ramp, lip=True, start=0.0):
    """A glowing crack in the ground along `path`, hot at its first point and cooling down `ramp`
    to its last, with a lip of occlusion under its open end so it reads as a groove. `start` skips
    the first part of its length."""
    core, hot, mid, edge = ramp
    pts = []
    for i in range(len(path) - 1):
        (x0, y0), (x1, y1) = path[i], path[i + 1]
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for k in range(int(n)):
            t = k / float(n)
            pts.append((int(round(lerp(x0, x1, t))), int(round(lerp(y0, y1, t)))))
    pts.append((int(path[-1][0]), int(path[-1][1])))
    total = float(max(1, len(pts) - 1))
    # The lip first, so the glow is laid over it: only where the crack is open wide, near the mouth -
    # a lip under its whole length drew a black rule across the card.
    if lip:
        for i, (x, y) in enumerate(pts):
            f = i / total
            if start <= f < 0.45:
                c.set(x, y + 1, OCC)
    for i, (x, y) in enumerate(pts):
        f = i / total
        if f < start:
            continue
        c.set(x, y, hot if f < 0.2 else (mid if f < 0.6 else edge))


def chip(c, x, y):
    """A chip of glass in the air: four pixels, lit on top, its shadow plane to the right."""
    c.set(x, y - 1, GLASS[0])
    c.set(x, y, GLASS[1])
    c.set(x + 1, y, GLASS[3])
    c.set(x, y + 1, GLASS[2])
