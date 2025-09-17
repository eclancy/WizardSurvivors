extends Control

var transitioned := false

func _input(event):
	if transitioned:
		return
	if (event is InputEventKey and event.pressed) or (event is InputEventMouseButton and event.pressed) or (event is InputEventJoypadButton and event.pressed):
		transitioned = true
		var scene_path = "res://scenes/CharacterSelection.tscn"
		if ResourceLoader.exists(scene_path):
			get_tree().change_scene_to_file(scene_path)
		else:
			push_error("TitleScreen: scene not found: %s" % scene_path)
