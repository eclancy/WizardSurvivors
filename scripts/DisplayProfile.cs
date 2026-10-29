using Godot;
using System;
using WizardSurvivors.scripts;

/// <summary>
/// Picks the game's canvas shape at startup: landscape on a PC, portrait on a phone.
/// </summary>
/// <remarks>
/// The game was laid out for one shape only - a 720x1280 portrait canvas - and a PC ran it in a tall
/// narrow window. Now a PC gets a 1280x720 landscape canvas (stretch aspect keep_height, so a wider
/// monitor gets more width, never a smaller game) and a phone keeps 720x1280 (keep_width). The short
/// side is 720 on both, which is what keeps every font size and touch target the same physical size.
///
/// project.godot carries the same split as feature-tag overrides (".mobile") so the very first frame
/// on a phone is already portrait; this node is what makes it switchable - <c>--portrait</c> on the
/// command line gives a desktop window the phone's canvas, which is how phone layouts are checked
/// through _UiShot (with --resolution 540x1212, the Pixel 9's shape).
///
/// An autoload, so it runs before the first scene lays itself out.
/// </remarks>
public partial class DisplayProfile : Node
{
	public static readonly Vector2I LandscapeCanvas = new(1280, 720);
	public static readonly Vector2I PortraitCanvas = new(720, 1280);

	/// <summary>True on a phone, or on a desktop launched with --portrait.</summary>
	public static bool IsPortrait { get; private set; }

	public override void _EnterTree()
	{
		IsPortrait = OS.HasFeature("mobile")
			|| Array.IndexOf(OS.GetCmdlineUserArgs(), "--portrait") >= 0
			|| Array.IndexOf(OS.GetCmdlineArgs(), "--portrait") >= 0;

		Window root = GetTree().Root;
		if (IsPortrait)
		{
			root.ContentScaleSize = PortraitCanvas;
			root.ContentScaleAspect = Window.ContentScaleAspectEnum.KeepWidth;
		}
		else
		{
			root.ContentScaleSize = LandscapeCanvas;
			root.ContentScaleAspect = Window.ContentScaleAspectEnum.KeepHeight;
		}

		// F11 and Alt+Enter work over every screen, the pause screen and the level-up included.
		ProcessMode = ProcessModeEnum.Always;
		askedFullscreen = StartsFullscreen();
		if (askedFullscreen)
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		if (DisplayServer.GetName() != "headless")
			CallDeferred(MethodName.LogWindow);
	}

	private bool askedFullscreen;

	private void LogWindow() =>
		GD.Print($"DisplayProfile: {(askedFullscreen ? "fullscreen asked" : "windowed launch")}; the window is "
			+ $"{DisplayServer.WindowGetMode()} at {DisplayServer.WindowGetSize()} (F11 or Alt+Enter toggles fullscreen)");

	// 2026-10-29: "wizard survivors should start in fullscreen when i start the game". Set here rather
	// than as window/size/mode in project.godot, because the tools photograph exact canvases with
	// --resolution (_UiShot at 1280x720, the Pixel 9's 540x1212) and a fullscreen window takes the
	// screen's size instead. Godot consumes its own flags (--windowed, --resolution, --position): none
	// of them reaches OS.GetCmdlineArgs, so they cannot be read here. What can be: every tool is an
	// underscore scene (SaveManager.IsToolSceneLaunch), the phone canvas (IsPortrait, the phone and
	// --portrait), headless, and the user argument "-- --windowed" for a window by hand. Fullscreen is
	// the borderless kind, so alt-tab still works.
	private static bool StartsFullscreen() =>
		!IsPortrait
		&& DisplayServer.GetName() != "headless"
		&& !SaveManager.IsToolSceneLaunch()
		&& Array.IndexOf(OS.GetCmdlineUserArgs(), "--windowed") < 0;

	// F11 or Alt+Enter: fullscreen to a window and back, for this session. Read in _Input, before the
	// menus: Enter is ui_accept, and a menu would otherwise press its focused button on Alt+Enter.
	public override void _Input(InputEvent @event)
	{
		if (OS.HasFeature("mobile") || @event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		bool toggle = key.Keycode == Key.F11
			|| (key.AltPressed && key.Keycode is Key.Enter or Key.KpEnter);
		if (!toggle)
			return;
		GetViewport().SetInputAsHandled();
		DisplayServer.WindowMode mode = DisplayServer.WindowGetMode();
		if (mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen)
			ToWindow();
		else
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		CallDeferred(MethodName.LogWindow);
	}

	// Out of fullscreen into the window the project opens (window_width/height_override, 1600x900), no
	// larger than nine tenths of the screen, centred. A game that started fullscreen was never shown in a
	// window, so there is no window size to go back to, and it came back the size of the screen.
	private static void ToWindow()
	{
		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		int screen = DisplayServer.WindowGetCurrentScreen();
		Rect2I usable = DisplayServer.ScreenGetUsableRect(screen);
		var wanted = new Vector2I(
			(int)ProjectSettings.GetSetting("display/window/size/window_width_override", 1600),
			(int)ProjectSettings.GetSetting("display/window/size/window_height_override", 900));
		float fit = Mathf.Min(1f, Mathf.Min(usable.Size.X * 0.9f / wanted.X, usable.Size.Y * 0.9f / wanted.Y));
		var size = new Vector2I(Mathf.RoundToInt(wanted.X * fit), Mathf.RoundToInt(wanted.Y * fit));
		DisplayServer.WindowSetSize(size);
		DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size) / 2);
	}
}
