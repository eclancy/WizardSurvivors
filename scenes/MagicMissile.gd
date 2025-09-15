extends Area2D

@export var speed := 400
@export var damage := 10
var direction := Vector2.ZERO
var target = null
var turn_speed := 6.0

func _ready():
	$AnimatedSprite2D.play("default")
	$CollisionShape2D.disabled = false
	connect("area_entered", Callable(self, "_on_area_entered"))
	connect("body_entered", Callable(self, "_on_body_entered"))

func shoot(from: Vector2, to: Vector2, enemy_target = null):
	global_position = from
	direction = (to - from).normalized()
	rotation = direction.angle()
	target = enemy_target

func _process(delta):
	if target and is_instance_valid(target):
		var to_target = (target.global_position - global_position).normalized()
		direction = direction.lerp(to_target, turn_speed * delta).normalized()
		rotation = direction.angle()
	position += direction * speed * delta

func _on_area_entered(area):
	if area.has_method("take_damage"):
		area.take_damage(damage)
		queue_free()

func _on_body_entered(body):
	print("MagicMissile collided with: ", body)
	if body.has_method("take_damage"):
		body.take_damage(damage)
		queue_free()
