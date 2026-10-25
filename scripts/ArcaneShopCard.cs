using Godot;

/// <summary>
/// One entry in the main menu's Arcane Upgrades shop: a page that lifts when it is pointed at.
/// MainMenu.Shop.cs builds what is written on it; this owns only the page's two states.
/// </summary>
/// <remarks>
/// THE LIFT IS THE LEVEL-UP SCREEN'S: hovered or focused, the page rests on its lit frame (the gilt
/// rule, the brighter leaf) and the icon's socket brightens - the furniture changes, never the face
/// under the words (.ai/level-up-screen.md, section 2: "Selecting a card must never change how
/// readable it is"). MenuFocusHighlight's ring does the rest, as everywhere else.
///
/// ONE STOP PER CARD, the card's own overlay, whatever the card's state: A on it buys what the card
/// sells (MainMenu.Shop.cs, BuildShopCard). The buy plate is for the pointer only and never takes the
/// selection. Focus on the card or the pointer anywhere over it lifts the page.
///
/// The page is MOUSE_FILTER_PASS so it hears the pointer arrive without stopping the drag that a
/// finger starts on it: the ScrollContainer behind it still scrolls (TouchScroll). Since Godot 4.2 a
/// Control's mouse_entered and mouse_exited count its children, so moving onto the plate does not
/// drop the lift.
/// </remarks>
public partial class ArcaneShopCard : PanelContainer
{
	public StyleBox RestingFrame { get; set; }
	public StyleBox LitFrame { get; set; }

	/// <summary>The card's stop: the selection's only place on the card; ignores the mouse.</summary>
	public Button FocusStop { get; set; }

	/// <summary>The buy plate (the pointer's), or null for an entry that has none.</summary>
	public Button Plate { get; set; }

	public TextureRect Socket { get; set; }
	public Texture2D SocketResting { get; set; }
	public Texture2D SocketLit { get; set; }

	private bool hovered;
	private bool lit;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Pass;
		MouseEntered += () => SetHovered(true);
		MouseExited += () => SetHovered(false);
		foreach (Button stop in new[] { FocusStop, Plate })
		{
			if (stop == null)
				continue;
			stop.FocusEntered += Refresh;
			stop.FocusExited += Refresh;
		}
		Apply(false, force: true);
	}

	private void SetHovered(bool value)
	{
		hovered = value;
		Refresh();
	}

	public void Refresh() => Apply(IsLifted(), force: false);

	private bool IsLifted()
	{
		bool focused = (FocusStop != null && IsInstanceValid(FocusStop) && FocusStop.HasFocus())
			|| (Plate != null && IsInstanceValid(Plate) && Plate.HasFocus());
		return hovered || focused;
	}

	/// <summary>Swaps the frames (the page's state, not its content). The shop calls this again when
	/// an upgrade reaches its last level and its page turns from the rubric to the gilt.</summary>
	public void SetFrames(StyleBox resting, StyleBox litFrame)
	{
		RestingFrame = resting;
		LitFrame = litFrame;
		Apply(IsLifted(), force: true);
	}

	private void Apply(bool value, bool force)
	{
		if (value == lit && !force)
			return;
		lit = value;
		if ((lit ? LitFrame : RestingFrame) is StyleBox frame)
			AddThemeStyleboxOverride("panel", frame);
		if (Socket != null && (lit ? SocketLit : SocketResting) is Texture2D socket)
			Socket.Texture = socket;
	}
}
