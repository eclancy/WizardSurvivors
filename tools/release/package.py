# -*- coding: utf-8 -*-
"""Turn an export in build/windows/ into the three files a GitHub Release carries.

    python tools/release/package.py 0.2.0

Writes build/release/:

  WizardSurvivors-windows.zip   everything, in a WizardSurvivors/ folder - the portable
                                version (the installer is what a new player downloads)
  WizardSurvivors-patch.zip     just the game: the .pck and the game's own assembly. A fraction
                                of the full zip, because the engine and the .NET runtime only
                                change when Godot is upgraded.
  version.json                  what the game's update check reads (see scripts/UpdateScreen.cs)

The installer, WizardSurvivors-Setup.exe, is compiled from the same export by release.sh
(tools/release/installer.iss); version.json carries its URL. The full zip is the portable version.

Normally run by tools/release/release.sh rather than by hand. See .ai/releasing.md.
Python 2.7, like every other tool in this repo - it is the only Python on the machine.
"""
from __future__ import print_function

import io
import json
import os
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXPORT = os.path.join(ROOT, "build", "windows")
OUT = os.path.join(ROOT, "build", "release")
REPO = "eclancy/WizardSurvivors"
NAME = "WizardSurvivors"
DATA = "data_%s_windows_x86_64" % NAME
FULL_ZIP = NAME + "-windows.zip"
PATCH_ZIP = NAME + "-patch.zip"
INSTALLER = NAME + "-Setup.exe"

# The engine the export was made with. A patch is only offered to a copy on the same engine;
# anything else downloads the full zip. Read from the editor binary's name rather than
# hard-coded, so upgrading Godot cannot leave this behind.
GODOT = os.environ.get("GODOT_BIN", "")

# What the patch carries: everything that changes when the game changes and the engine does
# not. The .pck is every scene, image, sound and setting; the WizardSurvivors.* files are the C#.
PATCH = [NAME + ".pck", DATA + "/" + NAME + ".dll", DATA + "/" + NAME + ".deps.json",
         DATA + "/" + NAME + ".runtimeconfig.json"]

# The controls are the input map in project.godot (move_*, ui_*, pause). The README has no
# controls section, so this is the one place they are written down for a player.
HOW_TO_PLAY = u"""Wizard Survivors
================

You are a lone wizard in a haunted wood, and the dead keep coming. You only move - your spells
cast themselves. Gather what the fallen leave behind to level up, choose new spells and
upgrades, and survive each chapter long enough to face the boss at its end.

1. Double-click WizardSurvivors.exe.

2. The first time, Windows may say "Windows protected your PC". That is because this game
   is not from a big company that paid for a signature. Click "More info", then "Run anyway".

3. Controls (keyboard or an Xbox controller):

     Move            WASD or the arrow keys, or the left stick
     Attack          nothing to press - your spells fire on their own
     Menus           arrows / WASD / d-pad to choose, Enter or Space or A to pick,
                     Esc or B to go back (the mouse works in menus too)
     Pause           Esc, or Start on a controller

The game updates itself: every time it starts, it checks for a new version and installs it.
Keep this folder somewhere you can write to, like Documents or the Desktop, so it can.

Made at https://ecec.dev - download page: https://ecec.dev/wizard-survivors/download
"""


def engine_version():
    name = os.path.basename(GODOT)
    # Godot_v4.5.1-stable_mono_win64_console.exe -> 4.5.1
    if name.startswith("Godot_v"):
        return name[len("Godot_v"):].split("-")[0]
    raise SystemExit("set GODOT_BIN to the Godot editor, so the engine version is known")


def add_tree(z, src, prefix):
    for dirpath, _dirs, files in os.walk(src):
        for f in sorted(files):
            full = os.path.join(dirpath, f)
            rel = os.path.relpath(full, src).replace(os.sep, "/")
            z.write(full, prefix + rel)


def main(argv):
    if not argv:
        print(__doc__)
        return 1
    version = argv[0]
    # Before writing anything, so a missing GODOT_BIN cannot leave half a release behind.
    engine = engine_version()
    if not os.path.isfile(os.path.join(EXPORT, NAME + ".pck")):
        raise SystemExit("no export in build/windows - run the Godot export first")
    for rel in PATCH:
        if not os.path.isfile(os.path.join(EXPORT, rel.replace("/", os.sep))):
            raise SystemExit("the export has no %s - is the preset still embedding nothing?" % rel)
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    full = os.path.join(OUT, FULL_ZIP)
    z = zipfile.ZipFile(full, "w", zipfile.ZIP_DEFLATED, allowZip64=True)
    add_tree(z, EXPORT, NAME + "/")
    z.writestr(NAME + "/How to play.txt", HOW_TO_PLAY.replace(u"\n", u"\r\n").encode("utf-8"))
    z.close()

    patch = os.path.join(OUT, PATCH_ZIP)
    z = zipfile.ZipFile(patch, "w", zipfile.ZIP_DEFLATED)
    for rel in PATCH:
        z.write(os.path.join(EXPORT, rel.replace("/", os.sep)), rel)
    z.close()

    base = "https://github.com/%s/releases/download/v%s/" % (REPO, version)
    info = {
        "version": version,
        "engine": engine,
        "patch": base + PATCH_ZIP,
        "full": base + FULL_ZIP,
        # Built by release.sh with Inno Setup after this script runs. The game does not read it;
        # it is there for anything that wants this release's installer by URL.
        "installer": base + INSTALLER,
    }
    with io.open(os.path.join(OUT, "version.json"), "w", encoding="utf-8") as f:
        f.write(unicode(json.dumps(info, indent=2, sort_keys=True)) + u"\n")

    for name in (FULL_ZIP, PATCH_ZIP, "version.json"):
        print("%-30s %6.1f MB" % (name, os.path.getsize(os.path.join(OUT, name)) / 1048576.0))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
