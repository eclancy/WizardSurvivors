namespace WizardSurvivors.scripts;

// THE RUIN TOPOLOGY: the Mystic Ruins' broken walls (.ai/endgame-castle.md 7.3, piece V1). The chapter's
// flavour line always said "broken spires and shattered walls", and it was the open field every other
// chapter is. With the Cursed Dungeon gone into the castle it is one of the two walled chapters on the
// road before the gate (7.1, 1.7).
//
// THE DUNGEON'S LATTICE AT THE RATES THAT BROKE IT. .ai/terrain.md section 6 tried loops at 18% and halls
// at 35% for the Dungeon and threw them out: "the walls broke up into isolated slabs - an open floor with
// furniture". That is a ruin. So this is the Dungeon's maze - the same eight-tile lattice, six-tile
// corridors and two-tile walls, the same blocks and doorways, so ExplorePlacer's cell snap, the traps'
// hallway rows (the Ruins' spike plates stand in the middle of a hallway as the Dungeon's do) and every
// placement and spawn rule of walled ground apply unchanged - with three passes of its own on top:
//
//   1. BROKEN WALLS. A wall the maze keeps between two cells is drawn whole three times in five. Else a
//      gap is knocked through it - four tiles out of the middle, leaving a stub on each post, or three off
//      one end, leaving a stub on the other. A gap is never narrower than three tiles (144 px, a third
//      again the widest body in the game), so it is a way through and never a snag.
//   2. FALLEN PILLARS. A post with every passage round it open is a pillar standing alone; some have
//      fallen, and leave floor.
//   3. COURTS AND COLONNADES. A share of the 4 x 4 blocks are open squares: every wall inside the block
//      knocked through, and either every post gone too (a court: thirty tiles of open floor inside the
//      broken wall round it, where a garden or a ward ring fits and a boss has room) or every post left
//      standing (a colonnade: a hall of pillars).
//
// Every pass only ever opens what the maze closed, so the ground is at least as connected as the
// Dungeon's - connected by construction, which is what walled ground's placement rules assume and what
// RegressionChecks.ValidateNoSealedPockets holds it to. Every decision is a hash of the wall, the post or
// the block it is about, so the seam rule holds without a margin. The palette's blobs lie between the walls
// (LevelGenerator.Caverns.cs, LayFloorBetweenWalls). The pits went: kept three tiles off every wall they
// came out as black rectangles cut square along the lattice, and one across a six-tile corridor closes it.
// The walls are the Ruins' obstacle now, and the spike plates its trap.
public sealed partial class LevelGenerator
{
	static partial void BuildRuinTopology(ref TopologyBuild build)
	{
		build.Generate = GenerateRuin;
		build.Walled = true;
	}

	/// <summary>The Ruins' maze: 18% loops and 35% halls (.ai/terrain.md section 6's rejected rates), the Dungeon's braid and doorways.</summary>
	private static MazeRates RuinRates => new(braid: 0.9f, loop: 0.18f, hall: 0.35f, door: 0.7f);

	// How a closed wall is drawn: whole, a gap out of its middle (stubs of one tile on each post), or a gap
	// off one end (a stub of three on the other). The rest of the hundred is whole.
	// Most stay whole: at those rates the maze keeps only about one wall in five between its cells, and
	// with half of those broken as well there were no partial corridors left, only pillars.
	private const float RuinGapMiddleChance = 0.2f;
	private const float RuinGapEndChance = 0.2f;
	// A pillar standing alone that has fallen. Half: at fewer, the posts that stand alone on the
	// eight-tile lattice read as a grid of pillars running to the horizon.
	private const float RuinFallenPillarChance = 0.5f;
	// Blocks that are an open court (no posts) or a colonnade (every post standing).
	private const float RuinCourtChance = 0.12f;
	// Colonnades are rare: a hall of pillars on the eight-tile lattice is the most regular thing a ruin can show.
	private const float RuinColonnadeChance = 0.06f;

	private const int SaltRuinStripE = 0x7A11;
	private const int SaltRuinStripS = 0x7A23;
	private const int SaltRuinPillar = 0x7A35;
	private const int SaltRuinBlock = 0x7A47;

	private static void GenerateRuin(string[,] grid, TopologyChunk chunk)
	{
		int ext = WalledFloorMargin;
		int size = chunk.Size + 2 * ext;
		int ex0 = chunk.X0 - ext;
		int ey0 = chunk.Y0 - ext;
		var ruin = new RuinLattice(chunk.Seed);

		sbyte[,] cells = WalledScratch(size);
		for (int x = 0; x < size; x++)
		{
			int u = x + ex0 + MazeLatticeShift;
			int i = FloorDiv(u, MazeUnit);
			int a = u - i * MazeUnit;
			for (int y = 0; y < size; y++)
			{
				int v = y + ey0 + MazeLatticeShift;
				int j = FloorDiv(v, MazeUnit);
				int b = v - j * MazeUnit;
				cells[x, y] = ruin.IsWall(i, j, a, b) ? CellWall : CellFloor;
			}
		}

		LayFloorBetweenWalls(grid, chunk, cells, ext);
	}

	/// <summary>The Ruins' lattice: the maze graph with the courts and colonnades opened, and how each wall and post is drawn.</summary>
	private sealed class RuinLattice
	{
		private readonly ulong seed;
		private readonly MazeGraph graph;

		public RuinLattice(ulong seed)
		{
			this.seed = seed;
			graph = new MazeGraph(seed, RuinRates);
		}

		// 0 an ordinary block, 1 a court, 2 a colonnade: a hash of the block.
		private int Plaza(int i, int j, out int lx, out int ly)
		{
			int bx = FloorDiv(i, MazeBlockCells), by = FloorDiv(j, MazeBlockCells);
			lx = i - bx * MazeBlockCells;
			ly = j - by * MazeBlockCells;
			float roll = new Rng(Hash(seed, bx, by, SaltRuinBlock)).NextFloat();
			return roll < RuinCourtChance ? 1 : roll < RuinCourtChance + RuinColonnadeChance ? 2 : 0;
		}

		/// <summary>Cells (i, j) and (i + 1, j) are joined: inside a court or colonnade always, else as the maze says.</summary>
		public bool OpenE(int i, int j) =>
			(Plaza(i, j, out int lx, out _) != 0 && lx < MazeBlockCells - 1) || graph.OpenE(i, j);

		/// <summary>Cells (i, j) and (i, j + 1) are joined.</summary>
		public bool OpenS(int i, int j) =>
			(Plaza(i, j, out _, out int ly) != 0 && ly < MazeBlockCells - 1) || graph.OpenS(i, j);

		/// <summary>The post south-east of cell (i, j) is floor: a hall, a court's, or a pillar that fell.</summary>
		private bool PostOpen(int i, int j)
		{
			int kind = Plaza(i, j, out int lx, out int ly);
			bool inside = lx < MazeBlockCells - 1 && ly < MazeBlockCells - 1;
			if (inside && kind == 1)
				return true;
			if (inside && kind == 2)
				return false;
			if (graph.Hall(i, j))
				return true;
			// A pillar standing alone - all four walls round it open - may have fallen.
			if (OpenE(i, j) && OpenE(i, j + 1) && OpenS(i, j) && OpenS(i + 1, j))
				return new Rng(Hash(seed, i, j, SaltRuinPillar)).NextFloat() < RuinFallenPillarChance;
			return false;
		}

		/// <summary>
		/// Whether the tile at offset (a, b) inside lattice unit (i, j) is wall: (a, b) in [0, 8), the
		/// corridor block below six on both axes, the east strip at a of six or seven, the south strip at
		/// b of six or seven, and the post at both.
		/// </summary>
		public bool IsWall(int i, int j, int a, int b)
		{
			bool corridorCol = a < MazeCorridor, corridorRow = b < MazeCorridor;
			if (corridorCol && corridorRow)
				return false;
			if (!corridorCol && corridorRow)
				return !OpenE(i, j) && !InGap(Hash(seed, i, j, SaltRuinStripE), b);
			if (corridorCol)
				return !OpenS(i, j) && !InGap(Hash(seed, i, j, SaltRuinStripS), a);
			return !PostOpen(i, j);
		}

		// Where along a closed wall (0 to 5, from its west or north end) a gap has been knocked through.
		private static bool InGap(ulong wall, int along)
		{
			float roll = new Rng(wall).NextFloat();
			if (roll < RuinGapMiddleChance)
				return along >= 1 && along <= 4;
			if (roll < RuinGapMiddleChance + RuinGapEndChance * 0.5f)
				return along <= 2;
			if (roll < RuinGapMiddleChance + RuinGapEndChance)
				return along >= 3;
			return false;
		}
	}
}
