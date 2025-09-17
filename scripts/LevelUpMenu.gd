extends CanvasLayer

signal weapon_selected(idx)

var options = []

func set_options(weapons):
	# Defensive access to scene and nodes
	var node_2d_game = get_tree().current_scene
	var weapon_level_ups = {}
	if node_2d_game != null:
		weapon_level_ups = node_2d_game.get("weapon_level_ups")

	options = weapons if weapons != null else []

	var panel = get_node_or_null("Panel")
	var vbox = panel.get_node_or_null("VBoxContainer") if panel else null
	if vbox == null:
		push_warning("LevelUpMenu: VBoxContainer not found; cannot populate options")
		return

	# Expecting the VBox to have children: Label (title), then 3 buttons
	for i in range(3):
		var btn_index = i + 1
		var btn = null
		if vbox.get_child_count() > btn_index:
			btn = vbox.get_child(btn_index)
		if btn == null:
			continue

		if i < options.size():
			var w = options[i]
			# Safe access to weapon properties
			var name = str(w.get("name", "Unknown"))
			var level = int(w.get("level", 0))

			var upgrade = null
			if name in weapon_level_ups:
				var upgrades = weapon_level_ups.get(name, [])
				if upgrades.size() > 0:
					var upgrade_idx = clamp(level, 0, upgrades.size() - 1)
					upgrade = upgrades[upgrade_idx]

			var stat_text = ""
			if upgrade:
				stat_text += "Next Level Upgrades:\n"
				for stat in upgrade.keys():
					var change = upgrade[stat]
					if change != 0:
						stat_text += "  • " + stat.capitalize() + ": +" + str(change) + "\n"

			btn.text = name + " (Lv. " + str(level + 1) + ")\n" + stat_text
			btn.disabled = false

			# Avoid duplicate connections and closure capture bugs by using Callable with binds
			if not btn.is_connected("pressed", self, "_on_option_pressed"):
				btn.pressed.connect(Callable(self, "_on_option_pressed"), [i])
			if not btn.is_connected("button_down", self, "_on_button_down"):
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
