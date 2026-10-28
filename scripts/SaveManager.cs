using Godot;

namespace WizardSurvivors.scripts;

public partial class SaveManager : Node
{
	public const string SavePath = "user://savegame.json";

	public SaveData Data { get; private set; } = new();

	/// <summary>
	/// When true, SaveGame does nothing - including the write-back after a migration on load. Set
	/// before the load for every tool-scene launch (<see cref="IsWriteProtectedLaunch"/>), and by
	/// tools that swap in an in-memory save they have altered, so a test run can never overwrite
	/// the player's real progress.
	/// </summary>
	public static bool WritesDisabled { get; set; }

	/// <summary>Replaces the in-memory save. Tools only; pair with <see cref="WritesDisabled"/>.</summary>
	public void ReplaceInMemory(SaveData data)
	{
		Data = data ?? new SaveData();
		DisplaySettings.ApplyFromSave(Data);
	}

	/// <summary>The user argument that keeps a whole launch from writing the save, migration included.</summary>
	public const string NoSaveWritesArg = "--no-save-writes";

	public override void _Ready()
	{
		// DECIDED BEFORE THE LOAD, not after it. LoadGame migrates an older save and writes the
		// result straight back, and this autoload is ready before any scene is: a tool scene that
		// sets WritesDisabled in its own _Ready is already too late for that write. On 2026-10-25 the
		// developer's real save was rewritten to a branch's schema 13, with a run of 6-second forced
		// defeats paid into it, by tool runs on branches still being built. A save written by a
		// harness, a probe or a screenshot is a save the player did not earn, and one migrated by a
		// branch that has not shipped is a schema the released build will never upgrade again.
		if (IsWriteProtectedLaunch())
			WritesDisabled = true;
		Data = LoadGame();
		// The display switches are read on hot paths (a damage number per hit), so they are held as
		// statics and taken from the save here, before any scene runs.
		DisplaySettings.ApplyFromSave(Data);
	}

	/// <summary>
	/// True when this launch must never write user://savegame.json: any run of a tool scene - every
	/// scene whose file name starts with an underscore (_UiShot, _BalanceHarness, _BossProbe,
	/// _ExploreProbe, _MazeProbe, ...) - or any run passed <c>-- --no-save-writes</c>.
	/// </summary>
	/// <remarks>
	/// Read from the command line because nothing else exists yet: the scene a headless or editor
	/// run opens is the one ".tscn" argument Godot hands through (OS.GetCmdlineArgs), and the main
	/// scene is not in the tree until after every autoload is ready. The game itself, launched with
	/// no scene argument, is the only thing that writes. A tool that needs a save to start from
	/// builds one in memory (ReplaceInMemory), which is what --all-unlocked and --fresh-save do.
	/// </remarks>
	public static bool IsWriteProtectedLaunch() =>
		System.Array.IndexOf(OS.GetCmdlineUserArgs(), NoSaveWritesArg) >= 0 || IsToolSceneLaunch();

	/// <summary>True when the scene this launch opens is a tool scene (its file name starts with "_").</summary>
	public static bool IsToolSceneLaunch()
	{
		foreach (string arg in OS.GetCmdlineArgs())
		{
			if (!arg.EndsWith(".tscn", System.StringComparison.OrdinalIgnoreCase)
				&& !arg.EndsWith(".scn", System.StringComparison.OrdinalIgnoreCase))
				continue;
			string normalized = arg.Replace('\\', '/');
			string fileName = normalized.Substring(normalized.LastIndexOf('/') + 1);
			if (fileName.StartsWith("_", System.StringComparison.Ordinal))
				return true;
		}
		return false;
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
