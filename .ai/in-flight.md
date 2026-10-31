# Work in flight (snapshot, 2026-10-30 evening)

A handoff from the cloud session that ran 2026-10-28 to 10-30. Eric moved the work to a Claude Code
session on his PC; every cloud agent was stopped and its work pushed. **A snapshot, not a source of
truth:** check `git log` and the branches before acting on it, and delete this file once everything
below has landed or been dropped.

## 1. Released

**0.9.2 is released** (`Release v0.9.2`, 614ad5e, tag `v0.9.2`): the world map, the pixel-art pass,
the new evolutions, fullscreen on PC and the pity bonus removed. The next release is 0.9.3 or 0.10.0
(`bash tools/release/release.sh X.Y.Z "..."`, on the Windows PC only: Inno Setup, the Android
release keystore and the Android SDK live there; `.ai/releasing.md`). The CS0162 "unreachable code"
warnings during its build are old and harmless.

## 2. `main` now carries everything (2026-10-31)

All nine spell vaults are built, so `main-ff86ni` was merged into `main` (048fe79) and **`main` is
releasable as 0.10.0**. Since 0.9.2 it adds:

- **Achievements for every spell (A1-A4), schema 19:** every non-starter spell earned through a
  feat, a chapter deed or one of the nine secret vaults; the achievements page; the shop sells no
  spells; the gate on wizards (fixes the Emberdeep being unreachable on a new save).
- **The nine vaults:** the Pale Hart, the Pinfold, the Fallen Star, the Geode Heart, the Cold Forge,
  the Swallow-Hole, the Broken Ward, the Buried Runes and the Fulgurite.
- **Variety:** the Sunken Cave's caverns and the Mystic Ruins' broken walls.
- **Castle groundwork:** the Last Bridge and the Court are built but not playable (`IsPlayable =
  false`); the Throne is a stub; their music is wired.
- **Playtest fixes:** Glacial Spike's extra spikes land on enemies; Avalanche reworded; Spiritual
  Weapon hovers and bobs, swings cleanly at speed, spreads its targets, and has the off-hand
  dagger (Twin Blades) and Deflection (Riposte); the crow's Great Raven; enemy wind-ups shown by an
  animated icon over the attacker, with danger areas as opaque dashed outlines; the Gleaning Bell
  (a rare pickup that calls every XP orb in); Hollow Star never opens on the wizard, pulls slower,
  hits hardest at its core and fires less often; Split Lens charges only the spells it feeds.

`main-ff86ni` stays the development branch: merge each finished branch into it, verify, then
fast-forward `main`.

## 3. Branches still running or unfinished

| branch | what | base |
|---|---|---|
| `wip/difficulty-curve` | the difficulty curve; Ascensions earned; 4 spell slots by default; rerolls and bans per run; Skip gives half an XP bar | `main-ff86ni` |
| `wip/ascension-audit` | Tidal Wall starts small and grows, fires less; every Ascension checked for power and screen share; Ascensions keep the mutation working; Sun Dogs glide back and stay on screen with Daybreak | `main` |
| `wip/fix-clipped-art` | the bushes and Scorched Sands art cut off at their boxes, and a scan of all world art | `main` |
| `wip/hounds-icons-hares` | the hound Ascension (main hound only, a bigger Cerberus; Littermates as two one-headed pups; Pancho pups), Familiar and Molten Shard icons, the hares' run cycle | `main` |
| `wip/pause-toasts-sketchbook` | the pause screen shows mutations and Ascensions, no Spell Index there, live achievements, the achievement banner, no white flash after a level-up, the Sketchbook as its own map | `main-ff86ni` |
| `wip/evolutions2-sky` | phase 2 cards for Stormsweep, Sleet Runes, Aegis Ward, and their pixel-art pass | **WIP d8048b3** from 10-29 |
| `wip/evolutions2-blasts` | phase 2 cards for Arcane Explosion, Fireball, Meteor Swarm | not started (Solar Flare landed) |

Every other `wip/*` branch is fully merged and can be deleted on GitHub.

## 4. What comes after

1. The seven vaults, then `main-ff86ni` into `main` and a release.
2. The castle, by `.ai/endgame-castle.md` section 9.2: the Bridge's and the Court's casts (C6),
   the bosses (C5), the Throne (C4, after the Court's reader), the Dungeon's move into the castle
   (C2, schema 20; it ships in the same release as the Bridge's flip), the world map's new places
   (C8) and the castle's secrets (C9). Each chapter's `IsPlayable` flips last.
3. The rest of the secrets catalogue, `.ai/achievements-and-secrets.md` section 8.

## 5. How the cloud session worked

One agent per piece, each in its own worktree on a `wip/*` branch, briefed with the design
section and the files it alone may touch; the integrator merged each finished piece into
`main-ff86ni` with a merge commit, then built, ran the validator and looked at screenshots before
pushing. Shared docs (`.ai/art-replacement-manifest.md` section 9, `.ai/spell-roster.md` 2h) collect
one section per agent and conflict on every merge: keep both sides. **Running five to eight agents
at once used the account's 5-hour limit within a few hours each time;** one or two at a time goes
further.

## 6. Loose ends

- The pause screen numbers chapters by stage index (the Fallow Fields shows "Chapter 10"); the map
  says "Chapter 2 of 10". `Node2DGame.cs`, around the pause screen's chapter line.
- `ImpactDebris`, the burst about thirty spells share, still fades by alpha in its last 30%, and
  Ice's first two ramp colours vanish on the Sketchbook's paper. One fix there covers every cold
  spell.
- `lingering_pool.gdshader` has no caller since Fireball and Arcane Explosion draw their own pools.
- CLAUDE.md says only Python 2.7 is installed; the cloud container had Python 3.13, and the art and
  audio generators were made to run on both.
- An intermittent "1 resources still in use at exit" line appears on some validator runs and not
  on reruns; never tied to a code path.
