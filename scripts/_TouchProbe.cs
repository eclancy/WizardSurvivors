using Godot;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// Does a finger do what a finger should: does a tap press what it lands on, and does a drag move
// the list without pressing anything?
//
// The two touch bugs this was written for were both invisible to a mouse. A STOP card inside a
// ScrollContainer swallowed every drag before the list heard of it, and the touch pad's full-screen
// STOP control sat over the game over screen and swallowed every tap on Continue. A mouse click
// reaches both, so neither showed up on a desktop - only in a hand.
//
// Run (windowed, not headless - a headless display server drops injected input on the floor):
//   "$GODOT_BIN" --path . scenes/_TouchProbe.tscn -- --open-menu --scene=res://scenes/TitleScreen.tscn --press=SpellIndexButton
//   "$GODOT_BIN" --path . --touch-controls scenes/_TouchProbe.tscn -- --scene=res://scenes/node_2d_game.tscn --force-death --settle=240
//
// On the phone, launched by intent (the scene and flags go in the command_line_params extra; the
// scene must be "--scene,<path>", a bare path is ignored there), with --manual so that adb's real
// touches do the pressing:
//   adb shell am start -n com.ecec.wizardsurvivors/com.godot.game.GodotApp --esa command_line_params //     --scene,res://scenes/_TouchProbe.tscn,--,--scene=res://scenes/node_2d_game.tscn,--force-death,--manual,--settle=240
//
// It injects InputEventScreenTouch and InputEventScreenDrag through Input.ParseInputEvent, which is
// the same door the Android touch driver comes in by, so Godot's mouse emulation, MenuNavigator,
// TouchControls and the viewport's GUI routing are all on the path being tested.
//
// ONE THING IT CANNOT SHOW ON A DESKTOP. ScrollContainer only turns a drag into scrolling when
// DisplayServer.IsTouchscreenAvailable() is true, which it is not on a machine without a touch
// panel. So on a desktop the probe reports whether the drag REACHED the list (the bug) and that it
// pressed nothing; whether the list then moved is the phone's half of the test, by adb swipe.
public partial class _TouchProbe : Node
{
	[Export] public string ScenePath { get; set; } = "res://scenes/TitleScreen.tscn";
	[Export] public int SettleFrames { get; set; } = 40;

	// The panel to open before testing, by the NAME of the button that opens it - most lists in
	// this game are MainMenu panels that start hidden.
	[Export] public string PressButton { get; set; } = "";

	// What to tap when the screen has no list worth dragging: a button name or its text.
	[Export] public string TapTarget { get; set; } = "ContinueButton";

	[Export] public bool ForceDeath { get; set; }

	// --manual: stage the screen (open the panel, force the death) and then inject nothing, so a
	// real finger - or adb input on the phone - does the pressing. The injected half of this probe
	// goes in through Input; only a real touch goes in through the Android driver.
	[Export] public bool Manual { get; set; }

	// --force-levelup / --open-chest: put the level-up or chest screen up at SettleFrames / 2, the
	// way _UiShot does, so their lists can be dragged without playing to a level first.
	[Export] public bool ForceLevelUp { get; set; }
	[Export] public bool OpenChest { get; set; }
	private bool levelForced;
	private bool chestOpened;

	private enum Phase { Settling, Opening, Drag, DragRelease, DragCoast, Tap, TapRelease, TapWait, Done }

	private Phase phase = Phase.Settling;
	private int frames;
	private int phaseFrame;
	private bool deathForced;
	private ScrollContainer scroll;
	private int scrollEvents;
	private double scrollBefore;
	private Vector2 dragPoint;
	private int dragSteps;
	private Button tapButton;
	private bool tapPressed;
	private readonly List<string> pressedLog = new();
	private readonly HashSet<BaseButton> watched = new();

	private const int DragStepCount = 12;
	private const float DragStepUnits = 25f;

	public override void _Ready()
	{
		// The game over screen pauses the tree, and a probe that pauses with it never reports.
		ProcessMode = ProcessModeEnum.Always;

		// Always, not only with --force-death: the tap lands on a real button, and on the options
		// panel that is a real setting being flipped in the player's real save.
		SaveManager.WritesDisabled = true;

		foreach (string arg in OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()))
		{
			if (arg.StartsWith("--scene="))
				ScenePath = arg.Substring("--scene=".Length);
			else if (arg.StartsWith("--settle="))
				SettleFrames = int.Parse(arg.Substring("--settle=".Length));
			else if (arg.StartsWith("--press="))
				PressButton = arg.Substring("--press=".Length);
			else if (arg.StartsWith("--tap="))
				TapTarget = arg.Substring("--tap=".Length);
			else if (arg == "--open-menu")
				Global.OpenMenuImmediately = true;
			else if (arg == "--force-death")
				ForceDeath = true;
			else if (arg == "--manual")
				Manual = true;
			else if (arg == "--force-levelup")
				ForceLevelUp = true;
			else if (arg == "--open-chest")
				OpenChest = true;
		}

		var packed = ResourceLoader.Load<PackedScene>(ScenePath);
		if (packed == null)
		{
			GD.PrintErr($"_TouchProbe: {ScenePath} did not load");
			GetTree().Quit(1);
			return;
		}

		AddChild(packed.Instantiate());
		GD.Print($"_TouchProbe: scene = {ScenePath}, touchscreen available = {DisplayServer.IsTouchscreenAvailable()}, "
			+ $"emulate mouse from touch = {Input.EmulateMouseFromTouch}, touch controls = {TouchControls.ShouldEnable()}");
	}

	public override void _Process(double delta)
	{
		if (phase == Phase.Done || !IsInsideTree())
			return;

		frames++;
		phaseFrame++;

		if (frames > 3000 && !Manual)
		{
			GD.PrintErr("_TouchProbe: timed out");
			GetTree().Quit(1);
			return;
		}

		if (ForceDeath && !deathForced && frames >= SettleFrames / 2
			&& FindAll<Player>(GetTree().Root).FirstOrDefault() is Player victim)
		{
			deathForced = true;
			for (int i = 0; i < 6 && !victim.IsDead; i++)
				victim.TakeDamage(System.Math.Max(1, victim.MaxHP * 10));
		}

		if (ForceLevelUp && !levelForced && frames >= SettleFrames / 2
			&& FindAll<Player>(GetTree().Root).FirstOrDefault() is Player leveller)
		{
			levelForced = true;
			leveller.AddXp(System.Math.Max(1, leveller.XPToNextLevel - leveller.CurrentXP));
		}

		if (OpenChest && !chestOpened && frames >= SettleFrames / 2
			&& FindAll<Node2DGame>(GetTree().Root).FirstOrDefault() is Node2DGame game)
		{
			chestOpened = true;
			game.OpenChestSelectionMenu();
		}

		switch (phase)
		{
			case Phase.Settling:
				if (frames < SettleFrames)
					return;
				if (!string.IsNullOrEmpty(PressButton))
				{
					Button opener = FindAll<Button>(GetTree().Root).FirstOrDefault(b => b.Name == PressButton);
					if (opener == null)
					{
						GD.PrintErr($"_TouchProbe: no button named '{PressButton}'");
						GetTree().Quit(1);
						return;
					}
					opener.EmitSignal(BaseButton.SignalName.Pressed);
				}
				Enter(Phase.Opening);
				return;

			case Phase.Opening:
				// Long enough for MenuNavigator's rescan to have prepared a panel built just now.
				if (phaseFrame < 30 || Manual)
					return;
				WatchButtons();
				scroll = FindScrollableList();
				if (scroll != null)
					BeginDrag();
				else
					BeginTap(FindTapTarget());
				return;

			case Phase.Drag:
				Vector2 step = new Vector2(0f, -DragStepUnits);
				dragPoint += step;
				InjectDrag(dragPoint, step);
				if (dragSteps == 0)
					GD.Print($"_TouchProbe: drag starts on {Describe(GetViewport().GuiGetHoveredControl())}");
				if (++dragSteps >= DragStepCount)
					Enter(Phase.DragRelease);
				return;

			case Phase.DragRelease:
				InjectTouch(dragPoint, false);
				Enter(Phase.DragCoast);
				return;

			case Phase.DragCoast:
				if (phaseFrame < 40)
					return;
				ReportDrag();
				BeginTap(FindButtonInside(scroll));
				return;

			case Phase.Tap:
				// A thumb is not a mouse: it rolls a few units while it is down. Inside the deadzone
				// this must still be a tap.
				InjectDrag(TapPoint() + new Vector2(0f, 3f), new Vector2(0f, 3f));
				// Read after a motion, not after the press: hover only updates when the pointer moves.
				GD.Print($"_TouchProbe: under the finger: {Describe(GetViewport().GuiGetHoveredControl())}");
				Enter(Phase.TapRelease);
				return;

			case Phase.TapRelease:
				InjectTouch(TapPoint() + new Vector2(0f, 3f), false);
				if (phase != Phase.Done)
					Enter(Phase.TapWait);
				return;

			case Phase.TapWait:
				if (phaseFrame < 10)
					return;
				ReportTap();
				return;
		}
	}

	private void Enter(Phase next)
	{
		phase = next;
		phaseFrame = 0;
	}

	private void BeginDrag()
	{
		Rect2 rect = VisibleRect(scroll);
		scrollBefore = scroll.ScrollVertical;
		scroll.GuiInput += _ => scrollEvents++;

		int stops = FindAll<Control>(scroll).Count(c => c != scroll && c.MouseFilter == Control.MouseFilterEnum.Stop);
		GD.Print($"_TouchProbe: list = {scroll.GetPath()}  filter = {scroll.MouseFilter}  deadzone = {scroll.ScrollDeadzone}  "
			+ $"range = {scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page:0}  STOP controls inside = {stops}");

		// Three quarters of the way down the visible list: where a thumb starts pushing a list up.
		dragPoint = new Vector2(rect.GetCenter().X, rect.Position.Y + rect.Size.Y * 0.75f);
		InjectTouch(dragPoint, true);
		dragSteps = 0;
		Enter(Phase.Drag);
	}

	private void ReportDrag()
	{
		double after = scroll.ScrollVertical;
		GD.Print($"_TouchProbe: DRAG {DragStepCount * DragStepUnits:0} units up - list received {scrollEvents} event(s), "
			+ $"scroll {scrollBefore:0} -> {after:0}, buttons pressed: {(pressedLog.Count == 0 ? "none" : string.Join(", ", pressedLog))}");
		if (scrollEvents == 0)
			GD.Print("_TouchProbe: FAIL - the drag never reached the list; something inside it is still STOP");
		else if (pressedLog.Count > 0)
			GD.Print("_TouchProbe: FAIL - a drag pressed a button");
		else if (after == scrollBefore && !DisplayServer.IsTouchscreenAvailable())
			GD.Print("_TouchProbe: drag reached the list; no touchscreen here, so it cannot move (check on the phone)");
		else if (after == scrollBefore)
			GD.Print("_TouchProbe: FAIL - the list did not move");
		else
			GD.Print("_TouchProbe: PASS - the list moved and nothing was pressed");
		pressedLog.Clear();
	}

	private void BeginTap(Button target)
	{
		tapButton = target;
		if (tapButton == null)
		{
			GD.PrintErr("_TouchProbe: nothing to tap");
			GetTree().Quit(1);
			return;
		}

		// Reported from the handler itself, and quit through the main loop rather than GetTree():
		// Continue changes scene, which takes this probe out of the tree before this handler runs.
		tapButton.Pressed += () =>
		{
			tapPressed = true;
			phase = Phase.Done;
			GD.Print("_TouchProbe: PASS - the tap pressed its button");
			(Engine.GetMainLoop() as SceneTree)?.Quit(0);
		};
		InjectTouch(TapPoint(), true);
		GD.Print($"_TouchProbe: tapping {tapButton.GetPath()} (\"{tapButton.Text}\")");
		Enter(Phase.Tap);
	}

	private void ReportTap()
	{
		GD.Print($"_TouchProbe: TAP - target pressed = {tapPressed}, all presses: "
			+ $"{(pressedLog.Count == 0 ? "none" : string.Join(", ", pressedLog))}");
		if (!tapPressed)
			GD.Print("_TouchProbe: FAIL - the tap pressed nothing");
		phase = Phase.Done;
		GetTree().Quit(tapPressed ? 0 : 1);
	}

	private Vector2 TapPoint()
	{
		return tapButton.GetGlobalTransformWithCanvas() * (tapButton.Size / 2f);
	}

	// Window coordinates, which is what a touch driver reports: the viewport undoes its own stretch
	// on the way in, so an event placed in canvas units would land in the wrong place on any window
	// that is not exactly the canvas size.
	private Vector2 ToWindow(Vector2 viewportPoint)
	{
		return GetViewport().GetFinalTransform() * viewportPoint;
	}

	private void InjectTouch(Vector2 viewportPoint, bool pressed)
	{
		Input.ParseInputEvent(new InputEventScreenTouch { Index = 0, Position = ToWindow(viewportPoint), Pressed = pressed });
		Input.FlushBufferedEvents();
	}

	private void InjectDrag(Vector2 viewportPoint, Vector2 viewportRelative)
	{
		Vector2 relative = GetViewport().GetFinalTransform().BasisXform(viewportRelative);
		Input.ParseInputEvent(new InputEventScreenDrag
		{
			Index = 0,
			Position = ToWindow(viewportPoint),
			Relative = relative,
			ScreenRelative = relative,
		});
		Input.FlushBufferedEvents();
	}

	private void WatchButtons()
	{
		foreach (BaseButton b in FindAll<BaseButton>(GetTree().Root))
		{
			if (!watched.Add(b))
				continue;
			BaseButton captured = b;
			b.Pressed += () =>
			{
				pressedLog.Add(captured.Name);
				GD.Print($"_TouchProbe: PRESSED {(captured.IsInsideTree() ? captured.GetPath().ToString() : captured.Name.ToString())}");
			};
		}
	}

	/// <summary>The visible list with the most to scroll, if any list on screen scrolls at all.</summary>
	private ScrollContainer FindScrollableList()
	{
		return FindAll<ScrollContainer>(GetTree().Root)
			.Where(s => s.IsVisibleInTree() && s.GetVScrollBar().MaxValue - s.GetVScrollBar().Page > 40)
			.OrderByDescending(s => s.GetVScrollBar().MaxValue - s.GetVScrollBar().Page)
			.FirstOrDefault();
	}

	private Button FindTapTarget()
	{
		return FindAll<Button>(GetTree().Root).FirstOrDefault(b => b.IsVisibleInTree()
			&& (b.Name == TapTarget || b.Text == TapTarget));
	}

	/// <summary>A visible, enabled button wholly inside the list's visible area.</summary>
	private Button FindButtonInside(ScrollContainer list)
	{
		Rect2 visible = VisibleRect(list);
		return FindAll<Button>(list).FirstOrDefault(b => b.IsVisibleInTree() && !b.Disabled
			&& visible.Encloses(VisibleRect(b)));
	}

	private static Rect2 VisibleRect(Control control)
	{
		Transform2D xform = control.GetGlobalTransformWithCanvas();
		return new Rect2(xform.Origin, xform.BasisXform(control.Size));
	}

	private static string Describe(Control control)
	{
		if (control == null)
			return "(nothing)";
		var chain = new List<string>();
		for (Node n = control; n != null && chain.Count < 6; n = n.GetParent())
		{
			if (n is Control c)
				chain.Add($"{c.Name}[{c.GetType().Name}:{c.MouseFilter}]");
			else if (n is CanvasLayer layer)
			{
				chain.Add($"{layer.Name}[layer {layer.Layer}]");
				break;
			}
		}
		return string.Join(" < ", chain);
	}

	private static IEnumerable<T> FindAll<T>(Node root) where T : Node
	{
		if (root is T match)
			yield return match;
		foreach (Node child in root.GetChildren())
		{
			foreach (T found in FindAll<T>(child))
				yield return found;
		}
	}
}
