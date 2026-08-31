---
name: ui-ux-polish
description: Refining Wizard Survivors HUD clarity, menu flow, upgrade and level-up card presentation, reroll/swap/remove flows, input feedback, and accessibility. Use for work on LevelUpMenu, ChestItemSelectionMenu, MainMenu, CharacterSelection, StageSelection, or the in-run HUD.
---

# UI/UX Polish for Wizard Survivors

Use this skill when the game is mechanically complete but the interface still feels unclear, crowded, or awkward.

## Focus areas
- HUD readability during combat: health, XP, cooldown states, element thresholds, and danger signals.
- Level-up and progression screens: card clarity, reward comparison, reroll/remove affordances, and decision speed.
- Menu flow: title, main menu, character/stage selection, pause, and game-over transitions.
- Feedback and affordances: hover states, selection clarity, button labels, and input responsiveness.

## Polish checklist
1. Identify the exact interaction or screen that is causing friction.
2. Decide whether the problem is hierarchy, spacing, language, contrast, timing, or flow.
3. Choose the smallest change that improves comprehension without adding noise.
4. Verify the solution still works during high-pressure combat or fast menu interactions.
5. Keep the interface readable even when the screen is busy.

## Rules of thumb
- Prioritize important information with strong hierarchy and limited visual noise.
- Keep labels short, visible, and action-oriented.
- Make the next action obvious without forcing the player to infer hidden rules.
- Preserve readability over decorative polish when the two compete.
- Prefer consistency across menus and HUD so the player learns the interface quickly.

## Examples

### Example: level-up menu polish
- Make the selected upgrade card feel clearly highlighted.
- Keep the reward summary and cost/impact information aligned and easy to compare.
- Ensure reroll and remove actions are obvious without crowding the card area.

### Example: HUD polish
- Clarify which stat or effect is changing when a reward is applied.
- Keep combat-critical information visible without overwhelming the play area.
- Use animation sparingly so the player can still track the action on screen.

## Deliverable formats
- **UX pass**: a list of interaction problems and proposed improvements.
- **UI polish pass**: spacing, hierarchy, labels, contrast, icon, and feedback changes.
- **Playtest notes**: what should feel clearer or faster during real runs.

## Learnings
- In a fast autoshooter, the best UI is often simple enough that the player never has to think about it.
- A polished interface should reduce uncertainty, not add more things to interpret.
