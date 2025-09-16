extends CanvasLayer

signal weapon_selected(idx)

var options = []

func set_options(weapons):
	var node_2d_game = get_tree().current_scene
	var weapon_level_ups = {}
	if node_2d_game and node_2d_game.has_method("has_variable") and node_2d_game.has_variable("weapon_level_ups"):
		weapon_level_ups = node_2d_game.weapon_level_ups
	options = weapons
	for i in range(3):
		var btn = $Panel/VBoxContainer.get_child(i+1)
		if i < options.size():
			var w = options[i]
			var upgrade = null
			if w["name"] in weapon_level_ups:
				var upgrades = weapon_level_ups[w["name"]]
				var upgrade_idx = min(w["level"], upgrades.size()-1)
				upgrade = upgrades[upgrade_idx]
			var stat_text = ""
			if upgrade:
				for stat in upgrade.keys():
					var change = upgrade[stat]
					if change != 0:
						stat_text += stat.capitalize() + ": +" + str(change) + "\n"
			btn.text = w["name"] + " (Lv. " + str(w["level"]+1) + ")\n" + w["desc"] + "\n" + stat_text
			btn.disabled = false
			btn.pressed.connect(func(): _on_option_pressed(i))
			btn.button_down.connect(Callable(self, "_on_button_down"))
		else:
			btn.text = "---"
			btn.disabled = true

func _on_button_down():
	print("do a thing")
	
func _on_option_pressed(idx):
	print("_on_option_pressed")
	emit_signal("weapon_selected", idx)
	queue_free()
