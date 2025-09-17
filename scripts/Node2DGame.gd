extends Node2D

var xp: int = 0
var level: int = 1
var xp_to_next: int = 10
var weapons = []
var available_weapons = [
	{"name": "Magic Missile", "desc": "Fires a homing bolt.", "level": 0, "damage": 10},
	{"name": "Fireball", "desc": "Explodes on impact.", "level": 0, "damage": 10},
	{"name": "Ice Shard", "desc": "Slows enemies.", "level": 0, "damage": 10},
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

var xp_counter_label: Label = null

var weapon_level_ups = {
	"Magic Missile": [
		{"damage": 5, "speed": 400, "area": 2, "fire_rate": - 0.1, "amount": 0, "pierce": 1},
		{"damage": 5, "speed": 420, "area": 2, "fire_rate": - 0.1, "amount": 1, "pierce": 1},
		{"damage": 10, "speed": 440, "area": 4, "fire_rate": - 0.2, "amount": 1, "pierce": 1},
		{"damage": 10, "speed": 460, "area": 4, "fire_rate": - 0.2, "amount": 2, "pierce": 2},
		{"damage": 20, "speed": 480, "area": 8, "fire_rate": - 0.3, "amount": 2, "pierce": 2}
	],
	"Fireball": [
		{"damage": 10, "speed": 300, "area": 16, "fire_rate": - 0.1, "amount": 0, "explosion_radius": 32, "burn": 0},
		{"damage": 15, "speed": 310, "area": 20, "fire_rate": - 0.1, "amount": 1, "explosion_radius": 40, "burn": 1},
		{"damage": 20, "speed": 320, "area": 24, "fire_rate": - 0.2, "amount": 1, "explosion_radius": 48, "burn": 2},
		{"damage": 30, "speed": 330, "area": 32, "fire_rate": - 0.2, "amount": 2, "explosion_radius": 56, "burn": 3},
		{"damage": 50, "speed": 340, "area": 40, "fire_rate": - 0.3, "amount": 2, "explosion_radius": 64, "burn": 4}
	],
	"Ice Shard": [
		{"damage": 8, "speed": 400, "area": 12, "fire_rate": - 0.1, "amount": 0, "slow": 0.1, "duration": 2},
		{"damage": 12, "speed": 450, "area": 16, "fire_rate": - 0.1, "amount": 1, "slow": 0.15, "duration": 2.5},
		{"damage": 16, "speed": 500, "area": 20, "fire_rate": - 0.2, "amount": 1, "slow": 0.2, "duration": 3},
		{"damage": 24, "speed": 550, "area": 24, "fire_rate": - 0.2, "amount": 2, "slow": 0.25, "duration": 3.5},
		{"damage": 36, "speed": 600, "area": 32, "fire_rate": - 0.3, "amount": 2, "slow": 0.3, "duration": 4}
	]
}

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
	xp_counter_label = $CanvasLayer2/XPCounter # Adjust path if needed
	update_xp_counter()

func add_xp(amount):
	xp += amount
	update_xp_counter()
	if xp >= xp_to_next:
		xp -= xp_to_next
		level += 1
		xp_to_next = int(xp_to_next * 1.5)
		show_levelup_menu()

func show_levelup_menu():
	print("show_levelup_menu")
	var menu = levelup_menu.instantiate()
	add_child(menu)
	# Set pause_mode on the root Control node of the menu, not CanvasLayer
	#if menu.has_node("Panel"): # Replace "Panel" with your actual root Control node name if different
		#get_tree().paused = true
		#menu.get_node("Panel").pause_mode = true
	menu.connect("weapon_selected", Callable(self, "_on_weapon_selected"))
	menu.set_options(available_weapons)
	get_tree().paused = true # Pause the game when level up menu is shown

func _on_weapon_selected(idx):
	print("_on_weapon_selected")
	get_tree().paused = false # Unpause the game when selection is made
	var weapon = available_weapons[idx]
	weapon["level"] += 1
	if weapon not in weapons:
		weapons.append(weapon)
	# Apply stat upgrades for Magic Missile
	if weapon["name"] == "Magic Missile":
		var upgrades = weapon_level_ups["Magic Missile"]
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
		if dist < min_dist:
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
	fire_interval = max(0.1, 1.0 + weapon.get("fire_rate", 1.0))

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
		xp_counter_label.text = "XP: %d" % xp
