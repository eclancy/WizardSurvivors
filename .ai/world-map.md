# The world map

Eric, 2026-10-29: *"Let's upgrade how map selection works. I want a world map that has all of the
different maps somewhere on it, in one contiguous drawing, separated by the drawn world. When the
player selects a character, that wizard is then placed on the world map, and they can press left or
right buttons to move between locations on the map, and the character Sprite will run over to catch
up with the camera. When highlighting a specific location, you will still see the preview card with
a small description of the location. It should also have silhouettes in the card to show what
unlocks are available on that map that haven't been found yet, that fill in when they unlock that
thing. Use the mobile game vampire crawlers as a reference if needed"*

And, the same day: *"Sketchbook can be a special final separate level that appears outside the
context of this map, but you should be able to scroll to it."* and *"There should also be a compass
style scroll bar at the top of the screen so you can see what levels you can scroll to."*

This doc is the design. It replaces the stage list (`StageSelection`) in the flow: CharacterSelection,
then the world map, then the run. Read with `.ai/campaign.md` (the order), `.ai/world-and-tone.md`
(why each place is held), `.ai/art-direction.md` (the contract, section 6a above all),
`.ai/controller-and-focus.md` and `.ai/stage-secrets.md`.

---

## 1. The world

**One picture, generated** (`tools/art/world_map.py`, never a hand-edited PNG): an overworld seen from
above, 1680x840 art pixels drawn at x2, in the Bonelight palette and under the same key from the upper
left as everything else. Every chapter is a *place* in it - a wood, a field system, a ring of stones,
a cave mouth, a keep - and what lies between them is drawn land: the road, a river, a scarp, a
mountain range, a coast. Nothing is a node on a blank ground.

**The road runs west to east.** The campaign is one road (`.ai/campaign.md`, "The gate": *being past
a chapter is having been through it*), and Eric's controls are left and right. So the first chapter
is at the west edge, the last at the east, and pressing right walks east: the controls, the road on
screen and the compass at the top all agree about which way is forward. The road winds north and
south between places, so the map is a world rather than a strip, and the camera scrolls on both axes.

| # | Chapter | Where | Why there |
|---|---|---|---|
| 1 | Enchanted Forest (`stage_0`) | the west, a deep wood running off the edge of the world | where the player starts; *"Every path now leads inward"* - the road leaves the wood eastward |
| 2 | The Fallow Fields (`stage_9`) | south of the road, just past the wood's edge: hedged fields, a millpond, the well | *"The farms beyond the wood"* (`campaign.md` 5) - the first place that was someone's livelihood |
| 3 | The Broken Circle (`stage_10`) | north, up on the high heath: heather, granite, eight stones and a gap | the moot was held high and apart; the road climbs to it out of the fields |
| 4 | Sunken Cave (`stage_2`) | below the heath's eastern scarp: the tarns' stream runs down and into a cave mouth | *"His shadow followed the water down into the dark"* - the water is drawn going in |
| 5 | Cursed Dungeon (`stage_1`) | a keep on a crag where the river comes out of the hills: the one way through | *"a grim choke-point"*; the road has to pass under its walls, which is why it is a gate in the middle of the campaign |
| 6 | Blighted Swamp (`stage_3`) | south-east, where the river spreads into a delta bog before the sea | the lowest land on the map; cold arcane lantern lights in the reeds (*"Nothing living lights them"*) |
| 7 | Mystic Ruins (`stage_4`) | north again, on a plateau above the bog: broken spires, rubble rings | the wizards' old seat of learning - *"The old wards were broken from the inside"* |
| 8 | Frozen Waste (`stage_5`) | the far north, past the ruins: ice field and wind ridges under the mountains | cold is where the land is highest and furthest from his fire |
| 9 | Scorched Sands (`stage_6`) | over the mountain pass, south-east: a dry basin, dunes, the trade road | the rain shadow of the range and the heat of his seat; *"He hid the water"* |
| 10 | The Emberdeep (`stage_7`) | the far east: black ground, lava, a volcano at the end of the road | *"His seat, and the end of the road."* The one place on the map lit by the `darkness` ramp as emission, at its summit (`world-and-tone.md`: he is the only source of dark light) |

The Emberdeep's summit is where the **Boss Rush** stands (section 5). Past it the land ends at black
cliffs and a dark sea, and the picture ends at the world's edge. **The Sketchbook is not in the
world** (section 4).

**Lighting and palette.** Ground from the deep half of each material's ramp, features lit on their
top and left, `occ` for contact and cliff shadow, no colour outside `bonelight.py`. The places' colours
are their chapters' ground colours (forest lichen, field gold-and-flesh deep, heath violet and stone,
swamp linen, snow in bone and steel, sand gold-and-flesh, basalt and fire), so each place reads as the
chapter the player is about to walk into. Emissive light is spent only where the fiction puts it:
his lanterns in the swamp (arcane), the Emberdeep's lava (fire), the seat (darkness), and the player.

## 2. Locked places: on the map, visitable, never startable

**Every chapter is drawn and every chapter is a stop**, open or not. The stage list hid closed
chapters entirely, and the cost it wrote down was real: the Emberdeep's gate - *every spell recovered
and every wizard freed* - was said nowhere until it opened. A map shows the places anyway, so letting
the cursor visit a closed one costs nothing and answers the question the picture raises: *why can I
not go there?* Its card gives the lock reason (`StageDefinition.LockedHint`, derived from the
campaign order) and what the place holds.

- **Drawn under his shroud.** A locked place carries a bank of dark fog over its landmark (a
  generated sprite, laid at runtime because the lock is the save's, not the art's) and the lock glyph
  above it. The road stays clear: the road is the drawn world, and the place is what he holds.
- **The wizard walks to the door, not in.** Every stop has a *stand point* on the road in front of its
  place. He walks there for a locked place as for an open one; the fog sits behind him.
- **A locked place can never be started.** The card's Start button says why instead (*"Locked -
  defeat Elderbark in the Enchanted Forest"*) and pressing it plays `ui_denied`. It is never
  `Disabled`: a disabled primary hands the selection to Back, and a pad user pressing A on a locked
  place would leave the screen. `WorldMap.TryStart` checks `GlobalStatsManager.IsStageAvailable` again
  at the moment of starting, whatever the button said.
- **Not built yet** (`IsPlayable = false`; none today, both Phase 2 chapters shipped): the same fog,
  thicker, and the card's line is *"Not yet open - this chapter is still being built"*. It is part of
  the drawn world like everything else.

## 3. The route, the wizard and the camera

**The route is one polyline** through every stand point in stop order (section 6), written by the
generator into `assets/bonelight/ui/worldmap/worldmap.json` with each stop's stand point, the point the
camera frames (its *focus*) and its shroud. The C# reads that file; nothing in code knows a coordinate.

**The wizard is a position along the route, in arc length** - one number. The selected stop is a
target arc length. Every frame he moves toward it along the line, so:

- **No teleporting, ever**: he can only move along the road, and a new target mid-walk simply turns
  him round where he stands.
- **Fast presses are free**: five presses of right set the target five stops on, and he runs there in
  one walk. His speed grows with the distance left (a floor of a brisk run, a ceiling so a jump from
  the first chapter to the last takes about two seconds), so a long jump is a run, not a crawl.
- **The animation cannot stick**: `moving` while he is more than half a pixel from the target, `idle`
  otherwise, chosen every frame from that one comparison. Facing is the sign of his horizontal motion,
  through `CharacterVisuals.SpriteFacesLeft` (the Stormcaller is drawn facing left). He is the
  selected character's own sprite (`CharacterData.Frames`, `moving` and `idle`): Laelaps is the dog;
  the Test Wizard, who has no `moving`, walks on his idle at a quicker beat.

**The camera leads and the wizard catches up, on a leash.** The camera eases toward the selected
stop's focus point quickly (it arrives first), but it is clamped every frame so that the wizard stays
inside the view with a margin. On a long jump the camera runs ahead until he reaches the edge of the
frame and then drags him along; it can never leave him. At rest the focus sits in the centre of the
map's own rectangle, not of the screen, so the card never covers the place it describes.

**Pixels.** The map, the wizard, the fog and the markers are drawn by one control (`WorldMapView`) at a
whole number of SCREEN pixels per art pixel - x2 on a 1:1 canvas, the nearest whole number to it on
any other window (`.ai/art-direction.md` 6a, `PixelArtRect`'s rule) - and every position is snapped to
a screen pixel. One pixel size for the world and the wizard on it. No `SubViewport`, no `Camera2D`.

## 4. The Sketchbook: off the map

Eric: *"a special final separate level that appears outside the context of this map, but you should be
able to scroll to it."*

**The last stop is beyond the world's east edge.** The picture of the world ends; past it is the dark
(`occ`), and across a short gap lies **a loose sheet of squared paper** - a workbook page, generated
(paper, faint blue ruling, a red margin, a strip of tape; `world_map.py`, never derived from anything
in `assets/kidsart/`). Its card is the Sketchbook's: *"A world drawn in a workbook, loose."*

- **The road goes there as a pencil line.** From the cliff path past the Emberdeep a ruled graphite
  line runs off the edge of the world, over the gap and onto the page - as if somebody drew a road off
  the paper into the world. The wizard walks it; nothing about the route model changes.
- **The kids' drawings are on the page as themselves.** The page is mine; what is drawn on it is theirs,
  shown at runtime from their own imported files (`assets/kidsart/doodle-*.png`, the Wizard Dude),
  untinted and at a whole scale - never redrawn, recoloured, cropped or derived, and never baked into a
  generated PNG (the generator does not open their files at all).
- It is open from the first run (its gate is `Open`) and is in no order: in the compass it sits apart
  from the campaign's marks, past a break.

## 5. Boss Rush

**A stop at the Emberdeep's summit, the seat**, between the Emberdeep and the edge of the world -
*"Every warden of the road, one after another, in the Emberdeep. The last is his."* It is a stop only
once `BossRushCatalog.IsOpen` (the Warden of the Deep has fallen in his own chapter), as its card was
before: until then the road simply passes the dark summit on its way to the cliff. Its card shows the
record (*"Cleared in 12:40"*, *"Best: 6 of 10 felled"*) in place of rewards, and Start hands the run
`Global.BossRush = true` with the arena's index, exactly as `StageSelection.OnBossRushPressed` did.

## 6. The stops, and their order

`[the campaign in StageCatalog.Campaign order] + [Boss Rush, if open] + [chapters outside the
campaign: the Sketchbook]`. Left and right step through that list; the route visits it in the same
order. A stop is a stage id or `boss_rush`.

`RegressionChecks.ValidateWorldMap` holds the layout file to it: every chapter in `StageCatalog` has a
stop, Boss Rush has one, the stand points' arc lengths rise in stop order, the Sketchbook's stand
point is off the world picture, and every focus point is inside the picture (or on the page). **A new
chapter must be placed on the map** (a line in `world_map.py`'s `PLACES` and a landmark drawn for it),
or the validator says so.

## 7. The compass strip

Eric: *"a compass style scroll bar at the top of the screen so you can see what levels you can scroll
to."*

A heading bar across the top of the screen, as a game compass is: a dark stone band with a gold rule
at its foot (the level-up heading band's materials), a fixed gold caret at its centre, and a mark for
**every stop** - each chapter, Boss Rush when it is a stop, and the Sketchbook past a break.

- **It slides with the wizard**, not with the press: the strip's centre is his fractional position
  between stops, so it glides as he walks and stops when he does. The selected stop's mark is lit and
  larger; the caret shows where he is on the way to it.
- **Mark states**: *cleared* (the chapter's warden has fallen: gold, with the done glyph), *open*
  (gold), *locked* (stone, with the lock glyph), *Boss Rush* (the seat's glyph), *Sketchbook* (a page).
  Under each chapter mark, its campaign number. Marks thin out toward the ends of the band.
- **Tapping a mark jumps to it** (my call). On a phone the compass is the quickest way to the
  Sketchbook from the first chapter, and because the wizard's speed grows with the distance (section 3)
  a jump is still a run along the road, never a teleport. It takes no focus: a pad steps with left and
  right, which the strip shows, and a stop on the strip for the stick would be a second selection
  fighting the first.
- **Fit**: anchored to the top edge, so `SafeArea.Apply` brings it down under a phone's camera hole and
  rounded corners; numerals at the Label role (18 wide, 20 narrow), ink measured against the band at
  4.5:1 or better (`.ai/level-up-screen.md` 4a). Back sits at the left end of the same row.

## 8. The card

Shown for the highlighted stop, rebuilt when the selection changes:

1. **Where it is in the road**: *"Chapter 3 of 10"* (campaign position, not stage index), *"Beyond the
   map"* for the Sketchbook, *"The Seat"* for Boss Rush.
2. **The name** (Title role, Cinzel) and **one short line** (Body): the chapter's `CorruptionText` - what
   he has done to it - or the `FlavorText` where it has none (the Sketchbook). Same rule as the old
   list: two lines, not a wall.
3. **The state line**, glyph-led: the lock reason (lock glyph) if locked; otherwise the warden -
   *"Elderbark has fallen"* (done) or *"Elderbark holds it"* - and the best time survived there.
4. **What it holds** (section 9): a row of slots, each the real icon or sprite of something tied to
   this chapter - a silhouette until it is found, filled once it is. Under the row, *"2 of 5 found"*.
5. **Start**: *"Enter the Fallow Fields"*, or the lock reason, never disabled (section 2).

**Silhouettes** are `CharacterVisuals.BuildSilhouetteFrames` - the character select's locked-wizard
shadow (occlusion fill, a stone rim lit from the key) - applied to every kind of icon, so a hidden
spell, boon, wizard or secret reads as one family. **A secret wizard whose chapter is not open** shows
the sealed portrait instead (the character select's rule: a silhouette naming a prisoner the player
cannot know of is a spoiler). **Secrets are never named until found** (`.ai/stage-secrets.md` 7.6: a
list of named blanks is a checklist): an unfound secret is the generic secret glint's silhouette, an
unfound keepsake the keepsake's. Every slot draws at a whole number of screen pixels (`PixelArtRect`).

**Where it sits.** PC landscape: a column down the right of the screen beside the map, under the
compass. Phone portrait: a panel along the bottom under the map. Both from `ResponsiveLayout.IsNarrow`.
The map's rectangle is what is left, and the camera frames the place inside it.

## 9. What a chapter holds: `StageRewards`

**The one authority** for "what a chapter holds", read by the card and by its validator. It invents
nothing: it derives every entry from the catalogs that already own them.

| Kind | From | Found when |
|---|---|---|
| the warden's reward (spell, boon, or Arcane Energy) | the deed whose `BossId` is the chapter's boss (`BossCatalog.ForStageIndex`), its `SpellUnlockId`/`BoonUnlockId`/`CurrencyReward` | the spell/boon is owned; for Energy, the deed is done |
| what its side event carries out (spell, boon, wizard) | `SideEventDirector.ForStage(id).SiteId` -> `UnlockCatalog.ForDiscoverySite` | owned |
| a wizard whose reveal is this chapter | `UnlockDefinition.RevealStageId` (Tituba, Vainamoinen, Laelaps's coursing) | owned |
| its secrets and its keepsake | `SecretCatalog.ForStage` | `SaveData.HasFoundSecret` |

Sketchbook's deed (*Best Drawing*, 400 Arcane Energy) gains its `BossId` (`kid_wizard`) so it joins by
the same rule; it was the one chapter clear without one.

**`RegressionChecks.ValidateStageRewards`** - so a new unlock tied to a chapter cannot be missed:

- every `Discovery` entry in `UnlockCatalog` lands on exactly one chapter (its site is some chapter's
  side event), every `RevealStageId` names a real chapter, every deed with a `BossId` resolves to a
  chapter, and every secret's stage exists - and each appears in that chapter's list;
- **the prose net**: any unlock whose hint, or whose deed's description, names a chapter (*"in the
  Broken Circle"*) must be in that chapter's list. That is the check that would have caught the
  Coursing if it had been written without `RevealStageId`: a deed tied to a place in words but not in
  data;
- every chapter has at least one thing on its card, and nothing is listed twice.

## 10. Controls

| | Previous / next stop | Choose | Back | Jump |
|---|---|---|---|---|
| Keyboard | Left / Right (and A / D), OS repeat | Enter, Space | Escape | - |
| Pad | D-pad or left stick (`ui_left`/`ui_right`), held repeats after 0.4 s every 0.18 s | A | B | - |
| Mouse | the arrow plates at the map's sides | the Start button | the Back button | click a place, or a compass mark |
| Touch | the arrow plates | Start | Back, or Android back | tap a place, or a compass mark |

`ui_left`/`ui_right` are heard in `_Input` and consumed there, before Godot's own focus navigation, so
they move the map and never the selection. The screen's only focus stops are Start and Back (up and
down move between them); every focus stop's left and right neighbours are itself, so
`MenuNavigator`'s held-stick repeat cannot walk the selection sideways either. `MenuNavigator.Attach(this,
startButton)` and `SafeArea.Apply(this)` run once the screen is built. A direction held as the screen
opens is ignored until let go (the navigator's rule, honoured by the map's own repeat). Taps are
mouse-button presses (the emulated mouse covers a finger), never both.

## 11. Returning

The map opens with the wizard **standing** (no walk) at the last stop played: `WorldMap.LastStopId`,
set when a run is started from it, survives the run and the results screen for the session; on a cold
start it falls back to the save's most recent run (`RunTelemetryHistory[0].StageId`), then to the
furthest open chapter of the campaign. Back returns to CharacterSelection.

## 12. Files

| What | Where |
|---|---|
| the generator | `tools/art/world_map.py` - the world, the page, the fog, the markers, the compass glyphs, the secret and keepsake icons, and `worldmap.json` |
| the art | `assets/bonelight/ui/worldmap/*.png` + `.import` |
| the screen | `scenes/WorldMap.tscn`, `scripts/WorldMap.cs` (layout, input, card), `scripts/WorldMapView.cs` (the drawn world, wizard and camera), `scripts/WorldMapCompass.cs`, `scripts/WorldMapLayout.cs` (reads the JSON) |
| the authority | `scripts/StageRewards.cs`; `RegressionChecks.ValidateStageRewards`, `ValidateWorldMap` |
| the flow | `CharacterSelection` opens `WorldMap.tscn`; `StageSelection` stays in the tree, unused, until this has been played |
| the harness | `_UiShot --scene=res://scenes/WorldMap.tscn`, plus `--map-at=<stop>` (where he starts), `--all-unlocked` (everything owned, found and beaten, in memory), and the existing `--pad=right,...` for a press and `--frames` to catch him mid-walk or at rest; `--map-start-probe` presses Start and checks the stage the run was handed, or that a locked stop handed none |

## 13. Sound

Nothing new. A step to a new stop plays `ui_hover` (a frequent sound, already at the frequent level of
`.ai/audio-direction.md`'s loudness table), Start plays `ui_click`, a locked Start `ui_denied`. No
footsteps: the walk lasts a second and would be the loudest-by-count sound on the screen.

## 14. What I took from Vampire Crawlers

poncle's mobile crawler (2026) moves from a village to a world map of regions, each a dungeon, with
branching routes, stage difficulty ratings, and some regions paying a new character instead of a relic,
the relics hidden in glowing objects on the floors. What this map takes from it:

- **The map is a place you travel, and what a region holds is stated before you go.** Their regions
  name the relic or the crawler you get there; this card shows it, as a silhouette until found, which
  is the collection-book pull that makes a player go back to a region.
- **A region that pays a person reads differently from one that pays an item** - here, the rescued
  wizards' slots are their own sprites and the spells are icons.
- **Locked regions stay on the map with their requirement**, rather than being hidden.

What it does not take: the branching (this campaign is one road, `.ai/campaign.md`), the numbered
difficulty (the chapter's place on the road says it), and the hub village (the title screen is the
quiet before the road). I could not reach a source that shows how their map animates, so the walking
and the camera are this doc's own, from Eric's description.

## 15. Decisions left to Eric

1. **Is the cursor right to visit locked places?** (Section 2.) The alternative is the classic
   overworld - the road stops at the first closed place - which reads as progress but hides the
   Emberdeep's goal again.
2. **Should a locked chapter's card show what it holds?** It does (as silhouettes, a secret wizard
   sealed): it is the reason to open it. Hiding it is one line.
3. **The Sketchbook as a page off the edge of the world**, reached by a pencil line. If it should feel
   more apart, the line can go and the camera can cut across the dark instead - but then the wizard
   has to cross it somehow, and walking is the rule of the screen.
