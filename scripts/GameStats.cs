using Godot;
using System;

public partial class GameStats : Node
{
	public int EnemiesKilled { get; set; } = 0;

	public void Reset()
	{
		EnemiesKilled = 0;
	}
}
