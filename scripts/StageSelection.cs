using Godot;
using System;
using System.Collections.Generic;

public partial class StageSelection : Control
{
	private List<Godot.Collections.Dictionary> stages = new List<Godot.Collections.Dictionary>()
	{
		new Godot.Collections.Dictionary{{"name","Enchanted Forest"},{"unlocked",true}},
		new Godot.Collections.Dictionary{{"name","Cursed Castle"},{"unlocked",false}},
		new Godot.Collections.Dictionary{{"name","Mystic Ruins"},{"unlocked",false}},
	};

	public override void _Ready()
	{
		for (int i = 0; i < stages.Count; i++)
		{
			var btn = GetNode<Button>($"StageList/StageButton{i+1}");
			if (btn != null)
			{
				btn.Text = stages[i]["name"].ToString() + ((bool)stages[i]["unlocked"] ? "" : " (Locked)");
				btn.Disabled = !(bool)stages[i]["unlocked"];
				int idx = i;
				btn.Pressed += () => OnStageButtonPressed(idx);
			}
		}
	}

	private void OnStageButtonPressed(int idx)
	{
		if ((bool)stages[idx]["unlocked"])
		{
			Global.SelectedStageIdx = idx;
			var scenePath = "res://scenes/node_2d_game.tscn";
			if (ResourceLoader.Exists(scenePath))
				GetTree().ChangeSceneToFile(scenePath);
			else
				GD.PushError($"StageSelection: scene not found: {scenePath}");
		}
	}
}
