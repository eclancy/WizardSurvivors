using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class ChestItemSelectionMenu : CanvasLayer
{
	[Signal] public delegate void ItemSelectedEventHandler(string itemId);

	private Control panelRoot = null!;
	// BoxContainer, not HBoxContainer: Godot refuses to change orientation on the H/V subclasses
	// ("Can.t change orientation of HBoxContainer"), and this row flips to vertical on a phone.
	private BoxContainer cardsRow = null!;

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.WhenPaused;
		BuildUiStructure();
		MenuNavigator.Attach(this);
	}

	private void BuildUiStructure()
	{
		// A dim, not a blackout - the same treatment the level-up menu got. Opening a chest
		// interrupts the run; it should not look like leaving it. This menu's cards are already
		// near-opaque panels, so they stay readable over whatever shows through.
		var dim = new ColorRect
		{
			Name = "BackgroundDim",
			Color = new Color(0.02f, 0.025f, 0.04f, 0.62f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		// The panel used to be a fixed 940x580, wider than a 720-unit portrait viewport, so both
		// outer cards were cut off. Size it to the space that actually exists.
		Vector2 viewport = ResponsiveLayout.ViewportSize(this);
		Vector2 panelSize = new Vector2(
			Mathf.Min(940f, viewport.X - 32f),
			viewport.Y - 60f);

		var panel = new PanelContainer
		{
			Name = "MenuPanel",
			CustomMinimumSize = panelSize
		};
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = -panelSize.X * 0.5f;
		panel.OffsetTop = -panelSize.Y * 0.5f;
		panel.OffsetRight = panelSize.X * 0.5f;
		panel.OffsetBottom = panelSize.Y * 0.5f;

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

		BonelightSkin.ApplyPanel(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		var title = new Label
		{
			Text = "TREASURE CHEST",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.40f));
		vbox.AddChild(title);

		var subtitle = new Label
		{
			Text = "Claim a relic, and advance a Full Set Enchantment",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(subtitle, ResponsiveLayout.TextRole.Micro);
		subtitle.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.86f));
		vbox.AddChild(subtitle);

		cardsRow = new BoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		cardsRow.AddThemeConstantOverride("separation", 16);

		// Relic cards carry a lot of text - name, effect, and up to three synergy entries - so on
		// a phone they stack rather than sitting side by side, and three stacked cards are taller
		// than the screen. BoxContainer.Vertical flips the row without rebuilding it, and the
		// scroll container gives the overflow somewhere to go.
		if (ResponsiveLayout.IsNarrow(this))
		{
			cardsRow.Vertical = true;
			var scroll = new ScrollContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
				FollowFocus = true
			};
			scroll.AddChild(cardsRow);
			vbox.AddChild(scroll);
		}
		else
		{
			vbox.AddChild(cardsRow);
		}
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

		// The previous chest's cards were just freed, so the pad's selection went with them.
		MenuNavigator.Attach(this);
	}

	private Control BuildOptionCard(string itemId, IReadOnlyCollection<string> ownedItems)
	{
		var cardPanel = new PanelContainer
		{
			// Stacked cards fill the width and size to their content instead of a fixed 420 tall.
			CustomMinimumSize = ResponsiveLayout.IsNarrow(this) ? new Vector2(0, 0) : new Vector2(280, 420),
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
		Texture2D texture = BonelightSkin.LoadTextureSafe(iconPath);
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
		ResponsiveLayout.SetFont(nameLabel, ResponsiveLayout.TextRole.Body);
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(nameLabel);

		var descLabel = new Label
		{
			Text = ChestItemCatalog.GetDescription(itemId),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(descLabel, ResponsiveLayout.TextRole.Micro);
		descLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.88f, 1.0f));
		vbox.AddChild(descLabel);

		var synergyHeader = new Label
		{
			Text = "Full Set Enchantments:",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		ResponsiveLayout.SetFont(synergyHeader, ResponsiveLayout.TextRole.Micro);
		synergyHeader.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.40f));
		vbox.AddChild(synergyHeader);

		var synergiesBox = new VBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		synergiesBox.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(synergiesBox);

		// Symbols, not paragraphs.
		//
		// Every set this item belongs to used to get a bordered panel carrying its name, its
		// progress and a full sentence of description. Three of those stacked under one chest item
		// pushed the claim button off a phone screen, and the sentences are reference text nobody
		// reads while a chest is open - what the player is deciding is "does this finish something".
		//
		// So the row answers that at a glance with one symbol per set and a progress badge, and the
		// sentence is one press away. Same bargain as the element notes on the level-up card.
		var sets = ChestItemCatalog.GetAssociatedSets(itemId);

		var symbolRow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		symbolRow.AddThemeConstantOverride("h_separation", 6);
		symbolRow.AddThemeConstantOverride("v_separation", 6);
		synergiesBox.AddChild(symbolRow);

		var detailPanel = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false
		};
		var detailStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.13f, 0.92f),
			BorderColor = new Color(0.24f, 0.28f, 0.38f, 0.85f)
		};
		detailStyle.SetBorderWidthAll(1);
		detailStyle.SetCornerRadiusAll(4);
		detailStyle.SetContentMarginAll(6);
		detailPanel.AddThemeStyleboxOverride("panel", detailStyle);

		var detailLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		ResponsiveLayout.SetFont(detailLabel, ResponsiveLayout.TextRole.Micro);
		detailLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.86f, 0.94f));
		detailPanel.AddChild(detailLabel);
		synergiesBox.AddChild(detailPanel);

		// Which symbol is open, per card. A second press on the same one closes it, and pressing a
		// different one swaps rather than stacking - only one sentence is ever on screen.
		string openSetId = null;

		foreach (var set in sets)
		{
			var (ownedCount, totalRequired) = ChestItemCatalog.GetSetProgress(set, ownedItems);
			bool currentlyOwned = ownedItems.Contains(itemId, StringComparer.OrdinalIgnoreCase);
			int projectedCount = currentlyOwned ? ownedCount : Math.Min(totalRequired, ownedCount + 1);
			bool completesSet = !currentlyOwned && projectedCount >= totalRequired;

			ChestSetDefinition capturedSet = set;
			int capturedOwned = ownedCount;
			int capturedTotal = totalRequired;
			bool capturedCompletes = completesSet;

			var symbol = new Button
			{
				Icon = BonelightSkin.LoadTextureSafe(set.IconPath),
				Text = $"{projectedCount}/{totalRequired}",
				CustomMinimumSize = new Vector2(76, 44),
				TooltipText = set.Name,
				ExpandIcon = true
			};
			ResponsiveLayout.SetFont(symbol, ResponsiveLayout.TextRole.Micro);
			BonelightSkin.ApplyButtonSet(new[] { symbol }, 14);

			// The one piece of information worth carrying without a press: this item finishes it.
			if (completesSet)
				symbol.Modulate = new Color(0.60f, 1.0f, 0.62f);

			symbol.Pressed += () =>
			{
				if (openSetId == capturedSet.Id)
				{
					openSetId = null;
					detailPanel.Visible = false;
					return;
				}

				openSetId = capturedSet.Id;
				string progress = capturedCompletes
					? $"COMPLETES SET ({capturedTotal}/{capturedTotal})"
					: $"{capturedOwned}/{capturedTotal} pieces";
				detailLabel.Text = $"{capturedSet.Name} - {progress}" + System.Environment.NewLine
					+ capturedSet.Description;
				detailPanel.Visible = true;
			};

			symbolRow.AddChild(symbol);
		}

		var claimButton = new Button
		{
			Text = "Claim Relic",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			ProcessMode = ProcessModeEnum.Always
		};
		ResponsiveLayout.EnsureTouchTarget(claimButton);
		BonelightSkin.StyleButton(claimButton);
		string capturedId = itemId;
		claimButton.Pressed += () => EmitSignal(SignalName.ItemSelected, capturedId);
		vbox.AddChild(claimButton);

		return cardPanel;
	}
}
