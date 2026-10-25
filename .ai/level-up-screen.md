# The level-up screen

The screen a player looks at more than any other in the game: three choices, a footer, and two
overlays reached from it. This is what it is made of and the three traps in it.

## 1. Two registers, and the trap that hid one of them

The screen follows the rule in `art-direction.md`: **stone outside, manuscript inside**. The panel
frame, the Reroll and Skip buttons and the Ascensions button are carved stone. The evolution page
and the ascension browser are parchment; the cards on them, and the three level-up cards, are each
marked for their kind (section 2c) - every spell card is a parchment page, and only the boon and
the level 8 plates are objects of their own.

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
length of the band so the repeat is seamless. **And it is sliced at 24, like every family frame**
(`CardFamilyArt.PatchMargin`): the page was sliced at 20 for its first months, which took four
pixels of each corner block - the gold leaf's arms and the fibre flecks - into the bands and the
centre and tiled them as a row of gold dashes along the rule and a grid of dots across the face.

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

**Every spell card is this page.** An upgrade used to be it resting on its lit frame; then, for a
day, a red silk bookmark ribbon; now it is the page again, rubricated (section 2c).

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

The first answer gave every kind of choice its **own object** - a red silk bookmark ribbon for an
upgrade, white vellum in a dark teal band with a wax seal for a mutation - and the player sent two
of them back the same day: "the level up coloring should be subtler, maybe just an outline to the
page", and "mutation is currently colored white. we need to again make the color subtle, around the
edges, but the mutation can have an ethereal animated glowing to signify its ability to be
mutated." The lesson in that: value is the loudest cue there is, and the two commonest choices in a
run cannot afford to be loud. **Every spell card is the torn page now, and the mark the scribe put
on it is the whole difference.** The boon and the level 8 plates keep their own objects: they are
not spells, or not ordinary ones, and they are rare enough to be loud. `scripts/CardFamilyArt.cs`
holds the enum, frames, overlays and inks; `LevelUpMenu.FamilyOf` picks the family;
`tools/art/ui_frames.py` draws every frame, the three pages from one function
(`torn_page_card(mark=...)`) so they share a tear and differ only in the mark.

| Family | Object | Mark / furniture | Writes in |
|---|---|---|---|
| **New spell** | the torn page, flesh `hi` | none: a faint parchment rule, gold leaf corners | `Ink` family (dark) |
| **Upgrade** (and Marginalia, and the Penknife's picker) | the same page | **rubricated**: a double rule in the scribe's red (red `lit`, the `Rubric` ink) round the text block; the pip row (2d); "Level up · Attack"; the deltas boxed in the same red | `Ink` family |
| **Mutation** (the level 4 card in the hand, and each branch) | the same page | the deckle **stained** teal (lichen `lit`, with a lichen `hi` inner pixel) and a teal rule; the red wax seal on the top right (`wax_seal`); **`MutationGlow`** breathing behind it; branch icon in a teal roundel (`mutation_roundel`) | `Ink` family |
| **Boon** | reliquary plaque, violet enamel (2a) | machined gold bezel, chamfers, glass window with the aura, bail above | Boon inks |
| **Ascension** (the level 8 fork's path plates, the browser's path plates, the ASCEND strip button) | obsidian plate, arcane `deep` glass in a gilded band of runes (`obsidian_plate`) | gold bevel; four rune shapes on a 24 period, gem bosses | gold hi name, bone hi, arcane lit, gold lit advice |
| **Apotheosis** (its page, the strip button) | the same plate in red `deep`, runes in the Fire ramp (`obsidian_plate(ember=True)`) | burning runes | Fire hot name, bone hi, Fire mid, gold lit |

Why these, and the calls that were not obvious:

- **The rubric is "improve what you have", in the ink the improvement is written in.** The deltas
  on an upgrade card are `Rubric` (red `lit`); the page is ruled in the same red, outside and
  inside the text block, two pixels of parchment between so it reads as a double rule and not a
  thick one. 8.6:1 on the parchment as a graphic, so it is the line a thumb-sized card still shows
  (the phone photograph in the sheet) - and still only a line. At rest both rules are red; lit
  gilds the inner one, as on every page, and the outer stays.
- **Marginalia is rubricated**: it improves what the player has (heal, +2% damage) and is not a
  spell, so it may not be an unmarked page. Its type line says "A note in the margin".
- **The Scribe's Penknife picker is rubricated pages** (`ChestItemSelectionMenu.BuildSpellPage`):
  it lists spells the player *holds*, and a held spell is the rubricated page wherever it is the
  choice. Its icons went back to being deepened toward ink (they were drawn as-is on the silk),
  and its inks to `CardFamilyArt.PageInk` and `PageInkSoft`; the protected spell's fade stays at
  0.7, where `Ink` measures 4.65:1. **The Blank Folio** needs no family of its own: the page it
  binds in is filled by an ordinary new-spell torn page. The chest's relic cards are still torn
  pages - that screen is outside this pass and is the one place left where something that is not a
  spell wears the page.
- **The mutation is stained, not framed.** A binder coloured a book's edges; the mutation page's
  tear is lichen `lit` where every other page's is flesh `base`, one lichen `hi` pixel soaked in
  behind it, and its text block is ruled in the same teal. The wax seal stays: it is what says
  ceremony. The level 4 card in the hand is this page too, so the ceremony starts on the card
  ("Mutation · Attack"). Level 8 is not a card (3a); the ASCEND and APOTHEOSIS strip buttons wear
  the obsidian and ember plates instead of the footer's stone, so the button and the page it opens
  are one object.
- **The glow is behind the page, so it can never sit under a word.** `scripts/MutationGlow.cs`,
  `ShowBehindParent`, a `Control` reaching 7 outside the card: a haze of one-pixel rings that is
  strongest inside the card's edge - seen only through the bites of the tear, so the tear itself
  lights - and falls off to nothing at 7, two glints with a core of light that go round the page
  half a turn apart (5.5 s a turn), and ten motes that lift off the edge and fade (2.8 s a life,
  golden-ratio spaced so they never bunch). The breath is 3.4 s and moves only the alpha, never the
  size - pixel art that grows by fractions shimmers. Ethereal, not an element: the light drifts
  between the wind ramp's pale teal and the arcane ramp's lilac over seven seconds, so it never
  settles into reading as one element's colour. The rings nearest the page take each ramp's `edge`
  tone (dark enough to show on the light evolution page); the outer ones the `mid` tone (it glows
  on stone). Brighter while the card is hovered, focused or pressed, on `Time.GetTicksMsec`
  (runs while paused), no allocation per frame, a redraw only while visible, `ProcessMode` Always.
  If a reduced-motion setting is added it should hold still at the top of the breath with no
  glints or motes. 7 is less than half the 14 between a PC row's cards, so it never lies on a
  neighbour. Photograph it with `--burst=3,45`.
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
- **All of them tile** (`CardFamilyArt.Frame` and `BuildCardStyleBox` set `AxisStretch` Tile,
  margin 24): flat centre, bands carrying only lines parallel to the edge or patterns whose period
  divides the 24-pixel band (the tear tables 24, runes 24), irregular furniture only in corner
  blocks. The roundel is stretched, like the boon window. Overlays (seal, bail) are sprites at
  their authored x2; the glow is drawn.
- **Focus.** In the plaque and the plates the outermost two drawn pixels are dark (occlusion, then
  a dark lip) so `MenuFocusHighlight`'s ring - two pixels in - lands on dark and stays visible;
  the obsidian plate gained a lip of glass outside its gold bevel for exactly that. A page's
  outermost pixels are the bites of its tear, which the ring has always lived with. Hover and focus
  lift to the lit frame, which only changes the furniture.

**Contrast.** `tools/art/ui_contrast.py` measures every family ink against its frame in both
states (section "card families" of its table: 183 pairs, 0 below target; the three pages share
the parchment inks - `Ink` 10.6, `InkSoft` 9.8, `Rubric` 8.6, `InkGold` 7.5 - and the marks are
measured as graphics at 3:1: the rubric 8.6, the stained tear 4.9, the pips 3.8; weakest text
anywhere 4.65:1, the faded protected page in the Penknife picker). The photographs are measured
too: `_UiShot --ink-report` samples the real pixels behind every visible label (section 5). The
first pass's before and after, on the plates and the (then vellum) mutation page:

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
browser was already clean (weakest 4.6:1, an element chip) and is unchanged. The second pass put
the mutation back on parchment, where its inks are the page's (above); the ascension screens are
untouched by it.

## 2d. The level pips are alive, and only on an upgrade

From the Pixel 9: "on leveling up spells, animate the pip for the level it's becoming".
`scripts/LevelPips.cs` replaced the row of `StyleBoxFlat`s. **One pip per level** (it was one per
level after the first, every pip the pick reached drawn already filled - an upgrade to 3 looked
like a spell at 3). Three states: held (solid, steady), **the one this pick reaches** (a thicker
ring, a fill that rises and falls, a halo two pixels out that breathes with it), and not yet (an
empty socket).

**Only an upgrade card has the row** (`LevelUpMenu.BuildLevelPips`). A new spell's card animated
pip 1 for a day and the player asked for it to go ("when you are offered a spell for the first
time, we do not need to see the level up animation pips"): a lone breathing socket at the head of
six empty ones says nothing the word "Attack" under the name does not, and on a PC it cost the card
the 20 its chips now stand clear of the frame with (2e). The mutation card in the hand has none
either: it is a milestone, named as one ("Mutation · Attack"), the seal and the glow say so, and
its level is the ceremony rather than a step. A boon never levels, and a branch on the evolution
page is not a level. So the one moving pip on the screen is on the card that improves something
already held - which, with the rubric and the "Level up" line, is what marks it.

Calm: a one-second eased cycle (`0.5 - 0.5 cos`), on `Time.GetTicksMsec`, which runs while the
level-up has the tree paused, so every row on screen breathes together. While the card is hovered,
focused or pressed the floor rises from 0.15 to 0.45 and the halo brightens (`FocusOwner`, as
`BoonAura` does it). No allocation per frame; it redraws only while visible; `ProcessMode` Always.
Gold base on the page, 3.8:1 as a graphic (the old gold leaf was 2.4:1); the becoming pip rises
to gold lit. Photograph it with `--burst=3,20`. There is no reduced-motion setting yet; if one is
added it should hold the pip at full fill.

## 2e. The foot of a card

From the Pixel 9: "the spell tags are too low on the level up pages, they are all the way against
the bottom of the border." They were: measured from the photograph (clear rows of face between
the lowest content pixel and the frame's inner border, in canvas units), the chips' boxes ended
exactly on a page's rule and two pixels *into* the plaque's groove, and the advice line on a plate
was one pixel off the gilt.

Every card now ends its content box `CardFamilyArt.FootInset(family)` from its bottom edge: the
frame's inner border (`InnerBorder`: 14 on a page, 16 on the plaque, 18 on a plate - the first row
inside the innermost line of the band) plus **`FootClearance` = 12**, the air a PC card already had
over its icon, so head and foot match. `BuildWideOptionCard` sets its content's `OffsetBottom` to
it; `BuildNarrowOptionCard` its margin's top and bottom (the row is centred in the card, so it only
bites when the text is taller than the card - four stat lines, the four-card hand - which is exactly
when the chips sat on the rule); the two evolution card builders their foot. Not the side padding
(16 to 20): on a PC the content box is already the frame plus two, and matching the sides would
have cost the tightest hand its last stat line.

| Card (gap below the lowest content, canvas units) | Before | After |
|---|---|---|
| PC page, chips (`--mixed-deal`, `--tall-deal=3`, `--tall-deal=4`) | 0 | 12 |
| PC plaque, chips | 0 (-2 into the groove) | 12 |
| PC mutation card in the hand, its box | 19 | 39 |
| PC mutation branch, advice line | 10 / 5 / 10 | 18 / 13 / 18 |
| PC ascension or apotheosis branch, advice line | 0 / 1 | 18 / 13 |
| Phone page, 4-card hand (`--tall-deal=4`, 300-tall rows) | 2.7 / 2.7 / 2.7 | 12 / 10.7 / 12 |
| Phone mutation branch | 9.3 / 8 / 12 | 14.7 / 14.7 / 18.7 |
| Phone ascension or apotheosis branch | 8 / 5.3 | 18.7 / 14.7 |

(A phone's three-card hand is taller than its text and centres it, so those gaps were already 55 to
100; a card the scroll cuts off reads as 0 and is not a gap.) The 13s are a label's descender box.
Nothing overflows that did not before: the tightest three-card PC hand (`--tall-deal=3`: four stat
lines, two chips, a two-line name) fits with the same 2 to spare; **the four-card hand now fits
too** - `GetResponsiveColumnCount` deals four across, where three put the fourth card on a second
row with the heading and the footer off the screen, and a four-across card steps its icon down to
64 so a two-word name's second line fits. A phone's three-card hand still scrolls by the 40 it
always did.

The gap is read from the photograph by a script outside the repo and from the geometry by
`_UiShot`'s card report (section 5), which prints every card's frame, rect and lowest content.

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
Apotheosis's. Rerolls and bans re-deal the cards and leave it alone. Pressing an ASCEND button
opens the level 8 **fork** (section 4e: the spell on top, an arrow down to each path plate, and
only the plates are pressable); an APOTHEOSIS button opens the ember evolution page as before.
Choosing a path spends the pick.

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
  whenever an Ascension is on offer. It predates the glow (see the before half of the sheet). The
  charge row (Ban, Save, Augury) does the same when a player owns charges: 468-tall cards, the
  heading, the charge row and both footer buttons are 724 in a 648 panel. **The ★ Ascensions button
  follows the column as well as the heading** (`AlignAscensionButton` is connected to both
  `ItemRectChanged`s): a level-up builds its cards before it hides an empty charge row, so for one
  layout the column is 66 taller and moves as a whole - the heading's rect inside it never changes
  - and the button was left aligned to a heading 33 above the panel.

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

**It is a forest of small trees** (`scripts/LevelUpMenu.Ascensions.cs`, from the Pixel 9:
"ascensions menu should have a more distinct UI for the options. the cards blend into the
background. if there are two options available for the same spell, it should show the spell on
top, and two arrows pointing below to the 2 ascension options available"). Two designs preceded
it. The first was a heading per spell and a box per branch with a "Still needed:" run-on - one
weight throughout, a met gate simply absent. The second was a CodexRows checklist, one row per
*path*: it fixed the gates (ticked, with the shortfall in words) but the two branches of one spell
were two rows each saying "Ascension of Cinderbreath" in small print, so that they were the same
decision was something to read rather than see, and a row was a gold-hi face on a flesh-hi page -
one step of value apart, which is what "blends into the background" was. Now, **one tree per
spell**:

- **The spell is the node at the top**: its icon, its name (Title, never wrapped), its level as
  `LevelPips`, and the **checklist of the gates its paths share** - spell level, element tags, your
  level - a `done` tick on each that holds and the `todo` socket on each that does not, the
  shortfall in words on a rubric band ("Needs 3 more Lightning tags"). The node is the page with a
  2px edge in the darkest ink - a root you have to squint for is not one - and takes the gold-hi
  face and a "Ready" tag when any path is open. A gate the paths do *not* share (an untagged
  branch of a hybrid takes either of its elements; its tagged sibling only one) is written under
  the path it belongs to, so nothing claims to gate both when it gates one. The rule is said once,
  under the heading. On a PC the checklist shares the node's row, so the node is one band and the
  plates start above the fold; on a phone it runs down the node.
- **The fork** is `scripts/AscensionForkArrows.cs`: a trunk out of the node's centre, a bar, and
  an arrowhead landing on the top of each plate. Into an open path it is solid gold base (3.8:1 on
  the page as a graphic) with a glow that breathes on the Ascension button's 2.6 s clock and a
  spark that runs down it every 2.4 s; into a closed one it is dashed in the meta ink. Every point
  is measured from the plates' real rects and it redraws when they move, so a plate that wraps to
  an extra line cannot leave an arrow pointing at the gap. When the plates do not fit side by side
  (`FitsAcross`: under 240 of plate on a phone, 300 on a PC) they stack and the fork becomes a
  **spine** down their left with an arrow turning into each.
- **The paths are the Ascension's own object, the obsidian plate** (section 2c) - the darkest
  thing on the lightest page, so they cannot blend into it. Ready: the lit frame. Not yet: the
  resting frame dimmed to 78% (`PathSeenDim`, a `SelfModulate` on the frame alone, so greying a
  plate can only darken the ground under light ink). **Sealed** (section 4e): dimmed to 56%,
  "???", the sealed glyph where the icon goes and one line saying why. The picture sits beside the
  name and the words run full width under it; on a phone's side-by-side plates the picture goes
  above the name, because beside it "Cataclysm" at the Title size broke mid-word.
- **Sections by what the player can act on**: *Ready* trees first, then *Not yet* (or *Ascensions
  to earn*), the near misses at the top; spells with no ascension fold into one quiet line.
- **It still only reports.** Nothing on the page is pressable: the node's chips (or a focus-only
  overlay) and one overlay per plate are a pad's stops, wired spell → left path → right path → next
  spell (`WireTreeFocus`, explicit, because from a node centred over two plates Godot's geometric
  neighbour could land on either). Every container ignores the mouse, so a finger drag reaches the
  scroll.

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
level-up cards, the boon effect line, the chest pick and the Arcane Upgrades effect lines all use
them. `sealed` (a gold medallion with a question cut into it) marks an ascension path never yet
met (section 4e) - not `lock`, which says "not got yet", a thing a seen path out of reach also is.
**A new stat line needs no code if its words match a key; check `KeyFor` before adding a
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

**Five relics** (John Dee with the Relic Key, 2026-10-22): five abreast on a PC broke names mid-word
and needed 856 of 595, so `fiveAbreast` sets the name at the Label size, the art at 48 and the set
art at 40, the margins at 12, and shows a set's full tier only when the claim completes it - worst
575 of 595 after. On a phone five stacked pages scroll by about 460px. `.ai/passives-and-items.md`
section 8d has the photographs' setup.

`--give-relics=a,b` and `--chest-offer=a,b,c` on `_UiShot --open-chest` pin what the player holds
and what the chest deals, so a before and an after show the same three cards.

## 4c. The Spell Index and Unlocks lists: whitespace is the hierarchy

**The names.** The player renamed both lists, and the shop with them: the **Spell Index** was the
Spellbook ("spell book" already means the spells carried in a run - `Player.IsSpellBookFull`, the
Scribe's Penknife - and this is the catalogue of every spell and where to find the rest),
**Unlocks** was Deeds (every entry exists to unlock something, and its reward line says what),
and **Arcane Upgrades** was the Arcane Codex. The words live once, in `CodexRows.SpellIndexTitle`,
`SpellIndexSubtitle`, `UnlocksTitle`, `UnlocksSubtitle` and `UnlocksSummary` ("13 of 26 unlocked"),
so the main menu and the pause tabs cannot drift apart. Internal names - the panels
(`SpellbookPanel`, `AchievementsPanel`), the lists, `BuildDeedRow`, achievement ids and every save
key - kept the old words on purpose: the save is persisted and the rest was not worth the churn.
The history below is written in the names the screens had at the time.

Each list carries a one-line subtitle under its heading, in the panel's soft ink, saying what
kind of page it is before the counts do ("Every spell, and how to find the ones you have not.";
"Finish one to earn its reward - a new spell, a boon or Arcane Energy."). On the pause screen
the same line leads the summary. The pause tab is called Spell Index too, not "Spells": the
player read "Spells" as the spells held this run, which is the Run tab's list - now headed
"Your attacks this run" and "Your passives this run" so the two cannot be confused.

The Run tab says the rest of what the run holds, too. **Spell details are closed by default**
behind one Show details / Hide details toggle at the right of each spell section's heading (one
state for the tab, kept for the run): they used to be an always-on "Classification: Targeting
NearestEnemy | Shape ProjectileHit | Scaling ..." line in the engine's enum names, and are now three
sentences - "Aims at the nearest enemy.", "Hits with a projectile that strikes what it meets.",
"Improved by damage, cooldown, ..." - from `SpellClassificationText`, which has a phrase for every
value of the three enums (`RegressionChecks.ValidateSpellClassificationText` warns on a missing
one). The pause screen's buttons take no focus (EscapeRoot keeps it, so B closes the pause), so a
pad cannot reach the toggle: **Y** on the Run tab does the same thing (EscapeRoot's GuiInput).
**Boons** show the element tags they add as chips (`CodexRows.BuildChip`, unfocusable, a tap opens
the element card), and a **Relics** section lists every relic held - icon, name, effect, tag chips -
then one line per Full Set Enchantment the run has started ("The Reliquary 2/3", gold with
"complete" once whole). The active element-passive rows now pick their ink by WCAG against the
colour as drawn (the old brightness threshold put white on the Arcane badge at 4.1:1).

On the main menu the Spell Index is not in the progression row any more. That row is the two
places that make the next run stronger, Arcane Upgrades and Unlocks; the Spell Index changes
nothing, so it is a quiet button in the utility row with Options and Report a bug
(.ai/title-screen.md, "How the menu is laid out against the artwork").

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

## 4e. The level 8 fork, and paths sealed until first met

**Level 8 is the same tree, for one spell** (`LevelUpMenu.BuildAscensionChoicePage`, reached from
`BuildEvolutionSelectionButtons` for milestone 8 only - the mutation and the Apotheosis keep the
evolution page). The node shows "Level 7 → 8" with the eighth pip breathing (`LevelPips.Becoming`,
the level this choice buys); the plates *are* the buttons, built like the phone's option card - a
`PanelContainer` holding the `Button` and the words as siblings, so a plate grows to its text and
is one target for a thumb and one ring for a stick. A locked path (its element gate unmet,
`EvolutionLockReason`) is a disabled, dimmed plate with a dashed arrow into it and what it wants on
the bone-on-red band under it. The subtitle counts the paths and says the stakes: the spell takes
only one, for the rest of the run. Focus: left path, right path, Back; the selection starts on the
first open path. On a 1280x720 PC the whole fork fits above the fold with the band showing - it
did not until the picture moved out of the text column and the node went to one row.

**Sealed until first met.** From the Pixel 9: "the ascension effects and icons shouldn't be
visible until the player meets the requirements for the first time. once they've seen it (even if
they don't pick it), then it can be always visible." The browser had listed every branch of every
spell in full from the first level-up, so the biggest reveal in a run was a list read on minute
two. `scripts/AscensionSightings.cs` holds the rule in two halves that are deliberately apart:

- **Shown** is `IsRevealed(met now, or seen before)`. A path whose requirements hold on *this*
  level-up is shown in full on this level-up, whether or not anything was written - so a run that
  may write nothing (every harness) draws exactly what a player would see.
- **Remembered** is `Record`, called once per level-up from `Node2DGame.OnPlayerLevelGained` after
  the menu is filled: every path with no unmet requirement in `Player.BuildAscensionPreview`, and
  every Apotheosis form in the strip (met when offered), goes into
  **`SaveData.SeenAscensionIds`** as `spell_id:branch_id` and the save is written on the spot -
  not at the end of the run, since a run quit from the pause screen never reaches the game-over
  save. Keyed by spell *and* branch because the derived evolutions share ids across spells.
- **Never by a tool.** `Record` returns at once when the current scene is a `_` tool scene
  (`IsToolRun`), on top of `SaveManager.WritesDisabled`: the balance bot without `--all-unlocked`
  and the boss probe still end runs into the real save, and a bot that met a path would reveal it
  to a player who never stood in front of it.
- **Schema 11** (`SaveData.CurrentSchemaVersion`), `MigrateToSeenAscensions`: starts every older
  save empty, which is the honest answer - nothing an older save wrote records a branch met or
  taken (telemetry keeps upgrade counts, `GameStats.ApotheosisIds` is per run and never saved), so
  there is nothing to seed from and a guess would reveal paths the player never saw. A returning
  player's sealed paths open on the first level-up that finds them met, as a new player's do.

A sealed path shows "???", the `sealed` glyph, the plate at 56% and "Sealed until you first meet
its requirements." - and nothing else: no effect, no icon, no synergy tag, no element line, no
advice, since each would give part of it away. Its gates still show on the node, because what it
*wants* is exactly what the player is meant to chase. On the level 8 fork a locked path that was
never met is sealed too; one met in an earlier run is shown locked but readable.

**Measured.** `tools/art/ui_contrast.py` ("ascension tree": the plate inks on the lit, the
78% and the 56% frames, the node's edge and pips, both fork lines - 0 of 199 pairs below target),
and the photographs through `--ink-report`: nine shots at both canvases, every label at or above
its target; the weakest are the Fire chip on a phone node (4.9:1) and the gold-lit element line on
a PC plate (6.9:1 against 3:1 at 24px).

## 4f. Arcane Upgrades: the shop is the same book

From the player: "arcane upgrade menu is unreadable. rebuild the whole menu making it legible,
seperating all of the spells more and theming it like the game for upgrades. including animations,
icons for the powerups, and making it clear which upgrades are at max." It was one grid of identical
dark boxes - four across on a PC, two on a phone - each a centred name, a Micro line of "Lv 3/8
Cost: 90 AE Buy", a glyph-led effect and a centred "Current total bonus" sentence. The five spells
for sale were more boxes at the grid's foot ("Unlock Gravewell"), and a maxed upgrade was told
apart by a grey "Maxed" on a button you could not press. `scripts/MainMenu.Shop.cs` builds it now.
The **behaviour is the old shop's, exactly**: the same definitions, prices, ceilings, purchase, save
and refreshes. Only the per-level copy was reworded, against what `Player` does with each one.

**It is the level-up screen's book.** A level bought here is the same kind of thing as one taken in
a run, so it wears the same family (section 2c):

| Entry | Object | What is on it |
|---|---|---|
| **Spell for sale** | the new spell's torn page (`ui-spell-page`) | the spell's own icon bare on the page, name, what it does, its element chips (tap to read, not focus stops), the price |
| **Permanent upgrade** | the rubricated upgrade page | its icon in an **arcane socket**, name, the stat now **→ next level** in the rubric, what one level gives, the level pips, the price |
| **Mastered** (an upgrade at its last level) | the same page **gilded** (`ui-spell-page-mastered`: old-gold outer rule, gold-leaf inner) | the **gold seal** in the top right, every pip full, "✓ Mastered" where the price was, no "→ next" |
| **Owned** (a spell already bought) | the new spell's page | "✓ Owned" where the price was |
| **Not open yet** (Redaction on a book under 13 spells) | the upgrade page | "Opens at 13 spells" and a lock where the price was |

Red says "this can still be improved"; gold says it is finished. The seal is not the mutation's wax
seal: that is red wax and means ceremony in a run; this is a gold rosette and means done for good.

**Layout.** The heading is the level-up screen's band (stone deep, gold rule) with the **purse on
its right**: the energy orb at x4, the amount at the Title size in the prose face, "Arcane Energy"
under it. It stays in view while the list scrolls. Then one line of intro, and **sections**,
CodexRows' heading with a tally ("1 of 7 mastered", "1 of 5 owned") and one line of note: **New
spells** first while any is for sale, last once all are owned; then **Power**, **Survival**,
**Fortune** and **Level-ups**. An upgrade the code registers but no section names still gets a card,
in a last "More" section, with a warning (`UpgradeSections`). One column on a phone, two on a PC
(`CodexRows.ColumnsFor`), and the panel takes the codex lists' 1200-unit column.

A card reads top to bottom: name (Title, the strongest ink), stat line (Body, its glyph, "Spell
damage x1.24 → x1.32"), "Each level: ..." (Label), then the foot: the pips over "Level 3/8" on the
left and the **plate** on the right. The foot sinks to the card's bottom, so the plates of a row
line up. Twenty-level tracks (Fortune Thread, Keen Focus) wrap their pips at ten
(`LevelPips.PerRow`), so they read as tens rather than as a row of dots to count. Calls that were
not obvious:

- **The price is in the foot on both canvases.** On a PC it was first a column of its own at the
  card's right, to save the foot row's height on a 720-tall canvas. That cost the words their width:
  half the stat lines and every spell description wrapped, and the cards came out taller than the
  foot row had made them. Spell descriptions are the Label size for the same reason.
- **The pips sit over "Level 3/8", not beside it.** The plate makes the foot 74 tall either way.
  Side by side, a ten-pip track, the words and the plate pushed one card's minimum width past half a
  PC panel, and the two columns came out different widths.
- **The plate is a stone button with its words inside the iron band** (10 units a side): the price
  over "Buy"/"Unlock" in gold hi and bone on the lit stone, or the price over "Need 35 more" in skin
  base on the disabled stone - **dim, never faint**, 7.6:1. 172 wide on a PC and 188 on a phone, so
  "Need 165 more" clears the band.
- **The next pip breathes only when the next level can be bought now** (`LevelPips.Becoming`). A
  still socket means "not yet", so affordability shows even before the plate is read.

**Selecting and buying.** **Every card is one stop, the card itself** - its overlay, focus-only,
ignoring the pointer - whatever its state. **A on a card buys what it sells**, refuses out loud if
it is short or not open, and does nothing if it is finished. The plate never takes the selection.
It began as "the plate when the card can be bought, the card when it cannot", CodexRows' rule, and
`_UiShot --pad` showed what that does to a grid: Godot's focus search measures rect to rect, a
whole card a row down was nearer than a plate across the way, so "right" from Molten Shard landed
on Kindled Ward and A refused it. Cards of one size in a grid navigate like the grid. **A pointer or
a finger buys only through the plate**: a tap that meant "let me read this" never spends anything,
and a drag that starts on a page scrolls the list (the page is `MOUSE_FILTER_PASS`, everything in it
ignores the mouse). **B, Escape or Android's back** leave the panel - and every other main menu
panel, which had no way out on a pad but walking down to Back (`MainMenu._UnhandledInput`, only
while the selection is inside the menu, so a screen over it hears its own cancel first). The list
opens at its top with the first card selected and does not scroll on arrival (`OpenShopAtTop`).
Hover or focus lifts the page to its lit frame and brightens the socket - the furniture, never the
face (`ArcaneShopCard`). The overlay's usual 16% gold wash is turned off here, because it tinted the
selected parchment olive.

**A purchase is updated in place, never rebuilt**, so it can be watched landing on the card that
was pressed and the selection never leaves it:

- **the socket flares** (`ShopFlare`, 0.5 s): a flash in the glass, a ring of the arcane ramp
  opening out of the bezel a whole pixel at a time, eight motes thrown clear;
- **the pip just bought flares** (`LevelPips.Celebrate`): a white flash settling into the held
  fill, and a ring of gold opening off it;
- **the purse ticks down** to what is left over 0.45 s, and the amount spent ("-125") rises off it
  in the flag ink and fades;
- **at the last level the seal is stamped** (`MasterySeal.Stamp`): it falls through x5, x4, x3 to
  its resting x2 in 0.22 s - whole scales, so every frame is on exact pixels - onto a page that has
  already turned gold;
- **refused** (short, or not open): `ui_denied`, the plate's words shake a few whole units, and the
  purse flushes the flag ink. A purchase plays `ui_click` (`.ai/audio-manifest.md`, both as proposed
  there).

None of it blocks input. A second purchase restarts whatever is still in the air, and every other
card's plate re-prices in the same pass, which is how Tempest Rhythm turns "Need 5 more" the moment
Eternal Wick spends the purse below 35.

**Art.** `tools/art/upgrade_icons.py` draws the twenty upgrade icons (32 at x2, ids as the shop's,
`assets/bonelight/ui/upgrades/<id>.png`). Each is the token of a vow or a working - a bone tablet
with crossed blades burning in it, an hourglass bound in arcane thread, a phoenix feather, a die with
fate cracked through it - lit from the upper left, with its magic in the element ramps. All twenty
share one mark, a part-alpha fringe of the arcane edge tone: the light of the Arcane Energy they were
bought with. They are drawn for the **socket** (`ui_frames.py:upgrade_socket`): arcane night glass
in a gold bezel, 40 authored and shown at exactly 80, never nine-sliced, because the glass carries a
dithered light at its centre. The hearts (Vitality, Emberheart) are the ember row, as the heart and
regen glyphs are. The gilded page and the seal are `torn_page_card(mark="mastered")` and
`mastery_seal()`, which was redrawn from 18x20 to 22x24 when the first one read as a gold speck
beside the corner leaf. `python tools/art/upgrade_icons.py` also writes
`tools/art/_upgrade_icons_preview.png` (gitignored): every icon in its socket and bare on a page.

**Measured.** `tools/art/ui_contrast.py`, "shop" rows: every ink on the three pages in both states,
the plate in both states, the band and the stone panel - 0 below target. The weakest text is the
section tally at 6.95:1; the weakest graphic is the pips at 3.8:1. Through `_UiShot --ink-report` on
six photographs (top, Power, end, at both canvases): 163 labels, 0 below target. The weakest is the
shared Earth element chip at 4.5:1; the weakest shop ink is "Locked" at 5.8:1.

**What is still for a person to judge**: the feel of the purchase on a real mouse, pad and phone.
How a held stick paces down twenty-five cards. Whether the breathing pips on every affordable card
are calm or busy. That a finger drag over the list scrolls it on the phone: `_TouchProbe` proves the
drag reaches the list and buys nothing, and that a tap on a plate buys, but a desktop has no
touchscreen, so the list cannot move there.

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
  `--open-pause` opens the in-run pause screen, and `--press-late=EscapeTabSpellIndex` (or
  `EscapeTabUnlocks`, `EscapeTabRun`, `EscapeTabOptions`) then opens one of its tabs. The menu's own
  lists are `--scene=res://scenes/TitleScreen.tscn --open-menu --press-late=SpellIndexButton` (or
  `UnlocksButton`, or `ArcaneUpgradesButton` for the shop). `--scroll=N` holds every visible list N units down (a large N is the end),
  `--demo-level=N`, `--demo-spell-level=N` and `--demo-spells=id,id` put the run in a state the
  ascension browser has something to say about (pair with `--grant-boons` for tags; the forced
  level-up adds one more level, so `--demo-level=16` photographs a player at 17), a trailing `*` on
  `--press-late` matches by prefix (`--press-late=ElementChip_*` opens the first chip's card),
  `--tap-test` is section 4d's input check, `--pad-y` sends a real pad Y press after the warmup
(with `--open-pause`, it opens the Run tab's spell details the way a controller does), `--mixed-deal` re-deals a forced level-up as one card
  of each family (a new spell, an upgrade, a boon; with `--demo-spell-level=3` the upgrade is the
  level 4 mutation card), `--tall-deal=N` re-deals it as the N cards with the most to say (the most
  stat lines, then the longest name - the tightest hand a PC can be dealt; N=4 is Playtest Mode's
  four-card hand), `--focus-card=N` puts the selection on card N, `--lock-branch` stamps the
  last branch of `--show-mutation`/`--show-ascension` locked, **`--seen-ascensions=none,fireball,
  cinderbreath:cinderbreath_titan_cataclysm`** sets which paths the in-memory save has seen (section
  4e: `none` forgets the real save's, a spell id marks every path of a held spell, `spell:branch`
  one path - put `none` first for a repeatable shot; a met path shows in full regardless), and
  **`--ink-report`** writes
  `user://<shot>-ink.txt`: every visible label and text button in a menu, its declared ink (hover
  or focus colour in that state, times every modulate above it) against the photographed pixels
  in its box - the dominant ground and the worst ground covering 4% of it - against 4.5:1 (3:1 at
  24px and up). It covers the main menu too (its root over the title art and whichever panel is
  open); two of its FAILs there are the sampler, not the ink - the Options gear and the gold focus
  ring are inside the box and count as ground.
  Whenever a level-up is up, the log also carries a **card report**: one line per
  card wearing a family frame (its frame, rect and the bottom of its lowest content and lowest
  chip, canvas units) and whether the column or scroll fits, so the foot of a card (2e) is measured
  rather than eyeballed. The harness also drops real key, mouse and pad presses while it runs (`_Input`,
  `ProcessMode` Always): its window takes the keyboard when it opens, and an Enter typed into
  another program was choosing the focused card before the shutter. **Every `_UiShot` run sets
  `SaveManager.WritesDisabled`** - any run the bot drives can end, and an ended run pays its Arcane
  Energy into the real save; it used to be off only for `--force-death`.
  `tools/art/_ascension_before_after.png` (gitignored) is the before and after of sections 3a, 4
  and 4d; `tools/art/_ascension_tree_sheet.png` (gitignored) is the tree and the sealing of
  sections 4 and 4e - the browser and the level 8 fork, before and after, at both canvases, with
  a sealed, a seen-but-locked and an open path (`--press-late=AscensionsButton` with
  `--demo-level=16 --demo-spell-level=7 --demo-spells=fireball,frost_shard,molten_shard
  --grant-boons=lantern_oil --seen-ascensions=none,fireball`, scrolled 330 and 1000; and
  `--show-ascension --lock-branch`, with and without `--seen-ascensions=cinderbreath`);
  `tools/art/_card_families_sheet.png` (gitignored) is sections 2c and 2d - every family
  frame tiled at card size in both states, eleven screens before and after at both canvas shapes
  (`--mixed-deal`, `--focus-card=1`, `--demo-spell-level=3`, `--show-mutation`,
  `--show-ascension`, `--show-apotheosis`, `--lock-branch`, the Penknife through `--fill-book
  --open-chest --chest-offer=scribes_penknife,blank_folio --press-late=ClaimCard`, the browser, the
  strip, `--max-book`), and three `--burst=3,20` frames of the pips - it shows the ribbon and the
  vellum, which lasted a day; `tools/art/_card_fixes_sheet.png` (gitignored) is the pass that
  replaced them and sections 2d and 2e: the hands before and after (`--mixed-deal`,
  `--demo-spell-level=3`, `--tall-deal=3`, `--tall-deal=4`), the evolution pages, three
  `--burst=3,45` frames of the mutation glow, the three page frames, the Penknife and
  `--focus-card=1`, at both canvas shapes;
  and `--demo-curation` gives the in-memory save two Redactions and sets Fireball aside, with
  writes disabled, so the curation controls can be photographed.
  **The shop (section 4f):** `--demo-shop` replaces the save in memory with a known one: 155
  Arcane Energy, Runes of Ruin and Vitality at their last level, Eternal Wick one affordable level
  from its last, a spread of affordable and unaffordable rows, Molten Shard owned, Redaction not yet
  open. `--press-late` takes a comma list, one press per warmup, so
  `--press-late=ArcaneUpgradesButton,Buy_duration --frames=3 --burst=3,6` photographs a purchase
  that masters an upgrade. `--focus-late=NAME` puts the selection on a control after the presses
  (a trailing `*` matches by prefix). `--pad=right,a,down,b` sends real pad presses, one per warmup,
  and logs where the selection is before each and where it comes to rest. `--hover-late` shows the
  lift. `tools/art/_arcane_upgrades_sheet.png` (gitignored) is the before (main at 78b596b) and the
  after at both canvases - top, Power (mastered and affordable), end (too dear, locked), a pad
  step, two burst frames of a mastering purchase - and the twenty icons. `_TouchProbe ... --press=ArcaneUpgradesButton --tap=Buy_*` drags the list and then taps the first
  plate on screen (`--tap` now chooses the tap inside a list too, and a focus-only overlay is never
  taken for the thing to tap). To photograph every menu at both canvas shapes,
  run the harness once per screen at `--resolution 1280x720` and at
  `--resolution 540x1212 --portrait --safe-area` (the Pixel 9), one Godot at a time.
