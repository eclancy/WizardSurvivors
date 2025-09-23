using Godot;
using System;
using System.Collections.Generic;

public partial class Node2DGame : Node2D
{
	[Export] public int MaxEnemies { get; set; } = 100;
	[Export] public float SpawnMinDistance { get; set; } = 250.0f;
	[Export] public float SpawnMaxDistance { get; set; } = 800.0f;
	[Export] public int SpawnPositionRetries { get; set; } = 8;

	private Node2D? player;
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
		player = GetNode<Node2D>("CharacterBody2D");
		// hide levelup menu if present
		if (HasNode("LevelUpMenu"))
		{
			var lu = GetNode("LevelUpMenu") as CanvasLayer;
			if (lu != null) lu.Hide();
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
