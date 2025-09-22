extends Node2D

@export var text: String = ""
@export var color: Color = Color(1, 1, 1, 1)
@export var duration: float = 0.7
@export var rise_distance: float = 24.0

var label: Label
var start_pos: Vector2
var elapsed: float = 0.0

func _ready():
	label = Label.new()
	label.text = text
	label.modulate = color
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(label)
	start_pos = position
	set_process(true)

func _process(delta):
	elapsed += delta
	# Move up over time
	position.y = start_pos.y - (rise_distance * (elapsed / duration))
	# Fade out
	label.modulate.a = lerp(1.0, 0.0, elapsed / duration)
	if elapsed >= duration:
		queue_free()
