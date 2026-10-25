using Godot;

namespace WizardSurvivors.scripts;

public partial class SaveManager : Node
{
	public const string SavePath = "user://savegame.json";

	public SaveData Data { get; private set; } = new();

	/// <summary>
	/// When true, SaveGame does nothing. Set only by tools that run the real game against an
	/// in-memory save they have altered - the balance harness with --all-unlocked - so that a test
	/// run which ends with the usual save cannot overwrite the player's real progress with it.
	/// </summary>
	public static bool WritesDisabled { get; set; }

	/// <summary>Replaces the in-memory save. Tools only; pair with <see cref="WritesDisabled"/>.</summary>
	public void ReplaceInMemory(SaveData data) => Data = data ?? new SaveData();

	/// <summary>The user argument that keeps a whole launch from writing the save, migration included.</summary>
	public const string NoSaveWritesArg = "--no-save-writes";

	public override void _Ready()
	{
		// Before the load, not after it: LoadGame migrates an older save and writes the result
		// straight back. A tool scene that sets WritesDisabled in its own _Ready is already too late
		// for that write, because this autoload is ready first - so a branch that bumps the schema
		// rewrote the player's real save the first time anyone ran a harness on it. Pass
		// `-- --no-save-writes` to any run that must leave user://savegame.json exactly as it was.
		if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), NoSaveWritesArg) >= 0)
			WritesDisabled = true;
		Data = LoadGame();
	}

	/// <summary>
	/// Wipes progress back to a brand-new save and writes it immediately.
	/// </summary>
	/// <remarks>
	/// Needed because migration deliberately grandfathers pre-campaign saves into owning
	/// everything - which is right for a returning player and useless for testing, since it leaves
	/// no way to see the unlock flow from the start short of hand-editing the JSON. Destructive and
	/// irreversible: every caller must confirm first.
	/// </remarks>
	public void ResetProgress()
	{
		Data = new SaveData();
		SaveGame();
		GD.Print("SaveManager: progress reset to a new save.");
	}

	public void SaveGame()
	{
		if (WritesDisabled)
			return;
		string json = Json.Stringify(Data.ToGodotDictionary());
		using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PushError($"Failed to open save file for writing: {SavePath}");
			return;
		}

		file.StoreString(json);
	}

	public SaveData LoadGame()
	{
		if (!FileAccess.FileExists(SavePath))
		{
			Data = new SaveData();
			return Data;
		}

		using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"Failed to open save file for reading: {SavePath}");
			Data = new SaveData();
			return Data;
		}

		string json = file.GetAsText();
		Variant parsed = Json.ParseString(json);
		if (parsed.VariantType == Variant.Type.Nil)
		{
			GD.PushWarning("Save file exists but contains invalid JSON. Loading defaults.");
			Data = new SaveData();
			return Data;
		}

		Data = SaveData.FromVariant(parsed);
		// Before anything reads unlock state. A pre-campaign save has an empty UnlockedSpellIds
		// that means "everything was free", not "nothing was earned"; Migrate is what keeps that
		// from reading as an empty spellbook.
		if (Data.Migrate())
			SaveGame();

		return Data;
	}
}
