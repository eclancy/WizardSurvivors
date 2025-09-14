extends Area2D

@export var value := 5

func _ready():
	$AnimatedSprite2D.play("default")
	$CollisionShape2D.disabled = false
	self.connect("body_entered", Callable(self, "_on_body_entered"))

func _on_body_entered(body):
	if body.name == "CharacterBody2D":
		body.get_parent().add_xp(value)
		queue_free()
