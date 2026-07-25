using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class StageSelection : Control
{
	private sealed record StageDefinition(string Name, string TerrainCategory, string PreviewTexturePath, bool Unlocked);

	private static readonly (string Id, string Path)[] TestWizardSpellOptions = new[]
	{
		("magic_missile", "res://SpellData.tres"),
		("arcane_explosion", "res://SpellData_ArcaneExplosion.tres"),
		("spiritual_weapon", "res://SpellData_SpiritualWeapon.tres"),
		("fireball", "res://SpellData_Fireball.tres"),
		("frost_shard", "res://SpellData_FrostShard.tres"),
		("shadow_bolt", "res://SpellData_ShadowBolt.tres"),
		("thorn_vine", "res://SpellData_ThornVine.tres"),
		("gale_blade", "res://SpellData_GaleBlade.tres"),
		("solar_flare", "res://SpellData_SolarFlare.tres"),
		("molten_shard", "res://SpellData_MoltenShard.tres"),
		("chain_lightning", "res://SpellData_ChainLightning.tres"),
		("toxic_spore_burst", "res://SpellData_ToxicSporeBurst.tres"),
		("obsidian_spike", "res://SpellData_ObsidianSpike.tres"),
		("cyclone_slash", "res://SpellData_CycloneSlash.tres"),
		("void_lance", "res://SpellData_VoidLance.tres"),
		("glacial_spike", "res://SpellData_GlacialSpike.tres"),
		("black_tentacles", "res://SpellData_BlackTentacles.tres"),
		("cone_of_cold", "res://SpellData_ConeOfCold.tres"),
		("scorching_ray", "res://SpellData_ScorchingRay.tres"),
		("meteor_swarm", "res://SpellData_MeteorSwarm.tres"),
	};

	// Passive/defensive spells have no .tres file - they are built in code and registered in the
	// Player spell catalog by id, so the Test Wizard picker lists them by id + display name to allow
	// testing passive-only starts.
	private static readonly (string Id, string Name)[] TestWizardPassiveOptions = new[]
	{
		("aegis_ward", "Aegis Ward"),
		("thornmail_barrier", "Thornmail Barrier"),
		("frozen_bulwark", "Frozen Bulwark"),
		("stormguard_aura", "Stormguard Aura"),
		("venom_cloak", "Venom Cloak"),
		("guardian_vines", "Guardian Vines"),
		("tidal_barrier", "Tidal Barrier"),
		("stone_bulwark", "Stone Bulwark"),
		("blur", "Blur"),
		("fortunes_favor", "Fortune's Favor"),
		("haste", "Haste"),
	};

	private readonly List<StageDefinition> stages = new List<StageDefinition>()
	{
		new("Enchanted Forest", "Forest path", "res://assets/ground_tile.png", true),
		new("Cursed Castle", "Dungeon stone", "res://assets/imported/fantasy/source_mirror/Fantasy Dungeon tilesets/Fantasy_Dungeon_A1_darker.png", true),
		new("Mystic Ruins", "Rocky ruins", "res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png", true),
		new("Bramble Thicket", "Dense forest growth", "res://assets/ground_tile.png", true),
		new("Elderwood Grove", "Ancient woodland", "res://assets/ground_tile.png", true),
		new("Broken Highlands", "Broken stone slopes", "res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png", true),
	};

	private readonly List<string> testWizardSpellIds = new List<string>();
	private OptionButton testWizardSpellPicker;
	private bool isTestWizardSelected = false;
	private string defaultTestWizardSpellId = "magic_missile";

	public override void _Ready()
	{
		ApplyFantasyGuiSkin();
		SetupTestWizardLoadoutPicker();
		BuildStageList();
	}

	private void ApplyFantasyGuiSkin()
	{
		FantasyGuiSkin.ApplyFullscreenBackdrop(this, "res://assets/imported/fantasy_rpg_gui/Map/1.png", 0.95f);
		FantasyGuiSkin.ApplyPanelBackdrop(GetNodeOrNull<Control>("StageScroll"), "res://assets/imported/fantasy_rpg_gui/Map/5.png", 0.18f);
	}

	private void BuildStageList()
	{
		var stageList = GetNode<Container>("StageScroll/StageGrid");
		foreach (Node child in stageList.GetChildren())
		{
			child.QueueFree();
		}

		for (int i = 0; i < stages.Count; i++)
		{
			StageDefinition stage = stages[i];
			var card = new PanelContainer
			{
				Name = $"StageCard{i + 1}",
				CustomMinimumSize = new Vector2(340, 194),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};

			var cardStyle = new StyleBoxFlat();
			cardStyle.BgColor = new Color(0.08f, 0.08f, 0.11f, 0.92f);
			cardStyle.BorderColor = new Color(0.38f, 0.40f, 0.48f, 0.95f);
			cardStyle.SetBorderWidthAll(1);
			cardStyle.SetCornerRadiusAll(6);
			cardStyle.SetContentMarginAll(8);
			card.AddThemeStyleboxOverride("panel", cardStyle);

			var stack = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			stack.AddThemeConstantOverride("separation", 6);
			card.AddChild(stack);

			var preview = new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(stage.PreviewTexturePath),
				CustomMinimumSize = new Vector2(0, 112),
				ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			stack.AddChild(preview);

			var titleLabel = new Label
			{
				Text = stage.Name,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			titleLabel.AddThemeFontSizeOverride("font_size", 22);
			stack.AddChild(titleLabel);

			var terrainLabel = new Label
			{
				Text = stage.TerrainCategory,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			terrainLabel.AddThemeFontSizeOverride("font_size", 14);
			terrainLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.80f, 0.88f));
			stack.AddChild(terrainLabel);

			if (!stage.Unlocked)
			{
				var lockedLabel = new Label
				{
					Text = "Locked",
					HorizontalAlignment = HorizontalAlignment.Center
				};
				lockedLabel.AddThemeFontSizeOverride("font_size", 16);
				lockedLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.55f, 0.55f));
				stack.AddChild(lockedLabel);
			}

			// The entire card is clickable; a transparent overlay button captures
			// input across the whole box and provides hover/press feedback.
			var cardButton = new Button
			{
				Name = $"StageButton{i + 1}",
				Flat = true,
				Disabled = !stage.Unlocked,
				MouseFilter = Control.MouseFilterEnum.Stop,
				MouseDefaultCursorShape = stage.Unlocked ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow
			};
			var transparent = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
			transparent.SetCornerRadiusAll(6);
			var hover = new StyleBoxFlat { BgColor = new Color(0.45f, 0.72f, 0.95f, 0.16f) };
			hover.SetCornerRadiusAll(6);
			var pressed = new StyleBoxFlat { BgColor = new Color(0.45f, 0.72f, 0.95f, 0.24f) };
			pressed.SetCornerRadiusAll(6);
			cardButton.AddThemeStyleboxOverride("normal", transparent);
			cardButton.AddThemeStyleboxOverride("hover", hover);
			cardButton.AddThemeStyleboxOverride("pressed", pressed);
			cardButton.AddThemeStyleboxOverride("focus", hover);
			cardButton.AddThemeStyleboxOverride("disabled", transparent);
			int idx = i;
			cardButton.Pressed += () => OnStageButtonPressed(idx);
			card.AddChild(cardButton);
			stageList.AddChild(card);
		}
	}

	private void OnStageButtonPressed(int idx)
	{
		if (stages[idx].Unlocked)
		{
			PersistTestWizardSpellChoice();
			Global.SelectedStageIdx = idx;
			var scenePath = "res://scenes/node_2d_game.tscn";
			if (ResourceLoader.Exists(scenePath))
				GetTree().ChangeSceneToFile(scenePath);
			else
				GD.PushError($"StageSelection: scene not found: {scenePath}");
		}
	}

	private void SetupTestWizardLoadoutPicker()
	{
		CharacterData selectedCharacter = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		isTestWizardSelected = selectedCharacter != null && selectedCharacter.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase);
		if (!isTestWizardSelected)
		{
			Global.TestWizardStartingSpellId = string.Empty;
			return;
		}

		defaultTestWizardSpellId = selectedCharacter.StartingSpellResource?.Id ?? "magic_missile";

		var panel = new VBoxContainer
		{
			Name = "TestWizardLoadout",
			LayoutMode = 1
		};
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0f;
		panel.OffsetLeft = -230f;
		panel.OffsetTop = 44f;
		panel.OffsetRight = 230f;
		panel.OffsetBottom = 132f;
		panel.AddThemeConstantOverride("separation", 6);

		var title = new Label
		{
			Text = "Test Wizard Start Spell",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 18);
		panel.AddChild(title);

		testWizardSpellPicker = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		Texture2D pickerIcon = FantasyGuiSkin.LoadTextureSafe(FantasyGuiSkin.GlyphSpellbook);
		if (pickerIcon != null)
		{
			testWizardSpellPicker.Icon = pickerIcon;
			testWizardSpellPicker.ExpandIcon = false;
			testWizardSpellPicker.AddThemeConstantOverride("icon_max_width", 24);
		}
		PopulateTestWizardSpellOptions();
		testWizardSpellPicker.ItemSelected += OnTestWizardSpellSelected;
		panel.AddChild(testWizardSpellPicker);

		AddChild(panel);
	}

	private void PopulateTestWizardSpellOptions()
	{
		if (testWizardSpellPicker == null)
			return;

		testWizardSpellIds.Clear();
		testWizardSpellPicker.Clear();

		foreach (var (id, path) in TestWizardSpellOptions)
		{
			var data = ResourceLoader.Load<SpellData>(path);
			if (data == null)
				continue;

			testWizardSpellPicker.AddItem(data.Name);
			testWizardSpellIds.Add(id);
		}

		// Passives (no .tres) are appended so the Test Wizard can start with a defensive spell.
		foreach (var (id, name) in TestWizardPassiveOptions)
		{
			testWizardSpellPicker.AddItem($"{name} (Passive)");
			testWizardSpellIds.Add(id);
		}

		if (testWizardSpellIds.Count == 0)
			return;

		string selectedId = string.IsNullOrWhiteSpace(Global.TestWizardStartingSpellId)
			? defaultTestWizardSpellId
			: Global.TestWizardStartingSpellId;

		int selectedIndex = testWizardSpellIds.FindIndex(id => id.Equals(selectedId, StringComparison.OrdinalIgnoreCase));
		if (selectedIndex < 0)
			selectedIndex = 0;

		testWizardSpellPicker.Select(selectedIndex);
		Global.TestWizardStartingSpellId = testWizardSpellIds[selectedIndex];
	}

	private void OnTestWizardSpellSelected(long index)
	{
		if (index < 0 || index >= testWizardSpellIds.Count)
			return;

		Global.TestWizardStartingSpellId = testWizardSpellIds[(int)index];
	}

	private void PersistTestWizardSpellChoice()
	{
		if (!isTestWizardSelected)
		{
			Global.TestWizardStartingSpellId = string.Empty;
			return;
		}

		if (testWizardSpellPicker == null || testWizardSpellIds.Count == 0)
		{
			Global.TestWizardStartingSpellId = defaultTestWizardSpellId;
			return;
		}

		int selectedIndex = testWizardSpellPicker.Selected;
		if (selectedIndex < 0 || selectedIndex >= testWizardSpellIds.Count)
			selectedIndex = 0;

		Global.TestWizardStartingSpellId = testWizardSpellIds[selectedIndex];
	}
}
