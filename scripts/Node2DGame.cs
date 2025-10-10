using Godot;
using System;
using System.Collections.Generic;

public partial class Node2DGame : Node2D
{
	[Export] public int MaxEnemies { get; set; } = 100;
	[Export] public float SpawnMinDistance { get; set; } = 250.0f;
	[Export] public float SpawnMaxDistance { get; set; } = 800.0f;
	[Export] public int SpawnPositionRetries { get; set; } = 8;

	private Player? player;
	private CanvasLayer? levelUpMenu;
	private float fireTimer = 0f;
	private float fireInterval = 1f;
	private float spawnTimer = 0f;
	private float spawnInterval = 2f;
	private float minSpawnInterval = 0.3f;
	private float spawnHealth = 20f;
	private float spawnHealthIncrease = 2f;
	private float spawnIntervalDecrease = 0.05f;
	private float timeElapsed = 0f;

	private PackedScene magicMissileScene = ResourceLoader.Load<PackedScene>("res://scenes/MagicMissile.tscn");
	private PackedScene enemyScene = ResourceLoader.Load<PackedScene>("res://scenes/enemy.tscn");
	private PackedScene levelupMenuScene = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpMenu.tscn");

	public override void _Ready()
	{
		player = GetNode<Player>("CharacterBody2D"); // Strongly typed YES
		player?.Connect("XpGained", new Callable(this, nameof(OnPlayerXpGained)));
		player?.Connect("LevelGained", new Callable(this, nameof(OnPlayerLevelGained)));

		if (HasNode("LevelUpMenu"))
			levelUpMenu = GetNode<CanvasLayer>("LevelUpMenu");
		levelUpMenu?.Hide();
		// Connect to WeaponSelected signal if menu exists at startup
		if (levelUpMenu != null)
		{
			var menuScript = levelUpMenu as Node;
			menuScript?.Connect("WeaponSelected", new Callable(this, nameof(OnWeaponSelected)));
		}
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		fireTimer += d;
		spawnTimer += d;
		timeElapsed += d;
		if (fireTimer >= fireInterval)
		{
			// TODO: call fire logic
			fireTimer = 0f;
		}
		if (spawnTimer >= spawnInterval)
		{
			var currentEnemies = GetTree().GetNodesInGroup("enemies");
			if (currentEnemies.Count < MaxEnemies)
			{
				SpawnEnemy();
			}
			spawnTimer = 0f;
			if (spawnInterval > minSpawnInterval)
				spawnInterval -= spawnIntervalDecrease;
			spawnHealth += spawnHealthIncrease;
		}
	}

	private void OnPlayerXpGained(int amount)
	{
		GD.Print($"Player XP Gained: {amount}");
		var xpCounter = GetNode<ProgressBar>("UIOverlay/XPCounter");
		if (xpCounter != null)
		{
			xpCounter.Value = amount; // or player.CurrentXP if you have access, which we do
			xpCounter.MaxValue = player.XPToNextLevel;
		}
	}

	private void OnPlayerLevelGained()
	{
		GD.Print("Player Level Gained!");
		// Show the LevelUpMenu scene
		if (levelupMenuScene != null)
		{
			levelUpMenu = levelupMenuScene.Instantiate<CanvasLayer>();
			AddChild(levelUpMenu);
			// Connect to WeaponSelected signal
			var menuScript = levelUpMenu as Node;
			menuScript?.Connect("WeaponSelected", new Callable(this, nameof(OnWeaponSelected)));
		}
		if (levelUpMenu != null)
		{
			// Show the menu first
			levelUpMenu.Show();
			// Call SetOptions to populate the menu
			var menuScript = levelUpMenu as Godot.Node;
			var setOptionsMethod = menuScript?.GetType().GetMethod("SetOptions");
			setOptionsMethod?.Invoke(menuScript, null);
			// Pause the game
			GetTree().Paused = true;
		}
	}


	private void OnWeaponSelected(string weaponId)
	{
		// Add the selected weapon to the player
		if (player != null)
		{
			// Find the weapon by id from the available list
			var allWeapons = new WizardSurvivors.scripts.Weapon().GetArcaneWeapons();
			foreach (var w in allWeapons)
			{
				if (w.Id.ToString() == weaponId && !player.equippedWeapons.Exists(ew => ew.Id == w.Id))
				{
					player.equippedWeapons.Add(w);
					player.weaponFireTimers[w.Id] = 0f;
					break;
				}
				else
				{
					GD.Print($"Weapon {w.Name} is already equipped.");
					// Perform level up for that specific weapon
				}
			}
			// Unpause the game and remove the menu
			GetTree().Paused = false;
			if (levelUpMenu != null)
			{
				levelUpMenu.QueueFree();
				levelUpMenu = null;
			}
		}
	}

	private void SpawnEnemy()
	{
		var enemy = enemyScene.Instantiate<Node2D>();
		// compute spawn position around player
		Vector2 pos = new Vector2();
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		if (player != null)
		{
			var angle = rng.Randf() * (Mathf.Pi * 2.0f);
			var radius = rng.RandfRange(SpawnMinDistance, SpawnMaxDistance);
			pos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}
		else
		{
			var screenSize = GetViewportRect().Size;
			pos = new Vector2((float)rng.Randf() * screenSize.X, -50);
		}
		enemy.Position = pos;
		AddChild(enemy);
	}

	public void RespawnEnemy(Node enemy)
	{
		if (player == null) return;
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var angle = rng.Randf() * (Mathf.Pi * 2.0f);
		var radius = rng.RandfRange(SpawnMinDistance, SpawnMaxDistance);
		var newPos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		if (enemy is Node2D n2d && n2d.HasMethod("reset_for_respawn"))
		{
			n2d.CallDeferred("reset_for_respawn", newPos, (int)spawnHealth);
		}
		else if (enemy is Node2D n)
		{
			n.Position = newPos;
		}
	}
}
