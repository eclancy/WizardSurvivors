using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class StageSelection : Control
{
	// LockedHint is what the card shows while the stage is closed, so a lock always explains itself.
	private sealed record StageDefinition(string Name, string TerrainCategory, string FlavorText, StageEnvironmentKind EnvironmentKind, string LockedHint);

	private Button backButton = null!;

	private readonly List<StageDefinition> stages = new List<StageDefinition>()
	{
		new("Enchanted Forest", "Forest path", "A bright woodland trail where ancient trees and thick brush crowd the battlefield.", StageEnvironmentKind.Forest, string.Empty),
		new("Cursed Castle", "Dungeon stone", "Stone corridors and crumbling keeps make this a grim choke-point of ruin and shadow.", StageEnvironmentKind.Castle, "Locked — defeat Elderbark in the Enchanted Forest"),
	};

	// Resolved once in _Ready from the save, so the list and the click handler cannot disagree.
	private readonly List<bool> stageUnlocked = new List<bool>();

	public override void _Ready()
	{
		ResolveStageUnlocks();
		CreateBackButton();
		ApplyFantasyGuiSkin();
		BuildStageList();
	}

	// Stage ids match RunResult.StageId ("stage_0", "stage_1", ...) so a boss victory recorded
	// against a stage index unlocks the stage the player sees here.
	private void ResolveStageUnlocks()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		stageUnlocked.Clear();
		for (int i = 0; i < stages.Count; i++)
			stageUnlocked.Add(GlobalStatsManager.IsStageUnlocked(saveManager?.Data, $"stage_{i}"));
	}

	private bool IsUnlocked(int index) => index >= 0 && index < stageUnlocked.Count && stageUnlocked[index];

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
		FantasyGuiSkin.ApplyFullscreenBackdrop(this, "res://assets/organized/ui/ui-fantasy-rpg-gui-map-1.png", 0.95f);
		FantasyGuiSkin.ApplyPanelBackdrop(GetNodeOrNull<Control>("StageScroll"), "res://assets/organized/ui/ui-fantasy-rpg-gui-map-5.png", 0.18f);
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

			var environmentProfile = StageEnvironmentCatalog.Get(stage.EnvironmentKind);
			var preview = new TextureRect
			{
				Texture = LoadStagePreviewTexture(environmentProfile),
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
				Text = $"{environmentProfile.DisplayName} • {stage.TerrainCategory}",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			terrainLabel.AddThemeFontSizeOverride("font_size", 14);
			terrainLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.80f, 0.88f));
			stack.AddChild(terrainLabel);

			var flavorLabel = new Label
			{
				Text = stage.FlavorText,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(0f, 44f)
			};
			flavorLabel.AddThemeFontSizeOverride("font_size", 13);
			flavorLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.97f, 0.92f));
			stack.AddChild(flavorLabel);

			bool unlocked = IsUnlocked(i);
			if (!unlocked)
			{
				// Dim the whole card, not just the label: a locked stage should read as unavailable
				// at a glance rather than only on the line that says so.
				preview.Modulate = new Color(0.45f, 0.45f, 0.50f, 0.85f);
				var lockedLabel = new Label
				{
					Text = string.IsNullOrWhiteSpace(stage.LockedHint) ? "Locked" : stage.LockedHint,
					HorizontalAlignment = HorizontalAlignment.Center,
					AutowrapMode = TextServer.AutowrapMode.WordSmart
				};
				lockedLabel.AddThemeFontSizeOverride("font_size", 14);
				lockedLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.55f, 0.55f));
				stack.AddChild(lockedLabel);
			}

			// The entire card is clickable; a transparent overlay button captures
			// input across the whole box and provides hover/press feedback.
			var cardButton = new Button
			{
				Name = $"StageButton{i + 1}",
				Flat = true,
				Disabled = !unlocked,
				MouseFilter = Control.MouseFilterEnum.Stop,
				MouseDefaultCursorShape = unlocked ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow
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

	private Texture2D? LoadStagePreviewTexture(StageEnvironmentProfile environmentProfile)
	{
		Texture2D? baseTexture = ResourceLoader.Load<Texture2D>(environmentProfile.BackgroundTexturePath);
		if (baseTexture == null)
			return null;

		if (environmentProfile.BackgroundRegion.Size == Vector2.Zero)
			return baseTexture;

		return new AtlasTexture
		{
			Atlas = baseTexture,
			Region = environmentProfile.BackgroundRegion
		};
	}

	private void OnStageButtonPressed(int idx)
	{
		if (IsUnlocked(idx))
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
