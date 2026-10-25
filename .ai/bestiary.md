# The bestiary - every chapter's own cast

Eric, 2026-10-24: *"move the enemies on each level to be unique to that level. the forest should
have evil forest creatures, the crypts should have evil undead, etc. each stage should have it's
unique set of creatures."*

Before this, six behaviour roles (the Hexer, the Skull Sentry, the Lunger, the Exploder, the
Slammer, the Summoner) were rolled in every chapter with one look each, the Warden was the recurring
miniboss everywhere, and seven of the nine chapter tables drew on the same four bodies. Only the
Enchanted Forest (wolves, brambles, wisps) and the Sketchbook (the kids' drawings) had anything of
their own.

This doc is the design of every chapter's cast, how the code holds it, and the plan for drawing it.
It was written in two phases:

- **Phase 1 (done, branch `content/bestiary`):** the design below, and the plumbing - a roster per
  chapter, in its own file, read by every site that puts an enemy on the field. **No spawn changed**:
  every chapter's roster points at the scenes it spawned before, through stand-ins, so behaviour and
  balance are exactly as they were.
- **Phase 2 (to do):** draw each chapter's creatures. Drawing a creature is *creating files at paths
  its roster already names*; the chapter switches to it by itself, and the startup validator lists
  what is still left (section 9).

Read with `.ai/world-and-tone.md` (why), `.ai/art-direction.md` (the contract), and
`.ai/enemy-behaviour.md` (the behaviour classes and the rules any enemy satisfies).

---

## 1. One mark, many bodies

`world-and-tone.md` says the horde is one colour, because shadow creatures serve one will. That is not
in tension with nine casts; it is what produces them. **Shadow has no shape of its own, so it wears
what it took** - and what it took is different in every place. A wood he took gives him its wolves,
its dryads and the woodsmen who worked it; a crypt gives him its dead; a fen gives him its peat
cutters and its toads. A chapter's cast is that place's own life, taken.

What stays identical everywhere is what says *whose* they are:

- **The eye.** One to two pixels of cold `arcane`, the same on every creature he took, in every
  chapter. It is the only emissive budget a taken creature has.
- **Built things burn.** The carve-out stands: a construct is lit by its maker, so it carries `fire`
  where a taken thing carries `arcane`. Every chapter's turret is something he built (the Skull Sentry
  was the first), and **the Emberdeep, his seat, is the one chapter whose whole cast is built** - so it
  is the one chapter where everything burns, which says whose house it is before the last boss does.
- **The deep half of every ramp**, no outlines, the 1 px rim. A chapter's identity comes from what its
  creatures *are* - their material rows and their silhouettes - never from a `modulate` tint.
- **No willing followers.** Every caster is a taken person, never an acolyte or a cultist
  (`world-and-tone.md`, the Cultist note). Names say what a thing was, not that it serves.
- **Serious, never cute; swollen, never gory.** The tone rules hold for every chapter. A toad that
  bursts is swollen, not rotten; nothing bleeds.

## 2. Rules every new creature follows

**Roles and the classes they use.** No new code: every job is an existing behaviour class with new
art and the same numbers.

| Role | Class | Stand-in (its stat block) | Notes |
|---|---|---|---|
| Bulk | `Enemy` | `scenes/enemy.tscn` - Speed 125, rect 40.5 x 24.3 | the baseline everything is read against |
| Fast | `Enemy` | `scenes/FastEnemy.tscn` - Speed 185, r 18.9 | **a summoner's minion is its chapter's Fast** |
| Heavy | `Enemy` | `scenes/TankEnemy.tscn` - Speed 115, r 25.65 | also what a side event asks for when it wants a heavy body |
| Wall | `Enemy` | `scenes/SlowEnemy.tscn` - Speed 95, r 21.6 | |
| Ranged | `RangedEnemy` | `scenes/HexerEnemy.tscn` - Speed 88, r 20.25, default cast timings | the back line; walks |
| Turret | `RangedEnemy`, `Rooted` | `scenes/SkullSentry.tscn` - Speed 0, KR 1.0, 3 bolts at 28 deg | **built - fire eyes**; never an elite |
| Charger | `LungerEnemy` | `scenes/LungerEnemy.tscn` - Speed 92, contact 2, 0.7 s wind-up, x4.2 | |
| Exploder | `ExploderEnemy` | `scenes/ExploderEnemy.tscn` - Speed 128, faces left, ignores decor, 0.85 s fuse | |
| Slammer | `SlammerEnemy` (ring) | `scenes/SlammerEnemy.tscn` - Speed 78, r 25.65, 0.75 s, radius 96 | |
| Summoner | `SummonerEnemy` | `scenes/SummonerEnemy.tscn` - Speed 70, 1.1 s wind-up, 2 per summon, cap 3 / 8 | never an elite |
| MiniBoss | `Enemy`, `IsMiniBoss` | `scenes/WardenEnemy.tscn` - **48 x 48 cell**, Speed 118, Health 220, r 28 | sent on a clock, one at a time |

**Copy the stand-in, change the look.** A new scene starts as a copy of its stand-in's `.tscn` and
changes only: the root node name, `EnemyType`, the `SpriteFrames` it points at, the collision shape (to
fit the art, within 30% of the stand-in's size), the telegraph and bolt colours (section 6), and for a
flyer `IgnoresDecorCollision = true` (the wisp's precedent). Every other root property stays. Every
chapter's mix was tuned on those numbers and a new body is a new look, not a retune -
`RegressionChecks.ValidateBestiary` compares them (speed, health, contact damage, knockback, every
behaviour timing, body size, sprite scale) and warns on any difference. A difference that is meant is
recorded on the slot (`Planned(..., statsChange: "why")`) and then stops warning.

**The grid.** 32 x 32 for every creature but the miniboss, which is 48 x 48. Feet on row `cell - 2`.
`AnimatedSprite2D.scale` is the stand-in's - **2.7** on every enemy today, not the contract's x2:
the roster was grown 35% after `art-direction.md` was written (`Enemy.EnemySpacingRadius`'s note),
and a new creature at x2 would be the one small body in its chapter. Fixing that is a whole-cast
decision (section 10), not a per-creature one.

**Animations.** `moving` 8 (loops), `attack` 6, `hurt` 2, `death` 6 - all derived from one drawn pose
by `anim_sets.full_set`, through `tools/art/bestiary_kit.py` (section 8). `death` is mandatory for
anything drawn to the contract; the validator warns on a missing one, a looping one, or a telegraphed
creature with no `attack`.

**Silhouette.** `art-direction.md` section 4's rule, applied per chapter: every creature in a chapter
owns one attachment that survives being filled solid black, and no two creatures *in the same
chapter* share one. Across chapters the same role may use different classes (the Forest's bulk is a
leaf heap, the Sands' a wrapped traveller); they never share a screen. **Nothing may echo its own
chapter's boss**, whose outline is its identity (`sprite_bosses.py`: the Gaoler a T, the Choir a bell,
Mother Rot a dome, the Archivist a slab with detached tablets, the Still Warden a wedge, the Long Coil
an S, the Deep Warden an H).

**Palette.** Existing material rows only (`art-direction.md` section 3), deep half, one emissive pair.
No player pigment row. No new rows without the developer (section 10 has the one candidate). Within a
chapter, separate creatures by material row as well as shape, the way the old roster put wool, steel,
linen and violet on four different jobs.

**Density.** 0.80 internal edges per pixel is the smell test (`bestiary_kit` prints it); the read wins.

**Names.** `EnemyType` is the scene's name (`CaveDrowned`), unique across the bestiary, and never
`Tank`, `TankEnemy`, `Fast` or `FastEnemy` - `Enemy.DropXp` pays those four as their own XP tiers.

## 3. The spawner, unchanged

Nothing about when or how often anything spawns moved. The six behaviour roles are still each rolled
on their own, ahead of the chapter's table, on the same clocks (`Node2DGame`):

| Role | First spawn (pace) | Share | Health | Elite? |
|---|---|---|---|---|
| Ranged | 105 s | 0.08 | x0.85 | yes |
| Turret | 210 s | 0.04 | x1.0 | never |
| Charger | 90 s | 0.10 | x0.9 | yes |
| Exploder | 165 s | 0.07 | x0.5 | yes |
| Slammer | 210 s | 0.08 | x1.6 | yes |
| Summoner | 285 s | 0.04 | x0.9 | never |
| MiniBoss | 5:15, then every 165 s | clock, one alive at a time | Health x (1 + 0.22 m) | always a chest carrier |

A chapter that has no creature for a role (the Sketchbook's four) skips that roll and draws no dice
for it, as before. The chapter tables, band for band from the old environment switch:

| Chapter | Table (share x health multiplier, in roll order) |
|---|---|
| Enchanted Forest | wolf .40 x0.85, bramble (Wall) .26 x1.5, wisp .14 x0.8, Bulk .12 x1.0, Fast .08 x0.75 |
| Cursed Dungeon | Heavy .20 x2.2, Wall .36 x1.4, Bulk .30 x1.0, Fast .14 x0.75 |
| Sunken Cave | Bulk .78, Fast .14, Wall .08; **from pace minute 5:** Fast .20, Wall .15, Heavy .10, Wall .13, Bulk .42 |
| Blighted Swamp | Wall .38, Bulk .26, Fast .22, Heavy .14 |
| Mystic Ruins | Heavy .26, Wall .32, Bulk .28, Fast .14 |
| Frozen Waste | Fast .36, Bulk .32, Wall .20, Heavy .12 |
| Scorched Sands | Fast .40, Bulk .32, Wall .18, Heavy .10 |
| The Emberdeep | Heavy .24, Bulk .38, Fast .24, Wall .14 |
| The Sketchbook | Smiler .28 x0.8, One-Eye .20 x1.0, Zombie .17 x1.5, Vampire .15 x0.85, Message .13 x1.1, Rockem .07 x2.4 |

(Bulk x1.0, Fast x0.75, Heavy x2.2, Wall x1.4 wherever not stated.) The Sunken Cave is the one chapter
with a late table: it never had a case of its own in the environment switch and fell to the default
branch, which changed its mix at five pace minutes. Kept exactly.

**What picks no scene, and so needed no plumbing:** elites (the elite treatment is applied to whatever
was rolled - "more, not different"), formations (each member is an ordinary spawn), the recycler (moves
a body, never swaps it), swarm folding (folds two bodies of the plain `Enemy` class into one, whatever
their art), and every event ring (`SpawnRing` takes the chapter's table).

---

## 4. The casts

Every row below is a planned creature: its scene path is fixed in its chapter's roster, and its
stand-in (the role's row in section 2) fills the slot until the scene exists. *Exists* marks what is
already drawn. Cells are 32 x 32 except the miniboss (48). Palette names are `art-direction.md`
section 3 rows; *bone* is the row the code calls `skin`.

### 4.1 Enchanted Forest (`stage_0`) - Elderbark, the Treant

*The wood itself, taken: its beasts, its spirits, and the woodsmen who worked it.* No skeletons:
the 20% the crypt's swarmer and runner held goes to a forest bulk and a forest runner. Bolt element:
**grass**. Rows: linen (fur and foliage - the grey-green a forest is under no light), stone shade and
deep (wood, as the bramble), lichen (moss).

| Role | Creature | Scene | What it is | Attachment that reads | Palette |
|---|---|---|---|---|---|
| Extra .40 | Dire Wolf | `ForestWolf` *(exists)* | the pack, the bulk of the wood | horizontal quadruped | linen |
| Wall | Bramble | `ForestBramble` *(exists)* | a thorn tangle that moves | low lace, full of holes | stone, linen |
| Extra .14 | Wisp | `ForestWisp` *(exists)* | a woodland fairy, taken; the table's back line | wings wider than the body, no ground contact | linen |
| Bulk | Leafwight | `ForestLeafwight` | a forager buried in the leaf-fall, walking: a heap of dead leaves wearing a person's outline | one curled oak leaf hooked forward over the void face; ragged leaf hem | linen deep, stone stalks |
| Fast | Hollow Beater | `ForestBeater` | one of the beaters who drove game for the hunt, still running ahead of something | gangly, pitched forward, a long streamer of hanging moss behind | linen body, lichen streamer (a different value from the body) |
| Heavy | *none* | - | the forest table has never carried a heavy band (section 10) | | |
| Ranged | Hollowed Dryad | `ForestDryad` | the tree's own spirit: bark-thin under a crown of dead twigs | a seed-pod floating detached at head height | stone, linen; pod in grass |
| Turret | Wicker Effigy | `ForestEffigy` | a warning effigy he bound from the woodsmen's withies and staked at the path. Built: it burns | a cross of bound sticks, arms out wider than anything else here; a wicker-cage head | stone shade; **fire** eyes |
| Charger | Gloamstag | `ForestGloamstag` | a stag gone gaunt, head lowered to charge | an antler rack wider than its body, thrust forward - never mistaken for the wolf's bar | linen deep; bone antlers |
| Exploder | Burstpod | `ForestBurstpod` | a seed-husk swollen past splitting, on spindly root legs | the only bulging shape in the wood, seams split | stone, linen. **Not a mushroom**: Toxic Spore Burst is mushrooms |
| Slammer | Hollow Bear | `ForestHollowBear` | a bear reared on its hind legs; the slam is it coming down | forepaws the highest thing in the cell, a broad hump | linen deep |
| Summoner | The Huntsman | `ForestHuntsman` | the hunt's master, raising a curled horn to call the beaters | the curled horn held up at hand height | linen hood, red (old livery, under 15%), bone horn. Minion: **Hollow Beater** |
| MiniBoss | The Gamekeeper | `ForestGamekeeper` (48) | his jailer in the wood: tall and gaunt, a man-trap slung on his back | a gibbet cage hung from a pole over one shoulder, rising past the head | linen coat, steel trap and cage, gold buckle |

### 4.2 Cursed Dungeon (`stage_1`) - the Gaoler

*The crypt's evil undead: the buried in grave linen and the buried who were soldiers, a taken caster,
a skull he built, and his jailer.* **Everything exists**: this is the Bonelight roster of
`tools/art/roster.py` and `sprite_warden.py`, which was rolled in every chapter until now and has come
home. It is every other chapter's stand-in. Bolt element: **arcane** (the violet it already throws).

| Role | Creature | Scene | What it is |
|---|---|---|---|
| Bulk | The Shrouded | `enemy` | the buried in grave linen; narrow cowl, no visible limbs |
| Fast | Grave Runner | `FastEnemy` | pitched hard forward, cloak streaming behind |
| Heavy | Shieldbearer | `TankEnemy` | hunched behind a plane of iron |
| Wall | Barrow Knight | `SlowEnemy` | the buried who were soldiers: horned helm, pauldrons |
| Ranged | Hexer | `HexerEnemy` | wool, a split mitre, a detached orb, ribs behind the robe |
| Turret | Skull Sentry | `SkullSentry` | built: a floating skull in an iron collar, fire eyes |
| Charger | Crypt Lunger | `LungerEnemy` | coiled low, legs folded under |
| Exploder | Grave Bloat | `ExploderEnemy` | distended, leaking at its seams |
| Slammer | Gravebreaker | `SlammerEnemy` | steel, arms up mid wind-up |
| Summoner | Bone-Caller | `SummonerEnemy` | violet, a horned crown, a sigil ring; calls Grave Runners |
| MiniBoss | The Warden | `WardenEnemy` (48) | his jailer: a cell door for a breastplate, chains; **also the Gaoler's key carrier** |

The "skeleton1 / skeleton2 / vampire" of older docs were dungeon-pack sheets: these Bonelight
redraws replaced them (`art-direction.md`, migration phase 2), and the packs themselves were then
removed from the repo and its history (`.ai/asset-licensing.md`).

### 4.3 Sunken Cave (`stage_2`) - the Hollow Choir

*"His shadow followed the water down into the dark." The miners and pearl divers who worked these
tunnels, and the blind pale things that lived in them.* Bolt element: **water** (a cold element on a
cold floor - section 6). Rows: stone, lichen, linen, bone (cave life is pale). **No bells**: the Choir
is a bell.

| Role | Creature | Scene | What it is | Attachment that reads | Palette |
|---|---|---|---|---|---|
| Bulk | The Drowned | `CaveDrowned` | whoever the water took when it rose: hunched, sodden | lank hair hanging in a curtain past the face, widening to the hem | linen deep, lichen; the eyes through the hair |
| Fast | Gloomwing | `CaveGloomwing` | a cave bat the size of a dog | flyer: wings wider than the body, detached shadow | stone, bone membrane |
| Heavy | Shieldclaw | `CaveShieldclaw` | a blind cave crab grown huge | hunched behind one enormous flat claw - the shield plane | stone deep shell, bone claw edge |
| Wall | Hollow Miner | `CaveHollowMiner` | a miner, broad and stooped | a pick laid across one shoulder, breaking the line; a lamp on the helm long dark (no glow - the player carries the only warm light) | steel helm, linen, stone |
| Ranged | Pearl Diver | `CavePearlDiver` | a diver, tall and dripping, who throws the pearl she drowned for | a pearl floating detached at her shoulder | linen, lichen; pearl in water |
| Turret | Echo Idol | `CaveEchoIdol` | a face he carved into a stalagmite, mouth open; the cave's echo comes out as bolts. Built | the stalagmite spike, the one tall point in the chapter | stone; **fire** eyes |
| Charger | Cave Leaper | `CaveLeaper` | a camel cricket the size of a hound | hind legs folded higher than its back; antennae swept back | bone deep, stone joints |
| Exploder | Bloatfish | `CaveBloatfish` | a spined fish swollen round with water, hauling itself on its fins | a ball of spines | lichen, bone |
| Slammer | Rockbreaker | `CaveRockbreaker` | a quarryman raising a maul overhead in both hands | the maul head highest; upright, where the Miner stoops | steel maul, linen, red sash |
| Summoner | Roost Hermit | `CaveRoostHermit` | a hermit who lived among the bats and calls them down | a crooked staff hung with roosting bats | stone, linen. Minion: **Gloomwing** |
| MiniBoss | The Anchor Warden | `CaveAnchorWarden` (48) | his jailer below: a hulk in a riveted diving helm | an anchor dragged on a chain off one side | steel (rivets), stone; the mark behind the grille |

### 4.4 Blighted Swamp (`stage_3`) - Mother Rot

*"The lanterns still burn. Nothing living lights them." The peat cutters and ferry folk of the fen, its
toads and striders, and the swollen things Mother Rot sheds.* Bolt element: **poison**. Rows: linen,
lichen, stone, and flesh and bone from the deep half - Mother Rot's own rows, so her brood matches her.

| Role | Creature | Scene | What it is | Attachment that reads | Palette |
|---|---|---|---|---|---|
| Wall | Broodling | `SwampBroodling` | **what Mother Rot sheds**: a swollen sac the size of a child that has grown two stubby legs. Also her `SpawnScenePath` | upright on its legs - her dome is wider than tall, so the brood must not be | flesh deep, bone deep, a split seam |
| Bulk | The Mired | `SwampMired` | a peat cutter sunk to the waist in the bog and still walking | reeds grown up out of its back, above the head line | linen deep, stone mud, lichen reeds |
| Fast | Mire Strider | `SwampStrider` | a water-strider grown huge | a small body slung between very long splayed legs - mostly holes | stone, linen, bone joints |
| Heavy | Mudback | `SwampMudback` | a snapping turtle the size of a cart | the shell canted up like a slab as it shoulders forward | stone shell, linen |
| Ranged | The Lamplighter | `SwampLamplighter` | the one who keeps the lanterns burning: tall, hooded | a cold flame hanging loose from a hooked pole, detached. **No cage**: the bog lanterns of the side event must never look like an enemy | violet, linen; flame in poison |
| Turret | Frog Idol | `SwampFrogIdol` | a frog's head he carved in stone and sank in the bog. Built | half-sunk, a wide mouth | stone; **fire** eyes |
| Charger | Bog Hound | `SwampBogHound` | a gaunt hound, belly to the mud, about to spring | coiled low, ears flat, a long tail | linen deep, stone mud |
| Exploder | Blister Toad | `SwampBlisterToad` | a toad swollen with blisters that burst | the blistered back is the bulge | flesh deep, lichen; nothing bleeds |
| Slammer | Peat Cutter | `SwampPeatCutter` | a cutter raising a long peat spade overhead | the spade blade highest | linen, steel blade |
| Summoner | Reed Piper | `SwampReedPiper` | a piper on bone reed-pipes, calling the striders out of the water | the pipes held at hand height | violet, bone. Minion: **Mire Strider** |
| MiniBoss | The Ferryman | `SwampFerryman` (48) | his jailer on the fen: tall, hooded, a chain at his belt | a long punt-pole carried diagonally across him | linen, violet, steel chain |

The lantern event's tender and wisps (`BogLanternsEvent`) are rolled from this table, so they become
swamp creatures with it.

### 4.5 Mystic Ruins (`stage_4`) - the Archivist

*"The old wards were broken from the inside." The readers, statues and guardians of a school of wards,
turned on it - and the one thing he built to break them.* Bolt element: **arcane**, the light end
(the Dungeon's casters throw the dark end). Rows: stone (laid courses), violet and wool (robes), gold
(old fittings - pigment, not light), linen (paper). **No floating slabs or tablets**: the Archivist's
detached tablets are its whole silhouette.

| Role | Creature | Scene | What it is | Attachment that reads | Palette |
|---|---|---|---|---|---|
| Bulk | The Unread | `RuinsUnread` | a student of the wards, buried in paper | torn pages hanging off it for a hem | violet hood, linen pages |
| Fast | Loose Folio | `RuinsFolio` | a book flying on its own open covers | flyer: the covers beat as wings (a V, never a hanging slab) | stone covers, linen pages, gold clasp |
| Heavy | Warded Statue | `RuinsWardedStatue` | a guardian statue holding its ward-slab before it | the slab held to one side - the shield plane | stone courses, gold inlay |
| Wall | Gargoyle | `RuinsGargoyle` | a gargoyle down off its gutter, crouched broad | wings folded high over the shoulders, horns | stone |
| Ranged | Hollow Magister | `RuinsMagister` | a master of the school, tall and narrow | a rune floating detached beside the head | wool deep, gold trim; rune in arcane |
| Turret | Breach Obelisk | `RuinsObelisk` | the thing he built inside the wards to break them. Built | a short tapering obelisk with one burning slot | stone deep; **fire** eye |
| Charger | Ward-Cat | `RuinsWardCat` | a temple cat statue gone lean, crouched to spring | coiled low, tail up | stone |
| Exploder | Cracked Urn | `RuinsCrackedUrn` | a sealed reliquary urn on stubby legs, cracked all over | the urn's swell, cracks across it | stone, gold bands |
| Slammer | Lintel-Bearer | `RuinsLintelBearer` | a caryatid that held up a doorway, lifting the broken lintel to bring it down | a flat stone bar above the head | stone |
| Summoner | The Lector | `RuinsLector` | a reader holding open the book the folios fly out of | the open book at hand height | violet, linen, gold. Minion: **Loose Folio** |
| MiniBoss | The Censor | `RuinsCensor` (48) | his jailer among the books | a padlocked book chained to its chest; a long quill raked up like a spear | red cloth, steel chain, gold lock |

### 4.6 Frozen Waste (`stage_5`) - the Still Warden

*"A wizard lies under the ice, and the cold stands guard." The thanes, sled hounds and drum callers of
the north, frozen and taken.* Bolt element: **ice**. Rows: steel, wool (blue-grey furs), bone
(whalebone), stone, linen. The **Rime Guard** (`RimeGuard.tscn`, the Still Warden's halberdier) is
already this chapter's: it fills no slot because only the boss calls it up.

**The ice rule for this chapter.** The player's Freeze status tints a body pale blue and marks it
(`Enemy.FrozenLook`, `StatusEffectLayer`). A cast drawn as pale blue ice shells would look frozen
before anything froze it, and a frozen one would look like nothing changed. So ice here is the Still
Warden's: dark stone planes with ice-ramp *edges*, and always an attachment (a crust ridge, spikes,
fists), never a whole-body shell.

| Role | Creature | Scene | What it is | Attachment that reads | Palette |
|---|---|---|---|---|---|
| Bulk | Rimebound | `FrostRimebound` | a traveller the cold caught, hunched | a jagged ice crust ridged up over the shoulders | wool deep; stone and ice-edge crust |
| Fast | Sled Hound | `FrostSledHound` | a sled dog run thin | the snapped trace of its harness streaming behind | linen, bone, steel buckle |
| Heavy | Floe-Bearer | `FrostFloeBearer` | a hunched figure carrying a slab of sea ice before it | the floe, a flat plane to one side | wool; stone floe, ice edges |
| Wall | Frost Thane | `FrostThane` | a northern lord, frost in his beard | the textbook bruiser: horned helm, a fur mantle breaking the shoulder line | steel, wool, bone beard |
| Ranged | Rime Seer | `FrostRimeSeer` | a seer wrapped in furs | an ice shard hanging detached at her shoulder | wool, linen; shard in ice |
| Turret | Whalebone Totem | `FrostTotem` | a totem he carved from a whale's jaw and set in the ice. Built | a tall bone post, the jaw tips curving | bone; **fire** eyes. Not a cairn (the Thaw's wayfinder) or a brazier (stage secret I3) |
| Charger | Ice Ram | `FrostIceRam` | a mountain ram, head down | curled horns iced at the edges - not the hound's lean line | linen fleece, bone horns |
| Exploder | Frostburst | `FrostFrostburst` | a body frozen from the inside, swollen and splitting | ice spikes pushing out through its seams | wool; ice-edge facets |
| Slammer | Hailbringer | `FrostHailbringer` | a giant of the north raising two ice-clubbed fists | the fists highest | wool, linen; ice edges |
| Summoner | Drum-Caller | `FrostDrumCaller` | a drummer calling the hounds | a round frame drum held at hand height | wool, bone. Minion: **Sled Hound** |
| MiniBoss | The Frostkeeper | `FrostFrostkeeper` (48) | his jailer in the north | a ring of keys frozen into a block at its belt; chains hanging rigid with icicles. Nothing like the Rime Guard's halberd and shield | steel, wool |

### 4.7 Scorched Sands (`stage_6`) - the Long Coil

*"He hid the water. The dead still walk the trade road." The porters, guards and travellers of a
caravan that never reached water, and what feeds on them.* Bolt element: **wind** (sand-laden; teal
reads on sand where earth would not) for the taken, fire for the built. Rows: linen (wrappings), red
cloth, gold, bone (sun-bleached), stone. **The heavy is also the caravan master**: `CaravanMasterEvent`
asks for this chapter's Heavy. No serpents: the Long Coil is an S.

| Role | Creature | Scene | What it is | Attachment that reads | Palette |
|---|---|---|---|---|---|
| Bulk | The Thirsting | `SandsThirsting` | a traveller from the caravan, wrapped head to foot against the sun | an empty waterskin hanging slack at one hip | linen, red deep skin |
| Fast | Bone Vulture | `SandsVulture` | a vulture with bare bone through its wings | flyer: broad ragged wings, detached shadow | linen, stone, bone |
| Heavy | Laden Porter | `SandsPorter` | a porter bent double under a bale-frame bigger than he is; **the caravan master** | the bale-frame, carried to one side - the shield plane | linen, stone frame, red bale |
| Wall | Caravan Guard | `SandsCaravanGuard` | a caravan's hired spear, broad | a crested helm and a flared shoulder mantle | steel, red |
| Ranged | Dune Oracle | `SandsOracle` | a veiled seer of the road | a glass bead floating detached beside her | linen veil, gold; bead in wind |
| Turret | Bone Ballista | `SandsBallista` | a bolt-thrower he built from the caravan's bones. Built | a crossbow frame on a tripod, arms out flat | bone, stone; **fire** sight; fire bolts |
| Charger | Scorpion | `SandsScorpion` | a scorpion the size of a dog | the tail arched over its back - segmented and small, never a serpent's curve | stone deep carapace, bone |
| Exploder | Swollen Cask | `SandsSwollenCask` | a caravan cask swollen and splitting at its hoops, walking | the barrel's swell, the hoops | stone wood, steel hoops |
| Slammer | Flailbearer | `SandsFlailbearer` | the caravan's guard captain whirling a chain flail overhead | the flail head highest | steel, red |
| Summoner | Carrion Caller | `SandsCarrionCaller` | a hunched figure holding up a perch-arm to call the vultures down | the raised perch-glove | linen, red. Minion: **Bone Vulture** |
| MiniBoss | The Toll-Keeper | `SandsTollKeeper` (48) | his jailer on the trade road | a toll-chain across the chest and a strongbox hung low at one side | steel, gold lock, red |

### 4.8 The Emberdeep (`stage_7`) - the Warden of the Deep

*"His seat, and the end of the road." The one chapter whose cast was **made**, not taken - so every
creature burns: fire eyes, where every other chapter's taken creatures stare cold.* Keep each to its
two-pixel fire mark and at most a crack or two of fire; **only the Deep Warden blazes**, and the
restraint is what lets it. Bolt element: **fire**. Rows: steel and stone from the deep end, red deep.
Nothing bipedal-heavy with horns: the Deep Warden is an H.

| Role | Creature | Scene | What it is | Attachment that reads | Palette |
|---|---|---|---|---|---|
| Bulk | Slag Thrall | `EmberSlagThrall` | a thrall he poured from slag, squat and hunched | a crust of cooled plates for a cowl | stone deep, steel |
| Fast | Chainhound | `EmberChainhound` | an iron hound | a broken chain dragging behind it | steel deep |
| Heavy | Iron Bulwark | `EmberBulwark` | a squat engine | a riveted plate held before it | steel, rivets |
| Wall | Kiln Brute | `EmberKilnBrute` | a kiln that walks, broad | flue pipes rising off its shoulders; a shut furnace door for a belly | stone, steel |
| Ranged | The Kindler | `EmberKindler` | a thin iron figure that throws coals | a coal floating detached at its shoulder | steel; coal in fire |
| Turret | Flue Mask | `EmberFlueMask` | a mask on a chimney stack, venting fire | the tall stack | steel, stone |
| Charger | Ironjaw | `EmberIronjaw` | a forged beast that is mostly jaw | coiled low, the oversized jaw | steel |
| Exploder | Burst Bellows | `EmberBellows` | a forge bellows on legs, swelling at every seam | the pleats | red deep, steel nozzle |
| Slammer | Trip-Hammer | `EmberTripHammer` | a forge hammer that walks | one great hammer-arm raised - asymmetric | steel |
| Summoner | Kennelmaster | `EmberKennelmaster` | the keeper of his hounds | a ring of chains held up at hand height | steel, red. Minion: **Chainhound** |
| MiniBoss | The Chainwright | `EmberChainwright` (48) | his jailer in his own seat: the forge every shackle in the land came from, walking | one arm a chain-hoist, chains hanging | steel deep |

### 4.9 The Sketchbook (`stage_8`) - Wizard Dude

**Unchanged, and not part of Phase 2.** Moved from `Node2DGame`'s switch into
`StageBestiary.Sketchbook.cs` band for band and slot for slot: Smiler (Bulk), Vampire (Fast), Rockem
(Heavy and MiniBoss), Zombie (Wall), One-Eye and the Message (Extra), Blobby (Charger), Pushy Dude
(Slammer, a cone). Ranged, Turret, Exploder and Summoner are absent - the kids drew no counterpart.
The ghost is an attendant on its own clock; the dog is a companion. The roster is marked
`ArtContractExempt`, so the validator never asks the kids' sheets for a death or an attack strip.

---

## 5. What exists and what must be drawn

| Chapter | Exists | To draw (32 cell) | To draw (48 cell) |
|---|---|---|---|
| Enchanted Forest | wolf, bramble, wisp | 8 | 1 |
| Cursed Dungeon | all 11 | 0 | 0 |
| Sunken Cave | - | 10 | 1 |
| Blighted Swamp | - | 10 | 1 |
| Mystic Ruins | - | 10 | 1 |
| Frozen Waste | Rime Guard (attendant) | 10 | 1 |
| Scorched Sands | - | 10 | 1 |
| The Emberdeep | - | 10 | 1 |
| The Sketchbook | all (the kids') | 0 | 0 |
| **Total** | | **68** | **7** |

75 poses, each derived to the 22-frame set (1650 frames). **No bought art can be used**: the packs
`art-inventory.md` describes were removed for licensing (`.ai/asset-licensing.md`), so that doc is
history. What is reusable is code:

- `tools/art/roster.py` - the `G` grid and the figure helpers (`body`, `void`, `eyes`, `tatters`,
  `plate`, `contact`, `beard`, `density`), and the palette with every row named above.
- `tools/art/foes_forest.py` - `_limb`, and the lessons on quadrupeds (masses first, jointed legs,
  paired close: "a table is a rectangle and four lines").
- `tools/art/sprite_bosses.py` - `swell` (hand-written width profiles, not tapers), `facets` (ice),
  `courses` (masonry), `ridges` (plate), `scales`, `veins`, `rivets`, `spike`, `limb`, `mark`.
- `tools/art/anim_sets.py` - the derived set, through `bestiary_kit`.

---

## 6. Telegraph and projectile colours

**Where they live: in each creature's own `.tscn`, as exports.** No code holds a per-chapter colour.

| Class | Exports |
|---|---|
| `RangedEnemy` | `BoltBodyColor`, `BoltCoreColor` (the bolt, and the muzzle tell that grows before it fires); `ProjectileScenePath` (`EnemyProjectile.tscn` draws itself from those two, over a fixed dark halo that keeps it from reading as a player spell) |
| `LungerEnemy` | `TellColor` (the charge streak) |
| `SlammerEnemy` | `SlamWarningColor` (the ring or cone); `SlamShape`, `SlamHalfAngleDegrees` are stats - keep them |
| `ExploderEnemy` | `BlastWarningColor` (the fuse ring) |
| `SummonerEnemy` | `SummonColor` (the summon ring and spots); `MinionScenePath` - the chapter's Fast |

Fixed and shared, not per creature: the slam's landing flash (`GroundSlamAttack`), the exploder's lit
fuse tint (`ExploderEnemy`), the elite ring.

**Bolts follow the chapter.** A caster's detached orb is its weapon, drawn in its chapter's element
ramp, and its bolt is the same colour - the orb is the thing it throws. Core white-hot, identity in
the mid, as the element rule says. Turrets are built and throw fire everywhere (the Skull Sentry's
`(0.95, 0.45, 0.12)` / `(1, 0.88, 0.55)`).

| Chapter | Element | `BoltBodyColor` (mid) | `BoltCoreColor` (core) |
|---|---|---|---|
| Enchanted Forest | grass | `#9BD455` (0.608, 0.831, 0.333) | `#F4FFDC` (0.957, 1, 0.863) |
| Cursed Dungeon | arcane, dark | as shipped (0.55, 0.24, 0.86) | (0.85, 0.62, 1.0) |
| Sunken Cave | water | `#6FB8F0` (0.435, 0.722, 0.941) | `#F0FAFF` (0.941, 0.980, 1) |
| Blighted Swamp | poison | `#B4E04A` (0.706, 0.878, 0.290) | `#F8FFD8` (0.973, 1, 0.847) |
| Mystic Ruins | arcane, light | `#C08CFF` (0.753, 0.549, 1) | `#FFF0FF` (1, 0.941, 1) |
| Frozen Waste | ice | `#7FDCF8` (0.498, 0.863, 0.973) | `#F4FEFF` (0.957, 0.996, 1) |
| Scorched Sands | wind | `#B4EEE0` (0.706, 0.933, 0.878) | `#FFFFFF` |
| The Emberdeep | fire | `#E87A1A` (0.910, 0.478, 0.102) | `#FFF4D0` (1, 0.957, 0.816) |

**Check every one against its floor with eyes.** The cold elements on the Cave's and the Frozen
Waste's floors are the one place Bonelight can fail (`art-direction.md`, "Bonelight-specific
caution"): if a bolt does not read, push its body to the ramp's hot stop and keep the core white. The
Forest's grass ramp on grass is the other risk; earth (`#C99A5E`) is the fallback.

**Ground warnings: a decision (section 10).** The charge streak (orange), slam ring (amber), fuse ring
(red) and summon ring (mint) are today one language across the campaign: a player learns once that
orange means a charge. The Sketchbook (Blobby violet, Pushy cyan) and the Rime Guard (pale cyan)
already break it per chapter. **Recommended: keep the four warning hues per verb everywhere, and let
only bolts and orbs take the chapter's element** - a warning is a road sign, not a costume.

---

## 7. The plumbing (Phase 1, as built)

**`scripts/StageBestiary.cs`** - the shared types and the lookup, and nothing about any chapter:
`BestiaryRole`, `BestiaryCreature` (id, name, home chapter, scene path, `EnemyType`),
`BestiarySlot` (`Own`, `Planned` with a stand-in, `Absent` with a reason), `BestiaryTableEntry`,
`ChapterBestiary`, `StageBestiary.All` (the one list of the nine chapters, written once) and
`LoadedBestiary` (a chapter's scenes loaded once for the run).

**`scripts/StageBestiary.<Chapter>.cs`** - one file per chapter: its creatures and its roster. Each is
a nested static class of its own, built on first use, because C# does not order static initialisers
across the files of a partial class and every chapter borrows the Cursed Dungeon's creatures.

**Planned creatures resolve by themselves.** A `Planned` slot spawns its creature the moment a scene
exists at the creature's path, and its stand-in until then. Phase 2 never edits a roster to switch a
creature on.

**What reads it:**

- `Node2DGame.SelectEnemyForCurrentStage` - the six role rolls (a missing role skips its roll and its
  dice) and the chapter's table. Every enemy the trickle, formations, elites and event rings spawn.
- `Node2DGame.TickWardenMiniBossSpawn` - the chapter's MiniBoss scene and its declared `EnemyType`,
  which the never-two-at-once check looks for in the registry.
- `Node2DGame.SpawnEventEnemy(BestiaryRole, ...)` - the role overload. `GaolersKeyEvent` asks for the
  MiniBoss (the Warden, in the Cursed Dungeon), `CaravanMasterEvent` the Heavy (the Laden Porter, in
  the Sands, once drawn). `SpawnEventEnemy(null, ...)` still takes the chapter's table.
- `Node2DGame.LoadBestiaryForStage` - once, in `_Ready`.

**Left where they are, on purpose:** `MotherRotBoss.tscn`'s `SpawnScenePath` (a boss scene's own
export; the Swamp's Phase 2 points it at the Broodling), `StillWardenBoss`'s guard (already the
Frozen Waste's), `SummonerEnemy.tscn`'s minion (it is a stand-in; each chapter's own summoner names its
own minion), the ghost and the dog (Sketchbook set-pieces, not roles), and the plain `enemy.tscn` that
`_UiShot` and the swarm-fold check use as a test body.

**`RegressionChecks.ValidateBestiary`** - at startup, beside the other validators. It checks that every
playable chapter has a roster; every role is filled or absent with a reason (Bulk, the MiniBoss and an
Extra never absent); every scene loads as an `Enemy` of the class its role needs (a Turret is a
`Rooted` `RangedEnemy`, a MiniBoss has `IsMiniBoss`, no table body is a miniboss outside the
Sketchbook); every sheet has a looping `moving` with an imported texture, a non-looping `death`, and an
`attack` if it telegraphs; declared `EnemyType`s match their scenes and are unique across chapters;
each table's shares add to 1; a drawn creature matches its stand-in's stat block; a chapter's own
summoner calls up the chapter's own minion; and its boss's adds are the chapter's. It runs the class
timing checks (`ValidateRangedEnemy`, `ValidateLunger`, ...) on any roster scene the hand-kept lists
do not name, so a new caster is held to them without anyone editing `RegressionChecks.cs`.

Its last line per chapter is the **Phase 2 to-do list**:

```
Bestiary to-do (Phase 2, .ai/bestiary.md): Sunken Cave still spawns 11 creatures that are not its
own - Bulk The Drowned (scenes/CaveDrowned.tscn); Fast Gloomwing (scenes/CaveGloomwing.tscn); ...
```

At the end of Phase 1 there are seven, one per chapter still drawing: the Forest (9), the Cave (11),
the Swamp (12 - its eleven and Mother Rot's brood), the Ruins, the Frozen Waste, the Sands and the
Emberdeep (11 each). The Cursed Dungeon and the Sketchbook own their whole casts. **Phase 2 is done
when the validator ends `0 errors and 1 warnings`** (the old `test_wizard` one).

**`tools/art/bestiary_kit.py`** - the art half: `build_cast` writes a creature's four strips and its
`SpriteFrames` from one pose, `report` prints size and density and writes a contact sheet, and
`build_all` finds every `tools/art/cast_<chapter>.py` for `build.py`, which calls it from one block
written in Phase 1. Stems and sheet names are checked against the chapter prefixes so one chapter can
never overwrite another's art. The *pose* is anchor-checked, not the derived frames: `anim_sets` lifts
the bob frames and collapses the death, and every derived enemy set measured - the roster's ten, the
forest's three, the Warden and the Rime Guard - "fails" a per-frame anchor check for that reason,
which is why `build.py` has never run one on an enemy.

---

## 8. Phase 2 - how to draw a chapter

One agent per group (section 9), each in its own worktree from `content/bestiary`
(`tools/new-worktree.sh`). For each creature in the chapter's section 4 table:

1. **Draw the pose** in `tools/art/cast_<chapter>.py` (the module contract is the docstring of
   `bestiary_kit.py`). Stem `<prefix>-<name>`, sheet `<Prefix><Name>Frames`, e.g. `cave-hollow-miner`,
   `CaveHollowMinerFrames`. Run **only your module**: `python tools/art/cast_<chapter>.py`. It writes
   `assets/bonelight/enemies/<stem>-{moving,attack,hurt,death}.png` and
   `scenes/resources/<Prefix><Name>Frames.tres`, prints density, and writes
   `tools/art/_cast/<chapter>.png` to look at. **Do not run `tools/art/build.py`** - it regenerates
   every sprite in the game, and three agents doing it at once is three sets of churn to merge.
2. **Import** the new PNGs: `"$GODOT_BIN" --headless --path . --import` (seconds with a warm cache).
   Commit the `.png.import` sidecars with the art.
3. **Make the scene** at the path the roster names (`scenes/<Prefix><Name>.tscn`) by copying the
   stand-in's `.tscn`, then: rename the root node, set `EnemyType` to the scene name, point the
   `SpriteFrames` ext_resource at your `.tres`, fit the collision shape to the art (within 30%), set
   the bolt and orb colours (section 6), and for a flyer `IgnoresDecorCollision = true`. **Delete the
   `uid="..."` from the copied `[gd_scene ...]` header** - two scenes with one uid break each other.
   Change no other property.
4. **Summoner:** `MinionScenePath` is your chapter's Fast (`res://scenes/<Prefix><Fast>.tscn`).
   **Swamp only:** set `SpawnScenePath = "res://scenes/SwampBroodling.tscn"` on the root of
   `scenes/MotherRotBoss.tscn`.
5. **Verify:** `dotnet build WizardSurvivors.sln`; then
   `"$GODOT_BIN" --headless --path . scenes/MainMenu.tscn --quit-after 300` - your chapter's to-do line
   is shorter by the creatures you drew, and there is no other new warning; then a balance-bot run on
   the chapter (`scenes/_BalanceHarness.tscn -- --minutes=6 --character=1 --all-unlocked --immortal
   --stage=N`), whose `spawned` line now names your creatures. One Godot at a time; never kill one by
   name. **Then look at them**: a run of the chapter, because no validator sees a silhouette.
6. **Update this doc's status line** for your chapter (section 9) and nothing else in it.

If a creature seems to need a behaviour no class has, or a stat change, stop and ask rather than
editing a behaviour class: the brief is new looks on tuned numbers.

## 9. The Phase 2 split

**Every group creates only files, plus at most the two single lines named below.** No file is shared
between groups.

**Never edit (any group):** `scripts/StageBestiary.cs`, any other chapter's
`scripts/StageBestiary.<Chapter>.cs`, `scripts/Node2DGame*.cs`, `scripts/RegressionChecks.cs`,
`scripts/ContentValidator.cs`, every behaviour class (`scripts/*Enemy.cs`, `scripts/*Boss.cs`), the
stand-in scenes (`enemy`, `FastEnemy`, `TankEnemy`, `SlowEnemy`, `HexerEnemy`, `SkullSentry`,
`LungerEnemy`, `ExploderEnemy`, `SlammerEnemy`, `SummonerEnemy`, `WardenEnemy` - copy them, never
change them) and any existing `scenes/resources/*Frames.tres`; `tools/art/build.py`,
`bestiary_kit.py`, `roster.py`, `foes_forest.py`, `anim_sets.py`, `pixel.py`, `spriteframes.py`,
`bonelight.py`; `.ai/art-direction.md` (no new material rows), `.ai/art-replacement-manifest.md`,
`README.md`, `CLAUDE.md`. The integrator updates the manifest and the README once, after the merge.

**Your own chapter's roster file** is yours, and normally needs no edit (planned creatures switch on by
themselves). Edit it only to record a deliberate `statsChange` or a design change agreed with the
developer.

Per chapter, the files a group creates (`<P>` the scene prefix, `<p>` the stem prefix):

- `tools/art/cast_<chapter>.py`
- for each creature in its section 4 table: `scenes/<P><Name>.tscn`,
  `scenes/resources/<P><Name>Frames.tres`, and
  `assets/bonelight/enemies/<p>-<name>-{moving,attack,hurt,death}.png` with their `.png.import`

| Chapter | Module | Scenes to create (`scenes/*.tscn`) | The one line elsewhere |
|---|---|---|---|
| Enchanted Forest | `cast_forest.py` | `ForestLeafwight`, `ForestBeater`, `ForestDryad`, `ForestEffigy`, `ForestGloamstag`, `ForestBurstpod`, `ForestHollowBear`, `ForestHuntsman`, `ForestGamekeeper` | - |
| Cursed Dungeon | *(none)* | *(none - confirm the cast in a run)* | - |
| Sunken Cave | `cast_cave.py` | `CaveDrowned`, `CaveGloomwing`, `CaveShieldclaw`, `CaveHollowMiner`, `CavePearlDiver`, `CaveEchoIdol`, `CaveLeaper`, `CaveBloatfish`, `CaveRockbreaker`, `CaveRoostHermit`, `CaveAnchorWarden` | - |
| Blighted Swamp | `cast_swamp.py` | `SwampMired`, `SwampStrider`, `SwampMudback`, `SwampBroodling`, `SwampLamplighter`, `SwampFrogIdol`, `SwampBogHound`, `SwampBlisterToad`, `SwampPeatCutter`, `SwampReedPiper`, `SwampFerryman` | `scenes/MotherRotBoss.tscn`: `SpawnScenePath` |
| Mystic Ruins | `cast_ruins.py` | `RuinsUnread`, `RuinsFolio`, `RuinsWardedStatue`, `RuinsGargoyle`, `RuinsMagister`, `RuinsObelisk`, `RuinsWardCat`, `RuinsCrackedUrn`, `RuinsLintelBearer`, `RuinsLector`, `RuinsCensor` | - |
| Frozen Waste | `cast_frost.py` | `FrostRimebound`, `FrostSledHound`, `FrostFloeBearer`, `FrostThane`, `FrostRimeSeer`, `FrostTotem`, `FrostIceRam`, `FrostFrostburst`, `FrostHailbringer`, `FrostDrumCaller`, `FrostFrostkeeper` | - |
| Scorched Sands | `cast_sands.py` | `SandsThirsting`, `SandsVulture`, `SandsPorter`, `SandsCaravanGuard`, `SandsOracle`, `SandsBallista`, `SandsScorpion`, `SandsSwollenCask`, `SandsFlailbearer`, `SandsCarrionCaller`, `SandsTollKeeper` | - |
| The Emberdeep | `cast_ember.py` | `EmberSlagThrall`, `EmberChainhound`, `EmberBulwark`, `EmberKilnBrute`, `EmberKindler`, `EmberFlueMask`, `EmberIronjaw`, `EmberBellows`, `EmberTripHammer`, `EmberKennelmaster`, `EmberChainwright` | - |

Stems are the scene name in lower-case hyphens after the prefix: `ForestHollowBear` is
`forest-hollow-bear`, `CaveRoostHermit` is `cave-roost-hermit`, `FrostFrostkeeper` is
`frost-frostkeeper`. The miniboss of each chapter is the last scene in its row, on the 48 cell.

**The three groups.** Seven chapters of about eleven creatures cannot split evenly three ways without
cutting a chapter in half (which would put two agents in one generator module); the closest is one
three-chapter group with the Forest, the smallest:

| Group | Chapters | Creatures | Why together |
|---|---|---|---|
| **A - wood and water** | Enchanted Forest, Sunken Cave, Blighted Swamp (+ the Cursed Dungeon's run check) | 9 + 11 + 11 = **31** | quadrupeds, flyers and the wet; the Forest's three existing creatures are the reference for all three |
| **B - stone and cold** | Mystic Ruins, Frozen Waste | 11 + 11 = **22** | masonry and ice, `sprite_bosses`' `courses` and `facets`; both rest on stone planes |
| **C - heat** | Scorched Sands, The Emberdeep | 11 + 11 = **22** | the warm chapters, and the two that carry fire (the Sands' ballista, everything in the Emberdeep) |

With a fourth agent, split Group A: the Forest and the Cave (20), the Swamp (11).

**Status** (each group edits only its own line):

- Enchanted Forest: designed, not drawn.
- Cursed Dungeon: complete (the old roster).
- Sunken Cave: designed, not drawn.
- Blighted Swamp: designed, not drawn.
- Mystic Ruins: drawn (`tools/art/cast_ruins.py`), all 11; carved stone is the bone row's deep half, rough stone the stone row's lit half (the floor is stone.shade); the Censor's red is the `red` row on the ember characters, remapped in the module's own palette.
- Frozen Waste: designed, not drawn.
- Scorched Sands: designed, not drawn.
- The Emberdeep: designed, not drawn.
- The Sketchbook: complete (the kids'), not part of Phase 2.

## 10. Decisions for the developer

1. **One jailer per chapter, or the Warden everywhere?** The fiction says the Warden is *his* jailer,
   sent rather than native, which argues for one Warden in every chapter. The brief says every
   chapter's cast is its own. This doc designs seven chapter jailers (the 48-cell row of each table);
   dropping them saves seven of the 75 drawings and changes no code - leave those slots' scenes
   uncreated and their stand-in, the Warden, stays.
2. **The Forest has no heavy.** Its table never carried a 2.2x band, and adding one makes the opening
   chapter harder. Left absent; say if it should have one.
3. **Ground warning colours** - per verb across the campaign (recommended) or per chapter (section 6).
4. **The Cursed Dungeon is the old Bonelight roster** - the buried in linen and steel. If "skeletons and
   a vampire" means literally those, that is new art for a chapter that otherwise needs none.
5. **Sprite scale.** Every enemy renders at x2.7, not the contract's x2 (section 2). New creatures
   follow the shipped 2.7; changing it is a whole-cast decision.
6. **A warm cloth row for the Sands?** Desert wrappings in the grey-green `linen` row rely on the
   per-stage light shader (`art-direction.md` section 7, not built) to warm them. A sand-cloth material
   row would let them read as sun-bleached today, at the cost of a contract amendment.
