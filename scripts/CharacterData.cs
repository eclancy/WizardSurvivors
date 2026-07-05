using Godot;

namespace WizardSurvivors.scripts;

[GlobalClass]
public partial class CharacterData : Resource
{
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string Name { get; set; } = string.Empty;
    [Export] public SpellData StartingSpellResource { get; set; } = null!;
    [Export] public float HealthModifier { get; set; } = 1.0f;
    [Export] public float SpeedModifier { get; set; } = 1.0f;
    [Export] public bool IsUnlocked { get; set; } = false;
}
