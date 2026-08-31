---
name: level-design-wave-balance
description: Shaping stage pacing, enemy wave curves, spawn density and composition, difficulty ramps, elite/miniboss/boss timing, and encounter tuning for Wizard Survivors. Use when a stage feels flat, spiky, or too safe, when adding an enemy to the wave ecosystem, or when tuning early/mid/late run pacing.
---

# Level Design and Wave Balance

Use this skill when shaping how a Wizard Survivors run escalates: spawn density, enemy mix, elite timing, and stage-specific pacing.

## Where the spawn system lives

All of it is in `scripts/Node2DGame.cs`, the run controller:

- `UpdateSpawnScaling()` — the difficulty ramp over run time.
- `SpawnEnemy()` / `GetSpawnBatchCount()` — how many enemies arrive per tick.
- `FindSeparatedSpawnPosition()` / `GetRandomSpawnPositionAroundPlayer()` — placement and anti-clumping.
- `ShouldSpawnElite()` / `GetEliteIntervalSeconds()` / `GetCurrentEliteEnemyCount()` — elite cadence and cap.
- `SpawnChestReward()` — the reward beat.

Enemy behavior itself is in `scripts/Enemy.cs` and the `enemy.tscn` / `FastEnemy.tscn` / `TankEnemy.tscn` scenes. Read `.ai/gameplay-design.md` for the intended pacing before proposing numbers.

## Design priorities

- **Readability first.** Avoid spawn patterns that create unavoidable or unreadable pressure spikes.
- **Escalation with rhythm.** Alternate pressure, reward, and recovery instead of ramping linearly forever.
- **Enemy role clarity.** Wave composition should make it obvious why each enemy exists in the mix.
- **Performance safety.** Prefer changes that fit the existing spawn system over expensive new logic — this is a swarm game, and per-frame allocations in the spawn path are costly.

## Workflow

1. Inspect the relevant spawn methods, enemy definitions, and stage data together.
2. State the player-experience goal for the segment being tuned before changing a number.
3. Adjust density, composition, and timing around that goal — one lever at a time so the effect is attributable.
4. Check how the proposed curve interacts with player power growth: level-up cadence, `MaxSpellSlots` (6), and element tier thresholds from `.ai/gameplay-design.md`.
5. End with a playtest checklist and the most likely regression risks.

## Output

- A concise tuning recommendation, with the specific method and value to change.
- Risks to readability or fairness.
- Suggested playtest scenarios (which stage, which minute of the run, what to watch).
- Notes on how the change interacts with player power progression.
