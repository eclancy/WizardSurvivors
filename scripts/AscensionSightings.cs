using Godot;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>
/// Which ascension paths the player has been allowed to read. A path is SEALED - "???", no effect,
/// no icon - until the first level-up that finds its requirements met, and revealed for good from
/// then on, in this run and every later one, whether or not it was taken.
/// </summary>
/// <remarks>
/// The player's call: "the ascension effects and icons shouldn't be visible until the player meets
/// the requirements for the first time. once they've seen it (even if they don't pick it), then it
/// can be always visible." The browser had listed every branch of every spell in full from the
/// first level-up, so the biggest reveal in a run was a list read on minute two.
///
/// TWO HALVES, KEPT APART. Whether a path is shown is <see cref="IsRevealed"/>: met right now, or
/// seen before. Whether it is REMEMBERED is <see cref="Record"/>, called once per level-up by
/// Node2DGame. The display never depends on the record having happened - a path whose requirements
/// are met is shown on that very level-up - so a run where nothing may be written (every harness)
/// still draws exactly what a player would see.
///
/// "Met" is Player.BuildAscensionPreview's IsAvailable for an Ascension (spell at its card cap, the
/// element gate, the player level) and being offered at all for an Apotheosis form. Nothing here
/// restates either rule.
/// </remarks>
public static class AscensionSightings
{
	/// <summary>Shown in full: its requirements hold now, or have held once before.</summary>
	public static bool IsRevealed(Node context, string spellId, string branchId, bool metNow)
	{
		if (metNow)
			return true;
		SaveData data = context?.GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		return data != null && data.IsAscensionSeen(spellId, branchId);
	}

	/// <summary>
	/// Remembers every path met on this level-up and saves if any was new. Returns how many were.
	/// </summary>
	/// <param name="equipped">The browser's view of the tome (Player.BuildAscensionPreview per spell).</param>
	/// <param name="offers">The strip (Player.GetAscensionOffers): an Apotheosis form is met when offered.</param>
	/// <param name="revealed">
	/// Optional. Every path seen for the first time is added to it, so the results screen can show
	/// what the run revealed (GameOverScreen). Nothing is added on a run that remembers nothing.
	/// </param>
	public static int Record(Node context, IEnumerable<EquippedSpellInfo> equipped, IEnumerable<LevelUpOption> offers,
		List<RunUnlockEntry> revealed = null)
	{
		if (context == null || IsToolRun(context))
			return 0;
		var saveManager = context.GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager?.Data == null)
			return 0;

		int added = 0;
		foreach (EquippedSpellInfo spell in equipped ?? Enumerable.Empty<EquippedSpellInfo>())
		{
			if (spell?.Ascensions == null)
				continue;
			foreach (AscensionInfo branch in spell.Ascensions)
			{
				if (branch == null || !branch.IsAvailable || !saveManager.Data.MarkAscensionSeen(spell.Id, branch.Id))
					continue;
				added++;
				revealed?.Add(new RunUnlockEntry
				{
					Kind = RunUnlockKind.Ascension,
					Id = $"{spell.Id}:{branch.Id}",
					Name = branch.DisplayName,
					Source = $"Ascension of {spell.DisplayName}",
					SpellId = spell.Id,
					// The path's own picture (EvolutionIcons); its spell's, which Player puts on the branch, if none.
					IconPath = EvolutionIcons.PathOrFallback(branch.Id, branch.Icon?.ResourcePath),
				});
			}
		}

		foreach (LevelUpOption offer in offers ?? Enumerable.Empty<LevelUpOption>())
		{
			if (offer == null || offer.MilestoneLevel < ApotheosisCatalog.MilestoneLevel || offer.EvolutionChoices == null)
				continue;
			foreach (SpellEvolutionOption form in offer.EvolutionChoices)
			{
				if (form == null || !saveManager.Data.MarkAscensionSeen(offer.SpellId, form.Id))
					continue;
				added++;
				revealed?.Add(new RunUnlockEntry
				{
					Kind = RunUnlockKind.Apotheosis,
					Id = $"{offer.SpellId}:{form.Id}",
					Name = form.DisplayName,
					Source = $"Apotheosis of {offer.DisplayName}",
					SpellId = offer.SpellId,
					IconPath = EvolutionIcons.PathOrFallback(form.Id, form.Icon?.ResourcePath),
				});
			}
		}

		// Written the moment it happens, not at the end of the run: a run quit from the pause screen
		// never reaches the game-over save, and the reveal is the thing being promised.
		if (added > 0)
			saveManager.SaveGame();
		return added;
	}

	/// <summary>
	/// True when a harness is driving the game rather than a player: the current scene is one of the
	/// `_` tool scenes (_BalanceHarness, _BossProbe, _UiShot and the rest), which instance the game
	/// as a child of themselves.
	/// </summary>
	/// <remarks>
	/// SaveManager.WritesDisabled is not enough on its own: the balance bot without --all-unlocked
	/// and the boss probe still end runs into the real save, and a bot that met a path would reveal
	/// it to the player who never saw it. Every tool scene in this project is named with a leading
	/// underscore, which is what this reads.
	/// </remarks>
	public static bool IsToolRun(Node context)
	{
		string path = context?.GetTree()?.CurrentScene?.SceneFilePath ?? string.Empty;
		return path.StartsWith("res://scenes/_");
	}
}
