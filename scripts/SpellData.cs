using Godot;
using System;

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
}
