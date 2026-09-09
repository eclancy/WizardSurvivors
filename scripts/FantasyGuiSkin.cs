using Godot;
using System.Collections.Generic;
using System.Linq;

public static class FantasyGuiSkin
{
	// Ornate framed stat crests (IconsMenu): blue 1-8, green 9-16, red 17-24.
	// Per tier order: 1=legs, 2=running legs, 3=shield, 4=dagger, 5-8=compound crests.
	public static readonly string[] MenuIconPaths = Enumerable.Range(1, 24)
		.Select(i => $"res://assets/organized/ui/ui-png-iconsmenu-{i}.png")
		.ToArray();

	// Large illustrated ability art (Skills Icon 1-15): 2=heal, 12=fire, 13=ice,
	// 14=nature, 15=lightning, 3=bow, 10=eye, 11=vitality, etc.
	public static readonly string[] SkillIconPaths = Enumerable.Range(1, 15)
		.Select(i => $"res://assets/organized/ui/ui-png-skills-icon-{i}{(i >= 3 && i <= 11 ? ".jpg" : ".png")}")
		.ToArray();

	// --- Semantic UI icons, verified by visual inspection of the pack ---
	// Round framed nav buttons (icons/): blue 1-9, green 10-18, red 19-27.
	// Per tier order: house, save, flag, gear, scroll, door, trophy, moneybag, mail.
	private const string RoundDir = "res://assets/organized/ui/ui-png-icons-";
	// Flat semantic glyphs (elements2/).
	private const string GlyphDir = "res://assets/organized/ui/ui-png-elements2-";

	public const string IconHome = RoundDir + "1.png";       // house
	public const string IconSave = RoundDir + "2.png";       // floppy disk
	public const string IconSettings = RoundDir + "4.png";   // gear (blue)
	public const string IconScroll = RoundDir + "5.png";     // scroll (blue)
	public const string IconExit = RoundDir + "6.png";       // door (blue)
	public const string IconTrophy = RoundDir + "7.png";     // trophy (blue)
	public const string IconCoin = RoundDir + "8.png";       // money bag (blue)

	public const string GlyphTrophy = GlyphDir + "6.png";    // trophy
	public const string GlyphQuest = GlyphDir + "10.png";    // quest scroll
	public const string GlyphSpellbook = GlyphDir + "21.png"; // blue book
	public const string GlyphCharacter = GlyphDir + "24.png"; // character bust
	public const string GlyphGem = GlyphDir + "36.png";      // blue orb / arcane gem
	public const string GlyphGear = GlyphDir + "39.png";     // gear
	public const string GlyphClose = GlyphDir + "43.png";    // X (close/remove)
	public const string GlyphMinus = GlyphDir + "45.png";    // minus
	public const string GlyphPlus = GlyphDir + "46.png";     // plus
	public const string GlyphPlay = GlyphDir + "47.png";     // play / forward arrow
	public const string GlyphGlobe = GlyphDir + "48.png";    // globe / world map

	public static Texture2D LoadTextureSafe(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
			return null;
		return ResourceLoader.Load<Texture2D>(path);
	}

	public static void ApplyFullscreenBackdrop(Control root, string texturePath, float alpha = 1.0f)
	{
		if (root == null)
			return;

		Texture2D texture = LoadTextureSafe(texturePath);
		if (texture == null)
			return;

		TextureRect backdrop = root.GetNodeOrNull<TextureRect>("FantasyGuiBackdrop");
		if (backdrop == null)
		{
			backdrop = new TextureRect
			{
				Name = "FantasyGuiBackdrop",
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

	public static void ApplyPanelBackdrop(Control panel, string texturePath, float alpha = 0.22f)
	{
		if (panel == null)
			return;

		Texture2D texture = LoadTextureSafe(texturePath);
		if (texture == null)
			return;

		TextureRect backdrop = panel.GetNodeOrNull<TextureRect>("FantasyGuiPanelBackdrop");
		if (backdrop == null)
		{
			backdrop = new TextureRect
			{
				Name = "FantasyGuiPanelBackdrop",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Texture = texture,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				Modulate = new Color(1f, 1f, 1f, alpha)
			};
			backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			panel.AddChild(backdrop);
			panel.MoveChild(backdrop, 0);
		}
		else
		{
			backdrop.Texture = texture;
			backdrop.Modulate = new Color(1f, 1f, 1f, alpha);
		}
	}

	/// <summary>
	/// Applies the blue-bordered button style, and optionally assigns a specific
	/// semantic icon. Pass an icon path from the Icon*/Glyph* constants for an
	/// exact, meaningful icon; pass null to style the button without an icon.
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
				// Source icons are ~120px; cap their draw width so they fit the button.
				button.AddThemeConstantOverride("icon_max_width", iconMaxWidth);
			}
		}

		ApplyButtonStyle(button);
	}

	/// <summary>
	/// Applies only the shared button style to each button (no icons). Use
	/// <see cref="StyleButton"/> for buttons that need an exact semantic icon.
	/// </summary>
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

		List<Button> buttons = new List<Button>();
		CollectButtons(root, buttons);
		ApplyButtonSet(buttons, iconOffset);
	}

	private static void CollectButtons(Node node, List<Button> output)
	{
		// A card overlay is a hit area, not a button anyone should see. Giving it the framed
		// panel style would paint an opaque box over the card it is supposed to be invisible on
		// top of - which is exactly what happened when CharacterSelection called
		// ApplyButtonsInTree after building its cards.
		if (node is Button button && !button.IsInGroup(CardOverlayGroup))
			output.Add(button);

		foreach (Node child in node.GetChildren())
			CollectButtons(child, output);
	}

	/// <summary>Group marking a click overlay, so the button skin leaves it transparent.</summary>
	public const string CardOverlayGroup = "card_click_overlay";

	/// <summary>
	/// A transparent button sized to its parent, so an entire card is one click target rather
	/// than only the few pixels of padding that no child control happens to cover.
	/// </summary>
	/// <remarks>
	/// Add it as the <em>first</em> child of the card and call
	/// <see cref="MakeSubtreeClickThrough"/> on the content that follows: content drawn above the
	/// overlay then lets clicks fall through to it, while any genuinely interactive child left out
	/// of that call (a dropdown, say) still takes its own input because it is drawn on top.
	///
	/// Hover and pressed tints come free, which is the other half of the fix - a card that
	/// responds to the pointer tells the player it is clickable before they try it.
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

		StyleBoxFlat transparent = Tinted(new Color(0f, 0f, 0f, 0f));
		overlay.AddThemeStyleboxOverride("normal", transparent);
		overlay.AddThemeStyleboxOverride("disabled", transparent);
		overlay.AddThemeStyleboxOverride("hover", Tinted(new Color(0.45f, 0.72f, 0.95f, 0.16f)));
		overlay.AddThemeStyleboxOverride("pressed", Tinted(new Color(0.45f, 0.72f, 0.95f, 0.26f)));
		overlay.AddThemeStyleboxOverride("focus", Tinted(new Color(0.45f, 0.72f, 0.95f, 0.12f)));
		return overlay;
	}

	/// <summary>
	/// Makes every Control under <paramref name="root"/> ignore the mouse, so clicks reach the
	/// card overlay beneath instead of being swallowed. Anything listed in
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
		StyleBoxFlat normal = new StyleBoxFlat
		{
			BgColor = new Color(0.11f, 0.12f, 0.18f, 0.93f),
			BorderColor = new Color(0.42f, 0.72f, 0.95f, 0.95f)
		};
		normal.SetBorderWidthAll(1);
		normal.SetCornerRadiusAll(6);
		normal.SetContentMarginAll(8);

		StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = normal.BgColor.Lightened(0.10f);

		StyleBoxFlat pressed = (StyleBoxFlat)normal.Duplicate();
		pressed.BgColor = normal.BgColor.Darkened(0.08f);

		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeStyleboxOverride("focus", hover);
		button.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1.0f));
	}
}

