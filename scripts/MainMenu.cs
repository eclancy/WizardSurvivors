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
	private Control spellbookPanel = null!;
	private Control achievementsPanel = null!;
	private Control arcaneUpgradesPanel = null!;
	private Control optionsPanel = null!;
	private HSlider masterVolumeSlider = null!;
	private HSlider musicVolumeSlider = null!;
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
	private static readonly Texture2D DefaultSpellIcon = GD.Load<Texture2D>("res://assets/imported/fantasy/vfx/magic/arcane-bolt.png");

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

		startRunButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/StartRunButton");
		spellbookButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/SpellbookButton");
		achievementsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/AchievementsButton");
		arcaneUpgradesButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/ArcaneUpgradesButton");
		optionsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/OptionsButton");
		spellbookGrid = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/SpellbookPanel/SpellbookVBox/SpellbookScroll/SpellbookGrid");
		achievementList = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/AchievementsPanel/AchievementsVBox/AchievementScroll/AchievementList");
		upgradeList = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/UpgradeScroll/UpgradeList");
		backFromArcaneButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/BackFromArcaneButton");
		backFromSpellbookButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/SpellbookPanel/SpellbookVBox/BackFromSpellbookButton");
		backFromAchievementsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/AchievementsPanel/AchievementsVBox/BackFromAchievementsButton");
		backFromOptionsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/BackFromOptionsButton");
		masterVolumeSlider = GetNode<HSlider>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MasterRow/MasterVolumeSlider");
		musicVolumeSlider = GetNode<HSlider>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MusicRow/MusicVolumeSlider");
		muteToggle = GetNode<CheckButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MuteToggle");
		onboardingTipsToggle = GetNodeOrNull<CheckButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/OnboardingTipsToggle") ?? EnsureOnboardingTipsToggle();
		playtestModeToggle = GetNodeOrNull<CheckButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/PlaytestModeToggle") ?? EnsurePlaytestModeToggle();
		balancePresetOption = GetNodeOrNull<OptionButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/BalancePresetRow/BalancePresetOption") ?? EnsureBalancePresetOption();
		EnsurePlaytestToolkitUi();
		ApplyFantasyGuiSkin();
		ApplyOptionsSolidBackground();

		BuildUpgradeDefinitions();
		BuildSpellbookEntries();
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

	private void ApplyFantasyGuiSkin()
	{
		FantasyGuiSkin.ApplyFullscreenBackdrop(this, "res://assets/imported/fantasy_rpg_gui/BG/1.png", 0.94f);

		Control topBar = GetNodeOrNull<Control>("MarginContainer/VBoxContainer/TopBar");
		FantasyGuiSkin.ApplyPanelBackdrop(topBar, "res://assets/imported/fantasy_rpg_gui/Avatar/1.png", 0.28f);
		FantasyGuiSkin.ApplyPanelBackdrop(spellbookPanel, "res://assets/imported/fantasy_rpg_gui/Skills/1.png", 0.20f);
		FantasyGuiSkin.ApplyPanelBackdrop(achievementsPanel, "res://assets/imported/fantasy_rpg_gui/Quests/1.png", 0.20f);
		FantasyGuiSkin.ApplyPanelBackdrop(arcaneUpgradesPanel, "res://assets/imported/fantasy_rpg_gui/Inventory/1.png", 0.20f);
		FantasyGuiSkin.ApplyPanelBackdrop(optionsPanel, "res://assets/imported/fantasy_rpg_gui/Options/1.png", 0.20f);

		FantasyGuiSkin.StyleButton(startRunButton, FantasyGuiSkin.GlyphPlay);
		FantasyGuiSkin.StyleButton(spellbookButton, FantasyGuiSkin.GlyphSpellbook);
		FantasyGuiSkin.StyleButton(achievementsButton, FantasyGuiSkin.IconTrophy);
		FantasyGuiSkin.StyleButton(arcaneUpgradesButton, FantasyGuiSkin.GlyphGem);
		FantasyGuiSkin.StyleButton(optionsButton, FantasyGuiSkin.IconSettings);

		FantasyGuiSkin.StyleButton(backFromArcaneButton, FantasyGuiSkin.IconExit);
		FantasyGuiSkin.StyleButton(backFromSpellbookButton, FantasyGuiSkin.IconExit);
		FantasyGuiSkin.StyleButton(backFromAchievementsButton, FantasyGuiSkin.IconExit);
		FantasyGuiSkin.StyleButton(backFromOptionsButton, FantasyGuiSkin.IconExit);

		FantasyGuiSkin.StyleButton(openLatestPlaytestLogButton, FantasyGuiSkin.GlyphQuest);
		FantasyGuiSkin.StyleButton(openPlaytestLogFolderButton, FantasyGuiSkin.IconHome);
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
		var menuButtons = GetNode<VBoxContainer>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons");
		if (menuButtons.GetNodeOrNull<Button>("SpellbookButton") == null)
		{
			var button = new Button
			{
				Name = "SpellbookButton",
				Text = "Spellbook",
				CustomMinimumSize = new Vector2(360, 56)
			};
			button.AddThemeFontSizeOverride("font_size", 24);
			menuButtons.AddChild(button);
			menuButtons.MoveChild(button, 1);
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
		title.AddThemeFontSizeOverride("font_size", 30);
		vbox.AddChild(title);

		var scroll = new ScrollContainer
		{
			Name = "SpellbookScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
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
		backButton.AddThemeFontSizeOverride("font_size", 22);
		vbox.AddChild(backButton);
	}

	private void EnsureAchievementsUi()
	{
		var menuButtons = GetNode<VBoxContainer>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons");
		if (menuButtons.GetNodeOrNull<Button>("AchievementsButton") == null)
		{
			var button = new Button
			{
				Name = "AchievementsButton",
				Text = "Achievements",
				CustomMinimumSize = new Vector2(360, 56)
			};
			button.AddThemeFontSizeOverride("font_size", 24);
			menuButtons.AddChild(button);
			menuButtons.MoveChild(button, 2);
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
		title.AddThemeFontSizeOverride("font_size", 30);
		vbox.AddChild(title);

		var scroll = new ScrollContainer
		{
			Name = "AchievementScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
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
		backButton.AddThemeFontSizeOverride("font_size", 22);
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

		arcaneEnergyLabel.Text = $"Arcane Energy: {total}   |   Preset: {presetName}   |   Next run gain x{nextRunPreview:0.00} ({streakTag})";
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

	private void ShowMainPanel()
	{
		ShowPanel(mainPanel);
	}

	private void ShowPanel(Control panelToShow)
	{
		mainPanel.Visible = panelToShow == mainPanel;
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

		int musicBus = AudioServer.GetBusIndex("Music");
		if (musicBus >= 0)
		{
			musicVolumeSlider.Value = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(musicBus));
		}
	}

	private void OnMasterVolumeChanged(double value)
	{
		int masterBus = AudioServer.GetBusIndex("Master");
		if (masterBus >= 0)
		{
			AudioServer.SetBusVolumeDb(masterBus, Mathf.LinearToDb((float)value));
		}
	}

	private void OnMusicVolumeChanged(double value)
	{
		int musicBus = AudioServer.GetBusIndex("Music");
		if (musicBus >= 0)
		{
			AudioServer.SetBusVolumeDb(musicBus, Mathf.LinearToDb((float)value));
		}
	}

	private void OnMuteToggled(bool pressed)
	{
		int masterBus = AudioServer.GetBusIndex("Master");
		if (masterBus >= 0)
		{
			AudioServer.SetBusMute(masterBus, pressed);
		}
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
		toggle.AddThemeFontSizeOverride("font_size", 16);
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
		label.AddThemeFontSizeOverride("font_size", 16);
		row.AddChild(label);

		var option = new OptionButton
		{
			Name = "BalancePresetOption",
			CustomMinimumSize = new Vector2(180, 32)
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
		toggle.AddThemeFontSizeOverride("font_size", 16);
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
		title.AddThemeFontSizeOverride("font_size", 18);
		section.AddChild(title);

		var buttonRow = new HBoxContainer();
		buttonRow.AddThemeConstantOverride("separation", 8);
		section.AddChild(buttonRow);

		openLatestPlaytestLogButton = new Button
		{
			Name = "OpenLatestPlaytestLogButton",
			Text = "Open Latest Run Log",
			CustomMinimumSize = new Vector2(220, 32)
		};
		buttonRow.AddChild(openLatestPlaytestLogButton);

		openPlaytestLogFolderButton = new Button
		{
			Name = "OpenPlaytestLogFolderButton",
			Text = "Open Log Folder",
			CustomMinimumSize = new Vector2(180, 32)
		};
		buttonRow.AddChild(openPlaytestLogFolderButton);

		var checklistTitle = new Label
		{
			Text = "First Session Checklist"
		};
		checklistTitle.AddThemeFontSizeOverride("font_size", 15);
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
		RegisterUpgrade("vitality", "Vitality", "+5 max HP", 25, 20, 10);
		RegisterUpgrade("luck", "Fortune Thread", "+1 Luck", 45, 30, 20);
		RegisterUpgrade("crit_chance", "Keen Focus", "+3% crit chance", 55, 35, 20);

		RegisterSpellUnlock("fireball", "Unlock Fireball", 90);
		RegisterSpellUnlock("frost_shard", "Unlock Frost Shard", 90);
		RegisterSpellUnlock("shadow_bolt", "Unlock Shadow Bolt", 90);
		RegisterSpellUnlock("thorn_vine", "Unlock Thorn Vine", 90);
		RegisterSpellUnlock("gale_blade", "Unlock Gale Blade", 90);
		RegisterSpellUnlock("solar_flare", "Unlock Solar Flare", 110);
		RegisterSpellUnlock("molten_shard", "Unlock Molten Shard", 110);
		RegisterSpellUnlock("chain_lightning", "Unlock Chain Lightning", 120);
		RegisterSpellUnlock("toxic_spore_burst", "Unlock Toxic Spore Burst", 120);
		RegisterSpellUnlock("obsidian_spike", "Unlock Obsidian Spike", 120);
		RegisterSpellUnlock("cyclone_slash", "Unlock Cyclone Slash", 120);
		RegisterSpellUnlock("void_lance", "Unlock Void Lance", 130);
		RegisterSpellUnlock("glacial_spike", "Unlock Glacial Spike", 130);
		RegisterSpellUnlock("black_tentacles", "Unlock Black Tentacles", 150);
		RegisterSpellUnlock("cone_of_cold", "Unlock Cone of Cold", 150);
		RegisterSpellUnlock("scorching_ray", "Unlock Scorching Ray", 150);
		RegisterSpellUnlock("meteor_swarm", "Unlock Meteor Swarm", 180);
		RegisterSpellUnlock("haste", "Unlock Haste", 160);
		RegisterSpellUnlock("aegis_ward", "Unlock Aegis Ward", 120);
		RegisterSpellUnlock("thornmail_barrier", "Unlock Thornmail Barrier", 120);
		RegisterSpellUnlock("frozen_bulwark", "Unlock Frozen Bulwark", 120);
		RegisterSpellUnlock("stormguard_aura", "Unlock Stormguard Aura", 130);
		RegisterSpellUnlock("venom_cloak", "Unlock Venom Cloak", 130);
		RegisterSpellUnlock("guardian_vines", "Unlock Guardian Vines", 130);
		RegisterSpellUnlock("tidal_barrier", "Unlock Tidal Barrier", 130);
		RegisterSpellUnlock("stone_bulwark", "Unlock Stone Bulwark", 130);
		RegisterSpellUnlock("blur", "Unlock Blur", 150);
		RegisterSpellUnlock("fortunes_favor", "Unlock Fortune's Favor", 150);
	}

	private void BuildSpellbookEntries()
	{
		spellbookEntries.Clear();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		AddSpellbookResource(seen, "magic_missile", "res://SpellData.tres");
		AddSpellbookResource(seen, "arcane_explosion", "res://SpellData_ArcaneExplosion.tres");
		AddSpellbookResource(seen, "spiritual_weapon", "res://SpellData_SpiritualWeapon.tres");
		AddSpellbookResource(seen, "fireball", "res://SpellData_Fireball.tres");
		AddSpellbookResource(seen, "frost_shard", "res://SpellData_FrostShard.tres");
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

		return string.Join(", ", weights.OrderBy(p => p.Key.ToString()).Select(p => p.Value > 1 ? $"{p.Key} x{p.Value}" : p.Key.ToString()));
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

		var style = new StyleBoxFlat();
		style.BgColor = isPassive
			? new Color(0.10f, 0.20f, 0.16f, 0.94f)
			: new Color(0.20f, 0.13f, 0.10f, 0.94f);
		style.BorderColor = isPassive
			? new Color(0.45f, 0.90f, 0.72f, 0.9f)
			: new Color(0.95f, 0.63f, 0.45f, 0.9f);
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(4);
		panel.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		box.Alignment = BoxContainer.AlignmentMode.Center;
		panel.AddChild(box);

		box.AddChild(MakeSpellbookLabel(title, 20));
		box.AddChild(MakeSpellbookLabel($"{count} entries", 12));

		return panel;
	}

	private Control BuildSpellbookCard(SpellbookEntry entry, bool discovered)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(210, 230);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var style = new StyleBoxFlat();
		if (discovered)
		{
			style.BgColor = entry.IsPassive
				? new Color(0.12f, 0.20f, 0.17f, 0.95f)
				: new Color(0.20f, 0.13f, 0.11f, 0.95f);
			style.BorderColor = entry.IsPassive
				? new Color(0.45f, 0.90f, 0.72f, 0.75f)
				: new Color(0.95f, 0.63f, 0.45f, 0.75f);
			style.SetBorderWidthAll(1);
		}
		else
		{
			style.BgColor = new Color(0.06f, 0.06f, 0.07f, 0.95f);
		}
		style.SetCornerRadiusAll(4);
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

		box.AddChild(MakeSpellbookLabel(discovered ? entry.DisplayName : "???", 15));
		box.AddChild(MakeSpellbookLabel(discovered ? (entry.IsPassive ? "Passive" : "Active") : "???", 12));
		box.AddChild(MakeSpellbookLabel(discovered ? entry.Elements : "???", 12));
		box.AddChild(MakeSpellbookLabel(discovered ? entry.Description : "???", 11));

		return panel;
	}

	private static Label MakeSpellbookLabel(string text, int fontSize)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private void RefreshSpellbookCards() => BuildSpellbookCards();

	private void UpdateSpellbookGridColumns()
	{
		float width = GetViewport().GetVisibleRect().Size.X;
		spellbookGrid.Columns = width >= 1500f ? 5 : width >= 1100f ? 4 : 3;
	}

	private void BuildAchievementCards()
	{
		foreach (Node child in achievementList.GetChildren())
			child.QueueFree();

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			bool unlocked = saveManager?.Data.UnlockedAchievementIds.Any(id => id.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase)) ?? false;
			achievementList.AddChild(BuildAchievementCard(achievement, unlocked));
		}
	}

	private Control BuildAchievementCard(AchievementDefinition achievement, bool unlocked)
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

		box.AddChild(MakeSpellbookLabel(achievement.DisplayName, 16));
		box.AddChild(MakeSpellbookLabel(unlocked ? "Complete" : "In Progress", 12));
		box.AddChild(MakeSpellbookLabel(achievement.Description, 12));
		box.AddChild(MakeSpellbookLabel(achievement.RewardText, 11));

		return panel;
	}

	private void RefreshAchievementCards() => BuildAchievementCards();

	private void UpdateAchievementGridColumns()
	{
		float width = GetViewport().GetVisibleRect().Size.X;
		achievementList.Columns = width >= 1500f ? 4 : width >= 1100f ? 3 : 2;
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
			titleLabel.AddThemeFontSizeOverride("font_size", 15);
			rowContainer.AddChild(titleLabel);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			rowContainer.AddChild(row);

			var levelLabel = new Label();
			levelLabel.CustomMinimumSize = new Vector2(92, 0);
			levelLabel.HorizontalAlignment = HorizontalAlignment.Left;
			levelLabel.AddThemeFontSizeOverride("font_size", 13);
			row.AddChild(levelLabel);

			var costLabel = new Label();
			costLabel.CustomMinimumSize = new Vector2(74, 0);
			costLabel.HorizontalAlignment = HorizontalAlignment.Right;
			costLabel.AddThemeFontSizeOverride("font_size", 13);
			row.AddChild(costLabel);

			var buyButton = new Button();
			buyButton.CustomMinimumSize = new Vector2(74, 30);
			buyButton.Text = "Buy";
			buyButton.AddThemeFontSizeOverride("font_size", 12);
			string captureId = def.Id;
			buyButton.Pressed += () => TryPurchaseUpgrade(captureId);
			row.AddChild(buyButton);

			var effectLabel = new Label();
			effectLabel.Text = def.EffectText;
			effectLabel.HorizontalAlignment = HorizontalAlignment.Center;
			effectLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			effectLabel.AddThemeFontSizeOverride("font_size", 11);
			rowContainer.AddChild(effectLabel);

			var currentBonusLabel = new Label();
			currentBonusLabel.Modulate = new Color(0.85f, 0.92f, 1.0f, 0.95f);
			currentBonusLabel.HorizontalAlignment = HorizontalAlignment.Center;
			currentBonusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			currentBonusLabel.AddThemeFontSizeOverride("font_size", 11);
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

	private void UpdateUpgradeGridColumns()
	{
		float width = GetViewport().GetVisibleRect().Size.X;
		upgradeList.Columns = width >= 1500f ? 4 : 3;
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
			bool isMax = level >= def.MaxLevel;
			int cost = GetShopItemCost(def, level);
			row.LevelLabel.Text = $"{def.DisplayName} Lv {level}/{def.MaxLevel}";
			row.CostLabel.Text = isMax ? "MAX" : $"Cost: {cost} AE";
			row.BuyButton.Text = def.IsSpellUnlock ? (isMax ? "Unlocked" : "Unlock") : (isMax ? "Maxed" : "Buy");
			row.BuyButton.Disabled = isMax || saveManager.Data.TotalCurrency < cost;
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
			case "vitality":
				return $"Max HP +{safeLevel * 5}";
			case "luck":
				return $"Luck level {safeLevel}";
			case "crit_chance":
				return $"Crit chance +{safeLevel * 3}%";
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
		if (currentLevel >= def.MaxLevel)
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
}
