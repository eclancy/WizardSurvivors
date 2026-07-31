using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class StageSelection : Control
{
	private sealed record StageDefinition(string Name, string TerrainCategory, string PreviewTexturePath, bool Unlocked);

	private Button backButton = null!;

	private readonly List<StageDefinition> stages = new List<StageDefinition>()
	{
		new("Enchanted Forest", "Forest path", "res://assets/ground_tile.png", true),
		new("Cursed Castle", "Dungeon stone", "res://assets/imported/fantasy/source_mirror/Fantasy Dungeon tilesets/Fantasy_Dungeon_A1_darker.png", true),
		new("Mystic Ruins", "Rocky ruins", "res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png", true),
		new("Bramble Thicket", "Dense forest growth", "res://assets/ground_tile.png", true),
		new("Elderwood Grove", "Ancient woodland", "res://assets/ground_tile.png", true),
		new("Broken Highlands", "Broken stone slopes", "res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png", true),
	};

	public override void _Ready()
	{
		CreateBackButton();
		ApplyFantasyGuiSkin();
		BuildStageList();
	}

	private void CreateBackButton()
	{
		backButton = new Button
		{
			Name = "BackButton",
			Text = "Back",
			CustomMinimumSize = new Vector2(160f, 46f)
		};
		backButton.AnchorLeft = 0f;
		backButton.AnchorTop = 0f;
		backButton.AnchorRight = 0f;
		backButton.AnchorBottom = 0f;
		backButton.OffsetLeft = 20f;
		backButton.OffsetTop = 16f;
		backButton.OffsetRight = 20f + 160f;
		backButton.OffsetBottom = 16f + 46f;
		backButton.AddThemeFontSizeOverride("font_size", 20);
		backButton.Pressed += OnBackButtonPressed;
		AddChild(backButton);
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
			Global.SelectedStageIdx = idx;
			var scenePath = "res://scenes/node_2d_game.tscn";
			if (ResourceLoader.Exists(scenePath))
				GetTree().ChangeSceneToFile(scenePath);
			else
				GD.PushError($"StageSelection: scene not found: {scenePath}");
		}
	}

	private void OnBackButtonPressed()
	{
		var scenePath = "res://scenes/CharacterSelection.tscn";
		if (ResourceLoader.Exists(scenePath))
			GetTree().ChangeSceneToFile(scenePath);
		else
			GD.PushError($"StageSelection: scene not found: {scenePath}");
	}

}
