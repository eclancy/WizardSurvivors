using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WizardSurvivors.scripts;

// THE COURT'S RULES (.ai/endgame-castle.md 5, ChapterRules' hooks). "A court with no one left in it."
//
// The run is an ordinary one (2: the pace, the XP curve and the level gates are .ai/run-pacing.md's,
// untouched); what is the Court's is the building and what there is to do in it. This:
//
//   - reads the building (CourtLayout, from court_layout.json) and lays out what is in it: every room's
//     furniture as the player comes within reach of the room, freed again when he is far away (the room's
//     memory is on its CourtRooms object, never on a node); the carved glyph beside every door and the
//     signs at every junction naming what lies down each branch (5.2); and the tapestry over each hidden
//     way, a pixel off true, with the draught moving in front of it (5.5);
//   - knows which room the player stands in, and tells the room (CourtRooms: the fowl, the ward, the
//     guard, the doors) and anyone listening (RoomFound / RoomEntered - the hooks the castle's secrets
//     and achievements attach to, piece C9) - the first visit of a run also marks the room in the secret
//     run state (SecretDirector.StateOf("court_room:<id>"));
//   - puts the line under the timer: the rooms found, of thirteen (hook 7);
//   - and answers Node2DGame's hooks: spawn points off screen that land on the building's floor rather
//     than in its walls (2); the shut keepsake alcoves and the hidden ways closed to placement (3); the
//     boss in a room near the player rather than in a hall (4 - the Seneschal's agent, C5, owns what he
//     does there); and the way home at the throne room doors (5).
public partial class CourtRules : ChapterRules
{
	/// <summary>Furniture lying on the floor - tables, pews, the map table: under every body.</summary>
	public const int FloorFurnitureZ = DrawLayers.Decal - 3;
	/// <summary>What stands on a table: the platters.</summary>
	public const int OnTableZ = DrawLayers.Decal - 2;
	/// <summary>What lies on a platter: a fowl, or its bones.</summary>
	public const int OnPlatterZ = DrawLayers.Decal - 1;
	/// <summary>What hangs on a wall's face: portraits, the door glyphs, the junction signs.</summary>
	public const int WallDecorZ = DrawLayers.Decal;

	/// <summary>A room's furniture is built when the player is this close to its floor (world pixels)...</summary>
	public const float BuildRadius = 3400f;
	/// <summary>...and freed when he is this far: past the painted ground (TerrainStreamer's five chunks).</summary>
	public const float FreeRadius = 4600f;

	private const string SpriteDir = "res://assets/bonelight/world/court/";

	/// <summary>The building; null if court_layout.json is missing (the chapter is then open ground).</summary>
	public CourtLayout Layout { get; private set; }

	/// <summary>The rooms, in the layout's order.</summary>
	public IReadOnlyList<CourtRoom> Rooms => rooms;

	/// <summary>Seconds since the run started, for the rooms' little animations.</summary>
	public float Elapsed { get; private set; }

	/// <summary>
	/// A room entered for the first time this run (its id). Fires before the room's own OnEntered. The
	/// achievements' hook (C9): finding a room is an achievement; "Every Room" a Feat.
	/// </summary>
	public event Action<string> RoomFound;

	/// <summary>Any entry into a room (its id), the first included.</summary>
	public event Action<string> RoomEntered;

	public bool IsRoomFound(string id) => found.Contains(id);

	/// <summary>The rooms found this run, counted ones and places alike.</summary>
	public IReadOnlyCollection<string> RoomsFoundThisRun => found;

	/// <summary>Of the thirteen, how many found this run.</summary>
	public int CountedRoomsFound => rooms.Count(r => r.Found && r.Place.Counted);

	/// <summary>The room the player is standing in, or null in a hall.</summary>
	public CourtRoom CurrentRoom { get; private set; }

	/// <summary>The Keepsake Doors this save has opened (of ten).</summary>
	public int KeepsakeDoorsOpen => Room<KeepsakeDoorsRoom>()?.OpenCount ?? 0;

	/// <summary>The save, or null in a tool run without one.</summary>
	public SaveData Save => GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;

	/// <summary>The first room of a kind of behaviour, for a hook or a probe.</summary>
	public T Room<T>() where T : CourtRoom => rooms.OfType<T>().FirstOrDefault();

	private readonly List<CourtRoom> rooms = new();
	private readonly Dictionary<string, CourtRoom> roomsById = new();
	private readonly HashSet<string> found = new();
	private Node2D holder;
	private readonly List<(Sprite2D Sprite, Rect2 Gap)> tapestries = new();
	private float streamTimer;
	private Label objective;
	private float objectiveFlash;
	private TextureRect map;
	private ColorRect mapDot;
	private float mapScale;
	private int mapFoundShown = -1;
	private readonly RandomNumberGenerator rng = new();
	private Dictionary<string, (int W, int H, string Anchor)> manifest;
	private readonly Dictionary<string, Texture2D> textures = new();

	// --- Hook 1: run start ---------------------------------------------------------------------------

	public override void RunStarted()
	{
		rng.Randomize();
		Layout = CourtLayout.Load(Game.StageOrigin);
		if (Layout == null)
			return;
		LoadManifest();

		// The holder sits under the run, not under this Node: a Node is not a CanvasItem, and anything
		// under it would leave the arena's y-sort and draw over or under every body at once.
		holder = new Node2D { Name = "CourtFurniture", YSortEnabled = true };
		Game.AddChild(holder);

		foreach (CourtLayout.Room place in Layout.Rooms)
		{
			CourtRoom room = CourtRooms.Create(this, place);
			rooms.Add(room);
			roomsById[place.Id] = room;
		}
		BuildMarkers();
		BuildHiddenWays();
		StreamRooms(Game.RunPlayer?.FootPosition ?? Game.StageOrigin);

		string[] args = OS.GetCmdlineUserArgs();
		if (args.Contains("--court-log") || args.Contains("--court-fowl-check"))
		{
			GD.Print($"COURT layout: {Layout.Rooms.Count} rooms ({Layout.CountedRooms} counted), {Layout.Plaques.Count} door glyphs, "
				+ $"{Layout.Signs.Count} junction signs, {Layout.HiddenWays.Count} hidden way(s), keepsake doors open {KeepsakeDoorsOpen}/10");
			foreach (CourtLayout.Room r in Layout.Rooms)
				GD.Print($"COURT room {r.Id,-18} centre ({r.Centre.X:0}, {r.Centre.Y:0}) world {r.World}");
		}
		fowlCheck = args.Contains("--court-fowl-check");
		tour = args.Contains("--court-tour");
	}

	public override void _ExitTree()
	{
		if (holder != null && IsInstanceValid(holder))
			holder.QueueFree();
		if (map != null && IsInstanceValid(map))
			map.QueueFree();
		holder = null;
		map = null;
		// Let go of the textures now rather than whenever the collector gets to this object, or the engine
		// counts them as still in use when the game quits.
		textures.Clear();
		mapTexture?.Dispose();
		mapTexture = null;
	}

	// --- The furniture -----------------------------------------------------------------------------

	private void LoadManifest()
	{
		manifest = new Dictionary<string, (int, int, string)>();
		string path = SpriteDir + "court.json";
		if (!Godot.FileAccess.FileExists(path))
		{
			GD.PushError($"CourtRules: {path} is missing; the Court's rooms will stand empty. Run python tools/art/props_court.py");
			return;
		}
		using JsonDocument doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
		foreach (JsonProperty p in doc.RootElement.EnumerateObject())
		{
			if (p.Name.StartsWith("_", StringComparison.Ordinal) || p.Value.ValueKind != JsonValueKind.Object)
				continue;
			manifest[p.Name] = (p.Value.GetProperty("width").GetInt32(), p.Value.GetProperty("height").GetInt32(),
				p.Value.GetProperty("anchor").GetString() ?? "centre");
		}
	}

	/// <summary>A court sprite's texture, loaded once; null (with one error) if it is missing or not imported.</summary>
	public Texture2D Texture(string name)
	{
		if (textures.TryGetValue(name, out Texture2D known))
			return known;
		Texture2D t = ResourceLoader.Exists(SpriteDir + name + ".png") ? GD.Load<Texture2D>(SpriteDir + name + ".png") : null;
		if (t == null)
			GD.PushError($"CourtRules: court sprite '{name}' is missing or not imported (run python tools/art/props_court.py, then --import).");
		textures[name] = t;
		return t;
	}

	/// <summary>
	/// A court sprite standing at a world point, by its anchor (court.json): "centre" is centred on the
	/// point; "ground" and "foot" stand their bottom edge's middle on it, so they sort with the bodies by
	/// where they stand. Not added to anything.
	/// </summary>
	public Sprite2D MakeSprite(string name, Vector2 at, int z)
	{
		var sprite = new Sprite2D { Name = name, Texture = Texture(name), Position = at, ZIndex = z, ZAsRelative = false };
		string anchor = manifest != null && manifest.TryGetValue(name, out var m) ? m.Anchor : "centre";
		if (anchor != "centre" && sprite.Texture != null)
		{
			sprite.Centered = false;
			Vector2 size = sprite.Texture.GetSize();
			sprite.Offset = new Vector2(-Mathf.Round(size.X / 2f), -size.Y);
		}
		return sprite;
	}

	/// <summary>
	/// A piece of the layout's furniture, built under <paramref name="parent"/>: its own sprite (the kind's
	/// name, or <paramref name="sprite"/>), lying under the bodies if it is flat and standing among them if
	/// it stands up. A kind with no sprite (a chest's spot, a vent's) places nothing.
	/// </summary>
	public Sprite2D PlaceSprite(Node2D parent, AuthoredLayout.Piece piece, string sprite = null, int? z = null)
	{
		string name = sprite ?? piece.Kind.Replace('_', '-');
		if (manifest == null || !manifest.TryGetValue(name, out var m))
			return null;
		int layer = z ?? (m.Anchor == "centre" ? (piece.Wall ? WallDecorZ : FloorFurnitureZ) : DrawLayers.Bodies);
		Sprite2D s = MakeSprite(name, Layout.CellToWorld(piece.At), layer);
		parent.AddChild(s);
		return s;
	}

	// The glyph beside every door and the sign at every junction (5.2): carved into the wall, so they are
	// built once for the run, all of them - a few hundred sprites standing still cost nothing to keep.
	private void BuildMarkers()
	{
		var markers = new Node2D { Name = "Glyphs", YSortEnabled = true };
		holder.AddChild(markers);
		foreach (CourtLayout.Marker p in Layout.Plaques)
		{
			Sprite2D plaque = MakeSprite("plaque", p.At, WallDecorZ);
			markers.AddChild(plaque);
			if (p.Glyphs.Count > 0)
			{
				Sprite2D glyph = MakeSprite("glyph-" + p.Glyphs[0], Vector2.Zero, WallDecorZ);
				glyph.ZAsRelative = true;
				glyph.ZIndex = 0;
				plaque.AddChild(glyph);
			}
		}
		foreach (CourtLayout.Marker s in Layout.Signs)
		{
			Sprite2D plate = MakeSprite("sign", s.At, WallDecorZ);
			markers.AddChild(plate);
			Sprite2D arrow = MakeSprite("arrow-" + s.Direction, new Vector2(-46f, 0f), 0);
			arrow.ZAsRelative = true;
			plate.AddChild(arrow);
			for (int i = 0; i < s.Glyphs.Count && i < 3; i++)
			{
				Sprite2D g = MakeSprite("glyph-" + s.Glyphs[i], new Vector2(-21f + 28f * i, 0f), 0);
				g.ZAsRelative = true;
				plate.AddChild(g);
			}
		}
	}

	// The hidden ways (5.5): a tapestry over a real gap - the wall's own cap and face drawn on over the
	// gap and its two jambs, the cloth hung a pixel off true - standing at the wall's foot so it sorts
	// with the bodies: anything in the gap is behind it, anything in the hall before it. And the draught:
	// a few motes drifting out of its foot into the hall, the one thing on a wall that moves.
	private void BuildHiddenWays()
	{
		foreach ((string room, Rect2 gap, Rect2I cells) in Layout.HiddenWays)
		{
			Vector2 foot = Layout.CellToWorld(new Vector2(cells.Position.X + cells.Size.X / 2f, cells.End.Y));
			Sprite2D tapestry = MakeSprite("tapestry-hidden", foot, DrawLayers.Bodies);
			holder.AddChild(tapestry);
			tapestries.Add((tapestry, gap));

			var draught = new CpuParticles2D
			{
				Name = "Draught",
				Position = foot + new Vector2(0f, -6f),
				Amount = 7,
				Lifetime = 3.2f,
				Preprocess = 3.2f,
				EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
				EmissionRectExtents = new Vector2(gap.Size.X * 0.45f, 3f),
				Direction = new Vector2(0f, 1f),
				Spread = 35f,
				Gravity = new Vector2(0f, -2f),
				InitialVelocityMin = 6f,
				InitialVelocityMax = 14f,
				ScaleAmountMin = 2f,
				ScaleAmountMax = 3f,
				// Bone's pale stop, faint: dust in a draught, cold, never the warm glint of a secret.
				Color = new Color(0.83f, 0.86f, 0.92f, 0.35f),
				ZIndex = DrawLayers.Decal,
				ZAsRelative = false,
			};
			holder.AddChild(draught);
		}
	}

	private void StreamRooms(Vector2 feet)
	{
		foreach (CourtRoom room in rooms)
		{
			float d = CourtLayout.DistanceTo(room.Place.World, feet);
			if (d <= BuildRadius)
				room.Build(holder);
			else if (d > FreeRadius)
				room.Teardown();
		}
	}

	// --- Each frame -----------------------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (Layout == null || Game == null)
			return;
		float d = (float)delta;
		Elapsed += d;
		Player player = Game.RunPlayer;
		if (player == null)
			return;
		Vector2 feet = player.FootPosition;

		streamTimer -= d;
		if (streamTimer <= 0f)
		{
			streamTimer = 0.25f;
			StreamRooms(feet);
		}

		CourtLayout.Room at = Layout.RoomAt(feet);
		CourtRoom here = at != null && roomsById.TryGetValue(at.Id, out CourtRoom r) ? r : null;
		if (here != CurrentRoom)
		{
			CurrentRoom = here;
			if (here != null)
				Enter(here);
		}
		foreach (CourtRoom room in rooms)
			room.Tick(d, room == CurrentRoom);

		// A hidden way's tapestry thins while he is under it, so he can see himself in the gap.
		foreach ((Sprite2D sprite, Rect2 gap) in tapestries)
		{
			if (!IsInstanceValid(sprite))
				continue;
			bool under = gap.Grow(20f).HasPoint(feet);
			float target = under ? 0.45f : 1f;
			Color c = sprite.Modulate;
			sprite.Modulate = new Color(c.R, c.G, c.B, Mathf.MoveToward(c.A, target, d * 3f));
		}

		UpdateObjective(d);
		UpdateMap(feet);
		if (fowlCheck)
			StepFowlCheck(d, player);
		else if (tour)
			StepTour(d, player);
	}

	private void Enter(CourtRoom room)
	{
		bool first = !room.Found;
		if (first)
		{
			room.Found = true;
			found.Add(room.Place.Id);
			// The secret run state, where the castle's secrets read their sites (SecretDirector, A4/C9).
			SecretDirector secrets = Game.GetNodeOrNull<SecretDirector>("SecretDirector");
			if (secrets != null)
				secrets.StateOf("court_room:" + room.Place.Id).Done = true;
			if (room.Place.Counted)
			{
				SfxPlayer.Global(SfxCatalog.BearingFound);
				objectiveFlash = 4f;
				if (objective != null && IsInstanceValid(objective))
					objective.Text = $"{room.Place.Name} - {CountedRoomsFound} of {Layout.CountedRooms} rooms";
			}
			RoomFound?.Invoke(room.Place.Id);
		}
		RoomEntered?.Invoke(room.Place.Id);
		room.OnEntered(first);
	}

	// --- Hook 7: the line under the timer ------------------------------------------------------------

	public override Control CreateObjective()
	{
		if (Layout == null)
			return null;
		objective = ObjectiveLabel(ObjectiveText());
		return objective;
	}

	private string ObjectiveText() => $"Rooms found {CountedRoomsFound} / {Layout.CountedRooms}";

	private void UpdateObjective(float delta)
	{
		if (objective == null || !IsInstanceValid(objective) || objectiveFlash <= 0f)
			return;
		objectiveFlash -= delta;
		if (objectiveFlash <= 0f)
			objective.Text = ObjectiveText();
	}

	// --- The Map Room's map (5.2): the game's only minimap, and only here, and only once earned -------

	/// <summary>Shows the Court's map for the rest of the run (the Map Room's reward). Idempotent.</summary>
	public void ShowMap()
	{
		if (map != null && IsInstanceValid(map))
			return;
		var ui = Game.GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (ui == null)
			return;
		float side = ResponsiveLayout.IsNarrow(ui) ? 120f : 150f;
		mapScale = side / Mathf.Max(Layout.Grid.Width, Layout.Grid.Height);
		map = new TextureRect
		{
			Name = "CourtMap",
			AnchorLeft = 1f,
			AnchorRight = 1f,
			OffsetLeft = -side - 12f,
			OffsetRight = -12f,
			OffsetTop = 124f,
			OffsetBottom = 124f + side,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			// Its rect is the size asked for, not the texture's 192 cells: KeepSize let it run off the screen edge.
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		mapDot = new ColorRect
		{
			Name = "You",
			Size = new Vector2(5f, 5f),
			// The player's own warm light: the one warm point on the map.
			Color = new Color(1f, 0.85f, 0.45f, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		map.AddChild(mapDot);
		ui.AddChild(map);
		SafeArea.ApplyTo(map);
		mapFoundShown = -1;
	}

	private void UpdateMap(Vector2 feet)
	{
		if (map == null || !IsInstanceValid(map))
			return;
		if (mapFoundShown != found.Count)
		{
			mapFoundShown = found.Count;
			// One texture for the run, redrawn in place as rooms are found.
			Image image = RenderMap();
			if (mapTexture == null)
				mapTexture = ImageTexture.CreateFromImage(image);
			else
				mapTexture.Update(image);
			map.Texture = mapTexture;
		}
		Vector2I cell = Layout.WorldToCell(feet);
		mapDot.Position = new Vector2(cell.X * mapScale - 2f, cell.Y * mapScale - 2f);
	}

	// One pixel a cell: the halls a dim stone, a room unseen dimmer, a room found washed in its wing's
	// colour, a hidden room nothing at all until it is found. The throne room doors, a pale mark.
	private ImageTexture mapTexture;

	private Image RenderMap()
	{
		int w = Layout.Grid.Width, h = Layout.Grid.Height;
		Image image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		var hall = new Color(0.16f, 0.20f, 0.27f, 0.85f);
		var unseen = new Color(0.10f, 0.13f, 0.18f, 0.85f);
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				var cell = new Vector2I(x, y);
				if (!Layout.Grid.IsWalkable(cell))
					continue;
				AuthoredLayout.Room room = Layout.Grid.RoomAt(cell);
				Color c = hall;
				if (room != null)
				{
					bool seen = found.Contains(room.Id);
					if (room.Hidden && !seen)
						continue;
					c = seen ? WingColour(room.Wing) : unseen;
				}
				else if (IsHiddenWayCell(cell) && !found.Contains("solar"))
					continue;
				image.SetPixel(x, y, c);
			}
		}
		Rect2I doors = Layout.Grid.RoomById("throne_doors")?.Rect ?? default;
		if (doors.Size != Vector2I.Zero)
			for (int x = doors.Position.X + doors.Size.X / 2 - 2; x <= doors.Position.X + doors.Size.X / 2 + 2; x++)
				image.SetPixel(x, doors.Position.Y, new Color(0.86f, 0.88f, 0.93f, 1f));
		return image;
	}

	private bool IsHiddenWayCell(Vector2I cell)
	{
		foreach ((string _, Rect2 _, Rect2I cells) in Layout.HiddenWays)
			if (cells.HasPoint(cell))
				return true;
		return false;
	}

	private static Color WingColour(string wing) => wing switch
	{
		"red" => new Color(0.42f, 0.13f, 0.16f, 0.95f),
		"blue" => new Color(0.16f, 0.22f, 0.48f, 0.95f),
		"green" => new Color(0.16f, 0.32f, 0.33f, 0.95f),
		"gold" => new Color(0.47f, 0.37f, 0.12f, 0.95f),
		_ => new Color(0.36f, 0.40f, 0.48f, 0.95f),
	};

	// --- Hook 2: spawn points on the building's floor ------------------------------------------------

	/// <summary>
	/// The usual ring off screen, but a bearing that runs into wall walks on along the same line, and then
	/// back toward the screen's edge, to the first floor of the building it meets - so a ring that is two
	/// thirds wall near the building's edge still spawns where there is somewhere to stand. Node2DGame vets
	/// the point as it vets any (walls, a long walk round, hook 3, separation).
	/// </summary>
	public override bool TryPickSpawnPoint(Vector2 playerPosition, out Vector2 point)
	{
		point = default;
		if (Layout == null)
			return false;
		Viewport viewport = GetViewport();
		Camera2D camera = viewport?.GetCamera2D();
		Vector2 zoom = camera?.Zoom ?? Vector2.One;
		Vector2 half = (viewport?.GetVisibleRect().Size ?? new Vector2(1280f, 720f)) / new Vector2(Mathf.Max(0.01f, zoom.X), Mathf.Max(0.01f, zoom.Y)) * 0.5f;
		float angle = rng.Randf() * Mathf.Tau;
		var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		float toEdge = Mathf.Min(half.X / Mathf.Max(0.0001f, Mathf.Abs(dir.X)), half.Y / Mathf.Max(0.0001f, Mathf.Abs(dir.Y)));
		float radius = toEdge * 1.08f + rng.RandfRange(40f, 330f);
		for (float r = radius; r < radius + 520f; r += CourtLayout.TilePixels)
		{
			Vector2 p = playerPosition + dir * r;
			if (Layout.IsFloor(p))
			{
				point = p;
				return true;
			}
		}
		for (float r = radius - CourtLayout.TilePixels; r > toEdge + 24f; r -= CourtLayout.TilePixels)
		{
			Vector2 p = playerPosition + dir * r;
			if (Layout.IsFloor(p))
			{
				point = p;
				return true;
			}
		}
		return false;
	}

	// --- Hook 3: closed to placement ------------------------------------------------------------------

	public override bool IsPlacementBlocked(Vector2 point)
	{
		if (Layout == null)
			return false;
		foreach ((string _, Rect2 gap, Rect2I _) in Layout.HiddenWays)
			if (gap.HasPoint(point))
				return true;
		KeepsakeDoorsRoom doors = keepsakeDoors ??= Room<KeepsakeDoorsRoom>();
		return doors != null && doors.IsInShutAlcove(point);
	}

	private KeepsakeDoorsRoom keepsakeDoors;

	// --- Hook 4: where the boss appears ----------------------------------------------------------------

	/// <summary>
	/// In a room near the player - never in a hall, and never the room he is standing in - so the fight
	/// opens across a threshold. The Seneschal's own agent (C5, 5.6) decides what he does from there.
	/// </summary>
	public override Vector2? BossSpawnPoint(BossDefinition boss)
	{
		Player player = Game?.RunPlayer;
		if (Layout == null || player == null)
			return null;
		foreach (CourtLayout.Room room in Layout.RoomsNear(player.FootPosition, 3200f))
		{
			if (room == CurrentRoom?.Place || room.Kind == "keepsake")
				continue;
			if (CourtLayout.DistanceTo(room.World, player.FootPosition) < 300f)
				continue;
			return room.Centre;
		}
		return null;
	}

	// --- Hook 5: the way home is the throne room doors ----------------------------------------------

	/// <summary>In front of the throne room doors (5.3): when the Seneschal falls they open, and walking
	/// through them ends the run. The arcane portal opens here, and the portal's pointer points at it
	/// across the house.</summary>
	public override Vector2? VictoryPortalPoint(Vector2 bossFellAt)
	{
		if (Layout == null)
			return null;
		Rect2 doors = Layout.ThroneDoors;
		return new Vector2(doors.GetCenter().X, doors.End.Y + 110f);
	}

	// --- The headless fowl check (--court-fowl-check) ----------------------------------------------
	//
	// Run it with _MazeProbe on the Court (it plays the run with an immortal player):
	//   "$GODOT_BIN" --headless --path . scenes/_MazeProbe.tscn -- --stage=12 --seconds=20 --bot=still --court-fowl-check --no-save-writes
	// It walks the player onto a platter through the fowl's own Area2D, so what is measured is the
	// pickup a player meets: the fowl is taken, his health rises by a flask's worth, the platter shows
	// its bones; and then the clock is run on to the minute when a fowl comes back.

	private bool fowlCheck;
	private float fowlCheckClock;
	private int fowlCheckStage;
	private int hpBefore;
	private int eatenBefore;

	/// <summary>Health before and after the last fowl eaten, for the check.</summary>
	public (int Before, int After) LastFowlHeal { get; set; }

	private void StepFowlCheck(float delta, Player player)
	{
		fowlCheckClock += delta;
		GreatHallRoom hall = Room<GreatHallRoom>();
		if (hall == null)
		{
			GD.PrintErr("COURT fowl check FAIL: the layout has no Great Hall");
			fowlCheck = false;
			return;
		}
		switch (fowlCheckStage)
		{
			case 0 when fowlCheckClock > 2f:
				// Stand him in the hall so it is built, hurt him, and put him on platter 5.
				hall.Build(holder);
				player.CurrentHP = Math.Max(1, player.MaxHP - 25);
				hpBefore = player.CurrentHP;
				eatenBefore = hall.FowlEaten;
				player.GlobalPosition = hall.PlatterPosition(5);
				fowlCheckStage = 1;
				fowlCheckClock = 0f;
				break;
			case 1 when fowlCheckClock <= 0.5f && hall.FowlEaten == eatenBefore:
				// Held hurt until the fowl is taken: a harness that keeps him immortal (_MazeProbe) refills
				// his health every frame before this runs, and the pickup fires between frames.
				player.CurrentHP = hpBefore;
				break;
			case 1 when fowlCheckClock > 0.5f || hall.FowlEaten != eatenBefore:
			{
				bool taken = hall.FowlEaten == eatenBefore + 1 && !hall.PlatterFull(5);
				(int before, int after) = LastFowlHeal;
				bool healed = after - before == GreatHallRoom.FowlHeal || (after == player.MaxHP && after > before);
				GD.Print($"COURT fowl check: walked onto platter 5 at {hall.PlatterPosition(5)}: "
					+ $"{(taken ? "the fowl was taken through its Area2D" : "the fowl was NOT taken")}; "
					+ $"health {before} -> {after} (+{after - before}, a flask is +{GreatHallRoom.FowlHeal}; set to {hpBefore} before); "
					+ $"{hall.FowlOnTheTables}/12 on the tables - {(taken && healed ? "PASS" : "FAIL")}");
				player.GlobalPosition = Game.StageOrigin;
				fowlCheckStage = 2;
				fowlCheckClock = 0f;
				break;
			}
			case 2:
				// Run the hall's own clock on: it is the room's Tick that brings a fowl back.
				hall.Tick(GreatHallRoom.FowlReturnSeconds + 0.1f, false);
				GD.Print($"COURT fowl check: after {GreatHallRoom.FowlReturnSeconds:0} s {hall.FowlOnTheTables}/12 on the tables, "
					+ $"platter 5 {(hall.PlatterFull(5) ? "laid again" : "still empty")} - {(hall.PlatterFull(5) && hall.FowlOnTheTables == 12 ? "PASS" : "FAIL")}");
				fowlCheck = false;
				break;
		}
	}

	// --- The headless tour (--court-tour) ----------------------------------------------------------
	//
	// Stands the player in every room in turn - the Solar through its hidden way - and says what each
	// did: whether it was found, its chest or guard, the ward, the map. With _MazeProbe on the Court:
	//   "$GODOT_BIN" --headless --path . scenes/_MazeProbe.tscn -- --stage=12 --seconds=40 --bot=still --court-tour --no-save-writes
	// The rooms' reactions are logged by what they leave in the world (a chest, an enemy, a map), not
	// by asking the rooms what they meant to do.

	private bool tour;
	private float tourClock;
	private int tourStop = -1;
	private int foundEvents;
	private int foundBeforeTour;

	private void StepTour(float delta, Player player)
	{
		if (tourStop < 0)
		{
			RoomFound += _ => foundEvents++;
			// The stair is found on the first frame, before the tour starts listening.
			foundBeforeTour = found.Count;
			tourStop = 0;
			tourClock = -1.5f;
		}
		tourClock += delta;
		if (tourClock < 0.8f)
			return;
		tourClock = 0f;
		if (tourStop > 0)
		{
			CourtRoom was = rooms[tourStop - 1];
			int chests = Game.GetChildren().Count(n => n is ChestReward);
			string extra = was switch
			{
				ChapelRoom c => $"ward {(c.WardActive ? "holding" : "down")}",
				TreasuryRoom t => $"the Chamberlain {(t.Guard != null ? $"stands ({t.Guard.Name}, {t.Guard.Health} HP)" : "absent")}",
				MapRoom => $"map {(map != null && IsInstanceValid(map) ? "shown" : "NOT shown")}",
				KitchensRoom k => $"{k.FlasksLeft} flask(s) in the larder",
				KeepsakeDoorsRoom kd => $"{kd.OpenCount}/10 doors open",
				GreatHallRoom g => $"{g.FowlOnTheTables}/12 fowl on the tables",
				_ => "",
			};
			GD.Print($"COURT tour: {was.Place.Id,-18} found={was.Found} (current room {CurrentRoom?.Place.Id ?? "a hall"}); "
				+ $"chests in the world {chests}; {extra}");
		}
		if (tourStop >= rooms.Count)
		{
			GD.Print($"COURT tour: done - {found.Count} places found, {CountedRoomsFound}/{Layout.CountedRooms} counted rooms, "
				+ $"{foundEvents} RoomFound events after the start - {(CountedRoomsFound == Layout.CountedRooms && foundEvents == found.Count - foundBeforeTour ? "PASS" : "FAIL")}");
			tour = false;
			return;
		}
		CourtRoom next = rooms[tourStop++];
		player.GlobalPosition = next.Place.Kind == "keepsake" ? next.Place.Centre + new Vector2(0f, 60f) : next.Place.Centre + new Vector2(0f, 40f);
	}
}
