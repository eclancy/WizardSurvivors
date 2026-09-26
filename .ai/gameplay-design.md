# Gameplay Design

## Core Loop
1. Move to avoid enemies.
2. Auto-fire spells.
3. Collect XP.
4. Level up and choose upgrades.
5. Survive waves.
6. Die -> meta progression -> retry.

## Systems

### Spell System
- Each spell is a Resource.
- Properties:
  - damage
  - cooldown
  - projectile scene
  - modifiers
- Supports one or two element tags per spell, and **never the same element twice**. A spell's base
  weights are capped at 1 per element; `RegressionChecks.ValidateElementTags` enforces it.
- Element counts create threshold bonuses at 2, 4, and 6 owned instances, summed across the six
  equipped slots (passives included).
- So a hybrid contributes 2 total weight (1 + 1) and a pure spell only 1. What a pure spell gets
  back is **attunement**, below.
- Canonical elements: Fire, Ice, Arcane, Darkness, Light, Grass, Earth, Wind, Lightning, Poison, Metal, Water.

### Element Threshold Balance
- Fire: +10% / +20% / +35% damage to nearby enemies.
- Ice: -10% / -20% / -35% enemy move speed for 2 seconds on hit.
- Arcane: +10% / +20% / +35% XP gained.
- Darkness: 6% / 12% / 20% chance to evade a hit entirely.
- Light: heal 3% / 6% / 10% of damage dealt.
- Grass: +1 / +2 / +4 HP per second regeneration.
- Earth: +20 / +50 / +100 max HP.
- Wind: +10% / +20% / +35% move speed.
- Lightning: +10% / +20% / +35% chance to chain a bolt to a second enemy on hit.
- Poison: +2 / +4 / +8 stacking-resistant damage over time per tick for 3 seconds on hit.
- Metal: +8% / +16% / +28% Armor.
- Water: -5% / -10% / -18% spell cooldowns.

**Water was the thinnest element in the game until Riptide.** Frost Shard was its only spell
carrier and carries Ice as well, so four of Water's seven tag sources were boons - meaning the
element was reachable only by spending most of six shared boon slots on it. The reachability check
could not see that, because it asks whether six is reachable and not what reaching it costs.
**Riptide** is the answer: a pure-Water surge that does not home, pierces a line of enemies and
drags what it passes. Being pure, it is attuned, which is the compensation a single-tag spell gets.
It doubles as the Water wizard's signature spell in the campaign roster.

### Status effects

Four debuffs exist, and all four are **stacking-resistant in the same way**: the strongest magnitude
and the longest remaining duration win, and neither adds to the other. Two spells hitting one target
must never multiply into an execute.

| status | applied by | what it does |
|---|---|---|
| Slow | `Enemy.ApplySlow(multiplier, duration)` | scales move speed; a multiplier of 0 is a root |
| Poison | `Enemy.ApplyPoison(tick, duration)` | damage over time |
| Shock | internal to `Enemy` | brief stagger on a lightning hit |
| **Vulnerable** | `Enemy.ApplyVulnerable(bonus, duration)` | **the target takes more damage from every source** |

**Vulnerable** is the newest and the only one that makes the rest of the loadout better rather than
doing something itself. It is capped at **+60%** on the enemy (`Enemy.MaxVulnerability`), not per
application - it multiplies every damage source the player owns, so an uncapped version would scale
with the whole build rather than with the spell that applied it.

It is a status on the *enemy* rather than a buff on the player on purpose: it is visible on the
target, it expires on its own, and everything benefits from it, including other spells, retaliation
boons and chest-item effects. `Player.DealDamageToEnemy` applies it after mitigation-free damage
assembly so it compounds with crits rather than replacing them.

### Attunement
A spell that names **exactly one** element is *attuned* to it, and deals more damage the deeper that
element is stacked: **+20% / +45% / +80%** at that element's tier 2 / 4 / 6. Hybrids get nothing.

This is the whole trade. A hybrid buys breadth immediately; a pure spell buys depth, and only pays
out once the player has committed. It is read from the spell's *current* weights, so it survives
evolution — see below.

### Element growth through evolution
Levelling used to do nothing at all for element counts. Now **every** spell's level 4 and level 8
evolutions can carry an element gain, under one governing rule:

> **A spell never names more than two distinct elements, ever.**

That rule is what keeps the pure/hybrid trade honest, and it splits the two apart:

| | level 4 | level 8 | ceiling | cost |
|---|---|---|---|---|
| **Pure** (1 element) | deepen it, **or** branch to a second | deepen it | 3 of its element, or 2 elements | free |
| **Hybrid** (2 elements) | deepen element A | deepen element B | 2 + 2 | +12% cooldown per gain |

- **Only a pure spell can reach 3 instances of one element,** and only a pure spell can branch — a
  hybrid has both of its element slots filled already, so its evolutions can only deepen what it
  carries. Together with attunement, that is what a pure spell gets for its halved base weight.
- **Branching is a level 4 decision, made once.** Allowing it at level 8 as well let a pure spell
  reach three elements; by the ascension tier a spell's identity is settled anyway.
- **A hybrid buys its gains.** A pure spell's deepening is compensation and is free; a hybrid is
  owed nothing, so each gain costs `SpellEvolutionCatalog.AttunementGainCooldownCost` (+12%
  cooldown). Without a price every hybrid would take the tag every time and depth would stop being
  a decision. Cards state the price.
- Untagged options remain at every milestone, so a straight stat upgrade is always on the table.
- The fifteen spells with no hand-written evolution block derive their gains in
  `SetupDefaultArchetypeEvolutions` from whatever elements they already declare, so this needs no
  per-spell authoring to stay complete.

`RegressionChecks.ValidateEvolutionElementCeiling` checks both ceilings across every level 4 × level
8 pairing — they are properties of the combination, not of either option alone. **It only covers the
`.tres` spells**; the code-built passives in `Player.CreateDefensiveSpellData` are not loaded by the
validator and are checked by hand.

**All twelve elements can now reach their tier-6 capstone**, and at a uniform price: three spells
carrying the element, taken to level 8, spending both evolution picks on tags rather than stats.
Water is the tightest at exactly 6, having only three carriers.

### Armor - the one damage-reduction stat

**Armor is a percentage, it is additive, and it is capped at 75%** (`Player.MaxArmorPercent`).
Every source of damage reduction in the game feeds it: the Metal element, chest items, a character
bonus, and - while they still exist - passive spells.

It replaced three stacked mechanisms that each needed their own explanation: a Darkness percentage,
a chest percentage, and a flat subtraction. Additive rather than multiplicative because additive is
the version a player can do in their head - two sources of 10% is 20%.

**Flat damage reduction is gone and must not come back.** An ordinary enemy deals
`ContactDamage = 1`, so a single point of flat reduction deleted the entire basic horde - and the
first Metal tier granted exactly that. No amount of tuning fixes it; 1 minus 1 is 0 at any scale.

Two consequences worth knowing:

- **A hit always lands for at least 1.** Armor reduces damage, it never deletes it, or flat
  reduction returns through rounding. The cost is that Armor does nothing against one-damage chip;
  if that should change, the fix is raising contact damage, not softening the floor.
- **Darkness moved to evasion.** It was a second percentage reduction doing almost the same job as
  Metal's. Avoidance is a different question - "did it hit me" rather than "how hard" - and needs
  no second explanation standing next to Armor. It also rescues evasion, which otherwise existed
  only through the Blur passive and would be orphaned when passive spells are removed.
- **The Geomancer's identity inverted.** Its bonus was flat on purpose, to blunt swarm chip while
  leaving boss hits dangerous. Percentage Armor does the reverse. The trade was made knowingly.

### Starting Characters
- Four playable characters, each opening on one element and one basic spell.
- A starter spell carries **exactly one** element weight, so a character begins the run already
  pointed at a single element's thresholds rather than splitting weight across two. This is why
  Magic Missile (Arcane + Lightning) is not a starter.
- No two characters open on the same element. `RegressionChecks.ValidateStartingCharacters`
  enforces both rules.

| Character | Element | Starting spell | Shape | Passive |
|---|---|---|---|---|
| Pyromancer | Fire | Fireball | Slow lob, explodes on impact | +10% spell damage |
| Frostweaver | Ice | Cone of Cold | Cone in front, slows what it touches | +0.5 HP/sec |
| Stormcaller | Lightning | Chain Lightning | Arcs between nearby enemies | -10% cooldowns |
| Geomancer | Earth | Obsidian Spike | Erupts under the target, no travel time | -1 damage per hit taken |

- **A starting spell must have a shape of its own.** The four wizards previously opened on
  purpose-built basics - Ember, Icicle, Spark and Stone Shard - which were all the same spell: a
  bolt that flew at the nearest enemy, differing only in colour and a small rider. Four wizards
  that all opened by holding a direction and watching a dot travel made the choice of wizard read
  as a palette swap, so those four spells were deleted and the slots handed to spells that already
  had a shape. A lob, a cone, a chain and an eruption are four different opening problems.
- Obsidian Spike was retagged from Earth + Darkness to pure Earth as part of that swap, since a
  starter must carry exactly one element. Its Darkness half was flavour taken from the word
  "black"; the change also separates it from Glacial Spike, which is the Earth/Ice hybrid.
- Test Wizard remains a development entry that picks its own loadout, and is exempt from both rules.

### Unlock Economy
Nothing is free except the opening hand. `scripts/UnlockCatalog.cs` is the single authority on how
every spell, boon and wizard is obtained, and every unlockable has exactly one source:

- **Starter** — the four signature spells above (Fireball, Cone of Cold, Chain Lightning, Obsidian
  Spike), Magic Missile, and four boons (Iron Rivets, Tidewater Flask, Lantern Oil, Mossgrown
  Charm). Nine candidates against three level-up slots.
- **Achievement** — 15 rewards, each tied to one achievement id. Fourteen spells and one boon
  (Untouchable grants Umbral Veil, which is evasion — the reward matches what earning it proved).
- **Purchase** — 2 spells and 8 boons bought with Arcane Energy in the Arcane Codex.
- **Discovery** — found in the world. Reserved; no entries yet.

`RegressionChecks.ValidateBoonSources` holds the same rule for boons that `ValidateUnlockCatalog`
holds for spells: exactly one source each, and never the same element twice on one boon.

### Chest items - rarity and tags

Items come in four rarities, and rarity does three jobs that move together: how often an item is
offered, how large its effect is, and **whether it carries an element tag**.

| Rarity | Offer weight | Element tags |
|---|---|---|
| Common | 1.0 | none |
| Uncommon | 0.55 | none |
| Rare | 0.22 | one |
| Relic | 0.08 | two |

Tying tags to rarity is what keeps element weight a budget. Handing a tag to all twenty-five items
would make thresholds arrive early for reasons the player could never see.

The offer roll is weighted sampling without replacement rather than a flat shuffle - each candidate
draws a key of `random^(1/weight)` and the highest win - so one roll cannot offer the same item
twice, and a Relic is rare rather than impossible.

**A completed Full Set Enchantment grants two tags.** It is the largest commitment the item system
asks for, so it pays the largest tag reward, and it is the right home for the scarcest elements.

**Neither counts toward `ValidateElementReachability`.** That check asks whether an element can be
reached *reliably*, and items depend on what the chests happen to offer. Items and sets sit on top
of the guaranteed floor that spells and boons provide - they are never the floor.

### Boons
Permanent, never levelled, and held in **six slots of their own** (`Player.MaxBoonSlots`) beside
the six spell slots. A boon is taken once and lasts the run.

- **They do not compete for a spell slot.** That is what separates them from the passive spells
  they replaced, and it means a level-up with a full spell loadout still has something to offer
  that is not a swap.
- **They carry element tags**, which is how the thinner elements reach their thresholds at all.
  The roster is deliberately weighted toward Metal, Water, Light, Grass and Darkness, because
  those five got most of their carriers from passive spells.
- **They never level**, so each has to be worth taking the moment it is offered. There is no level
  8 to borrow against.

**Passive spells are gone.** All eleven (`PassiveSpellEffect` and its subclasses) were removed at
save schema 9. What went with them, and where it landed:

| Was | Now |
|---|---|
| Stone Bulwark, Thornmail armour | Armor, from Metal and boons |
| Blur evasion | Darkness element, and Umbral Veil |
| Fortune's Favor / Guardian Vines luck | Wishing Coin |
| Aegis Ward shields | **Aegis Ward, now an active spell** — absorbs, breaks, recharges on its cooldown |
| Retaliation, on-hit reactions | **Saltbound Chain and Rimebriar** - two boons that carry behaviour |

That last row was the honest gap for a while: every boon was a number, so a defensive build was a
bigger health bar and nothing else. Two boons now answer it, and they do it the way the row
predicted - as boons carrying behaviour rather than spells taking a slot.

- **Saltbound Chain** (Water, Metal) damages everything nearby when the player is struck. It was
  the third identical +8% Armor boon before this, which is what unifying damage reduction had
  quietly turned it into.
- **Rimebriar** (Ice, Grass) slows everything nearby when the player is struck.

Both fire on being **struck**, not on being hurt - the trigger sits before the armour and shield
steps, because reacting less the better your defence is backwards. Both share one cooldown and one
radius, since contact damage ticks several times a second and an unthrottled reaction is just a
permanent aura again. See `.ai/passives-and-items.md` section 3a for the audit behind them.

`SaveData.Migrate()` is **stepwise** as of schema 9 — each version's changes are gated on the
version they were introduced at. It had to become stepwise: the old single-block form would have
re-run the pre-8 grant on a schema-8 save and handed it content it never earned.

Every non-starter carries a `LockedHint`, so a locked spellbook page or character card says how to
obtain it rather than showing `???`.

The final chapter is gated on `GlobalStatsManager.IsCampaignComplete` — every spell recovered and
every wizard freed. Chest item sets deliberately do not gate it.

**Adding a spell or a character now requires a `UnlockCatalog` entry.**
`RegressionChecks.ValidateUnlockCatalog` fails startup validation without one, and cross-checks
that achievements and the catalog agree about what grants what. A spell with no entry is locked
rather than free — the safe direction, and visible.

**Save migration.** Schema 8 introduced this. Before it every spell was unlocked by default, so
`SaveData.Migrate` credits any pre-8 save with the old default set and the full starting roster;
nobody loses access to something they already had. The main menu's two-press *Reset Progress* is
how the campaign is played from the start on a save that has been grandfathered.

### Level-up charges

Four tools that let the player argue with the level-up roll instead of only accepting it. All are
bought in the shop, so a player who buys none sees exactly the screen the game shipped with.

| charge | scope | what it does |
|---|---|---|
| Reroll (*Fate Fracture*) | **per level-up** | new cards |
| Ban (*Proscription*) | per run | strikes a spell from the offer pool for the rest of the run |
| Save (*Hoarded Insight*) | per run | defers this level-up, buying an extra pick at the next one |
| Augury | per run | names a spell that the next level-up offer must contain |

**Rerolls refill every level-up; the other three do not.** That is the whole reason they feel
different. A reroll costs nothing to hold, so it is a tactical button. A ban, a saved level-up and
an augury come out of a pool that never comes back, so spending one is a decision about the run.

Four rules that are easy to get wrong and are worth not re-deriving:

- **A ban does not cost the player their pick.** The struck spell leaves the pool and the offer is
  rebuilt, so they still choose from a full set of cards that level. Banning the same spell twice
  fails without spending a charge.
- **The augury does not re-roll the current cards.** Its promise is about the *next* level-up;
  rebuilding the offer would make it a reroll wearing another name. That is why `LevelUpMenu` has
  `RefreshCharges` separate from `SetOptions`.
- **The augury is one-shot.** `Player.guaranteedNextOfferSpellId` is consumed by the very next
  offer and cleared even if the spell can no longer appear - the charge is already spent. A
  permanent weighting would be a different feature.
- **Saving never grants a level.** `Node2DGame.extraPicksPending` reopens the menu after a pick
  closes, so the player picks twice at one level rather than levelling twice. Cashing the bank in
  happens at the *next* level-up, not when it was banked, or the menu reopens instantly in a loop.

The augury picker lists `Player.GetAuguryCandidates()`, which runs the real candidate pass rather
than the spell catalog - so it can never promise something the roll would not have produced: a
locked spell, a maxed one, or one the player already banned.

### Spellbook Curation
A spell you own can be **set aside** so it stops being offered at level-up — toggled on its own
spellbook page, not on a separate screen, since that page already shows what you own. Reversible at
any time; restoring is always free.

Two bounds, for different reasons:

- **The floor is a design bound.** `GlobalStatsManager.MinimumOfferablePool` (12) is the fewest
  spells the pool may ever hold. Below that every level-up starts showing the same three cards.
- **The slots are an economic bound.** Each removal needs a *Redaction* bought with Arcane Energy
  (80, +40 each), so curation competes with the stat upgrades.

Capacity is `unlocked spells - 12`, so it is **zero on a fresh save** and grows with the book. That
is deliberate: with 8 spells there is nothing worth pruning, and the shop row says so rather than
reading as maxed. At a full 36 spells capacity reaches 24, but the escalating cost is the real
brake long before that.

Curation never touches a spell the player is already carrying — the filter sits only in the
"not currently equipped" branch of `Player.GetLevelUpOptions`, so a spell set aside mid-campaign
still levels normally in a run that already picked it up.

### Achievements
An achievement is **measured, not asserted**. Each one declares a single
`Measure(AchievementContext) -> float` plus a `Target`, and both "am I done" and "how far am I"
derive from that one function — so completion and progress can never drift apart, which two
independent lambdas would eventually do.

Conditions are written against `SaveData.Lifetime` rather than the finished run wherever possible.
That is what lets the main menu show `6:12 / 10:00` instead of the word "In Progress": there is no
current run in the menu, so a run-only condition could never report anything there.

`scripts/RunEvents.cs` records what happens *during* a run — kills by enemy type, elites, chests,
relics, revives, sites, spell evolutions, and **peak** element counts. The peak matters:
`RunResult.ElementCounts` is an end-of-run snapshot, so a build that reached 6 Fire and swapped away
at level 18 would otherwise look like it never got there. `LifetimeStats.Absorb` folds a finished
run in **before** achievements are evaluated, or every one of them would fire a run late.

Rewards are a spell, a wizard, Arcane Energy, or any combination, and the game over screen reports
what was earned. Boss achievements match boss ids **exactly**; they used to match by substring
against terms like "forest", so an id containing another's keyword granted the wrong spell.

### Enemy Attack Patterns
Beyond the plain chaser, an enemy may carry a behaviour. Every one of them telegraphs before it
lands: a wind-up the player can read is the contract, and an attack without one is indistinguishable
from damage arriving at random. `AttackTelegraph` is the shared wind-up clock; `GroundSlamAttack`
is the shared "warning ring, then everything inside it is hit" on top of it.

- **Ranged** (Cultist, Skull Sentry) — holds range and fires a slow, dodgeable bolt.
- **Lunger** — plants, shows the line it will charge, then dashes far faster than it walks, and is
  left winded. Backs off afterwards rather than settling into melee, so it stays hit-and-run.
- **Slammer** — closes, plants, grows a ring, and hits everything inside it. Its threat has a
  radius, so backing off one step is not enough.
- **Exploder** — rushes in and detonates. Killing it during the fuse cancels the blast, and a
  detonation pays no XP, so bursting it down is always the rewarded play.
- **Summoner** — hangs back and calls minions. No attack of its own; it is the one enemy that
  makes the player choose a target. Capped both on live minions (performance) and on lifetime
  summons (so it cannot become an XP fountain).

### Upgrade System
- Level-up choices.
- Stat boosts.
- Spell modifiers.
- Element previews.
- If spell slots are full, new-spell choices enter a replace flow; players can also remove an owned spell or skip the level-up choice.

### Enemy Waves

Time-based spawns with increasing density, plus elites and minibosses. Three rules shape *where*
they arrive, and all three exist to answer the same complaint: the horde used to collect into a wad
behind the player instead of surrounding them.

- **Spawns are biased toward the player's heading** (`SpawnForwardBias`). A uniform ring is what
  produced the wad: spawns land evenly on a circle but the player only ever moves one way, so
  everything spawned ahead gets walked past and joins the tail, and nothing replenishes the front.
  The steady state of a uniform ring plus a moving player is always a comet. The bias eases back to
  uniform as the player slows, because a standing player has no "ahead". Measured over three runs
  of a straight-line flee: enemies behind roughly halved, enemies ahead unchanged.
- **The spawn radius is measured to the edge of the visible rectangle** per bearing, not a flat
  distance (`SpawnUsesScreenEdge`, `SpawnEdgeMargin`, `SpawnEdgeDepth`). A flat radius cannot work
  on a 720x1280 viewport: the screen edge is 288 units away to the side and 512 above, so one
  number is either inside the screen vertically or a long walk horizontally. The old flat 250 was
  inside the screen in every direction — measured, **about a quarter of all enemies appeared on
  camera out of nothing**, which is now essentially none. It also means the gameplay zoom can
  change without retuning this, which it has twice.
- **Some waves arrive as formations** rather than as independent singles (`scripts/SpawnFormations.cs`).
  Four shapes — Arc (a wall across your path), Column (single file, arriving over several seconds),
  Pincer (both flanks at once), Ring (surrounded, and deliberately rare and late). Which shape is
  available depends on run time: an Arc is a shape you can walk around, a Pincer takes away one of
  the two directions you would walk, a Ring takes away all of them.

**Formations get reserved headroom.** The ordinary trickle stops at `MaxEnemies - FormationMaxSize`
and the top of the budget belongs to waves. Without that the two compete and the trickle always
wins — it spawns constantly, a wave only every fifteen seconds — so formations were being clamped
down to three members, too few for any shape to survive. The feature degraded back into the scatter
it replaced, and it did so exactly when the arena is densest, which is when a shape matters most.
A formation that finds no room **retries rather than burning its slot**, for the same reason.

**The horde's pace ramps** (`EnemyMoveSpeedMultiplier` 0.60 to `EnemyMoveSpeedMultiplierLate` 0.85
over five minutes). A flat 0.55 put the fastest ordinary enemy at 102 against a player at 220, so
nothing could ever close and every steering fix was compensating for a deficit rather than removing
it. Ramped rather than raised flat, because the opening was deliberately thinned out and a 36%
speed increase from second zero would have undone that.

**A caution about that number.** Earlier in this work, raising enemy speed 1.6x was measured as by
far the largest lever on kiting - plain pursuit at that speed matched everything the interception
and encirclement work achieved. **That finding is now stale.** Re-measured after the spawner, cap,
size and steering changes all landed, the same speed increase moves the needle only modestly: at
the end of the ramp, against a circling player, contact +7%, mean distance -12%, hits taken +11% -
all directionally right, all inside the run-to-run spread of the control arm. The other work had
already closed most of the gap. The ramp is kept because it restores the design intent of enemies
that can plausibly close, not because it is transformative on its own.

### Meta Progression
- Permanent upgrades.
- Unlockable characters.
- Spell mastery.
