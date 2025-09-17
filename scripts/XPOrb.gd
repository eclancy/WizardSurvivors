extends Area2D

signal picked_up(amount: int)

@export var speed := 150
var player = null

func _ready():
	# Find the player in the current scene by group or node name
	player = get_tree().current_scene.get_node_or_null("CharacterBody2D")
	if not player:
		player = get_tree().current_scene.get_node_or_null("Player")
	$AnimatedSprite2D.play()

func _physics_process(delta):
	if player and is_instance_valid(player):
		var dir = (player.global_position - global_position)
		if dir.length() < 8:
			# emit a signal so the game manager can award XP
			emit_signal("picked_up", 1)
			queue_free()
		else:
			global_position += dir.normalized() * speed * delta
