using Godot;

namespace WizardSurvivors.scripts;

// Shared element -> display color palette. Used to tint/modulate the temporary placeholder art
// (reused generic wizard sprites in CharacterSelection, the shared default spell icon in
// LevelUpMenu, etc.) so visually-identical reused assets can still be told apart at a glance,
// until real per-character/per-spell art exists (issue #30).
public static class ElementColors
{
	public static Color GetColor(Element element)
	{
		return element switch
		{
			Element.Fire => new Color(0.9f, 0.35f, 0.2f),
			Element.Ice => new Color(0.5f, 0.8f, 1.0f),
			Element.Arcane => new Color(0.6f, 0.4f, 0.9f),
			Element.Darkness => new Color(0.3f, 0.2f, 0.4f),
			Element.Light => new Color(1.0f, 0.95f, 0.6f),
			Element.Grass => new Color(0.35f, 0.75f, 0.3f),
			Element.Earth => new Color(0.6f, 0.45f, 0.3f),
			Element.Wind => new Color(0.75f, 0.9f, 0.85f),
			Element.Lightning => new Color(0.95f, 0.9f, 0.3f),
			Element.Poison => new Color(0.5f, 0.75f, 0.2f),
			Element.Metal => new Color(0.7f, 0.7f, 0.75f),
			Element.Water => new Color(0.25f, 0.5f, 0.9f),
			_ => new Color(0.5f, 0.5f, 0.5f)
		};
	}
}
