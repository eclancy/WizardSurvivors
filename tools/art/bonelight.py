# -*- coding: utf-8 -*-
"""Bonelight palette - the single machine-readable copy of .ai/art-direction.md section 3.

Every generator imports from here. If a colour changes, it changes once and every sprite is
regenerated from it, which is the whole reason the art is generated rather than hand-painted.
"""

# --- shared light (see art-direction.md section 2) ---------------------------
# DCE8FF and 232C46 are RESERVED: the per-stage shader keys on an exact colour match, so
# neither may ever appear as pigment.
OCC = "#05070C"      # occlusion / contact line, shared by everything
RIM = "#DCE8FF"      # 1px key-facing silhouette edge, actors only. reserved.
KEY = "#DCE8FF"      # key light colour, shader-side
AMBIENT = "#232C46"  # ambient fill colour, shader-side. reserved.

# --- materials: hi / lit / base / shade / deep -------------------------------
MATERIALS = {
    "stone":  ["#3A4658", "#2A3444", "#1E2634", "#141A26", "#0B0F18"],
    "lichen": ["#5A8A94", "#3A6470", "#2A4E58", "#24404A", "#16282E"],
    "wool":   ["#9AB0F0", "#6A86E0", "#3A52A8", "#23306A", "#14183A"],
    # "skin" is bone and undead pallor despite the name - it is what skulls, grave-linen
    # figures and the moon are made of. Living skin needed its own row: everything in this
    # palette but gold is cold, and a hand borrowed from the bone ramp reads as a gauntlet or
    # a corpse rather than as the hand of the person holding the staff.
    "skin":   ["#EEF2FA", "#D4DCEA", "#9AA4B8", "#626E84", "#38404F"],
    "flesh":  ["#F2DCC4", "#D8A484", "#A87058", "#6E4450", "#3C2436"],
    "gold":   ["#FFF0C0", "#D8B04A", "#8A6A1E", "#543F10", "#2E2208"],
    "steel":  ["#DCE8F4", "#B8C8DC", "#6A7E98", "#3A4A62", "#202838"],
    "red":    ["#8A3038", "#6A2028", "#4A1820", "#2A0E14", "#16070A"],
    "linen":  ["#A2B0A0", "#7A8A7C", "#46544C", "#2C3834", "#1A2220"],
    "violet": ["#6E6296", "#4E4470", "#342C4C", "#221C33", "#14101F"],
    "arcane": ["#DCF4FF", "#6AB8E8", "#2A6A9A", "#17415E", "#0B2436"],

    # --- PLAYER PIGMENT ---------------------------------------------------------------------
    # Four saturated rows, added deliberately, and reserved for the PLAYER CAST. Nothing the
    # dark wizard made or took may wear them.
    #
    # Why they had to exist at all: the rows above are the palette of a drained world and every
    # one of them is desaturated by design. The most saturated warm row was `red` #8A3038, a
    # maroon at lightness 0.27 - there was no true red in the contract, and no true green. A
    # Pyromancer cannot be drawn in a colour the palette does not contain.
    #
    # Why reserving them to the player is not a restriction but the point: `.ai/world-and-tone.md`
    # says the horde is not a faction and has no colour, and that the player carries the only
    # light. Making the starter four the only saturated things on screen says that in pigment.
    # They read as primaries against a world that has none, which is also why they work as
    # STARTER characters - four instantly separable figures for a player who knows nothing yet.
    #
    # One row per starting element, keyed to the spell each wizard opens on.
    "ember":  ["#FFC0A8", "#FF6A4A", "#D8242E", "#8A1220", "#4A0810"],   # Pyromancer / Fireball
    "azure":  ["#C8ECFF", "#6AC0FF", "#2A72D8", "#1A3E8A", "#0E1E48"],   # Frostweaver / Cone of Cold
    "amber":  ["#FFF4B0", "#FFD63A", "#D89A10", "#8A5E0A", "#463006"],   # Stormcaller / Chain Lightning
    "verdant": ["#D0F0A0", "#8AD048", "#3E8A28", "#255416", "#122A0A"],  # Geomancer / Obsidian Spike
}

ROLES = ["hi", "lit", "base", "shade", "deep"]


def tone(material, role):
    return MATERIALS[material][ROLES.index(role)]


# --- element emissive ramps: core / hot / mid / edge -------------------------
# The core of every element is white-hot; identity lives in mid and edge.
ELEMENTS = {
    "fire":      ["#FFF4D0", "#FFC848", "#E87A1A", "#B8381A"],
    "ice":       ["#F4FEFF", "#C8F4FF", "#7FDCF8", "#2E7FC4"],
    "arcane":    ["#FFF0FF", "#E8C0FF", "#C08CFF", "#7A3FD4"],
    "darkness":  ["#E8D8FF", "#B48CE8", "#8A5CC4", "#35205C"],
    "light":     ["#FFFFFF", "#FFF8D8", "#FFF0A8", "#FFC63C"],
    "grass":     ["#F4FFDC", "#CFEE94", "#9BD455", "#4A8C3A"],
    "earth":     ["#FFF0D8", "#E8C48C", "#C99A5E", "#7A5024"],
    "wind":      ["#FFFFFF", "#EAFCF6", "#B4EEE0", "#4FAE9E"],
    "lightning": ["#FFFFF0", "#FFF8B0", "#FFF06A", "#E0B02A"],
    "poison":    ["#F8FFD8", "#DCF48C", "#B4E04A", "#6B9420"],
    "metal":     ["#FFFFFF", "#EAF2FA", "#C4D4E8", "#5E7290"],
    "water":     ["#F0FAFF", "#B4DCFF", "#6FB8F0", "#2A5FC4"],
}


def build_palette(spec):
    """spec maps a single grid character -> ("material", "role") or a literal "#RRGGBB"."""
    pal = {".": None}
    for ch, val in spec.items():
        pal[ch] = val if isinstance(val, str) and val.startswith("#") else tone(val[0], val[1])
    return pal
