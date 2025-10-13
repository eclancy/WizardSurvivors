using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	private Vector2 knockbackVelocity = Vector2.Zero;
	private float knockbackTime = 0f;
	private const float KnockbackDuration = 0.45f;
	[Export] public float Speed { get; set; } = 100f;
	[Export] public int Health { get; set; } = 20;
	[Export] public string EnemyType { get; set; } = "Enemy";
	[Export] public float RespawnDistance { get; set; } = 1600f;

	private Node2D? player;
	private int maxHealth = 0;
	private PackedScene floatingTextScene = ResourceLoader.Load<PackedScene>("res://scenes/FloatingText.tscn");
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
		if (knockbackTime > 0f)
		{
			float decayThreshold = KnockbackDuration * 0.3f;
			if (knockbackTime > decayThreshold)
			{
				// First 70%: constant velocity
				Velocity = knockbackVelocity;
			}
			else
			{
				// Last 30%: linearly decay velocity to zero
				float t = knockbackTime / decayThreshold; // t goes from 1 to 0
				Velocity = knockbackVelocity * t;
			}
			knockbackTime -= (float)delta;
			if (knockbackTime <= 0f)
			{
				knockbackVelocity = Vector2.Zero;
				knockbackTime = 0f;
			}
		}
		else if (player != null && IsInstanceValid(player))
		{
			var direction = (player.GlobalPosition - GlobalPosition).Normalized();
			Velocity = direction * Speed;
		}
		MoveAndSlide();
		if (player != null && IsInstanceValid(player))
		{
			var dist = player.GlobalPosition.DistanceTo(GlobalPosition);
			if (dist > RespawnDistance)
			{
				var scene = GetTree().CurrentScene as Node;
				if (scene != null && scene.HasMethod("respawn_enemy"))
				{
					scene.CallDeferred("respawn_enemy", this);
				}
			}
		}
	}
	public void ApplyKnockback(Vector2 force)
	{
		knockbackVelocity = force;
		knockbackTime = KnockbackDuration;
	}
	// 	var rng = new RandomNumberGenerator();
	// 	rng.Randomize();
	// 					var angle = rng.Randf() * (Mathf.Pi * 2.0f);
	// 	var radius = rng.RandfRange(250f, 800f);
	// 	var newPos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	// 	ResetForRespawn(newPos, maxHealth);
	// }
	// 			}
	// 		}
	// 	}

	public void TakeDamage(int amount)
	{
		// Show floating damage text
		if (floatingTextScene != null)
		{
			var textNode = floatingTextScene.Instantiate<Node2D>();
			if (textNode is FloatingText ft)
			{
				ft.Text = amount.ToString();
				ft.Color = new Color(1, 0.2f, 0.2f, 1); // Red for damage
				ft.GlobalPosition = this.GlobalPosition;
			}
			else
			{
				textNode.Set("Text", amount.ToString());
				textNode.Set("Color", new Color(1, 0.2f, 0.2f, 1));
				textNode.Set("GlobalPosition", this.GlobalPosition);
			}
			// Add to the enemy's parent so it persists after enemy is freed
			GetParent().AddChild(textNode);
		}
		Health -= amount;
		if (Health <= 0)
		{
			if (HasSignal("killed"))
				EmitSignal("killed");
			DropXp();
			// Defer freeing so FloatingText can show up for at least one frame
			CallDeferred("queue_free");
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
