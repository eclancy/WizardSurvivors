extends CanvasLayer

@onready var kills_label = $Panel/VBoxContainer/KillsLabel
@onready var continue_button = $Panel/VBoxContainer/ContinueButton

func _ready():
	continue_button.pressed.connect(_on_continue_pressed)

func set_kills_count(kills: int):
	kills_label.text = "Enemies killed: %d" % kills

func _on_continue_pressed():
	get_tree().change_scene_to_file("res://scenes/TitleScreen.tscn")
