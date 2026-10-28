using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// Where every evolution's own icon lives, and the one way the game looks one up.
/// </summary>
/// <remarks>
/// "mutations and ascensions should all have their own unique altered icons. for example, rimehound
/// should not just be a blue version of the icon" (Eric, 2026-10-28). Until then every level 4
/// mutation, level 8 Ascension and Apotheosis form was drawn as its SPELL's icon tinted by the card's
/// ModulateColor. Each one now has a picture of its own, drawn by tools/art/evolution_icons.py from its
/// spell's painter and the card's data, at <c>res://assets/bonelight/ui/evolutions/&lt;evolution id&gt;.png</c>
/// - the path is the id, so nothing in SpellEvolutionCatalog or ApotheosisCatalog has to name it.
/// .ai/level-up-screen.md, "Every evolution has its own icon", has the rule and the generator.
///
/// A missing icon is never an error on screen: the caller falls back to the old tinted spell icon,
/// and this warns once per id so the gap is visible in the log. RegressionChecks.ValidateEvolutionIcons
/// reports every catalog id without one at startup.
///
/// Nothing here holds a texture in a static: a static route to a RefCounted resource is the teardown
/// crash BonelightSkin's remarks describe. ResourceLoader caches the textures; only the ids already
/// warned about are kept.
/// </remarks>
public static class EvolutionIcons
{
	public const string Directory = "res://assets/bonelight/ui/evolutions/";

	private static readonly HashSet<string> Warned = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The path an evolution's icon is drawn to, whether or not it exists yet.</summary>
	public static string PathFor(string evolutionId) => $"{Directory}{evolutionId?.Trim().ToLowerInvariant()}.png";

	/// <summary>Whether the evolution has an icon of its own on disk (imported).</summary>
	public static bool Exists(string evolutionId) =>
		!string.IsNullOrWhiteSpace(evolutionId) && ResourceLoader.Exists(PathFor(evolutionId));

	/// <summary>
	/// The evolution's own icon: one set on the option itself if any, else its generated picture.
	/// Null when it has neither - the caller then shows the spell's icon tinted, as before - and the
	/// first miss for an id is warned about.
	/// </summary>
	public static Texture2D Own(SpellEvolutionOption evolution) =>
		evolution == null ? null : evolution.Icon ?? Own(evolution.Id);

	/// <summary>The generated icon for an evolution id, or null (warned once) if it was never drawn.</summary>
	public static Texture2D Own(string evolutionId)
	{
		if (string.IsNullOrWhiteSpace(evolutionId))
			return null;
		Texture2D icon = BonelightSkin.LoadTextureSafe(PathFor(evolutionId));
		if (icon == null && Warned.Add(evolutionId))
			GD.PushWarning($"EvolutionIcons: '{evolutionId}' has no icon at {PathFor(evolutionId)}; showing its spell's icon tinted instead. "
				+ "Run tools/art/evolution_icons.py (after _AscensionAudit --json) and import.");
		return icon;
	}

	/// <summary>The resource path to record for an evolution's picture (the results screen's reveal
	/// cards): its own icon's if it has one, else <paramref name="fallbackPath"/>.</summary>
	public static string PathOrFallback(string evolutionId, string fallbackPath) =>
		Exists(evolutionId) ? PathFor(evolutionId) : fallbackPath ?? string.Empty;

	/// <summary>
	/// A held spell's picture as it is now: its Apotheosis form's icon, else its Ascension's, else its
	/// mutation's, else its own. Used where the spell itself is the picture - the loadout strip, the
	/// pause screen's run list - so a spell that has evolved is shown evolved.
	/// </summary>
	public static Texture2D Current(SpellData spell)
	{
		if (spell == null)
			return null;
		foreach (SpellEvolutionOption milestone in new[] { spell.SelectedApotheosis, spell.SelectedLevel8Evolution, spell.SelectedLevel4Evolution })
		{
			if (milestone == null)
				continue;
			Texture2D own = Own(milestone);
			if (own != null)
				return own;
		}
		return spell.Icon;
	}
}
