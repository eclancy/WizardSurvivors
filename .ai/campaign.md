# The campaign: its order, two early chapters, and the Dungeon in the middle

Eric, 2026-10-24: *"cursed dungeon should be a later level. there should be another few levels that
open up earlier."* Asked to clarify, he chose to **build new chapters that open early** and to move
**the Cursed Dungeon to the middle of the campaign**, after the open-field chapters - a maze is a
harder ask than an open field.

This doc is the design and the plan. It was written in two phases, like `.ai/bestiary.md`:

- **Phase 1 (branch `campaign/new-chapters`):** the new order, the plumbing that makes an order a
  thing separate from stage ids, the schema 13 migration, and the two new chapters scaffolded with
  `IsPlayable = false` and stand-ins for everything. The order is **live** in Phase 1: a chapter
  that is not playable is skipped, so the Forest opens the Cave until the new chapters exist.
- **Phase 2 (to do):** build each new chapter - its ground, its cast, its boss, its side event, its
  music - in its own files, in parallel (section 7). Flipping `IsPlayable` is the last step of each.

Read with `.ai/world-and-tone.md` (why each place is held), `.ai/bestiary.md` (the cast recipe),
`.ai/enemy-behaviour.md` (the boss hook model), `.ai/side-events.md` and `.ai/audio-direction.md`.

---

## 1. The order

**Two new chapters, not three.** It puts the Dungeon fifth of ten - the middle - after four open
chapters, and two is a Phase 2 that two agents can build side by side. A third early chapter fits
between the Broken Circle and the Cave later with no plumbing change: a `StageCatalog` entry and one
line in the order (section 9, decision 1).

| # | Chapter | id | Boss | What the boss asks | Table mean (health x) | Boss health | Beating it opens | Boss pays |
|---|---|---|---|---|---|---|---|---|
| 1 | Enchanted Forest | `stage_0` | Elderbark | sustain damage | 1.02 | 4200 | the Fallow Fields * | Thorn Vine |
| 2 | **The Fallow Fields** | `stage_9` | **the Hayward** | **go round it** | 1.07 | 4500 | the Broken Circle * | Thornseed (boon) |
| 3 | **The Broken Circle** | `stage_10` | **the Ninth Stone** | **find the way out** | 1.06, 1.11 from 5:00 | 5000 | the Sunken Cave | Shadegrease (boon) |
| 4 | Sunken Cave | `stage_2` | the Hollow Choir | spread damage | 1.00, 1.18 from 5:00 | 3 x 1900 | the Cursed Dungeon | Riptide |
| 5 | **Cursed Dungeon** (moved from 2) | `stage_1` | the Gaoler | sidestep a lane - in a maze | 1.35 | 5200 | the Blighted Swamp | Shadow Bolt |
| 6 | Blighted Swamp | `stage_3` | Mother Rot | kill what heals her | 1.27 | 6800 | the Mystic Ruins | Mirefoot |
| 7 | Mystic Ruins | `stage_4` | the Archivist | orbit, re-acquire | 1.41 | 7200 | the Frozen Waste | Spiritual Weapon |
| 8 | Frozen Waste | `stage_5` | the Still Warden | hold ground | 1.13 | 7800 | the Scorched Sands | Rimebriar (boon) |
| 9 | Scorched Sands | `stage_6` | the Long Coil | burst a window | 1.09 | 8200 | - | Sunsteel Filament (boon) |
| 10 | The Emberdeep | `stage_7` | the Warden of the Deep | all of it, in sequence | 1.28 | 11000 | - | Fireball |
| - | The Sketchbook | `stage_8` | Wizard Dude | leave the wedge | (the kids') | 4600 | - | 400 Arcane Energy |

\* While the two new chapters are not playable, Elderbark opens the Sunken Cave.

The Emberdeep is still gated on **every spell recovered and every wizard freed**, not on a boss,
and is still last. The Sketchbook is still outside the campaign: open from the first run, in no
order, gating nothing. The table mean is the spawn table's shares times their health multipliers
(the Forest's wolves and wisps included), the measure `.ai/run-pacing.md` section 10.1 uses.

## 2. What moving the Dungeon does

**To the difficulty curve.** The Cursed Dungeon was the second chapter and the second-toughest spawn
table in the game (1.35, after the Ruins' 1.41), on the one maze. Fifth, it is the first step up
after four chapters whose tables sit at 1.00-1.18, and the maze is now the mid-campaign test it reads
as. The early curve is gentle by construction: Forest 1.02, Fields 1.07, Circle 1.06 rising to 1.11
at pace 5:00, Cave 1.00 rising to 1.18. Past the Dungeon nothing changed: Swamp 1.27, Ruins 1.41,
then the two fast chapters (Frozen 1.13 and Sands 1.09, whose difficulty is runners - Fast shares of
0.36 and 0.40 - not health) and the Emberdeep 1.28. That late-campaign dip in health was already
there; it is not this change's.

**To the bosses' health.** Boss health climbed with the old order (4200, 5200, 5700, 6800, ...).
The Gaoler's 5200 now follows the Choir's 5700, so it is the one dip in an otherwise rising line.
**Left as it is on purpose:** the maze is the step up - a lane charge in a corridor is harder to
sidestep than one on open ground - and retuning it is a balance call (section 9, decision 3; the
recommendation is 6200 and an Arcane bonus of 200, between the Choir's and Mother Rot's, measured
with `_BossProbe` and the balance bot before it ships).

**To which boss gates what.** Elderbark opens the Fields; the Hayward the Circle; the Ninth Stone the
Cave; **the Choir opens the Dungeon** (it opened the Swamp); **the Gaoler opens the Swamp** (it opened
the Cave); Mother Rot, the Archivist and the Still Warden as before; the Long Coil opens nothing,
because the finale is the campaign gate.

**To what the player is handed, and when.** The rewards travel with their chapters:

- **Tituba** is freed in the Dungeon and is secret until it opens (`UnlockCatalog`, `secretUntilStage:
  "stage_1"`). She is now revealed and freed at chapter 5 instead of 2. Nothing to change - the reveal
  is derived from the chapter opening - but it is the largest consequence of the move: the first
  rescued wizard arrives three chapters later. The Fields and the Circle hand over two boons and two
  found relics in that time (section 5, 6).
- **Shadow Bolt** (the Gaoler's deed) arrives later; **Riptide** (the Choir's) arrives earlier.
- The Arcane Energy bonuses (150, 160, 175, 190, 170, 210 ...) dip at the Gaoler for the reason his
  health does; same decision.

**What did not move:** every stage id and index, every save key, every unlock rule's chapter, every
harness flag. `--stage=1` is the Cursed Dungeon wherever it stands.

---

## 3. How the order is held

**`StageCatalog.CampaignOrder` is the only place the order is written** - a list of stage ids. It is
deliberately not the roster's order: a stage's id and index are in every save (`UnlockedStageIds`,
`BestTimeByStage`, the run log), in the unlock rules (Tituba's and Vainamoinen's reveal chapters) and
in every harness flag, so a chapter is never renumbered to move it. New chapters append to the
roster (`stage_9`, `stage_10`) and take their place through the order.

Everything that used to encode the order now reads it:

| What | Where | Was |
|---|---|---|
| which chapter a boss victory opens | `StageCatalog.ChapterOpenedBy`, `BossCatalog.ChapterOpenedBy`; written by `Node2DGame.ApplyBossVictoryUnlocks`, shown by `GameOverScreen` | `BossDefinition.UnlocksStageId`, one literal per boss (removed) |
| which chapter's boss opens this one | `StageCatalog.PreviousChapter` | implied by the literals above |
| the locked hint | `StageDefinition.LockedHint`, derived (`StageCatalog.LockedHintFor`) | a literal per chapter ("clear the Cursed Dungeon") |
| the stage select's order | `StageCatalog.InDisplayOrder` (campaign, then the chapters outside it) | roster order, so the Dungeon was second |
| the deeds page's order | `AchievementDefinitions.All` puts the chapter clears in campaign order in their own slots | written order |
| the final chapter | `StageCatalog.FinalChapter` (last in the order, the one `CampaignComplete` gate) | implied |

**Not playable means skipped.** `PreviousChapter` and `ChapterOpenedBy` step over a chapter whose
`IsPlayable` is false, so the order is live before the new chapters exist: Elderbark opens the Cave
today, and the day the Fields flip, Elderbark opens the Fields and the Cave's gate becomes the Ninth
Stone - with no edit anywhere but the flag.

**Stage index is not campaign position.** `Global.SelectedStageIdx` is a stage index (the number in
the id) and always was; the stage select now hands it `stage.Index` rather than the card's place in
the list. The `Mathf.Clamp(Global.SelectedStageIdx, 0, 9)` in eight places in `Node2DGame` is gone -
`StageCatalog.GetByIndex` clamps to the roster it actually has, and the old clamp read `stage_10` as
`stage_9`. The run's `StageId` comes from the catalog rather than being formatted from the index.
`--stage=N` on `_BalanceHarness`, `_BossProbe`, `_UiShot`, `_ExploreProbe`, `_MazeProbe` and
`_PadProbe` is the stage index: `--stage=9` is the Fallow Fields, `--stage=1` the Dungeon.

**Deeds for a chapter not built yet are not listed** (`AchievementDefinitions.Listed`, read by the
main menu and the pause screen), so nobody sees "Defeat the boss of the Fallow Fields" before the
Fields exist. They are still evaluated, which is harmless: the boss cannot be fought.

### The gate (`GlobalStatsManager.IsStageEarned`)

A boss-gated chapter is earned by any one of three things, and each only ever adds:

1. **Its id is in the save's opened list** - what a victory writes, and what every older save holds.
2. **The save has opened a chapter later in the campaign.** The campaign is one road; being past a
   chapter is having been through it. This is what keeps the order free to change, and it is how the
   two new chapters reach saves that are already past them: the moment they are playable they are
   open to anyone at the Cave or beyond - **no migration is needed when Phase 2 flips them**, and no
   one already in the campaign can be stopped by a chapter added behind them.
3. **The previous playable chapter's boss is in the lifetime record** (`LifetimeStats.BossesDefeated`,
   kept since schema 8). A reorder re-derives what a recorded kill opens instead of trusting what the
   old order wrote down.

`IsStageAvailable` is `IsPlayable && IsStageEarned`. The finale stays `CampaignComplete` (every spell
recovered and every wizard freed, live); the Sketchbook stays `Open`.

### Stand-ins, so Phase 2 creates files rather than editing shared ones

The bestiary's pattern (`.ai/bestiary.md` section 7), carried to the rest of a chapter:

| Piece | Planned as | Plays until it exists |
|---|---|---|
| each creature | `StageBestiary.FallowFields` / `.BrokenCircle`, `Planned(...)` at `scenes/Fields*.tscn`, `scenes/Heath*.tscn` | the Cursed Dungeon's creature for the role |
| the boss | `BossDefinition.ScenePath` = `scenes/HaywardBoss.tscn`, `scenes/NinthStoneBoss.tscn`; `StandInScenePath` | Elderbark, under the new boss's id and name (so its deed and unlock are the chapter's) |
| the music | `MusicCatalog.Fields` / `.Heath`, planned sets with a stand-in | the Forest's set / the Ruins' set, until `fields_bed.wav` / `heath_bed.wav` exist |
| the ground | a stand-in block each in `LevelGenerator`, `GroundDetail`, `StageEnvironmentCatalog`, and a `case` in `Node2DGame.BuildDecorProps` | the Forest's turf / the Ruins' stone |
| the side event | a pre-placed `"stage_9" => null` / `"stage_10" => null` line in `SideEventDirector.ForStage` | no event |
| the rewards | the deeds and the four boon sources are already in `AchievementDefinitions` and `UnlockCatalog` | - |

A chapter's look (haze, glow, weather, intro tint, water style, hazards, landmarks) moved from five
switches in `Node2DGame` onto `StageEnvironmentProfile`, with exactly the old values, so a chapter's
whole look is one block of its own.

### The save guard (`SaveManager.IsWriteProtectedLaunch`)

Added in Phase 1 after the developer's real save was rewritten to schema 13 by tool runs on unshipped
branches (section 9, decision 6). **A launch whose scene is a tool scene - any `scenes/_*.tscn` - or
that is passed `-- --no-save-writes` never writes `user://savegame.json`**, decided before the load,
so the migration's write-back on load is covered too (a tool's own `_Ready` was always too late for
it). The game itself, launched with no scene argument, is the only writer. Every Phase 2 run of
`MainMenu.tscn` on these branches passes `-- --no-save-writes`.

---

## 4. The save: schema 13

`SaveData.CurrentSchemaVersion` is 13; the step is `MigrateToCampaignOrder`.

**Nobody loses anything.** No id is removed from `UnlockedStageIds`, so every chapter a save could
enter, it can still enter - including the Dungeon for a save that had it as chapter two.

**What a save has beaten opens what it opens now.** The step finds every boss the save has beaten -
from the lifetime record, from the boss-clear deeds, and from the chapters the old order opened (a
victory was the only thing that ever wrote one; `OpenedBySchema12` is that table, frozen) - and opens
what each boss opens under the new order. A save that had beaten the Gaoler had the Cave; now it has
the Swamp too, because the Gaoler opens the Swamp. Rule 2 of the gate then opens everything before
the furthest chapter reached, which is how a save that only had the Dungeon also gets the Cave.

**The new early chapters** are granted by rule 2, live: any save at the Cave or past it has earned
both, and has them the day each becomes playable. A save at the Forest only meets them in order.

**Boons moved out of the shop's dead stock.** Thornseed and Shadegrease (the two bosses' deeds) and
Wishing Coin and Gilded Mote (the two side events) were `PurchaseBoon` entries no screen sells, so no
new save could ever be dealt them. A save that owns them from the schema-9 payback keeps them. That
payback paid "the cheapest purchasable boons", read live, which would have shrunk from seven to three
when four left the shop - so it now reads a frozen copy of the list as it stood at schema 9.

**Why boons and not spells or wizards** for the new chapters: the finale's gate counts every spell and
every wizard, live. A new spell or wizard in a chapter would close the Emberdeep to every save that had
opened it until they went back for it - schemas 10, 12 and the other branch's 13 each needed a grant
for exactly that. A boon is outside the gate, so the chapters can be inserted in front of a finished
campaign without touching it.

**Checked** by `RegressionChecks.ValidateCampaignMigration` on six sample schema-12 saves (beat
Elderbark; Elderbark and the Gaoler; everything to the Still Warden; the Gaoler known only from the
lifetime record; only from his deed; a save with a gap in the old order like the developer's own) -
every stored chapter still open, the right new ones open, nothing past them open, the early chapters
earned by every save past them, and a second migration changing nothing - and on a save with every
spell and wizard, which must still open the finale.

**Schema 13 is also claimed by `spells/star-rain`** (an uncommitted Starfall grant in
`ws-spells-star-rain`). Whichever lands second must renumber to 14 and keep both steps (decision 5).

---

## 5. The Fallow Fields (`stage_9`, chapter 2)

**The fiction.** *"He takes a tithe of everything that grows here."* The farms beyond the wood: the
people of the land, who are what `linen` means in the palette (`.ai/world-and-tone.md`), taken while
they worked. Fields gone to stubble and thistle between black hedgerows, a mill-pond, a well. It is
the first place the player sees that was someone's livelihood rather than someone's wilderness, which
is why it comes straight after the wood.

**Whose prison: a relic, not a person.** The well holds the coins the village threw into it - the
**Wishing Coin** (*"Thrown into still water by someone who is not coming back"* - the boon's own line
was already this chapter's). A new wizard is out of scope for a chapter build: it is a 48-cell
sprite, a signature spell on an element no other wizard opens on, and an Apotheosis only if an unused
verb exists - and it would join the finale's gate (section 4). Who the Fields might hold as a person
is decision 2.

**Ground.** A stubble floor (gold and linen from the deep half - a harvested field under no light),
furrows of bare ploughed earth, hedge-bank drifts, thistle and poppy detail, mill-ponds (water: the
one blocker). Hedgerows are bushes laid in long loose drifts; a few field trees stand alone; haycocks
and fence rails are props. **No hazards** - an open natural chapter is shaped by its enemies
(`StageEnvironmentProfile.HazardSpikes` stays false). Landmarks yes: a flower pen belongs in a field.
Bolt element: **earth** (`#C99A5E` body, `#FFF0D8` core) - warm on a cold dark field, and unused by
any other chapter.

**The cast** (format of `.ai/bestiary.md` section 4; stand-ins are the Cursed Dungeon's, section 2's
stat blocks; rows: linen, gold deep, stone, bone, steel; nothing may echo the Hayward - a tall figure
behind an upright rectangle of woven hurdle):

| Role | Creature | Scene | What it is | Attachment that reads | Palette | Stand-in |
|---|---|---|---|---|---|---|
| Bulk | The Gleaner | `FieldsGleaner` | a field hand stooped under her gleanings | a bound sheaf on the back, ears splayed above the head | linen deep; gold deep sheaf | The Shrouded |
| Fast | Harvest Rook | `FieldsRook` | a rook the size of a dog | flyer: ragged wings wider than the body, a heavy pale beak; detached shadow | stone, bone beak | Grave Runner |
| Heavy | Yoked Ox | `FieldsYokedOx` | a plough ox under its yoke, shouldering forward | the yoke beam across its shoulders, wider than the body | linen deep hide, stone yoke, bone horns | Shieldbearer |
| Wall | The Rick | `FieldsRick` | a stack of black hay with the shape of a man inside, walking | the rick's rounded thatch top with a fork driven through it | gold deep, linen | Barrow Knight |
| Ranged | Cunning Woman | `FieldsCunningWoman` | the village's healer, taken | a corn dolly floating detached at her shoulder - it is what she throws | linen, gold; dolly in earth | Hexer |
| Turret | Iron Weathercock | `FieldsWeathercock` | an iron cockerel he set on a post in the middle of the field. Built: it burns | the cockerel's tail fan and the vane's cross-arms at the top of a post | steel deep, stone post; **fire** eyes | Skull Sentry |
| Charger | Tusked Boar | `FieldsBoar` | an orchard boar, head down | humped bristled shoulders, tusks forward | linen deep, bone tusks | Crypt Lunger |
| Exploder | Tithe Sack | `FieldsTitheSack` | a grain sack of the tithe, swollen past its seams, on two stumps | the tied neck of the sack, the swell | linen, gold grain at the seams | Grave Bloat |
| Slammer | Haymaker | `FieldsHaymaker` | a field hand driving a hay-fork down two-handed | the fork's tines highest | linen, steel tines | Gravebreaker |
| Summoner | The Sower | `FieldsSower` | a sower casting seed from a hip basket; the rooks come down to it | the seed-lip at the hip, one arm flung wide. Minion: **Harvest Rook** | linen, gold | Bone-Caller |
| MiniBoss | The Tithe-Reeve | `FieldsTitheReeve` (48) | his jailer on the land: the one who counts what is owed | a hanging balance scale and a bundle of tally-sticks at the belt, a long hooded coat | linen coat, steel scale, gold | The Warden |

**The table** (`StageBestiary.FallowFields`): Bulk .46 x1.0, Fast .28 x0.75, Wall .22 x1.4, Heavy .04
x2.2 - mean 1.07, the gentlest after the Forest's 1.02. No late table. The six behaviour roles roll
on the shared clocks, as everywhere (`.ai/bestiary.md` section 3).

**The boss: the Hayward** (`HaywardBoss`, `fields_hayward`, health 4500, bonus 160). The parish
officer who kept the hedges and impounded strays, walking behind a hurdle - a woven fence panel - held
upright before him. *"He keeps his hurdle between you and him. Go round to his back."*

- **The verb no other boss asks: go round it.** Damage is cut hard while the player stands in front
  of him (within about 60 degrees of his facing: x0.2) and is full from the side and back. He turns
  to face the player at a limited rate (about 55 degrees a second), so circling at a comfortable
  radius outpaces him. Every spell aims itself, so the only way to "hit his back" is to stand there -
  which is the point. Elderbark asked the player to stand and deliver; the Hayward asks him to move
  with intent, and prepares the sidestep the Gaoler asks at chapter 5 and the orbit the Archivist
  asks at 7.
- `BuildSlam()`: a **Cone** in front (half-angle about 50, radius about 150) - he drives the hurdle
  forward. It lands only where he is blocking, so the safe ground and the damage ground are the same
  ground. `PlantsDuringWindUp` true; `WantsToAttack` only when the player is in front and in reach.
- `BuildPhaseThresholds()` `{0.5}`; `OnPhaseEntered(1)`: turns faster (about 75 a second), and every
  third shove becomes a full swing - a **Ring** of about 120 - so hugging his back is answered too.
- Primitives only: `GroundSlamAttack` Cone and Ring, `AdjustSteering` for the turn. **One shared
  edit:** a damage hook - `protected virtual int AdjustIncomingDamage(int amount) => amount;` called at
  the top of `Enemy.TakeDamage` (the floating number shows the adjusted amount). Nothing else in
  `Enemy.cs`; no other boss overrides it.
- `RegressionChecks.ValidateBossPatterns` holds it to the usual rules (no zero wind-up, cone under 90).
  `_BossProbe --stage=9` bleeds it through its phase.

**The side event: the Well** (`WellEvent`, site `the_well`, wayfinder Compass). From the line: the
well is the last water he has not fouled. **What it asks: defend** - nothing else in the game does.
When the player arrives, the field turns on the well: event enemies (`SpawnEventEnemy(null, ...)`, the
chapter's table) walk at it on `Enemy.ScriptedHeading`, and each that reaches it fouls it a step;
hold for about 45 s with it unfouled and the coin is drawn up (**Wishing Coin**, granted on the spot by
`DiscoveryRewards`, which pays boons as of Phase 1). Fouled through, it fails, and the next run may try
again. The well is never in `"enemies"` and takes no damage from the player (the breakables rule). It
inherits every rule in `.ai/side-events.md`: placed by seed, starts on arrival, internal timers in
real seconds, event enemies are elites.

**Music** (`tools/audio/music_fields.py`, piece `"fields"`): **G Mixolydian, a 9/8 slip jig at dotted
quarter 116**, 24 bars. Bed: a G-D drone, strings, a fiddle figure in spiccato running the three
groups of the bar. Pulse: frame drum in threes, plucked bass on the roots, a harp. Lead: a flute hook
in A; the fiddle (strings) answers in B. Sands is also Mixolydian but 6/8 at 104 on oud and shawm, and
chapter 9: the metre and the voices keep them apart. Check every instrument against the element table
(`.ai/audio-direction.md` section 11, "No chapter imitates an effect").

**Difficulty slot:** between the Forest and the Circle on every axis - table 1.07, boss 4500, bonus
160, and a fight whose damage is avoided by doing the thing the fight asks.

---

## 6. The Broken Circle (`stage_10`, chapter 3)

**The fiction.** *"The circle where the wizards met. He came here first."* A high heath of heather and
granite ringed by standing stones, where the wizards held their moot - and where the dark wizard
struck first, before any of the places that hold them now. Its people are the heath's: shepherds,
wallers, warreners, and the stewards who kept the moot for the wizards. It answers the question the
premise leaves open - how one man took them all - with a place, and it does it at chapter 3, early
enough to matter for everything after.

**Whose prison: a relic.** The circle was warded, and a fleck of the ward is still lit in the stones:
the **Gilded Mote** (*"A fleck of the warded circle, still lit"* - again the boon's own line). The
person this place could hold is decision 2 (Merlin, who in the legend raised a stone circle and was
sealed in stone, is the obvious one, and the reason the chapter is built to take him later).

**Ground.** Heather (violet, deep half) over peat (stone and linen deep), granite outcrops and scree
(`rocky_pit` is already walkable scree), cotton-grass and lichen detail, peat-dark tarns (water: the
one blocker). Wind-bent dead trees alone, gorse and bramble in low drifts, fallen and standing stones
as decor obstacles. **No hazards, no landmark.** Bolt element: **lightning** (`#FFF06A` body,
`#FFFFF0` core) - the high moor's storms, unused by any other chapter, and bright on violet and stone.

**The cast** (nothing may echo the Ninth Stone - a tall single slab with a split down its face - so no
other tall slab, no menhir, no obelisk; rows: violet deep, stone, linen, bone, steel, wool):

| Role | Creature | Scene | What it is | Attachment that reads | Palette | Stand-in |
|---|---|---|---|---|---|---|
| Bulk | The Shepherd | `HeathShepherd` | a hill shepherd in a sodden cloak | the crook rising above the head, its hook turned out | linen deep, stone crook | The Shrouded |
| Fast | Black Hare | `HeathBlackHare` | a hare as black as the peat, running flat out | ears laid back past the tail, a long stretched line | stone deep, linen | Grave Runner |
| Heavy | The Waller | `HeathWaller` | a dry-stone waller carrying a section of his wall on a frame before him | the stacked stones held to one side - the shield plane, wide and low (not a slab) | stone, linen | Shieldbearer |
| Wall | Heath Bull | `HeathBull` | a shaggy bull of the high moor, head low | horns wider than the body over a shaggy fringe | linen deep, bone horns | Barrow Knight |
| Ranged | Moot-Steward | `HeathSteward` | one who kept the circle for the wizards: tall, hooded | a spark floating detached at the hand - it is what it throws | violet deep, linen; spark in lightning | Hexer |
| Turret | Thunder Rod | `HeathThunderRod` | an iron rod he drove into the hill to call the storm. Built: it burns | a crown of iron prongs on a thin rod - thin, never a slab | steel deep; **fire** eyes in the crown | Skull Sentry |
| Charger | The Adder | `HeathAdder` | an adder the length of a man, coiled to strike | the coil with the head raised and drawn back | stone deep, linen zigzag | Crypt Lunger |
| Exploder | Fleece Bale | `HeathFleeceBale` | a bale of wet fleece swollen round nothing, splitting at its ties | rope ties cinching the swell | linen, bone | Grave Bloat |
| Slammer | Fire-Beater | `HeathFireBeater` | a heath-keeper bringing down a birch fire-beater | the broom-like head of the beater highest | linen, stone | Gravebreaker |
| Summoner | The Warrener | `HeathWarrener` | the keeper of the heath's warrens, calling the hares out | a purse net held up on a pole at hand height, a sack at the hip. Minion: **Black Hare** | linen, stone | Bone-Caller |
| MiniBoss | The Lawspeaker | `HeathLawspeaker` (48) | his jailer at the moot: the one who spoke the law to the wizards' council | a rod of office crowned with an iron ring, chains of office | violet deep, steel | The Warden |

**The table** (`StageBestiary.BrokenCircle`): to pace 5:00 Bulk .48 x1.0, Fast .28 x0.75, Wall .20
x1.4, Heavy .04 x2.2 (mean 1.06); from 5:00 Bulk .42, Fast .28, Wall .22, Heavy .08 (mean 1.11). A
late table like the Cave's, so chapter 3 leans toward chapter 4 without reaching the Cave's 1.18.

**The boss: the Ninth Stone** (`NinthStoneBoss`, `heath_ninth_stone`, health 5000, bonus 175). *"There
were nine stones. There are eight, and one that walks."* A standing stone with a split face and his
mark in the split. *"It closes the circle on you. Find the gap and get out before the middle falls."*

- **The verb no other boss asks: find the way out.** Every seven seconds or so it closes a circle on
  the player: a ring of stone-falls (`TelegraphedGroundHit`, about eight around a radius of about 170,
  centred where the player stood when it began) with **one gap** two stones wide, and then the middle
  (a `Ring` of about 120 at the same centre, landing about half a second after the ring). Standing
  still is hit by the middle; running out anywhere but the gap is hit by the ring. The Still Warden
  punishes standing; the Ninth Stone asks where to go, read from the ground, in a second and a half.
- Between closes it walks slowly at the player (about 70) and `BuildSlam()` is a small `Ring` (about
  90) at its feet for anyone who hugs it.
- `BuildPhaseThresholds()` `{0.5}`; `OnPhaseEntered(1)`: two circles in a row, the second centred
  where the first was escaped, its gap on another bearing - read twice. Kept to two phases: it is
  chapter 3.
- Primitives only: `TelegraphedGroundHit.Place` for the stones and the middle, `GroundSlamAttack` Ring.
  Generous tells for an early chapter (1.6 s to the ring), low damage per stone (about 4, the Still
  Warden's shards). `_BossProbe --stage=10`.

**The side event: Relight the circle** (`MootFiresEvent`, site `the_moot`, wayfinder Compass). Nine
standing stones in a ring about a chunk across - solid decor, never targets. One stone's rune lights;
reach it within about six seconds and the next lights, never a neighbour; six in a row and the circle
relights and leaves the **Gilded Mote**. Miss one and the runes go dark and the count starts again,
each restart sending a wave. **What it asks: follow** - a route set by the event, against the clock,
through the swarm. The grove asks the player to stay, the lanterns to explore in any order; this is
neither.

**Music** (`tools/audio/music_heath.py`, piece `"heath"`): **D Dorian in 5/4 (3+2) at 138**, 20 bars.
Bed: low male choir and strings over a D-A drone, a horn call as the ostinato. Pulse: taiko and frame
drum in 3+2, plucked bass. Lead: two horns in fifths in A; a solo voice answers in B. The Forest is
also Dorian, but 6/8 at 112 on flute and harp and two chapters away; the metre and the weight keep
them apart.

**Difficulty slot:** between the Fields and the Cave - table 1.06 then 1.11, boss 5000, bonus 175.

---

## 7. The Phase 2 split

**One agent per chapter**, each in its own worktree from `campaign/new-chapters`
(`tools/new-worktree.sh`). Phase 1 wrote every line a chapter needs in a shared file, so a chapter's
work is **creating files, and editing only blocks already marked as its own**. Two agents' blocks are
always separated by lines neither touches, which is what keeps git from calling it a conflict.

### Never edit (either agent)

`scripts/StageCatalog.cs` except the one flag below; `scripts/StageBestiary.cs` and every other
chapter's roster file; `scripts/GlobalStatsManager.cs`, `scripts/SaveData.cs`, `scripts/SaveManager.cs`
(no migration is needed - section 4); `scripts/AchievementDefinition.cs` and `scripts/UnlockCatalog.cs`
(the deeds and rewards are written); `scripts/MusicCatalog.cs` (the sets switch on by themselves);
`scripts/RegressionChecks.cs`, `scripts/ContentValidator.cs`; every stand-in scene and every existing
`scenes/resources/*Frames.tres`; every other chapter's blocks in the files below; `tools/art/tiles.py`,
`ground_detail.py`, `tile_atlas.py`, `bestiary_kit.py`, `build.py` (never run it), `tools/audio/music.py`;
`.ai/` docs other than your own status line in section 8; `README.md`, `CLAUDE.md`. Do not add a
**blocking or hazard terrain**: either must agree with three places no chapter owns (`.ai/terrain.md`).

### The Fallow Fields - files to create

| What | Files |
|---|---|
| the cast | `tools/art/cast_fields.py` (stems `fields-<name>`); for each of the 11 creatures in section 5: `scenes/Fields<Name>.tscn`, `scenes/resources/Fields<Name>Frames.tres`, `assets/bonelight/enemies/fields-<name>-{moving,attack,hurt,death}.png` + `.png.import` - the recipe is `.ai/bestiary.md` section 8, unchanged |
| the ground | `tools/art/terrain_fields.py` (TERRAINS / MATERIALS, `import tiles` for the helpers: a `stubble` material, `furrow` and `hedge_bank` terrains); `tools/art/detail_fields.py` (DETAIL_SETS: stubble tufts, thistle, poppy, chaff); their tile PNGs under `assets/bonelight/tiles/` (+ `.import`) |
| the boss | `scripts/HaywardBoss.cs` (`BossEnemy` subclass, the hooks in section 5), `scenes/HaywardBoss.tscn` (copy `ForestTreantBoss.tscn`, drop the `uid=`), its art in `tools/art/boss_hayward.py` and `assets/bonelight/enemies/fields-hayward-*.png`, `scenes/resources/FieldsHaywardFrames.tres` |
| the side event | `scripts/WellEvent.cs` (`SideEvent` subclass), any art in `tools/art/explore_well.py` |
| the music | `tools/audio/music_fields.py` (`PIECE`, name `"fields"`); `assets/music/fields_{bed,pulse,lead}.wav` + `.import` with `edit/loop_mode=2` - rendered once, after listening (`.ai/audio-direction.md`, "Size") |

### The Fallow Fields - the exact places it edits

| File | Its place, and only that |
|---|---|
| `scripts/StageEnvironmentCatalog.cs` | the `[StageEnvironmentKind.Fields]` profile block (banner tile, tints, water style, counts) |
| `scripts/LevelGenerator.cs` | the `[StageEnvironmentKind.Fields]` palette block |
| `scripts/GroundDetail.cs` | the `[StageEnvironmentKind.Fields]` rules block |
| `scripts/Node2DGame.cs` | the body of `case StageEnvironmentKind.Fields:` in `BuildDecorProps` |
| `scripts/BossCatalog.cs` | the `[9]` block, numbers only, if tuning moves them |
| `scripts/SideEventDirector.cs` | the line `"stage_9" => null,` becomes `"stage_9" => ("the_well", () => new WellEvent(), WayfinderKind.Compass),` |
| `scripts/Enemy.cs` | **the Fields agent only**: the `AdjustIncomingDamage` hook (section 5), a virtual method and one call at the top of `TakeDamage` |
| `scripts/StageCatalog.cs` | **last**: `IsPlayable = false` on the `stage_9` entry, removed |

### The Broken Circle - files to create

| What | Files |
|---|---|
| the cast | `tools/art/cast_heath.py` (stems `heath-<name>`); for each of the 11 creatures in section 6: `scenes/Heath<Name>.tscn`, `scenes/resources/Heath<Name>Frames.tres`, `assets/bonelight/enemies/heath-<name>-*.png` + `.import` |
| the ground | `tools/art/terrain_heath.py` (a `heather` material, `peat` and `granite` terrains), `tools/art/detail_heath.py` (heather tufts, cotton-grass, lichen stones); tile PNGs + `.import`. Standing-stone decor sprites in `tools/art/props_heath.py` -> `assets/bonelight/world/` |
| the boss | `scripts/NinthStoneBoss.cs`, `scenes/NinthStoneBoss.tscn`, `tools/art/boss_ninth_stone.py`, `assets/bonelight/enemies/heath-ninth-stone-*.png`, `scenes/resources/HeathNinthStoneFrames.tres` |
| the side event | `scripts/MootFiresEvent.cs`, any art in `tools/art/explore_moot.py` |
| the music | `tools/audio/music_heath.py` (`PIECE`, name `"heath"`); `assets/music/heath_{bed,pulse,lead}.wav` + `.import` |

### The Broken Circle - the exact places it edits

The same table as the Fields with `Heath` for `Fields`, the `[10]` boss block, the line
`"stage_10" => null,` (becoming `("the_moot", () => new MootFiresEvent(), WayfinderKind.Compass)`), the
`stage_10` flag last - and **not** `Enemy.cs`.

### Verify, per chapter

`dotnet build WizardSurvivors.sln`; `"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit-after
300 -- --no-save-writes` - the chapter's bestiary to-do line shortens as creatures land, its campaign
to-do line names only what is left, and nothing else new appears; `_BossProbe --stage=9|10`; a
balance-bot run (`scenes/_BalanceHarness.tscn -- --minutes=6 --character=1 --all-unlocked --immortal
--stage=9|10`); then **eyes on a real run** - a silhouette, a telegraph and a well are not checked by any
validator. After the flag flips, the chapter's campaign to-do line must be gone; if it says "playable
but still has stand-ins", the flag went first.

### The integrator, once, after both merge

Re-run `python tools/art/tiles.py` and `python tools/art/ground_detail.py` on the merged tree:
`tiles.json`, `detail.json` and `atlas.png` / `atlas.json` are shared outputs that both chapters
regenerate, so take either side in the merge and rebuild them. Then `"$GODOT_BIN" --headless --path .
--import`, the validator run above, and update `README.md`'s counts, `CLAUDE.md`'s schema line,
`.ai/bestiary.md` section 4, `.ai/audio-direction.md`'s music table and `.ai/enemy-behaviour.md`'s boss
table from this doc.

---

## 8. Status

Each Phase 2 agent edits only its own line.

- The Fallow Fields: designed, not built (Phase 1 stand-ins throughout).

- The Broken Circle: building on `chapter/broken-circle`. Cast drawn (11 of 11, `cast_heath.py`).
  Ground drawn: `heather` floor, `peat` and `granite` terrains (`terrain_heath.py`), heather tufts,
  cotton-grass and lichen stones (`detail_heath.py`), standing and fallen stones and gorse
  (`props_heath.py`, `world/heath/heath.json`). Boss, side event and music still on their Phase 1
  stand-ins; not playable.

---

## 9. Decisions for the developer

1. **Two early chapters, or three?** Two puts the Dungeon fifth of ten. A third early chapter would
   go between the Broken Circle and the Cave (Dungeon sixth of eleven) and costs one `StageCatalog`
   entry, one line in the order, and a third Phase 2 agent; nothing else in the plumbing changes.
2. **People, not just relics?** The fiction says each place holds one of the others. Both chapters
   pay a relic so they cannot close the finale (section 4) and because a wizard is not a chapter
   build. If either should hold a person: the Broken Circle is built for **Merlin** (the stone-raiser,
   sealed in stone); the Fields for a cunning woman (a historic one, as Tituba is). A wizard needs a
   sprite, a signature spell on an element no wizard opens on, the Apotheosis verb rule, and a schema
   step that grants him to a save that had finished the campaign (as schema 12 did for John Dee).
3. **Retune the Gaoler for chapter 5?** Left at 5200 health / 170 Arcane after the Choir's 5700 / 190.
   Recommended 6200 / 200, measured with `_BossProbe --stage=1` and the bot first. The maze may be
   step enough on its own.
4. **Tituba arrives at chapter 5 instead of 2.** The first rescued wizard is three chapters later;
   the early chapters pay two boons and two relics in her place. Accept, or move a reward earlier.
5. **Schema 13 is claimed twice.** `spells/star-rain` (uncommitted in `ws-spells-star-rain`) bumps to
   13 for a Starfall grant. Whichever merges second renumbers its step to 14 and keeps both; the
   validator's sample saves set `SchemaVersion = 12`, so they need no change if this one becomes 14.
   Note that a save already at 13 from the other branch would skip this step: harmless here, because
   gate rule 3 derives the same chapters from the lifetime record (checked on the developer's own save
   shape, section 4), but renumbering is still right.
6. **The real save was rewritten on 2026-10-25 at 11:20:32** - to schema 13, with 6-second forced
   defeats on `stage_3` and `stage_1` paid into it (`RunTelemetryHistory`), by a tool run on an
   unshipped branch. Not this branch's migration: it would have added `stage_2` to that save (the
   lifetime record has Elderbark beaten, and Elderbark opens the Cave in the new order), and the save
   still has only `stage_1, stage_3, stage_4`. The save was not changed back - that is the
   developer's call. The guard in section 3 stops any tool scene on this branch writing it again;
   it reaches other branches only when they merge this.
7. **Three boons are still sold by a shop that sells none** - Quicksilver Bead, Deepwater Pearl,
   Saltbound Chain. Four of the seven became the early chapters' rewards; the rest is the open work
   `.ai/gameplay-design.md` already names.
8. **The Hayward needs one shared primitive** (`Enemy.AdjustIncomingDamage`, section 5). It is the
   first boss whose damage taken depends on where the player stands; say if that is a line the game
   should not cross.
