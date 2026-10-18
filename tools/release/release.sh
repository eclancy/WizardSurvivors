#!/usr/bin/env bash
# Publish a new version of Wizard Survivors. Every copy of the game picks it up the next time it
# starts.
#
#   bash tools/release/release.sh 0.2.0 ["What changed, for players"]
#
# The optional second argument heads the release notes, and ecec.dev/games/wizard-survivors shows
# it under "What's new". The download instructions are always appended below it, and must keep
# starting with "Download **": that page strips the paragraph that starts that way.
#
# Commits the version bump, tags it, exports the Windows build, and makes a GitHub Release with
# the installer (WizardSurvivors-Setup.exe, built with Inno Setup from installer.iss), the full
# zip (the portable version), the small patch zip and version.json. ecec.dev/wizard-survivors/version.json
# redirects to the newest release's version.json, so nothing on ecec.dev needs deploying. See
# .ai/releasing.md. Ported from DrawFight's tools/release/release.sh.
set -euo pipefail

VERSION="${1:-}"
CHANGES="${2:-}"
HOW_TO="Download **WizardSurvivors-Setup.exe** and run it, then play from the Start menu or the desktop. It keeps itself up to date from then on. No installer? WizardSurvivors-windows.zip is the portable version: unzip it anywhere you can write to and run WizardSurvivors.exe. Download page: https://ecec.dev/wizard-survivors/download"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
	echo "usage: bash tools/release/release.sh X.Y.Z [\"what changed\"]" >&2
	exit 1
fi

cd "$(dirname "$0")/../.."

# A Claude session gets GODOT_BIN from .claude/settings.json; a plain shell reads it from there.
if [ -z "${GODOT_BIN:-}" ] && [ -f .claude/settings.json ]; then
	GODOT_BIN=$(sed -n 's/^ *"GODOT_BIN": *"\(.*\)",\{0,1\} *$/\1/p' .claude/settings.json | sed 's/\\\\/\\/g')
	GODOT_BIN=$(cygpath -u "$GODOT_BIN" 2>/dev/null || echo "$GODOT_BIN")
	export GODOT_BIN
fi
: "${GODOT_BIN:?GODOT_BIN is not set - see .claude/settings.json}"
[ -f "$GODOT_BIN" ] || { echo "GODOT_BIN does not exist: $GODOT_BIN" >&2; exit 1; }

# Inno Setup compiles the installer. Checked up front so a missing install cannot fail the
# release after the export. winget install JRSoftware.InnoSetup puts it here (per user).
ISCC="${ISCC:-$LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe}"
ISCC=$(cygpath -u "$ISCC" 2>/dev/null || echo "$ISCC")
[ -f "$ISCC" ] || { echo "Inno Setup's ISCC.exe not found at $ISCC - install it (winget install JRSoftware.InnoSetup) or set ISCC" >&2; exit 1; }

if [ -n "$(git status --porcelain)" ]; then
	echo "commit or stash your changes first - a release is built from what is committed" >&2
	exit 1
fi
if git rev-parse -q --verify "refs/tags/v$VERSION" >/dev/null; then
	echo "v$VERSION already exists" >&2
	exit 1
fi

CURRENT=$(sed -n 's/^config\/version="\(.*\)"$/\1/p' project.godot)
echo "Wizard Survivors $CURRENT -> $VERSION"

# The version lives in project.godot, which is where the running game reads it from.
sed -i "s/^config\/version=\".*\"$/config\/version=\"$VERSION\"/" project.godot

dotnet build WizardSurvivors.sln -nologo -v q
rm -rf build/windows build/release
mkdir -p build/windows
# Godot does not create the export folder itself, and on an export error it can sit there
# instead of exiting - hence the timeout.
timeout 900 "$GODOT_BIN" --headless --path . --export-release "Windows Desktop" build/windows/WizardSurvivors.exe \
	> build/export.log 2>&1 || { tail -20 build/export.log; echo "export failed - see build/export.log" >&2; git checkout project.godot; exit 1; }
[ -f build/windows/WizardSurvivors.pck ] || { echo "export produced no .pck - see build/export.log" >&2; git checkout project.godot; exit 1; }

python tools/release/package.py "$VERSION"
# The installer: build/release/WizardSurvivors-Setup.exe, from the same export. ISCC's switches
# are written -Q / -D rather than /Q / /D because Git Bash rewrites a leading / into a Windows
# path, and ISCC then sees extra script names.
"$ISCC" -Q "-DAppVersion=$VERSION" "$(cygpath -w tools/release/installer.iss 2>/dev/null || echo tools/release/installer.iss)" \
	|| { echo "installer compile failed" >&2; git checkout project.godot; exit 1; }
[ -f build/release/WizardSurvivors-Setup.exe ] || { echo "ISCC produced no WizardSurvivors-Setup.exe" >&2; git checkout project.godot; exit 1; }

git diff --quiet project.godot || git commit -q -m "Release v$VERSION" project.godot
git tag "v$VERSION"
git push -q origin HEAD "v$VERSION"

gh release create "v$VERSION" --repo eclancy/WizardSurvivors \
	build/release/WizardSurvivors-Setup.exe build/release/WizardSurvivors-windows.zip \
	build/release/WizardSurvivors-patch.zip build/release/version.json \
	--title "Wizard Survivors $VERSION" \
	--notes "${CHANGES:+$CHANGES

}$HOW_TO"

echo "Released v$VERSION - every copy updates the next time it starts."
