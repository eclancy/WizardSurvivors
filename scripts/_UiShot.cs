using Godot;
using System.Linq;
using WizardSurvivors.scripts;

// A screenshot harness for menu work, and the reason menu work can be judged by eye at all.
//
// Earlier attempts in this project to capture the game came back as a flat clear colour from both
// the window backbuffer and a SubViewport, which is why the level-up rebuild was done against
// geometry dumps instead of pictures. The missing piece was not the capture API, it was WHEN:
// `RenderingServer.FramePostDraw` fires after the GPU has finished the frame, and that is the only
// point at which the viewport texture holds anything. _Ready and _Process both read the buffer
// before the draw, which is precisely the clear colour that was seen.
//
// Run:
//   "$GODOT" --path . scenes/_UiShot.tscn -- --scene=res://scenes/MainMenu.tscn --shot=main-menu
//
// Also dumps a GEOMETRY REPORT beside the image: every Control with its rect and font size, plus
// any control wider than the viewport and any font below the Micro floor. A picture shows that a
// screen looks wrong; the report says by how many pixels.
public partial class _UiShot : Node
{
	[Export] public string ScenePath { get; set; } = "res://scenes/MainMenu.tscn";
	[Export] public string ShotName { get; set; } = "shot";
	[Export] public int WarmupFrames { get; set; } = 10;

	// A sub-panel to open before shooting, by the NAME of the button that opens it. Half the menus
	// in this game are panels inside MainMenu that start hidden, and a hidden container is not laid
	// out - measuring one reports its combined minimum size rather than the width it would really
	// get, which reads as a false overflow. Pressing the real button runs the real code path, so
	// what gets measured is what a player would see.
	[Export] public string PressButton { get; set; } = "";

	// A button pressed AFTER the warmup rather than in _Ready, for anything that does not exist
	// until the run has been going a while. PressButton cannot reach the level-up menu at all: it
	// fires the moment the scene is instanced, and at that point the menu has not been built.
	[Export] public string PressLateButton { get; set; } = "";

	// Stops the bot taking the first option, so a level-up can be photographed instead of skipped
	// past. The dismissal exists because a gameplay shot past the first minute is otherwise always
	// the card screen over a frozen arena - this is the other half of that switch.
	[Export] public bool KeepLevelUp { get; set; } = false;

	// Grants exactly one level halfway through the warmup. The XP curve went linear and the camera
	// went wide (.ai/gameplay-design.md), and this bot dodges rather than collects, so a run no longer
	// levels inside any warmup worth waiting for. Pair with --keep-levelup to photograph the card screen.
	[Export] public bool ForceLevelUp { get; set; } = false;
	private bool levelForced;

	// Puts a boon on the forced level-up (Player.ForceBoonOnNextOffer). Boons are rare now - about
	// one level-up in five - so without this the boon card could not be photographed on demand.
	// Implies --force-levelup and --keep-levelup.
	[Export] public bool ForceBoon { get; set; } = false;
	// --focus-boon: also moves the selection onto the boon card before the shutter, so the
	// controller focus ring can be judged on the plaque rather than on a page beside it.
	[Export] public bool FocusBoon { get; set; } = false;
	private bool boonFocused;

	// Opens the chest-item picker halfway through the warmup, the same way picking up a chest does
	// (Node2DGame.OpenChestSelectionMenu), so it can be photographed without waiting for a chest.
	[Export] public bool OpenChest { get; set; } = false;
	private bool chestOpened;

	// Kills the player halfway through the warmup, so the game over screen can be photographed.
	// Several hits, because a run can carry an extra life that revives at half health.
	[Export] public bool ForceDeath { get; set; } = false;
	private bool deathForced;

	// Opens the level 4 mutation page for the starting spell over a forced level-up, the page a
	// player reaches by picking a level 4 card. It had never been photographed (.ai/level-up-screen.md)
	// and its ink was reported unreadable. Reached through reflection because the page is private to
	// LevelUpMenu and its option type is not a Variant - this is a debug harness, not an API.
	[Export] public bool ShowMutation { get; set; } = false;
	private bool mutationShown;

	// Below this is the floor ResponsiveLayout.TextRole.Micro sets, so anything under it got its
	// size from a hardcoded call site rather than from the scale.
	private const int MinReadableFontSize = 16;

	// --teleport=X,Y: moves the player to that world position a third of the way through the warmup,
	// so ground far from the start can be photographed - the endless terrain (TerrainStreamer) is
	// only worth checking by eye where it was streamed in, not round the origin it loads with.
	// Give it a few hundred --frames: the streamer paints one step per frame, nearest chunk first.
	private Vector2? teleportTo;
	private bool teleported;

	// --shore=up|down|left|right: finds the nearest straight shoreline with water that way, stands
	// the player three tiles back from it on land, and walks him into it for the rest of the
	// warmup instead of kiting. The photograph then shows where his feet stopped, and the log says
	// by how many pixels they cleared the water's collision line (positive) or overhung it
	// (negative), measured off the drawn pixels of his boots rather than off any collider. Needs a
	// stage with water (3, the Swamp, has the most) and a couple of hundred --frames. On the maze (stage 1) it
	// finds a wall instead, which the feet meet the same way.
	private Vector2I? shoreDirection;
	private Vector2I shoreCell;
	private bool shorePlaced;

	private int frames;
	private bool captured;
	private bool latePressed;

	// Gameplay shots past the first minute were impossible without this: a run pauses on its
	// first level-up about eight seconds in, and every photograph after that point came back as
	// the card screen over a frozen arena. Taking the first offered option each time is not a
	// build anybody would choose, but the subject here is the arena, not the loadout.
	// The bot. Without it every gameplay photograph was a corpse: the player stands in the spawn
	// point, the swarm closes, and the shot comes back as the game-over panel at eight seconds.
	// It only has to survive long enough to be photographed, so this is the balance harness kite
	// with nothing clever in it - move away from the weighted centre of everything close, lean
	// toward the arena middle so it cannot reverse into a wall and be pinned there.
	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };

	private void DriveMovement()
	{
		Node2D self = null;
		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is Node2D found && IsInstanceValid(found))
				self = found;
		}

		if (self == null)
			return;

		Vector2 away = Vector2.Zero;
		int counted = 0;
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is not Node2D e || !IsInstanceValid(n))
				continue;

			Vector2 offset = self.GlobalPosition - e.GlobalPosition;
			float d = offset.Length();
			if (d > 260f || d <= 0.01f)
				continue;

			away += offset / d * (1f - d / 260f);
			counted++;
		}

		Vector2 toCentre = -self.GlobalPosition;
		Vector2 desired = counted > 0
			? away.Normalized() + toCentre.Normalized() * 0.45f
			: toCentre.Normalized();
		if (desired.LengthSquared() > 0.0001f)
			desired = desired.Normalized();

		Press("move_right", "move_left", desired.X);
		Press("move_down", "move_up", desired.Y);
	}

	private static void Press(string positive, string negative, float axis)
	{
		if (axis > 0.02f)
		{
			Input.ActionRelease(negative);
			Input.ActionPress(positive, System.Math.Min(1f, axis));
		}
		else if (axis < -0.02f)
		{
			Input.ActionRelease(positive);
			Input.ActionPress(negative, System.Math.Min(1f, -axis));
		}
		else
		{
			Input.ActionRelease(positive);
			Input.ActionRelease(negative);
		}
	}

	private void DismissLevelUpMenus(Node root)
	{
		if (KeepLevelUp)
			return;

		if (root is LevelUpMenu menu && menu.Visible)
		{
			System.Collections.Generic.IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions();
			if (options != null && options.Count > 0)
				menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, options[0].SpellId);
			return;
		}

		foreach (Node child in root.GetChildren())
			DismissLevelUpMenus(child);
	}

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--scene="))
				ScenePath = arg.Substring("--scene=".Length);
			else if (arg.StartsWith("--shot="))
				ShotName = arg.Substring("--shot=".Length);
			else if (arg.StartsWith("--press-late="))
				PressLateButton = arg.Substring("--press-late=".Length);
			else if (arg.StartsWith("--press="))
				PressButton = arg.Substring("--press=".Length);
			else if (arg == "--keep-levelup")
				KeepLevelUp = true;
			else if (arg == "--force-levelup")
				ForceLevelUp = true;
			else if (arg == "--focus-boon")
			{
				FocusBoon = true;
				ForceBoon = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--force-boon")
			{
				ForceBoon = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--open-chest")
				OpenChest = true;
			else if (arg == "--force-death")
			{
				ForceDeath = true;
				// A death ends the run and pays its Arcane Energy into the save - the player's real
				// one, since this harness runs against it. Photographing a screen must not do that.
				SaveManager.WritesDisabled = true;
			}
			else if (arg == "--show-mutation")
			{
				ShowMutation = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			// Gameplay needs hundreds of frames before there is a crowd worth photographing; a
			// menu is laid out in ten.
			else if (arg.StartsWith("--frames="))
				WarmupFrames = int.Parse(arg.Substring("--frames=".Length));
			// Photographing a RUN rather than a menu needs the two selections a real run would
			// have made on the way in. Without them every gameplay shot is chapter one as
			// character zero, which is a silent wrong answer rather than an error.
			else if (arg.StartsWith("--stage="))
				Global.SelectedStageIdx = int.Parse(arg.Substring("--stage=".Length));
			else if (arg.StartsWith("--character="))
				Global.SelectedCharacterIdx = int.Parse(arg.Substring("--character=".Length));
			// Gameplay wants hundreds of frames before there is a crowd worth photographing, and
			// at 1x that is ten real seconds of waiting per shot.
			else if (arg.StartsWith("--timescale="))
				Engine.TimeScale = float.Parse(arg.Substring("--timescale=".Length));
			// The main menu as a player sees it: TitleScreen with the menu already open over the
			// art, the way every return-to-menu path loads it. MainMenu.tscn on its own draws the
			// menu on a black ground, which shows the layout but not what it sits on.
			else if (arg == "--open-menu")
				Global.OpenMenuImmediately = true;
			else if (arg.StartsWith("--shore="))
			{
				shoreDirection = arg.Substring("--shore=".Length) switch
				{
					"up" => new Vector2I(0, -1),
					"down" => new Vector2I(0, 1),
					"left" => new Vector2I(-1, 0),
					"right" => new Vector2I(1, 0),
					_ => null,
				};
				if (shoreDirection == null)
					GD.PrintErr($"_UiShot: {arg} - expected up, down, left or right");
			}
			else if (arg.StartsWith("--teleport="))
			{
				string[] xy = arg.Substring("--teleport=".Length).Split(',');
				teleportTo = new Vector2(
					float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
					float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
			}
		}

		if (!ResourceLoader.Exists(ScenePath))
		{
			GD.PrintErr($"_UiShot: no scene at '{ScenePath}'");
			GetTree().Quit(1);
			return;
		}

		var packed = GD.Load<PackedScene>(ScenePath);
		Node instance = packed?.Instantiate();
		if (instance == null)
		{
			GD.PrintErr($"_UiShot: '{ScenePath}' failed to instantiate");
			GetTree().Quit(1);
			return;
		}

		AddChild(instance);
		MoveChild(instance, 0);

		if (!string.IsNullOrEmpty(PressButton))
		{
			Button target = FindButton(instance, PressButton);
			if (target == null)
				GD.PrintErr($"_UiShot: no button named '{PressButton}'");
			else
				target.EmitSignal(BaseButton.SignalName.Pressed);
		}

		RenderingServer.FramePostDraw += OnFramePostDraw;
	}

	public override void _ExitTree()
	{
		RenderingServer.FramePostDraw -= OnFramePostDraw;
		// Left pressed, the input stays held into whatever scene loads next.
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	private static Button FindButton(Node node, string name)
	{
		if (node is Button button && button.Name == name)
			return button;
		foreach (Node child in node.GetChildren())
		{
			Button found = FindButton(child, name);
			if (found != null)
				return found;
		}
		return null;
	}

	private static Button FindButtonWithChild(Node node, string childName)
	{
		if (node is Button button && button.GetNodeOrNull(childName) != null)
			return button;
		foreach (Node child in node.GetChildren())
		{
			Button found = FindButtonWithChild(child, childName);
			if (found != null)
				return found;
		}
		return null;
	}

	private static T FindNodeOfType<T>(Node root) where T : Node
	{
		if (root is T match)
			return match;
		foreach (Node child in root.GetChildren())
		{
			T found = FindNodeOfType<T>(child);
			if (found != null)
				return found;
		}
		return null;
	}

	private void OnFramePostDraw()
	{
		// Menus build themselves over several frames - fonts resolve, containers sort, and the
		// responsive column count is re-applied on a viewport signal. Capturing the first drawn
		// frame catches a half-laid-out screen, so give it a few.
		if (captured || ++frames < WarmupFrames)
		{
			// A paused tree still draws, so the pause has to be cleared from inside the draw
			// callback rather than from _Process - which does not run while a level-up is up.
			if (captured)
				return;
			if (GetTree().Paused)
				DismissLevelUpMenus(this);
			else if (shoreDirection is Vector2I push)
			{
				if (!shorePlaced && frames >= WarmupFrames / 3)
					PlaceAtShore(push);
				// Before placement, stand still; after it, walk into the water and keep walking.
				Press("move_right", "move_left", shorePlaced ? push.X : 0f);
				Press("move_down", "move_up", shorePlaced ? push.Y : 0f);
			}
			else
				DriveMovement();
			if (teleportTo is Vector2 target && !teleported && frames >= WarmupFrames / 3
				&& FindNodeOfType<Player>(GetTree().Root) is Player traveller)
			{
				traveller.GlobalPosition = target;
				teleported = true;
				GD.Print($"_UiShot: teleported the player to {target}");
			}
			if (ForceLevelUp && !levelForced && frames >= WarmupFrames / 2
				&& FindNodeOfType<Player>(GetTree().Root) is Player player)
			{
				levelForced = true;
				player.ForceBoonOnNextOffer = ForceBoon;
				player.AddXp(System.Math.Max(1, player.XPToNextLevel - player.CurrentXP));
			}
			if (ShowMutation && levelForced && !mutationShown && frames >= WarmupFrames * 3 / 4
				&& FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu menu
				&& FindNodeOfType<Player>(GetTree().Root) is Player owner
				&& owner.GetEquippedSpells().Count > 0)
			{
				mutationShown = true;
				SpellData spell = owner.GetEquippedSpells()[0];
				var option = new LevelUpOption
				{
					SpellId = spell.Id,
					DisplayName = spell.Name,
					Icon = spell.Icon,
					NextLevel = 4,
					MaxLevel = 7,
					IsEvolutionMilestone = true,
					MilestoneLevel = 4,
					EvolutionChoices = System.Linq.Enumerable.ToList(spell.GetEvolutionOptionsForLevel(4)),
				};
				typeof(LevelUpMenu).GetMethod("BuildEvolutionSelectionButtons",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
					?.Invoke(menu, new object[] { option });
			}
			if (FocusBoon && levelForced && !boonFocused && frames >= WarmupFrames * 3 / 4
				&& FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu boonMenu)
			{
				// The boon card is the Button carrying the plaque's clasp (LevelUpMenu.AddBoonClasp).
				Button boonCard = FindButtonWithChild(boonMenu, "BoonClasp");
				if (boonCard != null)
				{
					boonFocused = true;
					boonCard.GrabFocus();
				}
			}
			if (ForceDeath && !deathForced && frames >= WarmupFrames / 2
				&& FindNodeOfType<Player>(GetTree().Root) is Player victim)
			{
				deathForced = true;
				for (int i = 0; i < 6 && !victim.IsDead; i++)
					victim.TakeDamage(System.Math.Max(1, victim.MaxHP * 10));
			}
			if (OpenChest && !chestOpened && frames >= WarmupFrames / 2
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame game)
			{
				chestOpened = true;
				game.OpenChestSelectionMenu();
			}
			return;
		}
		// Late press, then one more settle pass before the shutter. Returning here rather than
		// capturing means the frame counter keeps running and the screen the press opened gets the
		// same warmup the first one had.
		if (!string.IsNullOrEmpty(PressLateButton) && !latePressed)
		{
			latePressed = true;
			Button target = FindButton(GetTree().Root, PressLateButton);
			if (target == null)
			{
				GD.PrintErr($"_UiShot: no button named '{PressLateButton}'");
			}
			else
			{
				target.EmitSignal(BaseButton.SignalName.Pressed);
				frames = 0;
				return;
			}
		}

		captured = true;

		Report();
		if (shorePlaced)
			ReportShore();

		Image image = GetViewport()?.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.PrintErr("_UiShot: no viewport texture");
			GetTree().Quit(1);
			return;
		}

		string path = $"user://{ShotName}.png";
		Error err = image.SavePng(path);
		GD.Print(err == Error.Ok
			? $"_UiShot: {ProjectSettings.GlobalizePath(path)}"
			: $"_UiShot: save failed ({err})");
		GetTree().Quit(err == Error.Ok ? 0 : 1);
	}

	// Nearest cell C whose shore faces the player: water at C, either side of it and behind it, and
	// four tiles of open, unhazardous land in front three cells wide - a straight stretch of shore,
	// so what is measured is the edge rather than a corner. The player's feet go three tiles back.
	private void PlaceAtShore(Vector2I d)
	{
		if (Node2DGame.Current?.Terrain is not TerrainStreamer terrain
			|| FindNodeOfType<Player>(GetTree().Root) is not Player player)
			return;

		float tile = terrain.TilePixels;
		Vector2 Centre(Vector2I c) => terrain.CellOriginWorld(c) + new Vector2(tile, tile) * 0.5f;
		// Water or maze wall: the two things the feet are stopped by.
		bool Water(Vector2I c) => terrain.IsBlockedAtWorld(Centre(c)) || terrain.IsWallAtWorld(Centre(c));
		bool Open(Vector2I c)
		{
			Vector2 w = Centre(c);
			return !terrain.IsBlockedAtWorld(w) && !terrain.IsWallAtWorld(w) && !terrain.IsHazardAtWorld(w);
		}

		var side = new Vector2I(System.Math.Abs(d.Y), System.Math.Abs(d.X));
		Vector2I from = terrain.CellOfWorld(player.FootPosition);
		int bestDistance = int.MaxValue;
		Vector2I? best = null;
		const int Reach = 60;
		for (int dy = -Reach; dy <= Reach; dy++)
		{
			for (int dx = -Reach; dx <= Reach; dx++)
			{
				int distance = dx * dx + dy * dy;
				if (distance >= bestDistance)
					continue;
				Vector2I c = from + new Vector2I(dx, dy);
				if (!Water(c) || !Water(c + side) || !Water(c - side) || !Water(c + d))
					continue;
				bool clear = true;
				for (int k = 1; k <= 4 && clear; k++)
					for (int s = -1; s <= 1 && clear; s++)
						clear = Open(c - d * k + side * s);
				if (!clear)
					continue;
				bestDistance = distance;
				best = c;
			}
		}

		if (best is not Vector2I found)
		{
			GD.PrintErr($"_UiShot: no straight shore (water or wall) {d} within {Reach} tiles of {from}");
			shoreDirection = null;
			return;
		}

		shoreCell = found;
		shorePlaced = true;
		Vector2 feet = Centre(found - d * 3);
		player.GlobalPosition += feet - player.FootPosition;
		GD.Print($"_UiShot: shore cell {found}, player's feet put at {feet}, walking {d}");
	}

	// How far the drawn boots stopped from the water's collision line: positive is dry land between
	// them, negative is boot over water. Measured off the pixels of the frame on screen - the cell's
	// bottom six rows, which is the boots and the contact line under them - not off any collider,
	// so it cannot agree with the physics just because it was computed from it.
	private void ReportShore()
	{
		if (Node2DGame.Current?.Terrain is not TerrainStreamer terrain
			|| FindNodeOfType<Player>(GetTree().Root) is not Player player
			|| player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") is not AnimatedSprite2D sprite
			|| sprite.SpriteFrames?.GetFrameTexture(sprite.Animation, sprite.Frame) is not Texture2D texture)
			return;

		Image image = texture.GetImage();
		if (image.IsCompressed())
			image.Decompress();
		const int BootRows = 6;
		int w = image.GetWidth(), h = image.GetHeight();
		int minX = w, maxX = -1;
		for (int y = h - BootRows; y < h; y++)
			for (int x = 0; x < w; x++)
				if (image.GetPixel(x, y).A > 0.5f)
				{
					minX = System.Math.Min(minX, x);
					maxX = System.Math.Max(maxX, x);
				}
		if (maxX < 0)
			return;

		Vector2 scale = sprite.GlobalScale;
		float left = (minX - w / 2f) * scale.X, right = (maxX + 1 - w / 2f) * scale.X;
		if (sprite.FlipH)
			(left, right) = (-right, -left);
		float bootLeft = sprite.GlobalPosition.X + left;
		float bootRight = sprite.GlobalPosition.X + right;
		float bootBottom = sprite.GlobalPosition.Y + (h / 2f) * scale.Y;
		float bootTop = sprite.GlobalPosition.Y + (h / 2f - BootRows) * scale.Y;

		// LevelTilePainter.BlockingPolygon's inset for a water edge tile: 0.18 of the cell. A maze
		// wall is a full cell (EnsureWallSources), so its line is the cell edge.
		float tile = terrain.TilePixels;
		Vector2 cell = terrain.CellOriginWorld(shoreCell);
		bool wall = terrain.IsWallAtWorld(cell + new Vector2(tile, tile) * 0.5f);
		float inset = wall ? 0f : tile * 0.18f;
		Vector2I d = shoreDirection ?? Vector2I.Zero;
		(string edge, float line, float clearance) = (d.X, d.Y) switch
		{
			(0, 1) => ("bottom", cell.Y + inset, cell.Y + inset - bootBottom),
			(0, -1) => ("top", cell.Y + tile - inset, bootTop - (cell.Y + tile - inset)),
			(-1, 0) => ("left", cell.X + tile - inset, bootLeft - (cell.X + tile - inset)),
			_ => ("right", cell.X + inset, cell.X + inset - bootRight),
		};
		GD.Print($"_UiShot: SHORE {d}: boots x {bootLeft:0.0}..{bootRight:0.0}, y {bootTop:0.0}..{bootBottom:0.0};"
			+ $" footprint centre {player.FootPosition}; {(wall ? "wall" : "water")} collision line {line:0.0};"
			+ $" boots' {edge} edge clears it by {clearance:0.0} px (negative = over the water)");
	}

	private static string Truncate(string text)
	{
		text = text?.Replace('\n', ' ') ?? string.Empty;
		return text.Length <= 40 ? text : text.Substring(0, 40) + "...";
	}

	private void Report()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		GD.Print($"_UiShot: viewport {viewport.X}x{viewport.Y}");

		int overflow = 0;
		int tiny = 0;
		Walk(GetChild(0), viewport, ref overflow, ref tiny);

		GD.Print($"_UiShot: {overflow} control(s) wider than the viewport, {tiny} below {MinReadableFontSize}px");
	}

	private void Walk(Node node, Vector2 viewport, ref int overflow, ref int tiny)
	{
		if (node is Control control)
		{
			// A control wider than the viewport is the horizontal-scroll bug in its raw form: the
			// content simply does not fit the 720 units keep_width pins us to.
			if (control.Size.X > viewport.X + 0.5f)
			{
				overflow++;
				GD.Print($"  WIDE  {control.GetPath()}  {control.Size.X:0}px > {viewport.X:0}px"
					+ $"  (min {control.GetCombinedMinimumSize().X:0})");
				// A container cannot shrink below its widest child's MINIMUM, so the child with the
				// largest combined minimum is the one actually setting the width. Naming it turns
				// "this screen is too wide" into "this label is too wide", which is fixable.
				Control worst = null;
				foreach (Node child in control.GetChildren())
				{
					if (child is not Control c)
						continue;
					if (worst == null || c.GetCombinedMinimumSize().X > worst.GetCombinedMinimumSize().X)
						worst = c;
				}
				if (worst != null)
					GD.Print($"        widest child: {worst.Name} ({worst.GetType().Name}) min {worst.GetCombinedMinimumSize().X:0}"
						+ (worst is Label l ? $"  text=\"{Truncate(l.Text)}\"" : string.Empty));
			}

			if (control is ScrollContainer scroll
				&& scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled)
			{
				GD.Print($"  HSCROLL  {scroll.GetPath()}  mode={scroll.HorizontalScrollMode}");
			}

			// GetThemeFontSize resolves the whole chain - local override, then theme, then
			// default - so this is the size actually drawn rather than the size someone asked for.
			if (control is Label or Button or RichTextLabel)
			{
				int size = control.GetThemeFontSize("font_size");
				if (size > 0 && size < MinReadableFontSize)
				{
					tiny++;
					GD.Print($"  TINY  {control.GetPath()}  {size}px");
				}
			}
		}

		foreach (Node child in node.GetChildren())
			Walk(child, viewport, ref overflow, ref tiny);
	}
}
