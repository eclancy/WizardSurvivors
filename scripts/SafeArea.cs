using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Keeps edge-anchored UI clear of a phone's rounded corners and camera hole.
/// </summary>
/// <remarks>
/// On a Pixel 9 the HUD's health bar sat under the top-left corner's curve, the pause button under
/// the top-right one, the run timer under the punch-hole camera, and every screen's Back button in
/// a corner. Godot's DisplayServer.GetDisplaySafeArea reports the camera cutout but knows nothing
/// about rounded corners, so the insets here are the larger of what Android reports and a fixed
/// floor that clears the corner curve.
///
/// How it moves things: a screen calls <see cref="Apply"/> once it has built itself. Every direct
/// Control child of the screen (or of a CanvasLayer) that is anchored to an edge is pulled in from
/// that edge - shifted if it is pinned to one side, shrunk if it spans both. Centre-anchored nodes
/// are left alone, which is what keeps the title art at its exact x2 and the menu furniture that
/// MainMenu.PinToArtRows places against it. Full-screen backgrounds (a ColorRect or TextureRect
/// stretched over the whole screen) are skipped too: a background that stops short of the corner
/// would draw the very border this exists to hide.
///
/// Off on desktop. <c>--safe-area</c> on the command line forces it on, to check the layout there.
/// </remarks>
public static class SafeArea
{
	// Floors in canvas units (the 720-wide canvas). A Pixel 9's corner curve needs about 28 on the
	// sides; top and bottom take more because the corner and the camera hole both sit there, and a
	// strip of HUD across the very top is exactly what a punch-hole covers.
	public const float MinSide = 28f;
	public const float MinTop = 56f;
	public const float MinBottom = 36f;

	private const string AppliedMeta = "safe_area_applied";

	public static bool Enabled =>
		OS.HasFeature("mobile")
		|| Array.IndexOf(OS.GetCmdlineUserArgs(), "--safe-area") >= 0
		|| Array.IndexOf(OS.GetCmdlineArgs(), "--safe-area") >= 0;

	/// <summary>Left, top, right, bottom insets in canvas units. All zero when not enabled.</summary>
	public static (float Left, float Top, float Right, float Bottom) Insets(Viewport viewport)
	{
		if (!Enabled || viewport == null)
			return (0f, 0f, 0f, 0f);

		float left = MinSide, top = MinTop, right = MinSide, bottom = MinBottom;

		// What Android reports, converted from screen pixels to canvas units. On a phone the window
		// is the screen, so the safe rect's distance from each screen edge is the inset.
		Vector2I screen = DisplayServer.ScreenGetSize();
		Rect2I safe = DisplayServer.GetDisplaySafeArea();
		Vector2 canvas = viewport.GetVisibleRect().Size;
		if (screen.X > 0 && screen.Y > 0 && safe.Size.X > 0 && safe.Size.Y > 0)
		{
			float sx = canvas.X / screen.X;
			float sy = canvas.Y / screen.Y;
			left = Mathf.Max(left, safe.Position.X * sx);
			top = Mathf.Max(top, safe.Position.Y * sy);
			right = Mathf.Max(right, (screen.X - safe.End.X) * sx);
			bottom = Mathf.Max(bottom, (screen.Y - safe.End.Y) * sy);
		}

		return (left, top, right, bottom);
	}

	/// <summary>
	/// Pulls every edge-anchored Control child of <paramref name="root"/> in from the edges it is
	/// pinned to. Call once a screen has built itself; calling it twice on the same child is harmless.
	/// </summary>
	public static void Apply(Node root)
	{
		if (!Enabled || root == null)
			return;

		var insets = Insets(root.GetViewport());
		foreach (Node child in root.GetChildren())
		{
			if (child is Control control)
				Inset(control, insets);
		}
	}

	/// <summary>Insets one control, whatever its parent. For UI added after <see cref="Apply"/> ran.</summary>
	public static void ApplyTo(Control control)
	{
		if (!Enabled || control == null)
			return;
		Inset(control, Insets(control.GetViewport()));
	}

	private static void Inset(Control control, (float Left, float Top, float Right, float Bottom) insets)
	{
		if (control.HasMeta(AppliedMeta) || IsFullScreenBackdrop(control))
			return;
		control.SetMeta(AppliedMeta, true);

		const float Edge = 0.001f;
		bool pinLeft = control.AnchorLeft < Edge;
		bool pinRight = control.AnchorRight > 1f - Edge;
		bool pinTop = control.AnchorTop < Edge;
		bool pinBottom = control.AnchorBottom > 1f - Edge;

		// Pinned to the left only (both anchors at 0): shift right. To the right only: shift left.
		// To both: shrink from both sides. Anchored anywhere in between: leave it.
		if (pinLeft && control.AnchorRight < Edge)
		{
			control.OffsetLeft += insets.Left;
			control.OffsetRight += insets.Left;
		}
		else if (pinRight && control.AnchorLeft > 1f - Edge)
		{
			control.OffsetLeft -= insets.Right;
			control.OffsetRight -= insets.Right;
		}
		else if (pinLeft && pinRight)
		{
			control.OffsetLeft += insets.Left;
			control.OffsetRight -= insets.Right;
		}

		if (pinTop && control.AnchorBottom < Edge)
		{
			control.OffsetTop += insets.Top;
			control.OffsetBottom += insets.Top;
		}
		else if (pinBottom && control.AnchorTop > 1f - Edge)
		{
			control.OffsetTop -= insets.Bottom;
			control.OffsetBottom -= insets.Bottom;
		}
		else if (pinTop && pinBottom)
		{
			control.OffsetTop += insets.Top;
			control.OffsetBottom -= insets.Bottom;
		}
	}

	// A ColorRect or TextureRect stretched over the whole screen is scenery: it has to reach the
	// corners, or the curve shows a hard-edged border instead of the game.
	private static bool IsFullScreenBackdrop(Control control)
	{
		bool fullRect = control.AnchorLeft < 0.001f && control.AnchorTop < 0.001f
			&& control.AnchorRight > 0.999f && control.AnchorBottom > 0.999f;
		return fullRect && (control is ColorRect || control is TextureRect);
	}
}
