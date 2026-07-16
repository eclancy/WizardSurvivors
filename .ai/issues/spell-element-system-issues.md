# Spell Element System Issue Drafts

This backlog splits the requested direction change into GitHub-sized issues.

## 1. Define spell elements and attach them to every spell
- Goal: replace combo-based evolution thinking with a per-spell element model.
- Scope:
  - Add one or two element tags to each spell.
  - Decide how multi-element spells are represented in `SpellData`.
  - Establish the canonical element list and naming.
- Acceptance criteria:
  - Every spell resource has at least one element.
  - The project can query a spell's elements at runtime.
  - Design docs list the canonical elements and their intended effects.

## 2. Implement elemental threshold effects
- Goal: make element counts matter across the owned spell list.
- Scope:
  - Count owned instances of each element.
  - Apply special effects at 2, 4, and 6 instances.
  - Support scaling versions of the same effect at higher thresholds.
- Acceptance criteria:
  - The game can compute active element bonuses from the current spell inventory.
  - Threshold bonuses update immediately when a spell is added, leveled, or removed.
  - The system supports effect definitions like fire, ice, darkness, light, grass, earth, wind, and pure arcane.

## 3. Show element counts in gameplay HUD
- Goal: display the number of instances for each element next to the spell list in the upper-left HUD.
- Scope:
  - Add a HUD readout for current element counts.
  - Keep the display updated as the spell list changes.
  - Place the readout near the existing upper-left item area.
- Acceptance criteria:
  - Players can see each active element count during gameplay.
  - The display reflects equipped spell changes without reopening menus.

## 4. Preview element effects in the level-up menu
- Goal: show how a selectable spell changes element counts and what those elements do.
- Scope:
  - Under each selectable spell, list the element effects it contributes.
  - Show the projected total count for each affected element if the spell is chosen.
  - Keep the preview readable when a spell has two elements.
- Acceptance criteria:
  - Every level-up option shows element-related effects underneath the spell choice.
  - The preview reflects the player's current spell list plus the hovered or selected option.

## 5. Allow level-up choices when spell slots are full
- Goal: remove the current hard stop on new spell selection when the build is full.
- Scope:
  - Permit choosing a new spell even if all slots are occupied.
  - Add a way to remove one owned spell as part of the same level-up flow.
  - Add a skip option for players who want no change.
- Acceptance criteria:
  - A full spell list never blocks level-up progression.
  - The level-up UI supports add, replace or remove, and skip flows.
  - Removing a spell updates all dependent systems cleanly.

## 6. Remove spell evolution and combination mechanics
- Goal: retire combination-based spell evolution in favor of the element threshold system.
- Scope:
  - Remove or disable combo checks that fuse spells into evolutions.
  - Update UI text and docs that mention evolutions or combination recipes.
  - Ensure no gameplay path still depends on evolution pairings.
- Acceptance criteria:
  - The game no longer offers or performs spell evolutions based on combinations.
  - Any references to evolution combos are removed or replaced with element-synergy language.

## 7. Add element-specific effect implementations
- Goal: implement the example elemental bonuses as gameplay features.
- Scope:
  - Fire: nearby enemies take more damage.
  - Ice: enemies hit are slowed.
  - Pure arcane: gain more experience.
  - Darkness: enemies deal less damage.
  - Light: heal a percentage of damage dealt.
  - Grass: regenerate over time.
  - Earth: increase max health and show the bonus near the health bar.
  - Wind: move spell is faster.
- Acceptance criteria:
  - Each element has a concrete effect definition.
  - Threshold scaling at 4 and 6 instances strengthens the same effect.
  - Earth bonus is visible in the HUD near the HP bar.

## 8. Update design and balance notes for the new system
- Goal: keep the repo docs aligned with the new direction.
- Scope:
  - Replace synergy and evolution language with elemental threshold language.
  - Record the intended balancing rules for 2, 4, and 6 thresholds.
  - Document any open questions before implementation starts.
- Acceptance criteria:
  - Gameplay design docs describe the element system instead of combo evolution.
  - Roadmap and architecture notes are consistent with the new spell model.
