using Godot;
using System;
using System.Collections.Generic;

public enum RunOutcome
{
	Defeat,
	Victory
}

public class RunSpellSnapshot
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public int Level { get; set; } = 1;
	public bool IsLegendary { get; set; } = false;
}

public class RunResult
{
	public RunOutcome Outcome { get; set; } = RunOutcome.Defeat;
	public string StageId { get; set; } = string.Empty;
	public int FinalPlayerLevel { get; set; } = 1;
	public float TimeSurvived { get; set; } = 0f;
	public int EnemiesKilled { get; set; } = 0;
	public string BossId { get; set; } = string.Empty;
	public bool TookDamageBeforeFiveMinutes { get; set; } = false;
	public Dictionary<string, int> ElementCounts { get; set; } = new();
	public List<RunSpellSnapshot> EquippedSpells { get; set; } = new();
}

public partial class GameStats : Node
{
	public static RunResult LastCompletedRunResult { get; private set; }

	public int EnemiesKilled { get; set; } = 0;
	public RunResult LastRunResult { get; private set; }

	public static void RecordRunResult(RunResult result)
	{
		LastCompletedRunResult = result;
	}

	public void SetLastRunResult(RunResult result)
	{
		RecordRunResult(result);
		LastRunResult = result;
		EnemiesKilled = result?.EnemiesKilled ?? 0;
	}

	public void Reset()
	{
		EnemiesKilled = 0;
		LastRunResult = null;
		LastCompletedRunResult = null;
	}
}
