using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using WizardSurvivors.scripts;
using HttpClient = System.Net.Http.HttpClient;

/// <summary>
/// The Android half of the update check: a small strip across the top of the title screen that
/// says a newer version is out and offers to open its APK in the browser. Nothing more.
/// </summary>
/// <remarks>
/// WHY NOT UpdateScreen. The Windows copy replaces its own files and restarts, and nothing like
/// that is possible here: an app cannot install an APK without REQUEST_INSTALL_PACKAGES and a
/// system prompt, and asking for that permission is exactly what makes a sideloaded game look
/// like malware to the person deciding whether to trust it. So the phone only ever TELLS the
/// player. The browser downloads the file, Android's own installer asks whether to install it, and
/// because the new APK carries the same release signature it installs over the old one, save and
/// all. See .ai/releasing.md, "Android".
///
/// WHY IT NEVER BLOCKS. UpdateScreen holds the title until its check is done because on Windows an
/// update changes what is about to run. Here the answer changes nothing on screen until the player
/// chooses to act on it, so the title starts at once and the strip slides in whenever the answer
/// arrives - or never, which is what every failure (no signal, GitHub down, a release with no APK,
/// a captive portal's HTML where the JSON should be) quietly comes to. An update is never worth
/// not being able to play.
///
/// TOUCH AND MOUSE ONLY, ON PURPOSE. Its buttons take no focus and it attaches no MenuNavigator.
/// On the title, A (or Enter) means "start": TitleScreen opens the menu on it. A navigator here
/// would select Download the moment the strip appeared, and the next A would then open a browser
/// as well as the menu - so a pad player trying to start the game would be thrown out of it. The
/// strip is a notice, not a screen, and on Android it is read with a thumb. A pad player loses
/// nothing: the same release is on the download page, and the strip comes back every launch.
///
/// It sits on its own CanvasLayer above the title and the menu (layer 6: above TouchControls' 5,
/// per CLAUDE.md, though no stick exists on the title) and below every screen that opens over the
/// menu, the bug report's 120 among them. While a menu panel such as the Spell Index fills the
/// screen it hides, because it would sit over that panel's heading; TitleScreen tells it when.
/// </remarks>
public partial class UpdateBanner : CanvasLayer
{
	private const int BannerLayer = 6;

	/// <summary>Forces the strip on outside an Android export, showing this version as the new
	/// one without touching the network: <c>--update-banner=9.9.9</c>. For photographing the strip
	/// with _UiShot on a desktop; see .ai/releasing.md.</summary>
	private const string TestArgPrefix = "--update-banner=";

	// The same five seconds as UpdateScreen. Shorter than that and a phone on a weak signal would
	// never hear back; longer is pointless, because nothing waits on it.
	private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

	/// <summary>What the check found: a version newer than this one, and where its APK is.</summary>
	private sealed record Offer(string Version, string ApkUrl);

	// Once per launch, like UpdateScreen. Every return "to the main menu" reloads TitleScreen, and
	// each new title asks the same finished task rather than fetching again - so the strip comes
	// back on every visit to the title until the player dismisses it, and then stays gone until the
	// next launch.
	private static Task<Offer> check;
	private static bool dismissed;

	private Offer offer;
	private PanelContainer panel;
	private bool hiddenByMenu;

	/// <summary>
	/// Adds the strip to <paramref name="title"/> if this copy should check, and starts the check
	/// the first time. Returns null when it should not (every Windows copy, the editor, every
	/// harness, `--no-update`), so the title screen is exactly what it always was.
	/// </summary>
	public static UpdateBanner AttachTo(Node title)
	{
		string testVersion = TestVersion();
		if (testVersion == null && !ShouldCheck())
			return null;
		if (dismissed)
			return null;

		check ??= testVersion != null
			? Task.FromResult(new Offer(testVersion, "https://ecec.dev/wizard-survivors/download"))
			: Task.Run(Fetch);

		var banner = new UpdateBanner { Name = "UpdateBanner" };
		title.AddChild(banner);
		return banner;
	}

	/// <summary>An exported Android build only. Desktop copies have UpdateScreen; the editor and
	/// `godot --path .` never check; `--no-update` turns it off on the phone too.</summary>
	private static bool ShouldCheck()
	{
		if (!OS.HasFeature("template") || OS.GetName() != "Android") return false;
		return Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-update") < 0
			&& Array.IndexOf(OS.GetCmdlineArgs(), "--no-update") < 0;
	}

	private static string TestVersion()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith(TestArgPrefix))
				return arg.Substring(TestArgPrefix.Length);
		}
		return null;
	}

	/// <summary>
	/// Runs on a worker thread. Null for "nothing to offer", whatever the reason - including a
	/// release that has no APK, which is every release made before the Android build existed.
	/// </summary>
	private static async Task<Offer> Fetch()
	{
		try
		{
			using var http = new HttpClient { Timeout = CheckTimeout };
			http.DefaultRequestHeaders.UserAgent.ParseAdd($"WizardSurvivors/{UpdateScreen.CurrentVersion}");

			string url = OS.GetEnvironment(UpdateScreen.UrlOverrideVariable);
			if (string.IsNullOrEmpty(url)) url = UpdateScreen.VersionUrl;
			using JsonDocument doc = JsonDocument.Parse(await http.GetStringAsync(url));
			JsonElement release = doc.RootElement;

			string latest = release.GetProperty("version").GetString() ?? "";
			if (!UpdateScreen.IsNewer(latest, UpdateScreen.CurrentVersion))
			{
				GD.Print($"UpdateBanner: {UpdateScreen.CurrentVersion} is up to date");
				return null;
			}
			if (!release.TryGetProperty("android", out JsonElement apk) || string.IsNullOrEmpty(apk.GetString()))
			{
				GD.Print($"UpdateBanner: {latest} is out, but has no Android build");
				return null;
			}

			GD.Print($"UpdateBanner: {latest} is available");
			return new Offer(latest, apk.GetString());
		}
		catch (Exception e)
		{
			GD.Print($"UpdateBanner: no update check ({e.GetType().Name}: {e.Message})");
			return null;
		}
	}

	public override void _Ready()
	{
		Layer = BannerLayer;
		Visible = false;
	}

	public override void _Process(double delta)
	{
		if (panel == null)
		{
			// Still waiting, or the answer was "nothing". Either way there is nothing to draw, and
			// once the answer is in, nothing more to do.
			if (check == null || !check.IsCompleted)
				return;
			offer = check.Result;
			if (offer == null)
			{
				SetProcess(false);
				return;
			}
			Build();
		}

		Visible = !dismissed && !hiddenByMenu;
	}

	/// <summary>TitleScreen hides the strip while a full-screen menu panel is open.</summary>
	public void SetHiddenByMenu(bool hidden)
	{
		hiddenByMenu = hidden;
	}

	/// <summary>
	/// Whether a press at this canvas position lands on the strip. TitleScreen opens the menu on
	/// ANY press, and a tap on Download must not also count as "press any button".
	/// </summary>
	public bool Covers(Vector2 position)
	{
		return Visible && panel != null && panel.GetGlobalRect().HasPoint(position);
	}

	private void Build()
	{
		// Full width under the top edge, pulled clear of the camera hole and the corner curve by
		// SafeArea below. Top-anchored and tall enough for one 48-unit touch target plus its frame.
		panel = new PanelContainer
		{
			Name = "Strip",
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetLeft = 12f,
			OffsetRight = -12f,
			OffsetTop = 12f,
			OffsetBottom = 12f + 76f,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		BonelightSkin.ApplyPanel(panel);
		AddChild(panel);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);
		panel.AddChild(row);

		var message = new Label
		{
			Text = $"Version {offer.Version} is available",
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddChild(message);
		// Wraps rather than pushing the buttons off a narrow screen if a version string is long.
		ResponsiveLayout.SetBodyText(message);
		message.AddThemeColorOverride("font_color", new Color(0.94f, 0.93f, 0.88f));

		var download = MakeButton("Download", BonelightSkin.Emphasis.Primary);
		download.Pressed += OnDownloadPressed;
		row.AddChild(download);

		var later = MakeButton("Later", BonelightSkin.Emphasis.Quiet);
		later.Pressed += OnLaterPressed;
		row.AddChild(later);

		SafeArea.Apply(this);
	}

	private Button MakeButton(string text, BonelightSkin.Emphasis emphasis)
	{
		var button = new Button
		{
			Text = text,
			// See the class remarks: never the selection, so A on the title only ever starts.
			FocusMode = Control.FocusModeEnum.None,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		BonelightSkin.StyleButton(button, emphasis);
		ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Body);
		ResponsiveLayout.EnsureTouchTarget(button, 96f);
		return button;
	}

	private void OnDownloadPressed()
	{
		// The browser fetches it and Android's installer takes it from there. The strip stays: a
		// player who backs out of the download still has the button to press again.
		SfxPlayer.Global(SfxCatalog.UiClick);
		OS.ShellOpen(offer.ApkUrl);
	}

	private void OnLaterPressed()
	{
		SfxPlayer.Global(SfxCatalog.UiClose);
		dismissed = true;
		Visible = false;
		SetProcess(false);
	}
}
