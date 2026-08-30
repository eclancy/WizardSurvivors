# Chest Spawn and Pickup Flow

Status: Proposed
Priority: High
Labels: gameplay, systems

## Summary
Implement the chest spawn, opening, and item reward flow during a run.

## Assigned Persona
godot-gameplay-engineer

## Problem
Chest rewards are a new gameplay system and need a clean integration point in the run flow. They should feel exciting without disrupting player movement or combat rhythm.

## Proposed Work
- Add chest spawn logic to stages or encounter flow.
- Decide spawn conditions, timing, and rarity weighting.
- Add chest interaction and item reward resolution.
- Ensure duplicate items and set progress are handled cleanly.
- Integrate reward feedback with the player state and run progression.

## Acceptance Criteria
- Chests appear in appropriate encounter or stage contexts without breaking flow.
- Opening a chest grants a valid item reward.
- Players receive clear reward feedback.
- Duplicate item handling and partial-set progress behave predictably.
- The flow is testable and can be tuned by stage difficulty.

## Dependencies
- Requires the data model and item registry to exist.
- Should be paired with balance tuning after spawn logic is working.
