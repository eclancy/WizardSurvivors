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
| level 4 | **Clear Night** | Light 2 | +2 stars every cloudburst, +15% damage |
| level 4 | **Frostfall** (the element path) | Light 1, Ice 1 | +20% damage; every hit slows 30% for 1.5 s; every star falls, twinkles and bursts in Ice's ramp |
| level 4 | **Downpour** | unchanged | stars fall 40% more often (cooldown x0.71), each 15% smaller |
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
| **Scorching Ray** - a beam that ramps on a held target | **Flashpoint** (today's AI, which already bursts): at full heat the target bursts. **Handoff**: when the held target dies the ray jumps to the nearest, keeping its heat. **Burn Through**: the beam runs on past the target to the screen's edge | **Prism**: at full heat the ray splits into three rays at three targets. **Sunlance**: at full heat it widens into a lance that sweeps from target to target |
| **Thorned Vine** - a whip vine that lashes a pack | **Barbed Lash**: the lashed bleed. **Snapback**: the whip cracks out and back, hitting twice. **Entangle**: the front of the pack is rooted 0.6 s | **Thicket**: where the whip lands, thorny vines stay on the ground 3 s. **Hydra Vine**: three vines lash three packs at once |
| **Gravewell** - a proximity trap that roots | **Bone Spikes**: when it springs, bone spikes throw out round it. **Quickset**: wells arm at once, and can be laid on the move. **Grave Chill**: the rooted are Weakened | **Mass Grave**: one great pit that roots everything in a wide circle and drains them. **Grave Line**: wells laid in a line along your path, a minefield behind you |
| **Mirefoot** - bog pools laid as you walk | **Sucking Mud**: the first enemy into a pool is rooted briefly. **Long Stride**: pools join into a continuous trail. **Fen Gas**: pools breathe poison clouds | **Great Bog**: when you stop, the pools near you join into one marsh that slows everything in it. **Will-o'-Wisps**: wisps rise from the pools and seek enemies |

### The seven with four template names (their element-path card stays)

| spell | AI | HA | TC (Ascension) | IH (Ascension) |
|---|---|---|---|---|
| **Riptide** | **Breaker**: the wave knocks back hard | **Crosscurrent**: the wave leaves a current 2 s that carries enemies sideways | **Tidal Wall** (today's look, renamed) | **Spring Tide**: three waves in a fan |
| **Hollow Star** | **Crush**: the collapse hits harder for every enemy in the knot | **Twin Stars**: two wells, the knots pulled toward each other | **Black Sun**: the star stays and collapses three times | **Nova Seed**: the collapse throws out shards that knot the next pack |
| **Bramble Seed** | **Burrs**: thorns cling and slow the ones that leave | **Runners**: the patch sends out runners that seed a second patch | **Bramble Keep**: one patch three times as wide with a wall of thorns at its rim | **Seedstorm**: seeds fall in a scatter round you, a patch wherever they land |
| **Contagion** | **Fever Burst**: a carrier that dies bursts, dosing all near it | **Quickening**: the plague jumps the moment it is cast, not on death | **Pandemic**: every carrier on screen bursts at once, every few seconds | **Plague Wind**: a drifting cloud that doses all it passes |
| **Iron Palisade** | **Spiked Rail**: enemies that touch the wall are shoved back and bleed | **Quick Raise**: the wall goes up where you stop, in a ring round you | **Iron Ring**: a closed ring of stakes round the pack nearest | **Rolling Wall**: the wall marches forward, carrying what it meets |
| **Kindled Ward** | **Flare**: the ward bursts when it first meets an enemy | **Wisp Trail**: it leaves embers on its path | **Pyre** (today's bigger burn, renamed) | **Kindling Flock**: three wards circling you instead of one drifting |
| **Cinderbreath** | **Backdraft**: the cone ends in a burst at its tip | **Ember Wake**: burning ground where the cone swept | **Dragon's Breath**: the cone doubles in reach and leaves a firewall | **Fire Fan**: three cones at once, fanned |

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
    was withered by the next cast before it had grown. +50% damage.
  - **Thornstorm** (L8, was -50% cooldown, three more patches, 20% harder slow, +160%): every grown
    thicket throws a ring of 24 thorns out to twice its width every 0.8 s; a thorn hits what it is drawn
    flying through, never the whole ring. Two more patches may stand, -30% cooldown, +40%.
    **Departed from the proposal:** Seedstorm (seeds scattered round you, a patch where each lands) was
    Wildwood's verb outright.
- **Contagion** (`Contagion.cs`, `Player.FireContagion`). The marks stay on the floor, everything new is
  in the air over the bodies.
  - **Fever Burst** (L4, was +35% damage and a 30% longer jump): a carrier that dies bursts - everything
    in the jump is hit for 80%, carrier or not - and leaves a fever cloud hanging where it fell for 1.5 s
    that infects whatever walks into it. +25%. **Departed:** the proposal's "dosing all near it" was what
    a death already did; the hit and the cloud are what is new.
  - **Outbreak** (L4, was -30% cooldown and the poison 25% faster): the first carrier bursts the moment
    it is infected, through its own pack (60% of a jump), and still jumps again when it dies.
    **Departed:** named Outbreak, not Quickening, which is one of Gale Blade's cards; the burst's reach
    was cut to 60% of a jump after the first build measured twice its peers at level 4.
  - **Pandemic** (L8, was +150% damage and a jump twice as far): every 2 s every carrier on the field
    bursts at once without dying, on one shared beat - the marks swell before it - infecting what is
    round it and hitting it for 100%, at most once an enemy a beat however many carriers stand round it.
    Jumps 30% farther, +30%.
  - **Plague Wind** (L8, was -50% cooldown, three more carriers, +8 reach, +65%): each cast also looses
    a thin cloud of the plague that drifts out from the wizard through the carrier's pack and on for 5 s,
    infecting what it passes into that plague; a maze wall breaks it up. Each plague reaches 8 more,
    -30% cooldown, +30%.
- **Iron Palisade** (`IronPalisade.cs`, `Player.RaiseIronPalisade`). The stakes are on `Air` (they were
  at z 1, over the bodies already), the caltrops on `Ground`.
  - **Spiked Rail** (L4, was +35% damage and a 30% longer wall): a spiked rail runs along the stakes; what
    touches the wall is thrown back 80% harder and bleeds - drops fall from it - for 2 s, half a cut every
    half second. +25%, 15% longer.
  - **Caltrops** (L4, was -30% cooldown and cuts 25% more often): when the wall comes down its stakes
    break into caltrops strewn on its far side (inside a ring) for 3 s, cutting what walks over them for
    75% every half second and slowing it 50%. -20% cooldown. **Departed:** Quick Raise put the wall up in
    a ring round the wizard - Sundering's verb (hold), the Geomancer's Apotheosis, a ring round him that
    shoves the near ones back - so the cadence card leaves something behind instead.
  - **Iron Ring** (L8, was +150% damage and twice as long): the stakes close into a ring (radius 1.1
    half-lengths) round the pack the nearest enemy stands in, its centre pushed off the wizard so it never
    pens him; inside is shoved back in, outside kept out, whatever leans on it cut. No stake is raised on
    water or a wall. +80%.
  - **Rolling Wall** (L8, was -50% cooldown, three more walls, 1 s longer): once raised the wall marches
    forward at 75 px/s, taking what is just ahead of its face along with it and cutting it, and halts at
    water or a wall. One more wall a cast, 1 s longer, -30% cooldown.

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

Black Sun sits below the Titan it replaced on the 8-minute run on purpose: that Titan was twice the top
of the level 8 band (331-465, section 2f), and Black Sun's three collapses still more than double the
spell against a crowd it cannot clear.

**Cost, honestly.** Each card that changes behaviour is a hook in the spell's script, a row in
`EvolutionStatReaders`, a look (often an effect in `tools/art/spell_fx.py`), and a balance run
against today's card (`_AscensionAudit` and the harness, section "Every Ascension doubles its
spell"). Roughly one to two hours a spell for the seven with nothing of their own, a little less for
the other seven: two to three days in all, best built spell by spell so each can be played as it
lands. Gale Blade, Shadow Bolt and Spiritual Weapon (hand-written but almost all numbers) are the
next tier after these fourteen. (Spiritual Weapon's five went on 2026-10-28: each of its three forms
has five of its own, section 2f.) Evolution card icons (each card its own picture rather than the
spell's, tinted) belong with open issue #52.

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
