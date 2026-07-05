using Godot;
using System;
using System.Collections.Generic;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);

	public override void _Ready()
	{
		// Pause mode is now set in the .tscn file (pause_mode = process)
	}
	public void SetOptions(List<LevelUpOption> options = null)
	{
		GD.Print("SetOptions called");
		BuildButtonsFrom(options ?? new List<LevelUpOption>());
	}

	private void OnOptionPressed(string choice)
	{
		EmitSignal("WeaponSelected", choice.ToString());
		Hide();
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

		int maxShow = Math.Min(3, options.Count);
		GD.Print($"BuildButtonsFrom: options.Count={options.Count}, maxShow={maxShow}");
		for (int i = 0; i < maxShow; i++)
		{
			var option = options[i];
			GD.Print($"Adding button for spell: {option.DisplayName}");

			var row = new VBoxContainer();
			row.CustomMinimumSize = new Vector2(0, 68);
			row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

			var btn = new Button();
			btn.Text = option.GetButtonText();
			btn.CustomMinimumSize = new Vector2(0, 44);
			btn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			btn.Pressed += () => OnOptionPressed(option.SpellId);
			row.AddChild(btn);

			if (!string.IsNullOrWhiteSpace(option.Description))
			{
				var subtitle = new Label();
				subtitle.Text = option.Description;
				subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				row.AddChild(subtitle);
			}

			container.AddChild(row);
		}
	}
}
