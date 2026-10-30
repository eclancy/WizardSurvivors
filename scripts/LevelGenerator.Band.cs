using System;
using Godot;

namespace WizardSurvivors.scripts;

// THE BAND TOPOLOGY: the Last Bridge's ground (.ai/endgame-castle.md 3.2), piece C1.
//
// Eric: "the level has a bridge edge on the top left and bottom right, so the player needs to travel up
// and to the right to get to the castle." A long stone deck runs from the bottom left of the screen to
// the top right - south-west to north-east in the world - with the drop either side of it. At 45 degrees,
// and only at 45 degrees, a PC (1506 x 847 world px at zoom 0.85) and a phone (847 x 1506) see the same
// bridge: about 600 px of deck ahead, and both edges in the same two corners.
//
// THE DROP IS WATER, as far as the engine knows. Water is the one blocking ground drawn from its shape
// and collided at its shoreline rather than its cells (.ai/terrain.md section 3), so a diagonal edge comes
// out as a straight shore through the outer corners of the staircase instead of a staircase of tiles. Every
// place that has to agree about blocking ground already knows water (the safe zone, IsImpassablePosition,
// prop avoidance, the navigation grid); the drop is drawn in its own water style, `abyss` (water.py), the sea
// far below. Nothing new blocks.
//
// THE SHAPE, measured on the world tile's two diagonals: e = tx - ty runs ALONG the bridge (north-east) and
// d = tx + ty ACROSS it (south-east positive). A cell is deck when its e falls inside one of the pieces in
// BridgeBand and -k <= d <= k - 2 for that piece's k: then its centre's distance across the axis is at most
// (k - 1) diagonal steps, and the shore WaterShape draws runs straight along x + y = -k and x + y = k (tile
// units), k diagonal steps of 34 px either side of the axis. The axis passes through the stage origin, the
// corner the player starts on. Everything is integer arithmetic on the tile, so a cell is a pure function of
// where it is (the seam rule, .ai/terrain.md section 7) and comes out the same from any chunk.
//
// What is NOT here: the collapsing front (BridgeCollapseFront - a hazard drawn over the deck, never a change
// to a cell), the parapets, gatehouse towers and the castle wall (props, placed by BridgeRules along the
// shores this file defines), and the deck's look (terrain_bridge.py's `deck`, the palette's GroundTerrain).
public sealed partial class LevelGenerator
{
	static partial void BuildBandTopology(ref TopologyBuild build)
	{
		build.Generate = GenerateBand;
		// The drop is water, not wall: no maze rules, and the open-ground cleanup still runs (a half-plane
		// staircase is already stable under it, and it rounds off the odd one-cell tip at a gatehouse corner).
		build.Walled = false;
		// The bridgehead is 31 tiles across round the start, so the usual clearing disc is always deck.
		build.OwnsSpawnClearing = false;
	}

	private const int SaltBridgeGap = 0x6A9B;

	private static void GenerateBand(string[,] grid, TopologyChunk chunk)
	{
		// The deck's floor (the palette's GroundTerrain paints it) and its few rubble patches first.
		FillOpenGround(grid, chunk);

		// The two broken spans: deterministic holes, not palette blobs, so every run has its two gaps
		// where 3.8 puts them and nowhere else. The existing `pit` hazard - it hurts to stand in, and a
		// passage is always left beside it.
		foreach (BridgeBand.Gap gap in BridgeBand.Gaps)
		{
			Vector2 c = BridgeBand.TileAt(gap.S, gap.T);
			ulong jitter = Hash(chunk.Seed, (int)gap.S, (int)gap.T, SaltBridgeGap);
			StampBlob(grid, chunk.X0, chunk.Y0, c.X, c.Y, gap.RadiusTiles, "pit", true, chunk.SafeRadius, jitter);
		}

		int size = chunk.Size;
		for (int x = 0; x < size; x++)
		{
			int tx = x + chunk.X0;
			for (int y = 0; y < size; y++)
			{
				int ty = y + chunk.Y0;
				if (!BridgeBand.IsDeckCell(tx, ty))
					grid[x, y] = "water";
			}
		}
	}
}

/// <summary>
/// The Last Bridge's shape, in one place: the generator asks it which cells are deck, and BridgeRules,
/// BridgeCollapseFront and BridgeBackdrop ask it where the shores, the gatehouses, the yards and the gate
/// are. Distances are world pixels from the stage origin: <c>s</c> along the bridge (north-east, up and
/// right on screen), <c>t</c> across it (south-east positive).
/// </summary>
public static class BridgeBand
{
	/// <summary>World pixels per tile (Node2DGame.ProceduralTilePixels).</summary>
	public const float TilePx = 48f;

	/// <summary>One diagonal step - a change of 1 in e or d - in world pixels along s or t: 48 / sqrt 2.</summary>
	public const float StepPx = TilePx * 0.70710678f;

	/// <summary>What a piece of the bridge is for (.ai/endgame-castle.md 3.2).</summary>
	public enum PieceKind { Bridgehead, Span, Gatehouse, Yard, GateYard }

	/// <summary>
	/// One piece of the bridge: [S0, S1) along it, and its half-width in diagonal steps (the shore is
	/// K * <see cref="StepPx"/> either side of the axis).
	/// </summary>
	public readonly record struct Piece(PieceKind Kind, int Index, float S0, float S1, int K)
	{
		public float HalfWidthPx => K * StepPx;
		public float Mid => (S0 + S1) * 0.5f;
	}

	// Half-widths in diagonal steps. A span is 18 tiles of deck (3.2): room to circle a pack, and both
	// edges in the screen's corners on both canvases. A gatehouse narrows it to 8 for 10 tiles of length;
	// a yard is 28 across, a bastion to breathe in. The bridgehead is the cliff top, wider still.
	private const int SpanK = 13;       // 18.4 tiles, 882 px
	private const int GatehouseK = 6;   // 8.5 tiles, 407 px
	private const int YardK = 20;       // 28.3 tiles, 1358 px
	private const int HeadK = 22;       // 31.1 tiles

	// Lengths along the bridge, px. Three chunks a span (4,600 px), a gatehouse 10 tiles, a yard a chunk,
	// the gate yard two. About 30,300 px from the back of the bridgehead to the castle wall, 28,700 of it
	// ahead of the start: a little over two minutes of walking with nothing in the way, which is never.
	public const float HeadBack = -1600f;
	private const float HeadFront = 700f;
	private const float SpanLength = 4600f;
	private const float GatehouseLength = 480f;
	private const float YardLength = 1536f;
	private const float GateYardLength = 3072f;

	/// <summary>Every piece, back to front.</summary>
	public static readonly Piece[] Pieces = BuildPieces();

	/// <summary>The back of the bridgehead: behind it is the drop (the end of the cliffs).</summary>
	public static float Start => Pieces[0].S0;

	/// <summary>The castle wall at the far end of the gate yard; past it, the drop and the castle on its rock.</summary>
	public static float End => Pieces[Pieces.Length - 1].S1;

	/// <summary>The gate yard's mouth: where the collapsing front stops at 15:00.</summary>
	public static float GateYardMouth => Pieces[Pieces.Length - 1].S0;

	private static Piece[] BuildPieces()
	{
		var list = new System.Collections.Generic.List<Piece>();
		float s = HeadBack;
		list.Add(new Piece(PieceKind.Bridgehead, 0, s, HeadFront, HeadK));
		s = HeadFront;
		for (int i = 0; i < 4; i++)
		{
			list.Add(new Piece(PieceKind.Span, i, s, s + SpanLength, SpanK));
			s += SpanLength;
			list.Add(new Piece(PieceKind.Gatehouse, i, s, s + GatehouseLength, GatehouseK));
			s += GatehouseLength;
			if (i < 3)
			{
				list.Add(new Piece(PieceKind.Yard, i, s, s + YardLength, YardK));
				s += YardLength;
			}
		}
		list.Add(new Piece(PieceKind.GateYard, 0, s, s + GateYardLength, YardK));
		return list.ToArray();
	}

	/// <summary>A hole in a span (3.8: "gaps in the deck on two spans"), as a blob of the `pit` hazard.</summary>
	public readonly record struct Gap(float S, float T, float RadiusTiles);

	// Span 2 is broken on its north-west side and span 4 on its south-east, each leaving a passage of four
	// to five tiles beside the hole - the swarm funnels through it as it does through a gatehouse.
	public static readonly Gap[] Gaps = BuildGaps();

	private static Gap[] BuildGaps()
	{
		Piece span2 = Find(PieceKind.Span, 1);
		Piece span4 = Find(PieceKind.Span, 3);
		return new[]
		{
			new Gap(span2.Mid, -250f, 4.0f),
			new Gap(span2.Mid + 170f, -90f, 2.8f),
			new Gap(span2.Mid - 190f, -330f, 3.0f),
			new Gap(span4.Mid, 250f, 4.0f),
			new Gap(span4.Mid - 170f, 90f, 2.8f),
			new Gap(span4.Mid + 190f, 330f, 3.0f),
		};
	}

	/// <summary>The piece of a kind by its index among that kind (the second span is Find(Span, 1)).</summary>
	public static Piece Find(PieceKind kind, int index)
	{
		foreach (Piece p in Pieces)
			if (p.Kind == kind && p.Index == index)
				return p;
		throw new ArgumentOutOfRangeException(nameof(index), $"The bridge has no {kind} {index}.");
	}

	/// <summary>The piece a point <paramref name="s"/> along the bridge is in, or -1 off either end.</summary>
	public static int PieceIndexAt(float s)
	{
		if (s < Pieces[0].S0 || s >= Pieces[Pieces.Length - 1].S1)
			return -1;
		// Fourteen pieces: a scan is cheaper than anything clever, and allocation-free.
		for (int i = 0; i < Pieces.Length; i++)
			if (s < Pieces[i].S1)
				return i;
		return -1;
	}

	/// <summary>Half the deck's width at <paramref name="s"/>, to the shore, in px; 0 off either end.</summary>
	public static float HalfWidthAt(float s)
	{
		int i = PieceIndexAt(s);
		return i < 0 ? 0f : Pieces[i].HalfWidthPx;
	}

	/// <summary>
	/// THE AUTHORITY: whether world tile (tx, ty) is deck. Integer diagonals, and the piece by the cell
	/// centre's distance along the bridge, so it is a pure function of the tile.
	/// </summary>
	public static bool IsDeckCell(int tx, int ty)
	{
		int i = PieceIndexAt((tx - ty) * StepPx);
		if (i < 0)
			return false;
		int k = Pieces[i].K;
		int d = tx + ty;
		return d >= -k && d <= k - 2;
	}

	/// <summary>Along the bridge, px, for a point relative to the stage origin.</summary>
	public static float AlongOf(Vector2 local) => (local.X - local.Y) * 0.70710678f;

	/// <summary>Across the bridge, px (south-east positive), for a point relative to the stage origin.</summary>
	public static float AcrossOf(Vector2 local) => (local.X + local.Y) * 0.70710678f;

	/// <summary>The point <paramref name="s"/> along and <paramref name="t"/> across, relative to the stage origin.</summary>
	public static Vector2 LocalAt(float s, float t) => new((s + t) * 0.70710678f, (t - s) * 0.70710678f);

	/// <summary>The same point in world tiles (fractional), for the generator's stamps.</summary>
	public static Vector2 TileAt(float s, float t) => LocalAt(s, t) / TilePx;

	/// <summary>The unit vector along the bridge (north-east) in world space.</summary>
	public static readonly Vector2 Along = new(0.70710678f, -0.70710678f);

	/// <summary>The unit vector across the bridge (south-east) in world space.</summary>
	public static readonly Vector2 Across = new(0.70710678f, 0.70710678f);

	/// <summary>
	/// True when a point (relative to the stage origin) is on the deck with <paramref name="margin"/> px
	/// to spare from either shore and from both ends. Answers from the shape, not from the cells, so it is
	/// the drawn shore's line to within the half-cell the staircase leaves.
	/// </summary>
	public static bool IsOnDeck(Vector2 local, float margin = 0f)
	{
		float s = AlongOf(local);
		int i = PieceIndexAt(s);
		if (i < 0)
			return false;
		float half = Pieces[i].HalfWidthPx - margin;
		float t = AcrossOf(local);
		return t > -half && t < half && s - Pieces[0].S0 > margin && Pieces[Pieces.Length - 1].S1 - s > margin;
	}

	/// <summary>The gate in the castle wall: the way home, where the Constable comes from.</summary>
	public static float GateS => End - 150f;
}
