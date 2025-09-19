extends CanvasLayer

signal weapon_selected(idx)

var options = []

func _fmt_num(v):
	# Show integers without decimals, floats with 2 decimals
	if typeof(v) == TYPE_INT or (typeof(v) == TYPE_FLOAT and v == int(v)):
		return str(int(v))
	return "%.2f" % v

func set_options(passed_options = null) -> void:
	# Prefer explicit options passed by Node2DGame, otherwise try to read from the current scene
	var node_game = get_tree().current_scene
	var weapon_level_ups = {}
	# Use passed options if valid
	if passed_options and passed_options.size() > 0:
		weapon_level_ups = passed_options
	else:
		# Safe fallback: try a few likely property names on the game scene
		if node_game:
			weapon_level_ups = node_game.get("weapon_level_ups")
			if not weapon_level_ups or weapon_level_ups.size() == 0:
				weapon_level_ups = node_game.get("available_weapons")
	# If still empty, bail out early
	if not weapon_level_ups or weapon_level_ups.size() == 0:
		push_warning("LevelUpMenu: no weapon_level_ups or options provided")
		_clear_buttons()
		return

	# Now build the UI from weapon_level_ups (expects a Dictionary or Array of upgrades)
	_build_buttons_from(weapon_level_ups)

func _on_option_pressed(choice):
	# choice is the weapon key/name bound when the button was created
	print("_on_option_pressed", choice)
	emit_signal("weapon_selected", choice)
	hide()

func _format_upgrade_text(weapon, upgrade):
	if not upgrade:
		return "No upgrade available"
	var parts := []
	if upgrade.has("damage") and upgrade["damage"] != 0:
		var before = weapon["damage"] if weapon.has("damage") else 0
		var after = (before + upgrade["damage"]) if weapon.has("damage") else upgrade["damage"]
		parts.append("Damage: %s → %s" % [_fmt_num(before), _fmt_num(after)])
	if upgrade.has("speed") and upgrade["speed"] != 0:
		var before = weapon["speed"] if weapon.has("speed") else 0
		var after = (before + upgrade["speed"]) if weapon.has("speed") else upgrade["speed"]
		parts.append("Projectile speed: %s px/s → %s px/s" % [_fmt_num(before), _fmt_num(after)])
	if upgrade.has("area") and upgrade["area"] != 0:
		var before = weapon["area"] if weapon.has("area") else 0
		var after = (before + upgrade["area"]) if weapon.has("area") else upgrade["area"]
		parts.append("AOE radius: %s px → %s px" % [_fmt_num(before), _fmt_num(after)])
	if upgrade.has("fire_rate") and upgrade["fire_rate"] != 0:
		var before = weapon["fire_rate"] if weapon.has("fire_rate") else 0
		var delta = upgrade["fire_rate"]
		var after = (before + delta) if weapon.has("fire_rate") else delta
		# Describe direction (faster/slower) based on delta sign
		var note = "(faster)" if delta < 0 else "(slower)"
		parts.append("Fire rate: %s → %s shots/sec %s" % [_fmt_num(before), _fmt_num(after), note])
	if upgrade.has("amount") and upgrade["amount"] != 0:
		var before = weapon["amount"] if weapon.has("amount") else 0
		var after = (before + upgrade["amount"]) if weapon.has("amount") else upgrade["amount"]
		parts.append("Projectiles per cast: %s → %s" % [_fmt_num(before), _fmt_num(after)])
	if upgrade.has("pierce") and upgrade["pierce"] != 0:
		var before = weapon["pierce"] if weapon.has("pierce") else 0
		var after = (before + upgrade["pierce"]) if weapon.has("pierce") else upgrade["pierce"]
		parts.append("Pierce: %s → %s" % [_fmt_num(before), _fmt_num(after)])
	# Handle any additional stats generically
	for stat in upgrade.keys():
		if stat in ["damage", "speed", "area", "fire_rate", "amount", "pierce"]:
			continue
		var before = weapon[stat] if weapon.has(stat) else 0
		var after = (before + upgrade[stat]) if weapon.has(stat) else upgrade[stat]
		parts.append("%s: %s → %s" % [stat.capitalize(), _fmt_num(before), _fmt_num(after)])
	return "\n".join(parts)

# New helper: find the container to place option buttons in
func _get_options_container() -> Control:
	# common node names first
	# direct child lookup
	var node = get_node_or_null("Options")
	if node and node is Control:
		return node
	node = get_node_or_null("OptionList")
	if node and node is Control:
		return node
	# search for the first VBoxContainer child
	for child in get_children():
		if child is VBoxContainer and child is Control:
			return child
		# recursive find by name using a safe helper
		var found = _find_control_by_name(child, "Options")
		if found and found is Control:
			return found
	# If no suitable container is found, create a fallback VBoxContainer named "Options"
	# so callers can always add buttons without producing warnings.
	var fallback := VBoxContainer.new()
	fallback.name = "Options"
	# Give it a minimal size so it is visible; users can reposition in the scene if desired.
	fallback.custom_minimum_size = Vector2(300, 120)
	add_child(fallback)
	return fallback


func _find_control_by_name(node: Node, target_name: String) -> Control:
	# Safe recursive search for a Control node with a given name
	if node == null:
		return null
	if node.name == target_name and node is Control:
		return node
	for c in node.get_children():
		var res = _find_control_by_name(c, target_name)
		if res:
			return res
	return null

# New helper: clear existing option buttons
func _clear_buttons() -> void:
	var container := _get_options_container()
	if not container:
		return
	for c in container.get_children():
		c.queue_free()

# Convert varied formats into a uniform entries array
func _listify_options(raw) -> Array:
	var out := []
	if typeof(raw) == TYPE_DICTIONARY:
		for key in raw.keys():
			var val = raw[key]
			var upgrade = {}
			if typeof(val) == TYPE_ARRAY:
				if val.size() > 0:
					upgrade = val[0]
			elif typeof(val) == TYPE_DICTIONARY:
				# val might be the weapon base; no upgrade => skip
				upgrade = val
			out.append({"name": key, "upgrade": upgrade})
		return out
	elif typeof(raw) == TYPE_ARRAY:
		for item in raw:
			if typeof(item) == TYPE_DICTIONARY:
				if item.has("name") and item.has("upgrade"):
					out.append({"name": item["name"], "upgrade": item["upgrade"]})
				elif item.size() == 1:
					var k = item.keys()[0]
					var v = item[k]
					var up = {}
					if typeof(v) == TYPE_ARRAY and v.size() > 0:
						up = v[0]
					elif typeof(v) == TYPE_DICTIONARY:
						up = v
					out.append({"name": k, "upgrade": up})
				else:
					out.append({"name": "Option", "upgrade": item})
			else:
				out.append({"name": str(item), "upgrade": {}})
		return out
	# fallback: single generic option
	return [ {"name": "Option", "upgrade": {}}]

# Build UI buttons from the normalized options
func _build_buttons_from(raw_options) -> void:
	_clear_buttons()
	var container := _get_options_container()
	if not container:
		push_warning("LevelUpMenu: no container found for options")
		return
	# Set vertical separation between button rows
#	if container is VBoxContainer:
		#container.separation = 8

	var entries = []
	# If raw_options is a dictionary, build entries deterministically from keys
	if typeof(raw_options) == TYPE_DICTIONARY:
		for k in raw_options.keys():
			var v = raw_options[k]
			var upgrade = {}
			if typeof(v) == TYPE_ARRAY and v.size() > 0:
				upgrade = v[0]
			elif typeof(v) == TYPE_DICTIONARY:
				upgrade = v
			entries.append({"name": k, "upgrade": upgrade})
	else:
		entries = _listify_options(raw_options)
	if entries.size() == 0:
		push_warning("LevelUpMenu: no entries to build")
		return

	# choose up to 3 options (shuffle first)
	var rng = RandomNumberGenerator.new()
	rng.randomize()
	for i in range(entries.size()):
		var j = rng.randi_range(0, entries.size() - 1)
		var tmp = entries[i]
		entries[i] = entries[j]
		entries[j] = tmp
	var max_show = min(3, entries.size())

	# try to get weapon baseline info from the running game scene
	var node_game = get_tree().current_scene
	var weapon_bases = {}
	if node_game:
		var aw = node_game.get("available_weapons")
		if aw:
			weapon_bases = aw

	for i in range(max_show):
		var e = entries[i]
		var raw_name = e["name"] if e.has("name") else ("Option %d" % i)
		# prettify names like "MagicMissile" -> "Magic Missile"
		var title = raw_name.replace("_", " ")
		# insert spaces before capital letters if needed
		if title.find(" ") == -1:
			var pretty = ""
			for c in title:
				# String has no is_upper() in some Godot versions; use to_upper/to_lower checks instead.
				# Ensure the character is a letter and is uppercase, then insert a space.
				if c == c.to_upper() and c != c.to_lower() and pretty.length() > 0:
					pretty += " "
				pretty += c
			title = pretty
		var upgrade = e["upgrade"] if e.has("upgrade") else {}
		var weapon_base = {}
		for w in weapon_bases:
			if typeof(w) == TYPE_DICTIONARY and w.values().has(title):
				weapon_base = w
		# format text
		var stat_text = _format_upgrade_text(weapon_base, upgrade)
	# Build an HBox with icon + button so each option shows a weapon icon
		var row = HBoxContainer.new()
		row.custom_minimum_size = Vector2(0, 56)
		row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.size_flags_vertical = Control.SIZE_SHRINK_CENTER

		# try to load an icon matching the weapon name (e.g., Magic_Missile.png)
		var icon_tex = null
		var asset_name = title.replace(" ", "_") + ".png"
		var path = "res://assets/%s" % asset_name
		if ResourceLoader.exists(path):
			icon_tex = load(path)
		var icon = TextureRect.new()
		# fixed icon size
		icon.custom_minimum_size = Vector2(36, 36)
		icon.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		icon.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		if icon_tex:
			icon.texture = icon_tex
		else:
			icon.texture = null
		# add a small margin to separate icon and button
		icon.offset_right = 8
		row.add_child(icon)

		var btn = Button.new()
		btn.text = "%s\n%s" % [title, stat_text]
		btn.expand_icon = false
		btn.size_flags_horizontal = Control.SIZE_FILL
		btn.alignment = HORIZONTAL_ALIGNMENT_CENTER
		btn.custom_minimum_size = Vector2(0, 48)
		# Center the label if present
		var label = container.get_node_or_null("Label")
		if label:
			label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		btn.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		# safe callable with bound weapon key/name
		var cb = Callable(self, "_on_option_pressed").bind(raw_name)
		# avoid duplicate connections
		if btn.is_connected("pressed", cb):
			btn.disconnect("pressed", cb)
		btn.connect("pressed", cb)
		#row.add_child(btn)
		#container.add_child(row)
		container.add_child(btn)
