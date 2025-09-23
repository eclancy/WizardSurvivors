using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 220f;
	[Export] public PackedScene MagicMissileScene { get; set; }
	[Export] public float FireInterval { get; set; } = 1.0f;
	private float fireTimer = 0f;
	private Vector2 _velocity = Vector2.Zero;

	public override void _PhysicsProcess(double delta)
	{
		var input = Vector2.Zero;
		input.X = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		input.Y = Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up");
		if (input.Length() > 1)
			input = input.Normalized();
		_velocity = input * Speed;
		Velocity = _velocity;
		MoveAndSlide();

		// --- Auto-shoot at nearest enemy ---
		fireTimer += (float)delta;
		if (fireTimer >= FireInterval && MagicMissileScene != null)
		{
			var enemies = GetTree().GetNodesInGroup("enemies");
			if (enemies.Count > 0)
			{
				Node2D nearest = null;
				float minDist = float.MaxValue;
				foreach (var e in enemies)
				{
					if (e is Node2D n2d)
					{
						float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
						if (dist < minDist)
						{
							minDist = dist;
							nearest = n2d;
						}
					}
				}
				if (nearest != null)
				{
					var missile = MagicMissileScene.Instantiate<Node2D>();
					missile.Position = GlobalPosition;
					GetParent().AddChild(missile);
					// If MagicMissile has a Shoot() method, call it:
					var shootMethod = missile.GetType().GetMethod("Shoot");
					if (shootMethod != null)
					{
						shootMethod.Invoke(missile, new object[] { GlobalPosition, nearest.GlobalPosition, nearest });
					}
				}
			}
			fireTimer = 0f;
		}
	}
}
