using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

[GlobalClass]
public partial class SpellData : Resource
{
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string Name { get; set; } = string.Empty;
    [Export] public int CurrentLevel { get; set; } = 1;
    [Export] public int MaxLevel { get; set; } = 8;
    [Export] public int BaseDamage { get; set; } = 1;
    [Export] public float BaseCooldown { get; set; } = 1.0f;
    [Export] public int BaseProjectileCount { get; set; } = 1;
    [Export] public float BaseRange { get; set; } = 0.0f;
    [Export] public Godot.Collections.Array<SpellLevelUpgrade> LevelUpgrades { get; set; } = new();
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;

    // Explicit spell classification for behavior-aware UI/balancing.
    [Export] public SpellTargetingMode TargetingMode { get; set; } = SpellTargetingMode.Auto;
    [Export] public SpellDamageShape DamageShape { get; set; } = SpellDamageShape.Auto;
    [Export(PropertyHint.Flags, "Damage,Cooldown,Area,Range,ProjectileCount,ProjectileSpeed,Pierce,Chain,Crit,Slow,Root,Knockback,Dot,Duration")]
    public int ScalingTagsMask { get; set; } = 0;

    // Element tags for this spell (issue #13). Keys are Element enum names (e.g. "Arcane"),
    // values are the weight this spell contributes toward that element's instance count.
    // Most spells have 1-2 entries with weight 1; a few are double-weighted on a single element.
    [Export] public Godot.Collections.Dictionary<string, int> ElementWeights { get; set; } = new();

    // Legendary variant (issue #24): rolled once per runtime instance when the spell is first
    // added to the loadout (see Player.TryAddOrLevelSpell), persists through level-ups/swaps just
    // like CurrentLevel since equipped spells are already per-instance Duplicate()s of the catalog
    // template. Doubles every element tag's weight (see GetElementWeights()) and is shown with a
    // gold Modulate tint on its visual (see Player.ApplyLegendaryVisual) until real art exists.
    [Export] public bool IsLegendary { get; set; } = false;

    // True for the code-only defensive/passive spells created via Player.CreateDefensiveSpellData
    // (issue #22's PassiveSpellEffect roster). Lets UI code (e.g. LevelUpMenu's elemental tag
    // section) distinguish "this element tag belongs to an equipped passive ability" from active
    // offensive spells, which don't get stronger from element tier bonuses the same way.
    [Export] public bool IsPassive { get; set; } = false;

    // Optional icon shown on the level-up card (LevelUpMenu). Left null for most spells (no unique
    // art yet, #30) - LevelUpMenu falls back to a shared default icon in that case.
    [Export] public Texture2D Icon { get; set; }

    public Dictionary<Element, int> GetElementWeights()
    {
        var result = new Dictionary<Element, int>();
        foreach (var pair in ElementWeights)
        {
            if (Enum.TryParse<Element>(pair.Key, true, out var element))
            {
                int weight = IsLegendary ? pair.Value * 2 : pair.Value;
                result[element] = result.TryGetValue(element, out int existing) ? existing + weight : weight;
            }
        }
        return result;
    }

    public int GetDamageAtLevel(int level)
    {
        int damage = BaseDamage;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                damage += upgrade.DamageBonus;
            }
        }
        return damage;
    }

    public float GetCooldownAtLevel(int level)
    {
        float cooldown = BaseCooldown;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                cooldown += upgrade.CooldownBonus;
            }
        }

        return MathF.Max(0.05f, cooldown);
    }

    public int GetProjectileCountAtLevel(int level)
    {
        int projectileCount = BaseProjectileCount;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                projectileCount += upgrade.ProjectileCountBonus;
            }
        }
        return Math.Max(1, projectileCount);
    }

    public float GetRangeAtLevel(int level)
    {
        float range = BaseRange;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                range += upgrade.RangeBonus;
            }
        }
        return MathF.Max(0.0f, range);
    }

    public float GetEffectValueAtLevel(SpellEffect effect, int level)
    {
        float value = 0.0f;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level && upgrade.Effect == effect)
            {
                value += upgrade.EffectValue;
            }
        }
        return value;
    }

    public SpellScalingTag GetScalingTags() => (SpellScalingTag)ScalingTagsMask;

    public bool HasScalingTag(SpellScalingTag tag) => (GetScalingTags() & tag) != 0;

    public void AddScalingTag(SpellScalingTag tag)
    {
        ScalingTagsMask |= (int)tag;
    }
}
