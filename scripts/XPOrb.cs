using Godot;
using System;

public partial class XPOrb : Area2D
{
	[Export] public int Value { get; set; } = 1;
	[Export] public float AttractDistance { get; set; } = 80f;
	[Export] public float AttractSpeed { get; set; } = 200f;

	private CharacterBody2D? player = null;
	private bool attracted = false;

	public override void _Ready()
	{
		var sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite != null)
		{
			sprite.Scale = new Vector2(0.2f, 0.2f);
			sprite.Play("default");
		}
		var cs = GetNode<CollisionShape2D>("CollisionShape2D");
		if (cs != null)
			cs.Disabled = false;
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public override void _Process(double delta)
	{
		if (player == null)
		{
			var first = GetTree().GetFirstNodeInGroup("player");
			if (first is CharacterBody2D cb) player = cb;
		}
		if (player != null)
		{
			float dynamicAttractDistance = AttractDistance;
			if (player is Player typedPlayer)
			{
				dynamicAttractDistance += typedPlayer.MagnetBonus;
			}

			var dist = GlobalPosition.DistanceTo(player.GlobalPosition);
			if (dist < dynamicAttractDistance) attracted = true;
			if (attracted)
			{
				var dir = (player.GlobalPosition - GlobalPosition).Normalized();
				GlobalPosition += dir * AttractSpeed * (float)delta;
			}
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("player") && body.HasMethod("AddXp"))
		{
			body.Call("AddXp", Value);
			QueueFree();
		}
	}
}
