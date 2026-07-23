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

	private readonly List<StageDefinition> stages = new List<StageDefinition>()
	{
		new("Enchanted Forest", "Forest path", "res://assets/ground_tile.png", true),
		new("Cursed Castle", "Dungeon stone", "res://assets/imported/fantasy/source_mirror/Fantasy Dungeon tilesets/Fantasy_Dungeon_A1_darker.png", true),
		new("Mystic Ruins", "Rocky ruins", "res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png", true),
		new("Bramble Thicket", "Bush terrain", "res://assets/imported/fantasy/curated/map_props/bushes/bushes_000.png", true),
		new("Elderwood Grove", "Tree terrain", "res://assets/imported/fantasy/curated/map_props/trees/trees_000.png", true),
		new("Broken Highlands", "Ruin terrain", "res://assets/imported/fantasy/curated/map_props/ruins/ruins_000.png", true),
	};

	private readonly List<string> testWizardSpellIds = new List<string>();
	private OptionButton testWizardSpellPicker;
	private bool isTestWizardSelected = false;
	private string defaultTestWizardSpellId = "magic_missile";

	public override void _Ready()
	{
		SetupTestWizardLoadoutPicker();
		BuildStageList();
	}

	private void BuildStageList()
	{
		var stageList = GetNode<Container>("StageList");
		foreach (Node child in stageList.GetChildren())
		{
			child.QueueFree();
		}

		for (int i = 0; i < stages.Count; i++)
		{
			StageDefinition stage = stages[i];
			var btn = new Button
			{
				Name = $"StageButton{i + 1}",
				Text = $"{stage.Name}\n{stage.TerrainCategory}" + (stage.Unlocked ? string.Empty : "\nLocked"),
				Disabled = !stage.Unlocked,
				CustomMinimumSize = new Vector2(360, 176),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				Icon = ResourceLoader.Load<Texture2D>(stage.PreviewTexturePath),
				ExpandIcon = true,
				IconAlignment = HorizontalAlignment.Center,
				VerticalIconAlignment = VerticalAlignment.Top,
				TooltipText = stage.TerrainCategory
			};
			btn.AddThemeFontSizeOverride("font_size", 20);
			int idx = i;
			btn.Pressed += () => OnStageButtonPressed(idx);
			stageList.AddChild(btn);
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
			Text = "Test Wizard Start Weapon",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 18);
		panel.AddChild(title);

		testWizardSpellPicker = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
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
