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
2. **Far wood** — eighteen trees on the horizon line at y=436, in `stone.shade` against the sky.
   The tree nearest the middle is the shortest of the set, so nothing crowds the figure.
3. Eyes in the dark (y 360–424) and ground mist.
4. Ground: flagstone bands that widen toward the camera, from y=428.
5. **Grass**, thickening toward the camera and thinned inside the ward circle so the runes still
   read through it; then **brush banked up both edges** of the clearing floor.
6. Boulders.
7. **Near wood** — two trees at the frame edges in flat `occ`, mostly cropped. These do most of
   the atmospheric work: they put the viewer *inside* the wood looking out at the clearing.
8. **Overhead canopy** — six boughs reaching in from off the top corners, then a band of loose
   leaf mass from y=0 to y=344 filling the sky around them. `CLEAR` in `vigil()` is the single
   hole left in it, and it exists to hold the wordmark: **move the wordmark and you must move
   `CLEAR` with it.**
9. **The ward circle**, on the `WARD` ramp — the brightest thing in the frame after the orb, and
   the reason the composition works. There are no candles on it any more: nine orange flames
   around a gold-white ward put two warm light sources in the same place competing for one job.
10. The figure.
11. Motes lifting off the ring, after him so some drift in front.
12. **Foreground grass and brush** in flat occlusion across the bottom strip, held out of an
    ellipse around the ward. It is the nearest thing in the frame so it gets no light at all,
    and a plane the figure stands behind is what turns a backdrop into somewhere he is.
13. Vignette.
14. Wordmark.

## The knobs

| To change | Where |
|---|---|
| Which of the seven screens ships | index into `made[]` in `splash.py: main()` |
| Tree placement, height, canopy spread | the `far` and near-pair lists in `splash.py: vigil()` |
| Canopy density and raggedness | `density` arg, and `_clump` in `splashkit.py` |
| The hole in the canopy | `CLEAR = (cx, cy, rx, ry)` in `vigil()`; its lobing lives in `canopy()` |
| Overhead boughs | the bough list in `vigil()` — origin, angle, length and width per limb |
| Grass density and reach | the two grass loops in `vigil()`; one blade shape in `grass()` |
| How enclosed the clearing feels | the brush loops in `vigil()` — treeline, then both floor edges |
| How dark it is under the brim | the brim cast polygon in `hero.py` — that band *is* the face |
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
is to be dark. Its bottom two units dither through robe `deep` and `shade` onto the mantle rather
than stopping on a rule. Out of the bottom of it comes a nose in `flesh` and a grey beard in
`skin`, and that is the entire face. There are no eyes and no mouth; at this size they are three
dark specks in a void, which is what made an earlier front-facing head read as a mask.

Both are **drawn as stylised things, not simulated ones**, and that is the single lesson of the
whole face. Three earlier beards — five tapering locks, then a field of scattered tufts, then a
bushier field of the same — all tried to be the real object, and all spent their detail on
texture the eye cannot resolve at 150px while leaving the silhouette soft. What carries at this
size is shape and value break, so that is all either of them spends anything on.

The nose is **a small round bulb**, three discs on the centre line — the body, a lit cap up and
left, a shaded one down and right. A flat triangle was tried and reads sharp and beaky, which is
a different character; between the two, the pointed version and the round one are worth thinking
of as a real choice about who he is, not a rendering detail.

The beard is `skin`, the bone/pallor row — white hair and old bone are the same material in this
palette — and it is **long, thick and round**:

- **`_BEARD_PROFILE` is a half-width table**, `t=0` at the jaw to `t=1` at the tip: narrow where
  the moustache covers it, widening to its full 4.3 units around three fifths down, closing in a
  rounded tip rather than a fork.
- **Length is most of the read.** It runs eleven units, past the mantle hem and most of the way
  to the sash. The earliest versions were about half that, and a half-length beard reads as a
  full beard rather than as a wizard's.
- **Width has a ceiling, and it is the length.** Taken out to five units the profile made the
  beard as wide as it is tall, and it stopped being a beard and became a ball; the shape needs
  to stay clearly taller than it is wide however thick it gets.
- **Eleven partings in `deep`, not in occlusion**, each following the taper down, with a lit
  strand beside every other one and the middle ones hanging longest. Hard black lines at full
  contrast across a pale mass are the front of a radiator whatever spacing they are on — two
  carry that weight and the rest are one step of value. Four thick lines read as a grille and
  five fat capsules read as dreadlocks.
- **The moustache is lighter than the beard and narrower than its widest point**, thin under the
  nose and heavy and drooping at the ends, over a deep occlusion drop. It went dark once — a
  dark moustache reads as relief in the beard, which is right only when the beard is the
  subject; as a thing in its own right it has to sit in front and above.

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
