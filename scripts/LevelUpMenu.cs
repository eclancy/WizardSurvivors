using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);

	public override void _Ready()
	{
		// Pause mode is now set in the .tscn file (pause_mode = process)
	}
	public void SetOptions()
	{
		// Use Weapon.GetArcaneWeapons() for options
		GD.Print("SetOptions called");
		var weaponList = new Weapon().GetArcaneWeapons();
		GD.Print($"Weapon list count: {weaponList.Length}");
		BuildButtonsFrom(weaponList);
	}

	private void OnOptionPressed(WeaponId choice)
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

	private void BuildButtonsFrom(object rawOptions)
	{
		ClearButtons();
		var container = GetOptionsContainer();
		GD.Print($"BuildButtonsFrom: container is {(container != null ? "not null" : "null")}");
		if (container == null)
		{
			GD.PushWarning("LevelUpMenu: no container found for options");
			return;
		}
		// If passed a Weapon[], show up to 3 weapons by Name
		if (rawOptions is Weapon[] weapons)
		{
			int maxShow = Math.Min(3, weapons.Length);
			GD.Print($"BuildButtonsFrom: weapons.Length={weapons.Length}, maxShow={maxShow}");
			for (int i = 0; i < maxShow; i++)
			{
				var weapon = weapons[i];
				GD.Print($"Adding button for weapon: {weapon.Name}");

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
				btn.Text = weapon.Name;
				btn.CustomMinimumSize = new Vector2(0, 48);
				btn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
				btn.Pressed += () => OnOptionPressed(weapon.Id);
				row.AddChild(btn);
				container.AddChild(row);
			}
			return;
		}
		// fallback to original logic if not Weapon[]
		// ...existing code...
	}
}
