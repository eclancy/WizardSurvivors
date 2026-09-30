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
	[Export] public string WallMaterial { get; set; } = "darkbrick";

	/// <summary>Tint applied to wall tiles so they read as raised, impassable walls.</summary>
	[Export] public Color WallModulate { get; set; } = new Color(0.42f, 0.44f, 0.50f);

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
				if (_sourceIds.TryGetValue(key, out int src))
					_overlayLayer.SetCell(coord, src, Vector2I.Zero);

				_cellTerrain[coord] = r.Terrain;
				if (IsHazardTerrain(r.Terrain))
					_hazardCells.Add(coord);
				if (IsBlockingTerrain(r.Terrain))
					_blockedCells.Add(coord);
			}
		}
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

		return role switch
		{
			"edge_top" => Box(-h, -h + s, h, h),
			"edge_bottom" => Box(-h, -h, h, h - s),
			"edge_left" => Box(-h + s, -h, h, h),
			"edge_right" => Box(-h, -h, h - s, h),
			"corner_tl" => Box(-h + s, -h + s, h, h),
			"corner_tr" => Box(-h, -h + s, h - s, h),
			"corner_bl" => Box(-h + s, -h, h, h - s),
			"corner_br" => Box(-h, -h, h - s, h - s),
			// A patch is a lone cell of water with ground on all four sides.
			"patch" => Box(-h + s, -h + s, h - s, h - s),
			// fill and the inner corners are water across the whole cell.
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

				string role = TerrainAutotiler.RoleKey(r.Role);
				string key = TileKey(r.Terrain, role);
				if (_sourceIds.ContainsKey(key)) continue;
				if (!_catalog.TryGetTile(r.Terrain, role, out CuratedTileEntry e)) continue;

				int id = AddTileSource(e.ResPath);
				if (id < 0) continue;
				_sourceIds[key] = id;

				// A blocking terrain gets a body shaped to the role it is painting. Attached to
				// the SOURCE, once, rather than to each placed cell - every cell of the same
				// (terrain, role) shares one source, which is why this sits here and not in Paint.
				if (e.Blocking)
					AttachBlockingBody(id, role);
			}
		}

		if (needWalls && _wallSources.Count == 0)
			EnsureWallSources();
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
	private void EnsureWallSources()
	{
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
