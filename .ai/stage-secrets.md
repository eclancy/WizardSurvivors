# Stage secrets: what he missed

A plan, and since 2026-10-28 partly a record. It sets out what a secret is in this game, how one is
placed in a stage that has no edge, what it may pay, and three to five concrete secrets for every
chapter, then names the shared systems and the order to build them in. **Built so far (issue #20):
the Enchanted Forest's Fenced Bloom and its keepsake, the First Tree, with the core they need** -
section 12 says what was built, how it departs from this plan, and how to drive it. **Since
2026-10-30 (piece A4 of `.ai/achievements-and-secrets.md`): the secret core every chapter builds on -
the per-chapter catalog files, placement by kind, the seen mark, the vault class, the far tell, and
the strike, follow, gather and lure primitives - and the first spell vault, the Sunken Cave's Geode
Heart.** Section 13 says what each piece is and is the recipe for adding a secret.

Read first: `.ai/world-and-tone.md` (the premise and the tone rules), `.ai/side-events.md` (the
five chapter events; secrets must not repeat them), `.ai/terrain.md` section 7 (chunk streaming),
`.ai/run-pacing.md` (why XP and the level gates are fragile) and `.ai/passives-and-items.md`
(boons, chests, Full Set Enchantments). GitHub issues #35 (coordinate-based discoveries), #56
(static map elements), #20 (in-map permanent upgrade pickups) and #62 (the crystal fields) are the
older asks this answers; how it answers each is in section 9.

The decorative **landmark** this plan hangs its first secret on has landed: the flower pen
(`scripts/Landmark.cs`, `.ai/terrain.md` section 8). Since 2026-10 it is a walled garden (20 x 14
cells with a wide gate in every side since 2026-10-28; 12 x 9 with two before) and a SOLID fence that the flow field routes round, not the small walk-through
plot this plan first assumed. Section 7.1 says what changed and what it means for the secrets.

---

## 1. What a secret is

**A secret is something he missed.** The premise (`world-and-tone.md`) says magic did not leave
the world: it was taken and locked up, and every point of warm light on screen is a piece of it
that he does not have. A secret is a small one he overlooked: a flower still growing inside a
fence somebody kept, a lamp nobody has lit, a well he did not find. You do not take it from him.
He never knew it was there.

That gives a secret its difference from a side event, and every rule below follows from it:

| | side event | secret |
|---|---|---|
| who put it there | he did: a prisoner, a guard, his lanterns | nobody now, or somebody before him |
| how you learn of it | its far tell (a cold column of light), the chapter's wayfinder, then an announcement, an objective line and a pointer once you arrive | **you see it.** No pointer, no objective line, no announcement until it is found |
| when | always there, from the first second (since 2026-10-22; it was a 90 pace-second timer); it begins when you arrive | always there, from the first second, wherever the seed put it |
| where | four to six chunks from the start, seeded, fixed for the run (`ExplorePlacer`) | anywhere, fixed in the world for the run; you walk to it |
| how many | one per chapter, five chapters | three to five kinds per chapter, several instances per run |
| asks for | a sustained task: hold, carry, explore, fight, catch | a short act of attention: up to ten seconds of commitment |
| pays | a Discovery unlock (a wizard or a spell) | a run-local reward, and once ever a mark on the Secrets page; a few pay a small meta reward |

**A secret never pays a spell or a wizard.** The Emberdeep opens on "every spell recovered and
every wizard freed" (`GlobalStatsManager.IsCampaignComplete`). A secret is missable by design, and
the far ones depend on the seed and on how far a player walks; putting anything the final chapter
counts behind one would make the campaign's last gate depend on luck. Spells and wizards stay with
side events, achievements and the shop. Section 3 lists what secrets may pay instead, and
`ValidateSecretCatalog` (section 7.5) enforces it.

**A secret is found with the feet.** This is an auto-shooter with no interact button. Every
interaction below is triggered by where the player is, how they move, or where their damage
lands. Nothing asks for a button press, so nothing needs a controller binding or a touch target.

## 2. Signposting: how a secret is seen

### What the camera actually shows

The camera is 0.85 on both canvases. A phone in portrait sees about **847 x 1506 world pixels**
(720 / 0.85 by 1280 / 0.85); a 16:9 PC sees the same turned on its side, and a wider monitor sees
more width. So "on screen" means within about **420 px to the side on a phone**. A secret's near
tell has to read from 400 px away, on a phone, in a crowd of thirty.

### Two tells, a far one and a near one

**The far tell is a gradient, not a marker.** From about one to two chunks out (1500 to 3000 px)
the ground starts to change: wildflowers thicken toward the Fenced Bloom, dead stumps toward the
First Tree, crystals grow commoner toward the geode heart, scrub and reeds appear on cracked earth
toward the hidden water. This is the mechanic #62 proposed for the crystal fields ("the crystals
around here are cracking open"), generalised. It must be a gradient and never a ring with an
edge, because a hard edge is a line the player learns to stand on. It costs one new hook in
furnishing (7.2): decor sets take a density multiplier from the distance to the nearest secret
site.

**The near tell is a warm glint that breathes.** One small cluster of the `Gold (trim, lantern)`
row's `hi` (`#FFF0C0`) on the object, fading in and out on a slow period (about 2.4 s). Three
things make it unmistakable:

- **Warm, against a horde that is cold.** Every creature carries the same cold `arcane` eye-light
  and nothing else (`world-and-tone.md`, the enemy palette). A warm point that is not on the
  player is, in this fiction, magic he does not have. That is exactly what a secret is.
- **It breathes, while fire flickers.** In the Scorched Sands and the Emberdeep, where lava and
  flame vents are warm too, the rhythm separates them: a flicker is a hazard, a slow breath is a
  secret. Shape and rhythm carry it, not hue alone, so it survives colour-blindness.
- **It is the only animated pixel on a still object.** Decor never animates. The eye goes to the
  one thing that moves and is not an enemy.

Draw it at the contract's integer x2 scale, as a 2 x 2 art-pixel core with a one-pixel soft halo
at most. It must not grow into a bloom effect: a glint the size of a spell reads as a spell.

**The Sketchbook is the exception.** It is outside the fiction and has no warm light to spend.
Its tells are pencil marks: a circled spot, an X, a doodle left uncoloured. See section 6.9.

### What signposting must never be

- **No text.** Tone rule 2: no copy carries the mood. A secret has a name, shown when it is
  found and on the Secrets page; nothing on the map is written.
- **No pointer.** The side-event pointer is the side event's, and the point-of-interest arrows
  (`PoiPointer`) belong to the wayfinder. A secret that pointed would be an event.
- **No minimap pin.** There is no minimap, and a secret is the reason not to add one.
- **No chapter hint, unless this section is revisited first.** Since 2026-10-25 a side event that
  still holds a discovery is announced partway through a run by a sound and a caption from its
  direction (`.ai/side-events.md`, "Hints that something is out there"). `StageHintCatalog` could
  carry a secret's too, but a hint is a line of text and a direction - both of the things above - so
  giving secrets one is a decision to make here, not an entry to add there.

## 3. Rewards, and their balance limits

### The two dangers

`.ai/run-pacing.md` and `gameplay-design.md` ("The Apotheosis") are explicit that two things are
fragile:

- **The level clock.** XP comes from kills, and the kill rate in the middle and late run is
  pinned by the spawn burst window in real seconds. The Ascension (level 17) and the Apotheosis
  (level 23) land where they land because of that. Anything that hands out XP or levels moves
  them directly.
- **The Apotheosis floor.** Decided 2026-10-11: an ultimate must not be reachable early, even by a
  skilled player. The bot with every shop charge at its cap is the case to beat, and its floor is
  7.5 minutes. Anything that adds element tags, level-up charges (rerolls, bans, banks,
  auguries) or Growth beyond what the shop sells undercuts that floor, because the floor was
  measured with all of them maxed.

### Allowed run-local rewards

| reward | limit | why it is safe |
|---|---|---|
| **heal** (a share of max HP, or a health flask) | none | survival, not progression |
| **a sanctuary**: a spot that heals slowly, or shields from hazards, for the rest of the run | one effect per site, small (about +0.5 HP/s; `MaxHP` is 20) | it asks you to stay somewhere, which costs kills and orbs |
| **a timed buff** (move speed, pickup radius, damage) | 60 s at most, never cooldown, Amount or Growth | damage buys little XP: the kill rate is capped by supply, not by damage (`run-pacing.md` 3.3) |
| **a tactical effect at the site**: a root trap, a ward the horde cannot enter | re-arms on a long timer, or once | it is a place to fight, not a stat |
| **an ordinary chest** (`Node2DGame.SpawnRewardChestAt`) | **two per run from all secrets together** (`SecretChestBudget`) | about 20 chests drop a run, so two is a 10% bump; a chest can carry a Rare or Relic tag, which is why it is capped |
| **Arcane Energy** | **40 per run from all secrets together** | about five minutes of survival pay (8 per real minute) |
| **a reveal**: the next secret's far tell starts earlier | none | information, not power |

### Forbidden, in a run

- XP orbs, levels, or anything that adds to `growthMultiplier`.
- Element tags, except what a capped chest happens to roll.
- Rerolls, bans, banked levels or auguries.
- A permanent in-run bonus to damage, cooldown, area or Amount.
- A boon, a spell card, or a level-up offer.

### Once-ever meta rewards

Every secret, the first time it is found on a save, gets **a mark on the Secrets page** (7.6).
That is the reward for most of them, and it is enough: the page is the collection.

Each campaign chapter has exactly one **keepsake**, its rarest and farthest secret, and only
keepsakes pay anything beyond the page:

- **A found level (issue #20)** of one of four comfort stats: Vitality, Recovery, Magnet,
  Movespeed. One found level of each across the whole game, never two. Found levels are stored
  apart from the shop's (`SaveData.FoundUpgradeLevels`, section 5) and stack **above** the shop
  cap, as #20 asks. Not Growth, Amount, Luck, Damage, Crit or any charge: those are the stats the
  Apotheosis floor was measured with at cap.
- **A one-time Arcane Energy lump**, 150 at most.
- **The Gaoler's ledger**, which adds a hint for every keepsake to the Secrets page (6.2).

**Boons are deliberately not on that list**, though the campaign gate does not count them. A new
boon dilutes the boon draw (`passives-and-items.md` 3b: one level-up in four or five shows a
boon, weighted to the deepest element), and Water and Grass reach their tiers partly through
boons. Eight secret boons would make every thin-element build worse. If one boon is ever offered
for finding every keepsake, it goes through an achievement, and the reachability audit is re-run
with it in the pool.

**Before shipping the first found level**, re-run
`_BalanceHarness --seek-apotheosis --all-unlocked --immortal` with all four found levels granted
(`--all-unlocked` should grant them) and confirm the 7.5-minute floor holds.

### What walking away costs, and why that is the price

Enemies spawn round the player wherever he goes (`SpawnForwardBias`), so walking does not starve
the spawner. The cost is elsewhere: **XP orbs left behind** are swept into one far orb past
4200 px (`SweepFarXpOrbs`) and are lost unless he walks back, the **side event** stays where it
started, and a player walking a straight line picks up less than one circling a field. At 220 px/s
a chunk (1536 px) is about seven seconds of walking. The bands in section 4 are priced in those
seconds: a near secret costs half a minute's detour, a keepsake a minute and a half out and the
same back, about a fifth of a 15-minute run. That is the right price for something you find once,
and it is the reason a keepsake is the only thing that pays a meta reward.

## 4. Placement in an endless, streamed stage

### A pure function of the seed

Every procedural stage is endless and streamed in 32 x 32-tile chunks (`terrain.md` section 7).
A secret must obey the same rule the ground does: **where a secret is, is a pure function of
(stage seed, chunk), never of what happened in the run.** Then a chunk forgotten and repainted when
the player walks back is furnished with the same secret in the same place, and a placement can
never disagree with what is painted.

- Secrets are rolled with `LevelGenerator.Hash(terrainSeed, chunk.X, chunk.Y, SaltSecrets)`, the
  same idiom as decor, curated props and hazards (`Node2DGame.FurnishDecorSet` and siblings).
  `terrainSeed` is new every run (`DateTime.Now.Ticks` unless `ProceduralSeed` is set), so secrets
  move between runs and stay put within one. A probe sets `ProceduralSeed` for a fixed map.
- **One candidate per super-cell of 2 x 2 chunks**, at a hashed chunk and hashed position inside
  it, kept with the band's probability. That gives determinism and a minimum spacing at once;
  two secrets can never sit in neighbouring corners of adjacent chunks.
- **Every roll is drawn before the position is tested**, as `FurnishDecorSet` already does, so a
  rejected spot never shifts the rolls after it.
- **A site has a footprint**, not a point. A candidate is tested at its centre and round its
  footprint radius with `IsPropAvoidedAt` (no water, lava, walls) and a deterministic list of
  hashed fallbacks. If none fits, that super-cell has no secret. The two exceptions, the anchor
  and the keepsake, search outward in rings (as `ReachablePoint` does) until they fit.
- **Decor must avoid a site's footprint**, so the furnishing passes ask the placer first. This
  makes the secret placer a fifth thing that has to agree about the ground, beside the four
  `terrain.md` section 2 lists.
- **On the maze** (Cursed Dungeon) a site snaps to a cell centre, preferring hall rooms. The
  endless maze is connected (every block is connected inside and to all four neighbours), so
  any cell is reachable; nothing needs the navigation window to be consulted at placement.

### Distance bands

Distance is measured from world origin, where every run starts.

| band | chunks from origin (Chebyshev) | px, roughly | walk at 220 px/s | holds |
|---|---|---|---|---|
| clear | 0 to 1 | under 2300 | | nothing. The spawn clearing and the first minute are for building a loadout |
| near | 2 to 4 | 3000 to 6900 | 15 to 30 s | the **anchor**, and common secrets at about 0.25 per super-cell |
| mid | 5 to 9 | 7700 to 14,600 | 35 to 65 s | most secrets, at about 0.2 per super-cell |
| far | 10 and beyond | 15,000 and beyond | 70 s and more | the **keepsake**, and rare secrets at about 0.08 per super-cell |

All probabilities are first guesses, to be set by walking the harness (`--walk=DEG`) and counting
what a player passes.

### The two guaranteed sites

Scatter alone would leave some runs with nothing in reach. Two sites per run are placed once at
stage start, from the seed, and do not depend on scatter:

- **The anchor**: the chapter's signature near secret (for the Forest, the Fenced Bloom), at a
  hashed bearing and a hashed distance of three to four chunks (about 4600 to 6100 px, 21 to 28 s
  of walking). Its far tell starts a chunk before it. **This is the guarantee that every run has
  at least one secret within reach.**
- **The keepsake**: at a different hashed bearing, at least 90 degrees from the anchor's, ten to
  twelve chunks out. Its far tell starts three chunks before it, so the trail is wide enough to
  stumble onto.

Both are computed from the seed and the bearing alone, so the chunk that holds one knows it when
it is furnished, however the player got there.

### Run state survives the chunk

A chunk is freed when the player is more than three chunks away, and furnished again when he comes
back. **A secret's state is therefore never held on its node.** A run-level dictionary
(`SecretRunState`) keyed by the site's instance id (super-cell coordinate plus kind) holds
progress and completion. A refurnished site reads it and draws itself bloomed, lit or spent.
Partial progress is kept too: a lap half-walked round the Fenced Bloom is still half-walked when
the player comes back. It costs a few bytes a site.

### Standing down

When the boss arrives, every secret stands down, as chests and the Warden do: interactions in
progress freeze, nothing new starts, rewards in flight are paid. A secret also never sits within
600 px of the side event's sites or its wayfinder, which are placed first and are known at stage
start (`SideEvent.Sites`, `Node2DGame.ReserveExploreGround`); the secret placer should ask the
same reserve list, and add its own footprints to it so the furnishing avoids them the same way.

**The events got here first (2026-10-22).** Section 4's rules - a pure function of the seed, rolls
drawn before positions are tested, a footprint and a fixed fallback search, maze snapping, decor
kept out - were built for the side events (`ExplorePlacer`, `.ai/side-events.md` "Finding the
event") before any secret existed. A secret placer should extend that class rather than start a
second one; and the chapter's **wayfinder** (the Arcane Compass and its two cousins) is the
obvious place to decide whether secrets are ever pointed at. Today it marks the event's sites
only; a secret that pointed would be an event (section 2), so the default answer is no.

## 5. Persistence in the save

- **`SaveData.FoundSecretIds`** (`List<string>`): every secret kind found at least once. Drives
  the Secrets page and "first time" rewards.
- **`SaveData.FoundUpgradeLevels`** (`Dictionary<string, int>`): issue #20's found levels, kept
  apart from `ArcaneUpgradeLevels` so a shop refund or a future respec can never eat them.
  `Player.ApplyArcaneUpgrades` sums the two.
- **Schema 17** (this said 11; six other migrations landed first). `CurrentSchemaVersion` went to
  17 with a `MigrateToSecrets` step gated on `SchemaVersion < 17`, stepwise like the others. It
  only initialises the two fields.
  **It has nothing to credit, and that is on purpose**: schema 10 had to credit discoveries to
  saves that had already met the final gate because side events added things the gate counts.
  Secrets pay nothing the gate counts (section 1), so no existing save can lose a chapter. If that
  rule is ever broken, the migration is no longer trivial.
- `ToGodotDictionary` and `FromGodotDictionary` write the fields by hand (as every other field is)
  and guard reads with `ContainsKey`. *Reset Progress* clears both.
- **Granted the moment it is found and saved at once**, the `DiscoveryRewards` rule: a keepsake
  found three minutes before death is still found.
- `RunEvents.RecordSiteDiscovered` and `RunResult.DiscoveredSiteIds` already exist and **nothing
  calls them**. Secrets should record through a sibling (`RecordSecretFound`), and
  `LifetimeStats` should count secrets found, which is what an achievement would measure.

## 6. The secrets, chapter by chapter

Each chapter lists its side event's verb so the secrets can be checked against it: a secret may
share a primitive with its chapter's event, never its verb.

**Interaction vocabulary**, used below and built once (section 7.3):

| verb | the player | built from |
|---|---|---|
| **touch** | walks onto it | distance check |
| **circle** | walks one full lap round it, within a band | accumulated unwrapped angle |
| **linger** | stays within reach for 2 to 5 s (never longer; the grove owns long holds) | distance plus a timer that drains when away |
| **strike** | lands damage on enemies near it; it is never itself a target | `Player.EnemyDamagedAt`, the thaw's pattern |
| **cull** | kills enemies near it | `Player.EnemyKilled` |
| **carry** | takes a token somewhere; three hits knock it loose | `GaolersKeyEvent`'s carried-token logic, extracted |
| **sequence** | touches several marks in order; a wrong one resets, never locks | ordered touch |
| **follow** | chases something that moves ahead and waits | a small mover on a hashed path |
| **hour** | arrives in a window of the pace clock | `Node2DGame.PaceSeconds` |
| **guarded** | defeats what wakes when he arrives | `Node2DGame.SpawnEventEnemy`, elites |

**Sizes**: **S** reuses what exists or what an earlier secret built, about a day. **M** needs one
new primitive or a set of art. **L** needs a new system, usually terrain or breakables.

### 6.1 Enchanted Forest (`stage_0`)

*"Elderbark's wood. Every path now leads inward."* Side event: **hold the grove** (stand in a ring
for 20 s). So: no long holds here.

**F1. The Fenced Bloom** (the anchor). *Size S, once the landmark exists.*
- **Sees:** a wattle fence round a walled garden (960 x 672 px since 2026-10-28: beds, paths in from
  four gates), a single moonflower in the middle with the breathing glint. Wildflowers thicken for a
  chunk around it.
- **Does:** *circle*. Walk one full lap round the fence, anywhere from its posts to about 200 px
  out. The flower head turns to follow him as he goes, which is the whole tutorial. Backtracking
  unwinds the lap; the half-lap survives the chunk being forgotten. The fence is solid now, so the
  lap is round the OUTSIDE of it - which is what it always meant - and the lap is 1,800 px at the posts
  (more further out), about three times the small pen's: count the lap by angle round `Landmark.Centre`, not by
  distance walked.
- **Pays:** heal 50%. The flower stays open and the plot becomes a sanctuary for the run (+0.5 HP/s
  inside the fence). The open flower drops a **seed** for F4. Page mark.
- **Where:** the anchor, three to four chunks out; also rare in the mid band.
- **Art:** the landmark (other session); an open-bloom state; the wildflower gradient uses the new
  decor flowers.
- **Systems:** placer (anchor only), *circle*, run state, heal, sanctuary aura.

**F2. The Fairy Ring.** *Size S, no new art.*
- **Sees:** nine to twelve mushrooms in a perfect circle about 160 px across. Nothing else in the
  scatter makes a perfect circle, which is the tell; one mushroom carries the glint.
- **Does:** *touch* the centre. The ring wakes for 6 s: every enemy that crosses it is rooted for
  2 s (`ApplySlow(0)`, which already respects a boss's `MinSlowMultiplier`). It re-arms after 45 s.
- **Pays:** a trap you can lead the horde through, as often as you come back. Page mark on first
  use.
- **Where:** mid band scatter, about 0.2 per super-cell.
- **Art:** none. The `mushrooms` prop exists and is the one flora prop that already reads
  (`terrain.md` section 4).
- **Systems:** per-chunk placer, *touch*, a zone that roots on entry.

**F3. The Outward Path.** *Size M.*
- **Sees:** five pale waystones in a line running away from origin, each a little brighter than
  the last. The line is about a chunk long.
- **Does:** *sequence*, outward. Touching them in order, inner to outer, lights each one;
  touching one out of order dims the line and it starts again. Inward does nothing, which is the
  joke the corruption line sets up and the only one this chapter tells: every path leads inward,
  except this one.
- **Pays:** the fifth stone is beside a hollow stump: a chest (from the budget).
- **Where:** mid band. All five points must place, or the site is skipped.
- **Art:** waystone, unlit and lit; hollow stump.
- **Systems:** *sequence*, a multi-point site placer.

**F4. The First Tree** (keepsake). *Size M.*
- **Sees:** a barren clearing ringed by dead stumps, a cracked seed-bed in the middle with a dim
  glint. Stumps thicken for three chunks round it.
- **Does:** *carry*. Bring the seed the Fenced Bloom drops and set it in the bed. A hit knocks the
  seed loose, as the Gaoler's key does. A sapling grows over six seconds, the way Bramble Seed
  grows. Without a seed the clearing is only a clearing; the glint brightens while he carries one,
  which tells him he has the right thing.
- **Pays:** full heal; **once ever, one found level of Vitality**; page mark.
- **Where:** the keepsake site.
- **Art:** stump ring, seed-bed, seed token, sapling in three stages.
- **Systems:** *carry* (extracted from `GaolersKeyEvent`), keepsake placement, found levels.

### 6.2 Cursed Dungeon (`stage_1`)

*"He keeps a wizard in one of these cells."* Side event: **the Gaoler's key** (carry). The maze:
every site snaps to a cell, and **no secret ever moves a wall** (walls are terrain, collision and
the navigation window at once).

**D1. Chalk Marks.** *Size M.*
- **Sees:** a chalk arrow on the floor at a junction beside a hall room.
- **Does:** *follow*. Four to six arrows at successive junctions lead to a dead end. Somebody held
  here before marked the way to what they hid.
- **Pays:** a stash (`open-crate` prop): a chest from the budget.
- **Where:** mid band, within one maze block. The route comes from the block's own seeded maze, so
  it is as deterministic as the walls.
- **Art:** a chalk arrow decal, drawn once and turned in 90-degree steps; the stash.
- **Systems:** a route through one block's cells (`LevelGenerator`'s block maze), placer snapping
  to cells.

**D2. The Alarm Bell.** *Size S.*
- **Sees:** a bell on a chain-post in a hall room, its rope hanging, the glint on the bell.
- **Does:** *touch* the rope. A deliberate risk: the bell calls the Warden now (or, if a Warden is
  alive, an elite pair from the stage's own table). A Warden called this way also drops a chest
  from the budget.
- **Pays:** control over when the hardest fight of the next few minutes happens, and a chest for
  taking it early.
- **Where:** near and mid band, at most one per run.
- **Art:** a bell on the existing `chain-post`.
- **Systems:** *touch*, a hook on the Warden timer to bring the next Warden forward.

**D3. The Hollow Flagstone.** *Size S.*
- **Sees:** a cracked flagstone in a hall room with tally marks scratched round it and the glint
  in the crack.
- **Does:** *strike*. About 400 damage landed on enemies standing within 80 px of it breaks it.
  It is never a target; enemies drawn over it are.
- **Pays:** a cache: a health flask and 20 Arcane Energy.
- **Where:** mid band.
- **Art:** cracked and broken flagstone overlays, tally decal.
- **Systems:** *strike*, the thaw's listener.

**D4. The Gaoler's Ledger** (keepsake). *Size M.*
- **Sees:** a lectern with an open book in an otherwise empty hall room.
- **Does:** *guarded*. Arriving wakes two elite jailers from the stage's table; when both are dead,
  *touch* the lectern.
- **Pays:** a chest; **once ever, the ledger**: every chapter's keepsake gets a one-line hint on
  the Secrets page, and from then on every keepsake's far tell starts one chunk earlier. It is the
  game's map of what he missed, kept by the one who was keeping count.
- **Where:** the keepsake site, in a hall room.
- **Art:** lectern and book.
- **Systems:** *guarded*, the Secrets page hint lines.

### 6.3 Sunken Cave (`stage_2`)

*"His shadow followed the water down into the dark."* **No side event yet.** Issue #62 proposes a
wizard imprisoned in a crystal, which pays a Discovery wizard and so belongs in
`side-events.md`, not here. C2 below shares its primitives, so building one builds most of the
other.

**C1. Cold Lamps** (the anchor is one of them). *Size S to M.*
- **Sees:** an unlit lamp on a stone post. The flavour line says the only light is the one you
  brought.
- **Does:** *linger* 2 s beside it and it takes a flame from him. It stays lit for the run.
- **Pays:** each lamp heals 25% and casts a lit circle in which his pickup radius doubles. Lighting
  three in one run pays a chest from the budget. Page mark.
- **Where:** one is the anchor; the others in the near and mid bands.
- **Art:** lamp post, cold and lit. It is the Swamp's lantern turned round: there, something
  dead lit them and you snuff them; here, you light them.
- **Systems:** *linger*, a per-run count across instances.

**C2. The Crystal Vein.** *Size L.*
- **Sees:** small crystal clusters growing commoner toward a large geode heart. The closer he is,
  the more of the small ones show hairline cracks.
- **Does:** *strike*. Hits landed near a cracked cluster break it; the heart breaks at about 1200.
  Crystals never join `"enemies"` (the breakables rule).
- **Pays:** small clusters spill a little Arcane Energy; the heart pays a chest from the budget.
- **Where:** mid band, at about 0.1 per super-cell.
- **Art:** the `crystal-*` sprites and the `geode` prop exist; cracked and broken states do not.
- **Systems:** a breakable family on the thaw's listener, and the far-tell gradient applied to
  breakability, not just density.

**C3. The Choir Loft** (keepsake). *Size M.*
- **Sees:** three empty stone seats in a niche, where the Hollow Choir sang before he took them.
- **Does:** stay inside the loft's circle (about 140 px) for 5 s **without being struck**. He may
  move; any hit resets the count. Not the grove's verb: the grove wants presence under pressure,
  the loft wants a clean moment.
- **Pays:** full heal and 60 s of +15% damage; **once ever, one found level of Magnet**; page mark.
- **Where:** the keepsake site.
- **Art:** the three seats.
- **Systems:** *linger* with a struck-resets condition (`Player.DamageTaken`, which fires before
  mitigation and after the dodge roll, which is the right place: a dodged hit did not touch him).

### 6.4 Blighted Swamp (`stage_3`)

*"The lanterns still burn. Nothing living lights them."* Side event: **snuff the lanterns**
(explore). Decorative lily pads on still water are the decor session's and cost nothing here;
S1 is the one that can be stood on.

**S1. Lily Crossing.** *Size L.*
- **Sees:** a line of four to six lily pads across a narrow arm of a pond, to a small island with
  a drowned shrine on it. One pad carries a flower with the glint.
- **Does:** *touch*, after walking the pads. The pads are the only walkable cells on that water.
- **Pays:** at the shrine, a heal and a chest from the budget. **Twenty seconds after the shrine
  is taken, the pads sink**, so the island is never a one-lane fortress the horde can only reach
  in single file.
- **Where:** mid band. The site stamps its own pond and island into the raw terrain, as a lake
  blob is stamped; a stamp keyed by a site that is itself a pure function of the seed is pure too,
  as long as neighbouring chunks stamp it as well (the seam rule, `terrain.md` section 7).
- **Art:** a walkable lily-pad tile, the flower, the island shrine (`urn` and `goblet-spill` exist).
- **Systems:** walkable-over-water cells (a non-blocking `pad` terrain, and all four places in
  `terrain.md` section 2 agreeing about it), a terrain stamp, and **a runtime override that
  restores the water's collision when the pads sink**. That last is the first runtime change to
  terrain anywhere, which is why this is L.

**S2. The Living Lantern.** *Size M, with S4's chapel.*
- **Sees:** among the swamp's lanterns, one burns warm gold rather than cold. It is the only one
  something living lit.
- **Does:** *carry* it to the Sunken Chapel (S4) and hang it on the empty hook. Three hits knock it
  loose. It is never one of the side event's lanterns and the event never counts it.
- **Pays:** see S4.
- **Where:** mid band, at most one per run.
- **Art:** the lantern, carried and hung.
- **Systems:** *carry*.

**S3. The Warm Wisp.** *Size M.*
- **Sees:** a small warm light in the reeds. The side event's wisps are cold and are enemies; this
  one is gold and is not.
- **Does:** *follow*. It hops 300 px ahead whenever he comes within 150 px, six times along a
  hashed path, then settles in a reed bed. If he wanders more than 1200 px off, it goes back to
  where it started.
- **Pays:** a health flask and 20 Arcane Energy where it settles.
- **Where:** near and mid band.
- **Art:** the wisp (an effect, drawn in code); the reed bed is decor.
- **Systems:** the *follow* mover. It never joins `"enemies"` and has no collision.

**S4. The Sunken Chapel** (keepsake). *Size M.*
- **Sees:** a roofless chapel half under the bog, an empty lamp hook by the door.
- **Does:** receives the Living Lantern (S2).
- **Pays:** a chest; **once ever, 150 Arcane Energy**; page mark. The chapel stays lit for the run
  and heals slowly within its walls.
- **Where:** the keepsake site.
- **Art:** chapel ruin, lamp hook empty and lit.
- **Systems:** keepsake placement; the carry target.

### 6.5 Mystic Ruins (`stage_4`)

*"The old wards were broken from the inside."* **No side event.**

**R1. The Broken Ward.** *Size M.*
- **Sees:** five ward stones in a ring, each cut with one to five notches.
- **Does:** *sequence*, in notch order. A wrong stone dims them all; start again.
- **Pays:** the ward re-forms for 30 s: enemies that try to enter are pushed back out at its edge
  (`ApplyKnockback`, so boss resistance applies). Then it breaks again, from the inside, and
  leaves a chest from the budget in the centre.
- **Where:** mid band.
- **Art:** ward stone in five notch counts, lit and unlit. The ring itself draws in code.
- **Systems:** *sequence*, a repelling zone.

**R2. The Misfiled Page.** *Size S, once the mover exists.*
- **Sees:** a loose page caught on rubble.
- **Does:** *follow*. It lifts and blows away when he comes within 200 px, five times, then lodges
  against a wall stub, and he *touches* it.
- **Pays:** 25 Arcane Energy and a page mark. It is a page the Archivist lost.
- **Where:** near band.
- **Art:** the page, still and blowing.
- **Systems:** the *follow* mover from S3.

**R3. The Sundial Vault** (keepsake). *Size M.*
- **Sees:** a sundial beside a door frame standing alone in the rubble, sealed. The gnomon's shadow
  turns with the pace clock. One notch on the dial is cut deeper than the rest.
- **Does:** *hour*. When the shadow reaches the deep notch (a one-minute window), stand on the
  dial: the door opens, and he walks through the frame.
- **Pays:** chest and full heal; **once ever, one found level of Movespeed**; page mark.
- **Anti-frustration:** the window recurs every quarter turn (about every 3.75 pace minutes), so
  arriving late costs a wait, never the secret.
- **Where:** the keepsake site.
- **Art:** sundial with shadow, door frame sealed and open.
- **Systems:** *hour*, keepsake placement.

**R4. The Statue's Eye.** *Size S. Optional.*
- **Sees:** a toppled statue head with one gem eye catching the light.
- **Does:** *strike*, about 300 near it.
- **Pays:** the gem chips out: a health flask.

### 6.6 Frozen Waste (`stage_5`)

*"A wizard lies under the ice, and the cold stands guard."* Side event: **the thaw** (damage
dealt near the ice). So: no *strike* here.

**I1. Footprints** (the anchor). *Size M.*
- **Sees:** a line of footprints in the snow, starting from nowhere.
- **Does:** *follow* them, about a chunk, to a dead camp: a cold fire ring and a bedroll. *Linger*
  2 s at the ring and it rekindles from his light.
- **Pays:** heal 50%, and the hearth heals slowly within 160 px for the rest of the run. Page mark.
- **Tone:** somebody came here alone before him and did not leave. That is absence, which is on
  direction; there is never a body.
- **Art:** footprint decal (left and right), fire ring cold and lit, bedroll.
- **Systems:** a decal trail placed along a hashed path, *linger*.

**I2. Settled Snow.** *Size S to M.*
- **Sees:** a drift field: a chunk where the snow is always blowing.
- **Does:** stand still for 2 s and the snow settles round him; anything buried within 400 px
  glints. Then *touch* it to dig it up. In a genre that is all movement, this one asks him to stop
  and look.
- **Pays:** two to four caches per field: a health flask or 15 Arcane Energy each.
- **Where:** mid band, about one chunk in eight is a drift field.
- **Art:** blowing-snow effect (code), the buried glint.
- **Systems:** stillness check, per-chunk hidden caches.

**I3. The Watch-Fire** (keepsake). *Size M.*
- **Sees:** a cold watch-fire on a ridge (the `brazier` prop).
- **Does:** *guarded*: arriving wakes a pair of Rime Guards (`scenes/RimeGuard.tscn`, the cone
  slammer the Still Warden calls), as elites. When both are dead, *linger* 2 s to light it. The
  Rime Guard is today only the boss's guard, not in the stage's ordinary spawn table; if
  `SpawnEventEnemy`'s "only what the stage would spawn" rule is to hold, use the table's own elite
  instead.
- **Pays:** chest and heal; **once ever, 150 Arcane Energy**; page mark.
- **Where:** the keepsake site.
- **Systems:** *guarded*, *linger*.

### 6.7 Scorched Sands (`stage_6`)

*"He hid the water. The dead still walk the trade road."* Side event: **the caravan master**
(catch a walker on a road).

**A1. The Buried Cart.** *Size S, no new art.*
- **Sees:** a cart wheel sticking out of a dune (`wheelbarrow` or `ore-cart`, half sunk), the glint
  on the hub.
- **Does:** *strike*. About 300 landed on enemies within 100 px blows the sand off.
- **Pays:** a chest from the budget.
- **Where:** mid band.
- **Art:** the existing props, drawn half buried (a sand mound overlay, or a crop).
- **Systems:** *strike*.

**A2. Milestones.** *Size S to M.*
- **Sees:** carved milestones every 800 px or so along an old road, each with a weathered number.
- **Does:** *sequence*, in order along the road. A skipped stone breaks the run of them, nothing
  more.
- **Pays:** 5 Arcane Energy a stone; every fifth in a row, a health flask. A trade road pays in
  coin.
- **Where:** a road line hashed per run, crossing the near and mid bands. It is not the caravan
  master's road, and the two never share a chunk.
- **Art:** milestone.
- **Systems:** *sequence*, a long linear site.

**A3. The Hidden Water** (keepsake). *Size M.*
- **Sees:** the far tell is the whole point. Cracked earth gives way to scrub, then reeds, then a
  slab on a low ring of stone with a glint in its seam.
- **Does:** *linger* 3 s on the slab and it shifts. Under it is a well.
- **Pays:** full heal, and the well heals slowly within 140 px for the rest of the run; **once ever,
  one found level of Recovery**; page mark. The water he hid.
- **Where:** the keepsake site.
- **Art:** well capped and open; scrub and reeds (decor session) for the gradient.
- **Systems:** *linger*, the decor gradient.

**A4. The Mirage.** *Size S. Optional.*
- **Sees:** a shimmering pool that always stays about 600 px ahead.
- **Does:** stand still for 3 s and it drifts to him.
- **Pays:** 60 s of +20% move speed.

### 6.8 The Emberdeep (`stage_7`)

*"His seat, and the end of the road."* **No side event.** It opens only once the campaign is
complete, so nothing found here can affect the gate. Two rules beyond the rest: **he is never on
screen** (tone rule 5), so no secret shows him, his likeness or his hand; and secrets here are
fewer, quieter and about who came before.

**E1. The Last Ward** (the anchor). *Size S.*
- **Sees:** a ring of cooled obsidian where the lava stopped flowing. Somebody made a stand here.
- **Does:** *touch*: walk in.
- **Pays:** inside the ring, hazards do not hurt and Armor is +10%, for the run. Page mark.
- **Art:** obsidian ring overlay.
- **Systems:** a zone exemption in the hazard tick (`IsHazardAtWorld`).

**E2. The Sleeping Sentry.** *Size S.*
- **Sees:** a skull-sentry on a plinth, its fire eyes dark. Made things burn; this one is out.
- **Does:** *touch* the plinth and it wakes: an elite sentry from the stage's table. *Guarded*.
- **Pays:** a chest from the budget. A risk, chosen.
- **Systems:** *guarded*.

**E3. The Marks** (keepsake). *Size M.*
- **Sees:** a basalt wall scored with marks.
- **Does:** *touch*. One mark lights for each wizard he has freed.
- **Pays:** heal; page mark. **No stat**: the Emberdeep is past the gate and the four found
  levels belong to the chapters that lead to it. This is where the Secrets page says how much he
  found.
- **Art:** the wall, marks unlit and lit. No writing: marks, not names.

### 6.9 The Sketchbook (`stage_8`)

Outside the campaign and outside the fiction; it has no corruption line because he has never been
here. Three rules, from `CLAUDE.md` and `art-replacement-manifest.md`, that bind every secret in it:

- **The kids' art is never altered, repalettised, extended or given derived frames.** A secret here
  may use their sprites exactly as they are, and nothing else of theirs.
- **Anything new is either generated by us in `kids_art.py`'s doodle section, and says so, or drawn
  by the kids.** Nothing goes on screen under their name that they did not draw.
- **No meta reward but the page mark.** The chapter gates nothing, and its secrets should not
  either.

Tells here are pencil marks, not warm glints.

**K1. The Dog's Find.** *Size S to M.*
- **Sees:** now and then the dog runs off and sits somewhere, using its existing animations.
- **Does:** *follow* it. Where it sits there is a pencilled X (ours).
- **Pays:** a chest from the budget.
- **Systems:** a "go and wait there" state on the dog's behaviour. No new frames: if the dog has no
  sitting pose, it stands.

**K2. Colour It In.** *Size S.*
- **Sees:** one doodle on the page drawn but not coloured.
- **Does:** *circle* it, the Fenced Bloom's lap. It colours in.
- **Pays:** heal 50%.
- **Art:** a coloured variant of one of our generated doodles.

**K3. The Kids' Page.** *Not scheduled.*
- A reserved slot: a secret whose object the kids draw, if and when they want to. The plan supplies
  the placement and an interaction; they supply what is found.

### 6.10 Every chapter at a glance

| chapter | side event verb | secrets (verb) | keepsake pays |
|---|---|---|---|
| Enchanted Forest | stand still | Fenced Bloom (circle), Fairy Ring (touch), Outward Path (sequence), **First Tree** (carry) | Vitality |
| Cursed Dungeon | carry | Chalk Marks (follow), Alarm Bell (touch, risk), Hollow Flagstone (strike), **Gaoler's Ledger** (guarded) | the ledger |
| Sunken Cave | none (#62 proposed) | Cold Lamps (linger), Crystal Vein (strike), **Choir Loft** (untouched) | Magnet |
| Blighted Swamp | explore | Lily Crossing (touch), Living Lantern (carry), Warm Wisp (follow), **Sunken Chapel** | 150 AE |
| Mystic Ruins | none | Broken Ward (sequence), Misfiled Page (follow), **Sundial Vault** (hour), Statue's Eye (strike) | Movespeed |
| Frozen Waste | fight near | Footprints (follow, linger), Settled Snow (stillness), **Watch-Fire** (guarded) | 150 AE |
| Scorched Sands | catch | Buried Cart (strike), Milestones (sequence), **Hidden Water** (linger), Mirage (stillness) | Recovery |
| The Emberdeep | none | Last Ward (touch), Sleeping Sentry (guarded), **The Marks** (touch) | page only |
| The Sketchbook | none | Dog's Find (follow), Colour It In (circle), Kids' Page (reserved) | page only |

The Dungeon's carry is its side event's, so its carry-shaped secret was dropped for chalk marks.
The Frozen Waste has no *strike*, which is the thaw's. The Forest's long hold belongs to the grove,
so the Bloom asks for a lap.

## 7. Shared systems

### 7.1 The landmark hook

What this plan needs from the landmark the decor session is building, so that a secret is
behaviour on top of art and never a subclass of it:

- **A stable identity and a footprint**: a kind, a world position, a radius the placer and decor
  can both avoid.
- **States it draws itself**: `Dormant` (with the breathing glint), `Stirring` (progress, 0 to 1:
  the flower turning, the lamp warming), `Awake`, `Spent`. The secret sets them; the landmark
  draws them. Nothing about the interaction lives in the landmark.
- **Not in `"enemies"`, ever**, and not targetable (the breakables rule).
- **No collision unless it is compact - OR IN THE NAVIGATION GRID (revised 2026-10).** This said a
  blocking fence is a wall, and that outside the maze enemies had no flow field and would wedge
  on it. Both halves changed. Every open stage now has the shared flow field (`terrain.md`
  section 9). The garden's fence cells are stamped into it (`TerrainStreamer.FlagFence`), so the
  horde routes round to a gate instead of wedging. So the pen's fence IS solid now (the user asked
  for it: "I should not be able to walk directly through the fence"), with two opposite gates so
  it is never a dead end to be pinned in. A landmark that blocks must do the same three things:
  be on layer 4 (the boots and enemies meet it), mark its cells in the grid, and leave a way
  through at least 96 px wide. A landmark that does not want that should still not block.
  `Landmark.Gates` gives the gaps' positions to anything that wants to watch them.
- **Placed by the secret placer, not by decor scatter**, so it is where the seed says, and
  re-furnished identically.

### 7.2 Placement: `SecretPlacer`

A pure function from (seed, chunk) to the sites in it, with the anchor and keepsake computed once
at stage start (section 4). Called by furnishing **before** decor, so decor avoids footprints, and
by decor for the gradient multiplier ("how near is the nearest site of kind K"). Super-cell
scatter, bands, footprint tests through `IsPropAvoidedAt`, maze snapping to cells. Multi-point
sites (the Outward Path, Chalk Marks, Milestones) place all their points or none.

### 7.3 Interaction primitives

Code-only `Node2D` components, distance-based like the side events, no `Area2D` and no scene: the
verbs in section 6. Each reports a progress value the landmark draws, so the tell and the rule can
never disagree (the `GroundSlamAttack.Contains` principle). `carry` is extracted from
`GaolersKeyEvent` rather than written twice; `strike` reuses the thaw's listener on
`Player.EnemyDamagedAt`; `cull` uses `Player.EnemyKilled`; `guarded` uses `SpawnEventEnemy` with
elites, so `RespawnEnemy` never recycles a guard.

### 7.4 Run state: `SecretRunState`

A dictionary on `Node2DGame`, instance id to progress and completion, read by every refurnished
site. Also holds the run's chest and Arcane budgets, and freezes on boss arrival.

### 7.5 `SecretCatalog`, the single authority

Like `UnlockCatalog` and `ApotheosisCatalog`: one list, every secret, and nothing defined anywhere
else. Each entry: id, stage id, display name, band, rarity, verb, run reward, meta reward (kind and
id), keepsake flag, the ledger's hint line, the landmark kind. `RegressionChecks.ValidateSecretCatalog`:

- every stage id exists in `StageCatalog`; every playable campaign chapter has at least three
  secrets and exactly one keepsake;
- **no meta reward is a spell, a wizard or a boon** (section 1, section 3);
- found levels name only Vitality, Recovery, Magnet or Movespeed, and each at most once;
- no run reward is XP, a level, a tag, a charge or Growth;
- Sketchbook secrets carry no meta reward;
- ids are unique, and every keepsake has a hint line.

`SecretRewards` pays them, as `DiscoveryRewards` pays sites: granted and saved at once, recorded
for the game over screen beside `RecordDiscovery`.

### 7.6 The Secrets page

A page beside the achievements in the main menu: one row per chapter, "2 of 4 found", the names
and a short line for each found secret, nothing at all for one not yet found (not even its name;
a list of named blanks is a checklist, which is a different game). Once the ledger is found,
each chapter's keepsake row shows its hint. It is a new screen, so it calls
`MenuNavigator.Attach(root)` and `SafeArea.Apply(root)`; check it portrait at 540x1212 with
`--portrait --safe-area` and landscape at 1280x720 through `_UiShot`.

In a run: a short toast with the secret's name when it is found (the side-event success beat,
quieter), and the game over screen lists what was found. No HUD counter.

### 7.7 Audio

Two sounds: a soft shimmer when the player comes within reach of a dormant site, and a found
sting. Both are rare, so `.ai/audio-direction.md`'s loudness table allows them to sit louder than a
hit. Generate them in `tools/audio/`, add them to `SfxCatalog`, and read the direction doc first.

### 7.8 Probes

`_UiShot` already takes `--teleport=X,Y`. A `--secret=<id>` flag that places that secret beside the
spawn is what makes each one photographable, and a headless probe that walks to every secret in
a fixed-seed run and completes it is what makes the set testable, the way the side events were
driven. Neither says whether a secret is fun; that needs a playtest.

## 8. Anti-frustration rules

1. **Every secret has a near tell visible on screen before it asks for anything.** Nothing is
   found by walking over an invisible square.
2. **Nothing needs a button, a text, or a particular build.** *Strike* counts any damage landed on
   enemies near the site, so every loadout can do it; no secret asks for an element.
3. **Commitment is short.** No interaction asks for more than about ten seconds, and no hold for
   more than five.
4. **A mistake resets, it never locks.** A wrong stone dims the sequence; a dropped seed lies where
   it fell; a missed sundial window comes round again. Only the risk secrets (the bell, the
   sentry) are once per run, and they are chosen.
5. **Progress survives the chunk.** Walking away and back never costs progress.
6. **Every run has one in reach.** The anchor is guaranteed three to four chunks out.
7. **The keepsake is never behind a coin flip.** It exists in every run; only its bearing varies.
   Once the ledger is found, its far tell starts earlier.
8. **Found is found.** Meta rewards are saved the moment they are earned; dying after does not
   take them back.
9. **Nothing hides in a hazard, water or a wall**, and nothing sits on the side event's site.
10. **The boss ends the hunt.** Secrets stand down when it arrives, as chests do.

## 9. How this answers the old issues

- **#35** (infinite map, coordinate-based discoveries): the map is endless now; this is the
  discoveries half. Coordinates are seeded per run rather than fixed for all time, because a fixed
  coordinate in an endless world is found once by the community and then is a number on a wiki.
- **#56** (static map elements at fixed coordinates): the landmark plus `SecretPlacer` is that
  system, with "fixed" meaning fixed for the run.
- **#20** (in-map permanent upgrade pickups): answered by the four keepsake found levels, narrowed
  to the four comfort stats for the reasons in section 3. The issue asked for every stat; the
  Apotheosis floor is why it does not get them.
- **#62** (Mines crystal fields): its gradient is the far tell for every secret; its breakables
  are C2; its imprisoned wizard is a side event for the Sunken Cave and should be planned in
  `side-events.md`.

## 10. Build order

### The first three

The three that need the fewest new systems and give the most delight, in order:

1. **The Fenced Bloom** (Forest, S). The user's own example, standing on the landmark the decor
   session is already building. It brings up the core every later secret uses: `SecretCatalog`,
   the anchor placement, `SecretRunState`, the *circle* primitive (an angle accumulator), the
   heal and sanctuary rewards, the found toast, and `FoundSecretIds` in the save. A flower that
   turns to watch you walk round it is the right first impression of the whole system.
2. **The Fairy Ring** (Forest, S). No new art at all: the mushrooms already exist and already
   read. It brings up the per-chunk super-cell scatter and the bands, which the anchor alone does
   not exercise, and a tactical reward a player will come back to.
3. **The Buried Cart** (Scorched Sands, S). Existing props, and the *strike* primitive on the
   thaw's listener, which C2, D3, R4 and every breakable after it reuse. It takes the system out
   of the Forest early, before Forest assumptions harden into it.

### Then

4. **Schema 17 (built) and the Secrets page**, before the first keepsake: the page needs found ids, and a
   keepsake needs `FoundUpgradeLevels`.
5. **The First Tree** (Forest keepsake): *carry* extracted from `GaolersKeyEvent`, keepsake
   placement, the first found level, and the balance re-run with found levels granted.
6. **The *follow* mover** and the cheap secrets on it: the Warm Wisp, the Misfiled Page, Chalk
   Marks, the Dog's Find.
7. **The decor gradient** (far tells) once the decor session's flowers, scrub and reeds exist.
8. **The rest of the keepsakes**, one chapter at a time, the Gaoler's Ledger first since it makes
   all the others easier to find.
9. **The two L items last**: the Crystal Vein (breakables) and the Lily Crossing (runtime terrain).
   Each is a system, not a secret, and both should be planned against `terrain.md` before a line
   is written.

## 11. Open questions

- **Should secret positions ever be fixed across runs** (one Fenced Bloom always at the same
  place) rather than seeded per run? Seeded is the recommendation, for the reason in section 9;
  a hybrid (the keepsake's band fixed, its bearing seeded) is available if seeded feels arbitrary.
- **Is the Arcane budget per run (40) and per keepsake (150) right?** First guesses, against 8 a
  real minute.
- **Do the sanctuary heals (about +0.5 HP/s on a 20 HP base) make standing at a site the dominant
  play late in a run?** Watch for it in the first playtest; the fix is a cap on time inside, not a
  smaller number.
- **Does every keepsake need a bespoke interaction, or could later chapters share one?** Every one
  above is distinct; if build time says otherwise, *guarded* is the one to reuse.

## 12. What was built (issue #20, 2026-10-28)

The first slice of section 10: the Fenced Bloom (1), schema 17 and a Secrets list (4) and the First
Tree (5). The Fairy Ring, the Buried Cart and everything after are still the plan above.

| piece | where |
|---|---|
| the catalog, the single authority | `SecretCatalog` - two entries; `RegressionChecks.ValidateSecretCatalog` holds sections 1, 3 and 7.5's rules (no meta reward but the page, a found level of the four comfort stats, or Arcane up to 150; one found level of each stat ever; one keepsake a chapter; the Sketchbook's page only) |
| the run's half | `SecretDirector`, added beside the `SideEventDirector` before the first chunk is furnished: the run state (`StateOf(key)`), the landmark hook, the keepsake's placement, the seeds, finding |
| the Fenced Bloom | `FencedBloom`, attached to every flower pen as it streams in (`Landmark.Spawned`, deferred a frame because a child added inside a parent's enter-tree is refused) |
| the seed | `SecretSeed`: carried over the head, three hits knock it loose (the Gaoler's key's rule) |
| the First Tree | `FirstTree`: placed by `ExplorePlacer` on its own stream (7), 10 to 12 chunks out, its ground reserved |
| finding | `SecretDirector.Found`: a toast (`SecretToast`), `SaveData.RecordSecretFound`, and once ever the keepsake's pay, saved at once |
| found levels | `SaveData.FoundUpgradeLevels`, summed with the shop's in `Player.ApplyArcaneUpgrades` (so they stack above the cap, as #20 asks), and applied on the spot when earned mid-run (`Player.ApplyFoundLevelNow`) |
| the Secrets list | a section at the foot of Unlocks (`MainMenu.AddSecretsSection`) |

**Where it departs from the plan, and why:**

- **Every Forest flower pen is a Fenced Bloom, and there is no forced anchor.** Pens are scattered
  at one chunk in four (`Node2DGame.LandmarkChance`), so about eighteen lie within four chunks of the
  start: "every run has one in reach" (rule 6) holds without placing one. Each bloom heals half and
  drops a seed once a run; only the first a run shows the toast. Whether that much healing on the map
  is too much is the sanctuary question in section 11, sharper now.
- **The keepsake's far tell is the seed, not a decor gradient.** The gradient (section 2) is unbuilt,
  and without a tell a keepsake twelve chunks out on a seeded bearing is the coin flip rule 7
  forbids. So the seed knows where it belongs: carried, it puts out a shoot that leans toward the
  First Tree and lets a mote drift off that way now and then. It is not a pointer - no arrow, nothing
  at the screen edge, nothing until a bloom has been opened - but it is a direction, which section 2
  said a secret never gives. Reconsider when the gradient exists.
- **The Secrets page is a section of Unlocks**, not a page of its own: a new page needs a button on
  the title art. Found secrets are named with what they were; a chapter with more to find says how
  many, and never names them.
- **No new art and no new sounds.** The bloom's glint and lap motes, the seed, the stump ring, the
  bed and the sapling draw themselves on the art grid (as the side events' sites do), and finding
  reuses `meta_unlock` (first time ever) and `event_reward`. Section 7.7's two sounds are still to
  generate.
- **The found level stays on the four comfort stats** (section 3), not "any shop stat" as #20's
  proposed decision has it: the Apotheosis floor was measured with the shop at its cap.

**Driving it:** `_UiShot --secret-probe` (under xvfb, `--stage=0`) walks a lap round the nearest
pen, takes the seed to the First Tree and plants it, checking each step - the bloom opens and heals
half, the seed drops and is carried, the tree is found, the save gets one found level of Vitality,
and the player +5 max HP at once - and saves `secret-N.png` at each. `--found-secrets=id,id` marks
secrets found in the in-memory save, to photograph the Unlocks list with something in it.

**Still to check by play:** whether the lap reads without being told (the turning flower and the
motes are the only tutorial), whether anyone follows the seed's lean twelve chunks out in a
fifteen-minute run, and whether the healing on every pen changes how the Forest is played.

## 13. The secret core (piece A4, 2026-10-30), and how a chapter adds a secret

Piece A4 of `.ai/achievements-and-secrets.md` (section 9): what every chapter's secrets stand on, so
the agents building chapters in parallel each write their own catalog file and their own nodes and
**never edit a shared file**. Built with one vault end to end as its proof - the Sunken Cave's Geode
Heart (that doc's 8.4.1, and its section 12).

### 13.1 The pieces

| piece | file | what it is |
|---|---|---|
| the catalog, one file a chapter | `SecretCatalog.cs` (shared: the enums, `SecretDefinition`, the list of chapters, the deeds) and `SecretCatalog.<Chapter>.cs` (eleven, the `StageBestiary` names: `EnchantedForest`, `CursedDungeon`, `SunkenCave`, ...) | a chapter's rows of section 8, in a nested class with a `Build()`. A1's nine vaults moved into their chapters' files. `SecretDefinition` is a record, so a chapter builds A1's written-down vault with `Vault(...) with { IsBuilt = true, CreateSite = ... }` |
| how it gets into a run | `SecretDefinition.Placement`, `Footprint`, `CreateSite`, `Install`, `RepeatReward` | `Anchor` (3-4 chunks, stream 9, at least 90 degrees off the side event), `Keepsake` (10-12 chunks, stream 7 - the First Tree's draw, unchanged), `Vault` (5-7 chunks, stream 8, at least 90 degrees off the side event and the keepsake; when they leave no such gap, the bearing farthest from both) or `Custom` (its `Install` hooks it in - the Fenced Bloom rides every flower pen through `SecretDirector.HookLandmarks`). `ValidateSecretCatalog` refuses a built secret with no way into the world, a vault not placed as a vault and a keepsake not placed as a keepsake |
| the director | `SecretDirector` | places every built secret of the chapter by its placement, once at stage start, reserving its footprint; the run state (`StateOf(key)`: `Progress`, `Done`, `Step`, `Marks`); the **seen mark**; `Found`; the chest budget (`PayChest`, two a run); the far tells and the solid ground (below); `PlaceBesideSpawn` for `_UiShot --secret=` |
| the base node | `SecretSite` | what every secret's node is: its id and director, its run state (`State`, keyed by `Key`), the seen check (`TellPosition`, `SeenRadius`), the warm glint (`DrawGlint`, `Breath`, `GoldHi`), and for a placed site `LayOut(placer)` (more points from the same seeded stream), `SolidFootprints` and `FarTell`. `FencedBloom` and `FirstTree` are `SecretSite`s now |
| the vault | `SecretVault` | `Open(at)`: the vault is done for the run, and pays - the deed and its spell the first time ever (`Found`), a chest from the budget in a later run (6.4's exhausted rule) |
| the seen mark | `SecretDirector._Process` | the first time the player is within a site's `SeenRadius` (450 px) of its `TellPosition`, the id goes on `SaveData.GlimpsedSecretIds` (saved with the next save) and `secret_seen` breathes once at the site, once per secret per save. Nothing else - no toast, no text. Not while the secrets stand down |
| finding | `SecretDirector.Found` | as before (the toast, the page mark, a keepsake's pay), and now **`AchievementManager.CompleteNow`** with the secret's deed every find - a vault's spell is granted and saved at once, recorded for the results screen (`RecordDiscovery`), and the level-up pool (which reads the save live) offers it from the next level-up. A find again plays `secret_found`, the first find `meta_unlock`. A vault whose spell was already owned (bought) says "you already had it" |
| the far tell | `SecretFarTell` | decor thickening toward a site over two chunks (quadratic, never a ring), with a near band where some pieces wear `NearTextures` (the cracked clusters). Furnished into each chunk as it streams in - `Node2DGame.FurnishChunk` calls `SecretDirector.FurnishChunk` beside the curated props - from its own rolls of (seed, chunk), every roll drawn before the spot is tested, under every rule a scattered sprite keeps (`Node2DGame.IsDecorSpotOpen`). No collision: a direction to walk in, not more to snag on. Each sprite is in the group `secret_far_tell` (or `_near`) |
| solid ground | `SecretSite.SolidFootprints`, `SecretDirector.StampChunk` | circles stamped into the chunk's navigation flags as a fence is (`TerrainStreamer.FlagFence`) every time the chunk's data is made (`Node2DGame.PlanChunkFeatures` calls it), so the flow field routes the horde round, nothing spawns or drops inside, and a shot still flies over. The body that stops boots and bodies is the site's own `StaticBody2D` on `LevelTilePainter.TerrainCollisionLayer` |
| strike | `StrikeListener`, `Breakable` | extracted from the thaw, which counts through `StrikeListener` and draws its cracks with `Breakable.DrawCracks` exactly as before. `StrikeListener`: damage landed on enemies within a radius of a point (`Player.EnemyDamagedAt`), weighted per spell. `Breakable`: a node with `Needed`, `Reach`, `StageCount` crack stages, its progress in a run state, listening only while the player is near and the owner allows (`CanProgress`), drawn from a strip (whole, each stage, broken) or in code; `Broke` fires the frame after the last hit, never inside it |
| follow | `SecretMover` | moves ahead along a path when he comes within `StartleRadius`, waits facing him, settles at the end; goes home if he wanders `LeashRadius` off. `SecretMover.LayPath(placer, ...)` lays the path in `LayOut`. Draws a warm wisp unless the owner draws the figure |
| gather | `SecretGather` | touch every point of a set, in any order (`State.Marks`, a bit a point, up to 64). `SecretGather.AlongTrail(placer, ...)` strews them along a line |
| lure | `SecretLure` | an ordinary enemy whose feet enter the zone is taken - out of `"enemies"` that frame, freed, no XP and no kill - and counted to `Needed`. Never an elite or a boss. The hazard to the player the Swallow-Hole also is, is the vault's own rule |
| carry | `SecretSeed` | still the Forest's; generalise it when the second carry secret is built |

**Not built, and the one shared edit still to come:** the per-chunk *scatter* of section 7.2 - common
secrets at a probability per super-cell in a band (the Fairy Ring, the Struck Oak, the Peat Bank). The
first chapter that needs one adds `SecretPlacement.Scatter` to `SecretDirector.FurnishChunk` (the hook
is already there: a scattered site is a node in the chunk's holder, keyed by its super-cell).

### 13.2 Adding a secret (the recipe)

1. **The entry**, in your chapter's file `scripts/SecretCatalog.<Chapter>.cs` - never `SecretCatalog.cs`.
   A vault: A1 wrote it down already; build it with `Vault(...) with { IsBuilt = true, CreateSite = () =>
   new global::MyVault() }` (it is placed as `Vault` with `SecretCatalog.VaultFootprint` by default).
   Anything else: a `new SecretDefinition { ... }` with its `Placement` (`Anchor`, `Keepsake` or `Custom`),
   `Footprint`, and `CreateSite` (or, for `Custom`, `Install`). Words: a `SeenLine` that says what is
   seen and never what to do, a `FoundLine`, a `RunReward` for the toast (and a `RepeatReward` if a later
   find pays something else). What it may pay is sections 3 and `.ai/achievements-and-secrets.md` 6.2;
   `ValidateSecretCatalog` holds it.
2. **The node**, `scripts/<Name>.cs`: a `SecretSite` (a vault: `SecretVault`), code-made, no scene, never
   in `"enemies"`. Its progress lives in `State` or `Director.StateOf(Key + "/part")`, never in a field
   that must outlive a chunk. Its near tell is `DrawGlint` (or its own breathing light, never a flicker).
   It checks `Active` (the secrets stand down with the boss; rewards in flight are still paid). It pays
   through `Director.Found(SecretId)` - a vault through `Open(at)` - and any chest through
   `Director.PayChest`. Anything it draws that flies goes on `DrawLayers.Air` with `ZAsRelative = false`.
3. **The verb**: strike is a `Breakable` child (or a `StrikeListener`); follow a `SecretMover` with a
   path from `SecretMover.LayPath` in `LayOut`; gather a `SecretGather` (`AlongTrail`); lure a
   `SecretLure`; circle is the Fenced Bloom's angle accumulator; linger and touch are a distance and a
   timer in the node's own `_Process`.
4. **What a placed site asks of the ground**: `LayOut(placer)` for anything else seeded (draw it from
   that placer, and its footprints are reserved for you); `SolidFootprints` and your own body on the
   terrain layer if it stops feet; `FarTell` as one `static readonly SecretFarTell` (it caches its
   textures).
5. **Art**: `tools/art/secret_<chapter>.py`, standalone like `tools/art/secret_cave.py`, writing to
   `assets/bonelight/world/<chapter>/`, audited against the palette, runnable on Python 3.13 with 2.7
   syntax. Then `--import`, and commit the `.import` and `.uid` sidecars.
6. **Sound**: `secret_seen`, `secret_found` and `meta_unlock` are the director's. A sound of the
   secret's own goes through `SfxCatalog` (the Geode Heart's `secret_shatter`).
7. **Check**: `dotnet build`; the validator; a photograph beside the spawn (`_UiShot --scene=res://scenes/node_2d_game.tscn
   --stage=N --secret=<id> --goto=secret --hold`, and `--secret-stage=N` for a breakable's stages); and
   the headless walk - `--secret-probe` runs `VaultProbe` on any chapter with a built vault (it walks to
   `Director.Vault` and works a `Breakable` named `Crystal`; a vault with another verb adds its branch
   there). Before a vault ships: `_BalanceHarness --seek-apotheosis --all-unlocked --immortal`.

### 13.3 The Geode Heart, driven

`--secret=geode_heart` stands it a quarter-chunk east of the spawn; `--goto=secret --hold` stands the
wizard 170 px south of its foot; `--secret-stage=1|2|3|4` poses it (4 breaks it: the shatter, the light,
the toast). `--secret-probe --stage=2 --fresh-save --terrain-seed=N` walks a fixed seed from the start to
the real placement and checks, in order: placed five to seven chunks out; never a target; its foot solid
in the grid; unseen on the way in (a step outside the 450 px) and seen at its foot, with the mark on the save; the far tell
denser within a chunk than beyond it, the cracked clusters only near; a hit beyond its reach counting for
nothing; hits within it cracking it through every stage to broken; the witnesses in the ring frozen;
then the vault opened, found and its deed recorded, Frost Shard owned and in the level-up pool and not
in the loadout, and no chest. With `--all-unlocked` instead of `--fresh-save` it checks a later run: a
chest from the budget and nothing granted.
