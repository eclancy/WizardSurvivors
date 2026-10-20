# Passives, boons and items — the loadout economy shift

> **Status: all five phases have landed.** The reachability audit, the boon system, the removal of
> all eleven passive spells (save schema 9), chest item rarities, and Full Set Enchantment tags.
> `gameplay-design.md` describes what shipped; the rest of this document is the plan it was built
> from, kept for the reasoning.
>
> **One thing came back.** Aegis Ward returned as an *active spell*: it raises a shield that absorbs
> damage and recharges on its cooldown, and it costs a spell slot like anything else. A shield is a
> thing that forms, breaks and returns - it was never a number sitting on the player, which is what
> the passive roster was made of.

Three changes, planned together because they share one budget: **element tags**.

1. **Passive spells go.** The eleven `PassiveSpellEffect` spells leave the game.
2. **Boons replace some of them** — permanent, non-levelling pickups that still carry tags.
3. **Items gain rarities, and a completed Full Set Enchantment grants tags.**

`gameplay-design.md` describes the economy as it is. This is the plan to change it.

---

## 1. What "no passive spells" actually removes

Eleven spells, and they are load-bearing in four separate systems.

| System | What leaves with them |
|---|---|
| **The spell roster** | 11 of 32 spells — Aegis Ward, Thornmail Barrier, Frozen Bulwark, Stormguard Aura, Venom Cloak, Guardian Vines, Tidal Barrier, Stone Bulwark, Blur, Fortune's Favor, Haste |
| **The unlock economy** | **3 of 8 starters** (Aegis Ward, Stone Bulwark, Blur), 1 achievement reward (Frozen Bulwark, from Untouchable), and **7 of 9 shop purchases** |
| **The damage pipeline** | The dodge roll, every reactive on-damage effect, and a slice of Armor |
| **Element weight** | Roughly a third of every tag in the game |

The unlock economy hole is the loudest: the shop drops from nine spells to **two**, and the starting
pool from eight candidates to five. Boons have to refill those slots, which is fine — they become
the new unlockables — but it means boons are not a side system. They are a third of the economy.

The damage pipeline is less alarming than it looks. Defence does not vanish, it **moves to the
element tiers**, which already carry it: Metal is Armor, Darkness is evasion, Earth is maximum
health, Grass is regeneration, Light is lifesteal. A player who wants to survive now stacks an
element instead of slotting a shield. That is arguably a better game — but it means element tiers
stop being a bonus and start being the defensive layer, which raises the stakes on the next section
considerably.

(The Armor unification and the Darkness move to evasion have **already landed** — see
`gameplay-design.md`. Flat damage reduction is gone from the game.)

---

## 2. The constraint that shapes everything: five elements die

Measured, not estimated. Maximum reachable weight in a six-slot loadout, before and after:

| Element | carriers now → after | max now → after | capstone |
|---|---|---|---|
| Fire | 5 → 5 | 11 → 11 | fine |
| Arcane | 7 → 5 | 13 → 11 | fine |
| Earth | 6 → 4 | 13 → 9 | fine |
| Poison | 5 → 4 | 10 → 8 | fine |
| Ice | 4 → 3 | 9 → 7 | fine |
| Wind | 6 → 3 | 13 → 7 | fine |
| Lightning | 5 → 3 | 11 → 7 | fine |
| **Darkness** | 3 → 2 | 6 → **4** | **dead** |
| **Light** | 4 → 2 | 8 → **4** | **dead** |
| **Grass** | 4 → 2 | 8 → **4** | **dead** |
| **Metal** | 6 → 2 | 12 → **4** | **dead** |
| **Water** | 3 → 1 | 6 → **2** | **dead** |

Metal collapses from twelve to four. Water goes to two — one carrier in the entire game.

**So the tag budget does not get to shrink; it gets to move.** Boons and Full Set Enchantments must
carry what the passives were carrying, and they must be weighted deliberately toward **Metal, Water,
Light, Grass and Darkness** rather than sprinkled evenly. This is the single hardest constraint in
this document, and it is easy to miss because nothing breaks loudly — an element with no carriers
just quietly never reaches a tier, and the passive that used to get it there is gone.

`RegressionChecks` should assert a floor: **every element must be able to reach 6.** That check
would have caught this before it shipped, and it is the same shape as the unlock-catalog audit that
caught the dead economy.

---

## 3. Boons — permanent, non-levelling

The replacement for the defensive half of the passive roster.

**What they are.** A boon is taken once and lasts the run. It has no levels, no upgrade path, and
no "it gets better later" — so **every boon must be worth taking at face value, the moment it is
offered.** That is the whole design constraint, and it is a harder writing problem than a levelled
spell: a spell can be weak at level 1 because level 8 justifies it, and a boon cannot.

**What they are not.** They do not occupy spell slots. The six slots are for spells; that is what
makes a slot a decision. A boon that competed for a slot would just be a passive spell again.

**Where they come from.** Offered in the level-up roll as a visibly different card — the frame work
in `tools/art/levelup_frames.py` already supports a second card texture, so this is a variant of
existing art rather than new art. *(Superseded: the boon card is its own object now, a
reliquary plaque rather than a page - `level-up-screen.md` section 2a - and boons are rare, section
3b below.)* Picking one does not consume a slot, which means a level-up offer
mixing spells and boons is a genuine choice between "more damage" and "more of everything else".

**A boon always looks magical - it is an object, but not an item.** Boon icons are drawn on the
material rows so they never read as spells, and that made them read as relics instead: Iron Rivets
was a plate of steel, the same kind of thing a chest pays out. A relic is a thing you found; a boon
was given, and still has the giving in it. So wherever a boon's picture is shown it is in its own
light, in its **first** element tag: the icon generator bakes a glowing inlay, an element fringe and
a glint into the icon (`tools/art/boon_icons.py`, "the enchantment"), and `scripts/BoonAura.cs`
draws it over a steady backlight with a breathing halo and a turning rune ring - on the level-up
card, and in the pause screen's **Boons** section, which is the only place an owned boon is listed.
The details, and why the reliquary window went from pale glass to night glass, are in
`level-up-screen.md` section 2b. **A new boon needs an `ELEMENT` entry in `boon_icons.py`** (and
an `INLAYS` entry if its object is all material), or its icon fails to generate.

**Boons are capped at six** — decided, and it mirrors the six spell slots. A run is therefore about
two loadouts of six, and the cap is what makes the sixth boon a choice rather than an accumulation.
It is also what keeps element tags honest: uncapped boons carrying tags would make every capstone
reachable at once. The reasoning behind the alternatives is kept below because the cap is a number
someone will want to revisit.

The alternatives are kept here because a cap is a number someone will want to revisit:

- **Cap the count** at six, mirroring the six spell slots. **This is the chosen answer.** Simple,
  and it makes boons a second loadout with its own decisions.
- **Tag only some boons.** The tag becomes the boon's cost — a tagged boon is weaker than an
  untagged one of the same rarity.
- **Half-weight tags.** Boons contribute at half a tag each, so it takes two to move an element.
  Fractional element counts complicate every readout in the game; I would avoid this.

The cap won because it is the only one of the three that also answers "how many boons is a run
about", which is the question that makes them feel like a build rather than a pile.

### 3a. Reactive boons, and the audit that produced them

Once the roster had shipped and Aegis Ward had come back as a shield spell, the tag economy was
measured rather than estimated. Carriers per element, counting spell tags, boon tags, Rare/Relic
item tags and completed Full Set Enchantment tags:

| element | spells | boons | items | sets |
|---|---|---|---|---|
| Water | **1** | 4 | 1 | 2 |
| Grass | 2 | 2 | **0** | 1 |
| Darkness | 2 | 2 | 2 | 2 |
| Metal | 3 | 4 | 1 | 4 |
| Light | 3 | 4 | 1 | 3 |
| Fire | 5 | 1 | 1 | 1 |

Two things fell out of it that the reachability check could not see.

**The roster was weighted for a crisis that had passed.** Aegis Ward carries Metal and Light, so
its return took both elements from two spell carriers to three. The boon roster had been written
when they had two, and Metal ended up the best-supported element in the game - three spells, four
boons, four sets - while still holding three of the thirteen boons.

**Unifying damage reduction had collapsed three boons into one.** Iron Rivets, Sunsteel Filament
and Saltbound Chain were distinct when armour, flat reduction and Darkness reduction were three
different mechanisms. With one Armor stat they became three copies of +8%, and with five of
thirteen boons on two effects against a cap of six, drawing the same card twice was likely.

So Saltbound Chain was repointed rather than deleted - it keeps its Water and Metal tags, because
Water cannot afford to lose a carrier - and it became the first of two **reactive boons**.

**Reactive boons are the real gap the passive removal left.** Every other boon is a number.
Thornmail retaliated, Frozen Bulwark froze the attacker, Stormguard arced, Venom Cloak and Guardian
Vines held auras; all five went at once, and nothing replaced the *behaviour*. Without it a
defensive build is a bigger health bar, which is the one thing a defensive build should not be.

- **Saltbound Chain** (Water, Metal) - damages everything nearby when the player is struck.
- **Rimebriar** (Ice, Grass) - slows everything nearby when the player is struck. Tagged Grass
  rather than Water on purpose: Grass is the only element with no chest item carrying its tag.

Three rules hold both of them together, and they are the interesting part:

1. **They fire on being struck, not on being hurt.** The trigger sits before the armour and shield
   steps in `Player.TakeDamage`. Put it after, and the better the defence the less the defence
   reacts - which is backwards for an effect sold as part of a defensive build.
2. **One shared cooldown, one shared radius.** Contact damage ticks several times a second, so an
   unthrottled reaction is a permanent damage aura - exactly the always-on passive these replace.
   Two separate clocks would only mean a player holding both got twice the pulses.
3. **The ring is drawn at the radius that was hit.** Same rule as `GroundSlamAttack`: a tell that
   lies about its reach is worse than no tell. A slow with no visual reads as nothing happening.

**What was deliberately not done.** Water was not given a fifth boon. Its floor held arithmetically
but four of its seven tag sources were boons, and boons share six slots across twelve elements - so
"reachable" meant "reachable if you spend most of your boon slots on one element". That is a content
gap, not a tuning one, and it was answered with a spell (see Riptide in `gameplay-design.md`) rather
than with another boon that would have hidden it.

### 3b. How rare a boon is

**About one level-up in four or five shows a boon, and never more than one.** It used to be most
of them.

Boons used to be ordinary candidates in the new-spell group of the level-up draw, at a per-card
weight of 1.2 against a new spell's 1.0. That was set when boons were few and the question was
whether a roll would ever offer one; with fourteen boons against twenty-odd unlocked spells it put
a boon in front of the player nearly every other screen, often two at once. A weight cannot say
"rare", because what a weight means moves with how many boons and spells are unlocked.

Now it is a **chance**: `Player.BoonOfferChance` (0.22) is rolled once per level-up in
`BeginLevelUpOffer`, beside the upgrade roll and held through rerolls and bans for the same
reason - a reroll re-deals *which* boon, never *whether* there is one. When it comes up, one boon
is dealt over a random one of the three spell cards. Measured with the balance bot (Pyromancer,
10 minutes, `--all-unlocked --immortal --sample-offers=200`, which deals 200 extra hands from the
player's state at every level-up):

| | hands with a boon | boon cards per hand |
|---|---|---|
| before (weight 1.2) | 52.1% - one in 1.9 | 0.81 |
| after (chance 0.22) | 21.3% - one in 4.7 | 0.21 |

Three exceptions keep it from closing anything off:

- **A dead pick is never allowed.** With a full book, every spell at its card cap and nothing
  else to take, a boon is dealt regardless of the roll - the same rule that lets upgrades into a
  pool that would otherwise be empty. (The pool used to be "all swaps" there; swaps are gone, and a
  hand with no boon left either is the Marginalia card - section 4b.)
- **The augury can name any boon.** It always could browse boons; now that they are rare it is
  the deliberate way to get a specific one, and it is the tool a player chasing a thin element
  should spend.
- **The boon that IS dealt leans toward the player's deepest element.** A boon carrying the
  element the player holds most of is `BoonOwnElementWeight` (3) times as likely to be the one.
  This is section 3a's warning made concrete: Water, Grass, Light, Metal and Darkness reach their
  tiers partly through boons, and one boon in five level-ups drawn evenly from fourteen would show
  a Water build about one Water boon a run. Rarity is unchanged by this - it decides which boon a
  rare offer is, not how often there is one.

**What was checked, and what could not be.** Measured with `--seek-apotheosis --all-unlocked --immortal
--minutes=30`, one run at a time, seed 1234. The bot now names a boon that carries its element
with an augury once its spell slots are full - a skilled player would, and that is the case the
measurement has to model:

| | Grass tier 6 | Apotheosis | hands with a boon |
|---|---|---|---|
| Vainamoinen, before | 5.3 min | 22.7 min | 46% |
| Vainamoinen, after | 2.6 min | 17.1 min | 22% |

Single runs, so read the direction rather than the minutes: Grass still reaches its capstone and
the Apotheosis still arrives. **Water could not be measured.** Tituba's bot run stalls at player
level 2 on `main` before any boon is in play - Riptide levels to 2 and the run stops gaining XP,
seed 1234 and seed 7 alike - so her path is unverified by the bot. On paper it is the one at risk:
Water has three spell carriers, two evolution grants, one chest item and four boons, so a Water
build now leans on the deepest-element weighting and the augury for its boons. Re-run
`--character=5 --seek-apotheosis` once that stall is fixed, before calling Water safe.

---

## 4. Item rarities

Twenty-five chest items exist today, all equal, all run-scoped, offered by a flat roll.

**Rarity does three jobs at once**, and they should move together:

| Rarity | Offer weight | Magnitude | Tags |
|---|---|---|---|
| Common | high | small | none |
| Uncommon | medium | moderate | none |
| Rare | low | large | one |
| Relic | rare, and gated behind a set or a discovery | build-defining | one or two |

Tying tags to rarity is what keeps section 2 honest: a tag is a scarce resource, so it belongs on
the scarce items. It also gives the chest a reason to be exciting beyond a stat bump.

**One thing to check before starting.** `ChestItemIntegrationTest` hard-asserts 25 items, 10 sets
and 17 stat fields, and will fail the moment anything is added. It needs updating alongside, not
afterwards.

---

### 4a. Rule-changers, and the curse

The twenty-five original items are all stat modifiers grouped as Damage, Defense, Healing, Utility
and Elemental. That is why the item roster reads as one item repeated: the only behaviour anywhere
in it lived on completed sets and in one conditional on Protective Ward.

Four items now change a **rule** instead. The test each had to pass is that it can be described
without a percentage as the subject of the sentence - "slowed enemies are vulnerable" is a rule,
"+12% damage" is not, however large the number.

| item | rarity | what it changes | reads |
|---|---|---|---|
| Cracked Prism | Uncommon | +1 projectile, all damage x0.75 | `Player.ApplyChestItemEffect` |
| Duellist's Chalk | Uncommon | hits on unwounded enemies always crit | `Player.DealDamageToEnemy` |
| Hoarfrost Nail | Rare (Ice) | slowed or rooted enemies take +25% from everything | `Enemy.IsSlowed` |
| Wormwood Tithe | Relic (Darkness, Grass) | the gap between spawns drops to 0.72x (about 39% more enemies) and they move 10% faster; +30% XP, +25% drops, +3 Luck | `Node2DGame` spawn clock and speed ramp |

Three things worth keeping in mind about this group:

- **Cracked Prism is the only item in the game that makes a number go down.** More shots that each
  hit softer is better for wide spells and worse for single heavy ones, which makes it a decision
  rather than a pickup. The roster had no genuine trades before it.
- **Duellist's Chalk keys off the target, not the caster.** It rewards spreading damage onto fresh
  enemies, which is deliberately the opposite instinct to Deathbringer's execute - two items that
  pull a build in opposite directions are worth more than two that agree.
- **Wormwood Tithe is a curse**, and the cost half cannot live on the Player: `Node2DGame` owns the
  spawn clock and the enemy speed ramp, so it asks via `Player.GetCurseSpawnIntervalMultiplier` and
  `GetCurseEnemySpeedMultiplier`. Both return 1.0 when it is not held, so the arena multiplies
  unconditionally and nothing branches. The spawn multiplier is applied *before* the
  `SpawnMinInterval` floor, so the curse can never drive the spawner past what the arena survives.

Its element tags close a real gap: Grass was the only element with no chest item carrying its tag
at all, which the audit in section 3a flagged and no amount of boon tuning would have fixed.

### 4b. Tome relics - the Scribe's Penknife and the Blank Folio

The player asked for spells to be permanent - "Remove the ability to erase spells from tomes. Once
you have your spells, that's what you've got" - and then for the two exceptions to be things you
find: "Maybe a relic could let you rip a spell out of your spell book? Another rare relic could give
you a bonus spell page." The level-up swap and remove are gone (`gameplay-design.md`, "Spells are
permanent once taken"); these two relics are the only things in the game that change the book.

| relic | id | rarity | the card says |
|---|---|---|---|
| **Scribe's Penknife** | `scribes_penknife` | Rare | Cut 1 held spell out of your spell book: its slot is free again, and it is not offered again this run. Your wizard's own spell cannot be cut. |
| **Blank Folio** | `blank_folio` | Relic | +1 spell slot: hold 7 spells instead of 6. |

**Named as objects, in the scriptorium's register.** A penknife is the knife a scribe scraped a
mistake off vellum with - the tool for exactly this - and a folio is a leaf bound into a book. The
two share a material in their icons (`tools/art/relic_icons.py`, section 11): the leaf is vellum,
and the curl coming off the knife's edge is a shaving of it. Vellum is drawn in the flesh row's lit
and base stops and outlined in flesh deep, because flesh hi is the parchment page these icons sit on
in the chest, and a leaf in it would vanish there.

**How the knife is claimed.** Pressing its page on the chest screen does not claim it: the chest
re-deals itself as a picker of the whole book (`ChestItemSelectionMenu.ShowCutPicker`), the torn
pages the level-up cards use, three or four abreast on a PC and stacked on a phone. Pressing a spell
cuts it and claims the knife in one act (`SpellCutChosen` -> `Player.TryCutSpellWithPenknife`); Back
returns to the same relics, and nothing is spent until a spell is pressed. Rules:

- **The wizard's own spell is never cut.** It is the wizard, and it is the spell the Apotheosis is
  the final form of, so cutting it would close the run's last milestone for good - a trap, not a
  choice. It is shown in the picker faded, unpressable, with the reason on it, because a picker
  that quietly left one spell out would read as a bug.
- **A cut spell is struck from the run's offers** (the ban list, without spending a Ban charge).
  Otherwise the knife is a way to take a spell back at level 1 with a different mutation, which is a
  re-roll and not what the player asked for.
- **It is only dealt when it can cut something**: a full book holding any spell but the wizard's own
  (`Player.CanUsePenknife`, passed to `GetChestItemOptions` as `unavailable`). With a free slot it
  would buy nothing a level-up does not.
- **One use.** It is owned once it cuts, so it is never dealt again; `AddChestItem` refuses it,
  because there is no such thing as holding it unspent.
- **Rare, not Uncommon.** Spells are meant to be permanent. At Uncommon (0.55) most chests after the
  book filled would offer the way out, and the permanence would be a formality.

**The Folio.** `Player.SpellSlots` is `MaxSpellSlots` (6) plus one while it is held, and every
"is the book full" question reads that (`IsSpellBookFull`). It is the rarest tier because a seventh
spell is the largest change any relic makes, and owning it makes it one per run. Two things
deliberately stay on the base six: the element reachability floor (`ValidateElementReachability` -
a relic most runs never see cannot be the floor) and the "carry six spells" deed. The pause screen's Run Summary has a **Spell slots** tile (`6/7`),
the one place the seventh slot is stated as a number; the knife's picker, the pause screen's spell
list and the ascension browser are all lists, so a seventh spell is a seventh row.

**Neither belongs to a Full Set Enchantment, and neither carries a tag** - the two rules every other
relic follows, broken on purpose (`ChestItemCatalog.TomeRelicIds`):

- A set is something you want more of, and both of these are one per run. A set that needed the
  Folio would turn a set chase into a hunt for the rarest relic in the pool; one that needed the
  knife would ask the player to cut a spell to finish a buckler.
- A tag is element budget, and a seventh slot already is element budget - up to three tags' worth,
  in whatever spell fills it. A tagged Folio would double-dip.

`RegressionChecks.ValidateTomeRelics` holds both rules, that the Folio is Relic rarity, and that the
Marginalia id cannot collide with a spell or a boon; `ValidateFullSetObjects` and
`ValidateChestRarity` exempt the tome relics by name rather than by loosening their rules.

**Marginalia - the card for a level-up with nothing in it.** Not a relic, but it exists for the same
reason and is drawn with them: with swaps gone, a late run can hold a full book at every card cap
and six boons, and the level-up would be a Skip button. Marginalia (*Heal 30% of max HP, +2% spell
damage*) fills that hand and only that hand. The reasoning is in `gameplay-design.md`.


## 5. Full Set Enchantments grant tags

### Every set is one object (issue #63)

A Full Set Enchantment is the pieces of **one real thing**, and completing it assembles that thing.
Sets used to be grouped by stat archetype (three unrelated defensive trinkets called "Vaultguard"),
so nothing told the player that a pendant and a rune completed anything. Now each relic is named as
its piece, and two pieces tell you what the third is:

| set (id) | the object | the pieces (relic id) |
|---|---|---|
| `vaultguard` | **The Reliquary** | Reliquary Key (`relic_key`) · Reliquary Vial (`vial_of_vitality`) · Reliquary Casket (`phylactery`) |
| `emberline` | **The Brazier** | Brazier Coals (`ember_flask`) · Brazier Bowl (`inferno_core`) · Brazier Tripod (`obsidian_heart`) |
| `stormbound` | **The Lightning Rod** | Copper Coil (`storm_lattice`) · Rod Tip (`ethereal_blade`) · Split Lens (`cracked_prism`) |
| `bastion_of_spikes` | **The Thorned Buckler** | Buckler Boss (`protective_ward`) · Buckler Thorns (`iron_fang`) · Hoarfrost Rivets (`hoarfrost_nail`) |
| `deathbringer` | **The Reaper's Scythe** | Scythe Blade (`spectral_fang`) · Scythe Snath (`wrath_amulet`) · Whetstone (`duellists_chalk`) |
| `eternal_guardian` | **The Barrow Harness** | Barrow Breastplate (`aegis_sigil`) · Barrow Greaves (`ironhide_cloak`) · Barrow Gorget (`basalt_carapace`) |
| `life_drain` | **The Tithe Chalice** | Chalice Cup (`essence_chalice`) · Heartstone (`heart_of_renewal`) · Tithe of Wormwood (`wormwood_tithe`) |
| `elemental_mastery` | **The Jewelled Crown** | **The Crown** (`aegis_crown`) · Frost Jewel (`frozen_tear`) · Storm Jewel (`thunderstone`) · Prism Jewel (`crystal_prism`) |
| `fortunes_favor` | **The Regalia** | **The Crown** (`aegis_crown`) · Signet Ring (`lucky_coin`) · Orb of State (`compass_rose`) |
| `speed_demon` | **The Seven-League Boots** | Left Boot (`quicksilver_pendant`) · Right Boot (`haste_rune`) |

Rules that hold it together:

- **A relic may belong to more than one set, and that is the normal case, not an exception.** The
  Crown completes the Jewelled Crown and is also one piece of the Regalia. Relics are never consumed,
  so nested sets can never block each other.
- **Ids did not change.** Saves, telemetry, icons and `Player.RefreshChestSetEffects` key on them,
  so the objects were renamed around the ids. A relic's description is the plain mechanic, and so
  is the set's (next section): the object's identity lives in its **name** and its icon.
- **Every effect label still says what the code does.** The rename was a fresh chance to
  over-promise, and the `Effects` arrays were not touched.
- **Pieces of one object share palette, metal and edge treatment** in `tools/art/relic_icons.py`, and
  the set icon is the object whole.
- `RegressionChecks.ValidateFullSetObjects`: every relic is a piece of something, every piece can
  drop, nothing is listed twice, and no partial fires at the same moment as the full effect.

### A set's description is its effect, with numbers

The player's report: "the full set enchantment descriptions should describe the effect, not fluff."
Every `ChestSetDefinition.Description` used to be a line of flavour ("A small gilded casket for what
must not be lost"), and the chest card then showed that line exactly where the player was deciding
whether finishing the set was worth a pick. Now each is **what the whole set does, in one sentence,
with its numbers**:

| set | description |
|---|---|
| The Reliquary | Take 12% less damage, and every chest you open heals 6 HP and raises a 6-point shield. |
| The Brazier | Every spell you cast adds +0.6% spell damage and +0.4% spell area for the rest of the run, up to +18% damage and +12% area after 30 casts. |
| The Lightning Rod | +10% crit chance. Every critical hit arcs to the nearest enemy within 170 for half its damage, and you move 10% faster while shielded. |
| The Thorned Buckler | Take 15% less damage. While you are below 30% HP, every hit that hurts you strikes all enemies within 120 for 8 damage + 3 per player level. |
| The Reaper's Scythe | +25% spell damage, and any hit on an enemy below 30% health kills it outright. |
| The Barrow Harness | +80 max HP, and take 18% less damage. |
| The Tithe Chalice | Every kill heals 1 HP. |
| The Jewelled Crown | Fire, Ice, Lightning and Poison tier bonuses +40%. Every hit slows 20% more for 40% longer. Lightning chains reach 50% farther and trigger 1 hit sooner. |
| The Seven-League Boots | +20% move speed and +15% attack speed. |
| The Regalia | Chests appear 25% more often, and +25% XP. |

Rules: the sentence states the **full** set only (the partial has its own `PartialEffects` rows, and
the full effect stacks on it); it must agree with `Effects` and with `Player.RefreshChestSetEffects`;
and the element tags a completed set grants are not written into it - the chest card and
`FullSetDetailScreen` append them from `ElementTags`, which no screen used to mention at all.

**Re-verifying against the code found three things that had drifted**, now fixed:

- **The Scythe Blade executed.** `ApplyChestItemEffect` set `chestExecuteThresholdPercent = 50` for
  it, under a comment calling it the +15% below half health - but that field is the Reaper's
  Scythe's execute, so the blade alone killed any enemy below half health outright, and completing
  the Scythe *lowered* the threshold to 30%. The blade's +15% is read by ownership and needs no field;
  the line is gone.
- **The Jewelled Crown's "+1 chain lightning target"** is really "chains trigger one hit sooner":
  `GetLightningChainRules` subtracts it from the hits required. The Storm Jewel said "+1 chain" and
  has the same fix.
- **Its "ice slow strength +20%"** is added to every hit's slow, Ice tier or not
  (`GetIceSlowPercent`), so it reads "every hit slows 20% more". The Frost Jewel's +15% is the same
  mechanism.

The Signet Ring's and the Tithe of Wormwood's "drop rate" is chest frequency (`Node2DGame` divides
the chest interval by it), so they say "chests appear 20% more often" and "+25% more chests".

### The Reliquary Vial: overheal, capped at 20% of max HP

The player asked for the vial's mechanic to come with a number - "like being able to heal past your
max health by 20%" - and what makes it different from more max health. It now reads **"Heal past
full health by up to 20% of max HP, as a shield that takes hits first."** (`Player.OverhealShieldCapFraction`,
read by `ChestItemCatalog.OverhealDescription` so the card and the code cannot disagree.)

**What was wrong before.** The vial handed overheal to `Player.AddShield`, which keeps the *larger*
of the old pool and the new amount - right for a shield that recharges, wrong for overheal. Most
overheal arrives a point at a time (a kill under the Tithe Chalice, a Light lifesteal tick), so the
pool could never grow past the single biggest overheal, which was usually 1. The relic did almost
nothing. Overheal now **adds** through `AddOverhealShield`, up to the cap. The cap is on the pool, not
the overheal: a bigger shield already up (an Aegis Ward cast) is left alone, and overheal has no
room until it is spent below 20%. `AddShield` keeps its stronger-of-two rule for its two callers,
Aegis Ward's cast and the Reliquary's per-chest shield, because both recharge and stacking them
would grow without bound.

**Why it is not just +20% max HP.** Four differences, all in the code:

1. **It is spent first.** The shield absorbs after armour and before health (`TakeDamage` step 6),
   so it is a buffer in front of the bar, not more bar.
2. **It only fills from healing you would otherwise waste.** Max HP is there from the moment you take
   it; this is empty until you heal at full health, so it rewards sustain - the Tithe Chalice and
   the Chalice Cup's per-kill heals, Light lifesteal, the heal on every chest - and is worth nothing
   to a build that never heals. (Regeneration does not count: it only ticks below max HP.)
3. **It is what "shielded" means.** The Buckler Boss (Protective Ward, +15% defence while a shield
   holds) and the Lightning Rod (+10% move speed while shielded) both read the shield pool, and
   overheal keeps them switched on between Aegis Ward casts.
4. **It does not raise the ceiling heals refill to.** A heal still stops at max HP; only the excess
   goes to the shield, and the shield never heals back by itself.

The health HUD now shows the pool: a steel strip along the foot of the bar on the same scale as
health, and `+N shield` after the numbers. Before, the only sign of a shield was the ring round the
player, which says whether there is one, not how much.

### Partial bonuses on the three-piece sets

Ten sets exist: eight of three or more pieces and two of two (the boots, which are a pair, and none
other). **Every set of three or more pays a taste of its effect at two pieces; the boots stay
all-or-nothing.** The Scythe, the Chalice and the Regalia grew a third piece when they became
objects, and got their partials then: +8% spell damage, +20 max HP and +10% XP.

That split falls out of the data rather than being a judgement call - on a two-piece set the
"partial" would be a single item, which is just owning the item. So the two-piece sets keep a single
unique effect as their whole identity, and the six three-piece sets get a `PartialItemCount` of 2
and a `PartialEffects` row worth roughly a third of the full bonus.

The problem this solves: two pieces of a three-piece set used to be worth exactly nothing, and the
player had no way to tell they were close. Progress is now legible without the payoff weakening,
because **the full effect is unchanged and stacks on top of the partial** - it is never refunded
when the set completes. Subtracting it back out would mean the last piece could feel like a
downgrade on any stat the two tiers share.

`Player.partialChestSets` is a separate guard from `completedChestSets` for that reason: a set pays
the partial once and the full once, and one shared guard would either skip the partial or pay it
twice. `FullSetDetailScreen` lists the partial tier *before* the full one, so a player two relics in
sees what they have already earned rather than a screen that reads as "you have nothing yet".



The term is **Full Set Enchantment** — not "synergy" — and each set is the pieces of one real
object. Completing one is the biggest single commitment the item system asks for, so it should pay
the biggest tag reward: **two or three tags at once**, enough to cross a threshold on its own.

This is also the right home for the scarcest tags. Metal and Water have almost no carriers left
after section 1, and a set is exactly the kind of long, deliberate acquisition that should be what
finally gets an element to its capstone. A Metal set completing into Metal 3 is a better story than
a Metal passive spell showing up in a level-up roll.

---

## 6. Migration, and what breaks quietly

- **Save migration is mandatory.** Eleven spell ids disappear from `UnlockedSpellIds`, and
  `SaveData.Migrate()` has to strip them and grant equivalents, or every existing save carries dead
  references. Schema bump, same shape as the migration to 8.
- **`UnlockCatalog` loses 11 of 32 entries**, and `RegressionChecks.ValidateUnlockCatalog` requires
  every spell and character to have exactly one source. Boons need a `UnlockKind` of their own, or
  the audit will not cover them at all.
- **`RemovedSpellIds` curation** can hold passive ids that no longer exist.
- **The Untouchable achievement** loses its reward and needs a new one.
- **`CharacterData.StartingPassiveId`** is a *different* system (a flat character bonus, validated
  against `Player.CharacterPassiveBonusIds`) and is unaffected. Worth saying out loud because the
  name collides exactly.

---

## 7. Phasing

**Phase A — the audit that protects the rest.** Add the `RegressionChecks` assertion that every
element can reach its capstone. Do this *first*, while passives are still in and it passes, so the
moment they come out the failure is visible rather than silent.

**Phase B — boons.** The type, the unlock kind, the level-up card variant, the cap, and enough of
them to refill the starter and shop slots the passives vacate. Weighted toward the five starved
elements.

**Phase C — remove passive spells.** Only after B, so the economy is never empty in between. Save
migration lands here.

**Phase D — item rarities**, and the `ChestItemIntegrationTest` update.

**Phase E — Full Set Enchantment tags**, which is the smallest change and the one that depends on
D being settled.

The ordering matters more than usual here: C before B leaves the shop with two items in it and the
level-up pool with five candidates, which is a worse game than either the before or the after.
