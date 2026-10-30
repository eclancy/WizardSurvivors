# The endgame castle: the bridge, the Cursed Dungeon, the Court and the throne

Eric, 2026-10-29:

> "also, let's add or edit levels. to introduce more variety
>
> for example, there should be a bridge to the final bosses tower. the level has a bridge edge on
> the top left and bottom right, so the player needs to travel up and to the right to get to the
> castle.
>
> inside the castle is the dungeon level, which has it's own walls and passageways.
>
> then a castle labyrinth, where the player has to navigate court halls, and can find different
> rooms, such as a huge dining room with a bunch of chicken that heals them, and other unlockables
> hidden within, etc
>
> and finally the throne room, which is not a map, but the player gets a bunch of levels as soon as
> the fight starts and can essentially make a build, then fights a series of bosses and minions until
> the final boss comes out."

**Status: design, decided 2026-10-30. C0, the plumbing, is built** (branch `wip/castle-plumbing`;
section 11 says what it is and how a chapter agent plugs into it). Nothing else is. Eric reviewed the first draft and chose: **move
the Cursed Dungeon in** (the first draft proposed a new chapter, the Undercroft, instead), and
**build everything else as recommended** - the diagonal bridge that collapses behind the player,
the roast fowl, the fixed Court, the Muster, earlier bosses in pairs, Boss Rush unchanged, and the
Ruins' walls and the Cave's caverns first. This revision carries his choice through: sections 1.3 to
1.8 resolve what moving the Dungeon costs, section 4 is the Dungeon in the castle, and the build plan
(section 9) is redone. Section 10 lists what was decided and the few questions the move raised.

Its sister doc, `.ai/achievements-and-secrets.md`, holds the achievements and secrets from the same
message (its unlock core is being built as piece A1, which takes schema 19 and the gate). The
castle's secrets are listed there and placed here.

Read with `.ai/campaign.md` (how a chapter is added and moved, and the gate's three rules),
`.ai/world-and-tone.md`, `.ai/terrain.md` (sections 2, 3, 6, 7 and 9 above all), `.ai/run-pacing.md`,
`.ai/bestiary.md`, `.ai/enemy-behaviour.md`, `.ai/boss-levels-design.md` (Boss Rush), `.ai/side-events.md`,
`.ai/gameplay-design.md` ("The Apotheosis") and `.ai/world-map.md` (on branch `wip/world-map`).

---

## 1. Where it belongs

### 1.1 What the fiction already says

- **He has never been on screen.** Tone rule 5: *"He is never on screen until he is."* No chapter has
  shown him. The Emberdeep's boss is the Warden of the Deep - a warden, built, a recap of the road -
  and the Boss Rush ends on it too.
- **The Emberdeep is "His seat, and the end of the road."** It is the one chapter whose whole cast
  was made, not taken: his forge, where every shackle came from (the Chainwright).
- **The world map ends there.** The Emberdeep is a volcano at the east end of the road, the Boss
  Rush stands on its summit, and *"past it the land ends at black cliffs and a dark sea"*
  (`.ai/world-map.md` section 1). The map's own doc expects the endgame to grow the world east.

So the castle is where he is: across the water from the end of the road.

### 1.2 After the Emberdeep (decided)

The castle is a run of chapters **after the Emberdeep** in `CampaignOrder`: the Warden of the Deep
opens the bridge. The alternatives - rebuilding the Emberdeep as the bridge, or a separate mode
outside the order - were set aside (the first draft's table is in this file's history). The new
chapters' ids are appended, never renumbered (`.ai/campaign.md` section 3).

### 1.3 The Cursed Dungeon moves in (decided)

Eric: *"Move Cursed Dungeon in."* The existing chapter - `stage_1`, its endless maze and jail
walls, its cast, the Gaoler, the Gaoler's key and Tituba, its music - leaves fifth place and becomes
the castle's dungeon, the second chapter of the castle, between the Last Bridge and the Court. **Its
id stays `stage_1`**: an id is in every save, unlock rule and harness flag, so a chapter moves by its
place in the order, never by renumbering (`--stage=1` is still the Dungeon wherever it stands).

What the move costs, and how each cost is met:

| cost | where it is resolved |
|---|---|
| the new order, and who opens what | 1.4 |
| saves that had reached or passed the Dungeon | 1.5, schema 20 |
| Tituba is freed there, and the final gate counts wizards | 1.6 |
| the Dungeon's content in a castle | section 4 |
| the hole at fifth place | 1.7 |
| Boss Rush, which fights the Gaoler fifth | 1.8 |
| the keep on the world map | section 8 |
| the build plan | section 9 (C2 is the move) |

### 1.4 The campaign, in its new order

| # | chapter | id | boss | what the boss asks | how it opens | beating it opens | deed pays |
|---|---|---|---|---|---|---|---|
| 1 | Enchanted Forest | `stage_0` | Elderbark | sustain damage | always | the Fallow Fields | Thorned Vine |
| 2 | The Fallow Fields | `stage_9` | the Hayward | go round it | Elderbark | the Broken Circle | Thornseed |
| 3 | The Broken Circle | `stage_10` | the Ninth Stone | find the way out | the Hayward | the Sunken Cave | Shadegrease |
| 4 | Sunken Cave | `stage_2` | the Hollow Choir | spread damage | the Ninth Stone | **the Blighted Swamp** (was the Dungeon) | Riptide |
| 5 | Blighted Swamp | `stage_3` | Mother Rot | kill what heals her | **the Hollow Choir** (was the Gaoler) | the Mystic Ruins | Mirefoot |
| 6 | Mystic Ruins | `stage_4` | the Archivist | orbit, re-acquire | Mother Rot | the Frozen Waste | Spiritual Weapon |
| 7 | Frozen Waste | `stage_5` | the Still Warden | hold ground | the Archivist | the Scorched Sands | Rimebriar |
| 8 | Scorched Sands | `stage_6` | the Long Coil | burst a window | the Still Warden | (half the Emberdeep's gate) | Fireball, Sunsteel Filament |
| 9 | **The Emberdeep** - the gate | `stage_7` | the Warden of the Deep | all of it, in sequence | **the Long Coil beaten and every wizard freed whose rescue comes before it** (1.6) | the Last Bridge | 300 Arcane Energy |
| 10 | **The Last Bridge** | `stage_11` | **the Constable** | **advance under fire** | the Warden of the Deep | the Cursed Dungeon | 350 Arcane Energy |
| 11 | **Cursed Dungeon** (moved from 5) | `stage_1` | **the Gaoler** | sidestep a lane, in a maze | the Constable | the Court | Shadow Bolt; its key event frees Tituba |
| 12 | **The Court** | `stage_12` | **the Seneschal** | **pursue** | the Gaoler | (half the Throne's gate) | 400 Arcane Energy |
| 13 | **The Throne** | `stage_13` | **the procession, then him** | **take it back** | **the Seneschal beaten and every wizard freed** (1.6) | - | the ending; 1,000 Arcane Energy; Merlin, as a later build (decision 11) |
| - | The Sketchbook | `stage_8` | Wizard Dude | leave the wedge | always, outside the order | - | 400 Arcane Energy |

Thirteen chapters: eight on the road before the gate, the gate, and the castle's four. (The first
draft's ids `stage_12` to `stage_14` for the Undercroft, Court and Throne were never built; the Court
and the Throne take `stage_12` and `stage_13` now, and there is no Undercroft.)

New and changed lines (`StageDefinition.CorruptionText`, under ten words; `FlavorText`):

- The Emberdeep: *"His forge, at the end of the road."* (was *"His seat, and the end of the road."*)
- The Last Bridge: *"His seat is across the water."*
- Cursed Dungeon: the corruption line stays - *"He keeps a wizard in one of these cells."* - and is
  stronger here: they are his own cells now. Its flavour line becomes *"The cells under his castle:
  stone corridors, and a wizard behind one of the doors."*
- The Court: *"A court with no one left in it."*
- The Throne: *"He is here."*

### 1.5 The save: schema 20

A1 (`.ai/achievements-and-secrets.md`, section 3) takes schema 19. This is the next step,
**`MigrateDungeonIntoCastle`, schema 20**. Whichever lands second renumbers and keeps both
(`.ai/campaign.md`, decision 5).

**Why a migration is needed at all.** The gate's three rules (`GlobalStatsManager.IsStageEarned`)
each read the Dungeon's old place as evidence:

1. *Its id is in the opened list.* `stage_1` is in every save that reached chapter 5.
2. *A chapter later in the campaign is opened, so this one was passed.* With the Dungeon eleventh, a
   save that reached the old chapter 5 would count as past every boss-gated chapter before it - the
   Swamp to the Sands, and the Last Bridge - and open them all.
3. *The previous chapter's boss is beaten.* The Court's previous chapter is the Dungeon; every save
   that ever beat the Gaoler at chapter 5 would open the Court.

And taking `stage_1` out of the list would close a chapter a save could enter, which is the silent
confiscation `CLAUDE.md` forbids. So the old evidence is kept, but filed where it means only what
it meant.

**A new field, `SaveData.GrandfatheredStageIds`**: chapters a save may enter because an older order
opened them. It grants **entry and nothing else**: it is never evidence for rule 2 or rule 3, and a
victory in a chapter entered only through it opens nothing.

**The steps:**

1. **Write down what the save can enter today.** Evaluate every chapter under the schema-19 order,
   frozen in the step (`CampaignOrderAtSchema19`: `stage_0, stage_9, stage_10, stage_2, stage_1,
   stage_3, stage_4, stage_5, stage_6, stage_7`), with the schema-19 rules and their evidence frozen
   too (each chapter's boss id; the gate's wizard list as A1 left it - the four starters, Tituba,
   Väinämöinen and John Dee). Every chapter the save can enter is written into `UnlockedStageIds`,
   **except the Dungeon**. This is what keeps rule 2's old gifts: the early chapters a save had
   *because* the Dungeon stood fifth are recorded before it moves.
2. **The Dungeon is grandfathered.** If step 1 found it enterable, `stage_1` leaves
   `UnlockedStageIds` and goes into `GrandfatheredStageIds`.
3. **Nothing is written for the castle.** No save can have been past it.

**The rules afterwards** (`GlobalStatsManager`):

- `IsStageAvailable(s)` = playable, and (`IsStageEarned(s)` **or** `s` is grandfathered).
- `IsStageEarned(s)` - *the road* - reads only `UnlockedStageIds`, never the grandfathered list.
  - Before the gate: rules 1 to 3, unchanged.
  - At a gate (the Emberdeep, the Throne): A1's gate (1.6).
  - **Past the gate, the road is a chain**: rules 1 and 2 as before, and rule 3 only when the
    previous chapter is itself earned on the road. So an old Gaoler kill can never open the Court
    while the Dungeon is only grandfathered.
- **A victory opens the next chapter only if the chapter it was won in is earned on the road**
  (`Node2DGame.ApplyBossVictoryUnlocks`, one condition). A win in a grandfathered Dungeon still pays
  everything else: the Gaoler's deed, the key event and Tituba, the vault, the lifetime record.

**"Being past a chapter is having been through it"** still holds on the road: a chapter opened on the
road earns every boss-gated chapter before it, as `.ai/campaign.md` built it. The grandfathered
Dungeon is not on the road and is evidence of nothing, so it puts nobody past the Swamp. A castle
chapter opened on the road does put the save past everything before it, which is true.

**What each kind of save gets:**

| at schema 19 | after schema 20 |
|---|---|
| reached the Cave, not the Dungeon | unchanged. The Choir now opens the Swamp |
| **had the Dungeon open** (the Choir beaten), not the Gaoler | keeps the Dungeon (grandfathered), and **gains the Swamp**: the Choir opens it now, and the Dungeon is no longer in the way. Chapters 1 to 4 written down |
| beat the Gaoler and went on (to the Waste, say) | everything kept, written down; the Dungeon grandfathered; nothing in the castle |
| finished the old campaign (the Emberdeep open) | everything kept; the Dungeon grandfathered. The bridge opens when the Warden of the Deep falls (the lifetime record, rule 3); the Dungeon joins the road when the Constable falls; and the Court opens **at once** then, because their old Gaoler kill counts for rule 3 once the Dungeon is on the road. A generous skip of one fight they have already won; accepted |
| a new save | the new road, from the Forest |

`RegressionChecks.ValidateDungeonMove` checks it on sample schema-19 saves, one per row above, plus:
no sample short of the Emberdeep can enter any castle chapter after migrating; a win in a
grandfathered Dungeon writes nothing to `UnlockedStageIds`; every chapter a sample could enter
before, it can enter after; and a second migration changes nothing. `ValidateCampaignMigration`'s
schema-12 samples are re-run through every step to 20 with their expectations updated for the move.

**Tool runs migrate the real save.** Pass `-- --no-save-writes` on every Godot run on these branches.

### 1.6 Tituba and the gates

**The problem.** Tituba is freed by the Gaoler's key, in the Dungeon, which is now behind the
Emberdeep. If the Emberdeep's gate counted every wizard, it would wait on someone held behind it: the
Fireball bug again (`.ai/achievements-and-secrets.md` 1.4), for a person this time.

**A1's rule is enough for correctness**: the gate counts only wizards whose rescue chapter comes
before it, and `ValidateGateReachability` holds it. The Emberdeep then opens on the Long Coil and the
wizards on the road - the four starters, John Dee and Väinämöinen (Laelaps is outside the gate) -
and it cannot be blocked by anything behind it.

**It is not enough for the fiction**, which says *he becomes reachable only when every wizard is
free* - and with the rule alone, a player could beat the Gaoler without doing his key event and walk
on to the Throne with Tituba still in her cell. So, the simplest fix that keeps it:

- **A1 writes the rule per gate chapter** - "every wizard whose rescue comes before *this* chapter" -
  rather than for one global gate (this note goes to the A1 agent through the coordinator).
- **The Throne carries the same kind of gate**: the Seneschal beaten and every wizard freed. At the
  Throne "every wizard" includes Tituba, so the sentence is true exactly where he is. It costs one
  gate value on `stage_13` and a second gate in the validator's rule ("every chapter after the first
  gate is boss-gated or a wizard gate").
- **Her rescue does not move.** She was a real woman jailed at Salem on an accusation; the Gaoler's
  key was built for her, and the Dungeon's line - *"He keeps a wizard in one of these cells"* - is
  her. In his own cells it is stronger, not weaker.

**What it costs, stated plainly:** a new player meets Tituba at chapter 11 of 13, not 5. Väinämöinen
(chapter 7) becomes the first wizard freed by a side event; John Dee (30 chests) and Laelaps (the
Coursing, chapter 3) still come early. Her Apotheosis is late-game play. Saves that already freed her
keep her, and grandfathered saves can free her in their Dungeon at any time.

**Knock-ons, all small:**

- The locked hints say who is missing (`StageCatalog.LockedHintFor`): the Emberdeep's *"Sealed -
  defeat the Long Coil and free every wizard on the road"*, the Throne's *"Sealed - free every
  wizard"*, each naming those still held.
- Tituba stays secret until the Dungeon opens (`RevealStageId = "stage_1"`), which is now late; the
  validator's reveal rule generalises to "a wizard a gate counts is revealed by a chapter before that
  gate" (the Dungeon is before the Throne).
- **Shadow Bolt** (the Gaoler's deed) and **Molten Shard** (the Dungeon's spell vault, the Cold Forge)
  move past the Emberdeep with her. The gate counts no spells (decided), so nothing closes.
  `.ai/achievements-and-secrets.md` 6.3's second element pass counts only sources before the gate:
  Darkness and Poison keep Hollow Star, Void Lance, Contagion and the rest; Metal and Fire keep
  Hunter's Draw, Iron Palisade, Fireball and the boons. If that pass warns, the fix is in that doc,
  not here.

### 1.7 Fifth place: nothing replaces it

**Recommended: the road shortens to eight chapters before the Emberdeep**, and no new chapter fills
fifth place.

| | table mean (health x) | boss health |
|---|---|---|
| 4 Sunken Cave | 1.00, 1.18 from 5:00 | 3 x 1900 |
| ~~5 Cursed Dungeon~~ | ~~1.35~~ | ~~6200~~ |
| 5 Blighted Swamp | 1.27 | 6800 |
| 6 Mystic Ruins | 1.41 | 7200 |
| 7 Frozen Waste | 1.13 | 7800 |
| 8 Scorched Sands | 1.09 | 8200 |
| 9 The Emberdeep | 1.28 | 11000 |

(From `.ai/campaign.md` section 1, with the Gaoler at his retuned 6200.)

- **The curve gets smoother, not worse.** The Dungeon's 1.35 was a spike between the Cave's 1.18 and
  the Swamp's 1.27; without it the step from chapter 4 to 5 is 1.18 to 1.27, and boss health climbs
  without a break from 4,200 to 11,000.
- **What is lost is the mid-campaign walled chapter.** Eric moved the Dungeon to the middle as the
  step up from open fields (*"a maze is a harder ask than an open field"*). **The Cave's caverns
  (chapter 4) and the Ruins' broken walls (chapter 6)** - the two variety pieces he chose to build
  first (section 7) - put walled ground back in the middle, on either side of the Swamp. So **V2 and V1
  should land before or with the move**, or the middle of the campaign is all open field for a while.
- A chapter can still be added at fifth place later with one line in the order, as `.ai/campaign.md`
  decision 1 noted for the early chapters. It is not needed.

### 1.8 Boss Rush stays as it is

Eric chose it unchanged: the ten wardens of the road, in the Emberdeep, ending on the Warden of the
Deep. The move would change it three ways unless held:

- **Its roster is derived from the campaign order**, which would now put the Gaoler after the
  Emberdeep and make him the finale. It becomes an explicit list instead -
  `BossRushCatalog.RoadOrder`, the ten stage ids in today's order, the Gaoler fifth - with
  `ValidateBossRush` checking every id has a boss with a scene. Immune to any later reordering.
- **Its arena is `FinalChapter`**, which is now the Throne. It reads `StageCatalog.GateChapter` (the
  Emberdeep) instead.
- **Its health comes from each boss's catalog health**, and the Gaoler's doubles for his castle
  slot (4.3). `BossDefinition` gains an optional `BossRushHealth` (the Gaoler's: 6,200) so the rush
  meets the Gaoler it always did.
- **When it opens.** It opens once the Warden of the Deep has fallen, so that *"nobody meets a boss
  here before they have beaten it where it lives"*. A new save now meets the Warden before the Gaoler,
  so `IsOpen` becomes **every boss on its roster beaten** - the same rule, kept true. On a new save
  the rush opens after the castle's dungeon; on every existing save that had it, nothing changes
  (they beat the Gaoler at chapter 5).

### 1.9 The plumbing

Built once, by one agent, before the chapters (section 9, C0), the way `.ai/campaign.md`'s Phase 1
was. The Dungeon's move itself is C2's (section 9).

- **`StageCatalog`**: three entries appended (`stage_11` the Last Bridge, `stage_12` the Court,
  `stage_13` the Throne), `IsPlayable = false` until each is built, placed in `CampaignOrder` after
  `stage_7`. A chapter that is not playable is skipped, so the Warden of the Deep opens nothing until
  the bridge exists, and nobody is stopped by a chapter not yet built.
- **The gate chapter is not the last chapter.** `FinalChapter` is "last in the order" and
  `ValidateCampaign` requires it to be the gated one. A new `StageCatalog.GateChapter` (the first
  chapter with a wizard gate) is what Boss Rush and the migration checks read; `FinalChapter` stays
  "last". The rule becomes: one gate chapter on the road, and every chapter after it boss-gated or
  wizard-gated (the Throne, 1.6). Three readers change (`BossRushCatalog.Arena`, two in
  `RegressionChecks`).
- **`BossCatalog`** `[11]`, `[12]`, `[13]` on stand-ins (the Warden of the Deep's scene) under the new
  bosses' ids and names, so each chapter's deed and opening are its own from the first day.
- **`StageBestiary.Bridge.cs` and `StageBestiary.Court.cs`**, `Planned(...)` at the scene paths of
  sections 3.5 and 5.7, the Emberdeep's creatures as stand-ins (built things, right for his house).
  The Dungeon keeps its roster. The Throne has no trickle cast (6.3).
- **`StageEnvironmentKind`** gains `Bridge`, `Court` and `Throne`, each with a stand-in profile block
  and a `LevelGenerator` palette block. The Dungeon keeps `Castle`.
- **`StageTopology`** gains `Band` and `Authored`, each generated in its own partial file
  (`LevelGenerator.Band.cs`, `LevelGenerator.Authored.cs`), falling back to `Open` until built.
- **Wall art by chapter**: walls are the Dungeon's jail set today. `LevelTilePainter.WallTileFor`
  learns to read the wall set of the stage's environment, with the jail set as the fallback, so the
  Court and the Throne get their own walls without an edit to `jail_walls.py`.
- **`ChapterRules`** (new, `scripts/ChapterRules.cs`): a per-chapter object `Node2DGame` asks at
  seven hook points, all no-ops by default so every existing chapter plays exactly as it does: run
  start, choosing a spawn point, whether a point is closed to placement, where the boss appears, where
  the way home opens, a pinned pace clock, and the objective line under the run timer.
  `ChapterRulesCatalog.ForStage` holds a pre-placed `null` line per castle chapter (the Dungeon needs
  none), as `SideEventDirector.ForStage` did for the early chapters, so each chapter's agent replaces
  one line and writes one file of its own. This is what keeps four agents out of `Node2DGame.cs`.
- **`MusicCatalog`**: three planned sets with stand-ins (the Emberdeep's). The Dungeon keeps its own.
- **`SideEventDirector`**: `null` lines for the three new chapters. The Dungeon keeps the Gaoler's key.
- **Saves**: the new ids need no migration (they only add). The move needs schema 20 (1.5).

---

## 2. What a run is in the castle

| chapter | clock | the goal | how it ends |
|---|---|---|---|
| The Last Bridge | the usual 15-minute run on the pace clock | **travel**: keep ahead of the collapsing span and reach the gate yard | the Constable at 15:00 in the yard; when he falls, the gate opens and walking through it is the way home |
| Cursed Dungeon | the usual | **survive** in the maze, and free whoever is in the cell | the Gaoler at 15:00, as always; the arcane portal is the way home |
| The Court | the usual | **explore**: find the rooms | the Seneschal at 15:00, pursued through the halls; when he falls, the throne room doors at the Court's heart open, and he walks there |
| The Throne | no survival clock; the pace clock pinned at its end | **build, then fight** | the procession, then him; when he falls, the doors behind the throne open |

**The three map chapters are ordinary runs.** Their pace, XP curve and level gates are
`.ai/run-pacing.md`'s, untouched: what changes is the ground and the goal, never when the world gets
harder or how fast levels come. The bridge and the Court must be measured with the balance bot anyway
(section 9), because a band or a fixed building can change the spawn supply, and the kill rate is the
supply.

**The castle's people.** It is his house. The Emberdeep's cast was made; the castle's is his
household, taken - his garrison in the steel family, his courtiers and servants in linen - with the
built things of a great house (gargoyles, empty armour, candelabra) burning his fire, the carve-out
`.ai/world-and-tone.md` allows. The Dungeon's crypt-dead and his jailer were already his: they are
under his house now. Cold eyes on the taken, fire on the made.

---

## 3. The Last Bridge (`stage_11`)

*"His seat is across the water."* From the black cliffs past the Emberdeep a long stone bridge spans
the dark sea to his castle on its rock. It is the only way across, and it falls in behind you.

### 3.1 Reading the sketch

*"a bridge edge on the top left and bottom right, so the player needs to travel up and to the
right"*: the bridge runs from the **bottom left to the top right of the screen** (south-west to
north-east), and its two edges - the parapets and the drop beyond them - are what the player sees in
the **top-left and bottom-right corners**. Walking it is walking up and right. (Confirmed: Eric chose
"build as recommended".)

```
                                             [ the gate ]
                                          ///////////
            the drop (top left)        ///////////
                                    ///////////
                                 ///////////      <- a gatehouse: the deck narrows
                              ///////////
                           ///////////     the drop (bottom right)
          [ the start ] ///////////
```

**Why a diagonal is right, not just Eric's choice.** The game has two canvases: a PC sees about
1506 x 847 world pixels, a phone 847 x 1506 (camera 0.85 on both). A horizontal bridge would show a
PC twice as much bridge ahead as a phone; a vertical one, the reverse. A 45-degree bridge shows both
the same: about 600 px of deck ahead, and both edges in the same corners. It is the one direction the
two canvases agree on.

### 3.2 The shape

The ground is a pure function of the world tile, like every stage (`.ai/terrain.md` section 7), on a
new `Band` topology: measure each cell along the bridge's axis (`s`, north-east) and across it (`t`);
anything farther across than the deck's half-width at that `s` is **the drop**.

| piece | where along the bridge | width across | what it is for |
|---|---|---|---|
| the bridgehead | the first chunk | the cliff top | the start; the spawn clearing |
| a span | three chunks each (about 4,600 px) | **18 tiles (864 px)** | the deck: room to circle a pack, and both edges in the screen's corners on both canvases |
| a gatehouse | at the end of each span, four in all | **8 tiles** for 10 tiles of length | a tower over the deck: the narrows, where the horde funnels, and murder-holes |
| a yard | after each of the first three gatehouses, one chunk long | **28 tiles** | a bastion yard to fight in and breathe |
| the gate yard | after the fourth gatehouse, two chunks | 28 tiles | the boss's ground; the castle gate in its far wall |

About 18 chunks in all, some 28,000 px - a little over two minutes of walking with nothing in the
way, which is never.

**The drop is water, as far as the engine knows.** Water is the one blocking ground that is drawn
from its shape and collides at its shoreline rather than its cells (`.ai/terrain.md` section 3), so a
diagonal edge comes out as a straight shore, not a staircase of tiles. The chasm is a new
`WaterStyle`, **`abyss`** (`tools/art/water.py`): the sea far below, near-black, with faint movement
and mist. Every place that must agree about blocking ground (section 2 of the terrain doc) already
knows water. The **parapet** is a strip drawn along the shore - a low crenellated wall at 45
degrees, drawn as its own sprites along the line the way the flower pen's fence is, with no
collision of its own (the shore is the collision). Drawing the parapet as wall *tiles* was ruled
out: a 45-degree wall on a square grid is a staircase.

### 3.3 What a run is

**The collapse.** From pace 0:30, the bridge falls in behind the player - his doing, cutting off the
way back - along a straight front across the deck that advances at a steady pace: from just behind
the start at 0:30 to the mouth of the gate yard (about 16 chunks on) at 15:00, about **29 px a pace
second**, an eighth of walking speed.

- **Behind the front is the drop.** It is drawn over the fallen deck (falling masonry along the
  line, the abyss beyond it) and it is not terrain: no cell changes, nothing is rebuilt. It is a
  moving hazard (`BridgeCollapseFront`, its own node), the way the Ninth Stone's ring is a hazard
  rather than a wall.
- **Standing in it costs 4 HP a second** and slows the player hard. It is never instant death, and
  it is always on screen before it reaches him: a player who never leaves the bridgehead has about a
  minute and a half.
- **Enemies behind the front fall** (gone, no XP), and nothing is placed there
  (`ChapterRules.IsPlacementBlocked`).
- **XP orbs it overtakes are folded into one orb at the front's edge**, as `SweepFarXpOrbs` folds
  far orbs: never lost, but fetching them means stepping back toward the fall.

**The spawner on a band.** The usual spawn ring is mostly over the drop, so the bridge picks its own
points (`ChapterRules.TryPickSpawnPoint`), off screen as always: **65% ahead** along the deck (they
come from his castle), **15% behind** but ahead of the front, **20% at the parapets** (climbing over).
The budget, the pace curve and the cast are unchanged; only where they appear is the bridge's.

**The end.** The front stops at the gate yard's mouth at 15:00, so at 15:00 the player is in the
yard, wherever he dawdled. The clock stops, the Constable comes (3.6). When he falls the gate opens:
it is the way home (`ChapterRules.VictoryPortalPoint`, in place of the arcane portal), and walking in
ends the run in victory and opens the Cursed Dungeon - the cells under the castle.

**Arriving early is allowed.** The gate is shut until the Constable falls, so a player who runs the
bridge in three minutes holds the gate yard for twelve, under his archers on the walls (the yard is
the most dangerous ground on the bridge before 15:00, by design: volleys every few seconds). It is a
choice, not an exploit.

**The objective line** under the run timer, where a side event's goes, is a slim strip: the player,
the front and the gate on one line. The bridge's goal is not a secret, so showing it is no pointer.

### 3.4 What makes a travel level fun under a horde

1. **A slow, visible pressure from behind** (the front), so standing still is a choice with a timer,
   never a sudden death.
2. **Something pulling forward**: the castle rises over the horizon ahead as he nears it - a
   parallax backdrop that *moves*, never scales, so it stays on whole pixels (`BridgeBackdrop`) -
   and each gatehouse is a milestone he can see coming.
3. **A rhythm**: span, narrows, yard. The narrows funnel the horde into an area spell's best minute
   and put murder-holes over the player's head; the yards are wide enough to circle.
4. **Pushing into the horde, not away from it.** Most of the swarm comes from ahead, so the usual
   kite - backing off - is wrong here; he has to cut through. The facing spells (Stormsweep, Sleet
   Runes, Cone of Cold) and the piercing lines have their chapter.
5. **Small reasons to step aside**: bastions bulge off a few spans with a chest or a secret (3.9).
6. **No reason to walk back**, except his own orbs at the edge of the fall.

### 3.5 The cast: the gate's garrison

`StageBestiary.Bridge`; format of `.ai/bestiary.md` section 4. Stand-ins are the Emberdeep's. Nothing
may echo the Constable (a tall armoured figure with a ring of keys and a halberd).

| role | creature | scene | what it is | attachment that reads | stand-in |
|---|---|---|---|---|---|
| Bulk | Gate Pikeman | `BridgePikeman` | a taken soldier of the gate | the pike upright, its head above the helm | Slag Thrall |
| Fast | Bridge Hound | `BridgeHound` | a gaunt war-dog | a spiked collar; long low line | Chainhound |
| Heavy | Pavisier | `BridgePavisier` | a soldier behind a standing shield | the pavise, taller than he is | Iron Bulwark |
| Wall | The Ram | `BridgeRam` | a roofed battering ram, walked by legs under it | the roof, wider than anything on the bridge | Kiln Brute |
| Ranged | Crossbowman | `BridgeCrossbowman` | a soldier with a crossbow held level | the bolt floating at the stock | Kindler |
| Turret | Gargoyle | `BridgeGargoyle` | a stone gargoyle on the parapet; built, it burns | crouched wings on a plinth | Flue Mask |
| Charger | Gate Knight | `BridgeKnight` | a knight on foot with a couched lance | the lance level and forward | Ironjaw |
| Exploder | Pitch Barrel | `BridgePitchBarrel` | a barrel of burning pitch, rolling; built | the bung and the lit fuse | Burst Bellows |
| Slammer | The Warder | `BridgeWarder` | a gate guard bringing a maul down | the maul's head highest | Trip-Hammer |
| Summoner | Houndmaster | `BridgeHoundmaster` | keeper of the gate's dogs. Minion: **Bridge Hound** | a leash-pole held up | Kennelmaster |
| MiniBoss | The Porter | `BridgePorter` (48) | keeper of the gate's windlass | the great windlass key across his back | The Chainwright |

### 3.6 The boss: the Constable

`BridgeConstableBoss`, `bridge_constable`, health about 11,800 (first guess, after the Warden's
11,000). *"He holds the gate from the wall. Get to the gate."*

- **The verb no other boss asks: advance under fire.** He fights from the wall-walk above the gate,
  untargetable (`Enemy.SetTargetable(false)`), directing volleys onto the yard: lines of crossbow
  bolts across it and rings of poured pitch (`TelegraphedGroundHit`, generous tells, 3-4 damage).
  When the player has stood within 280 px of the gate's foot for 1.5 s, he drops into the yard,
  targetable, and fights there for about twelve seconds: a halberd charge down the yard (a `Line`,
  the Gaoler's shape at the end of a travel level) and a `Cone` sweep. Then he climbs back.
- `BuildPhaseThresholds()` `{0.66, 0.33}`; each threshold sends him back to the wall at once and
  thickens the volleys, so the yard has to be crossed three times.
- **Where he appears** is the gate (`ChapterRules.BossSpawnPoint`), not near the player.
- Primitives only: `SetTargetable`, `TelegraphedGroundHit`, `GroundSlamAttack` `Line` and `Cone`. No
  shared edit. `_BossProbe --stage=11`.

### 3.7 Terrain and art

All generated (`tools/art/`), never painted:

| what | generator |
|---|---|
| the deck: laid setts, non-directional (a grid floor cannot carry diagonal ruts) | `terrain_bridge.py` (floor material `deck`) |
| the `abyss` water style: surface and tones | a new style block in `water.py` |
| the parapet strip at 45 degrees, its breaches, banners | `props_bridge.py` |
| the four gatehouses (towers over the deck, with collision on their piers) and the gate in the castle wall | `props_bridge.py`, placed by `BridgeRules` |
| the castle rising on the horizon, in three heights for the backdrop | `bridge_backdrop.py` |
| detail: cracks, scorch, dropped bolts, pitch stains | `detail_bridge.py` |
| the falling deck at the front (particles drawn in code) | none |

### 3.8 Hazards

The front; **murder-holes** in each gatehouse passage (pitch poured in telegraphed rings as the
player passes under: a reason not to fight in the narrows); **gaps** in the deck on two spans (the
existing `pit` hazard terrain); the **archers' volleys** over the gate yard before 15:00.

### 3.9 Secrets

(Achievements in `.ai/achievements-and-secrets.md` 8.12.)

- **The Toll Stone** (touch): coins left on a stone by those who crossed before. 30 Arcane Energy.
- **The Span Lantern** (linger 2 s, on a bastion): light it and the collapse waits for 30 s.
- **The Gargoyle's Perch** (strike 500 near a gargoyle on a dead plinth that does not move): it falls
  and a chest is under it.
- **The Names on the Parapet** (keepsake; gather: five sets of marks scratched into the parapet by
  those who crossed before him, spread along the bridge). Absence: names, never bodies. 150 Arcane
  Energy, once.

---

## 4. Cursed Dungeon, in the castle (`stage_1`)

*"He keeps a wizard in one of these cells."* The cells under his castle: the way in, since the gate
leads only to the yard. The chapter as it is, moved.

### 4.1 What moves with it, and what changes

| part | in the castle |
|---|---|
| **the id and every flag** | unchanged: `stage_1`, `--stage=1`, `BestTimeByStage["stage_1"]`, the run log |
| **the ground** | unchanged: the endless block maze, 6-tile corridors, braid, loops and halls (`.ai/terrain.md` sections 6 and 7), and its navigation, spawn and recycle rules |
| **its look** | **unchanged** (4.2) |
| **the cast** | unchanged: the crypt's dead and his jailer (`.ai/bestiary.md` 4.2), and still every other chapter's stand-in. Its table (mean 1.35, the game's second-toughest) sits naturally between the Bridge's and the Court's |
| **the boss** | the Gaoler, retuned for chapter 11 (4.3) |
| **the side event** | unchanged: the Gaoler's key, its map wayfinder, and Tituba (1.6). Its hint line (`StageHintCatalog`) unchanged |
| **its spell vault and secrets** | unchanged, and one added (4.4) |
| **its music** | unchanged: B Locrian over a B drone, 4/4 at 132 (`MusicCatalog.Dungeon`) |
| **its hazards** | unchanged: pits, and spikes and vents in the middle of the corridors |
| **its text** | the flavour line changes (1.4); the corruption line stays |
| **the start** | a stair foot drawn in the spawn clearing - the way down from the bridge's gate. A prop, optional, the only new art |
| **its deed** | unchanged: *Dungeon Conqueror* pays Shadow Bolt |

### 4.2 Its look in a castle: it stays

The jail walls were drawn to Eric's *"nicely detailed stone jail walls ... skulls, sconces, and a
bunch of details to make it look like a dungeon jail"* (`.ai/terrain.md` 6): cut stone faces, cells
with barred windows, chains, and sconces that burn **his** cold `arcane` light. That is exactly a
castle's cells, already lit by the castle's owner. The flagstone floor is a laid floor, somebody's
building. Nothing has to change for it to read as under his house; the transition is the bridge's
gate and the stair at the start. Redrawing it would be cost with no reader.

### 4.3 The Gaoler as a castle boss

The fight is unchanged - the lane he shows first and then runs, mounted, then on foot at half health
at a range that kills, then the enrage - because it is already the maze's question: *sidestep, never
back*, in corridors where back is the only obvious way. What changes is his numbers, in his own
`BossCatalog` block:

| | at chapter 5 | at chapter 11 |
|---|---|---|
| health | 6,200 | **about 12,500** (between the Constable's 11,800 and the Seneschal's 13,000) |
| Arcane Energy bonus | 200 | **375** |
| Boss Rush health | (his catalog health) | **6,200, frozen** (`BossRushHealth`, 1.8) |

Measure with `_BossProbe --stage=1` and a balance-bot run. If at 12,500 the fight is long rather than
hard, add a third phase at a third of his health - two lanes in a row, the second at right angles, a
maze corner - in `GaolerBoss.BuildPhaseThresholds`/`OnPhaseEntered`. Measure first.

### 4.4 Its secrets

Unchanged from `.ai/achievements-and-secrets.md` 8.5 - Chalk Marks, the Alarm Bell, the Hollow
Flagstone, the Gaoler's Ledger (its keepsake), **the Cold Forge** (its spell vault: Molten Shard), the
Empty Cell - and one added, moved from the first draft's Undercroft:

- **D7, the Oubliette** (untouched for 5 s on a grate in a dead-end hall room's floor): a voice
  below. If Merlin is built (decision 11) this is where his card stops being a sealed blank and
  becomes a silhouette: someone else is still held here. Until then it pays 150 Arcane Energy, once.

The Gaoler's Ledger now arrives late in the game, and the Court's Library a chapter later reveals the
hint of every secret, not only the keepsakes. The Ledger keeps its design; its reveal is simply the
smaller and earlier of the two.

### 4.5 What the move dropped

The first draft's **Undercroft** - a new chapter of rooms joined by narrow passageways on the maze's
lattice, its eleven-creature cast, and **the Watchman**, a boss whose lantern beam walls and pillars
block (*take cover*) - is not built. It is shelved, not lost: the rooms-and-passages look is a
candidate for another chapter's ground (section 7), and the Watchman's occlusion primitive for a later
boss. Neither is planned.

---

## 5. The Court (`stage_12`)

*"A court with no one left in it."* The halls of his house: galleries, a great hall laid for a feast,
a chapel, a library, rooms behind rooms. Eric's labyrinth.

### 5.1 Its shape: one building, the same every time (decided)

**The Court is finite and fixed**: an `Authored` topology, the first stage whose ground is read
from a file rather than a function. `tools/art/court_layout.py` lays it out once - hand-placed rooms
joined by generated halls, from a fixed seed - and writes `court_layout.json` (each cell's kind, each
room's rectangle and id, each door and each hidden passage). `LevelGenerator.Authored.cs` reads it;
cells outside the building are wall. The seam rule holds trivially: a lookup is a pure function.

**Why fixed, not seeded per run**: a building does not move. A player who has found the Great Hall
can find it again, which turns the labyrinth from a maze into a *place* - and it makes every room
achievement free of luck, which the hidden ones need.

**Size**: 6 x 6 chunks (9,216 px square; about a minute corner to corner with nothing in the way).
The player enters at the **south-west** corner, up the stair from the cells; the **throne room doors
are at the north-east**. Up and right again, the bridge's direction, all the way to him.

```
  +----------------------+--------------------+------------------------+
  | Solar (hidden)       | Treasury           |   THRONE ROOM DOORS    |
  |                      |   - gold wing -    |                        |
  +----------+-----------+---------+----------+------------------------+
  | Library  | Gallery of          | Minstrels'| GREAT HALL            |
  |  - blue  |  portraits          | gallery   |  (red wing)           |
  |  wing -  +---------------------+ (up a    |                        |
  | Chapel   |      Well Court (open sky)     |  Kitchens              |
  +----------+---------------------+----------+------------------------+
  | Armoury  | Kennels             | Map Room | the Keepsake Doors     |
  |  - green wing -                |          |                        |
  | START: the stair from the cells|          |                        |
  +--------------------------------+----------+------------------------+
```

### 5.2 Readable on a phone

- **Halls are 8 tiles wide** (384 px), galleries 10: on a phone, both walls and the swarm between
  them are on screen at once (847 px across).
- **Each wing has its own carpet runner** down the middle of its halls (red, blue, green, gold, from
  the deep half of each ramp: pigment, not light), so "which part of the house am I in" is answered at
  a glance. Hue is never the only carrier: the runners differ in border pattern too, for colour
  blindness.
- **Every door has a carved lintel glyph** of what is behind it - a cup for the Great Hall, a book for
  the Library, a key for the Treasury - drawn into the wall art. Signposts without words (tone rule
  2). A hall junction has the glyphs of the rooms down each branch.
- **Few junctions.** The halls are a loose ring round the Well Court with spurs to the rooms, not a
  maze of equal corridors; a player who keeps a wall on one hand gets everywhere that is not hidden.
- **The Map Room** draws the Court for the rest of the run: a small map in a corner, rooms found
  marked (the game's only minimap, and only here, and only once earned).
- **After the boss, a pointer**: the way home is the throne room doors, so the existing off-screen
  badge (`WorldPointer`, the portal's) points at them across the house.

### 5.3 What a run is

The ordinary run, with the Court's rooms as what there is to do in it. At 15:00 the Seneschal comes
(5.6) and is pursued through the halls. When he falls, **the throne room doors open** at the
north-east (`ChapterRules.VictoryPortalPoint` at a fixed point) and the walk there - through a
house whose swarm has stopped, past the rooms - is the last minute of the chapter. Walking through
them ends the run in victory. The Throne is then open if every wizard is free (1.6); if one is still
held, the doors' card says who.

### 5.4 The rooms

Every room is a place in the layout with its own behaviour (`CourtRooms.cs`: one small component per
room kind, attached by `CourtRules` as the room's chunk streams in, state kept by the room's id in
the secret run state, never on the node). Finding a room for the first time is an achievement
(`.ai/achievements-and-secrets.md` 8.12); "Every Room" is a Feat.

| room | where | what it does | run reward | once ever |
|---|---|---|---|---|
| **The Great Hall** | north-east, red wing | **Eric's dining room.** Three long tables laid for a feast for a court that is not coming. On the platters, **twelve roast fowl, each a pickup that heals like a flask** (10 HP). One returns to a platter every 60 s, so the Hall is somewhere to come back to. The horde walks through it and touches nothing | heals | 50 Arcane Energy |
| **The Kitchens** | beside the Hall | the ovens the fowl came from: flame vents; a cook's larder | two flasks | page |
| **The Chapel** | west, blue wing | a ward: for 20 s after he enters, the horde will not cross the threshold (the Broken Ward's repelling edge). Re-arms after 90 s. Bolts still come through the door | a breather | page |
| **The Library** | west, blue wing | his record of what he took | - | **every secret in the game gets its hint on the achievements page** (`.ai/achievements-and-secrets.md` 4.3) |
| **The Armoury** | south-west, green wing | racks of arms | 60 s of +15% Armor, and a relic chest from the budget | page |
| **The Kennels** | south, green wing | empty kennels, a broken chain. With the Familiar's hound in the loadout, the hound finds something in the straw | a chest (with the hound) | page |
| **The Map Room** | south | a table map of the house | the Court's map for the run (5.2) | page |
| **The Treasury** | north, gold wing | the Court's keepsake: guarded by the Chamberlain (the miniboss) | a chest | **150 Arcane Energy** |
| **The Gallery** | middle | portraits of the wizards he took, their faces covered; those the player has freed are uncovered | heal 50% | page |
| **The Minstrels' Gallery** | above the Great Hall, reached only by a back stair from the Kitchens (a place to reach) | a balcony over the feast | a chest | page |
| **The Solar** | north-west, **hidden** (5.5) | his own chamber | a relic chest | page |
| **The Keepsake Doors** | south-east | a corridor of ten locked doors, one for each campaign chapter's keepsake. Every keepsake the save has found opens its door: behind it, that keepsake in an alcove, and a heal | heals | **"Every Door"** (all ten): 500 Arcane Energy |
| **The Well Court** | the middle, open to the sky | a breather; the house's one view of the tower | - | - |

**Unlockables hidden within**, as Eric asked: the Library's reveal, the Treasury's keepsake, the
Keepsake Doors (which turn every secret found on the road into something in his house), and the
Solar. No room pays a spell or a wizard (none is designed; a new spell, if one is ever wanted for the
endgame, would close nothing here, because the gate counts no spells).

### 5.5 Hidden rooms are found with the feet

No secret door is a wall that breaks: that would be the first change to terrain at runtime, a system
of its own (`.ai/stage-secrets.md` lists the Lily Crossing as the same L-sized job). Instead a hidden
way is **a real gap in the wall with a tapestry hung over it** - decor drawn over walkable floor. The
tells are the ones `.ai/stage-secrets.md` already uses: a draught of motes drifting out of it, a
tapestry hanging a pixel off true, and - the best tell in the game - **enemies walking out of the
wall**, because the flow field knows the gap is floor. No runtime terrain change, no new system.

### 5.6 The boss: the Seneschal

`CourtSeneschalBoss`, `court_seneschal`, health about 13,000. *"He leaves when it suits him.
Follow."*

- **The verb no other boss asks: pursue.** He fights in a room with his staff of office (a `Ring`
  slam, and a `Line` of household guards he calls across the room as a wall that walks). At 75%, 50%
  and 25% he steps out through a servants' door: untargetable (`SetTargetable(false)`), he travels
  through the walls (a long `BurrowMove`) to another room one or two rooms away, and the room he left
  fills with his household. The off-screen boss badge points the way; reaching his room resumes the
  fight.
- It asks for what the Court has taught: knowing the house. In the open his flight would be a teleport
  with a pointer; in the Court the player has to choose a way round.
- Needs the Court's layout to name rooms near a point (`CourtLayout.RoomsNear`), so it is built after
  5.1. `_BossProbe --stage=12`.

### 5.7 The cast: the household

| role | creature | scene | what it is | attachment that reads | stand-in |
|---|---|---|---|---|---|
| Bulk | Lackey | `CourtLackey` | a liveried servant | a covered salver held up | Slag Thrall |
| Fast | Court Hound | `CourtHound` | a lean sighthound | the narrow head and arched back | Chainhound |
| Heavy | Household Guard | `CourtGuard` | a guard behind a kite shield with no device on it | the shield | Iron Bulwark |
| Wall | Empty Armour | `CourtArmour` | plate with nobody in it; built, fire behind the visor | great helm and pauldrons | Kiln Brute |
| Ranged | Falconer | `CourtFalconer` | a falconer; his hawk is what he throws | the hawk on the glove | Kindler |
| Turret | Candelabrum | `CourtCandelabrum` | a standing iron candelabrum; built, its candles burn his fire | the branching arms | Flue Mask |
| Charger | Duellist | `CourtDuellist` | a courtier lunging with a rapier | the blade out in the lunge | Ironjaw |
| Exploder | Censer | `CourtCenser` | a great censer on legs, swelling with smoke; built | the pierced dome and its chains | Burst Bellows |
| Slammer | Usher | `CourtUsher` | brings his staff of office down | the staff's knob highest | Trip-Hammer |
| Summoner | Huntsman | `CourtHuntsman` | winds a horn for the court hounds. Minion: **Court Hound** | the curled horn raised | Kennelmaster |
| MiniBoss | The Chamberlain | `CourtChamberlain` (48) | keeper of every room | a ring of keys the size of a shield | The Chainwright |

### 5.8 Terrain and art

`court_layout.py` (the layout and its JSON; it also checks itself, 9.3), `terrain_court.py` (floors
per wing: marble, oak boards, flags; the carpet runners as an overlay terrain), `court_walls.py`
(panelled walls for every wall role, with dressing: tapestries, lintel glyphs, door frames,
portraits), `props_court.py` (the banquet tables and platters, the fowl as a pickup sprite, pews,
lecterns, shelves, racks, the map table, kennels, the throne room doors).

### 5.9 Hazards

The Kitchens' ovens (flame vents); in the Great Hall, when the Seneschal fights there, **falling
chandeliers** (telegraphed rings). The halls themselves are clear: the Court is hard because of where
things are, not what is on the floor.

---

## 6. The Throne (`stage_13`)

*"He is here."* Not a map: one hall, closed behind him. Opened by the Seneschal's fall **and every
wizard freed** (1.6).

### 6.1 The room

A bounded hall, **40 x 40 tiles (1,920 px square)**, as an `Authored` layout of its own
(`throne_room.py` writes it; the same reader as the Court). Square, because the canvases are mirror
images of each other: a phone sees most of its height and half its width, a PC the reverse. Two rows
of four pillars (solid, layer 4) give it something to move round. The throne on a dais at the north.
The doors he came in by close behind him.

**The light** is the one in the game that is his: the `darkness` ramp as emission, which
`.ai/world-and-tone.md` has held back for this (*"Holding that back across eight chapters is what
makes it land"*). Everything else in the room is unlit stone and the player's light.

### 6.2 The Muster: a build in one breath (decided)

Eric: *"the player gets a bunch of levels as soon as the fight starts and can essentially make a
build"*.

- **At the start, the run is paused and the player is granted 17 levels at once** (level 1 to 18),
  each through the ordinary level-up screen, one after another - the queue `Node2DGame` already
  keeps for level-ups that arrive together (`queuedLevelUps`, `LevelUpImmediately`). Then **two relic
  chests** open, through the chest screen.
- **Everything about a level-up is as usual**: the wizard's own spell offered, rerolls refilling,
  bans, the Ascension strip from level 17 (`Player.AscensionMinimumPlayerLevel`). It is the whole
  game's build decisions made in one sitting, which is what "make a build" means.
- **The screen says it is the Muster**: its heading reads "The Muster - 5 of 17".
- **The arming pause** (`.ai/level-up-screen.md` 4g, 0.8 s, so a click meant for the battlefield
  does not choose a card) is kept on the first screen and shortened to 0.3 s on the rest: long enough
  that the click closing one cannot choose the next, short enough that seventeen screens are not
  fourteen seconds of waiting.

### 6.3 The procession (decided)

**The pace clock is pinned at 15:00** (`ChapterRules.PinnedPaceSeconds`): the world is at its end
state from the first second, which is what a level-18 build should meet. The minions are his
household guard - the Court's Heavy, Wall and Ranged, and the bridge's knights - on the end-of-run
spawn curve.

Then **his wardens, called home, two at a time**, each at half its chapter health:

| wave | who | the question, together |
|---|---|---|
| 0 | the household guard alone, 45 s | a warm-up for the new build |
| 1 | **the Hayward and the Gaoler** | go round one while you sidestep the other's lane |
| a breather, 20 s | a chest | |
| 2 | **Mother Rot and the Archivist** | kill what heals one while the other's sweep moves you |
| a breather, 20 s | a chest | |
| 3 | **the Still Warden and the Long Coil** | hold ground, and burst a window: the two demands at odds |
| 4 | **him** | 6.4 |

Six of the road's wardens, in pairs whose demands pull against each other: a question no chapter
asked, because no chapter had two. Elderbark, the Choir, the Ninth Stone and the Warden of the Deep
sit it out (Elderbark asks nothing a pair adds to; the Choir's three bodies and the Ninth Stone's
rings in a closed room are unreadable beside another boss; the Warden is the recap already). The
Gaoler's half health is half his castle health (4.3).

**The bosses were tuned in open fields.** In a closed 1,920 px room the Gaoler's lane, the Long
Coil's dives and the Archivist's orbit all change. `_BossProbe --stage=13` walks the procession, and
each pair is retuned there, in the throne's own data, never in the bosses' scenes.

### 6.4 The final boss

`ThroneDarkWizardBoss`, `throne_dark_wizard`, health about 18,000. Never named on screen; the banner
says *"He takes what you have. Take it back."*

**The verb no other boss asks: take it back.** He took every wizard's magic. Now he reaches for the
player's.

- **Phase 1 (to 66%): he takes.** Every 12 s a hand of his dark light flies at the player (a slow
  projectile with a long tell: dodge it and nothing happens). If it lands, **one of the player's
  spells is sealed**: it stops firing, its slot on the loadout strip wears his mark, and the seal - a
  dark orb with that spell's icon inside it - drifts from the player back toward him at about 60 px a
  second. **Touch the orb and the spell is back at once.** If it reaches him, the spell stays sealed
  for 20 s. He is targetable throughout.
- **Phase 2 (to 33%): his light.** The hall goes dark but for him and the player, and he fires lines
  of his dark light across it (`TelegraphedGroundHit`). The takings continue. Darkness dealt *to him*
  counts 25% more: `.ai/world-and-tone.md`'s *"a player can end up throwing his own element back at
  him"*, made true.
- **Phase 3 (to 0): all he took.** Between takings he casts, one every 6 s, the telegraph of each
  warden the player has beaten in the procession - the Hayward's cone, the Gaoler's lane, the Still
  Warden's falling ceiling - as ground hits placed round the player. The road, thrown back at him one
  last time.
- **New shared primitive, this agent's only**: a sealed spell slot on `Player`
  (`SealSpell(slot, seconds)`, read by the firing loop in `spellFireTimers`, cleared by the orb) and
  its mark on `LoadoutHud`. Everything else is `TelegraphedGroundHit`, `GroundSlamAttack` and
  `SetTargetable`. `ValidateBossPatterns` holds his tells to the usual rules.

**The end.** When he falls his dark light goes out, and the warm light he took leaves his body as a
storm of motes that fly off the screen in every direction: the magic going home. The doors behind the
throne open; walking through them ends the run in victory. The results screen carries an ending panel
with every freed wizard standing in a row, named and nothing else (no copy carries the mood), then
the credits.

### 6.5 How it differs from Boss Rush

| | Boss Rush | The Throne |
|---|---|---|
| where | the Emberdeep, streaming ground | one closed hall |
| the build | earned from level 1 through an ordinary run | made at the start, in the Muster |
| length | about 27 minutes (measured) | about 10 |
| bosses | the road's ten wardens, one at a time, in the road's order, scaled to when they arrive | six, in pairs, at half health, then him |
| the end | the Warden of the Deep | the dark wizard, the story's end |
| opens | every boss on its roster beaten (1.8) | the Seneschal beaten and every wizard freed |
| records | most felled, fastest clear | the deed, and a best time |

Boss Rush itself is unchanged in what it is (1.8).

### 6.6 The Apotheosis in the Throne

The Apotheosis floor (no earlier than 7.5 minutes for the planned bot) and the targets of
`.ai/gameplay-design.md` are about an ordinary run. The Throne grants levels on purpose, so it has
its own contract, and an ordinary run is untouched by it:

- **The Muster stops at level 18.** The Ascension (player level 17 and a spell at 8) is reachable
  inside it for a planned build: seven picks bring a spell to 8.
- **The Apotheosis needs level 23**, so five more levels have to be killed for. At the pinned pace an
  ordinary kill pays 5 XP (2 x 2.5) and a boss 125: levels 18 to 23 cost 5,275 XP, which at the
  end-of-run kill rate is about five minutes - **during the third pair or with him**. The form is
  then the build's payoff in the last fight, which is the right place for it.
- Measured before it ships: `_BalanceHarness --stage=13 --all-unlocked --seek-apotheosis --immortal`
  records when the form is taken, and the Muster's size moves until it lands in the third wave.
- The Throne's results never feed the balance tables of `.ai/run-pacing.md`; its bot runs are their
  own line.

---

## 7. More variety: the other chapters

### 7.1 What the chapters are today

| chapter | topology | ground (palette) | blockers | hazards | goal | side event |
|---|---|---|---|---|---|---|
| Enchanted Forest | open, endless | grass, moss, meadow | ponds, trunks, the pen's fence | - | survive, boss | hold the grove |
| The Fallow Fields | open | stubble, furrow, hedge bank | millponds, fences | - | survive, boss | defend the well |
| The Broken Circle | open | heather, peat, granite | tarns, standing stones | - | survive, boss | relight the moot |
| Sunken Cave | **caverns**, endless (V2, built) | its own: `cave_floor`, silt, scree; `cave_wall` rock | rock, pools in chambers | - | survive, boss | - |
| Cursed Dungeon | **maze**, endless | flags | jail walls, water | pits, spikes and vents in the corridors | survive, boss | the key |
| Blighted Swamp | open | mud, moss | much water | - | survive, boss | the lanterns |
| Mystic Ruins | **broken walls** on the maze, endless (V1, built) | graystone, cobble; `ruin_wall` | broken walls, pillars | spikes in hallway rows (the pits went) | survive, boss | - |
| Frozen Waste | open | graystone, ice | water | - | survive, boss | the thaw |
| Scorched Sands | open | sand, scree | - | rocky pits | survive, boss | the caravan |
| The Emberdeep | open | basalt | - | lava, pits | survive, boss | - |
| The Sketchbook | open | paper | - | - | survive, boss | - |

**Ten of eleven are the same shape**: an open endless field with blobs on it, played the same way -
survive fifteen minutes, kill the boss, walk into the portal. The side events give seven of them a
second goal; the ground gives one (the maze) a second shape. With the Dungeon moved into the castle,
**the road before the gate has no walled chapter at all** until V1 and V2 land. The castle adds two
more shapes (a band, a fixed building) and three goals (travel, explore, build-and-fight).

### 7.2 One engine, many looks

The Dungeon's block maze is a **connectivity engine**: a graph of cells, each block connected inside
and to its four neighbours, proven connected, endless, streamed, with a flow field and spawn rules
that understand it. What is drawn in its walls is a separate choice. The proposals below draw broken
walls, rock and water in it. Each inherits the proof and the navigation for nothing, which is why they
are cheap.

### 7.3 Proposals, ranked by value against cost

| rank | chapter | proposal | value | cost |
|---|---|---|---|---|
| 1 | **Mystic Ruins** (chapter 6) | **Broken walls.** The maze lattice at the hall and loop rates `.ai/terrain.md` section 6 found too open for a dungeon (18% loops, 35% halls: *"the walls broke up into isolated slabs - an open floor with furniture"*), which is exactly a ruin, plus a pass that breaks long walls into stubs. Its flavour text already says *"broken spires and shattered walls"*. Cover, partial corridors, sightlines that open and close | **high, and higher now**: with the Dungeon gone from the middle, this is one of the two walled chapters left before the gate | **S**: parameters and a ruin wall set (`ruin_walls.py`); the maze's navigation, spawn rules and hallway traps come with it, and the Ruins already has spikes |
| 2 | **Sunken Cave** (chapter 4) | **Caverns.** The lattice drawn as rock: passages of varying width (4 to 10 tiles, hashed per passage), rooms as chambers, walls as rounded rock masses. Connectivity is kept by one rule: the middle 4 tiles of every passage the maze opens are never rock, and noise only decides the rest. Its own palette at last (wet stone, pools, crystal), and a cavern pocket for the Geode Heart | **high, and higher now**: the one chapter on borrowed ground, whose fiction is *"dripping tunnels of black rock"*, and now the first walled step of the campaign | **M**: a cave pass on the lattice and a cave wall set; the far half is art |
| 3 | **Blighted Swamp** (chapter 5) | **Islands and causeways.** The lattice drawn as water: cells are islands, open passages are causeways and boardwalks, closed walls are open water | medium | **M**: the lattice with water for walls (water already draws from its shape) and a boardwalk detail set. Watch the side event's lanterns, which must stay on reachable ground |
| 4 | **The Emberdeep** | **Rivers of fire.** Lava in long channels rather than blobs - the isolines of a slow noise, a pure function of the world tile - with fords and bridges where the noise dips | medium | **M**: a channel stamp; lava is a hazard, not a blocker, so no new blocking terrain |
| 5 | **Scorched Sands** | **Storm fronts.** A sandstorm crosses the map on the pace clock, lowering sight and pushing everything with it for 30 s | low to medium: a moment, not a shape | **M**, and a readability risk on a phone |
| 6 | Enchanted Forest | Paths through dense wood (the lattice drawn as thicket) | low: a maze in chapter one is what Eric moved the Dungeon away from | **M**; **not recommended** |
| - | (shelved) | **Rooms and passageways**, the first draft's Undercroft topology (4.5): a chapter's look if one is ever wanted | - | **M** |

**Decided: 1 and 2 first.** With the move they are no longer only variety: they restore walled ground
to the middle of the campaign (1.7), so they should land **before or with** the Dungeon's move.

**Built (2026-10-30, `wip/variety-walls`): both.** What each became, where the proposal above changed in
the building (the Cave's graph at a 16-tile pitch rather than the Dungeon's 8, chambers as well as halls;
the Ruins' pits removed), the proof that the caverns have no sealed pockets, the two bosses' placement
fixes and the probe and harness numbers are in `.ai/terrain.md` section 11.

---

## 8. The world map

`.ai/world-map.md` reads a data-driven `PLACES` list in `tools/art/world_map.py` (branch
`wip/world-map`, not yet merged) and already expects this: *"They come after the Emberdeep, so the
world grows east: widen `W`, move the page's `PAGE_ORIGIN` with the edge, and the route, the compass
and the camera follow from the JSON."*

- **The keep leaves the river.** Today the Dungeon is drawn as *"a keep on a crag where the river
  comes out of the hills: the one way through"*, its stop at about (800, 642), and the road from the
  Cave to the Swamp runs through it and over the road's one bridge (`WAYPOINTS`: *"keep -> over the
  bridge to the bog"*). The keep's landmark comes off the crag and the `stage_1` stop leaves that
  spot; the road from the Cave runs straight to the bridge over the river and on to the bog. The
  bridge itself stays, unguarded: the river still has to be crossed. The crag stays as hills.
- **The castle.** Past the Emberdeep's black cliffs, the dark sea; the Last Bridge from the cliffs to
  a rock standing in it; on the rock his castle - **the Dungeon's stop at the castle's foot** (the
  sea-gate down to the cells; the keep's jail, which already belonged to him, now drawn under his
  walls), the walls and roofs of the Court above it, and the tower (the Throne). `W` grows from 1680
  to about 2100; the page (`PAGE_ORIGIN`) moves east with the edge.
- **Four stops in castle order**, roughly: `stage_11` with its stand point at the bridge-foot on the
  cliffs (about 1700, 470), `stage_1` at the sea-gate (about 1890, 380), `stage_12` on the walls (about
  1950, 300), `stage_13` at the tower's top (about 1990, 220). The road runs over the Last Bridge
  between the first two. Final coordinates are the map agent's.
- **The dark light moves.** The world map puts the one `darkness` emission on the Emberdeep's summit
  (*"his seat"*). With the castle, that light belongs on the tower; the summit keeps its fire.
- **The stop order changes in one rule.** It is `[campaign] + [Boss Rush] + [outside]` today, and
  the route visits stops in that order. Boss Rush stands on the Emberdeep's summit, so it must come
  *between* the road and the castle: `[campaign up to the gate chapter] + [Boss Rush] + [the rest of
  the campaign] + [outside]`. Otherwise the road would run back from the tower to the summit.
  `ValidateWorldMap`'s rising arc lengths then still hold.
- **A grandfathered Dungeon** (1.5) is simply an open stop beyond fogged ones: the wizard walks the
  road to it as he walks to any stop, and it can be started. It looks odd on an old save for as long
  as that save is short of the gate, and never on a new one.
- **`StageRewards`** lists every chapter's holdings from the catalogs, so the Dungeon's card keeps
  Tituba, Shadow Bolt and its secrets wherever its stop is drawn. Its card says *"Chapter 11 of 13"*;
  the Throne's *"Chapter 13 of 13"*.
- **`.ai/world-map.md` section 1's table** (the Dungeon's row and its reason, *"a gate in the middle of
  the campaign"*) is rewritten by the map agent.

---

## 9. The build plan

The pattern of `.ai/campaign.md` section 7: **one plumbing agent first, then one agent per piece in
its own worktree**, each creating files and editing only blocks already marked as its own.

### 9.1 The pieces

| piece | what | owns (creates, or edits only its own block) | depends on |
|---|---|---|---|
| **C0 Plumbing** | 1.9 entire: the three new `StageCatalog` entries and their place in the order, `GateChapter`, the validator's campaign rule, `BossRushCatalog.Arena`, `BossCatalog` `[11]`-`[13]` on stand-ins, the Bridge's and the Court's rosters, environment and palette blocks, the two topology partials as stubs, per-chapter wall art lookup, `ChapterRules` and its seven hooks in `Node2DGame`, `ChapterRulesCatalog`, music and side-event stubs, and the Throne's wizard gate value | `StageCatalog.cs` (new entries), `BossCatalog.cs` (new blocks), `BossRushCatalog.cs` (`Arena`), `StageBestiary.Bridge.cs`, `StageBestiary.Court.cs`, `StageEnvironmentCatalog.cs`, `LevelGenerator.cs` (+ partial stubs), `LevelTilePainter.cs` (wall lookup only), `Node2DGame.cs` (hook calls only), `ChapterRules.cs`, `ChapterRulesCatalog.cs`, `MusicCatalog.cs`, `SideEventDirector.cs`, `RegressionChecks.cs` (campaign rules) | **A1 merged** (the gate kind, per gate chapter) |
| **C1 The Last Bridge** | section 3: the `Band` topology, `BridgeRules` (front, spawns, boss point, gate, objective line), `BridgeCollapseFront`, `BridgeBackdrop`, the bridge's art | `LevelGenerator.Band.cs`, `BridgeRules.cs`, `BridgeCollapseFront.cs`, `BridgeBackdrop.cs`; `terrain_bridge.py`, `props_bridge.py`, `detail_bridge.py`, `bridge_backdrop.py`; **the `abyss` block in `water.py`**; its environment block; its line in `ChapterRulesCatalog` | C0 |
| **C2 The Dungeon moves in** | 1.4-1.8 and section 4: the Dungeon's line moved in `CampaignOrder`; schema 20 and `GrandfatheredStageIds` (1.5); the road rules in `IsStageEarned`/`IsStageAvailable`; the victory guard; Boss Rush held (`RoadOrder`, `IsOpen`, `BossRushHealth`); the Gaoler's retune; the Dungeon's text and the stair prop; the locked hints that name who is missing; `ValidateDungeonMove` and `ValidateCampaignMigration`'s updated expectations | `StageCatalog.cs` (the order's `stage_1` line, the `stage_1` entry's text, `LockedHintFor`), `GlobalStatsManager.cs` (stage rules), `SaveData.cs` (the schema 20 step and field), `BossRushCatalog.cs` (`RoadOrder`, `IsOpen`), `BossCatalog.cs` (the `[1]` block, `BossRushHealth`), `Node2DGame.cs` (`ApplyBossVictoryUnlocks` only), `RegressionChecks.cs` (its two validators), `tools/art/props_dungeon.py` (new: the stair) | C0 and A1 merged. **Merges in the same release as the Bridge's flip**, and after V2 (and ideally V1) |
| **C3 The Court** | 5.1-5.5, 5.8-5.9: the layout generator and its JSON, the `Authored` reader, `CourtRules`, `CourtRooms`, `CourtLayout` (rooms near a point, for 5.6), its art | `court_layout.py`, `LevelGenerator.Authored.cs`, `CourtRules.cs`, `CourtRooms.cs`, `CourtLayout.cs`; `terrain_court.py`, `court_walls.py`, `props_court.py`; its blocks | C0 |
| **C4 The Throne** | 6.1-6.3, 6.5: the hall's layout, `ThroneRules` (the Muster, the procession, the pinned clock), the procession's data, the level-up screen's Muster heading and short arming pause | `throne_room.py`, `ThroneRules.cs`, `ThroneProcession.cs`; **`LevelUpMenu.cs`** (the heading and the guard only) | C0, and C3's `Authored` reader |
| **C5 The bosses** | one agent each: the Constable (3.6), the Seneschal (5.6), the dark wizard (6.4, **with the `Player` seal and its `LoadoutHud` mark**). The Gaoler's retune is C2's | `<Boss>.cs`, `<Boss>.tscn`, `tools/art/boss_<name>.py`, its frames; the shared edit named | C0; the Seneschal after C3, the dark wizard after C4 |
| **C6 The casts** | two agents, the Bridge's and the Court's, by `.ai/bestiary.md` section 8's recipe: create the scenes at the paths the roster names. The Dungeon's cast exists | `tools/art/cast_<chapter>.py`, `scenes/<Chapter><Name>.tscn`, frames, PNGs | C0 |
| **C7 The music** | three pieces by `.ai/audio-direction.md`: the bridge a march under wind; the Court a court dance with no dancers; the Throne the heaviest piece in the game, whose pulse drops out in his second phase. The Dungeon keeps its own | `tools/audio/music_<chapter>.py`, `assets/music/<chapter>_*.wav` | C0 |
| **C8 The world map** | section 8, including the keep's removal from the river | `world_map.py`, the stop-order rule in `WorldMap`/`StageRewards`, `.ai/world-map.md`'s table | `wip/world-map` merged, C0, and C2's order |
| **C9 The castle's achievements and secrets** | `.ai/achievements-and-secrets.md` 8.12 and D7 (4.4), as more chapter agents there (its A5-A13 pattern) | `SecretCatalog.<Chapter>.cs`, its secret scripts | that doc's A4, and C1, C3 |
| **V1, V2** | 7.3's first two: the Ruins' broken walls, the Cave's caverns | `LevelGenerator.Ruin.cs` / `.Caverns.cs`, `ruin_walls.py` / `cave_walls.py`, each chapter's palette block | the topology partial pattern (C0); independent of the castle otherwise |
| **Flip** | `IsPlayable = true`, one chapter at a time, **last** | `StageCatalog.cs`, one line | everything that chapter needs |

**Shared files, and who alone may touch which part**: `Node2DGame.cs` (C0 the hooks, C2
`ApplyBossVictoryUnlocks`), `StageCatalog.cs` (C0 the new entries, C2 the Dungeon's line and entry
and `LockedHintFor`, the flips one line each), `BossCatalog.cs` (C0 the new blocks, C2 the `[1]`
block), `BossRushCatalog.cs` (C0 `Arena`, C2 the rest), `RegressionChecks.cs` (each its named
validators), `LevelGenerator.cs` (C0; every topology after it is a partial file), `water.py` (C1),
`LevelUpMenu.cs` (C4), `Player.cs` and `LoadoutHud.cs` (the dark wizard's agent). The generated
`tiles.json`, `detail.json` and the atlas are rebuilt by the integrator after the merges, as
`.ai/campaign.md` section 7 says - take either side in a merge and re-run `tiles.py` and
`ground_detail.py`.

### 9.2 Order

1. **A1** (`.ai/achievements-and-secrets.md`), already started: the gate kind and schema 19.
2. **C0**, alone.
3. **C1, C3, C6 x2, C7, V1, V2** in parallel. **C2** may be built now too, but holds its merge (step 6).
4. **C4** once C3's `Authored` reader has landed (or C0 lands the reader as a stub both use).
5. **C5** per boss as its chapter's ground lands.
6. **Flip the bridge, and merge C2, in one release**, after V2 (and ideally V1). Shipping the move
   alone would take the Dungeon out of the middle of the campaign with nothing at the end to reach it
   by; until the bridge is playable the Warden of the Deep would open the Dungeon directly, which
   works but is not the design.
7. **C8, C9.**
8. **Flip** the Court, then the Throne. A chapter's validator to-do line must be gone before its flag
   flips; if it says "playable but still has stand-ins", the flag went first.

### 9.3 What each piece must verify

Every piece: `dotnet build WizardSurvivors.sln` (and says so when only that ran); the validator run,
`"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit-after 300 -- --no-save-writes`, with no
new warnings and its chapter's to-do line shrinking.

| piece | also |
|---|---|
| C0 | the three chapters listed as to-do and nothing else new; `_BalanceHarness --stage=0` and `--boss-rush` unchanged (still ten, still on the Emberdeep); the campaign validator's sample saves still pass |
| C1 | `_BalanceHarness --stage=11 --walk=45 --immortal --minutes=15 --terrain-log` (the bot reaches the yard, never stands on the drop, chunk steps within budget); its kill rate against the Emberdeep's on the same seed (if the band starves the supply, the spawn split moves, not the XP curve); `--check-feet`; `ValidateTerrainSeams` on the band; `_UiShot --stage=11` at both canvases showing **both edges in the top-left and bottom-right corners**, and once with the front on screen |
| C2 | `ValidateDungeonMove`'s samples (1.5) and the updated schema-12 samples; an in-memory schema-19 save in `_UiShot` showing the stage list (or the world map, once merged) with the grandfathered Dungeon open and the bridge closed; a grandfathered win in a probe writing nothing to `UnlockedStageIds`; `_BossProbe --stage=1` at the new numbers; `_BalanceHarness --boss-rush` with the same ten in the same order and the Gaoler at 6,200 |
| C3 | `court_layout.py` checks its own output: every room reachable from the start without a hidden way, every hidden room reachable with one, the throne doors reachable, every door glyph naming a real room; `_MazeProbe --stage=12`; `_UiShot` at a hall junction and in the Great Hall at both canvases |
| C4 | `_BalanceHarness --stage=13 --all-unlocked --seek-apotheosis --immortal` (the form lands in the third wave or later, 6.6); `_UiShot --force-levelup --stage=13` during the Muster at both canvases; the whole procession under `_BossProbe --stage=13` |
| C5 | `_BossProbe --stage=N` through every phase; `ValidateBossPatterns` (no zero wind-up, cones under 90) |
| C6 | the recipe's own checks (`ValidateBestiary` shrinking) |
| C8 | `ValidateWorldMap`; `_UiShot --scene=res://scenes/WorldMap.tscn --map-at=stage_13`, and `--map-at=stage_2` showing the road from the Cave over the river with no keep |
| V1, V2 | `_MazeProbe --stage=4` and `--stage=2` (reach, detour, pinned); `_BalanceHarness` on each; `_UiShot` at both canvases |

**What no harness checks**: whether the bridge feels like a crossing or a corridor, whether the front
reads as pressure or as nagging, whether the Court is a place or a maze, whether the Muster is a
pleasure or a chore at seventeen screens, whether the Gaoler still holds up as chapter 11, and
whether the end lands. Eyes, on both devices.

---

## 10. Decisions

### 10.1 Decided by Eric, 2026-10-30

| # | question | decided |
|---|---|---|
| 1 | where the castle goes | after the Emberdeep, as recommended |
| 2 | the dungeon inside the castle | **the Cursed Dungeon moves in** (the draft recommended a new Undercroft; overruled) |
| 3 | the bridge as read | bottom left to top right, edges in the top-left and bottom-right corners |
| 4 | the collapsing span | yes, at an eighth of walking speed |
| 5 | the Court | one fixed building |
| 6 | the chickens | roast fowl on the Great Hall's platters, healing like a flask |
| 7 | the castle's casts | their own, on stand-ins first (now two new casts: the Bridge's and the Court's) |
| 8 | the Muster | 17 levels and two relic chests, moved by measurement |
| 9 | the procession | the road's wardens in pairs, then him |
| 10 | him | unnamed on screen; his light out, the motes going home, the freed wizards standing, credits |
| 11 | Merlin | yes, as its own build after the Throne: the one wizard he kept, revealed by the Oubliette (now D7, in the Dungeon), freed by the Throne, outside the gate as Laelaps is; verb *rewind*, on Light or Arcane |
| 12 | Boss Rush | unchanged (1.8 says what that takes) |
| 13 | variety first | the Ruins' walls and the Cave's caverns |

### 10.2 Raised by the move, with recommendations

1. **The grandfathered Dungeon stays enterable** on saves that had it (1.5), so nobody loses a chapter
   they could play. The alternative is to close it until they reach it again by the bridge, losing
   nothing they earned there (Tituba, Shadow Bolt, the deeds). **Recommended: keep it enterable**;
   the victory guard makes it harmless.
2. **The Throne also needs every wizard freed**, Tituba included (1.6), so the fiction's sentence is
   true where he is. **Recommended: yes**; it costs one gate value, once A1 writes its rule per gate.
3. **Boss Rush opens when every boss on its roster has fallen** (1.8), which on a new save is after
   the castle's dungeon. **Recommended: yes**; it keeps the rush's own rule (never meet a boss there
   first).
4. **A third phase for the Gaoler** (4.3), only if he measures long rather than hard at 12,500.
5. **Nothing at fifth place** (1.7); the Cave's caverns and the Ruins' walls carry the middle.
   **Recommended**, with V1 and V2 landing before the move.

---

## 11. What C0 built (2026-10-30, `wip/castle-plumbing`)

Section 1.9, entire, on top of A1 (`main-ff86ni`). Nothing in it is playable: all three chapters are
`IsPlayable = false`, so no save, screen or run can reach them, and every chapter of the road plays
exactly as it did. The Cursed Dungeon has not moved (C2).

### 11.1 Where it lives

| piece | where | what is there now |
|---|---|---|
| the chapters | `StageCatalog` | `stage_11` the Last Bridge, `stage_12` the Court, `stage_13` the Throne, appended; the texts of 1.4; `CampaignOrder` after `stage_7`, with a comment line between the bridge and the Court where C2 puts `stage_1` |
| the Throne's gate | `StageCatalog` | `Gate = StageGate.PreviousBossAndWizards` (A1's kind). It counts every wizard, Tituba included; `ValidateGateReachability` walks a new save to it |
| the bosses | `BossCatalog` `[11]`-`[13]` | `bridge_constable` the Constable (11,800, bonus 360), `court_seneschal` the Seneschal (13,000, 390), `throne_dark_wizard` "The Dark Wizard" (18,000, 450), each on `DeepWardenBoss.tscn` until `BridgeConstableBoss.tscn` / `CourtSeneschalBoss.tscn` / `ThroneDarkWizardBoss.tscn` exist |
| their deeds | `AchievementDefinitions` | `bridge_crossed` *Across the Water* (350), `court_emptied` *No One Left* (400), `throne_taken_back` *Taken Back* (1,000): Arcane Energy only, Road deeds, unlisted until their chapter is playable. A boss with no paying deed is a validator fault, so they had to exist with the bosses |
| Boss Rush | `BossRushCatalog.Arena` | `StageCatalog.GateChapter` (the Emberdeep). `Roster` and `IsOpen` are C2's and untouched |
| the casts | `StageBestiary.Bridge.cs`, `StageBestiary.Court.cs` | 3.5 and 5.7, every slot `Planned` at `scenes/Bridge*.tscn` / `scenes/Court*.tscn` on the Emberdeep's creature for the role. Tables: the bridge the Emberdeep's band for band (mean 1.28), the Court the Dungeon's shape a step up (1.37, "between the Bridge's and the Court's", 4.1). `bestiary_kit.PREFIXES` has `bridge-` and `court-` |
| the look | `StageEnvironmentCatalog` | `StageEnvironmentKind.Bridge`, `Court`, `Throne`, one stand-in profile each (no hazards); `StageEnvironmentProfile.WallSet` (below) |
| the ground | `LevelGenerator`, `GroundDetail` | a stand-in palette and detail block each; the bridge names `Band`, the Court and the Throne `Authored` |
| the topologies | `LevelGenerator.cs`, `LevelGenerator.Band.cs`, `LevelGenerator.Authored.cs` | the partial-file pattern (11.3); `Band` and `Authored` are stubs, so all three chapters generate as open field |
| the walls | `LevelTilePainter.WallArtSet` | a chapter's wall art by name; the Court names `court_wall`, the Throne `throne_wall`, both painting the jail set until drawn |
| the rules | `scripts/ChapterRules.cs`, `scripts/ChapterRulesCatalog.cs`, `Node2DGame` | the seven hooks (11.2); a null line per castle chapter |
| the music | `MusicCatalog` | `Bridge`, `Court`, `Throne`, `PlannedChapter(id, Emberdeep)`, rows `[11]`-`[13]`, exactly as `.ai/audio-manifest.md` asks; all three are rendered, so each chapter plays its own |
| side events | `SideEventDirector.ForStage` | `"stage_11" => null`, `"stage_12" => null`, `"stage_13" => null`, a comment apart |
| traps | `RegressionChecks` | the Court and the Throne are indoor chapters and all three built ones, so a chapter may set vents or spikes in its own profile without editing the validator |

### 11.2 The seven hooks

`ChapterRules` is an abstract `Node`. A chapter subclasses it in a file of its own; `Node2DGame` creates one
per run from `ChapterRulesCatalog.ForStage`, sets `Game` and `Stage`, and adds it as a child **before**
anything is placed (terrain, side event, secrets all ask hook 3). Its `_Ready` therefore runs with nothing
built; `RunStarted` is the one that runs once the stage exists. Every hook defaults to "no opinion", and a
chapter with no line in the catalog gets no rules object at all. Each call site in `Node2DGame` is marked
`Hook N`. `Node2DGame.Rules` hands the rules to whatever belongs to the chapter (its boss, its rooms).

| # | hook | asked in | for |
|---|---|---|---|
| 1 | `RunStarted()` | the end of `_Ready`, terrain's first chunks loaded, HUD built | placing the chapter's set pieces (the front, the gatehouses, the rooms, the Muster) |
| 2 | `TryPickSpawnPoint(playerPos, out point)` | `GetRandomSpawnPositionAroundPlayer`, i.e. every candidate of the trickle, elites, the miniboss, recycling, and a formation member that misses | the bridge's 65/15/20 split. The point is still vetted (walls, water, a long walk round, hook 3, separation) |
| 3 | `IsPlacementBlocked(point)` | `IsImpassablePosition` | ground closed to everything placed: enemies, chests, event sites, secrets, the boss, the way home. Asked very often: keep it cheap |
| 4 | `BossSpawnPoint(boss)` | `SpawnBoss` | the Constable at the gate, the Seneschal in his room |
| 5 | `VictoryPortalPoint(fellAt)` | `OpenVictoryPortal` | the castle gate, the throne room doors. The arcane portal still opens at the point and walking in still wins |
| 6 | `PinnedPaceSeconds` | `PaceSeconds` | the Throne's end state from the first second. The survival clock and the boss's arrival read `timeElapsed`, not this |
| 7 | `CreateObjective()` | once, after `RunStarted` | the line under the run timer: `Node2DGame` anchors the control across the HUD at `ObjectiveTop`-`ObjectiveBottom` (84-112, the side event's slot), insets it with `SafeArea.ApplyTo` and frees it with the run. `ChapterRules.ObjectiveLabel(text)` makes a text one in the side event's style |

A chapter that needs an eighth hook adds it to `ChapterRules` with a no-op default and one call in
`Node2DGame`, in one commit that says why - never a branch on the stage id in `Node2DGame`.

### 11.3 How a chapter agent plugs in

**Its topology** (C1 `Band`, C3 `Authored`, V1 `Ruin`, V2 `Caverns`; the enum values exist already). Fill
in the one partial method in `LevelGenerator.<Name>.cs` (C1 and C3 edit their stub; V1 and V2 create theirs):

```csharp
static partial void BuildRuinTopology(ref TopologyBuild build)
{
	build.Generate = (grid, chunk) => { /* fill grid; [0, 0] is world tile (chunk.X0, chunk.Y0) */ };
	build.Walled = true;              // walls: no cleanup pass, maze routing, maze spawn rules
	build.OwnsSpawnClearing = false;  // true if the start must not be stamped open
}
```

Then set `Topology = StageTopology.<Name>` in the chapter's own palette block (the castle's are set
already). That is all: until `Generate` is set the chapter is generated as Open; once it is,
`LevelGenerator.Describe` reports the topology and `LevelLayout.Walled`, `Node2DGame` plays walled ground
by the maze's rules, the validator's to-do item goes, and `ValidateTerrainSeams` holds the topology to the
seam rule by itself. The rules of a generator are in the comment above `TopologyBuild`: every cell a pure
function of (seed, world tile), the padded grid filled completely, floor as `BaseSentinel`, walls as
`WallTerrain`. `FillOpenGround(grid, chunk)` lays the palette's open ground first; `GenerateMaze(grid,
x0, y0, seed, rates)` is the Dungeon's lattice at a `MazeRates` of the topology's own (`MazeRates.Dungeon`
is the old constants, unchanged), which is the cheap way to V1's broken walls and V2's caverns
(section 7.2). Build-time code only assigns: anything loaded (a layout JSON) is loaded inside `Generate`,
because partial files do not order their static initialisers.

**Its walls.** Draw them under a terrain id of their own in a new module (`court_walls.py` ->
`court_wall`, run through `tiles.py`), every role the jail set has. The chapter's profile names the set
(`WallSet`); the painter uses it whole once the manifest has it, and `ValidateGroundArt` then holds it to
every role. The grid's wall cells stay `wall`. V1 and V2 add `WallSet = "ruin_wall"` / `"cave_wall"` to
their own profile blocks.

**Its rules.** Write `<Chapter>Rules : ChapterRules` in its own file and replace its one line in
`ChapterRulesCatalog.ForStage` (`"stage_11" => () => new BridgeRules(),`). The validator's to-do item for
the rules goes when the line is filled.

**Its look, ground and detail.** Its own block in `StageEnvironmentCatalog`, `LevelGenerator`'s palettes
and `GroundDetail`. `Node2DGame.BuildDecorProps` has one empty case for all three castle kinds on
purpose: their furniture is placed by their rules, so nobody edits that switch.

**Its cast** (C6). `.ai/bestiary.md` section 8's recipe at the paths the roster names; stems
`bridge-<name>` / `court-<name>`, sheets `Bridge<Name>Frames` / `Court<Name>Frames`. The roster needs no
edit; the bestiary to-do line shortens as scenes land.

**Its boss** (C5). Create the scene at `BossDefinition.ScenePath`; the chapter switches to it with no
edit. Tune numbers in its own `BossCatalog` block. `ValidateBossPattern` wants a `case` for a new
boss class.

**Its music** is wired. **Its flip** is one line, last, once its campaign to-do line is gone.

### 11.4 Decisions C0 made

1. **The world map (the rule for unbuilt chapters).** `.ai/world-map.md` section 2 says every chapter is a
   stop, an unbuilt one under a thicker fog, and section 6 that `ValidateWorldMap` demands a stop for every
   chapter. Drawing the castle is C8's and waits on C2's order, so C0 keeps the rule where it matters and
   relaxes it where it cannot yet be met: **a chapter that is not playable may stand off the map until it
   is drawn; its missing stop is an item on its campaign to-do line, and the screen leaves it out quietly
   (no `push_error`) and says "Chapter N of M" over the chapters it shows. A playable chapter with no stop
   is a fault, as before**, so none can flip with its place undrawn (shown: flipping the bridge early turns
   both its lines into faults). Once C8 places them, they are stops under the `Unbuilt` fog exactly as the
   map doc says, and the count is 13.
2. **No save is ever written an unbuilt chapter.** An unbuilt gate's previous playable chapter is earlier
   than it will be (the Throne's is the Emberdeep today), so `GlobalStatsManager.RecordMetGates` would have
   written `stage_13` into every save with the Warden of the Deep beaten and every wizard free, and handed it
   the Throne past an unbuilt bridge and court the day it flipped. It records playable gates only; a
   `ValidateCampaign` check holds it (shown to fire without the guard).
3. **Two `FinalChapter` readers meant the Emberdeep** (`ValidateCampaignMigration`'s "met the old gate"
   save and the schema-12 John Dee check). They read `GateChapter` now; with the Throne last and unplayable
   they would have stopped checking without a word. `ValidateCampaign`'s rule is A1's, unchanged.
4. **Stand-in numbers.** Health is section 3.6 / 5.6 / 6.4's. The victory bonuses (360, 390, 450) are
   mine, climbing from the Gaoler's castle 375 (4.3); each boss's block is its agent's to retune. The
   deed names are mine; the Throne's boss is "The Dark Wizard" in the catalog because the boss bar, the
   world map's card and the results screen need a name - a description, not his name (6.4).
5. **The Emberdeep's corruption line is unchanged** ("His seat, and the end of the road", 1.4 proposes
   "His forge, at the end of the road"): it reads wrong only once the bridge exists, so it belongs to the
   bridge's flip.
6. **`Ruin` and `Caverns` are declared now**, with `MazeRates`, so V1 and V2 create one file each.

### 11.5 Left open for the chapter pieces

- **The Throne has no roster** (6.3 says it has no trickle cast). Before it flips, C4 gives it one in
  `StageBestiary.Throne.cs` (the Court's Heavy, Wall and Ranged and the bridge's Gate Knight, which will
  read as "borrowed" on the bestiary line - C4 decides how that line ends) or its rules suppress the
  trickle. Today a run of `stage_13` from a tool spawns the Dungeon's cast with an error, and
  `ValidateBestiary` faults a playable chapter with no roster.
- **The procession needs more than the seven hooks.** Sending bosses on the Throne's own schedule, in
  pairs, with the fall of the last one winning, is the Boss Rush's machinery (`Node2DGame.BossRush.cs`),
  and granting the Muster's levels needs a public way in. Suppressing the clock's boss is already possible
  (`TimerVictorySeconds = 0` in `RunStarted`). C4 should propose the extra hooks rather than edit
  `Node2DGame` freely.
- **Boss Rush's roster is still derived** (C2's `RoadOrder` is not built). The day a castle boss's own
  scene lands before C2 merges, the rush would gain it after the Warden of the Deep; `ValidateBossRush`
  already says so ("the roster ends on ..., not the arena's boss"), so a C5 piece that lands first must
  wait for C2 or say so.
- **The way home is a point** (hook 5). The arcane portal opens there. If the bridge's gate or the Court's
  doors must replace the ring rather than stand behind it, that is an eighth hook.

### 11.6 Verified, and not

- `dotnet build`; the validator (`MainMenu.tscn --quit-after 300 -- --no-save-writes`): **0 errors, 6
  warnings** against the baseline's 0 and 1 - the standing `test_wizard` one, the three chapters' campaign
  to-do lines, and the bridge's and the Court's bestiary to-do lines (eleven stand-ins each). No migration,
  gate, world map, rush, music or ground line.
- Shown to fire by breaking what they guard: the `RecordMetGates` check without its guard; the bridge
  flipped early (a world map fault and "playable but still has stand-ins").
- `_BalanceHarness` (`--minutes=3 --timescale=3 --seed=4242 --character=1`), before and after:
  - `--boss-rush --immortal`: the same line both times, `BOSS RUSH in The Emberdeep: 10 bosses`, the
    road's ten in the road's order from `forest_treant` at pace 150 s to `emberdeep_warden` at 900 s, the
    first at 840 HP, the Emberdeep's cast.
  - `--stage=0`: the Forest and its cast both times; the bot died at 2.2 minutes before and at 3.0 and 1.6
    in two runs after. `--seed` seeds only the harness's chests (`Node2DGame` randomises its spawns), so
    that spread is the noise of one build, and nothing on the Forest's path changed (no rules, Open
    topology, the jail wall set).
  - smoke runs after: `--stage=1 --immortal` (the maze, now through `LevelLayout.Walled`) as before;
    `--stage=11 --immortal` plays an open field on the Emberdeep's stand-ins to the `bridge` music set.
- **Not run:** any castle chapter as the real thing (they are open fields on stand-ins); `_BossProbe
  --stage=11..13` (the stand-in is the Warden of the Deep); the world map with castle stops (C8). No
  chapter has rules yet, so no hook has run with an answer - the first real test of each is its chapter's.
