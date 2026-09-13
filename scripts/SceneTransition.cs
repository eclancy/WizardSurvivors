using Godot;
using WizardSurvivors.scripts;

// Loading feedback for menu scene changes.
//
// GetTree().ChangeSceneToFile() loads the whole next scene synchronously on the main thread, and
// only swaps once it is done. Clicking a wizard or a stage therefore left the menu sitting there,
// visually unchanged and unresponsive, for as long as the load took - the card looked like it had
// not registered the click at all, which is the "awkward hold on the page" this replaces.
//
// The fix is two halves, and both matter:
//   1. An overlay goes up on the very next frame, so the click is acknowledged immediately.
//   2. The load runs through ResourceLoader's threaded API, so the main thread stays free to draw
//      that overlay and animate its progress instead of freezing on the load.
//
// Only the instantiation of the finished PackedScene still happens on the main thread, and by then
// the player is looking at a loading screen rather than a menu that appears to be broken.
public partial class SceneTransition : CanvasLayer
{
	// Above every menu's own CanvasLayers, and above the HUD of anything that transitions mid-run.
	private const int OverlayLayer = 200;
	private const string OverlayName = "SceneTransitionOverlay";

	private string targetPath = string.Empty;
	private string caption = string.Empty;
	private ProgressBar progressBar = null!;
	private Label hintLabel = null!;
	private float elapsed;
	private bool finished;

	/// <summary>
	/// Puts up a loading overlay and swaps to <paramref name="scenePath"/> as soon as it has
	/// loaded. Call this instead of <c>GetTree().ChangeSceneToFile</c> from any menu click.
	/// </summary>
	/// <param name="caption">Where the player is going - the stage or wizard name.</param>
	public static void ChangeScene(Node from, string scenePath, string caption = "")
	{
		if (from == null || !IsInstanceValid(from))
			return;

		SceneTree tree = from.GetTree();
		if (tree == null)
			return;

		if (!ResourceLoader.Exists(scenePath))
		{
			GD.PushError($"SceneTransition: scene not found: {scenePath}");
			return;
		}

		// Parent to the current scene root rather than to the clicked control: the overlay must
		// outlive the card, and it is freed with the old scene at the swap either way.
		Node host = tree.CurrentScene ?? from;

		// A second click while the first load is in flight would start a second threaded request
		// for the same path and race the swap. The overlay eats input, but a keyboard-activated
		// button can still fire on the same frame it goes up.
		if (host.GetNodeOrNull<SceneTransition>(OverlayName) != null)
			return;

		host.AddChild(new SceneTransition
		{
			Name = OverlayName,
			targetPath = scenePath,
			caption = caption ?? string.Empty
		});
	}

	public override void _Ready()
	{
		Layer = OverlayLayer;
		BuildOverlay();

		// useSubThreads stays false. One background thread is enough to keep the main thread
		// drawing, and sub-threads have historically deadlocked on scenes that load scripts.
		Error requested = ResourceLoader.LoadThreadedRequest(targetPath, "", false);
		if (requested != Error.Ok)
		{
			GD.PushWarning($"SceneTransition: threaded load of {targetPath} refused ({requested}); loading synchronously.");
			Callable.From(SwapSynchronously).CallDeferred();
			return;
		}

		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (finished)
			return;

		elapsed += (float)delta;

		var progress = new Godot.Collections.Array();
		ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(targetPath, progress);

		if (progress.Count > 0)
			progressBar.Value = Mathf.Lerp((float)progressBar.Value, (float)progress[0].AsDouble() * 100f, 0.25f);

		// The dots are the honest signal that the process is alive; a progress bar that reports
		// one coarse step looks identical to a hang.
		hintLabel.Text = "Loading" + new string('.', 1 + (int)(elapsed * 2f) % 3);

		switch (status)
		{
			case ResourceLoader.ThreadLoadStatus.InProgress:
				return;

			case ResourceLoader.ThreadLoadStatus.Loaded:
				finished = true;
				SetProcess(false);
				if (ResourceLoader.LoadThreadedGet(targetPath) is PackedScene packed)
					Callable.From(() => SwapToPacked(packed)).CallDeferred();
				else
					Callable.From(SwapSynchronously).CallDeferred();
				return;

			default:
				// Failed or InvalidResource. Falling back keeps a broken threaded load from
				// stranding the player on a loading screen with no way out.
				GD.PushWarning($"SceneTransition: threaded load of {targetPath} ended as {status}; loading synchronously.");
				finished = true;
				SetProcess(false);
				Callable.From(SwapSynchronously).CallDeferred();
				return;
		}
	}

	private void SwapToPacked(PackedScene packed)
	{
		GetTree().ChangeSceneToPacked(packed);
	}

	private void SwapSynchronously()
	{
		GetTree().ChangeSceneToFile(targetPath);
	}

	private void BuildOverlay()
	{
		// Stop, not Ignore: the overlay is also the input block that stops a second selection
		// being made against a menu that is already on its way out.
		var shade = new ColorRect
		{
			Color = new Color(0.03f, 0.03f, 0.05f, 0.93f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(shade);

		var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(center);

		var column = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(420f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		column.AddThemeConstantOverride("separation", 14);
		center.AddChild(column);

		if (!string.IsNullOrWhiteSpace(caption))
		{
			var captionLabel = new Label
			{
				Text = caption,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			ResponsiveLayout.SetFont(captionLabel, ResponsiveLayout.TextRole.Display);
			captionLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.90f, 0.74f));
			column.AddChild(captionLabel);
		}

		hintLabel = new Label
		{
			Text = "Loading",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(hintLabel, ResponsiveLayout.TextRole.Label);
		hintLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.90f));
		column.AddChild(hintLabel);

		progressBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0f, 10f),
			ShowPercentage = false,
			MinValue = 0,
			MaxValue = 100,
			Value = 0
		};
		var background = new StyleBoxFlat { BgColor = new Color(0.14f, 0.15f, 0.19f, 0.95f) };
		background.SetCornerRadiusAll(5);
		var fill = new StyleBoxFlat { BgColor = new Color(0.55f, 0.78f, 0.98f, 0.95f) };
		fill.SetCornerRadiusAll(5);
		progressBar.AddThemeStyleboxOverride("background", background);
		progressBar.AddThemeStyleboxOverride("fill", fill);
		column.AddChild(progressBar);
	}
}
