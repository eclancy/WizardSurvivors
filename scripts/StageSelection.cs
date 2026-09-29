using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class StageSelection : Control
{
	private Button backButton = null!;

	// The chapter roster lives in StageCatalog now. This used to be a private record and a
	// two-entry list, one of four places that independently knew what a stage was called.
	private IReadOnlyList<StageDefinition> stages => StageCatalog.All;

	// Resolved once in _Ready from the save, so the list and the click handler cannot disagree.
	private readonly List<bool> stageUnlocked = new List<bool>();

	public override void _Ready()
	{
		ResolveStageUnlocks();
		CreateBackButton();
		// Screen headings take the display face. They carry a Display font SIZE from the scene
		// file but no FACE, so without this the card titles below them came out in Cinzel and
		// the heading above them in the body face - the wrong way round.
		ResponsiveLayout.SetFont(GetNodeOrNull<Label>("Title"), ResponsiveLayout.TextRole.Display);
		ApplyMenuSkin();
		BuildStageList();

		// After BuildStageList, not before: a locked chapter is not added to the list at all, so
		// the focusable set does not exist until the cards do.
		MenuNavigator.Attach(this);
	}

	// Stage ids match RunResult.StageId ("stage_0", "stage_1", ...) so a boss victory recorded
	// against a stage index unlocks the stage the player sees here.
	private void ResolveStageUnlocks()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		stageUnlocked.Clear();
		// IsStageAvailable rather than IsStageUnlocked: it also resolves the final chapter's
		// campaign gate, and refuses chapters that exist in the roster but have no content yet.
		foreach (StageDefinition stage in stages)
			stageUnlocked.Add(GlobalStatsManager.IsStageAvailable(saveManager?.Data, stage));
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
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Body);
		backButton.Pressed += OnBackButtonPressed;
		AddChild(backButton);
	}

	private void ApplyMenuSkin()
	{
		BonelightSkin.ApplyFullscreenBackdrop(this, "res://assets/bonelight/ui/menu-background.png", 0.95f);
		BonelightSkin.ApplyPanel(GetNodeOrNull<Control>("StageScroll"), inset: true);
	}

	// One chapter per row, each row the full width of the screen.
	//
	// The old two-column grid gave every chapter a 340-wide box, which on the project's 720-wide
	// viewport left each one too narrow to say anything: name, terrain and a clipped line of
	// flavour. A place the player is about to fight through, and possibly free a wizard from,
	// deserves the whole width - a banner on the left and room on the right for what the place is
	// *and* what he has done to it.
	private void BuildStageList()
	{
		var stageList = GetNode<Container>("StageScroll/StageGrid");
		// The scene still says two columns for anyone opening it in the editor; the list is
		// single-column by construction now, so say so here where the layout is actually built.
		if (stageList is GridContainer grid)
			grid.Columns = 1;

		foreach (Node child in stageList.GetChildren())
		{
			child.QueueFree();
		}

		for (int i = 0; i < stages.Count; i++)
		{
			StageDefinition stage = stages[i];

			// A CHAPTER THE PLAYER CANNOT ENTER IS NOT LISTED AT ALL.
			//
			// The list used to carry every chapter in the roster and grey out the closed ones, so
			// on a fresh save it was one playable card under seven dimmed ones and the screen read
			// as mostly unavailable. Hiding them makes the list mean "where you can go", and it
			// grows as the campaign does.
			//
			// The cost, stated so it is a decision rather than an oversight: the Emberdeep's gate
			// is the only one the player cannot satisfy by beating one thing, and its card was the
			// only place the campaign goal and its remaining count were ever written down. Nothing
			// says it now until it opens.
			if (!IsUnlocked(i))
				continue;

			var card = new PanelContainer
			{
				Name = $"StageCard{i + 1}",
				CustomMinimumSize = new Vector2(0, 200),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
			};

			var cardStyle = new StyleBoxFlat();
			cardStyle.BgColor = new Color(0.08f, 0.08f, 0.11f, 0.92f);
			cardStyle.BorderColor = new Color(0.38f, 0.40f, 0.48f, 0.95f);
			cardStyle.SetBorderWidthAll(1);
			cardStyle.SetCornerRadiusAll(6);
			// 8px of padding around four stacked lines of type read as a dense block rather than
			// as a place. The list scrolls either way, so height is the cheap axis to spend here.
			cardStyle.SetContentMarginAll(14);
			card.AddThemeStyleboxOverride("panel", cardStyle);

			// Added before the content so the hover tint washes *behind* the text rather than over
			// it. See BonelightSkin.MakeCardClickOverlay.
			var cardButton = BonelightSkin.MakeCardClickOverlay(true);
			cardButton.Name = $"StageButton{i + 1}";
			int idx = i;
			cardButton.Pressed += () => OnStageButtonPressed(idx);
			card.AddChild(cardButton);

			var row = new HBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			row.AddThemeConstantOverride("separation", 16);
			card.AddChild(row);

			var environmentProfile = StageEnvironmentCatalog.Get(stage.EnvironmentKind);
			var preview = new TextureRect
			{
				Texture = LoadStagePreviewTexture(environmentProfile),
				// Grows with the card. The banner is the only picture of the place the player ever
				// sees before entering it, so it gets to be a banner rather than a thumbnail.
				CustomMinimumSize = new Vector2(220, 172),
				// Covered rather than centered: the banner is a fixed slot in a row now, and a
				// letterboxed tile in it would leave two dead bars per chapter. ClipContents keeps
				// the overflow off the text beside it.
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				ClipContents = true,
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				SizeFlagsVertical = Control.SizeFlags.Fill
			};
			row.AddChild(preview);

			var text = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			text.AddThemeConstantOverride("separation", 6);
			row.AddChild(text);

			var titleLabel = new Label { Text = stage.DisplayName };
			ResponsiveLayout.SetFont(titleLabel, ResponsiveLayout.TextRole.Title);
			text.AddChild(titleLabel);

			// The terrain tag used to sit right-aligned on the title's own row. Two competing
			// strings on one line is what made the card feel packed, and it squeezed both: the tag
			// gets its own line under the name now, where it reads as a subtitle.
			var terrainLabel = new Label
			{
				Text = $"{environmentProfile.DisplayName} • {stage.TerrainCategory}"
			};
			ResponsiveLayout.SetFont(terrainLabel, ResponsiveLayout.TextRole.Micro);
			terrainLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.80f, 0.88f));
			text.AddChild(terrainLabel);

			// Both descriptions are running prose the player reads to choose, so both are Body on
			// the shared scale rather than the 13px they were hardcoded at - below Micro's floor,
			// and the reason four lines fitted in a 132-tall card at all.
			var flavorLabel = new Label { Text = stage.FlavorText };
			ResponsiveLayout.SetBodyText(flavorLabel);
			flavorLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.97f, 0.92f));
			text.AddChild(flavorLabel);

			// The second line is the one that makes this a campaign rather than a level select:
			// what he has done to the place. Warm-tinted so the two descriptions never read as one
			// paragraph. See .ai/world-and-tone.md.
			if (!string.IsNullOrWhiteSpace(stage.CorruptionText))
			{
				var corruptionLabel = new Label { Text = stage.CorruptionText };
				ResponsiveLayout.SetBodyText(corruptionLabel);
				corruptionLabel.AddThemeColorOverride("font_color", new Color(0.87f, 0.74f, 0.53f, 0.94f));
				text.AddChild(corruptionLabel);
			}

			// Everything above is display only, so it all lets the click through to the overlay.
			BonelightSkin.MakeSubtreeClickThrough(row);
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
		if (!IsUnlocked(idx))
			return;

		Global.SelectedStageIdx = idx;
		// The run scene is by far the heaviest load in the game, and it used to be loaded
		// synchronously with the stage list still on screen and nothing acknowledging the click.
		SceneTransition.ChangeScene(this, "res://scenes/node_2d_game.tscn", stages[idx].DisplayName);
	}

	private void OnBackButtonPressed()
	{
		SceneTransition.ChangeScene(this, "res://scenes/CharacterSelection.tscn");
	}

}
