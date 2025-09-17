extends Node2D

var xp : int = 0
var level : int = 1
var xp_to_next : int = 10
var weapons = []
var available_weapons = [
	{"name": "Magic Missile", "desc": "Fires a homing bolt.", "level": 0, "damage": 10},
	{"name": "Fireball", "desc": "Explodes on impact.", "level": 0, "damage": 10},
	{"name": "Ice Shard", "desc": "Slows enemies.", "level": 0, "damage": 10},
]

var player
var levelup_menu_path := "res://scenes/LevelUpMenu.tscn"
var magic_missile_scene_path := "res://scenes/MagicMissile.tscn"
var enemy_scene_path := "res://scenes/enemy.tscn"

var levelup_menu : PackedScene = null
var magic_missile_scene : PackedScene = null
var enemy_scene : PackedScene = null
var fire_timer = 0.0
var fire_interval = 1.0
var spawn_timer = 0.0
var spawn_interval = 2.0
var min_spawn_interval = 0.3
var spawn_health = 20
var spawn_health_increase = 2
var spawn_interval_decrease = 0.05
var time_elapsed = 0.0

var selected_character_idx = 0
var selected_stage_idx = 0

var xp_counter_label : Label = null

var weapon_level_ups = {
	"Magic Missile": [
		{"damage": 5, "speed": 50, "area": 2, "fire_rate": -0.1, "amount": 0, "pierce": 1},
		{"damage": 5, "speed": 50, "area": 2, "fire_rate": -0.1, "amount": 1, "pierce": 1},
		{"damage": 10, "speed": 100, "area": 4, "fire_rate": -0.2, "amount": 1, "pierce": 1},
		{"damage": 10, "speed": 100, "area": 4, "fire_rate": -0.2, "amount": 2, "pierce": 2},
		{"damage": 20, "speed": 200, "area": 8, "fire_rate": -0.3, "amount": 2, "pierce": 2}
	],
}

func _ready():
	player = $CharacterBody2D

	# Load external scenes/resources safely (avoid hard crash if missing)
	if ResourceLoader.exists(levelup_menu_path):
		levelup_menu = load(levelup_menu_path)
	else:
		push_error("Node2DGame: missing LevelUpMenu scene: %s" % levelup_menu_path)

	if ResourceLoader.exists(magic_missile_scene_path):
		magic_missile_scene = load(magic_missile_scene_path)
	else:
		push_error("Node2DGame: missing MagicMissile scene: %s" % magic_missile_scene_path)

	if ResourceLoader.exists(enemy_scene_path):
		enemy_scene = load(enemy_scene_path)
	else:
		push_error("Node2DGame: missing enemy scene: %s" % enemy_scene_path)
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
	xp_counter_label = $CanvasLayer2/XPCounter # Adjust path if needed
	update_xp_counter()

func add_xp(amount: int):
	xp += amount
	# Check for level up
	while xp >= xp_to_next:
		xp -= xp_to_next
		level += 1
		xp_to_next = int(xp_to_next * 1.5)
		# Open level-up menu if available
		if levelup_menu:
			var menu = levelup_menu.instantiate()
			get_tree().current_scene.add_child(menu)
	update_xp_counter()

func update_xp_counter():
	if xp_counter_label:
		xp_counter_label.text = "XP: %d" % xp

# ...existing code continues ...
