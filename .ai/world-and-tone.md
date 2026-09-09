# World and tone

`.ai/project-overview.md` describes a system — auto-firing spells, XP, waves, meta progression —
and says nothing about what any of it *is*. That gap has been filled ad hoc, one asset at a time:
the title screen decided the wizard stands in a warded circle, `sprite_basic_enemies.py` decided
the enemy eyes are cold arcane light, `StageCatalog` decided there are eight named places.

This doc settles the fiction and then spends the rest of its length on what the fiction *decides*.
It is not lore for its own sake. A premise earns its place here only if it answers a question an
artist or a designer would otherwise answer by taste, differently each time.

`.ai/art-direction.md` remains the contract — grid, light model, palette rows, silhouette rules.
This says what to point the contract at.

## The premise

A dark wizard has imprisoned every wizard who could have stood against him, and enslaved the land
with his shadow creatures.

You are the one he missed.

Each place you go is somewhere one of the others is still held. You fight through what he has put
there, and what you carry out is a person and the magic they knew. Only when every wizard is free
and every spell is recovered does he become reachable at all.

That is the whole story, and the game already runs on it.

## The fiction is the progression system

This is not a theme applied over mechanics. The mechanics were built to this shape before anyone
wrote it down, which is why the vocabulary in the code already fits:

| In the fiction | In the build |
|---|---|
| Wizards held prisoner, one per place | `UnlockCatalog` — every character has exactly one source |
| Magic taken and hidden in the world | `UnlockKind.Spell`, and the reserved `Discovery` source |
| Eight places he holds | `StageCatalog`, chapters gated `PreviousBoss` |
| His lieutenant at each place | the per-chapter boss that opens the next |
| He is unreachable until the land is free | `IsCampaignComplete` gates the final chapter — *every spell recovered and every wizard freed* |
| You start with almost nothing | four starter disciplines, eight starter unlocks |

`.ai/gameplay-design.md` already words the gate as **"every spell recovered and every wizard
freed."** That sentence is the story. Nothing here needs adding to the code.

Two consequences worth stating plainly:

- **The final chapter's gate is total, and that is a design commitment, not a flourish.** Every
  spell, including the nine bought with Arcane Energy. If that ever proves too hard a wall, the
  fiction survives being narrowed to *every wizard freed* — losing a spell is losing a weapon,
  losing a wizard is leaving a person behind. Narrow it there first, not everywhere.
- **`Discovery` has no entries yet.** It is the one unlock source that literally means "found in
  the place where it was hidden," so it is the most on-story source in the catalog and currently
  the emptiest. Filling it is a fiction win as much as a content one.

## What the premise decides

**Why you are alone.** No allies, no escort, no base — everyone who could help is in a cell. The
title screen is not a hero shot; it is the last quiet moment before a shift that has no relief
coming.

**Why the horde is one colour.** Shadow creatures serve one will and were made by one hand. They
are a faction of one master, and his colour is absence. Worked out in full below.

**Why the player is the light.** You carry the only warm light on screen. Everything else glowing
is something you made — a ward, a flame, a spell. Magic did not leave the world; it was taken and
locked up, and every point of warm light is a piece of it he does not have.

**Why unlockable wizards are named people.** The four you begin with — Pyromancer, Frostweaver,
Stormcaller, Geomancer — are *disciplines*, roles the player steps into. Everyone unlocked after is
a historic or legendary magic-worker, because each one is a prisoner with a name and a life before
this. An archetype has nothing to be taken from; a person does.

**Why every place is ruined.** Enchanted Forest, Cursed Dungeon, Sunken Cave, Blighted Swamp,
Mystic Ruins, Frozen Waste, Scorched Sands, The Emberdeep. None is a wilderness. Each is somewhere
that used to belong to someone, held now by the thing he left in it.

## Tone rules

1. **Serious, never cute.** The standing note on the player generalises: no mascots, no winking, no
   comic-relief enemy. A game about walking into an occupied place alone does not have a joke
   monster in it.
2. **No copy carries the mood.** No taglines, no flavour text doing work the image should do. The
   title screen shipped without its subtitle for exactly this reason.
3. **Occupied, not apocalyptic.** The world is not over — it is *held*. Cold, quiet, and someone
   else's. Gore and rot are off-direction; absence and cold are on-direction. Bonelight's shadows
   are deeply blue, never grey: unlit, not decayed.
4. **The horde is impersonal.** Individual creatures are not villains and need no personality. They
   need to be *legible in a crowd of thirty*, which is a different and harder job.
5. **He is never on screen until he is.** No taunts, no visions, no mid-run appearances. The one
   villain in the game is withheld until the gate opens.

## The enemy palette

This is the rule the premise exists to justify, and the sheets in the game are most of the way to
it already, without having been told.

Measured across the five Bonelight sheets, by share of opaque pixels:

| Sheet | Body row | Occlusion | Accent |
|---|---|---|---|
| `swarmer` | `linen` 59% | 31% | `arcane` 1% (eyes) |
| `runner` | `linen` 52% | 26% | `red` 17%, `arcane` 1% |
| `bruiser` | `steel` 50% | 35% | `red` 13%, `arcane` 1% |
| `shielder` | `steel` 56% | 35% | `gold` 4%, `arcane` 1% |
| `skullsentry` | `skin` 45% | 44% | `E:fire` 6% |

**The rule: shadow wears what it took, and every creature carries the same eye-light.**

- **Two body families, and they are a fiction rather than a style choice.** `linen` is the people
  of the land; `steel` is its soldiers. Both were taken. Shadow does not have a shape of its own,
  so it wears theirs — which is why the four sheets already agree, and why the split works without
  either family reading as a separate faction.
- **Body rows come from the deep half of their ramp.** These things are unlit. A creature that
  reaches the bright end of any ramp is competing with the player's light for the eye, and the
  player's light must always win.
- **The eye is his mark, and it is identical on every creature.** One to two pixels of `arcane`,
  and that is the entire emissive budget for anything he raised. Sameness is the point: they all
  answer to one will, so they all show one light.
- **`red` and `gold` are pigment, not light** — old cloth, old braid, tarnished fittings from the
  lives these were taken from. Under about 15%, never on the silhouette edge.
- **Class reads from silhouette, never from hue.** Already a Don't in `art-direction.md`; the
  premise is *why*. Tinting one creature green to tell it from another asserts they come from
  different places, and they do not — they come from him.

**The one carve-out: fire eyes mean built, not taken.** `skullsentry` owns `E:fire` and should keep
it. A construct is lit by whoever made it, so it burns with his making rather than staring with his
mark. Taken things get cold arcane; made things get fire. That single distinction is worth more
than any amount of per-enemy colour and is the only exception permitted.

**Elites and bosses get more, not different.** More steel, more occlusion, a larger cell — never a
body tint. Their one element-ramp accent belongs to the attack they telegraph, so colour on them is
a warning rather than a costume.

**The dark wizard is the exception that proves it.** He is the only figure allowed to be a source
of dark light, and he should be the only thing in the game using the `darkness` ramp as emission.
Holding that back across eight chapters is what makes it land. Note the good tension already
available: `darkness` is a *player* element too, so a player can end up throwing his own element
back at him.

### What is currently off-direction

- **Four enemies ship as `modulate` tints on borrowed sheets**: Lunger (hot orange, ×2.3), Slammer
  (grey, ×3.4), Exploder (acid green, ×3.0), Summoner (mint green, ×4.4). Every one of those hues
  claims an origin that does not exist, on top of a non-integer scale. Highest-value redraw left.
- **Four sheets predate the contract entirely** — Orc, Soldier, Boo, Cultist — from three different
  asset packs.
- **"Cultist" is now a naming problem as well as an art one.** Nothing in this premise is a cult:
  there are no willing followers, only the taken. Either it becomes a taken caster, or the idea of
  people who *chose* him needs adding to the fiction deliberately rather than by leftover.

## The player at 48×48

**The player is drawn on a 48×48 cell.** A deliberate exception to the actor row in
`art-direction.md` §1, which put player and basic enemies together at 32×32.

Why it is worth an exception:

- It is the only figure on screen 100% of the time, in every run, at the centre of the frame.
- The premise makes the player the light source and the subject. Reading as larger than the horde
  is the fiction, not a favour.
- 48×48 is already in the contract's table (elite / large enemy), so it costs no new cell size and
  no new render scale. Render stays ×2.
- It roughly doubles the drawing area, which is where detail past silhouette becomes possible at
  all — a belt, a clasp, sleeve folds, a face that can be half-read.

Unchanged: render scale ×2, feet on the contract's anchor row, the same 66 colours, silhouette
built first.

**Camera note.** Sprites are placed at `scale = 2`, so camera zoom must be a multiple of **0.5** or
art pixels stop landing on whole device pixels. From the current `zoom = 1.0` the next clean step is
1.5; there is nothing usable between them.

## Proportion — a correction

The four 32×32 wizard studies in `tools/art/wizards.py` were built on the principle that *the
signature shape must be wider than the body*. That is right for a swarmer that has to be picked out
of a crowd, and **wrong for the player**.

The standing note from the title-screen review is explicit about what read as cartoony: roughly 2:1
tall-to-wide, a hat brim wider than the body, and bilateral symmetry — "a chess piece or a mascot."
Conclave's brim is 24px against a 19px hem. It repeats the mistake.

At 48×48 the player wants:

- **About 2.6:1 tall-to-wide**, with the brim *inside* the shoulder width.
- **An off-axis cloak sweep.** Mirrored silhouettes read as pieces, not people.
- **The figure mostly in shade and deep tones**, with the rim light and the staff orb doing the
  separating. Filling the robe with the bright end of a ramp is what made it look like a toy.
- **The staff taller than the wizard**, setting the top of the silhouette, with a real head on it.

What survives from the four studies: the dark face with two points of light, the beard as second
silhouette, the hem that is not a flat bar. The proportion rule does not.

## What this does not change

No gameplay, no balance, no code. Nothing here overrides `art-direction.md`; where the two touch,
the contract wins and this doc explains the intent behind it. The one substantive change to the
contract is the 48×48 player cell, recorded in §1 and cross-referenced here.
