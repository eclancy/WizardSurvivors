using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WizardSurvivors.scripts;

public partial class Node2DGame : Node2D
{
	[Export] public int MaxEnemies { get; set; } = 100;
	[Export] public float TimerVictorySeconds { get; set; } = 900.0f;
	[Export] public float SpawnMinDistance { get; set; } = 250.0f;
	[Export] public float SpawnMaxDistance { get; set; } = 800.0f;
	[Export] public float SpawnMinEnemySeparation { get; set; } = 96.0f;
	[Export] public int SpawnPositionRetries { get; set; } = 8;
	[Export] public float SpawnBaseInterval { get; set; } = 2.2f;
	[Export] public float SpawnMinInterval { get; set; } = 0.28f;
	[Export] public float SpawnIntervalReductionPerMinute { get; set; } = 0.15f;
	[Export] public int SpawnBaseHealth { get; set; } = 20;
	[Export] public int SpawnHealthPerMinute { get; set; } = 16;
	[Export] public float EliteStartTimeSeconds { get; set; } = 135f;
	[Export] public float EliteHealthMultiplier { get; set; } = 2.45f;
	[Export] public float EliteSpeedMultiplier { get; set; } = 1.12f;
	[Export] public float EliteScaleMultiplier { get; set; } = 1.06f;
	[Export] public int MaxEliteEnemiesAlive { get; set; } = 2;
	[Export] public float PostLevelUpSpawnGraceSeconds { get; set; } = 1.2f;
	[Export] public float SpawnBurstWindowSeconds { get; set; } = 10f;
	[Export] public int MaxSpawnsPerBurstWindow { get; set; } = 18;
	[Export] public float ForestHalfHeight { get; set; } = 260.0f;
	[Export] public float CastleHalfWidth { get; set; } = 420.0f;
	[Export] public float CastleHalfHeight { get; set; } = 1400.0f;
	[Export] public float RuinsHalfSize { get; set; } = 1200.0f;
	[Export] public int BaseArcaneReward { get; set; } = 20;
	[Export] public int ArcanePerMinuteSurvived { get; set; } = 8;
	[Export] public int ArcanePerPlayerLevel { get; set; } = 2;
	[Export] public bool EnableDecorProps { get; set; } = true;
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

	private Player? player;
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
	private float spawnInterval = 2f;
	private float spawnHealth = 20f;
	private float nextEliteSpawnTime = 120f;
	private float spawnGraceRemaining = 0f;
	private float spawnBurstWindowTimer = 0f;
	private int spawnCountInBurstWindow = 0;
	private float presetSpawnIntervalScale = 1f;
	private float presetSpawnHealthScale = 1f;
	private float presetEliteIntervalScale = 1f;
	private float presetElitePowerScale = 1f;
	private float presetArcaneRewardScale = 1f;
	private float timeElapsed = 0f;
	private int totalEnemiesSpawned = 0;
	private bool tookDamageBeforeFiveMinutes = false;
	private bool runFinished = false;
	private int rerollsRemainingForCurrentLevelUp = 0;
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
	private PackedScene levelupMenuScene = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpMenu.tscn");
	private PackedScene gameOverScene = ResourceLoader.Load<PackedScene>("res://scenes/GameOverScreen.tscn");

	private static readonly (string Id, string Path)[] SpellbookResources = new[]
	{
		("magic_missile", "res://SpellData.tres"),
		("arcane_explosion", "res://SpellData_ArcaneExplosion.tres"),
		("spiritual_weapon", "res://SpellData_SpiritualWeapon.tres"),
		("fireball", "res://SpellData_Fireball.tres"),
		("frost_shard", "res://SpellData_FrostShard.tres"),
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
		("meteor_swarm", "res://SpellData_MeteorSwarm.tres")
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

	private static readonly Texture2D FallbackSpellHudIcon = GD.Load<Texture2D>("res://assets/organized/ui/ui-png-skills-icon-2.png");

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

			elementHudGrid = new GridContainer
			{
				Name = "ElementHudGrid",
				Position = new Vector2(7, 36),
				Columns = 4,
				CustomMinimumSize = new Vector2(430, 0)
			};
			elementHudGrid.AddThemeConstantOverride("h_separation", 4);
			elementHudGrid.AddThemeConstantOverride("v_separation", 4);
			uiOverlay.AddChild(elementHudGrid);

			var spellHudPanel = new PanelContainer
			{
				Name = "SelectedSpellHudPanel",
				AnchorLeft = 1f,
				AnchorRight = 1f,
				OffsetLeft = -356f,
				OffsetTop = 8f,
				OffsetRight = -8f,
				OffsetBottom = 76f
			};

			var spellHudStyle = new StyleBoxFlat
			{
				BgColor = new Color(0f, 0f, 0f, 0f),
				BorderColor = new Color(0f, 0f, 0f, 0f)
			};
			spellHudStyle.SetBorderWidthAll(0);
			spellHudStyle.SetCornerRadiusAll(6);
			spellHudStyle.SetContentMarginAll(6);
			spellHudPanel.AddThemeStyleboxOverride("panel", spellHudStyle);

			selectedSpellHudRow = new HBoxContainer
			{
				Name = "SelectedSpellHudRow",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.End
			};
			selectedSpellHudRow.AddThemeConstantOverride("separation", 6);
			spellHudPanel.AddChild(selectedSpellHudRow);
			uiOverlay.AddChild(spellHudPanel);

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
			runTimerLabel.AddThemeFontSizeOverride("font_size", 18);
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
			stageIntroLabel.AddThemeFontSizeOverride("font_size", 15);
			stageIntroPanel.AddChild(stageIntroLabel);
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
			onboardingTipLabel.AddThemeFontSizeOverride("font_size", 15);
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
			debugOverlayLabel.AddThemeFontSizeOverride("font_size", 13);
			uiOverlay.AddChild(debugOverlayLabel);
		}

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		showOnboardingTips = saveManager != null
			&& saveManager.Data.EnableGameplayOnboardingTips
			&& !saveManager.Data.HasSeenGameplayOnboarding;
		if (showOnboardingTips)
			ShowNextOnboardingTip(force: true);
		ConfigureXpCounterUi();
		CreateTileLegendUi();
		RefreshElementHud();
		EnsureEscapeMenuUi();
		BuildDecorProps();
		ShowStageIntroLabel();
		UpdateSpawnScaling();
	}

	private void ConfigureXpCounterUi()
	{
		var xpCounter = GetNodeOrNull<ProgressBar>("UIOverlay/XPCounter");
		if (xpCounter == null)
			return;

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
		var music = ResourceLoader.Load<AudioStream>("res://assets/background_music.mp3");
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

		var haze = CreateAmbientOverlay(canvas, baseTint, new Vector2(2000f, 1200f), Vector2.Zero);
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
		var glow = CreateAmbientOverlay(canvas, glowTint, new Vector2(1400f, 900f), new Vector2(120f, 80f));

		ColorRect? weatherOverlay = null;
		switch (environmentProfile.Kind)
		{
			case StageEnvironmentKind.Forest:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.90f, 0.96f, 0.86f, 0.025f), new Vector2(1800f, 1020f), new Vector2(-160f, -120f));
				break;
			case StageEnvironmentKind.Castle:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.06f, 0.08f, 0.12f, 0.028f), new Vector2(1400f, 760f), new Vector2(80f, 40f));
				break;
			case StageEnvironmentKind.Ruins:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.24f, 0.18f, 0.12f, 0.024f), new Vector2(1700f, 1000f), new Vector2(-40f, -60f));
				break;
			case StageEnvironmentKind.Swamp:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.12f, 0.24f, 0.16f, 0.032f), new Vector2(1900f, 1100f), new Vector2(-120f, -90f));
				break;
			case StageEnvironmentKind.Ice:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.94f, 0.97f, 1.00f, 0.018f), new Vector2(1600f, 900f), new Vector2(-80f, -40f));
				break;
			case StageEnvironmentKind.Desert:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.72f, 0.56f, 0.28f, 0.024f), new Vector2(1800f, 950f), new Vector2(-140f, -80f));
				break;
			case StageEnvironmentKind.Volcanic:
				weatherOverlay = CreateAmbientOverlay(canvas, new Color(0.54f, 0.20f, 0.10f, 0.026f), new Vector2(1600f, 880f), new Vector2(70f, 50f));
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

	private ColorRect CreateAmbientOverlay(CanvasLayer canvas, Color tint, Vector2? size = null, Vector2? position = null)
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
		if (size.HasValue)
			overlay.Size = size.Value;
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
			"res://assets/organized/level/tiles/lvl-tiles-fantasy-dungeon-tilesets-dungeon-floors-tileset-png-dungeon-floors-tileset.png",
			new Rect2(),
			"res://assets/organized/level/tiles/lvl-tiles-fantasy-dungeon-tilesets-dungeon-floors-tileset-png-dungeon-floors-tileset.png",
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

	private void BuildDecorProps()
	{
		if (!EnableDecorProps)
			return;

		ClearDecorProps();

		int stageIndex = Mathf.Clamp(Global.SelectedStageIdx, 0, 9);
		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(stageIndex);
		List<Texture2D> forestGroundAccents = LoadTexturesFromPaths(
			"res://assets/organized/level/props/lvl-props-rocks-and-stones-top-down-pixel-art-objects-separately-rock1-grass-shadow1.png",
			"res://assets/organized/level/props/lvl-props-rocks-and-stones-top-down-pixel-art-objects-separately-rock2-grass-shadow1.png");
		List<Texture2D> bushes = LoadTexturesFromPaths(
			"res://assets/organized/level/props/lvl-props-top-down-bushes-pixel-art-bush-simple1-1-2.png",
			"res://assets/organized/level/props/lvl-props-top-down-bushes-pixel-art-bush-simple2-1-2.png");
		List<Texture2D> trees = LoadTexturesFromPaths(
			"res://assets/organized/level/props/lvl-props-free-top-down-trees-pixel-art-autumn-tree1.png",
			"res://assets/organized/level/props/lvl-props-free-top-down-trees-pixel-art-autumn-tree2.png");
		List<Texture2D> ruins = LoadTexturesFromPaths(
			"res://assets/organized/level/props/lvl-props-top-down-crystals-pixel-art-yellow-crystal4.png",
			"res://assets/organized/level/props/lvl-props-top-down-crystals-pixel-art-violet-crystal3.png");
		List<Texture2D> swampReeds = LoadTexturesFromPaths(
			"res://assets/organized/level/props/lvl-props-top-down-bushes-pixel-art-bush-simple1-1-2.png",
			"res://assets/organized/level/props/lvl-props-top-down-bushes-pixel-art-bush-simple2-1-2.png");
		List<Texture2D> volcanicAsh = LoadTexturesFromPaths(
			"res://assets/organized/level/props/lvl-props-rocks-and-stones-top-down-pixel-art-objects-separately-rock1-grass-shadow1.png",
			"res://assets/organized/level/props/lvl-props-rocks-and-stones-top-down-pixel-art-objects-separately-rock2-grass-shadow1.png");

		switch (environmentProfile.Kind)
		{
			case StageEnvironmentKind.Forest:
				CreateDecorSet(bushes, Math.Max(18, environmentProfile.BushCount), 0.90f, 1.10f, false, false, -36, -22, true, 8, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, 0.04f);
				CreateDecorSet(trees, Math.Max(20, environmentProfile.TreeCount + 12), 1.00f, 1.22f, false, false, -24, -8, true, 8, TreeClusterRadiusMin, TreeClusterRadiusMax * 0.85f, TreeClusterCenterSeparation * 0.62f, 0.05f);
				CreateDecorSet(forestGroundAccents, 22, 0.88f, 1.02f, false, false, -42, -34, true, 4, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, 0.05f);
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

	private void ShowStageIntroLabel()
	{
		if (stageIntroLabel == null)
			return;

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
		tween.TweenInterval(2.4f);
		tween.TweenProperty(stageIntroLabel, "modulate:a", 0.0f, 0.6f);
	}

	private string GetCurrentStageName()
	{
		return Mathf.Clamp(Global.SelectedStageIdx, 0, 9) switch
		{
			0 => "Enchanted Forest",
			1 => "Cursed Castle",
			2 => "Mystic Ruins",
			3 => "Bramble Thicket",
			4 => "Elderwood Grove",
			5 => "Broken Highlands",
			6 => "Moonlit Marsh",
			7 => "Frostbound Hollow",
			8 => "Sunscorched Dunes",
			_ => "Ashen Crater"
		};
	}

	private string GetCurrentStageFlavorText()
	{
		return Mathf.Clamp(Global.SelectedStageIdx, 0, 9) switch
		{
			0 => "A bright woodland trail full of ancient trees and dense brush.",
			1 => "Stone corridors and crumbling keeps turn the battlefield into a grim choke-point.",
			2 => "Broken spires and shattered walls form a harsh field of rubble and ancient danger.",
			3 => "Thick brambles and tangled brush leave little room for clean movement.",
			4 => "A sacred grove of towering trunks and hushed paths feels older than the kingdom.",
			5 => "Fractured ridgelines and exposed rock make this a brutal, open battlefield.",
			6 => "Reeds whisper over murky water while bog lanterns glow through the fog.",
			7 => "Ice-slick ground and wind-carved ridges make every step a balancing act.",
			8 => "A blistering desert expanse of cracked earth and long shadows.",
			_ => "Blackened ground and glowing embers mark this infernal battlefield of heat and ruin."
		};
	}

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
			float scale = spawnRng.RandfRange(minScale, maxScale);
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

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
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

		UpdateSpawnScaling();
		ClampPlayerToStageBounds();
		if (TimerVictorySeconds > 0f && timeElapsed >= TimerVictorySeconds)
		{
			FinishRunAndReward(RunOutcome.Victory);
			return;
		}

		if (fireTimer >= fireInterval)
		{
			// TODO: call fire logic
			fireTimer = 0f;
		}
		if (spawnTimer >= spawnInterval)
		{
			var currentEnemies = GetTree().GetNodesInGroup("enemies");
			if (spawnGraceRemaining <= 0f && currentEnemies.Count < MaxEnemies)
			{
				int availableSlots = MaxEnemies - currentEnemies.Count;
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
			// World-space offset drives the seamless tiled ground (1:1 with camera).
			material.SetShaderParameter("world_offset", camera.GlobalPosition);
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
			menuScript?.Connect("RemoveRequested", new Callable(this, nameof(OnRemoveRequested)));
			menuScript?.Connect("SkipRequested", new Callable(this, nameof(OnSkipRequested)));
		}
		if (levelUpMenu != null)
		{
			// Show the menu first
			levelUpMenu.Show();
			rerollsRemainingForCurrentLevelUp = player?.RerollsPerLevelUp ?? 0;
			int optionCount = player?.IsPlaytestModeEnabled == true ? 4 : 3;
			if (levelUpMenu is LevelUpMenu typedMenu && player != null)
			{
				typedMenu.SetOptions(player.GetLevelUpOptions(optionCount), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts());
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
		int optionCount = player.IsPlaytestModeEnabled ? 4 : 3;
		typedMenu.SetOptions(player.GetLevelUpOptions(optionCount), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts());
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
				Icon = s.Icon
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

		SpellData existing = owner.GetEquippedSpells()
			.FirstOrDefault(s => s != null && s.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));

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
			if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape && escapeMenu != null && escapeMenu.Visible)
			{
				OnEscapeResumePressed();
				escapeRoot.AcceptEvent();
			}
		};
		escapeRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		escapeMenu.AddChild(escapeRoot);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.02f, 0.025f, 0.82f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		escapeRoot.AddChild(dim);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(900, 700),
			ProcessMode = ProcessModeEnum.Always
		};
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = -450;
		panel.OffsetTop = -350;
		panel.OffsetRight = 450;
		panel.OffsetBottom = 350;
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.10f, 0.10f, 0.13f, 0.98f);
		style.BorderColor = new Color(0.36f, 0.40f, 0.52f, 0.9f);
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		panel.AddThemeStyleboxOverride("panel", style);
		escapeRoot.AddChild(panel);

		var outer = new HBoxContainer();
		outer.AddThemeConstantOverride("separation", 18);
		panel.AddChild(outer);

		var nav = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(220, 0),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		nav.AddThemeConstantOverride("separation", 8);
		outer.AddChild(nav);

		var title = new Label
		{
			Text = "Paused",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 30);
		nav.AddChild(title);

		nav.AddChild(BuildEscapeNavGroup("Run", MakeEscapeButton("Resume", OnEscapeResumePressed), MakeEscapeButton("Restart Run", OnEscapeRestartPressed), MakeEscapeButton("Quit", OnEscapeQuitPressed)));
		nav.AddChild(BuildEscapeNavGroup("Reference", MakeEscapeTabButton("Run Details", ShowEscapeRunOverview), MakeEscapeTabButton("Spellbook Pool", ShowEscapeSpellbook), MakeEscapeTabButton("Achievements", ShowEscapeAchievements)));
		nav.AddChild(BuildEscapeNavGroup("Settings", MakeEscapeTabButton("Options", ShowEscapeOptions)));

		var details = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		details.AddThemeConstantOverride("separation", 8);
		outer.AddChild(details);

		escapeDetailTitle = new Label
		{
			Text = "Run Details",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		escapeDetailTitle.AddThemeFontSizeOverride("font_size", 24);
		details.AddChild(escapeDetailTitle);

		escapeDetailScroll = new ScrollContainer
		{
			Visible = false,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		details.AddChild(escapeDetailScroll);

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
		escapeDetailText.AddThemeFontSizeOverride("normal_font_size", 15);
		details.AddChild(escapeDetailText);
	}

	private Button MakeEscapeButton(string text, Action pressed)
	{
		var button = new Button
		{
			Text = text,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(200, 44),
			ProcessMode = ProcessModeEnum.Always
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		ApplyEscapeButtonStyle(button, false);
		button.Pressed += pressed;
		return button;
	}

	private Button MakeEscapeTabButton(string text, Action pressed)
	{
		Button button = MakeEscapeButton(text, pressed);
		escapeTabButtons[text] = button;
		return button;
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

	private Control BuildEscapeNavGroup(string title, params Button[] buttons)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.08f, 0.09f, 0.13f, 0.88f);
		style.BorderColor = new Color(0.30f, 0.34f, 0.46f, 0.92f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		style.SetContentMarginAll(8);
		panel.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 7);
		panel.AddChild(box);

		var header = BuildGroupLabel(title);
		header.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(header);
		foreach (Button button in buttons)
			box.AddChild(button);

		return panel;
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
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
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

			var muteToggle = new CheckButton { Text = "Mute All Audio" };
			int masterBus = AudioServer.GetBusIndex("Master");
			muteToggle.ButtonPressed = masterBus >= 0 && AudioServer.IsBusMute(masterBus);
			muteToggle.Toggled += pressed =>
			{
				int idx = AudioServer.GetBusIndex("Master");
				if (idx >= 0)
					AudioServer.SetBusMute(idx, pressed);
			};
			box.AddChild(muteToggle);

			sections.AddChild(BuildEscapeSection("Audio", box));
		});
	}

	private Control BuildEscapeVolumeRow(string label, string busName)
	{
		var row = new VBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		row.AddChild(BuildDetailLabel(label, new Color(0.82f, 0.84f, 0.9f), 14));

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
		slider.ValueChanged += value =>
		{
			int idx = AudioServer.GetBusIndex(busName);
			if (idx >= 0)
				AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb((float)value));
		};
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
		box.AddChild(BuildDetailLabel($"{spell.Name} Lv {spell.CurrentLevel}/{spell.MaxLevel}", Colors.White, 14));
		string description = string.IsNullOrWhiteSpace(spell.Description) ? "No description." : spell.Description;
		box.AddChild(BuildDetailLabel(description, new Color(0.82f, 0.84f, 0.90f), 12));
		box.AddChild(BuildDetailLabel($"Damage {spell.GetDamageAtLevel(spell.CurrentLevel)} | Cooldown {spell.GetCooldownAtLevel(spell.CurrentLevel):0.##}s | Projectiles {spell.GetProjectileCountAtLevel(spell.CurrentLevel)} | Range {spell.GetRangeAtLevel(spell.CurrentLevel):0}", new Color(0.78f, 0.81f, 0.88f), 12));
		box.AddChild(BuildDetailLabel(FormatElementWeights(spell.GetElementWeights()), new Color(0.66f, 0.72f, 0.86f), 12));
		box.AddChild(BuildDetailLabel($"Classification: {FormatSpellClassification(spell)}", new Color(0.60f, 0.82f, 0.96f), 12));
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
		textBox.AddChild(BuildDetailLabel(status, textColor, 13));
		textBox.AddChild(BuildDetailLabel(effect, textColor, 12));
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
		var name = BuildDetailLabel(element.ToString(), textColor, 13);
		name.HorizontalAlignment = HorizontalAlignment.Center;
		var progress = BuildDetailLabel(ElementPassiveDescriptions.GetProgressLabel(count), textColor, 12);
		progress.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(name);
		box.AddChild(progress);
		return BuildInfoFrame(box, badgeBackground, badgeBorder);
	}

	private Label BuildGroupLabel(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", 12);
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
		header.AddThemeFontSizeOverride("font_size", 17);
		header.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.98f));
		box.AddChild(header);
		box.AddChild(content);
		return panel;
	}

	private Control BuildStatTile(string label, string value)
	{
		var box = new VBoxContainer { CustomMinimumSize = new Vector2(132, 58), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel(label, new Color(0.66f, 0.70f, 0.80f), 12));
		box.AddChild(BuildDetailLabel(value, Colors.White, 18));
		return BuildInfoFrame(box, new Color(0.13f, 0.14f, 0.20f, 0.98f), new Color(0.34f, 0.38f, 0.52f, 0.95f));
	}

	private Control BuildInfoRow(string text, Color background, Color border, Color textColor)
	{
		return BuildInfoFrame(BuildDetailLabel(text, textColor, 13), background, border);
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

	private Label BuildDetailLabel(string text, Color color, int fontSize = 14)
	{
		var label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
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
				bool unlocked = saveManager?.Data.UnlockedAchievementIds.Any(id => id.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase)) ?? false;
				Control row = BuildAchievementRow(achievement, unlocked);
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
		box.AddChild(BuildDetailLabel($"{spell.Name} - {(unlocked ? "Available" : "Locked")}", unlocked ? Colors.White : new Color(0.58f, 0.59f, 0.64f), 14));
		box.AddChild(BuildDetailLabel(FormatElementWeights(spell.GetElementWeights()), unlocked ? new Color(0.66f, 0.72f, 0.86f) : new Color(0.46f, 0.48f, 0.54f), 12));
		box.AddChild(BuildDetailLabel($"Classification: {FormatSpellClassification(spell)}", unlocked ? new Color(0.60f, 0.82f, 0.96f) : new Color(0.45f, 0.56f, 0.64f), 12));
		return BuildStateFrame(box, unlocked);
	}

	private Control BuildSpellbookPassiveRow(string spellId, string name, string description, string elements, bool unlocked)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel($"{name} - {(unlocked ? "Available" : "Locked")}", unlocked ? Colors.White : new Color(0.58f, 0.59f, 0.64f), 14));
		box.AddChild(BuildDetailLabel(elements, unlocked ? new Color(0.66f, 0.72f, 0.86f) : new Color(0.46f, 0.48f, 0.54f), 12));
		box.AddChild(BuildDetailLabel($"Classification: {FormatPassiveClassification(spellId)}", unlocked ? new Color(0.60f, 0.82f, 0.96f) : new Color(0.45f, 0.56f, 0.64f), 12));
		box.AddChild(BuildDetailLabel(description, unlocked ? new Color(0.78f, 0.81f, 0.88f) : new Color(0.50f, 0.51f, 0.56f), 12));
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

	private Control BuildAchievementRow(AchievementDefinition achievement, bool complete)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel($"{achievement.DisplayName} - {(complete ? "Complete" : "In Progress")}", complete ? Colors.White : new Color(0.72f, 0.74f, 0.80f), 14));
		box.AddChild(BuildDetailLabel(achievement.Description, complete ? new Color(0.78f, 0.84f, 0.78f) : new Color(0.66f, 0.68f, 0.74f), 12));
		box.AddChild(BuildDetailLabel(achievement.RewardText, complete ? new Color(0.78f, 0.88f, 0.72f) : new Color(0.54f, 0.58f, 0.66f), 12));
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

		return string.Join(", ", weights.OrderBy(p => p.Key.ToString()).Select(p => p.Value > 1 ? $"{p.Key} x{p.Value}" : p.Key.ToString()));
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
			"cone_of_cold" => SpellTargetingMode.DirectionalCone,
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

	private void CloseLevelUpMenu()
	{
		// Unpause the game and remove the menu
		GetTree().Paused = false;
		if (levelUpMenu != null)
		{
			levelUpMenu.QueueFree();
			levelUpMenu = null;
		}
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
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", GetReadableTextColor(baseColor));
		panel.AddChild(label);
		return panel;
	}

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.R * 0.299f) + (background.G * 0.587f) + (background.B * 0.114f);
		return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.07f) : Colors.White;
	}

	private void UpdateSpawnScaling()
	{
		float minutesElapsed = Mathf.Max(0.0f, timeElapsed / 60.0f);
		spawnInterval = Mathf.Max(SpawnMinInterval, (SpawnBaseInterval - (minutesElapsed * SpawnIntervalReductionPerMinute)) * presetSpawnIntervalScale);
		spawnHealth = (SpawnBaseHealth + (minutesElapsed * SpawnHealthPerMinute)) * presetSpawnHealthScale;
	}

	private void SpawnEnemy()
	{
		var selection = SelectEnemyForCurrentStage();
		if (selection.IsElite && GetCurrentEliteEnemyCount() >= MaxEliteEnemiesAlive)
			selection = (selection.Scene, selection.HealthMultiplier, false);

		var enemy = selection.Scene.Instantiate<Node2D>();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.Health = Mathf.RoundToInt(spawnHealth * selection.HealthMultiplier);
			if (selection.IsElite)
			{
				typedEnemy.Health = Mathf.RoundToInt(typedEnemy.Health * EliteHealthMultiplier * presetElitePowerScale);
				typedEnemy.Speed *= EliteSpeedMultiplier * Mathf.Lerp(1.0f, presetElitePowerScale, 0.55f);
				typedEnemy.Scale *= EliteScaleMultiplier;
				typedEnemy.IsMiniBoss = true;
			}
		}

		enemy.Position = FindSeparatedSpawnPosition();
		AddChild(enemy);
		totalEnemiesSpawned++;
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
		if (minutesElapsed < 3.0f)
			return 1;
		if (minutesElapsed < 7.0f)
			return roll < 0.22f ? 2 : 1;
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
			int attempts = Math.Max(1, SpawnPositionRetries);
			for (int i = 0; i < attempts; i++)
			{
				Vector2 candidate = GetRandomSpawnPositionAroundPlayer();
				float nearestEnemyDistance = GetNearestEnemyDistance(candidate);
				if (nearestEnemyDistance >= SpawnMinEnemySeparation)
					return candidate;

				if (nearestEnemyDistance > bestDistance)
				{
					bestDistance = nearestEnemyDistance;
					bestCandidate = candidate;
				}
			}

			return bestCandidate;
		}

		var screenSize = GetViewportRect().Size;
		return new Vector2((float)spawnRng.Randf() * screenSize.X, -50);
	}

	private Vector2 GetRandomSpawnPositionAroundPlayer()
	{
		var angle = spawnRng.Randf() * (Mathf.Pi * 2.0f);
		var radius = spawnRng.RandfRange(SpawnMinDistance, SpawnMaxDistance);
		return ClampPositionToStageBounds(player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
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

		var environmentProfile = StageEnvironmentCatalog.GetForStageIndex(Mathf.Clamp(Global.SelectedStageIdx, 0, 9));
		var pick = environmentProfile.Kind switch
		{
			StageEnvironmentKind.Castle => roll < 0.20f ? (tankEnemyScene, 2.2f) : roll < 0.56f ? (slowEnemyScene, 1.4f) : roll < 0.86f ? (enemyScene, 1.0f) : (fastEnemyScene, 0.75f),
			StageEnvironmentKind.Ruins => roll < 0.26f ? (tankEnemyScene, 2.2f) : roll < 0.58f ? (slowEnemyScene, 1.4f) : roll < 0.86f ? (enemyScene, 1.0f) : (fastEnemyScene, 0.75f),
			StageEnvironmentKind.Swamp => roll < 0.24f ? (slowEnemyScene, 1.4f) : roll < 0.64f ? (enemyScene, 1.0f) : roll < 0.86f ? (fastEnemyScene, 0.75f) : (tankEnemyScene, 2.2f),
			StageEnvironmentKind.Ice => roll < 0.36f ? (fastEnemyScene, 0.75f) : roll < 0.68f ? (enemyScene, 1.0f) : roll < 0.88f ? (slowEnemyScene, 1.4f) : (tankEnemyScene, 2.2f),
			StageEnvironmentKind.Desert => roll < 0.40f ? (fastEnemyScene, 0.75f) : roll < 0.72f ? (enemyScene, 1.0f) : roll < 0.90f ? (slowEnemyScene, 1.4f) : (tankEnemyScene, 2.2f),
			StageEnvironmentKind.Volcanic => roll < 0.24f ? (tankEnemyScene, 2.2f) : roll < 0.62f ? (enemyScene, 1.0f) : roll < 0.86f ? (fastEnemyScene, 0.75f) : (slowEnemyScene, 1.4f),
			_ => minutesElapsed >= 5.0f
				? roll < 0.20f ? (fastEnemyScene, 0.75f) : roll < 0.35f ? (slowEnemyScene, 1.4f) : roll < 0.45f ? (tankEnemyScene, 2.2f) : (enemyScene, 1.0f)
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
		FinishRunAndReward(RunOutcome.Defeat);
	}

	private void OnPlayerDamageTaken(int amount)
	{
		GameStats.RecordDamageTaken(amount);
		PlayPlayerDamageFlash();

		if (amount > 0 && timeElapsed <= 300f)
			tookDamageBeforeFiveMinutes = true;
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

		int reward = CalculateArcaneReward(result);
		int totalCurrency = AwardArcaneEnergy(reward);
		WritePlaytestRunLog(result, reward, totalCurrency);
		GameStats.RecordRunResult(result);
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null)
		{
			saveManager.Data.RecordRunTelemetry(result);
			saveManager.Data.HasSeenGameplayOnboarding = true;
			AchievementManager.ApplyRunAchievements(saveManager.Data, result);
			saveManager.SaveGame();
		}
		ShowGameOver(result, reward, totalCurrency);
		GetTree().Paused = true;
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
			EquippedSpells = BuildSpellSnapshot()
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

	public void RespawnEnemy(Node enemy)
	{
		if (player == null) return;
		var newPos = FindSeparatedSpawnPosition();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.ResetForRespawn(newPos, Mathf.RoundToInt(spawnHealth));
		}
		else if (enemy is Node2D n)
		{
			n.Position = newPos;
		}
	}
}

