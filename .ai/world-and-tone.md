# World and tone

`.ai/project-overview.md` describes a system — auto-firing spells, XP, waves, meta progression —
and says nothing about what any of it *is*. That gap has been filled ad hoc, one asset at a time:
the title screen decided the wizard keeps a vigil, `sprite_basic_enemies.py` decided the dead have
cold arcane eyes, `StageCatalog` decided there are eight named places. None of those decisions knew
about each other.

This doc settles the fiction, and then spends the rest of its length on what the fiction *decides*.
It is not lore for its own sake. A premise earns its place here only if it answers a question an
artist or a designer would otherwise answer by taste, differently each time.

`.ai/art-direction.md` remains the contract — grid, light model, palette rows, silhouette rules.
This doc says what to point the contract at.

## The premise

The age of magic did not end in a war. It ended the way a fire ends: it was tended, and then it
was not.

What magic held down is coming back up. Not an invading army — the ground itself, giving back
everything that was ever put into it. The dead that walk are the ordinary dead: people who were
buried in linen and people who were buried in their armour, in the same earth, in numbers no one
counted. They are not a faction. They have no banner, no colour, no cause. They are the world with
the light taken out of it.

A handful of wizards still keep **vigils**. A vigil is a warded circle drawn at a thin place, held
by one person, for as long as that person can hold it. There is no relief coming and no line to
fall back to. You do not win a vigil; you hold it, and the length of the holding is the score.

That is the whole story. Everything below is what it costs.

## What the premise decides

**Why the player is alone, always.** No allies, no escort, no base. One figure, one circle. The
title screen is not a poster of a hero — it is the last quiet moment of a shift that is about to
start.

**Why the dead have no colour.** They are not an enemy faction, so they do not get a faction
palette. This is the single largest visual decision in the game and it is worked out in full
below.

**Why the light is the character.** The wizard carries the only warm light in the frame. Every
other source is something the wizard made — a ward, a flame, a spell. When the light goes out the
run ends, and that is literally true rather than a metaphor.

**Why unlockable wizards are named people.** The four you begin with — Pyromancer, Frostweaver,
Stormcaller, Geomancer — are *disciplines*, roles the player steps into. Everyone unlocked after
is a historic or legendary magic-worker, because each one is another vigil-keeper still holding
somewhere, alone, and reaching them is the campaign. An archetype has no history to interrupt; a
named person does.

**Why there are eight places and they are all ruined.** Enchanted Forest, Cursed Dungeon, Sunken
Cave, Blighted Swamp, Mystic Ruins, Frozen Waste, Scorched Sands, The Emberdeep. Every one is a
thin place — somewhere the boundary wore through first. They are not levels in a journey. They
are eight simultaneous failures.

## Tone rules

1. **Serious, never cute.** The standing note on the player character generalises to everything:
   no mascots, no winking, no comic relief enemy. A game about holding a line alone at night does
   not have a joke enemy in it.
2. **No copy carries the mood.** No taglines, no flavour text doing work the image should do. The
   title screen shipped without its subtitle for this reason.
3. **Quiet, not grim.** Bonelight is dark but chromatic — deeply blue shadows, not grey ones. The
   world is *unlit*, not *decayed*. Gore, rot and shock are off-direction; cold and absence are
   on-direction.
4. **The horde is impersonal.** Individual enemies are not villains and do not need personality.
   They need to be *legible in a crowd of thirty*, which is a different and harder job.

## The enemy palette

This is the rule the premise exists to justify, and the sheets in the game are already most of
the way to it without having been told.

Measured across the five Bonelight sheets, by share of opaque pixels:

| Sheet | Body row | Occlusion | Accent |
|---|---|---|---|
| `swarmer` | `linen` 59% | 31% | `arcane` 1% (eyes) |
| `runner` | `linen` 52% | 26% | `red` 17%, `arcane` 1% |
| `bruiser` | `steel` 50% | 35% | `red` 13%, `arcane` 1% |
| `shielder` | `steel` 56% | 35% | `gold` 4%, `arcane` 1% |
| `skullsentry` | `skin` 45% | 44% | `E:fire` 6% |

**The rule: the dead wear the world's own materials, and their only chromatic light is a cold
arcane eye.**

- **Two body families, and they are a fiction, not a style choice.** `linen` is the buried; `steel`
  is the buried who were soldiers. Every raised enemy belongs to one or the other. That is why the
  four sheets above already agree — the split was drawn before it was named.
- **Body rows come from the deep half of their ramp.** These are unlit things. A raised enemy that
  reaches the bright end of any ramp is competing with the player's light for the eye's attention,
  and the player's light must always win.
- **`arcane` eyes, one to two pixels, and that is the entire emissive budget** for anything raised.
- **`red` and `gold` are pigment, not light** — old cloth, old braid, tarnished fittings. Kept
  under about 15% and never on the silhouette edge.
- **Class reads from silhouette, never from hue.** Already in `art-direction.md` as a Don't; the
  premise is *why*. Tinting one enemy green to distinguish it from another asserts they belong to
  different factions, and there are no factions.

**The one carve-out: fire eyes mean made, not raised.** `skullsentry` owns `E:fire` and should
keep it. A thing that was *built* — a construct, a sentry, a summon — is lit by whoever built it,
so it gets the fire ramp. A thing that was *raised* gets cold arcane. That single distinction is
worth more than any amount of per-enemy colour, and it is currently the only exception permitted.

**Elites and bosses get more, not different.** More steel, more occlusion, a larger cell — never a
body tint. Their one element-ramp accent belongs to the attack they telegraph, so the colour on
them is a warning rather than a costume.

### What is currently off-direction

- **Four enemies ship as `modulate` tints on borrowed sheets**: Lunger (hot orange, ×2.3),
  Slammer (grey, ×3.4), Exploder (acid green, ×3.0), Summoner (mint green, ×4.4). Every one of
  those hues asserts a faction that does not exist, and every one of those scales is non-integer.
  They are the highest-value redraw left.
- **Four sheets predate the contract entirely** — Orc, Soldier, Boo, Cultist — and come from three
  different asset packs.

## The player at 48×48

**The player is drawn on a 48×48 cell.** This is a deliberate exception to the actor row in
`art-direction.md` §1, which puts player and basic enemies together at 32×32.

The reasons it is worth an exception:

- It is the only figure on screen 100% of the time, in every run, at the centre of the frame.
- The premise makes the player the light source and the subject; being read as *larger than the
  horde* is the fiction, not a favour.
- 48×48 is already in the contract's table (elite / large enemy), so the exception costs no new
  cell size and no new render scale. Render stays ×2.
- It roughly doubles the drawing area, which is where detail past silhouette becomes possible at
  all — a belt, a clasp, sleeve folds, a face that can be half-read.

What does **not** change: render scale is ×2 like everything else, feet anchor on the contract's
row, the palette is the same 66 colours, and the figure is still built silhouette-first.

**Camera note.** Sprites are placed at `scale = 2`, so camera zoom must be a multiple of **0.5**
or art pixels stop landing on whole device pixels. From the current `zoom = 1.0` the next clean
step is 1.5; there is nothing usable between them.

## Proportion — a correction

The four 32×32 wizard studies in `tools/art/wizards.py` were built on the principle that *the
signature shape must be wider than the body*. That is a good rule for a swarmer that has to be
picked out of a crowd, and it is the **wrong** rule for the player.

The standing note from the title-screen review is explicit about what made an earlier wizard read
as cartoony: roughly 2:1 tall-to-wide, a hat brim wider than the body, and bilateral symmetry —
"a chess piece or a mascot". Conclave's brim is 24px against a 19px hem. It repeats the mistake.

At 48×48 the player wants:

- **About 2.6:1 tall-to-wide**, with the brim *inside* the shoulder width.
- **An off-axis cloak sweep.** Mirrored silhouettes read as pieces, not people.
- **The figure mostly in the shade and deep tones**, with the rim light and the staff orb doing the
  separating. Filling the robe with the bright end of a ramp is what made it look like a toy.
- **The staff taller than the wizard**, setting the top of the silhouette, with a real head on it.

The four studies are still useful — the dark face with two points of light, the beard as second
silhouette, the non-flat hem all survive. The proportion rule does not.

## What this does not change

No gameplay, no balance, no code. Nothing here overrides `art-direction.md`; where the two touch,
the contract wins and this doc explains the intent behind it. The one substantive change to the
contract is the 48×48 player cell, recorded above and cross-referenced from §1.
