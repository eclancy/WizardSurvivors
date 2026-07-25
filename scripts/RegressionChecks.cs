using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

public static class RegressionChecks
{
	public static List<string> RunAll()
	{
		var warnings = new List<string>();
		ValidateDynamicRewardBounds(warnings);
		ValidateTelemetryHistoryCap(warnings);
		ValidateCharacterRosterBasics(warnings);
		ValidateSaveDefaults(warnings);
		ValidatePlaytestChecklistDefaults(warnings);
		ValidatePresetRewardOrdering(warnings);
		return warnings;
	}

	private static void ValidateDynamicRewardBounds(List<string> warnings)
	{
		var save = new SaveData();
		for (int i = 0; i < 8; i++)
		{
			save.RunTelemetryHistory.Add(new RunTelemetryRecord
			{
				Outcome = i < 4 ? "Defeat" : "Victory",
				TimeSurvived = 180f + (i * 25f),
				EnemiesKilled = 40 + (i * 7)
			});
		}

		var run = new RunResult
		{
			TimeSurvived = 480f,
			EnemiesKilled = 220,
			EquippedSpells = new List<RunSpellSnapshot>
			{
				new() { Id = "magic_missile", DisplayName = "Magic Missile" },
				new() { Id = "fireball", DisplayName = "Fireball" },
				new() { Id = "meteor_swarm", DisplayName = "Meteor Swarm" }
			}
		};

		float multiplier = GlobalStatsManager.GetDynamicArcaneRewardMultiplier(save, run, out _);
		if (multiplier < GlobalStatsManager.DynamicRewardMinMultiplier || multiplier > GlobalStatsManager.DynamicRewardMaxMultiplier)
			warnings.Add($"Dynamic reward multiplier out of bounds: {multiplier:0.000}");
	}

	private static void ValidateTelemetryHistoryCap(List<string> warnings)
	{
		var save = new SaveData();
		for (int i = 0; i < 40; i++)
		{
			save.RecordRunTelemetry(new RunResult
			{
				Outcome = i % 2 == 0 ? RunOutcome.Defeat : RunOutcome.Victory,
				TimeSurvived = 60f + i,
				EnemiesKilled = i
			});
		}

		if (save.RunTelemetryHistory.Count > 25)
			warnings.Add($"Telemetry history exceeded cap: {save.RunTelemetryHistory.Count}");
	}

	private static void ValidateCharacterRosterBasics(List<string> warnings)
	{
		var characters = CharacterRoster.GetAll();
		if (characters.Count == 0)
		{
			warnings.Add("Character roster returned zero entries.");
			return;
		}

		foreach (CharacterData character in characters)
		{
			if (character.StartingSpellResource == null)
				warnings.Add($"Character '{character.Id}' missing starting spell resource.");
		}
	}

	private static void ValidateSaveDefaults(List<string> warnings)
	{
		var save = new SaveData();
		if (!save.EnableGameplayOnboardingTips)
			warnings.Add("New saves should enable onboarding tips by default.");

		if (save.HasToggledOnboardingTipsAtLeastOnce)
			warnings.Add("New saves should start with HasToggledOnboardingTipsAtLeastOnce = false.");

		if (save.PlaytestModeEnabled)
			warnings.Add("New saves should start with PlaytestModeEnabled = false.");

		if (!string.Equals(save.BalancePresetId, GlobalStatsManager.BalancePresetDefault, StringComparison.OrdinalIgnoreCase))
			warnings.Add($"New saves should default to '{GlobalStatsManager.BalancePresetDefault}' preset, got '{save.BalancePresetId}'.");
	}

	private static void ValidatePresetRewardOrdering(List<string> warnings)
	{
		float casual = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetCasual);
		float normal = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetDefault);
		float hardcore = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetHardcore);

		if (!(casual < normal && normal < hardcore))
			warnings.Add($"Preset reward ordering should be Casual < Default < Hardcore, got {casual:0.00}, {normal:0.00}, {hardcore:0.00}.");
	}

	private static void ValidatePlaytestChecklistDefaults(List<string> warnings)
	{
		var save = new SaveData();
		if (save.PlaytestChecklistState == null)
			warnings.Add("Playtest checklist state should default to an empty map.");

		if (save.PlaytestChecklistState != null && save.PlaytestChecklistState.Count != 0)
			warnings.Add($"Playtest checklist defaults should be empty, got {save.PlaytestChecklistState.Count} entries.");
	}
}
