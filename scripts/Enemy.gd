extends CharacterBody2D

@export var speed := 100
@export var health := 20
@export var xp_orb_scene : PackedScene = preload("res://scenes/XPOrb.tscn")

var player = null
var health_label: Label = null

func _ready():
	add_to_group("enemies")
	player = get_parent().get_node_or_null("CharacterBody2D")
	# Create and add a Label node for health display
	health_label = Label.new()
	health_label.text = str(health)
	health_label.position = Vector2(0, -30) # Adjust above the enemy
	health_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(health_label)
	update_health_label()
	update_sprite_hue()

func _physics_process(_delta):
	if player and is_instance_valid(player):
		var direction = (player.global_position - global_position).normalized()
		velocity = direction * speed
		move_and_slide()

func take_damage(amount):
	health -= amount
	update_health_label()
	update_sprite_hue()
	if health <= 0:
		drop_xp()
		queue_free()
func update_sprite_hue():
	# As health increases, shift hue toward purple (hue 0.8)
	var min_health = 20.0 # base health
	var max_health = 200.0 # adjust as needed for your game
	var t = clamp((health - min_health) / (max_health - min_health), 0.0, 1.0)
	# Green (0.33) to Purple (0.8)
	var hue = lerp(0.33, 0.8, t)
	var _modulate = Color.from_hsv(hue, 1.0, 1.0)
	$AnimatedSprite2D.modulate = _modulate

func update_health_label():
	if health_label:
		health_label.text = str(max(health, 0))

func drop_xp():
	if xp_orb_scene:
		var orb = xp_orb_scene.instantiate()
		orb.global_position = global_position
		# Connect orb pickup to the game scene's add_xp method if present
		if get_tree().current_scene and get_tree().current_scene.has_method("add_xp"):
			var target = get_tree().current_scene
			var cb = Callable(target, "add_xp")
			orb.connect("picked_up", cb)
		get_tree().current_scene.call_deferred("add_child", orb)
