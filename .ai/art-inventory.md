# Art inventory — what we own, what we use, and where the rest could go

## How to regenerate these numbers

```bash
grep -rhoE 'res://assets/organized/[^")]+\.(png|jpg|ttf)' \
  --include='*.tscn' --include='*.tres' --include='*.cs' --include='*.godot' . \
  | sed 's|res://||' | sort -u > /tmp/used.txt
find assets/organized -type f \( -name '*.png' -o -name '*.jpg' -o -name '*.ttf' \) | sort -u > /tmp/all.txt
comm -13 /tmp/used.txt /tmp/all.txt      # everything nothing references
```

This misses assets referenced only by a string built at runtime, so treat the
"unused" list as a candidate list, not a delete list.

## Current state

The Desktop source folder (`C:\Users\ericc\Desktop\Wizard Survivors Art`) is **already
fully imported** — `assets/organized/asset_manifest.json` records 1625 source files mapped
into the tree. There is no un-imported art. The gap is between what is imported and what is
wired up: **113 of 2095 files are referenced by anything.**

That ratio is not itself a problem. Most packs ship four colour variants and three shadow
treatments of every prop, and we only need one of each. The sections below separate the real
opportunities from the noise.

## Wired up already

For reference, so nobody re-discovers these as "unused":

- Enemy `moving`, `death`, **`attack` and `hurt`** for skeleton1 / skeleton2 / vampire.
- The **orc** as a fourth enemy type (`scenes/OrcEnemy.tscn`), spawning in Ruins, Swamp and
  the late-game default table. The same sheet is reused at 5.6x scale and tinted mossy green as
  **Elderbark, the Treant** (`scenes/ForestTreantBoss.tscn`) — the bulkiest humanoid silhouette we
  own, and the only unused-animation set complete enough (moving/attack/hurt/death) for a boss.
  A purpose-drawn treant would be a clear upgrade whenever art budget allows.
- The **soldier** as a recurring miniboss (`scenes/SoldierEnemy.tscn`) on a timer, one alive at
  a time. The only living armoured humanoid in an undead roster, so it reads without a banner.
- The **dungeon-pack priest** (`char-…-priest3-v1-1..4`, filed under `characters/`) as the
  **Cultist** (`scenes/CultistEnemy.tscn`), the roster's first ranged attacker. It is a four-frame
  idle with no walk, attack, hurt or death strip, which is exactly why this role got it: a caster
  that plants itself to wind up and drifts at range never needed a walk cycle, its attack tell is
  drawn rather than animated, and `Enemy.StartDeath` already frees an enemy whose sheet has no
  `death` animation. Tinted violet to match the bolt it throws.
- The **dungeon-pack skull** (`enemy-…-skull-v1-1..4`) as the **Skull Sentry**
  (`scenes/SkullSentry.tscn`), the rooted turret. Same four-frame-idle bargain as the Cultist, and
  an even better fit: this one is `Rooted` and genuinely never walks. Tinted amber, which is also
  its bolt colour — the two ranged attackers are told apart by projectile colour as well as
  silhouette, so a player can see which volley is incoming before it lands.
- The **shield ring** (`8-self-shield`) as the player's `ShieldAura` node, and again tinted gold
  and squashed underfoot as the **elite marker**.
- The **four-frame character idle** — `CharacterVisuals` resolves sibling frames.
- The **ornate HP frame** (`ui-png-hp-mana-1`) as the screen-space health readout.
- The **chest idle and opening animations** on `ChestReward`.
- **Floor spikes** (`peaks`) and **flame vents** (`flamethrower-2`) as `StageHazard`.
- The GUI pack's **equipment icons** (`ui-png-elements2-*`) as relic icons.

### Relic icons: read this before changing one

Relics used to draw entirely from the spell-VFX icon set, which left three images each doing
duty for two relics and several plainly wrong — Frozen Tear, an ice relic, wore a lightning
bolt. They now come mostly from `ui-png-elements2-*`, a vocabulary of armour, boots, weapons
and vessels that actually suits gear. `RegressionChecks.ValidateChestItemIcons` fails the
startup validator if two relics ever share an icon again, so a collision will be caught rather
than shipped.

### Hazard art needs its own telegraph

The pack's hazard sprites were drawn for dark dungeon flooring. On a bright grass arena the
spikes are near-black and the flamethrower is a few small orange wisps — a hazard nobody can
see is a random tax, not a challenge. `StageHazard` therefore draws its own floor marker (a
socket that fills and glows as it arms) and treats the sprite as detail on top. Two traps to
remember if you add another hazard:

- The **marker radius must exceed the sprite's footprint**, or the art's opaque backing tile
  covers the ring entirely.
- **Not every strip is an extend sequence.** The spikes run flush-to-extended and can be
  scrubbed by progress; the flamethrower strip is a flame guttering whose *last frame is empty*,
  so scrubbing it shows nothing at the moment it is most dangerous. That is what
  `LoopWhileActive` is for.

## Opportunities, ranked

### 1. Decor variety — the arena repeats two rocks and two bushes

`BuildDecorProps` in `Node2DGame` picks from **11 props**. The tree holds roughly 600:

| Prop | Variants available |
|---|---|
| Rocks | 40 (`lvl-props-rocks-and-stones-…-rock*`) |
| Broken / dead trees | 28 (`lvl-props-free-top-down-trees-…-broken-tree*`) |
| Crystals | 15–16 each in blue, green, violet, white, yellow, red, dark-red, pink, black |
| Bushes | 12 each in snow, autumn, and red / pink / orange / blue flowers |

Each prop ships as base plus `-1` (shadow), `-2` (textured shadow), `-3` (dark). We use the
plain or `-1` form; the rest are alternates, not extra props.

The obvious use is biome-matched decor: snow bushes in Ice, autumn bushes and broken trees in
Ruins, dead trees in Swamp, and crystals tinted to the stage rather than the current fixed
five. `StageEnvironmentKind` already exists to key off.

### 2. More hazard types

`StageHazard` exists and drives spikes and flame vents. Still unused, and cheap to add as new
scenes pointed at the same script:

- `fx-…-floor-bloody-spikes`, `fx-…-fantasy-dungeon-traps2` — 576x768 sheets, so they need
  slicing first
- `fx-…-flamethrower-1` — the vertical jet, for a wall-mounted variant
- `fx-…-dungeon-portal` — a stage exit or boss gate, not a hazard
- `fx-…-cave-bridge-collapsing`

### 3. More enemy types from complete animation sets

- **Soldier** — idle, walk, attack ×3, hurt, death (`char-soldier-animation-*`, filed under
  `characters/` rather than `enemies/`). A humanoid elite or miniboss.
- **Skull v2** — 4-frame idle (`enemy-2d-pixel-dungeon-asset-pack-…-skull-v2-*`). v1 is now the
  Skull Sentry; v2 is still free, and is the obvious art for a second sentry tier or a
  pattern-turret variant.
- **Priest 1 and 2** — 4-frame idles (`char-2d-pixel-dungeon-asset-pack-…-priest1/2-v*`). Priest 3
  is the Cultist; the other two are the natural sheets for further caster types, and the whole
  family reads as robed spellcaster rather than as a walking melee threat.
- **Dungeon-pack skeleton / skeleton2 / vampire, v1 and v2** — 4-frame idles. Alternative art
  for the three existing types, useful for elite recolours or a second tier.

### 4. Unused spell VFX sheets

- `fx-…-5-explosion-explosion` — a generic impact burst; no spell currently has one
- `fx-…-9-black-hole-black-hole` — a vortex; Black Tentacles is procedural today
- `fx-…-fire-wall-ignition-fire-wall` — no fire-wall spell exists
- `fx-…-4-sun-strike-sun-strike` — Solar Flare uses this pack's *icon* but draws its own
  effect procedurally (`SolarFlareVisual`), deliberately: the procedural version is a ground
  flare, which this sheet is not
- `fx-…-3-midas-touch-shiny-explosion-midas-touch` — gold burst; suits a Fortune's Favor proc

### 5. Pickup and chest polish

- `fx-…-chest-open`, `fx-…-mini-chest-open` — chests are static today
- `fx-…-coin` — animated currency pickup
- `fx-…-flasks-1` / `-2`, `fx-…-keys-1` / `-2` — already used as chest-item *icons*, but the
  animated strips are unused

### 6. Dungeon interiors, if a stage ever wants them

Whole unused tilesheets: `mines-tilesheet`, `dungeon-walls-tileset`, `dungeon-decorations`,
`dungeon-prison-decorations`, `cave-walls-and-decorations`, `fantasy-dungeon-a5`,
`fantasy-dungeon-b`, plus ~300 pre-sliced frames under
`level/tiles/curated/fantasy-dungeon-dungeon-floors-48x48/frames`. Stages are currently open
outdoor arenas, so this only matters if an indoor stage is on the roadmap.

## Known problem: 420 world props are filed under `ui/`

The import script categorised by source pack name, and it put top-down world art in the GUI
folder. Everything matching these prefixes is world decor, not interface art:

```
assets/organized/ui/ui-top-down-ruins-pixel-art-*      (ruined walls and pillars)
assets/organized/ui/ui-undead-desert-map-*             (bones, graves, dead trees, rocks)
assets/organized/ui/ui-free-top-down-trees-pixel-art-* (fruit trees)
assets/organized/ui/ui-rocks-and-stones-*
assets/organized/ui/ui-seasonal-*
```

That is **420 files**, and it is why `ui/` looks like it has 634 unused entries when only
about 157 are genuine GUI art (`ui-png-elements`, `-icons`, `-iconsmenu`, `-loading`, and the
panel sets). Moving them to `level/props/` would need every referencing path updated, so it
is only worth doing alongside other work in that area — but until then, **search `ui/` when
looking for world decor**, or you will conclude we have no ruins art.

## Considered and deliberately left alone

**Animated water** (`seasonal water sparkles`, `summer waterfall`, both 16x16). There is no
water anywhere in the stage system — `StageEnvironmentKind` has no water biome and
`LevelTilePainter` builds its TileSet at runtime from curated floor frames with no notion of a
water region. Wiring these up is a level-generation feature, not an art-wiring task, and two
16x16 tiles is thin justification for it.

**Loading screens** (13 images under `ui-fantasy-rpg-gui-loading-*`). Scene changes here are
instant. A loading screen over an instant load is an artificial delay dressed as polish — it
makes the game slower to play in exchange for a picture. Worth revisiting only if stage loading
ever becomes slow enough to need covering.

**Elites wearing the v2 character sprites.** The idea was one sprite per tier rather than a
tint. It does not survive the details: the dungeon pack's v1/v2 sets are four-frame *idles*, so
an elite wearing one would lose its walk, attack, hurt and death animations. Elites got a gold
floor ring instead — a smaller change that adds information rather than removing it.

## Not worth chasing

- Shadow and texture variants (`-1`, `-2`, `-3`) of props we already use.
- Per-frame slices under any `frames/` directory where the parent sheet is referenced — the
  scenes slice sheets with `AtlasTexture` regions instead.
- `Fantasy_RPG_GUI` screens we have no equivalent for: Login, Registration, Chat, Avatar,
  Journal, Quests, Map.
