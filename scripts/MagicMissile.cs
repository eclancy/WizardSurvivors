using Godot;
using System;


namespace WizardSurvivors.scripts;

public partial class MagicMissile : Area2D
{
	public Weapon Weapon { get; set; }

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
		if (shape != null && Weapon != null) shape.Radius = Weapon.Area;
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public void Shoot(Vector2 from, Vector2 to, Node enemyTarget = null)
	{
		GlobalPosition = from;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		// Only lock onto a target if it is within Weapon.Range from the firing position
		spawnPosition = from;
		if (enemyTarget is Node2D enemyNode && Weapon != null)
		{
			float distToEnemy = (enemyNode.GlobalPosition - from).Length();
			if (distToEnemy <= Weapon.Range)
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

		// If missile has travelled beyond its Weapon.Range from spawn, drop any target lock
		if (Weapon != null && (GlobalPosition - spawnPosition).Length() > Weapon.Range)
		{
			target = null;
		}
		if (Weapon != null)
		{
			Position += direction * Weapon.Speed * (float)delta;
			lifetime += (float)delta;
			// Ensure the particle trail rotates with the missile
			var particles = GetNodeOrNull<GpuParticles2D>("GPUParticles2D");
			if (particles != null)
			{
				particles.Rotation = Rotation;
			}
			if (Weapon.Duration > 0 && lifetime > Weapon.Duration) QueueFree();
		}
	}

	private void OnAreaEntered(Area2D area)
	{
		if (Weapon != null && area.IsInGroup("enemies") && area.HasMethod("TakeDamage"))
		{
			area.Call("TakeDamage", Weapon.Damage);
			pierceCount++;
			if (pierceCount >= Weapon.Pierce) QueueFree();
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (Weapon != null && body.IsInGroup("enemies") && body.HasMethod("TakeDamage"))
		{
			body.Call("TakeDamage", Weapon.Damage);
			pierceCount++;
			if (pierceCount >= Weapon.Pierce) QueueFree();
		}
	}
}
