using Godot;
using System;

public static class Global
{
    // Simple static properties to mirror the earlier GDScript Global
    public static int SelectedCharacterIdx { get; set; } = 0;
    public static int SelectedStageIdx { get; set; } = 0;
    // Test-only override used by the "test wizard" character to start with any selected weapon.
    public static string TestWizardStartingSpellId { get; set; } = string.Empty;

    // Set by anything returning to the menu from inside the game, and cleared by TitleScreen the
    // moment it reads it. The title screen is the menu's shell now, so "back to the main menu"
    // means "load the title screen and open the menu at once" - without this the player would have
    // to press a key again after every single run.
    public static bool OpenMenuImmediately { get; set; } = false;

    // The run about to start is a Boss Rush (issue #12, BossRushCatalog) rather than a chapter.
    // Set by the stage list on every pick, so it is only ever true straight after the rush's card;
    // a Retry from the results screen keeps it, which is what retrying a rush should do.
    public static bool BossRush { get; set; } = false;
}
