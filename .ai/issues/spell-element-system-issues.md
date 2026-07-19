# Spell Element System Issue Drafts

This backlog splits the requested direction change into GitHub-sized issues.

## Current status
- Issues 1-8 are implemented in code/docs as of the current working tree.
- Follow-up UI/testing work is implemented: all current spells/passives can be offered for testing, level-up cards show upgrade stat deltas, element passive descriptions are centralized, the gameplay element banner uses colored element badges, the main menu has Spellbook/Achievements views, and the in-run Escape menu has Resume/Restart/Quit plus Run Details/Spellbook/Achievements views.
- Achievement definitions now drive achievement progress UI and spell unlock rewards; unlock balance is still open because the current default pool is intentionally fully available for testing.
- Automated validation completed: `dotnet build WizardSurvivors.csproj` succeeds, `godot --headless --quit --path .` loads the project, and `godot --headless --path . --quit-after 3 scenes/node_2d_game.tscn` starts the gameplay scene cleanly.
- Known non-blocking validation note: Godot headless load still reports an existing exit-time resource leak warning.
- Remaining before final sign-off: manual in-editor playtest of the level-up, element HUD, Escape menu, Spellbook, Achievements, and achievement unlock flows.

### Manual playtest checklist
- Reach a 6-spell loadout, then confirm new-spell choices can replace an owned spell.
- With a full loadout, confirm `Remove a Spell` removes an owned spell and updates the element HUD immediately.
- Confirm `Skip` closes the level-up menu without changing spells or element counts.
- Confirm rerolls still work when the menu starts from a scene-existing `LevelUpMenu` node.
- Confirm Earth tier bonuses show `+20 HP`, `+50 HP`, or `+100 HP` near the player HP bar and disappear when Earth drops below tier 2.
- Confirm Lightning tier bonuses visibly chain damage to a nearby second enemy at tiers 2, 4, and 6.
- Confirm the gameplay element banner shows only colored element badges with `Element count/threshold` text.
- Press Escape during a run and confirm Resume, Restart Run, Quit, Run Details, Spellbook Pool, and Achievements work while paused.
- Confirm main-menu Spellbook and Achievements panels render correctly and reflect save unlock/completion state.
- Complete a run achievement and confirm the achievement and configured spell unlock persist after returning to the main menu.

## 1. Define spell elements and attach them to every spell
- Status: Complete.
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
- Status: Complete.
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
- Status: Complete.
- Goal: display the number of instances for each element next to the spell list in the upper-left HUD.
- Scope:
  - Add a HUD readout for current element counts.
  - Keep the display updated as the spell list changes.
  - Place the readout near the existing upper-left item area.
- Acceptance criteria:
  - Players can see each active element count during gameplay.
  - The display reflects equipped spell changes without reopening menus.

## 4. Preview element effects in the level-up menu
- Status: Complete.
- Goal: show how a selectable spell changes element counts and what those elements do.
- Scope:
  - Under each selectable spell, list the element effects it contributes.
  - Show the projected total count for each affected element if the spell is chosen.
  - Keep the preview readable when a spell has two elements.
- Acceptance criteria:
  - Every level-up option shows element-related effects underneath the spell choice.
  - The preview reflects the player's current spell list plus the hovered or selected option.

## 5. Allow level-up choices when spell slots are full
- Status: Complete.
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
- Status: Complete.
- Goal: retire combination-based spell evolution in favor of the element threshold system.
- Scope:
  - Remove or disable combo checks that fuse spells into evolutions.
  - Update UI text and docs that mention evolutions or combination recipes.
  - Ensure no gameplay path still depends on evolution pairings.
- Acceptance criteria:
  - The game no longer offers or performs spell evolutions based on combinations.
  - Any references to evolution combos are removed or replaced with element-synergy language.

## 7. Add element-specific effect implementations
- Status: Complete.
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
- Status: Complete.
- Goal: keep the repo docs aligned with the new direction.
- Scope:
  - Replace synergy and evolution language with elemental threshold language.
  - Record the intended balancing rules for 2, 4, and 6 thresholds.
  - Document any open questions before implementation starts.
- Acceptance criteria:
  - Gameplay design docs describe the element system instead of combo evolution.
  - Roadmap and architecture notes are consistent with the new spell model.

## 9. Make the full spell pool available for testing
- Status: Complete.
- Goal: let playtests see every implemented weapon and passive without needing to grind unlock currency first.
- Scope:
  - Add every implemented active spell and code-only passive spell to the default level-up availability list.
  - Preserve the existing `UnlockedSpellIds` path so future progression tuning can re-lock items without changing level-up logic.
- Acceptance criteria:
  - All 20 active weapons can appear as level-up options.
  - All 11 passive spells can appear as level-up options.
  - Spellbook availability reflects the same pool.

## 10. Add run inspection and menu surfaces
- Status: Complete.
- Goal: expose the information needed to inspect a build during and between runs.
- Scope:
  - Add an in-run Escape menu with Resume, Restart Run, and Quit.
  - Add Escape menu views for current weapons/stats, equipped passives, character passive, element passives, spellbook pool, and achievement progress.
  - Add a main-menu Achievements panel alongside Spellbook, Arcane Upgrades, and Options.
- Acceptance criteria:
  - Escape pauses the run and shows navigable run details.
  - Restart Run reloads the gameplay scene; Quit returns to Main Menu.
  - Main-menu achievements use saved completion state.

## 11. Add achievement definitions and spell unlock rewards
- Status: Complete.
- Goal: make achievements data-driven enough to display in UI and grant unlock rewards from one source of truth.
- Scope:
  - Add shared achievement definitions with names, requirements, reward text, and optional `SpellUnlockId`.
  - Update run completion reward logic to iterate shared achievement definitions.
  - Seed achievements that unlock weapons/passives such as Fireball, Frost Shard, Chain Lightning, Solar Flare, Blur, Scorching Ray, Cone of Cold, Toxic Spore Burst, Obsidian Spike, Gale Blade, Thorn Vine, Shadow Bolt, Black Tentacles, and Meteor Swarm.
- Acceptance criteria:
  - Achievement menu and reward logic use the same definitions.
  - Completing a defined achievement persists both the achievement id and its spell unlock reward.

## 12. Show element passive descriptions consistently
- Status: Complete.
- Goal: make element threshold passives understandable anywhere they are shown.
- Scope:
  - Centralize player-facing element passive text in `ElementPassiveDescriptions`.
  - Use those descriptions in level-up element previews and the Escape menu's element passive section.
  - Keep the gameplay banner compact by showing only colored badges with element names and progress numbers.
- Acceptance criteria:
  - Level-up previews show the relevant element passive effect text.
  - Escape menu run details list active and upcoming element passive effects.
  - Gameplay HUD element banner is compact and color-coded.

## 13. Track spell/passive balance as a living issue
- Status: Open / Ongoing.
- Goal: keep future spell, passive, and element threshold tuning in one editable issue.
- Tracking issue: `spell-passive-balance.md`.
