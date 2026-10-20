# Run pacing: what decides WHEN things happen in a run

The single reference for every number that places something in time - spawns, enemy strength,
elites, chests, the boss, level-ups and the two level gates - and a recipe for changing the length
of a run without re-tuning all of it by hand. The design targets themselves (when the Ascension and
the Apotheosis should land, and why) live in `.ai/gameplay-design.md`, "The Apotheosis"; this doc
says how the code gets there and how to move it.

**The one thing to take away.** A run runs on two clocks, and only one of them follows the run
length:

- **The pace clock** places the *world*: which enemies exist, how many arrive, how tough they
  are, when elites, the Warden, chests and the side event come. It scales with
  `TimerVictorySeconds` automatically.
- **The player's level** places the *build*: the Ascension (level 17) and the Apotheosis
  (level 23). Level comes from XP, XP comes from kills, and the kill rate in the middle and late run
  is pinned by a cap that is in **real seconds** (the spawn burst window). So changing the run
  length moves the world but **does not move when the player reaches level 17 or 23**. Change
  `TimerVictorySeconds` alone and the gates stay where they are in real minutes, which on a shorter
  run means late or never.

Section 5 is the recipe; sections 3 and 4 are why it works.

**Current setting (2026-10-16): a 15-minute run, strategy B** - `TimerVictorySeconds` 900
(`RunPace` 1), `XpCostPerLevel` 27, gates unchanged at 17 / 23 (20 minutes with 35 was tried for
part of a day first). Applied from the worked example in section 5 and
not yet measured; the measured tables below are the 30-minute run they were predicted from. Current
values quoted elsewhere in this doc (1800, 55, `RunPace` 2) are that run's.

## 1. The three clocks

| clock | what it is | read through | moves with run length? |
|---|---|---|---|
| real seconds | `Node2DGame.timeElapsed`, stops when the boss arrives (`_Process`, `Node2DGame.cs:1945`) | `timeElapsed`, `RunSeconds` | no - it *is* the run length |
| pace clock | real time mapped onto the 15-minute reference run | `PaceSeconds` / `PaceMinutes` | yes, by construction |
| player level | XP collected against `CalculateXPForLevel` | `Player.CurrentLevel` | **no** - it follows the kill rate (section 3) |

**The pace clock, exactly** (`scripts/Node2DGame.cs:36-51`):

```
TimerVictorySeconds = 1800                     // when the clock stops and the boss walks in
ReferenceRunSeconds = 900   (const)            // the run every pacing number was tuned on
RunPace      = TimerVictorySeconds / 900       // 2.0 today; 1 if TimerVictorySeconds <= 0
PaceSeconds  = timeElapsed / RunPace
PaceMinutes  = max(0, PaceSeconds / 60)
```

So on today's run, "the Lunger arrives at 90 s" means real 3:00, and every pace number below
should be read as "doubled, in real time". A pacing number keeps its meaning as a *fraction* of the
run whatever `TimerVictorySeconds` becomes. Three idioms carry it, and new code must use one:

- compare a threshold against `PaceSeconds` / `PaceMinutes` (enemy introductions, curves);
- advance a timer by `delta / RunPace` (chests `:1964`, Warden `:3707`);
- multiply a first-occurrence time by `RunPace` (first formation, `:3634`).

What is deliberately left in real seconds is what describes how a moment *feels* - a burst, a
grace, a rhythm, a tick - because a longer run should not make a wave arrive in slow motion.

## 2. Every timing knob, classified

**P** = on the pace clock (scales with run length automatically). **R** = real seconds on purpose
(a feel timing). **F** = fixed, independent of time (a count, a cap, a multiplier). Values are
today's; "real on 30 min" is the pace value times `RunPace` = 2.

### Spawning (`scripts/Node2DGame.cs`)

| knob | line | value | class | notes |
|---|---|---|---|---|
| `TimerVictorySeconds` | 36 | 1800 | - | the run length; the only number a length change *must* touch |
| `ReferenceRunSeconds` | 43 | 900 | const | never change it - every P number is a position on this run |
| `MaxEnemies` x `WideViewEnemyCapScale` | 15, 21, 476 | 70 x 2.0 = **140** | F | the trickle stops at 140 - 9 = 131 (`trickleCap`, :2007) to leave room for formations |
| `MaxSpawnsPerBurstWindow` x scale | 143, 477 | 35 x 2.0 = **70** | F | per `SpawnBurstWindowSeconds` - **the XP ceiling**, see 3.3 |
| `SpawnBurstWindowSeconds` | 142 | 10 s | **R** | so the ceiling is 7 spawns per *real* second on any run length |
| `SpawnOpeningInterval` -> steady | 102-103 | 1.15 s eased over 2.5 pace min | P (time) / R (gap) | `UpdateSpawnScaling` :3455 |
| `SpawnBaseInterval` - `ReductionPerMinute` x m | 95, 97 | 0.75 - 0.05 m | P / R | the *gap* is real seconds, read at pace minute m |
| `SpawnMinInterval` | 96 | 0.18 s | R | floor reached at pace min 11.4 (real 22.8) |
| batch size per spawn tick | `GetSpawnBatchCount` :4085 | 1-2 (<1 min), 2-3 (<3), 3-4 (<7), **1-3 (<11)**, 2-4 after | P | the 7-11 dip is visible in the XP data (3.2) |
| `PostLevelUpSpawnGraceSeconds` | 134 | 1.2 s | R | no trickle or formations just after a level-up |
| `FormationFirstSeconds` | 90 | 75 pace s | P | multiplied by `RunPace` at :3634 |
| `FormationIntervalSeconds` | 87 | 15 s | R | the formation rhythm; 5-9 members, inside the burst budget |
| formation shapes | `SpawnFormations.Pick` (`SpawnFormations.cs:55`) | arcs/columns < 3, + more < 7, pincers after | P | |
| spawn ring, forward bias | 52-80 | screen edge x 1.08 + 330, bias 1.8 | F | changes kill rate, so XP - see 6 |

### Enemy strength and cast

| knob | line | value | class | notes |
|---|---|---|---|---|
| spawn health | 104-111, :3473 | 6 + 10/pace-min, eased over first 4 pace min | P | 156 at the end of the run; times the type's multiplier |
| horde speed | 124-126, :3426 | 0.60 -> 0.85 over 5 pace min | P | reaches full speed at real 10:00 |
| enemy contact damage | per scene | - | F | **not scaled by time at all** |
| Boo (2.5% of spawns) | `SelectEnemyForCurrentStage` | first 5 pace min, **Sketchbook only** since 2026-10-20 | P | drops 50 XP - see section 7 |
| Lunger / Hexer / Exploder / Slammer / Sentry / Summoner first spawn | 186, 165, 188, 190, 173, 192 | 90 / 105 / 165 / 210 / 210 / 285 pace s | P | shares 0.10 / 0.08 / 0.07 / 0.08 / 0.04 / 0.04 |
| default-table mix change | :4345 | at 5 pace min | P | only stages with no environment table |
| elites start | `EliteStartTimeSeconds` 127 | 135 pace s | P | |
| elite interval | `GetEliteIntervalSeconds` :4366 | 54 / 44 / 36 / 30 pace s at <5 / 5 / 8 / 12 pace min | P | x preset scale |
| elite power, cap | 128-131 | x2.45 HP, x1.12 speed, max 2 alive | F | |
| Warden first / interval | 160-161 | 150 / 165 pace s | P | HP x (1 + 0.22 x PaceMinutes) at :3730; never two at once |
| side event start | `SideEventDirector.StartAfterSeconds` (`SideEventDirector.cs:17`) | 90 pace s | P | its internal timers (grove hold 20 s etc.) are R |
| boss arrival | `TickBossPhaseStart` :3741 | `timeElapsed >= TimerVictorySeconds` | - | everything paced freezes with the clock |
| boss health | `BossCatalog.cs:42-127` | 1900-11000 | F | absolute; tuned for the build a 30-minute run brings |

### Chests, rewards, achievements, UI

| knob | where | value | class | notes |
|---|---|---|---|---|
| first chest / chest interval | `Node2DGame.cs:144-145`, :1964 | 20 / 45 pace s | P | about 20 chests a run at any length |
| Arcane reward per minute | `ArcanePerMinuteSurvived` :151, :4663 | 8 per **real** minute | R | a longer run pays more currency; see 7 |
| "improved on last run" bonus | `GlobalStatsManager.cs:408` | up to +6%, real s / 2100 | R | |
| achievement "Survivor" | `AchievementDefinition.cs:98, 113-120` | 10 real min | R | a third of today's run |
| achievement "Untouchable" | `AchievementDefinition.cs:99, 163-170`; `Node2DGame.cs:4487` | 5 real min | R | |
| achievement "Archmage Training" | `AchievementDefinition.cs:130-136` | player level 20 | **level** | about minute 13-15 for the bot today |
| onboarding tips | `Node2DGame.cs:446-452` | 0 / 30 / 90 / 180 / 300 s | R | |
| hazard tick | `HazardTickInterval` :231 | 0.5 s | R | |
| debug: F4 skip to boss | :1891 | sets `timeElapsed = TimerVictorySeconds` | - | |
| debug: stress swarm | :24-31, :1901 | `user://debug_stress_swarm` fills to 140 | - | XP held, so no levelling under it |

### Difficulty presets (`ApplyBalancePresetFromSave`, `Node2DGame.cs:4382`)

| preset | spawn interval | spawn health | elite interval / power | **burst window** |
|---|---|---|---|---|
| casual | x1.14 | x0.88 | x1.18 / x0.86, one fewer elite | **x0.84** (59 per 10 s) |
| normal | x1 | x1 | x1 / x1 | 70 |
| hardcore | x0.88 | x1.18 | x0.84 / x1.18, one more elite | **x1.15** (80 per 10 s) |

Because the burst window is the XP ceiling (3.3), **the preset moves the level clock**: hardcore
levels roughly 15% faster in the back half, casual roughly 16% slower. The gates were measured on
normal.

## 3. The levelling model

### 3.1 The XP economy

| piece | where | value |
|---|---|---|
| cost of level l -> l+1 | `Player.CalculateXPForLevel`, `Player.cs:2684-2691` | `XpFirstLevelCost + XpCostPerLevel x (l-1)` = **10 + 55(l-1)** |
| total XP to stand at level L | (derived) | **10(L-1) + 27.5(L-1)(L-2)** |
| ordinary kill | `Enemy.DropXp`, `Enemy.cs:1182` | **2 XP** (see 7: the 3 and 5 branches never fire) |
| `IsMiniBoss` kill (elites, event elites, Wardens, Boo and Rockem in the Sketchbook, bosses) | same | 50 XP |
| bonus drop | `Enemy.TryDropBonusItem` :1151; `Player.GetBonusDropChance` :1542 | 3% (+luck), a third of them an XP orb x8 = 16 |
| gain multipliers | `Player.AddXp` :4117 | Sage Spiral `growth` +10%/level up to 6 (`MainMenu.cs:1492`, `Player.cs:556`); Arcane tier 2/4/6 x1.10/1.20/1.35 (:1316); chest XP items +10..30% each; playtest mode x1.15 x1.75 |
| pickup radius | `XPOrb.AttractDistance` 80 + `MagnetBonus` | Void Lure +20/level up to 7 |
| level-up cadence knobs | `Player.cs:114, 132` | `UpgradeOfferChance` 0.6, `BoonOfferChance` 0.22 - these decide *what* a level offers, not when it comes |
| level-up charges | `Player.cs:231-237, 561-569` | rerolls refill every level; bans, saved levels, auguries are per run |
| gates | `Player.AscensionMinimumPlayerLevel` (`Player.cs:4792`), `ApotheosisCatalog.MinimumPlayerLevel` (`ApotheosisCatalog.cs:60`) | **17** and **23** |
| milestones | `SpellData.MaxLevel` 8; evolutions at 4 and 8; Apotheosis milestone 9 | per spell, needs upgrade picks |
| slots | `Player.MaxSpellSlots` (`Player.cs:281`) | 6 |

Cumulative XP to reach the levels that matter:

| level | 10 | 14 | 16 | 17 | 18 | 20 | 22 | 23 | 24 | 26 | 28 | 30 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| total XP | 2,070 | 4,420 | 5,925 | **6,760** | 7,650 | 9,595 | 11,760 | **12,925** | 14,145 | 16,750 | 19,575 | 22,620 |

### 3.2 Measured: the bot's level clock on today's run

`_BalanceHarness --seek-apotheosis --all-unlocked --immortal --minutes=30 --timescale=2`, one run
at a time, enemy cap 140, forest (stage 0), normal preset, 2026-10-15. Seven runs: the six
wizards at gates 22/16 plus the Pyromancer and the buffed Geomancer at 23/17 (the gates do not
change XP income, so all seven measure the same thing). The pre-buff Geomancer is left out of the
median; he is the example in section 6. Times are game time.

**Level at each minute:**

| min | 2 | 4 | 6 | 8 | 10 | 12 | 14 | 16 | 18 | 20 | 22 | 24 | 26 | 28 | 30 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| median | 5 | 8 | 11 | 14 | 16 | 18 | 20 | 21 | 22 | 24 | 25 | 26 | 27 | 28 | 29 |
| fastest | 6 | 9 | 12 | 16 | 18 | 20 | 21 | 22 | 23 | 24 | 26 | 27 | 28 | 29 | 31 |
| slowest | 4 | 7 | 10 | 13 | 15 | 17 | 19 | 20 | 21 | 22 | 24 | 25 | 26 | 27 | 28 |
| Geomancer before his buff | 5 | 7 | 10 | 13 | 15 | 17 | 19 | 20 | 20 | 21 | 22 | 22 | 23 | 23 | 24 |

**When each level is first reached (minutes):**

| level | Pyro | Frost | Storm | Tituba | Väinä | Pyro@23 | Geo@23 | median |
|---|---|---|---|---|---|---|---|---|
| 10 | 5.0 | 5.0 | 5.0 | 5.5 | 5.5 | 5.0 | 5.0 | 5.0 |
| 14 | 7.0 | 8.0 | 8.0 | 8.0 | 8.5 | 7.5 | 7.5 | 8.0 |
| 16 | 8.0 | 10.0 | 9.5 | 9.5 | 10.5 | 9.0 | 9.0 | 9.5 |
| **17** | **9.0** | 11.0 | 10.0 | 10.0 | 11.0 | 10.0 | 10.0 | **10.0** |
| 20 | 11.5 | 14.6 | 13.5 | 14.0 | 14.0 | 13.0 | 13.0 | 13.5 |
| 22 | 14.5 | 18.1 | 16.5 | 17.0 | 17.5 | 16.5 | 16.0 | 16.5 |
| **23** | **16.5** | 20.1 | 17.5 | 19.0 | 19.5 | 18.0 | 17.5 | **18.0** |
| 26 | 21.5 | 24.1 | 23.1 | 23.6 | 25.5 | 23.1 | 22.5 | 23.1 |
| 28 | 25.1 | 27.6 | 26.1 | 27.1 | 28.6 | 26.6 | 26.5 | 26.6 |

Today, as fractions of the 30-minute run: the Ascension gate lands at **0.33 T median, 0.30 T
fastest**; the Apotheosis gate at **0.60 T median, 0.55 T fastest**. The fastest wizard reaches a
level at about **0.9 x the median time**, and the spread between wizards is about +-2 minutes at
level 23. That spread is small because of 3.3.

**XP income per minute** (from the level deltas, median of the seven):

| window (min) | 0-5 | 5-10 | 10-15 | 15-20 | 20-25 | 25-30 |
|---|---|---|---|---|---|---|
| XP / min | ~400 | ~770 | ~830 | ~580 | ~780 | ~950 |

It rises over the first ten minutes, **dips at 15-20**, and ends near 950. The dip is the spawner,
not the wizards: `GetSpawnBatchCount` drops to batches of 1-3 between pace minutes 7 and 11 (real
14-22), which takes the supply under the burst ceiling for those minutes. It is why level 20 to 22
takes the bot three minutes.

### 3.3 Why: the kill rate is the spawn supply, and the supply is capped in real seconds

A strong wizard kills what arrives - the alive count holds at 80-130 rather than sitting on the cap
- so **XP income = spawns per second x XP per spawn**. Spawns per real second are:

```
supply(t) = min( batch(m) / interval(m)  +  formations (7 per 15 real s, after 75 pace s),
                 MaxSpawnsPerBurstWindow / SpawnBurstWindowSeconds )      // 70 / 10 = 7 per s
          where m = PaceMinutes(t)
XP / min  ~= kappa x supply x 2.16 x 60  +  elites (48 extra each)  +  Boo (2.5% at 50, first 5 pace min, Sketchbook only)
```

2.16 is 2 XP a kill plus the bonus orbs; `kappa` is the fraction of the supply the bot actually
kills and collects. **kappa = 0.8 reproduces the median table above to within one level at every
minute** (model minute 10 -> 17, measured 16; 20 -> 23, measured 24; 30 -> 29, measured 29). The
spawn *interval* is read at a pace minute but is itself a real-second gap, and the burst window is
real seconds; so from pace minute ~5 onward the supply sits at or near **7 enemies per real second,
about 900 XP a minute, on a run of any length**.

Two consequences, and they are the whole reason this doc exists:

1. **Shortening the run compresses only the opening ramp.** The ramp is paced; the ceiling is not.
   The model's level-by-real-minute for shorter runs:

   | min | 2 | 4 | 6 | 8 | 10 | 12 | 14 | 15 | 20 | 30 |
   |---|---|---|---|---|---|---|---|---|---|---|
   | T = 30 (fits measured) | 5 | 8 | 11 | 14 | 17 | 18 | 20 | 20 | 23 | 29 |
   | T = 20 | 6 | 9 | 13 | 15 | 17 | 18 | 20 | 21 | 24 | - |
   | T = 15 | 6 | 10 | 13 | 15 | 17 | 19 | 20 | 21 | - | - |

   After minute ten the three rows are the same. Level 23 is still about minute 18 on a 20-minute
   run - 0.9 T, against a target of 0.6 T - and a 15-minute run ends at about level 21, so the
   Apotheosis is **never offered**.

2. **Anything that changes the supply or the kill fraction moves the gates in real time** - the
   enemy cap, the burst window, the spawn interval curve, the batch sizes, the spawn ring, the
   preset, a wizard's damage. See section 6.

The model is an estimate, not a measurement: use it to choose a starting value, then measure
(section 5, step 5). It is a few lines to rebuild - step real time in quarter seconds, apply the
formula above, accumulate XP, and read off when the total passes each level's cost.

## 4. What a length change does to everything else

| thing | when `TimerVictorySeconds` changes |
|---|---|
| every P row in section 2 | moves with the run - nothing to do |
| R rows | stay in real seconds - by design; re-read the three achievements and the Arcane reward |
| level clock, gates | **stay put in real minutes** - must be re-chosen (section 5) |
| level at the end of the run | 29 today; about 24 at T = 20 and 21 at T = 15 with today's curve |
| boss health (`BossCatalog`) | fixed - tuned for a level-29 build; a shorter run on today's curve brings a smaller one |
| Arcane currency per run | scales with real minutes - a 15-minute run pays roughly half the minute bonus |
| enemy spawn health at the end | the same (it is paced) |

## 5. The recipe: changing the run length

Let T be the new length in minutes. The targets travel as fractions
(`.ai/gameplay-design.md`): Ascension at ~0.33 T median and no earlier than ~0.30 T for the fastest
wizard; Apotheosis at ~0.55-0.65 T median and **no earlier than 0.5 T for the fastest wizard**
(the floor).

1. **Set `TimerVictorySeconds`** (`Node2DGame.cs:36`) to 60 T. Nothing else in the pace clock
   needs touching; `ReferenceRunSeconds` stays 900.
2. **Pick a strategy for the level clock.** Two levers; prefer B.
   - **A. Keep the XP curve, lower the gates.** Read the model row for T in 3.3 (or the measured
     table, for T = 30) at 0.33 T and 0.6 T. Simple, but the run ends at a lower level: fewer picks,
     a smaller build at the boss, and fewer picks before each gate - today the Apotheosis gets 22
     picks to assemble its five conditions, at T = 15 it would get 16, and the upgrade roll starts
     deciding the timing again, which is what the gates exist to stop.
   - **B. Keep the gates (17 / 23), lower `XpCostPerLevel`** (`Player.cs:2685`). The run ends at
     about the same level as today, so every pick count, the build at the boss and the gates' place
     in the build are unchanged; only the real time between level-ups shrinks. The XP clock itself
     does not change, so solve `10(L-1) + (b/2)(L-1)(L-2) = XP(t_target)` for b at L = 23.
3. **Check both gates against the fastest wizard** (0.9 x the median time). It is the floor.
4. **Re-read the R rows**: the "Survivor" (10 min) and "Untouchable" (5 min) achievements, the
   "Archmage Training" level 20, and `ArcanePerMinuteSurvived` if the currency per run should hold.
5. **Measure.** One run per wizard, six in all, one at a time on an idle machine (section 6).
   Compare the `HARNESS ... APOTHEOSIS offered and taken` and level-17 times to the targets; move a
   gate by one level (about 1-2 minutes) or `XpCostPerLevel` by 2 (about 0.5 minute at level 23)
   and re-run the outliers. Update the measured table in `.ai/gameplay-design.md` and this one.
6. **Fight the bosses** with the build the new run brings (`_BossProbe` runs them, but only eyes
   say whether a fight is readable), since boss health does not scale.

### Worked example: a 20-minute run (`TimerVictorySeconds` = 1200)

Targets: Ascension ~6.6-7 min (fastest >= 6), Apotheosis ~11-12.5 min (fastest >= 10).

| strategy | settings | Ascension median / fastest | Apotheosis median / fastest | level at 20:00 |
|---|---|---|---|---|
| none (today's numbers) | 55, gates 17 / 23 | 9.3 / 8.4 (0.47 T) | 17.8 / 16.0 (0.89 T) | 24 |
| A | 55, gates **15 / 19** | 7.2 / 6.5 (0.36 T) | 12.2 / 11.0 (0.61 T) | 24 |
| **B** | **`XpCostPerLevel` 35**, gates 17 / 23 | 6.3 / 5.7 (0.32 T) | 11.8 / 10.6 (0.59 T) | 30 |

At 33 the Apotheosis fastest is 10.0 - exactly on the floor - and at 37 the Ascension drifts to
0.33 T median; 35 is the middle. Gate 18 under A would put the fastest at 9.7, under the floor.

Shipped for part of 2026-10-16 as B, `XpCostPerLevel` 35, then replaced by the 15-minute run below.

### Worked example: a 15-minute run (`TimerVictorySeconds` = 900, `RunPace` = 1)

Targets: Ascension ~5 min (fastest >= 4.5), Apotheosis ~8.5-9.5 min (fastest >= 7.5).

| strategy | settings | Ascension median / fastest | Apotheosis median / fastest | level at 15:00 |
|---|---|---|---|---|
| none | 55, gates 17 / 23 | 9.4 (0.63 T) | **never** - the run ends at 21 | 21 |
| A | 55, gates **13 / 17** | 5.2 / 4.7 (0.35 T) | 9.4 / 8.5 (0.63 T) | 21 |
| **B** | **`XpCostPerLevel` 27**, gates 17 / 23 | 4.8 / 4.3 (0.32 T) | 9.1 / 8.2 (0.61 T) | 29 |

**Shipped (2026-10-16): B, `XpCostPerLevel` 27.** When it is measured, put the six wizards' times
here.

Gate 16 under A puts the fastest at 7.5, on the floor exactly; take 17. At T = 15 the other four
Apotheosis conditions (spell at level 8, the named path, tier 6 - measured at 3.9 to 8.4 minutes)
are close behind the gate; under A they may become what sets the time, under B they keep today's
place in the build. Either way, step 5 is not optional.

**What is computed and what must be measured.** The fractions, the strategy and a first value are
computed. Every number in the two tables above is a model prediction good to about +-1 minute; the
floor is a promise, so it has to be re-measured on the real run.

## 6. Measuring: protocol and pitfalls

```
"$GODOT_BIN" --headless --path . scenes/_BalanceHarness.tscn -- --minutes=<T> --character=<1..6> --seek-apotheosis --all-unlocked --immortal --timescale=2
```

Characters: 1 Pyromancer, 2 Frostweaver, 3 Stormcaller, 4 Geomancer, 5 Tituba, 6 Väinämöinen.
The log has `HARNESS t=<s>s level=<L> enemies=<n>` every 30 game seconds, the tier-2/4/6 times
and `HARNESS APOTHEOSIS offered and taken at t=...` (`_BalanceHarness.cs:160, 405, 431`).
`--xp-first=N`, `--xp-step=N` and `--upgrade-chance=X` override the curve for one run, which is
how strategy B is tried without an edit.

- **One run at a time, machine idle.** Parallel Godot runs slow each other, frame timing changes,
  and per-frame hit tests (and so kills, and so XP) change with it. Six in parallel produce numbers
  that look plausible and are wrong.
- **`--timescale=2`**, as every table here was. Higher scales coarsen each physics step.
- **`--seek-apotheosis --all-unlocked --immortal`.** Seeking makes it the earliest plausible
  arrival; all-unlocked gives every spell and the four level-up charges at their caps; immortal
  keeps the run going and the bot holds its ground except to collect XP.
- **One run is a direction, not a number.** The same wizard swings by a minute or two between
  runs; take the median of six, and treat a single outlier as a reason to re-run, not to retune.
- **The bot levels faster than a human** - it walks to every orb and never stops moving well. The
  skilled and ordinary rows in `.ai/gameplay-design.md` are judgement on top of the bot floor.
- **The bot is not a finished shop.** `--all-unlocked` buys rerolls, bans, saved levels and
  auguries, but not Sage Spiral (`growth`, +60% XP at cap), Void Lure, or the damage upgrades
  (`_BalanceHarness.cs:113-118`). See section 7.
- **It measures the forest** (stage 0) on the normal preset only.
- **Things that silently change XP income, and so invalidate the gate times:** the enemy cap, the
  burst window, the spawn interval curve and batch sizes, the spawn ring and forward bias, the
  difficulty preset, `XpCostPerLevel` / `XpFirstLevelCost`, XP per kill, and **a wizard's damage**.
  The Geomancer at half damage levelled about three minutes slower from minute 18 on (table in 3.2:
  level 22 at 21.1 against 16.0 after his buff) because his kills fell behind the supply and the
  arena sat at the cap (130-136 alive) - a kill-limited wizard leaves the supply model. Any change
  to one of these means re-measure.

## 7. History, and findings still open

**The gates, as evidence of how sensitive they are:**

| when | enemy cap | gates (Asc / Apo) | why it moved |
|---|---|---|---|
| 2026-10-11 | 70 | 14 / 20 | first set: level 14 about minute 9, 20 about minute 16 on the linear curve |
| 2026-10-15 | 140 | 16 / 22 | the cap doubled for the wider view; doubled XP on the ground put 20 at 13-15 min, five of six forms under the floor |
| 2026-10-15 | 140 | **17 / 23** | at 22/16 the Pyromancer still took his form at 14.3 and ascended at 7.5 |

A one-line change to the enemy cap was worth three levels on each gate. Each gate level is about
one to two minutes of run.

**Findings from writing this doc - recorded, not fixed:**

1. **Tank and fast enemies drop 2 XP, not 5 and 3.** `Enemy.DropXp` switches on `EnemyType`
   (`Enemy.cs:1190-1195`), but `TankEnemy.tscn` and `FastEnemy.tscn` never set it, so it is the
   default `"Enemy"` and both branches are dead. The comment in `XPOrb.cs:12-13` describing the
   drop table is wrong in the same way (elites drop 50, not 5). Every ordinary kill in the game is
   2 XP, which is what makes the supply model simple; fixing the branches would raise XP on the
   tank-heavy stages (Castle, Ruins) by up to ~30% and move the gates there.
2. **Boo and Rockem drop 50 XP each.** Both scenes set `IsMiniBoss = true`. Rockem is 7% of the
   Sketchbook table, so the Sketchbook pays roughly 2.5x the XP per spawn of any other chapter and
   reaches the gates far earlier; both also count toward `MaxEliteEnemiesAlive`. (The Warden sets
   the flag too and drops 50; an earlier version of this note said it did not.)

   **The Boo now spawns only in the Sketchbook (2026-10-20).** It is Elim's drawing - one of Eric's
   kids - so it went to their chapter. Until then it opened *every* chapter, 2.5% of spawns for the
   first 5 pace minutes, and its 50 XP was a large share of every chapter's opening income. Taking
   it out of the eight campaign chapters (stages 0-7) changed their early XP, and nothing was
   retuned to make up for it. Same build both ways but for that one line:

   | Enchanted Forest, immortal bot, 5 min, timescale 4 | level at 1:00 | 2:00 | 3:00 | 4:00 | 5:00 |
   |---|---|---|---|---|---|
   | with the Boo (28 of 1078 spawns were Boos) | 4 | 7 | 10 | 13 | 16 |
   | without it | 4 | 7 | 9 | 11 | 13 |

   One run each, so read it as a direction rather than a figure, but it matches the arithmetic: 28
   Boos at 48 XP above an ordinary kill is about 1350 XP, which is 23% of what level 16 costs and
   about a minute and a half of mid-run income (3.3). **Expect the campaign chapters to level
   slower through the first five minutes, and the Ascension and Apotheosis (17 / 23) to arrive
   somewhat later there** - the late-run supply cap is unchanged, so the gap stops growing at about minute 5 and
   is carried rather than recovered. The tables in 3.2 were measured with the Boo in every chapter
   and now overstate the opening outside the Sketchbook. Re-measure the gates before deciding
   whether to compensate (an XP-curve change, or an opening XP source of the chapters' own).

   **Then the Boo became a scheduled miniboss (2026-10-20, later the same day).** Eric asked for it
   to be made bigger and scarier and a miniboss (`.ai/enemy-behaviour.md` 7b). It is in no spawn
   table now: `Node2DGame.TickGhostMiniBoss` sends it at **pace 4:00 and 9:30**, on the pace clock
   like everything else that decides when in a run, so a shorter run brings it proportionally
   sooner. It still pays **50 XP** (`IsMiniBoss`), the same as the Warden and Rockem, which answers
   the open question this note used to end on: at two a run, 50 each is a miniboss's reward rather
   than an income stream.

   **What that does to the Sketchbook's XP.** The old 2.5% share was about 28 Boos in the first
   five minutes (the Enchanted Forest measurement above), about 1350 XP above what ordinary kills
   in their place would have paid. Two ghosts pay 100. So the Sketchbook's first five minutes lose
   roughly 1250 XP - about a fifth of what level 16 costs - and, by the arithmetic of 3.3, it
   should now open about the way the campaign chapters do after the Boo left them: a level or three
   behind the old figures by minute five, carried rather than recovered. The Sketchbook gates
   nothing (no Discovery, no chapter after it), and Rockem still makes it the richest chapter per
   spawn, so nothing was retuned. One run after the change
   (`_BalanceHarness -- --minutes=12 --character=1 --timescale=2 --immortal --stage=8 --all-unlocked`,
   seed 1234): the ghost arrived at pace 240 s (health 489) and 570 s (health 803), exactly two
   spawned, no script errors; level 4 / 9 / 12 / 16 / 19 at minutes 1-5, 35 at minute 12. No
   pre-change Sketchbook run exists on this build to set it against, so the size of the drop is the
   arithmetic above, not a measurement.
   The ghost also no longer counts toward `MaxEliteEnemiesAlive` in the opening minutes, which the
   2.5% roll could do several times over.
3. **The floor is measured without Sage Spiral.** A player with the shop finished has +60% XP. At
   the bot's kill rate that is level 23 at about minute 12.5 and level 17 at about minute 8 - under
   the 15-minute floor `.ai/gameplay-design.md` says even a skilled player must not beat. Humans
   collect less, but the margin is not measured. Arcane tier 6 (x1.35) and the chest XP items stack
   on top.
4. **The difficulty preset scales the burst window**, so hardcore reaches the gates ~15% sooner
   and casual ~16% later (section 2).
5. **The burst window is the dominant levelling knob and it is in real seconds.** That is right
   for feel, but it is why the gates do not follow the pace clock; anyone retuning density for
   feel is also retuning the gates.
6. **`ArcanePerMinuteSurvived` is per real minute**, so doubling the run to 30 minutes doubled
   the time-based share of the currency per run; the shop prices were set before that.
7. **The "Survivor" achievement (10 real minutes) is now a third of a run** and "Archmage
   Training" (level 20) about minute 14 for the bot; both were set for a 15-minute run.
8. **Boss health is absolute** while the build that meets it depends on the level clock; a length
   change under strategy A brings a smaller build to the same boss.
9. `Player.AddXp` grants at most one level per call (`Player.cs:4124-4129`); an orb worth more
   than the next level leaves the remainder banked until the next pickup. Harmless at today's orb
   sizes (50 at most), worth knowing if a large XP source is added.
