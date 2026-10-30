using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// THE CAVERNS TOPOLOGY: the Sunken Cave's ground (.ai/endgame-castle.md 7.3, piece V2). The chapter
// whose fiction is "dripping tunnels of black rock" stood on the Ruins' open field until this file:
// LevelGenerator.PaletteFor fell back to the Ruins' palette and it had no ground of its own.
//
// THE DUNGEON'S GRAPH, DRAWN AS ROCK. The Cursed Dungeon's maze is a graph of cells - every 4 x 4 block
// connected inside and to its four neighbours, so the world is connected (LevelGenerator, "Maze
// topology") - and what is drawn in its walls is a separate choice (7.2). Here the graph is not
// rasterised as two-tile walls between six-tile corridors. Its cells stand CavernPitch tiles apart,
// each centre moved off the lattice (CavernJitter); each passage the graph opens is two lines through a
// bend pushed off its middle (CavernBend); each hall the hall pass knocks out is the quad between four
// centres, and a share of the cells are chambers, a disc round the centre. A tile is floor when it is
// nearer that skeleton than a half-width that swells and narrows across the cave. So passages wander
// and run five to nine tiles wide, chambers are eleven to twenty-one tiles across, and the rock is
// whatever is left: masses with lumpy, slanting edges, seven to eleven tiles thick between passages,
// and rounded pillars where four passages meet round no hall.
//
// WHY SIXTEEN AND NOT THE DUNGEON'S EIGHT. Tried in that order: at eight tiles a centre a passage wider
// than the Dungeon's six leaves no rock beside it, and the cave came out an open floor with boulders on
// it; at twelve the passages' bands still covered three quarters of the ground, rock islands in a
// floor. At sixteen it is tunnels in rock - about two fifths of the ground is rock. What reads the
// lattice's eight-tile pitch on walled ground copes: ExplorePlacer snaps a site to an eight-tile point
// and, when that is rock, searches rings round it for a footprint of floor; MazeHallwayMiddle and the
// trap rows run only where traps are scattered, which the Cave is not.
//
// CONNECTED BY CONSTRUCTION, WHICH IS THE WHOLE POINT. The half-width never drops below 2.5 tiles, so
// the middle five tiles of every passage the graph opens are never rock (7.3's "one rule", with a tile
// to spare), and those cores are connected because the graph is. Every OTHER floor tile is connected to
// a core too, and the reason is a number. Step from a floor tile outside the core one tile toward the
// nearest point of the skeleton, along the axis that point is mostly along: the distance falls by at
// least 2.5 - sqrt(2.5^2 - 2.5 * sqrt(2) + 1) = 0.573 (the worst case, a diagonal approach to the end of
// a passage at the core's edge), while the half-width rises by at most 1.5 * (CavernSwell / CavernPitch
// + CavernWobble / CavernWobbleTiles) = 1.5 * (1.2 / 16 + 0.6 / 6) = 0.26 (smoothstep value noise is never
// steeper than 1.5 times its node range per node spacing along an axis). So the step lands on floor,
// nearer the skeleton, and the walk ends in a core: there are no sealed pockets - which is what
// ExplorePlacer and Node2DGame.IsExploreGroundClear assume of walled ground ("connected by
// construction"), and what keeps anything from ever being placed where it cannot walk out. The chambers
// and halls are part of the skeleton, so the distance to them is as steady as to a passage. Every pass
// after the field only turns rock into floor beside floor, or stands water in a chamber with a ring of
// floor all round it, and neither can cut anything off. RegressionChecks.ValidateNoSealedPockets floods
// nine chunks of it to hold the claim. Raise CavernSwell or CavernWobble, or lower their spacings, and
// the arithmetic above has to be done again.
//
// A pure function of the world tile, like every topology: the extended grid this works in reaches
// WalledFloorMargin tiles past the chunk's padded grid, so every pass that reads its neighbours is exact
// on every cell it hands back, and the seam rule holds (.ai/terrain.md section 7).
public sealed partial class LevelGenerator
{
	static partial void BuildCavernsTopology(ref TopologyBuild build)
	{
		build.Generate = GenerateCaverns;
		build.Walled = true;
	}

	/// <summary>
	/// The Cave's graph: sparser than the Dungeon's (fewer loops, fewer doors between blocks), because a
	/// cave is tunnels through rock rather than a grid of corridors, and far more halls, because a hall is
	/// a chamber here.
	/// </summary>
	private static MazeRates CavernRates => new(braid: 0.85f, loop: 0.08f, hall: 0.55f, door: 0.6f);

	/// <summary>Tiles between neighbouring cells' centres on the Cave's lattice; cell (0, 0) is centred on the start.</summary>
	public const int CavernPitch = 16;

	// How far a cell's centre moves off the lattice, in tiles, and how far a passage's middle is pushed
	// sideways off the line between its two centres: so no passage runs dead straight along a row, and no
	// two run parallel. The jitter stays under a quarter of the pitch, which keeps a hall's quad convex.
	private const float CavernJitter = 4f;
	private const float CavernBend = 3.5f;
	// The half-width of the floor round the skeleton: CavernMinHalfWidth everywhere, plus a slow swell
	// hashed per cell (so a passage's width is set by the two cells it joins - "hashed per passage") and a
	// faster wobble that makes a rock edge lumpy. The connection proof above rests on these four numbers.
	private const float CavernMinHalfWidth = 2.5f;
	private const float CavernSwell = 1.2f;
	private const float CavernWobble = 0.6f;
	private const int CavernWobbleTiles = 6;

	// Chambers (7.3's "rooms as chambers"). A hall is one, where the graph happens to close a ring of four
	// cells round a post - which a braided tree seldom does. So a share of cells are chambers as well: the
	// cell's centre swells into a disc of the skeleton, which the floor's half-width then rings like any
	// passage. A disc round a centre is part of the skeleton, so the distance to it is as steady as the
	// distance to a passage and the connection proof does not change. CavernChamberMaxRadius plus the widest
	// half-width (11.5 tiles) is far less than a cell and a half, so only the nine cells round a tile can
	// reach it.
	private const float CavernChamberChance = 0.14f;
	private const float CavernChamberMinRadius = 3f;
	private const float CavernChamberMaxRadius = 6.5f;

	// Water in a chamber (the "pools" of 7.3): in a share of the chambers and halls, a disc near the
	// chamber's middle, and only where every tile within CavernPoolRing of its rim is floor - so a pool
	// always has a walkway three tiles wide all round it and can never close a way through or wall anything
	// in. Where its rolled size does not fit, a smaller one is tried, down to CavernPoolMinRadius.
	private const float CavernPoolChance = 0.5f;
	private const float CavernPoolMinRadius = 2.2f;
	private const float CavernPoolMaxRadius = 4f;
	private const float CavernPoolRing = 3f;

	private const int SaltCavernJitter = 0x6C17;
	private const int SaltCavernSwell = 0x6C29;
	private const int SaltCavernWobble = 0x6C3B;
	private const int SaltCavernPool = 0x6C4D;
	private const int SaltCavernBend = 0x6C5F;
	private const int SaltCavernChamber = 0x6C71;

	private static void GenerateCaverns(string[,] grid, TopologyChunk chunk)
	{
		int ext = WalledFloorMargin;
		int size = chunk.Size + 2 * ext;
		int ex0 = chunk.X0 - ext;
		int ey0 = chunk.Y0 - ext;
		var cave = new CavernField(chunk.Seed, ex0, ey0, size);

		// 1. The field.
		sbyte[,] cells = WalledScratch(size);
		for (int x = 0; x < size; x++)
			for (int y = 0; y < size; y++)
				cells[x, y] = cave.IsFloor(ex0 + x, ey0 + y) ? CellFloor : CellWall;

		// 2. Thin rock goes. A one-tile sliver of rock between two floors, a nub with one rock neighbour
		// or none, and two masses touching only at a corner have no wall art that reads as rock (each
		// resolves to the autotiler's Patch) and are no obstacle worth the name. Rock to floor beside
		// floor only, so nothing is cut off; two passes, because taking a sliver can leave a nub.
		sbyte[,] read = WalledReadScratch(size);
		for (int pass = 0; pass < 2; pass++)
		{
			Array.Copy(cells, read, cells.Length);
			for (int x = 1; x < size - 1; x++)
			{
				for (int y = 1; y < size - 1; y++)
				{
					if (read[x, y] != CellWall)
						continue;
					bool n = read[x, y - 1] == CellWall, s = read[x, y + 1] == CellWall;
					bool w = read[x - 1, y] == CellWall, e = read[x + 1, y] == CellWall;
					int around = (n ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0) + (e ? 1 : 0);
					bool pinch = (read[x + 1, y + 1] == CellWall && !e && !s) || (read[x - 1, y + 1] == CellWall && !w && !s)
						|| (read[x + 1, y - 1] == CellWall && !e && !n) || (read[x - 1, y - 1] == CellWall && !w && !n);
					if (around <= 1 || (!n && !s) || (!w && !e) || pinch)
						cells[x, y] = CellFloor;
				}
			}
		}

		// 3. Pools in some chambers.
		cave.StandPools(cells, chunk.SafeRadius);

		// 4. The palette's floor between the rock, and the chunk's middle handed back.
		LayFloorBetweenWalls(grid, chunk, cells, ext);
	}

	/// <summary>
	/// The cavern field over one extended grid: each cell's moved centre, the passages and halls the
	/// graph opens round it, and the two noise lattices of the half-width - all read once per chunk, so a
	/// tile costs two dozen segment distances and two lookups rather than a dozen hashes.
	/// </summary>
	private sealed class CavernField
	{
		private readonly ulong seed;
		// The extended grid this field is read into: [0, 0] is world tile (gridX0, gridY0).
		private readonly int gridX0, gridY0, gridSize;
		// Cells [ci0, ci0 + cn) x [cj0, cj0 + cn). A tile asks the cells two either side of its own, and a
		// pool's ring reaches up to two cells past the grid, so the range is the grid's cells and five more.
		private readonly int ci0, cj0, cn;
		private readonly float[] nodeX, nodeY;
		// Where each cell's east and south passages bend: the midpoint, pushed sideways (CavernBend).
		private readonly float[] bendEX, bendEY, bendSX, bendSY;
		private readonly bool[] openE, openS, hall;
		// A chamber's radius round the cell's centre, or 0 for none.
		private readonly float[] chamber;
		private readonly float[] swell;
		// The wobble lattice, every CavernWobbleTiles: [wi0, wi0 + wn) on both axes.
		private readonly int wi0, wj0, wn;
		private readonly float[] wobble;

		private const int CellReach = 5;
		private const int TileReach = CellReach * CavernPitch;

		public CavernField(ulong seed, int x0, int y0, int size)
		{
			this.seed = seed;
			gridX0 = x0;
			gridY0 = y0;
			gridSize = size;
			var graph = new MazeGraph(seed, CavernRates);
			// A tile is in cell CellOf(t): the nearest lattice centre.
			ci0 = CellOf(x0) - CellReach;
			cj0 = CellOf(y0) - CellReach;
			cn = CellOf(x0 + size - 1) + CellReach - ci0 + 1;
			int count = cn * cn;
			nodeX = new float[count];
			nodeY = new float[count];
			bendEX = new float[count];
			bendEY = new float[count];
			bendSX = new float[count];
			bendSY = new float[count];
			openE = new bool[count];
			openS = new bool[count];
			hall = new bool[count];
			chamber = new float[count];
			swell = new float[count];
			for (int a = 0; a < cn; a++)
			{
				for (int b = 0; b < cn; b++)
				{
					int i = ci0 + a, j = cj0 + b, k = a + b * cn;
					var rng = new Rng(Hash(seed, i, j, SaltCavernJitter));
					nodeX[k] = i * CavernPitch + (rng.NextFloat() * 2f - 1f) * CavernJitter;
					nodeY[k] = j * CavernPitch + (rng.NextFloat() * 2f - 1f) * CavernJitter;
					openE[k] = graph.OpenE(i, j);
					openS[k] = graph.OpenS(i, j);
					hall[k] = graph.Hall(i, j);
					swell[k] = Unit(Hash(seed, i, j, SaltCavernSwell));
					var room = new Rng(Hash(seed, i, j, SaltCavernChamber));
					if (room.NextFloat() < CavernChamberChance)
						chamber[k] = CavernChamberMinRadius + room.NextFloat() * (CavernChamberMaxRadius - CavernChamberMinRadius);
				}
			}
			// A passage is two straight lines through its bend, so it meanders instead of running along a
			// row; the bend is a hash of the passage, read the same from both cells it joins.
			for (int a = 0; a + 1 < cn; a++)
			{
				for (int b = 0; b + 1 < cn; b++)
				{
					int i = ci0 + a, j = cj0 + b, k = a + b * cn;
					var rng = new Rng(Hash(seed, i, j, SaltCavernBend));
					float east = (rng.NextFloat() * 2f - 1f) * CavernBend;
					float south = (rng.NextFloat() * 2f - 1f) * CavernBend;
					bendEX[k] = (nodeX[k] + nodeX[k + 1]) * 0.5f;
					bendEY[k] = (nodeY[k] + nodeY[k + 1]) * 0.5f + east;
					bendSX[k] = (nodeX[k] + nodeX[k + cn]) * 0.5f + south;
					bendSY[k] = (nodeY[k] + nodeY[k + cn]) * 0.5f;
				}
			}
			wi0 = FloorDiv(x0 - TileReach, CavernWobbleTiles) - 1;
			wj0 = FloorDiv(y0 - TileReach, CavernWobbleTiles) - 1;
			wn = (size + 2 * TileReach) / CavernWobbleTiles + 4;
			wobble = new float[wn * wn];
			for (int a = 0; a < wn; a++)
				for (int b = 0; b < wn; b++)
					wobble[a + b * wn] = Unit(Hash(seed, wi0 + a, wj0 + b, SaltCavernWobble));
		}

		private static float Unit(ulong h) => (h % 100003UL) / 100003f;

		private static int CellOf(int tile) => FloorDiv(tile + CavernPitch / 2, CavernPitch);

		private int K(int i, int j) => (i - ci0) + (j - cj0) * cn;

		/// <summary>True if world tile (tx, ty) is floor in the raw field, before any pass.</summary>
		public bool IsFloor(int tx, int ty)
		{
			float px = tx + 0.5f, py = ty + 0.5f;
			return SkeletonDistance(tx, ty, px, py) < HalfWidth(px, py);
		}

		private float HalfWidth(float px, float py) =>
			CavernMinHalfWidth
			+ CavernSwell * Smooth(swell, px / CavernPitch, py / CavernPitch, ci0, cj0, cn)
			+ CavernWobble * Smooth(wobble, px / CavernWobbleTiles, py / CavernWobbleTiles, wi0, wj0, wn);

		// Value noise with smoothstep between lattice nodes: at most 1.5 times the node range per node
		// spacing in its slope along either axis, which is what the connection proof counts on.
		private static float Smooth(float[] nodes, float fx, float fy, int i0, int j0, int n)
		{
			int ia = (int)MathF.Floor(fx), ib = (int)MathF.Floor(fy);
			float tx = fx - ia, ty = fy - ib;
			tx = tx * tx * (3f - 2f * tx);
			ty = ty * ty * (3f - 2f * ty);
			int a = ia - i0, b = ib - j0;
			float v00 = nodes[a + b * n], v10 = nodes[a + 1 + b * n];
			float v01 = nodes[a + (b + 1) * n], v11 = nodes[a + 1 + (b + 1) * n];
			float top = v00 + (v10 - v00) * tx;
			float bottom = v01 + (v11 - v01) * tx;
			return top + (bottom - top) * ty;
		}

		// Distance from (px, py) to the skeleton: the passages the graph opens and the halls it knocks
		// out. Only distances under the widest half-width (5 tiles) decide anything. A tile of cell (i, j)
		// lies within 6 tiles of the lattice's (12i, 12j), every centre within 3 of its own and every bend
		// within 2.5 of its passage's line, so the passages that can come that near are the east ones of
		// columns i - 2 to i + 1 and the south ones of rows j - 2 to j + 1, in the three rows or columns round
		// it; anything further starts at least 12 tiles off. A hall's edges are four of those passages (the hall pass needs all four
		// open), so only the four halls at the cell's own corners can have the tile inside them.
		private float SkeletonDistance(int tx, int ty, float px, float py)
		{
			int i0 = CellOf(tx), j0 = CellOf(ty);
			// Inside a hall at any of the cell's four corners: on the skeleton.
			for (int i = i0 - 1; i <= i0; i++)
				for (int j = j0 - 1; j <= j0; j++)
					if (hall[K(i, j)] && InsideQuad(i, j, px, py))
						return 0f;

			float best = float.MaxValue;
			// The chambers of the nine cells round it.
			for (int i = i0 - 1; i <= i0 + 1; i++)
			{
				for (int j = j0 - 1; j <= j0 + 1; j++)
				{
					int k = K(i, j);
					if (chamber[k] <= 0f)
						continue;
					float dx = px - nodeX[k], dy = py - nodeY[k];
					float d = MathF.Sqrt(dx * dx + dy * dy) - chamber[k];
					if (d <= 0f)
						return 0f;
					best = MathF.Min(best, d);
				}
			}
			// East passages of the cells from two columns west to one east, in the three rows round it.
			for (int i = i0 - 2; i <= i0 + 1; i++)
			{
				for (int j = j0 - 1; j <= j0 + 1; j++)
				{
					int k = K(i, j);
					if (openE[k])
						best = MathF.Min(best, MathF.Min(
							SegmentDistance(px, py, nodeX[k], nodeY[k], bendEX[k], bendEY[k]),
							SegmentDistance(px, py, bendEX[k], bendEY[k], nodeX[k + 1], nodeY[k + 1])));
				}
			}
			// South passages of the cells in the three columns round it, from two rows north to one south.
			for (int i = i0 - 1; i <= i0 + 1; i++)
			{
				for (int j = j0 - 2; j <= j0 + 1; j++)
				{
					int k = K(i, j);
					if (openS[k])
						best = MathF.Min(best, MathF.Min(
							SegmentDistance(px, py, nodeX[k], nodeY[k], bendSX[k], bendSY[k]),
							SegmentDistance(px, py, bendSX[k], bendSY[k], nodeX[k + cn], nodeY[k + cn])));
				}
			}
			return best;
		}

		// The hall at the post south-east of cell (i, j): the quad of the four moved centres round it.
		// Convex, because no centre moves as much as a sixth of the pitch.
		private bool InsideQuad(int i, int j, float px, float py)
		{
			int a = K(i, j), b = a + 1, c = a + 1 + cn, d = a + cn;
			return Side(nodeX[a], nodeY[a], nodeX[b], nodeY[b], px, py) >= 0f
				&& Side(nodeX[b], nodeY[b], nodeX[c], nodeY[c], px, py) >= 0f
				&& Side(nodeX[c], nodeY[c], nodeX[d], nodeY[d], px, py) >= 0f
				&& Side(nodeX[d], nodeY[d], nodeX[a], nodeY[a], px, py) >= 0f;
		}

		// Positive on the inside of a clockwise-on-screen (y down) edge.
		private static float Side(float ax, float ay, float bx, float by, float px, float py) =>
			(bx - ax) * (py - ay) - (by - ay) * (px - ax);

		private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
		{
			float dx = bx - ax, dy = by - ay;
			float t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy);
			t = Math.Clamp(t, 0f, 1f);
			float qx = ax + dx * t - px, qy = ay + dy * t - py;
			return MathF.Sqrt(qx * qx + qy * qy);
		}

		/// <summary>
		/// Water in a share of the chambers, written into <paramref name="cells"/> (whose [0, 0] is the
		/// world tile this field was built at). A pool stands only where every tile out to
		/// CavernPoolRing past its rim is floor in the raw field - and raw floor is never rock after the
		/// passes, which only take rock away - so it always has a walkway all round it.
		/// </summary>
		public void StandPools(sbyte[,] cells, float safeRadius)
		{
			// Every chamber or hall whose pool could touch the grid.
			int first = CellOf(gridX0) - 2, last = CellOf(gridX0 + gridSize) + 1;
			int firstJ = CellOf(gridY0) - 2, lastJ = CellOf(gridY0 + gridSize) + 1;
			for (int i = first; i <= last; i++)
			{
				for (int j = firstJ; j <= lastJ; j++)
				{
					int k = K(i, j);
					// A chamber pools round its own centre; a hall round the middle of its four.
					if (chamber[k] > 0f)
						TryPool(cells, safeRadius, Hash(seed, i, j, SaltCavernPool), nodeX[k], nodeY[k]);
					if (hall[k])
						TryPool(cells, safeRadius, Hash(seed, i, j, SaltCavernPool + 1),
							(nodeX[k] + nodeX[k + 1] + nodeX[k + cn] + nodeX[k + 1 + cn]) * 0.25f,
							(nodeY[k] + nodeY[k + 1] + nodeY[k + cn] + nodeY[k + 1 + cn]) * 0.25f);
				}
			}
		}

		private void TryPool(sbyte[,] cells, float safeRadius, ulong hash, float x, float y)
		{
			var rng = new Rng(hash);
			if (rng.NextFloat() >= CavernPoolChance)
				return;
			float cx = x + (rng.NextFloat() - 0.5f) * 2f;
			float cy = y + (rng.NextFloat() - 0.5f) * 2f;
			float r = CavernPoolMinRadius + rng.NextFloat() * (CavernPoolMaxRadius - CavernPoolMinRadius);
			ulong edgeSeed = rng.NextULong();
			for (; r >= CavernPoolMinRadius; r -= 0.6f)
			{
				// Water is one of SafeZoneExcludedTerrains: none round the start.
				if (MathF.Sqrt(cx * cx + cy * cy) < safeRadius + r + 1f)
					return;
				if (!RingIsFloor(cx, cy, r + CavernPoolRing))
					continue;
				StampPool(cells, cx, cy, r, edgeSeed);
				return;
			}
		}

		private bool RingIsFloor(float cx, float cy, float reach)
		{
			int minX = (int)MathF.Floor(cx - reach), maxX = (int)MathF.Ceiling(cx + reach);
			int minY = (int)MathF.Floor(cy - reach), maxY = (int)MathF.Ceiling(cy + reach);
			for (int tx = minX; tx <= maxX; tx++)
			{
				for (int ty = minY; ty <= maxY; ty++)
				{
					float dx = tx + 0.5f - cx, dy = ty + 0.5f - cy;
					if (dx * dx + dy * dy > reach * reach)
						continue;
					if (!IsFloor(tx, ty))
						return false;
				}
			}
			return true;
		}

		private void StampPool(sbyte[,] cells, float cx, float cy, float r, ulong edgeSeed)
		{
			int minX = Math.Max(0, (int)MathF.Floor(cx - r - 1) - gridX0);
			int maxX = Math.Min(gridSize - 1, (int)MathF.Ceiling(cx + r + 1) - gridX0);
			int minY = Math.Max(0, (int)MathF.Floor(cy - r - 1) - gridY0);
			int maxY = Math.Min(gridSize - 1, (int)MathF.Ceiling(cy + r + 1) - gridY0);
			for (int x = minX; x <= maxX; x++)
			{
				for (int y = minY; y <= maxY; y++)
				{
					int tx = x + gridX0, ty = y + gridY0;
					// A ragged rim, from the tile's own hash, as StampBlob has.
					var cell = new Rng(Hash(edgeSeed, tx, ty, SaltJitter));
					float jitter = cell.NextFloat() * 0.8f - 0.4f;
					float dx = tx + 0.5f - cx, dy = ty + 0.5f - cy;
					if (MathF.Sqrt(dx * dx + dy * dy) <= r + jitter && cells[x, y] == CellFloor)
						cells[x, y] = CellWater;
				}
			}
		}
	}

	// --- Shared by the walled topologies built on the lattice (this file, LevelGenerator.Ruin.cs) -------

	/// <summary>
	/// The maze's lattice read as a graph - which cells are joined, and where the hall pass knocked a post
	/// out - for a topology that draws its own ground from it instead of the Dungeon's two-tile walls. The
	/// same blocks GenerateMaze rasterises (BuildMazeBlock, DoorMask), at the topology's own rates, so it
	/// inherits the proof that the world is connected (.ai/endgame-castle.md 7.2).
	/// </summary>
	private sealed class MazeGraph
	{
		private readonly ulong seed;
		private readonly MazeRates rates;
		private readonly Dictionary<(int, int), MazeBlock> blocks = new();

		public MazeGraph(ulong seed, in MazeRates rates)
		{
			this.seed = seed;
			this.rates = rates;
		}

		public MazeBlock Block(int i, int j, out int lx, out int ly, out int bx, out int by)
		{
			bx = FloorDiv(i, MazeBlockCells);
			by = FloorDiv(j, MazeBlockCells);
			lx = i - bx * MazeBlockCells;
			ly = j - by * MazeBlockCells;
			if (!blocks.TryGetValue((bx, by), out MazeBlock b))
			{
				b = BuildMazeBlock(seed, bx, by, rates);
				blocks[(bx, by)] = b;
			}
			return b;
		}

		/// <summary>Cells (i, j) and (i + 1, j) are joined.</summary>
		public bool OpenE(int i, int j)
		{
			MazeBlock b = Block(i, j, out int lx, out int ly, out _, out _);
			return lx < MazeBlockCells - 1 ? b.OpenE[lx, ly] : ((b.DoorsE >> ly) & 1) != 0;
		}

		/// <summary>Cells (i, j) and (i, j + 1) are joined.</summary>
		public bool OpenS(int i, int j)
		{
			MazeBlock b = Block(i, j, out int lx, out int ly, out _, out _);
			return ly < MazeBlockCells - 1 ? b.OpenS[lx, ly] : ((b.DoorsS >> lx) & 1) != 0;
		}

		/// <summary>The post south-east of cell (i, j) was knocked out: the four cells round it are one room.</summary>
		public bool Hall(int i, int j)
		{
			MazeBlock b = Block(i, j, out int lx, out int ly, out _, out _);
			return lx < MazeBlockCells - 1 && ly < MazeBlockCells - 1 && b.Hall[lx, ly];
		}
	}

	// A walled topology builds its ground in a grid this much wider than the chunk's padded grid on every
	// side, so the passes that read their neighbours (thin rock, the blobs' cleanup below - eight passes,
	// as TerrainAutotiler's) are exact on every cell handed back. Walled ground is not cleaned by the
	// streamer (no cleanup pass: it would eat corridor ends), so the blobs are cleaned here, round walls
	// that never move.
	private const int WalledFloorMargin = 10;
	private const int WalledBlobCleanupPasses = 8;

	private const sbyte CellFloor = 0;
	private const sbyte CellWall = 1;
	private const sbyte CellWater = 2;

	// Scratch reused across chunks: the streamer generates a chunk at a time while the game runs, and the
	// per-chunk garbage is what used to land collector pauses inside a chunk step (.ai/terrain.md 7).
	[ThreadStatic] private static sbyte[,] walledCells, walledRead;
	[ThreadStatic] private static string[,] walledFloor, walledFloorRead;

	private static sbyte[,] WalledScratch(int size) =>
		walledCells is { } g && g.GetLength(0) == size ? g : walledCells = new sbyte[size, size];

	private static sbyte[,] WalledReadScratch(int size) =>
		walledRead is { } g && g.GetLength(0) == size ? g : walledRead = new sbyte[size, size];

	/// <summary>
	/// The floor of a walled topology: the palette's blobs laid as on open ground, walls and water written
	/// over them, the blobs cleaned round the walls, and the chunk's padded grid copied out of the middle.
	/// <paramref name="cells"/> is the extended grid, <paramref name="ext"/> tiles past the padded one on
	/// every side. A palette laid this way should not stamp a hazard blob: cut by walls into the lattice's
	/// straight lines it comes out a ruler-cut hole, and one across a passage closes it (the Ruins lost
	/// their pits for both reasons, LevelGenerator.Ruin.cs).
	/// </summary>
	private static void LayFloorBetweenWalls(string[,] grid, in TopologyChunk chunk, sbyte[,] cells, int ext)
	{
		int size = cells.GetLength(0);
		string[,] floor = walledFloor is { } f && f.GetLength(0) == size ? f : walledFloor = new string[size, size];
		string[,] read = walledFloorRead is { } r && r.GetLength(0) == size ? r : walledFloorRead = new string[size, size];
		FillOpenGround(floor, chunk.X0 - ext, chunk.Y0 - ext, chunk.ChunkX, chunk.ChunkY, chunk.ChunkTiles,
			PaletteFor(chunk.Kind), chunk.Seed, chunk.SafeRadius);

		for (int x = 0; x < size; x++)
		{
			for (int y = 0; y < size; y++)
			{
				if (cells[x, y] == CellWall)
					floor[x, y] = WallTerrain;
				else if (cells[x, y] == CellWater)
					floor[x, y] = "water";
			}
		}

		CleanBlobsBetweenWalls(floor, read);

		int inner = chunk.Size;
		for (int x = 0; x < inner; x++)
			for (int y = 0; y < inner; y++)
				grid[x, y] = floor[x + ext, y + ext];
	}

	// TerrainAutotiler.CleanupGrid's two rules, for the blob terrains only: a floor cell hemmed in on three
	// sides by one blob terrain joins it, and a blob cell with fewer than two of its own beside it goes.
	// Walls and water never change and never count. Double buffered, so a change spreads a cell a pass.
	private static void CleanBlobsBetweenWalls(string[,] g, string[,] read)
	{
		int w = g.GetLength(0), h = g.GetLength(1);
		for (int pass = 0; pass < WalledBlobCleanupPasses; pass++)
		{
			Array.Copy(g, read, g.Length);
			bool changed = false;
			for (int x = 0; x < w; x++)
			{
				for (int y = 0; y < h; y++)
				{
					string t = read[x, y];
					if (t == WallTerrain || t == "water")
						continue;
					string a = x > 0 ? read[x - 1, y] : null, b = x + 1 < w ? read[x + 1, y] : null;
					string c = y > 0 ? read[x, y - 1] : null, d = y + 1 < h ? read[x, y + 1] : null;
					string next = t;
					if (t == BaseSentinel)
					{
						string maj = BlobMajority(a, b, c, d, out int count);
						if (maj != null && count >= 3)
							next = maj;
					}
					else if ((a == t ? 1 : 0) + (b == t ? 1 : 0) + (c == t ? 1 : 0) + (d == t ? 1 : 0) < 2)
						next = BaseSentinel;
					if (next != t)
					{
						g[x, y] = next;
						changed = true;
					}
				}
			}
			if (!changed)
				break;
		}
	}

	// The blob terrain most of the four neighbours are, walls, water and floor never counting. No
	// collection: it runs for every floor cell of every pass while the game is running.
	private static string BlobMajority(string a, string b, string c, string d, out int count)
	{
		string best = null;
		count = 0;
		ConsiderBlob(a, a, b, c, d, ref best, ref count);
		ConsiderBlob(b, a, b, c, d, ref best, ref count);
		ConsiderBlob(c, a, b, c, d, ref best, ref count);
		ConsiderBlob(d, a, b, c, d, ref best, ref count);
		return best;
	}

	private static void ConsiderBlob(string candidate, string a, string b, string c, string d, ref string best, ref int count)
	{
		if (candidate == null || candidate == BaseSentinel || candidate == WallTerrain || candidate == "water")
			return;
		int n = (a == candidate ? 1 : 0) + (b == candidate ? 1 : 0) + (c == candidate ? 1 : 0) + (d == candidate ? 1 : 0);
		if (n > count)
		{
			count = n;
			best = candidate;
		}
	}
}
