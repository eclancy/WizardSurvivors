# The level-up screen

The screen a player looks at more than any other in the game: three choices, a footer, and two
overlays reached from it. This is what it is made of and the three traps in it.

## 1. Two registers, and the trap that hid one of them

The screen follows the rule in `art-direction.md`: **stone outside, manuscript inside**. The panel
frame, the Reroll and Skip buttons and the Ascensions button are carved stone. The evolution page
and the ascension browser are parchment; the cards on them, and the three level-up cards, are each
the object of their kind (section 2c) - only a new spell is a parchment page.

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
is.** Every family in section 2c follows the same rule: the lit frame changes the furniture -
stitching, gilt, bezel, runes - and never the face under the words.

**Only a spell not yet in the book is a page.** An upgrade used to be this same page resting on its
lit frame; it is the bookmark ribbon now (section 2c).

## 2a. The boon card is a reliquary

A spell is written, so it is a page. A boon is **carried** - a coin, a pearl, a flask, a seed; its
icons are objects drawn on the material rows, not effects drawn on an element ramp - so it comes
in the thing you would keep such an object in. Until this landed a boon was the same torn page
tinted a little warmer with its name in gold ink, and three pages side by side read as three
spells however the ink was coloured.

`tools/art/ui_frames.py:boon_plaque` draws a small reliquary plaque: **violet enamel in a
bevelled gold bezel**, chamfered corners, a diapered field, four gold studs. Three more pieces
complete it - `boon_window` (the night glass the relic sits behind, section 2b), `boon_clasp` (the bail it
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
shown as drawn, behind glass, in its own light (section 2b). The glass was bone `lit` - pale
because several icons (the flask, the seed, the veil) are dark enough to disappear on enamel - and
is now night glass, for the reason 2b gives.

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

## 2b. The relic is in its own light

"Boons should always look magical, like iron rivets should have a magical aura spin on it, so it
doesn't just look like an item." The plaque said *boon* before a word was read, but the thing in
the window was a plain object - a plate of steel, a bead, a flask - drawn on the material rows
exactly like a chest relic. So every boon now shows its **first element tag** as light, in two
halves:

- **Baked, so a still thumbnail reads as magic.** `tools/art/boon_icons.py`, "the enchantment":
  a glowing inlay cut into the objects that were all material (a ward-diamond scored into Iron
  Rivets' plate, with a spark on each rivet; a sun-cross on the lantern flask; a wave on the
  tidewater), a part-alpha fringe of the element's `edge` tone round the silhouette, and one
  four-point glint in the clear corner. All from the emissive ramps; the palette audit still passes.
- **Live, so the card is visibly alive.** `scripts/BoonAura.cs` draws, at one integer scale, a
  steady **backlight** (core and hot), a **breath** (mid and edge rings whose alpha rises and
  falls), a **rune ring** (six invented runes turning one way, a dashed track the other), and the
  icon on top. The atlas is `assets/bonelight/ui/ui-boon-aura.png`, written by the same generator.

**Why the window went dark.** A glow cannot show on a pale ground: the first cut drew Iron Rivets -
the boon the player named - in a white halo on white glass, and it looked like nothing had changed.
The window is night glass now (`ui_frames.py:boon_window`, violet `deep`), and the light ground the
dark relics need comes from the backlight instead: a disc of the element's core and hot that the
relic is silhouetted against. **The backlight never pulses**; only the breath does. How readable a
relic is must not depend on where in its breath the aura happens to be.

**The band comes out of the glass, not the plaque.** The aura cell is the 32 icon cell plus a six
pixel band (44), and the ring lives in that band - the objects fill most of their own cell. On a
phone the text starts twelve pixels from the window, so the band is taken from inside the window
and never spills onto the enamel; nothing animated sits behind a word. `BuildFramedOptionIcon`
sizes the window to a whole multiple of 44 (x2 on a PC, so the window is 108 inside the 112 icon
frame; x2 to x4 on a phone), which also puts the relic on exact pixels for the first time - the old
window drew the 64px icon at 68.

**Rotation is frames.** Nearest-neighbour rotation of pixel art crawls, so the ring is twelve frames
drawn on the grid (runes repeat every 60 degrees, dashes every 15, five degrees a frame). At rest a
frame every 0.24s - a turn in about eighteen seconds - and the breath every 2.6s; while the card is
hovered, focused or pressed, 0.11s and 1.5s with a higher floor, so the card about to be taken
wakes up. `BoonAura.FocusOwner` is passed in, because on a phone the card Button is a sibling of
the content rather than its ancestor. Cost: four texture draws, no allocation per frame, and a
redraw only when the frame or the breath has moved. There is no reduced-motion setting in the game
yet; if one is added, it should hold `BoonAura` on frame 0 at full breath.

**Where else.** The pause screen's Run Details has a **Boons** section (`Node2DGame
.BuildBoonListSection`) - before it, a boon taken was never shown again - with each relic at x1
through `BoonAura` at `Intensity` 0.7, so it reads as magic without pulling the eye off the row.
The Ban and Augury pickers list names only, for spells and boons alike, so there is no picture
there to enchant; they keep the plaque button.

To photograph the motion: `_UiShot ... --focus-boon --burst=3,20` takes three frames twenty
frames apart from one run (separate runs deal different cards). The pause section:
`--open-pause --grant-boons=iron_rivets,umbral_veil --scroll=690`.

## 2c. The card families

From the Pixel 9: "learning new spells should be the only thing colored like a spell page.
levelling up spells, and mutations, etc should all look different", and "mutation/ascension still
hard to read for color". An upgrade was the new spell's torn page with a gold rule, and the level 4
and level 8 choices were `vellum_card`s laid on the vellum page - parchment on parchment, so a card
was a rule drawn on the page rather than an object on it, with every word a brown, gold-brown or
violet ink on tan. Ratios passed; the screen was still mud.

Every kind of choice now comes in its **own object**, told apart by value, edge and one piece of
furniture before a word is read. `scripts/CardFamilyArt.cs` holds the enum, frames, overlays and
inks; `LevelUpMenu.FamilyOf` picks the family; `tools/art/ui_frames.py` draws every frame.

| Family | Object | Value | Edge | Furniture | Writes in |
|---|---|---|---|---|---|
| **New spell** | torn page, flesh `hi` | lightest | torn deckle | gold leaf corners | `Ink` family (dark) |
| **Upgrade** (and Marginalia, and the Penknife's picker) | bookmark ribbon of red silk, red `lit` (`ribbon_slip`) | mid-dark | woven, stitched dead straight | gold pin through the top left; swallowtail tail hanging below (`ribbon_tail`) | gold hi name, bone hi deltas, gold lit "Level up · Attack" |
| **Boon** | reliquary plaque, violet enamel (2a) | dark | machined gold bezel, chamfers | glass window with the aura, bail above | Boon inks |
| **Mutation** (the level 4 card in the hand, and each branch) | sealed vellum: bone `hi` in a dark teal illuminated band with a gold vine (`sealed_vellum`) | white face, dark band | straight, gilt | red wax seal on the top right (`wax_seal`); branch icon in a teal roundel (`mutation_roundel`) | `Ink`, `InkSoft`, `Rubric`, `InkGold` - all gain on the whiter skin |
| **Ascension** (level 8 branches, the ASCEND strip button) | obsidian plate, arcane `deep` glass in a gilded band of runes (`obsidian_plate`) | darkest | gold bevel | four rune shapes on a 24 period, gem bosses | gold hi name, bone hi, arcane lit, gold lit advice |
| **Apotheosis** (its page, the strip button) | the same plate in red `deep`, runes in the Fire ramp (`obsidian_plate(ember=True)`) | darkest | gold bevel | burning runes | Fire hot name, bone hi, Fire mid, gold lit |

Why these, and the calls that were not obvious:

- **The ribbon is "improve what you have".** The spell is already a page in the book; a level
  marks it so you can come back to it. Value carries the read first - a squint shows a light card,
  a mid-dark red card and a dark violet one - then the edge (stitched straight against a torn
  deckle), then the tail, which is the ribbon running on out of the bottom of the book. The tail
  hangs 10 below the card, behind it (`ShowBehindParent`), less than the 12 between phone rows.
- **Marginalia is a ribbon**: it improves what the player has (heal, +2% damage) and is not a
  spell, so under the new rule it may not be a page. Its type line says "A note in the margin".
- **The Scribe's Penknife picker is ribbons** (`ChestItemSelectionMenu.BuildSpellPage`): it lists
  spells the player *holds*, and a held spell is a ribbon wherever it is the choice. The protected
  spell's fade went from 0.55 to 0.7, because bone on silk at 0.55 is 4.1:1. **The Blank Folio**
  needs no family of its own: the page it binds in is filled by an ordinary new-spell torn page.
  The chest's relic cards are still torn pages - that screen is outside this pass and is the one
  place left where something that is not a spell wears the page.
- **The level 4 card in the hand is already the mutation's vellum and seal**, so the ceremony
  starts on the card that opens it ("Mutation · Attack"). Level 8 is not a card (3a); the ASCEND
  and APOTHEOSIS strip buttons wear the obsidian and ember plates instead of the footer's stone,
  so the button and the page it opens are one object.
- **The mutation face is white, the page warm.** The vellum's cool bone `hi` against the page's
  flesh `hi`, inside a dark band, is what lifts the card off the page; every dark ink gains on it
  (Ink 10.6 → 12.5, InkSoft 8.8 → 11.6, Rubric 8.6 → 10.1). A branch's own icon tint is often
  pale - a pale blue missile on white is under 2:1 - so the icon sits in a teal roundel, the way an
  illuminator set a capital in a coloured square.
- **The ascension is the darkest card on the lightest page.** The biggest decision in a run is
  the thing with the most contrast on its screen. Glass, not stone, so it is never furniture.
  The page heading takes the family's darkest tone (teal-black, glass-black, ember red) instead
  of the gold-brown `InkGold` that was the muddiest thing on the tan page.
- **Synergy tags are solid chips** in the family's dark tone with bone ink (8.0 to 16:1), not a
  one-pixel outline round violet ink. On a PC branch card the icon shares the tag's row instead of
  having a row of its own - with the cards framed and inset inside the page, that row was the two
  lines of description that fell below the fold of a 1280x720 mutation page.
- **The lock** is a band *under* the locked card, bone on red (10.1:1), in the layout. It was a
  `Rubric` label anchored over the card, which the phone card's `PanelContainer` laid out like
  any other child - centred over the description - and on a plate a solid band there would have
  hidden the words it explained (`_UiShot --show-ascension --lock-branch`).
- **The evolution page's content sits inside the vellum page.** It started at 14, inside the
  page's 18 inset, so the heading's top lay on the page's rim: the ink report measured the level 8
  heading at 2.3:1 against it.
- **The ASCEND strip button is about 7 taller** than the stone one: its words sit clear of the
  rune band and the gilt inside it (content margin 18), which at 12 and 16 the photograph measured
  at 4.3:1. Section 3a's known overflow on a 1280x720 PC is that much worse while one is on offer.
- **A spent Reroll** ("Reroll (0)") was BonelightSkin's disabled ink at 4.2:1 on the footer stone;
  the level-up footer overrides it to #7A8296, 5.2:1, still the dimmest word there.
- **All of them tile** (`CardFamilyArt.Frame` sets `AxisStretch` Tile, margin 24): flat centre,
  bands carrying only lines parallel to the edge or patterns whose period divides the 24-pixel
  band (stitch 4, vine 6, runes 24), irregular furniture only in corner blocks. The roundel is
  stretched, like the boon window. Overlays (tail, seal, bail) are sprites at their authored x2.
- **Focus.** In every family the outermost two drawn pixels are dark (occlusion, then a dark lip)
  so `MenuFocusHighlight`'s ring - two pixels in - lands on dark and stays visible; the obsidian
  plate gained a lip of glass outside its gold bevel for exactly that. Hover and focus lift to the
  lit frame, which only changes the furniture.

**Contrast.** `tools/art/ui_contrast.py` measures every family ink against its frame in both
states (section "card families" of its table: 179 pairs, 0 below target; weakest text 5.5:1, the
ribbon's gold lit line; pips as graphics at 3:1, weakest 3.8:1 on the page). The photographs are
measured too: `_UiShot --ink-report` samples the real pixels behind every visible label (section
5). The mutation and ascension screens before and after:

| Screen (worst ground in the box) | Before: below target / weakest | After: below target / weakest |
|---|---|---|
| Mutation, PC | 0 / 3.2:1, the heading on the page's rim | 0 / 7.6:1, a synergy chip |
| Mutation, phone | 0 / 3.2:1, the heading | 0 / 8.5:1, a synergy chip |
| Ascension, PC | 1 / 2.3:1, the gold-brown heading on the rim | 0 / 4.4:1, a 30px name by the runes (large: 3:1) |
| Ascension, phone | 1 / 2.3:1, the heading | 0 / 7.3:1, the advice line |
| Ascension, a branch locked | 1 / 2.3:1, the heading | 0 / 5.4:1, the dimmed advice |
| Apotheosis (new shot) | the same page as the Ascension | 0 / 3.2:1 PC, a 30px name (large); 8.9:1 phone |
| Penknife picker | 3 / 1.8:1, the faded protected page | 0 / 5.5:1 |
| Marginalia, Ascend strip | 1 / 4.2:1, the spent Reroll | 0 / 5.2:1 |

Median ink on the mutation and ascension pages went from 10.0 and 9.3:1 to 11.2 and 11.4:1. The
browser was already clean (weakest 4.6:1, an element chip) and is unchanged. Every label on every
photographed screen - 22 shots, PC and phone - is now at or above its target.

## 2d. The level pips are alive

From the Pixel 9: "on leveling up spells, animate the pip for the level it's becoming".
`scripts/LevelPips.cs` replaced the row of `StyleBoxFlat`s. **One pip per level** (it was one per
level after the first, every pip the pick reached drawn already filled - an upgrade to 3 looked
like a spell at 3, and a new spell had no pip to light). Three states: held (solid, steady), **the
one this pick reaches** (a thicker ring, a fill that rises and falls, a halo two pixels out that
breathes with it, and on the ribbon a one-pixel glint at the top of the breath), and not yet (an
empty socket). A new spell's card animates pip 1 - it reads well: a lone breathing socket at the
head of an empty row says "this starts at 1".

Calm: a one-second eased cycle (`0.5 - 0.5 cos`), on `Time.GetTicksMsec`, which runs while the
level-up has the tree paused, so all three cards breathe together. While the card is hovered,
focused or pressed the floor rises from 0.15 to 0.45 and the halo brightens (`FocusOwner`, as
`BoonAura` does it). No allocation per frame; it redraws only while visible; `ProcessMode` Always.
Colours by family, rings at 3:1 or better as graphics: gold base on the page (3.8:1; the old gold
leaf was 2.4:1) and the vellum (4.5:1), gold lit on the ribbon (5.5:1). Photograph it with
`--burst=3,20`. There is no reduced-motion setting yet; if one is added it should hold the pip at
full fill.

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
level-up from the moment it is earned until it is taken. Earned includes a player level: 17 for
an Ascension (`Player.AscensionMinimumPlayerLevel`), 23 for an Apotheosis
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
  both report it as `MaxLevel`, so the pips (`BuildLevelPips`, one per level - section 2d)
  fill exactly at 7.
- `Player.TryAddOrLevelSpell` **refuses** a plain pick for a spell at its cap. Level 8 is only
  reached through an Ascension branch.
- A spell at 7 with no branch earned is simply not in the strip. The ascension browser says what
  is missing.
- The strip sits in `Panel/VBoxContainer`, so it hides with the cards behind the evolution page
  and the browser. It is inset 30 px from the top so it clears the **★ Ascensions** button, which
  is pinned to the panel corner and overlapped it at phone width.
- **While anything is ready, the ★ Ascensions button is alive** (`AscensionReadyGlow`, from the
  Pixel 9 report "the Ascension button should be animated when an ascension is ready"): a gold ring
  that breathes round it over 2.6 s, a shimmer that crosses its face for 1.1 s every 4 s, a badge
  with the number of spells that have one ready, and gold lettering - the lettering so a still
  screenshot, or a player who does not notice motion, still sees it. The strip's own buttons get
  the same shimmer, on the same clock (`Time.GetTicksMsec`, which runs while the tree is paused),
  so the two read as one thing waiting. Ready is counted by `LevelUpMenu.CountReadyAscensions`:
  a strip offer (which also carries the Apotheosis) or a branch the browser would mark ready.
  When nothing is, the glow is hidden and not processing. No allocation per frame; the badge is
  near-black on gold lit, 9.3:1. **Known, not this work's:** on a 1280x720 PC the strip's height
  pushes the panel past the screen - the heading band's top and the Skip button's foot are cut off
  whenever an Ascension is on offer. It predates the glow (see the before half of the sheet).

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
`Player.EvaluateElementGate` is the single implementation of the element rule -
`DescribeUnmetElementGate` words its result for the locked level 8 card, the browser draws it, and
neither has its own copy. That split is how the two would otherwise drift, the first time either
wording was touched. The preview carries every gate, met or not, as an `AscensionRequirement`
(kind, have, need, and for the element gate the closest element and every element that would
do), beside the old `UnmetRequirements` strings; a caller that fills only the strings (the probe
scenes) gets them listed as plain unmet lines.

**It is a checklist** (redesigned from the Pixel 9 report "cluttered and hard to read; make it
clear that if you need more tags most of the ascension is greyed out"). It had been a heading per
spell and a box per branch with a "Still needed:" run-on of every failing gate in one rubric line:
one weight throughout, a met gate simply absent, and a locked branch the same box as a ready one,
faded a little. Now it is CodexRows (section 4c), one row per ascension path:

- **The rule once**, in one sentence under the heading, instead of a paragraph above and a
  reminder in every box.
- **Sections by what the player can act on**: *Ready* first, then *Not yet* (or *Ascensions to
  earn* when nothing is ready), the near misses at the top - fewest failing gates, then furthest
  along. Spells with no ascension fold into one quiet line at the foot.
- **A row**: the ascension's name (Body), "Ascension of <spell>" and one line of what it does,
  then a **checklist of all three gates** - spell level, element tags, your level - a `done` tick
  on each that holds and the `todo` glyph (an empty socket, `tools/art/stat_icons.py`) on each that
  does not. An unmet gate states the shortfall ("Needs 3 more Lightning tags", "Your level 15/17 ·
  2 more levels") in the rubric on its own band. The element gate shows its element as a tappable
  chip (section 4d) - all the elements that would do while it is unmet, only the one that met it
  once it is.
- **Not ready is greyed, except what is missing**: no face, a faint edge, the icon at 40%, and
  every word in the meta ink - except the unmet gates, which keep the strongest ink on the row, so
  the eye lands on exactly what is missing. Grey here is a lighter ink, never one below the floor:
  meta on the page is 6.1:1, the rubric on its band 7.2:1.
- **Ready has the weight**: a gold-hi face and a 2px gold edge, the name in full ink, a gold "Ready"
  tag and "Waiting at the top of every level-up." (inks 7.1:1 and up on that face).
- **One column on both shapes.** On a phone the checklist runs down the row and the element chips
  get a line of their own above the sentence (two chips and a sentence squeezed into one line set
  the sentence a word a line). On a PC the width goes to the row, not to a second column: the
  sentence fits on a line and the checklist runs across it in a flow. Two columns halved the width
  and roughly tripled every row's height, and the browser showed one path at a time.
- Focus: a row's element chips are its stops; a row without chips gets the focus-only overlay.

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
evasion (swerve), and generic (a gold lozenge, so a line nobody anticipated still gets a mark) - plus energy, done and lock, the codex marks of section 4c, and todo (an empty socket) for the ascension checklist. The
level-up cards, the boon effect line, the chest pick and the Arcane Codex effect lines all use
them. **A new stat line needs no code if its words match a key; check `KeyFor` before adding a
glyph.**

## 4b. The chest screen is the same kind of page

`ChestItemSelectionMenu` was the one card screen that never got this pass: tall dark columns in a
panel 940 wide, an 80px icon, a sentence of effect, and set badges whose meaning was a press away,
over a band of empty card - and the panel overran the foot of the screen. It follows this screen's
rules now:

- **Stone outside, manuscript inside.** The panel fills the screen less a 10px margin (capped at
  1400 wide), the heading sits on the same stone-deep band (`StyleHeading`), and each relic is a
  torn page in `ui-spell-page.png`, tiled, writing in `Ink`/`InkSoft` plus four rarity inks and a
  gain ink - all mirrored in `ui_contrast.py` against the page and the set box.
- **The whole page is the button**, built like `BuildNarrowOptionCard`: a `PanelContainer` holding
  the `Button` and the content as siblings, so the page grows to its text and is one target for a
  thumb and one ring for a stick. The **Claim** plate at its foot is a drawn stone button
  (`FocusMode.None`, mouse ignored), so the stick only ever lands on pages.
- **The set is on the card.** Per set: its icon, its name, every piece as a 32px icon (held, the one
  on offer ringed in gold, missing ones faded), the count before and after ("1/3 → 2/3"), the partial
  tier and the full set's Description, which is now the mechanic in numbers
  (`passives-and-items.md`). The tier a claim switches on says **now** in the gain ink and the box
  takes a heavier rule.
- **Height is the scarce axis.** On a PC the pages stand side by side and share the panel's height;
  a relic in two sets (The Crown) gets a wider page (`SizeFlagsStretchRatio` 1.5) rather than a
  taller one, and it and every page of a four-relic chest are **brief** - the partial tier only
  when the claim reaches it. The pieces row is a flow, so four pages under 300 wide drop the count
  to a second line instead of pushing the fourth page off the screen. On a phone the pages stack,
  the Claim plate rides in the header row, and three or more short stats go in two columns. Both
  sit in a scroll container that scrolls only when it must.

Measured with `_UiShot`, which now prints each page's needed height against what it has: every
three-relic chest tried fits a 1280x720 PC with room to spare (worst 585 of 595, Tithe of Wormwood
with its set's partial held), the Reliquary Key's four fit (worst 591), and a phone fits three
including The Crown (1296 of 1347). **Only a four-relic chest on a phone scrolls**, by about 90px.

`--give-relics=a,b` and `--chest-offer=a,b,c` on `_UiShot --open-chest` pin what the player holds
and what the chest deals, so a before and an after show the same three cards.

## 4c. The Spellbook and Deeds lists: whitespace is the hierarchy

Reported from the Pixel 9 as "overwhelming and tight". The Spellbook was a grid of 210-wide
parchment cards with every line centred, a divider tile standing in the grid as if it were a spell
and a full stone "Set aside" bar under every page; Deeds was a four-column grid (two on the phone)
of small cards packing name, status, requirement, reward and progress at one weight, done and
not-done interleaved; the pause screen's Spells and Deeds tabs said the same things a third way,
with an engine-enum "Classification:" line on every spell and a box inside a box for every row.
Before and after: `tools/art/_deeds_spellbook_before_after.png` (gitignored; rebuild it from the
`_UiShot` commands in section 5).

All four views are built by **`scripts/CodexRows.cs`**, so a rule here is made once:

- **A row reads top to bottom, left-aligned**: the name (Body, the strongest ink), one line of
  what it does (Label, softer ink), then quiet meta - element chips, a progress bar with its
  numbers, a reward or hint led by a glyph. No centred prose: in a narrow card every centred line
  has a different left edge and the eye has to find each one again.
- **One column on a phone, two on a PC, never more** (`CodexRows.ColumnsFor`). On a PC the panel is
  capped at 1200 units and centred; on a phone it reaches 24 units into the menu margin, since
  width is the scarce axis and the safe area is applied outside that margin.
- **Space carries the grouping**: 32 between sections, 14 between rows, 18 between columns, rows
  padded 18 by 14, +3 line spacing on wrapped text. Section headings are bold Cinzel (Title) with
  their count right-aligned in the meta ink and a rule beneath.
- **Order is by what the player can act on.** Spellbook: Attacks, Passives, then *Still to find*;
  pages not yet found keep their name hidden, show a silhouette, and make the unlock hint the row's
  content (led by the lock glyph). Locked pages that share a kind and a hint fold into one row
  ("10 unknown passives" - ten identical "Not yet obtainable." rows were the longest stretch of the
  book). Deeds: *In progress*, closest to done first, then *Done*.
- **Finished, set-aside and not-found rows are quiet**: no face, a fainter edge.
- **Set aside is a small outlined pill beside the name**, in the page inks, and it only exists once
  the player owns a Redaction (or has something set aside). Before that, the summary line under
  the heading says once how to get one, instead of thirty disabled buttons saying it.
- **Reward glyphs** are the reward itself where one exists - the spell's icon, the boon's icon -
  and otherwise `stat-energy` (Arcane Energy). `stat-done` leads a finished deed's tag and
  `stat-lock` a hint. All three are drawn by `tools/art/stat_icons.py` and loaded by name, not
  through `StatIcons.KeyFor`. Glyphs sit in a 32 box: the 24-pixel stat glyphs unscaled, the
  64-pixel icons halved, so neither smears.
- **Chips are the solid element colour with the WCAG-picked ink** (`StatIcons.ReadableOn`), nudged by
  `CodexRows.ChipFill` until they clear 4.6:1 - Earth's brown was 4.48:1 against either ink.

**Every row container ignores the mouse.** A row is text to a pointer, so a finger drag that
starts on one reaches the `ScrollContainer` behind it (both lists are standard ScrollContainers,
so a fix to finger-drag scrolling applies to them unchanged). Only the Set aside pill stops input.
For a controller, a row with no enabled control of its own gets a focus-only overlay
(`Entry.FocusStop`, a `MakeCardClickOverlay` with the mouse filter set to Ignore) - one stop per row
- because the stick can only scroll a list by moving the selection through it. Pressing Set aside
rebuilds the book; the selection is put back on the same page's pill (`RefreshAfterCuration`)
rather than jumping to the top. **The pause tabs give rows no focus stop**: the pause screen's
buttons take no focus so that `EscapeRoot` keeps it and hears B and Escape, and a focusable row
would take that away.

The inks are `CodexRows.Vellum` and `CodexRows.Stone`, mirrored in `tools/art/ui_contrast.py`
(`VELLUM_*`, `STONE_*`, `chip_fill`) and measured against the row face, the bare panel (quiet rows
have no face) and the pause panel: all pass, the weakest prose being the meta ink at 6.1:1.

## 4d. Element tags you can tap

From the phone: "make the tags clickable on mobile so I can see what they do". A chip said
"Lightning 1/2" and nothing on any screen said what Lightning was for or what the 2 was a threshold
of; the level-up card wrote one tier's sentence under the row on a press, and nothing else did
anything. **Every element chip is now an `ElementChip`** - the level-up cards, the ascension
browser, the chest pages (the relic's tags were words in the rarity line; they are chips now),
the Spellbook and the pause screen's Spells tab, through `CodexRows.BuildChip` everywhere but the
level-up card, whose chips keep their 88%-over-the-card face (now nudged by `ChipFill`: Fire on
the boon enamel was 4.45:1). A tap opens **`ElementTagPopover`**, one card built by one function:
the element on its own colour, how many the player holds and their tier, what taking the thing the
chip sits on makes it (`CountAfter` on a card, `Gain` on a relic), each tier's effect - ticked when
reached, "N more" in the flag ink on the next one - and what else the tags buy (the attunement
bonus, the ascension's 4). On a PC the same card is the chip's hover tooltip; a touchscreen gets
none, since an emulated mouse left resting on the chip after a tap would raise a second copy.

Why each part is the way it is:

- **The chip is a Button.** It fires on release and drops its press when a ScrollContainer sends
  NOTIFICATION_SCROLL_BEGIN, so only a tap opens the card and a drag that starts on a chip belongs
  to the list (the touch-scroll work, which turns STOP into PASS inside lists, leaves this right).
  It is a focus stop the navigator understands, so a pad lands on it and presses A; MenuFocusHighlight
  rings it. Its STOP filter is what keeps a tap on a PC level-up chip from choosing the spell: there
  the chip is inside the card's Button. On a phone the card Button is a sibling, never an ancestor.
  The old chip opened on PRESS and accepted it, so a drag from a chip could never reach the list.
- **The pause screen's chips take no focus** (CodexRows: chips are focusable only where the row
  would be - `FocusStop` or a `Trailing` control), because a focusable control there would take the
  selection off `EscapeRoot` and B could no longer close the pause.
- **The card is its own CanvasLayer (150)** under the root - above every menu, below the scene
  transition - with ProcessMode Always. A full-screen catcher under it spends the next press on
  closing, a press on the chip included (that is the toggle). Spending it is the point: a tap that
  closed the card and went through would choose whatever card was under the finger. B, Escape or
  Start close it too, and so do the selection leaving the chip, the chip leaving the tree and the
  screen hiding. It sits under the chip, or over it when there is no room, clamped inside the safe
  area. It is stone on every ground, written in `CodexRows.Stone`'s inks (6.1:1 and up).

`_UiShot --tap-test` drives all of that with real touches through `Input.ParseInputEvent` and prints
PASS/FAIL per behaviour - tap opens and does not choose, tap elsewhere closes and chooses nothing,
a drag opens nothing, B closes it - at either canvas. On a desktop a ScrollContainer does not
drag-scroll from mouse events, so it proves the chip lets go of a drag, not that the list moves.

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
  `--open-pause` opens the in-run pause screen, and `--press-late=EscapeTabSpells` (or
  `EscapeTabDeeds`, `EscapeTabRun`, `EscapeTabOptions`) then opens one of its tabs. The menu's own
  lists are `--scene=res://scenes/TitleScreen.tscn --open-menu --press-late=SpellbookButton` (or
  `AchievementsButton`). `--scroll=N` holds every visible list N units down (a large N is the end),
  `--demo-level=N`, `--demo-spell-level=N` and `--demo-spells=id,id` put the run in a state the
  ascension browser has something to say about (pair with `--grant-boons` for tags; the forced
  level-up adds one more level, so `--demo-level=16` photographs a player at 17), a trailing `*` on
  `--press-late` matches by prefix (`--press-late=ElementChip_*` opens the first chip's card),
  `--tap-test` is section 4d's input check, `--mixed-deal` re-deals a forced level-up as one card
  of each family (a new spell, an upgrade, a boon; with `--demo-spell-level=3` the upgrade is the
  level 4 mutation card), `--focus-card=N` puts the selection on card N, `--lock-branch` stamps the
  last branch of `--show-mutation`/`--show-ascension` locked, and **`--ink-report`** writes
  `user://<shot>-ink.txt`: every visible label and text button in a menu, its declared ink (hover
  or focus colour in that state, times every modulate above it) against the photographed pixels
  in its box - the dominant ground and the worst ground covering 4% of it - against 4.5:1 (3:1 at
  24px and up). The harness also drops real key, mouse and pad presses while it runs (`_Input`,
  `ProcessMode` Always): its window takes the keyboard when it opens, and an Enter typed into
  another program was choosing the focused card before the shutter. **Every `_UiShot` run sets
  `SaveManager.WritesDisabled`** - any run the bot drives can end, and an ended run pays its Arcane
  Energy into the real save; it used to be off only for `--force-death`.
  `tools/art/_ascension_before_after.png` (gitignored) is the before and after of sections 3a, 4
  and 4d; `tools/art/_card_families_sheet.png` (gitignored) is sections 2c and 2d - every family
  frame tiled at card size in both states, eleven screens before and after at both canvas shapes
  (`--mixed-deal`, `--focus-card=1`, `--demo-spell-level=3`, `--show-mutation`,
  `--show-ascension`, `--show-apotheosis`, `--lock-branch`, the Penknife through `--fill-book
  --open-chest --chest-offer=scribes_penknife,blank_folio --press-late=ClaimCard`, the browser, the
  strip, `--max-book`), and three `--burst=3,20` frames of the pips;
  and `--demo-curation` gives the in-memory save two Redactions and sets Fireball aside, with
  writes disabled, so the curation controls can be photographed. To photograph every menu at both canvas shapes,
  run the harness once per screen at `--resolution 1280x720` and at
  `--resolution 540x1212 --portrait --safe-area` (the Pixel 9), one Godot at a time.
