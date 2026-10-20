using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WizardSurvivors.scripts;

public partial class MainMenu : Control
{
	private Label arcaneEnergyLabel = null!;
	private Button startRunButton = null!;
	private Button spellbookButton = null!;
	private Button achievementsButton = null!;
	private Button arcaneUpgradesButton = null!;
	private Button optionsButton = null!;
	private Button backFromArcaneButton = null!;
	private Button backFromSpellbookButton = null!;
	private Button backFromAchievementsButton = null!;
	private Button backFromOptionsButton = null!;
	private Control mainPanel = null!;

	// Pulled out of the container stack by LayoutAroundTitleArt and pinned to the artwork, so
	// ShowPanel has to raise and lower them by hand - they are no longer children of mainPanel.
	private Control rootMenuButtons;
	private Control rootArcaneLabel;
	private Label rootDetailLabel;
	private ColorRect scrim;
	private Control spellbookPanel = null!;
	private Control achievementsPanel = null!;
	private Control arcaneUpgradesPanel = null!;
	private Control optionsPanel = null!;
	private HSlider masterVolumeSlider = null!;
	private HSlider musicVolumeSlider = null!;
	private HSlider sfxVolumeSlider = null!;
	private CheckButton muteToggle = null!;
	private CheckButton onboardingTipsToggle = null!;
	private CheckButton playtestModeToggle = null!;
	private OptionButton balancePresetOption = null!;
	private Button openLatestPlaytestLogButton = null!;
	private Button openPlaytestLogFolderButton = null!;
	private Label playtestChecklistStatusLabel = null!;
	private bool isInitializingPlaytestChecklist;
	private static readonly (string Id, string Label)[] PlaytestChecklistItems =
	{
		("run_default", "Run one game on Default preset"),
		("run_casual", "Run one game on Casual preset"),
		("run_hardcore", "Run one game on Hardcore preset"),
		("tips_toggle", "Verify onboarding tips toggle behavior"),
		("reward_diff", "Verify reward multipliers differ by preset")
	};
	private readonly List<CheckButton> playtestChecklistBoxes = new();
	private readonly Dictionary<string, CheckButton> playtestChecklistById = new();
	private VBoxContainer spellbookList = null!;
	private VBoxContainer spellbookSummary = null!;
	private VBoxContainer achievementList = null!;
	private VBoxContainer achievementSummary = null!;
	private GridContainer upgradeList = null!;
	private static readonly Texture2D DefaultSpellIcon = GD.Load<Texture2D>("res://assets/bonelight/ui/spells/unknown.png");

	private readonly Dictionary<string, UpgradeDefinition> upgradeDefinitions = new();
	private readonly Dictionary<string, UpgradeRowRefs> upgradeRows = new();
	private readonly List<SpellbookEntry> spellbookEntries = new();

	private sealed class SpellbookEntry
	{
		public string Id = string.Empty;
		public string DisplayName = string.Empty;
		public string Description = string.Empty;
		public string Elements = string.Empty;
		public List<(Element Element, int Weight)> ElementList = new();
		public bool IsPassive;
		public Texture2D Icon;
	}

	private sealed class UpgradeDefinition
	{
		public string Id = string.Empty;
		public string DisplayName = string.Empty;
		public string EffectText = string.Empty;
		public int BaseCost;
		public int CostPerLevel;
		public int MaxLevel;
		public bool IsSpellUnlock;
		// Curation is the one upgrade whose ceiling is not a constant: it rises as the spellbook
		// grows, so a fresh save is offered none of it and a finished one can prune hard.
		public bool HasPoolScaledMaxLevel;
		public string SpellId = string.Empty;
	}

	private sealed class UpgradeRowRefs
	{
		public PanelContainer RowPanel = null!;
		public Label EffectLabel = null!;
		public Label CurrentBonusLabel = null!;
		public Label LevelLabel = null!;
		public Label CostLabel = null!;
		public Button BuyButton = null!;
	}

	public override void _Ready()
	{
		ContentValidator.ValidateAtStartup(this);
		arcaneEnergyLabel = GetNode<Label>("MarginContainer/VBoxContainer/TopBar/ArcaneEnergyLabel");
		mainPanel = GetNode<Control>("MarginContainer/VBoxContainer/Content/MainPanel");
		arcaneUpgradesPanel = GetNode<Control>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel");
		optionsPanel = GetNode<Control>("MarginContainer/VBoxContainer/Content/OptionsPanel");
		EnsureSpellbookUi();
		EnsureAchievementsUi();
		EnsureResetProgressUi();

		startRunButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/StartRunButton");
		spellbookButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/SecondaryRow/SpellbookButton");
		achievementsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/SecondaryRow/AchievementsButton");
		arcaneUpgradesButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/SecondaryRow/ArcaneUpgradesButton");
		optionsButton = GetNode<Button>("OptionsButton");
		spellbookList = spellbookPanel.FindChild("SpellbookList", true, false) as VBoxContainer;
		spellbookSummary = spellbookPanel.FindChild("SpellbookSummary", true, false) as VBoxContainer;
		achievementList = achievementsPanel.FindChild("AchievementList", true, false) as VBoxContainer;
		achievementSummary = achievementsPanel.FindChild("AchievementSummary", true, false) as VBoxContainer;
		upgradeList = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/UpgradeScroll/UpgradeList");

		// Bind the column count HERE rather than only when the panel is first shown. A hidden
		// container is never laid out, so a panel that has not been opened yet keeps the column
		// count its .tscn was authored with - four columns of 210, or 870 units inside a 592-unit
		// panel. Nobody sees it, because it is hidden; but its minimum size is real, it is what a
		// geometry audit reports, and it becomes visible the moment anything reparents it.
		// The Spellbook and Deeds lists pick their columns as they are built (CodexRows.ColumnsFor).
		UpdateUpgradeGridColumns();
		backFromArcaneButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/BackFromArcaneButton");
		backFromSpellbookButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/SpellbookPanel/SpellbookVBox/BackFromSpellbookButton");
		backFromAchievementsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/AchievementsPanel/AchievementsVBox/BackFromAchievementsButton");
		backFromOptionsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/BackFromOptionsButton");
		masterVolumeSlider = GetNodeOrNull<HSlider>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MasterRow/MasterVolumeSlider") ?? EnsureMasterVolumeSlider();
		musicVolumeSlider = GetNodeOrNull<HSlider>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MusicRow/MusicVolumeSlider") ?? EnsureMusicVolumeSlider();
		sfxVolumeSlider = GetNodeOrNull<HSlider>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/SfxRow/SfxVolumeSlider") ?? EnsureSfxVolumeSlider();
		muteToggle = GetNode<CheckButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MuteToggle");

		// After every GetNode above, never before: this moves two nodes, and the paths have to
		// resolve against the scene as it was authored.
		LayoutAroundTitleArt();
		onboardingTipsToggle = GetNodeOrNull<CheckButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/OnboardingTipsToggle") ?? EnsureOnboardingTipsToggle();
		playtestModeToggle = GetNodeOrNull<CheckButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/PlaytestModeToggle") ?? EnsurePlaytestModeToggle();
		balancePresetOption = GetNodeOrNull<OptionButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/BalancePresetRow/BalancePresetOption") ?? EnsureBalancePresetOption();
		EnsurePlaytestToolkitUi();
		EnsureOptionsScroll();
		ApplyMenuSkin();
		ApplyOptionsSolidBackground();

		// Spellbook first: BuildUpgradeDefinitions names its shop rows from these entries.
		BuildSpellbookEntries();
		BuildUpgradeDefinitions();
		BuildUpgradeRows();
		BuildSpellbookCards();
		BuildAchievementCards();

		startRunButton.Pressed += OnStartRunPressed;
		spellbookButton.Pressed += OnSpellbookPressed;
		achievementsButton.Pressed += OnAchievementsPressed;
		arcaneUpgradesButton.Pressed += OnArcaneUpgradesPressed;
		optionsButton.Pressed += OnOptionsPressed;
		backFromArcaneButton.Pressed += ShowMainPanel;
		backFromSpellbookButton.Pressed += ShowMainPanel;
		backFromAchievementsButton.Pressed += ShowMainPanel;
		backFromOptionsButton.Pressed += ShowMainPanel;
		masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
		musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
		sfxVolumeSlider.ValueChanged += OnSfxVolumeChanged;
		muteToggle.Toggled += OnMuteToggled;
		onboardingTipsToggle.Toggled += OnOnboardingTipsToggled;
		playtestModeToggle.Toggled += OnPlaytestModeToggled;
		balancePresetOption.ItemSelected += OnBalancePresetSelected;
		openLatestPlaytestLogButton.Pressed += OnOpenLatestPlaytestLogPressed;
		openPlaytestLogFolderButton.Pressed += OnOpenPlaytestLogFolderPressed;
		foreach (CheckButton checkbox in playtestChecklistBoxes)
			checkbox.Toggled += _ => UpdatePlaytestChecklistStatus();
		foreach (CheckButton checkbox in playtestChecklistBoxes)
			checkbox.Toggled += _ => PersistPlaytestChecklistState();

		RefreshArcaneEnergy();
		RefreshUpgradeControls();
		RefreshSpellbookCards();
		RefreshAchievementCards();
		InitializeOptionsState();
		ShowMainPanel();
		PlayMenuMusic();

		// This screen swaps whole panels in and out rather than changing scene, and the navigator
		// only ever considers controls that are visible in the tree - so one attach here covers the
		// main panel, the spellbook, the achievements list, the arcane upgrades and the options.
		// Start Run named explicitly: it is buried in a MarginContainer while the Options button is
		// a direct child of the root, so tree order alone would open the game with Options selected.
		// Clear of the phone's rounded corners and camera hole; nothing on desktop (SafeArea).
		SafeArea.Apply(this);
		MenuNavigator.Attach(this, startRunButton);
		GetViewport().SizeChanged += OnViewportSizeChanged;
	}

	public override void _ExitTree()
	{
		if (GetViewport() is Viewport viewport)
			viewport.SizeChanged -= OnViewportSizeChanged;
	}

	private void ApplyMenuSkin()
	{
		// No fullscreen backdrop here any more. It inserted ui-png-bg-1.png at child index 0, i.e.
		// *behind* the scene's own opaque Background - so it was never visible, and it was a second
		// unrelated image competing for the same job.

		StyleTopBar(GetNodeOrNull<PanelContainer>("MarginContainer/VBoxContainer/TopBar"));
		// The two panel headings that live in the scene rather than in code. Same reason as
		// above: the scene gives them a Display size but no face.
		foreach (string title in new[] {
			"MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/ArcaneUpgradesTitle",
			"MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/OptionsTitle" })
			ResponsiveLayout.SetFont(GetNodeOrNull<Label>(title), ResponsiveLayout.TextRole.Display);
		BonelightSkin.ApplyPanel(spellbookPanel, BonelightSkin.Register.Vellum);
		BonelightSkin.ApplyPanel(achievementsPanel);
		BonelightSkin.ApplyPanel(arcaneUpgradesPanel);
		BonelightSkin.ApplyPanel(optionsPanel);

		// The five menu buttons used to be identical 360x56 rows, which meant "Start Run" and
		// "Options" had exactly the same visual weight and the player had to read all six to find
		// the one they wanted. Size does most of the work now; these three tiers do the rest, so
		// the hierarchy still reads at a glance and in a screenshot.
		// No icons on the menu buttons. The fantasy-pack glyphs are a different art language from
		// the Bonelight background - a gold play triangle and two saturated blue blobs against a
		// cold cyan field - and at 22px they read as clutter rather than as symbols. Checked in a
		// screenshot; size and tier styling carry the hierarchy on their own.
		StyleMenuTier(startRunButton, MenuTier.Primary);

		foreach (Button secondary in new[] { arcaneUpgradesButton, spellbookButton, achievementsButton })
			StyleMenuTier(secondary, MenuTier.Secondary);

		// The corner button gets the gear. An icon in a corner is what makes it findable without a
		// label loud enough to compete with the stack above it.
		BonelightSkin.StyleButton(optionsButton, "res://assets/bonelight/ui/icons/gear.png", 48);
		ResponsiveLayout.SetFont(optionsButton, ResponsiveLayout.TextRole.Label);
		if (resetProgressButton != null)
			StyleMenuTier(resetProgressButton, MenuTier.Tertiary);

		BonelightSkin.StyleButton(backFromArcaneButton);
		BonelightSkin.StyleButton(backFromSpellbookButton);
		BonelightSkin.StyleButton(backFromAchievementsButton);
		BonelightSkin.StyleButton(backFromOptionsButton);

		BonelightSkin.StyleButton(openLatestPlaytestLogButton);
		BonelightSkin.StyleButton(openPlaytestLogFolderButton);
	}

	// A plain dark plate. This used to be ui-png-avatar-1.png stretched behind the label: a small
	// framed portrait texture doing duty as a wide banner, which rendered as a broken-looking
	// fragment either side of text that overflowed it.
	private static void StyleTopBar(PanelContainer topBar)
	{
		if (topBar == null)
			return;

		foreach (Node child in topBar.GetChildren())
		{
			if (child is TextureRect backdrop && backdrop.Name.ToString().Contains("Backdrop"))
				backdrop.QueueFree();
		}

		var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.06f, 0.10f, 0.72f) };
		style.BorderColor = new Color(0.30f, 0.48f, 0.68f, 0.45f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(10);
		topBar.AddThemeStyleboxOverride("panel", style);
	}

	private enum MenuTier { Primary, Secondary, Tertiary }

	/// <summary>
	/// Re-styles a menu button for its place in the hierarchy, on top of the shared skin.
	/// </summary>
	/// <remarks>
	/// Deliberately local rather than pushed into BonelightSkin: "which of these is the important
	/// one" is a question about this screen's layout, not a property of buttons in general, and
	/// every other screen in the game has a flat set where one tier is the right answer.
	/// </remarks>
	private static void StyleMenuTier(Button button, MenuTier tier)
	{
		if (button == null)
			return;

		// The three tiers survive the reskin; what carries them does not. They used to be three
		// fills, three border colours and two corner radii of hand-picked blue. They are now three
		// weights of the same carved-stone frame, so the hierarchy is expressed in the light the
		// whole game is lit by rather than in a palette this one screen invented.
		BonelightSkin.StyleButton(button, tier switch
		{
			MenuTier.Primary => BonelightSkin.Emphasis.Primary,
			MenuTier.Secondary => BonelightSkin.Emphasis.Normal,
			_ => BonelightSkin.Emphasis.Quiet,
		});
	}

	// Solid dark backer so the options text stays readable over the busy fantasy backdrop.
	private void ApplyOptionsSolidBackground()
	{
		if (optionsPanel == null || optionsPanel.GetNodeOrNull<Panel>("OptionsSolidBackground") != null)
			return;

		var bg = new Panel
		{
			Name = "OptionsSolidBackground",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bg.AnchorLeft = 0.5f;
		bg.AnchorRight = 0.5f;
		bg.AnchorTop = 0f;
		bg.AnchorBottom = 1f;
		bg.OffsetLeft = -300f;
		bg.OffsetRight = 300f;
		bg.OffsetTop = 8f;
		bg.OffsetBottom = -8f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.07f, 0.10f, 0.97f),
			BorderColor = new Color(0.45f, 0.38f, 0.22f, 0.9f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(12);
		style.SetContentMarginAll(12);
		bg.AddThemeStyleboxOverride("panel", style);

		optionsPanel.AddChild(bg);
		optionsPanel.MoveChild(bg, 0);
	}

	private void PlayMenuMusic()
	{
		// Through the catalog rather than by path, via the one helper the title also uses. There
		// were three places naming a track and only one of them was the catalog, which is how a
		// removal misses one. Idempotent: the title already started it, and this does not restart.
		GetNodeOrNull<MusicPlayer>("/root/MusicPlayer")?.PlayMenuMusic();
	}

	private void EnsureSpellbookUi()
	{
		var secondaryRow = GetNodeOrNull<HBoxContainer>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/SecondaryRow");
		if (secondaryRow != null && secondaryRow.GetNodeOrNull<Button>("SpellbookButton") == null)
		{
			var button = new Button
			{
				Name = "SpellbookButton",
				Text = "Spellbook",
				CustomMinimumSize = new Vector2(146, 84),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Label);
			secondaryRow.AddChild(button);
			secondaryRow.MoveChild(button, 1);
		}

		var content = GetNode<Control>("MarginContainer/VBoxContainer/Content");
		if (content.GetNodeOrNull<Control>("SpellbookPanel") != null)
		{
			spellbookPanel = content.GetNode<Control>("SpellbookPanel");
			return;
		}

		spellbookPanel = new Control { Name = "SpellbookPanel", Visible = false };
		spellbookPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.AddChild(spellbookPanel);

		var vbox = new VBoxContainer { Name = "SpellbookVBox" };
		vbox.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		vbox.AnchorBottom = 1.0f;
		vbox.OffsetLeft = 24;
		vbox.OffsetTop = 20;
		vbox.OffsetRight = -24;
		vbox.OffsetBottom = -20;
		vbox.AddThemeConstantOverride("separation", 12);
		spellbookPanel.AddChild(vbox);

		var title = new Label
		{
			Name = "SpellbookTitle",
			Text = "Spellbook",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		// The spellbook heading sits ON the parchment, so it is ink like everything else there.
		title.AddThemeColorOverride("font_color", PageInk);
		vbox.AddChild(title);

		// The summary is rebuilt with the list (BuildSpellbookCards); the host keeps its place.
		vbox.AddChild(new VBoxContainer { Name = "SpellbookSummary", MouseFilter = Control.MouseFilterEnum.Ignore });

		vbox.AddChild(CodexRows.BuildScroll("SpellbookScroll", CodexRows.BuildList("SpellbookList")));

		var backButton = new Button
		{
			Name = "BackFromSpellbookButton",
			Text = "Back",
			CustomMinimumSize = new Vector2(220, 50),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Title);
		vbox.AddChild(backButton);
	}

	// Two-press reset, no dialog: the first press arms it and relabels, the second wipes. A confirm
	// dialog would be better, but this is a development affordance and an accidental single click
	// must never be able to destroy a save.
	private Button resetProgressButton;
	private bool resetArmed;

	// Reset Progress lives INSIDE Options, not on the main menu.
	//
	// It used to sit in the bottom row of the menu stack next to Options, at the same size and in
	// nearly the same style - one press away from wiping a save, presented as a peer of the button
	// that opens the volume sliders. Two presses are still required to confirm, but the press that
	// arms it should not be one the player can reach by accident on the way to Start Run.
	//
	// It goes at the END of the options list, after the sliders and before Back, because the last
	// thing in a settings panel is where destructive actions are expected to be.
	private void EnsureResetProgressUi()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>(
			"MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null || optionsVBox.GetNodeOrNull<Button>("ResetProgressButton") != null)
			return;

		resetProgressButton = new Button
		{
			Name = "ResetProgressButton",
			Text = "Reset Progress",
			CustomMinimumSize = new Vector2(220, 50)
		};
		ResponsiveLayout.SetFont(resetProgressButton, ResponsiveLayout.TextRole.Label);
		resetProgressButton.AddThemeColorOverride("font_color", new Color(0.86f, 0.56f, 0.56f));
		resetProgressButton.Pressed += OnResetProgressPressed;
		optionsVBox.AddChild(resetProgressButton);
	}

	/// <summary>
	/// Puts everything between the title and Back into a vertical-only ScrollContainer.
	/// </summary>
	/// <remarks>
	/// Options was the one panel in the game with no scroll, and it has since grown a playtest
	/// toolkit and an eight-item checklist - so the bottom of the list simply ran off the panel,
	/// with Reset Progress and Back among the things that could be pushed out of reach.
	///
	/// Done in code, after every Ensure* helper has added its row, rather than in the scene: the
	/// helpers add to OptionsVBox by path, so a scroll sitting in the scene between them and it
	/// would have to be threaded through every one of them. Inserting it last costs one reparenting
	/// loop and leaves those paths alone - every path lookup into OptionsVBox happens earlier in
	/// _Ready than this, and none of them runs again.
	///
	/// The title and Back stay OUTSIDE the scroll, pinned. A Back button that scrolls away is a
	/// dead end.
	/// </remarks>
	private void EnsureOptionsScroll()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>(
			"MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null || optionsVBox.GetNodeOrNull<ScrollContainer>("OptionsScroll") != null)
			return;

		var scroll = new ScrollContainer
		{
			Name = "OptionsScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true
		};
		var inner = new VBoxContainer
		{
			Name = "OptionsScrollBox",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		inner.AddThemeConstantOverride("separation", 12);
		scroll.AddChild(inner);

		// Collect first, then reparent: moving children while iterating the live list skips half
		// of them.
		var movable = new List<Node>();
		foreach (Node child in optionsVBox.GetChildren())
		{
			if (child.Name == "OptionsTitle" || child.Name == "BackFromOptionsButton"
				|| child.Name == "OptionsSolidBackground")
				continue;
			movable.Add(child);
		}
		foreach (Node child in movable)
		{
			optionsVBox.RemoveChild(child);
			inner.AddChild(child);
		}

		optionsVBox.AddChild(scroll);
		Button back = optionsVBox.GetNodeOrNull<Button>("BackFromOptionsButton");
		optionsVBox.MoveChild(scroll, back != null ? back.GetIndex() : optionsVBox.GetChildCount() - 1);

		// Reset Progress goes last, HERE, rather than earlier against OptionsVBox. Ordering it
		// before this point does not survive: the toggles, the preset row and the toolkit are each
		// added by their own Ensure* helper which appends and then repositions itself relative to
		// Back, so anything placed "last" beforehand gets stepped over by the next helper to run.
		// This is the only moment the list is complete and nothing else will touch it.
		if (resetProgressButton != null && resetProgressButton.GetParent() == inner)
			inner.MoveChild(resetProgressButton, inner.GetChildCount() - 1);
	}

	private void OnResetProgressPressed()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null || resetProgressButton == null)
			return;

		if (!resetArmed)
		{
			resetArmed = true;
			resetProgressButton.Text = "Reset Progress — press again to confirm";
			return;
		}

		saveManager.ResetProgress();
		resetArmed = false;
		resetProgressButton.Text = "Reset Progress";

		// Everything on this screen is a view over the save, so all of it is now stale.
		RefreshArcaneEnergy();
		RefreshUpgradeControls();
		RefreshSpellbookCards();
		RefreshAchievementCards();
	}

	private void EnsureAchievementsUi()
	{
		var secondaryRow = GetNodeOrNull<HBoxContainer>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/SecondaryRow");
		if (secondaryRow != null && secondaryRow.GetNodeOrNull<Button>("AchievementsButton") == null)
		{
			var button = new Button
			{
				Name = "AchievementsButton",
				Text = "Deeds",
				CustomMinimumSize = new Vector2(146, 84),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Label);
			secondaryRow.AddChild(button);
		}

		var content = GetNode<Control>("MarginContainer/VBoxContainer/Content");
		if (content.GetNodeOrNull<Control>("AchievementsPanel") != null)
		{
			achievementsPanel = content.GetNode<Control>("AchievementsPanel");
			return;
		}

		achievementsPanel = new Control { Name = "AchievementsPanel", Visible = false };
		achievementsPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.AddChild(achievementsPanel);

		var vbox = new VBoxContainer { Name = "AchievementsVBox" };
		vbox.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		vbox.AnchorBottom = 1.0f;
		vbox.OffsetLeft = 24;
		vbox.OffsetTop = 20;
		vbox.OffsetRight = -24;
		vbox.OffsetBottom = -20;
		vbox.AddThemeConstantOverride("separation", 12);
		achievementsPanel.AddChild(vbox);

		var title = new Label
		{
			Name = "AchievementsTitle",
			// The button that opens it says Deeds, and so does the pause tab.
			Text = "Deeds",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		vbox.AddChild(title);

		vbox.AddChild(new VBoxContainer { Name = "AchievementSummary", MouseFilter = Control.MouseFilterEnum.Ignore });

		vbox.AddChild(CodexRows.BuildScroll("AchievementScroll", CodexRows.BuildList("AchievementList")));

		var backButton = new Button
		{
			Name = "BackFromAchievementsButton",
			Text = "Back",
			CustomMinimumSize = new Vector2(220, 50),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Title);
		vbox.AddChild(backButton);
	}

	private void RefreshArcaneEnergy()
	{
		int total = 0;
		float nextRunPreview = 1.0f;
		int defeatStreak = 0;
		int victoryStreak = 0;
		string presetName = "Default";
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null)
		{
			total = saveManager.Data.TotalCurrency;
			nextRunPreview = GlobalStatsManager.GetNextRunArcanePreviewMultiplier(saveManager.Data);
			defeatStreak = GlobalStatsManager.GetConsecutiveOutcomeCount(saveManager.Data, "Defeat");
			victoryStreak = GlobalStatsManager.GetConsecutiveOutcomeCount(saveManager.Data, "Victory");
			presetName = GlobalStatsManager.GetBalancePresetDisplayName(saveManager.Data.BalancePresetId);
			nextRunPreview *= GlobalStatsManager.GetArcaneRewardScaleForPreset(saveManager.Data.BalancePresetId);
		}

		string streakTag = defeatStreak > 0
			? $"Pity streak {defeatStreak}"
			: victoryStreak > 1 ? $"Momentum streak {victoryStreak}" : "Steady";

		// On a phone-width viewport this line cannot fit on one row - it used to run off both
		// edges - so it stacks instead, with the separators becoming line breaks.
		arcaneEnergyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		ResponsiveLayout.SetFont(arcaneEnergyLabel, ResponsiveLayout.TextRole.Label);
		arcaneEnergyLabel.Text = $"Arcane Energy: {total}";

		if (rootDetailLabel != null)
		{
			// Label, outlined: it sits straight on the title art, not on a panel, and at Micro in a
			// grey with no outline it vanished into the painted forest behind it.
			ResponsiveLayout.SetFont(rootDetailLabel, ResponsiveLayout.TextRole.Label);
			rootDetailLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.89f, 0.95f));
			rootDetailLabel.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.027f, 0.047f, 0.95f));
			rootDetailLabel.AddThemeConstantOverride("outline_size", 5);
			rootDetailLabel.Text = $"Preset: {presetName}" + System.Environment.NewLine
				+ $"Next run gain x{nextRunPreview:0.00} ({streakTag})";
		}
	}

	private void OnStartRunPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/CharacterSelection.tscn");
	}

	private void OnArcaneUpgradesPressed()
	{
		RefreshArcaneEnergy();
		UpdateUpgradeGridColumns();
		RefreshUpgradeControls();
		ShowPanel(arcaneUpgradesPanel);
	}

	private void OnSpellbookPressed()
	{
		RefreshSpellbookCards();
		ShowPanel(spellbookPanel);
	}

	private void OnAchievementsPressed()
	{
		RefreshAchievementCards();
		ShowPanel(achievementsPanel);
	}

	// The two lists choose one column or two as they are built, so a canvas that changes shape
	// while one is open (a resized PC window) rebuilds it rather than leaving it in the old count.
	private int codexColumnsBuilt;

	private void OnViewportSizeChanged()
	{
		if (CodexRows.ColumnsFor(this) == codexColumnsBuilt)
			return;
		if (spellbookPanel.Visible)
			RefreshSpellbookCards();
		if (achievementsPanel.Visible)
			RefreshAchievementCards();
		ApplyCodexPanelWidth();
	}

	// On a PC the two lists are a centred column rather than the full canvas: at an ultrawide's
	// 1700 units, two columns of rows would each run 800 wide and a description would be one line
	// a reader loses their place in. The phone keeps the full width.
	private const float CodexColumnMaxWidth = 1200f;

	private void ApplyCodexPanelWidth()
	{
		// 80: the menu's MarginContainer, 40 a side. Inset only what is over the cap, so a 960-wide
		// canvas (a 4:3 monitor) keeps every unit it has.
		float available = ResponsiveLayout.ViewportSize(this).X - 80f;
		// On a phone the panel instead reaches 24 units INTO that margin: width is the scarce axis
		// there, and the safe area (SafeArea.MinSide, 28) is applied outside the margin, so the
		// frame still clears the rounded corners with 16 to spare.
		float inset = ResponsiveLayout.IsNarrow(this) ? -24f : Mathf.Max(0f, (available - CodexColumnMaxWidth) / 2f);
		foreach (Control panel in new[] { spellbookPanel, achievementsPanel })
		{
			panel.OffsetLeft = inset;
			panel.OffsetRight = -inset;
		}
	}

	private void OnOptionsPressed()
	{
		AutoUpdatePlaytestChecklistFromTelemetry();
		UpdatePlaytestChecklistStatus();
		ShowPanel(optionsPanel);
	}

	// The title artwork is 720x1680 drawn at a whole x2 and centred on the canvas, and the canvas
	// is 720 wide with a height that varies by device (project stretch is keep_width). So the art's
	// top edge is NOT the top of the screen, and anchoring menu furniture to the screen edge makes
	// it drift off the composition on any phone that is not exactly 16:9.
	//
	// Everything here is therefore anchored to the CENTRE and offset in artwork rows: a row R of the
	// original 1280-tall composition sits at (R - 640) from centre, on every device, always. The file
	// is 1680 tall now, but that composition is still centred in it, so these rows did not move.
	//
	// The three bands used, read off the composition in .ai/title-screen.md:
	//   rows   34- 86  canopy above the wordmark   -> the Arcane Energy readout
	//   rows  340-550  dark wood under the wordmark and above the staff orb -> the menu buttons
	//   rows 1168-1232 foreground grass            -> the Options button
	// The figure, the ward and the orb are never covered. That is the whole point of the pass.
	private void LayoutAroundTitleArt()
	{
		// A PC gets the wide title art and its own positions (PinToWideArt); a phone keeps
		// exactly the portrait layout below. See .ai/title-screen.md, "The landscape set".
		bool landscape = !DisplayProfile.IsPortrait;

		// TitleScreen draws the art now, so the menu's own copy is dead weight - and it is the OLD
		// pre-Bonelight title image, which would fight the new one if both drew.
		var background = GetNodeOrNull<Control>("Background");
		if (background != null)
			background.Visible = false;

		// Was a permanent 28% wash over everything. It is a scrim now: clear on the root menu so the
		// artwork reads, and only drawn when a dense panel needs a legible ground to sit on.
		scrim = GetNodeOrNull<ColorRect>("ColorRect");
		if (scrim != null)
			scrim.Color = new Color(0.02f, 0.03f, 0.05f, 0.0f);

		var buttons = GetNodeOrNull<Control>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons");
		if (buttons != null)
		{
			buttons.Reparent(this);
			if (landscape)
				PinToWideArt(buttons, 60f, 134f, 290f, 240f);
			else
				PinToArtRows(buttons, 340f, 550f, halfWidth: 230f);
			rootMenuButtons = buttons;
		}

		if (arcaneEnergyLabel != null)
		{
			// The label leaves the TopBar panel behind; a framed bar across the top of the artwork
			// was the single biggest thing covering the canopy.
			arcaneEnergyLabel.Reparent(this);
			if (landscape)
			{
				// top right, in the canopy above the figure's shoulder
				PinToWideArt(arcaneEnergyLabel, 430f, 8f, 630f, 34f);
				arcaneEnergyLabel.HorizontalAlignment = HorizontalAlignment.Right;
			}
			else
			{
				PinToArtRows(arcaneEnergyLabel, 34f, 86f, halfWidth: 340f);
				arcaneEnergyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			}
			rootArcaneLabel = arcaneEnergyLabel;

			// The readout used to be one Title-sized label carrying three stacked lines, and on the
			// artwork its third line ran straight into the top of the wordmark. Only the currency
			// belongs up there; the preset and the next-run multiplier are reference numbers, so
			// they go to the bottom corner opposite Options, in the smallest role we have.
			rootDetailLabel = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			AddChild(rootDetailLabel);
			if (landscape)
			{
				PinToWideArt(rootDetailLabel, 470f, 312f, 632f, 352f);
			}
			else
			{
				PinToArtRows(rootDetailLabel, 1150f, 1240f, halfWidth: 168f);
				rootDetailLabel.OffsetLeft = 4f;
				rootDetailLabel.OffsetRight = 340f;
			}

			var topBar = GetNodeOrNull<Control>("MarginContainer/VBoxContainer/TopBar");
			if (topBar != null)
				topBar.Visible = false;
		}

		// Options keeps its corner, but pinned to the artwork rather than to the screen bottom.
		if (optionsButton != null)
		{
			optionsButton.AnchorLeft = 0.5f;
			optionsButton.AnchorRight = 0.5f;
			optionsButton.AnchorTop = 0.5f;
			optionsButton.AnchorBottom = 0.5f;
			if (landscape)
			{
				PinToWideArt(optionsButton, 12f, 292f, 126f, 324f);
			}
			else
			{
				optionsButton.OffsetLeft = -320f;
				optionsButton.OffsetRight = -92f;
				optionsButton.OffsetTop = 528f;
				optionsButton.OffsetBottom = 592f;
			}
		}
	}

	// The landscape title art (tools/art/splash_wide.py) is a 640x360 core drawn at x2 and
	// centred, and a PC canvas is always 720 tall, so its pixel (x, y) is always at
	// ((x - 320) * 2, (y - 180) * 2) from the centre of the screen. The boxes passed in here are
	// the MENU_BOX, ARCANE_BOX, OPTIONS_BOX and DETAIL_BOX rectangles in splash_wide.py, in core
	// pixels - the art keeps each of them dark, so the numbers must move together.
	private static void PinToWideArt(Control control, float x0, float y0, float x1, float y1)
	{
		const float CoreHalfWidth = 320f;
		const float CoreHalfHeight = 180f;

		control.AnchorLeft = 0.5f;
		control.AnchorRight = 0.5f;
		control.AnchorTop = 0.5f;
		control.AnchorBottom = 0.5f;
		control.OffsetLeft = (x0 - CoreHalfWidth) * 2f;
		control.OffsetRight = (x1 - CoreHalfWidth) * 2f;
		control.OffsetTop = (y0 - CoreHalfHeight) * 2f;
		control.OffsetBottom = (y1 - CoreHalfHeight) * 2f;
	}

	// Anchors a control to the canvas centre and positions it by artwork row, so it lands on the
	// same part of the picture whatever the viewport height is.
	private static void PinToArtRows(Control control, float topRow, float bottomRow, float halfWidth)
	{
		const float ArtHalfHeight = 640f;

		control.AnchorLeft = 0.5f;
		control.AnchorRight = 0.5f;
		control.AnchorTop = 0.5f;
		control.AnchorBottom = 0.5f;
		control.OffsetLeft = -halfWidth;
		control.OffsetRight = halfWidth;
		control.OffsetTop = topRow - ArtHalfHeight;
		control.OffsetBottom = bottomRow - ArtHalfHeight;
	}

	private void ShowMainPanel()
	{
		ShowPanel(mainPanel);
	}

	private const float OptionsColumnHalfWidth = 420f;

	private void ShowPanel(Control panelToShow)
	{
		mainPanel.Visible = panelToShow == mainPanel;

		// The Options button is pinned to the corner of the SCREEN rather than parented into the
		// menu stack, so nothing hides it when a panel opens - it was drawing on top of the panel
		// it had just opened, over that panel's own Back button. A corner control that belongs to
		// the main screen leaves with the main screen.
		if (optionsButton != null)
			optionsButton.Visible = panelToShow == mainPanel;

		// The buttons and the energy readout were lifted out of mainPanel by LayoutAroundTitleArt,
		// so hiding mainPanel no longer hides them. They follow it by hand.
		bool atRoot = panelToShow == mainPanel;
		if (rootMenuButtons != null)
			rootMenuButtons.Visible = atRoot;
		if (rootArcaneLabel != null)
			rootArcaneLabel.Visible = atRoot;
		if (rootDetailLabel != null)
			rootDetailLabel.Visible = atRoot;

		// Clear on the root menu so the artwork is the screen; drawn behind a panel because a wall
		// of upgrade rows over a forest at night is unreadable.
		if (scrim != null)
			scrim.Color = new Color(0.02f, 0.03f, 0.05f, atRoot ? 0.0f : 0.88f);
		// The stack above Content - an empty TopBar (its energy label was lifted out onto the art) and
		// a 120-tall Spacer - still took about 150 units off the top of every panel, so on a 720-tall
		// PC canvas each one was a short strip under a faded wordmark with most of its rows behind a
		// scrollbar. A panel gets the full height instead.
		if (GetNodeOrNull<Control>("MarginContainer/VBoxContainer/TopBar") is Control topBar)
			topBar.Visible = atRoot;
		if (GetNodeOrNull<Control>("MarginContainer/VBoxContainer/Spacer") is Control spacer)
			spacer.Visible = atRoot;

		// Options is a column of sliders and toggles; on a PC's landscape canvas a full-width panel
		// stretched every slider across 1180 units. A centred column instead. The phone keeps the width.
		if (!ResponsiveLayout.IsNarrow(this))
		{
			optionsPanel.AnchorLeft = 0.5f;
			optionsPanel.AnchorRight = 0.5f;
			optionsPanel.OffsetLeft = -OptionsColumnHalfWidth;
			optionsPanel.OffsetRight = OptionsColumnHalfWidth;
		}
		ApplyCodexPanelWidth();

		spellbookPanel.Visible = panelToShow == spellbookPanel;
		achievementsPanel.Visible = panelToShow == achievementsPanel;
		arcaneUpgradesPanel.Visible = panelToShow == arcaneUpgradesPanel;
		optionsPanel.Visible = panelToShow == optionsPanel;
	}

	private void InitializeOptionsState()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null)
		{
			onboardingTipsToggle.ButtonPressed = saveManager.Data.EnableGameplayOnboardingTips;
			playtestModeToggle.ButtonPressed = saveManager.Data.PlaytestModeEnabled;
			SetBalancePresetSelection(GlobalStatsManager.NormalizeBalancePresetId(saveManager.Data.BalancePresetId));
			ApplyPlaytestChecklistState(saveManager.Data.PlaytestChecklistState);
			AutoUpdatePlaytestChecklistFromTelemetry();
		}
		UpdatePlaytestChecklistStatus();

		int masterBus = AudioServer.GetBusIndex("Master");
		if (masterBus >= 0)
		{
			masterVolumeSlider.Value = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(masterBus));
			muteToggle.ButtonPressed = AudioServer.IsBusMute(masterBus);
		}

		int musicBus = AudioServer.GetBusIndex(MusicPlayer.ResolveMusicBusName());
		if (musicBus >= 0)
		{
			musicVolumeSlider.Value = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(musicBus));
		}

		sfxVolumeSlider.Value = AudioSettings.GetBusLinear(SfxPlayer.ResolveSfxBusName());
	}

	// All three of these go through AudioSettings rather than touching AudioServer directly, so
	// that a slider move is also written to the save file. They used to set the bus and stop,
	// which is why every launch came back at full volume.
	private void OnMasterVolumeChanged(double value)
	{
		AudioSettings.Store(this, AudioSettings.MasterBus, (float)value);
	}

	private void OnMusicVolumeChanged(double value)
	{
		AudioSettings.Store(this, MusicPlayer.ResolveMusicBusName(), (float)value);
	}

	private void OnSfxVolumeChanged(double value)
	{
		AudioSettings.Store(this, SfxPlayer.ResolveSfxBusName(), (float)value);
	}

	private void OnMuteToggled(bool pressed)
	{
		AudioSettings.StoreMute(this, pressed);
	}

	private void OnOnboardingTipsToggled(bool enabled)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		saveManager.Data.EnableGameplayOnboardingTips = enabled;
		saveManager.Data.HasToggledOnboardingTipsAtLeastOnce = true;
		saveManager.SaveGame();
		AutoUpdatePlaytestChecklistFromTelemetry();
		UpdatePlaytestChecklistStatus();
	}

	private void OnPlaytestModeToggled(bool enabled)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		saveManager.Data.PlaytestModeEnabled = enabled;
		saveManager.SaveGame();
	}

	private void OnBalancePresetSelected(long index)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		string preset = index switch
		{
			0 => GlobalStatsManager.BalancePresetCasual,
			2 => GlobalStatsManager.BalancePresetHardcore,
			_ => GlobalStatsManager.BalancePresetDefault
		};

		saveManager.Data.BalancePresetId = preset;
		saveManager.SaveGame();
		RefreshArcaneEnergy();
	}

	private HSlider EnsureMasterVolumeSlider()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null)
			return new HSlider();

		var row = optionsVBox.GetNodeOrNull<VBoxContainer>("MasterRow");
		if (row == null)
		{
			row = new VBoxContainer { Name = "MasterRow" };
			row.AddThemeConstantOverride("separation", 6);
			var label = new Label { Text = "Master Volume" };
			row.AddChild(label);
			var masterSlider = new HSlider
			{
				Name = "MasterVolumeSlider",
				MinValue = 0.01f,
				MaxValue = 1.0f,
				Step = 0.01f,
				Value = 1.0f,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			row.AddChild(masterSlider);
			optionsVBox.AddChild(row);
			optionsVBox.MoveChild(row, 1);
			return masterSlider;
		}

		var existingSlider = row.GetNodeOrNull<HSlider>("MasterVolumeSlider");
		if (existingSlider != null)
			return existingSlider;

		var createdMasterSlider = new HSlider
		{
			Name = "MasterVolumeSlider",
			MinValue = 0.01f,
			MaxValue = 1.0f,
			Step = 0.01f,
			Value = 1.0f,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddChild(createdMasterSlider);
		return createdMasterSlider;
	}

	private HSlider EnsureMusicVolumeSlider()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null)
			return new HSlider();

		var row = optionsVBox.GetNodeOrNull<VBoxContainer>("MusicRow");
		if (row == null)
		{
			row = new VBoxContainer { Name = "MusicRow" };
			row.AddThemeConstantOverride("separation", 6);
			var label = new Label { Text = "Music Volume" };
			row.AddChild(label);
			var musicSlider = new HSlider
			{
				Name = "MusicVolumeSlider",
				MinValue = 0.01f,
				MaxValue = 1.0f,
				Step = 0.01f,
				Value = 1.0f,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			row.AddChild(musicSlider);
			optionsVBox.AddChild(row);
			optionsVBox.MoveChild(row, 2);
			return musicSlider;
		}

		var existingSlider = row.GetNodeOrNull<HSlider>("MusicVolumeSlider");
		if (existingSlider != null)
			return existingSlider;

		var createdMusicSlider = new HSlider
		{
			Name = "MusicVolumeSlider",
			MinValue = 0.01f,
			MaxValue = 1.0f,
			Step = 0.01f,
			Value = 1.0f,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddChild(createdMusicSlider);
		return createdMusicSlider;
	}

	// Cloned from EnsureMusicVolumeSlider. The options panel is built in the .tscn for the rows
	// that have always existed and patched up in code for the ones added later; SFX is the third,
	// so it follows the same pattern rather than inventing a fourth.
	private HSlider EnsureSfxVolumeSlider()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null)
			return new HSlider();

		var row = optionsVBox.GetNodeOrNull<VBoxContainer>("SfxRow");
		if (row == null)
		{
			row = new VBoxContainer { Name = "SfxRow" };
			row.AddThemeConstantOverride("separation", 6);
			row.AddChild(new Label { Text = "Sound Effects Volume" });
			var slider = new HSlider
			{
				Name = "SfxVolumeSlider",
				MinValue = 0.01f,
				MaxValue = 1.0f,
				Step = 0.01f,
				Value = 1.0f,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			row.AddChild(slider);
			optionsVBox.AddChild(row);
			optionsVBox.MoveChild(row, 3);   // directly under MusicRow, which sits at 2
			return slider;
		}

		var existing = row.GetNodeOrNull<HSlider>("SfxVolumeSlider");
		if (existing != null)
			return existing;

		var created = new HSlider
		{
			Name = "SfxVolumeSlider",
			MinValue = 0.01f,
			MaxValue = 1.0f,
			Step = 0.01f,
			Value = 1.0f,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddChild(created);
		return created;
	}

	private CheckButton EnsureOnboardingTipsToggle()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null)
			return new CheckButton();

		var toggle = new CheckButton
		{
			Name = "OnboardingTipsToggle",
			Text = "Show Gameplay Tips During Early Runs",
			ButtonPressed = true
		};
		ResponsiveLayout.SetFont(toggle, ResponsiveLayout.TextRole.Label);
		optionsVBox.AddChild(toggle);
		optionsVBox.MoveChild(toggle, Math.Max(0, optionsVBox.GetChildCount() - 2));
		return toggle;
	}

	private OptionButton EnsureBalancePresetOption()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null)
			return new OptionButton();

		var row = new HBoxContainer
		{
			Name = "BalancePresetRow"
		};
		row.AddThemeConstantOverride("separation", 10);

		var label = new Label
		{
			Text = "Run Difficulty Curve"
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Label);
		row.AddChild(label);

		var option = new OptionButton
		{
			Name = "BalancePresetOption",
			CustomMinimumSize = new Vector2(180, 48)
		};
		option.AddItem("Casual", 0);
		option.AddItem("Default", 1);
		option.AddItem("Hardcore", 2);
		row.AddChild(option);

		optionsVBox.AddChild(row);
		optionsVBox.MoveChild(row, Math.Max(0, optionsVBox.GetChildCount() - 2));
		return option;
	}

	private CheckButton EnsurePlaytestModeToggle()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null)
			return new CheckButton();

		var toggle = new CheckButton
		{
			Name = "PlaytestModeToggle",
			Text = "Enable Playtest Mode (faster XP + 4 options per level-up)",
			ButtonPressed = false
		};
		ResponsiveLayout.SetFont(toggle, ResponsiveLayout.TextRole.Label);
		optionsVBox.AddChild(toggle);
		optionsVBox.MoveChild(toggle, Math.Max(0, optionsVBox.GetChildCount() - 2));
		return toggle;
	}

	private void SetBalancePresetSelection(string presetId)
	{
		if (balancePresetOption == null)
			return;

		int index = GlobalStatsManager.NormalizeBalancePresetId(presetId) switch
		{
			GlobalStatsManager.BalancePresetCasual => 0,
			GlobalStatsManager.BalancePresetHardcore => 2,
			_ => 1
		};
		balancePresetOption.Select(index);
	}

	private void EnsurePlaytestToolkitUi()
	{
		var optionsVBox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox");
		if (optionsVBox == null)
			return;

		var existingSection = optionsVBox.GetNodeOrNull<VBoxContainer>("PlaytestToolsSection");
		if (existingSection != null)
		{
			openLatestPlaytestLogButton = existingSection.GetNodeOrNull<Button>("OpenLatestPlaytestLogButton") ?? new Button();
			openPlaytestLogFolderButton = existingSection.GetNodeOrNull<Button>("OpenPlaytestLogFolderButton") ?? new Button();
			playtestChecklistStatusLabel = existingSection.GetNodeOrNull<Label>("PlaytestChecklistStatus") ?? new Label();
			playtestChecklistBoxes.Clear();
			playtestChecklistById.Clear();
			foreach (Node child in existingSection.GetChildren())
			{
				if (child is CheckButton box && box.Name.ToString().StartsWith("PlaytestCheck"))
				{
					playtestChecklistBoxes.Add(box);
					string itemId = box.GetMeta("checklist_id", string.Empty).AsString();
					if (!string.IsNullOrWhiteSpace(itemId))
						playtestChecklistById[itemId] = box;
				}
			}
			return;
		}

		var section = new VBoxContainer
		{
			Name = "PlaytestToolsSection"
		};
		section.AddThemeConstantOverride("separation", 6);

		var title = new Label
		{
			Text = "Playtest Toolkit"
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Body);
		section.AddChild(title);

		var buttonRow = new HBoxContainer();
		buttonRow.AddThemeConstantOverride("separation", 8);
		section.AddChild(buttonRow);

		openLatestPlaytestLogButton = new Button
		{
			Name = "OpenLatestPlaytestLogButton",
			Text = "Open Latest Run Log",
			CustomMinimumSize = new Vector2(220, 48)
		};
		buttonRow.AddChild(openLatestPlaytestLogButton);

		openPlaytestLogFolderButton = new Button
		{
			Name = "OpenPlaytestLogFolderButton",
			Text = "Open Log Folder",
			CustomMinimumSize = new Vector2(180, 48)
		};
		buttonRow.AddChild(openPlaytestLogFolderButton);

		var checklistTitle = new Label
		{
			Text = "First Session Checklist"
		};
		ResponsiveLayout.SetFont(checklistTitle, ResponsiveLayout.TextRole.Label);
		section.AddChild(checklistTitle);

		playtestChecklistBoxes.Clear();
		playtestChecklistById.Clear();
		for (int i = 0; i < PlaytestChecklistItems.Length; i++)
		{
			(string itemId, string itemLabel) = PlaytestChecklistItems[i];
			var checkbox = new CheckButton
			{
				Name = $"PlaytestCheck{i + 1}",
				Text = itemLabel
			};
			checkbox.SetMeta("checklist_id", itemId);
			playtestChecklistBoxes.Add(checkbox);
			playtestChecklistById[itemId] = checkbox;
			section.AddChild(checkbox);
		}

		playtestChecklistStatusLabel = new Label
		{
			Name = "PlaytestChecklistStatus",
			Text = "Checklist 0/0 complete"
		};
		playtestChecklistStatusLabel.Modulate = new Color(0.84f, 0.92f, 1.0f, 0.95f);
		section.AddChild(playtestChecklistStatusLabel);

		optionsVBox.AddChild(section);
		optionsVBox.MoveChild(section, Math.Max(0, optionsVBox.GetChildCount() - 2));
	}

	private void OnOpenLatestPlaytestLogPressed()
	{
		string logsDir = ProjectSettings.GlobalizePath("user://playtest_logs");
		if (!Directory.Exists(logsDir))
		{
			playtestChecklistStatusLabel.Text = "No playtest logs yet. Complete a run first.";
			return;
		}

		string latestLogPath = Directory.GetFiles(logsDir, "run_*.txt")
			.OrderByDescending(File.GetLastWriteTimeUtc)
			.FirstOrDefault();

		if (string.IsNullOrWhiteSpace(latestLogPath))
		{
			playtestChecklistStatusLabel.Text = "No run log files found yet.";
			return;
		}

		OS.ShellOpen($"file:///{latestLogPath.Replace('\\', '/')}");
		playtestChecklistStatusLabel.Text = $"Opened: {Path.GetFileName(latestLogPath)}";
	}

	private void OnOpenPlaytestLogFolderPressed()
	{
		string logsDir = ProjectSettings.GlobalizePath("user://playtest_logs");
		Directory.CreateDirectory(logsDir);
		OS.ShellOpen($"file:///{logsDir.Replace('\\', '/')}");
		playtestChecklistStatusLabel.Text = "Opened playtest log folder.";
	}

	private void UpdatePlaytestChecklistStatus()
	{
		if (playtestChecklistStatusLabel == null)
			return;

		int checkedCount = playtestChecklistBoxes.Count(box => box != null && box.ButtonPressed);
		int totalCount = playtestChecklistBoxes.Count;
		int runCount = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data?.RunTelemetryHistory?.Count ?? 0;
		playtestChecklistStatusLabel.Text = $"Checklist {checkedCount}/{totalCount} complete | Recorded runs: {runCount}";
	}

	private void ApplyPlaytestChecklistState(Dictionary<string, bool> state)
	{
		isInitializingPlaytestChecklist = true;
		foreach ((string itemId, string _) in PlaytestChecklistItems)
		{
			if (!playtestChecklistById.TryGetValue(itemId, out CheckButton checkbox) || checkbox == null)
				continue;

			checkbox.ButtonPressed = state != null && state.TryGetValue(itemId, out bool isChecked) && isChecked;
		}
		isInitializingPlaytestChecklist = false;
	}

	private void PersistPlaytestChecklistState()
	{
		if (isInitializingPlaytestChecklist)
			return;

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		foreach ((string itemId, string _) in PlaytestChecklistItems)
		{
			if (!playtestChecklistById.TryGetValue(itemId, out CheckButton checkbox) || checkbox == null)
				continue;

			saveManager.Data.PlaytestChecklistState[itemId] = checkbox.ButtonPressed;
		}

		saveManager.SaveGame();
	}

	private void AutoUpdatePlaytestChecklistFromTelemetry()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		List<RunTelemetryRecord> history = saveManager.Data.RunTelemetryHistory ?? new List<RunTelemetryRecord>();
		bool hasDefaultRun = HasPresetRun(history, GlobalStatsManager.BalancePresetDefault);
		bool hasCasualRun = HasPresetRun(history, GlobalStatsManager.BalancePresetCasual);
		bool hasHardcoreRun = HasPresetRun(history, GlobalStatsManager.BalancePresetHardcore);
		bool rewardOrderingVerified = HasPresetRewardOrderingEvidence(history);
		bool tipsToggleVerified = saveManager.Data.HasToggledOnboardingTipsAtLeastOnce;

		isInitializingPlaytestChecklist = true;
		bool changed = false;
		changed |= SetChecklistItemIfTrue("run_default", hasDefaultRun);
		changed |= SetChecklistItemIfTrue("run_casual", hasCasualRun);
		changed |= SetChecklistItemIfTrue("run_hardcore", hasHardcoreRun);
		changed |= SetChecklistItemIfTrue("tips_toggle", tipsToggleVerified);
		changed |= SetChecklistItemIfTrue("reward_diff", rewardOrderingVerified);
		isInitializingPlaytestChecklist = false;

		if (changed)
			PersistPlaytestChecklistState();
	}

	private bool SetChecklistItemIfTrue(string itemId, bool shouldBeChecked)
	{
		if (!shouldBeChecked)
			return false;

		if (!playtestChecklistById.TryGetValue(itemId, out CheckButton checkbox) || checkbox == null)
			return false;

		if (checkbox.ButtonPressed)
			return false;

		checkbox.ButtonPressed = true;
		return true;
	}

	private static bool HasPresetRun(IEnumerable<RunTelemetryRecord> history, string presetId)
	{
		if (history == null)
			return false;

		string normalizedTarget = GlobalStatsManager.NormalizeBalancePresetId(presetId);
		foreach (RunTelemetryRecord run in history)
		{
			if (run == null)
				continue;

			if (string.Equals(GlobalStatsManager.NormalizeBalancePresetId(run.BalancePresetId), normalizedTarget, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private static bool HasPresetRewardOrderingEvidence(IEnumerable<RunTelemetryRecord> history)
	{
		if (history == null)
			return false;

		float casualTotal = 0f;
		int casualCount = 0;
		float defaultTotal = 0f;
		int defaultCount = 0;
		float hardcoreTotal = 0f;
		int hardcoreCount = 0;

		foreach (RunTelemetryRecord run in history)
		{
			if (run == null)
				continue;

			string preset = GlobalStatsManager.NormalizeBalancePresetId(run.BalancePresetId);
			if (string.Equals(preset, GlobalStatsManager.BalancePresetCasual, StringComparison.OrdinalIgnoreCase))
			{
				casualTotal += run.ArcaneRewardMultiplier;
				casualCount++;
			}
			else if (string.Equals(preset, GlobalStatsManager.BalancePresetHardcore, StringComparison.OrdinalIgnoreCase))
			{
				hardcoreTotal += run.ArcaneRewardMultiplier;
				hardcoreCount++;
			}
			else
			{
				defaultTotal += run.ArcaneRewardMultiplier;
				defaultCount++;
			}
		}

		if (casualCount == 0 || defaultCount == 0 || hardcoreCount == 0)
			return false;

		float casualAvg = casualTotal / casualCount;
		float defaultAvg = defaultTotal / defaultCount;
		float hardcoreAvg = hardcoreTotal / hardcoreCount;
		return casualAvg < defaultAvg && defaultAvg < hardcoreAvg;
	}

	private void BuildUpgradeDefinitions()
	{
		upgradeDefinitions.Clear();
		RegisterUpgrade("damage", "Runes of Ruin", "+8% spell damage", 30, 20, 8);
		RegisterUpgrade("recovery", "Emberheart", "+0.4 HP/sec regen", 30, 20, 8);
		RegisterUpgrade("cooldowns", "Chronoweave", "-5% cooldown time", 35, 25, 8);
		RegisterUpgrade("area", "Aether Bloom", "+8% area/range", 30, 20, 7);
		RegisterUpgrade("attack_speed", "Tempest Rhythm", "+6% attack speed", 35, 25, 8);
		RegisterUpgrade("duration", "Eternal Wick", "+10% effect duration", 25, 20, 6);
		RegisterUpgrade("amount", "Twinstar Sigil", "+1 additional projectile", 60, 40, 4);
		RegisterUpgrade("movespeed", "Fleetfoot Glyph", "+5% movement speed", 20, 15, 8);
		RegisterUpgrade("magnet", "Void Lure", "+20 pickup range", 25, 20, 7);
		RegisterUpgrade("growth", "Sage Spiral", "+10% XP gain", 30, 20, 6);
		RegisterUpgrade("greed", "Avarice Seal", "+10% Arcane Energy gain", 35, 25, 8);
		RegisterUpgrade("extra_lives", "Phoenix Oath", "+1 revive per run", 120, 80, 3);
		RegisterUpgrade("rerolls", "Fate Fracture", "+1 reroll per level-up", 75, 50, 5);
		// The three run-scoped level-up charges. Priced above rerolls because a reroll refills at
		// every level-up and these do not - one purchase buys one use for the whole run.
		RegisterUpgrade("bans", "Proscription", "+1 spell ban per run", 90, 70, 3);
		RegisterUpgrade("banked_levels", "Hoarded Insight", "+1 saved level-up per run", 110, 80, 3);
		RegisterUpgrade("auguries", "Augury", "+1 spell summoned to a level-up per run", 130, 90, 3);
		RegisterUpgrade("vitality", "Vitality", "+5 max HP", 25, 20, 10);
		RegisterUpgrade("luck", "Fortune Thread", "+1 Luck", 45, 30, 20);
		RegisterUpgrade("crit_chance", "Keen Focus", "+3% crit chance", 55, 35, 20);
		RegisterUpgrade(GlobalStatsManager.CurationUpgradeId, "Redaction",
			"Set one more spell aside from the level-up pool", 80, 40, 1);
		upgradeDefinitions[GlobalStatsManager.CurationUpgradeId].HasPoolScaledMaxLevel = true;

		// Driven by UnlockCatalog rather than a hand-kept list. The old list named 22 spells that
		// were every one of them already unlocked by default, so the entire Arcane Codex read as
		// bought-out on a fresh save.
		foreach (UnlockDefinition definition in UnlockCatalog.PurchasableSpells)
			RegisterSpellUnlock(definition.Id, $"Unlock {SpellDisplayNameFor(definition.Id)}", definition.PurchaseCost);
	}

	// Shop rows are named from the spellbook so a spell has one display name, not two. Passives have
	// no .tres, which is why this reads the entry list rather than loading resources.
	private string SpellDisplayNameFor(string spellId)
	{
		SpellbookEntry entry = spellbookEntries.FirstOrDefault(
			e => e.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		return entry != null ? entry.DisplayName : spellId;
	}

	private void BuildSpellbookEntries()
	{
		spellbookEntries.Clear();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		AddSpellbookResource(seen, "magic_missile", "res://SpellData.tres");
		AddSpellbookResource(seen, "arcane_explosion", "res://SpellData_ArcaneExplosion.tres");
		AddSpellbookResource(seen, "spiritual_weapon", "res://SpellData_SpiritualWeapon.tres");
		AddSpellbookResource(seen, "fireball", "res://SpellData_Fireball.tres");
		AddSpellbookResource(seen, "cinderbreath", "res://SpellData_Cinderbreath.tres");
		AddSpellbookResource(seen, "mirefoot", "res://SpellData_Mirefoot.tres");
		AddSpellbookResource(seen, "kindled_ward", "res://SpellData_KindledWard.tres");
		AddSpellbookResource(seen, "gravewell", "res://SpellData_Gravewell.tres");
		AddSpellbookResource(seen, "hollow_star", "res://SpellData_HollowStar.tres");
		AddSpellbookResource(seen, "iron_palisade", "res://SpellData_IronPalisade.tres");
		AddSpellbookResource(seen, "contagion", "res://SpellData_Contagion.tres");
		AddSpellbookResource(seen, "bramble_seed", "res://SpellData_BrambleSeed.tres");
		AddSpellbookResource(seen, "frost_shard", "res://SpellData_FrostShard.tres");
		AddSpellbookResource(seen, "riptide", "res://SpellData_Riptide.tres");
		AddSpellbookResource(seen, "shadow_bolt", "res://SpellData_ShadowBolt.tres");
		AddSpellbookResource(seen, "thorn_vine", "res://SpellData_ThornVine.tres");
		AddSpellbookResource(seen, "gale_blade", "res://SpellData_GaleBlade.tres");
		AddSpellbookResource(seen, "solar_flare", "res://SpellData_SolarFlare.tres");
		AddSpellbookResource(seen, "molten_shard", "res://SpellData_MoltenShard.tres");
		AddSpellbookResource(seen, "chain_lightning", "res://SpellData_ChainLightning.tres");
		AddSpellbookResource(seen, "toxic_spore_burst", "res://SpellData_ToxicSporeBurst.tres");
		AddSpellbookResource(seen, "obsidian_spike", "res://SpellData_ObsidianSpike.tres");
		AddSpellbookResource(seen, "cyclone_slash", "res://SpellData_CycloneSlash.tres");
		AddSpellbookResource(seen, "void_lance", "res://SpellData_VoidLance.tres");
		AddSpellbookResource(seen, "glacial_spike", "res://SpellData_GlacialSpike.tres");
		AddSpellbookResource(seen, "black_tentacles", "res://SpellData_BlackTentacles.tres");
		AddSpellbookResource(seen, "cone_of_cold", "res://SpellData_ConeOfCold.tres");
		AddSpellbookResource(seen, "scorching_ray", "res://SpellData_ScorchingRay.tres");
		AddSpellbookResource(seen, "meteor_swarm", "res://SpellData_MeteorSwarm.tres");
		AddSpellbookResource(seen, "hunters_draw", "res://SpellData_HuntersDraw.tres");

		AddSpellbookPassive(seen, "aegis_ward", "Aegis Ward", "Periodically grants an absorbing shield.", "Metal, Light");
		AddSpellbookPassive(seen, "thornmail_barrier", "Thornmail Barrier", "Retaliates against nearby enemies when hit.", "Earth, Grass");
		AddSpellbookPassive(seen, "frozen_bulwark", "Frozen Bulwark", "Chance to freeze nearby attackers when hit.", "Ice x2");
		AddSpellbookPassive(seen, "stormguard_aura", "Stormguard Aura", "Strikes the nearest enemy with lightning when hit.", "Lightning, Metal");
		AddSpellbookPassive(seen, "venom_cloak", "Venom Cloak", "Periodically poisons nearby enemies.", "Poison, Darkness");
		AddSpellbookPassive(seen, "guardian_vines", "Guardian Vines", "Periodically roots nearby enemies.", "Grass x2");
		AddSpellbookPassive(seen, "tidal_barrier", "Tidal Barrier", "Periodically knocks back and slows nearby enemies.", "Water, Wind");
		AddSpellbookPassive(seen, "stone_bulwark", "Stone Bulwark", "Grants armor that reduces incoming damage.", "Earth, Metal");
		AddSpellbookPassive(seen, "blur", "Blur", "Chance to avoid incoming hits entirely.", "Arcane, Wind");
		AddSpellbookPassive(seen, "fortunes_favor", "Fortune's Favor", "Passively boosts Luck.", "Arcane, Light");
		AddSpellbookPassive(seen, "haste", "Haste", "Periodically grants attack-speed and move-speed surges.", "Wind, Lightning");
	}

	private void AddSpellbookResource(HashSet<string> seen, string id, string path)
	{
		if (!seen.Add(id))
			return;

		SpellData spell = ResourceLoader.Load<SpellData>(path);
		if (spell == null)
			return;

		spellbookEntries.Add(new SpellbookEntry
		{
			Id = id,
			DisplayName = spell.Name,
			Description = spell.Description,
			Elements = FormatElementWeights(spell.GetElementWeights()),
			ElementList = CodexRows.FromWeights(spell.GetElementWeights()),
			IsPassive = spell.IsPassive,
			Icon = spell.Icon ?? DefaultSpellIcon
		});
	}

	private void AddSpellbookPassive(HashSet<string> seen, string id, string displayName, string description, string elements)
	{
		if (!seen.Add(id))
			return;

		spellbookEntries.Add(new SpellbookEntry
		{
			Id = id,
			DisplayName = displayName,
			Description = description,
			Elements = elements,
			ElementList = CodexRows.ParseElements(elements),
			IsPassive = true,
			Icon = DefaultSpellIcon
		});
	}

	private static string FormatElementWeights(Dictionary<Element, int> weights)
	{
		if (weights == null || weights.Count == 0)
			return "None";

		string tags = string.Join(", ", weights.OrderBy(p => p.Key.ToString()).Select(p => p.Value > 1 ? $"{p.Key} x{p.Value}" : p.Key.ToString()));

		// A spell naming exactly one element is attuned to it and scales with that element's tier.
		// Worth saying on every card: it is the reason a single-element spell is not simply worse
		// than a hybrid that hands you twice the element weight.
		return weights.Count == 1 ? $"{tags} - Attuned" : tags;
	}

	// The Spellbook as a list rather than a wall of cards (.ai/level-up-screen.md, section 4b).
	//
	// It was a grid of 210-wide parchment cards, every line centred, a divider tile standing in the
	// grid as if it were a spell, and a full stone button on every page. Now: what you have found,
	// in two sections, then what you have not, quietly, at the end - the pages a player can act on
	// come first and carry the weight.
	private void BuildSpellbookCards()
	{
		ClearChildren(spellbookList);
		ClearChildren(spellbookSummary);
		codexColumnsBuilt = CodexRows.ColumnsFor(this);

		SaveData data = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		bool Found(SpellbookEntry entry) => GlobalStatsManager.IsSpellUnlockedForLevelUp(data, entry.Id);

		var attacks = spellbookEntries.Where(e => !e.IsPassive).ToList();
		var passives = spellbookEntries.Where(e => e.IsPassive).ToList();
		var foundAttacks = attacks.Where(Found).ToList();
		var foundPassives = passives.Where(Found).ToList();
		var missing = spellbookEntries.Where(e => !Found(e)).ToList();

		// The Set aside control only exists once there is something to spend on it. Before the
		// first Redaction it was a disabled button on every page - thirty controls that could not
		// be pressed, explaining one fact. The summary line says that fact once instead.
		int slots = GlobalStatsManager.GetCurationSlotsOwned(data);
		int setAside = GlobalStatsManager.GetRemovedSpellCount(data);
		bool curating = slots > 0 || setAside > 0;

		string found = $"{foundAttacks.Count} of {attacks.Count} attacks and {foundPassives.Count} of {passives.Count} passives found.";
		string curation = curating
			? $"{setAside} of {slots} set aside - a page set aside is never offered at level-up."
			: "Redactions from the Arcane Codex let you set pages aside.";
		spellbookSummary.AddChild(CodexRows.BuildSummary(found + "\n" + curation, CodexRows.Vellum));

		int columns = codexColumnsBuilt;
		if (foundAttacks.Count > 0)
			CodexRows.AddSection(spellbookList, "Attacks", $"{foundAttacks.Count} found", CodexRows.Vellum, columns,
				foundAttacks.Select(e => BuildSpellbookRow(e, data, curating)));
		if (foundPassives.Count > 0)
			CodexRows.AddSection(spellbookList, "Passives", $"{foundPassives.Count} found", CodexRows.Vellum, columns,
				foundPassives.Select(e => BuildSpellbookRow(e, data, curating)));
		if (missing.Count > 0)
			CodexRows.AddSection(spellbookList, "Still to find", $"{missing.Count} pages", CodexRows.Vellum, columns,
				CodexRows.GroupMissing(missing, e => e.IsPassive, e => UnlockCatalog.GetLockedHint(e.Id, UnlockKind.Spell))
					.Select(g => BuildMissingSpellbookRow(g.First, g.Count)));
	}

	private Control BuildSpellbookRow(SpellbookEntry entry, SaveData data, bool curating)
	{
		bool removed = GlobalStatsManager.IsSpellRemovedFromPool(data, entry.Id);
		Button control = curating ? BuildCurationToggle(entry, removed) : null;
		var row = new CodexRows.Entry
		{
			Title = entry.DisplayName,
			Body = entry.Description,
			Icon = entry.Icon ?? DefaultSpellIcon,
			Elements = entry.ElementList,
			// A spell naming exactly one element is attuned to it and scales with that element's tier
			// - the reason a single-element spell is not simply worse than a hybrid.
			ChipNote = entry.ElementList.Count == 1 ? "Attuned" : string.Empty,
			Quiet = removed,
			Tag = removed ? "Set aside" : string.Empty,
			Trailing = control,
			// One stop per row: the button when it can be pressed, otherwise the row itself, so a
			// controller can still scroll past a page whose button is greyed out.
			FocusStop = control == null || control.Disabled,
		};
		return CodexRows.BuildRow(row, CodexRows.Vellum);
	}

	// A page not found yet says how to find it. The name stays hidden - that is the discovery - but
	// the hint is the one useful thing on the row, so it is the row's body, not a footnote.
	private Control BuildMissingSpellbookRow(SpellbookEntry entry, int count)
	{
		return CodexRows.BuildRow(new CodexRows.Entry
		{
			Title = CodexRows.UnknownTitle(entry.IsPassive, count),
			Icon = entry.Icon ?? DefaultSpellIcon,
			IconSilhouette = true,
			Meta = UnlockCatalog.GetLockedHint(entry.Id, UnlockKind.Spell),
			MetaGlyph = StatIcons.Load("lock"),
			Quiet = true,
			FocusStop = true,
		}, CodexRows.Vellum);
	}

	// Curation lives on the page itself rather than on a separate management screen: the spellbook
	// already shows every spell and whether you own it, which is exactly the context in which
	// "stop offering me this one" is a sensible thing to decide.
	//
	// A small outlined control beside the name, never a stone button. It was a full-width bar of
	// the heaviest frame in the game under every card - the loudest thing on the page, for an action
	// most players take once.
	private Button BuildCurationToggle(SpellbookEntry entry, bool setAside)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		bool canSetAside = GlobalStatsManager.CanRemoveSpellFromPool(saveManager?.Data, entry.Id, out string reason);

		var button = new Button
		{
			Name = "CurationToggle_" + entry.Id,
			Text = setAside ? "Restore" : "Set aside",
			// Putting a page back is always allowed; taking one out needs a spare Redaction.
			Disabled = !setAside && !canSetAside,
			TooltipText = setAside
				? "This spell is not offered at level-up. Restore it to put it back in the pool."
				: canSetAside
					? "Stop offering this spell at level-up. Reversible at any time."
					: reason
		};
		ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Label);
		StyleQuietPageButton(button);
		// The visible pill is smaller than this; the rest is the touch target.
		ResponsiveLayout.EnsureTouchTarget(button, 0f);

		button.Pressed += () =>
		{
			var manager = GetNodeOrNull<SaveManager>("/root/SaveManager");
			if (manager == null)
				return;

			if (!GlobalStatsManager.SetSpellRemovedFromPool(manager.Data, entry.Id, !setAside))
				return;

			manager.SaveGame();
			// Every other card's availability may have changed with the last slot spent, and the
			// shop row's "Lv n/max" moves too. Deferred: this button is in the list being rebuilt,
			// and it is still inside its own press.
			bool refocus = button.HasFocus();
			Callable.From(() => RefreshAfterCuration(entry.Id, refocus)).CallDeferred();
		};

		return button;
	}

	// The rebuild frees the button a controller user just pressed. Without this the navigator would
	// put the selection back on the first page of the book and scroll there, a whole list away from
	// the page they were curating.
	private void RefreshAfterCuration(string spellId, bool refocus)
	{
		RefreshSpellbookCards();
		RefreshUpgradeControls();
		if (refocus && spellbookList.FindChild("CurationToggle_" + spellId, true, false) is Button again && !again.Disabled)
			again.GrabFocus();
	}

	// An outlined pill in the page's own inks: hairline edge, no fill until it is pointed at. The
	// font colours are COPIED into tools/art/ui_contrast.py (the pill pairs).
	private static void StyleQuietPageButton(Button button)
	{
		StyleBoxFlat Pill(Color fill, Color edge)
		{
			var box = new StyleBoxFlat { BgColor = fill, BorderColor = edge };
			box.SetBorderWidthAll(1);
			box.SetCornerRadiusAll(6);
			box.ContentMarginLeft = 14;
			box.ContentMarginRight = 14;
			box.ContentMarginTop = 4;
			box.ContentMarginBottom = 4;
			return box;
		}

		Color edge = CodexRows.Vellum.Edge;
		Color clear = new Color(PageCardFace, 0f);
		Color wash = new Color(0.847f, 0.643f, 0.518f, 0.35f); // flesh.lit, a light touch
		button.AddThemeStyleboxOverride("normal", Pill(clear, edge));
		button.AddThemeStyleboxOverride("hover", Pill(wash, PageInk));
		button.AddThemeStyleboxOverride("pressed", Pill(new Color(0.847f, 0.643f, 0.518f, 0.6f), PageInk));
		button.AddThemeStyleboxOverride("focus", Pill(wash, PageInk));
		button.AddThemeStyleboxOverride("disabled", Pill(clear, new Color(edge, 0.4f)));
		button.AddThemeColorOverride("font_color", PageInkFaint);
		button.AddThemeColorOverride("font_hover_color", PageInk);
		button.AddThemeColorOverride("font_pressed_color", PageInk);
		button.AddThemeColorOverride("font_focus_color", PageInk);
		button.AddThemeColorOverride("font_disabled_color", CodexRows.Vellum.InkMeta);
	}

	// The parchment ramp, from .ai/art-direction.md section 3. `flesh` is the contract's name for
	// it; on this screen it is vellum and ink. CodexRows.Vellum holds the full set the rows use.
	private static readonly Color PageInk = new Color(0.235f, 0.141f, 0.212f);        // flesh.deep
	private static readonly Color PageInkFaint = new Color(0.204f, 0.173f, 0.298f);   // violet.base
	private static readonly Color PageCardFace = new Color(0.949f, 0.863f, 0.769f);   // flesh.hi

	private static void ClearChildren(Node node)
	{
		if (node == null)
			return;
		foreach (Node child in node.GetChildren())
		{
			// Removed as well as freed, so the rebuilt list lays out this frame without the old rows
			// still holding their space until the end of it.
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void RefreshSpellbookCards() => BuildSpellbookCards();

	// Deeds: what is still to do, closest first, then what is done, quietly. It was a four-column
	// grid in definition order, with done and not-done deeds interleaved and told apart by colour.
	private void BuildAchievementCards()
	{
		ClearChildren(achievementList);
		ClearChildren(achievementSummary);
		codexColumnsBuilt = CodexRows.ColumnsFor(this);

		SaveData data = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		AchievementContext context = AchievementContext.ForSave(data);
		var open = new List<(AchievementDefinition, AchievementProgress)>();
		var done = new List<AchievementDefinition>();
		foreach (AchievementDefinition deed in AchievementDefinitions.All)
		{
			if (GlobalStatsManager.IsAchievementUnlocked(data, deed.Id))
				done.Add(deed);
			else
				open.Add((deed, deed.GetProgress(context)));
		}

		int total = AchievementDefinitions.All.Count;
		achievementSummary.AddChild(CodexRows.BuildSummary($"{done.Count} of {total} deeds done", CodexRows.Stone,
			total > 0 ? done.Count / (float)total : 0f));

		Texture2D SpellIcon(string id) => spellbookEntries.FirstOrDefault(
			e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Icon;

		int columns = codexColumnsBuilt;
		if (open.Count > 0)
			CodexRows.AddSection(achievementList, "In progress", $"{open.Count} to go", CodexRows.Stone, columns,
				CodexRows.OrderInProgress(open).Select(p => CodexRows.BuildDeedRow(p.Deed, false, p.Progress, SpellIcon, CodexRows.Stone)));
		if (done.Count > 0)
			CodexRows.AddSection(achievementList, "Done", $"{done.Count}", CodexRows.Stone, columns,
				done.Select(d => CodexRows.BuildDeedRow(d, true, null, SpellIcon, CodexRows.Stone)));
	}

	private void RefreshAchievementCards() => BuildAchievementCards();

	private void RegisterUpgrade(string id, string displayName, string effectText, int baseCost, int costPerLevel, int maxLevel)
	{
		upgradeDefinitions[id] = new UpgradeDefinition
		{
			Id = id,
			DisplayName = displayName,
			EffectText = effectText,
			BaseCost = baseCost,
			CostPerLevel = costPerLevel,
			MaxLevel = Math.Max(1, maxLevel)
		};
	}

	private void RegisterSpellUnlock(string spellId, string displayName, int cost)
	{
		upgradeDefinitions[$"spell:{spellId}"] = new UpgradeDefinition
		{
			Id = $"spell:{spellId}",
			DisplayName = displayName,
			EffectText = "Adds this spell to future level-up offers.",
			BaseCost = cost,
			CostPerLevel = 0,
			MaxLevel = 1,
			IsSpellUnlock = true,
			SpellId = spellId
		};
	}

	private void BuildUpgradeRows()
	{
		foreach (Node child in upgradeList.GetChildren())
		{
			child.QueueFree();
		}

		upgradeRows.Clear();
		foreach (UpgradeDefinition def in upgradeDefinitions.Values)
		{
			var rowPanel = new PanelContainer();
			rowPanel.CustomMinimumSize = new Vector2(220, 94);
			rowPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			var rowContainer = new VBoxContainer();
			rowContainer.AddThemeConstantOverride("separation", 2);
			rowPanel.AddChild(rowContainer);

			var titleLabel = new Label();
			titleLabel.Text = def.DisplayName;
			titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
			ResponsiveLayout.SetBodyText(titleLabel, ResponsiveLayout.TextRole.Label);
			rowContainer.AddChild(titleLabel);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			rowContainer.AddChild(row);

			// No CustomMinimumSize on either of these any more. They were a column guide at the
			// old font size, and a floor the text now clears on its own - all they did was add to
			// a minimum width the card could not go below.
			var levelLabel = new Label();
			levelLabel.HorizontalAlignment = HorizontalAlignment.Left;
			levelLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			ResponsiveLayout.SetFont(levelLabel, ResponsiveLayout.TextRole.Micro);
			row.AddChild(levelLabel);

			var costLabel = new Label();
			costLabel.HorizontalAlignment = HorizontalAlignment.Right;
			costLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			ResponsiveLayout.SetFont(costLabel, ResponsiveLayout.TextRole.Micro);
			row.AddChild(costLabel);

			var buyButton = new Button();
			buyButton.Text = "Buy";
			ResponsiveLayout.SetFont(buyButton, ResponsiveLayout.TextRole.Micro);
			ResponsiveLayout.EnsureTouchTarget(buyButton, 96f);
			string captureId = def.Id;
			buyButton.Pressed += () => TryPurchaseUpgrade(captureId);
			row.AddChild(buyButton);

			// The effect line is what is being bought, so it leads with the same stat glyph the
			// level-up cards use (StatIcons) and reads at Label rather than Micro.
			var effectRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			effectRow.AddThemeConstantOverride("separation", 6);
			Texture2D effectIcon = StatIcons.Load(StatIcons.KeyFor(def.EffectText));
			if (effectIcon != null)
			{
				effectRow.AddChild(new TextureRect
				{
					Texture = effectIcon,
					CustomMinimumSize = new Vector2(StatIcons.IconSize, StatIcons.IconSize),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
				});
			}
			var effectLabel = new Label();
			effectLabel.Text = def.EffectText;
			effectLabel.HorizontalAlignment = HorizontalAlignment.Left;
			effectLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			ResponsiveLayout.SetBodyText(effectLabel, ResponsiveLayout.TextRole.Label);
			effectRow.AddChild(effectLabel);
			rowContainer.AddChild(effectRow);

			var currentBonusLabel = new Label();
			currentBonusLabel.Modulate = new Color(0.85f, 0.92f, 1.0f, 0.95f);
			currentBonusLabel.HorizontalAlignment = HorizontalAlignment.Center;
			ResponsiveLayout.SetBodyText(currentBonusLabel, ResponsiveLayout.TextRole.Micro);
			rowContainer.AddChild(currentBonusLabel);

			upgradeRows[def.Id] = new UpgradeRowRefs
			{
				RowPanel = rowPanel,
				EffectLabel = effectLabel,
				CurrentBonusLabel = currentBonusLabel,
				LevelLabel = levelLabel,
				CostLabel = costLabel,
				BuyButton = buyButton
			};

			upgradeList.AddChild(rowPanel);
		}

		UpdateUpgradeGridColumns();
	}

	// The worst of the three: 3 columns x 220 plus 2 x 10 separation is 680 units of content, and
	// it was living in a panel anchored to 520. That is where the sideways scroll came from.
	private void UpdateUpgradeGridColumns()
	{
		// 40 menu margin + 8 panel inset, both sides.
		ResponsiveLayout.BindGridColumns(upgradeList, 220f, 4, sidePadding: 96f);
	}

	private void RefreshUpgradeControls()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		foreach ((string id, UpgradeDefinition def) in upgradeDefinitions)
		{
			if (!upgradeRows.TryGetValue(id, out UpgradeRowRefs row))
				continue;

			int level = GetShopItemLevel(saveManager, def);
			int maxLevel = GetShopItemMaxLevel(saveManager, def);
			bool isMax = level >= maxLevel;
			int cost = GetShopItemCost(def, level);
			row.LevelLabel.Text = $"Lv {level}/{maxLevel}";
			// A pool-scaled row at zero capacity is not "maxed", it is not yet available - saying
			// MAX there would read as "you have finished this" rather than "grow your book first".
			bool notYetAvailable = def.HasPoolScaledMaxLevel && maxLevel <= 0;
			row.CostLabel.Text = notYetAvailable
				? $"Needs {GlobalStatsManager.MinimumOfferablePool + 1} spells"
				: isMax ? "MAX" : $"Cost: {cost} AE";
			row.BuyButton.Text = def.IsSpellUnlock ? (isMax ? "Unlocked" : "Unlock") : (isMax ? "Maxed" : "Buy");
			row.BuyButton.Disabled = isMax || notYetAvailable || saveManager.Data.TotalCurrency < cost;
			row.CurrentBonusLabel.Text = def.IsSpellUnlock ? GetSpellUnlockSummary(level) : $"Current total bonus: {GetUpgradeEffectSummary(def.Id, level)}";
			string tooltip = BuildUpgradeTooltip(def, level);
			row.RowPanel.TooltipText = tooltip;
			row.EffectLabel.TooltipText = tooltip;
			row.CurrentBonusLabel.TooltipText = tooltip;
			row.BuyButton.TooltipText = tooltip;
		}
	}

	private static string BuildUpgradeTooltip(UpgradeDefinition def, int level)
	{
		if (def.IsSpellUnlock)
		{
			return level >= 1
				? $"{def.DisplayName}\nUnlocked for future level-up offers."
				: $"{def.DisplayName}\nCost: {def.BaseCost} Arcane Energy\nAdds this spell to future level-up offers.";
		}

		int clampedLevel = Math.Max(0, Math.Min(level, def.MaxLevel));
		bool isMax = clampedLevel >= def.MaxLevel;
		string current = GetUpgradeEffectSummary(def.Id, clampedLevel);
		if (isMax)
		{
			return $"{def.DisplayName}\n{current}\nReached maximum level.";
		}

		string next = GetUpgradeEffectSummary(def.Id, clampedLevel + 1);
		return $"{def.DisplayName}\nCurrent: {current}\nNext Lv {clampedLevel + 1}: {next}";
	}

	private static string GetUpgradeEffectSummary(string id, int level)
	{
		int safeLevel = Math.Max(0, level);
		switch (id)
		{
			case "damage":
				return $"Spell damage x{(1.0f + (safeLevel * 0.08f)):0.00}";
			case "recovery":
				return $"Regen {safeLevel * 0.4f:0.0} HP/sec";
			case "cooldowns":
				float cooldownMult = MathF.Max(0.35f, 1.0f - (safeLevel * 0.05f));
				return $"Cooldown multiplier x{cooldownMult:0.00}";
			case "area":
				return $"Area and range x{(1.0f + (safeLevel * 0.08f)):0.00}";
			case "attack_speed":
				return $"Attack speed x{(1.0f + (safeLevel * 0.06f)):0.00}";
			case "duration":
				return $"Effect duration x{(1.0f + (safeLevel * 0.10f)):0.00}";
			case "amount":
				return $"Additional projectiles +{safeLevel}";
			case "movespeed":
				return $"Move speed x{(1.0f + (safeLevel * 0.05f)):0.00}";
			case "magnet":
				return $"Pickup radius +{safeLevel * 20}";
			case "growth":
				return $"XP gain x{(1.0f + (safeLevel * 0.10f)):0.00}";
			case "greed":
				return $"Arcane gain x{(1.0f + (safeLevel * 0.10f)):0.00}";
			case "extra_lives":
				return $"Revives per run {safeLevel}";
			case "rerolls":
				return $"Rerolls per level-up {safeLevel}";
			case "bans":
				return safeLevel == 1 ? "1 spell ban per run" : $"{safeLevel} spell bans per run";
			case "banked_levels":
				return safeLevel == 1 ? "1 saved level-up per run" : $"{safeLevel} saved level-ups per run";
			case "auguries":
				return safeLevel == 1 ? "1 augury per run" : $"{safeLevel} auguries per run";
			case "vitality":
				return $"Max HP +{safeLevel * 5}";
			case "luck":
				return $"Luck level {safeLevel}";
			case "crit_chance":
				return $"Crit chance +{safeLevel * 3}%";
			case GlobalStatsManager.CurationUpgradeId:
				return safeLevel == 1 ? "1 spell may be set aside" : $"{safeLevel} spells may be set aside";
			default:
				return $"Level {safeLevel}";
		}
	}

	private static string GetSpellUnlockSummary(int level)
	{
		return level >= 1 ? "Unlocked for level-up offers" : "Locked";
	}

	private void TryPurchaseUpgrade(string upgradeId)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		if (!upgradeDefinitions.TryGetValue(upgradeId, out UpgradeDefinition def))
			return;

		int currentLevel = GetShopItemLevel(saveManager, def);
		if (currentLevel >= GetShopItemMaxLevel(saveManager, def))
			return;

		int cost = GetShopItemCost(def, currentLevel);
		if (saveManager.Data.TotalCurrency < cost)
			return;

		saveManager.Data.TotalCurrency -= cost;
		if (def.IsSpellUnlock)
		{
			GlobalStatsManager.UnlockSpell(saveManager.Data, def.SpellId);
		}
		else
		{
			saveManager.Data.ArcaneUpgradeLevels[upgradeId] = currentLevel + 1;
		}
		saveManager.SaveGame();

		RefreshArcaneEnergy();
		RefreshUpgradeControls();
		RefreshSpellbookCards();
	}

	private static int GetShopItemLevel(SaveManager saveManager, UpgradeDefinition def)
	{
		if (def.IsSpellUnlock)
			return GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager.Data, def.SpellId) ? 1 : 0;

		return GlobalStatsManager.GetUpgradeLevel(saveManager.Data, def.Id);
	}

	private static int GetShopItemCost(UpgradeDefinition def, int level)
	{
		return def.BaseCost + (level * def.CostPerLevel);
	}

	// Every other upgrade's ceiling is a constant on the definition. Curation's rises with the size
	// of the book, which is what keeps it from being a way to trivialise a small early pool.
	private static int GetShopItemMaxLevel(SaveManager saveManager, UpgradeDefinition def)
	{
		if (!def.HasPoolScaledMaxLevel)
			return def.MaxLevel;

		return GlobalStatsManager.GetCurationSlotCapacity(saveManager?.Data);
	}
}

