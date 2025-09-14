extends CharacterBody2D

@export var speed := 100
@export var health := 20
@export var xp_orb_scene : PackedScene = preload("res://scenes/XPOrb.tscn")

var player = null

func _ready():
	add_to_group("enemies")
	player = get_parent().get_node_or_null("CharacterBody2D")

func _physics_process(_delta):
	if player and is_instance_valid(player):
		var direction = (player.global_position - global_position).normalized()
		velocity = direction * speed
		move_and_slide()

func take_damage(amount):
	health -= amount
	if health <= 0:
		drop_xp()
		queue_free()

func drop_xp():
	if xp_orb_scene:
		var orb = xp_orb_scene.instantiate()
		orb.global_position = global_position
		get_parent().add_child(orb)
