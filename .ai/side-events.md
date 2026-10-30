# Side events: what the dark wizard keeps in each place

Five chapters each hold one optional encounter, out in the world from the first second of a run,
for the player to find. Winning it carries out something he took: a wizard in a cell, or a spell.
These are the game's first **Discovery** unlocks, the source `UnlockCatalog` declared from the
start and never used.

Read `.ai/world-and-tone.md` first. Every chapter is somewhere he holds a prisoner, and the map
card's one line (`StageCatalog.CorruptionText`) is what each event is built from.

## The five

| chapter | event | the line it comes from | what it asks | reward |
|---|---|---|---|---|
| Enchanted Forest | **Hold the grove** (`GroveEvent`) | "Every path now leads inward." | **stand still** in a ring for 20 s; leaving drains it at half speed | Bramble Seed |
| Cursed Dungeon | **The Gaoler's key** (`GaolersKeyEvent`) | "He keeps a wizard in one of these cells." | **carry**: kill the Warden, take its key across the map; three hits knock it loose | Tituba |
| Blighted Swamp | **Snuff the lanterns** (`BogLanternsEvent`) | "Nothing living lights them." | **explore**: four lanterns spread over the map, each releasing wisps, then the lantern-tender comes and is fought on its own terms (below) | Contagion |
| Frozen Waste | **The thaw** (`ThawEvent`) | "A wizard lies under the ice." | **fight here**: damage dealt near the block thaws it, fire counts triple | Väinämöinen |
| Scorched Sands | **The caravan master** (`CaravanMasterEvent`) | "The dead still walk the trade road." | **catch**: a walker on a fixed road that never turns toward you and escapes at the end | Iron Palisade |

Each asks for something different, and none of them is "a harder fight". That was the brief: an
event that is only a bigger elite is not an encounter.

## How it is built

- **`SideEvent`** is the base: where its sites are, the dormant wait and the arrival, the far
  tell, the objective line under the run timer, the edge-of-screen pointer (`SideEventPointer`),
  the success and failure beats, and standing down when the boss arrives, for the same reason
  chests and the Warden do. A subclass is only its own rules. Each one is a code-created `Node2D`
  at the run's origin that draws itself on the ground (`ZIndex` -5, y-sorted) with an additive
  light layer over the actors; the only one with a sprite is the bog lantern.
- **`SideEventDirector`** is the whole mapping from chapter to event to Discovery site to
  wayfinder, and it **places** the event and the wayfinder at stage start, from the terrain seed
  (section "Finding the event"). A chapter not in its table has no event and no wayfinder.
- **`DiscoveryRewards`** pays out a site: everything in `UnlockCatalog` with
  `Source = Discovery` and that site as `SourceId`. **Granted and saved the moment the event is
  won**, not at the end of the run, because a wizard you carried out does not go back in when you
  die three minutes later. The game over screen lists it via `Node2DGame.RecordDiscovery`.
- **Once a site is exhausted the event still runs and pays a chest.** It stays worth doing.
- `Node2DGame` exposes the short list an event may use: `SpawnEventEnemy` (the stage's own spawn
  table and health curve, so an event can never put something on the field the stage would not),
  `FindEventSite`, `ReachablePoint`, `ClampToStage`, `SpawnRewardChestAt`.

## How a win shows (2026-10-20)

A player held the grove and asked what it had done. The honest answer was: on a first win it
unlocked Bramble Seed and saved it, said so in one small line under the timer for five seconds, and
played `meta_unlock`; on every later win it put a chest at the grove's **centre** - where the player
was standing - on the same frame the grove stopped drawing, so the chest usually opened under his
feet before he had seen it. Nothing marked the win.

All five events resolve through `SideEvent.Succeed`, so all five now get the same beat:

- **Where.** The reward spot is measured from the player, toward the event's site, clamped to
  80-200 px and passed through `ReachablePoint`: never underfoot, never off screen (the caravan
  master and the lantern tender can die anywhere a spell reaches them). Standing on the site, as in
  the grove and the thaw, it goes a step up-screen.
- **A phantasmal column** (`PhantasmRise`) stands there for 2.2 s: a pale Arcane-cyan beam, motes
  drifting up it, a ring running out across the floor. Not gold, which is the elite rim.
- **The chest rises as a ghost** (`ChestReward.MaterialiseSeconds`): translucent and flickering,
  lifting out of the ground, then taking on colour and settling, and it cannot be collected until it
  is solid. Its sound is `event_reward`, a breath swelling into one glass tone. A discovery keeps
  `meta_unlock`, because that is the save changing.
- **A heading-sized line** replaces the objective under the timer: bold Cinzel, pale gold, wrapped
  for the phone canvas, fading in and out over its five seconds. A discovery reads "Bramble Seed
  recovered - a new spell" or "Tituba is free - a new wizard"; a chest reads in the event's own words
  (`ChestLine`). The label is now inset by `SafeArea.ApplyTo`; it never was, and on a phone the
  objective line sat on the XP bar.
- **The grove blooms** for three seconds after it is held - every crown swells and a third leaf
  appears - and then fades, instead of vanishing on the winning frame.

| event | first win pays | every later win pays | chest line |
|---|---|---|---|
| Hold the grove | Bramble Seed (spell) | a chest | "The grove holds - a chest rises from it" |
| The Gaoler's key | Tituba (wizard) | a chest | "The cell stands empty - a chest was left in it" |
| Snuff the lanterns | Contagion (spell) | a chest | "The lanterns are dark - the tender's chest is yours" |
| The thaw | Väinämöinen (wizard) | a chest | "The ice gives up a chest" |
| The caravan master | Iron Palisade (spell) | a chest | "The caravan master falls - his chest is yours" |

To photograph any of them: `_UiShot --scene=res://scenes/node_2d_game.tscn --stage=N --win-event`
(add `--event-chest` for the chest path on a save that lacks the discovery). It sets
`SaveManager.WritesDisabled`, because a won event grants and saves on the spot. `--win-event` now
begins the event where it was placed (`SideEvent.Arrive`) and stands the player on it.

## Finding the event (2026-10-22)

Reported: "it is on the map when the game begins - they are all further away and hidden at first
... so the events are already populated and it's up to the player to adventure, rather than wait
for timers." Until then every event started itself at 90 pace seconds, a few hundred pixels from
wherever the player was. Now nothing about an event is timed.

### Where it is

`SideEventDirector._Ready` runs before the first chunk is furnished and lays the chapter out with
`ExplorePlacer`, a pure function of **(terrain seed, stage, stream)** on the ground the generator
will produce - the rules `.ai/stage-secrets.md` section 4 wrote for secrets, applied first to the
events:

| what | band (chunks of 1536 px from the start) | bearing | footprint |
|---|---|---|---|
| the event's main site | **4 to 6** (6,100 to 9,200 px) | seeded | the event's own (grove ring + 70, the thaw's heat radius, the cell's 110, a lantern's pool + 40, the camp 140) |
| the bog lanterns' other three | a chain: each **760 to 1,100 px** from the last, never within 650 of another, all within 2,200 of the first | seeded | |
| the caravan's road | 2,000 px from the camp, the first of twelve bearings whose whole length is open ground | seeded | a 72 px strip, reserved |
| the wayfinder | **1.5 to 2.5** (2,300 to 3,800 px) | seeded, at least **75 degrees** off the event's | 90 |

Every roll is drawn before its position is tested; a spot that is water, wall, hazard, fence,
landmark or an island (a 24-cell flood from its centre) is replaced by a fixed outward search
(96 px rings to 1,600 px; on the maze the search runs over corridor-cell centres, 384 px apart,
and the event's site snaps to one). `Node2DGame.IsExploreGroundClear` answers from the streamer's
chunk **data**, generating it for far ground without painting it, so the answer is what that ground
will be when the player gets there. The footprints, and the road, are then reserved
(`ReserveExploreGround`): every furnishing pass (decor, trees by a further 140 px, curated props,
stage hazards) keeps out of them on every visit, so the furnishing stays a pure function of
(seed, chunk) and a chunk forgotten and repainted comes back with the same things round the same
site. The site itself never moves - it is a position on a node that is never freed.

Measured on seed 4242 (`_ExploreProbe`, all nine stages): sites landed 4.1 to 4.7 chunks out, the
wayfinders 1.5 to 2.2, 66 to 106 degrees apart; every site on open ground with nothing on its
footprint, identical after its chunks were forgotten and reloaded. At 220 px/s with no swarm the
main site is 28 to 42 s of straight walking; a real walk through the horde is longer, and the
distance is the price of the trip, which is what the band is for. Stages without an event
(Cave, Ruins, Emberdeep, Sketchbook) place nothing.

### How it is seen, and when it starts

The event stands **dormant**: no announcement, no objective line, no arrow. It is drawn and lit
from the start, and gives off a **far tell** on an additive light layer: a wide faint glow (480 px),
a tighter pool, and a thin column of light 500 px tall standing up out of the site, so from a
third of a screen past the edge the glow reaches in, and from further below it the column shows.
It is **cold** (the event's own colour: poison green for the lanterns, ice for the thaw, arcane for
the cell, lichen grey-green for the grove, pale steel for the caravan), because it is his, and it
**flickers**, because found things breathe (`.ai/stage-secrets.md` section 2). Light is only drawn
for a site within 2,600 px of the player, and an event with nobody near costs one distance test
per site per frame.

The event **begins when the player arrives** - `ArrivalRadius`, 380 to 560 px depending on the
event, so the thing is on screen when the line comes up - and from then on runs exactly as it did:
the announcement, the objective, the gold objective arrow, the win beat. The thaw's hit listener
goes on at arrival, not at placement.

A site that comes onto the screen is **seen** (`PointsOfInterest.UpdateSeen`, the camera's view
grown by 80 px), and a seen site keeps a point-of-interest arrow (`PoiPointer`) at the screen edge
until it is done, so a lantern glimpsed at the edge of the view is not lost by walking on. The
bog lanterns carry **no objective arrow** at all: finding them is the event; the gold arrow only
comes up for the tender.

### The wayfinder

Each event chapter hides one object much nearer the start, which marks every point of interest on
the map - the event and each of its parts - with an arrow for the rest of the run
(`PointsOfInterest.Reveal`). Walk onto it (`WayfinderPickup`, with the feet, no prompt) and it is
taken: `bearing_found` plays (a needle swinging and settling, then an open fifth), a line under the
timer says what was marked, and the object lifts and is gone. It is **something he missed**, so it
carries the secrets' near tell and not the events': a 2x2 gold glint with a one-pixel halo drawn
into its four frames, **breathing** on a 2.4 s period, with a thin warm column and a wide faint
warm glow from further off. Warm against a world whose light is all cold.

| chapter | wayfinder | sprite |
|---|---|---|
| Enchanted Forest | **the Arcane Compass**, a brass compass open on a flat stone, its needle wavering with the glint | `arcane-compass` |
| Cursed Dungeon | **the Gaoler's map**, his own map of the cells nailed to a post, one cell marked in red, the glint on the nail | `gaolers-map` |
| Blighted Swamp | the Arcane Compass | `arcane-compass` |
| Frozen Waste | **a bearing cairn**, four snow-capped stones with a brass bearing plate on the top one | `bearing-cairn` |
| Scorched Sands | the Arcane Compass | `arcane-compass` |

`SideEventDirector.ForStage` is the table. A chapter without an event has no wayfinder, since
there is nothing for it to mark. Once secrets exist, what a wayfinder marks is a decision for that
system (`PointsOfInterest.Add` is the hook); today it marks the event's sites only.

### The arrows

`PoiPointer` draws a **diamond** badge with the place's pictogram in it and a short tail toward
it, pinned to the screen edge on the line from the centre, inset 66 px at the sides, 132 at the
top (under the HUD band and the objective line) and 76 at the bottom, plus the safe-area insets on
a phone. Three promises, three shapes: the objective's gold triangle (what to do now), the chest's
round bubble (a reward), and this (a place). His places wear an arcane-cyan rim; a wayfinder seen
and not yet taken wears gold. The badge and the eight pictograms (lantern, grove, cell, ice,
caravan, compass, map, cairn) are generated by `tools/art/explore_props.py` into
`assets/bonelight/ui/poi/` and drawn at x2 on whole pixels; only the tail turns. Nothing is drawn
for a point on screen, for the point the objective arrow is already on, or during the boss.

### The bog lanterns, redrawn

Reported: "the area around the lanterns in the bog that show the boundaries are too opaque, you
can't see the lanterns. make the lanterns more prominent, and glowing brightly, the area is bigger
and more subtle." The lantern was a 4 px line and a 12 px box drawn over the actors, with a solid
black ring at 45% round it - the ring was the most visible thing there, and the wizard standing on
the lantern hid the rest. Now the lantern is a generated 64x64 sprite (a crook of dead wood out of a
tussock, an iron cage, a cold flame; three lit frames and one dark), sorted with the actors at its
foot; its flame is light drawn above the actors - a core, a halo, a pool on the ground, a column -
in the poison ramp, flickering and guttering as it is snuffed, and the others flare for 2.6 s when
one goes out. The area is its light's pool, **120 px** (was 36), marked by a faint dashed ring at
20% that brightens to 42% while the player stands in it, with the progress filling along it.
Because a 240 px pool is far easier to stand in than a 72 px spot, **snuffing takes 3 s** (was 1.2)
and progress drains over 4 s outside it. Every event's progress track is now that dashed ring
rather than the black one.

### The lantern-tender (2026-10-28)

Eric: *"lantern tender should have a glowing lantern and be bigger so it's much easier to see him. he
should fire animated fire strikes and have a few different special abilities that the player needs to
deal with".* Until then the tender was `SpawnEventEnemy(null, ...)`: a roll on the swamp's table at
elite scale, the same body as the wisps, which walked at the player and did nothing else.

It is now **`LanternTenderEnemy`** (`scenes/SwampLanternTender.tscn`), the event's own creature, in
no roster slot. `BogLanternsEvent.TenderScenePath` names it; the health is unchanged - the run's spawn
health times `TenderHealthMultiplier` (22) - so it is as hard as the run wherever the mire is found,
and the win, the discovery and the chest are unchanged.

**How it is found.** On the 96 cell at x2 (`tools/art/swamp_tender.py`, the Boo's precedent for a
miniboss on the boss cell): a stooped keeper in a wet linen coat, a violet cape over a hunched back, a
sodden hat with bog weed off the brim, wading in its own wake - about 144 px of body, twice a swamp
creature, under a crook that reaches about 180. From the crook hangs **one of the bog lanterns**,
cage and all, burning their cold poison flame, and it carries their far tell: a glow and a column of
light from the flame (drawn additively on its own layer, `LanternLight`), so it is found past the edge
of the screen the way the lanterns were. The bestiary keeps the Lamplighter's flame uncaged so no
ordinary caster looks like an event lantern; the tender is the event, and is meant to.

**What it does.** One thing at a time (a second wind-up never starts during one), and the three
specials also wait for a thrown volley to land, so two tells are never read at once.

| move | when | the tell | what it asks | numbers |
|---|---|---|---|---|
| **Fire strikes** (`LanternFireStrike`) | every 3.4 s, within 820 px | the lantern raised and brightening for 0.5 s; then each flame arcs out of it onto a ring marked on the ground for its whole 1.1 s flight, and bursts there into an animated column with a crown of tongues standing on the ring | **change course**: aimed at him, then one and two steps along his walk (0.45 s of it each); standing still, the line runs off to one side at 80 px spacing | 3 flames (5 at half health, the extra two either side of the first step), 0.16 s apart, radius 64, 3 damage each |
| **Spilt oil** (`BurningOil`) | every 5.5 s, within 210 px | it plants and a wedge is marked in front of it for 0.85 s | **step sideways, and not back**: the splash lands on the wedge, and the wedge stays alight - the one ground in the fight that is still dangerous after it has hit | 230 px, 38 degrees either side; splash 3; burns 4.5 s, 1 damage per 0.6 s spent in it |
| **The dark** | first at 8 s, then every 14 s, only at someone 360-620 px away, never while warded | it plants and lifts the lantern: its light pools on the ground round it to a ring at 280 px, and the dark closes over everything outside it for 2.2 s; the ring flares white when it falls | **come into its light** - the one ring in the game where inside is safe. The anti-kite move, and the event inverted: it was won by standing in the lanterns' light to put it out | 4 damage to anyone outside 280 px; reaching the light from 620 asks 155 px/s |
| **Rekindle** | once, at half health, if a snuffed lantern is within 1500 px | a thread of cold fire crawls from its lantern to the nearest dark one for 1.6 s and lights it (`hint_lantern_flare` plays there) | **put it out again**: while that lantern burns it is warded - a ring of motes round it, the thread to the lantern - and takes a quarter of every hit. The objective line and the gold arrow move to the lantern, which is snuffed by the event's own rule, 3 s in its pool | ward 0.25 of damage taken, rounded stochastically as Weaken is |

Body: radius 30, speed 84 (times the pace multiplier), contact 2, knockback resistance 0.7, slow floor
0.35, `IsMiniBoss` (a blocker, never recycled, immune to fear), stands still once the player is within
150 px - its lantern reaches, it does not need to touch him. Every hit it deals goes through
`ScaleOutgoingDamage` (the strikes and the burn scale theirs when thrown, as `TelegraphedGroundHit`'s
callers do). Its rings, the oil's wedge and the dark's edge are drawn through the inverse of its scale,
so the elite's 1.06 and the hit punch never draw a shape bigger than the one tested.

**What passes between the event and the tender** is `ILanternKeeper`, four calls: the nearest snuffed
lantern, where its flame is, rekindle it, is it lit. The event owns the lanterns and the tender owns
its ward. Snuffing a relit lantern spawns no wisps.

**The flame is the lanterns' cold fire, not the fire ramp**, because what it throws is theirs:
"nothing living lights them". It is a judgement call, and a one-colour change if Eric wants orange -
`swamp_tender.FLAME` (then re-run it) and `LanternFireStrike.FlameCore/Hot/Mid`. Worth knowing before
deciding: the common XP orb is grass `#9BD455`, a near neighbour of poison's mid `#B4E04A`, so a
flame in flight and an orb on the ground are close in hue (the swamp's poison bolts already are).
The ring and the arc separate them; orange would too.

**Checked and photographed**: `RegressionChecks.ValidateLanternTender` (zero wind-ups, a strike flight
under 0.6 s, a wedge that cannot reach what it aims at, a dark whose light cannot be reached in its
tell, a ward that is no ward, the strips its fire draws from) - shown to fire by breaking three values
in the scene. `_ExploreProbe --stage=3` wins the event; with `--slow-bleed` the tender lives to
rekindle (at 0.41 health, the wind-up having waited for a volley), the probe follows the arrow to the
lantern, the ward breaks when it is out, and the event is won (39.6 s of game time after arriving).
`_UiShot --tender[=strikes|oil|dark|rekindle]` calls it through the event's own path and stands it
beside him (`--tender-offset`, `--tender-crowd`; the rekindle needs `--goto=site:3`); pair with
`--fixed-fps 60` on the Godot command line, or a slow software render runs several physics steps a
frame and `--fx-lead` stops meaning frames of the tell. **Not checked: whether it is fun, or the
numbers.** Every value above is a first guess, it was never fought by hand, and nobody has heard it.

### Probes and flags

- `_ExploreProbe` (`scenes/_ExploreProbe.tscn -- --stage=N [--seed=N] [--no-complete]`): placed at
  start and in band, deterministic, hidden until seen or revealed, the wayfinder reveals, the
  site and its furnishing survive unload and reload, dormant at 900 px and begun on arrival, and
  (bleeding its elites) finishable. Twenty-two to twenty-eight checks a stage; it renders nothing.
  `--slow-bleed` bleeds an elite at 4% of its health a second and gives back whatever the player's
  spells took, so a fight with a phase lives to reach it (the lantern-tender's rekindle, above).
- `_BalanceHarness --explore`: the bot walks to the wayfinder, then along the arrows to every
  site, following the objective once the event has begun, and logs each leg's time and distance.
  Without the flag the immortal bot holds its ground at the origin and **never meets the event**,
  which is deliberate: the gate measurements are made without it, as before.
- `_UiShot`: `--goto=wayfinder|event|site:N` (with `--goto-offset=DX,DY`), `--take-wayfinder`,
  `--arrive`; `--explore-log` on any run prints the placement.

## Hints that something is out there (2026-10-25)

Reported: "give hints part way through the runs of the unlockables on maps, such as a woman yelling
for help on the cursed dungeon map so the player identify that there are secrets to find". Since
2026-10-22 every event waits four to six chunks out and says nothing until the player arrives -
right for the hunt, wrong for a player who does not know there is anything to hunt. So, partway
through a run, while the chapter still holds something this save has not carried out, a cue comes
**from its direction**: a short generated sound panned that way, and a caption at that edge of the
screen with a tail pointing off toward it.

### When, and when not

- **On the pace clock** (`Node2DGame.PaceSeconds`): first at **300** pace seconds (a third of the
  reference run, when the build has its shape and a detour is affordable), then every **150**, none
  due after **810** (13:30 - too late to walk there and back before the boss). Four a run at most.
  A shortened run hears them proportionally earlier.
- **Only while the save lacks something the site gives** (`DiscoveryRewards.IsSiteExhausted`, checked
  at the moment of playing). Once Tituba is free nobody calls from the cell again; on a save that owns
  every discovery the system is silent.
- **Only while the event is dormant and none of its sites is marked.** Arriving starts the event's
  own announcement and objective; a site that has been **seen**, or **revealed by the wayfinder**,
  has a point-of-interest arrow (`PoiPointer`), which says where it is better than a voice can. The
  hints retire for good the moment either happens.
- **Never during the boss**; a caption up when it arrives fades out at once. Never while the tree is
  paused (the director does not process), so a level-up only delays one.
- **It writes nothing.** It reads the unlock state; no tool scene can mark anything found through it.

### How it relates to the wayfinder and the arrows

It comes **before** them and never alongside them. The far tell is light you see from about a third
of a screen past the edge; the wayfinder is an object 1.5 to 2.5 chunks out that marks everything;
the hint is the only one of the three that reaches a player who has seen neither, from anywhere on
the map, and it stops as soon as either has done its job. It draws no arrow: the edge of the screen
already carries three promises (the objective's gold triangle, the chest's round bubble, the
place's diamond), and the caption's tail is a speech bubble's - it says where the voice came from,
not "go here". Its rim is the event's far-tell colour (`SideEvent.FarTellColor`), so the light the
player finds is the colour the hint told him to look for.

### The caption

`StageHintCaption`, on the `UIOverlay`: a near-black band (`#0D0F17` at 92%) with a 2 px rim and a
tail, the line in the engine sans at the **Body** role (24 on a phone, 20 on a PC). Parchment ink for
speech, set in quotation marks; a cool pale ink for a sound, set plainly as a caption of it. Both are
about 15:1 on the band (`.ai/level-up-screen.md` 4a). It fades in over 0.35 s, holds 4.8 s and fades
over 0.9 s.

Placed every frame from the player's position on the canvas: pushed along the line to the source to
the edge of a safe inner rectangle, then held inside it. On a PC that rectangle starts at 150 (under
the timer, the boss bar's slot, the objective line, the onboarding tip and the loadout column). On a
phone it starts under the touch stick's HUD band (176 plus the safe area) and **stops at 70% of the
height**, so it never sits where the thumb holding the stick is; and since a caption to the side of
a 720-wide canvas is wider than the half-screen beside the wizard, it is stepped up (or down, for a
source below) off him. It takes **no input** - it and its label are `MouseFilter.Ignore`, so a
finger on it lands on the stick beneath as if it were not there.

**Tone.** `.ai/world-and-tone.md` rule 2 says no copy carries the mood. These are not taglines: the
cry is a person's words, and the rest are captions of a sound, the way a subtitle captions one -
which also makes them the only form of the cue a player with the sound off gets, as most phone
players have. **A hint never names what it is about**: a rescued wizard's name is his reward
("Secret wizards" above), and a found spell's name in a caption would be the answer printed on the
question. `RegressionChecks.ValidateStageHints` warns on any line that contains one.

### The hints

| chapter | site | voice | sound | lines, in order (the last repeats) |
|---|---|---|---|---|
| Enchanted Forest | `the_grove` | sound | `hint_grove_whisper` | "Leaves whispering, though there is no wind." / "The whisper again. Something out there still grows." / "Fainter now, the whispering. It has not stopped." |
| Cursed Dungeon | `gaolers_key` | **speech** (Tituba, unnamed) | `hint_cell_cry` | "Help! Is someone there?" / "Please - I can hear you. I am down here!" / "I have been patient so long. Please, this way." / "Do not leave me here. Not again." |
| Blighted Swamp | `bog_lanterns` | sound | `hint_lantern_flare` | "Far off, a cold light flares and gutters." / "The cold light again. Nothing living lit it." / "It is still burning out there." |
| Frozen Waste | `the_thaw` | sound (Väinämöinen, unnamed) | `hint_ice_song` | "The ice groans. Under it, faintly, someone is singing." / "The song again, slow and deep, from under the ice." / "The singing is weaker now." |
| Scorched Sands | `caravan_master` | sound | `hint_caravan_bell` | "A caravan bell, somewhere out on the sand." / "The bell again. Something walks the old road." / "The bell is still moving. It does not stop." |

Tituba speaks in her own character: a prisoner taken from Barbados and jailed on an accusation,
patient (her passive is *Patience*) and nobody's witch - so she asks for help and says she has
waited, and never says why she is there. Väinämöinen cannot speak through the ice; he is heard
singing, because he is a singer. The other three are not people, so nothing speaks. A line says
something is there and roughly where; none says what to do (no key, no snuffing, no catching).

The sounds are generated (`tools/audio/sfx.py`, "chapter hints"), registered in `SfxCatalog`, and
described in `.ai/audio-manifest.md`, "Chapter hints". Played 300 px toward the source, inside
`SfxPlayer`'s 900 px audible radius, so the 2D listener pans them that way.

### Adding one

1. An entry in `StageHintCatalog` keyed by the stage id: the Discovery site its side event pays out
   (it must be the one `SideEventDirector.ForStage` maps that chapter to - the hint comes from that
   event's place), a voice, a sound, and its lines (60 characters at most: two lines on a phone).
2. A sound in `tools/audio/sfx.py` beside the five, registered at -9 or -10 in category `hint`; run
   `python tools/audio/build.py hint_` and `analyse.py --confuse`, then check it against the whole
   set, not only its category (the first three drafts collided outside it). Add it to `SfxCatalog`,
   import it, and give it a manifest row.
3. Nothing else. `SideEventDirector` starts the director for any chapter whose hint names its
   event's site; `RegressionChecks.ValidateStageHints` checks the stage, the site, the event, the
   sound, the line lengths and the no-names rule.

A hint about something that is not a side event - a stage secret, once they exist - needs
`StageHintDirector` taught where that thing is, and needs `.ai/stage-secrets.md` section 2 revisited
first: it says a secret gets no text and no pointer, and a hint is both a line and a direction.

### Photographing and driving it

- `_UiShot --force-hint` plays the next hint `--fx-lead` frames before the shutter instead of on the
  pace clock; every other rule holds, so pair it with `--fresh-save` (in memory, writes off), e.g.
  `--scene=res://scenes/node_2d_game.tscn --stage=1 --fresh-save --force-hint --frames=200 --fx-lead=40`.
  With `--take-wayfinder`, or `--free-wizards=tituba`, it reports that no hint played - both checked.
- `_BalanceHarness --fresh-save` runs on a brand-new in-memory save, so the hints play on their real
  schedule: `--stage=1 --minutes=14.5 --immortal --fresh-save` logged `StageHint: stage_1 hint 1 at
  pace 300s` and then 450, 601 and 750, each line in order.
- Every hint prints one `StageHint:` line with its pace time and distance.

**Not verified: whether anyone hears them as intended.** The sounds were measured (levels, `hf`,
distances to every other sound), not auditioned, and a formant cry is the hardest thing in the set
to judge by numbers. Whether 300/150 is the right rhythm, and whether a caption at the edge pulls a
player off his kiting line or is ignored, needs a playtest.

## Rules every event inherits

- **Nothing an event places joins `"enemies"` unless it is an enemy.** Every targeting path
  iterates that group, and an ice block or a lantern in it would pull the whole loadout off the
  swarm. This is the breakables rule, and **the thaw is built around it**: the ice is never a
  target. It listens to `Player.EnemyDamagedAt` and warms from hits landed near it, which is how
  "only area damage and fire break it" works without a breakable-object system.
- **Anything an enemy drops for the player to reach goes through `ReachablePoint`.** Enemies are
  not held to the bounds the player is. The first Gaoler's key fell where the Warden died, outside
  the player's clamp, and could not be picked up.
- **Nothing an event does is timed, and nothing it places moves.** Where it is comes from the
  seed (`PlaceSites`), when it starts from the player's feet (`ArrivalRadius`). A new event
  overrides `PlaceSites` only if one site is not its shape, and must reserve any ground it needs
  kept clear.
- **Event enemies are elites** (`IsMiniBoss`), so `RespawnEnemy` never recycles a key-carrier or
  the caravan master to the far side of the map.
- **`Enemy.ScriptedHeading`** overrides the chase. The caravan master walks its road with it.

## The rewards

### Two wizards, named people

`.ai/world-and-tone.md`: the starters are disciplines, and everyone rescued is a person.

- **Tituba**, taken from Barbados and jailed at Salem on an accusation. She opens on **Riptide**
  (the sea she was taken from), deliberately *not* a darkness or curse spell: she was a real woman
  wrongly accused, and the game does not agree with her accusers. Passive *Patience*: +15% duration.
- **Väinämöinen**, the singer of the Kalevala, who sowed the first forests. Opens on **Bramble
  Seed**. Passive *Song of Making*: +10% area.

Both wear **no player pigment**. The four saturated rows are the starters'. The rescued wear what
they were held in, so the only saturated thing on them is their staff head. Sprites are in
`tools/art/roster.py`; `head()` gained a `tone` argument so Tituba is not drawn in the light skin
tone the starters share.

`Player.GetLevelUpOptions` always offers a character its **own starting spell**, unlocked or not.
Both open on spells a save may not own yet.

### Secret wizards: sealed, then a silhouette, then free (2026-10-22)

A wizard's card on the character select is in one of three states (`CharacterSelection.CardState`):

| state | when | portrait | name | line under it |
|---|---|---|---|---|
| **Secret** | locked, and his reveal condition not met | a wax seal with a question struck in it (`sealed-portrait.png`) | ??? | "Not yet discovered." |
| **Locked** | locked, revealed | his **silhouette**, animated with his idle | ??? | his `LockedHint` |
| **Unlocked** | owned | the wizard | his name | spell and passive |

**Both rescued wizards are secret until the chapter that holds them opens**: Tituba until the
Cursed Dungeon (`stage_1`, opened by beating Elderbark), Väinämöinen until the Frozen Waste
(`stage_5`). The reveal is data: `UnlockDefinition.IsSecret` and `RevealStageId`, set through
`DiscoveryCharacter(..., secretUntilStage:)`. Why that condition:

- Before the chapter opens the player cannot act on anything about him, and the stage list does
  not show a closed chapter at all (`StageSelection`: "a chapter the player cannot enter is not
  listed"). A silhouette with "Freed from a cell in the Cursed Dungeon." on a fresh save names a
  place the game has not shown yet.
- The chapter's card says it the moment it opens - "He keeps a wizard in one of these cells.",
  "A wizard lies under the ice" - so the character select now reveals him on the same unlock that
  puts that line on screen.
- **It is derived, not stored.** `GlobalStatsManager.IsCharacterRevealed` reads
  `IsStageAvailable` for the reveal chapter, so there is no "revealed" flag, no schema bump and
  nothing to migrate. A save that already owns him shows him unlocked whatever is open.

**The name stays hidden while he is locked.** The silhouette is the tease and the name is the
reward: the rescue line reads "Tituba is free - a new wizard", and the rescued are people
(`.ai/world-and-tone.md`), not a class a player could read off the coat. The starters never lock,
so this only ever hides a rescued name.

**The silhouette** is built at runtime from the sprite's alpha (`CharacterVisuals.BuildSilhouetteFrames`):
every opaque pixel filled with occlusion `#05070C`, the edge pixels lit as a rim in floor stone -
`hi #3A4658` where the open side faces the key light (up or left), `lit #2A3444` elsewhere. No
pigment, no staff glow, no face. The rim is what makes it read: the old locked card multiplied the
wizard to near-black, about 1.2:1 against the card, and read as a hole.

**The seal** is generated (`tools/art/props_fx.py`, `sealed_portrait`): the mutation card's red
wax at portrait size, on the 48 cell at 1:1 so it sits on the same pixel grid as the wizards. It is
deliberately not the hooded unknown figure, which is a silhouette of *somebody*.

`RegressionChecks.ValidateUnlockCatalog` (`ValidateSecret`) warns when a secret has no reveal
chapter, names one `StageCatalog` does not define or that is not playable, is revealed by the
campaign-gated finale (which needs every wizard first), is a spell or a starter (nothing reads
`IsSecret` there), reads as revealed on a brand-new save, or - for a Discovery wizard - is revealed
by a chapter whose side event is not the one that pays him out. All of these were checked by
pointing the two at a missing chapter and at the wrong one.

**Not built: a once-only reveal moment.** A beat the first time a sealed card opens needs to know
it has been seen, which is a save field, a schema bump and a migration - and schema 11 is already
planned for secrets in the world (`.ai/stage-secrets.md`). If it is wanted, add a
`SeenRevealedCharacterIds` list in that bump rather than a separate one.

Photograph the three states without touching the real save (`_UiShot`, in this order):
`--fresh-save` (new save, writes off), `--open-stages=stage_1,...` (opens chapters, which reveals),
`--free-wizards=tituba` (unlocks), and `--hover-late=CharacterButton6` to put that card in the PC
detail pane - a locked card cannot take focus, so a hover is the only way to show one there.

### Three spells, one per element that had no pure spell

Metal, Poison and Grass had no pure spell, and only a pure spell can carry an element to its
capstone (see the element-tag rules in `.ai/gameplay-design.md`). Each new spell closes one, and each is a
shape the roster did not have:

- **Iron Palisade** (Metal): a line of stakes between you and the nearest pack, which shoves back
  and cuts whatever walks into it. The **wall** #64 listed as missing. It is not a physics body,
  which would need a new collision layer and would also stop the player. Like Hollow Star it rides
  `ApplyKnockback`, so boss resistance applies.
- **Contagion** (Poison): one carrier; when a marked enemy dies, the plague jumps to everything
  within reach. **It spreads on death, by itself.** It is bounded by a per-cast cap of 24 infected,
  and by marks lapsing.
- **Bramble Seed** (Grass): planted at your feet, it **grows** from 16 px to 110 px over six seconds.
  Every other zone is at full size the moment it appears. At most three stand at once. It is drawn
  as thorned roots writhing out of the ground, laid out from the same radius the damage reads, so
  it cannot be drawn wider than it hurts (`.ai/spell-roster.md`, section 2a).

## The final chapter's gate

**Superseded at schema 19** (`.ai/achievements-and-secrets.md` 2.5 and section 11): the Emberdeep opens
on the Long Coil beaten and every wizard whose rescue comes before it, not on spells; and each event's
unlock is now the reward of that event's Road deed (`event_<site>`), granted the moment it is won as
before. What follows is the history.

The Emberdeep opens on "every spell recovered and every wizard freed", evaluated live, so the five
discoveries are now part of it. All five events sit in chapters before it. **Schema 10** credits
the discoveries to any save that had already met the gate, so nobody loses a chapter they had
opened. The schema-8 grant of "every wizard" now skips Discovery wizards, which did not exist when
everything was free.

## Verified, and not

Driven headless by a throwaway probe that walked to the pointer and killed what was near. All
five ran to completion and granted the right unlock, with the user's save backed up and restored
around every run. The thaw took 26 s and the grove 22 s. Each found spell dealt damage in a live run. A
frame of four events was captured and every one draws. The migration was checked on in-memory
saves.

**Not verified: whether any of it is fun, or the numbers.** Heat needed for the thaw (900), the
grove's 20 s, the master's health (26 times ordinary spawn health) and walking speed, and the
three-hit drop are first guesses. The probe hit for 400, so its times say the logic completes, not
how long a real run takes.
