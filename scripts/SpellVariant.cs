using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// One form a spell can take, chosen once - on the page that follows the card the first time the spell
/// is taken - and held for the rest of the run: Spiritual Weapon's axe, hammer or sword; the Familiar's
/// crow, cat or dog. .ai/decisions/2026-10-28-spell-variants.md is the design.
/// </summary>
/// <remarks>
/// A variant is DATA ON ITS SPELL: sub-resources of the spell's own .tres (SpellData.Variants), so a
/// form is written beside the numbers it replaces and read the way every other spell number is.
/// Choosing one writes it into the run's copy of the spell (<see cref="SpellVariants.Apply"/>) - its
/// name, icon, words, base numbers and level table - so every screen and every reader that already
/// knows a held spell (the loadout strip, the pause list, the results screen, every SpellData stat
/// reader) shows and uses the chosen form without being told variants exist. What a variant cannot do
/// is change the spell's element tags: the element reachability work balances twelve elements across
/// spells, boons and relics, and a form is a way of playing the spell, not a different carrier
/// (RegressionChecks.ValidateSpellVariants holds it).
///
/// Its evolutions are its own: SpellEvolutionCatalog builds the spell's level 4 and level 8 options
/// from <see cref="SpellData.SelectedVariantId"/>, and every one of them is named
/// "&lt;variant id&gt;_&lt;branch&gt;", so each has its own icon (EvolutionIcons, by id), its own row in
/// the seen-ascensions record and, through ApotheosisDefinition.VariantId, can be its own Apotheosis.
/// </remarks>
[GlobalClass]
public partial class SpellVariant : Resource
{
	/// <summary>
	/// Unique across the game, and the prefix of every evolution id the form has:
	/// "spiritual_weapon_axe" owns "spiritual_weapon_axe_broadaxe". Never equal to an evolution id.
	/// </summary>
	[Export] public string Id { get; set; } = string.Empty;

	/// <summary>The spell's name once this form is chosen ("Spirit Axe"). Shown everywhere the spell is.</summary>
	[Export] public string DisplayName { get; set; } = string.Empty;

	/// <summary>The one word the choice turns on ("Axe"): the Test Wizard's picker, the bots' logs.</summary>
	[Export] public string ShortName { get; set; } = string.Empty;

	/// <summary>The spell's description once this form is chosen (the pause screen, the results).</summary>
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;

	/// <summary>
	/// What this form is good at, in a few glyph-led lines (StatIcons.Separator between them), shown on
	/// its card on the choice page under its numbers. No sizes in pixels: a size is a percentage.
	/// </summary>
	[Export(PropertyHint.MultilineText)] public string Traits { get; set; } = string.Empty;

	/// <summary>The form's own card icon. The spell's icon once chosen.</summary>
	[Export] public Texture2D Icon { get; set; }

	/// <summary>What a count of this form is a count of ("Axes"), on its level-up lines. Empty: Projectiles.</summary>
	[Export] public string CountNoun { get; set; } = string.Empty;

	// The numbers, written out in full: a form replaces the spell's, it does not adjust them, so the
	// .tres reads as the form's table and no one has to add two tables in their head.
	[Export] public int BaseDamage { get; set; } = 1;
	[Export] public float BaseCooldown { get; set; } = 1.0f;
	[Export] public int BaseProjectileCount { get; set; } = 1;
	[Export] public float BaseRange { get; set; } = 0.0f;

	/// <summary>The form's level table, levels 2-8, written out in full (Player.EnsureRelevantLevelUps
	/// only fills in a line the table leaves out).</summary>
	[Export] public Godot.Collections.Array<SpellLevelUpgrade> LevelUpgrades { get; set; } = new();

	// How the form is classified - what the pause screen says it aims at, how it hurts and what improves
	// it. Auto / 0 keeps the spell's.
	[Export] public SpellTargetingMode TargetingMode { get; set; } = SpellTargetingMode.Auto;
	[Export] public SpellDamageShape DamageShape { get; set; } = SpellDamageShape.Auto;
	[Export(PropertyHint.Flags, "Damage,Cooldown,Area,Range,ProjectileCount,ProjectileSpeed,Pierce,Chain,Crit,Slow,Root,Knockback,Dot,Duration")]
	public int ScalingTagsMask { get; set; } = 0;
}

/// <summary>
/// The rules for spell variants that are not data: how a choice is written in a level-up selection,
/// and what choosing one does to the run's copy of a spell.
/// </summary>
public static class SpellVariants
{
	/// <summary>
	/// The selection a level-up sends for a NEW spell taken in one of its forms: "spell_id:variant_id".
	/// </summary>
	/// <remarks>
	/// The same shape as an evolution's "spell_id:evolution_id", on purpose. Every reader that takes the
	/// spell out of a selection already splits on the colon (Node2DGame.GetSpellLevel, the telemetry, the
	/// harness), and what the second half names is decided by whether the spell is held: for a spell the
	/// player does not have yet it is the form, for one they hold it is a milestone. Variant ids and
	/// evolution ids are both globally unique and never collide (RegressionChecks.ValidateSpellVariants).
	/// </remarks>
	public static string Selection(string spellId, string variantId) =>
		string.IsNullOrWhiteSpace(variantId) ? spellId : $"{spellId}:{variantId}";

	/// <summary>The spell and the form a selection names. The form is empty for a plain spell id.</summary>
	public static (string SpellId, string VariantId) Split(string selection)
	{
		if (string.IsNullOrWhiteSpace(selection))
			return (string.Empty, string.Empty);
		int colon = selection.IndexOf(':');
		return colon < 0 ? (selection.Trim(), string.Empty) : (selection[..colon].Trim(), selection[(colon + 1)..].Trim());
	}

	/// <summary>
	/// Makes <paramref name="spell"/> - the run's own copy, never the catalog's template - the chosen form.
	/// Refuses a second form: a choice holds for the run.
	/// </summary>
	public static bool Apply(SpellData spell, SpellVariant variant)
	{
		if (spell == null || variant == null)
			return false;
		if (!string.IsNullOrEmpty(spell.SelectedVariantId))
		{
			GD.PushError($"SpellVariants: '{spell.Id}' is already '{spell.SelectedVariantId}'; a form is chosen once a run.");
			return false;
		}
		if (spell.FindVariant(variant.Id) == null)
		{
			GD.PushError($"SpellVariants: '{variant.Id}' is not a form of '{spell.Id}'.");
			return false;
		}

		spell.SelectedVariantId = variant.Id;
		if (!string.IsNullOrWhiteSpace(variant.DisplayName))
			spell.Name = variant.DisplayName;
		if (!string.IsNullOrWhiteSpace(variant.Description))
			spell.Description = variant.Description;
		if (variant.Icon != null)
			spell.Icon = variant.Icon;
		else
			GD.PushWarning($"SpellVariants: '{variant.Id}' has no icon; '{spell.Id}' keeps its own.");

		spell.BaseDamage = variant.BaseDamage;
		spell.BaseCooldown = variant.BaseCooldown;
		spell.BaseProjectileCount = variant.BaseProjectileCount;
		spell.BaseRange = variant.BaseRange;
		if (variant.TargetingMode != SpellTargetingMode.Auto)
			spell.TargetingMode = variant.TargetingMode;
		if (variant.DamageShape != SpellDamageShape.Auto)
			spell.DamageShape = variant.DamageShape;
		if (variant.ScalingTagsMask != 0)
			spell.ScalingTagsMask = variant.ScalingTagsMask;

		// The form's own table, then whatever the catalog fills in for its scaling tags - the same pass
		// every spell's table gets before a run offers it.
		var table = new Godot.Collections.Array<SpellLevelUpgrade>();
		foreach (SpellLevelUpgrade upgrade in variant.LevelUpgrades ?? new Godot.Collections.Array<SpellLevelUpgrade>())
		{
			if (upgrade != null)
				table.Add(upgrade);
		}
		spell.LevelUpgrades = table;
		Player.PrepareSpellLevelUps(spell);

		// The form's milestones replace the ones the spell was copied with.
		spell.Level4Options = new Godot.Collections.Array<SpellEvolutionOption>();
		spell.Level8Options = new Godot.Collections.Array<SpellEvolutionOption>();
		SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
		return true;
	}

	/// <summary>
	/// A copy of <paramref name="template"/> in the form <paramref name="variant"/>: what the choice page
	/// shows of a form, and what the validators check. Never the template itself.
	/// </summary>
	public static SpellData Preview(SpellData template, SpellVariant variant)
	{
		if (template == null || variant == null)
			return null;
		var copy = template.Duplicate(true) as SpellData;
		if (copy == null)
			return null;
		copy.SelectedVariantId = string.Empty;
		SpellVariant own = copy.FindVariant(variant.Id);
		return own != null && Apply(copy, own) ? copy : null;
	}

	/// <summary>
	/// Every form <paramref name="template"/> can be played in: one preview per variant, or the template
	/// itself for a spell that has none. The validators walk this, so each form's evolutions, level table
	/// and readers are checked rather than only the template's.
	/// </summary>
	public static IEnumerable<SpellData> Forms(SpellData template)
	{
		if (template == null)
			yield break;
		if (!template.HasVariants)
		{
			yield return template;
			yield break;
		}
		foreach (SpellVariant variant in template.Variants)
		{
			SpellData form = Preview(template, variant);
			if (form != null)
				yield return form;
		}
	}

	/// <summary>
	/// Whether a level-up line that starts with <paramref name="word"/> is a count of some form ("Axes +1"),
	/// for StatIcons, which gives a count the projectiles glyph.
	/// </summary>
	public static bool IsCountNoun(string word)
	{
		if (string.IsNullOrWhiteSpace(word))
			return false;
		if (countNouns == null)
		{
			// Strings only, so the cache holds no Godot object (the teardown crash in BonelightSkin's
			// remarks); the spells come from ContentValidator's own held list for the reason it gives.
			var nouns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (SpellData spell in ContentValidator.LoadAllSpells())
			{
				if (spell?.Variants == null)
					continue;
				foreach (SpellVariant variant in spell.Variants)
				{
					if (!string.IsNullOrWhiteSpace(variant?.CountNoun))
						nouns.Add(variant.CountNoun.Trim());
				}
			}
			countNouns = nouns;
		}
		return countNouns.Contains(word.Trim());
	}

	private static HashSet<string> countNouns;
}
