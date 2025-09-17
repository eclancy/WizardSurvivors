extends SceneTree

# Runner for headless execution via Godot --script.  
# It performs the same checks as resource_checker.gd but as a SceneTree subclass so Godot accepts it.

func _init():
	var paths = [
		"res://scenes/node_2d_game.tscn",
		"res://scenes/player.tscn",
		"res://scenes/enemy.tscn",
		"res://scenes/MagicMissile.tscn",
		"res://scenes/XPOrb.tscn",
		"res://scenes/LevelUpMenu.tscn",
		"res://scenes/CharacterSelection.tscn",
		"res://scenes/StageSelection.tscn",
		"res://scenes/TitleScreen.tscn",
	]

	print("Resource runner starting...")
	var missing := []
	for p in paths:
		if not ResourceLoader.exists(p):
			missing.append(p)
		else:
			print("OK: " + p)
	if missing.size() > 0:
		print("\nMissing resources:")
		for m in missing:
			print(" - " + m)
	else:
		print("All checked resources exist.")

	# Exit cleanly
	quit()
