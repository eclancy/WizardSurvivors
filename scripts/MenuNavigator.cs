using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Makes a menu playable with an Xbox pad: something is always selected, the left stick scrolls
/// through the options and keeps scrolling while it is held, and A presses whatever is selected.
/// </summary>
/// <remarks>
/// WHAT GODOT ALREADY DOES, AND THE TWO THINGS IT DOES NOT. The input map points the left stick and
/// the D-pad at `ui_up`/`ui_down`/`ui_left`/`ui_right`, and a focused Control answers those on its
/// own with proper geometric neighbour resolution - so a single flick of the stick already moves the
/// selection correctly, and A already presses a focused Button through `ui_accept`. What is missing
/// is smaller than it looks:
///
///   1. NOTHING IS FOCUSED when a screen opens. Every menu in this game builds its buttons in code
///      and none of them called GrabFocus, so a pad user arrived on a screen where the stick moved
///      nothing and A did nothing. That is the entire reason the game felt like it had no controller
///      support; the mappings were mostly already there.
///   2. A HELD STICK DOES NOT REPEAT. Godot only emits a joypad motion event when an axis CHANGES,
///      so holding the stick down produces one event and then silence. One flick per option is
///      tolerable for three level-up cards and unusable for a spellbook.
///
/// So this node grants focus and adds repeat, and deliberately leaves the actual step to
/// <c>Control.FindValidFocusNeighbor</c> - the same call Godot's own navigation makes. A repeat that
/// walked its own list would drift out of agreement with a single flick on any screen whose cards
/// are a grid rather than a column, and the two would then disagree in a way nobody would think to
/// test.
///
/// WHY THE MOUSE DROPS FOCUS. Focus and hover are both drawn by <see cref="MenuFocusHighlight"/>, so
/// a pad user who reaches for the mouse would otherwise see two rings - one where the selection is
/// and one under the pointer. Mouse motion therefore releases focus, leaving hover as the only
/// highlight, and the next pad or key press takes it back. The player is never shown two answers to
/// "what am I about to press".
///
/// IT MUST RUN WHILE PAUSED. The level-up menu, the chest menu and the pause screen all appear with
/// the tree paused. A navigator that stops with the tree is a navigator that is absent from exactly
/// the three screens the player uses most.
/// </remarks>
public partial class MenuNavigator : Node
{
	public const string NodeName = "MenuNavigator";

	/// <summary>How long the stick must be held before the selection starts repeating. Long enough
	/// that a deliberate single flick never double-steps.</summary>
	[Export] public float RepeatDelay { get; set; } = 0.40f;

	/// <summary>Seconds between repeats once it has started. Roughly a keyboard's repeat rate.</summary>
	[Export] public float RepeatInterval { get; set; } = 0.12f;

	/// <summary>How far the stick must be pushed to count as held. Matches the `ui_*` deadzone in
	/// project.godot, so the repeat starts on the same push that Godot navigated on.</summary>
	[Export] public float StickThreshold { get; set; } = 0.5f;

	/// <summary>How often the focusable set is re-scanned, in seconds. Menus rebuild their cards -
	/// the level-up screen rebuilds all of them on every level - and a new card needs a highlight.
	/// </summary>
	[Export] public float RescanInterval { get; set; } = 0.25f;

	private Node menuRoot;

	/// <summary>The screen's primary action, if it named one. See <see cref="Attach"/>.</summary>
	private Control preferredFocus;

	/// <summary>
	/// True while the player is driving with a pad or the keyboard, false once they touch the mouse.
	/// It decides whether this node insists on something being focused.
	/// </summary>
	private bool keepFocus;

	private Side heldSide;
	private bool holding;
	private float repeatCountdown;
	private float rescanCountdown;

	/// <summary>
	/// Gives <paramref name="root"/> pad navigation and highlights everything focusable under it.
	/// Idempotent - a screen that rebuilds itself can call this again on every rebuild.
	/// </summary>
	/// <param name="initialFocus">
	/// What to select first. Without it the selection lands on the first focusable control in tree
	/// order, which is reading order on most of these screens but not on all of them: the main
	/// menu's Options button is a direct child of the root and its Start Run button is buried in a
	/// MarginContainer, so tree order opens the game with Options selected. A screen with a primary
	/// action should name it.
	/// </param>
	public static MenuNavigator Attach(Node root, Control initialFocus = null)
	{
		if (root == null)
			return null;

		var existing = root.GetNodeOrNull<MenuNavigator>(NodeName);
		if (existing != null)
		{
			if (initialFocus != null)
				existing.preferredFocus = initialFocus;
			existing.Refresh();
			return existing;
		}

		var navigator = new MenuNavigator
		{
			Name = NodeName,
			ProcessMode = ProcessModeEnum.Always,
			preferredFocus = initialFocus,
		};
		root.AddChild(navigator);
		return navigator;
	}

	public override void _Ready()
	{
		menuRoot = GetParent();

		// A screen that calls Attach part-way through its own _Ready has not finished building its
		// buttons yet, so the first scan waits a frame rather than finding an empty tree.
		CallDeferred(nameof(Bootstrap));
	}

	private void Bootstrap()
	{
		// A pad plugged in means the player is probably holding it, so start with a selection. With
		// no pad, start with nothing selected: a keyboard-and-mouse player who has not pressed
		// anything yet should not be shown a cursor they did not ask for.
		keepFocus = Input.GetConnectedJoypads().Count > 0;
		Rescan();

		if (keepFocus)
			EnsureFocus(suppressScroll: true);
	}

	/// <summary>Re-highlights the tree. Safe and cheap to call repeatedly.</summary>
	public void Rescan()
	{
		MenuFocusHighlight.AttachAll(menuRoot);
	}

	/// <summary>
	/// Call after a screen rebuilds its options: highlights the new controls, and puts the
	/// selection back on the first of them for a pad user, whose old selection was just freed.
	/// </summary>
	/// <remarks>
	/// Nothing is forced on a mouse user. A rebuilt level-up screen that grabbed focus regardless
	/// would put a ring on the first card every time the player rerolled, next to the pointer they
	/// are actually aiming with.
	/// </remarks>
	public void Refresh()
	{
		Rescan();

		if (keepFocus)
			EnsureFocus();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMenuVisible())
			return;

		// The mouse is the only input that gives focus up. Zero-relative motion events arrive when
		// the window regains focus and must not count as the player reaching for the mouse.
		if (@event is InputEventMouseMotion motion && motion.Relative != Vector2.Zero)
		{
			SetKeepFocus(false);
			return;
		}

		bool padInput = @event is InputEventJoypadButton { Pressed: true }
			|| (@event is InputEventJoypadMotion joyMotion && Mathf.Abs(joyMotion.AxisValue) >= StickThreshold);
		bool keyInput = @event is InputEventKey { Pressed: true, Echo: false };

		if (!padInput && !keyInput)
			return;

		// A screen above this one owns the selection; note the input device and otherwise keep out.
		if (AnotherMenuHasSelection())
		{
			keepFocus = true;
			return;
		}

		bool hadFocus = HasValidFocus();
		SetKeepFocus(true);

		if (hadFocus)
			return;

		// Nothing was selected, so this press is what establishes the selection - and it must not
		// also act on it. Left through, A would press the first button the instant focus landed on
		// it, and a flick of the stick would step straight past it. _Input runs before Godot's GUI
		// handling, so consuming it here is enough.
		if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_up")
			|| @event.IsActionPressed("ui_down") || @event.IsActionPressed("ui_left")
			|| @event.IsActionPressed("ui_right"))
		{
			EnsureFocus();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (!IsMenuVisible())
		{
			holding = false;
			return;
		}

		// The slow clock is for controls that appear while focus stays valid - the main menu swapping
		// in a panel whose buttons were built long ago, for instance. Those need a ring, not focus,
		// so it runs whether or not this screen currently owns the selection.
		rescanCountdown -= (float)delta;
		if (rescanCountdown <= 0f)
		{
			rescanCountdown = RescanInterval;
			Rescan();
		}

		// A screen layered over this one owns the selection, and both the focus grab and the repeat
		// have to stand down - a repeat that kept running would step the OTHER screen's selection,
		// twice per tick, because its navigator is stepping it too.
		if (AnotherMenuHasSelection())
		{
			holding = false;
			return;
		}

		// Checked every frame, because it is nearly free: EnsureFocus reads the current focus owner
		// and returns. It only walks the tree when focus has actually been lost, which is what makes
		// a panel switch or a reroll recover on the next frame instead of on the next rescan.
		if (keepFocus)
			EnsureFocus();

		UpdateRepeat((float)delta);
	}

	/// <summary>
	/// The held-stick repeat. The first step is Godot's, made when the axis crossed the deadzone;
	/// this only fills in the steps after it, which is why the countdown starts at
	/// <see cref="RepeatDelay"/> rather than firing immediately.
	/// </summary>
	private void UpdateRepeat(float delta)
	{
		if (!TryGetHeldDirection(out Side side))
		{
			holding = false;
			return;
		}

		if (!holding || side != heldSide)
		{
			holding = true;
			heldSide = side;
			repeatCountdown = RepeatDelay;
			return;
		}

		repeatCountdown -= delta;
		if (repeatCountdown > 0f)
			return;

		repeatCountdown = RepeatInterval;
		Step(side);
	}

	/// <summary>
	/// Which way the pad is being held, reading the hardware rather than the `ui_*` actions.
	/// </summary>
	/// <remarks>
	/// Reading the actions would pick up the arrow keys and WASD as well, and the keyboard already
	/// has an OS repeat - the two would stack into a selection that moves twice per tick. The axes
	/// and the D-pad buttons are the only inputs Godot does not repeat, so they are the only ones
	/// repeated here. Vertical wins a diagonal: menus here are lists, and a stick pushed down and
	/// slightly right means down.
	/// </remarks>
	private bool TryGetHeldDirection(out Side side)
	{
		side = Side.Bottom;

		foreach (int device in Input.GetConnectedJoypads())
		{
			float x = Input.GetJoyAxis(device, JoyAxis.LeftX);
			float y = Input.GetJoyAxis(device, JoyAxis.LeftY);

			if (y <= -StickThreshold || Input.IsJoyButtonPressed(device, JoyButton.DpadUp))
			{
				side = Side.Top;
				return true;
			}
			if (y >= StickThreshold || Input.IsJoyButtonPressed(device, JoyButton.DpadDown))
			{
				side = Side.Bottom;
				return true;
			}
			if (x <= -StickThreshold || Input.IsJoyButtonPressed(device, JoyButton.DpadLeft))
			{
				side = Side.Left;
				return true;
			}
			if (x >= StickThreshold || Input.IsJoyButtonPressed(device, JoyButton.DpadRight))
			{
				side = Side.Right;
				return true;
			}
		}

		return false;
	}

	/// <summary>Moves the selection one step, wrapping at the ends of a column.</summary>
	private void Step(Side side)
	{
		Control owner = GetViewport()?.GuiGetFocusOwner();
		if (owner == null || !IsInsideMenu(owner))
		{
			EnsureFocus();
			return;
		}

		Control next = owner.FindValidFocusNeighbor(side);
		if (next != null && next != owner)
		{
			next.GrabFocus();
			return;
		}

		// Off the end of the list. Wrapping matters more with a stick than with a mouse: there is
		// no way to jump to the far end of a long spellbook, so running off the bottom has to come
		// back round to the top or the player has to scroll all the way back.
		if (side != Side.Top && side != Side.Bottom)
			return;

		List<BaseButton> options = CollectFocusable();
		if (options.Count == 0)
			return;

		BaseButton wrapped = side == Side.Bottom ? options[0] : options[^1];
		if (wrapped != owner)
			wrapped.GrabFocus();
	}

	private void SetKeepFocus(bool value)
	{
		if (keepFocus == value)
			return;

		keepFocus = value;

		if (value)
		{
			EnsureFocus();
			return;
		}

		// Hand focus back so hover is the only highlight on screen. Only our own screen's focus is
		// released - another menu layered above this one owns its own.
		Control owner = GetViewport()?.GuiGetFocusOwner();
		if (owner != null && IsInsideMenu(owner))
			GetViewport().GuiReleaseFocus();
	}

	/// <summary>Selects the first option if nothing usable is selected.</summary>
	/// <remarks>
	/// Losing focus means the control that had it was hidden or freed, which means the tree changed,
	/// which means whatever replaced it has no ring yet. Rescanning here rather than only on the slow
	/// clock is what stops the selection landing on a card that is not drawn as selected.
	/// </remarks>
	private void EnsureFocus(bool suppressScroll = false)
	{
		if (HasValidFocus())
			return;

		Rescan();

		List<BaseButton> options = CollectFocusable();
		Control target = preferredFocus is BaseButton { Disabled: false } preferred
			&& preferred.IsVisibleInTree() && IsInsideMenu(preferred)
				? preferred
				: options.FirstOrDefault();

		if (target == null)
			return;

		if (suppressScroll)
			GrabWithoutScrolling(target);
		else
			target.GrabFocus();
	}

	/// <summary>
	/// Focuses a control without letting any scrolling ancestor jump to it.
	/// </summary>
	/// <remarks>
	/// FollowFocus is what the player wants when they NAVIGATE to an option below the fold, and
	/// never what they want on arrival. The character select proved it: the Test Wizard card is
	/// taller than the rest, so granting it focus as the screen opened scrolled the list far enough
	/// to show the card's bottom edge, and the screen came up already scrolled past its own first
	/// heading. A list opens at the top.
	///
	/// ScrollContainer follows focus from the viewport's gui_focus_changed signal, which fires
	/// synchronously inside GrabFocus - so turning the flag off around the call is enough, with no
	/// deferring and nothing to race.
	/// </remarks>
	private void GrabWithoutScrolling(Control target)
	{
		var restore = new List<ScrollContainer>();
		CollectFollowFocusScrolls(menuRoot, restore);

		foreach (ScrollContainer scroll in restore)
			scroll.FollowFocus = false;

		target.GrabFocus();

		foreach (ScrollContainer scroll in restore)
			scroll.FollowFocus = true;
	}

	private static void CollectFollowFocusScrolls(Node node, List<ScrollContainer> output)
	{
		if (node == null)
			return;

		if (node is ScrollContainer { FollowFocus: true } scroll)
			output.Add(scroll);

		foreach (Node child in node.GetChildren())
			CollectFollowFocusScrolls(child, output);
	}

	/// <remarks>
	/// A plain Control holding focus does not count. The pause screen deliberately focuses its
	/// backing Control so it can hear Escape, and a pad user left on it would find that the stick
	/// moved nothing - a Control with no focus neighbours navigates nowhere and A presses nothing.
	/// </remarks>
	private bool HasValidFocus()
	{
		Control owner = GetViewport()?.GuiGetFocusOwner();
		return owner is BaseButton { Disabled: false } button
			&& button.IsVisibleInTree()
			&& IsInsideMenu(owner);
	}

	/// <summary>
	/// Whether some other menu currently owns the selection.
	/// </summary>
	/// <remarks>
	/// Screens stack: the set-detail screen opens over the level-up menu, which is itself over the
	/// run. Every one of them has a navigator, and every navigator insisting on a selection of its
	/// own would take it off the others once per frame, so the ring would strobe between two
	/// screens and A would press whichever one won the last frame.
	///
	/// The rule that resolves it without any ordering machinery: in the steady state nobody steals.
	/// A menu takes the selection only at the moment it OPENS, through the Attach call on its own
	/// show path, and after that the screens underneath can see that the selection lives somewhere
	/// else and leave it there. Closing the top screen frees its buttons, the selection becomes
	/// invalid, and the screen underneath picks it up on the next frame.
	/// </remarks>
	private bool AnotherMenuHasSelection()
	{
		Control owner = GetViewport()?.GuiGetFocusOwner();
		return owner is BaseButton { Disabled: false } && owner.IsVisibleInTree() && !IsInsideMenu(owner);
	}

	private bool IsInsideMenu(Node node)
	{
		return menuRoot != null && node != null && (node == menuRoot || menuRoot.IsAncestorOf(node));
	}

	/// <summary>
	/// Every option on the screen, in tree order - which is reading order for every menu in this
	/// game, because they are all built top to bottom in code.
	/// </summary>
	private List<BaseButton> CollectFocusable()
	{
		var found = new List<BaseButton>();
		Collect(menuRoot, found);
		return found;
	}

	private static void Collect(Node node, List<BaseButton> output)
	{
		if (node == null)
			return;

		if (node is BaseButton button
			&& !button.Disabled
			&& button.FocusMode != Control.FocusModeEnum.None
			&& button.IsVisibleInTree())
		{
			output.Add(button);
		}

		foreach (Node child in node.GetChildren())
			Collect(child, output);
	}

	/// <summary>
	/// Whether this navigator's screen is actually on screen. The pause navigator lives on a hidden
	/// CanvasLayer for the whole run and must not answer the stick, or grab focus, until it opens.
	/// </summary>
	private bool IsMenuVisible()
	{
		return menuRoot switch
		{
			null => false,
			Control control => control.IsVisibleInTree(),
			CanvasLayer layer => layer.Visible,
			CanvasItem item => item.IsVisibleInTree(),
			_ => true,
		};
	}
}
