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
	/// <summary>
	/// A gate chapter: opened by defeating the previous chapter's boss AND freeing every wizard whose
	/// rescue comes before this chapter (GlobalStatsManager.GateWizardIds) - written per chapter, so the
	/// Emberdeep counts the wizards on the road and a later gate (the castle's Throne) counts those freed
	/// before it (.ai/achievements-and-secrets.md 2.5, .ai/endgame-castle.md 1.6). It replaced
	/// CampaignComplete, "every spell and every wizard", at schema 19: spells are earned in secrets now,
	/// a secret is missable, and the old gate could not even be met - Fireball was paid only behind it.
	/// The boss alone never writes the chapter open (StageCatalog.ChapterOpenedBy skips it).
	/// </summary>
	PreviousBossAndWizards,
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
			// The Long Coil beaten and every wizard on the road freed (since schema 19).
			Gate = StageGate.PreviousBossAndWizards,
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
		// THE CASTLE (.ai/endgame-castle.md): his seat, across the water from the end of the road. Three
		// chapters after the Emberdeep - the Last Bridge, the Court and the Throne - with the Cursed
		// Dungeon moved in between the first two by piece C2 (its line in CampaignOrder, not here). New
		// ids, appended, for the reason the early chapters' were (.ai/campaign.md section 3).
		//
		// Not playable until each is built (section 9). A closed chapter is skipped by the order, so the
		// Warden of the Deep opens nothing until the bridge exists and nobody is stopped by one. Each
		// entry below is its own chapter's: its builder flips IsPlayable last (one line), and the
		// validator's campaign to-do line for it must be gone first.
		new()
		{
			Id = "stage_11", Index = 11,
			DisplayName = "The Last Bridge",
			TerrainCategory = "Bridge deck",
			FlavorText = "A long stone bridge across the dark sea to the castle on its rock, and it falls in behind you.",
			CorruptionText = "His seat is across the water.",
			EnvironmentKind = StageEnvironmentKind.Bridge,
			Gate = StageGate.PreviousBoss,
			IsPlayable = false,
		},
		new()
		{
			Id = "stage_12", Index = 12,
			DisplayName = "The Court",
			TerrainCategory = "Castle halls",
			FlavorText = "The halls of his house: galleries, a great hall laid for a feast, and rooms behind rooms.",
			CorruptionText = "A court with no one left in it.",
			EnvironmentKind = StageEnvironmentKind.Court,
			Gate = StageGate.PreviousBoss,
			IsPlayable = false,
		},
		new()
		{
			Id = "stage_13", Index = 13,
			DisplayName = "The Throne",
			TerrainCategory = "Throne hall",
			FlavorText = "One hall of black stone, its doors shut behind you, and the throne at its end.",
			CorruptionText = "He is here.",
			EnvironmentKind = StageEnvironmentKind.Throne,
			// The second gate chapter (.ai/endgame-castle.md 1.6): the Seneschal beaten AND every wizard
			// freed. "Every wizard whose rescue comes before this chapter" (GlobalStatsManager.GateWizardIds)
			// is every wizard here, Tituba included, so "he is reachable only when every wizard is free" is
			// true exactly where he is.
			Gate = StageGate.PreviousBossAndWizards,
			IsPlayable = false,
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
	// in the game. The Emberdeep is the first gate chapter (PreviousBossAndWizards): its previous boss
	// AND the wizards freed before it. The castle comes after it (.ai/endgame-castle.md), boss-gated,
	// and ends on a second gate chapter, the Throne; the last chapter is always a gate. Chapters
	// outside the campaign (the Sketchbook) are not listed; they must be Open.
	//
	// The castle's lines are written now and skipped until each chapter is playable. The Dungeon's
	// own line moves between the Last Bridge and the Court in piece C2 (.ai/endgame-castle.md 1.3),
	// which is why the castle's three are kept a line apart below: C2's edit lands on a line of its
	// own and never on one of theirs.
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
		"stage_7",  // 10 The Emberdeep - the Warden of the Deep (the Long Coil and every wizard on the road first)
		"stage_11", // 11 The Last Bridge - the Constable (castle, not yet playable)
		// (the Cursed Dungeon's line comes here when C2 moves it into the castle)
		"stage_12", // 12 The Court - the Seneschal (castle, not yet playable)
		"stage_13", // 13 The Throne - the procession, then him (the Seneschal and every wizard first; not yet playable)
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
	/// The first gate chapter in the campaign (<see cref="StageGate.PreviousBossAndWizards"/>): the
	/// Emberdeep. "The road" is everything before it. Not <see cref="FinalChapter"/>, which stays "last"
	/// once the castle's chapters come after the gate (.ai/endgame-castle.md 1.9).
	/// </summary>
	public static StageDefinition GateChapter => Campaign.FirstOrDefault(s => s.Gate == StageGate.PreviousBossAndWizards);

	/// <summary>Every gate chapter in the campaign, in order: the Emberdeep today, the Throne later.</summary>
	public static IEnumerable<StageDefinition> GateChapters => Campaign.Where(s => s.Gate == StageGate.PreviousBossAndWizards);

	/// <summary>True for a gate whose previous chapter's boss is part of it.</summary>
	public static bool IsBossGated(StageGate gate) => gate == StageGate.PreviousBoss || gate == StageGate.PreviousBossAndWizards;

	/// <summary>
	/// The chapter whose boss opens <paramref name="stage"/> (or, for a gate chapter, is half its gate):
	/// the nearest PLAYABLE chapter before it in the campaign. Null for the first chapter, for a chapter
	/// outside the campaign, and for one that is not boss-gated.
	/// </summary>
	/// <remarks>
	/// Playable, because a chapter that is not built yet has no boss anyone can beat. Skipping it is
	/// what lets the two new early chapters sit in the order before they exist: until they do, the
	/// Forest opens the Cave, exactly as if they were not there.
	/// </remarks>
	public static StageDefinition PreviousChapter(StageDefinition stage)
	{
		if (stage == null || !IsBossGated(stage.Gate))
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
	/// way or the stage is outside the campaign - and for a gate chapter, whose boss is only half of
	/// it: a victory that wrote the gate into the opened list would open it without the wizards.
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
			case StageGate.PreviousBossAndWizards:
			{
				// "Sealed - defeat the Long Coil in the Scorched Sands and free every wizard on the road"
				// (.ai/endgame-castle.md 1.6). Which wizards are still held is the save's to say; the world
				// map's card counts them under this line.
				StageDefinition before = PreviousChapter(stage);
				BossDefinition gateBoss = before != null ? BossCatalog.ForStageIndex(before.Index) : null;
				string road = stage == GateChapter ? " on the road" : string.Empty;
				return gateBoss != null
					? $"Sealed — defeat {MidSentence(gateBoss.DisplayName)} in the {StripArticle(before.DisplayName)} and free every wizard{road}"
					: $"Sealed — free every wizard{road}";
			}
			case StageGate.PreviousBoss:
				StageDefinition previous = PreviousChapter(stage);
				if (previous == null)
					return "Locked";
				BossDefinition boss = BossCatalog.ForStageIndex(previous.Index);
				// "defeat the Hayward", not "defeat The Hayward": a name's article is lower case mid-sentence.
				// The world map shows every locked chapter's hint, where the old list hid them all.
				return boss != null
					? $"Locked — defeat {MidSentence(boss.DisplayName)} in the {StripArticle(previous.DisplayName)}"
					: $"Locked — clear the {StripArticle(previous.DisplayName)}";
			default:
				return string.Empty;
		}
	}

	// "the The Emberdeep" is what a hint built from a name that already carries its article reads as.
	private static string StripArticle(string name) =>
		name != null && name.StartsWith("The ", StringComparison.Ordinal) ? name.Substring(4) : name;

	private static string MidSentence(string name) =>
		name != null && name.StartsWith("The ", StringComparison.Ordinal) ? "the " + name.Substring(4) : name;

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
