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
	private GridContainer spellbookGrid = null!;
	private GridContainer achievementList = null!;
	private GridContainer upgradeList = null!;
	private static readonly Texture2D DefaultSpellIcon = GD.Load<Texture2D>("res://assets/organized/ui/ui-png-skills-icon-2.png");

	private readonly Dictionary<string, UpgradeDefinition> upgradeDefinitions = new();
	private readonly Dictionary<string, UpgradeRowRefs> upgradeRows = new();
	private readonly List<SpellbookEntry> spellbookEntries = new();

	private sealed class SpellbookEntry
	{
		public string Id = string.Empty;
		public string DisplayName = string.Empty;
		public string Description = string.Empty;
		public string Elements = string.Empty;
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
		spellbookGrid = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/SpellbookPanel/SpellbookVBox/SpellbookScroll/SpellbookGrid");
		achievementList = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/AchievementsPanel/AchievementsVBox/AchievementScroll/AchievementList");
		upgradeList = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/UpgradeScroll/UpgradeList");

		// Bind the column counts HERE rather than only when each panel is first shown. A hidden
		// container is never laid out, so a panel that has not been opened yet keeps the column
		// count its .tscn was authored with - four columns of 210, or 870 units inside a 592-unit
		// panel. Nobody sees it, because it is hidden; but its minimum size is real, it is what a
		// geometry audit reports, and it becomes visible the moment anything reparents it.
		UpdateSpellbookGridColumns();
		UpdateAchievementGridColumns();
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
		var musicPlayer = GetNodeOrNull<MusicPlayer>("/root/MusicPlayer");
		var music = ResourceLoader.Load<AudioStream>("res://assets/Pixel_Knights.mp3");
		if (musicPlayer != null && music != null)
			musicPlayer.PlayMusic(music);
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

		var scroll = new ScrollContainer
		{
			Name = "SpellbookScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true
		};
		vbox.AddChild(scroll);

		var grid = new GridContainer
		{
			Name = "SpellbookGrid",
			Columns = 4,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		scroll.AddChild(grid);

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
			Text = "Achievements",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		vbox.AddChild(title);

		var scroll = new ScrollContainer
		{
			Name = "AchievementScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true
		};
		vbox.AddChild(scroll);

		var grid = new GridContainer
		{
			Name = "AchievementList",
			Columns = 3,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		scroll.AddChild(grid);

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
			ResponsiveLayout.SetFont(rootDetailLabel, ResponsiveLayout.TextRole.Micro);
			rootDetailLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.84f));
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
		UpdateSpellbookGridColumns();
		RefreshSpellbookCards();
		ShowPanel(spellbookPanel);
	}

	private void OnAchievementsPressed()
	{
		UpdateAchievementGridColumns();
		RefreshAchievementCards();
		ShowPanel(achievementsPanel);
	}

	private void OnOptionsPressed()
	{
		AutoUpdatePlaytestChecklistFromTelemetry();
		UpdatePlaytestChecklistStatus();
		ShowPanel(optionsPanel);
	}

	// The title artwork is 720x1280 drawn at a whole x2 and centred on the canvas, and the canvas
	// is 720 wide with a height that varies by device (project stretch is keep_width). So the art's
	// top edge is NOT the top of the screen, and anchoring menu furniture to the screen edge makes
	// it drift off the composition on any phone that is not exactly 16:9.
	//
	// Everything here is therefore anchored to the CENTRE and offset in artwork rows: a row R of the
	// 1280-tall image sits at (R - 640) from centre, on every device, always.
	//
	// The three bands used, read off the composition in .ai/title-screen.md:
	//   rows   34- 86  canopy above the wordmark   -> the Arcane Energy readout
	//   rows  340-550  dark wood under the wordmark and above the staff orb -> the menu buttons
	//   rows 1168-1232 foreground grass            -> the Options button
	// The figure, the ward and the orb are never covered. That is the whole point of the pass.
	private void LayoutAroundTitleArt()
	{
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
			PinToArtRows(buttons, 340f, 550f, halfWidth: 230f);
			rootMenuButtons = buttons;
		}

		if (arcaneEnergyLabel != null)
		{
			// The label leaves the TopBar panel behind; a framed bar across the top of the artwork
			// was the single biggest thing covering the canopy.
			arcaneEnergyLabel.Reparent(this);
			PinToArtRows(arcaneEnergyLabel, 34f, 86f, halfWidth: 340f);
			arcaneEnergyLabel.HorizontalAlignment = HorizontalAlignment.Center;
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
			PinToArtRows(rootDetailLabel, 1150f, 1240f, halfWidth: 168f);
			rootDetailLabel.OffsetLeft = 4f;
			rootDetailLabel.OffsetRight = 340f;

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
			optionsButton.OffsetLeft = -320f;
			optionsButton.OffsetRight = -92f;
			optionsButton.OffsetTop = 528f;
			optionsButton.OffsetBottom = 592f;
		}
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
		ResponsiveLayout.SetFont(checklistTitle, ResponsiveLayout.TextRole.Micro);
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

	private void BuildSpellbookCards()
	{
		foreach (Node child in spellbookGrid.GetChildren())
			child.QueueFree();

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");

		var attacks = spellbookEntries.Where(entry => !entry.IsPassive).ToList();
		var passives = spellbookEntries.Where(entry => entry.IsPassive).ToList();

		if (attacks.Count > 0)
		{
			spellbookGrid.AddChild(BuildSpellbookSectionCard("Attacks", attacks.Count, false));
			foreach (SpellbookEntry entry in attacks)
			{
				bool discovered = GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, entry.Id);
				spellbookGrid.AddChild(BuildSpellbookCard(entry, discovered));
			}
		}

		if (passives.Count > 0)
		{
			spellbookGrid.AddChild(BuildSpellbookSectionCard("Passives", passives.Count, true));
			foreach (SpellbookEntry entry in passives)
			{
				bool discovered = GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, entry.Id);
				spellbookGrid.AddChild(BuildSpellbookCard(entry, discovered));
			}
		}
	}

	private Control BuildSpellbookSectionCard(string title, int count, bool isPassive)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(210, 110);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		// A section divider in the book: inked parchment, heavier edge than an entry card. The
		// green/orange pair it used to carry told Passive from Attack in colour; the heading text
		// already says which, and two saturated hues on parchment is a swatch book.
		var style = new StyleBoxFlat();
		style.BgColor = PageCardEdge;
		style.BorderColor = PageInk;
		style.SetBorderWidthAll(isPassive ? 3 : 2);
		style.SetCornerRadiusAll(2);
		panel.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		box.Alignment = BoxContainer.AlignmentMode.Center;
		panel.AddChild(box);

		box.AddChild(MakeSpellbookLabel(title, ResponsiveLayout.TextRole.Body));
		box.AddChild(MakeSpellbookLabel($"{count} entries", ResponsiveLayout.TextRole.Micro));

		return panel;
	}

	private Control BuildSpellbookCard(SpellbookEntry entry, bool discovered)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(210, 230);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// A card on the page is one step down the parchment ramp with an inked edge - not a dark
		// rectangle. Passive entries keep a distinguishing edge, but it is drawn in ink rather
		// than in a saturated green, because two bright hues on parchment is a swatch book.
		var style = new StyleBoxFlat();
		if (discovered)
		{
			style.BgColor = PageCardFace;
			style.BorderColor = entry.IsPassive ? PageInk : PageCardEdge;
			style.SetBorderWidthAll(entry.IsPassive ? 2 : 1);
		}
		else
		{
			// An entry you have not found reads as a gap in the book, so it stays dark.
			style.BgColor = new Color(0.06f, 0.06f, 0.07f, 0.95f);
			style.BorderColor = PageCardEdge;
			style.SetBorderWidthAll(1);
		}
		style.SetCornerRadiusAll(2);
		panel.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 5);
		panel.AddChild(box);

		var iconFrame = new CenterContainer { CustomMinimumSize = new Vector2(0, 82) };
		box.AddChild(iconFrame);

		var icon = new TextureRect
		{
			Texture = entry.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = new Vector2(64, 64),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Modulate = discovered ? Colors.White : new Color(0.02f, 0.02f, 0.025f, 0.95f)
		};
		iconFrame.AddChild(icon);

		box.AddChild(MakeSpellbookLabel(discovered ? entry.DisplayName : "???", ResponsiveLayout.TextRole.Micro));
		box.AddChild(MakeSpellbookLabel(discovered ? (entry.IsPassive ? "Passive" : "Active") : "Undiscovered", ResponsiveLayout.TextRole.Micro, discovered));
		box.AddChild(MakeSpellbookLabel(discovered ? entry.Elements : string.Empty, ResponsiveLayout.TextRole.Micro, discovered));
		// A locked page says how to find it. Four rows of "???" told the player nothing except
		// that something existed, which is the opposite of what a spellbook is for.
		box.AddChild(MakeSpellbookLabel(
			discovered ? entry.Description : UnlockCatalog.GetLockedHint(entry.Id, UnlockKind.Spell), ResponsiveLayout.TextRole.Micro));

		if (discovered)
			box.AddChild(BuildCurationToggle(entry, panel, style));

		return panel;
	}

	// Curation lives on the page itself rather than on a separate management screen: the spellbook
	// already shows every spell and whether you own it, which is exactly the context in which
	// "stop offering me this one" is a sensible thing to decide.
	private Button BuildCurationToggle(SpellbookEntry entry, PanelContainer panel, StyleBoxFlat style)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		bool setAside = GlobalStatsManager.IsSpellRemovedFromPool(saveManager?.Data, entry.Id);
		bool canSetAside = GlobalStatsManager.CanRemoveSpellFromPool(saveManager?.Data, entry.Id, out string reason);

		var button = new Button
		{
			Text = setAside ? "Restore to pool" : "Set aside",
			CustomMinimumSize = new Vector2(0, 30),
			// Putting a page back is always allowed; taking one out needs a spare Redaction.
			Disabled = !setAside && !canSetAside,
			TooltipText = setAside
				? "This spell is not offered at level-up. Restore it to put it back in the pool."
				: canSetAside
					? "Stop offering this spell at level-up. Reversible at any time."
					: reason
		};
		ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Micro);

		if (setAside)
		{
			// The whole card greys out, so a curated book is readable at a glance rather than only
			// by reading every button.
			button.AddThemeColorOverride("font_color", new Color(0.95f, 0.72f, 0.45f));
			style.BgColor = new Color(0.07f, 0.07f, 0.08f, 0.95f);
			style.BorderColor = new Color(0.55f, 0.42f, 0.28f, 0.75f);
			panel.Modulate = new Color(0.72f, 0.72f, 0.74f);
		}

		button.Pressed += () =>
		{
			var manager = GetNodeOrNull<SaveManager>("/root/SaveManager");
			if (manager == null)
				return;

			if (!GlobalStatsManager.SetSpellRemovedFromPool(manager.Data, entry.Id, !setAside))
				return;

			manager.SaveGame();
			// Every other card's availability may have changed with the last slot spent, and the
			// shop row's "Lv n/max" moves too.
			RefreshSpellbookCards();
			RefreshUpgradeControls();
		};

		return button;
	}

	// The parchment ramp, from .ai/art-direction.md section 3. `flesh` is the contract's name for
	// it; on this screen it is vellum and ink.
	private static readonly Color PageInk = new Color(0.235f, 0.141f, 0.212f);        // flesh.deep
	private static readonly Color PageInkFaint = new Color(0.431f, 0.267f, 0.314f);   // flesh.shade
	private static readonly Color PageCardFace = new Color(0.847f, 0.643f, 0.518f);   // flesh.lit
	private static readonly Color PageCardEdge = new Color(0.659f, 0.439f, 0.345f);   // flesh.base

	/// <param name="ink">
	/// True for a label sitting on parchment, false for one on a dark card. The spellbook is the
	/// only light surface in the game, and every label on it used to be white - which was correct
	/// everywhere else and nearly illegible here.
	/// </param>
	private static Label MakeSpellbookLabel(string text, ResponsiveLayout.TextRole role, bool ink = true)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(label, role);
		label.AddThemeColorOverride("font_color",
			ink ? (role == ResponsiveLayout.TextRole.Micro ? PageInkFaint : PageInk)
				: new Color(0.86f, 0.88f, 0.93f));
		return label;
	}

	private void RefreshSpellbookCards() => BuildSpellbookCards();

	// Card min width 210, and BindGridColumns re-asks whenever the viewport changes. The old
	// body tested for widths of 1100 and 1500, which keep_width makes unreachable - so it always
	// took the last branch and laid out three 210-wide cards in a 592-wide panel.
	private void UpdateSpellbookGridColumns()
	{
		// 40 menu margin + 24 panel inset, both sides.
		ResponsiveLayout.BindGridColumns(spellbookGrid, 210f, 5, sidePadding: 128f);
	}

	private void BuildAchievementCards()
	{
		foreach (Node child in achievementList.GetChildren())
			child.QueueFree();

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		AchievementContext context = AchievementContext.ForSave(saveManager?.Data);
		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			bool unlocked = GlobalStatsManager.IsAchievementUnlocked(saveManager?.Data, achievement.Id);
			achievementList.AddChild(BuildAchievementCard(achievement, unlocked, achievement.GetProgress(context)));
		}
	}

	private Control BuildAchievementCard(AchievementDefinition achievement, bool unlocked, AchievementProgress progress)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(260, 150);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var style = new StyleBoxFlat();
		style.BgColor = unlocked ? new Color(0.13f, 0.17f, 0.14f, 0.95f) : new Color(0.11f, 0.11f, 0.14f, 0.95f);
		style.BorderColor = unlocked ? new Color(0.45f, 0.95f, 0.55f, 0.85f) : new Color(0.34f, 0.34f, 0.40f, 0.7f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		panel.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 5);
		panel.AddChild(box);

		box.AddChild(MakeSpellbookLabel(achievement.DisplayName, ResponsiveLayout.TextRole.Label));
		// "In Progress" used to be a literal string, because nothing could measure how far along the
		// player was. Countable achievements now say so; yes/no ones still read as a state.
		string status = unlocked
			? "Complete"
			: string.IsNullOrEmpty(progress.Display) ? "Not yet" : progress.Display;
		var statusLabel = MakeSpellbookLabel(status, ResponsiveLayout.TextRole.Micro);
		statusLabel.AddThemeColorOverride("font_color", unlocked
			? new Color(0.55f, 0.95f, 0.62f)
			: new Color(0.86f, 0.82f, 0.62f));
		box.AddChild(statusLabel);

		if (!unlocked && !string.IsNullOrEmpty(progress.Display))
			box.AddChild(BuildProgressBar(progress.Fraction));

		box.AddChild(MakeSpellbookLabel(achievement.Description, ResponsiveLayout.TextRole.Micro));
		box.AddChild(MakeSpellbookLabel(achievement.RewardText, ResponsiveLayout.TextRole.Micro));

		return panel;
	}

	// A thin filled bar. Deliberately not a ProgressBar node: the theme would style it like the
	// health bar, and this is a menu readout rather than a gameplay gauge.
	private static Control BuildProgressBar(float fraction)
	{
		var track = new PanelContainer { CustomMinimumSize = new Vector2(0, 6) };
		var trackStyle = new StyleBoxFlat { BgColor = new Color(0.18f, 0.18f, 0.22f, 0.95f) };
		trackStyle.SetCornerRadiusAll(3);
		track.AddThemeStyleboxOverride("panel", trackStyle);

		var fill = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			// Anchoring by ratio rather than pixels keeps it correct as the grid reflows between
			// the 2/3/4 column layouts.
			SizeFlagsStretchRatio = Math.Max(0.001f, fraction),
		};
		var fillStyle = new StyleBoxFlat { BgColor = new Color(0.55f, 0.80f, 0.95f, 0.95f) };
		fillStyle.SetCornerRadiusAll(3);
		fill.AddThemeStyleboxOverride("panel", fillStyle);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 0);
		row.AddChild(fill);
		row.AddChild(new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = Math.Max(0.001f, 1f - fraction),
		});
		track.AddChild(row);
		return track;
	}

	private void RefreshAchievementCards() => BuildAchievementCards();

	private void UpdateAchievementGridColumns()
	{
		ResponsiveLayout.BindGridColumns(achievementList, 260f, 4, sidePadding: 128f);
	}

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

			var effectLabel = new Label();
			effectLabel.Text = def.EffectText;
			effectLabel.HorizontalAlignment = HorizontalAlignment.Center;
			ResponsiveLayout.SetBodyText(effectLabel, ResponsiveLayout.TextRole.Micro);
			rowContainer.AddChild(effectLabel);

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

