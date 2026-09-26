using Godot;
using System;

public partial class TitleScreen : Control
{
	// How long the artwork sits alone before the prompt appears, and how long the fade takes.
	[Export] public float PromptDelay { get; set; } = 1.1f;
	[Export] public float PromptFadeIn { get; set; } = 1.0f;
	[Export] public float PromptBreath { get; set; } = 1.6f;

	// --- animation ------------------------------------------------------------------------
	// Every state of every moving thing is a pre-rendered frame with a closed palette (see
	// tools/art/anim.py). Nothing here tints, fades or cross-dissolves a layer: a blended
	// pixel is not one of the 66 colours the Bonelight contract allows, and the palette audit
	// the generator runs would stop meaning anything the moment this screen breathed. The one
	// exception is the prompt, which is bone-white type alone on its own layer with nothing
	// behind it to dirty.
	[Export] public float FlameFrameSeconds { get; set; } = 0.13f;
	[Export] public float WardPulseSeconds { get; set; } = 0.95f;
	[Export] public float BlinkHoldSeconds { get; set; } = 0.11f;
	[Export] public float BlinkMinGap { get; set; } = 0.35f;
	[Export] public float BlinkMaxGap { get; set; } = 1.9f;

	private const int FlamePhases = 4;
	private const int WardFrames = 3;
	private const int BlinkFrames = 6;   // frame 0 is all-open

	private Texture2D[] flamesFar, flamesNear, figure, ward, eyesFar, eyesNear;
	private TextureRect flamesFarRect, flamesNearRect, figureRect, wardRect, eyesFarRect, eyesNearRect;

	private float flameClock, wardClock, blinkClock;
	private int flameStep, wardStep;                // walks 0,1,2,1 so the pulse turns round rather than snapping
	private float nextBlink;
	private int blinkFrame;
	private readonly RandomNumberGenerator rng = new RandomNumberGenerator();

	private bool transitioned = false;
	private Control menuInstance;

	public override void _Ready()
	{
		// Returning from a run, a quit, or backing out of character select: the player has already
		// seen the title beat and should not have to press a key to get past it again.
		bool openMenuNow = Global.OpenMenuImmediately;
		Global.OpenMenuImmediately = false;

		// No skin pass here. The scene used to paint two stock fantasy-GUI textures over the
		// whole screen at 0.96 and 0.28 opacity before the title art drew; both are from the
		// pre-Bonelight asset set and they fought the composition. The title art is now a
		// single 360x640 image rendered at a whole x2 (see .ai/art-direction.md section 1).
		SetUpAnimation();
		FadeInPrompt();

		// Route menu music through the MusicPlayer autoload so it plays continuously from the title
		// screen through the menus and restarts when the player quits a run back to the menu.
		var musicPlayer = GetNodeOrNull<MusicPlayer>("/root/MusicPlayer");
		var menuMusic = ResourceLoader.Load<AudioStream>("res://assets/Pixel_Knights.mp3");
		if (musicPlayer != null && menuMusic != null)
			musicPlayer.PlayMusic(menuMusic);

		GD.Print("TitleScreen: _Ready() invoked");

		// Last, deliberately: everything the menu draws on top of has to exist before it is added.
		if (openMenuNow)
		{
			transitioned = true;
			ShowMenu();
		}
	}

	/// <summary>
	/// Find the animated layers and their frame sets. Every one of them is optional: if the
	/// frames are missing the scene still renders, it just holds on whatever the .tscn has, so
	/// a half-finished regeneration of the art never takes the title screen down.
	/// </summary>
	private void SetUpAnimation()
	{
		rng.Randomize();

		flamesFarRect = GetNodeOrNull<TextureRect>("TitleImage/FlamesFar");
		flamesNearRect = GetNodeOrNull<TextureRect>("TitleImage/FlamesNear");
		figureRect = GetNodeOrNull<TextureRect>("TitleImage/Figure");
		wardRect = GetNodeOrNull<TextureRect>("TitleImage/Ward");
		// Two eye layers, not one, and at different depths: the treeline set is behind the
		// ground, the creep and the canopy, the branch set is in front of all of it.
		eyesFarRect = GetNodeOrNull<TextureRect>("TitleImage/EyesFar");
		eyesNearRect = GetNodeOrNull<TextureRect>("TitleImage/EyesNear");

		flamesFar = LoadFrames("flames-far-", FlamePhases);
		flamesNear = LoadFrames("flames-near-", FlamePhases);
		figure = LoadFrames("figure-", FlamePhases);
		ward = LoadFrames("ward-", WardFrames);
		eyesFar = LoadFrames("eyes-far-", BlinkFrames);
		eyesNear = LoadFrames("eyes-near-", BlinkFrames);

		nextBlink = (float)rng.RandfRange(BlinkMinGap, BlinkMaxGap);
	}

	private static Texture2D[] LoadFrames(string prefix, int count)
	{
		var frames = new Texture2D[count];
		for (int i = 0; i < count; i++)
		{
			var path = $"res://assets/bonelight/ui/title/{prefix}{i}.png";
			frames[i] = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		}
		return frames[0] == null ? null : frames;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// The flames and the figure share one clock on purpose. The rim and bounce on the
		// wizard are the light those flames are throwing, so driven off a second timer they
		// read as two unrelated animations happening near each other rather than as one light
		// source and the thing it is lighting. The far and near flame layers share it too -
		// they are the same six flames with the figure standing between them.
		flameClock += dt;
		if (flameClock >= FlameFrameSeconds)
		{
			flameClock -= FlameFrameSeconds;
			flameStep = (flameStep + 1) % FlamePhases;
			Show(flamesFarRect, flamesFar, flameStep);
			Show(flamesNearRect, flamesNear, flameStep);
			Show(figureRect, figure, flameStep);
		}

		// The ward breathes: 0,1,2,1 rather than 0,1,2,0, so it turns round at the bottom
		// instead of snapping back to full brightness.
		wardClock += dt;
		if (wardClock >= WardPulseSeconds)
		{
			wardClock -= WardPulseSeconds;
			wardStep = (wardStep + 1) % (WardFrames * 2 - 2);
			Show(wardRect, ward, wardStep < WardFrames ? wardStep : WardFrames * 2 - 2 - wardStep);
		}

		// One pair at a time, on an irregular gap. Sixteen eyes blinking together is a
		// lighthouse, not a wood - so the gap is re-rolled after every blink.
		blinkClock += dt;
		if (blinkClock >= nextBlink)
		{
			if (blinkClock >= nextBlink + BlinkHoldSeconds)
			{
				blinkClock = 0f;
				nextBlink = (float)rng.RandfRange(BlinkMinGap, BlinkMaxGap);
				blinkFrame = 0;
			}
			else if (blinkFrame == 0)
			{
				blinkFrame = rng.RandiRange(1, BlinkFrames - 1);
			}
			// Each blink frame shuts exactly one pair, so only one of the two layers actually
			// changes - but both are set from the same index so they can never disagree.
			Show(eyesFarRect, eyesFar, blinkFrame);
			Show(eyesNearRect, eyesNear, blinkFrame);
		}
	}

	private static void Show(TextureRect rect, Texture2D[] frames, int index)
	{
		if (rect == null || frames == null)
			return;
		var next = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
		if (next != null && rect.Texture != next)
			rect.Texture = next;
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
			ShowMenu();
		}
	}

	/// <summary>Brings the main menu up on top of the artwork.</summary>
	/// <remarks>
	/// The menu is a CHILD of this screen rather than the next scene, and that is the whole change.
	/// Swapping scenes tore the artwork down and rebuilt it, which restarted every animated layer -
	/// so even though both screens showed the same picture, the handoff read as a flicker and the
	/// flames visibly jumped. Now the picture never goes away: the menu arrives on top of it, and
	/// the flames, eyes, ward pulse and blinking keep running behind it.
	///
	/// MainMenu hides its own background when it finds itself parented here (see
	/// MainMenu.LayoutAroundTitleArt), so there is exactly one copy of the art on screen.
	/// </remarks>
	private void ShowMenu()
	{
		if (menuInstance != null && IsInstanceValid(menuInstance))
			return;

		var scene = ResourceLoader.Load<PackedScene>("res://scenes/MainMenu.tscn");
		if (scene == null)
		{
			GD.PushError("TitleScreen: could not load res://scenes/MainMenu.tscn");
			return;
		}

		menuInstance = scene.Instantiate<Control>();
		AddChild(menuInstance);

		// The prompt and the menu say the same thing, so only one may be on screen. The breathing
		// tween is left running; it only touches modulate on a node nobody can see.
		var promptNode = GetNodeOrNull<CanvasItem>("TitleImage/Prompt");
		if (promptNode != null)
			promptNode.Visible = false;
	}
}
