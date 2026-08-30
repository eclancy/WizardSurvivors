using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

[GlobalClass]
public partial class SpellEvolutionOption : Resource
{
	[Export] public string Id { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
	[Export] public int MilestoneLevel { get; set; } = 4; // 4 or 8
	[Export] public string SynergyTag { get; set; } = string.Empty; // e.g. "Arcane / Pierce"
	[Export] public string SynergyDescription { get; set; } = string.Empty;
	[Export] public Texture2D Icon { get; set; }

	// Stat Deltas & Multipliers
	[Export] public int DamageBonus { get; set; } = 0;
	[Export] public float DamageMultiplier { get; set; } = 1.0f;
	[Export] public float CooldownBonus { get; set; } = 0.0f;
	[Export] public float CooldownMultiplier { get; set; } = 1.0f;
	[Export] public int ProjectileCountBonus { get; set; } = 0;
	[Export] public float RangeBonus { get; set; } = 0.0f;
	[Export] public float AreaMultiplier { get; set; } = 1.0f;
	[Export] public int PierceBonus { get; set; } = 0;
	[Export] public int ChainArcBonus { get; set; } = 0;
	[Export] public float KnockbackBonus { get; set; } = 0.0f;
	[Export] public float SlowMagnitudeBonus { get; set; } = 0.0f;
	[Export] public int PoisonTickBonus { get; set; } = 0;

	// Effect flag & value
	[Export] public SpellEffect Effect { get; set; } = SpellEffect.None;
	[Export] public float EffectValue { get; set; } = 0.0f;

	// Visual & audio alterations
	[Export] public string VisualTag { get; set; } = string.Empty;
	[Export] public Color ModulateColor { get; set; } = Colors.White;
	[Export] public float ScaleMultiplier { get; set; } = 1.0f;
	[Export] public float SpeedMultiplier { get; set; } = 1.0f;

	// Optional bonus element weights contributed upon taking this evolution
	[Export] public Godot.Collections.Dictionary<string, int> BonusElementWeights { get; set; } = new();
}
