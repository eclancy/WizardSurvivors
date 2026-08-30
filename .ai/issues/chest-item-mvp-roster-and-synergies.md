# Design: Chest Item MVP Roster and Synergies

Status: Proposed
Priority: High
Labels: gameplay, systems, balance, visuals

## Summary
Define the first-pass roster for the chest item feature using the existing art assets already in the project. This design pass is the concrete implementation guide for the feature and should feed the data model and chest-system work.

## Problem
The feature has broad direction, but it needs a concrete item list, exact synergy identities, and numbers that can be implemented without causing constant redesign.

## Proposed Work
- Finalize the item roster around the asset families already in the project
- Define 3–4 synergies with distinct gameplay identities
- Write effect language and readability notes for each item
- Provide rough balance values and tuning goals
- Confirm that the plan still fits the current Wizard Survivors systems

## Item roster
- Relic Key
- Aegis Sigil
- Ember Flask
- Storm Lattice
- Inferno Core
- Iron Fang

## Set bonuses
- Vaultguard
- Emberline
- Stormbound
- Bastion of Spikes

## Assigned Personas
- godot-gameplay-engineer: translate the design into runtime systems and item data models
- level-design-wave-balance: tune chest frequency and set viability around the approved numbers
- ui-ux-polish: define labels and progression tracking for discovered items and sets
- two-d-art-director: confirm visual fit against the current asset families
- gameplay-qa-and-balance: test pacing and edge cases against the initial numbers

## Acceptance Criteria
- At least 6 item concepts are finalized.
- At least 3 set bonuses are defined with clear names and effect identity.
- All item concepts map cleanly to available project art assets.
- The item set logic is specific enough to support implementation without broad redesign.
- The version is balanced as an MVP and can be tuned later.

## Dependencies
- Depends on the earlier chest item feature prompt and art-driven concept pass.
- Feeds directly into the item data model and set registry task.
