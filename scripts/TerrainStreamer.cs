using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WizardSurvivors.scripts;

/// <summary>
/// One chunk of streamed ground: what is on each cell, and whether it is painted and furnished.
/// Arrays are indexed x + y * <see cref="TerrainStreamer.ChunkTiles"/>.
/// </summary>
public sealed class TerrainChunk
{
	public Vector2I Coord { get; init; }
	/// <summary>World cell of this chunk's [0, 0].</summary>
	public Vector2I MinCell { get; init; }
	/// <summary>Terrain id per cell; null is open floor.</summary>
	public string[] Terrain { get; init; }
	public AutotileRole[] Roles { get; init; }
	/// <summary>TerrainStreamer.Flag* bits per cell.</summary>
	public byte[] Flags { get; init; }
	/// <summary>
	/// Maze walls round each cell, for painting them: on an OPEN cell the TerrainStreamer.WallShade*
	/// bits (which of its north, west and north-west neighbours are wall - where a wall's shadow
	/// falls), on a WALL cell the TerrainStreamer.WallOpen* bits (which of its four sides are floor).
	/// Null when no wall is near the chunk. Read from the padded grid, so a cell on the chunk's border
	/// knows the neighbour chunk's cells. Paint only: nothing that decides where anything goes reads it.
	/// </summary>
	public byte[] WallNeighbours { get; init; }
	/// <summary>0 unpainted, up to TerrainStreamer.PaintedStage when fully painted.</summary>
	public int PaintStage { get; set; }
	public bool Painted => PaintStage >= TerrainStreamer.PaintedStage;
	/// <summary>How many furnishing parts have run; see TerrainStreamer.Populate.</summary>
	public int PopulatePart { get; set; }
	public bool Populated { get; set; }
	/// <summary>Props, hazards and decor placed for this chunk; freed with it.</summary>
	public List<Node> Props { get; set; }
	/// <summary>
	/// Cells a landmark stands on (Node2DGame.LandmarkCells), or null for none. Decided once from the
	/// chunk's own data and the stage seed, so it is the same on every visit, and kept with the data
	/// because decor, props and ground detail all have to stay out of it - and because its fence is
	/// stamped into <see cref="Flags"/> (TerrainStreamer.FlagFence) the moment the data exists, which
	/// is what puts the fence in the navigation grid before anything is painted.
	/// </summary>
	public Rect2I? Landmark { get; set; }
	public bool LandmarkPlanned { get; set; }

	/// <summary>
	/// Where this chunk's stage hazards go (Node2DGame.HazardPlanFor), or null until somebody asks.
	/// A pure function of the stage seed and the chunk's data, kept with the data because more than
	/// the chunk itself reads it: its later neighbours stand their own hazards clear of these, and the
	/// decor and props of every chunk round it stay off them.
	/// </summary>
	public List<StageHazardSpot> HazardPlan { get; set; }
}

/// <summary>One stage hazard a chunk will place: where, which kind, and its cycle's offset.</summary>
public readonly record struct StageHazardSpot(Vector2 Position, bool Flame, float Phase);

/// <summary>
/// The ground of an endless stage: generates square chunks from (stage seed, chunk coordinate),
/// keeps the ones round the player painted and furnished, and forgets the rest.
/// </summary>
/// <remarks>
/// WHY CHUNKS, AND WHY THESE NUMBERS (GitHub issue #35, .ai/terrain.md "Chunk streaming").
///
/// The stage used to be one 181x181-tile grid painted at the start, and the player was clamped
/// inside it. Now nothing ends. A chunk is <see cref="ChunkTiles"/> = 32 tiles (1536 px) on a side.
/// Every chunk within <see cref="LoadRadius"/> = 2 (Chebyshev) of the player's is painted - 5 x 5,
/// 160 tiles square, about the area of the old arena, so memory and node count are what they were.
/// The nearest unpainted ground is then at least two whole chunks, 3072 px, from the player whatever
/// cell he stands in, which covers the widest view (a 32:9 monitor at keep_height and camera zoom
/// 0.85 sees 1506 px either side), the spawn ring (screen edge x 1.08 + 330), Enemy.RespawnDistance
/// (1600) and MazeNavigation's 64-tile walking horizon. Painted chunks are forgotten past
/// <see cref="UnloadRadius"/> (3), one more than they load at, so walking back and forth across a
/// border does not paint and forget the same column every few steps.
///
/// WORK ONLY HAPPENS AT A BORDER. <see cref="Update"/> compares the player's chunk with the last one
/// - two integer divides, no allocation - and does nothing else until it changes. Crossing queues the
/// new column, and the queue is drained ONE STEP PER FRAME: generate a chunk's data, or paint it, or
/// furnish it. The new chunks are two chunks away when they are queued, so there are seconds to do it
/// in and no reason to do it all in one frame on a phone with 140 enemies on screen.
///
/// THE SEAM RULE. A chunk's cells must come out identical whichever side asked, or there is a line
/// across the world at every border. LevelGenerator makes the raw terrain a pure function of the
/// world tile; this pads each chunk with <see cref="Margin"/> cells of its neighbours before cleanup
/// and autotiling, and keeps only the middle. TerrainAutotiler's cleanup spreads a change at most one
/// cell per pass over MaxCleanupPasses (8) passes, and a role reads one cell further, so a margin of
/// 10 leaves every kept cell exactly what a single infinite grid would have given it.
///
/// THE FOUR BLOCKING AUTHORITIES (.ai/terrain.md section 2) all read the same chunk: the painter's
/// collision polygons are painted from it, <see cref="IsBlockedAtWorld"/> and the wall flag answer
/// Node2DGame.IsImpassablePosition from it, prop scatter avoids terrains through
/// <see cref="TerrainAtWorld"/>, and MazeNavigation's window is filled from it by
/// <see cref="FillNavigation"/>. A query about ground nobody has generated generates its data (not its
/// paint), so an answer is never "unknown" and never differs from what will later be painted there.
/// </remarks>
public sealed class TerrainStreamer
{
	public const int ChunkTiles = 32;
	public const int Margin = 10;

	private const int GroundPaintSteps = 2;
	/// <summary>TerrainChunk.PaintStage once the ground halves, the overlay and the water are all painted.</summary>
	public const int PaintedStage = GroundPaintSteps + 3;

	public const byte FlagHazard = 1;
	public const byte FlagBlocked = 2;
	public const byte FlagWall = 4;
	/// <summary>
	/// A landmark's fence (FlowerPenLayout): a body cannot walk into it, but a shot flies over it, so
	/// it is solid in the navigation grid and not a wall. Not blocking terrain either - nothing about
	/// the ground changes, and water's shape (FillWater) must never see it.
	/// </summary>
	public const byte FlagFence = 8;

	/// <summary>TerrainChunk.WallNeighbours on an open cell: a maze wall north of it, west of it, north-west of it.</summary>
	public const byte WallShadeN = 1;
	public const byte WallShadeW = 2;
	public const byte WallShadeNW = 4;
	/// <summary>
	/// TerrainChunk.WallNeighbours on a wall cell: floor north, south, west, east of it. The autotile role
	/// says the same for every ordinary wall cell; it is the spawn clearing's one-tile stubs, which all
	/// resolve to Patch, that need the sides spelled out (LevelTilePainter.WallRoleKey).
	/// </summary>
	public const byte WallOpenN = 16;
	public const byte WallOpenS = 32;
	public const byte WallOpenW = 64;
	public const byte WallOpenE = 128;

	/// <summary>Chunks painted round the player, Chebyshev.</summary>
	public int LoadRadius { get; init; } = 2;
	/// <summary>Painted chunks further than this are forgotten.</summary>
	public int UnloadRadius => LoadRadius + 1;
	/// <summary>
	/// Unpainted chunk DATA is kept this far out. Furnishing a chunk can put a cluster member a
	/// little over its border and has to ask what terrain is there, so the ring outside the painted
	/// square needs data; and it is a few kilobytes a chunk.
	/// </summary>
	public int DataRadius => LoadRadius + 2;

	/// <summary>Set by --terrain-log: one line per border crossing with step timings and node count.</summary>
	public static bool LogTimings { get; set; }

	/// <summary>
	/// Furnishes a freshly painted chunk (props, decor, stage hazards), one PART per call so a
	/// heavy chunk is spread over frames: called with part 0, 1, 2... until it returns true. Adds
	/// whatever it makes to chunk.Props, which are freed when the chunk is forgotten. Set by Node2DGame.
	/// </summary>
	public Func<TerrainChunk, int, bool> Populate { get; set; }

	/// <summary>
	/// Runs once on every chunk's data as soon as it is generated, before anything can read it: where
	/// Node2DGame plans the chunk's landmark and stamps its fence into the flags. Data, not paint - a
	/// chunk the navigation window reaches into is generated without ever being painted, and its
	/// fence must already be in the grid. Must be a pure function of the chunk, like the rest of it.
	/// </summary>
	public Action<TerrainChunk> PlanFeatures { get; set; }

	public bool IsSettled => pending.Count == 0;
	public Vector2I CentreChunk => centre;
	public int TilePixels => tilePx;
	public int DataChunkCount => chunks.Count;
	public int PaintedChunkCount => painter.PaintedChunkCount;

	private readonly LevelTilePainter painter;
	private readonly LevelGenerator generator = new();
	private readonly TerrainAutotiler autotiler = new();
	private readonly StageEnvironmentKind kind;
	private readonly ulong seed;
	private readonly int tilePx;
	private readonly Vector2 origin;
	private readonly float safeRadius;
	private readonly float spawnClearRadius;
	private readonly bool cleanup;
	private readonly string baseTerrain;

	private const int PaddedTiles = ChunkTiles + 2 * Margin;
	private readonly string[,] gridBuffer = new string[PaddedTiles, PaddedTiles];
	private readonly string[,] cleanupBuffer = new string[PaddedTiles, PaddedTiles];
	private readonly TileResolution[,] resolveBuffer = new TileResolution[PaddedTiles, PaddedTiles];

	private readonly Dictionary<Vector2I, TerrainChunk> chunks = new();
	// Chunks still to finish, outermost first, so the nearest is popped off the end.
	private readonly List<Vector2I> pending = new();
	private readonly List<Vector2I> scratch = new();
	private Vector2I centre;
	private bool hasCentre;
	private Vector2I anchorChunk;
	private bool hasAnchor;

	// Timings, per kind of step, since the last border crossing. Only reported under LogTimings.
	private double maxDataMs, maxPaintMs, maxPopulateMs, maxWaterMs, totalMs;
	private int steps;

	public TerrainStreamer(LevelTilePainter painter, StageEnvironmentKind kind, ulong seed, int tilePixels,
		Vector2 origin, string baseTerrain, bool cleanup, float safeRadius, float spawnClearRadius)
	{
		this.painter = painter;
		this.kind = kind;
		this.seed = seed;
		tilePx = tilePixels;
		this.origin = origin;
		this.baseTerrain = baseTerrain;
		this.cleanup = cleanup;
		this.safeRadius = safeRadius;
		this.spawnClearRadius = spawnClearRadius;
		painter.VariantSeed = seed;
	}

	// --- Coordinates ------------------------------------------------------------------------

	private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

	public Vector2I CellOfWorld(Vector2 world) => new(
		Mathf.FloorToInt((world.X - origin.X) / tilePx),
		Mathf.FloorToInt((world.Y - origin.Y) / tilePx));

	/// <summary>The world position of a cell's top-left corner; the inverse of <see cref="CellOfWorld"/>.</summary>
	public Vector2 CellOriginWorld(Vector2I cell) => origin + new Vector2(cell.X, cell.Y) * tilePx;

	public Vector2I ChunkOfWorld(Vector2 world)
	{
		Vector2I cell = CellOfWorld(world);
		return new Vector2I(FloorDiv(cell.X, ChunkTiles), FloorDiv(cell.Y, ChunkTiles));
	}

	private static int Chebyshev(Vector2I a, Vector2I b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

	// --- Streaming --------------------------------------------------------------------------

	/// <summary>
	/// Paint and furnish everything round <paramref name="playerWorld"/> now, in one go. Stage start
	/// only: the run has not begun, and the player should not watch the ground arrive.
	/// </summary>
	public void LoadAllNow(Vector2 playerWorld)
	{
		long start = Stopwatch.GetTimestamp();
		centre = ChunkOfWorld(playerWorld);
		hasCentre = true;
		Replan();
		while (pending.Count > 0)
			Step();
		if (LogTimings)
		{
			GD.Print($"TERRAIN initial load: {PaintedChunkCount} chunks painted, {DataChunkCount} with data, "
				+ $"{Ms(start):0.0} ms total; worst step data {maxDataMs:0.00} / paint {maxPaintMs:0.00} (water {maxWaterMs:0.00}) / "
				+ $"furnish {maxPopulateMs:0.00} ms; nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
		}
		ResetTimings();
	}

	/// <summary>
	/// Per frame. Does nothing unless the player (or the anchor) has crossed into another chunk, or
	/// there is queued work, of which it does one step. <paramref name="anchorWorld"/> keeps the
	/// ground under one more thing loaded - the boss, so a fight never runs onto unpainted ground.
	/// </summary>
	public void Update(Vector2 playerWorld, Vector2? anchorWorld = null)
	{
		Vector2I c = ChunkOfWorld(playerWorld);
		bool anchorNow = anchorWorld.HasValue;
		Vector2I a = anchorNow ? ChunkOfWorld(anchorWorld.Value) : default;
		if (!hasCentre || c != centre || anchorNow != hasAnchor || (anchorNow && a != anchorChunk))
		{
			if (LogTimings && hasCentre && steps > 0)
				ReportCrossing();
			centre = c;
			hasCentre = true;
			hasAnchor = anchorNow;
			anchorChunk = a;
			Replan();
		}

		if (pending.Count == 0)
			return;

		// One step, then more only while the frame's budget lasts - so cheap steps (a chunk whose
		// data already exists, an empty decor set) do not each cost a frame - or while the chunk
		// next in line touches the player's own. The budget is the normal case; the urgent rule is
		// the guarantee: if the streamer has fallen behind - a very fast player, a frame rate on
		// the floor (the harness at 3 fps crossed a chunk in twelve frames) - it catches up on the
		// ground the camera can see at once, rather than letting the player walk off the painted
		// edge. At 60 fps a chunk takes hundreds of frames to cross and this never triggers.
		long start = Stopwatch.GetTimestamp();
		do
		{
			Step();
		}
		while (pending.Count > 0
			&& (Chebyshev(pending[pending.Count - 1], centre) <= 1 || Ms(start) < StepBudgetMs));
	}

	/// <summary>Milliseconds of streaming work a frame may take before the rest waits for the next.</summary>
	public const double StepBudgetMs = 2.0;

	private bool WantedPainted(Vector2I coord) =>
		Chebyshev(coord, centre) <= LoadRadius || (hasAnchor && Chebyshev(coord, anchorChunk) <= 1);

	private bool KeepPainted(Vector2I coord) =>
		Chebyshev(coord, centre) <= UnloadRadius || (hasAnchor && Chebyshev(coord, anchorChunk) <= 1);

	private bool KeepData(Vector2I coord) =>
		Chebyshev(coord, centre) <= DataRadius || (hasAnchor && Chebyshev(coord, anchorChunk) <= 2);

	// On a crossing only: forget what is now too far, and queue what is now wanted.
	private void Replan()
	{
		scratch.Clear();
		foreach (KeyValuePair<Vector2I, TerrainChunk> pair in chunks)
		{
			if (pair.Value.PaintStage > 0 && !KeepPainted(pair.Key))
				Unpaint(pair.Value);
			if (!KeepData(pair.Key))
				scratch.Add(pair.Key);
		}
		foreach (Vector2I gone in scratch)
			chunks.Remove(gone);

		// Outermost ring first, so popping from the end serves the nearest chunk first.
		pending.Clear();
		for (int r = LoadRadius; r >= 0; r--)
		{
			for (int dy = -r; dy <= r; dy++)
			{
				for (int dx = -r; dx <= r; dx++)
				{
					if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
					QueueIfUnfinished(new Vector2I(centre.X + dx, centre.Y + dy));
				}
			}
		}
		if (hasAnchor)
		{
			for (int dy = -1; dy <= 1; dy++)
				for (int dx = -1; dx <= 1; dx++)
				{
					var coord = new Vector2I(anchorChunk.X + dx, anchorChunk.Y + dy);
					if (Chebyshev(coord, centre) <= LoadRadius)
						continue;
					if (chunks.TryGetValue(coord, out TerrainChunk done) && done.Painted && done.Populated)
						continue;
					// At the front, so the player's own square is always served first.
					pending.Insert(0, coord);
				}
		}
	}

	private void QueueIfUnfinished(Vector2I coord)
	{
		if (chunks.TryGetValue(coord, out TerrainChunk existing) && existing.Painted && existing.Populated)
			return;
		pending.Add(coord);
	}

	// One unit of work on the nearest queued chunk.
	private void Step()
	{
		Vector2I coord = pending[pending.Count - 1];
		if (!WantedPainted(coord))
		{
			pending.RemoveAt(pending.Count - 1);
			return;
		}

		long start = Stopwatch.GetTimestamp();
		gcAtStepStart = GC.CollectionCount(0) + GC.CollectionCount(1) * 1000 + GC.CollectionCount(2) * 1000000;
		steps++;

		if (!chunks.TryGetValue(coord, out TerrainChunk chunk))
		{
			GenerateData(coord);
			Record(ref maxDataMs, start);
			return;
		}

		// The ground in two halves, then the overlay and walls: three steps, because 1024 cells of
		// ground and their quadrant rebuild were the single most expensive thing in a chunk.
		if (chunk.PaintStage < GroundPaintSteps)
		{
			int rows = ChunkTiles / GroundPaintSteps;
			painter.PaintChunkGround(coord, chunk.MinCell, ChunkTiles, chunk.PaintStage * rows, rows);
			chunk.PaintStage++;
			Record(ref maxPaintMs, start);
			return;
		}

		if (chunk.PaintStage == GroundPaintSteps)
		{
			painter.PaintChunkTerrain(coord, chunk.MinCell, ChunkTiles, chunk.Terrain, chunk.Roles, chunk.WallNeighbours);
			chunk.PaintStage++;
			Record(ref maxPaintMs, start);
			return;
		}

		if (chunk.PaintStage == GroundPaintSteps + 1)
		{
			// The water's shape and distance field. It reads a window of blocked cells reaching into
			// the neighbours (a shore across a border is one shore), so their data first, one per step.
			if (EnsureNeighbourData(coord, start))
				return;
			bool any = painter.PrepareChunkWater(coord, chunk.MinCell, ChunkTiles, FillWater);
			chunk.PaintStage = any ? GroundPaintSteps + 2 : PaintedStage;
			RecordWater(start);
			return;
		}

		if (chunk.PaintStage == GroundPaintSteps + 2)
		{
			// The water's texture, its node and its collision body.
			painter.FinishChunkWater(coord, chunk.MinCell, ChunkTiles, FillWater);
			chunk.PaintStage = PaintedStage;
			RecordWater(start);
			return;
		}

		if (!chunk.Populated)
		{
			// Furnishing can place a member of a cluster just over the border, and asks the terrain
			// there; have the neighbours' data ready first, one per step.
			if (EnsureNeighbourData(coord, start))
				return;

			chunk.Props ??= new List<Node>();
			bool done = Populate == null || Populate(chunk, chunk.PopulatePart);
			chunk.PopulatePart++;
			Record(ref maxPopulateMs, start);
			if (!done)
				return;
			chunk.Populated = true;
		}

		pending.RemoveAt(pending.Count - 1);
	}

	private void RecordWater(long start)
	{
		double ms = Ms(start);
		if (ms > maxWaterMs) maxWaterMs = ms;
		Record(ref maxPaintMs, start);
	}

	// Generates one missing neighbour's data and returns true (that was this step), or false when
	// all eight are there.
	private bool EnsureNeighbourData(Vector2I coord, long start)
	{
		for (int dy = -1; dy <= 1; dy++)
		{
			for (int dx = -1; dx <= 1; dx++)
			{
				var n = new Vector2I(coord.X + dx, coord.Y + dy);
				if (chunks.ContainsKey(n)) continue;
				GenerateData(n);
				Record(ref maxDataMs, start);
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Fill <paramref name="wet"/> (side x side, indexed x + y * side) with the blocked flag of every
	/// cell from <paramref name="minCell"/>: the window LevelTilePainter.PrepareChunkWater shapes the
	/// shore from. The same flag every other blocking authority reads, so the drawn water can only
	/// ever be inside cells the flow field, placement and props already treat as water.
	/// </summary>
	public void FillWater(bool[] wet, Vector2I minCell, int side)
	{
		int firstChunkX = FloorDiv(minCell.X, ChunkTiles);
		int lastChunkX = FloorDiv(minCell.X + side - 1, ChunkTiles);
		int firstChunkY = FloorDiv(minCell.Y, ChunkTiles);
		int lastChunkY = FloorDiv(minCell.Y + side - 1, ChunkTiles);
		for (int cy = firstChunkY; cy <= lastChunkY; cy++)
		{
			for (int cx = firstChunkX; cx <= lastChunkX; cx++)
			{
				var coord = new Vector2I(cx, cy);
				if (!chunks.TryGetValue(coord, out TerrainChunk chunk))
					chunk = GenerateData(coord);
				int x0 = Math.Max(minCell.X, cx * ChunkTiles);
				int x1 = Math.Min(minCell.X + side, (cx + 1) * ChunkTiles);
				int y0 = Math.Max(minCell.Y, cy * ChunkTiles);
				int y1 = Math.Min(minCell.Y + side, (cy + 1) * ChunkTiles);
				for (int wy = y0; wy < y1; wy++)
				{
					int src = (wy - cy * ChunkTiles) * ChunkTiles - cx * ChunkTiles;
					int dst = (wy - minCell.Y) * side - minCell.X;
					for (int wx = x0; wx < x1; wx++)
						wet[dst + wx] = (chunk.Flags[src + wx] & FlagBlocked) != 0;
				}
			}
		}
	}

	private void Unpaint(TerrainChunk chunk)
	{
		painter.ClearChunk(chunk.Coord);
		chunk.PaintStage = 0;
		chunk.PopulatePart = 0;
		if (chunk.Props != null)
		{
			foreach (Node node in chunk.Props)
				if (GodotObject.IsInstanceValid(node))
					node.QueueFree();
		}
		chunk.Props = null;
		chunk.Populated = false;
	}

	private TerrainChunk GenerateData(Vector2I coord)
	{
		// Into buffers reused for every chunk: only the arrays the chunk keeps are allocated.
		string[,] grid = gridBuffer;
		TileResolution[,] res = resolveBuffer;
		generator.GenerateChunkInto(grid, kind, seed, coord.X, coord.Y, ChunkTiles, Margin, safeRadius, spawnClearRadius);
		autotiler.ResolveInPlace(grid, baseTerrain, cleanup, cleanupBuffer, res);

		const int n = ChunkTiles * ChunkTiles;
		var terrain = new string[n];
		var roles = new AutotileRole[n];
		var flags = new byte[n];
		byte[] wallNeighbours = null;
		bool IsWall(int px, int py) => res[px, py].Terrain == LevelGenerator.WallTerrain;
		for (int y = 0; y < ChunkTiles; y++)
		{
			for (int x = 0; x < ChunkTiles; x++)
			{
				TileResolution r = res[x + Margin, y + Margin];
				int i = x + y * ChunkTiles;
				if (r.Terrain == baseTerrain)
				{
					// Which walls throw a shadow on this floor cell (the key is upper-left, so the ones
					// north and west of it). From the padded grid, so across the chunk border too.
					byte shade = 0;
					if (IsWall(x + Margin, y + Margin - 1)) shade |= WallShadeN;
					if (IsWall(x + Margin - 1, y + Margin)) shade |= WallShadeW;
					if (IsWall(x + Margin - 1, y + Margin - 1)) shade |= WallShadeNW;
					if (shade != 0)
						(wallNeighbours ??= new byte[n])[i] = shade;
					continue;
				}
				if (r.Terrain == LevelGenerator.WallTerrain)
				{
					byte open = 0;
					if (!IsWall(x + Margin, y + Margin - 1)) open |= WallOpenN;
					if (!IsWall(x + Margin, y + Margin + 1)) open |= WallOpenS;
					if (!IsWall(x + Margin - 1, y + Margin)) open |= WallOpenW;
					if (!IsWall(x + Margin + 1, y + Margin)) open |= WallOpenE;
					if (open != 0)
						(wallNeighbours ??= new byte[n])[i] = open;
				}
				terrain[i] = r.Terrain;
				roles[i] = r.Role;
				byte f = 0;
				if (r.Terrain == LevelGenerator.WallTerrain) f |= FlagWall;
				else
				{
					if (painter.IsHazardTerrainId(r.Terrain)) f |= FlagHazard;
					if (painter.IsBlockingTerrainId(r.Terrain)) f |= FlagBlocked;
				}
				flags[i] = f;
			}
		}

		var chunk = new TerrainChunk
		{
			Coord = coord,
			MinCell = new Vector2I(coord.X * ChunkTiles, coord.Y * ChunkTiles),
			Terrain = terrain,
			Roles = roles,
			Flags = flags,
			WallNeighbours = wallNeighbours,
		};
		chunks[coord] = chunk;
		PlanFeatures?.Invoke(chunk);
		return chunk;
	}

	private TerrainChunk ChunkForCell(int cx, int cy, out int index)
	{
		var coord = new Vector2I(FloorDiv(cx, ChunkTiles), FloorDiv(cy, ChunkTiles));
		if (!chunks.TryGetValue(coord, out TerrainChunk chunk))
			chunk = GenerateData(coord);
		index = (cx - coord.X * ChunkTiles) + (cy - coord.Y * ChunkTiles) * ChunkTiles;
		return chunk;
	}

	// --- Queries ----------------------------------------------------------------------------

	/// <summary>True if this world position cannot be walked into (water). Walls are separate: <see cref="IsWallAtWorld"/>.</summary>
	public bool IsBlockedAtWorld(Vector2 world) => (FlagsAtWorld(world) & FlagBlocked) != 0;

	/// <summary>True if standing here hurts (lava, pit).</summary>
	public bool IsHazardAtWorld(Vector2 world) => (FlagsAtWorld(world) & FlagHazard) != 0;

	/// <summary>True if this world position is a maze wall.</summary>
	public bool IsWallAtWorld(Vector2 world) => (FlagsAtWorld(world) & FlagWall) != 0;

	/// <summary>True if a landmark's fence stands on this world position's cell.</summary>
	public bool IsFenceAtWorld(Vector2 world) => (FlagsAtWorld(world) & FlagFence) != 0;

	/// <summary>
	/// True if a body cannot walk into this world cell: a wall, blocking terrain or a landmark's fence -
	/// what the navigation grid calls solid. By cell, for a flood fill (Node2DGame.CanWalkAwayFrom).
	/// </summary>
	public bool IsSolidCell(int cx, int cy)
	{
		TerrainChunk chunk = ChunkForCell(cx, cy, out int i);
		return (chunk.Flags[i] & (FlagWall | FlagBlocked | FlagFence)) != 0;
	}

	/// <summary>The terrain id of a world cell, or null for open floor. Generates its chunk's data if need be.</summary>
	public string TerrainAtCell(int cx, int cy)
	{
		TerrainChunk chunk = ChunkForCell(cx, cy, out int i);
		return chunk.Terrain[i];
	}

	/// <summary>The data of one chunk, generated now if nobody has asked for it yet.</summary>
	public TerrainChunk DataFor(Vector2I coord) =>
		chunks.TryGetValue(coord, out TerrainChunk chunk) ? chunk : GenerateData(coord);

	/// <summary>The terrain id painted here, or null for open floor.</summary>
	public string TerrainAtWorld(Vector2 world)
	{
		Vector2I cell = CellOfWorld(world);
		TerrainChunk chunk = ChunkForCell(cell.X, cell.Y, out int i);
		return chunk.Terrain[i];
	}

	private byte FlagsAtWorld(Vector2 world)
	{
		Vector2I cell = CellOfWorld(world);
		TerrainChunk chunk = ChunkForCell(cell.X, cell.Y, out int i);
		return chunk.Flags[i];
	}

	/// <summary>
	/// Fill the navigation grid for the window whose first cell is <paramref name="minCell"/>, both
	/// indexed x + y * width: <paramref name="solid"/> is every cell a body cannot walk into (maze wall,
	/// blocking terrain - water - or a landmark's fence), <paramref name="wall"/> the walls alone, which
	/// also stop a shot (a bolt flies over a lake and over a garden fence).
	/// MazeNavigation's grid, from the same chunks the walls and the water's bodies are painted from.
	/// </summary>
	/// <remarks>
	/// A whole cell for every blocking cell, shoreline roles included, although the painted body of an
	/// edge or corner covers only part of it (WaterShape: the drawn water never leaves its cells). Routing a body a
	/// little wide of a shore costs nothing; routing it into the half of a cell that is water is the
	/// wedge this grid exists to prevent.
	/// </remarks>
	public void FillNavigation(bool[] solid, bool[] wall, Vector2I minCell, int width, int height)
	{
		// A chunk at a time, so it is one dictionary lookup per chunk rather than per cell.
		int firstChunkX = FloorDiv(minCell.X, ChunkTiles);
		int lastChunkX = FloorDiv(minCell.X + width - 1, ChunkTiles);
		int firstChunkY = FloorDiv(minCell.Y, ChunkTiles);
		int lastChunkY = FloorDiv(minCell.Y + height - 1, ChunkTiles);
		for (int cy = firstChunkY; cy <= lastChunkY; cy++)
		{
			for (int cx = firstChunkX; cx <= lastChunkX; cx++)
			{
				var coord = new Vector2I(cx, cy);
				if (!chunks.TryGetValue(coord, out TerrainChunk chunk))
					chunk = GenerateData(coord);
				int x0 = Math.Max(minCell.X, cx * ChunkTiles);
				int x1 = Math.Min(minCell.X + width, (cx + 1) * ChunkTiles);
				int y0 = Math.Max(minCell.Y, cy * ChunkTiles);
				int y1 = Math.Min(minCell.Y + height, (cy + 1) * ChunkTiles);
				for (int wy = y0; wy < y1; wy++)
				{
					int src = (wy - cy * ChunkTiles) * ChunkTiles - cx * ChunkTiles;
					int dst = (wy - minCell.Y) * width - minCell.X;
					for (int wx = x0; wx < x1; wx++)
					{
						byte f = chunk.Flags[src + wx];
						solid[dst + wx] = (f & (FlagWall | FlagBlocked | FlagFence)) != 0;
						wall[dst + wx] = (f & FlagWall) != 0;
					}
				}
			}
		}
	}

	// --- Timing -----------------------------------------------------------------------------

	private static double Ms(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

	private int gcAtStepStart;

	private void Record(ref double max, long start)
	{
		double ms = Ms(start);
		totalMs += ms;
		if (ms > max) max = ms;
		// A step far over its usual few ms is reported on its own, with whether the .NET collector
		// ran inside it - the difference between "this step is too big" and "a pause landed in it".
		if (LogTimings && ms > 20.0)
		{
			int gcNow = GC.CollectionCount(0) + GC.CollectionCount(1) * 1000 + GC.CollectionCount(2) * 1000000;
			GD.Print($"TERRAIN slow step {ms:0.0} ms; collections during it (gen0 + 1000 gen1 + 1e6 gen2): {gcNow - gcAtStepStart}");
		}
	}

	private void ReportCrossing()
	{
		GD.Print($"TERRAIN centre {centre}: {steps} steps, {totalMs:0.0} ms total; worst step data {maxDataMs:0.00} / "
			+ $"paint {maxPaintMs:0.00} (water {maxWaterMs:0.00}) / furnish {maxPopulateMs:0.00} ms; painted {PaintedChunkCount}, data {DataChunkCount}, "
			+ $"nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
		ResetTimings();
	}

	private void ResetTimings()
	{
		maxDataMs = maxPaintMs = maxPopulateMs = maxWaterMs = totalMs = 0;
		steps = 0;
	}
}
