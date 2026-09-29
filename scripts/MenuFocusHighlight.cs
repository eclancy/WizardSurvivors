using Godot;

/// <summary>
/// The one selection highlight in the game: a gold ring drawn over whichever control the player is
/// pointing at, whether they are pointing with a stick or with a mouse.
/// </summary>
/// <remarks>
/// WHY THIS IS A SEPARATE NODE AND NOT A STYLEBOX. Godot draws exactly one stylebox per button
/// state, so a highlight expressed as a `focus` stylebox has to REPLACE the frame rather than sit
/// on top of it - and the frames already carry meaning. The level-up screen is the case that
/// proves it: an upgrade to a spell the player already holds rests on the LIT frame, so
/// `AddThemeStyleboxOverride("focus", cardStyleLit)` gave it a focus state identical to its resting
/// state. A controller user could not see which of three cards was selected, because on two of
/// them nothing changed. A ring drawn over the top is additive, so it reads the same on a lit card,
/// an unlit card, a stone button and a transparent card overlay.
///
/// ONE CONCEPT, TWO INPUTS. Hover and focus both raise it and neither owns it, which is the whole
/// point: with a controller the ring follows focus, with a mouse it follows the pointer, and the
/// player never has to learn that those are two different things. A disabled control never shows
/// it - a locked character card that lights up when you point at it is a promise the screen cannot
/// keep.
///
/// WHERE IT IS DRAWN. Children are drawn after their parent, so a highlight parented to the
/// control it watches lands on top of that control - but a card is built as an invisible overlay
/// Button FIRST and its visible content after, so a ring inside the overlay would be buried under
/// the card art. <see cref="Attach"/> therefore separates the node that reports focus from the node
/// the ring is drawn inside, and defaults the second to the card when the first is a click overlay.
/// It also clips with its host, which is what keeps a highlight on a scrolled list from floating
/// outside the panel.
/// </remarks>
public partial class MenuFocusHighlight : Control
{
	/// <summary>Node name, so <see cref="Attach"/> can tell an already-highlighted control.</summary>
	public const string NodeName = "MenuFocusHighlight";

	/// <summary>
	/// The frames' inlay gold, lit. Staying in the gold family matters - a selection that arrives
	/// in a colour nothing else on the screen uses reads as an error state rather than a cursor -
	/// but it cannot be the SAME gold.
	/// </summary>
	/// <remarks>
	/// A photograph of the level-up screen settled the brightness. At the inlay's own 0.85/0.69/0.29
	/// the ring was lost on a card that already wears a gold frame, which is every upgrade card
	/// there, and the screen came back with three cards that all looked equally chosen. This is the
	/// same hue with the light turned up, so it separates from the frame it is drawn over rather
	/// than joining it.
	/// </remarks>
	private static readonly Color Gold = new Color(1.0f, 0.88f, 0.55f);

	/// <summary>Thick enough to survive being drawn over carved stone, thin enough not to crop the
	/// content it surrounds.</summary>
	[Export] public int BorderWidth { get; set; } = 3;

	/// <summary>Matches the corner radius of the card overlays, so the ring follows their edge.</summary>
	[Export] public int CornerRadius { get; set; } = 6;

	/// <summary>How far inside the control's edge the ring is drawn. See <see cref="_Draw"/>.</summary>
	[Export] public float Inset { get; set; } = 2f;

	/// <summary>
	/// A wash inside the ring, and deliberately almost nothing: the ring is the signal.
	/// </summary>
	/// <remarks>
	/// It started at 0.12 and a photograph of the character select settled it. A card overlay
	/// already washes itself gold at 0.16 when it is pointed at, so the two stacked to 0.28 and
	/// turned the selected card olive - the selection stopped reading as light on the card and
	/// started reading as a different card. This much only exists for the plain buttons, which
	/// have no wash of their own.
	/// </remarks>
	[Export] public float FillAlpha { get; set; } = 0.05f;

	private bool focused;
	private bool hovered;

	/// <summary>
	/// Gives <paramref name="watched"/> a selection highlight, drawn inside <paramref name="host"/>.
	/// Safe to call twice on the same control - the second call does nothing.
	/// </summary>
	/// <param name="watched">The control whose focus and hover drive the ring.</param>
	/// <param name="host">
	/// Where the ring is drawn. Defaults to the card when <paramref name="watched"/> is a
	/// <see cref="BonelightSkin.CardOverlayGroup"/> overlay, otherwise to the watched control
	/// itself.
	/// </param>
	public static void Attach(Control watched, Control host = null)
	{
		if (watched == null || !GodotObject.IsInstanceValid(watched))
			return;

		// A control nobody can point at has nothing to show. This is also what keeps the ring off
		// the labels and containers that ApplyButtonsInTree walks past.
		if (watched.FocusMode == FocusModeEnum.None && watched.MouseFilter == MouseFilterEnum.Ignore)
			return;

		// The invisible overlay reports the focus; the card underneath it is what the player sees,
		// so that is what gets ringed. Without this the ring lands behind the card's own art.
		host ??= watched.IsInGroup(BonelightSkin.CardOverlayGroup)
			? watched.GetParent() as Control ?? watched
			: watched;

		// A ring added to the wrong kind of Container becomes a row of the layout rather than an
		// overlay on it: a VBoxContainer would stack it under the content and shove everything up,
		// a CenterContainer would centre it at its minimum size, which is nothing. PanelContainer
		// and MarginContainer both fit EVERY child to their own rect, so a third child lands
		// exactly on top of the other two - which is the only reason a card can be ringed at all.
		if (host is Container && host is not PanelContainer and not MarginContainer)
			host = watched;

		if (host.GetNodeOrNull<MenuFocusHighlight>(NodeName) != null)
			return;

		var highlight = new MenuFocusHighlight
		{
			Name = NodeName,
			// Never eat a click. The whole card is one hit target and this sits over all of it.
			MouseFilter = MouseFilterEnum.Ignore,
			FocusMode = FocusModeEnum.None,
			Visible = false,
			// The level-up menu and the pause screen both run with the tree paused, and a cursor
			// that stops moving when the game stops is worse than no cursor.
			ProcessMode = ProcessModeEnum.Always,
		};
		highlight.SetAnchorsPreset(LayoutPreset.FullRect);
		host.AddChild(highlight);
		// Last child, so it is drawn after every sibling that makes up the card.
		host.MoveChild(highlight, host.GetChildCount() - 1);

		watched.FocusEntered += () => highlight.SetFocused(true);
		watched.FocusExited += () => highlight.SetFocused(false);
		watched.MouseEntered += () => highlight.SetHovered(true);
		watched.MouseExited += () => highlight.SetHovered(false);

		// A control can already hold focus by the time the highlight is attached - the pause screen
		// grabs focus as it opens, and the navigator attaches on the frame after.
		if (watched.HasFocus())
			highlight.SetFocused(true);
	}

	/// <summary>Walks a subtree and highlights every focusable control in it.</summary>
	public static void AttachAll(Node root)
	{
		if (root == null)
			return;

		if (root is BaseButton button)
			Attach(button);

		foreach (Node child in root.GetChildren())
			AttachAll(child);
	}

	private void SetFocused(bool value)
	{
		focused = value;
		Refresh();
	}

	private void SetHovered(bool value)
	{
		hovered = value;
		Refresh();
	}

	private void Refresh()
	{
		bool wanted = (focused || hovered) && !IsWatchedDisabled();
		if (wanted == Visible)
			return;

		Visible = wanted;
		QueueRedraw();
	}

	/// <remarks>
	/// Checked at show time rather than cached: a card can be disabled after the highlight is
	/// attached - the spellbook rebuilds its locked state, and the level-up menu disables a card
	/// the moment a choice is taken - and a ring left on a dead card is worse than none.
	/// </remarks>
	private bool IsWatchedDisabled()
	{
		// The watched control is either this node's host or the overlay inside it. Both cases are
		// answered by looking for a disabled BaseButton among this node's parent and its children.
		if (GetParent() is BaseButton parentButton)
			return parentButton.Disabled;

		foreach (Node sibling in GetParent()?.GetChildren() ?? new Godot.Collections.Array<Node>())
		{
			if (sibling is BaseButton overlay && overlay.IsInGroup(BonelightSkin.CardOverlayGroup))
				return overlay.Disabled;
		}

		return false;
	}

	public override void _Draw()
	{
		// Built per draw rather than held in a static. A StyleBox is RefCounted and a static C#
		// reference to one outlives the engine's own, which trips gchandle.is_released() in a
		// finalizer - see the BonelightSkin remarks. A menu redraws when the selection moves, not
		// every frame, so the allocation is not on any hot path.
		var ring = new StyleBoxFlat
		{
			BgColor = new Color(Gold, FillAlpha),
			BorderColor = Gold,
			DrawCenter = true,
		};
		ring.SetBorderWidthAll(BorderWidth);
		ring.SetCornerRadiusAll(CornerRadius);

		// Inset, so the ring sits just inside the frame rather than on top of its outer edge. Drawn
		// flush it merges with the carved border and reads as that border having brightened; drawn
		// two pixels in it reads as a light around the thing, which is what a cursor has to be.
		DrawStyleBox(ring, new Rect2(Inset, Inset, Mathf.Max(0f, Size.X - Inset * 2f), Mathf.Max(0f, Size.Y - Inset * 2f)));
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}
}
