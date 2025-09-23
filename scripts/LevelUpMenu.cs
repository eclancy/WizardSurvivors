using Godot;
using System;
using System.Collections.Generic;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);

	public override void _Ready()
	{
	}

	public void SetOptions(object? passedOptions = null)
	{
		// For now, mirror GDScript: try to get weapon_level_ups from current scene
		var scene = GetTree().CurrentScene as Node;
	object? weaponLevelUps = null;
		if (passedOptions != null)
			weaponLevelUps = passedOptions;
		else if (scene != null && scene.HasMethod("get"))
		{
			weaponLevelUps = scene.Call("get", "weapon_level_ups");
			if (weaponLevelUps == null || (weaponLevelUps is Godot.Collections.Dictionary && ((Godot.Collections.Dictionary)weaponLevelUps).Count == 0))
			{
				weaponLevelUps = scene.Call("get", "available_weapons");
			}
		}
		if (weaponLevelUps == null)
		{
			GD.PushWarning("LevelUpMenu: no weapon_level_ups or options provided");
			ClearButtons();
			return;
		}
		BuildButtonsFrom(weaponLevelUps);
	}

	private void OnOptionPressed(string choice)
	{
		EmitSignal("WeaponSelected", choice);
		Hide();
	}

	private void ClearButtons()
	{
		var container = GetOptionsContainer();
		if (container == null) return;
		foreach (Node c in container.GetChildren()) c.QueueFree();
	}

	private Control? GetOptionsContainer()
	{
		var node = GetNodeOrNull<Control>("Options");
		if (node != null) return node;
		node = GetNodeOrNull<Control>("OptionList");
		if (node != null) return node;
		foreach (Node child in GetChildren())
		{
			if (child is VBoxContainer vc) return vc;
			var found = FindControlByName(child, "Options");
			if (found != null) return found;
		}
		var fallback = new VBoxContainer();
		fallback.Name = "Options";
		fallback.CustomMinimumSize = new Vector2(300, 120);
		AddChild(fallback);
		return fallback;
	}

	private Control? FindControlByName(Node node, string targetName)
	{
		if (node == null) return null;
		if (node is Control c && node.Name == targetName) return c;
		foreach (Node child in node.GetChildren())
		{
			var res = FindControlByName(child, targetName);
			if (res != null) return res;
		}
		return null;
	}

	private void BuildButtonsFrom(object? rawOptions)
	{
		ClearButtons();
		var container = GetOptionsContainer();
		if (container == null)
		{
			GD.PushWarning("LevelUpMenu: no container found for options");
			return;
		}
		// Minimal: if rawOptions is a Dictionary, iterate its keys
		var entries = new System.Collections.Generic.List<(string name, Godot.Collections.Dictionary upgrade)>();
		if (rawOptions is Godot.Collections.Dictionary dict)
		{
			foreach (var k in dict.Keys)
			{
				var v = dict[k];
				var up = new Godot.Collections.Dictionary();
				// dict values are dynamic/Variant-like; avoid C# pattern matching and use casts with fallbacks
				try
				{
					var arr = (Godot.Collections.Array)v;
					if (arr != null && arr.Count > 0)
					{
						up = (Godot.Collections.Dictionary)arr[0];
					}
				}
				catch
				{
					try { up = (Godot.Collections.Dictionary)v; } catch { }
				}
				entries.Add((k.ToString(), up));
			}
		}
		// show up to 3
		int maxShow = Math.Min(3, entries.Count);
		var rng = new RandomNumberGenerator(); rng.Randomize();
		for (int i = 0; i < maxShow; i++)
		{
			var e = entries[i];
			var rawName = !string.IsNullOrEmpty(e.name) ? e.name : $"Option {i}";
			var title = rawName.Replace("_", " ");
			var upgrade = e.upgrade ?? new Godot.Collections.Dictionary();
			var row = new HBoxContainer();
			row.CustomMinimumSize = new Vector2(0, 56);
			row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			var icon = new TextureRect();
			icon.CustomMinimumSize = new Vector2(36, 36);
			icon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(icon);
			var btn = new Button();
			btn.Text = title;
			btn.CustomMinimumSize = new Vector2(0, 48);
			btn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			// capture a local copy so each button keeps its own name
			string nameCopy = rawName;
			btn.Pressed += () => OnOptionPressed(nameCopy);
			row.AddChild(btn);
			container.AddChild(row);
		}
	}
}
