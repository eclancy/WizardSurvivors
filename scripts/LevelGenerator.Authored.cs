using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WizardSurvivors.scripts;

// THE AUTHORED TOPOLOGY: ground read from a file rather than a function (.ai/endgame-castle.md 5.1, 6.1).
// Piece C3 built it for the Court; the Throne (piece C4) reads its hall with the same reader.
//
// A chapter on this topology is ONE FIXED PLACE: a grid of cells written by a generator in tools/art
// (court_layout.py -> court_layout.json; throne_room.py -> throne_room.json), the same every run. The
// generator lays the place out and checks it; this file only looks cells up. Every cell outside the
// grid is the layout's `outside` terrain - wall, for both castle chapters - so the world past the
// building's walls is solid for ever in every direction and the streamer, the navigation window and the
// placement checks all see a building standing in rock.
//
// THE SEAM RULE HOLDS BY CONSTRUCTION: a cell is a lookup by its world tile, a pure function of where it
// is, so any chunk asking for it gets the same answer (RegressionChecks.ValidateTerrainSeams holds it all
// the same, by itself, now that the topology is built).
//
// BUILT CHAPTER BY CHAPTER. A layout is registered per StageEnvironmentKind in AuthoredLayout.Files below,
// and the topology is built for a chapter only once its file exists (TopologyBuild.BuiltFor). So the Court
// plays on its building today while the Throne, whose throne_room.json is C4's to write, is still the open
// field it was - and its campaign to-do line still says so. The day C4's generator writes that file, the
// Throne is Authored with no edit here.
//
// It is WALLED (build.Walled): the halls play by the maze's rules - no cleanup pass (it would eat a
// carpet runner two cells wide), every enemy on the flow field, spawns and recycling by walking
// distance. It OWNS ITS SPAWN CLEARING: the run starts on the layout's `origin` cell, inside a room the
// generator made for it, so LevelGenerator.ClearSpawn's disc is never stamped through a wall.
public sealed partial class LevelGenerator
{
	static partial void BuildAuthoredTopology(ref TopologyBuild build)
	{
		build.Generate = GenerateAuthored;
		build.Walled = true;
		build.OwnsSpawnClearing = true;
		build.BuiltFor = kind => AuthoredLayout.For(kind) != null;
	}

	// The padded grid, [0, 0] being world tile (chunk.X0, chunk.Y0), from the chapter's layout.
	private static void GenerateAuthored(string[,] grid, TopologyChunk chunk)
	{
		AuthoredLayout layout = AuthoredLayout.For(chunk.Kind);
		if (layout == null)
		{
			// Unreachable while BuiltFor guards it (a chapter with no layout is generated as Open before
			// this is ever called); kept so a caller that skips the guard still gets ground, not nulls.
			FillOpenGround(grid, chunk);
			return;
		}
		layout.Fill(grid, chunk.X0, chunk.Y0, BaseSentinel, WallTerrain);
	}
}

/// <summary>
/// One authored place, as its generator wrote it: the grid, which cell the run starts on, and the rooms,
/// doors, hidden ways and furniture the generator placed. The general half of the Authored topology - the
/// Court and the Throne both read their files through this, and each chapter's own code reads the parts
/// that are only its (CourtLayout: the glyphs, the signs, the alcoves).
/// </summary>
/// <remarks>
/// THE FILE FORMAT (format 1), every coordinate in CELLS, x east and y south from the grid's north-west
/// corner:
///   width, height        the grid's size
///   origin   [x, y]      the cell that is world tile (0, 0): where the run starts (the player stands on
///                        its north-west corner, which is the stage origin)
///   outside  "#"         the grid character every cell beyond the grid is
///   legend   {ch: id}    grid character -> tiles.json terrain: "wall" is wall, "" is the base floor
///   rows     [string]    height strings of width characters, north first
///   rooms    [{id, name, kind, rect: [x, y, w, h], wing, hidden, counted, glyph}]
///   doors    [{room, rect, onto}]       a gap through a wall into a room
///   hidden   [{room, rect, onto}]       a hidden way: a real gap, drawn over (the Court's tapestries)
///   furniture [{kind, room, at: [x, y], index?, slot?, wall?, facing?}]   at: a point, in cells
/// A chapter's generator may add sections of its own; this reader ignores what it does not know.
/// </remarks>
public sealed class AuthoredLayout
{
	/// <summary>Where each authored chapter's layout lives. A chapter with no file here is not built.</summary>
	private static readonly Dictionary<StageEnvironmentKind, string> Files = new()
	{
		[StageEnvironmentKind.Court] = "res://assets/bonelight/layouts/court_layout.json",
		// The Throne (piece C4): throne_room.py writes this file; until it exists the Throne is open field.
		[StageEnvironmentKind.Throne] = "res://assets/bonelight/layouts/throne_room.json",
	};

	private static readonly Dictionary<StageEnvironmentKind, AuthoredLayout> Loaded = new();

	/// <summary>The chapter's layout, loaded once; null for a chapter with none (or a file that will not read).</summary>
	public static AuthoredLayout For(StageEnvironmentKind kind)
	{
		if (Loaded.TryGetValue(kind, out AuthoredLayout known))
			return known;
		AuthoredLayout layout = null;
		if (Files.TryGetValue(kind, out string path) && Godot.FileAccess.FileExists(path))
		{
			layout = Parse(Godot.FileAccess.GetFileAsString(path), path, out string problem);
			if (layout == null)
				GD.PushError($"AuthoredLayout: {path} will not read ({problem}); {kind} is generated as open ground. Re-run its generator in tools/art.");
		}
		Loaded[kind] = layout;
		return layout;
	}

	/// <summary>The path the chapter's layout is read from, or null for a chapter that has none registered.</summary>
	public static string PathFor(StageEnvironmentKind kind) => Files.TryGetValue(kind, out string p) ? p : null;

	public sealed class Room
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public string Kind { get; init; } = "";
		public Rect2I Rect { get; init; }
		public string Wing { get; init; } = "";
		public bool Hidden { get; init; }
		public bool Counted { get; init; }
		public string Glyph { get; init; } = "";
	}

	public sealed class Gap
	{
		public string Room { get; init; } = "";
		public Rect2I Rect { get; init; }
		public string Onto { get; init; } = "";
	}

	public sealed class Piece
	{
		public string Kind { get; init; } = "";
		public string Room { get; init; } = "";
		/// <summary>In cells (fractional): multiply by the tile size and add the cell's world origin.</summary>
		public Vector2 At { get; init; }
		public int Index { get; init; } = -1;
		public int Slot { get; init; } = -1;
		/// <summary>Stands against a wall (drawn on its face) rather than out on the floor.</summary>
		public bool Wall { get; init; }
		public string Facing { get; init; } = "";
	}

	public string Name { get; private set; } = "";
	public int Width { get; private set; }
	public int Height { get; private set; }
	/// <summary>The cell that is world tile (0, 0).</summary>
	public Vector2I Origin { get; private set; }
	public IReadOnlyList<Room> Rooms { get; private set; } = Array.Empty<Room>();
	public IReadOnlyList<Gap> Doors { get; private set; } = Array.Empty<Gap>();
	public IReadOnlyList<Gap> HiddenWays { get; private set; } = Array.Empty<Gap>();
	public IReadOnlyList<Piece> Furniture { get; private set; } = Array.Empty<Piece>();
	/// <summary>The file's text, for a chapter's own reader of the sections only it knows.</summary>
	public string Source { get; private set; } = "";

	// The grid as one byte a cell (an index into terrainOf), and the terrain each index stands for, with
	// floor and wall as the generator's own sentinels once Fill is first asked (see Fill).
	private byte[] cells = Array.Empty<byte>();
	private string[] legendIds = Array.Empty<string>();
	private byte outside;

	/// <summary>True if the cell is inside the grid.</summary>
	public bool Contains(Vector2I cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

	/// <summary>The cell of the grid a world tile is; the inverse of <see cref="WorldTileOf"/>.</summary>
	public Vector2I CellOfWorldTile(Vector2I tile) => tile + Origin;

	/// <summary>The world tile a cell of the grid is.</summary>
	public Vector2I WorldTileOf(Vector2I cell) => cell - Origin;

	/// <summary>The tiles.json terrain of a cell: "wall", "" for the base floor, or an overlay terrain.</summary>
	public string TerrainOf(Vector2I cell) => legendIds[Contains(cell) ? cells[cell.X + cell.Y * Width] : outside];

	/// <summary>True for any cell a body can stand on (not wall).</summary>
	public bool IsWalkable(Vector2I cell) => TerrainOf(cell) != LevelGenerator.WallTerrain;

	/// <summary>
	/// Fills a padded chunk grid whose [0, 0] is world tile (x0, y0). <paramref name="floor"/> and
	/// <paramref name="wall"/> are the generator's own sentinels, written by reference so the autotiler's
	/// comparisons see one string each.
	/// </summary>
	public void Fill(string[,] grid, int x0, int y0, string floor, string wall)
	{
		string[] ids = new string[legendIds.Length];
		for (int i = 0; i < ids.Length; i++)
			ids[i] = legendIds[i] == "" ? floor : legendIds[i] == LevelGenerator.WallTerrain ? wall : legendIds[i];
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);
		for (int x = 0; x < w; x++)
		{
			int cx = x + x0 + Origin.X;
			bool colInside = cx >= 0 && cx < Width;
			for (int y = 0; y < h; y++)
			{
				int cy = y + y0 + Origin.Y;
				grid[x, y] = ids[colInside && cy >= 0 && cy < Height ? cells[cx + cy * Width] : outside];
			}
		}
	}

	/// <summary>Reads a layout from its JSON text; null with the reason in <paramref name="problem"/>.</summary>
	public static AuthoredLayout Parse(string text, string path, out string problem)
	{
		problem = null;
		try
		{
			using JsonDocument doc = JsonDocument.Parse(text);
			JsonElement root = doc.RootElement;
			var layout = new AuthoredLayout
			{
				Source = text,
				Name = root.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "" : "",
				Width = root.GetProperty("width").GetInt32(),
				Height = root.GetProperty("height").GetInt32(),
			};
			JsonElement origin = root.GetProperty("origin");
			layout.Origin = new Vector2I(origin[0].GetInt32(), origin[1].GetInt32());

			// The legend: a byte per character, in the order the file lists them.
			var index = new Dictionary<char, byte>();
			var ids = new List<string>();
			foreach (JsonProperty entry in root.GetProperty("legend").EnumerateObject())
			{
				if (entry.Name.Length != 1)
				{
					problem = $"legend key '{entry.Name}' is not one character";
					return null;
				}
				index[entry.Name[0]] = (byte)ids.Count;
				ids.Add(entry.Value.GetString() ?? "");
			}
			layout.legendIds = ids.ToArray();
			string outsideKey = root.GetProperty("outside").GetString() ?? "#";
			if (outsideKey.Length != 1 || !index.TryGetValue(outsideKey[0], out byte outsideIndex))
			{
				problem = $"outside '{outsideKey}' is not in the legend";
				return null;
			}
			layout.outside = outsideIndex;

			JsonElement rows = root.GetProperty("rows");
			if (rows.GetArrayLength() != layout.Height)
			{
				problem = $"{rows.GetArrayLength()} rows for a height of {layout.Height}";
				return null;
			}
			layout.cells = new byte[layout.Width * layout.Height];
			int y = 0;
			foreach (JsonElement row in rows.EnumerateArray())
			{
				string s = row.GetString() ?? "";
				if (s.Length != layout.Width)
				{
					problem = $"row {y} is {s.Length} cells, not {layout.Width}";
					return null;
				}
				for (int x = 0; x < s.Length; x++)
				{
					if (!index.TryGetValue(s[x], out byte b))
					{
						problem = $"cell ({x}, {y}) is '{s[x]}', which the legend does not name";
						return null;
					}
					layout.cells[x + y * layout.Width] = b;
				}
				y++;
			}

			var rooms = new List<Room>();
			if (root.TryGetProperty("rooms", out JsonElement roomList))
			{
				foreach (JsonElement r in roomList.EnumerateArray())
				{
					rooms.Add(new Room
					{
						Id = r.GetProperty("id").GetString() ?? "",
						Name = Str(r, "name"),
						Kind = Str(r, "kind"),
						Rect = RectOf(r.GetProperty("rect")),
						Wing = Str(r, "wing"),
						Hidden = r.TryGetProperty("hidden", out JsonElement hid) && hid.GetBoolean(),
						Counted = r.TryGetProperty("counted", out JsonElement cnt) && cnt.GetBoolean(),
						Glyph = Str(r, "glyph"),
					});
				}
			}
			layout.Rooms = rooms;
			layout.Doors = Gaps(root, "doors");
			layout.HiddenWays = Gaps(root, "hidden");

			var pieces = new List<Piece>();
			if (root.TryGetProperty("furniture", out JsonElement furniture))
			{
				foreach (JsonElement f in furniture.EnumerateArray())
				{
					JsonElement at = f.GetProperty("at");
					pieces.Add(new Piece
					{
						Kind = Str(f, "kind"),
						Room = Str(f, "room"),
						At = new Vector2(at[0].GetSingle(), at[1].GetSingle()),
						Index = f.TryGetProperty("index", out JsonElement i) ? i.GetInt32() : -1,
						Slot = f.TryGetProperty("slot", out JsonElement sl) ? sl.GetInt32() : -1,
						Wall = f.TryGetProperty("wall", out JsonElement wl) && wl.GetBoolean(),
						Facing = Str(f, "facing"),
					});
				}
			}
			layout.Furniture = pieces;
			return layout;
		}
		catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
		{
			problem = $"{path}: {e.Message}";
			return null;
		}
	}

	private static string Str(JsonElement e, string name) =>
		e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

	public static Rect2I RectOf(JsonElement r) => new(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32());

	private static IReadOnlyList<Gap> Gaps(JsonElement root, string name)
	{
		var list = new List<Gap>();
		if (!root.TryGetProperty(name, out JsonElement arr))
			return list;
		foreach (JsonElement g in arr.EnumerateArray())
			list.Add(new Gap { Room = Str(g, "room"), Rect = RectOf(g.GetProperty("rect")), Onto = Str(g, "onto") });
		return list;
	}

	/// <summary>A room by id, or null.</summary>
	public Room RoomById(string id)
	{
		foreach (Room r in Rooms)
			if (r.Id == id)
				return r;
		return null;
	}

	/// <summary>The room a cell is in (its rectangle), or null for a hall, a door or a wall.</summary>
	public Room RoomAt(Vector2I cell)
	{
		foreach (Room r in Rooms)
			if (r.Rect.HasPoint(cell))
				return r;
		return null;
	}
}
