# Spell Evolution System - Quick Reference

## TL;DR
- 16 spells have custom Lv4 (3 choices) + Lv8 (2 choices) evolution branches
- All other spells get generic fallback evolutions automatically
- Evolutions modify stats, apply visuals, and trigger new mechanics
- UI milestone modal lets players pick evolution branches at Lv4 & Lv8

---

## For Players

### What Are Evolutions?
At Level 4 and Level 8, your spells level up with **choices** instead of auto-scaling:
- **Level 4 Mutation**: Pick 1 of 3 mechanical modifications (faster, bigger, more pierce, etc.)
- **Level 8 Ascension**: Pick 1 of 2 build-defining transformations (massive damage, speed builds, control builds)

### How to Pick an Evolution
1. Spell reaches Level 4 or 8
2. Level-up menu shows the spell with a blue border
3. **Click the spell** → Evolution selection screen appears
4. **Choose one card** → Evolution is applied and spell levels up
5. Spell now fires with the evolution's mechanics and visuals

### Synergy Tags
Each evolution has a **Synergy Tag** (e.g., "Arcane / Speed") suggesting which passives pair well:
- Pick evolutions that share synergy themes with your equipped passives
- Example: Fire + Burn damage passives combo with "Fire / Zone" evolutions

---

## For Developers

### Key Files
| File | Purpose |
|------|---------|
| `SpellEvolutionCatalog.cs` | Defines all evolution options (16 spells + fallback) |
| `SpellEvolutionOption.cs` | Data model: name, description, stat multipliers, effects, colors |
| `SpellData.cs` | Stores `Level4Options`, `Level8Options`, selected evolution state |
| `LevelUpMenu.cs` | UI: displays evolution cards, handles selection |
| `Player.cs` | Catalog initialization, milestone detection, evolution application |

### Adding Evolutions to a Spell

**1. Open `SpellEvolutionCatalog.cs`**

**2. Find or add handler for your spell:**
```csharp
case "your_spell_id":
    SetupYourSpellEvolutions(spell);
    break;
```

**3. Implement the setup method:**
```csharp
private static void SetupYourSpellEvolutions(SpellData spell)
{
    // Level 4: 3 mutations
    spell.Level4Options.Add(new SpellEvolutionOption
    {
        Id = "your_spell_mutation_1",
        DisplayName = "Mutation Name",
        Description = "What this mutation does.",
        MilestoneLevel = 4,
        DamageMultiplier = 1.5f,      // +50% damage
        PierceBonus = 2,               // +2 pierce
        ModulateColor = new Color(1.0f, 0.8f, 0.3f),  // Gold tint
        SynergyTag = "Fire / Speed",
        SynergyDescription = "Works with fire passives."
    });
    
    // ... 2 more for Level 4 ...
    
    // Level 8: 2 ascensions
    spell.Level8Options.Add(new SpellEvolutionOption
    {
        Id = "your_spell_ascension_1",
        DisplayName = "Ascension Name",
        Description = "Massive transformation.",
        MilestoneLevel = 8,
        DamageMultiplier = 3.0f,
        AreaMultiplier = 2.0f,
        ScaleMultiplier = 1.8f,
        ModulateColor = new Color(1.0f, 0.9f, 0.4f),
        SynergyTag = "Fire / Cataclysm",
        SynergyDescription = "Ascension: Total inferno."
    });
    
    // ... 1 more for Level 8 ...
}
```

### Available Stat Modifiers

**Multipliers** (scale all instances of that stat):
- `DamageMultiplier` (float)
- `CooldownMultiplier` (float)
- `AreaMultiplier` (float)
- `SpeedMultiplier` (float)
- `ScaleMultiplier` (float - visual size)

**Bonuses** (add flat amounts):
- `DamageBonus` (int)
- `RangeBonus` (float)
- `PierceBonus` (int)
- `ChainArcBonus` (int)
- `ProjectileCountBonus` (int)
- `SlowMagnitudeBonus` (float)
- `PoisonTickBonus` (int)
- `KnockbackBonus` (float)

**Effects** (trigger new mechanics):
- `Effect = SpellEffect.ExplosionOnHit` → Splash damage on hit
- `Effect = SpellEffect.VortexPull` → Inverts knockback to pull
- `Effect = SpellEffect.SpawnMinions` → Summons projectiles
- `Effect = SpellEffect.Lifesteal` → Heal on damage
- `Effect = SpellEffect.CritChance` → Crit multiplier bonus
- Set `EffectValue` for magnitude

**Visuals**:
- `ModulateColor` - Tint the spell (e.g., `new Color(1.0f, 0f, 0f)` = red)
- `VisualTag` - Semantic tag for custom animations (future)
- `ScaleMultiplier` - Size multiplier (1.5f = 50% bigger)

### How Evolutions Apply to Spell Behavior

**In Spell Scripts** (e.g., `MagicMissile.cs`):

```csharp
// Get stats accounting for evolution bonuses
int damage = Mathf.RoundToInt(SpellData.GetDamageAtLevel(level) * DamageMultiplier);
int pierce = SpellData.GetPierceAtLevel(level);  // Includes SelectedLevel4Evolution.PierceBonus
float area = SpellData.GetRangeAtLevel(level) * AreaMultiplier * SpellData.GetAreaMultiplierAtLevel(level);

// Check for effect flags
if (SpellData.HasEffectFlag(SpellEffect.ExplosionOnHit))
{
    // Do splash damage logic
}

// Apply visual mutations (handled in Player.cs ApplyLegendaryVisual)
// Spell modulate color = SpellData.GetModulateColor()
// Spell scale *= SpellData.GetScaleMultiplier()
```

**In Data Getters** (e.g., `SpellData.cs`):

```csharp
public int GetPierceAtLevel(int level)
{
    int pierce = (int)MathF.Round(GetEffectValueAtLevel(SpellEffect.Pierce, level));
    if (level >= 4 && SelectedLevel4Evolution != null)
        pierce += SelectedLevel4Evolution.PierceBonus;  // Evolution bonus
    if (level >= 8 && SelectedLevel8Evolution != null)
        pierce += SelectedLevel8Evolution.PierceBonus;  // Stacks
    return Math.Max(0, pierce);
}
```

### Wiring an Effect Flag to Spell Logic

**1. Add the enum value in `SpellEffect.cs`:**
```csharp
public enum SpellEffect
{
    // ... existing effects ...
    MyNewEffect = 999
}
```

**2. Add handler in the spell script that should react to it:**
```csharp
private void OnHit(Node target)
{
    if (SpellData != null && SpellData.HasEffectFlag(SpellEffect.MyNewEffect))
    {
        // Do something special
    }
}
```

**3. Reference it in evolution definitions:**
```csharp
new SpellEvolutionOption
{
    Id = "spell_my_effect",
    DisplayName = "Special Mutation",
    Effect = SpellEffect.MyNewEffect,
    EffectValue = 100f,  // Magnitude/amount
    // ...
}
```

### Testing an Evolution

1. **Build**: `dotnet build` (should succeed with 0 errors)
2. **Launch**: Godot editor, play game scene
3. **Test**: 
   - Pick the spell you modified
   - Level it to Lv4 → Verify your 3 mutations show with correct names, colors, descriptions
   - Pick one mutation → Verify spell applies the stat bonuses in-game
   - Level to Lv8 → Verify your 2 ascensions show
   - Pick one → Verify stacking behavior

---

## Data Structure

### SpellEvolutionOption (Resource)
```csharp
public partial class SpellEvolutionOption : Resource
{
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public int MilestoneLevel { get; set; }  // 4 or 8
    
    // Stat modifiers
    [Export] public float DamageMultiplier { get; set; } = 1.0f;
    [Export] public int DamageBonus { get; set; }
    [Export] public float CooldownMultiplier { get; set; } = 1.0f;
    [Export] public float AreaMultiplier { get; set; } = 1.0f;
    [Export] public float SpeedMultiplier { get; set; } = 1.0f;
    // ... (see SpellEvolutionOption.cs for full list)
    
    // Visual
    [Export] public Color ModulateColor { get; set; } = Colors.White;
    [Export] public float ScaleMultiplier { get; set; } = 1.0f;
    
    // Synergy messaging
    [Export] public string SynergyTag { get; set; }
    [Export] public string SynergyDescription { get; set; }
}
```

### LevelUpOption (in SpellData context)
```csharp
public class LevelUpOption
{
    public bool IsEvolutionMilestone { get; set; }      // True at Lv4/8
    public int MilestoneLevel { get; set; }              // 4 or 8
    public List<SpellEvolutionOption> EvolutionChoices { get; set; }  // 3 or 2 options
    // ... (other level-up fields)
}
```

### UI Flow Signal
```
Player clicks evolution card
  ↓
LevelUpMenu.OnEvolutionChosen(option, evo)
  ↓
Emits: WeaponSelected signal with "{spellId}:{evolutionId}"
  ↓
Node2DGame.OnWeaponSelected() parses payload
  ↓
Player.TryAddOrLevelSpell("{spellId}:{evolutionId}")
  ↓
Spell levels up + ApplyEvolution(evo) called
```

---

## Troubleshooting

| Issue | Cause | Fix |
|-------|-------|-----|
| Spell shows no evolutions at Lv4 | Not in catalog switch case | Add case and handler in `SetupDefaultArchetypeEvolutions` called instead |
| Evolutions don't apply stats | Spell script doesn't call getter | Add `GetPierceAtLevel()`, `GetAreaMultiplierAtLevel()`, etc. |
| Visual tint not showing | Evolution ModulateColor is white | Set `ModulateColor` to non-white value in evolution definition |
| Compilation errors | Missing fields on SpellEvolutionOption | Check `SpellEvolutionOption.cs` for all [Export] properties |
| UI crashes clicking evolution | EvolutionChoices list is null | Check `GetEvolutionOptionsForLevel()` fallback logic in Player.cs |

