using Godot;
using System;

public partial class TitleScreen : Control
{
	private bool transitioned = false;

	public override void _Ready()
	{
		FantasyGuiSkin.ApplyFullscreenBackdrop(this, "res://assets/imported/fantasy_rpg_gui/Loading/1.png", 0.96f);
		FantasyGuiSkin.ApplyPanelBackdrop(GetNodeOrNull<Control>("Prompt"), "res://assets/imported/fantasy_rpg_gui/Loading/5.png", 0.28f);

		// Route menu music through the MusicPlayer autoload so it plays continuously from the title
		// screen through the menus and restarts when the player quits a run back to the menu.
		var musicPlayer = GetNodeOrNull<MusicPlayer>("/root/MusicPlayer");
		var menuMusic = ResourceLoader.Load<AudioStream>("res://assets/Pixel_Knights.mp3");
		if (musicPlayer != null && menuMusic != null)
			musicPlayer.PlayMusic(menuMusic);

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
