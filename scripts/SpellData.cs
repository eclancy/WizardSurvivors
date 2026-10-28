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

    // The third milestone (issue #59/#60): a wizard's Apotheosis, the final form of its starting
    // spell. MilestoneLevel 9 - offered only past max level, and terminal once taken. Its option is
    // not stored on the spell: ApotheosisCatalog is the one place it lives.
    [Export] public SpellEvolutionOption SelectedApotheosis { get; set; }
    [Export] public string SelectedApotheosisId { get; set; } = string.Empty;

    /// <summary>True once this spell has become its wizard's Apotheosis.</summary>
    public bool IsApotheosis => SelectedApotheosis != null;

    // Optional icon shown on the level-up card (LevelUpMenu). Left null for most spells (no unique
    // art yet, #30) - LevelUpMenu falls back to a shared default icon in that case.
    [Export] public Texture2D Icon { get; set; }

    // FORMS (.ai/decisions/2026-10-28-spell-variants.md). A spell with variants is taken in one of them:
    // the card that offers it opens a page of its forms, and the form chosen holds for the run. Empty for
    // almost every spell. SpellVariants.Apply writes the chosen one into the run's copy of the spell -
    // name, icon, numbers, level table and milestones - and records it here.
    [Export] public Godot.Collections.Array<SpellVariant> Variants { get; set; } = new();

    /// <summary>The heading of the page that offers the forms ("Choose your weapon").</summary>
    [Export] public string VariantPrompt { get; set; } = string.Empty;

    /// <summary>The form this run's copy of the spell was taken in. Empty on a template, and on a spell with none.</summary>
    [Export] public string SelectedVariantId { get; set; } = string.Empty;

    public bool HasVariants => Variants != null && Variants.Count > 0;

    /// <summary>The form a bot or a starting spell gets when no one chose: the first in the .tres.</summary>
    public SpellVariant DefaultVariant => HasVariants ? Variants[0] : null;

    /// <summary>The chosen form, or null.</summary>
    public SpellVariant SelectedVariant => FindVariant(SelectedVariantId);

    public SpellVariant FindVariant(string variantId)
    {
        if (string.IsNullOrWhiteSpace(variantId) || Variants == null)
            return null;
        foreach (SpellVariant variant in Variants)
        {
            if (variant != null && variant.Id.Equals(variantId.Trim(), StringComparison.OrdinalIgnoreCase))
                return variant;
        }
        return null;
    }

    // EVERY READER BELOW WALKS THE SAME LIST, in milestone order: level 4, level 8, Apotheosis.
    // They used to repeat a level-4 block and a level-8 block each, fifteen times over, and the
    // issue that planned the third milestone (#60) listed seven of them by name as places it could
    // be forgotten. One collector means it cannot be. A reused buffer, because several of these
    // run on every cast of every spell.
    private readonly SpellEvolutionOption[] activeBuffer = new SpellEvolutionOption[3];

    private int CollectActive(int level)
    {
        int n = 0;
        if (level >= 4 && SelectedLevel4Evolution != null) activeBuffer[n++] = SelectedLevel4Evolution;
        if (level >= 8 && SelectedLevel8Evolution != null) activeBuffer[n++] = SelectedLevel8Evolution;
        if (SelectedApotheosis != null) activeBuffer[n++] = SelectedApotheosis;
        return n;
    }

    // The same list regardless of level, for the readers that never took one.
    private int CollectAll() => CollectActive(int.MaxValue);

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

        // Include any bonus element weights from chosen evolutions, in milestone order. A branch
        // that trades an element away (SpellEvolutionOption.ReplacesElement) removes it first, so a
        // swap leaves the spell naming the same number of elements it did.
        int n = CollectAll();
        for (int i = 0; i < n; i++)
        {
            string gone = activeBuffer[i]?.ReplacesElement;
            if (!string.IsNullOrEmpty(gone) && Enum.TryParse<Element>(gone, true, out var replaced))
                result.Remove(replaced);
            ApplyBonusElementWeights(activeBuffer[i], result);
        }

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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
        {
            damage += activeBuffer[i].DamageBonus;
            damage = Mathf.RoundToInt(damage * activeBuffer[i].DamageMultiplier);
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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
        {
            cooldown += activeBuffer[i].CooldownBonus;
            cooldown *= activeBuffer[i].CooldownMultiplier;
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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            projectileCount += activeBuffer[i].ProjectileCountBonus;

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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            chainArcCount += activeBuffer[i].ChainArcBonus;

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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            chainBranchCount += activeBuffer[i].ChainBranchBonus;

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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            poisonTickBonus += activeBuffer[i].PoisonTickBonus;

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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            range += activeBuffer[i].RangeBonus;

        return MathF.Max(0.0f, range);
    }

    public int GetPierceAtLevel(int level)
    {
        int pierce = (int)MathF.Round(GetEffectValueAtLevel(SpellEffect.Pierce, level));
        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            pierce += activeBuffer[i].PierceBonus;
        return Math.Max(0, pierce);
    }

    public float GetAreaMultiplierAtLevel(int level)
    {
        float areaMul = 1.0f;
        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            areaMul *= activeBuffer[i].AreaMultiplier;
        return areaMul;
    }

    public float GetKnockbackBonusAtLevel(int level)
    {
        float knockback = 0.0f;
        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            knockback += activeBuffer[i].KnockbackBonus;
        return knockback;
    }

    // KnockbackBonus is written on the cards in the same units as Arcane Explosion's knockback
    // range, which shoves at twice that in px/s (its BaseKnockbackSpeed). Every spell that turns the
    // bonus into a shove goes through this one conversion, so "+120 knockback" is the same push on a
    // cone, a pulse and a meteor. Enemy.ApplyKnockback holds the speed for 0.45 s, so 120 is a shove
    // of roughly a hundred pixels; a boss's KnockbackResistance takes its share off the top.
    public const float KnockbackSpeedPerPoint = 2.0f;

    public float GetKnockbackSpeedAtLevel(int level) => GetKnockbackBonusAtLevel(level) * KnockbackSpeedPerPoint;

    // Seconds of freeze (Enemy.ApplySlow at 0) a spell's hits carry. Effect Freeze was set by four
    // cards and read by nothing until 2026-10-26; the spells that now read it are listed against
    // SpellEffect.Freeze in EvolutionStatReaders.
    public float GetFreezeSecondsAtLevel(int level) => MathF.Max(0f, GetEffectValueAtLevel(SpellEffect.Freeze, level));

    public float GetSlowMagnitudeAtLevel(int level)
    {
        float slow = 0.0f;
        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
            slow += activeBuffer[i].SlowMagnitudeBonus;
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

        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
        {
            if (activeBuffer[i].Effect == effect)
                value += activeBuffer[i].EffectValue;
        }

        return value;
    }

    public bool HasEvolution(string evolutionId)
    {
        if (string.IsNullOrWhiteSpace(evolutionId)) return false;
        return string.Equals(SelectedLevel4EvolutionId, evolutionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedLevel8EvolutionId, evolutionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedApotheosisId, evolutionId, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasEffectFlag(SpellEffect effect)
    {
        int n = CollectAll();
        for (int i = 0; i < n; i++)
        {
            if (activeBuffer[i].Effect == effect)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The choices offered at a milestone. The Apotheosis (level 9) is not stored on the spell -
    /// <see cref="ApotheosisCatalog"/> owns it - so it is never returned from here.
    /// </summary>
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
        else if (evolution.MilestoneLevel == ApotheosisCatalog.MilestoneLevel)
        {
            // Terminal: an Apotheosis is never replaced, so a second one is refused rather than
            // swapped in (issue #59, "terminal by design").
            if (SelectedApotheosis != null)
                return;
            SelectedApotheosis = evolution;
            SelectedApotheosisId = evolution.Id;
        }
        else
        {
            // This used to drop anything that was not 4 or 8 without a word.
            GD.PushError($"SpellData '{Id}': evolution '{evolution.Id}' has milestone {evolution.MilestoneLevel}, which no branch applies.");
        }
    }

    // Latest milestone wins - except that a branch into a new element keeps its colour for good.
    // An element path is a different spell from then on (.ai/spell-roster.md, "Element paths"), and
    // a level 8 tint laid over it later (Titan Cataclysm's orange, say) would hide the element the
    // player chose. An Apotheosis cannot follow a branch (its required path deepens the wizard's own
    // element), so this never fights a final form for the colour.
    public Color GetModulateColor()
    {
        Color color = Colors.White;
        Color branch = Colors.White;
        int n = CollectAll();
        for (int i = 0; i < n; i++)
        {
            if (activeBuffer[i].ModulateColor == Colors.White)
                continue;
            color = activeBuffer[i].ModulateColor;
            if (BranchElementOf(activeBuffer[i]) != null)
                branch = activeBuffer[i].ModulateColor;
        }
        return branch != Colors.White ? branch : color;
    }

    /// <summary>
    /// The element a taken milestone added that the spell's own tags do not carry - its element path -
    /// or null if every milestone it took deepens what it already was. A spell names at most two
    /// elements (RegressionChecks.ValidateEvolutionElementCeiling), so there is at most one of these.
    /// Read once per cast or per node, never per frame: it walks Godot dictionaries.
    /// </summary>
    public Element? GetBranchElement()
    {
        int n = CollectAll();
        for (int i = 0; i < n; i++)
        {
            if (BranchElementOf(activeBuffer[i]) is Element element)
                return element;
        }
        return null;
    }

    private Element? BranchElementOf(SpellEvolutionOption evo)
    {
        if (evo?.BonusElementWeights == null)
            return null;
        foreach (var pair in evo.BonusElementWeights)
        {
            if (pair.Value > 0 && !ElementWeights.ContainsKey(pair.Key)
                && Enum.TryParse<Element>(pair.Key, true, out var element))
                return element;
        }
        return null;
    }

    /// <summary>
    /// Applies every element rider the spell's active milestones carry to one enemy it just hit
    /// (ElementPaths). Called by Player.DealDamageToEnemy, once per hit.
    /// </summary>
    public void ApplyElementRiders(Enemy enemy, int level)
    {
        int n = CollectActive(level);
        for (int i = 0; i < n; i++)
        {
            SpellEvolutionOption evo = activeBuffer[i];
            if (evo.Rider != ElementRider.None)
                ElementPaths.Apply(enemy, evo.Rider, evo.RiderMagnitude, evo.RiderSeconds);
        }
    }

    public float GetScaleMultiplier()
    {
        float scale = 1.0f;
        int n = CollectAll();
        for (int i = 0; i < n; i++)
            scale *= activeBuffer[i].ScaleMultiplier;
        return scale;
    }

    public float GetSpeedMultiplier()
    {
        float speed = 1.0f;
        int n = CollectAll();
        for (int i = 0; i < n; i++)
            speed *= activeBuffer[i].SpeedMultiplier;
        return speed;
    }

    // Latest milestone wins: the Apotheosis, then level 8, then level 4.
    public string GetVisualTag()
    {
        int n = CollectAll();
        for (int i = n - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(activeBuffer[i].VisualTag))
                return activeBuffer[i].VisualTag;
        }
        return string.Empty;
    }

    public SpellScalingTag GetScalingTags() => (SpellScalingTag)ScalingTagsMask;

    public bool HasScalingTag(SpellScalingTag tag) => (GetScalingTags() & tag) != 0;

    public void AddScalingTag(SpellScalingTag tag)
    {
        ScalingTagsMask |= (int)tag;
    }
}
