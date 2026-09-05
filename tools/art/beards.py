# -*- coding: utf-8 -*-
"""Render the Vigil title screen once per beard style, for choosing between them.

Writes into tools/art/_beards/, which .gitignore drops along with the rest of tools/art/_*.
Nothing here ships; splash.py is still the only thing that writes the shipping asset.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import hero
import splash

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_beards")


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    names = sys.argv[1:] or hero.BEARD_STYLES
    for name in names:
        c = splash.vigil(beard=name)[0]
        bad = c.audit()
        c.save(os.path.join(OUT, name + ".png"))
        c.scaled(2).save(os.path.join(OUT, name + "@2x.png"))
        print("%-9s off-contract: %d" % (name, len(bad)))
    print("wrote " + OUT)


if __name__ == "__main__":
    main()
