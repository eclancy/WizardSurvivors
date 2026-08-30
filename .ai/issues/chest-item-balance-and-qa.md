# Chest Item Balance and QA Playtest Pass

Status: Proposed
Priority: Medium
Labels: gameplay, balance, qa

## Summary
Tune drop rates, set viability, and item power across the run while validating the new system with structured playtesting.

## Assigned Persona
gameplay-qa-and-balance

## Problem
Synergy systems introduce strong build identity, but they can also become either too rare or too dominant if not tuned carefully. The feature must remain fair and readable across early and late game.

## Proposed Work
- Define balance goals for chest frequency and item rarity.
- Review set power levels and their impact on run pacing.
- Test edge cases such as duplicate items, partial sets, and chest interaction timing.
- Produce a regression checklist and manual playtest notes.

## Acceptance Criteria
- Chest frequency supports gameplay without overwhelming the player.
- Sets feel powerful but fair, with no obvious dead-set or mandatory-set problem.
- Edge cases are understood and do not create broken states.
- The feature passes manual QA and can be iterated safely.

## Dependencies
- Requires functional chest pickup logic and a near-final item/set registry.
- Should happen after the core implementation is stable.
