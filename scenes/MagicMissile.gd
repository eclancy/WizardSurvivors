extends Area2D

@export var speed := 400
@export var damage := 10
var direction := Vector2.ZERO

func _ready():
	$AnimatedSprite2D.play("default")
	$CollisionShape2D.disabled = false
	connect("area_entered", Callable(self, "_on_area_entered"))

func shoot(from: Vector2, to: Vector2):
	global_position = from
	direction = (to - from).normalized()

func _process(delta):
	position += direction * speed * delta

func _on_area_entered(area):
	if area.has_method("take_damage"):
		area.take_damage(damage)
		queue_free()
