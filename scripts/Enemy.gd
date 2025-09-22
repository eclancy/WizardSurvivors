extends CharacterBody2D

@export var speed := 100
@export var health := 20
#@export var base_color: Color = Color(1, 1, 1, 1)
@export var enemy_type: String = "Enemy"
@export var xp_orb_scene: PackedScene = preload("res://scenes/XPOrb.tscn")

var player = null
var health_label: Label = null

func _ready():
	add_to_group("enemies")
	# Enemy on layer 2, detects player on layer 1
	set_collision_layer_value(2, true) # Enemy is on layer 2
	set_collision_mask_value(1, true) # Detects layer 1 (player)
	player = get_parent().get_node_or_null("CharacterBody2D")
	# Create and add a Label node for enemy type display
	var type_name_label = Label.new()
	type_name_label.text = enemy_type
	type_name_label.position = Vector2(0, -48) # Above the health label
	type_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(type_name_label)

	# Create and add a Label node for health display
	health_label = Label.new()
	health_label.text = str(health)
	health_label.position = Vector2(0, -30) # Adjust above the enemy
	health_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(health_label)
	update_health_label()
	#update_sprite_hue()

func _physics_process(_delta):
	if player and is_instance_valid(player):
		var direction = (player.global_position - global_position).normalized()
		velocity = direction * speed
		move_and_slide()

func take_damage(amount):
	health -= amount
	update_health_label()
	#update_sprite_hue()
	if health <= 0:
		drop_xp()
		queue_free()

# was doing some stuff to make enemies change color throughout the game
#func update_sprite_hue():
	## Use base_color as the starting color, optionally shift hue with health
	#var sprite = get_node_or_null("AnimatedSprite2D")
	#if sprite:
		#var min_health = 20.0 # base health
		#var max_health = 200.0 # adjust as needed for your game
		#var t = clamp((health - min_health) / (max_health - min_health), 0.0, 1.0)
		## If you want to shift hue, do it relative to base_color
		#var base_r = base_color.r
		#var base_g = base_color.g
		#var base_b = base_color.b
		#var base_a = base_color.a
		##var base_h, base_s, base_v = Color.convert_rgb_to_hsv(base_r, base_g, base_b)
		#var stuff = Color.from_rgba8(base_r, base_g, base_b)
		#var base_h = stuff[0]
		#var base_s = stuff[1]
		#var base_v = stuff[2]
		## Green (0.33) to Purple (0.8) as before, but offset from base_color's hue
		#var hue = lerp(base_h, 0.8, t)
		#var _modulate = Color.from_hsv(hue, base_s, base_v, base_a)
		#sprite.modulate = _modulate

func update_health_label():
	if health_label:
		health_label.text = str(max(health, 0))

func drop_xp():
	if xp_orb_scene:
		var orb = xp_orb_scene.instantiate()
		orb.global_position = global_position
		# Connect orb pickup to the game scene's add_xp method if present
		var scene = get_tree().current_scene
		if scene and scene.has_method("add_xp"):
			var cb = Callable(scene, "add_xp")
			# Only connect if the orb actually defines the "picked_up" signal to avoid runtime errors
			if orb.has_signal("picked_up"):
				orb.connect("picked_up", cb)
		# Add orb to the current scene if available, otherwise fall back to the scene tree root
		if scene:
			scene.call_deferred("add_child", orb)
		else:
			get_tree().root.call_deferred("add_child", orb)
