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
