# Map Overhaul: Static Elements at Fixed Coordinates

Status: Proposed
Priority: Medium
Type: Feature

## Summary
Prepare for a larger map overhaul by introducing a system for static map elements that always appear at the same coordinates on a given map. This should support environmental features such as rocks, trees, ruins, pillars, bridges, and other landmarks that are part of the map layout rather than random encounter content.

## Goals
- Allow each map to define static elements with deterministic positions.
- Make fixed-location objects reusable across runs without random drift.
- Support a clear split between static layout props and dynamic spawned content.
- Keep the system flexible enough to support future map authoring and content-pipeline work.

## Proposed Scope
- Define a map-level data structure for static elements.
- Support fixed coordinates (and optionally rotation/scale) for each element.
- Ensure these elements load consistently when a map is entered.
- Preserve current gameplay flow while making future map authoring easier.

## Acceptance Criteria
- A map can declare one or more static elements.
- Those elements appear at the same coordinates every time the map is loaded.
- Static elements can coexist with existing randomly generated or encounter-based content.
- The implementation is documented clearly enough for future content work.

## Open Questions
- Should static elements be authored as scene instances, data resources, or a lightweight map-definition format?
- Should they support collision, visual layering, and interactions from the start?
- How should this interact with future procedural map generation or biome variation?

## Notes
This is a foundation issue for the larger map-overhaul effort and should be treated as a content-architecture and authoring improvement rather than a one-off map tweak.
