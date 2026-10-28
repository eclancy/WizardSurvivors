using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WizardSurvivors.scripts;

public partial class Node2DGame : Node2D
{
	// Lowered with the size bump. Every body is 35% wider, so the same count covers about 1.8x
	// the ground - at 110 the late arena was a solid sheet of enemies with no floor showing
	// between them, which reads as one mass rather than a crowd you can pick targets out of.
	// Fewer, larger bodies is the whole point of the size change.
	[Export] public int MaxEnemies { get; set; } = 70;
	// The cap above was tuned at a 1.25 zoom. The camera is now 0.85 on phone and PC alike
	// (player.tscn), which shows about twice the ground, so the same 70 spread over it read as an
	// empty field. Doubling the cap and the burst window keeps the crowd per square of floor where the
	// cap was tuned, rather than adding a wall. A Pixel 9 held 57-61 fps at 140 (the stress check
	// below); performance on a phone is logged as PERF lines in a debug build.
	[Export] public float WideViewEnemyCapScale { get; set; } = 2.0f;
	private float perfLogTimer;

	// Stress check for the handheld cap: a debug build that finds user://debug_stress_swarm fills the
	// arena to MaxEnemies at once and keeps it there, with the player unkillable and level-ups held
	// back (a level-up pauses the tree, and a paused tree logs nothing), so the PERF lines read the
	// frame rate against a full swarm in seconds instead of minutes in. On a phone the flag goes in
	// with: adb shell run-as com.ecec.wizardsurvivors touch files/debug_stress_swarm  (rm to remove).
	// A release build never reads the file.
	private bool stressSwarm;
	private const int StressSpawnsPerFrame = 8;
	// --status-all (debug builds only): every enemy slowed and poisoned, re-applied twice a second,
	// so the status marks (StatusEffectLayer) can be photographed on a whole crowd and their cost
	// measured against the same run without them. The poison ticks for 0 - what is being measured is
	// the drawing, and a swarm that melted would measure nothing.
	private bool statusAllDebug;
	private float statusAllTimer;
	// When the survival clock stops and the boss arrives: the length of an ordinary run. Thirty
	// minutes, so a wizard's final form has room to arrive in the back half rather than the first
	// three minutes (.ai/gameplay-design.md, "The Apotheosis"). Items and modes that shorten a run
	// change this one number, and the pacing below follows it.
	// 15 minutes since 2026-10-16 (30, briefly 20, before that) - the reference run itself, so RunPace
	// is 1 and every paced number plays exactly as it was tuned. The XP curve was rebalanced alongside
	// (Player.XpCostPerLevel), because levels are paid by kills, not by the clock (.ai/run-pacing.md).
	[Export] public float TimerVictorySeconds { get; set; } = 900.0f;

	// The run every pacing number in this file was tuned against. They keep their meaning as
	// positions on THAT run - "the Lunger arrives at 90 s" means 10% of the way in - and the pace
	// clock below maps them onto whatever length this run actually is. Only what says WHEN in a
	// run something happens is paced; what says how a moment feels (a burst window, the grace after
	// a level-up, the formation rhythm, a hazard tick) stays in real seconds.
	public const float ReferenceRunSeconds = 900.0f;

	/// <summary>How many real seconds one second of the reference run takes. 1 for the 15-minute run, 2 for a 30-minute one.</summary>
	public float RunPace => TimerVictorySeconds > 0f ? TimerVictorySeconds / ReferenceRunSeconds : 1f;

	/// <summary>The run clock as a position on the reference run. Every pacing check reads this, not timeElapsed.</summary>
	public float PaceSeconds => timeElapsed / RunPace;

	private float PaceMinutes => Mathf.Max(0.0f, PaceSeconds / 60.0f);
	// Fallbacks only. The spawn ring is measured from the visible rectangle now (see
	// GetSpawnRadiusForDirection); these are what it falls back to if the camera cannot be read.
	[Export] public float SpawnMinDistance { get; set; } = 250.0f;
	[Export] public float SpawnMaxDistance { get; set; } = 800.0f;
	// How far beyond the edge of the screen the nearest spawn sits, as a multiple of the distance
	// from the player to that edge along the chosen bearing.
	//
	// A flat radius cannot do this job: the viewport is 720x1280, so the screen edge is 288 units
	// away to the side and 512 above, and one number is either inside the screen vertically or a
	// long walk horizontally. The old 250 was inside the screen in every direction, so enemies
	// appeared on camera out of nothing. Measuring per-bearing also means the zoom can change
	// without re-tuning this, which it already has twice.
	// False falls back to the flat SpawnMinDistance..SpawnMaxDistance band. Kept as a real
	// switch rather than a magic zero: setting the margin to 0 to "disable" this yields a
	// radius of 0, which spawns enemies inside the player - a trap this walked straight into
	// while A/B testing, and it silently invalidated a whole set of measurements.
	[Export] public bool SpawnUsesScreenEdge { get; set; } = true;
	[Export] public float SpawnEdgeMargin { get; set; } = 1.08f;
	// Random depth added beyond that edge, so the ring is a band rather than a hard circle.
	[Export] public float SpawnEdgeDepth { get; set; } = 330.0f;
	// How strongly spawning favours the direction the player is travelling.
	//
	// 1.0 is the old behaviour: a uniform ring, which is *why* the horde ends up as a wad behind
	// you. Spawns land evenly on a circle but the player only ever moves one way, so everything
	// spawned ahead gets walked past and joins the tail, and nothing replenishes the front. The
	// steady state of a uniform ring plus a moving player is always a comet.
	//
	// At 1.8 roughly two thirds of spawns land in the forward half. It eases back to uniform as the
	// player slows, because a standing player has no "ahead".
	[Export] public float SpawnForwardBias { get; set; } = 1.8f;
	// Formations: a wave that arrives as a shape rather than as more singles. See SpawnFormations.
	//
	// These run alongside the ordinary trickle rather than replacing it, and are bounded by the same
	// MaxEnemies and burst-window caps everything else is - so a formation cannot exceed the budget,
	// it spends it in one place instead of scattering it.
	[Export] public float FormationIntervalSeconds { get; set; } = 15.0f;
	// Nothing shaped in the opening. The first minute was deliberately thinned out to be a trickle
	// the player can read; dropping a wall into it would undo that.
	[Export] public float FormationFirstSeconds { get; set; } = 75.0f;
	[Export] public int FormationMinSize { get; set; } = 5;
	[Export] public int FormationMaxSize { get; set; } = 9;
	[Export] public float SpawnMinEnemySeparation { get; set; } = 130.0f;
	[Export] public int SpawnPositionRetries { get; set; } = 8;
	[Export] public float SpawnBaseInterval { get; set; } = 0.75f;
	[Export] public float SpawnMinInterval { get; set; } = 0.18f;
	[Export] public float SpawnIntervalReductionPerMinute { get; set; } = 0.05f;
	// The gap between waves in the opening seconds, eased toward the steady curve over
	// SpawnOpeningRampMinutes. A run used to start at a wave every 0.75s of three enemies each,
	// which is about four a second before the burst cap trims it - a wall arriving while the
	// player still has one spell. Past the ramp this is gone and the original curve is intact.
	[Export] public float SpawnOpeningInterval { get; set; } = 1.15f;
	[Export] public float SpawnOpeningRampMinutes { get; set; } = 2.5f;
	[Export] public int SpawnBaseHealth { get; set; } = 6;
	[Export] public int SpawnHealthPerMinute { get; set; } = 10;
	// The health ramp eases in over these first minutes instead of starting at full slope.
	// A level-1 spell does single-digit damage on a two-second cooldown, so the old flat
	// 10 + 10*minutes line meant a two-minute-old skeleton took five casts and a tank took
	// eleven - the opening read as chewing rather than fighting. Past this many minutes the
	// eased curve rejoins the original line, so the mid and late game are untouched.
	[Export] public float SpawnHealthRampMinutes { get; set; } = 4.0f;
	// The pace of the horde, as a multiplier on every enemy's own Speed. It RAMPS across a run.
	//
	// A flat 0.55 was the single largest cause of kiting being free: the fastest ordinary enemy ran
	// at 102 against a player at 220, so nothing could ever close, and every steering fix built on
	// top of that was compensating for a deficit rather than removing it. Three separate experiments
	// landed on the same finding - at roughly 1.6x this, plain pursuit with no clever steering at all
	// matched everything interception and encirclement achieved.
	//
	// Ramped rather than raised flat, because the opening was deliberately thinned out (eased health
	// curve, slower first-minute spawns) and a 36% speed increase from second zero would have undone
	// that. The opening barely moves; by the five minute mark the horde can actually catch someone
	// running in a straight line.
	[Export] public float EnemyMoveSpeedMultiplier { get; set; } = 0.60f;
	[Export] public float EnemyMoveSpeedMultiplierLate { get; set; } = 0.85f;
	[Export] public float EnemySpeedRampMinutes { get; set; } = 5.0f;
	[Export] public float EliteStartTimeSeconds { get; set; } = 135f;
	[Export] public float EliteHealthMultiplier { get; set; } = 2.45f;
	[Export] public float EliteSpeedMultiplier { get; set; } = 1.12f;
	[Export] public float EliteScaleMultiplier { get; set; } = 1.06f;
	[Export] public int MaxEliteEnemiesAlive { get; set; } = 2;
	// Ceiling on how many abandoned enemies are moved back to the ring each second.
	[Export] public int MaxEnemyRecyclesPerSecond { get; set; } = 3;
	[Export] public float PostLevelUpSpawnGraceSeconds { get; set; } = 1.2f;
	// Insets of the pause panel from the viewport edge. The top inset is the deepest because the
	// HUD strip - health bar and run clock - lives up there, and a paused player wants to see how
	// long they have survived and how much health they are going back in with.
	private const float EscapePanelMargin = 18f;
	private const float EscapePanelMaxHalfWidth = 480f;
	private const float EscapePanelTopMargin = 58f;
	private const float EscapePanelBottomMargin = 30f;
	[Export] public float SpawnBurstWindowSeconds { get; set; } = 10f;
	[Export] public int MaxSpawnsPerBurstWindow { get; set; } = 35;
	[Export] public float ForestHalfHeight { get; set; } = 260.0f;
	[Export] public float CastleHalfWidth { get; set; } = 420.0f;
	[Export] public float CastleHalfHeight { get; set; } = 1400.0f;
	[Export] public float RuinsHalfSize { get; set; } = 1200.0f;
	[Export] public int BaseArcaneReward { get; set; } = 20;
	[Export] public int ArcanePerMinuteSurvived { get; set; } = 8;
	[Export] public int ArcanePerPlayerLevel { get; set; } = 2;
	[Export] public bool EnableDecorProps { get; set; } = true;
	[Export] public bool EnableStageHazards { get; set; } = true;
	[Export] public int StageHazardCount { get; set; } = 14;
	[Export] public float StageHazardSpread { get; set; } = 1450f;
	[Export] public float StageHazardMinPlayerDistance { get; set; } = 320f;
	[Export] public float StageHazardSeparation { get; set; } = 240f;
	[Export] public bool EnableWardenMiniBoss { get; set; } = true;
	[Export] public float WardenMiniBossFirstSpawnSeconds { get; set; } = 150f;
	[Export] public float WardenMiniBossIntervalSeconds { get; set; } = 165f;
	// The Hexer is held back from the opening so the first minutes still teach the plain
	// "keep walking, the swarm is behind you" lesson before anything starts shooting at where
	// the player is walking to.
	[Export] public float HexerFirstSpawnSeconds { get; set; } = 105f;
	// Ranged enemies are the most attention-expensive thing in the cast: each one is a tell to
	// read and a bolt to walk out of. At 0.13 plus the Sentry's share, one spawn in five was a
	// shooter, which is too many to track at once in a crowd this size.
	[Export] public float HexerSpawnShare { get; set; } = 0.08f;
	// The Skull Sentry is rooted, so it is area denial rather than a chase: it arrives later than
	// the Hexer and stays rarer, because ground the player has to route around costs more of the
	// run's attention than one more thing following them.
	[Export] public float SentryFirstSpawnSeconds { get; set; } = 210f;
	[Export] public float SentrySpawnShare { get; set; } = 0.04f;
	// The four attack-pattern enemies (issue #33). Each takes its own roll rather than a slice of
	// the per-environment table below, for the same reason the Hexer and Sentry do: a behaviour
	// is a role, not a biome, and giving them their own rolls leaves every stage's table with the
	// full 0..1 spread it was tuned on.
	//
	// They are introduced one at a time and in order of how much they teach. The Lunger comes
	// first and earliest because its lesson - keep moving before you are cornered - is the one the
	// other three build on. The Summoner comes last and stays rarest: it is the only enemy that
	// makes the arena worse by existing, so meeting two at once early would read as unfair rather
	// than as a priority target.
	[Export] public float LungerFirstSpawnSeconds { get; set; } = 90f;
	[Export] public float LungerSpawnShare { get; set; } = 0.10f;
	[Export] public float ExploderFirstSpawnSeconds { get; set; } = 165f;
	[Export] public float ExploderSpawnShare { get; set; } = 0.07f;
	[Export] public float SlammerFirstSpawnSeconds { get; set; } = 210f;
	[Export] public float SlammerSpawnShare { get; set; } = 0.08f;
	[Export] public float SummonerFirstSpawnSeconds { get; set; } = 285f;
	[Export] public float SummonerSpawnShare { get; set; } = 0.04f;
	[Export] public int BushDecorCount { get; set; } = 70;
	[Export] public int TreeDecorCount { get; set; } = 45;
	[Export] public int RuinDecorCount { get; set; } = 28;
	[Export] public int ForestGroundAccentCount { get; set; } = 22;
	[Export] public float DecorFieldHalfWidth { get; set; } = 4200.0f;
	[Export] public float DecorFieldHalfHeight { get; set; } = 4200.0f;
	[Export] public float DecorPlayerSafeRadius { get; set; } = 360.0f;
	[Export] public bool EnableBiomeClustering { get; set; } = true;
	[Export] public int GroundAccentClusterTargetSize { get; set; } = 14;
	[Export] public float GroundAccentClusterRadiusMin { get; set; } = 80.0f;
	[Export] public float GroundAccentClusterRadiusMax { get; set; } = 170.0f;
	[Export] public float GroundAccentClusterCenterSeparation { get; set; } = 250.0f;
	[Export] public float GroundAccentClusterOutlierChance { get; set; } = 0.05f;
	[Export] public int BushClusterTargetSize { get; set; } = 10;
	[Export] public float BushClusterRadiusMin { get; set; } = 140.0f;
	[Export] public float BushClusterRadiusMax { get; set; } = 260.0f;
	[Export] public float BushClusterCenterSeparation { get; set; } = 420.0f;
	[Export] public float BushClusterOutlierChance { get; set; } = 0.12f;
	[Export] public int TreeClusterTargetSize { get; set; } = 6;
	[Export] public float TreeClusterRadiusMin { get; set; } = 220.0f;
	[Export] public float TreeClusterRadiusMax { get; set; } = 420.0f;
	[Export] public float TreeClusterCenterSeparation { get; set; } = 760.0f;
	[Export] public float TreeClusterOutlierChance { get; set; } = 0.18f;
	[Export] public int RuinClusterTargetSize { get; set; } = 3;
	[Export] public float RuinClusterRadiusMin { get; set; } = 180.0f;
	[Export] public float RuinClusterRadiusMax { get; set; } = 340.0f;
	[Export] public float RuinClusterCenterSeparation { get; set; } = 980.0f;
	[Export] public float RuinClusterOutlierChance { get; set; } = 0.28f;
	[Export] public bool ConstrainPlayerToStageBounds { get; set; } = false;

	[Export] public bool UseProceduralTerrain { get; set; } = true;
	[Export] public int ProceduralTilePixels { get; set; } = 48;
	// Chunks painted round the player, Chebyshev, on the endless procedural stages. 2 is a 5 x 5
	// square of 32-tile chunks: about the old fixed arena's area, and at least 3072 px of painted
	// ground between the player and the nearest edge whatever monitor he has. See TerrainStreamer.
	[Export] public int TerrainLoadRadiusChunks { get; set; } = 2;
	[Export] public float ProceduralSafeRadiusTiles { get; set; } = 6.0f;
	// Zero draws a fresh layout every run, as a player gets. Anything else pins it, so a harness
	// (_MazeProbe) can compare two builds on the same maze rather than on two different ones.
	[Export] public long ProceduralSeed { get; set; } = 0;
	[Export] public int HazardDamagePerTick { get; set; } = 4;
	[Export] public float HazardTickInterval { get; set; } = 0.5f;
	[Export] public bool EnableCuratedProps { get; set; } = true;
	[Export] public int CuratedPropCount { get; set; } = 42;
	// Props sit at 1.0 so decor shares the actors' pixel grid now that the project filters
	// nearest - a randomised 0.85-1.15 scale aliased every prop edge. Size variety comes back
	// at migration phase 6 by drawing more props, not by rescaling one (.ai/art-direction.md).
	[Export] public float CuratedPropMinScale { get; set; } = 1.0f;
	[Export] public float CuratedPropMaxScale { get; set; } = 1.0f;

	private Player? player;
	private LevelTilePainter? levelPainter;
	private float hazardTickTimer;
	private PropCatalog? propCatalog;
	private IReadOnlyList<string> proceduralPropThemes = System.Array.Empty<string>();
	// The endless ground, on every procedural stage. Null only on the placeholder room fallback.
	private TerrainStreamer? terrainStreamer;
	// Read-only, for the debug harnesses (_UiShot --shore) that need to find a shoreline.
	public TerrainStreamer? Terrain => terrainStreamer;
	private ulong terrainSeed;
	private StageEnvironmentKind terrainKind;
	private IReadOnlyList<string> terrainTerrainIds = System.Array.Empty<string>();
	private bool proceduralActive;
	private CanvasLayer? levelUpMenu;
	private CanvasLayer? escapeMenu;
	private Control? escapeRoot;
	private Button? escapeResumeButton;
	private Button? escapeReportButton;
	private ScrollContainer? escapeDetailScroll;
	private VBoxContainer? escapeDetailSections;
	private readonly Dictionary<string, Button> escapeTabButtons = new();
	private GridContainer? elementHudGrid;
	private HBoxContainer? selectedSpellHudRow;
	private ColorRect? damageFlashOverlay;
	private ColorRect? lowHealthOverlay;
	private Tween? damageFlashTween;
	private Label? onboardingTipLabel;
	private Label? stageIntroLabel;
	private bool showOnboardingTips = false;
	private int onboardingTipIndex = 0;
	private float onboardingTipTimer = 0f;
	private Label? runTimerLabel;
	private int runTimerLastSecond = -1;
	private Label? debugOverlayLabel;
	private bool debugOverlayVisible = false;
	private float debugOverlayUpdateTimer = 0f;
	private float fireTimer = 0f;
	private float fireInterval = 1f;
	private float spawnTimer = 0f;
	private float spawnInterval = 0.75f;
	private float spawnHealth = 10f;
	// _BalanceHarness --enemy-hp=X only: the ordinary spawns' health times X. Measuring what a strong
	// spell gains needs a crowd it cannot clear, or both runs kill everything that spawns and read the
	// same (the 2026-10-26 Ascension sweep). Never set by the game; 1 is no change.
	public static float HarnessEnemyHealthScale = 1f;
	private float nextEliteSpawnTime = 120f;
	private float spawnGraceRemaining = 0f;
	private float spawnBurstWindowTimer = 0f;
	private int spawnCountInBurstWindow = 0;
	private float presetSpawnIntervalScale = 1f;
	private float presetSpawnHealthScale = 1f;
	// Rate cap on recycling abandoned enemies back to the spawn ring; see RespawnEnemy.
	private float recycleWindowStart = 0f;
	// The player actual travel, for biasing spawns toward where they are going. Measured from
	// positions rather than read off CharacterBody2D.Velocity: the arena boundary clamps position
	// instead of blocking physically, so Velocity still reports full speed while the player is
	// held against the edge. Enemy.cs was already burned by exactly that.
	private Vector2 measuredPlayerVelocity = Vector2.Zero;
	private Vector2 lastPlayerSpawnSample = Vector2.Zero;
	private bool hasPlayerSpawnSample = false;
	private float nextFormationTime = 0f;
	private int recyclesThisWindow = 0;
	private float presetEliteIntervalScale = 1f;
	private float presetElitePowerScale = 1f;
	private float presetArcaneRewardScale = 1f;
	private float timeElapsed = 0f;
	private int totalEnemiesSpawned = 0;
	private float wardenMiniBossTimer = 0f;
	// CHESTS COME FROM CARRIERS, NOT A CLOCK (DropTable). The first elite of a run always pays, so
	// the first relic still arrives on schedule - about 2:15 - and the rule "elites carry chests" is
	// taught by the first one rather than by luck.
	private bool firstEliteChestPaid = false;
	// The speed potion's spacing, in real seconds of play (DropTable.MinSecondsBetweenSpeedPotions).
	private float lastSpeedPotionAt = float.NegativeInfinity;
	private CanvasLayer? chestSelectionMenu;
	private bool tookDamageBeforeFiveMinutes = false;
	private bool runFinished = false;
	private int rerollsRemainingForCurrentLevelUp = 0;

	// Picks owed to the player because they banked an earlier level-up. Spent by reopening the menu
	// once a pick closes, never by granting a level - banking must not touch the XP curve.
	private int extraPicksPending = 0;
	private Vector2 stageOrigin = Vector2.Zero;
	private RandomNumberGenerator spawnRng = new RandomNumberGenerator();
	private const string DecorPropGroup = "decor_props";
	private StageVisualTheme currentStageTheme = StageVisualTheme.Default;
	private readonly List<ColorRect> ambientOverlays = new();

	private PackedScene magicMissileScene = ResourceLoader.Load<PackedScene>("res://scenes/MagicMissile.tscn");

	// THIS CHAPTER'S CAST (StageBestiary, .ai/bestiary.md). Every enemy the spawner, the elites, the
	// Warden's clock and the side events put on the field comes from here, loaded once in _Ready.
	//
	// It replaced nineteen PackedScene fields and an environment switch, under which six behaviour
	// roles and the Warden had one look in every chapter and seven chapter tables drew on the same four
	// bodies. Each chapter's roster now lives in its own file, StageBestiary.<Chapter>.cs, so a chapter's
	// creatures can be drawn without touching this one.
	private LoadedBestiary bestiary = null!;

	// The Sketchbook's two set-pieces that are not roles: the ghost and the dog.
	//
	// The ghost (Boo) is Elim's drawing, even though its sheets live in assets/enemies/ rather than
	// assets/kidsart/ and predate the chapter. It opened every chapter until it was recognised as
	// theirs. On 2026-10-20 Eric asked for it to be redrawn bigger and scarier and made a miniboss
	// (tools/art/sprite_boo.py, scripts/BooEnemy.cs) - the one kids' drawing the never-restyle rule
	// was lifted for. It is no longer in any spawn table; TickGhostMiniBoss sends it twice a run.
	private PackedScene booEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/BooEnemy.tscn");
	private PackedScene kidDogScene = ResourceLoader.Load<PackedScene>("res://scenes/KidDog.tscn");

	// Health readout in the top-left corner. Width matches the XP bar beneath it; height comes
	// from the frame art's 745x138 aspect so the ornament is not squashed.
	private const float HealthHudWidth = 240f;
	private const float HealthHudHeight = 44f;
	private ProgressBar? healthHudBar;
	private ProgressBar? shieldHudBar;
	private Label? healthHudLabel;

	// Boss phase. The run timer stops at TimerVictorySeconds and the stage's boss walks in; killing
	// it is what wins the level. Stages with no BossCatalog entry still end on the timer instead.
	private BossDefinition? activeBossDefinition;
	private BossEnemy? bossInstance;
	private bool bossFightActive = false;
	private bool bossPhaseTriggered = false;
	// Set the moment the boss dies, cleared never: the win is already decided and no later death,
	// vanish or second defeat signal may overwrite it.
	private bool bossVictoryPending = false;
	private const float BossVictoryDelaySeconds = 1.2f;
	private Control? bossHudRoot;
	private ProgressBar? bossHudBar;
	private Label? bossHudLabel;
	private PackedScene levelupMenuScene = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpMenu.tscn");
	private PackedScene gameOverScene = ResourceLoader.Load<PackedScene>("res://scenes/GameOverScreen.tscn");

	private static readonly (string Id, string Path)[] SpellbookResources = new[]
	{
		("magic_missile", "res://SpellData.tres"),
		("arcane_explosion", "res://SpellData_ArcaneExplosion.tres"),
		("spiritual_weapon", "res://SpellData_SpiritualWeapon.tres"),
		("fireball", "res://SpellData_Fireball.tres"),
		("cinderbreath", "res://SpellData_Cinderbreath.tres"),
		("mirefoot", "res://SpellData_Mirefoot.tres"),
		("kindled_ward", "res://SpellData_KindledWard.tres"),
		("gravewell", "res://SpellData_Gravewell.tres"),
		("hollow_star", "res://SpellData_HollowStar.tres"),
		("starfall", "res://SpellData_Starfall.tres"),
		("familiar", "res://SpellData_Familiar.tres"),
		("stormsweep", "res://SpellData_Stormsweep.tres"),
		("sleet_runes", "res://SpellData_SleetRunes.tres"),
		("iron_palisade", "res://SpellData_IronPalisade.tres"),
		("contagion", "res://SpellData_Contagion.tres"),
		("bramble_seed", "res://SpellData_BrambleSeed.tres"),
		("frost_shard", "res://SpellData_FrostShard.tres"),
		("riptide", "res://SpellData_Riptide.tres"),
		("shadow_bolt", "res://SpellData_ShadowBolt.tres"),
		("thorn_vine", "res://SpellData_ThornVine.tres"),
		("gale_blade", "res://SpellData_GaleBlade.tres"),
		("solar_flare", "res://SpellData_SolarFlare.tres"),
		("molten_shard", "res://SpellData_MoltenShard.tres"),
		("chain_lightning", "res://SpellData_ChainLightning.tres"),
		("toxic_spore_burst", "res://SpellData_ToxicSporeBurst.tres"),
		("obsidian_spike", "res://SpellData_ObsidianSpike.tres"),
		("cyclone_slash", "res://SpellData_CycloneSlash.tres"),
		("void_lance", "res://SpellData_VoidLance.tres"),
		("glacial_spike", "res://SpellData_GlacialSpike.tres"),
		("black_tentacles", "res://SpellData_BlackTentacles.tres"),
		("cone_of_cold", "res://SpellData_ConeOfCold.tres"),
		("scorching_ray", "res://SpellData_ScorchingRay.tres"),
		("meteor_swarm", "res://SpellData_MeteorSwarm.tres"),
		("hunters_draw", "res://SpellData_HuntersDraw.tres")
	};

	private static readonly (string Id, string DisplayName, string Description, string Elements)[] PassiveSpellbookEntries = new[]
	{
		("aegis_ward", "Aegis Ward", "Periodically grants an absorbing shield.", "Metal, Light"),
		("thornmail_barrier", "Thornmail Barrier", "Retaliates against nearby enemies when hit.", "Earth, Grass"),
		("frozen_bulwark", "Frozen Bulwark", "Chance to freeze nearby attackers when hit.", "Ice x2"),
		("stormguard_aura", "Stormguard Aura", "Strikes the nearest enemy with lightning when hit.", "Lightning, Metal"),
		("venom_cloak", "Venom Cloak", "Periodically poisons nearby enemies.", "Poison, Darkness"),
		("guardian_vines", "Guardian Vines", "Periodically roots nearby enemies.", "Grass x2"),
		("tidal_barrier", "Tidal Barrier", "Periodically knocks back and slows nearby enemies.", "Water, Wind"),
		("stone_bulwark", "Stone Bulwark", "Grants armor that reduces incoming damage.", "Earth, Metal"),
		("blur", "Blur", "Chance to avoid incoming hits entirely.", "Arcane, Wind"),
		("fortunes_favor", "Fortune's Favor", "Passively boosts Luck.", "Arcane, Light"),
		("haste", "Haste", "Periodically grants attack-speed and move-speed surges.", "Wind, Lightning")
	};

	private static readonly Texture2D FallbackSpellHudIcon = GD.Load<Texture2D>("res://assets/bonelight/ui/spells/unknown.png");

	private enum DungeonTileRole
	{
		Floor,
		FloorAccent,
		Wall,
		WallCorner,
		Detail
	}

	private static readonly Dictionary<DungeonTileRole, Vector2I> DungeonTileRoleAtlasCoords = new()
	{
		// Dungeon floor base tile (tile_000)
		[DungeonTileRole.Floor] = new Vector2I(0, 0),
		// Floor variation tile used for subtle patterning (tile_001)
		[DungeonTileRole.FloorAccent] = new Vector2I(1, 0),
		// Primary wall boundary tile (tile_015)
		[DungeonTileRole.Wall] = new Vector2I(2, 1),
		// Corner wall tile for map perimeter corners (tile_026)
		[DungeonTileRole.WallCorner] = new Vector2I(0, 2),
		// Center detail tile to break symmetry (tile_014)
		[DungeonTileRole.Detail] = new Vector2I(1, 1)
	};

	private static readonly (float Time, string Text)[] OnboardingTips = new[]
	{
		(0f, "Tip: Stay moving and kite enemies. Standing still is lethal."),
		(30f, "Tip: Prioritize one or two core spells before spreading upgrades."),
		// The chapter's event no longer comes to the player (.ai/side-events.md, "Finding the event"):
		// the first run has to be told that walking is worth something.
		(60f, "Tip: Explore. A light at the edge of the screen that is not yours marks something worth finding."),
		(90f, "Tip: Element tags stack. Hitting 2/4/6 grants stronger passive thresholds."),
		// Was "swap low-impact spells", from before spells became permanent. Chests are carried now
		// (DropCarriedChest), and the first elite arrives at about 2:15 - so this is when to say so.
		(180f, "Tip: Gold-ringed elites often carry a chest, and Wardens always do."),
		(300f, "Tip: If you're behind, use rerolls to force stronger options.")
	};

	/// <summary>The run in progress, if any. Read by CursorVisibility to know a run is being played.</summary>
	public static Node2DGame Current { get; private set; }

	/// <summary>True while a run is on screen and being played: not finished, not paused under a menu.</summary>
	public static bool InPlay =>
		Current != null && IsInstanceValid(Current) && Current.IsInsideTree()
		&& !Current.runFinished && !Current.GetTree().Paused;

	public override void _Ready()
	{
		Current = this;
		// Before anything reads the stage: a Boss Rush chooses its own arena (BossRushCatalog).
		InitBossRush();
		FrameProfiler.AttachIfRequested(this);
		ContentValidator.ValidateAtStartup(this);
		GameStats.ResetRunTelemetry();
		RunEvents.Reset();
		YSortEnabled = true;
		// Before anything can spawn, so the first enemy debuffed already has somewhere to be drawn.
		AddChild(new StatusEffectLayer());
		PlayRunMusic();
		spawnRng.Randomize();
		bestiary = LoadBestiaryForStage();
		player = GetNode<Player>("CharacterBody2D"); // Strongly typed YES
		stageOrigin = player?.GlobalPosition ?? Vector2.Zero;
		ApplyBalancePresetFromSave();
		// After the preset, which scales the burst window itself, so the two compose.
		MaxEnemies = Mathf.RoundToInt(MaxEnemies * WideViewEnemyCapScale);
		MaxSpawnsPerBurstWindow = Mathf.RoundToInt(MaxSpawnsPerBurstWindow * WideViewEnemyCapScale);
		// --stress-swarm does the same from the command line. The file lives in the shared user://
		// folder, so on a desktop running several sessions it would put every one of them under stress;
		// a command-line flag reaches only the process it was given to (_BalanceHarness measurements).
		stressSwarm = OS.IsDebugBuild() && (Godot.FileAccess.FileExists("user://debug_stress_swarm")
			|| OS.GetCmdlineUserArgs().Contains("--stress-swarm"));
		if (stressSwarm)
			GD.Print($"STRESS swarm on: filling to {MaxEnemies} enemies, player unkillable");
		statusAllDebug = OS.IsDebugBuild() && OS.GetCmdlineUserArgs().Contains("--status-all");
		if (statusAllDebug)
			GD.Print("STATUS all: every enemy slowed and poisoned (0 damage) for the whole run");
		ApplyStageTheme();
		ApplyAmbientStageEffects();
		BuildWorldTileMapLayout();
		nextEliteSpawnTime = EliteStartTimeSeconds;
		player?.Connect("XpGained", new Callable(this, nameof(OnPlayerXpGained)));
		player?.Connect("LevelGained", new Callable(this, nameof(OnPlayerLevelGained)));
		player?.Connect("Died", new Callable(this, nameof(OnPlayerDied)));
		player?.Connect("DamageTaken", new Callable(this, nameof(OnPlayerDamageTaken)));
		player?.Connect("ChestSetCompleted", new Callable(this, nameof(OnChestSetCompleted)));

		if (HasNode("LevelUpMenu"))
			levelUpMenu = GetNode<CanvasLayer>("LevelUpMenu");
		if (levelUpMenu != null)
			levelUpMenu.ProcessMode = ProcessModeEnum.WhenPaused;
		levelUpMenu?.Hide();
		// Connect to WeaponSelected signal if menu exists at startup
		if (levelUpMenu != null)
		{
			var menuScript = levelUpMenu as Node;
			menuScript?.Connect("WeaponSelected", new Callable(this, nameof(OnWeaponSelected)));
			menuScript?.Connect("RerollRequested", new Callable(this, nameof(OnRerollRequested)));
			menuScript?.Connect("SkipRequested", new Callable(this, nameof(OnSkipRequested)));
			menuScript?.Connect("BanRequested", new Callable(this, nameof(OnBanRequested)));
			menuScript?.Connect("BankRequested", new Callable(this, nameof(OnBankRequested)));
			menuScript?.Connect("AuguryRequested", new Callable(this, nameof(OnAuguryRequested)));
		}

		var uiOverlay = GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (uiOverlay != null)
		{
			damageFlashOverlay = new ColorRect
			{
				Name = "DamageFlashOverlay",
				Color = new Color(1f, 0.2f, 0.2f, 0f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AnchorRight = 1f,
				AnchorBottom = 1f
			};
			uiOverlay.AddChild(damageFlashOverlay);
			// Off-screen chests get a bubble at the screen edge (ChestPointer): on an infinite map a
			// chest that dropped behind the swarm was otherwise lost.
			uiOverlay.AddChild(new ChestPointer { Name = "ChestPointer" });

			lowHealthOverlay = new ColorRect
			{
				Name = "LowHealthOverlay",
				Color = new Color(0.8f, 0.05f, 0.05f, 0f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AnchorRight = 1f,
				AnchorBottom = 1f
			};
			uiOverlay.AddChild(lowHealthOverlay);

			// The element badges ("Fire 2/4" at 102x28), the 48-pixel equipped-spell row and the relic
			// strip used to live here and were taken off the playfield as too heavy for something the
			// player does not act on mid-fight. The glance came back, small: LoadoutHud is the held
			// spells as 32-unit icons with their levels and one colour chip per element ("3/4"), under
			// the health bar, redrawn on Player.LoadoutChanged. Boons and relics stay off it; the
			// pause screen is still where any of it is read in full.
			//
			// elementHudGrid and selectedSpellHudRow stay declared and stay null - the old builders
			// below are dead, and every refresh path already early-returns on null.
			uiOverlay.AddChild(new LoadoutHud { Name = "LoadoutHud", Holder = player });

			var runTimerPanel = new PanelContainer
			{
				Name = "RunTimerPanel",
				AnchorLeft = 0.5f,
				AnchorRight = 0.5f,
				OffsetLeft = -64f,
				OffsetTop = 8f,
				OffsetRight = 64f,
				OffsetBottom = 36f,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};

			var runTimerStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.10f, 0.14f, 0.76f),
				BorderColor = new Color(0.24f, 0.30f, 0.42f, 0.88f)
			};
			runTimerStyle.SetCornerRadiusAll(7);
			runTimerStyle.SetBorderWidthAll(1);
			runTimerStyle.SetContentMarginAll(5);
			runTimerPanel.AddThemeStyleboxOverride("panel", runTimerStyle);

			runTimerLabel = new Label
			{
				Name = "RunTimerLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				Text = FormatTime(0f),
				Modulate = new Color(0.95f, 0.98f, 1.0f, 0.98f)
			};
			ResponsiveLayout.SetFont(runTimerLabel, ResponsiveLayout.TextRole.Body);
			runTimerPanel.AddChild(runTimerLabel);
			uiOverlay.AddChild(runTimerPanel);

			var stageIntroPanel = new PanelContainer
			{
				Name = "StageIntroPanel",
				AnchorLeft = 0.5f,
				AnchorRight = 0.5f,
				OffsetLeft = -260f,
				OffsetTop = 42f,
				OffsetRight = 260f,
				OffsetBottom = 104f,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			var stageIntroStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.06f, 0.08f, 0.12f, 0.72f),
				BorderColor = new Color(0.24f, 0.30f, 0.42f, 0.90f)
			};
			stageIntroStyle.SetCornerRadiusAll(8);
			stageIntroStyle.SetBorderWidthAll(1);
			stageIntroStyle.SetContentMarginAll(8);
			stageIntroPanel.AddThemeStyleboxOverride("panel", stageIntroStyle);
			stageIntroLabel = new Label
			{
				Name = "StageIntroLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Modulate = new Color(0.96f, 0.98f, 1.0f, 1.0f)
			};
			ResponsiveLayout.SetFont(stageIntroLabel, ResponsiveLayout.TextRole.Micro);
			stageIntroPanel.AddChild(stageIntroLabel);
			stageIntroPanel.Visible = false;
			uiOverlay.AddChild(stageIntroPanel);

			onboardingTipLabel = new Label
			{
				Name = "OnboardingTipLabel",
				AnchorLeft = 0.5f,
				AnchorRight = 0.5f,
				OffsetLeft = -320f,
				OffsetTop = 82f,
				OffsetRight = 320f,
				OffsetBottom = 128f,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Visible = false,
				Modulate = new Color(0.95f, 0.98f, 1.0f, 0.95f)
			};
			ResponsiveLayout.SetFont(onboardingTipLabel, ResponsiveLayout.TextRole.Micro);
			uiOverlay.AddChild(onboardingTipLabel);

			debugOverlayLabel = new Label
			{
				Name = "DebugOverlayLabel",
				AnchorLeft = 0f,
				AnchorTop = 0f,
				AnchorRight = 0f,
				AnchorBottom = 0f,
				OffsetLeft = 10f,
				OffsetTop = 86f,
				OffsetRight = 430f,
				OffsetBottom = 230f,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Visible = false,
				Modulate = new Color(0.92f, 0.98f, 1.0f, 0.96f)
			};
			ResponsiveLayout.SetFont(debugOverlayLabel, ResponsiveLayout.TextRole.Micro);
			uiOverlay.AddChild(debugOverlayLabel);
		}

		// The relic strip is off the playfield for the same reason as the element badges above.
		// ChestItemHUD and FullSetDetailScreen are untouched and still work; nothing constructs
		// them during a run any more.

		if (TouchControls.ShouldEnable())
		{
			var touchControls = new TouchControls { Name = "TouchControls" };
			touchControls.PauseRequested += ToggleEscapeMenu;
			AddChild(touchControls);
		}

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		showOnboardingTips = saveManager != null
			&& saveManager.Data.EnableGameplayOnboardingTips
			&& !saveManager.Data.HasSeenGameplayOnboarding;
		if (showOnboardingTips)
			ShowNextOnboardingTip(force: true);
		ConfigurePlayerHealthHud();
		ConfigureXpCounterUi();
		CreateTileLegendUi();
		// The whole HUD is built by now. On a phone the health bar, XP bar and timer sat under the
		// rounded corners and the camera hole (SafeArea).
		SafeArea.Apply(GetNodeOrNull<CanvasLayer>("UIOverlay"));
		RefreshElementHud();
		EnsureEscapeMenuUi();
		BuildDecorProps();
		SpawnKidDog();
		// One side event per run, on the chapters that have one (.ai/side-events.md).
		AddChild(new SideEventDirector { Name = "SideEventDirector", Game = this });
		// The chapter's secrets (issue #20, .ai/stage-secrets.md): the keepsake is placed and its ground
		// reserved here, before LoadInitialTerrain furnishes the first chunks, as the event's is.
		AddChild(new SecretDirector { Name = "SecretDirector", Game = this });
		BuildStageHazards();
		BuildCuratedProps();
		// After all three: on an endless stage they only recorded what to scatter, and this is where
		// the first chunks are painted and furnished with it.
		LoadInitialTerrain();
		ShowStageIntroLabel();
		UpdateSpawnScaling();
	}

	// THE ONLY PLACE THE PLAYER'S HEALTH IS SHOWN.
	//
	// It used to be legible only from a 64x8 flat red rectangle floating above the player's head -
	// fine on a desktop monitor, close to useless on a phone in a crowded fight - so this was added
	// in the corner at a readable size with the numbers spelled out, and for a while the game had
	// both. Two readouts of one number is one too many: the floating bar has gone and this is it.
	//
	// The low-health vignette is NOT a second readout. It has no scale and reports no value; it is
	// an alarm, and it is the only other thing on screen that reacts to health at all.
	//
	// Updated from _Process rather than from a signal, so it cannot fall out of step with a health
	// change that forgot to tell anyone.
	private void ConfigurePlayerHealthHud()
	{
		var uiOverlay = GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (uiOverlay == null || player == null)
			return;

		// The ornate HP frame was a pack illustration. Null until one is drawn - the bar
		// below draws itself and reads fine without a frame around it.
		Texture2D frameTexture = null;
		var root = new Control
		{
			Name = "PlayerHealthHud",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			OffsetLeft = 7f,
			OffsetTop = 6f,
			OffsetRight = 7f + HealthHudWidth,
			OffsetBottom = 6f + HealthHudHeight
		};
		uiOverlay.AddChild(root);

		if (frameTexture != null)
		{
			var frame = new TextureRect
			{
				Name = "Frame",
				Texture = frameTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			frame.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			root.AddChild(frame);
		}

		// The trough inside the frame art, as fractions of the source image, so the inset stays
		// correct whatever size the bar is given.
		healthHudBar = new ProgressBar
		{
			Name = "HealthFill",
			MinValue = 0,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			OffsetLeft = HealthHudWidth * 0.085f,
			OffsetTop = HealthHudHeight * 0.24f,
			OffsetRight = HealthHudWidth * 0.925f,
			OffsetBottom = HealthHudHeight * 0.78f
		};
		var trough = new StyleBoxFlat { BgColor = new Color(0.08f, 0.03f, 0.03f, 0.85f) };
		trough.SetCornerRadiusAll(3);
		var fill = new StyleBoxFlat { BgColor = new Color(0.82f, 0.16f, 0.18f, 1.0f) };
		fill.SetCornerRadiusAll(3);
		healthHudBar.AddThemeStyleboxOverride("background", trough);
		healthHudBar.AddThemeStyleboxOverride("fill", fill);
		root.AddChild(healthHudBar);

		// The shield pool, as a steel strip along the foot of the trough, on the same scale as the
		// health above it (a fraction of max HP). Aegis Ward, the Reliquary's per-chest shield and
		// the Reliquary Vial's banked overheal all fill one pool, and until this the only sign of it
		// was the ring round the player - on or off, never how much. The overheal cap is 20% of max
		// HP, so a full overheal shield is a strip a fifth of the bar long.
		shieldHudBar = new ProgressBar
		{
			Name = "ShieldFill",
			MinValue = 0,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			OffsetLeft = HealthHudWidth * 0.085f,
			OffsetTop = HealthHudHeight * 0.78f - 5f,
			OffsetRight = HealthHudWidth * 0.925f,
			OffsetBottom = HealthHudHeight * 0.78f,
			Visible = false
		};
		shieldHudBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
		// steel lit (#B8C8DC): the metal the shield ring is drawn in, and nothing like the red fill.
		shieldHudBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.722f, 0.784f, 0.863f, 1f) });
		root.AddChild(shieldHudBar);

		healthHudLabel = new Label
		{
			Name = "HealthLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		healthHudLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		ResponsiveLayout.SetFont(healthHudLabel, ResponsiveLayout.TextRole.Micro);
		healthHudLabel.AddThemeColorOverride("font_color", new Color(1f, 0.94f, 0.86f));
		healthHudLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		healthHudLabel.AddThemeConstantOverride("outline_size", 5);
		root.AddChild(healthHudLabel);

		UpdatePlayerHealthHud();
	}

	private void UpdatePlayerHealthHud()
	{
		if (healthHudBar == null || player == null || !IsInstanceValid(player))
			return;

		healthHudBar.MaxValue = Math.Max(1, player.MaxHP);
		healthHudBar.Value = Mathf.Clamp(player.CurrentHP, 0, player.MaxHP);
		int shield = player.ShieldPoints;
		if (shieldHudBar != null)
		{
			shieldHudBar.Visible = shield > 0;
			shieldHudBar.MaxValue = Math.Max(1, player.MaxHP);
			shieldHudBar.Value = Mathf.Clamp(shield, 0, player.MaxHP);
		}
		if (healthHudLabel != null)
		{
			// Called every frame, so the label is only touched when a number moved.
			string text = shield > 0
				? $"{Math.Max(0, player.CurrentHP)} / {player.MaxHP}  +{shield} shield"
				: $"{Math.Max(0, player.CurrentHP)} / {player.MaxHP}";
			if (healthHudLabel.Text != text)
				healthHudLabel.Text = text;
		}
	}

	private void ConfigureXpCounterUi()
	{
		var xpCounter = GetNodeOrNull<ProgressBar>("UIOverlay/XPCounter");
		if (xpCounter == null)
			return;

		// Shifted down to sit under the new health bar, and slimmed - XP is secondary information
		// and does not need the same weight as health.
		xpCounter.OffsetTop = 6f + HealthHudHeight + 4f;
		xpCounter.OffsetBottom = xpCounter.OffsetTop + 15f;
		xpCounter.ShowPercentage = false;

		var xpBackground = new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.12f, 0.18f, 0.92f),
			BorderColor = new Color(0.24f, 0.30f, 0.42f, 0.95f)
		};
		xpBackground.SetCornerRadiusAll(4);
		xpBackground.SetBorderWidthAll(1);

		var xpFill = new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.56f, 1.0f, 1.0f)
		};
		xpFill.SetCornerRadiusAll(3);

		xpCounter.AddThemeStyleboxOverride("background", xpBackground);
		xpCounter.AddThemeStyleboxOverride("fill", xpFill);
	}

	// ---- run music -------------------------------------------------------------------------
	// The chapter's three stems play locked together (MusicPlayer) and this decides which are up.
	// Bed always. Pulse once the arena has filled. Lead for an elite, and for the last third of
	// the run. Then the boss loop replaces the set, and everything fades at the end. Thresholds
	// have hysteresis and the lead a hold, because a layer that flickers in and out with every
	// wave is worse than one that never comes. See .ai/audio-direction.md section 11.

	/// <summary>Pulse comes in at this share of MaxEnemies alive (28 of 140)...</summary>
	private const float MusicPulseOnShare = 0.20f;
	/// <summary>...and goes out only once the arena has thinned to this (14 of 140).</summary>
	private const float MusicPulseOffShare = 0.10f;
	/// <summary>The last third of the run, on the pace clock, brings the lead in for good.</summary>
	private const float MusicLeadLateRunPaceMinutes = 10f;
	/// <summary>Real seconds the lead stays after the elite that called it has gone - about one phrase of the tune.</summary>
	private const float MusicLeadHoldSeconds = 30f;
	/// <summary>How often the intensity is sampled. Counting enemies every frame buys nothing a 4 s fade can show.</summary>
	private const float MusicSampleSeconds = 0.25f;

	private MusicPlayer? runMusic;
	private MusicSet? runMusicSet;
	// The enemies that call the lead in: elites, event elites, the Warden (Rockem in the Sketchbook)
	// and the Sketchbook's ghost. Tracked at their spawn rather than read off IsMiniBoss, because a
	// flag set in a scene is not the same as being sent as a set-piece: when the Boo was still 2.5%
	// of the opening spawns the first headless run had the tune in ten seconds on one ghost.
	private readonly List<Node2D> musicLeadCallers = new();
	private bool musicPulseOn;
	private float musicLeadHeldUntil = -1f;
	private float musicSampleTimer;

	private void PlayRunMusic()
	{
		runMusic = GetNodeOrNull<MusicPlayer>("/root/MusicPlayer");
		// Which set is MusicCatalog's decision, not this scene's - the branch belongs in the table.
		runMusicSet = MusicCatalog.RunSetForStage(Global.SelectedStageIdx);
		runMusic?.PlaySet(runMusicSet, MusicPlayer.RunFadeInSeconds, 0f, 0f);
	}

	private void UpdateRunMusic(float delta)
	{
		// A Boss Rush's fights come before the boss phase, and their loop is a solo the set's
		// intensity must not reach under.
		if (runMusic == null || bossPhaseTriggered || bossFightActive || runFinished)
			return;
		musicSampleTimer -= delta;
		if (musicSampleTimer > 0f)
			return;
		musicSampleTimer = MusicSampleSeconds;

		int alive = EnemyRegistry.Count;
		if (alive >= MaxEnemies * MusicPulseOnShare)
			musicPulseOn = true;
		else if (alive <= MaxEnemies * MusicPulseOffShare)
			musicPulseOn = false;

		for (int i = musicLeadCallers.Count - 1; i >= 0; i--)
		{
			Node2D caller = musicLeadCallers[i];
			// A dying enemy leaves "enemies" on the frame it reaches 0 HP (Enemy.StartDeath).
			if (!IsInstanceValid(caller) || !caller.IsInsideTree() || !caller.IsInGroup("enemies"))
				musicLeadCallers.RemoveAt(i);
		}

		// When in the run: the pace clock. How long the tune lingers: real seconds.
		bool leadCalled = PaceMinutes >= MusicLeadLateRunPaceMinutes || musicLeadCallers.Count > 0;
		if (leadCalled)
			musicLeadHeldUntil = timeElapsed + MusicLeadHoldSeconds;
		bool leadOn = leadCalled || timeElapsed < musicLeadHeldUntil;

		// The tune never plays without the rhythm under it.
		runMusic.SetLayerTargets(musicPulseOn || leadOn ? 1f : 0f, leadOn ? 1f : 0f);
	}

	private void PlayBossMusic()
	{
		if (runMusic == null || runMusicSet == null)
			return;
		var stream = ResourceLoader.Load<AudioStream>(runMusicSet.Boss);
		if (stream == null)
		{
			GD.PushWarning($"Node2DGame: boss music missing at {runMusicSet.Boss}; the chapter's set plays on.");
			return;
		}
		runMusic.PlaySolo(stream, MusicPlayer.BossCrossfadeSeconds);
	}

	private void CreateTileLegendUi()
	{
		var uiOverlay = GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (uiOverlay == null)
			return;

	}

	private Dictionary<DungeonTileRole, Vector2I> ValidateDungeonTileRoleAtlasCoords(TileMapLayer worldTileMap, int sourceId)
	{
		var resolvedCoords = new Dictionary<DungeonTileRole, Vector2I>(DungeonTileRoleAtlasCoords);
		var fallbackFloorCoords = DungeonTileRoleAtlasCoords[DungeonTileRole.Floor];

		var tileSet = worldTileMap.TileSet;
		if (tileSet == null)
		{
			GD.PushError("[WorldTileMap] Missing TileSet on WorldTileMap. Using fallback floor coordinates for all dungeon tile roles.");
			return DungeonTileRoleAtlasCoords.Keys.ToDictionary(role => role, _ => fallbackFloorCoords);
		}

		var atlasSource = tileSet.GetSource(sourceId) as TileSetAtlasSource;
		if (atlasSource == null)
		{
			GD.PushError($"[WorldTileMap] TileSet source {sourceId} is missing or not a TileSetAtlasSource. Using fallback floor coordinates for all dungeon tile roles.");
			return DungeonTileRoleAtlasCoords.Keys.ToDictionary(role => role, _ => fallbackFloorCoords);
		}

		Vector2I atlasGridSize = GetAtlasGridSize(atlasSource);
		if (atlasGridSize.X <= 0 || atlasGridSize.Y <= 0)
		{
			GD.PushError("[WorldTileMap] Could not determine atlas grid size. Using fallback floor coordinates for all dungeon tile roles.");
			return DungeonTileRoleAtlasCoords.Keys.ToDictionary(role => role, _ => fallbackFloorCoords);
		}

		foreach (var (role, coords) in DungeonTileRoleAtlasCoords)
		{
			bool inBounds = coords.X >= 0 && coords.Y >= 0 && coords.X < atlasGridSize.X && coords.Y < atlasGridSize.Y;
			if (!inBounds)
			{
				GD.PushError($"[WorldTileMap] Atlas coords {coords} for role {role} are outside atlas grid {atlasGridSize}. Falling back to floor coords {fallbackFloorCoords}.");
				resolvedCoords[role] = fallbackFloorCoords;
			}
		}

		return resolvedCoords;
	}

	private static Vector2I GetAtlasGridSize(TileSetAtlasSource atlasSource)
	{
		if (atlasSource.Texture == null || atlasSource.TextureRegionSize.X <= 0 || atlasSource.TextureRegionSize.Y <= 0)
			return Vector2I.Zero;

		Vector2 textureSize = atlasSource.Texture.GetSize();
		int cols = Mathf.FloorToInt(textureSize.X / atlasSource.TextureRegionSize.X);
		int rows = Mathf.FloorToInt(textureSize.Y / atlasSource.TextureRegionSize.Y);
		return new Vector2I(cols, rows);
	}

	private void ApplyStageTheme()
	{
		int stageIndex = Global.SelectedStageIdx;
		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(stageIndex);
		currentStageTheme = new StageVisualTheme(
			environmentProfile.BackgroundTexturePath,
			environmentProfile.BackgroundRegion,
			environmentProfile.OverlayTexturePath,
			environmentProfile.OverlayRegion,
			environmentProfile.BackgroundTextureScale,
			environmentProfile.OverlayTextureScale,
			environmentProfile.BackgroundModulate,
			environmentProfile.OverlayModulate,
			environmentProfile.OverlayScrollScale,
			environmentProfile.BackgroundTilePixelSize,
			environmentProfile.BushCount,
			environmentProfile.TreeCount,
			environmentProfile.RuinCount);

		BushDecorCount = currentStageTheme.BushCount;
		TreeDecorCount = currentStageTheme.TreeCount;
		RuinDecorCount = currentStageTheme.RuinCount;
		// Read off the profile rather than the visual theme: StageVisualTheme is the BACKGROUND
		// record and carries only what the parallax layers need. Threading a prop count through it
		// would put a scatter number in a struct about textures.
		if (environmentProfile.PropCount > 0)
			CuratedPropCount = environmentProfile.PropCount;

		var background = GetNodeOrNull<TextureRect>("CanvasLayer/Background");
		if (background != null)
		{
			background.Texture = LoadThemeTexture(currentStageTheme.BackgroundTexturePath, currentStageTheme.BackgroundRegion);
			background.Modulate = currentStageTheme.BackgroundModulate;
			// Tile whole textures (ground sheets) and explicit atlas crops; clamp full-size
			// painted backdrops if added in the future.
			bool tileBackground =
				currentStageTheme.BackgroundTilePixelSize > 0f
				|| currentStageTheme.BackgroundRegion.Size != Vector2.Zero
				|| currentStageTheme.BackgroundTexturePath.EndsWith("ground_tile.png")
				|| currentStageTheme.BackgroundTexturePath.EndsWith("ground_rocks_tile.png");
			float tilePixelSize = currentStageTheme.BackgroundTilePixelSize > 0f
				? currentStageTheme.BackgroundTilePixelSize
				: ResolveBackgroundTilePixelSize(background.Texture);
			background.Set("texture_repeat", tileBackground ? 1 : 0);
			if (background.Material is ShaderMaterial backgroundMaterial)
			{
				backgroundMaterial.SetShaderParameter("tile", tileBackground);
				backgroundMaterial.SetShaderParameter("texture_scale", currentStageTheme.BackgroundTextureScale);
				backgroundMaterial.SetShaderParameter("scroll_scale", 0.0f);
				backgroundMaterial.SetShaderParameter("tile_size", tilePixelSize);
			}
		}

		var backgroundOverlay = GetNodeOrNull<TextureRect>("CanvasLayer/BackgroundOverlay");
		if (backgroundOverlay != null)
		{
			backgroundOverlay.Texture = string.IsNullOrEmpty(currentStageTheme.OverlayTexturePath)
				? null
				: LoadThemeTexture(currentStageTheme.OverlayTexturePath, currentStageTheme.OverlayRegion);
			backgroundOverlay.Modulate = currentStageTheme.OverlayModulate;
			backgroundOverlay.Set("texture_repeat", 0);
			backgroundOverlay.Visible = backgroundOverlay.Texture != null && backgroundOverlay.Modulate.A > 0.001f;
			if (backgroundOverlay.Material is ShaderMaterial overlayMaterial)
			{
				overlayMaterial.SetShaderParameter("texture_scale", currentStageTheme.OverlayTextureScale);
				overlayMaterial.SetShaderParameter("scroll_scale", currentStageTheme.OverlayScrollScale);
			}
		}
	}

	private Texture2D? LoadThemeTexture(string texturePath, Rect2 region)
	{
		Texture2D? baseTexture = ResourceLoader.Load<Texture2D>(texturePath);
		if (baseTexture == null)
			return null;

		if (region.Size == Vector2.Zero)
			return baseTexture;

		Image sourceImage = baseTexture.GetImage();
		if (sourceImage == null)
		{
			return new AtlasTexture
			{
				Atlas = baseTexture,
				Region = region
			};
		}

		var requestedRegion = new Rect2I(
			Mathf.FloorToInt(region.Position.X),
			Mathf.FloorToInt(region.Position.Y),
			Mathf.FloorToInt(region.Size.X),
			Mathf.FloorToInt(region.Size.Y));

		var imageBounds = new Rect2I(0, 0, sourceImage.GetWidth(), sourceImage.GetHeight());
		Rect2I clippedRegion = requestedRegion.Intersection(imageBounds);
		if (clippedRegion.Size.X <= 0 || clippedRegion.Size.Y <= 0)
		{
			GD.PushWarning($"[StageTheme] Region {requestedRegion} is out of bounds for texture '{texturePath}'. Falling back to full texture.");
			return baseTexture;
		}

		Image croppedImage = sourceImage.GetRegion(clippedRegion);
		return ImageTexture.CreateFromImage(croppedImage);
	}

	private static float ResolveBackgroundTilePixelSize(Texture2D? texture)
	{
		if (texture == null)
			return 64.0f;

		if (texture is AtlasTexture atlasTexture)
		{
			Vector2 regionSize = atlasTexture.Region.Size;
			float atlasCellSize = Mathf.Min(regionSize.X, regionSize.Y);
			if (atlasCellSize > 0f)
			{
				// Atlas cells are often 16px source sprites. Upscale so repeated background
				// detail remains readable in gameplay view.
				return Mathf.Max(128.0f, atlasCellSize * 8.0f);
			}
		}

		Vector2 textureSize = texture.GetSize();
		float tileSize = Mathf.Min(textureSize.X, textureSize.Y);
		return tileSize > 0f ? tileSize : 64.0f;
	}

	private void BuildWorldTileMapLayout()
	{
		// Cleared here so a prior maze run never leaks navigation into a non-maze build.
		MazeNavigation.Active = null;

		if (UseProceduralTerrain && TryBuildProceduralTerrain())
			return;

		BuildPlaceholderRoomLayout();
	}

	// Procedurally generate the stage terrain from the curated autotile tileset and paint it
	// with a LevelTilePainter (ground + overlay layers, resolved by TerrainAutotiler). Returns
	// false if the curated catalog is unavailable so the caller can fall back to the room layout.
	// Applies damage-over-time while the player stands on a hazard tile (lava / pit).
	private void UpdateHazardDamage(float delta)
	{
		if (terrainStreamer == null || player == null || player.IsDead)
		{
			hazardTickTimer = 0f;
			return;
		}

		// At his feet, not GlobalPosition: that is the middle of the sprite, about 36 px above his
		// boots, so lava used to burn a wizard standing on the grass below it. Same footprint the
		// water stops (Player.FootPosition).
		if (!terrainStreamer.IsHazardAtWorld(player.FootPosition))
		{
			hazardTickTimer = 0f;
			return;
		}

		hazardTickTimer += delta;
		if (hazardTickTimer >= HazardTickInterval)
		{
			hazardTickTimer -= HazardTickInterval;
			player.TakeDamage(HazardDamagePerTick);
		}
	}

	private bool TryBuildProceduralTerrain()
	{
		// Clear the placeholder room tilemap so it doesn't render underneath.
		GetNodeOrNull<TileMapLayer>("WorldTileMap")?.Clear();

		int stageIndex = Global.SelectedStageIdx;
		StageEnvironmentKind kind = StageEnvironmentCatalog.GetForStageIndex(stageIndex).Kind;

		// --terrain-seed=N pins the ground for a run, so two photographs of the same place (before and
		// after a tile change, _UiShot --teleport) show the same lakes and the same clearings.
		long seedOverride = ProceduralSeed;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--terrain-seed=") && long.TryParse(arg.Substring("--terrain-seed=".Length), out long parsed))
				seedOverride = parsed;
		}
		ulong seed = seedOverride != 0
			? (ulong)seedOverride
			: (ulong)((stageIndex + 1) * 73856093) ^ (ulong)DateTime.Now.Ticks;
		LevelLayout layout = new LevelGenerator().Describe(kind);

		if (levelPainter == null)
		{
			levelPainter = new LevelTilePainter
			{
				Name = "ProceduralTerrain",
				PaintDemoOnReady = false,
				TileSize = ProceduralTilePixels,
				ZIndex = -100,
				Position = stageOrigin,
			};
			AddChild(levelPainter);
		}

		levelPainter.GroundMaterial = layout.GroundMaterial;
		// Which water art (tools/art/water.py): a forest pond, swamp murk, the Frozen Waste's black
		// water with an ice shelf, or a stone pool. Each chapter's profile says which.
		levelPainter.WaterStyle = StageEnvironmentCatalog.Get(kind).WaterStyle;
		levelPainter.GroundTerrain = layout.GroundTerrain;
		levelPainter.BaseTerrain = layout.BaseTerrain;

		bool isMaze = layout.Topology == StageTopology.Maze;
		// THE STAGE HAS NO EDGE (issue #35). It used to be one 181x181 grid painted here, with the
		// player clamped three tiles inside it. Now the ground is streamed in chunks round wherever
		// the player is (TerrainStreamer, .ai/terrain.md "Chunk streaming"); nothing is painted yet,
		// because the first load also furnishes the chunks and the furnishing is set up later in
		// _Ready - see LoadInitialTerrain. Mazes keep corridor dead-ends (no cleanup) so the
		// labyrinth stays intact.
		terrainSeed = seed;
		terrainKind = kind;
		terrainStreamer = new TerrainStreamer(levelPainter, kind, seed, ProceduralTilePixels, stageOrigin,
			layout.BaseTerrain, cleanup: !isMaze, ProceduralSafeRadiusTiles, spawnClearRadius: 4f)
		{
			LoadRadius = Mathf.Max(1, TerrainLoadRadiusChunks),
		};
		// Landmarks are planned with the data, not the paint, so a garden fence is in the
		// navigation grid wherever the window reaches - see PlanChunkFeatures.
		terrainStreamer.PlanFeatures = PlanChunkFeatures;
		TerrainStreamer.LogTimings = OS.GetCmdlineUserArgs().Contains("--terrain-log");

		// Terrain-aware enemy navigation, on every procedural stage: maze walls and lakes alike. Its
		// window is the painted square, and it is filled the first time the streamer has loaded round
		// the player. A maze steers every enemy along it; an open stage only the ones whose straight
		// walk is blocked (Enemy, .ai/terrain.md section 9).
		int windowTiles = (2 * terrainStreamer.LoadRadius + 1) * TerrainStreamer.ChunkTiles;
		MazeNavigation.Active = isMaze || MazeNavigation.BuildOnOpenStages
			? new MazeNavigation(windowTiles, ProceduralTilePixels, stageOrigin, isMaze)
			: null;

		proceduralPropThemes = layout.PropThemes;
		terrainTerrainIds = layout.Terrains;

		// No clamp. ConstrainPlayerToStageBounds is left as the scene set it (false), so
		// ClampPositionToStageBounds hands every position straight back on a procedural stage.
		proceduralActive = true;
		return true;
	}

	// Paints and furnishes the chunks round the player in one go, before the first frame. Called
	// from _Ready once the decor, hazard and prop tables exist, since furnishing a chunk reads them.
	private void LoadInitialTerrain()
	{
		if (terrainStreamer == null || player == null)
			return;

		terrainStreamer.Populate = FurnishChunk;
		levelPainter?.PrewarmTerrainSources(terrainTerrainIds);
		levelPainter?.PrewarmDetailSources(GroundDetail.SetsFor(terrainKind));
		if (StageHasLandmarks)
			PreloadLandmarkTextures();
		terrainStreamer.LoadAllNow(player.GlobalPosition);
		RebuildNavigationIfDue(force: true);
	}

	// Per frame. The streamer does nothing unless the player has crossed into another chunk or it
	// has queued work; the navigation window moves once the streamer has caught up with a crossing.
	private void UpdateTerrainStreaming()
	{
		if (terrainStreamer == null || player == null || !IsInstanceValid(player))
			return;

		// The boss keeps the ground under it loaded too, so a fight the player drags a long way never
		// has the boss walking over unpainted ground where its walls and water have no bodies.
		Vector2? anchor = bossInstance != null && IsInstanceValid(bossInstance) ? bossInstance.GlobalPosition : null;
		terrainStreamer.Update(player.GlobalPosition, anchor);
		RebuildNavigationIfDue(force: false);
	}

	private const int MazeWindowSlackTiles = 8;

	private void RebuildNavigationIfDue(bool force)
	{
		MazeNavigation? nav = MazeNavigation.Active;
		if (nav == null || terrainStreamer == null || player == null)
			return;

		// A window being built is advanced one bounded step a frame (MazeNavigation.StepRebuild) -
		// more on a long frame, so a low frame rate does not stretch a move over seconds of game time.
		if (!force && nav.IsRebuilding)
		{
			int steps = MazeNavigation.FramesOfWork((float)GetProcessDeltaTime());
			long stageStart = System.Diagnostics.Stopwatch.GetTimestamp();
			bool swapped = false;
			for (int i = 0; i < steps && !swapped; i++)
				swapped = nav.StepRebuild(player.FootPosition);
			if (TerrainStreamer.LogTimings)
			{
				double stageMs = (System.Diagnostics.Stopwatch.GetTimestamp() - stageStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
				GD.Print($"TERRAIN nav window {steps} step(s) {(swapped ? "to swap" : "build")}: {stageMs:0.00} ms");
			}
			return;
		}

		if (!force && (!terrainStreamer.IsSettled
			|| (nav.HasWindow && nav.WindowCentreChunk == terrainStreamer.CentreChunk)))
			return;

		// Hysteresis. Moving the window is the most expensive thing streaming does, and a player
		// fighting on a chunk border crosses it back and forth every few seconds - measured, that
		// rebuilt the window twice a second. So it only moves once the player is MazeWindowSlackTiles
		// past the edge of the chunk it is centred on. The window reaches 2.5 chunks (80 tiles) out
		// from its centre, so the player is still 56 tiles from its edge when it moves: past the 53
		// at which Enemy recycles on walking distance, which is what the window has to cover.
		if (!force && nav.HasWindow)
		{
			Vector2I cell = terrainStreamer.CellOfWorld(player.GlobalPosition);
			Vector2I middle = nav.WindowCentreChunk * TerrainStreamer.ChunkTiles
				+ new Vector2I(TerrainStreamer.ChunkTiles / 2, TerrainStreamer.ChunkTiles / 2);
			int off = Mathf.Max(Mathf.Abs(cell.X - middle.X), Mathf.Abs(cell.Y - middle.Y));
			if (off <= TerrainStreamer.ChunkTiles / 2 + MazeWindowSlackTiles)
				return;
		}

		Vector2I centre = terrainStreamer.CentreChunk;
		int r = terrainStreamer.LoadRadius;
		var minCell = new Vector2I((centre.X - r) * TerrainStreamer.ChunkTiles, (centre.Y - r) * TerrainStreamer.ChunkTiles);
		if (force)
		{
			// Stage start: the run has not begun, so the whole window is built at once.
			nav.Rebuild(terrainStreamer, minCell, centre, player.FootPosition);
			return;
		}

		nav.BeginRebuild(terrainStreamer, minCell, centre);
		if (TerrainStreamer.LogTimings)
			GD.Print($"TERRAIN nav window moving to {centre}");
	}

	private void BuildPlaceholderRoomLayout()
	{
		var worldTileMap = GetNodeOrNull<TileMapLayer>("WorldTileMap");
		if (worldTileMap == null)
			return;

		worldTileMap.Clear();

		const int halfSize = 11;
		const int sourceId = 0;
		var atlasCoordsByRole = ValidateDungeonTileRoleAtlasCoords(worldTileMap, sourceId);
		var floorCoords = atlasCoordsByRole[DungeonTileRole.Floor];
		var floorAccentCoords = atlasCoordsByRole[DungeonTileRole.FloorAccent];
		var wallCoords = atlasCoordsByRole[DungeonTileRole.Wall];
		var wallCornerCoords = atlasCoordsByRole[DungeonTileRole.WallCorner];
		var detailCoords = atlasCoordsByRole[DungeonTileRole.Detail];

		for (int x = -halfSize; x <= halfSize; x++)
		{
			for (int y = -halfSize; y <= halfSize; y++)
			{
				var tileCoords = new Vector2I(x, y);
				bool isBorder = x == -halfSize || x == halfSize || y == -halfSize || y == halfSize;
				bool isInnerRing = Math.Abs(x) <= 1 || Math.Abs(y) <= 1;
				bool isCorner = (x == -halfSize || x == halfSize) && (y == -halfSize || y == halfSize);
				var atlasCoords = isBorder ? (isCorner ? wallCornerCoords : wallCoords) : floorCoords;

				if (!isBorder && isInnerRing && (x + y) % 3 == 0)
					atlasCoords = floorAccentCoords;
				if (!isBorder && (x == 0 && y == 0))
					atlasCoords = detailCoords;

				worldTileMap.SetCell(tileCoords, sourceId, atlasCoords);
			}
		}
	}

	private void ApplyAmbientStageEffects()
	{
		ClearAmbientOverlays();

		var canvas = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		if (canvas == null)
			return;

		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(Global.SelectedStageIdx);
		// Every value here is the chapter's own (StageEnvironmentProfile): this used to be three
		// switches with a line per environment, which a new chapter had to add to in three places.
		Color baseTint = environmentProfile.HazeTint;
		var haze = CreateAmbientOverlay(canvas, baseTint, Vector2.Zero);
		var glow = CreateAmbientOverlay(canvas, environmentProfile.GlowTint, new Vector2(120f, 80f));

		ColorRect? weatherOverlay = environmentProfile.WeatherTint is Color weather
			? CreateAmbientOverlay(canvas, weather, environmentProfile.WeatherOffset)
			: null;

		var baseTween = CreateTween();
		baseTween.SetLoops();
		baseTween.TweenProperty(haze, "color", new Color(baseTint.R, baseTint.G, baseTint.B, baseTint.A + 0.03f), 4.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		baseTween.TweenProperty(haze, "color", baseTint, 4.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		var driftTween = CreateTween();
		driftTween.SetLoops();
		driftTween.TweenProperty(glow, "position", new Vector2(220f, 180f), 7.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		driftTween.TweenProperty(glow, "position", new Vector2(120f, 80f), 7.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		if (weatherOverlay != null)
		{
			var weatherTween = CreateTween();
			weatherTween.SetLoops();
			weatherTween.TweenProperty(weatherOverlay, "position", new Vector2(20f, 30f), 9.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			weatherTween.TweenProperty(weatherOverlay, "position", new Vector2(-40f, -20f), 9.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}
	}

	// The overlay stretches to the viewport via its anchors. It used to also take an explicit size
	// (2000x1200 for the haze, 1400x900 for the glow, and so on) and assign it to overlay.Size,
	// which Godot rejects on a control whose anchors already determine its rect - it printed
	// "Can't change the size of a control with anchors set" for every overlay on every stage load
	// and kept the anchored rect regardless. Those sizes were authored for the old landscape
	// viewport and have never had any effect, so the parameter is gone rather than made live:
	// honouring 1400x900 on a 720x1280 portrait screen would leave the bottom 300 units untinted.
	private ColorRect CreateAmbientOverlay(CanvasLayer canvas, Color tint, Vector2? position = null)
	{
		var overlay = new ColorRect
		{
			Name = "AmbientOverlay",
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 0f,
			OffsetTop = 0f,
			OffsetRight = 0f,
			OffsetBottom = 0f,
			Color = tint,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = 1
		};
		if (position.HasValue)
			overlay.Position = position.Value;
		canvas.AddChild(overlay);
		ambientOverlays.Add(overlay);
		return overlay;
	}

	private void ClearAmbientOverlays()
	{
		foreach (ColorRect overlay in ambientOverlays)
		{
			if (IsInstanceValid(overlay))
				overlay.QueueFree();
		}
		ambientOverlays.Clear();
	}

	private readonly record struct StageVisualTheme(
		string BackgroundTexturePath,
		Rect2 BackgroundRegion,
		string OverlayTexturePath,
		Rect2 OverlayRegion,
		float BackgroundTextureScale,
		float OverlayTextureScale,
		Color BackgroundModulate,
		Color OverlayModulate,
		float OverlayScrollScale,
		float BackgroundTilePixelSize,
		int BushCount,
		int TreeCount,
		int RuinCount)
	{
		public static StageVisualTheme Default => new(
			"res://assets/bonelight/tiles/floor_graystone_0.png",
			new Rect2(),
			"res://assets/bonelight/tiles/floor_graystone_0.png",
			new Rect2(),
			0.30f,
			0.08f,
			new Color(1f, 1f, 1f, 1f),
			new Color(0.62f, 0.58f, 0.5f, 0.18f),
			0.0f,
			0f,
			10,
			6,
			26);
	}

	// Which hazards, if any, a stage is allowed to place.
	//
	// Spike plates and flame vents are *built* things - dungeon furniture. Scattering them across
	// an enchanted forest read as a bug, because nothing in a woodland clearing installed them.
	// So they are keyed to environments where somebody built them (Castle, Ruins) or where the
	// ground does it unaided (Volcanic, which gets vents and no spikes). Forest, Swamp, Ice and
	// Desert get none: an open natural stage is shaped by its enemies, not by traps. The mix is the
	// chapter's own (StageEnvironmentProfile.HazardSpikes / HazardFlameChance); flame vents are the
	// rarer, nastier one, so where both appear they stay the minority pick.
	private static (bool UseSpikes, float FlameChance) GetStageHazardMix(StageEnvironmentKind kind)
	{
		StageEnvironmentProfile profile = StageEnvironmentCatalog.Get(kind);
		return (profile.HazardSpikes, profile.HazardFlameChance);
	}

	// Scatters cycling spike traps and flame vents around the arena at stage start.
	//
	// Placed once rather than spawned over time, so a player can learn where they are: a hazard
	// that appears under you is a random tax, while one you walked past thirty seconds ago is a
	// piece of the arena you are expected to remember. They are kept well clear of the player's
	// start so nobody eats one before they have moved.
	private void BuildStageHazards()
	{
		if (!EnableStageHazards)
			return;

		StageEnvironmentKind kind = StageEnvironmentCatalog.GetForStageIndex(Global.SelectedStageIdx).Kind;
		(bool useSpikes, float flameChance) = GetStageHazardMix(kind);
		if (!useSpikes && flameChance <= 0f)
			return;

		var spikeScene = useSpikes ? ResourceLoader.Load<PackedScene>("res://scenes/SpikeTrap.tscn") : null;
		var flameScene = flameChance > 0f ? ResourceLoader.Load<PackedScene>("res://scenes/FlameVent.tscn") : null;
		if (spikeScene == null && flameScene == null)
			return;

		// Endless stage: every chunk gets its share (FurnishChunk), at the density this used to put
		// round the start, so the ground is furnished the same wherever the player walks - and a
		// hazard stays where it was, because a revisited chunk is furnished from the same seed.
		if (terrainStreamer != null)
		{
			hazardChunkMix = (spikeScene, flameScene, flameChance);
			return;
		}

		Vector2 origin = player != null && IsInstanceValid(player) ? player.GlobalPosition : Vector2.Zero;
		int placed = 0;
		for (int attempt = 0; attempt < StageHazardCount * 12 && placed < StageHazardCount; attempt++)
		{
			float angle = spawnRng.Randf() * Mathf.Tau;
			float distance = (float)spawnRng.RandfRange(StageHazardMinPlayerDistance, StageHazardSpread);
			Vector2 position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

			bool tooClose = false;
			foreach (Node node in GetTree().GetNodesInGroup("stage_hazards"))
			{
				if (node is Node2D other && IsInstanceValid(other)
					&& other.GlobalPosition.DistanceTo(position) < StageHazardSeparation)
				{
					tooClose = true;
					break;
				}
			}
			if (tooClose)
				continue;

			PackedScene chosen = (spawnRng.Randf() < flameChance ? flameScene : spikeScene) ?? spikeScene ?? flameScene;
			if (chosen == null)
				return;

			var hazard = chosen.Instantiate<Node2D>();
			hazard.GlobalPosition = position;
			if (hazard is StageHazard typedHazard)
				// Desynchronise the cycles, otherwise the whole field breathes in unison and the
				// arena reads as one big on/off switch instead of a set of separate obstacles.
				typedHazard.StartPhaseOffset = spawnRng.Randf() * 3.0f;
			AddChild(hazard);
			placed++;
		}
	}

	private void BuildDecorProps()
	{
		if (!EnableDecorProps)
			return;

		ClearDecorProps();
		// On an endless stage CreateDecorSet records each set instead of placing it; see FurnishChunk.
		decorChunkSpecs = terrainStreamer != null ? new List<DecorSetSpec>() : null;

		int stageIndex = Global.SelectedStageIdx;
		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(stageIndex);
		List<Texture2D> forestGroundAccents = LoadTexturesFromPaths(
			"res://assets/bonelight/world/rock-1.png",
			"res://assets/bonelight/world/rock-2.png");
		List<Texture2D> ruins = LoadTexturesFromPaths(
			"res://assets/bonelight/world/crystal-yellow.png",
			"res://assets/bonelight/world/crystal-violet.png");
		// What is drawn on the page. Generated by tools/art/kids_art.py alongside the paper -
		// the only things in assets/kidsart/ that the kids did not draw, and the file says so.
		List<Texture2D> doodles = LoadTexturesFromPaths(
			"res://assets/kidsart/doodle-stick-figure.png",
			"res://assets/kidsart/doodle-sun.png",
			"res://assets/kidsart/doodle-spiral.png",
			"res://assets/kidsart/doodle-scribble.png",
			"res://assets/kidsart/doodle-star.png",
			"res://assets/kidsart/doodle-writing.png",
			"res://assets/kidsart/doodle-house.png",
			"res://assets/kidsart/doodle-flower.png");
		List<Texture2D> volcanicAsh = LoadTexturesFromPaths(
			"res://assets/bonelight/world/rock-1.png",
			"res://assets/bonelight/world/rock-2.png");

		switch (environmentProfile.Kind)
		{
			case StageEnvironmentKind.Sketchbook:
				// Doodles on the page: a house, a sun, a spiral, somebody scribbled out, four
				// lines of pretend handwriting. Loosely clustered, because a page is not doodled
				// on evenly - things collect in a corner and then somebody starts again
				// somewhere else.
				//
				// Bigger than any enemy in the chapter, which is the right way round: a drawing
				// on the page should read as something the page HAS on it rather than as another
				// sprite. They sit behind everything but the paper itself.
				// Three hundred, which sounds like a lot and is not: the decor field is 8400
				// across and a screen is 720 by 1280, so a hundred and twenty works out at about a
				// fifth of one per screen - invisible. This is roughly one cluster in view at a
				// time, which is what a doodled-on page looks like.
				CreateDecorSet(doodles, 300, 1.0f, 1.7f, false, false, -44, -30, true, 4,
					BushClusterRadiusMin * 1.4f, BushClusterRadiusMax * 1.6f,
					BushClusterCenterSeparation * 1.3f, 0.22f);
				break;
			case StageEnvironmentKind.Forest:
				// THE NUMBERS ARE LARGE BECAUSE THE FIELD IS. Decor scatters over 8400x8400 and the
				// camera shows 720x1280 - about 1.2% of it - so a hundred objects put roughly one
				// on screen and the Enchanted Forest had no trees in it for as long as it existed.
				// These put six to eight in view, which reads as woodland while leaving the open
				// ground the swarm needs. Measured, not guessed: see .ai/terrain.md section 5.
				//
				// CLUSTER SIZE 3, DOWN FROM 8, MATTERS AS MUCH AS THE COUNT. Clustering divides the
				// count into clumps, so raising the count alone just makes the same few thickets
				// denser and leaves the ground between them as bare as before. Smaller clumps means
				// more of them.
				//
				// BIGGER TREES, FEWER OF THEM (2026-10, .ai/terrain.md section 10). The trees were
				// 96 px canopies, the wizard's own height; a grown oak is now 300 px. At the old 430
				// a screen carried six to eight crowns of that size and the wood was a wall with the
				// swarm hidden under it. 150 mature trees and 90 young ones put about three big
				// crowns and two small ones in view - woodland with clearings - and the bushes, which
				// you walk through, fill the ground between them at the old density.
				CreateGroundedDecorSet(GroundedSprites("bush-round-1", "bush-round-2", "bush-wide-1", "bush-berry-1", "bush-berry-2"),
					300, 3, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.06f, 44f);
				CreateGroundedDecorSet(GroundedSprites("oak-large-1", "oak-large-2", "oak-medium-1", "oak-medium-2", "fir-1", "fir-2"),
					150, 3, TreeClusterRadiusMin, TreeClusterRadiusMax * 1.1f, TreeClusterCenterSeparation * 0.85f, 0.08f, MatureTreeSpacing);
				CreateGroundedDecorSet(GroundedSprites("birch-1", "birch-2", "sapling-1"),
					90, 3, TreeClusterRadiusMin * 0.7f, TreeClusterRadiusMax * 0.8f, TreeClusterCenterSeparation * 0.6f, 0.12f, YoungTreeSpacing);
				CreateDecorSet(forestGroundAccents, 240, 0.88f, 1.02f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				break;
			case StageEnvironmentKind.Castle:
				CreateDecorSet(ruins, Math.Max(18, environmentProfile.RuinCount), 0.95f, 1.14f, false, false, -26, -14, true, 3, RuinClusterRadiusMin, RuinClusterRadiusMax, RuinClusterCenterSeparation, 0.06f);
				CreateDecorSet(forestGroundAccents, 8, 0.72f, 0.90f, false, false, -40, -28, true, 2, GroundAccentClusterRadiusMin * 0.82f, GroundAccentClusterRadiusMax * 0.82f, GroundAccentClusterCenterSeparation * 0.80f, 0.04f);
				break;
			case StageEnvironmentKind.Ruins:
				CreateDecorSet(forestGroundAccents, 18, 0.88f, 1.02f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				CreateDecorSet(ruins, Math.Max(16, environmentProfile.RuinCount), 0.95f, 1.12f, false, false, -28, -14, true, 3, RuinClusterRadiusMin, RuinClusterRadiusMax, RuinClusterCenterSeparation, 0.06f);
				// Trees that grew up through the stones: gnarled survivors and dead ones, sparse -
				// a ruin is open ground with things standing in it, not a wood - and brambles in the
				// rubble. There were eight 96 px trees per field here: one on about every seventh screen.
				CreateGroundedDecorSet(GroundedSprites("gnarled-1", "gnarled-2", "dead-tree-1"),
					48, 2, TreeClusterRadiusMin * 0.8f, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.15f, MatureTreeSpacing);
				CreateGroundedDecorSet(GroundedSprites("bramble-1", "bramble-2"),
					110, 3, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.08f, 40f);
				break;
			case StageEnvironmentKind.Ice:
				CreateDecorSet(forestGroundAccents, 16, 0.80f, 0.96f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax * 0.82f, GroundAccentClusterCenterSeparation * 0.86f, 0.05f);
				CreateDecorSet(ruins, 10, 0.82f, 0.96f, false, false, -28, -14, true, 3, RuinClusterRadiusMin * 0.78f, RuinClusterRadiusMax * 0.78f, RuinClusterCenterSeparation * 0.82f, 0.06f);
				CreateDecorSet(volcanicAsh, 6, 0.54f, 0.74f, false, false, -34, -24, true, 2, GroundAccentClusterRadiusMin * 0.60f, GroundAccentClusterRadiusMax * 0.60f, GroundAccentClusterCenterSeparation * 0.70f, 0.04f);
				break;
			case StageEnvironmentKind.Desert:
				CreateDecorSet(forestGroundAccents, 12, 0.84f, 0.98f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin * 0.86f, GroundAccentClusterRadiusMax * 0.86f, GroundAccentClusterCenterSeparation * 0.90f, 0.05f);
				CreateDecorSet(ruins, 12, 0.88f, 1.02f, false, false, -28, -14, true, 3, RuinClusterRadiusMin * 0.86f, RuinClusterRadiusMax * 0.86f, RuinClusterCenterSeparation * 0.9f, 0.06f);
				CreateDecorSet(volcanicAsh, 8, 0.58f, 0.78f, false, false, -36, -26, true, 2, GroundAccentClusterRadiusMin * 0.72f, GroundAccentClusterRadiusMax * 0.72f, GroundAccentClusterCenterSeparation * 0.78f, 0.04f);
				// The Sands had nothing growing at all. A few sun-bleached dead trees, mostly alone
				// (a desert does not do groves), and dead scrub in loose drifts: something to read
				// distance and direction by on the most featureless floor in the game.
				CreateGroundedDecorSet(GroundedSprites("bleached-tree-1", "bleached-tree-2"),
					26, 2, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.45f, MatureTreeSpacing);
				CreateGroundedDecorSet(GroundedSprites("dead-bush-1", "dead-bush-2", "dry-scrub-1"),
					150, 3, BushClusterRadiusMin * 1.2f, BushClusterRadiusMax * 1.3f, BushClusterCenterSeparation, 0.2f, 40f);
				break;
			case StageEnvironmentKind.Volcanic:
				CreateDecorSet(ruins, Math.Max(14, environmentProfile.RuinCount), 0.90f, 1.06f, false, false, -28, -14, true, 3, RuinClusterRadiusMin * 0.88f, RuinClusterRadiusMax * 0.88f, RuinClusterCenterSeparation * 0.90f, 0.06f);
				CreateDecorSet(forestGroundAccents, 8, 0.84f, 0.96f, false, false, -42, -34, true, 2, GroundAccentClusterRadiusMin * 0.78f, GroundAccentClusterRadiusMax * 0.78f, GroundAccentClusterCenterSeparation * 0.86f, 0.05f);
				CreateDecorSet(volcanicAsh, 12, 0.70f, 0.94f, false, false, -36, -22, true, 3, GroundAccentClusterRadiusMin * 0.76f, GroundAccentClusterRadiusMax * 0.76f, GroundAccentClusterCenterSeparation * 0.82f, 0.04f);
				break;
			case StageEnvironmentKind.Swamp:
				// Willows on the banks and drowned snags hung with moss, sparse enough that the swamp
				// stays open water and mud; sedge tussocks and dark shrubs between them, which you
				// wade through. It had eighteen bushes and eight small trees per field before - one
				// tree on about every seventh screen.
				CreateGroundedDecorSet(GroundedSprites("reed-tuft-1", "reed-tuft-2", "swamp-shrub-1"),
					220, 4, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.06f, 36f);
				CreateGroundedDecorSet(GroundedSprites("willow-1", "willow-2"),
					50, 2, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.15f, MatureTreeSpacing);
				CreateGroundedDecorSet(GroundedSprites("swamp-snag-1", "swamp-snag-2"),
					60, 3, TreeClusterRadiusMin * 0.8f, TreeClusterRadiusMax, TreeClusterCenterSeparation * 0.8f, 0.2f, YoungTreeSpacing);
				break;
			case StageEnvironmentKind.Fields:
			{
				// THE FALLOW FIELDS (.ai/campaign.md section 5). Open ground first - a field is what
				// the wood was cleared to make: hedgerow bushes in long loose drifts (big clusters,
				// spread wide), a few field trees standing alone, stooks of sheaves drying in groups,
				// runs of broken fence, the odd stone. Stooks and fences are walked through like the
				// bushes (Trunk 0), so the ground still plays as open.
				//
				// The stooks and fences are tools/art/props_fields.py's, which writes no manifest: its
				// one convention is that a PNG's bottom edge is its ground line and its middle column
				// stands on the point - GroundedSprite(ground = height, anchor = width / 2).
				List<GroundedSprite> FieldsProps(params string[] ids)
				{
					var list = new List<GroundedSprite>(ids.Length);
					foreach (string id in ids)
					{
						string path = $"res://assets/bonelight/world/fields/{id}.png";
						Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
						if (texture == null)
						{
							GD.PushError($"Node2DGame: the Fields prop '{path}' is missing or not imported. Re-run: python tools/art/props_fields.py, then import.");
							continue;
						}
						int w = texture.GetWidth(), h = texture.GetHeight();
						list.Add(new GroundedSprite(id, "bush", texture, w, h, h, w / 2, 0));
					}
					return list;
				}

				CreateGroundedDecorSet(GroundedSprites("bush-round-1", "bush-round-2", "bush-wide-1", "bush-berry-1"),
					260, 6, BushClusterRadiusMin, BushClusterRadiusMax * 1.7f, BushClusterCenterSeparation * 1.3f, 0.05f, 40f);
				CreateGroundedDecorSet(GroundedSprites("oak-large-1", "oak-medium-1", "oak-medium-2"),
					36, 1, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation * 1.4f, 0.3f, MatureTreeSpacing);
				CreateGroundedDecorSet(FieldsProps("stook-1", "stook-2"),
					120, 5, BushClusterRadiusMin, BushClusterRadiusMax * 1.2f, BushClusterCenterSeparation * 1.4f, 0.1f, 56f);
				CreateGroundedDecorSet(FieldsProps("fence-1", "fence-2"),
					40, 2, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation * 1.6f, 0.3f, 96f);
				CreateDecorSet(forestGroundAccents, 50, 0.84f, 0.98f, false, false, -42, -34, true, 2, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				break;
			}
			case StageEnvironmentKind.Heath:
			{
				// THE BROKEN CIRCLE (.ai/campaign.md section 6). A high heath, open ground first: the
				// stones of the old circles standing, leaning and fallen in the heather, in twos and
				// threes; gorse and bramble in low drifts; a dead tree alone here and there, bent by
				// the wind; granite lying about. The stones and the gorse are tools/art/props_heath.py,
				// whose heath.json is in trees.json's shape, so they go through the same grounded path
				// as the trees: sorted at their feet, a stone's foot on the ground's physics layer
				// where the wizard's boots meet it, the gorse walked through. None carries a mark or a
				// split face - those are the Ninth Stone's, and a stone that had them would be the
				// boss standing still.
				var heath = new Dictionary<string, GroundedSprite>();
				const string HeathManifest = "res://assets/bonelight/world/heath/heath.json";
				string heathJson = Godot.FileAccess.GetFileAsString(HeathManifest);
				if (string.IsNullOrEmpty(heathJson))
					GD.PushError($"Node2DGame: no {HeathManifest}. Re-run: python tools/art/props_heath.py, then import.");
				else
				{
					using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(heathJson);
					foreach (System.Text.Json.JsonElement s in doc.RootElement.GetProperty("sprites").EnumerateArray())
					{
						string id = s.GetProperty("id").GetString();
						string path = s.GetProperty("path").GetString();
						Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
						if (texture == null)
						{
							GD.PushError($"Node2DGame: heath sprite '{id}' ({path}) is not imported. Run the Godot import.");
							continue;
						}
						heath[id] = new GroundedSprite(id, s.GetProperty("kind").GetString(), texture,
							s.GetProperty("width").GetInt32(), s.GetProperty("height").GetInt32(),
							s.GetProperty("ground").GetInt32(), s.GetProperty("anchor_x").GetInt32(),
							s.GetProperty("trunk").GetInt32());
					}
				}
				List<GroundedSprite> HeathSet(params string[] ids) =>
					ids.Where(heath.ContainsKey).Select(id => heath[id]).ToList();

				// About one stone and a half on a screen: standing in small groups, fallen ones apart.
				CreateGroundedDecorSet(HeathSet("standing-stone-1", "standing-stone-2", "standing-stone-3"),
					70, 2, TreeClusterRadiusMin * 0.7f, TreeClusterRadiusMax * 0.8f, TreeClusterCenterSeparation, 0.3f, YoungTreeSpacing);
				CreateGroundedDecorSet(HeathSet("fallen-stone-1", "fallen-stone-2"),
					40, 1, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.5f, MatureTreeSpacing);
				List<GroundedSprite> drifts = HeathSet("gorse-1", "gorse-2");
				drifts.AddRange(GroundedSprites("bramble-2"));
				CreateGroundedDecorSet(drifts,
					180, 4, BushClusterRadiusMin, BushClusterRadiusMax * 1.3f, BushClusterCenterSeparation, 0.08f, 40f);
				CreateGroundedDecorSet(GroundedSprites("dead-tree-1", "gnarled-2"),
					22, 1, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation * 1.3f, 0.4f, MatureTreeSpacing);
				CreateDecorSet(forestGroundAccents, 50, 0.86f, 1.06f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				break;
			}
			default:
				// Today that is the Cave (and the placeholder room). Crystals and rubble as before;
				// what grows underground is bramble and the odd dead tree where the roof has fallen
				// in - not the Forest's oaks, which this case used to borrow.
				CreateDecorSet(forestGroundAccents, 20, 0.86f, 1.00f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				CreateDecorSet(ruins, 24, 0.90f, 1.18f, false, false, -30, -14, true, 4, RuinClusterRadiusMin, RuinClusterRadiusMax * 1.15f, RuinClusterCenterSeparation * 0.88f, 0.06f);
				CreateGroundedDecorSet(GroundedSprites("bramble-1", "bramble-2"),
					60, 3, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.08f, 40f);
				CreateGroundedDecorSet(GroundedSprites("dead-tree-1", "gnarled-2"),
					14, 2, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.3f, MatureTreeSpacing);
				break;
		}
	}

	// Scatters curated mine/torture props onto open ground. Obstacle props get a StaticBody2D
	// (block movement); decoration props are walkable Sprite2D nodes. Placement avoids the
	// spawn area, hazards, and liquid terrain (water/ice/lava/pit) via the level painter.
	private static readonly HashSet<string> PropAvoidTerrains = new() { "water", "ice", "lava", "pit", "wall" };
	private const string CuratedPropGroup = "curated_props";

	private void BuildCuratedProps()
	{
		if (!EnableCuratedProps || CuratedPropCount <= 0)
			return;

		// Not on the page. The curated props are a dungeon pack - barrels, chains, braziers - and
		// PropCatalog.Load treats an empty theme list as "no filter" rather than "nothing", so
		// leaving the Sketchbook to fall through put a teal crystal on it.
		if (IsSketchbookStage)
			return;

		ClearCuratedProps();

		propCatalog ??= PropCatalog.Load(
			proceduralPropThemes,
			// One manifest of ours where there were two bought ones. Twenty-one props, three per
			// theme, against the pack's hundred and seventy-four - the palettes ask for themes
			// rather than for counts, so the scatter is as varied as the theme list, and a
			// smaller set that we can publish beats a larger one that we cannot.
			"res://assets/bonelight/world/props/props.json");

		IReadOnlyList<PropEntry> props = propCatalog.FloorProps;
		if (props.Count == 0)
			return;

		// Endless stage: remembered and scattered per chunk (FurnishChunk), not placed here.
		if (terrainStreamer != null)
		{
			curatedChunkProps = props;
			return;
		}

		int placed = 0;
		int attempts = 0;
		int maxAttempts = CuratedPropCount * 8;
		while (placed < CuratedPropCount && attempts < maxAttempts)
		{
			attempts++;
			Vector2 position = SampleDecorPosition();

			if (IsPropAvoidedAt(position))
				continue;

			PropEntry entry = props[spawnRng.RandiRange(0, props.Count - 1)];
			Texture2D texture = LoadPropTexture(entry.ResPath);
			if (texture == null)
				continue;

			float scale = spawnRng.RandfRange(CuratedPropMinScale, CuratedPropMaxScale);
			CreateCuratedPropNode(this, entry, texture, position, scale, spawnRng.Randf() < 0.5f);
			placed++;
		}
	}

	private Node2D CreateCuratedPropNode(Node parent, PropEntry entry, Texture2D texture, Vector2 position, float scale, bool flipH)
	{
		var sprite = new Sprite2D
		{
			Texture = texture,
			Centered = true,
			ZIndex = 0,
		};
		sprite.Scale = new Vector2(scale, scale);
		sprite.FlipH = flipH;

		Node2D propNode;
		if (entry.IsObstacle)
		{
			Vector2 texSize = texture.GetSize() * scale;
			var body = new StaticBody2D
			{
				Position = position,
				CollisionLayer = 1,
				CollisionMask = 0,
				YSortEnabled = false,
			};
			var collisionShape = new CollisionShape2D
			{
				Position = new Vector2(0f, texSize.Y * 0.20f),
				Shape = new RectangleShape2D
				{
					Size = new Vector2(
						Mathf.Max(8f, texSize.X * 0.55f),
						Mathf.Max(6f, texSize.Y * 0.34f)),
				},
			};
			body.AddChild(collisionShape);
			body.AddChild(sprite);
			propNode = body;
		}
		else
		{
			sprite.Position = position;
			sprite.ZIndex = -20; // walkable decoration sits under entities
			propNode = sprite;
		}

		propNode.AddToGroup(CuratedPropGroup);
		AddProp(parent, propNode);
		return propNode;
	}

	private void ClearCuratedProps()
	{
		foreach (Node node in GetTree().GetNodesInGroup(CuratedPropGroup))
			node.QueueFree();
	}

	private static Texture2D LoadPropTexture(string resPath)
	{
		if (ResourceLoader.Exists(resPath))
			return GD.Load<Texture2D>(resPath);
		Image img = Image.LoadFromFile(resPath);
		return img != null ? ImageTexture.CreateFromImage(img) : null;
	}

	private void ShowStageIntroLabel()
	{
		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(Global.SelectedStageIdx);
		string text = IsBossRush
			? $"Boss Rush • {GetCurrentStageName()}\n{BossRushRosterCount} wardens, one after another. The last is his."
			: $"{GetCurrentStageName()} • {environmentProfile.DisplayName}\n{GetCurrentStageFlavorText()}";
		ShowRunBanner(text, environmentProfile.IntroTint, 2.4f, 0.6f);
	}

	// These were two ten-entry switches naming stages that StageSelection had never heard of and
	// GameOverScreen named differently. StageCatalog is the one roster now.
	private string GetCurrentStageName() =>
		StageCatalog.GetByIndex(Global.SelectedStageIdx)?.DisplayName ?? "Unknown Region";

	private string GetCurrentStageFlavorText() =>
		StageCatalog.GetByIndex(Global.SelectedStageIdx)?.FlavorText ?? string.Empty;

	private void ClearDecorProps()
	{
		foreach (Node node in GetTree().GetNodesInGroup(DecorPropGroup))
		{
			if (node.GetParent() == this)
				node.QueueFree();
		}
	}

	private List<Texture2D> LoadTexturesFromFolder(string resFolder)
	{
		var textures = new List<Texture2D>();
		string folderPath = ProjectSettings.GlobalizePath(resFolder);
		if (!Directory.Exists(folderPath))
			return textures;

		foreach (string filePath in Directory.GetFiles(folderPath, "*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
		{
			string resPath = filePath.Replace('\\', '/');
			string projectRoot = ProjectSettings.GlobalizePath("res://").Replace('\\', '/').TrimEnd('/');
			if (!resPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
				continue;

			resPath = "res://" + resPath.Substring(projectRoot.Length + 1);
			Texture2D? texture = ResourceLoader.Load<Texture2D>(resPath);
			if (texture != null)
				textures.Add(texture);
		}

		return textures;
	}

	private List<Texture2D> LoadTexturesFromPaths(params string[] resPaths)
	{
		var textures = new List<Texture2D>();
		foreach (string resPath in resPaths)
		{
			Texture2D? texture = ResourceLoader.Load<Texture2D>(resPath);
			if (texture != null)
				textures.Add(texture);
		}

		return textures;
	}

	private void CreateDecorSet(
		List<Texture2D> textures,
		int count,
		float minScale,
		float maxScale,
		bool randomRotation,
		bool allowYFlip,
		int minZ,
		int maxZ,
		bool useClustering,
		int clusterTargetSize,
		float clusterRadiusMin,
		float clusterRadiusMax,
		float clusterCenterSeparation,
		float clusterOutlierChance)
	{
		if (textures.Count == 0 || count <= 0)
			return;

		// On an endless stage the set is not placed here: it is remembered, and every chunk the
		// streamer paints scatters its share of it (FurnishChunk). The counts the callers pass were
		// tuned for the old 8400 x 8400 decor field, so they are read as a DENSITY over that field.
		if (decorChunkSpecs != null)
		{
			decorChunkSpecs.Add(new DecorSetSpec(textures, count, minScale, maxScale, randomRotation, allowYFlip,
				useClustering && EnableBiomeClustering, clusterTargetSize, clusterRadiusMin, clusterRadiusMax,
				clusterCenterSeparation, clusterOutlierChance));
			return;
		}

		List<Vector2> spawnPositions = useClustering && EnableBiomeClustering
			? BuildClusteredPositions(count, clusterTargetSize, clusterRadiusMin, clusterRadiusMax, clusterCenterSeparation, clusterOutlierChance)
			: BuildUniformPositions(count);

		for (int i = 0; i < spawnPositions.Count; i++)
		{
			Vector2 position = spawnPositions[i];
			Texture2D texture = textures[spawnRng.RandiRange(0, textures.Count - 1)];

			if (IsPropAvoidedAt(position))
				continue;
			// Quantised to whole pixels: the project filters nearest now, and a fractional
			// prop scale aliases every edge of a hand-drawn sprite. Every range the callers
			// below pass sits between 0.72 and 1.22, so today they all land on 1 - the
			// per-biome numbers stay as relative intent for migration phase 6, which adds
			// size variety by drawing more props rather than by rescaling one.
			float scale = Mathf.Max(1f, Mathf.Round(spawnRng.RandfRange(minScale, maxScale)));
			bool flipH = spawnRng.Randf() < 0.45f;
			bool flipV = allowYFlip && spawnRng.Randf() < 0.1f;
			float rotation = randomRotation ? Mathf.DegToRad(spawnRng.RandfRange(-8.0f, 8.0f)) : 0f;
			CreateDecorPropNode(this, texture, position, scale, flipH, flipV, rotation);
		}
	}

	// --- Trees and bushes that stand on the ground (.ai/terrain.md section 10) ---------------------
	//
	// tools/art/trees.py draws them in three-quarter view - a trunk on the ground and a crown above
	// it - at two to four times the wizard's height, and writes each sprite's ground line and trunk
	// column into trees.json. Three things follow from standing on a ground line:
	//
	//   1. SORTED AT THE FEET. The node is WorldSprites.SortLift above the ground line, the texture
	//      hangs below it, so y-sort draws the wizard in front of a trunk when his boots are south of
	//      its foot and behind the crown when he is north of it.
	//   2. ONLY THE TRUNK BLOCKS, AND IT MEETS HIS BOOTS. A trunk is a small rectangle at the ground
	//      line on the ground's physics layer (LevelTilePainter.TerrainCollisionLayer), which the
	//      player meets with his Feet and enemies with their bodies, like water - so the boots stop
	//      where the trunk is drawn and the crown never stops anything. The old decor collided with
	//      his body on layer 1, a box round the sprite's middle, which for a 300 px tree would be a
	//      wall at head height. Ghost enemies still pass through: the body is in DecorPropGroup.
	//   3. BUSHES BLOCK NOTHING. Knee-high, a sprite and no body: one node where the old bush was
	//      three. A swarm squeezing through a thicket of small solid bodies is a swarm wedging on
	//      them; one wading through shrubs looks right and plays the same as open ground.
	//
	// Trunks are deliberately not in the navigation grid: 12-28 px wide and at least
	// MatureTreeSpacing apart, a body slides off one. _MazeProbe on the Forest measured whether that
	// held (section 10).

	private const float MatureTreeSpacing = 120f;
	private const float YoungTreeSpacing = 72f;
	// How far trees and bushes keep from a landmark, in cells: a 300 px crown stood just south of a
	// garden's fence would hide the garden, and a trunk outside a gate would half close it.
	private const int TreeLandmarkMarginCells = 3;
	private const int BushLandmarkMarginCells = 1;
	// The depth of a trunk's footprint, world px, ending 2 px below the ground line.
	private const float TrunkFootDepth = 12f;

	private Dictionary<string, GroundedSprite>? worldTrees;
	private ShaderMaterial? canopyMaterial;
	private static readonly StringName CanopyPlayerParam = "player_pos";
	// Read every frame by ProcessTick. A string literal there is a new NodePath or StringName each
	// frame, each one finalizable - built once here instead (.ai/performance.md).
	private static readonly NodePath BackgroundPath = "CanvasLayer/Background";
	private static readonly NodePath BackgroundOverlayPath = "CanvasLayer/BackgroundOverlay";
	private static readonly NodePath CameraPath = "Camera2D";
	private static readonly StringName ScrollOffsetParam = "scroll_offset";
	private static readonly StringName WorldOffsetParam = "world_offset";
	private static readonly StringName CameraZoomParam = "camera_zoom";

	private ShaderMaterial CanopyMaterial => canopyMaterial ??= new ShaderMaterial
	{
		Shader = GD.Load<Shader>("res://scenes/shaders/canopy_fade.gdshader"),
	};

	private List<GroundedSprite> GroundedSprites(params string[] ids)
	{
		worldTrees ??= WorldSprites.LoadTrees();
		var list = new List<GroundedSprite>(ids.Length);
		foreach (string id in ids)
		{
			if (worldTrees.TryGetValue(id, out GroundedSprite? sprite) && sprite.Texture != null)
				list.Add(sprite);
			else
				GD.PushError($"Node2DGame: no tree or bush '{id}' in {WorldSprites.TreesManifest}, or it is not imported. "
					+ "Re-run: python tools/art/trees.py, then import.");
		}
		return list;
	}

	private void CreateGroundedDecorSet(List<GroundedSprite> sprites, int count, int clusterTargetSize,
		float clusterRadiusMin, float clusterRadiusMax, float clusterCenterSeparation, float clusterOutlierChance,
		float minSpacing)
	{
		if (sprites.Count == 0 || count <= 0)
			return;
		var spec = new DecorSetSpec(new List<Texture2D>(), count, 1f, 1f, false, false,
			EnableBiomeClustering, clusterTargetSize, clusterRadiusMin, clusterRadiusMax,
			clusterCenterSeparation, clusterOutlierChance, sprites, minSpacing);
		if (decorChunkSpecs != null)
		{
			decorChunkSpecs.Add(spec);
			return;
		}

		// The placeholder room (no streamed terrain): placed once, over the old fixed field.
		List<Vector2> positions = spec.Clustered
			? BuildClusteredPositions(count, clusterTargetSize, clusterRadiusMin, clusterRadiusMax, clusterCenterSeparation, clusterOutlierChance)
			: BuildUniformPositions(count);
		foreach (Vector2 position in positions)
		{
			GroundedSprite art = sprites[spawnRng.RandiRange(0, sprites.Count - 1)];
			if (!IsPropAvoidedAt(position))
				CreateGroundedDecor(this, art, position, spawnRng.Randf() < 0.5f);
		}
	}

	/// <summary>
	/// One tree or bush standing at <paramref name="ground"/> (the point its trunk meets the ground).
	/// Returns the trunk's body for a tree, null for a bush.
	/// </summary>
	private StaticBody2D? CreateGroundedDecor(Node parent, GroundedSprite art, Vector2 ground, bool flipH)
	{
		// Whole pixels, so the art stays on the grid.
		var origin = new Vector2(Mathf.Round(ground.X), Mathf.Round(ground.Y - WorldSprites.SortLift));
		int anchor = flipH ? art.Width - art.AnchorX : art.AnchorX;
		var sprite = new Sprite2D
		{
			Texture = art.Texture,
			Centered = false,
			FlipH = flipH,
			Offset = new Vector2(-anchor, WorldSprites.SortLift - art.Ground),
		};

		if (art.Trunk <= 0)
		{
			sprite.Position = origin;
			sprite.AddToGroup(DecorPropGroup);
			AddProp(parent, sprite);
			return null;
		}

		sprite.Material = CanopyMaterial;
		var body = new StaticBody2D
		{
			Position = origin,
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D
		{
			Position = new Vector2(0f, WorldSprites.SortLift + 2f - TrunkFootDepth * 0.5f),
			Shape = new RectangleShape2D { Size = new Vector2(art.Trunk, TrunkFootDepth) },
		});
		body.AddChild(sprite);
		body.AddToGroup(DecorPropGroup);
		AddProp(parent, body);
		return body;
	}

	// Per frame: where the wizard is, for every crown at once (canopy_fade.gdshader). One uniform on
	// one shared material, so it costs the same with ten trees on the stage as with a thousand.
	private void UpdateCanopyFade()
	{
		if (canopyMaterial != null && player != null && IsInstanceValid(player))
			canopyMaterial.SetShaderParameter(CanopyPlayerParam, player.GlobalPosition);
	}

	// Props placed straight on the game go to the front of its children, as they always have. A
	// chunk's holder is small and y-sorted with the game (nested y-sort merges), so order there is moot
	// - and MoveChild on the game is linear in its thousands of children, once per prop.
	private void AddProp(Node parent, Node prop)
	{
		parent.AddChild(prop);
		if (parent == this)
			MoveChild(prop, 0);
	}

	// True where no prop, decor or stage hazard may stand: a hazard tile or a terrain in
	// PropAvoidTerrains (water, ice, lava, pit, maze wall).
	private bool IsPropAvoidedAt(Vector2 position)
	{
		if (terrainStreamer == null)
			return false;
		if (terrainStreamer.IsHazardAtWorld(position))
			return true;
		string terrain = terrainStreamer.TerrainAtWorld(position);
		return terrain != null && PropAvoidTerrains.Contains(terrain);
	}

	// parent is the game itself for the one-shot fallback, or a chunk's prop holder when streaming.
	private StaticBody2D CreateDecorPropNode(Node parent, Texture2D texture, Vector2 position, float scale, bool flipH, bool flipV, float rotation)
	{
		var propBody = new StaticBody2D
		{
			Position = position,
			CollisionLayer = 1,
			CollisionMask = 0,
			YSortEnabled = false
		};

		var sprite = new Sprite2D
		{
			Texture = texture,
			Centered = true,
			ZIndex = 0
		};
		sprite.Scale = new Vector2(scale, scale);
		sprite.FlipH = flipH;
		sprite.FlipV = flipV;
		sprite.Rotation = rotation;

		Vector2 texSize = texture.GetSize() * scale;
		var collisionShape = new CollisionShape2D
		{
			Position = new Vector2(0f, texSize.Y * 0.22f),
			Shape = new RectangleShape2D
			{
				Size = new Vector2(
					Mathf.Max(8f, texSize.X * 0.42f),
					Mathf.Max(6f, texSize.Y * 0.24f))
			}
		};

		propBody.AddChild(collisionShape);
		propBody.AddChild(sprite);
		propBody.AddToGroup(DecorPropGroup);
		AddProp(parent, propBody);
		return propBody;
	}

	private List<Vector2> BuildUniformPositions(int count)
	{
		var result = new List<Vector2>(count);
		for (int i = 0; i < count; i++)
			result.Add(SampleDecorPosition());

		return result;
	}

	private List<Vector2> BuildClusteredPositions(
		int count,
		int targetSize,
		float radiusMin,
		float radiusMax,
		float centerSeparation,
		float outlierChance)
	{
		int targetClusterSize = Mathf.Max(2, targetSize);
		int clusterCount = Mathf.Clamp(Mathf.CeilToInt((float)count / targetClusterSize), 1, count);

		var centers = new List<Vector2>(clusterCount);
		for (int i = 0; i < clusterCount; i++)
		{
			Vector2 center = SampleDecorPosition();
			for (int tryIndex = 0; tryIndex < 14; tryIndex++)
			{
				Vector2 candidate = SampleDecorPosition();
				bool separated = true;
				foreach (Vector2 existing in centers)
				{
					if (candidate.DistanceTo(existing) < centerSeparation)
					{
						separated = false;
						break;
					}
				}

				if (separated)
				{
					center = candidate;
					break;
				}
			}

			centers.Add(center);
		}

		var result = new List<Vector2>(count);
		for (int i = 0; i < count; i++)
		{
			if (spawnRng.Randf() < outlierChance)
			{
				result.Add(SampleDecorPosition());
				continue;
			}

			Vector2 center = centers[spawnRng.RandiRange(0, centers.Count - 1)];
			float radius = spawnRng.RandfRange(radiusMin, radiusMax) * Mathf.Sqrt(spawnRng.Randf());
			float angle = spawnRng.RandfRange(0f, Mathf.Tau);
			Vector2 clustered = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

			clustered = new Vector2(
				Mathf.Clamp(clustered.X, stageOrigin.X - DecorFieldHalfWidth, stageOrigin.X + DecorFieldHalfWidth),
				Mathf.Clamp(clustered.Y, stageOrigin.Y - DecorFieldHalfHeight, stageOrigin.Y + DecorFieldHalfHeight));

			if (clustered.DistanceTo(stageOrigin) < DecorPlayerSafeRadius)
				clustered = SampleDecorPosition();

			result.Add(clustered);
		}

		return result;
	}

	private Vector2 SampleDecorPosition()
	{
		Vector2 position = stageOrigin;
		for (int tryIndex = 0; tryIndex < 16; tryIndex++)
		{
			float x = spawnRng.RandfRange(stageOrigin.X - DecorFieldHalfWidth, stageOrigin.X + DecorFieldHalfWidth);
			float y = spawnRng.RandfRange(stageOrigin.Y - DecorFieldHalfHeight, stageOrigin.Y + DecorFieldHalfHeight);
			position = new Vector2(x, y);
			if (position.DistanceTo(stageOrigin) >= DecorPlayerSafeRadius)
				break;
		}

		return position;
	}

	// --- Furnishing streamed chunks (endless stages) ----------------------------------------------
	//
	// Decor, themed props and stage hazards used to be scattered once over a fixed field at stage
	// start. On an endless stage each chunk the streamer paints gets its share, and loses it again
	// when the chunk is forgotten. Three rules:
	//
	//   1. SAME DENSITY. Every count below was tuned over the old 8400 x 8400 decor field (or, for
	//      hazards, the disc of StageHazardSpread round the start), so a chunk gets count times its
	//      share of that area. .ai/terrain.md section 4 measured what those densities look like.
	//   2. SAME PLACE ON A REVISIT. Each chunk draws from a generator seeded by (stage seed, chunk,
	//      set), never from spawnRng, so the tree the player walked past is still there when he walks
	//      back - which matters most for stage hazards, whose whole point is being learnable.
	//   3. CLUMPS SURVIVE. A sparse set cannot be split as "count / chunks" without losing its
	//      clusters - eight rocks over the field is a fraction of a rock per chunk. So the CLUSTER is
	//      the unit: a chunk rolls how many clumps it holds, each a full clump, and a member may fall
	//      a little over the border (its terrain is asked of the neighbour's data, which the streamer
	//      has ready before it calls this).

	// Grounded, when set, replaces Textures: trees and bushes that stand on a ground line
	// (CreateGroundedDecor), kept MinSpacing apart within the set and the chunk.
	private sealed record DecorSetSpec(
		List<Texture2D> Textures, int Count, float MinScale, float MaxScale, bool RandomRotation, bool AllowYFlip,
		bool Clustered, int ClusterTargetSize, float ClusterRadiusMin, float ClusterRadiusMax,
		float ClusterCenterSeparation, float ClusterOutlierChance,
		List<GroundedSprite>? Grounded = null, float MinSpacing = 0f);

	private List<DecorSetSpec>? decorChunkSpecs;
	private IReadOnlyList<PropEntry>? curatedChunkProps;
	private (PackedScene? Spike, PackedScene? Flame, float FlameChance)? hazardChunkMix;

	private const int SaltDecor = 0x0DEC0;
	private const int SaltCurated = 0x0C0A7;
	private const int SaltHazards = 0x0BA2D;

	// One part per call, so the streamer can spread a heavy chunk over frames: part 0 makes the
	// chunk's prop holder, part 1 lays the ground detail and any landmark, then one part per decor
	// set, then the themed props, then the hazards. Returns true once the last part has run.
	private bool FurnishChunk(TerrainChunk chunk, int part)
	{
		float chunkPixels = TerrainStreamer.ChunkTiles * ProceduralTilePixels;
		Vector2 min = stageOrigin + new Vector2(chunk.MinCell.X, chunk.MinCell.Y) * ProceduralTilePixels;
		var rect = new Rect2(min, new Vector2(chunkPixels, chunkPixels));
		float decorShare = (chunkPixels * chunkPixels) / Mathf.Max(1f, 4f * DecorFieldHalfWidth * DecorFieldHalfHeight);
		int decorSets = decorChunkSpecs?.Count ?? 0;

		if (part == 0)
		{
			// Everything a chunk owns hangs off one y-sorted holder: entering the tree as one subtree,
			// forgotten with one QueueFree, and sorted with the actors because nested y-sort merges.
			var holder = new Node2D { Name = $"Props_{chunk.Coord.X}_{chunk.Coord.Y}", YSortEnabled = true };
			AddChild(holder);
			chunk.Props.Add(holder);
			return false;
		}

		Node2D parent = (Node2D)chunk.Props[0];

		// Part 1: the small things on the ground, and the landmark if this chunk has one. First,
		// so the decor sets after it can see the landmark's cells and stay out of them.
		if (part == 1)
		{
			FurnishGroundDetail(chunk, parent);
			return false;
		}

		int index = part - 2;

		if (index < decorSets)
		{
			FurnishDecorSet(chunk, parent, rect, decorShare, index);
			return false;
		}

		if (index == decorSets)
		{
			FurnishCuratedProps(chunk, parent, rect, decorShare);
			return false;
		}

		FurnishStageHazards(chunk, parent, rect, chunkPixels);
		return true;
	}

	// --- Ground detail and landmarks (.ai/terrain.md section 8) -----------------------------------

	// Reused for every chunk: GroundDetail fills it, the painter reads it. One chunk at a time.
	private readonly string[] detailBuffer = new string[TerrainStreamer.ChunkTiles * TerrainStreamer.ChunkTiles];

	private void FurnishGroundDetail(TerrainChunk chunk, Node2D parent)
	{
		if (terrainStreamer == null || levelPainter == null)
			return;
		Rect2I? landmark = LandmarkCells(chunk);
		GroundDetail.PlanChunk(terrainStreamer, chunk, terrainKind, terrainSeed, landmark, detailBuffer);
		levelPainter.PaintChunkDetail(chunk.Coord, chunk.MinCell, TerrainStreamer.ChunkTiles, detailBuffer);
		if (landmark is Rect2I cells)
			CreateFlowerPen(chunk, parent, cells);
	}

	// Stages a flower pen belongs on: the green ones. A pen of flowers in the ash of the Emberdeep or
	// on the Frozen Waste would be a joke the world has not earned.
	// Which ones say so on their profile (StageEnvironmentProfile.HasLandmarks): today the Forest, the
	// Swamp, the Ruins, the Cave and the Fallow Fields.
	private bool StageHasLandmarks => StageEnvironmentCatalog.Get(terrainKind).HasLandmarks;

	// About one chunk in four rolls for one; some fail for want of open ground. A chunk is 1536 px,
	// so that is one every few screens of walking - rare enough to be a find.
	private const float LandmarkChance = 0.25f;
	// Never inside the start: the player should walk to one, not spawn beside it.
	private const float LandmarkMinDistance = 1400f;
	// How far the pen keeps from its chunk's border, in cells, so the open-ground test, the fence's
	// navigation cells and the decor margin round it never need another chunk's data.
	private const int LandmarkBorder = 3;
	// Tries per chunk at a spot of open floor. The garden is 12 x 9 cells now, with a clear ring
	// round it, and the Forest's meadows and moss cover enough of a chunk that eight tries found
	// one in under half the chunks that rolled for it.
	private const int LandmarkAttempts = 24;
	private const int SaltLandmark = 0x1A4D;

	/// <summary>
	/// TerrainStreamer.PlanFeatures: the moment a chunk's data exists, decide its landmark and stamp
	/// the fence into its flags, so the navigation grid has the fence in it before anything reads
	/// the chunk - including the window filling from a chunk nobody will ever paint.
	/// </summary>
	private void PlanChunkFeatures(TerrainChunk chunk)
	{
		if (LandmarkCells(chunk) is not Rect2I cells)
			return;
		const int n = TerrainStreamer.ChunkTiles;
		for (int y = 0; y < FlowerPenLayout.CellsH; y++)
		{
			for (int x = 0; x < FlowerPenLayout.CellsW; x++)
			{
				if (!FlowerPenLayout.IsFenceCell(x, y))
					continue;
				int lx = cells.Position.X - chunk.MinCell.X + x;
				int ly = cells.Position.Y - chunk.MinCell.Y + y;
				chunk.Flags[lx + ly * n] |= TerrainStreamer.FlagFence;
			}
		}
	}

	/// <summary>
	/// The cells this chunk's landmark stands on, or null. Decided once per chunk from the stage seed
	/// and the chunk's own terrain, so it is the same on every visit and in every run on that seed.
	/// </summary>
	private Rect2I? LandmarkCells(TerrainChunk chunk)
	{
		if (chunk.LandmarkPlanned)
			return chunk.Landmark;
		chunk.LandmarkPlanned = true;
		chunk.Landmark = null;
		if (!StageHasLandmarks)
			return null;

		var rng = new LevelGenerator.Rng(LevelGenerator.Hash(terrainSeed, chunk.Coord.X, chunk.Coord.Y, SaltLandmark));
		if (rng.NextDouble() >= LandmarkChance)
			return null;

		const int n = TerrainStreamer.ChunkTiles;
		const int w = FlowerPenLayout.CellsW, h = FlowerPenLayout.CellsH;
		int span = n - 2 * LandmarkBorder;
		for (int attempt = 0; attempt < LandmarkAttempts; attempt++)
		{
			int x0 = LandmarkBorder + rng.Next(span - w);
			int y0 = LandmarkBorder + rng.Next(span - h);
			// Open floor under the pen and one cell round it: no water, no blob, nothing to fade into -
			// so a fence never straddles a shore and a gate never opens onto a lake.
			bool open = true;
			for (int y = y0 - 1; y <= y0 + h && open; y++)
				for (int x = x0 - 1; x <= x0 + w && open; x++)
					open = chunk.Terrain[x + y * n] == null;
			if (!open)
				continue;
			var cells = new Rect2I(chunk.MinCell.X + x0, chunk.MinCell.Y + y0, w, h);
			Vector2 centre = stageOrigin + ((Vector2)cells.Position + (Vector2)cells.Size * 0.5f) * ProceduralTilePixels;
			if (centre.DistanceTo(stageOrigin) < LandmarkMinDistance)
				return null;
			chunk.Landmark = cells;
			return cells;
		}
		return null;
	}

	/// <summary>
	/// True if a world position falls inside any landmark, or within <paramref name="marginCells"/>
	/// cells of one, so nothing is scattered into one (and nothing tall right against it).
	/// </summary>
	/// <remarks>Only the position's own chunk is asked: a landmark keeps LandmarkBorder cells from
	/// its chunk's border, so a margin up to that never reaches into a neighbour.</remarks>
	private bool IsInLandmark(Vector2 position, int marginCells = 0)
	{
		if (terrainStreamer == null || !StageHasLandmarks)
			return false;
		Vector2I cell = terrainStreamer.CellOfWorld(position);
		var coord = new Vector2I(
			Mathf.FloorToInt(cell.X / (float)TerrainStreamer.ChunkTiles),
			Mathf.FloorToInt(cell.Y / (float)TerrainStreamer.ChunkTiles));
		Rect2I? cells = LandmarkCells(terrainStreamer.DataFor(coord));
		return cells is Rect2I r && r.Grow(Math.Min(marginCells, LandmarkBorder)).HasPoint(cell);
	}

	private WorldSprites.Pen? penArt;

	private void PreloadLandmarkTextures()
	{
		penArt = WorldSprites.LoadPen();
		bool complete = penArt != null;
		foreach (string part in new[] { "back", "front", "side", "flower", "garden" })
			complete &= penArt != null && penArt.Parts.TryGetValue(part, out GroundedSprite? s) && s.Texture != null;
		if (!complete)
		{
			GD.PushError("Node2DGame: the flower pen's sprites or flower-pen.json are missing; no landmarks will be drawn. "
				+ "Re-run: python tools/art/ground_detail.py, then import.");
			penArt = null;
		}
	}

	/// <summary>
	/// The flower pen: a walled garden, its fence solid and its gates open. Every standing part is
	/// anchored at its own ground line WorldSprites.SortLift below its node, so the chunk holder's
	/// y-sort draws the wizard in front of the fence south of his boots and behind the fence north
	/// of them; the garden is a ground decal under everything. The fence's collision is one body on
	/// the ground's physics layer, shaped by FlowerPenLayout - the same layout that put its cells
	/// in the navigation grid (PlanChunkFeatures) - so the boots, the enemies and the flow field all
	/// meet the fence where it is drawn.
	/// </summary>
	private void CreateFlowerPen(TerrainChunk chunk, Node2D parent, Rect2I cells)
	{
		if (penArt == null)
			return;

		int tile = ProceduralTilePixels;
		Vector2 min = stageOrigin + (Vector2)cells.Position * tile;
		float line = tile * 0.5f;
		Vector2 size = (Vector2)cells.Size * tile;
		float gateX = (FlowerPenLayout.GateX + FlowerPenLayout.GateW * 0.5f) * tile;
		var landmark = new Landmark
		{
			Name = $"FlowerPen_{chunk.Coord.X}_{chunk.Coord.Y}",
			Kind = "flower_pen",
			Key = $"stage{Global.SelectedStageIdx}:flower_pen:{chunk.Coord.X},{chunk.Coord.Y}",
			Bounds = new Rect2(min + new Vector2(line, line), size - new Vector2(tile, tile)),
			Centre = min + size * 0.5f,
			Gates = new[] { min + new Vector2(gateX, line), min + new Vector2(gateX, size.Y - line) },
			YSortEnabled = true,
		};

		// A part standing on the ground at world point (x, groundY); anchorX is the column on x.
		Sprite2D Stand(GroundedSprite part, float x, float groundY)
		{
			var sprite = new Sprite2D
			{
				Texture = part.Texture,
				Centered = false,
				Position = new Vector2(Mathf.Round(x), Mathf.Round(groundY - WorldSprites.SortLift)),
				Offset = new Vector2(-part.AnchorX, WorldSprites.SortLift - part.Ground),
			};
			landmark.AddChild(sprite);
			return sprite;
		}

		GroundedSprite garden = penArt.Parts["garden"];
		landmark.AddChild(new Sprite2D
		{
			Texture = garden.Texture,
			Centered = false,
			Position = min,
			ZIndex = -20, // on the ground, under every actor, like a walkable prop
		});
		Stand(penArt.Parts["back"], min.X, min.Y + line);
		Stand(penArt.Parts["front"], min.X, min.Y + size.Y - line);
		GroundedSprite side = penArt.Parts["side"];
		for (int k = 1; k < cells.Size.Y; k++)
		{
			// one segment per cell of each side, each sorted at its own south end
			float foot = min.Y + line + k * tile;
			Stand(side, min.X + line, foot);
			Stand(side, min.X + size.X - line, foot);
		}
		// Named, so the Fenced Bloom (a secret, FencedBloom.cs) can turn it to face the player.
		Stand(penArt.Parts["flower"], landmark.Centre.X, landmark.Centre.Y + 10f).Name = "Flower";

		var fence = new StaticBody2D
		{
			Name = "Fence",
			Position = min,
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		foreach (Rect2 rail in FlowerPenLayout.RailRects(tile))
		{
			fence.AddChild(new CollisionShape2D
			{
				Position = rail.GetCenter(),
				Shape = new RectangleShape2D { Size = rail.Size },
			});
		}
		landmark.AddChild(fence);
		parent.AddChild(landmark);
		if (TerrainStreamer.LogTimings)
			GD.Print($"TERRAIN landmark {landmark.Key} at {landmark.Centre}");
	}

	private void FurnishDecorSet(TerrainChunk chunk, Node parent, Rect2 rect, float decorShare, int setIndex)
	{
		DecorSetSpec spec = decorChunkSpecs![setIndex];
		var rng = new RandomNumberGenerator
		{
			Seed = LevelGenerator.Hash(terrainSeed, chunk.Coord.X, chunk.Coord.Y, SaltDecor + setIndex),
		};
		List<Vector2> positions = ChunkScatterPositions(rng, rect, spec.Count * decorShare, spec);
		if (positions.Count == 0)
			return;

		// Ghost-like enemies (Boo, wisps, exploders) pass through decor, which Enemy sets up once at
		// spawn against the decor that exists then. Decor streamed in afterwards has to make the same
		// exception from its side, or a wisp alive when a wood loads would snag on it.
		List<Enemy>? ghosts = null;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is Enemy e && e.IgnoresDecorCollision)
				(ghosts ??= new List<Enemy>()).Add(e);
		}

		if (spec.Grounded != null)
		{
			FurnishGroundedSet(parent, spec, rng, positions, ghosts);
			return;
		}

		foreach (Vector2 position in positions)
		{
			// Every roll is drawn before the position is tested, so a rejected spot never shifts the
			// rolls of the ones after it.
			Texture2D texture = spec.Textures[rng.RandiRange(0, spec.Textures.Count - 1)];
			float scale = Mathf.Max(1f, Mathf.Round(rng.RandfRange(spec.MinScale, spec.MaxScale)));
			bool flipH = rng.Randf() < 0.45f;
			bool flipV = spec.AllowYFlip && rng.Randf() < 0.1f;
			float rotation = spec.RandomRotation ? Mathf.DegToRad(rng.RandfRange(-8.0f, 8.0f)) : 0f;
			if (position.DistanceTo(stageOrigin) < DecorPlayerSafeRadius || IsPropAvoidedAt(position) || IsInLandmark(position)
				|| IsInExploreReserve(position) || IsNearPlannedHazard(position))
				continue;
			StaticBody2D body = CreateDecorPropNode(parent, texture, position, scale, flipH, flipV, rotation);
			if (ghosts != null)
			{
				foreach (Enemy ghost in ghosts)
					if (IsInstanceValid(ghost))
						ghost.AddCollisionExceptionWith(body);
			}
		}
	}

	// Trees and bushes for one chunk. Same rules as the sprites above - rolls first, then tests - and
	// two more: a trunk keeps MinSpacing from the others of its set (so a grove is trees, not a
	// fused hedge of trunks the swarm cannot get between), and the whole set keeps clear of a
	// landmark by a margin, a tree by more than a bush.
	private void FurnishGroundedSet(Node parent, DecorSetSpec spec, RandomNumberGenerator rng, List<Vector2> positions, List<Enemy>? ghosts)
	{
		List<GroundedSprite> sprites = spec.Grounded!;
		float spacingSq = spec.MinSpacing * spec.MinSpacing;
		var placed = new List<Vector2>(positions.Count);
		foreach (Vector2 position in positions)
		{
			GroundedSprite art = sprites[rng.RandiRange(0, sprites.Count - 1)];
			bool flipH = rng.Randf() < 0.5f;
			int margin = art.Trunk > 0 ? TreeLandmarkMarginCells : BushLandmarkMarginCells;
			if (position.DistanceTo(stageOrigin) < DecorPlayerSafeRadius || IsPropAvoidedAt(position) || IsInLandmark(position, margin)
				|| IsInExploreReserve(position, art.Trunk > 0 ? ExploreTreeMargin : 0f) || IsNearPlannedHazard(position))
				continue;
			bool crowded = false;
			foreach (Vector2 other in placed)
			{
				if (other.DistanceSquaredTo(position) < spacingSq) { crowded = true; break; }
			}
			if (crowded)
				continue;
			placed.Add(position);

			StaticBody2D? body = CreateGroundedDecor(parent, art, position, flipH);
			if (body != null && ghosts != null)
			{
				foreach (Enemy ghost in ghosts)
					if (IsInstanceValid(ghost))
						ghost.AddCollisionExceptionWith(body);
			}
		}
	}

	private void FurnishCuratedProps(TerrainChunk chunk, Node parent, Rect2 rect, float decorShare)
	{
		if (curatedChunkProps == null || curatedChunkProps.Count == 0 || CuratedPropCount <= 0)
			return;

		var rng = new RandomNumberGenerator
		{
			Seed = LevelGenerator.Hash(terrainSeed, chunk.Coord.X, chunk.Coord.Y, SaltCurated),
		};
		int count = RollCount(rng, CuratedPropCount * decorShare);
		for (int i = 0; i < count; i++)
		{
			var position = new Vector2(rng.RandfRange(rect.Position.X, rect.End.X), rng.RandfRange(rect.Position.Y, rect.End.Y));
			PropEntry entry = curatedChunkProps[rng.RandiRange(0, curatedChunkProps.Count - 1)];
			float scale = rng.RandfRange(CuratedPropMinScale, CuratedPropMaxScale);
			bool flipH = rng.Randf() < 0.5f;
			if (position.DistanceTo(stageOrigin) < DecorPlayerSafeRadius || IsPropAvoidedAt(position) || IsInLandmark(position)
				|| IsInExploreReserve(position) || IsNearPlannedHazard(position))
				continue;
			Texture2D texture = LoadPropTexture(entry.ResPath);
			if (texture == null)
				continue;
			CreateCuratedPropNode(parent, entry, texture, position, scale, flipH);
		}
	}

	private void FurnishStageHazards(TerrainChunk chunk, Node parent, Rect2 rect, float chunkPixels)
	{
		if (hazardChunkMix is not { } mix || StageHazardCount <= 0)
			return;

		foreach (StageHazardSpot spot in HazardPlanFor(chunk))
		{
			// A hazard keeps StageHazardSeparation from every other one, not only from its own chunk's:
			// on the maze they all stand on the same corridor centrelines, and a centreline running
			// across a chunk border would otherwise carry one plate from each side a few pixels apart.
			// The chunk that comes later (row, then column) gives way, to the PLAN of the earlier one -
			// which never shrinks by this rule's own hand, so nothing here is recursive and any two
			// hazards that both survive are at least the separation apart.
			if (ClaimedByEarlierNeighbour(chunk.Coord, spot.Position))
				continue;

			PackedScene? chosen = (spot.Flame ? mix.Flame : mix.Spike) ?? mix.Spike ?? mix.Flame;
			if (chosen == null)
				return;
			var hazard = chosen.Instantiate<Node2D>();
			hazard.Position = spot.Position;
			if (hazard is StageHazard typedHazard)
				// Desynchronised, as BuildStageHazards explains.
				typedHazard.StartPhaseOffset = spot.Phase;
			parent.AddChild(hazard);
		}
	}

	/// <summary>
	/// Where a chunk's stage hazards go: a pure function of (stage seed, chunk) and the ground's data,
	/// worked out once and kept with the chunk's data (TerrainChunk.HazardPlan).
	/// </summary>
	/// <remarks>
	/// The same rolls, in the same order, as the scatter it replaced, so an open stage places exactly
	/// what it did. On the maze each candidate is moved to the middle of its corridor first
	/// (LevelGenerator.MazeCorridorCentre, .ai/terrain.md section 6) and kept only if that is still in
	/// this chunk - a cell centre on a chunk border belongs to one chunk, never to both - and the tests
	/// and the separation are asked of where it ended up. Nothing it reads is placed during the run
	/// (props, enemies, the player), which is what lets the neighbouring chunks and the decor ask it
	/// before this chunk is even painted.
	/// </remarks>
	private List<StageHazardSpot> HazardPlanFor(TerrainChunk chunk)
	{
		if (chunk.HazardPlan != null)
			return chunk.HazardPlan;
		var plan = new List<StageHazardSpot>();
		chunk.HazardPlan = plan;
		if (hazardChunkMix is not { } mix || StageHazardCount <= 0 || terrainStreamer == null)
			return plan;

		float chunkPixels = TerrainStreamer.ChunkTiles * ProceduralTilePixels;
		var rect = new Rect2(stageOrigin + new Vector2(chunk.MinCell.X, chunk.MinCell.Y) * ProceduralTilePixels,
			new Vector2(chunkPixels, chunkPixels));
		bool maze = MazeNavigation.Active is { IsMaze: true };
		var rng = new RandomNumberGenerator
		{
			Seed = LevelGenerator.Hash(terrainSeed, chunk.Coord.X, chunk.Coord.Y, SaltHazards),
		};
		// StageHazardCount used to be spread over a disc of StageHazardSpread round the start.
		float discArea = Mathf.Pi * StageHazardSpread * StageHazardSpread;
		int count = RollCount(rng, StageHazardCount * (chunkPixels * chunkPixels) / Mathf.Max(1f, discArea));
		for (int attempt = 0; attempt < count * 12 && plan.Count < count; attempt++)
		{
			var position = new Vector2(rng.RandfRange(rect.Position.X, rect.End.X), rng.RandfRange(rect.Position.Y, rect.End.Y));
			bool flame = rng.Randf() < mix.FlameChance;
			float phase = rng.Randf() * 3.0f;
			if (maze)
			{
				position = MazeCorridorCentre(position);
				if (!rect.HasPoint(position))
					continue;
			}
			// Not on water, lava or a wall. BuildStageHazards never asked, because the disc it used was
			// small and mostly floor; over an endless stage a spike plate would end up in a lake.
			if (position.DistanceTo(stageOrigin) < StageHazardMinPlayerDistance || IsPropAvoidedAt(position) || IsInLandmark(position)
				|| IsInExploreReserve(position))
				continue;
			bool tooClose = false;
			foreach (StageHazardSpot other in plan)
			{
				if (other.Position.DistanceTo(position) < StageHazardSeparation) { tooClose = true; break; }
			}
			if (tooClose)
				continue;
			plan.Add(new StageHazardSpot(position, flame, phase));
		}
		return plan;
	}

	/// <summary>The middle of the maze corridor a world position is in (LevelGenerator.MazeCorridorCentre).</summary>
	public Vector2 MazeCorridorCentre(Vector2 world) =>
		stageOrigin + LevelGenerator.MazeCorridorCentre(world - stageOrigin, ProceduralTilePixels,
			local => terrainStreamer!.IsWallAtWorld(stageOrigin + local));

	// The four neighbours that come before a chunk in (row, column) order.
	private static readonly Vector2I[] EarlierNeighbours = { new(-1, -1), new(0, -1), new(1, -1), new(-1, 0) };

	private bool ClaimedByEarlierNeighbour(Vector2I coord, Vector2 position)
	{
		foreach (Vector2I offset in EarlierNeighbours)
		{
			foreach (StageHazardSpot other in HazardPlanFor(terrainStreamer!.DataFor(coord + offset)))
			{
				if (other.Position.DistanceTo(position) < StageHazardSeparation)
					return true;
			}
		}
		return false;
	}

	// How far decor and props keep from a planned hazard: the flame vent's marker (50) and half a
	// prop. A crystal standing on a spike plate hid it and could stop the player reaching it.
	private const float HazardPropClearance = 72f;

	/// <summary>
	/// True if a stage hazard is planned within HazardPropClearance of this position, in its chunk or
	/// any chunk round it. Decor and props ask it, so nothing stands on a trap - and since the plans are
	/// pure, a chunk furnished again comes out the same whichever of its neighbours were there first.
	/// </summary>
	private bool IsNearPlannedHazard(Vector2 position)
	{
		if (hazardChunkMix == null || terrainStreamer == null || StageHazardCount <= 0)
			return false;
		// Only the chunks a hazard within the clearance could be in: the position's own, and a neighbour
		// only when the position is that close to the border between them.
		Vector2I first = terrainStreamer.ChunkOfWorld(position - new Vector2(HazardPropClearance, HazardPropClearance));
		Vector2I last = terrainStreamer.ChunkOfWorld(position + new Vector2(HazardPropClearance, HazardPropClearance));
		for (int cy = first.Y; cy <= last.Y; cy++)
		{
			for (int cx = first.X; cx <= last.X; cx++)
			{
				foreach (StageHazardSpot spot in HazardPlanFor(terrainStreamer.DataFor(new Vector2I(cx, cy))))
				{
					if (spot.Position.DistanceSquaredTo(position) < HazardPropClearance * HazardPropClearance)
						return true;
				}
			}
		}
		return false;
	}

	// A fractional expected count, rounded up or down by a roll so the average over many chunks is
	// the expectation itself.
	private static int RollCount(RandomNumberGenerator rng, float expected)
	{
		int whole = Mathf.FloorToInt(expected);
		return whole + (rng.Randf() < expected - whole ? 1 : 0);
	}

	// Where one decor set falls in one chunk: whole clumps, plus the set's share of loners.
	private static List<Vector2> ChunkScatterPositions(RandomNumberGenerator rng, Rect2 rect, float expected, DecorSetSpec spec)
	{
		var result = new List<Vector2>();
		Vector2 RandomIn() => new(rng.RandfRange(rect.Position.X, rect.End.X), rng.RandfRange(rect.Position.Y, rect.End.Y));

		if (!spec.Clustered)
		{
			int n = RollCount(rng, expected);
			for (int i = 0; i < n; i++)
				result.Add(RandomIn());
			return result;
		}

		int targetSize = Mathf.Max(2, spec.ClusterTargetSize);
		int loners = RollCount(rng, expected * spec.ClusterOutlierChance);
		int clumps = RollCount(rng, expected * (1f - spec.ClusterOutlierChance) / targetSize);

		var centres = new List<Vector2>(clumps);
		for (int c = 0; c < clumps; c++)
		{
			Vector2 centre = RandomIn();
			for (int tryIndex = 0; tryIndex < 14; tryIndex++)
			{
				bool separated = true;
				foreach (Vector2 existing in centres)
				{
					if (centre.DistanceTo(existing) < spec.ClusterCenterSeparation) { separated = false; break; }
				}
				if (separated) break;
				centre = RandomIn();
			}
			centres.Add(centre);

			int members = rng.RandiRange(Mathf.Max(1, targetSize - 1), targetSize + 1);
			for (int m = 0; m < members; m++)
			{
				float radius = rng.RandfRange(spec.ClusterRadiusMin, spec.ClusterRadiusMax) * Mathf.Sqrt(rng.Randf());
				float angle = rng.RandfRange(0f, Mathf.Tau);
				result.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
			}
		}

		for (int i = 0; i < loners; i++)
			result.Add(RandomIn());
		return result;
	}

	// --- XP left behind ---------------------------------------------------------------------------
	//
	// The arena used to bound how far away an uncollected orb could be, and the player swept back
	// over most of them. An endless stage does not: walk in a straight line and every orb behind is
	// a live Area2D with a _Process for the rest of the run. So orbs far behind are folded into ONE
	// orb - the first far one found becomes the bank and the rest add their value to it - keeping
	// the XP in the world (walk back for it) without the node count growing with distance walked.
	// Further than any spawn ring or recycle distance, so nothing the player can see is touched.

	[Export] public float XpOrbFoldDistance { get; set; } = 4200f;
	private const float XpOrbSweepIntervalSeconds = 2f;
	private float xpOrbSweepTimer;
	private XPOrb? farXpBank;

	private void SweepFarXpOrbs(float delta)
	{
		if (terrainStreamer == null || player == null || !IsInstanceValid(player))
			return;
		xpOrbSweepTimer += delta;
		if (xpOrbSweepTimer < XpOrbSweepIntervalSeconds)
			return;
		xpOrbSweepTimer = 0f;

		Vector2 p = player.GlobalPosition;
		float foldSq = XpOrbFoldDistance * XpOrbFoldDistance;
		if (farXpBank != null && (!IsInstanceValid(farXpBank) || farXpBank.IsQueuedForDeletion()
			|| farXpBank.GlobalPosition.DistanceSquaredTo(p) < foldSq))
			farXpBank = null;

		foreach (Node node in GetTree().GetNodesInGroup(XPOrb.GroupName))
		{
			if (node is not XPOrb orb || orb == farXpBank || orb.IsQueuedForDeletion())
				continue;
			if (orb.GlobalPosition.DistanceSquaredTo(p) < foldSq)
				continue;
			if (farXpBank == null)
			{
				farXpBank = orb;
				continue;
			}
			farXpBank.Absorb(orb.Value);
			orb.QueueFree();
		}
	}

	private void UpdateDecorParallax()
	{
		// Parallax intentionally disabled.
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (runFinished)
			return;

		// The "pause" action rather than a raw Escape keycode, so Start on a pad opens the menu too.
		// Deliberately its own action and not ui_cancel: ui_cancel also carries B, and B is "back"
		// everywhere else in this game, so reading ui_cancel here would pause the run every time a
		// player tapped back out of habit.
		if (@event.IsActionPressed("pause"))
		{
			ToggleEscapeMenu();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey debugKeyEvent && debugKeyEvent.Pressed && !debugKeyEvent.Echo && debugKeyEvent.Keycode == Key.F3)
		{
			debugOverlayVisible = !debugOverlayVisible;
			if (debugOverlayLabel != null)
				debugOverlayLabel.Visible = debugOverlayVisible;
			GetViewport().SetInputAsHandled();
			return;
		}

		// Skip to the boss. Sitting through thirty minutes to check one fight is not a testing
		// loop anyone will actually run, and the boss is the only part of a stage that can be
		// broken without the rest of the run showing it.
		if (@event is InputEventKey bossKeyEvent && bossKeyEvent.Pressed && !bossKeyEvent.Echo && bossKeyEvent.Keycode == Key.F4)
		{
			if (IsBossRush)
				SkipToNextBossRushArrival();
			else if (!bossPhaseTriggered && TimerVictorySeconds > 0f)
				timeElapsed = TimerVictorySeconds;
			GetViewport().SetInputAsHandled();
		}
	}

	// Every ten seconds on a phone debug build: frame rate against the swarm, read over adb with
	// tools/android-deploy.sh --log. The handheld enemy cap was raised on the strength of these.
	private void TickStressSwarm()
	{
		if (player == null || !IsInstanceValid(player))
			return;
		player.MaxHP = Math.Max(player.MaxHP, 100000);
		player.CurrentHP = player.MaxHP;
		player.XPToNextLevel = int.MaxValue;
		int alive = EnemyRegistry.Count;
		for (int i = 0; i < StressSpawnsPerFrame && alive + i < MaxEnemies; i++)
			SpawnEnemy();
	}

	private void TickStatusAll(float delta)
	{
		statusAllTimer -= delta;
		if (statusAllTimer > 0f)
			return;
		statusAllTimer = 0.5f;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy)
				continue;
			enemy.ApplySlow(0.6f, 1.0f);
			enemy.ApplyPoison(0, 1.0f);
		}
	}

	private void LogHandheldPerformance(float delta)
	{
		if (!OS.IsDebugBuild() || !(OS.HasFeature("mobile") || stressSwarm))
			return;
		perfLogTimer += delta;
		// Every 2 s under stress, so the reading comes in while the swarm is full.
		if (perfLogTimer < (stressSwarm ? 2f : 10f))
			return;
		perfLogTimer = 0f;
		GD.Print($"PERF t={FormatTime(timeElapsed)} fps={Engine.GetFramesPerSecond():0} enemies={EnemyRegistry.Count}/{MaxEnemies}");
		LogNavigationCost();
	}

	// What enemy navigation cost since the last PERF line: the shared field (whole builds, and the
	// worst single slice - what one frame paid) and every enemy's line test and flow read together,
	// per physics frame. Timing the enemies is switched on only while these lines are printed.
	private ulong navPerfPhysicsFrameAtReset;

	private void LogNavigationCost()
	{
		MazeNavigation.MeasureSteering = true;
		ulong physicsFrames = Engine.GetPhysicsFrames() - navPerfPhysicsFrameAtReset;
		if (MazeNavigation.Active != null && navPerfPhysicsFrameAtReset > 0 && physicsFrames > 0)
		{
			int fields = MazeNavigation.PerfFieldsBuilt;
			double steerMsPerFrame = MazeNavigation.PerfSteeringTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / physicsFrames;
			GD.Print($"PERF nav fields={fields} mean={(fields > 0 ? MazeNavigation.PerfFieldMsTotal / fields : 0):0.00}ms"
				+ $" worst={MazeNavigation.PerfFieldMsWorst:0.00}ms worst-slice={MazeNavigation.PerfSliceMsWorst:0.00}ms"
				+ $" steering={steerMsPerFrame * 1000.0:0}us/physics-frame over {MazeNavigation.PerfSteeringCalls / (double)physicsFrames:0} enemies");
		}
		MazeNavigation.ResetPerf();
		navPerfPhysicsFrameAtReset = Engine.GetPhysicsFrames();
	}

	public override void _Process(double delta)
	{
		var perfStart = FrameProfiler.Begin();
		ProcessTick(delta);
		FrameProfiler.End(FrameProfiler.Section.GameProcess, perfStart);
	}

	private void ProcessTick(double delta)
	{
		if (runFinished)
			return;

		if (GetTree().Paused)
			return;

		float d = (float)delta;
		UpdateRunMusic(d);
		if (stressSwarm)
			TickStressSwarm();
		if (statusAllDebug)
			TickStatusAll(d);
		LogHandheldPerformance(d);
		UpdateLowHealthWarning();
		UpdateOnboardingTips(d);
		UpdateDebugOverlay(d);
		fireTimer += d; // Updated fireTimer calculation
		spawnTimer += d;
		// The clock stops the moment the boss arrives. Everything that scales off timeElapsed -
		// spawn rate, enemy health, elite cadence - freezes with it, so the fight is fought at the
		// difficulty the player reached rather than one that keeps climbing underneath them.
		if (!bossFightActive)
			timeElapsed += d;
		spawnBurstWindowTimer += d;
		if (spawnBurstWindowTimer >= SpawnBurstWindowSeconds)
		{
			spawnBurstWindowTimer = 0f;
			spawnCountInBurstWindow = 0;
		}

		if (spawnGraceRemaining > 0f)
			spawnGraceRemaining = Mathf.Max(0f, spawnGraceRemaining - d);

		UpdateRunTimerHud();
		UpdatePlayerHealthHud();

		// No Warden during the boss fight: the arena is meant to drain down to the boss and its own
		// mechanics, not keep handing out set-pieces mid-duel. (Chests no longer come on a clock at
		// all - they are carried by elites and minibosses, DropCarriedChest - and the same rule holds
		// for them there.)
		if (!bossFightActive)
		{
			TickWardenMiniBossSpawn(d);
			TickGhostMiniBoss();
		}

		UpdateSpawnScaling();
		TickSwarmPressure(d);
		TickFormations(d);
		ClampPlayerToStageBounds();
		// After the clamp, deliberately. The whole point of measuring travel rather than reading
		// Velocity is that it must go to zero when the player is pinned against the arena edge.
		// (Only the placeholder room still has an edge; a procedural stage is endless.)
		TrackPlayerTravel(d);
		var terrainStart = FrameProfiler.Begin();
		UpdateTerrainStreaming();
		FrameProfiler.End(FrameProfiler.Section.Terrain, terrainStart);
		UpdateCanopyFade();
		SweepFarXpOrbs(d);
		UpdateHazardDamage(d);
		// Seeded at the feet: they are what water and walls stop (Player.FootPosition), and a player
		// walking north into a lake has his sprite centre over the water when his boots reach it.
		var navStart = FrameProfiler.Begin();
		if (MazeNavigation.Active != null && player != null && IsInstanceValid(player))
			MazeNavigation.Active.Update(player.FootPosition, d);
		FrameProfiler.End(FrameProfiler.Section.Navigation, navStart);
		UpdateBossHud();
		TickBossRush(d);
		if (TickBossPhaseStart())
			return;

		if (fireTimer >= fireInterval)
		{
			// TODO: call fire logic
			fireTimer = 0f;
		}
		if (spawnTimer >= spawnInterval)
		{
			var currentEnemies = EnemyRegistry.Snapshot;
			// Regular waves - elites included, since elites come through SpawnEnemy - stop for the
			// boss. Whatever is already alive is left to be cleared, so the arena drains instead of
			// cutting to an empty field.
			// The trickle stops short of the real cap and leaves the top of the budget for wave
			// formations. Without the reserve the two compete and the trickle always wins, because
			// it spawns constantly and a wave only every fifteen seconds: measured, formations were
			// getting clamped down to three members, which is too few for any shape to survive, so
			// the feature quietly degraded back into the scatter it replaced - and it did so
			// exactly when the arena is densest, which is when a shape matters most.
			int trickleCap = Mathf.Max(1, MaxEnemies - FormationMaxSize);
			if (!bossFightActive && spawnGraceRemaining <= 0f && currentEnemies.Length < trickleCap)
			{
				int availableSlots = trickleCap - currentEnemies.Length;
				int burstCapacity = Math.Max(0, MaxSpawnsPerBurstWindow - spawnCountInBurstWindow);
				int spawnBatch = Math.Min(Math.Min(availableSlots, burstCapacity), GetSpawnBatchCount());
				for (int i = 0; i < spawnBatch; i++)
				{
					SpawnEnemy();
					spawnCountInBurstWindow++;
				}
			}
			NoteTrickleTick(currentEnemies.Length, trickleCap);
			spawnTimer = 0f;
		}

		var background = GetNode<TextureRect>(BackgroundPath);
		var backgroundOverlay = GetNodeOrNull<TextureRect>(BackgroundOverlayPath);
		var camera = player.GetNode<Camera2D>(CameraPath);
		if (background.Material is ShaderMaterial material && camera != null)
		{
			var textureSize = background.Texture.GetSize();
			Vector2 offset = camera.GlobalPosition / textureSize;
			material.SetShaderParameter(ScrollOffsetParam, offset);
			// World-space offset drives the seamless tiled ground. The shader samples in screen
			// pixels, so it also needs the zoom: without it the ground scrolls at 1/zoom the rate
			// of everything standing on it, and every prop appears to slide across the floor.
			material.SetShaderParameter(WorldOffsetParam, camera.GlobalPosition);
			material.SetShaderParameter(CameraZoomParam, camera.Zoom.X);
			if (backgroundOverlay?.Material is ShaderMaterial overlayMaterial)
				overlayMaterial.SetShaderParameter(ScrollOffsetParam, camera.GlobalPosition / textureSize);

			UpdateDecorParallax();
		}
	}

	// No log line per orb any more: it printed on every pickup, and on a phone every print is a
	// write to logcat - dozens a second in a full arena (.ai/performance.md).
	private void OnPlayerXpGained(int amount)
	{
		var xpCounter = GetNode<ProgressBar>("UIOverlay/XPCounter");
		if (xpCounter != null)
		{
			xpCounter.ShowPercentage = false;
			xpCounter.Value = amount; // or player.CurrentXP if you have access, which we do
			xpCounter.MaxValue = player.XPToNextLevel;
		}
	}

	private void UpdateRunTimerHud()
	{
		if (runTimerLabel == null)
			return;

		int totalSeconds = Mathf.FloorToInt(timeElapsed);
		if (totalSeconds == runTimerLastSecond)
			return;

		runTimerLastSecond = totalSeconds;
		runTimerLabel.Text = FormatTime(timeElapsed);
	}

	// ONE RUN MENU AT A TIME (issue #44's timing pass, 2026-10-28). The level-up and chest screens each
	// paused the run on opening and unpaused it on closing, as if each were the only one, and three
	// things went wrong when two arrived in one physics step: a second chest found the first's screen
	// up and was spent giving nothing; a chest closed over a level-up unpaused the run under it, and the
	// level-up screen - which only takes input while paused - sat on screen dead; and a second level-up
	// built a second menu over the first and lost track of it. Now a screen that would open over
	// another waits in a queue, chests first, and the run resumes only when the queue is empty.
	private int queuedChestOpens;
	private int queuedLevelUps;

	private bool IsLevelUpMenuOpen => levelUpMenu != null && IsInstanceValid(levelUpMenu) && levelUpMenu.Visible;
	private bool IsChestMenuOpen => chestSelectionMenu != null && IsInstanceValid(chestSelectionMenu);

	private void ResumeOrOpenNextMenu()
	{
		if (runFinished)
			return;
		if (queuedChestOpens > 0)
		{
			queuedChestOpens--;
			OpenChestSelectionMenu();
			return;
		}
		if (queuedLevelUps > 0)
		{
			queuedLevelUps--;
			OnPlayerLevelGained();
			return;
		}
		GetTree().Paused = false;
	}

	private void OnPlayerLevelGained()
	{
		if (IsLevelUpMenuOpen || IsChestMenuOpen)
		{
			queuedLevelUps++;
			return;
		}
		GameStats.RecordLevelUp();
		spawnGraceRemaining = PostLevelUpSpawnGraceSeconds;
		GD.Print("Player Level Gained!");
		// Show the LevelUpMenu scene
		if (levelupMenuScene != null)
		{
			levelUpMenu = levelupMenuScene.Instantiate<CanvasLayer>();
			levelUpMenu.ProcessMode = ProcessModeEnum.WhenPaused;
			AddChild(levelUpMenu);
			// Connect to WeaponSelected signal
			var menuScript = levelUpMenu as Node;
			menuScript?.Connect("WeaponSelected", new Callable(this, nameof(OnWeaponSelected)));
			menuScript?.Connect("RerollRequested", new Callable(this, nameof(OnRerollRequested)));
			menuScript?.Connect("SkipRequested", new Callable(this, nameof(OnSkipRequested)));
			menuScript?.Connect("BanRequested", new Callable(this, nameof(OnBanRequested)));
			menuScript?.Connect("BankRequested", new Callable(this, nameof(OnBankRequested)));
			menuScript?.Connect("AuguryRequested", new Callable(this, nameof(OnAuguryRequested)));
		}
		if (levelUpMenu != null)
		{
			// Show the menu first
			levelUpMenu.Show();
			rerollsRemainingForCurrentLevelUp = player?.RerollsPerLevelUp ?? 0;

			// A level-up banked earlier is cashed in HERE rather than when it was banked, which is
			// what makes "save until next level up" mean what it says. Spending it now sets up one
			// extra pick, redeemed by CloseLevelUpMenu when this one closes.
			if (player != null && extraPicksPending <= 0 && player.TrySpendBankedLevelUp())
				extraPicksPending++;

			int optionCount = player?.IsPlaytestModeEnabled == true ? 4 : 3;
			// Once per level-up, here and never in ReofferLevelUpOptions: whether upgrades to held
			// spells are in this level-up's pool must survive a reroll or a ban, or a reroll is a
			// way to fish one out.
			player?.BeginLevelUpOffer();
			if (levelUpMenu is LevelUpMenu typedMenu && player != null)
			{
				List<EquippedSpellInfo> equippedInfo = BuildEquippedInfo();
				List<LevelUpOption> ascensionOffers = player.GetAscensionOffers();
				typedMenu.SetOptions(player.GetLevelUpOptions(optionCount), rerollsRemainingForCurrentLevelUp, equippedInfo, BuildBaselineElementCounts(), player.BuildCharges(rerollsRemainingForCurrentLevelUp));
				// The Ascension strip: level 8 and the Apotheosis, offered above the cards (Player.GetAscensionOffers).
				typedMenu.SetAscensions(ascensionOffers);
				// A path met on this level-up is revealed in the browser from now on, in every run
				// (AscensionSightings; harness runs remember nothing).
				AscensionSightings.Record(this, equippedInfo, ascensionOffers, runRevealedAscensions);
			}
			else
			{
				var menuScript = levelUpMenu as Godot.Node;
				var setOptionsMethod = menuScript?.GetType().GetMethod("SetOptions");
				setOptionsMethod?.Invoke(menuScript, null);
			}
			// Pause the game
			GetTree().Paused = true;
		}
	}

	private void OnRerollRequested()
	{
		if (player == null || levelUpMenu is not LevelUpMenu typedMenu)
			return;

		if (rerollsRemainingForCurrentLevelUp <= 0)
			return;

		rerollsRemainingForCurrentLevelUp--;
		GameStats.RecordRerollUsed();
		ReofferLevelUpOptions();
	}

	// One place that re-rolls the cards and refreshes the bar. Used by reroll and by ban - both need
	// a fresh offer, and they must agree about what that means.
	private void ReofferLevelUpOptions()
	{
		if (player == null || levelUpMenu is not LevelUpMenu typedMenu)
			return;

		int optionCount = player.IsPlaytestModeEnabled ? 4 : 3;
		typedMenu.SetOptions(player.GetLevelUpOptions(optionCount), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts(), player.BuildCharges(rerollsRemainingForCurrentLevelUp));
	}

	// A ban does not cost the player their pick: the struck spell is removed from the pool and the
	// offer is rebuilt, so they still choose from a full set of cards this level.
	private void OnBanRequested(string spellId)
	{
		if (player == null || levelUpMenu is not LevelUpMenu)
			return;

		// A refused ban (already struck, or the Marginalia card) still has to put the cards back: the
		// menu left them for the ban picker and only a refresh from here returns to them.
		if (!player.TryBanSpell(spellId))
		{
			if (levelUpMenu is LevelUpMenu refused)
				refused.RefreshCharges(player.BuildCharges(rerollsRemainingForCurrentLevelUp));
			return;
		}

		GameStats.RecordBanUsed();
		ReofferLevelUpOptions();
	}

	private void OnBankRequested()
	{
		if (player == null)
			return;

		// Closing without banking would silently eat the level-up, so a failed bank leaves the menu
		// open and the player still has to choose something.
		if (!player.TryBankLevelUp())
			return;

		GameStats.RecordSaveUsed();
		CloseLevelUpMenu();
	}

	// The augury deliberately does NOT re-roll the current cards - the promise is about the NEXT
	// level-up, and rebuilding the offer here would make it a reroll wearing a different name.
	private void OnAuguryRequested(string spellId)
	{
		if (player == null || levelUpMenu is not LevelUpMenu typedMenu)
			return;

		if (!player.TryAugurSpell(spellId))
			return;

		GameStats.RecordAuguryUsed();
		typedMenu.RefreshCharges(player.BuildCharges(rerollsRemainingForCurrentLevelUp));
	}

	// Projects the player's currently-equipped spells into the lightweight EquippedSpellInfo shape
	// LevelUpMenu needs (the ascension browser + elemental tag section's passive-highlight logic).
	private List<EquippedSpellInfo> BuildEquippedInfo()
	{
		if (player == null)
			return new List<EquippedSpellInfo>();

		return player.GetEquippedSpells()
			.Where(s => s != null)
			.Select(s => new EquippedSpellInfo
			{
				Id = s.Id,
				DisplayName = s.Name,
				CurrentLevel = s.CurrentLevel,
				ElementWeights = s.GetElementWeights().ToDictionary(p => p.Key.ToString(), p => p.Value),
				IsPassive = s.IsPassive,
				Icon = s.Icon,
				Ascensions = player.BuildAscensionPreview(s)
			})
			.ToList();
	}

	// The player's true current element instance counts (issue #16), keyed by Element name, used as
	// the baseline for LevelUpMenu's elemental tag section.
	private Dictionary<string, int> BuildBaselineElementCounts()
	{
		if (player == null)
			return new Dictionary<string, int>();

		return player.GetElementInstanceCounts().ToDictionary(p => p.Key.ToString(), p => p.Value);
	}


	private void OnWeaponSelected(string weaponId)
	{
		if (player == null)
			return;

		// Boons ride the same signal as spells because the choice is the same choice - but they are
		// permanent, never levelled, and take no spell slot, so they take a different path here.
		if (BoonCatalog.IsBoon(weaponId))
		{
			if (!player.TryAddBoon(weaponId))
				GD.PrintErr($"Could not take boon '{weaponId}'.");

			RefreshElementHud();
			CloseLevelUpMenu();
			return;
		}

		// The card a level-up holds when it has nothing else (Player.MarginaliaId): not a spell, so it
		// must not reach TryAddOrLevelSpell, which would refuse it and close the menu having paid nothing.
		if (weaponId == Player.MarginaliaId)
		{
			player.TakeMarginalia();
			CloseLevelUpMenu();
			return;
		}

		int beforeLevel = GetSpellLevel(player, weaponId);

		bool changed = player.TryAddOrLevelSpell(weaponId);
		if (!changed)
		{
			GD.PrintErr($"Could not add or level spell for selection '{weaponId}'.");
		}
		else
		{
			TrackSpellSelectionTelemetry(weaponId, beforeLevel);
		}

		RefreshElementHud();
		CloseLevelUpMenu();
	}

	private void OnSkipRequested()
	{
		GameStats.RecordSkipUsed();
		CloseLevelUpMenu();
	}

	private static int GetSpellLevel(Player owner, string spellId)
	{
		if (owner == null || string.IsNullOrWhiteSpace(spellId))
			return 0;

		string baseSpellId = spellId.Split(':', 2)[0];

		SpellData existing = owner.GetEquippedSpells()
			.FirstOrDefault(s => s != null && s.Id.Equals(baseSpellId, StringComparison.OrdinalIgnoreCase));

		return existing?.CurrentLevel ?? 0;
	}

	private void TrackSpellSelectionTelemetry(string spellId, int beforeLevel)
	{
		if (player == null || string.IsNullOrWhiteSpace(spellId))
			return;

		int afterLevel = GetSpellLevel(player, spellId);
		if (afterLevel <= 0)
			return;

		if (beforeLevel <= 0)
			GameStats.RecordSpellPicked(spellId);
		else if (afterLevel > beforeLevel)
			GameStats.RecordSpellUpgraded(spellId);
	}

	// The pause screen is in the stone register of every other screen over a run - the results
	// screen above all (GameOverScreen), which is where its band, tabs, rows and buttons come from.
	// It used to be its own flat blue-grey sheet with a blue Resume, and was the one menu in the game
	// that looked like nothing else (2026-10-27, "restyle it to look like the menus from the other
	// parts of the game"). Inks are CodexRows.Stone throughout, measured by tools/art/ui_contrast.py.
	private static CodexRows.Palette PauseInk => CodexRows.Stone;
	// The heading band: stone deep, a gold-base rule at its foot (LevelUpMenu.StyleHeading).
	private static readonly Color PauseBandColour = new(0.043f, 0.059f, 0.094f);
	private static readonly Color PauseBandRule = new(0.541f, 0.416f, 0.118f);

	private void EnsureEscapeMenuUi()
	{
		if (escapeMenu != null)
			return;

		escapeMenu = new CanvasLayer
		{
			Name = "EscapeMenu",
			Layer = 100,
			ProcessMode = ProcessModeEnum.Always,
			Visible = false
		};
		AddChild(escapeMenu);

		// Escape, Start or B close it, whichever control is selected (MenuBackListener). Unlike the
		// opening check this may read ui_cancel: inside a menu, back IS the gesture, and there is no
		// run in progress to interrupt. Y opens and closes the spell details on the Run tab - the
		// toggle is a button the stick can reach as well, but it sits at the top of a long list.
		MenuBackListener.Attach(escapeMenu, OnEscapeResumePressed, "pause", "ui_cancel").PadY = () =>
		{
			if (escapeActiveTitle == EscapeRunTitle && escapeSpellDetailBlocks.Count > 0)
				ToggleEscapeSpellDetails();
		};

		escapeRoot = new Control
		{
			Name = "EscapeRoot",
			MouseFilter = Control.MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always
		};
		escapeRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		escapeMenu.AddChild(escapeRoot);

		// Not fully opaque any more. A pause screen that blacks the arena out entirely makes the
		// player lose their read on the fight they are about to return to.
		var dim = new ColorRect
		{
			Color = new Color(0.025f, 0.03f, 0.045f, 0.86f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		escapeRoot.AddChild(dim);

		// The panel used to be a fixed 900x700 laid out as two columns. The viewport is 720x1280
		// portrait, so 180px of it - including the whole right edge of the detail pane - simply
		// hung off the screen. It now fills the screen minus a margin, and stacks vertically.
		var panel = new PanelContainer { ProcessMode = ProcessModeEnum.Always };
		panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		panel.OffsetLeft = EscapePanelMargin;
		panel.OffsetTop = EscapePanelTopMargin;
		panel.OffsetRight = -EscapePanelMargin;
		panel.OffsetBottom = -EscapePanelBottomMargin;
		// On a PC's landscape canvas the full-width sheet stretched every row of stats and every
		// button across 1240 units - a line of text the eye has to travel the whole screen to read.
		// A centred column the width of a book page instead; the phone keeps the full width it needs.
		if (!ResponsiveLayout.IsNarrow(this))
		{
			panel.AnchorLeft = 0.5f;
			panel.AnchorRight = 0.5f;
			panel.OffsetLeft = -EscapePanelMaxHalfWidth;
			panel.OffsetRight = EscapePanelMaxHalfWidth;
		}
		panel.AddThemeStyleboxOverride("panel", BonelightSkin.PanelStyle(BonelightSkin.Register.Stone));
		escapeRoot.AddChild(panel);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		panel.AddChild(root);

		root.AddChild(BuildEscapeHeader());

		// Resume is the only thing most pauses are for, so it is the one large target, the one lit
		// frame, the first selection, and it sits at the top. Restart and Quit are deliberately at
		// the far end of the panel: they used to be stacked directly under Resume in the same size
		// and colour, one misclick from ending a run the player only meant to pause.
		escapeResumeButton = MakeEscapeButton("Resume", OnEscapeResumePressed, BonelightSkin.Emphasis.Primary,
			ResponsiveLayout.TextRole.Title);
		escapeResumeButton.Name = "EscapeResume";
		escapeResumeButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		escapeResumeButton.CustomMinimumSize = new Vector2(0, ResponsiveLayout.IsNarrow(this) ? 68f : 60f);
		root.AddChild(escapeResumeButton);

		// One flat tab strip in place of three titled group panels. The old grouping spent a
		// bordered box and a header label on every two buttons, which is most of what made the
		// menu read as busy. The tab that is open is underlined in gold, so the pane needs no title
		// of its own - it had one, "Run Details", saying again what the tab said.
		// Four across on a PC; two rows of two on a phone, where four Cinzel captions do not fit a
		// quarter of its width each ("Spell Index" is the long one).
		var tabs = new GridContainer { Columns = ResponsiveLayout.IsNarrow(this) ? 2 : 4 };
		tabs.AddThemeConstantOverride("h_separation", 4);
		tabs.AddThemeConstantOverride("v_separation", 4);
		var tabGroup = new ButtonGroup();
		// The catalogue tab is the Spell Index, as on the main menu, and not "Spells": the player read
		// "Spells" as the spells held this run, which is the Run tab's list. This one is every spell
		// in the game and where to find the rest.
		tabs.AddChild(MakeEscapeTabButton(EscapeRunTitle, "Run", ShowEscapeRunOverview, tabGroup));
		tabs.AddChild(MakeEscapeTabButton(CodexRows.SpellIndexTitle, CodexRows.SpellIndexTitle, ShowEscapeSpellbook, tabGroup));
		tabs.AddChild(MakeEscapeTabButton(CodexRows.UnlocksTitle, CodexRows.UnlocksTitle, ShowEscapeAchievements, tabGroup));
		tabs.AddChild(MakeEscapeTabButton("Options", "Options", ShowEscapeOptions, tabGroup));
		root.AddChild(tabs);
		// Down from Resume is the Run tab. Left to geometry it was whichever tab sat under Resume's
		// centre - Spell Index on a PC - which is not the first thing on the line.
		if (escapeTabButtons.TryGetValue(EscapeRunTitle, out Button runTab))
			escapeResumeButton.FocusNeighborBottom = escapeResumeButton.GetPathTo(runTab);

		escapeDetailScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			// The stick scrolls this pane by moving the selection through it: every row on every tab
			// is a focus stop (BuildPauseRow, CodexRows' FocusStop), and the pane follows.
			FollowFocus = true
		};
		root.AddChild(escapeDetailScroll);

		escapeDetailSections = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		escapeDetailSections.AddThemeConstantOverride("separation", 26);
		// Room on the right for the scroll bar, so it never sits on a row.
		escapeDetailScroll.AddChild(CodexRows.WithGutter(escapeDetailSections));

		var exits = new HBoxContainer();
		exits.AddThemeConstantOverride("separation", 8);
		// Report a bug leads the row, in the ordinary frame rather than the exits' quiet one: it
		// leaves the run exactly where it was. A bug is most often noticed mid-run, which is why it
		// is here at all, and the report carries the run with it (BuildBugReportContext).
		escapeReportButton = MakeEscapeButton("Report a bug", OnEscapeReportBugPressed, BonelightSkin.Emphasis.Normal);
		escapeReportButton.Name = "EscapeReportBug";
		escapeReportButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		exits.AddChild(escapeReportButton);
		// Restart and Quit: the quiet frame, so the two irreversible actions do not compete for the
		// eye with the one the player almost always wants.
		foreach (Button exit in new[]
		{
			MakeEscapeButton("Restart Run", OnEscapeRestartPressed, BonelightSkin.Emphasis.Quiet),
			MakeEscapeButton("Quit to Menu", OnEscapeQuitPressed, BonelightSkin.Emphasis.Quiet),
		})
		{
			exit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			exits.AddChild(exit);
		}
		root.AddChild(exits);
	}

	// "Paused" on the results screen's band, with where the run stands beside it: the chapter and
	// the wizard, which the pause screen never said anywhere.
	private Control BuildEscapeHeader()
	{
		bool narrow = ResponsiveLayout.IsNarrow(this);
		var band = new PanelContainer { Name = "EscapeBand", MouseFilter = Control.MouseFilterEnum.Ignore };
		var style = new StyleBoxFlat { BgColor = PauseBandColour, BorderColor = PauseBandRule };
		style.BorderWidthBottom = 2;
		style.SetCornerRadiusAll(2);
		style.ContentMarginLeft = 16;
		style.ContentMarginRight = 16;
		style.ContentMarginTop = 4;
		style.ContentMarginBottom = 6;
		band.AddThemeStyleboxOverride("panel", style);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 18);
		band.AddChild(row);

		var title = new Label
		{
			Text = "Paused",
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(0f, ResponsiveLayout.MinTouchTarget),
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", PauseInk.Heading);
		row.AddChild(title);

		StageDefinition stage = StageCatalog.GetByIndex(Global.SelectedStageIdx);
		CharacterData character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		string chapter = stage == null || string.IsNullOrWhiteSpace(stage.DisplayName)
			? $"Chapter {Global.SelectedStageIdx + 1}"
			: $"Chapter {Global.SelectedStageIdx + 1}  ·  {stage.DisplayName}";
		Label where = CodexRows.MakeText(character == null ? chapter : $"{chapter}\n{character.Name}",
			ResponsiveLayout.TextRole.Label, PauseInk.InkSoft);
		where.Name = "EscapeWhere";
		where.HorizontalAlignment = HorizontalAlignment.Right;
		where.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// A phone has the width for "Paused" and about twenty letters beside it; the line wraps
		// rather than pushing the band wider than the panel.
		where.AutowrapMode = narrow ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
		row.AddChild(where);
		return band;
	}

	// Every button on the pause screen takes focus. They used to refuse it, so that EscapeRoot could
	// keep the focus and hear B through its GuiInput - which left a controller nothing to select at
	// all. Back is heard by MenuBackListener now, whatever is selected.
	private Button MakeEscapeButton(string text, Action pressed, BonelightSkin.Emphasis emphasis,
		ResponsiveLayout.TextRole role = ResponsiveLayout.TextRole.Body)
	{
		var button = new Button
		{
			Text = text,
			FocusMode = Control.FocusModeEnum.All,
			ProcessMode = ProcessModeEnum.Always
		};
		ResponsiveLayout.SetFont(button, role);
		BonelightSkin.StyleButton(button, emphasis);
		ResponsiveLayout.EnsureTouchTarget(button);
		button.Pressed += pressed;
		return button;
	}

	/// <summary>A tab in the pause menu's reference strip.</summary>
	/// <param name="detailTitle">
	/// The title its handler passes to <c>ShowStructuredEscapeDetail</c>, which is what
	/// <see cref="SetEscapeActiveTab"/> matches on. It is keyed on the title rather than on the
	/// button caption because those two used to differ for the achievements tab - the button said
	/// "Achievements" and the pane said "Achievement Progress" - so that tab never highlighted.
	/// </param>
	private Button MakeEscapeTabButton(string detailTitle, string caption, Action pressed, ButtonGroup group)
	{
		var button = new Button
		{
			// Named so _UiShot --press-late=EscapeTabSpellIndex (or Unlocks, Run, Options) can open a tab.
			Name = "EscapeTab" + caption.Replace(" ", string.Empty),
			Text = caption,
			ToggleMode = true,
			ButtonGroup = group,
			FocusMode = Control.FocusModeEnum.All,
			ProcessMode = ProcessModeEnum.Always,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, ResponsiveLayout.MinTouchTarget),
		};
		ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Title);
		CodexRows.StyleTab(button, PauseInk);
		button.Pressed += pressed;
		escapeTabButtons[detailTitle] = button;
		return button;
	}

	private string escapeActiveTitle = string.Empty;

	private void SetEscapeActiveTab(string title)
	{
		escapeActiveTitle = title ?? string.Empty;
		foreach (var pair in escapeTabButtons)
			pair.Value.SetPressedNoSignal(pair.Key.Equals(title, StringComparison.OrdinalIgnoreCase));
	}

	private void ToggleEscapeMenu()
	{
		EnsureEscapeMenuUi();
		if (escapeMenu == null)
			return;

		if (escapeMenu.Visible)
		{
			OnEscapeResumePressed();
			return;
		}

		if (levelUpMenu != null && levelUpMenu.Visible)
			return;

		ShowEscapeRunOverview();
		escapeMenu.Show();

		// Clear of the phone's rounded corners and camera hole; nothing on desktop (SafeArea).
		SafeArea.Apply(escapeMenu);
		// Resume is selected on every open, not only the first: the selection a pause was closed on
		// (a tab, Options' slider) means nothing to the next one. On the CanvasLayer, not on
		// escapeRoot: a Control's IsVisibleInTree stops at the first non-CanvasItem ancestor, so a
		// Control under a hidden CanvasLayer still reports itself visible - and a navigator that
		// believed that would grab focus, and answer the stick, through the whole run. Attached on
		// every open rather than once, so it also picks up the run overview just rebuilt.
		escapeResumeButton?.GrabFocus();
		MenuNavigator.Attach(escapeMenu, escapeResumeButton);
		GetTree().Paused = true;
	}

	private void OnEscapeResumePressed()
	{
		escapeMenu?.Hide();
		GetTree().Paused = false;
	}

	private void OnEscapeReportBugPressed()
	{
		// Over the pause screen, which keeps the run paused underneath it. A sibling of the escape
		// menu rather than its child, so the escape menu's navigator does not count its buttons as
		// its own. The selection goes back to Report a bug on close, where the player left it.
		ReportBugScreen.Open(this, BuildBugReportContext(), () =>
		{
			if (escapeMenu != null && escapeMenu.Visible)
				escapeReportButton?.GrabFocus();
		});
	}

	/// <summary>
	/// The run, as the lines a bug report carries: chapter, wizard, time, level and every spell
	/// held with its level. Shown on the report screen exactly as sent, and nothing more is - no
	/// save, no seed, no log.
	/// </summary>
	private List<string> BuildBugReportContext()
	{
		var lines = new List<string>();
		StageDefinition stage = StageCatalog.GetByIndex(Global.SelectedStageIdx);
		lines.Add($"Chapter: {stage?.DisplayName ?? $"stage {Global.SelectedStageIdx}"}");
		CharacterData character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		lines.Add($"Wizard: {character?.Name ?? $"character {Global.SelectedCharacterIdx}"}");
		lines.Add($"Time in run: {FormatTime(timeElapsed)}");
		lines.Add($"Player level: {player?.CurrentLevel ?? 1}");
		List<SpellData> spells = player?.GetEquippedSpells().Where(s => s != null).ToList() ?? new List<SpellData>();
		lines.Add(spells.Count == 0
			? "Spells: none"
			: "Spells: " + string.Join(", ", spells.Select(s => $"{s.Name} {s.CurrentLevel}/{s.MaxLevel}")));
		return lines;
	}

	private void OnEscapeRestartPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/node_2d_game.tscn");
	}

	private void OnEscapeQuitPressed()
	{
		GetTree().Paused = false;
		// The menu lives on the title screen now, so going "back to the menu" means loading
		// its shell and telling it to skip the press-any-key beat.
		Global.OpenMenuImmediately = true;
		GetTree().ChangeSceneToFile("res://scenes/TitleScreen.tscn");
	}

	private void ShowStructuredEscapeDetail(string title, Action<VBoxContainer> buildContent)
	{
		SetEscapeActiveTab(title);
		if (escapeDetailSections == null)
			return;

		ClearEscapeDetailSections();
		buildContent(escapeDetailSections);
		// A tab opens at its top, not wherever the last one was left.
		if (escapeDetailScroll != null)
			escapeDetailScroll.ScrollVertical = 0;
	}

	private void ClearEscapeDetailSections()
	{
		if (escapeDetailSections == null)
			return;

		foreach (Node child in escapeDetailSections.GetChildren())
		{
			escapeDetailSections.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void ShowEscapeOptions()
	{
		ShowStructuredEscapeDetail("Options", sections =>
		{
			var box = new VBoxContainer();
			box.AddThemeConstantOverride("separation", 10);

			box.AddChild(BuildEscapeVolumeRow("Master Volume", "Master"));
			box.AddChild(BuildEscapeVolumeRow("Music Volume", MusicPlayer.ResolveMusicBusName()));
			box.AddChild(BuildEscapeVolumeRow("Sound Effects Volume", SfxPlayer.ResolveSfxBusName()));

			var muteToggle = new CheckButton { Text = "Mute All Audio", FocusMode = Control.FocusModeEnum.All };
			ResponsiveLayout.SetFont(muteToggle, ResponsiveLayout.TextRole.Body);
			muteToggle.AddThemeColorOverride("font_color", PauseInk.InkSoft);
			muteToggle.AddThemeColorOverride("font_focus_color", PauseInk.Ink);
			muteToggle.AddThemeColorOverride("font_hover_color", PauseInk.Ink);
			muteToggle.AddThemeColorOverride("font_pressed_color", PauseInk.Ink);
			muteToggle.AddThemeColorOverride("font_hover_pressed_color", PauseInk.Ink);
			ResponsiveLayout.EnsureTouchTarget(muteToggle);
			int masterBus = AudioServer.GetBusIndex("Master");
			muteToggle.ButtonPressed = masterBus >= 0 && AudioServer.IsBusMute(masterBus);
			muteToggle.Toggled += pressed => AudioSettings.StoreMute(this, pressed);
			box.AddChild(muteToggle);

			sections.AddChild(BuildEscapeSection("Audio", box));
		});
	}

	private Control BuildEscapeVolumeRow(string label, string busName)
	{
		var row = new VBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		row.AddChild(BuildDetailLabel(label, PauseInk.InkSoft, ResponsiveLayout.TextRole.Label));

		// The stick reaches a slider like a button, and left and right then move it (Godot's own
		// ui_left/ui_right on a focused Range). MenuNavigator counts a focused slider as a
		// selection; it used to take the focus straight back to Resume.
		var slider = new HSlider
		{
			MinValue = 0.0,
			MaxValue = 1.0,
			// Also what one press of the stick moves it by (Godot has no separate keyboard step for a
			// Slider any more): twenty presses end to end rather than a hundred.
			Step = 0.05,
			FocusMode = Control.FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, ResponsiveLayout.MinTouchTarget)
		};
		// The track in the stone rule, the filled part in the bar gold: CodexRows' progress bar.
		StyleBoxFlat Track(Color colour)
		{
			var box = new StyleBoxFlat { BgColor = colour };
			box.SetCornerRadiusAll(3);
			box.ContentMarginTop = 4;
			box.ContentMarginBottom = 4;
			return box;
		}
		slider.AddThemeStyleboxOverride("slider", Track(PauseInk.BarTrack));
		slider.AddThemeStyleboxOverride("grabber_area", Track(PauseInk.BarFill));
		slider.AddThemeStyleboxOverride("grabber_area_highlight", Track(PauseInk.BarFill));
		int busIndex = AudioServer.GetBusIndex(busName);
		slider.Value = busIndex >= 0 ? Mathf.DbToLinear(AudioServer.GetBusVolumeDb(busIndex)) : 1.0;
		// Through AudioSettings so a mid-run volume change survives the run, the same as the
		// identical sliders on the main menu.
		slider.ValueChanged += value => AudioSettings.Store(this, busName, (float)value);
		row.AddChild(slider);
		return row;
	}

	private const string EscapeRunTitle = "Run Details";

	private void ShowEscapeRunOverview()
	{
		escapeSpellDetailBlocks.Clear();
		escapeSpellDetailToggles.Clear();
		ShowStructuredEscapeDetail(EscapeRunTitle, sections =>
		{
			sections.AddChild(BuildRunSummarySection());
			sections.AddChild(BuildCharacterPassiveSection());
			// "Your ... this run", in the Spell Index's own words (Attacks, Passives): this tab is
			// the spells in hand, the Spell Index tab is every spell there is, and the two headings
			// used to be "Offensive Spells" beside a tab called "Spells" - read as the same list.
			sections.AddChild(BuildSpellListSection("Your attacks this run", player?.GetEquippedSpells().Where(s => s != null && !s.IsPassive).ToList() ?? new List<SpellData>()));
			sections.AddChild(BuildSpellListSection("Your passives this run", player?.GetEquippedSpells().Where(s => s != null && s.IsPassive).ToList() ?? new List<SpellData>()));
			sections.AddChild(BuildBoonListSection());
			sections.AddChild(BuildRelicListSection());
			sections.AddChild(BuildElementPassiveSection());
		});
	}

	private Control BuildRunSummarySection()
	{
		// Three tiles abreast on both canvases, the results screen's headline (GameOverScreen). Level
		// and XP are one tile now - the XP is how far through the level, not a fourth number.
		var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 8);
		grid.AddChild(BuildStatTile("duration", FormatTime(timeElapsed), "Time"));
		grid.AddChild(BuildStatTile("xp", $"Level {player?.CurrentLevel ?? 1}", $"{player?.CurrentXP ?? 0}/{player?.XPToNextLevel ?? 0} XP"));
		// How full the book is, and how big: spells are permanent, so this is the number that says
		// how many choices are left, and the Blank Folio's seventh slot has to show up somewhere.
		grid.AddChild(BuildStatTile("todo", $"{player?.GetEquippedSpells().Count ?? 0}/{player?.SpellSlots ?? Player.MaxSpellSlots}", "Spell slots"));
		return BuildEscapeSection("Run Summary", grid);
	}

	private Control BuildCharacterPassiveSection()
	{
		CharacterData character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 4);
		if (character == null)
		{
			list.AddChild(BuildDetailLabel("Apprentice fallback: no character passive loaded.", PauseInk.InkMeta));
			return BuildEscapeSection("Character Passive", list);
		}

		string passiveName = string.IsNullOrWhiteSpace(character.StartingPassiveName) ? "Passive" : character.StartingPassiveName;
		string passiveDescription = string.IsNullOrWhiteSpace(character.StartingPassiveDescription) ? "No passive description." : character.StartingPassiveDescription;
		list.AddChild(BuildDetailLabel($"{character.Name}: {passiveName}", PauseInk.Ink, ResponsiveLayout.TextRole.Body));
		list.AddChild(BuildDetailLabel(passiveDescription, PauseInk.InkSoft));
		return BuildEscapeSection("Character Passive", BuildInfoFrame(list, PauseInk.Face, PauseInk.Edge, focusStop: true));
	}

	private Control BuildSpellListSection(string title, List<SpellData> spells)
	{
		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 10);
		if (spells.Count == 0)
		{
			list.AddChild(BuildDetailLabel("None", PauseInk.InkMeta));
			return BuildEscapeSection(title, list);
		}

		foreach (SpellData spell in spells)
			list.AddChild(BuildSpellRow(spell));

		return BuildEscapeSection(title, list, BuildSpellDetailsToggle());
	}

	// How each spell aims, hits and scales, behind one toggle for the whole tab and closed until
	// asked for: the player reported it as hard to read and not something they needed on every
	// pause. One state for every row (and both spell sections), kept for the run, so a player who
	// wants it open does not reopen it at every pause.
	private bool escapeSpellDetailsShown;
	private readonly List<Control> escapeSpellDetailBlocks = new();
	private readonly List<Button> escapeSpellDetailToggles = new();

	// A tap, a click, or A with the stick on it; on a pad, Y anywhere on the Run tab as well
	// (MenuBackListener's PadY), since the toggle sits at the head of a long list. The results
	// screen's quiet toggle (CodexRows.StyleQuietToggle): a frame would read as a fifth action.
	private Button BuildSpellDetailsToggle()
	{
		var toggle = new Button
		{
			Name = "EscapeSpellDetailsToggle",
			Text = SpellDetailsToggleText(),
			FocusMode = Control.FocusModeEnum.All,
			ProcessMode = ProcessModeEnum.Always,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
		};
		ResponsiveLayout.SetFont(toggle, ResponsiveLayout.TextRole.Label);
		CodexRows.StyleQuietToggle(toggle, PauseInk);
		// Over the 48-unit touch floor; the text alone is a small target.
		ResponsiveLayout.EnsureTouchTarget(toggle, 150f);
		toggle.Pressed += ToggleEscapeSpellDetails;
		escapeSpellDetailToggles.Add(toggle);
		return toggle;
	}

	private string SpellDetailsToggleText() => escapeSpellDetailsShown ? "Hide details" : "Show details";

	private void ToggleEscapeSpellDetails()
	{
		escapeSpellDetailsShown = !escapeSpellDetailsShown;
		foreach (Control block in escapeSpellDetailBlocks)
			if (IsInstanceValid(block))
				block.Visible = escapeSpellDetailsShown;
		foreach (Button toggle in escapeSpellDetailToggles)
			if (IsInstanceValid(toggle))
				toggle.Text = SpellDetailsToggleText();
	}

	// A held spell as the results screen lists one: the icon at its own 64, the name with its level
	// on the right, what it does, then its numbers as glyph-led stat lines and its elements as the
	// chips the level-up card showed - in place of "Damage 12 | Cooldown 1.2s | ..." and "Elements:
	// Fire 1, Arcane 1" set as two more lines of the same small text.
	private Control BuildSpellRow(SpellData spell)
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 14);

		row.AddChild(new TextureRect
		{
			Texture = spell.Icon ?? FallbackSpellHudIcon,
			CustomMinimumSize = new Vector2(64, 64),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});

		var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 6);
		var titleRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		titleRow.AddThemeConstantOverride("separation", 10);
		Label name = BuildDetailLabel(spell.Name, PauseInk.Ink, ResponsiveLayout.TextRole.Body);
		titleRow.AddChild(name);
		Label level = BuildDetailLabel($"Level {spell.CurrentLevel} of {spell.MaxLevel}", PauseInk.InkMeta);
		level.AutowrapMode = TextServer.AutowrapMode.Off;
		level.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		titleRow.AddChild(level);
		box.AddChild(titleRow);
		string description = string.IsNullOrWhiteSpace(spell.Description) ? "No description." : spell.Description;
		box.AddChild(BuildDetailLabel(description, PauseInk.InkSoft));

		var stats = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		stats.AddThemeConstantOverride("h_separation", 22);
		stats.AddThemeConstantOverride("v_separation", 4);
		foreach (string stat in new[]
		{
			$"Damage {spell.GetDamageAtLevel(spell.CurrentLevel)}",
			$"Cooldown {spell.GetCooldownAtLevel(spell.CurrentLevel):0.##}s",
			$"Projectiles {spell.GetProjectileCountAtLevel(spell.CurrentLevel)}",
			$"Range {spell.GetRangeAtLevel(spell.CurrentLevel):0}",
		})
			stats.AddChild(StatIcons.BuildLine(stat, PauseInk.InkSoft, ResponsiveLayout.TextRole.Label, expand: false, wrap: false));
		box.AddChild(stats);
		if (BuildPauseTagChips(spell.GetElementWeights().Select(p => (p.Key.ToString(), p.Value))) is Control chips)
			box.AddChild(chips);
		string attunement = DescribeAttunement(spell);
		if (!string.IsNullOrEmpty(attunement))
			box.AddChild(BuildDetailLabel(attunement, PauseInk.Reward));
		Control details = BuildSpellClassificationBlock(spell);
		details.Visible = escapeSpellDetailsShown;
		escapeSpellDetailBlocks.Add(details);
		box.AddChild(details);
		row.AddChild(box);

		return BuildInfoFrame(row, PauseInk.Face, PauseInk.Edge, focusStop: true);
	}

	// The boons the run has taken. Until this section the pause screen listed every spell and no
	// boon at all, so once picked a boon was never seen again. Each relic is shown through BoonAura,
	// the same light it wore on the level-up card, smaller and quieter (Intensity) so the list still
	// reads as a list.
	private const float PauseBoonAuraIntensity = 0.7f;

	private Control BuildBoonListSection()
	{
		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 6);
		IReadOnlyList<string> owned = player?.GetOwnedBoonIds();
		if (owned == null || owned.Count == 0)
		{
			list.AddChild(BuildDetailLabel("None", PauseInk.InkMeta));
			return BuildEscapeSection("Boons", list);
		}

		foreach (string id in owned)
		{
			BoonDefinition boon = BoonCatalog.GetById(id);
			if (boon == null)
				continue;

			var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			row.AddThemeConstantOverride("separation", 8);
			// One aura cell at x1, so the 64px icon is drawn at exactly its authored 32.
			row.AddChild(new BoonAura
			{
				Icon = BonelightSkin.LoadTextureSafe(boon.IconPath),
				AuraElement = BoonAura.ElementFor(boon),
				Intensity = PauseBoonAuraIntensity,
				CustomMinimumSize = new Vector2(BoonAura.Cell, BoonAura.Cell),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			});

			var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			box.AddThemeConstantOverride("separation", 3);
			box.AddChild(BuildDetailLabel(boon.Name, PauseInk.Ink, ResponsiveLayout.TextRole.Body));
			box.AddChild(BuildDetailLabel(BoonCatalog.DescribeEffect(boon), PauseInk.InkSoft, ResponsiveLayout.TextRole.Label));
			// A stance boon pays only while it holds, so say whether it held when the game paused.
			if (boon.Effect == BoonEffect.StanceRegen && player != null)
				box.AddChild(BuildDetailLabel(player.IsStanceRooted
					? $"Rooted: +{boon.Magnitude:0.0} HP per second now."
					: $"Not rooted - stand still {BoonCatalog.StanceSeconds:0.#}\u00A0s.", PauseInk.InkSoft, ResponsiveLayout.TextRole.Label));
			// The element tags it adds to the run, as the same chips the level-up card showed when it
			// was taken - "why is my Water at 3" was a question the pause screen could not answer.
			if (BuildPauseTagChips(boon.ElementWeights) is Control chips)
				box.AddChild(chips);
			row.AddChild(box);
			list.AddChild(BuildInfoFrame(row, PauseInk.Face, PauseInk.Edge, focusStop: true));
		}

		return BuildEscapeSection("Boons", list);
	}

	// Element chips for the Run tab: CodexRows.BuildChip, so a tap or a click opens the same element
	// card everywhere. Not focusable: the row they sit on is already the stick's stop (BuildInfoFrame),
	// and a row that stopped the stick once for itself and again for each chip would take three
	// presses to pass. "Fire x2" for a double tag.
	private static Control BuildPauseTagChips(IEnumerable<(string Element, int Weight)> tags)
	{
		if (tags == null)
			return null;
		var flow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		flow.AddThemeConstantOverride("h_separation", 6);
		flow.AddThemeConstantOverride("v_separation", 6);
		foreach (var (name, weight) in tags)
		{
			if (!Enum.TryParse(name, true, out Element element))
				continue;
			flow.AddChild(CodexRows.BuildChip(element, weight > 1 ? $"{element} x{weight}" : element.ToString(), false));
		}
		return flow.GetChildCount() > 0 ? flow : null;
	}

	// The relics held this run, and how far along each Full Set Enchantment they belong to is. The
	// pause screen listed spells and boons and no relic at all, so a relic was seen on the chest
	// card that offered it and then only as an icon in the HUD strip.
	private Control BuildRelicListSection()
	{
		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 6);
		IReadOnlyList<string> owned = player?.GetOwnedChestItems();
		if (owned == null || owned.Count == 0)
		{
			list.AddChild(BuildDetailLabel("None yet - open a chest to claim one.", PauseInk.InkMeta));
			return BuildEscapeSection("Relics", list);
		}

		foreach (string id in owned)
		{
			var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			row.AddThemeConstantOverride("separation", 8);
			// The 64-pixel relic art halved to 32, as CodexRows sets icons: a whole step, no smear.
			row.AddChild(new TextureRect
			{
				Texture = BonelightSkin.LoadTextureSafe(ChestItemCatalog.GetIconPath(id)),
				CustomMinimumSize = new Vector2(32, 32),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			});
			var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			box.AddThemeConstantOverride("separation", 3);
			box.AddChild(BuildDetailLabel(ChestItemCatalog.GetDisplayName(id), PauseInk.Ink, ResponsiveLayout.TextRole.Body));
			box.AddChild(BuildDetailLabel(ChestItemCatalog.GetDescription(id), PauseInk.InkSoft, ResponsiveLayout.TextRole.Label));
			if (BuildPauseTagChips(ChestItemCatalog.GetElementTags(id)) is Control chips)
				box.AddChild(chips);
			row.AddChild(box);
			list.AddChild(BuildInfoFrame(row, PauseInk.Face, PauseInk.Edge, focusStop: true));
		}

		// One line per set the run has started, not one per relic: two pieces of the same set would
		// otherwise say the same thing twice. Complete sets first, then the closest to done.
		var sets = ChestItemCatalog.Sets
			.Select(set => (Set: set, Progress: ChestItemCatalog.GetSetProgress(set, owned.ToList())))
			.Where(s => s.Progress.OwnedCount > 0)
			.OrderByDescending(s => s.Progress.OwnedCount >= s.Progress.TotalRequired)
			.ThenByDescending(s => s.Progress.TotalRequired == 0 ? 0f : s.Progress.OwnedCount / (float)s.Progress.TotalRequired)
			.ToList();
		if (sets.Count > 0)
		{
			list.AddChild(BuildGroupLabel("Full Set Enchantments"));
			foreach (var (set, (have, need)) in sets)
			{
				bool complete = need > 0 && have >= need;
				var line = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
				line.AddThemeConstantOverride("separation", 8);
				line.AddChild(new TextureRect
				{
					Texture = BonelightSkin.LoadTextureSafe(set.IconPath),
					CustomMinimumSize = new Vector2(32, 32),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				});
				// Gold once it is whole, the same gold as an attunement that is paying; the plain
				// ink while it is still being gathered.
				string text = complete ? $"{set.Name}  {have}/{need} - complete" : $"{set.Name}  {have}/{need}";
				line.AddChild(BuildDetailLabel(text, complete ? PauseInk.Reward : PauseInk.InkSoft,
					ResponsiveLayout.TextRole.Label));
				list.AddChild(line);
				// The one set whose bonus moves while you play: what the Brazier is paying at the
				// moment the game was paused (the pause freezes the heat, so this is exact).
				if (complete && set.Id == ChestItemCatalog.EmberlineSetId && player != null && player.IsBrazierLit)
					list.AddChild(BuildDetailLabel(DescribeBrazierHeat(player), PauseInk.InkSoft, ResponsiveLayout.TextRole.Label));
			}
		}

		return BuildEscapeSection("Relics", list);
	}

	// "Heat 62%: +19% spell damage, +9% spell area (full: +30%, +15%)". The numbers are the ones
	// GetChestDamageBonusPercent and GetEffectiveAreaMultiplier are reading at this moment.
	private static string DescribeBrazierHeat(Player holder)
	{
		int damageFull = Mathf.RoundToInt(Player.BrazierDamageAtFullHeat * 100f);
		int areaFull = Mathf.RoundToInt(Player.BrazierAreaAtFullHeat * 100f);
		if (holder.BrazierHeat >= 0.999f)
			return $"Heat full: +{damageFull}% spell damage, +{areaFull}% spell area. Moving cools it.";
		if (holder.BrazierHeat <= 0.001f)
			return $"Cold. Stand still to heat it: up to +{damageFull}% spell damage, +{areaFull}% spell area.";
		return $"Heat {Mathf.RoundToInt(holder.BrazierHeat * 100f)}%: +{Mathf.RoundToInt(holder.BrazierDamageBonus * 100f)}% spell damage,"
			+ $" +{Mathf.RoundToInt(holder.BrazierAreaBonus * 100f)}% spell area (full: +{damageFull}%, +{areaFull}%).";
	}

	private Control BuildElementPassiveSection()
	{
		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 8);
		var counts = player?.GetElementInstanceCounts() ?? new Dictionary<Element, int>();
		var entries = Enum.GetValues<Element>()
			.Select(element => (Element: element, Count: counts.TryGetValue(element, out int count) ? count : 0, Tier: player?.GetElementTier(element) ?? 0))
			.OrderByDescending(entry => entry.Tier)
			.ThenByDescending(entry => entry.Count)
			.ThenBy(entry => entry.Element.ToString())
			.ToList();

		var activeEntries = entries.Where(entry => entry.Tier > 0).ToList();
		var inactiveEntries = entries.Where(entry => entry.Tier <= 0 && entry.Count > 0).ToList();
		list.AddChild(BuildGroupLabel("Active"));
		if (activeEntries.Count == 0)
			list.AddChild(BuildDetailLabel("No active element passives yet", PauseInk.InkMeta));
		else
			foreach (var entry in activeEntries)
				list.AddChild(BuildElementPassiveRow(entry.Element, entry.Count, entry.Tier));

		list.AddChild(BuildGroupLabel("Inactive"));
		foreach (var entry in inactiveEntries)
			list.AddChild(BuildElementPassiveRow(entry.Element, entry.Count, entry.Tier));

		return BuildEscapeSection("Element Passives", list);
	}

	private Control BuildElementPassiveRow(Element element, int count, int tier)
	{
		bool active = tier > 0;
		Color elementColor = ElementColors.GetColor(element);
		Color background = active ? new Color(elementColor.R, elementColor.G, elementColor.B, 0.88f) : PauseInk.Face;
		Color border = active ? new Color(1.0f, 1.0f, 1.0f, 0.28f) : PauseInk.QuietEdge;
		// Each ink is picked against the colour as it is actually drawn: the row is the element at
		// 0.88 over the stone panel, and the badge the element again at 0.96 over the row.
		// Picking both against the bare element put dark ink on the darker Arcane row at 3.9:1.
		Color rowDrawn = SectionGround.Lerp(elementColor, 0.88f);
		Color textColor = active ? GetReadableTextColor(rowDrawn) : PauseInk.InkMeta;
		Color badgeTextColor = active ? GetReadableTextColor(rowDrawn.Lerp(elementColor, 0.96f)) : textColor;
		string status = active ? $"Tier {tier}" : "Inactive";
		string effect = active ? ElementPassiveDescriptions.GetEffectText(element, tier) : ElementPassiveDescriptions.GetEffectText(element, 0);

		var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(BuildElementNameBadge(element, count, active, elementColor, badgeTextColor));

		var textBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		textBox.AddThemeConstantOverride("separation", 2);
		textBox.AddChild(BuildDetailLabel(status, textColor, ResponsiveLayout.TextRole.Label));
		textBox.AddChild(BuildDetailLabel(effect, textColor, ResponsiveLayout.TextRole.Label));
		row.AddChild(textBox);

		return BuildInfoFrame(row, background, border, focusStop: true);
	}

	private Control BuildElementNameBadge(Element element, int count, bool active, Color elementColor, Color textColor)
	{
		// An inactive badge is a well of stone deep: the meta ink on it is 7.8:1, where on the
		// lighter stone edge it fell under 4.5.
		Color badgeBackground = active ? new Color(elementColor.R, elementColor.G, elementColor.B, 0.96f) : PauseBandColour;
		Color badgeBorder = active ? new Color(1.0f, 1.0f, 1.0f, 0.36f) : PauseInk.Edge;
		var box = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(118, 0),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		box.AddThemeConstantOverride("separation", 0);
		var name = BuildDetailLabel(element.ToString(), textColor, ResponsiveLayout.TextRole.Label);
		name.HorizontalAlignment = HorizontalAlignment.Center;
		var progress = BuildDetailLabel(ElementPassiveDescriptions.GetProgressLabel(count), textColor, ResponsiveLayout.TextRole.Label);
		progress.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(name);
		box.AddChild(progress);
		// Its own width, not half the row: every frame expands by default, and the badge and the
		// effect beside it used to split the row evenly between them.
		Control badge = BuildInfoFrame(box, badgeBackground, badgeBorder);
		badge.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		return badge;
	}

	// A group inside a section ("Active", "Full Set Enchantments"): the results screen's group label,
	// the heading gold at the Body role.
	private Label BuildGroupLabel(string text)
	{
		Label label = CodexRows.MakeText(text, ResponsiveLayout.TextRole.Body, PauseInk.Heading);
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		return label;
	}

	// What an element row's translucent colour is laid over: the stone panel, ui-panel.png at its
	// centre, now that a section draws no fill of its own.
	private static readonly Color SectionGround = new Color(0.078f, 0.102f, 0.149f);

	// A section of the pause pane in the CodexRows manner: a Cinzel heading in the heading gold,
	// anything trailing on its right (the spell sections' Show details), a rule, then the content.
	// No box round it any more: the rows inside are the boxes, and a box round boxes is most of what
	// made the old sheet read as busy.
	private Control BuildEscapeSection(string title, Control content, Control trailing = null)
	{
		var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 10);

		var head = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeConstantOverride("separation", 12);
		Label heading = CodexRows.MakeText(title, ResponsiveLayout.TextRole.Title, PauseInk.Heading);
		heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		heading.VerticalAlignment = VerticalAlignment.Bottom;
		head.AddChild(heading);
		if (trailing != null)
		{
			trailing.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			head.AddChild(trailing);
		}
		box.AddChild(head);
		box.AddChild(new ColorRect { Color = PauseInk.Rule, CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore });
		box.AddChild(content);
		return box;
	}

	// A headline number as the results screen sets one (GameOverScreen.BuildHeadlineTile): the
	// stat's glyph, the number in Cinzel, what it is in the meta ink beneath.
	private Control BuildStatTile(string glyph, string value, string label)
	{
		bool narrow = ResponsiveLayout.IsNarrow(this);
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 10);
		// The 12-pixel glyph at a whole multiple: x2 on a phone, where three tiles share the width,
		// x3 on a PC.
		float glyphSize = narrow ? 24f : 36f;
		line.AddChild(new TextureRect
		{
			Texture = StatIcons.Load(glyph),
			CustomMinimumSize = new Vector2(glyphSize, glyphSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		text.AddThemeConstantOverride("separation", 0);
		Label number = CodexRows.MakeText(value, ResponsiveLayout.TextRole.Title, PauseInk.Ink);
		number.AutowrapMode = TextServer.AutowrapMode.Off;
		text.AddChild(number);
		text.AddChild(CodexRows.MakeText(label, ResponsiveLayout.TextRole.Label, PauseInk.InkMeta));
		line.AddChild(text);
		return BuildInfoFrame(line, PauseInk.Face, PauseInk.Edge);
	}

	/// <summary>A row of the pause pane: a face, an edge, padding - and, if asked, a focus stop.</summary>
	/// <remarks>
	/// CodexRows' row arrangement (and GameOverScreen.Row's): the padding is an inner MarginContainer,
	/// so the focus stop - an invisible overlay laid first - covers the whole row and the selection
	/// ring sits on its edge. The stop is how a controller scrolls the Run tab at all: a pad moves
	/// a selection, not a scroll bar, and the pane follows the selection (FollowFocus). It ignores
	/// the mouse - to a pointer the row is text, and a press that starts on it must be free to
	/// become a drag of the list.
	/// </remarks>
	private static Control BuildInfoFrame(Control content, Color background, Color border, bool focusStop = false)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(0);
		panel.AddThemeStyleboxOverride("panel", style);
		if (focusStop)
		{
			Button overlay = BonelightSkin.MakeCardClickOverlay(true);
			overlay.Name = "RowFocus";
			overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
			overlay.ProcessMode = ProcessModeEnum.Always;
			panel.AddChild(overlay);
		}
		var padding = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		padding.AddThemeConstantOverride("margin_left", 12);
		padding.AddThemeConstantOverride("margin_right", 12);
		padding.AddThemeConstantOverride("margin_top", 10);
		padding.AddThemeConstantOverride("margin_bottom", 10);
		padding.AddChild(content);
		panel.AddChild(padding);
		return panel;
	}

	// Label, not Micro, by default and at every call in the pause screen. Micro is the floor of the
	// type scale, for numerals on chips; the whole pause screen - run summary, spell details,
	// element passives, the codex - was set in it, and it read as fine print. Every ink is one of
	// CodexRows.Stone's now, each measured by tools/art/ui_contrast.py on the ground it lands on.
	private static Label BuildDetailLabel(string text, Color color,
		ResponsiveLayout.TextRole role = ResponsiveLayout.TextRole.Label)
	{
		Label label = CodexRows.MakeText(text, role, color);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return label;
	}

	// The Spell Index and Unlocks tabs are the main menu's two lists in the stone register, built by
	// the same CodexRows so the pause screen cannot word them a third way. They used to: every spell
	// carried a "Classification: Targeting NearestEnemy | Shape ProjectileHit | Scaling ..." line in
	// the engine's own enum names and no description at all, and every row was a box in a box.
	private void ShowEscapeSpellbook()
	{
		SaveData data = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		ShowStructuredEscapeDetail(CodexRows.SpellIndexTitle, sections =>
		{
			var attacks = new List<Control>();
			var passives = new List<Control>();
			var missing = new List<(string Id, Texture2D Icon, bool Passive)>();
			int attackTotal = 0;

			foreach (var (id, path) in SpellbookResources)
			{
				SpellData spell = ResourceLoader.Load<SpellData>(path);
				if (spell == null)
					continue;
				attackTotal++;
				var elements = CodexRows.FromWeights(spell.GetElementWeights());
				if (GlobalStatsManager.IsSpellUnlockedForLevelUp(data, id))
					attacks.Add(BuildPauseSpellRow(id, spell.Name, spell.Description, spell.Icon, elements, data));
				else
					missing.Add((id, spell.Icon, false));
			}

			foreach (var passive in PassiveSpellbookEntries)
			{
				var elements = CodexRows.ParseElements(passive.Elements);
				if (GlobalStatsManager.IsSpellUnlockedForLevelUp(data, passive.Id))
					passives.Add(BuildPauseSpellRow(passive.Id, passive.DisplayName, passive.Description, null, elements, data));
				else
					missing.Add((passive.Id, null, true));
			}

			int columns = CodexRows.ColumnsFor(this);
			var list = CodexRows.BuildList();
			// The subtitle leads, so the tab says what it is before it says how much of it there is:
			// every spell in the game, not the ones in hand (those are on the Run tab).
			list.AddChild(CodexRows.BuildSummary(
				CodexRows.SpellIndexSubtitle + "\n"
				+ $"{attacks.Count} of {attackTotal} attacks and {passives.Count} of {PassiveSpellbookEntries.Length} passives can be offered at level-up.",
				CodexRows.Stone));
			if (attacks.Count > 0)
				CodexRows.AddSection(list, "Attacks", $"{attacks.Count} found", CodexRows.Stone, columns, attacks);
			if (passives.Count > 0)
				CodexRows.AddSection(list, "Passives", $"{passives.Count} found", CodexRows.Stone, columns, passives);
			if (missing.Count > 0)
				CodexRows.AddSection(list, "Still to find", $"{missing.Count} spells", CodexRows.Stone, columns,
					CodexRows.GroupMissing(missing, m => m.Passive, m => UnlockCatalog.GetLockedHint(m.Id, UnlockKind.Spell))
						.Select(g => BuildPauseMissingRow(g.First.Id, g.First.Icon, g.First.Passive, g.Count)));
			sections.AddChild(list);
		});
	}

	private Control BuildPauseSpellRow(string id, string name, string description, Texture2D icon,
		List<(Element Element, int Weight)> elements, SaveData data)
	{
		bool removed = GlobalStatsManager.IsSpellRemovedFromPool(data, id);
		return CodexRows.BuildRow(new CodexRows.Entry
		{
			Title = name,
			Body = string.IsNullOrWhiteSpace(description) ? string.Empty : description,
			Icon = icon ?? FallbackSpellHudIcon,
			Elements = elements,
			ChipNote = elements.Count == 1 ? "Attuned" : string.Empty,
			// Curated out on the main menu: said here, because "why is it never offered" is
			// exactly the question a player pauses to answer.
			Tag = removed ? "Set aside" : string.Empty,
			Quiet = removed,
			// A stop for the stick, as on the main menu: the pad scrolls the list by moving the
			// selection through it, and B still closes the pause from any row (MenuBackListener).
			FocusStop = true,
		}, CodexRows.Stone);
	}

	private Control BuildPauseMissingRow(string id, Texture2D icon, bool passive, int count)
	{
		return CodexRows.BuildRow(new CodexRows.Entry
		{
			Title = CodexRows.UnknownTitle(passive, count),
			Icon = icon ?? FallbackSpellHudIcon,
			IconSilhouette = true,
			Meta = UnlockCatalog.GetLockedHint(id, UnlockKind.Spell),
			MetaGlyph = StatIcons.Load("lock"),
			Quiet = true,
			// A stop for the stick, as on the main menu: the pad scrolls the list by moving the
			// selection through it, and B still closes the pause from any row (MenuBackListener).
			FocusStop = true,
		}, CodexRows.Stone);
	}

	private void ShowEscapeAchievements()
	{
		SaveData data = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		ShowStructuredEscapeDetail(CodexRows.UnlocksTitle, sections =>
		{
			AchievementContext context = AchievementContext.ForSave(data);
			var open = new List<(AchievementDefinition, AchievementProgress)>();
			var done = new List<AchievementDefinition>();
			foreach (AchievementDefinition deed in AchievementDefinitions.Listed)
			{
				if (GlobalStatsManager.IsAchievementUnlocked(data, deed.Id))
					done.Add(deed);
				else
					open.Add((deed, deed.GetProgress(context)));
			}

			Texture2D SpellIcon(string spellId)
			{
				foreach (var (id, path) in SpellbookResources)
					if (id.Equals(spellId, StringComparison.OrdinalIgnoreCase))
						return ResourceLoader.Load<SpellData>(path)?.Icon;
				return null;
			}

			int total = open.Count + done.Count;
			int columns = CodexRows.ColumnsFor(this);
			var list = CodexRows.BuildList();
			list.AddChild(CodexRows.BuildSummary(CodexRows.UnlocksSubtitle + "\n" + CodexRows.UnlocksSummary(done.Count, total),
				CodexRows.Stone, total > 0 ? done.Count / (float)total : 0f));
			// In progress first: the player is mid-run and wants to know what this attempt could finish.
			if (open.Count > 0)
				CodexRows.AddSection(list, "In progress", $"{open.Count} to go", CodexRows.Stone, columns,
					CodexRows.OrderInProgress(open).Select(p => (Control)CodexRows.BuildDeedRow(p.Deed, false, p.Progress, SpellIcon, CodexRows.Stone, focusStop: true)));
			if (done.Count > 0)
				CodexRows.AddSection(list, "Done", $"{done.Count}", CodexRows.Stone, columns,
					done.Select(d => (Control)CodexRows.BuildDeedRow(d, true, null, SpellIcon, CodexRows.Stone, focusStop: true)));
			sections.AddChild(list);
		});
	}

	private void AppendCharacterPassive(StringBuilder builder)
	{
		CharacterData character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		builder.AppendLine("Character Passive");
		if (character == null)
		{
			builder.AppendLine("  Apprentice fallback: no character passive loaded.");
			return;
		}

		string passiveName = string.IsNullOrWhiteSpace(character.StartingPassiveName) ? "Passive" : character.StartingPassiveName;
		string passiveDescription = string.IsNullOrWhiteSpace(character.StartingPassiveDescription) ? "No passive description." : character.StartingPassiveDescription;
		builder.AppendLine($"  {character.Name}: {passiveName}");
		builder.AppendLine($"  {passiveDescription}");
	}

	private void AppendEquippedWeapons(StringBuilder builder)
	{
		builder.AppendLine("Weapons");
		var weapons = player?.GetEquippedSpells().Where(s => s != null && !s.IsPassive).ToList() ?? new List<SpellData>();
		if (weapons.Count == 0)
		{
			builder.AppendLine("  None");
			return;
		}

		foreach (SpellData spell in weapons)
			builder.AppendLine($"  {FormatSpellStats(spell)}");
	}

	private void AppendEquippedPassives(StringBuilder builder)
	{
		builder.AppendLine("Equipped Passives");
		var passives = player?.GetEquippedSpells().Where(s => s != null && s.IsPassive).ToList() ?? new List<SpellData>();
		if (passives.Count == 0)
		{
			builder.AppendLine("  None");
			return;
		}

		foreach (SpellData spell in passives)
			builder.AppendLine($"  {FormatSpellStats(spell)}");
	}

	private void AppendElementPassives(StringBuilder builder)
	{
		builder.AppendLine("Element Passives");
		if (player == null)
		{
			builder.AppendLine("  None");
			return;
		}

		foreach (var pair in player.GetElementInstanceCounts().OrderByDescending(p => p.Value).ThenBy(p => p.Key.ToString()))
		{
			int tier = player.GetElementTier(pair.Key);
			string status = tier > 0 ? $"Tier {tier}: {ElementPassiveDescriptions.GetEffectText(pair.Key, tier)}" : ElementPassiveDescriptions.GetEffectText(pair.Key, tier);
			builder.AppendLine($"  {pair.Key} {pair.Value}/6 - {status}");
		}
	}

	private string FormatSpellStats(SpellData spell)
	{
		return $"{spell.Name} Lv {spell.CurrentLevel}/{spell.MaxLevel} | Damage {spell.GetDamageAtLevel(spell.CurrentLevel)} | Cooldown {spell.GetCooldownAtLevel(spell.CurrentLevel):0.##}s | Projectiles {spell.GetProjectileCountAtLevel(spell.CurrentLevel)} | Range {spell.GetRangeAtLevel(spell.CurrentLevel):0} | {FormatElementWeights(spell.GetElementWeights())}";
	}

	private static string FormatElementWeights(Dictionary<Element, int> weights)
	{
		if (weights == null || weights.Count == 0)
			return "No elements";

		string tags = string.Join(", ", weights.OrderBy(p => p.Key.ToString()).Select(p => p.Value > 1 ? $"{p.Key} x{p.Value}" : p.Key.ToString()));

		// A spell naming exactly one element is attuned to it and scales with that element's tier.
		// Worth saying on every card: it is the reason a single-element spell is not simply worse
		// than a hybrid that hands you twice the element weight.
		return weights.Count == 1 ? $"{tags} - Attuned" : tags;
	}

	// The live value of a spell's attunement, so the pause screen answers "is going deeper on Fire
	// actually doing anything for my Fireball yet" without the player working it out from the table.
	private string DescribeAttunement(SpellData spell)
	{
		Element? attuned = Player.GetAttunedElement(spell);
		if (attuned == null || player == null)
			return string.Empty;

		int count = player.GetElementInstanceCounts().TryGetValue(attuned.Value, out int value) ? value : 0;
		float multiplier = player.GetAttunementMultiplier(spell);
		if (multiplier <= 1.0f)
			return $"Attuned to {attuned.Value} ({count}/2) - no bonus until {attuned.Value} reaches 2";

		return $"Attuned to {attuned.Value} ({count}) - +{(multiplier - 1f) * 100f:0}% damage";
	}

	// Three sentences in place of "Classification: Targeting NearestEnemy | Shape ProjectileHit |
	// Scaling Damage, Cooldown, ProjectileSpeed, Pierce, Crit" - the engine's enum names, which the
	// player reported as hard to read. The words are SpellClassificationText's, one phrase per
	// enum value (RegressionChecks.ValidateSpellClassificationText).
	private Control BuildSpellClassificationBlock(SpellData spell)
	{
		var block = new VBoxContainer { Name = "SpellDetails", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		block.AddThemeConstantOverride("separation", 2);
		Color ink = PauseInk.InkMeta;
		string aim = SpellClassificationText.Aim(ResolveTargetingMode(spell)) ?? ResolveTargetingMode(spell).ToString();
		string shape = SpellClassificationText.Shape(ResolveDamageShape(spell)) ?? ResolveDamageShape(spell).ToString();
		block.AddChild(BuildDetailLabel($"Aims {aim}.", ink, ResponsiveLayout.TextRole.Label));
		block.AddChild(BuildDetailLabel($"Hits with {shape}.", ink, ResponsiveLayout.TextRole.Label));
		block.AddChild(BuildDetailLabel($"Improved by {SpellClassificationText.ScalingList(ResolveScalingTags(spell))}.", ink, ResponsiveLayout.TextRole.Label));
		return block;
	}

	private static SpellTargetingMode ResolveTargetingMode(SpellData spell)
	{
		if (spell.TargetingMode != SpellTargetingMode.Auto)
			return spell.TargetingMode;

		return spell.Id?.ToLowerInvariant() switch
		{
			"arcane_explosion" => SpellTargetingMode.Self,
			"spiritual_weapon" => SpellTargetingMode.Self,
			"solar_flare" => SpellTargetingMode.Self,
			"cyclone_slash" => SpellTargetingMode.Self,
			"black_tentacles" or "toxic_spore_burst" => SpellTargetingMode.GroundAtEnemy,
			"obsidian_spike" => SpellTargetingMode.GroundAtEnemy,
			"glacial_spike" => SpellTargetingMode.GroundAtEnemy,
			"meteor_swarm" => SpellTargetingMode.GroundAtEnemy,
			"scorching_ray" => SpellTargetingMode.MultiTarget,
			"chain_lightning" => SpellTargetingMode.MultiTarget,
			"cone_of_cold" or "cinderbreath" => SpellTargetingMode.DirectionalCone,
			"frozen_bulwark" or "guardian_vines" or "venom_cloak" or "tidal_barrier" => SpellTargetingMode.Self,
			_ => SpellTargetingMode.NearestEnemy
		};
	}

	private static SpellDamageShape ResolveDamageShape(SpellData spell)
	{
		if (spell.DamageShape != SpellDamageShape.Auto)
			return spell.DamageShape;

		return spell.Id?.ToLowerInvariant() switch
		{
			"arcane_explosion" => SpellDamageShape.RadiusBurst,
			"fireball" => SpellDamageShape.RadiusBurst,
			"cinderbreath" => SpellDamageShape.PersistentZone,
			"obsidian_spike" => SpellDamageShape.RadiusBurst,
			"glacial_spike" => SpellDamageShape.RadiusBurst,
			"meteor_swarm" or "toxic_spore_burst" => SpellDamageShape.RadiusBurst,
			"cone_of_cold" => SpellDamageShape.RadiusBurst,
			"black_tentacles" => SpellDamageShape.PersistentZone,
			"solar_flare" => SpellDamageShape.PersistentZone,
			"frozen_bulwark" or "guardian_vines" or "venom_cloak" or "tidal_barrier" => SpellDamageShape.PersistentZone,
			"spiritual_weapon" => SpellDamageShape.ContactOrbit,
			"cyclone_slash" => SpellDamageShape.ContactOrbit,
			"scorching_ray" => SpellDamageShape.BeamHit,
			"shadow_bolt" or "gale_blade" or "chain_lightning" => SpellDamageShape.ChainJump,
			_ => SpellDamageShape.ProjectileHit
		};
	}

	private static SpellScalingTag ResolveScalingTags(SpellData spell)
	{
		SpellScalingTag tags = spell.GetScalingTags();
		if (tags != SpellScalingTag.None)
			return tags;

		// Fallback for resources that still have an empty mask.
		tags = SpellScalingTag.Cooldown;
		if (!spell.IsPassive)
			tags |= SpellScalingTag.Damage;

		SpellDamageShape shape = ResolveDamageShape(spell);
		if (shape == SpellDamageShape.RadiusBurst || shape == SpellDamageShape.PersistentZone)
			tags |= SpellScalingTag.Area;
		if (shape == SpellDamageShape.ProjectileHit)
			tags |= SpellScalingTag.ProjectileSpeed;
		if (shape == SpellDamageShape.ChainJump)
			tags |= SpellScalingTag.Chain;

		return tags;
	}

	private static string FormatTime(float seconds)
	{
		int totalSeconds = Mathf.FloorToInt(seconds);
		return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
	}

	// Sampled here because the level-up menu is where the loadout changes - a spell added or levelled
	// - and before the Scribe's Penknife cuts one. RunResult.ElementCounts is an end-of-run snapshot,
	// so without this a build that peaked at six Fire and cut a Fire spell at level 18 would look like
	// it never got there.
	private void SampleElementPeaks()
	{
		if (player != null && IsInstanceValid(player))
			RunEvents.RecordElementCounts(BuildElementCountSnapshot());
	}

	private void CloseLevelUpMenu()
	{
		// The single choke point for "the loadout may have just changed" - every pick, save and skip
		// funnels through here.
		SampleElementPeaks();

		// Decided before the teardown below, because reopening has to happen after the menu is gone
		// and the flag must not survive into the reopened menu and loop.
		bool reopenForBankedPick = extraPicksPending > 0;
		if (reopenForBankedPick)
			extraPicksPending--;

		// Remove the menu; the run resumes below, unless another screen is waiting.
		if (levelUpMenu != null)
		{
			levelUpMenu.QueueFree();
			levelUpMenu = null;
		}

		// The menu can open while standing inside a swarm; without a grace period the player takes
		// contact damage on the first physics tick after unpausing, before they can react.
		if (player != null && IsInstanceValid(player))
			player.GrantInvincibility(player.PostMenuInvincibilitySeconds);

		// The extra pick a banked level-up bought. Reopened rather than granted as a level, so the
		// player picks twice at one level rather than levelling twice.
		if (reopenForBankedPick)
			OnPlayerLevelGained();
		else
			ResumeOrOpenNextMenu();
	}

	private void RefreshElementHud()
	{
		RefreshSelectedSpellHud();

		if (elementHudGrid == null || player == null)
			return;

		foreach (Node child in elementHudGrid.GetChildren())
			child.QueueFree();

		var counts = player.GetElementInstanceCounts();
		foreach (var pair in counts
			.Where(kvp => kvp.Value > 0)
			.OrderByDescending(kvp => kvp.Value)
			.ThenBy(kvp => kvp.Key.ToString()))
		{
			elementHudGrid.AddChild(BuildElementHudBadge(pair.Key, pair.Value));
		}
	}

	private void RefreshSelectedSpellHud()
	{
		if (selectedSpellHudRow == null || player == null)
			return;

		foreach (Node child in selectedSpellHudRow.GetChildren())
			child.QueueFree();

		var spells = player.GetEquippedSpells()
			.Where(spell => spell != null)
			.ToList();

		if (spells.Count == 0)
		{
			selectedSpellHudRow.AddChild(new Label
			{
				Text = "No spells selected",
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			});
			return;
		}

		foreach (SpellData spell in spells)
			selectedSpellHudRow.AddChild(BuildSelectedSpellHudIcon(spell));
	}

	private Control BuildSelectedSpellHudIcon(SpellData spell)
	{
		var frame = new PanelContainer
		{
			CustomMinimumSize = new Vector2(48f, 48f),
			TooltipText = $"{spell.Name} Lv {spell.CurrentLevel}/{spell.MaxLevel}",
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};

		var frameStyle = new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0f),
			BorderColor = spell.IsPassive
				? new Color(0.45f, 0.90f, 0.72f, 0.95f)
				: new Color(0.95f, 0.63f, 0.45f, 0.95f)
		};
		frameStyle.SetBorderWidthAll(1);
		frameStyle.SetCornerRadiusAll(4);
		frameStyle.SetContentMarginAll(3);
		frame.AddThemeStyleboxOverride("panel", frameStyle);

		var stack = new MarginContainer();
		frame.AddChild(stack);

		var icon = new TextureRect
		{
			Texture = spell.Icon ?? FallbackSpellHudIcon,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(42f, 42f)
		};
		stack.AddChild(icon);

		return frame;
	}

	private Control BuildElementHudBadge(Element element, int count)
	{
		Color baseColor = ElementColors.GetColor(element);
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(102, 28),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		var style = new StyleBoxFlat();
		style.BgColor = new Color(baseColor.R, baseColor.G, baseColor.B, 0.88f);
		style.BorderColor = new Color(1f, 1f, 1f, 0.28f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(5);
		panel.AddThemeStyleboxOverride("panel", style);

		var label = new Label
		{
			Text = $"{element} {ElementPassiveDescriptions.GetProgressLabel(count)}",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.AddThemeColorOverride("font_color", GetReadableTextColor(baseColor));
		panel.AddChild(label);
		return panel;
	}

	// By WCAG contrast (StatIcons.ReadableOn), not by a brightness threshold. The threshold put
	// white on the Arcane badge of an active element passive at 4.1:1 (measured by _UiShot
	// --ink-report on the pause screen's Run tab); dark ink there is about 4.9:1. Same fix the
	// level-up chips had (.ai/level-up-screen.md, section 4a).
	private static Color GetReadableTextColor(Color background) => StatIcons.ReadableOn(background);

	/// <summary>The horde's pace right now, easing from the opening value to the late one.</summary>
	private float GetEnemyPaceMultiplier()
	{
		float minutesElapsed = PaceMinutes;
		float ramp = EnemySpeedRampMinutes > 0.0f
			? Mathf.Clamp(minutesElapsed / EnemySpeedRampMinutes, 0.0f, 1.0f)
			: 1.0f;

		// Wormwood Tithe makes them faster. Multiplied in here rather than branched on, because
		// the getter returns 1.0 when the curse is not held.
		return Mathf.Lerp(EnemyMoveSpeedMultiplier, EnemyMoveSpeedMultiplierLate, ramp)
			* (player?.GetCurseEnemySpeedMultiplier() ?? 1.0f);
	}

	private void TrackPlayerTravel(float delta)
	{
		if (player == null || delta <= 0.0f)
			return;

		Vector2 position = player.GlobalPosition;
		if (hasPlayerSpawnSample)
		{
			Vector2 sample = (position - lastPlayerSpawnSample) / delta;
			measuredPlayerVelocity = measuredPlayerVelocity.Lerp(sample, 0.25f);
		}

		lastPlayerSpawnSample = position;
		hasPlayerSpawnSample = true;
	}

	private void UpdateSpawnScaling()
	{
		float minutesElapsed = PaceMinutes;
		float steadyInterval = SpawnBaseInterval - (minutesElapsed * SpawnIntervalReductionPerMinute);
		float openingBlend = SpawnOpeningRampMinutes > 0.0f
			? Mathf.Clamp(minutesElapsed / SpawnOpeningRampMinutes, 0.0f, 1.0f)
			: 1.0f;
		// The curse shortens the gap between spawns. It is applied before the SpawnMinInterval
		// floor, so it can never drive the spawner past the rate the arena is built to survive.
		spawnInterval = Mathf.Max(
			SpawnMinInterval,
			Mathf.Lerp(SpawnOpeningInterval, steadyInterval, openingBlend) * presetSpawnIntervalScale
				* (player?.GetCurseSpawnIntervalMultiplier() ?? 1.0f));
		// Quadratic ease-in: the per-minute term is scaled by how far into the ramp we are, so
		// it grows as minutes^2 early and as the plain line once the ramp is spent.
		float rampProgress = SpawnHealthRampMinutes > 0.0f
			? Mathf.Min(1.0f, minutesElapsed / SpawnHealthRampMinutes)
			: 1.0f;
		spawnHealth = (SpawnBaseHealth + (minutesElapsed * SpawnHealthPerMinute * rampProgress)) * presetSpawnHealthScale;
	}

	/// <param name="at">
	/// Where to put it. Null keeps the ordinary behaviour of choosing a spot on the spawn ring;
	/// a formation passes its member positions in so the shape survives contact with the spawner.
	/// </param>
	// --- Side events (.ai/side-events.md) -------------------------------------------------------
	// What a SideEvent is allowed to ask of the run. Kept to this short list on purpose: an event
	// spawns through the same paths, health curve and stage table as everything else, so it can
	// never put something on the field the stage would not.

	public Player? RunPlayer => player != null && IsInstanceValid(player) ? player : null;
	public bool BossFightActive => bossFightActive;
	public bool RunFinished => runFinished;
	public float RunSeconds => timeElapsed;

	/// <summary>
	/// Spawns one enemy for an event. A null scene path takes the stage's own spawn table, so a
	/// swamp's wisps are swamp creatures. Health is the current ordinary spawn health times
	/// <paramref name="healthMultiplier"/>, so an event late in a run is as hard as the run is.
	/// </summary>
	public Enemy? SpawnEventEnemy(string? scenePath, Vector2 at, float healthMultiplier, bool elite)
	{
		PackedScene? scene = string.IsNullOrEmpty(scenePath)
			? SelectEnemyForCurrentStage().Scene
			: ResourceLoader.Load<PackedScene>(scenePath);
		return scene == null ? null : SpawnEventEnemyFrom(scene, at, healthMultiplier, elite);
	}

	/// <summary>
	/// Spawns this chapter's own creature for <paramref name="role"/> for an event (StageBestiary): the
	/// Gaoler's key carrier is the chapter's miniboss, the caravan master its heavy. Health as above.
	/// </summary>
	public Enemy? SpawnEventEnemy(BestiaryRole role, Vector2 at, float healthMultiplier, bool elite)
	{
		PackedScene? scene = bestiary.SceneFor(role);
		if (scene == null)
		{
			// Only reachable if an event moves to a chapter without that role (the Forest has no
			// heavy): say so, and still give the event its body.
			GD.PushError($"Node2DGame: {bestiary.Source.ChapterName} has no {role} for a side event; it takes the spawn table instead.");
			scene = SelectEnemyForCurrentStage().Scene;
		}
		return SpawnEventEnemyFrom(scene, at, healthMultiplier, elite);
	}

	private Enemy? SpawnEventEnemyFrom(PackedScene scene, Vector2 at, float healthMultiplier, bool elite)
	{
		var node = scene.Instantiate<Node2D>();
		if (node is not Enemy enemy)
		{
			node.QueueFree();
			return null;
		}

		enemy.Health = Mathf.Max(1, Mathf.RoundToInt(spawnHealth * healthMultiplier));
		enemy.SpawnBaseSpeed = enemy.Speed;
		enemy.Speed *= GetEnemyPaceMultiplier();
		if (elite)
		{
			enemy.Scale *= EliteScaleMultiplier;
			// Elites are never recycled to the spawn ring, which is what an event enemy needs: a
			// key-carrier that vanished and reappeared somewhere else would read as a bug.
			enemy.IsMiniBoss = true;
			musicLeadCallers.Add(enemy);
		}
		enemy.Position = IsImpassablePosition(at) ? FindSeparatedSpawnPosition() : ClampPositionToStageBounds(at);
		AddChild(enemy);
		totalEnemiesSpawned++;
		return enemy;
	}

	/// <summary>
	/// A walkable point between <paramref name="minDistance"/> and <paramref name="maxDistance"/>
	/// from the player, inside the stage, kept clear of <paramref name="avoid"/> by
	/// <paramref name="avoidRadius"/>. Falls back to the best try rather than failing.
	/// </summary>
	public Vector2 FindEventSite(float minDistance, float maxDistance, IReadOnlyList<Vector2>? avoid = null, float avoidRadius = 0f)
	{
		Vector2 origin = player != null && IsInstanceValid(player) ? player.GlobalPosition : Vector2.Zero;
		Vector2 best = origin;
		for (int attempt = 0; attempt < 40; attempt++)
		{
			float angle = spawnRng.Randf() * Mathf.Tau;
			float distance = Mathf.Lerp(minDistance, maxDistance, spawnRng.Randf());
			Vector2 candidate = ClampPositionToStageBounds(origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
			if (IsImpassablePosition(candidate))
				continue;
			best = candidate;
			if (candidate.DistanceTo(origin) < minDistance * 0.8f)
				continue;
			bool clear = true;
			if (avoid != null)
			{
				foreach (Vector2 other in avoid)
				{
					if (other.DistanceTo(candidate) < avoidRadius) { clear = false; break; }
				}
			}
			if (clear)
				return candidate;
		}
		return best;
	}

	public Vector2 ClampToStage(Vector2 position) => ClampPositionToStageBounds(position);

	/// <summary>
	/// The nearest point to <paramref name="position"/> the player can actually stand on: inside the
	/// stage bounds and not in water or a wall. Enemies are not held to the bounds the player is, so
	/// anything an enemy drops for the player to pick up must pass through here first - a Warden that
	/// dies outside the clamp would otherwise leave its key where nobody can reach it.
	/// </summary>
	public Vector2 ReachablePoint(Vector2 position)
	{
		Vector2 clamped = ClampPositionToStageBounds(position);
		if (!IsImpassablePosition(clamped))
			return clamped;
		for (float radius = 24f; radius <= 480f; radius += 24f)
		{
			for (int k = 0; k < 12; k++)
			{
				float angle = k * Mathf.Tau / 12f;
				Vector2 candidate = ClampPositionToStageBounds(clamped + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
				if (!IsImpassablePosition(candidate))
					return candidate;
			}
		}
		return clamped;
	}

	/// <summary>
	/// A chest at a point: what a side event pays out once its discovery is already owned. With
	/// <paramref name="materialiseSeconds"/> it rises as a ghost first and cannot be taken until solid.
	/// </summary>
	public void SpawnRewardChestAt(Vector2 at, float materialiseSeconds = 0f)
	{
		AddChild(new ChestReward { Position = at, MaterialiseSeconds = materialiseSeconds, Source = "side event" });
	}

	// Discoveries made this run, for the game over screen. Granted and saved the moment they happen
	// (DiscoveryRewards) - a wizard carried out of a cell stays free even if the run is lost after.
	private readonly List<string> runDiscoveredSpells = new();
	private readonly List<string> runDiscoveredWizards = new();
	// The same discoveries as results-screen cards, with the ids their pictures are found by.
	private readonly List<RunUnlockEntry> runDiscoveryEntries = new();

	public void RecordDiscovery(DiscoveryRewards.Grant grant)
	{
		// A boon from an early chapter's site is its own kind of card, not a spell with no icon.
		bool isBoon = !grant.IsWizard && BoonCatalog.GetById(grant.Id) != null;
		if (!isBoon)
			(grant.IsWizard ? runDiscoveredWizards : runDiscoveredSpells).Add(grant.DisplayName);
		runDiscoveryEntries.Add(new RunUnlockEntry
		{
			Kind = grant.IsWizard ? RunUnlockKind.Wizard : isBoon ? RunUnlockKind.Boon : RunUnlockKind.Spell,
			Id = grant.Id ?? string.Empty,
			Name = grant.DisplayName,
			Source = grant.IsWizard ? "Freed during the run" : "Found during the run",
		});
	}

	// Ascension paths and Apotheosis forms this run met for the first time ever (AscensionSightings),
	// for the results screen. Empty on any run that remembers nothing (every harness).
	private readonly List<RunUnlockEntry> runRevealedAscensions = new();

	// --- Exploration: where the side event and its wayfinder are (.ai/side-events.md) ------------
	// The event is placed at stage start, out in the world, from the terrain seed (ExplorePlacer).
	// What the placer needs from the run is the seed and an answer about the ground; what the run
	// needs back is the footprints, so the furnishing never puts a tree on a lantern's pool.

	public ulong TerrainSeed => terrainSeed;
	public Vector2 StageOrigin => stageOrigin;

	/// <summary>A placer for this run's points of interest. <paramref name="stream"/> separates independent layouts.</summary>
	public ExplorePlacer CreateExplorePlacer(int stream, float eventMinChunks, float eventMaxChunks)
	{
		bool maze = MazeNavigation.Active is { IsMaze: true };
		return new ExplorePlacer(terrainSeed, Global.SelectedStageIdx, stream, new ExplorePlacer.Ground
		{
			IsClear = IsExploreGroundClear,
			Origin = stageOrigin,
			ChunkPixels = TerrainStreamer.ChunkTiles * ProceduralTilePixels,
			// A maze corridor block plus its wall strip: cell centres are this far apart, and the
			// spawn point is the middle of cell (0, 0) (LevelGenerator's lattice).
			MazeCellPixels = maze ? 8 * ProceduralTilePixels : 0f,
		})
		{
			EventMinChunks = eventMinChunks,
			EventMaxChunks = eventMaxChunks,
		};
	}

	// A site's footprint is tested at its centre, on a ring at its radius and on one at half of it.
	private const int ExploreRingPoints = 8;
	// And it must not be an island: a flood over open cells from its centre has to get this many
	// cells away. Far chunks have no navigation window yet to ask (IsReachable is false beyond the
	// painted square), so this asks the chunk data directly. Lakes are a few cells to a couple of
	// dozen across; an enclosure bigger than this is not a thing the generator makes.
	private const int ExploreEscapeCells = 24;

	/// <summary>
	/// True if a footprint of <paramref name="radius"/> round <paramref name="centre"/> is open ground
	/// the player can stand on and walk away from: no water, wall, hazard, fence or landmark, and not
	/// an island. Asks the streamer's chunk data, generating it for far ground (not painting it), so
	/// the answer is what that ground will be when the player gets there.
	/// </summary>
	public bool IsExploreGroundClear(Vector2 centre, float radius)
	{
		if (terrainStreamer == null)
			return true;
		if (!IsExploreSpotOpen(centre))
			return false;
		for (int k = 0; k < ExploreRingPoints; k++)
		{
			float angle = k * Mathf.Tau / ExploreRingPoints;
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			if (!IsExploreSpotOpen(centre + dir * radius) || !IsExploreSpotOpen(centre + dir * radius * 0.5f))
				return false;
		}
		// The maze is connected by construction (.ai/terrain.md section 7); open ground is asked.
		return MazeNavigation.Active is { IsMaze: true } || CanWalkAwayFrom(centre);
	}

	private bool IsExploreSpotOpen(Vector2 p) =>
		!terrainStreamer!.IsBlockedAtWorld(p) && !terrainStreamer.IsWallAtWorld(p) && !terrainStreamer.IsFenceAtWorld(p)
		&& !IsPropAvoidedAt(p) && !IsInLandmark(p, 2);

	// A flood over cells that are not water, wall or fence, from the centre's cell, until it gets
	// ExploreEscapeCells away (Chebyshev). Stage start only, a handful of times; allocates its own
	// small grid.
	private bool CanWalkAwayFrom(Vector2 centre)
	{
		const int r = ExploreEscapeCells;
		const int size = 2 * r + 1;
		Vector2I start = terrainStreamer!.CellOfWorld(centre);
		var visited = new bool[size * size];
		var queue = new Queue<Vector2I>();
		queue.Enqueue(Vector2I.Zero);
		visited[r + r * size] = true;
		while (queue.Count > 0)
		{
			Vector2I c = queue.Dequeue();
			if (Math.Abs(c.X) >= r || Math.Abs(c.Y) >= r)
				return true;
			for (int d = 0; d < 4; d++)
			{
				Vector2I n = c + (d == 0 ? Vector2I.Right : d == 1 ? Vector2I.Left : d == 2 ? Vector2I.Down : Vector2I.Up);
				int i = (n.X + r) + (n.Y + r) * size;
				if (visited[i])
					continue;
				visited[i] = true;
				if (terrainStreamer.IsSolidCell(start.X + n.X, start.Y + n.Y))
					continue;
				queue.Enqueue(n);
			}
		}
		return false;
	}

	// The footprints of this run's sites, and the strip along the caravan's road. A handful of
	// circles, fixed at stage start, so every chunk is furnished round them identically on every
	// visit - the furnishing stays a pure function of (seed, chunk).
	private readonly List<(Vector2 Centre, float Radius)> exploreReserves = new();

	/// <summary>Keeps decor, props and stage hazards out of a circle for the rest of the run.</summary>
	public void ReserveExploreGround(Vector2 centre, float radius) => exploreReserves.Add((centre, radius));

	private bool IsInExploreReserve(Vector2 position, float margin = 0f)
	{
		foreach (var (centre, radius) in exploreReserves)
		{
			float r = radius + margin;
			if (centre.DistanceSquaredTo(position) < r * r)
				return true;
		}
		return false;
	}

	// How much further than a footprint a tree keeps away: its crown is far wider than its trunk, and
	// a crown over a lantern hides the lantern.
	private const float ExploreTreeMargin = 140f;

	private void SpawnEnemy(Vector2? at = null)
	{
		var perfStart = FrameProfiler.Begin();
		var selection = SelectEnemyForCurrentStage();
		if (selection.IsElite && GetCurrentEliteEnemyCount() >= MaxEliteEnemiesAlive)
			selection = (selection.Scene, selection.HealthMultiplier, false);

		var enemy = selection.Scene.Instantiate<Node2D>();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.Health = Mathf.RoundToInt(spawnHealth * selection.HealthMultiplier * HarnessEnemyHealthScale);
			typedEnemy.SpawnBaseSpeed = typedEnemy.Speed;
			typedEnemy.Speed *= GetEnemyPaceMultiplier();
			// The swarm may speed, fold and re-place these, and nothing else (SwarmPressure.cs).
			typedEnemy.SpawnHealthMultiplier = selection.HealthMultiplier;
			typedEnemy.SwarmEligible = !selection.IsElite;
			// A body the table rolls is an ordinary body, whatever its scene says. Rockem's scene is
			// flagged IsMiniBoss because it is the Sketchbook's scheduled miniboss as well as 7% of its
			// table (TickWardenMiniBossSpawn takes the scene as it is), and every one the table sent
			// carried the flag with it: unshovable (BlocksPlayer), never recycled, 50 XP a kill. A
			// crowd there held several bodies the player could not push through, and pinned him
			// (2026-10-27, "enemies are getting stuck on the player again").
			typedEnemy.IsMiniBoss = selection.IsElite;
			if (selection.IsElite)
			{
				typedEnemy.Health = Mathf.RoundToInt(typedEnemy.Health * EliteHealthMultiplier * presetElitePowerScale);
				typedEnemy.Speed *= EliteSpeedMultiplier * Mathf.Lerp(1.0f, presetElitePowerScale, 0.55f);
				typedEnemy.Scale *= EliteScaleMultiplier;
				typedEnemy.IsMiniBoss = true;
				typedEnemy.ChestSource = ChestSourceElite;
				musicLeadCallers.Add(typedEnemy);
			}
		}

		enemy.Position = at ?? FindSeparatedSpawnPosition();
		AddChild(enemy);
		totalEnemiesSpawned++;
		FrameProfiler.End(FrameProfiler.Section.Spawning, perfStart);
	}

	/// <summary>Spawns one wave in a shape, if it is time for one.</summary>
	private void TickFormations(float delta)
	{
		if (player == null || bossFightActive || spawnGraceRemaining > 0f)
			return;

		if (nextFormationTime <= 0f)
			nextFormationTime = Mathf.Max(FormationFirstSeconds * RunPace, FormationIntervalSeconds);

		if (timeElapsed < nextFormationTime)
			return;

		// Formations answer to the same two budgets as the trickle: a wave arriving while the arena
		// is already full must not push past MaxEnemies.
		//
		// The timer is advanced only once a wave actually goes out. Advancing it first meant a
		// formation that found no room burned its slot and the next was another fifteen seconds
		// away - measured, that starved them down to two waves in the first three minutes out of
		// roughly eight due, because the ordinary trickle had already filled the arena. Retrying
		// each frame instead costs one count of a group and fires the moment room appears.
		int alive = EnemyRegistry.Count;
		int room = Mathf.Min(MaxEnemies - alive, MaxSpawnsPerBurstWindow - spawnCountInBurstWindow);
		// Below three members no shape survives, so a wave that small is just a scatter wearing the
		// name of one. Better to wait for room than to spend the slot on something shapeless.
		if (room < 3)
			return;

		nextFormationTime = timeElapsed + Mathf.Max(1f, FormationIntervalSeconds);

		float minutesElapsed = PaceMinutes;
		SpawnFormation shape = SpawnFormations.Pick(minutesElapsed, spawnRng.Randf());
		int count = Mathf.Min(room, spawnRng.RandiRange(
			Mathf.Max(1, FormationMinSize), Mathf.Max(FormationMinSize, FormationMaxSize)));

		// One bearing for the whole wave, biased toward where the player is going, so the shape
		// lands in their path rather than somewhere they have already been.
		float baseBearing = PickSpawnBearing();

		for (int i = 0; i < count; i++)
		{
			var (bearing, radiusScale) = SpawnFormations.Placement(shape, i, count, baseBearing);
			var direction = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
			Vector2 position = ClampPositionToStageBounds(
				player.GlobalPosition + (direction * GetSpawnRadiusForDirection(direction) * radiusScale));

			// A member that lands in a wall or a lake would be stuck there for the rest of its
			// life. Falling back to an ordinary spawn point costs that one member its place in the
			// shape, which is far better than a formation with a hole in it that never moves. In a
			// maze the same goes for a member that would land a long walk round from the player.
			if (IsImpassablePosition(position) || IsPoorMazeSpawn(position))
				position = FindSeparatedSpawnPosition();

			SpawnEnemy(position);
			spawnCountInBurstWindow++;
		}
	}

	// The Warden is the run's recurring miniboss: the dark wizard's jailer, which is why it
	// arrives on a clock rather than out of a spawn table - a jailer is not part of the landscape,
	// it is sent. The Soldier it replaces was justified in this comment as "the only living,
	// armoured humanoid among a roster of skeletons, an orc and a ghost", which was a silhouette
	// argument resting on an art-pack accident and flatly against .ai/world-and-tone.md: the horde
	// serves one will and there are no mercenaries in it. The Warden reads because it is the only
	// figure on the 48x48 elite cell and the only one in the cast with chains hanging off it.
	// One at a time, on a timer, well after the opening minutes.
	private void TickWardenMiniBossSpawn(float delta)
	{
		// The chapter's own jailer (StageBestiary, the MiniBoss role): the Warden in the Cursed
		// Dungeon, Rockem in the Sketchbook, and in every other chapter its own once drawn. The
		// recurring miniboss is the run's only set-piece between the opening and the boss, so it is
		// the last place a chapter should be wearing another chapter's creature.
		PackedScene? scene = bestiary.SceneFor(BestiaryRole.MiniBoss);
		string miniBossType = bestiary.EnemyTypeFor(BestiaryRole.MiniBoss);

		if (!EnableWardenMiniBoss || runFinished || player == null || !IsInstanceValid(player))
			return;
		if (PaceSeconds < WardenMiniBossFirstSpawnSeconds)
			return;

		wardenMiniBossTimer += delta / RunPace;
		if (wardenMiniBossTimer < WardenMiniBossIntervalSeconds)
			return;

		// Never stack them: a second Warden arriving while the first is alive turns a set-piece
		// into an unwinnable pile. Minibosses only: Rockem is also the Sketchbook's table heavy, and
		// counting every body of its EnemyType held the scheduled Rockem back for as long as any of
		// the table's was alive, which in a full swarm is nearly the whole run. The table's are
		// ordinary bodies now (SpawnEnemy), so IsMiniBoss tells them apart, and the Gaoler's key
		// carrier - the miniboss an event sends - still counts.
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is Enemy existing && IsInstanceValid(existing) && existing.IsMiniBoss && existing.EnemyType == miniBossType)
				return;
		}
		// In the Sketchbook the ghost is the other set-piece, and the miniboss waits for it too, so
		// the chapter's two never share the screen.
		if (IsScheduledMiniBossAlive(ghostMiniBoss))
			return;

		if (scene == null)
			return;

		wardenMiniBossTimer = 0f;
		var warden = scene.Instantiate<Node2D>();
		if (warden is Enemy typedWarden)
		{
			// Scales with run length the same way ordinary spawns do, so a late Warden is still
			// a threat rather than a speed bump.
			float minutesElapsed = PaceMinutes;
			typedWarden.Health = Mathf.RoundToInt(typedWarden.Health * (1f + minutesElapsed * 0.22f) * presetElitePowerScale);
			// The run's recurring miniboss always carries a chest: four a run, at 5:15, 8:00, 10:45
			// and 13:30. (Rockem in the Sketchbook, the same slot.)
			typedWarden.ChestSource = ChestSourceWarden;
		}
		warden.Position = FindSeparatedSpawnPosition();
		AddChild(warden);
		musicLeadCallers.Add(warden);
		totalEnemiesSpawned++;
		scheduledMiniBoss = warden as Enemy;
	}

	// The two scheduled set-pieces, held by reference. NOT found by EnemyType: Rockem is also 7% of
	// the Sketchbook's ordinary table, so "is a Rockem alive" is nearly always yes and the first
	// version of the ghost's schedule, which asked that, never sent it at all.
	private Enemy? scheduledMiniBoss;
	private Enemy? ghostMiniBoss;

	// Alive and still owed its fight. A ghost mid-phase has left "enemies" (SetTargetable) and is
	// still alive, so this reads the node, not the group.
	private static bool IsScheduledMiniBossAlive(Enemy? enemy) =>
		enemy != null && IsInstanceValid(enemy) && enemy.IsInsideTree() && !enemy.IsDying;

	// THE SKETCHBOOK'S GHOST (Elim's Boo, scripts/BooEnemy.cs): its second miniboss, sent twice a
	// run on the pace clock. Rockem is the recurring one and arrives at about 5:15, 8:00, 10:45 and
	// 13:30 (TickWardenMiniBossSpawn: first at 150 + 165 pace seconds, then every 165), so the ghost
	// is set into the gaps - 4:00 and 9:30 - and the chapter gets a set-piece every 75-90 seconds
	// from minute four, never two at once. Pace seconds, so a shorter run brings it proportionally
	// sooner, exactly as it does everything else that decides WHEN in a run (.ai/run-pacing.md).
	private static readonly float[] GhostMiniBossPaceSeconds = { 240f, 570f };
	private int ghostMiniBossesSent;

	private void TickGhostMiniBoss()
	{
		if (!IsSketchbookStage || runFinished || player == null || !IsInstanceValid(player))
			return;
		if (ghostMiniBossesSent >= GhostMiniBossPaceSeconds.Length
			|| PaceSeconds < GhostMiniBossPaceSeconds[ghostMiniBossesSent])
			return;

		// Late rather than stacked. A ghost due while the scheduled Rockem (or the last ghost) is
		// still alive waits for it to die - the appearance is owed, not skipped.
		if (IsScheduledMiniBossAlive(scheduledMiniBoss) || IsScheduledMiniBossAlive(ghostMiniBoss))
			return;

		ghostMiniBossesSent++;
		var ghost = booEnemyScene.Instantiate<Enemy>();
		// Scaled with the run the way the Warden is, so the 9:30 ghost is not a speed bump.
		float minutesElapsed = PaceMinutes;
		ghost.Health = Mathf.RoundToInt(ghost.Health * (1f + minutesElapsed * 0.22f) * presetElitePowerScale);
		// Elim's ghost is a miniboss, and a miniboss carries a chest.
		ghost.ChestSource = ChestSourceGhost;
		ghost.Position = FindSeparatedSpawnPosition();
		AddChild(ghost);
		ghostMiniBoss = ghost;
		musicLeadCallers.Add(ghost);
		totalEnemiesSpawned++;
		GD.Print($"Node2DGame: the Sketchbook's ghost arrives ({ghostMiniBossesSent} of {GhostMiniBossPaceSeconds.Length}) "
			+ $"at pace {PaceSeconds:0}s, real {timeElapsed:0}s, health {ghost.Health}");
	}

	// The end of a level. At TimerVictorySeconds the survival clock stops and the stage's boss
	// arrives; beating it is the win. A stage with no boss in the catalog still ends right here the
	// way it always did, so bosses can be added one stage at a time.
	// Returns true when the run ended inside this call, so _Process knows to stop for the frame.
	private bool TickBossPhaseStart()
	{
		if (bossPhaseTriggered || runFinished || bossFightActive || TimerVictorySeconds <= 0f || timeElapsed < TimerVictorySeconds)
			return false;

		bossPhaseTriggered = true;
		// A Boss Rush ends on its own last boss (BossRushCatalog.Roster), which in its arena is the
		// same one - but the rush is the authority on it, and on which of how many it is.
		activeBossDefinition = IsBossRush ? BeginBossRushFinale() : BossCatalog.ForStageIndex(Global.SelectedStageIdx);
		if (activeBossDefinition == null || !SpawnBoss(activeBossDefinition))
		{
			activeBossDefinition = null;
			FinishRunAndReward(RunOutcome.Victory);
			return true;
		}

		bossFightActive = true;
		PlayBossMusic();
		ShowBossBanner(activeBossDefinition);
		GD.Print($"Boss phase: {activeBossDefinition.DisplayName} ({activeBossDefinition.Id}) with {bossInstance?.Health ?? 0} HP. Timer stopped at {FormatTime(timeElapsed)}.");
		return false;
	}

	// The dog, once, at the start of the Sketchbook and nowhere else.
	//
	// Spawned with the arena rather than dropped by anything, because the kids' brief for it was
	// "the player should always have it in this map" - so it is a property of the chapter, not a
	// reward, not a pickup and not a spell. It is invincible and never leaves, which means this
	// runs exactly once and there is nothing to respawn, cap or clean up.
	private void SpawnKidDog()
	{
		if (!IsSketchbookStage || kidDogScene == null || player == null || !IsInstanceValid(player))
			return;

		if (kidDogScene.Instantiate() is not Node2D dog)
		{
			GD.PushError("Node2DGame: KidDog.tscn did not instantiate as a Node2D.");
			return;
		}

		AddChild(dog);
		dog.GlobalPosition = player.GlobalPosition + new Vector2(-70f, 34f);
	}

	/// <summary>
	/// True while the run is in the Sketchbook, the one chapter whose whole cast is the kids'
	/// drawings.
	/// </summary>
	/// <remarks>
	/// Everything here keys off one decision: nothing in that chapter may be a sprite the kids did
	/// not draw. Its cast - the table, Blobby and Pushy in the charger and slammer slots, Rockem as
	/// the miniboss, and the four roles with no counterpart left empty - is its roster now
	/// (StageBestiary.Sketchbook.cs). What still reads this flag is what is not a role: the ghost's
	/// clock, the dog, and the bare page with no decor.
	///
	/// Keyed on the ENVIRONMENT rather than on the stage index, so a second kids' chapter would
	/// inherit all of it for free.
	/// </remarks>
	private bool IsSketchbookStage =>
		StageEnvironmentCatalog.GetForStageIndex(Global.SelectedStageIdx).Kind
			== StageEnvironmentKind.Sketchbook;

	/// <param name="healthScale">
	/// 1 for a chapter's boss. A Boss Rush scales the bosses it sends before the finale to the point
	/// of the run they arrive at (BossRushHealthScale).
	/// </param>
	private bool SpawnBoss(BossDefinition definition, float healthScale = 1f)
	{
		if (player == null || !IsInstanceValid(player))
			return false;

		// SpawnScenePath: the boss's own scene once it exists, its stand-in until then (a chapter
		// still being built fights another chapter's boss under its own boss's id and name).
		var scene = ResourceLoader.Load<PackedScene>(definition.SpawnScenePath);
		if (scene == null)
		{
			GD.PushError($"Node2DGame: boss scene not found: {definition.SpawnScenePath}");
			return false;
		}

		if (scene.Instantiate() is not BossEnemy boss)
		{
			GD.PushError($"Node2DGame: {definition.SpawnScenePath} does not have a BossEnemy script attached.");
			return false;
		}

		boss.BossId = definition.Id;
		boss.BossDisplayName = definition.DisplayName;
		// Health rides the balance preset the same way elites do, so hardcore keeps its teeth and
		// casual does not turn the fight into a ten-minute chore.
		boss.Health = Mathf.RoundToInt(definition.Health * presetElitePowerScale * healthScale);
		boss.Position = FindBossSpawnPosition();
		boss.Connect(BossEnemy.SignalName.BossDefeated, new Callable(this, nameof(OnBossDefeated)));
		AddChild(boss);
		bossInstance = boss;
		totalEnemiesSpawned++;
		ConfigureBossHud(definition);
		return true;
	}

	// Far enough out that the boss is seen walking in rather than materialising on top of the
	// player, close enough that it is on screen when the banner names it.
	private Vector2 FindBossSpawnPosition()
	{
		if (player == null || !IsInstanceValid(player))
			return stageOrigin;

		for (int i = 0; i < 16; i++)
		{
			float angle = spawnRng.Randf() * Mathf.Tau;
			float radius = spawnRng.RandfRange(300f, 400f);
			Vector2 candidate = ClampPositionToStageBounds(player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
			if (candidate.DistanceTo(player.GlobalPosition) < 240f)
				continue;
			if (IsImpassablePosition(candidate))
				continue;
			return candidate;
		}

		return ClampPositionToStageBounds(player.GlobalPosition + new Vector2(0f, -340f));
	}

	// A wide bar under the run timer. The boss's health is the only thing the player needs to read
	// during the fight, so it gets the full width rather than sitting beside the player's own bar.
	private void ConfigureBossHud(BossDefinition definition)
	{
		var uiOverlay = GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (uiOverlay == null)
			return;

		bossHudRoot = new Control
		{
			Name = "BossHud",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			OffsetLeft = 24f,
			OffsetTop = 44f,
			OffsetRight = -24f,
			OffsetBottom = 44f + 34f
		};
		uiOverlay.AddChild(bossHudRoot);
		SafeArea.ApplyTo(bossHudRoot);

		bossHudBar = new ProgressBar
		{
			Name = "BossHealthFill",
			MinValue = 0,
			MaxValue = 1000,
			Value = 1000,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bossHudBar.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var trough = new StyleBoxFlat { BgColor = new Color(0.06f, 0.04f, 0.05f, 0.88f), BorderColor = new Color(0.62f, 0.30f, 0.24f, 0.95f) };
		trough.SetBorderWidthAll(1);
		trough.SetCornerRadiusAll(4);
		var fill = new StyleBoxFlat { BgColor = new Color(0.72f, 0.20f, 0.16f, 1.0f) };
		fill.SetCornerRadiusAll(4);
		bossHudBar.AddThemeStyleboxOverride("background", trough);
		bossHudBar.AddThemeStyleboxOverride("fill", fill);
		bossHudRoot.AddChild(bossHudBar);

		bossHudLabel = new Label
		{
			Name = "BossHealthLabel",
			Text = BossHudTitle(definition),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bossHudLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		ResponsiveLayout.SetFont(bossHudLabel, ResponsiveLayout.TextRole.Micro);
		bossHudLabel.AddThemeColorOverride("font_color", new Color(1f, 0.94f, 0.88f));
		bossHudLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		bossHudLabel.AddThemeConstantOverride("outline_size", 5);
		bossHudRoot.AddChild(bossHudLabel);
	}

	private void UpdateBossHud()
	{
		if (!bossFightActive || runFinished)
			return;

		// The boss leaving the tree without its defeat signal would strand the run: the timer is
		// frozen and nothing else can end it. Treat a vanished boss as a win rather than letting the
		// player wander a dead arena forever.
		if (bossInstance == null || !IsInstanceValid(bossInstance))
		{
			GD.PushWarning("Node2DGame: the boss left the scene without reporting a defeat; ending the run in victory.");
			CompleteBossVictory(activeBossDefinition?.Id ?? string.Empty);
			return;
		}

		if (bossHudBar != null)
			bossHudBar.Value = bossHudBar.MaxValue * bossInstance.BossHealthFraction;
	}

	private void ShowBossBanner(BossDefinition definition)
	{
		ShowRunBanner($"{BossBannerTitle(definition)}\n{definition.Tagline}", new Color(1.0f, 0.86f, 0.72f, 1.0f), 3.6f);
	}

	// The stage intro's panel, reused for a line the whole run should read: a boss's arrival, and in a
	// Boss Rush each fall. One at a time - a new banner kills the last one's fade, or that fade's
	// closing Hide would take the new banner down with it.
	private Tween? runBannerTween;
	private const float BannerTop = 42f;

	private void ShowRunBanner(string text, Color tint, float holdSeconds, float fadeSeconds = 0.7f)
	{
		if (stageIntroLabel == null)
			return;

		runBannerTween?.Kill();
		var panel = stageIntroLabel.GetParent() as Control;
		if (panel != null)
		{
			panel.Visible = true;
			panel.Modulate = Colors.White;
			// Under the boss's bar while there is one. The banner sits where the bar does (both hang
			// from the timer), and the bar, added later, was drawn over the banner's top line - the
			// boss's own name, on the one frame it is announced.
			float top = BannerTop;
			if (bossHudRoot != null)
				top = Mathf.Max(top, bossHudRoot.OffsetBottom + 8f);
			// The phone's spell row is as wide as the banner is off-centre, so there it clears the
			// row's foot too, wherever the cutout has put it.
			if (ResponsiveLayout.IsNarrow(panel) && GetNodeOrNull<Control>("UIOverlay/LoadoutHud") is Control loadout)
				top = Mathf.Max(top, loadout.GetGlobalRect().End.Y + 8f);
			panel.OffsetBottom = top + (panel.OffsetBottom - panel.OffsetTop);
			panel.OffsetTop = top;
		}

		stageIntroLabel.Text = text;
		stageIntroLabel.Modulate = tint;

		// Hold, THEN fade, then hide - three steps in order. It used to set the whole tween parallel,
		// which started the fade on the first frame alongside the hold: every banner, the chapter's
		// opening line and each boss's name and tagline, was gone in about half a second, and the hold
		// only delayed the Hide of a panel already invisible. The fades share one step (Parallel).
		var tween = CreateTween();
		runBannerTween = tween;
		tween.TweenInterval(holdSeconds);
		tween.TweenProperty(stageIntroLabel, "modulate:a", 0.0f, fadeSeconds);
		if (panel != null)
		{
			tween.Parallel().TweenProperty(panel, "modulate:a", 0.0f, fadeSeconds);
			tween.TweenCallback(Callable.From(() => panel.Hide()));
		}
	}

	private void OnBossDefeated(string bossId)
	{
		CompleteBossVictory(bossId);
	}

	private void CompleteBossVictory(string bossId)
	{
		if (runFinished || bossVictoryPending)
			return;

		// A Boss Rush boss that was not the last: the run goes on (Node2DGame.BossRush.cs).
		if (FellBossRushBoss(bossId))
			return;

		if (bossInstance != null && IsInstanceValid(bossInstance)
			&& bossInstance.IsConnected(BossEnemy.SignalName.BossDefeated, new Callable(this, nameof(OnBossDefeated))))
		{
			bossInstance.Disconnect(BossEnemy.SignalName.BossDefeated, new Callable(this, nameof(OnBossDefeated)));
		}

		bossFightActive = false;
		bossInstance = null;
		bossHudRoot?.QueueFree();
		bossHudRoot = null;
		bossHudBar = null;
		bossHudLabel = null;

		// The fight is over the moment the boss falls, so its music is too - it fades under
		// boss_death rather than playing on through the victory beat.
		runMusic?.FadeOutAll(MusicPlayer.EndFadeSeconds);

		// Let the death animation and the last hits land before the victory screen covers them.
		// The player is made untouchable for that beat so a stray enemy cannot turn a win into a
		// loss in the second after the boss is already dead.
		bossVictoryPending = true;
		GD.Print($"Boss defeated: {bossId}. Level cleared.");
		player?.GrantInvincibility(BossVictoryDelaySeconds + 0.5f);
		GetTree().CreateTimer(BossVictoryDelaySeconds).Timeout += () =>
		{
			if (IsInstanceValid(this))
				FinishRunAndReward(RunOutcome.Victory, bossId);
		};
	}

	// --- Chests come from what the player kills (DropTable) -------------------------------------
	//
	// They used to come on a clock: one at 0:20 and one every 45 pace seconds after, about twenty a
	// run whatever the player did, set down beside him out of nothing. Now an elite, a Warden or the
	// Sketchbook's ghost carries one, and a side event pays one - so a chest is a thing that was won,
	// and the relics that make a collector are the ones that change who carries and what is inside.
	public const string ChestSourceElite = "elite";
	public const string ChestSourceWarden = "warden";
	public const string ChestSourceGhost = "ghost";
	public const string ChestSourceNested = "nested";
	public const string ChestSourceBoss = "boss";

	/// <summary>
	/// A chest carrier has died (Enemy.StartDeath). The Warden and the ghost always pay; an elite pays
	/// on Player.GetEliteChestChance, except the run's first, which always does.
	/// </summary>
	public void DropCarriedChest(Enemy carrier)
	{
		// The boss fight hands out nothing mid-duel, the rule the chest clock kept (_Process).
		if (carrier == null || player == null || !IsInstanceValid(player) || bossFightActive || runFinished)
			return;

		if (carrier.ChestSource == ChestSourceElite)
		{
			bool pays = !firstEliteChestPaid || spawnRng.Randf() < player.GetEliteChestChance();
			firstEliteChestPaid = true;
			if (!pays)
				return;
		}

		PlaceChestNear(carrier.GlobalPosition, carrier.ChestSource);
	}

	private void PlaceChestNear(Vector2 at, string source)
	{
		if (player == null || !IsInstanceValid(player))
			return;

		// Where it fell, but never under the player: an elite usually dies touching him, and a chest
		// he is already standing on would open the moment it could. Set out along the line from him to
		// the body, then passed through ReachablePoint like everything an enemy drops (a Warden can
		// die over water the player cannot cross).
		Vector2 fromPlayer = at - player.GlobalPosition;
		if (fromPlayer.Length() < DropTable.CarriedChestMinDistance)
		{
			Vector2 direction = fromPlayer.LengthSquared() > 1f ? fromPlayer.Normalized() : Vector2.Up;
			at = player.GlobalPosition + direction * DropTable.CarriedChestMinDistance;
		}
		// Deferred, as Enemy.DropXp defers its orb: a death can land inside a physics callback (a
		// spell's body_entered), where adding an Area2D is refused.
		var chest = new ChestReward
		{
			Position = ReachablePoint(at),
			MaterialiseSeconds = DropTable.CarriedChestRiseSeconds,
			Source = source
		};
		CallDeferred(Node.MethodName.AddChild, chest);
	}

	/// <summary>
	/// Whether a speed potion may drop now, and if so, spends the spacing (Enemy.TryDropBonusItem).
	/// Real seconds of play: the potion is a burst, and the spacing is how often one may be felt.
	/// </summary>
	public bool TryClaimSpeedPotion()
	{
		if (timeElapsed - lastSpeedPotionAt < DropTable.MinSecondsBetweenSpeedPotions)
			return false;
		lastSpeedPotionAt = timeElapsed;
		return true;
	}

	// The Hidden Drawer: a chest just claimed sometimes held another. It rises beside the player the
	// way a side event's chest does, and it can hold one in turn - a chain, but a short one (one in
	// four, then one in sixteen).
	private void TrySpawnNestedChest()
	{
		if (player == null || !IsInstanceValid(player) || bossFightActive || runFinished)
			return;
		if (spawnRng.Randf() >= player.GetNestedChestChance())
			return;
		AddChild(new ChestReward
		{
			Position = GetChestSpawnPositionAroundPlayer(),
			MaterialiseSeconds = DropTable.NestedChestRiseSeconds,
			Source = ChestSourceNested
		});
	}

	public void OpenChestSelectionMenu()
	{
		if (player == null || !IsInstanceValid(player) || runFinished)
			return;

		// Waits its turn behind a screen already up (see ResumeOrOpenNextMenu) instead of being spent.
		if (IsChestMenuOpen || IsLevelUpMenuOpen)
		{
			queuedChestOpens++;
			return;
		}

		var menu = new ChestItemSelectionMenu();
		chestSelectionMenu = menu;
		AddChild(menu);

		var ownedItems = player.GetOwnedChestItems();
		// Three, plus the Relic Key, plus John Dee's eye for a collection: five at most.
		int offerCount = player.ChestOfferCount;
		// The knife is only dealt when it has something to cut (Player.CanUsePenknife).
		var unavailable = player.CanUsePenknife ? null : new[] { ChestItemCatalog.ScribesPenknife };
		var options = ChestItemCatalog.GetChestItemOptions(ownedItems, spawnRng, offerCount, unavailable);
		// The knife's picker lists the book as it stands, the wizard's own spell greyed (it cannot be cut).
		menu.SetSpellBook(player.GetEquippedSpells(), player.SignatureSpellId);
		menu.SetOptions(options, ownedItems);
		menu.Connect(ChestItemSelectionMenu.SignalName.ItemSelected, Callable.From<string>(OnChestItemSelected));
		menu.Connect(ChestItemSelectionMenu.SignalName.SpellCutChosen, Callable.From<string>(OnPenknifeCutChosen));

		GetTree().Paused = true;
	}

	// The Scribe's Penknife is claimed by naming the spell it cuts, in the chest's own picker. The
	// chest then closes exactly as any other claim does.
	private void OnPenknifeCutChosen(string spellId)
	{
		if (player != null && IsInstanceValid(player))
		{
			// The peaks before the cut, for the same reason CloseLevelUpMenu samples them: the cut
			// lowers element counts, and a run that peaked first must still be credited with the peak.
			SampleElementPeaks();
			if (player.TryCutSpellWithPenknife(spellId))
			{
				GameStats.RecordRemovalUsed();
				RefreshElementHud();
			}
			else
			{
				GD.PrintErr($"Scribe's Penknife could not cut '{spellId}': the picker offered a spell the player cannot cut.");
			}
		}
		FinishChestClaim(ChestItemCatalog.ScribesPenknife);
	}

	private void OnChestItemSelected(string itemId)
	{
		if (player != null && IsInstanceValid(player))
			player.AddChestItem(itemId);
		FinishChestClaim(itemId);
	}

	// Everything a claim does after the relic itself: the run's records, the chest's heal, the
	// Reliquary's per-chest payout, and closing the screen.
	private void FinishChestClaim(string itemId)
	{
		if (player != null && IsInstanceValid(player))
		{
			RunEvents.RecordChestOpened();
			RunEvents.RecordChestItem(itemId);
			player.Heal(4);
			// Vaultguard's set bonus keys off a chest actually being opened, so it fires here
			// rather than once when the set completed. Called after AddChestItem so the chest
			// that finishes the set also pays out.
			player.OnChestOpened();
		}

		if (chestSelectionMenu != null && IsInstanceValid(chestSelectionMenu))
		{
			chestSelectionMenu.QueueFree();
			chestSelectionMenu = null;
		}

		// Same hazard as the level-up menu: the chest is picked up mid-run, often with enemies
		// already touching the player, so give the same grace period on the way out.
		if (player != null && IsInstanceValid(player))
			player.GrantInvincibility(player.PostMenuInvincibilitySeconds);

		// After the claim, so a Hidden Drawer taken from this very chest already counts.
		TrySpawnNestedChest();
		ResumeOrOpenNextMenu();
	}

	private Vector2 GetChestSpawnPositionAroundPlayer()
	{
		if (player == null || !IsInstanceValid(player))
			return stageOrigin;

		for (int i = 0; i < 12; i++)
		{
			float angle = spawnRng.Randf() * Mathf.Tau;
			float radius = spawnRng.RandfRange(90f, 220f);
			Vector2 candidate = ClampPositionToStageBounds(player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
			if (candidate.DistanceTo(player.GlobalPosition) < 80f)
				continue;
			if (IsImpassablePosition(candidate))
				continue;
			return candidate;
		}

		return ClampPositionToStageBounds(player.GlobalPosition + new Vector2(0f, -130f));
	}

	private int GetCurrentEliteEnemyCount()
	{
		int count = 0;
		foreach (Enemy enemy in EnemyRegistry.Snapshot.OfType<Enemy>())
		{
			if (IsInstanceValid(enemy) && enemy.IsMiniBoss)
				count++;
		}

		return count;
	}

	private int GetSpawnBatchCount()
	{
		float minutesElapsed = PaceMinutes;
		float roll = spawnRng.Randf();
		// Small waves for as long as the opening interval ramp is running. Lengthening the gap on
		// its own would only have spaced out the same three-enemy wall; the first minute needs to
		// arrive as ones and twos, which is also what makes the bigger bodies readable.
		if (minutesElapsed < 1.0f)
			return roll < 0.5f ? 2 : 1;
		if (minutesElapsed < 3.0f)
			return roll < 0.30f ? 3 : 2;
		if (minutesElapsed < 7.0f)
			return roll < 0.30f ? 4 : 3;
		if (minutesElapsed < 11.0f)
			return roll < 0.16f ? 3 : roll < 0.64f ? 2 : 1;

		return roll < 0.24f ? 3 : roll < 0.88f ? 2 : 4;
	}

	private Vector2 FindSeparatedSpawnPosition()
	{
		if (player != null)
		{
			Vector2 bestCandidate = player.GlobalPosition;
			float bestDistance = -1.0f;
			Vector2 bestNonWall = player.GlobalPosition;
			bool haveNonWall = false;
			bool bestNonWallIsPoor = true;
			// A maze rejects far more candidates than open ground does - walls, and corridors that
			// are a long walk round - so it gets more tries before settling for a fallback.
			int attempts = Math.Max(1, SpawnPositionRetries) * (MazeNavigation.Routing?.IsMaze == true ? 3 : 1);
			for (int i = 0; i < attempts; i++)
			{
				Vector2 candidate = GetRandomSpawnPositionAroundPlayer();
				bool onWall = IsImpassablePosition(candidate);
				bool poorPath = !onWall && IsPoorMazeSpawn(candidate);
				float nearestEnemyDistance = GetNearestEnemyDistance(candidate);
				if (!onWall && !poorPath && nearestEnemyDistance >= SpawnMinEnemySeparation)
					return candidate;

				if (!onWall && (!haveNonWall || (bestNonWallIsPoor && !poorPath)))
				{
					haveNonWall = true;
					bestNonWall = candidate;
					bestNonWallIsPoor = poorPath;
				}

				if (nearestEnemyDistance > bestDistance)
				{
					bestDistance = nearestEnemyDistance;
					bestCandidate = candidate;
				}
			}

			return haveNonWall ? bestNonWall : bestCandidate;
		}

		var screenSize = GetViewportRect().Size;
		return new Vector2((float)spawnRng.Randf() * screenSize.X, -50);
	}

	// True if the given world position is a maze wall tile (so enemies don't spawn trapped).
	/// <summary>
	/// True where nothing should be PLACED: a maze wall, or any terrain the tile manifest marks
	/// blocking - which today means water.
	/// </summary>
	/// <remarks>
	/// The water half is deliberately not gated on MazeNavigation the way the wall half is. A maze
	/// wall only exists on a maze stage; a lake can exist on any of them, and a spawner that only
	/// asked about walls would drop enemies into the middle of one and leave them shouldering
	/// their own collision body for the rest of the run.
	/// </remarks>
	private bool IsImpassablePosition(Vector2 position)
	{
		if (terrainStreamer == null)
			return false;

		// Asked of the streamer's chunk data, the same data the collision is painted from. A point on
		// ground nobody has loaded yet still gets a true answer: the streamer generates that chunk's
		// data (not its paint) rather than guessing, so nothing is placed on a lake that has not
		// been drawn yet.
		if (terrainStreamer.IsBlockedAtWorld(position))
			return true;
		// A garden fence is not terrain, but nothing may be put down inside its rails either.
		if (terrainStreamer.IsFenceAtWorld(position))
			return true;

		// "Impassable" also covers a pocket not connected to the player's: a maze corridor sealed off
		// inside the window, or an island in a lake. A placement there would be an enemy that can
		// never arrive or a key nobody can fetch, so it is asked of the navigation rather than assumed.
		// IsReachable is false outside the navigation window, which is the painted square round the
		// player - so nothing is placed beyond the ground that is actually there.
		MazeNavigation nav = MazeNavigation.Routing;
		return nav != null
			&& (terrainStreamer.IsWallAtWorld(position)
				|| !nav.IsReachable(position));
	}

	// How much longer than the straight line a fresh maze spawn may have to walk. The spawn ring sits
	// just off screen, and a point there can be the far side of one wall and twenty tiles of corridor
	// away: the enemy then spends its life in a corridor the player can see but never meets - the
	// Cursed Dungeon's "trapped in other corridors". Rejecting those points means everything that
	// spawns is a short walk from the player. Must stay well under Enemy.MazeRecyclePathFactor.
	[Export] public float MazeSpawnMaxDetour { get; set; } = 1.7f;
	[Export] public float MazeSpawnDetourSlackTiles { get; set; } = 4f;

	// Open stages too, since they have routing: a point on the far shore of a lake is the same
	// enemy-that-never-arrives as one in the next corridor.
	private bool IsPoorMazeSpawn(Vector2 position)
	{
		var nav = MazeNavigation.Routing;
		if (nav == null || player == null)
			return false;

		float path = nav.PathTilesAt(position);
		if (path < 0f)
			return true;

		float straight = position.DistanceTo(player.GlobalPosition) / nav.TileSize;
		return path > straight * MazeSpawnMaxDetour + MazeSpawnDetourSlackTiles;
	}

	private Vector2 GetRandomSpawnPositionAroundPlayer()
	{
		float angle = PickSpawnBearing();
		var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		float radius = GetSpawnRadiusForDirection(direction);
		return ClampPositionToStageBounds(player.GlobalPosition + (direction * radius));
	}

	/// <summary>A bearing to spawn on, weighted toward the way the player is travelling.</summary>
	private float PickSpawnBearing()
	{
		float travelSpeed = measuredPlayerVelocity.Length();
		float playerSpeed = player is Player typedPlayer ? Mathf.Max(1f, typedPlayer.Speed) : 220f;

		// Ease from uniform at a standstill to the full bias at running speed. Without this a
		// player who stops gets a ring biased toward whatever they were last doing, which is worse
		// than no bias at all - it leaves a permanent hole on one side.
		float committed = Mathf.Clamp(travelSpeed / playerSpeed, 0f, 1f);
		float exponent = Mathf.Lerp(1.0f, Mathf.Max(1.0f, SwarmForwardBias), committed);

		if (travelSpeed < 1f)
			return spawnRng.Randf() * Mathf.Pi * 2.0f;

		// Offset from the heading, in [-PI, PI]. Raising the magnitude to a power > 1 pulls the
		// distribution toward zero - toward straight ahead - while still leaving a real chance of
		// spawning behind, which is what keeps the player from being able to simply reverse.
		float u = spawnRng.RandfRange(-1f, 1f);
		float offset = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), exponent) * Mathf.Pi;
		return measuredPlayerVelocity.Angle() + offset;
	}

	/// <summary>
	/// How far along <paramref name="direction"/> the spawn ring sits: just past the edge of the
	/// visible rectangle on that bearing, plus a random depth.
	/// </summary>
	private float GetSpawnRadiusForDirection(Vector2 direction)
	{
		Vector2 half = GetVisibleHalfExtents();
		if (!SpawnUsesScreenEdge || half.X <= 1f || half.Y <= 1f)
			return spawnRng.RandfRange(SpawnMinDistance, SpawnMaxDistance);

		// Distance from the centre of the screen to its edge along this bearing: whichever of the
		// vertical and horizontal walls is crossed first.
		float absX = Mathf.Max(0.0001f, Mathf.Abs(direction.X));
		float absY = Mathf.Max(0.0001f, Mathf.Abs(direction.Y));
		float toEdge = Mathf.Min(half.X / absX, half.Y / absY);

		// Under swarm pressure the ring closes toward the screen edge (SwarmPressure.cs); at none, these
		// are SpawnEdgeMargin and SpawnEdgeDepth exactly.
		return (toEdge * SwarmEdgeMargin) + spawnRng.RandfRange(0f, SwarmEdgeDepth);
	}

	// Half the visible world rectangle, which is the viewport scaled by the gameplay zoom.
	private Vector2 GetVisibleHalfExtents()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		float zoom = 1f;
		var camera = player?.GetNodeOrNull<Camera2D>("Camera2D");
		if (camera != null && camera.Zoom.X > 0.01f)
			zoom = camera.Zoom.X;

		return viewport / (2f * zoom);
	}

	private float GetNearestEnemyDistance(Vector2 position)
	{
		if (SpawnMinEnemySeparation <= 0.0f)
			return float.MaxValue;

		float nearest = float.MaxValue;
		foreach (Node2D enemy in EnemyRegistry.Snapshot.OfType<Node2D>())
		{
			if (!IsInstanceValid(enemy))
				continue;

			nearest = Math.Min(nearest, position.DistanceTo(enemy.GlobalPosition));
		}

		return nearest;
	}

	private (PackedScene Scene, float HealthMultiplier, bool IsElite) SelectEnemyForCurrentStage()
	{
		float minutesElapsed = PaceMinutes;
		float roll = spawnRng.Randf();
		bool forceElite = ShouldSpawnElite();
		// The ghost used to be drawn here - 2.5% of spawns for the first five pace minutes, first in
		// every chapter, then in the Sketchbook only. It is a scheduled miniboss now
		// (TickGhostMiniBoss), so it is in no table at all. .ai/run-pacing.md section 7.

		// Every role below is a behaviour, and the chapter says which of its own creatures does it
		// (StageBestiary). A chapter with no creature for a role skips that roll entirely - the
		// Sketchbook has no ranged, turret, exploder or summoner - and draws no dice for it, so the
		// rolls it does make land exactly where they always did.

		// The back line. A role, not a biome, so every chapter has one, on a roll of its own rather
		// than off the shared one, so each chapter's table keeps the full 0..1 spread it was tuned
		// on. Below-average health, because the threat is reaching it, not chewing through it.
		if (bestiary.SceneFor(BestiaryRole.Ranged) is PackedScene ranged
			&& PaceSeconds >= HexerFirstSpawnSeconds && spawnRng.Randf() < HexerSpawnShare)
			return (ranged, 0.85f, forceElite);

		// The rooted turret. Also its own roll, for the same reason. Never an elite: the elite
		// treatment is more health, more speed and a bigger body, and on something that cannot move
		// that reads as a health sponge parked in the open rather than as a threat worth the fight.
		if (bestiary.SceneFor(BestiaryRole.Turret) is PackedScene turret
			&& PaceSeconds >= SentryFirstSpawnSeconds && spawnRng.Randf() < SentrySpawnShare)
			return (turret, 1.0f, false);

		// The charger. Slightly under-healthy, because its threat is the dash and a lunger that
		// also took a while to kill would just be a tank that occasionally moves fast. (Blobby in the
		// Sketchbook: the kids drew a squash-and-stretch sheet and a leap sheet for the same blob,
		// which is a wind-up and a dash.)
		if (bestiary.SceneFor(BestiaryRole.Charger) is PackedScene charger
			&& PaceSeconds >= LungerFirstSpawnSeconds && spawnRng.Randf() < LungerSpawnShare)
			return (charger, 0.9f, forceElite);

		// Half health: the whole enemy is the question "can you kill it before it reaches you", and
		// the answer has to be yes often enough that trying is the right instinct.
		if (bestiary.SceneFor(BestiaryRole.Exploder) is PackedScene exploder
			&& PaceSeconds >= ExploderFirstSpawnSeconds && spawnRng.Randf() < ExploderSpawnShare)
			return (exploder, 0.5f, forceElite);

		// Tanky, because it is meant to still be standing when the slam lands - the fight it wants
		// is one the player chooses to leave rather than one they burst down on the spot. (Pushy
		// Dude in the Sketchbook, a cone rather than a ring: it shoves the way it is facing.)
		if (bestiary.SceneFor(BestiaryRole.Slammer) is PackedScene slammer
			&& PaceSeconds >= SlammerFirstSpawnSeconds && spawnRng.Randf() < SlammerSpawnShare)
			return (slammer, 1.6f, forceElite);

		// Never an elite, for the Sentry's reason turned around: an elite summoner is not a better
		// fight, it is the same fight with more health in front of the thing making it worse.
		if (bestiary.SceneFor(BestiaryRole.Summoner) is PackedScene summoner
			&& PaceSeconds >= SummonerFirstSpawnSeconds && spawnRng.Randf() < SummonerSpawnShare)
			return (summoner, 0.9f, false);

		// The chapter's own table: its bodies, their shares and health multipliers, carried over
		// band for band from the environment switch that used to sit here (StageBestiary.<Chapter>.cs).
		var pick = bestiary.PickFromTable(roll, minutesElapsed);
		return (pick.Scene, pick.HealthMultiplier, forceElite);
	}

	/// <summary>The roster for the chapter being played, with its scenes loaded for the run.</summary>
	private static LoadedBestiary LoadBestiaryForStage()
	{
		ChapterBestiary? chapter = StageBestiary.ForStageIndex(Global.SelectedStageIdx);
		if (chapter == null)
		{
			// Every chapter in StageCatalog has a roster and ValidateBestiary says so at startup, so
			// this is a new chapter added without one: loud, and the crypt's cast rather than an empty field.
			GD.PushError($"Node2DGame: stage {Global.SelectedStageIdx} has no bestiary (StageBestiary.All); spawning the Cursed Dungeon's cast.");
			chapter = StageBestiary.ForStageId("stage_1")!;
		}
		return LoadedBestiary.Load(chapter);
	}

	private bool ShouldSpawnElite()
	{
		if (PaceSeconds < EliteStartTimeSeconds)
			return false;

		// nextEliteSpawnTime is on the pace clock; spawnInterval is real seconds.
		if (PaceSeconds + spawnInterval / RunPace < nextEliteSpawnTime)
			return false;

		nextEliteSpawnTime += GetEliteIntervalSeconds();
		return true;
	}

	private float GetEliteIntervalSeconds()
	{
		float minutesElapsed = PaceMinutes;
		float interval;
		if (minutesElapsed >= 12.0f)
			interval = 30.0f;
		else if (minutesElapsed >= 8.0f)
			interval = 36.0f;
		else if (minutesElapsed >= 5.0f)
			interval = 44.0f;
		else
			interval = 54.0f;

		return interval * presetEliteIntervalScale;
	}

	private void ApplyBalancePresetFromSave()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		string preset = GlobalStatsManager.NormalizeBalancePresetId(saveManager?.Data.BalancePresetId);
		if (saveManager != null)
			saveManager.Data.BalancePresetId = preset;

		switch (preset)
		{
			case GlobalStatsManager.BalancePresetCasual:
				presetSpawnIntervalScale = 1.14f;
				presetSpawnHealthScale = 0.88f;
				presetEliteIntervalScale = 1.18f;
				presetElitePowerScale = 0.86f;
				presetArcaneRewardScale = GlobalStatsManager.GetArcaneRewardScaleForPreset(preset);
				MaxEliteEnemiesAlive = Math.Max(1, MaxEliteEnemiesAlive - 1);
				MaxSpawnsPerBurstWindow = Mathf.RoundToInt(MaxSpawnsPerBurstWindow * 0.84f);
				break;
			case GlobalStatsManager.BalancePresetHardcore:
				presetSpawnIntervalScale = 0.88f;
				presetSpawnHealthScale = 1.18f;
				presetEliteIntervalScale = 0.84f;
				presetElitePowerScale = 1.18f;
				presetArcaneRewardScale = GlobalStatsManager.GetArcaneRewardScaleForPreset(preset);
				MaxEliteEnemiesAlive += 1;
				MaxSpawnsPerBurstWindow = Mathf.RoundToInt(MaxSpawnsPerBurstWindow * 1.15f);
				break;
			default:
				presetSpawnIntervalScale = 1.0f;
				presetSpawnHealthScale = 1.0f;
				presetEliteIntervalScale = 1.0f;
				presetElitePowerScale = 1.0f;
				presetArcaneRewardScale = GlobalStatsManager.GetArcaneRewardScaleForPreset(preset);
				break;
		}

		GD.Print($"Balance preset: {GlobalStatsManager.GetBalancePresetDisplayName(preset)} | SpawnIntervalScale={presetSpawnIntervalScale:0.00} | SpawnHealthScale={presetSpawnHealthScale:0.00} | EliteIntervalScale={presetEliteIntervalScale:0.00} | ElitePowerScale={presetElitePowerScale:0.00} | ArcaneRewardScale={presetArcaneRewardScale:0.00}");
	}

	private void ClampPlayerToStageBounds()
	{
		if (player == null || !ConstrainPlayerToStageBounds)
			return;

		player.GlobalPosition = ClampPositionToStageBounds(player.GlobalPosition);
	}

	private Vector2 ClampPositionToStageBounds(Vector2 position)
	{
		if (!ConstrainPlayerToStageBounds)
			return position;

		// A procedural stage is endless (TerrainStreamer): there is no edge to clamp to. Callers that
		// PLACE things still reject water, walls and unconnected maze pockets through
		// IsImpassablePosition; this only ever kept them inside a square that no longer exists.
		if (proceduralActive)
			return position;

		return StageEnvironmentCatalog.GetForStageIndex(Global.SelectedStageIdx).Kind switch
		{
			StageEnvironmentKind.Forest => new Vector2(position.X, Mathf.Clamp(position.Y, stageOrigin.Y - ForestHalfHeight, stageOrigin.Y + ForestHalfHeight)),
			StageEnvironmentKind.Castle => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - CastleHalfWidth, stageOrigin.X + CastleHalfWidth),
				Mathf.Clamp(position.Y, stageOrigin.Y - CastleHalfHeight, stageOrigin.Y + CastleHalfHeight)),
			StageEnvironmentKind.Ruins => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - RuinsHalfSize, stageOrigin.X + RuinsHalfSize),
				Mathf.Clamp(position.Y, stageOrigin.Y - RuinsHalfSize, stageOrigin.Y + RuinsHalfSize)),
			StageEnvironmentKind.Swamp => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - RuinsHalfSize * 0.86f, stageOrigin.X + RuinsHalfSize * 0.86f),
				Mathf.Clamp(position.Y, stageOrigin.Y - RuinsHalfSize * 0.74f, stageOrigin.Y + RuinsHalfSize * 0.74f)),
			StageEnvironmentKind.Ice => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - CastleHalfWidth * 0.72f, stageOrigin.X + CastleHalfWidth * 0.72f),
				Mathf.Clamp(position.Y, stageOrigin.Y - CastleHalfHeight * 0.62f, stageOrigin.Y + CastleHalfHeight * 0.62f)),
			StageEnvironmentKind.Desert => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - RuinsHalfSize * 0.92f, stageOrigin.X + RuinsHalfSize * 0.92f),
				Mathf.Clamp(position.Y, stageOrigin.Y - RuinsHalfSize * 0.80f, stageOrigin.Y + RuinsHalfSize * 0.80f)),
			StageEnvironmentKind.Volcanic => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - CastleHalfWidth * 0.86f, stageOrigin.X + CastleHalfWidth * 0.86f),
				Mathf.Clamp(position.Y, stageOrigin.Y - CastleHalfHeight * 0.70f, stageOrigin.Y + CastleHalfHeight * 0.70f)),
			_ => position
		};
	}

	private void OnPlayerDied()
	{
		// The bar has to say 0 on the frame the run ends. It is refreshed from _Process, and death
		// ends the run (FinishRunAndReward sets runFinished and pauses the tree) inside the very
		// TakeDamage that killed - a contact or bolt hit in the physics step, before this frame's
		// _Process - so _Process never ran again and the HUD froze on the health BEFORE the killing
		// blow. In a swarm that blow is usually a one-damage touch (armour floors a hit at 1), which
		// is why a player who died read "1 / 100" on the game over screen. Reported from the Pixel 9.
		UpdatePlayerHealthHud();

		// The boss is already dead and the victory screen is one timer away. Dying in that gap is
		// not a loss, so the pending win stands.
		if (bossVictoryPending)
			return;

		FinishRunAndReward(RunOutcome.Defeat);
	}

	// A Full Set Enchantment assembled (issue #43). Built on the first one, since most runs complete
	// none; it holds any that complete together and plays them in turn.
	private SetAssembledMoment? setAssembledMoment;

	private void OnChestSetCompleted(string setId)
	{
		ChestSetDefinition? set = ChestItemCatalog.Sets.FirstOrDefault(s => s.Id == setId);
		if (set == null)
		{
			GD.PrintErr($"Node2DGame: a set completed that the catalog does not know: '{setId}'");
			return;
		}
		if (setAssembledMoment == null || !IsInstanceValid(setAssembledMoment))
		{
			setAssembledMoment = new SetAssembledMoment { Name = "SetAssembledMoment" };
			AddChild(setAssembledMoment);
		}
		setAssembledMoment.Play(set);
	}

	private void OnPlayerDamageTaken(int amount)
	{
		GameStats.RecordDamageTaken(amount);
		PlayPlayerDamageFlash();

		if (amount > 0)
		{
			// Recorded as a time rather than a bool, so "survive N minutes untouched" works for
			// any N instead of only the single five-minute question the old flag could answer.
			RunEvents.RecordFirstDamage(timeElapsed);
			if (timeElapsed <= 300f)
				tookDamageBeforeFiveMinutes = true;
		}
	}

	private void PlayPlayerDamageFlash()
	{
		if (damageFlashOverlay == null)
			return;

		damageFlashTween?.Kill();
		damageFlashOverlay.Color = new Color(1f, 0.25f, 0.2f, 0.3f);
		damageFlashTween = CreateTween();
		damageFlashTween.TweenProperty(damageFlashOverlay, "color", new Color(1f, 0.2f, 0.2f, 0f), 0.16f);
	}

	private void UpdateLowHealthWarning()
	{
		if (player == null || lowHealthOverlay == null || player.MaxHP <= 0)
			return;

		float hpRatio = Mathf.Clamp((float)player.CurrentHP / player.MaxHP, 0f, 1f);
		if (hpRatio > 0.35f)
		{
			lowHealthOverlay.Color = new Color(0.8f, 0.05f, 0.05f, 0f);
			return;
		}

		float pulse = (Mathf.Sin(timeElapsed * 6.0f) + 1.0f) * 0.5f;
		float alpha = Mathf.Lerp(0.05f, 0.22f, pulse) * Mathf.Clamp((0.35f - hpRatio) / 0.35f, 0.25f, 1.0f);
		lowHealthOverlay.Color = new Color(0.8f, 0.05f, 0.05f, alpha);
	}

	public void FinishRunAndReward(RunOutcome outcome = RunOutcome.Defeat, string bossId = "")
	{
		if (runFinished)
			return;

		runFinished = true;
		// Win or lose, the run's music fades under the result. The title brings the menu theme back.
		runMusic?.FadeOutAll(MusicPlayer.EndFadeSeconds);
		RunResult result = BuildRunResult(outcome, bossId);
		GameStats.ApplyTelemetryToRunResult(result);
		// Last sample of the loadout: a run that ends mid-level-up would otherwise miss whatever
		// the player was holding at the end.
		RunEvents.RecordElementCounts(BuildElementCountSnapshot());
		RunEvents.ApplyToRunResult(result, timeElapsed);

		int reward = CalculateArcaneReward(result);
		int totalCurrency = AwardArcaneEnergy(reward);
		WritePlaytestRunLog(result, reward, totalCurrency);
		GameStats.RecordRunResult(result);
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null)
		{
			saveManager.Data.RecordRunTelemetry(result);
			saveManager.Data.HasSeenGameplayOnboarding = true;
			runUnlocks = AchievementManager.ApplyRunAchievements(saveManager.Data, result);
			runUnlocks.SpellNames.AddRange(runDiscoveredSpells);
			runUnlocks.WizardNames.AddRange(runDiscoveredWizards);
			// Found in the world first - they happened before the run ended - then what the run's
			// totals earned, then what the boss opened, then the paths the run revealed.
			runUnlocks.Entries.InsertRange(0, runDiscoveryEntries);
			ApplyBossVictoryUnlocks(saveManager.Data, result, runUnlocks);
			runUnlocks.Entries.AddRange(runRevealedAscensions);
			RecordBossRush(saveManager.Data, outcome);
			saveManager.SaveGame();
		}
		ShowGameOver(result, reward, totalCurrency);
		GetTree().Paused = true;
	}

	// Beating a boss opens the next stage. The spell unlock that comes with it is handled by
	// AchievementDefinitions ("forest_cleared" and friends), which already match on BossId - this
	// only has to cover the stage gate, which nothing wrote to before now.
	//
	// WHICH stage is the campaign order's to say (StageCatalog.ChapterOpenedBy), not the boss's: it
	// used to be a field on each BossDefinition, and moving the Cursed Dungeon would have meant
	// rewriting four of them in step. The chapter is the boss's own, so a victory recorded against
	// a boss from another chapter (a stand-in fight in a chapter still being built) opens what its
	// own chapter opens.
	private static void ApplyBossVictoryUnlocks(SaveData data, RunResult result, RunUnlockSummary summary)
	{
		if (data == null || result == null || result.Outcome != RunOutcome.Victory)
			return;

		BossDefinition defeatedBoss = BossCatalog.GetById(result.BossId);
		if (defeatedBoss == null)
			return;

		// Only a chapter this victory actually opened is a reward; beating the same boss again
		// opens nothing, and saying "Unlocked" every time was the old screen's one untrue line.
		StageDefinition opened = BossCatalog.ChapterOpenedBy(defeatedBoss);
		if (opened != null && GlobalStatsManager.UnlockStage(data, opened.Id))
		{
			summary?.Entries.Add(new RunUnlockEntry
			{
				Kind = RunUnlockKind.Chapter,
				Id = opened.Id,
				Name = opened.DisplayName,
				Source = $"Defeated {defeatedBoss.DisplayName}",
			});
		}
	}

	private RunResult BuildRunResult(RunOutcome outcome, string bossId)
	{
		string presetId = GlobalStatsManager.BalancePresetDefault;
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null)
			presetId = GlobalStatsManager.NormalizeBalancePresetId(saveManager.Data.BalancePresetId);

		return new RunResult
		{
			Outcome = outcome,
			BalancePresetId = presetId,
			// From the catalog rather than formatted from the index: the id is what saves and unlocks
			// match on, and the catalog is the one place that says which id an index is.
			StageId = StageCatalog.GetByIndex(Global.SelectedStageIdx)?.Id ?? $"stage_{Global.SelectedStageIdx}",
			FinalPlayerLevel = player?.CurrentLevel ?? 1,
			TimeSurvived = timeElapsed,
			EnemiesKilled = CalculateKillsEstimate(),
			// A Boss Rush records no boss: its victory is over the whole roster, not a chapter's warden,
			// so it opens no chapter, grants no chapter deed and pays no single boss's bonus
			// (BossRushCatalog). Its own records are written by RecordBossRush.
			BossId = IsBossRush ? string.Empty : bossId ?? string.Empty,
			TookDamageBeforeFiveMinutes = tookDamageBeforeFiveMinutes,
			ElementCounts = BuildElementCountSnapshot(),
			EquippedSpells = BuildSpellSnapshot(),
			CharacterId = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx)?.Id ?? string.Empty
		};
	}

	private void UpdateOnboardingTips(float deltaSeconds)
	{
		if (!showOnboardingTips || onboardingTipLabel == null)
			return;

		// A tip waits while a banner is up (ShowRunBanner): the two share the band under the timer,
		// and the chapter's opening line or a boss's name is the one to read. A tip cut short by one
		// is shown again once it has gone.
		if (stageIntroLabel?.GetParent() is Control bannerPanel && bannerPanel.Visible)
		{
			if (onboardingTipLabel.Visible)
			{
				onboardingTipLabel.Visible = false;
				onboardingTipIndex = Math.Max(0, onboardingTipIndex - 1);
			}
			return;
		}

		onboardingTipTimer += deltaSeconds;
		if (onboardingTipLabel.Visible && onboardingTipTimer >= 7.5f)
		{
			onboardingTipLabel.Visible = false;
			onboardingTipTimer = 0f;
		}

		if (onboardingTipIndex >= OnboardingTips.Length)
			return;

		if (!onboardingTipLabel.Visible && timeElapsed >= OnboardingTips[onboardingTipIndex].Time)
			ShowNextOnboardingTip();
	}

	private void ShowNextOnboardingTip(bool force = false)
	{
		if (onboardingTipLabel == null)
			return;

		if (onboardingTipIndex >= OnboardingTips.Length)
		{
			showOnboardingTips = false;
			onboardingTipLabel.Visible = false;
			return;
		}

		if (!force && timeElapsed < OnboardingTips[onboardingTipIndex].Time)
			return;

		onboardingTipLabel.Text = OnboardingTips[onboardingTipIndex].Text;
		onboardingTipLabel.Visible = true;
		onboardingTipTimer = 0f;
		onboardingTipIndex++;
	}

	private Dictionary<string, int> BuildElementCountSnapshot()
	{
		if (player == null)
			return new Dictionary<string, int>();

		return player.GetElementInstanceCounts().ToDictionary(p => p.Key.ToString(), p => p.Value);
	}

	private List<RunSpellSnapshot> BuildSpellSnapshot()
	{
		if (player == null)
			return new List<RunSpellSnapshot>();

		return player.GetEquippedSpells()
			.Where(s => s != null)
			.Select(s => new RunSpellSnapshot
			{
				Id = s.Id,
				DisplayName = s.Name,
				Level = s.CurrentLevel,
				IsLegendary = s.IsLegendary,
				MaxLevel = s.MaxLevel,
				IsPassive = s.IsPassive,
				IconPath = s.Icon?.ResourcePath ?? string.Empty,
				// GetElementWeights walks every active milestone (CollectActive), so a mutation's
				// or an Apotheosis's tags are in it.
				Elements = s.GetElementWeights()
					.Where(p => p.Value > 0)
					.OrderByDescending(p => p.Value).ThenBy(p => p.Key.ToString())
					.Select(p => (p.Key.ToString(), p.Value)).ToList(),
				MutationName = s.SelectedLevel4Evolution?.DisplayName ?? string.Empty,
				AscensionName = s.SelectedLevel8Evolution?.DisplayName ?? string.Empty,
				ApotheosisName = s.SelectedApotheosis?.DisplayName ?? string.Empty,
			})
			.ToList();
	}

	private int CalculateKillsEstimate()
	{
		int activeEnemies = EnemyRegistry.Count;
		return Math.Max(0, totalEnemiesSpawned - activeEnemies);
	}

	private int CalculateArcaneReward(RunResult result)
	{
		int minutesSurvived = Mathf.FloorToInt(timeElapsed / 60.0f);
		int playerLevel = player?.CurrentLevel ?? 1;
		int baseReward = BaseArcaneReward + (minutesSurvived * ArcanePerMinuteSurvived) + (playerLevel * ArcanePerPlayerLevel);
		// The results screen shows the payout part by part. Each part is RECORDED here as the
		// arithmetic below uses it, never recomputed elsewhere, so the breakdown cannot disagree
		// with what was paid.
		var breakdown = new ArcaneRewardBreakdown
		{
			BaseFlat = BaseArcaneReward,
			Minutes = minutesSurvived,
			PerMinute = ArcanePerMinuteSurvived,
			Level = playerLevel,
			PerLevel = ArcanePerPlayerLevel,
			BaseTotal = baseReward,
		};
		lastArcaneBreakdown = breakdown;

		float rewardMultiplier = 1.0f;
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null && saveManager.Data.ArcaneUpgradeLevels.TryGetValue("arcane_resonance", out int resonanceLevel))
		{
			rewardMultiplier += resonanceLevel * 0.10f;
		}

		if (saveManager != null && saveManager.Data.ArcaneUpgradeLevels.TryGetValue("greed", out int greedLevel))
		{
			rewardMultiplier += greedLevel * 0.10f;
		}
		breakdown.UpgradeMultiplier = rewardMultiplier;

		if (saveManager != null)
		{
			float dynamicMultiplier = GlobalStatsManager.GetDynamicArcaneRewardMultiplier(saveManager.Data, result, out string dynamicBreakdown,
				breakdown.DynamicParts);
			rewardMultiplier *= dynamicMultiplier;
			rewardMultiplier *= presetArcaneRewardScale;
			result.ArcaneRewardMultiplier = rewardMultiplier;
			result.ArcaneRewardBreakdown = $"{dynamicBreakdown}, Preset x{presetArcaneRewardScale:0.00}";
			breakdown.DynamicMultiplier = dynamicMultiplier;
			breakdown.PresetScale = presetArcaneRewardScale;
		}

		int computed = Mathf.RoundToInt(baseReward * rewardMultiplier);

		// The boss bonus is added after the multipliers rather than inside them: it is the flat
		// payout for clearing the level, and it should not swing with a pity streak or a preset.
		BossDefinition defeatedBoss = BossCatalog.GetById(result.BossId);
		if (result.Outcome == RunOutcome.Victory && defeatedBoss != null)
		{
			computed += defeatedBoss.ArcaneVictoryBonus;
			breakdown.BossBonus = defeatedBoss.ArcaneVictoryBonus;
			breakdown.BossName = defeatedBoss.DisplayName;
		}
		// A Boss Rush pays for every boss it felled, won or lost: a share of each one's own bonus
		// (BossRushCatalog.ArcaneBonusShare), outside the multipliers for the same reason.
		if (IsBossRush && bossRushArcane > 0)
		{
			computed += bossRushArcane;
			breakdown.BossBonus = bossRushArcane;
			breakdown.BossName = $"{bossRushFelled} of {BossRushRosterCount} bosses";
		}

		return Math.Max(1, computed);
	}

	// How CalculateArcaneReward counted the run's payout, for the results screen.
	private ArcaneRewardBreakdown lastArcaneBreakdown;

	private int AwardArcaneEnergy(int amount)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
		{
			GD.PrintErr("SaveManager autoload not found. Arcane Energy reward was not persisted.");
			return 0;
		}

		saveManager.Data.TotalCurrency += amount;
		saveManager.SaveGame();
		return saveManager.Data.TotalCurrency;
	}

	// What this run earned, handed to the game over screen. Held as a field rather than threaded
	// through ShowGameOver's signature because it is written inside a null-guarded save block.
	private RunUnlockSummary runUnlocks;

	private void ShowGameOver(RunResult result, int reward, int totalCurrency)
	{
		if (gameOverScene == null)
		{
			GD.PrintErr("GameOverScreen scene could not be loaded.");
			return;
		}

		var overlay = gameOverScene.Instantiate<CanvasLayer>();
		AddChild(overlay);

		if (overlay is GameOverScreen gameOver)
			gameOver.Present(BuildRunReport(result, reward, totalCurrency));
	}

	// The whole run for the results screen (RunReport). Everything in it was already paid and saved
	// by FinishRunAndReward; this only gathers it up.
	private RunReport BuildRunReport(RunResult result, int reward, int totalCurrency)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		CharacterData character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		StageDefinition stage = StageCatalog.GetByIndex(Global.SelectedStageIdx);
		RunUnlockSummary unlocks = runUnlocks ?? new RunUnlockSummary();
		ArcaneRewardBreakdown arcane = lastArcaneBreakdown ?? new ArcaneRewardBreakdown { BaseTotal = reward };
		arcane.UnlockBonus = unlocks.CurrencyAwarded;

		return new RunReport
		{
			Result = result,
			CharacterId = character?.Id ?? string.Empty,
			CharacterName = character?.Name ?? string.Empty,
			StageIndex = Global.SelectedStageIdx,
			StageName = stage?.DisplayName ?? StageCatalog.DisplayNameFor(result.StageId),
			BossName = BossCatalog.ForStageIndex(Global.SelectedStageIdx)?.DisplayName ?? string.Empty,
			BossReached = bossPhaseTriggered || result.Outcome == RunOutcome.Victory,
			BossArrivalSeconds = TimerVictorySeconds,
			BossRushFelled = bossRushFelled,
			BossRushTotal = BossRushRosterCount,
			BossRushSeconds = IsBossRush ? BossRushTotalSeconds : 0f,
			ArcaneEarned = reward,
			// totalCurrency is the purse straight after the run's payout, before the Unlocks entries
			// paid theirs (AchievementManager adds to the same purse a moment later), so the old
			// screen's "Total" was short by exactly the bonus it listed above it. The save is the truth.
			ArcanePurse = saveManager?.Data?.TotalCurrency ?? totalCurrency + unlocks.CurrencyAwarded,
			Arcane = arcane,
			Unlocks = unlocks,
			SpellIcons = (player?.GetEquippedSpells() ?? Array.Empty<SpellData>())
				.Where(s => s != null && s.Icon != null)
				.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First().Icon, StringComparer.OrdinalIgnoreCase),
			BoonIds = player?.GetOwnedBoonIds()?.ToList() ?? new List<string>(),
			RelicIds = player?.GetOwnedChestItems()?.ToList() ?? new List<string>(),
			DamageBySource = GameStats.DamageBySource
				.Where(p => p.Value > 0)
				.OrderByDescending(p => p.Value)
				.Select(p => (p.Key, p.Value))
				.ToList(),
			XpGathered = player?.RunXpGathered ?? 0,
			PlaytestMode = saveManager?.Data?.PlaytestModeEnabled == true,
		};
	}

	private void UpdateDebugOverlay(float deltaSeconds)
	{
		if (debugOverlayLabel == null)
			return;

		debugOverlayLabel.Visible = debugOverlayVisible;
		if (!debugOverlayVisible)
			return;

		debugOverlayUpdateTimer += deltaSeconds;
		if (debugOverlayUpdateTimer < 0.2f)
			return;
		debugOverlayUpdateTimer = 0f;

		float eliteIn = Math.Max(0f, (nextEliteSpawnTime - PaceSeconds) * RunPace);
		int currentEnemies = EnemyRegistry.Count;
		int eliteCount = GetCurrentEliteEnemyCount();
		string presetName = GlobalStatsManager.GetBalancePresetDisplayName(GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data.BalancePresetId);
		debugOverlayLabel.Text =
			$"Debug (F3)\n" +
			$"Time {FormatTime(timeElapsed)} | Preset {presetName}\n" +
			$"SpawnInterval {spawnInterval:0.00}s | SpawnHealth {spawnHealth:0} | BatchWindow {spawnCountInBurstWindow}/{MaxSpawnsPerBurstWindow}\n" +
			$"Elites {eliteCount}/{MaxEliteEnemiesAlive} | NextEliteIn {eliteIn:0.0}s | SpawnGrace {spawnGraceRemaining:0.0}s\n" +
			$"Scales SI {presetSpawnIntervalScale:0.00} SH {presetSpawnHealthScale:0.00} EI {presetEliteIntervalScale:0.00} EP {presetElitePowerScale:0.00} AR {presetArcaneRewardScale:0.00}\n" +
			$"Enemies {currentEnemies}/{MaxEnemies} | TotalSpawned {totalEnemiesSpawned}\n" +
			SwarmDebugLine();
	}

	private void WritePlaytestRunLog(RunResult result, int reward, int totalCurrency)
	{
		if (result == null)
			return;

		string logsDir = ProjectSettings.GlobalizePath("user://playtest_logs");
		DirAccess.MakeDirRecursiveAbsolute(logsDir);

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		string preset = GlobalStatsManager.GetBalancePresetDisplayName(saveManager?.Data.BalancePresetId);
		string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
		string safeOutcome = result.Outcome.ToString().ToLowerInvariant();
		string filename = $"run_{timestamp}_{safeOutcome}.txt";
		string path = Path.Combine(logsDir, filename);

		var sb = new StringBuilder();
		sb.AppendLine($"TimestampUTC: {DateTime.UtcNow:O}");
		sb.AppendLine($"Preset: {preset}");
		sb.AppendLine($"Outcome: {result.Outcome}");
		sb.AppendLine($"Stage: {result.StageId}");
		sb.AppendLine($"TimeSurvived: {result.TimeSurvived:0.0}s");
		sb.AppendLine($"FinalLevel: {result.FinalPlayerLevel}");
		sb.AppendLine($"EnemiesKilled: {result.EnemiesKilled}");
		sb.AppendLine($"DamageDealt: {result.TotalDamageDealt}");
		sb.AppendLine($"DamageTaken: {result.TotalDamageTaken}");
		sb.AppendLine($"HitsTaken: {result.HitsTaken}");
		sb.AppendLine($"Rerolls/Swaps/Removals/Skips: {result.RerollsUsed}/{result.SwapsUsed}/{result.RemovalsUsed}/{result.SkipsUsed}");
		sb.AppendLine($"ArcaneReward: +{reward}");
		sb.AppendLine($"ArcaneTotalAfterRun: {totalCurrency}");
		sb.AppendLine($"RewardMultiplier: {result.ArcaneRewardMultiplier:0.000}");
		sb.AppendLine($"RewardBreakdown: {result.ArcaneRewardBreakdown}");

		if (result.EquippedSpells != null && result.EquippedSpells.Count > 0)
		{
			sb.AppendLine("Loadout:");
			foreach (RunSpellSnapshot spell in result.EquippedSpells)
				sb.AppendLine($"- {spell.DisplayName} ({spell.Id}) Lv{spell.Level}{(spell.IsLegendary ? " Legendary" : string.Empty)}");
		}

		if (result.SpellPickCounts != null && result.SpellPickCounts.Count > 0)
		{
			sb.AppendLine("SpellPicks:");
			foreach (var pair in result.SpellPickCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
				sb.AppendLine($"- {pair.Key}: {pair.Value}");
		}

		if (result.SpellUpgradeCounts != null && result.SpellUpgradeCounts.Count > 0)
		{
			sb.AppendLine("SpellUpgrades:");
			foreach (var pair in result.SpellUpgradeCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
				sb.AppendLine($"- {pair.Key}: {pair.Value}");
		}

		using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PushWarning($"Playtest log could not be written: {path}");
			return;
		}

		file.StoreString(sb.ToString());
	}

	/// <summary>
	/// Recycles an enemy the player has walked away from back to the spawn ring.
	/// </summary>
	/// <remarks>
	/// Called from <see cref="Enemy"/> past its RespawnDistance. Until the method name was
	/// corrected this never ran at all, so a run accumulated a long tail of enemies trailing across
	/// the arena: they held entity budget, contributed nothing, and the pressure the spawner thought
	/// it was applying was spread over enemies the player would never meet.
	///
	/// Rate-capped because the trigger is per-enemy and undirected. A player who sprints in one
	/// direction crosses the threshold for dozens of enemies within a second or two, and moving the
	/// whole tail at once turns a quiet moment into an ambush out of nowhere.
	/// </remarks>
	public void RespawnEnemy(Node enemy)
	{
		if (player == null) return;

		if (timeElapsed - recycleWindowStart >= 1.0f)
		{
			recycleWindowStart = timeElapsed;
			recyclesThisWindow = 0;
		}

		// Under swarm pressure the ceiling rises, and the body comes back on the player's path
		// (SwarmPressure.cs); with none, both are exactly as they were.
		if (recyclesThisWindow >= SwarmRecycleAllowance)
			return;

		recyclesThisWindow++;

		var newPos = FindRecycleSpawnPosition();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.ResetForRespawn(newPos, SwarmRecycleHealth(typedEnemy));
			// Rescaled from the scene's original number, not from the current one, so a body that
			// has been recycled a dozen times does not drift. Elites never reach here - they are
			// exempt from recycling - so the elite speed bonus cannot be lost this way.
			if (typedEnemy.SpawnBaseSpeed > 0f)
				typedEnemy.Speed = typedEnemy.SpawnBaseSpeed * GetEnemyPaceMultiplier();
		}
		else if (enemy is Node2D n)
		{
			n.Position = newPos;
		}
	}

	// The ordinary spawn ring starts at SpawnMinDistance, which is inside the visible area, so a
	// recycled enemy could pop into existence in plain sight - far worse than a fresh spawn doing
	// it, because the player watched that same enemy vanish from behind them. Retries for a
	// placement outside the screen and only settles for a near one if the arena gives it no choice.
	private Vector2 FindRecycleSpawnPosition()
	{
		float offScreenRadius = GetVisibleRadius() * 1.1f;
		Vector2 fallback = FindSeparatedSpawnPosition();
		for (int i = 0; i < 5; i++)
		{
			Vector2 candidate = i == 0 ? fallback : FindSeparatedSpawnPosition();
			if (player!.GlobalPosition.DistanceTo(candidate) >= offScreenRadius)
				return candidate;
		}

		return fallback;
	}

	// Half the diagonal of what the camera actually shows - the worst case "still on screen"
	// distance, used by the recycler to avoid putting an enemy back in plain sight.
	private float GetVisibleRadius() => GetVisibleHalfExtents().Length();
}
