# -*- coding: utf-8 -*-
"""The actor finish: matte, not wet. Every frame of every actor passes through here once drawn.

Eric, 2026-10-28: "characters and enemies should be slightly less 'shiny' - the highlight on some
of the characters makes them look wet. tone that down a bit". The contract's answer is in
.ai/art-direction.md section 2, "Matte, not wet"; this is the one copy of it a generator runs.

WHAT READ AS WET. A highlight reads as wet when it is the colour of the LIGHT rather than of the
thing it lies on, and when it stands hard against a dark stop with nothing between: that is how a
specular looks, and a specular is what a wet surface has and a matte one does not. Measured on
every actor sheet (the first frame of each), three places did it:

  1. THE RIM. Two thirds of rim pixels lit a surface darker than L*50 (82% one darker than L*63),
     so most of the cast wore a near-white line (#DCE8FF, L*92) straight against its darkest
     stops - the glint along a wet edge, and on a dome (an egg, a shell, a back) the reflection
     off a wet one. On the spindly ones (striders, leapers, beaters) the rim is half the visible
     width of a limb, and they read as glass. It was the majority of the bright, isolated pixels
     on nearly every enemy.
  2. HIGHLIGHTS AS POINTS AND STREAKS. `hi` laid as a dot in the middle of a dome (Mother Rot's
     sacs, the blister toad's warts) or as a one-pixel streak down a fold, and `lit` laid hard on
     `shade` or `deep` beside a crease - the Frostweaver's mantle (steel lit on steel deep, 64 L*
     apart), the Stormcaller's raincoat, the Geomancer's lime band. On the saturated player rows
     the top two stops are nearly white, so a fold drawn as "light stop beside dark stop" came out
     as satin.
  3. THE FAMILIAR'S HOT SPOTS. Its renderer lights each blob by its own normal, so the top stop
     lands as a round spot in the middle of every form, and the dither sprinkles that stop across
     the lit planes - the glitter on the Rimehound and the wet sheen on the Cerberus. familiar.py
     runs settle() below on its own ramps (Fig.render), because its coats are built from stops of
     several rows and a pixel's colour alone does not say which ramp it is on.

THE TWO RULES. Neither redraws anything: they only step a highlight down its own ramp, or swap
one rim colour for another, so every silhouette, fold and anchor is exactly where it was drawn.

  1. A highlight is a plane, not a point (settle). A `hi` or `lit` pixel that stands more than
     STEP L* above a neighbour of its own row steps one stop down, and a `hi` with no `hi` or
     `lit` of its own row beside it steps down to `lit`. The brightest stop survives where it is
     a lit plane; where it was a glint, a dot or a streak against the dark it becomes the next
     stop, so the light turns the form instead of reflecting off it. Gold, arcane and every
     element ramp are light sources (section 7) and are never stepped: eyes, lanterns, crystals,
     embers and spell light keep their glow, and so does a plane of steel - metal still gleams,
     a little.
  2. The rim is the key as the surface returns it (rims). Where the rim lights a surface lighter
     than RIM_FULL_FROM it stays the full key, RIM; on anything darker it is RIM_SOFT. A pale robe
     keeps its crisp edge, where a white line costs nothing; a dark body - most of the horde -
     gets a rim that still clears the floor by a wide margin but no longer glints.

"A bit" is the brief, so both numbers sit where the wet read goes and the look stays: the rim
still separates every actor from the floor, the light still comes from the upper left, and the
first pass changed 4.8% of all actor pixels (82% of the rim, and a median 7.4% of an idle or walk
frame).

THE HURT FLASH stays bright. It runs through here like every frame, but anim_sets' flinch steps
the whole figure up its ramps, which compresses the top of every ramp, so few of its jumps exceed
STEP: a hit still flashes.
"""
from __future__ import division

import bonelight as B

# The most a highlight may stand above a neighbour on its own ramp, in CIE L* (0..100). One stop
# is 6 to 28 L* depending on the row, so a stop count would treat rows unequally - skin's lit sits
# 41 L* over its shade but stone's hi only 20 over its deep - and the eye judges the jump.
STEP = 35.0

# A rim lighting a surface at least this light keeps the full key. RIM_SOFT is L*~74; at 63 the
# soft rim would still stand only ~11 over the surface it edges, and below that it starts to read
# as a pale outline rather than as light.
RIM_FULL_FROM = 63.0

RIM = B.RIM
RIM_SOFT = B.RIM_SOFT


def _rgb(h):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16))


def _lin(v):
    v = v / 255.0
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


_LSTAR = {}


def lstar(rgb):
    """CIE L* of an (r, g, b), 0..100. Cached: an actor has a few dozen colours."""
    rgb = tuple(rgb[:3])
    v = _LSTAR.get(rgb)
    if v is None:
        y = 0.2126 * _lin(rgb[0]) + 0.7152 * _lin(rgb[1]) + 0.0722 * _lin(rgb[2])
        v = 116.0 * (y ** (1.0 / 3.0)) - 16.0 if y > 0.008856 else 903.3 * y
        _LSTAR[rgb] = v
    return v


# Light sources (art-direction.md section 7): never stepped, wherever they appear.
_EMISSIVE = set(_rgb(t) for row in ("gold", "arcane") for t in B.MATERIALS[row])
_EMISSIVE |= set(_rgb(t) for ramp in B.ELEMENTS.values() for t in ramp)

# The rows the image-level finish knows, by colour. A colour belongs to exactly one row (the
# palette has no duplicate material tones), which is what lets a finished PNG be read back into
# ramps at all.
_ROWS = {}
_POS = {}


def add_ramp(name, tones):
    """Register a five-tone row defined outside bonelight.py, so its highlights are settled too.

    cast_sands.py's sand cloth is the one so far: the contract keeps it out of bonelight.py on
    purpose (art-direction.md section 3), and a row nobody registers is passed through untouched.
    """
    _ROWS[name] = list(tones)
    for i, t in enumerate(tones):
        _POS.setdefault(_rgb(t), (name, i))


for _name, _tones in B.MATERIALS.items():
    add_ramp(_name, _tones)


def settle(tones):
    """Rule 1. tones: {(x, y): (key, ramp, index)}, ramp a light-to-dark list of '#RRGGBB'.

    Returns {(x, y): new index} for every pixel that steps down. Neighbours count only when they
    share the key (are on the same ramp): a bright thing beside a dark thing is two objects, not a
    highlight. Decided against the tones as given, so the result does not depend on scan order.
    """
    out = {}
    for (x, y), (key, ramp, i) in tones.items():
        if i > 1:
            continue
        here = _rgb(ramp[i])
        if here in _EMISSIVE:
            continue
        same = []
        for n in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            t = tones.get(n)
            if t is not None and t[0] == key:
                same.append(t[2])
        new = i
        if same:
            darkest = min(lstar(_rgb(ramp[j])) for j in same)
            if lstar(here) - darkest > STEP:
                new = i + 1
        if i == 0 and not any(j <= 1 for j in same):
            new = max(new, 1)
        if new != i:
            out[(x, y)] = new
    return out


def tones_of(img):
    """{(x, y): (row, ramp, index)} for every opaque pixel of img on a registered row."""
    px = img.load()
    w, h = img.size
    tones = {}
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if not p[3]:
                continue
            pos = _POS.get(p[:3])
            if pos is not None:
                tones[(x, y)] = (pos[0], _ROWS[pos[0]], pos[1])
    return tones


_RIM_RGB = _rgb(RIM)
_RIM_SOFT_RGBA = _rgb(RIM_SOFT) + (255,)
# Where a rim's light lands: the key is upper left, so the surface it edges lies right of it, below
# it, or both. The second ring is for a doubled rim (two rim pixels side by side on a stair-step).
_NEAR = ((1, 0), (0, 1), (1, 1))
_FAR = ((2, 0), (0, 2))


def rims(img):
    """Rule 2, in place: every RIM pixel lighting a surface darker than RIM_FULL_FROM -> RIM_SOFT."""
    px = img.load()
    w, h = img.size
    soft = []
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if not p[3] or p[:3] != _RIM_RGB:
                continue
            lit = None
            for ring in (_NEAR, _FAR):
                for dx, dy in ring:
                    if x + dx < w and y + dy < h:
                        q = px[x + dx, y + dy]
                        if q[3] and q[:3] != _RIM_RGB:
                            v = lstar(q)
                            lit = v if lit is None else max(lit, v)
                if lit is not None:
                    break
            if lit is None or lit < RIM_FULL_FROM:
                soft.append((x, y))
    for (x, y) in soft:
        px[x, y] = _RIM_SOFT_RGBA
    return img


def finish(img):
    """Both rules, in place, on one rendered actor frame (RGBA). Returns img."""
    tones = tones_of(img)
    steps = settle(tones)
    if steps:
        px = img.load()
        for (x, y), i in steps.items():
            px[x, y] = _rgb(tones[(x, y)][1][i]) + (255,)
    return rims(img)


def is_actor_palette(pal):
    """A grid palette that carries the rim is an actor's: section 2 draws the rim on actors only."""
    return RIM in pal.values()
