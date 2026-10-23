using Godot;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public sealed class LevelUpOption
{
	public string SpellId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string UpgradeSummary { get; set; } = string.Empty;

	/// <summary>The spell's stats as it would arrive, for a card with no previous level to diff.</summary>
	/// <remarks>
	/// UpgradeSummary is a list of CHANGES, so it is empty for a brand-new spell - there is nothing
	/// to change yet. Before this existed the description was the only body text a new-spell card
	/// had, so removing descriptions left those cards blank. This is what "just leave the stats"
	/// means for a card that has no deltas to show.
	/// </remarks>
	public string StatSummary { get; set; } = string.Empty;
	public int NextLevel { get; set; } = 1;
	// The spell's maximum level, used to draw one level pip per available level on the option card.
	public int MaxLevel { get; set; } = 1;
	public bool IsNewUnlock { get; set; } = true;
	public bool IsPassive { get; set; } = false;

	// A boon rather than a spell: permanent, never levelled, and it does not consume a spell slot.
	// See BoonCatalog and .ai/passives-and-items.md.
	public bool IsBoon { get; set; } = false;
	// (RequiresSlotSwap is gone with the swap itself: a full book is offered no new spell, so no
	// option ever needs a held one erased to make room. See Player.GetLevelUpOptions.)

	// Optional spell icon (SpellData.Icon) shown on the card - LevelUpMenu falls back to a shared
	// default icon when this is null (no unique art for most spells yet, #30).
	public Texture2D Icon { get; set; }

	// Element preview data (issue #15): how much this option would change each element's instance
	// count if chosen, keyed by element name, plus the resulting count after the pick.
	public Dictionary<string, int> ElementContribution { get; set; } = new();
	public Dictionary<string, int> ResultingElementCounts { get; set; } = new();

	// The spell's own element tag weights (what tags this spell provides), independent of whether
	// it's a new unlock or an upgrade. Used to render the colored tag chips on each option card.
	public Dictionary<string, int> SpellElementTags { get; set; } = new();

	// Evolution / Branching Milestone Support (Issue #48 & #49).
	public bool IsEvolutionMilestone { get; set; } = false;
	public int MilestoneLevel { get; set; } = 0; // 4 or 8
	public List<WizardSurvivors.scripts.SpellEvolutionOption> EvolutionChoices { get; set; } = new();
	public WizardSurvivors.scripts.SpellEvolutionOption SelectedEvolution { get; set; }

	/// <summary>Evolution id to the reason it cannot be taken. Absent means it can.</summary>
	/// <remarks>
	/// Locked ascensions are still shown, greyed, carrying their requirement. Filtering them out
	/// instead would mean the player never learns the rule exists - they would just see two
	/// choices one run and three the next and conclude the game was being random at them.
	/// </remarks>
	public Dictionary<string, string> EvolutionLockReason { get; set; } = new();
	public string SynergyTag { get; set; } = string.Empty;
	public string SynergyDescription { get; set; } = string.Empty;

	public string GetButtonText()
	{
		string prefix = IsNewUnlock ? "New" : "Upgrade";
		return $"{DisplayName}  [{prefix} Lv {NextLevel}]";
	}

	public string GetElementPreviewText()
	{
		if (ResultingElementCounts == null || ResultingElementCounts.Count == 0)
			return string.Empty;

		var parts = new List<string>();
		foreach (var pair in ResultingElementCounts.OrderByDescending(p => p.Value))
		{
			string element = pair.Key;
			int resultingCount = pair.Value;
			int delta = ElementContribution != null && ElementContribution.TryGetValue(element, out int d) ? d : 0;
			int tier = resultingCount >= 6 ? 6 : resultingCount >= 4 ? 4 : resultingCount >= 2 ? 2 : 0;
			string deltaText = delta > 0 ? $" +{delta}" : string.Empty;
			string tierText = tier > 0 ? $" (Tier {tier})" : string.Empty;
			parts.Add($"{element}{deltaText} -> {resultingCount}{tierText}");
		}
		return string.Join("   ", parts);
	}
}

// Minimal info about a currently-equipped spell, used by the ascension browser and (via
// ElementWeights/IsPassive) the elemental tag section's passive-highlight logic.
public sealed class EquippedSpellInfo
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public int CurrentLevel { get; set; } = 1;
	public Dictionary<string, int> ElementWeights { get; set; } = new();
	public bool IsPassive { get; set; } = false;

	// Optional spell icon (SpellData.Icon) shown in the ascension browser.
	public Texture2D Icon { get; set; }

	/// <summary>
	/// Every level 8 branch this spell has, and what still stands between the player and each one.
	/// Filled by Player.BuildAscensionPreview and read only by the ascension browser.
	/// </summary>
	/// <remarks>
	/// An ascension is the biggest single decision in a run and until now the player met it by
	/// surprise: nothing anywhere said which spells had one, what it wanted, or how close they
	/// were. Carrying it on the equipped-spell record rather than adding a parameter to SetOptions
	/// keeps the two probe scenes that call that method compiling unchanged.
	/// </remarks>
	public List<AscensionInfo> Ascensions { get; set; } = new();
}

/// <summary>One level 8 branch, as the ascension browser needs to show it.</summary>
public sealed class AscensionInfo
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Texture2D Icon { get; set; }

	/// <summary>
	/// What is not yet true, in the player's own terms - "Spell level 8 (5/8)", "Requires 4 Fire
	/// (2/4)". Empty means the branch is available the moment the spell next levels.
	/// </summary>
	public List<string> UnmetRequirements { get; set; } = new();

	/// <summary>
	/// Every gate, met or not, in the order the browser lists them (spell level, element, player
	/// level), so it can tick the met ones and point at the rest. Filled alongside
	/// <see cref="UnmetRequirements"/> by Player.BuildAscensionPreview; a caller that only fills the
	/// strings (the probe scenes) gets them listed as plain lines.
	/// </summary>
	public List<AscensionRequirement> Requirements { get; set; } = new();

	public bool IsAvailable => UnmetRequirements.Count == 0;

	/// <summary>This is the branch the spell ascended into this run.</summary>
	/// <remarks>
	/// The requirements of an ascended spell stay met - its level, tags and the player's level do not
	/// go down - so before this the browser went on calling the spell "Ready" after it had ascended,
	/// and the Ascensions button kept glowing for it.
	/// </remarks>
	public bool Taken { get; set; }

	/// <summary>Another branch of this spell was taken, so this one can no longer be chosen this run.</summary>
	public bool Closed { get; set; }

	/// <summary>Takeable now: every gate holds and the spell has not ascended yet.</summary>
	public bool Open => IsAvailable && !Taken && !Closed;
}

public enum AscensionRequirementKind
{
	SpellLevel,
	Element,
	PlayerLevel,
}

/// <summary>One gate on an ascension, with how far along the player is.</summary>
public sealed class AscensionRequirement
{
	public AscensionRequirementKind Kind { get; set; }
	public int Have { get; set; }
	public int Need { get; set; }

	/// <summary>
	/// For an element gate, the element the player is closest in (what the browser names), and every
	/// element that would satisfy it - an ascension with no element of its own takes any of its
	/// spell's tags.
	/// </summary>
	public WizardSurvivors.scripts.Element? Element { get; set; }
	public List<WizardSurvivors.scripts.Element> AnyOf { get; set; } = new();

	public bool Met => Have >= Need;
	public int Missing => System.Math.Max(0, Need - Have);
}
