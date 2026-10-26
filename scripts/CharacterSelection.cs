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
		("starfall", "res://SpellData_Starfall.tres"),
		("familiar", "res://SpellData_Familiar.tres"),
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

	// The PC layout (landscape, not ResponsiveLayout.IsNarrow): a roster of compact portrait cards on
	// the left and a detail pane on the right that follows whatever card is hovered or focused. On a
	// 720-tall canvas the phone's tall cards fitted one row of the seven wizards and hid the rest
	// behind a scrollbar; a roster of faces fits everyone, and the words move to one place that has
	// the room for them. The phone keeps its cards.
	private bool wideLayout;

	// What a card may show (.ai/side-events.md, "Secret wizards"). A LOCKED wizard is his
	// silhouette and how he is found, with the name kept back: the name is what freeing him gives
	// you ("Tituba is free - a new wizard"), and the rescued are people, not a class you can read
	// off a coat. A SECRET wizard is a sealed card and nothing else, until UnlockCatalog's reveal
	// condition holds - then he is an ordinary lock.
	private enum CardState { Unlocked, Locked, Secret }

	private readonly List<CardState> cardStates = new();
	private VBoxContainer detailBox;
	private Control testWizardPickerHost;
	private int detailIdx = -1;
	private const float CompactCardWidth = 160f;
	private const float RosterFraction = 0.6f;
	private string defaultTestWizardSpellId = "magic_missile";

	public override void _Ready()
	{
		characters = CharacterRoster.GetAll();
		var grid = GetNode<GridContainer>("CardScroll/CardGrid");
		wideLayout = !ResponsiveLayout.IsNarrow(this);

		// The scene hardcodes three columns, which overflows both edges of a 720-wide portrait
		// viewport. Derive the count from the card width that actually fits, and keep it correct
		// if the window is resized or the device rotated.
		// Four across on a PC's landscape canvas: at three the grid stopped three-quarters of the way
		// across and left the right of the screen empty. The phone fits fewer and takes what fits.
		if (wideLayout)
		{
			// The roster takes the left of the screen; the detail pane is built beside it below.
			var scroll = GetNode<Control>("CardScroll");
			scroll.AnchorRight = RosterFraction;
			scroll.OffsetRight = -12f;
			ResponsiveLayout.BindGridColumns(grid, CompactCardWidth, maxColumns: 4);
		}
		else
		{
			ResponsiveLayout.BindGridColumns(grid, CardMinWidth, maxColumns: 3);
		}

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
			CardState state = unlocked ? CardState.Unlocked
				: GlobalStatsManager.IsCharacterRevealed(saveManager?.Data, character.Id) ? CardState.Locked
				: CardState.Secret;

			cardStates.Add(state);
			grid.AddChild(wideLayout ? BuildCompactCard(character, i, state) : BuildCard(character, i, state));
		}

		if (wideLayout)
		{
			BuildDetailPane();
			int first = Global.SelectedCharacterIdx >= 0 && Global.SelectedCharacterIdx < characters.Count
				? Global.SelectedCharacterIdx : 0;
			ShowDetail(first);
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
		// Clear of the phone's rounded corners and camera hole; nothing on desktop (SafeArea).
		SafeArea.Apply(this);
		MenuNavigator.Attach(this);
	}

	public override void _ExitTree()
	{
		// The test wizard's picker is lifted out of the detail pane, not freed, while another wizard
		// is shown there (ShowDetail), so on leaving the screen with someone else in the pane it is
		// in no tree and nothing frees it - and an OptionButton owns a popup window, so the leak at
		// exit was a whole viewport, its render target and its text.
		if (testWizardPickerHost != null && IsInstanceValid(testWizardPickerHost) && !testWizardPickerHost.IsInsideTree())
			testWizardPickerHost.Free();
		testWizardPickerHost = null;
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
	// Loaded where it is used, not held in a static: a static Texture2D outlives the engine's own
	// reference and trips the finalizer (BonelightSkin's remarks). ResourceLoader caches it.
	private const string SharedWizardFrame1Path = "res://assets/bonelight/characters/unknown-portrait.png";

	// A secret wizard's card: a wax seal with a question struck in it (tools/art/props_fx.py,
	// sealed_portrait). It is drawn on the same 48 cell as the wizards so it sits on their grid.
	private const string SealedPortraitPath = "res://assets/bonelight/characters/sealed-portrait.png";

	// The line a secret's card carries in place of a hint. It says there is someone, not how to
	// find him: a hint here would be the spoiler the secret exists to hold back.
	private const string SecretLine = "Not yet discovered.";

	private static readonly Color HiddenNameInk = new(0.72f, 0.72f, 0.78f);

	private static string CardName(CharacterData character, CardState state) =>
		state == CardState.Unlocked ? character.Name : "???";

	private static Color CardNameInk(CharacterData character, CardState state) =>
		state == CardState.Unlocked ? CharacterVisuals.GetCharacterTint(character.Id).Lightened(0.18f) : HiddenNameInk;

	private static string LockedLine(CharacterData character, CardState state) =>
		state == CardState.Secret ? SecretLine : UnlockCatalog.GetLockedHint(character.Id, UnlockKind.Character);

	private Control BuildCard(CharacterData character, int idx, CardState state)
	{
		bool unlocked = state == CardState.Unlocked;
		var card = new PanelContainer();
		// 450 tall fitted one row on a 720-tall PC canvas, with the second row of wizards behind a
		// scrollbar. Shorter on a PC, so both rows are on screen; the phone has the height.
		card.CustomMinimumSize = new Vector2(CardMinWidth, ResponsiveLayout.IsNarrow(this) ? 450f : 300f);

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
			Text = CardName(character, state),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(nameLabel, ResponsiveLayout.TextRole.Title);
		nameLabel.AddThemeColorOverride("font_color", CardNameInk(character, state));
		vbox.AddChild(nameLabel);

		var portraitBg = new PanelContainer();
		portraitBg.CustomMinimumSize = new Vector2(0, ResponsiveLayout.IsNarrow(this) ? 200f : 140f);
		var portraitStyle = new StyleBoxFlat();
		portraitStyle.BgColor = new Color(0.12f, 0.12f, 0.14f);
		portraitStyle.SetCornerRadiusAll(4);
		portraitBg.AddThemeStyleboxOverride("panel", portraitStyle);
		vbox.AddChild(portraitBg);

		var portraitCenter = new CenterContainer();
		portraitBg.AddChild(portraitCenter);

		portraitCenter.AddChild(BuildPortraitDisplay(character, state));

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
			// A lock that does not explain itself is indistinguishable from missing content. A
			// secret explains nothing on purpose, and says only that it is one.
			var lockedLabel = MakeInfoLabel(LockedLine(character, state));
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
			ResponsiveLayout.SetFont(pickerLabel, ResponsiveLayout.TextRole.Label);
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

	// A roster card on a PC: the face and the name, nothing else - the words live in the detail
	// pane. Hovering or focusing it shows that wizard there; pressing it chooses him, as on a phone.
	private Control BuildCompactCard(CharacterData character, int idx, CardState state)
	{
		bool unlocked = state == CardState.Unlocked;
		var card = new PanelContainer { CustomMinimumSize = new Vector2(CompactCardWidth, 220f) };

		var cardButton = BonelightSkin.MakeCardClickOverlay(unlocked);
		cardButton.Name = $"CharacterButton{idx + 1}";
		int capturedIdx = idx;
		cardButton.Pressed += () => OnCharButtonPressed(capturedIdx);
		cardButton.FocusEntered += () => ShowDetail(capturedIdx);
		cardButton.MouseEntered += () => ShowDetail(capturedIdx);
		card.AddChild(cardButton);

		var margin = new MarginContainer();
		foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
			margin.AddThemeConstantOverride(side, 8);
		card.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		margin.AddChild(vbox);

		var portraitBg = new PanelContainer { CustomMinimumSize = new Vector2(0, 160f) };
		var portraitStyle = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.14f) };
		portraitStyle.SetCornerRadiusAll(4);
		portraitBg.AddThemeStyleboxOverride("panel", portraitStyle);
		var portraitCenter = new CenterContainer();
		portraitBg.AddChild(portraitCenter);
		portraitCenter.AddChild(BuildPortraitDisplay(character, state));
		vbox.AddChild(portraitBg);

		var nameLabel = new Label
		{
			Text = CardName(character, state),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(nameLabel, ResponsiveLayout.TextRole.Label);
		nameLabel.AddThemeColorOverride("font_color", CardNameInk(character, state));
		vbox.AddChild(nameLabel);

		BonelightSkin.MakeSubtreeClickThrough(margin, null);
		return card;
	}

	private void BuildDetailPane()
	{
		var pane = new PanelContainer { Name = "DetailPane" };
		pane.AnchorLeft = RosterFraction;
		pane.AnchorRight = 1f;
		pane.AnchorTop = 0f;
		pane.AnchorBottom = 1f;
		pane.OffsetLeft = 12f;
		pane.OffsetRight = -24f;
		pane.OffsetTop = 64f;
		pane.OffsetBottom = -24f;
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.08f, 0.11f, 0.94f),
			BorderColor = new Color(0.38f, 0.40f, 0.48f, 0.95f)
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(16);
		pane.AddThemeStyleboxOverride("panel", style);
		AddChild(pane);

		detailBox = new VBoxContainer();
		detailBox.AddThemeConstantOverride("separation", 10);
		pane.AddChild(detailBox);
	}

	// Rebuilt rather than updated in place: it is seven wizards, built on a hover, and a pane that
	// rebuilds cannot leave one wizard's line behind under the next.
	private void ShowDetail(int idx)
	{
		if (detailBox == null || idx < 0 || idx >= characters.Count || idx == detailIdx)
			return;
		detailIdx = idx;
		CharacterData character = characters[idx];
		CardState state = idx < cardStates.Count ? cardStates[idx] : CardState.Secret;
		bool unlocked = state == CardState.Unlocked;

		// The test wizard's picker is created once and only moved in and out, so a hover never
		// resets the spell the player picked.
		if (testWizardPickerHost != null && testWizardPickerHost.GetParent() == detailBox)
			detailBox.RemoveChild(testWizardPickerHost);
		foreach (Node child in detailBox.GetChildren())
			child.QueueFree();

		var portraitCenter = new CenterContainer { CustomMinimumSize = new Vector2(0, 260f) };
		portraitCenter.AddChild(BuildPortraitDisplay(character, state, scale: 5));
		detailBox.AddChild(portraitCenter);

		var name = new Label { Text = CardName(character, state), HorizontalAlignment = HorizontalAlignment.Center };
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Display);
		name.AddThemeColorOverride("font_color", CardNameInk(character, state));
		detailBox.AddChild(name);

		if (unlocked)
		{
			string weaponName = character.StartingSpellResource != null ? character.StartingSpellResource.Name : "-";
			detailBox.AddChild(MakeInfoLabel($"Starting Spell: {weaponName}"));
			if (!string.IsNullOrWhiteSpace(character.StartingPassiveDescription))
				detailBox.AddChild(MakeInfoLabel($"Passive Bonus: {character.StartingPassiveDescription}"));
			if (character.IsLegendaryStart)
				detailBox.AddChild(MakeInfoLabel("Starts Legendary!"));
		}
		else
		{
			detailBox.AddChild(MakeInfoLabel(LockedLine(character, state)));
		}

		detailBox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

		if (unlocked && character.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
		{
			testWizardPickerHost ??= BuildTestWizardPicker();
			detailBox.AddChild(testWizardPickerHost);
		}

		if (unlocked)
		{
			var choose = new Button
			{
				Text = $"Choose {character.Name}",
				CustomMinimumSize = new Vector2(0, 52f),
				FocusMode = Control.FocusModeEnum.None
			};
			BonelightSkin.StyleButton(choose);
			int capturedIdx = idx;
			choose.Pressed += () => OnCharButtonPressed(capturedIdx);
			detailBox.AddChild(choose);
		}
	}

	private Control BuildTestWizardPicker()
	{
		var host = new VBoxContainer();
		host.AddThemeConstantOverride("separation", 6);
		var pickerLabel = new Label { Text = "Start Spell", HorizontalAlignment = HorizontalAlignment.Center };
		ResponsiveLayout.SetFont(pickerLabel, ResponsiveLayout.TextRole.Label);
		host.AddChild(pickerLabel);
		testWizardSpellPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		PopulateTestWizardSpellOptions();
		testWizardSpellPicker.ItemSelected += OnTestWizardSpellSelected;
		host.AddChild(testWizardSpellPicker);
		return host;
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

	private static Control BuildPortraitDisplay(CharacterData character, CardState state, int scale = 3)
	{
		// The box holds the 48px cell at a whole scale: 160 at 3x for a card, 256 at 5x for the pane.
		// x3 is the largest whole scale that fits a card - x8 once showed only the middle of the coat,
		// and the whole figure is the point now that each wizard is told apart by silhouette.
		int box = scale == 3 ? 160 : 48 * scale + 16;
		Texture2D portrait = character.Portrait ?? GD.Load<Texture2D>(SharedWizardFrame1Path);

		// Drawn straight onto the canvas at a whole number of SCREEN pixels per art pixel, which is
		// what keeps it sharp on a big or fractionally-scaled window. It used to go through a
		// SubViewport at canvas resolution, which filtered it bilinearly and was then stretched by
		// the window: the blur the user saw full screen (PixelArtRect's remarks).
		List<Texture2D> frames = state switch
		{
			CardState.Secret => new List<Texture2D> { GD.Load<Texture2D>(SealedPortraitPath) },
			CardState.Locked => CharacterVisuals.BuildSilhouetteFrames(CharacterVisuals.GetIdleFrames(portrait)),
			_ => CharacterVisuals.GetIdleFrames(portrait),
		};
		return new PixelArtRect(frames, scale, box)
		{
			FramesPerSecond = CharacterVisuals.IdleFramesPerSecond,
			// White for every wizard with art of his own (CharacterVisuals.GetCharacterTint). The
			// silhouette and the seal are drawn in their final colours and take no tint.
			Modulate = state == CardState.Unlocked ? CharacterVisuals.GetCharacterTint(character.Id) : Colors.White,
		};
	}

	private static Label MakePassiveDescriptionLabel(string text)
	{
		var label = MakeInfoLabel(text);
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Label);
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

