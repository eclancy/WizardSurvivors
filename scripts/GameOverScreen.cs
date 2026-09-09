using Godot;
using System;
using System.Linq;
using WizardSurvivors.scripts;

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
		ApplyFantasyGuiSkin();
		SfxPlayer.Global(SfxCatalog.GameOver);
		continueButton.Pressed += OnContinuePressed;
	}

	private void ApplyFantasyGuiSkin()
	{
		Panel panel = GetNodeOrNull<Panel>("Panel");
		if (panel != null)
		{
			var style = new StyleBoxFlat();
			style.BgColor = new Color(0f, 0f, 0f, 0.7f);
			style.SetCornerRadiusAll(8);
			panel.AddThemeStyleboxOverride("panel", style);
		}
		FantasyGuiSkin.StyleButton(continueButton, FantasyGuiSkin.GlyphPlay);
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
			bossLabel.Text = $"Boss defeated: {FormatBossName(result.BossId)}{FormatBossUnlockSuffix(result.BossId)}";
		}

		if (loadoutLabel != null)
			loadoutLabel.Text = $"{BuildLoadoutText(result)}\n{BuildTelemetrySummaryText(result)}";
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

	// The raw BossId is a save-facing key ("forest_treant"), not something to show a player.
	private static string FormatBossName(string bossId)
	{
		var definition = WizardSurvivors.scripts.BossCatalog.GetById(bossId);
		return definition != null ? definition.DisplayName : FormatStageId(bossId);
	}

	// Telling the player what the kill opened is the point of gating a stage behind a boss; a
	// victory screen that stays silent about it makes the lock feel arbitrary.
	private static string FormatBossUnlockSuffix(string bossId)
	{
		var definition = WizardSurvivors.scripts.BossCatalog.GetById(bossId);
		if (definition == null || string.IsNullOrWhiteSpace(definition.UnlocksStageId))
			return string.Empty;

		return $"\nUnlocked: {StageDisplayName(definition.UnlocksStageId)}";
	}

	private static string StageDisplayName(string stageId)
	{
		return stageId switch
		{
			"stage_0" => "Enchanted Forest",
			"stage_1" => "Cursed Castle",
			"stage_2" => "Mystic Ruins",
			_ => FormatStageId(stageId)
		};
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

	private static string BuildTelemetrySummaryText(RunResult result)
	{
		float minutes = Math.Max(0.0167f, result.TimeSurvived / 60.0f);
		float dps = result.TotalDamageDealt / minutes;
		float avgHit = result.HitsTaken > 0 ? (float)result.TotalDamageTaken / result.HitsTaken : 0f;

		string topPickedSpell = GetTopSpell(result.SpellPickCounts);
		string topUpgradedSpell = GetTopSpell(result.SpellUpgradeCounts);

		string rewardModel = result.ArcaneRewardMultiplier > 0f
			? $"Reward x{result.ArcaneRewardMultiplier:0.00}" + (string.IsNullOrWhiteSpace(result.ArcaneRewardBreakdown) ? string.Empty : $" ({result.ArcaneRewardBreakdown})")
			: string.Empty;

		return $"Telemetry: Damage dealt {result.TotalDamageDealt} ({dps:0} per min), damage taken {result.TotalDamageTaken} across {result.HitsTaken} hits (avg {avgHit:0.0}), level-ups {result.LevelUpsGained}, rerolls {result.RerollsUsed}, swaps {result.SwapsUsed}, removals {result.RemovalsUsed}, skips {result.SkipsUsed}. Picks: {topPickedSpell}. Upgrades: {topUpgradedSpell}. {rewardModel}";
	}

	private static string GetTopSpell(System.Collections.Generic.Dictionary<string, int> counts)
	{
		if (counts == null || counts.Count == 0)
			return "none";

		var top = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First();
		return $"{top.Key} x{top.Value}";
	}

	private void OnContinuePressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}
}
