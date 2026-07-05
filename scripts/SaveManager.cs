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
		return Data;
	}
}
