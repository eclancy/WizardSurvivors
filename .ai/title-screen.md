# Title screen — "Vigil"

The shipping title screen and everything you need to change a detail of it without rediscovering
how it is put together. Decided 2026-09-04 from a set of seven drawn candidates; the other six are
still in the generator and are one line away from shipping instead.

**Read `.ai/art-direction.md` first.** This screen is an application of that contract, not an
exception to it: 1:1 authoring, a whole ×2 render scale, nearest filtering, and nothing on frame
that is not one of the 50 material tones, 4 lights or 12 element ramps in §3.

## What ships

| File | What it is |
|---|---|
| `assets/bonelight/ui/title-screen.png` | 360×640 artwork, drawn at ×2 into the 720×1280 viewport |
| `assets/bonelight/ui/title-prompt.png` | 360×20 transparent strip, PRESS ANY KEY only |
| `scenes/TitleScreen.tscn` | ground + artwork + prompt |
| `scripts/TitleScreen.cs` | prompt fade and breath, music, input |
| `tools/art/splash.py` | generates both PNGs, plus the six unused candidates |
| `tools/art/hero.py` | the title-size player figure |
| `tools/art/splashkit.py` | trees, boulders, candles, wordmark, sigils, skull, moon |
| `tools/art/raster.py`, `tools/art/font.py` | drawing primitives and the two pixel faces |

Regenerate everything with **`python tools/art/splash.py`** (Python 2.7 + PIL, the only Python on
this machine). It writes the shipping PNGs *and* the previews under `tools/art/_splash/`, which is
gitignored — the candidates live as code, not as files, so a palette change re-renders all seven.

The old `assets/Wizard_Survivors_Title_Screen.png` is **still referenced** by `MainMenu.tscn` and
`StageSelection.tscn` and cannot be deleted yet. See the manifest for that job.

## Why the scene is built the way it is

The bug being fixed: the old art was a 1024×1024 square on a `TextureRect` with
`stretch_mode = Keep`, which means *draw at native size*. In a 720×1280 portrait viewport it
anchored top-left, ran 304px off the right edge, and left a 256px dead band below.

The replacement deliberately does **not** anchor the artwork to the viewport. `TitleImage` is a
fixed 720×1280 rect centred on a `Ground` `ColorRect` painted `#05070C`:

- The project stretches `canvas_items` with `aspect = keep_width`, so canvas width is always 720
  but canvas *height* varies with the device. Anchoring the image full-rect would scale it by a
  fraction vertically, and fractional scaling of pixel art is the exact failure the whole art
  direction exists to prevent.
- A fixed rect means the scale is always exactly ×2. A taller phone letterboxes, and because the
  top and bottom of the composition are near-black, the letterbox is invisible.

`TitleScreen.cs` used to call `FantasyGuiSkin.ApplyFullscreenBackdrop` with a stock fantasy-GUI
plate at 0.96 opacity over the entire screen, plus a second plate on a `Prompt` node that did not
exist. Both calls are gone. **Do not reintroduce a skin pass on this scene.**

## Composition and draw order

Draw order in `vigil()` is load-bearing; three of the four bugs found while building this were
order bugs. Back to front:

1. Sky gradient (`vramp` 0→430) and starfield.
2. **Far wood** — eighteen trees on the horizon line at y=436. Each tree, each clump of
   undergrowth and each leaf mass picks its own pair of near-blacks from `FAR_TONES` or
   `NEAR_TONES`. A dark mass painted in one colour is a hole; six near-blacks that differ by a
   few points of *hue* read as depth, because the eye takes the variation as things at different
   distances. Far is a step lighter and cooler than near — atmospheric perspective, the only
   depth cue left once everything in shot is silhouette.
3. Eyes in the dark (y 360–424) and ground mist.
4. Ground: flagstone bands that widen toward the camera, from y=428.
5. **Grass**, thickening toward the camera and thinned inside the ward circle so the runes still
   read through it; then **brush banked up both edges** of the clearing floor.
6. **The creep** — off in the shipping preset, and it is worth knowing why: filling the lawn cost the composition its depth. With the wood brought down to his feet there was no distance left between the trees and him for the eye to read, and no amount of size or tone gradient inside that band bought it back. It is foliage growing out of the treeline down onto the lawn, plus six saplings
   rooted on it. This has to be its own pass *here*, after the floor: the flagstones are drawn
   over the whole treeline block, so anything placed below y=428 up there is silently destroyed,
   which is why raising the treeline counts could never make anything encroach. It is the only
   foliage in the frame actually standing on the grass, and it keeps a hard exclusion ellipse
   around the ward.
7. Boulders.
8. **Near wood** — two trees at the frame edges in flat `occ`, mostly cropped. These do most of
   the atmospheric work: they put the viewer *inside* the wood looking out at the clearing.
9. **Overhead canopy** — six boughs reaching in from off the top corners, then a band of loose
   leaf mass from y=0 to y=344 filling the sky around them. `CLEAR` in `vigil()` is the single
   hole left in it, and it exists to hold the wordmark: **move the wordmark and you must move
   `CLEAR` with it.**
10. **The ward circle**, on the `WARD` ramp — the brightest thing in the frame after the orb, and
   the reason the composition works. There are no candles on it any more: nine orange flames
   around a gold-white ward put two warm light sources in the same place competing for one job.
11. The figure.
12. Motes lifting off the ring, after him so some drift in front.
13. **Foreground grass and brush** in flat occlusion across the bottom strip, held out of an
    ellipse around the ward. It is the nearest thing in the frame so it gets no light at all,
    and a plane the figure stands behind is what turns a backdrop into somewhere he is.
14. Vignette.
15. Wordmark.

## The knobs

| To change | Where |
|---|---|
| Which of the seven screens ships | index into `made[]` in `splash.py: main()` |
| Tree placement, height, canopy spread | the `far` and near-pair lists in `splash.py: vigil()` |
| Canopy density and raggedness | `density` arg, and `_clump` in `splashkit.py` |
| The hole in the canopy | `CLEAR = (cx, cy, rx, ry)` in `vigil()`; its lobing lives in `canopy()` |
| Overhead boughs | the bough list in `vigil()` — origin, angle, length and width per limb |
| Grass density and reach | the two grass loops in `vigil()`; one blade shape in `grass()` |
| **How closed-in the wood is** | `wood=` on `vigil()`. `open` **ships**; `thicket`, `rank`, `walls` and `current` are one word away. `python tools/art/woods.py` renders them |
| How broken the horizon line is | `humps` — a couple of dozen taller clumps along the treeline. **Raising `under` only moves the flat line up; a horizon is broken by a few things standing above the rest, not by density** |
| How far the wood comes down the lawn | `creep`, `reach`, `cbias`, `cedge` and `keep` in the chosen preset. **`cbias` above 1 pushes clumps up against the treeline; below 1 pushes them down into the ward keep-out, where they are thrown away and the middle of the band stays bare however high `creep` goes** |
| How enclosed the clearing feels | the brush loops in `vigil()` — treeline, floor edges, then the creep |
| How dark it is under the brim | `_hood()` in `hero.py`, and the `_hat_shadow()` call at the foot of `_face()` — move the skip rect in `wizard_hero` with them |
| **Which beard he wears** | `beard=` on `hero.wizard_hero`, and the default on `splash.vigil()`. Eight styles; `python tools/art/beards.py` renders them all into the screen |
| Hand and finger size and tone | `_fingers` / `_arm` in `hero.py`; tones from the `flesh` ramp |
| Branch forking, lean, hanging strands | `tree()` in `splashkit.py` |
| **The colour of all wizard magic** | `WARD` at the top of `vigil()` — one name moves the orb, the rings, the motes and the light on the figure together |
| Ward circle size and position | `cxp, cyp` and the radii in `vigil()` |
| How bright the ward burns | the rings, star nodes and rune ticks in `vigil()`, all on `WARD` |
| The six flames on it | `ward_flames()` in `vigil()` for height and depth-sort; `flame()` in `splashkit.py` for shape |
| How many things are watching | the two `eyes_in_the_dark` calls — one in the treeline, one up in the branches after the canopy |
| How far its light spreads on the floor | the three `radial` pools, on the **violet material** row |
| How much it lights the figure | `under_ramp=` on `wizard_hero`, and `_underlight` in `hero.py` |
| Figure size | `hero.wizard_hero(c, cxp, cyp, h=150)` |
| Figure proportions | `wizard_hero` in `hero.py`, all in units of `u = h/32` |
| Robe colour | `robe="wool"` — any key in `bonelight.MATERIALS` |
| Orb / staff colour | `staff_ramp=` — any ramp in `bonelight.ELEMENTS` |
| Wordmark position and tone ramps | `sk.wordmark(...)` at the foot of `vigil()` |
| Prompt wording and face | `prompt_asset()` in `splash.py` |
| Prompt timing and resting opacity | `[Export]`s on `TitleScreen.cs`, and the `0.42f` in `FadeInPrompt` |
| Prompt position on screen | the `Prompt` node offsets in the scene: `-48` / `-8` from the bottom of a 1280-tall rect maps to artwork rows 616–636 |

## The figure

`hero.py` is a separate construction from the 32×32 in-game sprite, on purpose: at 32px the
silhouette is the entire design, and at title size it is only the starting point. Four things
carry the read, and all four were arrived at by getting them wrong first:

1. **The staff is taller than the wizard** and ends in a forked claw, so it sets the top of the
   silhouette and is read first.
2. **Proportion**: 34 units tall, 13 wide at the hem, about 2.6:1. The first attempt was nearer
   2:1 with a hat brim wider than the body, which reads as a chess piece.
3. **Value**: he is unlit on the camera side. The fill is `base`/`shade`/`deep`; separation comes
   from the bone rim, the orb bounce and the gold staff. Filling the robe from the *bright* end of
   the ramp is most of what made the first version look like a mascot.
4. **Asymmetry**: a cloak swept off-axis, a mantle layered over the robe.
5. **A jointed arm**: shoulder, elbow, wrist, drawn as two tapering capsule runs with a slightly
   wider disc at the joint. The first version was one straight quad from body to wrist — no
   joint in it anywhere and four units thick — and it read as a plank laid across him. It is
   drawn **over the robe but under the mantle**: on top of every layer he wears, it read as an
   arm laid over the outside of his clothes.
6. **Warm light, cold world.** `WARD` is the Light element ramp, not Arcane. Everything around
   him is cold blue-grey stone and violet, so a purple ward sat in the same hue family as its
   own background and had to shout to be seen. Gold-white is the only high-contrast option the
   palette offers, and it reads as *protective* rather than merely magical. Blue and green were
   both considered and both lose to the ground they sit on.

**The face is a shadow, a nose and a beard — and nothing else.** `_hood()` puts a band of `occ`
under the brim and that band stays unlit: it is handed to `_rim` and `_bounce` as a skip rect, so
neither pass can put a bone edge or a gold bounce on the one part of the figure whose whole job
is to be dark. It reaches from the brim underside down to −21 units, well past the nose, and its
bottom two units dither through robe `deep` and `shade` onto the mantle rather than stopping on a
rule. **The skip rect and the shadow have to move together** — deepen one and leave the other and
the extra shadow starts taking an edge light again. Out of the bottom of it comes a nose in `flesh` and a grey beard in
`skin`, and that is the entire face. There are no eyes and no mouth; at this size they are three
dark specks in a void, which is what made an earlier front-facing head read as a mask.

Both are **drawn as stylised things, not simulated ones**, and that is the single lesson of the
whole face. Three earlier beards — five tapering locks, then a field of scattered tufts, then a
bushier field of the same — all tried to be the real object, and all spent their detail on
texture the eye cannot resolve at 150px while leaving the silhouette soft. What carries at this
size is shape and value break, so that is all either of them spends anything on.

The nose is **a small sharp wedge in one tone** — three points, a straight taper to an apex on
the centre line, three pixels across and five tall. It has been round twice and pointed twice; at
this size the only property that survives is whether it ends in a point or does not, because a
bulb three pixels wide has no room to read as round. It also gets **one** colour plus two edge
lines: a body disc with a lit cap and a shaded spot in it is three colours fighting over a shape
the eye reads as one. The brim's shadow supplies almost all of the modelling.

The beard is `skin`, the bone/pallor row — white hair and old bone are the same material in this
palette. It is **eight named styles sharing one renderer**, and `beard=` on `wizard_hero` picks
one. They are genuine alternatives, not one shape with knobs on: the silhouette is the whole
design at this size, so a style that differs by a couple of units of width is not a style.

| Key | Shape | |
|---|---|---|
| `cascade` | broad straight fall past the belt, 13.4u | the only one that makes him taller rather than wider |
| `storm` | blown 3.4u off the vertical, like the cloak | power in use rather than at rest |
| `bound` | gathered in two gold bands, 12.2u | the only one that puts metal on him below the staff |
| `mane` | one undivided mass, 10.6u wide | primal; sits right on the width ceiling |
| `fork` | a mass splitting into two weighted points | the most legibly arcane |
| `mane-spear` | **ships** — mane held wide, then a long taper to a point | heavy first, sharp second |
| `mane-blade` | widest highest, straight sides to a chisel point | edged, the most predatory |
| `mane-talon` | mane hooked off the vertical in its last third | a claw rather than wind |

Render every style into the screen with **`python tools/art/beards.py`** (or name a few:
`beards.py mane-spear mane-blade`). It writes to `tools/art/_beards/`, which is gitignored, and
it never touches the shipping asset — only `splash.py` does that.

`_rows()` turns a half-width profile plus an optional sweep into a stack of spans, and **both the
outline and the texture come off that same list**, so feathering always lands on the edge and
streaks always land inside it whatever the profile is doing. Everything below is enforced in
`_mass()` and applies to all eight:

- **Length is most of the read.** The shipping beard runs twelve units, past the mantle hem and
  down to the belt. The earliest versions were about half that, and a half-length beard reads as
  a full beard rather than as a wizard's.
- **Width has a ceiling, and it is the length.** Taken out until it is as wide as it is tall, any
  of these stops being a beard and becomes a ball. `mane` sits closest to that line deliberately.
- **No ruled partings down the front.** Tried at four, eleven and thirteen across, in occlusion
  and in `deep`, ragged and even; at every count and every weight they read as ruling on a
  surface. Five fat capsules instead read as dreadlocks, and a field of ringed discs as bubbles.
  The mass is one dithered ramp, and the hair is short scattered flicks — under a unit and a
  half, because **length is what turns a streak into a parting**.
- **No keyline round the outside either.** A continuous `occ` line all the way round the profile
  reads as a shield boss hung on his chest: hair has no outline, it has an edge that breaks up.
  The edge is short flicks instead, each leaving the profile by a quarter to half a unit — at a
  unit and a half they are spines and the beard is a hedgehog.
- **The moustache is one tapering `_capsule` run per side**, in the beard's own values, with its
  own feathering. Every pale, smooth version read as two slabs laid across the face; a smooth
  shape lying on a hairy one never joins it.
- **A capsule cannot come to a point**, because the smallest disc it draws is a pixel across and
  the end is always a soft cap. `tip=` on `_must_run` carries a hard needle past the end by hand,
  and `feather_span=` keeps the flicks off the outer 40% so they do not soften it back. Those two
  arguments are the whole difference between a moustache that thins and one that ends.

**The shipping beard starts low.** `mane-spear` opens at −22.35 rather than the −23.60 the rest
of the set shares, so it hangs from *under* the moustache instead of running up past it to the
cheekbones; its bottom drops the same amount, so the length is unchanged. What that buys is dark.
The band the cheeks used to fill is hood shadow now, and the face reads as a moustache and a nose
coming out of a void rather than as a head of hair filling the brim.

- **`_hat_shadow()` is a pass, not a shape.** The nose and moustache are drawn after the hood void
  and over it, so without it they come out fully lit inside a shadow meant to be swallowing the
  top of the face. It walks pixels and steps each one down its own material row, dithering the
  boundary — and its map holds only `skin` and `flesh`, which is what keeps it off the hat and
  the robe. One step, not two: at two the nose went black.
- It runs from −23.70 to −21.60 — fully dark above, clear below, dithered across two units in
  between. Both ends have moved down twice now, and the fade is deliberately long: over a short
  span the shadow is a step, and over two units it is something you watch happen across the nose
  and the top of the moustache. **Shrinking the nose is not what keeps it quiet** — lowering this
  is. Reach for these two numbers before the nose geometry.

There is also **no clasp at the throat** any more. A gold disc sat there, and under the hood
shadow with nothing else below it, a warm rounded shape at chin height reads as a chin.

**The hand is the only bare skin, and it uses `flesh`, not `skin`.** The row called `skin` in
this palette is bone — cold and moon-pale, what the skulls and the moon are made of — and a hand
painted from it reads as a gauntlet. `flesh` was added to the contract for this. Beside the gold
shaft it is one of only two warm notes on the figure, the other being the sash.

The hand is the fiddliest part. The arm, cuff and back of the hand are drawn **before** the staff
and only four fingertips and a thumb come round the near side after it — he grips the shaft
rather than holding it up in front of himself. Fingers are drawn as chains of discs (`_capsule`),
never polygons: at this size four right angles reads as a brick. They are four different lengths
and thicknesses, and the thumb crosses the stack and **stops on the shaft**: laid parallel it was
a fifth finger, and run past the far side of the fingers it was one again.

## Four bugs worth not reintroducing

- **The vignette ran last**, so it dithered frame-edge darkening straight over the wordmark. Two
  candidates were unreadable because of it. The vignette is scene light: it runs before the type.
- **The rim and bounce passes were silent no-ops.** Both find the silhouette edge by testing
  alpha, and the scene canvas is opaque, so every pixel passed. The figure is drawn on its own
  transparent layer and composited; anything else that needs an edge pass must do the same.
- **`poly_shade`'s gamma reads backwards.** Colours run light-to-dark along the axis, so gamma
  *below* 1 biases toward the dark end. Two rounds of "darken this" brightened it instead.
- **The staff's leather grip sat at exactly the hand's height**, so its stripes ran across the
  fingers and it read as a candy cane in a fist. The bindings are now clear of the grip.
- **A clean elliptical hole in the canopy reads as a vignette**, not as a gap in leaves. The
  clearing edge is perturbed by three sine harmonics in `canopy()`; without them the effect gives
  itself away at a glance.
- **Emissive ramps belong on the emitter, not on what it lights.** The ward's ground wash was
  first painted on `E["arcane"]` like the rings themselves, and it filled the floor with a flat
  slab of saturated purple that read as a rug. What the ward lands on is stone, so the wash is
  built from the **violet material** row and only the rings are allowed to emit.
- **A figure standing in a light still blocks it.** Painting the ward back over the near lip of
  his contact shadow — to "soften" it — punched a visible hole in the floor. The shadow stays
  `occ`; what it needed was a dithered edge, not a brighter middle.
- The ward sits at `cyp = 568`, not 578, purely so its lower arc clears the PRESS ANY KEY band at
  rows 616–636. Brightening the circle is what made that collision matter.
- **A flame is a body with a thin core, not a bloom with a body inside it.** The first version
  put one filled ellipse of the ramp bottom behind each flame and gave the core half the body
  width; both read as a glowing ball with a tail. The bloom is now two stages — a wide dim halo
  in *pigment* gold and a tight emissive one — and the core is 30% of the body width.
- **A shadow that takes an edge light stops being a shadow.** The hood void was a flat `occ` quad
  wider than anything around it, so in those rows it *was* the outer silhouette and both edge
  passes obligingly lit it — a bone rim and a gold bounce on the darkest part of the figure. Both
  passes now take a `skip` rect; keep it around `_hood`. Anything else meant to read as absence
  needs the same treatment.
- **Eyes are cheap and it is easy to overspend them.** 34 pairs with an amber core turned the
  wood into a firefly meadow. Nine in the treeline and seven in the branches, a step down the
  fire ramp so they read red, is the amount that reads as *watched*.

## The animation

**Built.** `python tools/art/anim.py` writes the layer set to `assets/bonelight/ui/title/`;
`scenes/TitleScreen.tscn` stacks it and `scripts/TitleScreen.cs` drives the frames. The flat
`splash.py` render is untouched — `vigil()` with `layers=False` still produces the shipping
`title-screen.png` byte for byte, and it is worth keeping that true: it is the only cheap check
that the layer split has not drifted from the composition.

**To verify a change:** composite the layers in the order below and diff against
`title-screen.png`. It must be **zero** differing pixels. That check is what caught the eye-depth
bug below at 44 pixels out of 230,400 — far too few to spot by eye, and completely wrong.

Four behaviours, in the order they matter:

1. **The eyes in the dark blink.** Occasionally, and not together.
2. **The magic pulses.** Lightly and slowly — a breath, not a strobe.
3. **The six flames burn.** They are currently a still frame of something that should be moving,
   and they are the most obviously frozen thing on screen.
4. **The edge light on the wizard flickers with the flames.** Not on its own clock.

### The rule that governs all four

**Animate by frame, never by tint.** `modulate`, alpha fades and cross-dissolves all blend
colours, and a blended pixel is by definition not one of the 66 in the contract — the palette
audit that `splash.py` runs would stop meaning anything the moment anything on this screen
breathes with an alpha ramp. Every state of every animated thing is a *generated frame* with a
closed palette, and the scene swaps between them. That is also why the prompt is the one thing on
screen that is allowed to fade: it is bone-white type on near-black, alone on its own layer, and
nothing behind it can be dirtied.

The corollary is that `splash.py` grows a frame-emitting mode. Nobody hand-animates any of this;
the generator already knows how to draw every piece, and a flame at a different seed is a
different frame for free.

### What it costs: the screen has to come apart into layers

One baked 360×640 PNG cannot animate a part of itself. The split, back to front:

| Layer | Frames | Driven by |
|---|---|---|
| `back-a` — sky, far wood, undergrowth, leaf mass | 1 | — |
| `eyes-far-*` — the treeline pairs | 6 | blink timer |
| `back-b` — mist, ground, grass, creep, near trees, canopy, wordmark | 1 | — |
| `eyes-near-*` — the branch pairs | 6 | the same blink index |
| `ward-*` — pools, rings, hexagram, runes | 3 | `WardPulseSeconds`, walked 0,1,2,1 |
| `flames-far-*` | 4 | `FlameFrameSeconds` |
| `figure-*` — the whole figure, rim and bounce stepped down | 4 | **the same index as the flames** |
| `flames-near-*` — near flames and the motes | 4 | the same index again |
| `fore` — foreground grass and brush | 1 | — |

Thirty PNGs. The pulse is carried by the **size** of the ward light pools and by whether the
hottest tone survives on the rune ticks; the flicker steps the rim from `RIM` down through two
bone tones and shifts the bounce and underlight one index toward their dark end. Nothing anywhere
is tinted or faded.

Three things that are not obvious from that table:

- **The flames straddle the figure.** `ward_flames(False)` draws the far ones, then the figure,
  then `ward_flames(True)` draws the near ones. Any animated flame layer is therefore *two*
  textures with the figure sandwiched between them, and both have to advance on the same clock or
  the near and far flames of one ring will be visibly out of step.
- **The vignette survives the split, but not for free.** It is a pure per-pixel function of
  position, so applying it independently to each layer gives the same result as applying it to
  the composite — *provided it skips transparent pixels*. It writes a colour chosen by position
  rather than multiplying what is already there, so unguarded it paints an opaque dark frame into
  the empty corners of every transparent layer. Hence `only_opaque=` on `raster.Canvas.vignette`.
  It does not work as a top overlay either, because that would need alpha. It must also only ever
  make a pixel **darker**: `stone.deep #0B0F18` is lighter than `occ #05070C`, so an unconditional
  write replaced 63% of the pixels in a corner sample with a mid-dark grey, which read as a filter
  laid over the whole image and flattened the contrast of exactly the areas meant to be deepest.
- **The two eye sets are at different depths, and the backdrop has to split around them.** The
  treeline pairs are drawn early and are then legitimately covered by the ground, the creep, the
  near trees and the canopy; the branch pairs are drawn after the canopy and sit in front of it.
  Lift both onto one layer above a single backdrop and the treeline pairs shine straight through
  the foliage that is meant to be in front of them. Hence `backA` / `eyesA` / `backB` / `eyesB`.
- **Every layer must be fully opaque or fully transparent, never in between.** Partial alpha
  anywhere reintroduces blending, and with it off-contract colour.

### The fade band is the whole game

Every soft edge in this project is a dithered ramp, because the palette has no intermediate
values and no alpha. `raster.Canvas.radial` spreads its stops evenly from centre to edge and
dithers between each adjacent pair, so **the number of stops decides how much of the radius is
speckle**. A two-stop radial dithers across its *entire* radius and is an exact 50/50
checkerboard at the half-way line — at ×2 render, a lattice of 2×2 blocks.

That is what put a fuzzy grid around the base of every ward flame: `flame()` drew its bloom as a
two-stop and a three-stop radial, both taller than they were wide and both centred well above the
base, so the fire stood inside a ball of checkerboard instead of on a pool of light. The fix is
not fewer dithered pixels, it is **repeating the leading stops** so the inner bands come out solid
and only the outermost one fades:

| ramp | dithered fraction of the radius |
| --- | --- |
| `[a, None]` | all of it |
| `[a, b, None]` | the outer half |
| `[a, a, b, None]` | the outer third |
| `[a, a, b, b, None]` | the outer quarter |

A glow wants the last two rows. `flame()` now uses a wide **flat** ground pool sitting on the
baseline (light landing on the floor) plus a narrow vertical air glow hugging the flame, both
solid-cored, and two small opaque discs where the flame meets the ground so the taper starts from
something. The orb aura in `hero.py` follows the same rule.

The same arithmetic is the reason the ward's floor pool was already written as
`[g2, g2, g3, g3, g4, g4, None]` rather than `[g2, g3, g4, None]`.

### The GIF preview must carry an exact palette

`tools/art/preview_anim.py` writes `tools/art/_title-anim.gif`, which is how the animation gets
looked at without launching Godot. It is worth knowing exactly how that export can lie, because
it did, and the symptoms point straight at the art rather than at the exporter.

Pillow converts an RGB frame to P mode for GIF by snapping it to the **216-colour WEB palette** —
every channel a multiple of 51 — and error-diffusion dithering the difference. Not one Bonelight
colour is in that palette. `occ #05070C` and `stone.deep #0B0F18` collapse together, the mid-tones
are pulled toward grey, and a 39-colour frame comes back with 85 in it. Measured: the old export's
frame zero differed from the shipping `title-screen.png` in **230,332 of 230,400 pixels** — 99.97%
of the frame.

The flicker is the same mechanism in the time axis. Floyd–Steinberg carries its error rightward
and downward across the whole image and is recomputed per frame, so a change confined to the six
flames re-rolls the dither of everything downstream of them:

| pixels changed by one flame phase | old export | exact palette |
| --- | --- | --- |
| frame 0 → 1 | 44,245 | 906 |
| frame 1 → 2 | 134,158 | 923 |
| frame 2 → 3 | 131,350 | 911 |

A 900-pixel change presented as a 134,000-pixel one is a screen that crawls. `indexed()` now maps
each frame onto a 39-entry palette by exact dictionary lookup — no quantiser, no dithering — and
GIF frame zero is now bit-identical to the shipping still. **That equality is the check**: if the
preview and `1-vigil.png` ever differ by a pixel at frame zero, the exporter has started
approximating again.

Only the preview was ever affected. The layers, the shipping PNG and the running game share no
code with this path.

### Per-behaviour notes

**Eyes.** Cheapest as *no texture at all*: have the generator emit the pair positions alongside
the art and draw them in `_Draw()` from the palette constants, with a per-pair timer that skips
drawing for a few frames. Each eye is one or two pixels; a texture layer for that is more
machinery than the thing it animates. Blinks must be independent per pair and irregular — nine in
the treeline and seven in the branches blinking on one clock is a lighthouse, not a wood.

**Pulse.** Three or four frames of the ward stepped up and down the ward ramp, cycling slowly.
Amplitude is the whole risk: the ward is already the brightest thing in the frame after the orb,
and the note is *lightly*. If the pulse is legible as a pulse on a still comparison of two frames,
it is probably too strong in motion.

**Flames.** `flame()` already takes a `seed`, so frames are the same six flames re-drawn at
successive seeds. Two cautions: the disc-stack shape means consecutive random seeds jump rather
than flow, so the seeds want to walk a path rather than be independent draws; and the bloom should
move *less* than the body, because a halo that flickers as hard as the flame reads as the whole
screen strobing. As built, the bloom does not move at all — only the body, core and sparks are
seeded — which is deliberate and is why the ring does not strobe.

**The orb.** The staff head is the brightest thing on the screen and it drives the rim, the bounce
and the underlight on the figure, so it has to breathe with them: if those step down and the
source does not, the source reads as a decal stuck on the picture. `wizard_hero` scales the aura
radius and steps its hot stop off the same `step` the rim uses. The forked claw is drawn over the
bloom (so it is not swallowed) and then a second, sparse pass of the aura goes over the claw, so
the light lies *across* the metal instead of stopping at it. Without that pass the claw is the
one object in frame the light goes around.

**The shaft runs all the way up into the orb.** It used to stop at `yb - 34u`, a hand's width
short of the head, while the bloom is centred 3u *above* the head with a radius reaching 2.2u
*below* it — so the top ~10px of the handle sat inside the glow and dithered away, and because
that radius breathes with the flicker, the amount eaten changed every frame. Handle and head read
as two objects with a flickering gap between them.

**A collar over the joint is not the fix, and was tried.** It read as exactly what it was: a
bright patch stuck on to hide a seam, obvious and obnoxious at any size. Three things replace it,
and between them there is no seam left to hide:

- The shaft ends at `yb - 36.5u`, *inside* the orb, so the orb sits on the end of it rather than
  floating above it.
- The fork legs root into the shaft's *sides* at `yb - 31.2u`, well below the head, with a lit
  fillet where each leaves the metal. Fork and shaft are one casting, not two parts touching.
- The tone climb up the shaft is **per-column and staggered**, never a full-width `rect`. Two
  rect steps put a hard horizontal line across the metal, which is a seam again — just further
  down than the one being fixed. Staggering it by column turns the step into a diagonal that
  reads as a falloff.

**The scene is lifted 48px off the bottom, and the lift is a translation.** `LIFT` in
`splash.py` moves every finished layer up the frame just before the vignette runs, which hands
the bottom band of the screen back to the UI. The composition is authored against a floor line
at `y=428`, a ward at `y=568` and about thirty other anchors; re-deriving all of them from a new
horizon produces a *different picture*, where a translation produces the same picture higher up.

Three things make it cheap, and all three depend on where in the order it happens:

- **Before the vignette.** That pass is a pure function of position, so it crushes the newly
  exposed band *and* the floor's cut edge above it toward `occ` in the same sweep that darkens
  the rest of the frame. The seam does not read as a crop because by the time you see it, it is
  the same colour as everything around it.
- **Before the wordmark.** WIZARD SURVIVORS is stamped afterwards and keeps its own height. Had
  it been lifted with the scene it would sit 30px off the top edge.
- **Every layer by the same amount**, via `Canvas.lift`, so the animation layers still composite
  to the flat render exactly and `TitleScreen.tscn` needs no change — the rects did not move,
  their contents did.

Measured after: the PRESS ANY KEY band at rows 616–636 is **100% pure occlusion**, as are the
bottom 40 rows; the lowest lit pixel anywhere in the frame is `y=560`. There are 80 authored rows
— 160 device pixels — of clean black under the art for a prompt, a version string or a menu.

**PRESS ANY KEY moved up with the art.** The `Prompt` `TextureRect` is anchored to the bottom
of the viewport, so the lift did not carry it: it stayed at device offsets `-48 .. -8` while the
picture above it climbed away. Its offsets are now `-100 .. -60`, which puts the 360x20 strip on
authored rows **590-610** - roughly centred in the band between the lowest lit pixel of the art
(`y=560`) and the frame edge, and 94.9% pure occlusion behind it. Position is entirely in
`TitleScreen.tscn`; `TitleScreen.cs` only ever touches `modulate:a`, so moving the prompt is a
two-number edit and needs no code change.

**The glow draws in front of the metal.** The fork was drawn over the bloom so the claw would not
be swallowed, which put a hard gold cutout across the front of the light source — the one thing
on frame the glow went *behind*. The metal goes down first now and the halo lies over it. That
only survives because the halo is nearly all fade: the legs sit at 1.7–3.4u, out in the dithered
part, so the light stipples *across* them rather than filling over them. Reverting the halo to a
mostly-solid ramp would erase the claw entirely.

**A glow is the fade. The solid part is the lamp.** This is the one place the fade-band rule
above inverts. A flame's ground pool wants its leading stops repeated, because there the solid
*is* the subject and the dither is only its edge. A halo is the opposite: the dither is the whole
subject, so the ramp wants to be **short** and the fade wants to be most of the radius. The orb's
bloom held a solid disc out to 3.9u — a flat slab of `#FFC63C` nearly twice the radius of the orb
itself, carrying the hard polygonal edge that a banded radial gives you. That is not an effect,
it is an oversized fill, and it read exactly as what it was: a yellow blob sitting on the staff
head. Three stops (`[a, a, None]`) put the solid edge at the orb's own radius and leave everything
outside it as halo.

**Every pass that builds one light shares one centre, and every radius is equal on both axes.**
The bloom was at `-36.6u`, the orb core at `-37.0u` and the over-pass at `-35.8u` with `ry 5.4`
against `rx 4.8`. Three centres and an ellipse: the light pooled low and the glow could not come
out round however it was tuned. One `hy`, circles only.

**The head's local variable must not be called `oy`.** `wizard_hero` sets `scene, ox, oy` as the
canvas blit origin at the top and uses it in the final `scene.blit(c.img, ox, oy)`. Naming the
orb's centre `oy` shadowed it and moved the whole figure ~300px up the frame — a silent one, since
the render still succeeds and audits clean. The layer's `getbbox()` is the check that catches it.

**Motes are sparks or they are nothing.** Two rules, and satisfying only the first is worse than
satisfying neither. Their x offsets must sum to about zero, or they drag the apparent centre of
the glow sideways even when the bloom is perfectly centred. But every one must also sit *further
than the bloom's reach* from the orb, and none may wear `ramp[3]` — that is the halo's own tone.
Balancing the sum alone once moved a mote to 2.6u, inside the glow and the same colour as it,
where a disc of pale gold on a gold halo is not a spark, it is a blob stuck to the head.

**A centre computed from a float is not the centre that gets drawn.** `Canvas.rect` truncates
its bounds — `range(int(x0), int(x1) + 1)` — so a shaft asked for at `sx = 20.625` with `w = 6`
is painted on columns **20..26, true centre 23.00**, while `sx + w * 0.5` is **23.625**. The
bloom, the orb, the fork and the motes were all centred on that float, which put every one of
them 0.62px right of the shaft they belong to — 1.25 device pixels at the ×2 render, and quite
visible: the glow reads as off-centre and the head reads as not quite mounted straight. The head
centre is therefore `(int(sx) + int(sx + w)) * 0.5`, taken from the columns that actually get
painted. **This applies to anything mounted on anything else in these generators**, not just the
staff.

The motes are the other half of that complaint and are not a rounding bug. Their x offsets summed
to −0.8u, so the apparent centre of the glow sat left of the shaft even once the bloom itself was
centred. They are meant to be irregular; they are not meant to be lopsided, so the offsets now sum
to zero.

**The fork legs are backlit, so they are drawn in shade gold, not base.** They sit between the
viewer and the orb. In base gold they washed straight into the bloom and the head read as a light
bulb; in `gold.shade` with one lit edge and an `occ` inner line they read as metal silhouetted
against light, which is both correct and the only way the claw survives at that brightness.

One over-pass of the aura covers the shaft *and* the fork, outer stops only. Lighting the fork
but not the shaft is what let the eye read the unlit part as a separate object in the first
place: everything metal near the orb is inside the light, so all of it takes the same stipple.

**Rim flicker.** `hero.py` already draws the figure onto its own transparent layer so `_rim` and
`_bounce` have an edge to find — which means emitting just those two passes as their own frames is
a small change, not a rebuild. The rim colour is `RIM #DCE8FF`, which is **reserved**: flicker
means varying *which pixels* get it, not what colour they are. And it has to be phase-locked to
the flame frames — the same index into both sequences — or the screen has two unrelated
animations happening near each other instead of one light source and the thing it lights.

### Open questions

- `TextureRect` in a `Control` tree, or `Sprite2D`/`AnimatedSprite2D` on a `CanvasLayer`? Either
  works, but whatever replaces the current node must keep the **fixed 720×1280 rect at exactly
  ×2** — the whole reason the scene is built the way it is (see above) is that fractional scaling
  of pixel art is the failure the art direction exists to prevent.
- Frame rate. The contract's animation section is the place to settle this, not this doc.
- Whether the motes drift as part of the ward layer or get their own; they are the one element
  that would look better on a continuous path than on a frame cycle.

## Verifying a change

`dotnet build` does not touch any of this. After regenerating:

1. `"$GODOT_BIN" --headless --path . --import`
2. `"$GODOT_BIN" --headless --path . scenes/TitleScreen.tscn --quit-after 260` — 260 frames runs
   past the prompt fade completing (~2.1s) and into the breath loop, so a fault in the tween chain
   actually surfaces. `--quit` alone exits on frame one and proves nothing about it.
3. The `ObjectDB instances leaked at exit` warning is pre-existing and unrelated — `MainMenu.tscn`
   prints the same.
4. `splash.py` audits every finished image against the contract palette and prints the count.
   It must be **0 off-contract colours**.
