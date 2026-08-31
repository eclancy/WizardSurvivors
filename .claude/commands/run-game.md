---
description: Launch the game in Godot
argument-hint: "[extra godot args]"
allowed-tools: Bash
---
Launch the project with: `"$GODOT_BIN" --path . $ARGUMENTS`

`GODOT_BIN` is set in `.claude/settings.json`. If it is unset or the file does
not exist, stop and tell me — do not guess a path.

If it fails with `Unrecognized UID` or missing `.ctex` resources, this worktree
has not been imported yet. Run `"$GODOT_BIN" --headless --path . --import` once
(slow — ~2000 PNGs) and retry. Those errors mean "not imported", not "broken".

Watch stdout for `ContentValidator:` lines. That is the startup validation
report, and it is the only place spell/character resource problems surface.
Report anything it prints, plus any Godot errors or push_warnings.
