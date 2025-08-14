using System;
using Godot;

public partial class Player : CharacterBody2D
{
	// Exports a variable to the Godot editor, allowing you to change it without editing code.
	[Export]
	public int Speed { get; set; } = 300; // Player movement speed in pixels/second.

	// An internal method for processing Physics
	public override void _PhysicsProcess(double delta)
	{
		// Input is the built-in keybind system. These are custom strings that, when a key is pressed, will emit this message
		var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = direction * Speed;
		MoveAndSlide();
	}
}
