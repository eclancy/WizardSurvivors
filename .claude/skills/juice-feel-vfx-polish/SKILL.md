---
name: juice-feel-vfx-polish
description: Tuning Wizard Survivors game feel — hit stop, damage flash, camera shake, particles, trails, death pops, screen feedback, and the tradeoff between juice and readability in a swarm. Use when a hit feels weak, an effect is noisy, or a spell needs impact.
---

# Juice, Feel, and VFX Polish

Use this skill when gameplay is correct but the moment-to-moment experience needs more punch, clarity, or satisfaction.

## Scope
- Combat impact: hit stop, flash, shake, trails, bursts, and death pops.
- Feedback clarity: damage taken, shield loss, cooldown readiness, XP pickup, level-up reveal.
- Presentation polish: particles, motion accents, sound/VFX pairing, subtle screen-space emphasis.
- Readability protection: improve feel without hiding threats or important UI.

## Polish checklist
1. Identify the player action or event that needs stronger feedback.
2. Decide whether the best improvement is timing, motion, color, particles, sound, or camera.
3. Keep the strongest feedback on high-value moments only.
4. Verify the effect does not obscure enemy silhouettes, pickups, or HUD.
5. Prefer short, readable accents over constant heavy effects.

## Rules of thumb
- Use tiny feedback for common events and bigger juice for rare or high-stakes moments.
- Pair bright effects with meaningful gameplay events, not every frame of movement.
- Keep VFX short enough that dense combat stays legible.
- If a polish idea competes with readability, reduce intensity before adding more layers.
- Favor reusable effect language so spells and UI feel like the same game.

## Examples

### Example: new spell impact
- Add a brief hit flash, small radial burst, and a short trail fade.
- Keep the effect centered on the target and capped so it does not cover nearby enemies.

### Example: player damage feedback
- Use a sharp sound, quick flash, and subtle camera nudge.
- If the player can be hit repeatedly, keep the effect short enough to avoid visual fatigue.

## Deliverable formats
- **Feel pass**: a list of moment-to-moment improvements prioritized by impact.
- **VFX polish pass**: effect-by-effect guidance with intensity and duration notes.
- **Readability review**: what to tone down so the game still reads in swarm combat.

## Learnings
- In a top-down autoshooter, more particles are not automatically better; timing and contrast matter more.
- The best juice usually reinforces an action the player already understands instead of trying to explain the system on its own.
