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
    // (the retired passive-spell roster). Nothing sets it any more - boons replaced those spells -
    // but UI code still reads it, so it stays as a false default rather than a removal that ripples.
    // Lets UI code (e.g. LevelUpMenu's elemental tag
    // section) distinguish "this element tag belongs to an equipped passive ability" from active
    // offensive spells, which don't get stronger from element tier bonuses the same way.
    [Export] public bool IsPassive { get; set; } = false;

    // Evolution / branching options at milestones (Issue #48: 3 choices at Lv 4, 2 choices at Lv 8).
    [Export] public Godot.Collections.Array<SpellEvolutionOption> Level4Options { get; set; } = new();
    [Export] public Godot.Collections.Array<SpellEvolutionOption> Level8Options { get; set; } = new();

    // Selected evolution instances for this runtime spell instance.
    [Export] public SpellEvolutionOption SelectedLevel4Evolution { get; set; }
    [Export] public SpellEvolutionOption SelectedLevel8Evolution { get; set; }
    [Export] public string SelectedLevel4EvolutionId { get; set; } = string.Empty;
    [Export] public string SelectedLevel8EvolutionId { get; set; } = string.Empty;

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

        // Include any bonus element weights from chosen evolutions
        ApplyBonusElementWeights(SelectedLevel4Evolution, result);
        ApplyBonusElementWeights(SelectedLevel8Evolution, result);

        return result;
    }

    private void ApplyBonusElementWeights(SpellEvolutionOption evo, Dictionary<Element, int> target)
    {
        if (evo?.BonusElementWeights == null) return;
        foreach (var pair in evo.BonusElementWeights)
        {
            if (Enum.TryParse<Element>(pair.Key, true, out var element))
            {
                int weight = IsLegendary ? pair.Value * 2 : pair.Value;
                target[element] = target.TryGetValue(element, out int existing) ? existing + weight : weight;
            }
        }
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

        if (level >= 4 && SelectedLevel4Evolution != null)
        {
            damage += SelectedLevel4Evolution.DamageBonus;
            damage = Mathf.RoundToInt(damage * SelectedLevel4Evolution.DamageMultiplier);
        }

        if (level >= 8 && SelectedLevel8Evolution != null)
        {
            damage += SelectedLevel8Evolution.DamageBonus;
            damage = Mathf.RoundToInt(damage * SelectedLevel8Evolution.DamageMultiplier);
        }

        return Math.Max(1, damage);
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

        if (level >= 4 && SelectedLevel4Evolution != null)
        {
            cooldown += SelectedLevel4Evolution.CooldownBonus;
            cooldown *= SelectedLevel4Evolution.CooldownMultiplier;
        }

        if (level >= 8 && SelectedLevel8Evolution != null)
        {
            cooldown += SelectedLevel8Evolution.CooldownBonus;
            cooldown *= SelectedLevel8Evolution.CooldownMultiplier;
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

        if (level >= 4 && SelectedLevel4Evolution != null)
        {
            projectileCount += SelectedLevel4Evolution.ProjectileCountBonus;
        }

        if (level >= 8 && SelectedLevel8Evolution != null)
        {
            projectileCount += SelectedLevel8Evolution.ProjectileCountBonus;
        }

        return Math.Max(1, projectileCount);
    }

    public int GetChainArcCountAtLevel(int level)
    {
        int chainArcCount = 0;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                chainArcCount += upgrade.ChainArcBonus;
            }
        }

        if (level >= 4 && SelectedLevel4Evolution != null)
            chainArcCount += SelectedLevel4Evolution.ChainArcBonus;
        if (level >= 8 && SelectedLevel8Evolution != null)
            chainArcCount += SelectedLevel8Evolution.ChainArcBonus;

        return Math.Max(0, chainArcCount);
    }

    public int GetChainBranchCountAtLevel(int level)
    {
        int chainBranchCount = 0;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                chainBranchCount += upgrade.ChainBranchBonus;
            }
        }

        if (level >= 4 && SelectedLevel4Evolution != null)
            chainBranchCount += SelectedLevel4Evolution.ChainBranchBonus;
        if (level >= 8 && SelectedLevel8Evolution != null)
            chainBranchCount += SelectedLevel8Evolution.ChainBranchBonus;

        return Math.Max(0, chainBranchCount);
    }

    public float GetChainChanceAtLevel(int level)
    {
        float chainChance = 0.0f;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                chainChance += upgrade.ChainChanceBonus;
            }
        }
        return MathF.Max(0.0f, chainChance);
    }

    public int GetPoisonTickBonusAtLevel(int level)
    {
        int poisonTickBonus = 0;
        foreach (SpellLevelUpgrade upgrade in LevelUpgrades)
        {
            if (upgrade != null && upgrade.Level <= level)
            {
                poisonTickBonus += upgrade.PoisonTickBonus;
            }
        }

        if (level >= 4 && SelectedLevel4Evolution != null)
            poisonTickBonus += SelectedLevel4Evolution.PoisonTickBonus;
        if (level >= 8 && SelectedLevel8Evolution != null)
            poisonTickBonus += SelectedLevel8Evolution.PoisonTickBonus;

        return Math.Max(0, poisonTickBonus);
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

        if (level >= 4 && SelectedLevel4Evolution != null)
            range += SelectedLevel4Evolution.RangeBonus;
        if (level >= 8 && SelectedLevel8Evolution != null)
            range += SelectedLevel8Evolution.RangeBonus;

        return MathF.Max(0.0f, range);
    }

    public int GetPierceAtLevel(int level)
    {
        int pierce = (int)MathF.Round(GetEffectValueAtLevel(SpellEffect.Pierce, level));
        if (level >= 4 && SelectedLevel4Evolution != null)
            pierce += SelectedLevel4Evolution.PierceBonus;
        if (level >= 8 && SelectedLevel8Evolution != null)
            pierce += SelectedLevel8Evolution.PierceBonus;
        return Math.Max(0, pierce);
    }

    public float GetAreaMultiplierAtLevel(int level)
    {
        float areaMul = 1.0f;
        if (level >= 4 && SelectedLevel4Evolution != null)
            areaMul *= SelectedLevel4Evolution.AreaMultiplier;
        if (level >= 8 && SelectedLevel8Evolution != null)
            areaMul *= SelectedLevel8Evolution.AreaMultiplier;
        return areaMul;
    }

    public float GetKnockbackBonusAtLevel(int level)
    {
        float knockback = 0.0f;
        if (level >= 4 && SelectedLevel4Evolution != null)
            knockback += SelectedLevel4Evolution.KnockbackBonus;
        if (level >= 8 && SelectedLevel8Evolution != null)
            knockback += SelectedLevel8Evolution.KnockbackBonus;
        return knockback;
    }

    public float GetSlowMagnitudeAtLevel(int level)
    {
        float slow = 0.0f;
        if (level >= 4 && SelectedLevel4Evolution != null)
            slow += SelectedLevel4Evolution.SlowMagnitudeBonus;
        if (level >= 8 && SelectedLevel8Evolution != null)
            slow += SelectedLevel8Evolution.SlowMagnitudeBonus;
        return slow;
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

        if (level >= 4 && SelectedLevel4Evolution != null && SelectedLevel4Evolution.Effect == effect)
            value += SelectedLevel4Evolution.EffectValue;
        if (level >= 8 && SelectedLevel8Evolution != null && SelectedLevel8Evolution.Effect == effect)
            value += SelectedLevel8Evolution.EffectValue;

        return value;
    }

    public bool HasEvolution(string evolutionId)
    {
        if (string.IsNullOrWhiteSpace(evolutionId)) return false;
        return string.Equals(SelectedLevel4EvolutionId, evolutionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedLevel8EvolutionId, evolutionId, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasEffectFlag(SpellEffect effect)
    {
        if (SelectedLevel4Evolution != null && SelectedLevel4Evolution.Effect == effect)
            return true;
        if (SelectedLevel8Evolution != null && SelectedLevel8Evolution.Effect == effect)
            return true;
        return false;
    }

    public IReadOnlyList<SpellEvolutionOption> GetEvolutionOptionsForLevel(int level)
    {
        if (level == 4)
            return Level4Options ?? (IReadOnlyList<SpellEvolutionOption>)Array.Empty<SpellEvolutionOption>();
        if (level == 8)
            return Level8Options ?? (IReadOnlyList<SpellEvolutionOption>)Array.Empty<SpellEvolutionOption>();
        return Array.Empty<SpellEvolutionOption>();
    }

    public void ApplyEvolution(SpellEvolutionOption evolution)
    {
        if (evolution == null) return;
        if (evolution.MilestoneLevel == 4)
        {
            SelectedLevel4Evolution = evolution;
            SelectedLevel4EvolutionId = evolution.Id;
        }
        else if (evolution.MilestoneLevel == 8)
        {
            SelectedLevel8Evolution = evolution;
            SelectedLevel8EvolutionId = evolution.Id;
        }
    }

    public Color GetModulateColor()
    {
        Color color = Colors.White;
        if (SelectedLevel4Evolution != null && SelectedLevel4Evolution.ModulateColor != Colors.White)
            color = SelectedLevel4Evolution.ModulateColor;
        if (SelectedLevel8Evolution != null && SelectedLevel8Evolution.ModulateColor != Colors.White)
            color = SelectedLevel8Evolution.ModulateColor;
        return color;
    }

    public float GetScaleMultiplier()
    {
        float scale = 1.0f;
        if (SelectedLevel4Evolution != null)
            scale *= SelectedLevel4Evolution.ScaleMultiplier;
        if (SelectedLevel8Evolution != null)
            scale *= SelectedLevel8Evolution.ScaleMultiplier;
        return scale;
    }

    public float GetSpeedMultiplier()
    {
        float speed = 1.0f;
        if (SelectedLevel4Evolution != null)
            speed *= SelectedLevel4Evolution.SpeedMultiplier;
        if (SelectedLevel8Evolution != null)
            speed *= SelectedLevel8Evolution.SpeedMultiplier;
        return speed;
    }

    public string GetVisualTag()
    {
        if (SelectedLevel8Evolution != null && !string.IsNullOrWhiteSpace(SelectedLevel8Evolution.VisualTag))
            return SelectedLevel8Evolution.VisualTag;
        if (SelectedLevel4Evolution != null && !string.IsNullOrWhiteSpace(SelectedLevel4Evolution.VisualTag))
            return SelectedLevel4Evolution.VisualTag;
        return string.Empty;
    }

    public SpellScalingTag GetScalingTags() => (SpellScalingTag)ScalingTagsMask;

    public bool HasScalingTag(SpellScalingTag tag) => (GetScalingTags() & tag) != 0;

    public void AddScalingTag(SpellScalingTag tag)
    {
        ScalingTagsMask |= (int)tag;
    }
}
