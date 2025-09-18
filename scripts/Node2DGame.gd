extends Node2D

var xp: int = 0
var level: int = 1
var xp_to_next: int = 10
var weapons = []
var available_weapons = [
	{"name": "Magic Missile", "desc": "Fires a homing bolt.", "level": 0, "damage": 10, "speed": 400, "area": 16, "fire_rate": 0.2, "amount": 1, "pierce": 1},
	{"name": "Fireball", "desc": "Explodes on impact.", "level": 0, "damage": 12, "speed": 300, "area": 20, "fire_rate": 1.2, "amount": 1, "pierce": 0, "explosion_radius": 32, "burn": 0},
	{"name": "Ice Shard", "desc": "Slows enemies.", "level": 0, "damage": 8, "speed": 400, "area": 12, "fire_rate": 1.0, "amount": 1, "pierce": 0, "slow": 0.1, "duration": 2},
]

var player
var levelup_menu = preload("res://scenes/LevelUpMenu.tscn")
var magic_missile_scene = preload("res://scenes/MagicMissile.tscn")
var enemy_scene = preload("res://scenes/enemy.tscn")
var fire_timer = 0.0
var fire_interval = 1.0
var spawn_timer = 0.0
var spawn_interval = 2.0
var min_spawn_interval = 0.3
var spawn_health = 20
var spawn_health_increase = 2
var spawn_interval_decrease = 0.05
var time_elapsed = 0.0

# Add global singleton for passing character/stage selection
# In Godot, this is usually done via autoload, but for now, use a workaround
var selected_character_idx = 0
var selected_stage_idx = 0


# Targeting range for weapons and visual indicator
var targeting_range := 300.0
var xp_counter_label: ProgressBar = null
var _xp_tween = null

# Ensure Node2DGame exposes the upgrade tables
@export var weapon_level_ups := {
	"MagicMissile": [
		{"damage": 5},            # level 1 upgrade
		{"fire_rate": -0.1},      # level 2 upgrade (negative = faster)
		{"amount": 1},            # level 3 upgrade (extra projectiles)
	],
	"Fireball": [
		{"damage": 8},
		{"area": 6},
		{"pierce": 1},
	],
	"IceShard": [
		{"damage": 4},
		{"speed": 80},
		{"duration": 1.5},
	]
}

func _ready():
	player = $CharacterBody2D
	# Play background music using MusicPlayer singleton
	if has_node("/root/MusicPlayer"):
		get_node("/root/MusicPlayer").play_music(load("res://assets/background_music.mp3"))
	# Remove initial enemies
	for child in get_children():
		if child is CharacterBody2D and child != player:
			child.queue_free()
	# Give starting spell to player based on selected character
	var characters = [
		{"name": "Arcane Mage", "unlocked": true, "starting_spell": "Magic Missile"},
		{"name": "Necromancer", "unlocked": false, "starting_spell": "Fireball"},
		{"name": "Elementalist", "unlocked": false, "starting_spell": "Ice Shard"},
	]
	var starting_spell = characters[Global.selected_character_idx]["starting_spell"]
	for w in available_weapons:
		if w["name"] == starting_spell:
			w["level"] = 1
			weapons.append(w)
	xp_counter_label = $CanvasLayer2/XPCounter # ProgressBar node
	# initialize bar
	if xp_counter_label:
		xp_counter_label.min_value = 0
		xp_counter_label.max_value = xp_to_next
		xp_counter_label.value = xp
	update_xp_counter()

	# Ensure a LevelUpMenu node is present and connected
	var lu_node = null
	if has_node("LevelUpMenu"):
		lu_node = $LevelUpMenu
	else:
		lu_node = levelup_menu.instantiate()
		add_child(lu_node)
		lu_node.name = "LevelUpMenu"

	# always hide menu at start (it will be shown when leveling up)
	if lu_node:
		lu_node.hide()

	# connect the selection signal to the game handler if not already connected
	if lu_node and lu_node.has_signal("weapon_selected"):
		# use a metadata flag to avoid calling is_connected on the node (CanvasLayer doesn't expose it reliably)
		if not lu_node.has_meta("connected_to_game") or not lu_node.get_meta("connected_to_game"):
			lu_node.connect("weapon_selected", Callable(self, "_on_weapon_selected"))
			lu_node.set_meta("connected_to_game", true)

func add_xp(amount):
	var prev_xp = xp
	xp += amount
	# Level up check (support multi-level gaps)
	while xp >= xp_to_next:
		xp -= xp_to_next
		level += 1
		xp_to_next = int(xp_to_next * 1.5)
		show_levelup_menu()
	# animate the bar from previous xp to new xp
	_animate_xp_change(prev_xp, xp, xp_to_next)

func show_levelup_menu():
	if not $LevelUpMenu:
		return
	# pass weapon_level_ups directly so the menu uses the game data
	$LevelUpMenu.set_options(weapon_level_ups)
	$LevelUpMenu.show()
	get_tree().paused = true # Pause the game when level up menu is shown

func _on_weapon_selected(choice):
	print("_on_weapon_selected", choice)
	get_tree().paused = false # Unpause the game when selection is made
	# choice may be a name (string) or an index (int)
	var weapon = null
	if typeof(choice) == TYPE_STRING:
		# find matching weapon in available_weapons by name (case-insensitive approx)
		for w in available_weapons:
			if w.has("name") and w["name"].to_lower() == choice.replace("_", "").to_lower():
				weapon = w
				break
	elif typeof(choice) == TYPE_INT:
		if choice >= 0 and choice < available_weapons.size():
			weapon = available_weapons[choice]
	if not weapon:
		push_warning("_on_weapon_selected: could not resolve choice: %s" % str(choice))
		return
	weapon["level"] += 1
	if weapon not in weapons:
		weapons.append(weapon)
	# Apply stat upgrades: look up upgrades by canonical name if present
	var canonical = weapon["name"].replace(" ", "")
	if weapon_level_ups.has(canonical):
		var upgrades = weapon_level_ups[canonical]
		var upgrade_idx = min(weapon["level"] - 1, upgrades.size() - 1)
		var upgrade = upgrades[upgrade_idx]
		for stat in upgrade.keys():
			if stat in weapon:
				weapon[stat] += upgrade[stat]
			else:
				weapon[stat] = upgrade[stat]
	# Optionally, add logic to spawn weapon node or upgrade


func _process(delta):
	fire_timer += delta
	spawn_timer += delta
	time_elapsed += delta
	if has_weapon("Magic Missile") and fire_timer >= fire_interval:
		fire_magic_missile()
		fire_timer = 0.0
	if spawn_timer >= spawn_interval:
		spawn_enemy()
		spawn_timer = 0.0
		# Increase difficulty
		if spawn_interval > min_spawn_interval:
			spawn_interval -= spawn_interval_decrease
		spawn_health += spawn_health_increase
	queue_redraw()

# Draw a circle around the player to show the targeting range
func _draw():
	if player:
		draw_arc(player.global_position, targeting_range, 0, TAU, 64, Color(0.5, 0.5, 1.0, 0.8), 3.0)

func has_weapon(weapon_name):
	for w in weapons:
		if w["name"] == weapon_name and w["level"] > 0:
			return true
	return false


# Helper function to find the closest enemy to a given position
func get_closest_enemy(pos: Vector2) -> Node:
	var enemies = get_tree().get_nodes_in_group("enemies")
	var closest_enemy = null
	var min_dist = INF
	for enemy in enemies:
		var dist = pos.distance_to(enemy.global_position)
		if dist <= targeting_range and dist < min_dist:
			min_dist = dist
			closest_enemy = enemy
	return closest_enemy

func fire_magic_missile():
	var weapon = null
	for w in weapons:
		if w["name"] == "Magic Missile":
			weapon = w
			break
	if weapon == null:
		return
	var enemies = get_tree().get_nodes_in_group("enemies")
	if enemies.size() == 0:
		return
	# Fire 'amount' missiles
	for i in range(weapon.get("amount", 1)):
		var closest_enemy = get_closest_enemy(player.global_position)
		if closest_enemy == null:
			return
		var missile = magic_missile_scene.instantiate()
		missile.global_position = player.global_position
		missile.damage = weapon.get("damage", 10)
		missile.speed = weapon.get("speed", 400)
		missile.area = weapon.get("area", 16.0)
		missile.duration = weapon.get("duration", 5.0)
		missile.pierce = weapon.get("pierce", 1)
		missile.shoot(player.global_position, closest_enemy.global_position, closest_enemy)
		get_tree().current_scene.add_child(missile)
	# Adjust fire rate
	fire_interval = max(0.1, 1.0 + weapon.get("fire_rate", 0.0))

# Add similar firing functions for Fireball and Ice Shard
#func fire_fireball():
	#var weapon = null
	#for w in weapons:
		#if w["name"] == "Fireball":
			#weapon = w
			#break
	#if weapon == null:
		#return
	#var enemies = get_tree().get_nodes_in_group("enemies")
	#if enemies.size() == 0:
		#return
	#for i in range(weapon.get("amount", 1)):
		#var closest_enemy = get_closest_enemy(player.global_position)
		#if closest_enemy == null:
			#return
		#var fireball = preload("res://scenes/Fireball.tscn").instantiate()
		#fireball.global_position = player.global_position
		#fireball.damage = weapon.get("damage", 10)
		#fireball.speed = weapon.get("speed", 400)
		#fireball.area = weapon.get("area", 16.0)
		#fireball.duration = weapon.get("duration", 5.0)
		#fireball.explosion_radius = weapon.get("explosion_radius", 32)
		#fireball.burn = weapon.get("burn", 0)
		#fireball.shoot(player.global_position, closest_enemy.global_position, closest_enemy)
		#get_tree().current_scene.add_child(fireball)
#
#func fire_ice_shard():
	#var weapon = null
	#for w in weapons:
		#if w["name"] == "Ice Shard":
			#weapon = w
			#break
	#if weapon == null:
		#return
	#var enemies = get_tree().get_nodes_in_group("enemies")
	#if enemies.size() == 0:
		#return
	#for i in range(weapon.get("amount", 1)):
		#var closest_enemy = get_closest_enemy(player.global_position)
		#if closest_enemy == null:
			#return
		#var ice_shard = preload("res://scenes/IceShard.tscn").instantiate()
		#ice_shard.global_position = player.global_position
		#ice_shard.damage = weapon.get("damage", 10)
		#ice_shard.speed = weapon.get("speed", 400)
		#ice_shard.area = weapon.get("area", 16.0)
		#ice_shard.duration = weapon.get("duration", 5.0)
		#ice_shard.slow = weapon.get("slow", 0.1)
		#ice_shard.slow_duration = weapon.get("duration", 2)
		#ice_shard.shoot(player.global_position, closest_enemy.global_position, closest_enemy)
		#get_tree().current_scene.add_child(ice_shard)

func spawn_enemy():
	var enemy = enemy_scene.instantiate()
	# Spawn at random edge of the screen
	var margin = 50
	var screen_size = get_viewport_rect().size
	var side = randi() % 4
	var pos = Vector2()
	if side == 0:
		pos = Vector2(randf() * screen_size.x, -margin) # Top
	elif side == 1:
		pos = Vector2(randf() * screen_size.x, screen_size.y + margin) # Bottom
	elif side == 2:
		pos = Vector2(-margin, randf() * screen_size.y) # Left
	else:
		pos = Vector2(screen_size.x + margin, randf() * screen_size.y) # Right
	enemy.global_position = pos
	if enemy.has_method("set_health"):
		enemy.set_health(spawn_health)
	elif "health" in enemy:
		enemy.health = spawn_health
	add_child(enemy)

func update_xp_counter():
	if xp_counter_label:
		# ensure the bar's max matches the current xp_to_next
		xp_counter_label.max_value = xp_to_next
		# value is animated elsewhere; set immediately as fallback
		xp_counter_label.value = xp


func _animate_xp_change(from_val: int, to_val: int, current_xp_to_next: int) -> void:
	if not xp_counter_label:
		return
	# stop existing tween if present
	if _xp_tween and _xp_tween.is_valid():
		_xp_tween.kill()
		_xp_tween = null

	# ensure max is up-to-date for the animation target
	xp_counter_label.max_value = current_xp_to_next

	# clamp values to bar range
	var start = clamp(from_val, 0, current_xp_to_next)
	var target = clamp(to_val, 0, current_xp_to_next)

	xp_counter_label.value = start

	# short animation duration based on delta
	var duration = max(0.15, abs(target - start) * 0.05)

	_xp_tween = get_tree().create_tween()
	_xp_tween.tween_property(xp_counter_label, "value", target, duration).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
