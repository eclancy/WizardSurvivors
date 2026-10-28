using Godot;
using WizardSurvivors.scripts;

// The beat when a stage secret is found (.ai/stage-secrets.md 7.6): its name, what it did in the run,
// and - the first time ever - what it paid for good. The side event's success beat, quieter: no column
// of light, a plate under the HUD that comes and goes in four seconds. Takes no input and pauses nothing.
// Layer 7, with the set-assembled moment: above the touch stick, under every menu.
public partial class SecretToast : CanvasLayer
{
	private const float InSeconds = 0.3f;
	private const float HoldSeconds = 3.6f;
	private const float OutSeconds = 0.6f;
	private const float WideTop = 110f;
	private const float NarrowTop = 160f;
	private const float MaxWidth = 520f;

	private Control root = null!;
	private PanelContainer plate = null!;
	private Label title = null!;
	private Label line = null!;
	private Label paid = null!;
	private float clock = -1f;

	public override void _Ready()
	{
		Layer = 7;
		ProcessMode = ProcessModeEnum.Pausable;
		root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		plate = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		var face = new StyleBoxFlat { BgColor = new Color(CodexRows.Stone.Face, 0.9f), BorderColor = CodexRows.Stone.Reward };
		face.SetBorderWidthAll(2);
		face.SetCornerRadiusAll(6);
		face.ContentMarginLeft = face.ContentMarginRight = 20f;
		face.ContentMarginTop = 10f;
		face.ContentMarginBottom = 12f;
		plate.AddThemeStyleboxOverride("panel", face);
		root.AddChild(plate);

		var lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		lines.AddThemeConstantOverride("separation", 2);
		plate.AddChild(lines);
		title = MakeLine(lines, ResponsiveLayout.TextRole.Title, CodexRows.Stone.Heading);
		line = MakeLine(lines, ResponsiveLayout.TextRole.Label, CodexRows.Stone.Ink);
		paid = MakeLine(lines, ResponsiveLayout.TextRole.Label, CodexRows.Stone.Done);
		SetProcess(false);
	}

	public void Show(string name, string runReward, string metaReward, bool firstEver)
	{
		title.Text = firstEver ? $"Secret found: {name}" : name;
		line.Text = runReward;
		paid.Text = metaReward;
		paid.Visible = metaReward.Length > 0;
		clock = 0f;
		root.Visible = true;
		root.Modulate = new Color(1f, 1f, 1f, 0f);
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (clock < 0f)
			return;
		clock += (float)delta;
		float alpha = clock < InSeconds ? clock / InSeconds
			: clock < InSeconds + HoldSeconds ? 1f
			: 1f - (clock - InSeconds - HoldSeconds) / OutSeconds;
		root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));

		Vector2 view = root.Size;
		float width = Mathf.Min(MaxWidth, view.X - 32f);
		plate.CustomMinimumSize = new Vector2(width, 0f);
		line.CustomMinimumSize = new Vector2(width - 40f, 0f);
		paid.CustomMinimumSize = new Vector2(width - 40f, 0f);
		plate.Size = new Vector2(width, plate.GetCombinedMinimumSize().Y);
		plate.Position = new Vector2((view.X - width) * 0.5f, ResponsiveLayout.IsNarrow(root) ? NarrowTop : WideTop);

		if (clock >= InSeconds + HoldSeconds + OutSeconds)
		{
			clock = -1f;
			root.Visible = false;
			SetProcess(false);
		}
	}

	private Label MakeLine(Container parent, ResponsiveLayout.TextRole role, Color colour)
	{
		var label = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		parent.AddChild(label);
		ResponsiveLayout.SetFont(label, role);
		label.AddThemeColorOverride("font_color", colour);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
		label.AddThemeConstantOverride("outline_size", 4);
		return label;
	}
}
