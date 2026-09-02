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
  the late-game default table.
- The **shield ring** (`8-self-shield`) as the player's `ShieldAura` node.
- The **four-frame character idle** — `CharacterVisuals` resolves sibling frames.

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

### 2. Environmental hazards — a feature with all its art already here

Nothing in the game uses these, and there is no hazard system yet:

- `fx-…-flamethrower-1` / `-2` — wall-mounted flame jet
- `fx-…-peaks` — floor spikes that extend and retract
- `fx-…-floor-bloody-spikes`, `fx-…-fantasy-dungeon-traps2`
- `fx-…-dungeon-portal` — a stage exit or boss gate
- `fx-…-cave-bridge-collapsing`

### 3. More enemy types from complete animation sets

- **Soldier** — idle, walk, attack ×3, hurt, death (`char-soldier-animation-*`, filed under
  `characters/` rather than `enemies/`). A humanoid elite or miniboss.
- **Skull** — 4-frame idle, v1 and v2 (`enemy-2d-pixel-dungeon-asset-pack-…-skull-v*`). A
  floating-skull variant in the same family as BooEnemy.
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

## Not worth chasing

- Shadow and texture variants (`-1`, `-2`, `-3`) of props we already use.
- Per-frame slices under any `frames/` directory where the parent sheet is referenced — the
  scenes slice sheets with `AtlasTexture` regions instead.
- `Fantasy_RPG_GUI` screens we have no equivalent for: Login, Registration, Chat, Avatar,
  Journal, Quests, Map.
