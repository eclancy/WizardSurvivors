# Achievements and secrets: every spell earned, and much more to find

Eric, 2026-10-29:

> "Unlocking all non-default-unlocked spells should be something that you get through achievements.
> Sometimes those achievements are simple challenges, and some are secrets you have to find in the
> world. regardless, all achievements should be listed or revealed as the player plays the game in
> the achievements menu in the main menu (and in the pause menu)
>
> Let's add significantly more secrets, like a giant crystal that needs to be destroyed to find a
> related spell, etc."

**Status: design, decided 2026-10-30; the unlock core (A1) and the secret core (A4, with the first
vault, the Geode Heart - section 12) built, the rest not.** Eric accepted every
recommendation (section 10), confirming three directly: the final gate is **every wizard freed and the
Long Coil beaten**; the shop stops selling spells and Arcane Energy keeps the stat upgrades and the
boons; Fireball goes to the Long Coil. The unlock core (piece A1, section 9) is built on branch
`wip/achievements-core`; **section 11 says what it built and where it departs from this design** -
read it before building A2, A3 or A4 against it. Its sister doc,
`.ai/endgame-castle.md`, designs the castle chapters from the same message - and there Eric chose to
**move the Cursed Dungeon into the castle**, which this doc now follows: the Dungeon, its deed, its
key event and Tituba, and its spell vault are past the gate (8.5). Castle secrets are listed here
(section 8.12) and their rooms there.

Read with `.ai/stage-secrets.md` (what a secret is, its tells, its placement, its rules - all of it
still holds except the payment rule, which section 6 revises), `.ai/side-events.md`,
`.ai/campaign.md`, `.ai/world-and-tone.md`, `.ai/passives-and-items.md`, `.ai/run-pacing.md`,
`.ai/gameplay-design.md` ("The Apotheosis"), `.ai/level-up-screen.md` 4c (how the Unlocks list is
laid out) and `.ai/controller-and-focus.md`.

---

## 1. The audit: how everything is obtained today

Read from `scripts/UnlockCatalog.cs`, `scripts/AchievementDefinition.cs`, `scripts/BoonCatalog.cs`,
`scripts/SecretCatalog.cs` and the `CharacterData_*.tres` on 2026-10-29.

### 1.1 Spells (35)

| spell | element | source today | how |
|---|---|---|---|
| Cinderbreath | Fire | **Starter** | the Pyromancer's opener |
| Cone of Cold | Ice | **Starter** | the Frostweaver's opener |
| Chain Lightning | Lightning | **Starter** | the Stormcaller's opener |
| Obsidian Spike | Earth | **Starter** | the Geomancer's opener |
| Magic Missile | Arcane, Lightning | **Starter** | |
| Aegis Ward | Metal, Light | **Starter** | the one shield |
| Arcane Explosion | Arcane | Achievement | First Blood: defeat 1 enemy |
| Frost Shard | Ice, Water | Achievement | Survivor: survive 10 minutes in one run |
| Meteor Swarm | Fire, Earth | Achievement | Veteran: clear a chapter |
| Solar Flare | Light, Fire | Achievement | Archmage Training: reach level 20 |
| Cyclone Slash | Wind | Achievement | Elementalist: 4 of any one element (Laelaps opens on it) |
| Void Lance | Arcane, Darkness | Achievement | Full Grimoire: six spells at once |
| Scorching Ray | Fire, Arcane | Achievement | Fire Adept: 4 Fire |
| Glacial Spike | Earth, Ice | Achievement | Ice Adept: 4 Ice |
| Toxic Spore Burst | Poison, Grass | Achievement | Poison Adept: 4 Poison |
| Black Tentacles | Poison, Earth | Achievement | Earth Adept: 4 Earth |
| Gale Blade | Wind, Lightning | Achievement | Wind Adept: 4 Wind |
| Thorned Vine | Grass, Poison | Achievement | Elderbark (Forest boss) |
| Riptide | Water | Achievement | the Hollow Choir (Cave boss); Tituba opens on it |
| Shadow Bolt | Darkness, Poison | Achievement | the Gaoler (Dungeon boss) |
| Mirefoot | Water, Grass | Achievement | Mother Rot (Swamp boss) |
| Spiritual Weapon | Arcane, Light | Achievement | the Archivist (Ruins boss) |
| Fireball | Fire | Achievement | **the Warden of the Deep (Emberdeep boss) - see 1.4** |
| Molten Shard | Metal, Fire | Purchase | 110 Arcane Energy |
| Sleet Runes | Ice, Wind | Purchase | 130 |
| Hunter's Draw | Metal, Wind | Purchase | 140 |
| Gravewell | Earth, Darkness | Purchase | 150 |
| Starfall | Light | Purchase | 150 |
| Hollow Star | Darkness | Purchase | 160; John Dee opens on it |
| Stormsweep | Lightning | Purchase | 160 |
| Kindled Ward | Light | Purchase | 170 |
| Familiar | Arcane | Purchase | 170; the Coursing (Laelaps) needs it |
| Bramble Seed | Grass | Discovery | the grove (Forest side event); Väinämöinen opens on it |
| Contagion | Poison | Discovery | the bog lanterns (Swamp side event) |
| Iron Palisade | Metal | Discovery | the caravan master (Sands side event) |

6 starters, 17 achievements, 9 purchases (1,340 Arcane Energy in all), 3 discoveries.

### 1.2 Wizards (8, and the sandbox)

| wizard | source | how |
|---|---|---|
| Pyromancer, Frostweaver, Stormcaller, Geomancer | Starter | |
| Tituba | Discovery | the Gaoler's key (Dungeon); secret until the Dungeon opens |
| Väinämöinen | Discovery | the thaw (Frozen Waste); secret until the Waste opens |
| John Dee | Achievement | The Library at Mortlake: open 30 chests |
| Laelaps | Achievement | The Coursing (Broken Circle); secret until the Circle opens; outside the campaign gate |
| Test Wizard | Starter | development sandbox, not counted anywhere |

### 1.3 Boons (15)

| boon | source | how |
|---|---|---|
| Iron Rivets, Tidewater Flask, Lantern Oil, Mossgrown Charm, Taproot | Starter | |
| Umbral Veil | Achievement | Untouchable: 3 minutes without losing health |
| Thornseed | Achievement | the Hayward (Fields boss) |
| Shadegrease | Achievement | the Ninth Stone (Circle boss) |
| Rimebriar | Achievement | the Still Warden (Waste boss) |
| Sunsteel Filament | Achievement | the Long Coil (Sands boss) |
| Wishing Coin | Discovery | the well (Fields side event) |
| Gilded Mote | Discovery | the moot (Circle side event) |
| Quicksilver Bead, Deepwater Pearl, Saltbound Chain | Purchase | 110, 120, 140 |

### 1.4 What the audit found

1. **A new save can never open the Emberdeep.** Its gate is `IsCampaignComplete`: every spell
   recovered and every wizard freed, evaluated live (`GlobalStatsManager`). Fireball is a spell, and
   its only source is the deed for beating the Warden of the Deep - who lives in the Emberdeep. The
   Boss Rush, the other place he is fought, opens only once he has fallen there. So a save written
   at schema 8 or later is locked out of the final chapter for good. Only saves older than the unlock
   economy hold Fireball (the schema 8 grant of `LegacyDefaultUnlockedSpellIds`). Nothing reports it:
   `ValidateCampaignMigration` checks the gate on `SaveWithEverything()`, which already owns Fireball.
   **The rule this breaks:** a gate must never count something that can only be earned behind it.
   Section 2.2 fixes the case, and section 9 (A1) adds the check.
2. **The 9 shop spells are about 5% of what Arcane Energy buys.** The Powers shelf costs about
   25,000 to finish (Luck and Keen Focus alone are about 14,000). Taking the spells out of the shop
   takes very little away from Arcane Energy (2.4).
3. **Discoveries are not achievements.** A won side event pays its spell, wizard or boon, but it
   never appears in Unlocks, so seven of the game's unlocks are listed nowhere a player can see what
   is left. (The world map's `StageRewards` lists them per chapter.)
4. **Secrets are a section of Unlocks on the main menu only.** The pause tab has no secrets, and it
   shows lifetime progress, not this run's: it builds `AchievementContext.ForSave`, so the Coursing's
   "this run's count", which the deed's comment promises, never shows.
5. **Achievements are only evaluated when a run ends** (`AchievementManager.ApplyRunAchievements`).
   Anything found in a run (a secret, an event) has to be paid at once by its own path
   (`DiscoveryRewards`, `SecretDirector.Found`), and those paths do not record an achievement.
6. **The main menu and the pause tab build the list twice** (`MainMenu.BuildAchievementCards`,
   `Node2DGame.ShowEscapeAchievements`). Only the row is shared (`CodexRows.BuildDeedRow`).

---

## 2. The rule

**Every spell that is not a starter is obtained through exactly one achievement.** An achievement
is one of three kinds:

| kind | what the player does | listed | completes |
|---|---|---|---|
| **Feat** | a simple challenge measured over runs: kills, time survived, level, elements held | always | at the end of the run that meets it (as today) |
| **Road** | a chapter's deed: beat its boss, win its side event | once its chapter is playable; a deed that frees a secret wizard is sealed until his chapter opens (as the character select already does) | the boss at the end of the run; the event **at once**, saved on the spot |
| **Secret** | something found in the world | hidden until the player has seen it (section 4.3) | **at once**, saved on the spot |

The six starters stay starters. Wizards and boons are not under Eric's rule, but the same three
kinds already cover them, so the achievements menu becomes the one complete list of how anything is
earned (2.3).

### 2.1 Every non-starter spell, and its achievement

| spell | achievement | kind | what the player does | why it suits the spell |
|---|---|---|---|---|
| Arcane Explosion | First Blood | Feat | defeat 1 enemy | unchanged. The first thing anyone does, and a spell that charges off the others wants to arrive while there are others to charge it |
| **Hollow Star** | **Survivor** | Feat | survive 10 minutes in one run | **moved from the shop.** The only pure Darkness carrier; it was sold rather than earned so Darkness could be built early (`UnlockCatalog`'s comment). Ten minutes is the first good run, so it stays early |
| Meteor Swarm | Veteran | Feat | clear a chapter without falling | unchanged |
| Solar Flare | Archmage Training | Feat | reach level 20 in a run | unchanged |
| Void Lance | Full Grimoire | Feat | carry six spells at once | unchanged |
| Cyclone Slash | Elementalist | Feat | hold 4 of any one element | unchanged |
| Scorching Ray, Glacial Spike, Toxic Spore Burst, Black Tentacles, Gale Blade | the five Adepts | Feat | hold 4 of Fire, Ice, Poison, Earth, Wind | unchanged |
| Bramble Seed | **The Grove Held** | Road | win the grove (Forest) | was a Discovery; now a deed with the same trigger |
| Thorned Vine | Forest Cleared | Road | beat Elderbark | unchanged |
| Riptide | Silence in the Deep | Road | beat the Hollow Choir | unchanged |
| Shadow Bolt | Dungeon Conqueror | Road | beat the Gaoler | unchanged; the Dungeon is in the castle now, past the gate (8.5) |
| Contagion | **The Lanterns Out** | Road | snuff the bog lanterns (Swamp) | was a Discovery |
| Mirefoot | Cut the Rot Out | Road | beat Mother Rot | unchanged |
| Spiritual Weapon | Ruins Delver | Road | beat the Archivist | unchanged |
| Iron Palisade | **The Caravan Stopped** | Road | catch the caravan master (Sands) | was a Discovery |
| **Fireball** | **Unwound** | Road | beat the Long Coil (Sands); the deed keeps paying Sunsteel Filament too | **moved from the Warden of the Deep** (1.4). The big slow blast is a late shape, and chapter 9 is the latest place the gate can pay it |
| **Hunter's Draw** | **The Pale Hart** | Secret | follow a hart of warm light through the wood to an old hunter's stand (Forest) | a bow that charges while you move and looses when you stop: the hart is followed by moving and waits when you stop. The wood's own hunters used the stand |
| **Familiar** | **The Pinfold** | Secret | a hound of light in the Hayward's pound; wake its guards, beat them, and it comes to you (Fields) | the Hayward impounded strays (`campaign.md` 5). The Familiar is that hound, and the next chapter's Coursing needs it |
| **Starfall** | **The Fallen Star** | Secret | gather the seven shards strewn along a fallen star's trail, then stand in its crater (Broken Circle) | a rain of many small lights, earned by picking up many small lights. The moot watched this sky |
| **Frost Shard** | **The Geode Heart** | Secret | break a crystal three men tall by fighting at its foot (Sunken Cave) | **Eric's giant crystal.** Frost Shard makes every death shatter; it is learned by shattering the biggest thing in the cave. Moved from Survivor |
| **Molten Shard** | **The Cold Forge** | Secret | kill on the hearth of the gaol's dead smithy until it roars (Cursed Dungeon, in the castle now) | Metal and Fire: the forge that made the shackles makes your shards |
| **Gravewell** | **The Swallow-Hole** | Secret | lure thirty of his creatures into a sinkhole in the bog (Blighted Swamp) | a trap you let them come to, learned from a hole they walked into |
| **Kindled Ward** | **The Broken Ward** | Secret | touch five notched ward stones in notch order; the ward re-forms, and its light comes to you (Mystic Ruins) | "The old wards were broken from the inside." A ward that stands on its own, from a ward made whole |
| **Sleet Runes** | **The Buried Runes** | Secret | stand still in a drift field until the snow settles, then dig up the rune-stones it shows (Frozen Waste) | walking aims the runes; stopping is what finds them |
| **Stormsweep** | **The Fulgurite** | Secret | stand by a glass tree in the dunes when the dry storm comes round and draws its lightning (Scorched Sands) | lightning that strikes sand leaves glass; a sweep of lightning, from where it struck |

Totals: **11 Feats, 9 Road deeds, 9 secrets** - one spell vault in each campaign chapter but the
Emberdeep: eight on the road before the gate, and the Cursed Dungeon's, which moves past it into the
castle with its chapter (`.ai/endgame-castle.md` 1.3). The gate counts no spells (2.5), so a vault
behind it closes nothing. Each vault is detailed in section 8.

### 2.2 What moved, and why

| what | from | to | why |
|---|---|---|---|
| Fireball | the Warden of the Deep's deed | the Long Coil's deed, beside Sunsteel Filament | 1.4: a spell the gate counts cannot be paid behind it. A deed already carries a spell, a boon and a wizard field, so one deed paying two things needs no new data |
| the Warden of the Deep's deed | Fireball | 300 Arcane Energy, and it opens the castle (`.ai/endgame-castle.md`) | Sunsteel Filament stays before the gate, where Light and Metal need it |
| Frost Shard | Survivor | the Geode Heart | the crystal's spell |
| Hollow Star | the shop | Survivor | Darkness's only pure carrier stays early |
| the other 8 shop spells | the shop | eight spell vaults | Eric's rule |
| Bramble Seed, Contagion, Iron Palisade | Discovery | Road deeds with the same triggers | Eric's rule; and they become visible in the list |

The Ice Adept keeps Glacial Spike. Moving it to the crystal (it is the more literal crystal spell)
would have left the Ice Adept with nothing to pay and moved three things instead of one; Frost
Shard's shatter is the better story anyway.

### 2.3 Wizards and boons

- **Tituba, Väinämöinen, Wishing Coin and Gilded Mote become Road deeds** with their side events as
  triggers ("Tituba Freed" - built as **"The Open Cell"**, section 11 - "Out of the Ice", "The Last
  Coin", "The Circle Relit"), granted at once as
  now. Their reveal rules are unchanged: a deed that frees a secret wizard is a sealed row until his
  chapter opens. Tituba's chapter is the castle's dungeon now, so on a new save her row stays sealed
  until late; how the gates treat her is `.ai/endgame-castle.md` 1.6.
- **John Dee and Laelaps** are already achievements.
- **The three shop boons stay in the shop** (2.4). Boons are not under the rule, and Water draws
  two of its carriers from them: holding them back behind chapters would thin the element Tituba's
  Apotheosis needs.

`UnlockSource.Discovery` survives only as the trigger of a Road deed: `UnlockCatalog` keeps the site
id, and `ValidateUnlockCatalog` requires that **every spell's source is `Starter` or `Achievement`**.

### 2.4 Purchase, the shop, and Arcane Energy

- **The shop sells no spells.** The "New spells" shelf leaves `MainMenu.Shop.cs`. No refund is due:
  a bought spell stays bought, and an unbought one cost nothing.
- **What Arcane Energy is for: Powers, the three boons, and Redaction.** That was 95% of it
  already (1.4). The early game loses its cheapest buys (110 to 170), so the first purchases a new
  save makes are now Powers levels, which start at 20 to 60.
- **The "Spells & Boons" menu holds three boons.** Decided: fold them into Powers as its last
  section and take the button off the title menu (decision 5). Every card in that shop is then a
  stat or a boon; every spell is on the achievements page.
- **Arcane Energy does not buy a skip.** No achievement can be bought outright: that would make the
  secrets a price list. A hint for a hidden achievement could be sold (decision 10), but is not
  recommended for the first version.

### 2.5 The final chapter's gate (decided: wizards, not spells)

Today the Emberdeep opens on **every spell and every wizard**, live. Under the new rule nine spells
are behind secrets, and `.ai/stage-secrets.md` section 1 already said why that is a problem: a
secret is missable by design, and a gate behind luck is a bad gate. The draft weighed two answers -
narrow the gate to wizards, or keep it total and make the vaults carry it - and **Eric chose to
narrow it**, the fallback `.ai/world-and-tone.md` already named: *"the fiction survives being
narrowed to every wizard freed - losing a spell is losing a weapon, losing a wizard is leaving a
person behind. Narrow it there first."*

- The Emberdeep's gate becomes **the Long Coil beaten and every wizard freed** (a new
  `StageGate.PreviousBossAndWizards`). The previous boss keeps the road one road: without it the
  Sands could be skipped once Väinämöinen is free.
- **"Every wizard" means every wizard whose rescue chapter comes before the gate.** With the Cursed
  Dungeon moved into the castle, Tituba is freed behind the Emberdeep; counting her would be the
  Fireball bug again, for a person. So the gate counts the four starters, John Dee and Väinämöinen
  (Laelaps is outside every gate), and `ValidateGateReachability` holds it. **The rule is written per
  gate chapter** - "every wizard whose rescue comes before *this* chapter" - because the castle doc
  gives the Throne the same kind of gate, where "every wizard" includes Tituba and the fiction's
  sentence is true where he actually is (`.ai/endgame-castle.md` 1.6).
- Spells become the collection, not the key. A Collection deed, **The Grimoire Restored** (every
  spell recovered), pays 500 Arcane Energy and a mark on the save.
- It ends a recurring cost: schemas 14, 15 and 16 exist only because a new spell would have closed
  the Emberdeep to saves that had opened it. A new spell needs no migration now.
- It lets the chapters after the gate pay spells: the Dungeon's Shadow Bolt and Molten Shard already
  do, and close nothing.

Everything a save already has is kept: section 3.

### 2.6 How it is held

- **`AchievementDefinition` gains** `Kind` (Feat, Road, Secret, Collection), `StageId` (the chapter
  it belongs to, for grouping and for the world map card), `Visibility` (Always, ChapterOpen, Hidden),
  `CompletesInRun` (true for events and secrets), and the source it mirrors: `SiteId` or `SecretId`.
- **Secret and event deeds are derived, not written twice.** `AchievementDefinitions.All` is the
  hand-written Feats, Road boss clears and Collection deeds, plus one deed made from every
  `SecretDefinition` (`SecretCatalog.AsAchievement`) and one from every side-event site
  (`SideEventDirector`'s table). `SecretCatalog` stays the authority on secrets; the deed is a view
  of it. A secret that pays a spell names it in `SecretDefinition.SpellUnlockId`, and the derived
  deed carries it.
- **`AchievementManager.CompleteNow(save, id)`** records the deed, grants its reward and saves, the
  `DiscoveryRewards` rule ("granted the moment it is won"). `DiscoveryRewards.GrantSite` and
  `SecretDirector.Found` call it. The results screen reads what was completed in the run from
  `Node2DGame.RecordDiscovery`'s list as it does now.
- **`AchievementManager.Reconcile(save)`** grants the current reward of every deed already done,
  if it is not owned. It only ever adds. It runs in the schema 19 step and after every
  `ApplyRunAchievements`, so a reward that moves between deeds reaches the saves that had done the
  deed (3, step 3) without a schema bump next time.
- **New lifetime facts:** `LifetimeStats.EventsWon` (site ids; written by `DiscoveryRewards`; what
  the event deeds measure) and `SaveData.GlimpsedSecretIds` (4.3).
- **Validators** (`RegressionChecks`):
  - `ValidateUnlockCatalog`: every spell is `Starter` or `Achievement`; every achievement source
    exists, in both directions, as today.
  - **`ValidateGateReachability` (new):** for every gate chapter, everything that gate counts has a
    source that can be met before it - a Road or Secret deed's `StageId` is earlier in the campaign
    than the gate chapter, and a Feat names no chapter at or past it. This is the check that would
    have caught Fireball, and it is what keeps Tituba out of the Emberdeep's count and in the
    Throne's.
  - `ValidateSecretCatalog`: section 6.2's revised rules.
  - `ValidateAchievements`: every derived deed has a name, a measure and a reward; no Hidden deed
    is complete on a new save; every Hidden deed has a sealed line and a seen line (4.3).
  - `ValidateElementReachability` gains a second pass that leaves out spells paid by secrets
    (6.3, item 5).

---

## 3. The save: schema 19

`SaveData.CurrentSchemaVersion` goes to 19, one step, `MigrateToAchievementUnlocks`. **Nothing
anybody owns is taken away**, and every step only adds.

1. **Initialise** `GlimpsedSecretIds` and `LifetimeStats.EventsWon`.
2. **Seed `EventsWon` from what the save owns**, through a frozen table of the seven discovery
   unlocks as they stand at schema 18 (`the_grove` - Bramble Seed, `bog_lanterns` - Contagion,
   `caravan_master` - Iron Palisade, `gaolers_key` - Tituba, `the_thaw` - Väinämöinen, `the_well` -
   Wishing Coin, `the_moot` - Gilded Mote). A save that owns the unlock shows the event's deed done.
   Saves credited with discoveries by schema 10 (a finished campaign) get the deeds too, which is
   what they were credited with.
3. **Reconcile** (2.6): for every deed in `UnlockedAchievementIds`, grant its current reward if it
   is not owned. Concretely: **Survivor done** grants Hollow Star (and Frost Shard, already owned,
   stays); **Unwound done** grants Fireball (Sunsteel Filament stays); **The Long Road Back done**
   keeps Fireball and is not paid its new 300 Arcane Energy - reconciling pays unlocks, never
   currency, because a payment cannot be checked as already owned. A done deed whose reward moved
   away leaves its old reward where it is.
4. **Keep the gate open for anyone who had it open.** If the save met the schema 18 gate - every
   spell in a frozen list of the 35 and every wizard in a frozen list of the 7 - add `stage_7` to
   `UnlockedStageIds`. The gate honours the opened list (rule 1 of `IsStageEarned`), so whatever the
   gate becomes (2.5), a save that could enter the Emberdeep still can. Under option A nobody is
   closed out anyway, except a save that met the old gate without beating the Long Coil, which is
   exactly who this step is for.
5. **Shop spells:** nothing. Bought ones are in `UnlockedSpellIds` and stay. Their vaults still pay
   the run reward and the page mark when found; the row says the spell was already owned.

**Checked** by `RegressionChecks.ValidateAchievementMigration` on sample schema-18 saves: bought
Gravewell (still owned, its vault not done); Survivor done (owns Frost Shard and Hollow Star);
the Long Coil and the Warden both beaten (owns Fireball and Sunsteel Filament); met the old gate
without the Long Coil (still enters the Emberdeep); owns Bramble Seed (the grove deed done); a
fresh save (nothing granted, nothing done); and every one migrated twice (no change the second
time).

**Two traps from the last schema bumps.** Another branch may claim 19 first: whichever lands second
renumbers its step and keeps both (`.ai/campaign.md`, decision 5). And a run of any Godot scene on
this branch migrates the real save: pass `-- --no-save-writes` on every run that is not play.

---

## 4. The achievements menu

The screen exists twice today, as **Unlocks**: a panel on the main menu and a tab on the pause
screen. It keeps both places and one builder.

### 4.1 Its name

Eric called it "the achievements menu"; the player chose "Unlocks" for it earlier because "Deeds"
hid what the entries were for (`.ai/level-up-screen.md` 4c). Decided: **Achievements** (decision
4) - every entry still pays something and says so, and secrets are achievements now, which "Unlocks"
does not say. It is one constant (`CodexRows.UnlocksTitle`) and the subtitle.

### 4.2 What is listed, and when

| visibility | who | before | after |
|---|---|---|---|
| **Always** | Feats, Collection deeds | listed from the first run | - |
| **ChapterOpen** | Road deeds | a boss's deed is listed once its chapter is playable (`AchievementDefinitions.Listed`, as now); a deed that frees a **secret wizard** is a sealed row ("The Cursed Dungeon holds someone") until the chapter opens - the character select's rule | listed in full |
| **Hidden** | Secrets | counted, never named: one row per chapter (4.3) | its own row once seen; full once found |

### 4.3 A hidden achievement, before and after it is revealed

Three states. The words for each live on the `SecretDefinition`, so the screen invents nothing.

| state | when | its row |
|---|---|---|
| **Sealed** | not yet seen | **not a row of its own.** Each open chapter with sealed secrets gets one quiet row: *"The Sunken Cave - 3 things you have not found"*, with one silhouette glyph per sealed secret showing only the **kind** of reward (a spell's page, a keepsake, a page mark). No name, no place, no verb. A list of named blanks is a checklist, which is a different game (`.ai/stage-secrets.md` 7.6) |
| **Seen** | its near tell has been on screen: the player came within about 450 px of the site, once, in any run (`SaveData.GlimpsedSecretIds`) | its own row: the secret's **name** (what it looks like: "The Geode Heart"), **a seen line** - what was seen, written as an observation and never as an instruction ("A crystal three men tall, grown up through the cavern floor. The small ones near it are cracking.") - and what it pays, named ("Frees Frost Shard"). Quiet ink, a "Seen" tag with the eye glyph |
| **Found** | completed | name, the found line (what it was and what opened it), what it paid, the done glyph; quiet with the other done rows |

Why "seen" is the reveal: it is earned by the player's own eyes, so it can never spoil something
the player has not been near; it turns "I saw a strange crystal" into a row they can come back to;
and a player who walked past a vault without understanding it is told only what they saw. Seen
is recorded by `SecretDirector` (one distance check per active site per frame, which the sites
already make for their near tells) and saved with the next save, not at once: losing a seen mark to
a crash costs nothing.

**Two further reveals, both earned:**

- **The Gaoler's Ledger** (`.ai/stage-secrets.md` 6.2, the Dungeon's keepsake) turns every
  keepsake's sealed glyph into a seen row with a one-line hint, as planned.
- **The Library** in the castle's court (`.ai/endgame-castle.md`) does the same for every secret
  in the game: his own record of what he took. It is past the gate, so it is a reward for a
  campaign finished, not a substitute for looking.

**A spell vault is also named on the Spell Index** before it is seen: the locked spell's hint says
the chapter, never the place or the verb ("Hidden somewhere in the Sunken Cave"). That is the one
hint a secret gets without being seen, because it pays a spell (6.4).

### 4.4 Progress

- **A countable deed shows a bar and its numbers**, a yes/no one neither (as now).
- **A deed measured over one run shows the best run on the main menu, and this run on the pause
  screen** - Survivor's clock, the Coursing's hares, Full Grimoire's slots. The pause screen builds
  its context from a live snapshot of the run (`Node2DGame.SnapshotRunResult`, the fields
  `BuildRunResult` fills, without ending anything), so `Math.Max(lifetime best, this run)` is
  finally true where the deed's comment says it is.
- **A secret with steps shows them once seen**: shards gathered, stones touched in order, damage
  landed on the crystal - this run's, on the pause screen; on the main menu only "Seen".
- **A chapter's sealed row shows "2 of 5 found"** with a bar, as the Secrets section does now.

### 4.5 What it pays

Every row ends on its reward, led by a glyph that is the reward itself (`CodexRows.RewardGlyph`):
the spell's icon, the boon's icon, the wizard's portrait at the row's scale, `stat-energy` for
Arcane Energy, the stat's glyph for a found level, the page-mark glyph for a secret that pays only
the page. A sealed secret's glyph is the **silhouette** of its kind
(`CharacterVisuals.BuildSilhouetteFrames`, the world map card's family), never the real icon.
A deed whose reward is already owned (a bought spell's vault) says so: "Frees Gravewell - you
already had it".

### 4.6 Layout

**The main menu panel.** Heading (Display), subtitle, summary ("41 of 88 earned - 6 secrets
found"), then a row of four filter tabs, then the list:

| tab | holds | order |
|---|---|---|
| **All** | everything | In progress (closest first), then the sealed chapter rows, then Done |
| **Chapters** | Road deeds and secrets, grouped by chapter in campaign order | each chapter a section: its boss, its event, its vault, its other secrets (seen ones as rows, sealed ones as the count row), done ones last and quiet. This is the world map card's "what it holds" (`StageRewards`), in a list |
| **Feats** | Feats and Collection | In progress, then Done |
| **Secrets** | secrets only | the chapter rows (sealed counts and seen rows), done last |

The tab chosen is remembered for the session. Rows are `CodexRows` rows: name, one line, then quiet
meta (bar, reward). One column on a phone, two on a PC (`CodexRows.ColumnsFor`).

**The pause tab.** No sub-tabs (it is already a tab in a tab strip). One list, in this order:

1. **This chapter**: the current chapter's deeds and its secrets - the seen ones as rows, the
   sealed count row - with this run's progress. What this run could still finish.
2. **In progress**: everything else not done, closest first.
3. **Done**, collapsed behind one "Show done (31)" toggle, closed by default, Y on a pad.

A secret seen in this run appears in "This chapter" as soon as it is seen: the pause screen is
where a player checks whether the strange crystal was a thing.

### 4.7 Both canvases

- **Phone (720 x 1280, `keep_width`)**: one column, tabs in one row (four short words, about 520
  of the 672 units inside the margin), the summary on two lines. Every edge-anchored control goes
  through `SafeArea.Apply`. Check at `_UiShot --resolution 540x1212 --portrait --safe-area` (the
  Pixel 9).
- **PC (1280 x 720 and wider, `keep_height`)**: two columns, panel capped at 1200 units and centred
  (as now). Check at `_UiShot --resolution 1280x720`.
- Text roles and inks are the codex's; `python tools/art/ui_contrast.py` covers any new ink (the
  seen tag and the silhouette glyph's rim).

### 4.8 Controller and touch

- **`MenuNavigator.Attach(root, firstTab)`** on the main menu panel; the pause tab already sits under
  the pause screen's navigator. `SafeArea.Apply(root)` once built.
- **Tabs are focus stops**: left and right move between them; down enters the list; up from the
  first row returns to the tabs. Choosing a tab rebuilds the list and keeps focus on the tab.
  (Shoulder buttons are not bound to anything in `project.godot`; LB/RB for tabs is a separate
  input-map change, not needed for this.)
- **Every row is one focus stop** (`Entry.FocusStop`), so the stick scrolls the list by moving
  through it; rows ignore the mouse so a finger drag scrolls (`TouchScroll.PrepareAll` through
  `MenuNavigator.Attach`, as the list does now).
- **Y** toggles "Show done" on the pause tab (`MenuBackListener.PadY`), B goes back.
- **Touch**: tabs are buttons; a tap on a row does nothing (a row is text), so a scroll never
  chooses anything.
- **Over a run** the tab is part of the pause screen, which pauses the tree, so the touch stick's
  full-screen STOP control cannot take its taps.

### 4.9 In a run

- A secret or event completed in a run shows the found toast (`SecretToast`, the side event's
  success beat for events) with **what it paid**: "The Geode Heart - Frost Shard is yours". First
  time ever it plays `meta_unlock`, after that `event_reward` (as `SecretDirector.Found` does).
- A secret **seen** for the first time makes the soft shimmer `.ai/stage-secrets.md` 7.7 asks for
  (still to generate) and nothing else: no toast, no text, which would be a pointer.
- The results screen lists everything completed in the run, as now.

### 4.10 One builder

`scripts/AchievementsPage.cs` (new, static, like `CodexRows`) builds the page for both places:
`Build(VBoxContainer list, AchievementContext context, PageMode mode, Filter filter, string
currentStageId)`. `MainMenu.BuildAchievementCards` and `Node2DGame.ShowEscapeAchievements` become
calls to it, and `MainMenu.AddSecretsSection` goes (its rows are the Secrets tab's).

---

## 5. Kinds of secret

A secret is still what `.ai/stage-secrets.md` section 1 says: something he missed, found with the
feet, with a near tell on screen before it asks for anything, no text, no pointer, a short ask, a
mistake that resets and never locks. These are the kinds to draw on. The verbs it already lists
(touch, circle, linger, strike, cull, carry, sequence, follow, hour, guarded) are the parts.

| kind | examples | how it is worked | built from |
|---|---|---|---|
| **Things to break** | the giant crystal, a lightning-split oak, a sealed vault door, a cart under sand, a cracked flagstone | **strike**: damage landed on enemies near it breaks it, in stages the player can see (hairline, cracked, splitting, gone). It is never a target and never joins `"enemies"` (the breakables rule) - which in this genre costs nothing, because the swarm is always at the player's feet: standing beside the crystal and fighting *is* breaking it | the thaw's listener on `Player.EnemyDamagedAt`, extracted once as `StrikeListener`; a `Breakable` node that draws its cracks from progress |
| **Things to follow** | a hart, a wisp, a blown page, footprints, chalk arrows, a meteor's trail of shards | a **mover** that hops ahead when he comes near and waits facing him, and goes home if he wanders off; or a **trail** of marks along a hashed path | `SecretMover` (planned in stage-secrets 7.3, not built); a decal trail placed along a hashed path |
| **Places to reach** | an island over lily pads, a ledge at the end of one causeway, a clearing ringed by trunks with one gap, a balcony up a back stair | the place is stamped into the ground with **one way in**; reaching it is the secret | a terrain stamp keyed by a site that is itself a pure function of the seed, stamped by every neighbouring chunk too (the seam rule, `.ai/terrain.md` 7) |
| **Conditions to meet** | stand still till the snow settles; be untouched for five seconds; be there when the storm comes round; let thirty of them into the hole; kill on the hearth | a condition held at a place: **stillness**, **untouched**, **hour** or **storm** (a site-local cycle on the pace clock), **lure** (enemies that enter a zone are counted, and taken), **cull** | `Player` velocity, `Player.DamageTaken`, `Node2DGame.PaceSeconds`, `Player.EnemyKilled` |
| **Things to gather** | seven star-shards, three lit lamps, scattered pages | touch every one of a set, **in any order**, within a run | a set of touch sites under one id; progress kept in `SecretRunState` |
| **Things to carry** | the seed, the living lantern, the last sheaf | carry a token to its place; three hits knock it loose | `SecretSeed` (built) |
| **Risks, chosen** | the alarm bell, the sleeping sentry, the scarecrow's post | touch it and something hard wakes now; beat it for a chest | `SpawnEventEnemy` with elites |
| **Chains** | bloom to tree, lantern to chapel, sheaf to barn | one secret's reward is another's key | the carry primitive |
| **Across chapters** | the ledger; the castle's keepsake doors | what was found elsewhere opens something here | the save's `FoundSecretIds` |

**Two new verbs**, both small: **gather** (any order, distinct from sequence and from the lanterns'
explore, which is a side event's) and **lure** (distinct from cull: only enemies that walk into the
zone count, so it asks the player to stand at the edge and let them come, which is Gravewell's whole
demand). **Storm** is a kind of hour: a site-local cycle with a visible build (clouds, then
telegraphed strikes drawn to the site) rather than a sundial.

---

## 6. Secrets that pay spells: the rule revised

### 6.1 What the old rule protected

`.ai/stage-secrets.md` section 1 said **a secret never pays a spell, a wizard or a boon**, and
`ValidateSecretCatalog` enforces it (`SecretMetaReward` has no value for one). It protected three
different things:

1. **The campaign gate from luck.** The gate counts every spell and wizard, live; a secret is
   missable; so a secret paying one would make the last chapter depend on a seed and a walk. That
   was the whole stated reason.
2. **The boon draw.** A new boon dilutes the one-in-four or five level-ups that show a boon, and the
   thin elements reach their tiers partly through boons (`.ai/passives-and-items.md` 3b).
3. **The Apotheosis floor - but not through the spell rule.** The floor (no earlier than 7.5 minutes
   for the planned bot with every shop charge at cap) is protected by section 3's *run* rules: no
   XP, levels, element tags, charges or Growth from a secret, and found levels only on four comfort
   stats. The spell rule never touched it, because the floor is measured with every spell owned
   (`_BalanceHarness --all-unlocked`).

### 6.2 The rule now

- **A secret may pay a spell, once ever, through its achievement, on the save.** Never into the run:
  the spell is not added to the loadout, not levelled, not offered on the next level-up. It joins the
  level-up **pool**, from the next level-up of this run.
- **Only a spell vault pays a spell** (6.4), one per chapter.
- **A secret still never pays a wizard or a new boon.** A wizard is the gate's in either option of
  2.5; a boon still dilutes the draw. The one exception is past the gate and outside it (a wizard
  with `OutsideCampaignGate`, as Laelaps), which `.ai/endgame-castle.md` raises for Merlin as a
  decision.
- **Run rewards and found levels are unchanged** (`.ai/stage-secrets.md` section 3): heals,
  sanctuaries, timed buffs under 60 s, tactical effects, two chests a run from all secrets, 40 Arcane
  Energy a run, keepsakes paying a found level of Vitality, Recovery, Magnet or Movespeed once each,
  or up to 150 Arcane Energy.
- `SecretMetaReward` gains `Spell`; `ValidateSecretCatalog` allows it only on a vault, at most one
  vault per chapter, and only for a spell whose catalog source is that vault's deed.

### 6.3 Why the Apotheosis gates stay sound

1. **The floor is measured with every spell owned.** A secret only changes *when* a save owns a
   spell, never what the bot already has. And a bigger pool makes any one wanted card *rarer*, so
   adding a spell to the pool mid-run can only slow a planned build, never speed it.
2. **Nothing is put into the run.** No spell in the loadout, no level, no tag, no guaranteed offer.
   A guaranteed offer is an augury, and auguries were removed from the game on 2026-10-28.
3. **The Apotheosis spell is never behind anything.** The gate is a (wizard, starting spell) pair,
   and `Player.GetLevelUpOptions` always offers a wizard his own starting spell, owned or not
   (`.ai/side-events.md`).
4. **The wizard's own element is carried by his starting spell**, so tier 6 for his Apotheosis
   never waits on a secret.
5. **No element's capstone waits on a secret.** `ValidateElementReachability` gains a second pass
   that counts only starters, Feats and Road deeds **from chapters before the gate** (the build a
   player has on the road): every element must still reach 6. With the Dungeon in the castle, its
   Shadow Bolt and Molten Shard drop out of that pass; Darkness and Poison keep Hollow Star, Void
   Lance, Contagion and the rest, Metal and Fire keep Iron Palisade, Fireball and the boons. Ten of
   the twelve have a pure carrier outside the vaults (Hollow Star moves to a Feat for exactly this).
   **Light is the one to watch**: both its pure spells, Kindled Ward and Starfall, become vaults,
   so it has to reach 6 through hybrids - Aegis Ward, Solar Flare, Spiritual Weapon, their
   evolutions, and Lantern Oil, Gilded Mote, Wishing Coin and Sunsteel Filament. On paper that is
   seven before evolutions. If the second pass warns, Kindled Ward becomes a Feat (a Light Adept)
   and the Ruins' vault pays something else, rather than the rule bending. No wizard opens on Light,
   so no Apotheosis is at stake either way.
6. **Found levels stay on the four comfort stats, once each.** No vault pays one.
7. **Before shipping any vault**, re-run `_BalanceHarness --seek-apotheosis --all-unlocked
   --immortal` once on the branch (nothing in it should move, and that is the check that it did not)
   and the reachability validator.

The castle's throne room grants levels on purpose, which is a different contract; it is set out in
`.ai/endgame-castle.md` and does not touch an ordinary run.

### 6.4 The spell vault

A secret class between the anchor and the keepsake, built so a spell is never a coin flip:

- **In every run of its chapter**, placed once at stage start from the seed (as the keepsake is,
  through `ExplorePlacer` on its own stream), **five to seven chunks out** (35 to 50 s of walking),
  at a bearing at least 90 degrees from the side event's and the keepsake's.
- **A far tell that starts two chunks out** (the decor gradient of `.ai/stage-secrets.md` section 2,
  or the vault's own trail, as the seed's lean was for the First Tree), and a **large** object: a
  vault is the size of the flower pen, readable from 400 px on a phone in a crowd.
- **The chapter is named on the Spell Index** before it is seen (4.3).
- **The ask may run to 20 seconds**, twice an ordinary secret's, because it pays a spell; no hold
  past five seconds still.
- **It stands down with the boss**, like every secret.
- **Found once, it still works**: later runs pay its run reward and a chest from the budget, so it
  stays worth the walk (the side events' exhausted rule).

---

## 7. How many, and the budget

`.ai/stage-secrets.md` planned 32 secrets over nine chapters (two built). This doc keeps those, adds
the Fields and the Broken Circle (written after that plan), changes four, and adds the rest: **about
75 in all**, 9 of them spell vaults, plus the castle's rooms. Per run the budget does not grow: two
chests and 40 Arcane Energy from all secrets together. More secrets means more to choose between in
fifteen minutes, not more loot.

---

## 8. The secrets, chapter by chapter

Each chapter lists its side event's verb (a secret may share a primitive with the event, never its
verb). **Status**: *built*, *planned* (in `.ai/stage-secrets.md` section 6, unchanged unless said),
*changed*, or *new*. Sizes as there: **S** reuses what exists, **M** needs one primitive or a set of
art, **L** needs a system. Where and how far follow `.ai/stage-secrets.md` section 4's bands (near
2-4 chunks, mid 5-9, far 10+); a vault is 5-7 (6.4). Tells are the warm breathing glint plus the
far tell named. Every new art piece is generated (`tools/art/secret_<chapter>.py`), never painted.

### 8.1 Enchanted Forest (`stage_0`) - event: hold the grove (stand still)

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| F1 | The Fenced Bloom | built | every flower pen | circle the pen | the flower turns to watch him | heal 50%, sanctuary, a seed |
| F2 | The Fairy Ring | planned | mid | touch the centre | a perfect ring of mushrooms | a root trap, re-arms |
| F3 | The Outward Path | planned | mid | sequence, outward | five waystones in a line | a chest |
| F4 | The First Tree | built, keepsake | 10-12 chunks | carry the seed | the seed's lean; stumps thicken | full heal; Vitality, once |
| F5 | **The Pale Hart** | **new, vault** | 5-7 chunks | **follow**: a hart of warm light stands at a clearing's edge; it bounds 400 px away when he comes within 250 and waits facing him, six times, along a hashed path, then stands under an old hunter's high seat where a bow of light hangs. Touch the seat. Wander 1500 px off and the hart goes back to its start | far: hoofprints in the moss and bracken bent one way, from two chunks out; near: the hart itself, the only warm thing that moves and is not his | **Hunter's Draw**; later runs a chest |
| F6 | **The Struck Oak** | **new** | mid | **strike**: about 500 landed near a lightning-split oak; it falls open | scorch on the ground round it; an ember breathing in the split | a chest |
| F7 | **The Woodcutter's Clearing** | **new** | mid | **reach**: a clearing ringed by trunks with one gap, found by walking the ring (trunks block, `.ai/terrain.md` 10) | the ring itself: nothing else in the scatter is a perfect ring of trees | a flask and 20 Arcane Energy in a hollow log |

### 8.2 The Fallow Fields (`stage_9`) - event: the well (defend)

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| FF1 | **The Pinfold** | **new, vault** | 5-7 chunks | **guarded**: a walled stone pound (the Hayward's pinfold, `campaign.md` 5) with a hound of warm light lying behind a barred gate. Arriving wakes two elites from the Fields' own table (the Tithe-Reeve's men); when both are dead, touch the latch and the hound runs to him | far: broken hurdles and a trail of pawprints toward it, two chunks out; near: the hound's light through the bars | **the Familiar**; later runs a chest |
| FF2 | **The Last Sheaf** | **new** | near | **linger** 3 s: the one sheaf still standing in a stubble field (the last sheaf, left for luck) | the only upright sheaf in a cut field | heal 50% and 60 s of doubled pickup radius; drops the sheaf token for FF4 |
| FF3 | **The Plough in the Furrow** | **new** | mid | **follow** a single straight furrow (a trail) to where the plough was left in it | a furrow that runs dead straight across every other | a chest |
| FF4 | **The Tithe Barn** | **new, keepsake** | 10-12 chunks | **carry** the last sheaf into the empty tithe barn: his tithe, given back | the barn's doors stand open on nothing; chaff drifts toward it from three chunks | full heal; **150 Arcane Energy**, once |
| FF5 | **The Scarecrow's Post** | **new** | mid, once a run | **risk**: touch the empty cross where the scarecrow stood and a Haymaker pair walks out of the stubble | the empty cross | a chest |

### 8.3 The Broken Circle (`stage_10`) - event: relight the moot (follow the runes)

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| BC1 | **The Fallen Star** | **new, vault** | 5-7 chunks | **gather** seven warm shards strewn along a scorched trail across the heather, in any order, then **linger** 2 s in the crater they lead to | the trail itself: burnt heather in a straight line, glinting at intervals, pointing at the crater | **Starfall**; later runs a chest |
| BC2 | **The Hare's Form** | **new** | near | **linger** 2 s in a hare-shaped hollow pressed into the heather | the form, and Black Hares scattering from it as he comes | 60 s of +15% move speed; page |
| BC3 | **The Shepherd's Bothy** | **new** | mid | **linger** 2 s at the cold hearth of a roofless hut | a thin warm thread of smoke | heal 50%; the hearth heals within 160 px for the run |
| BC4 | **The Speaker's Stone** | **new, keepsake** | 10-12 chunks | **sequence**: nine cup-marks round the moot's old speaking-stone, touched smallest to largest | lichen thinning toward it; the cups catch the light | full heal; **150 Arcane Energy**, once |

### 8.4 Sunken Cave (`stage_2`) - no event

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| C1 | Cold Lamps | planned | anchor, near, mid | linger 2 s | an unlit lamp | heal, light circle; three lit, a chest |
| C2 | **The Geode Heart** | **changed, vault** (was the Crystal Vein) | 5-7 chunks | **strike** (section 8.4.1) | crystals grow commoner and crack as he nears | **Frost Shard**; later runs a chest |
| C3 | The Choir Loft | planned, keepsake | 10-12 | untouched for 5 s in the loft | three empty seats | full heal; Magnet, once |
| C4 | **The Overlook** | **new** | mid | **reach**: a ledge above a cave lake at the end of one rock causeway | the causeway is the only dry line across the water | a chest |
| C5 | **The Blind Fish** | **new** | near | **follow** a pale fish of warm light along an underground stream to its pool | the only light in the water | a flask and 20 Arcane Energy |

#### 8.4.1 The Geode Heart (Eric's crystal)

- **Sees:** from two chunks out the small crystal clusters (the cave's own `crystal-blue` and
  `crystal-violet`) grow commoner, and from one chunk out more of them carry hairline cracks. Then a
  crystal three men tall (about 200 px, a `Breakable` landmark on layer 4, so it stops feet and
  bodies and the flow field routes round it) grown up through a cavern floor, with the warm glint
  deep inside it: the light he missed, locked in stone.
- **Does:** **strike**. Every point of damage landed on an enemy within 140 px of its foot counts.
  2,000 breaks it, in four drawn stages (hairline, cracked, splitting, shattered). It is never a
  target: the player stands at its foot and fights, and the swarm brings the damage to him.
- **Pays:** it shatters outward in a ring that freezes every enemy within 300 px for 2 s (Frost
  Shard's burst at a crystal's size, through `ApplySlow(0)`, which respects a boss's floor - and
  the boss has stood down by then anyway), and the warm light rises out of the shards to him:
  **Frost Shard**, granted and saved at once. Later runs: the same burst and a chest.
- **Why strike and not a target:** a target would pull every auto-aimed spell off the swarm, which
  is the rule the thaw was built around. Breaking by fighting at its foot also gives the vault a
  place to fight rather than a chore: the cavern pocket it stands in is the best ground in the run
  for a minute.
- **Art:** the crystal in four stages and its shards (`tools/art/secret_cave.py`), a crack-glow on
  the small clusters (a modulated overlay of the existing sprites). **Sound:** the shatter is the
  game's loudest secret sound and plays once; per `.ai/audio-direction.md`'s loudness table, a sound
  that rare may sit above a hit.

### 8.5 Cursed Dungeon (`stage_1`) - event: the Gaoler's key (carry)

**In the castle now** (`.ai/endgame-castle.md` 1.3 and section 4): chapter 11 of 13, between the Last
Bridge and the Court, unchanged but for its place. Its secrets move with it, its vault (the Cold
Forge) included, and gain the Oubliette (D7), moved from the castle doc's first-draft Undercroft. The
Gaoler's Ledger now arrives late, a chapter before the Court's Library reveals every secret's hint;
it keeps its design.

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| D1 | Chalk Marks | planned | mid | follow | a chalk arrow at a junction | a stash: a chest |
| D2 | The Alarm Bell | planned | near, mid | risk | a bell on a chain-post | calls the Warden now; a chest |
| D3 | The Hollow Flagstone | planned | mid | strike | tally marks round a cracked flag | flask and 20 Arcane Energy |
| D4 | The Gaoler's Ledger | planned, keepsake | 10-12 | guarded, then touch | a lectern in an empty hall | a chest; the ledger, once (4.3) |
| D5 | **The Cold Forge** | **new, vault** | 5-7 chunks, in a hall room | **cull**: kill 40 enemies standing on the forge's hearth ring (a 180 px circle in front of the anvil); each kill sets an ember in the dead coals until the forge roars | far: the walls' sconces give way to soot-stained faces and dropped shackles from two chunks out; near: the anvil, with a warm glint on the horn | **Molten Shard**; later runs a chest |
| D6 | **The Empty Cell** | **new** | mid | **untouched**: stand in the one cell whose door stands open for 4 s without being struck | a cell door open in a corridor of shut ones; the name scratched off the lintel | heal 50%; page. Absence, never a body |
| D7 | **The Oubliette** | **new** | mid, in a dead-end hall room | **untouched** for 5 s on a grate in the floor | a draught of cold motes rising through the grate; a voice below | 150 Arcane Energy, once; when Merlin is built, his card turns from a sealed blank to a silhouette here (`.ai/endgame-castle.md`, decision 11) |

### 8.6 Blighted Swamp (`stage_3`) - event: snuff the lanterns (explore)

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| S1 | Lily Crossing | planned (L) | mid | reach over the pads | a flower on a pad | heal, a chest; the pads sink |
| S2 | The Living Lantern | planned | mid | carry to S4 | one lantern burns warm | see S4 |
| S3 | The Warm Wisp | planned | near, mid | follow | a gold light in the reeds | flask and 20 Arcane Energy |
| S4 | The Sunken Chapel | planned, keepsake | 10-12 | receives S2 | an empty lamp hook | a chest; 150 Arcane Energy, once |
| S5 | **The Swallow-Hole** | **new, vault** | 5-7 chunks | **lure**: a sinkhole of black mud about 160 px across. Any enemy whose feet enter it sinks and is gone (counted, no XP); thirty sunk and the hole gives up what it took. It is not blocking ground - enemies must be able to walk in, and they mask the same layer his feet do - so it is a hazard to him instead: wading in slows him to a crawl and costs 1 HP a second. The flow field runs straight through it, so a horde chasing a wizard who stands at the rim walks into it; standing at the edge and letting them come is the whole ask | far: reeds lean toward it, the ground goes from bog to black mud over two chunks; near: bubbles, and the glint at the bottom | **Gravewell**; later runs a chest |
| S6 | **The Peat Bank** | **new** | mid | **strike**: about 400 landed near a cut peat bank; a bog-oak chest comes loose | a straight cut face in the bog, the only straight thing there | a chest |

### 8.7 Mystic Ruins (`stage_4`) - no event

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| R1 | **The Broken Ward** | **changed, vault** | 5-7 chunks | **sequence**: five ward stones in a ring, each cut with one to five notches; touch them in notch order (a wrong stone dims them, start again). The ward re-forms for 30 s and pushes enemies out at its edge (as planned), then its light leaves the ring and comes to him | far: rubble gives way to fallen ward-stone fragments from two chunks out; near: the five stones, the notches lit | **Kindled Ward**; later runs the 30 s ward and a chest |
| R2 | The Misfiled Page | planned | near | follow | a loose page | 25 Arcane Energy |
| R3 | The Sundial Vault | planned, keepsake | 10-12 | hour | a sundial whose shadow turns with the pace clock | a chest, full heal; Movespeed, once |
| R4 | The Statue's Eye | planned | mid | strike | a gem eye | a flask |
| R5 | **The Lecture Hall** | **new** | mid | **cull** 25 inside a ring of broken stone benches | the benches face an empty lectern | 60 s of +15% damage; page |

### 8.8 Frozen Waste (`stage_5`) - event: the thaw (fight near)

No *strike* here: it is the thaw's.

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| I1 | Footprints | planned | anchor | follow, then linger | a line of footprints from nowhere | heal 50%, a hearth for the run |
| I2 | **The Buried Runes** | **changed, vault** (was Settled Snow) | 5-7 chunks | **stillness**: inside a drift field (a chunk where the snow is always blowing), stand still for 2 s and the snow settles round him; three rune-stones show under it within 400 px; **touch** all three | far: the blowing snow, visible from two chunks; near: the settled snow's circle round his feet, and a cut rune catching his light | **Sleet Runes**; later runs a chest |
| I3 | The Watch-Fire | planned, keepsake | 10-12 | guarded, then linger | a cold brazier on a ridge | a chest; 150 Arcane Energy, once |
| I4 | **The Ice Window** | **new** | mid | **circle** a clear pane of lake ice with something warm frozen under it | the one clear patch in white ice | a flask and 25 Arcane Energy |

### 8.9 Scorched Sands (`stage_6`) - event: the caravan master (catch)

| id | secret | status | where | how it is found | tell | pays |
|---|---|---|---|---|---|---|
| A1 | The Buried Cart | planned | mid | strike | a wheel out of a dune | a chest |
| A2 | Milestones | planned | a road | sequence | numbered stones | Arcane Energy, flasks |
| A3 | The Hidden Water | planned, keepsake | 10-12 | linger | cracked earth to scrub to reeds | full heal, a well; Recovery, once |
| A4 | The Mirage | planned | - | stillness | a pool always ahead | 60 s of +20% speed |
| A5 | **The Fulgurite** | **new, vault** | 5-7 chunks | **storm**: a branching tree of glass stands in the dunes where lightning struck. A dry storm passes over it every 90 pace seconds (clouds gather for 10 s, then six telegraphed strikes land round it over 6 s - `TelegraphedGroundHit`, 3 damage each, which also hit enemies). Stand within 120 px of the glass through the strikes: it draws the last one into itself and rings | far: glass beads in the sand and a dark sky over one place, visible from two chunks; near: the glass tree with the glint in its root | **Stormsweep**; later runs a chest |

### 8.10 The Emberdeep (`stage_7`) - no event

The gate chapter. Whatever is decided in 2.5, a secret here pays nothing the gate counts.

| id | secret | status | how it is found | pays |
|---|---|---|---|---|
| E1 | The Last Ward | planned, anchor | touch | hazards do not hurt inside; +10% Armor there |
| E2 | The Sleeping Sentry | planned | guarded | a chest |
| E3 | The Marks | planned, keepsake | touch | heal; page only |
| E4 | **The Quench Trough** | **new** | **linger** 3 s in the steam over his forge's quench trough | heal 50% and 60 s in which fire hazards do not hurt |

### 8.11 The Sketchbook (`stage_8`)

Unchanged from `.ai/stage-secrets.md` 6.9 (the Dog's Find, Colour It In, the Kids' Page), under the
kids' art rules: nothing of theirs altered, nothing new under their name, page marks only.

### 8.12 The castle (`.ai/endgame-castle.md`)

Past the gate, so their rewards may be larger (still never XP, levels, tags, charges or Growth in a
run). Listed here as achievements; their places, rooms and art are in the castle doc.

| chapter | secrets |
|---|---|
| The Last Bridge | the Toll Stone (touch: 30 Arcane Energy), the Span Lantern (linger: the collapse waits 30 s), the Gargoyle's Perch (strike: a chest), **the Names on the Parapet** (keepsake, gather: 150 Arcane Energy) |
| Cursed Dungeon | moved into the castle with its chapter: D1 to D7 (8.5), its vault the Cold Forge. (The first draft's Undercroft and its secrets are not built; the Oubliette moved to the Dungeon as D7) |
| The Court | its rooms are its secrets: the Great Hall, the Chapel, the Library (reveals every secret's hint, 4.3), the Armoury, the Map Room, the Treasury, the Gallery, the Kennels, the Kitchens, the Minstrels' Gallery, the Solar, and **the Keepsake Doors** (one door for every chapter keepsake found) |
| The Throne | none in the fight (secrets stand down for a boss); its deeds are Feats |

---

## 9. Build plan

Pieces that agents can build in parallel without touching the same files. The castle doc has its
own plan; the two meet only at `SecretCatalog`'s castle files and the achievements derived from them.

| piece | what | files it owns | depends on |
|---|---|---|---|
| **A1 Unlock and achievement core** | 2.6 and section 3: the new `AchievementDefinition` fields, `Kind`/`Visibility`, derived secret and event deeds, `CompleteNow`, `Reconcile`, the moves of 2.2, the gate (2.5, as decided), schema 19, `LifetimeStats.EventsWon`, `GlimpsedSecretIds`, every validator in 2.6 and `ValidateAchievementMigration` | `UnlockCatalog.cs`, `AchievementDefinition.cs`, `AchievementManager.cs`, `GlobalStatsManager.cs`, `SaveData.cs`, `LifetimeStats.cs`, `DiscoveryRewards.cs`, `StageCatalog.cs` (the gate value only), `RegressionChecks.cs` (its own validators). The gate rule per gate chapter (2.5), so the castle's Throne can use it. **Also touched, section 11:** `SecretCatalog.cs` (additive), `WorldMap.cs`, `_UiShot.cs`, `_BalanceHarness.cs` | **built** (`wip/achievements-core`, section 11) |
| **A2 The achievements page** | section 4: `AchievementsPage.cs`, its two call sites, the run snapshot for the pause tab, the name (decision 4), the Spell Index's vault hint | `AchievementsPage.cs` (new), `CodexRows.cs`, `MainMenu.cs` (the Unlocks panel only), `Node2DGame.cs` (`ShowEscapeAchievements` and a `SnapshotRunResult` beside `BuildRunResult` only) | A1's fields (the doc fixes their names, so A2 can start against them) |
| **A3 The shop** | 2.4: the spell shelf out, the three boons folded into Powers (decision 5) | `MainMenu.Shop.cs`, the title menu's button in `MainMenu.cs` (coordinate with A2: different methods) | A1 |
| **A4 Secret core** | the vault class (6.4), `SecretMetaReward.Spell`, seen-tracking, `StrikeListener` and `Breakable` extracted from the thaw, `SecretMover`, the gather and lure primitives, and **`SecretCatalog` split into one partial file per chapter** with an empty block each (`SecretCatalog.Forest.cs` and so on, the `StageBestiary.<Chapter>.cs` pattern), so the chapter agents never edit a shared file | `SecretCatalog*.cs`, `SecretDirector.cs`, new primitive files, `ThawEvent.cs` (the extraction only) | **built** (`wip/secret-core`, section 12), with the Geode Heart as its proof |
| **A5-A13 One chapter's secrets each** | that chapter's rows of section 8: its vault first, then the rest; art in `tools/art/secret_<chapter>.py` | its own `SecretCatalog.<Chapter>.cs`, its own secret scripts and generator | A4 |
| **A14 Sounds** | the shimmer (seen) and found stings `.ai/stage-secrets.md` 7.7 asked for, and the crystal's shatter | `tools/audio/`, `SfxCatalog.cs` | none |

**Order:** A1, then A2/A3/A4 in parallel, then the chapter agents in parallel, vaults before
anything else in each (they carry the spells). The vaults can ship before the menu does: until A2
lands, a found vault still grants its spell and the old Unlocks list shows its derived deed.

**Each piece verifies:**

- `dotnet build WizardSurvivors.sln`, and say when only that ran.
- The validator run: `"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit-after 300 --
  --no-save-writes` - no new warnings; A1's migration samples pass.
- A1: a fresh in-memory save can reach the Emberdeep on paper (`ValidateGateReachability`).
- A2: `_UiShot` at `--resolution 540x1212 --portrait --safe-area` and `--resolution 1280x720`, on
  the main menu panel (each tab) and the pause tab (`--open-pause`), with `--found-secrets` and a
  new `--seen-secrets=id,id` to fill the states; a pad walk with `--pad=right,down,...`;
  `python tools/art/ui_contrast.py`.
- A4 and each chapter: a `_UiShot --secret=<id>` photograph of each secret beside the spawn
  (planned in stage-secrets 7.8) and the headless walk that completes it on a fixed seed
  (`--secret-probe`, extended per secret); for a vault, that the spell is owned in the in-memory
  save afterwards and joins the pool.
- Before the first vault ships: `_BalanceHarness --seek-apotheosis --all-unlocked --immortal` once
  (6.3, item 7).
- **What no harness checks:** whether a tell reads on a phone in a crowd, whether the hart is
  followable, whether 2,000 is the right crystal. Eyes on the phone.

---

## 10. Decisions (decided by Eric, 2026-10-30)

Every recommendation stands. The first three were confirmed directly.

| # | question | decided |
|---|---|---|
| 1 | the final gate (2.5) | **narrowed: the Long Coil beaten and every wizard freed** - every wizard whose rescue comes before the gate, written per gate chapter, since the Cursed Dungeon (and Tituba) moved past it. "Every spell recovered" is a Collection deed, *The Grimoire Restored*, 500 Arcane Energy |
| 2 | the shop (2.4) | **it stops selling spells**; Arcane Energy keeps the stat upgrades, the three boons and Redaction |
| 3 | Fireball (2.2) | **to the Long Coil's deed**, beside Sunsteel Filament; the Warden of the Deep's deed pays 300 Arcane Energy and opens the castle |
| 4 | the menu's name (4.1) | **Achievements** |
| 5 | the last three boons (2.4) | **folded into Powers**; the "Spells & Boons" button leaves the title menu |
| 6 | hidden achievements (4.3) | **revealed once seen**: name and an observation after the tell has been on screen |
| 7 | spell vault hints (4.3, 6.4) | **the Spell Index names each vault's chapter** ("Hidden somewhere in the Sunken Cave") |
| 8 | the pairings (2.1) | **as written**: Frost Shard to the Geode Heart, Hollow Star to Survivor, the nine vaults - the Cold Forge's now in the castle with its chapter |
| 9 | when a found spell joins the pool (6.2) | **at once**, from the next level-up of the same run |
| 10 | selling hints for Arcane Energy | **not now**; the Library and the Ledger reveal them as rewards |
| 11 | a new crystal spell | **no**; Frost Shard is the crystal's spell |

**Raised since, and answered in the castle doc** (`.ai/endgame-castle.md` 10.2): whether the Throne
also needs every wizard freed (recommended yes, so Tituba is counted where he is), and the schema 20
step that moves the Dungeon, which follows this doc's schema 19.

---

## 11. What A1 built (2026-10-30, `wip/achievements-core`)

The unlock and achievement core: sections 2.1-2.6 and 3. Nothing of section 4 (the page, A2), 2.4's
shop (A3) or the vaults in the world (A4 and the chapter agents) is built.

### 11.1 Where it lives

| piece | where |
|---|---|
| the new fields | `AchievementDefinition`: `Kind` (`AchievementKind`: Feat, Road, Secret, Collection), `StageId`, `Visibility` (`AchievementVisibility`: Always, ChapterOpen, Hidden), `CompletesInRun`, `SiteId`, `SecretId`, as 2.6 names them, plus **`SeenLine`** and **`SealedLine`** (4.3's words, so the page invents nothing) |
| deed ids | a side event's deed is `event_<site id>` (`AchievementDefinitions.EventDeedId`), a secret's `secret_<secret id>` (`SecretDeedId`), made from the id so neither can drift or collide with a hand-written deed. The Collection deed is `grimoire_restored` |
| derived event deeds | `AchievementDefinitions.SideEventDeeds`: one per site in `SideEventDirector.ForStage`, its reward read from `UnlockCatalog.ForDiscoverySite`, its words from `SideEventWords` (the one hand-written part; `ValidateAchievements` ties them together both ways) |
| derived secret deeds | `SecretCatalog.AsAchievement`, one per secret in `SecretCatalog.Defined`. Hidden, `CompletesInRun`, measured by `SaveData.FoundSecretIds` |
| the unlocks | `UnlockCatalog`: every spell is `Starter` or `Achievement`. A side event's unlock is `Achievement`-sourced from its event deed and keeps the site on **`UnlockDefinition.SiteId`**. `UnlockSource.Discovery` is still in the enum and nothing uses it; `ValidateUnlockCatalog` refuses it. `PurchasableSpells` is empty (the shop's shelf draws nothing until A3 removes it) |
| in-run completion | `AchievementManager.CompleteNow(saveManager, id)` (complete, grant, save, named entries) over `Complete(save, id)` (records the deed **and the fact it measures** - the event won, the secret found - so the two can never disagree; pays its Arcane Energy only the first time; grants what is not owned). `DiscoveryRewards.GrantSite` is a call to it now, with an in-memory overload the validators use |
| reconciling | `AchievementManager.Reconcile(save)`: every recorded deed's current spell, boon or wizard, if not owned. Never currency. Runs in the schema 19 step and at the end of every `ApplyRunAchievements` |
| the gate | `StageGate.PreviousBossAndWizards` (it replaced `CampaignComplete`) on the Emberdeep. `GlobalStatsManager.GateWizardIds(gate)` / `CountsTowardGate(wizard, gate)` / `IsGateMet(save, gate)` / `RemainingGateWizardCount`. `StageCatalog.GateChapter` (the first), `GateChapters` (all), `IsBossGated` |
| the save | `SaveData.CurrentSchemaVersion` 19, `MigrateToAchievementUnlocks`; `SaveData.GlimpsedSecretIds` (`HasGlimpsedSecret`, `RecordSecretGlimpsed`); `LifetimeStats.EventsWon` (`HasWonEvent`, `RecordEventWon`) |
| listing | `AchievementDefinitions.IsListed(deed, save)` and `ListedFor(save)` hold 4.2's rules; `IsSealed(deed, save)` is the sealed-row test; `ForStage(stageId)` groups a chapter |

### 11.2 Where it departs from this doc, and why

1. **The nine vaults are written in `SecretCatalog` now, as planned entries** (`IsBuilt = false`),
   with their deeds, words and spells - not left for A4. Every vault spell has to point at a deed
   that exists, or `ValidateUnlockCatalog` fails; and the doc's own wording ("a secret that pays a
   spell names it in `SecretDefinition.SpellUnlockId`", `SecretCatalog.AsAchievement`) puts that in
   the secret catalog. The edit is additive: `SecretDefinition` gained `SeenLine`, `IsVault`,
   `IsBuilt`, `SpellUnlockId` and `SpellName`; `SecretVerb` gained `Gather` and `Lure`;
   `SecretMetaReward` gained `Spell`; `SecretCatalog.Defined` is every secret written down and
   `SecretCatalog.All` only the built ones (what the Secrets list and the world map count, so no
   screen names a vault that is not in the world). **A4 moves each vault into its chapter's partial
   file; each chapter agent flips `IsBuilt` when its vault is placed.**
2. **Until a vault is built, its spell cannot be earned on a new save.** That is nine spells (the
   eight shop spells and Frost Shard). Every copy anyone bought stays; but A1 alone should not ship
   to players before the vaults do. The Spell Index already says where each is hidden (decision 7:
   the unlock's `LockedHint` is the deed's sealed line, "Hidden somewhere in the Sunken Cave").
3. **A secret's deed carries the vault's spell and nothing else.** A keepsake's found level or Arcane
   lump stays `SecretDirector.Found`'s to pay, on the first find, never the deed's - so a deed
   reconciled or completed twice can never pay it twice.
4. **`SecretDirector.Found` does not call `CompleteNow` yet** - it is A4's file. Until it does, a
   found secret's deed is recorded at the run's end by `ApplyRunAchievements` (its measure reads
   `FoundSecretIds`), and a vault's spell would be granted then, not at once. **Decision 9 ("at once")
   needs A4's one call**: `AchievementManager.CompleteNow(saveManager, AchievementDefinitions.SecretDeedId(id))`.
   The level-up pool already reads the save live (`Player.GetLevelUpOptions`), so nothing else is
   needed for the spell to be offered from the next level-up. A deed completed in the run that only
   reaches the run's end (as these do) is recorded silently there unless it grants something - its
   own beat already announced it.
5. **The Cursed Dungeon's event deed is "The Open Cell", not "Tituba Freed".** A rescued wizard's
   name is his reward and stays hidden while he is locked (`.ai/side-events.md`, "Secret wizards"),
   and the deed is listed from the day the Dungeon opens. Its reward line still says "Frees Tituba",
   as the Coursing's says "Frees Laelaps": **A2 should show a locked secret wizard's reward without
   his name** (the deed's `CharacterUnlockId` and `GlobalStatsManager.IsCharacterUnlocked` say when).
6. **The old Unlocks list hides some rows until A2.** `AchievementDefinitions.Listed` (no save) is
   `ListedFor(null)`, which reveals nothing secret: no Hidden deed, and no deed that frees a secret
   wizard - The Open Cell, Out of the Ice and **the Coursing**, which the old list did show (naming
   Laelaps on a fresh save). A2's page calls `ListedFor(save)` and draws `IsSealed` rows.
7. **The gate is written per chapter and counts Tituba today.** "Every wizard whose rescue comes
   before *this* chapter": the Cursed Dungeon is still fifth in `CampaignOrder`, so the Emberdeep
   counts all seven wizards now. When schema 20 moves the Dungeon past the gate she drops out of the
   Emberdeep's count by herself and into the Throne's, with no edit to the gate.
8. **Met gates are written into the opened list** (`GlobalStatsManager.RecordMetGates`, at every
   run's end and in the schema 19 step), so a wizard added before a gate later can never close it to
   a save that had it open - the cost schemas 10, 12, 14, 15 and 16 each paid for a new spell or
   wizard. A gate met at a run's end is shown on the results screen as a new chapter.
9. **The old migration steps read frozen lists.** Steps 8, 10, 12, 14, 15 and 16 read
   `UnlockCatalog.AllSpellIds`, `AllWizardIds` and `Source == Discovery` live. With the discoveries
   turned into deeds, step 8 would have handed Tituba and Vainamoinen to every pre-campaign save, and
   step 10 would have stopped crediting the discoveries; and every future spell would have changed
   what they did. They read the schema-18 lists frozen in `SaveData` now, so a new spell or wizard
   needs no migration at all.
10. **Step 2 also records done, quietly, every event and secret deed whose fact the save holds**, so
    the first run after the update does not announce as new an event won long ago. A save that owns
    Wishing Coin or Gilded Mote from schema 9's payback has those events' deeds done: it owns what
    they pay.
11. **Kinds of the existing deeds**: the Coursing is a Road deed of the Broken Circle (it names the
    chapter and frees a wizard secret until it opens); Relic Hunter, Attuned and the Grimoire are
    Collection; the rest of the hand-written non-boss deeds are Feats. John Dee's Mortlake deed is a
    Feat with no chapter, so every gate counts him.
12. **The reveal rule is generalised**: a wizard a gate counts must be revealed by a chapter before
    that gate (`ValidateSecret`), as `.ai/endgame-castle.md` 1.6 asks.
13. **Outside the file list, forced by the gate**: `WorldMap` (the finale card read the old enum, and
    its "Still held" line counted spells; it counts the gate's wizards now), `_UiShot --campaign-complete`
    (it must beat the gate's boss as well), and `_BalanceHarness` (a "new spells offered" report line,
    to show a `--fresh-save` pool is the starters).

### 11.3 What the element check found

The second pass of `ValidateElementReachability` (6.3, item 5: starters, the shop's boons, Feats and
chapter deeds before the gate, no secrets) passes for all twelve, as measured on the branch:

| element | on the road (spells + boons) | everything |
|---|---|---|
| Light | 7 + 4 = 11 | 13 + 4 |
| **Wind** | **5 + 1 = 6** | 9 + 1 |
| Ice | 5 + 3 = 8 | 11 + 3 |
| every other | 10 or more | |

**Light is comfortable; Wind is the one at the floor.** Two of its spells are vaults (Sleet Runes,
Hunter's Draw), which leaves Cyclone Slash, Gale Blade and Shadegrease. Any change that takes one more
Wind carrier off the road trips the check.

### 11.4 Verified, and not

- `dotnet build`; the validator run (0 errors, the one standing test_wizard warning); every check
  above was shown to fire by breaking what it guards (a reward line, a wizard's chapter past the
  gate, the per-chapter rule, `Reconcile`, schema 8's frozen list).
- `ValidateGateReachability` walks a new save in memory through every chapter before the gate - each
  boss beaten as a run's end records it, each side event won, each built secret found, each Feat done
  - and it enters the Emberdeep; without the Long Coil, or without any one wizard the gate counts, it
  does not.
- `ValidateAchievementMigration`: the doc's seven samples, a Long Coil-only save, a pre-campaign save
  and a schema-9 save that had everything, each migrated twice.
- **Not verified by play**: nothing here was seen on a screen - the old Unlocks list with the new
  rows, the world map's finale card, a won event's line. A2's photographs cover the page.

---

## 12. What A4 built (2026-10-30, `wip/secret-core`)

The secret core of section 9's A4 row, and one vault end to end as its proof: the Sunken Cave's Geode
Heart (8.4.1), Eric's crystal, which pays Frost Shard. **`.ai/stage-secrets.md` section 13 is the
record of each piece and the recipe a chapter agent follows**; this section is what changed against
this doc.

### 12.1 What it is

- **`SecretCatalog` is one partial a chapter** (`SecretCatalog.<Chapter>.cs`, the `StageBestiary`
  names - `SecretCatalog.EnchantedForest.cs`, `.SunkenCave.cs` and so on, eleven, one for each chapter
  in `StageCatalog`; a castle chapter adds its file and one line to the list in `SecretCatalog.cs`). A1's
  vault entries moved into their chapters' files, unchanged but for the Geode Heart's, which is built.
  `SecretDefinition` is a record now, so a chapter builds A1's entry with `Vault(...) with { IsBuilt =
  true, CreateSite = ... }`.
- **Placement by kind** (`SecretPlacement`: `Anchor`, `Keepsake`, `Vault`, `Custom`), done by
  `SecretDirector` for every built secret of the chapter: 6.4's vault band and bearing rule (five to
  seven chunks, at least 90 degrees from the side event's and the keepsake's bearings), on its own
  placer stream (8), its footprint reserved (`SecretCatalog.VaultFootprint`, 420 px).
- **The seen mark** (4.3) is the director's: within 450 px of a site's near tell, once, recorded on
  `GlimpsedSecretIds` and saved with the next save; `secret_seen` at the site the first time per save.
- **`SecretDirector.Found` calls `AchievementManager.CompleteNow`** (11.2, item 4): a vault's spell is
  granted and saved at once and offered from the next level-up of the same run (decision 9). The
  results screen lists it with the run's discoveries. A vault whose spell was bought says "you already
  had it" (4.5). `secret_found` plays for a find again, `meta_unlock` for the first.
- **`SecretVault`** (6.4's class): `Open` pays the deed the first time ever and a chest from the run's
  secret budget (two) in a later run.
- **The far tell is built** (stage-secrets section 2, which section 12 there said was not):
  `SecretFarTell`, furnished into each chunk as it streams in through one new call in
  `Node2DGame.FurnishChunk`.
- **Solid ground**: a site's footprint is stamped into the navigation flags as a fence is, through one
  call in `Node2DGame.PlanChunkFeatures`, so the flow field routes round the crystal (8.4.1).
- **The primitives**: `StrikeListener` and `Breakable` (extracted from the thaw, which counts and
  draws its cracks through them exactly as before), `SecretMover` (follow), `SecretGather`,
  `SecretLure`.

### 12.2 Where it departs from this doc, and why

1. **Three hooks in `Node2DGame`**, outside section 9's file list: `Secrets` (the director, assigned
   before it is added, because placing it makes chunk data, which asks it for solid cells),
   `Secrets?.FurnishChunk` beside the curated props, `Secrets?.StampChunk` at the top of
   `PlanChunkFeatures`, and `IsDecorSpotOpen` (the tests a scattered sprite passes, for the far tell).
   They are why no chapter has to touch the furnishing.
2. **The far tell's crystals have no collision.** They are a direction to walk in; with collision, a
   gradient thickening toward the vault would clutter the very pocket the vault asks him to fight in.
3. **The seen mark is not taken while the secrets stand down** (the boss), and a site with many
   instances (every flower pen) is seen once for its id.
4. **The Geode Heart's first crack shows on the first hit that counts**, not at a quarter: four drawn
   stages over 2,000 with the hairline at the first point says at once that fighting here does
   something - the only feedback it gives besides a one-pixel jolt per counted hit. Nothing on screen
   says how far along it is.
5. **Its frost ring is drawn** (a one-shot ring of its own light ending on the freeze radius, with its
   shards flying to it) - an animation, which `.ai/art-direction.md` "The player's spells carry no area
   markers" allows, not a marker.
6. **`_UiShot` is touched** (the tool, not the game): `--secret=<id>` (stage-secrets 7.8),
   `--secret-stage=N[@F]`, `--goto=secret`, and `--secret-probe` runs `VaultProbe` on a chapter with a
   built vault.
7. **Not built:** the per-chunk scatter of stage-secrets 7.2 (common secrets at a probability per
   super-cell). The first chapter that needs one adds it in `SecretDirector.FurnishChunk` - the one
   shared edit still to come. And `SecretSeed` is still the Forest's carry, not extracted.

### 12.3 Verified, and not

- `dotnet build`; the validator run (0 errors, the one standing test_wizard warning) with the Geode
  Heart built and the new `ValidateSecretCatalog` rules in.
- **The thaw behaves as before**: `_ExploreProbe --stage=5` (seed 4242), 22 of 22 before the
  extraction and after it, the event finished in 15.3 s and 15.4 s of game time after arriving.
- **The Forest's two secrets still work**: `_UiShot --secret-probe --stage=0` all pass before and after
  (the lap opens the bloom and heals half, the seed drops and is carried, the First Tree is placed ten
  to twelve chunks out on stream 7, planted, found, and pays its found level of Vitality).
- **The Geode Heart, walked and broken on a fixed seed** (`--secret-probe --stage=2 --terrain-seed=4242`):
  on a fresh save, 23 of 23 - placed 5.2 chunks out, never a target, its foot solid in the grid, unseen
  on the way in and seen at its foot, the far tell 109 crystals within a chunk (43 of them cracked)
  against 69 from one to two chunks out (none cracked), a hit beyond its reach counting nothing, the
  four stages at 54, 702, 1,350 and 2,000 landed, both witnesses in the ring frozen, then found, its deed
  recorded, Frost Shard owned, in the level-up pool and not in the loadout, and no chest. With
  `--all-unlocked` (a later run), 22 of 22: the same, and a chest from the budget instead.
- **Photographed** beside the spawn (`--secret=geode_heart`) on both canvases: every stage, the shatter
  in flight with the crowd frozen in its ring, the first find's toast and a later one's, and the far
  tell zoomed out.
- `_BalanceHarness --seek-apotheosis --all-unlocked --immortal --stage=2` (6.3, item 7): see the branch's
  report for the minute the Apotheosis was offered; with every spell owned nothing in the pool can move,
  and the immortal bot holds its ground, so it never walks to the vault.
- **Not verified by play**: whether the crystal reads in a crowd on the phone in the real light, whether
  anyone follows the cracked clusters to it, whether 2,000 is the right amount (`.ai/achievements-and-
  secrets.md` 9, "What no harness checks"), and whether `secret_seen` is audible at 450 px. The
  sounds were wired, not listened to.
