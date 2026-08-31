---
name: gameplay-testing
description: Validating Wizard Survivors gameplay changes — minimal repro steps, manual playtest plans, regression checklists, and balance passes over damage/HP/cooldown/XP numbers. Use after a gameplay change lands, when a bug report needs a repro, or before touching the damage pipeline, spell timers, or element thresholds.
---

# Gameplay Testing and Balance Workflow

Use this skill when a gameplay change needs structured validation beyond "it seems to work."

## Focus areas
- Combat correctness: damage, mitigation, status effects, enemy death, XP drops.
- Timing correctness: cooldowns, pulses, durations, level-up flow, spawn pacing.
- State correctness: unlocks, saves, rerolls, stage/character selection, game-over flow.
- Feel and balance: threat readability, upgrade value, wave pressure, survivability, and reward pacing.

## Test workflow
1. Define the exact scenario and what should happen.
2. List the smallest repro or playtest setup that exercises the changed behavior.
3. Record what to observe in gameplay, UI, and persistent state.
4. Add nearby regression checks for systems that commonly couple to the change.
5. Separate hard failures from tuning observations.

## Common regression checklist
- Does the player damage pipeline still respect passive reactions, shields, flat reduction, then HP?
- Do active spells still fire on the intended cadence after upgrades or cooldown modifiers?
- Do passive spells trigger at the correct event timing?
- Are enemies still in the `"enemies"` group and hittable by the relevant areas?
- Did a collision-layer change break detection because a `collision_mask` was missed?
- Do element threshold bonuses update correctly at 2, 4, and 6 instances?
- Does save data still preserve unlock and upgrade progress after the scenario?

## Output template
- Scenario
- Setup
- Steps
- Expected result
- Observed result
- Likely subsystem
- Regression follow-ups

## Examples

### Example: validating a new active spell
- Setup a run where the spell can be equipped quickly.
- Observe projectile spawn cadence, enemy hit registration, AoE or pierce behavior, and level-up scaling.
- Compare behavior before and after cooldown or amount upgrades.

### Example: debugging a mitigation issue
- Force a scenario where the player takes repeatable enemy hits.
- Track damage before passives, shield consumption, flat reduction, and final HP loss.
- Verify the issue is not caused by a second overlapping hurt source or an outdated UI readout.

## Learnings
- Many gameplay regressions only appear when a feature is exercised through the full run loop, not in isolated code reading.
- For this project, collision setup and signal order are as important to test as raw damage numbers.
