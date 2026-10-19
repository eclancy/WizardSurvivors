# The level-up screen

The screen a player looks at more than any other in the game: three choices, a footer, and two
overlays reached from it. This is what it is made of and the three traps in it.

## 1. Two registers, and the trap that hid one of them

The screen follows the rule in `art-direction.md`: **stone outside, manuscript inside**. The panel
frame, the Reroll and Skip buttons and the Ascensions button are carved stone. The three spell
cards, the mutation and ascension choices, and the ascension browser are parchment.

**`Panel` is a plain `Control` with an opaque nine-patch called `Plate` as its first child.** That
one fact caused the bug that made the level 4 screen unreadable for as long as it existed. Both
overlays asked for the manuscript register with `BonelightSkin.ApplyPanel(panel, Vellum)`, and on a
plain `Control` that helper inserts its backdrop as **child zero** — underneath the plate. The
vellum was drawn on every one of those screens and never once seen, so every ink colour on them was
landing on stone brick. Darkening the text, which is the obvious fix when a screen is hard to read,
would have made it strictly worse.

`LevelUpMenu.ShowVellumPage` replaces that call. It inserts the page **immediately after the
plate**, inset by 18px, so the stone stays the outer edge and the page is laid inside it. Anything
added afterwards — the overlay's own content — is appended after the page and draws on it.

If you add another overlay to this screen, call `ShowVellumPage(true)`, not `ApplyPanel`.

## 2. The cards are torn pages

The three options used to be carved stone slabs inside a gold border. Handsome, and the wrong
object: a spell is a thing written in a book, and three on offer are three pages out of the same
book. `tools/art/ui_frames.py:torn_page_card` draws them, and the footer keeps the stone so the
screen's furniture never looks like the same kind of thing as the choice.

**A torn edge has to tile, not stretch.** A nine-slice stretches its edge bands, and a stretched
deckle smears into one long taper — the tear simply disappears at any size other than the one it
was drawn at. `BuildCardStyleBox(..., tornEdge: true)` sets `AxisStretchHorizontal` and
`AxisStretchVertical` to `Tile` instead, and the four tear tables in `ui_frames.py` are exactly the
length of the band so the repeat is seamless.

**The period of the tear is the whole design.** The first attempt used an eight-pixel sawtooth and
rendered a postage stamp: at that rate the eye reads perforation, because a regular period *is*
what perforation is. The shipping tables are twenty-four pixels, authored by hand so the bite
arrives in runs of uneven length.

Everything written on a spell card is ink — `Ink`, `InkSoft`, `InkGold`, `Rubric` (a boon card is not paper and has its own, section 2a). Note that
`BuildOptionTitle` had **no** colour override at all before the cards became pages: the spell name
was taking the theme's light default, which on parchment is very nearly the parchment.

**Both states of a page have the same face** - the lightest parchment, flesh `hi` (#F2DCC4). The
resting page used to be one step down (flesh `lit`, #D8A484) and only a hovered page was light,
so three cards out of four put their words on the darker ground. Hover, focus and "this is an
upgrade" now live in the furniture: the lit page's rule is gold and its leaf brighter, and
`MenuFocusHighlight` rings the focused card. **Selecting a card must never change how readable it
is.** The evolution cards (`vellum_card`) follow the same rule, with a gold inner rule when lit.

## 2a. The boon card is a reliquary

A spell is written, so it is a page. A boon is **carried** - a coin, a pearl, a flask, a seed; its
icons are objects drawn on the material rows, not effects drawn on an element ramp - so it comes
in the thing you would keep such an object in. Until this landed a boon was the same torn page
tinted a little warmer with its name in gold ink, and three pages side by side read as three
spells however the ink was coloured.

`tools/art/ui_frames.py:boon_plaque` draws a small reliquary plaque: **violet enamel in a
bevelled gold bezel**, chamfered corners, a diapered field, four gold studs. Three more pieces
complete it - `boon_window` (the pale glass the relic sits behind), `boon_clasp` (the bail it
hangs from, pinned over the top edge) and the boon inks. What makes it read as a different
*kind* of object at a glance, strongest first:

1. **Value.** The pages are the lightest thing on the screen and the plaque is one of the
   darkest. Squint, and the boon is the dark card. No reading involved.
2. **Edge.** Paper tears; this is machined - a straight bevel lit from the upper left like every
   other bevel in `ui_frames.py`, with cut corners.
3. **The window and the bail.** No page has either. The bail is the one thing on the card that
   says "you will carry this" before a word of it is read.

**Why violet and not stone.** Stone was the obvious dark and is wrong: the plate the cards sit
on is stone brick and the footer buttons are stone, so a stone card is furniture. `violet` is an
existing contract row (robe violet), so the plaque added no colour to the palette.

**It tiles, like the pages, for a different reason.** The field carries a diaper - a diagonal
lattice with a boss in each diamond - and a stretched diaper smears into streaks. The lattice
lives only in the 24x24 centre block on a 12-pixel period, which divides 24, so the tiles meet
seamlessly; the bezel is lines parallel to each edge, identical whether tiled or stretched; and
the chamfers and studs are in the corner blocks, which never scale. Its patch margin is **24**
(12 authored), not the page's 20 - `BuildBoonStyleBox`, not `BuildCardStyleBox`. The window is
stretched (flat centre, parallel bands) with a 12 margin; the bail is a plain `TextureRect` at its
authored x2 (48x36) that rises 12 px above the card.

**The ink on it is light, and is its own set.** `Ink`, `InkSoft`, `InkGold` and `Rubric` are
dark marks for a light page and all but vanish on enamel, so a boon card writes with
`BoonInkGold` (gold `hi`, the name), `BoonInkGoldSoft` (gold `lit`, the "BOON - PERMANENT, NO
SLOT" line) and `BoonInk` (bone `hi`, the effect line and the chip explanation). Contrast against
the resting enamel (#221C33) and the lit one (#342C4C) is in the comment on those colours; the
worst is the gold-lit line on the lit plaque, about 5.5:1. **Any new text on a boon card must take
one of these, never a parchment ink.**

**The relic is not tinted.** A spell icon is deepened toward `Ink` by its element; a boon icon is
shown as drawn, behind glass. The glass is bone `lit` - pale because the icons were drawn for
parchment and several (the flask, the seed, the veil) are dark enough to disappear on enamel, and
cold rather than warm because a parchment-coloured window would put the page back inside the
plaque.

**States.** It rests on the dark plaque and lifts to `ui-boon-plaque-lit.png` (enamel one step up,
gold one step brighter) on hover, focus and press - the same rule as a page, so focus behaves the
same everywhere. The outermost two pixels are deliberately dark (occlusion, then deep enamel) with
the gold starting inside them: `MenuFocusHighlight` draws its lit-gold ring a couple of pixels in
from the edge, and on a gold outer edge the ring would land on gold and vanish, which is the bug
section 4 of `controller-and-focus.md` already paid for once.

**Everywhere a boon appears as a choice.** The level-up cards (`BuildWideOptionCard`,
`BuildNarrowOptionCard`, both through `ApplyOptionCardStyle` and `BuildFramedOptionIcon`), and
the two pickers that list boons as plain buttons - **Ban** and **Augury** - where a boon entry
wears the plaque as a button (`ApplyBoonFrameToButton`) rather than stone, so the rare card is the
same object wherever the player meets it.

To photograph it on demand: `_UiShot --force-boon` (implies `--force-levelup --keep-levelup`)
sets `Player.ForceBoonOnNextOffer`, because boons are rare now (see `passives-and-items.md`, "How
rare a boon is") and a forced level-up shows one only about one time in five. `--focus-boon` also moves the selection onto it, to judge the focus ring on the plaque.

## 3. The cards fill the screen

Three 132-tall rows in a panel a thousand units deep left the bottom half of the most-looked-at
screen in the game empty. `NarrowCardHeight()` works out what each card can actually have from the
viewport, the HUD safe area and `NarrowChromeAllowance`, and falls back to `NarrowRowMinHeight`
only when the answer is too small to read. The icon column scales with it.

It is derived from the viewport rather than measured from the laid-out `ScrollContainer`, because
the cards are built before that container has a size; a deferred second pass would make the screen
visibly resettle. If the allowance is a little off the scroll absorbs it, which is the failure mode
worth having.

## 3a. Level 8 is not a card: the Ascension strip

**Level-up cards stop at level 7, with every pip full.** The Ascension to level 8, and a
wizard's Apotheosis after it, is a row of buttons **above the cards** (`AscendStrip`, built by
`LevelUpMenu.SetAscensions` from `Player.GetAscensionOffers`). It is offered on **every**
level-up from the moment it is earned until it is taken. Earned includes a player level: 14 for
an Ascension (`Player.AscensionMinimumPlayerLevel`), 20 for an Apotheosis
(`ApotheosisCatalog.MinimumPlayerLevel`) - see `.ai/gameplay-design.md`, "The Apotheosis". The
ascension browser names the Ascension's level among its requirements; nothing yet names the
Apotheosis's. Rerolls and bans re-deal the cards and leave it alone. Pressing a button opens the same evolution page a milestone card always did, and
choosing a branch spends the pick.

Why it moved: level 8 used to be the eighth card. When it came up before its element was deep
enough, `MarkUnearnedAscensions` turned it into a plain upgrade. Taking that maxed the spell with
no Ascension, for the rest of the run, and with it any route to the wizard's Apotheosis. Nothing
on the card said so. The balance harness walked straight into it: Tituba reached element tier 6
holding a level 8 Riptide that could never ascend.

Rules that keep it closed:

- `Player.CardLevelCap(spell)` is 7 for any spell with an Ascension. New-spell and upgrade cards
  both report it as `MaxLevel`, so the pips (`BuildLevelPips`, one per level after the first)
  fill exactly at 7.
- `Player.TryAddOrLevelSpell` **refuses** a plain pick for a spell at its cap. Level 8 is only
  reached through an Ascension branch.
- A spell at 7 with no branch earned is simply not in the strip. The ascension browser says what
  is missing.
- The strip sits in `Panel/VBoxContainer`, so it hides with the cards behind the evolution page
  and the browser. It is inset 30 px from the top so it clears the **★ Ascensions** button, which
  is pinned to the panel corner and overlapped it at phone width.

## 4. The ascension browser

An ascension is the biggest single decision in a run, and until this landed the player met each one
by surprise. Nothing anywhere said which of their spells *had* one, what it wanted, or how close
they were — the requirement only ever appeared stamped across a card they were already being shown,
at the one moment it was too late to go and earn it.

The **★ Ascensions** button in the panel's top right opens a page listing every spell in the tome
with all of its level 8 branches, each either ready or greyed with **every** requirement it still
fails. Both gates are named, because a player one element short *and* three levels short who is
told only about the element will chase the wrong one.

It **reports, it never grants**. `Player.BuildAscensionPreview` is the only source, and
`Player.DescribeUnmetElementGate` is the single implementation of the element rule — the level 8
screen stamps it across a locked card and the browser lists it, and neither has its own copy. That
split is how the two would otherwise drift, the first time either wording was touched.

## 4a. Legibility: faces, the heading, the floor, the inks and the glyphs

The player's report was that the menus were all hard to read and the level-up heading was
completely illegible. Five causes, five fixes; the before and after are photographed by the
harness in section 5 and measured by `tools/art/ui_contrast.py`.

**Faces.** Prose - every Label, description, stat line and button - is the engine's own sans
(Open Sans SemiBold, built into Godot; nothing added to `assets/` or `NOTICE`), selected by
`scenes/resources/UiTheme.tres` setting **no** `default_font`. Headings - the Display and Title
roles only - are Cinzel at weight 700 through `scenes/resources/HeadingFont.tres`. Cinzel had been
the body face, and its lower case is small capitals: every sentence in every menu was set in what
reads as all caps with hairline strokes. Do not put Cinzel on `Button` in the theme: `CheckButton`
and `CheckBox` inherit it, and they carry whole sentences on the options screen.

**The heading sits on a band, never on the floor.** `StyleHeading` clears the scene's
`LabelSettings` on both layouts (a `LabelSettings` font size beats every theme override, and on a
phone it was drawing the heading at 16px), sets the Display role and gives the label a solid
stone-deep band with a gold rule at its foot. Heading ink on the band is 17.3:1; on the old
floor's bright stone lips it was 1.25:1. On a phone the heading says only "Level Up!",
left-aligned, because the Ascensions button owns the right of that row; `AlignAscensionButton`
keeps the button centred on the band wherever layout puts it.

**The floor is matte.** `tools/art/levelup_frames.py:plate` lays the same running-bond cobbles in
two adjacent dark `stone` tones with a one-step lip on top only - no bone-white lips, no glints, no
pitting, no cracks. The rivets on the stone panel (`ui_frames.py:stone_panel`) lost their
specular for the same reason. The floor should never be the brightest thing behind a word.

**The ink rule.** Every ink is measured against the ground it is actually drawn on, sampled from
the generated PNG - never assumed. Targets are WCAG 2: **4.5:1 for anything read as text, 3:1 for
a heading**. `python tools/art/ui_contrast.py` prints the table (and writes
`tools/art/_menu_contrast.txt`); `--rev HEAD` measures the committed art instead, for a before and
after. The inks are copied into that script from C#, so a change to `Ink`, `InkSoft`, `InkGold`,
`Rubric`, the boon inks or `HeadingInk` must be copied there too. Element chips pick near-black or
white by WCAG contrast against the chip as drawn (`StatIcons.ReadableOn`); the old brightness
threshold put white on poison and grass at about 2.3:1. Nothing a player reads to decide is set
below the Label role: chips, chip explanations, the browser's "Still needed" line and the pause
screen's detail text were all Micro.

**Stat glyphs.** A stat line leads with a 12x12 glyph drawn by `tools/art/stat_icons.py` (x2, so
24px - a capital letter at the Body role), outlined in occlusion so one set reads on parchment,
enamel and stone. `scripts/StatIcons.cs` picks the glyph by matching the line's text
(`KeyFor`, most specific test first), spells out the old abbreviations (DMG, CD, RNG, xN), and
lays a summary out one glyph-led line per stat. Colour stays a second cue; meaning no longer
depends on it. The set: damage (sword), cooldown (hourglass), range (reticle), projectiles (three
darts), area (arrows to the corners), pierce (arrow through a board), chain (two links), crit
(four-point star), knockback (chevrons), freeze (snowflake), slow (snail), root (sprout and
roots), burn (flame), poison (drop), duration (clock), speed (boot), shield, heart (max HP), regen
(heart with a cross of light), magnet (pickup radius), luck (clover), xp (gem), thorns (retaliate),
evasion (swerve), and generic (a gold lozenge, so a line nobody anticipated still gets a mark). The
level-up cards, the boon effect line, the chest pick and the Arcane Codex effect lines all use
them. **A new stat line needs no code if its words match a key; check `KeyFor` before adding a
glyph.**

## 5. Looking at it

`dotnet build` proves none of this. Two harnesses do:

- **`scripts/_UiShot.cs`** photographs a real run. `--keep-levelup` stops the bot taking the first
  option so the level-up screen can be photographed rather than skipped past, and
  `--press-late=<ButtonName>` presses a button *after* the warmup — `--press` fires the moment the
  scene is instanced, which cannot reach a menu that does not exist until the player levels.

  ```
  "$GODOT_BIN" --path . scenes/_UiShot.tscn -- --scene=res://scenes/node_2d_game.tscn \
      --shot=levelup --frames=700 --timescale=4 --keep-levelup --press-late=AscensionsButton
  ```

- **`scripts/_LevelUpProbe.cs`** stands the menu up with synthetic data and takes `--screen=` and
  `--settle=`. **Its picture does not work on this machine** — the capture comes back as a flat
  fill of the clear colour whether the menu is parented under the probe or under the tree root, and
  the probe now says so in the filename rather than silently discarding the evidence. Its geometry
  report (`user://levelup-probe-<screen>.tsv`: every label, its font size, and whether its text
  fits its box) is what it is actually good for, and is stricter than a picture for that.

  The level 4 mutation page is photographed through `_UiShot --show-mutation`, and the level 8
  Ascension page through `--show-ascension`; both reach the private page builder by reflection.
  `--open-pause` opens the in-run pause screen. To photograph every menu at both canvas shapes,
  run the harness once per screen at `--resolution 1280x720` and at
  `--resolution 540x1212 --portrait --safe-area` (the Pixel 9), one Godot at a time.
