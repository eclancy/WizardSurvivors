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

Everything written on a card is ink — `Ink`, `InkSoft`, `InkGold`, `Rubric`. Note that
`BuildOptionTitle` had **no** colour override at all before the cards became pages: the spell name
was taking the theme's light default, which on parchment is very nearly the parchment.

## 3. The cards fill the screen

Three 132-tall rows in a panel a thousand units deep left the bottom half of the most-looked-at
screen in the game empty. `NarrowCardHeight()` works out what each card can actually have from the
viewport, the HUD safe area and `NarrowChromeAllowance`, and falls back to `NarrowRowMinHeight`
only when the answer is too small to read. The icon column scales with it.

It is derived from the viewport rather than measured from the laid-out `ScrollContainer`, because
the cards are built before that container has a size; a deferred second pass would make the screen
visibly resettle. If the allowance is a little off the scroll absorbs it, which is the failure mode
worth having.

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

  **The level 4 mutation screen has therefore never been photographed.** It shares `ShowVellumPage`
  and the whole ink palette with the ascension browser, which has been, so the fix is verified by
  construction rather than by eye. If you touch it, that is the gap.
