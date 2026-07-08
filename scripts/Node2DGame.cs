using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class Node2DGame : Node2D
{
	[Export] public int MaxEnemies { get; set; } = 100;
	[Export] public float SpawnMinDistance { get; set; } = 250.0f;
	[Export] public float SpawnMaxDistance { get; set; } = 800.0f;
	[Export] public int SpawnPositionRetries { get; set; } = 8;
	[Export] public int BaseArcaneReward { get; set; } = 20;
	[Export] public int ArcanePerMinuteSurvived { get; set; } = 8;
	[Export] public int ArcanePerPlayerLevel { get; set; } = 2;

	private Player? player;
	private CanvasLayer? levelUpMenu;
	private float fireTimer = 0f;
	private float fireInterval = 1f;
	private float spawnTimer = 0f;
	private float spawnInterval = 2f;
	private float minSpawnInterval = 0.2f;
	private float spawnHealth = 20f;
	private float spawnHealthIncrease = 2f;
	private float spawnIntervalDecrease = 0.02f;
	private float timeElapsed = 0f;
	private int totalEnemiesSpawned = 0;
	private bool runFinished = false;
	private int rerollsRemainingForCurrentLevelUp = 0;

	private PackedScene magicMissileScene = ResourceLoader.Load<PackedScene>("res://scenes/MagicMissile.tscn");
	private PackedScene enemyScene = ResourceLoader.Load<PackedScene>("res://scenes/enemy.tscn");
	private PackedScene levelupMenuScene = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpMenu.tscn");
	private PackedScene gameOverScene = ResourceLoader.Load<PackedScene>("res://scenes/GameOverScreen.tscn");

	public override void _Ready()
	{
		player = GetNode<Player>("CharacterBody2D"); // Strongly typed YES
		player?.Connect("XpGained", new Callable(this, nameof(OnPlayerXpGained)));
		player?.Connect("LevelGained", new Callable(this, nameof(OnPlayerLevelGained)));
		player?.Connect("Died", new Callable(this, nameof(OnPlayerDied)));

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
		if (runFinished)
			return;

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

		var background = GetNode<TextureRect>("CanvasLayer/Background");
		var camera = player.GetNode<Camera2D>("Camera2D");
		if (background.Material is ShaderMaterial material && camera != null)
		{
			var textureSize = background.Texture.GetSize();
			Vector2 offset = camera.GlobalPosition / textureSize;
			material.SetShaderParameter("scroll_offset", offset);
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
			menuScript?.Connect("RerollRequested", new Callable(this, nameof(OnRerollRequested)));
		}
		if (levelUpMenu != null)
		{
			// Show the menu first
			levelUpMenu.Show();
			rerollsRemainingForCurrentLevelUp = player?.RerollsPerLevelUp ?? 0;
			if (levelUpMenu is LevelUpMenu typedMenu && player != null)
			{
				typedMenu.SetOptions(player.GetLevelUpOptions(), rerollsRemainingForCurrentLevelUp);
			}
			else
			{
				var menuScript = levelUpMenu as Godot.Node;
				var setOptionsMethod = menuScript?.GetType().GetMethod("SetOptions");
				setOptionsMethod?.Invoke(menuScript, null);
			}
			// Pause the game
			GetTree().Paused = true;
		}
	}

	private void OnRerollRequested()
	{
		if (player == null || levelUpMenu is not LevelUpMenu typedMenu)
			return;

		if (rerollsRemainingForCurrentLevelUp <= 0)
			return;

		rerollsRemainingForCurrentLevelUp--;
		typedMenu.SetOptions(player.GetLevelUpOptions(), rerollsRemainingForCurrentLevelUp);
	}


	private void OnWeaponSelected(string weaponId)
	{
		if (player == null)
			return;

		bool changed = player.TryAddOrLevelSpell(weaponId);
		if (!changed)
		{
			GD.PrintErr($"Could not add or level spell for selection '{weaponId}'.");
		}

		// Unpause the game and remove the menu
		GetTree().Paused = false;
		if (levelUpMenu != null)
		{
			levelUpMenu.QueueFree();
			levelUpMenu = null;
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
		totalEnemiesSpawned++;
	}

	private void OnPlayerDied()
	{
		FinishRunAndReward();
	}

	public void FinishRunAndReward()
	{
		if (runFinished)
			return;

		runFinished = true;

		int reward = CalculateArcaneReward();
		int totalCurrency = AwardArcaneEnergy(reward);
		ShowGameOver(reward, totalCurrency);
		GetTree().Paused = true;
	}

	private int CalculateArcaneReward()
	{
		int minutesSurvived = Mathf.FloorToInt(timeElapsed / 60.0f);
		int playerLevel = player?.CurrentLevel ?? 1;
		int baseReward = BaseArcaneReward + (minutesSurvived * ArcanePerMinuteSurvived) + (playerLevel * ArcanePerPlayerLevel);

		float rewardMultiplier = 1.0f;
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null && saveManager.Data.ArcaneUpgradeLevels.TryGetValue("arcane_resonance", out int resonanceLevel))
		{
			rewardMultiplier += resonanceLevel * 0.10f;
		}

		if (saveManager != null && saveManager.Data.ArcaneUpgradeLevels.TryGetValue("greed", out int greedLevel))
		{
			rewardMultiplier += greedLevel * 0.10f;
		}

		int computed = Mathf.RoundToInt(baseReward * rewardMultiplier);
		return Math.Max(1, computed);
	}

	private int AwardArcaneEnergy(int amount)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
		{
			GD.PrintErr("SaveManager autoload not found. Arcane Energy reward was not persisted.");
			return 0;
		}

		saveManager.Data.TotalCurrency += amount;
		saveManager.SaveGame();
		return saveManager.Data.TotalCurrency;
	}

	private void ShowGameOver(int reward, int totalCurrency)
	{
		if (gameOverScene == null)
		{
			GD.PrintErr("GameOverScreen scene could not be loaded.");
			return;
		}

		var overlay = gameOverScene.Instantiate<CanvasLayer>();
		AddChild(overlay);

		if (overlay is GameOverScreen gameOver)
		{
			int activeEnemies = GetTree().GetNodesInGroup("enemies").Count;
			int killsEstimate = Math.Max(0, totalEnemiesSpawned - activeEnemies);
			gameOver.SetKillsCount(killsEstimate);
			gameOver.SetArcaneReward(reward, totalCurrency);
		}
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
