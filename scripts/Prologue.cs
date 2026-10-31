using Godot;
using WizardSurvivors.scripts;

/// <summary>
/// The story-mode prologue: six generated panels (tools/art/prologue.py), each held a few seconds
/// with its layers drifting, three short lines between them, then on to wherever story mode was
/// going. See .ai/prologue.md.
/// </summary>
/// <remarks>
/// <para><b>When it plays.</b> The first time the player presses Start Run (MainMenu), before the
/// character select, and whenever they ask for it from Options. "Seen" is
/// <see cref="SaveData.HasSeenPrologue"/>, written when it ends - finished or skipped - and never by a
/// launch whose writes are off (every <c>_</c> tool scene, every <c>--no-save-writes</c> run).</para>
///
/// <para><b>The art is never scaled to fit.</b> Every layer is 960x840 drawn at a whole x2 into a
/// 1920x1680 rect centred on the canvas, the title screen's move in both directions at once: a PC
/// canvas (720 tall) shows rows 240-600, a phone canvas (720 wide) columns 300-660, and the 360x360
/// square both always show holds every subject. A screen bigger than the frame meets the black
/// ground, which the art fades into at its edges.</para>
///
/// <para><b>Nothing here tints or alpha-fades the art</b> (.ai/title-screen.md, "Animate by frame,
/// never by tint"). Panels change by a dissolve through the Bayer matrix, the one fade the contract
/// allows: every pixel is the old panel's or the new one's, never a blend. The drift moves layers by
/// whole screen pixels. Only the caption and the skip prompt fade - type on its own layer, as the
/// title's prompt does.</para>
/// </remarks>
public partial class Prologue : Control
{
	public const string ScenePath = "res://scenes/Prologue.tscn";
	private const string ArtFolder = "res://assets/bonelight/ui/prologue/";
	private const string CharacterSelectionPath = "res://scenes/CharacterSelection.tscn";
	private const string TitleScreenPath = "res://scenes/TitleScreen.tscn";

	// The frame, in art pixels, and its whole render scale. tools/art/prologue.py FW/FH.
	private static readonly Vector2 ArtSize = new(960f, 840f);
	private const float ArtScale = 2f;
	private static readonly string[] LayerNames = { "back", "mid", "fore" };

	/// <summary>Where the prologue goes when it ends.</summary>
	public enum After
	{
		/// <summary>On into story mode: the character select, as Start Run always did.</summary>
		Story,
		/// <summary>Back to the main menu it was opened from (Options, Watch the Prologue).</summary>
		Menu,
	}

	// Static because the prologue is a scene of its own: whoever opens it says where it goes next.
	private static After next = After.Story;

	/// <summary>Opens the prologue, which then continues to <paramref name="then"/>.</summary>
	public static void Play(Node from, After then)
	{
		if (from == null || !IsInstanceValid(from))
			return;
		next = then;
		from.GetTree().ChangeSceneToFile(ScenePath);
	}

	/// <summary>True when Start Run should show the prologue first: it has never been seen.</summary>
	public static bool IsUnseen(SaveManager saveManager) =>
		saveManager?.Data != null && !saveManager.Data.HasSeenPrologue;

	/// <summary>
	/// One panel: its art, how long it holds, its line (most have none - "keep text minimal"), and
	/// how far each layer drifts over the hold, in art pixels. Back drifts least and fore most, so
	/// the layers separate; a figure that moves (he rises, they march, the player walks) is its layer
	/// moving against the others.
	/// </summary>
	private readonly record struct Beat(string Name, float Seconds, string Caption, Vector2 Back, Vector2 Mid, Vector2 Fore);

	private static readonly Beat[] Beats =
	{
		// The circle where the wizards met, every one of their lights still in the world.
		new("moot", 4.6f, null, new Vector2(-4f, 0f), new Vector2(-10f, 0f), new Vector2(-26f, 0f)),
		// Him, rising over it, the light going out of them and into his hand.
		new("throne", 5.2f, "He came here first.", new Vector2(0f, 4f), new Vector2(0f, -12f), new Vector2(0f, 10f)),
		// His shadows over the land, coming down on a village that is still lit.
		new("tide", 4.6f, null, new Vector2(-3f, 0f), new Vector2(-14f, 4f), new Vector2(-22f, 0f)),
		// The wizards led away to his tower, their light drawn out of them.
		new("taken", 5.2f, "He took them all.", new Vector2(2f, 0f), new Vector2(14f, -2f), new Vector2(-20f, 0f)),
		// The title's picture, as story: the one he missed.
		new("missed", 5.4f, "You are the one he missed.", new Vector2(-4f, 0f), new Vector2(0f, -2f), new Vector2(18f, 0f)),
		// The road to all of it.
		new("road", 4.8f, null, new Vector2(0f, 2f), new Vector2(8f, -10f), new Vector2(0f, 16f)),
	};

	[Export] public float DissolveSeconds { get; set; } = 0.7f;
	[Export] public float HoldToSkipSeconds { get; set; } = 0.9f;
	[Export] public float CaptionInAt { get; set; } = 0.9f;
	[Export] public float CaptionFadeSeconds { get; set; } = 0.7f;

	// The dissolve: a pixel of the incoming panel shows once its Bayer threshold is under the
	// progress. Thresholds come from the layer's own art pixel (UV x art size), so the pattern sits on
	// the art grid at x2 and moves with the layer as it drifts.
	private const string DissolveShaderCode = @"
shader_type canvas_item;
uniform float progress : hint_range(0.0, 1.0) = 1.0;
uniform vec2 art_size = vec2(960.0, 840.0);

int bayer8(ivec2 p) {
	int x = p.x & 7;
	int y = p.y & 7;
	int xy = x ^ y;
	return ((xy & 1) << 5) | ((y & 1) << 4) | ((xy & 2) << 2) | ((y & 2) << 1) | ((xy & 4) >> 1) | ((y & 4) >> 2);
}

void fragment() {
	ivec2 p = ivec2(floor(UV * art_size));
	float threshold = (float(bayer8(p)) + 0.5) / 64.0;
	if (threshold > progress) {
		discard;
	}
}
";

	// One per prologue, shared by its panels. Not static: a Godot resource held by a C# static
	// outlives the engine's own cleanup and is reported at exit as a resource still in use.
	private Shader dissolveShader;

	private sealed class PanelView
	{
		public Control Root;
		public TextureRect[] Layers;
		public ShaderMaterial Material;
		public float Elapsed;
	}

	private readonly PanelView[] views = new PanelView[Beats.Length];
	private int current;
	private bool finished;
	private bool endingOut;

	private Control stage;
	private Label caption;
	private Button skipButton;
	private ColorRect skipFill;
	private bool skipHeldByButton;
	private bool skipHeldByCancel;
	private float skipHold;
	// The prompt rests at half strength so it does not compete with the art, and comes up to full
	// for a few seconds after any input - the moment a player is looking for a way out.
	private const float SkipRestAlpha = 0.5f;
	private const float SkipWakeSeconds = 2.5f;
	private float skipWake = SkipWakeSeconds;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		BuildUi();
		// The menu theme carries on from the title; idempotent, never a restart.
		GetNodeOrNull<MusicPlayer>("/root/MusicPlayer")?.PlayMenuMusic();

		ShowPanel(0);
		RequestPanel(1);

		// Clear of a phone's rounded corners and camera hole; nothing on desktop. The art is centre-
		// anchored and the ground full-screen, so only the caption and the skip prompt move.
		SafeArea.Apply(this);
		// The one control: Skip. Focused on arrival so a pad's A and the keyboard's Enter hold it.
		MenuNavigator.Attach(this, skipButton);
		GD.Print("Prologue: _Ready");
	}

	private void BuildUi()
	{
		var ground = new ColorRect { Name = "Ground", Color = new Color("05070c"), MouseFilter = MouseFilterEnum.Ignore };
		ground.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(ground);

		stage = new Control { Name = "Stage", MouseFilter = MouseFilterEnum.Ignore };
		stage.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(stage);

		caption = new Label
		{
			Name = "Caption",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		ResponsiveLayout.SetFont(caption, ResponsiveLayout.TextRole.Display);
		// Bone, the palette's pale stone, over an occlusion outline: the art behind every caption
		// is the darkened ground, but the outline keeps it legible whatever drifts under it.
		caption.AddThemeColorOverride("font_color", new Color("d4dcea"));
		caption.AddThemeColorOverride("font_outline_color", new Color("05070c"));
		caption.AddThemeConstantOverride("outline_size", 8);
		AddChild(caption);
		PlaceCaption();
		GetViewport().SizeChanged += PlaceCaption;

		// HOLD to skip, never tap: on a phone a stray touch must not throw away the story, and the
		// same rule for every device means the prompt says one thing. A button, so a pad and a
		// keyboard reach it through MenuNavigator; ButtonDown/ButtonUp fire for a mouse, a finger,
		// Enter and A alike. Back (Escape, B, Start) holds it too, from anywhere.
		skipButton = new Button
		{
			Name = "SkipButton",
			Text = "Hold to skip",
			AnchorLeft = 1f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetLeft = -236f,
			OffsetRight = -24f,
			OffsetTop = 24f,
			OffsetBottom = 76f,
			FocusMode = FocusModeEnum.All,
		};
		BonelightSkin.StyleButton(skipButton, BonelightSkin.Emphasis.Quiet);
		ResponsiveLayout.SetFont(skipButton, ResponsiveLayout.TextRole.Label);
		skipButton.ButtonDown += () => skipHeldByButton = true;
		skipButton.ButtonUp += () => skipHeldByButton = false;
		AddChild(skipButton);

		skipFill = new ColorRect
		{
			Name = "SkipFill",
			Color = new Color("d8b04a"),
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 0f,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			OffsetTop = -5f,
			OffsetBottom = -2f,
			OffsetLeft = 6f,
			OffsetRight = 6f,
		};
		skipButton.AddChild(skipFill);
	}

	public override void _ExitTree()
	{
		if (GetViewport() is Viewport viewport)
			viewport.SizeChanged -= PlaceCaption;
	}

	// The caption sits near the foot of what the screen shows. Every panel darkens its ground
	// toward the frame edge, so on a PC (rows 560-600 of the frame) and on a phone (rows ~690-790)
	// the line lands on the darkened ground under the subject, never across it.
	private void PlaceCaption()
	{
		if (caption == null)
			return;
		Vector2 size = GetViewportRect().Size;
		bool portrait = size.Y > size.X;
		float bottom = portrait ? 120f : 44f;
		caption.OffsetLeft = portrait ? 36f : 120f;
		caption.OffsetRight = portrait ? -36f : -120f;
		caption.OffsetBottom = -bottom;
		caption.OffsetTop = -bottom - 120f;
	}

	// --- panels -----------------------------------------------------------------------------
	private static string LayerPath(string beat, string layer) => $"{ArtFolder}{beat}-{layer}.png";

	/// <summary>Starts loading a panel's three layers on a worker thread, a panel ahead.</summary>
	private static void RequestPanel(int index)
	{
		if (index < 0 || index >= Beats.Length)
			return;
		foreach (string layer in LayerNames)
		{
			string path = LayerPath(Beats[index].Name, layer);
			if (ResourceLoader.Exists(path))
				ResourceLoader.LoadThreadedRequest(path, "Texture2D");
		}
	}

	private static Texture2D TakeLayer(string path)
	{
		if (!ResourceLoader.Exists(path))
		{
			GD.PushError($"Prologue: missing panel layer {path}. Run: python tools/art/prologue.py, then import.");
			return null;
		}
		// Blocks only if the prefetch has not finished; a path never requested loads here.
		ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path);
		if (status == ResourceLoader.ThreadLoadStatus.InvalidResource)
			return GD.Load<Texture2D>(path);
		return ResourceLoader.LoadThreadedGet(path) as Texture2D ?? GD.Load<Texture2D>(path);
	}

	private void ShowPanel(int index)
	{
		Beat beat = Beats[index];
		dissolveShader ??= new Shader { Code = DissolveShaderCode };
		var material = new ShaderMaterial { Shader = dissolveShader };
		material.SetShaderParameter("progress", 0f);
		material.SetShaderParameter("art_size", ArtSize);

		Vector2 half = ArtSize * ArtScale * 0.5f;
		var root = new Control
		{
			Name = "Panel_" + beat.Name,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -half.X,
			OffsetRight = half.X,
			OffsetTop = -half.Y,
			OffsetBottom = half.Y,
			Material = material,
		};
		var layers = new TextureRect[LayerNames.Length];
		for (int i = 0; i < LayerNames.Length; i++)
		{
			layers[i] = new TextureRect
			{
				Name = LayerNames[i],
				Texture = TakeLayer(LayerPath(beat.Name, LayerNames[i])),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				TextureFilter = TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore,
				UseParentMaterial = true,
				Size = ArtSize * ArtScale,
			};
			root.AddChild(layers[i]);
		}
		stage.AddChild(root);
		views[index] = new PanelView { Root = root, Layers = layers, Material = material };
		ApplyDrift(index);
	}

	private void ApplyDrift(int index)
	{
		PanelView view = views[index];
		if (view == null)
			return;
		Beat beat = Beats[index];
		// -0.5 to +0.5 over the hold, so the picture the generator composed is the one on screen
		// mid-panel, and every layer stays within half its drift of where it was drawn.
		float t = Mathf.Clamp(view.Elapsed / beat.Seconds, 0f, 1f) - 0.5f;
		// Whole SCREEN pixels: a layer at a fraction of one would be resampled (.ai/art-direction.md 6a).
		float perUnit = Mathf.Max(0.01f, GetViewport().GetFinalTransform().X.X);
		Vector2[] drift = { beat.Back, beat.Mid, beat.Fore };
		for (int i = 0; i < view.Layers.Length; i++)
		{
			Vector2 offset = drift[i] * t * ArtScale;
			view.Layers[i].Position = new Vector2(Mathf.Round(offset.X * perUnit), Mathf.Round(offset.Y * perUnit)) / perUnit;
		}
	}

	public override void _Process(double delta)
	{
		if (finished)
			return;
		float dt = (float)delta;
		UpdateSkip(dt);
		if (finished)
			return;

		for (int i = 0; i < views.Length; i++)
		{
			if (views[i] == null)
				continue;
			views[i].Elapsed += dt;
			ApplyDrift(i);
		}

		PanelView now = views[current];
		Beat beat = Beats[current];
		if (!endingOut)
			now.Material.SetShaderParameter("progress", Mathf.Clamp(now.Elapsed / DissolveSeconds, 0f, 1f));

		int nextIndex = current + 1;
		if (nextIndex < Beats.Length)
		{
			// The next panel dissolves in over the last moments of this one, which keeps drifting
			// underneath it; once it covers the screen this one is freed.
			if (views[nextIndex] == null && now.Elapsed >= beat.Seconds - DissolveSeconds)
			{
				ShowPanel(nextIndex);
				RequestPanel(nextIndex + 1);
			}
			PanelView incoming = views[nextIndex];
			if (incoming != null)
			{
				incoming.Material.SetShaderParameter("progress", Mathf.Clamp(incoming.Elapsed / DissolveSeconds, 0f, 1f));
				if (incoming.Elapsed >= DissolveSeconds)
				{
					now.Root.QueueFree();
					views[current] = null;
					current = nextIndex;
				}
			}
		}
		else if (now.Elapsed >= beat.Seconds)
		{
			// The last panel dissolves out to the ground, and then story mode.
			endingOut = true;
			float progress = 1f - (now.Elapsed - beat.Seconds) / DissolveSeconds;
			now.Material.SetShaderParameter("progress", Mathf.Clamp(progress, 0f, 1f));
			if (progress <= 0f)
				Finish();
		}

		UpdateCaption();
	}

	private void UpdateCaption()
	{
		PanelView view = views[current];
		Beat beat = Beats[current];
		if (view == null || string.IsNullOrEmpty(beat.Caption))
		{
			caption.Modulate = new Color(1f, 1f, 1f, 0f);
			return;
		}
		if (caption.Text != beat.Caption)
			caption.Text = beat.Caption;
		float e = view.Elapsed;
		float fadeIn = Mathf.Clamp((e - CaptionInAt) / CaptionFadeSeconds, 0f, 1f);
		// Gone before the next panel starts to dissolve in, so a line never sits over two pictures.
		float outStart = beat.Seconds - DissolveSeconds - CaptionFadeSeconds;
		float fadeOut = 1f - Mathf.Clamp((e - outStart) / CaptionFadeSeconds, 0f, 1f);
		caption.Modulate = new Color(1f, 1f, 1f, Mathf.Min(fadeIn, fadeOut));
	}

	// --- skipping -----------------------------------------------------------------------------
	// Only watches: nothing is consumed here, so the button still gets its press.
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true }
			or InputEventScreenTouch { Pressed: true } or InputEventJoypadButton { Pressed: true })
			skipWake = SkipWakeSeconds;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			skipHeldByCancel = true;
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionReleased("ui_cancel"))
		{
			skipHeldByCancel = false;
			GetViewport().SetInputAsHandled();
		}
	}

	private void UpdateSkip(float dt)
	{
		// The button's own draw state as well as its signals: a finger dragged off the button stops
		// holding it, and a release that lands elsewhere must not leave the hold running.
		bool buttonDown = skipHeldByButton && skipButton.GetDrawMode() is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed;
		bool holding = buttonDown || skipHeldByCancel;
		skipHold = holding ? skipHold + dt : Mathf.Max(0f, skipHold - dt * 3f);
		float fill = Mathf.Clamp(skipHold / HoldToSkipSeconds, 0f, 1f);
		skipFill.OffsetRight = 6f + (skipButton.Size.X - 12f) * fill;
		skipFill.Visible = fill > 0f;
		skipWake = Mathf.Max(0f, skipWake - dt);
		float alpha = holding || skipWake > 0f ? 1f : SkipRestAlpha;
		skipButton.Modulate = new Color(1f, 1f, 1f, Mathf.MoveToward(skipButton.Modulate.A, alpha, dt * 2.5f));
		if (fill >= 1f)
			Finish();
	}

	/// <summary>Ends the prologue - played through or skipped - records it as seen, and moves on.</summary>
	private void Finish()
	{
		if (finished)
			return;
		finished = true;
		MarkSeen();
		After then = next;
		next = After.Story;
		if (then == After.Menu)
		{
			Global.OpenMenuImmediately = true;
			GetTree().ChangeSceneToFile(TitleScreenPath);
		}
		else
		{
			GetTree().ChangeSceneToFile(CharacterSelectionPath);
		}
	}

	private void MarkSeen()
	{
		// A tool scene or a --no-save-writes run never marks it: the flag is the player's, and a
		// screenshot that played the prologue is not the player having seen it.
		if (SaveManager.WritesDisabled)
			return;
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager?.Data == null || saveManager.Data.HasSeenPrologue)
			return;
		saveManager.Data.HasSeenPrologue = true;
		saveManager.SaveGame();
	}
}
