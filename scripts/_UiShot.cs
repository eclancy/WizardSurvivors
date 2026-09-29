using Godot;
using System.Linq;
using WizardSurvivors.scripts;

// A screenshot harness for menu work, and the reason menu work can be judged by eye at all.
//
// Earlier attempts in this project to capture the game came back as a flat clear colour from both
// the window backbuffer and a SubViewport, which is why the level-up rebuild was done against
// geometry dumps instead of pictures. The missing piece was not the capture API, it was WHEN:
// `RenderingServer.FramePostDraw` fires after the GPU has finished the frame, and that is the only
// point at which the viewport texture holds anything. _Ready and _Process both read the buffer
// before the draw, which is precisely the clear colour that was seen.
//
// Run:
//   "$GODOT" --path . scenes/_UiShot.tscn -- --scene=res://scenes/MainMenu.tscn --shot=main-menu
//
// Also dumps a GEOMETRY REPORT beside the image: every Control with its rect and font size, plus
// any control wider than the viewport and any font below the Micro floor. A picture shows that a
// screen looks wrong; the report says by how many pixels.
public partial class _UiShot : Node
{
	[Export] public string ScenePath { get; set; } = "res://scenes/MainMenu.tscn";
	[Export] public string ShotName { get; set; } = "shot";
	[Export] public int WarmupFrames { get; set; } = 10;

	// A sub-panel to open before shooting, by the NAME of the button that opens it. Half the menus
	// in this game are panels inside MainMenu that start hidden, and a hidden container is not laid
	// out - measuring one reports its combined minimum size rather than the width it would really
	// get, which reads as a false overflow. Pressing the real button runs the real code path, so
	// what gets measured is what a player would see.
	[Export] public string PressButton { get; set; } = "";

	// Below this is the floor ResponsiveLayout.TextRole.Micro sets, so anything under it got its
	// size from a hardcoded call site rather than from the scale.
	private const int MinReadableFontSize = 16;

	private int frames;
	private bool captured;

	// Gameplay shots past the first minute were impossible without this: a run pauses on its
	// first level-up about eight seconds in, and every photograph after that point came back as
	// the card screen over a frozen arena. Taking the first offered option each time is not a
	// build anybody would choose, but the subject here is the arena, not the loadout.
	// The bot. Without it every gameplay photograph was a corpse: the player stands in the spawn
	// point, the swarm closes, and the shot comes back as the game-over panel at eight seconds.
	// It only has to survive long enough to be photographed, so this is the balance harness kite
	// with nothing clever in it - move away from the weighted centre of everything close, lean
	// toward the arena middle so it cannot reverse into a wall and be pinned there.
	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };

	private void DriveMovement()
	{
		Node2D self = null;
		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is Node2D found && IsInstanceValid(found))
				self = found;
		}

		if (self == null)
			return;

		Vector2 away = Vector2.Zero;
		int counted = 0;
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is not Node2D e || !IsInstanceValid(n))
				continue;

			Vector2 offset = self.GlobalPosition - e.GlobalPosition;
			float d = offset.Length();
			if (d > 260f || d <= 0.01f)
				continue;

			away += offset / d * (1f - d / 260f);
			counted++;
		}

		Vector2 toCentre = -self.GlobalPosition;
		Vector2 desired = counted > 0
			? away.Normalized() + toCentre.Normalized() * 0.45f
			: toCentre.Normalized();
		if (desired.LengthSquared() > 0.0001f)
			desired = desired.Normalized();

		Press("move_right", "move_left", desired.X);
		Press("move_down", "move_up", desired.Y);
	}

	private static void Press(string positive, string negative, float axis)
	{
		if (axis > 0.02f)
		{
			Input.ActionRelease(negative);
			Input.ActionPress(positive, System.Math.Min(1f, axis));
		}
		else if (axis < -0.02f)
		{
			Input.ActionRelease(positive);
			Input.ActionPress(negative, System.Math.Min(1f, -axis));
		}
		else
		{
			Input.ActionRelease(positive);
			Input.ActionRelease(negative);
		}
	}

	private void DismissLevelUpMenus(Node root)
	{
		if (root is LevelUpMenu menu && menu.Visible)
		{
			System.Collections.Generic.IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions();
			if (options != null && options.Count > 0)
				menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, options[0].SpellId);
			return;
		}

		foreach (Node child in root.GetChildren())
			DismissLevelUpMenus(child);
	}

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--scene="))
				ScenePath = arg.Substring("--scene=".Length);
			else if (arg.StartsWith("--shot="))
				ShotName = arg.Substring("--shot=".Length);
			else if (arg.StartsWith("--press="))
				PressButton = arg.Substring("--press=".Length);
			// Gameplay needs hundreds of frames before there is a crowd worth photographing; a
			// menu is laid out in ten.
			else if (arg.StartsWith("--frames="))
				WarmupFrames = int.Parse(arg.Substring("--frames=".Length));
			// Photographing a RUN rather than a menu needs the two selections a real run would
			// have made on the way in. Without them every gameplay shot is chapter one as
			// character zero, which is a silent wrong answer rather than an error.
			else if (arg.StartsWith("--stage="))
				Global.SelectedStageIdx = int.Parse(arg.Substring("--stage=".Length));
			else if (arg.StartsWith("--character="))
				Global.SelectedCharacterIdx = int.Parse(arg.Substring("--character=".Length));
			// Gameplay wants hundreds of frames before there is a crowd worth photographing, and
			// at 1x that is ten real seconds of waiting per shot.
			else if (arg.StartsWith("--timescale="))
				Engine.TimeScale = float.Parse(arg.Substring("--timescale=".Length));
		}

		if (!ResourceLoader.Exists(ScenePath))
		{
			GD.PrintErr($"_UiShot: no scene at '{ScenePath}'");
			GetTree().Quit(1);
			return;
		}

		var packed = GD.Load<PackedScene>(ScenePath);
		Node instance = packed?.Instantiate();
		if (instance == null)
		{
			GD.PrintErr($"_UiShot: '{ScenePath}' failed to instantiate");
			GetTree().Quit(1);
			return;
		}

		AddChild(instance);
		MoveChild(instance, 0);

		if (!string.IsNullOrEmpty(PressButton))
		{
			Button target = FindButton(instance, PressButton);
			if (target == null)
				GD.PrintErr($"_UiShot: no button named '{PressButton}'");
			else
				target.EmitSignal(BaseButton.SignalName.Pressed);
		}

		RenderingServer.FramePostDraw += OnFramePostDraw;
	}

	public override void _ExitTree()
	{
		RenderingServer.FramePostDraw -= OnFramePostDraw;
		// Left pressed, the input stays held into whatever scene loads next.
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	private static Button FindButton(Node node, string name)
	{
		if (node is Button button && button.Name == name)
			return button;
		foreach (Node child in node.GetChildren())
		{
			Button found = FindButton(child, name);
			if (found != null)
				return found;
		}
		return null;
	}

	private void OnFramePostDraw()
	{
		// Menus build themselves over several frames - fonts resolve, containers sort, and the
		// responsive column count is re-applied on a viewport signal. Capturing the first drawn
		// frame catches a half-laid-out screen, so give it a few.
		if (captured || ++frames < WarmupFrames)
		{
			// A paused tree still draws, so the pause has to be cleared from inside the draw
			// callback rather than from _Process - which does not run while a level-up is up.
			if (captured)
				return;
			if (GetTree().Paused)
				DismissLevelUpMenus(this);
			else
				DriveMovement();
			return;
		}
		captured = true;

		Report();

		Image image = GetViewport()?.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.PrintErr("_UiShot: no viewport texture");
			GetTree().Quit(1);
			return;
		}

		string path = $"user://{ShotName}.png";
		Error err = image.SavePng(path);
		GD.Print(err == Error.Ok
			? $"_UiShot: {ProjectSettings.GlobalizePath(path)}"
			: $"_UiShot: save failed ({err})");
		GetTree().Quit(err == Error.Ok ? 0 : 1);
	}

	private static string Truncate(string text)
	{
		text = text?.Replace('\n', ' ') ?? string.Empty;
		return text.Length <= 40 ? text : text.Substring(0, 40) + "...";
	}

	private void Report()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		GD.Print($"_UiShot: viewport {viewport.X}x{viewport.Y}");

		int overflow = 0;
		int tiny = 0;
		Walk(GetChild(0), viewport, ref overflow, ref tiny);

		GD.Print($"_UiShot: {overflow} control(s) wider than the viewport, {tiny} below {MinReadableFontSize}px");
	}

	private void Walk(Node node, Vector2 viewport, ref int overflow, ref int tiny)
	{
		if (node is Control control)
		{
			// A control wider than the viewport is the horizontal-scroll bug in its raw form: the
			// content simply does not fit the 720 units keep_width pins us to.
			if (control.Size.X > viewport.X + 0.5f)
			{
				overflow++;
				GD.Print($"  WIDE  {control.GetPath()}  {control.Size.X:0}px > {viewport.X:0}px"
					+ $"  (min {control.GetCombinedMinimumSize().X:0})");
				// A container cannot shrink below its widest child's MINIMUM, so the child with the
				// largest combined minimum is the one actually setting the width. Naming it turns
				// "this screen is too wide" into "this label is too wide", which is fixable.
				Control worst = null;
				foreach (Node child in control.GetChildren())
				{
					if (child is not Control c)
						continue;
					if (worst == null || c.GetCombinedMinimumSize().X > worst.GetCombinedMinimumSize().X)
						worst = c;
				}
				if (worst != null)
					GD.Print($"        widest child: {worst.Name} ({worst.GetType().Name}) min {worst.GetCombinedMinimumSize().X:0}"
						+ (worst is Label l ? $"  text=\"{Truncate(l.Text)}\"" : string.Empty));
			}

			if (control is ScrollContainer scroll
				&& scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled)
			{
				GD.Print($"  HSCROLL  {scroll.GetPath()}  mode={scroll.HorizontalScrollMode}");
			}

			// GetThemeFontSize resolves the whole chain - local override, then theme, then
			// default - so this is the size actually drawn rather than the size someone asked for.
			if (control is Label or Button or RichTextLabel)
			{
				int size = control.GetThemeFontSize("font_size");
				if (size > 0 && size < MinReadableFontSize)
				{
					tiny++;
					GD.Print($"  TINY  {control.GetPath()}  {size}px");
				}
			}
		}

		foreach (Node child in node.GetChildren())
			Walk(child, viewport, ref overflow, ref tiny);
	}
}
