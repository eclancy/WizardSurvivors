extends Node

# Run this scene in the editor or play it to validate key resources exist.
# Add it to a temporary scene and run, or add as autoload for quick checks.

var paths := [
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

func _ready():
	print("Resource checker running...")
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

	# Optionally, check ext_resources inside scenes for texture references
	# This is a simple check - for more thorough checks, expand parsing of tscn files.
	get_tree().quit()
