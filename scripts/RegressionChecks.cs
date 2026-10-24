using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public static class RegressionChecks
{
	public static List<string> RunAll()
	{
		var warnings = new List<string>();
		ValidateDynamicRewardBounds(warnings);
		ValidateTelemetryHistoryCap(warnings);
		ValidateCharacterRosterBasics(warnings);
		ValidateSaveDefaults(warnings);
		ValidatePlaytestChecklistDefaults(warnings);
		ValidatePresetRewardOrdering(warnings);
		ValidateSpellEvolutionCoverage(warnings);
		ValidateApotheosisCatalog(warnings);
		ValidateChestSetPresentation(warnings);
		ValidateFullSetObjects(warnings);
		ValidateTomeRelics(warnings);
		ValidateCollectorEconomy(warnings);
		ValidateBossCatalog(warnings);
		ValidateBossPatterns(warnings);
		ValidateRangedEnemies(warnings);
		ValidateMiniBoss(warnings);
		ValidateAttackPatternEnemies(warnings);
		ValidateStartingCharacters(warnings);
		ValidateElementTags(warnings);
		ValidateElementPaths(warnings);
		ValidateSpawnFormations(warnings);
		ValidateBoonSources(warnings);
		ValidateBoonPresentation(warnings);
		ValidateChestRarity(warnings);
		ValidateElementReachability(warnings);
		ValidateUnlockCatalog(warnings);
		ValidateAchievements(warnings);
		ValidateStageCatalog(warnings);
		ValidateAudio(warnings);
		ValidateMusic(warnings);
		ValidateInputMap(warnings);
		ValidateBlockingTerrain(warnings);
		ValidateTerrainSeams(warnings);
		ValidateWaterShapes(warnings);
		ValidateGroundArt(warnings);
		ValidateNavigation(warnings);
		ValidateStatusEffectAtlas(warnings);
		ValidateFlowerPen(warnings);
		ValidateSpellClassificationText(warnings);
		ValidateExploreArt(warnings);
		return warnings;
	}

	// The pause screen's spell details are written in SpellClassificationText's words. A new enum
	// value without a phrase would fall back to its code name on screen, which is the thing that
	// was reported as unreadable - so a missing phrase is a warning here, not a silent fallback.
	private static void ValidateSpellClassificationText(List<string> warnings)
	{
		foreach (SpellTargetingMode mode in Enum.GetValues<SpellTargetingMode>())
			if (string.IsNullOrEmpty(SpellClassificationText.Aim(mode)))
				warnings.Add($"Spell details: SpellTargetingMode.{mode} has no plain-English phrase in SpellClassificationText.Aim.");
		foreach (SpellDamageShape shape in Enum.GetValues<SpellDamageShape>())
			if (string.IsNullOrEmpty(SpellClassificationText.Shape(shape)))
				warnings.Add($"Spell details: SpellDamageShape.{shape} has no plain-English phrase in SpellClassificationText.Shape.");
		foreach (SpellScalingTag tag in Enum.GetValues<SpellScalingTag>())
			if (string.IsNullOrEmpty(SpellClassificationText.Scaling(tag)))
				warnings.Add($"Spell details: SpellScalingTag.{tag} has no plain-English word in SpellClassificationText.Scaling.");
	}

	// The exploration art (.ai/side-events.md, "Finding the event"): every sprite explore.json names
	// is imported and stands inside its own picture, the lantern knows where its flame is, every
	// glyph an arrow can carry exists, and every event chapter's wayfinder has a sprite. A missing
	// PNG draws nothing and says nothing (CLAUDE.md), which here would be a lantern that is only
	// light, or an arrow with no pictogram in it. Re-run: python tools/art/explore_props.py, then import.
	private static void ValidateExploreArt(List<string> warnings)
	{
		Dictionary<string, ExploreSprite> sprites = ExploreArt.Load();
		foreach (string id in new[] { "bog-lantern", "arcane-compass", "gaolers-map", "bearing-cairn" })
		{
			if (!sprites.TryGetValue(id, out ExploreSprite s))
			{
				warnings.Add($"Explore: '{id}' is missing from {ExploreArt.Manifest}.");
				continue;
			}
			if (s.Texture == null)
				warnings.Add($"Explore: '{id}.png' is not imported; it will not be drawn.");
			if (s.Frames <= 0 || s.Ground <= 0 || s.Ground > s.FrameH || s.AnchorX <= 0 || s.AnchorX >= s.FrameW)
				warnings.Add($"Explore: '{id}' has its ground line or foot column outside its frame.");
			if (id == "bog-lantern" && (s.Flame == Vector2.Zero || s.LitFrames <= 0 || s.LitFrames >= s.Frames))
				warnings.Add("Explore: the bog lantern needs a flame position and fewer lit frames than frames (the last is dark).");
		}
		foreach (string glyph in ExploreArt.Glyphs)
		{
			if (!ResourceLoader.Exists(ExploreArt.GlyphPath(glyph)))
				warnings.Add($"Explore: arrow glyph '{glyph}' is missing or not imported ({ExploreArt.GlyphPath(glyph)}).");
		}
		foreach (bool wayfinder in new[] { false, true })
		{
			if (!ResourceLoader.Exists(ExploreArt.BadgePath(wayfinder)))
				warnings.Add($"Explore: arrow badge {ExploreArt.BadgePath(wayfinder)} is missing or not imported.");
		}
		foreach (StageDefinition stage in StageCatalog.All)
		{
			var entry = SideEventDirector.ForStage(stage.Id);
			if (entry != null && Array.IndexOf(ExploreArt.Glyphs, WayfinderPickup.GlyphFor(entry.Value.Wayfinder)) < 0)
				warnings.Add($"Explore: {stage.Id}'s wayfinder {entry.Value.Wayfinder} has no arrow glyph.");
		}
	}

	// The status marks (StatusEffectLayer) are drawn out of one atlas by row and frame index, so a
	// sheet regenerated with a row added, or never imported at all, would draw the wrong mark or
	// nothing - silently either way, because DrawTextureRectRegion on a null or short texture does
	// not complain. Re-run: python tools/art/status_fx.py, then import.
	private static void ValidateStatusEffectAtlas(List<string> warnings)
	{
		if (!ResourceLoader.Exists(StatusEffectLayer.AtlasPath))
		{
			warnings.Add($"Status marks: {StatusEffectLayer.AtlasPath} is missing or not imported; no status effects will show on enemies.");
			return;
		}
		Texture2D atlas = GD.Load<Texture2D>(StatusEffectLayer.AtlasPath);
		var expected = new Vector2(StatusEffectLayer.Cell * StatusEffectLayer.Frames, StatusEffectLayer.Cell * StatusEffectLayer.Rows);
		if (atlas == null)
			warnings.Add($"Status marks: {StatusEffectLayer.AtlasPath} exists but did not load as a texture.");
		else if (atlas.GetSize() != expected)
			warnings.Add($"Status marks: {StatusEffectLayer.AtlasPath} is {atlas.GetSize()}, expected {expected} ({StatusEffectLayer.Rows} rows of {StatusEffectLayer.Frames} {StatusEffectLayer.Cell}px frames).");
	}

	// Enemy navigation on a hand-made lake (.ai/terrain.md, "Enemy navigation"). Three things a build
	// cannot see and a playtest only shows as "the swarm piles up on the shore again":
	//   - a field built a slice at a time over several frames must equal one built whole, or the
	//     resumable queue has lost or double-counted a cell;
	//   - the straight-walk test must see the lake, or no enemy on an open stage ever reads the field;
	//   - the flow on the far side must go round the lake, the short way, not into it.
	// Also that a ring of water seals its middle off, so nothing is ever spawned on an island.
	private static void ValidateNavigation(List<string> warnings)
	{
		const int size = 40;
		const int tile = 48;
		var solid = new bool[size * size];
		var wall = new bool[size * size];
		// A lake from x 12 to 30, y 15 to 20: the way round its left end (x 11) is shorter than its
		// right (x 31) for anything standing above x 20.
		for (int y = 15; y <= 20; y++)
			for (int x = 12; x <= 30; x++)
				solid[x + y * size] = true;
		// A one-tile ring of water with dry ground inside it: an island.
		for (int y = 2; y <= 8; y++)
			for (int x = 30; x <= 36; x++)
				if (x == 30 || x == 36 || y == 2 || y == 8)
					solid[x + y * size] = true;

		Vector2 Cell(int x, int y) => new Vector2((x + 0.5f) * tile, (y + 0.5f) * tile);
		Vector2 player = Cell(20, 32);
		Vector2 behind = Cell(20, 6);

		var whole = new MazeNavigation(size, tile, Vector2.Zero, isMaze: false);
		whole.RebuildFromGrid(solid, wall, Vector2I.Zero, player);

		// Built first round another cell, then moved to the player's and stepped in tiny slices.
		var sliced = new MazeNavigation(size, tile, Vector2.Zero, isMaze: false) { CellsPerFrame = 37 };
		sliced.RebuildFromGrid(solid, wall, Vector2I.Zero, Cell(5, 35));
		int frames = 0;
		sliced.Update(player, 1f);
		while (sliced.IsBuildingField && frames++ < 1000)
			sliced.Update(player, 0.01f);
		if (frames < 2)
			warnings.Add("Navigation: the sliced field finished in one step - the regression check is not exercising resumption.");
		int mismatched = 0;
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				if (whole.PathTilesAt(Cell(x, y)) != sliced.PathTilesAt(Cell(x, y)))
					mismatched++;
		if (mismatched > 0)
			warnings.Add($"Navigation: a field built in slices differs from one built whole on {mismatched} cells (MazeNavigation.StepField).");

		if (whole.HasClearWalk(behind, player))
			warnings.Add("Navigation: the straight walk across a lake reads as clear - open-stage enemies would never route round water.");
		if (!whole.HasClearWalk(Cell(5, 6), Cell(5, 32)))
			warnings.Add("Navigation: a straight walk on dry ground reads as blocked.");

		Vector2 flow = whole.FlowDirectionAt(behind);
		if (flow.X > -0.2f)
			warnings.Add($"Navigation: the flow behind a lake is {flow}, which does not head round its near (left) end.");
		float walk = whole.PathTilesAt(behind);
		if (walk < 27f)
			warnings.Add($"Navigation: walking distance round the lake is {walk} tiles, no longer than the 26-tile straight line - the lake is not in the field.");

		if (whole.IsReachable(Cell(33, 5)))
			warnings.Add("Navigation: dry ground inside a ring of water counts as reachable - enemies could be spawned on an island.");
		if (!whole.IsReachable(Cell(5, 5)))
			warnings.Add("Navigation: open ground connected to the player counts as unreachable.");
		if (whole.IsWallAt(behind) || !whole.HasLineOfSight(behind, player))
			warnings.Add("Navigation: water is stopping shots - only maze walls should.");
	}

	// Water stops the player because assets/bonelight/tiles/tiles.json says "blocking": true on
	// its sixteen tiles - and that file is GENERATED. Re-run tools/art/tiles.py from a checkout
	// where the BLOCKERS set has been lost and the flag quietly disappears: the art still renders,
	// the game still builds, the tiles still paint, and the player walks across the lake. Nothing
	// else in the project would notice.
	private static void ValidateBlockingTerrain(List<string> warnings)
	{
		const string manifest = "res://assets/bonelight/tiles/tiles.json";
		CuratedTileCatalog catalog = CuratedTileCatalog.LoadMany(manifest);
		if (catalog == null)
		{
			warnings.Add($"Tiles: {manifest} did not load; terrain blocking cannot be checked.");
			return;
		}

		if (!catalog.IsBlockingTerrain("water"))
		{
			warnings.Add("Tiles: water is not marked blocking in tiles.json, so it can be walked on. "
				+ "Check BLOCKERS in tools/art/tiles.py and re-run: python tools/art/tiles.py");
		}

		// The maze walls are the "void" material. Without it LevelTilePainter refuses to paint them,
		// and before that guard it painted them as whatever floor came first - sand.
		if (!catalog.HasGroundMaterial("void"))
		{
			warnings.Add("Tiles: the 'void' material (maze walls) is missing from tiles.json. "
				+ "Check MATERIALS in tools/art/tiles.py and re-run: python tools/art/tiles.py");
		}

		// The inverse, so the flag cannot creep onto terrain that is meant to be crossed. Lava and
		// the pits hurt you for standing in them, which only works if you can stand in them.
		foreach (string crossable in new[]
		{
			"grass", "meadow", "moss", "dirt", "overgrowth", "sand", "cobble", "dark_dirt", "mossy_rock",
			"rocky_pit", "ice", "lava", "pit",
		})
		{
			if (catalog.IsBlockingTerrain(crossable))
				warnings.Add($"Tiles: '{crossable}' is marked blocking, which makes it a wall.");
		}
	}

	// Everything a stage paints has to exist, and nothing reports it if it does not: a terrain
	// missing from tiles.json paints as nothing (EnsureTerrainSources caches the miss), a missing
	// ground-detail set paints as nothing, and a missing landmark sprite places no landmark. The
	// natural-ground pass (.ai/terrain.md section 8) added nine names across three files, so this
	// holds the palettes, GroundDetail's rules and the art against each other.
	// The res:// path of every frame in a tile manifest (tiles.json's "frames").
	private static IEnumerable<string> ManifestTilePaths(string manifestPath)
	{
		string text = Godot.FileAccess.GetFileAsString(manifestPath);
		if (string.IsNullOrEmpty(text))
			yield break;
		using var doc = System.Text.Json.JsonDocument.Parse(text);
		if (!doc.RootElement.TryGetProperty("frames", out var frames))
			yield break;
		foreach (var frame in frames.EnumerateArray())
		{
			string path = frame.TryGetProperty("path", out var p) ? p.GetString() : null;
			if (!string.IsNullOrEmpty(path))
				yield return path.StartsWith("res://") ? path : "res://" + path;
		}
	}

	private static void ValidateGroundArt(List<string> warnings)
	{
		CuratedTileCatalog catalog = CuratedTileCatalog.LoadMany(
			"res://assets/bonelight/tiles/tiles.json", "res://assets/kidsart/tiles.json");
		var generator = new LevelGenerator();
		foreach (StageEnvironmentKind kind in Enum.GetValues<StageEnvironmentKind>())
		{
			LevelLayout layout = generator.Describe(kind);
			foreach (string terrain in layout.Terrains)
			{
				if (catalog.TileVariants(terrain, "fill").Count == 0)
					warnings.Add($"Ground: {kind} stamps '{terrain}', which tiles.json does not define. Re-run: python tools/art/tiles.py");
			}
			if (!string.IsNullOrEmpty(layout.GroundTerrain) && catalog.FillTilesFor(layout.GroundTerrain).Count == 0)
				warnings.Add($"Ground: {kind} stands on terrain '{layout.GroundTerrain}', which has no fill tiles.");
			else if (string.IsNullOrEmpty(layout.GroundTerrain) && !catalog.HasGroundMaterial(layout.GroundMaterial))
				warnings.Add($"Ground: {kind} stands on material '{layout.GroundMaterial}', which no manifest defines.");
		}

		Dictionary<string, List<string>> detail = LevelTilePainter.LoadDetailManifest("res://assets/bonelight/tiles/detail/detail.json");
		foreach (string set in GroundDetail.AllSets())
		{
			if (!detail.ContainsKey(set))
				warnings.Add($"Ground: detail set '{set}' is named by GroundDetail but missing from detail.json. Re-run: python tools/art/ground_detail.py");
		}

		// The tile atlas the painter draws from (tools/art/tile_atlas.py). A tile missing from it
		// still paints - LevelTilePainter gives it a texture of its own - but costs a draw call per
		// cell again, which is the frame rate the atlas exists to save (.ai/performance.md).
		Dictionary<string, Vector2I> atlas = LevelTilePainter.LoadAtlasManifest("res://assets/bonelight/tiles/atlas.json", 48, out _);
		if (atlas.Count == 0 || !ResourceLoader.Exists("res://assets/bonelight/tiles/atlas.png"))
			warnings.Add("Ground: the tile atlas (atlas.json / atlas.png) is missing, so every tile is its own draw call. Re-run: python tools/art/tile_atlas.py");
		else
		{
			var unpacked = new List<string>();
			foreach (string path in ManifestTilePaths("res://assets/bonelight/tiles/tiles.json"))
				if (!atlas.ContainsKey(path)) unpacked.Add(path);
			foreach (List<string> variants in detail.Values)
				foreach (string path in variants)
					if (!atlas.ContainsKey(path)) unpacked.Add(path);
			if (unpacked.Count > 0)
				warnings.Add($"Ground: {unpacked.Count} tile(s) are not in the tile atlas (first: {unpacked[0]}). Re-run: python tools/art/tile_atlas.py");
		}

		// The garden: its parts, and the layout its art was drawn to against the one its fence is
		// built from. A pen drawn twelve cells wide with a fence collided at ten would put an
		// invisible rail through a flower bed and leave a drawn one you walk through.
		WorldSprites.Pen pen = WorldSprites.LoadPen();
		if (pen == null)
			warnings.Add($"Ground: {WorldSprites.PenManifest} is missing; no flower pens will be drawn. Re-run: python tools/art/ground_detail.py");
		else
		{
			foreach (string part in new[] { "back", "front", "side", "flower", "garden" })
			{
				if (!pen.Parts.TryGetValue(part, out GroundedSprite sprite) || sprite.Texture == null)
					warnings.Add($"Ground: flower pen part '{part}' is missing or not imported; no flower pens will be drawn.");
			}
			if (pen.CellsW != FlowerPenLayout.CellsW || pen.CellsH != FlowerPenLayout.CellsH
				|| pen.GateX != FlowerPenLayout.GateX || pen.GateW != FlowerPenLayout.GateW
				|| pen.Line * 2 != 48 || pen.Segment != 48)
				warnings.Add($"Ground: flower-pen.json was drawn to {pen.CellsW}x{pen.CellsH} cells, gate {pen.GateX}+{pen.GateW}, "
					+ $"but FlowerPenLayout is {FlowerPenLayout.CellsW}x{FlowerPenLayout.CellsH}, gate {FlowerPenLayout.GateX}+{FlowerPenLayout.GateW}: "
					+ "the fence would collide where it is not drawn.");
		}

		// Trees and bushes: every sprite in the manifest loads, stands inside its own picture, and a
		// tree has a trunk to stop at (a tree with no footprint is a 300 px sprite you walk through).
		foreach (GroundedSprite s in WorldSprites.LoadTrees().Values)
		{
			if (s.Texture == null)
				warnings.Add($"Ground: tree or bush '{s.Id}' is not imported; it will never be placed. Run the import.");
			if (s.Ground <= 0 || s.Ground > s.Height || s.AnchorX <= 0 || s.AnchorX >= s.Width)
				warnings.Add($"Ground: tree or bush '{s.Id}' has its ground line or trunk column outside its picture.");
			if ((s.Kind == "tree") != (s.Trunk > 0))
				warnings.Add($"Ground: '{s.Id}' is a {s.Kind} with a trunk of {s.Trunk} px: trees block at the trunk, bushes never.");
		}
	}

	// The flower pen's fence in the navigation (.ai/terrain.md section 8). The fence used to stop
	// nothing; now it stops the boots and the horde, and the only thing standing between "solid
	// fence" and "a wall the swarm wedges on" is that its cells are in the grid and its gates are not.
	// So, on a pen stamped exactly as Node2DGame.PlanChunkFeatures stamps one:
	//   - the rails are solid and stop no shot, the gates are open;
	//   - a straight walk through the fence reads as blocked, so an enemy follows the field;
	//   - the inside is reachable, and only the long way, through a gate;
	//   - an enemy beside a side rail is sent along it, not into it;
	//   - every rectangle of the rails' collision lies on fence cells, so a body is never stopped
	//     on a cell the field calls open (the rule water keeps, section 3).
	private static void ValidateFlowerPen(List<string> warnings)
	{
		const int size = 40;
		const int tile = 48;
		const int px0 = 14, py0 = 14;
		var solid = new bool[size * size];
		var wall = new bool[size * size];
		for (int y = 0; y < FlowerPenLayout.CellsH; y++)
			for (int x = 0; x < FlowerPenLayout.CellsW; x++)
				if (FlowerPenLayout.IsFenceCell(x, y))
					solid[(px0 + x) + (py0 + y) * size] = true;

		Vector2 Cell(int x, int y) => new Vector2((x + 0.5f) * tile, (y + 0.5f) * tile);
		Vector2 flower = Cell(px0 + FlowerPenLayout.CellsW / 2, py0 + FlowerPenLayout.CellsH / 2);
		int midRow = py0 + FlowerPenLayout.CellsH / 2;
		Vector2 west = Cell(px0 - 3, midRow);
		Vector2 besideRail = Cell(px0 - 1, midRow);

		var nav = new MazeNavigation(size, tile, Vector2.Zero, isMaze: false);
		nav.RebuildFromGrid(solid, wall, Vector2I.Zero, flower);

		if (!nav.IsBlockedAt(Cell(px0, midRow)) || !nav.IsBlockedAt(Cell(px0 + 2, py0)))
			warnings.Add("Navigation: a flower pen's rail is not solid in the grid - the horde would wedge on the fence.");
		if (nav.IsBlockedAt(Cell(px0 + FlowerPenLayout.GateX, py0)) || nav.IsBlockedAt(Cell(px0 + FlowerPenLayout.GateX, py0 + FlowerPenLayout.CellsH - 1)))
			warnings.Add("Navigation: a flower pen's gate is solid - the garden would be sealed.");
		if (nav.IsWallAt(Cell(px0, midRow)) || !nav.HasLineOfSight(west, flower))
			warnings.Add("Navigation: a garden fence is stopping shots - only maze walls should.");
		if (nav.HasClearWalk(west, flower))
			warnings.Add("Navigation: the straight walk through a garden fence reads as clear - enemies would never route to a gate.");
		if (!nav.IsReachable(flower) || !nav.IsReachable(west))
			warnings.Add("Navigation: the inside of a flower pen, or the ground outside it, counts as unreachable.");
		float walk = nav.PathTilesAt(west);
		float straight = west.DistanceTo(flower) / tile;
		if (walk < straight + 4f)
			warnings.Add($"Navigation: walking from outside a pen to its flower is {walk} tiles against {straight} straight - the fence is not in the field.");
		Vector2 flow = nav.FlowDirectionAt(besideRail);
		if (flow != Vector2.Zero && Mathf.Abs(flow.X) > Mathf.Abs(flow.Y) && flow.X > 0f)
			warnings.Add($"Navigation: the flow beside a pen's west rail is {flow}, into the fence rather than along it to a gate.");

		foreach (Rect2 rail in FlowerPenLayout.RailRects(tile))
		{
			foreach (Vector2 corner in new[] { rail.Position, rail.End - Vector2.One * 0.01f,
				new Vector2(rail.Position.X, rail.End.Y - 0.01f), new Vector2(rail.End.X - 0.01f, rail.Position.Y) })
			{
				int cx = Mathf.FloorToInt(corner.X / tile), cy = Mathf.FloorToInt(corner.Y / tile);
				if (!FlowerPenLayout.IsFenceCell(cx, cy))
				{
					warnings.Add($"Navigation: the flower pen's rail collision {rail} reaches cell ({cx}, {cy}), which the grid calls open.");
					break;
				}
			}
		}
	}

	// THE SEAM RULE (.ai/terrain.md, "Chunk streaming"). Endless stages are generated a chunk at a
	// time, each padded with TerrainStreamer.Margin cells of its neighbours, and the border only comes
	// out the same from both sides if that margin is wide enough for everything that reads past a
	// cell: the cleanup passes and the autotile role. Nothing would report a margin that is too
	// narrow - the game builds and runs, and there is simply a line across the world every 1536 px.
	// So: a chunk resolved with the shipping margin must match the same chunk resolved with twice
	// it, cell for cell; and two neighbours' raw grids must agree wherever they overlap.
	//
	// The maze half checks the other promise: corridors connect across chunk borders. Nine chunks
	// round the start are stitched together and flooded from the spawn point; almost all of the
	// floor must be reached inside that window alone.
	// The water's shape (WaterShape) is built per chunk from a window reaching into its neighbours,
	// and two chunks build the cells along their shared border. If those disagree, the shore steps
	// at every chunk border - and so does its collision, since the polygons are the bodies. So build
	// two neighbours on the Swamp, which has the most water, and compare every cell both build.
	private static void ValidateWaterShapes(List<string> warnings)
	{
		const ulong seed = 20261016;
		const int c = TerrainStreamer.ChunkTiles;
		const int m = TerrainStreamer.Margin;
		var generator = new LevelGenerator();
		var autotiler = new TerrainAutotiler();
		LevelLayout layout = generator.Describe(StageEnvironmentKind.Swamp);

		// Resolved water flags over chunks (-1..2, -1..1), as the streamer would hold them.
		const int x0 = -1, y0 = -1, wide = 4 * c, high = 3 * c;
		var wet = new bool[wide * high];
		for (int cy = -1; cy <= 1; cy++)
		{
			for (int cx = -1; cx <= 2; cx++)
			{
				TileResolution[,] r = autotiler.Resolve(generator.GenerateChunk(StageEnvironmentKind.Swamp, seed, cx, cy, c, m), layout.BaseTerrain);
				for (int y = 0; y < c; y++)
					for (int x = 0; x < c; x++)
						wet[(cx - x0) * c + x + ((cy - y0) * c + y) * wide] = r[x + m, y + m].Terrain == "water";
			}
		}

		WaterShape Build(Vector2I chunk)
		{
			var shape = new WaterShape(c);
			Vector2I min = shape.WindowMin(chunk * c);
			int side = shape.WindowCells;
			for (int y = 0; y < side; y++)
				for (int x = 0; x < side; x++)
				{
					int wx = min.X + x - x0 * c, wy = min.Y + y - y0 * c;
					shape.WetBuffer[x + y * side] = wx >= 0 && wy >= 0 && wx < wide && wy < high && wet[wx + wy * wide];
				}
			shape.Build(chunk * c, seed);
			return shape;
		}

		WaterShape left = Build(new Vector2I(0, 0));
		WaterShape right = Build(new Vector2I(1, 0));
		int compared = 0, differ = 0, wetEmpty = 0;
		for (int y = -WaterShape.ShapeRing; y < c + WaterShape.ShapeRing; y++)
		{
			for (int x = c - WaterShape.ShapeRing; x < c + WaterShape.ShapeRing; x++)
			{
				int a = left.KindAt(x, y), b = right.KindAt(x, y);
				compared++;
				if (a != b) { differ++; continue; }
				if (a != 2) continue;
				ReadOnlySpan<Vector2> pa = left.PolygonAt(x, y), pb = right.PolygonAt(x, y);
				bool same = pa.Length == pb.Length;
				for (int i = 0; same && i < pa.Length; i++)
					same = pa[i].DistanceSquaredTo(pb[i]) < 1e-4f;
				if (!same) differ++;
			}
		}
		for (int y = 0; y < c; y++)
			for (int x = 0; x < c; x++)
				if (left.KindAt(x, y) == 3) wetEmpty++;
		if (differ > 0)
			warnings.Add($"Water: chunks (0, 0) and (1, 0) build {differ} of {compared} shared border cells differently, so "
				+ "the shore and its collision step at every chunk border. WaterShape.WindowMargin is too narrow, or something "
				+ "in it reads the chunk rather than the world.");
		if (wetEmpty > 0)
			warnings.Add($"Water: {wetEmpty} water cells of chunk (0, 0) come out with no water drawn in them at all. The "
				+ "blocked grid still calls them water, so they are an invisible no-go square for the flow field.");
	}

	private static void ValidateTerrainSeams(List<string> warnings)
	{
		const ulong seed = 20261016;
		const int c = TerrainStreamer.ChunkTiles;
		const int m = TerrainStreamer.Margin;
		var generator = new LevelGenerator();
		var autotiler = new TerrainAutotiler();

		foreach (StageEnvironmentKind kind in new[] { StageEnvironmentKind.Swamp, StageEnvironmentKind.Volcanic })
		{
			LevelLayout layout = generator.Describe(kind);
			foreach ((int cx, int cy) in new[] { (0, 0), (3, -2) })
			{
				TileResolution[,] narrow = autotiler.Resolve(generator.GenerateChunk(kind, seed, cx, cy, c, m), layout.BaseTerrain);
				TileResolution[,] wide = autotiler.Resolve(generator.GenerateChunk(kind, seed, cx, cy, c, m * 2), layout.BaseTerrain);
				int differ = 0;
				for (int x = 0; x < c; x++)
					for (int y = 0; y < c; y++)
						if (narrow[x + m, y + m].Terrain != wide[x + m * 2, y + m * 2].Terrain
							|| narrow[x + m, y + m].Role != wide[x + m * 2, y + m * 2].Role)
							differ++;
				if (differ > 0)
					warnings.Add($"Terrain: {kind} chunk ({cx}, {cy}) resolves {differ} cells differently with a wider "
						+ "margin, so chunk borders will seam. TerrainStreamer.Margin is too narrow for the cleanup passes.");
			}

			// Raw grids of two horizontal neighbours, over the cells both of them generate.
			string[,] left = generator.GenerateChunk(kind, seed, 0, 0, c, m);
			string[,] right = generator.GenerateChunk(kind, seed, 1, 0, c, m);
			int rawDiffer = 0;
			for (int wx = c - m; wx < c + m; wx++)
				for (int y = 0; y < c + 2 * m; y++)
					if (left[wx + m, y] != right[wx - c + m, y])
						rawDiffer++;
			if (rawDiffer > 0)
				warnings.Add($"Terrain: {kind} chunks (0, 0) and (1, 0) disagree on {rawDiffer} raw cells they share. "
					+ "Something in LevelGenerator draws from an order-dependent RNG instead of a hash of the tile.");
		}

		LevelLayout maze = generator.Describe(StageEnvironmentKind.Castle);
		if (maze.Topology != StageTopology.Maze)
			return;
		const int span = 3 * c;
		var open = new bool[span, span];
		for (int cy = -1; cy <= 1; cy++)
		{
			for (int cx = -1; cx <= 1; cx++)
			{
				string[,] g = generator.GenerateChunk(StageEnvironmentKind.Castle, seed, cx, cy, c, m);
				for (int x = 0; x < c; x++)
					for (int y = 0; y < c; y++)
						open[(cx + 1) * c + x, (cy + 1) * c + y] = g[x + m, y + m] == maze.BaseTerrain;
			}
		}
		int total = 0;
		foreach (bool o in open) if (o) total++;
		var seen = new bool[span, span];
		var stack = new Stack<(int, int)>();
		stack.Push((c, c));
		seen[c, c] = true;
		int reached = 0;
		while (stack.Count > 0)
		{
			(int x, int y) = stack.Pop();
			reached++;
			foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
			{
				int nx = x + dx, ny = y + dy;
				if (nx < 0 || ny < 0 || nx >= span || ny >= span || seen[nx, ny] || !open[nx, ny]) continue;
				seen[nx, ny] = true;
				stack.Push((nx, ny));
			}
		}
		if (!open[c, c] || reached < total * 0.9f)
			warnings.Add($"Terrain: the maze round the start reaches only {reached} of {total} floor cells across nine chunks; "
				+ "corridors are not connecting across chunk borders (LevelGenerator.DoorMask).");
	}

	// The input map lives in project.godot as a wall of serialised event objects, which is the one
	// place in this project where a merge can silently delete a feature: drop the four lines that
	// carry the joypad and the game still builds, still runs, and simply cannot be played with a
	// controller. Nothing else would report it, because there is no controller in a headless run.
	//
	// So this checks the SHAPE of the map rather than the bindings themselves - that each action
	// exists, and that the ones a pad needs actually carry a pad event.
	private static void ValidateInputMap(List<string> warnings)
	{
		// Every action any script reads by name. A typo here is a silent no-op at runtime, because
		// Input.IsActionPressed on an unknown action just returns false.
		string[] required =
		{
			"ui_accept", "ui_cancel", "pause",
			"ui_up", "ui_down", "ui_left", "ui_right",
			"move_up", "move_down", "move_left", "move_right",
		};

		foreach (string action in required)
		{
			if (!InputMap.HasAction(action))
				warnings.Add($"Input: action '{action}' is missing from project.godot; everything that reads it is dead.");
		}

		// A is select, Start is pause, and the left stick both moves and scrolls. Each of those is
		// one event in project.godot and none of them is visible in any C# file.
		RequireJoypadButton(warnings, "ui_accept", JoyButton.A, "A cannot press a menu option");
		RequireJoypadButton(warnings, "ui_cancel", JoyButton.Start, "Start cannot back out of a menu");
		RequireJoypadButton(warnings, "pause", JoyButton.Start, "Start cannot pause a run");

		RequireJoypadAxis(warnings, "move_up", JoyAxis.LeftY, "the left stick cannot move the player");
		RequireJoypadAxis(warnings, "move_left", JoyAxis.LeftX, "the left stick cannot move the player");
		RequireJoypadAxis(warnings, "ui_down", JoyAxis.LeftY, "the left stick cannot scroll a menu");
		RequireJoypadAxis(warnings, "ui_right", JoyAxis.LeftX, "the left stick cannot scroll a menu");

		// Movement reads move_* precisely because ui_* has to be deaf to a thumb resting on the
		// stick. If the two deadzones ever converge, one of the two jobs is being done wrongly.
		if (InputMap.HasAction("ui_down") && InputMap.HasAction("move_down")
			&& InputMap.ActionGetDeadzone("move_down") >= InputMap.ActionGetDeadzone("ui_down"))
		{
			warnings.Add("Input: move_* deadzone is no longer below ui_*'s. Movement then throws away "
				+ "the first half of the stick, which is the reason the two action sets are separate.");
		}
	}

	private static void RequireJoypadButton(List<string> warnings, string action, JoyButton button, string consequence)
	{
		if (!InputMap.HasAction(action))
			return;

		bool found = InputMap.ActionGetEvents(action)
			.Any(e => e is InputEventJoypadButton pad && pad.ButtonIndex == button);

		if (!found)
			warnings.Add($"Input: '{action}' has no {button} button event - {consequence}.");
	}

	private static void RequireJoypadAxis(List<string> warnings, string action, JoyAxis axis, string consequence)
	{
		if (!InputMap.HasAction(action))
			return;

		bool found = InputMap.ActionGetEvents(action)
			.Any(e => e is InputEventJoypadMotion motion && motion.Axis == axis);

		if (!found)
			warnings.Add($"Input: '{action}' has no {axis} motion event - {consequence}.");
	}

	// Music fails the same silent way, plus one of its own: a WAV whose .import does not say
	// edit/loop_mode=2 (Forward) plays its minute once and stops, which in a fifteen-minute run is
	// fourteen minutes of nothing. That setting lives in the .import file, not the WAV, so deleting
	// the .import loses it - the comparison is against LoopModeEnum.Disabled to catch exactly that.
	// The stems must also be one length, or AudioStreamSynchronized drifts them apart a loop at a time.
	private static void ValidateMusic(List<string> warnings)
	{
		foreach (string path in MusicCatalog.AllTracks())
		{
			if (!ResourceLoader.Exists(path))
			{
				warnings.Add($"Music: missing '{path}'. Run: python tools/audio/music.py");
				continue;
			}
			if (ResourceLoader.Load<AudioStream>(path) is AudioStreamWav wav
				&& wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled)
			{
				warnings.Add($"Music: '{path}' does not loop. Set edit/loop_mode=2 in its .import.");
			}
		}

		// Two stage rosters must agree (MusicCatalog's note says why it is not yet folded into
		// StageCatalog). A chapter with no row plays the Forest - audible, but in the wrong key -
		// and a row for a chapter that does not exist is a renumbering nobody followed through.
		var stageIndices = new HashSet<int>(StageCatalog.All.Select(s => s.Index));
		foreach (StageDefinition stage in StageCatalog.All)
		{
			if (!MusicCatalog.HasSetForStage(stage.Index))
				warnings.Add($"Music: chapter {stage.Index} ({stage.DisplayName}) has no set in MusicCatalog.PerStage and plays '{MusicCatalog.DefaultRunSet.Id}'.");
		}
		foreach (int index in MusicCatalog.StageIndices)
		{
			if (!stageIndices.Contains(index))
				warnings.Add($"Music: MusicCatalog.PerStage has a set for stage {index}, which StageCatalog does not list.");
		}

		foreach (MusicSet set in MusicCatalog.AllSets())
		{
			double length = -1.0;
			foreach (string path in set.Stems)
			{
				if (!ResourceLoader.Exists(path) || ResourceLoader.Load<AudioStream>(path) is not AudioStream stream)
					continue;
				double l = stream.GetLength();
				if (length >= 0.0 && Math.Abs(l - length) > 0.001)
					warnings.Add($"Music: the '{set.Id}' stems differ in length ({length:0.000} s and {l:0.000} s at {path}); they will drift apart.");
				length = l;
			}
		}
	}

	// Sound is the one system where a missing asset is not a crash and not a visible glitch - it
	// is silence, which looks exactly like a design decision. So the whole catalog is checked at
	// startup rather than discovered one absent hit at a time.
	private static void ValidateAudio(List<string> warnings)
	{
		int missing = 0;
		foreach (string name in SfxCatalog.AllNames)
		{
			string path = SfxCatalog.PathFor(name);
			if (ResourceLoader.Exists(path))
				continue;
			missing++;
			if (missing <= 5)
				warnings.Add($"Audio: missing sound '{name}' (expected {path}). Run: python tools/audio/build.py");
		}
		if (missing > 5)
			warnings.Add($"Audio: {missing - 5} further sounds missing from {SfxCatalog.Directory}");

		// A missing bus is not fatal - MusicPlayer and SfxPlayer both fall back to Master - but it
		// silently collapses the volume sliders onto one control, which is the bug that shipped
		// before default_bus_layout.tres existed.
		if (AudioServer.GetBusIndex(MusicPlayer.MusicBusName) < 0)
			warnings.Add("Audio: no 'Music' bus; the music slider is falling back to Master and now duplicates it.");
		if (AudioServer.GetBusIndex(SfxPlayer.SfxBusName) < 0)
			warnings.Add("Audio: no 'SFX' bus; the effects slider is falling back to Master and now duplicates it.");

		// The duck lowers SFX_Bed and leaves SFX_Priority alone. Without both, every sound lands
		// on one bus, the duck would fight the volume slider, and a critical sound would duck
		// itself - so the whole thing degrades to no ducking at all, silently.
		if (AudioServer.GetBusIndex(SfxPlayer.BedBusName) < 0)
			warnings.Add($"Audio: no '{SfxPlayer.BedBusName}' bus; ducking is disabled.");
		if (AudioServer.GetBusIndex(SfxPlayer.PriorityBusName) < 0)
			warnings.Add($"Audio: no '{SfxPlayer.PriorityBusName}' bus; critical sounds will duck themselves.");

		int sfxBus = AudioServer.GetBusIndex(SfxPlayer.SfxBusName);
		if (sfxBus >= 0 && AudioServer.GetBusEffectCount(sfxBus) == 0)
		{
			// tools/audio/mixsim.py measured the summed mix peaking at +1.5 to +2.5 dBFS in every
			// scenario, including the sparse early game. Without a limiter here it clips.
			warnings.Add("Audio: the SFX bus has no limiter; the summed mix clips in every measured scenario.");
		}

		// The tiers only work because Critical is rare. Promote enough sounds into it and the
		// duck never releases, the priority bus stops meaning anything, and the mix pumps.
		int critical = 0;
		foreach (string name in SfxCatalog.AllNames)
		{
			if (SfxCatalog.TierOf(name) == SfxCatalog.SfxTier.Critical)
				critical++;
		}
		if (critical > MaxCriticalSounds)
		{
			warnings.Add($"Audio: {critical} sounds are tier Critical (limit {MaxCriticalSounds}). "
				+ "Critical ducks everything else and is never stolen; it has to stay rare to mean anything.");
		}
	}

	// Ten today. The ceiling is deliberately close to that number so raising it is a decision
	// somebody makes on purpose rather than a threshold nobody notices drifting.
	private const int MaxCriticalSounds = 14;


	// The stage list is the only screen that says what a chapter is and what the dark wizard has
	// done to it, and a blank string there is invisible to the compiler: the card simply renders
	// one line shorter. Index/id agreement matters more - Global.SelectedStageIdx is an index into
	// this array, while unlocks are recorded against the id, so a mismatch unlocks the wrong place.
	private static void ValidateStageCatalog(List<string> warnings)
	{
		var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		for (int i = 0; i < StageCatalog.All.Count; i++)
		{
			StageDefinition stage = StageCatalog.All[i];

			if (stage.Index != i)
				warnings.Add($"Stage '{stage.Id}' sits at position {i} but declares Index {stage.Index}; a selection would enter a different chapter than the card clicked.");

			if (!string.Equals(stage.Id, $"stage_{i}", StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Stage at position {i} has id '{stage.Id}', not 'stage_{i}'; boss unlocks and save entries match on that id.");

			if (!seenIds.Add(stage.Id))
				warnings.Add($"Stage id '{stage.Id}' appears more than once, so one of the two can never be unlocked.");

			if (string.IsNullOrWhiteSpace(stage.DisplayName))
				warnings.Add($"Stage '{stage.Id}' has no DisplayName, so its card and the game-over screen would both be blank.");

			if (string.IsNullOrWhiteSpace(stage.FlavorText))
				warnings.Add($"Stage '{stage.Id}' has no FlavorText, so its card and the in-run intro banner say nothing about the place.");

			// Exempt the one chapter that is not somewhere he has been. CorruptionText exists to
			// say what the dark wizard has done to a place, and demanding it of a chapter outside
			// the campaign would mean inventing fiction to satisfy a check.
			if (string.IsNullOrWhiteSpace(stage.CorruptionText)
				&& stage.EnvironmentKind != StageEnvironmentKind.Sketchbook)
			{
				warnings.Add($"Stage '{stage.Id}' has no CorruptionText, so its card never says how the dark wizard holds it.");
			}

			if (!stage.IsPlayable && string.IsNullOrWhiteSpace(stage.LockedHint) && stage.Gate != StageGate.CampaignComplete)
				warnings.Add($"Stage '{stage.Id}' is not playable and has no LockedHint, so its card locks with no explanation.");
		}
	}

	// The recurring miniboss. Node2DGame loads it by path on a timer and returns quietly when the
	// load fails, so every way this breaks produces the same symptom: the miniboss never arrives,
	// which reads as a tuning decision rather than as a broken reference. Checked here for the
	// same reason the ranged enemies are - a dotnet build sees none of it.
	private const string MiniBossScenePath = "res://scenes/WardenEnemy.tscn";

	private static void ValidateMiniBoss(List<string> warnings)
	{
		if (!ResourceLoader.Exists(MiniBossScenePath))
		{
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' is missing, so the recurring miniboss would never spawn.");
			return;
		}

		var scene = GD.Load<PackedScene>(MiniBossScenePath);
		if (scene == null)
		{
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' failed to load, so the recurring miniboss would never spawn.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not Enemy miniBoss)
		{
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' has no Enemy script attached.");
			probe?.Free();
			return;
		}

		// IsMiniBoss is what the spawn tick counts to keep one on screen at a time, and it is also
		// what the reward and telemetry paths read. Off, this is an ordinary enemy with 220 HP.
		if (!miniBoss.IsMiniBoss)
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' does not have IsMiniBoss set, so it would spawn as an ordinary enemy and could stack.");

		var sprite = miniBoss.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite?.SpriteFrames == null)
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' has no SpriteFrames, so it would arrive invisible.");
		else if (!sprite.SpriteFrames.HasAnimation("moving"))
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' has no 'moving' animation, so it would stand still while it chases.");

		miniBoss.Free();
	}

	// Scenes carrying a RangedEnemy script. There is no catalog for ordinary enemies - Node2DGame
	// holds them as fields - so this list is maintained by hand the same way SpellResourcePaths is.
	private static readonly string[] RangedEnemyScenePaths =
	{
		"res://scenes/HexerEnemy.tscn",
		"res://scenes/ForestWisp.tscn",
		"res://scenes/SkullSentry.tscn",
	};

	// A ranged enemy is the first one whose behaviour depends on a second scene resolving at
	// runtime. Every way this breaks is silent: a missing bolt scene, or a wind-up of zero, both
	// produce an enemy that still walks and still looks fine, so nobody finds out until a player
	// wonders why the caster never does anything - or takes a hit with no tell at all. A dotnet
	// build sees none of it.
	private static void ValidateRangedEnemies(List<string> warnings)
	{
		foreach (string path in RangedEnemyScenePaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				warnings.Add($"Ranged enemy scene '{path}' is missing, so it would never spawn.");
				continue;
			}

			var scene = GD.Load<PackedScene>(path);
			if (scene == null)
			{
				warnings.Add($"Ranged enemy scene '{path}' failed to load.");
				continue;
			}

			Node probe = scene.Instantiate();
			if (probe is not RangedEnemy ranged)
			{
				warnings.Add($"Ranged enemy scene '{path}' has no RangedEnemy script attached, so it would chase and melee like every other enemy.");
				probe?.Free();
				continue;
			}

			// The three ranges only mean anything as an ordered set. Out of order, the enemy either
			// backs away from a spot it is also trying to reach, or holds station outside the range
			// it can shoot from - both read as an enemy that has lost interest in the fight.
			if (ranged.RetreatDistance >= ranged.StandoffDistance || ranged.StandoffDistance >= ranged.AttackRange)
			{
				warnings.Add($"Ranged enemy '{path}' has ranges out of order (retreat {ranged.RetreatDistance}, standoff {ranged.StandoffDistance}, attack {ranged.AttackRange}); they must increase in that order.");
			}

			if (ranged.CastWindUpSeconds <= 0f)
				warnings.Add($"Ranged enemy '{path}' has no cast wind-up, so its shot arrives with no tell the player can read.");

			if (ranged.BoltsPerVolley < 1)
				warnings.Add($"Ranged enemy '{path}' fires {ranged.BoltsPerVolley} bolts per volley, so it would telegraph and shoot nothing.");

			// Stacked bolts are the one way a volley can be dishonest: several bolts on the exact
			// same heading look like one shot, land as one hit, and quietly multiply its damage.
			if (ranged.BoltsPerVolley > 1 && ranged.VolleySpreadDegrees <= 0f)
				warnings.Add($"Ranged enemy '{path}' fires {ranged.BoltsPerVolley} bolts with no spread, so they overlap into what looks like a single shot dealing several times its listed damage.");

			ValidateEnemyProjectile(warnings, path, ranged.ProjectileScenePath);
			ranged.Free();
		}
	}

	// The four attack-pattern enemies (issue #33). Every one of them promises the player something
	// before it hits: a streak along the line it will charge, a ring where the ground is about to
	// be hit, a ring where minions are about to stand. A wind-up of zero silently turns any of
	// those promises into damage that simply arrives, and the enemy still walks around looking
	// perfectly fine - so nothing but a check like this one catches it. A dotnet build sees none
	// of it: the scenes and their exported tuning only exist in Godot.
	private static readonly string[] AttackPatternEnemyScenePaths =
	{
		"res://scenes/LungerEnemy.tscn",
		"res://scenes/SlammerEnemy.tscn",
		"res://scenes/ExploderEnemy.tscn",
		"res://scenes/SummonerEnemy.tscn",
		// The Still Warden guard is a SlammerEnemy with the telegraph shape flipped to a cone,
		// so it validates through exactly the same path and needs no case of its own.
		"res://scenes/RimeGuard.tscn",
		// The Sketchbook's ghost (Elim's Boo): a wail ring and a phase that marks where it re-forms.
		"res://scenes/BooEnemy.tscn",
	};

	private static void ValidateAttackPatternEnemies(List<string> warnings)
	{
		foreach (string path in AttackPatternEnemyScenePaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				warnings.Add($"Attack-pattern enemy scene '{path}' is missing, so it would never spawn.");
				continue;
			}

			var scene = GD.Load<PackedScene>(path);
			if (scene == null)
			{
				warnings.Add($"Attack-pattern enemy scene '{path}' failed to load.");
				continue;
			}

			Node probe = scene.Instantiate();
			switch (probe)
			{
				case LungerEnemy lunger:
					ValidateLunger(warnings, path, lunger);
					break;
				case SlammerEnemy slammer:
					ValidateSlammer(warnings, path, slammer);
					break;
				case ExploderEnemy exploder:
					ValidateExploder(warnings, path, exploder);
					break;
				case SummonerEnemy summoner:
					ValidateSummoner(warnings, path, summoner);
					break;
				case BooEnemy ghost:
					ValidateGhost(warnings, path, ghost);
					break;
				default:
					warnings.Add($"Attack-pattern enemy scene '{path}' has no attack-pattern script attached, so it would chase and melee like every other enemy.");
					break;
			}

			probe?.Free();
		}
	}

	private static void ValidateLunger(List<string> warnings, string path, LungerEnemy lunger)
	{
		if (lunger.LungeWindUpSeconds <= 0f)
			warnings.Add($"Lunger '{path}' has no wind-up, so its charge starts with no tell the player can read.");

		// A "dash" at or below walking pace is just a pause followed by more walking, and the
		// streak drawn during the wind-up would promise a reach it never covers.
		if (lunger.LungeSpeedMultiplier <= 1f)
			warnings.Add($"Lunger '{path}' has LungeSpeedMultiplier {lunger.LungeSpeedMultiplier}, so its charge is no faster than its walk.");

		if (lunger.MinimumLungeDistance >= lunger.LungeRange)
			warnings.Add($"Lunger '{path}' can never charge: MinimumLungeDistance {lunger.MinimumLungeDistance} is not below LungeRange {lunger.LungeRange}.");

		// The recovery is the reward for dodging. Without it the charge has no cost and the enemy
		// is simply faster than the player with extra steps.
		if (lunger.RecoverySeconds <= 0f)
			warnings.Add($"Lunger '{path}' has no recovery window, so reading and dodging its charge gains the player nothing.");
	}

	private static void ValidateSlammer(List<string> warnings, string path, SlammerEnemy slammer)
	{
		if (slammer.SlamTelegraphSeconds <= 0f)
			warnings.Add($"Slammer '{path}' has no telegraph, so its slam lands with no warning ring.");

		if (slammer.SlamRadius <= 0f)
			warnings.Add($"Slammer '{path}' has a slam radius of {slammer.SlamRadius}, so it would draw a ring and hit nothing.");

		if (slammer.SlamDamage <= 0)
			warnings.Add($"Slammer '{path}' deals {slammer.SlamDamage} slam damage, so its whole attack is decorative.");

		// Below 1 it only commits once the player is already inside the ring, which spends the
		// wind-up warning them about a hit they were given no room to avoid.
		if (slammer.SlamEngageRadiusMultiplier < 1f)
			warnings.Add($"Slammer '{path}' engages at {slammer.SlamEngageRadiusMultiplier}x its radius; below 1 the player is inside the ring before the tell begins.");

		// A cone is only worth having because it can be stepped out of sideways. At 90 degrees or
		// more it is a half-plane, there is no sideways left, and it is a worse ring.
		if (slammer.SlamShape == TelegraphShape.Cone
			&& (slammer.SlamHalfAngleDegrees <= 0f || slammer.SlamHalfAngleDegrees >= 90f))
		{
			warnings.Add($"Slammer '{path}' has a cone half-angle of {slammer.SlamHalfAngleDegrees} degrees; outside 0-90 it is either nothing or a half-plane the player cannot step out of.");
		}
	}

	private static void ValidateExploder(List<string> warnings, string path, ExploderEnemy exploder)
	{
		if (exploder.FuseSeconds <= 0f)
			warnings.Add($"Exploder '{path}' has no fuse, so it detonates on arrival with no chance to kill it or step away.");

		if (exploder.BlastRadius <= 0f)
			warnings.Add($"Exploder '{path}' has a blast radius of {exploder.BlastRadius}, so it would detonate harmlessly.");

		if (exploder.BlastDamage <= 0)
			warnings.Add($"Exploder '{path}' deals {exploder.BlastDamage} blast damage, so nothing is at stake in letting it reach you.");
	}

	private static void ValidateGhost(List<string> warnings, string path, BooEnemy ghost)
	{
		if (ghost.WailWindUpSeconds <= 0f)
			warnings.Add($"Ghost '{path}' has no wail wind-up, so its ring lands with no warning.");
		if (ghost.PhaseTellSeconds <= 0f)
			warnings.Add($"Ghost '{path}' has no phase tell, so the ring it places where it re-forms lands unseen.");
		if (ghost.WailRadius <= 0f || ghost.WailDamage <= 0)
			warnings.Add($"Ghost '{path}' wails at radius {ghost.WailRadius} for {ghost.WailDamage}, so the wail is decorative.");
		if (ghost.PhaseRadius <= 0f || ghost.PhaseDamage <= 0)
			warnings.Add($"Ghost '{path}' phases at radius {ghost.PhaseRadius} for {ghost.PhaseDamage}, so its mark hits nothing.");
		if (ghost.WailEngageRadiusMultiplier < 1f)
			warnings.Add($"Ghost '{path}' engages at {ghost.WailEngageRadiusMultiplier}x its wail radius; below 1 the player is inside the ring before the tell begins.");
		// The same rule as the bosses' burrows: the mark must land before the ghost re-forms on it,
		// or its arrival is itself an untelegraphed hit and the ring a lie about when it is safe.
		if (ghost.PhaseTravelSeconds <= ghost.PhaseTellSeconds)
			warnings.Add($"Ghost '{path}' crosses in {ghost.PhaseTravelSeconds}s but its mark takes {ghost.PhaseTellSeconds}s; it would re-form before its own warning lands.");
		if (ghost.PhaseMinDistance >= ghost.PhaseMaxDistance)
			warnings.Add($"Ghost '{path}' can never phase: PhaseMinDistance {ghost.PhaseMinDistance} is not below PhaseMaxDistance {ghost.PhaseMaxDistance}.");
		if (!ghost.IsMiniBoss)
			warnings.Add($"Ghost '{path}' is not IsMiniBoss, so it would be recycled to the spawn ring mid-fight, shoved by the player and pay ordinary XP.");
	}

	private static void ValidateSummoner(List<string> warnings, string path, SummonerEnemy summoner)
	{
		if (summoner.SummonWindUpSeconds <= 0f)
			warnings.Add($"Summoner '{path}' has no wind-up, so minions appear with no ring showing where.");

		if (summoner.MaxActiveMinions < 1 || summoner.MinionsPerSummon < 1)
			warnings.Add($"Summoner '{path}' is capped at {summoner.MaxActiveMinions} active / {summoner.MinionsPerSummon} per summon, so it would telegraph and summon nothing.");

		// The lifetime cap is the anti-farming bound. Set below the live cap it would stop the
		// summoner before it ever fills the arena once, which is not what either cap is for.
		if (summoner.MaxTotalSummons < summoner.MaxActiveMinions)
			warnings.Add($"Summoner '{path}' has MaxTotalSummons {summoner.MaxTotalSummons} below MaxActiveMinions {summoner.MaxActiveMinions}, so it can never reach its own live cap.");

		if (summoner.RetreatDistance >= summoner.StandoffDistance)
			warnings.Add($"Summoner '{path}' has ranges out of order (retreat {summoner.RetreatDistance}, standoff {summoner.StandoffDistance}); retreat must be the smaller.");

		if (summoner.MinionHealthFraction <= 0f || summoner.MinionHealthFraction > 1f)
			warnings.Add($"Summoner '{path}' has MinionHealthFraction {summoner.MinionHealthFraction}; it must be above 0 and at most 1, or minions are free kills or tougher than their summoner.");

		ValidateSummonerMinion(warnings, path, summoner.MinionScenePath);
	}

	private static void ValidateSummonerMinion(List<string> warnings, string ownerPath, string minionPath)
	{
		if (string.IsNullOrWhiteSpace(minionPath) || !ResourceLoader.Exists(minionPath))
		{
			warnings.Add($"Summoner '{ownerPath}' points at a missing minion scene: '{minionPath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(minionPath);
		if (scene == null)
		{
			warnings.Add($"Minion scene '{minionPath}' failed to load, so '{ownerPath}' would telegraph and summon nothing.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not Enemy)
		{
			warnings.Add($"Minion scene '{minionPath}' has no Enemy script attached, so '{ownerPath}' cannot set its health or track it.");
		}
		else if (probe is SummonerEnemy)
		{
			// Each generation carries its own MaxTotalSummons, so summoners summoning summoners is
			// bounded but grows exponentially - and every one of them is another live tell to draw.
			warnings.Add($"Summoner '{ownerPath}' summons another summoner ('{minionPath}'), which multiplies across generations.");
		}

		probe?.Free();
	}

	private static void ValidateEnemyProjectile(List<string> warnings, string ownerPath, string projectilePath)
	{
		if (string.IsNullOrWhiteSpace(projectilePath) || !ResourceLoader.Exists(projectilePath))
		{
			warnings.Add($"Ranged enemy '{ownerPath}' points at a missing projectile scene: '{projectilePath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(projectilePath);
		if (scene == null)
		{
			warnings.Add($"Projectile scene '{projectilePath}' failed to load, so '{ownerPath}' would telegraph and then fire nothing.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not EnemyProjectile bolt)
		{
			warnings.Add($"Projectile scene '{projectilePath}' has no EnemyProjectile script attached.");
			probe?.Free();
			return;
		}

		// The bolt finds the player through its collision shape; without one it passes straight
		// through and the caster is decorative.
		if (bolt.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null)
			warnings.Add($"Projectile scene '{projectilePath}' has no CollisionShape2D, so its bolts would pass through the player.");

		bolt.Free();
	}

	// A boss is the only way to win a level, so a broken entry silently costs the player the ending
	// of that stage - the run would just keep going past the timer with nothing to kill. None of
	// this is checkable from a dotnet build: the scene path and its script only resolve in Godot.
	private static void ValidateBossCatalog(List<string> warnings)
	{
		foreach (BossDefinition boss in BossCatalog.All)
		{
			int stageIndex = BossCatalog.StageIndexOf(boss);

			if (string.IsNullOrWhiteSpace(boss.Id))
				warnings.Add($"Boss for stage {stageIndex} has no Id, so its victory cannot be recorded or matched to an achievement.");

			ValidateBossScene(warnings, boss);

			if (boss.Health <= 0)
				warnings.Add($"Boss '{boss.Id}' has non-positive Health, so it would die on spawn.");

			// Boss ids are matched exactly by the achievements now, so a typo on either side is a
			// boss whose kill silently grants nothing. The probe records the win into a throwaway
			// save's lifetime stats, which is the same path a real victory takes.
			// A boon counts, and so does currency. The check is "does beating this pay out",
			// not "does it pay out a spell": two chapter clears pay in a boon because the shop
			// and the achievement track draw on one finite pool, and the Sketchbook pays in
			// Arcane Energy because it is a bonus chapter and must not hold campaign content.
			if (!string.IsNullOrWhiteSpace(boss.Id)
				&& !AchievementDefinitions.All.Any(a =>
					(!string.IsNullOrWhiteSpace(a.SpellUnlockId)
						|| !string.IsNullOrWhiteSpace(a.BoonUnlockId)
						|| !string.IsNullOrWhiteSpace(a.CharacterUnlockId)
						|| a.CurrencyReward > 0)
					&& a.IsComplete(BuildVictoryProbe(boss.Id))))
			{
				warnings.Add($"Boss '{boss.Id}' matches no achievement, so defeating it pays nothing at all.");
			}

			if (!string.IsNullOrWhiteSpace(boss.UnlocksStageId) && !boss.UnlocksStageId.StartsWith("stage_", StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Boss '{boss.Id}' unlocks '{boss.UnlocksStageId}', which is not a stage id of the form 'stage_N'.");
		}
	}

	// The tuning a boss fight is actually made of, none of which a dotnet build can see: every
	// number below lives in a .tscn and only resolves in Godot.
	//
	// The failures these catch all look like nothing while they are happening. A wind-up of zero
	// is an attack that simply arrives, and the boss still walks around looking perfectly fine. A
	// charge tell of 1.0 is a boss that plants for its whole wind-up and then never moves, which
	// reads as a boss that has stopped working. A burrow shorter than the tell it places means
	// the boss surfaces before its own warning resolves and the warning lands behind it.
	private static void ValidateBossPatterns(List<string> warnings)
	{
		foreach (BossDefinition boss in BossCatalog.All)
		{
			if (string.IsNullOrWhiteSpace(boss.ScenePath) || !ResourceLoader.Exists(boss.ScenePath))
				continue;

			var scene = GD.Load<PackedScene>(boss.ScenePath);
			if (scene?.Instantiate() is not BossEnemy probe)
				continue;

			// THE ONE RULE EVERY BOSS SHARES. Everything below it is per-fight.
			if (probe.SlamTelegraphSeconds <= 0f)
				warnings.Add($"Boss '{boss.Id}' has no telegraph, so its attack lands with no warning at all.");

			if (probe.SlamDamage <= 0)
				warnings.Add($"Boss '{boss.Id}' deals {probe.SlamDamage} attack damage, so its whole telegraphed attack is decorative.");

			if (probe.SlamRadius <= 0f)
				warnings.Add($"Boss '{boss.Id}' has a reach of {probe.SlamRadius}, so it would draw a warning and hit nothing.");

			if (probe.EnrageHealthFraction <= 0f || probe.EnrageHealthFraction >= 1f)
				warnings.Add($"Boss '{boss.Id}' has EnrageHealthFraction {probe.EnrageHealthFraction}; outside 0-1 the fight either never escalates or opens enraged.");

			ValidateBossPattern(warnings, probe);
			probe.Free();
		}
	}

	// Split out only because the switch is long. Everything in it is one boss idea plus the
	// number that would silently switch that idea off.
	private static void ValidateBossPattern(List<string> warnings, BossEnemy probe)
	{
		switch (probe)
		{
			case GaolerBoss gaoler:
				// Below 0 there is no planted tell before the charge; at 1 the charge never
				// happens, because the wind-up ends before it starts moving.
				if (gaoler.ChargeTellFraction <= 0f || gaoler.ChargeTellFraction >= 1f)
					warnings.Add($"Gaoler has ChargeTellFraction {gaoler.ChargeTellFraction}; at 0 it charges with no tell and at 1 it never charges at all.");
				if (gaoler.ChargeSpeedMultiplier <= 1f)
					warnings.Add($"Gaoler charges at {gaoler.ChargeSpeedMultiplier}x its walk, so the lane it draws promises a reach it never covers.");
				if (gaoler.DismountHealthFraction <= probe.EnrageHealthFraction)
					warnings.Add($"Gaoler dismounts at {gaoler.DismountHealthFraction} and enrages at {probe.EnrageHealthFraction}; the dismount has to come first or phase two is skipped.");
				break;

			case HollowChoirBoss choir:
				if (choir.VoiceCount < 2)
					warnings.Add($"Hollow Choir has {choir.VoiceCount} voices, so nothing can revive anything and the linked-health fight is an ordinary boss.");
				if (choir.ReviveSeconds <= 0f)
					warnings.Add("Hollow Choir revives instantly, so the window to drop all three at once does not exist.");
				if (choir.ReviveHealthFraction <= 0f || choir.ReviveHealthFraction >= 1f)
					warnings.Add($"Hollow Choir revives at {choir.ReviveHealthFraction} health; at or above 1 the fight cannot end.");
				break;

			case MotherRotBoss rot:
				if (rot.RegenPerSecond <= 0f)
					warnings.Add("Mother Rot does not regenerate, so killing her spawn achieves nothing and the whole decision the fight poses is gone.");
				if (!ResourceLoader.Exists(rot.SpawnScenePath))
					warnings.Add($"Mother Rot spawn scene '{rot.SpawnScenePath}' is missing, so she would never split.");
				if (rot.MaxLiveSpawn < rot.SpawnPerSplit)
					warnings.Add($"Mother Rot caps live spawn at {rot.MaxLiveSpawn} but sheds {rot.SpawnPerSplit} at a time, so a split can silently produce nothing.");
				break;

			case ArchivistBoss archivist:
				if (archivist.SweepDegreesPerSecond <= 0f)
					warnings.Add("The Archivist beam does not turn, so a stationary boss threatens exactly one spot on the floor.");
				if (archivist.SweepHalfAngle <= 0f || archivist.SweepHalfAngle >= 90f)
					warnings.Add($"The Archivist beam half-angle is {archivist.SweepHalfAngle} degrees; outside 0-90 it is either nothing or a half-plane.");
				if (archivist.ResurfaceTellSeconds >= archivist.BurrowTravelSeconds)
					warnings.Add($"The Archivist surfaces in {archivist.BurrowTravelSeconds}s but marks the ground for {archivist.ResurfaceTellSeconds}s, so it arrives before its own warning resolves.");
				// It is always winding up - that is how the beam stays on screen - so an interval
				// at or above the wind-up turns a continuous sweep into a strobe.
				if (probe.SlamIntervalSeconds >= probe.SlamTelegraphSeconds)
					warnings.Add($"The Archivist interval ({probe.SlamIntervalSeconds}s) is not below its wind-up ({probe.SlamTelegraphSeconds}s), so its beam blinks off between sweeps instead of turning continuously.");
				break;

			case StillWardenBoss warden:
				if (warden.ShardsPerVolley <= 0 || warden.IceVolleyIntervalSeconds <= 0f)
					warnings.Add("The Still Warden drops no ice, so a boss that cannot chase has no pressure at all.");
				if (warden.IceScatterRadius <= warden.IceShardRadius)
					warnings.Add($"The Still Warden scatters ice within {warden.IceScatterRadius} using a {warden.IceShardRadius} radius, so every volley covers the player and none of it is dodgeable.");
				if (!ResourceLoader.Exists(warden.GuardScenePath))
					warnings.Add($"The Still Warden guard scene '{warden.GuardScenePath}' is missing, so it stands unguarded.");
				break;

			case LongCoilBoss coil:
				if (coil.SurfacedSeconds <= 0f)
					warnings.Add("The Long Coil is never above ground, so it can never be damaged and the fight cannot end.");
				if (coil.ResurfaceTellSeconds >= coil.DiveTravelSeconds)
					warnings.Add($"The Long Coil surfaces in {coil.DiveTravelSeconds}s but marks the ground for {coil.ResurfaceTellSeconds}s, so it arrives before its own warning resolves.");
				if (coil.BodySegments < 2)
					warnings.Add($"The Long Coil has {coil.BodySegments} body segments, so the one boss built around having a body behind it does not have one.");
				break;

			case DeepWardenBoss deep:
				if (deep.ChargeTellFraction <= 0f || deep.ChargeTellFraction >= 1f)
					warnings.Add($"The Warden of the Deep has ChargeTellFraction {deep.ChargeTellFraction}; at 0 it charges with no tell and at 1 it never charges.");
				if (deep.SweepDegreesPerSecond <= 0f)
					warnings.Add("The Warden of the Deep sweep does not turn, so its second phase is a fixed wedge.");
				if (deep.SurfacedSeconds <= 0f)
					warnings.Add("The Warden of the Deep is never above ground in its third phase, so the fight stalls there.");
				break;
		}
	}

	// Actually builds the boss once. A .tscn that exists but has the wrong script - or no script -
	// still passes ResourceLoader.Exists and then fails at minute fifteen, where nobody is watching.
	// Instantiate does not run _Ready (that needs the tree), so this is a cheap structural probe.
	private static void ValidateBossScene(List<string> warnings, BossDefinition boss)
	{
		if (string.IsNullOrWhiteSpace(boss.ScenePath) || !ResourceLoader.Exists(boss.ScenePath))
		{
			warnings.Add($"Boss '{boss.Id}' has a missing scene: '{boss.ScenePath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(boss.ScenePath);
		if (scene == null)
		{
			warnings.Add($"Boss '{boss.Id}' scene '{boss.ScenePath}' failed to load.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not BossEnemy)
			warnings.Add($"Boss '{boss.Id}' scene '{boss.ScenePath}' does not have a BossEnemy script attached, so the run could never be won.");

		probe?.Free();
	}

	// The smallest RunResult that can satisfy a boss achievement: a victory carrying that boss id.
	// Deliberately leaves every other field at its default so only the boss clause can match.
	private static AchievementContext BuildVictoryProbe(string bossId)
	{
		var run = new global::RunResult
		{
			Outcome = global::RunOutcome.Victory,
			BossId = bossId,
			StageId = "stage_0",
		};

		var save = new SaveData();
		save.Lifetime.Absorb(run);
		return AchievementContext.ForRun(save, run);
	}

	// The Full Set Enchantment screen renders each set from its icon and its Effects list, so a set that ships
	// with neither is invisible or blank to the player. This cannot check that the numbers still
	// agree with Player.RefreshChestSetEffects - that switch is imperative - but it does catch a
	// new set being added without its presentation data filled in.
	// Issue #63: every Full Set Enchantment is the pieces of one object, and a piece may belong to
	// more than one (The Crown is in the Jewelled Crown and the Regalia). What that needs checked:
	// every relic is a piece of something, every piece is a relic that can actually drop, nothing is
	// listed twice in one set, and a set's partial threshold is below its size - a "two-piece taste"
	// on a two-piece set would be paid at the same moment as the full effect and mean nothing.
	// Nested sets can never block each other here because relics are never consumed: holding The
	// Crown for one set is holding it for the other.
	private static void ValidateFullSetObjects(List<string> warnings)
	{
		var pool = new HashSet<string>(ChestItemCatalog.AllItemIds, StringComparer.OrdinalIgnoreCase);
		var pieces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var set in ChestItemCatalog.Sets)
		{
			if (set.RequiredItemIds == null || set.RequiredItemIds.Length < 2)
			{
				warnings.Add($"Full Set Enchantment '{set.Id}' has fewer than two pieces; an object that cannot come apart is not a set.");
				continue;
			}
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string id in set.RequiredItemIds)
			{
				if (!seen.Add(id))
					warnings.Add($"Full Set Enchantment '{set.Id}' lists '{id}' twice.");
				if (!pool.Contains(id))
					warnings.Add($"Full Set Enchantment '{set.Id}' needs '{id}', which is not in ChestItemCatalog.AllItemIds, so it can never drop and the set can never complete.");
				pieces.Add(id);
			}
			if (set.PartialItemCount > 0 && set.PartialItemCount >= set.RequiredItemIds.Length)
				warnings.Add($"Full Set Enchantment '{set.Id}' pays its partial at {set.PartialItemCount} of {set.RequiredItemIds.Length} pieces, the same moment as the full effect.");
		}
		foreach (string id in ChestItemCatalog.AllItemIds)
		{
			// The tome relics are the one exception, and the opposite rule holds for them: they must
			// be in NO set (ChestItemCatalog.TomeRelicIds says why). ValidateTomeRelics checks that.
			if (ChestItemCatalog.IsTomeRelic(id))
				continue;
			if (!pieces.Contains(id))
				warnings.Add($"Relic '{id}' ({ChestItemCatalog.GetDisplayName(id)}) is a piece of no Full Set Enchantment. Every relic is part of an object (#63).");
		}
	}

	// The two relics that act on the spell book (.ai/passives-and-items.md, section 4b). Spells are
	// permanent once taken, so these are the only way the book changes, and each rule below is one a
	// build cannot see: a set that needed the Folio would make a set chase a hunt for the rarest
	// relic; a tag on either would double-dip the element budget a seventh slot already is; and a
	// Marginalia id that collided with a spell or boon would route a real pick to the fallback card.
	private static void ValidateTomeRelics(List<string> warnings)
	{
		foreach (string id in ChestItemCatalog.TomeRelicIds)
		{
			if (!ChestItemCatalog.AllItemIds.Contains(id, StringComparer.OrdinalIgnoreCase))
				warnings.Add($"Tome relic '{id}' is not in ChestItemCatalog.AllItemIds, so no chest can ever offer it.");
			if (ChestItemCatalog.GetElementTags(id).Count > 0)
				warnings.Add($"Tome relic '{id}' carries an element tag; a tome relic changes the book, and the book's spells carry the tags.");
			if (ChestItemCatalog.GetAssociatedSets(id).Count > 0)
				warnings.Add($"Tome relic '{id}' is a piece of a Full Set Enchantment; tome relics are one per run and must complete nothing.");
			if (ChestItemCatalog.GetDescription(id) == ChestItemCatalog.GetDescription("__no_such_relic__"))
				warnings.Add($"Tome relic '{id}' has no description of its own.");
		}
		if (ChestItemCatalog.GetRarity(ChestItemCatalog.BlankFolio) != ChestItemRarity.Relic)
			warnings.Add("The Blank Folio is not Relic rarity; a seventh spell slot belongs in the rarest tier.");

		string marginalia = Player.MarginaliaId;
		if (BoonCatalog.IsBoon(marginalia) || UnlockCatalog.AllSpellIds.Contains(marginalia, StringComparer.OrdinalIgnoreCase))
			warnings.Add($"'{marginalia}' is both the Marginalia fallback card and a spell or boon id; Node2DGame would route the real pick to the fallback.");
		if (!ResourceLoader.Exists("res://assets/bonelight/ui/icons/marginalia.png"))
			warnings.Add("The Marginalia card has no icon (res://assets/bonelight/ui/icons/marginalia.png); run python tools/art/relic_icons.py and import.");
	}

	// The collector economy (.ai/passives-and-items.md, section 8). Each rule here fails quietly in
	// play: a drop table whose chances sum past 1 at full Luck silently stops dropping the last item
	// in the roll; a sixth relic page overruns a chest screen laid out and measured for five; and a
	// schema-12 migration that lost its credit would close the Emberdeep on a player who had opened it.
	private static void ValidateCollectorEconomy(List<string> warnings)
	{
		const int ChestPagesLaidOut = 5;
		int mostOffers = DropTable.BaseChestOffers + 1 // the Relic Key
			+ CharacterRoster.GetAll().Count(c => c != null && "chest_options_1".Equals(c.StartingPassiveId?.Trim(), StringComparison.OrdinalIgnoreCase));
		if (mostOffers > ChestPagesLaidOut)
			warnings.Add($"A chest can offer {mostOffers} relics, but the chest screen is laid out and measured for {ChestPagesLaidOut}.");

		// Read into locals: compared as constants, a rule that holds today is "unreachable code".
		float orb = DropTable.BonusOrbChance, flask = DropTable.HealthFlaskChance, potion = DropTable.SpeedPotionChance;
		float sum = (orb + flask + potion) * (1f + DropTable.LuckScale);
		if (sum >= 1f || orb <= 0f || flask <= 0f || potion <= 0f)
			warnings.Add($"DropTable's bonus chances are out of range: {sum:0.000} a kill at full Luck, and each must be above 0.");
		float elite = DropTable.EliteChestChance, nested = DropTable.NestedChestChance;
		if (elite <= 0f || elite > 1f || nested >= 1f)
			warnings.Add("DropTable's chest chances are out of range: an elite's must be in (0, 1], and a nested chest's below 1 or the Hidden Drawer never stops.");
		int bigger = DropTable.GreaterOrbValue;
		if (bigger <= 2)
			warnings.Add($"A bigger XP orb is worth {bigger}, no more than the ordinary orb it replaces.");

		// Schema 12: a save that had everything at 11 is credited with John Dee; one that did not is not.
		var complete = new SaveData { SchemaVersion = 11 };
		foreach (string id in UnlockCatalog.AllSpellIds)
			complete.UnlockedSpellIds.Add(id);
		foreach (string id in UnlockCatalog.AllWizardIds.Where(id => !id.Equals("john_dee", StringComparison.OrdinalIgnoreCase)))
			complete.UnlockedCharacterIds.Add(id);
		complete.Migrate();
		if (!GlobalStatsManager.IsCampaignComplete(complete))
			warnings.Add("A save that had freed every wizard and recovered every spell at schema 11 loses the Emberdeep at 12: SaveData.MigrateCollectorIntoCompletedCampaign did not credit John Dee.");
		var partial = new SaveData { SchemaVersion = 11 };
		partial.Migrate();
		if (GlobalStatsManager.IsCharacterUnlocked(partial, "john_dee"))
			warnings.Add("A schema-11 save that had not met the campaign gate was handed John Dee by the schema-12 migration; he is earned by opening chests.");
	}

	private static void ValidateChestSetPresentation(List<string> warnings)
	{
		foreach (var set in ChestItemCatalog.Sets)
		{
			if (set.Effects == null || set.Effects.Length == 0)
				warnings.Add($"Chest set '{set.Id}' declares no Effects, so the Full Set Enchantment screen has nothing to list.");

			if (string.IsNullOrWhiteSpace(set.IconPath) || !ResourceLoader.Exists(set.IconPath))
				warnings.Add($"Chest set '{set.Id}' has a missing icon: '{set.IconPath}'.");

			if (set.RequiredItemIds == null || set.RequiredItemIds.Length == 0)
				warnings.Add($"Chest set '{set.Id}' requires no items, so it completes immediately.");
		}

		ValidateChestItemIcons(warnings);
	}

	// Relics are told apart by their icon in the chest menu, the HUD strip and the Full Set Enchantment screen,
	// so two relics sharing one image is a real readability bug rather than a cosmetic one. Three
	// pairs used to collide; this keeps them from drifting back together.
	private static void ValidateChestItemIcons(List<string> warnings)
	{
		var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string itemId in ChestItemCatalog.AllItemIds)
		{
			string icon = ChestItemCatalog.GetIconPath(itemId);
			if (string.IsNullOrWhiteSpace(icon) || !ResourceLoader.Exists(icon))
			{
				warnings.Add($"Chest item '{itemId}' has a missing icon: '{icon}'.");
				continue;
			}

			if (seen.TryGetValue(icon, out string owner))
				warnings.Add($"Chest items '{owner}' and '{itemId}' share the icon '{icon}'.");
			else
				seen[icon] = itemId;
		}
	}

	// The Apotheosis covenant (issue #60). Every one of these produces a gate that can never open, or
	// a reward that takes the player OUT of the tier that earned it, and none of them is visible to
	// dotnet build. A typo in a required evolution id is the classic one: the offer simply never
	// appears, forever, and nothing says why.
	private static void ValidateApotheosisCatalog(List<string> warnings)
	{
		var spells = ContentValidator.LoadAllSpells();
		var characters = CharacterRoster.GetAll();
		var seenCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var seenVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (ApotheosisDefinition definition in ApotheosisCatalog.All)
		{
			string label = $"Apotheosis '{definition.Form?.Id}'";
			if (definition.Form == null || definition.Form.MilestoneLevel != ApotheosisCatalog.MilestoneLevel)
				warnings.Add($"{label} has no form, or a form whose MilestoneLevel is not {ApotheosisCatalog.MilestoneLevel}, so SpellData.ApplyEvolution would refuse it.");

			if (definition.CharacterId.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
				warnings.Add($"{label} is given to the Test Wizard, which picks its own loadout and is exempt from the covenant.");
			if (!seenCharacters.Add(definition.CharacterId))
				warnings.Add($"{label}: character '{definition.CharacterId}' has more than one Apotheosis.");
			if (!seenVerbs.Add(definition.Verb))
				warnings.Add($"{label}: verb '{definition.Verb}' is already claimed. Each Apotheosis must own a verb no other does (#59).");

			CharacterData character = characters.FirstOrDefault(c => c.Id.Equals(definition.CharacterId, StringComparison.OrdinalIgnoreCase));
			if (character == null)
			{
				warnings.Add($"{label} names character '{definition.CharacterId}', which is not in CharacterRoster.");
				continue;
			}
			if (character.StartingSpellResource == null
				|| !character.StartingSpellResource.Id.Equals(definition.SpellId, StringComparison.OrdinalIgnoreCase))
			{
				warnings.Add($"{label} transforms '{definition.SpellId}', but {character.Id} starts with '{character.StartingSpellResource?.Id}'. An Apotheosis is the final form of the wizard's OWN starting spell.");
			}

			SpellData template = spells.FirstOrDefault(s => s.Id.Equals(definition.SpellId, StringComparison.OrdinalIgnoreCase));
			if (template == null)
			{
				warnings.Add($"{label} names spell '{definition.SpellId}', which is not in ContentValidator.SpellResourcePaths.");
				continue;
			}

			var spell = (SpellData)template.Duplicate(true);
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			string element = definition.RequiredElement.ToString();
			if (!spell.ElementWeights.ContainsKey(element))
				warnings.Add($"{label} requires {element}, which '{spell.Id}' does not carry.");

			CheckRequiredPath(warnings, label, element, spell.Level4Options, definition.RequiredLevel4EvolutionId, 4);
			CheckRequiredPath(warnings, label, element, spell.Level8Options, definition.RequiredLevel8EvolutionId, 8);

			if (definition.Form == null)
				continue;

			// Preserve or increase, never reduce, and never a new element: the form may only add
			// weight to the element that qualified it.
			foreach (var pair in definition.Form.BonusElementWeights)
			{
				if (!pair.Key.Equals(element, StringComparison.OrdinalIgnoreCase))
					warnings.Add($"{label} adds {pair.Key}; a form may only deepen {element}, never name another element.");
				if (pair.Value < 0)
					warnings.Add($"{label} REDUCES {pair.Key}, which would drop the player out of the tier that earned it.");
			}

			// The two-element ceiling, across base, path and form together.
			var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var key in spell.ElementWeights.Keys) distinct.Add(key);
			foreach (var option in spell.Level4Options.Concat(spell.Level8Options))
			{
				if (option.Id.Equals(definition.RequiredLevel4EvolutionId, StringComparison.OrdinalIgnoreCase)
					|| option.Id.Equals(definition.RequiredLevel8EvolutionId, StringComparison.OrdinalIgnoreCase))
				{
					foreach (var key in option.BonusElementWeights.Keys) distinct.Add(key);
				}
			}
			foreach (var key in definition.Form.BonusElementWeights.Keys) distinct.Add(key);
			if (distinct.Count > 2)
				warnings.Add($"{label} ends up naming {distinct.Count} elements ({string.Join(", ", distinct)}); no spell may ever name more than two.");
		}
	}

	private static void CheckRequiredPath(List<string> warnings, string label, string element,
		Godot.Collections.Array<SpellEvolutionOption> options, string requiredId, int milestone)
	{
		SpellEvolutionOption option = options?.FirstOrDefault(o => o != null && o.Id.Equals(requiredId, StringComparison.OrdinalIgnoreCase));
		if (option == null)
		{
			warnings.Add($"{label} requires level {milestone} option '{requiredId}', which the spell does not offer. The gate can never open.");
			return;
		}
		// A required branch must never lead away from the wizard's own element (#61), or tier 6 is
		// harder on the exact build the gate demands.
		foreach (var key in option.BonusElementWeights.Keys)
		{
			if (!key.Equals(element, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"{label}: required level {milestone} option '{requiredId}' branches into {key}, away from {element}.");
		}
		if (!string.IsNullOrEmpty(option.ReplacesElement))
			warnings.Add($"{label}: required level {milestone} option '{requiredId}' trades away {option.ReplacesElement}; a required path may only deepen {element}.");
	}

	private static void ValidateSpellEvolutionCoverage(List<string> warnings)
	{
		string[] testSpellIds = new[] { "magic_missile", "fireball", "arcane_explosion", "aegis_ward", "meteor_swarm" };
		foreach (string id in testSpellIds)
		{
			var spell = new SpellData { Id = id, Name = id };
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			if (spell.Level4Options == null || spell.Level4Options.Count != 3)
				warnings.Add($"Spell '{id}' should have exactly 3 Level 4 evolution choices, got {spell.Level4Options?.Count ?? 0}.");
			if (spell.Level8Options == null || spell.Level8Options.Count != 2)
				warnings.Add($"Spell '{id}' should have exactly 2 Level 8 evolution choices, got {spell.Level8Options?.Count ?? 0}.");
		}
	}

	private static void ValidateDynamicRewardBounds(List<string> warnings)
	{
		var save = new SaveData();
		for (int i = 0; i < 8; i++)
		{
			save.RunTelemetryHistory.Add(new RunTelemetryRecord
			{
				Outcome = i < 4 ? "Defeat" : "Victory",
				TimeSurvived = 180f + (i * 25f),
				EnemiesKilled = 40 + (i * 7)
			});
		}

		var run = new RunResult
		{
			TimeSurvived = 480f,
			EnemiesKilled = 220,
			EquippedSpells = new List<RunSpellSnapshot>
			{
				new() { Id = "magic_missile", DisplayName = "Magic Missile" },
				new() { Id = "fireball", DisplayName = "Fireball" },
				new() { Id = "meteor_swarm", DisplayName = "Meteor Swarm" }
			}
		};

		float multiplier = GlobalStatsManager.GetDynamicArcaneRewardMultiplier(save, run, out _);
		if (multiplier < GlobalStatsManager.DynamicRewardMinMultiplier || multiplier > GlobalStatsManager.DynamicRewardMaxMultiplier)
			warnings.Add($"Dynamic reward multiplier out of bounds: {multiplier:0.000}");
	}

	private static void ValidateTelemetryHistoryCap(List<string> warnings)
	{
		var save = new SaveData();
		for (int i = 0; i < 40; i++)
		{
			save.RecordRunTelemetry(new RunResult
			{
				Outcome = i % 2 == 0 ? RunOutcome.Defeat : RunOutcome.Victory,
				TimeSurvived = 60f + i,
				EnemiesKilled = i
			});
		}

		if (save.RunTelemetryHistory.Count > 25)
			warnings.Add($"Telemetry history exceeded cap: {save.RunTelemetryHistory.Count}");
	}

	private static void ValidateCharacterRosterBasics(List<string> warnings)
	{
		var characters = CharacterRoster.GetAll();
		if (characters.Count == 0)
		{
			warnings.Add("Character roster returned zero entries.");
			return;
		}

		foreach (CharacterData character in characters)
		{
			if (character.StartingSpellResource == null)
				warnings.Add($"Character '{character.Id}' missing starting spell resource.");
		}
	}

	// The starting roster's one real invariant: a character is its element. Each playable character
	// opens with a spell carrying a single element weight, and no two of them open the same one, so
	// picking a character is picking which set of element thresholds the run starts pointed at.
	//
	// Both halves rot silently. A starting spell quietly retagged with a second element (Magic
	// Missile carries Arcane *and* Lightning, which is why it is no longer a starter) splits the
	// character's opening weight without changing anything visible; two characters sharing an
	// element makes one of them redundant without breaking a thing.
	// Two rules that only exist as agreement between data files, so nothing but this notices when
	// they break.
	//
	// 1. No spell carries the same element twice in its BASE tags. A spell used to be able to be
	//    "Fire x2", which made a single-element spell worth as much element weight as a hybrid and
	//    left nothing to distinguish the two. Weight is now capped at 1 per element, and what a
	//    pure spell gets instead is attunement (Player.GetAttunementMultiplier). Evolutions may
	//    still stack a second tag of the same element - that is the level 4 and level 8 choice -
	//    which is why this checks the resource's declared weights and not GetElementWeights().
	//
	// 2. Attunement is only worth anything to a spell that deals damage, so a pure spell that is
	//    passive gets no compensation at all for its halved element weight. Passives must carry
	//    two elements.
	// Wave formation geometry. None of this is visible to a build, and all of it fails quietly in
	// play: a formation with a bad placement does not crash, it just spawns a wave that looks like
	// the scatter it was meant to replace, which is indistinguishable from the feature not being
	// finished.
	// Every element must be able to reach its 6-instance capstone.
	//
	// This is the check that guarded the passives-to-boons shift, and it is why that shift did not
	// silently strand five elements below their top tier. Passive spells carried roughly a third of
	// the element weight in the game - four of Metal's six carriers, two of Water's three - and
	// nothing breaks loudly when a carrier disappears: the element simply never reaches a threshold
	// again, and the bonus at the end of it becomes text nobody ever sees.
	//
	// It stays because the same thing can happen to any future content change.
	//
	// Slightly pessimistic by construction: magic_missile is built in code rather than loaded from
	// a resource, so its Arcane and Lightning tags are not counted. Both elements are far above the
	// floor, so a false pass is not possible from that omission - only a false failure, which would
	// be visible rather than silent.
	// Every boon needs exactly one way to be obtained, for the same reason every spell does: one
	// with no catalog entry is defined, offered by nothing, and invisible.
	// Rarity and element tags on chest items, which are two separate tables that have to agree.
	//
	// The rule they encode is the point: a tag belongs only on a scarce item. If a Common ever
	// picks one up, element weight stops being a budget - and nothing in play would look wrong,
	// the thresholds would just start arriving early and the reason would be invisible.
	private static void ValidateChestRarity(List<string> warnings)
	{
		foreach (string itemId in ChestItemCatalog.AllItemIds)
		{
			// Tome relics use rarity for how often they are offered and nothing else: they carry no
			// tag at any rarity (ValidateTomeRelics).
			if (ChestItemCatalog.IsTomeRelic(itemId))
				continue;
			ChestItemRarity rarity = ChestItemCatalog.GetRarity(itemId);
			IReadOnlyList<(string Element, int Weight)> tags = ChestItemCatalog.GetElementTags(itemId);
			bool scarce = rarity is ChestItemRarity.Rare or ChestItemRarity.Relic;

			if (!scarce && tags.Count > 0)
				warnings.Add($"Chest item '{itemId}' is {rarity} but carries {tags.Count} element tags; only Rare and Relic items may.");

			if (scarce && tags.Count == 0)
				warnings.Add($"Chest item '{itemId}' is {rarity} but carries no element tag, which is most of what that rarity is for.");

			if (rarity == ChestItemRarity.Relic && tags.Count != 2)
				warnings.Add($"Relic '{itemId}' carries {tags.Count} element tags; a Relic carries two.");

			if (rarity == ChestItemRarity.Rare && tags.Count != 1)
				warnings.Add($"Rare '{itemId}' carries {tags.Count} element tags; a Rare carries one.");

			CheckTagList(warnings, $"Chest item '{itemId}'", tags);
		}

		foreach (ChestSetDefinition set in ChestItemCatalog.Sets)
		{
			if (set.ElementTags.Length != 2)
				warnings.Add($"Full Set Enchantment '{set.Id}' grants {set.ElementTags.Length} element tags; a completed set grants two.");

			CheckTagList(warnings, $"Set '{set.Id}'", set.ElementTags);
		}
	}

	private static void CheckTagList(List<string> warnings, string owner, IReadOnlyList<(string Element, int Weight)> tags)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (element, weight) in tags)
		{
			if (!Enum.TryParse<Element>(element, true, out _))
				warnings.Add($"{owner} carries unknown element '{element}'.");
			if (!seen.Add(element))
				warnings.Add($"{owner} carries {element} twice; one instance per element, as with every other tag source.");
			if (weight != 1)
				warnings.Add($"{owner} carries {element} at weight {weight}; tags are one instance each.");
		}
	}

	private static void ValidateBoonSources(List<string> warnings)
	{
		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			int sources = UnlockCatalog.All.Count(d =>
				d.Kind == UnlockKind.Boon && d.Id.Equals(boon.Id, StringComparison.OrdinalIgnoreCase));

			if (sources == 0)
				warnings.Add($"Boon '{boon.Id}' has no unlock source, so it can never be offered.");
			else if (sources > 1)
				warnings.Add($"Boon '{boon.Id}' has {sources} unlock sources; each thing needs exactly one.");

			// Same rule as spells: never the same element twice on one thing.
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var (element, weight) in boon.ElementWeights)
			{
				if (!Enum.TryParse<Element>(element, true, out _))
					warnings.Add($"Boon '{boon.Id}' carries unknown element '{element}'.");
				if (!seen.Add(element))
					warnings.Add($"Boon '{boon.Id}' carries {element} twice; base element weights are one per element.");
				if (weight != 1)
					warnings.Add($"Boon '{boon.Id}' carries {element} at weight {weight}; boons carry one instance per element.");
			}
		}
	}

	// A boon's card is its icon and its stat line, and both fail silently. The icon is drawn by
	// tools/art/boon_icons.py only for a boon with an ELEMENT entry there, and an unimported or
	// ungenerated PNG loads as null and the card shows an empty reliquary window. The stat line is
	// BoonCatalog.DescribeEffect's switch, whose default arm is an empty string - so a new BoonEffect
	// without an arm would be dealt as a card that says nothing at all.
	private static void ValidateBoonPresentation(List<string> warnings)
	{
		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			if (!ResourceLoader.Exists(boon.IconPath))
				warnings.Add($"Boon '{boon.Id}' has no icon at '{boon.IconPath}'; add it to ELEMENT (and ICONS) in tools/art/boon_icons.py, run it and import.");
			if (string.IsNullOrWhiteSpace(BoonCatalog.DescribeEffect(boon)))
				warnings.Add($"Boon '{boon.Id}' has an empty stat line; BoonCatalog.DescribeEffect has no arm for {boon.Effect}.");
		}
		// Read into a local: compared as a constant, a rule that holds today is "unreachable code".
		float stance = BoonCatalog.StanceSeconds;
		if (stance <= 0f)
			warnings.Add("BoonCatalog.StanceSeconds is not positive, so a stance boon would hold even while walking.");
	}

	private static void ValidateElementReachability(List<string> warnings)
	{
		// Two pools with two separate caps: spells fill six slots, boons fill six of their own.
		// Summing them as one pool would let a six-boon element borrow spell slots it cannot use.
		var spellContributions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
		var boonContributions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

		static void RecordInto(Dictionary<string, List<int>> pool, string element, int weight)
		{
			if (weight <= 0)
				return;

			if (!pool.TryGetValue(element, out List<int> list))
			{
				list = new List<int>();
				pool[element] = list;
			}

			list.Add(weight);
		}

		void Record(string element, int weight) => RecordInto(spellContributions, element, weight);

		foreach (SpellData spell in ContentValidator.LoadAllSpells())
		{
			if (spell?.ElementWeights == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			List<(string Id, Dictionary<string, int> Gains)> level4 =
				MaterialiseGains(spell.Level4Options, spell.Id, warnings);
			List<(string Id, Dictionary<string, int> Gains)> level8 =
				MaterialiseGains(spell.Level8Options, spell.Id, warnings);

			// Every element this spell could ever touch - the ones it already carries AND the ones
			// it can branch into. Walking only the base tags missed branches entirely, which is how
			// the first run of this check under-counted Darkness: Obsidian Spike is a pure Earth
			// spell whose level 4 branch grants Darkness, and that carrier was invisible.
			var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var pair in spell.ElementWeights)
				candidates.Add(pair.Key);
			foreach (var option in level4.Concat(level8))
			{
				foreach (var gain in option.Gains)
					candidates.Add(gain.Key);
			}

			foreach (string element in candidates)
			{
				// The best this one spell could ever contribute to this one element: its base tag,
				// plus the most generous evolution available at each milestone.
				int baseWeight = spell.ElementWeights.TryGetValue(element, out int w) ? w : 0;
				Record(element, baseWeight + BestGain(level4, element) + BestGain(level8, element));
			}
		}

		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			foreach (var (element, weight) in boon.ElementWeights)
				RecordInto(boonContributions, element, weight);
		}

		foreach (Element element in Enum.GetValues<Element>())
		{
			string name = element.ToString();
			int fromSpells = TopN(spellContributions, name, Player.MaxSpellSlots);
			int fromBoons = TopN(boonContributions, name, Player.MaxBoonSlots);
			int reachable = fromSpells + fromBoons;

			if (reachable < 6)
			{
				warnings.Add($"Element '{name}' can reach at most {reachable} instances ({fromSpells} from spells, {fromBoons} from boons), so its tier-6 bonus is unreachable. It needs more carriers.");
			}
		}
	}

	private static int TopN(Dictionary<string, List<int>> pool, string element, int slots)
	{
		if (!pool.TryGetValue(element, out List<int> list))
			return 0;

		list.Sort();
		list.Reverse();
		return list.Take(slots).Sum();
	}

	private static int BestGain(List<(string Id, Dictionary<string, int> Gains)> options, string element)
	{
		int best = 0;
		foreach (var option in options)
		{
			if (option.Gains.TryGetValue(element, out int gain) && gain > best)
				best = gain;
		}

		return best;
	}

	private static void ValidateSpawnFormations(List<string> warnings)
	{
		foreach (SpawnFormation shape in Enum.GetValues<SpawnFormation>())
		{
			for (int count = 1; count <= 12; count++)
			{
				var bearings = new List<float>();
				int leftFlank = 0;
				int rightFlank = 0;

				for (int i = 0; i < count; i++)
				{
					var (bearing, radiusScale) = SpawnFormations.Placement(shape, i, count, 0f);

					if (float.IsNaN(bearing) || float.IsInfinity(bearing))
					{
						warnings.Add($"Formation {shape} member {i} of {count} has a bearing of {bearing}.");
						continue;
					}

					// A scale at or below zero puts the member on top of the player, which is the
					// same trap that silently invalidated an A/B of the spawn ring.
					if (radiusScale <= 0f)
						warnings.Add($"Formation {shape} member {i} of {count} has radius scale {radiusScale}; it would spawn inside the player.");

					bearings.Add(bearing);
					if (shape == SpawnFormation.Pincer)
					{
						if (Mathf.Sin(bearing) >= 0f) rightFlank++;
						else leftFlank++;
					}
				}

				if (bearings.Count != count)
					continue;

				// A Ring has to actually surround: its members must span most of a circle.
				if (shape == SpawnFormation.Ring && count >= 4)
				{
					float span = bearings.Max() - bearings.Min();
					if (span < Mathf.Pi * 1.4f)
						warnings.Add($"Formation Ring of {count} spans only {span:0.00} rad; it would read as an arc, not a ring.");
				}

				// An Arc has to stay in front. One that wraps past a half circle is a ring with a
				// gap, and the player would be flanked by something advertised as a wall.
				if (shape == SpawnFormation.Arc && count >= 2)
				{
					float span = bearings.Max() - bearings.Min();
					if (span > Mathf.Pi)
						warnings.Add($"Formation Arc of {count} spans {span:0.00} rad, more than half a circle.");
				}

				// A Pincer has to squeeze from both sides. Loading one flank makes it an arc that
				// happens to be off to one side.
				if (shape == SpawnFormation.Pincer && count >= 2 && Math.Abs(leftFlank - rightFlank) > 1)
					warnings.Add($"Formation Pincer of {count} splits {leftFlank}/{rightFlank} across its flanks; it should be even.");
			}
		}
	}

	private static void ValidateElementTags(List<string> warnings)
	{
		// One pass, and every Godot array is materialised into a plain List before it is read more
		// than once. Enumerating a Godot.Collections.Array creates a fresh managed wrapper per
		// element per enumeration, so re-walking Level4Options inside a loop over Level8Options
		// produced enough short-lived wrappers to crash the finaliser mid-validation.
		foreach (SpellData spell in ContentValidator.LoadAllSpells())
		{
			if (spell?.ElementWeights == null)
				continue;

			foreach (var pair in spell.ElementWeights)
			{
				if (pair.Value > 1)
					warnings.Add($"Spell '{spell.Id}' declares {pair.Key} with weight {pair.Value}; base element weights are capped at 1 per element, and depth comes from evolutions instead.");
			}

			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);

			var baseWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var pair in spell.ElementWeights)
				baseWeights[pair.Key] = pair.Value;

			List<(string Id, Dictionary<string, int> Gains)> level4 = MaterialiseGains(spell.Level4Options, spell.Id, warnings);
			List<(string Id, Dictionary<string, int> Gains)> level8 = MaterialiseGains(spell.Level8Options, spell.Id, warnings);
			ValidateEvolutionElementCeiling(spell.Id, baseWeights, level4, level8, warnings);
		}
	}

	// Flattens a milestone's options to plain data, checking each named element exists on the way -
	// an unknown element name is dropped in silence by SpellData.ApplyBonusElementWeights, so the
	// branch would simply grant nothing. A null entry stands for "took neither option".
	private static List<(string Id, Dictionary<string, int> Gains)> MaterialiseGains(
		Godot.Collections.Array<SpellEvolutionOption> options, string spellId, List<string> warnings)
	{
		var result = new List<(string, Dictionary<string, int>)> { ("none", new Dictionary<string, int>()) };
		if (options == null)
			return result;

		foreach (SpellEvolutionOption evo in options)
		{
			var gains = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			// A traded-away element rides in the same map under a sentinel, so every reader of the
			// gains sees it without the tuple changing shape: BestGain never picks it (it is the
			// smallest int there is) and the ceiling below removes the element instead of adding.
			if (!string.IsNullOrEmpty(evo?.ReplacesElement))
			{
				if (!Enum.TryParse<Element>(evo.ReplacesElement, true, out _))
					warnings.Add($"Evolution '{evo.Id}' on '{spellId}' replaces element '{evo.ReplacesElement}', which is not a known element, so nothing is traded away.");
				else if (evo.BonusElementWeights != null && evo.BonusElementWeights.ContainsKey(evo.ReplacesElement))
					warnings.Add($"Evolution '{evo.Id}' on '{spellId}' both replaces and grants {evo.ReplacesElement}.");
				else
					gains[evo.ReplacesElement] = ReplacedElement;
			}
			if (evo?.BonusElementWeights != null)
			{
				foreach (var pair in evo.BonusElementWeights)
				{
					if (!Enum.TryParse<Element>(pair.Key, true, out _))
					{
						warnings.Add($"Evolution '{evo.Id}' on '{spellId}' grants element '{pair.Key}', which is not a known element, so the branch grants nothing.");
						continue;
					}

					gains[pair.Key] = pair.Value;
				}
			}

			result.Add((evo?.Id ?? "none", gains));
		}

		return result;
	}

	// The gain MaterialiseGains records for an element a branch trades away.
	private const int ReplacedElement = int.MinValue;

	// Two ceilings that only hold by agreement between catalog entries, and that a player would
	// find by out-scaling the game rather than by anything looking broken:
	//
	//   1. No spell may end up naming more than TWO distinct elements. A hybrid has both of its
	//      slots filled already, so its evolutions may only deepen what it carries - or TRADE one
	//      element for another (SpellEvolutionOption.ReplacesElement), which keeps it at two;
	//      branching to a new element on top is what a pure spell trades its attunement for.
	//   2. Only a pure spell may reach 3 instances of one element. A hybrid able to put both of its
	//      gains into the same element would match a pure spell's depth and keep a second element
	//      as well, which is most of the reason to ever pick a pure spell.
	//
	// Checked over every level 4 x level 8 pairing, because both ceilings are properties of the
	// combination rather than of either option alone.
	private static void ValidateEvolutionElementCeiling(
		string spellId,
		Dictionary<string, int> baseWeights,
		List<(string Id, Dictionary<string, int> Gains)> level4,
		List<(string Id, Dictionary<string, int> Gains)> level8,
		List<string> warnings)
	{
		bool isPure = baseWeights.Count == 1;

		foreach (var early in level4)
		{
			foreach (var late in level8)
			{
				var totals = new Dictionary<string, int>(baseWeights, StringComparer.OrdinalIgnoreCase);
				foreach (var gains in new[] { early.Gains, late.Gains })
				{
					// Trades first, as SpellData.GetElementWeights applies them, then the gains.
					foreach (var pair in gains)
					{
						if (pair.Value != ReplacedElement)
							continue;
						if (!totals.Remove(pair.Key))
							warnings.Add($"Spell '{spellId}' trades away {pair.Key} via '{(gains == early.Gains ? early.Id : late.Id)}', which it does not carry at that point.");
					}
					foreach (var pair in gains)
					{
						if (pair.Value != ReplacedElement)
							totals[pair.Key] = totals.TryGetValue(pair.Key, out int existing) ? existing + pair.Value : pair.Value;
					}
				}

				if (totals.Count > 2)
					warnings.Add($"Spell '{spellId}' can reach {totals.Count} elements via '{early.Id}' + '{late.Id}'; a spell may never name more than two.");

				if (isPure)
					continue;

				foreach (var pair in totals)
				{
					if (pair.Value >= 3)
						warnings.Add($"Hybrid '{spellId}' can reach {pair.Value} {pair.Key} via '{early.Id}' + '{late.Id}'; only a pure spell may reach 3 of one element.");
				}
			}
		}
	}

	// Element paths (.ai/spell-roster.md, "Element paths"; ElementPaths). The rules the user set, which
	// no single catalog entry can see broken:
	//
	//   1. A pure spell's level 4 offers BOTH a branch that deepens its own element and one that adds
	//      a different element - so the milestone is a decision, never a free tag.
	//   2. A hybrid's path is deepening its other half (a third element is never allowed), so each of
	//      its two elements is deepened by some level 4 or level 8 option.
	//   3. A branch into a new element looks like it (a tint) and names its tag on the card; a rider,
	//      if it has one, is the status that element carries (ElementPaths.RiderFor), with numbers
	//      its status can apply.
	//   4. Every element has a pure spell, because only a pure spell can carry an element to its
	//      capstone.
	//
	// Options are flattened to plain data first, for the reason ValidateElementTags gives.
	private static void ValidateElementPaths(List<string> warnings)
	{
		var pureElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (SpellData spell in ContentValidator.LoadAllSpells())
		{
			if (spell?.ElementWeights == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			var baseElements = new HashSet<string>(spell.ElementWeights.Keys, StringComparer.OrdinalIgnoreCase);
			bool isPure = baseElements.Count == 1;
			if (isPure)
				pureElements.UnionWith(baseElements);

			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			var options = new List<(int Milestone, SpellEvolutionOption Option, List<string> Gains)>();
			foreach (var (milestone, list) in new[] { (4, spell.Level4Options), (8, spell.Level8Options) })
			{
				if (list == null)
					continue;
				foreach (SpellEvolutionOption evo in list.ToList())
				{
					if (evo == null)
						continue;
					var gains = evo.BonusElementWeights == null
						? new List<string>()
						: evo.BonusElementWeights.Where(p => p.Value > 0).Select(p => p.Key).ToList();
					options.Add((milestone, evo, gains));
				}
			}

			if (isPure)
			{
				string own = baseElements.First();
				bool deepens = options.Any(o => o.Milestone == 4 && o.Gains.Any(g => g.Equals(own, StringComparison.OrdinalIgnoreCase)));
				bool branches = options.Any(o => o.Milestone == 4 && o.Gains.Any(g => !baseElements.Contains(g)));
				if (!deepens)
					warnings.Add($"Pure spell '{spell.Id}' has no level 4 branch that deepens {own}; a pure spell's milestone must offer one.");
				if (!branches)
					warnings.Add($"Pure spell '{spell.Id}' has no level 4 element path - no branch adds a second element to {own}, so the milestone is never an element decision.");
			}
			else
			{
				foreach (string element in baseElements)
				{
					if (!options.Any(o => o.Gains.Any(g => g.Equals(element, StringComparison.OrdinalIgnoreCase))))
						warnings.Add($"Hybrid '{spell.Id}' never deepens {element}; a hybrid's element path is deepening either half.");
				}
			}

			foreach (var (milestone, evo, gains) in options)
			{
				foreach (string gain in gains.Where(g => !baseElements.Contains(g)))
				{
					if (evo.ModulateColor == Colors.White)
						warnings.Add($"Evolution '{evo.Id}' branches '{spell.Id}' into {gain} but keeps the spell's look (no ModulateColor); an element path must look like its element.");
					if (string.IsNullOrEmpty(evo.Description) || evo.Description.IndexOf(gain, StringComparison.OrdinalIgnoreCase) < 0)
						warnings.Add($"Evolution '{evo.Id}' branches '{spell.Id}' into {gain} but its description never names the {gain} tag.");
				}

				if (evo.Rider == ElementRider.None)
					continue;
				bool carried = gains.Any(g => Enum.TryParse<Element>(g, true, out Element e) && ElementPaths.RiderFor(e) == evo.Rider);
				if (!carried)
					warnings.Add($"Evolution '{evo.Id}' on '{spell.Id}' carries a {evo.Rider} rider, but no element it grants ({string.Join(", ", gains)}) carries {evo.Rider} (ElementPaths.RiderFor).");
				if (!ElementPaths.IsValid(evo.Rider, evo.RiderMagnitude, evo.RiderSeconds))
					warnings.Add($"Evolution '{evo.Id}' on '{spell.Id}' has a {evo.Rider} rider of {evo.RiderMagnitude} for {evo.RiderSeconds} s, which its status cannot apply.");
			}
		}

		foreach (Element element in Enum.GetValues<Element>())
		{
			if (!pureElements.Contains(element.ToString()))
				warnings.Add($"Element '{element}' has no pure spell, so nothing can carry it to its 6-instance capstone.");
		}
	}

	private static void ValidateStartingCharacters(List<string> warnings)
	{
		var elementOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (CharacterData character in CharacterRoster.GetAll())
		{
			// The Test Wizard picks its loadout on the selection screen, so it has no fixed
			// starting element to check and is exempt by design.
			if (character == null || character.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
				continue;

			if (!string.IsNullOrWhiteSpace(character.StartingPassiveId)
				&& !Player.CharacterPassiveBonusIds.Contains(character.StartingPassiveId.Trim()))
			{
				warnings.Add($"Character '{character.Id}' names starting passive '{character.StartingPassiveId}', which is not a recognised bonus id, so it silently grants nothing.");
			}

			SpellData starter = character.StartingSpellResource;
			if (starter == null)
				continue;

			// No unlock check on the starting spell any more: Player.GetLevelUpOptions always offers a
			// character its own starting spell, which the rescued wizards need - they open on spells
			// the save may not own yet (.ai/side-events.md).

			var weights = starter.ElementWeights;
			if (weights == null || weights.Count == 0)
			{
				warnings.Add($"Character '{character.Id}' starts with '{starter.Id}', which carries no element weight, so the character opens pointed at no element at all.");
				continue;
			}

			if (weights.Count > 1)
			{
				warnings.Add($"Character '{character.Id}' starts with '{starter.Id}', which carries {weights.Count} elements; a starter must carry exactly one so the character's opening weight is not split.");
				continue;
			}

			string element = string.Empty;
			foreach (Variant key in weights.Keys)
			{
				element = key.AsString();
				break;
			}

			if (elementOwners.TryGetValue(element, out string owner))
				warnings.Add($"Characters '{owner}' and '{character.Id}' both open on {element}; each starting character should claim a different element.");
			else
				elementOwners[element] = character.Id;
		}
	}

	// The check that would have caught the whole dead economy.
	//
	// Every spell and every achievement reward used to line up perfectly on paper while granting
	// nothing at all, because the thing that decided what was locked - a 35-entry default-unlocked
	// set - lived somewhere else and contained everything. Nothing compared the two. These checks
	// are all cross-references between catalogs that must agree, and every one of them fails
	// silently in play: an unreachable spell just never appears, and a reward that grants something
	// the player already owns looks exactly like one that worked.
	private static void ValidateUnlockCatalog(List<string> warnings)
	{
		var seen = new Dictionary<string, UnlockDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (UnlockDefinition definition in UnlockCatalog.All)
		{
			string key = $"{definition.Kind}:{definition.Id}";
			if (seen.ContainsKey(key))
				warnings.Add($"Unlock catalog has two entries for {definition.Kind} '{definition.Id}'; each thing must have exactly one source.");
			else
				seen[key] = definition;

			if (definition.Source != UnlockSource.Starter && string.IsNullOrWhiteSpace(definition.LockedHint))
				warnings.Add($"Unlock '{definition.Id}' has no LockedHint, so its locked card cannot tell the player how to get it.");

			if (definition.Source == UnlockSource.Purchase && definition.PurchaseCost <= 0)
				warnings.Add($"Unlock '{definition.Id}' is purchasable for {definition.PurchaseCost} Arcane Energy, so it is free.");

			if (definition.Kind == UnlockKind.Boon && BoonCatalog.GetById(definition.Id) == null)
				warnings.Add($"Unlock catalog lists boon '{definition.Id}', which BoonCatalog does not define.");

			if (definition.Source == UnlockSource.Achievement
				&& AchievementDefinitions.GetById(definition.SourceId) == null)
			{
				warnings.Add($"Unlock '{definition.Id}' points at achievement '{definition.SourceId}', which does not exist, so it can never be earned.");
			}

			ValidateSecret(definition, warnings);
		}

		// Every spell the game can offer needs a source. A spell with no entry is locked forever -
		// which is the safe direction, but only if somebody is told about it.
		foreach (string path in ContentValidator.SpellResourcePaths)
		{
			var spell = GD.Load<SpellData>(path);
			if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			if (UnlockCatalog.GetSpell(spell.Id) == null)
				warnings.Add($"Spell '{spell.Id}' has no unlock catalog entry, so it can never be offered at level-up.");
		}

		foreach (CharacterData character in CharacterRoster.GetAll())
		{
			if (character != null && UnlockCatalog.GetCharacter(character.Id) == null)
				warnings.Add($"Character '{character.Id}' has no unlock catalog entry, so it can never be selected.");
		}

		// The other direction: an achievement that grants something the catalog says comes from
		// somewhere else. Both halves would look correct on their own.
		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			// A wizard freed by an achievement (John Dee) keeps the same agreement a spell does.
			if (!string.IsNullOrWhiteSpace(achievement.CharacterUnlockId))
			{
				UnlockDefinition wizard = UnlockCatalog.GetCharacter(achievement.CharacterUnlockId);
				if (wizard != null && (wizard.Source != UnlockSource.Achievement || !wizard.SourceId.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase)))
					warnings.Add($"Achievement '{achievement.Id}' frees '{achievement.CharacterUnlockId}', but the catalog sources that wizard from {wizard.Source} '{wizard.SourceId}' - one of the two is wrong.");
			}

			if (string.IsNullOrWhiteSpace(achievement.SpellUnlockId))
				continue;

			UnlockDefinition definition = UnlockCatalog.GetSpell(achievement.SpellUnlockId);
			if (definition == null)
				warnings.Add($"Achievement '{achievement.Id}' grants spell '{achievement.SpellUnlockId}', which has no unlock catalog entry.");
			else if (definition.Source != UnlockSource.Achievement || !definition.SourceId.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Achievement '{achievement.Id}' grants '{achievement.SpellUnlockId}', but the catalog sources that from {definition.Source} '{definition.SourceId}' - one of the two is wrong.");
		}

		// A fresh save must be able to start: at least one chapter open, and every starting
		// character's opening spell available on turn one.
		var fresh = new SaveData();
		if (!StageCatalog.All.Any(s => GlobalStatsManager.IsStageAvailable(fresh, s)))
			warnings.Add("No chapter is available on a new save, so a fresh game cannot be started.");

		if (GlobalStatsManager.IsCampaignComplete(fresh))
			warnings.Add("A brand-new save already satisfies the campaign gate, so the final chapter is not gated by anything.");

		ValidateCuration(fresh, warnings);
	}

	// A secret wizard's card is a sealed blank until its reveal condition holds (UnlockDefinition.
	// IsSecret, .ai/side-events.md "Secret wizards"). Every way that goes wrong is silent: a secret
	// with no reveal, or one pointing at a chapter that does not exist or never opens, is a card that
	// stays sealed for ever and looks exactly like one the player has not reached yet.
	private static void ValidateSecret(UnlockDefinition definition, List<string> warnings)
	{
		if (!definition.IsSecret)
		{
			if (!string.IsNullOrWhiteSpace(definition.RevealStageId))
				warnings.Add($"Unlock '{definition.Id}' names a reveal chapter '{definition.RevealStageId}' but is not secret, so nothing reads it.");
			return;
		}

		if (definition.Kind != UnlockKind.Character)
			warnings.Add($"Unlock '{definition.Id}' is a secret {definition.Kind}; only the character select reads IsSecret, so it would show as an ordinary lock.");
		if (definition.Source == UnlockSource.Starter)
			warnings.Add($"Starter '{definition.Id}' is marked secret; a starter is never locked, so the secret can never show.");

		if (string.IsNullOrWhiteSpace(definition.RevealStageId))
		{
			warnings.Add($"Secret '{definition.Id}' has no reveal condition (RevealStageId), so its card stays sealed for ever.");
			return;
		}

		StageDefinition stage = StageCatalog.GetById(definition.RevealStageId);
		if (stage == null)
		{
			warnings.Add($"Secret '{definition.Id}' is revealed by chapter '{definition.RevealStageId}', which StageCatalog does not define, so its card stays sealed for ever.");
			return;
		}
		if (!stage.IsPlayable)
			warnings.Add($"Secret '{definition.Id}' is revealed by '{stage.DisplayName}', which is not playable yet, so its card stays sealed.");
		// The final chapter opens only once every wizard is free, and a wizard nobody has seen
		// cannot be looked for: revealed by it, he would be revealed after he was needed.
		if (stage.Gate == StageGate.CampaignComplete)
			warnings.Add($"Secret '{definition.Id}' is revealed by '{stage.DisplayName}', which needs every wizard freed first - he would stay sealed until after he was needed.");
		if (GlobalStatsManager.IsCharacterRevealed(new SaveData(), definition.Id) && stage.Gate != StageGate.Open)
			warnings.Add($"Secret '{definition.Id}' reads as revealed on a brand-new save although '{stage.DisplayName}' is closed on one; IsCharacterRevealed has stopped honouring the reveal.");

		// A discovered wizard is revealed by the chapter that holds him: the place the player is
		// shown is the place he is freed from. Anything else hints at the wrong map.
		if (definition.Source == UnlockSource.Discovery)
		{
			var site = SideEventDirector.ForStage(stage.Id);
			if (site == null || !site.Value.SiteId.Equals(definition.SourceId, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Secret '{definition.Id}' is found at '{definition.SourceId}' but revealed by '{stage.DisplayName}', whose side event is '{site?.SiteId ?? "none"}'; reveal him with the chapter that holds him.");
		}
	}

	// Achievements are measured, not asserted: one function produces both "am I done" and "how far
	// am I", so they cannot disagree. What can still go wrong is a definition that is unreachable,
	// unmeasurable, or already satisfied - and every one of those is invisible in play, because an
	// achievement that never fires looks exactly like one the player has not earned yet.
	private static void ValidateAchievements(List<string> warnings)
	{
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var fresh = AchievementContext.ForSave(new SaveData());

		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			if (string.IsNullOrWhiteSpace(achievement.Id))
			{
				warnings.Add("An achievement has no Id, so it can never be recorded as unlocked.");
				continue;
			}

			if (!ids.Add(achievement.Id))
				warnings.Add($"Two achievements share the id '{achievement.Id}'; the second can never be earned separately.");

			if (achievement.Target <= 0f)
				warnings.Add($"Achievement '{achievement.Id}' has a target of {achievement.Target}, so it completes instantly.");

			if (achievement.Measure == null)
			{
				warnings.Add($"Achievement '{achievement.Id}' has no Measure, so it can never complete.");
				continue;
			}

			// A brand-new save must satisfy nothing. One that does is either mis-measured or has a
			// target of zero, and would fire on the player's very first run for no reason.
			if (achievement.IsComplete(fresh))
				warnings.Add($"Achievement '{achievement.Id}' is already complete on a new save.");

			bool grantsSomething = !string.IsNullOrWhiteSpace(achievement.SpellUnlockId)
				|| !string.IsNullOrWhiteSpace(achievement.BoonUnlockId)
				|| !string.IsNullOrWhiteSpace(achievement.CharacterUnlockId)
				|| achievement.CurrencyReward > 0;
			if (!grantsSomething)
				warnings.Add($"Achievement '{achievement.Id}' grants nothing, so completing it is not a reward.");

			if (!string.IsNullOrWhiteSpace(achievement.CharacterUnlockId)
				&& UnlockCatalog.GetCharacter(achievement.CharacterUnlockId) == null)
			{
				warnings.Add($"Achievement '{achievement.Id}' frees '{achievement.CharacterUnlockId}', which has no unlock catalog entry.");
			}
		}
	}

	// Curation's two bounds are easy to break from opposite directions and both fail quietly: too
	// generous and a player can narrow the pool until every level-up is the same three cards, too
	// strict and the shop sells a row that can never be bought.
	private static void ValidateCuration(SaveData fresh, List<string> warnings)
	{
		if (GlobalStatsManager.GetCurationSlotCapacity(fresh) != 0)
		{
			warnings.Add("A new save can already buy Redactions; curation is meant to be unavailable "
				+ "until the spellbook has grown past the minimum offerable pool.");
		}

		// With every spell unlocked, capacity must still leave the floor intact.
		var full = new SaveData();
		foreach (string id in UnlockCatalog.AllSpellIds)
			GlobalStatsManager.UnlockSpell(full, id);

		int unlocked = GlobalStatsManager.GetUnlockedSpellCount(full);
		int capacity = GlobalStatsManager.GetCurationSlotCapacity(full);
		if (unlocked - capacity < GlobalStatsManager.MinimumOfferablePool)
		{
			warnings.Add($"With every spell unlocked ({unlocked}), curation capacity {capacity} would allow the "
				+ $"pool to fall below the {GlobalStatsManager.MinimumOfferablePool}-spell floor.");
		}

		// And the floor must actually hold when the player spends every slot.
		full.ArcaneUpgradeLevels[GlobalStatsManager.CurationUpgradeId] = capacity;
		int removed = 0;
		foreach (string id in UnlockCatalog.AllSpellIds.ToList())
		{
			if (GlobalStatsManager.SetSpellRemovedFromPool(full, id, true))
				removed++;
		}

		if (unlocked - removed < GlobalStatsManager.MinimumOfferablePool)
		{
			warnings.Add($"Spending every Redaction removed {removed} of {unlocked} spells, leaving fewer than "
				+ $"the {GlobalStatsManager.MinimumOfferablePool} the floor promises.");
		}
	}

	private static void ValidateSaveDefaults(List<string> warnings)
	{
		var save = new SaveData();
		if (!save.EnableGameplayOnboardingTips)
			warnings.Add("New saves should enable onboarding tips by default.");

		if (save.HasToggledOnboardingTipsAtLeastOnce)
			warnings.Add("New saves should start with HasToggledOnboardingTipsAtLeastOnce = false.");

		if (save.PlaytestModeEnabled)
			warnings.Add("New saves should start with PlaytestModeEnabled = false.");

		if (!string.Equals(save.BalancePresetId, GlobalStatsManager.BalancePresetDefault, StringComparison.OrdinalIgnoreCase))
			warnings.Add($"New saves should default to '{GlobalStatsManager.BalancePresetDefault}' preset, got '{save.BalancePresetId}'.");
	}

	private static void ValidatePresetRewardOrdering(List<string> warnings)
	{
		float casual = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetCasual);
		float normal = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetDefault);
		float hardcore = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetHardcore);

		if (!(casual < normal && normal < hardcore))
			warnings.Add($"Preset reward ordering should be Casual < Default < Hardcore, got {casual:0.00}, {normal:0.00}, {hardcore:0.00}.");
	}

	private static void ValidatePlaytestChecklistDefaults(List<string> warnings)
	{
		var save = new SaveData();
		if (save.PlaytestChecklistState == null)
			warnings.Add("Playtest checklist state should default to an empty map.");

		if (save.PlaytestChecklistState != null && save.PlaytestChecklistState.Count != 0)
			warnings.Add($"Playtest checklist defaults should be empty, got {save.PlaytestChecklistState.Count} entries.");
	}
}
