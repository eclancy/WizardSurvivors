using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WizardSurvivors.scripts;
using HttpClient = System.Net.Http.HttpClient;

/// <summary>
/// "Report a bug": a player types what went wrong and it is sent to Eric. Opened from the main
/// menu and from the pause screen. Ported from DrawFight's SuggestScreen and FighterIntake, and it
/// goes to the same server (EricClancyEngineeringAndCrafts: intake/server.js, POST
/// /api/draw/suggest), which saves it under /data/suggestions and emails it. Where reports go and
/// exactly what is sent: .ai/releasing.md, "Bug reports".
/// </summary>
/// <remarks>
/// WHAT IS SENT IS WHAT IS SHOWN. The lines under "Sent with it" are built by one method
/// (<see cref="ContextLines"/>) and that same list is what goes on the front of the report, so the
/// screen cannot promise one thing and send another. It is the version, the platform, the canvas
/// and window size, and - from the pause screen - the run: chapter, wizard, time, level and the
/// spells held. Nothing else: no save file, no log, no device or account identifier.
///
/// System.Net.Http rather than Godot's HTTPRequest node: it is what UpdateScreen already uses
/// here, and what DrawFight's intake uses on its Android build, and a Task polled from _Process
/// needs nothing in the tree that the paused run could stop. The Android preset needs
/// permissions/internet for it, which a debug export only adds by itself when remote debugging.
///
/// The typed text outlives the screen (<see cref="draft"/>): Back, Escape, B, a failed send or a
/// closed pause screen all keep it, and only a report the server accepted clears it. A bug report
/// is exactly the text a player will not type twice.
/// </remarks>
public partial class ReportBugScreen : CanvasLayer
{
	/// <summary>The intake server. Same site as DrawFight's, deliberately - one server, one inbox.</summary>
	public const string Site = "https://ecec.dev";

	/// <summary>Points the game at another server - a local one, for testing without emailing Eric.</summary>
	public const string UrlOverrideVariable = "WIZARDSURVIVORS_INTAKE_URL";

	private const string Route = "/api/draw/suggest";

	/// <summary>
	/// The server's allowlist key (intake/server.js, REPORT_GAMES). It must be exactly this string:
	/// anything else, or nothing, files the report as a DrawFight suggestion.
	/// </summary>
	public const string GameField = "Wizard Survivors";

	public const int MaxChars = 2000;
	private const int MaxNameChars = 40;

	// Long enough for a phone on a weak signal, short enough that a dead connection says so while
	// the player is still looking at the screen.
	private const double TimeoutSeconds = 15.0;

	// Above the pause screen (100) and the set detail screen (110) it can be opened over.
	private const int ScreenLayer = 120;

	// Kept across openings for the life of the process; see the class remarks.
	private static string draft = string.Empty;
	private static string lastName = string.Empty;

	private static readonly CodexRows.Palette Ink = CodexRows.Stone;

	private enum Phase { Editing, Sending, Done }

	private Phase phase = Phase.Editing;
	private Task<string> sending;
	private IReadOnlyList<string> runContext = Array.Empty<string>();
	private Action closed;

	private PanelContainer panel = null!;
	private ScrollContainer scroll = null!;
	private float panelRestBottom;
	private Control form = null!;
	private Control done = null!;
	private LineEdit nameField = null!;
	private TextEdit description = null!;
	private Label counter = null!;
	private Label status = null!;
	private Button sendButton = null!;
	private Button backButton = null!;

	/// <summary>True while a report is on its way. The screenshot harness waits on it.</summary>
	public bool IsSending => phase == Phase.Sending;

	/// <summary>
	/// Puts the screen up over <paramref name="parent"/>.
	/// </summary>
	/// <param name="runContext">The run's lines for the report ("Chapter: ..."), or null from the
	/// main menu. Built by the caller, which is the one that knows the run.</param>
	/// <param name="onClosed">Called once the screen has gone, so the screen under it can take
	/// focus back - the pause screen's root has to, or it stops hearing B and Escape.</param>
	public static ReportBugScreen Open(Node parent, IReadOnlyList<string> runContext, Action onClosed)
	{
		var screen = new ReportBugScreen
		{
			Name = "ReportBugScreen",
			runContext = runContext ?? Array.Empty<string>(),
			closed = onClosed,
		};
		parent.AddChild(screen);
		return screen;
	}

	public override void _Ready()
	{
		Layer = ScreenLayer;
		// Always: from the pause screen it opens over a paused tree, and a screen that stopped
		// with the tree could never process its own Send or Back.
		ProcessMode = ProcessModeEnum.Always;
		var root = Build();
		SfxPlayer.Global(SfxCatalog.UiOpen);

		// Clear of the phone's rounded corners and camera hole; nothing on desktop (SafeArea).
		SafeArea.Apply(root);
		panelRestBottom = panel.OffsetBottom;
		MenuNavigator.Attach(this, sendButton);

		// A keyboard is ready to type, so the field takes focus. Not on a phone: there, focusing
		// the field is what raises the on-screen keyboard, and it would cover the screen before the
		// player has read what it is for. A tap on the field raises it instead. Deferred so it runs
		// after the navigator's own first selection, which would otherwise take it back to Send.
		if (!OS.HasFeature("mobile"))
			CallDeferred(MethodName.FocusDescription);
	}

	// Without letting the list follow the focus down to it: the screen opens at its top, name
	// field and all (MenuNavigator.GrabWithoutScrolling, for the same reason).
	private void FocusDescription()
	{
		scroll.FollowFocus = false;
		description.GrabFocus();
		scroll.FollowFocus = true;
	}

	private Control Build()
	{
		var root = new Control
		{
			Name = "ReportBugRoot",
			// STOP: a click that misses the panel must not reach the menu underneath.
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.025f, 0.04f, 0.86f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);

		bool narrow = ResponsiveLayout.IsNarrow(this);
		panel = new PanelContainer { Name = "ReportBugPanel" };
		panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (narrow)
		{
			panel.OffsetLeft = 12f;
			panel.OffsetRight = -12f;
			panel.OffsetTop = 16f;
			panel.OffsetBottom = -16f;
		}
		else
		{
			// A centred column the width of a page, like the pause screen's, rather than a text
			// box 1200 units wide whose lines the eye loses.
			// 920 wide is what fits the intro and the closing promise on one line each, which is
			// what keeps the whole form inside a 720-tall canvas without scrolling.
			const float HalfWidth = 460f;
			panel.AnchorLeft = 0.5f;
			panel.AnchorRight = 0.5f;
			panel.OffsetLeft = -HalfWidth;
			panel.OffsetRight = HalfWidth;
			panel.OffsetTop = 24f;
			panel.OffsetBottom = -24f;
		}
		BonelightSkin.ApplyPanel(panel);
		root.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		var heading = new Label { Text = "Report a bug" };
		ResponsiveLayout.SetFont(heading, ResponsiveLayout.TextRole.Display);
		heading.AddThemeColorOverride("font_color", Ink.Heading);
		column.AddChild(heading);

		form = BuildForm();
		column.AddChild(form);

		done = BuildDone();
		done.Visible = false;
		column.AddChild(done);

		status = new Label { Name = "ReportBugStatus", Visible = false };
		ResponsiveLayout.SetBodyText(status, ResponsiveLayout.TextRole.Label);
		column.AddChild(status);

		var buttons = new HBoxContainer();
		buttons.AddThemeConstantOverride("separation", 10);
		column.AddChild(buttons);

		backButton = MakeButton("ReportBugBack", "Back", BonelightSkin.Emphasis.Normal);
		backButton.Pressed += Close;
		buttons.AddChild(backButton);

		sendButton = MakeButton("ReportBugSend", "Send bug report", BonelightSkin.Emphasis.Primary);
		sendButton.Pressed += TrySend;
		buttons.AddChild(sendButton);

		return root;
	}

	private Control BuildForm()
	{
		var box = new VBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		box.AddThemeConstantOverride("separation", 8);

		var intro = new Label
		{
			Text = "Tell us what you were doing and what went wrong. It goes straight to the game's maker.",
		};
		ResponsiveLayout.SetBodyText(intro, ResponsiveLayout.TextRole.Label);
		intro.AddThemeColorOverride("font_color", Ink.InkSoft);
		box.AddChild(intro);

		// Everything below the intro scrolls, so the phone's keyboard can take the bottom of the
		// screen and the field and the buttons still fit above it (see _Process).
		scroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true,
		};
		box.AddChild(scroll);

		var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		content.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(content);

		// The name and its label share a row, and so do "What happened" and its counter: a row
		// saved each is most of what the form needed to fit a PC canvas without scrolling.
		var nameRow = new HBoxContainer();
		nameRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(nameRow);
		Label nameLabel = FieldLabel("Your name (optional)");
		nameLabel.VerticalAlignment = VerticalAlignment.Center;
		nameRow.AddChild(nameLabel);
		nameField = new LineEdit
		{
			Name = "ReportBugName",
			Text = lastName,
			MaxLength = MaxNameChars,
			PlaceholderText = "Blank stays anonymous",
			VirtualKeyboardEnabled = true,
			CustomMinimumSize = new Vector2(0f, ResponsiveLayout.MinTouchTarget),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		StyleField(nameField);
		nameRow.AddChild(nameField);

		var whatRow = new HBoxContainer();
		content.AddChild(whatRow);
		Label what = FieldLabel("What happened");
		what.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		whatRow.AddChild(what);
		counter = new Label { HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
		ResponsiveLayout.SetFont(counter, ResponsiveLayout.TextRole.Label);
		whatRow.AddChild(counter);

		description = new TextEdit
		{
			Name = "ReportBugText",
			Text = draft,
			PlaceholderText = "What were you doing, and what went wrong? Which chapter, which wizard, what did you press, and what did you expect to happen?",
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			// Tab leaves the field rather than typing a tab into a bug report.
			TabInputMode = false,
			VirtualKeyboardEnabled = true,
			VirtualKeyboardShowOnFocus = true,
			CustomMinimumSize = new Vector2(0f, ResponsiveLayout.IsNarrow(this) ? 280f : 130f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		StyleField(description);
		description.TextChanged += OnDescriptionChanged;
		content.AddChild(description);
		UpdateCounter();

		var sentHeading = FieldLabel("Sent with it");
		sentHeading.AddThemeColorOverride("font_color", Ink.Heading);
		content.AddChild(sentHeading);

		// Read-only and not focusable: a Label on a sunk face, so it reads as information about
		// the report rather than as a second field to fill in.
		var sentFace = new PanelContainer();
		var faceStyle = new StyleBoxFlat { BgColor = Ink.Face, BorderColor = Ink.Edge };
		faceStyle.SetBorderWidthAll(1);
		faceStyle.SetCornerRadiusAll(4);
		faceStyle.SetContentMarginAll(10);
		sentFace.AddThemeStyleboxOverride("panel", faceStyle);
		content.AddChild(sentFace);

		var sent = new Label { Name = "ReportBugContext", Text = string.Join("\n", ContextLines()) };
		ResponsiveLayout.SetBodyText(sent, ResponsiveLayout.TextRole.Label);
		sent.AddThemeColorOverride("font_color", Ink.InkSoft);
		sentFace.AddChild(sent);

		var promise = new Label
		{
			Text = "That and what you type is all: no save file, no logs, nothing that identifies you.",
		};
		ResponsiveLayout.SetBodyText(promise, ResponsiveLayout.TextRole.Label);
		promise.AddThemeColorOverride("font_color", Ink.InkMeta);
		content.AddChild(promise);

		return box;
	}

	private Control BuildDone()
	{
		var box = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 12);

		var thanks = new Label { Text = "Thank you!" };
		ResponsiveLayout.SetFont(thanks, ResponsiveLayout.TextRole.Title);
		thanks.AddThemeColorOverride("font_color", Ink.Done);
		box.AddChild(thanks);

		var body = new Label
		{
			Text = "Your report is on its way to the person who makes Wizard Survivors.",
		};
		ResponsiveLayout.SetBodyText(body);
		body.AddThemeColorOverride("font_color", Ink.Ink);
		box.AddChild(body);
		return box;
	}

	private static Label FieldLabel(string text)
	{
		var label = new Label { Text = text };
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Body);
		label.AddThemeColorOverride("font_color", Ink.Ink);
		return label;
	}

	// The field face is the stone row face the Deeds list uses, so its inks are already measured
	// on it by tools/art/ui_contrast.py ("codex row on stone (face)"): typed text in Ink, the
	// placeholder in InkMeta at 6:1. Focus is a gold edge, because MenuFocusHighlight rings buttons
	// only and a field a pad has landed on must still look selected.
	private void StyleField(Control field)
	{
		StyleBoxFlat Face(Color border, int width)
		{
			var style = new StyleBoxFlat { BgColor = Ink.Face, BorderColor = border };
			style.SetBorderWidthAll(width);
			style.SetCornerRadiusAll(4);
			style.SetContentMarginAll(10);
			return style;
		}

		field.AddThemeStyleboxOverride("normal", Face(Ink.Edge, 1));
		field.AddThemeStyleboxOverride("focus", Face(Ink.Reward, 2));
		field.AddThemeStyleboxOverride("read_only", Face(Ink.QuietEdge, 1));
		field.AddThemeColorOverride("font_color", Ink.Ink);
		field.AddThemeColorOverride("font_readonly_color", Ink.InkMeta);
		field.AddThemeColorOverride("font_placeholder_color", Ink.InkMeta);
		field.AddThemeColorOverride("caret_color", Ink.Ink);
		field.AddThemeColorOverride("selection_color", new Color(Ink.Reward, 0.35f));
		ResponsiveLayout.SetFont(field, ResponsiveLayout.TextRole.Body);
	}

	private Button MakeButton(string name, string text, BonelightSkin.Emphasis emphasis)
	{
		var button = new Button
		{
			Name = name,
			Text = text,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 56f),
		};
		BonelightSkin.StyleButton(button, emphasis);
		ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Body);
		return button;
	}

	/// <summary>
	/// Everything attached to the report, one line each, exactly as sent and as shown. The first
	/// line is a header that opens the email body, so a report read on a phone says where it came
	/// from before anything else.
	/// </summary>
	private List<string> ContextLines()
	{
		Vector2 canvas = GetViewport().GetVisibleRect().Size;
		Vector2I window = DisplayServer.WindowGetSize();
		Vector2I screen = DisplayServer.ScreenGetSize();
		string shape = DisplayProfile.IsPortrait ? "phone" : "PC";
		var lines = new List<string>
		{
			$"[{GameField} {UpdateScreen.CurrentVersion} | {OS.GetName()} | {shape} canvas {canvas.X:0}x{canvas.Y:0}]",
			$"Window: {window.X}x{window.Y} on a {screen.X}x{screen.Y} screen",
		};
		if (runContext.Count == 0)
			lines.Add("Sent from the main menu, not during a run");
		else
			lines.AddRange(runContext);
		return lines;
	}

	private void OnDescriptionChanged()
	{
		// TextEdit has no MaxLength of its own. Cut at the limit and put the caret back where the
		// typing was, so a paste that overflows loses its tail rather than jumping the cursor.
		if (description.Text.Length > MaxChars)
		{
			int line = description.GetCaretLine();
			int column = description.GetCaretColumn();
			description.Text = description.Text.Substring(0, MaxChars);
			description.SetCaretLine(Math.Min(line, description.GetLineCount() - 1));
			description.SetCaretColumn(Math.Min(column, description.GetLine(description.GetCaretLine()).Length));
		}
		draft = description.Text;
		UpdateCounter();
	}

	private void UpdateCounter()
	{
		int length = description.Text.Length;
		counter.Text = $"{length} / {MaxChars}";
		counter.AddThemeColorOverride("font_color", length >= MaxChars ? Ink.Flag : Ink.InkMeta);
	}

	private void TrySend()
	{
		if (phase != Phase.Editing)
			return;

		string text = description.Text.Trim();
		if (text.Length == 0)
		{
			Say("Tell us what happened first.", Ink.Flag);
			SfxPlayer.Global(SfxCatalog.UiDenied);
			description.GrabFocus();
			return;
		}

		lastName = nameField.Text.Trim();
		var report = new StringBuilder();
		foreach (string line in ContextLines())
			report.Append(line).Append('\n');
		report.Append("---\n").Append(text);

		// game and kind are read by the server's allowlist and ignored by older servers; name,
		// suggestion and version are the fields DrawFight has always sent.
		var fields = new List<KeyValuePair<string, string>>
		{
			new("game", GameField),
			new("kind", "bug"),
			new("name", lastName),
			new("version", UpdateScreen.CurrentVersion),
			new("suggestion", report.ToString()),
		};

		SfxPlayer.Global(SfxCatalog.UiClick);
		SetPhase(Phase.Sending);
		Say("Sending...", Ink.InkSoft);
		sending = Send(fields);
	}

	public override void _Process(double delta)
	{
		if (phase == Phase.Sending && sending != null && sending.IsCompleted)
			Finished(sending.Result);
		FitAboveKeyboard();
	}

	// The phone's on-screen keyboard is drawn over the game, not made room for, so while it is up
	// the panel's foot is lifted clear of it: the field being typed in and the Send button both
	// stay in view, and the list in between scrolls. Nothing happens on a desktop, which has no
	// such keyboard - and where asking for its height is an engine error every frame, not a 0.
	private void FitAboveKeyboard()
	{
		if (!DisplayServer.HasFeature(DisplayServer.Feature.VirtualKeyboard))
			return;
		int keyboard = DisplayServer.VirtualKeyboardGetHeight();
		Vector2I window = DisplayServer.WindowGetSize();
		float lift = 0f;
		if (keyboard > 0 && window.Y > 0)
			lift = keyboard * GetViewport().GetVisibleRect().Size.Y / window.Y + 8f;
		float bottom = Mathf.Min(panelRestBottom, -lift);
		if (!Mathf.IsEqualApprox(panel.OffsetBottom, bottom))
			panel.OffsetBottom = bottom;
	}

	private void Finished(string error)
	{
		sending = null;
		if (error != null)
		{
			SetPhase(Phase.Editing);
			Say(error, Ink.Flag);
			SfxPlayer.Global(SfxCatalog.UiDenied);
			sendButton.GrabFocus();
			return;
		}

		draft = string.Empty;
		SetPhase(Phase.Done);
		Say(string.Empty, Ink.InkSoft);
	}

	private void SetPhase(Phase next)
	{
		phase = next;
		bool editing = next == Phase.Editing;
		nameField.Editable = editing;
		description.Editable = editing;
		sendButton.Disabled = !editing;
		sendButton.Text = next == Phase.Sending ? "Sending..." : "Send bug report";
		backButton.Disabled = next == Phase.Sending;
		form.Visible = next != Phase.Done;
		done.Visible = next == Phase.Done;
		sendButton.Visible = next != Phase.Done;
		if (next == Phase.Done)
		{
			// The one thing left to press. Primary, and selected, so A or Enter leaves at once.
			BonelightSkin.StyleButton(backButton, BonelightSkin.Emphasis.Primary);
			backButton.GrabFocus();
		}
	}

	private void Say(string text, Color ink)
	{
		status.Text = text;
		status.Visible = text.Length > 0;
		status.AddThemeColorOverride("font_color", ink);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// After the GUI has had it, so Escape inside a text field first lets the field stop editing.
		if (!@event.IsActionPressed("ui_cancel") || phase == Phase.Sending)
			return;
		GetViewport().SetInputAsHandled();
		Close();
	}

	private void Close()
	{
		if (phase == Phase.Sending)
			return;
		if (phase == Phase.Editing)
			draft = description.Text;
		SfxPlayer.Global(SfxCatalog.UiBack);
		// Hidden first, so the navigators under it stop treating its buttons as a selection on
		// this frame rather than on the next.
		Visible = false;
		QueueFree();
		closed?.Invoke();
	}

	private static string BaseUrl
	{
		get
		{
			string url = OS.GetEnvironment(UrlOverrideVariable);
			return string.IsNullOrEmpty(url) ? Site : url.TrimEnd('/');
		}
	}

	/// <summary>
	/// Posts the report as multipart/form-data. Null when the server took it, otherwise what went
	/// wrong in words that can go straight on screen - the server's own message where it gave one.
	/// </summary>
	private static async Task<string> Send(IReadOnlyList<KeyValuePair<string, string>> fields)
	{
		string url = BaseUrl + Route;
		try
		{
			using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
			http.DefaultRequestHeaders.UserAgent.ParseAdd($"WizardSurvivors/{UpdateScreen.CurrentVersion}");
			using var form = new MultipartFormDataContent();
			foreach (KeyValuePair<string, string> field in fields)
				form.Add(new StringContent(field.Value ?? string.Empty), field.Key);

			using HttpResponseMessage response = await http.PostAsync(url, form);
			string body = await response.Content.ReadAsStringAsync();
			int code = (int)response.StatusCode;
			if (response.IsSuccessStatusCode)
			{
				GD.Print($"ReportBugScreen: report accepted by {url} ({code})");
				return null;
			}

			GD.Print($"ReportBugScreen: {url} said {code}: {body}");
			// Our own words for the rate limit. The server's are DrawFight's ("That is a lot of
			// fighters!"), and the limit is per address across both games.
			if (code == 429)
				return "Too many reports from here just now. Wait a while and try again.";
			try
			{
				using JsonDocument doc = JsonDocument.Parse(body);
				if (doc.RootElement.TryGetProperty("error", out JsonElement error)
					&& error.ValueKind == JsonValueKind.String
					&& !string.IsNullOrWhiteSpace(error.GetString()))
					return error.GetString();
			}
			catch (JsonException)
			{
				// Not JSON - a proxy's error page, say. The status code is all there is to report.
			}
			return $"Something went wrong ({code}). Please try again.";
		}
		catch (TaskCanceledException)
		{
			GD.Print($"ReportBugScreen: {url} did not answer within {TimeoutSeconds:0} s");
			return "The server took too long to answer. Your report is still here - try again in a moment.";
		}
		catch (Exception e)
		{
			GD.Print($"ReportBugScreen: sending to {url} failed ({e.GetType().Name}: {e.Message})");
			return "Could not send it. Check your internet and try again - your report is still here.";
		}
	}
}
