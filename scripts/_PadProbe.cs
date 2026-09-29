using Godot;
using System.Collections.Generic;
using System.Linq;

// Does pressing A actually press the thing that is selected?
//
// Moving the selection and activating it are handled by two DIFFERENT pieces of Godot, and only
// one of them cares whether the focused control is allowed to process input. Viewport's focus
// navigation needs nothing but a focus owner, so the selection moves; delivering ui_accept to that
// control goes through `gui.key_focus->can_process()`, which is false for anything Pausable while
// the tree is paused. A menu can therefore navigate perfectly and refuse to activate anything,
// which is exactly the shape of bug this probe exists to tell apart from a bad input mapping.
//
// Run:
//   "$GODOT_BIN" --path . scenes/_PadProbe.tscn -- --scene=res://scenes/MainMenu.tscn
//   "$GODOT_BIN" --path . scenes/_PadProbe.tscn -- --scene=res://scenes/node_2d_game.tscn --levelup
//
// It injects a real InputEventJoypadButton for A rather than calling EmitSignal, so every layer
// between the pad and the button - the input map, MenuNavigator._Input, the viewport's GUI
// routing, BaseButton's own handling - is on the path being tested. Emitting the signal would
// prove only that the handler is wired, which was never in doubt.
public partial class _PadProbe : Node
{
	[Export] public string ScenePath { get; set; } = "res://scenes/MainMenu.tscn";
	[Export] public int SettleFrames { get; set; } = 40;

	/// <summary>Wait for a level-up to come up before testing, instead of testing the loaded scene.</summary>
	[Export] public bool WaitForLevelUp { get; set; }

	/// <summary>Which joypad slot to pretend the press came from. The bug this probe found lives
	/// here: a binding pinned to device 0 is dead on every other slot.</summary>
	[Export] public int Device { get; set; }

	private Node instance;
	private int frames;
	private bool fired;
	private bool released;
	private bool reported;
	private Button watched;

	public override void _Ready()
	{
		// The screens most worth testing - level-up, chest, pause - all PAUSE the tree, and a probe
		// that stops processing when one appears can never reach it.
		ProcessMode = ProcessModeEnum.Always;

		// BOTH lists. Args after a bare "--" land in GetCmdlineUserArgs and args before it in
		// GetCmdlineArgs, and reading only one of them fails SILENTLY: every flag is ignored and
		// the probe runs its defaults while looking like it honoured the command line. That cost
		// an afternoon here - four runs that all reported on the main menu while claiming to be
		// testing character select.
		foreach (string arg in OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()))
		{
			if (arg.StartsWith("--scene="))
				ScenePath = arg.Substring("--scene=".Length);
			else if (arg.StartsWith("--settle="))
				SettleFrames = int.Parse(arg.Substring("--settle=".Length));
			else if (arg.StartsWith("--device="))
				Device = int.Parse(arg.Substring("--device=".Length));
			else if (arg == "--levelup")
				WaitForLevelUp = true;
			else if (arg.StartsWith("--stage="))
				Global.SelectedStageIdx = int.Parse(arg.Substring("--stage=".Length));
		}

		var packed = ResourceLoader.Load<PackedScene>(ScenePath);
		if (packed == null)
		{
			GD.PrintErr($"_PadProbe: {ScenePath} did not load");
			GetTree().Quit(1);
			return;
		}

		instance = packed.Instantiate();
		AddChild(instance);

		GD.Print($"_PadProbe: scene = {ScenePath}");
		GD.Print($"_PadProbe: joypads connected = {Input.GetConnectedJoypads().Count}, injecting as device {Device}");
		GD.Print($"_PadProbe: ui_accept has {InputMap.ActionGetEvents("ui_accept").Count} event(s), "
			+ $"joypad A present = {InputMap.ActionGetEvents("ui_accept").Any(e => e is InputEventJoypadButton { ButtonIndex: JoyButton.A })}");
	}

	public override void _Process(double delta)
	{
		if (reported)
			return;

		frames++;

		if (WaitForLevelUp && !LevelUpIsUp())
		{
			// Let the run play itself far enough to level. Nothing steers; standing still is enough
			// to reach level 2 on any stage, and what is being tested is the menu, not the fight.
			if (frames > 3000)
			{
				GD.Print("_PadProbe: no level-up appeared within 3000 frames");
				Finish();
			}
			return;
		}

		if (frames < SettleFrames)
			return;

		if (!fired)
		{
			fired = true;
			Report();
			WatchEverything();
			Inject(true);
			return;
		}

		if (!released)
		{
			released = true;
			// A BaseButton emits on RELEASE by default, so the up event is the one that matters and
			// a probe that only sent the down event would report a false negative.
			Inject(false);
			releasedAt = frames;
			return;
		}

		if (frames < releasedAt + 6)
			return;

		if (attempts < 2)
		{
			attempts++;
			GD.Print($"_PadProbe: attempt {attempts} did not fire; focus is now "
				+ $"{GetViewport()?.GuiGetFocusOwner()?.GetPath().ToString() ?? "(none)"} - pressing again");
			fired = false;
			released = false;
			return;
		}

		Finish();
	}

	private bool LevelUpIsUp()
	{
		return FindAll<CanvasLayer>(GetTree().Root).Any(l => l is LevelUpMenu && l.Visible);
	}

	private void Report()
	{
		Control owner = GetViewport()?.GuiGetFocusOwner();
		GD.Print($"_PadProbe: tree paused = {GetTree().Paused}");

		// The two things that decide whether a pad can do anything at all on a screen: is there a
		// navigator, and does it have anything to select. "Nothing is focused" has two completely
		// different causes and this is what tells them apart.
		var navigators = FindAll<MenuNavigator>(GetTree().Root).ToList();
		var candidates = FindAll<BaseButton>(GetTree().Root)
			.Where(b => !b.Disabled && b.FocusMode != Control.FocusModeEnum.None && b.IsVisibleInTree())
			.ToList();
		GD.Print($"_PadProbe: MenuNavigator(s) present = {navigators.Count}, "
			+ $"focusable buttons = {candidates.Count}");
		foreach (BaseButton b in candidates.Take(4))
			GD.Print($"_PadProbe:    candidate {b.GetPath()}");

		if (owner == null)
		{
			GD.Print("_PadProbe: NOTHING IS FOCUSED - the pad has nothing to press.");
			return;
		}

		GD.Print($"_PadProbe: focus owner = {owner.GetPath()} ({owner.GetType().Name})");
		// THE DIAGNOSTIC. A false here with a paused tree is the whole bug: the selection will move
		// and ui_accept will never be delivered.
		GD.Print($"_PadProbe: focus owner CanProcess = {owner.CanProcess()}  "
			+ $"ProcessMode = {owner.ProcessMode}");

		if (owner is Button button && watched != button)
		{
			watched = button;
			button.Pressed += OnWatchedPressed;
		}
	}

	/// <summary>
	/// Listen to every button on the screen, not just the focused one. If A activates something
	/// OTHER than what was selected, that is a different bug and a probe watching one button would
	/// report it as silence.
	/// </summary>
	private void WatchEverything()
	{
		foreach (BaseButton b in FindAll<BaseButton>(GetTree().Root))
		{
			if (watchedAll.Contains(b))
				continue;
			watchedAll.Add(b);
			BaseButton captured = b;
			b.Pressed += () => GD.Print($"_PadProbe: PRESSED {captured.GetPath()}");
		}
	}

	private readonly HashSet<BaseButton> watchedAll = new();

	private bool wasPressed;
	private int releasedAt;
	private int attempts;

	private void OnWatchedPressed()
	{
		wasPressed = true;
		GD.Print($"_PadProbe: fired on attempt {attempts + 1}");
		// Printed HERE, not at the end. Half the buttons worth testing change scene when they fire,
		// which frees this probe along with the scene it was watching - so the first version of
		// this reported a working button as a hang.
		GD.Print("_PadProbe: RESULT ok - A activated the focused button.");

		// And quit immediately, for the same reason. Start Run and a character card both change
		// scene, so waiting for the normal finish meant every passing screen sat there until the
		// shell timeout killed it - a pass that looked exactly like a hang, and cost a hundred
		// seconds each time.
		GetTree().Quit(0);
	}

	private void Inject(bool pressed)
	{
		Input.ParseInputEvent(new InputEventJoypadButton
		{
			Device = Device,
			ButtonIndex = JoyButton.A,
			Pressed = pressed,
		});
	}

	private void Finish()
	{
		reported = true;
		if (watched != null && GodotObject.IsInstanceValid(watched))
			watched.Pressed -= OnWatchedPressed;

		if (!wasPressed)
			GD.Print("_PadProbe: RESULT FAILED - A did not activate the focused button.");
		GetTree().Quit(wasPressed ? 0 : 2);
	}

	private static IEnumerable<T> FindAll<T>(Node node) where T : Node
	{
		if (node is T match)
			yield return match;

		foreach (Node child in node.GetChildren())
		{
			foreach (T found in FindAll<T>(child))
				yield return found;
		}
	}
}
