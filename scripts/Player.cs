using Godot;
using System;

public partial class Player : CharacterBody2D
{
	// Signal for XP gained
	// I'm not happy with the name, it's really setting the current XP, not just notifying of gain
	[Signal] public delegate void XpGainedEventHandler(int amount);
	[Signal] public delegate void LevelGainedEventHandler();
	[Export] public float Speed { get; set; } = 220f;
	[Export] public PackedScene MagicMissileScene { get; set; }
	[Export] public float FireInterval { get; set; } = 1.0f;
	[Export] public int StartingXP { get; set; } = 0;
	[Export] public int StartingLevel { get; set; } = 1;
	[Export] public int CurrentXP { get; set; } = 0;
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public int XPToNextLevel { get; set; } = 10;
	private float fireTimer = 0f;
	private Vector2 _velocity = Vector2.Zero;

	public override void _Ready()
	{
		AddToGroup("player");
		_velocity = Vector2.Zero;
		fireTimer = FireInterval; // So we can shoot immediately
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);
	}

	private static int CalculateXPForLevel(int level)
	{
		return (level * 10) + (level - 1) * 10;
	}

	public override void _PhysicsProcess(double delta)
	{
		MovePlayer(delta);

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

	// delta isn't used yet, but it might be needed later
	private void MovePlayer(double delta)
	{
		var input = Vector2.Zero;
		input.X = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		input.Y = Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up");
		if (input.Length() > 1)
			input = input.Normalized();
		_velocity = input * Speed;
		Velocity = _velocity;
		MoveAndSlide();
	}

	public void AddXp(int amount)
	{
		CurrentXP += amount;
		if (CurrentXP >= XPToNextLevel)
		{
			CurrentXP -= CalculateXPForLevel(CurrentLevel); // Reset XP to zero, but keep overflow
			CurrentLevel += 1; // level up
			XPToNextLevel = CalculateXPForLevel(CurrentLevel);
			GD.Print($"Player leveled up to level {CurrentLevel}!");
			// Optionally, you could emit a signal or call a method to open a level-up menu here
			// Open the level-up menu here
			// LevelUpMenu? menu = GetTree().Root.GetNodeOrNull<LevelUpMenu>("LevelUpMenu");
			// if (menu != null)
			// {
			// 	menu.Show();
			// 	menu.SetOptions(); // You can pass specific options if needed
			// }
			EmitSignal(nameof(LevelGained));

		}
		EmitSignal(nameof(XpGained), CurrentXP);
	}
}
