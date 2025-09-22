extends CharacterBody2D
# Track enemies in HurtBox and their timers
var hurtbox_enemies := {}
const HURTBOX_DAMAGE_INTERVAL := 1.0


@export var speed := 300
@export var magic_missile_scene: PackedScene

# Player health and invulnerability
var hit_points := 20
var max_hit_points := 20
var invulnerable := false
var flash_timer := 0.0
var flash_frames := 4
var flash_frame_count := 0

func _ready():
	queue_redraw() # Ensure _draw is called at start
	add_to_group("player")
	if has_node("AnimatedSprite2D"):
		$AnimatedSprite2D.modulate = Color(1, 1, 1, 1)
	set_process(true)
	set_physics_process(true)
	# Set collision layers/masks so player and enemies can interact
	# Player on layer 1, detects enemies on layer 2
	set_collision_layer_value(1, true) # Player is on layer 1
	set_collision_mask_value(2, true) # Detects layer 2 (enemies)
	# Connect Hurtbox.body_entered for damage detection if Hurtbox exists
	if has_node("HurtBox"):
		$HurtBox.body_entered.connect(_on_hurtbox_body_entered)
		$HurtBox.body_exited.connect(_on_hurtbox_body_exited)

func _physics_process(_delta):
	queue_redraw() # Redraw health bar every frame

	# Update timers for enemies in HurtBox
	var to_damage := []
	for enemy in hurtbox_enemies.keys():
		if not is_instance_valid(enemy):
			to_damage.append(enemy)
			continue
		hurtbox_enemies[enemy] += _delta
		if hurtbox_enemies[enemy] >= HURTBOX_DAMAGE_INTERVAL:
			to_damage.append(enemy)
	for enemy in to_damage:
		if is_instance_valid(enemy) and enemy.is_in_group("enemies"):
			take_damage(1)
			hurtbox_enemies[enemy] = 0.0
		else:
			hurtbox_enemies.erase(enemy)
# Draw health bar above the player
func _draw():
	var bar_width = 40
	var bar_height = 6
	var bar_offset = Vector2(-bar_width / 2.0, -40)
	var hp_ratio = float(hit_points) / float(max_hit_points)
	var bg_rect = Rect2(bar_offset, Vector2(bar_width, bar_height))
	var fg_rect = Rect2(bar_offset, Vector2(bar_width * hp_ratio, bar_height))
	# Soft white border (glow)
	var border_pad = 2.0
	var border_rect = Rect2(bar_offset - Vector2(border_pad, border_pad), Vector2(bar_width + 2 * border_pad, bar_height + 2 * border_pad))
	draw_rect(border_rect, Color(1, 1, 1, 0.35), true)
	# Health bar background and foreground
	draw_rect(bg_rect, Color(0.2, 0.2, 0.2, 0.8), true)
	draw_rect(fg_rect, Color(0.2, 0.9, 0.2, 0.9), true)
	# Black border
	draw_rect(bg_rect, Color(0, 0, 0, 1), false, 1.5)
	var direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	velocity = direction * speed
	move_and_slide()

	# Handle invulnerability and flashing
	if invulnerable:
		flash_timer += 1
		if has_node("AnimatedSprite2D"):
			# Alternate between white and normal color
			if int(flash_timer) % 2 == 0:
				$AnimatedSprite2D.modulate = Color(1, 1, 1, 1)
			else:
				$AnimatedSprite2D.modulate = Color(1, 1, 1, 0.5)
		if flash_timer >= flash_frames:
			invulnerable = false
			flash_timer = 0
			if has_node("AnimatedSprite2D"):
				$AnimatedSprite2D.modulate = Color(1, 1, 1, 1)

func shoot_magic_missile():
	var missile = magic_missile_scene.instantiate()
	missile.global_position = global_position
	missile.shoot(global_position, get_global_mouse_position())
	# Optionally set direction or target here, e.g.:
	# missile.direction = Vector2.RIGHT
	get_tree().current_scene.add_child(missile)

# Damage and collision logic
func take_damage(amount: int):
	if invulnerable:
		return
	hit_points = max(0, hit_points - amount)
	invulnerable = true
	flash_timer = 0
	queue_redraw() # Redraw health bar
	if hit_points == 0:
		# Notify parent game node of player death
		var parent = get_parent()
		if parent and parent.has_method("on_player_death"):
			parent.on_player_death()

func _on_body_entered(body):
	if body.is_in_group("enemies"):
		take_damage(1)

func _on_hurtbox_body_entered(body):
	if body.is_in_group("enemies"):
		take_damage(1)
		hurtbox_enemies[body] = 0.0

func _on_hurtbox_body_exited(body):
	if body in hurtbox_enemies:
		hurtbox_enemies.erase(body)
