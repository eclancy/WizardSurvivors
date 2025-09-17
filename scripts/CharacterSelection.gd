extends Control

# Example character data, can be loaded from a save file for unlocks
var characters = [
	{"name": "Arcane Mage", "unlocked": true, "starting_spell": "Magic Missile"},
	{"name": "Necromancer", "unlocked": false, "starting_spell": "Fireball"},
	{"name": "Elementalist", "unlocked": false, "starting_spell": "Ice Shard"},
]

var selected_character_idx = -1

func _ready():
	# Set up character buttons based on unlocks
	for i in range(characters.size()):
		var btn = $CharacterList.get_child(i)
		btn.text = characters[i]["name"] + ("" if characters[i]["unlocked"] else " (Locked)")
		btn.disabled = not characters[i]["unlocked"]
		btn.pressed.connect(func(): _on_CharButton_pressed(i))

func _on_CharButton_pressed(idx):
	if characters[idx]["unlocked"]:
		Global.selected_character_idx = idx
		var scene_path = "res://scenes/StageSelection.tscn"
		if ResourceLoader.exists(scene_path):
			get_tree().change_scene_to_file(scene_path)
		else:
			push_error("CharacterSelection: scene not found: %s" % scene_path)
