# Chest Item Data Model and Set Registry

Status: Proposed
Priority: High
Labels: gameplay, systems

## Summary
Define the data-driven structure for chest items and synergy sets so the feature can expand without special-case code paths.

## Assigned Persona
godot-gameplay-engineer

## Problem
The game already uses data-driven resources for spells, so chest items should follow a similar pattern. Without a clean data model, set logic becomes brittle and difficult to tune.

## Proposed Work
- Create a chest item data structure with fields like name, rarity, effect id, icon reference, and description.
- Add a synergy registry that defines the required item set and the resulting bonus.
- Ensure item effects are easy to query at runtime.
- Support future additions of new set definitions without breaking existing content.

## Acceptance Criteria
- Items can be defined in data rather than hardcoded logic for the MVP.
- Each synergy has explicit required components and a resulting effect.
- The system can detect partial and complete set states.
- Item and set definitions are easy to extend for future content.

## Dependencies
- Depends on the feature prompt and high-level design signoff.
- Must be completed before balance tuning and UI work can finalize exact values and labels.
