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
	[Export] public float Range { get; set; } = 500f;

	private Vector2 direction = Vector2.Zero;
	private Node target = null;
	private float turnSpeed = 6.0f;
	private float lifetime = 0f;
	private int pierceCount = 0;
	private Vector2 spawnPosition = Vector2.Zero;

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

	public void Shoot(Vector2 from, Vector2 to, Node enemyTarget = null)
	{
		GlobalPosition = from;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		// Only lock onto a target if it is within Range from the firing position
		spawnPosition = from;
		if (enemyTarget is Node2D enemyNode)
		{
			float distToEnemy = (enemyNode.GlobalPosition - from).Length();
			if (distToEnemy <= Range)
				target = enemyTarget;
			else
				target = null; // out of range, don't home
		}
		else
		{
			target = null;
		}
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

		// If missile has travelled beyond its Range from spawn, drop any target lock
		if ((GlobalPosition - spawnPosition).Length() > Range)
		{
			target = null;
		}
		Position += direction * Speed * (float)delta;
		lifetime += (float)delta;
		// Ensure the particle trail rotates with the missile
		var particles = GetNodeOrNull<GpuParticles2D>("GPUParticles2D");
		if (particles != null)
		{
			particles.Rotation = Rotation;
		}
		if (Duration > 0 && lifetime > Duration) QueueFree();
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area.IsInGroup("enemies") && area.HasMethod("TakeDamage"))
		{
			area.Call("TakeDamage", Damage);
			pierceCount++;
			if (pierceCount >= Pierce) QueueFree();
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies") && body.HasMethod("TakeDamage"))
		{
			body.Call("TakeDamage", Damage);
			pierceCount++;
			if (pierceCount >= Pierce) QueueFree();
		}
	}
}
