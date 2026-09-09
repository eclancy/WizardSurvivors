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
	/// </summary>
	public string CorruptionText { get; init; } = string.Empty;
	public StageEnvironmentKind EnvironmentKind { get; init; }
	public StageGate Gate { get; init; }
	/// <summary>Shown on the card while the chapter is closed, so a lock always explains itself.</summary>
	public string LockedHint { get; init; } = string.Empty;
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
			CorruptionText = "Elderbark kept this wood for a thousand years. It answers to him now, and every path it grows leads inward.",
			EnvironmentKind = StageEnvironmentKind.Forest,
			Gate = StageGate.Open,
		},
		new()
		{
			Id = "stage_1", Index = 1,
			DisplayName = "Cursed Dungeon",
			TerrainCategory = "Dungeon stone",
			FlavorText = "Stone corridors and crumbling keeps make this a grim choke-point of ruin and shadow.",
			CorruptionText = "The keep's cells were cut for grain. He keeps a wizard in one of them, and something he made in all the rest.",
			EnvironmentKind = StageEnvironmentKind.Castle,
			Gate = StageGate.PreviousBoss,
			LockedHint = "Locked — defeat Elderbark in the Enchanted Forest",
		},
		// Chapters 2 through 6 are listed so the campaign's shape is visible from the first run,
		// but they are not enterable yet: none has a boss, and a chapter that ends on the timer
		// with nothing to kill is the free non-victory the Cursed Dungeon already suffers from.
		// Flipping IsPlayable is the last step of building each one, not the first.
		new()
		{
			Id = "stage_2", Index = 2,
			DisplayName = "Sunken Cave",
			TerrainCategory = "Wet stone",
			FlavorText = "Dripping tunnels of black rock, where the only light is the one you brought.",
			CorruptionText = "His shadow followed the water down to where no light ever reached, and whatever digs in the dark has not come up since.",
			EnvironmentKind = StageEnvironmentKind.Cave,
			Gate = StageGate.PreviousBoss,
			LockedHint = "Locked — clear the Cursed Dungeon",
			IsPlayable = false,
		},
		new()
		{
			Id = "stage_3", Index = 3,
			DisplayName = "Blighted Swamp",
			TerrainCategory = "Bog and reed",
			FlavorText = "Reeds whisper over murky water while bog lanterns glow through the fog.",
			CorruptionText = "The bog lanterns still burn, but nothing living lights them. The fog is his, and it counts everyone who walks in.",
			EnvironmentKind = StageEnvironmentKind.Swamp,
			Gate = StageGate.PreviousBoss,
			LockedHint = "Locked — clear the Sunken Cave",
			IsPlayable = false,
		},
		new()
		{
			Id = "stage_4", Index = 4,
			DisplayName = "Mystic Ruins",
			TerrainCategory = "Shattered stone",
			FlavorText = "Broken spires and shattered walls form a harsh field of rubble and ancient danger.",
			CorruptionText = "Wards that held for an age were broken from the inside, by someone who knew where every last one of them was buried.",
			EnvironmentKind = StageEnvironmentKind.Ruins,
			Gate = StageGate.PreviousBoss,
			LockedHint = "Locked — clear the Blighted Swamp",
			IsPlayable = false,
		},
		new()
		{
			Id = "stage_5", Index = 5,
			DisplayName = "Frozen Waste",
			TerrainCategory = "Ice field",
			FlavorText = "Ice-slick ground and wind-carved ridges make every step a balancing act.",
			CorruptionText = "He put a wizard under the ice and let the cold stand guard. The wind here carries a voice, and it is not the wind's.",
			EnvironmentKind = StageEnvironmentKind.Ice,
			Gate = StageGate.PreviousBoss,
			LockedHint = "Locked — clear the Mystic Ruins",
			IsPlayable = false,
		},
		new()
		{
			Id = "stage_6", Index = 6,
			DisplayName = "Scorched Sands",
			TerrainCategory = "Desert waste",
			FlavorText = "A blistering desert expanse of cracked earth and long shadows.",
			CorruptionText = "He took the water and hid it, so nothing grows. The dead still walk the trade road, still carrying the loads they died under.",
			EnvironmentKind = StageEnvironmentKind.Desert,
			Gate = StageGate.PreviousBoss,
			LockedHint = "Locked — clear the Frozen Waste",
			IsPlayable = false,
		},
		new()
		{
			Id = "stage_7", Index = 7,
			DisplayName = "The Emberdeep",
			TerrainCategory = "Volcanic rock",
			FlavorText = "Blackened ground and glowing embers mark the last road, and what waits at its end.",
			CorruptionText = "His seat. Every spell he stole and every wizard he took came through here, and he has never once needed to leave it.",
			EnvironmentKind = StageEnvironmentKind.Volcanic,
			Gate = StageGate.CampaignComplete,
			LockedHint = "Sealed — recover every spell and free every wizard",
			IsPlayable = false,
		},
	};

	public static IReadOnlyList<StageDefinition> All => Stages;

	public static int Count => Stages.Length;

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
