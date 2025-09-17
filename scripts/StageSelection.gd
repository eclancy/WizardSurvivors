extends Control

# Example stage data, can be loaded from a save file for unlocks
var stages = [
	{"name": "Enchanted Forest", "unlocked": true},
	{"name": "Cursed Castle", "unlocked": false},
	{"name": "Mystic Ruins", "unlocked": false},
]

var selected_stage_idx = -1

func _ready():
	# Set up stage buttons based on unlocks
	for i in range(stages.size()):
		var btn = $StageList.get_child(i)
		btn.text = stages[i]["name"] + ("" if stages[i]["unlocked"] else " (Locked)")
		btn.disabled = not stages[i]["unlocked"]
		btn.pressed.connect(func(): _on_StageButton_pressed(i))

func _on_StageButton_pressed(idx):
	if stages[idx]["unlocked"]:
		Global.selected_stage_idx = idx
		var scene_path = "res://scenes/node_2d_game.tscn"
		if ResourceLoader.exists(scene_path):
			get_tree().change_scene_to_file(scene_path)
		else:
			push_error("StageSelection: scene not found: %s" % scene_path)
