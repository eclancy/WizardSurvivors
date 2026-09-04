# Art direction — Candle Ward, Bonelight tone

The chosen direction, decided 2026-09-03 after an audit of the existing art, three drawn
direction proposals, and a five-option tone study at the chosen pixel density. This file is the
contract: the grid, the light model, the palette, the silhouette rules, the animation and import
settings, the per-stage light table, and the migration order.

`.ai/art-inventory.md` still describes what we own. This describes what replaces it.

**The one-line version:** every material gets a plain albedo, and a single shared light model
does the colouring — a cold key from the upper left, a deep indigo ambient in shadow, and a
1 px bone-white rim on the key-facing edge of every actor. Colour is a *lighting* variable, not
a pigment one, so a stage changes room through two shader uniforms instead of a repaint.

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
| Actor (player, basic enemies) | 32×32 | ~26–29 px tall | ×2 | ~52–58 px |
| Elite / large enemy | 48×48 | ~40 px | ×2 | ~80 px |
| Boss | 96×96 | ~80 px | ×2 | ~160 px |
| Floor tile | 32×32 | full | ×2 | 64 px |
| Prop (small / large) | 32×32 / 64×64 | full | ×2 | 64 / 128 px |
| Projectile | 32×32 | varies | ×2 | 64 px |
| AoE / impact | 64×64 or 96×96 | varies | ×2 | 128 / 192 px |
| Pickup (XP, health) | 16×16 | full | ×2 | 32 px |
| UI icon | 32×32 | full | ×2 | 64 px |

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
  Colour `#DCE8FF`.

`#DCE8FF` and `#232C46` are **reserved**. They may not be used as pigment anywhere in the
project, because the per-stage shader (§7) keys on an exact colour match to retint them.

### Every material is five tones

| Slot | Construction | Rule |
|---|---|---|
| `hi` | albedo × key, brightest, hue rotated **+8 to +12° toward cyan** | Sparingly — a highlight, not a plane |
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

## 3. Palette — Bonelight

Shared light:

| Role | Hex | Notes |
|---|---|---|
| occlusion / contact | `#05070C` | shared by everything |
| rim (reserved) | `#DCE8FF` | never pigment |
| key light colour (reserved) | `#DCE8FF` | shader-side |
| ambient light colour (reserved) | `#232C46` | shader-side |

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

That is **fifty-five material colours plus four lights**, against the 935 the current art
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

## 7. The per-stage light shader

The one piece of new code the direction needs, and the last thing to build — everything before
it reads correctly under the reference (Castle) lighting. A canvas shader on actors and floor:

```
uniform vec3  rim_tint;      // replaces #DCE8FF
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
- Use `#DCE8FF` or `#232C46` as pigment — the shader keys on them.
- Make `shade` a desaturated `base`, or let any tone fall below ~0.20 saturation. Bonelight is
  dark, not grey.
- Tint an emissive slot per stage. Gold, arcane and the element ramps are light sources.
- Use `modulate` to tell one enemy class from another. Status only.
- Ship a non-integer `scale`, or a cell size that is not in the §1 table.
- Scale a basic enemy sheet up into a boss.
- Reuse a world prop as a projectile, or a UI icon as a world effect. Both happen today
  (`crystal4` is the XP orb *and* FrostShard *and* ThornVine; the sun-strike icon is
  ScorchingRay's world effect) and both are why art cannot be replaced one sprite at a time.
- Add a material colour without adding its five-tone row to §3.
