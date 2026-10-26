using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// Prints every spell's level 4 and level 8 (Ascension) cards with the numbers that decide how much an
// Ascension changes - and the commands to measure it - so the "is every Ascension big?" question is
// answered from the catalog rather than from memory.
//
// Run:
//   "$GODOT_BIN" --headless --path . scenes/_AscensionAudit.tscn -- --no-save-writes
//
// One line per card: the spell, the milestone, the id, then damage x, cooldown x (and the rate that
// gives), +projectiles, area x, speed x, visual scale x, and whether the card brings an Effect or an
// element rider. "rate" is 1 / cooldown x, so "x2.0 dmg, x0.56 cd" reads as about 3.6 times the
// damage a second before area and count. A card whose only change is damage is flagged DMG-ONLY: it
// can be big on paper and still look like nothing happened, which is what the player has said about
// Ascensions before (.ai/spell-roster.md, Toxic Spore Burst).
//
// It reads the catalog only. Whether a card's number reaches its spell is
// RegressionChecks.ValidateEvolutionReaders'; whether it is big in play is the balance harness's
// (`_BalanceHarness -- --spell=<id> --solo --force-ascension --branch8=<id>`, against the same run
// without --force-ascension).
public partial class _AscensionAudit : Node
{
	public override void _Ready()
	{
		var spells = new List<SpellData>(ContentValidator.LoadAllSpells());
		// Magic Missile has no .tres; the catalog builds its cards from the id alone.
		if (!spells.Any(s => s.Id == "magic_missile"))
			spells.Insert(0, new SpellData { Id = "magic_missile", Name = "Magic Missile" });

		GD.Print("AUDIT spell | milestone | id | dmg x | cd x | rate x | +proj | area x | speed x | scale x | effect | rider | flags");
		foreach (SpellData spell in spells.OrderBy(s => s.Id, StringComparer.Ordinal))
		{
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			foreach (var (milestone, list) in new[] { (4, spell.Level4Options), (8, spell.Level8Options) })
			{
				if (list == null)
					continue;
				foreach (SpellEvolutionOption o in list)
				{
					if (o == null)
						continue;
					float rate = o.CooldownMultiplier > 0.0001f ? 1f / o.CooldownMultiplier : 1f;
					bool other = o.ProjectileCountBonus != 0 || Math.Abs(o.AreaMultiplier - 1f) > 0.001f
						|| Math.Abs(o.SpeedMultiplier - 1f) > 0.001f || o.Effect != SpellEffect.None
						|| o.Rider != ElementRider.None || o.PierceBonus != 0 || o.ChainArcBonus != 0
						|| o.ChainBranchBonus != 0 || Math.Abs(o.KnockbackBonus) > 0.001f
						|| Math.Abs(o.SlowMagnitudeBonus) > 0.001f || o.PoisonTickBonus != 0
						|| Math.Abs(o.RangeBonus) > 0.001f || Math.Abs(o.CooldownMultiplier - 1f) > 0.001f;
					string flags = other ? "" : "DMG-ONLY";
					GD.Print($"AUDIT {spell.Id} | {milestone} | {o.Id} | {o.DamageMultiplier:0.##} (+{o.DamageBonus}) | "
						+ $"{o.CooldownMultiplier:0.##} | {rate:0.##} | {o.ProjectileCountBonus} | {o.AreaMultiplier:0.##} | "
						+ $"{o.SpeedMultiplier:0.##} | {o.ScaleMultiplier:0.##} | {(o.Effect == SpellEffect.None ? "-" : $"{o.Effect} {o.EffectValue:0.##}")} | "
						+ $"{(o.Rider == ElementRider.None ? "-" : o.Rider.ToString())} | {flags}");
				}
			}
		}
		GetTree().Quit();
	}
}
