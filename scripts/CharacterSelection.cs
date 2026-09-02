using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Data-driven character selection (issue #1/#29): builds one scrollable card per CharacterRoster
// entry at runtime instead of a hardcoded stub list, and respects both CharacterData.IsUnlocked
// and SaveData.UnlockedCharacterIds. Each card shows the character's starting weapon, element and
// passive so the player can make an informed pick before a run.
public partial class CharacterSelection : Control
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

	private List<CharacterData> characters = new List<CharacterData>();
	private readonly List<string> testWizardSpellIds = new List<string>();
	private OptionButton testWizardSpellPicker = null!;
	private string defaultTestWizardSpellId = "magic_missile";

	public override void _Ready()
	{
		characters = CharacterRoster.GetAll();
		var grid = GetNode<GridContainer>("CardScroll/CardGrid");

		// The scene hardcodes three columns, which overflows both edges of a 720-wide portrait
		// viewport. Derive the count from the card width that actually fits, and keep it correct
		// if the window is resized or the device rotated.
		ResponsiveLayout.BindGridColumns(grid, CardMinWidth, maxColumns: 3);

		// Clear any placeholder cards left in the scene, then build the real roster. Free()
		// (not QueueFree()) so old children are gone immediately instead of coexisting with the
		// freshly-built cards for one frame.
		foreach (var child in grid.GetChildren())
			child.Free();

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");

		for (int i = 0; i < characters.Count; i++)
		{
			var character = characters[i];
			// Safety net: the baseline starter remains selectable even if its resource cache is stale,
			// while still allowing Test Wizard to appear first in the list for development flows.
			bool unlocked = character.Id.Equals("apprentice_wizard", System.StringComparison.OrdinalIgnoreCase)
				|| character.IsUnlocked
				|| (saveManager != null && saveManager.Data.UnlockedCharacterIds.Contains(character.Id));

			grid.AddChild(BuildCard(character, i, unlocked));
		}

		var backButton = GetNodeOrNull<Button>("BackButton");
		if (backButton != null)
			backButton.Pressed += OnBackButtonPressed;

		ApplyFantasyGuiSkin();
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

	private void ApplyFantasyGuiSkin()
	{
		FantasyGuiSkin.ApplyFullscreenBackdrop(this, "res://assets/organized/ui/ui-png-registration-1.png", 0.96f);
		FantasyGuiSkin.ApplyPanelBackdrop(GetNodeOrNull<Control>("CardScroll"), "res://assets/organized/ui/ui-png-character-1.png", 0.18f);
		FantasyGuiSkin.ApplyButtonsInTree(this, 5);
		FantasyGuiSkin.StyleButton(GetNodeOrNull<Button>("BackButton"), FantasyGuiSkin.IconExit);
	}

	// Card width the grid sizes its column count against; see ResponsiveLayout.BindGridColumns.
	private const float CardMinWidth = 280f;

	private const string SharedWizardFrame1Path = "res://assets/organized/characters/char-2d-pixel-dungeon-asset-pack-character-animation-priest1-v1-1.png";
	private static readonly Texture2D DefaultPortrait = GD.Load<Texture2D>(SharedWizardFrame1Path);

	private Control BuildCard(CharacterData character, int idx, bool unlocked)
	{
		var card = new PanelContainer();
		card.CustomMinimumSize = new Vector2(CardMinWidth, 450);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		card.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		margin.AddChild(vbox);

		var nameLabel = new Label
		{
			Text = unlocked ? character.Name : "???",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 24);
		nameLabel.AddThemeColorOverride("font_color", unlocked ? CharacterVisuals.GetCharacterTint(character.Id).Lightened(0.18f) : new Color(0.72f, 0.72f, 0.78f));
		vbox.AddChild(nameLabel);

		var portraitBg = new PanelContainer();
		portraitBg.CustomMinimumSize = new Vector2(0, 200);
		var portraitStyle = new StyleBoxFlat();
		portraitStyle.BgColor = new Color(0.12f, 0.12f, 0.14f);
		portraitStyle.SetCornerRadiusAll(4);
		portraitBg.AddThemeStyleboxOverride("panel", portraitStyle);
		vbox.AddChild(portraitBg);

		var portraitCenter = new CenterContainer();
		portraitBg.AddChild(portraitCenter);

		portraitCenter.AddChild(BuildPortraitDisplay(character, unlocked));

		if (unlocked)
		{
			string weaponName = character.StartingSpellResource != null ? character.StartingSpellResource.Name : "-";
			vbox.AddChild(MakeInfoLabel($"Starting Spell: {weaponName}"));
			if (!string.IsNullOrWhiteSpace(character.StartingPassiveDescription))
				vbox.AddChild(MakeInfoLabel($"Passive Bonus: {character.StartingPassiveDescription}"));
			if (character.IsLegendaryStart)
				vbox.AddChild(MakeInfoLabel("Starts Legendary!"));
		}
		else
		{
			var lockedLabel = MakeInfoLabel("???\n???\n???");
			lockedLabel.HorizontalAlignment = HorizontalAlignment.Center;
			vbox.AddChild(lockedLabel);
		}

		var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		vbox.AddChild(spacer);

		if (character.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
		{
			var testWizardPickerContainer = new VBoxContainer
			{
				CustomMinimumSize = new Vector2(0f, 76f),
				MouseFilter = Control.MouseFilterEnum.Stop
			};
			testWizardPickerContainer.AddThemeConstantOverride("separation", 6);

			var pickerLabel = new Label
			{
				Text = "Start Spell",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			pickerLabel.AddThemeFontSizeOverride("font_size", 15);
			testWizardPickerContainer.AddChild(pickerLabel);

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
			testWizardPickerContainer.AddChild(testWizardSpellPicker);
			vbox.AddChild(testWizardPickerContainer);
		}

		int capturedIdx = idx;
		card.GuiInput += (InputEvent inputEvent) =>
		{
			if (unlocked && inputEvent is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
				OnCharButtonPressed(capturedIdx);
		};

		return card;
	}

	private static Label MakeInfoLabel(string text)
	{
		return new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
	}

	private static Control BuildPortraitDisplay(CharacterData character, bool unlocked)
	{
		Texture2D portrait = character.Portrait ?? DefaultPortrait;
		Color unlockedTint = CharacterVisuals.GetCharacterTint(character.Id);
		Color portraitModulate = unlocked ? unlockedTint : new Color(0.02f, 0.02f, 0.025f, 0.95f);

		if (!CharacterVisuals.TryBuildIdleFrames(portrait, out SpriteFrames frames))
		{
			return new TextureRect
			{
				Texture = portrait,
				CustomMinimumSize = new Vector2(160, 160),
				ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				Modulate = portraitModulate
			};
		}

		var viewportContainer = new SubViewportContainer
		{
			CustomMinimumSize = new Vector2(160, 160),
			Stretch = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		var viewport = new SubViewport
		{
			Size = new Vector2I(160, 160),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always
		};
		viewportContainer.AddChild(viewport);

		var root = new Node2D();
		viewport.AddChild(root);

		var portraitSprite = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Animation = "idle",
			Position = new Vector2(79, 88),
			Scale = new Vector2(8.5f, 8.5f),
			Centered = true,
			Modulate = portraitModulate
		};
		root.AddChild(portraitSprite);
		portraitSprite.Play("idle");

		return viewportContainer;
	}

	private static Label MakePassiveDescriptionLabel(string text)
	{
		var label = MakeInfoLabel(text);
		label.AddThemeFontSizeOverride("font_size", 12);
		label.Modulate = new Color(0.85f, 0.85f, 0.9f);
		return label;
	}

	private void OnCharButtonPressed(int idx)
	{
		Global.SelectedCharacterIdx = idx;
		var scenePath = "res://scenes/StageSelection.tscn";
		if (ResourceLoader.Exists(scenePath))
			GetTree().ChangeSceneToFile(scenePath);
		else
			GD.PushError($"CharacterSelection: scene not found: {scenePath}");
	}

	private void OnBackButtonPressed()
	{
		var scenePath = "res://scenes/MainMenu.tscn";
		if (ResourceLoader.Exists(scenePath))
			GetTree().ChangeSceneToFile(scenePath);
	}
}

