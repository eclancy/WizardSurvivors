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
		ApplyFantasyGuiSkin();
		BuildStageList();
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

	// The final chapter's lock is the only one the player cannot satisfy by beating one thing, so
	// it is the only one that has to report progress rather than a condition. "Recover every spell"
	// with no count is a wall; "4 spells and 2 wizards remain" is a to-do list.
	private string BuildLockedText(StageDefinition stage)
	{
		if (!stage.IsPlayable)
			return "Coming soon";

		if (stage.Gate == StageGate.CampaignComplete)
		{
			var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
			int spells = GlobalStatsManager.RemainingSpellCount(saveManager?.Data);
			int wizards = GlobalStatsManager.RemainingWizardCount(saveManager?.Data);
			if (spells > 0 || wizards > 0)
				return $"Sealed — {spells} spell{(spells == 1 ? "" : "s")} and {wizards} wizard{(wizards == 1 ? "" : "s")} still lost";
		}

		return string.IsNullOrWhiteSpace(stage.LockedHint) ? "Locked" : stage.LockedHint;
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
		FantasyGuiSkin.ApplyFullscreenBackdrop(this, "res://assets/organized/ui/ui-fantasy-rpg-gui-map-1.png", 0.95f);
		FantasyGuiSkin.ApplyPanelBackdrop(GetNodeOrNull<Control>("StageScroll"), "res://assets/organized/ui/ui-fantasy-rpg-gui-map-5.png", 0.18f);
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
			bool unlocked = IsUnlocked(i);

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
			// it. See FantasyGuiSkin.MakeCardClickOverlay.
			var cardButton = FantasyGuiSkin.MakeCardClickOverlay(unlocked);
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

			if (!unlocked)
			{
				// Dim the whole card, not just the label: a locked stage should read as unavailable
				// at a glance rather than only on the line that says so.
				preview.Modulate = new Color(0.45f, 0.45f, 0.50f, 0.85f);
				var lockedLabel = new Label { Text = BuildLockedText(stage) };
				ResponsiveLayout.SetBodyText(lockedLabel, ResponsiveLayout.TextRole.Label);
				lockedLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.55f, 0.55f));
				text.AddChild(lockedLabel);
			}

			// Everything above is display only, so it all lets the click through to the overlay.
			FantasyGuiSkin.MakeSubtreeClickThrough(row);
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
