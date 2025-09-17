extends Node

var player: AudioStreamPlayer

func _ready():
	player = AudioStreamPlayer.new()
	add_child(player)
	player.bus = "Music"
	player.volume_db = linear_to_db(0.2)
	player.process_mode = Node.PROCESS_MODE_ALWAYS # Prevent pausing during gameplay

func play_music(stream: AudioStream, restart: bool = false):
	if player.stream != stream or restart:
		player.stop()
		player.stream = stream
		player.play()
	elif not player.playing:
		player.play()

func stop_music():
	player.stop()
