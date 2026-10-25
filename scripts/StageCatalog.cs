using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>How a chapter is opened.</summary>
public enum StageGate
{
	/// <summary>Always available. Somewhere to play on a fresh save.</summary>
	Open,
	/// <summary>Opened by defeating the previous chapter's boss.</summary>
	PreviousBoss,
	/// <summary>Opened only once every spell and every wizard has been recovered.</summary>
	CampaignComplete,
}

public sealed class StageDefinition
{
	/// <summary>Stable id, "stage_0" upward. Matches <c>RunResult.StageId</c> and the save's unlocked list.</summary>
	public string Id { get; init; } = string.Empty;
	/// <summary>Index into the roster. Equals <c>Global.SelectedStageIdx</c>.</summary>
	public int Index { get; init; }
	public string DisplayName { get; init; } = string.Empty;
	public string TerrainCategory { get; init; } = string.Empty;
	/// <summary>What the place is - terrain and mood, the line the run itself opens on.</summary>
	public string FlavorText { get; init; } = string.Empty;
	/// <summary>
	/// What the dark wizard has done to it. Every chapter is somewhere he holds a prisoner
	/// (.ai/world-and-tone.md), and the stage list is the only screen where that is ever said out
	/// loud - without it a chapter reads as a texture swap rather than a place worth taking back.
	/// It is the card's only line under the name, so keep it to one short sentence (under ten words).
	/// </summary>
	public string CorruptionText { get; init; } = string.Empty;
	public StageEnvironmentKind EnvironmentKind { get; init; }
	public StageGate Gate { get; init; }
	/// <summary>
	/// Why the chapter is closed, in one line. Derived from the campaign order rather than written
	/// per chapter: it used to be a literal on each entry ("Locked — clear the Cursed Dungeon"),
	/// which is a statement about the ORDER, and the first time the order moved every one of them
	/// would have named the wrong chapter.
	/// </summary>
	public string LockedHint => StageCatalog.LockedHintFor(this);
	/// <summary>False while a chapter has no boss or content yet - it is listed, but not enterable.</summary>
	public bool IsPlayable { get; init; } = true;
}

/// <summary>
/// The campaign's chapters, and the single authority on what a stage is called.
/// </summary>
/// <remarks>
/// Stage naming was previously hardcoded in four places that had already drifted apart:
/// <c>StageSelection</c> knew two stages, <c>GameOverScreen.StageDisplayName</c> knew three,
/// <c>Node2DGame</c> had ten names and ten flavour strings, and
/// <c>StageEnvironmentCatalog.GetForStageIndex</c> mapped ten indices onto seven environments with
/// no relation to any of them. All of those become readers of this list.
///
/// The roster is a list, not a fixed set: post-final bonus chapters append, and nothing here
/// assumes eight.
/// </remarks>
public static class StageCatalog
{
	private static readonly StageDefinition[] Stages =
	{
		new()
		{
			Id = "stage_0", Index = 0,
			DisplayName = "Enchanted Forest",
			TerrainCategory = "Forest path",
			FlavorText = "A bright woodland trail where ancient trees and thick brush crowd the battlefield.",
			CorruptionText = "Elderbark's wood. Every path now leads inward.",
			EnvironmentKind = StageEnvironmentKind.Forest,
			Gate = StageGate.Open,
		},
		new()
		{
			Id = "stage_1", Index = 1,
			DisplayName = "Cursed Dungeon",
			TerrainCategory = "Dungeon stone",
			FlavorText = "Stone corridors and crumbling keeps make this a grim choke-point of ruin and shadow.",
			CorruptionText = "He keeps a wizard in one of these cells.",
			EnvironmentKind = StageEnvironmentKind.Castle,
			Gate = StageGate.PreviousBoss,
		},
		// Every chapter below now has a boss in BossCatalog, which is what IsPlayable was waiting
		// on: a chapter that ends on the timer with nothing to kill is a free non-victory, and
		// that was the reason these were listed but closed. What they still share is one tile set
		// and one enemy roster per environment - the fights differ, the ground does not yet.
		new()
		{
			Id = "stage_2", Index = 2,
			DisplayName = "Sunken Cave",
			TerrainCategory = "Wet stone",
			FlavorText = "Dripping tunnels of black rock, where the only light is the one you brought.",
			CorruptionText = "His shadow followed the water down into the dark.",
			EnvironmentKind = StageEnvironmentKind.Cave,
			Gate = StageGate.PreviousBoss,
		},
		new()
		{
			Id = "stage_3", Index = 3,
			DisplayName = "Blighted Swamp",
			TerrainCategory = "Bog and reed",
			FlavorText = "Reeds whisper over murky water while bog lanterns glow through the fog.",
			CorruptionText = "The lanterns still burn. Nothing living lights them.",
			EnvironmentKind = StageEnvironmentKind.Swamp,
			Gate = StageGate.PreviousBoss,
		},
		new()
		{
			Id = "stage_4", Index = 4,
			DisplayName = "Mystic Ruins",
			TerrainCategory = "Shattered stone",
			FlavorText = "Broken spires and shattered walls form a harsh field of rubble and ancient danger.",
			CorruptionText = "The old wards were broken from the inside.",
			EnvironmentKind = StageEnvironmentKind.Ruins,
			Gate = StageGate.PreviousBoss,
		},
		new()
		{
			Id = "stage_5", Index = 5,
			DisplayName = "Frozen Waste",
			TerrainCategory = "Ice field",
			FlavorText = "Ice-slick ground and wind-carved ridges make every step a balancing act.",
			CorruptionText = "A wizard lies under the ice, and the cold stands guard.",
			EnvironmentKind = StageEnvironmentKind.Ice,
			Gate = StageGate.PreviousBoss,
		},
		new()
		{
			Id = "stage_6", Index = 6,
			DisplayName = "Scorched Sands",
			TerrainCategory = "Desert waste",
			FlavorText = "A blistering desert expanse of cracked earth and long shadows.",
			CorruptionText = "He hid the water. The dead still walk the trade road.",
			EnvironmentKind = StageEnvironmentKind.Desert,
			Gate = StageGate.PreviousBoss,
		},
		new()
		{
			Id = "stage_7", Index = 7,
			DisplayName = "The Emberdeep",
			TerrainCategory = "Volcanic rock",
			FlavorText = "Blackened ground and glowing embers mark the last road, and what waits at its end.",
			CorruptionText = "His seat, and the end of the road.",
			EnvironmentKind = StageEnvironmentKind.Volcanic,
			Gate = StageGate.CampaignComplete,
		},
		// THE ONE CHAPTER OUTSIDE THE CAMPAIGN, and the reason the roster is a list rather than
		// a fixed eight. It is open from the first run and gates nothing, so it can sit after the
		// finale in the roster without implying it comes after it in the story.
		//
		// Every sprite in it - eight enemies, a boss and its projectile - was drawn by Eric's
		// kids. It has no CorruptionText because there is nothing here the dark wizard did: the
		// field exists to say what he has taken from a place, and he has not been to this one.
		new()
		{
			Id = "stage_8", Index = 8,
			DisplayName = "The Sketchbook",
			TerrainCategory = "Squared paper",
			FlavorText = "A world drawn in a workbook, loose.",
			EnvironmentKind = StageEnvironmentKind.Sketchbook,
			Gate = StageGate.Open,
		},
		// THE TWO CHAPTERS THAT OPEN EARLY (.ai/campaign.md). Eric, 2026-10-24: "cursed dungeon
		// should be a later level. there should be another few levels that open up earlier." They
		// sit second and third in CampaignOrder below, between the Forest and the Cave, and their
		// ids are new rather than renumbered: stage ids are in every save and every unlock rule, so
		// a chapter appends here and takes its place in the campaign through the order alone.
		//
		// Not playable until Phase 2 builds them. A closed chapter is skipped by the order - the
		// Forest's boss opens the Cave until these exist - so nobody can be stopped by one, and
		// flipping IsPlayable is the LAST step of building each, not the first.
		new()
		{
			Id = "stage_9", Index = 9,
			DisplayName = "The Fallow Fields",
			TerrainCategory = "Stubble and hedgerow",
			FlavorText = "Open fields gone to stubble and thistle between black hedgerows, and a well in the middle of them.",
			CorruptionText = "He takes a tithe of everything that grows here.",
			EnvironmentKind = StageEnvironmentKind.Fields,
			Gate = StageGate.PreviousBoss,
		},
		new()
		{
			Id = "stage_10", Index = 10,
			DisplayName = "The Broken Circle",
			TerrainCategory = "Heath and standing stone",
			FlavorText = "A high heath of heather and granite, ringed by stones that are older than the wizards.",
			CorruptionText = "The circle where the wizards met. He came here first.",
			EnvironmentKind = StageEnvironmentKind.Heath,
			Gate = StageGate.PreviousBoss,
		},
	};

	// THE CAMPAIGN, IN THE ORDER IT IS PLAYED - and the only place that order is written.
	//
	// It is deliberately a list of ids and not the order of the roster above. Stage ids and their
	// indices are in every save (UnlockedStageIds, BestTimeByStage, the run log), in the unlock
	// rules (UnlockCatalog's secret reveals) and in every harness flag (--stage=N), so a chapter can
	// never be renumbered to move it. Moving one is editing this list and nothing else: which boss
	// opens which chapter, the locked hint, the stage select's order and the deeds' order are all
	// read from it.
	//
	// The Cursed Dungeon is fifth, after four open chapters (.ai/campaign.md): a maze is a harder
	// ask than an open field, and as the second chapter it was also the second-toughest spawn table
	// in the game. The final chapter is last and is gated on CampaignComplete, not on a boss.
	// Chapters outside the campaign (the Sketchbook) are not listed; they must be Open.
	private static readonly string[] CampaignOrder =
	{
		"stage_0",  // 1  Enchanted Forest - Elderbark
		"stage_9",  // 2  The Fallow Fields - the Hayward (Phase 2)
		"stage_10", // 3  The Broken Circle - the Ninth Stone (Phase 2)
		"stage_2",  // 4  Sunken Cave - the Hollow Choir
		"stage_1",  // 5  Cursed Dungeon - the Gaoler
		"stage_3",  // 6  Blighted Swamp - Mother Rot
		"stage_4",  // 7  Mystic Ruins - the Archivist
		"stage_5",  // 8  Frozen Waste - the Still Warden
		"stage_6",  // 9  Scorched Sands - the Long Coil
		"stage_7",  // 10 The Emberdeep - the Warden of the Deep (every spell and wizard first)
	};

	private static StageDefinition[] campaign;

	public static IReadOnlyList<StageDefinition> All => Stages;

	public static int Count => Stages.Length;

	/// <summary>The campaign's chapters in the order they are played. Excludes chapters outside it.</summary>
	public static IReadOnlyList<StageDefinition> Campaign =>
		campaign ??= CampaignOrder.Select(GetById).Where(s => s != null).ToArray();

	/// <summary>The raw order, ids as written, for validation - including any id that names no stage.</summary>
	public static IReadOnlyList<string> CampaignOrderIds => CampaignOrder;

	/// <summary>
	/// Every stage in the order the stage select lists them: the campaign as it is played, then the
	/// chapters outside it in roster order.
	/// </summary>
	public static IEnumerable<StageDefinition> InDisplayOrder =>
		Campaign.Concat(Stages.Where(s => !IsInCampaign(s)));

	/// <summary>0-based position in the campaign, or -1 for a chapter outside it.</summary>
	public static int CampaignPositionOf(string stageId)
	{
		if (string.IsNullOrWhiteSpace(stageId))
			return -1;
		IReadOnlyList<StageDefinition> order = Campaign;
		for (int i = 0; i < order.Count; i++)
		{
			if (order[i].Id.Equals(stageId.Trim(), StringComparison.OrdinalIgnoreCase))
				return i;
		}
		return -1;
	}

	public static bool IsInCampaign(StageDefinition stage) => stage != null && CampaignPositionOf(stage.Id) >= 0;

	/// <summary>The campaign's last chapter - the one his seat is in.</summary>
	public static StageDefinition FinalChapter => Campaign.Count > 0 ? Campaign[Campaign.Count - 1] : null;

	/// <summary>
	/// The chapter whose boss opens <paramref name="stage"/>: the nearest PLAYABLE chapter before it
	/// in the campaign. Null for the first chapter, for a chapter outside the campaign, and for one
	/// that is not boss-gated.
	/// </summary>
	/// <remarks>
	/// Playable, because a chapter that is not built yet has no boss anyone can beat. Skipping it is
	/// what lets the two new early chapters sit in the order before they exist: until they do, the
	/// Forest opens the Cave, exactly as if they were not there.
	/// </remarks>
	public static StageDefinition PreviousChapter(StageDefinition stage)
	{
		if (stage == null || stage.Gate != StageGate.PreviousBoss)
			return null;
		int position = CampaignPositionOf(stage.Id);
		for (int i = position - 1; i >= 0; i--)
		{
			if (Campaign[i].IsPlayable)
				return Campaign[i];
		}
		return null;
	}

	/// <summary>
	/// The chapter that beating <paramref name="stageId"/>'s boss opens: the nearest playable,
	/// boss-gated chapter after it in the campaign. Null when the next chapter is opened some other
	/// way (the finale) or the stage is outside the campaign.
	/// </summary>
	public static StageDefinition ChapterOpenedBy(string stageId)
	{
		int position = CampaignPositionOf(stageId);
		if (position < 0)
			return null;
		for (int i = position + 1; i < Campaign.Count; i++)
		{
			StageDefinition next = Campaign[i];
			if (!next.IsPlayable)
				continue;
			return next.Gate == StageGate.PreviousBoss ? next : null;
		}
		return null;
	}

	/// <summary>The line a closed chapter's lock gives, derived from the order. See <see cref="StageDefinition.LockedHint"/>.</summary>
	public static string LockedHintFor(StageDefinition stage)
	{
		if (stage == null)
			return string.Empty;
		if (!stage.IsPlayable)
			return "Not yet open - this chapter is still being built";

		switch (stage.Gate)
		{
			case StageGate.CampaignComplete:
				return "Sealed — recover every spell and free every wizard";
			case StageGate.PreviousBoss:
				StageDefinition previous = PreviousChapter(stage);
				if (previous == null)
					return "Locked";
				BossDefinition boss = BossCatalog.ForStageIndex(previous.Index);
				return boss != null
					? $"Locked — defeat {boss.DisplayName} in the {StripArticle(previous.DisplayName)}"
					: $"Locked — clear the {StripArticle(previous.DisplayName)}";
			default:
				return string.Empty;
		}
	}

	// "the The Emberdeep" is what a hint built from a name that already carries its article reads as.
	private static string StripArticle(string name) =>
		name != null && name.StartsWith("The ", StringComparison.Ordinal) ? name.Substring(4) : name;

	public static StageDefinition GetByIndex(int index)
	{
		if (Stages.Length == 0)
			return null;

		// Clamped rather than null-returning: every caller here is a display path reached from a
		// stage index that some other system already chose, and a missing name should degrade to
		// the first chapter's rather than crash a run that is otherwise fine.
		int clamped = Math.Clamp(index, 0, Stages.Length - 1);
		return Stages[clamped];
	}

	public static StageDefinition GetById(string stageId)
	{
		if (string.IsNullOrWhiteSpace(stageId))
			return null;

		return Stages.FirstOrDefault(s => s.Id.Equals(stageId.Trim(), StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Display name for a stage id, falling back to the raw id so a log line is never blank.</summary>
	public static string DisplayNameFor(string stageId)
	{
		StageDefinition definition = GetById(stageId);
		return definition != null ? definition.DisplayName : stageId;
	}
}
