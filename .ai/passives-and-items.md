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
- **The augury could name any boon** - the deliberate way to get a specific one - until the Augury
  was removed on 2026-10-28 (`.ai/gameplay-design.md`, "Level-up charges"). The measurements below
  were taken with it.
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

### 3c. The stance boon: Taproot (2026-10-23)

The player asked for "a passive / spell / item / item set that adds bonuses if you stand still for
a time". The Brazier set became that (section 5, "The Brazier burns while you stand still"), but a
set is three chests deep, so a boon puts the same demand in front of every run.

| | |
|---|---|
| id / name | `taproot`, **Taproot** - an object, like every boon: a root that only feeds once it is in the ground |
| tags | **Earth, Grass**. Earth first, so its aura (`BoonAura`) and the ring at the feet are earthen; Grass because Grass is the regeneration element and one of the five thin ones (section 3a) |
| effect | `BoonEffect.StanceRegen`: **stand still 1.5 s (`BoonCatalog.StanceSeconds`), then +1.5 HP a second** until you move. Any step breaks it and the 1.5 s starts again |
| card | "Stand still 1.5 s: +1.5 HP per second" - "HP per" picks the regen glyph (`StatIcons.KeyFor`) |
| source | **Starter** (see below) |
| art | `tools/art/boon_icons.py`, `taproot()`: a parsnip-shaped root, three leaves at the crown, a forked root-rune lit in the Earth ramp down its body. Drawn a step down the flesh row, because Earth's backlight is the same warm tan as the row's upper stops and the first, paler root vanished into it |

**Healing, not Armor.** The obvious stance boon is "take 15% less damage while still", and it would
be a lie. What a planted player takes is the swarm's one-damage touches (a never-moving bot took
about twelve a second), and Armor is floored at 1 - it reduces a hit, never deletes it (`Player.TakeDamage`,
and `gameplay-design.md`, "Armor") - so 15% of a one-damage touch is nothing, and the card would
promise the one player who took it something it never delivers. Regeneration is the stat that answers
chip; evasion is the other, and evading while planted contradicts itself.

**Why 1.5 a second.** 2.5 times Tidewater Flask's always-on 0.6, the premium the conditional relics
carry over their unconditional cousins (Protective Ward's +15% Armor while shielded against Iron
Rivets' +8%; Ironhide Cloak's +20% while healthy), and between Grass tier 2 (1) and tier 4 (2), so it
never outranks the element it carries. It does not make a surrounded player safe and is not meant to:
it heals the stand between waves.

**A stance is all or nothing; the heat is a slope.** Taproot breaks on any step, where the Brazier's
heat cools over 3 s. They are drawn differently for that reason (`StanceGlow`): the stance is an
earthen ring drawn round from the front as the 1.5 s fills, then closed and breathing with four roots
reaching out of it; it vanishes the moment he moves, while the embers go out one by one. The pause
screen's Boons section says which it was when the game paused ("Rooted: +1.5 HP per second now." /
"Not rooted - stand still 1.5 s.").

**Why a starter, and the shop that sells no boons.** The natural source for a new boon is the shop,
and the shop does not sell boons: `MainMenu.BuildUpgradeDefinitions` registers only
`UnlockCatalog.PurchasableSpells`. The seven `PurchaseBoon` entries (Quicksilver Bead, Thornseed,
Deepwater Pearl, Shadegrease, Saltbound Chain, Gilded Mote, Wishing Coin) have only ever been granted
by the schema-9 payback, so on a new save they are cards no level-up can deal - and section 3a's
carrier counts, and `ValidateElementReachability`, count them anyway. A Taproot sold there would have
joined them. As a starter it is in every run's pool from the first level-up, and the starter boons
were already the ones meant to make thin elements reachable early. Selling the seven is separate
work; until it lands, read every "boons" column in this document as optimistic for those elements.

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
| Wormwood Tithe | Relic (Darkness, Grass) | the gap between spawns drops to 0.72x (about 39% more enemies) and they move 10% faster; +30% XP, +5% bigger XP orbs (was +25% chests - section 8), +3 Luck | `Node2DGame` spawn clock and speed ramp |

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
a relic most runs never see cannot be the floor) and the "carry six spells" unlock (Full Grimoire, on the Unlocks list). The pause screen's Run Summary has a **Spell slots** tile (`6/7`),
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
| `fortunes_favor` | **The Regalia** | **The Crown** (`aegis_crown`) · Signet Ring (`lucky_coin`) · Orb of Antioch (`compass_rose`) |
| `speed_demon` | **The Seven-League Boots** | Left Boot (`quicksilver_pendant`) · Right Boot (`haste_rune`) |
| `cabinet_of_curiosities` | **The Cabinet of Curiosities** | Lodestone (`lodestone`) · Curio Ledger (`curio_ledger`) · Hidden Drawer (`hidden_drawer`) - the collector's set, section 8 |

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

### The moment a set assembles (issue #43, 2026-10-28)

Completing a set used to show nothing during a run: the relic strip that marked it
(`ChestItemHUD`) is no longer built there, so the only trace was a line in the pause screen.
`Player` now emits `ChestSetCompleted(setId)` as the last piece is claimed, and `Node2DGame` plays
`SetAssembledMoment`: **the pieces' icons fly in from the sides and meet** (0.55 s, eased in so they
slam together), a white flash and a gold ring, the set icon - the object whole - stands at x3 in
slow rays, with a stone plate giving "Full Set Enchantment", the object's name in Cinzel and its
description; it holds to 3.6 s and lifts away as it fades. `spell_evolve` sounds at the flash
(no sound was generated for it, and it is as rare as an evolution).

- **It sits between the HUD and the player's head**, because the camera keeps him at the centre:
  object and plate side by side on a wide canvas, stacked on a phone. The first layout, a third of
  the way down, put the plate over him.
- **It takes no input and pauses nothing** (every control ignores the mouse, so the touch stick
  under it still works). Layer 7: above the stick, below the level-up menu (10), the game-over
  screen (10) and the chest menu (100). Pausable, so a set completed from the chest menu waits
  hidden until the run resumes, and two sets completed by one relic play in turn.
- Photograph it with `_UiShot --give-relics=<the set's pieces>` (a short `--frames` and a
  `--burst` catch the gathering).

The same shot found the Barrow Harness's full +80 max HP arriving empty (its partial's +30 was
filled, every other set's max HP is): a player on 50 of 50 completed it and read 50 / 130. It now
fills as well.

### A set's description is its effect, with numbers

The player's report: "the full set enchantment descriptions should describe the effect, not fluff."
Every `ChestSetDefinition.Description` used to be a line of flavour ("A small gilded casket for what
must not be lost"), and the chest card then showed that line exactly where the player was deciding
whether finishing the set was worth a pick. Now each is **what the whole set does, in one sentence,
with its numbers**:

| set | description |
|---|---|
| The Reliquary | Take 12% less damage, and every chest you open heals 6 HP and raises a 6-point shield. |
| The Brazier | Stand still: heat builds over 4 s to +30% spell damage and +15% spell area. Moving cools it over 3 s. (Was a per-cast ramp to +18% / +12% - see "The Brazier burns while you stand still", below.) |
| The Lightning Rod | +10% crit chance. Every critical hit arcs to the nearest enemy within 170 for half its damage, and you move 10% faster while shielded. |
| The Thorned Buckler | Take 15% less damage. While you are below 30% HP, every hit that hurts you strikes all enemies within 120 for 8 damage + 3 per player level. |
| The Reaper's Scythe | +25% spell damage, and any hit on an enemy below 30% health kills it outright. |
| The Barrow Harness | +80 max HP, and take 18% less damage. |
| The Tithe Chalice | Every kill heals 1 HP. |
| The Jewelled Crown | Fire, Ice, Lightning and Poison tier bonuses +40%. Every hit slows 20% more for 40% longer. Lightning chains reach 50% farther and trigger 1 hit sooner. |
| The Seven-League Boots | +20% move speed and +15% attack speed. |
| The Regalia | +25% XP, and +10% chance an XP orb drops bigger: 10 XP instead of 2. (Was "Chests appear 25% more often" - section 8.) |
| The Cabinet of Curiosities | Every elite drops a chest, and +50 pickup radius. |

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

The Signet Ring's and the Tithe of Wormwood's "drop rate" was chest frequency (`Node2DGame` divided
the chest interval by it), so they said "chests appear 20% more often" and "+25% more chests".
**Superseded (2026-10-22): chests no longer come on a clock, and all three chest-frequency pieces
pay in bigger XP orbs instead - section 8.**

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

Eleven sets exist: ten of three or more pieces and one of two (the boots, which are a pair, and none
other). **Every set of three or more pays a taste of its effect at two pieces; the boots stay
all-or-nothing.** The Scythe, the Chalice and the Regalia grew a third piece when they became
objects, and got their partials then: +8% spell damage, +20 max HP and +10% XP. The Cabinet of
Curiosities (section 8) was built with one: +25 pickup radius.

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

### The Brazier burns while you stand still (2026-10-23)

The player's report: "brazier item set says 'every spell you cast up to a maximum'. wouldn't that
just max out very quickly? whats the point of it building up?" - and, in the same breath, "lets add
a passive / spell / item / item set that adds bonuses if you stand still for a time". The two
answered each other: the Brazier became the stand-still set, and a boon (section 3c) puts the same
mechanic in reach of a run that never finds the three relics.

**What was wrong with the ramp.** Every cast added +0.6% spell damage and +0.4% spell area, to a
cap of +18% / +12% after 30 casts, and nothing ever took it away. A loadout casts several times a
second through the one firing loop (the same casts Arcane Explosion counts), so the cap arrived a
few seconds after the set completed and stayed for the rest of the run. It was a flat bonus with a
wind-up nobody could see - exactly what the player said.

**The rule now.** The set holds a **heat** from 0 to 1 (`Player.brazierHeat`, ticked in
`Player.TickStandStill`):

| | |
|---|---|
| standing still | heat rises 1/4 a second: cold to full in **4 s** (`BrazierHeatUpSeconds`) |
| moving | heat falls 1/3 a second: full to cold in **3 s** (`BrazierCoolDownSeconds`) |
| at full heat | **+30% spell damage, +15% spell area** (`BrazierDamageAtFullHeat`, `BrazierAreaAtFullHeat`) |
| in between | both scale linearly with the heat, read live: damage at the moment of each hit (`GetChestDamageBonusPercent`), area at each cast (`GetEffectiveAreaMultiplier`) |
| a level-up screen | freezes it - the tree is paused, so nothing ticks |

The pieces, the partial (+6% spell damage at two pieces) and the Fire and Light tags are unchanged:
only the full effect moved. The card is built from the four constants
(`ChestItemCatalog`, the Reliquary Vial's pattern), so it cannot drift from the code.

**A slope, not a reset.** A reset on moving would make every sidestep cost the whole stack, and a
telegraphed slam is precisely the thing that asks for a sidestep. Cooling over 3 s means a 0.3 s step
costs a tenth of the heat; a second of stillness wins it back. Cooling is a little faster than
heating on purpose, so drifting - a second still, a second moving - sinks toward cold rather than
holding warm. The set pays for a *stand*.

**Why those numbers.** What the set pays depends on how a player plays, which is the point. Mean
heat over a long run of stand-and-move cycles, computed from the two rates:

| play | still | mean heat | spell damage | spell area |
|---|---|---|---|---|
| never moves | 100% | 1.00 | +30% | +15% |
| 6 s stands, 2 s moves | 75% | 0.81 | +24% | +12% |
| 4 s stands, 2 s moves | 67% | 0.74 | +22% | +11% |
| 3 s / 3 s | 50% | 0.33 | +10% | +5% |
| 1 s / 1 s | 50% | 0.11 | +3% | +2% |
| kiting | ~0% | 0 | 0 | 0 |
| **the old ramp, any play** | - | - | **+18%** | **+12%** |

So a player who plants pays the risk of standing in the swarm - one-damage touches that Armor cannot
reduce below 1 - and earns more than the old set gave; a player who kites earns less than it did.
The ceiling is bounded and exact: the whole set's spell damage at full heat is +58% (Coals 12 +
Tripod 10 + partial 6 + heat 30) against +46% before, so a player who never moves deals at most about
8% more than the old set paid, and only in the play style the set asks for. The Tripod's -5%
movement speed was already pointing this way.

**What "standing still" is.** `Player.IsStandingStill` and `Player.StillSeconds` - the one
definition, which Taproot and the visual read too, and which Solar Flare's planned rework (#68)
should read (`.ai/spell-roster.md`). It is **intent, not displacement**: still means no movement
input - no key, no stick past `move_*`'s 0.2 deadzone, no thumb pushed past the touch stick's 0.15
(`TouchControls.MoveVector` reads zero for a thumb resting on the stick). That is the same `walking`
test `MovePlayer` already makes for the crowd slowdown, so the two cannot disagree. Being shoved,
knocked back or slid off a blocker never counts as moving; holding a direction into a wall does.
Hunter's Draw measures displacement instead, because what it charges on is ground actually covered.

**The tell.** `scripts/StanceGlow.cs`, one node at the feet the player makes the first time the set
or Taproot is owned (nothing for a run holding neither): twelve embers on an ellipse round the boots
that catch one at a time as the heat builds - from the front, round the left, across the back, down
the right - so half the ring lit is half the bonus, rising and brightening from the Fire mid toward
its white-hot core; at full heat a glow spreads under him and sparks lift off. Walking puts them out
one by one. It filled from the front up both sides at first, and at half heat the whole visible front
was lit while the unlit half hid behind his legs - half read as nearly full - so it sweeps instead.
The number itself is on the pause screen's Run tab, under the set's line: "Heat 62%: +19% spell
damage, +9% spell area (full: +30%, +15%)", exact because the pause froze it.

**Removed with the ramp:** `TickEmberlineCast`, the four `Emberline*` exports and the two stack
fields. Nothing else read them. Arcane Explosion's charge was the other thing keyed off a cast and is
untouched: it is fed by `NotifyArcaneCharge` at the same point in the firing loop, which the ramp's
call sat beside and never shared.

**Measured.** `scenes/_StandStillProbe.tscn` (headless, exit code 1 on failure) drives the real
game through standing, walking and three 40 px shoves with the set and Taproot held; every check
held, heat within 0.03 of the rates at every step:

| step | heat | rooted |
|---|---|---|
| still 2.0 s from cold | 0.50 | yes |
| still 1.0 s, shoved three times | 0.75 | yes |
| still 1.5 s more | 1.00 | yes |
| walk 0.3 s | 0.90 | no |
| still 0.6 s | 1.00 | no |
| walk 1.5 s, then 2.0 s more | 0.50, then 0.00 | no |
| still 1.6 s | 0.40 | yes |
| 3 s wounded: rooted against walking | healed 5 against 1 | Taproot paid 4 (4.5 expected; whole-HP accumulator) |

The balance bot carries a "stand still" line in every report now, and takes `--give-relics=`,
`--grant-boons=` and `--stand-still`. Pyromancer, forest, seed 1234, the Brazier handed over at the
start, 15 minutes, `--all-unlocked`:

| bot | still | mean heat | full heat | level at 15:00 | damage per minute |
|---|---|---|---|---|---|
| `--immortal` | 9% | 0.05 | 1% | 16 | 10 437 |
| `--immortal --stand-still` | 100% | 1.00 | 100% | 11 | 5 778 |
| default (kites) | 0% | 0 | 0% | died at 1:36 | - |

**The `--immortal` bot does not stand still** - it walks to the nearest XP orb almost without pause,
so it earns almost nothing from the set; it is not a test of the Brazier at all. The never-moving
bot pins the heat at full and gets exactly the card's +30% / +15%, and nothing more, and it ends five
levels down because standing still costs XP: orbs only come to it inside its pickup radius. Neither
is a player; the table above is the honest model of one.

### What needs playing (the Brazier and Taproot)

- Whether a stand feels worth its risk at +30% / +15% - and whether the cool rate (3 s) is right, or
  should be slower so a player can reposition between stands without losing most of the heat.
- Whether the embers read in a real crowd on every chapter's floor - the Frozen Waste's pale ground
  above all, and under a Pyromancer, whose own fire competes with them.
- Whether Taproot's 1.5 HP a second is noticed at all against a full surround (a planted bot took
  twelve touches a second), or is the right size for healing between waves.
- Whether the stance ring breaking on any step is right, or wants the heat's leniency.
- On a phone, whether a thumb resting on the stick ever reads as moving (it should not: the stick
  has a 0.15 deadzone and reads zero at rest).



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

---

## 8. Collecting: the drop table, chests from carriers, and the collector (2026-10-22)

The player's request, whole: "there should be characters and items who specifically are designed
to make builds that collect items. the speed up potions are spawning a lot in game. lets reduce the
spawn rate of certain droppables, connect chest drops to bosses, change items to make more valuable
xp orbs show up more often instead of making items that make chests drop more often, and then make
a character / items that provide more opportunities for chests or up the number of options in a
chest. oh and also the collector wizard type + items can include pickup range"

Every number below is in `scripts/DropTable.cs` or read from `Player` constants by the card text, so
the code, the card and this section cannot drift apart without one of them being edited.

### 8a. The drop table

What an ordinary kill can drop besides its own 2 XP orb. It used to be **one** chance - 3% a kill,
plus 7 points at full Luck, plus every chest-frequency relic, capped at 25% - split evenly three
ways. That last term is why the potions flooded: the Signet Ring added 20 points to it, so a run
holding the ring dropped a bonus item on almost a quarter of all kills, a third of them potions.

| drop | before | after |
|---|---|---|
| XP orb x8 (16 XP) | 1% a kill (a third of 3%) | **1%, unchanged** - it is part of the XP economy the gates were measured on |
| health flask (10 HP) | 1% | **0.4%** |
| speed potion (8 s, +50% attack speed, +30% move speed) | 1% | **0.3%, and never two within 45 real seconds** |
| Luck | the shared 3% rose to 10% at full Luck | each chance x(1 + 2.33 x Luck), the same 3.3x at full Luck |
| a relic holding "more chests" | +20 or +25 points on the shared chance, to the 25% cap | nothing - see 8c |

Measured with `_BalanceHarness -- --minutes=15 --character=N --timescale=2 --immortal --all-unlocked`,
one run at a time, on a machine other sessions were also loading - so read the counts, not the
timings. The harness now counts every pickup that enters the world, in five-minute windows
(`_BalanceHarness.DropLedger`). The bot holds no relic that touches drops in either column: under
the harness a chest takes its no-menu path and grants a Relic Key.

Pyromancer, forest, seed 1234, the same flags both times. "After" is the build as measured, at a
potion chance of 0.2% and an elite chest chance of 50%; the two rows marked *projected* are what
the shipped 0.3% and 65% give on the same run's kills (3406 ordinary kills, 16 elites killed),
which were raised after this run showed four potions and twelve chests to be too few.

| per 15-minute run | before | after, measured | after, as shipped (projected) |
|---|---|---|---|
| speed potions | **52** (3.5 a minute; 12 / 26 / 14 by five-minute window) | 4 (0.27 a minute) | **about 10** (0.3% x 3406), never two within 45 s |
| health flasks | 29 (1.9 a minute) | **12** (0.8 a minute; 3 / 4 / 5) | 12 |
| bonus XP orbs (16) | 61 medium orbs | 40 | 40 |
| chests | **20**, all from the clock (7 / 6 / 7); the bot walked into 14 | 12: 8 from elites, 4 from Wardens (1 / 7 / 4) | **about 15**: 1 + 15 x 65% elites, 4 Wardens |
| elites and Wardens killed | 18 of 20 | 20 of 22 | - |
| XP dropped | 8758 | 8452 | 8452 |
| player level at 15:00 | 26 | 26 | - |

The before bot took the Wishing Coin boon (+3 Luck) partway through, which raised its bonus drops
by up to a third from then on, and the after bot did not; so the before counts run a little above
the 1% each the table above promises, and the XP orb row moved for the same reason rather than
because anything about it changed. The level at the end is the same - the XP economy is untouched.
Bigger orbs do not appear in either run, because the bot holds no relic that grants them.

**Why those numbers.** The potion is a burst, and a burst that is up a large share of the time is a
stat, not a moment. About ten a run - two thirds of one a minute at the back half's kill rate - and
never two within 45 s, makes picking one up an event again, and keeps it up about a tenth of the
run where it used to be up nearly half of it. The spacing is real seconds because it is a rhythm - how often the burst may be
felt - which is the rule `.ai/run-pacing.md` gives for feel timings. The flask is cut by less: the
swarm deals 1 a touch, and the flask is most of the healing a build without Grass or Light has.
Measured it went from 29 to 12, so a run still finds about one flask a minute in its back half. The XP orb is untouched for the reason in the table.

### 8b. Chests come from carriers

There is no chest clock. It used to set one down beside the player at 0:20 and every 45 pace
seconds after - 20 a run, whatever the player did. Now a chest is carried, and drops where its
carrier dies (`Node2DGame.DropCarriedChest`), rising out of the ground for 0.6 s so it is never
claimed on the frame it appears, and set out to 90 px if the carrier died on top of the player:

| carrier | chest | how many a 15-minute run |
|---|---|---|
| an elite (`SpawnEnemy`'s elite roll) | 65%, raised by the Curio Ledger and the whole Cabinet; **the run's first elite always** | about 20 come (from 2:15, every 54 to 30 pace s), at most 2 alive |
| the Warden (Rockem in the Sketchbook) | always | 4 - at 5:15, 8:00, 10:45 and 13:30 |
| the Sketchbook's ghost | always | 2 - at 4:00 and 9:30 |
| a side event, once its discovery is owned | always | 0 or 1 |
| the boss | never: its death ends the run | - |
| an event's own elites; Rockem in the ordinary table | never: the event pays its own chest, and Rockem is 7% of the Sketchbook's table | - |

The carrier opts in (`Enemy.ChestSource`, set by the spawner), because `IsMiniBoss` cannot say
which bodies should pay: Rockem wears it both as the scheduled miniboss and as an ordinary spawn,
and an event's key-carrier wears it too. No chest drops once the boss has arrived, the rule the
clock kept.

**Chests per run: 20 before; about 15 after.** Measured at a 50% elite chance it was 12 (8 elite, 4
Warden - the first elite's guaranteed chest included), which left a three-piece set too far out of
reach, so the chance is 65%. A collector gets back past the clock: the Curio Ledger puts elites at
90% (about 18 a run), the whole Cabinet at all of them (about 20), and the Hidden Drawer adds a
quarter on top (about 25). The first chest comes with the first elite, at about 2:15, instead of at
0:20.

### 8c. The three chest-frequency relics pay in bigger orbs

With no clock, "chests appear more often" means nothing, so the Signet Ring, the Tithe of Wormwood
and the Regalia were changed to the player's other ask - "make more valuable xp orbs show up more
often". A **bigger orb** is an ordinary kill's orb dropped one size up: 10 XP instead of 2, in the
medium orb art (`DropTable.GreaterOrbValue`, `Player.GetGreaterOrbChance`). Each point of chance is
worth 0.08 XP a kill against a base of 2.16, so +5% is about +18% XP - the Orb of Antioch's +15%,
paid in something that shows on the ground.

| relic | before | after |
|---|---|---|
| Signet Ring (Common, the Regalia) | "Chests appear 20% more often." - and, unsaid, +20 points of bonus drop chance: about +49% XP, and the potion flood | **"+5% chance an XP orb drops bigger: 10 XP instead of 2."** (about +18% XP) |
| Tithe of Wormwood (Relic, the Tithe Chalice) | "+39% enemies, +10% enemy speed, +30% XP, +25% more chests, +3 luck." | **"+39% enemies, +10% enemy speed, +30% XP, +5% bigger XP orbs, +3 luck."** ("Bigger", because the phone sets these five stats in two columns, which needs each under 19 characters.) |
| The Regalia (set) | "Chests appear 25% more often, and +25% XP." | **"+25% XP, and +10% chance an XP orb drops bigger: 10 XP instead of 2."** |

Set membership is unchanged. What it does to the level clock is modelled in `.ai/run-pacing.md`
section 8: the floor does not move, and a player who finds one of these early now levels **later**
than before, because the old ones were much larger XP items than their cards said.

### 8d. The collector: John Dee and the Cabinet of Curiosities

**John Dee** (1527-1608/9): Elizabeth I's astrologer and adviser, mathematician, and the greatest
collector of books in England - three to four thousand books and manuscripts at Mortlake, with
instruments and curiosities, among them the obsidian scrying mirror now in the British Museum. His
library was looted while he was abroad. He is a historic magic-worker, so he fits the naming rule
for unlocked wizards (`.ai/world-and-tone.md`), and of the names on that list he is the one whose
life was *collecting*. Considered and passed over: Nicolas Flamel (gold, not collecting) and Ole
Worm (a real cabinet of curiosities, but not a magic-worker).

| | |
|---|---|
| id / roster | `john_dee`, `CharacterData_JohnDee.tres`, appended to `CharacterRoster` (index 7, `--character=7`) |
| passive | **Collector's Eye - "+1 relic to choose from in every chest"** (`chest_options_1`). With the Relic Key, five. |
| starting spell | **Hollow Star**, pure Darkness - the pull spell: a well on the densest pack that drags it into one knot. No wizard opened on Darkness, and it is the one spell in the roster that is about *gathering*. It is sold in the shop (160); `GetLevelUpOptions` offers a wizard his own spell whether or not the save owns it, as it does for Tituba. |
| unlock | Achievement **The Library at Mortlake - "Open 30 chests"** (`mortlake_library`), measured on `Lifetime.TotalChestsOpened`, which every save has kept since schema 8: no new stat, no new save field. Thirty is about two runs that reach the boss on the carried-chest economy. |
| Apotheosis | **Yes - The Spirit Mirror, verb "pull"**, which was on the ADR's unclaimed list. Path: Amplified Impact / Titan Cataclysm, the pair that deepens Darkness. The well opens half again as wide and drags 35% harder, and when it closes every XP orb, potion and flask inside its reach flies to him (`HollowStar.PullLootToPlayer`). |
| health / speed | x0.95 / x1.04 - an old man who walks to his chests |
| art | `tools/art/roster.py`, `dee()`: a full set - 4-frame idle, 8-frame walk, hurt, death - derived like every wizard's (`anim_sets.player_set`), 0.91 edges per pixel against the 0.80 floor. A scholar's near-black gown (the stone row: no player pigment, since he is freed, not a starter), a starched ruff, a black skullcap, the long white beard, a girdle book on a chain, his free hand cupped with the well's motes spiralling into it, and the obsidian mirror in a gold ring on his staff - the one thing on him that glows, in the Darkness ramp. The palette had no printable character left, so that ramp is four control characters (`roster.DARKNESS_RAMP`). |
| save | **Schema 12** (11 is main's `SeenAscensionIds`; the two stack). The Emberdeep's gate is "every wizard freed", evaluated live, so a new wizard would close a chapter a player had opened. `SaveData.MigrateCollectorIntoCompletedCampaign` credits him to any save that met the gate as it stood before 12; the steps for 8 and 10 neither grant nor count him. |

**The Cabinet of Curiosities** (`cabinet_of_curiosities`) - a Renaissance collector's cabinet, Dee's
kind of object, in ebony inlaid with ivory (`tools/art/relic_icons.py`, section 12; the ivory line
is the family mark). Its tags are Arcane, whose tiers pay XP, and Darkness, Dee's element.

| relic | rarity | the card says |
|---|---|---|
| Lodestone (`lodestone`) | Common | +50 pickup radius. |
| Curio Ledger (`curio_ledger`) | Uncommon | +25% chance an elite drops a chest (65% without it). |
| Hidden Drawer (`hidden_drawer`) | Rare (Darkness) | 25% chance a chest you open leaves another chest behind. |
| two pieces | partial | +25 pickup radius |
| all three | Full Set Enchantment | **Every elite drops a chest, and +50 pickup radius.** |

The drawer's second chest rises beside the player as a side event's does (1.2 s) and can hold
another in turn - one in four, then one in sixteen. "+1 option in every chest" is Dee's passive and
the Relic Key's; a third source would put six pages on a phone, so the Cabinet pays in chests.

**The chest screen with five relics** (Dee holding the Relic Key). Photographed with `_UiShot
--character=7 --give-relics=relic_key,lodestone --open-chest`, with the five relics pinned so the
worst pages were in it (Frost Jewel's four-piece set, Buckler Thorns' long effect, the Hidden
Drawer with its partial switching on). As the screen stood, a PC put the five abreast at about 234
wide, the names broke mid-word ("Hearts/tone", "Buckle/r Thorn/s") and the tallest page needed 856
of 595. `ChestItemSelectionMenu.fiveAbreast` fixes it on a PC only: the name drops to the Label size
in its Cinzel face, the art to 48 and the set's to 40, the page margins to 12, and a set box shows
its full tier only when this claim completes the set (the pieces and the count still show how
close it is). Now every page fits a 1280x720 PC, worst 575 of 595, no word broken. A phone stacks
the five and scrolls, by about 460 px (1804 of 1347) - a four-relic chest already scrolled there,
by about 90. A five-relic chest offering the piece that COMPLETES a long set (the Jewelled Crown)
has not been photographed; that page is the one that could still outgrow a PC.

### 8e. Found on the way

- **XP bonuses rounded to nothing on a 2 XP orb**: the Orb of Antioch's +15%, Arcane tiers 2 and 4,
  Sage Spiral's first two levels. Fixed by carrying the fraction; what it moves is in
  `.ai/run-pacing.md` section 7, finding 10. It is a one-field change in `Player.AddXp`
  (`xpFraction`) and can be reverted on its own if the faster Sage Spiral is not wanted.
- **Hollow Star had Bramble Seed's bug**: its evolutions' area grew nothing, while Titan
  Cataclysm's x2 visual scale drew the well twice as wide as it pulled. Dee's Apotheosis path runs
  through Titan Cataclysm, so it is fixed the same way - the area is in the radii and the node scale
  is reset. A Titan well now reaches about 390 px (150 x 1.3 x 2); its pull speed is unchanged, and
  240 px/s for 1.6 s already carries the rim to the knot.
- `XPOrb`'s comment on the drop table (2 and 3 for ordinary kills, 5 for elites) repeated the
  finding-1 fiction in `.ai/run-pacing.md`; it now says what drops.

### 8g. The balance and QA pass (issue #44, 2026-10-28)

Chest frequency and rarity were already measured and set (8a to 8c). What #44 still asked: how
strong each Full Set Enchantment is, whether any is dead or mandatory, the edge cases, and a
regression checklist.

**How it was measured.** The Pyromancer (`--character=1`), seeds 11 and 23, each set handed over
whole at 0:00 (`--give-relics`), against the same run holding nothing. Two bots: `--immortal` for
8 minutes (damage dealt, HP lost, kills, level) and the mortal `--survivor` for 12 (how long it
lives). Mean of the two seeds.

| set | dealt (immortal) | HP lost | kills | level at 8:00 | survivor: died at (min) |
|---|---|---|---|---|---|
| nothing | 69,374 | 4,234 | 1,501 | 16.0 | 3.4, 4.2 |
| The Reaper's Scythe | x1.39 | x0.55 | x1.35 | 18.0 | **lived, lived** |
| The Regalia | x1.34 | x0.62 | x1.21 | **25.5** | **lived, lived** |
| The Brazier | x1.40 | x0.62 | x1.23 | 18.0 | 5.7, lived |
| The Tithe Chalice | x1.37 | x0.78 | x1.34 | 22.5 | 7.0, 11.4 |
| The Jewelled Crown | x1.33 | x0.61 | x1.21 | 18.0 | 7.3, 9.6 |
| The Lightning Rod | x1.21 | x0.78 | x1.15 | 17.0 | 3.5, 3.8 |
| The Thorned Buckler | x1.12 | x0.69 | x1.07 | 16.5 | 10.0, lived |
| The Seven-League Boots | x1.09 | x0.67 | x1.03 | 16.5 | 2.6, 6.2 |
| The Barrow Harness | x0.94 | x0.81 | x0.95 | 15.0 | 4.4, 10.4 |
| The Reliquary | x0.96 | x0.92 | x0.96 | 16.5 | 2.8, 3.1 |
| The Cabinet of Curiosities | x0.90 | x1.07 | x0.91 | 16.0 | 2.5, 3.8 |

Two seeds and a bot: read differences under about 10% as noise. And a set handed over at 0:00 is
not how a run gets one - the third piece of a set comes late, if at all, from about fifteen chests -
so a set that pays over a long run (the Reliquary's heal and shield per chest, the Cabinet's
chests) is undersold here, and one that pays at once is oversold.

**What it says:**

- **No mandatory set.** The two strongest - the Reaper's Scythe (+25% damage and the 30% execute
  on top of its pieces) and the Regalia (XP: ten levels ahead by minute 8) - are strong the way a
  capstone should be, and both are three pieces from about fifteen chests.
- **Three sets look dead for fighting, and one of them is meant to be.** The Cabinet pays in
  chests (8d), which this cannot see. **The Reliquary and the Lightning Rod** are the real
  question: neither kept the survivor alive longer than nothing did. The Reliquary's -12% is the
  smallest damage cut of the defensive sets and its chest payout is slow; the Lightning Rod's speed
  only applies while shielded and its arc needs a crit. Candidates, **not applied** - your numbers
  to choose: the Reliquary to -18% (the Barrow Harness's) or its chest heal to a share of max HP;
  the Lightning Rod's speed unconditional.
- **The defensive sets were invisible until this pass** - see the fixes below.

**What it fixed:**

- **HP actually lost is counted now** (`GameStats.RunHpLost`). The run's "damage taken" is fed by
  `Player.DamageTaken`, which carries the raw hit by design, so no armour, shield or damage cut ever
  showed in it: the first sweep had the Barrow Harness "taking" 11% more than nothing.
- **The Barrow Harness's +80 max HP arrived empty** (50 / 130 the moment it completed); it fills
  now, as its partial and every other set's max HP do. Found photographing the set moment (#43).
- **One run menu at a time** (`Node2DGame.ResumeOrOpenNextMenu`). Two screens in one physics step
  went wrong three ways: a second chest was spent giving nothing; a chest claimed over a level-up
  unpaused the run under the level-up screen, which then sat there dead (it only takes input
  while paused); two level-ups built two menus and lost one. A screen that would open over another
  now waits its turn. `_UiShot --menu-queue-probe` drives all three; the old code failed 6 of 9.

**Edge cases checked, and how they stand:**

| case | what happens |
|---|---|
| a chest offers a relic already held | never in one roll (`GetChestItemOptions` draws without replacement); owned relics only pad a roll once the unowned pool is empty, never a tome relic; claiming one changes nothing, the chest still heals 4 and pays the Reliquary |
| a relic in two sets (The Crown) | both sets count it; relics are never consumed, so nested sets cannot block each other (`ValidateFullSetObjects`) |
| a partial, then the full set | the partial is paid once and never refunded; the full effect is on top of it (`RefreshChestSetEffects`) |
| the piece that completes a set | the set's moment plays when the chest screen closes (#43); the Reliquary pays on the completing chest too |
| two chests, a chest and a level-up, two level-ups in one step | queued, one screen at a time (above) |
| the Hidden Drawer's chest | spawned after the claim, rising 1.2 s, so it can never land under an open screen |
| the boss arrives | no chest drops after it (8b) |

**Regression checklist** - after any change to relics, sets or the chest screen:

1. The startup validators: `ValidateChestSetPresentation`, `ValidateFullSetObjects`,
   `ValidateChestRarity`, `ValidateTomeRelics`, `ValidateCollectorEconomy` (0 errors).
2. `_UiShot --menu-queue-probe` under xvfb: ALL PASS.
3. `_UiShot --give-relics=<a set's pieces>` with a short `--frames` and `--burst`: the pieces fly
   together and the plate fits between the HUD and the player, on 1280x720 and on 540x1212
   `--portrait --safe-area`.
4. The chest screen with five relics on a phone (8d) and a set's completing piece on offer.
5. If a set's numbers moved: this section's sweep (`--give-relics`, both bots, two seeds) and its
   table updated; `ChestSetEffect` labels match `RefreshChestSetEffects` by hand - no check reads
   both.

**To play by hand:** whether the Reliquary feels like anything (it is the candidate for a buff);
whether the Regalia's levels feel earned or handed over; whether the set's moment interrupts at a
bad time in a crowd (it takes no input, but it covers the top of the screen for 4 s).

### 8h. A card passed over fades (2026-10-28)

Eric: "i see a lot of the same items in the same game. do i have to unlock more or do we need more
items added to game?" Neither. Relics have no unlocks (`UnlockKind` is spells, wizards and boons),
and 34 relics in 11 sets is a full pool. The cause was the deal: `GetChestItemOptions` excluded only
what the player OWNED, so a card passed over went straight back at full weight, and with commons
weighted 12.5 times a Relic-tier piece the nine commons filled about half of every run's cards.

Now a run remembers what its chests have shown (`ChestOfferMemory`, held by `Node2DGame` and by the
balance bot, which deals its own chests): each showing halves a relic's weight for the rest of the
run (`ChestItemCatalog.ShownOfferFade` 0.5), and the last chest's cards sit the next one out unless
that would leave too few. Simulated over 10,000 runs of 15 chests (the selection code re-implemented in Python, a random picker / one that always takes the rarest):

| | relics shown, of 33 | most-shown relic | chests repeating a card from the last | Relic-tier seen, of 5 | card share C/U/R/Relic |
|---|---|---|---|---|---|
| before | 25.0 / 23.7 | 4.9 / 5.6 times | 4.9 / 5.3 | 1.5 / 1.3 | 45/33/19/4 |
| after | 28.7 / 28.0 | 3.3 / 3.5 times | 0 / 0 | 2.4 / 2.2 | 38/32/24/6 |

Rarity still decides most of it, but rare cards are shown a little more often, so this is a small
power increase at the margins as well as variety. Not done, and worth a look if runs still feel
samey: flatter rarity weights, and a nudge toward pieces of a set the player has started (nothing
steers toward finishing a set today); a reroll or banish on the chest screen.

### 8f. What needs playing

- Whether an elite at 65% feels like "elites carry chests" or like noise. The first elite always
  pays, to teach it; if it still reads as random, the next step is marking the carriers rather than
  raising the chance.
- The potion at its new rate - an event, or a rarity nobody notices?
- Dee with five relics on a phone (it scrolls), and whether one more relic per chest is reason
  enough to play him in a run that opens fifteen.
- The Spirit Mirror's reach and pull at Titan size, and whether a Titan Hollow Star - on any wizard
  - is now too strong: it knots most of the screen.
- The XP clock with an early Signet Ring or Tithe, against `.ai/run-pacing.md` section 8.
