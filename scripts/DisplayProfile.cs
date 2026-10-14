using Godot;
using System;

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
	}
}
