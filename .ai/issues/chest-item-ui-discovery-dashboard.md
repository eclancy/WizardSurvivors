# Chest Item UI and Synergy Discovery Dashboard

Status: Proposed
Priority: Medium
Labels: ui, gameplay

## Summary
Create a readable UI for discovered items, set completion progress, and active synergy states.

## Assigned Persona
ui-ux-polish

## Problem
Set-based item systems are rewarding only if players understand what they have, what they are missing, and what changed when a set completed. Without a strong UI, the feature will feel opaque.

## Proposed Work
- Build a synergy or artifact book screen.
- Show discovered sets and current progress toward completion.
- Show item names, descriptions, and clear state indicators.
- Add run-time notifications when a set is completed.
- Keep the UI readable on small mobile screens.

## Acceptance Criteria
- The player can tell what items they own and what set components are missing.
- A completed set is visible immediately and clearly.
- The interface supports future sets without major redesign.
- The UI remains readable during high-intensity combat.

## Dependencies
- Depends on the item and set registry being defined.
- Should be reviewed against the art direction and final synergy wording.
