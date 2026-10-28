# ADR: Spell forms - a spell taken in one of several forms, chosen once

**Date:** 2026-10-28 · **Status:** accepted, built (Spiritual Weapon, then the Familiar: `.ai/spell-roster.md` 2f and 2g)

## The request

Eric: *"spiritual weapon should be able to select one of 3 weapons when you initially pick it, they
each have different advantages. axe is heavier but more damage, hammer is aoe that shakes ground and
slows enemies, and sword is the most direct damage to single targets, faster swings. they should each
have their own icons and special mutations/ascension that differ from each other at least slightly."*
The Familiar's crow, cat and dog are the same request again, so this is a mechanism, not a Spiritual
Weapon special case.

## Decision

A spell may carry **forms** (`SpellVariant`, `scripts/SpellVariant.cs`). The first time a spell with
forms is taken from a level-up, the player picks one on a page of its own, and the run holds that form
for good. A form is chosen once a run; it never changes and it is never offered again.

**A form is data on its spell.** Each one is a sub-resource of the spell's own `.tres`, listed in
`SpellData.Variants`, beside `SpellData.VariantPrompt` (the page's heading, "Choose your weapon"). It
carries:

| field | what it is |
|---|---|
| `Id` | unique across the game, starting `<spell id>_`, never an evolution id; the prefix of every milestone the form has |
| `DisplayName`, `ShortName` | "Spirit Axe" is the spell's name once chosen; "Axe" is the word on the spell's card and in logs |
| `Description`, `Traits` | the spell's words once chosen; what the form is good at, as glyph-led lines on its card |
| `Icon` | the form's own card icon - the spell's icon once chosen |
| `CountNoun` | what a count of it is ("Axes +1") - `Player.ProjectileNoun`, `StatIcons.KeyFor` |
| `BaseDamage`, `BaseCooldown`, `BaseProjectileCount`, `BaseRange`, `LevelUpgrades` | the form's numbers, written out in full; they REPLACE the spell's, they do not adjust them |
| `TargetingMode`, `DamageShape`, `ScalingTagsMask` | its classification (Auto / 0 keeps the spell's) |

**Choosing writes the form into the run's copy of the spell** (`SpellVariants.Apply`, called by
`Player.TryAddOrLevelSpell` on the deep copy it already makes): name, icon, words, numbers, level table
(then `Player.PrepareSpellLevelUps`, as every spell's table gets) and milestones, and
`SpellData.SelectedVariantId`. Everything that already shows or reads a held spell - the loadout strip,
the pause screen's run list, the results screen, the ascension browser, the Ascension strip, every
`SpellData` stat reader, `EvolutionIcons.Current` - shows and uses the form without knowing forms exist.
That is the reason for writing it in rather than looking it up: no screen had to change.

**A form never changes the spell's element tags.** The element reachability work balances twelve
elements across spells, boons and relics, and a form is a way of playing a spell, not a different
carrier. `RegressionChecks.ValidateSpellVariants` holds it; `ValidateElementReachability` counts a spell
with forms once, at the best of its forms.

## Each form's evolutions are its own

`SpellEvolutionCatalog.EnsureEvolutionCoverage` builds a spell's level 4 and level 8 options from
`SelectedVariantId` (Spiritual Weapon: `SetupSpiritualWeaponEvolutions` switches on the form). Every
milestone id starts with its form's id (`spiritual_weapon_axe_broadaxe`), which buys, for nothing:

- **its own icon**: `EvolutionIcons` looks an icon up by id; `tools/art/evolution_icons.py` draws one
  per id, keyed by the form (`subject_of`: the catalog's `variant` field, written by `_AscensionAudit`);
- **its own sealed/seen record**: `SeenAscensionIds` is `spell:branch`, and the branch is per form;
- **its own reader row**: `EvolutionStatReaders.TryGet(SpellData)` takes the form's row (keyed by the
  form id) before the spell's, so what a card promises is checked against what that form's code reads.

A template with no form chosen (the catalog's own, which no run plays) carries its default form's block,
so `Player.CardLevelCap` and anything else that reads a template sees a real set.

**An Apotheosis can belong to a form** (`ApotheosisDefinition.VariantId`; `ApotheosisCatalog.ForSpell`,
`SpellMeetsPath`). A wizard who opens on a spell with forms may have one per form, each with its own
path and verb; `ValidateApotheosisCatalog` checks the path against that form's own milestones and
refuses a definition on a spell with forms that names none. No wizard opens on Spiritual Weapon today.

## The level-up

- A spell with forms is dealt as **one card** - its own name, icon and words, and "Choose its form: Axe,
  Hammer, Sword" for a stat line. It carries a card per form (`LevelUpOption.Variants`, built by
  `Player.BuildNewSpellOption` from copies of the spell in each form, made once a run - `Player.FormsOf`).
- **Taking the card does not choose.** It turns the hand into the forms (`LevelUpMenu.Variants.cs`):
  the new spell's torn page for each, laid out by the same builders as any hand, with the form's icon,
  its numbers, its traits and its tag chips. The heading asks the spell's question, one line under it
  says the choice holds, and Reroll, Skip, the charges, the Ascension strip and the Ascensions button
  give way to one **Back**, which returns to the hand as it was dealt. Rerolling, banning or skipping
  the spell is done from the hand, not the page.
- Taking a form sends **`spell:form`** (`SpellVariants.Selection`) - the shape an evolution already
  used. What the second half names is decided by whether the spell is held: for a spell not yet held
  it is the form, for a held one a milestone. `TryAddOrLevelSpell` refuses a form named for a held
  spell. A selection with no form (a starting spell, a tool) gets the first form in the `.tres`.
- The rules every page that swaps the cards keeps: the **arming pause** starts again
  (`RearmForNewChoices`, `InputGuardSeconds`), so a double click cannot take a form unread; the
  **navigator** is re-attached with the first form selected (`BuildButtonsFrom`); the panel keeps its
  `SafeArea` insets; a phone's cards are sized for the forms on the page, not the hand behind it
  (`shownCount`).
- Telemetry records a new spell by its id alone (the variety bonus compares spells, not forms).

## The tools never stop on the page

`LevelUpOption.AutoChoice(preferred)` is what every tool that takes cards sends: the card's own
selection, or for a card that opens a page of forms, the preferred form if it is one of them, else the
first. `_BalanceHarness` (every policy, through `Act`/`WithForm`; `--variant=ID` picks the form for
`--spell` and for any card of that spell), `_BossProbe`, `_ExploreProbe`, `_MazeProbe` and `_UiShot`'s
level-up skipper all use it. `--spell=spiritual_weapon --variant=spiritual_weapon_hammer` starts the
Test Wizard on the hammer; the Test Wizard's picker on the character screen lists each form.
`_UiShot --show-variant=SPELL` photographs the page (the spell's card pressed through the menu's own
path); `--variant-hand` stops at the card in a hand.

## The validators

Everything that walks evolutions walks every form (`ContentValidator.LoadAllSpellForms`):
`ValidateEvolutionReaders`, `ValidateLevelUpReaders`, `ValidateElementTags`, `ValidateElementPaths`,
`ValidateEvolutionIcons`, `ValidateElementReachability`, `ValidateApotheosisCatalog`, and the
`_AscensionAudit` that feeds the icon generator. `ValidateSpellVariants` adds what only forms can get
wrong: ids, names, a prompt, an icon of each form's own, tags unchanged, a table in order, three level 4
and two level 8 milestones each with the form's prefix and shared with no other form, a reader row of
its own - and no player-facing word on a form or its milestones that gives a size in pixels (a size is a
percentage; Eric: "never mention px, add a %").

## Rejected

- **A form as three spells.** Three catalog entries, three unlocks, three cards competing for a hand,
  a book that could hold two of them, and the element and unlock validators counting three carriers.
  The request is one spell the player shapes.
- **A form as a level 1 mutation.** A milestone adds to the spell's numbers (`CollectActive`); a form
  replaces them, and it decides the milestones that come after it. Bending milestones to branch on an
  earlier milestone would have touched every stat reader for one spell's sake.
- **Choosing in the shop, or on the character screen.** The choice belongs where every other build
  decision is made, on the level-up screen, in the run; and it should be made again each run.

## For the Familiar (crow, cat, dog) - done 2026-10-28, `.ai/spell-roster.md` 2g

1. Add the three `SpellVariant` sub-resources to `SpellData_Familiar.tres` (ids `familiar_crow`,
   `familiar_cat`, `familiar_dog`), `VariantPrompt`, each form's icon (`tools/art/spell_icons.py`), its
   numbers and its whole level table. The first listed is the default for a starting spell and the bots.
2. In `SpellEvolutionCatalog.SetupFamiliarEvolutions`, switch on `spell.SelectedVariantId` (default:
   `spell.DefaultVariant`) and build each form's block with ids starting with the form's id. Keep the
   hybrid/pure element rules (`ValidateElementPaths`): the Familiar is pure Arcane, so each form's level 4
   needs a branch that deepens Arcane and one that adds an element.
3. Give each form a row in `EvolutionStatReaders` keyed by the form id.
4. Read the form in the node (`SpiritualWeapon.FormOf` is the pattern: one switch on
   `SelectedVariantId` per cooldown).
5. `tools/art/evolution_icons.py`: a `SPELLS` row per form id (its subject), a `BASE` entry per form
   (its card icon), and `@card` overrides. Re-run `_AscensionAudit -- --json=...`, the generator, then
   `--import`.
6. Nothing in `LevelUpMenu`, the bots, the results or the pause screen needs touching. (As built, the cat
   needed one thing more: its grants listed on the pause screen's spell row, `Familiar.GrantLines` -
   nothing else shows what a spell gives the wizard rather than the enemy.)
