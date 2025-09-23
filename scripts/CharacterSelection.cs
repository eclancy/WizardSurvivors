using Godot;
using System;
using System.Collections.Generic;

public partial class CharacterSelection : Control
{
	private List<Godot.Collections.Dictionary> characters = new List<Godot.Collections.Dictionary>()
	{
		new Godot.Collections.Dictionary{{"name","Arcane Mage"},{"unlocked",true},{"starting_spell","Magic Missile"}},
		new Godot.Collections.Dictionary{{"name","Necromancer"},{"unlocked",false},{"starting_spell","Fireball"}},
		new Godot.Collections.Dictionary{{"name","Elementalist"},{"unlocked",false},{"starting_spell","Ice Shard"}},
	};

	public override void _Ready()
	{
		for (int i = 0; i < characters.Count; i++)
		{
			var btn = GetNode<Button>($"CharacterList/{i}");
			if (btn != null)
			{
				btn.Text = characters[i]["name"].ToString() + ((bool)characters[i]["unlocked"] ? "" : " (Locked)");
				btn.Disabled = !(bool)characters[i]["unlocked"];
				int idx = i;
				btn.Pressed += () => OnCharButtonPressed(idx);
			}
		}
	}

	private void OnCharButtonPressed(int idx)
	{
		if ((bool)characters[idx]["unlocked"])
		{
			Global.SelectedCharacterIdx = idx;
			var scenePath = "res://scenes/StageSelection.tscn";
			if (ResourceLoader.Exists(scenePath))
				GetTree().ChangeSceneToFile(scenePath);
			else
				GD.PushError($"CharacterSelection: scene not found: {scenePath}");
		}
	}
}
