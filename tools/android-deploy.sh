#!/usr/bin/env bash
# Build Wizard Survivors for Android, install it on the connected phone, launch it, and stream its
# log. One command for the phone debugging loop.
#
#   tools/android-deploy.sh            export, install, launch, then follow the log (Ctrl+C to stop)
#   tools/android-deploy.sh --no-log   export, install, launch, and return
#   tools/android-deploy.sh --log      only follow the log of a build already installed
#
# Needs: $GODOT_BIN (see .claude/settings.json), and adb. adb is found on PATH, else under
# $ANDROID_SDK_ROOT / $ANDROID_HOME, else the SDK path Godot's editor settings point at on this
# machine. The phone must already be paired for wireless debugging and show up in "adb devices".
#
# The export is a DEBUG build signed with Godot's debug keystore from the editor settings, so it
# needs no release keystore and nothing machine-specific lives in export_presets.cfg.
#
# Godot's headless export can finish the APK and then fail to exit, so it runs under a timeout and
# success is judged by the APK being written, not by the exit code.
set -u

PACKAGE="com.ecec.wizardsurvivors"
APK="build/android/WizardSurvivors.apk"
cd "$(dirname "$0")/.." || exit 1

find_adb() {
	if command -v adb >/dev/null 2>&1; then command -v adb; return; fi
	for root in "${ANDROID_SDK_ROOT:-}" "${ANDROID_HOME:-}" "$HOME/Android/Sdk" "${LOCALAPPDATA:-}/Android/Sdk"; do
		[ -n "$root" ] && [ -x "$root/platform-tools/adb.exe" ] && { echo "$root/platform-tools/adb.exe"; return; }
		[ -n "$root" ] && [ -x "$root/platform-tools/adb" ] && { echo "$root/platform-tools/adb"; return; }
	done
}

ADB="$(find_adb)"
if [ -z "$ADB" ]; then
	echo "adb not found. Install the Android SDK platform-tools or set ANDROID_SDK_ROOT." >&2
	exit 1
fi

if ! "$ADB" devices | grep -q "device$"; then
	echo "No phone connected. Pair it for wireless debugging and check \"$ADB devices\"." >&2
	exit 1
fi

# Wireless debugging can list the same phone twice - once by its mDNS name, once by IP after an
# "adb connect" - and then every adb call fails with "more than one device". Pin the first one
# unless the caller already chose with ANDROID_SERIAL.
if [ -z "${ANDROID_SERIAL:-}" ]; then
	ANDROID_SERIAL="$("$ADB" devices | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
	export ANDROID_SERIAL
fi

follow_log() {
	echo "--- following the game log (Ctrl+C to stop) ---"
	"$ADB" logcat -v time godot:V DOTNET:V SurfaceWatchdog:V AndroidRuntime:E "*:S"
}

if [ "${1:-}" = "--log" ]; then
	follow_log
	exit 0
fi

if [ -z "${GODOT_BIN:-}" ] || [ ! -x "$GODOT_BIN" ]; then
	echo "GODOT_BIN is not set or not executable." >&2
	exit 1
fi

# The export is a Gradle build (for the GL surface watchdog in android/build/src/.../GodotApp.java,
# see CLAUDE.md). Git tracks the template's source but not its 264 MB libs/, so a fresh checkout
# restores the Godot AARs from the installed export templates' android_source.zip. Per directory,
# not 'libs/*': this unzip silently extracts nothing for that pattern, which left a fresh worktree
# exporting without the AARs.
if [ ! -f android/build/libs/debug/godot-lib.template_debug.aar ]; then
	ZIP="${APPDATA:-}/Godot/export_templates/$(cat android/.build_version)/android_source.zip"
	if [ ! -f "$ZIP" ]; then
		echo "Missing $ZIP - install the full $(cat android/.build_version) export templates." >&2
		exit 1
	fi
	echo "Restoring android/build/libs from $ZIP ..."
	unzip -o -q "$ZIP" 'libs/debug/*' 'libs/release/*' -d android/build || exit 1
fi

mkdir -p "$(dirname "$APK")"
rm -f "$APK" "$APK.idsig"
echo "Exporting $APK ..."
timeout 900 "$GODOT_BIN" --headless --path . --export-debug "Android" "$APK" > build/android/export.log 2>&1
if [ ! -s "$APK" ]; then
	echo "Export failed - no APK written. See build/android/export.log." >&2
	grep -E "^ERROR" build/android/export.log | grep -v "invalid UID" | head -10 >&2
	exit 1
fi

echo "Installing ..."
"$ADB" install -r "$APK" || exit 1

echo "Launching ..."
"$ADB" shell am force-stop "$PACKAGE"
"$ADB" logcat -c
"$ADB" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1

[ "${1:-}" = "--no-log" ] && exit 0
follow_log
