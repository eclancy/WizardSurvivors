using System.Collections.Generic;
using System.Text.Json;
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

	/// <summary>
	/// The physics layer (1-based, as SetCollisionMaskValue takes it) every blocking tile - water
	/// and maze walls - is on. Its own layer rather than layer 1 with the decor, because the
	/// player meets the two with different parts of himself: the ground with his feet (Player's
	/// Feet body, so his boots stop at the drawn shore and his body may stand in front of the
	/// water), decor and enemies with his body as before. Anything that must stop at water or
	/// a wall has to mask this layer: Player's Feet and Enemy.ConfigureEntityCollision.
	/// </summary>
	public const int TerrainCollisionLayer = 4;

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

	/// <summary>
	/// materialFamily of the flat tiles a maze wall falls back to when the `wall` terrain has no art for
	/// its role. The Cursed Dungeon paints the `wall` terrain's jail tiles by role (WallTileFor).
	/// </summary>
	/// <remarks>
	/// "void" is a flat `occ` tile from tools/art/tiles.py: a wall drawn as the absence of floor, which
	/// is what every maze wall was until the jail walls (.ai/terrain.md section 6).
	/// This used to name "darkbrick", which no manifest has defined since the bought tile pack went,
	/// and GroundVariants quietly fell back to the first material it had - sand - so the Cursed
	/// Dungeon's walls were sand tinted grey-blue, a value step away from its floor. EnsureWallSources
	/// now refuses that fallback out loud.
	/// </remarks>
	[Export] public string WallMaterial { get; set; } = "void";

	/// <summary>Tint applied to wall tiles. White: the wall art is drawn in the contract's own tones, and
	/// a tint would move every one of them off it.</summary>
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

		// Physics layer used only by blocking tiles (water, maze walls), on TerrainCollisionLayer.
		// It was collision layer 1, shared with the decor props, until the player's feet had to meet
		// the ground without his body also meeting it - see TerrainCollisionLayer.
		_tileSet.AddPhysicsLayer();
		_wallPhysicsLayer = _tileSet.GetPhysicsLayersCount() - 1;
		_tileSet.SetPhysicsLayerCollisionLayer(_wallPhysicsLayer, 1u << (TerrainCollisionLayer - 1));
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
					PutTile(_groundLayer, coord, gs);
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
						PutTile(_wallLayer, coord, ws);
					}
					_cellTerrain[coord] = r.Terrain;
					continue;
				}

				// Water is drawn by WaterSurface on the streamed stages, not by tiles; this demo
				// path just leaves it out.
				string key = TileKey(r.Terrain, TerrainAutotiler.RoleKey(r.Role));
				if (!IsBlockingTerrain(r.Terrain) && _sourceIds.TryGetValue(key, out int src) && src >= 0)
					PutTile(_overlayLayer, coord, src);

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
					PutTile(ground, coord, _groundSources[(int)(h % (ulong)variants)]);
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
	/// <param name="wallNeighbours">TerrainChunk.WallNeighbours (the walls round each cell: where a wall's
	/// shadow falls on the floor, and which sides of a wall cell are open), or null for a chunk with no
	/// wall near it.</param>
	public void PaintChunkTerrain(Vector2I chunk, Vector2I minCell, int size, string[] terrain, AutotileRole[] roles,
		byte[] wallNeighbours = null)
	{
		if (!_chunkNodes.TryGetValue(chunk, out Node2D root))
			return;

		TileMapLayer overlay = null;
		TileMapLayer walls = null;
		TileMapLayer Walls()
		{
			if (walls == null)
			{
				walls = new TileMapLayer { Name = "Walls", TileSet = _tileSet, Modulate = WallModulate };
				root.AddChild(walls);
			}
			return walls;
		}

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				string t = terrain[x + y * size];
				var coord = new Vector2I(minCell.X + x, minCell.Y + y);
				if (t == null)
				{
					// Open floor beside a wall: the wall's shadow, in the walls' own layer (same atlas,
					// so no extra draw call), and with no collision.
					byte shade = wallNeighbours != null ? wallNeighbours[x + y * size] : (byte)0;
					if (shade != 0 && WallShadowSource(shade) is int s and >= 0)
						PutTile(Walls(), coord, s);
					continue;
				}

				if (t == WallTerrainId)
				{
					byte open = wallNeighbours != null ? wallNeighbours[x + y * size] : (byte)0;
					int tile = WallTileFor(WallRoleKey(roles[x + y * size], open), coord);
					if (tile < 0) continue;
					PutTile(Walls(), coord, tile);
					continue;
				}

				// Water is not tiles: Prepare/FinishChunkWater draw it, shore and collision both, from
				// WaterShape (.ai/terrain.md section 3).
				if (IsBlockingTerrain(t)) continue;
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
				PutTile(overlay, coord, src);
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
				foreach (AutotileRole role in System.Enum.GetValues<AutotileRole>())
					EnsureWallRoleSources(TerrainAutotiler.RoleKey(role));
				foreach (string key in StubWallRoles)
					EnsureWallRoleSources(key);
				for (byte shade = 1; shade < 8; shade++)
					WallShadowSource(shade);
				continue;
			}
			if (IsBlockingTerrain(t))
				continue;
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
				PutTile(detail, coord, variants[(int)(h % (ulong)variants.Length)]);
			}
		}
		detail?.UpdateInternals();
	}

	// --- Open water (.ai/terrain.md section 3) ---------------------------------------------------
	//
	// Water is not tiles. WaterShape works out, per water cell, the convex polygon of it that is
	// water - smooth shores, straight diagonals, round corners, never outside the blocked cells - and
	// WaterSurface draws a distance field of those polygons with a shader and collides with the same
	// polygons on TerrainCollisionLayer. One node a chunk, after the overlay, before the detail layer.

	/// <summary>Which water art to use: "forest", "swamp", "frozen" or "pool" (tools/art/water.py).</summary>
	public string WaterStyle { get; set; } = "pool";

	private WaterShape _waterShape;
	private WaterSurface.Builder _waterBuilder;
	private ShaderMaterial _waterMaterial;

	// Which chunk the shape and the field in the builder belong to, between the two halves.
	private Vector2I? _waterPrepared;

	/// <summary>
	/// The first half of one chunk's water: its shape and its distance field. <paramref name="fillWet"/>
	/// fills a square of blocked flags (buffer, first world cell, side) - the window round the chunk,
	/// which reaches into its neighbours, so a shore across a chunk border is one shore. False when
	/// there is no water near the chunk, and then there is no second half to run.
	/// </summary>
	public bool PrepareChunkWater(Vector2I chunk, Vector2I minCell, int size, System.Action<bool[], Vector2I, int> fillWet)
	{
		_waterPrepared = null;
		if (!_chunkNodes.ContainsKey(chunk))
			return false;
		_waterShape ??= new WaterShape(size);
		_waterBuilder ??= new WaterSurface.Builder(size);
		fillWet(_waterShape.WetBuffer, _waterShape.WindowMin(minCell), _waterShape.WindowCells);
		if (!_waterShape.AnyWater())
			return false;
		_waterShape.Build(minCell, VariantSeed);
		if (!_waterBuilder.Prepare(_waterShape, minCell))
			return false;
		_waterPrepared = chunk;
		return true;
	}

	/// <summary>
	/// The second half: the field's texture, the node that draws it and the collision body. Prepares
	/// again first if the builder has moved on to another chunk in between (a border crossing can
	/// reorder the streamer's queue between two steps).
	/// </summary>
	public void FinishChunkWater(Vector2I chunk, Vector2I minCell, int size, System.Action<bool[], Vector2I, int> fillWet)
	{
		if (_waterPrepared != chunk && !PrepareChunkWater(chunk, minCell, size, fillWet))
			return;
		_waterPrepared = null;
		if (!_chunkNodes.TryGetValue(chunk, out Node2D root))
			return;
		WaterSurface node = _waterBuilder.Finish(_waterShape, minCell, root, EnsureWaterMaterial(),
			GlobalTransform, GetWorld2D().Space, 1u << (TerrainCollisionLayer - 1));
		// Under the detail layer if it is already there (reeds and lilypads sit on the water).
		Node detail = root.GetNodeOrNull("Detail");
		if (detail != null)
			root.MoveChild(node, detail.GetIndex());
	}

	private ShaderMaterial EnsureWaterMaterial()
	{
		if (_waterMaterial != null)
			return _waterMaterial;
		const string Dir = "res://assets/bonelight/tiles/water/";
		var shader = GD.Load<Shader>("res://scenes/shaders/water_surface.gdshader");
		Texture2D surface = LoadTexture($"{Dir}{WaterStyle}_surface.png");
		Texture2D tones = LoadTexture($"{Dir}{WaterStyle}_tones.png");
		Texture2D ripple = LoadTexture($"{Dir}ripple.png");
		if (shader == null || surface == null || tones == null || ripple == null)
		{
			GD.PushError($"LevelTilePainter: water art for '{WaterStyle}' is missing; water would draw as nothing. "
				+ "Re-run: python tools/art/water.py, then import.");
		}
		_waterMaterial = new ShaderMaterial { Shader = shader };
		_waterMaterial.SetShaderParameter("surface_tex", surface);
		_waterMaterial.SetShaderParameter("tones", tones);
		_waterMaterial.SetShaderParameter("ripple_tex", ripple);
		return _waterMaterial;
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
		foreach (CuratedTileEntry v in _catalog.GroundVariants(WallMaterial))
		{
			// Its own tile data: the collision square must not land on the same art painted as floor
			// anywhere else.
			int id = AddTileSource(v.ResPath, ownTileData: true);
			if (id < 0) continue;
			AddWallCollision(id);
			_wallSources.Add(id);
		}
	}

	// --- The Cursed Dungeon's jail walls (.ai/terrain.md section 6, tools/art/jail_walls.py) -------
	//
	// The walls were one flat `occ` tile. They are now the `wall` terrain's tiles, picked per cell by
	// the role the autotiler resolved for it - floor to the south is the wall's face, anything else
	// its top - and dressed: a role's PLAIN variants are the wall, and a cell paints one of its
	// DRESSED variants (a sconce, a skull in a niche, a barred window) only when it rolls under the
	// role's chance and the cell before it along the wall did not roll at all, so two never stand
	// side by side. Every roll is a hash of the world cell, so a chunk painted again is identical,
	// and the neighbour's roll is a hash too, so a chunk border changes nothing.

	/// <summary>The plain and the (weight-repeated) dressed sources of one wall role.</summary>
	private readonly struct WallRoleSources
	{
		public readonly int[] Plain;
		public readonly int[] Dressed;

		public WallRoleSources(int[] plain, int[] dressed)
		{
			Plain = plain;
			Dressed = dressed;
		}
	}

	private readonly Dictionary<string, WallRoleSources> _wallRoles = new();

	// The chance a cell of each role carries dressing, before the no-neighbour rule halves it. Faces
	// carry most - they are what the eye reads as a jail - the tops of the vertical walls a sconce or
	// a chain now and then, and the plain tops a little moss. No role may exceed MaxWallDressChance,
	// which is what makes "the neighbour rolled under it" mean "the neighbour might be dressed".
	private const float MaxWallDressChance = 0.5f;

	private static float WallDressChance(string roleKey) => roleKey switch
	{
		"edge_bottom" => 0.5f,
		"edge_left" or "edge_right" => 0.16f,
		"edge_top" or "fill" => 0.06f,
		_ => 0f,
	};

	// The extra wall art for the one-tile stubs the spawn clearing leaves (tools/art/jail_walls.py).
	private static readonly string[] StubWallRoles = { "patch_v", "patch_cap", "patch_face" };

	/// <summary>
	/// Which wall art a cell takes. The autotile role, except for Patch: the spawn clearing (a disc of
	/// open floor round the start, LevelGenerator.ClearSpawn) cuts the two-tile walls near the start down
	/// to one tile, and every such cell resolves to Patch, which says only "not a shape the set has".
	/// Spelled out from its open sides, a stub is a one-tile wall like any other: a face if the floor is
	/// south of it, otherwise a top with its edges lit.
	/// </summary>
	private static string WallRoleKey(AutotileRole role, byte open)
	{
		if (role != AutotileRole.Patch || open == 0)
			return TerrainAutotiler.RoleKey(role);
		bool n = (open & TerrainStreamer.WallOpenN) != 0, s = (open & TerrainStreamer.WallOpenS) != 0;
		bool w = (open & TerrainStreamer.WallOpenW) != 0, e = (open & TerrainStreamer.WallOpenE) != 0;
		if (n && s && w && e)
			return "patch";
		if (s)
			return w && e ? "patch_face" : w ? "corner_bl" : e ? "corner_br" : "edge_bottom";
		if (w && e)
			return n ? "patch_cap" : "patch_v";
		if (n)
			return w ? "corner_tl" : e ? "corner_tr" : "edge_top";
		return w ? "edge_left" : e ? "edge_right" : "fill";
	}

	private static float WallRoll(ulong seed, int x, int y) =>
		(LevelGenerator.Hash(seed, x, y, 0x3A11) % 10000UL) / 10000f;

	/// <summary>The wall tile for one cell, or -1 if there is no wall art at all.</summary>
	private int WallTileFor(string roleKey, Vector2I cell)
	{
		WallRoleSources set = EnsureWallRoleSources(roleKey);
		if (set.Plain.Length == 0)
		{
			// No jail art for this role (a manifest without it): the flat void wall, as before, rather
			// than a wall painted as nothing.
			if (_wallSources.Count == 0 && !_wallSourcesTried)
				EnsureWallSources();
			if (_wallSources.Count == 0) return -1;
			ulong hv = LevelGenerator.Hash(VariantSeed, cell.X, cell.Y, 0x3A11);
			return _wallSources[(int)(hv % (ulong)_wallSources.Count)];
		}

		// Along the wall: a face or a top runs east-west, a vertical wall's sides run north-south.
		bool vertical = roleKey is "edge_left" or "edge_right";
		Vector2I before = vertical ? new Vector2I(cell.X, cell.Y - 1) : new Vector2I(cell.X - 1, cell.Y);
		if (set.Dressed.Length > 0
			&& WallRoll(VariantSeed, cell.X, cell.Y) < WallDressChance(roleKey)
			&& WallRoll(VariantSeed, before.X, before.Y) >= MaxWallDressChance)
		{
			ulong hd = LevelGenerator.Hash(VariantSeed, cell.X, cell.Y, 0x3A12);
			return set.Dressed[(int)(hd % (ulong)set.Dressed.Length)];
		}
		ulong hp = LevelGenerator.Hash(VariantSeed, cell.X, cell.Y, 0x3A13);
		return set.Plain[(int)(hp % (ulong)set.Plain.Length)];
	}

	private WallRoleSources EnsureWallRoleSources(string roleKey)
	{
		if (_wallRoles.TryGetValue(roleKey, out WallRoleSources known))
			return known;
		var plain = new List<int>();
		var dressed = new List<int>();
		foreach (CuratedTileEntry e in _catalog.TileVariants(WallTerrainId, roleKey))
		{
			// TileVariants falls back to the fills for a role the manifest lacks; a top is the
			// right thing to paint then, but not the fills' dressing.
			if (e.Role != roleKey && e.Dressing != null) continue;
			int id = AddTileSource(e.ResPath, ownTileData: true);
			if (id < 0) continue;
			AddWallCollision(id);
			if (e.Dressing == null)
				plain.Add(id);
			else
				for (int k = 0; k < e.Weight; k++)
					dressed.Add(id);
		}
		var set = new WallRoleSources(plain.ToArray(), dressed.ToArray());
		_wallRoles[roleKey] = set;
		return set;
	}

	// The full-cell collision square every wall tile carries, on the terrain layer. Its own tile data
	// (an alternative of the atlas cell), so it can never land on the same art used anywhere else.
	private void AddWallCollision(int handle)
	{
		if (_wallPhysicsLayer < 0) return;
		TileRef tile = _tiles[handle];
		if (_tileSet.GetSource(tile.Source) is not TileSetAtlasSource atlas
			|| atlas.GetTileData(tile.Coords, tile.Alternative) is not TileData td)
			return;
		float half = TileSize / 2f;
		td.SetCollisionPolygonsCount(_wallPhysicsLayer, 1);
		td.SetCollisionPolygonPoints(_wallPhysicsLayer, 0, new Vector2[]
		{
			new(-half, -half), new(half, -half), new(half, half), new(-half, half),
		});
	}

	private readonly int[] _wallShadowSources = { -2, -2, -2, -2, -2, -2, -2, -2 };

	/// <summary>
	/// The shadow tile for a floor cell with these TerrainStreamer.WallShade* bits, or -1. The key light
	/// is upper-left, so a wall throws its shadow down and right: over the floor at the foot of a face
	/// (a wall north of the cell) and beside a vertical wall (west of it). No collision: it is floor.
	/// </summary>
	private int WallShadowSource(byte shade)
	{
		shade &= 7;
		if (_wallShadowSources[shade] != -2)
			return _wallShadowSources[shade];
		bool n = (shade & TerrainStreamer.WallShadeN) != 0;
		bool w = (shade & TerrainStreamer.WallShadeW) != 0;
		string role = n && w ? "shade_nw" : n ? "shade_n" : w ? "shade_w"
			: (shade & TerrainStreamer.WallShadeNW) != 0 ? "shade_corner" : null;
		int id = -1;
		if (role != null)
		{
			IReadOnlyList<CuratedTileEntry> v = _catalog.TileVariants("wall_shadow", role);
			if (v.Count > 0 && v[0].Role == role)
				id = AddTileSource(v[0].ResPath);
		}
		_wallShadowSources[shade] = id;
		return id;
	}

	// --- The tile atlas (tools/art/tile_atlas.py) -------------------------------------------------
	//
	// Every tile the bonelight manifests name is a cell of one packed texture, atlas.png, and the
	// TileSet holds it as ONE TileSetAtlasSource. It used to give each 48x48 PNG a source of its own,
	// and the Compatibility renderer batches draws only while they share a texture: random variants
	// side by side made nearly every visible cell of the ground, overlay and detail layers its own
	// draw call - about 2,000 a frame on the phone's portrait view, 95% of all of them
	// (.ai/performance.md). Now a layer's quadrant is one draw. The pixels are the same. Each atlas
	// cell carries a one-pixel extruded border that the source's margins and separation step over, so
	// nearest sampling never bleeds a neighbour in, and Godot's own texture padding - which read every
	// source back from the GPU at load to build a padded copy - is off. A tile the atlas lacks (a
	// manifest newer than its atlas, or the Sketchbook's paper) still gets a source of its own.
	[Export] public string AtlasMetadataPath { get; set; } = "res://assets/bonelight/tiles/atlas.json";
	[Export] public string AtlasTexturePath { get; set; } = "res://assets/bonelight/tiles/atlas.png";

	/// <summary>Where a tile handle (what AddTileSource returns) points: a source, a cell and an alternative.</summary>
	private readonly struct TileRef
	{
		public readonly int Source;
		public readonly Vector2I Coords;
		public readonly int Alternative;

		public TileRef(int source, Vector2I coords, int alternative)
		{
			Source = source;
			Coords = coords;
			Alternative = alternative;
		}
	}

	private readonly List<TileRef> _tiles = new();
	private readonly Dictionary<string, int> _tileByPath = new();
	private Dictionary<string, Vector2I> _atlasCells;
	private TileSetAtlasSource _atlasSource;
	private int _atlasSourceId = -1;

	private void PutTile(TileMapLayer layer, Vector2I cell, int tile)
	{
		TileRef t = _tiles[tile];
		layer.SetCell(cell, t.Source, t.Coords, t.Alternative);
	}

	/// <summary>
	/// The atlas manifest written by tools/art/tile_atlas.py: each packed tile's res:// path to its
	/// cell. Empty when there is none, or when it was packed for another tile size.
	/// </summary>
	public static Dictionary<string, Vector2I> LoadAtlasManifest(string path, int tileSize, out int padding)
	{
		var cells = new Dictionary<string, Vector2I>();
		padding = 0;
		if (!Godot.FileAccess.FileExists(path))
			return cells;
		string text = Godot.FileAccess.GetFileAsString(path);
		if (string.IsNullOrEmpty(text))
			return cells;
		using var doc = JsonDocument.Parse(text);
		JsonElement root = doc.RootElement;
		if (!root.TryGetProperty("tile", out JsonElement tile) || tile.GetInt32() != tileSize
			|| !root.TryGetProperty("padding", out JsonElement pad) || !root.TryGetProperty("tiles", out JsonElement tiles))
			return cells;
		padding = pad.GetInt32();
		foreach (JsonProperty entry in tiles.EnumerateObject())
		{
			string key = entry.Name.StartsWith("res://") ? entry.Name : "res://" + entry.Name;
			cells[key] = new Vector2I(entry.Value[0].GetInt32(), entry.Value[1].GetInt32());
		}
		return cells;
	}

	private bool EnsureAtlas()
	{
		if (_atlasCells != null)
			return _atlasSourceId >= 0;
		_atlasCells = LoadAtlasManifest(AtlasMetadataPath, TileSize, out int padding);
		if (_atlasCells.Count == 0)
			return false;
		Texture2D texture = ResourceLoader.Exists(AtlasTexturePath) ? GD.Load<Texture2D>(AtlasTexturePath) : null;
		if (texture == null)
		{
			GD.PushWarning($"LevelTilePainter: '{AtlasTexturePath}' did not load; every tile gets a texture of its own. "
				+ "Re-run: python tools/art/tile_atlas.py");
			_atlasCells.Clear();
			return false;
		}
		_atlasSource = new TileSetAtlasSource
		{
			Texture = texture,
			TextureRegionSize = new Vector2I(TileSize, TileSize),
			Margins = new Vector2I(padding, padding),
			Separation = new Vector2I(2 * padding, 2 * padding),
			UseTexturePadding = false,
		};
		_atlasSourceId = _tileSet.AddSource(_atlasSource);
		return true;
	}

	// A handle for the tile drawn from resPath. One per path, except with ownTileData - a tile whose
	// data (a wall's collision square) must not be shared with the same art used elsewhere, which in
	// the atlas becomes an alternative tile of that cell.
	private int AddTileSource(string resPath, bool ownTileData = false)
	{
		if (!ownTileData && _tileByPath.TryGetValue(resPath, out int known))
			return known;

		int handle;
		if (EnsureAtlas() && _atlasCells.TryGetValue(resPath, out Vector2I cell))
		{
			if (!_atlasSource.HasTile(cell))
				_atlasSource.CreateTile(cell);
			int alternative = ownTileData ? _atlasSource.CreateAlternativeTile(cell) : 0;
			handle = _tiles.Count;
			_tiles.Add(new TileRef(_atlasSourceId, cell, alternative));
		}
		else
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
			handle = _tiles.Count;
			_tiles.Add(new TileRef(_tileSet.AddSource(src), Vector2I.Zero, 0));
		}
		if (!ownTileData)
			_tileByPath[resPath] = handle;
		return handle;
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
