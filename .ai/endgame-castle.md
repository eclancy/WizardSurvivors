# The endgame castle: the bridge, the Undercroft, the Court and the throne

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

**Status: design only.** Nothing is built. It is written to be reviewed and then built by several
agents in parallel (section 9). Its sister doc, `.ai/achievements-and-secrets.md`, holds the
achievements and secrets from the same message; the castle's secrets are listed there and placed
here.

Read with `.ai/campaign.md` (how a chapter is added, and how the last two were built in two phases),
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
  (`.ai/world-map.md` section 1). The map's own doc already expects the endgame to grow the world
  east.

So the castle is where he is. The fiction has left exactly one place for it: across the water from
the end of the road.

### 1.2 Three places it could go

| | **A. After the Emberdeep** (recommended) | B. The Emberdeep's own end | C. A separate endgame mode |
|---|---|---|---|
| what it is | four new chapters after the Emberdeep in `CampaignOrder`: the Warden of the Deep opens the bridge | the Emberdeep becomes the bridge; its cast and the Warden move to the gate | a fifth thing beside Boss Rush, opened by the Warden of the Deep, outside the order |
| fiction | the Emberdeep is his forge at the end of the road; his seat is the castle across the water. One line changes (1.4) | rewrites a built chapter: its cast of eleven, its boss and its ground were made for a volcano | the castle is the story's end but not part of the story's road |
| campaign order | four ids appended and placed; nothing renumbered (`.ai/campaign.md` section 3) | the Emberdeep's content is rebuilt | none |
| saves | **no migration**: the bridge is earned by the Warden of the Deep's lifetime record (rule 3 of `IsStageEarned`); nothing a save holds changes | the Emberdeep's records and best times would describe a different place | none |
| gate | the gate stays on the Emberdeep; the castle is after it. `FinalChapter` (the last) and the gate chapter stop being the same chapter (1.5) | as A | none |
| Boss Rush | stays the Emberdeep's, roster unchanged (6.5) | loses its arena | unchanged |
| world map | four stops east of the Emberdeep, over the sea | the Emberdeep's place redrawn | stops outside the campaign's compass marks |
| cost | 1.5's plumbing, then four chapters | the same four chapters plus redoing a finished one | the same chapters, plus a second ordering system for "after the campaign" |

**Recommended: A.** It builds on top of what exists, costs no save change, and gives the road an
end that the player can see coming from the world map.

### 1.3 "Inside the castle is the dungeon level"

| | Move the Cursed Dungeon (`stage_1`) into the castle | **A new chapter: the Undercroft** (recommended) | Reskin the Dungeon's maze for the castle |
|---|---|---|---|
| campaign | loses its mid-campaign maze, which Eric moved to fifth on purpose five days ago ("cursed dungeon should be a later level") | the fifth chapter stays | the fifth chapter stays |
| saves | **breaks rule 2 of the gate**: every save that has opened the Dungeon would count as *past* chapters 6 to 9 and open them all. Undoing that is a migration that takes chapters away, which the save rules forbid | none | none |
| rewards | Tituba is freed there and the final gate counts her: she cannot stand past the gate. She would need a new home, and her reveal, her card and her chapter's side event would all move | its own | its own |
| play | the same maze twice in one campaign | rooms joined by narrow passageways: a different rhythm (4.2) | the same maze twice |
| cost | high, and it undoes a decision | one new topology on the maze's machinery, its own art | lowest, and least worth having |

**Recommended: the Undercroft**, a new chapter. It shares the Dungeon's connectivity engine
(`LevelGenerator`'s block maze, `MazeNavigation`, the spawn and recycle rules of `.ai/terrain.md`
section 6) and nothing a player can see.

### 1.4 The campaign, as recommended

| # | chapter | id | boss | what the boss asks | beating it opens | deed pays |
|---|---|---|---|---|---|---|
| 9 | Scorched Sands | `stage_6` | the Long Coil | burst a window | (the gate) | Fireball and Sunsteel Filament (`.ai/achievements-and-secrets.md` 2.2) |
| 10 | The Emberdeep | `stage_7` | the Warden of the Deep | all of it, in sequence | **the Last Bridge** | 300 Arcane Energy |
| 11 | **The Last Bridge** | `stage_11` | **the Constable** | **advance under fire** | the Undercroft | 350 Arcane Energy |
| 12 | **The Undercroft** | `stage_12` | **the Watchman** | **take cover** | the Court | 375 Arcane Energy |
| 13 | **The Court** | `stage_13` | **the Seneschal** | **pursue** | the Throne | 400 Arcane Energy |
| 14 | **The Throne** | `stage_14` | **the procession, then him** | **take it back** | - | the ending; 1,000 Arcane Energy; Merlin (decision 11) |

New corruption lines (`StageDefinition.CorruptionText`, under ten words each):

- The Emberdeep: *"His forge, at the end of the road."* (was *"His seat, and the end of the road."*)
- The Last Bridge: *"His seat is across the water."*
- The Undercroft: *"Below his house, he keeps what he took."*
- The Court: *"A court with no one left in it."*
- The Throne: *"He is here."*

The castle chapters pay **Arcane Energy, not spells or wizards**, under either answer to
`.ai/achievements-and-secrets.md` decision 1: under the total gate nothing past the gate may pay
what it counts, and under the narrowed gate the castle *may* pay spells, but none is designed here
(new spells belong to their own design pass). Merlin, if Eric wants him (decision 11), is outside
the gate as Laelaps is.

### 1.5 The plumbing this needs

Built once, by one agent, before anything else (section 9, C0), the way `.ai/campaign.md`'s Phase 1
was:

- **`StageCatalog`**: four entries appended (`stage_11` to `stage_14`, ids never renumbered),
  `IsPlayable = false` until each is built, and their place in `CampaignOrder` after `stage_7`. A
  chapter that is not playable is skipped, so the Warden of the Deep opens nothing until the bridge
  exists, and nobody is stopped by a chapter not yet built.
- **The gate chapter is not the last chapter.** `StageCatalog.FinalChapter` is "last in the order"
  today, and `ValidateCampaign` requires it to be the `CampaignComplete` one. A new
  `StageCatalog.GateChapter` (the one chapter gated on the campaign) is what the gate rules,
  `ValidateCampaignMigration` and Boss Rush read; `FinalChapter` stays "last". The validator's
  rule becomes: exactly one gate chapter, and every chapter after it is `PreviousBoss`. Three readers
  change (`BossRushCatalog.Arena`, two in `RegressionChecks`).
- **`BossCatalog`** `[11]` to `[14]` with stand-ins (the Warden of the Deep's scene) under the new
  bosses' ids and names, so each chapter's deed and opening are its own from the first day.
- **`StageBestiary.Bridge.cs`, `.Undercroft.cs`, `.Court.cs`**, `Planned(...)` at the scene paths of
  section 3-5's casts, the Emberdeep's creatures as stand-ins (built things, which is right for his
  house). The Throne has no trickle cast (6.3).
- **`StageEnvironmentKind`** gains `Bridge`, `Undercroft`, `Court`, `Throne`, each with a stand-in
  profile block and a `LevelGenerator` palette block, the pattern of `.ai/campaign.md` section 3's
  stand-in table.
- **`StageTopology`** gains `Band`, `Rooms` and `Authored`, each generated in its own partial file
  (`LevelGenerator.Band.cs` and so on), falling back to `Open` until built.
- **Wall art by chapter**: walls are the dungeon's jail set today. `LevelTilePainter.WallTileFor`
  learns to read the wall set of the stage's environment, with the jail set as the fallback, so a
  chapter's walls are its own generator's output, not an edit to `jail_walls.py`.
- **`ChapterRules`** (new, `scripts/ChapterRules.cs`): a per-chapter object that `Node2DGame` asks
  at seven hook points, all no-ops by default so every existing chapter plays exactly as it does:
  run start, choosing a spawn point, whether a point is closed to placement, where the boss appears,
  where the way home opens, a pinned pace clock, and the objective line under the run timer.
  `ChapterRulesCatalog.ForStage` holds a pre-placed `null` line per castle chapter, as
  `SideEventDirector.ForStage` did for the early chapters, so each chapter's agent replaces one line
  and writes one file of its own. This is what keeps four agents out of `Node2DGame.cs`.
- **`MusicCatalog`**: four planned sets with stand-ins (the Emberdeep's).
- **`SideEventDirector`**: `null` lines for the four (none is planned; the bridge's front and the
  Court's rooms carry those chapters).
- **Saves: no migration.** The new ids only add. A save that has beaten the Warden of the Deep
  (`LifetimeStats.BossesDefeated`) has the bridge the day it becomes playable.

---

## 2. What a run is in the castle

| chapter | clock | the goal | how it ends |
|---|---|---|---|
| The Last Bridge | the usual 15-minute run on the pace clock | **travel**: keep ahead of the collapsing span and reach the gate yard | the Constable at 15:00 in the yard; when he falls, the gate opens and walking through it is the way home |
| The Undercroft | the usual | **survive** room to room | the Watchman at 15:00; the stair up opens as the way home |
| The Court | the usual | **explore**: find the rooms | the Seneschal at 15:00, pursued through the halls; when he falls, the throne room doors at the Court's heart open, and he walks there |
| The Throne | no survival clock; the pace clock pinned at its end | **build, then fight** | the procession, then him; when he falls, the doors behind the throne open |

**The three map chapters are ordinary runs.** Their pace, XP curve and level gates are
`.ai/run-pacing.md`'s, untouched: what changes is the ground and the goal, never when the world
gets harder or how fast levels come. Each must be measured with the balance bot anyway (section 9),
because a band or a room lattice can change the spawn supply, and the kill rate is the supply.

**The castle's people.** It is his house. The Emberdeep's cast was made; the castle's is his
household, taken - his garrison in the steel family, his servants and courtiers in linen - with the
built things of a great house (gargoyles, furnaces, empty armour, candelabra) burning his fire, the
carve-out `.ai/world-and-tone.md` allows. Cold eyes on the taken, fire on the made. No willing
followers: a courtier here was taken like a shepherd on the heath.

---

## 3. The Last Bridge (`stage_11`)

*"His seat is across the water."* From the black cliffs past the Emberdeep a long stone bridge spans
the dark sea to his castle on its rock. It is the only way across, and it falls in behind you.

### 3.1 Reading the sketch

*"a bridge edge on the top left and bottom right, so the player needs to travel up and to the
right"*: the bridge runs from the **bottom left to the top right of the screen** (south-west to
north-east), and its two edges - the parapets and the drop beyond them - are what the player sees in
the **top-left and bottom-right corners**. Walking it is walking up and right.

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
the same: about 600 px of deck ahead, and both edges in the same corners. It is the one direction
the two canvases agree on.

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
ends the run in victory.

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
3. **A rhythm**: span, narrows, yard. The narrows funnel the horde into an area spell's best
   minute and put murder-holes over the player's head; the yards are wide enough to circle.
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

## 4. The Undercroft (`stage_12`)

*"Below his house, he keeps what he took."* The cellars, cisterns and vaults under the castle: the
way in, since the gate leads only to the yard. It is where the light he took is kept.

### 4.1 Its shape: rooms and passageways

A new `Rooms` topology on the maze's lattice, at twice its scale:

- **A room cell is 16 tiles** (two to a chunk side). Each holds one **room**, 10 to 12 tiles square
  (hashed), so the rock between two rooms is 4 to 6 tiles thick.
- **Passageways** are 4 tiles wide (192 px) and run through that rock wherever the block maze says
  two cells are joined. The block maze is the Cursed Dungeon's: per block of 4 x 4 cells, a
  backtracker, the braid and loop passes, and door masks across block borders (`DoorMask`), so the
  whole endless Undercroft is connected by the same proof the Dungeon is.
- **Great halls**: 15% of joined pairs merge into one long room with a row of pillars down the join.
- **Room kinds**, hashed per room: **cellar** (a grid of single-tile pillars three tiles apart: cover,
  4.4), **cistern** (a pool in the middle and a walk round it; the `pool` water style),
  **crypt** (sarcophagi as props), **store** (barrels and racks), **furnace** (flame vents),
  **vault** (a sealed door: 4.6).

**How it differs from the Cursed Dungeon**, which is a lattice of 6-tile corridors everywhere: here
the fighting happens in **rooms**, and the passages between them are **chokepoints** - a room fills,
the player falls back through a passage, the horde funnels after him into whatever he has. It is the
first place in the game where a narrow way through is worth holding.

**The risk** is the passages jamming: 4 tiles is two tiles under the Dungeon's corridors, which were
measured. `_MazeProbe --stage=12` (reach, detour, pinned share) decides whether 4 holds or becomes 5.

### 4.2 What a run is

The ordinary run: fifteen minutes, then the Watchman. The goal is the classic one on purpose - the
bridge before it is travel and the Court after it is exploration, and a held room is the rhythm
between them. When the Watchman falls, **the stair up** opens in the nearest room wall (the way
home, `ChapterRules.VictoryPortalPoint`).

### 4.3 The cast: below stairs

| role | creature | scene | what it is | attachment that reads | stand-in |
|---|---|---|---|---|---|
| Bulk | Scullion | `UndercroftScullion` | a kitchen drudge under a yoke | the yoke and its two buckets | Slag Thrall |
| Fast | Cellar Rat | `UndercroftRat` | a rat the size of a hound | the hunch and the long tail | Chainhound |
| Heavy | Cask-Porter | `UndercroftCaskPorter` | a porter with a cask on his shoulder | the cask, wider than he is | Iron Bulwark |
| Wall | The Stoker | `UndercroftStoker` | a furnace stoker in a leather apron | the long iron rake held across him | Kiln Brute |
| Ranged | Lamplighter | `UndercroftLamplighter` | a thin figure with a pole lamp, throwing cold sparks | the lamp at the pole's end, burning his cold `arcane` | Kindler |
| Turret | Furnace Mouth | `UndercroftFurnaceMouth` | a furnace door in a wall stack, spitting coals; built | the grated door and its flue | Flue Mask |
| Charger | Larder Hog | `UndercroftHog` | a kitchen pig, head down | humped shoulders, low | Ironjaw |
| Exploder | Spoiled Cask | `UndercroftSpoiledCask` | a cask swollen past its hoops | bulging staves | Burst Bellows |
| Slammer | Turnkey | `UndercroftTurnkey` | a jailer bringing a door-bar down | the bar raised highest | Trip-Hammer |
| Summoner | Ratcatcher | `UndercroftRatcatcher` | calls the rats out of the drains. Minion: **Cellar Rat** | a cage held up at hand height | Kennelmaster |
| MiniBoss | The Cellarer | `UndercroftCellarer` (48) | keeper of the stores | a tally-board and a ring of cellar keys | The Chainwright |

### 4.4 The boss: the Watchman

`UndercroftWatchmanBoss`, `undercroft_watchman`, health about 12,500. *"His lantern finds you
anywhere the stone does not hide you."*

- **The verb no other boss asks: take cover.** He carries a great lantern of his cold light. Its beam
  is a narrow `Cone` (half-angle about 14 degrees, reach about 900 px) that sweeps the room at about
  40 degrees a second; **anywhere the beam reaches and the stone does not block** takes 4 damage a
  tick and is slowed. The pillars of a cellar, the walls of a passage, a sarcophagus: anything solid
  between the lantern and the player is cover. He walks slowly after the player, room to room.
- `BuildPhaseThresholds()` `{0.5, 0.25}`: at half, two beams; at a quarter, the beam stops sweeping and
  turns toward the player at a limited rate, so the answer becomes moving from cover to cover.
- **One shared edit, the Watchman agent's only**: `GroundSlamAttack` gains an optional occlusion
  test, so `Contains()` - still the one authority on a shape - can say "inside the cone *and* not
  behind a wall", answered from `MazeNavigation.IsSolidAt` along a ray. The paint reads the same
  answer: the beam is drawn as the polygon of the same rays, clipped where they stop, so the light
  on the floor and the hit cannot disagree (the rule `.ai/enemy-behaviour.md` holds every shape to).
  No other boss sets it. `_BossProbe --stage=12`.

### 4.5 Terrain and art

`terrain_undercroft.py` (a worn flag floor, damp, drains), `undercroft_walls.py` (a brick-vault wall
set for every role the jail set has, read through the per-chapter wall art of 1.5; sconces burn his
cold `arcane` as the Dungeon's do), `props_undercroft.py` (barrels, racks, sacks, sarcophagi, the
sealed vault doors, the stair up), the cisterns in the existing `pool` water style.

### 4.6 Hazards and secrets

Hazards: flame vents in furnace rooms (an indoor chapter may have them, `StageEnvironmentProfile`),
cistern water (blocking), and spike rows in some passages (the Dungeon's hallway traps, which already
stand in the middle of a corridor).

Secrets:

- **The Stolen Lights** (strike, several a run, two pay): a vault room's sealed door, cracked, with
  warm light leaking under it. About 600 landed in front of it opens it: inside, a relic chest from the
  budget. The rest are empty, which is the point: he has spent what he took.
- **The Cistern Bell** (risk): ring it and the Cellarer comes now.
- **The Cellarer's Stores** (linger 2 s in a store room's open larder): heal 50%.
- **The Oubliette** (keepsake; untouched for 5 s on a grate in a dead-end room's floor): a voice
  below. If Merlin is approved (decision 11) this is where his card stops being a sealed blank and
  becomes a silhouette: someone is still held here. Otherwise it pays 150 Arcane Energy, once.

---

## 5. The Court (`stage_13`)

*"A court with no one left in it."* The halls of his house: galleries, a great hall laid for a feast,
a chapel, a library, rooms behind rooms. Eric's labyrinth.

### 5.1 Its shape: one building, the same every time

**The Court is finite and fixed**: an `Authored` topology, the first stage whose ground is read
from a file rather than a function. `tools/art/court_layout.py` lays it out once - hand-placed rooms
joined by generated halls, from a fixed seed - and writes `court_layout.json` (each cell's kind, each
room's rectangle and id, each door and each hidden passage). `LevelGenerator.Authored.cs` reads it;
cells outside the building are wall. The seam rule holds trivially: a lookup is a pure function.

**Why fixed, not seeded per run** (decision 5): a building does not move. A player who has found the
Great Hall can find it again, which turns the labyrinth from a maze into a *place* - and it makes
every room achievement free of luck, which the hidden ones need.

**Size**: 6 x 6 chunks (9,216 px square; about a minute corner to corner with nothing in the way).
The player enters at the **south-west** corner, up the Undercroft's stair; the **throne room doors
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
  | START: the Undercroft stair    |          |                        |
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
them ends the run in victory and opens the Throne.

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
Solar. No room pays a spell or a wizard (1.4).

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
  5.1. `_BossProbe --stage=13`.

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

`court_layout.py` (the layout and its JSON; it also checks itself, 9), `terrain_court.py` (floors per
wing: marble, oak boards, flags; the carpet runners as an overlay terrain), `court_walls.py` (panelled
walls for every wall role, with dressing: tapestries, lintel glyphs, door frames, portraits),
`props_court.py` (the banquet tables and platters, the fowl as a pickup sprite, pews, lecterns,
shelves, racks, the map table, kennels, the throne room doors).

### 5.9 Hazards

The Kitchens' ovens (flame vents); in the Great Hall, when the Seneschal fights there, **falling
chandeliers** (telegraphed rings). The halls themselves are clear: the Court is hard because of where
things are, not what is on the floor.

---

## 6. The Throne (`stage_14`)

*"He is here."* Not a map: one hall, closed behind him.

### 6.1 The room

A bounded hall, **40 x 40 tiles (1,920 px square)**, as an `Authored` layout of its own
(`throne_room.py` writes it; the same reader as the Court). Square, because the canvases are mirror
images of each other: a phone sees most of its height and half its width, a PC the reverse. Two rows
of four pillars (solid, layer 4) give it something to move round. The throne on a dais at the north.
The doors he came in by close behind him.

**The light** is the one in the game that is his: the `darkness` ramp as emission, which
`.ai/world-and-tone.md` has held back for this (*"Holding that back across eight chapters is what
makes it land"*). Everything else in the room is unlit stone and the player's light.

### 6.2 The Muster: a build in one breath

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

### 6.3 The procession

**The pace clock is pinned at 15:00** (`ChapterRules.PinnedPaceSeconds`): the world is at its end
state from the first second, which is what a level-18 build should meet. The minions are his
household guard - the Court's Heavy, Wall and Ranged, and the Constable's knights - on the end-of-run
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
rings in a closed room are unreadable beside another boss; the Warden is the recap already).

**The bosses were tuned in open fields.** In a closed 1,920 px room the Gaoler's lane, the Long
Coil's dives and the Archivist's orbit all change. `_BossProbe --stage=14` walks the procession, and
each pair is retuned there, in the throne's own data, never in the bosses' scenes.

### 6.4 The final boss

`ThroneDarkWizardBoss`, `throne_dark_wizard`, health about 18,000. Never named on screen (decision
10); the banner says *"He takes what you have. Take it back."*

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
the credits (decision 10).

### 6.5 How it differs from Boss Rush, and what happens to Boss Rush

| | Boss Rush | The Throne |
|---|---|---|
| where | the Emberdeep, streaming ground | one closed hall |
| the build | earned from level 1 through an ordinary run | made at the start, in the Muster |
| length | about 27 minutes (measured) | about 10 |
| bosses | all ten of the road's wardens, one at a time, in campaign order, scaled to when they arrive | six, in pairs, at half health, then him |
| the end | the Warden of the Deep | the dark wizard, the story's end |
| opens | after the Warden of the Deep | after the Court |
| records | most felled, fastest clear | the deed, and a best time |

**Boss Rush stays as it is**: the road's wardens, on the road's last chapter. Two lines change so the
castle does not leak into it: `BossRushCatalog.Arena` reads `StageCatalog.GateChapter` (still the
Emberdeep) instead of `FinalChapter` (now the Throne), and `Roster` takes the campaign up to and
including the gate chapter, so it stays ten and still ends on the Warden of the Deep.

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
- Measured before it ships: `_BalanceHarness --stage=14 --all-unlocked --seek-apotheosis --immortal`
  records when the form is taken, and the Muster's size (decision 8) moves until it lands in the
  third wave.
- The Throne's results never feed the balance tables of `.ai/run-pacing.md`; its bot runs are their
  own line.

---

## 7. More variety: the other chapters

### 7.1 What the eleven are today

| chapter | topology | ground (palette) | blockers | hazards | goal | side event |
|---|---|---|---|---|---|---|
| Enchanted Forest | open, endless | grass, moss, meadow | ponds, trunks, the pen's fence | - | survive, boss | hold the grove |
| The Fallow Fields | open | stubble, furrow, hedge bank | millponds, fences | - | survive, boss | defend the well |
| The Broken Circle | open | heather, peat, granite | tarns, standing stones | - | survive, boss | relight the moot |
| Sunken Cave | open | **the Ruins' palette** - it has none of its own (`LevelGenerator.PaletteFor` falls back) | - | - | survive, boss | - |
| Cursed Dungeon | **maze**, endless | flags | jail walls, water | pits, spikes and vents in the corridors | survive, boss | the key |
| Blighted Swamp | open | mud, moss | much water | - | survive, boss | the lanterns |
| Mystic Ruins | open | graystone, cobble | - | pits, spikes | survive, boss | - |
| Frozen Waste | open | graystone, ice | water | - | survive, boss | the thaw |
| Scorched Sands | open | sand, scree | - | rocky pits | survive, boss | the caravan |
| The Emberdeep | open | basalt | - | lava, pits | survive, boss | - |
| The Sketchbook | open | paper | - | - | survive, boss | - |

**Ten of eleven are the same shape**: an open endless field with blobs on it, played the same way -
survive fifteen minutes, kill the boss, walk into the portal. The side events give seven of them a
second goal; the ground gives one (the maze) a second shape. The castle adds three shapes (band,
rooms, a fixed building) and three goals (travel, explore, build-and-fight).

### 7.2 One engine, many looks

The Dungeon's block maze is a **connectivity engine**: a graph of cells, each block connected inside
and to its four neighbours, proven connected, endless, streamed, with a flow field and spawn rules
that understand it. What is drawn in its walls is a separate choice. The Undercroft draws rooms in
it; the proposals below draw broken walls, rock and water. Each inherits the proof and the
navigation for nothing, which is why they are cheap.

### 7.3 Proposals, ranked by value against cost

| rank | chapter | proposal | value | cost |
|---|---|---|---|---|
| 1 | **Mystic Ruins** | **Broken walls.** The maze lattice at the hall and loop rates `.ai/terrain.md` section 6 found too open for a dungeon (18% loops, 35% halls: *"the walls broke up into isolated slabs - an open floor with furniture"*), which is exactly a ruin, plus a pass that breaks long walls into stubs. Its flavour text already says *"broken spires and shattered walls"*. Cover, partial corridors, sightlines that open and close | high: the second walled chapter, and a different one | **S**: parameters and a ruin wall set (`ruin_walls.py`); the maze's navigation, spawn rules and hallway traps come with it, and the Ruins already has spikes |
| 2 | **Sunken Cave** | **Caverns.** The lattice drawn as rock: passages of varying width (4 to 10 tiles, hashed per passage), rooms as chambers, walls as rounded rock masses. Connectivity is kept by one rule: the middle 4 tiles of every passage the maze opens are never rock, and noise only decides the rest. Its own palette at last (wet stone, pools, crystal), and a cavern pocket for the Geode Heart | high: the one chapter on borrowed ground, whose fiction is *"dripping tunnels of black rock"* | **M**: a cave pass on the lattice and a cave wall set; the far half is art |
| 3 | **Blighted Swamp** | **Islands and causeways.** The lattice drawn as water: cells are islands, open passages are causeways and boardwalks, closed walls are open water. The swamp already has the most water; this gives it a shape the player has to read | medium: a third look on the same proof | **M**: the lattice with water for walls (water already draws from its shape) and a boardwalk detail set. Watch the side event's lanterns, which must stay on reachable ground |
| 4 | **The Emberdeep** | **Rivers of fire.** Lava in long channels rather than blobs - the isolines of a slow noise, a pure function of the world tile - with fords and bridges where the noise dips. It gives the last chapter of the road a direction | medium | **M**: a channel stamp; lava is a hazard, not a blocker, so no new blocking terrain |
| 5 | **Scorched Sands** | **Storm fronts.** A sandstorm crosses the map on the pace clock, lowering sight and pushing everything with it for 30 s | low to medium: a moment, not a shape | **M**, and a readability risk on a phone |
| 6 | Enchanted Forest | Paths through dense wood (the lattice drawn as thicket) | low: a maze in chapter one is what Eric moved the Dungeon away from | **M**; **not recommended** |

**Recommended first: 1 and 2.** They are the cheapest high-value changes in the game, they reuse
what the Undercroft builds, and between them every third chapter of the campaign gets a shape of its
own (Dungeon, Ruins, Cave; then the castle's three).

---

## 8. The world map

`.ai/world-map.md` reads a data-driven `PLACES` list in `tools/art/world_map.py` (branch
`wip/world-map`, not yet merged) and already expects this: *"They come after the Emberdeep, so the
world grows east: widen `W`, move the page's `PAGE_ORIGIN` with the edge, and the route, the compass
and the camera follow from the JSON."*

- **The drawing.** Past the Emberdeep's black cliffs, the dark sea; a long bridge from the cliffs to
  a rock standing in it; on the rock his castle - the sea-gate at its foot (the Undercroft), the walls
  and roofs of the Court, and the tower (the Throne). `W` grows from 1680 to about 2100; the page
  (`PAGE_ORIGIN`) moves east with the edge.
- **Four `PLACES` lines and their `WAYPOINTS`**, roughly: `stage_11` with its stand point at the
  bridge-foot on the cliffs (about 1700, 470), `stage_12` at the sea-gate (about 1890, 380), `stage_13`
  on the walls (about 1950, 300), `stage_14` at the tower's top (about 1990, 220). The road runs over
  the bridge between the first two. Final coordinates are the map agent's.
- **The dark light moves.** The world map puts the one `darkness` emission on the Emberdeep's summit
  (*"his seat"*). With the castle, that light belongs on the tower; the summit keeps its fire.
- **The stop order changes in one rule.** It is `[campaign] + [Boss Rush] + [outside]` today, and
  the route visits stops in that order. With chapters after the Emberdeep, Boss Rush - which stands
  on the Emberdeep's summit - must come *between* them: `[campaign up to the gate chapter] + [Boss
  Rush] + [the rest of the campaign] + [outside]`. Otherwise the road would run back from the tower to
  the summit. `ValidateWorldMap`'s rising arc lengths then still hold.
- **`StageRewards`** lists the castle chapters' deeds and secrets (derived from the catalogs, as
  every chapter's are). The Throne's card says *"Chapter 14 of 14"*.

---

## 9. The build plan

The pattern of `.ai/campaign.md` section 7: **one plumbing agent first, then one agent per piece in
its own worktree**, each creating files and editing only blocks already marked as its own.

### 9.1 The pieces

| piece | what | owns (creates, or edits only its own block) | depends on |
|---|---|---|---|
| **C0 Plumbing** | 1.5 entire: the four `StageCatalog` entries and order, `GateChapter`, the validator rule, `BossRushCatalog` (6.5), `BossCatalog` `[11]`-`[14]` on stand-ins, the three `StageBestiary.<Chapter>.cs` rosters, environment and palette blocks, the three topology partials as stubs, per-chapter wall art lookup, `ChapterRules` and its seven hooks in `Node2DGame`, `ChapterRulesCatalog`, music and side-event stubs | `StageCatalog.cs`, `BossCatalog.cs`, `BossRushCatalog.cs`, `StageBestiary.*.cs` (new), `StageEnvironmentCatalog.cs`, `LevelGenerator.cs` (+ partial stubs), `LevelTilePainter.cs` (wall lookup only), `Node2DGame.cs` (hook calls only), `ChapterRules.cs`, `ChapterRulesCatalog.cs`, `MusicCatalog.cs`, `SideEventDirector.cs`, `RegressionChecks.cs` (campaign rules) | Eric's decisions 1, 2 |
| **C1 The Last Bridge** | 3: the `Band` topology, `BridgeRules` (front, spawns, boss point, gate, objective line), `BridgeCollapseFront`, `BridgeBackdrop`, the bridge's art | `LevelGenerator.Band.cs`, `BridgeRules.cs`, `BridgeCollapseFront.cs`, `BridgeBackdrop.cs`; `terrain_bridge.py`, `props_bridge.py`, `detail_bridge.py`, `bridge_backdrop.py`; **the `abyss` block in `water.py`**; its environment block; its line in `ChapterRulesCatalog` | C0 |
| **C2 The Undercroft** | 4.1-4.2, 4.5-4.6: the `Rooms` topology, room kinds, its art and hazards | `LevelGenerator.Rooms.cs`, `UndercroftRules.cs`; `terrain_undercroft.py`, `undercroft_walls.py`, `props_undercroft.py`; its blocks | C0 |
| **C3 The Court** | 5.1-5.5, 5.8-5.9: the layout generator and its JSON, the `Authored` reader, `CourtRules`, `CourtRooms`, `CourtLayout` (rooms near a point, for 5.6), its art | `court_layout.py`, `LevelGenerator.Authored.cs`, `CourtRules.cs`, `CourtRooms.cs`, `CourtLayout.cs`; `terrain_court.py`, `court_walls.py`, `props_court.py`; its blocks | C0 |
| **C4 The Throne** | 6.1-6.3, 6.5: the hall's layout, `ThroneRules` (the Muster, the procession, the pinned clock), the procession's data, the level-up screen's Muster heading and short arming pause | `throne_room.py`, `ThroneRules.cs`, `ThroneProcession.cs`; **`LevelUpMenu.cs`** (the heading and the guard only) | C0, and C3's `Authored` reader |
| **C5 The bosses** | one agent each: the Constable (3.6), the Watchman (4.4, **with the `GroundSlamAttack` occlusion edit**), the Seneschal (5.6), the dark wizard (6.4, **with the `Player` seal and its `LoadoutHud` mark**) | `<Boss>.cs`, `<Boss>.tscn`, `tools/art/boss_<name>.py`, its frames; the shared edit named | C0; the Watchman after C2, the Seneschal after C3, the dark wizard after C4 |
| **C6 The casts** | three agents, one per chapter, by `.ai/bestiary.md` section 8's recipe: create the scenes at the paths the roster names | `tools/art/cast_<chapter>.py`, `scenes/<Chapter><Name>.tscn`, frames, PNGs | C0 |
| **C7 The music** | four pieces by `.ai/audio-direction.md`: the bridge a march under wind; the Undercroft low drones and a slow ostinato; the Court a court dance with no dancers; the Throne the heaviest piece in the game, whose pulse drops out in his second phase | `tools/audio/music_<chapter>.py`, `assets/music/<chapter>_*.wav` | C0 |
| **C8 The world map** | section 8 | `world_map.py`, the stop-order rule in `WorldMap`/`StageRewards` | `wip/world-map` merged, and C0 |
| **C9 The castle's achievements and secrets** | `.ai/achievements-and-secrets.md` 8.12, as one more chapter agent there (A5-A13 pattern) | `SecretCatalog.<Castle chapter>.cs`, its secret scripts | that doc's A4, and C1-C3 |
| **V1, V2** | 7.3's first two: the Ruins' broken walls, the Cave's caverns | `LevelGenerator.Ruin.cs` / `.Caverns.cs`, `ruin_walls.py` / `cave_walls.py`, each chapter's palette block | C0's topology partial pattern; independent of the castle |
| **Flip** | `IsPlayable = true`, one chapter at a time in order, **last** | `StageCatalog.cs`, one line | everything that chapter needs |

**Shared files, and who alone may touch them**: `Node2DGame.cs` (C0), `LevelGenerator.cs` (C0; every
topology after it is a partial file), `water.py` (C1), `LevelUpMenu.cs` (C4), `GroundSlamAttack.cs`
(the Watchman's agent), `Player.cs` and `LoadoutHud.cs` (the dark wizard's agent). The generated
`tiles.json`, `detail.json` and the atlas are rebuilt by the integrator after the merges, as
`.ai/campaign.md` section 7 says - take either side in a merge and re-run `tiles.py` and
`ground_detail.py`.

### 9.2 Order

1. **C0**, alone.
2. **C1, C2, C3, C6 x3, C7** in parallel (and V1, V2 whenever).
3. **C4** once C3's `Authored` reader has landed (or C0 lands the reader as a stub both use).
4. **C5** per boss as its chapter's ground lands.
5. **C8, C9.**
6. **Flip** the bridge, then the Undercroft, the Court, the Throne. A chapter's validator to-do line
   must be gone before its flag flips; if it says "playable but still has stand-ins", the flag went
   first.

### 9.3 What each piece must verify

Every piece: `dotnet build WizardSurvivors.sln` (and says so when only that ran); the validator run,
`"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit-after 300 -- --no-save-writes`, with no
new warnings and its chapter's to-do line shrinking.

| piece | also |
|---|---|
| C0 | the four chapters listed as to-do and nothing else new; `_BalanceHarness --stage=0` and `--boss-rush` unchanged (the rush still ten, still on the Emberdeep); the campaign validator's sample saves still pass |
| C1 | `_BalanceHarness --stage=11 --walk=45 --immortal --minutes=15 --terrain-log` (the bot reaches the yard, never stands on the drop, chunk steps within budget); its kill rate against the Emberdeep's on the same seed (if the band starves the supply, the spawn split moves, not the XP curve); `--check-feet`; `ValidateTerrainSeams` on the band; `_UiShot --stage=11` at both canvases showing **both edges in the top-left and bottom-right corners**, and once with the front on screen |
| C2 | `_MazeProbe --stage=12 --bot=roam` (reach, detour and pinned share against the Dungeon's table in `.ai/terrain.md` 6; passages widened if they jam); `_BalanceHarness --stage=12` |
| C3 | `court_layout.py` checks its own output: every room reachable from the start without a hidden way, every hidden room reachable with one, the throne doors reachable, every door glyph naming a real room; `_MazeProbe --stage=13`; `_UiShot` at a hall junction and in the Great Hall at both canvases |
| C4 | `_BalanceHarness --stage=14 --all-unlocked --seek-apotheosis --immortal` (the form lands in the third wave or later, 6.6); `_UiShot --force-levelup --stage=14` during the Muster at both canvases; the whole procession under `_BossProbe --stage=14` |
| C5 | `_BossProbe --stage=N` through every phase; `ValidateBossPatterns` (no zero wind-up, cones under 90); for the Watchman, a photograph of the beam clipped by a pillar and a probe that the player behind it takes nothing |
| C6 | the recipe's own checks (`ValidateBestiary` shrinking) |
| C8 | `ValidateWorldMap`, `_UiShot --scene=res://scenes/WorldMap.tscn --map-at=stage_14` |

**What no harness checks**: whether the bridge feels like a crossing or a corridor, whether the front
reads as pressure or as nagging, whether the Court is a place or a maze, whether the Muster is a
pleasure or a chore at seventeen screens, and whether the end lands. Eyes, on both devices.

---

## 10. Decisions for Eric

1. **Where the castle goes.** After the Emberdeep, as four chapters (recommended); the Emberdeep's
   own end; or a separate mode. **Recommended: after the Emberdeep** - no save change, and the road
   gets an end you can see from the map.
2. **The dungeon inside the castle.** A new chapter, the Undercroft, rooms and passageways
   (recommended), or the Cursed Dungeon moved there. Moving it would open chapters 6 to 9 for every
   save that had reached it and strand Tituba behind the gate. **Recommended: the Undercroft.**
3. **The bridge, as read**: from the bottom left of the screen to the top right, its edges in the
   top-left and bottom-right corners. Confirm, or say if you meant the bridge itself to run from top
   left to bottom right.
4. **The collapsing span** that keeps the player moving. The alternative is a free walk to the gate
   with the boss at 15:00 either way. **Recommended: the collapse**, at an eighth of walking speed.
5. **The Court is one fixed building**, the same every run (recommended), or laid out anew each run.
   **Recommended: fixed** - it makes the rooms places and the room achievements free of luck.
6. **The chickens.** Roast fowl on the Great Hall's platters, each healing like a flask - the
   genre's floor chicken, and a feast laid for a court that is not coming (recommended); or live
   chickens, which tone rule 1 ("serious, never cute") argues against. **Recommended: roast.**
7. **The castle's casts.** Three new casts of eleven, your rule from the bestiary ("each stage should
   have its unique set of creatures"), played on the Emberdeep's stand-ins until drawn (recommended);
   or one household cast shared across the three. **Recommended: three casts, stand-ins first.**
8. **The Muster's size**: 17 levels and two relic chests (to level 18), so the Ascension is in reach
   at once and the Apotheosis is fought for. **Recommended as written, moved by measurement.**
9. **The procession**: six of the road's wardens in pairs, then him (recommended); or the castle's own
   three officers again; or new bosses. **Recommended: the wardens in pairs.**
10. **Him.** Unnamed on screen - "he" everywhere, as the game has always said (recommended) - or a
    name; and the ending: his light out, the motes going home, the freed wizards standing, credits.
    **Recommended: unnamed, and that ending.**
11. **Merlin.** `.ai/campaign.md` left a person for the Broken Circle open, with Merlin "the obvious
    one". The castle is a better home: the one wizard he kept for himself, in his own house, revealed
    by the Undercroft's Oubliette and freed by the Throne, outside the gate as Laelaps is. His verb
    would be *rewind* (unclaimed; T. H. White's Merlyn lives backwards), on an element no wizard opens
    on (Light or Arcane). **Recommended: yes, as its own build after the Throne exists.**
12. **Boss Rush unchanged** - the road's ten wardens on the Emberdeep. **Recommended.**
13. **Variety first**: the Ruins' broken walls and the Cave's caverns (7.3). **Recommended: both,
    before or beside the castle; they are cheaper and reach players sooner.**
