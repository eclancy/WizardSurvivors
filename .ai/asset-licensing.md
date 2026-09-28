# Asset licensing — what blocks making this repo public

Written because the question was asked directly: *is all purchased art out of the game yet?*

**It is now.** When this document was first written the answer was no, and not close. Every piece
of bought art has since been replaced and `assets/organized/` has been deleted.

**What is left is history**, which is section 3 and is the harder half. Sections 1 and 2 are kept
as the record of what was removed and why.

## 1. The licence

Every licence file under `assets/organized/` pointed at the same place:
`https://craftpix.net/file-licenses/`.

CraftPix licences are written around **use in a game, not distribution of the files**. The terms
on that page are the authority; the shape of them is that you may ship the assets compiled into a
game and may not redistribute, resell or make the asset files themselves available for download,
including as part of a repository. A public repo containing the PNGs is a download link for the
PNGs.

## 2. What was replaced

Direct `res://assets/organized/` references in code and scenes went **94 → 0**, and the directory
— 2,116 PNGs, 4,211 tracked files, about 59 MB — is gone from the working tree.

| Replaced | What it was | Generator |
|---|---|---|
| 26 spell icons moved out of `assets/organized/ui/` | already ours, just inside the blast radius | `spell_icons*.py` |
| 3 spell icons that no generator produced, plus `unknown.png` | the last GUI-pack slices, including the default spell icon in four call sites | `spell_icons*.py` |
| 29 relic icons, 10 Full Set Enchantment icons | `ui-png-elements2-*`, `ui-png-iconsmenu-*` | `relic_icons.py` |
| 13 spell projectiles and impacts, 53 frames | two magic-effects packs, two crystal props, a dungeon arrow | `spell_fx.py` |
| 7 interactables, 32 frames, and a fallback portrait | the dungeon items-and-traps pack, a magic-pack frame, a dungeon-pack priest | `props_fx.py` |
| 11 decor sprites, 21 themed props, 1 manifest | four prop sheets and two curated prop packs | `world_props.py` |
| 187 ground tiles across 11 terrains and 4 materials | the dungeon-floor pack, every stage's ground | `tiles.py` |
| 1 menu backdrop | a bought GUI plate on the character screen | reuses the existing menu background |

Two things fell out of the work that were worth having anyway.

**Everything is on an integer scale now.** The bought art was shown at 0.24, 0.42, 0.58, 0.7,
0.8, 0.85, 1.25 and ×3 across different systems — a pixel of source covering a fraction of a
pixel of screen, which `.ai/art-direction.md` section 1 calls its worst case and which several
scenes were papering over with `texture_filter = 2`. Every replacement is a contract cell rendered
at ×2 and shown at a multiple of 0.5, so a source pixel always covers a whole number of screen
pixels. Every Linear override is gone.

**Every generator is wired into `tools/art/build.py`.** None of the icon ones were, and a
generator nobody runs from the build is a generator whose output path nobody checks — which is
exactly how 26 pieces of our own art came to live inside the bought-art directory.

### What is still not ours, and is fine

| Directory | Origin | Publishable |
|---|---|---|
| `assets/bonelight/`, `assets/kidsart/`, `assets/sfx/` | generated in-repo | yes |
| `assets/testwizard/`, `assets/enemies/boo*` | Eric's own drawings | yes |
| `assets/fonts/` | Cinzel and PixelifySans, SIL OFL, licence files present | yes — OFL permits it |

### Still unresolved

**The music licence is unverified.** `.ai/audio-manifest.md` records that
`assets/music/labyrinth-escape.mp3` arrived as `good_day_story-labyrinth-escape-333453.mp3`, the
filename shape of a stock library, and that the same is unknown for `Pixel_Knights.mp3`. Music is
in the same category as the art was and has to be resolved before the repo is public.

**Nine root-level assets predate the generated pipeline** and no document says where they came
from: `wizard_guy1.png`, `wizard_guy2.png`, `Magic_Missile.png`, `spiritual_weapon.png`,
`arcane_explosion.png`, `ground_tile.png`, `fireball.wav`, `magic_missile.wav` and
`Wizard_Survivors_Title_Screen.png`. Several are still referenced — `PlayerFrames.tres` uses both
`wizard_guy` sheets. They need the same treatment.

## 3. The second problem, which is bigger: history

Deleting `assets/organized/` today removes it from the working tree and from `HEAD`. It does not
remove it from the repository.

- **54 commits** touch `assets/organized/`.
- The pack is most of the repo's weight: `.git` is **77 MB**, of which the pack objects are
  roughly 73 MB.

A public repository publishes its history. `git log`, `git checkout <old sha>` and the GitHub
archive download all reach the files. So making the repo public safely requires **rewriting
history** — `git filter-repo` or equivalent — which rewrites every commit hash in the project.

That collides with how this repo is worked on. `CLAUDE.md` is explicit that several sessions run
against it at once and that a branch another session holds must not be rebased or amended. A
filter-repo run is that, for every branch at once. It needs to be done deliberately, with
everybody else stopped, and with a backup clone kept.

## 4. What is already clean

Worth stating, because it is most of the art the player now sees:

| Directory | Origin | Publishable |
|---|---|---|
| `assets/bonelight/` | generated by `tools/art/*.py` | yes |
| `assets/kidsart/` | Eric's kids, plus a generated ground | yes |
| `assets/sfx/` (69 files) | generated by `tools/audio/*.py` | yes |
| `assets/testwizard/` | Eric's own drawing | yes |
| `assets/enemies/boo*` | Eric's own drawing | yes |
| `assets/fonts/` | Cinzel and PixelifySans, SIL OFL, licence files present | yes — OFL permits it |
| `scripts/`, `scenes/`, `tools/`, `.ai/` | ours | yes |

## 5. Two traps, one of them expensive

**26 of our own files are mis-filed inside the pack directory.** The generated spell icons are
written to `assets/organized/ui/ui-derived-spell-icon-*.png`, because `spell_icons_core.py` and
`spell_icons.py` both use `OUT_DIR = assets/organized/ui`. They are generated art and perfectly
publishable, but a blanket `git rm -r assets/organized` deletes every spell icon in the game
along with the pack. **They should move to `assets/bonelight/ui/spells/` before any removal
happens**, and the generators repointed. The name "derived" is a fossil of when they really were
derived from pack art; they are drawn from scratch now.

**The music licence is unverified and already flagged.** `.ai/audio-manifest.md` records that
`assets/music/labyrinth-escape.mp3` arrived as `good_day_story-labyrinth-escape-333453.mp3`, the
filename shape of a stock library, and that the same is unknown for `Pixel_Knights.mp3`. Music is
in the same category as the art here and has to be resolved on the same pass.

Also unresolved: the root-level `assets/wizard_guy1.png`, `wizard_guy2.png`, `Magic_Missile.png`,
`spiritual_weapon.png`, `arcane_explosion.png`, `ground_tile.png`, `fireball.wav`,
`magic_missile.wav` and `Wizard_Survivors_Title_Screen.png` predate the generated pipeline and no
document says where they came from. Several are still referenced — `ground_tile.png` is the Forest
background, `PlayerFrames.tres` uses both `wizard_guy` sheets.

## 6. The routes, and what each costs

**A. Replace the remaining pack art, then publish.** The honest one. It is the whole of
`.ai/art-replacement-manifest.md` plus a tile set and a prop set, which is the largest outstanding
job in the project. The tile ground alone is 303 tiles, though `LevelGenerator` only asks for a
handful of materials and the Sketchbook shows the shape of the answer: a generated material, a
`tiles.json` of our own, merged by `CuratedTileCatalog.LoadMany`. Everything needed to do this
incrementally already exists.

**B. Publish the code, keep the art private.** Move `assets/organized/` (and the unresolved music)
out of the repo, gitignore it, and have the build fetch it from somewhere the licence allows. The
game does not run for a stranger who clones it, which for a game repo is a real cost. **Still
requires the history rewrite** — this is not the cheap option it looks like.

**C. Publish with the art in place.** Not available. It is the thing the licence is there to stop.

Whichever route: the history rewrite in §3 is required by A and B alike, and the icon move in §5
must happen first or it takes 26 pieces of our own art with it.

## 7. If you want the smallest first step

Move the 26 generated icons out of `assets/organized/ui/`, repoint the two generators and the
`.tres` files, and re-run `python tools/art/build.py`. That is a contained change, it removes the
one landmine in the removal path, and it makes `assets/organized/` finally mean exactly one thing:
art we did not make and cannot publish.
