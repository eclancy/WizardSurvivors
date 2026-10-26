#!/usr/bin/env bash
# Publish a new version of Wizard Survivors. Every Windows copy picks it up the next time it
# starts, and every Android copy says on its title screen that it is out.
#
#   bash tools/release/release.sh 0.2.0 ["What changed, for players"]
#   bash tools/release/release.sh --dry-run 0.2.0     everything but the commit, tag, push and release
#
# The optional second argument heads the release notes, and ecec.dev/games/wizard-survivors shows
# it under "What's new". The download instructions are always appended below it, and must keep
# starting with "Download **": that page strips the paragraph that starts that way.
#
# Commits the version bump, tags it, exports the Windows and Linux builds and the Android APK,
# and makes a GitHub Release with the installer (WizardSurvivors-Setup.exe, built with Inno Setup
# from installer.iss), the full zip (the portable version), the small patch zip, the Linux download
# (WizardSurvivors-linux.tar.gz, with an install.sh) and its patch, the release-signed APK
# (WizardSurvivors-android.apk) and version.json. ecec.dev/wizard-survivors/version.json
# redirects to the newest release's version.json, so nothing on ecec.dev needs deploying. See
# .ai/releasing.md. Ported from DrawFight's tools/release/release.sh.
#
# --dry-run builds every file a release would carry into build/release/ and verifies the APK's
# signature, then puts project.godot and export_presets.cfg back: no commit, no tag, no push, no
# GitHub Release. It is how a release is rehearsed - above all the Android signing, which cannot
# be checked any other way short of publishing.
set -euo pipefail

DRY_RUN=false
ARGS=()
for arg in "$@"; do
	if [ "$arg" = "--dry-run" ]; then DRY_RUN=true; else ARGS+=("$arg"); fi
done
VERSION="${ARGS[0]:-}"
CHANGES="${ARGS[1]:-}"
HOW_TO="Download **WizardSurvivors-Setup.exe** and run it, then play from the Start menu or the desktop. It keeps itself up to date from then on. No installer? WizardSurvivors-windows.zip is the portable version: unzip it anywhere you can write to and run WizardSurvivors.exe. On an Android phone, download WizardSurvivors-android.apk on the phone and open it (allow your browser to install apps when Android asks); the game says on its title screen when a newer version is out. On Linux, download **WizardSurvivors-linux.tar.gz**, unpack it and run **./install.sh** - it adds Wizard Survivors to your app menu and keeps itself up to date too. Download page: https://ecec.dev/wizard-survivors/download"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
	echo "usage: bash tools/release/release.sh [--dry-run] X.Y.Z [\"what changed\"]" >&2
	exit 1
fi

cd "$(dirname "$0")/../.."

# --- Android version code ------------------------------------------------------------------
# Android installs an APK over an older one only if its versionCode is higher, and refuses a
# lower one outright, so the code must climb with every release and can never be reused. It is
# derived from the version rather than counted, so it can be worked out from the tag alone:
# major*10000 + minor*100 + patch (0.3.1 -> 301, 1.2.0 -> 10200). That only stays monotonic while
# minor and patch stay under 100, hence the check.
IFS=. read -r V_MAJOR V_MINOR V_PATCH <<<"$VERSION"
V_MAJOR=$((10#$V_MAJOR)); V_MINOR=$((10#$V_MINOR)); V_PATCH=$((10#$V_PATCH))
if [ "$V_MINOR" -ge 100 ] || [ "$V_PATCH" -ge 100 ]; then
	echo "minor and patch must stay under 100, or the Android version code stops climbing - see .ai/releasing.md" >&2
	exit 1
fi
VERSION_CODE=$((V_MAJOR * 10000 + V_MINOR * 100 + V_PATCH))
CURRENT_CODE=$(sed -n 's/^version\/code=\([0-9]*\)$/\1/p' export_presets.cfg)
if [ -z "$CURRENT_CODE" ]; then
	echo "export_presets.cfg has no version/code line for the Android preset" >&2
	exit 1
fi
if [ "$VERSION_CODE" -le "$CURRENT_CODE" ]; then
	echo "Android version code $VERSION_CODE (from $VERSION) is not above the last one, $CURRENT_CODE - Android would refuse it as an update" >&2
	exit 1
fi

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

# --- Android prerequisites, all checked before anything is touched -------------------------
GODOT_DATA="$(cygpath -u "${APPDATA:-}" 2>/dev/null || echo "${APPDATA:-}")/Godot"
# The release keystore and its password live beside Godot's debug keystore, outside the repo.
# This repo is public: the password is read from the secret file into the environment of the
# Godot export and of keytool, and nowhere else - never into export_presets.cfg, a log, or the
# command line. Losing either file means no Android copy can ever be updated again; see
# .ai/releasing.md, "The signing key".
KEYSTORE="${WIZARDSURVIVORS_KEYSTORE:-$GODOT_DATA/keystores/wizardsurvivors-release.keystore}"
KEYSTORE_SECRET="${KEYSTORE%.keystore}.secret"
[ -f "$KEYSTORE" ] || { echo "release keystore not found: $KEYSTORE - see .ai/releasing.md, \"The signing key\"" >&2; exit 1; }
[ -f "$KEYSTORE_SECRET" ] || { echo "keystore secret not found: $KEYSTORE_SECRET" >&2; exit 1; }
KEY_ALIAS=$(sed -n 's/^alias=//p' "$KEYSTORE_SECRET" | tr -d '\r')
[ -n "$KEY_ALIAS" ] || { echo "$KEYSTORE_SECRET has no alias= line" >&2; exit 1; }
grep -q '^password=.' "$KEYSTORE_SECRET" || { echo "$KEYSTORE_SECRET has no password= line" >&2; exit 1; }

# The Android SDK (apksigner, aapt) and the JDK (keytool) are wherever the Godot editor was told
# they are, which is where the export itself looks.
EDITOR_SETTINGS=$(ls "$GODOT_DATA"/editor_settings-4.*.tres 2>/dev/null | sort -V | tail -1 || true)
editor_setting() {
	[ -n "$EDITOR_SETTINGS" ] && sed -n "s|^export/android/$1 = \"\(.*\)\"\$|\1|p" "$EDITOR_SETTINGS"
	return 0
}
ANDROID_SDK="${ANDROID_SDK_ROOT:-$(editor_setting android_sdk_path)}"
ANDROID_SDK=$(cygpath -u "$ANDROID_SDK" 2>/dev/null || echo "$ANDROID_SDK")
BUILD_TOOLS=$(ls -d "$ANDROID_SDK"/build-tools/* 2>/dev/null | sort -V | tail -1 || true)
APKSIGNER="$BUILD_TOOLS/apksigner.bat"
[ -f "$APKSIGNER" ] || APKSIGNER="$BUILD_TOOLS/apksigner"
AAPT="$BUILD_TOOLS/aapt.exe"
[ -f "$AAPT" ] || AAPT="$BUILD_TOOLS/aapt"
[ -f "$APKSIGNER" ] && [ -f "$AAPT" ] || { echo "no apksigner/aapt under $ANDROID_SDK/build-tools - set ANDROID_SDK_ROOT" >&2; exit 1; }
JDK="${JAVA_HOME:-$(editor_setting java_sdk_path)}"
JDK=$(cygpath -u "$JDK" 2>/dev/null || echo "$JDK")
KEYTOOL="$JDK/bin/keytool.exe"
[ -f "$KEYTOOL" ] || KEYTOOL="$JDK/bin/keytool"
[ -f "$KEYTOOL" ] || { echo "keytool not found under $JDK - set JAVA_HOME" >&2; exit 1; }

# The export is a Gradle build (the GL surface watchdog in android/build/src/.../GodotApp.java,
# see CLAUDE.md). Git tracks the template's source but not its 264 MB libs/, which come from the
# installed export templates. Per directory, not 'libs/*': this unzip silently extracts nothing
# for that pattern.
ANDROID_SOURCE_ZIP="$GODOT_DATA/export_templates/$(cat android/.build_version)/android_source.zip"
if [ ! -f android/build/libs/release/godot-lib.template_release.aar ] && [ ! -f "$ANDROID_SOURCE_ZIP" ]; then
	echo "missing $ANDROID_SOURCE_ZIP - install the full $(cat android/.build_version) export templates" >&2
	exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
	echo "commit or stash your changes first - a release is built from what is committed" >&2
	exit 1
fi
if ! $DRY_RUN && git rev-parse -q --verify "refs/tags/v$VERSION" >/dev/null; then
	echo "v$VERSION already exists" >&2
	exit 1
fi

CURRENT=$(sed -n 's/^config\/version="\(.*\)"$/\1/p' project.godot)
if $DRY_RUN; then
	echo "Wizard Survivors $CURRENT -> $VERSION (dry run: nothing is committed or published)"
else
	echo "Wizard Survivors $CURRENT -> $VERSION"
fi

# Until the release commit exists, any way out of this script - a failed export, Ctrl+C, the end
# of a dry run - puts the two version files back, so a failure never leaves a half-bumped tree
# (which the next attempt would refuse as "uncommitted changes").
COMMITTED=false
restore_versions() {
	$COMMITTED || git checkout -q -- project.godot export_presets.cfg
}
trap restore_versions EXIT

# The version lives in project.godot, which is where the running game reads it from - and where
# the Android export takes its versionName from, because the preset's version/name is left
# empty on purpose. The version code is the one number that has to be written into the preset.
sed -i "s/^config\/version=\".*\"$/config\/version=\"$VERSION\"/" project.godot
sed -i "s/^version\/code=[0-9]*$/version\/code=$VERSION_CODE/" export_presets.cfg

dotnet build WizardSurvivors.sln -nologo -v q
rm -rf build/windows build/linux build/release build/android
mkdir -p build/windows build/linux build/android
# Godot does not create the export folder itself, and on an export error it can sit there
# instead of exiting - hence the timeout.
timeout 900 "$GODOT_BIN" --headless --path . --export-release "Windows Desktop" build/windows/WizardSurvivors.exe \
	> build/export.log 2>&1 || { tail -20 build/export.log; echo "export failed - see build/export.log" >&2; exit 1; }
[ -f build/windows/WizardSurvivors.pck ] || { echo "export produced no .pck - see build/export.log" >&2; exit 1; }
# The same game for Linux. Needs the linux_* export templates (see .ai/releasing.md).
timeout 900 "$GODOT_BIN" --headless --path . --export-release "Linux" build/linux/WizardSurvivors.x86_64 \
	> build/export-linux.log 2>&1 || { tail -20 build/export-linux.log; echo "Linux export failed - see build/export-linux.log" >&2; exit 1; }
[ -f build/linux/WizardSurvivors.pck ] || { echo "Linux export produced no .pck - see build/export-linux.log" >&2; exit 1; }

python tools/release/package.py "$VERSION"
# The installer: build/release/WizardSurvivors-Setup.exe, from the same export. The MSYS
# variables stop Git Bash rewriting ISCC's /Q and /D switches into Windows paths (ISCC would
# then see extra script names). DrawFight and Wizard Wars call it the same way.
MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' "$ISCC" /Q "/DAppVersion=$VERSION" tools/release/installer.iss \
	|| { echo "installer compile failed" >&2; exit 1; }
[ -f build/release/WizardSurvivors-Setup.exe ] || { echo "ISCC produced no WizardSurvivors-Setup.exe" >&2; exit 1; }

# --- The Android APK ------------------------------------------------------------------------
if [ ! -f android/build/libs/release/godot-lib.template_release.aar ]; then
	echo "Restoring android/build/libs from $ANDROID_SOURCE_ZIP ..."
	unzip -o -q "$ANDROID_SOURCE_ZIP" 'libs/debug/*' 'libs/release/*' -d android/build
fi

# Runs one command with the release signing in its environment, in a subshell, so the password
# exists only for the Godot export and keytool and leaves with them. Godot reads these three
# variables whenever the preset's keystore/release fields are empty, which they always are.
with_signing() {
	(
		export GODOT_ANDROID_KEYSTORE_RELEASE_PATH="$(cygpath -w "$KEYSTORE" 2>/dev/null || echo "$KEYSTORE")"
		export GODOT_ANDROID_KEYSTORE_RELEASE_USER="$KEY_ALIAS"
		GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD="$(sed -n 's/^password=//p' "$KEYSTORE_SECRET" | tr -d '\r')"
		export GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD
		"$@"
	)
}

# True if the release password appears in the file named. The comparison happens inside the
# subshell; the password itself is never printed.
password_in() {
	with_signing sh -c 'grep -qF -- "$GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD" "$1"' _ "$1"
}

APK=build/android/WizardSurvivors.apk
echo "Exporting the Android APK (a cold Gradle build takes several minutes) ..."
# The headless export can write the APK and then never exit, so it runs under a timeout and is
# judged by the APK it wrote, never by its exit code - the signature check below catches a file
# cut short. See tools/android-deploy.sh, which learned this first.
with_signing timeout 1800 "$GODOT_BIN" --headless --path . --export-release "Android" "$APK" \
	> build/android/export.log 2>&1 || true
if password_in build/android/export.log; then
	rm -f build/android/export.log
	echo "the export log contained the keystore password - deleted it; do not publish this build" >&2
	exit 1
fi
if [ ! -s "$APK" ]; then
	grep -E "^ERROR" build/android/export.log | grep -v "invalid UID" | head -10 >&2 || true
	echo "Android export wrote no APK - see build/android/export.log" >&2
	exit 1
fi

# The export rewrites the template's manifest, launcher icons and app-name strings under
# android/build/ from the preset. Committed as the export writes them, so normally nothing
# changes - but if an engine or preset change makes it differ, put the tracked files back so the
# release commit holds only the version bump and the next release does not find a dirty tree.
if [ -n "$(git status --porcelain -- android)" ]; then
	echo "the Android export rewrote files under android/ - restoring them:" >&2
	git status --porcelain -- android >&2
	git checkout -q -- android
fi

# Signed, and signed with OUR key: an APK signed with Godot's debug keystore would install, and
# then no later release could ever be installed over it. The certificate must be the release
# keystore's, compared by SHA-256 digest; keytool reads the password from the environment.
SIGNERS=$("$APKSIGNER" verify --print-certs "$APK" 2>/dev/null) \
	|| { echo "apksigner says $APK does not verify" >&2; exit 1; }
APK_CERT=$(echo "$SIGNERS" | sed -n 's/^Signer #1 certificate SHA-256 digest: *//p' | tr -d '\r')
RELEASE_CERT=$(with_signing sh -c '"$1" -list -v -keystore "$2" -alias "$3" -storepass:env GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD' \
	_ "$KEYTOOL" "$(cygpath -w "$KEYSTORE" 2>/dev/null || echo "$KEYSTORE")" "$KEY_ALIAS" \
	| sed -n 's/^[[:space:]]*SHA256: *//p' | tr -d ':\r' | tr 'A-F' 'a-f' || true)
if [ -z "$APK_CERT" ] || [ "$APK_CERT" != "$RELEASE_CERT" ]; then
	echo "$APK is not signed with the release key (APK: ${APK_CERT:-none}, release key: ${RELEASE_CERT:-unreadable})" >&2
	exit 1
fi
if echo "$SIGNERS" | grep -qE "certificate DN: .*(Android Debug|Godot Engine)"; then
	echo "$APK carries a debug certificate" >&2
	exit 1
fi
BADGING=$("$AAPT" dump badging "$APK" 2>/dev/null | sed -n 1p || true)
echo "$BADGING" | grep -q "versionCode='$VERSION_CODE' versionName='$VERSION'" \
	|| { echo "$APK has the wrong version: $BADGING" >&2; exit 1; }
cp "$APK" build/release/WizardSurvivors-android.apk
printf '%-30s %6.1f MB  (versionCode %s, signer SHA-256 %s)\n' WizardSurvivors-android.apk \
	"$(awk "BEGIN { print $(stat -c %s "$APK") / 1048576 }")" "$VERSION_CODE" "$APK_CERT"

if $DRY_RUN; then
	echo "Dry run complete: build/release/ holds every file v$VERSION would publish. Nothing was committed, tagged, pushed or released; project.godot and export_presets.cfg are put back."
	exit 0
fi

git diff --quiet project.godot export_presets.cfg || git commit -q -m "Release v$VERSION" project.godot export_presets.cfg
COMMITTED=true
git tag "v$VERSION"
git push -q origin HEAD "v$VERSION"

gh release create "v$VERSION" --repo eclancy/WizardSurvivors \
	build/release/WizardSurvivors-Setup.exe build/release/WizardSurvivors-windows.zip \
	build/release/WizardSurvivors-patch.zip build/release/WizardSurvivors-android.apk \
	build/release/WizardSurvivors-linux.tar.gz build/release/WizardSurvivors-linux-patch.zip \
	build/release/version.json \
	--title "Wizard Survivors $VERSION" \
	--notes "${CHANGES:+$CHANGES

}$HOW_TO"

echo "Released v$VERSION - every Windows copy updates the next time it starts, and every Android copy offers it on its title screen."
