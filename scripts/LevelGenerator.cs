using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>Broad generation topology for a stage archetype.</summary>
public enum StageTopology
{
	Open,   // scattered organic blobs on open ground (forest, desert, swamp...)
	Maze,   // wall-tile labyrinth with wide corridors (castle)
}

/// <summary>
/// What a stage's ground is made of: the ids the painter needs and the prop themes, with no grid.
/// Cells equal to <see cref="BaseTerrain"/> are open floor (painted from
/// <see cref="GroundTerrain"/> fills when set, else <see cref="GroundMaterial"/>), every other
/// cell is an autotile terrain id resolved by <see cref="TerrainAutotiler"/>. The grid itself is
/// produced a chunk at a time by <see cref="LevelGenerator.GenerateChunk"/>.
/// </summary>
public sealed class LevelLayout
{
	public string BaseTerrain { get; init; }
	public string GroundMaterial { get; init; }
	public string GroundTerrain { get; init; } = "";
	public IReadOnlyList<string> PropThemes { get; init; } = Array.Empty<string>();
	public StageTopology Topology { get; init; } = StageTopology.Open;
	/// <summary>Every non-floor terrain id this stage can generate, for building tile sources up front.</summary>
	public IReadOnlyList<string> Terrains { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Procedurally lays out a stage, one square chunk at a time, for the curated autotile pipeline.
/// Each <see cref="StageEnvironmentKind"/> maps to a palette: a ground surface (plain
/// material or an autotile terrain like grass), a weighted set of blob terrains, a set of
/// prop themes, and a topology. Hazard terrains (lava/pit/water) are kept out of a safe radius
/// around the spawn point, which is world tile (0, 0).
/// </summary>
/// <remarks>
/// THE WORLD HAS NO EDGE (GitHub issue #35). It used to be one 181x181 grid drawn at stage start,
/// with the player clamped inside it. Now every cell is a pure function of (stage seed, world tile):
/// <see cref="GenerateChunk"/> can be asked for any chunk, in any order, any number of times, and
/// the same tile always comes out the same. That is the whole contract TerrainStreamer relies on to
/// load ground ahead of the player and forget it behind him. Three things make it hold:
///
///   - Blobs are scattered per CHUNK from a seed hashed from the chunk coordinate, and a chunk is
///     stamped with every blob of its eight neighbours too, in one global order (row, column,
///     index), so a lake that straddles a border is the same lake from both sides.
///   - The per-cell edge jitter is a hash of the world tile, not the next number off an RNG whose
///     position would depend on how much had been drawn before it.
///   - The maze is a lattice in world tiles, and passages across a block border are decided by a
///     hash of that border, so both blocks agree on where the doorways are (see GenerateMaze).
///
/// The returned grid includes a margin of neighbouring cells on every side; cleanup and autotile
/// roles need to see past the chunk's edge to get its border cells right.
/// </remarks>
public sealed class LevelGenerator
{
	private readonly struct BlobSpec
	{
		public readonly string Terrain;
		public readonly float Weight;
		public readonly float MinRadius;
		public readonly float MaxRadius;

		public BlobSpec(string terrain, float weight, float minRadius, float maxRadius)
		{
			Terrain = terrain;
			Weight = weight;
			MinRadius = minRadius;
			MaxRadius = maxRadius;
		}
	}

	private sealed class Palette
	{
		public string GroundMaterial;
		public string GroundTerrain = "";     // autotile terrain used as the solid base (optional)
		public BlobSpec[] Blobs;
		public float Density = 0.006f;
		public string[] PropThemes = Array.Empty<string>();
		public StageTopology Topology = StageTopology.Open;
	}

	// Terrains that must not generate inside the player's safe zone. The comment always said
	// "walkable-blocking / damaging" and the set only ever held the damaging half; water joined the
	// blocking half the day it was given a collision body, and a lake generated over the spawn
	// point would start the run with the player inside a wall.
	private static readonly HashSet<string> SafeZoneExcludedTerrains = new() { "lava", "pit", "water" };

	private const string BaseSentinel = "ground";
	public const string WallTerrain = "wall";

	// Maze layout (in tiles): corridor width and wall thickness.
	private const int MazeCorridor = 6;
	private const int MazeWall = 2;

	private static readonly Dictionary<StageEnvironmentKind, Palette> Palettes = new()
	{
		// THE FOREST FLOOR (.ai/terrain.md section 8). It used to stamp `mossy_rock` - black cobble
		// with an outline, two value steps under the turf - and that was the "large dark patches of
		// something unrecognisable". Now four things a wood floor actually has, each within a step of
		// the grass and each fading into it: shaded moss and leaf litter, a lighter clearing, a
		// patch of bare earth, and the ponds. Bigger and a little denser than before, because the
		// variation has to come from here: a fill tile can only vary inside its own 48 px.
		[StageEnvironmentKind.Forest] = new Palette
		{
			GroundTerrain = "grass",
			GroundMaterial = "sand",
			Blobs = new[]
			{
				new BlobSpec("water", 1.0f, 2.0f, 3.4f),
				new BlobSpec("moss", 1.4f, 2.2f, 3.8f),
				new BlobSpec("meadow", 1.2f, 2.4f, 4.0f),
				new BlobSpec("dirt", 0.5f, 1.8f, 2.8f),
			},
			Density = 0.0065f,
			PropThemes = new[] { "flora" },
			Topology = StageTopology.Open,
		},
		// The kids' chapter: a page, and nothing on it but paper. No blobs at all, which is the
		// only palette here with none - water, moss and lava are things that happen to a PLACE,
		// and this one is a sheet of squared paper.
		[StageEnvironmentKind.Sketchbook] = new Palette
		{
			GroundMaterial = "paper",
			Blobs = Array.Empty<BlobSpec>(),
			Density = 0f,
			PropThemes = Array.Empty<string>(),
			Topology = StageTopology.Open,
		},
		[StageEnvironmentKind.Castle] = new Palette
		{
			GroundMaterial = "stonetile",
			Blobs = new[]
			{
				new BlobSpec("cobble", 2.4f, 2.0f, 3.6f),
				new BlobSpec("water", 0.8f, 1.6f, 2.6f),
				new BlobSpec("pit", 0.8f, 1.4f, 2.2f),
			},
			PropThemes = new[] { "torture", "bones", "funerary" },
			Topology = StageTopology.Maze,
		},
		// `overgrowth`, not `grass`: the Forest's turf stamped opaque on black stone was a lawn on a
		// dungeon floor. Overgrowth is partly transparent and lets the stone show between the stems.
		[StageEnvironmentKind.Ruins] = new Palette
		{
			GroundMaterial = "graystone",
			Blobs = new[]
			{
				new BlobSpec("cobble", 2.2f, 2.0f, 3.4f),
				new BlobSpec("dark_dirt", 1.4f, 1.8f, 3.0f),
				new BlobSpec("overgrowth", 1.4f, 2.0f, 3.6f),
				new BlobSpec("pit", 0.7f, 1.4f, 2.2f),
			},
			PropThemes = new[] { "funerary", "bones", "mining" },
		},
		[StageEnvironmentKind.Ice] = new Palette
		{
			GroundMaterial = "graystone",
			Blobs = new[]
			{
				new BlobSpec("ice", 3.2f, 2.6f, 4.4f),
				new BlobSpec("water", 1.2f, 2.0f, 3.2f),
			},
			PropThemes = new[] { "crystal", "bones" },
		},
		[StageEnvironmentKind.Desert] = new Palette
		{
			GroundMaterial = "sand",
			Blobs = new[]
			{
				// Buried paving and spills of scree. dark_dirt went: a purple-brown loam patch on
				// bright sand was the jarring kind of change, and a desert has no soil to show.
				new BlobSpec("cobble", 1.6f, 2.0f, 3.4f),
				new BlobSpec("rocky_pit", 1.8f, 2.0f, 3.4f),
			},
			PropThemes = new[] { "mining", "treasure", "crystal" },
		},
		[StageEnvironmentKind.Volcanic] = new Palette
		{
			// Basalt, not darktile: a square grid of black lines is a floor somebody laid, and
			// whose seams moved per variant, so the grid broke at every tile.
			GroundMaterial = "basalt",
			Blobs = new[]
			{
				new BlobSpec("lava", 3.0f, 2.2f, 4.0f),
				new BlobSpec("rocky_pit", 1.4f, 1.8f, 3.0f),
				new BlobSpec("pit", 1.2f, 1.6f, 2.6f),
			},
			PropThemes = new[] { "bones", "mining" },
		},
		[StageEnvironmentKind.Swamp] = new Palette
		{
			// Mud and moss, not tiles and turf: the Swamp was black squared paving with bright teal
			// lawns stamped on it.
			GroundMaterial = "mud",
			Blobs = new[]
			{
				new BlobSpec("water", 2.6f, 2.2f, 3.8f),
				new BlobSpec("dark_dirt", 1.6f, 2.0f, 3.2f),
				new BlobSpec("moss", 1.4f, 2.0f, 3.4f),
				new BlobSpec("mossy_rock", 0.9f, 1.6f, 2.6f),
			},
			PropThemes = new[] { "flora", "bones", "funerary" },
		},
		// THE FALLOW FIELDS (.ai/campaign.md section 5): a harvested field under no light. The floor
		// is `stubble` - a terrain used as the ground, the way the Forest stands on `grass`, because
		// a chapter's art module can add terrains but not floor styles (tools/art/terrain_fields.py
		// says why) - with patches of bare ploughed `furrow`, the grassy `hedge_bank` the hedgerows
		// grow on, and a few millponds. Walkable terrains only, plus water - a new BLOCKING terrain
		// has to agree in four shared places (.ai/terrain.md), which is not a chapter's to edit.
		// Ploughed land is the big patch and the ponds are rare: a field is mostly open ground,
		// which is what a gentle second chapter wants.
		[StageEnvironmentKind.Fields] = new Palette
		{
			GroundTerrain = "stubble",
			GroundMaterial = "mud",
			Blobs = new[]
			{
				new BlobSpec("furrow", 1.8f, 2.6f, 4.6f),
				new BlobSpec("hedge_bank", 1.1f, 1.6f, 2.8f),
				new BlobSpec("water", 0.45f, 1.8f, 2.8f),
			},
			Density = 0.006f,
			PropThemes = new[] { "flora" },
		},
		// THE BROKEN CIRCLE (.ai/campaign.md section 6): a high heath. The floor is heather on peat
		// (tools/art/terrain_heath.py, violet's deep half), and the blobs are what a moor has between
		// the heather: bare black peat where it has been stripped, granite outcrops breaking through
		// (partly transparent, so the heather shows between the slabs), a little scree, and the
		// tarns - water, the one blocker, which is the only terrain here the four shared places of
		// .ai/terrain.md had to know about already. The heather itself is the detail layer's
		// (GroundDetail, detail_heath.py), laid in drifts.
		[StageEnvironmentKind.Heath] = new Palette
		{
			GroundMaterial = "heather",
			Blobs = new[]
			{
				new BlobSpec("peat", 1.6f, 2.2f, 3.8f),
				new BlobSpec("granite", 1.3f, 2.0f, 3.4f),
				new BlobSpec("rocky_pit", 0.6f, 1.6f, 2.6f),
				new BlobSpec("water", 0.7f, 1.6f, 2.8f),
			},
			Density = 0.006f,
			// Bracken (the flora set's ferns) and the bones of what died on the moor. Not the
			// funerary set: the heath's monuments are its own standing stones (BuildDecorProps).
			PropThemes = new[] { "flora", "bones" },
		},
	};

	/// <summary>The ids a stage paints with and the prop themes it scatters. No grid.</summary>
	public LevelLayout Describe(StageEnvironmentKind kind)
	{
		Palette palette = PaletteFor(kind);
		return new LevelLayout
		{
			BaseTerrain = BaseSentinel,
			GroundMaterial = palette.GroundMaterial,
			GroundTerrain = palette.GroundTerrain,
			PropThemes = palette.PropThemes,
			Topology = palette.Topology,
			Terrains = palette.Topology == StageTopology.Maze
				? new[] { WallTerrain }
				: Array.ConvertAll(palette.Blobs, b => b.Terrain),
		};
	}

	private static Palette PaletteFor(StageEnvironmentKind kind) =>
		Palettes.TryGetValue(kind, out var p) ? p : Palettes[StageEnvironmentKind.Ruins];

	/// <summary>
	/// The raw terrain of one chunk plus <paramref name="margin"/> cells of its neighbours on every
	/// side, so the grid is (chunkTiles + 2 * margin) square and index [0, 0] is world tile
	/// (chunk * chunkTiles - margin). Raw: not cleaned and not resolved to roles; the caller does
	/// both on the whole padded grid and keeps only the middle.
	/// </summary>
	/// <param name="safeRadius">Tiles around world tile (0, 0) where SafeZoneExcludedTerrains never generate.</param>
	/// <param name="spawnClearRadius">A smaller disc round (0, 0) forced to open floor, so the run starts with room.</param>
	public string[,] GenerateChunk(StageEnvironmentKind kind, ulong seed, int chunkX, int chunkY,
		int chunkTiles, int margin, float safeRadius = 6f, float spawnClearRadius = 4f)
	{
		int size = chunkTiles + margin * 2;
		var grid = new string[size, size];
		GenerateChunkInto(grid, kind, seed, chunkX, chunkY, chunkTiles, margin, safeRadius, spawnClearRadius);
		return grid;
	}

	/// <summary>
	/// <see cref="GenerateChunk"/> into a caller-owned grid of (chunkTiles + 2 * margin) square, so a
	/// streamer generating chunks during play can reuse one buffer instead of making garbage.
	/// </summary>
	public void GenerateChunkInto(string[,] grid, StageEnvironmentKind kind, ulong seed, int chunkX, int chunkY,
		int chunkTiles, int margin, float safeRadius = 6f, float spawnClearRadius = 4f)
	{
		Palette palette = PaletteFor(kind);
		int size = chunkTiles + margin * 2;
		if (grid.GetLength(0) != size || grid.GetLength(1) != size)
			throw new ArgumentException($"GenerateChunkInto: grid must be {size} x {size}.");
		int x0 = chunkX * chunkTiles - margin;
		int y0 = chunkY * chunkTiles - margin;

		if (palette.Topology == StageTopology.Maze)
		{
			GenerateMaze(grid, x0, y0, seed);
		}
		else
		{
			for (int x = 0; x < size; x++)
				for (int y = 0; y < size; y++)
					grid[x, y] = BaseSentinel;
			StampBlobs(grid, x0, y0, chunkX, chunkY, chunkTiles, palette, seed, safeRadius);
		}

		// Force an open-floor clearing around the spawn point. On a maze this opens a small room
		// round the start, which is what the old single-grid maze did with its centre too.
		ClearSpawn(grid, x0, y0, spawnClearRadius);
	}

	// --- Hashing ----------------------------------------------------------------------------
	//
	// Everything below draws from a hash of WHERE it is rather than from one RNG walked in
	// order, because the order chunks are generated in depends on which way the player walked.

	private const int SaltBlobs = 0x51A7;
	private const int SaltJitter = 0x7E11;
	private const int SaltMazeBlock = 0x3A2E;
	private const int SaltDoorEast = 0x0EA5;
	private const int SaltDoorSouth = 0x5007;

	/// <summary>SplitMix64 finaliser: a cheap, well-mixed 64-bit hash step.</summary>
	public static ulong Mix(ulong z)
	{
		z += 0x9E3779B97F4A7C15UL;
		z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
		z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
		return z ^ (z >> 31);
	}

	/// <summary>A hash of a seed, a 2D integer coordinate and a salt naming what it is for.</summary>
	public static ulong Hash(ulong seed, int x, int y, int salt) =>
		Mix(seed ^ Mix(((ulong)(uint)x) | ((ulong)(uint)y << 32)) ^ Mix((ulong)(uint)salt * 0x632BE59BD9B4E019UL));

	/// <summary>A small allocation-free generator seeded from <see cref="Hash"/>.</summary>
	public struct Rng
	{
		private ulong state;
		public Rng(ulong seed) { state = seed; }
		public ulong NextULong() { state += 0x9E3779B97F4A7C15UL; return Mix(state); }
		public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
		public float NextFloat() => (float)NextDouble();
		public int Next(int exclusiveMax) => exclusiveMax <= 1 ? 0 : (int)(NextDouble() * exclusiveMax);
	}

	private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

	// --- Open topology ----------------------------------------------------------------------

	// Blobs per chunk come from the chunk's own seed, and a chunk is stamped with its eight
	// neighbours' blobs as well: the biggest blob reaches 4.4 + 1.2 tiles from its centre and the
	// margin is far less than a chunk, so nothing further away can touch the padded grid. Stamped in
	// one global order (row, then column, then index) so that where two blobs overlap, the same one
	// wins whichever chunk is asking.
	private static void StampBlobs(string[,] grid, int x0, int y0, int chunkX, int chunkY, int chunkTiles,
		Palette palette, ulong seed, float safeRadius)
	{
		if (palette.Blobs.Length == 0 || palette.Density <= 0f)
			return;

		float totalWeight = 0f;
		foreach (BlobSpec b in palette.Blobs) totalWeight += b.Weight;

		// The old single grid stamped width * height * Density blobs over the whole arena; the
		// same density per chunk keeps the ground looking the way it was tuned.
		float expected = chunkTiles * chunkTiles * palette.Density;
		int whole = (int)expected;
		float fraction = expected - whole;

		for (int by = chunkY - 1; by <= chunkY + 1; by++)
		{
			for (int bx = chunkX - 1; bx <= chunkX + 1; bx++)
			{
				var rng = new Rng(Hash(seed, bx, by, SaltBlobs));
				int count = whole + (rng.NextDouble() < fraction ? 1 : 0);
				for (int i = 0; i < count; i++)
				{
					BlobSpec spec = PickBlob(palette.Blobs, totalWeight, ref rng);
					float cx = bx * chunkTiles + rng.NextFloat() * chunkTiles;
					float cy = by * chunkTiles + rng.NextFloat() * chunkTiles;
					float radius = Lerp(spec.MinRadius, spec.MaxRadius, rng.NextFloat());
					ulong jitterSeed = rng.NextULong();
					bool excluded = SafeZoneExcludedTerrains.Contains(spec.Terrain);
					StampBlob(grid, x0, y0, cx, cy, radius, spec.Terrain, excluded, safeRadius, jitterSeed);
				}
			}
		}
	}

	private static BlobSpec PickBlob(BlobSpec[] blobs, float totalWeight, ref Rng rng)
	{
		float roll = rng.NextFloat() * totalWeight;
		foreach (BlobSpec b in blobs)
		{
			roll -= b.Weight;
			if (roll <= 0f) return b;
		}
		return blobs[blobs.Length - 1];
	}

	// (cx, cy) and the radius are in world tiles; the grid's [0, 0] is world tile (x0, y0).
	private static void StampBlob(string[,] grid, int x0, int y0, float cx, float cy, float radius, string terrain,
		bool excludedFromSafeZone, float safeRadius, ulong jitterSeed)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		int minX = Math.Max(0, (int)MathF.Floor(cx - radius - 2) - x0);
		int maxX = Math.Min(w - 1, (int)MathF.Ceiling(cx + radius + 2) - x0);
		int minY = Math.Max(0, (int)MathF.Floor(cy - radius - 2) - y0);
		int maxY = Math.Min(h - 1, (int)MathF.Ceiling(cy + radius + 2) - y0);

		for (int x = minX; x <= maxX; x++)
		{
			int tx = x + x0;
			for (int y = minY; y <= maxY; y++)
			{
				int ty = y + y0;
				// The ragged edge, from the tile's own hash: the same tile gets the same jitter
				// from every chunk that stamps it.
				var cell = new Rng(Hash(jitterSeed, tx, ty, SaltJitter));
				float jitter = cell.NextFloat() * 2.4f - 1.2f;
				float dist = MathF.Sqrt((tx - cx) * (tx - cx) + (ty - cy) * (ty - cy));
				if (dist > radius + jitter) continue;

				if (excludedFromSafeZone && DistanceToSpawn(tx, ty) < safeRadius)
					continue;

				grid[x, y] = terrain;
			}
		}
	}

	// Distance in tiles from a tile's centre to the spawn point, which is the corner of tile (0, 0):
	// the player starts at the stage origin and the painter puts tile (0, 0) just below-right of it.
	private static float DistanceToSpawn(int tx, int ty)
	{
		float dx = tx + 0.5f;
		float dy = ty + 0.5f;
		return MathF.Sqrt(dx * dx + dy * dy);
	}

	// --- Maze topology ----------------------------------------------------------------------
	//
	// A recursive backtracker on its own makes a PERFECT maze: exactly one route between any two
	// cells, long dead ends, and neighbours seen through one wall that are twenty tiles of corridor
	// apart. That is a puzzle, and this is not a puzzle game - with the camera at 0.85 a player sees
	// four or five corridors at once, and on the Cursed Dungeon most of the horde was visible in
	// them, walking a long way round and never arriving. Measured with _MazeProbe on one seed, the
	// median enemy had to walk 2.96 times the straight-line distance to reach a roaming player.
	//
	// So the perfect maze is only the skeleton. Three passes then open it up while keeping it a
	// dungeon of walled corridors rather than an open floor:
	//   1. BRAID - almost every dead end gets a second exit, preferring one into another dead end.
	//   2. LOOPS - a share of the remaining walls between neighbouring cells is knocked through.
	//   3. HALLS - where all four passages round a wall post are open, the post itself sometimes
	//      goes too, joining four corridor blocks into one room.
	//
	// AN INFINITE MAZE, IN BLOCKS. The maze is a lattice of cells in world tiles: cell (i, j) is a
	// MazeCorridor-square corridor block followed by a MazeWall-thick strip to its east and south,
	// shifted by MazeLatticeShift so that the spawn point sits in the middle of cell (0, 0)'s
	// corridor. Cells are grouped into MazeBlockCells-square blocks. Each block runs the backtracker
	// and the three passes on its own cells only, from a seed hashed from the block coordinate, so it
	// is internally connected. The passages ACROSS a block border are not either block's to decide:
	// they are a hash of the border itself (DoorMask), at least one per border, so both blocks see the
	// same doorways and every block connects to all four neighbours. Connected blocks, each connected
	// inside, is a connected world, with loops both within and between blocks.
	private const float MazeBraidChance = 0.9f;
	private const float MazeLoopChance = 0.12f;
	private const float MazeHallChance = 0.2f;
	// Chance each cell-to-cell passage across a block border is open (at least one always is).
	// Close to the share of walls a block opens inside itself (a 4 x 4 backtracker opens 15 of its
	// 24 internal walls, and the braid and loop passes take that to roughly three in four), so a
	// border does not read as a seam of solid wall. 0.5 was tried first: _MazeProbe measured the
	// median roaming detour at 1.44 against the old fixed maze's 1.18.
	private const float MazeDoorChance = 0.7f;
	private const int MazeBlockCells = 4;
	private const int MazeUnit = MazeCorridor + MazeWall;
	private const int MazeLatticeShift = MazeCorridor / 2;

	private sealed class MazeBlock
	{
		// openE[x, y]: passage between (x, y) and (x + 1, y) inside the block. openS likewise south.
		public readonly bool[,] OpenE = new bool[MazeBlockCells, MazeBlockCells];
		public readonly bool[,] OpenS = new bool[MazeBlockCells, MazeBlockCells];
		// hall[x, y]: the wall post between cells (x, y) and (x + 1, y + 1) has been removed.
		public readonly bool[,] Hall = new bool[MazeBlockCells, MazeBlockCells];
		public int DoorsE, DoorsW, DoorsS, DoorsN;
	}

	// Which of the MazeBlockCells passages across one border are open, as a bit mask. Keyed on the
	// border (the block to its west or north, plus which way it faces), so both sides read one value.
	private static int DoorMask(ulong seed, int bx, int by, int salt)
	{
		var rng = new Rng(Hash(seed, bx, by, salt));
		int mask = 0;
		for (int k = 0; k < MazeBlockCells; k++)
			if (rng.NextDouble() < MazeDoorChance)
				mask |= 1 << k;
		if (mask == 0)
			mask = 1 << rng.Next(MazeBlockCells);
		return mask;
	}

	private static MazeBlock BuildMazeBlock(ulong seed, int bx, int by)
	{
		const int n = MazeBlockCells;
		var block = new MazeBlock
		{
			DoorsE = DoorMask(seed, bx, by, SaltDoorEast),
			DoorsW = DoorMask(seed, bx - 1, by, SaltDoorEast),
			DoorsS = DoorMask(seed, bx, by, SaltDoorSouth),
			DoorsN = DoorMask(seed, bx, by - 1, SaltDoorSouth),
		};
		var rng = new Rng(Hash(seed, bx, by, SaltMazeBlock));

		var visited = new bool[n, n];
		var stack = new Stack<(int, int)>();
		int[] dx = { 1, -1, 0, 0 };
		int[] dy = { 0, 0, 1, -1 };
		int[] order = { 0, 1, 2, 3 };

		int sx = rng.Next(n), sy = rng.Next(n);
		visited[sx, sy] = true;
		stack.Push((sx, sy));
		while (stack.Count > 0)
		{
			(int cx, int cy) = stack.Peek();
			Shuffle(order, ref rng);
			bool advanced = false;
			for (int k = 0; k < 4; k++)
			{
				int nx = cx + dx[order[k]];
				int ny = cy + dy[order[k]];
				if (nx < 0 || nx >= n || ny < 0 || ny >= n || visited[nx, ny])
					continue;
				visited[nx, ny] = true;
				SetPassage(block, cx, cy, nx, ny);
				stack.Push((nx, ny));
				advanced = true;
				break;
			}
			if (!advanced)
				stack.Pop();
		}

		// 1. Braid. In a shuffled order so the survivors are not all in one corner. A doorway out
		// of the block counts as an exit: it is one.
		var cells = new List<(int, int)>(n * n);
		for (int x = 0; x < n; x++)
			for (int y = 0; y < n; y++)
				cells.Add((x, y));
		for (int i = cells.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(cells[i], cells[j]) = (cells[j], cells[i]);
		}

		foreach ((int x, int y) in cells)
		{
			if (CountExits(block, x, y) != 1 || rng.NextDouble() >= MazeBraidChance)
				continue;
			Shuffle(order, ref rng);
			int pick = -1;
			for (int k = 0; k < 4; k++)
			{
				int d = order[k];
				int nx = x + dx[d];
				int ny = y + dy[d];
				// Inside the block only: the border's passages belong to DoorMask.
				if (nx < 0 || nx >= n || ny < 0 || ny >= n || IsInternalOpen(block, x, y, nx, ny))
					continue;
				if (pick < 0)
					pick = d;
				// Two dead ends joined through each other cost one wall and fix both.
				if (CountExits(block, nx, ny) == 1)
				{
					pick = d;
					break;
				}
			}
			if (pick >= 0)
				SetPassage(block, x, y, x + dx[pick], y + dy[pick]);
		}

		// 2. Loops.
		for (int x = 0; x < n; x++)
		{
			for (int y = 0; y < n; y++)
			{
				if (x + 1 < n && !block.OpenE[x, y] && rng.NextDouble() < MazeLoopChance)
					block.OpenE[x, y] = true;
				if (y + 1 < n && !block.OpenS[x, y] && rng.NextDouble() < MazeLoopChance)
					block.OpenS[x, y] = true;
			}
		}

		// 3. Halls, on the posts inside the block. A post on a block border would need the other
		// block's passages to decide, so those always stand.
		for (int x = 0; x + 1 < n; x++)
		{
			for (int y = 0; y + 1 < n; y++)
			{
				if (!block.OpenE[x, y] || !block.OpenE[x, y + 1] || !block.OpenS[x, y] || !block.OpenS[x + 1, y])
					continue;
				if (rng.NextDouble() < MazeHallChance)
					block.Hall[x, y] = true;
			}
		}

		return block;
	}

	private static void Shuffle(int[] order, ref Rng rng)
	{
		for (int i = order.Length - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(order[i], order[j]) = (order[j], order[i]);
		}
	}

	private static void SetPassage(MazeBlock b, int ax, int ay, int bx, int by)
	{
		if (bx > ax) b.OpenE[ax, ay] = true;
		else if (bx < ax) b.OpenE[bx, by] = true;
		else if (by > ay) b.OpenS[ax, ay] = true;
		else b.OpenS[bx, by] = true;
	}

	private static bool IsInternalOpen(MazeBlock b, int ax, int ay, int bx, int by)
	{
		if (bx > ax) return b.OpenE[ax, ay];
		if (bx < ax) return b.OpenE[bx, by];
		if (by > ay) return b.OpenS[ax, ay];
		return b.OpenS[bx, by];
	}

	private static int CountExits(MazeBlock b, int x, int y)
	{
		const int last = MazeBlockCells - 1;
		int e = 0;
		e += x < last ? (b.OpenE[x, y] ? 1 : 0) : ((b.DoorsE >> y) & 1);
		e += x > 0 ? (b.OpenE[x - 1, y] ? 1 : 0) : ((b.DoorsW >> y) & 1);
		e += y < last ? (b.OpenS[x, y] ? 1 : 0) : ((b.DoorsS >> x) & 1);
		e += y > 0 ? (b.OpenS[x, y - 1] ? 1 : 0) : ((b.DoorsN >> x) & 1);
		return e;
	}

	// Rasterise the lattice into the padded grid. Every tile is decided from its own lattice
	// position and the (cached, per call) blocks around it, so a tile reads the same in any chunk.
	private static void GenerateMaze(string[,] grid, int x0, int y0, ulong seed)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		var blocks = new Dictionary<(int, int), MazeBlock>();

		MazeBlock BlockOf(int i, int j, out int lx, out int ly)
		{
			int bx = FloorDiv(i, MazeBlockCells);
			int by = FloorDiv(j, MazeBlockCells);
			lx = i - bx * MazeBlockCells;
			ly = j - by * MazeBlockCells;
			if (!blocks.TryGetValue((bx, by), out MazeBlock b))
			{
				b = BuildMazeBlock(seed, bx, by);
				blocks[(bx, by)] = b;
			}
			return b;
		}

		for (int x = 0; x < w; x++)
		{
			int u = x + x0 + MazeLatticeShift;
			int i = FloorDiv(u, MazeUnit);
			bool corridorCol = u - i * MazeUnit < MazeCorridor;
			for (int y = 0; y < h; y++)
			{
				int v = y + y0 + MazeLatticeShift;
				int j = FloorDiv(v, MazeUnit);
				bool corridorRow = v - j * MazeUnit < MazeCorridor;

				bool open;
				MazeBlock b = BlockOf(i, j, out int lx, out int ly);
				if (corridorCol && corridorRow)
					open = true;
				else if (!corridorCol && corridorRow)
					// The strip east of cell (i, j).
					open = lx < MazeBlockCells - 1 ? b.OpenE[lx, ly] : ((b.DoorsE >> ly) & 1) != 0;
				else if (corridorCol)
					// The strip south of cell (i, j).
					open = ly < MazeBlockCells - 1 ? b.OpenS[lx, ly] : ((b.DoorsS >> lx) & 1) != 0;
				else
					// The post south-east of cell (i, j).
					open = lx < MazeBlockCells - 1 && ly < MazeBlockCells - 1 && b.Hall[lx, ly];

				grid[x, y] = open ? BaseSentinel : WallTerrain;
			}
		}
	}

	/// <summary>Tiles from one maze cell's centre to the next: a corridor block and its wall strip.</summary>
	public const int MazeCellTiles = MazeUnit;

	/// <summary>Tiles across a maze corridor.</summary>
	public const int MazeCorridorTiles = MazeCorridor;

	/// <summary>
	/// The middle of the maze corridor <paramref name="local"/> stands in: the nearest point on the
	/// corridor's centreline, the middle of the junction it is in, or the middle of a hall.
	/// </summary>
	/// <remarks>
	/// Stage hazards stand here on the maze (.ai/terrain.md section 6, "Traps in the middle of the
	/// path"). Reported: "the traps should always be in the center of the paths they're within, not
	/// randomly next to the walls." A random point in a six-tile corridor is against a wall a third of
	/// the time, and a trap against a wall reads as a mistake and plays as a corner the swarm pins you
	/// in.
	///
	/// The lattice decides it, since it is what the walls are made from: cell (i, j)'s corridor block
	/// is centred on (i, j) * MazeCellTiles tiles from the stage origin (the lattice shift puts the
	/// spawn in the middle of cell (0, 0)), and its passages to the four neighbours are the wall strips
	/// half a cell away, open or not. <paramref name="isWall"/> answers for a point relative to the
	/// origin - the chunk data's wall flag in the game - and is asked only at a cell's four passages
	/// and four posts, never at the point itself. The point snaps:
	/// <list type="bullet">
	/// <item>into the middle of a HALL, if a post round its cell is open (the hall pass knocks out a post
	/// whose four passages are open, so the four cells round it are one room, and the post is its
	/// middle);</item>
	/// <item>otherwise onto the centreline of the arm of its cell it is nearest - east-west if it is
	/// further east or west of the cell's middle than north or south, and that way is open - keeping how
	/// far along the corridor it was;</item>
	/// <item>otherwise onto the other arm, if that way is open, and failing both onto the cell's middle
	/// (a dead end, or a corner the point was in the closed half of).</item>
	/// </list>
	/// So a snapped point is half a corridor - MazeCorridorTiles / 2 tiles - from every wall it can see,
	/// which is what RegressionChecks.ValidateMazeHazards measures, independently of all this.
	/// </remarks>
	public static Godot.Vector2 MazeCorridorCentre(Godot.Vector2 local, float tilePixels, Func<Godot.Vector2, bool> isWall)
	{
		float pitch = MazeUnit * tilePixels;
		float half = pitch * 0.5f;
		var c = new Godot.Vector2(MathF.Round(local.X / pitch) * pitch, MathF.Round(local.Y / pitch) * pitch);
		Godot.Vector2 d = local - c;

		Godot.Vector2? hall = null;
		float nearest = float.MaxValue;
		for (int sy = -1; sy <= 1; sy += 2)
		{
			for (int sx = -1; sx <= 1; sx += 2)
			{
				var post = new Godot.Vector2(c.X + sx * half, c.Y + sy * half);
				if (isWall(post))
					continue;
				float dist = (local - post).LengthSquared();
				if (dist < nearest)
				{
					nearest = dist;
					hall = post;
				}
			}
		}
		if (hall is Godot.Vector2 room)
			return room;

		bool xOpen = !isWall(new Godot.Vector2(c.X + (d.X >= 0f ? half : -half), c.Y));
		bool yOpen = !isWall(new Godot.Vector2(c.X, c.Y + (d.Y >= 0f ? half : -half)));
		var onX = new Godot.Vector2(c.X + d.X, c.Y);
		var onY = new Godot.Vector2(c.X, c.Y + d.Y);
		bool preferX = MathF.Abs(d.X) >= MathF.Abs(d.Y);
		if (xOpen && (preferX || !yOpen))
			return onX;
		if (yOpen)
			return onY;
		return c;
	}

	private static void ClearSpawn(string[,] grid, int x0, int y0, float radius)
	{
		if (radius <= 0f) return;
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		int r = (int)MathF.Ceiling(radius) + 1;
		int minX = Math.Max(0, -r - x0);
		int maxX = Math.Min(w - 1, r - x0);
		int minY = Math.Max(0, -r - y0);
		int maxY = Math.Min(h - 1, r - y0);
		for (int x = minX; x <= maxX; x++)
			for (int y = minY; y <= maxY; y++)
				if (DistanceToSpawn(x + x0, y + y0) <= radius)
					grid[x, y] = BaseSentinel;
	}

	private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
