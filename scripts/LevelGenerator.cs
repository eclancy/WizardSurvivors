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
/// The result of a procedural level layout: a logical terrain grid plus the ids the
/// painter needs. <see cref="Grid"/> is indexed [x, y]; cells equal to
/// <see cref="BaseTerrain"/> are open floor (painted from <see cref="GroundTerrain"/>
/// fills when set, else <see cref="GroundMaterial"/>), every other cell is an autotile
/// terrain id resolved by <see cref="TerrainAutotiler"/>.
/// </summary>
public sealed class LevelLayout
{
	public string[,] Grid { get; init; }
	public string BaseTerrain { get; init; }
	public string GroundMaterial { get; init; }
	public string GroundTerrain { get; init; } = "";
	public IReadOnlyList<string> PropThemes { get; init; } = Array.Empty<string>();
	public StageTopology Topology { get; init; } = StageTopology.Open;
	public int Width => Grid.GetLength(0);
	public int Height => Grid.GetLength(1);
}

/// <summary>
/// Procedurally lays out a stage as a terrain grid for the curated autotile pipeline.
/// Each <see cref="StageEnvironmentKind"/> maps to a palette: a ground surface (plain
/// material or an autotile terrain like grass), a weighted set of blob terrains, a set of
/// prop themes, and a topology. Hazard terrains (lava/pit) are kept out of a safe radius
/// around the spawn point at the grid centre.
///
/// Usage:
///   var layout = new LevelGenerator().Generate(120, 120, kind, seed);
///   painter.GroundTerrain = layout.GroundTerrain; painter.GroundMaterial = layout.GroundMaterial;
///   painter.Paint(layout.Grid, layout.BaseTerrain);
/// </summary>
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
		[StageEnvironmentKind.Forest] = new Palette
		{
			GroundTerrain = "grass",
			GroundMaterial = "sand",
			Blobs = new[]
			{
				new BlobSpec("water", 1.2f, 1.8f, 3.0f),
				new BlobSpec("mossy_rock", 1.4f, 1.6f, 2.8f),
			},
			Density = 0.005f,
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
		[StageEnvironmentKind.Ruins] = new Palette
		{
			GroundMaterial = "graystone",
			Blobs = new[]
			{
				new BlobSpec("cobble", 2.2f, 2.0f, 3.4f),
				new BlobSpec("dark_dirt", 1.4f, 1.8f, 3.0f),
				new BlobSpec("grass", 1.2f, 1.8f, 3.0f),
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
				new BlobSpec("cobble", 1.6f, 2.0f, 3.2f),
				new BlobSpec("rocky_pit", 1.4f, 1.8f, 3.0f),
				new BlobSpec("dark_dirt", 1.2f, 1.8f, 3.0f),
			},
			PropThemes = new[] { "mining", "treasure", "crystal" },
		},
		[StageEnvironmentKind.Volcanic] = new Palette
		{
			GroundMaterial = "darktile",
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
			GroundMaterial = "darktile",
			Blobs = new[]
			{
				new BlobSpec("water", 2.6f, 2.2f, 3.8f),
				new BlobSpec("dark_dirt", 1.8f, 2.0f, 3.2f),
				new BlobSpec("grass", 1.2f, 1.8f, 3.0f),
				new BlobSpec("mossy_rock", 1.0f, 1.6f, 2.6f),
			},
			PropThemes = new[] { "flora", "bones", "funerary" },
		},
	};

	/// <summary>
	/// Generate a layout. <paramref name="safeRadius"/> is in tiles around the grid centre
	/// where hazard terrains are suppressed (the spawn area); <paramref name="spawnClearRadius"/>
	/// is a smaller disc forced to open floor so the player always spawns with room.
	/// </summary>
	public LevelLayout Generate(int width, int height, StageEnvironmentKind kind, ulong seed,
		float safeRadius = 6f, float spawnClearRadius = 4f)
	{
		width = Math.Max(8, width);
		height = Math.Max(8, height);
		Palette palette = Palettes.TryGetValue(kind, out var p) ? p : Palettes[StageEnvironmentKind.Ruins];

		var grid = new string[width, height];
		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++)
				grid[x, y] = BaseSentinel;

		var rng = new Random(unchecked((int)seed));
		float cx = width / 2f;
		float cy = height / 2f;

		if (palette.Topology == StageTopology.Maze)
		{
			GenerateMaze(grid, rng);
			// Open a spawn room at the exact grid centre and connect it to the maze.
			ClearSpawn(grid, cx, cy, spawnClearRadius);
		}
		else
		{
			float totalWeight = 0f;
			foreach (BlobSpec b in palette.Blobs) totalWeight += b.Weight;

			// A palette may legitimately have nothing to stamp. Without this the floor of 4 below
			// would ask PickBlob to choose from an empty array, which is an index out of range on
			// the first chapter that wanted a plain floor.
			int blobCount = palette.Blobs.Length == 0
				? 0
				: Math.Max(4, (int)(width * height * palette.Density));
			for (int i = 0; i < blobCount; i++)
			{
				BlobSpec spec = PickBlob(palette.Blobs, totalWeight, rng);
				float bx = (float)rng.NextDouble() * width;
				float by = (float)rng.NextDouble() * height;
				float radius = Lerp(spec.MinRadius, spec.MaxRadius, (float)rng.NextDouble());
				bool hazard = SafeZoneExcludedTerrains.Contains(spec.Terrain);

				StampBlob(grid, bx, by, radius, spec.Terrain, hazard, cx, cy, safeRadius, rng);
			}

			// Force an open-floor clearing around the spawn point (grid centre).
			ClearSpawn(grid, cx, cy, spawnClearRadius);
		}

		return new LevelLayout
		{
			Grid = grid,
			BaseTerrain = BaseSentinel,
			GroundMaterial = palette.GroundMaterial,
			GroundTerrain = palette.GroundTerrain,
			PropThemes = palette.PropThemes,
			Topology = palette.Topology,
		};
	}

	private static BlobSpec PickBlob(BlobSpec[] blobs, float totalWeight, Random rng)
	{
		float roll = (float)rng.NextDouble() * totalWeight;
		foreach (BlobSpec b in blobs)
		{
			roll -= b.Weight;
			if (roll <= 0f) return b;
		}
		return blobs[blobs.Length - 1];
	}

	private static void StampBlob(string[,] grid, float bx, float by, float radius, string terrain,
		bool hazard, float cx, float cy, float safeRadius, Random rng)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		int minX = Math.Max(0, (int)(bx - radius - 2));
		int maxX = Math.Min(w - 1, (int)(bx + radius + 2));
		int minY = Math.Max(0, (int)(by - radius - 2));
		int maxY = Math.Min(h - 1, (int)(by + radius + 2));

		for (int x = minX; x <= maxX; x++)
		{
			for (int y = minY; y <= maxY; y++)
			{
				float jitter = (float)(rng.NextDouble() * 2.4 - 1.2);
				float dist = MathF.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));
				if (dist > radius + jitter) continue;

				if (hazard)
				{
					float distToCenter = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
					if (distToCenter < safeRadius) continue;
				}

				grid[x, y] = terrain;
			}
		}
	}

	// --- Maze topology (recursive backtracker with wide corridors) ---

	private static void GenerateMaze(string[,] grid, Random rng)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);

		// Start with everything solid, then carve corridors.
		for (int x = 0; x < w; x++)
			for (int y = 0; y < h; y++)
				grid[x, y] = WallTerrain;

		int unit = MazeCorridor + MazeWall;
		int cols = Math.Max(3, (w - MazeWall) / unit);
		int rows = Math.Max(3, (h - MazeWall) / unit);

		var visited = new bool[cols, rows];
		var stack = new Stack<(int cx, int cy)>();

		// Start at the centre cell so the spawn point is an open corridor.
		int startX = cols / 2;
		int startY = rows / 2;
		visited[startX, startY] = true;
		CarveCell(grid, startX, startY, unit);
		stack.Push((startX, startY));

		int[] dx = { 1, -1, 0, 0 };
		int[] dy = { 0, 0, 1, -1 };
		var order = new int[] { 0, 1, 2, 3 };
		while (stack.Count > 0)
		{
			(int ccx, int ccy) = stack.Peek();
			// Fisher-Yates shuffle of the 4 directions.
			for (int i = 3; i > 0; i--)
			{
				int j = rng.Next(i + 1);
				(order[i], order[j]) = (order[j], order[i]);
			}

			bool advanced = false;
			for (int k = 0; k < 4; k++)
			{
				int d = order[k];
				int nx = ccx + dx[d];
				int ny = ccy + dy[d];
				if (nx < 0 || nx >= cols || ny < 0 || ny >= rows || visited[nx, ny])
					continue;

				visited[nx, ny] = true;
				CarveCell(grid, nx, ny, unit);
				CarvePassage(grid, ccx, ccy, nx, ny, unit);
				stack.Push((nx, ny));
				advanced = true;
				break;
			}

			if (!advanced)
				stack.Pop();
		}
	}

	// Opens the corridor block of a coarse maze cell.
	private static void CarveCell(string[,] grid, int cx, int cy, int unit)
	{
		int ox = MazeWall + cx * unit;
		int oy = MazeWall + cy * unit;
		OpenRect(grid, ox, oy, MazeCorridor, MazeCorridor);
	}

	// Opens the wall strip between two adjacent coarse cells.
	private static void CarvePassage(string[,] grid, int ax, int ay, int bx, int by, int unit)
	{
		int aox = MazeWall + ax * unit;
		int aoy = MazeWall + ay * unit;
		if (bx > ax)      OpenRect(grid, aox + MazeCorridor, aoy, MazeWall, MazeCorridor);
		else if (bx < ax) OpenRect(grid, aox - MazeWall, aoy, MazeWall, MazeCorridor);
		else if (by > ay) OpenRect(grid, aox, aoy + MazeCorridor, MazeCorridor, MazeWall);
		else              OpenRect(grid, aox, aoy - MazeWall, MazeCorridor, MazeWall);
	}

	private static void OpenRect(string[,] grid, int ox, int oy, int rw, int rh)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		for (int x = ox; x < ox + rw; x++)
			for (int y = oy; y < oy + rh; y++)
				if (x >= 0 && x < w && y >= 0 && y < h)
					grid[x, y] = BaseSentinel;
	}

	private static void ClearSpawn(string[,] grid, float cx, float cy, float radius)
	{
		if (radius <= 0f) return;
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		int minX = Math.Max(0, (int)(cx - radius - 1));
		int maxX = Math.Min(w - 1, (int)(cx + radius + 1));
		int minY = Math.Max(0, (int)(cy - radius - 1));
		int maxY = Math.Min(h - 1, (int)(cy + radius + 1));
		for (int x = minX; x <= maxX; x++)
			for (int y = minY; y <= maxY; y++)
				if (MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) <= radius)
					grid[x, y] = BaseSentinel;
	}

	private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
