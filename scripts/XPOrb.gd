extends Area2D

@export var value := 5
@export var attract_distance := 80.0
@export var attract_speed := 200.0

signal picked_up(amount: int)

@export var speed := 150

var player: CharacterBody2D = null
var attracted := false

func _ready():
	$AnimatedSprite2D.scale = Vector2(0.2, 0.2) # Scale to 50%
	$AnimatedSprite2D.play("default")
	$CollisionShape2D.disabled = false
	self.connect("body_entered", Callable(self, "_on_body_entered"))

func _process(delta):
	if not player:
		player = get_tree().get_first_node_in_group("player")
	if player:
		var dist = global_position.distance_to(player.global_position)
		if dist < attract_distance:
			attracted = true
		if attracted:
			var direction = (player.global_position - global_position).normalized()
			global_position += direction * attract_speed * delta

func _on_body_entered(body):
	if body.is_in_group("player"):
		var game = get_tree().current_scene
		if game and game.has_method("add_xp"):
			game.add_xp(value)
		queue_free()


#func _physics_process(delta):
	#if player and is_instance_valid(player):
		#var dir = (player.global_position - global_position)
		#if dir.length() < 8:
			## emit a signal so the game manager can award XP
			#emit_signal("picked_up", 1)
			#queue_free()
		#else:
			#global_position += dir.normalized() * speed * delta
