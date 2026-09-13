using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The one place menu chrome is styled. Replaces FantasyGuiSkin, which funnelled a third-party
/// GUI pack into every screen in the game.
/// </summary>
/// <remarks>
/// It keeps the old method names on purpose - StyleButton, ApplyButtonSet, ApplyButtonsInTree,
/// MakeCardClickOverlay, MakeSubtreeClickThrough - because those call sites are spread across nine
/// files and none of them needed to change to get new art. What changed underneath is everything:
/// the flat rounded rectangles with a blue border are now nine-sliced carved stone and iron from
/// tools/art/ui_frames.py, and no licensed texture is referenced at all.
///
/// TWO REGISTERS, and the rule that keeps them from becoming two games:
///
///   Stone is the default - buttons, panels, every screen's frame and chrome.
///   Vellum is reserved for the screens where the player reads ABOUT magic rather than presses a
///   button: the spellbook, mutation and ascension choices, any future codex.
///
///   MANUSCRIPT IS ALWAYS INSIDE STONE. Vellum only ever appears as the page within a stone frame,
///   never as a screen's outer edge. Opening the spellbook should read as opening a book.
///
/// NOTHING HERE IS CACHED IN A STATIC. A StyleBox and a Texture2D are both RefCounted, and a
/// static C# reference to one outlives the engine's own - the finalizer then trips
/// `gchandle.is_released()`, which reads as a Mono GC bug and is really a dangling reference. That
/// took the gameplay scene from 0 crashes in 4 runs to 5 in 5 once. Styles are built per call and
/// shared by the control that asked for them; ResourceLoader caches the textures underneath, so
/// the cost is a dictionary lookup.
/// </remarks>
public static class BonelightSkin
{
	/// <summary>Which visual register a control belongs to. See the class remarks.</summary>
	public enum Register
	{
		/// <summary>Carved stone and iron. Everything, unless it is a page of the book.</summary>
		Stone,
		/// <summary>Aged vellum and gold leaf. Spellbook, mutations, codex.</summary>
		Vellum,
	}

	private const string UiRoot = "res://assets/bonelight/ui/";

	// Authored at half size and rendered at x2 by tools/art/ui_frames.py, so these are the margins
	// at the size the files actually are. They must match BTN_MARGIN and PANEL_MARGIN there.
	private const int ButtonPatchMargin = 20;
	private const int PanelPatchMargin = 24;

	// Enough to clear the iron band and the gold inlay inside it, so no glyph ever sits on the
	// frame. The panel is looser because it holds a whole group rather than one line of text.
	private const int ButtonContentMargin = 14;
	private const int PanelContentMargin = 18;

	private static Texture2D Load(string file) => GD.Load<Texture2D>(UiRoot + file);

	/// <summary>A texture if it exists, otherwise null. Used for icons, which may be absent while
	/// the icon set is still being drawn.</summary>
	public static Texture2D LoadTextureSafe(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
			return null;
		return ResourceLoader.Load<Texture2D>(path);
	}

	private static StyleBoxTexture Frame(string file, int patchMargin, int contentMargin)
	{
		var style = new StyleBoxTexture { Texture = Load(file) };
		style.TextureMarginLeft = patchMargin;
		style.TextureMarginTop = patchMargin;
		style.TextureMarginRight = patchMargin;
		style.TextureMarginBottom = patchMargin;
		style.ContentMarginLeft = contentMargin;
		style.ContentMarginTop = contentMargin;
		style.ContentMarginRight = contentMargin;
		style.ContentMarginBottom = contentMargin;
		return style;
	}

	/// <summary>The stone slab a group of controls sits on. `inset` sinks it, for scrolling lists
	/// whose content should read as recessed rather than floating.</summary>
	public static StyleBoxTexture PanelStyle(Register register = Register.Stone, bool inset = false)
	{
		string file = register == Register.Vellum
			? "ui-page.png"
			: inset ? "ui-panel-inset.png" : "ui-panel.png";
		return Frame(file, PanelPatchMargin, PanelContentMargin);
	}

	/// <summary>
	/// Gives a Control the panel frame. Works on anything, but only a PanelContainer or Panel draws
	/// a "panel" stylebox, so for a bare Control this quietly does nothing - which is why it takes
	/// the node it is given and checks.
	/// </summary>
	public static void ApplyPanel(Control control, Register register = Register.Stone, bool inset = false)
	{
		if (control == null)
			return;

		if (control is PanelContainer or Panel)
		{
			control.AddThemeStyleboxOverride("panel", PanelStyle(register, inset));
			return;
		}

		// A plain Control cannot draw a stylebox, so give it a PanelContainer-shaped backdrop
		// behind its content instead. Same visual result, no scene restructuring at the call site.
		var backdrop = control.GetNodeOrNull<Panel>("BonelightPanelBackdrop");
		if (backdrop == null)
		{
			backdrop = new Panel { Name = "BonelightPanelBackdrop", MouseFilter = Control.MouseFilterEnum.Ignore };
			backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			control.AddChild(backdrop);
			control.MoveChild(backdrop, 0);
		}
		backdrop.AddThemeStyleboxOverride("panel", PanelStyle(register, inset));
	}

	/// <summary>A full-screen background image behind everything on a menu.</summary>
	public static void ApplyFullscreenBackdrop(Control root, string texturePath, float alpha = 1.0f)
	{
		if (root == null)
			return;

		Texture2D texture = LoadTextureSafe(texturePath);
		if (texture == null)
			return;

		var backdrop = root.GetNodeOrNull<TextureRect>("BonelightBackdrop");
		if (backdrop == null)
		{
			backdrop = new TextureRect
			{
				Name = "BonelightBackdrop",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Texture = texture,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				Modulate = new Color(1f, 1f, 1f, alpha)
			};
			backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			root.AddChild(backdrop);
			root.MoveChild(backdrop, 0);
		}
		else
		{
			backdrop.Texture = texture;
			backdrop.Modulate = new Color(1f, 1f, 1f, alpha);
		}
	}

	/// <summary>
	/// The iron-bound stone button style, in all four states. `iconPath` is optional and may point
	/// at nothing - the icon set is drawn in a later pass, and a missing icon leaves a plain button
	/// rather than an error.
	/// </summary>
	public static void StyleButton(Button button, string iconPath = null, int iconMaxWidth = 26)
	{
		if (button == null)
			return;

		if (!string.IsNullOrEmpty(iconPath))
		{
			Texture2D icon = LoadTextureSafe(iconPath);
			if (icon != null)
			{
				button.Icon = icon;
				button.IconAlignment = HorizontalAlignment.Left;
				button.ExpandIcon = false;
				button.AddThemeConstantOverride("icon_max_width", iconMaxWidth);
			}
		}

		ApplyButtonStyle(button);
	}

	/// <summary>How much weight a button carries on its screen.</summary>
	/// <remarks>
	/// A flat set of identical buttons makes the player read all of them to find the one they
	/// want. These three tiers are carried entirely by LIGHT, using frames that already exist:
	/// the primary button rests on the lit frame, so its gold is always catching; the quiet one
	/// rests on the unlit frame that has no gold at all, so it recedes without being greyed out.
	/// Nothing changes size between tiers, so a row of buttons cannot twitch.
	/// </remarks>
	public enum Emphasis
	{
		/// <summary>The one thing the player came to this screen to do. At most one per screen.</summary>
		Primary,
		/// <summary>The standard button.</summary>
		Normal,
		/// <summary>Deliberate destinations like Options or Reset - findable, never noticeable.</summary>
		Quiet,
	}

	/// <summary>The stone button style at a given weight. Text colour is set to match.</summary>
	public static void StyleButton(Button button, Emphasis emphasis)
	{
		if (button == null)
			return;

		string resting = emphasis switch
		{
			Emphasis.Primary => "ui-button-hover.png",
			Emphasis.Quiet => "ui-button-disabled.png",
			_ => "ui-button.png",
		};

		button.AddThemeStyleboxOverride("normal", Frame(resting, ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("hover", Frame("ui-button-hover.png", ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("pressed", Frame("ui-button-pressed.png", ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("focus", Frame("ui-button-hover.png", ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("disabled", Frame("ui-button-disabled.png", ButtonPatchMargin, ButtonContentMargin));

		Color text = emphasis switch
		{
			Emphasis.Primary => new Color(1.0f, 0.98f, 0.90f),
			Emphasis.Quiet => new Color(0.70f, 0.72f, 0.78f),
			_ => new Color(0.94f, 0.93f, 0.88f),
		};
		button.AddThemeColorOverride("font_color", text);
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.98f, 0.92f));
		button.AddThemeColorOverride("font_disabled_color", new Color(0.42f, 0.45f, 0.52f));
	}

	public static void ApplyButtonSet(IEnumerable<Button> buttons, int iconOffset = 0)
	{
		if (buttons == null)
			return;

		foreach (Button button in buttons.Where(b => b != null))
			ApplyButtonStyle(button);
	}

	public static void ApplyButtonsInTree(Control root, int iconOffset = 0)
	{
		if (root == null)
			return;

		var buttons = new List<Button>();
		CollectButtons(root, buttons);
		ApplyButtonSet(buttons, iconOffset);
	}

	private static void CollectButtons(Node node, List<Button> output)
	{
		// A card overlay is a hit area, not a button anyone should see. Giving it the framed panel
		// style would paint an opaque box over the card it is supposed to be invisible on top of -
		// which is exactly what happened when CharacterSelection called ApplyButtonsInTree after
		// building its cards.
		if (node is Button button && !button.IsInGroup(CardOverlayGroup))
			output.Add(button);

		foreach (Node child in node.GetChildren())
			CollectButtons(child, output);
	}

	/// <summary>Group marking a click overlay, so the button skin leaves it transparent.</summary>
	public const string CardOverlayGroup = "card_click_overlay";

	/// <summary>
	/// A transparent button sized to its parent, so an entire card is one click target rather than
	/// only the few pixels of padding that no child control happens to cover.
	/// </summary>
	/// <remarks>
	/// Add it as the <em>first</em> child of the card and call <see cref="MakeSubtreeClickThrough"/>
	/// on the content that follows: content drawn above the overlay then lets clicks fall through
	/// to it, while any genuinely interactive child left out of that call still takes its own input
	/// because it is drawn on top.
	/// </remarks>
	public static Button MakeCardClickOverlay(bool enabled, int cornerRadius = 6)
	{
		var overlay = new Button
		{
			Flat = true,
			Disabled = !enabled,
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = enabled ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow,
			FocusMode = enabled ? Control.FocusModeEnum.All : Control.FocusModeEnum.None
		};
		overlay.AddToGroup(CardOverlayGroup);

		StyleBoxFlat Tinted(Color color)
		{
			var box = new StyleBoxFlat { BgColor = color };
			box.SetCornerRadiusAll(cornerRadius);
			return box;
		}

		// Gold rather than the old blue: the hover wash should be the same light the frames are
		// bevelled in, or the card lights up in a colour nothing else on the screen uses.
		StyleBoxFlat transparent = Tinted(new Color(0f, 0f, 0f, 0f));
		overlay.AddThemeStyleboxOverride("normal", transparent);
		overlay.AddThemeStyleboxOverride("disabled", transparent);
		overlay.AddThemeStyleboxOverride("hover", Tinted(new Color(0.85f, 0.69f, 0.29f, 0.16f)));
		overlay.AddThemeStyleboxOverride("pressed", Tinted(new Color(0.85f, 0.69f, 0.29f, 0.26f)));
		overlay.AddThemeStyleboxOverride("focus", Tinted(new Color(0.85f, 0.69f, 0.29f, 0.12f)));
		return overlay;
	}

	/// <summary>
	/// Makes every Control under <paramref name="root"/> ignore the mouse, so clicks reach the card
	/// overlay beneath instead of being swallowed. Anything listed in
	/// <paramref name="keepInteractive"/> is skipped along with its whole subtree.
	/// </summary>
	/// <remarks>
	/// Labels already default to Ignore, but containers - MarginContainer, VBoxContainer,
	/// PanelContainer, CenterContainer - default to Stop and cover the entire card between them.
	/// That is why a card with a click handler on the outer panel only responded around its edges.
	/// </remarks>
	public static void MakeSubtreeClickThrough(Control root, params Control[] keepInteractive)
	{
		if (root == null)
			return;

		// The root counts too - it is a container itself, and one Stop anywhere over the card is
		// enough to swallow the click.
		if (IsKept(root, keepInteractive))
			return;

		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		foreach (Node child in root.GetChildren())
		{
			if (child is Control control)
				MakeSubtreeClickThrough(control, keepInteractive);
		}
	}

	private static bool IsKept(Control control, Control[] keepInteractive)
	{
		return keepInteractive != null
			&& keepInteractive.Any(k => k != null && (k == control || k.IsAncestorOf(control)));
	}

	private static void ApplyButtonStyle(Button button)
	{
		button.AddThemeStyleboxOverride("normal", Frame("ui-button.png", ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("hover", Frame("ui-button-hover.png", ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("pressed", Frame("ui-button-pressed.png", ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("focus", Frame("ui-button-hover.png", ButtonPatchMargin, ButtonContentMargin));
		button.AddThemeStyleboxOverride("disabled", Frame("ui-button-disabled.png", ButtonPatchMargin, ButtonContentMargin));

		// Warm white, so text on stone reads as lit by the same key the frames are bevelled in.
		button.AddThemeColorOverride("font_color", new Color(0.94f, 0.93f, 0.88f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.98f, 0.92f));
		button.AddThemeColorOverride("font_disabled_color", new Color(0.42f, 0.45f, 0.52f));
	}
}
