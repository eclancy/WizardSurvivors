# Art direction — Candle Ward, Bonelight tone

The chosen direction, decided 2026-09-03 after an audit of the existing art, three drawn
direction proposals, and a five-option tone study at the chosen pixel density. This file is the
contract: the grid, the light model, the palette, the silhouette rules, the animation and import
settings, the per-stage light table, and the migration order.

`.ai/art-inventory.md` still describes what we own. This describes what replaces it.

**The one-line version:** every material gets a plain albedo, and a single shared light model
does the colouring — a cold key from the upper left, a deep indigo ambient in shadow, and a
1 px bone-white rim on the key-facing edge of every actor, softened where it lights a dark surface
(§2, "Matte, not wet"). Colour is a *lighting* variable, not a pigment one, so a stage changes
room through two shader uniforms instead of a repaint.

## Why this, and why Bonelight

The audit found the real problem was never the palette, it was the grid: eleven different
effective pixel sizes on screen at once, no integer scales, nothing filtered nearest-neighbour,
and the floor being *minified* below 1:1. Two of the three proposals fixed that by flattening
the art (a locked 16-colour index; a stained-glass lead-line system). Candle Ward fixes it while
leaving room to grow, because what distinguishes an entity is light, and light is cheap to add
later — new elements, elites, status effects and stage moods are all recolours of one system.

Bonelight is the tone. The brief asked for creepier and less colourful, and the tone study
showed those are two different requests: **less chroma** (drain the world, which Cold Ash does)
or **less light** (keep the chroma, put the frame in the dark, which Bonelight does). Bonelight
was chosen because it wins on both mood and measurement:

| | Candle Ward | Cold Ash | Fenlight | **Bonelight** | Bleachbone |
|---|---|---|---|---|---|
| Worst actor vs floor | 1.45:1 | 1.39:1 | 1.54:1 | **2.29:1** | 1.70:1 |
| Best actor vs floor | 2.19:1 | 2.53:1 | 2.65:1 | **3.68:1** | 3.48:1 |
| Spell impact vs floor | 3.95:1 | 5.04:1 | 5.57:1 | **7.48:1** | 1.21:1 |
| Actors clearing 3:1 | 0 / 4 | 0 / 4 | 0 / 4 | **2 / 4** | 1 / 4 |
| World mean saturation | 0.43 | 0.19 | 0.47 | 0.45 | 0.34 |

Bonelight's *worst* actor beats every other option's *best* one, and it gives effects nearly
double Candle Ward's headroom. It gets there without draining chroma — its shadows are deeply
chromatic blues, which is why it measures more saturated than the tone it replaced. That matters
for content: Cold Ash's drained world makes every coloured thing loud, so twelve elements all
read as "a bright thing happened" before they read as *which*. Bonelight keeps the hues.

The costs were stated up front and have not gone away. This is the most expensive of the three
directions to author, and the only one whose payoff depends on a shader we have not written.
Both are priced into the migration order in §8: engine and grid first, shader last, so the art
reads correctly at every intermediate step.

## 1. The grid

All art is authored 1:1. No integer upscales in source files, ever — the audit confirmed the
current art is clean on this and it must stay clean.

| Kind | Authored cell | Figure fills | Render scale | On screen |
|---|---|---|---|---|
| Basic enemy | 32×32 | ~26–29 px tall | ×2 | ~52–58 px |
| **Player** | **48×48** | ~40 px | ×2 | ~80 px |
| Elite / large enemy | 48×48 | ~40 px | ×2 | ~80 px |
| Boss | 96×96 | ~80 px | ×2 | ~160 px |
| Floor tile | 32×32 | full | ×2 | 64 px |
| Prop (small / large) | 32×32 / 64×64 | full | ×2 | 64 / 128 px |
| Projectile | 32×32 | varies | ×2 | 64 px |
| AoE / impact | 64×64 or 96×96 | varies | ×2 | 128 / 192 px |
| Pickup (XP, health) | 16×16 | full | ×2 | 32 px |
| UI icon | 32×32 | full | ×2 | 64 px |
| Stat glyph (one per stat line, `tools/art/stat_icons.py`) | 12×12 | full, 1px clear rim | ×2 | 24 px |

**The player is the one actor on the elite cell, and that is deliberate.** It is the only figure
on screen for every frame of every run, it is the fiction's light source, and reading as larger
than the horde is the point rather than a favour. It costs no new cell size and no new render
scale — 48×48 was already in this table. See `.ai/world-and-tone.md` for the reasoning.

**Render scale is ×2 for everything.** One integer, at every tier, no exceptions — a boss is a
bigger *cell*, never a bigger scale. With `Camera2D.zoom = 1.0` and nearest filtering this puts
every art pixel on exactly two device pixels, which is the whole point.

**This makes actors roughly 1.9× their current on-screen height.** Today's skeletons are a 15 px
figure at scale 2.0 → 30 px; a Bonelight actor is a ~28 px figure at ×2 → 56 px. That is a
deliberate trade: fewer, larger, individually readable bodies. Wave counts, spawn radii and
`MinPlayerSeparation` all need re-tuning when phase 1 lands — expect to cut concurrent enemy
counts by roughly half to hold the same screen coverage. If a playtest says the horde feels
thin, the fallback is to author actors at **24×24** and keep ×2, landing at 40 px. Do not fix it
by changing the render scale.

## 2. The light model

This is the part that must not drift. Three lights, shared by every asset in the game:

- **Key** — cold, directional, from the **upper left**, direction `(-1, -1)` normalised.
  Colour `#DCE8FF`.
- **Ambient** — deep indigo fill, non-directional. Colour `#232C46`.
- **Rim** — 1 px bone-white, on the **key-facing silhouette edge of actors only**.
  Colour `#DCE8FF` where it lights a pale surface, the softer `#A8B8DC` where it lights a dark
  one (see "Matte, not wet" below).

`#DCE8FF`, `#A8B8DC` and `#232C46` are **reserved**. They may not be used as pigment anywhere in
the project, because the per-stage shader (§7) keys on an exact colour match to retint them.

### Every material is five tones

| Slot | Construction | Rule |
|---|---|---|
| `hi` | albedo × key, brightest, hue rotated **+8 to +12° toward cyan** | Sparingly — the crown of a lit plane, never a plane itself and never a lone glint ("Matte, not wet") |
| `lit` | albedo × key at grazing incidence | The upper-left facing planes |
| `base` | albedo under half light | The bulk of the form |
| `shade` | albedo × ambient, hue rotated **−18 to −24° toward indigo**, saturation **held or raised** | Never a desaturated `base` |
| `deep` | the shade pushed to near-occlusion, still chromatic | Interior crevices, under-forms |
| `occ` | `#05070C` — shared, not per-material | Contact line and cast shadow only |

Three rules do the heavy lifting, and they are the ones that get broken first:

- **`shade` rotates toward indigo and keeps its chroma.** A shade that is just a darker base is
  what makes a game look drab, and it is the exact failure the audit measured in the existing
  art (world mean saturation 0.33 against neon 0.66 effects). Colour survives into the shadows
  here, which is how a dark game still reads as a colourful one.
- **`occ` is one shared value.** Every contact shadow in the game is `#05070C`, so the whole
  cast reads as standing on one ground plane in one room.
- **Nothing dark is allowed to be grey.** Bonelight is dark, not desaturated. If a tone's
  saturation drops below about 0.20, it is wrong — push it back toward indigo or teal.

### The rim is load-bearing

Actors have **no outlines**. Separation from the floor is carried entirely by the 1 px rim plus
the `occ` contact line. This is the direction's known density failure: forty enemies lit by the
same key from the same angle stop being distinguishable *by the key*, and the rim is what saves
them. Consequences:

- The rim is redrawn every animation frame, because it follows the silhouette. Budget roughly
  **2× the time of a flat sprite** per frame.
- Never add an outline "to help readability". It breaks the model. The fix for a sprite that
  will not read is a stronger silhouette or a boosted rim, not a black line.

### Matte, not wet

Decided 2026-10-28, from a playtest: *"characters and enemies should be slightly less 'shiny' -
the highlight on some of the characters makes them look wet. tone that down a bit."*

A highlight reads as **wet** when it is the colour of the light rather than of the surface, small,
and hard against a dark stop with nothing between: that is a specular, and a specular is what a
wet surface has and a matte one does not. Measured across every actor sheet, three things did it:

1. **The rim.** Two thirds of rim pixels lit a surface darker than L\*50, so most of the cast wore
   a near-white line (L\*92) straight against its darkest stops - the glint along a wet edge, and
   on a dome (an egg, a shell, a back) the reflection off a wet one. On the spindly enemies the
   rim is half the width of a limb, and they read as glass.
2. **Highlights as points and streaks.** `hi` as a dot in the middle of a dome (Mother Rot's sacs,
   the blister toad's warts), `hi` and `lit` as one-pixel streaks hard on `shade` or `deep` beside
   a fold (the Frostweaver's mantle had steel `lit` on steel `deep`, 64 L\* apart). The player rows'
   top two stops are nearly white, so a fold drawn as light-stop-beside-dark-stop came out as satin.
3. **The familiar's hot spots.** Its renderer lights each blob by its own normal, so the top stop
   landed as a round spot mid-form and the dither sprinkled it across the lit planes.

Two rules now, applied by **`tools/art/matte.py` to every actor frame after it is drawn**, so a
generator draws exactly as before and the finish decides. `pixel.write_strip`/`write_frames` run it
on any palette that carries the rim (the rim marks an actor's palette); `familiar.Fig.render(finish=True)`
runs it on the familiar's own ramps; `sprite_boo.animations` runs it on the Boo.

- **A highlight is a plane, not a point.** A `hi` or `lit` pixel that stands more than **35 L\***
  above a neighbour on its own ramp steps one stop down, and a `hi` with no `hi` or `lit` of its own
  ramp beside it steps to `lit`. The brightest stop survives where it crowns a lit plane; where it
  was a glint, a dot or a streak against the dark, it becomes the next stop, and the light turns
  the form instead of reflecting off it. Measured in L\* rather than in stops because a stop is 6 to
  28 L\* depending on the row. **Gold, arcane and every element ramp are never stepped** - they are
  light sources (§7) - so eyes, lanterns, crystals, embers and spell light keep their glow, and a
  plane of steel still gleams, a little.
- **The rim is the key as the surface returns it.** Full `#DCE8FF` where it lights a surface of
  L\*63 or more; the soft rim `#A8B8DC` (L\*75, the key's hue) on anything darker. A pale robe keeps
  its crisp edge, where a white line costs nothing; a dark body - most of the horde - gets a rim that
  still clears every floor by a wide margin but no longer glints.

What it cost: nothing moved. No silhouette, fold, anchor, frame or `.tres` changed - only a highlight
one stop down, or a rim one notch softer: 82% of rim pixels and 4.8% of all actor pixels, median
7.4% of an idle or walk frame. The hurt flash is left bright on purpose (it steps the whole figure
up its ramps, which compresses the top of every ramp, so few of its jumps exceed the threshold).
Not finished, by design: the title art and app icon (`hero.py`, `splash.py`, `icon_concepts.py`,
which use the full key as a painting's light), the evolution and spell icons that paint the familiar
(they call `Fig.render()` bare), the test wizard, and the kids' art.

## 3. Palette — Bonelight

Shared light:

| Role | Hex | Notes |
|---|---|---|
| occlusion / contact | `#05070C` | shared by everything |
| rim (reserved) | `#DCE8FF` | never pigment |
| soft rim (reserved) | `#A8B8DC` | the rim where it lights a dark surface (§2, "Matte, not wet"); never pigment |
| key light colour (reserved) | `#DCE8FF` | shader-side |
| ambient light colour (reserved) | `#232C46` | shader-side |

**Player pigment — added, and reserved.** Four saturated rows exist that nothing in the world
may wear except the player cast:

| Row | hi | lit | base | shade | deep | Whose |
|---|---|---|---|---|---|---|
| ember | `#FFC0A8` | `#FF6A4A` | `#D8242E` | `#8A1220` | `#4A0810` | Pyromancer / Fireball |
| azure | `#C8ECFF` | `#6AC0FF` | `#2A72D8` | `#1A3E8A` | `#0E1E48` | Frostweaver / Cone of Cold |
| amber | `#FFF4B0` | `#FFD63A` | `#D89A10` | `#8A5E0A` | `#463006` | Stormcaller / Chain Lightning |
| verdant | `#D0F0A0` | `#8AD048` | `#3E8A28` | `#255416` | `#122A0A` | Geomancer / Obsidian Spike |

They had to be added because the rows below are the palette of a drained world and every one of
them is desaturated by design: the most saturated warm row was `red #8A3038`, a maroon at
lightness 0.27. There was no true red in the contract and no true green, and a Pyromancer cannot
be drawn in a colour the palette does not contain.

Reserving them to the player is not a restriction, it is the fiction in pigment. `world-and-tone.md`
says the horde is not a faction and has no colour, and that the player carries the only light —
so the starters being the only saturated figures on screen says that without a word of text. It
also does the job a *starter* roster has to do: four instantly separable characters for a player
who knows nothing yet.

This took the contract from 106 colours to 126. It only widens what is allowed, so no existing
art was invalidated by it.

Materials — `hi / lit / base / shade / deep`:

| Material | hi | lit | base | shade | deep |
|---|---|---|---|---|---|
| Floor stone | `#3A4658` | `#2A3444` | `#1E2634` | `#141A26` | `#0B0F18` |
| Lichen / moss | `#5A8A94` | `#3A6470` | `#2A4E58` | `#24404A` | `#16282E` |
| Indigo wool (player) | `#9AB0F0` | `#6A86E0` | `#3A52A8` | `#23306A` | `#14183A` |
| Bone / pallor ("skin") | `#EEF2FA` | `#D4DCEA` | `#9AA4B8` | `#626E84` | `#38404F` |
| Living flesh | `#F2DCC4` | `#D8A484` | `#A87058` | `#6E4450` | `#3C2436` |
| Gold (trim, lantern) | `#FFF0C0` | `#D8B04A` | `#8A6A1E` | `#543F10` | `#2E2208` |
| Steel (armour) | `#DCE8F4` | `#B8C8DC` | `#6A7E98` | `#3A4A62` | `#202838` |
| Cloth red (tabard) | `#8A3038` | `#6A2028` | `#4A1820` | `#2A0E14` | `#16070A` |
| Grave linen (ghoul) | `#A2B0A0` | `#7A8A7C` | `#46544C` | `#2C3834` | `#1A2220` |
| Robe violet (caster) | `#6E6296` | `#4E4470` | `#342C4C` | `#221C33` | `#14101F` |
| Arcane cyan | `#DCF4FF` | `#6AB8E8` | `#2A6A9A` | `#17415E` | `#0B2436` |
| Sand cloth (Scorched Sands only) | `#D8CCA4` | `#A88C62` | `#7C6040` | `#54362E` | `#341E24` |

**Sand cloth** was added 2026-10-24 for the Scorched Sands' cast (`.ai/bestiary.md` section 10, item
6, Eric's call): desert wrappings in grey-green `linen` only read sun-bleached under the per-stage
light shader (§7), which is not built. It is the one warm cloth, worn by that chapter alone, so it is
defined in `tools/art/cast_sands.py` rather than in `bonelight.py`. Its base and lit sit where steel's
shade and base do (lightness 0.37 / 0.52), so a wrapped body is no brighter than an armoured one,
and its shade rotates 19 degrees toward indigo with its chroma held. Its lower stops are also that
chapter's wood (the Swollen Cask), the one warm brown the contract holds.

That is **fifty-five material colours plus five lights** (the soft rim, 2026-10-28, is the fifth), against the 935 the current art
carries. It is a budget, not a suggestion: a new material means a new five-tone row added to this
table, reviewed, and then used — never a colour picked per sprite.

Note the deliberate asymmetry: gold, steel `hi` and bone `hi` are the only high-value tones in
the game. On a floor whose brightest stone is `#3A4658`, those read as light sources. Spend them.

**On the two skin rows.** The row the code calls `skin` is *bone*: it is what skulls, the moon
and grave-linen figures are made of, and it is deliberately cold and moon-pale. It is not a
person's skin. **Living flesh** was added as its own row (2026-09-04, for the title-screen
figure's hand) because everything in this palette except gold is cold, and a hand borrowed from
the bone ramp reads as a gauntlet or a corpse. It follows the five-tone rule like everything
else: its `shade` rotates toward indigo-magenta and *holds* its chroma rather than going grey.
Use `flesh` for anything alive and `skin` for anything that is not.

### Elements are emissive, not pigment

Twelve elements do not fit in any pigment palette — that is what sank the locked-palette
proposal. Here they live in the emissive ramp of effects, under one rule:

> **The core of every element's effect is white-hot. Identity lives in the mid and edge.**

So a cast always reads as "a spell went off" at a glance, and the hue answers "which one" a beat
later. One drawn burst can serve every element by remapping its ramp.

| Element | core | hot | mid | edge |
|---|---|---|---|---|
| Fire | `#FFF4D0` | `#FFC848` | `#E87A1A` | `#B8381A` |
| Ice | `#F4FEFF` | `#C8F4FF` | `#7FDCF8` | `#2E7FC4` |
| Arcane | `#FFF0FF` | `#E8C0FF` | `#C08CFF` | `#7A3FD4` |
| Darkness | `#E8D8FF` | `#B48CE8` | `#8A5CC4` | `#35205C` |
| Light | `#FFFFFF` | `#FFF8D8` | `#FFF0A8` | `#FFC63C` |
| Grass | `#F4FFDC` | `#CFEE94` | `#9BD455` | `#4A8C3A` |
| Earth | `#FFF0D8` | `#E8C48C` | `#C99A5E` | `#7A5024` |
| Wind | `#FFFFFF` | `#EAFCF6` | `#B4EEE0` | `#4FAE9E` |
| Lightning | `#FFFFF0` | `#FFF8B0` | `#FFF06A` | `#E0B02A` |
| Poison | `#F8FFD8` | `#DCF48C` | `#B4E04A` | `#6B9420` |
| Metal | `#FFFFFF` | `#EAF2FA` | `#C4D4E8` | `#5E7290` |
| Water | `#F0FAFF` | `#B4DCFF` | `#6FB8F0` | `#2A5FC4` |

`ElementColors.GetColor` should return the **mid** of each row, so the existing tint-and-icon
call sites line up with the effect art instead of drifting from it.

### The player's spells carry no area markers

Decided 2026-10-23 from a playtest: *"remove aoe markers from abilities, their animations should
make it clear what the hitbox is."* A ring, disc, outline or preview drawn round what a player's spell
will hit, is hitting or is still hitting is a **marker**, and is not drawn. What shows the reach is the
effect itself, drawn out of the same number the hit tests:

- **A burst** throws its own substance - debris, spores, shards, sparks - out from the centre and the
  furthest of it stops on the hit radius (`scripts/ImpactDebris.cs`, the shared one). It is under way
  for a sixth of a second and gone in under half: an animation, not a ring.
- **A lasting zone** is the stuff that hurts, laid out to the radius: a cloud of puffs whose outer
  ring lands on the rim, flames filling a patch, a rift whose lip is the slow's edge, roots that end on
  the radius. No flat translucent disc under it and no stroked rim round it.
- **A trap** is an object whose edge is its trigger (Gravewell's bank is drawn at the trigger radius).
- **Pre-hit warnings** are the falling or swelling thing itself (a meteor in the sky, a fattening
  cap), never the area it will hit.

Not markers, and kept: **enemy telegraphs** (`GroundSlamAttack`, `AttackTelegraph`,
`TelegraphedGroundHit` - they warn the player and must stay), a status mark on one enemy (a carrier's
mark, a link's halo), and the stand-still gauge at the player's feet (`StanceGlow`), which shows a
state, not an area. A one-shot shockwave of the effect's own light at the moment of the hit that ends
on the radius (Arcane Explosion's sheet, Solar Flare's front, Riptide's foam) is an animation.

**Bonelight-specific caution.** The world is cold, so the warm elements (Fire, Light, Earth,
Lightning) pop hardest and the cold ones (Ice, Water, Metal, Wind) are competing with the
ground's own hue family. Those four need their cores held at full white and their edges pushed
*more* saturated than the warm rows — a pale blue effect on a deep blue floor is the one place
this tone can fail. Check any new cold-element effect against a Castle floor before shipping it.

## 4. Silhouette taxonomy

Bodies are naturalistic, so proportion alone will not separate seven enemy types — the audit
measured the current roster at 0.69–1.00 silhouette overlap, with `Enemy` and `SlowEnemy`
scoring 1.00 because they are literally the same sprite. The rule that replaces tinting:

> **Every enemy class owns one silhouette-breaking attachment that survives being filled solid
> black.** Fill the sprite with `#000` and you must still be able to name the class.

| Class | Body | The attachment that reads | Footprint in cell |
|---|---|---|---|
| Swarmer | small, hunched, narrow | pointed cowl, no visible limbs | 14×18 |
| Runner | lean, forward-leaning | a rag streaming *behind* — wide and low | 24×16 |
| Bruiser | wide, upright, heavy | horned helm plus pauldrons breaking the shoulder line | 26×28 |
| Caster | tall, narrow, vertical | a **detached** floating orb, offset from the head | 16×30 |
| Shielder | medium | a flat plane held to one side — straight edges against a curved body | asymmetric |
| Flyer | small | no ground contact; the `occ` shadow sits detached below | shadow gap |
| Elite | any base body | rim boosted and shifted to gold, plus a ground ring | unchanged |
| Boss | 96×96 | a silhouette that appears nowhere else — never a scaled-up basic body | 80×80 |

Two consequences worth stating plainly, because both contradict how the game works today:

- **`modulate` stops being a class identifier.** It becomes a status channel only: hurt flash,
  frozen, poisoned, elite. Telling a bruiser from a runner is the silhouette's job.
- **A boss may not be a scaled enemy sheet.** Elderbark is currently the orc at 5.6×, which is
  both the worst pixel-size offender in the audit and a boss with no identity of its own.

## 4b. Detail density

**Silhouette-first has never meant hollow, and reading it that way is a real failure this
document caused.** Four player studies drawn to §4 came back with clean outlines around dead
middles, because "the silhouette carries the read" was taken as licence to fill interiors with
one flat tone and shade only the two edge columns of each row. The silhouette carries the read.
It does not carry the whole drawing.

The measure is **internal edges per filled pixel**: how often the eye is handed a boundary inside
the figure rather than at its outline. Measured across real work:

| | edges/px |
|---|---|
| Shipping Bonelight enemies | 0.74 – 1.00 |
| A hand-drawn test sprite, on **seven** colours | 0.83 |
| The four flat studies | 0.46 – 0.55 |

**Density is internal shapes, not palette size** — seven colours beat seventeen in that table.
Treat **0.80 as a floor** for a new actor, and note the four things that buy it, in order of how
much:

1. **Separate every overlapping form with `occ`.** One dark pixel between a pauldron and the arm
   under it is worth more than five tones inside either.
2. **Carry four or five stops inside the mass**, not two at the edges. A fold is a light stop and
   a dark stop adjacent, in the middle of a shape.
3. **Asymmetric furniture.** A strap on one side, a torn hem on one corner, one broken rib.
   Symmetry halves the information for free.
4. **One hot pixel.** Against a body in the deep stops, a single emissive pixel does more than
   any amount of mid-tone.

**0.80 is a smell test, not a gate.** A shielder once scored **0.90 and read as a radiator** — it
got there by banding its torso with evenly spaced plates, and evenly spaced anything at 32px is a
stripe pattern. The metric counts boundaries; it cannot tell that repetition carries no
information. Where the number and the read disagree, the read wins.

## 4c. Who wears what

Two rules that between them decide every colour choice on a character, and both come from
`world-and-tone.md` rather than from taste:

- **Enemy bodies come from the DEEP half of their ramp.** The shipping shielder wore
  `steel.0 #DCE8F4`, the lightest steel in the palette, which is most of why the horde never read
  as the dark wizard's. Deep is the direction — but *invisible* is not: `stone.shade` and
  `stone.deep` sit within a few points of `occ`, and a figure drawn in them is a silhouette with
  nothing inside it.
- **One emissive pair per enemy, and its colour is a statement of origin.** Cold `arcane` if the
  thing was **taken**; `fire` if it was **built**. A construct is lit by whoever made it; a raised
  thing stares with the mark its maker left. That single distinction is worth more than any amount
  of per-enemy colour, and it is the only exception to the horde having no colour of its own.

**The player is the inverse of all of it** — saturated pigment, a lit face, and never the void
with two glowing points. That face is the mark of something taken, so putting it on the player
says the opposite of what the story means. Four wizards drawn as heroes read as villains until
the voids came off them.

## 5. Animation contract

One horizontal strip per animation, no padding, cell size from §1, and the figure anchored so
**feet sit on row `cell-2` in every frame** (row 30 for a 32 px cell), with the `occ` contact
line on row `cell-1`. A figure that bobs must bob its head, not its feet.

| Animation | Frames | Loop | Notes |
|---|---|---|---|
| `moving` | 8 | yes | |
| `attack` | 6 | no | |
| `hurt` | 2 | no | |
| `death` | 6 | no | **Mandatory.** `Enemy.StartDeath` frees the node instantly if it is missing |

## 6. Engine and import settings

Phase 0 of the migration is entirely these, and none of it needs art:

- `project.godot` → `rendering/textures/canvas_textures/default_texture_filter=0` (Nearest). It
  is currently unset, which means Godot's Linear default is bilinearly resampling every sprite
  in the game.
- `player.tscn` → `Camera2D.zoom = Vector2(1, 1)`. This also fixes the background parallax
  drift, because `node_2d_game.gdshader` tiles in `FRAGCOORD` space while `Node2DGame.cs` feeds
  it `world_offset = camera.GlobalPosition` — at zoom 0.9 the ground slides about 11% against
  the props standing on it.
- Every `AnimatedSprite2D` / `Sprite2D` scale → `Vector2(2, 2)`. No fractional scales anywhere,
  including the randomised prop scales in `BuildDecorProps`.
- `project.godot` → `window/size/window_width_override=720`, `window_height_override=1280`, so
  the `canvas_items` stretch factor is exactly 1.0. At the old 630×1120 it was 0.875, which put
  every sprite on a fractional device-pixel grid no matter what its scale was.
- Texture import stays lossless — `compress/mode=0`, `mipmaps/generate=false`,
  `process/fix_alpha_border=true`. These are already the values on disk; do not let a re-import
  change them.
- **Sub-1.0 scales are exempt until their art is re-authored.** Eight spell scenes shrink a
  72×72 sheet to between 0.24 and 0.9; nearest filtering *drops* pixels at those factors and
  looks worse than bilinear. Those nodes carry an explicit `texture_filter = 2` (Linear) and
  lose it in phase 4, when the effects are redrawn at a real cell size.

Moving the floor into world space on the actor grid — `LevelTilePainter.TileSize = 32` with the
`TileMapLayer` at scale 2, and retiring the `FRAGCOORD` background shader — is **phase 3 work,
not phase 0**. It only pays off once the tiles are redrawn at 32×32; doing it against the
current 48×48 tiles would just resample them differently. Phase 0's `zoom = 1.0` already fixes
the parallax drift on its own, because `world_offset` then matches the world 1:1.

## 6a. Pixel art in menus: whole SCREEN pixels, not whole canvas units

The window-size override in section 6 no longer holds the stretch at 1.0. A PC runs a 1280×720
canvas (`DisplayProfile`, `canvas_items`, `keep_height`) in a 1600×900 window by default, so the
stretch is ×1.25 before anyone resizes anything; full screen it is ×1.5 at 1080p and **×2.1375 on
a 2736×1824 screen** (letterboxed to 2736×1539). A whole scale on the *canvas* is then a fraction
on the *screen*: x2 art lands on 4.275 screen pixels per art pixel, and nearest filtering draws
that as a mix of 4- and 5-pixel columns.

That unevenness is everywhere on a fractional window - the title art measures as 4- and 5-pixel
runs at 2736, the stage banners as 9 and 10 - but it is not blur: those images keep their palette
(36 colours on the title, 4 on a banner). **Blur came from one place**, the character select, which
drew each wizard into a `SubViewport` at canvas resolution. A `SubViewport` has its own default
texture filter, **linear**, not the project's nearest, so the wizard was bilinearly resampled at x3
inside it, and the window then magnified those 160 soft pixels by the stretch again. A crop of the
Pyromancer at 2736 held 1321 colours from a 23-colour sprite.

**The rule for a sprite shown in a menu:** draw it straight onto the canvas with nearest
filtering, at a whole number of *screen* pixels per art pixel. `PixelArtRect` does this: it reads
the screen pixels one canvas unit covers (`GetViewport().GetFinalTransform()` times the control's
canvas transform), takes the whole number nearest the scale it was laid out for (capped to fit its
box), draws the art `n / stretch` canvas units per pixel and snaps the corner to a screen pixel.
At 2736 the card wizard is 6 screen pixels per art pixel (36 colours in the same crop, every run of
sprite pixels a multiple of 6), at 1080p 5, in the default window 4; the detail pane's x5 is 11,
8 and 6.

- **"Higher resolution on a bigger screen" means a bigger whole scale of the same 48×48 art**,
  never a redrawn or resampled sprite. No portrait is a downscale of a larger source: every wizard
  sheet is authored at 48. The one baked-in upscale was the unknown-figure fallback, a 48 cell
  saved at x2 like the props, which would have drawn twice the size of the wizards beside it;
  `props_fx.py` now writes the portraits at 1:1.
- **Never put menu art through a `SubViewport`** without setting its
  `CanvasItemDefaultTextureFilter` to nearest *and* sizing it in screen pixels; it is easier not to.
- On a phone (720 wide, ×1.5 on a Pixel 9) the card wizard works out at 5 pixels per art pixel,
  filling its box exactly - computed, not yet seen on the device. The desktop phone check at
  540×1212 is ×0.75, where only x2 fits - smaller than the phone, and correct.
- Not done, and worth knowing: the title art and the stage banners are still x2 / "cover" on the
  canvas and therefore uneven on a fractional window. The title cannot simply snap: its menu is
  pinned to rectangles in the art (`MainMenu.PinToWideArt`, `.ai/title-screen.md`), so its scale
  is a layout decision, not a filter fix.

## 7. The per-stage light shader

The one piece of new code the direction needs, and the last thing to build — everything before
it reads correctly under the reference (Castle) lighting. A canvas shader on actors and floor:

```
uniform vec3  rim_tint;      // replaces #DCE8FF, and #A8B8DC at its own ~80%
uniform float rim_boost;     // 1.0 normal, ~1.6 for elites
uniform vec3  ambient_tint;  // replaces #232C46 and biases the shade/deep half of each ramp
uniform vec3  pulse_color;   // additive, set on a nearby cast
uniform float pulse;         // 0..1, decays over ~0.15s
```

It is a **palette swap keyed on exact colour match**, which is why `#DCE8FF` and `#232C46` are
reserved. **Emissive slots are exempt** — gold, arcane cyan and every element ramp pass through
untinted, because they are light sources rather than lit surfaces. A stage that tints the fire
is a bug.

Per-stage values, with Bonelight as the reference:

| Stage | `rim_tint` | `ambient_tint` | Intent |
|---|---|---|---|
| Castle | `#DCE8FF` | `#232C46` | the reference — moonlight on stone |
| Ruins | `#E8DCC0` | `#2E2A3E` | warmer dust, cold fill |
| Forest | `#D8F0C8` | `#1E3028` | green-gold dapple, Fenlight-adjacent |
| Swamp | `#C8E08A` | `#1E2A24` | full Fenlight — sickly and saturated |
| Ice | `#EAF6FF` | `#26344E` | Cold Ash-adjacent, chroma drained |
| Desert | `#FFE8C0` | `#3A3038` | the one stage where the key is warm |
| Volcanic | `#FFA060` | `#2E1626` | warm rim, blood ambient |

`StageEnvironmentCatalog` gains these two colours per profile and stops carrying a
`BackgroundModulate` that tints the whole floor sheet.

The three tones that lost the study are kept as stage identities rather than discarded:
**Fenlight** is the Swamp, **Cold Ash** is Ice and a candidate for a late-difficulty overlay
where the world drains as the run degrades, and **Candle Ward** is the warm first stage — the
only tone of the five that reads as somewhere you would choose to stand, which makes it useful
exactly once. **Bleachbone** is shelved: a bright floor and a white-hot spell core are the same
value, so the fire impact measured 1.21:1 there. Reviving it would require dark-cored effects,
which breaks the white-core rule the twelve elements depend on.

## 8. Migration order

Ordered so the game looks *better*, never worse, at every stop — and so the 240 hardcoded
`Rect2` atlas regions get re-cut in the largest useful batches rather than one sprite at a time.

| Phase | Work | Regions touched | Why here |
|---|---|---|---|
| 0 | Engine settings from §6 — **done 2026-09-03** | 0 | Fixes the blur and the parallax drift immediately, and improves the *existing* art |
| 1 | One production test sheet: the player plus one enemy, fully animated, under Bonelight | 0 | The direction's real risk is whether 28 px of figure holds five tones, a rim and an attachment across eight frames. Learn that on one sheet, not on 129 regions |
| 2 | Three basic enemy sheets → 32×32, and **split `EnemyFrames.tres`** so `SlowEnemy` stops sharing `Enemy`'s sprite | **129** | The biggest single batch, and it retires the 1.00 silhouette overlap |
| 3 | Six floor tiles → 32×32 under one light, **and** the floor into world space (`TileSize = 32`, `TileMapLayer` scale 2, `FRAGCOORD` background retired) | 0 | Biggest perceived change for the least engine risk; the world-space move only pays off once the tiles are 32×32 |
| 4 | Projectile and impact VFX → 32/64/96 cells; break the shared sheets | **57** | Ten `.tscn` files; ends fireball-serves-three-spells |
| 5 | Orc, soldier, and **Elderbark on its own 96×96 sheet** | **44** | Retires the 5.6× scale, the worst grid offender |
| 6 | Props, hazards, pickups; XP orb gets a purpose-drawn 16×16 emissive sprite | ~4 | Stops world decor doubling as projectiles |
| 7 | The rim/tint shader and the per-stage light table | 0 | Only worth building once the art it recolours exists |
| 8 | Icons — 12 spell paths in `Player.cs`, 24 relic paths in `ChestItemCatalog`, the interpolated GUI ranges | 0 | Pure 1:1 swaps, no geometry; `RegressionChecks.ValidateChestItemIcons` guards collisions |

Phase 2 is also where wave tuning has to be revisited, per the size note in §1.

## Don't

- Put an outline on an actor. The rim and the `occ` contact line do that job.
- Leave an interior flat. See §4b — a clean outline around a dead middle is the failure mode this
  document used to encourage.
- Dress an enemy in a player pigment row, or a player in the enemy's dark-void face.
- Regenerate a `.tres` without preserving its `uid`. Scenes reference resources by uid as well as
  by path, and dropping one does not fail cleanly — the game boots, spawns, and then dies in a C#
  finalizer with `gchandle.is_released()`, which reads as a Mono bug and is really a dangling
  reference. `tools/art/spriteframes.py` handles this; anything else that writes a `.tres` must
  too.
- Use `#DCE8FF`, `#A8B8DC` or `#232C46` as pigment — the shader keys on them.
- Write an actor's frames around `tools/art/matte.py`. A near-white glint on a dark surface, or a
  full white rim against an actor's darkest stops, is what made the cast read wet (§2, "Matte, not
  wet"); `pixel.write_strip` finishes any palette carrying the rim, and a renderer of its own (the
  familiar, the Boo) calls the finish itself.
- Make `shade` a desaturated `base`, or let any tone fall below ~0.20 saturation. Bonelight is
  dark, not grey.
- Tint an emissive slot per stage. Gold, arcane and the element ramps are light sources.
- Use `modulate` to tell one enemy class from another. Status only.
- Ship a non-integer `scale`, or a cell size that is not in the §1 table.
- Scale a basic enemy sheet up into a boss.
- Separate a detached attachment from the body with `occ`. `occ` is a drawn pixel, so an
  orb joined to its hand by a line of it looks detached in colour and is solid under the
  fill-it-black test in §4 — the attachment stops reading as detached at exactly the size
  where that matters. Only transparency separates two masses in a silhouette.
- Reuse a world prop as a projectile, or a UI icon as a world effect. Both happen today
  (`crystal4` is the XP orb *and* FrostShard *and* ThornVine; the sun-strike icon is
  ScorchingRay's world effect) and both are why art cannot be replaced one sprite at a time.
- Add a material colour without adding its five-tone row to §3.
- Draw a ring, disc or preview round a player spell's hit area (§3, "The player's spells carry no
  area markers"). Draw the effect out to the radius it tests instead. Enemy telegraphs are exempt.
