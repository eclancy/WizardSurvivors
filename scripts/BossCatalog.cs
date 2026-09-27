using System.Collections.Generic;
using System.Linq;

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
	public string ScenePath { get; init; } = string.Empty;
	/// <summary>Health before the balance preset's elite scaling is applied.</summary>
	public int Health { get; init; } = 1000;
	/// <summary>Flat Arcane Energy added on top of the usual run reward for the kill.</summary>
	public int ArcaneVictoryBonus { get; init; } = 120;
	/// <summary>Stage id this victory unlocks, or empty if it gates nothing.</summary>
	public string UnlocksStageId { get; init; } = string.Empty;
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
			ArcaneVictoryBonus = 150,
			UnlocksStageId = "stage_1"
		},
		[1] = new BossDefinition
		{
			Id = "castle_warden",
			DisplayName = "The Gaoler",
			Tagline = "It charges down a lane it shows you first. Step sideways, never back.",
			ScenePath = "res://scenes/GaolerBoss.tscn",
			Health = 5200,
			ArcaneVictoryBonus = 170,
			UnlocksStageId = "stage_2"
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
			ArcaneVictoryBonus = 190,
			UnlocksStageId = "stage_3"
		},
		[3] = new BossDefinition
		{
			Id = "swamp_mother",
			DisplayName = "Mother Rot",
			Tagline = "She heals from everything she sheds. Clear the spawn or the bar goes backwards.",
			ScenePath = "res://scenes/MotherRotBoss.tscn",
			Health = 6800,
			ArcaneVictoryBonus = 210,
			UnlocksStageId = "stage_4"
		},
		[4] = new BossDefinition
		{
			Id = "ruins_sentinel",
			DisplayName = "The Archivist",
			Tagline = "It never moves and its beam never stops. Keep orbiting, and watch where it surfaces.",
			ScenePath = "res://scenes/ArchivistBoss.tscn",
			Health = 7200,
			ArcaneVictoryBonus = 230,
			UnlocksStageId = "stage_5"
		},
		[5] = new BossDefinition
		{
			Id = "frost_warden",
			DisplayName = "The Still Warden",
			Tagline = "Frozen in place, and the arena does the chasing. The safe ground is not where it is.",
			ScenePath = "res://scenes/StillWardenBoss.tscn",
			Health = 7800,
			ArcaneVictoryBonus = 250,
			UnlocksStageId = "stage_6"
		},
		[6] = new BossDefinition
		{
			Id = "sands_coil",
			DisplayName = "The Long Coil",
			Tagline = "Only killable while it is above the sand. Bring something that spikes.",
			ScenePath = "res://scenes/LongCoilBoss.tscn",
			Health = 8200,
			ArcaneVictoryBonus = 270,
			// Deliberately empty. The Emberdeep is gated on StageGate.CampaignComplete - every
			// spell recovered and every wizard freed - and not on this kill, so naming it here
			// would be a claim the unlock path does not honour.
			UnlocksStageId = string.Empty
		},
		[7] = new BossDefinition
		{
			Id = "emberdeep_warden",
			DisplayName = "The Warden of the Deep",
			Tagline = "Every fight you have already won, in sequence, with nothing in between.",
			ScenePath = "res://scenes/DeepWardenBoss.tscn",
			Health = 11000,
			ArcaneVictoryBonus = 400,
			UnlocksStageId = string.Empty
		},
		[8] = new BossDefinition
		{
			Id = "kid_wizard",
			DisplayName = "Wizard Dude",
			Tagline = "Throws a fan of missiles down the wedge he shows you. Leave the wedge.",
			ScenePath = "res://scenes/KidWizardBoss.tscn",
			Health = 4600,
			ArcaneVictoryBonus = 200,
			// Gates nothing. The Sketchbook sits outside the campaign, so beating it must not be
			// on the critical path to anything.
			UnlocksStageId = string.Empty
		}
	};

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
