---
description: Add a new spell following the project's data-driven recipe
argument-hint: <spell name> [active|passive] [element(s)]
allowed-tools: Read, Grep, Glob, Edit, Write, Bash(dotnet build:*)
---
Add a new spell: $ARGUMENTS

Load the `godot-autoshooter-feature` skill first. Then:

1. Decide active vs passive. If the request is ambiguous, ask before writing code.

2. **ACTIVE** — read the reference triple first: `SpellData_Fireball.tres`,
   `scripts/MagicMissile.cs`, `scenes/MagicMissile.tscn`. Then create:
   - `SpellData_<Name>.tres` at the **repo root** (that is where they all live)
   - `scenes/<Name>.tscn` and `scripts/<Name>.cs`
   - firing wired into `Player`'s existing `spellFireTimers` loop — never a
     parallel timer system
   - **MANDATORY:** add `"res://SpellData_<Name>.tres"` to `SpellResourcePaths`
     in `scripts/ContentValidator.cs`. Skipping this silently exempts the new
     resource from all validation.

3. **PASSIVE** — read `scripts/PassiveSpellEffect.cs` and one concrete subclass.
   Code-only, no `.tscn`. Override `OnPlayerDamaged(int)` for reactive effects
   or set `UsesPulseTimer = true` + `OnPulseTick()` for auras. Register it
   alongside the existing ten via `CreateDefensiveSpellData(...)` in
   `scripts/Player.cs` (around line 408).
   Check the effect against the `TakeDamage` ordering in CLAUDE.md — note the
   `DamageTaken` signal fires on the RAW pre-mitigation amount, and a dodge
   skips the signal entirely.

4. Set element weights per `.ai/gameplay-design.md`. Never touch `Weapon.cs`.

5. Run `dotnet build`, then list the manual in-Godot checks that remain —
   the build cannot verify the scene or resource wiring.
