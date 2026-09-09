# Audio manifest — what exists, and where each sound belongs

Companion to `.ai/audio-direction.md`, which holds the contract and the reasoning. This file
is the inventory and the wiring backlog: sixty-nine generated `.wav` files in `assets/sfx/`,
what each one is for, and where in the code it is meant to be triggered.

**Status as of 2026-09-05: every file is generated; none is wired.** The game currently
plays music only — `MusicPlayer.cs` plus three `ResourceLoader.Load<AudioStream>` calls in
`TitleScreen.cs`, `MainMenu.cs` and `Node2DGame.cs`. There is not a single `.wav` playback
call anywhere in `scripts/`. The Trigger column below is therefore a **proposal**, not a
description of existing behaviour, and the line references are from 2026-09-05.

Lengths and peak levels are generated. Do not retype them:

```
python tools/audio/build.py --manifest
```

## Wiring order

Do it in this order — each step is independently playable and the early ones carry most of
the perceived value:

1. **Infrastructure.** An `SFX` bus resolved the way `MusicPlayer.ResolveMusicBusName()`
   resolves `Music`, a preloaded stream cache, and a pooled 2D player. Nothing else works
   without this and nothing here should allocate per hit.
2. **The hit loop** — `enemy_hurt_*`, `impact_*`, `enemy_death_*`, `player_hurt`. This is
   95% of the sounds a player actually hears, and it is where the rate limiting in
   `.ai/audio-direction.md` §7 has to be right the first time.
3. **Casts** — one per element, from the spell's own element weights.
4. **Rewards and menus** — `pickup_*`, `level_up`, `chest_open`, `card_*`, `ui_*`.
5. **Bosses, elites, stingers** — `boss_*`, `elite_spawn`, `wave_warning`, `stage_clear`,
   `game_over`, `meta_unlock`.
6. **The beam and the special shapes** — the three `spell_beam_*` files, `spell_orbit`,
   `spell_summon`, `spell_bow_*`. These need per-spell state, so they come last.

## Elements — casts

One per canonical element. A spell picks its file from its own `ElementWeights`; where a
spell carries two elements, use the heavier one, and break a tie toward the more distinctive
voice rather than alphabetically.

| File | Length | Peak | Trigger |
|---|---|---|---|
| `cast_fire.wav` | 0.60 s | −9.0 dBFS | Fireball, MoltenShard, MeteorSwarm, ScorchingRay, SolarFlare |
| `cast_ice.wav` | 0.52 s | −9.0 dBFS | FrostShard, GlacialSpike, ConeOfCold |
| `cast_arcane.wav` | 0.70 s | −9.0 dBFS | ArcaneExplosion, MagicMissile, SpiritualWeapon |
| `cast_darkness.wav` | 0.78 s | −9.0 dBFS | ShadowBolt, VoidLance, BlackTentacles |
| `cast_light.wav` | 0.93 s | −9.0 dBFS | SolarFlare's Light half, any future Light spell |
| `cast_grass.wav` | 0.46 s | −9.0 dBFS | ThornVine, GuardianVines |
| `cast_earth.wav` | 0.58 s | −9.0 dBFS | ObsidianSpike, GroundSpike, StoneBulwark |
| `cast_wind.wav` | 0.80 s | −9.0 dBFS | GaleBlade, CycloneSlash |
| `cast_lightning.wav` | 0.42 s | −9.0 dBFS | ChainLightning, StormguardAura |
| `cast_poison.wav` | 0.68 s | −9.0 dBFS | ToxicSporeBurst, VenomCloak |
| `cast_metal.wav` | 1.03 s | −9.0 dBFS | HuntersDraw, AegisWard |
| `cast_water.wav` | 0.54 s | −9.0 dBFS | TidalBarrier, any future Water spell |

`Player`'s existing `spellFireTimers` loop is the single place these should be triggered
from — one call site, not one per spell scene.

## Elements — impacts

Played where a projectile or AoE actually damages something. **Rate-limit these**: an AoE
landing on forty enemies plays one impact, not forty.

| File | Length | Peak | Trigger |
|---|---|---|---|
| `impact_fire.wav` | 0.33 s | −13.0 dBFS | the damage-dealing call in each Fire spell's scene script |
| `impact_ice.wav` | 0.29 s | −13.0 dBFS | as above, plus `Enemy.ApplySlow` freeze onset |
| `impact_arcane.wav` | 0.39 s | −13.0 dBFS | `ArcaneExplosion.cs`, `MagicMissile.cs` |
| `impact_darkness.wav` | 0.43 s | −13.0 dBFS | `ShadowBolt.cs`, `VoidLance.cs` |
| `impact_light.wav` | 0.51 s | −13.0 dBFS | Light spell hits; also the Light element's heal-on-damage tier |
| `impact_grass.wav` | 0.25 s | −13.0 dBFS | `ThornVine.cs` |
| `impact_earth.wav` | 0.32 s | −13.0 dBFS | `ObsidianSpike.cs`, `GroundSpike.cs` |
| `impact_wind.wav` | 0.44 s | −13.0 dBFS | `GaleBlade.cs`, `CycloneSlash.cs` |
| `impact_lightning.wav` | 0.23 s | −13.0 dBFS | `ChainLightning.cs`, including each chained bounce |
| `impact_poison.wav` | 0.38 s | −13.0 dBFS | `ToxicSporeBurst.cs`; **not** per poison tick |
| `impact_metal.wav` | 0.57 s | −13.0 dBFS | `HuntersArrow` landing |
| `impact_water.wav` | 0.30 s | −13.0 dBFS | Water spell hits |

`Enemy.ApplyPoison` ticks every second for its whole duration on every poisoned enemy. It
must **not** play a sound per tick — that is the single worst mix hazard in the game.

## Spell shapes

Sounds no element table can express, because they are about a spell's *form* rather than its
element.

| File | Length | Peak | Loops | Trigger |
|---|---|---|---|---|
| `spell_explosion.wav` | 0.91 s | −5.0 dBFS | no | `ArcaneExplosion.cs`, `MeteorImpact.cs` — the only files allowed sub-80 Hz energy besides the boss |
| `spell_beam_start.wav` | 0.28 s | −11.0 dBFS | no | `ScorchingRayBeam.cs` ignition |
| `spell_beam_loop.wav` | 0.50 s | −14.0 dBFS | **yes** | the beam sustain — its `.import` already carries `edit/loop_mode=1`; keep it if the file is regenerated |
| `spell_beam_end.wav` | 0.34 s | −12.0 dBFS | no | beam extinguish |
| `spell_orbit.wav` | 0.17 s | −20.0 dBFS | no | `SpiritualWeapon.cs` / `OrbitingBlade.cs` per-tick hit |
| `spell_summon.wav` | 0.75 s | −9.0 dBFS | no | `BlackTentacles.cs`, `ThornVine.cs`, `GuardianVines.cs` emergence |
| `spell_bow_draw.wav` | 0.45 s | −15.0 dBFS | no | `BowDrawVisual.cs` wind-up start |
| `spell_bow_release.wav` | 0.30 s | −10.0 dBFS | no | `HuntersArrow` spawn |

## The player

Trigger points map onto the damage pipeline in `CLAUDE.md`, and the order matters:

| File | Length | Peak | Trigger |
|---|---|---|---|
| `player_dodge.wav` | 0.26 s | −13.0 dBFS | `Player.cs` dodge branch (`scripts/Player.cs:1387`), where the hit is fully avoided — this replaces `player_hurt`, it does not accompany it |
| `player_shield_absorb.wav` | 0.30 s | −12.0 dBFS | shield pool absorb (`scripts/Player.cs:1410`), when `absorbed > 0` |
| `player_shield_break.wav` | 0.65 s | −8.0 dBFS | the same block, when `shieldPoints` reaches 0 |
| `player_hurt.wav` | 0.34 s | −6.0 dBFS | after mitigation, only when HP actually fell — read the HP delta, not the `DamageTaken` signal argument, which carries the **pre**-mitigation amount |
| `player_heal.wav` | 0.60 s | −11.0 dBFS | any heal, including the Light element tier and the extra-life half-heal |
| `player_low_health.wav` | 0.55 s | −14.0 dBFS | a repeating timer below a HP threshold — designed to loop as a heartbeat, not to fire once |
| `player_death.wav` | 1.60 s | −4.0 dBFS | the `Died` emit, **not** the extra-life branch |

## Enemies

| File | Length | Peak | Trigger |
|---|---|---|---|
| `enemy_hurt_a/b/c.wav` | 0.13 s | −17.0 dBFS | `Enemy.TakeDamage` (`scripts/Enemy.cs:374`) — pick at random, randomise pitch, and throttle globally to about one per 60 ms |
| `enemy_death_small.wav` | 0.45 s | −14.0 dBFS | `Enemy.StartDeath` (`scripts/Enemy.cs:415`) for swarmers and runners |
| `enemy_death_heavy.wav` | 0.75 s | −11.0 dBFS | `StartDeath` for bruisers, tanks and shielders |
| `enemy_shoot.wav` | 0.20 s | −14.0 dBFS | `RangedEnemy.cs` firing an `EnemyProjectile` |
| `enemy_melee.wav` | 0.27 s | −12.0 dBFS | a contact hit landing on the player, paired with `player_hurt` |
| `elite_spawn.wav` | 0.90 s | −8.0 dBFS | an elite entering the field |
| `boss_roar.wav` | 1.80 s | −4.0 dBFS | boss spawn and phase change (`BossEnemy.cs`) |
| `boss_death.wav` | 2.43 s | −3.0 dBFS | `BossEnemy.StartDeath` (`scripts/BossEnemy.cs:141`) |

`StartDeath` drops rewards and leaves the `"enemies"` group in the same frame, before the
death animation plays. The death sound is not gating anything and may outlast the corpse.

## Pickups and progression

| File | Length | Peak | Trigger |
|---|---|---|---|
| `pickup_xp.wav` | 0.09 s | −22.0 dBFS | `XPOrb.OnBodyEntered` (`scripts/XPOrb.cs:82`) — ramp `PitchScale` upward with the collection streak, reset when it breaks |
| `pickup_health.wav` | 0.42 s | −13.0 dBFS | `PickupBase.OnBodyEntered` (`scripts/PickupBase.cs:78`) for `HealthPickup` |
| `pickup_magnet.wav` | 0.37 s | −13.0 dBFS | a vacuum or `FortunesFavor` pull beginning — once per pull, not per orb |
| `level_up.wav` | 0.96 s | −5.0 dBFS | `LevelUpPickup` / the level-up trigger, before `LevelUpMenu` opens |
| `chest_open.wav` | 1.26 s | −6.0 dBFS | `ChestReward.cs`, before `ChestItemSelectionMenu` opens |
| `spell_evolve.wav` | 1.70 s | −4.0 dBFS | a `SpellEvolutionCatalog` evolution being applied |

## Interface

| File | Length | Peak | Trigger |
|---|---|---|---|
| `card_appear.wav` | 0.18 s | −15.0 dBFS | each card dealt in `LevelUpMenu` / `ChestItemSelectionMenu`, staggered ~70 ms apart |
| `card_select.wav` | 0.55 s | −9.0 dBFS | an option confirmed in either menu |
| `reroll.wav` | 0.23 s | −12.0 dBFS | `LevelUpMenu.OnRerollPressed` (`scripts/LevelUpMenu.cs:51`) |
| `ui_hover.wav` | 0.05 s | −24.0 dBFS | `Control.MouseEntered` on any button — fires constantly, hence the level |
| `ui_click.wav` | 0.11 s | −16.0 dBFS | any forward/confirm button |
| `ui_back.wav` | 0.12 s | −16.0 dBFS | any back/cancel button, including `ui_cancel` |
| `ui_denied.wav` | 0.24 s | −15.0 dBFS | a locked stage or unaffordable arcane upgrade |
| `ui_open.wav` | 0.35 s | −14.0 dBFS | a menu or overlay opening |
| `ui_close.wav` | 0.30 s | −15.0 dBFS | a menu or overlay closing |
| `ui_pause.wav` | 0.30 s | −12.0 dBFS | the in-run pause |

## Run and meta stingers

| File | Length | Peak | Trigger |
|---|---|---|---|
| `wave_warning.wav` | 1.30 s | −7.0 dBFS | a wave or boss inbound, from `Node2DGame`'s wave scheduling |
| `stage_clear.wav` | 1.85 s | −4.0 dBFS | a run survived to its end |
| `game_over.wav` | 2.50 s | −5.0 dBFS | `GameOverScreen` appearing |
| `meta_unlock.wav` | 1.70 s | −4.0 dBFS | `SaveData` gaining a character, stage or max-difficulty record — the one sound that means the **save** changed, not the run |

## Deliberately absent

Listed so nobody adds them by reflex:

- **Footsteps.** The player is always moving in this genre; footsteps would be the loudest
  thing in the mix by play count and carry no information.
- **A sound per poison or burn tick.** Status damage is continuous and would smear the mix.
  The application is audible; the ticks are not.
- **Per-projectile travel loops.** A dozen missiles in flight is a dozen loops.
- **A distinct hurt sound per enemy class.** Class identity is the silhouette's job
  (`.ai/art-direction.md` §4). Three seeded variants plus pitch randomisation cover it.
- **Music.** `Pixel_Knights.mp3` and `background_music.mp3` stay as they are; this pipeline
  cannot produce anything comparable.
- **Voice or foley of any kind.** Out of reach of pure synthesis, and out of scope.

## Known limitations

- **Nobody has heard these.** They were designed and measured, not auditioned. The audition
  reels at `tools/audio/_preview_<category>.wav` exist for exactly that pass, and the first
  listen will almost certainly move some levels. Levels are one number each in `REGISTRY`.
- **The confusion audit is a proxy.** It measures spectrum and envelope, not melody or
  meaning. `level_up`, `chest_open` and `stage_clear` are separated by their note sequences,
  which the metric cannot see and which a listener certainly can.
- **`ui_denied` is the one sound with a deliberate square-wave buzz.** If it reads as a bug
  rather than a refusal, that is the first candidate for a redesign.

## Music

Not synthesised. `tools/audio/` cannot produce anything comparable to a produced track, and the
ADR says so; these are licensed or sourced files that live in the repo as ordinary assets.

| File | Where it plays | Loops | Chosen by |
|---|---|---|---|
| `assets/music/labyrinth-escape.mp3` | every chapter, for the whole run | **yes** | `MusicCatalog.RunTrackForStage` |
| `assets/Pixel_Knights.mp3` | title screen and main menu | no | `TitleScreen.cs`, `MainMenu.cs` |

**`MusicCatalog` is where a track is chosen**, not `Node2DGame`. Every chapter shares one loop
today and the plan is one each, so it is a table keyed by stage index with an empty per-stage
map and a shared default: adding chapter 3 its own music is one row, not a conditional at the
call site. When `StageCatalog` lands, this table is a good candidate to fold onto
`StageDefinition` alongside the other per-chapter facts - **move** it rather than copying it, or
the campaign gains a second, disagreeing roster.

### Two things to check before shipping

**The licence is unverified.** The run track arrived as
`good_day_story-labyrinth-escape-333453.mp3`, which is the filename shape a stock library hands
out - artist, title, asset id. It was renamed to `labyrinth-escape.mp3` on the way in, so this
line is now the only record of where it came from. Confirm the licence and whether it requires
attribution in-game before release. The same is unknown for `Pixel_Knights.mp3`, which predates
this note.

**`assets/background_music.mp3` is now unreferenced.** It was the old run track and nothing
loads it any more. Four megabytes of dead asset; delete it once you are sure the new track is
staying.

### Looping

The run track is imported with `loop=true`, set in `assets/music/labyrinth-escape.mp3.import`.
That setting lives in the `.import` file rather than the MP3, so **regenerating or re-adding the
file loses it**.

This is a fix as much as a setting: `background_music.mp3` was imported with `loop=false`, so a
run that outlasted the track simply went quiet and stayed quiet. The menu track is deliberately
left unlooped, because nobody sits on the title screen long enough to notice and the loop point
was never authored.
