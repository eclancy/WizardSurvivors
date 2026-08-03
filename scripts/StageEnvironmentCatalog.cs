using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

public enum StageEnvironmentKind
{
	Forest,
	Castle,
	Ruins,
	Ice,
	Desert,
	Volcanic,
	Swamp
}

public sealed class StageEnvironmentProfile
{
	public StageEnvironmentProfile(
		StageEnvironmentKind kind,
		string id,
		string displayName,
		string backgroundTexturePath,
		Rect2 backgroundRegion,
		string overlayTexturePath,
		Rect2 overlayRegion,
		float backgroundTextureScale,
		float overlayTextureScale,
		Color backgroundModulate,
		Color overlayModulate,
		float overlayScrollScale,
		int bushCount,
		int treeCount,
		int ruinCount,
		IReadOnlyList<string> sampleTilePaths,
		float backgroundTilePixelSize = 0f)
	{
		Kind = kind;
		Id = id;
		DisplayName = displayName;
		BackgroundTexturePath = backgroundTexturePath;
		BackgroundRegion = backgroundRegion;
		OverlayTexturePath = overlayTexturePath;
		OverlayRegion = overlayRegion;
		BackgroundTextureScale = backgroundTextureScale;
		OverlayTextureScale = overlayTextureScale;
		BackgroundModulate = backgroundModulate;
		OverlayModulate = overlayModulate;
		OverlayScrollScale = overlayScrollScale;
		BushCount = bushCount;
		TreeCount = treeCount;
		RuinCount = ruinCount;
		SampleTilePaths = sampleTilePaths;
		BackgroundTilePixelSize = backgroundTilePixelSize;
	}

	public StageEnvironmentKind Kind { get; }
	public string Id { get; }
	public string DisplayName { get; }
	public string BackgroundTexturePath { get; }
	public Rect2 BackgroundRegion { get; }
	public string OverlayTexturePath { get; }
	public Rect2 OverlayRegion { get; }
	public float BackgroundTextureScale { get; }
	public float OverlayTextureScale { get; }
	public Color BackgroundModulate { get; }
	public Color OverlayModulate { get; }
	public float OverlayScrollScale { get; }
	public int BushCount { get; }
	public int TreeCount { get; }
	public int RuinCount { get; }
	public IReadOnlyList<string> SampleTilePaths { get; }
	public float BackgroundTilePixelSize { get; }
}

public static class StageEnvironmentCatalog
{
	private static readonly Dictionary<StageEnvironmentKind, StageEnvironmentProfile> Profiles = new()
	{
		[StageEnvironmentKind.Forest] = new StageEnvironmentProfile(
			StageEnvironmentKind.Forest,
			"forest",
			"Forest",
			"res://assets/ground_tile.png",
			new Rect2(),
			"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
			new Rect2(),
			0.38f,
			0.12f,
			new Color(0.78f, 0.96f, 0.82f, 1.0f),
			new Color(0.24f, 0.46f, 0.28f, 0.16f),
			0.04f,
			24,
			20,
			0,
			new[]
			{
				"res://assets/imported/fantasy/curated/map_props/trees/trees_000.png",
				"res://assets/imported/fantasy/curated/map_props/bushes/bushes_000.png",
				"res://assets/ground_tile.png"
			})
		,
		[StageEnvironmentKind.Castle] = new StageEnvironmentProfile(
			StageEnvironmentKind.Castle,
			"castle",
			"Castle",
			"res://assets/imported/fantasy/curated/backgrounds/castle_floor_tile_alt_a.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.40f,
			0.10f,
			new Color(0.60f, 0.63f, 0.70f, 1.0f),
			new Color(0.30f, 0.32f, 0.38f, 0.0f),
			0.0f,
			0,
			0,
			28,
			new[]
			{
				"res://assets/imported/fantasy/curated/backgrounds/castle_floor_tile_alt_a.png",
				"res://assets/imported/fantasy/curated/map_props/ruins/ruins_000.png"
			},
			192f)
		,
		[StageEnvironmentKind.Ruins] = new StageEnvironmentProfile(
			StageEnvironmentKind.Ruins,
			"ruins",
			"Ruins",
			"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.35f,
			0.0f,
			new Color(0.92f, 0.84f, 0.72f, 1.0f),
			new Color(1f, 1f, 1f, 0f),
			0.0f,
			2,
			10,
			24,
			new[]
			{
				"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
				"res://assets/imported/fantasy/curated/map_props/ruins/ruins_000.png"
			})
		,
		[StageEnvironmentKind.Ice] = new StageEnvironmentProfile(
			StageEnvironmentKind.Ice,
			"ice",
			"Ice",
			"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
			new Rect2(),
			"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
			new Rect2(),
			0.34f,
			0.08f,
			new Color(0.80f, 0.90f, 0.99f, 1.0f),
			new Color(0.20f, 0.44f, 0.70f, 0.20f),
			0.02f,
			8,
			6,
			4,
			new[]
			{
				"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
				"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png"
			})
		,
		[StageEnvironmentKind.Desert] = new StageEnvironmentProfile(
			StageEnvironmentKind.Desert,
			"desert",
			"Desert",
			"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
			new Rect2(),
			"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
			new Rect2(),
			0.30f,
			0.06f,
			new Color(0.97f, 0.91f, 0.71f, 1.0f),
			new Color(0.70f, 0.54f, 0.26f, 0.16f),
			0.02f,
			4,
			4,
			8,
			new[]
			{
				"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
				"res://assets/imported/fantasy/curated/map_props/ruins/ruins_000.png"
			})
		,
		[StageEnvironmentKind.Volcanic] = new StageEnvironmentProfile(
			StageEnvironmentKind.Volcanic,
			"volcanic",
			"Volcanic",
			"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
			new Rect2(),
			"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
			new Rect2(),
			0.28f,
			0.08f,
			new Color(0.94f, 0.66f, 0.48f, 1.0f),
			new Color(0.72f, 0.24f, 0.16f, 0.20f),
			0.03f,
			2,
			2,
			16,
			new[]
			{
				"res://assets/imported/fantasy/curated/backgrounds/ground_rocks_tile.png",
				"res://assets/imported/fantasy/curated/map_props/ruins/ruins_000.png"
			})
		,
		[StageEnvironmentKind.Swamp] = new StageEnvironmentProfile(
			StageEnvironmentKind.Swamp,
			"swamp",
			"Swamp",
			"res://assets/ground_tile.png",
			new Rect2(),
			"res://assets/imported/fantasy/curated/backgrounds/ground_detail_overlay.png",
			new Rect2(),
			0.34f,
			0.08f,
			new Color(0.60f, 0.80f, 0.54f, 1.0f),
			new Color(0.20f, 0.34f, 0.20f, 0.16f),
			0.02f,
			18,
			12,
			2,
			new[]
			{
				"res://assets/ground_tile.png",
				"res://assets/imported/fantasy/curated/map_props/bushes/bushes_000.png"
			})
		,
	};

	public static StageEnvironmentProfile Get(StageEnvironmentKind kind)
	{
		return Profiles[kind];
	}

	public static StageEnvironmentProfile GetForStageIndex(int stageIndex)
	{
		return stageIndex switch
		{
			0 => Get(StageEnvironmentKind.Forest),
			1 => Get(StageEnvironmentKind.Castle),
			2 => Get(StageEnvironmentKind.Ruins),
			3 => Get(StageEnvironmentKind.Forest),
			4 => Get(StageEnvironmentKind.Forest),
			5 => Get(StageEnvironmentKind.Ruins),
			6 => Get(StageEnvironmentKind.Swamp),
			7 => Get(StageEnvironmentKind.Ice),
			8 => Get(StageEnvironmentKind.Desert),
			_ => Get(StageEnvironmentKind.Volcanic),
		};
	}
}
