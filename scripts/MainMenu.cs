using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class MainMenu : Control
{
	private Label arcaneEnergyLabel = null!;
	private Button startRunButton = null!;
	private Button arcaneUpgradesButton = null!;
	private Button optionsButton = null!;
	private Button backFromArcaneButton = null!;
	private Button backFromOptionsButton = null!;
	private Control mainPanel = null!;
	private Control arcaneUpgradesPanel = null!;
	private Control optionsPanel = null!;
	private HSlider masterVolumeSlider = null!;
	private HSlider musicVolumeSlider = null!;
	private CheckButton muteToggle = null!;
	private GridContainer upgradeList = null!;

	private readonly Dictionary<string, UpgradeDefinition> upgradeDefinitions = new();
	private readonly Dictionary<string, UpgradeRowRefs> upgradeRows = new();

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
		arcaneEnergyLabel = GetNode<Label>("MarginContainer/VBoxContainer/TopBar/ArcaneEnergyLabel");
		mainPanel = GetNode<Control>("MarginContainer/VBoxContainer/Content/MainPanel");
		arcaneUpgradesPanel = GetNode<Control>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel");
		optionsPanel = GetNode<Control>("MarginContainer/VBoxContainer/Content/OptionsPanel");

		startRunButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/StartRunButton");
		arcaneUpgradesButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/ArcaneUpgradesButton");
		optionsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/MainPanel/MenuButtons/OptionsButton");
		upgradeList = GetNode<GridContainer>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/UpgradeScroll/UpgradeList");
		backFromArcaneButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox/BackFromArcaneButton");
		backFromOptionsButton = GetNode<Button>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/BackFromOptionsButton");
		masterVolumeSlider = GetNode<HSlider>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MasterRow/MasterVolumeSlider");
		musicVolumeSlider = GetNode<HSlider>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MusicRow/MusicVolumeSlider");
		muteToggle = GetNode<CheckButton>("MarginContainer/VBoxContainer/Content/OptionsPanel/OptionsVBox/MuteToggle");

		BuildUpgradeDefinitions();
		BuildUpgradeRows();

		startRunButton.Pressed += OnStartRunPressed;
		arcaneUpgradesButton.Pressed += OnArcaneUpgradesPressed;
		optionsButton.Pressed += OnOptionsPressed;
		backFromArcaneButton.Pressed += ShowMainPanel;
		backFromOptionsButton.Pressed += ShowMainPanel;
		masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
		musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
		muteToggle.Toggled += OnMuteToggled;

		RefreshArcaneEnergy();
		RefreshUpgradeControls();
		InitializeOptionsState();
		ShowMainPanel();
	}

	private void RefreshArcaneEnergy()
	{
		int total = 0;
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null)
		{
			total = saveManager.Data.TotalCurrency;
		}

		arcaneEnergyLabel.Text = $"Arcane Energy: {total}";
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

	private void OnOptionsPressed()
	{
		ShowPanel(optionsPanel);
	}

	private void ShowMainPanel()
	{
		ShowPanel(mainPanel);
	}

	private void ShowPanel(Control panelToShow)
	{
		mainPanel.Visible = panelToShow == mainPanel;
		arcaneUpgradesPanel.Visible = panelToShow == arcaneUpgradesPanel;
		optionsPanel.Visible = panelToShow == optionsPanel;
	}

	private void InitializeOptionsState()
	{
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
