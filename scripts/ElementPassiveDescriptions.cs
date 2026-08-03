using Godot;

namespace WizardSurvivors.scripts;

public static class ElementPassiveDescriptions
{
	public static string GetProgressLabel(int count)
	{
		int threshold = GetDisplayedThresholdForCount(count);
		return $"{Mathf.Min(count, threshold)}/{threshold}";
	}

	public static string GetEffectText(string elementName, int tier)
	{
		return System.Enum.TryParse<Element>(elementName, true, out var element)
			? GetEffectText(element, tier)
			: "Unknown element passive";
	}

	public static string GetEffectText(Element element, int tier)
	{
		if (tier <= 0)
			return GetEffectText(element, 2);

		return element switch
		{
			Element.Fire => tier switch { 6 => "+35% damage to nearby enemies", 4 => "+20% damage to nearby enemies", _ => "+10% damage to nearby enemies" },
			Element.Ice => tier switch { 6 => "35% slow for 2s on hit", 4 => "20% slow for 2s on hit", _ => "10% slow for 2s on hit" },
			Element.Arcane => tier switch { 6 => "+35% XP gained", 4 => "+20% XP gained", _ => "+10% XP gained" },
			Element.Darkness => tier switch { 6 => "-35% incoming damage", 4 => "-20% incoming damage", _ => "-10% incoming damage" },
			Element.Light => tier switch { 6 => "Heal 10% of damage dealt", 4 => "Heal 6% of damage dealt", _ => "Heal 3% of damage dealt" },
			Element.Grass => tier switch { 6 => "+4 HP/sec regeneration", 4 => "+2 HP/sec regeneration", _ => "+1 HP/sec regeneration" },
			Element.Earth => tier switch { 6 => "+100 max HP", 4 => "+50 max HP", _ => "+20 max HP" },
			Element.Wind => tier switch { 6 => "+35% move speed", 4 => "+20% move speed", _ => "+10% move speed" },
			Element.Lightning => tier switch { 6 => "Every 2nd cast from your single target spells will be empowered, bouncing to a nearby enemy for 60% damage.", 4 => "Every 4th cast from your single target spells will be empowered, bouncing to a nearby enemy for 45% damage.", _ => "Every 6th cast from your single target spells will be empowered, bouncing to a nearby enemy for 30% damage." },
			Element.Poison => tier switch { 6 => "+8 poison damage/tick", 4 => "+4 poison damage/tick", _ => "+2 poison damage/tick" },
			Element.Metal => tier switch { 6 => "-4 flat damage taken", 4 => "-2 flat damage taken", _ => "-1 flat damage taken" },
			Element.Water => tier switch { 6 => "-18% spell cooldowns", 4 => "-10% spell cooldowns", _ => "-5% spell cooldowns" },
			_ => "Active"
		};
	}

	public static string GetHudText(Element element, int count, int tier)
	{
		string tierText = tier > 0 ? $"T{tier}" : "Next";
		return $"{element} {GetProgressLabel(count)} {tierText}: {GetEffectText(element, tier)}";
	}

	private static int GetDisplayedThresholdForCount(int count)
	{
		if (count > 4) return 6;
		if (count > 2) return 4;
		return 2;
	}
}