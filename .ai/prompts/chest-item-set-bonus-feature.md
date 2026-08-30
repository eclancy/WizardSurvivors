# Chest Item Sets and Synergy Bonuses

Status: Draft
Priority: High

## Feature Prompt

Design and plan a new Wizard Survivors game feature: "Chest Items & Set Bonuses."

### Summary
Create a chest-based item system where the player finds rare, unique passive items during a run. Each item grants a memorable, identity-rich effect. When the player collects a specific combination of items, a new synergy bonus activates that exceeds the value of the individual items alone. This should feel like a strong Wizard Survival-inspired progression layer without requiring a full rewrite of the existing spell system.

### Core gameplay loop
- Chests occasionally appear during a run and can be opened for a random item reward.
- Each item is a permanent-for-the-run pickup with a unique passive or trigger-based ability.
- Items have clear identity and readability so players understand what they do immediately.
- Completing a set unlocks an additional bonus effect, encouraging build planning and experimentation.
- Some synergies should reward offense, some defense, some utility, and some map/control play.

### Design goals
- Encourage build planning and set hunting without making the game feel like a spreadsheet.
- Make chest rewards exciting while keeping the run readable and fast to understand.
- Support partial discovery: getting one piece of a set should reveal information about the rest.
- Keep item and set effects understandable on mobile while preserving depth.
- Make the system tunable by stage and difficulty.

### Non-goals
- This is not a permanent meta progression system for individual item unlocks.
- This is not a full replacement for existing spells or passive spell effects.
- This is not a huge combinatorics system of 100+ random items for the MVP.
- This is not a full shop economy or chest rarity overhaul beyond the feature scope.

### Suggested MVP scope
- 2-4 item sets for the initial pass
- 8-15 unique chest items across the first wave of designs
- 1 synergy UI tracking panel
- Chest spawn tuning and rarity balancing
- At least one visual language for chest reward feedback and synergy activation

### Implementation considerations
- Prefer a data-driven item model that can be extended with future item definitions.
- Keep item effects in the same general architecture used by the existing passive systems where possible.
- Use a synergy registry or set data file so new combinations can be added without invasive code edits.
- Make completion checks explicit and easy to debug.
- Ensure sets can be discovered and tracked without requiring the player to memorize everything.

### Persona-based delivery plan
- godot-gameplay-engineer: data model, chest pickup flow, item application, set detection, gameplay logic
- level-design-wave-balance: chest spawn tuning, rarity, synergy difficulty, run pacing
- ui-ux-polish: synergy book, pickup readability, notifications, inventory clarity
- two-d-art-director: chest art, item icons, VFX, visual hierarchy
- gameplay-qa-and-balance: manual playtests, edge cases, balance reviews, regression checklist

### Research signal
The closest genre precedent is the Magic Survival Synergy page, which shows a dedicated synergy book, clue-based discovery, and 3-4 part set completion as a core feature. This is a strong fit for Wizard Survivors and should be treated as a core gameplay pillar rather than a cosmetic extra.
