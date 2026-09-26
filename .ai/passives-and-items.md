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
existing art rather than new art. Picking one does not consume a slot, which means a level-up offer
mixing spells and boons is a genuine choice between "more damage" and "more of everything else".

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


## 5. Full Set Enchantments grant tags

### Partial bonuses on the three-piece sets

Ten sets exist: six of three pieces, four of two. **The three-piece sets now pay a taste of their
effect at two pieces; the two-piece sets stay all-or-nothing.**

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
twice. `SynergyDetailScreen` lists the partial tier *before* the full one, so a player two relics in
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
