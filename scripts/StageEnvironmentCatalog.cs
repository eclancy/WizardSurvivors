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
	Sketchbook,
	/// <summary>The Fallow Fields (stage_9): stubble, hedgerow and millpond. Its own ground is Phase 2 (.ai/campaign.md).</summary>
	Fields,
	/// <summary>The Broken Circle (stage_10): heather, granite and standing stone. Its own ground is Phase 2.</summary>
	Heath,
	/// <summary>The Last Bridge (stage_11): a diagonal deck over the abyss. Its own ground is piece C1 (.ai/endgame-castle.md 3).</summary>
	Bridge,
	/// <summary>The Court (stage_12): his halls, one fixed building. Its own ground is piece C3 (.ai/endgame-castle.md 5).</summary>
	Court,
	/// <summary>The Throne (stage_13): one closed hall. Its own ground is piece C4 (.ai/endgame-castle.md 6).</summary>
	Throne,
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
		float backgroundTilePixelSize = 0f,
		int propCount = 0)
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
		PropCount = propCount;
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

	/// <summary>
	/// How many themed props this chapter scatters, or 0 to use Node2DGame.CuratedPropCount.
	/// </summary>
	/// <remarks>
	/// One global number could not serve every chapter: the flora theme is three sprites over an
	/// 8400-unit field, and at the shared default of 42 the Enchanted Forest put roughly one prop
	/// on screen at a time. A castle full of torture racks wants far fewer than a wood wants ferns.
	/// </remarks>
	public int PropCount { get; }

	// THE CHAPTER'S LOOK, AS DATA. Each of these was an arm in a switch over StageEnvironmentKind in
	// Node2DGame - five switches with one line per environment - and a new chapter had to add a
	// line to every one of them, beside the lines another new chapter was adding. Held here, a
	// chapter's whole look is its own block below, and two chapters built in parallel never edit
	// the same lines (.ai/campaign.md, the Phase 2 split). The defaults are the old switches'
	// default arms, so an environment that sets nothing looks exactly as it did.

	/// <summary>The slow-breathing wash over the whole screen (Node2DGame.ApplyAmbientStageEffects).</summary>
	public Color HazeTint { get; init; } = new Color(0.10f, 0.10f, 0.14f, 0.08f);

	/// <summary>The drifting glow layered over the haze.</summary>
	public Color GlowTint { get; init; } = new Color(0.12f, 0.12f, 0.18f, 0.04f);

	/// <summary>A third, slower drifting layer - mist, dust, snow-light. Null for none.</summary>
	public Color? WeatherTint { get; init; }

	/// <summary>Where the weather layer starts its drift.</summary>
	public Vector2 WeatherOffset { get; init; }

	/// <summary>The colour of the chapter's name on the banner a run opens with.</summary>
	public Color IntroTint { get; init; } = new Color(0.96f, 0.98f, 1.0f, 1.0f);

	/// <summary>Which water art (tools/art/water.py): "forest", "swamp", "frozen", or "pool".</summary>
	public string WaterStyle { get; init; } = "pool";

	/// <summary>
	/// Whether spike plates are scattered. Built things: only where somebody built them
	/// (Node2DGame.BuildStageHazards). An open natural chapter is shaped by its enemies, not traps.
	/// </summary>
	public bool HazardSpikes { get; init; }

	/// <summary>
	/// Share of hazards that are flame vents rather than spikes; 0 for none. A vent is an iron grate set
	/// in a floor, so only an indoor chapter may have one (the Cursed Dungeon today).
	/// </summary>
	public float HazardFlameChance { get; init; }

	/// <summary>Whether the rare flower-pen landmark may appear: green chapters only.</summary>
	public bool HasLandmarks { get; init; }

	/// <summary>
	/// Which wall art a wall cell is drawn with: the tiles.json terrain id of the chapter's wall set
	/// (LevelTilePainter.WallArtSet). The grid's wall cells are always the `wall` terrain - collision,
	/// navigation and the flags read that - and this names only how they look. "wall" is the Cursed
	/// Dungeon's jail set (tools/art/jail_walls.py); a chapter that names a set nobody has drawn yet
	/// paints the jail set until it exists, so a chapter can name its walls before its art lands
	/// (.ai/endgame-castle.md 1.9). A new set is a module of its own (court_walls.py, ruin_walls.py),
	/// never an edit to jail_walls.py.
	/// </summary>
	public string WallSet { get; init; } = DefaultWallSet;

	/// <summary>The jail walls: what every wall is drawn with until its chapter has its own.</summary>
	public const string DefaultWallSet = "wall";
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
			// bushes, trees, ruins. See the Forest case in Node2DGame.BuildDecorProps for why
			// these are in the hundreds: the decor field is 8400 units square and the camera sees
			// about one and a fifth per cent of it.
			300,
			430,
			0,
			new[]
			{
				"res://assets/bonelight/world/crystal-violet.png",
				"res://assets/bonelight/world/crystal-yellow.png",
				"res://assets/bonelight/tiles/grass_fill.png"
			},
			0f,
			// Ferns, mushrooms and fallen logs, against a shared default of 42. Raised far less
			// than the trees were on purpose: one prop in three is a fallen log, which is an
			// OBSTACLE with a StaticBody2D, and a wood the player has to pick their way through
			// is a different game from a wood they run in.
			propCount: 260)
		{
			HazeTint = new Color(0.16f, 0.28f, 0.16f, 0.10f),
			GlowTint = new Color(0.26f, 0.40f, 0.24f, 0.05f),
			WeatherTint = new Color(0.90f, 0.96f, 0.86f, 0.025f),
			WeatherOffset = new Vector2(-160f, -120f),
			IntroTint = new Color(0.90f, 0.98f, 0.86f, 1.0f),
			WaterStyle = "forest",
			HasLandmarks = true,
		}
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
		{
			HazeTint = new Color(0.12f, 0.16f, 0.24f, 0.10f),
			GlowTint = new Color(0.28f, 0.22f, 0.34f, 0.05f),
			WeatherTint = new Color(0.06f, 0.08f, 0.12f, 0.028f),
			WeatherOffset = new Vector2(80f, 40f),
			IntroTint = new Color(0.94f, 0.92f, 0.96f, 1.0f),
			HazardSpikes = true,
			HazardFlameChance = 0.28f,
		}
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
		{
			HazeTint = new Color(0.24f, 0.18f, 0.12f, 0.10f),
			GlowTint = new Color(0.36f, 0.26f, 0.14f, 0.05f),
			WeatherTint = new Color(0.24f, 0.18f, 0.12f, 0.024f),
			WeatherOffset = new Vector2(-40f, -60f),
			IntroTint = new Color(0.98f, 0.90f, 0.80f, 1.0f),
			// Spike plates only: old traps set in the stones fit ruins, but an iron floor grate breathing
			// fire under the open sky does not (Eric, 2026-10-28: "there shouldn't be fire vents outside").
			HazardSpikes = true,
			HasLandmarks = true,
		}
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
		{
			HazeTint = new Color(0.16f, 0.24f, 0.34f, 0.12f),
			GlowTint = new Color(0.26f, 0.36f, 0.50f, 0.06f),
			WeatherTint = new Color(0.94f, 0.97f, 1.00f, 0.018f),
			WeatherOffset = new Vector2(-80f, -40f),
			IntroTint = new Color(0.90f, 0.95f, 1.0f, 1.0f),
			WaterStyle = "frozen",
		}
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
		{
			HazeTint = new Color(0.28f, 0.22f, 0.12f, 0.10f),
			GlowTint = new Color(0.44f, 0.34f, 0.16f, 0.05f),
			WeatherTint = new Color(0.72f, 0.56f, 0.28f, 0.024f),
			WeatherOffset = new Vector2(-140f, -80f),
			IntroTint = new Color(0.99f, 0.95f, 0.78f, 1.0f),
		}
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
		{
			HazeTint = new Color(0.28f, 0.12f, 0.08f, 0.12f),
			GlowTint = new Color(0.46f, 0.20f, 0.10f, 0.06f),
			WeatherTint = new Color(0.54f, 0.20f, 0.10f, 0.026f),
			WeatherOffset = new Vector2(70f, 50f),
			IntroTint = new Color(0.99f, 0.88f, 0.78f, 1.0f),
			// No placed traps. It had flame vents and nothing else, and a vent is an iron grate - built
			// floor - on open volcanic ground (2026-10-28, "there shouldn't be fire vents outside"). Its
			// lava already burns whoever stands in it (Node2DGame.UpdateHazardDamage).
		}
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
				"res://assets/bonelight/tiles/grass_fill.png",
				"res://assets/bonelight/world/crystal-yellow.png"
			})
		{
			HazeTint = new Color(0.17f, 0.24f, 0.16f, 0.12f),
			GlowTint = new Color(0.20f, 0.30f, 0.18f, 0.06f),
			WeatherTint = new Color(0.12f, 0.24f, 0.16f, 0.032f),
			WeatherOffset = new Vector2(-120f, -90f),
			IntroTint = new Color(0.86f, 0.96f, 0.84f, 1.0f),
			WaterStyle = "swamp",
			HasLandmarks = true,
		}
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
		{
			HasLandmarks = true,
		}
		,
		// THE FALLOW FIELDS (stage_9, .ai/campaign.md section 5): stubble, ploughed earth and hedge
		// banks (tools/art/terrain_fields.py), stooks and broken fences standing in it
		// (tools/art/props_fields.py, placed by Node2DGame.BuildDecorProps). A dry, warm wash over
		// a cold world, chaff drifting, a millpond's water. A flower pen belongs in a field; traps
		// do not - nothing here was built but the weathercock.
		[StageEnvironmentKind.Fields] = new StageEnvironmentProfile(
			StageEnvironmentKind.Fields,
			"fields",
			"Fields",
			"res://assets/bonelight/tiles/stubble_fill.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.38f,
			0.12f,
			new Color(0.86f, 0.90f, 0.74f, 1.0f),
			new Color(0.46f, 0.42f, 0.24f, 0.14f),
			0.03f,
			// Hedgerows are bushes; a field has a few standing trees and no wood.
			260,
			40,
			0,
			new[] { "res://assets/bonelight/tiles/stubble_fill.png" },
			0f,
			// Ferns, mushrooms and logs from the shared flora theme, kept sparse: the field's own
			// furniture is the stooks and the fences, and a log every few steps is a wood.
			propCount: 60)
		{
			HazeTint = new Color(0.26f, 0.24f, 0.14f, 0.08f),
			GlowTint = new Color(0.40f, 0.34f, 0.18f, 0.05f),
			WeatherTint = new Color(0.80f, 0.74f, 0.52f, 0.020f),
			WeatherOffset = new Vector2(-120f, -60f),
			IntroTint = new Color(0.98f, 0.94f, 0.82f, 1.0f),
			WaterStyle = "forest",
			HasLandmarks = true,
		}
		,
		// THE BROKEN CIRCLE (stage_10, .ai/campaign.md section 6): heather on peat, granite, standing
		// stones, and tarns. Its floor tile is the stage card's banner. No traps - nothing here was
		// built but the stones - and no flower pen on a heath. The tarns are peat-dark, so they take
		// the near-black "pool" water rather than the woodland's teal. The haze is the moor's: a
		// cold violet wash with mist drifting over it.
		[StageEnvironmentKind.Heath] = new StageEnvironmentProfile(
			StageEnvironmentKind.Heath,
			"heath",
			"Heath",
			"res://assets/bonelight/tiles/floor_heather_0.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.35f,
			0.0f,
			new Color(0.92f, 0.90f, 1.0f, 1.0f),
			new Color(1f, 1f, 1f, 0f),
			0.0f,
			// Bushes, trees, ruins: read only by the old fixed field. The streamed heath scatters
			// its stones, gorse and dead trees from Node2DGame.BuildDecorProps.
			180,
			22,
			0,
			new[]
			{
				"res://assets/bonelight/tiles/floor_heather_0.png",
				"res://assets/bonelight/world/heath/standing-stone-1.png"
			},
			48f,
			// Bracken and bones (LevelGenerator's prop themes), sparser than a wood's ferns: a moor is
			// open, and its standing stones are the things standing on it.
			propCount: 70)
		{
			HazeTint = new Color(0.18f, 0.16f, 0.26f, 0.10f),
			GlowTint = new Color(0.30f, 0.26f, 0.40f, 0.05f),
			WeatherTint = new Color(0.86f, 0.88f, 0.94f, 0.022f),
			WeatherOffset = new Vector2(-100f, -70f),
			IntroTint = new Color(0.92f, 0.90f, 0.98f, 1.0f),
			WaterStyle = "pool",
		}
		,
		// THE CASTLE (.ai/endgame-castle.md). Three STAND-IN blocks, one per chapter, each its own piece's
		// to replace (C1 the bridge, C3 the Court, C4 the Throne) - kept a blank line apart so two of them
		// never edit adjacent lines. Until then each borrows the nearest look the game already has, and
		// sets no hazard: the castle's traps are its own (murder-holes, ovens, chandeliers) and are placed
		// by its ChapterRules, not scattered here.

		// THE LAST BRIDGE (stage_11), stand-in: the Ruins' stone under a cold sea wind. C1 gives it the
		// deck (terrain_bridge.py) and the `abyss` water style (water.py) for the drop.
		[StageEnvironmentKind.Bridge] = new StageEnvironmentProfile(
			StageEnvironmentKind.Bridge,
			"bridge",
			"Bridge",
			"res://assets/bonelight/tiles/floor_graystone_0.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.35f,
			0.0f,
			new Color(0.82f, 0.86f, 0.94f, 1.0f),
			new Color(1f, 1f, 1f, 0f),
			0.0f,
			// Nothing grows on a bridge, and its furniture (parapet, gatehouses) is BridgeRules'.
			0,
			0,
			0,
			new[] { "res://assets/bonelight/tiles/floor_graystone_0.png" },
			48f,
			propCount: 16)
		{
			HazeTint = new Color(0.12f, 0.16f, 0.24f, 0.10f),
			GlowTint = new Color(0.20f, 0.24f, 0.34f, 0.05f),
			WeatherTint = new Color(0.70f, 0.78f, 0.90f, 0.026f),
			WeatherOffset = new Vector2(-140f, -100f),
			IntroTint = new Color(0.90f, 0.94f, 1.0f, 1.0f),
			WaterStyle = "pool",
		}
		,

		// THE COURT (stage_12), stand-in: the Cursed Dungeon's flagstones under its cold haze. C3 gives it
		// its floors per wing, its carpet runners and its own walls; it already names that wall set, which
		// paints as the jail walls until court_walls.py draws it.
		[StageEnvironmentKind.Court] = new StageEnvironmentProfile(
			StageEnvironmentKind.Court,
			"court",
			"Court",
			"res://assets/bonelight/tiles/floor_stonetile_0.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.40f,
			0.10f,
			new Color(0.66f, 0.64f, 0.70f, 1.0f),
			new Color(0.30f, 0.32f, 0.38f, 0.0f),
			0.0f,
			0,
			0,
			0,
			new[] { "res://assets/bonelight/tiles/floor_stonetile_0.png" },
			48f,
			propCount: 20)
		{
			HazeTint = new Color(0.14f, 0.14f, 0.22f, 0.10f),
			GlowTint = new Color(0.28f, 0.22f, 0.34f, 0.05f),
			WeatherTint = new Color(0.06f, 0.08f, 0.12f, 0.024f),
			WeatherOffset = new Vector2(60f, 30f),
			IntroTint = new Color(0.94f, 0.92f, 0.96f, 1.0f),
			WallSet = "court_wall",
		}
		,

		// THE THRONE (stage_13), stand-in: the Emberdeep's laid dark tile, unlit. C4 gives it the hall and
		// his light - the `darkness` ramp as emission, the one place in the game it is spent
		// (.ai/endgame-castle.md 6.1) - and its own walls, already named here.
		[StageEnvironmentKind.Throne] = new StageEnvironmentProfile(
			StageEnvironmentKind.Throne,
			"throne",
			"Throne",
			"res://assets/bonelight/tiles/floor_darktile_0.png",
			new Rect2(),
			string.Empty,
			new Rect2(),
			0.30f,
			0.0f,
			new Color(0.62f, 0.58f, 0.70f, 1.0f),
			new Color(1f, 1f, 1f, 0f),
			0.0f,
			0,
			0,
			0,
			new[] { "res://assets/bonelight/tiles/floor_darktile_0.png" },
			48f,
			propCount: 8)
		{
			HazeTint = new Color(0.10f, 0.08f, 0.16f, 0.12f),
			GlowTint = new Color(0.20f, 0.12f, 0.30f, 0.05f),
			IntroTint = new Color(0.90f, 0.86f, 0.98f, 1.0f),
			WallSet = "throne_wall",
		}
	};

	public static StageEnvironmentProfile Get(StageEnvironmentKind kind)
	{
		return Profiles[kind];
	}

	// The chapter roster owns the index -> environment mapping now. This used to be its own switch
	// over ten indices that had no relation to the stage names in Node2DGame or the two entries in
	// StageSelection, so the three disagreed about what stage 3 even was.
	//
	// Takes the raw stage index and clamps through StageCatalog.GetByIndex, which knows how many
	// stages there are. Its callers used to clamp first, to 0..9, and the roster has outgrown that:
	// stage_10 read as stage_9's ground.
	public static StageEnvironmentProfile GetForStageIndex(int stageIndex)
	{
		StageDefinition stage = StageCatalog.GetByIndex(stageIndex);
		return Get(stage?.EnvironmentKind ?? StageEnvironmentKind.Forest);
	}
}
