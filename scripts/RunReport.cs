using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// Everything the results screen shows about a finished run, gathered once by Node2DGame when the
/// run ends (ShowGameOver) and handed to <c>GameOverScreen.Present</c>.
/// </summary>
/// <remarks>
/// The screen used to be fed three setters with a line of text each, and everything it could not
/// be told - the boons, the relics, which spell did the killing, how the Arcane Energy was counted -
/// it did not show. This is the whole run in one object, so the screen builds from data rather
/// than from whatever the setters happened to carry, and a harness (_UiShot --demo-unlocks) can
/// fill it in to photograph a screen no ordinary run reaches.
///
/// NO NODES, AND ONE KIND OF TEXTURE. Ids and resource paths, which the screen resolves as it
/// builds - except <see cref="SpellIcons"/>, because a spell whose art duplicates another's is given
/// an ImageTexture made at run start (Player.EnsureSpellCatalogUniqueIcons) that has no path to
/// load it back from. The report is held only by the screen, which the scene frees; never put it,
/// or anything from it, in a static (the teardown crash in BonelightSkin's remarks).
///
/// NOTHING HERE PAYS ANYTHING. Every number is a record of what FinishRunAndReward already wrote to
/// the save; the screen reads, it never grants.
/// </remarks>
public sealed class RunReport
{
	public global::RunResult Result { get; set; }

	// --- the headline -------------------------------------------------------------------------
	public string CharacterId { get; set; } = string.Empty;
	public string CharacterName { get; set; } = string.Empty;
	public int StageIndex { get; set; }
	public string StageName { get; set; } = string.Empty;
	/// <summary>The chapter's boss, whether or not the run met it.</summary>
	public string BossName { get; set; } = string.Empty;
	/// <summary>The survival clock reached the boss (Node2DGame's boss phase began).</summary>
	public bool BossReached { get; set; }
	/// <summary>When the boss walks in, on the survival clock (Node2DGame.TimerVictorySeconds).</summary>
	public float BossArrivalSeconds { get; set; }

	// --- the rewards --------------------------------------------------------------------------
	/// <summary>The run's own Arcane Energy payout (CalculateArcaneReward), already in the purse.</summary>
	public int ArcaneEarned { get; set; }
	/// <summary>The purse after everything the run paid, unlock rewards included.</summary>
	public int ArcanePurse { get; set; }
	public ArcaneRewardBreakdown Arcane { get; set; } = new();
	public RunUnlockSummary Unlocks { get; set; } = new();

	// --- the build ----------------------------------------------------------------------------
	// The spells are Result.EquippedSpells (RunSpellSnapshot).
	/// <summary>Each held spell's icon as the run drew it, by spell id.</summary>
	public Dictionary<string, Godot.Texture2D> SpellIcons { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
	public List<string> BoonIds { get; set; } = new();
	public List<string> RelicIds { get; set; } = new();

	// --- the stats ----------------------------------------------------------------------------
	/// <summary>Damage each source dealt (GameStats.DamageBySource), copied at the end of the run.</summary>
	public List<(string SourceId, int Damage)> DamageBySource { get; set; } = new();
	public int XpGathered { get; set; }
	/// <summary>Playtest Mode on: the telemetry line (rerolls, bans, pity...) is shown too.</summary>
	public bool PlaytestMode { get; set; }
}

/// <summary>
/// How the run's Arcane Energy was counted, part by part - the same values
/// Node2DGame.CalculateArcaneReward computed the payout from, recorded rather than recomputed.
/// </summary>
public sealed class ArcaneRewardBreakdown
{
	public int BaseFlat { get; set; }
	public int Minutes { get; set; }
	public int PerMinute { get; set; }
	public int Level { get; set; }
	public int PerLevel { get; set; }
	/// <summary>BaseFlat + Minutes x PerMinute + Level x PerLevel.</summary>
	public int BaseTotal { get; set; }
	/// <summary>1 + the Arcane Upgrades that raise the payout (Avarice Seal).</summary>
	public float UpgradeMultiplier { get; set; } = 1f;
	/// <summary>The streak, variety and improvement bonuses together, after their clamp.</summary>
	public float DynamicMultiplier { get; set; } = 1f;
	public List<(string Name, float Bonus)> DynamicParts { get; set; } = new();
	/// <summary>The balance preset's scale (1 on Default).</summary>
	public float PresetScale { get; set; } = 1f;
	/// <summary>A boss's flat victory payout, added after every multiplier.</summary>
	public int BossBonus { get; set; }
	public string BossName { get; set; } = string.Empty;
	/// <summary>Arcane Energy finished Unlocks entries paid, on top of the run's own payout.</summary>
	public int UnlockBonus { get; set; }
}
