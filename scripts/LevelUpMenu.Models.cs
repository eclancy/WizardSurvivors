using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class LevelUpOption
{
	public string SpellId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string UpgradeSummary { get; set; } = string.Empty;
	public int NextLevel { get; set; } = 1;
	public bool IsNewUnlock { get; set; } = true;
	// True when picking this option requires removing an owned spell first (loadout is full, issue #10).
	public bool RequiresSlotSwap { get; set; } = false;

	// Optional spell icon (SpellData.Icon) shown on the card - LevelUpMenu falls back to a shared
	// default icon when this is null (no unique art for most spells yet, #30).
	public Texture2D Icon { get; set; }

	// Element preview data (issue #15): how much this option would change each element's instance
	// count if chosen, keyed by element name, plus the resulting count after the pick.
	public Dictionary<string, int> ElementContribution { get; set; } = new();
	public Dictionary<string, int> ResultingElementCounts { get; set; } = new();

	public string GetButtonText()
	{
		string prefix = IsNewUnlock ? "New" : "Upgrade";
		string suffix = RequiresSlotSwap ? " (replaces a spell)" : string.Empty;
		return $"{DisplayName}  [{prefix} Lv {NextLevel}]{suffix}";
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

// Minimal info about a currently-equipped spell, used to build the "choose a spell to remove"
// prompt and (via ElementWeights/IsPassive) the elemental tag section's passive-highlight logic.
public sealed class EquippedSpellInfo
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public int CurrentLevel { get; set; } = 1;
	public Dictionary<string, int> ElementWeights { get; set; } = new();
	public bool IsPassive { get; set; } = false;
}
