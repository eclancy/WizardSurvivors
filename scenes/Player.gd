extends CharacterBody2D

@export var speed := 300
@export var magic_missile_scene: PackedScene

func _physics_process(_delta):
	var direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	velocity = direction * speed
	move_and_slide()

func shoot_magic_missile():
	var missile = magic_missile_scene.instantiate()
	missile.global_position = global_position
	missile.shoot(global_position, get_global_mouse_position())
	# Optionally set direction or target here, e.g.:
	# missile.direction = Vector2.RIGHT
	get_tree().current_scene.add_child(missile)
# we don't need this, the player doesn't shoot anything.
#func _process(delta):
	#if Input.is_action_just_pressed("shoot"):
		#shoot_magic_missile()
