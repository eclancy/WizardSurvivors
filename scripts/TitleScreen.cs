using Godot;
using System;

public partial class TitleScreen : Control
{
	// How long the artwork sits alone before the prompt appears, and how long the fade takes.
	[Export] public float PromptDelay { get; set; } = 1.1f;
	[Export] public float PromptFadeIn { get; set; } = 1.0f;
	[Export] public float PromptBreath { get; set; } = 1.6f;

	private bool transitioned = false;

	public override void _Ready()
	{
		// No skin pass here. The scene used to paint two stock fantasy-GUI textures over the
		// whole screen at 0.96 and 0.28 opacity before the title art drew; both are from the
		// pre-Bonelight asset set and they fought the composition. The title art is now a
		// single 360x640 image rendered at a whole x2 (see .ai/art-direction.md section 1).
		FadeInPrompt();

		// Route menu music through the MusicPlayer autoload so it plays continuously from the title
		// screen through the menus and restarts when the player quits a run back to the menu.
		var musicPlayer = GetNodeOrNull<MusicPlayer>("/root/MusicPlayer");
		var menuMusic = ResourceLoader.Load<AudioStream>("res://assets/Pixel_Knights.mp3");
		if (musicPlayer != null && menuMusic != null)
			musicPlayer.PlayMusic(menuMusic);

		GD.Print("TitleScreen: _Ready() invoked");
	}

	/// <summary>
	/// PRESS ANY KEY is a separate texture rather than part of the title art, so it can arrive
	/// after the artwork has had a moment on its own and then breathe. It is a TextureRect and
	/// not a Label because the prompt is set in the same hand-drawn pixel face as the wordmark
	/// and there is no Godot font resource for that face.
	/// </summary>
	private void FadeInPrompt()
	{
		var prompt = GetNodeOrNull<CanvasItem>("TitleImage/Prompt");
		if (prompt == null)
			return;

		prompt.Modulate = new Color(1f, 1f, 1f, 0f);

		var appear = CreateTween();
		appear.TweenInterval(PromptDelay);
		appear.TweenProperty(prompt, "modulate:a", 1.0f, PromptFadeIn)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		// The pulse is a second tween started on completion rather than looped stages of the
		// first: SetLoops on the first would replay the delay and the fade every cycle.
		appear.Finished += () =>
		{
			if (!IsInstanceValid(prompt))
				return;
			var breath = CreateTween().SetLoops();
			breath.TweenProperty(prompt, "modulate:a", 0.42f, PromptBreath)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			breath.TweenProperty(prompt, "modulate:a", 1.0f, PromptBreath)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		};
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
