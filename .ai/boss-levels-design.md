# Boss levels

A run can now end in victory, not only in death. At `Node2DGame.TimerVictorySeconds` (15:00) the
survival clock stops and the stage's boss arrives; killing it clears the level, pays a currency
bonus, unlocks the next stage, and shows the victory screen. Related GitHub issues: #8 (Base Boss
Class & State Machine) and #12 (Boss Rush Gauntlet Mode, still open).

## Decisions

**First boss (Enchanted Forest) — Elderbark, the Treant: heavy tank, slow melee.** Enormous HP,
slow but relentless, heavy contact damage and a telegraphed ground slam. It rewards sustained DPS
and survivability, and punishes glass-cannon burst builds that cannot hold the line. Two
alternatives were considered and set aside: a Broodmother that continuously summons adds, and a
Mirage Stag whose decoys break auto-targeting. The stag was rejected *for the first boss only*,
because decoys mean touching the targeting code every spell shares — it remains a good candidate
for a later stage.

**Clearing a boss gates the next stage.** Cursed Castle is locked until the Forest boss falls.
`StageSelection` reads `SaveData.UnlockedStageIds`, which nothing wrote to before this. The
accepted cost is that half the current stage roster is closed on a fresh save.

**Normal spawning stops when the boss appears.** The timer freezes, regular waves (elites and the
Soldier miniboss included) stop, and chests stop dropping; enemies already alive are left to be
cleared. The arena drains so the fight reads as a duel rather than the usual swarm with a large
enemy inside it.

## How it is wired

- `BossCatalog` (`scripts/BossCatalog.cs`) maps a stage index to its boss: id, display name,
  banner tagline, scene, health, currency bonus, and the stage the win unlocks. **A stage with no
  entry still ends on the timer exactly as before**, so bosses can be added one stage at a time.
- `BossEnemy` (`scripts/BossEnemy.cs`) subclasses `Enemy`. It adds the identity the run result
  records, the telegraphed slam, a single enrage threshold at a third health, and the
  `BossDefeated` signal the run listens for. Chasing, damage, status and death stay in `Enemy`.
- `Enemy` gained three exports that default to the old behaviour: `ContactDamage` (read by the
  player's overlap loop, 1 for everything else), `KnockbackResistance` and `MinSlowMultiplier`, so
  a boss cannot be shoved around the arena or parked by a freeze build for the whole fight.
- `BossId` flows into `RunResult`, where `AchievementDefinitions` already matched on it —
  `forest_cleared` fires on any victory whose boss id contains "forest". **A new boss id must
  contain its stage's keyword** ("forest", "castle", "ruins") or the win silently grants no spell;
  `RegressionChecks.ValidateBossCatalog` fails the build's startup validation if it does not.

## Known tuning, unverified

Elderbark's 4200 HP is a first guess calibrated against a 15-minute elite (~860 HP). It has not
been playtested against a real endgame loadout — expect to move it. `ContactDamage = 3` against a
0.2s touch cooldown is 15 damage per second, which is meant to make standing in the boss a hard
mistake; that number is the other one most likely to need a pass.

Press **F4** in a run to skip straight to the boss instead of playing fifteen minutes to reach it.
