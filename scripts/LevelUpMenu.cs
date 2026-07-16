using Godot;
using System;
using System.Collections.Generic;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);
	[Signal] public delegate void RerollRequestedEventHandler();
	[Signal] public delegate void SkipRequestedEventHandler();
	[Signal] public delegate void SwapRequestedEventHandler(string newSpellId, string removedSpellId);
	private Button rerollButton = null!;
	private Button skipButton = null!;
	private List<LevelUpOption> currentOptions = new();
	private List<EquippedSpellInfo> currentEquippedSpells = new();
	private int currentRerollsRemaining = 0;
	private LevelUpOption pendingSwapOption = null;

	public override void _Ready()
	{
		rerollButton = GetNodeOrNull<Button>("Panel/VBoxContainer/RerollButton");
		if (rerollButton != null)
		{
			rerollButton.Pressed += OnRerollPressed;
		}

		skipButton = GetNodeOrNull<Button>("Panel/VBoxContainer/SkipButton");
		if (skipButton == null)
		{
			// Fallback: create the skip button if the scene doesn't have one yet.
			var parent = GetNodeOrNull<Control>("Panel/VBoxContainer");
			if (parent != null)
			{
				skipButton = new Button();
				skipButton.Name = "SkipButton";
				skipButton.Text = "Skip";
				skipButton.CustomMinimumSize = new Vector2(180, 40);
				parent.AddChild(skipButton);
			}
		}
		if (skipButton != null)
		{
			skipButton.Pressed += OnSkipPressed;
		}

		var viewport = GetViewport();
		if (viewport != null)
		{
			viewport.SizeChanged += OnViewportSizeChanged;
		}
	}

	public void SetOptions(List<LevelUpOption> options = null, int rerollsRemaining = 0, List<EquippedSpellInfo> equippedSpells = null)
	{
		GD.Print("SetOptions called");
		currentOptions = options ?? new List<LevelUpOption>();
		currentEquippedSpells = equippedSpells ?? new List<EquippedSpellInfo>();
		currentRerollsRemaining = rerollsRemaining;
		pendingSwapOption = null;
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
	}

	private void OnViewportSizeChanged()
	{
		if (!Visible)
			return;

		if (pendingSwapOption != null)
		{
			BuildSwapSelectionButtons(pendingSwapOption);
		}
		else
		{
			BuildButtonsFrom(currentOptions);
		}
		UpdateRerollState(currentRerollsRemaining);
	}

	private void OnOptionChosen(LevelUpOption option)
	{
		if (option.RequiresSlotSwap)
		{
			pendingSwapOption = option;
			BuildSwapSelectionButtons(option);
			return;
		}

		EmitSignal(nameof(WeaponSelected), option.SpellId);
		Hide();
	}

	private void OnSwapChoiceChosen(LevelUpOption newOption, EquippedSpellInfo toRemove)
	{
		EmitSignal(nameof(SwapRequested), newOption.SpellId, toRemove.Id);
		Hide();
	}

	private void OnSkipPressed()
	{
		EmitSignal(nameof(SkipRequested));
		Hide();
	}

	private void OnRerollPressed()
	{
		EmitSignal(nameof(RerollRequested));
	}

	private void UpdateRerollState(int rerollsRemaining)
	{
		if (rerollButton == null)
			return;

		rerollButton.Text = $"Reroll ({rerollsRemaining})";
		rerollButton.Disabled = rerollsRemaining <= 0;
	}

	private void ClearButtons()
	{
		var container = GetOptionsContainer();
		if (container == null) return;
		foreach (Node c in container.GetChildren()) c.QueueFree();
	}

	private Control GetOptionsContainer()
	{
		// Always look for Options under Panel/VBoxContainer/Options
		var optionsPath = "Panel/VBoxContainer/Options";
		var node = GetNodeOrNull<Control>(optionsPath);
		GD.Print($"GetOptionsContainer: node at {optionsPath} is {(node != null ? "found" : "not found")}");
		if (node != null) return node;
		// fallback: search recursively
		foreach (Node child in GetChildren())
		{
			var found = FindControlByName(child, "Options");
			if (found != null) return found;
		}
		// fallback: create one if not found
		var fallback = new VBoxContainer();
		fallback.Name = "Options";
		fallback.CustomMinimumSize = new Vector2(300, 120);
		AddChild(fallback);
		return fallback;
	}

	private Control FindControlByName(Node node, string targetName)
	{
		if (node == null) return null;
		if (node is Control c && c.Name == targetName) return c;
		foreach (Node child in node.GetChildren())
		{
			var res = FindControlByName(child, targetName);
			if (res != null) return res;
		}
		return null;
	}

	private void BuildButtonsFrom(List<LevelUpOption> options)
	{
		ClearButtons();
		var container = GetOptionsContainer();
		GD.Print($"BuildButtonsFrom: container is {(container != null ? "not null" : "null")}");
		if (container == null)
		{
			GD.PushWarning("LevelUpMenu: no container found for options");
			return;
		}
		if (options == null || options.Count == 0)
		{
			var label = new Label();
			label.Text = "No upgrades available";
			container.AddChild(label);
			return;
		}

		if (container is GridContainer grid)
		{
			grid.Columns = GetResponsiveColumnCount(options.Count);
		}

		int maxShow = options.Count;
		GD.Print($"BuildButtonsFrom: options.Count={options.Count}, maxShow={maxShow}");
		for (int i = 0; i < maxShow; i++)
		{
			var option = options[i];
			GD.Print($"Adding button for spell: {option.DisplayName}");

			var card = new PanelContainer();
			card.CustomMinimumSize = new Vector2(180, 116);
			card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			var row = new VBoxContainer();
			row.AddThemeConstantOverride("separation", 2);
			card.AddChild(row);

			var btn = new Button();
			btn.Text = option.GetButtonText();
			btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			btn.CustomMinimumSize = new Vector2(0, 52);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			btn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			btn.Pressed += () => OnOptionChosen(option);
			row.AddChild(btn);

			if (!string.IsNullOrWhiteSpace(option.Description))
			{
				var subtitle = new Label();
				subtitle.Text = option.Description;
				subtitle.HorizontalAlignment = HorizontalAlignment.Center;
				subtitle.AddThemeFontSizeOverride("font_size", 12);
				subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				row.AddChild(subtitle);
			}

			string elementPreview = option.GetElementPreviewText();
			if (!string.IsNullOrWhiteSpace(elementPreview))
			{
				var elementLabel = new Label();
				elementLabel.Text = elementPreview;
				elementLabel.HorizontalAlignment = HorizontalAlignment.Center;
				elementLabel.AddThemeFontSizeOverride("font_size", 11);
				elementLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.85f, 1.0f));
				elementLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				row.AddChild(elementLabel);
			}

			container.AddChild(card);
		}
	}

	private int GetResponsiveColumnCount(int optionCount)
	{
		if (optionCount <= 1)
			return 1;

		return Math.Min(2, optionCount);
	}

	private void BuildSwapSelectionButtons(LevelUpOption newOption)
	{
		ClearButtons();
		var container = GetOptionsContainer();
		if (container == null)
			return;

		var label = new Label();
		label.Text = $"Loadout is full - choose a spell to remove for {newOption.DisplayName}:";
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		container.AddChild(label);

		if (container is GridContainer grid)
		{
			grid.Columns = GetResponsiveColumnCount(currentEquippedSpells.Count);
		}

		foreach (var equipped in currentEquippedSpells)
		{
			var btn = new Button();
			btn.Text = $"{equipped.DisplayName} (Lv {equipped.CurrentLevel})";
			btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			btn.CustomMinimumSize = new Vector2(160, 52);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			btn.Pressed += () => OnSwapChoiceChosen(newOption, equipped);
			container.AddChild(btn);
		}
	}
}
