# The spell roster, rethought

Companion to `.ai/spell-variety.md`, which argues *why*. This is the *what*: the whole roster, one
by one, with a distinct identity for each. 24 spells existed when the pass was written; **three more
were built out of it** and are in section 2a.

**The constraint I held throughout:** element tags do not change. The reachability work in
`.ai/passives-and-items.md` balanced twelve elements across spells, boons, items and sets, and
re-tagging spells would invalidate all of it for no gain. Every proposal below changes how a spell
*behaves*, never what it counts as.

---

## 1. What the audit found, beyond the obvious

Three collisions that are not visible until the roster is laid out in one table.

**Ten of twenty-four spells sit at 400–500 range.** Frost Shard, Gale Blade, Glacial Spike, Meteor
Swarm, Molten Shard, Scorching Ray, Shadow Bolt, Chain Lightning, Void Lance and Magic Missile are
all "long". Against them sit six spells at 60–100 (Cyclone Slash, Solar Flare, Toxic Spore Burst,
Arcane Explosion, Spiritual Weapon) and almost nothing between. The roster is bimodal: a ring of
auras on the player, and a cloud of auto-aimed things at half a screen. **The 150–350 band, where
you have to make a decision about distance, is nearly empty.**

**Three spells are chain-jumps.** Shadow Bolt, Gale Blade and Chain Lightning all hit a target, then
hop. Chain Lightning owns that fantasy; the other two are wearing it.

**Three spells are self-centred pulse zones.** Solar Flare, Toxic Spore Burst and Arcane Explosion
all mean "damage appears around me on a timer" and differ only in colour and status rider.

Those three collisions account for most of the sameness. Fixing them is most of this document.

---

## 2. The signature grid

Every spell should be unique on the combination of four axes. Where two spells collide on all four,
one of them is redundant however different its particles look.

- **Aim** — what decides the target
- **Cadence** — what decides when it fires
- **Demand** — what it asks of the player, if anything
- **Payload** — the shape of the damage

Proposed roster. **Bold** is a change from today.

| # | spell | element | aim | cadence | demand | payload | band |
|---|---|---|---|---|---|---|---|
| 1 | Cinderbreath | Fire | nearest | duty cycle | hold the line | **stream of thrown tongues** | close |
| 2 | Cone of Cold | Ice | **facing** | timer | **face the threat** | burst cone | mid |
| 3 | Chain Lightning | Lightning | multi-target | timer | — | **chain jump, damage only** | long |
| 4 | Obsidian Spike | Earth | ground at enemy | timer | — | eruption, no travel | mid |
| 5 | Magic Missile | Arcane+Lightning | nearest | timer | — | projectile | long |
| 6 | Aegis Ward | Metal+Light | self | **recharge on break** | — | shield pool | self |
| 7 | Riptide | Water | fired once, no homing | timer | line it up | piercing line | mid |
| 8 | Molten Shard | Metal+Fire | **random enemy** | timer | — | projectile | long |
| 9 | Thorn Vine | Grass+Poison | **densest cluster** | timer | — | piercing DoT | **mid** |
| 10 | Glacial Spike | Earth+Ice | **random ground nearby** | timer | — | **persistent zone** | mid |
| 11 | Void Lance | Arcane+Darkness | **fixed, slowly rotating** | timer | **position, not aim** | piercing beam | long |
| 12 | Cyclone Slash | Wind | **facing arc** | timer | **face the threat** | **sweep** | close |
| 13 | Fireball | Fire | nearest | long timer | — | heavy blast | long |
| 14 | Frost Shard | Ice+Water | **every dying enemy** | **on kill** | — | **shatter burst** | — |
| 15 | Shadow Bolt | Darkness+Poison | nearest | timer | — | **chain jump, debuffs only** | long |
| 16 | Gale Blade | Wind+Lightning | nearest | timer | — | **boomerang, hits both legs** | mid |
| 17 | Scorching Ray | Fire+Arcane | multi-target | timer | **stay on one target** | **ramping beam** | long |
| 18 | Meteor Swarm | Fire+Earth | **random ground, many** | long timer | — | **barrage** | long |
| 19 | Solar Flare | Light+Fire | self | timer | **stand still to grow** | pulse zone | close |
| 20 | Toxic Spore Burst | Poison+Grass | self | **proximity trip** | **let them surround you** | pulse zone | close |
| 21 | Spiritual Weapon | Arcane+Light | self | continuous | — | orbit | close |
| 22 | Arcane Explosion | Arcane | self | **charges from other casts** | **build the loadout around it** | burst | close |
| 23 | Hunter's Draw | Metal+Wind | nearest | **charge while moving** | stop to loose | volley | long |
| 24 | Black Tentacles | Poison+Earth | **densest cluster** | timer | — | root zone | mid |

Cadence spread afterwards: 15 timer, 2 duty-cycle or continuous, and **five that are not timers at
all** — on-kill, proximity, charge-by-moving, charge-by-casting, recharge-on-break. Today that
number is one.

Range band afterwards: 8 close, 7 mid, 9 long, against today's 6 / 2 / 10.

---

## 2a. The three built out of this pass

The grid above is a rethink of spells that already existed. These three are new, and each exists
because the grid had an empty row - an archetype the genre uses constantly and we had never built.

| # | spell | element | aim | cadence | demand | payload | band |
|---|---|---|---|---|---|---|---|
| 25 | Mirefoot | Water+Grass | behind the player | **distance travelled** | keep moving | trail of pools | close |
| 26 | Kindled Ward | Light | its own, once made | timer, capped by count | place it well | autonomous | mid |
| 27 | Gravewell | Earth+Darkness | **random ground nearby** | **proximity trip** | let them come to it | armed trap | mid |
| 28 | Hollow Star | Darkness | densest cluster | timer | **build around the knot** | **pull, then collapse** | mid |

### Hollow Star - the spell that moves the swarm (issue #64)

Every other spell takes the enemies where it finds them. Hollow Star opens a well on the densest pack
in range, drags everything within 150 px into one knot for 1.6 s, then collapses on an 80 px circle -
so its own hit is modest and most of its value is what the knot does for the area spells beside it.
`VortexPull` had only ever been an Arcane Explosion rider; this is the spell that is about it.

- **Pure Darkness, on purpose.** Darkness, Metal, Grass and Poison had no pure spell, and only a pure
  spell can carry an element to its capstone. This closes Darkness. Sold in the shop (160) rather
  than gated on an achievement, for Riptide's reason.
- **The pull is `Enemy.ApplyKnockback`, re-applied every 0.1 s**, eased near the centre so the swarm
  settles rather than overshooting. Riding knockback is what makes bosses correct for free:
  `KnockbackResistance` scales it, and the Archivist (1.0) is not moved at all.
- **The collapse is smaller than the reach**, so a well that gathered nothing also hits little.
- Draws itself (reach ring, three turning arms, a swelling core); the card icon is
  `hollow-star-dark` from `tools/art/spell_icons.py`. Default archetype evolutions for now.

Verified headless: twelve still enemies in a ring went from a mean 101 px to 45 px from the centre
and all ended inside the collapse; with no well they moved 3 px.

### Mirefoot - the first spell paid for by movement

Bog wells up in the player's footprints and drags at whatever follows. Every other spell in the
game fires on a clock, so standing still and running flat out produce identical output. This one
fires on **distance travelled**: it costs nothing to own if you never move and pays out constantly
if you kite.

Cooldown reduction has to mean something for a spell with no cooldown, so it buys **density** - the
faster your spells recharge, the tighter the pools are laid. That is `TrailWeaver.EffectiveSpacing`,
and level-up cooldown bonuses fold into it the same way.

It is the deliberate counterweight to Solar Flare, which rewards holding still, and the partner of
Hunter's Draw, which charges while moving and looses when you stop.

The pools themselves are `LingeringZone`, which is generic and knows nothing about Mirefoot. Anything
later that wants to leave something on the floor reuses that scene with different numbers.

### Kindled Ward - the autonomous archetype, without an ally

A mote of light that drifts at whatever is nearest and burns it out, then gutters. It is the one
thing no amount of re-pointing an existing spell can fake: every other spell resolves the instant it
is cast, and this one is still working several seconds later somewhere the player is not.

**It is not a companion, and that is a hard constraint rather than flavour.** `.ai/world-and-tone.md`
is explicit that the player has no allies and no escort - everyone who could help is in a cell - and
that everything glowing other than the player is *something you made*. So it has no face, no name
and no loyalty. It is a lit object. A summoned creature with a personality would quietly delete the
premise of the game, and the constraint made the spell better: a drifting ward that is slower than
the player has to be *placed*, so casting it is still a decision about where you are standing.

The cooldown makes one ward; `BaseProjectileCount` is a ceiling on how many may burn at once. The
spell tops itself back up as old ones go out.

### Gravewell - two missing archetypes at once

A pit opened in the ground nearby, which arms, waits, and collapses on whatever steps in.

- **Aim: random ground near the player.** Not thrown at an enemy, not centred on the caster. It is
  put somewhere and the enemies decide whether it was a good spot - area denial rather than aiming.
  The inner 45% of the ring is excluded so a trap never lands underfoot.
- **Cadence: proximity.** The timer only decides when a trap is *placed*. It can sit doing nothing
  for seconds and then resolve exactly when the crowd arrives. A trap that fired on a clock would
  just be a slow bomb.

The blast radius is wider than the trigger radius on purpose: whatever sets it off is caught, and so
is whatever was following it. It cannot trigger while arming, which is what stops a trap dropped
into a crowd from resolving on the frame it lands - the wind-up is the cost of the aim being free.

Mirefoot and Gravewell draw themselves, so neither waited on the art backlog. Kindled Ward draws itself too, and should keep doing so - it is a made light rather than an object.

Cinderbreath was the counter-example and is worth remembering as one: it drew itself at first and had to be rebuilt around a real sprite sheet, because a procedurally filled cone reads as a lit area and never as fire. Drawing your own shape is the right default for a zone, a ward or a trap - something that occupies ground. It is the wrong default for anything the player is supposed to watch travel.

---

## 3. The changes that matter most

Nine spells carry the whole rethink. The rest are re-pointings that cost a line each once
`SpellTargetingMode` is load-bearing (`.ai/spell-variety.md` phase 2).

### Frost Shard becomes an on-kill spell

**Today:** a bolt that flies at the nearest enemy and bursts. It is the fourth thing in the roster
doing that.

**Proposed:** it no longer fires at anything. Every enemy that dies **shatters**, throwing ice
shards into whatever is next to it. Damage scales with the spell level; the spell never picks a
target, because the enemies pick it by dying.

This is the single best trade in the document. It costs one hook — the existing death path in
`Enemy.StartDeath` already runs on every kill — and it produces a spell that feels completely unlike
anything we have: it is *quiet* when you are struggling and *deafening* when the build is working,
which is exactly the feedback curve a bullet-heaven wants. It also makes Ice the "the horde kills
itself" element, which nothing else claims.

### Chain Lightning and Shadow Bolt split the chain between them - BUILT

**What was there:** three spells chain-jumped and were hard to tell apart. Worse, the names were
backwards. Shadow Bolt and Chain Lightning **run the same script** (`scripts/ChainLightning.cs`),
and Shadow Bolt was the better chainer of the two:

- Chain Lightning did not chain at all until level 3. Arc depth was
  `(projectileCount - 1) + arcBonuses`, which is zero at level 1, so the spell named for chaining
  hit exactly one enemy.
- Its `BounceDelay` defaulted to **a full second**. A three-hop chain took over two seconds to
  resolve, by which time the targets had moved and most were dead. The chain never read as one
  event.
- The Lightning element tier separately gives *every* spell a 10/20/35% chance to arc, so by the
  time a player cared about Lightning, their Magic Missile was chaining too.

**What they are now:** they both still chain. They stop chaining the same thing.

**Chain Lightning carries damage.** `BaseArcDepth = 1` so it chains from level 1, `BounceDelay` is
0.12s so a chain resolves as one visible arc, `ChainDamageMultiplier` is 0.9 so hops barely fall
off, and its base damage went 4 to 7 with damage-only level bonuses. It hops *once*, *close*, and
*hard*.

**Shadow Bolt carries debuffs.** Base damage went 3 to 2 and it gains no damage from levelling at
all - every bonus it used to spend on damage now buys chain chance, poison, slow power or
vulnerability. In exchange its chain radius went 240 to 320, its chain chance 0.18 to 0.5, and every
target it touches is left poisoned, slowed **and Vulnerable**. Its real damage arrives as rot, not
impact; its real value is that everything else you own hits the marked target harder.

That is a better answer than taking the chain away from one of them. A shared delivery archetype is
not automatically redundancy - what makes two spells the same is carrying the same payload to the
same place, and splitting the payload fixes it without destroying a spell that already works.

It gives the roster its first genuinely **supportive** spell. Every other spell is trying to kill
things; one whose job is to make the other five better is a build decision no existing slot can
express.

### The level 4 and 8 milestones

Three new evolution sets, and two knobs that had to be built before they could be written.

`SpellEvolutionOption` could grant **arcs** (how far a chain travels) but not **branches** (how wide
it forks), so "the option that makes it split" was not expressible - every splitting upgrade had to
be spelled as a longer chain. `ChainBranchBonus` now exists and folds through
`SpellData.GetChainBranchCountAtLevel`. Separately, `SpellEffect.CritDamage` and
`SpellEffect.Vulnerability` were added so a milestone can raise a spell's crit multiplier or the
depth of the debuff it applies.

- **Chain Lightning** keeps damage as its default and sells splitting as the fork. Forked Surge
  (+2 branches, x0.7 damage) and Cascade (+3 branches, +2 arcs, x0.6 damage) both cost real damage;
  Dense Current and Thunderhead are the branches that refuse to split and hit harder instead.
- **Shadow Bolt** options add or deepen debuffs and almost never add damage. Withering Mark and
  Anathema deepen Vulnerable; Creeping Rot deepens poison and slow; Spreading Dark spreads the
  curse wider for no extra damage at all. Plaguebearer is the single exception, and it pays for its
  damage in poison rather than impact.
- **Gale Blade** left the chain archetype, so its milestones are damage, crit rate, crit damage and
  attack speed - Honed Edge, Keen Wind, Quickening, Executioner's Gale, Tempest Rhythm. It also had
  **one level upgrade across eight levels**, which is now seven.

**One rule caught this in review.** A hybrid spell may reach 2 of each of its elements but never 3 -
only a pure spell may. Every level 4 option here already grants one of the spell's two elements, so
no level 8 option may grant either, because `RegressionChecks` checks every level 4 x level 8
combination rather than only the matching pair. The level 8 options grant no element tags.

### Arcane Explosion charges off your other spells

**Today:** a pulse around the player on a 1.5s timer. Indistinguishable in feel from Solar Flare and
Toxic Spore Burst.

**Proposed:** it has no timer. Every cast of **any other spell** adds a charge; at full charge it
detonates. A loadout of six fast spells sets it off constantly; a loadout of slow heavy spells makes
it rare and huge.

This makes one spell in the game care what the other five are, which is the closest thing to a
"build" the spell system currently has, and it is very Arcane — magic about magic.

### Toxic Spore Burst triggers on being surrounded

**Today:** a pulse on a 2.2s timer.

**Proposed:** it does not fire on a clock. It fires when **enough enemies are within its radius**,
so it rewards letting them close instead of punishing it. Paired with the Armor and shield work, it
gives the close-range band an actual payoff.

### Solar Flare grows while you stand still

**Today:** a pulse on a timer, at 90px.

**Proposed:** the same pulse, but its radius and damage **ramp while the player holds position** and
reset when they move. It becomes the anti-kite spell — the thing you take when you want to stand in
one place and hold it.

Note the deliberate symmetry: Solar Flare rewards standing still, Shadow Bolt rewards moving, and
Hunter's Draw charges by moving and fires when you stop. Three spells that read the same input and
disagree about it is a build decision.

### Cyclone Slash becomes a facing sweep

**Today:** an orbit at 60px, doing the same job as Spiritual Weapon.

**Proposed:** a sweep across the arc **you are facing**. One orbit in the roster is right; two is one
too many, and Spiritual Weapon is the better orbit because its blades persist.

Needs `Player.FacingDirection` (phase 1).

### Gale Blade returns

**Today:** a chain jump. The second one.

**Proposed:** thrown out to a distance and **back to the player**, dealing damage on both legs.
A returning weapon is a persistence type we do not have, and it makes Wind the element that covers
the ground between you and them rather than skipping over it.

**Built (issue #64).** `ElementalBolt.Returning`: the blade flies straight (no homing) to
`ReturnOvershoot` past its target - never less than `MinThrowDistance` x Area - then steers back to
wherever the player now is and is freed on the catch. Unlimited pierce, each enemy struck once per
leg. The first version capped the throw at a fixed reach shorter than the 450 cast range, so a blade
aimed at a far enemy turned round in front of it; the throw is now measured from the target, not
capped. Verified headless against five enemies in a row: five hits out, five back, caught at the
thrower.

### The aim column, built (issue #64)

`Player.FireBoltSpell` takes a `BoltAim`. **Thorn Vine** and **Black Tentacles** aim at the densest
cluster (`FindDensestEnemy`: the in-range enemy with most others within 110 px, scoring at most 48
candidates so a late swarm cannot make one cast quadratic). **Molten Shard** aims at a random enemy
in range. Both fall back to the nearest enemy, and both still only cast when the nearest is in range,
so a spell never fires into an empty field. In a headless run, 3 of 10 Thorn Vine and 4 of 7 Molten
Shard casts left at more than 20 degrees from the nearest enemy.

### Meteor Swarm becomes a barrage

**Today:** a single heavy ground strike on a 5s timer — which is now exactly what the reworked
Fireball does.

**Proposed:** many small impacts scattered over an area, saturating it. Fireball is the sniper
round; Meteor Swarm is the artillery. Same element pair, completely different problem.

### Scorching Ray ramps on a held target

**Today:** a beam that picks targets and deals flat damage.

**Proposed:** damage **ramps the longer it stays on the same enemy**, resetting when it switches.
Our only beam should feel like a beam: the fantasy is burning *through* something, and that requires
the beam to remember.

---

## 4. What deliberately does not change

- **Magic Missile** stays a plain nearest-enemy projectile on a timer. Keep one honest baseline, so
  everything else reads as a departure from a thing the player knows.
- **Chain Lightning** keeps the chain and keeps being the damage one; see above for how it and
  Shadow Bolt divide it.
- **Spiritual Weapon** stays the orbit.
- **Obsidian Spike** stays an eruption with no travel time — that is already a distinct answer to
  "how does damage arrive".
- **Hunter's Draw** is untouched. It is the model the rest of this document is arguing toward.
- **Cinderbreath, Riptide, Fireball, Aegis Ward** were reworked recently and are already on-pattern.

---

## 5. Dependency order

Nothing here is blocked on new art; three of the nine draw themselves.

| needs | spells |
|---|---|
| `Player.FacingDirection` (variety phase 1) | Cone of Cold, Cyclone Slash |
| `SpellTargetingMode` load-bearing (phase 2) | Molten Shard, Thorn Vine, Glacial Spike, Void Lance, Meteor Swarm, Black Tentacles |
| Trigger conditions (phase 3) | Frost Shard, Toxic Spore Burst, Arcane Explosion |
| Nothing — self-contained | Chain Lightning, Shadow Bolt, Gale Blade, Scorching Ray, Solar Flare |

**Suggested first slice:** the four self-contained ones. They need no new machinery, they are four of
the most distinctive changes in the document, and shipping them proves the direction before the
targeting refactor is paid for.

---

## 6. Verification

Same shape as the Cinderbreath probe. Per spell: run headless as a character holding only that
spell, park an enemy where the spell should reach, and assert it loses health — the headless player
cannot move, so anything depending on travel or proximity has to have its condition forced.

Two roster-wide checks worth adding to `RegressionChecks`:

- **No two spells share an entire signature.** The grid in section 2 is the contract; a validator
  over aim, cadence and payload catches the next Shadow Bolt before it ships.
- **Range bands stay spread.** Assert no band holds more than half the roster. The 10-at-450
  collision built up silently, one reasonable-looking spell at a time.
