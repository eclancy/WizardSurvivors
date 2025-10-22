using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class Player : CharacterBody2D
{
	[Signal] public delegate void XpGainedEventHandler(int amount);
	[Signal] public delegate void LevelGainedEventHandler();
	[Export] public float Speed { get; set; } = 220f;
	[Export] public PackedScene MagicMissileScene { get; set; }
	[Export] public PackedScene ArcaneExplosionScene { get; set; }
	[Export] public PackedScene SpiritualWeaponScene { get; set; }
	[Export] public float FireInterval { get; set; } = 1.0f;
	[Export] public int StartingXP { get; set; } = 0;
	[Export] public int StartingLevel { get; set; } = 1;
	[Export] public int CurrentXP { get; set; } = 0;
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public int XPToNextLevel { get; set; } = 10;

	[Export] public int MaxHP { get; set; } = 20;
	[Export] public int CurrentHP { get; set; } = 20;
	private ProgressBar hpBar;
	private Dictionary<Node, float> enemyDamageCooldowns = new Dictionary<Node, float>();
	private const float DamageCooldownSeconds = 0.2f; // 12 frames at 60fps
	private HashSet<Node> overlappingEnemies = new HashSet<Node>();




	// Track fire timers for each weapon by WeaponId
	public Dictionary<WeaponId, float> weaponFireTimers = new Dictionary<WeaponId, float>();
	private Vector2 _velocity = Vector2.Zero;

	// Equipped weapons (for demo, start with MagicMissile and ArcaneExplosion)
	public List<Weapon> equippedWeapons = new List<Weapon>();

	public override void _Ready()
	{
		AddToGroup("player");
		_velocity = Vector2.Zero;
		weaponFireTimers.Clear();
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);

		var allWeapons = new Weapon().GetArcaneWeapons();

		// Initial Weapon: Magic Missile
		equippedWeapons.Add(allWeapons.Where(w => w.Id == WeaponId.SpiritualWeapon).First());
		weaponFireTimers[equippedWeapons.Last().Id] = 0f;

		// Create HP bar above player
		hpBar = new ProgressBar();
		hpBar.MinValue = 0;
		hpBar.MaxValue = MaxHP;
		hpBar.Value = CurrentHP;
		hpBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hpBar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		hpBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		hpBar.Position = new Vector2(-32, -48); // Adjust for your sprite size
		hpBar.Size = new Vector2(64, 8);
		AddChild(hpBar);

		var hurtBox = GetNode<Area2D>("HurtBox");
		if (hurtBox != null)
		{
			hurtBox.Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
			hurtBox.Connect("body_exited", new Callable(this, nameof(OnBodyExited)));
		}
	}

	public void TakeDamage(int amount)
	{
		CurrentHP = Math.Max(0, CurrentHP - amount);
		if (hpBar != null)
			hpBar.Value = CurrentHP;
		if (CurrentHP <= 0)
		{
			GD.Print("Player died");
			// TODO: Death logic
		}
	}

	public void Heal(int amount)
	{
		CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
		if (hpBar != null)
			hpBar.Value = CurrentHP;
	}

	private static int CalculateXPForLevel(int level)
	{
		return (level * 10) + (level - 1) * 10;
	}

	public override void _PhysicsProcess(double delta)
	{
		MovePlayer(delta);

		// enemy damage cooldown logic
		// Update cooldown timers

		// Damage-over-time for overlapping enemies
		foreach (var enemy in overlappingEnemies)
		{
			if (!enemyDamageCooldowns.ContainsKey(enemy))
				enemyDamageCooldowns[enemy] = DamageCooldownSeconds; // So damage applies on next tick

			enemyDamageCooldowns[enemy] += (float)delta;
			if (enemyDamageCooldowns[enemy] >= DamageCooldownSeconds)
			{
				TakeDamage(1);
				enemyDamageCooldowns[enemy] = 0f;
			}
		}

		// Weapon firing logic
		if (MagicMissileScene == null || ArcaneExplosionScene == null || SpiritualWeaponScene == null)
		{
			GD.PrintErr("Error: MagicMissileScene or ArcaneExplosionScene or SpiritualWeaponScene is not assigned in the Player script.");
			return;
		}

		foreach (var weapon in equippedWeapons)
		{
			// Calculate interval for this weapon
			float interval = weapon.AttackSpeed > 0 ? (1f / weapon.AttackSpeed) : 1f;
			weaponFireTimers[weapon.Id] += (float)delta;
			if (weaponFireTimers[weapon.Id] >= interval)
			{
				if (weapon.Id == WeaponId.MagicMissile)
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
								var script = missile as MagicMissile;
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
				else if (weapon.Id == WeaponId.ArcaneExplosion)
				{
					GD.Print("Firing Arcane Explosion");

					// Ensure only one ArcaneExplosion follows this player. If not present, create and attach to player.
					bool hasExplosion = GetChildren().OfType<Node>().Any(n => n is ArcaneExplosion);
					if (!hasExplosion)
					{
						var explosion = ArcaneExplosionScene.Instantiate<Area2D>();
						// Attach to player so it follows automatically; set local position to origin
						explosion.Position = Vector2.Zero;
						var script = explosion as ArcaneExplosion;
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
				else if (weapon.Id == WeaponId.SpiritualWeapon)
				{
					GD.Print("Firing Spiritual Weapon");

					// Ensure only one SpiritualWeapon follows this player. If not present, create and attach to player.
					bool hasSpiritualWeapon = GetChildren().OfType<Node>().Any(n => n is SpiritualWeapon);
					if (!hasSpiritualWeapon)
					{
						var spiritualWeapon = SpiritualWeaponScene.Instantiate<Node2D>();
						// Attach to player so it follows automatically; set local position to origin
						spiritualWeapon.Position = Vector2.Zero;
						var script = spiritualWeapon as SpiritualWeapon;
						if (script != null)
						{
							script.Weapon = weapon;
							// Make the explosion follow the player and persist (TotalLifetime = 0 means infinite)
							script.PlayerRef = this;
							//script.TotalLifetime = 0f;
						}
						AddChild(spiritualWeapon);
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

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			overlappingEnemies.Add(body);
			// Optionally, apply damage immediately
			if (!enemyDamageCooldowns.ContainsKey(body) || enemyDamageCooldowns[body] >= DamageCooldownSeconds)
			{
				TakeDamage(1);
				enemyDamageCooldowns[body] = 0f;
			}
		}
	}

	private void OnBodyExited(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			overlappingEnemies.Remove(body);
			enemyDamageCooldowns.Remove(body);
		}
	}
}
