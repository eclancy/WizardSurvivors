using Godot;
using System;

public partial class MagicMissile : Area2D
{
	[Export] public float FireRate { get; set; } = 1.0f;
	[Export] public int Damage { get; set; } = 10;
	[Export] public float Area { get; set; } = 16.0f;
	[Export] public float Duration { get; set; } = 5.0f;
	[Export] public float Speed { get; set; } = 400f;
	[Export] public int Amount { get; set; } = 1;
	[Export] public int Pierce { get; set; } = 1;

	private Vector2 direction = Vector2.Zero;
	private Node? target = null;
	private float turnSpeed = 6.0f;
	private float lifetime = 0f;
	private int pierceCount = 0;

	public override void _Ready()
	{
		var sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite != null) sprite.Play("default");
		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs != null) cs.Disabled = false;
		var shape = cs?.Shape as CircleShape2D;
		if (shape != null) shape.Radius = Area;
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public void Shoot(Vector2 from, Vector2 to, Node? enemyTarget = null)
	{
		GlobalPosition = from;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		target = enemyTarget;
		lifetime = 0f;
		pierceCount = 0;
	}

	public override void _Process(double delta)
	{
		if (target != null && IsInstanceValid(target))
		{
			if (target is not Node2D targetNode) return;
			var toTarget = targetNode.GlobalPosition - GlobalPosition;
			if (toTarget.Length() > 0)
			{
				var tdir = toTarget.Normalized();
				var alpha = MathF.Min(1f, turnSpeed * (float)delta);
				direction = (direction * (1f - alpha) + tdir * alpha).Normalized();
				Rotation = direction.Angle();
			}
		}
		Position += direction * Speed * (float)delta;
		lifetime += (float)delta;
		if (Duration > 0 && lifetime > Duration) QueueFree();
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area.IsInGroup("enemies") && area.HasMethod("take_damage"))
		{
			area.Call("take_damage", Damage);
			pierceCount++;
			if (pierceCount >= Pierce) QueueFree();
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies") && body.HasMethod("take_damage"))
		{
			body.Call("take_damage", Damage);
			pierceCount++;
			if (pierceCount >= Pierce) QueueFree();
		}
	}
}
