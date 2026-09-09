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

	// Takes Node rather than CanvasItem so CanvasLayer-based menus (LevelUpMenu,
	// ChestItemSelectionMenu) can ask too - CanvasLayer is not a CanvasItem.
	public static Vector2 ViewportSize(Node node)
	{
		return node?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(720f, 1280f);
	}

	public static bool IsNarrow(Node node)
	{
		return ViewportSize(node).X < NarrowWidthThreshold;
	}

	public static bool IsPortrait(Node node)
	{
		Vector2 size = ViewportSize(node);
		return size.Y > size.X;
	}

	// The most columns of `minItemWidth` that fit across the viewport, clamped to [1, maxColumns].
	// `sidePadding` is the total horizontal chrome (margins on both sides) to keep clear.
	public static int ColumnsFor(Node node, float minItemWidth, int maxColumns,
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

	// ---- Type scale ----------------------------------------------------------------------
	//
	// Font sizes were being chosen per call site, and the result was 57 uses of size 15 or
	// smaller across nine menus - 21 of them in LevelUpMenu alone, with a floor of 10. On a
	// viewport that is 720 units wide, body copy at 11 is about 1.5% of the screen's width;
	// the usual guidance for something a person reads on a handset is nearer 4%. The level-up
	// menu is the worst case because it is also the one screen the player is *forced* to read
	// under time pressure, several times a run.
	//
	// The fix is not to bump the numbers at each call site. It is to have a scale, so that two
	// labels that mean the same thing cannot drift apart - which is how 13 different sizes for
	// five jobs happened in the first place.
	//
	// Each step carries a narrow (phone) and a wide (desktop) size. Narrow is LARGER: a phone
	// is held closer but has less width, so the same information has fewer characters per line
	// and each has to work harder.
	public enum TextRole
	{
		/// <summary>Screen heading. One per menu.</summary>
		Display,
		/// <summary>Card and section titles - the thing being chosen.</summary>
		Title,
		/// <summary>Everything the player actually reads to decide.</summary>
		Body,
		/// <summary>Secondary lines: costs, levels, "owned", element tags.</summary>
		Label,
		/// <summary>Numerals and chips only. The floor - never running prose.</summary>
		Micro,
	}

	public static int FontSize(Node node, TextRole role)
	{
		bool narrow = IsNarrow(node);
		return role switch
		{
			TextRole.Display => narrow ? 34 : 28,
			TextRole.Title => narrow ? 26 : 22,
			TextRole.Body => narrow ? 20 : 18,
			TextRole.Label => narrow ? 18 : 16,
			TextRole.Micro => narrow ? 16 : 14,
			_ => narrow ? 20 : 18,
		};
	}

	/// <summary>
	/// Sets a control's font size from the scale. One line per call site, and the narrow/wide
	/// choice cannot be forgotten because there is nowhere to forget it.
	/// </summary>
	public static void SetFont(Control control, TextRole role)
	{
		if (control == null)
			return;
		control.AddThemeFontSizeOverride("font_size", FontSize(control, role));
	}

	/// <summary>
	/// Body copy that has to wrap. Anything longer than a few words needs both of these or it
	/// runs off the card: Godot does not wrap a Label by default, so a long description either
	/// clips or forces its container wider than the viewport.
	/// </summary>
	public static void SetBodyText(Label label, TextRole role = TextRole.Body)
	{
		if (label == null)
			return;
		SetFont(label, role);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 0f);
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
