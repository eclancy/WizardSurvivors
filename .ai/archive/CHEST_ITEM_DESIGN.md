# Expanded Chest Item System Design

## Item Roster (25 items)

### Category: Damage (6 items)
1. **Relic Key** [EXISTING]
   - Magnet Range +18
   - ID: relic_key

2. **Ember Flask** [EXISTING]
   - Spell Damage +12%
   - ID: ember_flask

3. **Wrath Amulet** [NEW]
   - Spell Damage +8%
   - ID: wrath_amulet

4. **Ethereal Blade** [NEW]
   - Critical Damage Multiplier +0.3x
   - ID: ethereal_blade

5. **Spectral Fang** [NEW]
   - Spell Damage +15% to enemies below 50% HP
   - ID: spectral_fang

6. **Obsidian Heart** [NEW]
   - Spell Damage +10%, but -5% Movement Speed
   - ID: obsidian_heart

### Category: Defense (6 items)
1. **Aegis Sigil** [EXISTING]
   - Damage Reduction +8%
   - ID: aegis_sigil

2. **Iron Fang** [EXISTING]
   - Damage Reduction +6%
   - ID: iron_fang

3. **Basalt Carapace** [NEW]
   - Damage Reduction +10%
   - ID: basalt_carapace

4. **Aegis Crown** [NEW]
   - Max HP +30
   - ID: aegis_crown

5. **Ironhide Cloak** [NEW]
   - Damage Reduction +7%, shields from crits
   - ID: ironhide_cloak

6. **Protective Ward** [NEW]
   - Shields grant +15% Damage Reduction while active
   - ID: protective_ward

### Category: Healing & Recovery (4 items)
1. **Vial of Vitality** [NEW]
   - Healing received +20%
   - ID: vial_of_vitality

2. **Heart of Renewal** [NEW]
   - Max HP +50, Regeneration +0.5/sec
   - ID: heart_of_renewal

3. **Phylactery** [NEW]
   - On death, restore 25% HP (once per stage)
   - ID: phylactery

4. **Essence Chalice** [NEW]
   - Gain 1 HP for each enemy killed (stacking)
   - ID: essence_chalice

### Category: Utility (4 items)
1. **Quicksilver Pendant** [NEW]
   - Movement Speed +15%
   - ID: quicksilver_pendant

2. **Haste Rune** [NEW]
   - Attack Speed +12%
   - ID: haste_rune

3. **Compass Rose** [NEW]
   - XP gain +15%
   - ID: compass_rose

4. **Lucky Coin** [NEW]
   - Item drop rates +20%
   - ID: lucky_coin

### Category: Elemental (5 items)
1. **Storm Lattice** [EXISTING]
   - Critical Chance +8%
   - ID: storm_lattice

2. **Inferno Core** [EXISTING]
   - Area of Effect +12%
   - ID: inferno_core

3. **Frozen Tear** [NEW]
   - Ice duration +40%, slow enemies by additional 15%
   - ID: frozen_tear

4. **Thunderstone** [NEW]
   - Lightning chain radius +50%, chain count +1
   - ID: thunderstone

5. **Crystal Prism** [NEW]
   - All elemental effects +20% potency
   - ID: crystal_prism

---

## Synergy Sets (10 sets, 2-3 items each)

### Set 1: Vaultguard (3 items) [EXISTING]
- **Items**: Relic Key, Aegis Sigil, Iron Fang
- **Bonus**: Damage Reduction +12%, Shield +4 on chest open
- **ID**: vaultguard

### Set 2: Emberline (3 items) [EXISTING]
- **Items**: Ember Flask, Inferno Core, Relic Key
- **Bonus**: Spell Damage +18%, Area +12%
- **ID**: emberline

### Set 3: Stormbound (3 items) [EXISTING]
- **Items**: Storm Lattice, Inferno Core, Aegis Sigil
- **Bonus**: Critical Chance +10%, Movement Speed +10%
- **ID**: stormbound

### Set 4: Bastion of Spikes (3 items) [EXISTING]
- **Items**: Aegis Sigil, Iron Fang, Ember Flask
- **Bonus**: Damage Reduction +15%, Retaliation enabled
- **ID**: bastion_of_spikes

### Set 5: Deathbringer (2 items) [NEW]
- **Items**: Wrath Amulet, Spectral Fang
- **Bonus**: Spell Damage +25%, execute enemies below 30% HP
- **ID**: deathbringer

### Set 6: Eternal Guardian (3 items) [NEW]
- **Items**: Aegis Crown, Basalt Carapace, Protective Ward
- **Bonus**: Max HP +80, Damage Reduction +18%
- **ID**: eternal_guardian

### Set 7: Life Drain (2 items) [NEW]
- **Items**: Essence Chalice, Heart of Renewal
- **Bonus**: Kill streak: +1 damage per kill (max +50)
- **ID**: life_drain

### Set 8: Elemental Mastery (3 items) [NEW]
- **Items**: Crystal Prism, Frozen Tear, Thunderstone
- **Bonus**: All elemental effects +40%, chain reactions activate
- **ID**: elemental_mastery

### Set 9: Speed Demon (2 items) [NEW]
- **Items**: Quicksilver Pendant, Haste Rune
- **Bonus**: Movement Speed +20%, Attack Speed +15%
- **ID**: speed_demon

### Set 10: Fortune's Favor (2 items) [NEW]
- **Items**: Lucky Coin, Compass Rose
- **Bonus**: Chest drop rate +25%, XP gain +25%
- **ID**: fortunes_favor

---

## Implementation Plan

### Phase 1: Update ChestItemCatalog.cs
- Add all 19 new item IDs as constants
- Update AllItemIds array to include all 25 items
- Add 6 new synergy sets to Sets array
- Expand GetDisplayName() switch statement
- Expand GetDescription() switch statement
- Expand GetIconPath() switch statement

### Phase 2: Update Player.cs
Add new stat fields:
- `private float chestHealingBonusPercent = 0f;` (for healing items)
- `private float chestRegenPerSecond = 0f;` (for regen items)
- `private int chestMaxHpBonus = 0;` (for max HP items)
- `private float chestCooldownMultiplier = 1.0f;` (for attack speed modifiers)
- `private float chestXpBonusPercent = 0f;` (for XP items)
- `private int chestExecuteThreshold = 0;` (for execute mechanics)
- `private float chestElementalPotencyBonus = 0f;` (for elemental scaling)
- `private bool chestPhylacteryActive = true;` (for phylactery one-time use)

### Phase 3: Implement Item Effects
- ApplyChestItemEffect() to handle all new items
- RefreshChestSetEffects() to handle all new synergy bonuses

### Phase 4: Balance & Polish
- Validate no item is over/underpowered
- Test synergy interactions
- Ensure asset paths are correct
