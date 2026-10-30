# Art replacement manifest

Every asset that has to be redrawn to put the game on the Bonelight contract, what it is today,
and what replaces it. Counted from the tree at `ca39186` (after the ranged-enemy consolidation).

Three docs, three jobs — don't confuse them:

- `.ai/art-inventory.md` — what art we **own**, and what is wired up vs. sitting unused.
- `.ai/art-direction.md` — the **contract**: grid, light model, palette, silhouette rules.
- **this file** — the **backlog**: what still has to be drawn, in what order, and how much of it.


## The pack-art kill list

**Every asset the game still references that came out of a bought pack**, measured rather than
remembered. Regenerate the list at any time with the one-liner at the end of this section; the
counts below are from that script.

| where | referenced files | status |
|---|---|---|
| `assets/bonelight/**` | **106** | ours, generated, keep |
| `assets/organized/ui/` | 35 | **26 are ours** (`ui-derived-spell-icon-*`), 9 to replace |
| `assets/organized/effects/` | 34 | all pack |
| `assets/organized/level/` | 17 | all pack |
| `assets/organized/characters/` | 1 | pack |
| root and `assets/enemies/` | 11 | legacy, pre-`organized` |

So the job is roughly **90 files**, and they are not 90 separate jobs - they come from five sheets.

### 1. Spell and relic icons - 27 files, the highest-value job

Every `SpellData_*.tres` icon and every `ChestItemCatalog.GetIconPath` entry that is not already
`ui-derived-*`. Two packs: `ui-10-magic-sprite-sheet-effects-*` (14 files) and
`ui-magic-sprite-effects-pack-2-icons-*` (7), plus `ui-png-elements2-*` and `ui-png-iconsmenu-*`
behind the relics and the Full Set Enchantment icons.

**The spell icons are DONE.** `tools/art/spell_icons.py` drew the five that shipped with new
spells (Riptide, Cinderbreath, Mirefoot, Kindled Ward, Gravewell); `tools/art/boon_icons.py` drew
fourteen for the boons; and `tools/art/spell_icons_core.py` has now back-filled the remaining
eighteen. **Every spell in the game wears an icon we drew.**

`spell_icons_core.py` is deliberately a separate module from `spell_icons.py` because they have
different lifetimes: that file grows by a function whenever a spell is added, this one shrinks to
nothing and gets deleted when the back-fill is finished.

**Every evolution has its own icon too** (2026-10-28): `tools/art/evolution_icons.py` draws one per
level 4, level 8 and Apotheosis id - 182 - from the spell painters above, at
`assets/bonelight/ui/evolutions/<id>.png`. A new card needs no art job: re-dump the catalog
(`_AscensionAudit --json`) and re-run the generator. The rule and the layers are in
`.ai/level-up-screen.md`, section 2f; `RegressionChecks.ValidateEvolutionIcons` reports a gap.

**Twenty-five relic icons remain**, and four of them still slice the `ui-10-magic-sprite-sheet`
pack sheet (Storm Lattice, Inferno Core, Thunderstone, Crystal Prism). That is the next batch, and
it is the same shape of job as the boons: objects on the material rows, not effects on the element
ramps.

Highest value because an icon is looked at while the game is *paused*, so it is the art the player
studies rather than glances at - and because two of them are still doing double duty for different
things, which the note further up this file records.

### 2. Projectile and impact VFX - 34 files

`fx-10-magic-sprite-sheet-*` (5 sheets, sliced into most of the projectiles) and
`fx-2d-pixel-dungeon-asset-pack-*` (chests, flasks, arrows, spikes, flamethrower). Cinderbreath's
flame strip is the first replacement and the template: `tools/art/spell_flames.py`, 32x32 cells on
the element ramp, one horizontal strip per animation.

Note that several spells now **draw themselves** and need no art at all - Mirefoot's pools,
Kindled Ward, the bursts every area spell throws out to its radius (`scripts/ImpactDebris.cs`). That
is the cheaper answer wherever the thing is a zone rather than a travelling object, and it is recorded
in `.ai/spell-roster.md`. **Gravewell left that list (2026-10-23):** drawn as a faint disc it read as
nothing at all in a playtest, so it is generated art now - `tools/art/gravewell.py` writes the pit
(open, armed, collapse; 48 cell) and the heap of earth that holds a caught enemy (32x16) - with its
collapse drawn by `scenes/shaders/gravewell_collapse.gdshader` out of the blast radius. None of the
self-drawn effects draws a ring or disc at its radius any more (`.ai/art-direction.md` section 3).

### 3. Level tiles and props - 17 files

`lvl-props-*` and the tilesets. Lowest urgency: they are the least-looked-at pixels in the game and
the stage shader already does a lot of the work of making them cohere.

### 4. Actors - 6 files

- `char-2d-pixel-dungeon-asset-pack-character-animation-priest1-v1-1.png`, still referenced by
  `CharacterSelection.cs`
- *(the Boo's `assets/enemies/boo-*.png` left this list on 2026-10-20: Elim's ghost, redrawn to
  the contract at Eric's request - see "Also shipped")*
- `assets/testwizard/testwizard-1.png`

Everything else in the actor tier has already moved to `assets/bonelight/characters/`.

### 5. Root-level legacy - 6 files

Pre-date the `organized/` move and are the easiest wins because each has exactly one consumer:
`Magic_Missile.png`, `magic_missile.png` (two different files for one spell),
`arcane_explosion.png`, `spiritual_weapon.png`, `ground_tile.png`, and
`Wizard_Survivors_Title_Screen.png` - the last of which is only still alive because
`StageSelection.tscn` references it. The title screen itself stopped using it long ago.

### Fonts

`PixelifySans-Regular.ttf` **is no longer referenced** - it was the theme's body face until its
digits turned out to be unreadable (its 5 is its 8, its 7 is its 1) and everything moved to Cinzel.
Two copies of `font-planes-valmore.ttf` sit under `organized/ui/` unreferenced. Neither is art we
made, and a body face of our own is a real project rather than a sprite: `tools/art/font.py` has a
hand-drawn 5x7 uppercase face, but turning that into a Godot `FontFile` means emitting a bitmap
font, and an uppercase-only face cannot set body copy. Left as a known gap.

### Regenerating this list

```
python - <<EOF
import io, os, re, collections
pat = re.compile(r'res://(assets/[^"\')\s]+\.(?:png|jpg|ttf))')
w = collections.defaultdict(set)
for root, dirs, files in os.walk('.'):
    if '.godot' in root: continue
    for fn in files:
        if not fn.endswith(('.cs', '.tres', '.tscn')): continue
        t = io.open(os.path.join(root, fn), encoding='utf-8', errors='ignore').read()
        for m in pat.finditer(t): w[m.group(1)].add(fn)
for a in sorted(w):
    if not a.startswith('assets/bonelight/'): print(a, sorted(w[a])[:2])
EOF
```


## How to regenerate these numbers

```bash
# animation sets and frame counts
for f in scenes/resources/*Frames.tres; do
  echo "$(basename $f .tres) | $(grep -o '"name": &"[a-z]*"' $f | tr '\n' ' ') | \
frames: $(grep -o '"texture": \(Sub\|Ext\)Resource' $f | wc -l) | \
rects: $(grep -c 'region = Rect2' $f)"
done

# which scene uses which sheet (finds the shared ones)
for f in scenes/*.tscn; do
  fr=$(grep -o 'scenes/resources/[A-Za-z]*Frames\.tres' $f | head -1)
  [ -n "$fr" ] && echo "$(basename $f .tscn) -> $(basename $fr)"
done

# scenes that draw themselves and therefore need no art. Both tests matter: a scene can
# carry no Texture2D and still be fully drawn, because its art arrives through a
# SpriteFrames .tres instead (every enemy is like this).
for f in scenes/*.tscn; do
  grep -q 'type="Texture2D"' $f && continue
  grep -q 'Frames\.tres' $f && continue
  echo "$(basename $f .tscn)"
done
```

## 1a. Status — the roster shipped

`tools/art/roster.py` draws fourteen actors, `tools/art/sprite_warden.py` a fifteenth on the
elite cell, and `tools/art/anim_sets.py` derives the full animation contract from each pose.
**344 frames**, all generated, all in the game:

| | Cell | Animations | Notes |
|---|---|---|---|
| Pyromancer, Frostweaver, Stormcaller, Geomancer | 48×48 | idle 4, moving 8, hurt 2, death 6 | designed from the spell each casts, in its own saturated pigment row |
| swarmer, runner, bruiser, shielder, skullsentry | 32×32 | moving 8, attack 6, hurt 2, death 6 | redrawn into the deep half of their ramps |
| lunger, slammer, exploder, summoner | 32×32 | moving 8, attack 6, hurt 2, death 6 | **their own sheets at last** — no tint, no fractional scale |
| hexer | 32×32 | moving 8, attack 6, hurt 2, death 6 | the ranged caster, replacing the Cultist |
| warden | 48×48 | moving 8, attack 6, hurt 2, death 6 | the recurring miniboss, replacing the Soldier |
| Gaoler, Hollow Choir, Mother Rot, Archivist, Still Warden, Long Coil, Warden of the Deep | 96×96 | moving 8, attack 6, hurt 2, death 6 | the seven chapter bosses — `tools/art/sprite_bosses.py` |
| rime guard | 48×48 | moving 8, attack 6, hurt 2, death 6 | the Still Warden's cone-telegraph guard |

That retires three contract violations on the last four: reusing another class's sheet, using
`modulate` to tell classes apart, and scales of 2.3 / 3.4 / 3.0 / 4.4 under nearest filtering.

**The wizards' sheets are in the game but not yet driven.** `Player.cs` plays only `idle` and
never switches animation — it flips H and that is all. The art and the `CharacterData.Frames`
wiring are both in place; what is missing is the state machine, and an attempt at it crashed the
game reproducibly in a C# finalizer (`gchandle.is_released()`). Four bisects did not localise it.
That is a code problem with nothing left to draw.

## 1b. Cut — the pack-derived enemies

**Orc, Soldier and Cultist are gone.** All three were third-party pack sheets wearing a tint,
and the game is going to completely original art. The scenes and the three `.tres` are deleted;
the source PNGs still sit unreferenced in `assets/organized/` with the rest of the pack, which
`.ai/art-inventory.md` covers.

Two of the three were holding a gameplay role that nothing else filled, so those roles were
redrawn rather than deleted with the art:

| Cut | Was | Replaced by | Why not simply deleted |
|---|---|---|---|
| `CultistEnemy` | the only enemy that attacks from range | **`HexerEnemy`** — `tools/art/roster.py`, 32×32 | A back line is a role, not a biome. Nothing else in the cast shoots. |
| `SoldierEnemy` | the recurring miniboss, on a timer | **`WardenEnemy`** — `tools/art/sprite_warden.py`, 48×48 | Losing it loses the run's only set-piece between the opening and the boss. |
| `OrcEnemy` | a heavy body in three earthy spawn tables | *nothing — its band folds back into the bruiser* | Its whole design argument, written into `Node2DGame`, was that it came from a different art pack to the skeletons and so earned its place on silhouette alone. That argument died with the art, and the bruiser is already the heavy body it stood in for. |

**The Warden is a fiction fix as much as an art one.** The comment justifying the Soldier called
it "the only living, armoured humanoid among a roster of skeletons, an orc and a ghost", which is
flatly against `.ai/world-and-tone.md`: the horde serves one will, it was made or taken by one
hand, and there are no mercenaries in it. The premise says the dark wizard keeps every other
wizard locked up, so the thing that keeps turning up to stop you is his **jailer** — which is why
it arrives on a clock rather than out of a spawn table. It wears a cell door as a breastplate,
has a bolted hasp where a face should be, and drags two chains ending in open shackles.

**The Hexer is the first sprite in the game to draw the Caster row of §4 as written.** The
contract specifies "a **detached** floating orb, offset from the head" and nothing had one — the
Summoner holds a sigil ring at hand height instead. The Hexer's orb sits beside the head with
genuinely transparent pixels under it, which is the part that matters: the first pass filled that
gap with `occ`, which looks identical in colour and fails the fill-it-solid-black test outright,
because `occ` is a drawn pixel and only *nothing* separates two masses in a silhouette.

Both are wool and steel respectively against the cast's linen and violet, so neither shares a
material row with the robed figures it has to stay distinct from. The `modulate` tint that used
to separate Cultist from Summoner is gone with the sheet it was propping up, which is the last of
the tint-as-class-identifier cases §4 forbids.

`RegressionChecks.ValidateMiniBoss` now checks the Warden scene at startup. `Node2DGame` loads it
by path inside a spawn tick that returns quietly on failure, so every way that reference can break
produces the same symptom — the miniboss never arrives, which reads as tuning rather than as a
bug.

### Also shipped

| | Cell | Scale | Note |
|---|---|---|---|
| **Boo** | 96×96 | `3` → `2` | Elim's ghost, redrawn on the Bonelight model **at Eric's request (2026-10-20)** and made the Sketchbook's second miniboss - the one kids' drawing the never-restyle rule was lifted for. `tools/art/sprite_boo.py` grows his 17×15 drawing ×4 and smooths it, so the silhouette, slot eyes, O mouth and raised arm are his; then lights it (rim on the key side, `occ` on the shadow side, bone ramp sinking to deep), cuts his round base into a ragged trailing hem that is dithered see-through, hollows the eyes round his mark, and puts a cold `arcane` cyan light in the chest and throat - the colour of its wail. ~64 px figure, ~128 on screen. `moving` is still his own two frames; `attack` is the wail, timed to its 1 s wind-up; `hurt` flinches and thins; `death` dissolves from the edge in, drifting up, and leaves the two eye-points last. |
| **Elderbark** | 96×96 | `6` → `2` | The boss, off the orc sheet at last. |

Both scales were fractional or applied to the wrong cell size, and both are integers now.

### The Sketchbook — not on the backlog, and never will be

Twelve sprite sheets drawn by Eric's kids, wired in as chapter 8. They are the whole cast of it:
eight enemies, a boss and its projectile, plus a dog and a two-faced message scattered as decor.

**And the ghost.** Boo (`assets/enemies/booscaryspritesheet.png`) is Elim's drawing too. It
predates the chapter and lives outside `assets/kidsart/`, and since 2026-10-20 it spawns in the
Sketchbook and nowhere else. It is the one kids' drawing with derived frames: `attack`, `hurt` and
`death` were generated from its two frames by `tools/art/sprite_boo.py` (see "Also shipped" above)
when it was taken for Eric's own. **Eric's decision, 2026-10-20:** keep the animation frames, tune
the drawing up to sit with the rest of the art, make it bigger and scarier and a miniboss. So it is
redrawn, by request and credited to Elim, and it is the only kids' drawing that is. It is still not
on this backlog: it is finished, and it is regenerated from his file rather than replaced.

**They are excluded from this document on purpose.** Every other line here is a promise to
replace something; these are finished. `tools/art/kids_art.py` slices and repacks them and does
nothing else - no repalettising, no rescaling, no derived hurt or death frames. A sheet with no
`death` animation is handled by `Enemy.StartDeath`, which frees immediately, so nothing has to be
invented to fill a gap.

The only generated thing in `assets/kidsart/` is the squared-paper ground, in four variants plus
a `tiles.json` manifest of its own - kept out of the bought pack's manifest so that updating the
pack cannot delete it. It is the only pale ground in the game, which is how the chapter announces
that it is outside the campaign before a word of it is read.

| Drawing | Role in the chapter |
|---|---|
| `wizarddude` + `magicmissel` | **Wizard Dude**, the boss, and the fan of missiles he throws |
| `Rockem` | the heavy, and the chapter's recurring miniboss in place of the Warden |
| `SmiLeRspritesheet` | the swarmer - fast and flimsy |
| `oneeyedudespritesheet` | the middle-weight chaser |
| `zombiebrainsspritesheet` | the slow wall |
| `vampiredudespritesheet` | the sprinter (two coffins, then two vampires) |
| `pushydudespritesheet` | a `SlammerEnemy` with a **cone** telegraph - it shoves the way it faces |
| `blobbyjump` + `blobbymorejump` | a `LungerEnemy`: squash-and-stretch is the wind-up, the leap is the dash |
| `friendlymessagespritesheet` | the **friendly message**, an enemy that chases you and alternates between its grinning face and its frowning one |
| `dogcompanionspritesheet` | the **dog**, a companion the player always has in this chapter: it heels, runs at whatever is nearest, bites it, and cannot be killed |

The blobby pair is worth noting: the kids drew a wind-up sheet and a dash sheet for the same
creature without being asked for one, which is exactly a lunger. It needed no new code at all.

**The chapter has no decor.** The dog and the message were scattered as scenery in the first pass
and both were wrong: one is a creature with two expressions, the other is a companion that fights.
A page with nothing on it is a better page than one with two pictures strewn over it.

**The dog is the one place `world-and-tone.md` is deliberately broken.** That doc says the player
has no allies, which is why `KindledWard` is careful to be a made light rather than a creature.
The dog is a creature, it has a face and it is loyal. That is allowed because the Sketchbook is
not in the fiction - it carries no CorruptionText because the dark wizard has never been there -
and a chapter outside the story is the right place for the one thing the story forbids. It is not
a precedent for a campaign chapter.

### The seven remaining chapter bosses — `tools/art/sprite_bosses.py`

All seven, plus the Still Warden's guard, on the 96×96 boss cell (the guard on the 48×48 elite
cell) at an integer ×2 scale. 176 frames, derived by `anim_sets.full_set` from one drawn pose
each, with a per-boss `BOSS_FEEL` row in `build.py` giving each its own sway, reach and stagger.

**They were designed as seven outlines before a single pixel of detail went down**, because a
player meets these fifteen minutes apart across eight hours with a swarm on screen. The
silhouettes are deliberately different classes of shape — a T, a bell, a dome, a slab, a wedge, an
S and an H. Say those seven words to someone who has played the game and they can name the fight.

Six carry the cold arcane mark because they were taken. The Warden of the Deep burns instead: it
was *built*, in his own seat, and the emissive colour is the last boss saying whose it is before
it has moved.

**Three lessons, each paid for by a pass that had to be thrown away:**

- **A mass filled with one character has no interior.** The first pass came in at 0.35–0.50
  edges/px and the contact sheet said exactly why: the Gaoler read as a table, the Archivist as a
  bookcase, the Long Coil as a boot. What fixed it was not more speckle — noise was tried first
  and every sprite came out wearing the same gravel — but giving each figure a *structure made of
  boundaries*: masonry courses, plate ridges, ice facets, scale rows, veins.
- **A linear taper is a traffic cone.** Five of the eight were built from one and the contact
  sheet was a set of road furniture. A hand-written half-width profile is what lets a silhouette
  have a shoulder, a waist and a flare.
- **Deep is a direction; invisible is not.** The Still Warden's ice spur and the Long Coil's neck
  were both built from the two darkest stops of their ramps and both came out as holes in the
  screen. `sprite_treant.py` records making this exact correction, which is the third time now.

**Honest number: these run 0.50–0.68 edges/px against the §4b floor of 0.80.** So does the
shipped Elderbark, at 0.70 — the floor was set on 32×32 sprites and a 96×96 cell is mostly
interior. They are in the same band as the boss already in the game rather than at the target,
and raising the whole boss cell to 0.80 is one job for all eight rather than seven separate ones.

## 1. Actors — 11 sheets, ~236 frames

The bulk of the work, and the reason phase 1 exists. Target per enemy is the §5 animation
contract: `moving` 8 + `attack` 6 + `hurt` 2 + `death` 6 = **22 frames**. The player needs
`moving` 8 + `hurt` 2 + `death` 6 = 16; it has no `attack` because spells fire on a timer.

| Sheet | Silhouette class | Today | The gap |
|---|---|---|---|
| Player (inline in `player.tscn`) | — | 4 priest frames, idle only | No hurt or death; 16×16 cell |
| `EnemyFrames` | swarmer | 41 frames, 4 anims | **Shared with SlowEnemy** — must split |
| SlowEnemy | swarmer variant | *none of its own* | **New sheet.** Scores 1.00 silhouette overlap with Enemy today |
| `FastEnemyFrames` | runner | 45 frames, 4 anims | Needs the trailing-rag attachment |
| `TankEnemyFrames` | bruiser | 43 frames, 4 anims | Needs horned helm + pauldrons |
| `BooEnemyFrames` | flyer miniboss | **Done** (2026-10-20): 22 frames, 4 anims, 96×96 | Elim's ghost on the light model - see "Also shipped" |
| `SkullSentryFrames` | flyer / turret | **4 frames, `moving` only** | No attack/hurt/death |
| LungerEnemy | charger | *none — `FastEnemyFrames` tinted, ×2.3* | **New sheet.** Wants a coiled, low, spring-loaded read — it is the only enemy that stops dead and then moves fastest |
| SlammerEnemy | heavy bruiser | *none — `OrcEnemyFrames` tinted grey, ×3.4* | **New sheet.** Wants raised arms mid-wind-up: the tell is a ring on the ground, and the body should agree with it |
| ExploderEnemy | swollen rusher | *none — `BooEnemyFrames` tinted, ×3.0* | **New sheet.** Wants a distended, unstable silhouette readable at a glance in a crowd — it is the one enemy you must find *before* it arrives |
| SummonerEnemy | caster | *none — `CultistEnemyFrames` tinted green, ×4.4* | **Done.** Its own sheet, and the Hexer that replaced the Cultist is a different material row again, so the two casters no longer share a body |

Three things to know before drawing any of these:

- **Boo and SkullSentry used to vanish instead of dying** (so did the Cultist, which is gone). `Enemy.StartDeath` frees
  the node immediately when the `SpriteFrames` has no `death` animation. That is the documented
  fallback, not a bug, but it is why those three feel abrupt next to the skeletons.
- **SkullSentry used to be 16×16 at ×4**, so its art pixel was 4 against the skeletons' 2 —
  integer, so on the grid, but visibly chunkier once nearest filtering went on. It is on the
  32×32 cell now, like everything else basic.
- **The four attack-pattern enemies ship on borrowed sheets.** Lunger, Slammer, Exploder and
  Summoner (issue #33) are behaviour-complete and validated, but every one of them is an
  existing sheet under a tint and a scale, which is exactly the collapse `CLAUDE.md` warns
  against. It is survivable today only because each one draws a tell nothing else draws — a
  charge streak, a slam ring, a fuse ring, a summon ring — so class still reads, just from the
  effect rather than from the body. Drawing these four is the highest-value silhouette work
  left, ahead of re-drawing sheets that are merely imperfect.

## 1b. Menu background — done

`assets/bonelight/ui/menu-background.png`, generated by `tools/art/menu_bg.py`. Authored 360x640
at x2 like the title screen, and palette-clean against section 3 of the art direction (the
generator refuses to write off-contract colours).

It is deliberately abstract — an arcane sigil, a ley lattice and drifting motes, no subject and no
horizon — because the main menu is looked at constantly with buttons on top of it, and a second
illustration would compete with the UI and make the title screen's one moment ordinary. It also
leans arcane cyan where the title leans gold, so the two screens do not read as the same picture.

**Edit the generator and re-run it, never the PNG.** The sigil's vertical position in particular
was set against a real screenshot rather than reasoned about.

## 1c. Level-up screen — done

`assets/bonelight/ui/levelup-plate.png`, `levelup-card.png` and `levelup-card-hover.png`,
generated by `tools/art/levelup_frames.py`. Authored at half size and rendered at x2, so their
pixels are the same size as the pixels in the sprites beside them.

The screen was hard to read, and the cause was transparency rather than typography: cards at 55%
alpha, over a 62% dim, over live gameplay, so the swarm was legible *through* the text. The cards
are now an opaque stone face inside a bevelled gold border, the whole menu sits on a stone slab,
and the dim behind it went to 88% since nothing the player needs is under it any more.

- **The card is a nine-slice**, 80×80 with 20px margins. The centre is a single flat tone on
  purpose — it is stretched the width of the card, and anything textured there smears into a
  vertical streak. All the ornament lives in the corner blocks, which nine-slice never stretches.
- **The plate is a nine-slice too**, authored at exactly the panel size the code builds
  (696×1156 in portrait, from `LevelUpMenu.CenterMenuPanel`, not from the scene anchors, which are
  overwritten at runtime). At the real size nothing stretches, so every crack lands on a whole
  pixel; at any other size it degrades to a stretch instead of breaking.
- **The slab is drawn from the top of the stone ramp, not the bottom.** The first attempt used
  `deep` and `shade` throughout and came out near-black — no texture survived and the card faces
  vanished into it. The slab has to be the lightest thing on screen after the gold, so the cards
  read as recessed panels on it. Bonelight being a dark palette does not make every layer dark.
- An upgrade to a spell already held wears the **lit** frame at rest, where a new spell wears the
  plain one. That replaces the cyan border the old styling used to tell the two apart, and says it
  with light instead of a second colour.

**Still flat:** the element preview strips between cards (`Fire 1/2`, `Earth 1/2`) keep their
element-coloured borders. They are secondary information and are deliberately not competing with
the gold, but they are the one part of the screen still wearing the old treatment.

## 2. Floor — ~24 tiles

**Superseded in substance; the table below is history.** Every floor and terrain is now generated
by `tools/art/tiles.py` (fifteen terrains with role variants, seven materials), and the small things
on the ground - flowers, lilypads, reeds, pebbles, snow - by `tools/art/ground_detail.py`, along
with the flower-pen landmark. `.ai/terrain.md` section 8 has the current state. What is still open
from this section is the contract's grid: the tiles are 48×48 at ×1, not 32×32 at ×2 (phase 3).

Six stages ran on **one 64×64 grass tile plus five 48×48 curated dungeon frames**, separated
only by a per-stage `modulate`. The curated manifest exposed 303 frames; we painted six of them.

| Stage | Tile today | Target |
|---|---|---|
| Forest, Swamp | `assets/ground_tile.png` 64×64 | 3 × 32×32 each, authored under Bonelight |
| Castle | `dun_071` 48×48 | 3 × 32×32 |
| Ruins | `dun_002` 48×48 | 3 × 32×32 |
| Ice | `dun_051` 48×48 | 3 × 32×32 |
| Desert | `dun_019` 48×48 | 3 × 32×32 |
| Volcanic | `dun_145` 48×48 | 3 × 32×32 |
| Maze walls | **done** - `floor_void_0`, a flat `occ` tile from `tools/art/tiles.py`; see `.ai/terrain.md` §6 | none |

Roughly **18 floor + 6 wall = 24 tiles**. Stage mood stops being a `modulate` on the sheet and
becomes the two shader uniforms in §7 of the contract.

## 3. Decor props — ~14

Eleven props were in rotation: 2 rocks, 2 bushes, 2 trees, 5 crystals. **Trees and bushes are
done (2026-10):** `tools/art/trees.py` draws 18 trees and 13 bushes across four biomes, in three-quarter
view at 2-4x the wizard's height, and the four old 48 px tree and bush sprites are gone
(`.ai/terrain.md` section 10). Rocks and crystals are still the originals.

We own roughly 600 unused prop variants, and it is tempting to treat this as curation rather
than drawing. It is not. The pack props are a different craft tradition — one autumn tree
carries **37 colours** with smooth painterly shading and no light direction — so they cannot sit
under a single key light next to a 50-colour cast. They also carry their own baked shadows.
Redraw, don't curate.

## 4. Spell VFX — 17 effect sets from 12 sheets

The de-sharing is most of the work here. Every row below where one sheet serves several jobs has
to become several sheets, because the whole reason art cannot be swapped one sprite at a time is
that most sprites are load-bearing twice.

| Sheet today | Serving |
|---|---|
| `fx-…-10-fire-ball` | **Fireball · MoltenShard · MeteorImpact** |
| `fx-…-2-lightning-crash` | **ShadowBolt · ChainLightning** |
| `fx-…-8-self-shield` | **Player ShieldAura · elite floor marker** |
| `lvl-props-…-white-crystal4` | FrostShard — a world prop cast as a projectile |
| `lvl-props-…-green-crystal4` | ThornVine — likewise |
| `ui-…-4-sun-strike2` | ScorchingRay — a **UI icon** rendered in the arena; **gone (2026-10-29)**: its last trace, a never-shown `ImpactSprite` of `scorching-ray.png`, left the scene when the ray became `scenes/shaders/scorching_ray.gdshader` (section 9) |
| `fx-…-1-lightning-bolt` | GaleBlade |
| `fx-…-6-spikes-from-ground` | ObsidianSpike |
| `fx-magic-…-spritesheets-7` | GlacialSpike |
| `fx-magic-…-spritesheets-9` | ToxicSporeBurst — **gone (2026-10-23)**: the pulse became mushrooms, drawn by `tools/art/spore_mushrooms.py` |
| `fx-…-just-arrow` | HuntersArrow |
| `assets/Magic_Missile.png`, `assets/arcane_explosion.png` | MagicMissile, ArcaneExplosion |

Target cells are 32×32 for projectiles and 64×64 or 96×96 for impacts, per §1 of the contract.
Roughly **100–130 frames** once each effect owns its own sheet.

Elements do **not** multiply this. §3 of the contract maps twelve element ramps onto one drawn
burst — white-hot core, hue in the mid and edge — so one impact drawing serves all twelve.

## 5. Hazards and pickups — ~26 frames

| Asset | Today | Note |
|---|---|---|
| SpikeTrap | **Done (2026-10)**: 9 frames, idle / tell / active / cool | A plate of seven holes the spikes shoot out of; `props_fx.py` |
| FlameVent | **Done (2026-10)**: 13 frames, idle / tell / active / cool | An iron grate whose slots heat up, then vent fire (looped); `props_fx.py` |
| HealthPickup | 4 frames (a flask) | |
| BuffItem | 4 frames (a flask) | Same flask family as HealthPickup — must diverge |
| ChestReward | 8 frames (idle + open) | |
| XPOrb | **1 frame — a 16×16 level prop** | Needs a purpose-drawn emissive pickup |

`StageHazard` no longer draws a floor marker: the plate and the grate are drawn to their collision
shapes and are the danger area, and the boots are tested against them (`.ai/art-inventory.md`,
"Hazard art needs its own telegraph").

## 6. Icons — 67

Better shape than the rest of the art, because most already have distinct images.

| Set | Count | State |
|---|---|---|
| Active spells | 24 | **19 carry distinct icons**, four of them drawn for this project (Riptide is the first with a generator behind it, `tools/art/spell_icons.py`, which also draws its projectile sprite). Gaps: ArcaneExplosion slices its own VFX sheet; SpiritualWeapon falls through to the generic `ui-png-skills-icon-2.png` |
| Passive spells | 12 | Paths hardcoded in `Player.cs`. **`void_lance` and `blur` both point at `9-black-hole2.png`** — the one collision left |
| Relics | 25 (+1 fallback) | `ChestItemCatalog.GetIconPath`. Guarded: `RegressionChecks.ValidateChestItemIcons` fails startup if two ever collide |
| Chest sets | 10 | `SetIconRoot + n` |
| Evolutions | 182 | **Done, all generated** (`tools/art/evolution_icons.py`, `.ai/level-up-screen.md` 2f). Guarded: `RegressionChecks.ValidateEvolutionIcons` reports an id with no icon |

These come **last** (phase 8): they are 1:1 swaps with no geometry, so nothing else is blocked
on them. Note the earlier estimate of ~48 given in conversation was low — it omitted the chest
set icons and undercounted the relic roster.

## 7. Not art — don't budget for these

**Twelve visuals draw themselves.** No texture to replace:

`BlackTentacles` · `ConeOfCold` (`ConeBlast`) · `CycloneSlash` · `SolarFlare`
(`SolarFlareVisual`) · `SpiritualWeapon` · `VoidLance` · `EnemyProjectile` · `TidalBarrier` ·
`BowDrawVisual` · `StageHazard`'s floor marker · `BossEnemy` · `LevelUpPickup`

The last one is easy to miss: `LevelUpPickup.tscn` carries no texture and no `SpriteFrames`,
because it renders through `PlaceholderShape` — the level-up gem is drawn in code. Don't go
looking for its sprite. `PickupBase` resolves the same `PlaceholderShape` node for XP orbs,
which is why the orb has both a procedural body *and* a crystal texture on top.

They do have a *style* problem now that the project filters nearest: smooth anti-aliased circles
sitting in a hard-edged frame. That is a code change — quantise the draw calls to the pixel grid
— not an art one, and it belongs with phase 4.

`VoidLance` no longer has it (2026-10-29): the blade, its motes, Void Glaive's blade, Witching Hour's
smear and Cosmic Collapse's hole are one shader on one quad (`scenes/shaders/void_lance.gdshader`), and
Abyssal Scar's crack another (`void_scar.gdshader`), both snapped to the world's 2-unit cell like
`riptide_wave` and `stormsweep_lash`. `.ai/spell-roster.md` section 2h, "Void Lance's look", says how.

The growth spells no longer have it (2026-10-29; section 9 has each part's audit): `ThornVine` is one
shader on one quad (`thorn_vine.gdshader`), its Thicket and Entangle stamped in ramp stops; `BrambleSeed`'s
thicket, runners and hedge are one quad a patch (`bramble_patch.gdshader`), its thorns in the air stamped;
`Contagion`'s marks are stamped rings and its clouds pixel gas (`spore_cloud.gdshader` through `GasCloud`);
Toxic Spore Burst's Lingering Rot, embers and mycelium are opaque and on the grid; Mirefoot's Fen Gas is
the same pixel gas; a `LingeringZone` that draws itself (Pyroclasm's magma, the Lingering Rift) is
`lingering_pool.gdshader`; the speed potion's trail is dithered.

`ScorchingRayBeam` no longer has it either (2026-10-29): it is a shader on the world's 2-unit cell, and
its scorch another. Nor do Cinderbreath's tongues (`FlamePuff`, a sprite strip the engine turned and
scaled, now a shader) and the char under its fire (`CinderGround`, the scorch's shader), nor Kindled Ward's
mote and flares (`KindledWard`) and Wisp Trail (`WardWisps`), a shader each, nor `SpiritualWeapon`: its
sheets kept, everything it draws through a shader that turns alpha into dithered coverage, and its quake
and holy ground a shader of their own. Section 9, below, has the audit and the new look spell by spell.

`BowDrawVisual` no longer has it (2026-10-29): Hunter's Draw's bow is one quad drawn by
`scenes/shaders/bow_draw.gdshader` in the bow's frame, snapped to the world's 2-unit cell and still bending
continuously with the charge, and the aim brackets on its target are grid-snapped rectangles. So, with it,
is everything else the four `ElementalBolt` spells draw for themselves: the arrows in flight, Rain of
Arrows' volley and Barbed's lodged arrows (`hunters_arrow.gdshader`), Hunter's Mark (`hunters_mark`),
Molten Shard's shard, slag and Caldera (`molten_shard`, `molten_slag`, `caldera`), Gale Blade's blade and
Live Wire (`gale_blade`, `live_wire`) and Spring Tide's lane (`spring_tide_lane`). Section 9 has each.

`CycloneSlash` (`OrbitingBlade`) no longer has it (2026-10-29): the whirlwind - both rings' blades and
their wake, the gust's eddies, Typhoon Core's arms, Vortex Reaper's in-drawn wind - is one quad drawn by
`scenes/shaders/cyclone_whirl.gdshader` on the world's 2-unit cell; Wind Shear's crescents and the
Endless Chase's throws are stamped in world-grid cells. Section 9 has its audit.

`HollowStar`, `IronPalisade`, `Starfall`, `ThunderStrike` (Gale Blade's Thunderhead) and `LatticeField`
(the Lattice) no longer have it either (2026-10-29): Hollow Star's well is one shader on one quad
(`scenes/shaders/hollow_star_well.gdshader`), every line these spells drew is a `SpellStrokes` stroke
on the world's 2-unit cell (`spell_strokes.gdshader`), Iron Palisade's stakes and caltrops are steel-row
pixels drawn upright in world coordinates, and every fade among them - and the Familiar's, Gravewell's
and Mirefoot's wisps' - is a dither (`spell_dither.gdshader`). Section 9, "The trap and star spells'
shared pieces" and after, has each part.

`SolarFlare` (`SolarFlareVisual`) no longer has it either (2026-10-29): the burst, Corona Discharge's
beams and the charge at his boots are `scenes/shaders/solar_flare.gdshader` on the world's 2-unit cell,
and Sun Dogs' mock suns are stamped cell by cell. Section 9 has the audit.

**~110 GUI plates and panels** (`ui-png-*`, the `FantasyGuiSkin` interpolated ranges, backdrops)
are illustrated menu chrome, deliberately off-grid and off-contract. They can stay longest and
may never need replacing.

**The title screen is done.** `assets/bonelight/ui/title-screen.png` is a 360×640 composition grown to
360×840 (more canopy above, near fern below, so tall phones are not letterboxed) and drawn at a
whole ×2 into a fixed 720×1680 rect. It is generated by `tools/art/splash.py` — screen 1,
"Vigil" — which also holds six other finished candidates that stay in that file for reference;
switch which one ships by changing the index in its `main()`. `tools/art/hero.py` holds the
title-size player figure, which is a different construction from the 32×32 sprite and is meant to
be: at that size the sprite's silhouette is the whole design, and here it is a starting point.

`assets/bonelight/ui/title-prompt.png` is a second, transparent 360×20 strip carrying PRESS ANY
KEY in the same pixel face. It is deliberately *not* baked into the background and deliberately
not a `Label`: `TitleScreen.cs` fades it in a beat after the artwork lands and then breathes it,
and there is no Godot font resource for the hand-drawn face the wordmark uses. Any future overlay
on this screen — a version string, a "continue" line — should follow the same pattern.

It replaces `assets/Wizard_Survivors_Title_Screen.png`, a 1024×1024 square that `TitleScreen.tscn`
drew with `stretch_mode = Keep` — native size, anchored top-left, so it ran 304px off the right
edge and left a 256px dead band below.

**Two screens still use that old square as wallpaper**, and they are the remaining half of the
job: `MainMenu.tscn` covers with it (`stretch_mode = 6`, so it at least fills, cropped and
resampled) and `StageSelection.tscn` has the *same* Keep bug the title screen had. Neither is
fixed by the title work, and both want a Bonelight backdrop of their own before that PNG can be
deleted.

## 8. Totals

| Category | Drawings | Phase |
|---|---|---|
| Actors | ~236 | 1, 2, 5 |
| Floor and walls | ~24 | 3 |
| Spell VFX | ~120 | 4 |
| Icons | 67 | 8 |
| Hazards and pickups | ~26 | 6 |
| Decor props | ~14 | 6 |
| **Total** | **~490** | |

Actors are roughly half of it, which is why the migration order front-loads them — and why
phase 1 draws exactly **one** production pair first. If a 28 px figure cannot hold five tones, a
rim and a class attachment across eight frames, that is worth discovering on one sheet rather
than after re-cutting the 129 atlas regions in phase 2.

## 9. The pixel-art pass (2026-10-29)

Eric, from screenshots: "does not look up to our typical visual fidelity", then "Update all spells to
the same quality we expect now". Every part a spell puts on screen was audited against the finished
references (`stormsweep_lash`, `riptide_wave`, `tentacle_rift`, `gravewell_collapse`, `mire_surface`,
`crater` with `crater_fire.py`, `obsidian_glass.py` with `GroundSpike`, `frost_patch`) and
`.ai/art-direction.md` sections 1-3 and 6: square art pixels on the world's 2-unit cell, opaque ramp
stops, Bayer dither as the only gradient, no area markers. A part that passed was left alone.

### Thorned Vine

| Part | Audit | Now |
|---|---|---|
| The vine (all milestones) | **Failed.** On the grid (WhipLimb's stamped squares) but every square's colour was LERPED - bark into grass down the vine, grass into an off-palette brown as it withered - so it read as a smooth vector wedge with a rounded club for a root | `scenes/shaders/thorn_vine.gdshader`, one quad: the vine laid out by WhipLimb.Layout's own integration from the same five numbers (the hit still tests WhipLimb's tip on the CPU), each world cell measured against it. Bark in the earth row with grain and lit/shaded sides, greening through a dithered band into Grass with verdant shade away from the key; raked thorns and three leaves fixed to the stem as it grows; poison white-hot at the tip while it strikes; cut square where it leaves the ground. Withering eats it into the gold row's soil cell by cell |
| Sweep arc | **Failed.** A dotted arc at 85% alpha held at the tip's radius over the whole sweep - under Hydra Vine's three heads, a dotted ring round the pack | The shader's smear: streaks along the vine's travel over the ground it just crossed, dense right behind the lash and gone within 0.7 rad, thinning through the hold |
| Torn soil | **Failed.** Occlusion and clods at fractional alpha | In the shader: a ragged hole wider than the stem, its lip lit toward the key, clods thrown while it grows, crumbling by dither |
| Strikes (poison where it cuts) | **Failed.** Faded in alpha | Steps core, hot, mid, edge down the Poison ramp; each drop goes out on its own |
| Entangle's coils | **Failed** in their last tenth: lerped to the off-palette brown | Each stamped cell dries to the earth row on its own fixed beat |
| Barbed Lash's barbs and sap | Passed (opaque ramp stops on the grid, sap eaten away cell by cell) | Unchanged |
| Thicket's brambles | **Failed** only while withering (lerp to the off-palette brown) | Each stamp dries to the earth row and the gold row's soil on its own beat |
| Snapback, Hydra Vine | No parts of their own beyond the vine and smear | - |

### Bramble Seed

| Part | Audit | Now |
|---|---|---|
| The roots (every patch, Wildwood's thrown ones too) | **Failed.** Antialiased `DrawPolyline` runs with a `DrawCircle` on every joint, 1.5 px antialiased thorn lines, a `DrawCircle` bud, every colour a LERP (bark to grass down a root, all to an off-palette brown as it dried) at fractional alpha | `scenes/shaders/bramble_patch.gdshader`, one quad a patch under the bodies. Each root is laid out per cell exactly as `DrawRoot` laid it - polar, its length never past `Radius`, so the long roots still end on the hit - with its width held across the hooked tip by the root's slope. Bark (earth row, lit and shaded sides) greening through a dithered band into its element's living tone, raked barbs, a living bud that swells as it whips, clods while it erupts; drying eats it into the gold row's soil cell by cell, and its last 0.4 s dissolves by dither. The eleven roots' shapes are uploaded once; per frame only scalars cross. A cell skips any root it is too far round from |
| Torn soil at the seed | **Failed.** Two translucent `DrawCircle`s | In the shader: the hole in occlusion, clumps of dark soil lit on the key side |
| Runners | **Failed.** Antialiased polyline and barb lines, lerped colours, alpha | In the shader from the strip it cuts (`from`, `to`, bearing): a 2-cell creeper swaying, barbs raked back, a living tip |
| Bramble Keep's hedge | **Failed.** Antialiased `DrawMultiline` canes, lerped colours, alpha | In the shader: bushes on the rim, three canes each, an art pixel or two thick in an occlusion line, bark at the foot and a thorn at the tip. Bushes that would stand in water or a wall are one bit each in an `ivec4`, worked out again only when the rim moves |
| Thornstorm's volley | **Failed.** Antialiased `DrawLine` darts that faded in alpha | Stamped darts on the world's grid, outlined, tip in the ramp's core; spent, a dart falls away from its tail forward |
| Burrs | **Failed.** Antialiased crossed lines and a bark square at fractional alpha | A stamped ball of bark bristling with four thorns and four diagonals; letting go it sheds the diagonals, then the thorns |
| Ironbriar | The same parts in Metal's ramp (`living_*`, `thorn_tip`) | Unchanged in kind |

### Contagion

| Part | Audit | Now |
|---|---|---|
| A carrier's mark (a status, so it stays) | **Failed.** An antialiased `DrawArc` circle at 90% alpha | A ring stamped on the world's grid at the carrier's feet, flattened to lie on the floor, in an occlusion line: its far side the ramp's hot, its sides mid, its near side edge. Pandemic's swell grows it a whole art pixel at a time and adds a broken second ring of the hot tone before the beat |
| The spores a jump throws | Drawn by the shared `ImpactDebris` - on the grid, ramp stops, but its last 30% fades in alpha. A shared helper, so not changed here | Unchanged |
| Fever Burst's clouds, Plague Wind's cloud and its trail | **Failed.** Seven overlapping antialiased `DrawCircle`s a cloud at 26-30% alpha: a soft wash, doubled where puffs overlapped, and a green fog over a pack that died together | `scenes/shaders/spore_cloud.gdshader` through `scripts/GasCloud.cs`: one quad a cloud carrying its numbers in its vertices, every cloud on the node in one batch. A ring of six billowing lobes round a middle one whose outer edges land on the radius the cloud infects; each lobe lit on its key side, edge-toned under, a fleck of the core rising through; filled by world-space Bayer that is densest in a lobe's heart and opens toward its edge so the swarm in it still reads, and thins away by the same dither. Overlapping clouds dither as one and never double |
| Marsh Fever | The same parts in Water's ramp | Unchanged in kind |
| Outbreak, Pandemic | No parts of their own beyond the burst and the marks | - |

### Toxic Spore Burst

| Part | Audit | Now |
|---|---|---|
| The mushrooms (base, Giant Caps, Doomcap; poison and Ember Caps' fire) | Passed: generated 1:1 by `tools/art/spore_mushrooms.py`, drawn at x2. Only their corner was wherever the sowing landed, so their art pixels sat off the world's grid by a fraction | The corner snapped to the world's 2-unit grid (`PixelCells.SnapPoint`). Sheets not regenerated |
| The burst's spores | Passed: the sheet's own puff frames, which thin by dither | Snapped to the grid |
| Lingering Rot's cloud | **Failed.** The burst's puffs drawn again at 70% alpha and faded, so the overlapping puffs doubled into a translucent wash | The same puffs, opaque, in the sheet's dithered frames (2 and 3, which let the floor through); going, each puff steps to the last two, sparser frames on its own beat and drops out, so the rim of puffs that showed the reach is still the rim of what re-poisons |
| Ember Caps' burning patches | Flames passed (the sheet's); the patch faded them in alpha | The flames go out one by one, the middle one last |
| Mycelial Bloom's thread | **Failed.** An antialiased `DrawLine` pair that faded in alpha | Stamped on the world's grid in Grass mid and edge in an occlusion line, with a slight wave; it runs out with the child cap and is drawn back into it from the parent's end once the child sprouts |

### Mirefoot

| Part | Audit | Now |
|---|---|---|
| The bog (`MireSurface`, `mire_surface.gdshader`), the waders' rings, Broad Wake's and Great Bog's wider pools | Passed - it is one of the references | Unchanged |
| Sucking Mud's heap round the held one's feet | Passed: opaque verdant stops stamped on the grid | Unchanged |
| Fen Gas | **Failed.** A scatter of 4-unit squares - two art pixels each, coarser than everything round it - picked by hash rather than dithered, in two tones | The shared pixel gas (`GasCloud`, `spore_cloud.gdshader`, Contagion's): lobes whose outer edges are the radius it poisons, lit and shaded, thinning away by dither; one quad a cloud, all of them in one batch |
| Will-o'-Wisps (`WispSwarm`) | Mote, halo and tail on the grid in opaque Grass stops, but the whole wisp faded in alpha over its last 0.4 s and its tail was drawn at 80% | Redrawn by the traps-and-stars pass (its commit 9725348, "Mirefoot's wisps fade by dither"), so not touched here |
| `TrailWeaver`, `GravePath` | Draw nothing (`GravePath` is Gravewell's remembered path) | - |

### LingeringZone's own pool - Pyroclasm's magma and the Lingering Rift

Mirefoot's pools never draw themselves (MireSurface does), but a `LingeringZone` that does is what
Fireball's Pyroclasm and Arcane Explosion's Lingering Rift leave. **Failed:** two translucent
`DrawColoredPolygon`s (the caller's element edge at 34-42% alpha and a darker copy inside), an
antialiased bubble, all fading in alpha over the last third, and a bank that wandered up to 3% past
the radius it burns. **Now** `scenes/shaders/lingering_pool.gdshader`, one quad: every world cell
shaded once and opaque - mostly a crust of occlusion, thickest toward the bank, the caller's `FillColor`
tone molten in the channels between its plates and their edges glowing in its `EdgeColor`, a bubble
swelling and bursting - its bank wandering only inward of `Radius`, dissolving from the edge in. A first
version lit the lip toward the key in the glow tone; round the near half of the pool that read as a
stroked rim, and went. Only the two colours' RGB is read, so
neither caller changed.

### The speed potion's trail (HasteTrail)

**Failed:** six afterimages at fractional alpha (overlapping into a soft smear) through a flat
Wind silhouette, and three additive antialiased lines at the boots that faded in alpha. **Now** each
afterimage is opaque Wind mid where it is drawn at all, thinned by a Bayer dither on the sprite's own
texels (the older the ghost, the fewer), so it stays as crisp as the frame it was taken from; the
streaks are opaque Wind cells on the world's grid that shorten as the potion runs out, the outer two
going first.

### Magic Missile, Frost Shard, Chain Lightning and Shadow Bolt - the shared pieces

- **Areas and turning bodies are shaders** on one quad each, snapped to the world cell as `riptide_wave`
  and `void_lance` are; each spell's section names its own.
- **Strokes are stamped** cell by cell by `scripts/PixelInk.cs` - lines (Bresenham on the 2-unit grid,
  runs merged into one rect), cells, blocks, discs and rings - for what is a line rather than an area:
  a chain's arc, a thread, a spark, a crack, a shard in flight. A fade is a dissolve through the 4x4
  Bayer matrix (`PixelInk.Keeps`), never alpha. It needs a world-aligned canvas, so every pooled layer
  that uses it is `TopLevel` at the origin. `ImpactDebris` already worked this way and is left alone.
- **Lightning reads on paper as well as on the dark**: an element-edge under-stroke two art pixels wider
  than a seam of hot (or white-hot) light. On the Sketchbook a bare yellow seam vanished into the cream;
  the gold edge holds it.

### Magic Missile

| part | before | bar |
|---|---|---|
| the dart (and Twin Volley's shards, the same scene) | `magic-missile.png` at the scene's scale 0.5 - art pixels half the world's - turned to its heading, so its pixels stood on a rotated grid | fails |
| its spray | a `GPUParticles2D` of translucent teal squares (#4DDED5, off the Arcane ramp), spinning | fails |
| Threaded Needle's thread | two antialiased `DrawLine`s a stitch under a translucent outline, fading by alpha; motes as off-grid 2x2 rects | fails |
| Overcharge's sparks and overload | antialiased `DrawLine` zigzags under a translucent outline, a translucent `DrawCircle` flash | fails |
| Arcane Orb | translucent `DrawCircle`s under an antialiased `DrawArc` | fails |
| Unstable Detonation's burst, the orb's end burst | `ImpactDebris`, stamped on the grid | passes, left alone |

Now: the dart and the orb are `magic_missile.gdshader` on the missile itself (the sprite and the
particles are gone from `MagicMissile.tscn`; `magic-missile.png` is no longer referenced by a scene).
The dart keeps the drawn one's silhouette at the size it was on screen - a solid pointed head with a
white-hot tip and core, a wavering tail - and the spray is sparks shed off the tail in the dart's frame.
The orb is drawn out of the radius it grinds: a lit rim, a body dithered at a third so the bodies it
grinds show through the gaps instead of through a translucent wash, the light on the key's side and
five motes turning; it still swells and flickers in its last fifth. The thread is an Arcane-edge
under-stroke three art pixels thick with a hot seam, each stitch dissolving as it ages. Overcharge's
sparks are little zigzags in the Lightning ramp and the overload's bolts a gold edge under a white-hot
root, with a dissolving flash at the heart. The card tints no longer multiply over the ramp; a
Legendary gets a gold rim.

Photographed before (`pb_mm_*`) and after (`pa_mm_*`) in
`/root/.local/share/godot/app_userdata/Wizard Survivors/` (`_z16`/`_zg` the zoom, `_d` the Enchanted
Forest, `_b` the Sketchbook's paper). After: the base spell and every level 8 pairing at `--zoom=1.6`
on the forest, and the base with three pairings that cover all five cards at both zooms on both floors.
Before: the base and those three pairings at `--zoom=1.6` on the forest and at the game's zoom on the
paper (the machine was shared and loaded; the full grid did not fit). Still needs eyes: a volley of
darts at full rate (each dart is its own quad), the orb on a phone.

### Scorching Ray

**Audit.** Everything the ray drew itself failed; what it borrows passed.

| Part | Verdict |
|---|---|
| Beam core and heat glow | **Failed.** Two `Line2D`s - a 2.5-4 px core and a 7-10 px glow at 50-70% alpha - so a rotated, fractional-width, translucent stroke at every angle but the four square ones |
| Haze, heat pulses | **Failed.** Antialiased `DrawLine`s, the haze a 15-21 px wash at 16% |
| Muzzle flare, head, splash pool, landing flash | **Failed.** Stacks of translucent `DrawCircle`s, the pool squashed by a transform; the glint four antialiased lines spun to any angle |
| Shockwave ring | **Failed, and a marker.** An antialiased `DrawArc` thrown out to a fixed 40 px round a ray that strikes one body |
| Sparks, smoke | **Failed.** Antialiased lines with alpha; translucent discs |
| Flashpoint's flashover | **Failed.** Translucent discs, squashed to 0.6 while the burst it shows is a circle |
| Burn Through | **Failed.** The beam as above, and the band its pierce test burns behind the target (`PierceHalfWidth` either side) was not drawn at all |
| Prism's split rays | **Failed.** Three antialiased lines and two soft circles a ray |
| Sunlance | **Failed.** The same strokes widened to 5-14 px, while the sweep burns `LanceHalfWidth x (1 + heat)` = 26-52 px either side of it |
| Scorch | **Failed.** Squashed translucent circles with antialiased crack lines |
| Area's burst, Prism's debris | **Passed** (`ImpactDebris`, shared) |
| `ImpactSprite` | Dead: a `Sprite2D` of the old UI icon that nothing ever showed. Removed from the scene |

**New look** (`scenes/shaders/scorching_ray.gdshader`, `scenes/shaders/cinder_scorch.gdshader`). Still a
thin red laser splashing into the ground. The beam, flare, head, pulses, pool, flash, flashover, smoke and
Prism's rays are one shader that snaps every fragment to the world cell and then measures it against the
beam, so the laser is a staircase of square pixels at any angle: a white-hot core one cell wide (two at
full heat), a cell of ember-lit red, a cell or two of ember red that flickers along its length, and a
crackle of red sparks a cell off it. The colours are Fire's core and the ember row (section 3's player
pigment, "Pyromancer / Fireball"), never Fire's yellow mid, so it reads red. The quads only say where to
look - one along the beam, one along each split ray, one over the strike and the hands - and every
fragment shades the whole ray at its cell, so overlapping quads agree. The droplets, sparks and the glint
(square arms, then diagonal, by turns) are cells stamped on the grid in `_Draw`. Fading is dither.

- **The splash.** The ring went; a crown of eight molten droplets is thrown flat across the floor and
  hops as it cools down the ember row, round a pool of molten light lying under the target's feet.
- **Drawn from the hit's numbers.** Burn Through draws the band it burns: embers drifting out to
  `PierceHalfWidth`, from the target to where the burning stops. Sunlance draws tongues of flame licking
  across its band while it sweeps, the furthest on `LanceHalfWidth x (1 + heat)`. Flashpoint's flashover is
  a blast whose front runs out to exactly the radius it hit, unsquashed, as the hit is.
- **The scorch** is `ScorchGround`, a helper made for these spells: one quad a burn, its numbers in the
  vertex colour (one material, batched), a char thinning to soot at a ragged edge that only bites inward,
  cracks one cell wide cooling from white-hot down Fire's ramp into the red row's coals, and crumbling
  away edge first. Cinderbreath's char uses it too.
- A Legendary's gold rides in the quads' modulate, not the node's, which would reach the scorch's numbers.

Photographed before (`vis_beams_b_*`) and after (`vis_beams_a_*`) with `_UiShot --spell-stage
--cast-on-stage`: the base ray and Flashpoint + Prism, Handoff + Sunlance and Burn Through + Prism (every
card), at `--zoom=1.6` on the Cursed Dungeon and at the game's zoom on the Scorched Sands; after shots
also at `--zoom=3` and on the Sketchbook's paper, the brightest floor.

### Riptide

Audited from `_UiShot` bursts against a `--spell-stage` crowd: the base spell, level 8 with all six level
4 + level 8 pairings, and Spring Tide (Tituba's Apotheosis), each at `--zoom=1.6` on the Cursed Dungeon
(stage 1, dark) and at the game's zoom on the Frozen Waste (stage 5 - which photographs dark too: it is
night there); and, for a pale floor, the base, Brackish Tide with Tidal Wall, and Spring Tide at the
game's zoom on the Sketchbook's paper (stage 8). Spring Tide's lane was also photographed on its own over
a dark and a pale floor, since the Tidal Wall that always comes with it covers it in every game shot.

| Part | Audit | Now |
|---|---|---|
| The wave's crest, face, body and lip, with Groundswell's growth, Brackish Tide's sea, Tidal Wall's titanic form and Trident's fan (`riptide_wave.gdshader`) | Passed - it is one of the references | Unchanged |
| The wave's spray, and Tidal Wall's spume and wake (the same shader) | **Failed - missed in the first audit, caught at review** (`--demo-spell-level=8 --hold --crowd=14 --zoom=1.6` with Breaker + Tidal Wall). On the grid, but at a tidal wave's size every droplet and every heap of spume was a flat disc of one tone up to 32 px across - soft white and pale-blue circles - and the wake was ruled lines of one tone dragged the length of the screen, like rain | In the shader: a droplet or a heap is a clump (`foam_tone`) - its edge knocked in and out cell by cell, a white cap toward the top of the screen, a hot body, a broken rim of mid - and the spume is capped at 22 px across, the spray at 14; the wake is short flecks a pixel thick that waver across their lane, break up and dither away behind the crest, hot where fresh and mid after |
| The splash where it breaks (`RiptideSplash`) | Passed: whole art pixels on the grid (`WhipLimb.Cell`, its node snapped), opaque Water tones; the foam ring is the kept one-shot animation | Unchanged |
| Breaker | Nothing drawn of its own (a shove) | - |
| The trail particles on the bolt | **Failed.** Untextured `GPUParticles2D`, translucent, a screen pixel each, off the grid | Removed from `Riptide.tscn`, and `RiptideWave.FitTrail` with them: the shader's spray and wake behind the crest are the trail |
| Spring Tide's flooded lane (`SpringTideLane`) | **Failed.** A translucent rectangle (alpha 0.28) washing the floor blue, with translucent stroked dashes, turned to the heading | `scenes/shaders/spring_tide_lane.gdshader`, one quad drawn once over the lane's whole run: shallow water in the azure shade and Water edge dithered over the floor - denser down the middle, thinning to the banks, a swell drifting with the current - a broken waterline of azure deep along banks that wander only inward of `HalfWidth`, foam streaks running down it at `DragSpeed`, a lumpy lip of foam on `FrontX` and a Bayer dissolve over the stretch draining behind `TailX`. Those three are the drag's own numbers. On the ground layer as before, now named (`DrawLayers.Ground`) |

### Chain Lightning

| part | before | bar |
|---|---|---|
| the arc of every hop (and Return Stroke's, the same path home) | a `Line2D`, so at any angle but four a smooth rotated quad | fails |
| Storm Surge's wet glow along the arc | a translucent antialiased `Line2D` and a `Tween` a hop, fading by alpha | fails |
| Electrocution's crawl, leap and the light on the held body | antialiased `DrawLine`s under a translucent outline; a translucent `DrawCircle` | fails |
| Cascade | its forks are more arcs: as the arc | fails with it |
| a kill's spark burst (Cascade) | `ImpactDebris` | passes, left alone |
| the scene's `AnimatedSprite2D` (`chain-lightning.png` at scale 0.5) and `TrailParticles` | parked on the chain node, which sits at the world's origin, never moved: drawn there, or nowhere | removed |

Now every stroke is stamped on the world grid by `PixelInk`: the arc a gold Lightning-edge under-stroke
two art pixels wider than a seam of hot light (two pixels and a white-hot core from about one and a half
times the base width), drawn by a top-level `ArcStroke` child from the same bends, for the same
`ArcDuration`; Return Stroke's way home runs white-hot. The glow an arc leaves is a pooled `Afterglow`
on the ground layer - the arc's own points in its ramp's edge, thinning and dissolving through the Bayer
matrix, holding its strength first and going late. Electrocution's crawl and leap are the same gold
edge under a white-hot seam, the light on the body a dithered patch. Storm Surge paints all of it in
the Water ramp, and Shadow Bolt (which runs this script) in the Darkness ramp, where its arc was an
antialiased purple `Line2D`. `ResidualAuraColor` is gone: a glow takes its arc's ramp.

Photographed before (`pb_cl_*`) and after (`pa_cl_*`, a denser burst so the 0.09 s arcs are caught):
after, the base and every pairing at `--zoom=1.6` on the forest, and the base with three pairings that
cover all five cards at both zooms on both floors. Still needs eyes: Cascade's full storm on a dense
screen, the Lattice (the Stormcaller's Apotheosis, `LatticeField.cs`) beside the new arcs - it was not
part of this pass.

### Cinderbreath

**Audit.**

| Part | Verdict |
|---|---|
| The tongue (`FlamePuff`) | **Failed.** A five-frame strip (`tools/art/spell_flames.py`, generated and clean as a sheet) that the engine turned to the tongue's heading and scaled by the jet's width: at any heading but four its art pixels came out rotated and sheared, and at a width of 1.15 or 1.75 uneven - the smear `spirit_weapons.py` draws every heading to avoid |
| The char under Ember Wake's embers, Dragon's Breath's wall and Backdraft's blaze (`CinderGround`) | **Failed.** Squashed translucent `DrawCircle`s and 30-50%-alpha `DrawLine`s as wide as the wall - a dark wash that doubled wherever two overlapped, as embers along a cone do |
| The flames standing in the embers, the wall and the backdraft | **Passed.** `CraterFire`'s sheet (`crater_fire.py`, Meteor Swarm's - the bar), snapped to the grid; Hellbreath repaints it through `ElementRampMaterial` |
| Backdraft's burst | **Passed** (`ImpactDebris`) |
| Cataclysm, the Pyromancer's Apotheosis | **Passed.** `CataclysmField` draws its craters with `crater.gdshader` and `CraterFire` - the bar itself |
| Fire Fan's three jets | The tongue, as above; the aim (each jet at its own enemy, Eric's of 2026-10-28) is untouched |

**New look** (`scenes/shaders/cinder_tongue.gdshader`). A tongue is one quad drawn by a shader that snaps to
the world cell before it turns into the tongue's frame, so its pixels are square and upright at any heading
and any width. **The fire is laid out on the hit**: `HitRadius` - the reach the burn tests, which the jet's
width already scales - is where the flame's round front is, it is eight tenths of it wide either side at its
widest, and everything hotter than the ramp's mid lies inside it; behind it a tail that sways as it flies,
cooler than the fire it trails (the ramp's mid and edge, then soot). Inside, fire streams back through it -
a coarse value noise on whole art pixels carried backward along the tongue breaks the heat into licks - so it
reads as flame rather than as rings (the first try, a disc with concentric stops, read as a fireball). Its
life is still the range indicator, played once over `Lifetime`: a
bud that swells to its full head in a few frames, a full tongue, a tear (cells dropping out through the
dither as the heat steps down the ramp) and a few embers inside the head. One material per ramp, cached
(`FlamePuff.MaterialFor`) - plain fire, or Hellbreath's element - and the tongue's life, radius and seed in
its vertex colour, so every tongue on screen batches. The char under the embers, the wall (a burn every
nine-tenths of its half thickness along it) and the backdraft's bowl is `ScorchGround`, Scorching Ray's.
`assets/bonelight/effects/cinderbreath-flame.png` is no longer drawn by anything; it and its generator
(`spell_flames.py`) were left in place.

Photographed before (`vis_beams_b_*`) and after (`vis_beams_a_*`) with `_UiShot --spell-stage
--cast-on-stage`: the base jet, Backdraft + Dragon's Breath, Ember Wake + Fire Fan, Hellbreath + Fire Fan
and the Cataclysm Apotheosis, at `--zoom=1.6` on the Cursed Dungeon and at the game's zoom on the
Scorched Sands; after shots also at `--zoom=3` and on the Sketchbook's paper, the brightest floor.

### The trap and star spells' shared pieces



Each of the trap and star spells was photographed at its base, at level 8 with each level 4 and
level 8 pairing (and its Apotheosis where a wizard has one), at `--zoom=1.6` and the game's own
zoom, on the Castle floor (stage 1), the Scorched Sands (6) and the Sketchbook's paper (8), before
and after.
These helpers serve only the trap and star spells (Gravewell, Glacial Spike, Iron Palisade, Hollow
Star, Starfall, the Familiar, and the strays assigned with them: Mirefoot's `WispSwarm`, Gale Blade's
`ThunderStrike`, Chain Lightning's `LatticeField`). The shared ones (`ImpactDebris`, `element_ramp`,
`ElementColors`, `DrawLayers`) are untouched.

- **`scenes/shaders/spell_dither.gdshader` through `SpellInk.DitherByCell` / `DitherByTexel`.** Most
  of these canvases already stamped opaque cells on the 2 px grid; what they faded, they faded with
  alpha. Drawn through this material the alpha is coverage: each art pixel is drawn, opaque, or not
  drawn, by a 4x4 Bayer threshold on the world's cell (stamped rects) or on the sprite's own texel (a
  sprite at x2, so a fading pit or a blinking hound loses whole art pixels). One shared material each.
- **`SpellInk.AlignToWorldGrid`.** `WhipLimb.Cell` snaps to the grid of the canvas it draws in, which is
  the world's only when the canvas stands on an even world position. A trap or a well stands wherever an
  enemy was, so its drawing is shifted by under an art pixel onto the world's grid. Nothing tested moves.
- **`scenes/shaders/spell_strokes.gdshader` with `scripts/SpellStrokes.cs`.** Every straight line these
  spells drew was `DrawLine`/`DrawPolyline` - a rotated, smooth quad, translucent besides. A stroke is
  one quad whose fragments snap to the world's cell and measure that cell against the segment: a
  one-pixel spine, a body that can taper, a dithered glow, tones stepped down the ramp along its length
  (`Cool`), the key side of a bar lit and the far side shaded (`Lit`), a dithered tail and dissolve. The
  strokes go to the GPU as a float texture, Stormsweep's way, so nothing is allocated per frame.



### Gravewell

| Part | Audit | Now |
|---|---|---|
| Pit (`gravewell-pit.png`, x2) | Passed as art. Failed two ways: it faded with alpha as it filled back in and as its scar went, and it stood off the world's grid | Dissolves by texel dither; drawn on the world's grid |
| Mass Grave's pit | **Failed**: the 48 px pit drawn at x4, art pixels twice the game's | Its own drawing, `gravewell-pit-mass.png` (96 px cell, three headstones, every pebble the usual size), at x2; the bank still 80 px |
| Collapse (`gravewell_collapse.gdshader`) | Reference; passes | Unchanged; its node stood on the world's grid so its cells meet the floor's |
| Grasp heaps (`gravewell-grasp.png`) | Passed; rounded to 1 px, not 2 | Snapped to the 2 px grid |
| Grave Chill's breath, Mass Grave's drain, Bone Spikes' bones | Passed (opaque cells), off the world grid | On the world grid |
| Quickset's earth (`ImpactDebrisLayer`) | Shared; passes | Unchanged |
| `GravePath` | Draws nothing | - |

`tools/art/gravewell.py` draws the grave at a size `S` (`set_size`); at `S = 1` its output is
pixel-identical to the old generator's under the same Python, so `gravewell-pit.png` and
`gravewell-grasp.png` were not regenerated (under Python 3 both drift from the committed files).
### Glacial Spike

| Part | Audit | Now |
|---|---|---|
| The spike (`glacial-spike.png`) | **Failed**: a flat triangle with a lit line and three white strokes, and its sprite was scaled by Area (1.3, 1.7 - uneven, stretched art pixels), under a 98% modulate | `tools/art/glacial_ice.py`: faceted ice on the obsidian glass's rules - lit and shadow planes, a fold, the ridge's specular as a line, light through the ice beside the ridge, azure shade and deep on the dark side for the cold-element caution - pushed up out of a hole with chips of ice. Never scaled |
| Frozen Spire | **Failed**: the same sprite at about x2.6 | Its own pillar drawing (58 art px, flanked by smaller crystals) at x2 |
| Glacier Crown's ring spikes | **Failed**: the sprite at x0.6 x Area | Their own small drawing at x2 |
| Frost burst (`GPUParticles2D`) | **Failed**: soft, translucent, at a fixed 18 px of its own | Gone; the strike throws its ice out to exactly the radius it hit (`ImpactDebrisLayer`, Ice), which is now what shows the reach Area grows |
| The withdrawing shake | Moved in fractions of a pixel | Whole art pixels |
| Shatter's splinters (`ThrownShards`) | Passed (opaque cells), off the world grid | On the world grid |
| Rimefoot (`frost_patch.gdshader`) | Reference; passes | Unchanged |
| `GlacierCrown` | Draws nothing | - |

`spell_fx.py` no longer draws `glacial-spike.png`; `glacial_ice.py` does, from `build.py`. The level-up
icon (`spell_icons_core.glacial_spike`) is unchanged and still the same one tall spike with a lit spine.

### Iron Palisade

| Part | Audit | Now |
|---|---|---|
| Stakes | **Failed**: `DrawLine`s in a grey no palette holds, rotated with the wall, over translucent black discs | Drawn in world coordinates, upright: three columns of art pixels in the steel row (lit, base, shade) cut to a slanted point of steel's hi, an iron band, occlusion at the foot away from the key. A little taller (26 and 20 px, from 22 and 17 - a height nothing tests), so a palisade reads as one rather than as pegs |
| Spiked Rail's bar and spikes | **Failed**: thin smooth lines, over the stakes | `SpellStrokes`: a bar shaded by the key (top lit, underside shade), spikes tapering to a point; behind the stakes, which are what hits |
| Caltrops | **Failed**: four smooth lines each over a translucent disc, faded with alpha | A caltrop of seven art pixels, lit and shaded, occlusion under it, alternate ones mirrored; dissolve by dither |
| Bleeding | **Failed**: a red at 95% in no palette, rotated cells | The ember row's base and shade, world-aligned, dissolving by dither |
| Lightning Rod's sparks | **Failed**: smooth lines | Four art pixels zig-zagging off the point, the element's core and hot |

### Hollow Star

| Part | Audit | Now |
|---|---|---|
| Core, rim, arms, Eclipse's corona, Black Sun's prominences | **Failed**: `DrawCircle`, `DrawArc`, `DrawPolyline`, all translucent violets | One quad, `scenes/shaders/hollow_star_well.gdshader`, on the world's cell. A hole lit as a bowl (the far lip hot, the near wall dark, occlusion at the bottom); three arms - a hot spine in the ramp's mid with a dithered fringe - laid out exactly as before and ending on `PullRadius`; the corona a dithered, ragged light; seven prominences with white tips |
| The Spirit Mirror (John Dee's Apotheosis) | Had no look of its own | The hole is Dee's obsidian scrying glass: black glass with one hard steel reflection across it (under the form's violet tint, which the well took before too) |
| Motes, Black Sun's count | Translucent cells | Opaque cells with a dark seam, on the world grid |
| Twin Stars' tether | **Failed**: two `DrawPolyline`s | `SpellStrokes`: an occlusion filament round a one-pixel core of the ramp's mid |
| Nova Seed's shards | **Failed**: `DrawLine`s | `SpellStrokes`: a dark sliver with a hot spine cooling behind a white point |
| Collapse debris (`ImpactDebris`) | Shared; passes | Fades by dither in this canvas |
| The heat - the collapse's falloff (0.9.3) | New | In the same shader: the floor between the lip and the collapse radius glows by ordered dither, opaque cells of the ramp, densest and hottest (hot, then mid, then edge) against the core and thinning to nothing at the collapse's edge, in slowly turning bands. It smoulders as the well opens, builds through the pull, and flares as the core shuts (with the hole gone, a disc hottest at its centre). It says where the collapse hits hardest (`HollowStar.ShareAt`: the whole hit at the centre, about a third at the edge) and is never outlined |

### Starfall

| Part | Audit | Now |
|---|---|---|
| Heads, twinkles, fuses, sparks, lit stars | Passed as cells on the grid; failed on their alpha (glows at 55%, fades) | Same cells, faded by dither |
| Falling trails | **Failed**: three `DrawLine`s at the slant every star falls at | One stroke: white-hot at the head, cooling hot to mid to edge, tapering, its last third dissolving |
| Shooting stars' tail and cut line | **Failed**: `DrawLine`s at 30-55% alpha | Strokes: the cut a hot spine in a 30% dither of the ramp's edge as wide as it strikes; the tail as a trail |
| The Constellation's web | **Failed**: `DrawLine`s, a translucent 6 px glow round a 2 px core | Strokes: a hot spine in a dither of the ramp's edge (Light's gold, so the web holds even on the Sketchbook's paper, where white on white vanished) out to exactly `LineHalfWidth`, flashing white on a strike. **Still drawn over the bodies** (Air - 1): a line has to be seen crossing a body |

### The strays: Mirefoot's wisps, Gale Blade's Thunderhead, the Lattice

| Part | Audit | Now |
|---|---|---|
| `WispSwarm` | Passed as cells; faded with alpha | Faded by dither, its dark seam with it |
| `ThunderStrike` | **Failed**: three translucent `DrawPolyline`s and two soft discs | Strokes: a white-hot spine 3 px across, a hot body, the edge as a dithered glow; the knot a white point in a dithered hot glow; the flicker a dissolve |
| `LatticeField` | **Failed**: `DrawLine` arcs and translucent halos | Strokes: hot spines with a dithered edge, kinked as before; each linked body's halo a ring of the ramp's mid at the old halo's edge, dithered to half (a status mark, not an area marker; a fill of dots read as a screen door) |

### The Familiar

| Part | Audit | Now |
|---|---|---|
| The creatures (`tools/art/familiar.py`, x2) | Pass - opaque, on-palette, binary alpha. The dog's toned-down eye is untouched | Unchanged |
| The blink | **Failed**: the sprite faded out and in with alpha | Dissolves by texel dither, whole art pixels |
| Jaws, claws, pecks, puffs, howls, feathers | Passed as cells; failed on their alpha fades | Faded by dither |

### The trap and star spells: what only a build checked, and what still needs eyes

Built, validated (`ContentValidator`: 0 errors, the usual test-wizard warning) and photographed with
`_UiShot` before and after (`vis_traps_*`). Not photographed in the act: the Familiar's blink (0.14 s;
it rides the same texel dither as Gravewell's fading pit, which was), Twin Stars' tether and Nova
Seed's shards only at the game's zoom in a scattered crowd. Still for eyes in a real run: the phone's
portrait canvas - nothing here is layout, but what the strokes and the dither cost on the Pixel 9 is
unmeasured (`--profile` shows Starfall's section) - and whether Glacial Spike's fixed-size spike reads
right in a level 8 crowd now that only its ice debris grows with Area. `.ai/spell-roster.md` still says
Glacial Spike's "drawing follows" its Area: it is the debris that does.

### Cyclone Slash

Eric, of a level 8 Twin Gyres with Wind Shear: *"does not look up to our typical visual fidelity."*

| Part | Verdict |
|---|---|
| The ring's trails | **Failed.** Antialiased `DrawArc` bands at fractional alpha behind each blade - smooth see-through arcs |
| The blades | **Failed.** Soft white `DrawCircle` discs - UI dots, not wind |
| The gust's arcs | **Failed.** Thin stroked `DrawArc`s at alpha, a ring drawn for its own sake inside the whirlwind |
| Twin Gyres' inner ring | **Failed** (the same drawing at a second radius) |
| Typhoon Core, Vortex Reaper | **Failed.** More stroked arcs; Vortex Reaper's pull drew nothing past the ring |
| Wind Shear's crescents | **Failed.** Antialiased arcs at alpha, fading |
| The Endless Chase's throws | **Failed.** An antialiased line at alpha |
| Razor Gale's colour | **Failed.** The persistent node never wore the card's tint (it reached only a node made at level 1), so the Metal path drew in Wind's colours |

**New look** (`scenes/shaders/cyclone_whirl.gdshader`, `scripts/OrbitingBlade.cs`). One quad over the
whirlwind's reach, every fragment snapped to the world cell, opaque from the element ramp (Wind's, or the
path's: Razor Gale's Metal), Bayer the only gradient. A **blade** is a sickle of wind filling the front of
exactly its hit circle (`bladeNow` on `radiusNow`, the numbers its `CircleShape2D` is placed with each
frame): outlined in the ramp's edge on the circle itself, white-hot inside, occlusion along the inside of
the cut, a glint flashing off its point. The outline is section 3's cold-element caution made concrete -
on the Sketchbook's paper a white rim alone was the paper. Behind each blade a dithered **wake** the
blade's width, its sides in the edge, then **streaks** in three lanes cooling down the ramp (never white).
**Twin Gyres'** inner ring is the same drawing a stop cooler, turning the other way. The gust's
**eddies** are short broken streaks inside the ring turning against the spin; **Typhoon Core** adds three
hurricane arms winding in to an empty eye; **Vortex Reaper** draws air spiralling in from as far as its
drag reaches - the wind showing the reach, no ring. **Wind Shear's crescents** and the **Endless Chase's**
throws fly loose of the ring and are stamped by the node in world-grid cells: a sickle as big as the
crescent's hit radius, outlined, breaking up cell by cell over its last third; a line of the wind from
the catch to what it struck, the head a white heart in the edge. Layers (`DrawLayers.Air`), the running
whirlwind (#68) and every hit are unchanged; the quad is redrawn only when the reach moves a cell and
nothing allocates per frame.

Photographed before and after, without `--hold`, for each level 8 pairing (Twin Gyres + Vortex Reaper,
Wind Shear + Typhoon Core, Razor Gale + Typhoon Core, Laelaps' Endless Chase) at `--zoom=1.6` and at the
game's 0.85 on the Enchanted Forest, Wind Shear + Typhoon Core on the Sketchbook's paper (bright) and the
Cursed Dungeon (dark), and Twin Gyres at level 7. **Still needs eyes:** the streaks are one art pixel wide
and faint on the paper, where the outlined blades carry it; whether the whirlwind reads as a ring at a
glance at level 1 on a phone; at level 8 with Typhoon Core the ring is wider than the screen at 1.6.



### Obsidian Spike

Audited from `_UiShot` bursts against a `--spell-stage` crowd at level 8 - Splinter Field + Tectonic
Uprising, Deep Strata + Basalt Lance, Umbral Vein + Tectonic Uprising - at `--zoom=1.6` on the Enchanted
Forest and at the game's 0.85 on the Sketchbook's paper, the Cursed Dungeon and the forest.

| Part | Verdict |
|---|---|
| The glass cluster (`obsidian_sheet`, drawn at x2) | **Passed.** Left alone |
| Splinter Field's splinters, Tectonic's vent spikes, Basalt Lance's followers | **Passed** (the same sheet), but a splinter's spot was rounded to any whole unit, so one on an odd unit sat half an art pixel off the cluster beside it. Now placed on an even unit |
| The cracks (the tell, the eruption, Tectonic's smoulder) | **Failed.** `DrawLine` strokes 2-6 units wide at any angle - thin vector lines, off the grid - fading by alpha, and the smoulder throbbing in alpha |
| The chips | **Failed** (lightly). Opaque glass squares on the node's grid, but the node stood off the world's grid, and they faded by alpha |
| The chips' shadows | **Failed.** Occ at 55% alpha |
| Deep Strata's thrown bodies | **Passed.** The enemy's own sprite on its arc (`Enemy.ApplyLaunch`) |
| Deep Strata's shadows under them | **Failed.** An occ wash at 35-60% alpha, at the body's unsnapped feet |
| The hit's debris (`ImpactDebrisLayer`) | **Passed** (shared) |

**New look.** The node stands on the world's 2-unit grid (`CastAt` snaps it; the hit is measured from
the same point, so the drawing is still the hit). The cracks are stamped cell by cell along the same
jagged paths in three passes over all of them - a lip of occ under the mouth, the glow a stop cooler
three art pixels wide at the mouth and two further out, then the split two art pixels at the mouth
thinning to one - so no crack's glow covers another's split; cooling steps them down the ramp as
before, and going they drop out cell by cell on the Bayer matrix (`PixelInk.Bayer`), Tectonic's
smoulder throbbing in how many cells are lit rather than in alpha. Chips go one at a time, whole.
A chip's shadow is two occ cells on the diagonal of a 2x2; a launched body's is
`GroundSpike.DrawAirShadow`, an oval of occ cells on the grid dithered to the old alpha's density,
smaller and sparser the higher the body is (Black Tentacles' Fling uses it too). The ground layer is
now drawn again only when something on it has changed - the front a cell further, a stop cooler, a
sixteenth more gone, or a shadow moving - instead of every frame. **Still needs eyes:** the cracks are
now heavier pixels than the hairlines were; whether three art pixels of glow at the mouth crowds the
cluster at the game's zoom. Not photographed in this pass: the Sundering Ring (the Apotheosis).

### Meteor Swarm

Audited from `_UiShot` bursts against a `--spell-stage` crowd: the base spell and level 8 with Meteor
Shower + Extinction Event, Magma Craters + Orbital Bombardment and Cosmic Impact + Extinction Event at
`--zoom=1.6` on the Cursed Dungeon (stage 1, dark); Magma Craters + Orbital Bombardment at the game's zoom
on the Frozen Waste (stage 5) and on the Sketchbook's paper (stage 8, the bright floor), with the base there
too. No wizard's Apotheosis rides on it.

| Part | Audit | Now |
|---|---|---|
| The crater (`crater.gdshader`, `CraterGround`, `CraterLayer`) | **Passed** - it is one of the references (2026-10-28) | Unchanged. `DeepWardenBoss` and `BossDeath` share it |
| The fire, smoke and embers in it (`CraterFire`, `crater_fire.py`) | **Passed** - the reference | Unchanged |
| The debris thrown to the radius (`ImpactDebris`) | **Passed** (shared; its last 30% still fades by alpha, which is the shared helper's and was not changed here) | Unchanged |
| The streak (the web demo's four squares) | **Passed** on the grid (`WhipLimb.Cell`, whole art pixels, Fire's ramp). **Failed** on colour under every evolution: the node's modulate - the card's tint at full strength - multiplied the squares, so Orbital Bombardment's head was orange, Magma Craters' and Extinction Event's red, and no evolution's meteor had a white-hot head | `MeteorImpact.TakeTint` takes the tint off the node when it is cast and hands it on softened to the share the crater's fire already takes (`CraterLayer.FireTintShare`, 0.3): the head stays Fire's white-hot core, the three cooler squares and the debris carry the card's colour, and the crater gets the tint it always did |
| Its layer | `ZIndex = 6`, relative | `DrawLayers.Air` with `ZAsRelative = false` - the same layer, named and absolute as the contract asks |
| Cataclysm (the Pyromancer's Apotheosis on Cinderbreath, which opens these craters) | **Passed**: `CataclysmField` draws `CraterGround` and `CraterFire` | Unchanged |

On the Sketchbook's paper the white-hot head is quieter than the tinted orange one was; the hot, mid and
edge squares behind it carry the streak there. Photographed before (`vis_blasts_b_*ms*`) and after
(`vis_blasts_a*_ms*`) in `/root/.local/share/godot/app_userdata/Wizard Survivors/`. Still needs eyes: a full
Orbital Bombardment volley on a phone.

### Kindled Ward

**Audit.**

| Part | Verdict |
|---|---|
| The mote | **Failed.** Three translucent `DrawCircle`s - a halo at 22%, a body at 55%, a core - and on a grown ward (Pyre's small sun) a fourth wash at 9% nearly twice the halo's size |
| The strike's flare and Flare's burst | **Failed.** A fan of `DrawLine`s at fractional widths and fading alpha |
| Guttering in its last second | **Failed.** Every circle's alpha scaled down |
| Rimelight | The same parts in Ice's ramp; failed with them |
| Flare's debris | **Passed** (`ImpactDebris`). Its ramp was the ward's tones plus a 35%-darkened mid that is in no palette; it now takes the ward's own ramp |
| Wisp Trail (`WardWisps`) | **Failed.** Each wisp two squashed translucent circles for the glow pooled under it - a wash that doubled into a brighter lens wherever two overlapped, as nearly all of them do along a trail - and two more for its mote |
| Kindling Flock | The mote and flare, as above |

**New look** (`scenes/shaders/kindled_ward.gdshader`, `scenes/shaders/ward_wisp.gdshader`). A ward is one
quad, every cell opaque from its ramp (Light's, or Rimelight's Ice): a white-hot heart in a teardrop of
the ramp's hot and mid that stands up from it and licks at its tip, a glitter of a few twinkling cells
round it in place of the halo - never a ring or a disc - and on a grown ward a corona of short flickering
rays, held inside 70% of its reach so it is never the strike's edge. **The flare is the reach**: every
strike, tongues of its light lick out to exactly `StrikeRadius`, solid and twice as many when it caught
something, broken every other cell when it burned the air; Flare's burst does the same out to
`StrikeRadius x FlareReachScale`. Each tongue tapers from its root to a pixel at its tip and burns away
root-first, so the tips - the reach - are the last of it; and there are never more than fit with a gap of
two and a half roots between them half way out (`KindledWard.TonguesThatFit`) - the first build packed a
Pyre's flare solid, and it read as a sunburst disc, the marker it must not be. Guttering dissolves it
through the dither from the outside in, the heart last. A Legendary's gold is the node's modulate, kept.
A wisp is one quad of a shared material, its numbers in the vertex colour: flecks of the ward's light
settled on the floor, thickest under the mote and thinning out, the last of them on the radius it sears
and none past it, twinkling a few times a second; squashed into the floor's view as the craters are (it
was squashed to 0.59 before, well inside the circle it hits); and the mote bobbing over it. Light's ward
is pale on the Sketchbook's paper (as Light is on any bright floor; its gold edge carries it), and worth
a look there in play.

Photographed before (`vis_beams_b_*`) and after (`vis_beams_a_*`) with `_UiShot --spell-stage
--cast-on-stage`: the base ward, Flare + Pyre, Wisp Trail + Kindling Flock, Rimelight + Pyre, and Wisp
Trail + Pyre with the crowd 300 px off so the ward drifts and sheds, at `--zoom=1.6` on the Cursed
Dungeon and at the game's zoom on the Scorched Sands; after shots also at `--zoom=3` and on the
Sketchbook's paper, the brightest floor.

### Shadow Bolt

| part | before | bar |
|---|---|---|
| the curse's arc (Spreading Dark's forks and Threefold Curse's three curses are more of it) | an antialiased purple `Line2D` | fails - fixed with Chain Lightning, whose script it runs: stamped in the Darkness ramp |
| the glow left along it | a translucent antialiased `Line2D` and a `Tween` a hop | fails - fixed with Chain Lightning: the pooled `Afterglow` |
| the scene's `TrailParticles` | emitting at the world's origin, where the node sits | removed with Chain Lightning |
| Black Sigil's mark at a cursed body's feet | a translucent `DrawCircle` under an antialiased `DrawArc`, four pairs of antialiased hooks | fails |
| a sigil breaking | a translucent squashed `DrawCircle` under an antialiased `DrawArc`, over `ImpactDebris` | the ring fails; the debris passes |
| Creeping Rot's path | three translucent antialiased `DrawLine`s and translucent circles for bubbles | fails |
| Anathema's pall | seven overlapping translucent `DrawCircle`s, squashed | fails |

Now (`ShadowCurse.cs`, top-level at the origin): each path and pall is one quad from a fixed pool,
painted by `scenes/shaders/shadow_curse.gdshader` out of the reach it curses. A path is a band out to
`PathHalfWidth` from its line, round its ends too, exactly what `CurseWhatStands` measures: mottled
Poison-edge rot toward a ragged edge, the dark of robe violet's deep and occlusion down its spine, a
broken seam of the rot's light and bubbles of it swelling and gone. A pall is a pool of night out to
its radius - a circle, as its curse is, where it was drawn squashed - densest at the heart and thinning
to a rim that drifts in lobes only inward, swirls of the Darkness edge turning in it and motes of its
light lifting off. Both fade in and out by dissolving. A sigil is stamped (`PixelInk.Ring` and `Line`):
a ring of robe violet's deep under the Darkness edge, turning hooks that brighten to the Darkness's hot
light once armed - a mark on one body, so it stays flat to the ground. A breaking sigil throws a front
of the Darkness's light to the burst's radius that thins and dissolves as it goes, over the debris.

Photographed before (`pb_sb_*`) and after (`pa_sb_*`): after, the base and every pairing at
`--zoom=1.6` on the forest, and the base with three pairings that cover all five cards at both zooms
on both floors. Still needs eyes: the sigils are under the bodies by design (`DrawLayers.GroundTell`) and
are hard to see in a packed crowd, as they were; a screen of Spreading Dark and Threefold Curse stacks
many glows at once.

### Black Tentacles

Audited from `_UiShot` bursts against a `--spell-stage` crowd at level 8 - Fling + Shadow Forest, Crushing
Grip + Elder God Maw, Soul Drain + Shadow Forest - at `--zoom=1.6` on the Enchanted Forest and at the
game's 0.85 on the Sketchbook's paper, the Cursed Dungeon and the forest.

| Part | Verdict |
|---|---|
| The rift (`tentacle_rift.gdshader`), Shadow Forest's pools, Elder God Maw's mouth | **Passed.** Left alone |
| The arms, their suckers and the lip of shadow at the root (`WhipLimb`) | **Passed.** Opaque cells on the grid (the node already snapped to it) |
| A slap's landing burst, and Fling's at each landing | **Passed.** A one-shot ring of the rot's light ending on the radius it hit - an animation, not a marker |
| The lash's trail | **Failed** (lightly). Faded by alpha over its last 0.12 s |
| Crushing Grip's coils | **Passed.** Two turns of opaque cells up the legs |
| Fling's thrown bodies | **Passed.** The enemy's own sprite on its arc |
| Fling's arc | **Failed.** Violet squares at 40-90% alpha |
| Fling's shadows | **Failed.** An occ wash at 35-55% alpha at the body's unsnapped feet |
| Soul Drain's wisps | **Failed.** The glow a see-through square at 45%, the mote at 90%, the tail at 17-53% |
| Elder God Maw's dust | **Failed.** Discs of soil at 70% alpha - the translucent brown blobs over the crowd |
| The maw's slabs, clods and rubble | **Passed.** Opaque chunks lit upper left, contact shadow in occ |

**New look.** Nothing is drawn see-through. The lash's trail goes out cell by cell on the Bayer matrix
(`PixelInk.Bayer`). Fling's arc thins in size and steps down the tentacles' ramp toward where the body
was thrown from - one art pixel of the edge, two of the mid, three of the hot with the rot's green at
the heart - and its shadow is Deep Strata's (`GroundSpike.DrawAirShadow`): dithered occ cells on the
grid, smaller and sparser the higher the body is. A wisp's tail thins the same way down the rot's ramp;
its glow is a ring of edge cells dithered to half round a mid mote with its corners off and a
white-hot heart, all concentric cell for cell. The maw's dust is the same soil discs as opaque cells
dithered to the density the alpha was, thinning as it settles - still the one thing here you see
through, because it is air, but seen through the gaps. **Still needs eyes:** the dust's dither reads as
a mesh at `--zoom=1.6`; whether it reads as dust at the game's zoom on a phone.

### Frost Shard

| part | before | bar |
|---|---|---|
| the shatter, a shard's landing, a pool's freezing burst | `ImpactDebris` | passes, left alone |
| Avalanche | more shatters, a beat apart | passes with the shatter |
| Frost Nova Burst's freeze | the enemy's own frozen look (`Enemy`), not this spell's drawing | not this spell's |
| Ice Statues | the corpse's frame under an over-bright multiply, (0.9, 1.2, 1.6) and a (1.6, 1.8, 2.0) flash - colours on no ramp; cracks as antialiased `DrawLine`s fading by alpha | fails |
| Ice Flurry's shards in flight | an antialiased `DrawLine` under a translucent outline and a `DrawColoredPolygon` point | fails |
| Meltwater's pool | two translucent squashed `DrawCircle`s and an antialiased `DrawArc` | fails |
| the spikes a pool freezes into | translucent `DrawColoredPolygon`s and lines | fails |
| `FrostShard.tscn` (`frost-shard.png` on `ElementalBolt`) | held by `Player.FrostShardScene` and never instantiated since Frost Shard became a shatter: nothing on screen | not drawn |

Now (`FrostShatter.cs`, top-level at the origin): a statue is still exactly its corpse's frame on the
corpse's own grid, but drawn through `scenes/shaders/frost_statue.gdshader`, which maps every texel by
its brightness onto the four Ice stops (as `element_ramp.gdshader` remaps a sheet into another element),
spreading the horde's dark range over all four so a body keeps its shading in ice. It frosts over as a
dissolve across the frame's own texels and climbs toward white-hot as it nears the break; the call's
colour carries both numbers, since one node draws every statue. Its cracks, the shards in flight (a deep
ice streak, a hot sliver, a white-hot point) and the spikes a pool bursts into (the deep ice at the foot,
the mid up the blade, a white-hot edge on the key's side) are stamped by `PixelInk`. A pool is one quad
from a fixed pool, painted by `scenes/shaders/meltwater.gdshader` out to the reach its freeze measures -
a circle, where it was drawn squashed: the deep water dithered thin enough for the floor to show
through, ripples running out from where the body fell broken into short arcs so they never read as
rings round a target, the ice's light on the lip and sheen facing the key and nowhere else (so no line
runs round it), spreading out in its first moments and drying from the edge in, dithered.

Photographed before (`pb_fs_*`) and after (`pa_fs_*`), with Magic Missile beside it to do the killing
(a shatter needs a death): after, the base and every pairing at `--zoom=1.6` on the forest, and the base
with three pairings that cover all five cards at both zooms on both floors. Still needs eyes: statues of
every chapter's cast (a sprite whose darkest and brightest sit outside the horde's usual range will map
flatter), the meltwater pools under a dense fight, the shards at the game's zoom.

### Cone of Cold

Audited from `_UiShot` bursts against a small `--spell-stage` crowd set off the diagonal (so the spray
is thrown on a turned heading and shows against the floor) at level 8 - Deep Freeze + Zero Point
Shockwave, Arctic Blast + Glacier Tide, Ice Shards + Zero Point - at `--zoom=1.6` on the Enchanted Forest
and at the game's 0.85 on the Sketchbook's paper, the Cursed Dungeon and the forest. The burst runs past
the cone's cooldown so its second cast is caught from its first frame.

| Part | Verdict |
|---|---|
| The spray of ice (`ImpactDebris`, every milestone, Arctic Blast's half circle, the Long Winter's ring) | **Failed.** The node took the cast's heading as its rotation, so every square of ice was drawn in a turned frame: on any heading but the four axes the shards were tilted diamonds off the world's grid. Its last third faded in alpha |
| Zero Point's star of ice at the tip | **Failed** for the same reason - a cross turned with the cone |
| Zero Point's second spray | **Failed**, the same way as the first |
| The frozen and slowed bodies | Not this spell's drawing (the status marks) |

**New look.** The node is never turned and stands on the world's grid; the spray is thrown at the
heading's angle from the exact cast point, under a cell off the node, so the reach is still measured
from where the hit is and every shard is an upright square on the grid. The star stands upright too, a
four-pointed sparkle of whole art pixels on the grid. The node draws through `SpellInk.DitherByCell`,
so the shared burst's alpha fade reads as coverage and a shard dissolves cell by cell instead of
going see-through. **Still needs eyes:** on the Sketchbook's paper the spray is faint while it flies -
`ImpactDebris` paints a shard core-white and then the ramp's pale hot for the first third of its life,
and Ice's core and hot are the paper's own colour; only the mid and the edge, as it lands, read there.
That is the shared burst's palette and every cold spell that throws one has it; it wants a lit and
shaded shard (an edge on the lower right) in `ImpactDebris` itself rather than a fork for one spell.

### Spiritual Weapon

**Audit.**

| Part | Verdict |
|---|---|
| The weapons - axe, hammer, sword and the colossal greataxe, maul and greatsword, Whirling Axes' spin | **Passed** as drawn: `tools/art/spirit_weapons.py`'s sheets, one cell per heading on the elite and boss cells, drawn at x2 and snapped to the grid. Kept |
| Coming and going | **Failed.** A weapon faded in over 0.18 s and out over 0.2 s through its modulate's alpha |
| Afterimages through a strike | **Failed.** The sheet tinted Arcane's mid and hot at 22% and 38% alpha - a translucent wash, and the gold haft muddied by the multiply |
| The slash | **Failed.** Translucent `DrawLine`s between snapped points: a rotated stroke at any angle but four |
| Motes (summon puffs, sparks, Celestial Greatsword's light, Hallowed Ground's rising motes) | Squares on the grid - **passed** in shape, **failed** in their fade, which was alpha |
| The shadow under each weapon | **Failed.** Rows of rects in occlusion at 30% alpha - a translucent wash |
| The hammer's quake (and Aftershock's echo) | **Failed.** Cracks drawn as 4 px of occlusion at 75% under 2 px of Arcane, `DrawLine`s between snapped points - rotated, translucent strokes - and nothing showed the ground heaving |
| Hallowed Ground | **Failed** in its fade (rune cells through alpha), and its reach was shown by nothing: the runes sat between 55% and 90% of the radius it Weakens and slows |
| Blows' debris | **Passed** (`ImpactDebris`) |
| Layers | Kept: the hammer in the air (`Air`), its quake under the bodies (`GroundTell`, -1), as Eric asked (spell-roster 2f) |

**New look** (`scenes/shaders/spirit_weapon_air.gdshader`, `scenes/shaders/spirit_quake.gdshader`). The
sheets stay. Every canvas the weapon draws on now goes through a shader that turns the ALPHA a draw is
made with into dithered coverage of the world's 2-unit cell: a weapon dissolves in and out; an afterimage
is a stipple of the weapon's own shape filled with Arcane's mid and hot (a separate canvas, under the
weapon, `silhouette` on); motes break up as they cool; the shadow is a half-tone of occlusion that thins
the higher the weapon is. The slash is square cells stamped along the head's own path, a cell every half
its size, wide and white at the head and narrowing and cooling down Arcane's ramp behind it. **The
quake** is one quad a slam through `spirit_quake.gdshader`, its numbers in the vertex colour: seven
jagged cracks out to exactly the radius the slam hit, a seam of light a pixel wide inside a lip of
occlusion, white-hot at the heart while fresh and cooling down Arcane's ramp; a pit of broken ground
where it struck; and a band of broken ground thrown out to the radius in a sixth of a second that falls
away as it arrives. Aftershock's echo is the same with finer cracks and no pit. **Hallowed Ground** is
holy light scattered evenly over the whole zone, a little thicker toward its heart, with ten
three-by-three rune marks inside; its reach - where it Weakens and slows - is where the light stops. (The
first build gathered a band of it on the rim, and it read as a dotted ring: a marker.) It dissolves
through the dither as it ends. The quake and the holy ground are squashed into the floor's view as the
craters are. The spell's tint is a uniform on the quake and in the colours of everything else. On the
Sketchbook's paper the holy light is pale against the page, as Light is on any bright floor; the cracks
carry the slam there.

Photographed before (`vis_beams_b_*`) and after (`vis_beams_a_*`) with `_UiShot --spell-stage
--cast-on-stage`: each form at level 1, Broadaxe + Executioner's Greataxe, Headsman + Whirling Axes,
Hallowed Ground + Titan's Maul, Aftershock + Hammerfall, Riposte + Celestial Greatsword and Twin Blades +
Thousand Cuts (every card), at `--zoom=1.6` on the Cursed Dungeon and at the game's zoom on the Scorched
Sands; after shots also at `--zoom=3` and on the Sketchbook's paper, the brightest floor.

**2026-10-30** (`.ai/spell-roster.md` 2f, "the feel pass after 0.9.2"): one new sheet,
`spirit-dagger.png` - Twin Blades' dagger, the sword's make on the 32 cell, 32 headings, grip 8.5 art px
behind the centre and head 7.5 ahead (`tools/art/spirit_weapons.py --only=dagger`). The hover is whole art
pixels (three up and down, one side to side, a heading cell of rock), so nothing new is drawn smooth. The
Twin Blades and Deflection (Riposte's id) icons were redrawn: a sword and a smaller dagger, and a bolt
broken on the blade with its pieces thrown aside rather than struck back.

### Molten Shard

Audited as Riptide was: the base spell and level 8 with all six pairings (Slag Trail, Splitting Shard or
Magma Core with Caldera or Forge Swarm), at `--zoom=1.6` on the Cursed Dungeon and the game's zoom on the
Frozen Waste (which photographs dark: it is night there), `--ripen-every=12` so a Forge Swarm ring fills
and flies inside one burst; and, for a pale floor, the base and three pairings that show every card at
the game's zoom on the Sketchbook's paper (stage 8).

| Part | Audit | Now |
|---|---|---|
| The shard in flight (and Splitting Shard's halves, the same scene at 0.72) | **Failed.** `molten-shard.png`, a 32x32 bar, turned to its heading - its pixels on a rotated grid at every angle but four - multiplied by an orange tint (1, 0.55, 0.35) on top of the card tints, which pushed the Metal ramp off the palette | `scenes/shaders/molten_shard.gdshader` on one quad (`MoltenShardLook`, the scene's `Look` child; the sprite is gone from `MoltenShard.tscn` and `molten-shard.png` is no longer referenced by a scene). A splinter of steel tumbling end over end: its rim lit by the key on one side and in Metal's edge stop on the other, a seam of molten Fire down its spine, a white-hot point and a torn end still glowing, and an art pixel of Fire's edge hugging it - a glow on the dark floors, and the contour that keeps pale steel from vanishing on the pale ones (it did, on the Sketchbook's paper, until it had one). Its half-length is `HitRadius` plus a body - the reach at which an enemy's centre is struck, as the wave and the Gale Blade are drawn - which is also the length the sprite was on screen |
| Its trail | **Failed.** Untextured `GPUParticles2D`, translucent (alpha 0.85), off the grid | In the shader: a one-pixel streak of slag behind it cooling down the Fire ramp, and sparks shed behind it - each one art pixel, its position snapped to the world's cell, stepping core, hot, mid, edge as it falls back |
| Magma Core, lodged | **Failed** (the same sprite, swelling) | The same shader, lodged (`ElementalBolt.IsLodged`, `LodgeFuse`): the shard heats through the Fire ramp toward white as the fuse burns, throbs white-hot at the end, and spits sparks round itself inside its own reach. The swell is still the node's scale |
| Magma Core's eruption | The shared `ImpactDebrisLayer`, on the grid, but spawned with the bolt's modulate - the card tint multiplied over the Fire ramp | Spawned untinted: the Fire ramp as drawn, like every other burst of these four spells |
| Forge Swarm's ring | **Failed** (the sprite, at 1.4, turned point-out) | The same shader, forging (`IsForging`): held still, point out, glowing a step hotter, no trail until it is loosed |
| Slag Trail (`MoltenSlag`, trail) | **Failed.** Three passes of translucent `DrawCircle` and `DrawLine` per point - antialiased, their overlaps doubling in alpha - and embers as fading translucent squares; the lumps were jittered off the line by up to 1.5 times the half-width they burn | `scenes/shaders/molten_slag.gdshader`: one quad a droplet (`DrawPrimitive`, its offset in the UV and its heat, seed and radius in the vertex colour, one shared material) and a smaller drip between each and the next. A lump of crust in the red row's dark stops, lit on its upper left, with molten metal in it that cools white, hot, mid, edge as the point ages and shrinks as the crust closes, cracks of glow in the crust while it is warm, an ember flicked up off some of the hot ones, and a Bayer dissolve as the last of it goes out. Every droplet now stays inside `SlagHalfWidth`, the width its burn tests |
| Caldera (`MoltenSlag`, pool) | **Failed.** Four translucent sixteen-sided polygons stacked crust to heart - their wobble reaching 4% past the radius it burns - three translucent crust plates and antialiased `DrawArc` bubbles | `scenes/shaders/caldera.gdshader`, one quad: a bank of crust in the red row lit on its upper left, wandering only inward of `PoolRadiusNow` (the disc it burns); plates of crust turning on the melt (a Voronoi a quarter of the pool across) with the Fire ramp glowing through the seams, white-hot at the heart and stepping down toward the bank; bubbles that swell into a ring of white-hot metal and burst. In its last second the plates spread and the seams close and cool; in its last moment it dissolves from the bank in |
| Splitting Shard's split | Nothing drawn of its own beyond the two halves | - |

Not changed, and why: `spell_fx.py` still draws `molten-shard.png` (the generator is shared with other
spells' sprites, so the entry was left rather than pull a shared file into this commit); nothing loads it.

### Gale Blade

Audited as Molten Shard was: the base spell and level 8 with all six pairings (Eddy, Hooked Throw or Live
Wire with Thunderhead or Squall), at `--zoom=1.6` on the Cursed Dungeon and the game's zoom on the Frozen
Waste, and the base with two pairings that show every card at the game's zoom on the Sketchbook's paper.

| Part | Audit | Now |
|---|---|---|
| The blade, out and home (and Squall's four extra blades, the same scene) | **Failed.** `gale-blade.png`, three arcs, spun with the bolt at sixteen radians a second - its pixels on a rotated grid at every angle but four - at 95% opacity, under the card tints | `scenes/shaders/gale_blade.gdshader` on one quad (`GaleBladeLook`, the scene's `Look` child; `gale-blade.png` is no longer referenced by a scene). A crescent of wind - a disc less a disc set behind it, so its belly leads and its horns sweep back - its cutting edge white, then Wind's hot and mid, its inner edge in Wind's saturated edge stop, and an art pixel of that edge round the cutting edge too - a cold glow on the dark floors, and the contour that keeps a white edge from vanishing on the pale ones (it did, on the Sketchbook's paper, until it had one). Drawn to the swath it cuts, `HitRadius` plus a body, as the Riptide wave is; the horns are on that radius, so as it spins it sweeps what it cuts |
| Its trail | **Failed.** Untextured `GPUParticles2D`, translucent, off the grid | The spin is drawn instead: two ghosts of the crescent a little behind it in its turn, a ramp stop and two down, dithered thinner each time |
| Eddy, hanging at the end of the throw | **Failed** (the sprite, swollen and spun faster); nothing showed the reach its recut scans, which is wider than the swollen blade | The same shader: the blade swells with the node, and rings of wind streaks wheel round it out to `ElementalBolt.EddyReachNow` - the outermost ring's pixels on the reach its recut scans - growing in with the swell. The stuff that cuts, laid out to the radius it cuts, not a ring drawn round it |
| Hooked Throw | Nothing drawn of its own (a path) | - |
| Live Wire's wire | **Failed.** Three stroked polylines from the hand - a 5 px band of Lightning's edge at 55% alpha under a hot and a white line - at the blade's angle, drawn from 18 px above the point the cut measures from | `scenes/shaders/live_wire.gdshader` on one quad (`LiveWireLook`, a top-level child of the blade it spans every frame): a white-hot core one art pixel wide, a hot pixel either side where it kinks, the odd spark of mid and edge, re-jagged every 0.06 s as before. It now runs along exactly the segment the cut measures (the wizard's position to the blade), leaving him at the edge of his body rather than across his sprite, and every kink and spark stays inside `LiveWireHalfWidth`, the band it cuts |
| Thunderhead's strike (`ThunderStrike`) | **Failed** (stroked translucent polylines, soft discs) | Done in the strays pass above (`SpellStrokes` strokes, a dithered glow, a dissolve; commit 4327ca5), which landed first; this pass left it as that one made it |
| Thunderhead's burst | Passed - the shared `ImpactDebrisLayer` | Unchanged |
| Squall's burst into five | Nothing drawn of its own beyond the blades | - |

### Solar Flare

Audited from `_UiShot` bursts against a `--spell-stage` crowd, the wizard standing still so the flare
builds: the base spell (crowd at arm's length so a pulse lands in the burst), Sun Dogs alone at level 5
(at Daybreak's reach the mock suns hang off the screen), and level 8 with Sun Dogs + Daybreak Avatar,
Solar Prominence + Corona Discharge and Blinding Flash + Corona Discharge, at `--zoom=1.6` on the Cursed
Dungeon; Prominence + Corona at the game's zoom on the Frozen Waste (stage 5) and the Sketchbook's paper
(stage 8, the bright floor), with the base and Sun Dogs on the paper too. No wizard's Apotheosis rides on it.

| Part | Audit | Now |
|---|---|---|
| The burst (`SolarFlareVisual`) | **Failed.** Two `DrawArc` hoops at 95% and 60% alpha - antialiased, and held on the radius for the back half of every burst, which in a still frame is a ring round the reach - translucent spoke wedges, `DrawCircle` embers and a soft kernel | `scenes/shaders/solar_flare.gdshader`, one quad: every fragment snaps to the world cell and is measured from the snapped centre. A white-hot kernel for the first instant; rays of Light from hot at the root to Fire's mid at the tip, long and short by turns, never past 0.95 of the reach; a front that leaves white-hot, cools through gold to Fire's orange and lands on the radius as it breaks up by dither; a dimmer burnt wave behind it; embers riding out, cooling down Fire's ramp, inside the reach by their own size. The same numbers as before - reach, duration, ray and ember counts, spin - so a pulse is as wide and as quick as it was |
| Corona Discharge's beams | **Failed.** Translucent quads under an antialiased `DrawLine` | In the same shader: each beam at exactly the half width and length `FireCoronaBeams` hits, a white-hot spine, gold and orange to its edges, a knot of light running out along it; it thins only after it has struck, and goes out by dither |
| The charge at his boots | **Failed.** Two soft translucent discs squashed onto the floor and circle motes - and drawn in the air, over his boots | The same shader on a second material, on a child at `DrawLayers.GroundTell` (under every body, where the Brazier's `StanceGlow` is): a dithered pool of light that widens, fills and whitens as the charge climbs, a sparse gold halo, motes one art pixel each drawn in to it |
| Sun Dogs' mock suns (`ElementalPulse.DrawSunDogs`) | **Failed.** Translucent `DrawCircle` halo and glow, antialiased `DrawLine` rays, a soft kernel | Stamped cell by cell round the sun's snapped centre, on the world grid: a white kernel, a hot ring, a dithered glow, a sparse halo, four one-cell rays turning (`PixelInk.Bayer` for the dither) |
| Sun Dogs' bursts | The burst, at 0.6 of the reach | The shader, as the burst (10 rays, 8 embers) |
| Solar Prominence's loops | **Passed.** Rects stamped on the 2 px grid (`WhipLimb.Cell`) in Fire's ramp, on a canvas at the world's origin | Unchanged |
| A prominence's landing | **Passed** (`ImpactDebris`) | Unchanged |
| Blinding Flash, Daybreak Avatar | Nothing of their own: a stagger on the enemy, and a bolt burnt away by `EnemyProjectile` | - |

The node's modulate - a Legendary's gold, a card's warmth - still multiplies the flare, as it always did:
the cards' tints are pale yellows over a white and gold ramp. On the paper the Light stops are quiet and
the gold and orange edges carry it. Photographed before (`vis_blasts_b_*sf*`) and after
(`vis_blasts_a*_sf*`). Still needs eyes: Daybreak's full-screen flare on a phone, where a dithered front
of that size is the most fragments this pass draws in one quad.

### Fireball

Audited from `_UiShot` bursts against a `--spell-stage` crowd: the base spell and level 8 with Pyroclasm +
Hellfire Nova, Cluster Bombs + Armageddon Sun and Iron Comet + Hellfire Nova at `--zoom=1.6` on the Cursed
Dungeon (stage 1); Pyroclasm + Hellfire Nova at the game's zoom on the Frozen Waste (stage 5), and that and
the base on the Sketchbook's paper (stage 8). No wizard's Apotheosis rides on it.

| Part | Audit | Now |
|---|---|---|
| The ball, every milestone | **Failed.** `fireball.png` (`spell_fx.py`'s 32 px cell saved at x2) at a sprite scale of 1.35 to 1.9, times the node scale Hellfire Nova (x2.2) and Armageddon Sun (x2.0) give it, turned to the heading: art pixels 2.7 to 8.4 world px wide on a tilted grid. Each card's tint was multiplied over it, so Hellfire Nova's ball was a flat red disc with no heart | `scenes/shaders/fireball.gdshader` on the Fireball node, snapped to the world cell before it turns into the ball's frame. The white-hot heart is exactly the collision circle; the flame round it is the old sprite's size (its exports still set it), leads the heart by little and streams back in rippling tongues, its edge torn in clumps of two art pixels, sparks shed off the back. **Painted per card, not tinted:** Pyroclasm crusts over into plates of char with the melt glowing between them (a lava bomb); Cluster Bombs carries its three bomblets round inside the flame, iron shells lit from the key; Iron Comet's heart is an iron ball lit from the key with a glowing rim; Hellfire Nova burns down into the red rows with soot off its tail; Armageddon Sun is a round sun with a corona; a Legendary's flame is gold |
| Its trail | **Failed.** `GPUParticles2D` with no texture - one world px each - in (1, 0.45, 0.1, 0.8), on no ramp | Removed from `Fireball.tscn`: the shader's tongues and sparks are the trail |
| The blast | **Failed.** Twenty-four more of those particles. The reach the damage tests - 165 px at the base, 412 at Hellfire Nova - was drawn by nothing | `scripts/FireburstLayer.cs` (one pooled node per arena, in the air, every burst one quad of one material) through `scenes/shaders/fireburst.gdshader`: a white-hot flash at the heart, capped at 30 px so a wide blast is not a white screen, then a band of fire puffs - no wider than a body or two - thrown out to exactly `explosionRadius` in a sixth of a second, hottest along its leading edge, the middle burnt out behind it, breaking into puffs and then up by dither from the outside in, gone in 0.42 s. `ImpactDebris` is thrown to the same radius with it. Hellfire Nova's band runs down the red rows with soot, Armageddon Sun's heart is white light, a Legendary's is gold |
| Cluster Bombs' bomblets | Debris only (`ImpactDebris`) - on the grid, so passing as debris, but with no fire where the main blast now has it | Each bomblet also bursts in fire to exactly the radius it hits, on the same layer |
| Pyroclasm's pool of magma | **Failed.** `LingeringZone` drawing itself: two translucent polygons and a soft bubble - at Hellfire Nova's size a 250 px red wash | `scripts/BlastPoolSurface.cs` on the zone (which no longer draws itself) through `scenes/shaders/magma_pool.gdshader`, on the ground under the bodies: melt under drifting plates of char crust, the seams wide and white-hot in the middle and thin toward a bank of charred floor, bubbles breaking the skin, the crust closing and the glow sinking as it ages, and a dissolve from the bank in at the end. The bank wanders only inward of `Radius` |

While this was in hand another session converted `LingeringZone`'s own drawing to a generic pool
(`lingering_pool.gdshader`, "the Lingering Rift ... still needs eyes"). These two zones now draw through
`BlastPoolSurface` with `DrawsItself` false, so that shader has no caller; it was left in place.
`fireball.png` is no longer drawn by anything; it and `spell_fx.py`, which draws other spells' sprites too,
were left in place. Photographed before (`vis_blasts_b_*fb*`) and after (`vis_blasts_a*_fb*`). Still needs
eyes: Hellfire Nova's blast on a phone, where its band is most of the screen for a fifth of a second.

### Arcane Explosion

Audited from `_UiShot` bursts against a `--spell-stage` crowd, with Magic Missile beside it so its charge
comes due (`--ripen-every=3`): the base spell and level 8 with Dual Shockwave + Supernova, Static Nova +
Event Horizon and Lingering Rift + Supernova at `--zoom=1.6` on the Cursed Dungeon; Lingering Rift +
Supernova at the game's zoom on the Frozen Waste, and that and the base on the Sketchbook's paper. No
wizard's Apotheosis rides on it.

| Part | Audit | Now |
|---|---|---|
| The blast, every milestone | **Failed.** `spell_fx.py`'s 32 px ring, saved at x2 and shown at `range / 32` - x3.1 at the base reach, about x7 under Supernova - so its art pixels were 6 to 14 world px wide; every card's tint multiplied over it | `scenes/shaders/arcane_blast.gdshader` on the ArcaneExplosion node, drawn from `range`, the distance `TriggerExplosion` tests: a flash at the heart capped at 22 px; a front of force half a body wide, nine lobes that only ever fall back from it, white-hot on its leading edge and cooling to the ramp's violet behind, landing on the radius a quarter of a second in and breaking up by dither; sparks of it thrown ahead that fall inside the radius, a few motes in its wake; the middle clear, so the wizard is seen in his own spell. **Painted per card:** Dual Shockwave throws a second, thinner front a beat behind the first; Static Nova's front crackles with Lightning; Supernova's burns down Fire's ramp behind its white edge; Event Horizon's - whatever pulls inward - starts on the radius and collapses into a small dithered dark heart, which is the pull it does; a Legendary's front is gold |
| The ring of particles under it | **Failed.** 64 white `GPUParticles2D`, one world px each, on an emission ring that grew to the range | Removed from `ArcaneExplosion.tscn` |
| Lingering Rift | **Failed.** `LingeringZone` drawing itself: a translucent violet disc, screen-wide under Supernova | `BlastPoolSurface` through `scenes/shaders/arcane_rift.gdshader`, on the ground: a hole torn in the floor at the middle, dithered dark and lipped with arcane light; arms of the light wound out of it, turning slowly in toward it, their width in world px so a wide rift has longer arms rather than fatter ones and the floor shows between them, their ragged ends falling short of `Radius`; a flare on every half-second bite, so it pulses on the beat it hurts on; a dissolve from the edge in at the end. No lip at the reach - a ring of flakes there read as a dotted outline |

`arcane-explosion.png` is no longer drawn by the scene (`SpellData_ArcaneExplosion.tres` still carries an
unused `AtlasTexture` of it); it and `spell_fx.py` were left in place. Photographed before
(`vis_blasts_b_*ae*`) and after (`vis_blasts_a*_ae*`). Still needs eyes: the blast's real cadence in a run
(the harness detonates it far more often than play does), and Supernova's front on a phone.

### Hunter's Draw

Audited as Molten Shard was: the base spell and level 8 with all six pairings (Heavy Draw, Running Shot
or Barbed with Rain of Arrows or Hunter's Mark), at `--zoom=1.6` on the Cursed Dungeon and the game's zoom
on the Frozen Waste, and the base with two pairings that show every card at the game's zoom on the
Sketchbook's paper - the wizard walking to draw and stopping to loose (`--walk-from`, `--walk-until`).
Every part of it failed the bar except the shared debris bursts; on the paper the occlusion backing every
arrow and the bow already carried (the old outline's job) holds them, so nothing needed a contour.

| Part | Audit | Now |
|---|---|---|
| The arrow in flight (Heavy Draw's heavier ones and Running Shot's snap shots are the same scene) | **Failed.** `hunters-arrow.png` at a scale of 1.5 - a fractional scale, so an art pixel was three device pixels and a half - turned to its heading and multiplied by an over-bright cyan (0.85, 1.35, 1.75) | `scenes/shaders/hunters_arrow.gdshader` on one quad (`HuntersArrowLook`, the scene's `Look` child; `hunters-arrow.png` is no longer referenced by a scene): the arrow the bow had on the string, the same length and head - a Metal head, white at the point and lit on the flank toward the key, a Wind shaft and fletching, one art pixel of occlusion behind it all. Its point is on the front of the collision circle, so it strikes what its point touches; Heavy Draw's scale grows the arrow and the circle together |
| Its trail | **Failed.** Translucent `GPUParticles2D` off the grid | In the shader: a wake of Wind streaks off the fletching, dashes sliding back and dithering away |
| The bow (`BowDrawVisual`) | **Failed.** Antialiased polylines on translucent outlines, a smooth polygon head, two soft circles for the full-draw glow, turned with the aim onto a rotated grid | `scenes/shaders/bow_draw.gdshader`, one quad in the bow's frame, still bending continuously with the charge because the shapes are measured, not stamped: limbs in the Earth ramp lit toward the key, a string of bone one art pixel wide, the arrow in its charge ink (Metal's dull stops while stopping would only relax the bow, Wind's mid once it would loose, Wind's hot and white at full draw), occlusion behind limbs and arrow, and at full draw a white knot on the point with a dithered spray of hot round it that throbs with the strain |
| The aim brackets on the target (`BowDrawVisual`'s mark) | **Failed.** Antialiased polylines | Grid-snapped rectangles on a grid-snapped node: an art pixel of occlusion under one art pixel of ink (two at full draw), the ink the arrow's, flashing white with the strain. A mark on one body, not an area |
| Hunter's Mark (`HuntersMark`) | **Failed.** Four rotated polygons over a 90% outline | `scenes/shaders/hunters_mark.gdshader`, one quad on the quarry: four heads turning round it, the arrow's Metal-white points in, Wind flanks lit toward the key, an art pixel of occlusion; it dissolves through the Bayer matrix as the mark runs out |
| Rain of Arrows (`ArrowRain`) | **Failed.** Stroked lines, polylines and polygons over a translucent outline, fading by alpha | The arrow shader in its many-arrows mode: one `DrawPrimitive` quad an arrow, its heading, how much of it is in the ground and how much is left in the vertex colour. The volley climbs, falls with each point coming down exactly on the spot it strikes, and stands there with its head buried, dissolving. The node's tint is dropped (it would land in the vertex colour); a Legendary's gold is the shader's own |
| Rain of Arrows' strikes | Passed - the shared `ImpactDebrisLayer` | Unchanged |
| Barbed's lodged arrows (`LodgedArrows`) | **Failed.** A stroked shaft and fletching over a translucent outline, a soft circle for the wound, a translucent drop | The same many-arrows mode with the wound on: the arrow with its head buried in the body, a bead of blood at the wound and a drop falling from it each tick in the red row's lit and base stops; it tilts and dissolves as it works loose |
| Running Shot | Nothing drawn of its own beyond its arrows | - |

Not changed, and why: `spell_fx.py` still draws `hunters-arrow.png` (shared generator); nothing loads it.
The loosed arrow is now the bow's own 52 px arrow rather than the old sprite's 81 at a scale of 1.5.
