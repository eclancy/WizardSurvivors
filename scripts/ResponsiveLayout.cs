using Godot;
using System;

namespace WizardSurvivors.scripts;

// Shared helpers for making menus fit a portrait phone as well as a desktop window.
//
// The project's base viewport is 720x1280 with stretch aspect "keep_width", so the width every
// menu gets is fixed at 720 units while the height varies with the device's aspect. That makes
// *width* the scarce axis: a card grid authored with three fixed-width columns overflows both
// edges on a phone. Screens ask ColumnsFor() how many columns actually fit instead of hardcoding
// a count, and re-ask whenever the viewport changes size.
public static class ResponsiveLayout
{
	// Below this width a layout should treat itself as a phone: single-column lists, stacked
	// rows rather than side-by-side ones, and no long single-line status strings.
	public const float NarrowWidthThreshold = 820f;

	public static Vector2 ViewportSize(CanvasItem node)
	{
		return node?.GetViewportRect().Size ?? new Vector2(720f, 1280f);
	}

	public static bool IsNarrow(CanvasItem node)
	{
		return ViewportSize(node).X < NarrowWidthThreshold;
	}

	public static bool IsPortrait(CanvasItem node)
	{
		Vector2 size = ViewportSize(node);
		return size.Y > size.X;
	}

	// The most columns of `minItemWidth` that fit across the viewport, clamped to [1, maxColumns].
	// `sidePadding` is the total horizontal chrome (margins on both sides) to keep clear.
	public static int ColumnsFor(CanvasItem node, float minItemWidth, int maxColumns,
		float sidePadding = 32f, float separation = 16f)
	{
		if (minItemWidth <= 0f)
			return Math.Max(1, maxColumns);

		float available = Math.Max(0f, ViewportSize(node).X - sidePadding);
		// n columns need n*width + (n-1)*separation.
		int fit = (int)Math.Floor((available + separation) / (minItemWidth + separation));
		return Math.Clamp(fit, 1, Math.Max(1, maxColumns));
	}

	// Applies ColumnsFor to a GridContainer and keeps it correct across window resizes and
	// device rotation. Safe to call more than once - the signal is only connected on the first.
	public static void BindGridColumns(GridContainer grid, float minItemWidth, int maxColumns,
		float sidePadding = 32f)
	{
		if (grid == null)
			return;

		float separation = grid.GetThemeConstant("h_separation");
		void Apply() => grid.Columns = ColumnsFor(grid, minItemWidth, maxColumns, sidePadding, separation);

		Apply();
		var viewport = grid.GetViewport();
		if (viewport != null && !viewport.IsConnected(Viewport.SignalName.SizeChanged, Callable.From(Apply)))
			viewport.SizeChanged += Apply;
	}

	// Minimum edge length for anything the player taps. Apple's HIG asks for 44pt and Material
	// for 48dp; 48 base units at this viewport lands comfortably above both on a real handset.
	public const float MinTouchTarget = 48f;

	// Grows a control to at least MinTouchTarget on both axes without shrinking a deliberately
	// larger one.
	public static void EnsureTouchTarget(Control control, float minWidth = MinTouchTarget)
	{
		if (control == null)
			return;

		Vector2 size = control.CustomMinimumSize;
		control.CustomMinimumSize = new Vector2(
			Math.Max(size.X, minWidth),
			Math.Max(size.Y, MinTouchTarget));
	}
}
