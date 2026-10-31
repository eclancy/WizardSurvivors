# The difficulty curve: chapters that get harder, damage that stays taken, Ascensions that are earned

Eric, 2026-10-30 and 31:

> "ascensions shouldn't be easy to unlock at first, and the game shouldn't be easy to beat. the first
> level can be easy, second should be harder, and then the player should reach walls that require
> strategy, farming powerups, etc."
>
> "rerolls/bans/etc should be per run, not per level up. we should rebalance the amounts that are able
> to be acquired based on that knowledge."
>
> "skip should give you 50% of your exp bar when you choose it."
>
> "remove number from health bar. player should have more health, but there should be a lot less
> sources of getting health back. the game is too easy to survive because damage is so impermanent
> right now."

(He also asked for four spell slots, then withdrew it: "let's stick with 6 spells available to
select." The book stays at six, the Blank Folio's seventh as before.)

This doc is the measurement, the target, the levers and what was built. It is the authority on **how
hard each chapter is meant to be, and how much a run lets the player heal**. `.ai/run-pacing.md` stays
the authority on *when* things happen in a run, `.ai/campaign.md` on the order, `.ai/gameplay-design.md`
on the Ascension and the Apotheosis themselves.

---

## 1. How it is measured

### 1.1 The bot, and what it is asked

`_BalanceHarness --survivor --immortal --policy=ordinary --stop-overrun=60`, `--fixed-fps 30
--timescale=1` (`.ai/run-pacing.md` 10.5: the protocol whose numbers survive a shared machine), 19
minutes so the boss fight fits, seed 1234, the Stormcaller (character 3) on every road chapter, the
Frostweaver (character 2, seed 77) on a few for the range.

- `--survivor` dodges, walks to orbs and chests when the way is clear and backs off from what is close:
  the careful player. `--policy=ordinary` takes cards as a good player who does not plan Ascensions
  does, takes every Ascension the strip shows, and opens every chest.
- `--immortal` lets it play the whole run. What it would have needed to live is the **health ledger**
  (`_BalanceHarness.NeedMultiple`, new): real health lost after armour and shields, every heal it was
  offered (the **healing ledger**, below) and its regeneration, revives, in five-second buckets - and
  the least multiple of the wizard's starting health that lives through all of it.

| column | what it is | why it matters |
|---|---|---|
| **over** | the minute swarm pressure reached 0.9 and held it a minute (`--stop-overrun`, which ends the run) | the swarm's verdict that the build fell behind for good. `.ai/run-pacing.md` 10.4: at full pressure every dodger down to a thirtieth of this bot's damage died within minutes. **An overrun is a lost chapter.** |
| **siege** | minutes the field sat at the trickle's cap at least half the time | how long the player was under siege: a chapter cleared after a nine-minute siege is not an easy one |
| **boss** | when the boss fell (the fight starts at 15:00) | the other way a chapter is lost |
| **need** | the health multiple the bot needed | **a ranking, not a verdict**: the bot dodges far worse than a person (10.4 put a careful player at a twentieth of its damage or less), so it compares chapters and saves on one bot, never says what a person survives |
| **L15** | level at 15:00 | the build the boss meets |
| **asc** | Ascensions taken / when the first was offered | Eric's other half |
| **heal, regen, lost** | HP a minute offered by heals, by regeneration, and lost | the healing audit (section 5) |

### 1.2 Two saves: fresh and farmed

- **Fresh** (`--fresh-save`): starters only, nothing bought. It now also records the bosses of the
  chapters before `--stage` as beaten - the kill and the opened chapter, nothing they pay - because no
  save stands in a chapter without them, and the Ascension's teaching (section 6) reads that record.
- **Farmed** (`--farmed`, new): the player who has farmed sensibly up to this chapter. Every chapter
  before it beaten; every Feat done; every Road deed and secret of those chapters found, so their spells
  and boons are in the pool; and Arcane Energy spent on Powers - **300 + 650 for each chapter behind**
  (one clear and two failures of each: a clear pays about 200 and its boss's bonus, a failure at minute
  eight about 130, `Node2DGame.CalculateArcaneReward`), spent on the cheapest next level among damage,
  Vitality, Emberheart, cooldowns, attack speed, area, projectiles, move speed, pickup, Sage Spiral,
  Phoenix Oath and duration, again and again. `--arcane=N` overrides the budget. At the Broken Circle
  that is 1,600 Arcane (damage 3, Vitality 4, Emberheart 3, cooldowns 2, attack speed 2, area 3,
  projectiles 1, move speed 5, pickup 3, Sage Spiral 3, duration 3) and 15 spells and 3 boons from
  deeds; at the Emberdeep 6,150.

  All Feats from the first chapter on is generous - the five Adepts each want four of an element - and
  it makes the farmed column the strong end of "farmed sensibly". The walls are tuned to it, so a
  player who farmed less meets them harder.

### 1.3 What it costs, and what was skipped

A run is 4-10 minutes of wall clock at three in parallel, longer while a field sits at the cap. **Not
measured:** the other four wizards, a second seed on most cells, the planned and default policies, the
castle (not playable), the Sketchbook, the phone. Read every cell as a direction: two runs of one
cell differ by a level or two and a minute of siege.

---

## 2. Before: the curve as it was

First measured on `main-ff86ni` at 92e9063 (the harness changes only; six slots; the Ascension at
player level 17 with no meta gate), Stormcaller, seed 1234. **Being re-measured on 6352ebe** (the
Gleaning Bell and Spiritual Weapon merged since), with the healing ledger: the first cells already
differ - a fresh Forest with no siege at level 36, a fresh Circle overrun at 11.5 - so section 10
compares against that re-measure, not this table.

| # | chapter | fresh: over | siege | boss | need | L15 | asc (offered) | farmed: over | siege | boss | need | L15 | asc (offered) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Enchanted Forest | - | **9** | 16.5 | x32 | 24 | 1 (14.5) | - | 5 | 15.3 | x43 | 28 | 2 (12.7) |
| 2 | The Fallow Fields | - | **9** | 18.7 | x44 | 22 | 1 (13.0) | - | 0 | 15.3 | x0.6 | 32 | 3 (7.1) |
| 3 | The Broken Circle | - | 7 | 15.6 | x36 | 24 | 1 (12.0) | - | 0 | 15.0 | x0.1 | 35 | 3 (7.7) |
| 4 | Sunken Cave | - | 2 | 15.2 | x8 | 28 | 1 (9.5) | - | 1 | 15.1 | x0.3 | 38 | 3 (8.9) |
| 5 | Cursed Dungeon | - | 4 | 15.1 | x4 | 28 | 2 (10.7) | - | 0 | 15.4 | x0.2 | 36 | 3 (8.9) |
| 6 | Blighted Swamp | - | 7 | 16.5 | x173 | 25 | 1 (15.5) | - | 0 | 15.3 | x0.1 | 33 | 3 (6.3) |
| 10 | The Emberdeep | **11.0** | 7 | - | x49 | (16) | 0 | - | 0 | 15.0 | x0.1 | 35 | 4 (8.0) |

### 2.1 What it says

1. **The curve was flat by chapter and steep by save.** A fresh save sat under a seven-to-nine-minute
   siege in the first three chapters - **the Forest was among the hardest of them** - and had an easier
   time in the Cave (2) and the Dungeon (4). Only the Emberdeep overran it. Eric's chapter 1 was not
   easy for a new player; his chapter 3 was no wall.
2. **A farmed save trivialised everything from chapter 2 on.** No siege, need under one bar, every boss
   dead within seconds of arriving - the Ninth Stone, the Hollow Choir and the Warden of the Deep in
   under five. Boss health was set for a build that no longer arrives: a farmed build's single-target
   damage at 15:00 deletes 11,000 HP before its telegraph finishes.
3. **The farmed Forest is the fresh Forest** (300 Arcane, no deeds behind it): the column only starts to
   mean "farmed" at chapter 2.
4. **Ascensions came to everyone**: one a run on a fresh save from the first chapter, three or four on a
   farmed one, the first offered at minute 6-9.
5. **The need column is enormous on fresh saves** - x30-x170 of a 19-HP wizard - and almost all of it
   is the siege, minutes 3-13, when a field of 130 bodies stands round a build that cannot clear it.
   Section 5 says what healed it back.

---

## 3. The target

| # | chapter | fresh save | farmed save |
|---|---|---|---|
| 1 | Enchanted Forest | **easy**: no overrun, siege 3 minutes or less, boss down inside 1.5 | trivial |
| 2 | The Fallow Fields | **harder**: no overrun, a siege of 4-8 minutes, boss down inside 3 | easy |
| 3 | The Broken Circle | **the first wall**: overrun, or the boss not beaten | passes: no overrun, siege 5 or less, boss inside 3 |
| 4-9 | Cave to the Sands | walls, overrun sooner the further in | passes, and works for it: siege up to 6, pressure under 0.8, boss 1-3 minutes |
| 10 | The Emberdeep | overrun early | the hardest pass: the most siege and pressure of any chapter, the longest boss |

| who | Ascensions |
|---|---|
| a new save, chapters 1 and 2 | **none** - not yet learned (section 6) |
| once learned, an average good run | 1-2 (`.ai/gameplay-design.md`, "How many Ascensions a run gets") |
| a planned run on a farmed save | up to 4 |

**What "passes" means for a person.** A farmed save that keeps the field clear is never overrun, so a
person who dodges as most do lives; a fresh save overrun at minute nine dies whatever he does. Between
the two the human margin decides - with less healing (section 5) a larger part of it - and that is the
playtest (section 11).

---

## 4. The levers

### 4.1 Per chapter: `ChapterDifficultyCatalog` (new, `scripts/ChapterDifficulty.cs`)

One table, one row per chapter in campaign order, every value a multiplier on the game as it was
tuned (1 is unchanged). It is its own file rather than fields on `StageCatalog`: the difficulty is one
concern read in one place (`Node2DGame.ApplyChapterDifficulty`, after the balance preset, so the two
multiply), and the castle chapters' builders edit `StageCatalog` and `StageBestiary.*` in parallel.
Boss health stays where it lives, in `BossCatalog`.

| lever | what it scales | read at |
|---|---|---|
| `EnemyHealth` | ordinary spawns' health, on top of the pace curve and each creature's own multiplier; elites are built from a spawn, so they carry it too | `UpdateSpawnScaling` (`presetSpawnHealthScale`) |
| `EnemyDamage` | every hit an enemy lands - contact, bolts, slams, the boss's attacks - rounded stochastically as Weaken is, so 1.25 makes a quarter of the one-damage touches land for two. Never below 1: under 1 a touch could roll nothing and read as a dodge | `Enemy.ScaleOutgoingDamage` |
| `EliteInterval` | the gap between elites (under 1 is more often) | `GetEliteIntervalSeconds` |
| `ElitePower` | elites' and Wardens' health on top of the above | the elite and Warden spawns |
| boss health | `BossDefinition.Health` | `BossCatalog` |

**Why health is the main lever.** The overrun is the swarm's answer to a build that cannot clear the
field, so the spawns' health is what decides whether a save keeps up - and a wall is exactly a chapter
a fresh save cannot keep up in and a farmed one can. Damage is the second lever: it decides what the
survivors of a siege carry out of it, which section 5 makes permanent. Spawn pressure (the burst
window, the interval) is **not** used: it is the XP ceiling (`.ai/run-pacing.md` 3.3), so raising it
would pay the player for the difficulty it added. The XP curve is not moved for the same reason.

### 4.2 The proposal (first values; section 10 has what shipped)

| # | chapter | EnemyHealth | EnemyDamage | EliteInterval | ElitePower | boss health (was) |
|---|---|---|---|---|---|---|
| 1 | Enchanted Forest | 0.85 | 1.0 | 1.15 | 0.9 | 3,800 (4,200) |
| 2 | The Fallow Fields | 1.0 | 1.0 | 1.0 | 1.0 | 6,500 (4,500) |
| 3 | The Broken Circle | 1.4 | 1.1 | 0.9 | 1.25 | 16,000 (5,000) |
| 4 | Sunken Cave | 1.9 | 1.1 | 0.9 | 1.3 | 3 x 6,000 (3 x 1,900) |
| 5 | Cursed Dungeon | 1.7 | 1.15 | 0.85 | 1.35 | 20,000 (6,200) |
| 6 | Blighted Swamp | 1.9 | 1.15 | 0.85 | 1.4 | 22,000 (6,800) |
| 7 | Mystic Ruins | 1.9 | 1.2 | 0.8 | 1.45 | 24,000 (7,200) |
| 8 | Frozen Waste | 2.3 | 1.25 | 0.8 | 1.5 | 26,000 (7,800) |
| 9 | Scorched Sands | 2.5 | 1.25 | 0.8 | 1.55 | 28,000 (8,200) |
| 10 | The Emberdeep | 2.3 | 1.35 | 0.75 | 1.65 | 36,000 (11,000) |

The tables are not flat underneath (the Ruins' mean health multiplier is 1.41, the Cave's 1.00, the
Dungeon's 1.35 - `.ai/campaign.md`), so the column is not monotonic: it is set so the *outcome* climbs,
and `RegressionChecks.ValidateChapterDifficulty` holds the outcome's proxy - EnemyHealth times the
table mean - to a road that never falls. Boss health roughly triples from the Circle on: a farmed
build's single-target damage at 15:00 is about 1,000 a second, and a wall's boss should take one to three
minutes of it while it hits back. The castle chapters inherit the Emberdeep's row until their builders
tune them; the Sketchbook, outside the campaign, plays at 1.

**The Boss Rush** reads `BossCatalog` health (scaled by `BossRushHealthScale`), so its bosses grow with
these numbers. Not re-tuned here; it is a playtest item.

---

## 5. Healing: damage that stays taken

### 5.1 The audit: every way a run gives health back

Measured with the healing ledger (`GameStats.TrackHealing`, harness only: every `Player.Heal` call
named by its caller, as **offered** - before the bar caps it - plus every regen line integrated,
`Player.RegenBreakdown`), Stormcaller, per minute of play. Lines the Stormcaller never met are worked
from the code at a typical holding.

| source | where | rule | HP a minute, typical | now |
|---|---|---|---|---|
| base regeneration | `Player.BaseHealthRegenPerSecond` | 0.25 a second, every run | **15**, always | **removed** (0) |
| Emberheart (shop `recovery`) | `ApplyArcaneUpgrades` | +0.4 a second a level, 8 levels | **72** farmed at the Circle (3 levels), **168** at the Emberdeep (7) | **+0.05 a level** (3 a minute a level), re-priced 15 + 10 a level and refunded |
| Grass tier | `GetGrassRegenPerSecond` | 1 / 2 / 4 a second at tier 2 / 4 / 6 | 60 / 120 / 240 | **0.1 / 0.2 / 0.4** (6 / 12 / 24) |
| Light tier | `NotifySpellDamageDealt` | 3 / 6 / 10% of every point of spell damage dealt | at 20,000-40,000 damage a minute, 600-4,000 offered: the bar refills as fast as it empties | **0.5 / 1 / 1.5%**, and **at most 1 HP a second** |
| health flask | `HealthPickup`, `DropTable` | 10 HP, a rare drop | **9-19** | **8 HP**, half as often |
| Essence Chalice relic | `OnChestEnemyKilled` | 1 HP a kill, first 50 kills | 50 a run | **first 15 kills** |
| Life Drain set | `OnChestEnemyKilled` | 1 HP a kill, unlimited | **80** in the Forest run that held it | **1 HP every 15th kill** (about 15) |
| Heart of Renewal relic | `chestRegenPerSecond` | 0.8 a second | 48 while held (11 averaged over its run) | **0.15** (9) |
| the 0.5-regen chest line (`health_regen_0_5`) | `ApplyChestItem` | 0.5 a second | 30 | **0.1** (6) |
| Mossgrown regen boon (`BoonEffect.Regen`) | `BoonCatalog` | 0.6 a second | 36 | **0.15** (9) |
| opening a chest | `ChestReward`, `Node2DGame` | 4 HP | **5** | **removed** |
| Vaultguard set | `OnChestOpenedVaultguard` | shield and heal per chest | under 5 | heal removed, shield kept |
| Spiritual Weapon's Soul Siphon | `SpiritualWeapon.Siphon` | up to 3 a second | up to 180 | **1 a second** |
| Black Tentacles' wisps | `BlackTentacles.TearWisp` | 1 HP a wisp | spell-bound, small | unchanged |
| Taproot stance | `GetStanceRegenPerSecond` | while rooted | small | unchanged |
| Marginalia | `TakeMarginalia` | 30% of max HP, only into an otherwise empty hand | rare | unchanged: a dead pick's consolation |
| the grove, the first tree, the Court's fowl | side events and secrets | one heal each | once a run at most | unchanged |
| Phoenix Oath revives | `TakeDamage` | half the bar, per revive | - | unchanged |
| **the Familiar's Purr** | `familiarGrants.RegenPerSecond` | the cat's regen | not met by this bot | **left to the Familiar agent** (being reworked into a periodic heal) |

The Gleaning Bell pays XP, not health.

**Before, a fresh Forest run was offered 130 HP a minute against 43 lost** - three times what it took -
and a farmed Emberdeep run 200 against nothing. That is "damage is so impermanent": a 19-HP wizard was
refilled every ten seconds.

### 5.2 More health, and almost none back

- **`Player.MaxHP` 20 to 50** (every wizard's modifier still applies: the Stormcaller is 48). Vitality
  stays +5 a level - a smaller share of a bigger bar, which is the point.
- **No base regeneration.** What heals is now something the player took: Grass, Light, a relic, a boon,
  Emberheart, a flask - each cut to a fraction (table above), so a build that wants sustain spends picks
  and slots on it, and even then gets a trickle.
- **The health bar shows no number** (`Node2DGame.BuildPlayerHealthHud`): the bar and the shield strip
  only, on the phone and the PC alike, which share the one HUD. Nothing else read the label.

How much damage a chapter deals is now the second half of its difficulty, and `EnemyDamage` (4.1) is
the lever for it.

---

## 6. The Ascension is learned: the Hayward teaches it

### 6.1 The four ways it could have been made hard at first

| option | what it does | verdict |
|---|---|---|
| **later in the run** (a pace-clock gate) | no Ascension before pace minute N, on every save | **No.** It is not "at first": it moves every Ascension of every save, the expert's too, and lands it on the Apotheosis (level 23, about minute 10) - the "seconds apart" problem the two gates were set to solve (`.ai/gameplay-design.md`, "The Apotheosis"). |
| **stricter element tiers on a fresh save** | 6 tags instead of 4 until something | **No.** What lifts it is meta progress, so it is the next row with a worse explanation: the browser would say "Requires 6 Fire" on one save and 4 on another and never say why - and six of one element on a starters-only pool is out of reach for some wizards, a lock nobody is told about. |
| **a meta unlock over the campaign** | a chapter's boss teaches it | **Chosen.** Legible ("Learned by defeating the Hayward"), uses the campaign as the teacher, costs nothing in a run once learned, and it is literally "at first". |
| **rarer offers** | the strip shows an earned Ascension only some of the time | **No.** It puts the roll back in charge of the timing, which the player-level gate was built to stop, and it is the "artificial roof" the Ascension target forbids. |

### 6.2 The rule

**A new save cannot ascend until it has beaten the Hayward**, the boss of the Fallow Fields (chapter
2). Until then a spell stops at level 7, as it always has before player level 17, and the ascension
browser lists every path with one more requirement - **Learned by defeating the Hayward** - beside the
spell level, the tags and the player level. The Apotheosis needs its spell's Ascension path, so it is
sealed with it.

- **Why the Hayward.** The first two chapters are learned on the base game - level-ups, element tiers,
  the level 4 mutation - and the Forest is where a new player dies first, so Elderbark teaching it would
  be teaching it at once. The Ninth Stone (chapter 3) would make the first wall a wall without the tool
  the walls are built around. The Hayward closes the "clearly harder" chapter and hands over what the
  walls ask the player to plan for: one spell to 7, one element to 4.
- **What reads it.** `GlobalStatsManager.HasLearnedAscension(save)`: the save carries
  `AscensionLearned`, or **the chapter the Hayward opens is earned** (`IsStageEarned` on
  `StageCatalog.ChapterOpenedBy("stage_9")`: the Hayward beaten, the Broken Circle written open, or any
  chapter past it opened). Derived rather than written at the kill, so a save that came past the Fields
  before the Fields existed is already learned, as is every save at the Cave or beyond.
- **Where it is enforced.** `Player.GetAscensionOffers` (no level 8 offer while unlearned) and
  `Player.BuildAscensionPreview` (`AscensionRequirementKind.Learned`). Read once at the start of a run;
  the Hayward's fall ends the run it falls in.
- **The kill says so.** The results screen lists the Ascension as taught the first time, and the
  Hayward's deed reads "Unlocks Thornseed and teaches Ascension".

---

## 7. Rerolls and bans are per run

**Before:** Fate Fracture (`rerolls`) gave +1 reroll **at every level-up**, refilled each time - five at
its cap, about 160 a run, which is no limit at all; the `--seek-ascensions` bot spent 30-45 a run.
Proscription (`bans`) was already per run, +1 a level to 3.

**Now both are a pool for the run** (`Player.RerollsRemaining`, `Player.BansRemaining`), filled when the
run starts and never refilled:

| Power | per level | cap | pool at cap | price (base + per level) | was |
|---|---|---|---|---|---|
| Fate Fracture | **+2 rerolls per run** | 5 | 10 | **40 + 25** | +1 per level-up, 75 + 50 |
| Proscription | +1 ban per run | 3 | 3 | 90 + 70 | unchanged |

Ten rerolls a run makes a reroll a decision, as a ban already was; the price comes down because the
level buys less. Playtest mode adds two rerolls a run (it added one a level-up). Nothing in a run adds
charges. **Only the counts moved**: what a ban does on screen is another agent's change, and the two
meet only in `Player.BansRemaining`. Every Fate Fracture level already bought is re-priced and the
difference paid back (section 9).

---

## 8. Skip pays half a bar

The Skip button now pays **half of the XP bar the player has just started** - 50% of
`CalculateXPForLevel(CurrentLevel)` after the level-up - straight into the bar, not through the XP
multipliers (it is a share of the bar, not XP). If that fills the bar, the next level-up opens at once.
The button says so: **Skip (+50% XP)**.

Skipping is still a loss in picks - one pick for half a level - so it is the answer to a hand where
every card is wrong, never a farm. It moves the player level (and so the level 17 and 23 gates) half a
level sooner per skip, which no measured policy uses: the ordinary bot never skips. `--pick-every=N`
and the harness's dead-level-up skip now gain the XP too, so `.ai/run-pacing.md` 10.4's falling-behind
rows read slightly kinder if re-run.

---

## 9. The save: schema 21

**Schema 20 is reserved** for the Cursed Dungeon's move into the castle (`.ai/endgame-castle.md`), so
this is **21**: one field and one step, `MigrateToLearnedAscensionsAndRepricedPowers`.

1. **`AscensionLearned`**, set for every save with a run on record (`Lifetime.TotalRuns`, or the
   telemetry ring). Every such save had the Ascension in every run it played; taking it away until the
   Hayward falls would be the silent confiscation CLAUDE.md forbids. A save with no run had nothing, and
   starts sealed like a new one.
2. **Re-priced Powers are paid back** (`SaveData.RepricedPowers`, frozen): every level bought of Fate
   Fracture and of Emberheart (section 5) is refunded the difference between what it cost and what it
   costs now. The levels stay; what each pays is the new rule.

If the Dungeon's 20 lands after this, its step must still run on a save already at 21: the integrator
orders the two steps, or 20 renumbers past 21. Held by `RegressionChecks.ValidateDifficultyMigration`
on sample saves.

---

## 10. After

PENDING-AFTER

---

## 11. What needs a human playtest

PENDING-PLAYTEST
