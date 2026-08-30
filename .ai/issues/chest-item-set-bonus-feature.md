# Chest Item Set Bonus Feature

Status: Proposed
Priority: High
Labels: gameplay, systems, ui, visuals, balance

## Summary
Add a new chest-based item system inspired by Wizard Survival / Magic Survival synergy mechanics. Items are discovered during a run, each with a unique effect, and specific item combinations unlock a powerful set bonus. This feature should improve build diversity, reward experimentation, and fit the existing Wizard Survivors run loop.

## Problem
The current game loop supports spells, passives, and progression, but it does not yet create a stronger "chest rewards + identity-based set bonus" loop. The feature should give players a reason to hunt, compare, and complete curated combinations while keeping the run readable and accessible.

## Proposed Work
- Define the item and set data model
- Add chest spawning and item pickup flow
- Implement set completion detection and synergy application
- Add UI for item discovery and set tracking
- Provide art and VFX direction for chests and synergy activation
- Tune chest frequency, item rarity, and set balance
- Validate gameplay with QA and balance passes

## Planned Persona Assignments
- godot-gameplay-engineer: build the data model, chest logic, pickup flow, and synergy application
- level-design-wave-balance: tune drop frequency and viability across stages and difficulties
- ui-ux-polish: create readable item and synergy interfaces
- two-d-art-director: direct chest visuals, item icon language, and synergy VFX
- gameplay-qa-and-balance: test edge cases and provide balance feedback

## Acceptance Criteria
- The feature has a clear design document and MVP scope.
- Chest rewards grant unique, readable items that fit the Wizard Survivors fantasy.
- Completing a set triggers a visible and meaningful bonus effect.
- Players can track discovered sets and missing components.
- Item and synergy balance is tuned for early and late game play.
- The feature supports future expansion without requiring major refactoring.

## Notes
This issue is the umbrella plan for the feature and should be used as the coordination point for the linked implementation issues.
