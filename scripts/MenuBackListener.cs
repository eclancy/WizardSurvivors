using Godot;
using System;

/// <summary>
/// Hears "back" for a screen whatever is selected on it: B and Escape (and any other action the
/// screen names) call <see cref="Back"/>, and pad Y calls <see cref="PadY"/> if the screen has a
/// second thing for it to do.
/// </summary>
/// <remarks>
/// The pause screen used to hear back by giving focus to its backing Control and reading that
/// Control's GuiInput. That only works while the backing Control HOLDS the focus, so every button on
/// the screen had to refuse focus to keep it there - and a pad could then select nothing at all: the
/// stick moved nothing, A pressed nothing, and the only things a controller could do were Y and
/// close it (2026-10-27, "the pause menu is not able to be used by controller").
///
/// So back is heard here instead, in _UnhandledInput: after the GUI has had the press, so a control
/// that wants it (a text field leaving edit mode, a slider) still gets it first, and only while the
/// selection is on this screen or nowhere. A screen opened over this one (Report a bug) holds the
/// selection, hears its own back, and marks it handled before it gets here.
///
/// IT MUST RUN WHILE PAUSED, like <see cref="MenuNavigator"/>: the screens that need it all appear
/// with the tree paused. And it marks what it acts on as handled - the pause screen closes on the
/// same Start that opens it, and a Start left unhandled would reach Node2DGame, unpaused by then,
/// and open it again on the same press.
/// </remarks>
public partial class MenuBackListener : Node
{
	public const string NodeName = "MenuBackListener";

	/// <summary>What closes the screen. B and Escape (`ui_cancel`) unless the screen says otherwise.</summary>
	public string[] Actions { get; set; } = { "ui_cancel" };

	public Action Back { get; set; }

	/// <summary>The pad's Y, for a screen with a second action that has no button of its own nearby.</summary>
	public Action PadY { get; set; }

	/// <summary>Gives <paramref name="screen"/> a back gesture. Idempotent: a second call replaces the first.</summary>
	public static MenuBackListener Attach(Node screen, Action back, params string[] actions)
	{
		if (screen == null)
			return null;

		var listener = screen.GetNodeOrNull<MenuBackListener>(NodeName);
		if (listener == null)
		{
			listener = new MenuBackListener { Name = NodeName, ProcessMode = ProcessModeEnum.Always };
			screen.AddChild(listener);
		}
		listener.Back = back;
		if (actions != null && actions.Length > 0)
			listener.Actions = actions;
		return listener;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		Node screen = GetParent();
		if (!IsScreenOpen(screen))
			return;

		// Another screen has the selection: its back is its own.
		Control owner = GetViewport()?.GuiGetFocusOwner();
		if (owner != null && !screen.IsAncestorOf(owner))
			return;

		foreach (string action in Actions)
		{
			if (!@event.IsActionPressed(action))
				continue;
			GetViewport().SetInputAsHandled();
			Back?.Invoke();
			return;
		}

		if (PadY != null && @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y })
		{
			GetViewport().SetInputAsHandled();
			PadY();
		}
	}

	// The same reading MenuNavigator.IsMenuVisible makes, for the same reason: a Control under a
	// hidden CanvasLayer still reports itself visible, so a layer is asked about itself.
	private static bool IsScreenOpen(Node screen) => screen switch
	{
		null => false,
		CanvasLayer layer => layer.Visible,
		CanvasItem item => item.IsVisibleInTree(),
		_ => true,
	};
}
