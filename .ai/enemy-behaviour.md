# Enemy behaviour — the contract, and the plan for the roster

How an enemy becomes more than a chaser: what the engine already gives you, what has to be built,
which behaviour each enemy in the cast should carry, and the rules any new one has to satisfy.

`gameplay-design.md` describes the five behaviours that exist today. This is the framework under
them and the plan for the rest.

---

## 1. The rule that shapes everything: not every enemy gets a telegraph

The brief was "different enemies should do different things", and every enemy in the cast should
indeed be distinguishable by what it *does*. But a wind-up is not the way to say that for all of
them, and giving every type one would make the game less readable, not more.

**Attention is the budget.** Seventy enemies share one screen. A telegraph works because the player
can pick it out of the crowd and answer it; if a third of the crowd is telegraphing at once, none of
them reads and the tells become texture. The current split is six plain chasers to five telegraphed
types, and that ratio is roughly right. The fix is not to telegraph everything — it is to give the
plain ones **movement character**, which costs the player no attention and still makes a wolf
unmistakably not a skeleton.

So behaviours come in three tiers, and most of the roster belongs in tier 0:

| Tier | What it is | Costs the player | Good for |
|---|---|---|---|
| **0 — Movement character** | Always on, no wind-up, no attack of its own. Circling, skirmishing, planting, drifting. | Nothing. It reads peripherally. | Most of the cast |
| **1 — Telegraphed attack** | Plants, shows a tell, resolves once. | Attention, and a reaction. | A minority |
| **2 — Death behaviour** | Fires when killed. Splitting, bursting, leaving a hazard. | Nothing up front, a surprise after | A handful |

An enemy may hold tier 0 and tier 2 together cheaply. Tier 0 plus tier 1 is the expensive
combination and should be deliberate.

---

## 2. What already exists — do not rebuild any of this

| Piece | What it gives you |
|---|---|
| `Enemy.AdjustSteering(chaseDirection, distanceToPlayer)` | The steering hook. **The returned vector's length is a speed multiplier** — unit to walk, longer to dash, `Vector2.Zero` to hold exactly still (which also suppresses the separation nudge, so a planted enemy cannot drift off its own tell). |
| `AttackTelegraph` | The wind-up clock: cooldown → wind-up → resolve exactly once. Deliberately not a Node — a swarm scene should not pay for a child node per attacker. |
| `GroundSlamAttack` | Warning ring, then everything inside it is hit. The ring drawn and the radius hit are the same number by construction. |
| `Enemy.StartDeath()` | Virtual. Drops rewards, leaves the `enemies` group and zeroes collision *in the same frame*, then plays `death`. The hook for every tier 2 behaviour. |
| `Enemy.ResetForRespawn()` | Virtual. Called when the recycler moves an abandoned enemy back to the ring. |
| `_Draw()` | `QueueRedraw` already runs every physics frame, so a tell costs only the draw. |
| The `attack` animation | The art contract already reserves **`attack`, 6 frames, non-looping** on every sheet. The slot for every wind-up exists. |

---

## 3. The gap nobody has closed: `attack` is never played

`Enemy` plays exactly one animation by name — `death`. Every telegraphed enemy in the game draws a
geometric tell and stands in its `moving` loop while doing it. The art shipped (278 frames across
the roster); the code has never asked for it.

This is the highest-value single change in this document. It improves all five existing behaviours
at once, it needs no new art, and "archers kneel and wind up" is *mostly just this* — the kneel is
the `attack` animation, and the wind-up clock already exists to drive it.

---

## 4. Phase 0 — plumbing, before any new behaviour

**0.1 Play `attack` on the wind-up.** A small `Enemy.PlayAttackAnimation()` driven from
`Beat.Started`, returning to `moving` on resolve or animation finish. It must fall back silently
when a sheet has no `attack`, exactly as `StartDeath` already does for `death` — otherwise adding a
behaviour to an enemy whose art is not redrawn yet breaks it.

**0.2 `EnemyCatalog`.** One authority for type → scene, spawn share, first-spawn time, health
multiplier, elite eligibility, behaviour tier. Today that is twenty `[Export]` fields plus a
thirty-line switch in `Node2DGame.SelectEnemyForCurrentStage`, and adding an enemy means editing
five places that can disagree. This is the same shape as the four disagreeing stage tables
`StageCatalog` replaced, and the same fix.

**0.3 `LevelTilePainter.TryFindPlaceableNear(worldPos, radius)`.** Rejects wall, water, ice, lava
and pit. Blink and Split both need to put something somewhere legal, and the only placement check
today is `Node2DGame.IsWallPosition`, which is maze-stages-only and private. The campaign plan
already called for this helper for its discovery sites; it is the same helper.

---

## 5. Phase 1 — tier 0 behaviours, no new primitives and no new art

Every one of these is an `AdjustSteering` override and nothing else.

| Behaviour | Who | What it does | Why it reads |
|---|---|---|---|
| **Circler** ("wolf") | FastEnemy | Closes to a ring, then orbits at a per-enemy direction, tightening as it goes. Commits inward when the player stops, or when a packmate is already in contact. | A pack that wheels around you rather than queueing behind you |
| **Skirmisher** | SlowEnemy | Advances, touches, then backs off instead of grinding. Hit-and-run with no dash. | Its damage comes in pulses you can count |
| **Bulwark** | TankEnemy | Closes fast, then plants near the player and stops. Becomes terrain. | You route around it instead of outrunning it |
| **Drifter** | BooEnemy | Ignores separation entirely and passes through the pack. | Nothing else moves through a crowd; deeply unsettling |
| **Bouncer** | *(new)* | Hops in committed arcs, each aimed with a wide spread, pausing between. Caroms off whatever it hits. | You can read the hop it is in and not the one after |

**Circler carries a measured hazard.** Lateral motion costs closing speed, and at the current pace
an enemy has very little to spare — an ungated encircling offset was measured to cut a straight-line
chase by ten times. `Enemy.ComputeApproachDirection` already has the guard for this: the `slack`
term, which spends lateral movement only when the player is not pulling directly away. **Circler
must reuse that gate, not reinvent it.**

### The Bouncer, and why "erratic" has to mean something specific

An enemy that moves erratically is the easiest thing in this document to get wrong, in two ways that
pull against each other.

**Erratic must not mean random.** Noise layered on top of a chase is the bad version: it reads as
drunk rather than dangerous, the player cannot plan around it because there is nothing to plan
around, and it is barely distinguishable from the path wander every enemy already carries. The good
version is **committed and unpredictable**: the Bouncer picks a hop, commits to it visibly, and the
player can read *that* hop completely — it is the one after that they cannot call. Determinism
inside each beat is what makes the unpredictability between beats feel fair rather than cheap.

**It must not be slower for it.** This is the measured trap. Movement spent sideways is ground never
recovered, and an erratic walker simply never arrives — the same failure that cut a straight-line
chase by ten times when encircling was left ungated. The Bouncer escapes it the way the Lunger does:
**it moves in bursts faster than it walks**, so it can afford to spend some of that burst on a wide
aim spread and still close. Hop speed is the knob that decides whether this enemy is a threat or a
decoration.

Two things worth deciding up front:

- **Keep the hop closer to a run than to a dash.** A burst fast enough to feel like an attack has to
  telegraph, and then the Bouncer stops being a tier 0 enemy and starts costing the player
  attention. Fast enough to close, slow enough to stay furniture.
- **Let it carom off the crowd.** Enemies are solid to each other at runtime
  (`ConfigureEntityCollision` masks layer 2, whatever the `.tscn` says), so a hopping body already
  bounces off the pack for free. That is the cheapest readability this enemy can get — you watch it
  glance off a skeleton and change course, and the erratic path explains itself.

**It also exposes a real gap in the framework.** A Bouncer wants to be *still* between hops but
still *shoved* by the crowd it landed in. `AdjustSteering` returning `Vector2.Zero` means hold
exactly still, and it deliberately suppresses the separation nudge as well — that is what stops a
planted wind-up sliding off its own tell. There is currently no way to say "stop steering but stay
pushable". Adding one is a small change to the steering branch in `Enemy._PhysicsProcess`, and the
Bouncer is the first enemy that needs it. Build it with the Bouncer, not before.

---

## 6. Phase 2 — one new primitive each

**Blink** (the teleporting mage). A shared `BlinkMove` primitive on the same footing as
`GroundSlamAttack`: vanish tell → gone → arrival tell → appear.

- Placed through `TryFindPlaceableNear`, never inside the player (`MinPlayerSeparation`).
- **Both ends are telegraphed.** A blink with no arrival tell is indistinguishable from a spawn,
  and a spawn appearing next to the player is the thing the screen-relative spawn ring exists to
  prevent.
- Never mid wind-up. Teleporting out of your own tell is the same lie as a slam whose ring moves.
- A flag on `RangedEnemy` rather than a subclass, for the reason `Rooted` is a flag: "it blinks" is
  a tuning choice on a scene, not a new behaviour, and one class is easier to keep honest than two.

**Split** (the blob). A `StartDeath` override, and the rules matter more than the code:

- A **generation counter that stops at 2**. Unbounded splitting is an arena-filling bug.
- Children spawn at reduced health and scale, and are placed via `TryFindPlaceableNear`.
- **Children pay no XP.** The Summoner is capped on lifetime summons precisely because a spawner
  becomes an XP fountain; split is the same failure with a different trigger.
- Children count against `MaxEnemies` like anything else — a split at the cap spawns fewer, or none.

---

## 7. The roster, and where each enemy lands

| Enemy | Tier now | Proposed | Phase |
|---|---|---|---|
| `enemy` (skeleton) | — | stays plain, deliberately: the baseline everything else is read against | — |
| `FastEnemy` | — | **Circler** | 1 |
| `SlowEnemy` | — | **Skirmisher** | 1 |
| `TankEnemy` | — | **Bulwark** | 1 |
| `BooEnemy` | — | **Drifter** | 1 |
| `WardenEnemy` | — | miniboss; **Bulwark + a telegraphed slam** (tier 0 + 1, deliberate) | 2 |
| `HexerEnemy` | Ranged | **+ Blink** between shots | 2 |
| `SkullSentry` | Ranged, rooted | **+ kneel** — the `attack` animation, 0.1 | 0 |
| `LungerEnemy` | Lunger | unchanged; gains the `attack` animation free | 0 |
| `SlammerEnemy` | Slammer | unchanged; gains the `attack` animation free | 0 |
| `ExploderEnemy` | Exploder | unchanged | — |
| `SummonerEnemy` | Summoner | unchanged | — |
| *(new)* Bouncer | — | **Bouncer** — erratic hops, tier 0 | 1 |
| *(new)* Blob | — | **Split** | 2 |

One enemy stays plain on purpose. A swarm needs a baseline for the others to be read against, and
the skeleton is it.

---

## 8. The contract for any new behaviour

Every line below is load-bearing, and most were learned by getting them wrong.

1. **If it can damage, it telegraphs.** An attack without a wind-up is indistinguishable from
   damage arriving at random.
2. **If it holds an `AttackTelegraph`, it overrides `ResetForRespawn` and rebuilds it.** The
   recycler moves abandoned enemies back to the spawn ring; one that arrives with a banked wind-up
   attacks before the player has seen it.
3. **`Vector2.Zero` from `AdjustSteering` means hold exactly still** — it also suppresses the
   separation nudge, which is what stops a planted enemy sliding out from under its own tell.
4. **The returned vector's length is a speed multiplier.** Return a unit vector to walk at `Speed`.
5. **Drawing is local space; hitting is global.** The ring drawn and the radius hit must be the
   same number by construction, not by two numbers that agree today.
6. **A `death` animation, or the node frees instantly.** That is the documented fallback, not a bug.
7. **Anything that spawns enemies is capped** — live *and* lifetime — and its offspring pay no XP.
8. **Add a `RegressionChecks` assertion.** `dotnet build` validates none of this.
   `ValidateAttackPatternEnemies` already checks that no wind-up is zero; extend it rather than
   starting a second checker.
9. **Lateral movement costs closing speed.** Anything that circles, strafes or sidesteps must gate
   that on slack, or it stops being able to reach the player at all.

---

## 9. Sequencing

**Phase 0 first**, and 0.1 before anything else: playing the `attack` animation improves five
existing enemies for one small change, and it is the difference between "the archer kneels" being a
behaviour task and a one-line wiring task.

Then **Phase 1** — four behaviours, no new primitives, no new art, and it is the phase that
actually answers the brief for the bulk of the cast.

Then **Phase 2**, which is where the new art and the new primitives live.

**What is cheap and what is not.** Phases 0.1 and 1 need no art at all. Blink needs a vanish and an
arrival effect. Split needs a smaller blob — possibly just the same sheet at reduced scale, which
`art-direction.md` would rather it were not, so check there before assuming.

---

## 10. Taking from the wider genre — patterns, not creatures

Other games in this genre are a **mechanics and density reference**, on the same footing
`art-direction.md` and the pinned art rule put Vampire Survivors: what transfers is the vocabulary
of behaviours, never a creature, a name or a silhouette. Their bosses are theirs. A telegraph that
travels instead of planting belongs to nobody.

That is not a limitation dressed up as principle. The premise in `world-and-tone.md` gives this game
a cast nothing else has: these are not fantasy beasts wandering a dungeon, they are **an occupying
force garrisoning a place where a wizard is held**. Jailers, wardens, the apparatus of an
occupation. An archetype borrowed as a *behaviour* and rebuilt as a jailer is a better enemy than
the same archetype copied whole, because it means something in a game about breaking people out.

### What the genre does that we do not

Five patterns, each of which lands on something already half-built here.

**1. Mass, not knockback resistance.** In that design knockback is a function of how hard the hit
lands against the body's mass, so a heavy swing shifts a big enemy a little and a light one across
the room. We have `Enemy.KnockbackResistance`, a flat 0-to-1 scalar that can say "a boss does not
move" but cannot say "a boss moves *a little* when you hit it properly". Replacing it with mass is a
small change with an unusually wide effect: every enemy gains physical weight, and a heavy-hitting
build starts to *feel* different from a fast one rather than only killing at a different rate.

**2. Attacks that do not require planting.** This is the important one, and it is exactly what the
mounted-attacker idea exposes. **Every telegraphed enemy we have stops to attack** - `AdjustSteering`
returns `Vector2.Zero`, `AttackTelegraph` assumes a stationary wind-up, and `GroundSlamAttack` draws
its ring at a fixed point. An enemy that attacks *while moving* is structurally new: a telegraph
that travels with its owner, and a hit region resolved along a path rather than at a point. Nothing
in the current framework can express it, and it is the single largest gap in section 2's table.

**3. Charges that end in something.** A charge that resolves into a shockwave matters even when it
misses, because the miss still moves you. Our Lunger dashes and is left winded, and the dash
resolves into nothing at all. This costs almost nothing: `GroundSlamAttack` already exists, and
firing it at the end of the dash is two existing primitives combined.

**4. Multi-phase bosses.** `BossEnemy` runs one pattern on a loop. Phase changes at health
thresholds - the pattern shifts, adds arrive, a radial nova goes out - are the standard vocabulary,
and we own every piece needed to build them: `GroundSlamAttack` for the slam, `SummonerEnemy`'s
capped minion logic for the adds, `RangedEnemy`'s volley fan for the nova.

**5. Enemies that leave hazard behind.** We have `StageHazard` and `SpikeTrap` as *level* furniture,
and a cast that never interacts with them. An enemy that lays hazard as it moves bridges two systems
that already exist separately, and it is the one behaviour that makes the player unable to stand
where the fight has already been.

### Where this lands hardest: the bosses we do not have

`BossCatalog` has **one entry**. Elderbark guards the Enchanted Forest, and the other seven chapters
end on a timer with nothing to fight - which `gameplay-design.md` already calls the free
non-victory. A genre where every stage terminates in a distinct, multi-phase Lord is pointing
directly at the largest content hole in this game, and the vocabulary above is what fills it: a
moving attacker, a slam, a nova, capped adds, a phase change.

That is also the honest reason to take the inspiration seriously. It is not that our enemies need to
be more like theirs. It is that seven chapters currently end in nothing.

### Archetypes worth building, in our fiction

| Archetype | Tier | New machinery | Cost |
|---|---|---|---|
| **Rider** — a mounted jailer that circles and strikes without stopping | 1 | **Travelling telegraph** (new) | High, and it unlocks every other moving attacker |
| **Ooze** — slow, splits on death, leaves a corrosive trail | 0 + 2 | Split (Phase 2) + hazard trail | Medium; both halves are planned already |
| **Shield-bearer** — walks in front of the pack and blocks projectiles for what follows | 0 | Projectile blocking | Medium; the first enemy that punishes firing blindly into a crowd |
| **Lantern-bearer** — buffs nearby enemies; killing it weakens the group | 0 | Aura | Low; same design job as the Summoner, a target worth choosing |
| **Burrower** — submerges, untargetable, surfaces near you | 1 | Reuses **Blink** | Low once Blink exists |
| **Charger** — the Lunger, with a shockwave on the dash | 1 | None | Very low; two existing primitives |

**Shield-bearer is the one I would argue for hardest** after the Rider. Every enemy in this game
dies to fire poured in its general direction; nothing yet asks the player to care *which* body is in
front. It is also the cleanest possible partner for a horde that now arrives in formations - a wall
with a shield at the front is a genuinely different problem from a wall without one.

### The seven Lords, read closely

Researched rather than remembered, because the interesting part of a boss is never its stat block.
Mechanics reference only - none of this is a licence to copy art, names or fiction.

| Lord | Stage | What it actually does |
|---|---|---|
| **Wyrm Queen** | Ember Grounds | Stationary at the centre of the arena. Cycles between summoning explosive orbs and a **rotating breath weapon** that sweeps the ring. **Burrows, becomes untargetable, repositions, resurfaces to ambush.** Fights alongside a normal swarm, on a clock. |
| **Lord of Pain** | Haunted Caverns | Two stages: mounted, then **dismounts on foot** when the first health bar empties. Charges are telegraphed by circles that appear in front of him before he moves. |
| **Wraith Horseman** | Forgotten Viaduct | High mobility. Lance charges, **arrow rain marked by circles on the ground**, a spear attack marked by a **spear-shaped** ground decal, and summoned guards that **only attack in the direction they face**. |
| **Frost Knight** | Frozen Depths | Area denial. Fights *with* the arena: falling icicles and freezing winds that slow, plus elite guards. |
| **Lord of Regret** | Chambers of Dissonance | Spectral arrow rain over three marked zones, plus bombs. |

Their elites are worth a line too, because several are things we have already planned: a **slime that
splits when damaged and regenerates**, **twin gargoyles that must be killed close together**, a
**hydra whose difficulty changes as heads come off**, a **lich that summons archers**, and a
**flamelord that leaves a burning trail**.

### The Wyrm Queen is the one to learn from

Three separate ideas, and the third is the one worth stealing.

**1. A stationary boss with a rotating sweep.** It cannot chase, so the threat is entirely
positional: the beam turns at a fixed rate and the player orbits to stay off it. That is a boss
fight made of movement rather than of reaction time, which suits a game where the player never aims.

**2. Orbs alternating with the sweep.** The two attacks want opposite things - the sweep wants you
circling, the orbs want you not standing still - so cycling them stops the fight settling into one
groove.

**3. It burrows, and is untargetable while under.** This is the important one.

A stationary boss in a bullet heaven has one failure mode: a built player parks at max range and
melts it, and the only lever the designer has left is more health, which makes the fight longer
rather than harder. The burrow solves that without inflating anything. It **paces the fight with
time the player cannot spend damaging**, forces them to break position and re-acquire, and converts
a DPS check into an anticipation problem. It also gives the boss a reason to reposition that reads
as intent rather than as pathfinding.

**We already have the primitive.** The Burrower archetype in the table above is this, and it reuses
Blink. What we do not have is the *invulnerable* half: nothing in `Enemy` can currently be
untargetable while remaining alive, because `StartDeath` is the only thing that leaves the
`"enemies"` group. A boss that submerges needs to leave the group and come back, and every targeting
path reads that group - so the mechanic is nearly free, and the one thing to be careful of is that
`Player.OnEnemyDiedAt` and the AoE sweeps must not treat "gone from the group" as "dead".

### A boss for each of the eight chapters

Sketches, not specifications - enough that the content track can start without redesigning from
scratch each time. Each one is built from primitives in the table above, and each is a different
*kind* of problem rather than a different element.

| # | Chapter | Boss sketch | Primitives |
|---|---|---|---|
| 1 | Enchanted Forest | **Elderbark** (exists). Rooted; the arena grows against you. | Slam, adds |
| 2 | Cursed Dungeon | **The Gaoler** - mounted jailer who dismounts at half health and fights faster on foot. | Rider, phase change |
| 3 | Sunken Cave | **The Hollow Choir** - three small bosses that must die close together, or the survivors revive the others. | Twin/triplet link, adds |
| 4 | Blighted Swamp | **Mother Rot** - splits on damage into smaller copies that recombine if left alone. | Split, regen |
| 5 | Mystic Ruins | **The Archivist** - stationary, rotating beam sweep, submerges into the floor to reposition. | **Rotating sweep + burrow** |
| 6 | Frozen Waste | **The Still Warden** - fights with the arena: falling ice, a slow field, guards that only strike ahead of them. | Hazard, facing guards |
| 7 | Scorched Sands | **The Long Coil** - a segmented burrower that surfaces in arcs across the arena; only the head takes damage. | Burrow, segmented body |
| 8 | The Emberdeep | **The Warden of the Deep** - every phase borrows one mechanic from an earlier Lord. | All of the above |

The Ruins and the Sands both take from the Wyrm Queen deliberately, and they take different halves:
the Archivist is the **stationary sweep** with burrowing as punctuation, the Long Coil is the
**burrow** as the whole fight. Reusing one idea twice is fine as long as each time it is the spine
of a different encounter.

### What boss work needs that does not exist yet

Ordered by how many of the sketches above are blocked on it.

1. **Untargetable-but-alive.** Leaving and rejoining the `"enemies"` group, with the death path kept
   distinct. Blocks every burrower.
2. **A travelling telegraph.** `AttackTelegraph` plants a tell and resolves it in place, so a boss
   cannot wind up an attack *while moving*. Blocks every Rider and every charge.
3. **Telegraph shapes other than a ring.** A spear-shaped decal telling the player to step sideways,
   not backwards, is information a circle cannot carry. `GroundSlamAttack` draws rings only.
4. **Phase changes.** A boss that changes behaviour at a health threshold, not just its numbers.
   `BossEnemy` has no notion of a phase.
5. **Linked health** - enemies that revive each other unless killed together.

Numbers 1 and 2 unlock five of the eight sketches between them, and neither is large: the first is
group membership, the second is letting the telegraph's anchor follow a node instead of a point.

### What not to take

- **Their stage length.** Thirty minutes to a Lord is a structure built around their meta and their
  build depth. Ours runs fifteen (`Node2DGame.TimerVictorySeconds`), and lengthening it is a
  decision about pacing and unlock economy, not an enemy decision.
- **Enemy block chance.** A static defensive roll on enemies adds a second damage layer to a game
  whose damage pipeline is already ten ordered steps deep (`Player.TakeDamage`), and it makes
  outcomes less legible rather than more.
- **Anything recognisable.** Not a name, not a silhouette, not a specific creature. The behaviours
  above are generic to the genre and would be arrived at independently by anyone solving the same
  problems; that is the test for whether something is safe to take.
