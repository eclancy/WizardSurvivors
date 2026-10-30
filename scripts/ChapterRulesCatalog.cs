using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// WHICH CHAPTERS HAVE RULES OF THEIR OWN (ChapterRules, .ai/endgame-castle.md 1.9), and the one place
// that is decided. The pattern is SideEventDirector.ForStage's: a line per chapter that needs one,
// written by the plumbing as null, so each chapter's agent replaces ONE line and writes one file of its
// own - BridgeRules.cs, CourtRules.cs, ThroneRules.cs - and never edits Node2DGame.
//
// The lines are a comment apart, so two agents' edits are never on adjacent lines (what git calls a
// conflict). A chapter not listed has no rules, which is every chapter of the road: the Cursed Dungeon
// moves into the castle with none, because its maze, its key and its portal are the road's own.
public static class ChapterRulesCatalog
{
	/// <summary>A new rules object for one run of the chapter, or null for a chapter with none.</summary>
	public static Func<ChapterRules> ForStage(string stageId) => stageId switch
	{
		// The Last Bridge (piece C1): becomes () => new BridgeRules() - the collapsing front, the spawns on
		// the band, the Constable at the gate, the gate as the way home, the strip under the timer.
		"stage_11" => null,
		// (the Court's line is below; leave this one as it is)
		// The Court (piece C3): its rooms, the boss in a room near the player, the throne room doors as the
		// way home (CourtRules.cs).
		"stage_12" => () => new CourtRules(),
		// (the Throne's line is below; leave this one as it is)
		// The Throne (piece C4): becomes () => new ThroneRules() - the Muster, the procession, the pinned
		// pace clock.
		"stage_13" => null,
		_ => null,
	};

	/// <summary>
	/// The chapters whose rules are designed and still to be written: the validator's campaign to-do list
	/// names each until its line above is filled, and calls it a fault if the chapter is playable first.
	/// A chapter leaves this list never - filling its line is what ends the to-do.
	/// </summary>
	public static IReadOnlyList<string> Designed => DesignedIds;

	private static readonly string[] DesignedIds = { "stage_11", "stage_12", "stage_13" };

	/// <summary>True for a chapter whose rules are designed but whose line is still the null placeholder.</summary>
	public static bool IsAwaitingRules(string stageId) =>
		ForStage(stageId) == null && Array.IndexOf(DesignedIds, stageId) >= 0;
}
