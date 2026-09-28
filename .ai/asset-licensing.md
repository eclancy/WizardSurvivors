# Asset licensing — what blocks making this repo public

Written because the question was asked directly: *is all purchased art out of the game yet?*

**It is now.** When this document was first written the answer was no, and not close. Every piece
of bought art has since been replaced and `assets/organized/` has been deleted.

**And the repo is public**, at https://github.com/eclancy/WizardSurvivors. History was rewritten
before it was flipped; section 3 records how and what to watch for. Sections 1 and 2 are the
record of what was removed and why.

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

**RESOLVED  the music is gone.** Both tracks were removed from the working tree and from
history in the same pass as the art. Neither licence could be established, and the rule that
governed the art governs music too. `MusicCatalog` is empty rather than deleted, so a track with
known terms can be dropped back in.

~~**The music licence is unverified.**~~ `.ai/audio-manifest.md` records that
`assets/music/labyrinth-escape.mp3` arrived as `good_day_story-labyrinth-escape-333453.mp3`, the
filename shape of a stock library, and that the same is unknown for `Pixel_Knights.mp3`. Music is
in the same category as the art was and has to be resolved before the repo is public.

**RESOLVED  the root-level assets are cleared.** Seven were dead and were deleted; four were
live and are replaced by generated art. `assets/` is now eight directories and nothing else.

~~**Nine root-level assets predate the generated pipeline** and no document says where they came
from: `wizard_guy1.png`, `wizard_guy2.png`, `Magic_Missile.png`, `spiritual_weapon.png`,
`arcane_explosion.png`, `ground_tile.png`, `fireball.wav`, `magic_missile.wav` and
`Wizard_Survivors_Title_Screen.png`. Several are still referenced — `PlayerFrames.tres` uses both
`wizard_guy` sheets. They need the same treatment.~~

## 3. History — done, and what it took

Deleting the files from `HEAD` did not remove them. A public repository publishes its history:
`git log`, `git checkout <old sha>` and the GitHub archive download all reach it. So the history
was rewritten before the repo was made public.

**`git filter-repo` was not available** — it needs Python 3 and this machine has only 2.7, with
no pip. The rewrite used `git filter-branch`, which is deprecated and slow but built in. Four
passes over 475 commits, because each pass turned up something the last one had missed:

1. `assets/organized/` — the CraftPix packs, plus the music, the tilesets and the nine
   root-level files of unknown origin.
2. `assets/imported/` — **the pre-`organized` layout of the same packs**, 791 blobs nobody had
   mentioned anywhere. Also `bin/` and `obj/`, which had been committed twice.
3. `tools/inspect/bin` and `tools/inspect/obj` — more committed build output — and
   `tools/_sheet_*.png`, which are contact sheets OF the bought GUI packs.
4. The `.import` sidecars left pointing at all of the above.

**The lesson worth keeping: do not build the strip list from what you remember removing.** Build
it from what is actually in the history. The command that found passes 2 and 3 is:

```
git rev-list --objects --all \
  | git cat-file --batch-check='%(objecttype) %(objectsize) %(rest)' \
  | sort -k2 -rn | head -40
```

Two other things had to go first. **149 `refs/agents/*` refs** — Claude Code session checkpoints
from finished turns, local only and never pushed — pinned the old history and had to be deleted
before anything could be pruned. So did the remote-tracking refs, which cannot be bulk-deleted
while `origin/HEAD` is a symref pointing into them.

### Verifying it, and the one number that matters

The local pack stayed at 66 MB after all four passes, which looked like failure and was not: it
was unreachable cruft that `push` does not send. **The measurement that counts is a fresh mirror
clone of the remote**, which is what a stranger gets:

```
git clone --mirror https://github.com/eclancy/WizardSurvivors.git verify.git
```

**75 MB before, 8.8 MB after, and a path scan across every ref in it returns nothing.**

### Two things still worth doing

**Ask GitHub to garbage-collect.** A force-push leaves the old objects on their side, unreferenced
but reachable by SHA for a while. Nobody outside ever had those SHAs — the repo was private its
whole life — so the risk is small, but GitHub Support can purge them on request and it costs
nothing to ask.

**The backup is at `../WizardSurvivors-prefilter-backup.git`**, a 75 MB mirror taken immediately
before the first rewrite. It contains everything that was stripped. **It must never be pushed
anywhere public.** Delete it once you are satisfied nothing was lost.

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

