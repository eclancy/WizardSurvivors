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
    [Export] public bool IsUnlocked { get; set; } = true;

    // Character identity (issue #29): a starting passive spell id/bonus and whether this character's
    // starting spell is granted as its Legendary variant (issue #24) from run start rather than
    // needing to be rolled in-run. Element tags come only from equipped spells/passives.
    [Export] public string StartingPassiveId { get; set; } = string.Empty;
    [Export] public string StartingPassiveName { get; set; } = string.Empty;
    [Export] public string StartingPassiveDescription { get; set; } = string.Empty;
    [Export] public bool IsLegendaryStart { get; set; } = false;

    // Portrait shown on the character selection card. Left null falls back to a shared default
    // image in CharacterSelection.cs (no unique per-character art yet, #30).
    [Export] public Texture2D Portrait { get; set; }
}
