using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class StageSelection : Control
{
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

	private List<Godot.Collections.Dictionary> stages = new List<Godot.Collections.Dictionary>()
	{
		new Godot.Collections.Dictionary{{"name","Enchanted Forest"},{"unlocked",true}},
		new Godot.Collections.Dictionary{{"name","Cursed Castle"},{"unlocked",false}},
		new Godot.Collections.Dictionary{{"name","Mystic Ruins"},{"unlocked",false}},
	};

	private readonly List<string> testWizardSpellIds = new List<string>();
	private OptionButton testWizardSpellPicker;
	private bool isTestWizardSelected = false;
	private string defaultTestWizardSpellId = "magic_missile";

	public override void _Ready()
	{
		SetupTestWizardLoadoutPicker();

		for (int i = 0; i < stages.Count; i++)
		{
			var btn = GetNode<Button>($"StageList/StageButton{i+1}");
			if (btn != null)
			{
				btn.Text = stages[i]["name"].ToString() + ((bool)stages[i]["unlocked"] ? "" : " (Locked)");
				btn.Disabled = !(bool)stages[i]["unlocked"];
				int idx = i;
				btn.Pressed += () => OnStageButtonPressed(idx);
			}
		}
	}

	private void OnStageButtonPressed(int idx)
	{
		if ((bool)stages[idx]["unlocked"])
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
