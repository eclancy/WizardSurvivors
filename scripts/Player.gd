extends CharacterBody2D


@export var speed := 300
@export var magic_missile_scene: PackedScene

# Player health and invulnerability
var hit_points := 20
var invulnerable := false
var flash_timer := 0.0
var flash_frames := 4
var flash_frame_count := 0

func _ready():
	add_to_group("player")
	if has_node("AnimatedSprite2D"):
		$AnimatedSprite2D.modulate = Color(1, 1, 1, 1)
	set_process(true)
	set_physics_process(true)
	set_collision_layer(1)
	set_collision_mask(1)
	if has_signal("body_entered"):
		connect("body_entered", Callable(self, "_on_body_entered"))

func _physics_process(_delta):
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
	hit_points -= amount
	invulnerable = true
	flash_timer = 0
	# Optionally, play a sound or trigger a UI update here

func _on_body_entered(body):
	if body.is_in_group("enemies"):
		take_damage(1)
