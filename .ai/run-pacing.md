# Run pacing: what decides WHEN things happen in a run

The single reference for every number that places something in time - spawns, enemy strength,
elites, chests, the boss, level-ups and the two level gates - and a recipe for changing the length
of a run without re-tuning all of it by hand. The design targets themselves (when the Ascension and
the Apotheosis should land, and why) live in `.ai/gameplay-design.md`, "The Apotheosis"; this doc
says how the code gets there and how to move it.

**The one thing to take away.** A run runs on two clocks, and only one of them follows the run
length:

- **The pace clock** places the *world*: which enemies exist, how many arrive, how tough they
  are, when elites, the Warden and chests come. It scales with `TimerVictorySeconds`
  automatically. (The side event is on neither clock since 2026-10-22: it is placed in space,
  not time, and the trip to it is paid in walking - about half a minute each way with no swarm,
  more with one - which is XP orbs left behind rather than a position on the run.)
- **The player's level** places the *build*: the Ascension (level 17) and the Apotheosis
  (level 23). Level comes from XP, XP comes from kills, and the kill rate in the middle and late run
  is pinned by a cap that is in **real seconds** (the spawn burst window). So changing the run
  length moves the world but **does not move when the player reaches level 17 or 23**. Change
  `TimerVictorySeconds` alone and the gates stay where they are in real minutes, which on a shorter
  run means late or never.

Section 5 is the recipe; sections 3 and 4 are why it works.

**Current setting (2026-10-23): a 15-minute run, strategy B** - `TimerVictorySeconds` 900
(`RunPace` 1), **`XpCostPerLevel` 45**, gates unchanged at 17 / 23. It was 27 from 2026-10-16,
solved from the model in section 5 for a bot that never opened a chest; measured with bots that do
(section 8), 27 paid 38-43 levels in 15 minutes and put level 23 under the Apotheosis floor, so it
went to 45. The measured tables in section 3 are still the 30-minute run; current values quoted
elsewhere in this doc (1800, 55, `RunPace` 2) are that run's.

**Since 2026-10-24: XP grows on the pace clock** (`Node2DGame.XpGrowthPerPaceMinute` 0.10 - a point
of XP is worth 1 + 0.10 x `PaceMinutes`, applied in `Player.AddXp`), step unchanged at 45, and **a
field nobody clears closes in** (`Node2DGame.SwarmPressure.cs`). Re-measured on the build with the
collector economy, the 45 alone had the ordinary bot end the run at level 22-24 with one or two
Ascensions, levelling every 50-75 s in the last five minutes (and at level 17 in the Mystic Ruins).
Section 10 has the design, the knobs and the before/after tables; it also corrects how this doc's
runs should be made on a shared machine (`--fixed-fps 30`, 10.5).

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
- advance a timer by `delta / RunPace` (the Warden `:3707`; chests used to, until they became
  carried by elites and minibosses, which are themselves on the pace clock);
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
| side event start | `SideEvent.ArrivalRadius` | **not timed** (since 2026-10-22) | F | placed 4-6 chunks out at stage start, begins when the player arrives; its internal timers (grove hold 20 s, lantern snuff 3 s) are R. Walking there is the cost: `.ai/side-events.md`, "Finding the event" |
| boss arrival | `TickBossPhaseStart` :3741 | `timeElapsed >= TimerVictorySeconds` | - | everything paced freezes with the clock |
| boss health | `BossCatalog.cs:42-127` | 1900-11000 | F | absolute; tuned for the build a 30-minute run brings |

### Chests, rewards, achievements, UI

| knob | where | value | class | notes |
|---|---|---|---|---|
| chests | `Node2DGame.DropCarriedChest`, `DropTable` | carried: an elite 65% (the run's first always), the Warden and the ghost always, a side event | P, through the carriers | **no chest clock since 2026-10-22** (it was 20 / 45 pace s, 20 a run); see `.ai/passives-and-items.md` section 8 for chests per run |
| speed potion spacing | `DropTable.MinSecondsBetweenSpeedPotions` | 45 s | **R** | a rhythm: how often the burst may be felt (0.3% a kill, about 10 a run) |
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
| bonus drop | `Enemy.TryDropBonusItem`; `DropTable.Roll` | XP orb x8 = 16 at **1%** a kill, unchanged (x(1 + 2.33 Luck)); the flask and the potion are rarer since 2026-10-22 (`.ai/passives-and-items.md` 8a) |
| bigger orbs | `Enemy.DropXp`; `Player.GetGreaterOrbChance` | an ordinary kill's orb is 10 instead of 2 at the chance the Signet Ring (5%), the Tithe of Wormwood (5%) and the Regalia (10%) add; 0 without them. Each point is +0.08 XP a kill |
| pace multiplier | `Player.AddXp`, `Node2DGame.XpPaceMultiplier` | **x(1 + 0.10 x `PaceMinutes`)** since 2026-10-24: x1.5 at pace 5:00, x2 at 10:00, x2.5 at the boss (it freezes with the clock). On every point of XP, after every other multiplier (section 10.2) |
| gain multipliers | `Player.AddXp` | Sage Spiral `growth` +10%/level up to 6 (`MainMenu.cs:1492`, `Player.cs:556`); Arcane tier 2/4/6 x1.10/1.20/1.35 (:1316); chest XP items +10..30% each; playtest mode x1.15 x1.75. **The fraction is carried since 2026-10-22** - before, each orb was rounded, so on a 2 XP orb every bonus under +25% paid nothing and +30% to +60% all paid +50% (finding 10) |
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

**Shipped (2026-10-16): B, `XpCostPerLevel` 27. Measured 2026-10-23 and replaced by 45** - see
section 8. The model's 27 assumed the chest-less bot's income; a bot that opens chests (Compass Rose
+15%, Wormwood Tithe +30% XP, both in nearly every run) earns about 1,500-1,700 XP a minute from
minute six, not 900, and at 27 reached level 23 at 6.0-7.2 minutes and level 38-43 by 15:00.

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
and `HARNESS APOTHEOSIS offered and taken at t=...` (`_BalanceHarness.TrackTier`,
`PickTowardApotheosis`), and the times player levels 17 and 23 were reached.
`--xp-first=N`, `--xp-step=N` and `--upgrade-chance=X` override the curve for one run, which is
how strategy B is tried without an edit.

- **One run at a time, machine idle.** Parallel Godot runs slow each other, frame timing changes,
  and per-frame hit tests (and so kills, and so XP) change with it. Six in parallel produce numbers
  that look plausible and are wrong. **And a full field is slow on its own**: a weak build standing
  in 130 enemies ran at 1-5 fps here with nothing else running (section 10.5). Where that matters -
  any before/after of the level clock - use `--fixed-fps 30 --timescale=1` (an engine flag, before
  the `--`), which makes every frame 1/30 s of game time whatever the machine does; the price is
  about 20-30 real minutes per 15-minute run.
- **`--timescale=2`**, as every table here was. Higher scales coarsen each physics step.
- **`--seek-apotheosis --all-unlocked --immortal`.** Seeking makes it the earliest plausible
  arrival; all-unlocked gives every spell and the four level-up charges at their caps; immortal
  keeps the run going and the bot holds its ground except to collect XP.
- **One run is a direction, not a number.** The same wizard swings by a minute or two between
  runs; take the median of six, and treat a single outlier as a reason to re-run, not to retune.
- **The bot levels faster than a human** - it walks to every orb and never stops moving well. The
  skilled and ordinary rows in `.ai/gameplay-design.md` are judgement on top of the bot floor.
- **The bot is not a finished shop.** `--all-unlocked` buys rerolls, bans, saved levels and
  auguries, but not Sage Spiral (`growth`, +60% XP at cap), Void Lure, or the damage upgrades.
  `--full-shop` adds every Arcane Codex upgrade at its cap; keep it out of anything compared with
  the Apotheosis floor, which is measured without it. See section 7.
- **The older policies never open a chest properly.** Under the harness the game is not the
  current scene, so `ChestReward` takes its no-menu path and grants its `ItemId`, which nothing
  sets: every chest the bot stumbled into was a Relic Key. `--policy=ordinary` and
  `--seek-ascensions` walk to chests and deal themselves the real chest offer
  (`_BalanceHarness.PrepareNearbyChests`), so their runs carry XP relics (Compass Rose, Wormwood
  Tithe) and tag relics that the `--seek-apotheosis` tables never had - and level faster for it.
- **Counting Ascensions:** `--policy=ordinary` (a good player who does not plan for them) and
  `--seek-ascensions` (one who does, with every charge). Every report now has an `ascensions` line
  (when each was taken), a `level clock` line, an `ascension standing` block saying what held back
  each spell that did not ascend, and a final `HARNESS SUMMARY` line. See `.ai/gameplay-design.md`,
  "How many Ascensions a run gets".
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
| 2026-10-16 | 140 | 17 / 23, `XpCostPerLevel` 55 -> 27 | the run shortened to 15 minutes; strategy B from the chest-less model |
| 2026-10-23 | 140 | 17 / 23, `XpCostPerLevel` 27 -> **45** | measured with bots that open chests: level 23 at 6-7 min, level 38-43 at 15:00, 4-5 Ascensions a run (section 8) |
| 2026-10-24 | 140 | 17 / 23, 45, **XP x(1 + 0.10 x pace min)** | re-measured with the collector economy: 45 alone ended the run at level 22-24 (17 in the Ruins), a level every 50-75 s at the end (section 10) |

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
10. **XP bonuses were rounded per orb, and on a 2 XP orb most of them paid nothing (fixed
    2026-10-22).** `AddXp` did `RoundToInt(amount x every multiplier)`, and nine kills in ten pay a
    2 XP orb: 2 x 1.15 = 2.3 rounds to 2, and so does 2 x 1.25 (C# rounds half to even). So the Orb
    of Antioch's +15%, Arcane's tier 2 and tier 4 (+10%, +20%) and Sage Spiral's first two levels did
    nothing on an ordinary kill, while everything from +30% to +60% paid the same 3 - a flat +50%.
    The fraction is carried now (`Player.xpFraction`), so every bonus pays what its card says. What
    that moves, by the model in section 8: Sage Spiral at level 2 goes from nothing to +20% (level
    23 about 1.2 minutes sooner for a player who owns it), at its cap from an effective +50% to +60%
    (about 0.4 minutes sooner); Arcane tier 6 from +50% to +35% (later). The bot is measured without
    Sage Spiral and without relics, so the floor measurements do not move unless a run reaches
    Arcane tier 2. **Anything measured with `--full-shop` (branch `balance/ascension-count`) moves
    with it** - re-measure there after this lands.

## 8. The collector economy (2026-10-22): what it does to the level clock

Chests stopped coming on a clock, the speed potion and the health flask became rarer, and the three
relics that made chests come more often now pay in bigger XP orbs (`.ai/passives-and-items.md`
section 8). **The bot's level clock - the floor - does not move:** the bonus table's XP orb is
unchanged at 1% a kill, the bot never holds a relic (under the harness a chest takes its no-menu
path and grants a Relic Key), and a bigger orb needs a relic. What moves is the clock of a player
who finds one of those relics, and it moves **later** than before, because the old ones were a far
larger XP item than their cards admitted: the Signet Ring's "+20% chests" also added 20 points to
every kill's bonus drop chance (3% to 23%), a third of which were 16 XP orbs - +49% XP from a
Common relic that never mentioned XP.

Modelled, not measured: section 3.3's T = 15 row turned into cumulative XP and read against the
shipping curve (`XpCostPerLevel` 27), with the relic's multiplier applied from the moment it is
found. A relic in the first chest is the best case - the first elite, at about 2:15 - and the
Regalia is assumed assembled by minute 8.

| holding | XP multiplier before / after | level 17 before / after | level 23 before / after |
|---|---|---|---|
| nothing (the bot) | 1 / 1 | 5.6 / 5.6 | 9.6 / 9.6 |
| Signet Ring from 2:15 | x1.49 / x1.19 | 4.6 / 5.1 | 7.2 / 8.5 |
| Tithe of Wormwood from 2:15 | x2.20 / x1.54 | 3.9 / 4.5 | 5.5 / 7.0 |
| Orb of Antioch from 2:15 | x1.01 / x1.15 | 5.6 / 5.2 | 9.6 / 8.7 |
| the whole Regalia from 8:00 | x2.31 / x2.33 | 5.6 / 5.6 | 8.7 / 8.7 |
| Sage Spiral level 2, all run | x1.01 / x1.20 | 5.5 / 4.9 | 9.5 / 8.3 |
| Sage Spiral at its cap, all run | x1.51 / x1.60 | 4.2 / 4.1 | 6.8 / 6.4 |

The model's no-item row is about half a minute behind the strategy B prediction in section 5
(4.8 / 9.1), because it reads the level table rather than the kappa formula; compare rows with
each other, not with section 5. Read it as:

- **An early Signet Ring or Tithe no longer drags the Apotheosis under the floor the way it did.**
  The Tithe in the first chest put level 23 at 5.5 minutes; it is 7.0 now, still under 7.5, and it
  is a Relic-rarity curse that also brings 39% more enemies, which is the trade it is sold as.
- **The Regalia is unchanged** - its two hidden halves (chest frequency, and the bonus drop chance
  it fed, capped at 25%) and its stated +25% XP come to the same total as +25% XP and 10% bigger
  orbs. It is now the number on the card.
- **The Orb of Antioch and Sage Spiral get faster** because of finding 10, not because of the
  rework: they used to round to nothing.
- **Fewer relics, later.** The first chest is the first elite's (about 2:15) rather than 0:20, and
  a run without collector relics sees about three quarters as many chests (passives-and-items 8b), so
  every XP relic tends to arrive later than it did. That only moves the clock later.
- **The rarer potion costs damage.** At the old rate a late-run potion was up about half the time,
  +50% attack speed; at the new one about a tenth. A wizard who kills everything that arrives does
  not level slower for it (section 3.3: the supply is the ceiling), but a kill-limited one may.

**To check by play**, since no bot holds relics: a run that finds the Signet Ring or the Tithe in the
first chest, timed to level 17 and 23, against these rows.

## 9. Measured on the 15-minute run, with chests (2026-10-23)

**Measured before the collector economy (section 8) was merged.** These runs had the per-orb XP
rounding bug (finding 10), chests on the old 45-second timer, and the old chest-rate relics,
so the level-by-minute tables below are the shape, not the current numbers - re-measure.

The first measurement of the 15-minute run itself, and the first with a bot that opens chests.
`_BalanceHarness --policy=ordinary` and `--seek-ascensions` (what each is: `.ai/gameplay-design.md`,
"How many Ascensions a run gets"), 15 minutes, 2x time, `--immortal --all-unlocked`, seed 1234,
one run per wizard per policy, one at a time, on a machine running other sessions' Godot
processes. Outcomes are sound; seconds are noisy.

**Why it differs from section 3.** The old bot stumbled into chests that granted a Relic Key
(the harness is the current scene, so `ChestReward` took its no-menu path). Both new policies deal
themselves the real offer and take a relic, and in nearly every run that is the Compass Rose (+15%
XP) and the Wormwood Tithe (+30%) by minute five, on top of a build with level 7 spells where the
section 3 bot had six level 3s. Income from minute six on is about 1,500-1,700 XP a minute against
the model's 900, and the end-of-run level went from 29 to 38-43.

### Before: `XpCostPerLevel` 27

Minute each level was first reached (ordinary / planned):

| wizard | L10 | L17 (Ascension gate) | L20 | L23 (Apotheosis gate) | L26 | L29 | level at 15:00 | Apotheosis taken |
|---|---|---|---|---|---|---|---|---|
| Pyromancer | 3.5 / 3.1 | 5.2 / 4.8 | 5.9 / 5.4 | 6.7 / 6.2 | 7.8 / 7.3 | 8.9 / 8.4 | 42 / 37 | 6.7 / 6.2 |
| Frostweaver | 3.3 / 3.3 | 5.0 / 4.9 | 5.8 / 5.6 | 6.5 / 6.4 | 8.0 / 7.8 | 9.4 / 9.0 | 39 / 40 | 9.4 / 6.4 |
| Stormcaller | 3.3 / 3.0 | 5.1 / 4.8 | 5.8 / 5.4 | 7.2 / 6.4 | 8.5 / 7.7 | 10.2 / 9.1 | 31 / 40 | 7.3 / 6.4 |
| Geomancer | 3.1 / 3.2 | 4.9 / 5.0 | 5.5 / 5.8 | 6.2 / 6.7 | 7.2 / 7.8 | 8.4 / 9.4 | 43 / 40 | 13.1 / 6.7 |
| Tituba | 3.3 / 2.9 | 5.0 / 4.6 | 5.7 / 5.3 | 6.5 / 6.0 | 7.5 / 7.1 | 8.5 / 8.2 | 41 / 44 | 13.7 / 6.0 |
| Väinämöinen | 3.3 / 3.3 | 5.0 / 5.2 | 5.9 / 5.9 | 6.8 / 6.7 | 7.7 / 7.6 | 8.7 / 9.0 | 42 / 42 | never / 6.7 |

The Ascension gate landed inside its 4.5-6 target. The Apotheosis gate (level 23) landed at
6.0-7.2 for every wizard, under the 7.5 floor; the planned bot took its form at 6.0-6.7 and the
run went on to 38-43 levels, which is what paid for four or five Ascensions a run.

**Re-solving the step.** XP to stand at level L is `10(L-1) + (b/2)(L-1)(L-2)`. The planned bot
had about 10,500 XP at 9 minutes (its level 29) and about 20,400 at 15:00 (level 40). For level
23 at 9 minutes, `220 + 231 b = 10,500` gives b = 44.5; at 45 the run ends where
`10(L-1) + 22.5(L-1)(L-2) = 20,400`, L = 31.6, and the fastest wizard (Tituba, 10,500 XP at 8.2
minutes) reaches 23 at about 8.4 - over the floor. Shipped at 45.

### After: `XpCostPerLevel` 45

Minute each level was first reached (ordinary / planned), same protocol:

| wizard | L10 | L17 (Ascension gate) | L20 | L23 (Apotheosis gate) | L26 | L29 | level at 15:00 | Apotheosis taken |
|---|---|---|---|---|---|---|---|---|
| Pyromancer | 4.0 / 3.7 | 5.9 / 5.6 | 6.9 / 6.7 | 8.2 / 7.8 | 9.8 / 9.6 | 11.6 / 11.0 | 33 / 35 | 8.2 / 7.8 |
| Frostweaver | 4.1 / 3.9 | 5.9 / 5.9 | 6.8 / 7.1 | 8.1 / 8.6 | 9.5 / 10.2 | 11.4 / 11.9 | 33 / 33 | never / 8.6 |
| Stormcaller | 4.1 / 3.6 | 5.9 / 5.5 | 7.2 / 6.7 | 8.7 / 7.9 | 10.2 / 8.9 | 12.0 / 10.2 | 33 / 35 | 14.3 / 7.9 |
| Geomancer | 4.1 / 4.0 | 6.0 / 5.7 | 7.0 / 7.0 | 8.4 / 8.7 | 10.0 / 10.4 | 11.5 / 12.4 | 34 / 33 | never / 8.7 |
| Tituba | 4.1 / 4.3 | 6.0 / 6.6 | 6.9 / 7.6 | 8.3 / 9.0 | 9.8 / 10.7 | 11.3 / 12.6 | 35 / 33 | 13.5 / 9.1 |
| Väinämöinen | 4.1 / 4.1 | 6.2 / 6.1 | 7.4 / 7.1 | 9.1 / 8.4 | 11.1 / 10.2 | 12.4 / 11.8 | 32 / 34 | 12.5 / 8.4 |

- **Ascension gate (level 17): 5.5-6.6 minutes**, median 5.9 - the 4.5-6 skilled target, now at
  its far edge rather than its near one (it was 4.6-5.2). The fastest is 5.5, well clear of the
  4.5 floor.
- **Apotheosis gate (level 23): 7.8-9.1**, median 8.4; the planned bot took its form at 7.8-9.1,
  over the 7.5 floor in every run (it was 6.0-6.7). The skilled target is 8.5-10.
- **End of run: level 32-35** (it was 31-44), so the run pays about 32-34 picks, which is what puts
  the planned bot's Ascensions at 3-4 (`.ai/gameplay-design.md`, "How many Ascensions a run gets").
- The model in section 5 predicted 4.8 / 9.1 at 27; with chests the same income makes 45 land at
  roughly that place. **Re-measure** after anything that changes XP income (section 6), and note
  that these runs carry chest XP relics that the section 3 tables did not.

## 10. Keeping pace, and falling behind (2026-10-24)

Eric, after a playtest: "exp should scale better so the player levels up reasonably as they go
through the levels. if they fall behind they should eventually die, change how spawning/moving
enemies works so that if the player falls behind, they will eventually get swarmed."

Two changes answer the two halves, and they lean on each other. XP that keeps pace with the run
makes a player who kills well strong enough to keep the field clear; a swarm that closes on a field
nobody clears makes falling behind fatal rather than merely slow. A player who keeps the field
under the cap never meets the second change at all.

**Every table in this section is `--fixed-fps 30 --timescale=1`** (10.5 says why: at timescale 2 a
full field ran at 1-5 fps here and understated a weak build's kills by a quarter or more). One run
per row, seed 1234, `--all-unlocked`, normal preset; spawns are not seeded (`spawnRng.Randomize`), so
two runs of one row differ by a level or so - read rows as directions, and the before/after gaps,
which are far larger than that, as the result.

### 10.1 What was wrong, measured

`--policy=ordinary --immortal` (the good player who does not plan; it walks to every orb and opens
every chest), XP curve 10 + 45, no growth - the build as it stood on 2026-10-23:

| run | L10 | L17 | L20 | L23 | level at 15:00 | Ascensions | mean gap between level-ups, minutes 0-5 / 5-10 / 10-15 | XP a minute, by the same thirds |
|---|---|---|---|---|---|---|---|---|
| Pyromancer, Forest | 4.0 | 8.3 | 10.9 | 13.7 | 24 | 2 | 30 / 43 / 50 s | 509 / 886 / 1,078 |
| Stormcaller, Forest | 4.0 | 8.7 | 11.1 | 13.6 | 24 | 2 | 30 / 43 / 50 s | 516 / 755 / 1,124 |
| Tituba, Forest | 4.0 | 8.6 | 11.8 | never | 22 | 1 | 30 / 43 / 75 s | 514 / 822 / 713 |
| Pyromancer, Mystic Ruins | 4.4 | 14.1 | never | never | 17 | 1 | 33 / 75 / 100 s | 416 / 416 / 366 |

Three things, and the third is the one Eric felt:

1. **The gates had drifted out of reach.** `XpCostPerLevel` 45 was solved (section 9) from bots
   carrying Compass Rose and Wormwood Tithe under the per-orb rounding bug that made both a flat
   +50%; the collector economy (section 8) and the rounding fix landed the same day and were never
   measured with it. Level 17 came at 8.3-8.7 minutes against a 4.5-6 target, level 23 at 13.6 or
   never against 8.5-10.
2. **The cadence crawled.** Income rose to about 900-1,100 XP a minute and stopped, because the
   kill rate reaches what the spawner sends (about 450 a minute) by minute ten - while each level
   costs 45 more than the last. So the gap between level-ups grew all run: 30 s, then 43, then 50-75.
3. **A tougher chapter could not be kept up with at all.** The Mystic Ruins table is the tankiest
   (mean health multiplier 1.41 against the Forest's 1.02). The same wizard there killed 178, then
   168, then 141 a minute, sat at the trickle's cap 68% then 86% of the time, banked a flat 400 XP a
   minute and ended at level 17 - the slower he levelled, the less he killed, the slower he levelled.
   Every chapter pays the same 2 XP a kill, so the tankier ones were simply poorer.

### 10.2 XP keeps pace with the run (`Node2DGame.XpPacing.cs`)

Every point of XP is worth more the further into the run it is picked up:

```
XpPaceMultiplier = 1 + XpGrowthPerPaceMinute x PaceMinutes        // applied in Player.AddXp
XpGrowthPerPaceMinute = 0.10   ->  x1.5 at pace 5:00, x2 at 10:00, x2.5 at the boss
```

**P**, on the pace clock, because the run's own toughness is (spawn health is 6 + 10 a pace minute):
enemies that take longer to kill pay more, and a shorter run brings both on sooner together. It
freezes with the clock when the boss arrives. Applied in `Player.AddXp` after every other
multiplier rather than to the orb, so all XP is paced alike (the ghost's 50 and the bonus orb's 16 as
much as an ordinary kill's 2), the orb art keeps its three tiers (`XPOrb`), and the fraction is
carried exactly as every other bonus is (`Player.xpFraction`, section 7 finding 10).

What it does not change: income is still kills, so a player who kills more still levels sooner, and
one who falls behind still falls behind - 10.3 deals with him. The curve is unchanged
(`XpFirstLevelCost` 10, `XpCostPerLevel` 45), so every cumulative-XP figure in 3.1 still holds.

**Why a growing multiplier rather than a cheaper level:** a cheaper level makes the early run faster
and leaves the late crawl in place (the model in `xpmodel`-style replay of the before runs, step 20
and no growth: a level every 21 s in the first five minutes and every 60 s in the last five). A
multiplier that grows keeps the opening where it was and lifts the back half, which is the half that
was broken. **Why 0.10:** 0.15 was the first value, tuned on timescale-2 runs; measured properly it
ended the ordinary Pyromancer at level 37 with level 23 at 8.3 minutes. Replayed from that run's own
kills, 0.10 put level 23 at about 9.1 and the end near 34; measured, 9.9 and 31 (the replay ignores
that a build that levels slower kills a little less). It sits between the skilled and ordinary
Apotheosis targets and leaves the planned bot clear of the floor.

### 10.3 The swarm closes in (`Node2DGame.SwarmPressure.cs`)

Before this, a player whose build stopped keeping pace was protected by the arena. The live cap (140)
stopped the trickle the moment the field was full, so a field he could not clear simply stopped
growing; bodies he walked away from were recycled to a random point on the ring, so the tail never
caught him; and nothing the spawner did cared how many were already alive. A full field stops
getting worse at 131 bodies, and a player who can dodge what 131 bodies do survives it indefinitely.

**The signal: the trickle choking on the cap.** A spawn tick that wanted to send a body - not the boss
fight, not the grace after a level-up, burst budget left - and found the field full is a tick the
player did not make room for. Every half second (`SwarmSweepSeconds`, R) the share of ticks that
choked is read: after a sweep where at least `SwarmChokedShareToRise` (0.5) did, pressure climbs over
`SwarmPressureRiseSeconds` (45 s, R); after one where fewer did it drains three times as fast
(`SwarmPressureFallSeconds` 15 s). So it only builds while about three sweeps in four choke: a dense
wave that is cleared costs nothing, a field nobody clears is at full pressure in about a minute.
Ticks come every `spawnInterval`, so the share is about `exp(-kills a second x interval)` - the same
threshold asks for more kills as the run's interval shrinks, the pace clock doing its job without a
table. It reads no XP figure and no level curve, so it cannot drift when those are retuned. No
pressure builds before pace 2:00 (`SwarmPressureStartPaceSeconds`, P).

**Folding would erase its own signal** unless counted: each fold frees a slot, the next tick fills
it, and that tick would read as "not choked" - so pressure would fall the moment it began to work. A
tick that could place a body only because the swarm folded two into one counts as choked
(`swarmFoldsSinceTick`).

**What pressure P (0-1) does** - position, speed, toughness and folding, never more nodes:

| effect | at P | knob (value) | class |
|---|---|---|---|
| stragglers off screen walk faster | x(1 + 0.75 P) | `SwarmStragglerSpeedBonus` (0.75) | F |
| bodies on screen walk faster | x(1 + 0.2 P) | `SwarmNearSpeedBonus` (0.2) | F |
| the ring closes toward the screen edge | margin 1.08 -> 1.02, depth 330 -> 132 | `SwarmFullEdgeMargin` (1.02), `SwarmRingTighten` (0.6) | F |
| more of the ring ahead of his heading | bias exponent 1.8 -> 3.5 | `SwarmForwardBiasFull` (3.5) | F |
| the recycler may move more a second | 3 -> 12 | `SwarmExtraRecyclesPerSecond` (9) | F |
| bodies far behind a moving player are sent round to the front | from P 0.5 | `SwarmTrailRecyclePressure` (0.5) | F |
| bodies off screen fold into one another | from P 0.35, 1-2 folds a sweep, mass at most 6 | `SwarmSwellPressure` (0.35), `SwarmSwellsPerSweep` (2), `SwarmMaxMass` (6) | F |
| no pressure before | pace 2:00 | `SwarmPressureStartPaceSeconds` (120) | P |
| none to full / full to none | 45 s / 15 s | `SwarmPressureRiseSeconds`, `SwarmPressureFallSeconds` | R |
| a sweep counts as choked at | 0.5 of its ticks | `SwarmChokedShareToRise` | F |

A folded body (`Enemy.AbsorbIntoSwarm`) takes the other's health, mass and XP, and touches for one
more for every two bodies past the first (mass 5 touches for 3); the other leaves through
`SetTargetable(false)` without dying, so it pays nothing and drops nothing. It is drawn the same
size - `.ai/art-direction.md` allows no fractional scale, and modulate is a status channel - so what
tells it apart is that it does not die, it hurts, and its orb is bigger. Recycling keeps its mass
(`SwarmRecycleHealth`). Only `SpawnEnemy`'s non-elite bodies are eligible (`Enemy.SwarmEligible`):
never an elite, an event's body, a summoned minion or a boss, and only the plain chaser folds - a
telegraphed body draws its tell in local space against numbers its own script owns
(`.ai/enemy-behaviour.md` section 8, item 11).

**What a player who keeps up sees: nothing.** At P = 0 every value above is exactly today's
(`SwarmEdgeMargin` is `SpawnEdgeMargin`, `SwarmRecycleAllowance` is `MaxEnemyRecyclesPerSecond`,
every `SwarmSpeedScale` is 1, nothing folds), and the sweep does no work. **One change is not
pressure-gated, on purpose:** a recycled body now comes back with its type's health multiplier
(`Enemy.SpawnHealthMultiplier`); a recycled tank used to return at a skeleton's health.

**Cost.** One pass over the live enemies every half second while there is pressure to apply (or a
speed scale to put back), plus at most two folds a sweep, each a pass over the off-screen candidates.
Nothing per frame, no new nodes, the cap unchanged. `Enemy.cs` gains one factor in `EffectiveSpeed`
and a region of methods at the end of the class; everything else is in the two partial files.

### 10.4 After, measured

**The level clock**, same protocol as 10.1, XP growth 0.10, swarm on:

| run | L10 | L17 | L20 | L23 | L26 | L29 | level at 15:00 | Ascensions | mean gap, minutes 0-5 / 5-10 / 10-15 | XP a minute | most pressure |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Pyromancer, Forest | 3.7 | 6.8 | 8.3 | 9.9 | 11.5 | 13.4 | 31 | 2 | 25 / 30 / 38 s | 644 / 1,523 / 1,865 | 0.00 |
| Stormcaller, Forest | 3.7 | 6.9 | 8.7 | 10.6 | 12.1 | 14.0 | 30 | 3 (Apotheosis 11.2) | 27 / 33 / 33 s | 597 / 1,304 / 1,957 | 0.02 |
| Tituba, Forest | 3.6 | 6.7 | 8.4 | 10.4 | 12.1 | 13.8 | 31 | 2 | 27 / 30 / 33 s | 604 / 1,426 / 1,979 | 0.00 |
| Pyromancer, Mystic Ruins | 3.5 | 6.5 | 8.2 | 10.0 | 11.5 | 13.2 | 32 | 3 | 25 / 30 / 33 s | 652 / 1,515 / 2,144 | 0.00 |
| Pyromancer, `--seek-ascensions` (planned) | 3.6 | 6.4 | 8.0 | 9.2 | 10.7 | 12.2 | 35 | **4** (Apotheosis **9.2**) | 25 / 27 / 27 s | 668 / 1,716 / 2,803 | 0.01 |
| Pyromancer at the first value, 0.15 | 3.4 | 5.7 | 6.8 | 8.3 | 9.6 | 11.1 | 37 | 3 | 23 / 25 / 27 s | - | 0.00 |

- **The cadence is steady:** a level every 25-27 s in the opening, 30-33 s through the middle and
  33-38 s at the end, where it was 30 / 43 / 50-100.
- **The gates are back on target.** Level 17 at 6.5-6.9 (it was 8.3-14.1), level 23 at 9.9-10.6
  for the ordinary bot - between the skilled (8.5-10) and ordinary (11-12.5) Apotheosis targets,
  which is where a bot that walks to every orb belongs - and the planned bot took its form at 9.2,
  clear of the 7.5 floor. The planned Tituba at 0.15 on a clean timescale-2 run (24-98 fps) took it at
  9.1; at 0.10 it can only be later.
- **Ascensions:** 2-3 for the ordinary bot, 4 for the planned one - "1-2 for an average good run, up
  to 4 for a great one" (`.ai/gameplay-design.md`), with the ordinary bot's usual caveat that it
  collects better than a person and takes no boon.
- **The chapters converge.** The Ruins went from level 17 to 32, the same as the Forest: a build that
  keeps growing kills the tankier table (222 / 400 / 441 a minute, at the cap 0-1% of the time), so
  the chapter no longer starves it. Only one chapter was re-measured besides the Forest; the Cursed
  Dungeon's maze and the Sketchbook (Rockem pays 50) are the two to check next.
- **Players who keep up never meet the swarm:** most pressure 0.00-0.02 across all five, choke
  shares at most 0.4 in any minute.

**Falling behind.** The `--survivor` bot (10.5) is a far worse dodger than a person: even with 40
times a wizard's health it would die by minute 7-8 on either build, so its own deaths say nothing
about the change. Measured instead with `--survivor --immortal` - it plays the whole run and the
ledger keeps the damage it took - and replayed at the Stormcaller's real health (19, base regen 0.25
a second) for a player who takes a fraction f of what the bot took:

| Stormcaller, survivor | level at 15:00 | most pressure | folds / sent round | damage a minute, minutes 0-5 / 5-10 / 10-15 | dies at, f = 0.08 / 0.06 / 0.05 / 0.04 / 0.03 |
|---|---|---|---|---|---|
| keeps up, before | 19 | - | - | 45 / 186 / 130 | lives / lives / lives / lives / lives |
| keeps up, after | 19 | 1.00 from minute 11 | 134 / 327 | 42 / 232 / 333 | 9.6 / 10.7 / lives / lives / lives |
| one level-up in three, before | 13 | - | - | 197 / 226 / 179 | 4.5 / 6.0 / lives / lives / lives |
| one level-up in three, after | 7 | 1.00 from minute 6 | 94 / 680 | 107 / 527 / 529 | 7.3 / 8.0 / **8.3 / 8.7 / 9.7** |

- **Before, falling behind was survivable.** The weak bot's damage held flat at about 200 a minute -
  the cap was a shield - and a player good enough to take a twentieth of it lived to 15:00 with one
  level-up in three. That is the complaint.
- **After, it is not.** The same player, falling behind the same way, is at full pressure by minute
  six; from then the field folds into bodies that do not die (his kills went from about 70 a minute
  to about 4), the tail catches him and the ring is ahead of him, the damage rises to 530 a minute,
  and he dies at 8.3-9.7 minutes for every f down to 0.03.
- **The "keeps up" survivor is itself behind** - level 19 at 15:00 against 30-32 for the bots that
  keep up, because it backs off from what it should be killing - so it built pressure too, from
  minute 11, and dies for the worse dodgers (f 0.06-0.08) at 10-11 minutes. That is the design
  working, not a false positive: every bot that keeps pace with the level clock built none.

**Not measured:** the other chapters, more seeds, `--full-shop` (Sage Spiral's +60% XP on top of the
pace multiplier: the floor was already open there, section 7 finding 3), and the mortal bots on the
phone, where low fps is the norm (10.5) and a lagging phone that skips spell hits will look, to the
swarm, like a player falling behind.

### 10.5 Measuring it

`_BalanceHarness` gained what this needed (all flags combine with the pick policies):

| flag | what it is |
|---|---|
| `--survivor` | a **mortal** bot that plays the way a careful person does: steps away from what is close (about 140 px for a bramble, 170 for a wolf), sidesteps bolts, avoids hazard and blocking ground, closes on the pack when nothing is near so a short-range spell still reaches it, and walks to an orb or chest only when no enemy is within 120 px of it. It steers on physics steps, not rendered frames. The old mortal kite bot fled in a straight line, collected nothing and died at level 2 |
| `--survivor --immortal` | plays the whole run and adds a **shadow death** line: when it would have died with 3, 5, 10, 20 or 40 times the wizard's health (real damage taken, base regen only) |
| `--hp-scale=X` | the mortal survivor's max health times X - its dodging gap against a person's, stated out loud |
| `--pick-every=N` | take one level-up in N and skip the rest: the build stops keeping pace while the XP still comes |
| `--no-orbs` | with `--survivor`, never walk to XP: the kills come, the levels do not |
| `--xp-growth=X`, `--no-swarm` | override `XpGrowthPerPaceMinute` / switch the swarm off for one run, set by name so the flag is refused (not a build error) on a tree from before them |
| `--log-hits` | one line per hit taken, with what was nearest - for tuning a bot |

Every report carries a **pace ledger**, one row per game minute (`HARNESS MINUTE` lines live):
level, level-ups and their mean gap, XP banked, kills, spawns, enemies alive and within 600 px,
damage taken, the share of the minute at the trickle's cap, the share of trickle ticks that choked,
pressure, folds, bodies sent round, **clock** (physics time per minute of process time) and **frame
ms** (game time per frame - the fidelity measure). The `HARNESS SUMMARY` line gains stage, mover, end
(`died@` / `clock@`), the XP curve and growth, swarm on/off, `lvByMin`, `minClock`, `maxFrameMs`,
`maxPressure`, folds, sent-round and the shadow deaths. The swarm's numbers are read by name, so the
same harness file builds against a tree from before the swarm - which is how every "before" above was
measured with the same bot, from a detached worktree of the commit before the change.

**The fps trap, and the protocol it forced.** A weak build standing in a full field runs the game at
1-5 fps on this machine with nothing else running (130 alive, 85-113 within 600 px); a strong one that
keeps the field thin runs at 25-100, and other sessions' runs drag either into single figures. At
timescale 2, Godot's eight-physics-steps-a-frame cap then makes a rendered frame a quarter second of
game time (113 frames per 30 game seconds, against 900 at 30 fps), and nearly every spell moves and
hits in `_Process`, so it skips hits. The same baseline Pyromancer killed 186-328 a minute at minutes
4-8 that way and 419 at full fidelity, reached level 16 by minute 10 instead of 18, and ended at 20
instead of 24. The distortion is uneven - it is the weakest builds that sit in the fullest fields -
so a timescale-2 before/after flatters the after, and the first XP value (0.15) tuned on it was a
third too generous. **Use `--fixed-fps 30 --timescale=1`** (an engine flag, before the `--`): every
frame is exactly 1/30 s of game time and real-time sync is off, so the run simulates a 30 fps game
whatever the machine does; contention costs wall-clock time (12-20 minutes per 15-minute run here),
not validity. Check `frame ms` in the ledger: 33 under it, and anything over about 35 is a minute the
machine decided. Sections 3 and 9 were timescale 2 on a shared machine and carry this caveat.
