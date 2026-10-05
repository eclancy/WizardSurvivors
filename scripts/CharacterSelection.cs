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
		("cinderbreath", "res://SpellData_Cinderbreath.tres"),
		("mirefoot", "res://SpellData_Mirefoot.tres"),
		("kindled_ward", "res://SpellData_KindledWard.tres"),
		("gravewell", "res://SpellData_Gravewell.tres"),
		("hollow_star", "res://SpellData_HollowStar.tres"),
		("iron_palisade", "res://SpellData_IronPalisade.tres"),
		("contagion", "res://SpellData_Contagion.tres"),
		("bramble_seed", "res://SpellData_BrambleSeed.tres"),
		("frost_shard", "res://SpellData_FrostShard.tres"),
		("riptide", "res://SpellData_Riptide.tres"),
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
		("hunters_draw", "res://SpellData_HuntersDraw.tres"),
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
			// UnlockCatalog plus the save is the whole answer now. CharacterData.IsUnlocked is
			// deliberately NOT consulted: it is an [Export] that defaults to true, so honouring it
			// would mean every wizard added from here on ships unlocked unless someone remembered to
			// flip it - which is precisely the failure that left the old character rail dormant.
			// The previous check also used a case-sensitive List.Contains, alone among every unlock
			// check in the project.
			bool unlocked = GlobalStatsManager.IsCharacterUnlocked(saveManager?.Data, character.Id);

			grid.AddChild(BuildCard(character, i, unlocked));
		}

		var backButton = GetNodeOrNull<Button>("BackButton");
		if (backButton != null)
			backButton.Pressed += OnBackButtonPressed;

		// Screen headings take the display face. They carry a Display font SIZE from the scene
		// file but no FACE, so without this the card titles below them came out in Cinzel and
		// the heading above them in the body face - the wrong way round.
		ResponsiveLayout.SetFont(GetNodeOrNull<Label>("Title"), ResponsiveLayout.TextRole.Display);
		ApplyMenuSkin();

		// Last, once every card exists: this grants the selection a pad needs and rings whatever
		// the player points at, with a stick or with a mouse.
		MenuNavigator.Attach(this);
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

	private void ApplyMenuSkin()
	{
		// The same plate the stage list uses. It was a bought GUI backdrop - the last reference to
		// assets/organized/ anywhere in the project - and the two selection screens sit next to each
		// other in the flow, so sharing one backdrop is also the right answer independently.
		BonelightSkin.ApplyFullscreenBackdrop(this, "res://assets/bonelight/ui/menu-background.png", 0.96f);
		BonelightSkin.ApplyPanel(GetNodeOrNull<Control>("CardScroll"), inset: true);
		BonelightSkin.ApplyButtonsInTree(this, 5);
		BonelightSkin.StyleButton(GetNodeOrNull<Button>("BackButton"));
	}

	// Card width the grid sizes its column count against; see ResponsiveLayout.BindGridColumns.
	private const float CardMinWidth = 280f;

	// What a card shows for a character with no Portrait of its own. It was a bought dungeon-pack
	// priest - the last character sheet in the game we could not publish, and a priest standing
	// in for a wizard. It is a hooded figure with no face now, which is the honest picture for
	// "not drawn yet": the cast gains eight rescued wizards long before all eight have art.
	private const string SharedWizardFrame1Path = "res://assets/bonelight/characters/unknown-portrait.png";
	private static readonly Texture2D DefaultPortrait = GD.Load<Texture2D>(SharedWizardFrame1Path);

	private Control BuildCard(CharacterData character, int idx, bool unlocked)
	{
		var card = new PanelContainer();
		card.CustomMinimumSize = new Vector2(CardMinWidth, 450);

		// First child, so the card's content draws over it and the hover tint reads as the card
		// lighting up rather than as a film across the portrait. Every display control below is
		// then made click-through, which is what actually makes the *whole* card a hit area: the
		// old handler sat on the PanelContainer, but the MarginContainer and VBoxContainer inside
		// it default to MouseFilter.Stop and covered everything except a 12px rim.
		var cardButton = BonelightSkin.MakeCardClickOverlay(unlocked);
		cardButton.Name = $"CharacterButton{idx + 1}";
		int capturedIdx = idx;
		cardButton.Pressed += () => OnCharButtonPressed(capturedIdx);
		card.AddChild(cardButton);

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
		ResponsiveLayout.SetFont(nameLabel, ResponsiveLayout.TextRole.Title);
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
			// A lock that does not explain itself is indistinguishable from missing content.
			var lockedLabel = MakeInfoLabel(UnlockCatalog.GetLockedHint(character.Id, UnlockKind.Character));
			lockedLabel.HorizontalAlignment = HorizontalAlignment.Center;
			vbox.AddChild(lockedLabel);
		}

		var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		vbox.AddChild(spacer);

		Control testWizardPicker = null;
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
			ResponsiveLayout.SetFont(pickerLabel, ResponsiveLayout.TextRole.Micro);
			testWizardPickerContainer.AddChild(pickerLabel);

			testWizardSpellPicker = new OptionButton
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			// No icon until the Bonelight icon set is drawn; the pack glyph that used to sit
			// here went with the rest of the licensed art.
			Texture2D pickerIcon = null;
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
			testWizardPicker = testWizardPickerContainer;
		}

		// The test wizard's spell dropdown is the one thing on a card that must keep its own input;
		// everything else lets the click fall through to the overlay button underneath.
		BonelightSkin.MakeSubtreeClickThrough(margin, testWizardPicker);

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
			// Integer scale so the portrait lands on whole pixels now that the project
			// filters nearest (.ai/art-direction.md section 6). 8.5 shimmered.
			Scale = new Vector2(8f, 8f),
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
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.Modulate = new Color(0.85f, 0.85f, 0.9f);
		return label;
	}

	private void OnCharButtonPressed(int idx)
	{
		Global.SelectedCharacterIdx = idx;
		string chosen = idx >= 0 && idx < characters.Count ? characters[idx].Name : string.Empty;
		SceneTransition.ChangeScene(this, "res://scenes/StageSelection.tscn", chosen);
	}

	private void OnBackButtonPressed()
	{
		// The menu lives on the title screen now, so going "back to the menu" means loading
		// its shell and telling it to skip the press-any-key beat.
		Global.OpenMenuImmediately = true;
		SceneTransition.ChangeScene(this, "res://scenes/TitleScreen.tscn");
	}
}

