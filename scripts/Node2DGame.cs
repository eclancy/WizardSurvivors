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
	[Export] public float SpawnBaseInterval { get; set; } = 2.0f;
	[Export] public float SpawnMinInterval { get; set; } = 0.2f;
	[Export] public float SpawnIntervalReductionPerMinute { get; set; } = 0.18f;
	[Export] public int SpawnBaseHealth { get; set; } = 20;
	[Export] public int SpawnHealthPerMinute { get; set; } = 18;
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
	private float fireTimer = 0f;
	private float fireInterval = 1f;
	private float spawnTimer = 0f;
	private float spawnInterval = 2f;
	private float spawnHealth = 20f;
	private float timeElapsed = 0f;
	private int totalEnemiesSpawned = 0;
	private bool tookDamageBeforeFiveMinutes = false;
	private bool runFinished = false;
	private int rerollsRemainingForCurrentLevelUp = 0;
	private Vector2 stageOrigin = Vector2.Zero;
	private RandomNumberGenerator spawnRng = new RandomNumberGenerator();
	private const string DecorPropGroup = "decor_props";
	private StageVisualTheme currentStageTheme = StageVisualTheme.Default;

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
		("stone_bulwark", "Stone Bulwark", "Passively reduces incoming damage.", "Earth, Metal"),
		("blur", "Blur", "Chance to avoid incoming hits entirely.", "Arcane, Wind"),
		("fortunes_favor", "Fortune's Favor", "Passively boosts Luck.", "Arcane, Light"),
		("haste", "Haste", "Periodically grants attack-speed and move-speed surges.", "Wind, Lightning")
	};

	public override void _Ready()
	{
		YSortEnabled = true;
		PlayRunMusic();
		spawnRng.Randomize();
		player = GetNode<Player>("CharacterBody2D"); // Strongly typed YES
		stageOrigin = player?.GlobalPosition ?? Vector2.Zero;
		ApplyStageTheme();
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
		}
		RefreshElementHud();
		EnsureEscapeMenuUi();
		BuildDecorProps();
		UpdateSpawnScaling();
	}

	private void PlayRunMusic()
	{
		var musicPlayer = GetNodeOrNull<Node>("/root/MusicPlayer");
		var music = ResourceLoader.Load<AudioStream>("res://assets/background_music.mp3");
		musicPlayer?.Call("PlayMusic", music);
	}

	private void ApplyStageTheme()
	{
		int stageIndex = Mathf.Clamp(Global.SelectedStageIdx, 0, 5);
		currentStageTheme = stageIndex switch
		{
			0 => new StageVisualTheme(
				"res://assets/ground_tile.png",
				new Rect2(),
				string.Empty,
				new Rect2(),
				1.0f,
				1.0f,
				new Color(0.88f, 1.0f, 0.9f, 1.0f),
				new Color(1f, 1f, 1f, 0f),
				0.28f,
				80,
				55,
				0),
			1 => new StageVisualTheme(
				"res://assets/imported/fantasy/source_mirror/Fantasy Dungeon tilesets/Fantasy_Dungeon_A1_darker.png",
				new Rect2(0, 144, 144, 144),
				string.Empty,
				new Rect2(),
				5.5f,
				1.0f,
				new Color(0.7f, 0.72f, 0.78f, 1.0f),
				new Color(1f, 1f, 1f, 0f),
				0.4f,
				0,
				0,
				44),
			2 => new StageVisualTheme(
				"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
				new Rect2(),
				string.Empty,
				new Rect2(),
				3.0f,
				1.0f,
				new Color(0.9f, 0.84f, 0.72f, 1.0f),
				new Color(1f, 1f, 1f, 0f),
				0.33f,
				0,
				8,
				52),
			3 => new StageVisualTheme(
				"res://assets/ground_tile.png",
				new Rect2(),
				"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
				new Rect2(),
				1.2f,
				1.5f,
				new Color(0.76f, 0.94f, 0.76f, 1.0f),
				new Color(0.32f, 0.55f, 0.34f, 0.24f),
				0.26f,
				118,
				18,
				0),
			4 => new StageVisualTheme(
				"res://assets/ground_tile.png",
				new Rect2(),
				"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
				new Rect2(),
				1.4f,
				1.1f,
				new Color(0.68f, 0.86f, 0.72f, 1.0f),
				new Color(0.24f, 0.43f, 0.28f, 0.2f),
				0.18f,
				30,
				98,
				8),
			_ => new StageVisualTheme(
				"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
				new Rect2(),
				"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
				new Rect2(),
				2.4f,
				1.35f,
				new Color(0.88f, 0.82f, 0.72f, 1.0f),
				new Color(0.48f, 0.42f, 0.34f, 0.22f),
				0.3f,
				12,
				8,
				78)
		};

		BushDecorCount = currentStageTheme.BushCount;
		TreeDecorCount = currentStageTheme.TreeCount;
		RuinDecorCount = currentStageTheme.RuinCount;

		var background = GetNodeOrNull<TextureRect>("CanvasLayer/Background");
		if (background != null)
		{
			background.Texture = LoadThemeTexture(currentStageTheme.BackgroundTexturePath, currentStageTheme.BackgroundRegion);
			background.Modulate = currentStageTheme.BackgroundModulate;
			if (background.Material is ShaderMaterial backgroundMaterial)
				backgroundMaterial.SetShaderParameter("texture_scale", currentStageTheme.BackgroundTextureScale);
		}

		var backgroundOverlay = GetNodeOrNull<TextureRect>("CanvasLayer/BackgroundOverlay");
		if (backgroundOverlay != null)
		{
			backgroundOverlay.Texture = string.IsNullOrEmpty(currentStageTheme.OverlayTexturePath)
				? null
				: LoadThemeTexture(currentStageTheme.OverlayTexturePath, currentStageTheme.OverlayRegion);
			backgroundOverlay.Modulate = currentStageTheme.OverlayModulate;
			backgroundOverlay.Visible = backgroundOverlay.Texture != null && backgroundOverlay.Modulate.A > 0.001f;
			if (backgroundOverlay.Material is ShaderMaterial overlayMaterial)
			{
				overlayMaterial.SetShaderParameter("texture_scale", currentStageTheme.OverlayTextureScale);
				overlayMaterial.SetShaderParameter("scroll_scale", 1.0f);
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

		return new AtlasTexture
		{
			Atlas = baseTexture,
			Region = region
		};
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
		int BushCount,
		int TreeCount,
		int RuinCount)
	{
		public static StageVisualTheme Default => new(
			"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
			new Rect2(),
			"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
			new Rect2(),
			1.0f,
			1.0f,
			new Color(1f, 1f, 1f, 1f),
			new Color(0.62f, 0.58f, 0.5f, 0.18f),
			0.35f,
			70,
			45,
			28);
	}

	private void BuildDecorProps()
	{
		if (!EnableDecorProps)
			return;

		ClearDecorProps();

		int stageIndex = Mathf.Clamp(Global.SelectedStageIdx, 0, 5);
		List<Texture2D> forestGroundAccents = LoadTexturesFromPaths(
			"res://assets/imported/fantasy/source_mirror/craftpix-net-974061-free-rocks-and-stones-top-down-pixel-art/PNG/Objects_separately/Rock1_grass_shadow1.png",
			"res://assets/imported/fantasy/source_mirror/craftpix-net-974061-free-rocks-and-stones-top-down-pixel-art/PNG/Objects_separately/Rock2_grass_shadow1.png");
		List<Texture2D> bushes = LoadTexturesFromFolder("res://assets/imported/fantasy/curated/map_props/bushes");
		List<Texture2D> trees = LoadTexturesFromFolder("res://assets/imported/fantasy/curated/map_props/trees");
		List<Texture2D> ruins = LoadTexturesFromFolder("res://assets/imported/fantasy/curated/map_props/ruins");

		switch (stageIndex)
		{
			case 0:
					CreateDecorSet(forestGroundAccents, ForestGroundAccentCount, 0.82f, 1.02f, false, false, -42, -34, true, GroundAccentClusterTargetSize, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, GroundAccentClusterOutlierChance);
					CreateDecorSet(bushes, BushDecorCount, 0.95f, 1.2f, false, false, -36, -22, true, BushClusterTargetSize, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, BushClusterOutlierChance);
					CreateDecorSet(trees, TreeDecorCount, 1.0f, 1.35f, false, false, -24, -8, true, TreeClusterTargetSize, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, TreeClusterOutlierChance);
				break;
			case 1:
					CreateDecorSet(ruins, RuinDecorCount, 0.95f, 1.15f, false, false, -26, -14, true, RuinClusterTargetSize, RuinClusterRadiusMin, RuinClusterRadiusMax, RuinClusterCenterSeparation, RuinClusterOutlierChance);
				break;
			case 2:
					CreateDecorSet(ruins, RuinDecorCount, 0.95f, 1.18f, false, false, -28, -14, true, RuinClusterTargetSize, RuinClusterRadiusMin, RuinClusterRadiusMax, RuinClusterCenterSeparation, RuinClusterOutlierChance);
					CreateDecorSet(trees, TreeDecorCount, 0.98f, 1.2f, false, false, -22, -10, true, TreeClusterTargetSize, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, TreeClusterOutlierChance);
				break;
			case 3:
					CreateDecorSet(forestGroundAccents, ForestGroundAccentCount, 0.78f, 1.0f, false, false, -42, -34, true, GroundAccentClusterTargetSize, GroundAccentClusterRadiusMin, GroundAccentClusterRadiusMax, GroundAccentClusterCenterSeparation, GroundAccentClusterOutlierChance);
					CreateDecorSet(bushes, BushDecorCount, 0.92f, 1.28f, false, false, -38, -22, true, BushClusterTargetSize + 4, BushClusterRadiusMin, BushClusterRadiusMax * 1.25f, BushClusterCenterSeparation * 0.85f, BushClusterOutlierChance);
					CreateDecorSet(trees, TreeDecorCount, 0.95f, 1.16f, false, false, -24, -10, true, TreeClusterTargetSize, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, TreeClusterOutlierChance);
				break;
			case 4:
					CreateDecorSet(bushes, BushDecorCount, 0.9f, 1.1f, false, false, -36, -22, true, BushClusterTargetSize, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, BushClusterOutlierChance);
					CreateDecorSet(trees, TreeDecorCount, 1.04f, 1.44f, false, false, -24, -8, true, TreeClusterTargetSize + 2, TreeClusterRadiusMin, TreeClusterRadiusMax * 1.2f, TreeClusterCenterSeparation * 0.88f, TreeClusterOutlierChance);
					CreateDecorSet(ruins, RuinDecorCount, 0.82f, 1.0f, false, false, -28, -16, true, RuinClusterTargetSize, RuinClusterRadiusMin, RuinClusterRadiusMax, RuinClusterCenterSeparation, RuinClusterOutlierChance);
				break;
			default:
					CreateDecorSet(ruins, RuinDecorCount, 0.9f, 1.28f, false, false, -30, -14, true, RuinClusterTargetSize + 2, RuinClusterRadiusMin, RuinClusterRadiusMax * 1.25f, RuinClusterCenterSeparation * 0.85f, RuinClusterOutlierChance);
					CreateDecorSet(bushes, BushDecorCount, 0.82f, 1.0f, false, false, -36, -24, true, BushClusterTargetSize, BushClusterRadiusMin, BushClusterRadiusMax, BushClusterCenterSeparation, BushClusterOutlierChance);
					CreateDecorSet(trees, TreeDecorCount, 0.9f, 1.05f, false, false, -24, -12, true, TreeClusterTargetSize, TreeClusterRadiusMin, TreeClusterRadiusMax, TreeClusterCenterSeparation, TreeClusterOutlierChance);
				break;
		}
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
		}
	}

	public override void _Process(double delta)
	{
		if (runFinished)
			return;

		if (GetTree().Paused)
			return;

		float d = (float)delta;
		fireTimer += d;
		spawnTimer += d;
		timeElapsed += d;
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
			if (currentEnemies.Count < MaxEnemies)
			{
				SpawnEnemy();
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
			xpCounter.Value = amount; // or player.CurrentXP if you have access, which we do
			xpCounter.MaxValue = player.XPToNextLevel;
		}
	}

	private void OnPlayerLevelGained()
	{
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
			if (levelUpMenu is LevelUpMenu typedMenu && player != null)
			{
				typedMenu.SetOptions(player.GetLevelUpOptions(), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts());
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
		typedMenu.SetOptions(player.GetLevelUpOptions(), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts());
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
				IsPassive = s.IsPassive
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

		bool changed = player.TryAddOrLevelSpell(weaponId);
		if (!changed)
		{
			GD.PrintErr($"Could not add or level spell for selection '{weaponId}'.");
		}

		RefreshElementHud();
		CloseLevelUpMenu();
	}

	private void OnSwapRequested(string newSpellId, string removedSpellId)
	{
		if (player == null)
			return;

		if (!player.RemoveEquippedSpell(removedSpellId))
		{
			GD.PrintErr($"Could not remove spell '{removedSpellId}' for swap.");
		}

		if (!player.TryAddOrLevelSpell(newSpellId))
		{
			GD.PrintErr($"Could not add spell '{newSpellId}' after swap.");
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

		RefreshElementHud();
		CloseLevelUpMenu();
	}

	private void OnSkipRequested()
	{
		CloseLevelUpMenu();
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

	private void ShowEscapeRunOverview()
	{
		ShowStructuredEscapeDetail("Run Details", sections =>
		{
			sections.AddChild(BuildRunSummarySection());
			sections.AddChild(BuildCharacterPassiveSection());
			sections.AddChild(BuildSpellListSection("Weapons", player?.GetEquippedSpells().Where(s => s != null && !s.IsPassive).ToList() ?? new List<SpellData>()));
			sections.AddChild(BuildSpellListSection("Equipped Passives", player?.GetEquippedSpells().Where(s => s != null && s.IsPassive).ToList() ?? new List<SpellData>()));
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
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel($"{spell.Name} Lv {spell.CurrentLevel}/{spell.MaxLevel}", Colors.White, 14));
		box.AddChild(BuildDetailLabel($"Damage {spell.GetDamageAtLevel(spell.CurrentLevel)} | Cooldown {spell.GetCooldownAtLevel(spell.CurrentLevel):0.##}s | Projectiles {spell.GetProjectileCountAtLevel(spell.CurrentLevel)} | Range {spell.GetRangeAtLevel(spell.CurrentLevel):0}", new Color(0.78f, 0.81f, 0.88f), 12));
		box.AddChild(BuildDetailLabel(FormatElementWeights(spell.GetElementWeights()), new Color(0.66f, 0.72f, 0.86f), 12));
		return BuildInfoFrame(box, new Color(0.14f, 0.15f, 0.20f, 0.96f), new Color(0.30f, 0.34f, 0.46f, 0.9f));
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
		var inactiveEntries = entries.Where(entry => entry.Tier <= 0).ToList();
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
				passives.AddChild(BuildSpellbookPassiveRow(passive.DisplayName, passive.Description, passive.Elements, unlocked));
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
		return BuildStateFrame(box, unlocked);
	}

	private Control BuildSpellbookPassiveRow(string name, string description, string elements, bool unlocked)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(BuildDetailLabel($"{name} - {(unlocked ? "Available" : "Locked")}", unlocked ? Colors.White : new Color(0.58f, 0.59f, 0.64f), 14));
		box.AddChild(BuildDetailLabel(elements, unlocked ? new Color(0.66f, 0.72f, 0.86f) : new Color(0.46f, 0.48f, 0.54f), 12));
		box.AddChild(BuildDetailLabel(description, unlocked ? new Color(0.78f, 0.81f, 0.88f) : new Color(0.50f, 0.51f, 0.56f), 12));
		return BuildStateFrame(box, unlocked);
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
		spawnInterval = Mathf.Max(SpawnMinInterval, SpawnBaseInterval - (minutesElapsed * SpawnIntervalReductionPerMinute));
		spawnHealth = SpawnBaseHealth + (minutesElapsed * SpawnHealthPerMinute);
	}

	private void SpawnEnemy()
	{
		var selection = SelectEnemyForCurrentStage();
		var enemy = selection.Scene.Instantiate<Node2D>();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.Health = Mathf.RoundToInt(spawnHealth * selection.HealthMultiplier);
		}

		enemy.Position = FindSeparatedSpawnPosition();
		AddChild(enemy);
		totalEnemiesSpawned++;
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

	private (PackedScene Scene, float HealthMultiplier) SelectEnemyForCurrentStage()
	{
		float minutesElapsed = timeElapsed / 60.0f;
		float roll = spawnRng.Randf();
		if (minutesElapsed < 5.0f && roll < 0.025f)
			return (booEnemyScene, 3.6f);

		return Global.SelectedStageIdx switch
		{
			1 => roll < 0.50f ? (fastEnemyScene, 0.75f) : roll < 0.75f ? (enemyScene, 1.0f) : roll < 0.90f ? (slowEnemyScene, 1.4f) : (tankEnemyScene, 2.2f),
			2 => roll < 0.45f ? (tankEnemyScene, 2.2f) : roll < 0.70f ? (slowEnemyScene, 1.4f) : roll < 0.90f ? (enemyScene, 1.0f) : (fastEnemyScene, 0.75f),
			_ => minutesElapsed >= 5.0f
				? roll < 0.20f ? (fastEnemyScene, 0.75f) : roll < 0.35f ? (slowEnemyScene, 1.4f) : roll < 0.45f ? (tankEnemyScene, 2.2f) : (enemyScene, 1.0f)
				: roll < 0.78f ? (enemyScene, 1.0f) : roll < 0.92f ? (fastEnemyScene, 0.75f) : (slowEnemyScene, 1.4f)
		};
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

		return Global.SelectedStageIdx switch
		{
			0 => new Vector2(position.X, Mathf.Clamp(position.Y, stageOrigin.Y - ForestHalfHeight, stageOrigin.Y + ForestHalfHeight)),
			1 => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - CastleHalfWidth, stageOrigin.X + CastleHalfWidth),
				Mathf.Clamp(position.Y, stageOrigin.Y - CastleHalfHeight, stageOrigin.Y + CastleHalfHeight)),
			2 => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - RuinsHalfSize, stageOrigin.X + RuinsHalfSize),
				Mathf.Clamp(position.Y, stageOrigin.Y - RuinsHalfSize, stageOrigin.Y + RuinsHalfSize)),
			_ => position
		};
	}

	private void OnPlayerDied()
	{
		FinishRunAndReward(RunOutcome.Defeat);
	}

	private void OnPlayerDamageTaken(int amount)
	{
		if (amount > 0 && timeElapsed <= 300f)
			tookDamageBeforeFiveMinutes = true;
	}

	public void FinishRunAndReward(RunOutcome outcome = RunOutcome.Defeat, string bossId = "")
	{
		if (runFinished)
			return;

		runFinished = true;
		RunResult result = BuildRunResult(outcome, bossId);

		int reward = CalculateArcaneReward();
		int totalCurrency = AwardArcaneEnergy(reward);
		GameStats.RecordRunResult(result);
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null && AchievementManager.ApplyRunAchievements(saveManager.Data, result))
			saveManager.SaveGame();
		ShowGameOver(result, reward, totalCurrency);
		GetTree().Paused = true;
	}

	private RunResult BuildRunResult(RunOutcome outcome, string bossId)
	{
		return new RunResult
		{
			Outcome = outcome,
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

	private int CalculateArcaneReward()
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
