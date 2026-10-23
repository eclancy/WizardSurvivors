# Technical Architecture

## Core Scenes
- `scenes/player.tscn` / `scripts/Player.cs` (`CharacterBody2D`)
- Enemy scenes (`enemy.tscn`, `FastEnemy.tscn`, `TankEnemy.tscn`) using `scripts/Enemy.cs`
- Spell scenes plus `SpellData` resources for active spells
- `Node2DGame.cs` as the main run scene controller
- `LevelUpMenu.cs` for level-up choices, rerolls, skips, element previews, full-slot replacement, and remove-only flow
- `SaveManager.cs` autoload for persistent meta progression

## Data-Driven Design
- Spells defined in .tres/.res Resources.
- Upgrades defined in .tres Resources.
- Enemy stats defined in .tres Resources.

## Element Threshold Runtime
- Each `SpellData` resource exposes `ElementWeights`; most spells contribute one or two element instances.
- `Player.GetElementInstanceCounts()` totals equipped spell weights plus the selected character's innate element bonus.
- `Player.GetElementTier()` resolves the active 0/2/4/6 threshold for each element.
- Threshold bonuses are applied in the relevant gameplay path: XP gain, healing from damage dealt, incoming damage mitigation, HP regeneration, Earth max HP, Wind movement speed, Water cooldowns, and on-hit Fire/Ice/Lightning/Poison effects.
- The in-run HUD shows every element the player holds a tag of, with its progress to the next threshold (the loadout strip below), and the player HP bar shows the active Earth max-HP bonus when present.

## The in-run HUD

Everything drawn over a run lives on `node_2d_game.tscn`'s `UIOverlay` CanvasLayer and is built in
`Node2DGame._Ready` (the health bar in `ConfigurePlayerHealthHud`, the XP bar in
`ConfigureXpCounterUi`, the timer, the stage intro, the onboarding tip, the debug overlay) or added
to it later (`ConfigureBossHud`, `SideEvent._Ready`, `ChestPointer`, `SideEventPointer`). Every node
on it is `MouseFilter` Ignore: the touch stick (`TouchControls`, canvas layer 5) takes a thumb
anywhere below its HUD band, and inside the band a control that stopped the mouse would be a dead
patch of screen. `SafeArea.Apply(UIOverlay)` runs once the lot is built; anything added after that
calls `SafeArea.ApplyTo` on itself.

Where things sit, in canvas units (the 720 short side on both shapes):

| Slot | PC 1280x720 | Phone 720x1280 |
|---|---|---|
| Health bar | top-left, 7..247 x 6..50 | same |
| XP bar | under it, 7..247 x 54..69 | same |
| Run timer | top-centre, 8..36 | same |
| Boss bar (only during the fight) | full width, 44..78 | same |
| Side event objective | full width, 84..112 | same |
| Stage intro / onboarding tip | centred, 42..128 | same |
| **Loadout strip** | a column under the boss slot, 7..257 x 84.. | one row on the floor of the touch band, ..172 |
| Touch pause button | - | top-right, 8..56 |
| Touch stick | - | anywhere below 176 |

### The loadout strip (`scripts/LoadoutHud.cs`)

"we should show the spells and tags on the game screen while playing, but small. the tags can just
be colors with the 2/3 symbol, and the spells can just be the icons. we don't need to show the
boons." The old element badges ("Fire 2/4" at 102x28) and the 48-pixel spell row had been taken off
the playfield as too heavy for information the player does not act on mid-fight; this is the glance
put back at about a third of the area, and the pause screen is still where any of it is read in
full.

- **Spells**: one 34-unit slot per book slot (`Player.SpellSlots`: six, seven with the Blank
  Folio), in slot order, the spell's icon at 32 with its level in the bottom-right corner in the
  health readout's ink and outline. The icons are 32-pixel art stored at x2; at 32 units and
  Nearest filtering every art pixel is one unit, which is why the strip is not the 24-28 first
  sketched (Nearest at those sizes drops every fourth or eighth row and reads as damage, not as
  small). An unfilled slot is a faint outline. A spell's rim says which milestone it has passed,
  in the hue its level-up family is drawn in (`.ai/level-up-screen.md` 2c): steel for a plain
  held spell, gold lit for a level 4 mutation, arcane lit for a level 8 Ascension, fire mid for
  the Apotheosis; the backing takes a breath of the same hue.
- **Tags**: one chip per element the player holds at least one tag of, most tags first, in the
  same face every other element chip in the game wears (`CodexRows.ChipFill` and
  `StatIcons.ReadableOn`, so the ratios measured in `tools/art/ui_contrast.py` are the ratios on
  screen; the weakest is Earth, 4.9:1) and the same text (`ElementPassiveDescriptions.GetProgressLabel`:
  "1/2", "3/4", "5/6"), so the strip and the level-up card never give two numbers for one state.
  At 6 there is no next threshold: the count and a drawn star. Boons and relics are not shown;
  their tags are counted.
- **Placement**. Both canvases put it on the left under the health and XP bars, so the player's
  own state is one block in one corner. On a PC it is a column the width of a full book
  (250 units), starting under the boss bar's slot, so the bar never lands on it; the chips wrap to
  a second line inside the column; the run's centred lines never reach x 260 on a 1280 canvas. On
  a phone every top-of-screen line spans the canvas, so the only slot clear of all of them is the
  floor of the touch band (`TouchControls.DefaultHudBandHeight`, 176): one row across the screen,
  spells then chips, four units above the line where a thumb starts to steer, growing upward if
  the chips wrap. `SafeArea` moves the band and the strip down together.
- **Updates** on `Player.LoadoutChanged` only - raised at the mutation (a spell taken, levelled,
  mutated, ascended, cut; a boon taken; a relic claimed) rather than at the menu handlers, because
  the harnesses grant spells and boons directly - coalesced to one rebuild per frame through
  `CallDeferred`, which flushes while the level-up has the tree paused. The slot and chip nodes
  are built once and re-filled; nothing runs or allocates per frame.
- **Checks**. `python tools/art/ui_contrast.py` has a "HUD" block: the twelve chips, the numeral
  on its outline over white, and the four rims against the slot backing on the darkest floor and
  on white paper. The slot backing is floor stone deep at 0.85 because at 0.62 the plain steel rim
  measured 1.3:1 on the Sketchbook's paper. Photograph it with `_UiShot`: `--fill-book` for a
  full book, `--give-relics=blank_folio` for the seventh slot, `--grant-boons=` for tags,
  `--hud-milestones` for one spell at each of the three milestones (through the real Apotheosis
  pick, so its field attaches), `--touch-controls` on the phone canvas for the band and the pause
  button. `tools/art/_hud_loadout_sheet.png` (gitignored) is the strip on the dungeon, the swamp
  and the Sketchbook at 1280x720, 1920x1080 and the Pixel 9 canvas.

## Performance Notes
- Use object pooling for projectiles.
- Avoid per-frame allocations.
- Use CanvasLayer for UI.
- Use FastNoiseLite for procedural patterns if needed.
