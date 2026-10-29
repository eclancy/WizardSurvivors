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
shader on one quad (`thorn_vine.gdshader`), its Thicket and Entangle stamped in ramp stops.

`ScorchingRayBeam` no longer has it either (2026-10-29): it is a shader on the world's 2-unit cell, and
its scorch another. Nor do Cinderbreath's tongues (`FlamePuff`, a sprite strip the engine turned and
scaled, now a shader) and the char under its fire (`CinderGround`, the scorch's shader). Section 9, below,
has the audit and the new look spell by spell.

`CycloneSlash` (`OrbitingBlade`) no longer has it (2026-10-29): the whirlwind - both rings' blades and
their wake, the gust's eddies, Typhoon Core's arms, Vortex Reaper's in-drawn wind - is one quad drawn by
`scenes/shaders/cyclone_whirl.gdshader` on the world's 2-unit cell; Wind Shear's crescents and the
Endless Chase's throws are stamped in world-grid cells. Section 9 has its audit.

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

### Riptide

Audited from `_UiShot` bursts against a `--spell-stage` crowd: the base spell, level 8 with all six level
4 + level 8 pairings, and Spring Tide (Tituba's Apotheosis), each at `--zoom=1.6` on the Cursed Dungeon
(stage 1, dark) and at the game's zoom on the Frozen Waste (stage 5, pale). Spring Tide's lane was also
photographed on its own over a dark and a pale floor, since the Tidal Wall that always comes with it
covers it in every game shot.

| Part | Audit | Now |
|---|---|---|
| The wave, with Groundswell's growth, Brackish Tide's sea, Tidal Wall's titanic form and Trident's fan (`riptide_wave.gdshader`) | Passed - it is one of the references | Unchanged |
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
