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

## 2. Two branches: `main` ships, `main-ff86ni` is the development branch

`main-ff86ni` carries all of this on top of `main`, each merged with a build and the validator
(0 errors; 6 warnings, five of them the castle chapters' expected to-do lines):

- **A1** every non-starter spell earned through an achievement; the gate on wizards (fixes the
  Emberdeep being unreachable on a new save); Fireball to the Long Coil; **schema 19**.
- **A2** the achievements page (main menu and pause tab). **A3** the shop sells no spells; its
  three boons fold into Powers. **A4** the secret core (`.ai/stage-secrets.md` section 13 is the
  recipe for adding a secret).
- **Two of the nine spell vaults:** the Geode Heart (Sunken Cave, Frost Shard) and the Swallow-Hole
  (Blighted Swamp, Gravewell); the secrets' effects redrawn on the pixel grid.
- **C0** the castle plumbing: the Last Bridge, the Court and the Throne exist, `IsPlayable = false`,
  on stand-ins, their music wired.
- **V2** the Sunken Cave's own caverns.
- The results screen's "closest unlock" no longer names a sealed deed.

**Do not merge `main-ff86ni` into `main` or release from it yet.** Seven spells are in vaults that
are not built, so a new save could not earn **Hunter's Draw, the Familiar, Starfall, Molten Shard,
Kindled Ward, Sleet Runes or Stormsweep**. Saves that already own them keep them. Once all nine
vaults are built, merge `main-ff86ni` into `main` (with a merge commit) and release.

## 3. Unfinished branches, in priority order

Each was stopped mid-task and pushed as is. Merge `main-ff86ni` into the branch first, finish the
work against its design, then build, run the validator (`-- --no-save-writes`, always: schema 19
migrates a real save the moment it loads) and the checks the design names, and merge it into
`main-ff86ni`.

| branch | what | state |
|---|---|---|
| `wip/vaults-a` | the Pale Hart (Forest, Hunter's Draw), the Pinfold (Fields, the Familiar), the Fallen Star (Broken Circle, Starfall), the Cold Forge (Cursed Dungeon, Molten Shard) | pixel fixes merged; the four vaults are **WIP b0ad603, unverified** |
| `wip/vaults-b` | the Broken Ward (Ruins, Kindled Ward), the Buried Runes (Waste, Sleet Runes), the Fulgurite (Sands, Stormsweep) | the Swallow-Hole is merged; the rest is **WIP 2b6bd6a, unverified** (the Broken Ward was in progress) |
| `wip/castle-bridge` | C1, the Last Bridge (`.ai/endgame-castle.md` section 3) | 51c6463: band, abyss, collapsing front, towers; the agent was on the ground detail. Not yet verified by the integrator. Do not flip `IsPlayable` |
| `wip/castle-court` | C3, the Court (section 5) | bde62e3: layout, `Authored` reader, rules, rooms, photographed; the agent was writing a tour probe. Not yet verified by the integrator |
| `wip/variety-walls` | V1, the Mystic Ruins' broken walls (V2 is merged) | **WIP 813db36, unverified**: mostly generated wall tiles |
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
