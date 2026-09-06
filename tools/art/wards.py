# -*- coding: utf-8 -*-
"""Render the Vigil title screen once per candidate magic colour, for choosing between them.

The ward ramp is one name in vigil(): it drives the circle, the orb, the motes, the light the
figure catches from below, and the glow on the staff. Changing it changes every one of those
together, which is the only reason a colour swap is a one-line experiment here.

Writes into tools/art/_wards/, which .gitignore drops with the rest of tools/art/_*.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bonelight as bl
import splash

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_wards")


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    # element ramp -> the MATERIAL row the floor and the flame blooms come up in. The pairing
    # is the whole job: an unmatched ground leaves green fire standing on an amber floor.
    GROUND = {"light": "gold", "ice": "arcane", "poison": "lichen", "metal": "steel",
              "wind": "lichen", "arcane": "violet", "water": "arcane",
              "lightning": "gold", "grass": "linen", "darkness": "violet"}
    names = sys.argv[1:] or ["light", "ice", "poison", "metal", "wind", "arcane"]
    for name in names:
        c = splash.vigil(ward=bl.ELEMENTS[name],
                         ground=bl.MATERIALS[GROUND[name]])[0]
        bad = c.audit()
        c.save(os.path.join(OUT, name + ".png"))
        print("%-9s off-contract: %d" % (name, len(bad)))
    print("wrote " + OUT)


if __name__ == "__main__":
    main()
