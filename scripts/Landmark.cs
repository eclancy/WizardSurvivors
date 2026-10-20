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
/// the horde round to a gate instead of wedging it on the rails; and there are TWO gates, north
/// and south, so a pen is a room with two doors and never a dead end. See
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
/// water follows, .ai/terrain.md section 3). The gates are whole cells of the north and south
/// rails, <see cref="GateW"/> wide - 96 px, which clears the widest body in the game (the Deep
/// Warden, 88 px) as well as the wizard's 56 px boots.
/// </remarks>
public static class FlowerPenLayout
{
	public const int CellsW = 12;
	public const int CellsH = 9;
	/// <summary>First gate cell along the north and the south rail.</summary>
	public const int GateX = 5;
	public const int GateW = 2;
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
		return !(gateRow && x >= GateX && x < GateX + GateW);
	}

	/// <summary>
	/// The rails' collision, as rectangles relative to the plot's top-left corner in world pixels:
	/// the north and south rails either side of their gate, and the two sides.
	/// </summary>
	public static List<Rect2> RailRects(int tile)
	{
		float line = tile * 0.5f;
		float half = RailDepth * 0.5f;
		float west = line, east = CellsW * tile - line;
		float north = line, south = CellsH * tile - line;
		float gate0 = GateX * tile, gate1 = (GateX + GateW) * tile;
		var rects = new List<Rect2>(6);
		foreach (float y in new[] { north, south })
		{
			rects.Add(new Rect2(west - half, y - half, gate0 - (west - half), RailDepth));
			rects.Add(new Rect2(gate1, y - half, east + half - gate1, RailDepth));
		}
		rects.Add(new Rect2(west - half, north - half, RailDepth, south - north + RailDepth));
		rects.Add(new Rect2(east - half, north - half, RailDepth, south - north + RailDepth));
		return rects;
	}
}
