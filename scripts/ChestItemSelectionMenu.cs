using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ChestItemSelectionMenu : CanvasLayer
{
	[Signal] public delegate void ItemSelectedEventHandler(string itemId);

	private Control panelRoot = null!;
	private HBoxContainer cardsRow = null!;

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.WhenPaused;
		BuildUiStructure();
	}

	private void BuildUiStructure()
	{
		var dim = new ColorRect
		{
			Name = "BackgroundDim",
			Color = new Color(0.02f, 0.02f, 0.03f, 0.85f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		var panel = new PanelContainer
		{
			Name = "MenuPanel",
			CustomMinimumSize = new Vector2(940, 580)
		};
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = -470;
		panel.OffsetTop = -290;
		panel.OffsetRight = 470;
		panel.OffsetBottom = 290;

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.13f, 0.98f),
			BorderColor = new Color(0.85f, 0.68f, 0.28f, 0.95f)
		};
		panelStyle.SetBorderWidthAll(2);
		panelStyle.SetCornerRadiusAll(10);
		panelStyle.SetContentMarginAll(20);
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(panel);
		panelRoot = panel;

		FantasyGuiSkin.ApplyPanelBackdrop(panel, "res://assets/organized/ui/ui-png-skills-2.png", 0.18f);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		var title = new Label
		{
			Text = "TREASURE CHEST",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 30);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.40f));
		vbox.AddChild(title);

		var subtitle = new Label
		{
			Text = "Select a Relic to Claim Its Power and Advance Your Set Synergies",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		subtitle.AddThemeFontSizeOverride("font_size", 14);
		subtitle.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.86f));
		vbox.AddChild(subtitle);

		cardsRow = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		cardsRow.AddThemeConstantOverride("separation", 16);
		vbox.AddChild(cardsRow);
	}

	public void SetOptions(List<string> itemIds, IReadOnlyCollection<string> ownedItems)
	{
		if (cardsRow == null)
			return;

		foreach (Node child in cardsRow.GetChildren())
		{
			cardsRow.RemoveChild(child);
			child.QueueFree();
		}

		ownedItems ??= Array.Empty<string>();
		foreach (string itemId in itemIds)
		{
			Control card = BuildOptionCard(itemId, ownedItems);
			cardsRow.AddChild(card);
		}
	}

	private Control BuildOptionCard(string itemId, IReadOnlyCollection<string> ownedItems)
	{
		var cardPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(280, 420),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};

		var cardStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.13f, 0.19f, 0.95f),
			BorderColor = new Color(0.32f, 0.36f, 0.48f, 0.90f)
		};
		cardStyle.SetBorderWidthAll(1);
		cardStyle.SetCornerRadiusAll(8);
		cardStyle.SetContentMarginAll(14);
		cardPanel.AddThemeStyleboxOverride("panel", cardStyle);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		cardPanel.AddChild(vbox);

		string iconPath = ChestItemCatalog.GetIconPath(itemId);
		Texture2D texture = FantasyGuiSkin.LoadTextureSafe(iconPath);
		var iconRect = new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(80, 80),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		vbox.AddChild(iconRect);

		var nameLabel = new Label
		{
			Text = ChestItemCatalog.GetDisplayName(itemId),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(nameLabel);

		var descLabel = new Label
		{
			Text = ChestItemCatalog.GetDescription(itemId),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		descLabel.AddThemeFontSizeOverride("font_size", 13);
		descLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.88f, 1.0f));
		vbox.AddChild(descLabel);

		var synergyHeader = new Label
		{
			Text = "Potential Synergies:",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		synergyHeader.AddThemeFontSizeOverride("font_size", 13);
		synergyHeader.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.40f));
		vbox.AddChild(synergyHeader);

		var synergiesBox = new VBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		synergiesBox.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(synergiesBox);

		var sets = ChestItemCatalog.GetAssociatedSets(itemId);
		foreach (var set in sets)
		{
			var (ownedCount, totalRequired) = ChestItemCatalog.GetSetProgress(set, ownedItems);
			bool currentlyOwned = ownedItems.Contains(itemId, StringComparer.OrdinalIgnoreCase);
			int projectedCount = currentlyOwned ? ownedCount : Math.Min(totalRequired, ownedCount + 1);
			bool completesSet = !currentlyOwned && projectedCount >= totalRequired;

			var setPanel = new PanelContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			var setStyle = new StyleBoxFlat
			{
				BgColor = completesSet ? new Color(0.12f, 0.28f, 0.16f, 0.95f) : new Color(0.08f, 0.09f, 0.13f, 0.88f),
				BorderColor = completesSet ? new Color(0.40f, 0.90f, 0.50f, 0.95f) : new Color(0.24f, 0.28f, 0.38f, 0.85f)
			};
			setStyle.SetBorderWidthAll(1);
			setStyle.SetCornerRadiusAll(4);
			setStyle.SetContentMarginAll(6);
			setPanel.AddThemeStyleboxOverride("panel", setStyle);

			var setVbox = new VBoxContainer();
			setVbox.AddThemeConstantOverride("separation", 2);
			setPanel.AddChild(setVbox);

			string progressText = completesSet
				? $"{set.Name} • ({projectedCount}/{totalRequired}) COMPLETES SET!"
				: $"{set.Name} • ({ownedCount}/{totalRequired} items)";
			var titleLabel = new Label
			{
				Text = progressText,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			titleLabel.AddThemeFontSizeOverride("font_size", 12);
			titleLabel.AddThemeColorOverride("font_color", completesSet ? new Color(0.60f, 1.0f, 0.60f) : new Color(0.90f, 0.92f, 0.98f));
			setVbox.AddChild(titleLabel);

			var setDescLabel = new Label
			{
				Text = set.Description,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			setDescLabel.AddThemeFontSizeOverride("font_size", 11);
			setDescLabel.AddThemeColorOverride("font_color", new Color(0.70f, 0.74f, 0.82f));
			setVbox.AddChild(setDescLabel);

			synergiesBox.AddChild(setPanel);
		}

		var claimButton = new Button
		{
			Text = "Claim Relic",
			CustomMinimumSize = new Vector2(0, 38),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			ProcessMode = ProcessModeEnum.Always
		};
		FantasyGuiSkin.StyleButton(claimButton, FantasyGuiSkin.GlyphPlus);
		string capturedId = itemId;
		claimButton.Pressed += () => EmitSignal(SignalName.ItemSelected, capturedId);
		vbox.AddChild(claimButton);

		return cardPanel;
	}
}
