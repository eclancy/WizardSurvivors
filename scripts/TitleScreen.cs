using Godot;
using System;

public partial class TitleScreen : Control
{
	private bool transitioned = false;

	public override void _Ready()
	{
		if (!HasNode("AudioStreamPlayer"))
		{
			var music = new AudioStreamPlayer();
			music.Name = "AudioStreamPlayer";
			music.Stream = ResourceLoader.Load<AudioStream>("res://assets/Pixel_Knights.mp3");
			music.Autoplay = false;
			music.Bus = "Music";
			music.VolumeDb = Mathf.LinearToDb(0.2f);
			AddChild(music);
			music.Play();
		}
		var musicPlayer = GetNodeOrNull<Node>("/root/MusicPlayer");
		if (musicPlayer != null)
		{
			//musicPlayer.Call("PlayMusic", ResourceLoader.Load<AudioStream>("res://assets/Pixel_Knights.mp3"));
		}

		GD.Print("TitleScreen: _Ready() invoked");
	}

	public override void _Input(InputEvent @event)
	{
		if (transitioned) return;
		if ((@event is InputEventKey ek && ek.Pressed) || (@event is InputEventMouseButton mb && mb.Pressed) || (@event is InputEventJoypadButton jb && jb.Pressed))
		{
			transitioned = true;
			var scenePath = "res://scenes/MainMenu.tscn";
			if (ResourceLoader.Exists(scenePath))
				GetTree().ChangeSceneToFile(scenePath);
			else
				GD.PushError($"TitleScreen: scene not found: {scenePath}");
		}
	}
}
