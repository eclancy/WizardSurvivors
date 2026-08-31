---
description: Full validation pass — build, Godot startup validators, and allowlist drift
allowed-tools: Bash, Read, Grep, Glob
---
Run a full validation pass and report as four sections: BUILD / VALIDATOR /
ALLOWLIST DRIFT / NOT RUN.

1. **BUILD** — `dotnet build WizardSurvivors.sln -v q --nologo`. Report errors
   and warnings with file:line.

2. **VALIDATOR** — if the build succeeded, run
   `"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit` and report
   every `ContentValidator:` and `Regression:` line.
   - If `GODOT_BIN` is unset or missing, say so and continue to step 3 rather
     than stopping.
   - If you see `Unrecognized UID` or a wall of `Failed loading resource` /
     missing `.ctex` errors, the worktree has not been imported yet. Run
     `"$GODOT_BIN" --headless --path . --import` once (slow — ~2000 PNGs) and
     retry. Do not report those as validation failures.

3. **ALLOWLIST DRIFT** — this step needs no Godot and is the highest-value
   check. List every `SpellData*.tres` at the repo root, then read the
   hardcoded `SpellResourcePaths` array in `scripts/ContentValidator.cs`
   (around line 11). Report:
   - resources on disk that are NOT in the array (silently unvalidated), and
   - array entries with no file on disk (will fail to load at startup).
   The two sets should match exactly.

4. **NOT RUN** — state what you could not check. `validate_chest_system.py`
   cannot run on this machine (Python 2.7 only). There is no test suite.
