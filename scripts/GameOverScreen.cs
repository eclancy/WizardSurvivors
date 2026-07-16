using Godot;
using System;

public partial class GameOverScreen : CanvasLayer
{
	private Label? killsLabel;
	private Label? arcaneRewardLabel;
	private Label? totalArcaneLabel;
	private Button? continueButton;

	public override void _Ready()
	{
		killsLabel = GetNode<Label>("Panel/VBoxContainer/KillsLabel");
		arcaneRewardLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/ArcaneRewardLabel");
		totalArcaneLabel = GetNodeOrNull<Label>("Panel/VBoxContainer/TotalArcaneLabel");
		continueButton = GetNode<Button>("Panel/VBoxContainer/ContinueButton");
		continueButton.Pressed += OnContinuePressed;
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

	private void OnContinuePressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}
}
