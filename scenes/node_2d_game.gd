extends Node2D

var xp : int = 0
var level : int = 1
var xp_to_next : int = 10
var weapons = []
var available_weapons = [
	{"name": "Magic Missile", "desc": "Fires a homing bolt.", "level": 0},
	{"name": "Fireball", "desc": "Explodes on impact.", "level": 0},
	{"name": "Ice Shard", "desc": "Slows enemies.", "level": 0},
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

func _ready():
	player = $CharacterBody2D
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

func add_xp(amount):
	xp += amount
	if xp >= xp_to_next:
		xp -= xp_to_next
		level += 1
		xp_to_next = int(xp_to_next * 1.5)
		show_levelup_menu()

func show_levelup_menu():
	var menu = levelup_menu.instance()
	add_child(menu)
	menu.connect("weapon_selected", self, "_on_weapon_selected")
	menu.set_options(available_weapons)

func _on_weapon_selected(idx):
	var weapon = available_weapons[idx]
	weapon["level"] += 1
	if weapon not in weapons:
		weapons.append(weapon)
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

func has_weapon(weapon_name):
	for w in weapons:
		if w["name"] == weapon_name and w["level"] > 0:
			return true
	return false

func fire_magic_missile():
	var enemies = get_tree().get_nodes_in_group("enemies")
	if enemies.size() == 0:
		return
	var target = enemies[randi() % enemies.size()]
	var missile = magic_missile_scene.instantiate()
	missile.global_position = player.global_position
	missile.target = target
	get_tree().current_scene.add_child(missile)

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
