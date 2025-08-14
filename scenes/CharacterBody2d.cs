using System;
using Godot;

public partial class CharacterBody2d : CharacterBody2D
{
	// Exports a variable to the Godot editor, allowing you to change it without editing code.
	[Export]
	public int Speed { get; set; } = 300; // Player movement speed in pixels/second.

	// An internal method for processing Physics
	public override void _PhysicsProcess(double delta)
	{
		var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = direction * Speed;
		MoveAndSlide();
	}
}
