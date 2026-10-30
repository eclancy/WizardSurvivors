using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WizardSurvivors.scripts;

/// <summary>
/// The Court's building, in world coordinates (.ai/endgame-castle.md 5): its rooms, doors, hidden ways,
/// the glyphs over the doors and the signs at the junctions, the Keepsake Doors' alcoves and the throne
/// room doors - read from court_layout.json, which tools/art/court_layout.py writes and checks.
/// </summary>
/// <remarks>
/// The grid itself is <see cref="AuthoredLayout"/>'s (LevelGenerator.Authored.cs reads it to make the
/// ground); this adds the sections only the Court has and turns cells into world pixels.
///
/// A CELL AND A WORLD POINT. The layout's <c>origin</c> cell is world tile (0, 0), whose north-west
/// corner is the stage origin - where the player starts - and a tile is 48 world pixels. So a point
/// given in cells (fractional, as the file gives furniture) is at
/// <c>stageOrigin + (cell - origin) * 48</c>; <see cref="CellToWorld"/> and <see cref="WorldToCell"/> are
/// the two directions.
///
/// FOR THE SENESCHAL (5.6, piece C5): <see cref="RoomsNear"/> names the rooms near a point, nearest
/// first, which is what "he steps out through a servants' door to another room one or two rooms away"
/// needs; <see cref="RoomAt"/> says which room a point is in. Distances are straight lines from the point
/// to each room's rectangle (0 inside it); a boss that must choose by walking distance asks
/// MazeNavigation.PathTilesAt of the rooms this returns.
/// </remarks>
public sealed class CourtLayout
{
	public const int TilePixels = 48;

	/// <summary>One room or place of the building, in world pixels.</summary>
	public sealed class Room
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		/// <summary>What it does (great_hall, chapel, ... start, throne_doors): CourtRooms keys its behaviour on it.</summary>
		public string Kind { get; init; } = "";
		/// <summary>red, blue, green, gold, or "" for the middle of the house.</summary>
		public string Wing { get; init; } = "";
		public string Glyph { get; init; } = "";
		/// <summary>Reached only through a hidden way (the Solar).</summary>
		public bool Hidden { get; init; }
		/// <summary>One of the thirteen ("Every Room"); false for the stair and the throne room's vestibule.</summary>
		public bool Counted { get; init; }
		/// <summary>The room's floor, in cells of the layout.</summary>
		public Rect2I Cells { get; init; }
		/// <summary>The room's floor, in world pixels.</summary>
		public Rect2 World { get; init; }
		public Vector2 Centre => World.GetCenter();
	}

	/// <summary>A carved glyph on a wall: over a door (a plaque) or at a junction (a sign, with an arrow).</summary>
	public sealed class Marker
	{
		/// <summary>The room it names (a plaque), or the rooms down its branch, nearest first (a sign).</summary>
		public IReadOnlyList<string> Rooms { get; init; } = Array.Empty<string>();
		public IReadOnlyList<string> Glyphs { get; init; } = Array.Empty<string>();
		/// <summary>A sign's branch: n, e, s or w. Empty for a plaque.</summary>
		public string Direction { get; init; } = "";
		/// <summary>Where its middle is, in world pixels.</summary>
		public Vector2 At { get; init; }
		/// <summary>On a wall's face (true) or its top (false).</summary>
		public bool OnFace { get; init; }
	}

	/// <summary>One of the Keepsake Doors' ten alcoves.</summary>
	public sealed class Alcove
	{
		public int Index { get; init; }
		/// <summary>The alcove behind the door, world pixels.</summary>
		public Rect2 World { get; init; }
		/// <summary>The door's mouth through the corridor's wall, world pixels.</summary>
		public Rect2 Mouth { get; init; }
	}

	public AuthoredLayout Grid { get; }
	public Vector2 StageOrigin { get; }
	public IReadOnlyList<Room> Rooms { get; }
	public IReadOnlyList<Marker> Plaques { get; }
	public IReadOnlyList<Marker> Signs { get; }
	public IReadOnlyList<Alcove> Alcoves { get; }
	/// <summary>The hidden ways (5.5): a real gap in a wall with a tapestry over it, world pixels, and its room.</summary>
	public IReadOnlyList<(string Room, Rect2 World, Rect2I Cells)> HiddenWays { get; }
	/// <summary>The throne room doors in the vestibule's north wall, world pixels.</summary>
	public Rect2 ThroneDoors { get; }

	private CourtLayout(AuthoredLayout grid, Vector2 stageOrigin, List<Room> rooms, List<Marker> plaques,
		List<Marker> signs, List<Alcove> alcoves, Rect2 throneDoors)
	{
		Grid = grid;
		StageOrigin = stageOrigin;
		Rooms = rooms;
		Plaques = plaques;
		Signs = signs;
		Alcoves = alcoves;
		ThroneDoors = throneDoors;
		var hidden = new List<(string, Rect2, Rect2I)>();
		foreach (AuthoredLayout.Gap g in grid.HiddenWays)
			hidden.Add((g.Room, CellsToWorld(g.Rect), g.Rect));
		HiddenWays = hidden;
	}

	/// <summary>
	/// The Court, placed so its start cell is the stage origin; null with an error if the layout is
	/// missing or will not read (the chapter is then open ground, and its rules have nothing to place).
	/// </summary>
	public static CourtLayout Load(Vector2 stageOrigin)
	{
		AuthoredLayout grid = AuthoredLayout.For(StageEnvironmentKind.Court);
		if (grid == null)
		{
			GD.PushError("CourtLayout: court_layout.json is missing or unreadable - run python tools/art/court_layout.py.");
			return null;
		}
		try
		{
			using JsonDocument doc = JsonDocument.Parse(grid.Source);
			JsonElement root = doc.RootElement;
			Vector2 CellPoint(JsonElement at) =>
				stageOrigin + (new Vector2(at[0].GetSingle(), at[1].GetSingle()) - (Vector2)grid.Origin) * TilePixels;
			Rect2 CellRect(Rect2I r) =>
				new(stageOrigin + (Vector2)(r.Position - grid.Origin) * TilePixels, (Vector2)r.Size * TilePixels);

			var rooms = new List<Room>();
			foreach (AuthoredLayout.Room r in grid.Rooms)
			{
				rooms.Add(new Room
				{
					Id = r.Id, Name = r.Name, Kind = r.Kind, Wing = r.Wing, Glyph = r.Glyph,
					Hidden = r.Hidden, Counted = r.Counted, Cells = r.Rect, World = CellRect(r.Rect),
				});
			}

			var plaques = new List<Marker>();
			foreach (JsonElement p in root.GetProperty("plaques").EnumerateArray())
			{
				plaques.Add(new Marker
				{
					Rooms = new[] { p.GetProperty("room").GetString() ?? "" },
					Glyphs = new[] { p.GetProperty("glyph").GetString() ?? "" },
					At = CellPoint(p.GetProperty("at")),
					OnFace = p.GetProperty("face").GetBoolean(),
				});
			}

			var signs = new List<Marker>();
			foreach (JsonElement s in root.GetProperty("signs").EnumerateArray())
			{
				signs.Add(new Marker
				{
					Rooms = Strings(s.GetProperty("rooms")),
					Glyphs = Strings(s.GetProperty("glyphs")),
					Direction = s.GetProperty("dir").GetString() ?? "",
					At = CellPoint(s.GetProperty("at")),
					OnFace = s.GetProperty("face").GetBoolean(),
				});
			}

			var alcoves = new List<Alcove>();
			foreach (JsonElement a in root.GetProperty("alcoves").EnumerateArray())
			{
				alcoves.Add(new Alcove
				{
					Index = a.GetProperty("index").GetInt32(),
					World = CellRect(AuthoredLayout.RectOf(a.GetProperty("rect"))),
					Mouth = CellRect(AuthoredLayout.RectOf(a.GetProperty("mouth"))),
				});
			}

			Rect2 throne = CellRect(AuthoredLayout.RectOf(root.GetProperty("throneDoors")));
			return new CourtLayout(grid, stageOrigin, rooms, plaques, signs, alcoves, throne);
		}
		catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
		{
			GD.PushError($"CourtLayout: court_layout.json will not read ({e.Message}) - run python tools/art/court_layout.py.");
			return null;
		}
	}

	private static string[] Strings(JsonElement arr)
	{
		var list = new List<string>();
		foreach (JsonElement e in arr.EnumerateArray())
			list.Add(e.GetString() ?? "");
		return list.ToArray();
	}

	// --- Cells and the world ------------------------------------------------------------------------

	/// <summary>A point given in layout cells (fractional, as the file gives furniture), in world pixels.</summary>
	public Vector2 CellToWorld(Vector2 cellPoint) => StageOrigin + (cellPoint - (Vector2)Grid.Origin) * TilePixels;

	/// <summary>The layout cell a world point is in.</summary>
	public Vector2I WorldToCell(Vector2 world)
	{
		Vector2 local = (world - StageOrigin) / TilePixels;
		return new Vector2I(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y)) + Grid.Origin;
	}

	/// <summary>A rectangle of layout cells, in world pixels.</summary>
	public Rect2 CellsToWorld(Rect2I cells) =>
		new(StageOrigin + (Vector2)(cells.Position - Grid.Origin) * TilePixels, (Vector2)cells.Size * TilePixels);

	/// <summary>True where a body can stand: inside the building and not wall.</summary>
	public bool IsFloor(Vector2 world) => Grid.IsWalkable(WorldToCell(world));

	// --- Rooms ----------------------------------------------------------------------------------------

	/// <summary>A room by id, or null.</summary>
	public Room RoomById(string id)
	{
		foreach (Room r in Rooms)
			if (r.Id == id)
				return r;
		return null;
	}

	/// <summary>The room a world point stands in, or null in a hall, a doorway or a wall. Allocation-free.</summary>
	public Room RoomAt(Vector2 world)
	{
		Vector2I cell = WorldToCell(world);
		foreach (Room r in Rooms)
			if (r.Cells.HasPoint(cell))
				return r;
		return null;
	}

	/// <summary>
	/// The rooms within <paramref name="radius"/> world pixels of a point, nearest first (straight-line
	/// distance to each room's floor, 0 for the room the point is in). Places that are not rooms - the
	/// stair, the throne room's vestibule - are left out unless <paramref name="includePlaces"/>, and a
	/// hidden room unless <paramref name="includeHidden"/>. For the Seneschal (5.6), who leaves for a room
	/// one or two away; allocates its list, so call it on a decision, not every frame.
	/// </summary>
	public List<Room> RoomsNear(Vector2 world, float radius, bool includeHidden = false, bool includePlaces = false)
	{
		var found = new List<(float, Room)>();
		foreach (Room r in Rooms)
		{
			if ((r.Hidden && !includeHidden) || (!r.Counted && !includePlaces))
				continue;
			float d = DistanceTo(r.World, world);
			if (d <= radius)
				found.Add((d, r));
		}
		found.Sort((a, b) => a.Item1.CompareTo(b.Item1));
		var list = new List<Room>(found.Count);
		foreach ((float _, Room r) in found)
			list.Add(r);
		return list;
	}

	/// <summary>Straight-line distance from a point to a rectangle; 0 inside it.</summary>
	public static float DistanceTo(Rect2 rect, Vector2 p)
	{
		float dx = Mathf.Max(Mathf.Max(rect.Position.X - p.X, 0f), p.X - rect.End.X);
		float dy = Mathf.Max(Mathf.Max(rect.Position.Y - p.Y, 0f), p.Y - rect.End.Y);
		return Mathf.Sqrt(dx * dx + dy * dy);
	}

	/// <summary>The thirteen rooms "Every Room" counts.</summary>
	public int CountedRooms
	{
		get
		{
			int n = 0;
			foreach (Room r in Rooms)
				if (r.Counted)
					n++;
			return n;
		}
	}
}
