using Godot;

/// <summary>
/// Shows the mouse cursor only when the mouse is what the player is using, and only where it is
/// needed - a menu.
/// </summary>
/// <remarks>
/// The user's words: "make the mouse disappear while playing if using controller. Remove the mouse
/// from the screen unless it's needed for a menu." So the cursor is hidden whenever a pad was the
/// last thing touched, and hidden during play even for a mouse-and-keyboard player - nothing in a
/// run is aimed with it (every spell auto-fires). It comes back the moment the mouse moves over a
/// menu: the title and main menu, character and stage select, and every screen that pauses a run
/// (level-up, chest, pause, game over).
///
/// Device detection mirrors MenuNavigator's (.ai/controller-and-focus.md): a stick has to leave its
/// deadzone and the mouse has to actually travel, so resting a thumb on the pad or a single pixel
/// of mouse jitter does not flip the device. This only ever READS input - it never consumes or
/// redirects an event, so it cannot cost a pad press the way the old "mouse motion releases focus"
/// rule did.
///
/// An autoload. Does nothing on a phone, which has no cursor.
/// </remarks>
public partial class CursorVisibility : Node
{
	// A stick deflection below this is a resting thumb, not a choice to use the pad.
	private const float StickWakeThreshold = 0.5f;
	// Mouse travel, in pixels per event, below this is jitter.
	private const float MouseWakeDistance = 3f;

	private bool usingPad;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		if (OS.HasFeature("mobile"))
			SetProcess(false);
	}

	public override void _Input(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventJoypadButton button when button.Pressed:
				usingPad = true;
				break;
			case InputEventJoypadMotion motion when Mathf.Abs(motion.AxisValue) >= StickWakeThreshold:
				usingPad = true;
				break;
			case InputEventMouseMotion mouse when mouse.Relative.Length() >= MouseWakeDistance:
				usingPad = false;
				break;
			case InputEventMouseButton click when click.Pressed:
				usingPad = false;
				break;
		}
	}

	public override void _Process(double delta)
	{
		bool show = !usingPad && !Node2DGame.InPlay;
		Input.MouseModeEnum wanted = show ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;
		if (Input.MouseMode != wanted)
			Input.MouseMode = wanted;
	}
}
