using Godot;
using System;

public partial class MusicPlayer : Node
{
#nullable enable
	public const string MusicBusName = "Music";
	public const string MasterBusName = "Master";

	private AudioStreamPlayer? player;

	public static string ResolveMusicBusName()
	{
		return AudioServer.GetBusIndex(MusicBusName) >= 0 ? MusicBusName : MasterBusName;
	}

	public override void _Ready()
	{
		player = new AudioStreamPlayer();
		AddChild(player);
		player.Bus = ResolveMusicBusName();
		player.VolumeDb = Mathf.LinearToDb(0.2f);
		player.ProcessMode = Node.ProcessModeEnum.Always;
		GD.Print("MusicPlayer: _Ready() invoked — player added and configured");
		// Diagnostic: list loaded assemblies to verify correct GodotSharp and project assembly are loaded
		try
		{
			foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
			{
				GD.Print($"Loaded assembly: {asm.GetName().Name}");
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"MusicPlayer: failed to enumerate assemblies: {e.Message}");
		}
	}

	public void PlayMusic(AudioStream stream, bool restart = false)
	{
		if (player == null) return;
		if (player.Stream != stream || restart)
		{
			player.Stop();
			player.Stream = stream;
			player.Play();
		}
		else if (!player.Playing)
		{
			player.Play();
		}
	}
	public void StopMusic() { if (player != null) player.Stop(); }
	public void PauseMusic() { if (player != null) player.StreamPaused = true; }
	public void ResumeMusic() { if (player != null) player.StreamPaused = false; }
}
