using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// One stage's end-of-run boss. A stage without an entry here still ends at
/// <c>Node2DGame.TimerVictorySeconds</c> the old way, so stages can gain bosses one at a time.
/// </summary>
public sealed class BossDefinition
{
	/// <summary>
	/// Stable id written into <c>RunResult.BossId</c>, and matched EXACTLY by the achievement that
	/// rewards the kill. It used to be matched by substring against terms like "forest" and
	/// "castle", which meant a new boss whose id happened to contain another chapter's keyword
	/// would silently grant the wrong spell.
	/// </summary>
	public string Id { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	/// <summary>One line shown under the name on the arrival banner: what this boss punishes.</summary>
	public string Tagline { get; init; } = string.Empty;
	/// <summary>The boss's own scene - or, for a boss not built yet, where it will be.</summary>
	public string ScenePath { get; init; } = string.Empty;
	/// <summary>
	/// What fights in its place until <see cref="ScenePath"/> exists: another chapter's boss scene,
	/// spawned under this boss's id and name so its victory, achievement and unlock are this
	/// chapter's. Empty for a boss that was built with its chapter. The same pattern as a planned
	/// bestiary creature (StageBestiary): building the boss is creating the scene at ScenePath, and
	/// nothing else has to change for the chapter to switch to it.
	/// </summary>
	public string StandInScenePath { get; init; } = string.Empty;
	/// <summary>Health before the balance preset's elite scaling is applied.</summary>
	public int Health { get; init; } = 1000;
	/// <summary>Flat Arcane Energy added on top of the usual run reward for the kill.</summary>
	public int ArcaneVictoryBonus { get; init; } = 120;

	/// <summary>True while the boss's own scene does not exist and its stand-in fights instead.</summary>
	public bool IsOnStandIn =>
		!string.IsNullOrEmpty(StandInScenePath)
		&& (string.IsNullOrEmpty(ScenePath) || !ResourceLoader.Exists(ScenePath));

	/// <summary>The scene a run actually spawns: its own once it exists, the stand-in until then.</summary>
	public string SpawnScenePath => IsOnStandIn ? StandInScenePath : ScenePath;

	// There used to be an UnlocksStageId here, one per boss. Which chapter a victory opens is a fact
	// about the campaign ORDER, not about the boss, and keeping it on the boss meant that moving the
	// Cursed Dungeon was four edits that had to agree. It is BossCatalog.ChapterOpenedBy now, read
	// from StageCatalog's order (.ai/campaign.md).
}

public static class BossCatalog
{
	// Keyed by Global.SelectedStageIdx, the same index StageSelection hands to the run.
	private static readonly Dictionary<int, BossDefinition> ByStageIndex = new()
	{
		[0] = new BossDefinition
		{
			Id = "forest_treant",
			DisplayName = "Elderbark, the Treant",
			Tagline = "Slow, immense, and impossible to burst down. Bring damage you can sustain.",
			ScenePath = "res://scenes/ForestTreantBoss.tscn",
			Health = 4200,
			ArcaneVictoryBonus = 150
		},
		[1] = new BossDefinition
		{
			Id = "castle_warden",
			DisplayName = "The Gaoler",
			Tagline = "It charges down a lane it shows you first. Step sideways, never back.",
			ScenePath = "res://scenes/GaolerBoss.tscn",
			// Tuned as chapter 2 (5200, 170); chapter 5 since the campaign order moved it
			// (.ai/campaign.md), after the Choir's 3 x 1900. Retuned for the slot as campaign.md
			// decision 3 proposed - between the Choir's 5700 / 190 and Mother Rot's 6800 / 210 - so
			// boss health climbs with the order again instead of dipping at the maze.
			Health = 6200,
			ArcaneVictoryBonus = 200
		},
		[2] = new BossDefinition
		{
			Id = "cave_choir",
			DisplayName = "The Hollow Choir",
			Tagline = "Three voices. Drop one and the others sing it back - they have to fall together.",
			ScenePath = "res://scenes/HollowChoirBoss.tscn",
			// A third each. Node2DGame sets this on the voice it spawns and that voice hands the
			// same number to the two it calls up, so the fight is three times what is written here.
			Health = 1900,
			ArcaneVictoryBonus = 190
		},
		[3] = new BossDefinition
		{
			Id = "swamp_mother",
			DisplayName = "Mother Rot",
			Tagline = "She heals from everything she sheds. Clear the spawn or the bar goes backwards.",
			ScenePath = "res://scenes/MotherRotBoss.tscn",
			Health = 6800,
			ArcaneVictoryBonus = 210
		},
		[4] = new BossDefinition
		{
			Id = "ruins_sentinel",
			DisplayName = "The Archivist",
			Tagline = "It never moves and its beam never stops. Keep orbiting, and watch where it surfaces.",
			ScenePath = "res://scenes/ArchivistBoss.tscn",
			Health = 7200,
			ArcaneVictoryBonus = 230
		},
		[5] = new BossDefinition
		{
			Id = "frost_warden",
			DisplayName = "The Still Warden",
			Tagline = "Frozen in place, and the arena does the chasing. The safe ground is not where it is.",
			ScenePath = "res://scenes/StillWardenBoss.tscn",
			Health = 7800,
			ArcaneVictoryBonus = 250
		},
		[6] = new BossDefinition
		{
			Id = "sands_coil",
			DisplayName = "The Long Coil",
			Tagline = "Only killable while it is above the sand. Bring something that spikes.",
			ScenePath = "res://scenes/LongCoilBoss.tscn",
			Health = 8200,
			ArcaneVictoryBonus = 270
		},
		[7] = new BossDefinition
		{
			Id = "emberdeep_warden",
			DisplayName = "The Warden of the Deep",
			Tagline = "Every fight you have already won, in sequence, with nothing in between.",
			ScenePath = "res://scenes/DeepWardenBoss.tscn",
			Health = 11000,
			ArcaneVictoryBonus = 400
		},
		[8] = new BossDefinition
		{
			Id = "kid_wizard",
			DisplayName = "Wizard Dude",
			Tagline = "Throws a fan of missiles down the wedge he shows you. Leave the wedge.",
			ScenePath = "res://scenes/KidWizardBoss.tscn",
			Health = 4600,
			// Opens nothing: the Sketchbook is outside the campaign order (StageCatalog), so
			// ChapterOpenedBy has no next chapter for it - beating it is never on the critical path.
			ArcaneVictoryBonus = 200
		},
		// THE TWO EARLY CHAPTERS' BOSSES (.ai/campaign.md). Designed, not built: each fights as
		// Elderbark under its own id and name until Phase 2 creates the scene at ScenePath, at which
		// point the chapter switches to it with no edit here. Health and bonus sit between the
		// Forest's and the Cave's, in campaign order; Phase 2 may tune them in its own block.
		[9] = new BossDefinition
		{
			Id = "fields_hayward",
			DisplayName = "The Hayward",
			Tagline = "He keeps his hurdle between you and him. Go round to his back.",
			ScenePath = "res://scenes/HaywardBoss.tscn",
			StandInScenePath = "res://scenes/ForestTreantBoss.tscn",
			Health = 4500,
			ArcaneVictoryBonus = 160
		},
		[10] = new BossDefinition
		{
			Id = "heath_ninth_stone",
			DisplayName = "The Ninth Stone",
			Tagline = "It closes the circle on you. Find the gap and get out before the middle falls.",
			ScenePath = "res://scenes/NinthStoneBoss.tscn",
			StandInScenePath = "res://scenes/ForestTreantBoss.tscn",
			Health = 5000,
			ArcaneVictoryBonus = 175
		}
	};

	/// <summary>
	/// The chapter that beating <paramref name="definition"/> opens, read from the campaign order
	/// (StageCatalog.ChapterOpenedBy). Null for a boss whose next chapter is not boss-gated - the
	/// Long Coil, whose next is the finale - and for one outside the campaign.
	/// </summary>
	public static StageDefinition ChapterOpenedBy(BossDefinition definition)
	{
		int index = StageIndexOf(definition);
		return index < 0 ? null : StageCatalog.ChapterOpenedBy(StageCatalog.GetByIndex(index).Id);
	}

	public static BossDefinition ForStageIndex(int stageIndex)
	{
		return ByStageIndex.TryGetValue(stageIndex, out BossDefinition definition) ? definition : null;
	}

	public static BossDefinition GetById(string bossId)
	{
		if (string.IsNullOrWhiteSpace(bossId))
			return null;

		return ByStageIndex.Values.FirstOrDefault(boss => boss.Id.Equals(bossId, System.StringComparison.OrdinalIgnoreCase));
	}

	public static IEnumerable<BossDefinition> All => ByStageIndex.Values;

	/// <summary>The stage index a boss guards, for validation. -1 when the boss is not in the table.</summary>
	public static int StageIndexOf(BossDefinition definition)
	{
		foreach (KeyValuePair<int, BossDefinition> entry in ByStageIndex)
		{
			if (ReferenceEquals(entry.Value, definition))
				return entry.Key;
		}

		return -1;
	}
}
