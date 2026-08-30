# Chest Item Effects - Implementation Reference

## How to Extend the System

### Adding a New Item

1. **Add constant to ChestItemCatalog.cs:**
```csharp
public const string NewItemId = "new_item_id";
```

2. **Add to AllItemIds array:**
```csharp
public static readonly IReadOnlyList<string> AllItemIds = new[]
{
    // ... existing items ...
    NewItemId  // Add here
};
```

3. **Add display name:**
```csharp
NewItemId => "New Item Display Name",
```

4. **Add description:**
```csharp
NewItemId => "Passively increases stat by +X%.",
```

5. **Add icon path:**
```csharp
NewItemId => "res://assets/organized/[type]/[filename].png",
```

6. **Add effect in Player.cs:**
```csharp
case ChestItemCatalog.NewItemId:
    // Apply the bonus to relevant stat field
    chestSomeBonus += 0.10f;
    break;
```

7. **Add getter if new stat field:**
```csharp
public float GetChestNewBonus() => chestNewBonus;
```

### Adding a New Synergy Set

1. **Add constant to ChestItemCatalog.cs:**
```csharp
public const string NewSetSetId = "new_set";
```

2. **Add to Sets array:**
```csharp
new ChestSetDefinition
{
    Id = NewSetSetId,
    Name = "Set Display Name",
    RequiredItemIds = new[] { ItemId1, ItemId2 },
    Description = "Synergy description text."
}
```

3. **Add effect in Player.cs RefreshChestSetEffects:**
```csharp
case ChestItemCatalog.NewSetSetId:
    chestBonusPercent += 0.20f;
    break;
```

---

## Item Effect Implementation Patterns

### Simple Flat Bonuses
```csharp
// Pattern: chestStatBonus += value
case ChestItemCatalog.WrathAmulet:
    chestDamageBonusPercent += 0.08f;
    break;
```

### Percentage-Based Multipliers
```csharp
// Pattern: stat *= multiplier
case ChestItemCatalog.QuicksilverPendant:
    Speed *= 1.15f;  // +15%
    break;
```

### Max Value Overrides
```csharp
// Pattern: SetMax then increment current
case ChestItemCatalog.AegisCrown:
    chestMaxHpBonus += 30;
    MaxHP += 30;
    CurrentHP += 30;  // Instantly heal
    break;
```

### Toggle Flags
```csharp
// Pattern: Set boolean flag
case ChestItemCatalog.Phylactery:
    chestPhylacteryActive = true;
    break;
```

### Trade-off Effects
```csharp
// Pattern: Increase one stat, decrease another
case ChestItemCatalog.ObsidianHeart:
    chestDamageBonusPercent += 0.10f;  // +10% damage
    Speed *= 0.95f;                     // -5% speed
    break;
```

### Dynamic Thresholds
```csharp
// Pattern: Store threshold for runtime checking
case ChestItemCatalog.SpectralFang:
    chestExecuteThresholdPercent = 50;  // 50% HP enemies
    break;
```

### Stat Field Accumulation
```csharp
// Pattern: Multiple items contribute to same stat
// Wrath Amulet: +8%
// Ember Flask: +12%
// Total if both collected: +20%
// (handled by += in each case)
```

---

## Integration Points

### Damage Calculation (Player.cs DealDamageToEnemy)
```csharp
// Existing code that uses chest bonuses:
float chestCritChance = GetChestCritBonusChance();
bool isCrit = combatRng.Randf() < (GetTotalCritChance() + bonusCritChance + chestCritChance);

float fireBonus = GetFireDamageBonusPercent();
finalDamage = Mathf.RoundToInt(finalDamage * (1.0f + GetChestDamageBonusPercent()));

// Would need to add for execute/spectral fang:
// if (enemyHealthPercent < GetChestExecuteThresholdPercent()) { ... }
```

### Health Regen Calculation (Player.cs _Process)
```csharp
// Added chest regen to effective regen calculation:
float effectiveRegenPerSecond = BaseHealthRegenPerSecond + recoveryPerSecond 
    + GetGrassRegenPerSecond() + GetPassiveSpellRegenPerSecond() + chestRegenPerSecond;
```

### Movement Speed (Player.cs _PhysicsProcess)
```csharp
// Speed is used directly from field
// Quicksilver Pendant applies Speed *= 1.15f in ApplyChestItemEffect
// Stormbound synergy adds chestMoveSpeedBonusPercent used in speed calc
```

### Attack Speed (Player.cs FireSpells)
```csharp
// Would use attackSpeedMultiplier which is set from:
// attackSpeedMultiplier = 1.0f + (attackSpeedLevel * 0.05f) + chestAttackSpeedBonusPercent
```

### Area of Effect (Player.cs SpellFireLoop)
```csharp
// Uses areaMultiplier which should include:
// areaMultiplier *= (1.0f + GetChestAreaBonusPercent())
```

---

## Testing Recommendations

### Unit Tests
```csharp
// Test item addition
[Test]
public void AddChestItem_WrathAmulet_IncreasesDamage()
{
    player.AddChestItem(ChestItemCatalog.WrathAmulet);
    Assert.AreEqual(0.08f, player.GetChestDamageBonusPercent());
}

// Test synergy completion
[Test]
public void AddChestItem_CompleteDeathbringer_BonusApplied()
{
    player.AddChestItem(ChestItemCatalog.WrathAmulet);
    player.AddChestItem(ChestItemCatalog.SpectralFang);
    Assert.AreEqual(0.25f, player.GetChestDamageBonusPercent());
}

// Test stat stacking
[Test]
public void AddChestItem_MultipleDefense_StackCorrectly()
{
    player.AddChestItem(ChestItemCatalog.AegisSigil);      // +8%
    player.AddChestItem(ChestItemCatalog.IronFang);        // +6%
    player.AddChestItem(ChestItemCatalog.BasaltCarapace);  // +10%
    Assert.AreEqual(0.24f, player.GetChestDamageReductionPercent());
}
```

### Integration Tests
```csharp
// Test damage calculation with bonuses
[Test]
public void DealDamageToEnemy_WithChestBonuses_CorrectDamage()
{
    player.AddChestItem(ChestItemCatalog.EmberFlask);
    int baseDamage = 10;
    int actualDamage = player.DealDamageToEnemy(enemy, baseDamage);
    Assert.AreEqual(12, actualDamage);  // 10 * 1.12
}

// Test synergy interactions
[Test]
public void Synergy_ElementalMastery_EnhancesElementals()
{
    player.AddChestItem(ChestItemCatalog.CrystalPrism);
    player.AddChestItem(ChestItemCatalog.FrozenTear);
    player.AddChestItem(ChestItemCatalog.Thunderstone);
    
    Assert.AreEqual(0.40f, player.GetChestElementalPotencyBonus());
    Assert.AreEqual(0.40f, player.GetChestIceDurationBonus());
}
```

### Manual Testing Checklist
- [ ] Open chest UI and verify all 25 items display
- [ ] Verify each item's description is correct
- [ ] Verify each item's icon loads properly
- [ ] Collect single items and verify stat changes
- [ ] Collect synergy items in order and verify bonus applies
- [ ] Collect two separate synergies and verify both work
- [ ] Test stat stacking (collect 3 similar items)
- [ ] Test trade-off items (Obsidian Heart: damage vs speed)
- [ ] Test HP bonuses (Aegis Crown increases max HP)
- [ ] Test regeneration (Heart of Renewal ticks health)
- [ ] Test movement speed changes (feel speed differences)
- [ ] Test attack speed changes (feel fire rate differences)

---

## Performance Considerations

### Stat Field Count (17 fields)
- **Memory**: ~68 bytes (17 floats/ints)
- **Impact**: Negligible, one instance per player

### Method Call Overhead
```csharp
// Getter methods are 1-line, inline-optimized:
public float GetChestDamageBonusPercent() => chestDamageBonusPercent;
```
- No allocation, minimal overhead
- Called once per damage calc (thousands of times per run)

### Switch Statement Size
```csharp
// 25-item switch in ApplyChestItemEffect
// Called once per item addition (typically 0-10 items per run)
// Not performance-critical path
```

### Best Practices
1. ✅ Use simple getters (not computed)
2. ✅ Accumulate bonuses with += (not recalculate)
3. ✅ Cache stat values when used multiple times per frame
4. ✅ Avoid divisions in per-frame paths

---

## Debugging Tips

### Check if item effect applied:
```csharp
GD.Print($"Damage bonus: {player.GetChestDamageBonusPercent()}");
```

### Verify synergy detection:
```csharp
var sets = ChestItemCatalog.GetAssociatedSets("spectral_fang");
GD.Print($"Associated synergies: {string.Join(", ", sets.Select(s => s.Name))}");
```

### Check set completion:
```csharp
var ownedItems = player.GetOwnedChestItems();
foreach (var set in ChestItemCatalog.Sets)
{
    var (owned, total) = ChestItemCatalog.GetSetProgress(set, ownedItems);
    GD.Print($"{set.Name}: {owned}/{total}");
}
```

### Validate item catalog:
```csharp
foreach (var itemId in ChestItemCatalog.AllItemIds)
{
    var name = ChestItemCatalog.GetDisplayName(itemId);
    var desc = ChestItemCatalog.GetDescription(itemId);
    var icon = ChestItemCatalog.GetIconPath(itemId);
    
    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(icon))
        GD.PrintErr($"Missing data for {itemId}");
}
```

---

