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
- Darkness: -10% / -20% / -35% incoming damage.
- Light: heal 3% / 6% / 10% of damage dealt.
- Grass: +1 / +2 / +4 HP per second regeneration.
- Earth: +20 / +50 / +100 max HP.
- Wind: +10% / +20% / +35% move speed.
- Lightning: +10% / +20% / +35% chance to chain a bolt to a second enemy on hit.
- Poison: +2 / +4 / +8 stacking-resistant damage over time per tick for 3 seconds on hit.
- Metal: -1 / -2 / -4 flat damage taken per hit.
- Water: -5% / -10% / -18% spell cooldowns.

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
every spell and wizard is obtained, and every unlockable has exactly one source:

- **Starter** — the four signature spells above (Fireball, Cone of Cold, Chain Lightning, Obsidian
  Spike), Magic Missile, and three passives (Aegis Ward, Stone Bulwark, Blur). Eight candidates
  against three level-up slots.
- **Achievement** — 15 spells, each tied to one achievement id. Promoting those four spells to
  starters vacated four achievement rewards, which were refilled from the shop: First Blood now
  grants Arcane Explosion, Elementalist grants Cyclone Slash, Ice Adept grants Glacial Spike, and
  Earth Adept grants Black Tentacles (with Ruins Delver taking Spiritual Weapon).
- **Purchase** — 9 spells bought with Arcane Energy in the Arcane Codex.
- **Discovery** — found in the world. Reserved; no entries yet.

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
- Time-based spawns.
- Increasing density.
- Elite enemies and minibosses.

### Meta Progression
- Permanent upgrades.
- Unlockable characters.
- Spell mastery.
