extends Area2D

# Magic Missile stats
@export var fire_rate := 1.0 # shots per second
@export var damage := 10
@export var area := 16.0 # collider radius
@export var duration := 5.0 # seconds, infinite for wand
@export var speed := 400
@export var amount := 1 # projectiles per cast
@export var pierce := 1 # enemies hit before disappearing

var direction := Vector2.ZERO
var target = null
var turn_speed := 6.0
var lifetime := 0.0
var pierce_count := 0

func _ready():
	$AnimatedSprite2D.play("default")
	$CollisionShape2D.disabled = false
	$CollisionShape2D.shape.radius = area
	connect("area_entered", Callable(self, "_on_area_entered"))
	connect("body_entered", Callable(self, "_on_body_entered"))

func shoot(from: Vector2, to: Vector2, enemy_target = null):
	global_position = from
	direction = (to - from).normalized()
	rotation = direction.angle()
	target = enemy_target
	lifetime = 0.0
	pierce_count = 0

func _process(delta):
	if target and is_instance_valid(target):
		var to_target = (target.global_position - global_position).normalized()
		direction = direction.lerp(to_target, turn_speed * delta).normalized()
		rotation = direction.angle()
	position += direction * speed * delta
	lifetime += delta
	if duration > 0 and lifetime > duration:
		queue_free()

func _on_area_entered(area):
	if area.has_method("take_damage"):
		area.take_damage(damage)
		pierce_count += 1
		if pierce_count >= pierce:
			queue_free()

func _on_body_entered(body):
	if body.has_method("take_damage"):
		body.take_damage(damage)
		pierce_count += 1
		if pierce_count >= pierce:
			queue_free()
