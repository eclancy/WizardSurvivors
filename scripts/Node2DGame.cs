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
	[Export] public float TimerVictorySeconds { get; set; } = 900.0f;
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
	private const float EscapePanelTopMargin = 58f;
	private const float EscapePanelBottomMargin = 30f;
	[Export] public float SpawnBurstWindowSeconds { get; set; } = 10f;
	[Export] public int MaxSpawnsPerBurstWindow { get; set; } = 35;
	[Export] public float ChestSpawnIntervalSeconds { get; set; } = 45.0f;
	[Export] public float FirstChestSpawnDelaySeconds { get; set; } = 20.0f;
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
	[Export] public int ProceduralGridRadius { get; set; } = 90;
	[Export] public float ProceduralSafeRadiusTiles { get; set; } = 6.0f;
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
	private float proceduralArenaHalfExtent;
	private bool proceduralActive;
	private CanvasLayer? levelUpMenu;
	private CanvasLayer? escapeMenu;
	private Control? escapeRoot;
	private RichTextLabel? escapeDetailText;
	private ScrollContainer? escapeDetailScroll;
	private VBoxContainer? escapeDetailSections;
	private Label? escapeDetailTitle;
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
	private float chestSpawnTimer = 0f;
	private float wardenMiniBossTimer = 0f;
	private bool initialChestSpawned = false;
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
	private PackedScene enemyScene = ResourceLoader.Load<PackedScene>("res://scenes/enemy.tscn");
	private PackedScene booEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/BooEnemy.tscn");
	private PackedScene fastEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/FastEnemy.tscn");
	private PackedScene slowEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/SlowEnemy.tscn");
	private PackedScene tankEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/TankEnemy.tscn");
	private PackedScene hexerEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/HexerEnemy.tscn");

	// THE FIRST BIOME FAMILY. Every stage used to draw from one pool of grey-green humanoids, so
	// the Enchanted Forest and the Cursed Dungeon fought the same enemies - and seven of those ten
	// share a silhouette. These three are the forest's own: a quadruped, a flyer and a rooted
	// tangle, none of which is shaped like anything else in the game.
	private PackedScene forestWolfScene = ResourceLoader.Load<PackedScene>("res://scenes/ForestWolf.tscn");
	private PackedScene forestWispScene = ResourceLoader.Load<PackedScene>("res://scenes/ForestWisp.tscn");
	private PackedScene forestBrambleScene = ResourceLoader.Load<PackedScene>("res://scenes/ForestBramble.tscn");
	private PackedScene lungerEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/LungerEnemy.tscn");
	private PackedScene exploderEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/ExploderEnemy.tscn");
	private PackedScene slammerEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/SlammerEnemy.tscn");
	private PackedScene summonerEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/SummonerEnemy.tscn");
	private PackedScene skullSentryScene = ResourceLoader.Load<PackedScene>("res://scenes/SkullSentry.tscn");

	// THE SKETCHBOOK CAST, drawn by Eric's kids. The chapter uses these and nothing else - see
	// IsSketchbookStage below for the three places that enforce it. Seven bodies covering the
	// same roles the Bonelight roster does: three chasers at different speeds, a heavy, a
	// miniboss, a charger and a shover.
	private PackedScene kidSmilerScene = ResourceLoader.Load<PackedScene>("res://scenes/KidSmiler.tscn");
	private PackedScene kidOneEyeScene = ResourceLoader.Load<PackedScene>("res://scenes/KidOneEye.tscn");
	private PackedScene kidZombieScene = ResourceLoader.Load<PackedScene>("res://scenes/KidZombie.tscn");
	private PackedScene kidVampireScene = ResourceLoader.Load<PackedScene>("res://scenes/KidVampire.tscn");
	private PackedScene kidRockemScene = ResourceLoader.Load<PackedScene>("res://scenes/KidRockem.tscn");
	private PackedScene kidBlobbyScene = ResourceLoader.Load<PackedScene>("res://scenes/KidBlobby.tscn");
	private PackedScene kidPushyScene = ResourceLoader.Load<PackedScene>("res://scenes/KidPushy.tscn");
	private PackedScene kidMessageScene = ResourceLoader.Load<PackedScene>("res://scenes/KidMessage.tscn");
	private PackedScene kidDogScene = ResourceLoader.Load<PackedScene>("res://scenes/KidDog.tscn");

	// Health readout in the top-left corner. Width matches the XP bar beneath it; height comes
	// from the frame art's 745x138 aspect so the ornament is not squashed.
	private const float HealthHudWidth = 240f;
	private const float HealthHudHeight = 44f;
	private ProgressBar? healthHudBar;
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
		(90f, "Tip: Element tags stack. Hitting 2/4/6 grants stronger passive thresholds."),
		(180f, "Tip: If your loadout is full, swap low-impact spells for scaling picks."),
		(300f, "Tip: If you're behind, use rerolls to force stronger options.")
	};

	public override void _Ready()
	{
		ContentValidator.ValidateAtStartup(this);
		GameStats.ResetRunTelemetry();
		RunEvents.Reset();
		YSortEnabled = true;
		PlayRunMusic();
		spawnRng.Randomize();
		player = GetNode<Player>("CharacterBody2D"); // Strongly typed YES
		stageOrigin = player?.GlobalPosition ?? Vector2.Zero;
		ApplyBalancePresetFromSave();
		ApplyStageTheme();
		ApplyAmbientStageEffects();
		BuildWorldTileMapLayout();
		nextEliteSpawnTime = EliteStartTimeSeconds;
		player?.Connect("XpGained", new Callable(this, nameof(OnPlayerXpGained)));
		player?.Connect("LevelGained", new Callable(this, nameof(OnPlayerLevelGained)));
		player?.Connect("Died", new Callable(this, nameof(OnPlayerDied)));
		player?.Connect("DamageTaken", new Callable(this, nameof(OnPlayerDamageTaken)));

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
			menuScript?.Connect("SwapRequested", new Callable(this, nameof(OnSwapRequested)));
			menuScript?.Connect("RemoveRequested", new Callable(this, nameof(OnRemoveRequested)));
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

			lowHealthOverlay = new ColorRect
			{
				Name = "LowHealthOverlay",
				Color = new Color(0.8f, 0.05f, 0.05f, 0f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AnchorRight = 1f,
				AnchorBottom = 1f
			};
			uiOverlay.AddChild(lowHealthOverlay);

			// The element badges, the equipped-spell row and the relic strip used to live here and
			// are all gone from the playfield.
			//
			// None of them was information the player acts on WHILE playing. What you own is
			// decided at a level-up and read back on the pause screen, which already lists the
			// equipped weapons, the character passive and every element count in full - so nothing
			// was lost by taking them off the field, and what was gained is the field.
			//
			// elementHudGrid and selectedSpellHudRow stay declared and stay null. Every refresh
			// path already early-returns on null, so the call sites did not have to change and
			// putting any of this back is a matter of constructing it again.

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
		// ChestItemHUD and SynergyDetailScreen are untouched and still work; nothing constructs
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
		RefreshElementHud();
		EnsureEscapeMenuUi();
		BuildDecorProps();
		SpawnKidDog();
		BuildStageHazards();
		BuildCuratedProps();
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
		if (healthHudLabel != null)
			healthHudLabel.Text = $"{Math.Max(0, player.CurrentHP)} / {player.MaxHP}";
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

	private void PlayRunMusic()
	{
		var musicPlayer = GetNodeOrNull<MusicPlayer>("/root/MusicPlayer");
		// Which track is MusicCatalog's decision, not this scene's - every chapter shares one
		// loop today and the plan is one each, so the branch belongs in the table.
		string path = MusicCatalog.RunTrackForStage(Global.SelectedStageIdx);
		// An empty path is the catalog saying there is no track, which is the state today and
		// is not a fault - warning about it every run would be noise, and noise is where a real
		// missing-asset warning goes to hide.
		if (string.IsNullOrEmpty(path))
			return;

		var music = ResourceLoader.Load<AudioStream>(path);
		if (music == null)
		{
			GD.PushWarning($"Node2DGame: run music missing at {path}; the run will play silent.");
			return;
		}
		musicPlayer?.PlayMusic(music);
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
		int stageIndex = Mathf.Clamp(Global.SelectedStageIdx, 0, 9);
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
		if (levelPainter == null || player == null || player.IsDead)
		{
			hazardTickTimer = 0f;
			return;
		}

		if (!levelPainter.IsHazardAtWorld(player.GlobalPosition))
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

		int stageIndex = Mathf.Clamp(Global.SelectedStageIdx, 0, 9);
		StageEnvironmentKind kind = StageEnvironmentCatalog.GetForStageIndex(stageIndex).Kind;

		int size = ProceduralGridRadius * 2 + 1;
		ulong seed = (ulong)((stageIndex + 1) * 73856093) ^ (ulong)DateTime.Now.Ticks;
		LevelLayout layout = new LevelGenerator().Generate(size, size, kind, seed, ProceduralSafeRadiusTiles);

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
		levelPainter.GroundTerrain = layout.GroundTerrain;
		levelPainter.BaseTerrain = layout.BaseTerrain;
		levelPainter.PaintCellOffset = new Vector2I(-ProceduralGridRadius, -ProceduralGridRadius);

		bool isMaze = layout.Topology == StageTopology.Maze;
		// Mazes keep corridor dead-ends (no cleanup) so the labyrinth stays intact.
		levelPainter.Paint(layout.Grid, layout.BaseTerrain, cleanupGrid: !isMaze);

		// Wall-aware enemy navigation is only needed (and only valid) for maze stages.
		MazeNavigation.Active = isMaze
			? new MazeNavigation(layout.Grid, LevelGenerator.WallTerrain, ProceduralTilePixels,
				new Vector2I(-ProceduralGridRadius, -ProceduralGridRadius), stageOrigin)
			: null;

		proceduralPropThemes = layout.PropThemes;

		// Constrain the player to just inside the painted arena so they never reach the void edge.
		proceduralArenaHalfExtent = Mathf.Max(0, ProceduralGridRadius - 3) * ProceduralTilePixels;
		proceduralActive = true;
		ConstrainPlayerToStageBounds = true;
		return true;
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

		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(Mathf.Clamp(Global.SelectedStageIdx, 0, 9));
		Color baseTint = environmentProfile.Kind switch
		{
			StageEnvironmentKind.Forest => new Color(0.16f, 0.28f, 0.16f, 0.10f),
			StageEnvironmentKind.Castle => new Color(0.12f, 0.16f, 0.24f, 0.10f),
			StageEnvironmentKind.Ruins => new Color(0.24f, 0.18f, 0.12f, 0.10f),
			StageEnvironmentKind.Swamp => new Color(0.17f, 0.24f, 0.16f, 0.12f),
			StageEnvironmentKind.Ice => new Color(0.16f, 0.24f, 0.34f, 0.12f),
			StageEnvironmentKind.Desert => new Color(0.28f, 0.22f, 0.12f, 0.10f),
			StageEnvironmentKind.Volcanic => new Color(0.28f, 0.12f, 0.08f, 0.12f),
			_ => new Color(0.10f, 0.10f, 0.14f, 0.08f)
		};

		var haze = CreateAmbientOverlay(canvas, baseTint, Vector2.Zero);
		var glowTint = environmentProfile.Kind switch
		{
			StageEnvironmentKind.Forest => new Color(0.26f, 0.40f, 0.24f, 0.05f),
			StageEnvironmentKind.Castle => new Color(0.28f, 0.22f, 0.34f, 0.05f),
			StageEnvironmentKind.Ruins => new Color(0.36f, 0.26f, 0.14f, 0.05f),
			StageEnvironmentKind.Swamp => new Color(0.20f, 0.30f, 0.18f, 0.06f),
			StageEnvironmentKind.Ice => new Color(0.26f, 0.36f, 0.50f, 0.06f),
			StageEnvironmentKind.Desert => new Color(0.44f, 0.34f, 0.16f, 0.05f),
			StageEnvironmentKind.Volcanic => new Color(0.46f, 0.20f, 0.10f, 0.06f),
			_ => new Color(0.12f, 0.12f, 0.18f, 0.04f)
		};
		var glow = CreateAmbientOverlay(canvas, glowTint, new Vector2(120f, 80f));

		ColorRect? weatherOverlay = null;
		switch (environmentProfile.Kind)
		{
			case StageEnvironmentKind.Forest:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.90f, 0.96f, 0.86f, 0.025f), new Vector2(-160f, -120f));
				break;
			case StageEnvironmentKind.Castle:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.06f, 0.08f, 0.12f, 0.028f), new Vector2(80f, 40f));
				break;
			case StageEnvironmentKind.Ruins:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.24f, 0.18f, 0.12f, 0.024f), new Vector2(-40f, -60f));
				break;
			case StageEnvironmentKind.Swamp:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.12f, 0.24f, 0.16f, 0.032f), new Vector2(-120f, -90f));
				break;
			case StageEnvironmentKind.Ice:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.94f, 0.97f, 1.00f, 0.018f), new Vector2(-80f, -40f));
				break;
			case StageEnvironmentKind.Desert:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.72f, 0.56f, 0.28f, 0.024f), new Vector2(-140f, -80f));
				break;
			case StageEnvironmentKind.Volcanic:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.54f, 0.20f, 0.10f, 0.026f), new Vector2(70f, 50f));
				break;
		}

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
	// Desert get none: an open natural stage is shaped by its enemies, not by traps.
	private static (bool UseSpikes, float FlameChance) GetStageHazardMix(StageEnvironmentKind kind)
	{
		return kind switch
		{
			// Flame vents are the rarer, nastier one, so they stay the minority pick.
			StageEnvironmentKind.Castle => (true, 0.28f),
			StageEnvironmentKind.Ruins => (true, 0.18f),
			StageEnvironmentKind.Volcanic => (false, 1.0f),
			_ => (false, 0f)
		};
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

		StageEnvironmentKind kind = StageEnvironmentCatalog.GetForStageIndex(Mathf.Clamp(Global.SelectedStageIdx, 0, 9)).Kind;
		(bool useSpikes, float flameChance) = GetStageHazardMix(kind);
		if (!useSpikes && flameChance <= 0f)
			return;

		var spikeScene = useSpikes ? ResourceLoader.Load<PackedScene>("res://scenes/SpikeTrap.tscn") : null;
		var flameScene = flameChance > 0f ? ResourceLoader.Load<PackedScene>("res://scenes/FlameVent.tscn") : null;
		if (spikeScene == null && flameScene == null)
			return;

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

		int stageIndex = Mathf.Clamp(Global.SelectedStageIdx, 0, 9);
		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(stageIndex);
		List<Texture2D> forestGroundAccents = LoadTexturesFromPaths(
			"res://assets/bonelight/world/rock-1.png",
			"res://assets/bonelight/world/rock-2.png");
		List<Texture2D> bushes = LoadTexturesFromPaths(
			"res://assets/bonelight/world/bush-1.png",
			"res://assets/bonelight/world/bush-2.png");
		List<Texture2D> trees = LoadTexturesFromPaths(
			"res://assets/bonelight/world/tree-1.png",
			"res://assets/bonelight/world/tree-2.png");
		List<Texture2D> ruins = LoadTexturesFromPaths(
			"res://assets/bonelight/world/crystal-yellow.png",
			"res://assets/bonelight/world/crystal-violet.png");
		List<Texture2D> swampReeds = LoadTexturesFromPaths(
			"res://assets/bonelight/world/bush-1.png",
			"res://assets/bonelight/world/bush-2.png");
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
				CreateDecorSet(bushes, Math.Max(18, environmentProfile.BushCount), 0.90f, 1.10f, false, false, -36, -22, true, 3, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.04f);
				CreateDecorSet(trees, Math.Max(20, environmentProfile.TreeCount), 1.00f, 1.22f, false, false, -24, -8, true, 3, TreeClusterRadiusMin, TreeClusterRadiusMax * 0.85f, TreeClusterCenterSeparation * 0.62f, 0.05f);
				CreateDecorSet(forestGroundAccents, 240, 0.88f, 1.02f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				break;
			case StageEnvironmentKind.Castle:
				CreateDecorSet(ruins, Math.Max(18, environmentProfile.RuinCount), 0.95f, 1.14f, false, false, -26, -14, true, 3, RuinClusterRadiusMin, RuinClusterRadiusMax, RuinClusterCenterSeparation, 0.06f);
				CreateDecorSet(forestGroundAccents, 8, 0.72f, 0.90f, false, false, -40, -28, true, 2, GroundAccentClusterRadiusMin * 0.82f, GroundAccentClusterRadiusMax * 0.82f, GroundAccentClusterCenterSeparation * 0.80f, 0.04f);
				break;
			case StageEnvironmentKind.Ruins:
				CreateDecorSet(forestGroundAccents, 18, 0.88f, 1.02f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				CreateDecorSet(ruins, Math.Max(16, environmentProfile.RuinCount), 0.95f, 1.12f, false, false, -28, -14, true, 3, RuinClusterRadiusMin, RuinClusterRadiusMax, RuinClusterCenterSeparation, 0.06f);
				CreateDecorSet(trees, Math.Max(8, environmentProfile.TreeCount / 2), 0.96f, 1.10f, false, false, -22, -10, true, 4, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.05f);
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
				break;
			case StageEnvironmentKind.Volcanic:
				CreateDecorSet(ruins, Math.Max(14, environmentProfile.RuinCount), 0.90f, 1.06f, false, false, -28, -14, true, 3, RuinClusterRadiusMin * 0.88f, RuinClusterRadiusMax * 0.88f, RuinClusterCenterSeparation * 0.90f, 0.06f);
				CreateDecorSet(forestGroundAccents, 8, 0.84f, 0.96f, false, false, -42, -34, true, 2, GroundAccentClusterRadiusMin * 0.78f, GroundAccentClusterRadiusMax * 0.78f, GroundAccentClusterCenterSeparation * 0.86f, 0.05f);
				CreateDecorSet(volcanicAsh, 12, 0.70f, 0.94f, false, false, -36, -22, true, 3, GroundAccentClusterRadiusMin * 0.76f, GroundAccentClusterRadiusMax * 0.76f, GroundAccentClusterCenterSeparation * 0.82f, 0.04f);
				break;
			case StageEnvironmentKind.Swamp:
				CreateDecorSet(bushes, Math.Max(16, environmentProfile.BushCount), 0.86f, 1.00f, false, false, -36, -24, true, 4, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.04f);
				CreateDecorSet(trees, Math.Max(8, environmentProfile.TreeCount / 2), 0.90f, 1.04f, false, false, -24, -12, true, 4, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.05f);
				CreateDecorSet(swampReeds, 10, 0.78f, 0.94f, false, false, -38, -24, true, 3, BushClusterRadiusMin * 0.78f, BushClusterRadiusMax * 0.78f, BushClusterCenterSeparation * 0.72f, 0.04f);
				break;
			default:
				CreateDecorSet(forestGroundAccents, 20, 0.86f, 1.00f, false, false, -42, -34, true, 3, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
				CreateDecorSet(ruins, 24, 0.90f, 1.18f, false, false, -30, -14, true, 4, RuinClusterRadiusMin, RuinClusterRadiusMax * 1.15f, RuinClusterCenterSeparation * 0.88f, 0.06f);
				CreateDecorSet(bushes, 10, 0.86f, 0.98f, false, false, -36, -24, true, 4, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.04f);
				CreateDecorSet(trees, 10, 0.90f, 1.04f, false, false, -24, -12, true, 4, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, 0.05f);
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

		int placed = 0;
		int attempts = 0;
		int maxAttempts = CuratedPropCount * 8;
		while (placed < CuratedPropCount && attempts < maxAttempts)
		{
			attempts++;
			Vector2 position = SampleDecorPosition();

			if (levelPainter != null)
			{
				if (levelPainter.IsHazardAtWorld(position))
					continue;
				string terrain = levelPainter.TerrainAtWorld(position);
				if (terrain != null && PropAvoidTerrains.Contains(terrain))
					continue;
			}

			PropEntry entry = props[spawnRng.RandiRange(0, props.Count - 1)];
			Texture2D texture = LoadPropTexture(entry.ResPath);
			if (texture == null)
				continue;

			float scale = spawnRng.RandfRange(CuratedPropMinScale, CuratedPropMaxScale);
			var sprite = new Sprite2D
			{
				Texture = texture,
				Centered = true,
				ZIndex = 0,
			};
			sprite.Scale = new Vector2(scale, scale);
			sprite.FlipH = spawnRng.Randf() < 0.5f;

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
			AddChild(propNode);
			MoveChild(propNode, 0);
			placed++;
		}
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
		if (stageIntroLabel == null)
			return;

		var panel = stageIntroLabel.GetParent() as Control;
		if (panel != null)
		{
			panel.Visible = true;
			panel.Modulate = Colors.White;
		}

		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(Mathf.Clamp(Global.SelectedStageIdx, 0, 9));
		stageIntroLabel.Text = $"{GetCurrentStageName()} • {environmentProfile.DisplayName}\n{GetCurrentStageFlavorText()}";
		stageIntroLabel.Modulate = environmentProfile.Kind switch
		{
			StageEnvironmentKind.Forest => new Color(0.90f, 0.98f, 0.86f, 1.0f),
			StageEnvironmentKind.Castle => new Color(0.94f, 0.92f, 0.96f, 1.0f),
			StageEnvironmentKind.Ruins => new Color(0.98f, 0.90f, 0.80f, 1.0f),
			StageEnvironmentKind.Swamp => new Color(0.86f, 0.96f, 0.84f, 1.0f),
			StageEnvironmentKind.Ice => new Color(0.90f, 0.95f, 1.0f, 1.0f),
			StageEnvironmentKind.Desert => new Color(0.99f, 0.95f, 0.78f, 1.0f),
			StageEnvironmentKind.Volcanic => new Color(0.99f, 0.88f, 0.78f, 1.0f),
			_ => new Color(0.96f, 0.98f, 1.0f, 1.0f)
		};

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenInterval(2.4f);
		if (panel != null)
			tween.TweenProperty(panel, "modulate:a", 0.0f, 0.6f);
		tween.TweenProperty(stageIntroLabel, "modulate:a", 0.0f, 0.6f);
		tween.SetParallel(false);
		if (panel != null)
			tween.TweenCallback(Callable.From(() => panel.Hide()));
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

		List<Vector2> spawnPositions = useClustering && EnableBiomeClustering
			? BuildClusteredPositions(count, clusterTargetSize, clusterRadiusMin, clusterRadiusMax, clusterCenterSeparation, clusterOutlierChance)
			: BuildUniformPositions(count);

		for (int i = 0; i < spawnPositions.Count; i++)
		{
			Vector2 position = spawnPositions[i];
			Texture2D texture = textures[spawnRng.RandiRange(0, textures.Count - 1)];

			if (levelPainter != null)
			{
				if (levelPainter.IsHazardAtWorld(position))
					continue;
				string terrain = levelPainter.TerrainAtWorld(position);
				if (terrain != null && PropAvoidTerrains.Contains(terrain))
					continue;
			}
			// Quantised to whole pixels: the project filters nearest now, and a fractional
			// prop scale aliases every edge of a hand-drawn sprite. Every range the callers
			// below pass sits between 0.72 and 1.22, so today they all land on 1 - the
			// per-biome numbers stay as relative intent for migration phase 6, which adds
			// size variety by drawing more props rather than by rescaling one.
			float scale = Mathf.Max(1f, Mathf.Round(spawnRng.RandfRange(minScale, maxScale)));
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
			sprite.FlipH = spawnRng.Randf() < 0.45f;
			sprite.FlipV = allowYFlip && spawnRng.Randf() < 0.1f;
			if (randomRotation)
				sprite.Rotation = Mathf.DegToRad(spawnRng.RandfRange(-8.0f, 8.0f));

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
			AddChild(propBody);
			MoveChild(propBody, 0);
		}
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

		// Skip to the boss. Sitting through fifteen minutes to check one fight is not a testing
		// loop anyone will actually run, and the boss is the only part of a stage that can be
		// broken without the first fourteen minutes showing it.
		if (@event is InputEventKey bossKeyEvent && bossKeyEvent.Pressed && !bossKeyEvent.Echo && bossKeyEvent.Keycode == Key.F4)
		{
			if (!bossPhaseTriggered && TimerVictorySeconds > 0f)
				timeElapsed = TimerVictorySeconds;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (runFinished)
			return;

		if (GetTree().Paused)
			return;

		float d = (float)delta;
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

		// No chests and no Warden during the boss fight: the arena is meant to drain down to the
		// boss and its own mechanics, not keep handing out set-pieces mid-duel.
		if (!bossFightActive)
		{
			chestSpawnTimer += d;
			float chestFrequencyMultiplier = 1.0f + (player?.GetChestItemDropRateBonus() ?? 0.0f);
			float targetChestInterval = (initialChestSpawned ? ChestSpawnIntervalSeconds : FirstChestSpawnDelaySeconds) / chestFrequencyMultiplier;
			if (chestSpawnTimer >= targetChestInterval)
			{
				chestSpawnTimer = 0f;
				initialChestSpawned = true;
				SpawnChestReward();
			}

			TickWardenMiniBossSpawn(d);
		}

		UpdateSpawnScaling();
		TickFormations(d);
		ClampPlayerToStageBounds();
		// After the clamp, deliberately. The whole point of measuring travel rather than reading
		// Velocity is that it must go to zero when the player is pinned against the arena edge.
		TrackPlayerTravel(d);
		UpdateHazardDamage(d);
		if (MazeNavigation.Active != null && player != null && IsInstanceValid(player))
			MazeNavigation.Active.Update(player.GlobalPosition, d);
		UpdateBossHud();
		if (TickBossPhaseStart())
			return;

		if (fireTimer >= fireInterval)
		{
			// TODO: call fire logic
			fireTimer = 0f;
		}
		if (spawnTimer >= spawnInterval)
		{
			var currentEnemies = GetTree().GetNodesInGroup("enemies");
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
			if (!bossFightActive && spawnGraceRemaining <= 0f && currentEnemies.Count < trickleCap)
			{
				int availableSlots = trickleCap - currentEnemies.Count;
				int burstCapacity = Math.Max(0, MaxSpawnsPerBurstWindow - spawnCountInBurstWindow);
				int spawnBatch = Math.Min(Math.Min(availableSlots, burstCapacity), GetSpawnBatchCount());
				for (int i = 0; i < spawnBatch; i++)
				{
					SpawnEnemy();
					spawnCountInBurstWindow++;
				}
			}
			spawnTimer = 0f;
		}

		var background = GetNode<TextureRect>("CanvasLayer/Background");
		var backgroundOverlay = GetNodeOrNull<TextureRect>("CanvasLayer/BackgroundOverlay");
		var camera = player.GetNode<Camera2D>("Camera2D");
		if (background.Material is ShaderMaterial material && camera != null)
		{
			var textureSize = background.Texture.GetSize();
			Vector2 offset = camera.GlobalPosition / textureSize;
			material.SetShaderParameter("scroll_offset", offset);
			// World-space offset drives the seamless tiled ground. The shader samples in screen
			// pixels, so it also needs the zoom: without it the ground scrolls at 1/zoom the rate
			// of everything standing on it, and every prop appears to slide across the floor.
			material.SetShaderParameter("world_offset", camera.GlobalPosition);
			material.SetShaderParameter("camera_zoom", camera.Zoom.X);
			if (backgroundOverlay?.Material is ShaderMaterial overlayMaterial)
				overlayMaterial.SetShaderParameter("scroll_offset", camera.GlobalPosition / textureSize);

			UpdateDecorParallax();
		}
	}

	private void OnPlayerXpGained(int amount)
	{
		GD.Print($"Player XP Gained: {amount}");
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

	private void OnPlayerLevelGained()
	{
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
			menuScript?.Connect("SwapRequested", new Callable(this, nameof(OnSwapRequested)));
			menuScript?.Connect("RemoveRequested", new Callable(this, nameof(OnRemoveRequested)));
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
			if (levelUpMenu is LevelUpMenu typedMenu && player != null)
			{
				typedMenu.SetOptions(player.GetLevelUpOptions(optionCount), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts(), player.BuildCharges(rerollsRemainingForCurrentLevelUp));
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

		if (!player.TryBanSpell(spellId))
			return;

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
	// LevelUpMenu needs (swap-selection prompt + elemental tag section's passive-highlight logic).
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

	private void OnSwapRequested(string newSpellId, string removedSpellId)
	{
		if (player == null)
			return;

		int beforeLevel = GetSpellLevel(player, newSpellId);

		if (!player.RemoveEquippedSpell(removedSpellId))
		{
			GD.PrintErr($"Could not remove spell '{removedSpellId}' for swap.");
		}

		if (!player.TryAddOrLevelSpell(newSpellId))
		{
			GD.PrintErr($"Could not add spell '{newSpellId}' after swap.");
		}
		else
		{
			GameStats.RecordSwapUsed();
			TrackSpellSelectionTelemetry(newSpellId, beforeLevel);
		}

		RefreshElementHud();
		CloseLevelUpMenu();
	}

	private void OnRemoveRequested(string removedSpellId)
	{
		if (player == null)
			return;

		if (!player.RemoveEquippedSpell(removedSpellId))
		{
			GD.PrintErr($"Could not remove spell '{removedSpellId}'.");
		}
		else
		{
			GameStats.RecordRemovalUsed();
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

		escapeRoot = new Control
		{
			Name = "EscapeRoot",
			FocusMode = Control.FocusModeEnum.All,
			MouseFilter = Control.MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always
		};
		escapeRoot.GuiInput += @event =>
		{
			// Escape, Start or B all close it. Unlike the opening check this one may read ui_cancel:
			// inside a menu, back IS the gesture, and there is no run in progress to interrupt.
			bool closes = @event.IsActionPressed("pause") || @event.IsActionPressed("ui_cancel");
			if (closes && escapeMenu != null && escapeMenu.Visible)
			{
				OnEscapeResumePressed();
				escapeRoot.AcceptEvent();
			}
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
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.10f, 0.10f, 0.13f, 0.98f);
		style.BorderColor = new Color(0.36f, 0.40f, 0.52f, 0.9f);
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(14);
		panel.AddThemeStyleboxOverride("panel", style);
		escapeRoot.AddChild(panel);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		panel.AddChild(root);

		var title = new Label
		{
			Text = "Paused",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		root.AddChild(title);

		// Resume is the only thing most pauses are for, so it is the one large target and it sits
		// at the top. Restart and Quit are deliberately at the far end of the panel: they used to
		// be stacked directly under Resume in the same size and colour, one misclick from ending a
		// run the player only meant to pause.
		Button resume = MakeEscapeButton("Resume", OnEscapeResumePressed, 72, ResponsiveLayout.TextRole.Title);
		resume.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		ApplyEscapePrimaryStyle(resume);
		root.AddChild(resume);

		// One flat tab strip in place of three titled group panels. The old grouping spent a
		// bordered box and a header label on every two buttons, which is most of what made the
		// menu read as busy.
		var tabs = new HBoxContainer();
		tabs.AddThemeConstantOverride("separation", 6);
		tabs.AddChild(MakeEscapeTabButton("Run Details", "Run", ShowEscapeRunOverview));
		tabs.AddChild(MakeEscapeTabButton("Spellbook Pool", "Spells", ShowEscapeSpellbook));
		tabs.AddChild(MakeEscapeTabButton("Achievement Progress", "Deeds", ShowEscapeAchievements));
		tabs.AddChild(MakeEscapeTabButton("Options", "Options", ShowEscapeOptions));
		root.AddChild(tabs);

		escapeDetailTitle = new Label { Text = "Run Details" };
		ResponsiveLayout.SetFont(escapeDetailTitle, ResponsiveLayout.TextRole.Body);
		escapeDetailTitle.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.92f));
		root.AddChild(escapeDetailTitle);

		escapeDetailScroll = new ScrollContainer
		{
			Visible = false,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true
		};
		root.AddChild(escapeDetailScroll);

		escapeDetailSections = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		escapeDetailSections.AddThemeConstantOverride("separation", 10);
		escapeDetailScroll.AddChild(escapeDetailSections);

		escapeDetailText = new RichTextLabel
		{
			FitContent = false,
			ScrollActive = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			BbcodeEnabled = false
		};
		escapeDetailText.AddThemeFontSizeOverride("normal_font_size",
			ResponsiveLayout.FontSize(escapeDetailText, ResponsiveLayout.TextRole.Body));
		root.AddChild(escapeDetailText);

		var exits = new HBoxContainer();
		exits.AddThemeConstantOverride("separation", 8);
		foreach (Button exit in new[]
		{
			MakeEscapeButton("Restart Run", OnEscapeRestartPressed, 46, ResponsiveLayout.TextRole.Label),
			MakeEscapeButton("Quit to Menu", OnEscapeQuitPressed, 46, ResponsiveLayout.TextRole.Label),
		})
		{
			exit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			ApplyEscapeQuietStyle(exit);
			exits.AddChild(exit);
		}
		root.AddChild(exits);
	}

	private Button MakeEscapeButton(string text, Action pressed, float height = 44f,
		ResponsiveLayout.TextRole role = ResponsiveLayout.TextRole.Body)
	{
		var button = new Button
		{
			Text = text,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(0, height),
			ProcessMode = ProcessModeEnum.Always
		};
		ResponsiveLayout.SetFont(button, role);
		ApplyEscapeButtonStyle(button, false);
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
	private Button MakeEscapeTabButton(string detailTitle, string caption, Action pressed)
	{
		Button button = MakeEscapeButton(caption, pressed, 46f, ResponsiveLayout.TextRole.Label);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		escapeTabButtons[detailTitle] = button;
		return button;
	}

	// Resume: the one filled, lit control on the panel.
	private static void ApplyEscapePrimaryStyle(Button button)
	{
		button.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color(0.26f, 0.42f, 0.56f, 0.98f), new Color(0.62f, 0.86f, 1.0f, 1.0f)));
		button.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color(0.32f, 0.50f, 0.66f, 0.98f), new Color(0.78f, 0.94f, 1.0f, 1.0f)));
		button.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color(0.20f, 0.33f, 0.45f, 0.98f), new Color(0.86f, 0.97f, 1.0f, 1.0f)));
		button.AddThemeColorOverride("font_color", Colors.White);
	}

	// Restart and Quit: hairline on near-black, so the two irreversible actions do not compete for
	// the eye with the one the player almost always wants.
	private static void ApplyEscapeQuietStyle(Button button)
	{
		button.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color(0.08f, 0.08f, 0.11f, 0.94f), new Color(0.28f, 0.30f, 0.38f, 0.85f)));
		button.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color(0.16f, 0.13f, 0.14f, 0.96f), new Color(0.66f, 0.44f, 0.44f, 0.95f)));
		button.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color(0.06f, 0.06f, 0.08f, 0.98f), new Color(0.80f, 0.56f, 0.56f, 1.0f)));
		button.AddThemeColorOverride("font_color", new Color(0.74f, 0.76f, 0.82f));
	}

	private void SetEscapeActiveTab(string title)
	{
		foreach (var pair in escapeTabButtons)
			ApplyEscapeButtonStyle(pair.Value, pair.Key.Equals(title, StringComparison.OrdinalIgnoreCase));
	}

	private void ApplyEscapeButtonStyle(Button button, bool active)
	{
		if (active)
		{
			button.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color(0.30f, 0.34f, 0.45f, 0.98f), new Color(0.78f, 0.84f, 1.0f, 1.0f)));
			button.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color(0.34f, 0.39f, 0.52f, 0.98f), new Color(0.88f, 0.92f, 1.0f, 1.0f)));
			button.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color(0.24f, 0.28f, 0.38f, 0.98f), new Color(0.92f, 0.95f, 1.0f, 1.0f)));
			button.AddThemeColorOverride("font_color", Colors.White);
			return;
		}

		button.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color(0.15f, 0.16f, 0.22f, 0.96f), new Color(0.34f, 0.38f, 0.50f, 0.95f)));
		button.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color(0.22f, 0.24f, 0.32f, 0.98f), new Color(0.58f, 0.64f, 0.80f, 1.0f)));
		button.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color(0.11f, 0.12f, 0.17f, 0.98f), new Color(0.75f, 0.79f, 0.92f, 1.0f)));
		button.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.98f));
	}

	private static StyleBoxFlat BuildButtonStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat();
		style.BgColor = background;
		style.BorderColor = border;
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		style.SetContentMarginAll(8);
		return style;
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
		escapeRoot?.GrabFocus();

		// On the CanvasLayer, not on escapeRoot: a Control's IsVisibleInTree stops at the first
		// non-CanvasItem ancestor, so a Control under a hidden CanvasLayer still reports itself
		// visible - and a navigator that believed that would grab focus, and answer the stick,
		// through the whole run. Attached on every open rather than once, so it also picks up the
		// run overview ShowEscapeRunOverview has just rebuilt.
		MenuNavigator.Attach(escapeMenu);
		GetTree().Paused = true;
	}

	private void OnEscapeResumePressed()
	{
		escapeMenu?.Hide();
		GetTree().Paused = false;
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

	private void SetEscapeDetail(string title, string text)
	{
		SetEscapeActiveTab(title);
		if (escapeDetailTitle != null)
			escapeDetailTitle.Text = title;
		if (escapeDetailText != null)
		{
			escapeDetailText.Text = text;
			escapeDetailText.Visible = true;
		}
		if (escapeDetailScroll != null)
			escapeDetailScroll.Visible = false;
	}

	private void ShowStructuredEscapeDetail(string title, Action<VBoxContainer> buildContent)
	{
		SetEscapeActiveTab(title);
		if (escapeDetailTitle != null)
			escapeDetailTitle.Text = title;
		if (escapeDetailText != null)
			escapeDetailText.Visible = false;
		if (escapeDetailScroll != null)
			escapeDetailScroll.Visible = true;
		if (escapeDetailSections == null)
			return;

		ClearEscapeDetailSections();
		buildContent(escapeDetailSections);
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

			var muteToggle = new CheckButton { Text = "Mute All Audio" };
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
		row.AddChild(BuildDetailLabel(label, new Color(0.82f, 0.84f, 0.9f), ResponsiveLayout.TextRole.Micro));

		var slider = new HSlider
		{
			MinValue = 0.0,
			MaxValue = 1.0,
			Step = 0.01,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 24)
		};
		int busIndex = AudioServer.GetBusIndex(busName);
		slider.Value = busIndex >= 0 ? Mathf.DbToLinear(AudioServer.GetBusVolumeDb(busIndex)) : 1.0;
		// Through AudioSettings so a mid-run volume change survives the run, the same as the
		// identical sliders on the main menu.
		slider.ValueChanged += value => AudioSettings.Store(this, busName, (float)value);
		row.AddChild(slider);
		return row;
	}

	private void ShowEscapeRunOverview()
	{
		ShowStructuredEscapeDetail("Run Details", sections =>
		{
			sections.AddChild(BuildRunSummarySection());
			sections.AddChild(BuildCharacterPassiveSection());
			sections.AddChild(BuildSpellListSection("Offensive Spells", player?.GetEquippedSpells().Where(s => s != null && !s.IsPassive).ToList() ?? new List<SpellData>()));
			sections.AddChild(BuildSpellListSection("Defensive Spells", player?.GetEquippedSpells().Where(s => s != null && s.IsPassive).ToList() ?? new List<SpellData>()));
			sections.AddChild(BuildElementPassiveSection());
		});
	}

	private Control BuildRunSummarySection()
	{
		var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 8);
		grid.AddChild(BuildStatTile("Time", FormatTime(timeElapsed)));
		grid.AddChild(BuildStatTile("Level", (player?.CurrentLevel ?? 1).ToString()));
		grid.AddChild(BuildStatTile("XP", $"{player?.CurrentXP ?? 0}/{player?.XPToNextLevel ?? 0}"));
		return BuildEscapeSection("Run Summary", grid);
	}

	private Control BuildCharacterPassiveSection()
	{
		CharacterData character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 4);
		if (character == null)
		{
			list.AddChild(BuildDetailLabel("Apprentice fallback: no character passive loaded.", new Color(0.82f, 0.84f, 0.9f)));
			return BuildEscapeSection("Character Passive", list);
		}

		string passiveName = string.IsNullOrWhiteSpace(character.StartingPassiveName) ? "Passive" : character.StartingPassiveName;
		string passiveDescription = string.IsNullOrWhiteSpace(character.StartingPassiveDescription) ? "No passive description." : character.StartingPassiveDescription;
		list.AddChild(BuildDetailLabel($"{character.Name}: {passiveName}", Colors.White));
		list.AddChild(BuildDetailLabel(passiveDescription, new Color(0.78f, 0.81f, 0.88f)));
		return BuildEscapeSection("Character Passive", list);
	}

	private Control BuildSpellListSection(string title, List<SpellData> spells)
	{
		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 6);
		if (spells.Count == 0)
		{
			list.AddChild(BuildDetailLabel("None", new Color(0.68f, 0.70f, 0.76f)));
			return BuildEscapeSection(title, list);
		}

		foreach (SpellData spell in spells)
			list.AddChild(BuildSpellRow(spell));

		return BuildEscapeSection(title, list);
	}

	private Control BuildSpellRow(SpellData spell)
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 8);

		var icon = new TextureRect
		{
			Texture = spell.Icon ?? FallbackSpellHudIcon,
			CustomMinimumSize = new Vector2(40, 40),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		row.AddChild(icon);

		var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel($"{spell.Name} Lv {spell.CurrentLevel}/{spell.MaxLevel}", Colors.White, ResponsiveLayout.TextRole.Micro));
		string description = string.IsNullOrWhiteSpace(spell.Description) ? "No description." : spell.Description;
		box.AddChild(BuildDetailLabel(description, new Color(0.82f, 0.84f, 0.90f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel($"Damage {spell.GetDamageAtLevel(spell.CurrentLevel)} | Cooldown {spell.GetCooldownAtLevel(spell.CurrentLevel):0.##}s | Projectiles {spell.GetProjectileCountAtLevel(spell.CurrentLevel)} | Range {spell.GetRangeAtLevel(spell.CurrentLevel):0}", new Color(0.78f, 0.81f, 0.88f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel(FormatElementWeights(spell.GetElementWeights()), new Color(0.66f, 0.72f, 0.86f), ResponsiveLayout.TextRole.Micro));
		string attunement = DescribeAttunement(spell);
		if (!string.IsNullOrEmpty(attunement))
			box.AddChild(BuildDetailLabel(attunement, new Color(1.0f, 0.86f, 0.42f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel($"Classification: {FormatSpellClassification(spell)}", new Color(0.60f, 0.82f, 0.96f), ResponsiveLayout.TextRole.Micro));
		row.AddChild(box);

		return BuildInfoFrame(row, new Color(0.14f, 0.15f, 0.20f, 0.96f), new Color(0.30f, 0.34f, 0.46f, 0.9f));
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
			list.AddChild(BuildInfoRow("No active element passives yet", new Color(0.14f, 0.15f, 0.18f, 0.90f), new Color(0.30f, 0.32f, 0.38f, 0.82f), new Color(0.62f, 0.64f, 0.70f)));
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
		Color background = active ? new Color(elementColor.R, elementColor.G, elementColor.B, 0.88f) : new Color(0.18f, 0.18f, 0.21f, 0.86f);
		Color border = active ? new Color(1.0f, 1.0f, 1.0f, 0.28f) : new Color(0.36f, 0.36f, 0.40f, 0.80f);
		Color textColor = active ? GetReadableTextColor(elementColor) : new Color(0.56f, 0.57f, 0.62f);
		string status = active ? $"Tier {tier}" : "Inactive";
		string effect = active ? ElementPassiveDescriptions.GetEffectText(element, tier) : ElementPassiveDescriptions.GetEffectText(element, 0);

		var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(BuildElementNameBadge(element, count, active, elementColor, textColor));

		var textBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		textBox.AddThemeConstantOverride("separation", 2);
		textBox.AddChild(BuildDetailLabel(status, textColor, ResponsiveLayout.TextRole.Micro));
		textBox.AddChild(BuildDetailLabel(effect, textColor, ResponsiveLayout.TextRole.Micro));
		row.AddChild(textBox);

		return BuildInfoFrame(row, background, border);
	}

	private Control BuildElementNameBadge(Element element, int count, bool active, Color elementColor, Color textColor)
	{
		Color badgeBackground = active ? new Color(elementColor.R, elementColor.G, elementColor.B, 0.96f) : new Color(0.24f, 0.24f, 0.27f, 0.95f);
		Color badgeBorder = active ? new Color(1.0f, 1.0f, 1.0f, 0.36f) : new Color(0.44f, 0.44f, 0.48f, 0.9f);
		var box = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(118, 0),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		box.AddThemeConstantOverride("separation", 0);
		var name = BuildDetailLabel(element.ToString(), textColor, ResponsiveLayout.TextRole.Micro);
		name.HorizontalAlignment = HorizontalAlignment.Center;
		var progress = BuildDetailLabel(ElementPassiveDescriptions.GetProgressLabel(count), textColor, ResponsiveLayout.TextRole.Micro);
		progress.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(name);
		box.AddChild(progress);
		return BuildInfoFrame(box, badgeBackground, badgeBorder);
	}

	private Label BuildGroupLabel(string text)
	{
		var label = new Label { Text = text };
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.AddThemeColorOverride("font_color", new Color(0.70f, 0.74f, 0.84f));
		return label;
	}

	private Control BuildEscapeSection(string title, Control content)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.09f, 0.10f, 0.14f, 0.94f);
		style.BorderColor = new Color(0.28f, 0.32f, 0.43f, 0.95f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		style.SetContentMarginAll(10);
		panel.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		panel.AddChild(box);

		var header = new Label { Text = title };
		ResponsiveLayout.SetFont(header, ResponsiveLayout.TextRole.Label);
		header.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.98f));
		box.AddChild(header);
		box.AddChild(content);
		return panel;
	}

	private Control BuildStatTile(string label, string value)
	{
		var box = new VBoxContainer { CustomMinimumSize = new Vector2(132, 58), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel(label, new Color(0.66f, 0.70f, 0.80f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel(value, Colors.White, ResponsiveLayout.TextRole.Body));
		return BuildInfoFrame(box, new Color(0.13f, 0.14f, 0.20f, 0.98f), new Color(0.34f, 0.38f, 0.52f, 0.95f));
	}

	private Control BuildInfoRow(string text, Color background, Color border, Color textColor)
	{
		return BuildInfoFrame(BuildDetailLabel(text, textColor, ResponsiveLayout.TextRole.Micro), background, border);
	}

	private Control BuildInfoFrame(Control content, Color background, Color border)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var style = new StyleBoxFlat();
		style.BgColor = background;
		style.BorderColor = border;
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(7);
		panel.AddThemeStyleboxOverride("panel", style);
		panel.AddChild(content);
		return panel;
	}

	private Label BuildDetailLabel(string text, Color color,
		ResponsiveLayout.TextRole role = ResponsiveLayout.TextRole.Micro)
	{
		var label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		ResponsiveLayout.SetFont(label, role);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private void ShowEscapeSpellbook()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		ShowStructuredEscapeDetail("Spellbook Pool", sections =>
		{
			var weapons = new VBoxContainer();
			weapons.AddThemeConstantOverride("separation", 6);
			int availableWeapons = 0;
			int totalWeapons = 0;
			foreach (var (id, path) in SpellbookResources)
			{
				SpellData spell = ResourceLoader.Load<SpellData>(path);
				if (spell == null)
					continue;

				bool unlocked = GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, id);
				totalWeapons++;
				if (unlocked)
					availableWeapons++;
				weapons.AddChild(BuildSpellbookWeaponRow(spell, unlocked));
			}
			sections.AddChild(BuildEscapeSection($"Weapons in Level-Up Pool ({availableWeapons}/{totalWeapons} Available)", weapons));

			var passives = new VBoxContainer();
			passives.AddThemeConstantOverride("separation", 6);
			int availablePassives = 0;
			foreach (var passive in PassiveSpellbookEntries)
			{
				bool unlocked = GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, passive.Id);
				if (unlocked)
					availablePassives++;
				passives.AddChild(BuildSpellbookPassiveRow(passive.Id, passive.DisplayName, passive.Description, passive.Elements, unlocked));
			}
			sections.AddChild(BuildEscapeSection($"Passives in Level-Up Pool ({availablePassives}/{PassiveSpellbookEntries.Length} Available)", passives));
		});
	}

	private void ShowEscapeAchievements()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		ShowStructuredEscapeDetail("Achievement Progress", sections =>
		{
			var completed = new VBoxContainer();
			completed.AddThemeConstantOverride("separation", 6);
			var inProgress = new VBoxContainer();
			inProgress.AddThemeConstantOverride("separation", 6);
			int completedCount = 0;

			foreach (AchievementDefinition achievement in AchievementDefinitions.All)
			{
				bool unlocked = GlobalStatsManager.IsAchievementUnlocked(saveManager?.Data, achievement.Id);
				// The live run counts toward progress here, unlike the main menu - the player is
				// mid-run and wants to know whether this attempt is on track.
				Control row = BuildAchievementRow(achievement, unlocked,
					achievement.GetProgress(AchievementContext.ForSave(saveManager?.Data)));
				if (unlocked)
				{
					completedCount++;
					completed.AddChild(row);
				}
				else
					inProgress.AddChild(row);
			}

			if (completed.GetChildCount() == 0)
				completed.AddChild(BuildInfoRow("No completed achievements yet", new Color(0.14f, 0.15f, 0.18f, 0.90f), new Color(0.30f, 0.32f, 0.38f, 0.82f), new Color(0.62f, 0.64f, 0.70f)));
			sections.AddChild(BuildEscapeSection($"Complete ({completedCount}/{AchievementDefinitions.All.Count})", completed));
			sections.AddChild(BuildEscapeSection($"In Progress ({AchievementDefinitions.All.Count - completedCount})", inProgress));
		});
	}

	private Control BuildSpellbookWeaponRow(SpellData spell, bool unlocked)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel($"{spell.Name} - {(unlocked ? "Available" : "Locked")}", unlocked ? Colors.White : new Color(0.58f, 0.59f, 0.64f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel(FormatElementWeights(spell.GetElementWeights()), unlocked ? new Color(0.66f, 0.72f, 0.86f) : new Color(0.46f, 0.48f, 0.54f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel($"Classification: {FormatSpellClassification(spell)}", unlocked ? new Color(0.60f, 0.82f, 0.96f) : new Color(0.45f, 0.56f, 0.64f), ResponsiveLayout.TextRole.Micro));
		return BuildStateFrame(box, unlocked);
	}

	private Control BuildSpellbookPassiveRow(string spellId, string name, string description, string elements, bool unlocked)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel($"{name} - {(unlocked ? "Available" : "Locked")}", unlocked ? Colors.White : new Color(0.58f, 0.59f, 0.64f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel(elements, unlocked ? new Color(0.66f, 0.72f, 0.86f) : new Color(0.46f, 0.48f, 0.54f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel($"Classification: {FormatPassiveClassification(spellId)}", unlocked ? new Color(0.60f, 0.82f, 0.96f) : new Color(0.45f, 0.56f, 0.64f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel(description, unlocked ? new Color(0.78f, 0.81f, 0.88f) : new Color(0.50f, 0.51f, 0.56f), ResponsiveLayout.TextRole.Micro));
		return BuildStateFrame(box, unlocked);
	}

	private static string FormatPassiveClassification(string spellId)
	{
		var passive = new SpellData
		{
			Id = spellId ?? string.Empty,
			IsPassive = true,
			TargetingMode = SpellTargetingMode.Auto,
			DamageShape = SpellDamageShape.Auto,
			ScalingTagsMask = 0
		};
		return FormatSpellClassification(passive);
	}

	private Control BuildAchievementRow(AchievementDefinition achievement, bool complete, AchievementProgress progress)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 3);
		string status = complete
			? "Complete"
			: string.IsNullOrEmpty(progress.Display) ? "Not yet" : progress.Display;
		box.AddChild(BuildDetailLabel($"{achievement.DisplayName} - {status}", complete ? Colors.White : new Color(0.72f, 0.74f, 0.80f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel(achievement.Description, complete ? new Color(0.78f, 0.84f, 0.78f) : new Color(0.66f, 0.68f, 0.74f), ResponsiveLayout.TextRole.Micro));
		box.AddChild(BuildDetailLabel(achievement.RewardText, complete ? new Color(0.78f, 0.88f, 0.72f) : new Color(0.54f, 0.58f, 0.66f), ResponsiveLayout.TextRole.Micro));
		return BuildStateFrame(box, complete);
	}

	private Control BuildStateFrame(Control content, bool active)
	{
		return BuildInfoFrame(content,
			active ? new Color(0.14f, 0.17f, 0.20f, 0.96f) : new Color(0.14f, 0.14f, 0.16f, 0.86f),
			active ? new Color(0.36f, 0.50f, 0.42f, 0.92f) : new Color(0.32f, 0.32f, 0.36f, 0.78f));
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

	private static string FormatSpellClassification(SpellData spell)
	{
		if (spell == null)
			return "Unknown";

		SpellTargetingMode targeting = ResolveTargetingMode(spell);
		SpellDamageShape shape = ResolveDamageShape(spell);
		SpellScalingTag scalingTags = ResolveScalingTags(spell);

		return $"Targeting {targeting} | Shape {shape} | Scaling {FormatScalingTags(scalingTags)}";
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
			"toxic_spore_burst" => SpellTargetingMode.Self,
			"cyclone_slash" => SpellTargetingMode.Self,
			"black_tentacles" => SpellTargetingMode.GroundAtEnemy,
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
			"meteor_swarm" => SpellDamageShape.RadiusBurst,
			"cone_of_cold" => SpellDamageShape.RadiusBurst,
			"black_tentacles" => SpellDamageShape.PersistentZone,
			"solar_flare" => SpellDamageShape.PersistentZone,
			"toxic_spore_burst" => SpellDamageShape.PersistentZone,
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

	private static string FormatScalingTags(SpellScalingTag tags)
	{
		if (tags == SpellScalingTag.None)
			return "None";

		var names = Enum.GetValues<SpellScalingTag>()
			.Where(tag => tag != SpellScalingTag.None && (tags & tag) != 0)
			.Select(tag => tag.ToString())
			.ToList();

		return names.Count == 0 ? "None" : string.Join(", ", names);
	}

	private static string FormatTime(float seconds)
	{
		int totalSeconds = Mathf.FloorToInt(seconds);
		return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
	}

	// Sampled here because the level-up menu is where the loadout changes - a spell added, levelled,
	// swapped or removed. RunResult.ElementCounts is an end-of-run snapshot, so without this a build
	// that peaked at six Fire and swapped away at level 18 would look like it never got there.
	private void SampleElementPeaks()
	{
		if (player != null && IsInstanceValid(player))
			RunEvents.RecordElementCounts(BuildElementCountSnapshot());
	}

	private void CloseLevelUpMenu()
	{
		// The single choke point for "the loadout may have just changed" - every pick, swap, remove
		// and skip funnels through here.
		SampleElementPeaks();

		// Decided before the teardown below, because reopening has to happen after the menu is gone
		// and the flag must not survive into the reopened menu and loop.
		bool reopenForBankedPick = extraPicksPending > 0;
		if (reopenForBankedPick)
			extraPicksPending--;

		// Unpause the game and remove the menu
		GetTree().Paused = false;
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

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.R * 0.299f) + (background.G * 0.587f) + (background.B * 0.114f);
		return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.07f) : Colors.White;
	}

	/// <summary>The horde's pace right now, easing from the opening value to the late one.</summary>
	private float GetEnemyPaceMultiplier()
	{
		float minutesElapsed = Mathf.Max(0.0f, timeElapsed / 60.0f);
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
		float minutesElapsed = Mathf.Max(0.0f, timeElapsed / 60.0f);
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
	private void SpawnEnemy(Vector2? at = null)
	{
		var selection = SelectEnemyForCurrentStage();
		if (selection.IsElite && GetCurrentEliteEnemyCount() >= MaxEliteEnemiesAlive)
			selection = (selection.Scene, selection.HealthMultiplier, false);

		var enemy = selection.Scene.Instantiate<Node2D>();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.Health = Mathf.RoundToInt(spawnHealth * selection.HealthMultiplier);
			typedEnemy.SpawnBaseSpeed = typedEnemy.Speed;
			typedEnemy.Speed *= GetEnemyPaceMultiplier();
			if (selection.IsElite)
			{
				typedEnemy.Health = Mathf.RoundToInt(typedEnemy.Health * EliteHealthMultiplier * presetElitePowerScale);
				typedEnemy.Speed *= EliteSpeedMultiplier * Mathf.Lerp(1.0f, presetElitePowerScale, 0.55f);
				typedEnemy.Scale *= EliteScaleMultiplier;
				typedEnemy.IsMiniBoss = true;
			}
		}

		enemy.Position = at ?? FindSeparatedSpawnPosition();
		AddChild(enemy);
		totalEnemiesSpawned++;
	}

	/// <summary>Spawns one wave in a shape, if it is time for one.</summary>
	private void TickFormations(float delta)
	{
		if (player == null || bossFightActive || spawnGraceRemaining > 0f)
			return;

		if (nextFormationTime <= 0f)
			nextFormationTime = Mathf.Max(FormationFirstSeconds, FormationIntervalSeconds);

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
		int alive = GetTree().GetNodesInGroup("enemies").Count;
		int room = Mathf.Min(MaxEnemies - alive, MaxSpawnsPerBurstWindow - spawnCountInBurstWindow);
		// Below three members no shape survives, so a wave that small is just a scatter wearing the
		// name of one. Better to wait for room than to spend the slot on something shapeless.
		if (room < 3)
			return;

		nextFormationTime = timeElapsed + Mathf.Max(1f, FormationIntervalSeconds);

		float minutesElapsed = timeElapsed / 60.0f;
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
			// shape, which is far better than a formation with a hole in it that never moves.
			if (IsImpassablePosition(position))
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
		// Rockem holds this slot in the Sketchbook. The recurring miniboss is the run's only
		// set-piece between the opening and the boss, so leaving the Warden in would have put the
		// one Bonelight body the chapter still had in its most visible moment.
		string miniBossScenePath = IsSketchbookStage
			? "res://scenes/KidRockem.tscn"
			: "res://scenes/WardenEnemy.tscn";
		string miniBossType = IsSketchbookStage ? "KidRockem" : "Warden";

		if (!EnableWardenMiniBoss || runFinished || player == null || !IsInstanceValid(player))
			return;
		if (timeElapsed < WardenMiniBossFirstSpawnSeconds)
			return;

		wardenMiniBossTimer += delta;
		if (wardenMiniBossTimer < WardenMiniBossIntervalSeconds)
			return;

		// Never stack them: a second Warden arriving while the first is alive turns a set-piece
		// into an unwinnable pile.
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is Enemy existing && IsInstanceValid(existing) && existing.EnemyType == miniBossType)
				return;
		}

		var scene = ResourceLoader.Load<PackedScene>(miniBossScenePath);
		if (scene == null)
			return;

		wardenMiniBossTimer = 0f;
		var warden = scene.Instantiate<Node2D>();
		if (warden is Enemy typedWarden)
		{
			// Scales with run length the same way ordinary spawns do, so a late Warden is still
			// a threat rather than a speed bump.
			float minutesElapsed = timeElapsed / 60f;
			typedWarden.Health = Mathf.RoundToInt(typedWarden.Health * (1f + minutesElapsed * 0.22f) * presetElitePowerScale);
		}
		warden.Position = FindSeparatedSpawnPosition();
		AddChild(warden);
		totalEnemiesSpawned++;
	}

	// The end of a level. At TimerVictorySeconds the survival clock stops and the stage's boss
	// arrives; beating it is the win. A stage with no boss in the catalog still ends right here the
	// way it always did, so bosses can be added one stage at a time.
	// Returns true when the run ended inside this call, so _Process knows to stop for the frame.
	private bool TickBossPhaseStart()
	{
		if (bossPhaseTriggered || runFinished || TimerVictorySeconds <= 0f || timeElapsed < TimerVictorySeconds)
			return false;

		bossPhaseTriggered = true;
		activeBossDefinition = BossCatalog.ForStageIndex(Global.SelectedStageIdx);
		if (activeBossDefinition == null || !SpawnBoss(activeBossDefinition))
		{
			activeBossDefinition = null;
			FinishRunAndReward(RunOutcome.Victory);
			return true;
		}

		bossFightActive = true;
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
	/// Three things key off this and they are all the same decision: nothing in that chapter may
	/// be a sprite the kids did not draw. The base mix swaps wholesale, the charger and shover
	/// slots swap to their drawings, the miniboss swaps to Rockem, and the three specials with no
	/// counterpart - the exploder, the summoner and the sentry - are simply suppressed rather
	/// than reskinned, because a chapter with three skeletons in it is not their chapter any more.
	///
	/// Keyed on the ENVIRONMENT rather than on the stage index, so a second kids' chapter would
	/// inherit all of it for free.
	/// </remarks>
	private bool IsSketchbookStage =>
		StageEnvironmentCatalog.GetForStageIndex(Mathf.Clamp(Global.SelectedStageIdx, 0, 9)).Kind
			== StageEnvironmentKind.Sketchbook;

	private bool SpawnBoss(BossDefinition definition)
	{
		if (player == null || !IsInstanceValid(player))
			return false;

		var scene = ResourceLoader.Load<PackedScene>(definition.ScenePath);
		if (scene == null)
		{
			GD.PushError($"Node2DGame: boss scene not found: {definition.ScenePath}");
			return false;
		}

		if (scene.Instantiate() is not BossEnemy boss)
		{
			GD.PushError($"Node2DGame: {definition.ScenePath} does not have a BossEnemy script attached.");
			return false;
		}

		boss.BossId = definition.Id;
		boss.BossDisplayName = definition.DisplayName;
		// Health rides the balance preset the same way elites do, so hardcore keeps its teeth and
		// casual does not turn the fight into a ten-minute chore.
		boss.Health = Mathf.RoundToInt(definition.Health * presetElitePowerScale);
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
			Text = definition.DisplayName,
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
		if (stageIntroLabel == null)
			return;

		var panel = stageIntroLabel.GetParent() as Control;
		if (panel != null)
		{
			panel.Visible = true;
			panel.Modulate = Colors.White;
		}

		stageIntroLabel.Text = $"{definition.DisplayName}\n{definition.Tagline}";
		stageIntroLabel.Modulate = new Color(1.0f, 0.86f, 0.72f, 1.0f);

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenInterval(3.6f);
		if (panel != null)
			tween.TweenProperty(panel, "modulate:a", 0.0f, 0.7f);
		tween.TweenProperty(stageIntroLabel, "modulate:a", 0.0f, 0.7f);
		tween.SetParallel(false);
		if (panel != null)
			tween.TweenCallback(Callable.From(() => panel.Hide()));
	}

	private void OnBossDefeated(string bossId)
	{
		CompleteBossVictory(bossId);
	}

	private void CompleteBossVictory(string bossId)
	{
		if (runFinished || bossVictoryPending)
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

	private void SpawnChestReward()
	{
		if (player == null || !IsInstanceValid(player))
			return;

		var chest = new ChestReward
		{
			Position = GetChestSpawnPositionAroundPlayer()
		};
		AddChild(chest);
	}

	public void OpenChestSelectionMenu()
	{
		if (player == null || !IsInstanceValid(player) || runFinished)
			return;

		if (chestSelectionMenu != null && IsInstanceValid(chestSelectionMenu))
			return;

		var menu = new ChestItemSelectionMenu();
		chestSelectionMenu = menu;
		AddChild(menu);

		var ownedItems = player.GetOwnedChestItems();
		// Relic Key is the only thing in the game that changes how many relics a chest offers.
		int offerCount = player != null && player.HasRelicKey ? 4 : 3;
		var options = ChestItemCatalog.GetChestItemOptions(ownedItems, spawnRng, offerCount);
		menu.SetOptions(options, ownedItems);
		menu.Connect(ChestItemSelectionMenu.SignalName.ItemSelected, Callable.From<string>(OnChestItemSelected));

		GetTree().Paused = true;
	}

	private void OnChestItemSelected(string itemId)
	{
		if (player != null && IsInstanceValid(player))
		{
			player.AddChestItem(itemId);
			RunEvents.RecordChestOpened();
			RunEvents.RecordChestItem(itemId);
			player.Heal(4);
			// Vaultguard's set bonus keys off a chest actually being opened, so it fires here
			// rather than once when the set completed. Called after AddChestItem so the chest
			// that finishes the set also pays out.
			player.OnChestOpened();
		}

		GetTree().Paused = false;
		if (chestSelectionMenu != null && IsInstanceValid(chestSelectionMenu))
		{
			chestSelectionMenu.QueueFree();
			chestSelectionMenu = null;
		}

		// Same hazard as the level-up menu: the chest is picked up mid-run, often with enemies
		// already touching the player, so give the same grace period on the way out.
		if (player != null && IsInstanceValid(player))
			player.GrantInvincibility(player.PostMenuInvincibilitySeconds);
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
		foreach (Enemy enemy in GetTree().GetNodesInGroup("enemies").OfType<Enemy>())
		{
			if (IsInstanceValid(enemy) && enemy.IsMiniBoss)
				count++;
		}

		return count;
	}

	private int GetSpawnBatchCount()
	{
		float minutesElapsed = Mathf.Max(0.0f, timeElapsed / 60.0f);
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
			int attempts = Math.Max(1, SpawnPositionRetries);
			for (int i = 0; i < attempts; i++)
			{
				Vector2 candidate = GetRandomSpawnPositionAroundPlayer();
				bool onWall = IsImpassablePosition(candidate);
				float nearestEnemyDistance = GetNearestEnemyDistance(candidate);
				if (!onWall && nearestEnemyDistance >= SpawnMinEnemySeparation)
					return candidate;

				if (!onWall && !haveNonWall)
				{
					haveNonWall = true;
					bestNonWall = candidate;
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
		if (levelPainter == null)
			return false;

		if (levelPainter.IsBlockedAtWorld(position))
			return true;

		return MazeNavigation.Active != null
			&& levelPainter.TerrainAtWorld(position) == LevelGenerator.WallTerrain;
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
		float exponent = Mathf.Lerp(1.0f, Mathf.Max(1.0f, SpawnForwardBias), committed);

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

		return (toEdge * SpawnEdgeMargin) + spawnRng.RandfRange(0f, SpawnEdgeDepth);
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
		foreach (Node2D enemy in GetTree().GetNodesInGroup("enemies").OfType<Node2D>())
		{
			if (!IsInstanceValid(enemy))
				continue;

			nearest = Math.Min(nearest, position.DistanceTo(enemy.GlobalPosition));
		}

		return nearest;
	}

	private (PackedScene Scene, float HealthMultiplier, bool IsElite) SelectEnemyForCurrentStage()
	{
		float minutesElapsed = timeElapsed / 60.0f;
		float roll = spawnRng.Randf();
		bool forceElite = ShouldSpawnElite();
		if (minutesElapsed < 5.0f && roll < 0.025f)
			return (booEnemyScene, 3.6f, forceElite);

		// The Hexer is the only enemy that attacks from range, so it belongs to every stage: a
		// back line is a role, not a biome. It takes its share on a roll of its own rather than off
		// the shared one, so each environment's table below keeps the full 0..1 spread it was tuned
		// on. Below-average health, because the threat is reaching it, not chewing through it.
		if (!IsSketchbookStage && timeElapsed >= HexerFirstSpawnSeconds && spawnRng.Randf() < HexerSpawnShare)
			return (hexerEnemyScene, 0.85f, forceElite);

		// The rooted turret. Also its own roll, for the same reason. Never an elite: the elite
		// treatment is more health, more speed and a bigger body, and on something that cannot move
		// that reads as a health sponge parked in the open rather than as a threat worth the fight.
		if (!IsSketchbookStage && timeElapsed >= SentryFirstSpawnSeconds && spawnRng.Randf() < SentrySpawnShare)
			return (skullSentryScene, 1.0f, false);

		// The charger. Slightly under-healthy, because its threat is the dash and a lunger that
		// also took a while to kill would just be a tank that occasionally moves fast.
		// Blobby in the Sketchbook. The kids drew a squash-and-stretch sheet and a leap sheet for
		// the same blob, which is a wind-up and a dash - so it is a LungerEnemy and it fills the
		// charger slot rather than needing one of its own.
		if (timeElapsed >= LungerFirstSpawnSeconds && spawnRng.Randf() < LungerSpawnShare)
			return (IsSketchbookStage ? kidBlobbyScene : lungerEnemyScene, 0.9f, forceElite);

		// Half health: the whole enemy is the question "can you kill it before it reaches you", and
		// the answer has to be yes often enough that trying is the right instinct.
		if (!IsSketchbookStage && timeElapsed >= ExploderFirstSpawnSeconds && spawnRng.Randf() < ExploderSpawnShare)
			return (exploderEnemyScene, 0.5f, forceElite);

		// Tanky, because it is meant to still be standing when the slam lands - the fight it wants
		// is one the player chooses to leave rather than one they burst down on the spot.
		// Pushy Dude in the Sketchbook, and it is a cone rather than a ring: it is called Pushy,
		// so it shoves the way it is facing.
		if (timeElapsed >= SlammerFirstSpawnSeconds && spawnRng.Randf() < SlammerSpawnShare)
			return (IsSketchbookStage ? kidPushyScene : slammerEnemyScene, 1.6f, forceElite);

		// Never an elite, for the Sentry's reason turned around: an elite summoner is not a better
		// fight, it is the same fight with more health in front of the thing making it worse.
		if (!IsSketchbookStage && timeElapsed >= SummonerFirstSpawnSeconds && spawnRng.Randf() < SummonerSpawnShare)
			return (summonerEnemyScene, 0.9f, false);

		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(Mathf.Clamp(Global.SelectedStageIdx, 0, 9));
		var pick = environmentProfile.Kind switch
		{
			// The forest is the first stage with a cast of its own. Wolves are the bulk of it and
			// they are FAST and light - the lesson of this biome is that you cannot outrun it, so
			// the common enemy is the one that keeps pace. Brambles are the slow wall you have to
			// go around, and wisps are the back line. Skeletons still turn up, but as the minority:
			// the forest was taken, and some of what he brought with him is still walking about.
			StageEnvironmentKind.Forest => roll < 0.40f ? (forestWolfScene, 0.85f) : roll < 0.66f ? (forestBrambleScene, 1.5f) : roll < 0.80f ? (forestWispScene, 0.8f) : roll < 0.92f ? (enemyScene, 1.0f) : (fastEnemyScene, 0.75f),
			StageEnvironmentKind.Castle => roll < 0.20f ? (tankEnemyScene, 2.2f) : roll < 0.56f ? (slowEnemyScene, 1.4f) : roll < 0.86f ? (enemyScene, 1.0f) : (fastEnemyScene, 0.75f),
			// The Orc used to hold a band in the three earthy tables, justified here as coming
			// from a different art pack to the skeletons and so earning its place on silhouette
			// alone. That argument died with the art: the cast is drawn to one contract now, and
			// the bruiser is already the heavy body it was standing in for. Its share folds back
			// into the bruiser band it was carved out of, so the earthy stages keep the same
			// weight of heavy enemies rather than quietly getting lighter.
			StageEnvironmentKind.Ruins => roll < 0.26f ? (tankEnemyScene, 2.2f) : roll < 0.58f ? (slowEnemyScene, 1.4f) : roll < 0.86f ? (enemyScene, 1.0f) : (fastEnemyScene, 0.75f),
			StageEnvironmentKind.Swamp => roll < 0.38f ? (slowEnemyScene, 1.4f) : roll < 0.64f ? (enemyScene, 1.0f) : roll < 0.86f ? (fastEnemyScene, 0.75f) : (tankEnemyScene, 2.2f),
			StageEnvironmentKind.Ice => roll < 0.36f ? (fastEnemyScene, 0.75f) : roll < 0.68f ? (enemyScene, 1.0f) : roll < 0.88f ? (slowEnemyScene, 1.4f) : (tankEnemyScene, 2.2f),
			StageEnvironmentKind.Desert => roll < 0.40f ? (fastEnemyScene, 0.75f) : roll < 0.72f ? (enemyScene, 1.0f) : roll < 0.90f ? (slowEnemyScene, 1.4f) : (tankEnemyScene, 2.2f),
			StageEnvironmentKind.Volcanic => roll < 0.24f ? (tankEnemyScene, 2.2f) : roll < 0.62f ? (enemyScene, 1.0f) : roll < 0.86f ? (fastEnemyScene, 0.75f) : (slowEnemyScene, 1.4f),
			// The Sketchbook. Smilers are the bulk and they are fast and flimsy; one-eyes are the
			// middle; zombies are the slow wall; vampires are the sprinters. Rockem takes the top
			// band as the heavy, and also stands in for the Warden as this chapter's miniboss.
			StageEnvironmentKind.Sketchbook => roll < 0.28f ? (kidSmilerScene, 0.8f) : roll < 0.48f ? (kidOneEyeScene, 1.0f) : roll < 0.65f ? (kidZombieScene, 1.5f) : roll < 0.80f ? (kidVampireScene, 0.85f) : roll < 0.93f ? (kidMessageScene, 1.1f) : (kidRockemScene, 2.4f),
			_ => minutesElapsed >= 5.0f
				? roll < 0.20f ? (fastEnemyScene, 0.75f) : roll < 0.35f ? (slowEnemyScene, 1.4f) : roll < 0.45f ? (tankEnemyScene, 2.2f) : roll < 0.58f ? (slowEnemyScene, 1.4f) : (enemyScene, 1.0f)
				: roll < 0.78f ? (enemyScene, 1.0f) : roll < 0.92f ? (fastEnemyScene, 0.75f) : (slowEnemyScene, 1.4f)
		};

		return (pick.Item1, pick.Item2, forceElite);
	}

	private bool ShouldSpawnElite()
	{
		if (timeElapsed < EliteStartTimeSeconds)
			return false;

		if (timeElapsed + spawnInterval < nextEliteSpawnTime)
			return false;

		nextEliteSpawnTime += GetEliteIntervalSeconds();
		return true;
	}

	private float GetEliteIntervalSeconds()
	{
		float minutesElapsed = Mathf.Max(0.0f, timeElapsed / 60.0f);
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

		// Procedural terrain defines its own square arena; clamp to just inside the painted area.
		if (proceduralActive && proceduralArenaHalfExtent > 0f)
		{
			return new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - proceduralArenaHalfExtent, stageOrigin.X + proceduralArenaHalfExtent),
				Mathf.Clamp(position.Y, stageOrigin.Y - proceduralArenaHalfExtent, stageOrigin.Y + proceduralArenaHalfExtent));
		}

		return StageEnvironmentCatalog.GetForStageIndex(Mathf.Clamp(Global.SelectedStageIdx, 0, 9)).Kind switch
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
		// The boss is already dead and the victory screen is one timer away. Dying in that gap is
		// not a loss, so the pending win stands.
		if (bossVictoryPending)
			return;

		FinishRunAndReward(RunOutcome.Defeat);
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
			ApplyBossVictoryUnlocks(saveManager.Data, result);
			saveManager.SaveGame();
		}
		ShowGameOver(result, reward, totalCurrency);
		GetTree().Paused = true;
	}

	// Beating a boss opens the next stage. The spell unlock that comes with it is handled by
	// AchievementDefinitions ("forest_cleared" and friends), which already match on BossId - this
	// only has to cover the stage gate, which nothing wrote to before now.
	private static void ApplyBossVictoryUnlocks(SaveData data, RunResult result)
	{
		if (data == null || result == null || result.Outcome != RunOutcome.Victory)
			return;

		BossDefinition defeatedBoss = BossCatalog.GetById(result.BossId);
		if (defeatedBoss == null || string.IsNullOrWhiteSpace(defeatedBoss.UnlocksStageId))
			return;

		GlobalStatsManager.UnlockStage(data, defeatedBoss.UnlocksStageId);
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
			StageId = $"stage_{Global.SelectedStageIdx}",
			FinalPlayerLevel = player?.CurrentLevel ?? 1,
			TimeSurvived = timeElapsed,
			EnemiesKilled = CalculateKillsEstimate(),
			BossId = bossId ?? string.Empty,
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
				IsLegendary = s.IsLegendary
			})
			.ToList();
	}

	private int CalculateKillsEstimate()
	{
		int activeEnemies = GetTree().GetNodesInGroup("enemies").Count;
		return Math.Max(0, totalEnemiesSpawned - activeEnemies);
	}

	private int CalculateArcaneReward(RunResult result)
	{
		int minutesSurvived = Mathf.FloorToInt(timeElapsed / 60.0f);
		int playerLevel = player?.CurrentLevel ?? 1;
		int baseReward = BaseArcaneReward + (minutesSurvived * ArcanePerMinuteSurvived) + (playerLevel * ArcanePerPlayerLevel);

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

		if (saveManager != null)
		{
			float dynamicMultiplier = GlobalStatsManager.GetDynamicArcaneRewardMultiplier(saveManager.Data, result, out string dynamicBreakdown);
			rewardMultiplier *= dynamicMultiplier;
			rewardMultiplier *= presetArcaneRewardScale;
			result.ArcaneRewardMultiplier = rewardMultiplier;
			result.ArcaneRewardBreakdown = $"{dynamicBreakdown}, Preset x{presetArcaneRewardScale:0.00}";
		}

		int computed = Mathf.RoundToInt(baseReward * rewardMultiplier);

		// The boss bonus is added after the multipliers rather than inside them: it is the flat
		// payout for clearing the level, and it should not swing with a pity streak or a preset.
		BossDefinition defeatedBoss = BossCatalog.GetById(result.BossId);
		if (result.Outcome == RunOutcome.Victory && defeatedBoss != null)
			computed += defeatedBoss.ArcaneVictoryBonus;

		return Math.Max(1, computed);
	}

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
		{
			gameOver.SetRunResult(result);
			gameOver.SetArcaneReward(reward, totalCurrency);
			gameOver.SetRunUnlocks(runUnlocks);
		}
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

		float eliteIn = Math.Max(0f, nextEliteSpawnTime - timeElapsed);
		int currentEnemies = GetTree().GetNodesInGroup("enemies").Count;
		int eliteCount = GetCurrentEliteEnemyCount();
		string presetName = GlobalStatsManager.GetBalancePresetDisplayName(GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data.BalancePresetId);
		debugOverlayLabel.Text =
			$"Debug (F3)\n" +
			$"Time {FormatTime(timeElapsed)} | Preset {presetName}\n" +
			$"SpawnInterval {spawnInterval:0.00}s | SpawnHealth {spawnHealth:0} | BatchWindow {spawnCountInBurstWindow}/{MaxSpawnsPerBurstWindow}\n" +
			$"Elites {eliteCount}/{MaxEliteEnemiesAlive} | NextEliteIn {eliteIn:0.0}s | SpawnGrace {spawnGraceRemaining:0.0}s\n" +
			$"Scales SI {presetSpawnIntervalScale:0.00} SH {presetSpawnHealthScale:0.00} EI {presetEliteIntervalScale:0.00} EP {presetElitePowerScale:0.00} AR {presetArcaneRewardScale:0.00}\n" +
			$"Enemies {currentEnemies}/{MaxEnemies} | TotalSpawned {totalEnemiesSpawned}";
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

		if (recyclesThisWindow >= MaxEnemyRecyclesPerSecond)
			return;

		recyclesThisWindow++;

		var newPos = FindRecycleSpawnPosition();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.ResetForRespawn(newPos, Mathf.RoundToInt(spawnHealth));
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
