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
	/// True while the mouse is the device in use. The ring then draws for HOVER only, so the
	/// pointer and the selection cannot both be ringed at once.
	/// </summary>
	/// <remarks>
	/// This exists so that MenuNavigator never has to release focus to keep the screen unambiguous.
	/// It used to, and that cost the player their next press of A: the press arrived with nothing
	/// selected and was spent establishing a selection instead of acting on one. Hiding a ring is
	/// free; giving up focus is not.
	///
	/// A static bool, deliberately - not a static holding a Node or a RefCounted, which is the
	/// thing this file and BonelightSkin both warn about.
	/// </remarks>
	public static bool PointerActive;

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

	/// <summary>The top of the pulse: the same gold nearly to white.</summary>
	private static readonly Color GoldHot = new Color(1.0f, 0.97f, 0.82f);

	/// <summary>
	/// The dark either side of the gold band - occlusion, warm. It is what makes the ring readable on
	/// a LIGHT ground: see <see cref="_Draw"/>.
	/// </summary>
	private static readonly Color Outline = new Color(0.07f, 0.05f, 0.04f, 0.95f);

	/// <summary>Matches the corner radius of the card overlays, so the ring follows their edge.</summary>
	[Export] public int CornerRadius { get; set; } = 6;

	/// <summary>One full breath of the pulse, in seconds.</summary>
	private const float PulsePeriod = 1.2f;

	/// <summary>
	/// Below this height or width a control gets the thin ring. An element chip is 28 units tall
	/// with 2 of padding, and the full ring's 8 units a side would sit on its word.
	/// </summary>
	private const float SmallControl = 60f;

	/// <summary>
	/// A wash inside the ring, and deliberately almost nothing: the ring is the signal.
	/// </summary>
	// The wash inside the ring breathes between 0 and 0.05 (_Draw). It once sat at 0.12, and a
	// photograph of the character select settled it: a card overlay already washes itself gold at
	// 0.16 when it is pointed at, so the two stacked to 0.28 and turned the selected card olive -
	// the selection stopped reading as light on the card and started reading as a different card.

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

		// A slider is a selection too (MenuNavigator.IsSelection) - the volume rows - and a stick
		// that lands on one has to be shown where it landed.
		if (root is BaseButton or Slider)
			Attach((Control)root);

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

	/// <summary>Re-evaluates every highlight under a subtree, after <see cref="PointerActive"/> flips.</summary>
	public static void RefreshAll(Node root)
	{
		if (root == null)
			return;

		if (root is MenuFocusHighlight highlight)
			highlight.Refresh();

		foreach (Node child in root.GetChildren())
			RefreshAll(child);
	}

	private void Refresh()
	{
		// Hover always draws. Focus draws only when the pointer is not the thing being used, which
		// is what keeps a pad user from seeing their selection ringed in one place and the mouse
		// cursor ringing another.
		bool wanted = (hovered || (focused && !PointerActive)) && !IsWatchedDisabled();
		if (wanted == Visible)
			return;

		Visible = wanted;
		// The pulse redraws every frame while the ring is up, and only then.
		SetProcess(wanted);
		QueueRedraw();
	}

	public override void _Ready()
	{
		SetProcess(Visible);
	}

	public override void _Process(double delta)
	{
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

	/// <remarks>
	/// THREE BANDS, NOT ONE. The ring was a single pale-gold line two pixels inside the edge, and on
	/// the level-up screen it disappeared: a spell card is parchment (flesh hi), and pale gold on
	/// parchment is about 1.1:1 - so the selected card looked like every other card, and the
	/// UNselected upgrade card, which wears a red double rule, looked the more chosen of the two
	/// (2026-10-27, "it's very hard to tell what you have highlighted when levelling up"). A bright
	/// band between two dark ones reads on any ground: on dark stone the gold is the contrast, on
	/// parchment the dark lines either side of it are. The outer dark line sits on the control's
	/// very edge, so on a torn page it squares the page off against the stone round it.
	///
	/// AND A GLOW OUTSIDE IT, in the band's colour, fading over six units: the gap between two cards
	/// (or two rows) is dark stone on every screen that has one, and gold on dark stone is the
	/// strongest contrast the palette has. It is drawn outside the control's rect, which nothing
	/// clips but a ScrollContainer's edge, and it stays inside the gap every card and row list here
	/// leaves (12 units or more).
	///
	/// AND IT BREATHES. The band swings between the lit gold and nearly white, and the wash under it
	/// with it, over <see cref="PulsePeriod"/>: a static ring among gilded frames is one more frame,
	/// while the one thing on the screen that moves is where the eye goes. Real time
	/// (Time.GetTicksMsec), so it keeps breathing while the tree is paused, which is when the
	/// level-up and pause screens are up.
	///
	/// Built per draw rather than held in a static. A StyleBox is RefCounted and a static C#
	/// reference to one outlives the engine's own, which trips gchandle.is_released() in a
	/// finalizer - see the BonelightSkin remarks. Only the one visible ring redraws per frame.
	/// </remarks>
	public override void _Draw()
	{
		float breath = 0.5f - 0.5f * Mathf.Cos(Time.GetTicksMsec() / 1000f * Mathf.Tau / PulsePeriod);
		Color band = Gold.Lerp(GoldHot, breath);

		// The full ring is 2 + 4 + 2 units a side; a chip or a small button gets 1 + 2 + 1.
		bool small = Size.X < SmallControl || Size.Y < SmallControl;
		int edge = small ? 1 : 2;
		int width = small ? 2 : 4;

		StyleBoxFlat Stroke(Color colour, int border, int radius, float fill = 0f)
		{
			var box = new StyleBoxFlat
			{
				BgColor = new Color(Gold, fill),
				BorderColor = colour,
				DrawCenter = fill > 0f,
			};
			box.SetBorderWidthAll(border);
			box.SetCornerRadiusAll(Mathf.Max(0, radius));
			return box;
		}

		Rect2 Inset(float by) =>
			new Rect2(by, by, Mathf.Max(0f, Size.X - by * 2f), Mathf.Max(0f, Size.Y - by * 2f));

		// The glow first, so the ring's own outline is drawn over its inner edge.
		int[] glow = small ? new[] { 2, 4 } : new[] { 2, 4, 6 };
		for (int i = 0; i < glow.Length; i++)
		{
			float alpha = Mathf.Lerp(0.55f, 0.85f, breath) * (1f - i / (float)glow.Length);
			DrawStyleBox(Stroke(new Color(band, alpha), 2, CornerRadius + glow[i]), Inset(-glow[i]));
		}
		DrawStyleBox(Stroke(Outline, edge, CornerRadius, Mathf.Lerp(0.0f, 0.05f, breath)), Inset(0f));
		DrawStyleBox(Stroke(band, width, CornerRadius - edge), Inset(edge));
		DrawStyleBox(Stroke(new Color(Outline, 0.75f), edge, CornerRadius - edge - width), Inset(edge + width));
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}
}
