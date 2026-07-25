using Godot;

namespace WizardSurvivors.scripts;

[GlobalClass]
public partial class SpellLevelUpgrade : Resource
{
    [Export] public int Level { get; set; } = 1;

    // Additive stat bonuses applied when this level is reached.
    [Export] public int DamageBonus { get; set; } = 0;
    [Export] public float CooldownBonus { get; set; } = 0.0f;
    [Export] public int ProjectileCountBonus { get; set; } = 0;
    [Export] public float RangeBonus { get; set; } = 0.0f;
    [Export] public int ChainArcBonus { get; set; } = 0;
    [Export] public int ChainBranchBonus { get; set; } = 0;
    [Export] public float ChainChanceBonus { get; set; } = 0.0f;
    [Export] public int PoisonTickBonus { get; set; } = 0;

    // Optional effect payload for level-specific behavior.
    [Export] public SpellEffect Effect { get; set; } = SpellEffect.None;
    [Export] public float EffectValue { get; set; } = 0.0f;
}
