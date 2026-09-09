# -*- coding: utf-8 -*-
"""Render the Vigil title screen once per backdrop density preset, for choosing between them.

Writes into tools/art/_woods/, which .gitignore drops with the rest of tools/art/_*.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import splash

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_woods")


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    for name in sys.argv[1:] or ["current", "thicket", "rank", "walls"]:
        c = splash.vigil(wood=name)[0]
        c.save(os.path.join(OUT, name + ".png"))
        print("%-9s off-contract: %d" % (name, len(c.audit())))
    print("wrote " + OUT)


if __name__ == "__main__":
    main()
