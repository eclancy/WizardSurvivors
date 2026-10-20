using Godot;
using WizardSurvivors.scripts;

/// <summary>
/// An element tag the player can tap to read what the element does. Every element chip in the
/// menus is one of these - the level-up cards, the ascension browser, the chest pages, the
/// Spellbook and the pause screen's Spells tab - so they all open the same card
/// (<see cref="ElementTagPopover"/>).
/// </summary>
/// <remarks>
/// Reported from the Pixel 9: "make the tags clickable on mobile so I can see what they do". A chip
/// said "Lightning 1/2" and nothing on the screen said what Lightning was FOR, or what the 2 was a
/// threshold of. The level-up card had a half-answer - pressing a chip wrote one tier's effect under
/// the row - and nothing else did at all.
///
/// A BUTTON, NOT A PANEL WITH A CLICK HANDLER. Three things come with BaseButton that a hand-rolled
/// press handler would each have to rebuild, and the old one got two of them wrong:
/// <list type="bullet">
/// <item>It fires on RELEASE, and only if the press was not taken away first. A ScrollContainer that
/// sees a finger pass its deadzone sends NOTIFICATION_SCROLL_BEGIN down its subtree and BaseButton
/// drops its press on that - so a drag that starts on a chip scrolls the list, and only a tap opens
/// the card. The old chip opened on PRESS and accepted it, so a drag starting on a chip could never
/// reach the list.</item>
/// <item>It is a focus stop the MenuNavigator understands (it only counts BaseButtons as a valid
/// selection), so a pad can land on a chip and press A on it, and MenuFocusHighlight rings it.</item>
/// <item>It stops the mouse by default. On the PC level-up card the chip sits INSIDE the card's own
/// Button, and a press that travelled on up to it would pick the card: STOP keeps a tap on a chip
/// from ever choosing the spell under it. On a phone the card Button is a sibling of the content
/// rather than its parent, so nothing up the chain from the chip is the card anyway.</item>
/// </list>
/// A list that makes its children PASS so a drag can reach it (the touch-scroll work) leaves this
/// correct: the press still reaches the chip first, and the scroll still cancels it.
///
/// On a PC the same card also comes up on hover, as the chip's tooltip; a touchscreen gets no
/// tooltip, since an emulated mouse left resting on the chip after a tap would raise a second copy
/// over the first.
/// </remarks>
public partial class ElementChip : Button
{
	/// <summary>The element this chip names.</summary>
	public Element Tag { get; private set; }

	/// <summary>
	/// What the player's count of this element becomes if they take the thing the chip sits on, or
	/// -1 when the chip is not on a choice (a Spellbook row, a requirement line).
	/// </summary>
	public int CountAfter { get; private set; } = -1;

	/// <summary>
	/// Tags of this element the thing the chip sits on would add, for a choice that does not know
	/// the player's count when it is built (a chest relic). The card adds it to the live count.
	/// </summary>
	public int Gain { get; set; }

	/// <param name="fill">The chip's face, as drawn. Pick the ink against this.</param>
	/// <param name="focusable">
	/// False on screens whose buttons take no focus - the pause screen, where a focusable control
	/// would take the selection off EscapeRoot and B could no longer close it.
	/// </param>
	public static ElementChip Create(Element element, string text, Color fill, Color border, Color ink,
		bool focusable, int countAfter = -1, int padX = 10, int padY = 2)
	{
		var chip = new ElementChip
		{
			// Named, so a harness can press one (_UiShot --press-late=ElementChip_Lightning).
			Name = $"ElementChip_{element}",
			Tag = element,
			CountAfter = countAfter,
			Text = text,
			FocusMode = focusable ? FocusModeEnum.All : FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			ClipText = false,
		};
		ResponsiveLayout.SetFont(chip, ResponsiveLayout.TextRole.Label);
		foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color",
			"font_focus_color", "font_hover_pressed_color" })
			chip.AddThemeColorOverride(state, ink);

		StyleBoxFlat Face(Color edge, int edgeWidth)
		{
			var style = new StyleBoxFlat { BgColor = fill, BorderColor = edge };
			style.SetBorderWidthAll(edgeWidth);
			style.SetCornerRadiusAll(4);
			// The border eats into the content margin, so a hovered chip with a 2px rim keeps
			// exactly the size of a resting one and the row does not twitch.
			style.ContentMarginLeft = padX - (edgeWidth - 1);
			style.ContentMarginRight = padX - (edgeWidth - 1);
			style.ContentMarginTop = padY - (edgeWidth - 1);
			style.ContentMarginBottom = padY - (edgeWidth - 1);
			return style;
		}

		chip.AddThemeStyleboxOverride("normal", Face(border, 1));
		// A heavier rim of the same colour: says "this answers a touch" without changing the face
		// the ink was measured against.
		chip.AddThemeStyleboxOverride("hover", Face(border.Darkened(0.25f), 2));
		chip.AddThemeStyleboxOverride("pressed", Face(border.Darkened(0.35f), 2));
		chip.AddThemeStyleboxOverride("hover_pressed", Face(border.Darkened(0.35f), 2));
		// Focus is drawn by MenuFocusHighlight over the chip, as on every other button.
		chip.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		if (!DisplayServer.IsTouchscreenAvailable())
			chip.TooltipText = $"{element}: what it does";

		chip.Pressed += () =>
		{
			SfxPlayer.Global(SfxCatalog.UiClick);
			ElementTagPopover.Toggle(chip);
		};
		return chip;
	}

	// The hover card on a PC is the same card a tap opens, built by the same code.
	public override GodotObject _MakeCustomTooltip(string forText)
	{
		return ElementTagPopover.BuildCard(this, Tag, CountAfter, Gain);
	}
}
