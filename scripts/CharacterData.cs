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

    // Character identity (issue #29): an inherent bonus element weight granted just for playing
    // this character (added on top of whatever the equipped spells contribute, see
    // Player.GetElementInstanceCounts), a starting passive spell id (equipped the same way as
    // StartingSpellResource, via Player.TryAddOrLevelSpell), and whether this character's starting
    // spell is granted as its Legendary variant (issue #24) from run start rather than needing to
    // be rolled in-run.
    [Export] public Element StartingElement { get; set; } = Element.Arcane;
    [Export] public string StartingPassiveId { get; set; } = string.Empty;
    [Export] public string StartingPassiveName { get; set; } = string.Empty;
    [Export] public bool IsLegendaryStart { get; set; } = false;

    // Portrait shown on the character selection card. Left null falls back to a shared default
    // image in CharacterSelection.cs (no unique per-character art yet, #30).
    [Export] public Texture2D Portrait { get; set; }
}
