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
	Swamp,
	Cave,
	/// <summary>The kids' chapter. Squared paper, and the only pale ground in the game.</summary>
	Sketchbook
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
			"res://assets/bonelight/tiles/grass_fill.png",
			new Rect2(),
			"",
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
				"res://assets/bonelight/world/crystal-violet.png",
				"res://assets/bonelight/world/crystal-yellow.png",
				"res://assets/ground_tile.png"
			})
		,
		// THE ONE CHAPTER THAT IS NOT A PLACE. Every other profile here is somewhere the dark
		// wizard holds; this is a page of squared paper with drawings on it, and it is deliberately
		// the only pale ground in the game so that the chapter announces itself as outside the
		// campaign before a word of it is read.
		//
		// No bushes, no trees and no ruins: its decor is the kids' own dog and message drawings,
		// scattered by Node2DGame.BuildDecorProps rather than drawn from the shared prop sets.
		[StageEnvironmentKind.Sketchbook] = new StageEnvironmentProfile(
			StageEnvironmentKind.Sketchbook,
			"sketchbook",
			"Sketchbook",
			"res://assets/kidsart/paper-tile.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			1.0f,
			0.0f,
			new Color(1.0f, 1.0f, 1.0f, 1.0f),
			new Color(1.0f, 1.0f, 1.0f, 0.0f),
			0.0f,
			0,
			0,
			0,
			new[] { "res://assets/kidsart/paper-tile.png" },
			48f)
		,
		[StageEnvironmentKind.Castle] = new StageEnvironmentProfile(
			StageEnvironmentKind.Castle,
			"castle",
			"Castle",
			"res://assets/bonelight/tiles/floor_stonetile_0.png",
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
				"res://assets/bonelight/world/crystal-yellow.png",
				"res://assets/bonelight/world/crystal-yellow.png"
			},
			48f)
		,
		[StageEnvironmentKind.Ruins] = new StageEnvironmentProfile(
			StageEnvironmentKind.Ruins,
			"ruins",
			"Ruins",
			"res://assets/bonelight/tiles/floor_graystone_0.png",
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
				"res://assets/bonelight/world/crystal-yellow.png",
				"res://assets/bonelight/world/crystal-yellow.png"
			}, 48f)
		,
		[StageEnvironmentKind.Ice] = new StageEnvironmentProfile(
			StageEnvironmentKind.Ice,
			"ice",
			"Ice",
			"res://assets/bonelight/tiles/ice_fill.png",
			new Rect2(),
			"",
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
				"res://assets/bonelight/world/crystal-yellow.png",
				"res://assets/bonelight/world/crystal-yellow.png"
			}, 48f)
		,
		[StageEnvironmentKind.Desert] = new StageEnvironmentProfile(
			StageEnvironmentKind.Desert,
			"desert",
			"Desert",
			"res://assets/bonelight/tiles/floor_sand_0.png",
			new Rect2(),
			"",
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
				"res://assets/bonelight/world/crystal-yellow.png",
				"res://assets/bonelight/world/crystal-yellow.png"
			}, 48f)
		,
		[StageEnvironmentKind.Volcanic] = new StageEnvironmentProfile(
			StageEnvironmentKind.Volcanic,
			"volcanic",
			"Volcanic",
			"res://assets/bonelight/tiles/floor_darktile_0.png",
			new Rect2(),
			"",
			new Rect2(),
			0.28f,
			0.08f,
			new Color(1.25f, 0.62f, 0.40f, 1.0f),
			new Color(0.72f, 0.24f, 0.16f, 0.20f),
			0.03f,
			2,
			2,
			16,
			new[]
			{
				"res://assets/bonelight/world/crystal-yellow.png",
				"res://assets/bonelight/world/crystal-yellow.png"
			}, 48f)
		,
		[StageEnvironmentKind.Swamp] = new StageEnvironmentProfile(
			StageEnvironmentKind.Swamp,
			"swamp",
			"Swamp",
			"res://assets/bonelight/tiles/dark_dirt_fill.png",
			new Rect2(),
			"",
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
				"res://assets/bonelight/world/crystal-yellow.png"
			})
		,
		// Placeholder art (issue #30): the dungeon floor set, darkened hard and given a cold cast,
		// so a cave reads as enclosed and lightless next to the Castle's grey stone. It reuses
		// those tiles rather than inventing a look, because the real cave set is art-backlog work
		// and a chapter that references a kind which does not exist is worse than one that borrows.
		[StageEnvironmentKind.Cave] = new StageEnvironmentProfile(
			StageEnvironmentKind.Cave,
			"cave",
			"Cave",
			"res://assets/bonelight/tiles/floor_darktile_1.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.40f,
			0.10f,
			new Color(0.34f, 0.40f, 0.52f, 1.0f),
			new Color(0.10f, 0.14f, 0.24f, 0.22f),
			0.0f,
			0,
			0,
			16,
			new[]
			{
				"res://assets/bonelight/world/crystal-blue.png",
				"res://assets/bonelight/world/crystal-violet.png"
			},
			48f)
	};

	public static StageEnvironmentProfile Get(StageEnvironmentKind kind)
	{
		return Profiles[kind];
	}

	// The chapter roster owns the index -> environment mapping now. This used to be its own switch
	// over ten indices that had no relation to the stage names in Node2DGame or the two entries in
	// StageSelection, so the three disagreed about what stage 3 even was.
	public static StageEnvironmentProfile GetForStageIndex(int stageIndex)
	{
		StageDefinition stage = StageCatalog.GetByIndex(stageIndex);
		return Get(stage?.EnvironmentKind ?? StageEnvironmentKind.Forest);
	}
}
