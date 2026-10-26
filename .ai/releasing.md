# Releasing

Decided 2026-10-12, ported from DrawFight (whose `.ai/releasing.md` is where this design was
first argued and tested). How a downloadable copy of Wizard Survivors gets to people, and how it
keeps itself up to date.

**In one line:** `bash tools/release/release.sh X.Y.Z "What changed"` makes a GitHub Release. The
next time a Windows copy starts, it finds the release and updates itself; an Android copy says on
its title screen that the new version is out and offers the APK.

## Where things live

| What | Where | Why |
|---|---|---|
| The download page | `ecec.dev/wizard-survivors/download` (in the EricClancyEngineeringAndCrafts repo) | the page people are sent; it offers the installer, with the zip as the portable fallback |
| The game files | GitHub Releases on `eclancy/WizardSurvivors` | GitHub serves release downloads free with no bandwidth limit, so the droplet's 1 TB a month is left alone |
| The update check | `ecec.dev/wizard-survivors/version.json` | an nginx 302 to `github.com/eclancy/WizardSurvivors/releases/latest/download/version.json` |
| "What's new" | `ecec.dev/games/wizard-survivors` | shows the newest release's notes, minus the download paragraph |

The update address is on ecec.dev, not GitHub, because it is the one address compiled into every
copy ever downloaded (`UpdateScreen.VersionUrl`). Behind a redirect, the releases can move
somewhere else later without breaking old copies. The download button links to GitHub's
`releases/latest/download/...`, which always points at the newest release, so **neither the page
nor ecec.dev needs a deploy for a release**.

## What a release contains

Seven files: four made from the export in `build/windows/`, two from the export in
`build/linux/` (see "Linux" below), and the Android APK.

- **`WizardSurvivors-Setup.exe`**: the installer, and the file a new player downloads. Compiled
  by `release.sh` with Inno Setup from `tools/release/installer.iss`. See "The installer" below.
- **`WizardSurvivors-windows.zip`** (about 69 MB): the **portable version**, for anyone who would
  rather not run an installer. Everything, in a `WizardSurvivors/` folder, with a
  `How to play.txt` (SmartScreen, where to keep the folder, the controls). The controls in it are
  written from the input map in `project.godot`; change one and change the other.
- **`WizardSurvivors-patch.zip`** (about 2 MB): `WizardSurvivors.pck` (every image, sound,
  setting and scene) and `WizardSurvivors.dll` with its two JSON files (all the C#). These are the
  only files that change when the game changes and Godot does not. The rest of the full zip is
  the Godot engine and the .NET runtime.
- **`WizardSurvivors-android.apk`** (about 90 MB): the Android game, release-signed. See
  "Android" below.
- **`version.json`**: the version, the Godot version it was built with, and the URLs of both
  zips, the installer (`"installer"`; the game itself does not read that one) and the APK
  (`"android"`, read only by the Android copy), and under `"linux"` the Linux pair. Adding a
  field is safe: `UpdateScreen` reads the fields it names with `GetProperty`/`TryGetProperty` and
  ignores the rest, so every Windows copy already out there carries on as before.

`tools/release/package.py` makes the zips and `version.json`; ISCC makes the installer;
`release.sh` exports, checks and copies the APK.

The export preset is `Windows Desktop` in `export_presets.cfg`: x86_64, `.pck` beside the exe
rather than embedded (the patch depends on that), `tools/`, `docs/`, `.ai/` and `build/`
excluded. Everything under `build/` is gitignored.

## The installer

`tools/release/installer.iss`, Inno Setup 6. `release.sh` compiles it after `package.py` with
`/DAppVersion=X.Y.Z`, into `build/release/WizardSurvivors-Setup.exe`.

- **It installs per user, to `%LOCALAPPDATA%\Programs\WizardSurvivors`**, with no admin
  prompt (`PrivilegesRequired=lowest`). Two reasons. The self-updater copies new files into the
  game's own folder, so that folder has to stay writable by the player - Program Files would
  leave every installed copy stuck on its first version. And no UAC prompt means one less
  scary dialog after SmartScreen's.
- It adds a Start menu shortcut and, checked by default, a desktop one, and offers "Play Wizard
  Survivors now" at the end. Uninstalling (Settings > Apps) removes the whole folder, including
  anything the updater added, but **not the save** in `%APPDATA%\Godotpp_userdata\`.
- **The `AppId` GUID in `installer.iss` must never change.** It is how Windows recognises a newer
  setup as the same program, so running a new installer upgrades in place (into the same folder,
  `UsePreviousAppDir`) instead of installing a second copy. It closes a running game first.
- An installed copy updates itself exactly like an unzipped one; the installer is only how the
  first copy arrives. Installing a newer setup over an older copy also works.
- The setup is not code-signed either, so SmartScreen warns about it the same way.

**A releasing machine needs Inno Setup:** `winget install JRSoftware.InnoSetup` (per user, to
`%LOCALAPPDATA%\Programs\Inno Setup 6\`). `release.sh` looks there, or wherever the `ISCC`
environment variable points, and stops before doing anything if it is missing.

Tested 2026-10-18 (0.2.1): a silent install to a scratch folder and a default install both
worked without an admin prompt, the installed game started, and the uninstaller left no folder
or shortcuts behind.

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

## Linux

Added 2026-10-26, on Eric's call, for every game on ecec.dev (DrawFight did it first, the same
way). `release.sh` exports the `Linux` preset (x86_64, `.pck` beside the binary) to `build/linux/`,
and `package.py` makes two more files from it:

- **`WizardSurvivors-linux.tar.gz`** (about 70 MB): the game in a `WizardSurvivors/` folder - the
  `WizardSurvivors.x86_64` binary, the `.pck`, and `data_WizardSurvivors_linuxbsd_x86_64/` with
  the .NET runtime, so nothing has to be installed first - plus `install.sh`, `icon.png` and
  `How to play.txt`. A tarball rather than a zip, because a tarball carries the "can be run"
  permission on the binary and the script; `package.py` sets it, which it can do from Windows.
- **`WizardSurvivors-linux-patch.zip`** (about 11 MB): the Linux export's `.pck` and the game's
  DLL with its two JSON files.

**`install.sh`** (`tools/release/install.sh`) is the Linux installer. It copies the game to
`~/.local/share/WizardSurvivors` and writes `wizardsurvivors.desktop` into
`~/.local/share/applications`, so it is on the app menu. No sudo and nothing outside the home
folder, for the same reason the Windows installer installs per user: the game updates itself, so
its folder must stay writable. `./install.sh --uninstall` takes it out again; the save (in
`~/.local/share/godot/app_userdata/Wizard Survivors`) is never touched. Running
`./WizardSurvivors.x86_64` straight from the unpacked folder works too.

**Updating** works as on Windows, from `version.json`'s `"linux"` pair: the patch on the same
engine, the tarball otherwise (unpacked with `System.Formats.Tar`, so the binary stays runnable).
The game writes `apply.sh`, starts it with `/bin/sh` and quits; the script waits for the game to
close, copies the new files over, `chmod +x`es the binary and starts it again. A release from
before Linux has no `"linux"` pair, and a Linux copy just goes on to the title.

**Tested so far:** the export, the packaging and the permissions in the tarball, on Windows. Not
yet run on Linux - Eric is testing it on a Linux machine.

## Android

Added 2026-10-21. Every release carries `WizardSurvivors-android.apk`, a **release-signed** APK
made from the `Android` preset in `export_presets.cfg` (arm64, portrait, a Gradle build for the
GL surface watchdog - see CLAUDE.md). It is not on the Play Store; players sideload it from the
download page.

### How the APK is built

`release.sh`, after the Windows export and the installer:

1. Restores `android/build/libs/` from the installed templates' `android_source.zip` if it is
   missing (git does not track the 264 MB of AARs). The patterns are per directory
   (`'libs/debug/*' 'libs/release/*'`): Git Bash's unzip silently extracts nothing for `'libs/*'`.
2. Runs `--export-release "Android" build/android/WizardSurvivors.apk` under a 30-minute timeout,
   with the signing key in the environment (below). A cold Gradle build takes several minutes;
   the first one on this machine took 11. The export can write the APK and then never exit, so it
   is judged by the file, not the exit code.
3. Puts back anything the export rewrote under `android/` (the manifest, launcher icons and
   app-name strings are committed as the export writes them, so normally nothing changes). The
   release commit holds only `project.godot` and `export_presets.cfg`.
4. Checks the APK: `apksigner verify --print-certs` must pass, its certificate's SHA-256 must equal
   the release keystore's (read with `keytool`), it must not carry a debug certificate
   (`Android Debug` or Godot's `Godot Engine`), and `aapt dump badging` must show this release's
   versionCode and versionName. Any failure stops the release before anything is committed.
5. Copies it to `build/release/WizardSurvivors-android.apk`.

apksigner, aapt and keytool are found where the Godot editor settings point
(`export/android/android_sdk_path`, `export/android/java_sdk_path`), or through `ANDROID_SDK_ROOT`
and `JAVA_HOME`. The script checks all of them, the keystore and the templates **before** it
touches anything.

### The signing key

| File | What |
|---|---|
| `%APPDATA%\Godot\keystores\wizardsurvivors-release.keystore` | the release keystore, alias `wizardsurvivors` |
| `%APPDATA%\Godot\keystores\wizardsurvivors-release.secret` | two lines, `alias=...` and `password=...` |

**This repo is public, so the password is never in it.** The preset's `keystore/release` fields
are left empty; Godot then reads `GODOT_ANDROID_KEYSTORE_RELEASE_PATH`, `..._USER` and
`..._PASSWORD` from the environment. `release.sh` reads the secret file and exports those three
inside a subshell around the Godot export (and around `keytool`), so the password exists only in
those two processes. It is never on a command line, never in `export_presets.cfg`, and the
script searches the export log for it and deletes the log and stops if it is there (it was not,
in testing). `WIZARDSURVIVORS_KEYSTORE` points the script at a keystore elsewhere; the secret is
the same path with `.secret` for `.keystore`.

> **Back up both files somewhere safe, off this machine** - a password manager or an encrypted
> drive, not this repo and not a public cloud folder. **If the keystore or its password is lost,
> no Android copy already installed can ever be updated again**: Android installs an update only
> if it is signed with the same key as the installed app. Every player would have to uninstall -
> losing their save - and install a new APK signed with a new key. There is no recovery.

### Version codes

Android refuses to install an APK over one with an equal or higher `versionCode`, so it has to
climb with every release. It is derived from the version: `major*10000 + minor*100 + patch`
(0.3.1 is 301, 1.2.0 is 10200). That stays monotonic only while minor and patch are under 100;
the script refuses a version that breaks it, and one whose code is not above the last.

The **version name is not stored**: the preset's `version/name` is empty, so Godot uses
`application/config/version` from `project.godot`. The **version code is stored**, in
`export_presets.cfg`, and the release commit carries it beside the version bump. Kept rather
than set-and-restored because it is not secret, it makes the committed tree describe exactly
what was published, it is what the monotonic check compares against, and
`tools/android-deploy.sh`'s debug builds then carry the same number.

### How players install it

On the phone: open `ecec.dev/wizard-survivors/download`, tap **Download for Android**, open the
downloaded `WizardSurvivors-android.apk`, and when Android asks, allow the browser to install
unknown apps (Settings > *Allow from this source*), then **Install**. Play Protect may warn that
it does not recognise the app, because it is not from the Play Store: *More details*, *Install
anyway*. The download page says all of this.

### How updates reach Android players

An Android copy cannot replace itself the way the Windows one does, and asking for
`REQUEST_INSTALL_PACKAGES` to do it would make a sideloaded game look like malware. So it only
tells the player. `scripts/UpdateBanner.cs`, laid over the title screen by `TitleScreen._Ready`:

- runs only in an exported Android build (`template` feature and `OS.GetName() == "Android"`),
  once per launch, and `--no-update` turns it off;
- fetches the same `version.json` as Windows (5-second timeout) on a worker thread, **without
  holding the title up** - the title starts at once and the strip appears whenever the answer
  arrives;
- if the version is newer *and* the release has an `"android"` URL, shows a strip across the top:
  "Version X is available", **Download** (opens the APK URL in the browser with `OS.ShellOpen`)
  and **Later** (hides it until the next launch). Every failure, and a release with no APK, shows
  nothing;
- never installs anything itself. The browser downloads, Android's installer asks, and because
  the new APK has the same signature and a higher version code, it installs over the old one and
  **the save survives**.

The strip is a CanvasLayer at 6, inside the safe area, and hides while a full-screen menu panel
(Spell Index, Unlocks, Arcane Upgrades, Options) is open, because it would sit over that panel's heading. Its
buttons take taps and clicks only and attach no `MenuNavigator`, on purpose: on the title A means
"start", and a strip that took the selection would turn a pad player's A into "open the browser".
A tap on the strip does not count as the title's "press any button".

To look at it on a desktop: `--update-banner=9.9.9` shows it with that version, no network, in
any build. With `_UiShot`: `"$GODOT_BIN" --path . --resolution 540x1212 scenes/_UiShot.tscn --
--portrait --safe-area --scene=res://scenes/TitleScreen.tscn --update-banner=0.9.9 --shot=banner`
(add `--open-menu` for the menu over it). `WIZARDSURVIVORS_UPDATE_URL` points the real check at a
test server, as on Windows.

### Switching from the debug build: uninstall first, and the save is lost

The builds `tools/android-deploy.sh` installs are signed with Godot's **debug** key, the release
APK with the **release** key. Android will not install one over the other ("App not installed as
package conflicts with an existing package"). **Anyone with the debug build - the development
phone included - must uninstall it before installing the release APK, and uninstalling deletes
the app's data, which is where the save (`user://savegame.json`) lives on Android. Their progress
is gone.** Nothing in the game can carry it across.

The same wall stands the other way: once a phone has the release build, `android-deploy.sh` cannot
install its debug build over it without an uninstall either. For day-to-day phone debugging,
keep the debug build on the development phone and judge the release APK by its signature check
(or on a second device), unless losing that phone's save is fine.

### Rehearsing it: `--dry-run`

```sh
bash tools/release/release.sh --dry-run 0.3.1
```

Does everything a release does - version bump, Windows export, both zips, `version.json`, the
installer, the Android export and every APK check - into `build/release/`, then puts
`project.godot` and `export_presets.cfg` back. No commit, no tag, no push, no GitHub Release. It
still needs a clean tree, because it builds what is committed. Run it after any change to the
preset, the keystore, the Android template or the engine.

Tested 2026-10-21: `--dry-run 0.3.1` took 10.5 minutes end to end and produced a 91 MB APK,
versionCode 301, versionName 0.3.1 (so the empty `version/name` does fall back to
`config/version`), minSdk 24, not debuggable, `INTERNET` its only permission, signed (v2 scheme)
by `CN=Eric Clancy, O=ECEC, C=US`, certificate SHA-256
`8052adfcf2f544073f05085b41fa0d025a10d4a9b9ce193af18a86ce48bddd47`. Godot's debug certificate is
`970b6d17...452c5b`, `CN=Godot, OU=Godot Engine`. The export left `android/` untouched and the
tree was clean afterwards. It has not yet been installed on a phone.

**One-time setup for Android on a releasing machine:** the **full** Godot export templates
(`android_source.zip` comes only from the whole `.tpz`, not the Windows subset), the Android SDK
with build-tools, a JDK 17, both set in the Godot editor settings, and the keystore and secret
above.

## Making a release

```sh
bash tools/release/release.sh 0.2.0 "Two new spells. The Gaoler hits less hard."
```

The quoted text is optional: it is what players see under "What's new" on
`ecec.dev/games/wizard-survivors`, so write it for them. Leave it out and the release just says
how to download. The script always appends a paragraph starting `Download **` below it; that page
strips the paragraph that starts that way, so **keep that wording when editing the script**.

This needs a clean working tree, Inno Setup, and everything the Android build needs (see
"Android" above). The script sets the version and the Android version code, builds, exports for
Windows, packages, compiles the installer, exports and checks the APK, commits
"Release v0.2.0" (`project.godot` and `export_presets.cfg`), tags it, pushes, and runs
`gh release create`. The Windows half takes about a minute; the Android export adds several more.
Rehearse it with `--dry-run` first. It
reads `GODOT_BIN` from `.claude/settings.json` when the shell does not have it.

Version numbers: bump the last number for fixes and balance changes, and the middle one for a new
chapter, wizard or spell set.

**One-time setup on a new machine:** install Godot's export templates for the exact editor
version. They come from the `godotengine/godot-builds` GitHub release, file
`Godot_v<version>-stable_mono_export_templates.tpz`, about 1.2 GB. The `windows_*` files and
`linux_release.x86_64` / `linux_debug.x86_64` are needed, unpacked into
`%APPDATA%\Godot\export_templates\<version>.stable.mono\`. The export fails with "No export
template found" until they are there.

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

## Bug reports

"Report a bug" sits beside Options on the main menu and in the pause screen's bottom row
(`scripts/ReportBugScreen.cs`). It is DrawFight's bug report screen, ported, and it posts to the
same server: `https://ecec.dev/api/draw/suggest` (EricClancyEngineeringAndCrafts,
`intake/server.js`), which saves each report under `/data/suggestions/<id>/` and emails it to Eric.
The server picks the email subject from the `game` field through an allowlist, so this game must
send exactly `Wizard Survivors`; anything else is filed as a DrawFight suggestion. The server's
rate limit (a few posts an hour per address) is shared with DrawFight.

**What is sent**, as multipart/form-data with a `WizardSurvivors/<version>` User-Agent:

| Field | Value |
|---|---|
| `game` | `Wizard Survivors` |
| `kind` | `bug` |
| `name` | what the player typed, or empty (40 characters at most) |
| `version` | `application/config/version` |
| `suggestion` | the context lines below, `---`, then what the player typed (2000 characters at most) |

The context lines are the ones the screen shows under "Sent with it", from one method, so what is
shown is what is sent: `[Wizard Survivors 0.2.1 | Windows | PC canvas 1280x720]`, the window and
screen size, and from the pause screen the chapter, wizard, time in run, player level and every
spell held with its level. **Nothing else**: no save file, no log, no device or account
identifier. Keep it that way, and if a line is added, the screen shows it automatically.

The typed text survives Back, Escape, a failed send and closing the pause screen; only a report
the server accepted clears it. A failure shows the server's own message (429 gets ours, since the
server's speaks of fighters) and keeps everything. The request times out after 15 seconds.

**Testing without emailing Eric:** set `WIZARDSURVIVORS_INTAKE_URL=http://127.0.0.1:8799` and run
any local server that answers `POST /api/draw/suggest`. `_UiShot` drives the screen:
`--press-late=ReportBugButton` (with `--open-menu --scene=res://scenes/TitleScreen.tscn`) or
`--open-pause --press-late=EscapeReportBug` in a run opens it, `--report-name=`, `--report-text=`
fill it, `--report-send` presses Send and holds the shutter for the answer, and `--report-back`
checks where focus lands afterwards.

**On Android** the export preset carries `permissions/internet=true`; a debug export adds the
permission by itself only when remote debugging, so without it the phone build could not send.

## Things to know

- **Windows SmartScreen** warns "Windows protected your PC" the first time, because neither the
  setup nor the exe is code-signed. `How to play.txt` and the download page tell people to click "More info", then
  "Run anyway".
- **Upgrading Godot** changes the engine version, so every copy downloads the full zip once.
  Install the new export templates before releasing.
- **The game must be somewhere it can write to.** The installer sees to that. Unzipped into Program Files, it cannot update
  itself and silently stays on its version. `How to play.txt` says to keep it in Documents or on
  the Desktop.
- **Windows, Linux and Android are exported.** A Mac build needs Apple notarization to open without a
  fight; iOS needs the App Store.
- The save (`user://savegame.json`) lives in `%APPDATA%\Godot\app_userdata\Wizard Survivors\`
  (on Linux, `~/.local/share/godot/app_userdata/Wizard Survivors/`), not in the game folder, so an
  update never touches it. On Android it is in the app's private
  data: an update installed over the top keeps it, an uninstall deletes it.
