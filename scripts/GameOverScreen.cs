using Godot;
using System;

public partial class GameOverScreen : CanvasLayer
{
	private Label titleLabel;
	private Label stageLabel;
	private Label killsLabel;
	private Label timeLabel;
	private Label levelLabel;
	private Label bossLabel;
	private Label loadoutLabel;
	private Label arcaneRewardLabel;
	private Label totalArcaneLabel;
	private Button continueButton;

	public override void _Ready()
	{
		titleLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/Label");
		stageLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/StageLabel");
		killsLabel = GetNode<Label>("Panel/VBoxContainer/KillsLabel");
		timeLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/TimeLabel");
		levelLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/LevelLabel");
		bossLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/BossLabel");
		loadoutLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/LoadoutLabel");
		arcaneRewardLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/ArcaneRewardLabel");
		totalArcaneLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/TotalArcaneLabel");
		continueButton = GetNode<Button>("Panel/VBoxContainer/ContinueButton");
		continueButton.Pressed += OnContinuePressed;
	}

	public void SetRunResult(RunResult result)
	{
		if (result == null)
			return;

		if (titleLabel != null)
			titleLabel.Text = result.Outcome == RunOutcome.Victory ? "Victory" : "Defeat";

		if (stageLabel != null)
			stageLabel.Text = $"Stage: {FormatStageId(result.StageId)}";

		SetKillsCount(result.EnemiesKilled);

		if (timeLabel != null)
			timeLabel.Text = $"Time survived: {FormatTime(result.TimeSurvived)}";

		if (levelLabel != null)
			levelLabel.Text = $"Final level: {result.FinalPlayerLevel}";

		if (bossLabel != null)
		{
			bossLabel.Visible = !string.IsNullOrWhiteSpace(result.BossId);
			bossLabel.Text = $"Boss defeated: {result.BossId}";
		}

		if (loadoutLabel != null)
			loadoutLabel.Text = BuildLoadoutText(result);
	}

	public void SetKillsCount(int kills)
	{
		if (killsLabel != null)
			killsLabel.Text = $"Enemies killed: {kills}";
	}

	public void SetArcaneReward(int runReward, int totalCurrency)
	{
		if (arcaneRewardLabel != null)
			arcaneRewardLabel.Text = $"Arcane Energy earned: +{runReward}";

		if (totalArcaneLabel != null)
			totalArcaneLabel.Text = $"Total Arcane Energy: {totalCurrency}";
	}

	private static string FormatTime(float seconds)
	{
		int totalSeconds = Math.Max(0, Mathf.FloorToInt(seconds));
		int minutes = totalSeconds / 60;
		int remainder = totalSeconds % 60;
		return $"{minutes:00}:{remainder:00}";
	}

	private static string FormatStageId(string stageId)
	{
		if (string.IsNullOrWhiteSpace(stageId))
			return "Unknown";

		return stageId.Replace("_", " ");
	}

	private static string BuildLoadoutText(RunResult result)
	{
		if (result.EquippedSpells == null || result.EquippedSpells.Count == 0)
			return "Final loadout: None";

		var parts = new System.Collections.Generic.List<string>();
		foreach (var spell in result.EquippedSpells)
		{
			string name = string.IsNullOrWhiteSpace(spell.DisplayName) ? spell.Id : spell.DisplayName;
			string legendary = spell.IsLegendary ? " Legendary" : string.Empty;
			parts.Add($"{name} Lv {spell.Level}{legendary}");
		}

		return $"Final loadout: {string.Join(", ", parts)}";
	}

	private void OnContinuePressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}
}
