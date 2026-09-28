# Asset licensing — what blocks making this repo public

Written because the question was asked directly: *is all purchased art out of the game yet?*

**No. Not close, and the files being deleted today would not be enough on its own.**

This document is the audit behind that answer, and the routes out of it. It is about
**redistribution**, which is a different question from the art-quality backlog in
`.ai/art-replacement-manifest.md`: that one asks whether the art is good enough, this one asks
whether we are allowed to publish it at all. A sprite can be perfectly fine to ship inside a game
and still be illegal to put in a public GitHub repository.

## 1. The licence

Every licence file under `assets/organized/` points at the same place:

```
assets/organized/effects/fx-10-magic-sprite-sheet-effects-pixel-art-license-txt-license.txt
assets/organized/effects/fx-magic-sprite-effects-pack-license-txt-license.txt
assets/organized/level/props/lvl-props-free-top-down-trees-pixel-art-license-txt-license.txt
assets/organized/level/props/lvl-props-rocks-and-stones-top-down-pixel-art-license-txt-license.txt
assets/organized/level/props/lvl-props-top-down-bushes-pixel-art-license-txt-license.txt
assets/organized/level/props/lvl-props-top-down-crystals-pixel-art-license-txt-license.txt
assets/organized/ui/ui-l-license.txt
assets/organized/ui/ui-top-down-ruins-pixel-art-license-txt-license.txt
assets/organized/ui/ui-undead-desert-map-license-txt-license.txt
```

Each contains one line: `https://craftpix.net/file-licenses/`.

CraftPix licences are written around **use in a game, not distribution of the files**. The terms
to read before doing anything here are the ones on that page, and they are the authority rather
than this paragraph — but the shape of them is that you may ship the assets compiled into a game,
and may not redistribute, resell or make the asset files themselves available for download,
including as part of a repository. A public repo containing the PNGs is a download link for the
PNGs.

**Read the actual terms at that URL before acting.** Nothing below assumes a specific clause; it
assumes only that publishing the source files is the thing the licence is most likely to forbid,
which is the normal shape of a stock-asset licence.

## 1a. Progress

Removal is underway. What has gone so far, newest last:

| Done | What it replaced | Generator |
|---|---|---|
| 26 spell icons moved out of `assets/organized/ui/` | nothing - they were already ours, just in the blast radius | `spell_icons*.py` |
| 3 spell icons that had never been redrawn | the last GUI-pack slices among them | `spell_icons_core.py` |
| `unknown.png`, the default spell icon | `ui-png-skills-icon-2.png`, in four call sites | `spell_icons.py` |
| 29 relic icons | `ui-png-elements2-*` | `relic_icons.py` |
| 10 Full Set Enchantment icons | `ui-png-iconsmenu-*` | `relic_icons.py` |
| 13 spell projectiles and impacts, 53 frames | two bought magic-effects packs and two crystal props | `spell_fx.py` |
| 7 interactables, 32 frames | the dungeon items-and-traps pack, and the last magic-pack frame | `props_fx.py` |
| 1 fallback character portrait | a dungeon-pack priest standing in for a wizard | `props_fx.py` |
| 11 decor sprites, 21 themed props, 1 manifest | four bought prop sheets and two curated prop packs | `world_props.py` |

**Direct `res://assets/organized/` references in code and scenes: 94 at the start of this, 12
now, and every one of the twelve is a ground tile.** Every generator above is wired into `tools/art/build.py`, which none of the icon ones were
- and a generator nobody runs from the build is a generator whose output path nobody checks,
which is exactly how 26 pieces of our own art ended up inside the bought-art directory.

The spell effects fixed a second thing on the way. The bought frames were 72x72 cells shown at
scales of 0.24, 0.42, 0.58, 0.7, 0.8, 0.85 and 1.25 - seven fractional scales under nearest
filtering, which section 1 of the art contract calls its worst case, and which the scenes were
papering over with `texture_filter = 2`. The replacements are the 32x32 projectile cell rendered
at x2 and shown at 0.5, 1.0 or 1.5, so a source pixel always covers a whole number of screen
pixels. Every Linear override is gone.

### What is left, by size

| Remaining | Refs | Notes |
|---|---|---|
| Ground tiles | 11 | `StageEnvironmentCatalog`, `Node2DGame`, `LevelTilePainter`. The big one: 303 tiles and 174 props reachable through three manifests |
| A menu backdrop | 1 | `CharacterSelection` uses a bought GUI plate |

## 2. What is still pack art, and what the game does with it

2,090 PNG/JPG files under `assets/organized/` are from bought packs. Of those, roughly 550 are
reachable by the running game:

| Reached how | Count | What it is |
|---|---|---|
| The dungeon-floor tile manifest | 303 | **Every ground tile in every chapter** except the Sketchbook |
| Two curated prop manifests (mines, torture) | 174 | Every scattered world prop |
| Direct `res://` references in code and scenes | ~69 | see below |

The direct references, by what they hold up:

- **World decor** — trees, bushes, rocks, five crystal colours.
- **Interactables** — every chest frame, spike traps, flame vents, flasks, arrows.
- **Spell VFX** — lightning bolt, lightning from above, fireball, ground spikes, self-shield.
- **UI** — menu icons, GUI element sheets, and `ui-png-skills-icon-2.png`, which is
  `Player.DefaultSpellIconTexture`: the fallback every spell without its own icon falls through to.
- **A character sheet** — `char-…-priest1-v1-1.png`, used by `CharacterSelection` as the shared
  wizard portrait.

The remaining ~1,540 pack files are in the repo but unreferenced. They are the rest of the packs
as purchased.

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
