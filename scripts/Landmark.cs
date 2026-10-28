using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// A rare, hand-made-looking place in the endless ground - for now the flower pen, a walled garden
/// round one glowing moonflower. Built as a placement other systems can find and hang behaviour on.
/// See .ai/terrain.md section 8.
/// </summary>
/// <remarks>
/// THE HOOK, FOR WHOEVER BUILDS SECRETS. Three ways to find a landmark, all cheap:
///   - <see cref="Spawned"/> fires when one enters the tree (and <see cref="Despawned"/> when its
///     chunk is forgotten), so a system can keep its own list without polling the tree.
///   - Every landmark is in the "landmarks" group.
///   - <see cref="Key"/> is stable across visits AND across runs on the same terrain seed: it is
///     built from the stage, the chunk and the kind, and the chunk's placement is a pure function of
///     the seed (Node2DGame.LandmarkCells). Save "found" state against it.
/// <see cref="Centre"/> is the flower; <see cref="Bounds"/> is the fenced area in world pixels,
/// fence line to fence line.
/// What a secret DOES - a boon for standing at the flower, a wizard who appears at the third one
/// found - belongs in that system, not here: this node owns placement and appearance only.
///
/// THE FENCE IS SOLID (2026-10). It used to stop nothing, on the grounds that a solid pen is a
/// corner a swarm can pin the player in and that open-stage enemies had no pathfinding to find
/// the way in. Reported: "I should not be able to walk directly through the fence." Both reasons
/// are answered now. The rails are a body on the ground's physics layer, so they stop the
/// wizard's boots where the fence is drawn (and enemies, which mask that layer too); the fence's
/// cells are in the navigation grid (TerrainStreamer.FlagFence), so the shared flow field sends
/// the horde round to a gate instead of wedging it on the rails; and there are FOUR wide gates, one
/// in every side (two narrow ones until 2026-10-28, below), so a pen is never a dead end. See
/// <see cref="FlowerPenLayout"/> for the geometry and .ai/terrain.md section 8 for why.
/// </remarks>
public partial class Landmark : Node2D
{
	/// <summary>Fired when a landmark enters the tree. The secrets hook - see the class remarks.</summary>
	public static event Action<Landmark> Spawned;

	/// <summary>Fired when a landmark leaves the tree, which is when its chunk is forgotten.</summary>
	public static event Action<Landmark> Despawned;

	public const string GroupName = "landmarks";

	/// <summary>What kind of landmark this is. "flower_pen" is the only one so far.</summary>
	public string Kind { get; set; } = "";

	/// <summary>Stable id: stage, chunk and kind. The same place has the same key every visit.</summary>
	public string Key { get; set; } = "";

	/// <summary>The fenced area, in world pixels: from fence line to fence line.</summary>
	public Rect2 Bounds { get; set; }

	/// <summary>The special thing at the middle - the moonflower - in world pixels.</summary>
	public Vector2 Centre { get; set; }

	/// <summary>Where the gates are, in world pixels: the middle of each gap in the fence.</summary>
	public Vector2[] Gates { get; set; } = Array.Empty<Vector2>();

	public override void _EnterTree()
	{
		AddToGroup(GroupName);
		Spawned?.Invoke(this);
	}

	public override void _ExitTree()
	{
		Despawned?.Invoke(this);
	}
}

/// <summary>
/// The flower pen's shape in cells: the one place the fence's navigation cells and its collision
/// are both derived from, so the flow field and the bodies cannot disagree about where it stands.
/// tools/art/ground_detail.py draws to the same numbers and writes them into flower-pen.json;
/// RegressionChecks.ValidateGroundArt holds the two against each other.
/// </summary>
/// <remarks>
/// The plot is <see cref="CellsW"/> x <see cref="CellsH"/> cells. Its outer ring of cells is the
/// fence: the fence's ground line runs down the MIDDLE of those cells, and its collision is a
/// strip <see cref="RailDepth"/> deep centred on that line - never outside the ring, so every
/// cell the grid marks solid really is fence, and the cells inside it really are open (the rule
/// water follows, .ai/terrain.md section 3). The gates are whole cells: <see cref="GateW"/> of the
/// north and south rails and <see cref="SideGateH"/> of the east and west ones.
///
/// BIGGER, WITH FOUR WIDE GATES (2026-10-28). Reported: "standing in the fence near the big flower
/// makes an easy way for players to get enemies stuck. make the area with the flower much larger and
/// the entrances larger so enemies dont get stuck." The pen was 12 x 9 cells with two 96 px gates,
/// and a swarm chasing a wizard inside queued in single file at both: measured with
/// `_UiShot --pen-camp` (the wizard held still just inside the east rail, a crowd of seventy topped
/// up round him), 22-28 enemies a second stood stalled outside the fence. It is 20 x 14 now
/// (960 x 672 px), and every side has a 192 px gate - two of the widest body in the game (the Deep
/// Warden, 88 px) - so no rail is more than eight cells from a way in.
/// </remarks>
public static class FlowerPenLayout
{
	public const int CellsW = 20;
	public const int CellsH = 14;
	/// <summary>First gate cell along the north and the south rail.</summary>
	public const int GateX = 8;
	public const int GateW = 4;
	/// <summary>First gate cell (row) down the east and the west rail.</summary>
	public const int SideGateY = 5;
	public const int SideGateH = 4;
	/// <summary>How deep the rails' collision is, in world pixels, centred on the fence line.</summary>
	public const float RailDepth = 14f;

	/// <summary>True for a cell (in the plot's own coordinates) the fence stands on.</summary>
	public static bool IsFenceCell(int x, int y)
	{
		if (x < 0 || y < 0 || x >= CellsW || y >= CellsH)
			return false;
		bool ring = x == 0 || y == 0 || x == CellsW - 1 || y == CellsH - 1;
		if (!ring)
			return false;
		bool gateRow = y == 0 || y == CellsH - 1;
		if (gateRow && x >= GateX && x < GateX + GateW)
			return false;
		bool gateColumn = x == 0 || x == CellsW - 1;
		return !(gateColumn && !gateRow && y >= SideGateY && y < SideGateY + SideGateH);
	}

	/// <summary>
	/// The side gate's drawn and solid gap, in world pixels from the plot's top: a side rail is drawn
	/// a cell a segment, each segment centred on a cell boundary, so the gap runs from half a cell
	/// above the first gate cell to half a cell below the last. That is wider than the gate's cells,
	/// never narrower, so the grid never calls a cell open that the rail stops a body in.
	/// </summary>
	public static (float Top, float Bottom) SideGap(int tile) =>
		((SideGateY - 0.5f) * tile, (SideGateY + SideGateH + 0.5f) * tile);

	/// <summary>
	/// True for the side segment ending k cells below the north fence line (1-based, as
	/// Node2DGame.CreateFlowerPen counts them) when it falls in a side gate's gap.
	/// </summary>
	public static bool IsSideGateSegment(int k) => k >= SideGateY && k <= SideGateY + SideGateH;

	/// <summary>
	/// The rails' collision, as rectangles relative to the plot's top-left corner in world pixels:
	/// the north and south rails either side of their gate, and the two sides either side of theirs.
	/// </summary>
	public static List<Rect2> RailRects(int tile)
	{
		float line = tile * 0.5f;
		float half = RailDepth * 0.5f;
		float west = line, east = CellsW * tile - line;
		float north = line, south = CellsH * tile - line;
		float gate0 = GateX * tile, gate1 = (GateX + GateW) * tile;
		(float sideTop, float sideBottom) = SideGap(tile);
		var rects = new List<Rect2>(8);
		foreach (float y in new[] { north, south })
		{
			rects.Add(new Rect2(west - half, y - half, gate0 - (west - half), RailDepth));
			rects.Add(new Rect2(gate1, y - half, east + half - gate1, RailDepth));
		}
		foreach (float x in new[] { west, east })
		{
			rects.Add(new Rect2(x - half, north - half, RailDepth, sideTop - (north - half)));
			rects.Add(new Rect2(x - half, sideBottom, RailDepth, south + half - sideBottom));
		}
		return rects;
	}
}
