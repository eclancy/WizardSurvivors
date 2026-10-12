# Releasing

Decided 2026-10-12, ported from DrawFight (whose `.ai/releasing.md` is where this design was
first argued and tested). How a downloadable copy of Wizard Survivors gets to people, and how it
keeps itself up to date.

**In one line:** `bash tools/release/release.sh X.Y.Z "What changed"` makes a GitHub Release. The
next time any copy of the game starts, it finds the release and updates itself.

## Where things live

| What | Where | Why |
|---|---|---|
| The download page | `ecec.dev/wizard-survivors/download` (in the EricClancyEngineeringAndCrafts repo) | the page people are sent |
| The game files | GitHub Releases on `eclancy/WizardSurvivors` | GitHub serves release downloads free with no bandwidth limit, so the droplet's 1 TB a month is left alone |
| The update check | `ecec.dev/wizard-survivors/version.json` | an nginx 302 to `github.com/eclancy/WizardSurvivors/releases/latest/download/version.json` |
| "What's new" | `ecec.dev/games/wizard-survivors` | shows the newest release's notes, minus the download paragraph |

The update address is on ecec.dev, not GitHub, because it is the one address compiled into every
copy ever downloaded (`UpdateScreen.VersionUrl`). Behind a redirect, the releases can move
somewhere else later without breaking old copies. The download button links to GitHub's
`releases/latest/download/...`, which always points at the newest release, so **neither the page
nor ecec.dev needs a deploy for a release**.

## What a release contains

`tools/release/package.py` makes three files from the export in `build/windows/`:

- **`WizardSurvivors-windows.zip`** (about 69 MB): everything, in a `WizardSurvivors/` folder,
  with a `How to play.txt` (SmartScreen, where to keep the folder, the controls). This is the file
  a new player downloads. The controls in it are written from the input map in `project.godot`;
  change one and change the other.
- **`WizardSurvivors-patch.zip`** (about 2 MB): `WizardSurvivors.pck` (every image, sound,
  setting and scene) and `WizardSurvivors.dll` with its two JSON files (all the C#). These are the
  only files that change when the game changes and Godot does not. The rest of the full zip is
  the Godot engine and the .NET runtime.
- **`version.json`**: the version, the Godot version it was built with, and the URLs of both zips.

The export preset is `Windows Desktop` in `export_presets.cfg`: x86_64, `.pck` beside the exe
rather than embedded (the patch depends on that), `tools/`, `docs/`, `.ai/` and `build/`
excluded. Everything under `build/` is gitignored.

## How a copy updates

`scripts/UpdateScreen.cs` runs first in an exported Windows build. It never runs in the editor,
from `godot --path .`, or in any harness, because it checks `OS.HasFeature("template")`.

It is not a scene. `TitleScreen.tscn` stays the main scene, and `TitleScreen._Ready` lays
`UpdateScreen` over the artwork before the prompt, the music or the menu start; input to the title
is ignored until it is done. It runs once per launch, so returning to the title after a run does
not check again.

1. It fetches `version.json` (5-second timeout) and compares versions number by number, so
   0.10.0 beats 0.9.0. The game's own version is `application/config/version` in
   `project.godot`, which the release script sets.
2. If the release was built with the same Godot version as this copy, it downloads the patch;
   otherwise the full zip. It unpacks to `user://update/`.
3. Windows will not let a running program overwrite its own files. So the game writes
   `apply.cmd`, starts it, and quits. The script waits for the game to close, `robocopy`s the new
   files over the old ones, and starts the game again.

Anything that goes wrong, including no internet, GitHub being down, a web page where the JSON
should be, or a folder the game cannot write to, just goes on to the title screen. **An update is never worth not being able to
play.** The current version shows small in the bottom-left corner of the title screen.

Tested end to end on 2026-10-12: a 0.0.9 export in `build/test/` updated itself to 0.1.0 from a
local server in about 8 seconds and restarted, and its `.pck` and DLL came out byte-identical to
the 0.1.0 export. Relaunched, it said it was up to date and went to the title. With the server
down, and against the real address before it existed, it went straight to the title.

## Making a release

```sh
bash tools/release/release.sh 0.2.0 "Two new spells. The Gaoler hits less hard."
```

The quoted text is optional: it is what players see under "What's new" on
`ecec.dev/games/wizard-survivors`, so write it for them. Leave it out and the release just says
how to download. The script always appends a paragraph starting `Download **` below it; that page
strips the paragraph that starts that way, so **keep that wording when editing the script**.

This needs a clean working tree. The script sets the version, builds, exports, packages, commits
"Release v0.2.0", tags it, pushes, and runs `gh release create`. It takes about a minute. It
reads `GODOT_BIN` from `.claude/settings.json` when the shell does not have it.

Version numbers: bump the last number for fixes and balance changes, and the middle one for a new
chapter, wizard or spell set.

**One-time setup on a new machine:** install Godot's export templates for the exact editor
version. They come from the `godotengine/godot-builds` GitHub release, file
`Godot_v<version>-stable_mono_export_templates.tpz`, about 1.2 GB. Only the `windows_*` files are
needed, unpacked into `%APPDATA%\Godot\export_templates\<version>.stable.mono\`. The export fails
with "No export template found" until they are there.

**Testing an update without publishing:**

1. Export the current version to `build/windows/` and run `package.py` (or let a dry run of the
   steps in `release.sh` do it).
2. Copy `build/release/` somewhere, and edit its `version.json` so the two zip URLs point at a
   local server (`http://127.0.0.1:8765/...`). Serve it: `python -m SimpleHTTPServer 8765`.
3. Temporarily set `config/version` lower, export to `build/test/`, and put it back.
4. Run `build/test/WizardSurvivors.exe` with `WIZARDSURVIVORS_UPDATE_URL` set to the local
   `version.json`. It should update, restart, and on the next launch say it is up to date. The
   relaunched copy inherits the variable.

Run any build with `--no-update` to skip the check entirely. The exported exe prints to stdout
when started from a shell; `user://logs/` is shared with every other copy and editor session on
the machine, so do not trust it for this.

## Things to know

- **Windows SmartScreen** warns "Windows protected your PC" the first time, because the exe is
  not code-signed. `How to play.txt` and the download page tell people to click "More info", then
  "Run anyway".
- **Upgrading Godot** changes the engine version, so every copy downloads the full zip once.
  Install the new export templates before releasing.
- **The game must be somewhere it can write to.** Unzipped into Program Files, it cannot update
  itself and silently stays on its version. `How to play.txt` says to keep it in Documents or on
  the Desktop.
- **Only Windows is exported.** A Mac build needs Apple notarization to open without a fight, and
  the portrait viewport was laid out with phones in mind, which are not exported either.
- The save (`user://savegame.json`) lives in `%APPDATA%\Godot\app_userdata\Wizard Survivors\`,
  not in the game folder, so an update never touches it.
