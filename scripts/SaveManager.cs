using Godot;

namespace WizardSurvivors.scripts;

public partial class SaveManager : Node
{
	public const string SavePath = "user://savegame.json";

	public SaveData Data { get; private set; } = new();

	public override void _Ready()
	{
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
