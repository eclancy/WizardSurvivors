using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

public static class ContentValidator
{
	private static bool hasRun;

	// Every spell resource, loaded once and held for the life of the process.
	//
	// This exists for a crash, not for speed. ResourceLoader.Load hands back a cached resource
	// behind a fresh *managed wrapper*, and when a wrapper is collected while another Load is in
	// flight, Godot's .NET bridge double-disposes the handle and the process dies with
	// "Condition gchandle.is_released() is true". The validators walk all 20 spell resources
	// several times over, and adding the evolution-tag checks pushed that over the edge. A static
	// list is a GC root, so the wrappers - and the evolution options hanging off them - are never
	// collected and the race cannot happen.
	private static List<SpellData> loadedSpells;

	public static IReadOnlyList<SpellData> LoadAllSpells()
	{
		if (loadedSpells != null)
			return loadedSpells;

		loadedSpells = new List<SpellData>();
		foreach (string path in SpellResourcePaths)
		{
			var spell = ResourceLoader.Load<SpellData>(path);
			if (spell != null)
				loadedSpells.Add(spell);
		}

		return loadedSpells;
	}
	public static readonly string[] SpellResourcePaths = new[]
	{
		"res://SpellData.tres",
		"res://SpellData_AegisWard.tres",
		"res://SpellData_ArcaneExplosion.tres",
		"res://SpellData_SpiritualWeapon.tres",
		"res://SpellData_Fireball.tres",
		"res://SpellData_FrostShard.tres",
		"res://SpellData_Gravewell.tres",
		"res://SpellData_HollowStar.tres",
		"res://SpellData_IronPalisade.tres",
		"res://SpellData_Contagion.tres",
		"res://SpellData_BrambleSeed.tres",
		"res://SpellData_KindledWard.tres",
		"res://SpellData_Mirefoot.tres",
		"res://SpellData_Riptide.tres",
		"res://SpellData_ShadowBolt.tres",
		"res://SpellData_ThornVine.tres",
		"res://SpellData_GaleBlade.tres",
		"res://SpellData_SolarFlare.tres",
		"res://SpellData_MoltenShard.tres",
		"res://SpellData_ChainLightning.tres",
		"res://SpellData_Cinderbreath.tres",
		"res://SpellData_ToxicSporeBurst.tres",
		"res://SpellData_ObsidianSpike.tres",
		"res://SpellData_CycloneSlash.tres",
		"res://SpellData_VoidLance.tres",
		"res://SpellData_GlacialSpike.tres",
		"res://SpellData_BlackTentacles.tres",
		"res://SpellData_ConeOfCold.tres",
		"res://SpellData_ScorchingRay.tres",
		"res://SpellData_MeteorSwarm.tres",
		"res://SpellData_HuntersDraw.tres"
	};

	public static void ValidateAtStartup(Node context)
	{
		if (hasRun)
			return;

		hasRun = true;
		ContentValidationSummary summary = ValidateSpellAndCharacterData();
		foreach (string warning in RegressionChecks.RunAll())
			summary.Warnings.Add($"Regression: {warning}");
		if (summary.Errors.Count == 0 && summary.Warnings.Count == 0)
		{
			GD.Print("ContentValidator: all spell and character resources passed validation.");
			return;
		}

		foreach (string error in summary.Errors)
			GD.PushError($"ContentValidator: {error}");

		foreach (string warning in summary.Warnings)
			GD.PushWarning($"ContentValidator: {warning}");

		GD.Print($"ContentValidator finished with {summary.Errors.Count} errors and {summary.Warnings.Count} warnings.");
	}

	public static ContentValidationSummary ValidateSpellAndCharacterData()
	{
		var summary = new ContentValidationSummary();
		ValidateSpellData(summary);
		ValidateCharacterData(summary);
		return summary;
	}

	private static void ValidateSpellData(ContentValidationSummary summary)
	{
		var spellsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var iconsByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (string path in SpellResourcePaths)
		{
			SpellData spell = ResourceLoader.Load<SpellData>(path);
			if (spell == null)
			{
				summary.Errors.Add($"Spell resource failed to load: {path}");
				continue;
			}

			if (string.IsNullOrWhiteSpace(spell.Id))
				summary.Errors.Add($"Spell at {path} has empty Id.");
			else if (spellsById.TryGetValue(spell.Id, out string existingPath))
				summary.Errors.Add($"Duplicate spell Id '{spell.Id}' in {existingPath} and {path}.");
			else
				spellsById[spell.Id] = path;

			if (string.IsNullOrWhiteSpace(spell.Name))
				summary.Errors.Add($"Spell '{spell.Id}' at {path} has empty Name.");

			if (spell.MaxLevel < 1)
				summary.Errors.Add($"Spell '{spell.Id}' has MaxLevel < 1.");

			if (spell.CurrentLevel < 1 || spell.CurrentLevel > spell.MaxLevel)
				summary.Warnings.Add($"Spell '{spell.Id}' has CurrentLevel {spell.CurrentLevel} outside 1..{spell.MaxLevel}.");

			if (spell.BaseCooldown <= 0.0f)
				summary.Errors.Add($"Spell '{spell.Id}' has non-positive BaseCooldown {spell.BaseCooldown:0.###}.");

			if (spell.BaseProjectileCount < 1)
				summary.Errors.Add($"Spell '{spell.Id}' has BaseProjectileCount < 1.");

			if (spell.BaseRange < 0.0f)
				summary.Errors.Add($"Spell '{spell.Id}' has negative BaseRange.");

			if (spell.Icon == null)
				summary.Errors.Add($"Spell '{spell.Id}' has no icon assigned in resource.");
			else if (!string.IsNullOrWhiteSpace(spell.Icon.ResourcePath))
			{
				string iconPath = spell.Icon.ResourcePath;
				if (iconsByPath.TryGetValue(iconPath, out string existingSpellId) && !existingSpellId.Equals(spell.Id, StringComparison.OrdinalIgnoreCase))
					summary.Warnings.Add($"Spell icon is reused by '{existingSpellId}' and '{spell.Id}' ({iconPath}).");
				else
					iconsByPath[iconPath] = spell.Id;
			}

			if (spell.ElementWeights == null || spell.ElementWeights.Count == 0)
				summary.Warnings.Add($"Spell '{spell.Id}' has no element weights.");
			else
			{
				foreach (KeyValuePair<string, int> pair in spell.ElementWeights)
				{
					if (!Enum.TryParse<Element>(pair.Key, true, out _))
						summary.Errors.Add($"Spell '{spell.Id}' uses invalid element key '{pair.Key}'.");
					if (pair.Value <= 0)
						summary.Errors.Add($"Spell '{spell.Id}' has non-positive weight for '{pair.Key}'.");
				}
			}

			if (spell.LevelUpgrades != null)
			{
				int previousLevel = 0;
				foreach (SpellLevelUpgrade upgrade in spell.LevelUpgrades)
				{
					if (upgrade == null)
					{
						summary.Errors.Add($"Spell '{spell.Id}' contains a null level upgrade entry.");
						continue;
					}
					// Several upgrades may legitimately share one level - Player.EnsureRelevantLevelUps
					// generates e.g. a Lv4 damage bonus alongside a Lv4 pierce, then sorts by level.
					// Only a level that actually goes backwards indicates unsorted data.
					if (upgrade.Level < previousLevel)
						summary.Errors.Add($"Spell '{spell.Id}' has non-ascending upgrade level order near Lv {upgrade.Level}.");
					if (upgrade.Level < 2 || upgrade.Level > spell.MaxLevel)
						summary.Errors.Add($"Spell '{spell.Id}' upgrade level {upgrade.Level} is outside expected range 2..{spell.MaxLevel}.");
					previousLevel = upgrade.Level;
				}
			}
		}
	}

	private static void ValidateCharacterData(ContentValidationSummary summary)
	{
		var characters = CharacterRoster.GetAll();
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CharacterData character in characters)
		{
			if (character == null)
				continue;

			if (string.IsNullOrWhiteSpace(character.Id))
				summary.Errors.Add("A character resource has an empty Id.");
			else if (!ids.Add(character.Id))
				summary.Errors.Add($"Duplicate character Id '{character.Id}'.");

			if (string.IsNullOrWhiteSpace(character.Name))
				summary.Errors.Add($"Character '{character.Id}' has empty Name.");

			if (character.HealthModifier <= 0.0f)
				summary.Errors.Add($"Character '{character.Id}' has non-positive HealthModifier.");

			if (character.SpeedModifier <= 0.0f)
				summary.Errors.Add($"Character '{character.Id}' has non-positive SpeedModifier.");

			if (character.StartingSpellResource == null || string.IsNullOrWhiteSpace(character.StartingSpellResource.Id))
				summary.Errors.Add($"Character '{character.Id}' has invalid StartingSpellResource.");

			if (string.IsNullOrWhiteSpace(character.StartingPassiveId))
				summary.Warnings.Add($"Character '{character.Id}' has no StartingPassiveId.");
		}

		if (characters.Count == 0)
			summary.Errors.Add("Character roster is empty.");
	}
}

public sealed class ContentValidationSummary
{
	public List<string> Errors { get; } = new();
	public List<string> Warnings { get; } = new();
}
