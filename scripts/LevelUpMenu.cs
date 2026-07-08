using Godot;
using System;
using System.Collections.Generic;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);
	[Signal] public delegate void RerollRequestedEventHandler();
	private Button rerollButton = null!;
	private List<LevelUpOption> currentOptions = new();
	private int currentRerollsRemaining = 0;

	public override void _Ready()
	{
		rerollButton = GetNodeOrNull<Button>("Panel/VBoxContainer/RerollButton");
		if (rerollButton != null)
		{
			rerollButton.Pressed += OnRerollPressed;
		}

		var viewport = GetViewport();
		if (viewport != null)
		{
			viewport.SizeChanged += OnViewportSizeChanged;
		}
	}

	public void SetOptions(List<LevelUpOption> options = null, int rerollsRemaining = 0)
	{
		GD.Print("SetOptions called");
		currentOptions = options ?? new List<LevelUpOption>();
		currentRerollsRemaining = rerollsRemaining;
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
	}

	private void OnViewportSizeChanged()
	{
		if (!Visible)
			return;

		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
	}

	private void OnOptionPressed(string choice)
	{
		EmitSignal("WeaponSelected", choice.ToString());
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
			card.CustomMinimumSize = new Vector2(180, 92);
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
			btn.Pressed += () => OnOptionPressed(option.SpellId);
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

			container.AddChild(card);
		}
	}

	private int GetResponsiveColumnCount(int optionCount)
	{
		if (optionCount <= 1)
			return 1;

		return Math.Min(2, optionCount);
	}
}
