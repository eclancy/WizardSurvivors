using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody2D
{
	[Signal] public delegate void XpGainedEventHandler(int amount);
	[Signal] public delegate void LevelGainedEventHandler();
	[Export] public float Speed { get; set; } = 220f;
	[Export] public PackedScene MagicMissileScene { get; set; }
	[Export] public PackedScene ArcaneExplosionScene { get; set; }
	[Export] public float FireInterval { get; set; } = 1.0f;
	[Export] public int StartingXP { get; set; } = 0;
	[Export] public int StartingLevel { get; set; } = 1;
	[Export] public int CurrentXP { get; set; } = 0;
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public int XPToNextLevel { get; set; } = 10;
	// Track fire timers for each weapon by WeaponId
	public Dictionary<WizardSurvivors.scripts.WeaponId, float> weaponFireTimers = new Dictionary<WizardSurvivors.scripts.WeaponId, float>();
	private Vector2 _velocity = Vector2.Zero;

	// Equipped weapons (for demo, start with MagicMissile and ArcaneExplosion)
	public List<WizardSurvivors.scripts.Weapon> equippedWeapons = new List<WizardSurvivors.scripts.Weapon>();

	public override void _Ready()
	{
		AddToGroup("player");
		_velocity = Vector2.Zero;
		weaponFireTimers.Clear();
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);

		var allWeapons = new WizardSurvivors.scripts.Weapon().GetArcaneWeapons();

		equippedWeapons.Add(allWeapons.Where(w => w.Id == WizardSurvivors.scripts.WeaponId.MagicMissile).First());
		weaponFireTimers[equippedWeapons.Last().Id] = 0f;
	}

	private static int CalculateXPForLevel(int level)
	{
		return (level * 10) + (level - 1) * 10;
	}

	public override void _PhysicsProcess(double delta)
	{
		MovePlayer(delta);


		if (MagicMissileScene == null || ArcaneExplosionScene == null)
		{
			GD.PrintErr("Error: MagicMissileScene or ArcaneExplosionScene is not assigned in the Player script.");
			return;
		}

		foreach (var weapon in equippedWeapons)
		{
			// Calculate interval for this weapon
			float interval = weapon.AttackSpeed > 0 ? (1f / weapon.AttackSpeed) : 1f;
			weaponFireTimers[weapon.Id] += (float)delta;
			if (weaponFireTimers[weapon.Id] >= interval)
			{
				if (weapon.Id == WizardSurvivors.scripts.WeaponId.MagicMissile)
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
							// Only fire if the nearest enemy is within the weapon's range
							if (minDist <= weapon.Range)
							{
								var missile = MagicMissileScene.Instantiate<Area2D>();
								missile.Position = GlobalPosition;
								var script = missile as WizardSurvivors.scripts.MagicMissile;
								if (script != null)
								{
									script.Weapon = weapon;
								}
								GetParent().AddChild(missile);
								var shootMethod = missile.GetType().GetMethod("Shoot");
								if (shootMethod != null)
								{
									shootMethod.Invoke(missile, new object[] { GlobalPosition, nearest.GlobalPosition, nearest });
								}
							}
						}
					}
				}
				else if (weapon.Id == WizardSurvivors.scripts.WeaponId.ArcaneExplosion)
				{
					GD.Print("Firing Arcane Explosion");

					// Ensure only one ArcaneExplosion follows this player. If not present, create and attach to player.
					bool hasExplosion = GetChildren().OfType<Node>().Any(n => n is WizardSurvivors.scripts.ArcaneExplosion);
					if (!hasExplosion)
					{
						var explosion = ArcaneExplosionScene.Instantiate<Area2D>();
						// Attach to player so it follows automatically; set local position to origin
						explosion.Position = Vector2.Zero;
						var script = explosion as WizardSurvivors.scripts.ArcaneExplosion;
						if (script != null)
						{
							script.Weapon = weapon;
							// Make the explosion follow the player and persist (TotalLifetime = 0 means infinite)
							script.PlayerRef = this;
							script.TotalLifetime = 0f;
						}
						AddChild(explosion);
					}

				}
				weaponFireTimers[weapon.Id] = 0f;
			}
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
