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
    GROUND = {"light": "gold", "fire": "gold", "ice": "arcane", "poison": "lichen",
              "metal": "steel", "wind": "lichen", "arcane": "violet", "water": "arcane",
              "lightning": "gold", "grass": "linen", "darkness": "violet"}
    # "fire" takes its default floor from the table; "fire:red" overrides it. The floor is a
    # real choice and not a lookup - the same fire over gold reads as firelight on stone and
    # over red as something closer to a pyre, and neither is wrong.
    # Wider golds. Every stop below is already somewhere in the contract - they are pulled
    # from the light, lightning, fire, earth and gold rows and recombined - so these audit
    # clean without touching bonelight.py. The shipping "light" ramp is
    # #FFFFFF / #FFF8D8 / #FFF0A8 / #FFC63C, which is three shades of near-white and one
    # gold: almost all of its travel is in the last stop, which is why the ward reads as
    # bright rather than as gold. Each of these spends the travel differently.
    EXTRA = {
        # value: same hue family, four times the light-to-dark range
        "gold-deep": (["#FFFFFF", "#FFF0A8", "#D8B04A", "#8A6A1E"], "gold"),
        # hue: white through lemon and amber into orange
        "gold-molten": (["#FFFFFF", "#FFF06A", "#FFC848", "#E87A1A"], "gold"),
        # both, and no white at all - metal rather than light
        "gold-reliquary": (["#FFF0C0", "#E0B02A", "#C99A5E", "#7A5024"], "gold"),
        # lemon-forward with a dark edge
        "gold-brass": (["#FFFFFF", "#FFF8B0", "#E0B02A", "#8A6A1E"], "gold"),
        # amber-forward, brown edge. NOT on the flesh row: its lower stops are #6E4450 and
        # #3C2436, which are plum, and a "warm" floor picked by name came out pink.
        "gold-ember": (["#FFFFFF", "#FFC848", "#D8B04A", "#7A5024"], "gold"),
    }
    names = sys.argv[1:] or ["light", "fire", "fire:red", "ice", "poison", "metal", "wind",
                             "arcane"]
    for name in names:
        if ":" in name:
            elem, floor = name.split(":", 1)
        else:
            elem, floor = name, None
        if elem in EXTRA:
            ramp, default_floor = EXTRA[elem]
        else:
            ramp, default_floor = bl.ELEMENTS[elem], GROUND[elem]
        c = splash.vigil(ward=ramp, ground=bl.MATERIALS[floor or default_floor])[0]
        bad = c.audit()
        c.save(os.path.join(OUT, name.replace(":", "-") + ".png"))
        print("%-11s off-contract: %d" % (name, len(bad)))
    print("wrote " + OUT)


if __name__ == "__main__":
    main()
