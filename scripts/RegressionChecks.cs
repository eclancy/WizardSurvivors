using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
		ValidateSpellEvolutionCoverage(warnings);
		ValidateChestSetPresentation(warnings);
		ValidateBossCatalog(warnings);
		ValidateRangedEnemies(warnings);
		return warnings;
	}

	// Scenes carrying a RangedEnemy script. There is no catalog for ordinary enemies - Node2DGame
	// holds them as fields - so this list is maintained by hand the same way SpellResourcePaths is.
	private static readonly string[] RangedEnemyScenePaths =
	{
		"res://scenes/CultistEnemy.tscn",
		"res://scenes/SkullSentry.tscn",
	};

	// A ranged enemy is the first one whose behaviour depends on a second scene resolving at
	// runtime. Every way this breaks is silent: a missing bolt scene, or a wind-up of zero, both
	// produce an enemy that still walks and still looks fine, so nobody finds out until a player
	// wonders why the caster never does anything - or takes a hit with no tell at all. A dotnet
	// build sees none of it.
	private static void ValidateRangedEnemies(List<string> warnings)
	{
		foreach (string path in RangedEnemyScenePaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				warnings.Add($"Ranged enemy scene '{path}' is missing, so it would never spawn.");
				continue;
			}

			var scene = GD.Load<PackedScene>(path);
			if (scene == null)
			{
				warnings.Add($"Ranged enemy scene '{path}' failed to load.");
				continue;
			}

			Node probe = scene.Instantiate();
			if (probe is not RangedEnemy ranged)
			{
				warnings.Add($"Ranged enemy scene '{path}' has no RangedEnemy script attached, so it would chase and melee like every other enemy.");
				probe?.Free();
				continue;
			}

			// The three ranges only mean anything as an ordered set. Out of order, the enemy either
			// backs away from a spot it is also trying to reach, or holds station outside the range
			// it can shoot from - both read as an enemy that has lost interest in the fight.
			if (ranged.RetreatDistance >= ranged.StandoffDistance || ranged.StandoffDistance >= ranged.AttackRange)
			{
				warnings.Add($"Ranged enemy '{path}' has ranges out of order (retreat {ranged.RetreatDistance}, standoff {ranged.StandoffDistance}, attack {ranged.AttackRange}); they must increase in that order.");
			}

			if (ranged.CastWindUpSeconds <= 0f)
				warnings.Add($"Ranged enemy '{path}' has no cast wind-up, so its shot arrives with no tell the player can read.");

			if (ranged.BoltsPerVolley < 1)
				warnings.Add($"Ranged enemy '{path}' fires {ranged.BoltsPerVolley} bolts per volley, so it would telegraph and shoot nothing.");

			// Stacked bolts are the one way a volley can be dishonest: several bolts on the exact
			// same heading look like one shot, land as one hit, and quietly multiply its damage.
			if (ranged.BoltsPerVolley > 1 && ranged.VolleySpreadDegrees <= 0f)
				warnings.Add($"Ranged enemy '{path}' fires {ranged.BoltsPerVolley} bolts with no spread, so they overlap into what looks like a single shot dealing several times its listed damage.");

			ValidateEnemyProjectile(warnings, path, ranged.ProjectileScenePath);
			ranged.Free();
		}
	}

	private static void ValidateEnemyProjectile(List<string> warnings, string ownerPath, string projectilePath)
	{
		if (string.IsNullOrWhiteSpace(projectilePath) || !ResourceLoader.Exists(projectilePath))
		{
			warnings.Add($"Ranged enemy '{ownerPath}' points at a missing projectile scene: '{projectilePath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(projectilePath);
		if (scene == null)
		{
			warnings.Add($"Projectile scene '{projectilePath}' failed to load, so '{ownerPath}' would telegraph and then fire nothing.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not EnemyProjectile bolt)
		{
			warnings.Add($"Projectile scene '{projectilePath}' has no EnemyProjectile script attached.");
			probe?.Free();
			return;
		}

		// The bolt finds the player through its collision shape; without one it passes straight
		// through and the caster is decorative.
		if (bolt.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null)
			warnings.Add($"Projectile scene '{projectilePath}' has no CollisionShape2D, so its bolts would pass through the player.");

		bolt.Free();
	}

	// A boss is the only way to win a level, so a broken entry silently costs the player the ending
	// of that stage - the run would just keep going past the timer with nothing to kill. None of
	// this is checkable from a dotnet build: the scene path and its script only resolve in Godot.
	private static void ValidateBossCatalog(List<string> warnings)
	{
		foreach (BossDefinition boss in BossCatalog.All)
		{
			int stageIndex = BossCatalog.StageIndexOf(boss);

			if (string.IsNullOrWhiteSpace(boss.Id))
				warnings.Add($"Boss for stage {stageIndex} has no Id, so its victory cannot be recorded or matched to an achievement.");

			ValidateBossScene(warnings, boss);

			if (boss.Health <= 0)
				warnings.Add($"Boss '{boss.Id}' has non-positive Health, so it would die on spawn.");

			// AchievementDefinitions matches boss ids by substring ("forest", "castle", "ruins").
			// An id that matches nothing still wins the level but silently grants no spell.
			if (!string.IsNullOrWhiteSpace(boss.Id)
				&& !AchievementDefinitions.All.Any(a => !string.IsNullOrWhiteSpace(a.SpellUnlockId) && a.IsComplete(BuildVictoryProbe(boss.Id))))
			{
				warnings.Add($"Boss '{boss.Id}' matches no achievement, so defeating it unlocks no spell.");
			}

			if (!string.IsNullOrWhiteSpace(boss.UnlocksStageId) && !boss.UnlocksStageId.StartsWith("stage_", StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Boss '{boss.Id}' unlocks '{boss.UnlocksStageId}', which is not a stage id of the form 'stage_N'.");
		}
	}

	// Actually builds the boss once. A .tscn that exists but has the wrong script - or no script -
	// still passes ResourceLoader.Exists and then fails at minute fifteen, where nobody is watching.
	// Instantiate does not run _Ready (that needs the tree), so this is a cheap structural probe.
	private static void ValidateBossScene(List<string> warnings, BossDefinition boss)
	{
		if (string.IsNullOrWhiteSpace(boss.ScenePath) || !ResourceLoader.Exists(boss.ScenePath))
		{
			warnings.Add($"Boss '{boss.Id}' has a missing scene: '{boss.ScenePath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(boss.ScenePath);
		if (scene == null)
		{
			warnings.Add($"Boss '{boss.Id}' scene '{boss.ScenePath}' failed to load.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not BossEnemy)
			warnings.Add($"Boss '{boss.Id}' scene '{boss.ScenePath}' does not have a BossEnemy script attached, so the run could never be won.");

		probe?.Free();
	}

	// The smallest RunResult that can satisfy a boss achievement: a victory carrying that boss id.
	// Deliberately leaves every other field at its default so only the boss clause can match.
	private static global::RunResult BuildVictoryProbe(string bossId)
	{
		return new global::RunResult
		{
			Outcome = global::RunOutcome.Victory,
			BossId = bossId
		};
	}

	// The synergy screen renders each set from its icon and its Effects list, so a set that ships
	// with neither is invisible or blank to the player. This cannot check that the numbers still
	// agree with Player.RefreshChestSetEffects - that switch is imperative - but it does catch a
	// new set being added without its presentation data filled in.
	private static void ValidateChestSetPresentation(List<string> warnings)
	{
		foreach (var set in ChestItemCatalog.Sets)
		{
			if (set.Effects == null || set.Effects.Length == 0)
				warnings.Add($"Chest set '{set.Id}' declares no Effects, so the synergy screen has nothing to list.");

			if (string.IsNullOrWhiteSpace(set.IconPath) || !ResourceLoader.Exists(set.IconPath))
				warnings.Add($"Chest set '{set.Id}' has a missing icon: '{set.IconPath}'.");

			if (set.RequiredItemIds == null || set.RequiredItemIds.Length == 0)
				warnings.Add($"Chest set '{set.Id}' requires no items, so it completes immediately.");
		}

		ValidateChestItemIcons(warnings);
	}

	// Relics are told apart by their icon in the chest menu, the HUD strip and the synergy screen,
	// so two relics sharing one image is a real readability bug rather than a cosmetic one. Three
	// pairs used to collide; this keeps them from drifting back together.
	private static void ValidateChestItemIcons(List<string> warnings)
	{
		var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string itemId in ChestItemCatalog.AllItemIds)
		{
			string icon = ChestItemCatalog.GetIconPath(itemId);
			if (string.IsNullOrWhiteSpace(icon) || !ResourceLoader.Exists(icon))
			{
				warnings.Add($"Chest item '{itemId}' has a missing icon: '{icon}'.");
				continue;
			}

			if (seen.TryGetValue(icon, out string owner))
				warnings.Add($"Chest items '{owner}' and '{itemId}' share the icon '{icon}'.");
			else
				seen[icon] = itemId;
		}
	}

	private static void ValidateSpellEvolutionCoverage(List<string> warnings)
	{
		string[] testSpellIds = new[] { "magic_missile", "fireball", "arcane_explosion", "aegis_ward", "meteor_swarm" };
		foreach (string id in testSpellIds)
		{
			var spell = new SpellData { Id = id, Name = id };
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			if (spell.Level4Options == null || spell.Level4Options.Count != 3)
				warnings.Add($"Spell '{id}' should have exactly 3 Level 4 evolution choices, got {spell.Level4Options?.Count ?? 0}.");
			if (spell.Level8Options == null || spell.Level8Options.Count != 2)
				warnings.Add($"Spell '{id}' should have exactly 2 Level 8 evolution choices, got {spell.Level8Options?.Count ?? 0}.");
		}
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
