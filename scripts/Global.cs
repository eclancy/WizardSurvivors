using Godot;
using System;

public static class Global
{
    // Simple static properties to mirror the earlier GDScript Global
    public static int SelectedCharacterIdx { get; set; } = 0;
    public static int SelectedStageIdx { get; set; } = 0;
    // Test-only override used by the "test wizard" character to start with any selected weapon.
    public static string TestWizardStartingSpellId { get; set; } = string.Empty;
}
