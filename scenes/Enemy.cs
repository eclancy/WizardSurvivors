using System;
using Godot;

public partial class Enemy : CharacterBody2D
{
	private Player player;

	// Exports a variable to the Godot editor, allowing you to change it without editing code.
	[Export]
	public int Speed { get; set; } = 100; // Enemy movement speed in pixels/second.

	public override void _Ready()
	{
		// get the player
		player = GetNode<Player>("/root/Node2DGame/CharacterBody2D");
		// then let the game know we're ready
		base._Ready();
	}

	// An internal method for processing Physics
	public override void _PhysicsProcess(double delta)
	{
		var direction = GlobalPosition.DirectionTo(player.GlobalPosition);
		Velocity = direction * Speed;
		MoveAndSlide();
	}
}
