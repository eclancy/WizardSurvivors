# Spell and Passive Balance - Ongoing

Status: Open / Ongoing.

This is the living balance issue for all weapons, passive spells, and element threshold passives. Keep this file updated as numbers, unlock requirements, and playtest notes change.

## Goals
- Keep each weapon distinct enough to justify taking it.
- Keep passive spells useful without making active weapons feel secondary.
- Keep element threshold passives readable and impactful at 2 / 4 / 6 instances.
- Use achievements/unlocks for pacing once the current all-unlocked test pool is ready to be narrowed again.

## Open Balance Questions
- Which active spells are too similar in targeting pattern, cooldown, or damage profile?
- Which passive spells are must-picks, and which feel invisible?
- Should passive spells occupy the same 6 loadout slots as weapons long term?
- Should all-unlocked testing remain behind a debug flag before progression tuning resumes?
- Which achievement unlocks should be early, mid, and late progression rewards?

## Active Weapons
| Spell | Id | Elements | Base Damage | Cooldown | Projectiles | Range | Balance notes |
| --- | --- | --- | ---: | ---: | ---: | ---: | --- |
| Magic Missile | `magic_missile` | Arcane 1, Lightning 1 | 10 | 0.5 | 1 | 500 | Starter baseline; watch if high damage plus fast cooldown crowds out later weapons. |
| Arcane Explosion | `arcane_explosion` | Arcane 2 | 5 | 1.5 | 1 | 100 | Close AoE; tune against survivability and XP-growth synergy. |
| Spiritual Weapon | `spiritual_weapon` | Arcane 1, Light 1 | 8 | 1.0 | 2 | 100 | Persistent spectral blades; watch overlap with passive/reactive damage. |
| Fireball | `fireball` | Fire 2 | 6 | 2.0 | 1 | 400 | Slow heavy projectile with explosion; should feel like high-impact Fire builder. |
| Frost Shard | `frost_shard` | Ice 1, Water 1 | 4 | 1.2 | 1 | 450 | Piercing/slowing identity; compare against Glacial Spike and Cone of Cold. |
| Shadow Bolt | `shadow_bolt` | Darkness 1, Poison 1 | 3 | 1.3 | 1 | 450 | Low damage plus poison/darkness utility; verify DoT contribution is visible. |
| Thorn Vine | `thorn_vine` | Grass 1, Poison 1 | 3 | 1.4 | 1 | 400 | Line pierce/root-poison style; compare against Guardian Vines passive. |
| Gale Blade | `gale_blade` | Wind 1, Lightning 1 | 4 | 1.1 | 1 | 450 | Fast Wind/Lightning bridge; watch chain-scaling breakpoints. |
| Solar Flare | `solar_flare` | Light 1, Fire 1 | 4 | 2.0 | 1 | 90 | Short-range Light/Fire AoE; tune risk/reward around healing and proximity damage. |
| Molten Shard | `molten_shard` | Metal 1, Fire 1 | 5 | 1.3 | 1 | 450 | Piercing armor/fire hybrid; compare with Fireball for Fire damage identity. |
| Chain Lightning | `chain_lightning` | Lightning 2 | 4 | 1.5 | 1 | 450 | Dedicated Lightning builder; verify chain behavior is strong but not runaway. |
| Toxic Spore Burst | `toxic_spore_burst` | Poison 1, Grass 1 | 3 | 2.2 | 1 | 90 | Poison AoE cloud; should reward dense enemy packs. |
| Obsidian Spike | `obsidian_spike` | Earth 1, Darkness 1 | 7 | 1.8 | 1 | 400 | Heavy single-target/ground spike; compare against Glacial Spike. |
| Cyclone Slash | `cyclone_slash` | Wind 2 | 3 | 1.0 | 2 | 60 | Dedicated close-range Wind builder; watch melee safety with Wind speed. |
| Void Lance | `void_lance` | Arcane 1, Darkness 1 | 6 | 1.6 | 1 | 500 | Long-range pierce; should feel precise and high-value. |
| Glacial Spike | `glacial_spike` | Ice 2 | 7 | 2.0 | 1 | 450 | Dedicated Ice heavy hit; compare against Frost Shard's frequency and Cone of Cold's AoE. |
| Black Tentacles | `black_tentacles` | Poison 1, Earth 1 | 3 | 4.0 | 1 | 400 | Stationary control AoE; cooldown likely needs playtest attention. |
| Cone of Cold | `cone_of_cold` | Ice 2 | 5 | 2.2 | 1 | 150 | Directional control burst; should be visibly different from Glacial Spike. |
| Scorching Ray | `scorching_ray` | Fire 1, Arcane 1 | 3 | 1.6 | 3 | 450 | Multi-bolt Fire/Arcane option; tune around projectile count upgrades. |
| Meteor Swarm | `meteor_swarm` | Fire 2, Earth 1 | 9 | 5.0 | 1 | 450 | Late/high-impact spell; cooldown and warning timing are key balance levers. |

## Passive Spells
| Passive | Id | Elements | Base Cooldown | Effect | Balance notes |
| --- | --- | --- | ---: | --- | --- |
| Aegis Ward | `aegis_ward` | Metal 1, Light 1 | 10.0 | Periodically grants an absorbing shield. | Watch shield uptime with Metal flat reduction and Darkness percent reduction. |
| Thornmail Barrier | `thornmail_barrier` | Earth 1, Grass 1 | 1.0 | Retaliates against nearby enemies when hit. | Needs enough feedback to justify taking damage-adjacent power. |
| Frozen Bulwark | `frozen_bulwark` | Ice 2 | 1.0 | Chance to freeze/root nearby attackers when hit. | Dedicated Ice passive; watch freeze reliability at high enemy density. |
| Stormguard Aura | `stormguard_aura` | Lightning 1, Metal 1 | 1.0 | Strikes the nearest enemy with lightning when hit. | Reactive damage can scale sharply with defensive builds. |
| Venom Cloak | `venom_cloak` | Poison 1, Darkness 1 | 2.5 | Periodically poisons nearby enemies. | Compare with Toxic Spore Burst; avoid passive AoE doing all clear work. |
| Guardian Vines | `guardian_vines` | Grass 2 | 6.0 | Periodically roots nearby enemies. | Dedicated Grass passive; root timing and duration need feel testing. |
| Tidal Barrier | `tidal_barrier` | Water 1, Wind 1 | 5.0 | Periodically knocks back and slows nearby enemies. | Strong defensive utility; watch overlap with Ice slow and Wind movement. |
| Stone Bulwark | `stone_bulwark` | Earth 1, Metal 1 | 1.0 | Passively reduces incoming damage. | Stack interaction with Earth HP, Metal armor, Darkness reduction is a key risk. |
| Blur | `blur` | Arcane 1, Wind 1 | 1.0 | Chance to avoid incoming hits entirely. | Dodge chance should stay understandable and not erase damage tension. |
| Fortune's Favor | `fortunes_favor` | Arcane 1, Light 1 | 1.0 | Passively boosts Luck. | Confirm Luck effects are visible enough: crit, legendary chance, bonus drops. |
| Haste | `haste` | Wind 1, Lightning 1 | 8.0 | Periodically grants attack-speed and move-speed surges. | Tune burst uptime against Wind movement and attack-speed meta upgrades. |

## Element Threshold Passives
| Element | 2 instances | 4 instances | 6 instances | Balance notes |
| --- | --- | --- | --- | --- |
| Fire | +10% damage to nearby enemies | +20% damage to nearby enemies | +35% damage to nearby enemies | Proximity condition should matter; verify range communicates well. |
| Ice | 10% slow for 2s on hit | 20% slow for 2s on hit | 35% slow for 2s on hit | Watch stacking-resistant slow feel on fast enemies. |
| Arcane | +10% XP gained | +20% XP gained | +35% XP gained | XP growth can snowball; tune with level curve. |
| Darkness | -10% incoming damage | -20% incoming damage | -35% incoming damage | Multiplicative/flat stacking with Metal and Stone Bulwark needs testing. |
| Light | Heal 3% of damage dealt | Heal 6% of damage dealt | Heal 10% of damage dealt | Strong with high-AoE builds; verify overheal does not occur. |
| Grass | +1 HP/sec regeneration | +2 HP/sec regeneration | +4 HP/sec regeneration | Compare against recovery meta upgrades and Frostweaver regen. |
| Earth | +20 max HP | +50 max HP | +100 max HP | HP jumps should update HUD and current HP cleanly when elements change. |
| Wind | +10% move speed | +20% move speed | +35% move speed | Avoid making player control too slippery at tier 6. |
| Lightning | 10% chance to chain a bolt | 20% chance to chain a bolt | 35% chance to chain a bolt | Watch recursive/secondary damage limits and visual clarity. |
| Poison | +2 poison damage/tick | +4 poison damage/tick | +8 poison damage/tick | DoT should be meaningful without invalidating direct damage. |
| Metal | -1 flat damage taken | -2 flat damage taken | -4 flat damage taken | Flat reduction can trivialize low-damage enemies; test late scaling. |
| Water | -5% spell cooldowns | -10% spell cooldowns | -18% spell cooldowns | Cooldown reduction stacks with shop/meta and Haste; watch caps. |

## Achievement Unlock Hooks
| Achievement | Requirement | Unlock reward | Notes |
| --- | --- | --- | --- |
| First Blood | Defeat at least one enemy in a run | Fireball | Very early unlock candidate. |
| Survivor | Survive for 10 minutes | Frost Shard | Early/mid survivability gate. |
| Veteran | Survive for 20 minutes | Meteor Swarm | Late unlock candidate. |
| Archmage Training | Reach level 20 in a run | Solar Flare | XP/build quality gate. |
| Elementalist | Reach 4 instances of any element | Chain Lightning | Encourages element stacking. |
| Full Grimoire | Fill all 6 spell slots | Void Lance | Encourages full-loadout play. |
| Untouchable | Survive 5 minutes without taking damage | Blur | Skill gate. |
| Fire Adept | End a run with 4 Fire instances | Scorching Ray | Element-specific unlock. |
| Ice Adept | End a run with 4 Ice instances | Cone of Cold | Element-specific unlock. |
| Poison Adept | End a run with 4 Poison instances | Toxic Spore Burst | Element-specific unlock. |
| Earth Adept | End a run with 4 Earth instances | Obsidian Spike | Element-specific unlock. |
| Wind Adept | End a run with 4 Wind instances | Gale Blade | Element-specific unlock. |
| Forest Cleared | Defeat the forest boss | Thorn Vine | Boss/stage unlock. |
| Castle Conqueror | Defeat the castle boss | Shadow Bolt | Boss/stage unlock. |
| Ruins Delver | Defeat the ruins boss | Black Tentacles | Boss/stage unlock. |

## Playtest Notes
- Add dated notes here after each pass.
- Record loadout, stage, time survived, final level, strongest/weakest picks, and any element thresholds reached.

## Change Log
- 2026-07-19: Created living issue from current runtime catalog, spell resources, passive spell catalog, element threshold descriptions, and achievement definitions.