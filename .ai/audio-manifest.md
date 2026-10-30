# Audio manifest — what exists, and where each sound belongs

Companion to `.ai/audio-direction.md`, which holds the contract and the reasoning. This file
is the inventory and the wiring backlog: seventy-one generated `.wav` files in `assets/sfx/`,
what each one is for, and where in the code it is meant to be triggered. The music - a menu
theme and nine chapter sets in `assets/music/` - has its own section at the end.

**2026-10-30: 84 files, all wired.** The secret stings (piece A14 of
`.ai/achievements-and-secrets.md`): `secret_seen`, `secret_found` and `secret_shatter`, generated,
allowlisted in `SfxCatalog` and - since piece A4 the same day - played where the table under
"Secrets" below proposed: `secret_seen` at the site by `SecretDirector`'s seen mark, once per secret
per save; `secret_found` by `SecretDirector.Found` for a secret found again (`event_reward` is a side
event's chest again); `secret_shatter` by the Geode Heart (`GeodeHeart.OnBroke`). `SfxPlayer` reports
`84 sounds preloaded`. Also on 2026-10-30, three castle sets were rendered for the music
(the Last Bridge, the Court, the Throne; "The castle's music" at the end) and are not yet in
`MusicCatalog`. And the generators now run under Python 3 (the container has only 3.13) as well as
2.7, writing the same bytes under either: all 81 earlier effects and the heath's three stems
re-rendered byte-identical under 3.13 (`.ai/audio-direction.md` section 3).

**2026-10-25: 81 files.** `spell_stormsweep` and `spell_sleet_rune` were added and wired for the facing family
(`.ai/spell-roster.md` "The facing family"); `SfxPlayer` reports `81 sounds preloaded`. Each REPLACES
its spell's element cast cue rather than sitting on it (both spells return `CastOutcome.Maintained`):
- `spell_stormsweep` - one Stormsweep pass, every pass of every arc, throttled at 60 ms. An arc of
  storm-light gathers and then discharges: 80 ms of rising air as it leaps out, then a band-limited snap
  gone in 15 ms with a short 90 Hz-gated buzz behind it as it sweeps. -14 dBFS (up to three sweeps a
  second at level 8), hf 13%, centroid 2.2 kHz; nearest neighbour in the whole set `chest_open` at 0.161.
  The first draft borrowed Lightning's palette crackle for its tail and measured 0.025 from
  `cast_lightning` and 0.039 from `impact_lightning` - a cast that sounded like a lightning hit, on the
  spell whose hits are lightning hits - and was rebuilt as the gather-then-discharge construct; a second
  pass took its hf from 17% to 13%.
- `spell_sleet_rune` - one Sleet Rune leaving the hand, every rune, throttled at 45 ms, Swarm tier. A
  bandpass sweeping UP through 900-2400 Hz (the orbit's swipe falls) ending on a two-partial ice chime
  45 ms in - a late attack, which no other spell sound has. -22 dBFS beside `spell_orbit`, hf 13%,
  centroid 2.3 kHz; nearest neighbour `player_dodge` at 0.234.
(Both were first written as `spell_lash` and `spell_knife`, for a build the developer asked to be rethemed
as magic; same constructs, renamed before anything shipped.)

**Before that, 79 files.** `spell_familiar_bite` was added and wired (`Familiar.Bite` and each strike
of the Griffin's swoop; `.ai/spell-roster.md` "Familiar"). Two pitched clacks 24 ms apart - the jaws
meeting, the teeth setting - then a short huff of breath that rises, at -21 dBFS beside `spell_orbit`,
Swarm tier, throttled at 110 ms at the call site, because a pack bites several times a second. No bark:
a voice at that rate is what the loudness table exists to stop. hf 4%, centroid 1.36 kHz; nearest
neighbour in the whole set `hint_lantern_flare` at 0.126. The first draft, a falling breath over a
falling growl, was `spell_beam_end`'s construct (falling noise over a falling saw) and measured its twin
at 0.098; it was rebuilt, not nudged. The familiar's arrival and each new form play `spell_summon`.

**Before that, 78 files.** `spell_starfall` was added and wired (`Starfall.Land`: every star of the
Starfall rain as it lands, hit or miss; `.ai/spell-roster.md` "Starfall"). A falling run of three
glass glints, D7 G6 D6 28 ms apart, at -24 dBFS - under `spell_orbit`, because at level 8 it is the
most frequent spell sound in the game - Swarm tier, throttled at 90 ms at the call site. hf 0%,
centroid 1.45 kHz; nearest neighbour in the whole set `reroll` at 0.178. Two drafts were twins and
were rebuilt rather than nudged: a swish under one glint (`spell_orbit`, 0.056) and two glints
(`player_shield_absorb`, 0.065 - a star landing must not sound like the shield taking a hit).

Also on 2026-10-25, five chapter hints were added and wired (`StageHintDirector`, see
`.ai/side-events.md` "Hints that something is out there"): one cue per side-event chapter, played
a few hundred pixels toward the place a few times a run, under a caption. `SfxPlayer` reports `78
sounds preloaded` with both. Every one is far away by construction - highpassed, lowpassed at 2-3.4 kHz and
put in a room - and none has more than 3% of itself above 4 kHz. Nearest neighbour of each in the
whole set: the cry `level_up` 0.124, the whisper `enemy_melee` 0.219, the lantern `level_up` 0.127,
the ice `player_death` 0.204, the bell `bearing_found` 0.182. Three first drafts collided and were
rebuilt (the table under "Chapter hints").

**2026-10-22: 72 files.** `bearing_found` was added and wired (`WayfinderPickup.Take`, the Arcane
Compass and its cousins; see `.ai/side-events.md` "The wayfinder"); `SfxPlayer` reports `72 sounds
preloaded`. Five decelerating wooden ticks - a needle coming to rest - then a low open fifth that
holds while the arrows come up. At -8 dBFS, once a run, lowpassed throughout (hf 2%, centroid
923 Hz); nearest neighbour in its category is further than 0.17.

**2026-10-20: 71 files.** `pickup_haste` and `event_reward` were added and wired, and `chest_open`
was wired; `SfxPlayer` reported `71 sounds preloaded`. The status paragraph below is the 2026-09-07
record and its counts are of that date.

**Status as of 2026-09-07: all 69 generated, 44 wired.** The infrastructure is in and
verified in Godot — `SfxPlayer` reports `69 sounds preloaded on bus 'SFX'` and
`ContentValidator` finishes with no audio warnings.

Wired: the whole hit loop (`enemy_hurt_*`, `enemy_death_*`, `boss_death`, `enemy_shoot`), the
player pipeline (`player_dodge`, `player_shield_absorb`, `player_shield_break`, `player_hurt`,
`player_heal`, `player_death`), all twelve `cast_*`, `pickup_xp` with its streak ladder,
`pickup_health`, `level_up`, `card_select`, `reroll`, `ui_back` and `game_over`.

All twelve `impact_*` are now wired too, through the `source` parameter on
`Player.DealDamageToEnemy` - the one funnel every damaging spell already used.

**Not yet wired — 25 files.** Every `spell_*` shape, `elite_spawn`, `boss_roar`,
`enemy_melee`, `player_low_health`, `pickup_magnet`, `chest_open`, `spell_evolve`,
`card_appear`, and the rest of `ui_*` and the meta stingers. For those, the Trigger column
below is still a **proposal** rather than a description of behaviour.

Note that `elite_spawn` and `boss_roar` are wired in the tier table but have no trigger site
yet, so their ducking is defined and untested.

Lengths and peak levels are generated. Do not retype them:

```
python tools/audio/build.py --manifest
```

## How it is wired

`scripts/SfxPlayer.cs` is an autoload and the only place a sound effect is played from. It
preloads every stream in `_Ready`, owns a fixed pool of 24 positional and 8 non-positional
voices, and holds the throttles. Nothing allocates per hit and no call site sets a volume,
because `tools/audio/build.py` has already normalised every file to its mix target.

`scripts/SfxCatalog.cs` names every file. It is the same allowlist discipline
`ContentValidator.SpellResourcePaths` follows: `RegressionChecks.ValidateAudio` walks it at
startup, so a missing or renamed file is a console warning rather than silence mid-fight. The
24 element sounds are derived from the `Element` enum rather than listed, so the two cannot
drift.

`default_bus_layout.tres` defines **Master, Music and SFX**. Before it existed the project had
only Godot's default Master bus, so `ResolveMusicBusName()` fell through to `"Master"` and the
Music slider was a second Master slider. `scripts/AudioSettings.cs` now routes every slider and
the mute toggle through the save file, so volumes survive a relaunch.

### How impacts are wired

It turned out not to need twenty edits. Every damaging spell already funnels through
`Player.DealDamageToEnemy`, which now takes an optional `source` SpellData used for nothing but
choosing the impact sound. A spell opts in by passing it:

```csharp
player?.DealDamageToEnemy(enemy, damage, source: SpellData);
```

`SfxPlayer.Impact` throttles per element at 55 ms, so an AoE landing on forty enemies makes one
sound. `Enemy.TakeDamage` would have been a smaller edit still and is the wrong place: the
enemy does not know what hit it, so all twelve elements would sound identical.

Two call sites are deliberately left out. `ScorchingRayBeam` damages continuously, so an impact
per tick would be a drone - it wants `spell_beam_*` instead. `OrbitingBlade` has `spell_orbit`
for the same reason.

## Elements — casts

One per canonical element. A spell picks its file from its own `ElementWeights`; where a
spell carries two elements, use the heavier one, and break a tie toward the more distinctive
voice rather than alphabetically.

**The persistent emitters cue themselves.** Solar Flare and Toxic Spore Burst (`ElementalPulse`)
and Cinderbreath (`Flamethrower`) are created once and then kept current, so the fire loop in
`Player` returns `CastOutcome.Maintained` for them - including on the creation frame - and never
plays their cast. Instead `ElementalPulse.TriggerPulse` plays the cast cue on every pulse, hit
or not, and `Flamethrower` plays it each time a burn lights (once per burn, not per puff). The
first pulse and the first burn come from their `_Ready`, which is why creation is not `Fired`:
that would play the first one twice.

| File | Length | Peak | Trigger |
|---|---|---|---|
| `cast_fire.wav` | 0.60 s | −11.0 dBFS | Fireball, MoltenShard, MeteorSwarm, ScorchingRay, SolarFlare |
| `cast_ice.wav` | 0.52 s | −11.0 dBFS | FrostShard, GlacialSpike, ConeOfCold |
| `cast_arcane.wav` | 0.70 s | −11.0 dBFS | ArcaneExplosion, MagicMissile, SpiritualWeapon |
| `cast_darkness.wav` | 0.78 s | −11.0 dBFS | ShadowBolt, VoidLance, BlackTentacles |
| `cast_light.wav` | 0.93 s | −11.0 dBFS | SolarFlare's Light half, any future Light spell |
| `cast_grass.wav` | 0.46 s | −11.0 dBFS | ThornVine, GuardianVines |
| `cast_earth.wav` | 0.58 s | −11.0 dBFS | ObsidianSpike, GroundSpike, StoneBulwark |
| `cast_wind.wav` | 0.80 s | −11.0 dBFS | GaleBlade, CycloneSlash |
| `cast_lightning.wav` | 0.42 s | −11.0 dBFS | ChainLightning, StormguardAura |
| `cast_poison.wav` | 0.68 s | −11.0 dBFS | ToxicSporeBurst, VenomCloak |
| `cast_metal.wav` | 1.03 s | −11.0 dBFS | HuntersDraw, AegisWard |
| `cast_water.wav` | 0.54 s | −11.0 dBFS | TidalBarrier, any future Water spell |

`Player`'s existing `spellFireTimers` loop is the single place these should be triggered
from — one call site, not one per spell scene.

## Elements — impacts

Played where a projectile or AoE actually damages something. **Rate-limit these**: an AoE
landing on forty enemies plays one impact, not forty.

| File | Length | Peak | Trigger |
|---|---|---|---|
| `impact_fire.wav` | 0.33 s | −15.0 dBFS | the damage-dealing call in each Fire spell's scene script |
| `impact_ice.wav` | 0.29 s | −15.0 dBFS | as above, plus `Enemy.ApplySlow` freeze onset |
| `impact_arcane.wav` | 0.39 s | −15.0 dBFS | `ArcaneExplosion.cs`, `MagicMissile.cs` |
| `impact_darkness.wav` | 0.43 s | −15.0 dBFS | `ShadowBolt.cs`, `VoidLance.cs` |
| `impact_light.wav` | 0.51 s | −15.0 dBFS | Light spell hits; also the Light element's heal-on-damage tier |
| `impact_grass.wav` | 0.25 s | −15.0 dBFS | `ThornVine.cs` |
| `impact_earth.wav` | 0.32 s | −15.0 dBFS | `ObsidianSpike.cs`, `GroundSpike.cs` |
| `impact_wind.wav` | 0.44 s | −15.0 dBFS | `GaleBlade.cs`, `CycloneSlash.cs` |
| `impact_lightning.wav` | 0.23 s | −15.0 dBFS | `ChainLightning.cs`, including each chained bounce |
| `impact_poison.wav` | 0.38 s | −15.0 dBFS | Toxic Spore Burst's bursts (`SporeMushroom`, through `DealDamageToEnemy`); **not** per poison tick |
| `impact_metal.wav` | 0.57 s | −15.0 dBFS | `HuntersArrow` landing |
| `impact_water.wav` | 0.30 s | −15.0 dBFS | Water spell hits |

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
| `spell_orbit.wav` | 0.17 s | −22.0 dBFS | no | `SpiritualWeapon.cs` / `OrbitingBlade.cs` per-tick hit |
| `spell_starfall.wav` | 0.16 s | −24.0 dBFS | no | **wired**, `Starfall.Land`: every star of the rain landing, hit or miss, throttled at 90 ms (`Starfall.SoundGapMs`). Its hits are the Light impact through `DealDamageToEnemy`, as for any spell; the rain has no cast cue (`CastOutcome.Maintained`) because nothing is ever cast |
| `spell_familiar_bite.wav` | 0.13 s | −21.0 dBFS | no | **wired**, `Familiar.Bite` and `Familiar.StrikeAlong`: every strike of every form - a hound's bite, a crow's peck, a cat's swipe (one sound for all three since the forms, 2026-10-28; a peck and a swipe of their own are open) - and every enemy a Griffin's swoop strikes, throttled at 110 ms (`Familiar.SoundGapMs`); also Laelaps catching a bolt in his jaws (`Player.TryCatchBolt`). The damage is the Arcane impact through `DealDamageToEnemy`; the pack has no cast cue (`CastOutcome.Maintained`) |
| `spell_stormsweep.wav` | 0.22 s | −14.0 dBFS | no | **wired**, `Stormsweep.Tick`: every pass of every arc (a Thunderclap arc sweeps twice), throttled at 60 ms (`Stormsweep.SoundGapMs`). It replaces the Lightning cast cue for the spell (`CastOutcome.Maintained`); hits are the Lightning impact through `DealDamageToEnemy`, and a Thunderclap burst plays `cast_lightning` |
| `spell_sleet_rune.wav` | 0.12 s | −22.0 dBFS | no | **wired**, `SleetRunes.Release`: every rune leaving the hand, throttled at 45 ms (`SleetRunes.SoundGapMs`), Swarm tier. It replaces the cast cue for the spell (`CastOutcome.Maintained`); hits are the Ice impact through `DealDamageToEnemy` |
| `spell_summon.wav` | 0.75 s | −9.0 dBFS | no | **wired** (2026-10-23): `BlackTentacles.CastAt` as the rift opens, `ThornVine.Launch` as the vine breaks ground, `SporeMushroom` as a Doomcap breaks ground (the titan only - an ordinary sowing is covered by the cast cue). No slap/lash sound exists; the Darkness/Grass impacts play through `DealDamageToEnemy` as for every hit, and a dedicated wet slap would be the one addition worth generating |
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
| `enemy_hurt_a/b/c.wav` | 0.13 s | −19.0 dBFS | `Enemy.TakeDamage` (`scripts/Enemy.cs:374`) — pick at random, randomise pitch, and throttle globally to about one per 60 ms |
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
| `pickup_xp.wav` | 0.09 s | −23.0 dBFS | `XPOrb.OnBodyEntered` (`scripts/XPOrb.cs:82`) — ramp `PitchScale` upward with the collection streak, reset when it breaks |
| `pickup_health.wav` | 0.42 s | −13.0 dBFS | `PickupBase.OnBodyEntered` (`scripts/PickupBase.cs:78`) for `HealthPickup` |
| `pickup_magnet.wav` | 0.37 s | −13.0 dBFS | a vacuum or `FortunesFavor` pull beginning — once per pull, not per orb |
| `pickup_haste.wav` | 0.38 s | −12.0 dBFS | **wired**, `BuffItem.OnPickedUp` (the speed potion). A double gust and a climbing triangle shimmer; hf 11%, nearest neighbour in the whole set `pickup_magnet` at 0.157. About one kill in a hundred drops a potion, so it sits a dB over `pickup_health` |
| `event_reward.wav` | 1.45 s | −8.0 dBFS | **wired**, `SideEvent.Succeed` when the event pays a chest: a formant breath swelling into one glass tone with vibrato, under the chest rising as a ghost. Not played on a discovery, which keeps `meta_unlock`. hf 3%, nearest neighbour `spell_evolve` at 0.135 |
| `bearing_found.wav` | 1.58 s | −8.0 dBFS | **wired**, `WayfinderPickup.Take`: the chapter's wayfinder taken and every point of interest marked. Five decelerating ticks (a needle settling; `pickup_magnet` accelerates, which is the difference between found and pulled) then an open fifth. hf 2% |
| `level_up.wav` | 0.96 s | −5.0 dBFS | `LevelUpPickup` / the level-up trigger, before `LevelUpMenu` opens |
| `chest_open.wav` | 1.26 s | −6.0 dBFS | **wired** (2026-10-20), `ChestReward.OnPickedUp`, before `ChestItemSelectionMenu` opens. It was allowlisted from the start and never played |
| `spell_evolve.wav` | 1.70 s | −4.0 dBFS | a `SpellEvolutionCatalog` evolution being applied |

## Interface

| File | Length | Peak | Trigger |
|---|---|---|---|
| `card_appear.wav` | 0.18 s | −15.0 dBFS | each card dealt in `LevelUpMenu` / `ChestItemSelectionMenu`, staggered ~70 ms apart |
| `card_select.wav` | 0.55 s | −9.0 dBFS | an option confirmed in either menu |
| `reroll.wav` | 0.23 s | −12.0 dBFS | `LevelUpMenu.OnRerollPressed` (`scripts/LevelUpMenu.cs:51`) |
| `ui_hover.wav` | 0.05 s | −26.0 dBFS | `Control.MouseEntered` on any button — fires constantly, hence the level |
| `ui_click.wav` | 0.11 s | −16.0 dBFS | any forward/confirm button. **Wired** for a purchase in the Arcane Upgrades shop (`MainMenu.Shop.cs`) |
| `ui_back.wav` | 0.12 s | −16.0 dBFS | any back/cancel button, including `ui_cancel` |
| `ui_denied.wav` | 0.24 s | −15.0 dBFS | a locked stage or unaffordable arcane upgrade. **Wired** for the second: A on a shop card that is short of energy or not open yet (`MainMenu.Shop.cs`) |
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

## Chapter hints

Partway through a run, while a chapter's side event still holds a wizard or a spell the save does
not own, a cue comes from its direction (`StageHintDirector`, `StageHintCatalog`). Four a run at
most, so they sit at the quiet end of the once-a-minute band, and the game plays them positionally
300 px toward the source (`SfxPlayer.AtPosition`), which takes another 3.5 dB off and pans them that
way. Tier Normal: they never duck anything. The caption carries the words; these carry the place.

| File | Length | Peak | Trigger |
|---|---|---|---|
| `hint_grove_whisper.wav` | 2.20 s | -9.0 dBFS | **wired**, Enchanted Forest (the grove). Three breathed syllables with gaps between them over a low open-fifth hum and a few leaf ticks. hf 3% |
| `hint_cell_cry.wav` | 2.30 s | -9.0 dBFS | **wired**, Cursed Dungeon (the cell). A woman's call, formant synthesis: one rising "Help!" syllable, then the falling-then-rising tune of "Is someone there?" with no consonants, in a long stone room with a corridor slap. hf 2% |
| `hint_lantern_flare.wav` | 2.30 s | -9.0 dBFS | **wired**, Blighted Swamp (a lantern). A flame catching, burning level for a second at a 13 Hz flicker, guttering out in uneven gaps, under a thin cold E5 with a slow wobble. hf 2% |
| `hint_ice_song.wav` | 2.45 s | -9.0 dBFS | **wired**, Frozen Waste (the ice). A stick-slip groan through two falling resonances, one crack, and under it a man's voice muffled through the ice, two slow notes on "oh". hf 0% |
| `hint_caravan_bell.wav` | 2.50 s | -10.0 dBFS | **wired**, Scorched Sands (the caravan). Five strikes of a hollow tin bell with a wooden clapper, at a walking beast's uneven stride, nearly level, one dune echo. A dB under the rest: a bell carries. hf 0% |

| First draft | Collided with | Distance | Fix |
|---|---|---|---|
| a continuous whisper over a rustle | `cast_wind` | 0.044 | separate breaths with silences (speech rhythm), and a low hum in the band wind is highpassed out of |
| a whoosh, a thump and a decay | `impact_darkness` | 0.062 | the flame holds level, then gutters: held, not struck |
| one strike and its tail | `ui_pause` | 0.084 | five nearly level strikes - a rhythm, not a decay |
| the cry at G4 | `meta_unlock` | 0.104 | raised to B-flat 4 rising to C-sharp 5, called rather than spoken - out of the A-minor chord's partials |

## Secrets

The secrets of `.ai/stage-secrets.md` and `.ai/achievements-and-secrets.md`: the soft shimmer and
the found sting section 7.7 of the first asked for, and the Geode Heart's shatter (8.4.1 of the
second). Generated 2026-10-30 (`tools/audio/sfx.py`, "secrets"), allowlisted in `SfxCatalog`
(`SecretSeen`, `SecretFound`, `SecretShatter`) and wired by piece A4 the same day, each at the
Trigger below (the first reading of `secret_seen`: once per secret per save, at the site).

They are the near tell in sound. A secret's tell is a warm glint that breathes (stage-secrets
section 2), so the shimmer and the sting are a cloud of soft sine grains - each a Hann swell, the
cloud swelling and going - in A minor's pentatonic from A4 to E6, never on G5 (the light element's
bell). Breathed, never struck: every other reward in the game is a struck bell, and a secret must not
sound like one. Nothing in them opens on `palette.transient`, because nothing here is a spell.

| File | Length | Peak | Tier | Trigger (proposed) |
|---|---|---|---|---|
| `secret_seen.wav` | 2.05 s | -9.0 dBFS | Normal | `SecretDirector`, the moment a secret's near tell is first on screen - the seen mark, recorded into `SaveData.GlimpsedSecretIds` (achievements-and-secrets 4.3, 4.9): **`SfxPlayer.AtPosition(name, site)`**, once per secret per save, with no toast and no text. At the site, not global: the tell is already on screen, so the pan points at nothing he has not seen, and at the ~450 px the mark is taken from Godot's attenuation takes another 5-6 dB off, which is what makes -9 "soft". If stage-secrets 7.7's reading is wanted instead (every approach to a dormant site, every run), it still fits the band: a few a run |
| `secret_found.wav` | 1.74 s | -8.0 dBFS | Normal | `SecretDirector.Found`, **in place of `event_reward`** at both of its sites there (a secret found again this run - another pen blooming - and a find in a later run). `meta_unlock` stays where it is for a find that changes the save (the page mark the first time; a vault's spell): it is the one sound that means the save changed. `event_reward` then goes back to meaning a side event's chest. Global, under the toast. A few a run - every Forest pen blooms - so beside `event_reward` |
| `secret_shatter.wav` | 2.30 s | -4.0 dBFS | **Critical** | the Geode Heart's `Breakable` reaching its last stage (the Sunken Cave's vault, C2), as the ring of frost goes out: `SfxPlayer.AtPosition(name, crystal)`. Once a run at most. Then `SecretDirector.Found` plays its own sting a beat later (`meta_unlock` the first time, with Frost Shard; `secret_found` after), so the shatter carries the breaking and the freeze and leaves the reward to that |

What each is:

- **`secret_seen`** - one breath of the glint: twenty-six grains thickening over 0.7 s and thinning
  over the next second, under a faint band of air of the same shape. Not a tremolo (arcane's 17 Hz),
  not detuned triangles (arcane), not a run of glints (`spell_starfall` falls in 60 ms), not a swell
  into a held tone (`event_reward`). Centroid 1.07 kHz, hf 0%.
- **`secret_found`** - the glint, now his: the cloud **gathers** for 0.4 s (its first grain on the
  frame he finds it) and **blooms** into a rolled F major seventh (F3 F4 C5 E5 A5, 18 ms apart), a
  sine and a soft triangle per voice under a lowpass that opens as it sounds, with three grains
  settling over it. Centroid 670 Hz, hf 0%. Warm and on purpose no brighter: spread up to E6 it
  measured 0.112 from `player_heal` and 0.121 from `card_select`, and a Fenced Bloom heals as it is
  found, so this and `player_heal` play together.
- **`secret_shatter`** - two strain ticks 85 ms apart (the hairline cracks giving); the crack, a band
  of noise over a low split falling 190 to 92 Hz (nothing under 80: the sub band is the explosion's
  and the boss's); forty crystal shards going out, dense at the crack and thinning, falling in pitch
  as they fly farther, each two inharmonic partials at 1 : 2.43 - not the ice element's
  1 : 2.76 : 5.40 stack; a cold rush falling 2.2 kHz to 500 Hz (frost spreading - the ice element's
  hiss rises); all in a long cavern. Driven for weight, lowpassed after the drive. Centroid 2.0 kHz,
  hf 6%. It is Critical because it is a vault opening in the thick of the fight the crystal asked
  for, where a Normal sound can lose its voice to the swarm; that makes eleven Critical sounds of
  the fourteen `RegressionChecks` allows.

**The audit.** `analyse.py --confuse` compares inside a category, and these three are a category of
their own (`secret`) - so each was also measured against the **whole** set with the same profile.
Nearest neighbours: `secret_seen` 0.175 `hint_cell_cry`, 0.231 `hint_lantern_flare`;
`secret_found` 0.146 `wave_warning`, 0.152 `card_select`, 0.168 `hint_caravan_bell`, 0.192
`player_heal`; `secret_shatter` 0.155 `cast_wind`, 0.160 `card_appear`, 0.203 `spell_beam_end`. The
other glass breaking in the set, `player_shield_break` - one struck stack in 0.65 s where this is a
cascade over a second and a half - is not among its nearest six. Nothing within 0.14 of anything;
inside their own category the closest pair is `secret_seen` / `secret_found` at 0.415, and the full
`analyse.py --confuse` still reports **0 confusable pairs**.

| First draft | Measured | Fix |
|---|---|---|
| the found bloom voiced close from F3 at full weight | centroid 570 Hz, in the band the music owns | the low F softened (0.45) |
| the found bloom spread up to E6 for brightness | 0.112 from `player_heal`, 0.121 from `card_select` | back to F3-A5; warm, not bright |
| the found cloud starting from a near-silent density | the first 150 ms silent: a sting that answers late | its first grain placed on the frame he finds it |
| the shatter's strain ticks at a third of the crack | 30 dB under the peak, inaudible | raised to about 20 dB under |

## Deliberately absent

Listed so nobody adds them by reflex:

- **Footsteps.** The player is always moving in this genre; footsteps would be the loudest
  thing in the mix by play count and carry no information.
- **A sound per poison or burn tick.** Status damage is continuous and would smear the mix.
  The application is audible; the ticks are not.
- **Per-projectile travel loops.** A dozen missiles in flight is a dozen loops.
- **A distinct hurt sound per enemy class.** Class identity is the silhouette's job
  (`.ai/art-direction.md` §4). Three seeded variants plus pitch randomisation cover it.
- **Recorded voice or foley of any kind.** Out of reach of pure synthesis, and out of scope. The two
  hints that suggest a person (`hint_cell_cry`, `hint_ice_song`) are formant synthesis - a buzz
  through a vowel's resonances, phrased like speech, with no words in it. The caption carries the
  words; a synthesised word would be worse than none.

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

**Generated, like everything else here** - `tools/audio/music.py`, under the contract in
`.ai/audio-direction.md` section 11. The two stock tracks this section used to list were removed
before the repo went public, for want of a licence (`.ai/asset-licensing.md`); what replaced
them is ours outright. Status as of 2026-10-22: **a menu theme and all nine chapters built and
wired**, each its own mode of the same seven notes, 104-168 to the beat (section 11 has the
per-chapter table: key, metre, tempo, palette). The boss loop is still shared.

| File | Length | RMS | Plays | Up when |
|---|---|---|---|---|
| `assets/music/menu_theme.wav` | 26.67 s | -32.0 dBFS | title, every menu | always, as a solo loop; 2 s in, crossfaded out over 3 s as a run starts |
| `assets/music/<chapter>_bed.wav` | 24.0-30.0 s | -33.0 dBFS | that chapter's runs | always while the set plays |
| `assets/music/<chapter>_pulse.wav` | same | -34.0 dBFS | that chapter's runs | 28+ enemies alive; out under 14 |
| `assets/music/<chapter>_lead.wav` | same | -35.0 dBFS | that chapter's runs | an elite, event elite or Warden alive, or pace minute 10+; held 30 s |
| `assets/music/boss_loop.wav` | 41.74 s | -29.5 dBFS | every boss fight | crossfaded in over 3 s as the boss arrives |

`<chapter>` is, in `StageCatalog` order: `forest` (25.71 s), `dungeon` (29.09), `cave` (27.00),
`swamp` (24.00), `ruins` (30.00), `frozen` (26.67), `sands` (27.69), `emberdeep` (25.60),
`sketchbook` (25.71). The three stems of a chapter are always the same length to the sample.

All 29 are 22050 Hz mono 16-bit, **33.4 MiB on disk** (the 28 rendered on 2026-10-22 are 31.6 MiB,
33.1 decimal MB; their blobs compress to about 25.5 MiB in git (loose objects went from 1.05 to 26.8 MiB on staging)), imported as QOA (`compress/mode=2`)
with `edit/loop_mode=2` (Forward) - about **6.7 MB in an export**, computed from the format.
Regenerate with `python tools/audio/music.py` (one piece at a time with a name: `music.py cave`);
measure with `music.py --report`, `analyse.py --music` and `mixsim.py --music --policy tiered`.
Audition reels are `music.py --journey`, written to the gitignored `tools/audio/_music_preview/`:
`<chapter>_journey.wav` (bed, then pulse, then lead, then the boss crossfading in),
`menu_journey.wav` (the theme twice, then a run starting) and `all_chapters_sampler.wav`.

### How it is wired

| Moment | Call | What happens |
|---|---|---|
| title screen, any menu | `MusicPlayer.PlayMenuMusic()` from `TitleScreen.StartTitle` and `MainMenu._Ready` | the menu theme as a solo loop, 2 s fade in; whatever else is playing fades out over the same 2 s. Idempotent - a theme already playing is faded back up, never restarted |
| run starts | `Node2DGame.PlayRunMusic` | `MusicCatalog.RunSetForStage` - the chapter's own set; 3 s fade in while the theme fades out over 3 s |
| every 0.25 s of play | `Node2DGame.UpdateRunMusic` | sets the pulse and lead targets; `MusicPlayer` fades them, in 4 s and out 8 s |
| boss arrives | `Node2DGame.PlayBossMusic` from `TickBossPhaseStart` | the set's `Boss` loop crossfades in over 3 s; the set stops once silent |
| boss falls | `CompleteBossVictory` | everything fades over 2.5 s, under `boss_death` |
| run ends, win or lose | `FinishRunAndReward` | the same fade, under `game_over` / `stage_clear` |
| quit to the menu mid-run | `TitleScreen` | `PlayMenuMusic` fades the set out under the theme; from a boss fight, the boss loop fades out on one solo player while the theme fades in on the other |

`MusicCatalog` is the only place a file is named. `RegressionChecks.ValidateMusic` warns at
startup for a missing file, a file that does not loop, a set whose stems differ in length
(which would drift them apart once a loop), a chapter in `StageCatalog` without a row in
`MusicCatalog.PerStage`, and a row for a chapter that does not exist. The Music slider is the bus
volume and `AudioSettings` owns it; nothing in the music path sets a `VolumeDb` against it.

### What the measurements say

All from 2026-10-22, on the 28 rendered files; section 11 of `.ai/audio-direction.md` has the
full tables.

- **Seams:** every loop's wrap is a smaller jump than the loop makes on its own (step 0.00-0.22,
  click energy 0.00-0.57 of the 99th percentile); every bed and pulse is within 2.4 dB of its
  median second at the seam, and the leads, most of which rest before the wrap, read -0.2 to
  -6.2 dB there on purpose.
- **Budget:** 0% of every stem's power under 80 Hz and 0% above 5 kHz, at most 2% in the
  2-5 kHz band, `hf` at most 2%. Beds 53-80% mid band, leads 68-100%, pulses 19-94% low (the
  dungeon's and the Emberdeep's are a big drum and a bass).
- **Under the effects** (`mixsim --music --policy tiered`): a median cast is +14.6 to +15.1 dB
  over a bed alone, +12.2 to +13.2 over bed and pulse, +10.1 over the boss loop and +7.9 to +8.7
  over a full late-game set; the music costs a late cast 1.4-1.8 dB of its headroom over the swarm,
  as the old Forest did. `player_hurt` stays 17-23 dB over it everywhere.
- **Inside each stem:** `music.py --balance` prints every voice's level; no ostinato is more than
  7 dB under its bed, no snare or tek more than 22 dB under its pulse.

### The castle's music (2026-10-30: rendered, not yet wired)

Piece C7 of `.ai/endgame-castle.md`: a set for each of the three new castle chapters, scored in its
own `tools/audio/music_<chapter>.py` (which `music.py` picks up by name) and rendered once into
`assets/music/`. The Cursed Dungeon, which moves into the castle, keeps its own set. Section 11 of
`.ai/audio-direction.md` has their rows in the chapter table and the rule they change (the Court and
the Throne take up his G sharp and B flat). **Nobody has heard them.**

| Files | Chapter | What it is | Length | RMS |
|---|---|---|---|---|
| `bridge_bed/pulse/lead.wav` | The Last Bridge, `stage_11` | a march under wind: A Aeolian, 2/4 at 120; low strings on the march figure under long low gusts (bed), a side drum, bass drum and oom-pah (pulse), horns in octaves then fifes (lead) | 24.00 s | -33 / -34 / -35 |
| `court_bed/pulse/lead.wav` | The Court, `stage_12` | a court dance with no dancers: an A minor minuet in 3/4 at 112; viols and a harpsichord's continuo (bed), a theorbo's bass, pah-pah and a tambour playing the minuet step's hemiola - the dancers' feet, with nobody dancing (pulse), recorder then viol (lead) | 25.71 s | -33 / -34 / -35 |
| `throne_bed/pulse/lead.wav` | The Throne, `stage_13` | the heaviest piece: A minor with B flat and G sharp, 4/4 at 104 in half time, a lament bass to E2; choir, strings, a brass pedal and a slow tread (bed), taiko, snare, square bass and a sixteenth-note motor (pulse), low brass then a chant (lead) | 27.69 s | -33 / -34 / -35 |

All nine are 22050 Hz mono 16-bit, 9.8 MB of WAV (about 2 MB in an export as QOA), each `.import`
written before the first import with `edit/loop_mode=2` (Forward) and `compress/mode=2`.

**Where they are wired, and by whom:**

- **`MusicCatalog`: the castle's plumbing agent (C0).** Three planned sets with the Emberdeep's as
  their stand-in, under these ids - `PlannedChapter("bridge", Emberdeep)`, `("court", ...)`,
  `("throne", ...)` - and rows `[11]`, `[12]`, `[13]` in `PerStage`. A planned set switches itself
  on when its bed is imported, so the moment C0's rows land these play; `ValidateMusic` checks the
  three stems are the same length (they are, to the sample) and loop.
- **The Last Bridge and the Court play as every chapter does** (`Node2DGame.PlayRunMusic`, the
  pulse and lead thresholds, the shared boss loop for the Constable and the Seneschal). Nothing more.
- **The Throne does not.** Whoever builds `ThroneRules` (C4) and the dark wizard (C5) between them:
  the set plays from the Muster on and **never hands over to the boss loop** - the whole chapter is
  the fight - so the Throne should not call `PlayBossMusic` (its `MusicSet.Boss` can stay the shared
  loop for `ValidateMusic`; it is simply never asked for). Bed alone under the Muster's level-ups
  (paused; the music runs in real time), the pulse and lead by the usual rule through the procession
  (the pinned pace clock keeps the lead up), all three for his first phase. **His second phase drops
  the pulse: `MusicPlayer.SetLayerTargets(pulse: 0, lead: 1)` when phase 2 begins** - the 8 s
  layer fade-out is the right speed for "the hall goes dark" - held against `Node2DGame.UpdateRunMusic`,
  which re-sets the layers every quarter second and would otherwise bring the pulse straight back;
  **phase 3 restores it** (`SetLayerTargets(1, 1)`, in over 4 s). His fall fades everything as a
  boss's does. No phase-2 file exists or is needed: every bit of the piece's drive is in the pulse,
  so the bed and lead are complete without it. `music.py --journey` writes
  `tools/audio/_music_preview/throne_journey.wav`: bed, pulse in, lead in, pulse out over 8 s, back
  over 4 s, the end - the switch, to listen to before it is built.

**Measured** (the same checks as every chapter; the scratch renders are byte-identical to the
committed files):

- **Seams** (`music.py --report`): step 0.00-0.24 and click 0.00-0.80 of each loop's own body, every
  bed and pulse within 2.3 dB of its median second at the wrap, the leads -3.0 to -3.4 dB there
  because each rests before it. The bridge's pulse is the clickiest seam in the set at 0.80: a side
  drum roll lands on the downbeat across it, which is the music, not a discontinuity (step 0.11).
- **Budget** (`analyse.py --music`): 0% under 80 Hz and above 5 kHz in all nine, at most 1% in the
  2-5 kHz band, hf at most 1%. Beds 51-70% mid band, leads 59-100% mid (the Throne's is 41% low, its
  brass doubled an octave down), pulses 65-85% low.
- **Under the effects** (`mixsim.py --music --policy tiered --sets bridge,court,throne`): a median
  cast +14.8 to +15.2 dB over a bed, +12.0 to +12.3 over bed and pulse, +7.5 to +8.3 over all three;
  `player_hurt` +16.3 to +21.7 dB over the music; the worst critical in the late game is still
  `elite_spawn` (-6.6 to -7.4 dB), the known redraw. The Throne, the heaviest, is the edge of the
  set: a late cast 0.4 dB nearer its music and `player_hurt` about a dB nearer than any road
  chapter's - still 16 dB clear. The Court's worst-tenth cast over bed and pulse (+6.8 dB) is the
  lowest of any chapter's (the road's are +7.4 to +8.7).
- **Inside each stem** (`--balance`): the bridge's gusts 7.5 dB under its bed and its side drum
  6.7 under its pulse; the Court's tambour 6.1 under its pulse; the Throne's tread 7.0 under its
  bed, its motor 6.7 and its snare 13.4 under its pulse.

### Known limitations

- **Nobody has heard it.** As with the effects, it was designed and measured, not auditioned.
  The first listen will move things; the knobs are `STEM_RMS_DB` (loudness), the event lists
  (the music), the velocities `music.py --balance` reports (the mix inside a stem) and the
  instrument functions (the timbre), all in `music.py`. Section 11 lists what to listen for.
- **The boss loop is shared.** Every chapter hands over to the same A minor loop at 138. The
  chapters run from 104 to 168, so the handover is a change of tempo as well as of music; whether
  that reads as a drop or a stumble needs an ear. A per-chapter boss loop costs about 1.8 MB of
  history each.
- **`AudioStreamSynchronized` is trusted to read its stream volumes live.** It is the engine's
  layered-music stream and is built for exactly this, but the crossfades have only been
  exercised headless, where the dummy audio driver plays nothing. If the pulse or lead never
  arrive by ear, that is the first thing to check.
