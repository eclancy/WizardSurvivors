# Art replacement manifest

Every asset that has to be redrawn to put the game on the Bonelight contract, what it is today,
and what replaces it. Counted from the tree at `ca39186` (after the ranged-enemy consolidation).

Three docs, three jobs — don't confuse them:

- `.ai/art-inventory.md` — what art we **own**, and what is wired up vs. sitting unused.
- `.ai/art-direction.md` — the **contract**: grid, light model, palette, silhouette rules.
- **this file** — the **backlog**: what still has to be drawn, in what order, and how much of it.

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
| `OrcEnemyFrames` | bruiser | 22 frames, 4 anims | **Shared with ForestTreantBoss at ×6** |
| `SoldierEnemyFrames` | miniboss | 22 frames, 4 anims | Has a baked-in drop shadow nothing else has |
| `BooEnemyFrames` | flyer miniboss | **2 frames, `moving` only** | No attack/hurt/death; 2-colour cartoon outline, alien to everything |
| `CultistEnemyFrames` | caster | **4 frames, `moving` only** | No attack/hurt/death; needs the detached orb |
| `SkullSentryFrames` | flyer / turret | **4 frames, `moving` only** | No attack/hurt/death |
| ForestTreantBoss | boss | *none — the orc sheet at ×6* | **New 96×96 sheet.** A boss may not be a scaled basic enemy |

Two things to know before drawing any of these:

- **Boo, Cultist and SkullSentry currently vanish instead of dying.** `Enemy.StartDeath` frees
  the node immediately when the `SpriteFrames` has no `death` animation. That is the documented
  fallback, not a bug, but it is why those three feel abrupt next to the skeletons.
- **Cultist and SkullSentry are 16×16 at ×4**, so their art pixel is 4 against the skeletons' 2.
  Integer, so they are on the grid, but visibly chunkier now that nearest filtering is on.

## 2. Floor — ~24 tiles

Six stages run on **one 64×64 grass tile plus five 48×48 curated dungeon frames**, separated
only by a per-stage `modulate`. The curated manifest exposes 303 frames; we paint six of them.

| Stage | Tile today | Target |
|---|---|---|
| Forest, Swamp | `assets/ground_tile.png` 64×64 | 3 × 32×32 each, authored under Bonelight |
| Castle | `dun_071` 48×48 | 3 × 32×32 |
| Ruins | `dun_002` 48×48 | 3 × 32×32 |
| Ice | `dun_051` 48×48 | 3 × 32×32 |
| Desert | `dun_019` 48×48 | 3 × 32×32 |
| Volcanic | `dun_145` 48×48 | 3 × 32×32 |
| Maze walls | pulled from the same manifest, tinted by `WallModulate` | ~6 × 32×32 |

Roughly **18 floor + 6 wall = 24 tiles**. Stage mood stops being a `modulate` on the sheet and
becomes the two shader uniforms in §7 of the contract.

## 3. Decor props — ~14

Eleven props are in rotation: 2 rocks, 2 bushes, 2 trees, 5 crystals.

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
| `ui-…-4-sun-strike2` | ScorchingRay — a **UI icon** rendered in the arena |
| `fx-…-1-lightning-bolt` | GaleBlade |
| `fx-…-6-spikes-from-ground` | ObsidianSpike |
| `fx-magic-…-spritesheets-7` | GlacialSpike |
| `fx-magic-…-spritesheets-9` | ToxicSporeBurst |
| `fx-…-just-arrow` | HuntersArrow |
| `assets/Magic_Missile.png`, `assets/arcane_explosion.png` | MagicMissile, ArcaneExplosion |

Target cells are 32×32 for projectiles and 64×64 or 96×96 for impacts, per §1 of the contract.
Roughly **100–130 frames** once each effect owns its own sheet.

Elements do **not** multiply this. §3 of the contract maps twelve element ramps onto one drawn
burst — white-hot core, hue in the mid and edge — so one impact drawing serves all twelve.

## 5. Hazards and pickups — ~26 frames

| Asset | Today | Note |
|---|---|---|
| SpikeTrap | 4 frames | Extend sequence, scrubbed by progress |
| FlameVent | 4 frames | A guttering flame whose **last frame is empty** — needs `LoopWhileActive`, not scrubbing |
| HealthPickup | 4 frames (a flask) | |
| BuffItem | 4 frames (a flask) | Same flask family as HealthPickup — must diverge |
| ChestReward | 8 frames (idle + open) | |
| XPOrb | **1 frame — a 16×16 level prop** | Needs a purpose-drawn emissive pickup |

`StageHazard` draws its own floor marker procedurally and that stays; the sprite is detail on
top of it. Keep the marker radius larger than the sprite footprint or the art's opaque backing
tile covers the ring.

## 6. Icons — 67

Better shape than the rest of the art, because most already have distinct images.

| Set | Count | State |
|---|---|---|
| Active spells | 20 | **18 carry distinct icons**, three of them drawn for this project (`ui-derived-spell-icon-*`). Gaps: ArcaneExplosion slices its own VFX sheet; SpiritualWeapon falls through to the generic `ui-png-skills-icon-2.png` |
| Passive spells | 12 | Paths hardcoded in `Player.cs`. **`void_lance` and `blur` both point at `9-black-hole2.png`** — the one collision left |
| Relics | 25 (+1 fallback) | `ChestItemCatalog.GetIconPath`. Guarded: `RegressionChecks.ValidateChestItemIcons` fails startup if two ever collide |
| Chest sets | 10 | `SetIconRoot + n` |

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

**~110 GUI plates and panels** (`ui-png-*`, the `FantasyGuiSkin` interpolated ranges, backdrops)
are illustrated menu chrome, deliberately off-grid and off-contract. They can stay longest and
may never need replacing.

**The title screen is done.** `assets/bonelight/ui/title-screen.png` is authored 360×640 and drawn
at a whole ×2 into the 720×1280 viewport. It is generated by `tools/art/splash.py` — screen 1,
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
