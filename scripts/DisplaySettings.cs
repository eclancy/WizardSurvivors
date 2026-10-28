using Godot;

namespace WizardSurvivors.scripts;

// What the screen shows, as opposed to what the game does: today one switch, the damage numbers.
//
// 2026-10-28: "add way to remove damage numbers from enemies". The numbers stay on by default - they
// are how a new player learns what a spell is worth - and the switch sits beside the audio controls
// on the main menu's Options and in the pause screen. Stored in the save like the volumes
// (SaveData.ShowDamageNumbers), and held here as a plain static the hit path reads, because
// Enemy.TakeDamage runs several times a physics step in a swarm and must not look a node up to ask.
public static class DisplaySettings
{
	/// <summary>Whether a hit on an enemy throws its number (Enemy.TakeDamage).</summary>
	public static bool DamageNumbers { get; private set; } = true;

	/// <summary>Takes the saved switches. Called by SaveManager whenever its data is loaded or replaced.</summary>
	public static void ApplyFromSave(SaveData data)
	{
		if (data != null)
			DamageNumbers = data.ShowDamageNumbers;
	}

	/// <summary>Flips the damage numbers and persists it. Safe to call when there is no SaveManager.</summary>
	public static void StoreDamageNumbers(Node context, bool show)
	{
		DamageNumbers = show;
		var saveManager = context?.GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;
		saveManager.Data.ShowDamageNumbers = show;
		saveManager.SaveGame();
	}
}
