using Godot;
using System;

public partial class GameOverScreen : CanvasLayer
{
	private Label? killsLabel;
	private Button? continueButton;

	public override void _Ready()
	{
		killsLabel = GetNode<Label>("Panel/VBoxContainer/KillsLabel");
		continueButton = GetNode<Button>("Panel/VBoxContainer/ContinueButton");
		continueButton.Pressed += OnContinuePressed;
	}

	public void SetKillsCount(int kills)
	{
		if (killsLabel != null)
			killsLabel.Text = $"Enemies killed: {kills}";
	}

	private void OnContinuePressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/TitleScreen.tscn");
	}
}
