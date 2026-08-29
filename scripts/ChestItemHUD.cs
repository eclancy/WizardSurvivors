using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// In-run HUD that displays owned chest items and active synergy bonuses.
/// Positioned in the top-left corner with a compact layout:
/// - Owned relics shown as small icon grid
/// - Active set bonuses listed below with descriptions
/// - Incomplete sets shown as "X/Y items needed"
/// </summary>
public partial class ChestItemHUD : CanvasLayer
{
	private Label relicCountLabel;
	private VBoxContainer itemsPanel;
	private VBoxContainer setsPanel;
	private Player player;
	private float updateTimer = 0f;
	private const float UPDATE_INTERVAL = 0.5f;

	public override void _Ready()
	{
		player = GetTree().Root.GetChild(0).FindChild("Player", true, false) as Player;
		if (player == null)
		{
			GD.PrintErr("ChestItemHUD: Could not find Player node!");
			Visible = false;
			return;
		}

		// Create UI hierarchy
		var rootPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(280, 400),
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 0f,
			AnchorBottom = 0f,
			OffsetLeft = 10f,
			OffsetTop = 10f
		};

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.1f, 0.1f, 0.15f, 0.85f),
			BorderColor = new Color(0.4f, 0.3f, 0.2f, 0.9f)
		};
		panelStyle.SetBorderEnabled(Side.Left, true);
		panelStyle.SetBorderEnabled(Side.Top, true);
		panelStyle.SetBorderEnabled(Side.Right, true);
		panelStyle.SetBorderEnabled(Side.Bottom, true);
		panelStyle.SetBorderWidth(Side.Left, 2);
		panelStyle.SetBorderWidth(Side.Top, 2);
		panelStyle.SetBorderWidth(Side.Right, 2);
		panelStyle.SetBorderWidth(Side.Bottom, 2);
		rootPanel.AddThemeStyleboxOverride("panel", panelStyle);

		var vbox = new VBoxContainer();
		rootPanel.AddChild(vbox);

		// Title
		var titleLabel = new Label
		{
			Text = "RELICS",
			CustomMinimumSize = new Vector2(260, 0),
			ThemeFontSizes = { ["font_size"] = 14 }
		};
		vbox.AddChild(titleLabel);

		// Relic count
		relicCountLabel = new Label
		{
			Text = "Owned: 0",
			ThemeFontSizes = { ["font_size"] = 12 }
		};
		vbox.AddChild(relicCountLabel);

		// Owned items section
		var itemsTitle = new Label
		{
			Text = "Items:",
			ThemeFontSizes = { ["font_size"] = 11 }
		};
		vbox.AddChild(itemsTitle);

		itemsPanel = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(260, 120)
		};
		var itemsScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(260, 120),
			VerticalScrollBarMode = ScrollContainer.ScrollBarModeEnum.Auto
		};
		itemsScroll.AddChild(itemsPanel);
		vbox.AddChild(itemsScroll);

		// Separator
		var hsep1 = new HSeparator();
		vbox.AddChild(hsep1);

		// Active sets section
		var setsTitle = new Label
		{
			Text = "Active Sets:",
			ThemeFontSizes = { ["font_size"] = 11 }
		};
		vbox.AddChild(setsTitle);

		setsPanel = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(260, 200)
		};
		var setsScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(260, 200),
			VerticalScrollBarMode = ScrollContainer.ScrollBarModeEnum.Auto
		};
		setsScroll.AddChild(setsPanel);
		vbox.AddChild(setsScroll);

		AddChild(rootPanel);
	}

	public override void _Process(double delta)
	{
		if (player == null)
			return;

		updateTimer += (float)delta;
		if (updateTimer >= UPDATE_INTERVAL)
		{
			updateTimer = 0f;
			RefreshDisplay();
		}
	}

	private void RefreshDisplay()
	{
		// Clear and rebuild owned items
		foreach (var child in itemsPanel.GetChildren())
		{
			child.QueueFree();
		}

		var ownedItems = player.GetOwnedChestItems();
		relicCountLabel.Text = $"Owned: {ownedItems.Count}";

		if (ownedItems.Count == 0)
		{
			var emptyLabel = new Label { Text = "No items yet" };
			itemsPanel.AddChild(emptyLabel);
		}
		else
		{
			// Show owned items in a grid (2 columns)
			var currentRow = new HBoxContainer();
			int columnCount = 0;

			foreach (var itemId in ownedItems)
			{
				var itemName = ChestItemCatalog.GetDisplayName(itemId);
				var iconPath = ChestItemCatalog.GetIconPath(itemId);

				var itemBox = new VBoxContainer();
				itemBox.CustomMinimumSize = new Vector2(120, 80);

				// Icon
				if (ResourceLoader.Exists(iconPath))
				{
					var texture = GD.Load<Texture2D>(iconPath);
					var icon = new TextureRect
					{
						Texture = texture,
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
						CustomMinimumSize = new Vector2(50, 50)
					};
					itemBox.AddChild(icon);
				}

				// Name
				var nameLabel = new Label
				{
					Text = itemName,
					ClipText = true,
					ThemeFontSizes = { ["font_size"] = 9 }
				};
				itemBox.AddChild(nameLabel);

				currentRow.AddChild(itemBox);
				columnCount++;

				if (columnCount >= 2)
				{
					itemsPanel.AddChild(currentRow);
					currentRow = new HBoxContainer();
					columnCount = 0;
				}
			}

			// Add remaining row if not full
			if (columnCount > 0)
			{
				itemsPanel.AddChild(currentRow);
			}
		}

		// Clear and rebuild active sets
		foreach (var child in setsPanel.GetChildren())
		{
			child.QueueFree();
		}

		var completedSets = player.GetCompletedChestSets();
		var allSets = ChestItemCatalog.GetAllSets();
		bool hasAnySets = false;

		foreach (var set in allSets)
		{
			var isCompleted = completedSets.Contains(set.Id);
			var ownedCount = 0;

			foreach (var itemId in set.ComponentItems)
			{
				if (ownedItems.Contains(itemId))
					ownedCount++;
			}

			// Show completed sets prominently
			if (isCompleted)
			{
				hasAnySets = true;
				var setBox = new VBoxContainer();

				var setNameLabel = new Label
				{
					Text = $"✓ {set.DisplayName}",
					ThemeFontSizes = { ["font_size"] = 11 }
				};
				setNameLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
				setBox.AddChild(setNameLabel);

				var setDescLabel = new Label
				{
					Text = set.BonusDescription,
					ClipText = true,
					ThemeFontSizes = { ["font_size"] = 9 },
					CustomMinimumSize = new Vector2(250, 0),
					WordWrapMode = TextServer.WordWrapMode.Word
				};
				setBox.AddChild(setDescLabel);

				setsPanel.AddChild(setBox);
			}
			// Show incomplete sets as progress
			else if (ownedCount > 0)
			{
				hasAnySets = true;
				var progressLabel = new Label
				{
					Text = $"{set.DisplayName}: {ownedCount}/{set.ComponentItems.Length}",
					ThemeFontSizes = { ["font_size"] = 9 }
				};
				progressLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
				setsPanel.AddChild(progressLabel);
			}
		}

		if (!hasAnySets)
		{
			var noSetsLabel = new Label { Text = "Collect items to unlock sets" };
			setsPanel.AddChild(noSetsLabel);
		}
	}
}
