# Spell variety — what the roster is missing, and what the genre already solved

The complaint that started this: *"Many of the spells in this game are just projectiles launching at
enemies. I want much more variety."*

That is right, but the diagnosis matters because it changes what we build. We are not short of
payloads. We are short of **reasons for the player to care which spell is firing**.

---

## 1. The audit

23 spells. By payload shape (`SpellDamageShape`, resolved in `Player.ResolveSpellClassification`):

| shape | count | spells |
|---|---|---|
| RadiusBurst | 7 | Fireball, Frost Shard, Glacial Spike, Obsidian Spike, Cone of Cold, Meteor Swarm, Arcane Explosion |
| ProjectileHit | 5 | Magic Missile, Riptide, Thorn Vine, Molten Shard, Void Lance |
| ChainJump | 3 | Shadow Bolt, Gale Blade, Chain Lightning |
| PersistentZone | 4 | Solar Flare, Toxic Spore Burst, Black Tentacles, **Cinderbreath** |
| ContactOrbit | 2 | Spiritual Weapon, Cyclone Slash |
| BeamHit | 1 | Scorching Ray |

Six payload shapes across eleven separate firing functions is respectable. The problem is the next
table. By **what decides where the spell goes**:

| targeting | count |
|---|---|
| nearest enemy | 13 |
| the player's own position | 5 |
| ground under an enemy | 4 |
| multi-target | 2 |

**Twenty-two of twenty-three spells aim themselves, on a fixed timer, with no relationship to
anything the player is doing.** That is the sameness. The payload differs; the experience of owning
the weapon does not.

Three findings that sharpen it:

- **There is no facing direction on the Player at all.** Cone of Cold is classified
  `DirectionalCone`, but `Player.FireConeBlast` calls `FindNearestEnemy` like everything else. A
  whole family of weapons is unreachable because the variable does not exist.
- **`SpellTargetingMode` is decorative.** The enum has six values, but it is only ever computed in
  `Node2DGame.ResolveTargetingMode` to build a debug string. The firing functions hardcode
  `FindNearestEnemy` at seven call sites. We have the vocabulary without the machinery.
- **Hunter's Draw is the only exception, and it proves the hook works.** `Player.TickHuntersDraw`
  charges while the player moves and looses when they stop, ticked per frame out of the spell loop.
  One spell in the game reads player behaviour, and it is the one that feels unlike the others.

---

## 2. What the genre actually varies

Mechanics reference only — none of this is a licence to copy anyone's art or assets.

The instructive thing about **Vampire Survivors** is that most of its weapons have nearly identical
payloads ("damage on contact") and still feel wildly different, because the variety is in *where the
thing goes and what decides that*:

- **Magic Wand vs. Fire Wand** — nearest enemy vs. *random* enemy. One word of difference, and they
  play nothing alike.
- **Whip / Knife** — fire in the direction you are facing, so movement is aiming.
- **Axe** — fires upward regardless of enemies; you position yourself under the threat.
- **Santa Water** — drops zones at random nearby spots. Area denial you do not control.
- **Lightning Ring** — hits random on-screen enemies with no travel time at all.
- **Runetracer** — bounces around the screen persistently, aimed at nothing.
- **Shadow Pinion / La Robba** — drop behind you as you move. Kiting *is* the weapon.
- **Clock Lancet** — deals no damage. It only freezes.

**Halls of Torment** adds two things we have nothing like: autonomous **summons** with their own AI,
and **sustained** weapons — the Exterminator's flamethrower being the clearest case, where damage is
applied by presence rather than by a hit. That one is now built; see section 4.

**Brotato** makes melee vs. ranged a real distinction: short-range weapons that force you to close,
which changes the whole play pattern. **Death Must Die** hangs much of its identity on triggers —
on-crit, on-kill, on-dodge — instead of timers.

### The axes, and which ones we use

| axis | what we use | what exists |
|---|---|---|
| **What decides the target** | nearest enemy, own position, ground under an enemy | + random enemy, random nearby ground, player facing, fixed direction, behind the player, densest cluster, whatever last hit you |
| **What decides when it fires** | fixed timer (22 of 23) | + on kill, on crit, on taking damage, ammo and reload, proximity trigger, charge-up |
| **What it asks of the player** | nothing | + be close, stand still, keep moving, face it, be at low health |
| **Payload shape** | **6 kinds — our strength** | + sustained (**done**), returning, ricochet, trap that arms and waits |
| **Persistence** | instant, travelling, zone, orbit, sustained | + autonomous minion, armed trap |

---

## 3. The build order

Ranked by what it costs against what it changes. The first three re-differentiate the spells we
**already own**, which is why they come before new content.

### Phase 1 — Give the Player a facing direction

Derive it from the last non-zero movement input and expose it as `Player.FacingDirection`. Nothing
else in this document is possible without it, and it immediately fixes Cone of Cold, which currently
lies about what it is.

Cheap, and it unlocks the whole facing family: a whip that sweeps where you are heading, a dash
that damages along its path, a shield that only blocks the way you face.

### Phase 2 — Make `SpellTargetingMode` load-bearing

Promote it from a debug string to the thing the firing functions actually read, and add the missing
values:

```
RandomEnemy        // the Fire Wand trick - same payload, completely different weapon
RandomGroundNearby // area denial you do not aim
Facing             // needs phase 1
FixedDirection     // fires the same way regardless, so you position instead of aiming
DensestCluster     // rewards letting them group up
BehindPlayer       // drops in your wake; kiting becomes the weapon
```

After this, a new targeting rule costs one enum value and one switch arm. Then re-point some of the
thirteen nearest-enemy spells so the monoculture breaks up — see section 5.

### Phase 3 — Trigger conditions

An alternative to the fixed timer: fire on kill, on crit, on taking damage, on a proximity trip.
The reactive-boon plumbing added alongside Saltbound Chain and Rimebriar is the same shape — a
condition evaluated in `Player`, a shared cooldown, a visible tell — so there is a working precedent
to copy rather than a system to invent.

### Phase 4 — A close-range band

Nothing in the game rewards being near anything. Cinderbreath is the first spell with a short enough
reach to start this, but it is one spell. A melee band wants two or three more, plus a reason to
survive up close (which the Armor and shield work already provides).

### Phase 5 — Autonomous summons

The biggest single gap and the most work. `SpellEffect.SpawnMinions` is already in the enum and
unused. A minion is the only weapon type that makes the screen feel like it has an ally in it, and
it is the one thing no amount of re-pointing existing spells can fake.

---

## 4. Fire, reworked — the worked example

Done, and it is the template for the rest.

**Cinderbreath** (`scripts/Flamethrower.cs`, `SpellData_Cinderbreath.tres`) replaces Fireball as the
Pyromancer's opener and as the Fire starter in `UnlockCatalog`. It is a sustained cone: lit for 1.5s,
ticking every 0.2s against everything inside a 55-degree, 170px arc, then recharging on the spell's
cooldown. It is a persistent child of the Player, so it travels with the caster.

Three things make it worth copying:

- **Damage by presence, not by a hit.** Nothing else in the roster does this. It is the first spell
  where the question is "am I pointed at the crowd" rather than "did the shot connect".
- **It owns its own duty cycle.** The spell fire loop can only fire discrete casts, so the burn and
  recharge rhythm lives in the node. Any future sustained weapon needs the same.
- **It throws individual tongues.** The first build drew the whole cone in `_Draw` as three shaded
  polygons. Both the drawing and the hit test were correct and it still did not read as fire: a
  filled wedge has nothing in it for the eye to track, so it looked like a lit area rather than
  something being poured. It now emits `FlamePuff` scenes on a 0.09s interval into the arc, each
  carrying its own damage, its own five-frame sprite and its own short life. Drawing a cone is
  cheap; drawing fire is not, and the difference was the whole spell.
- **The animation is still the range indicator.** A puff plays its strip exactly once over its
  flight, and `speed x lifetime` cancels the per-puff jitter, so reach is the spell range by
  construction. A flame that has visibly gone out has stopped hurting things - the
  `GroundSlamAttack` rule, kept without matching two numbers by hand.

**Fireball** becomes what it should have been: the big slow one. Speed 180 to 120, explosion radius
95 to 165, damage 6 to 20, cooldown 2.0s to 5.5s, and it moves from `Starter` to a 190-cost shop
purchase. A five-second cooldown with a blast worth waiting for is a late-game shape, and the game
already had enough fast nearest-enemy lobs.

---

## 5. The re-pointing pass

Once phases 1 and 2 land, these cost a line each and are where most of the felt variety comes from.
Nothing here changes a payload — only what decides the target.

| spell | today | proposed | why |
|---|---|---|---|
| Molten Shard | nearest enemy | **random enemy** | It is our Magic Wand twin. Randomising it makes it a crowd spell instead of a duplicate. |
| Thorn Vine | nearest enemy | **densest cluster** | Pierce wants a line of bodies; aiming at the nearest one actively wastes it. |
| Glacial Spike | ground under an enemy | **random ground nearby** | Turns it into area denial and gives Ice a zoning tool it does not have. |
| Void Lance | nearest enemy | **fixed direction, rotating** | A sweeping beam you position around instead of aiming. |
| Cyclone Slash | orbit | **facing arc** | An orbit that becomes a sweep in front of you is a different weapon for free. |
| Cone of Cold | nearest enemy | **facing** | It is already classified `DirectionalCone`. Make the classification true. |
| Magic Missile | nearest enemy | unchanged | Keep one honest baseline so the others read as departures from it. |

---

## 6. Items — what to take from Vampire Survivors and Magic Survival

### Where our items are now

25 chest items, grouped in `ChestItemCatalog.AllItemIds` under Damage, Defense, Healing, Utility and
Elemental. All 25 are stat modifiers. The only behaviour in the whole item system lives on **sets**
(Bastion of Spikes grants retaliation) and in one conditional (Protective Ward gives extra Armor
while a shield is up). Items got rarity and element tags recently; they did not get anything to do.

### What the two games do

**Magic Survival** runs 175 artifacts across five rarity tiers — Normal 23, Rare 36, Epic 52,
Special 35, Legendary 29 — and **collecting a full set of 3 to 4 required artifacts grants a new
passive effect for the run**. That is our Full Set Enchantment, independently arrived at, which is
good evidence the mechanic is sound. The lesson is less the count than the *shape*: a deep pool
where most items are ordinary makes the rare ones feel like finds.

**Vampire Survivors** has about 34 passive items, and most of them are the boring flat stats you
would expect — Spinach for damage, Empty Tome for cooldown, Candelabrador for area, Bracer for
projectile speed, Wings for movement. Taken alone they are exactly what our 25 already are. What
makes them matter is the structural trick:

> **Every passive item is the evolution key for a specific weapon.** Magic Wand plus Empty Tome
> becomes Holy Wand. Fire Wand plus Spinach becomes Hellfire. Axe plus Candelabrador becomes Death
> Spiral. The evolution needs the weapon at max level, the passive item held, and a boss chest.

That single rule turns every stat stick into a build decision, retroactively, without changing what
any of them do.

### The four ideas worth taking

**1. Items as evolution keys — the big one.** We have `SpellEvolutionCatalog` with branch choices at
levels 4, 6 and 8, and we have 25 chest items, and the two systems have never touched. Gating or
unlocking specific evolution branches behind specific held items would make a chest item
build-defining rather than a number, and give the player a reason to keep something they do not
otherwise need. It also answers a live problem: evolution branches are currently offered blind at
level-up, so the player cannot plan toward one. An item you are carrying is a plan.

Worth being careful here — VS requires the item *and* a max-level weapon *and* a boss chest, which
is three gates. Ours should be one: hold the item, and the branch appears in the level-up offer.

**2. A curse item.** VS's Skull O'Maniac makes the run measurably harder — faster, more numerous
enemies — and pays for it in rewards. We have a difficulty selection and a Luck stat but nothing the
player opts into mid-run. A single Relic-rarity item that raises enemy count and speed while raising
drop quality would be the first item in the game that is a *decision* rather than an upgrade.

**3. Items that change a rule, not a number.** The cheapest way to make 25 items feel like 25
different things. Candidates, all implementable against systems that already exist:

| item | effect | hooks into |
|---|---|---|
| Hoarfrost Nail | your slows also root for 0.3s | `Enemy.ApplySlow` |
| Bitter Draught | damage-over-time ticks twice as fast for half as long | `Enemy.ApplyPoison` |
| Duellist's Chalk | the first hit of each spell cycle always crits | `Player.DealDamageToEnemy` |
| Widow's Lens | spells prefer the enemy with the lowest health instead of the nearest | phase 2 targeting |
| Gravebloom | enemies that die inside a persistent zone leave a smaller one behind | `ElementalPulse` |
| Cracked Prism | one extra projectile, but each deals 30% less | existing amount and damage multipliers |

Note the last one: a genuine trade rather than a strict upgrade. We currently have none of those,
and they are what makes an item roster feel like it has opinions.

**4. Partial set bonuses.** Magic Survival's sets are 3 to 4 pieces. Ours are four-piece and
all-or-nothing, which means three pieces of a set is worth exactly nothing and the player cannot
tell whether they are close. A 2-piece taste of the effect would make progress toward a Full Set
Enchantment legible without weakening the payoff.

### What not to take

Magic Survival's 175 artifacts is the wrong target. Our problem is not that 25 is too few — it is
that all 25 do the same kind of thing. Twenty-five items where six change a rule would feel far
deeper than a hundred that all add percentages, and it is a fraction of the work.

---

## 7. Verification

`dotnet build` validates none of this. Per phase:

- **Facing direction**: a headless probe asserting `FacingDirection` survives a frame with no input
  and does not reset to zero, which is the obvious bug.
- **Targeting modes**: a `RegressionChecks` assertion that every spell id resolves to a targeting
  mode the firing loop actually implements — the failure today is that the enum and the code
  disagree silently, and it would recur.
- **Cinderbreath and anything sustained**: the probe pattern used when it landed — run the game
  headless as the owning character, confirm exactly one instance is parented to the Player, that it
  re-aims (rotation changes from its default), and that enemies inside the cone lose health.
- **Items as evolution keys**: a validator that every branch gated behind an item names an item that
  exists, and that no branch is gated behind an item the player cannot obtain on that chapter.
