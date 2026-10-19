using System.Collections.Generic;
using Godot;
using WizardSurvivors.scripts;

/// <summary>
/// Example Godot integration for the curated autotile pipeline. Consumes a logical
/// terrain grid (<c>string[,]</c> of terrain ids with a base terrain), resolves each
/// cell to an autotile role via <see cref="TerrainAutotiler"/>, and paints two stacked
/// <see cref="TileMapLayer"/>s: a ground layer (random base variants) and an overlay
/// layer (the resolved terrain tiles, which are transparent at edges so the ground
/// shows through).
///
/// A TileSet is built at runtime from the curated PNG frames — no hand-authored
/// TileSet resource required. Call <see cref="Paint"/> from your level generator, or
/// leave <see cref="PaintDemoOnReady"/> on to see a self-contained demo.
/// </summary>
public partial class LevelTilePainter : Node2D
{
	[Export] public string MetadataPath { get; set; } =
		"res://assets/bonelight/tiles/tiles.json";

	/// <summary>
	/// Extra manifests merged on top of <see cref="MetadataPath"/>. Ours rather than a pack's.
	/// </summary>
	[Export] public string[] ExtraMetadataPaths { get; set; } =
	{
		"res://assets/kidsart/tiles.json",
	};

	[Export] public int TileSize { get; set; } = 48;

	private string[] BuildMetadataPaths()
	{
		var paths = new System.Collections.Generic.List<string> { MetadataPath };
		if (ExtraMetadataPaths != null)
			paths.AddRange(ExtraMetadataPaths);
		return paths.ToArray();
	}

	/// <summary>Logical id used for base cells in the grid (skipped on the overlay layer).</summary>
	[Export] public string BaseTerrain { get; set; } = "ground";

	/// <summary>materialFamily of the plain ground variants painted on the base layer.</summary>
	[Export] public string GroundMaterial { get; set; } = "sand";

	/// <summary>
	/// Optional autotile terrain (e.g. "grass") whose fill tiles are used as the solid base
	/// ground layer. When set and available, it takes precedence over <see cref="GroundMaterial"/>.
	/// Only near-opaque fills are used so the base has no holes.
	/// </summary>
	[Export] public string GroundTerrain { get; set; } = "";

	/// <summary>materialFamily of the opaque tiles used for maze walls (collision blocks).</summary>
	/// <remarks>
	/// "void" is a flat `occ` tile from tools/art/tiles.py: a wall is drawn as the absence of floor.
	/// This used to name "darkbrick", which no manifest has defined since the bought tile pack went,
	/// and GroundVariants quietly fell back to the first material it had - sand - so the Cursed
	/// Dungeon's walls were sand tinted grey-blue, a value step away from its floor. EnsureWallSources
	/// now refuses that fallback out loud.
	/// </remarks>
	[Export] public string WallMaterial { get; set; } = "void";

	/// <summary>Tint applied to wall tiles. White: the void tile is already the contract's darkest
	/// tone, and anything else would lift it off black or tint it away from occ.</summary>
	[Export] public Color WallModulate { get; set; } = Colors.White;

	[Export] public bool PaintDemoOnReady { get; set; } = true;
	[Export] public Vector2I DemoSize { get; set; } = new Vector2I(24, 15);
	[Export] public ulong DemoSeed { get; set; } = 12345;

	private CuratedTileCatalog _catalog;
	private TileSet _tileSet;
	private TileMapLayer _groundLayer;
	private TileMapLayer _overlayLayer;
	private TileMapLayer _wallLayer;
	private int _wallPhysicsLayer = -1;
	private readonly List<int> _wallSources = new();
	private readonly Dictionary<string, int> _sourceIds = new();
	private readonly List<int> _groundSources = new();
	private readonly HashSet<Vector2I> _hazardCells = new();

	/// <summary>Cells the player cannot walk into. See IsBlockedAtWorld.</summary>
	private readonly HashSet<Vector2I> _blockedCells = new();
	private readonly Dictionary<Vector2I, string> _cellTerrain = new();
	private readonly Dictionary<string, bool> _hazardTerrainCache = new();
	private readonly TerrainAutotiler _autotiler = new();
	private readonly RandomNumberGenerator _rng = new();

	private const string WallTerrainId = "wall";

	/// <summary>
	/// Cell offset applied when painting, so a caller can centre the grid on the world
	/// origin (e.g. set to (-width/2, -height/2)). Demo painting leaves this at zero.
	/// </summary>
	public Vector2I PaintCellOffset { get; set; } = Vector2I.Zero;

	public override void _Ready()
	{
		_catalog = CuratedTileCatalog.LoadMany(BuildMetadataPaths());

		_tileSet = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };

		// Physics layer used only by wall tiles (collision layer 1, matching decor props/walls).
		_tileSet.AddPhysicsLayer();
		_wallPhysicsLayer = _tileSet.GetPhysicsLayersCount() - 1;
		_tileSet.SetPhysicsLayerCollisionLayer(_wallPhysicsLayer, 1);
		_tileSet.SetPhysicsLayerCollisionMask(_wallPhysicsLayer, 0);

		_groundLayer = new TileMapLayer { Name = "Ground", TileSet = _tileSet, CollisionEnabled = false };
		// Collision ON for the overlay, but only BLOCKING terrains are given a polygon - a tile
		// without one contributes no body, so every other terrain is unaffected. This is what
		// stops the player walking into water without needing a fourth TileMapLayer.
		_overlayLayer = new TileMapLayer { Name = "Overlay", TileSet = _tileSet, CollisionEnabled = true };
		_wallLayer = new TileMapLayer { Name = "Walls", TileSet = _tileSet, Modulate = WallModulate };
		AddChild(_groundLayer);
		AddChild(_overlayLayer);
		AddChild(_wallLayer);

		if (PaintDemoOnReady)
		{
			_rng.Seed = DemoSeed;
			string[,] grid = BuildDemoGrid(DemoSize.X, DemoSize.Y);
			Paint(grid, BaseTerrain);
			AddDemoCamera();
		}
	}

	/// <summary>
	/// Resolve <paramref name="grid"/> and paint the layers. The grid is indexed [x, y];
	/// each cell is a terrain id, with <paramref name="baseTerrain"/> for empty ground.
	/// Set <paramref name="cleanupGrid"/> false for mazes so corridor dead-ends are preserved.
	/// </summary>
	public void Paint(string[,] grid, string baseTerrain, bool cleanupGrid = true)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);

		TileResolution[,] res = _autotiler.Resolve(grid, baseTerrain, cleanup: cleanupGrid);
		EnsureSources(res, baseTerrain);

		_groundLayer.Clear();
		_overlayLayer.Clear();
		_wallLayer.Clear();
		_wallLayer.Modulate = WallModulate;
		_hazardCells.Clear();
		_blockedCells.Clear();
		_cellTerrain.Clear();

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				var coord = new Vector2I(x, y) + PaintCellOffset;

				if (_groundSources.Count > 0)
				{
					int gs = _groundSources[_rng.RandiRange(0, _groundSources.Count - 1)];
					_groundLayer.SetCell(coord, gs, Vector2I.Zero);
				}

				TileResolution r = res[x, y];
				if (r.Terrain == baseTerrain) continue;

				// Walls are painted as solid collision blocks on the wall layer (uniform,
				// top-down readable), not autotiled like organic terrains.
				if (r.Terrain == WallTerrainId)
				{
					if (_wallSources.Count > 0)
					{
						int ws = _wallSources[_rng.RandiRange(0, _wallSources.Count - 1)];
						_wallLayer.SetCell(coord, ws, Vector2I.Zero);
					}
					_cellTerrain[coord] = r.Terrain;
					continue;
				}

				string key = TileKey(r.Terrain, TerrainAutotiler.RoleKey(r.Role));
				if (_sourceIds.TryGetValue(key, out int src) && src >= 0)
					_overlayLayer.SetCell(coord, src, Vector2I.Zero);

				_cellTerrain[coord] = r.Terrain;
				if (IsHazardTerrain(r.Terrain))
					_hazardCells.Add(coord);
				if (IsBlockingTerrain(r.Terrain))
					_blockedCells.Add(coord);
			}
		}
	}

	// --- Streamed chunks (TerrainStreamer) --------------------------------------------------
	//
	// The infinite stages paint a chunk at a time into layers of its own, under one Node2D per
	// chunk, so forgetting a chunk is one QueueFree and the layer count never grows with distance
	// walked. Every chunk shares the one TileSet, so a terrain's sources (and the collision polygon
	// on a blocking one) are still built once, the first time any chunk needs them.

	private readonly Dictionary<Vector2I, Node2D> _chunkNodes = new();

	/// <summary>Seed for the per-tile variant choice. Same seed, same tile, same variant.</summary>
	public ulong VariantSeed { get; set; }

	/// <summary>Number of chunks currently painted.</summary>
	public int PaintedChunkCount => _chunkNodes.Count;

	/// <summary>
	/// Paint the base floor of one chunk of <paramref name="size"/> x <paramref name="size"/> cells
	/// whose first cell is world cell <paramref name="minCell"/>. The first of two steps; the second is
	/// <see cref="PaintChunkTerrain"/>. Split because a chunk is painted while the game is running, and
	/// one step per frame is the budget (TerrainStreamer).
	/// </summary>
	/// <remarks>
	/// The base variant under each cell and a wall's variant come from a hash of the cell rather than
	/// from <see cref="_rng"/>: a chunk forgotten and painted again when the player walks back must
	/// come out the same, and the RNG's position would depend on everything painted in between.
	/// </remarks>
	/// <param name="firstRow">First row of the chunk to paint; with <paramref name="rows"/>, lets the
	/// streamer paint the ground in halves, one per frame. Row 0 creates the chunk's node.</param>
	public void PaintChunkGround(Vector2I chunk, Vector2I minCell, int size, int firstRow, int rows)
	{
		TileMapLayer ground;
		if (firstRow == 0)
		{
			ClearChunk(chunk);
			EnsureGroundSources();
			var root = new Node2D { Name = $"Chunk_{chunk.X}_{chunk.Y}" };
			ground = new TileMapLayer { Name = "Ground", TileSet = _tileSet, CollisionEnabled = false };
			root.AddChild(ground);
			AddChild(root);
			_chunkNodes[chunk] = root;
		}
		else if (_chunkNodes.TryGetValue(chunk, out Node2D existing))
		{
			ground = existing.GetChild<TileMapLayer>(0);
		}
		else
		{
			return;
		}

		int variants = _groundSources.Count;
		if (variants > 0)
		{
			for (int y = firstRow; y < Mathf.Min(size, firstRow + rows); y++)
			{
				for (int x = 0; x < size; x++)
				{
					var coord = new Vector2I(minCell.X + x, minCell.Y + y);
					ulong h = LevelGenerator.Hash(VariantSeed, coord.X, coord.Y, 0x6A11);
					ground.SetCell(coord, _groundSources[(int)(h % (ulong)variants)], Vector2I.Zero);
				}
			}
		}

		// Pay for the quadrant rebuild here, inside the step TerrainStreamer times, rather than in the
		// engine's deferred update at the end of the frame where no stopwatch sees it.
		ground.UpdateInternals();
	}

	/// <summary>
	/// Paint the overlay terrain and the maze walls of a chunk whose ground is already painted.
	/// <paramref name="terrain"/> and <paramref name="roles"/> are indexed x + y * size; null is open floor.
	/// </summary>
	public void PaintChunkTerrain(Vector2I chunk, Vector2I minCell, int size, string[] terrain, AutotileRole[] roles)
	{
		if (!_chunkNodes.TryGetValue(chunk, out Node2D root))
			return;

		TileMapLayer overlay = null;
		TileMapLayer walls = null;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				string t = terrain[x + y * size];
				if (t == null) continue;
				var coord = new Vector2I(minCell.X + x, minCell.Y + y);

				if (t == WallTerrainId)
				{
					if (_wallSources.Count == 0 && !_wallSourcesTried)
						EnsureWallSources();
					if (_wallSources.Count == 0) continue;
					if (walls == null)
					{
						walls = new TileMapLayer { Name = "Walls", TileSet = _tileSet, Modulate = WallModulate };
						root.AddChild(walls);
					}
					ulong h = LevelGenerator.Hash(VariantSeed, coord.X, coord.Y, 0x3A11);
					walls.SetCell(coord, _wallSources[(int)(h % (ulong)_wallSources.Count)], Vector2I.Zero);
					continue;
				}

				int[] variants = EnsureTerrainSources(t, roles[x + y * size]);
				if (variants.Length == 0) continue;
				// Which variant of this role, from a hash of the cell: two to six of each are drawn
				// (tools/art/tiles.py) so a long shore is not one wiggle repeated every tile, and a
				// hash rather than an RNG so a chunk painted again comes out the same.
				int src = variants.Length == 1
					? variants[0]
					: variants[(int)(LevelGenerator.Hash(VariantSeed, coord.X, coord.Y, 0x7E2A) % (ulong)variants.Length)];
				if (overlay == null)
				{
					// See _Ready: collision on, but only blocking terrains carry a polygon. Created only
					// when a chunk has any overlay at all - a page of the Sketchbook has none.
					overlay = new TileMapLayer { Name = "Overlay", TileSet = _tileSet, CollisionEnabled = true };
					root.AddChild(overlay);
					if (walls != null)
						root.MoveChild(overlay, 1);
				}
				overlay.SetCell(coord, src, Vector2I.Zero);
			}
		}

		overlay?.UpdateInternals();
		walls?.UpdateInternals();
	}

	/// <summary>
	/// Build every tile source the given terrains can need, now. Called at stage start: building a
	/// source loads a texture and, for water, attaches a collision polygon, and doing that the first
	/// time some far chunk happens to need an inner corner was a 40 ms frame.
	/// </summary>
	public void PrewarmTerrainSources(IEnumerable<string> terrains)
	{
		EnsureGroundSources();
		foreach (string t in terrains)
		{
			if (t == WallTerrainId)
			{
				if (_wallSources.Count == 0 && !_wallSourcesTried)
					EnsureWallSources();
				continue;
			}
			foreach (AutotileRole role in System.Enum.GetValues<AutotileRole>())
				EnsureTerrainSources(t, role);
		}
	}

	// --- Ground detail (flowers, lilypads, reeds, pebbles...) ----------------------------------
	//
	// One more TileMapLayer per chunk, over the overlay, holding the small things lying on the
	// ground. A layer and not sprites because there are a lot of them: decor is three nodes an item,
	// and a hundred flowers a screen streamed in and out with the chunks would undo the flat node
	// count .ai/terrain.md section 7 measured. A TileMapLayer draws its cells in batched quadrants
	// for one node a chunk. Which set goes on which cell is GroundDetail's call; this only paints.

	/// <summary>The detail manifest written by tools/art/ground_detail.py.</summary>
	[Export] public string DetailMetadataPath { get; set; } = "res://assets/bonelight/tiles/detail/detail.json";

	private Dictionary<string, List<string>> _detailPaths;
	private readonly Dictionary<string, int[]> _detailSources = new();

	/// <summary>Detail set name to the res:// paths of its variants, read once from the manifest.</summary>
	public static Dictionary<string, List<string>> LoadDetailManifest(string path)
	{
		var sets = new Dictionary<string, List<string>>();
		string text = Godot.FileAccess.GetFileAsString(path);
		if (string.IsNullOrEmpty(text))
			return sets;
		using var doc = System.Text.Json.JsonDocument.Parse(text);
		if (!doc.RootElement.TryGetProperty("frames", out var frames))
			return sets;
		foreach (var f in frames.EnumerateArray())
		{
			string set = f.TryGetProperty("set", out var se) ? se.GetString() : null;
			string p = f.TryGetProperty("path", out var pe) ? pe.GetString() : null;
			if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(p)) continue;
			if (!sets.TryGetValue(set, out var list))
				sets[set] = list = new List<string>();
			list.Add(p.StartsWith("res://") ? p : "res://" + p);
		}
		return sets;
	}

	/// <summary>Build the sources for these detail sets now (stage start), like PrewarmTerrainSources.</summary>
	public void PrewarmDetailSources(IEnumerable<string> sets)
	{
		_detailPaths ??= LoadDetailManifest(DetailMetadataPath);
		foreach (string set in sets)
			EnsureDetailSources(set);
	}

	private int[] EnsureDetailSources(string set)
	{
		if (_detailSources.TryGetValue(set, out int[] known)) return known;
		_detailPaths ??= LoadDetailManifest(DetailMetadataPath);
		var ids = new List<int>();
		if (_detailPaths.TryGetValue(set, out List<string> paths))
		{
			foreach (string p in paths)
			{
				int id = AddTileSource(p);
				if (id >= 0) ids.Add(id);
			}
		}
		else
		{
			GD.PushWarning($"LevelTilePainter: ground detail set '{set}' is not in {DetailMetadataPath}. "
				+ "Re-run: python tools/art/ground_detail.py");
		}
		int[] result = ids.ToArray();
		_detailSources[set] = result;
		return result;
	}

	/// <summary>
	/// Paint a chunk's ground detail. <paramref name="setPerCell"/> is indexed x + y * size, null for
	/// nothing. The variant of each set comes from a hash of the cell, so a repaint is identical.
	/// </summary>
	public void PaintChunkDetail(Vector2I chunk, Vector2I minCell, int size, string[] setPerCell)
	{
		if (!_chunkNodes.TryGetValue(chunk, out Node2D root))
			return;

		TileMapLayer detail = null;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				string set = setPerCell[x + y * size];
				if (set == null) continue;
				int[] variants = EnsureDetailSources(set);
				if (variants.Length == 0) continue;
				var coord = new Vector2I(minCell.X + x, minCell.Y + y);
				ulong h = LevelGenerator.Hash(VariantSeed, coord.X, coord.Y, 0x0D7A);
				if (detail == null)
				{
					detail = new TileMapLayer { Name = "Detail", TileSet = _tileSet, CollisionEnabled = false };
					root.AddChild(detail);
				}
				detail.SetCell(coord, variants[(int)(h % (ulong)variants.Length)], Vector2I.Zero);
			}
		}
		detail?.UpdateInternals();
	}

	/// <summary>Forget a painted chunk. Harmless if it was never painted.</summary>
	public void ClearChunk(Vector2I chunk)
	{
		if (_chunkNodes.Remove(chunk, out Node2D root) && IsInstanceValid(root))
			root.QueueFree();
	}

	/// <summary>True if the manifest marks this terrain as damaging to stand in (lava, pit).</summary>
	public bool IsHazardTerrainId(string terrain) => terrain != null && IsHazardTerrain(terrain);

	/// <summary>True if the manifest marks this terrain as impassable (water).</summary>
	public bool IsBlockingTerrainId(string terrain) => terrain != null && IsBlockingTerrain(terrain);

	// Every variant's source per (terrain, role), without building a string key per cell. Empty
	// (never null) when the manifest has no such tile, so a miss is remembered too.
	private readonly Dictionary<(string, AutotileRole), int[]> _sourcesByRole = new();

	private int[] EnsureTerrainSources(string terrain, AutotileRole role)
	{
		if (_sourcesByRole.TryGetValue((terrain, role), out int[] known)) return known;
		string roleKey = TerrainAutotiler.RoleKey(role);
		var ids = new List<int>();
		foreach (CuratedTileEntry e in _catalog.TileVariants(terrain, roleKey))
		{
			int id = AddTileSource(e.ResPath);
			if (id < 0) continue;
			// Every variant of a blocking role carries the same body: they differ in where the
			// shoreline wanders, by a pixel or two, not in where the water is.
			if (e.Blocking)
				AttachBlockingBody(id, e.Role == roleKey ? roleKey : "fill");
			ids.Add(id);
		}
		int[] result = ids.ToArray();
		_sourcesByRole[(terrain, role)] = result;
		return result;
	}

	// The source for one (terrain, role), built on first use; -1 if the manifest has no such tile.
	private int EnsureTerrainSource(string terrain, string role)
	{
		string key = TileKey(terrain, role);
		if (_sourceIds.TryGetValue(key, out int existing)) return existing;
		if (!_catalog.TryGetTile(terrain, role, out CuratedTileEntry e))
		{
			// Remember the miss, or every chunk would ask the catalog again for every such cell.
			_sourceIds[key] = -1;
			return -1;
		}

		int id = AddTileSource(e.ResPath);
		_sourceIds[key] = id;
		if (id >= 0 && e.Blocking)
			AttachBlockingBody(id, role);
		return id;
	}

	private bool IsHazardTerrain(string terrain)
	{
		if (_hazardTerrainCache.TryGetValue(terrain, out bool cached)) return cached;
		bool hazard = _catalog != null && _catalog.IsHazardTerrain(terrain);
		_hazardTerrainCache[terrain] = hazard;
		return hazard;
	}

	private bool IsBlockingTerrain(string terrain)
	{
		if (_blockingTerrainCache.TryGetValue(terrain, out bool cached)) return cached;
		bool blocking = _catalog != null && _catalog.IsBlockingTerrain(terrain);
		_blockingTerrainCache[terrain] = blocking;
		return blocking;
	}

	private readonly Dictionary<string, bool> _blockingTerrainCache = new();

	/// <summary>
	/// The collision body for one role of a blocking terrain, as a fraction of the cell.
	/// </summary>
	/// <remarks>
	/// NOT a full cell for every role, and that is the whole point. An edge tile is half water and
	/// half shoreline, and a corner is mostly shore - giving those a full body would stop the
	/// player a tile short of the water on ground that plainly looks walkable, which is the most
	/// annoying kind of invisible wall. Each role is blocked over roughly the part of the cell the
	/// art actually fills with water.
	/// </remarks>
	private Vector2[] BlockingPolygon(string role)
	{
		float h = TileSize / 2f;

		// Inset, because the art fades out over the last few pixels. Colliding on the painted edge
		// would put the barrier where the water is already transparent.
		const float Shore = 0.18f;
		float s = TileSize * Shore;

		static Vector2[] Box(float x0, float y0, float x1, float y1) =>
			new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };

		// THE CORNERS ARE ROUND because the art is (tools/art/tiles.py, "shore" profile: boundary 8 px
		// in, corner radius 22). A square body under a rounded shoreline left a triangle of grass in
		// every corner that the player could not step on. Mirrored from the top-left case; a mirror
		// in one axis reverses the winding, so those are reversed back.
		const float Radius = 22f;
		Vector2[] Corner(float sx, float sy)
		{
			var pts = new List<Vector2> { new(h, h), new(-h + s, h) };
			float c = -h + s + Radius;
			for (int k = 0; k <= 4; k++)
			{
				float a = Mathf.Pi + k * (Mathf.Pi / 2f) / 4f;
				pts.Add(new Vector2(c + Mathf.Cos(a) * Radius, c + Mathf.Sin(a) * Radius));
			}
			pts.Add(new Vector2(h, -h + s));
			for (int i = 0; i < pts.Count; i++)
				pts[i] = new Vector2(pts[i].X * sx, pts[i].Y * sy);
			if (sx * sy < 0)
				pts.Reverse();
			return pts.ToArray();
		}

		// An inner corner is water everywhere but a quarter-disc of ground of radius s round one
		// corner, where the two shorelines either side of it meet.
		Vector2[] Notch(float sx, float sy)
		{
			var pts = new List<Vector2>
			{
				new(-h + s, -h), new(h, -h), new(h, h), new(-h, h), new(-h, -h + s),
				new(-h + s * 0.71f, -h + s * 0.71f),
			};
			for (int i = 0; i < pts.Count; i++)
				pts[i] = new Vector2(pts[i].X * sx, pts[i].Y * sy);
			if (sx * sy < 0)
				pts.Reverse();
			return pts.ToArray();
		}

		// A patch is a lone cell of water with ground on all four sides: round, inside the inset.
		static Vector2[] Disc(float r)
		{
			var pts = new Vector2[12];
			for (int k = 0; k < pts.Length; k++)
			{
				float a = k * Mathf.Tau / pts.Length;
				pts[k] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
			}
			return pts;
		}

		return role switch
		{
			"edge_top" => Box(-h, -h + s, h, h),
			"edge_bottom" => Box(-h, -h, h, h - s),
			"edge_left" => Box(-h + s, -h, h, h),
			"edge_right" => Box(-h, -h, h - s, h),
			"corner_tl" => Corner(1, 1),
			"corner_tr" => Corner(-1, 1),
			"corner_bl" => Corner(1, -1),
			"corner_br" => Corner(-1, -1),
			"inner_corner_tl" => Notch(1, 1),
			"inner_corner_tr" => Notch(-1, 1),
			"inner_corner_bl" => Notch(1, -1),
			"inner_corner_br" => Notch(-1, -1),
			"patch" => Disc(h - s),
			// fill is water across the whole cell.
			_ => Box(-h, -h, h, h),
		};
	}

	/// <summary>
	/// True if the painted tile at this world position cannot be walked into (water).
	/// </summary>
	/// <remarks>
	/// The physics body already stops the player. This is for everything that places things
	/// WITHOUT moving into them - enemy spawns, prop scatter, discovery sites - which would
	/// otherwise drop them in the middle of a lake and leave them stuck against their own wall.
	/// </remarks>
	public bool IsBlockedAtWorld(Vector2 worldPos)
	{
		if (_overlayLayer == null || _blockedCells.Count == 0) return false;
		Vector2I cell = _overlayLayer.LocalToMap(_overlayLayer.ToLocal(worldPos));
		return _blockedCells.Contains(cell);
	}

	/// <summary>True if the painted tile at this world position is a hazard (lava/pit).</summary>
	public bool IsHazardAtWorld(Vector2 worldPos)
	{
		if (_overlayLayer == null || _hazardCells.Count == 0) return false;
		Vector2I cell = _overlayLayer.LocalToMap(_overlayLayer.ToLocal(worldPos));
		return _hazardCells.Contains(cell);
	}

	/// <summary>
	/// The overlay terrain painted at this world position, or null for open base floor.
	/// Useful for scattering props only on solid/walkable ground.
	/// </summary>
	public string TerrainAtWorld(Vector2 worldPos)
	{
		if (_overlayLayer == null || _cellTerrain.Count == 0) return null;
		Vector2I cell = _overlayLayer.LocalToMap(_overlayLayer.ToLocal(worldPos));
		return _cellTerrain.TryGetValue(cell, out string terrain) ? terrain : null;
	}

	private static string TileKey(string terrain, string role) => terrain + ":" + role;

	private void EnsureSources(TileResolution[,] res, string baseTerrain)
	{
		EnsureGroundSources();

		int w = res.GetLength(0);
		int h = res.GetLength(1);
		bool needWalls = false;
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				TileResolution r = res[x, y];
				if (r.Terrain == baseTerrain) continue;

				if (r.Terrain == WallTerrainId)
				{
					needWalls = true;
					continue;
				}

				// A blocking terrain gets a body shaped to the role it is painting. Attached to
				// the SOURCE, once, rather than to each placed cell - every cell of the same
				// (terrain, role) shares one source. See EnsureTerrainSource.
				EnsureTerrainSource(r.Terrain, TerrainAutotiler.RoleKey(r.Role));
			}
		}

		if (needWalls && _wallSources.Count == 0)
			EnsureWallSources();
	}

	private void EnsureGroundSources()
	{
		if (_groundSources.Count == 0)
		{
			// Prefer an autotile terrain's opaque fill tiles as the base (e.g. grass floor);
			// otherwise use the plain ground-material variants.
			var terrainFills = _catalog.FillTilesFor(GroundTerrain);
			if (terrainFills.Count > 0)
			{
				foreach (CuratedTileEntry fill in terrainFills)
				{
					if (!IsNearOpaque(fill.ResPath)) continue;
					int id = AddTileSource(fill.ResPath);
					if (id >= 0) _groundSources.Add(id);
				}
			}

			if (_groundSources.Count == 0)
			{
				foreach (CuratedTileEntry g in _catalog.GroundVariants(GroundMaterial))
				{
					int id = AddTileSource(g.ResPath);
					if (id >= 0) _groundSources.Add(id);
				}
			}
		}
	}

	private void AttachBlockingBody(int sourceId, string role)
	{
		if (_wallPhysicsLayer < 0) return;
		if (_tileSet.GetSource(sourceId) is not TileSetAtlasSource atlas) return;
		if (atlas.GetTileData(Vector2I.Zero, 0) is not TileData td) return;

		td.SetCollisionPolygonsCount(_wallPhysicsLayer, 1);
		td.SetCollisionPolygonPoints(_wallPhysicsLayer, 0, BlockingPolygon(role));
	}

	// Builds opaque wall tile sources with full-cell collision on the wall physics layer.
	// Set on the first attempt, so a missing wall material is reported once rather than once per
	// wall cell of every chunk painted after it.
	private bool _wallSourcesTried;

	private void EnsureWallSources()
	{
		_wallSourcesTried = true;
		if (!_catalog.HasGroundMaterial(WallMaterial))
		{
			GD.PushError($"LevelTilePainter: wall material '{WallMaterial}' is not in any tile manifest; "
				+ "maze walls would silently paint as another floor. Re-run: python tools/art/tiles.py");
			return;
		}
		var variants = _catalog.GroundVariants(WallMaterial);
		float half = TileSize / 2f;
		Vector2[] square =
		{
			new(-half, -half), new(half, -half), new(half, half), new(-half, half),
		};

		foreach (CuratedTileEntry v in variants)
		{
			int id = AddTileSource(v.ResPath);
			if (id < 0) continue;
			if (_tileSet.GetSource(id) is TileSetAtlasSource atlas
				&& atlas.GetTileData(Vector2I.Zero, 0) is TileData td
				&& _wallPhysicsLayer >= 0)
			{
				td.SetCollisionPolygonsCount(_wallPhysicsLayer, 1);
				td.SetCollisionPolygonPoints(_wallPhysicsLayer, 0, square);
			}
			_wallSources.Add(id);
		}
	}

	private int AddTileSource(string resPath)
	{
		Texture2D tex = LoadTexture(resPath);
		if (tex == null)
		{
			GD.PushWarning($"LevelTilePainter: missing texture '{resPath}'.");
			return -1;
		}

		var src = new TileSetAtlasSource
		{
			Texture = tex,
			TextureRegionSize = new Vector2I(TileSize, TileSize),
		};
		src.CreateTile(Vector2I.Zero);
		return _tileSet.AddSource(src);
	}

	private static Texture2D LoadTexture(string resPath)
	{
		// Prefer the imported resource (editor/exported builds); fall back to reading the
		// raw PNG so the pipeline also works on freshly generated frames that lack a .import.
		if (ResourceLoader.Exists(resPath))
			return GD.Load<Texture2D>(resPath);

		Image img = Image.LoadFromFile(resPath);
		return img != null ? ImageTexture.CreateFromImage(img) : null;
	}

	// True if the texture is >= 90% opaque, so it works as a hole-free base ground tile.
	private static bool IsNearOpaque(string resPath)
	{
		Texture2D tex = LoadTexture(resPath);
		Image img = tex?.GetImage();
		if (img == null) return false;
		if (img.IsCompressed()) img.Decompress();

		int w = img.GetWidth();
		int h = img.GetHeight();
		if (w == 0 || h == 0) return false;

		int stepX = Mathf.Max(1, w / 16);
		int stepY = Mathf.Max(1, h / 16);
		int total = 0, opaque = 0;
		for (int y = 0; y < h; y += stepY)
		{
			for (int x = 0; x < w; x += stepX)
			{
				total++;
				if (img.GetPixel(x, y).A >= 0.9f) opaque++;
			}
		}
		return total > 0 && (float)opaque / total >= 0.9f;
	}

	// --- Built-in demo generator (mirrors the Python validation prototype) ---

	private string[,] BuildDemoGrid(int w, int h)
	{
		var grid = new string[w, h];
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				grid[x, y] = BaseTerrain;

		Blob(grid, 5, 5, 3.4f, "grass");
		Blob(grid, 16, 4, 3.0f, "water");
		Blob(grid, 9, 11, 3.0f, "dark_dirt");
		Blob(grid, 19, 11, 2.4f, "lava");
		Blob(grid, 13, 8, 2.1f, "pit");

		// Carve concave notches so inner (concave) corners are exercised.
		Bite(grid, 5, 3, 8, 6);
		Bite(grid, 16, 4, 19, 7);
		Bite(grid, 18, 9, 21, 11);
		return grid;
	}

	private void Blob(string[,] grid, int cx, int cy, float radius, string terrain)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
				if (dist <= radius + _rng.RandfRange(-1.2f, 1.2f))
					grid[x, y] = terrain;
			}
		}
	}

	private void Bite(string[,] grid, int x0, int y0, int x1, int y1)
	{
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		for (int y = y0; y < y1; y++)
			for (int x = x0; x < x1; x++)
				if (x >= 0 && x < w && y >= 0 && y < h)
					grid[x, y] = BaseTerrain;
	}

	private void AddDemoCamera()
	{
		var cam = new Camera2D
		{
			Position = new Vector2(DemoSize.X * TileSize / 2f, DemoSize.Y * TileSize / 2f),
		};
		AddChild(cam);
		cam.MakeCurrent();
	}
}
