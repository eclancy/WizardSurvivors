# Asset-Driven Chest Item Concepts and Synergies

Status: Proposed
Priority: High
Labels: gameplay, visuals, systems, balance

## Summary
Create an art-informed MVP roster for the chest item feature using the existing asset library already checked into the project. The goal is to keep momentum moving without waiting for a brand-new art package.

## Problem
We have a large existing library of chest, key, flask, shield, lightning, and explosion assets already in the repo. Rather than leaving the feature in a generic state, we should use those assets to define a concrete first-pass item roster and synergy set plan.

## Proposed Work
- Review the existing asset families in `assets/organized/effects`
- Map those assets to chest item identities and roles
- Define 4–6 first-pass item concepts
- Define 3–4 set bonuses using those items
- Coordinate the item identities with the relevant gameplay and UI work

## Asset families available
- chest open and chest idle animations
- key / treasure / coin / flask / shield / lightning / fireball / explosion / spikes effects

## Proposed MVP roster
- Relic Key
- Aegis Sigil
- Ember Flask
- Storm Lattice
- Inferno Core
- Iron Fang

## Proposed set bonuses
- Vaultguard
- Emberline
- Stormbound
- Bastion of Spikes

## Assigned Personas
- two-d-art-director: choose the best existing art families and confirm readability and fit
- godot-gameplay-engineer: map the item concepts to runtime logic and effect hooks
- ui-ux-polish: define names, descriptions, and progress tracking for the item and set UI
- level-design-wave-balance: tune rarity and chest pacing around these items
- gameplay-qa-and-balance: validate that the early roster is fair and exciting

## Acceptance Criteria
- At least 4 items and 3 set bonuses are defined using existing asset families.
- Every item has a clear fantasy identity and tie to the game’s visual language.
- The set roster is specific enough to feed later implementation without reworking the concept again.
- The art direction is compatible with existing project assets and performance constraints.

## Dependencies
- Requires the feature-level chest item prompt to be approved.
- Feeds directly into implementation work on the item data model and UI pass.
