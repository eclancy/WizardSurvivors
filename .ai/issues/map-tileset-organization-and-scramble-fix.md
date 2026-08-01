# Map Tile Organization and Scramble Fix

Status: Proposed
Priority: Medium
Labels: map-content, tiles, environment-art, level-design

## Summary
Address the current map issue where tiles appear randomly scrambled and mixed across environments. The goal is to make each map use a consistent, organized set of tiles that clearly belong to the same environment and do not accidentally mingle different biomes or themes.

## Problem
- Some maps currently feel visually inconsistent because tiles from different environments are being mixed together.
- Random tile scrambling makes the level layout feel less intentional and less readable.
- It is difficult to author or maintain maps when tiles are not clearly grouped by environment.

## Goals
- Fix maps that currently have scrambled or mixed tiles.
- Organize tiles into clear environment categories (for example: forest, ruins, castle, snow, desert, etc.).
- Prevent accidental mixing of different environment tiles in future map work.
- Create a more reliable foundation for future map content and art pipeline work.

## Proposed Scope
- Audit current maps for tile placement issues and visual mismatches.
- Group tiles by environment/theme so they can be used consistently.
- Rebuild or fix affected maps so their tile sets are coherent.
- Document which environment groups should be used for each map.

## Acceptance Criteria
- Each map uses tiles that belong to a single coherent environment style.
- No map contains visually scrambled or mismatched tile mixtures from unrelated environments.
- The tile organization is clear enough for future content authors to follow.
- The fix is documented in a way that can be reused for later map expansion.

## Notes
This should be treated as both a visual polish issue and a content-authoring/level-design cleanup task.
