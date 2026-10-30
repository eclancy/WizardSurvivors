# Work in flight (snapshot, 2026-10-30)

A handoff note from the cloud session that ran 2026-10-28 to 10-30, for whichever session picks the
work up next (Eric asked to move it to a session on his PC). **A snapshot, not a source of truth:**
check `git log` and the branches before acting on it, and delete this file once everything below has
landed or been dropped.

## Release first

Nothing since **0.8.1** is released. `main` builds and passes the startup validator, and is ready to
ship. Eric's commit 42b85e1 already set `config/version` to 0.9.1 and the Android `version/code` to
901 by hand, so `release.sh 0.9.1` refuses (901 is not above 901). Release the next number instead:

```sh
git checkout main && git pull
bash tools/release/release.sh --dry-run 0.9.2
bash tools/release/release.sh 0.9.2 "A world map replaces the chapter list: walk your wizard along the road between chapters and see what each one still hides. Every spell redrawn as crisp pixel art. Twenty-six spells have new evolutions that change how they look and play, not just their numbers. The game now starts fullscreen on PC (F11 toggles)."
```

It has to run on the Windows PC: it needs Inno Setup, the Android release keystore and its secret
(`%APPDATA%\Godot\keystores`), the Android SDK and the full export templates (`.ai/releasing.md`).

## On main since 0.8.1

- Every spell's own evolutions, phase 1 (16 spells) and phase 2 so far (10): `.ai/spell-roster.md`
  2e and 2h.
- The pixel-art pass on 31 of 34 spells: `.ai/art-replacement-manifest.md` section 9.
- The world map stage select: `.ai/world-map.md`.
- The designs for achievements, secrets and the castle: `.ai/achievements-and-secrets.md`,
  `.ai/endgame-castle.md` (both marked decided).
- The secret stings and the three castle music sets, generated but not yet wired or heard:
  `.ai/audio-manifest.md`.
- The losing-streak ("pity") Arcane Energy bonus is removed.

## Two branches: `main` is releasable, `main-ff86ni` is not yet

**`main` is what ships.** **`main-ff86ni`** (the development branch) carries A1 on top of it:
every spell earned through an achievement, the gate on wizards (which also fixes the Emberdeep
being unreachable on a new save), Fireball to the Long Coil, schema 19. A1 moves nine spells into
spell vaults that are not built in the world yet, so **on `main-ff86ni` a new save cannot earn
Hunter's Draw, the Familiar, Starfall, Frost Shard, Molten Shard, Gravewell, Kindled Ward, Sleet
Runes or Stormsweep.** Saves that already own them keep them. Do not merge `main-ff86ni` into `main`
or release from it until the vaults are built (A4 and the chapter agents,
`.ai/achievements-and-secrets.md` section 9).

## Branches still to merge or finish

Merge each into `main-ff86ni` with a merge commit, then build and run the validator
(`-- --no-save-writes`, always: schema 19 migrates a real save the moment it loads).

| branch | what | state |
|---|---|---|
| `wip/achievements-core` | A1 | **merged into `main-ff86ni`** (held off `main`, above) |
| `wip/achievements-page` | A2: the achievements page in the main menu and pause screen | finishing in the cloud session; merges into `main-ff86ni` |
| `wip/evolutions2-sky` | phase 2 cards for Stormsweep, Sleet Runes and Aegis Ward (Starfall landed) | **unfinished WIP commit d8048b3**; also owes those three spells' pixel-art pass |
| `wip/evolutions2-blasts` | phase 2 cards: Solar Flare landed; Arcane Explosion, Fireball and Meteor Swarm not started | merge main first (their pixel-art passes landed) |

Every other `wip/*` branch is fully merged and can be deleted on GitHub.

## What comes next (the build plans)

- `.ai/achievements-and-secrets.md` section 9: after A1, the shop (A3), the secret core (A4), then one
  agent per chapter for its secrets, vault first (A5-A13).
- `.ai/endgame-castle.md` section 9: after A1, the plumbing (C0) alone, then the Last Bridge, the
  Court, the casts, the Ruins' walls and the Cave's caverns in parallel; the Dungeon's move (C2,
  schema 20) ships in the same release as the Bridge's flip.

## Loose ends noticed along the way

- The pause screen numbers chapters by stage index (the Fallow Fields shows "Chapter 10"); the map
  says "Chapter 2 of 10". `Node2DGame.cs`, around the pause screen's chapter line.
- `ImpactDebris`, the burst about thirty spells share, still fades by alpha in its last 30%, and Ice's
  first two ramp colours vanish on the Sketchbook's paper. One fix there covers every cold spell.
- `lingering_pool.gdshader` has no caller since Fireball and Arcane Explosion draw their own pools.
- CLAUDE.md says only Python 2.7 is installed; the cloud container had Python 3.13, and the art and
  audio generators were made to run on both.
