# The spell roster, rethought

Companion to `.ai/spell-variety.md`, which argues *why*. This is the *what*: the whole roster, one
by one, with a distinct identity for each. 24 spells existed when the pass was written; **three more
were built out of it** and are in section 2a.

**The constraint I held throughout:** element tags do not change. The reachability work in
`.ai/passives-and-items.md` balanced twelve elements across spells, boons, items and sets, and
re-tagging spells would invalidate all of it for no gain. Every proposal below changes how a spell
*behaves*, never what it counts as. (Base tags still never change. What a spell can *become* at its
level 4 milestone - its element path - is section 2b.)

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
| 7 | Riptide | Water | fired once, no homing | timer | line it up | **a travelling wave**, piercing line | mid |
| 8 | Molten Shard | Metal+Fire | **random enemy** | timer | — | projectile | long |
| 9 | **Thorned Vine** (id `thorn_vine`) | Grass+Poison | **densest cluster** | timer | — | **a vine sweeps the pack**, poisons | **mid** |
| 10 | Glacial Spike | Earth+Ice | **random ground nearby** | timer | — | **persistent zone** | mid |
| 11 | Void Lance | Arcane+Darkness | **fixed, slowly rotating** | timer | **position, not aim** | piercing beam | long |
| 12 | Cyclone Slash | Wind | an orbit round him | continuous | **keep moving** | contact, **a ring that widens as you run** | close |
| 13 | Fireball | Fire | nearest | long timer | — | heavy blast | long |
| 14 | Frost Shard | Ice+Water | **every dying enemy** | **on kill** | — | **shatter burst** | — |
| 15 | Shadow Bolt | Darkness+Poison | nearest | timer | — | **chain jump, debuffs only** | long |
| 16 | Gale Blade | Wind+Lightning | nearest | timer | — | **boomerang, hits both legs** | mid |
| 17 | Scorching Ray | Fire+Arcane | multi-target | timer | **stay on one target** | **ramping beam** | long |
| 18 | Meteor Swarm | Fire+Earth | **a random enemy in range, per meteor** | long timer | — | **barrage**, one meteor after another | long |
| 19 | Solar Flare | Light+Fire | self | timer | **stand still to grow** | pulse zone | close |
| 20 | Toxic Spore Burst | Poison+Grass | **sown among the enemies in range** | timer, **then a fuse** | read the swell | **mushrooms that burst** | **mid** |
| 21 | Spiritual Weapon | Arcane+Light | **by form: nearest / densest knot / toughest** | timer, a swing per weapon | — | **a blow from above: cleave / slam / one cut** | close |
| 22 | Arcane Explosion | Arcane | self | **charges from other casts** | **build the loadout around it** | burst | close |
| 23 | Hunter's Draw | Metal+Wind | nearest | **charge while moving** | stop to loose | volley | long |
| 24 | Black Tentacles | Poison+Earth | **densest cluster** | timer | — | **slow rift + slapping tentacles** | mid |

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
| 27 | Gravewell | Earth+Darkness | **the ground ahead of an approaching enemy** | **proximity trip** | let them come to it | armed trap, root | mid |
| 28 | Hollow Star | Darkness | densest cluster | timer | **build around the knot** | **pull, then collapse** | mid |
| 29 | Starfall | Light | **random ground round the player - nothing** | **continuous drizzle** (the cooldown is a cloudburst) | **walk the rain into them** | **many small bursts** | mid |
| 30 | Familiar | Arcane | **the enemy nearest the player, chosen by a creature that runs to it** (the dog; the crow ranges and goes for shooters, the cat stays underfoot - section 2g) | timer (a strike per creature per cooldown) | **it fights where you stand** | **a pounce and a bite** - one quarry and what is in its jaws | close |
| 31 | Stormsweep | Lightning | **the side the player faces** | timer, every cooldown whatever is there | **face the crowd** | **a band beside him, swept** | close |
| 32 | Sleet Runes | Ice+Wind | **the way the player faces** | fast timer, a stream | **walk to aim** | **true projectiles: pierce and speed** | mid-long |

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
- Draws itself: three turning arms that grow out to exactly the reach as it opens, motes dragged in
  along them from that rim while it pulls, a swelling core, and debris thrown out to the collapse
  radius - no reach ring (section 2c). The card icon is `hollow-star-dark` from
  `tools/art/spell_icons.py`. Its evolutions were the default archetype's, spelled out for a well
  on 2026-10-24 (section 2d); since 2026-10-29 they are its own - Crush, Twin Stars, Black Sun and
  Nova Seed (section 2e).

Verified headless: twelve still enemies in a ring went from a mean 101 px to 45 px from the centre
and all ended inside the collapse; with no well they moved 3 px.

### Starfall - the weather spell (2026-10-25)

The developer: *"lets add a falling star spell of some kind. maybe a meteor ascension, or a rain ability
that can turn into exploding stars?"* It is the rain, and its level 8 is the exploding stars. Not a
Meteor Swarm ascension: Meteor Swarm is aimed rocks on the enemies, and a second aimed barrage would
be the collision this document exists to prevent.

**What it is** (`scripts/Starfall.cs`, `Player.FireStarfall`): a drizzle of small falling stars over
the ground in a circle round the player, landing one at a time, all the time. Each slants in from the
upper left, a white head with a short trail, and lands with a twinkle and a burst of light thrown out
to exactly the radius it hit - the only warning is the star in the sky.

| | Starfall | Meteor Swarm | Gravewell | Toxic Spore Burst | Solar Flare |
|---|---|---|---|---|---|
| aim | **random ground round the player - no enemy decides it** | the enemies | ahead of an approaching enemy | among the enemies in range | the caster |
| cadence | **a continuous drizzle**; the cooldown is a cloudburst spread across the next interval | long timer, a volley | proximity trip | timer, then a fuse | timer |
| demand | **walk the rain into the crowd** | - | let them come | read the swell | stand still |
| payload | **many small bursts, most of them on nothing** | barrage of heavy blasts | armed trap, root | mushrooms that burst | one pulse |

Its value is **coverage, not aim**. A crowd standing in the rain is hit; a crowd outside it is not;
and nothing about it ever turns toward an enemy, so it is the one spell that works the same in an
empty field as in a full one - it is weather. It falls whether or not anything is in range.

- **The cooldown is a cloudburst.** The fire loop can only cast on a cooldown, and a volley every
  cooldown would be the fixed-timer burst the spell exists to avoid, so each cooldown hands the node a
  number of stars (`BaseProjectileCount` + Amount) that it lets fall one at a time across the next
  interval, on a jittered gap. Amount is more stars; cooldown reduction is a heavier rain. It returns
  `CastOutcome.Maintained`: nothing is ever cast, so there is no cast cue (each star twinkles instead)
  and the cooldown still charges Arcane Explosion like the other held spells.
- **Uniform over the area** of the rain (`sqrt` of a uniform draw), so the drizzle is as dense at its
  edge as at the player's feet. The rain's width is the spell's range, which only its levels move;
  Area makes each star bigger, not the rain thinner.
- **One node, every star.** The rain lives in the arena at the origin and draws in world space, so a
  landed star stays where it fell. Stars are slots in fixed arrays drawn in one `_Draw` on the 2 px
  grid; a twinkle's arms are a few snapped bars rather than a square per step, and a trail is three
  lines. Nothing is instanced or allocated per star. The table (written out in full in the `.tres`):
  4 stars of 4 damage every 1.0 s, 28 px landings over a 190 px rain; every level +1 damage, even
  levels the cooldown step and a star (2, 6, 8) or 20 px more rain (4), odd levels +3 px a landing.
  Level 8: 7 stars of 12 every 0.68 s (10.3 a second), 37 px landings over 210 px.
- **The animation is the hit area.** The landing burst is `ImpactDebris`, whose front stops on the
  radius tested; the twinkle's arms stop at 70% of it. No ring, no ground marker before it lands.
- **A star is drawn at projectile size, not mote size.** The first build drew a 4 px head with a
  21 px dotted trail; at the camera's 0.85 that is three screen pixels, and the first capture showed
  a crowd being hit by nothing anyone could see. A star is now a 6 px white core in a diamond of glow
  with glints to 10 px, and a 56 px trail in three continuous runs (hot and 4 px thick at the head,
  then mid, then a thin gold edge), falling 180 px over half a second.

| milestone | branch | tags after | what it does |
|---|---|---|---|
| level 4 | **Shooting Stars** (was Clear Night, +2 stars and +15%, until phase 2 - section 2h) | Light 2 | every third star streaks across the whole rain, rim to rim, a line that strikes what it crosses for 80% of a star; +10% damage |
| level 4 | **Frostfall** (the element path) | Light 1, Ice 1 | +20% damage; every hit slows 30% for 1.5 s; every star falls, twinkles and bursts in Ice's ramp |
| level 4 | **Skipping Stars** (was Downpour, a faster, finer rain, until phase 2 - section 2h) | unchanged | every star skips on twice to the right, like a stone across water: landings 75% and 55% as wide, for 55% and 35% |
| level 8 | **Starburst** | Light +1 | every star is heavy: it lands, swells for 0.3 s and explodes 120% wider for +110% damage, throwing 3 sparks that arc out past its rim and land as small stars (40% damage, 40% of its radius; a white flash and a small burst, no twinkle); stars fall half as often |
| level 8 | **Constellation** | unchanged; gated on the spell's current tags | every star stays lit where it fell for 2.5 s (x Duration), joined to the nearest lit star within 140 px by a line of light; every 0.5 s the lines strike everything they touch for 55% of a star's damage, once per enemy however many lines it stands across; +1 star |

Ice for the path because of the fiction (the cold light of stars) and because Ice has three spell
carriers. Kindled Ward's path is also Ice; both of Light's pure spells going cold is a reading of the
element, not an accident. Its own evolution block (`SetupStarfallEvolutions`) rather than the default
archetype, whose cards promise pierce and projectile speed: every number on these cards is one the
node reads (count, cooldown, area multiplier, damage, and the two behaviours by id), and no
`ScaleMultiplier` or `SpeedMultiplier` is set - the stars are drawn out of the radius they hit and
nothing travels. A Constellation line is drawn 6 px wide and tested against enemy centres at
`LineHalfWidth + RiptideWave.BodyAllowance` (15 px): it strikes what it is seen to touch. The web is
drawn over the bodies, just under the falling stars - on the floor (z -1) the crowd hid every line
exactly where it struck - and a line flashes white along its length on the tick it hits.

**Sound:** `spell_starfall` (`.ai/audio-manifest.md`) on every landing, hit or miss, at -24 dBFS and
throttled at 90 ms - at level 8 it is the most frequent spell sound in the game, so it sits under the
orbit tick. Hits are the Light impact through `DealDamageToEnemy`, as for every spell.

**Unlock:** bought in Arcane Upgrades for 150, beside the other archetypes. Light already had its
pure spell (Kindled Ward), so this is a new way to play rather than a missing carrier. Adding it to the
catalog would have closed the final chapter on a save that had already opened it (the gate is "every
spell recovered", evaluated live), so **schema 14** credits Starfall to a save that met the gate as it
stood before - schema 10's and 12's reason again. (13 is the campaign-order migration from
campaign/new-chapters; 14 chains after it, and any step before 14 that counts the catalog's spells
must leave Starfall out, as `SpellsAddedAtSchema14` does for 10 and 12.)

**Art:** the card icon is `starfall-light` from `tools/art/spell_icons.py` - stars falling across a
round patch of night (Arcane cyan's deep and shade, a sky rather than an element), because drawn in
Light's ramp alone it was pale cream on the level-up card's cream page and vanished there. The stars
draw themselves.
`python tools/art/starfall_sheet.py` composes the captures into `tools/art/_starfall_sheet.png`.

**Measured** (`_BalanceHarness --timescale=2 --immortal --all-unlocked --character=0 --spell=starfall
--solo --upgrade-chance=1 --branch4= --branch8= --force-ascension --profile=10`, one Godot of mine at
a time, on a machine other sessions were loading), damage a second by level. Read the rows with the
frame rate beside them: under load the harness fell into the physics spiral of `.ai/performance.md`
section 1 (8 steps a frame), which slows enemy arrival against the process clock the levels are timed
on, so a contended row understates.

| run | conditions | L4 | L5 | L6 | L8 |
|---|---|---|---|---|---|
| Clear Night + Starburst (first tuning: 3 stars of 5, Starburst x2.3) | clean, 140 fps | 43 | 41 | 74 | 458 |
| Clear Night + Starburst (4 stars of 4, x2.1) | spiral for part of it | 83 | 90 | 141 | 434 |
| **Clear Night + Starburst, shipped** | **clean, 97-137 fps** | **45** | **54** | **92** | **365** |
| Frostfall + Constellation (lines 35% for 2 s) | spiral from level 6 | 37 | 49 | 83 | 227 |
| **Frostfall + Constellation, shipped** (55% for 2.5 s) | **clean, 107-136 fps** | **33** | **47** | **94** | **269** |
| peer: Toxic Spore Burst, Lingering Rot + Doomcap (all damage) | clean, 39-135 fps | 27 | 47 | 62 | 319 |

Against the documented peer bands (40-60 / 70-130 / 331-465) the shipped Starburst row sits inside
all three; against the peer run beside it, the same conditions, it is ahead by about two thirds at
level 4, a half at level 6 and a seventh at level 8. Levels 4-6 swing by a factor of two between runs
with the crowd's density (the second Starburst row is the same build as the shipped one), so read them
as a band. If a playtest finds the drizzle carrying too hard before level 8, Clear Night's +2 stars is
the first knob. Constellation was well short at 227 and its lines were buffed (35% to 55% a tick, lit
2 to 2.5 s) rather than its stars, because the lines are what the ascension is. Its shipped 269 rides
on Frostfall, which pays for its slow in damage: Clear Night's +2 stars and +15% are about 1.24x
Frostfall's +20% at level 8, so Constellation on the same level 4 as the Starburst row would be about
330 - a tenth under Starburst and level with the peer.

**What it costs a frame** (the `starfall` FrameProfiler section: the node's process and its two draw
passes, inclusive - so it also holds the damage its hits deal, which the `takeDamage` section shows
nested inside it). Clean, levels 1-6: 57-187 us. Level 8 Starburst after the draw trim: 190-323 us
with 34-91 us of that nested damage, so about 0.15-0.25 ms of its own at 100-130 enemies. Before the
trim a spark carried a full twinkle and a heavy burst up to 36 debris particles - about 740 draw calls a
frame at level 8 by count, about 330 after. Every draw is a rect or a line into one canvas item, so
the GPU sees a handful of batches; the cost is the C#-to-engine call per primitive. Not measured on the
phone.

**The spiral was the machine, not the spell.** Two Frostfall runs fell into the physics spiral with
the crowd at the 140 cap, and it looked like an area slow packing the crowd against a bot that stands
its ground (`MoveAndSlide` contact, section 7 of `.ai/performance.md`). The clean rerun says otherwise:
125-131 enemies under Frostfall's slow at 107-136 fps, worst frame 17 ms. Starfall's own section in
it was 187-335 us a frame, 29-102 us of that the damage it dealt.

### Familiar - the wizard's hound (2026-10-25)

**Since 2026-10-28 this is the dog**, one of three forms chosen when the spell is first taken - the dog,
the crow and the cat (section 2g). Everything below is the dog's and still true of it, but for two
things section 2g records: the Griffin went to the crow, and the dog's Great Hound and Cerberus were
retuned when the forms were measured.

The developer: *"add new spell, one that summons a pet that fights with you (clearly a wizard dog, but
can evolve/grow/become a new creature on mutation/ascension)"*. It is the autonomous summon of
`.ai/spell-variety.md` phase 5 - the one weapon type that makes the screen feel like it has an ally in
it.

**The premise, honestly.** Kindled Ward was built as the autonomous archetype *without* an ally, on the
reading of `.ai/world-and-tone.md` that the wizard has no escort and that everything glowing besides him
is something he made; its note (below) says a summoned creature with a personality would delete the
premise. The developer asked for the dog anyway, and he decides the fiction. What this keeps of that
reading is the part that matters: the familiar is **made** - conjured out of the wizard's own light,
blinking out and back in a puff of it - not someone who came to help. It carries his colours, not a
faction's. Whether it reads as a mascot is the open question for a playtest (tone rule 1): it is drawn
as a lean sighthound, not a puppy, and the hat is worn the way the wizard wears his.

**What it is** (`scripts/Familiar.cs`, `Player.FireFamiliar`): one pack node in the arena, y-sorted so
each hound stands among the bodies round it. A hound goes for the enemy nearest the **wizard** inside
its hunting range (the spell's range, 200 px, +20 on odd levels) - it guards rather than hunts - runs to
it, crouches, leaps (42% of the range, homing) and bites. Every cooldown hands every hound one bite; a
hound holding one leaps the moment its quarry is in reach, one without it waits off its quarry's
shoulder. With nothing near it comes to heel, behind the wizard on the side away from where he walks.

| | Familiar | Kindled Ward | Spiritual Weapon | Hunter's Draw |
|---|---|---|---|---|
| aim | **the enemy nearest the player, picked by the hound** | nearest to the ward | by form (section 2f) | nearest |
| cadence | a bite per hound per cooldown | a ward per cooldown, capped | a swing per weapon per cooldown | charge while moving |
| demand | **it fights where you stand**: walk away and it breaks off | place it | - | stop to loose |
| payload | **a pounce and a bite**: one quarry, and what is in its jaws | a burning mote | a blow from above | a volley |
| persistence | **all run, a body of its own** | lives 9 s | all run, in the air | instant |

Spiritual Weapon was the other spell with something circling the player - an orbit, a blade on a fixed
circle that hit what it passed. Since 2026-10-28 it too picks a quarry near him and leaves his side for
it (section 2f), and the difference is the body: the hound runs on the ground, pounces, catches bolts
and comes to heel; the weapon is held by nothing, hangs in the air and comes down from above.

- **A body nothing can touch.** Not a physics body and in no group: no enemy can see, target or hit
  it, and it never blocks the player. It meets the ground the way the enemies' routing does -
  `MazeNavigation`'s solid grid (water, maze walls, tree trunks, fences): it will not step onto a
  blocked cell, slides along one, only chooses a quarry it can walk to in a straight line, and goes home
  round water and walls on the navigation's flow field toward the player. Stuck for 0.6 s, more than
  470 px from his feet, or standing on solid ground for a quarter second, it **blinks** home in a puff
  of its own light - it is a spell. The balance report counts every blink (`Familiar.StatsLine`).
- **Cadence is the cooldown.** `FireFamiliar` returns `CastOutcome.Maintained`: nothing is cast, each
  bite snaps on its own, and the cooldown still charges Arcane Explosion like every held spell. A long
  frame carries over between the crouch, the leap and the bite, so a loaded machine or a phone does not
  stretch a bite to a frame per phase.
- **Amount is more hounds** (up to six). Levels add 4 to the bite every level, the cooldown step on
  even levels (it bites faster) and 20 px of hunting range on odd ones (and the leap with it), written
  out in full in the `.tres`. Level 8: 44 a bite every 0.68 s over 260 px.
- **The bite shows its reach.** The jaws (18 px, times the player's Area and the evolutions') snap two
  rows of teeth shut on the radius tested and `ImpactDebris` throws the hound's light out to exactly
  it. No marker anywhere (section 2c).
- **Sound:** `spell_familiar_bite` on every bite (`.ai/audio-manifest.md`); the hit is the Arcane
  impact through `DealDamageToEnemy`. `spell_summon` when it first arrives and when it becomes a new
  form; each hound howls (the `howl` strip and rising motes) on summoning, on every level and on every
  new form.

| milestone | branch | tags after | what it does |
|---|---|---|---|
| level 4 | **Great Hound** | Arcane 2 | it grows into a great hound in the wizard's mantle (the 64 cell): +35% damage, jaws 60% wider, so a bite mauls what stands beside its quarry (+20% and 40% since 2026-10-28, section 2g) |
| level 4 | **Rimehound** (the element path) | Arcane 1, Ice 1 | +20% damage; every bite slows 30% for 1.5 s (the rider); its breath is frost, so its jaws close 40% wider; frost-blue fur, a ruff of ice, frost on its breath and in its bite |
| level 4 | **Littermates** | unchanged | +1 hound, each hunting its own quarry (hounds prefer a quarry no other has claimed); every hound bites 15% softer - a trade, not a doubling |
| level 8 | **Cerberus** | Arcane +1 | three heads: every bite takes its quarry and the two enemies nearest it within 64 px (x Area), +35% damage (+10% since 2026-10-28, section 2g). A hellhound: near-black hide lit blood red, ember eyes. Drawn in frost (a stop darker than the Rimehound) after the Ice path |
| level 8 | **Griffin** (the crow's since 2026-10-28; the dog has the Hound of Annwn) | unchanged; gated on the spell's current tags | it takes wing: a swoop through its quarry and 130 px on past it in a straight line, striking everything within 22 px of the path (x Area, plus a body's half-width) once, +20% damage; swoops come 30% less often than bites; it flies, so water and walls are nothing to it. Drawn in frost after the Ice path |

Ice for the path because a frost hound is the fiction (and the developer's own example) and because Ice
has three spell carriers. Its own evolution block (`SetupFamiliarEvolutions`): every number on these
cards is one the pack reads - damage, the jaws through `GetAreaMultiplierAtLevel`, the hounds through
the projectile count, the swoop rate through the cooldown, and Cerberus's and the Griffin's verbs by id
with their reach in `Familiar`'s own constants. No `ScaleMultiplier`: each form is drawn at its own size.

**Art** (`python tools/art/familiar.py`, all seven forms with idle, run, pounce and howl strips and
their SpriteFrames; `--preview` writes `tools/art/_familiar/`): one renderer lights every part -
ellipsoid blobs, tapered capsules, shaded polygons - from the contract's upper-left key, quantises to
the material's five tones, casts a pixel of shadow from every front part onto the part behind it,
rims the key edge of the near side and puts the occ contact line down; the fur has a lay of flecks
keyed to the part's own coordinates so it moves with the body. Density 1.0-1.1 edges a pixel; every
pixel audited. **What keeps it from ever reading as an enemy**: the four enemy canines (Dire Wolf,
Bog Hound, Sled Hound, Chainhound) are dark linen or steel with two points of the violet arcane mark
for eyes and carry their heads low; the familiar is warm fawn (`flesh`, a stop deep), carries its head
high, has amber eyes and a gold star on its collar, and wears **the wizard's hat** - indigo wool, a
gold band, a gold star - which is the attachment that survives the fill-it-black test. The hound is on
the 48 cell at 1.3: on the 32 cell it stood a head under the skeletons and the hat was a speck. Every
larger form is on the 64 cell. Cerberus fans its three heads one above another with the hat on the top
one; the Griffin has an eagle's head and breast in gold, the wizard's indigo for wings edged in gold,
the hound's fawn for its lion quarters, and its shadow detached below it. The card icon is
`familiar-arcane` (`tools/art/spell_icons.py`, drawn by the same renderer as a portrait).
`python tools/art/familiar_sheet.py` composes the `_UiShot` captures - every form at 1280x720 and
540x1212 portrait, at heel and in a crowd, the level-up card, both evolution pages and the shop - into
`tools/art/_familiar_sheet.png`; its docstring has the commands. What the first captures changed: the
32-cell hound (too small, hat a speck, fur pinkish-white), heel at 40 px (it stood in the wizard's
robe; 58 now), and card texts that ran off the foot of the mutation cards.

**Second pass (2026-10-28, two playtest notes from the developer).** *"The dogs front legs look like
they're going backwards"*: the foreleg was one two-bone reach from mid-chest to the paw with its joint
thrown forward, so every standing leg had a knee jutting out at mid-height and a shin raking back -
worst on the heavy forms. It is now a dog's foreleg in every form and frame (`front_leg`): the root at
the point of the shoulder, the elbow pointing back at the brisket, a straight forearm, and a wrist that
folds the paw back only as the leg swings through; in the run the forelegs reach out straight in front
of the chest before they plant. The hind leg already had its backward joint and is unchanged.
*"Cerberus should give the dogs a new redder / blacker skin"*: Cerberus is the hellhound - its body in
the `red` row's near-black shade and deep (then `violet` deep, the shade turning toward indigo), only
its key-facing planes lit blood red and crimson from the `ember` row, six eyes lit ember, embers in the
coat and sparks on its breath. It is still the wizard's: the hat, the indigo mantle and the gold collar
and star are unchanged on it, and those carry the read now that the warm fawn coat is gone. Cerberus
after the Ice path stays frost, a whole stop down the azure row from the Rimehound, eyes lit ice.

**Unlock:** bought in Arcane Upgrades for 170, Kindled Ward's price - Arcane has its pure spell, so this
is a new way to play rather than a missing carrier. **Schema 15** credits it to a save that already met
the campaign gate, Starfall's schema 14 reason.

**Measured** (`_BalanceHarness --timescale=2 --immortal --all-unlocked --character=0 --spell=familiar
--solo --upgrade-chance=1 --branch4= --branch8= --force-ascension`, 8-9 minutes, one Godot of mine at a
time on a machine another session's Godot was loading), damage a second by level. "Frames" is the game
time per frame; over about 35 ms the row understates (`.ai/performance.md` section 1).

| run | frames | L4 | L5 | L6 | L8 |
|---|---|---|---|---|---|
| first build: 12 a bite, the bite cycle a frame per phase | 23-400 ms | 22 | - | - | - |
| Great Hound + Cerberus (16 a bite, +4 a level; Cerberus +25%) | 14-22, minute 9 at 233 | 53 | 60 | 74 | 328 |
| Littermates (+1 hound, no trade yet) + Griffin | 14 to minute 6, then 106-267 | 75 | 71 | 90 | 421 |
| Rimehound (no frost breath yet) + Cerberus (+35%) | 14-34 | 38 | 43 | 55 | 235 |
| **Rimehound (frost breath, jaws 40% wider) + Cerberus (+35%), shipped** | **14-36** | **38** | **42** | **52** | **270** |

Peer bands (Gravewell's table above): 40-60 / 70-130 / 331-465. What came of them: **Cerberus went from
+25% to +35%** (the Great Hound row sat on the floor of the level 8 band; at +35% it is about 355);
**Littermates became a trade** - +1 hound, every hound bites 15% softer, about 1.7x instead of 2x, which
puts its level 4 near 64; **the Rimehound got frost on its breath** - jaws 40% wider, so a frost hound
chills the knot it bites into. The Ice path still pays for its slow in damage, as Frostfall does for
Starfall: it gives up attunement (the spell names two elements) for the Ice tier and the rider, and
its +20% is the standard path's against the Great Hound's +35% and wider jaws. Shipped, it measures
270 at level 8 - level with Frostfall + Constellation's 269 - and 52 at level 6, both under the bands,
on a solo bot that holds no other Ice to give it a tier. That is the element-path convention, kept on
purpose (every path is +20%, the tag and the rider); if a playtest finds the frost hound weak, its
breath (`rimeBreath` in `SetupFamiliarEvolutions`) is the first knob, then the path's own +20%.

A bite lands on one enemy and whatever is in its jaws, where the peers' casts land on several, so it
bites harder than they hit: 16 at level 1, 44 at level 8. Bite rate is the cooldown when the crowd is
close (the cycle - crouch 0.08 s, leap 0.22 s, bite 0.16 s - fits inside it); swoops saturated theirs
(745 in 326 s from two griffins).

**Never stuck.** The report's familiar line counts every blink home by reason, the longest stall, and
the hound-frames standing on water or a wall - by the navigation it steers with AND by the terrain
itself (`TerrainStreamer`, what the collision is painted from), with the frames the navigation was
present, so a missing navigation window cannot pass by default. Walking a straight bearing for four
minutes (`--walk=30` and `--walk=120` on the Enchanted Forest, `--walk=30` through the Cursed Dungeon's
maze): 195-231 bites a walk, no blink home of any kind, never left behind, 0 of 14,889-16,943
hound-frames on water or a wall by both authorities, longest stall 0.10 s (a slide along a wall). Every
balance run above: 0 blinks, 0 frames on solid ground.

### The facing family - Stormsweep and Sleet Runes (2026-10-25)

The developer asked for *"spells that function like vampire survivors whip and dagger - they launch in a
specific direction from the player every time they fire, not targeting enemies"*, and of the first
build, which drew a whip and threw knives: *"I didn't mean literally whip and dagger, I still want spells
themed around magic. But I do want the spells to operate similarly to those spells."* So the mechanics
are that genre's and the fiction is spellcraft: no lash, blade or hilt in a name, a card, a sprite or an
icon. Mechanics only from the genre; nothing here is drawn from its art. Both stand on
`Player.FacingDirection` / `FacingSide` (`.ai/spell-variety.md`, phase 1, built with them) and declare
`SpellTargetingMode.Facing`.

| | Stormsweep | Sleet Runes | Thorned Vine | Cyclone Slash | Hunter's Draw | Gale Blade |
|---|---|---|---|---|---|---|
| aim | **the side he faces** | **the way he faces** | densest pack | an orbit round him | nearest enemy | nearest enemy |
| cadence | timer, whatever is there | fast timer, a stream | timer | continuous | charge by moving | timer |
| demand | **turn to face the crowd** | **walk to aim** | - | keep moving | stop to loose | - |
| payload | a band beside him, swept top to bottom | true projectiles: pierce, speed | one sweep across a pack, poisons | contact | volley | returning projectile |

Neither ever picks an enemy, and both fire every cooldown whether or not anything is near - a spell
that waited for a target would be aiming itself. Walking is aiming: a player backing away from a crowd
casts away from it, so turning into the threat for a moment is the decision both spells are. **Element
choice:** Lightning, Ice and Wind each had three spell carriers, the fewest in the game with Water
(`.ai/passives-and-items.md` 3a's measure, recounted). Stormsweep is pure Lightning - the second pure
Lightning spell after Chain Lightning; Sleet Runes is Ice + Wind, which gives two of the thinnest elements
a carrier each. **Names** were checked against the roster: "Sweeps" is Stormsweep's count noun because
"arcs" already means Chain Lightning's jumps on its cards.

**Stormsweep** (`scripts/Stormsweep.cs`, `Player.FireStormsweep`). An arc of storm-light leapt from his
hand and swept down through the ground on the side he faces. One arc is three beats: it LEAPS out along
the top edge of a band beside him to full length (0.07 s - the warning), SWEEPS down across the whole
band with its far end leading (0.08 s), and LINGERS while the swept band fades and the spent arc springs
back to his hand (0.16 s). Damage lands during the sweep: an enemy is struck when, at its distance
along the arc, the drawn arc's across position moved past its centre that frame (plus
`RiptideWave.BodyAllowance`, a body's half-width) - so the band is struck by being swept, nothing outside
what was swept is touched, and nothing is struck in the linger. Amount and the count are more sweeps,
0.14 s apart, alternating sides from the facing one (the genre's rule for this shape): level 2 adds the
sweep to the other side, level 8 a third. The band's length is the spell's Range (150 px from his centre,
190 at level 8), its height `BaseHalfBand` (22 px above and below his centre) plus the AreaSize lines, and
the player's Area and every milestone's AreaMultiplier grow both - "area makes it longer and wider". A
child of the Player, so an arc stays at his hand while he walks; one node carries every arc in a fixed
pool, and the crack at an arc's tip and a Thunderclap's burst stay in the world where they went off.

**Drawn as a whip** (2026-10-28). Eric: *"stormsweep looks robotic. it should be shaped and animated
more like a whip".* The first build drew a uniform zigzag polyline that slid down the band in lockstep
and left five even lines behind it. The beats, their timing and the hit test above did not change; the
picture did. Every frame the node poses each arc as a 17-point lash and
`scenes/shaders/stormsweep_lash.gdshader` draws it as pixel art on the world's 2 px grid, half-width 4-8
px at the hand (with the square root of Area) thinning fast to one art pixel at the tip:

- **Leap** - it unfurls along the band's starting edge: a loop rolls out from the hand to the tip, the
  part past it folded back a radius into the band (never out of it), shrinking until the lash lies
  straight along the edge - exactly the pose the sweep starts from. At that moment the tip **cracks**:
  white-hot for 0.05 s, a four-armed star and sparks thrown to `BodyAllowance` (a picture, not a hit -
  nothing is struck there the sweep does not cross). It replaces the old spark at the far end.
- **Sweep** - the lash lies on `PassAcross` itself, the curve the hit test sweeps, tip leading, with a
  ripple of 2.5 px (falling as it goes) running down it and 1.5 px of lightning wander, both well
  inside the body allowance. Behind it a dithered smear of streaks covers the band between where the
  lash is and where it was - the ground just struck, and only that.
- **Linger** - spent, it goes slack into the band it swept and springs back toward the hand with a wave
  running up it, cooling down its ramp (white, hot, mid, edge) and dissolving, while the smear of the
  swept band fades out.

The colours are the arc's ramp (Lightning, or Metal on Iron Current), the crackle the storm's own, and
a Legendary's gold multiplies all of it. Iron Current keeps its look as a lash: beads of iron, long and
square by turns with an `occ` seam between them, strung along it and thinning with it, with the storm
crawling over them. Thunderclap's second pass, Cardinal Storm's cross and Amount's alternating sides are
unchanged. Nothing is allocated per frame: every lash is one row of a 13 x 16 float image written in
place and pushed with `ImageTexture.Update` (handing the shader uniform arrays every frame allocated a
finalizable disposer per array on the C# side, 0.3 KB a frame - measured), and one quad per lash.

| milestone | branch | tags after | what it does |
|---|---|---|---|
| level 4 | **High Voltage** | Lightning 2 | +30% damage, every sweep 20% further and wider |
| level 4 | **Iron Current** (the element path) | Lightning 1, Metal 1 | +20% damage; the first hit Withers 5% of max HP; the arc is repainted as beads of iron strung on the current with lightning crawling over them (`IPaintsElementPath`) |
| level 4 | **Rapid Discharge** | unchanged | -30% cooldown, every sweep crackles through 25% faster (SpeedMultiplier is its tempo) |
| level 8 | **Thunderclap** | Lightning +1 | every arc sweeps twice, down and back up, and each pass ends in a thunderclap at its far end hitting everything within 56 px (x Area) for 80% of the sweep |
| level 8 | **Cardinal Storm** | unchanged; gated on current tags | every sweep goes all four ways at once; 25% less often |

Metal for the path because of the fiction (a storm drawn through iron) and because a sweep's first touch
on an elite is exactly where a share-of-health Wither pays.

**Sleet Runes** (`scripts/SleetRunes.cs`, `Player.FireSleetRunes`). Runes of rime cast on the wind the way
he faces. The cooldown is a cast: it hands the node the spell's runes (count plus Amount) and the node
lets them go one after another, 0.05 s apart, each along the facing AT ITS RELEASE - so a stream bends as
he turns, and a rune cast while he stands still goes where he last walked. Every other rune steps 4-6 px
off the line, alternating sides, so a stream reads as several runes. A rune is a true projectile: 640 px/s
(+80 at level 5, x1.3 under Far Wind), flies the spell's Range (380 px, 440 at level 7), strikes what its
path crossed this frame (a segment test, so a stutter cannot step it over a body) nearest first, and
passes through Pierce + 1 bodies before it is spent. One node in the arena carries every rune in a fixed
pool of 96; each is one region of a 32-heading pre-rotated sheet (`tools/art/sleet_runes.py`) - pixel art
turned by the engine smears, a rune rasterised at its heading keeps its pixels - drawn in one call, with
two wind streaks behind it. The rune is a sigil, not an object: a diamond frame of rime with a lit point at
each corner, a ring inside it and a white-hot heart, trailing a tail of sleet banded Ice and Wind. A strike
throws rime flakes forward to exactly the width the rune was tested at; a spent rune dissolves into sleet.

| milestone | branch | tags after | what it does |
|---|---|---|---|
| level 4 | **Deep Rime** | Ice 2, Wind 1 (+12% cooldown) | +35% damage, each rune passes through 1 more |
| level 4 | **Rune Flurry** | unchanged | +2 runes a cast |
| level 4 | **Far Wind** | unchanged | runes fly 30% faster and 120 px farther |
| level 8 | **Sigil Fan** | unchanged; gated on current tags | every cast leaves at once across 50 degrees; +3 runes, +1 pierce |
| level 8 | **Turning Gale** | Wind +1 (+12% cooldown) | at the end of its flight the wind turns every rune back to the player, and it strikes again on the way, through as many as it struck going out; +35% damage |

A hybrid, so its path is deepening either half (Ice at 4, Wind at 8, never 3 of either). Turning Gale is a
return, Gale Blade's verb; it is a level 8 here, on a stream cast the way he faces rather than one blade at
the nearest enemy, and it is Wind's both times - the element that brings things back. Neither block sets a
ScaleMultiplier (the arc is drawn out of the band, the sheet at one size), and the runes read no Area:
their reach is Range.

**Sound.** Each replaces its spell's element cast cue (both return `CastOutcome.Maintained`):
`spell_stormsweep` per pass, rising air into a hard discharge with a short buzz, -14 dBFS, throttled 60 ms;
`spell_sleet_rune` per rune, a rising breath of air ending on an ice chime, -22 dBFS, Swarm tier, throttled
45 ms (`.ai/audio-manifest.md`). Hits are the element impact through `DealDamageToEnemy`, as for any spell.

**Unlock:** both bought in Arcane Upgrades - Stormsweep 160, Sleet Runes 130 - beside the other archetypes:
walking becomes aiming, which changes how a run plays. Neither is a missing carrier an achievement would
hold back. Two new catalog spells would close the final chapter on a save that had already opened it, so
**schema 16** credits both to a save that met the gate before (schemas 10, 12 and 14's reason; 15 is
main's Familiar, the same kind of grant, and the two can be folded into one step); steps 10, 12 and 14
leave them out of their counts (`SpellsAddedAtSchema16`).

**Art:** card icons `stormsweep-lightning` (an arc of lightning swept a quarter-turn round a glowing sigil,
two afterglow arcs inside it, on violet night - pale yellow vanished on the cream card) and
`sleet-runes-ice` (three of the game's own runes in flight on the wool row's night), both in
`tools/art/spell_icons.py`; the rune sheet `assets/bonelight/effects/sleet-rune.png`. The arc draws itself
(`scenes/shaders/stormsweep_lash.gdshader`, "Drawn as a whip" above).
`python tools/art/facing_sheet.py` composes the captures into `tools/art/_facing_sheet.png`.

**Measured** (`_BalanceHarness --minutes=8 --timescale=2 --immortal --all-unlocked --character=0
--spell=<id> --solo --upgrade-chance=1 --branch4=<L4> --branch8=<L8> --force-ascension --profile=10`,
one Godot of mine at a time, on a machine where other sessions were running three more; several runs fell
into the physics spiral of `.ai/performance.md` section 1 for a few minutes, so read the rows as bands).
Damage a second by level; "aim" is the new `--aim-facing`, a standing bot that taps toward the nearest
enemy for one physics frame every quarter second - the player who turns to face the crowd. Without it the
immortal bot faces wherever its last XP orb lay, which is the floor for a facing spell. The runs were made
under the spells' first names (Stormlash, Sleet Knives); the rename changed no number and no mechanic,
and the branch names below are the current ones.

| run | bot | conditions | L4 | L5 | L6 | L8 |
|---|---|---|---|---|---|---|
| Stormsweep, High Voltage + Thunderclap (first numbers: 8 base) | default | spiral from minute 4 | 57 | 70 | 93 | 388 |
| same | aim | spiral from minute 4 | 67 | 121 | 248 | 420 |
| Stormsweep, Iron Current + Cardinal Storm (8 base) | aim | mostly clean | 34 | 66 | 88 | 386 |
| **Stormsweep, High Voltage + Thunderclap, shipped (7 base)** | **aim** | **clean, 94-145 fps** | **59** | **91** | **109** | **391** |
| same, shipped | default | clean; stood at level 2 for 112 s, so a dense crowd met levels 3-6 | 151 | 151 | 219 | 443 |
| Sleet Runes, Deep Rime + Turning Gale (first numbers: 5 base, Gale +20%) | default | **never passed level 4** - see below | - | - | - | - |
| same | aim | spiral for a minute | 27 | 43 | 67 | 296 |
| Sleet Runes, Rune Flurry + Sigil Fan (first numbers) | aim | clean | 26 | 42 | 79 | 330 |
| **Sleet Runes, Deep Rime + Turning Gale, shipped (6 base, Gale +35%)** | **aim** | spiral for a minute | **26** | **42** | **67** | **300** |
| peer: Toxic Spore Burst, Lingering Rot + Doomcap (all damage) | default | brief spiral | 23 | 49 | 70 | 344 |

Against the documented peer bands (40-60 / 70-130 / 331-465) the shipped Stormsweep sits inside all three
on the aiming bot and inside the level 8 band on every run (386-443); levels 4-6 swing with the crowd's
density as every close spell's do, and the dense-crowd row is the sweep at its best, not its median. Its
first numbers were a seventh hotter and are trimmed (base 8 to 7). If a playtest finds it carrying too
hard, the knobs are the level 2 sweep to the other side and Thunderclap's 80%.

Sleet Runes sits with the peer run beside it at every level and at the foot of the level 8 band. The buff
between its two rows (base 5 to 6, Turning Gale +20% to +35%: +25% a rune at level 8) moved nothing,
because what limits it is not damage a hit but hits a rune - about 1.2 bodies a rune on the aiming bot,
which faces the NEAREST enemy rather than the densest line. A player who casts into the thick of a crowd
does better; if a playtest finds them weak anyway, the lever is runes a cast or the cooldown, not damage.

What the rows say:

- **The default bot is the wrong floor for the runes, and says so loudly.** Sleet Runes on it never passed
  level 4: the runes kill up to 380 px out, the bot walks out to the orbs they drop (so it keeps facing
  outward), and from 4.5 minutes it stood wedged in one spot with the whole crowd behind it and nothing in
  front to drop an orb that would turn it - 1-2 kills a minute, the swarm at its cap. A player turns; this
  bot cannot. Stormsweep does not have the problem (it hits beside him, and level 2 adds the other side),
  and on the same bot it is ahead of the peer at every level.
- Runes cast from inside a crowd strike it (checked on a placed crowd with `_UiShot`), so the collapse is
  the bot's, not the hit test's. Where exactly the bot wedged was not checked.

**What it costs a frame** (the `stormsweep` and `runes` FrameProfiler sections, inclusive of the damage
their hits deal). Clean frames: Stormsweep 83-110 us at levels 1-6, 190-270 us at level 8 with Thunderclap
(40-70 us of that the nested damage), 230-390 us with Cardinal Storm (twelve arcs a cast); Sleet Runes
30-60 us at levels 1-6 and 145-195 us at level 8 with Sigil Fan. Nothing is allocated per arc or per rune;
the 0.5-1.3 KB a frame the stormsweep section shows at level 8 grows with the hits and is the damage
path's (floating numbers, the stats), counted because the section is inclusive. Headless, so CPU only;
not measured on the phone.

The whip (2026-10-28), same harness flags (`--aim-facing --upgrade-chance=1 --branch4=stormsweep_iron_current
--branch8=stormsweep_cardinal_storm --force-ascension --seed=11 --minutes=6 --timescale=2 --profile=10`),
minutes 3-6 with Cardinal Storm, ~100 enemies: the old arc 112-143 us and 0.3-0.4 KB a frame, the lash
70-83 us and 0.3-0.4 KB (the damage path's, as above). The CPU went down because the drawing moved to the
GPU: each lash is a quad over its band whose fragments walk the lash's 16 segments - fill cost the
headless profiler cannot see, **not yet measured on the phone**. Damage is unchanged within the harness's
run-to-run spread (`--character=0 --spell=stormsweep --solo --immortal --seed=11 --minutes=4`, attributed
total, one run each on the same base): default bot 22,420 before / 21,029 after; with `--aim-facing
--upgrade-chance=1` 22,675 / 22,856, and level 7's rate 175 / 180 a second. Two runs of the default bot on
the same code (on an earlier base) differed by more than that: 19,097 and 19,410 before, 20,853 and
10,264 after - the second skipped six level-ups and never left level 3.

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

**How the bog is drawn** (2026-10-28). Eric: *"improve the mirefoot animation. it shouldnt just draw semi
transparent circles that overlap each other and the player."* Each pool drew its own translucent disc at
z 1, so a trail - pools 72 px apart at a 46 px radius, overlapping everywhere - was a row of stacked
lenses darker wherever two met, and the newest one sat over the wizard and the skeletons in it. The
pools keep every number that matters (damage, slow, radius, tick, life, the cap) and no longer draw
(`MirePool.tscn`: `DrawsItself = false`; the Magma Pool and the Lingering Rift still draw themselves).
One `MireSurface` per trail, made with its first pool and kept in the arena beside them, draws them all:

- **Under the actors**, at z -16 (`CraterGround.GroundZ - 1`), the lowest of the spells' ground
  layers: over the walkable decor (-20), just under the craters, scorch, magma pools and rifts (-15)
  and a crater's fire (-14), and under an event's site (-5), a secret's seed (-4), the freeze marks
  (-2) and the ground effects at -1. The player, the enemies, trees and rocks stand in it, and a meteor
  landing in it punches its crater over the water; nothing is ever covered by it.
- **One surface.** `scenes/shaders/mire_surface.gdshader` shades each 2 px cell once - the first pool
  whose circle holds it owns it, every other quad over it discards - from the union of every pool that
  reaches it. Overlaps are one body of water with one waterline; nothing doubles. Opaque Bonelight
  tones, Bayer dithering the only gradient: dark water (moss deep) with peat (verdant deep) stirred
  through it in slow clouds, green shallows and a lit lip (verdant shade and base) on the bank toward
  the key light, an `occ` waterline and peat shadow under the bank away from it. The bank wanders
  (world-space noise, so where pools meet their banks are one bank) but only **inward** of each pool's
  radius - at most 8% of it, 3-10 px - so the bog never promises more than the hit does.
- **Alive.** Duckweed drifting in the shallows, short ripple lines of sky (water edge and mid) on every
  third row drifting either way, a sheen sliding slowly across the whole bog, two bubbles a pool that
  swell, burst white and roll a ring out, and rings rolling out round the feet of whatever is wading in
  the water.
- **Its life.** A pool seeps in from a third of its size over at most 0.3 s (always inside its first
  tick, so it is whole before it first bites), its leading edge soaked earth going under. From 55% of
  its life the water draws back by up to 40% and leaves a mottled damp stain out to the old edge -
  still the full reach, still biting - and its last 0.45 s (or 15% of a short life) dissolves it,
  dithered rather than faded.

Nothing is allocated per frame: the pool arrays go to the shader only when a pool comes or goes (each
pool's seep, dry and dissolve are worked out in the shader from its birth time and one clock), and the
waders are quads of their own carrying their ring offset in UV and their phase in the vertex colour,
gathered from the enemy grid with a struct visitor. The first build uploaded three uniform arrays a frame
and the profiler's new `mire` section showed 0.4 KB a frame for it.

**Measured** (`--profile=10`, `--all-unlocked --character=0 --spell=mirefoot --solo --immortal --circle=300
--upgrade-chance=1 --branch8=mirefoot_infinite_horizon --force-ascension --seed=11 --minutes=6
--timescale=2`, level 8 with 15 pools, ~130 enemies): `mire` 44-46 us and 0.1 KB a frame, CPU only - the
fill (every quad's owner test, the union walk once a cell) is GPU work the headless profiler cannot see,
**not yet measured on the phone**; Titan Cataclysm's pools three times as wide are the case to watch.
Damage is unchanged within the harness's spread (attributed total, `--seed=11 --minutes=4`, one run each
on the same base): the standing `--immortal` bot deals 0 before and after (Mirefoot needs a walker -
"Spells the standing bot cannot use", below); `--circle=300 --upgrade-chance=1` 21,493 before / 20,424 after, level 7's rate 182 /
199 a second; on an earlier base 20,916 / 20,092, and `--circle=300` alone 20,522 and 20,623 / 19,964 and
18,540. The totals lean a few per cent lower after while the per-level rates lean higher - time spent at
each level, not the pools; the hit code and every number are untouched.

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

A grave opened in the ground on the swarm's path, which opens, waits, and caves in on whatever
treads on it.

- **Aim: the ground just ahead of an approaching enemy** (since 2026-10-23; it was random ground in
  a ring round the player). Still never on an enemy and never centred on the caster - the enemy
  decides by walking into it - but put where enemies walk: `LeadPixels` (75) toward the player from
  the nearest enemy, never nearer him than `MinFromPlayer` (70). Each pit of a cast takes the next
  nearest enemy whose spot is `Spacing` (92) clear of the cast's other pits and of every pit already
  waiting, so a cast covers several approaches; with every approach covered it holds the cast. Nothing
  in range, nothing opened.
- **Cadence: proximity.** The timer only decides when a pit is *placed*. It resolves when an enemy
  treads on its bank. A trap that fired on a clock would just be a slow bomb.

The blast radius is wider than the trigger radius on purpose: whatever sets it off is caught, and so
is whatever was following it. It cannot trigger while opening, which is what stops a pit dropped
into a crowd from resolving on the frame it lands - the wind-up is the cost of the aim being free.

**Why it changed (2026-10-23).** The playtest: *"does the grave ability (i forgot the name) do
anything? i couldn't tell what it was supposed to do."* Three reasons, all fixed:

- **It could not be seen.** A faint dark disc and a faint ring, on a dark floor, under a crowd. It is
  generated art now (`tools/art/gravewell.py`): a grave with a lit bank of turned earth - drawn at the
  trigger radius, so stepping on the earth IS the trigger - a dark mouth with Darkness motes rising out
  of it while it waits, and a leaning headstone. The collapse shows the blast by its own motion:
  cracks and a heave of earth run out to exactly the blast radius (`gravewell_collapse.gdshader`) and
  stop there; everything caught is tugged toward the pit and held for the root by heaped earth and
  fingers of Darkness at its feet. No ring anywhere (section 2c).
- **It went off where nothing was.** Random ground, so most pits sat out their twelve seconds. Aimed
  at the swarm's path, nearly every pit springs.
- **Its cards did not reach it.** Level 3's Area +12 and the generated Root Duration line were read by
  nothing; Amplified Impact's and Titan Cataclysm's Area grew only the drawing (Titan's x2 painted the
  pit twice as wide as it hit); Infinite Horizon's +3 opened no extra pits; Duration did not lengthen
  the wait. All of them reach it now, through `GravewellTrap.For`.

Measured with the balance bot (`_BalanceHarness --minutes=9 --timescale=2 --immortal --all-unlocked
--character=0 --spell=gravewell --solo --upgrade-chance=1 --branch4=gravewell_amplified_impact
--branch8=<branch> --force-ascension`, one run at a time), damage a second at each level; the level 4
and 6 columns give both runs of a row (one per level 8 branch):

| | L4 | L6 | L8 Titan Cataclysm | L8 Infinite Horizon |
|---|---|---|---|---|
| before (random ground, cards unread) | 43-47 | 64-92 | 275 | 221 |
| aimed, every card read, old numbers | 68-71 | 116-118 | 680 | 469 |
| damage table trimmed | 59-75 | 93-98 | 527 | 397 |
| **shipped**: and blast 94 -> 86 px | 51-64 | 84-118 | 536 | 417 |

Peers in the same bot (section 3, Toxic Spore Burst): 40-60 at level 4, 70-130 at level 6, 331-465 at
level 8. The trims: base damage 14 -> 12, the level 2/5/8 damage +4/+5/+6 -> +3/+3/+3, the blast
94 -> 86 px, and the tug toward the pit softened so it no longer gathers the crowd into one knot for
the next pit (pulled all the way in, the knot was most of the overshoot - Hollow Star is the spell that
gathers). Titan Cataclysm's level 8 did not move for the last trim: at 255 px its blast takes most of
what arrives, so its number is the spawn rate as much as the spell, as Doomcap's is. Bot runs at levels
4-6 swing with how dense the crowd is when it holds that level; read them as a band.

Mirefoot draws itself, so it never waited on the art backlog. Kindled Ward draws itself too, and should keep doing so - it is a made light rather than an object. Gravewell drew itself until the playtest that asked whether it did anything; it is generated art now (section 2c).

**Bramble Seed draws itself out of its own hit radius (2026-10-20).** It was a green disc with
fourteen spokes at the edge and read as a decal; the brief was "roots writhing out". It is now
eleven thorned roots (six reaching the edge, five shorter) that burst out of the seed over a
staggered third of a second, writhe the whole time with a wave running seed-to-tip, whip on their
own intervals, and at the end dry to brown and pull back into the ground. They are drawn, not a
sprite sheet, for one reason: each root is laid out in polar terms with its distance from the seed
running 0..length and length never above `BrambleSeed.Radius`, so the writhing bends a root sideways
and cannot carry it past the ground it hurts, and the long roots end exactly on the radius. (A faint
disc and rim sat under them at the full radius until 2026-10-23; they were an area marker and went -
the roots now hold their full length until the patch stops hurting and only then pull back in, so they
are honest to the last hurting frame. Section 2c.) It sits at ZIndex -1 now, under the actors (the old disc at 1 was drawn over the wizard),
and a seed pushed out by a fourth one withers over half a second rather than vanishing; it stops
hurting at once, so the three-patch cap is unchanged.

**Riptide, Black Tentacles and Thorned Vine were rebuilt as pixel art that draws itself (2026-10-23).**
All three had the same complaint in different words - Riptide's sprite "looks like a thrown weapon",
Black Tentacles was a circle with squiggles, Thorn Vine "looks ridiculous" - and the same fix: draw
the thing at the game's 2 px art grid, out of the number that decides what it hits.

- **Riptide** is a small breaking wave (`RiptideWave` + `scenes/shaders/riptide_wave.gdshader`): a
  foam lip that never holds the same shape two frames running, streaks on the face, a shadow under
  the curl, spray left behind. The crest spans the swath the bolt cuts - its hit circle plus half an
  enemy body each side (`RiptideWave.BodyAllowance`), because the circle hits BODIES and a wave drawn
  to the bare 8 px circle was a speck that hit a body-width beyond itself. Every fragment snaps to
  the world grid before it is turned into the wave's frame, so the pixels stay square at any heading.
  Titan Cataclysm (and so Spring Tide) is the **titanic** form: darker, deeper water, a trough drawn
  down ahead of the face, a lip half again as thick with spume heaped up the screen, twice the spray,
  and a heavy splash (`RiptideSplash`) on every enemy it breaks over - inside the doubled swath, since
  that is what the ascension bought. The default archetype's fire-orange tint is dropped for it;
  Spring Tide's lane now floods at the crest's own speed instead of racing ahead of it.
- **Black Tentacles** is a rift that slows and tentacles that slap. The rift (`tentacle_rift.gdshader`,
  per-2 px-cell, Bayer-dithered, Bonelight tones only) tears open on the densest pack, every enemy in
  it is slowed - `ApplySlow`, re-applied each 0.15 s scan with a short linger so the stacking-resistant
  rule holds - and the rift deals nothing. Tentacles (`WhipLimb`, stamped squares on the grid) burst
  up, each picks the nearest unclaimed enemy inside, winds up, lashes to it and lands: the target and
  anything within 40 px of the landing take one slap. **`CurrentRadius` is the one hit authority** and
  the shader draws to it; the old version grew the DRAWING by the ascensions' x2 / x1.7 scale while
  their area never reached the hit. Levels add tentacles (even), rift size and slow (odd), +1 slap
  damage every level - written out in full in the `.tres`. Amount adds tentacles. All five evolutions
  now do what their card says (Soul Drain and the Maw's vortex had no reader at all) and the cards
  were rewritten to say only that. **Elder God Maw** was redrawn: a lip of torn earth and wet flesh,
  three rings of teeth of uneven length turning against each other and sinking into black, a ring of
  small hollow eyes that open one by one once the maw is open, blink on their own clocks and all look
  at the player, and a pull toward the teeth. **2026-10-28, its summon** ("it should look like it's
  breaking through the earth and throwing earth aside"): the ground cracks out to the edge of the
  slow and heaves; at the breach the plates are thrown aside as tumbling slabs and clods, over the
  bodies, with their shadows on the ground and dust blown after them; the maw rises out of the hole,
  teeth growing out of the dark, and tips the ground up round it into a torn rim of slabs that the
  thrown earth lands on and lies on until it closes. Same 0.8 s, same slow - only its curve is eased
  out, because the cracks are the slow's edge and have to run ahead. **Two maws never overlap**
  ("they should never overlap"): a new one opens only where its full radius clears every open maw's
  by 8 px; if the pack is under a maw it opens beside it, on the stragglers, and if nowhere in range
  would catch anything the cast waits (`Player.TryPlaceMaw`, `BlackTentacles.MawFits`).
- **Thorned Vine** keeps id `thorn_vine` and every save key; only the name changed. One vine bursts
  from the ground on the player's side of the densest pack, coils, lashes across it in a 170 degree
  sweep and withers. It strikes enemies in the order its drawn line reaches them, cuts through
  pierce + 1 of them and then SNAGS on the last - so "Pierce +1" still means one more enemy, and the
  payload (several enemies hit and poisoned per cast) is what the bolt's was. The densest-cluster aim
  fits it better than it fit the bolt: a sweep at a straggler swings through air. Its five default
  evolutions keep their ids, numbers and element gains and are re-worded for a vine (reach, snag
  count, tempo, more vines).

Measured with the balance harness (`--spell=ID` and `--solo`, new) - see the commit for the tables.

Cinderbreath was the counter-example and is worth remembering as one: it drew itself at first and had to be rebuilt around a real sprite sheet, because a procedurally filled cone reads as a lit area and never as fire. Drawing your own shape is the right default for a zone, a ward or a trap - something that occupies ground. It is the wrong default for anything the player is supposed to watch travel.

---

## 2b. Element paths (2026-10-23)

"Other spells should have paths that change their elemental tags when leveling up, such as a water
element spell becoming water + poison later." The rule, its status table and its validator are in
`.ai/gameplay-design.md`, "Element paths". In short: every **pure** spell's level 4 offers a branch
that **deepens** its element and one that **adds** a different element (+1 tag of it, that element's
status on every hit, and its look); a **hybrid** already names two elements and may never name a
third, so its path is deepening either half. Level 8 only deepens.

**Pure spells.** "Path" is the branch that adds an element; the third level 4 option is untagged.
Deepening options marked (A) are the wizard's Apotheosis path and keep their ids.

| spell | base | level 4: deepens | level 4: element path - new tag, what every hit does | level 8 gain |
|---|---|---|---|---|
| Riptide | Water | Amplified Impact (A) | **Brackish Tide** - +1 Poison, +20% damage; poisons 3 dmg/s for 3 s. The wave's foam and water go Poison-green (`riptide_wave.gdshader`), splashes too | Titan Cataclysm +Water (A) |
| Chain Lightning | Lightning | Storm Reach (A) | **Storm Surge** (id `forked_surge`) - +1 Water; forks 3 ways at x0.7; slows 25% for 1.5 s. Arc in Water's hot tone with a wet glow on every hop | Cascade +Lightning (A), Thunderhead +Lightning |
| Cinderbreath | Fire | Amplified Impact (A) | **Hellbreath** - +1 Darkness, +20% damage; Vulnerable +10% for 2 s. Tongues remapped to the Darkness ramp (`element_ramp.gdshader`) | Titan Cataclysm +Fire (A) |
| Hollow Star | Darkness | Amplified Impact (A) | **Eclipse** - +1 Light, +20% damage; the collapse Weakens 30% for 3 s. Gold reach, arms and rim, a corona on the black core | Titan Cataclysm +Darkness (A) |
| Bramble Seed | Grass | Amplified Impact (A) | **Ironbriar** - +1 Metal, +20% damage; the first hit Withers 5% of max HP. Steel roots and thorns | Titan Cataclysm +Grass (A) |
| Contagion | Poison | Amplified Impact | **Marsh Fever** - +1 Water, +20% damage; infections slow 25% for 3 s. Marks and bursts in Water's ramp | Titan Cataclysm +Poison |
| Iron Palisade | Metal | Amplified Impact | **Lightning Rod** - +1 Lightning, +20% damage; cuts stagger 0.25 s. Lightning-lit stakes, sparks at the tips | Titan Cataclysm +Metal |
| Kindled Ward | Light | Amplified Impact | **Rimelight** - +1 Ice, +20% damage; slows 30% for 1.5 s. The ward burns in Ice's ramp | Titan Cataclysm +Light |
| Starfall | Light | Clear Night (+2 stars, +15% damage) | **Frostfall** - +1 Ice, +20% damage; slows 30% for 1.5 s. Every star falls, twinkles and bursts in Ice's ramp | Starburst +Light |
| Fireball | Fire | Pyroclasm | **Iron Comet** (id `fireball_comet`) - +1 Metal; the first hit Withers 5% of max HP. Metal tint | Hellfire Nova +Fire |
| Cyclone Slash | Wind | Tailwind Surge | **Razor Gale** - +1 Metal, +35% damage; the first hit Withers 4% of max HP. Metal tint | Typhoon Core +Wind |
| Arcane Explosion | Arcane | Lingering Rift | **Static Nova** - +1 Lightning, +20% damage; staggers 0.3 s. Lightning tint | Event Horizon +Arcane |
| Obsidian Spike | Earth | Splinter Field (A) | **Umbral Vein** - +1 Darkness; Vulnerable +10% for 3 s. Darkness tint | Tectonic Uprising +Earth (A) |
| Cone of Cold | Ice | Deep Freeze (A) | **Frost Shrapnel** - +1 Water, +30% damage; no rider (the cone already slows harder than Water's drag). Water tint | Zero Point Shockwave +Ice (A) |

The first eight are new, and Starfall (2026-10-25) was built with its path. The last five had a branch already, and every one of them promised a
mechanic its spell never read - Iron Comet's pierce, Razor Gale's crescents, Static Nova's sparks,
Umbral Vein's bleed, Frost Shrapnel's spikes - so each paid little more than its tag. Their cards now
say only what they do, plus the element's rider; the dead fields that still sit on them are commented.

**Hybrids** deepen either half; no third element is allowed (`ValidateEvolutionElementCeiling`).

| spell | base | deepens at level 4 | deepens at level 8 |
|---|---|---|---|
| Magic Missile | Arcane + Lightning | Unstable Detonation +Arcane | Arcane Gatling Barrage +Lightning |
| Aegis Ward | Metal + Light | Fortified Aegis +Metal | Sun Aegis +Light |
| Black Tentacles | Poison + Earth | Crushing Grip +Earth | Shadow Forest +Poison |
| Frost Shard | Ice + Water | Glacial Spike +Ice | Blizzard Engine +Water |
| Gale Blade | Wind + Lightning | Honed Edge +Wind, or Quickening +Lightning | - |
| Shadow Bolt | Darkness + Poison | Withering Mark +Darkness, or Creeping Rot +Poison | - |
| Solar Flare | Light + Fire | Scorching Heat +Fire | Daybreak Avatar +Light |
| Spiritual Weapon (each form, section 2f) | Arcane + Light | Dawnsplitter / Hallowed Ground / Soul Siphon +Light | Whirling Axes / Hammerfall / Thousand Cuts +Arcane |
| Void Lance | Arcane + Darkness | Gravity Spike +Darkness | Annihilation Beam +Arcane |
| Meteor Swarm | Fire + Earth | Cosmic Impact +Earth | Extinction Event +Fire |
| Thorned Vine | Grass + Poison | Amplified Impact +Grass | Titan Cataclysm +Poison |
| Toxic Spore Burst | Poison + Grass | Lingering Rot +Poison (Ember Caps trades Poison for Fire) | Mycelial Bloom +Grass |
| Glacial Spike, Gravewell, Hunter's Draw, Mirefoot, Molten Shard, Scorching Ray | their two tags | Amplified Impact deepens the first | Titan Cataclysm deepens the second |

Toxic Spore Burst was left on the default archetype here because it was being reworked elsewhere at
the time; it has its own block now (section 3). The last row's branches are the default archetype's,
each spelled out for its own spell since 2026-10-24 (section 2d).

---

## 2c. No area markers (2026-10-23)

The playtest: *"remove aoe markers from abilities, their animations should make it clear what the
hitbox is"*, and, of Gravewell, *"does the grave ability do anything? i couldn't tell what it was
supposed to do"*. The rule is in `.ai/art-direction.md` section 3 ("The player's spells carry no area
markers"); this is what it changed. Every replacement is drawn out of the number the hit tests, and
nothing is drawn past it. Enemy telegraphs were not touched.

| spell | the marker | what shows the reach now |
|---|---|---|
| Gravewell | faint disc at the trigger, a ring closing while it armed, a ring at the blast | generated art (`tools/art/gravewell.py`): a grave whose bank is the trigger; the collapse's cracks and heave run out to the blast radius (`gravewell_collapse.gdshader`); caught enemies are yanked in and held by heaped earth |
| Toxic Spore Burst | a ring at the burst radius through the whole swell, a flash ring | the fattening cap is the warning; spores thrown out from the cap, a ring of them landing on the rim |
| — Lingering Rot / Ember Caps | a flat disc and a rim under the cloud and every patch | the cloud is the burst's own puffs, turning in place; a patch is the flames that fill it |
| Hollow Star | a reach ring closing as it opened and held while it pulled; a ring at the collapse | arms grow out to the reach; motes dragged in from that rim; debris thrown to the collapse radius |
| Bramble Seed | a disc and a rim at the full radius | the roots alone - and they now stay full length until the patch stops hurting |
| Kindled Ward | a halo as wide as the strike | a small glow, and on every strike tongues of light out to the strike radius |
| Mirefoot | a stroked rim round each pool | the bog water to within a few per cent of the radius, a deeper middle, a bubble; since 2026-10-28 one opaque bog under the actors, the pools' union, its bank wandering only inward of the radius (section "Mirefoot", How the bog is drawn) |
| Meteor Swarm | two red rings growing to the blast for the whole fall - the one true preview | the meteor streaks in (the web demo's look, section 3); on impact a crater opens in the ground, its ejecta short of the radius, and burning rock is thrown to it |
| Cone of Cold, The Long Winter | a translucent wedge (a disc, for the Long Winter) with a bright arc | ice sprayed out across the cone to exactly its range |
| Solar Flare | squashed to a 0.42-high floor ellipse the circular hit is not; a scorch disc; particles flying past the radius | the same burst on the circle it hits; its leading front stops on the radius |
| Black Tentacles | a dithered disc and a dashed rim between the rift (two thirds of the radius) and the slow's edge | the rift opens to the whole slow; Shadow Forest's outer rifts have their lips on it |
| Sundering | a floor disc inside the pillars | the pillars; earth thrown out to the shove radius when it rises |
| Contagion | a ring opening at each jump | plague spores thrown to the spread radius |
| Frost Shard | a Line2D ring per kill that grew to x1.18 of the radius | ice thrown to the radius, on one pooled layer (`ImpactDebrisLayer`) |
| Cataclysm | an ember disc and a rim per crater | flames filling the crater; a detonation throws fire to its radius |
| Saltbound Chain, Rimebriar | a Line2D ring that grew to x1.12 of the radius | steel or frost shards thrown to the radius |

**Kept, and why.** Arcane Explosion's sheet, Riptide's foam, the tentacles' slap and Solar Flare's
front are one-shot shockwaves of the effect's own light at the moment of the hit that stop on the
radius - animations. Contagion's carrier marks and the Lattice's link halos are statuses on one
enemy. The Brazier's embers and Taproot's ring at the player's feet (`StanceGlow`) show a state, not an
area. Iron Palisade's stakes, Thorned Vine's sweep and Spring Tide's lane are the things that hit.

**The same bug, five more times.** Redrawing an effect out of its hit radius means the node can no
longer carry the evolutions' visual scale, and that exposed the Bramble Seed bug (`.ai/gameplay-design.md`)
in five more spells: their evolutions' Area grew the drawing and never reached the hit. It does now -
Gravewell, Kindled Ward, Contagion and Meteor Swarm read `GetAreaMultiplierAtLevel` into the radius they
test, and Cone of Cold (whose reach already did) no longer draws at its x2 visual scale on top. That is
a buff where the card promised Area: Meteor Swarm's Extinction Event ("obliterates the entire screen")
now has its x3 Area. Meteor Swarm's fixed four meteors are gone too (section 3: the volley is the
spell's projectile count plus Amount, so Meteor Shower's and Orbital Bombardment's "+3/+6 Meteors"
reach it). Solar Flare's evolution Area was the last of these, read since 2026-10-26 (section 2d).

---

## 2d. The default archetype, spell by spell (2026-10-24)

The player, of Gravewell: *"gravewell mutation has 'projectile speed' and 'pierce'. isn't it an aoe?
does that even make sense?"* It did not. Thirteen spells had no evolution block and fell through to
`SpellEvolutionCatalog.SetupDefaultArchetypeEvolutions`, whose five branches were written for a bolt:
Amplified Impact (+35% damage, +30% area), Hyper Acceleration (-30% cooldown, +25% projectile
speed), Reactive Catalyst (+20% damage, +2 pierce; a pure spell has its element path instead), Titan
Cataclysm (+150% damage, +100% area) and Infinite Horizon (-50% cooldown, +3 projectiles, +4
pierce). On most of the thirteen the words were wrong and the numbers reached nothing:

- **Speed reached none of them.** Even the bolts: `ElementalBolt` read the level table's flat
  projectile speed and never an evolution's `SpeedMultiplier`.
- **Pierce** reached only Riptide, Molten Shard and Hunter's Draw.
- **Projectiles** did nothing for Riptide or Molten Shard (`Player.FireBoltSpell` fired one bolt
  whatever the count), Hollow Star, Bramble Seed, Contagion, Iron Palisade, Mirefoot or Cinderbreath.
- **Area** did nothing for Cinderbreath, Glacial Spike, Iron Palisade, Mirefoot, Scorching Ray or the
  three bolts. Glacial Spike and Iron Palisade got Titan Cataclysm's x2 visual scale instead, which drew
  the spike twice as big as it hit, and made the wall twice as long *and* twice as thick.

Each has its own block now, in Thorned Vine's way. `SpellEvolutionCatalog.Branch` fixes what a block
may not change - the id, the name, the element gain, the attunement price of that gain and the look,
which `ApotheosisCatalog`, `RiptideWave.TitanEvolutionId`, `SeenAscensionIds` and `RegressionChecks`
name - so a block only changes what a branch does and says. The budget is the archetype's: a stat
that means nothing to a spell became one of like weight that it reads, with a small hook where the
spell had none. Pierce, which only a projectile can use, became each spell's version of "hits more":
the hold of a trap, the drag of a patch, the reach of a plague, the life of a wall.

| spell | Amplified Impact | Hyper Acceleration | level 4 third | Titan Cataclysm | Infinite Horizon |
|---|---|---|---|---|---|
| Gravewell | +35% dmg, +30% blast | -30% cd, pits open 25% faster | Reactive Catalyst: +20% dmg, held 0.5 s longer | +80% dmg, x3 blast | -50% cd, +3 pits, held 1 s longer |
| Hollow Star | +35% dmg, +30% reach and collapse (A) | -30% cd, drags 25% harder | Eclipse (path) | +150% dmg, x2 reach and collapse (A) | -50% cd, +3 wells on other packs, the knot rooted 1 s |
| Bramble Seed | +35% dmg, +30% radius (A) | -30% cd, grows 25% faster | Ironbriar (path) | +80% dmg, x3 radius (A) | -50% cd, +3 patches may stand, thorns slow 20% harder |
| Contagion | +35% dmg, jumps 30% farther | -30% cd, poison 25% faster (same dose, sooner) | Marsh Fever (path) | +150% dmg, jumps x2 | -50% cd, +3 carriers, each plague reaches 8 more |
| Iron Palisade | +35% dmg, wall 30% longer | -30% cd, cuts 25% more often | Lightning Rod (path) | +150% dmg, wall x2 long | -50% cd, +3 walls toward other packs, each stands 1 s longer |
| Kindled Ward | +35% dmg, burns 30% wider | -30% cd, drifts 25% faster | Rimelight (path) | +80% dmg, burn x2 | -50% cd, +3 wards may burn, each lasts 3 s longer |
| Riptide | +35% dmg, wave 30% bigger (A) | -30% cd, wave 25% faster | Brackish Tide (path) | +60% dmg, swath x3 (A), +40 pierce | -50% cd, +3 waves at other enemies, +4 pierce, 40% bigger |
| Molten Shard | +35% dmg, shards 30% larger | -30% cd, +25% projectile speed | Reactive Catalyst: +20% dmg, +2 pierce | +80% dmg, shards x3, +8 pierce | -50% cd, +3 shards at other enemies, +4 pierce, 40% larger |
| Hunter's Draw | +35% dmg, arrowheads 30% broader | -30% to nock, arrows 25% faster | Reactive Catalyst: +20% dmg, +2 pierce | +80% dmg, arrows x3, +8 pierce | -50% to nock, +3 arrows a volley, +4 pierce, 40% larger |
| Glacial Spike | +35% dmg, spikes 30% wider | -30% cd, erupts 25% faster | Reactive Catalyst: +20% dmg, pins 0.4 s | +80% dmg, spikes x3 wide | -50% cd, +3 spikes, pins 0.6 s more |
| Scorching Ray | +35% dmg, each ray bursts on its target for 50% | -30% cd, rays strike 25% faster | Reactive Catalyst: +20% dmg, burns through to 2 behind | +80% dmg, a burst x3 wide, enough for a pack | -50% cd, +3 rays, burns through to 4 behind |
| Cinderbreath | +35% dmg, tongues 30% wider (A) | -30% recharge, tongues thrown 25% faster | Hellbreath (path) | +80% dmg, tongues x3 wide (A) | -50% recharge, three tongues a throw |
| Mirefoot | +35% dmg, pools 30% wider | pools laid 30% closer, bite 25% more often | Reactive Catalyst: +20% dmg, slows 15% harder | +80% dmg, pools x3 wide | pools laid x2 close, +5 pools in the trail, each lingers 1.5 s longer |

(A) is a wizard's Apotheosis path; its id, element gain and numbers are unchanged. (Hollow Star's,
Bramble Seed's, Contagion's and Iron Palisade's rows are history since 2026-10-29: each now has four
cards of its own, section 2e.) Notes on the choices that are not one-for-one:

- **The bolts carry Area as their scale.** A bolt's size is its node's scale - the collision circle
  and its drawing grow together (`RiptideWave` already drew from it) - so Riptide, Molten Shard and
  Hunter's Draw express "+30% area" as `ScaleMultiplier` 1.3, at the archetype's number. Titan's x2
  was already a x2 scale; its unread `AreaMultiplier` went.
- **The spells that draw their own hit drop the visual scale** - Iron Palisade, Glacial Spike and
  Scorching Ray now as well as the eight that already did - so their Area is in the number they test
  and the drawing follows it. Iron Palisade's wall grows by length alone and draws more stakes at the
  same spacing; Scorching Ray's burst is thrown out to its exact radius on the arena's debris layer.
- **Cinderbreath's Area is width, never reach.** A flamethrower that outranged a bolt would be a
  better bolt (`Flamethrower.BaseRange`), so the evolutions widen each tongue (its hit and its sprite).
  A tongue already burns everything it touches, so Infinite Horizon's projectiles and pierce are two
  more tongues a throw together: three at once.
- **Mirefoot's cooldown is spacing**, and its cards say "laid closer" rather than "cooldown".
- **Bramble Seed still sows one seed a cast.** Scattering seeds is Wildwood's verb (Vainamoinen's
  Apotheosis); Infinite Horizon raises how many patches may stand, which also fixes the trap it was:
  with the cap at three, a halved cooldown withered every patch before it had grown.
- **Amount is not read by any spell that newly reads a count** (wells, walls, carriers, patches,
  pools, tongues, the bolts), as it never was by the bolts - Thorned Vine's rule.
- **Two side effects of reading Zone Duration:** Kindled Ward's and Mirefoot's generated level 6 line
  ("Zone duration +0.45s"), which read nothing, now does - a ward lasts 5% longer, a pool 10%.

**The reader check.** `scripts/EvolutionStatReaders.cs` says, for every spell, which
`SpellEvolutionOption` fields its script reads and where, and
`RegressionChecks.ValidateEvolutionReaders` reports any level 4, level 8 or Apotheosis option that sets
a field its spell does not: the warning names the option, the field and the reads. A spell with no
entry is reported too, so the next spell to land on the default archetype is caught the first time
the validator runs. A table cannot read C#, so the entries are written by hand from the scripts; a
spell that starts or stops reading a field updates its row.

The hand-written blocks were held to it as well. Thirty-one fields their cards never mentioned were
removed, and the 1.15 scale of the four Apotheosis forms whose spells drop it, which changed nothing a
player could see - among them Fireball's Iron Comet pierce and speed, kept "for whenever Fireball
learns to pierce", and every visual scale on a spell that drops it or is only ever scaled once. Gale Blade's Quickening and Tempest Rhythm
speeds came true with the bolt hook: its blade flies 25% and 40% faster. What is left is fifty numbers
that hand-written cards promise and their spells never built - a burn, a freeze, a pull, a wider area,
more meteors - listed with the promise in `EvolutionStatReaders.KnownUnbuilt`. That list is a backlog,
not an allowlist to grow: an entry that comes true is reported as stale, so it can only shrink. The
worst of it: Frost Shard's five cards are almost all a bolt's, and none but the damage reaches its
shatter; Cone of Cold's Deep Freeze - the Frostweaver's required Apotheosis path - freezes nothing;
Chain Lightning's Storm Reach and Cascade - the Stormcaller's - promise a wider jump the chain does
not read; and Solar Flare, Fireball, Spiritual Weapon, Cyclone Slash and Void Lance promise area their
scripts do not read.

**Closed 2026-10-26: the list is empty.** Every one of the 46 was built or its card rewritten to say what
it pays. Built: Deep Freeze (1.2 s, not 2.0, on a 1.8 s cone), Storm Reach and Cascade's jump, Event
Horizon's pull and freeze, Lingering Rift, Pyroclasm's magma pool, Cluster Bombs, Soul Siphon (4 HP a
cast), Vortex Reaper, Gravity Spike, Cosmic Collapse, Singularity Core, Thorn Crystal Shell, and every
knockback card through one conversion, `SpellData.GetKnockbackSpeedAtLevel`; Effect Freeze through
`GetFreezeSecondsAtLevel`. Frost Shard's five cards were rewritten around the shatter (slow, freeze,
flung shards, a bigger burst). Burn stayed unbuilt as a status - the game has none - so the cards that
promised one either leave burning ground (Pyroclasm) or say they pay damage instead. Void Lance now
hits at the size an evolution draws it. Two checks keep it so: `ValidateEvolutionReaders` for the
milestone cards and `ValidateLevelUpReaders` for levels 2-8, which found 34 level-ups (mostly scaling
tags' generated lines) that reached nothing. Neither reads prose: a read of all 182 cards against the
code found 22 whose text promised more than the code did; three were built (Arctic Blast's 180 degrees,
Sun Aegis's break flash, Reflective Barrier) and the rest reworded. **A card's words need the same care
as its numbers - no check sees them.**

**Measured** (`_BalanceHarness --timescale=3 --immortal --all-unlocked --character=0 --spell=<id> --solo
--upgrade-chance=1 --branch4= --branch8= --force-ascension`, one run at a time, with temporary prints of
what each hook computed). Every hook was seen reaching its spell: pits opening in 0.44 s and holding
2.45 s, four pits, wells and walls and carriers a cast, a well dragging at 300 px/s and holding its
knot 1 s, patches capped at six growing in 4.8 s and slowing to 0.4, plagues at rate 1.25 reaching 32,
walls cutting every 0.4 s and standing 6 s at scale 1, wards drifting at 119 and lasting 12.5 s, the
wave at 431 px/s. Damage a second at level 8, seven-minute runs on a machine other sessions were
loading, so read them as a band:

| spell | level 4 / level 8 | L8 damage/s |
|---|---|---|
| Gravewell | Hyper Acceleration / Infinite Horizon | 323 |
| Hollow Star | Hyper Acceleration / Infinite Horizon | 395 |
| Bramble Seed | Hyper Acceleration / Infinite Horizon | 329 |
| Contagion | Hyper Acceleration / Infinite Horizon | 343 (241 attributed; the rest is poison) |
| Iron Palisade | Hyper Acceleration / Infinite Horizon | 314 |
| Kindled Ward | Hyper Acceleration / Infinite Horizon | 221 |
| Glacial Spike | Amplified Impact / Infinite Horizon | 316 |
| Cinderbreath | Amplified Impact / Infinite Horizon | 256 |
| Iron Palisade | Amplified Impact / Titan Cataclysm | 566 |
| Scorching Ray | Amplified Impact / Infinite Horizon | 470, over a 7 s sample: it reached level 8 at 4.9 minutes |

The five-minute runs (the last four rows) also saw Glacial Spike's eruption grow 70 -> 91 px with
Amplified Impact and pin for 0.6 s, Scorching Ray's 39 px burst and its beams burning through four,
Cinderbreath's tongues at x1.3 width and three a throw, and Iron Palisade's wall at 130 then 260 px
half-length with no visual scale. **Iron Palisade's Amplified Impact + Titan Cataclysm is the one at the
top of the band**: the two Areas now multiply into a wall 2.6x long, where Titan's scale alone made it
2x long and 2x thick. Gravewell's Titan measured 536 in the 2026-10-23 table, so it is not out of the
family, but it is the first to trim if the Metal wall proves too much. The solo bolts (Riptide, Molten
Shard) never reached level 8 in a short run - a solo bot levels slowly on one bolt - and Mirefoot
deals nothing under a bot that holds its ground (`--walk` walks, but collects no XP and never levels),
so those were checked at level 8 on a staged crowd instead: `_UiShot --character=0
--start-spell=<id> --demo-spell-level=8 --demo-evolutions=<l4>,<l8> --spell-stage=8` (Mirefoot with
`--walk=1,0`). Molten Shard and Riptide fired volleys of four (Riptide at scale 1.82, Amplified
Impact's 1.3 times Infinite Horizon's 1.4); Mirefoot's pools were 92 px biting every 0.36 s (Hyper
Acceleration + Titan), and with Reactive Catalyst + Infinite Horizon slowed to 0.45, lasted 6.45 s,
capped at 15 and were laid 21 px apart; Glacial Spike erupted at x1.25 to a 168 px spike, Scorching
Ray struck x1.25 faster with a 60 px burst, and Cinderbreath threw x1.25 faster at x2 width. Hunter's
Draw's speed is the same ElementalBolt hook as the two bolts.

---

## 2d-bis. Spells that guard against bolts (2026-10-28)

Eric: *"a few spells should help guard you from projectiles, like familiar being able to catch
projectiles"* and *"one of the spells that can protect you from projectiles should be an ascension
for an aoe spell around you that destroys projectiles, like garlic does in vampire survivors."*

Three guards, all reading one list - `EnemyProjectile.Live`, every enemy bolt in flight, kept by
entering and leaving the tree - and all taking a bolt out with `EnemyProjectile.Destroy(element)`,
which deals nothing and bursts in the guard's element:

| guard | what it takes | the limit | measured (Cursed Dungeon, the spell alone, immortal bot, 6 min) |
|---|---|---|---|
| **Familiar** (base spell; the dog since 2026-10-28) | a bolt coming at the wizard inside 260 px, that a free hound can meet within 170 px: it leaps and catches it | each hound then rests 1.6 s; two hounds never chase one bolt (`ClaimedBy`) | 140 caught; damage taken 5,983 -> 5,086 |
| **Familiar - the crow** (section 2g) | a bolt inside 320 px, met within 230: taken on the wing | rests 1.5 s | 254-264 caught (Enchanted Forest, 8 min, seed 11 - the dog 401 there) |
| **Familiar - the cat** (section 2g) | a bolt inside 170 px, met within 110: batted away right at his feet | rests 1.0 s - the most often, the closest in | 273 caught (the same run) |
| **Spiritual Weapon** (base spell; the orbiting blades until 2026-10-28) | any bolt a circling blade passes through | only while the blades are out, only what they touch | 164 parried |
| **Spirit Sword** (the form, section 2f) | any bolt that touches a sword's blade - hanging at his shoulder, hunting or swinging | only where the swords are; Riposte widens the guard 60% and every parry is a strike | see 2f |
| **Spirit Hammer** (the form) | every bolt inside a slam's shockwave | only where and when a slam lands | see 2f |
| **Solar Flare - Daybreak Avatar** (Ascension) | every bolt inside the flare's reach this moment, between pulses too - the garlic | only inside the flare | 202 burned |

Daybreak was one of the Ascensions that only scaled numbers (2e); this is its verb now. The Familiar's
pack also changed shape: one hound to start, the second at level 3, a third with Littermates, and
Amount adds none (`Player.FireFamiliar`).

## 2e. Every spell its own evolutions - PROPOSAL, not built (2026-10-28)

Eric: *"Every spell should have its own unique mutations and ascensions based on that spell's unique
characteristics and looks. are all of the mutations/ascensions not unique?"*

**Partly.** Audited card by card on 2026-10-28 (35 active spells, 175 cards):

- **No id is shared.** Every evolution is `<spell>_<branch>` (`SpellEvolutionCatalog.Branch`), and
  each card's text was rewritten per spell in 2d. What 14 spells share is five NAMES, with the tint and
  look that go with each: Amplified Impact, Hyper Acceleration, Reactive Catalyst, Titan Cataclysm and
  Infinite Horizon - 63 of the 175 cards, and 28 of the 70 Ascensions are one of the last two.
- **21 spells have five cards written only for them.** 7 (Riptide, Hollow Star, Bramble Seed,
  Contagion, Iron Palisade, Kindled Ward, Cinderbreath) have the four template names plus their own
  element path. 7 are template-named on all five: Thorned Vine, Gravewell, Glacial Spike, Molten
  Shard, Hunter's Draw, Scorching Ray, Mirefoot.
- **A spell's own name is not a spell's own change.** About 40 of the 175 cards change how a spell
  looks or behaves; the rest move numbers. Toxic Spore Burst (all five) and Familiar (four) are the
  model. Gale Blade, Shadow Bolt and Spiritual Weapon are hand-written and still almost all numbers.
- **No evolution has its own card icon**: each card shows the spell's icon tinted, so every Titan
  Cataclysm is the same orange.

**The rule this proposes:** every card says, in its name and its first line, a change the player can
SEE on that spell - a new shape, a new behaviour, something left behind - drawn from what the spell
already is. Numbers ride along; they are never the card. One verb per spell family stays reserved
(the pull is Hollow Star's; Apotheosis verbs are `ApotheosisCatalog`'s).

**What must not move:** the ids. Four wizards' Apotheosis paths require `<spell>_amplified_impact`
and `<spell>_titan_cataclysm` (`ApotheosisCatalog.cs`), `RiptideWave.TitanEvolutionId` reads one, and
`SeenAscensionIds` is keyed by them. Renaming and re-specifying a branch keeps its id, so saves, the
browser and the gates carry over. The element-path cards (the pure spells' level 4 third) are already
each spell's own and stay.

### The seven with nothing of their own

| spell (what it is) | level 4 (in place of AI / HA / RC) | level 8 (in place of TC / IH) |
|---|---|---|
| **Glacial Spike** - a spike erupts under a target, cracks, withdraws | **Shatter**: as it withdraws it bursts into a ring of ice shards that fly out. **Spike Run**: three spikes race along the ground to the target, one after another. **Rimefoot**: leaves a frozen patch for 3 s that slows whatever crosses it | **Glacier Crown**: eight spikes erupt in a ring round the target and stand 1.5 s - the pack inside is penned. **Frozen Spire**: one spike three times as tall that stands 5 s as a pillar; touching it freezes |
| **Molten Shard** - a homing shard at a random enemy | **Slag Trail**: its flight leaves a line of burning slag for 2 s. **Splitting Shard**: on its first hit it splits in two that home on new targets. **Magma Core**: it lodges in the target and erupts 0.8 s later | **Caldera**: the shard dives into the ground and a lava pool wells up there for 4 s. **Forge Swarm**: shards gather in orbit round you as they are made, then all launch at once at different enemies |
| **Hunter's Draw** - charge while moving, loose a volley when you stop | **Heavy Draw**: a full draw's arrows knock back and stagger. **Running Shot**: while moving, a single arrow every few steps at the nearest; the volley still comes on stopping. **Barbed**: arrows lodge and bleed | **Rain of Arrows**: the volley goes up and falls as a field over the pack nearest where you stopped. **Hunter's Mark**: a full draw marks the strongest enemy in range; the whole volley seeks it and it takes more from everything |
| **Scorching Ray** - a beam that ramps on a held target (**built**, below) | **Flashpoint** (today's AI, which already bursts): at full heat the target bursts. **Handoff**: when the held target dies the ray jumps to the nearest, keeping its heat. **Burn Through**: the beam runs on past the target to the screen's edge | **Prism**: at full heat the ray splits into three rays at three targets. **Sunlance**: at full heat it widens into a lance that sweeps from target to target |
| **Thorned Vine** - a whip vine that lashes a pack | **Barbed Lash**: the lashed bleed. **Snapback**: the whip cracks out and back, hitting twice. **Entangle**: the front of the pack is rooted 0.6 s | **Thicket**: where the whip lands, thorny vines stay on the ground 3 s. **Hydra Vine**: three vines lash three packs at once |
| **Gravewell** - a proximity trap that roots | **Bone Spikes**: when it springs, bone spikes throw out round it. **Quickset**: wells arm at once, and can be laid on the move. **Grave Chill**: the rooted are Weakened | **Mass Grave**: one great pit that roots everything in a wide circle and drains them. **Grave Line**: wells laid in a line along your path, a minefield behind you |
| **Mirefoot** - bog pools laid as you walk | **Sucking Mud**: the first enemy into a pool is rooted briefly. **Long Stride**: pools join into a continuous trail. **Fen Gas**: pools breathe poison clouds | **Great Bog**: when you stop, the pools near you join into one marsh that slows everything in it. **Will-o'-Wisps**: wisps rise from the pools and seek enemies |

#### Built: Thorned Vine, Glacial Spike, Gravewell, Mirefoot (2026-10-29)

Every card of the four is now its own. The ids, and with them the element gains (the first element
deepens on the old Amplified Impact's id, the second on Titan Cataclysm's, each at the hybrid's 12%),
are unchanged, so saves, `SeenAscensionIds` and the browser carry over; none of the four is on an
Apotheosis path. Each hook is read by id in the spell's script (`ThornVine`, `IceSpike`,
`GravewellTrap`, `TrailWeaver`, and their `Player.Fire*`), every number has its reader row, and every
card has its own icon. What each one now does, and where it left the table above:

- **Thorned Vine.** **Barbed Lash** (AI, +Grass): the game has no bleed and the vine already poisons,
  so the bleed is a **stride**: a barb tears again for 35% of the lash every 48 px its enemy walks, for
  4 s, leaving a splash of sap each time (`ThornBarbs`) - a pinned enemy pays nothing. **Snapback**
  (HA): after the lash it tears free and sweeps the same arc back, cutting through as many again.
  **Entangle** (RC): what it cuts is rooted 0.6 s, the vine coiled round its feet; +20%. **Thicket**
  (TC, +Poison): the band the first stroke swept stays a crescent of brambles 3 s that cuts for 40% of
  the lash every 0.5 s, poisons and slows 40% (`ThornThicket`, on the ground, never on blocked
  ground); +60%, 30% more reach. Thinned after the first look in play: every lash laid one, three stood
  at once and a long fight carpeted the ground in looping strands, so at most **two** stand (another
  withers the oldest) and they are drawn as solid stems. **Hydra Vine** (IH): one root comes up **in
  the thick of the pack** and splits into three heads, each lashing its own third of the ring round it -
  one root, not three vines at three packs, which the old Infinite Horizon already did; heads 20%
  longer, -40% cooldown, +2 cut through. A vine whose root would land in water or a wall comes up
  under the pack.
- **Glacial Spike.** **Shatter** (AI, +Earth): as each spike withdraws, 6 ice splinters fly out past its
  rim, each breaking on the first enemy it meets for 50% (`ThrownShards`); +20%. **Spike Run** (HA): the
  cast's spikes, and 2 more, rise one after another in a line **across the front of the pack**, the
  middle one under the target - not from the wizard to the target, which crossed open ground (nothing
  is nearer than the nearest enemy), and not behind it, which is Obsidian Spike's Basalt Lance.
  **Rimefoot** (RC): every spike leaves its footprint frozen 3 s, slowing what crosses it 45%
  (`FrostPatch`, `frost_patch.gdshader`, on the ground); +20%. **Glacier Crown** (TC, +Ice): **twelve**
  smaller spikes, not eight - eight stood like fence posts with a body between them - erupt in a ring
  round the target and stand 1.5 s; whatever is in the band of the ice is shoved back to the side it
  started on (`GlacierCrown`, a knockback, so a boss shoulders out), the crown strikes each enemy once,
  and it is moved off the wizard so he is never inside his own pen; +150%. **Frozen Spire** (IH): the
  spike under the target rises **twice** as tall (three times was near 300 px at level 8 - the spike
  already grows with its Area - and hid the knot) and 40% wider, stands 5 s as a pillar sorted among
  the bodies, and strikes and freezes 0.8 s whatever touches it, each enemy at most once a 1.2 s; -40%
  cooldown, +80%. Every spike now grows from its foot, so a big one no longer sinks its base below the
  enemy it rises under.
- **Gravewell.** **Bone Spikes** (AI, +Earth): as a grave springs it spits 8 bones out past the
  collapse, each breaking on the first enemy for 50% (the same `ThrownShards`, drawn in bone); +20%.
  **Quickset** (HA): graves tear open in 0.08 s, earth flung off them, already armed, hard in front of
  each enemy (30 px, not 75) - "laid on the move" read as Grave Line's, so the card is the ambush; -30%
  cooldown. **Grave Chill** (RC): what the grave holds is Weakened 30% while held and 2.5 s after,
  breath smoking off it; +20%. **Mass Grave** (TC, +Darkness): one great grave a cast, opened under
  the front of the swarm - bank twice as wide, collapse 2.2 times - holding 0.6 s longer and draining
  what it holds for 30% every 0.35 s, the dark drawn down into the pit; no pull (Hollow Star's); +80%.
  **Grave Line** (IH): the cast's graves, and 3 more, dug 92 px apart along the path the wizard just
  walked (`GravePath`), or out toward the nearest enemy when he stood still; -50% cooldown, +80%. No
  grave is dug on blocked ground any more.
- **Mirefoot.** **Sucking Mud** (AI, +Water): the first enemy into each pool is held 1 s, the mud
  heaped round its feet (`LingeringZone.GripSeconds`, drawn by `MireSurface` over the feet); +20%.
  **Broad Wake** (HA), renamed from **Long Stride**: "pools join into a continuous trail" came true for
  every trail on 2026-10-28, when overlapping pools became one bog, so it would have shown nothing - each
  stride now lays its pool with two at 60% beside it, a wake three across (never in water; the trail
  holds three times the pools, at most the 32 the surface draws). **Fen Gas** (RC): the pools breathe
  clouds of marsh gas in turn that hang 2.2 s and poison what is under them (`FenGas`, in the air).
  **Great Bog** (TC, +Grass): stand still a moment and a marsh three pools wide wells up round you,
  slowing to 30%, again every 1.5 s while you stay; walking, pools 40% wider and +60%. **Will-o'-Wisps**
  (IH): marsh lights rise out of the pools in turn and fly at the nearest enemy, breaking on it for 2.5
  bites (`WispSwarm`); the trail holds 5 more pools, each 1.5 s longer, laid 30% closer; +50%.

**Measured** (`_BalanceHarness` seed 11 unless marked, solo, immortal, timescale 2). The machine was
carrying three other agents' runs - load 12-40 on four cores, frames 16-50 ms against 14-20 for the
before runs - so read these as a band. Ratio: four minutes, cheap levels, `--enemy-hp=30` (Thorned Vine
at 10, where its solo poison still levels), level 7 against the Ascension; Mirefoot circling
(`--circle=300`), whose bot stuck on terrain in about half its runs (those were rerun or dropped). A *
marks a run that cleared the field, where the ratio is a floor:

| spell | level 7, before / after | first Ascension, before / after | second Ascension, before / after |
|---|---|---|---|
| Thorned Vine | 75 / 79 | Titan 151 (x2.0) / Thicket 266 (x3.4) | Infinite Horizon 478* / Hydra Vine 380 (x4.8) |
| Glacial Spike | 896 / 637 (546 without Shatter's +20%) | Titan 3,000* / Glacier Crown 1,142 (x2.1 of 546) | Infinite Horizon 2,781* / Frozen Spire 1,982 (x3.6 of 546) |
| Gravewell | 420 / 290 (242 without Bone Spikes' +20%) | Titan 1,628 (x3.9) / Mass Grave 1,603 (x6.6 of 242) | Infinite Horizon 1,989* / Grave Line 1,657 (x6.8 of 242) |
| Mirefoot | 1,585 / 1,051 (seed 13) | Titan 2,912*-3,067* / Great Bog 2,089-2,163 (x2.1) | Infinite Horizon 2,037-2,127 (x1.3) / Will-o'-Wisps 1,977-2,100 (x1.9-2.0, seeds 12 and 13) |

Eight minutes at normal health (`--upgrade-chance=1 --force-ascension`, three level 4 / level 8 pairs
a spell), damage a second by level, before / after, against the peer bands (40-60 / 70-130 / 331-465):

| spell | L4 | L6 | L8 |
|---|---|---|---|
| Thorned Vine | 24-28 / 32-37 | 52-67 / 50-74 | 131-323 / 248-352 |
| Glacial Spike | 34-43 / 37-51 | 71-97 / 67-99 | 439-502 / 368-465 |
| Gravewell | 45-58 / 38-61 | 76-105 / 84-110 | 470-506 / 415-431 |
| Mirefoot | 30-55 / 36-43 | 78-110 / 79-100 | 264-425 / 330-466 |

Reading it: the old Titan Cataclysms of Glacial Spike and Mirefoot were a spike and a pool three
times as wide - nine times the ground - which is why they cleared the x30 field; the cards that
replaced them do something instead of covering the screen, and were set to the floor (Glacier Crown
from +50% to +150%, where it measured below level 7; Will-o'-Wisps took +50% from x1.57, and sits at the floor within the runs' spread). In the
eight-minute runs every path now sits in or near the level 8 band, where Glacial Spike and Gravewell
sat over it and Thorned Vine's Titan well under it. Shatter and Bone Spikes measured 58-61% of the old
Amplified Impact at level 7 with nothing but their splinters and took +20% each; they are the first
knob if the level 4 band runs low in play.

**What still needs eyes:** all of it in a real crowd, and the phone - the pen's shove at the ring,
whether a field of spires reads or clutters, the barbs and the sap at a glance, Broad Wake's flanks and
Great Bog's marsh (three pools wide - most of a phone's width - and drawn by the same surface) for
fill cost, Fen Gas and the wisps over the bog, and the Mass Grave's art drawn at x4.

### The seven with four template names (their element-path card stays)

| spell | AI | HA | TC (Ascension) | IH (Ascension) |
|---|---|---|---|---|
| **Riptide** | **Breaker**: the wave knocks back hard | **Crosscurrent**: the wave leaves a current 2 s that carries enemies sideways | **Tidal Wall** (today's look, renamed) | **Spring Tide**: three waves in a fan |
| **Hollow Star** | **Crush**: the collapse hits harder for every enemy in the knot | **Twin Stars**: two wells, the knots pulled toward each other | **Black Sun**: the star stays and collapses three times | **Nova Seed**: the collapse throws out shards that knot the next pack |
| **Bramble Seed** | **Burrs**: thorns cling and slow the ones that leave | **Runners**: the patch sends out runners that seed a second patch | **Bramble Keep**: one patch three times as wide with a wall of thorns at its rim | **Seedstorm**: seeds fall in a scatter round you, a patch wherever they land |
| **Contagion** | **Fever Burst**: a carrier that dies bursts, dosing all near it | **Quickening**: the plague jumps the moment it is cast, not on death | **Pandemic**: every carrier on screen bursts at once, every few seconds | **Plague Wind**: a drifting cloud that doses all it passes |
| **Iron Palisade** | **Spiked Rail**: enemies that touch the wall are shoved back and bleed | **Quick Raise**: the wall goes up where you stop, in a ring round you | **Iron Ring**: a closed ring of stakes round the pack nearest | **Rolling Wall**: the wall marches forward, carrying what it meets |
| **Kindled Ward** (**built**, below) | **Flare**: the ward bursts when it first meets an enemy | **Wisp Trail**: it leaves embers on its path | **Pyre** (today's bigger burn, renamed) | **Kindling Flock**: three wards circling you instead of one drifting |
| **Cinderbreath** (**built**, below) | **Backdraft**: the cone ends in a burst at its tip | **Ember Wake**: burning ground where the cone swept | **Dragon's Breath**: the cone doubles in reach and leaves a firewall | **Fire Fan**: three cones at once, fanned |

#### BUILT 2026-10-29: Hollow Star, Bramble Seed, Contagion, Iron Palisade

Eric approved the table (2026-10-29: *"yes lets make all mutations more unique per spell"*). The ids
did not move (`<spell>_amplified_impact` and so on), so John Dee's path (Crush, then Black Sun),
Vainamoinen's (Burrs, then Bramble Keep), saves, `SeenAscensionIds` and the browser carry over; each
card's name, words, look and icon are its own (the template tints are gone - each draws in its own
element). The behaviour is in the spell's script by id (`HasEvolution`, ids as constants there);
the numbers on the card ride the stat fields `EvolutionStatReaders` lists for it. Every new drawing is
on a `DrawLayers` layer: what flies or clings in `Air`, what lies on the ground under the bodies.

- **Hollow Star** (`HollowStar.cs`, `Player.FireHollowStar`). Built as proposed - the pull is its own.
  - **Crush** (L4, was +35% damage and +30% area): the collapse hits +6% harder for every body in it,
    up to +90% at fifteen; the core swells a third wider as the knot fills and throws more debris.
  - **Twin Stars** (L4, was -30% cooldown and a 25% harder drag): two wells a cast, the twin on a pack
    one to three and a half reaches off; a filament of the dark is strung between them and they drift
    together over the pull, so the two knots are dragged into one and both collapses land on it. Drag
    +15%. On a field with one knot it opens one.
  - **Black Sun** (L8, was +150% damage and twice the reach): the star does not go out - it pulls and
    collapses three times (each re-pull 60% of the first), dark flares licking off its rim and a mote
    circling the core for each collapse left. Half again as wide, +60%. With the Spirit Mirror on top,
    each of the three collapses draws the loot in.
  - **Nova Seed** (L8, was -50% cooldown, three more wells and the knot held 1 s): each collapse
    throws three shards of the star (in the air) at the nearest packs outside it, out to three reaches;
    each opens a well 65% the size for 60% of the damage that pulls for 60% as long, and never throws
    again. -40% cooldown, +20%. With Twin Stars only one of the pair throws.
- **Bramble Seed** (`BrambleSeed.cs`, `Player.FireBrambleSeed`). No card sows another patch: a planting
  that becomes several is Wildwood's verb (swarm, Vainamoinen's Apotheosis), and the 2d note already
  held Bramble Seed to one seed a cast for that reason.
  - **Burrs** (L4, was +35% damage and +30% area): what walks out of the thicket carries burrs, drawn on
    it, for 2.5 s that slow it as the thorns do and prick it for half a tick. +25% damage, 15% wider.
  - **Runners** (L4; was -30% cooldown and 25% faster growth): once half grown, the thicket sends three
    thorned runners creeping over the ground (90 px/s) to the nearest enemies past its rim, up to 220 px
    beyond it, stopping short of water and walls; what stands on one is cut and slowed as if inside.
    Grows 25% faster. **Departed from the proposal:** a runner does not seed a second patch (Wildwood).
  - **Bramble Keep** (L8, was +80% damage and three times the area): one great thicket three times as
    wide, ringed by a hedge of thorn bushes at its rim (none drawn on water or walls). What is in the
    hedge's outer band is cut 150% harder and slowed to 0.3. It stands while the wizard stays inside it -
    each cast with him in it tends it for another cooldown and a half instead of sowing - and walking out
    of it withers it and sows the next at his feet. Without that, a keep that takes six seconds to grow
    was withered by the next cast before it had grown. Only a patch sown as a keep is tended (the first
    build tended the plain patch the wizard was standing in when the card was taken, for good). The
    hedge's bushes stand chest-high on a skeleton; smaller, at a keep's size, they read as stubble.
    +80% damage.
  - **Thornstorm** (L8, was -50% cooldown, three more patches, 20% harder slow, +160%): every grown
    thicket throws a ring of 24 thorns out to twice its width every 0.8 s; a thorn hits what it is drawn
    flying through, never the whole ring (the first build's 16 thorns 11 px wide hit 0-2 a volley). Two
    more patches may stand, -30% cooldown, +70%.
    **Departed from the proposal:** Seedstorm (seeds scattered round you, a patch where each lands) was
    Wildwood's verb outright.
- **Contagion** (`Contagion.cs`, `Player.FireContagion`). The marks stay on the floor, everything new is
  in the air over the bodies.
  - **Fever Burst** (L4, was +35% damage and a 30% longer jump): a carrier that dies bursts - everything
    in the jump is hit for 60%, carrier or not - and leaves a fever cloud hanging where it fell for 1.5 s
    that infects whatever walks into it. +25%. **Departed:** the proposal's "dosing all near it" was what
    a death already did; the hit and the cloud are what is new. At 80% it was 40% over the card it
    replaced at level 7 against a crowd it could not clear.
  - **Outbreak** (L4, was -30% cooldown and the poison 25% faster): the first carrier bursts the moment
    it is infected, through its own pack (60% of a jump), and still jumps again when it dies.
    **Departed:** named Outbreak, not Quickening, which is one of Gale Blade's cards; the burst's reach
    was cut to 60% of a jump after the first build measured twice its peers at level 4.
  - **Pandemic** (L8, was +150% damage and a jump twice as far): every 2 s every carrier on the field
    bursts at once without dying, on one shared beat - the marks swell before it - infecting what is
    round it and hitting it for 100%, at most once an enemy a beat however many carriers stand round it.
    Jumps 50% farther, +80%.
  - **Plague Wind** (L8, was -50% cooldown, three more carriers, +8 reach, +65%): each cast also looses
    a thin cloud of the plague that drifts out from a stride ahead of the wizard (loosed on his own spot
    it covered him) through the carrier's pack and on for 5 s, infecting what it passes into that
    plague; a maze wall breaks it up. Each plague reaches 8 more, -40% cooldown, +60%.
- **Iron Palisade** (`IronPalisade.cs`, `Player.RaiseIronPalisade`). The stakes are on `Air` (they were
  at z 1, over the bodies already), the caltrops on `Ground`.
  - **Spiked Rail** (L4, was +35% damage and a 30% longer wall): a spiked rail runs along the stakes; what
    touches the wall is thrown back 80% harder and bleeds - drops fall from it - for 2 s, a quarter of a
    cut every half second. +20%, 15% longer. The bleed was half a cut at first: weak on the 8-minute run
    (the cut kills before the bleed matters) and 35% over the card it replaced against a crowd that lives.
  - **Caltrops** (L4, was -30% cooldown and cuts 25% more often): when the wall comes down its stakes
    break into caltrops strewn on its far side (inside a ring) for 3.5 s, cutting what walks over them for
    75% every half second and slowing it 50%. -25% cooldown. **Departed:** Quick Raise put the wall up in
    a ring round the wizard - Sundering's verb (hold), the Geomancer's Apotheosis, a ring round him that
    shoves the near ones back - so the cadence card leaves something behind instead.
  - **Iron Ring** (L8, was +150% damage and twice as long): the stakes close into a ring (radius 1.4
    half-lengths) round the pack the nearest enemy stands in, its centre pushed off the wizard so it never
    pens him; inside is shoved back in, outside kept out, whatever leans on it cut. No stake is raised on
    water or a wall. +150%. (The first build's ring of 1.1 at +80% did two-thirds of the old Titan.)
  - **Rolling Wall** (L8, was -50% cooldown, three more walls, 1 s longer): once raised the wall marches
    forward at 75 px/s, taking what is just ahead of its face along with it and cutting it, and halts at
    water or a wall. Two more walls a cast, 1 s longer, -50% cooldown, +30%.

**Measured** (2026-10-29), each card against the template card it replaced, on the same footing, two
ways:

- *the 8-minute run*: `_BalanceHarness --minutes=8 --timescale=2 --all-unlocked --immortal
  --character=0 --seed=11 --spell=<id> --solo --upgrade-chance=1 --force-ascension --branch4=<L4>
  --branch8=<L8>` (Bramble Seed `--stand-still`), all damage a second (poison included) at level 4 and
  at level 8. The level 4 column pairs AI with TC and HA with IH, as the runs did.
- *the doubling* ("Every Ascension doubles its spell"): `--minutes=4 --xp-first=1 --xp-step=1
  --upgrade-chance=1 --timescale=3 --enemy-hp=N`, the Ascension over the same spell held at level 7,
  against a crowd it cannot clear.

Three other agents were running the same harness on the same four cores (load up to 60); every
report's clock stayed at 1.00, but read levels 4-6 as a band, as section 3 says.

| spell | card | 8-min, before -> after | doubling, before -> after |
|---|---|---|---|
| Hollow Star | Crush (L4) | 112 -> 107 | - |
| | Twin Stars (L4) | 48 -> 52 | - |
| | Black Sun (L8) | 1,000 -> 771 | x2.63 -> x2.30 (`--enemy-hp=30`) |
| | Nova Seed (L8) | 451 -> 434 | x1.99 -> x1.90 (`--enemy-hp=30`) |
| Bramble Seed (`--stand-still`) | Burrs (L4) | 159 -> 118 | level 7: 2,420 -> 2,236 |
| | Runners (L4) | 109 -> 86 | - |
| | Bramble Keep (L8) | 681 -> 735 | x1.80 -> x1.87 (`--enemy-hp=60`) |
| | Thornstorm (L8) | 393 -> 490 | x1.64 -> x1.72 |
| Contagion | Fever Burst (L4) | 37 -> 41-52 | level 7: 554 -> 630 (`--enemy-hp=12`) |
| | Outbreak (L4) | 32 -> 31-50 | - |
| | Pandemic (L8) | 525 -> 476 | 891 -> 875 a second (x1.61 -> x1.39) |
| | Plague Wind (L8) | 420 -> 438 | 920 -> 975 a second (x1.66 -> x1.55) |
| Iron Palisade | Spiked Rail (L4) | 54 -> 55 | level 7: 659 -> 675 (`--enemy-hp=30`) |
| | Caltrops (L4) | 47 -> 33-66 over five runs (median 36; levels 5-6 level with the old card) | - |
| | Iron Ring (L8) | 687 -> 563 | x2.77 -> x2.96 |
| | Rolling Wall (L8) | 370 -> 429-486 | x2.30 -> x2.46 |

Black Sun sits below the Titan it replaced on the 8-minute run on purpose: that Titan was twice the top
of the level 8 band (331-465, section 2f), and Black Sun's three collapses still more than double the
spell against a crowd it cannot clear.

Bramble Seed's level 4 cards sit under the ones they replaced on the 8-minute run, which a standing bot
reaches late, and within 8% of them at level 7 under a crowd it cannot clear (the "level 7" cell). Its
old Ascensions read x1.80 and x1.64 that day, not the x2.00 and x2.15 of 2026-10-26, and were clearing
the field (76 alive at the end, to the new cards' 102-123): one day's run against another moves this
much, which is why each before and after here was run the same day on the same machine.

Contagion cannot be held to the doubling: at `--enemy-hp=30` its poison kills nothing, earns no XP and
the spell never reaches its card (section 3 measured it at 12), and at 12 both the old Ascensions and
the new clear the field (50-55 alive at the end, to 131 at level 7), so every ratio there is a floor.
What it is held to is the card it replaced on the same footing: Pandemic and Plague Wind deal within
2% and 6% of the old Titan Cataclysm and Infinite Horizon there, and within 10% on the 8-minute run.
Its level 4 cards swing between runs more than any of the four (Outbreak read 31, 50 and 94 - the 94
at the first build, whose burst reached a whole jump).

**Cost, honestly.** Each card that changes behaviour is a hook in the spell's script, a row in
`EvolutionStatReaders`, a look (often an effect in `tools/art/spell_fx.py`), and a balance run
against today's card (`_AscensionAudit` and the harness, section "Every Ascension doubles its
spell"). Roughly one to two hours a spell for the seven with nothing of their own, a little less for
the other seven: two to three days in all, best built spell by spell so each can be played as it
lands. Gale Blade, Shadow Bolt and Spiritual Weapon (hand-written but almost all numbers) are the
next tier after these fourteen. (Spiritual Weapon's five went on 2026-10-28: each of its three forms
has five of its own, section 2f.) Evolution card icons (each card its own picture rather than the
spell's, tinted) belong with open issue #52.

### Built: Scorching Ray, Cinderbreath, Kindled Ward and Shadow Bolt (2026-10-29)

Eric approved 2e on 2026-10-29: *"yes lets make all mutations more unique per spell"*. Every id stayed
(the Pyromancer's path is still `cinderbreath_amplified_impact` then `cinderbreath_titan_cataclysm`), so
saves, `SeenAscensionIds` and the gates carry over; `Branch` still sets each default card's element gain,
its attunement price and its tint, and the block renames the card after it. Each card's hook reads its id
through `HasEvolution`, its numbers are constants in the spell's script that the card's words are built
from, and each re-specified card has its own icon (`tools/art/evolution_icons.py`). What a card leaves on
the ground is one node per arena with a fixed pool (`CinderGround`, `WardWisps`, `ShadowCurse`), ticked on
one half-second clock and drawn out of the reach it tests, at `DrawLayers.Ground` (a sigil at
`GroundTell`); what flies is at `Air`.

**Scorching Ray** (`ScorchingRayBeam`), from what it is - a thin red laser that heats up on a held target:

| card | was | now |
|---|---|---|
| level 4 (+Fire) | Amplified Impact | **Flashpoint**: every ray still bursts where it strikes, and the burst grows with the ray's heat; a target held to full heat flashes over in a white blast 60% wider, at the ray's full damage. +20% damage |
| level 4 | Hyper Acceleration | **Handoff**: a ray whose target falls swings its end on to the nearest enemy and burns it as hot as it was, once a cast, and that enemy is held at that heat into the next cast (`Player.HandOffScorch`). Rays strike 25% faster |
| level 4 | Reactive Catalyst | **Burn Through**: the beam runs on through its target to the edge of what the camera shows, burning every enemy on its line. +15% damage |
| Ascension (+Arcane) | Titan Cataclysm | **Prism**: every ray splits where it strikes into four violet-edged rays at the enemies round its target, each for 80% of the ray, heat and all, and holds them that hot into the next cast (`Player.HandOffScorch`). +60% damage |
| Ascension | Infinite Horizon | **Sunlance**: every ray widens into a lance that stays lit after it strikes and sweeps on to the next enemy and the next, burning everything it crosses - wider the hotter it burns. -30% cooldown |

Departures: the proposal gated Flashpoint, Prism and Sunlance on full heat. Heat is reached on a body
held six casts in a row, and a swarm body dies in one or two (section 3), so gated they would almost
never be seen; instead the heat is what grows each of them - the burst, the splits' damage, the lance's
width - and Flashpoint's flashover is the full-heat moment. Burn Through reads the screen's edge from the
viewport (a fallback reach without a camera).

**Cinderbreath** (`Flamethrower`, `FlamePuff`, `CinderGround`), a held jet of thrown tongues:

| card | was | now |
|---|---|---|
| level 4 (+Fire, the Pyromancer's) | Amplified Impact | **Backdraft**: as each burn goes out its fire rolls back into a burst at the tip of every jet, scorching everything round it for 3x a tongue. +35% damage |
| level 4 | Hyper Acceleration | **Ember Wake**: every second tongue drops an ember where it goes out, so the cone it swept burns for 2 s, scorching what walks in for 50% of a tongue every half second. Tongues thrown 25% faster |
| level 4 | Hellbreath (element path) | unchanged |
| Ascension (+Fire, the Pyromancer's) | Titan Cataclysm | **Dragon's Breath**: the jet reaches 75% farther with tongues twice as wide, and where each burn ends a wall of fire rises across its path and stands 3 s, burning what crosses it. +80% damage |
| Ascension | Infinite Horizon | **Fire Fan**: today's three jets, each turned on its own enemy in its own direction (Eric's of 2026-10-28, `Flamethrower.SpreadAims`) - renamed and reworded, numbers unchanged |

Departures: Dragon's Breath reaches 75% farther, not twice as far: at level 8 the jet's 240 doubled is
480, past a bolt's 450, which is the one thing `Flamethrower.BaseRange` says the jet may never do. Its
tongues are twice as wide, not the old three times, since the reach and the wall carry the card now.
Backdraft and Ember Wake gave up the old +30% width and -30% recharge for their bursts and embers.

**Kindled Ward** (`KindledWard`, `WardWisps`), a mote of light that drifts at the nearest enemy:

| card | was | now |
|---|---|---|
| level 4 (+Light) | Amplified Impact | **Flare**: the first time a ward meets the pack it flares - a burst of its light 1.6 times as wide as its burn, for 2x a strike, once a ward. +20% damage |
| level 4 | Hyper Acceleration | **Wisp Trail**: a drifting ward sheds wisps of its light every short step of its path; each hangs low for 2.5 s and sears what walks into it for 50% of a strike every half second. Wards drift 25% faster |
| level 4 | Rimelight (element path) | unchanged |
| Ascension (+Light) | Titan Cataclysm | **Pyre**: today's bigger burn, renamed - twice the burn, each ward a small sun. +80% damage |
| Ascension | Infinite Horizon | **Kindling Flock**: the wards no longer drift off. Each takes a place on a ring round the wizard and turns with it, breaks off to burn whatever comes near its place and flies back; 3 more may burn at once, each 3 s longer, -50% cooldown, +80% damage |

Departures: the trail is wisps of the ward's own light rather than embers - the ward is Light, and fire
on the ground is Cinderbreath's. The flock is every ward the spell may hold, not three, and it is not an
orbit of contact blades (Cyclone Slash's): the wards hang back on their ring and go out to meet what
comes, so the burn still does the work.

**Shadow Bolt** (`ChainLightning`, which Chain Lightning also runs - every hook is switched on by a Shadow
Bolt id, so Chain Lightning is untouched - and `ShadowCurse`). It had no row: hand-written, almost all
numbers. Its cards now come from what it is - shadow lightning that arcs far, hits softly and leaves a
trail - and stay the debuff half of the chain pair (section 3):

| card | was | now |
|---|---|---|
| level 4 (+Darkness) | Withering Mark: +12% damage taken | **Black Sigil**: everything the curse touches wears a sigil at its feet; the next hit it takes from any of your OTHER spells breaks it in a burst of shadow that lays the curse on everything near and strikes for 4x the bolt. +12% damage taken |
| level 4 (+Poison) | Creeping Rot: deeper poison, heavier drag | **Creeping Rot**: every arc sinks into the ground as a path of rot for 2.5 s that poisons, slows and exposes whatever crosses it. The deeper poison and drag ride along |
| level 4 | Spreading Dark | unchanged in what it does (the fork it already drew); reworded to say it, icon unchanged |
| Ascension | Anathema: +20% damage taken, two hops further, +45% | **Anathema**: where the curse ends, night falls - a pall of darkness hangs over the last enemy it reached for 3 s, cursing and striking everything under it every half second. The numbers ride along |
| Ascension | Plaguebearer: splits twice over, +8 poison | **Threefold Curse**: three curses leave the staff at once, each at its own enemy and each chaining on its own; +8 poison and +20% ride along (the +2 branches went) |

Black Sigil is broken only by the loadout: when Shadow Bolt's own next cast broke it, a lone Shadow Bolt
became a damage spell (x3.8 of Withering Mark at level 7), which is the one thing section 3 says it is
not. Solo, then, it is Withering Mark's exposure and nothing else.

**Measured** (`_BalanceHarness -- --timescale=2 --all-unlocked --immortal --character=0 --seed=11 --solo
--upgrade-chance=1`, before = the tree at 75a66a6, after = these cards). Two kinds of run, as in "Every
Ascension doubles its spell": the **ratio** runs are four minutes with `--xp-first=1 --xp-step=1
--enemy-hp=N`, the baseline stopping at level 7 and each Ascension forced (`--force-ascension`), all with
the level 4 card named first below; the **band** runs are eight minutes at normal health, damage a
second at levels 4 / 5 / 6 / 8 against the peer bands (40-60 / 70-130 at level 6 / 331-465). Four
sessions were measuring on four cores at once (load 25-38), so read every figure as a band; a level 4-6
column swings by half between runs of one build.

| spell | ratio runs (all damage a second, level 7 base -> level 8) | band runs, L4 / L5 / L6 / L8 |
|---|---|---|
| Scorching Ray, before | x30: 410 -> Titan 1,631 (x3.98), Infinite Horizon 1,560 (x3.80) | AI+TC 34 / 56 / 81 / 383; HA+IH 43 / 60 / 89 / 413, and again 57 / 78 / 98 / 488; RC+TC 44 / 76 / 153 / 429 |
| Scorching Ray, after | x30: 676 -> Prism 1,591 (x2.35), Sunlance 1,450 (x2.14) | Flashpoint+Prism 40 / 58 / 76 / 530; Handoff+Sunlance 63 / 71 / 105 / 426, and again 36 / 73 / 94 / 391; Burn Through+Prism 37 / 49 / 88 / 448 |
| Cinderbreath, before | x10: 327 -> Titan 668 (x2.04), Infinite Horizon 687 (x2.10). x30: 661 -> Titan 1,962 (x2.97) | AI+TC 32 / 49 / 78 / 411; HA+IH 46 / 59 / 89 / 401; Hellbreath+IH 29 / 48 / 74 / 396 |
| Cinderbreath, after | x10: 301 -> Dragon's Breath 566 (x1.88, the field cleared: a floor), Fire Fan 679 (x2.26). x30: 582 -> Dragon's Breath 1,336 (x2.30) | Backdraft+Dragon's Breath 40 / 65 / 63 / 439; Ember Wake+Fire Fan 45 / 63 / 98 / 412; Hellbreath+Fire Fan 22 / 44 / 76 / 392 |
| Kindled Ward, before | x90: 3,068 -> Titan 8,714 (x2.84), Infinite Horizon 7,908 (x2.58). (x30 is cleared by both Ascensions, before and after: x1.34 and x1.26 there are floors) | AI+TC 37 / 55 / 99 / 512; HA+IH 33 / 46 / 78 / 417; Rimelight+TC 43 / 55 / 73 / 420 |
| Kindled Ward, after | x90: 2,513 -> Pyre 7,049 (x2.80), Kindling Flock 7,546 (x3.00) | Flare+Pyre 43 / 61 / 83 / 501; Wisp Trail+Kindling Flock 30 / 50 / 73 / 416; Rimelight+Pyre 49 / 88 / 195 / 480 |
| Shadow Bolt, before | x10: 178 -> Anathema 615 (x3.46), Plaguebearer 721 (x4.05) | Withering Mark+Anathema 20 / 43 / 51 / 420 (55 s at level 8); Creeping Rot+Plaguebearer 23 / 59 / 80 / 455; Spreading Dark+Anathema 37 / 57 / 85 / 356 |
| Shadow Bolt, after | x10: 182 -> Anathema 647 (x3.55), Threefold Curse 604 (x3.32) | Black Sigil+Anathema 15 / 38 / 55 / 387; Creeping Rot+Threefold Curse 33 / 72 / 81 / 371 |

Scorching Ray's Ascensions deal what the old ones did (1,591 and 1,450 against 1,631 and 1,560); their
ratio fell because Flashpoint's level 7 rose. A 30-times-health crowd is where a heat ramp pays (a body
held six casts), so Flashpoint's level 7 there is 676 against Amplified Impact's 410, while at normal
health, where a body falls to one or two rays, it sits in the band (40 at level 4). Its first build (a
burst twice as wide at full heat, +35%) made 755 and 76 and was cut to 60% wider and +20%. Prism took
four passes: three splits for 60% at +25% measured x1.26 of the spell, and four at +100% only x1.69,
because the harder rays killed their held targets sooner and started cold - carrying the heat on with
the split (`Player.HandOffScorch`) is what made it double (x2.85 at +100%, x2.35 at +60%). Its 530 at
level 8 in the band run is over the band, but the +100% build measured 512 there, so that column is not
following the card's damage at this noise: worth a look in play. Handoff's first build swung twice a
cast and made 52 / 83 / 119 and 80 / 91 / 119 at levels 4-6, a quarter over the old card's two runs; one
swing a cast puts it where Hyper Acceleration was.

Flare's first build (a burst 2.2 times the burn for 2.5 strikes, +35%) made 81 / 84 / 143 at levels 4-6,
twice the band; at 1.6, 2 and +20% it sits where Amplified Impact did. Pyre and Rimelight are the old
cards' code, so their rows are run-to-run noise. Dragon's Breath deals less than the old three-times-wide jet in a packed crowd (1,336 against 1,962 at
x30) and still doubles its spell; at normal health it sits where the old Titan did (439 against 411), and
the wall is what it buys with the difference. A solo Shadow Bolt never breaks its own sigils, so its
baseline is Withering Mark's; with a partner spell (`_UiShot --demo-spells=magic_missile`) the sigils
break under the partner's hits.

### BUILT: the four bolts - Molten Shard, Riptide, Hunter's Draw, Gale Blade (2026-10-29)

Eric approved 2e on 2026-10-29 (*"yes lets make all mutations more unique per spell"*). The four spells
that fly on `ElementalBolt` are built, Gale Blade with them, since its five were hand-written but all
numbers. Every card's behaviour is the spell's own script, switched on by the card's id
(`scripts/BoltEvolutions.cs`, which also holds every number the behaviour is built from, so the card
text quotes the constants the code runs on). **No id moved**: Tituba's path (`riptide_amplified_impact`,
`riptide_titan_cataclysm`), `RiptideWave.TitanEvolutionId` and `SeenAscensionIds` carry over, and each
level 4 card gains the element it always did. Every re-specified card has a new icon
(`tools/art/evolution_icons.py`); Tidal Wall and Brackish Tide keep theirs.

| card (id) | was | now: what the player sees |
|---|---|---|
| Molten Shard 4 (`_amplified_impact`, +Metal) | Amplified Impact | **Slag Trail**: the flight lays a line of molten droplets for 2 s (only within the cast range); what crosses it burns for 30% of a hit every 0.4 s. +20% damage |
| Molten Shard 4 (`_hyper_acceleration`) | Hyper Acceleration | **Splitting Shard**: at its first hit the shard breaks in two; the halves fork off and home on the two nearest other enemies for 65% each, piercing one. -20% cooldown, +25% speed |
| Molten Shard 4 (`_reactive_catalyst`) | Reactive Catalyst | **Magma Core**: the shard stays in what it hit, swelling and throbbing, and erupts 0.8 s later for 130% of the hit on everything within 62 (withering each). +10% damage |
| Molten Shard 8 (`_titan_cataclysm`, +Fire) | Titan Cataclysm | **Caldera**: the shard dives in at its first hit and a lava pool wells up there (72 across the radius) for 4 s, 45% of a hit twice a second. +60% damage. With Splitting Shard each half makes its own pool; with Magma Core the pool wells up where the core erupts |
| Molten Shard 8 (`_infinite_horizon`) | Infinite Horizon | **Forge Swarm**: each cast forges 4 shards into a ring turning round the wizard, points out; when it holds 8 they all launch at once, each at a different enemy. -50% cooldown, +4 pierce, 40% larger |
| Riptide 4 (`_amplified_impact`, +Water, Tituba's) | Amplified Impact | **Breaker**: the wave throws everything it breaks over along its run (knockback 100, each body struck once a wave). +35% damage, 30% bigger |
| Riptide 4 (`_hyper_acceleration`) | Hyper Acceleration | **Groundswell**: the wave leaves the hand at 60% of its size and grows to 180% by the end of its run - hit and drawing alike. -25% cooldown, +25% speed |
| Riptide 8 (`_titan_cataclysm`, Tituba's) | Titan Cataclysm | **Tidal Wall**: unchanged, renamed |
| Riptide 8 (`_infinite_horizon`) | Infinite Horizon | **Trident**: three waves fan out from the hand at once, 24 degrees apart, the middle one on the aim. -50% cooldown, 40% bigger, +40% damage, +4 pierce |
| Hunter's Draw 4 (`_amplified_impact`, +Metal) | Amplified Impact | **Heavy Draw**: a full draw's arrows are drawn 35% heavier and throw what they hit along their flight (knockback 150) and stagger it 0.35 s. +30% damage |
| Hunter's Draw 4 (`_hyper_acceleration`) | Hyper Acceleration | **Running Shot**: while the bow is drawn, every 85 of ground run looses a snap shot at the nearest, at 60%; the draw is not spent. -15% time to nock, +25% speed |
| Hunter's Draw 4 (`_reactive_catalyst`) | Reactive Catalyst | **Barbed**: an arrow spent in an enemy stays in it, shaft and fletching out, and the wound bleeds 25% of the hit every 0.5 s for 3 s, a drop falling each time. +10% damage |
| Hunter's Draw 8 (`_titan_cataclysm`, +Wind) | Titan Cataclysm | **Rain of Arrows**: the volley climbs out of sight and comes down 0.4-0.95 s later as six arrows for every one loosed, scattered over the densest pack in reach (110 across the radius); each strikes what stands at its spot. +50% damage |
| Hunter's Draw 8 (`_infinite_horizon`) | Infinite Horizon | **Hunter's Mark**: a full draw marks the strongest enemy in reach (a miniboss first): four arrowheads turn round it, it is Vulnerable +25% for 5 s, and the whole volley homes on it. -50% time to nock, +3 arrows, +4 pierce, +60% damage |
| Gale Blade 4 (`_honed_edge`, +Wind) | Honed Edge (+50% damage) | **Eddy**: at the end of its throw the blade hangs 0.6 s, swelling to 1.7x and spinning, cutting everything in its reach every 0.15 s. +25% damage |
| Gale Blade 4 (`_keen_wind`) | Keen Wind (+25% crit) | **Hooked Throw**: the blade comes home on a hook - it turns 68 degrees off the way home, closing as it nears - so the way back is a second path. +10% crit |
| Gale Blade 4 (`_quickening`, +Lightning) | Quickening (-30% cooldown) | **Live Wire**: a crackling line of lightning from the hand to the blade while it is out, cutting what it crosses for 35% of a cut every 0.2 s. +20% speed |
| Gale Blade 8 (`_executioners_gale`) | Executioner's Gale (+100% crit damage) | **Thunderhead**: where the blade turns, a stroke of lightning out of the sky: 280% of a cut on everything within 92, staggered 0.4 s; every cut +15%, the blade 30% larger |
| Gale Blade 8 (`_tempest_rhythm`) | Tempest Rhythm (-40% cooldown) | **Squall**: at the end of its throw the blade bursts into five that come home on five bends (130 degrees end to end), each cutting for 75%. -20% cooldown |

**Where it departs from the tables above, and why.**

- **Riptide's Crosscurrent became Groundswell.** "The wave leaves a current that carries enemies" is
  Spring Tide's verb (flood: the lane behind the wave drags what stands in it); a sideways current would
  have been the Apotheosis on a level 4 card. Groundswell comes from the wave alone: it builds as it runs,
  so a long shot is a wide one.
- **Riptide's Spring Tide is called Trident**: Spring Tide is Tituba's Apotheosis.
- **Barbed bleeds without a bleed status.** The game has none, and poison's colour on a wound would read
  as poison. The arrow itself does the bleeding (`LodgedArrows`, one node per arena, a fixed array).
- **Heavy Draw's stagger** is `Enemy.ApplyShock` with the shove, as Solar Flare's Blinding Flash gives
  them: it reels, then goes.
- **Hunter's Mark's "takes more from everything"** is Vulnerable (+25%, 5 s); "strongest" is the most
  health left, an elite first.
- **Magma Core, Splitting Shard and Caldera end the shard at its first hit**, so they trade the level's
  pierce for what they do; the split halves pierce one of their own.
- **Gale Blade** had no row. Its five are built to the same rule out of what it is - a boomerang of wind
  and lightning that covers the ground between the wizard and the swarm: hang time, a second path, a
  line to the hand, a strike where it turns, a burst into many. Each level 4 card still gains its old
  element (Eddy Wind, Live Wire Lightning).

**Code.** `ElementalBolt` reads the evolutions once when a bolt is shot and runs each behaviour by id;
with none of these cards taken - and for Frost Shard, which takes none - every path is the one it was.
It now also reads the milestones' Knockback (Breaker, Heavy Draw), so `EvolutionStatReaders.Bolt` has
it. Ground left behind (`MoltenSlag`'s slag and lava) is on `DrawLayers.Ground`; what flies or strikes
(the forged ring, a lodged core, `ArrowRain`, `LodgedArrows`, `HuntersMark`, `ThunderStrike`, the live
wire) is on `Air`. Nothing allocates per frame: the slag, the rain, the barbs and the strike draw from
fixed arrays, scans walk `EnemyRegistry.Snapshot` on ticks, and Groundswell's growing wave refits its
spray in 15% steps (`RiptideWave.FitTrail`) rather than duplicating a particle material every frame.

**Measured** (2026-10-29, the same seed before and after, the shipped build against this one; the machine
was shared with three other sessions and ran at a load of 25-38 on four cores, though every report's clock
held at 1.00). Two footings, both solo on the Test Wizard with `--upgrade-chance=1 --seed=11`:

- **band**: `--minutes=8 --timescale=2 --immortal --all-unlocked --force-ascension --branch4= --branch8=`, at
  normal enemy health - damage a second at levels 4 / 5 / 6 and at the Ascension (L8).
- **ratio**: `--minutes=4 --timescale=3 --xp-first=1 --xp-step=1 --enemy-hp=10`, the level 7 baseline
  (level 4 card AI's id, no Ascension) and each Ascension forced (`Every Ascension doubles its spell`).

Hunter's Draw cannot be measured by a bot that holds its ground - it never moves, so it never draws, and
dealt 0 in all three first runs. Its band runs walk a 200-wide circle (`--circle=200`: it never stops, so
every volley is a full draw the arms gave out on) and its ratio runs use `--survivor --immortal` (steps
away, walks to orbs: stops and starts). Both are noisy - a survivor bot pinned by the crowd at minute 3
never draws again (one run read 2 a second for six minutes and was discarded).

| spell | card pair | before L4 / L5 / L6, L8 | after L4 / L5 / L6, L8 |
|---|---|---|---|
| Molten Shard | AI + TC: Slag Trail + Caldera | 40 / 44 / 50, 332 | 34 / 40 / 59, 279 (Caldera at +50%; now +60%) |
| | HA + IH: Splitting Shard + Forge Swarm | 37 / 47 / 59, 350 | 36 / 52 / 83, 376 |
| | RC + TC: Magma Core + Caldera | 36 / 49 / 78, 337 | 34 / 46 / 64, 267 (Caldera at +50%) |
| Riptide | AI + TC: Breaker + Tidal Wall | 62 / 66 / 105, 517 (field cleared) | 52 / 65 / 98, 497 (field cleared) |
| | HA + IH: Groundswell + Trident | 50 / 101 / 127, 449 (cleared) | 85 / 82 / 130, 440 (cleared; Trident before its +40%) |
| Hunter's Draw (circle) | AI + TC: Heavy Draw + Rain of Arrows | 58 / 78 / 99, 400 | 55 / 71 / 82, 403 |
| | HA + IH: Running Shot + Hunter's Mark | 52 / 73 / 100, 205 | 64 / 71 / 126, 351 |
| | RC + IH: Barbed + Hunter's Mark | (the bot was pinned) | 47 / 74 / 90, 357 |
| Gale Blade | AI + TC: Eddy + Thunderhead | 55 / 61 / 99, 237 | 34 / 48 / 89, 406 |
| | KW + TR: Hooked Throw + Squall | 30 / 55 / 88, 320 | 38 / 83 / 79, 371 |
| | QK + TR: Live Wire + Squall | 36 / 62 / 86, 315 | 45 / 68 / 90, 325 |

| spell | level 7 (with AI's card) | Ascension 1 (TC) | Ascension 2 (IH) |
|---|---|---|---|
| Molten Shard | 59 before, 84 after (Slag Trail) | Titan 335 (x5.7) -> Caldera 666 (x8.0, at +50%) | Infinite Horizon 647 (x11) -> Forge Swarm 593 (x7.1) |
| Riptide | 454 before, 513 after (Breaker) | Titan 942 (x2.1) -> Tidal Wall 709 (x1.4) | Infinite Horizon 795 (x1.75) -> Trident 778 (x1.5) |
| Hunter's Draw (survivor) | 175 before, 188 after (Heavy Draw at +20%) | Titan 923 (28 s at L8 only) -> Rain of Arrows 427 (x2.3) | Infinite Horizon 414 (x2.4) -> Hunter's Mark 442 (x2.35) |
| Gale Blade | 235 before (Honed Edge), 275 after (Eddy) | Executioner's Gale 253 (x1.1) -> Thunderhead 533 (x1.9) | Tempest Rhythm 451 (x1.9) -> Squall 588 (x2.2) |

Reading it:

- **Levels 4-6 sit in the band they sat in**, inside the factor-of-two a level swings by between runs
  of one build (section 3). The one sustained drop was Gale Blade's level 4, Eddy against Honed Edge's
  flat +50%; Eddy went from +15% to +25% and its level 7 now reads above Honed Edge's (275 against 235).
- **Riptide's Ascensions read lower, and it is not the cards.** Tidal Wall is unchanged; run with
  Breaker's shove switched off - which makes the build the shipped one exactly - it read 716 against the
  shipped build's 942 on the same seed, so that 942 was one run's luck (Riptide nearly clears a 10x field,
  72 left alive, and its number rides on how many walk into the next wave). With the shove on, 709. Trident
  throws three waves where Infinite Horizon threw four, and carries +40% for it.
- **Executioner's Gale was the short one**: x1.1 with no crit chance behind it. Thunderhead doubles on
  its own, and Squall a little more than Tempest Rhythm did.
- **Molten Shard** stays the large-ratio spell it was (x5-11, section "Every Ascension doubles"): its level 7
  is weak on one target. Caldera reads below the old Titan at normal enemy health (279 against 332; it burns
  the ground a pack stands on, and at normal health the pack dies off it) and well above at 10x (666 against
  335), so it went from +50% to +60%.
- **Hunter's Mark** at +0% read 234 in the band (the volley converges on one enemy, so a crowd loses the
  spread); +60% put it at 351-442. It is the single-target Ascension: everything else the player owns hits
  the marked enemy 25% harder too, which a solo measurement cannot see.

**What still needs eyes.** Whether each change reads in a crowd at the game's own zoom and on a phone:
the slag line under a dense pack, Groundswell's growth on a wave already the width of the screen, the
barbs and their drips on small enemies, the rain's field (it falls for 0.22 s and stands 0.45 s; the
first screenshot that caught it showed every falling arrow pointing up - the fletching was drawn as a
second head - so the rain and the barbs now draw a solid head and swept-back fletching; the falling rain
reads as falling, but only in a staged burst, never yet at the game's zoom), the live wire against the
Stormsweep and Chain Lightning bolts it shares a palette with, and the thunderstroke, which lasts 0.32 s.
Feel: whether Running Shot's snap shots and Heavy Draw's shove change how the bow is played, and whether
Breaker's shove helps Tituba's flood or fights it.

## 2f. Spiritual Weapon - the axe, the hammer and the sword (2026-10-28)

Eric: *"spiritual weapon should be able to select one of 3 weapons when you initially pick it, they
each have different advantages. axe is heavier but more damage, hammer is aoe that shakes ground and
slows enemies, and sword is the most direct damage to single targets, faster swings. they should each
have their own icons and special mutations/ascension that differ from each other at least slightly.
the animation for this should be that there is a single floating weapon that targets enemies. it
should animate by raising into the air, swinging down onto the targeted enemy, and then winding up
again. mutations/ascensions can up the amount as well, or change the weapon into a whole new, much
bigger and more detailed version of the weapon."* And of the old card: *"never mention px, add a %"*.

**What it was:** blades on a fixed orbit round the caster - Cyclone Slash's job, which is why it sat in
the "self / continuous / orbit" row of the grid. **What it is** (`scripts/SpiritualWeapon.cs`,
`Player.FireSpiritualWeapon`): one persistent node in the arena holding up to six weapons of light.
Each hangs at the wizard's shoulder until something comes within the spell's range of him, goes to hang
beside it, rises and winds back over its shoulder, comes over the top and down until its head is on
the quarry - the blow resolves on that frame - bounces up and settles head-up again. Every cooldown
hands each weapon one swing, staggered across the interval so several fall in turn;
`CastOutcome.Maintained`, so the cooldown still charges Arcane Explosion. Amount adds no weapon: a
count is a whole second attacker, as a hound is.

**Three forms, chosen once a run** on the page after the spell's card (`SpellVariant`; the mechanism,
built for this and for the Familiar's crow, cat and dog next, is
`.ai/decisions/2026-10-28-spell-variants.md`; the page is `.ai/level-up-screen.md` section 4h). Each
form is its own name, icon, numbers, level table and milestones; none changes the tags (Arcane +
Light).

| | **Spirit Axe** | **Spirit Hammer** | **Spirit Sword** |
|---|---|---|---|
| aim | the enemy nearest the wizard (Headsman: the most wounded) | the densest knot in reach | the toughest enemy in reach |
| blow | cleaves everything within reach of its quarry and throws it back | a slam: everything in the shockwave hit, slowed 30% for 1.2 s and jolted; the ground cracks and the camera shakes | its quarry and nothing else |
| level 1 | 20 a blow every 1.5 s, one axe | 12 a slam every 1.3 s, one hammer | 10 a cut every 0.7 s, one sword |
| level 7 | 45 every 1.2 s, **still one axe**, reach +15% | 25 every 1.06 s, two hammers, reach +30%, slow 40% for 1.6 s | 22 every 0.58 s, three swords, +10% crit |
| against bolts | - | **crushes** every enemy bolt inside a slam | **parries** any bolt that touches a blade, hanging, hunting or swinging |
| what it pays | the slowest blow; one weapon unless Whirling Axes; no guard | the least damage a blow | no area at all |

The level tables are written out in full in `SpellData_SpiritualWeapon.tres`; an Area line reads as a
share of the blow's reach ("Area +15%"), never a size (`Player.FormatAreaLine`). The axe's table buys
weight, one Area line and never a second axe, and its tags carry no ProjectileCount: a spell with that
tag and no count line is given a +1 at level 4 by `Player.EnsureRelevantLevelUps`, which is how a
second axe came back after it had been taken out of the table (measured below).

**Milestones** - each form's own, ids prefixed with the form (`spiritual_weapon_axe_broadaxe`), each with
its own icon. The level 4 third and the second Ascension are the hybrid element paths (+Light, +Arcane,
the 12% cooldown each pays); the first Ascension of every form **transforms the weapon** into a
colossal new drawing on the boss cell, twice the size, whose reach doubles with it.

| | level 4 | level 4 | level 4 (+Light) | Ascension (colossal) | Ascension (+Arcane) |
|---|---|---|---|---|---|
| **Axe** | **Broadaxe**: cleave 25% wider, +10% | **Headsman**: goes for the wounded; double on an enemy under half health | **Dawnsplitter**: Weakens what it cleaves (-30%, 3 s) | **Executioner's Greataxe**: double-bitted, x2 size and cleave, knockback doubled | **Whirling Axes**: two more axes, spinning end over end between blows |
| **Hammer** | **Aftershock**: 0.4 s later a second wave, 40% wider, 60% damage, slows again | **Earthshaker**: slam 35% wider, slows 15% harder | **Hallowed Ground**: the slam leaves holy ground 2.5 s that Weakens and slows | **Titan's Maul**: x2 size and slam, +20%, freezes 0.5 s (a boss takes a hard slow) | **Hammerfall**: two more hammers, falling in turn, +15% |
| **Sword** | **Twin Blades**: one more sword, each 10% softer | **Riposte**: guard 60% longer, a parry is a strike at once, +30% | **Soul Siphon**: 1 HP a strike, at most 3 a second | **Celestial Greatsword**: x2 size, cuts every enemy along the blade as it falls, +30% | **Thousand Cuts**: every swing a flurry of three cuts at 85%; a cut whose quarry has fallen goes to the nearest enemy |

**The drawing.** `tools/art/spirit_weapons.py` models the six weapons as signed-distance parts,
supersampled: the striking part (bit, face, blade) is conjured light on Arcane's emissive ramp,
white-hot along the edge; haft, hilt and guard are gold, lit from the upper left, with grips wound in
the wizard's violet; every one is ringed in occlusion so it holds over enemies and floor alike. Each is
written at 32 headings - pixel art turned by the engine smears, a sheet rasterised at the heading keeps
its pixels (Sleet Runes' reason): the axe, hammer and sword on the 48 cell (the axe in two rows, its
bit on either side, lit for each rather than flipped, since a bit leads the swing), the greataxe, maul
and greatsword on the 96 cell, drawn again rather than scaled - a double bit, a jewelled collar and
spikes; banded, studded faces and a great rune; a winged guard, a gilded ricasso and a fuller lettered
with runes. The node picks the cell nearest the heading. Behind a swing an analytic arc of the head's
path fades in 0.16 s; under the weapon its shadow on the ground says it is in the air; a slam's cracks
run out to the radius it hit, and every blow throws `ImpactDebris` out to exactly its reach (no marker,
section 2c). Card icons: `spirit-axe-arcane`, `spirit-hammer-arcane`, `spirit-sword-arcane`, and the
spell's own icon is the three crossed (`tools/art/spell_icons.py`); fifteen evolution icons in
`tools/art/evolution_icons.py`.

**Sound:** `spell_orbit` as a whoosh on every swing, throttled to one in 70 ms so a flurry does not
stack; `spell_summon` when the weapon first appears. The hits are the Arcane hit.

**Guards** (section 2d-bis): the old orbit's parry stays with the **sword**, the form that fights at a
blade's length - along the blade, hanging or swinging, and Riposte widens the guard and turns a parry
into a strike. The **hammer** crushes what is inside its slam: a guard that works only where and when
it lands. The axe has none; it throws enemies back instead.

**Measured** (`_BalanceHarness --minutes=8 --timescale=2 --immortal --all-unlocked --character=0
--spell=spiritual_weapon --variant=<form> --solo --upgrade-chance=1 --branch4=<L4> --branch8=<L8>
--force-ascension`, three or four Godots at once on four cores, every run's frames clean at 14-16 ms),
damage a second by level against the peer bands (40-60 / 70-130 / 331-465), and the health the immortal
bot lost standing in the same seed for eight minutes:

| form | level 4 + Ascension | L4 | L5 | L6 | L8 | health lost |
|---|---|---|---|---|---|---|
| axe | Broadaxe + Executioner's Greataxe | 82 | 126 | 165 | 469 | 6,241 |
| axe | Headsman + Whirling Axes | 59 | 91 | 114 | 444 | 7,657 |
| axe | Dawnsplitter + Whirling Axes (before the axe's table lost its last 9%) | 74 | 107 | 129 | 312 | 8,699 |
| hammer | Earthshaker + Titan's Maul | 46 | 72 | 91 | 423 | 454 |
| hammer | Aftershock + Hammerfall | 56 | 80 | 116 | 418 | 760 |
| hammer | Hallowed Ground + Titan's Maul | 41 | 60 | 79 | 405 | 616 |
| sword | Riposte + Celestial Greatsword | 50 | 68 | 89 | 446 | 777 |
| sword | Twin Blades + Thousand Cuts | 52 | 71 | 94 | 362 | 3,460 |
| sword | Soul Siphon + Celestial Greatsword | 41 | 49 | 61 | 389 | 3,046 |

How it got there: the first build had two cleaving axes by level 3 (Broadaxe 50% and +15%, the
Greataxe +60%) at 125 / 148 / 194 / 636, and the sword at 14 a cut every 0.62 s with the Greatsword
+80% at 65 / 104 / 154 / 602. Taking the axe's count line out produced a generated one (106 / 147 / 216
/ 780) until the tag went too. One axe with the Greataxe at +40% still made 613 - it struck six enemies
a swing whatever its cleave (46 or 38) - so the Greataxe's card is now its size and its throw, no
damage bonus. Thousand Cuts at 65% with no carry-on made 217: in a crowd of fodder the first cut killed
and the other two were lost, so a cut whose quarry falls now goes to the nearest enemy, at 85%. The
hammer's first build (Titan's Maul +50%, Hammerfall +30%) made 479 and moved least.

**Reading it.** All nine paths sit in the level 8 band or at its edges (312-469), the axe at the top -
"heavier but more damage". The Broadaxe path ran over the level 6 band in all three of its last runs
(134, 170, 165); if a playtest finds the axe carrying too hard mid-run, Broadaxe's +10% and 25% are the
first knob. The Light paths pay for their rider in damage, the element-path convention (Frostfall,
the Rimehound). What separates the forms is not that column:

- **On one enemy** (the tables' arithmetic at level 8, before the player's own bonuses) the sword does
  253 a second through the Greatsword and 410 through Thousand Cuts, against the axe's 51 (Greataxe) to
  124 (Whirling Axes; 186 on an enemy under half health with Headsman) and the hammer's 69 (Titan's
  Maul) to 117 (Hammerfall, before Aftershock's second wave). The sword is the boss-killer, and Thousand
  Cuts the most of it; the Greatsword is the sword for a crowd.
- **What it costs to stand there.** In the same seed the hammer's bot lost 454-760 health, the Riposte
  sword's 777, the other swords' 3,000-3,500 and the axe's 6,200-8,700: the slow, the freeze and the
  crushed bolts are the hammer's damage spent on not being hit, and Riposte's parries are the sword's.
  The axe is the most damage in a crowd and the least safety.

Levels 4-6 swing by a factor of two between runs of one build (section 3, Toxic Spore Burst); read
them as a band. The harness exits with a teardown crash after writing its report on this branch and
on the Familiar's alike; the reports are whole.

## 2g. The Familiar - the dog, the crow and the cat (2026-10-28)

Eric: *"when you FIRST take familiar, you should be able to select your familiar. each has it's own
advantages - crow moves faster, cat gives passive bonuses, dog does the most direct damage. they each
have their own mutations/ascensions"*. The second spell on the forms mechanism
(`.ai/decisions/2026-10-28-spell-variants.md`; Spiritual Weapon, section 2f, was the first): three
`SpellVariant`s in `SpellData_Familiar.tres`, each its own name, icon, words, numbers and whole level
table, chosen on the page after the spell's card and held for the run. `Familiar.FormOf` reads the form
in the node; every milestone id starts with its form's (`familiar_crow_griffin`). None changes the
tags: every form is pure Arcane. The dog is listed first, so it is the default for a starting spell and
for the bots, and it is the hound pack of the section above.

| | **Dog** | **Crow** | **Cat** |
|---|---|---|---|
| aim | the enemy nearest the wizard, inside 200 - it guards | an enemy that **shoots** first, else the one nearest the crow, inside 300; it goes from quarry to quarry without coming home | what comes right up to him, inside 120 |
| body | runs 330 (sprints 450) and leaps 42% of its range; walks round water and walls | **flies** at 640 over water, walls and the crowd; stoops from 120 off at 900, pecks in 0.08 s and rises off to circle 96 off | runs 300; a short, quick pounce with a cat's wiggle before it; heels close (34 behind) |
| strike | a bite: the quarry and what is in its jaws (18) | a peck: the quarry and what is at its beak (12) | a swipe: three claw strokes (16) |
| level 1 | 16 a bite every 1.0 s, one hound | 9 a peck every 0.55 s, one crow | 6 a swipe every 1.0 s, one cat; **cast speed +8%, evasion +4%** |
| level 8 | 44 every 0.68 s, two hounds (the second at level 3), 260 range | 24 every 0.39 s, two crows (the second at level 3), 375 range | 14 every 0.8 s, always one cat, 150 range; **cast speed +17%, evasion +10%** |
| against bolts | leaps and snaps one coming at him inside 260 that it can meet within 170; rests 1.6 s | takes one on the wing inside 320, met within 230; rests 1.5 s | bats one away inside 170, met within 110; rests 1.0 s - the most often, the closest in |
| what it pays | a ground body - it walks round water and walls - and it hunts only what nears the wizard | the least in a strike | almost no damage of its own: its worth is what it gives the wizard |

**The cat's grants pass through the pipelines that already exist**, read live and never stored into a
field, so they cannot double-apply and leave with the cat (the Brazier's pattern): two new level-up
effects, `SpellEffect.CastSpeed` and `Evasion` (the cat's table carries them on alternate levels), go
through `Familiar.GrantsOf` into `Player.EffectiveAttackSpeed` - the divisor every spell's cooldown
already has - and into the dodge roll's sum, under its 0.75 cap; Purr's regeneration joins the regen
sum. The pause screen's spell row lists them (`Familiar.GrantLines`), the only place that says what a
spell gives the wizard rather than the enemy.

**Milestones.** The level 4 element path and the hybrid rules are as every pure spell's (2b): one level
4 card deepens Arcane, one adds an element.

| | level 4 (+Arcane) | level 4 (element path, +20% and the rider) | level 4 | level 8 (+Arcane) | level 8 |
|---|---|---|---|---|---|
| **Dog** | **Great Hound**: +20%, jaws 40% wider | **Rimehound** (+Ice): bites slow 30% for 1.5 s; frost breath, jaws 40% wider | **Littermates**: +1 hound, each 15% softer | **Cerberus**: three heads, three bites a leap, +10% | **Hound of Annwn**: white and red-eared, +120% and jaws 50% wider; goes for an elite or a boss near the wizard first and bites one double |
| **Crow** | **Raven**: +30%, beak 80% wider | **Stormcrow** (+Lightning): every peck staggers (0.2 s) | **Murder of Crows**: +1 crow, each 10% softer | **The Morrigan**: a peck on an ordinary enemy under 30% of its health kills it; double on an elite or a boss there; +100% | **Griffin** (the dog's, before forms): a swoop through its quarry and on past it along a line, +20%, 30% less often |
| **Cat** | **Grimalkin**: every grant x1.4 | **Cait Sith** (+Darkness): what it claws takes 25% more from every spell for 3 s | **Purr**: 0.8 HP a second while it is out | **Cath Palug**: every grant x1.5 again, swipes 60% wider and 50% harder | **Nine Lives**: +1 life - when he would fall he stands back up with half his health |

The Darkness rider is Vulnerable, which is the cat's way of fighting: through his spells. After an
element path the level 8 looks are drawn in that element (Cerberus and the Hound of Annwn in frost,
the Morrigan and the Griffin storm-lit, Cath Palug black as the Cait Sith).

**What moved, and the save.** The Griffin was the dog's; it flies, so it is the crow's, and the dog has
the Hound of Annwn in its place. Every milestone id is new - the form's prefix - and the two that were
ever saved, the seen Ascensions `familiar:familiar_cerberus` and `familiar:familiar_griffin`
(`SaveData.SeenAscensionIds`), still read as seen through `Familiar.RenamedMilestones` in
`SaveData.IsAscensionSeen`. Level 4 milestones were never recorded anywhere a save keeps. No schema
step.

**Art** (`python tools/art/familiar.py`, all three forms through the hound's renderer with the matte
finish, `Fig.render(finish=True)`). The crow is a body plan of its own - wings, a wedge tail, a beak,
the wizard's hat - drawn on the 48 cell hovering over its shadow, with the Raven, the Morrigan and the
Griffin on the 64; in the arena a flier is drawn in the air over every body (`DrawLayers.Air`), where
the dog and the cat stand y-sorted among them. The cat is the hound's skeleton made feline - low and
long, a round head with big ears, a tail carried up - a striped fawn tabby in the hat; Grimalkin
grizzled and heavier in the wizard's mantle, the Cait Sith black with a white star on its breast, Cath
Palug a great spotted cat in gold with ember eyes; Grimalkin and Cath Palug on the 64. The Hound of
Annwn is the white hound with red ears of the Welsh otherworld. Card icons `familiar-dog-arcane`,
`familiar-crow-arcane`, `familiar-cat-arcane`, and the spell's own icon is the three together; fifteen
evolution icons (`tools/art/evolution_icons.py`). Eric, of the first icon: *"it looks a little creepy,
the eye is too prominent"* - every portrait now has the calm eye (`familiar.calm_eye`: a smaller, darker
amber with no glint and no black socket) and the hound's mouth is closed with a slight upturn; on the
sprites the crow and the cat take the calm eye too, and the hound's stays as it was, since at 48 pixels
it is one pixel and reads as attention, not a stare.

**Measured** - the roster's real-time method (`_BalanceHarness --minutes=8 --timescale=2 --immortal
--all-unlocked --character=0 --spell=familiar --variant=<form> --solo --upgrade-chance=1 --branch4=<L4>
--branch8=<L8> --force-ascension`, seed 11, three Godots at once on four cores, every run's frames 14
ms). The Spiritual Weapon measured the same way on the same afternoon, as the calibration: the axe
(Broadaxe + Executioner's Greataxe) 475 at level 8 against 2f's 469, the hammer (Earthshaker + Titan's
Maul) 426 against 423. Damage a second by level, against the peer bands (40-60 / 70-130 / 331-465):

| form | level 4 + level 8 | L4 | L5 | L6 | L8 | leaps (swoops) in 8 min | health lost |
|---|---|---|---|---|---|---|---|
| dog | Great Hound + Cerberus | 91 | 104 | 136 | **480** | 1,175 | 3,457 |
| dog | Great Hound + Hound of Annwn | 81 | 103 | 134 | **478** | 1,218 | 10,187 |
| dog | Rimehound + Cerberus | 76 | 93 | 119 | **426** | 1,180 | 5,038 |
| dog | Littermates + Hound of Annwn | 58 | 87 | 105 | **360** | 1,767 | 6,022 |
| crow | Murder of Crows + The Morrigan | 49 | 56 | 82 | **424** | 2,735 (288 executed) | 3,786 |
| crow | Stormcrow + Griffin | 63 | 79 | 107 | **421** | 731 (992) | 3,494 |
| crow | Raven + The Morrigan | 66 | 99 | 103 | **380** | 2,008 (149 executed) | 5,563 |
| crow | Murder of Crows + Griffin | 44 | 63 | 76 | **376** | 489 (1,862) | 3,028 |
| cat | Grimalkin + Cath Palug | 21 | 21 | 23 | 60 | 591 | 25,017 |
| cat | Purr + Cath Palug | 17 | 17 | 23 | 76 | 601 | 23,661 |
| cat | Cait Sith + Nine Lives | 20 | 24 | 32 | - (the lone cat never reached level 8) | 578 | 24,310 |
| *Spiritual Weapon, the calibration* | axe: Broadaxe + Executioner's Greataxe | 104 | 119 | 156 | 475 | - | 4,959 |
| | hammer: Earthshaker + Titan's Maul | 37 | 62 | 77 | 426 | - | 361 |

**The cat, measured as what it gives.** Its own strike is the weakest by design - 21-23 a second through
levels 4-6 and 60 at level 8 with Grimalkin and Cath Palug, an eighth of the dog's - and the form's
worth is the grants. At level 8 with both, the cat gives the wizard **+36% cast speed** for every spell
he holds (0.17 x 1.4 x 1.5) and **21% evasion**. Measured as the lift to a partner
(`--spell=chain_lightning --partner=familiar:familiar_cat --partner-level=N --partner-path=...
--enemy-hp=3`, a new harness switch that levels the partner at once; three times the health so the
partner never runs out of something to hit): beside a level 1 cat (+8% cast speed) Chain Lightning did
60 a second at its level 4 and 70 at level 5; beside a level 8 cat with Grimalkin and Cath Palug (+36%),
71 and 86 - **a fifth more**, against the 26% the arithmetic gives (1.36 / 1.08; the fixed-fps bench
measured 29%). A wizard holding five spells in the band gets more from the cat than from a sixth spell;
one holding two gets less, which is the trade the card offers.

**Reading it.** Every dog and crow path is in the level 8 band or at its top edge (360-480), and the
order Eric asked for holds. **The dog is the most damage**: 426-480 on three of its four paths, level
with the axe at the top of the roster; Littermates + Annwn, the path that spreads the pack, 360. **The
crow is the fastest**: it stoops about twice for every hound's leap (2,008-2,735 in eight minutes
against 1,175-1,218; the Murder's griffins swooped 1,862 times), flies at 640 against a hound's
330-450, and does 376-424 a second at level 8 - under the dog on every matched path, and mostly under
it at levels 4-6. The Morrigan's execute is work no peck's number shows: 149-288 kills a run. **The cat
is the weakest attack** by a factor of six to eight (60-76 a second at level 8) and pays it back in
grants, below. Health lost says what a path costs to stand in: the Hound of Annwn leaves the wizard's
side for the elites and cost the bot three times what Cerberus did; the cat's 24,000 is a bot standing
alone with almost nothing killing for it, which no player holding a cat is.

**How it got here.** The first numbers came from the fixed-fps bench (`--fixed-fps 30` at 6x, 200 ms of
game time a frame), which understates a creature whose bite cycle is shorter than a frame: there the
dog read 252-433, the crow 251-340 and the Spiritual Weapon 391-400. Tuned on it, the Morrigan went to
+200% and the Great Hound down to +20% and 40% (from +35% and 60%); in real time the dog's Great Hound
+ Cerberus then measured 599 and the Raven + Morrigan 585, a quarter over the band. **Cerberus went from
+35% to +10% and the Morrigan from +200% to +100%**, and the Great Hound kept its trim (with it the
dog's levels 4-6, 76-136 a second, already sit with the axe's 104-156, the top of the roster). The
Hound of Annwn first hunted the most health anywhere in its range and spent its bites running to it (a
third of Cerberus's damage on the bench); it now goes for an elite or a boss near the wizard first, as
the dog guards, at +120%. Read levels 4-6 as a band (2f): they swing by a factor of two between runs of
one build.

**Fixed (2026-10-29): walkers in the tarns.** The forest runs above kept every walker off water and
walls (0 frames in 34,000-96,000) but one: the Cait Sith, 309 of 34,528 and 3 blinks home. On the
Broken Circle, in the coursing runs (`.ai/campaign.md` section 6), the hound stood on a tarn or a wall
in 334-398 of 64,742-126,578 hound-frames and blinked home 5-6 times for it. **The cause was the
landing, not the walking.** The crouch asked `LeapClear` of the spot beside the quarry, but the leap
re-aimed at the quarry at take-off and on every frame of the flight without asking again, so a quarry
walking a shore carried the spot - a jaw's reach out - over the water; the bolt catch tracked its bolt
over a tarn the same way; and an arriving creature was set on its heel spot unasked. Every walking
step, nudge and blink already refused blocked ground, which is why the hound only ever got in through
the air. Now `Familiar.OpenSpot` takes the quarry's other side when the near spot is wet, `Landing`
brings a leap down on the furthest open point of its line (the shore, not the tarn), a landing cut
short bites only what is in its jaws (`InJaws`, so no quarry is bitten from a stride away), a walker
does not go for a bolt it cannot land under, and `AddHound` uses `FreeHeelSpot`.

Measured by walking the bot across the heath rather than holding it at the start (a bot standing still
rarely meets a tarn; `_BalanceHarness --stage=10 --spell=familiar --solo --immortal --minutes=8
--timescale=2` with `--walk=DEG` or `--explore`), before and after:

| Run | Frames on water or a wall | Blinks home |
|---|---|---|
| dog, `--walk=0` | 46 of 34,478 / 0 of 58,958 | 1 / 0 |
| dog, `--walk=90` | 363 of 56,825 / 0 of 34,352 | 7 / 0 |
| Cait Sith, `--walk=225` | 74 of 34,401 / 0 of 34,336 | 1 / 0 |
| dog, `--explore` | 3,913 of 53,261 / 0 of 64,315 | 77 / 0 |

Strikes and catches per hound-frame are unchanged within the runs' own spread (0.0129 / 0.0124 a frame
walking south, 0.0138 / 0.0168 exploring), so the forms' numbers above stand.

## 2h. Phase 2 - the rest of the roster's evolutions (2026-10-29)

Eric: *"lets make all mutations more unique per spell"*. Phase 1 (2e) rebuilt sixteen spells; phase 2 is
the rest, split between sessions by spell. The rule is 2e's: **every card says, in its name and its first
line, a change the player can SEE on that spell** - a new shape, a new behaviour, or something left
behind - drawn from what the spell already is. Numbers ride along; they are never the card. Each card is
audited first: **visible** (it already changes how the spell looks or behaves - it stays, its words may
be sharpened and its icon redrawn) or **numbers** (redesigned). What does not move: the ids (saves,
`SeenAscensionIds`, the Ascension browser and the Apotheosis gates are keyed by them), the element each
card gains, and every wizard's Apotheosis path.

### Magic Missile, Frost Shard, Chain Lightning and Void Lance

The four that fly on `ElementalBolt` and `ChainLightning` and were left out of 2e. `ElementalBolt` itself
did not change (Frost Shard has not flown on it since it became a shatter), and neither did anything
Shadow Bolt runs: every Chain Lightning hook is switched on by a Chain Lightning id.

**The audit.** Of twenty cards, five were visible, fifteen moved numbers - or, in two cases, used the
pull, which is Hollow Star's (2e):

| spell | card (id) | audit | becomes |
|---|---|---|---|
| Magic Missile | Twin Volley (`_twin_volley`) | visible: forks into two shards | stays |
| | Piercing Stardust (`_piercing_needle`) | numbers: +3 pierce | **Threaded Needle** |
| | Unstable Detonation (`_unstable_shard`, +Arcane) | a splash, but nothing drawn | stays; the burst is drawn |
| | Arcane Gatling Barrage (`_gatling_barrage`, +Lightning) | numbers: +2 darts, -44% cooldown, +5 pierce | **Overcharge** |
| | Singularity Core (`_singularity_core`) | big orbs that pull - Hollow Star's verb | **Arcane Orb** |
| Frost Shard | Glacial Spike (`_glacial_spike`, +Ice) | numbers: 30% harder slow (and another spell's name) | **Ice Statues** |
| | Ice Flurry (`_ice_flurry`) | visible: shards at enemies past the burst - but struck at once, a puff where they stood | stays; the shards fly |
| | Frost Nova Burst (`_frost_nova`) | visible: the shatter freezes | stays |
| | Absolute Zero (`_absolute_zero`) | numbers: +100% damage, +30% area | **Avalanche** |
| | Blizzard Engine (`_blizzard_engine`, +Water) | numbers: 3 more shards, +150% | **Meltwater** |
| Chain Lightning | Storm Surge (`_forked_surge`, +Water) | the element path | stays |
| | Storm Reach (`_storm_reach`, +Lightning, the Stormcaller's) | numbers: +3 hops, a wider jump | **Storm Reach**: the hops leap long |
| | Dense Current (`_dense_current`) | numbers: +40%, crits | **Return Stroke** |
| | Cascade (`_cascade`, +Lightning, the Stormcaller's) | numbers: splits three ways again | **Cascade**: it comes apart on its kills |
| | Thunderhead (`_thunderhead`, +Lightning) | numbers: x2 and a hop (and Gale Blade's name since 2e) | **Electrocution** |
| Void Lance | Abyssal Pierce (`_abyssal_pierce`) | numbers: open 0.5 s longer | **Abyssal Scar** |
| | Rift Blade (`_rift_blade`) | numbers: +50% length and width | **Void Glaive** |
| | Gravity Spike (`_gravity_spike`, +Darkness) | drags onto the lance - Hollow Star's verb | **Dread** |
| | Annihilation Beam (`_annihilation_beam`, +Arcane) | numbers: x2 size, +200% | **Witching Hour** |
| | Cosmic Collapse (`_cosmic_collapse`) | visible: a burst at the tip | stays; the black hole is drawn |

Verbs held clear of: the pull (Hollow Star), the Apotheoses' burn, stop, link, hold, flood, swarm, pull and
reflect, and the 2e cards - no slag or rot path, no sky strike (Thunderhead, Rain of Arrows), no ring of
forged shards (Forge Swarm), no lance that sweeps from target to target (Sunlance).

#### Magic Missile - a homing violet dart that never misses

`MagicMissile.cs` (ids and numbers as constants there), `StaticCharges.cs`. The starter's spell, the
Apprentice's and the Test Wizard's; neither has an Apotheosis on it.

| card | was | now: what the player sees |
|---|---|---|
| Twin Volley (L4) | unchanged | the dart forks on its first hit, two shards flung straight out either side |
| **Threaded Needle** (L4) | Piercing Stardust: +3 pierce | after each hit the dart turns on the nearest enemy it has not struck (within 220) and flies on, 3 more times, stitching a fading thread of stardust through them. No numbers |
| Unstable Detonation (L4, +Arcane) | the same splash, drawn nothing | the burst throws arcane sparks out to the 60 it strikes |
| **Overcharge** (L8, +Lightning) | Arcane Gatling Barrage | every dart leaves a spark of static crackling round what it hits; an enemy carrying 3 overloads - lightning bursts out of it to 64 (x Area), striking it and everything round it for 3x a dart. A spark no dart renews goes out after 2.5 s. -40% cooldown |
| **Arcane Orb** (L8) | Singularity Core: x2.2 area, +100%, a pull | every dart is loosed as a great slow orb (radius 26 x Area, a third of a dart's speed, no homing) that rolls straight through the pack, striking everything under it five times a second for 1.8 s, and bursts where its flight ends (72 x Area) for 2x. +50% damage, +50% cooldown. With Unstable Detonation the end burst reaches 30% further; with Twin Volley the orb forks at its first contact |

Departures and why: **Threaded Needle is the genre's ricochet**, a payload the roster lacked
(`.ai/spell-variety.md` section 2) - it is not a chain: the dart flies to each enemy, so it takes time and
can be outrun, and it strikes only what it reaches. **Overcharge** keeps the old card's cadence (-40%) and
spends the rest of it on a stacking burst that the spell's nearest-first aim feeds on its own against an
elite or a boss. **Arcane Orb** started at 2.2 times a dart's radius, homing, every 0.45 s: the first
screenshot buried the wizard under six opaque orbs that had settled on their targets beside him. It now
rolls straight on, translucent but for its core, from in front of him, half as often. The dart's sprite
particles used to be made per dart and are untouched; the thread, the sparks and the orb draw from fixed
arrays (no allocation per hit), the burst on `ImpactDebrisLayer`.

**Measured** (seed 11, solo Test Wizard, `--upgrade-chance=1`; the before runs on the tree at 2959dba,
the after runs on this one; three of these sessions' runs and other agents' shared four cores at load
8-57, so read every figure as a band):

| path | before L4 / L5 / L6, L8 | after L4 / L5 / L6, L8 |
|---|---|---|
| Piercing Stardust + Gatling -> Threaded Needle + Overcharge | 50 / 56 / 93, 427 | 43 / 62 / 127, 441 |
| Twin Volley + Singularity Core -> Twin Volley + Arcane Orb | 35 / 73 / 82, 450 | 66 / 155 / 220, 494 (Twin Volley is unchanged; this run's levels came late - the first Orb tune read 45 / 102 / 105) |

Doubling (`--minutes=4 --xp-first=1 --xp-step=1 --enemy-hp=10`, level 7 against the Ascension forced):
before, Piercing Stardust's level 7 302 -> Gatling 592 (x1.96) and Singularity Core 653 (x2.16), both still
clearing the field (55 and 72 alive at the end: floors); after, Threaded Needle's level 7 238-254 ->
Overcharge 647 (x2.6-2.7, 64 alive: a floor) and Arcane Orb 586 (x2.3). The first Arcane Orb (radius 22,
0.55 of a dart's speed, 1.6 s, no damage) read 192 (x0.8) and 265 at level 8: an orb crossing a body in a
fifth of a second struck it once. It was slowed to a third, given 1.8 s, five strikes a second, a 2x burst
and +50%.

**Still needs eyes:** the thread at the game's zoom (it is two pixels wide), the overload bursts in a
dense pack (seven bolts each - how many at once before it is noise), and the orb over a crowd on a
phone.

#### Chain Lightning - a heavy bolt that hops close and hard

`ChainLightning.cs` (ids and numbers as constants beside Shadow Bolt's), `Electrocutions.cs`. The
Stormcaller's spell: his Apotheosis, the Lattice, needs Storm Reach and then Cascade, and links whatever
the chain strikes - so both cards keep their ids, their Lightning and their numbers, and only what the
chain does with them changed. Storm Surge, the Water element path, is as it was.

| card | was | now: what the player sees |
|---|---|---|
| **Storm Reach** (L4, +Lightning, the Stormcaller's) | +3 hops, a 35% wider jump, -15% | the chain leaps long: every hop jumps to the **farthest** enemy it can reach instead of the nearest, so the arc sprawls across the pack from side to side. The numbers ride along unchanged |
| **Return Stroke** (L4) | Dense Current: +40%, +15% crit | where the chain ends, the stroke runs back along it to the wizard's hand a hop every 0.05 s, white-hot, striking every enemy on the way again for 80% (a body that fell is passed through). +30% |
| **Cascade** (L8, +Lightning, the Stormcaller's) | splits three ways at every hop, +2 hops, +20%, 30% wider | the chain comes apart on what it kills: every enemy a hop kills flashes and throws 2 more arcs out of its body (up to 12 a cast, 36 hops in all), and the chain goes a hop further from it. +1 hop, +50%, 30% wider |
| **Electrocution** (L8, +Lightning) | Thunderhead: x2 and a hop (Gale Blade's name since 2e) | the first enemy the bolt strikes is held in the current for 1.2 s, convulsing as arcs crawl over it (`Enemy.ApplyShock` renewed), struck again every 0.2 s for 40% of the bolt; if it dies held, the current leaps to the nearest enemy within 110 and holds that. +50%, +1 hop |

Why these: the hop is the spell, so each card changes the hop - its length, its direction home, what a
kill does to it, how long it lasts. **Cascade** first fed kills more hops (two a kill, +20%): one chain
still, and it measured 166 at level 8 against the old net's 400 - at normal health the old card's three-way
split at every hop simply hit everything in reach. Forking where it kills brings the split back only where
the chain is winning, which is what a cascade is, and it reads: a chain that bursts into a branching storm
when it starts killing. **Electrocution** first let go of a body that died held; in a crowd that dies
under the first strike it gave nothing, so the current now leaps on. Electrocution is not a chain: the
Lattice's link is between bodies the web holds; this is one body at a time, and it is the anti-elite card
the old one was - the nearest-first aim holds whatever walks at the wizard, an elite every cast.

The chain's candidate search no longer sorts the whole registry with LINQ every hop: it reads the enemy
grid's near set (`EnemyRegistry.CollectNear`) and picks the nearest - or Storm Reach's farthest - by hand.
Shadow Bolt picks exactly what it picked before. Every new drawing is in the air (the held crawl, the
leap, the kill flash on `ImpactDebrisLayer`); nothing is allocated per frame.

**Measured** (seed 11 unless marked, solo Test Wizard, the same footings as Magic Missile above):

| path | before L4 / L5 / L6, L8 | after L4 / L5 / L6, L8 |
|---|---|---|
| Storm Reach + Cascade | 49 / 61 / 76, 400 (27 alive: the net cleared the field) | 42 / 56 / 75, 437 (45 alive) |
| Dense Current + Thunderhead -> Return Stroke + Electrocution | 43 / 73 / 96, 276 | 42 / 57 / 93, 326 (the first build, the stroke back at 60% with no damage of its own and an Electrocution that let go of the dead, read 29 / 45 / 70, 166) |

Doubling at x10 enemy health, Storm Reach's level 7 the baseline: before 92 -> Cascade 793 (x8.6, 41
alive: it cleared the field, a floor) and Thunderhead 203 (x2.2); after 104 -> Cascade 358 (x3.4) and
Electrocution 253 (x2.4). Kill-fed cards are weakest exactly where this footing puts them - a crowd the
spell cannot clear - and both still double; the first Cascade read x1.57 there and the first Electrocution
x2.07.

**The Stormcaller's Apotheosis** (`--character=3 --seek-apotheosis --minutes=20`, the path Storm Reach then
Cascade, the Lattice offered at player level 23): it still arrives and still links - the report shows the
Lattice taken after Cascade, and Chain Lightning carrying the run.

| seed | before: the Lattice at, Chain Lightning's share | after |
|---|---|---|
| 11 | 13.0 min, 405,645 (84%) | 10.8 min, 444,706 (81%) |
| 12 | 12.3 min, 441,173 (88%) | 11.8 min, 425,370 (83%) |

The form is gated on player level 23 (`ApotheosisCatalog.MinimumPlayerLevel`), and both runs reached it a
little sooner - half a minute and two - because the new path kills more of what reaches the wizard, so the
run levels faster. Both are inside what one seed against another moves (the first, kill-fed Cascade read
9.4 on seed 11), but if the Stormcaller's form starts arriving early in play, this is where to look.

**Still needs eyes:** Storm Reach's long arcs at the game's zoom (they leave the screen at a close zoom),
whether Cascade's storm is readable or a scribble in a big pack, the return stroke (it is 0.05 s a hop), and
the held crawl on small enemies.

#### Void Lance - a short-lived lance of shadow, where position is the aim

`VoidLance.cs` (ids and numbers as constants there), `VoidScars.cs`, `Player.FireVoidLanceSpell`. No wizard
starts with it.

| card | was | now: what the player sees |
|---|---|---|
| **Abyssal Scar** (L4) | Abyssal Pierce: the lance open 0.5 s longer | as the lance withdraws it tears a scar of void into the ground along its line - a jagged black crack lit violet at the lips, on `DrawLayers.Ground` - that stays 2 s and cuts whatever stands on it for 40% of the lance every 0.4 s. It stops short of water and walls |
| **Void Glaive** (L4) | Rift Blade: +50% length and width | the lance runs out of both hands: with every cast a second blade, 75% as long, thrusts out behind the wizard along the same line |
| **Dread** (L4, +Darkness) | Gravity Spike: dragged what was near onto the lance - the pull | everything the lance runs through is struck with dread and turns and runs from the wizard for 1.6 s (`Enemy.ApplyFear`; elites and bosses stand firm, nothing twice in 4 s). +20% |
| **Witching Hour** (L8, +Arcane) | Annihilation Beam: twice the length and width, +200% | the lance no longer withdraws: one hand of void stays in the wizard's hands and turns round him once every 2.6 s, cutting everything it sweeps across - struck again each time the hand comes round. It goes out a few cooldowns after nothing is left in reach |
| Cosmic Collapse (L8) | unchanged in what it strikes | the hole is drawn: a disc of void at the tip that swells to the reach it struck and caves in on itself, its rim brightest as it closes (it was a spray of debris) |

Why these: Void Lance's row in the grid (section 2) was always "fixed, slowly rotating - position, not aim",
and the spell never got there; Witching Hour is that, as the Ascension. It is not Cyclone Slash's orbit (a
ring that hits on contact round him) nor Sunlance's sweep (a ray swinging from target to target): it is one
long spoke, and the player's only decision is where to stand. **Dread** takes the other Darkness verb -
fear, which every Darkness hit already has a chance of (`Player.TryFrighten`) - and makes it certain along
the lance: the defensive choice of the three, a lane of the pack scattering away. The first Witching Hour
struck each body once in the hand's life (the thrust's enter-hit bookkeeping never let go of a body), and
read 207 at level 8; a hand that never withdraws now lets go of what leaves it, and forgets the dead once
a cast. The lance's motes were a Sprite2D and a Tween each, forty a second; a hand that never withdraws
would have kept that up for the run, so they are drawn from a fixed ring by the lance itself.

**Measured** (seed 11, the same footings):

| path | before L4 / L5 / L6, L8 | after L4 / L5 / L6, L8 |
|---|---|---|
| Abyssal Pierce + Annihilation Beam -> Abyssal Scar + Witching Hour | 31 / 48 / 81, 463 | 36 / 64 / 99, 354 (a turn in 3.6 s: 26 / 40 / 64, 313; the first, which struck each body once: 35 / 47 / 82, 207) |
| Rift Blade + Cosmic Collapse -> Void Glaive + Cosmic Collapse | 33 / 47 / 79, 411 | 34 / 44 / 99, 420 |
| Gravity Spike + Cosmic Collapse -> Dread + Cosmic Collapse | 37 / 52 / 81, 410 | 31 / 54 / 71, 402 |
| Void Glaive + Witching Hour (new: the hand both ways) | - | 60 / 87 / 124, 383 |

Doubling at x10, the level 4 card AI's id at level 7: before 239 -> Annihilation Beam 632 (x2.6) and Cosmic
Collapse 659 (x2.75); after 262-266 -> Cosmic Collapse 608 (x2.3) and Witching Hour 579 (x2.2). Turning once in
3.6 s it read 503 (x1.9), and the first build 216 (x0.8). Witching Hour sits under the old Annihilation
Beam at normal health (354 against 463, which was the top of the band): a hand that turns past everything
in reach is limited by how often it comes round, not by what each cut deals.

**Still needs eyes:** the scars on a busy floor (several at once at a short cooldown), the hand turning
over the wizard's own sprite, and whether Dread's scatter helps a player or pushes the pack out of his
other spells.

### Obsidian Spike, Cyclone Slash, Black Tentacles and Cone of Cold

**The audit.** Each card was read against the code that reads it and put in one of three classes:
*visible* (it already changes how the spell looks or behaves: it stays, its words may be sharpened),
*numbers* (it only moves numbers: redesigned), and *element path* (a pure spell's branch into a second
element, 2b: it stays). **No id moved**, so the three wizards whose Apotheosis paths name these cards -
the Frostweaver (Deep Freeze, Zero Point Shockwave), the Geomancer (Splinter Field, Tectonic Uprising)
and Laelaps (Tailwind Surge, Typhoon Core) - keep their gates, and `SeenAscensionIds` and the browser
carry over. Every hook reads its card by id in the spell's script (`HasEvolution`); every number on a
card rides a field `EvolutionStatReaders` lists for it; every changed card has a new icon.

**Verbs avoided:** the pull (Hollow Star's), every Apotheosis verb (`ApotheosisCatalog`: burn, stop, link,
hold, flood, swarm, pull, reflect), and every phase 1 card's. **Two cards that stay carry a pull**, and
are flagged rather than changed, because both are visible and the rule keeps a visible card: Cyclone
Slash's Vortex Reaper (the whole card is a drag to the ring) and Black Tentacles' Elder God Maw (the
maw is the card; its drag toward the teeth rides along). Each is a decision for Eric - keep, or give it
another verb.

**Measured** the way 2e's batches were: the *band* run (`_BalanceHarness --minutes=8 --timescale=2
--immortal --all-unlocked --character=0 --seed=11 --solo --upgrade-chance=1 --force-ascension
--branch4= --branch8=`, Cyclone Slash with `--circle=160`), damage a second at levels 4 / 5 / 6 and at
the Ascension against the peer bands (40-60 / 70-130 at level 6 / 331-465); and the *ratio* run
("Every Ascension doubles its spell": `--minutes=4 --timescale=3 --xp-first=1 --xp-step=1
--enemy-hp=N`, the level 7 baseline against the Ascension forced). Before is the tree at 2959dba in a
worktree of its own, after is this one, same seed, same day. Other agents were measuring on the same
four cores (load 20-50), so read levels 4-6 as a band. The Apotheosis check is `--seek-apotheosis
--character=N --minutes=20 --timescale=2 --immortal --all-unlocked --seed=11`.

#### Cone of Cold (`ConeBlast`)

What it is: a spray of ice thrown in a 60-degree cone at the nearest enemy, every 1.8 s, slowing what
it hits; its reach is the spray's (2c).

| card (id) | class | what the player sees now |
|---|---|---|
| Deep Freeze (`cone_deep_freeze`, +Ice, the Frostweaver's) | visible | stays: everything the cone hits is frozen solid, white with frost, for 1.2 s |
| Arctic Blast (`cone_wide_angle`) | visible | stays: a cone's angle is its shape, and a 180-degree half-moon is a different weapon from a 60-degree cone. Words sharpened ("opens into a half-moon") |
| Frost Shrapnel (`cone_ice_shards`, +Water) | element path | stays |
| **Zero Point Shockwave** (`cone_zero_point_wave`, +Ice, the Frostweaver's) | numbers (+150% damage, +40% reach) | **the blast goes on past its end**: the cold that reaches the tip gathers there into a star of ice (0.16 s, `ConeBlast.RelaySeconds`) and bursts on into a second cone thrown from that point the same way, as wide and half as long (`RelayLength`), into what stood behind the front. The +40% reach stays (the Long Winter's ring is measured on it); +120% damage, from +150% |
| Glacier Tide (`cone_glacier_tide`) | visible | stays: the blast throws everything it hits away from you. Words lead with it now |

The relay never strikes what the first cone struck: any point of a cone at most 90 degrees wide thrown
from the tip lies further from the caster than the tip does. It keeps the cone's own angle under the
Long Winter, so the form's full ring carries an aimed relay out past it - the Apotheosis arrives on top
of it and does more. Not a ring (the Long Winter's) and not a shatter (Frost Shard's, the form's).

| | before | after |
|---|---|---|
| band, Deep Freeze + Zero Point, L4 / L5 / L6 / L8 | 37 / 35 / 60 / 302 | 42 / 67 / 89 / 302 (Deep Freeze is unchanged: its levels are run-to-run spread) |
| ratio, `--enemy-hp=10`: level 7 (Deep Freeze) 93 -> Zero Point | 387 (x4.2) | 356 (x3.8) |
| the Frostweaver, `--seek-apotheosis`: The Long Winter offered at | 11.8 min | 10.7 min |
| ... Cone of Cold's damage over the 20 minutes (its share) | 352,320 (69%) | 366,819 (66%) |

**Needs eyes:** the cone's spray is a scatter of small ice squares and the relay is drawn the same way
(a little denser, `ImpactDebris` from the tip), so whether a second fan reads at the game's zoom in a
crowd - the star is the tell - is for a playtest. At level 8 the relay reaches about 550 from the
wizard, past a phone's half-width when he aims sideways.

#### Black Tentacles (`BlackTentacles`)

What it is: a rift torn open on the densest pack; everything in it is slowed, and tentacles burst up
and slap what is inside, a slap hitting its target and what stands round it. Poison + Earth.

| card (id) | class | what the player sees now |
|---|---|---|
| Crushing Grip (`tentacles_constriction`, +Earth) | visible, but not drawn | stays, and now SHOWS: the root it always gave read only as the freeze tint every root wears; the slapping tentacle now leaves two turns of its coil wound round the target's legs, suckers out, for as long as it holds (2 s), loosening at the end (`DrawCoils`). Numbers unchanged |
| **Fling** (`tentacles_abyssal_spread`) | numbers (Abyssal Spread: +60% rift, +2 tentacles) | **the arms throw what they catch**: a slap that lands picks its target up and hurls it across the pit at the enemy in the rift furthest from it (within 1.25 radii); it flies in an arc over everything (`Enemy.ApplyLaunch` and a shove), a violet trail behind it and its shadow on the ground under it, and where it comes down it and everything round it take 45% of a slap (`FlingShare`). Elites and bosses are too heavy to throw. +1 tentacle, the rift 20% wider |
| Soul Drain (`tentacles_soul_drain`) | behaviour, unseen | stays, and now SHOWS: every fourth slap tears a wisp of rot-light out of what it struck, which flies to the wizard; the hit point lands when it arrives (the cap, 3 a cast, is unchanged) |
| Elder God Maw (`tentacles_elder_god_maw`) | visible | stays (the maw breaking up through the earth, 2026-10-28). Its drag toward the teeth is a pull - flagged above. A flung body is never dragged mid-air |
| Shadow Forest (`tentacles_shadow_forest`, +Poison) | visible | stays: it was always drawn as seven small pits over the field; the words now say so first |

**The throw is new to the game** (`Enemy.ApplyLaunch`, shared with Obsidian Spike's Deep Strata below):
the body's sprite rides an arc over the body, which stays on the ground for collision and contact; in
the air it chooses nothing (no step, no wind-up, no shot), it is drawn over everything in the air
(`DrawLayers.Air + 1`) so the crowd and the arm that threw it cannot hide it, and a knockback given
with it still carries it. A body already up cannot be thrown again until it lands; a miniboss never
is. Killed in the air it drops.

| | before | after |
|---|---|---|
| band, Abyssal Spread / Fling + Shadow Forest, L4 / L5 / L6 / L8 | 53 / 68 / 98 / 366 | 29 / 41 / 61 / 374 |
| band, Abyssal Spread / Fling + Elder God Maw (Fling at 100%) | 35 / 42 / 62 / 406 | 33 / 46 / 61 / 380 |
| level 7, `--enemy-hp=10` (the level 4 card's own worth against a crowd that lives) | Abyssal Spread 149; Crushing Grip 188 and Soul Drain 159 (both unchanged) | Fling 236 (at 100%: 316; at 70%: 264) |

Fling is a card for a crowd that lives: at normal health the first slap usually kills and nothing is
thrown, so its band runs sit at or under the card it replaced (whose own two runs read 53 and 35 at level
4), while against ten times the health every slap lands twice, and at a full slap it doubled the old
card. It went 100% -> 70% -> 45%, where it sits a quarter over Crushing Grip, its sibling, at level 7
and under the level 4 band floor at normal health - the first knob either way.

**Needs eyes:** in a still frame of a packed knot a thrown body is hard to pick out even drawn over
everything, so the arc's trail is what carries it - whether it reads in motion, at the game's zoom, is
the first thing to check. The coil sits on the legs over the freeze tint every root shows, which is ice
blue on a tentacle's grip (the status look is shared, not the spell's). The wisps are drawn as big as
a head; whether three a cast are noticed at all is for a playtest.

#### Obsidian Spike (`GroundSpike`)

What it is: black volcanic glass erupting, with no travel, under each of the nearest enemies after a
0.22 s tell - cracks running out to the radius it hits, chips thrown, the glass glinting, fracturing and
falling back into the ground (2026-10-28). The Geomancer's spell: the ground opens under the front rank.

| card (id) | class | what the player sees now |
|---|---|---|
| **Splinter Field** (`obsidian_spike_splinter_field`, +Earth, the Geomancer's) | numbers ("bursts 35% wider") | **the eruption splinters through the ground**: as its cracks run out, a splinter of glass punches up under each of the 4 nearest enemies just past its rim (out to 2 radii), one after another, striking what stands over it for 50% (`LaySplinters`, the sheet's splinter row). The eruption 15% wider rides along |
| Umbral Vein (`obsidian_spike_umbral_vein`, +Darkness) | element path | stays |
| **Deep Strata** (`obsidian_spike_deep_strata`) | numbers (+45% damage, a longer cast range) | **the spike throws what it breaks out under into the air**: everything it hits is thrown up (`Enemy.ApplyLaunch`, 40 high) and hangs there 0.6 s doing nothing, its shadow on the ground under it, then drops where it was; an elite or boss reels instead (`ApplyShock`, 0.3 s). +40% damage and the longer reach ride along |
| **Tectonic Uprising** (`obsidian_spike_tectonic_uprising`, +Earth, the Geomancer's) | numbers (+120% damage, +60% area) | **the ground does not close**: where each spike broke out a vent stays open 3 s, its cracks smouldering, and every 0.5 s it throws up a fresh spike of glass under the nearest enemy within 1.6 radii, for 60% of the spike. +80% damage, the eruption 30% wider |
| Basalt Lance (`obsidian_spike_basalt_lance`) | visible | stays: a column of three more spikes driving on through the crowd behind the target (2026-10-26) - the spell's line. Glacial Spike's Spike Run keeps the line ACROSS the front, as phase 1 split them |

Not taken: a ring of glass round the wizard (Sundering's - the Geomancer's form, "hold", raises one
every cast on top of all of this and is unchanged), a ring round the target (Glacier Crown's), shards
flung out flying (Shatter's), glass left lying to walk on (Caltrops'), a lodged core that erupts later
(Magma Core's). The splinters and the vent's spikes come up out of the ground under a body, as the spell
always has, and every one is under an enemy, so never on blocked ground.

| | before | after |
|---|---|---|
| band, Splinter Field + Tectonic Uprising, L4 / L5 / L6 / L8 | 31 / 58 / 81 / 461 | 39 / 69 / 106 / 430 |
| band, Splinter Field + Basalt Lance (each follower splinters too) | 41 / 77 / 107 / 524 | 70 / 72 / 128 / 490 (level 4 over a 20 s sample) |
| band, Deep Strata + Basalt Lance | 34 / 48 / 79 / 489 | 34 / 52 / 83 / 501 (at +30%: 26 / 43 / 94 / 488) |
| level 7, `--enemy-hp=30`: Splinter Field / Deep Strata | 723 / 532 | 748 / 502 (Deep Strata at +30% read 460, and went to +40%) |
| Tectonic Uprising at `--enemy-hp=30` (over Splinter Field's level 7) | 2,468 (x3.4) | 2,449 (x3.3) |
| the Geomancer, `--seek-apotheosis`: Sundering offered at | 10.0 min | 10.1 min |
| ... Obsidian Spike's damage over the 20 minutes (its share) | 461,658 (86%) | 544,525 (93%) |

The Geomancer comes out a sixth stronger on that seed: his path's two cards now reach the ring of the
crowd round each eruption and then keep erupting under it, where they only widened the disc. He was the
wizard the spell's history calls the weakest; if he proves too strong in play, the vent's 60% is the
knob.

**Needs eyes:** the vents smoulder in the earth ramp and throw up spikes for 3 s after every eruption,
so a level 8 Geomancer leaves a field of glass popping behind the front rank - whether that reads as the
ground breaking or as clutter, on a phone above all. Deep Strata's throw is hidden behind its own glass
at the moment it happens (the body is drawn over the glass, but the glass is as tall as the throw).

### Starfall, Stormsweep, Sleet Runes and Aegis Ward

Each spell below was audited card by card: **visible** (it already changes how the spell looks or
behaves - it stays, its words may be sharpened) or **numbers** (it only moves numbers - redesigned).
The ids never move: a redesigned card keeps its id, its element gain and its price, so saves
(`SeenAscensionIds`), the Ascension browser and the gates carry over; the element-path cards (Frostfall,
Iron Current) stay as they are. Verbs already owned were avoided: the pull (Hollow Star), the eight
Apotheosis verbs (burn, stop, link, hold, flood, swarm, pull, reflect), and every phase 1 card's.

How each one was measured: the band runs of 2e (`_BalanceHarness --minutes=8 --timescale=2 --immortal
--all-unlocked --character=0 --seed=11 --solo --upgrade-chance=1 --force-ascension --branch4= --branch8=`,
`--aim-facing` for the facing pair), damage a second at levels 4 / 5 / 6 and at the Ascension (L8), the
old card on the tree it shipped on (2959dba) the same afternoon, against the peer bands (40-60 / 70-130
at level 6 / 331-465). No Ascension changed in these four spells, so the doubling runs of "Every Ascension
doubles its spell" were not re-run; what moved under each Ascension is the level 4 card that feeds it.
Three other agents were measuring on the same four cores (load 12-52); every report's clock held at
1.00, but levels 4-6 swing by up to a factor of two between runs of one build (3, Toxic Spore Burst),
so read them as a band and the level 8 column as the firmer number.

#### Starfall

| card | audit | now |
|---|---|---|
| L4 Clear Night (+Light) | numbers: +2 stars a cloudburst, +15% | **Shooting Stars** |
| L4 Frostfall (element path) | visible (Ice's ramp, a slow) | unchanged |
| L4 Downpour | numbers: 40% more often, 15% smaller | **Skipping Stars** |
| L8 Starburst | visible: every star swells and explodes, throwing sparks | unchanged |
| L8 Constellation | visible: lit stars joined by lines that cut | unchanged |

- **Shooting Stars** (`starfall_clear_night`, +Light): every third star does not fall - it streaks in low
  across the whole rain, rim to rim, on a chord through a point drawn as a landing is (so the streaks are as
  even over the circle as the drizzle), in any direction, at 900 a second, and strikes everything whose centre
  is within its half-width (4, grown with the square root of the star's Area, plus a body's allowance) of the
  line as the head passes it, once, for 80% of a star. Drawn as a big white head, a hot tail and the line it
  has cut lit faintly at the width it struck, which goes out from the entry end once it has left. +10%. It
  aims at nothing, as every star does - the line is weather too. Under Starburst the streaks stay streaks
  (one star in three is never heavy); under Constellation they kindle nothing.
- **Skipping Stars** (`starfall_downpour`): every star lands at its slant and skips on twice, to the right -
  the way every star slants in, the key light behind it - like a stone across water: a low hop two landings
  wide, then a landing 75% as wide for 55% of the first, a second hop and a landing 55% as wide for 35%. A
  heavy Starburst star bursts and still skips (what comes out of the burst is a star its size); the skips
  kindle no Constellation star. No number rides along.

Departures from a first thought: a ring of rain at the rim ("halo") was Sundering's and the Kindling
Flock's shape; a comet or a squall line crossing the rain would have been the same line as Shooting Stars;
stars breaking in three mid-fall read as Starburst's sparks.

**Measured** (damage a second, L4 / L5 / L6, L8):

| run | L4 | L5 | L6 | L8 |
|---|---|---|---|---|
| before: Clear Night + Starburst | 36 | 47 | 67 | 411 |
| before: Downpour + Constellation | 34 | 54 | 91 | 391 |
| Shooting Stars, first build (streaks at 100%, +15%) + Starburst | 77 | 73 | 109 | 413 |
| same + Constellation | 47 | 43 | 78 | 382 |
| Shooting Stars at 80%, no bonus + Starburst | 44 | 52 | 68 | 353 |
| same + Constellation | 66 | 66 | 100 | 340 |
| **Shooting Stars, shipped (80%, +10%) + Starburst** | **68** | **83** | **154** | **435** |
| **same + Constellation** | **45** | **77** | **100** | **388** |
| **Skipping Stars + Starburst** | **53** | **64** | **97** | **405** |
| **Skipping Stars + Constellation** | **44** | **50** | **73** | **336** |

Every path sits in the level 8 band. A streak covers about three and a half landings' ground, so one star
in three streaking does most of what Clear Night's two more stars did; the first build was a third over
it at levels 4-6. Taken off its +15% it fell to 353 under Starburst - a streak is never a heavy star, so it
feeds the Ascension less than two more stars did - and +10% put it back. Its shipped Starburst row is the
top of the band at level 6 (154, on a run whose level 5 lasted 52 s in a dense crowd; the other two builds
read 68-109 there); if a playtest finds it hot mid-run, the streak's 80% is the knob. Skipping Stars sits
with the old Downpour under Starburst and a seventh under it under Constellation (336 against 391):
Downpour fed the web 40% more stars, and a skip lights none. It is at the foot of the band, where 2a
already put Constellation on anything but Clear Night.

**What still needs eyes:** whether a streak reads as a star and not as a beam at the game's own zoom, and
on the phone; whether the skips read as hops in a crowd (a still frame shows the twinkles stepping right,
not the arc); the streak's line over a bright floor. Screenshots: `sky_shoot*.png`, `sky_skip*.png`.

### The blasts: Arcane Explosion, Fireball, Meteor Swarm and Solar Flare (2026-10-29)

None of the four is a wizard's starting spell (the Pyromancer opens on Cinderbreath, not Fireball), and
no Apotheosis path names any of their ids (`ApotheosisCatalog`), so no form's gate moves with them.
Verbs avoided because another card or form owns them: the pull (Hollow Star - which is why Event Horizon
lost its own), a second shockwave (the Spirit Hammer's Aftershock - which is why Dual Shockwave did not
simply become the two rings its name promised), thrown shards (Shatter, Bone Spikes), a delayed eruption
in the target (Magma Core), a wall or ring of fire (Dragon's Breath), radial beams (Corona Discharge,
which stays), a chain hop (Chain Lightning), a drizzle round the wizard (Starfall), an orbit that looses
(Forge Swarm), and the Apotheosis verbs (burn, stop, link, hold, flood, swarm, pull, reflect).

#### Solar Flare - a sun round the wizard that grows while he stands still

| card (id) | audit | now: what the player sees |
|---|---|---|
| level 4 (`solar_flare_radiance`) | **numbers**: Radiant Glow, +40% area and 25% faster pulses | **Sun Dogs**: two mock suns hang either side of him on the line through his middle and flare with every pulse, each 60% as wide as the flare for 60% of its damage (and its Weaken). They hang 1.6 flare-reaches out, so they drift outward as the flare builds while he stands still. Pulses 20% faster |
| level 4 (`solar_flare_scorching_heat`, +Fire) | **numbers**: Scorching Heat, +45% damage and the tag | **Solar Prominence**: every pulse hurls two loops of fire off the flare's rim - a third at half charge, a fourth at full - at the nearest enemies just past its edge (out to 2.4 reaches); each arcs over and comes down in a burst 0.4 reaches across, for 80% of the pulse. +1 Fire at the hybrid's price, +15% damage |
| level 4 (`solar_flare_blinding_flash`) | **visible**: some of what it catches reels back, staggered | unchanged; reworded to say it |
| Ascension (`solar_flare_daybreak`, +Light) | **visible**: burns away every enemy bolt inside the flare (2d-bis) | unchanged |
| Ascension (`solar_flare_corona_discharge`) | **visible**: eight sunbeams past the flare (section 3) | unchanged |

Built in `ElementalPulse` by id (`SunDogsId`, `ProminenceId`). The mock suns and the loops are in the air
(`DrawLayers.Air`): the suns on a child of the flare, each with its own `SolarFlareVisual` for its burst;
the loops on a `TopLevel` canvas with a fixed pool of sixteen, so a loop in flight stays where it was
thrown. The prominences land on enemies, so nothing is placed on blocked ground.

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

- **Chain Lightning** keeps damage as its default and sells splitting as the fork. Storm Surge
  (id `chain_lightning_forked_surge`, once Forked Surge: +2 branches, x0.7 damage, and since
  2026-10-23 the spell's Water element path - section 2b) costs real damage; Cascade (+3 branches,
  +2 arcs) did too, at x0.6, until the Ascension rule below made it x1.2. Dense Current and
  Thunderhead are the branches that refuse to split and hit harder instead.
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

### Every Ascension doubles its spell (2026-10-26)

The developer: *"make sure that ascensions all feel important and big."* The rule that came out of it:
**an Ascension at least doubles what its spell does at level 7, and changes something the player can
see** - a new shape, a new verb, or the spell visibly larger. A card that is only a damage number on
the same shape is not an Ascension (Corona Discharge and Basalt Lance were; both now throw something
new - eight sunbeams past the flare, a column of three more spikes behind the target).

**How it is measured.** `scenes/_AscensionAudit.tscn` prints every card's numbers. Then, per spell, a
baseline and one run per Ascension at the same seed:

```
_BalanceHarness -- --no-save-writes --immortal --minutes=4 --xp-first=1 --xp-step=1 --upgrade-chance=1
                   --enemy-hp=10 --character=0 --solo --spell=<id> [--force-ascension --branch8=<id>]
```

Without `--force-ascension` a solo spell stops at level 7, which is the baseline; the ratio is the
"all dmg /s" of the top row of the per-level table, Ascension over baseline. Four things it took to make
that number mean anything, each of which produced a wrong table first:

- **`--enemy-hp`, not `--hp-scale`.** `--hp-scale` is the PLAYER's health. At normal enemy health both
  runs kill everything that spawns and read the same; Chain Lightning at level 7 read 83 dps at x1 and
  567 at x10. Read the last `HARNESS MINUTE` line's `alive=`: near the 140 cap is a fair run; an
  Ascension run at 20-80 alive is still clearing the field, so its ratio is a FLOOR - raise
  `--enemy-hp` (30, 60) until it is not. Contagion is the opposite case: at x30 its poison kills
  nothing, earns no XP and the spell never levels; it is measured at x12.
- **Spells the standing bot cannot use.** Hunter's Draw and Mirefoot need `--walk=0`; Sleet Runes and
  Stormsweep `--aim-facing`; Bramble Seed `--stand-still` (it plants at the feet, and the bot's walks to
  XP orbs left the patches behind - the more an Ascension killed, the more it walked). Frost Shard and
  Arcane Explosion need `--partner=magic_missile`: one shatters what something else killed, the other
  is charged by other spells' casts.
- **A run can break.** A solo bot is sometimes dealt hands without its spell's card for minutes and
  finishes at spell level 2-5; the report's level column shows it. Rerun it.
- **One run at a time per core.** Four in parallel on four cores was fine; more changes frame timing.

**What it found.** At the first, broken footing (normal enemy health) 30 of 70 read under x1.6, and
26 were raised; at x10-x60 most of those raises proved unnecessary and ten were undone. What was
genuinely short, and why:

- **Spiritual Weapon** (x1.3 for both): the evolution's ScaleMultiplier scaled the whole node, so
  Celestial Executioner's x2.1 swung the blades out to 210 px, round the outside of a crowd pressed
  against the wizard. The scale now grows the blades, not the orbit - x2.8 for both.
- **The eight Infinite Horizons on capped or spaced spells** (Gravewell, Hollow Star, Bramble Seed,
  Contagion, Iron Palisade, Kindled Ward, Glacial Spike, Cinderbreath): more casts and a faster
  cooldown land where the first casts are already working. Six carry a damage multiplier now (Hollow
  Star's and Iron Palisade's did not need one); Bramble Seed's the most, x2.6.
- **Corona Discharge and Basalt Lance**: damage only - built (above).
- Smaller raises where a card's own verb paid for less than its number promised: Cascade, Event
  Horizon, Anathema, Blade Tempest, Blizzard Engine, Doomcap, Mycelial Bloom, Constellation.

Measured after, against a crowd the spell could not clear, every Ascension is x1.9 or more except
the two Toxic Spore forms (x1.92, x1.94). Bramble Seed's are x2.00 and x2.15 at x60, standing still.
Where a baseline cannot level at x30 (Cone of Cold, Meteor Swarm, Starfall, Shadow Bolt) the x10
figure stands, scaled for any later change. The large ones (x5-11: Riptide's and Molten Shard's Infinite Horizon, the Griffin,
Thorn Vine's Infinite Horizon) are spells with a weak level 7 on a single target, and were left alone:
the rule is a floor. **Aegis Ward** is measured by eye, not by this: its Ascensions are a shield that
breaks into crystal (damage, built) or a flash that staggers and weakens (built), plus Reflective
Barrier's return at level 4.

**Bigger, not only stronger (2026-10-27).** The developer: *"kindled ward and the tidal wave attack
area should grow as they level up so they become more of a spectacle"*, and *"riptide size should end
up being a huge portion of the screen per shot when you take the ascension that increases size by a
big amount."* An Ascension that says "bigger" has to be bigger at the scale of the screen, not of a
skeleton:

- **Riptide grows every level**: an Area line at each level (px on its 8 px hit radius, 8 to 100,
  in steps that get bigger as it climbs), so the wave sweeps 40 px across at level 1 and 224 at level
  8 - a wall of water as tall as a pack. Its hit is a capsule ACROSS the heading
  (`ElementalBolt.CrestDepthFraction`, 0.35): as wide as the drawn crest and only as deep as its face,
  because a circle that size hit what was ahead of the water before the water reached it. **A wave's
  pierce grows with its width** (6 at level 1, 75 at level 8): on a flat 6 a 224 px wave met a whole
  rank at once and was gone a body into the pack. **Titan Cataclysm is a tidal wave**: x3 the swath
  (about 620 px at level 8, 800 with Amplified Impact - the height of a PC screen, the width of a
  phone's), +60% damage, 40 more pierce. The shader caps its foam, spray and wake in world pixels
  (`riptide_wave.gdshader`, `px`), so a wider wave carries more foam rather than bigger foam; the
  trail particles are counter-scaled and thrown from a band as wide as the crest; a splash stops at
  64 px; Spring Tide's lane is as wide as the wave that floods it.
- **Kindled Ward burns wider every level**: +28% strike radius a level (34 to 100 px by level 8), and
  the mote and its flare are drawn in proportion (`KindledWard.DrawScale`, the growth to the power
  0.75 - a level 8 ward is drawn over twice the size, a Titan's four times - so the mote never
  becomes a disc as wide as the strike); a grown ward throws a faint outer glow and its flare lingers
  up to 0.35 s. Titan's tint is dropped as Riptide's is - it turned a sun of candle light red.
- **The first pass was too small to see.** It grew Riptide to 100 px and the ward to 63 px by level
  8, and the developer asked again: photographed side by side, level 8 was barely bigger than level 1.
  A size that is meant to be a spectacle has to be checked in a screenshot of level 1 against level
  8, not read off the numbers.
- **Measured** (solo, level 8, 30x enemy health, cheap levels, seed 11, damage a second): Riptide's
  Titan 314 before, 2,210 after; its Infinite Horizon 2,609 after - Riptide had been an order of
  magnitude behind the other level 8s and is now among them. Kindled Ward's Titan 3,953 before and
  6,253 after at +150% damage, against Infinite Horizon's 2,731, so Titan's damage came down to +80%.
- **Measured again after the steeper growth**, same settings, old growth against new: Riptide's
  Infinite Horizon 2,570 to 5,443 and its Titan 2,272 to 3,382, and at level 6 it went from the
  bottom of the field (130-160) to the top (370-390, the field running 120-420). Kindled Ward's
  Infinite Horizon 2,635 to 4,598, its Titan unchanged at about 4,770. The same day's level 8 Titans
  elsewhere ran 1,700 (Gravewell) to 4,700 (Glacial Spike). Riptide's Infinite Horizon sits about 15%
  over the top, and that was left alone on purpose: cutting its level 6 and 8 damage lines from 3 to 2
  moved it nothing (5,494, inside run-to-run noise), because a wave that wide hits nearly everything
  on the field and is limited by how many enemies there are, not by what each hit deals. The cut
  would only have weakened it against a single elite or boss, where the width gains it nothing.

### Arcane Explosion charges off your other spells

**Today:** a pulse around the player on a 1.5s timer. Indistinguishable in feel from Solar Flare and
Toxic Spore Burst.

**Proposed:** it has no timer. Every cast of **any other spell** adds a charge; at full charge it
detonates. A loadout of six fast spells sets it off constantly; a loadout of slow heavy spells makes
it rare and huge.

This makes one spell in the game care what the other five are, which is the closest thing to a
"build" the spell system currently has, and it is very Arcane — magic about magic.

### Toxic Spore Burst sows mushrooms - BUILT (2026-10-23)

**What was there:** a pulse round the player on a 2.2 s timer (ElementalPulse, which Solar Flare still
uses). The player: *"toxic spore burst is very small, and it doesn't appear to get much bigger with
even the ascension that says it is a 'colossal expansion'."* Both true, for four reasons:

- `ElementalPulse` took its reach from `GetRangeAtLevel` x the player's area and **never read
  `GetAreaMultiplierAtLevel`**, so the default archetype's Titan Cataclysm ("+150% Damage, +100%
  Area") and Amplified Impact (+30%) grew nothing. **Solar Flare has the same bug today** - Radiant
  Glow's +40% and Daybreak's +150% area do not reach its hit either - and nothing reads its Scorching
  Heat `SpellEffect.Burn`. Left alone here; Solar Flare is another session's.
- The evolution's x2 `ScaleMultiplier` is applied by `ApplyLegendaryVisual` once, when the persistent
  pulse is created - at level 1, before any evolution exists - so even the DRAWING never grew.
- The sprite was scaled to about three quarters of the 90 px it hurt (a 72 px `FrameSize` against a
  64 px sheet) and squashed to 0.62 high, under the player's own feet.
- Its `.tres` had one level-up. Levels 3-8 were the code's generic +1 damage, and the "Area +0.16"
  and "DoT per tick +1" lines `EnsureRelevantLevelUps` invented for it were read by nothing.

**Issue #68 planned it as a proximity trip** ("fires when you are surrounded"). **The player's request
superseded that:** *"it should spawn mushroom spores that explode around the map, and the
mutations/ascensions should accompany that - such as making the spawn explosions slower but much
bigger and more potent, or leaving poison clouds where they explode, or changing the element of the
spores along with the animation to reflect it."* The surrounded trigger is not built.

**What it is now** (`SporeMushroom`, `Player.FireSporeMushrooms`): each cast sows mushrooms among the
enemies within 280 px - each a little in front of a different enemy, on the side it is walking toward,
kept apart so a cast covers several knots - and every one **sprouts, swells for a moment, then bursts**
in a cloud of spores thrown out to exactly the reach it hits, poisoning everything inside. (It drew a
ring at that reach through the swell until 2026-10-23; section 2c.) Amount adds mushrooms. It aims at ground near enemies, has a fuse the player can read,
and sits in the mid band where it used to be a ring on the player's feet - so the three pulse zones of
section 1 are two.

- **The drawn size is the hit size.** `Cap.Radius` is the one hit authority; the spores' flight, the
  ring of puffs that lands on the rim, the cloud and the burning patches are laid out of it, every puff
  and flame with its outer edge inside it. Every area term - levels (AreaSize, in px), the player, every evolution's
  `AreaMultiplier` through `GetAreaMultiplierAtLevel` - is folded in once, in `SporeMushroom.For`.
- **The table** (written out in full in the `.tres`): 6 damage, 3 mushrooms, 80 px bursts, 2.8 s; even
  levels add a mushroom (2, 6, 8) or poison (4), odd levels +8 px, every level +1 damage, poison 2/s
  for 3 s +1 at levels 4 and 7. Level 7: 12 damage, 5 mushrooms, 104 px, poison 4. Cards say
  "Mushrooms +1" (`Player.ProjectileNoun`, which also covers Black Tentacles).
- **One node, several mushrooms.** A Mycelial Bloom chain is slots in its sown mushroom's fixed array,
  not new nodes, so the one place the spell makes things quickly instantiates and allocates nothing.
- **Art** (`tools/art/spore_mushrooms.py`, authored 1:1, shown x2): a 14-frame mushroom (sprout 5,
  swell 4, burst 5) on the 32 cell, the same on the 48 cell for Giant Caps and the 64 cell for
  Doomcap, each in poison and fire; a 6-frame spore puff in each; a 4-frame flame. The old pulse
  scene, its sheet and its `spell_fx.py` entry are gone.
- **Sound:** the fire loop's element cast cue at sowing, impacts through `DealDamageToEnemy`, and
  `spell_summon` as a Doomcap breaks ground. No new file.

| milestone | branch | tags after | what it does |
|---|---|---|---|
| level 4 | **Lingering Rot** | Poison 2, Grass 1 (+12% cooldown) | each burst leaves a cloud as wide as itself for 3.5 s that re-poisons anything inside every 0.5 s; poison +1 |
| level 4 | **Ember Caps** | **Grass 1, Fire 1** - trades Poison away | orange caps and ember spores; bursts +50% and light 4 patches inside the burst for 2.5 s that burn for 30% of the burst every 0.5 s, at most once per enemy per 0.5 s across every mushroom; no poison |
| level 4 | **Giant Caps** | unchanged | two fewer mushrooms on the 48 cell, 67% slower to swell, 60% wider, +80% damage, +20% cooldown |
| level 8 | **Doomcap** | unchanged; gate is the spell's CURRENT tags | one titan on the densest pack instead of the scatter, 2.6 s to grow, 180% wider; +15% damage per mushroom it replaces (x1.9 at six); +40% cooldown |
| level 8 | **Mycelial Bloom** | Grass +1 (+12% cooldown) | every burst sprouts 2 mushrooms past its rim (half damage, 80% size, twice as fast), and each of those 1 more |

**The element change is a trade, not a third tag.** Toxic Spore Burst is a hybrid, and a hybrid may
never name a third element (`.ai/gameplay-design.md`, "Element growth through evolution"), so "changing
the element of the spores" is `SpellEvolutionOption.ReplacesElement`: Ember Caps removes Poison and adds
Fire, and the spell still names two. That is new, and the rules section and
`RegressionChecks.ValidateEvolutionElementCeiling` now know it. The mushroom itself is Grass, which no
branch touches, so Mycelial Bloom deepens it on every path; and Doomcap, which names no element, is
gated on whatever the spell has become - which needed `Player.GetAscensionGateElements` to read the
spell's current tags rather than its resource (it used to ask an Ember Caps build for 4 Poison).

Only one element branch, on purpose: a milestone keeps one untagged option and one that deepens, so
a third slot was all there was. Frost Caps (ice spores that slow) and a Darkness branch (Vulnerable)
are the obvious next ones if the level 4 ever grows a fourth option, and the art generator already
draws by element ramp.

**Measured** (`_BalanceHarness --minutes=9 --timescale=2 --immortal --all-unlocked --spell=toxic_spore_burst
--solo --upgrade-chance=1 --branch4= --branch8= --force-ascension`, one run at a time). Two harness
additions came with it: `--force-ascension` takes `--branch8` the moment a solo spell reaches its card
cap - a solo bot holds one spell's tags and is level 9 at minute 6, so it never meets either Ascension
gate and no level 8 branch could be measured before - and an "all dmg" column, because poison ticks
arrive with no source and a poison spell's attributed column is only its impacts. Damage per second at
level 8, all damage: Lingering Rot + Doomcap 392, + Mycelial Bloom 337; Ember Caps + Doomcap 401,
+ Bloom 411; Giant Caps + Doomcap 465, + Bloom 388. The same bot gives Black Tentacles + Elder God Maw
331 and Glacial Spike + Titan Cataclysm 390; the old pulse with Titan Cataclysm made 102 (a 19 s
sample - it reached level 8 at 5.7 minutes). Levels 4-6 swing by a factor of two between runs of the
same build, with how dense the crowd is when the bot holds that level, so read them as a band: 40-60
at level 4, 70-130 at level 6, against the old pulse's 55 and 71. Two changes came out of the first
batch: burning is held to once per enemy per half second across every mushroom (`burnedThisBucket`;
before it Ember Caps made 271/s at level 6 on a crowd pinned in five mushrooms' patches), and
Doomcap's per-mushroom bonus went 20% to 15%. Doomcap's level 8 barely moved for it - at that size
it kills most of what it covers, so its number is the spawn rate as much as the spell.

### Solar Flare grows while you stand still - BUILT (2026-10-27)

**Built:** every pulse reads `Player.StillSeconds` and goes off wider and hotter for it - up to
**+60% reach and x2 damage after four seconds held**, reset by a step (`ElementalPulse.StillCharge`,
`StillGrowthSeconds`, `StillRadiusGrowth`, `StillDamageGrowth`). Corona Discharge's beams grow with
the pulse they come out of. The build-up shows as light pooling on the ground at his boots, inside
the Brazier's ember ring, throbbing quicker as it fills (`SolarFlareVisual.SetCharge`); the reach
shows only in the burst, per section 2c. Measured solo with the balance bot, seed 3, four minutes:
walking, the old flare dealt 16,934 (the kiting bot drags it through crowds); standing still, only
4,720. Standing still now deals 21,954 - about 1.3x the walking figure, so holding ground is a real
choice and not the only one. Walking is unchanged.

**Before:** a pulse on a timer, at 90px.

**Proposed:** the same pulse, but its radius and damage **ramp while the player holds position** and
reset when they move. It becomes the anti-kite spell — the thing you take when you want to stand in
one place and hold it.

Note the deliberate symmetry: Solar Flare rewards standing still, Shadow Bolt rewards moving, and
Hunter's Draw charges by moving and fires when you stop. Three spells that read the same input and
disagree about it is a build decision.

**Standing still is no longer Solar Flare's alone (2026-10-23).** The Brazier set and the Taproot
boon reward it too (`.ai/passives-and-items.md`, section 5 "The Brazier burns while you stand
still" and section 3c). They were built not to collide with this rework (#68):

| | Solar Flare (planned) | the Brazier (set) | Taproot (boon) |
|---|---|---|---|
| what grows | this one spell's pulse: its radius and damage | every spell's damage (+30%) and area (+15%) | healing, 1.5 HP a second |
| on moving | resets - the anti-kite spell | cools over 3 s: a step costs a slice | breaks at once, re-roots after 1.5 s still |
| costs | a spell slot | three chests | a boon slot |
| asks | stand still with them inside its 90 px | stand still anywhere | stand still anywhere |

They stack, and that is the turret build rather than a collision: a player holding all three is
asking to stand in one place, and each pays a different thing for it. The one thing to account for
when #68 is tuned is that the Brazier's +15% area also widens the flare. **Build it on the same
stillness**: `Player.IsStandingStill` and `Player.StillSeconds` (`Player.TickStandStill`) - intent,
not displacement, the `walking` test `MovePlayer` makes for the crowd slowdown - so a shove never
resets the flare when it does not cool the Brazier, and the three cannot disagree about what "still"
is. Hunter's Draw keeps its own displacement test, because it charges on distance covered.

### Cyclone Slash becomes a running whirlwind

**Was:** an orbit at 60px, doing the same job as Spiritual Weapon.

**First proposed:** a sweep across the arc you are facing. Overtaken by **Stormsweep** (2026-10-25),
which already sweeps a band on the side you face; a second "swipe where you face" would have been
the collision this document exists to remove.

**Built instead (issue #68, 2026-10-28, the developer's choice of three):** it stays a ring and gains
a demand - **keep moving**. A gust builds while the player walks (2.5 s to full): the ring widens
from 0.6x to 1.6x its orbit, spins from 0.6x to 1.5x as fast, and its blades grow by 30%. When he
stops it collapses to the tight, slow ring within half a second. It is the counterpart of Solar
Flare, the Brazier and Taproot, which all pay for standing still, and it reads the same
`Player.IsStandingStill` - intent, not displacement - so the four can never disagree. Hunter's
Draw also charges by moving, but on distance covered and to be released by stopping; this one is
spent by stopping. **Typhoon Core** (level 8), the "permanent hurricane", is now literally that: the
Ascension that never collapses. `OrbitingBlade` draws each blade as a bright disc - the size it hits
at - in a crescent of the wind it has just cut, trailing longer and brighter as the gust rises, with
faint streaks turning inside the ring; the placeholder discs are gone.

Measured against the old orbit (solo, Dual Windblades at 4, 30x enemy health, cheap levels, seed 11,
damage a second). A bot that holds its ground could not measure a spell that pays for moving, and
one that walks a bearing outruns the swarm and never levels, so the harness gained **`--circle=R`**
(with `--immortal`): it walks a circle R px across, round and round, and the crowd keeps catching it.

| | level 4 | level 6 | level 8, Typhoon Core | level 8, Vortex Reaper |
|---|---|---|---|---|
| moving (`--circle=160`), old | 65-128 | 109-236 | 910 | 655 |
| moving, new | 156 | 195-255 | 1,038 | 947 |
| holding ground, old | 132-154 | 191-211 | 1,203 | 622 |
| holding ground, new | 68-75 | 78-93 | 877 | 321 |

Moving it is stronger than the orbit it replaced at every level; standing it is about half - which is
the trade. Typhoon Core held still comes out under the old one even though it never collapses: its
full ring (1.6x) runs wider than a crowd pressed against a player who is not moving. The Cyclone was
already among the weakest level 8s (the field ran 1,700-4,700 the same week) and moving it is no
weaker for this.

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

`Player.FireBoltSpell` takes a `BoltAim`. **Thorned Vine** and **Black Tentacles** aim at the densest
cluster (`FindDensestEnemy`: the in-range enemy with most others within 110 px, scoring at most 48
candidates so a late swarm cannot make one cast quadratic). **Molten Shard** aims at a random enemy
in range. Both fall back to the nearest enemy, and both still only cast when the nearest is in range,
so a spell never fires into an empty field. In a headless run, 3 of 10 Thorn(ed) Vine and 4 of 7 Molten
Shard casts left at more than 20 degrees from the nearest enemy.

### Meteor Swarm becomes a barrage

**Today:** a single heavy ground strike on a 5s timer — which is now exactly what the reworked
Fireball does.

**Proposed:** many small impacts scattered over an area, saturating it. Fireball is the sniper
round; Meteor Swarm is the artillery. Same element pair, completely different problem.

**Built (2026-10-25), and it looks like the web demo now.** *"meteor swarm should look like the
meteors on the web based demo version. it does not look as good on this version."* The demo
(`SurviveDemo.tsx` on the site) drops 2 + level meteors, 0.12 s apart, each on a random foe; a meteor
is a tiny pixel-art head with a stepped trail - four squares in the fire ramp, white-hot at the head,
stepping back up a diagonal from the upper left - and it lands as the `meteor-impact` crater. The game
had a fixed four, scattered round the nearest enemy and landing together, each one the crater sprite
tinted, rotated and slid straight down: an orange smear.

- **Look** (`MeteorImpact`, drawn in code, no scene children): the demo's streak at the game's scale -
  its numbers in demo pixels x2.5 for the path (150 left, 350 up, 0.45 s, linear) and x3 for the squares
  (12/12/10/10 px), stamped on the 2 px grid, so the head is still a few device pixels on a phone - then
  a crater in the ground and the `ImpactDebris` rock thrown to exactly the hit radius. A wider blast is a
  bigger rock (by the square root), never a longer fall. **Not taken from the demo:** its flickering red
  ellipse on the landing point - an area marker (2c).
- **The crater lies on the ground (2026-10-28).** *"the crater from meteor should not cover other
  elements that would stand on top of the ground, like the player. fires from meteor and the crater
  should look better and more detailed."* The demo's crater sprite was flashed for 0.35 s on the
  meteor's node at z 6, over every actor, so a meteor beside the wizard painted an orange ellipse across
  him; Magma Craters' fire was Ember Caps' 16 px lick stamped in rings and multiplied flat red by the
  evolution's tint. Now `CraterLayer` (one pooled node per arena, 40 slots) draws each crater at
  absolute z -15 - over the terrain and the walkable decals, under every actor, Scorching Ray's scorch
  rule - and its fire at -14. The hole is `scenes/shaders/crater.gdshader` (`CraterGround`): a scorched
  floor that is molten at the hit and crusts over as it cools, the crust's seams glowing last; an inner
  wall lit on its far side; a lumpy raised rim whose crest catches the key light, an `occ` shadow at its
  foot away from the key; cracks out through the rim; soot rays and rocks short of the hit radius; a
  white flash in the bowl for the first fifth of a second. Pixel art in the 2 px grid at any radius,
  every crater one batch (its numbers ride in the quad's vertex colour). The fire is
  `tools/art/crater_fire.py`'s sheet (`CraterFire`): a blaze, a low lick and a smoke puff, 16x24 at x2.
  A plain impact blazes in its bowl for under a second, smokes, cools in 1.3 s and dissolves a second
  later; Magma Craters holds the crater molten and fills the burning radius with fire - blazes in the
  bowl, licks on the ejecta - catching outward and going out flame by flame. The streak and the debris
  are in the air and stay over the actors; the evolution's tint reaches the fire at 30% and the ground
  not at all, so the white-hot heart survives Magma Craters' red. Cataclysm's craters are the same
  crater and fire (`CataclysmField`, which drew at z 1). `scenes/_CraterSheet.tscn` photographs the
  whole life cycle through `_UiShot`.
- **Aim and volley** (`Player.FireMeteorSwarm`): each meteor on a random enemy in range, led by that
  enemy's velocity for as long as the meteor takes to land (capped short of the player), with the
  demo's small scatter; 0.12 s apart, packed tighter past 1.2 s of volley. Nothing in range now means
  the spell waits, rather than dropping the volley round the player's feet.
- **Every number on its cards reaches it.** The count is the projectile count plus Amount (it was a
  fixed four, so Meteor Shower's +3 and Orbital Bombardment's +6 did nothing): 3 at level 1, +1 every
  level, written out in the `.tres`, and the cards say "Meteors". Level 3's "Area +0.16" is read as
  +16% of the blast (nothing read it). **Magma Craters** promised burning craters for 4 s and nothing
  read its Burn: the crater now burns for that long, ticking a quarter of the impact every 0.5 s, drawn
  as fire filling it - under the actors, like every burning patch (the crater bullet above). Cosmic Impact's
  `KnockbackBonus` is still unread (spells never knock back, `Player.DealDamageToEnemy`); its card names
  no number for it.

Damage a second by level, `_BalanceHarness --minutes=6 --timescale=2 --immortal --all-unlocked
--character=0 --spell=meteor_swarm --solo --upgrade-chance=1 --branch4=<L4> --branch8=<L8>
--force-ascension`, one run at a time on a machine other sessions were also loading, so read these as
bands:

| | L4 | L6 | L8 |
|---|---|---|---|
| before: a fixed four, Meteor Shower / Orbital Bombardment | 46 | 85 | 241 |
| aimed volley, base 9, Shower / Orbital (two runs) | 37-38 | 74-80 | 295-302 |
| aimed volley, base 9, Magma Craters / Extinction Event | 37 | 93 | 374 |
| **shipped**: base 10, Shower / Orbital | 37 | 77 | 280 |

Peers in the same bot (Gravewell, above): 40-60, 70-130, 331-465. Level 4 sits a little under the band
and holds for barely half a minute, so it is the noisiest column; the +1 base damage is a nudge the
bot cannot resolve. Two things the per-level table hides: the volley spreads across the whole 450 px
range where the old four bunched on the nearest pack, so each meteor finds fewer bodies; and it kills
more overall - every new run levelled faster (Ascension at 2.4 minutes against 4.0, level 13-15 against
12 at minute 6), so its level 8 column covers more of the thinner early swarm than the old one did.

### Scorching Ray ramps on a held target - BUILT (2026-10-27)

**Built:** each cast remembers what each beam burned (`Player.FireScorchingRay`). A beam on the
enemy it struck last cast burns **+40% per cast in a row, to x3 on the sixth** (`ScorchRampPerCast`,
`ScorchRampCasts`); a target it did not strike last cast starts cold. It **holds**: last cast's
targets still in reach are aimed at first, hottest first, then the nearest - a beam that let go of a
boss whenever a skeleton stepped closer would never ramp on anything worth it. A hot beam thickens
and whitens (`ScorchingRayBeam.Heat`). The bot also showed it was the weakest attack by far - solo,
seed 5, six minutes: Magic Missile 40,217, Chain Lightning 65,868, Scorching Ray **10,411** (three
beams of 3 damage every 1.6 s, about 5.6 a second against Magic Missile's 20). With the ramp it was
18,504; with its base damage raised from 3 to 6 as well, 27,562 (and 20,707 on seed 9) - under the
baseline in the open field, and the one to hold on a boss.

**Before:** a beam that picks targets and deals flat damage.

**Proposed:** damage **ramps the longer it stays on the same enemy**, resetting when it switches.
Our only beam should feel like a beam: the fantasy is burning *through* something, and that requires
the beam to remember.

---

## 4. What deliberately does not change

- **Magic Missile** stays a plain nearest-enemy projectile on a timer. Keep one honest baseline, so
  everything else reads as a departure from a thing the player knows.
- **Chain Lightning** keeps the chain and keeps being the damage one; see above for how it and
  Shadow Bolt divide it.
- **Spiritual Weapon** stayed the orbit until 2026-10-28, when it became a weapon that strikes from
  above in one of three forms (section 2f) - the request was the developer's, and Cyclone Slash had
  kept the orbit by then.
- **Obsidian Spike** stays an eruption with no travel time — that is already a distinct answer to
  "how does damage arrive".
- **Hunter's Draw** is untouched. It is the model the rest of this document is arguing toward.
- **Cinderbreath, Riptide, Fireball, Aegis Ward** were reworked recently and are already on-pattern.

---

## 5. Dependency order

Nothing here is blocked on new art; three of the nine draw themselves.

| needs | spells |
|---|---|
| `Player.FacingDirection` (variety phase 1) - **built 2026-10-25** with Stormsweep and Sleet Runes; not re-pointed yet | Cone of Cold (Cyclone Slash went another way - section 3) |
| `SpellTargetingMode` load-bearing (phase 2) | Molten Shard, Thorn Vine, Glacial Spike, Void Lance, Meteor Swarm, Black Tentacles |
| Trigger conditions (phase 3) | Frost Shard, Arcane Explosion (Toxic Spore Burst went another way - section 3) |
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
