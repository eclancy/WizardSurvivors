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

	/// <summary>One element's emissive ramp, white-hot core to saturated edge.</summary>
	public readonly record struct Ramp(Color Core, Color Hot, Color Mid, Color Edge);

	/// <summary>
	/// The emissive ramps of .ai/art-direction.md section 3, "Elements are emissive, not pigment":
	/// the core is white-hot so a cast reads as a spell, and the hue lives in the mid and the edge.
	/// What an element path is repainted in (scenes/shaders/element_ramp.gdshader and the spells that
	/// draw themselves), so a branch into Darkness is the game's Darkness and not a guess at purple.
	/// </summary>
	public static Ramp GetRamp(Element element) => element switch
	{
		Element.Fire => new Ramp(Hex(0xFFF4D0), Hex(0xFFC848), Hex(0xE87A1A), Hex(0xB8381A)),
		Element.Ice => new Ramp(Hex(0xF4FEFF), Hex(0xC8F4FF), Hex(0x7FDCF8), Hex(0x2E7FC4)),
		Element.Arcane => new Ramp(Hex(0xFFF0FF), Hex(0xE8C0FF), Hex(0xC08CFF), Hex(0x7A3FD4)),
		Element.Darkness => new Ramp(Hex(0xE8D8FF), Hex(0xB48CE8), Hex(0x8A5CC4), Hex(0x35205C)),
		Element.Light => new Ramp(Hex(0xFFFFFF), Hex(0xFFF8D8), Hex(0xFFF0A8), Hex(0xFFC63C)),
		Element.Grass => new Ramp(Hex(0xF4FFDC), Hex(0xCFEE94), Hex(0x9BD455), Hex(0x4A8C3A)),
		Element.Earth => new Ramp(Hex(0xFFF0D8), Hex(0xE8C48C), Hex(0xC99A5E), Hex(0x7A5024)),
		Element.Wind => new Ramp(Hex(0xFFFFFF), Hex(0xEAFCF6), Hex(0xB4EEE0), Hex(0x4FAE9E)),
		Element.Lightning => new Ramp(Hex(0xFFFFF0), Hex(0xFFF8B0), Hex(0xFFF06A), Hex(0xE0B02A)),
		Element.Poison => new Ramp(Hex(0xF8FFD8), Hex(0xDCF48C), Hex(0xB4E04A), Hex(0x6B9420)),
		Element.Metal => new Ramp(Hex(0xFFFFFF), Hex(0xEAF2FA), Hex(0xC4D4E8), Hex(0x5E7290)),
		Element.Water => new Ramp(Hex(0xF0FAFF), Hex(0xB4DCFF), Hex(0x6FB8F0), Hex(0x2A5FC4)),
		_ => new Ramp(Colors.White, Colors.White, Colors.Gray, Colors.DimGray),
	};

	/// <summary>
	/// The evolution tint for a branch into <paramref name="element"/>: the ramp's mid, scaled so its
	/// brightest channel is 1. A Modulate MULTIPLIES, so the raw mid would darken whatever it tints
	/// (Darkness's mid is a third as bright as white); normalised, it keeps the hue the art direction
	/// gives the element and loses none of the art's light. Also tints the branch's card icon.
	/// </summary>
	public static Color GetEvolutionTint(Element element)
	{
		Color mid = GetRamp(element).Mid;
		float peak = Mathf.Max(mid.R, Mathf.Max(mid.G, mid.B));
		return peak <= 0.001f ? Colors.White : new Color(mid.R / peak, mid.G / peak, mid.B / peak);
	}

	private static Color Hex(uint rgb) =>
		new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
}
