extends Control

var transitioned := false
var music: AudioStreamPlayer = null

func _ready():
	# Add and play title music
	if not has_node("AudioStreamPlayer"):
		music = AudioStreamPlayer.new()
		music.name = "AudioStreamPlayer"
		music.stream = load("res://assets/Pixel_Knights.mp3")
		music.autoplay = false
		music.bus = "Music"
		music.volume_db = linear_to_db(0.2) # Set volume to 20%
		add_child(music)
		music.play()
	else:
		music = $AudioStreamPlayer
	# Use MusicPlayer singleton to play title music
	var music_player = get_node("/root/MusicPlayer")
	music_player.play_music(load("res://assets/Pixel_Knights.mp3"))

func _input(event):
	if transitioned:
		return
	if (event is InputEventKey and event.pressed) or (event is InputEventMouseButton and event.pressed) or (event is InputEventJoypadButton and event.pressed):
		transitioned = true
		# Do NOT stop music here; let it persist through CharacterSelection and StageSelection
		var scene_path = "res://scenes/CharacterSelection.tscn"
		if ResourceLoader.exists(scene_path):
			get_tree().change_scene_to_file(scene_path)
		else:
			push_error("TitleScreen: scene not found: %s" % scene_path)
