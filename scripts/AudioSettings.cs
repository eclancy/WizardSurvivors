using Godot;

namespace WizardSurvivors.scripts;

// Bus volumes, and the one place they are read from and written back to the save file.
//
// Before this existed the options sliders wrote straight to AudioServer and nothing persisted,
// so every launch came back at full volume. Worse, there was no bus layout at all: the project
// shipped only Godot's default Master bus, MusicPlayer.ResolveMusicBusName fell through to
// "Master", and the Music slider was therefore a second Master slider that silently desynced
// the first. default_bus_layout.tres now defines Master, Music and SFX, and this class keeps
// the three of them and the save file in agreement.
//
// Volumes are stored LINEAR (0..1, what a slider shows) and converted to dB at the bus, because
// a linear 0.5 is a sensible thing to write into a save file and -6.02 dB is not.
public static class AudioSettings
{
	public const string MasterBus = "Master";

	/// <summary>Push the saved volumes onto the buses. Called once, as the game starts.</summary>
	public static void ApplyFromSave(SaveData data)
	{
		if (data == null)
			return;
		SetBusLinear(MasterBus, data.MasterVolume);
		SetBusLinear(MusicPlayer.ResolveMusicBusName(), data.MusicVolume);
		SetBusLinear(SfxPlayer.ResolveSfxBusName(), data.SfxVolume);
		int master = AudioServer.GetBusIndex(MasterBus);
		if (master >= 0)
			AudioServer.SetBusMute(master, data.AudioMuted);
	}

	public static void SetBusLinear(string busName, float linear)
	{
		int index = AudioServer.GetBusIndex(busName);
		if (index < 0)
			return;
		// A slider at zero is silence, and LinearToDb(0) is negative infinity, which Godot will
		// take but which round-trips badly through JSON. Clamp to a floor that is inaudible.
		float clamped = Mathf.Clamp(linear, 0.0f, 1.0f);
		AudioServer.SetBusVolumeDb(index, clamped <= 0.001f ? -80.0f : Mathf.LinearToDb(clamped));
	}

	public static float GetBusLinear(string busName)
	{
		int index = AudioServer.GetBusIndex(busName);
		return index >= 0 ? Mathf.DbToLinear(AudioServer.GetBusVolumeDb(index)) : 1.0f;
	}

	/// <summary>Record a slider move and persist it. Safe to call when there is no SaveManager.</summary>
	public static void Store(Node context, string busName, float linear)
	{
		SetBusLinear(busName, linear);
		var saveManager = context?.GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;
		if (busName == MasterBus)
			saveManager.Data.MasterVolume = linear;
		else if (busName == MusicPlayer.ResolveMusicBusName())
			saveManager.Data.MusicVolume = linear;
		else if (busName == SfxPlayer.ResolveSfxBusName())
			saveManager.Data.SfxVolume = linear;
		saveManager.SaveGame();
	}

	public static void StoreMute(Node context, bool muted)
	{
		int master = AudioServer.GetBusIndex(MasterBus);
		if (master >= 0)
			AudioServer.SetBusMute(master, muted);
		var saveManager = context?.GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;
		saveManager.Data.AudioMuted = muted;
		saveManager.SaveGame();
	}
}
