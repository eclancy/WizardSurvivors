using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>One wizard's Apotheosis: the covenant that unlocks it, and the form it takes.</summary>
public sealed class ApotheosisDefinition
{
	/// <summary>The gate is a (character, spell) pair: a Frostweaver who maxes Cinderbreath gets nothing.</summary>
	public string CharacterId { get; init; } = string.Empty;
	public string SpellId { get; init; } = string.Empty;
	public string RequiredLevel4EvolutionId { get; init; } = string.Empty;
	public string RequiredLevel8EvolutionId { get; init; } = string.Empty;
	public Element RequiredElement { get; init; }
	public int RequiredTier { get; init; } = 6;

	/// <summary>The one verb this form owns and no other may (issue #59, the rule that keeps the set apart).</summary>
	public string Verb { get; init; } = string.Empty;

	public SpellEvolutionOption Form { get; init; }
}

/// <summary>
/// The single authority on Apotheoses (issue #59, #60, #61; .ai/decisions/2026-10-10-apotheosis.md).
/// </summary>
/// <remarks>
/// An Apotheosis is the final form of the spell a wizard starts with, offered in the Ascension strip
/// above the level-up cards once four things hold on the same spell at the same time: it is at max level, the named
/// level 4 and level 8 path was taken, the wizard's element is at tier 6, and the wizard is the
/// one this entry names. Taking it spends the pick, and it is terminal.
///
/// Every required path is the pair of milestone choices that DEEPENS the wizard's own element. A
/// required branch that led away from it would make tier 6 harder on the exact build the gate
/// demands (#61 says so in as many words), and the issue's original Stormcaller path named a
/// level 8 option that no longer exists. RegressionChecks.ValidateApotheosisCatalog holds both.
///
/// Each form's behaviour lives in the spell's own script, switched on by
/// <see cref="SpellData.IsApotheosis"/>; this catalog is only the gate and the stat card. The
/// form's element weight is +1 of the wizard's own element and nothing else, so accepting it can
/// never drop the player out of the tier that qualified them.
/// </remarks>
public static class ApotheosisCatalog
{
	public const int MilestoneLevel = 9;

	private static readonly ApotheosisDefinition[] Definitions =
	{
		new()
		{
			CharacterId = "pyromancer", SpellId = "cinderbreath",
			RequiredLevel4EvolutionId = "cinderbreath_amplified_impact",
			RequiredLevel8EvolutionId = "cinderbreath_titan_cataclysm",
			RequiredElement = Element.Fire, Verb = "burn",
			Form = Make("cinderbreath_cataclysm", "Cataclysm", Element.Fire,
				"Whatever the flame kills leaves a burning crater, and anything that dies in a crater opens another. The crowd is the fuel.",
				"Burn: the denser the wave, the further it spreads. Almost nothing against a lone boss.",
				new Color(1.0f, 0.45f, 0.12f)),
		},
		new()
		{
			CharacterId = "frostweaver", SpellId = "cone_of_cold",
			RequiredLevel4EvolutionId = "cone_deep_freeze",
			RequiredLevel8EvolutionId = "cone_zero_point_wave",
			RequiredElement = Element.Ice, Verb = "stop",
			Form = Make("cone_of_cold_long_winter", "The Long Winter", Element.Ice,
				"The cone opens into a full ring that freezes everything it touches solid. The frozen shatter when they die, and the shards freeze their neighbours.",
				"Stop: measured in what does not reach you. Bosses take a hard slow instead of a root.",
				new Color(0.70f, 0.92f, 1.0f)),
		},
		new()
		{
			CharacterId = "stormcaller", SpellId = "chain_lightning",
			RequiredLevel4EvolutionId = "chain_lightning_storm_reach",
			RequiredLevel8EvolutionId = "chain_lightning_cascade",
			RequiredElement = Element.Lightning, Verb = "link",
			Form = Make("chain_lightning_lattice", "The Lattice", Element.Lightning,
				"The chain stops ending. Everything it strikes stays linked for a few seconds, and the web keeps discharging along its own edges.",
				"Link: damage follows how connected the crowd is. Herd them together.",
				new Color(1.0f, 0.95f, 0.45f)),
		},
		new()
		{
			CharacterId = "geomancer", SpellId = "obsidian_spike",
			RequiredLevel4EvolutionId = "obsidian_spike_splinter_field",
			RequiredLevel8EvolutionId = "obsidian_spike_tectonic_uprising",
			RequiredElement = Element.Earth, Verb = "hold",
			Form = Make("obsidian_spike_sundering", "Sundering", Element.Earth,
				"Each eruption shoves everything back and raises a broken ring of obsidian around you. Inside it you take less from every hit.",
				"Hold: the only form that edits the arena. Keep re-siting the ring as the wave moves.",
				new Color(0.55f, 0.45f, 0.70f)),
		},
		new()
		{
			CharacterId = "tituba", SpellId = "riptide",
			RequiredLevel4EvolutionId = "riptide_amplified_impact",
			RequiredLevel8EvolutionId = "riptide_titan_cataclysm",
			RequiredElement = Element.Water, Verb = "flood",
			Form = Make("riptide_spring_tide", "Spring Tide", Element.Water,
				"Every wave leaves the ground behind it flooded. The current drags whatever stands in it along the wave's path and will not let it walk.",
				"Flood: aim the line, then let the water carry them where you want them.",
				new Color(0.40f, 0.72f, 1.0f)),
		},
		new()
		{
			CharacterId = "vainamoinen", SpellId = "bramble_seed",
			RequiredLevel4EvolutionId = "bramble_seed_amplified_impact",
			RequiredLevel8EvolutionId = "bramble_seed_titan_cataclysm",
			RequiredElement = Element.Grass, Verb = "swarm",
			Form = Make("bramble_seed_wildwood", "Wildwood", Element.Grass,
				"A patch that finishes growing throws seeds of its own, and the thicket spreads across the ground you have walked.",
				"Swarm: the longer the run goes on, the more of the field is yours.",
				new Color(0.55f, 0.90f, 0.40f)),
		},
	};

	private static SpellEvolutionOption Make(string id, string name, Element element, string description, string synergy, Color tint) => new()
	{
		Id = id,
		DisplayName = name,
		Description = description,
		MilestoneLevel = MilestoneLevel,
		SynergyTag = "Apotheosis",
		SynergyDescription = synergy,
		// A modest stat lift: the form's behaviour is the reward, not a bigger number.
		DamageMultiplier = 1.25f,
		BonusElementWeights = new() { { element.ToString(), 1 } },
		ModulateColor = tint,
		ScaleMultiplier = 1.15f,
		VisualTag = "Apotheosis",
	};

	public static IReadOnlyList<ApotheosisDefinition> All => Definitions;

	public static ApotheosisDefinition ForCharacter(string characterId) =>
		Definitions.FirstOrDefault(d => d.CharacterId.Equals(characterId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

	public static ApotheosisDefinition ForForm(string formId) =>
		Definitions.FirstOrDefault(d => d.Form.Id.Equals(formId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Whether <paramref name="spell"/> meets this wizard's covenant right now, minus the element
	/// tier, which the caller reads from the whole loadout.
	/// </summary>
	public static bool SpellMeetsPath(ApotheosisDefinition definition, SpellData spell) =>
		definition != null && spell != null && !spell.IsApotheosis
		&& spell.Id.Equals(definition.SpellId, StringComparison.OrdinalIgnoreCase)
		&& spell.CurrentLevel >= spell.MaxLevel
		&& spell.SelectedLevel4EvolutionId.Equals(definition.RequiredLevel4EvolutionId, StringComparison.OrdinalIgnoreCase)
		&& spell.SelectedLevel8EvolutionId.Equals(definition.RequiredLevel8EvolutionId, StringComparison.OrdinalIgnoreCase);
}
