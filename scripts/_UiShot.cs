using Godot;
using System.Linq;

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
			return;
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
