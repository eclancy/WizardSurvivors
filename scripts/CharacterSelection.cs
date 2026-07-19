using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Data-driven character selection (issue #1/#29): builds one scrollable card per CharacterRoster
// entry at runtime instead of a hardcoded stub list, and respects both CharacterData.IsUnlocked
// and SaveData.UnlockedCharacterIds. Each card shows the character's starting weapon, element and
// passive so the player can make an informed pick before a run.
public partial class CharacterSelection : Control
{
	private List<CharacterData> characters = new List<CharacterData>();

	public override void _Ready()
	{
		characters = CharacterRoster.GetAll();
		var grid = GetNode<GridContainer>("CardScroll/CardGrid");

		// Clear any placeholder cards left in the scene, then build the real roster. Free()
		// (not QueueFree()) so old children are gone immediately instead of coexisting with the
		// freshly-built cards for one frame.
		foreach (var child in grid.GetChildren())
			child.Free();

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");

		for (int i = 0; i < characters.Count; i++)
		{
			var character = characters[i];
			// Safety net: index 0 (the default starter, "Apprentice Wizard") is always selectable
			// even if CharacterData.IsUnlocked somehow fails to deserialize as true (e.g. a stale
			// resource cache), so the menu can never present zero playable options.
			bool unlocked = i == 0
				|| character.IsUnlocked
				|| (saveManager != null && saveManager.Data.UnlockedCharacterIds.Contains(character.Id));

			grid.AddChild(BuildCard(character, i, unlocked));
		}

		var backButton = GetNodeOrNull<Button>("BackButton");
		if (backButton != null)
			backButton.Pressed += OnBackButtonPressed;
	}

	// Shared apprentice portrait for all current wizards. Character identity comes from element
	// color modulation until unique per-character art exists (issue #30).
	private static readonly Texture2D ApprenticePortrait = GD.Load<Texture2D>("res://assets/another_wizard.png");

	private Control BuildCard(CharacterData character, int idx, bool unlocked)
	{
		var card = new PanelContainer();
		card.CustomMinimumSize = new Vector2(280, 450);

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
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
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

		var portraitTexture = new TextureRect
		{
			Texture = ApprenticePortrait,
			CustomMinimumSize = new Vector2(160, 160),
			ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Modulate = unlocked ? Colors.White : new Color(0.02f, 0.02f, 0.025f, 0.95f)
		};
		portraitCenter.AddChild(portraitTexture);

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

		var button = new Button
		{
			Text = unlocked ? "Select" : "Locked",
			Disabled = !unlocked
		};
		int capturedIdx = idx;
		button.Pressed += () => OnCharButtonPressed(capturedIdx);
		vbox.AddChild(button);

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
