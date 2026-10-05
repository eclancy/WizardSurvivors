# Side events: what the dark wizard keeps in each place

Five chapters each hold one optional encounter, partway through a run. Winning it carries out
something he took: a wizard in a cell, or a spell. These are the game's first **Discovery**
unlocks, the source `UnlockCatalog` declared from the start and never used.

Read `.ai/world-and-tone.md` first. Every chapter is somewhere he holds a prisoner, and the map
card's one line (`StageCatalog.CorruptionText`) is what each event is built from.

## The five

| chapter | event | the line it comes from | what it asks | reward |
|---|---|---|---|---|
| Enchanted Forest | **Hold the grove** (`GroveEvent`) | "Every path now leads inward." | **stand still** in a ring for 20 s; leaving drains it at half speed | Bramble Seed |
| Cursed Dungeon | **The Gaoler's key** (`GaolersKeyEvent`) | "He keeps a wizard in one of these cells." | **carry**: kill the Warden, take its key across the map; three hits knock it loose | Tituba |
| Blighted Swamp | **Snuff the lanterns** (`BogLanternsEvent`) | "Nothing living lights them." | **explore**: four lanterns spread over the map, each releasing wisps, then the tender comes | Contagion |
| Frozen Waste | **The thaw** (`ThawEvent`) | "A wizard lies under the ice." | **fight here**: damage dealt near the block thaws it, fire counts triple | Väinämöinen |
| Scorched Sands | **The caravan master** (`CaravanMasterEvent`) | "The dead still walk the trade road." | **catch**: a walker on a fixed road that never turns toward you and escapes at the end | Iron Palisade |

Each asks for something different, and none of them is "a harder fight". That was the brief: an
event that is only a bigger elite is not an encounter.

## How it is built

- **`SideEvent`** is the base: the objective line under the run timer, the edge-of-screen pointer
  (`SideEventPointer`), the success and failure beats, and standing down when the boss arrives,
  for the same reason chests and the Warden do. A subclass is only its own rules. Each one is a
  code-created `Node2D` at the run's origin that draws itself; there is no scene and no art.
- **`SideEventDirector`** is the whole mapping from chapter to event to Discovery site, and it
  starts the event once per run, at 90 s. A chapter not in its table has no event.
- **`DiscoveryRewards`** pays out a site: everything in `UnlockCatalog` with
  `Source = Discovery` and that site as `SourceId`. **Granted and saved the moment the event is
  won**, not at the end of the run, because a wizard you carried out does not go back in when you
  die three minutes later. The game over screen lists it via `Node2DGame.RecordDiscovery`.
- **Once a site is exhausted the event still runs and pays a chest.** It stays worth doing.
- `Node2DGame` exposes the short list an event may use: `SpawnEventEnemy` (the stage's own spawn
  table and health curve, so an event can never put something on the field the stage would not),
  `FindEventSite`, `ReachablePoint`, `ClampToStage`, `SpawnRewardChestAt`.

## Rules every event inherits

- **Nothing an event places joins `"enemies"` unless it is an enemy.** Every targeting path
  iterates that group, and an ice block or a lantern in it would pull the whole loadout off the
  swarm. This is the breakables rule, and **the thaw is built around it**: the ice is never a
  target. It listens to `Player.EnemyDamagedAt` and warms from hits landed near it, which is how
  "only area damage and fire break it" works without a breakable-object system.
- **Anything an enemy drops for the player to reach goes through `ReachablePoint`.** Enemies are
  not held to the bounds the player is. The first Gaoler's key fell where the Warden died, outside
  the player's clamp, and could not be picked up.
- **Event enemies are elites** (`IsMiniBoss`), so `RespawnEnemy` never recycles a key-carrier or
  the caravan master to the far side of the map.
- **`Enemy.ScriptedHeading`** overrides the chase. The caravan master walks its road with it.

## The rewards

### Two wizards, named people

`.ai/world-and-tone.md`: the starters are disciplines, and everyone rescued is a person.

- **Tituba**, taken from Barbados and jailed at Salem on an accusation. She opens on **Riptide**
  (the sea she was taken from), deliberately *not* a darkness or curse spell: she was a real woman
  wrongly accused, and the game does not agree with her accusers. Passive *Patience*: +15% duration.
- **Väinämöinen**, the singer of the Kalevala, who sowed the first forests. Opens on **Bramble
  Seed**. Passive *Song of Making*: +10% area.

Both wear **no player pigment**. The four saturated rows are the starters'. The rescued wear what
they were held in, so the only saturated thing on them is their staff head. Sprites are in
`tools/art/roster.py`; `head()` gained a `tone` argument so Tituba is not drawn in the light skin
tone the starters share.

`Player.GetLevelUpOptions` always offers a character its **own starting spell**, unlocked or not.
Both open on spells a save may not own yet.

### Three spells, one per element that had no pure spell

Metal, Poison and Grass had no pure spell, and only a pure spell can carry an element to its
capstone (see the element-tag rules in `.ai/gameplay-design.md`). Each new spell closes one, and each is a
shape the roster did not have:

- **Iron Palisade** (Metal): a line of stakes between you and the nearest pack, which shoves back
  and cuts whatever walks into it. The **wall** #64 listed as missing. It is not a physics body,
  which would need a new collision layer and would also stop the player. Like Hollow Star it rides
  `ApplyKnockback`, so boss resistance applies.
- **Contagion** (Poison): one carrier; when a marked enemy dies, the plague jumps to everything
  within reach. **It spreads on death, by itself.** It is bounded by a per-cast cap of 24 infected,
  and by marks lapsing.
- **Bramble Seed** (Grass): planted at your feet, it **grows** from 16 px to 110 px over six seconds.
  Every other zone is at full size the moment it appears. At most three stand at once.

## The final chapter's gate

The Emberdeep opens on "every spell recovered and every wizard freed", evaluated live, so the five
discoveries are now part of it. All five events sit in chapters before it. **Schema 10** credits
the discoveries to any save that had already met the gate, so nobody loses a chapter they had
opened. The schema-8 grant of "every wizard" now skips Discovery wizards, which did not exist when
everything was free.

## Verified, and not

Driven headless by a throwaway probe that walked to the pointer and killed what was near. All
five ran to completion and granted the right unlock, with the user's save backed up and restored
around every run. The thaw took 26 s and the grove 22 s. Each found spell dealt damage in a live run. A
frame of four events was captured and every one draws. The migration was checked on in-memory
saves.

**Not verified: whether any of it is fun, or the numbers.** Heat needed for the thaw (900), the
grove's 20 s, the master's health (26 times ordinary spawn health) and walking speed, and the
three-hit drop are first guesses. The probe hit for 400, so its times say the logic completes, not
how long a real run takes.
