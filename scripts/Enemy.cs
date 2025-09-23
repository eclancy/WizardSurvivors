using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 100f;
	[Export] public int Health { get; set; } = 20;
	[Export] public string EnemyType { get; set; } = "Enemy";
	[Export] public float RespawnDistance { get; set; } = 1600f;

	private Node2D? player;
	private int maxHealth = 0;
	private PackedScene xpOrbScene = ResourceLoader.Load<PackedScene>("res://scenes/XPOrb.tscn");

	public override void _Ready()
	{
		maxHealth = Health;
		AddToGroup("enemies");
		player = GetParent().GetNodeOrNull<Node2D>("CharacterBody2D");
		SetProcess(true);
		SetPhysicsProcess(true);
	}

	public override void _PhysicsProcess(double delta)
	{
		QueueRedraw();
		if (player != null && IsInstanceValid(player))
		{
			var direction = (player.GlobalPosition - GlobalPosition).Normalized();
			Velocity = direction * Speed;
			MoveAndSlide();
			var dist = player.GlobalPosition.DistanceTo(GlobalPosition);
			if (dist > RespawnDistance)
			{
				var scene = GetTree().CurrentScene as Node;
				if (scene != null && scene.HasMethod("respawn_enemy"))
				{
					scene.CallDeferred("respawn_enemy", this);
				}
				else
				{
					var rng = new RandomNumberGenerator();
					rng.Randomize();
					var angle = rng.Randf() * (Mathf.Pi * 2.0f);
					var radius = rng.RandfRange(250f, 800f);
					var newPos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
					ResetForRespawn(newPos, maxHealth);
				}
			}
		}
	}

	public void TakeDamage(int amount)
	{
		Health -= amount;
		if (Health <= 0)
		{
			if (HasSignal("killed"))
				EmitSignal("killed");
			DropXp();
			QueueFree();
		}
	}

	private void DropXp()
	{
		if (xpOrbScene != null)
		{
			var orb = xpOrbScene.Instantiate<Node2D>();
			orb.GlobalPosition = GlobalPosition;
			var scene = GetTree().CurrentScene as Node;
			if (scene != null && scene.HasMethod("AddXp"))
			{
				var cb = new Callable(scene, "AddXp");
				if (orb.HasSignal("picked_up"))
					orb.Connect("picked_up", cb);
			}
			if (scene != null)
				scene.CallDeferred("add_child", orb);
			else
				GetTree().Root.CallDeferred("add_child", orb);
		}
	}

	public void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		GlobalPosition = newPos;
		Health = newHealth;
		maxHealth = newHealth;
		Velocity = Vector2.Zero;
		QueueRedraw();
	}
}
