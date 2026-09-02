using Godot;
using System;
using System.Linq;
using WizardSurvivors.scripts;

/// <summary>
/// Floating virtual joystick for touch play.
///
/// The game is an auto-shooter - spells fire on their own timers - so movement is the only thing a
/// touch player needs to drive. Rather than pinning a stick to a fixed corner, the pad covers the
/// whole play area below the HUD band: wherever the player puts a thumb down becomes the centre,
/// and dragging from there steers. That avoids the usual problem of a fixed stick being under the
/// wrong thumb, and it means no part of the screen is wasted on chrome.
///
/// <see cref="Player"/> reads <see cref="MoveVector"/> and folds it into the same input vector the
/// keyboard and gamepad produce, so nothing downstream has to know touch exists.
/// </summary>
public partial class TouchControls : CanvasLayer
{
	/// <summary>Emitted when the on-screen pause button is tapped.</summary>
	[Signal] public delegate void PauseRequestedEventHandler();

	// Below the synergy strip (6) and every menu (10+) so their controls win both the input pick
	// and the draw order, and above the arena so the pad still covers it.
	private const int PadLayer = 5;

	// How far the thumb travels for full-speed movement, in viewport units. At the 720-wide base
	// resolution this is a quarter of the screen width - enough travel to be analog, short enough
	// to reach without shifting grip.
	[Export] public float MaxRadius { get; set; } = 90f;

	// Below this fraction of MaxRadius the stick reads as centred. Stops a stationary thumb from
	// creeping the player around.
	[Export] public float DeadZone { get; set; } = 0.15f;

	// Height of the reserved band at the top of the screen. Touches there are left for the HUD -
	// the synergy strip, the pause button - instead of yanking the player sideways.
	[Export] public float HudBandHeight { get; set; } = 176f;

	/// <summary>
	/// The live instance, if any. Cleared in <c>_ExitTree</c> so a stale vector can never survive
	/// a scene change and walk the next run's player into a wall.
	/// </summary>
	public static TouchControls? ActiveInstance { get; private set; }

	/// <summary>Current stick deflection, already dead-zoned and clamped to unit length.</summary>
	public static Vector2 MoveVector =>
		ActiveInstance != null && GodotObject.IsInstanceValid(ActiveInstance)
			? ActiveInstance.moveVector
			: Vector2.Zero;

	private Control pad = null!;
	private Button pauseButton = null!;
	private Vector2 moveVector = Vector2.Zero;
	private Vector2 stickOrigin;
	private Vector2 stickTip;
	private bool stickActive;

	// Index of the finger that owns the stick. A second finger tapping a HUD icon must not steal
	// it, and lifting an unrelated finger must not release it.
	private int activeTouchIndex = -1;

	// Once a genuine touch arrives, ignore mouse events entirely: with
	// emulate_mouse_from_touch (on by default) every tap also arrives as a click, and handling
	// both would drive the stick twice per frame.
	private bool sawRealTouch;

	// Mouse only steers when --touch-controls was passed explicitly. A Windows laptop with a
	// touchscreen reports IsTouchscreenAvailable() and gets the pad, and on that machine a plain
	// left-click in the arena should not suddenly walk the player somewhere.
	private bool allowMouseFallback;

	/// <summary>
	/// True when this build should show the pad: a real touchscreen, a mobile export, or the
	/// <c>--touch-controls</c> command-line flag, which is how you test the layout on a desktop.
	/// </summary>
	public static bool ShouldEnable()
	{
		return OS.HasFeature("mobile") || DisplayServer.IsTouchscreenAvailable() || IsForcedByFlag();
	}

	// Both lists: Godot routes anything after a bare "--" into the user args instead.
	private static bool IsForcedByFlag()
	{
		return OS.GetCmdlineArgs().Contains("--touch-controls")
			|| OS.GetCmdlineUserArgs().Contains("--touch-controls");
	}

	public override void _Ready()
	{
		Layer = PadLayer;
		// Always, not the default Pausable: a paused node keeps whatever it last drew on screen,
		// so a stick frozen mid-drag would hang over the level-up menu. Running lets ReleaseStick
		// clear it the moment the tree pauses.
		ProcessMode = ProcessModeEnum.Always;
		ActiveInstance = this;
		allowMouseFallback = IsForcedByFlag();

		pad = new Control
		{
			Name = "JoystickPad",
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetTop = HudBandHeight
		};
		pad.GuiInput += OnPadInput;
		pad.Draw += OnPadDraw;
		AddChild(pad);

		// Added after the pad, in the same layer, so it wins the input pick over the pad beneath
		// it. Keeping it here rather than in its own higher layer is what stops it from painting
		// over the level-up and chest menus.
		pauseButton = BuildPauseButton();
		AddChild(pauseButton);
	}

	// There is no Escape key on a handset, so without this the only way out of a run is killing
	// the app.
	private Button BuildPauseButton()
	{
		var button = new Button
		{
			Name = "TouchPauseButton",
			Text = "II",
			TooltipText = "Pause",
			FocusMode = Control.FocusModeEnum.None,
			AnchorLeft = 1f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 0f,
			OffsetLeft = -56f,
			OffsetTop = 8f,
			OffsetRight = -8f,
			OffsetBottom = 56f
		};
		button.AddThemeFontSizeOverride("font_size", 20);
		ResponsiveLayout.EnsureTouchTarget(button);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.11f, 0.16f, 0.88f),
			BorderColor = new Color(0.36f, 0.40f, 0.52f, 0.85f)
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(8);
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", style);
		button.AddThemeStyleboxOverride("pressed", style);
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Pressed += () => EmitSignal(SignalName.PauseRequested);
		return button;
	}

	public override void _ExitTree()
	{
		if (ActiveInstance == this)
			ActiveInstance = null;

		if (pad != null)
		{
			pad.GuiInput -= OnPadInput;
			pad.Draw -= OnPadDraw;
		}
	}

	public override void _Process(double delta)
	{
		bool paused = GetTree().Paused;

		// A menu opening mid-drag must not leave the player sliding, and must not leave a stick
		// painted over the menu.
		if (stickActive && paused)
			ReleaseStick();

		// Nothing to pause while a menu already owns the screen.
		if (pauseButton != null)
			pauseButton.Visible = !paused;
	}

	private void OnPadInput(InputEvent @event)
	{
		if (GetTree().Paused)
			return;

		switch (@event)
		{
			case InputEventScreenTouch touch:
				sawRealTouch = true;
				if (touch.Pressed)
					GrabStick(touch.Index, touch.Position);
				else if (touch.Index == activeTouchIndex)
					ReleaseStick();
				pad.AcceptEvent();
				break;

			case InputEventScreenDrag drag when drag.Index == activeTouchIndex:
				MoveStick(drag.Position);
				pad.AcceptEvent();
				break;

			// Mouse fallback so the pad can be driven - and seen - on a desktop with
			// --touch-controls. Index -1 keeps it distinct from any real finger.
			case InputEventMouseButton mouse when allowMouseFallback && !sawRealTouch && mouse.ButtonIndex == MouseButton.Left:
				if (mouse.Pressed)
					GrabStick(-1, mouse.Position);
				else if (activeTouchIndex == -1)
					ReleaseStick();
				pad.AcceptEvent();
				break;

			case InputEventMouseMotion motion when allowMouseFallback && !sawRealTouch && stickActive && activeTouchIndex == -1:
				MoveStick(motion.Position);
				pad.AcceptEvent();
				break;
		}
	}

	private void GrabStick(int index, Vector2 localPosition)
	{
		if (stickActive)
			return;

		stickActive = true;
		activeTouchIndex = index;

		// Pull the centre in from the edges so a thumb landing near a border still has the full
		// travel available in every direction.
		Vector2 padSize = pad.Size;
		stickOrigin = new Vector2(
			Mathf.Clamp(localPosition.X, MaxRadius, Mathf.Max(MaxRadius, padSize.X - MaxRadius)),
			Mathf.Clamp(localPosition.Y, MaxRadius, Mathf.Max(MaxRadius, padSize.Y - MaxRadius)));
		stickTip = localPosition;
		UpdateVector();
	}

	private void MoveStick(Vector2 localPosition)
	{
		if (!stickActive)
			return;

		stickTip = localPosition;
		UpdateVector();
	}

	private void ReleaseStick()
	{
		stickActive = false;
		activeTouchIndex = -1;
		moveVector = Vector2.Zero;
		pad.QueueRedraw();
	}

	private void UpdateVector()
	{
		Vector2 offset = stickTip - stickOrigin;
		float deflection = Mathf.Min(offset.Length() / MaxRadius, 1f);
		moveVector = deflection < DeadZone ? Vector2.Zero : offset.Normalized() * deflection;
		pad.QueueRedraw();
	}

	private void OnPadDraw()
	{
		if (!stickActive)
			return;

		// Deliberately low contrast: the stick is feedback, not a control the player has to look
		// at, and a bright ring in the middle of a swarm hides enemies.
		pad.DrawCircle(stickOrigin, MaxRadius, new Color(0.85f, 0.90f, 1.0f, 0.10f));
		pad.DrawArc(stickOrigin, MaxRadius, 0f, Mathf.Tau, 48, new Color(0.85f, 0.90f, 1.0f, 0.30f), 2f);

		Vector2 knob = stickOrigin + (stickTip - stickOrigin).LimitLength(MaxRadius);
		pad.DrawCircle(knob, MaxRadius * 0.34f, new Color(0.92f, 0.95f, 1.0f, 0.26f));
		pad.DrawArc(knob, MaxRadius * 0.34f, 0f, Mathf.Tau, 32, new Color(0.98f, 0.99f, 1.0f, 0.55f), 2f);
	}
}
