extends Control

var transitioned := false

func _input(event):
	if transitioned:
		return
	if (event is InputEventKey and event.pressed) or (event is InputEventMouseButton and event.pressed) or (event is InputEventJoypadButton and event.pressed):
		transitioned = true
		get_tree().change_scene_to_file("res://scenes/CharacterSelection.tscn")
