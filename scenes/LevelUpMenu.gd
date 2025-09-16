extends CanvasLayer

signal weapon_selected(idx)

var options = []

func set_options(weapons):
	print("set_options")
	options = weapons
	for i in range(3):
		var btn = $Panel/VBoxContainer.get_child(i+1)
		if i < options.size():
			btn.text = options[i]["name"] + " (Lv. " + str(options[i]["level"]+1) + ")\n" + options[i]["desc"]
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
