using Godot;
using System.Collections.Generic;

/// <summary>
/// Makes every list in a menu scroll under a dragged finger, while a tap still presses what it
/// lands on.
/// </summary>
/// <remarks>
/// WHAT GODOT ALREADY DOES. A ScrollContainer on a touchscreen drag-scrolls itself, with a deadzone
/// and a flick that coasts to a stop, from the mouse events Godot emulates for the first finger. Once
/// the drag passes the deadzone it sends NOTIFICATION_SCROLL_BEGIN down its subtree, and BaseButton
/// answers that by dropping the press it was holding - so a drag that started on a card scrolls the
/// list and does not choose the card, and a tap that never left the deadzone presses it as normal.
/// None of that needed writing.
///
/// WHY IT NEVER RAN. The container only sees the drag if the events reach it, and a pointer event
/// travels up from the control it landed on only while every control on the way is MOUSE_FILTER_PASS.
/// Button, Panel and PanelContainer default to STOP, and nearly every square inch of every list in
/// this game is one of those three - so the press was stopped at the card, the ScrollContainer never
/// heard of it, and a finger could not move a single list in the game (the reported "I can't use
/// finger to scroll menus"). A mouse never noticed, because the wheel is a scroll event and Control
/// passes those up regardless (force_pass_scroll_events).
///
/// So this turns STOP into PASS inside each scroll, and makes the outermost ScrollContainer itself
/// STOP. That last half keeps what STOP inside the list used to guarantee: an event that lands in a
/// list still never leaks out of it to whatever the screen has behind. Controls that are dragged in
/// their own right - sliders, scrollbars, text fields - keep STOP, so dragging a volume slider moves
/// the slider and not the page.
///
/// The deadzone is the other half. The project default is ZERO, which with PASS children would turn
/// every tap with a pixel of wobble into a scroll and cancel the button under it; see
/// <see cref="DragDeadzone"/>.
///
/// <see cref="MenuNavigator"/> calls <see cref="PrepareAll"/> on every rescan, which every screen
/// already gets from <c>MenuNavigator.Attach</c>, so a screen does nothing extra for this. A list
/// rebuilt after the screen opens is picked up within one rescan.
/// </remarks>
public static class TouchScroll
{
	/// <summary>
	/// How far a finger travels, in canvas units, before a touch is a drag and not a tap.
	/// </summary>
	/// <remarks>
	/// Android's own touch slop is 8dp, about 21 physical pixels on a Pixel 9 - and the 720-wide
	/// portrait canvas puts 1.5 physical pixels in every canvas unit there, so 16 is a shade over the
	/// slop the OS itself uses to tell the same two gestures apart. Smaller and a firm tap on a card
	/// that rolls under the thumb scrolls instead of choosing; larger and a short flick does nothing.
	/// </remarks>
	public const int DragDeadzone = 16;

	/// <summary>
	/// Prepares every ScrollContainer under <paramref name="root"/> for touch. Idempotent, and
	/// cheap on a screen already prepared: it only writes a filter that is still STOP.
	/// </summary>
	public static void PrepareAll(Node root)
	{
		if (root != null)
			Walk(root, insideScroll: false);
	}

	/// <summary>Prepares one ScrollContainer and everything currently inside it.</summary>
	public static void Prepare(ScrollContainer scroll)
	{
		if (scroll != null)
			Walk(scroll, insideScroll: HasScrollAncestor(scroll));
	}

	private static void Walk(Node node, bool insideScroll)
	{
		if (node is ScrollContainer scroll)
		{
			// The outermost list stops what lands in it; a list nested in another must PASS, or the
			// outer one could never be dragged from inside the inner. The inner still takes a drag on
			// its own axis first, because it accepts the event once it has actually scrolled.
			if (scroll.MouseFilter != Control.MouseFilterEnum.Ignore)
				scroll.MouseFilter = insideScroll ? Control.MouseFilterEnum.Pass : Control.MouseFilterEnum.Stop;

			// Only the project default is replaced. A list that chose its own deadzone keeps it.
			if (scroll.ScrollDeadzone == 0)
				scroll.ScrollDeadzone = DragDeadzone;

			insideScroll = true;
		}
		else if (insideScroll && node is Control control
			&& control.MouseFilter == Control.MouseFilterEnum.Stop
			&& !OwnsItsDrag(control))
		{
			control.MouseFilter = Control.MouseFilterEnum.Pass;
		}

		// GetChildren leaves out internal children, which is what keeps a ScrollContainer's own
		// scrollbars STOP - they are Ranges anyway, but they are not ours to touch.
		foreach (Node child in node.GetChildren())
			Walk(child, insideScroll);
	}

	/// <summary>Controls a finger drags in their own right. A drag that starts on one is theirs.</summary>
	private static bool OwnsItsDrag(Control control)
	{
		return control is Range or LineEdit or TextEdit or ItemList or Tree;
	}

	private static bool HasScrollAncestor(Node node)
	{
		for (Node parent = node.GetParent(); parent != null; parent = parent.GetParent())
		{
			if (parent is ScrollContainer)
				return true;
		}
		return false;
	}

	/// <summary>
	/// Turns off FollowFocus on every list under <paramref name="root"/> for the length of one touch,
	/// recording which ones it turned off; <see cref="ReleaseFollowFocus"/> turns them back on.
	/// </summary>
	/// <remarks>
	/// A press on a button focuses it before the press reaches anything else, and a list that follows
	/// focus then jumps to show the whole of that button. With a mouse that is a click on a card half
	/// off the bottom edge, which is rare. With a finger it is nearly every swipe, because the natural
	/// place to start pushing a list up is the bottom of it - so every drag began by lurching the list
	/// somewhere the player had not asked for. The focus still moves, so a pad picked up afterwards
	/// starts from the card the finger last touched; only the jump is skipped. Navigation with the
	/// stick follows focus again the moment the finger lifts.
	/// </remarks>
	public static void HoldFollowFocus(Node root, List<ScrollContainer> suspended)
	{
		if (root != null)
			CollectAndSuspend(root, suspended);
	}

	public static void ReleaseFollowFocus(List<ScrollContainer> suspended)
	{
		foreach (ScrollContainer scroll in suspended)
		{
			if (GodotObject.IsInstanceValid(scroll))
				scroll.FollowFocus = true;
		}
		suspended.Clear();
	}

	private static void CollectAndSuspend(Node node, List<ScrollContainer> suspended)
	{
		if (node is ScrollContainer { FollowFocus: true } scroll)
		{
			scroll.FollowFocus = false;
			suspended.Add(scroll);
		}

		foreach (Node child in node.GetChildren())
			CollectAndSuspend(child, suspended);
	}
}
