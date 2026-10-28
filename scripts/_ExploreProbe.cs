using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// Drives one chapter's exploration headless, from the real game scene, and checks what the side
// events promise now that they are placed rather than timed (.ai/side-events.md, "Finding the event").
//
// Run, one stage per process (a run's statics are reset when its director is made):
//   "$GODOT_BIN" --headless --path . scenes/_ExploreProbe.tscn -- --stage=3 [--seed=4242] [--no-complete]
//
// What it checks, as PASS / FAIL lines:
//   1. placed at start: the event's sites and the wayfinder exist before anyone moves, inside their
//      bands, on open ground, the wayfinder at least 75 degrees off the event's bearing, and dormant;
//   2. deterministic: laying the same chapter out again from the same seed gives the same places;
//   3. hidden: nothing is marked until it is seen or revealed;
//   4. the wayfinder reveals: walking onto it marks every site of the event;
//   5. it stays put across unload and reload: the player is sent far enough away that the site's
//      chunks are forgotten, then back, and the decor round the site is the same both visits and
//      never inside its footprint;
//   6. it starts on arrival, not before: dormant at 900 px, begun once the player is on it;
//   7. (unless --no-complete) it can be finished: the probe stands on whatever the event wants and
//      bleeds its elites, the way _BossProbe bleeds a boss, and reports whether it was won.
//      --slow-bleed bleeds them at 4% of their health a second instead of a sixth a frame, so a
//      fight with a phase in it lives to reach it: on the Swamp the lantern-tender rekindles a
//      lantern at half health, the objective moves to that lantern, the probe stands in it until it
//      is out, and only then comes back for the tender (LanternTenderEnemy logs both beats).
// It teleports rather than walks: the walking is _BalanceHarness --explore's job. Saving is off.
// WHAT IT CANNOT TELL YOU: whether anything is visible, readable or fun. It renders nothing.
public partial class _ExploreProbe : Node
{
	[Export] public int StageIndex { get; set; } = 3;
	[Export] public long Seed { get; set; } = 4242;
	[Export] public float TimeScale { get; set; } = 4f;
	[Export] public bool Complete { get; set; } = true;
	// --log-props: name every furnished thing the visit comparison counts.
	private bool logProps;
	// --slow-bleed: elites bled at SlowBleedPerSecond of their health, carried between frames.
	private bool slowBleed;
	private float bleedCarry;
	private Enemy bleedTarget;
	private int bleedFloor;
	private const float SlowBleedPerSecond = 0.04f;

	private Node2DGame game;
	private Player player;
	private SideEventDirector director;
	private int step;
	private int wait;
	private float stepClock;
	private int passes, fails;
	private readonly List<string> notes = new();
	private List<Vector2> visitA;
	private Vector2 lookout;
	private Vector2 firstSiteAtStart;
	private LevelUpMenu levelUpMenu;
	private int settleFrames;
	private float completeClock;
	private bool finished;

	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--stage=")) StageIndex = int.Parse(arg.Substring(8));
			else if (arg.StartsWith("--seed=")) Seed = long.Parse(arg.Substring(7));
			else if (arg.StartsWith("--timescale=")) TimeScale = float.Parse(arg.Substring(12), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg == "--no-complete") Complete = false;
			else if (arg == "--log-props") logProps = true;
			else if (arg == "--slow-bleed") slowBleed = true;
		}
		ProcessMode = ProcessModeEnum.Always;
		SaveManager.WritesDisabled = true;
		Global.SelectedCharacterIdx = 1;
		Global.SelectedStageIdx = StageIndex;
		Engine.TimeScale = TimeScale;
		GameStats.ResetRunTelemetry();
		GD.Print($"EXPLOREPROBE stage={StageIndex} seed={Seed} timescale={TimeScale}");

		game = ResourceLoader.Load<PackedScene>("res://scenes/node_2d_game.tscn").Instantiate<Node2DGame>();
		game.ProceduralSeed = Seed;
		AddChild(game);
	}

	public override void _ExitTree()
	{
		Engine.TimeScale = 1.0f;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	private void Check(string what, bool ok, string detail = "")
	{
		if (ok) passes++; else fails++;
		GD.Print($"EXPLOREPROBE {(ok ? "PASS" : "FAIL")} {what}{(detail.Length > 0 ? " - " + detail : "")}");
	}

	public override void _Process(double delta)
	{
		if (finished)
			return;
		stepClock += (float)delta;
		if (player == null || !IsInstanceValid(player))
		{
			player = game.RunPlayer;
			if (player == null)
				return;
		}
		player.MaxHP = Math.Max(player.MaxHP, 100000);
		player.CurrentHP = player.MaxHP;
		AutoPickLevelUp();
		if (wait > 0)
		{
			wait--;
			return;
		}
		// After a teleport, nothing is compared until the streamer has painted and furnished every
		// chunk round the new spot: a snapshot taken half-way would differ from the next for no reason.
		if (step >= 4 && game.Terrain != null && !game.Terrain.IsSettled && settleFrames < 2000)
		{
			settleFrames++;
			return;
		}
		settleFrames = 0;
		if (step >= 3 && player.IsDead)
		{
			Finish("player died");
			return;
		}

		switch (step)
		{
			case 0:
				// Let the stage build and the first chunks settle.
				wait = 20;
				step = 1;
				break;
			case 1:
				CheckPlacement();
				break;
			case 2:
				CheckDeterminism();
				break;
			case 3:
				// Away, far enough that the event's chunks and the wayfinder's are forgotten.
				SendAway();
				break;
			case 4:
				CheckHiddenThenTakeWayfinder();
				break;
			case 5:
				CheckRevealed();
				break;
			case 6:
				FirstVisit();
				break;
			case 7:
				RecordVisitAndLeave();
				break;
			case 8:
				ComeBack();
				break;
			case 9:
				CompareVisits();
				break;
			case 10:
				Arrive();
				break;
			case 11:
				CheckBegun();
				break;
			case 12:
				DriveToCompletion((float)delta);
				break;
		}
	}

	private SideEvent Event => director?.Event != null && IsInstanceValid(director.Event) ? director.Event : null;

	private void Teleport(Vector2 to, int settleFrames = 60)
	{
		player.GlobalPosition = to;
		player.Velocity = Vector2.Zero;
		wait = settleFrames;
	}

	private void CheckPlacement()
	{
		director = game.GetNodeOrNull<SideEventDirector>("SideEventDirector");
		StageDefinition stage = StageCatalog.GetByIndex(StageIndex);
		var entry = stage != null ? SideEventDirector.ForStage(stage.Id) : null;
		if (entry == null)
		{
			Check("a chapter without an event places nothing", director != null && director.Event == null
				&& director.Wayfinder == null && PointsOfInterest.Live.Count == 0);
			Finish("no side event on this stage");
			return;
		}
		Check("the director placed the event at stage start", Event != null && Event.Sites.Count > 0,
			$"{Event?.GetType().Name} with {Event?.Sites.Count} site(s), t={game.RunSeconds:0.0}s");
		Check("the wayfinder was placed", director.Wayfinder != null, director.Wayfinder?.Kind.ToString() ?? "none");
		if (Event == null || director.Wayfinder == null)
		{
			Finish("nothing to probe");
			return;
		}

		Vector2 origin = game.StageOrigin;
		float chunk = TerrainStreamer.ChunkTiles * game.ProceduralTilePixels;
		TerrainStreamer terrain = game.Terrain;
		// The bands are where the roll lands; the open-ground search may move a site up to 1600 px.
		const float slack = 1700f;
		for (int i = 0; i < Event.Sites.Count; i++)
		{
			Vector2 p = Event.Sites[i].Position;
			float d = p.DistanceTo(origin);
			float min = SideEventDirector.EventMinChunks * chunk - slack;
			// The lanterns are a chain from the first, so later ones may lie a mire's reach further.
			float max = SideEventDirector.EventMaxChunks * chunk + slack + (i > 0 ? 2200f : 0f);
			Check($"site {i} ({Event.Sites[i].Glyph}) is in the event band", d >= min && d <= max, $"{d:0} px = {d / chunk:0.0} chunks");
			bool open = terrain == null || (!terrain.IsBlockedAtWorld(p) && !terrain.IsWallAtWorld(p)
				&& !terrain.IsFenceAtWorld(p) && !terrain.IsHazardAtWorld(p));
			Check($"site {i} stands on open ground", open && game.IsExploreGroundClear(p, 40f));
		}
		Vector2 w = director.Wayfinder.Site.Position;
		float wd = w.DistanceTo(origin);
		Check("the wayfinder is in its band", wd >= SideEventDirector.WayfinderMinChunks * chunk - slack
			&& wd <= SideEventDirector.WayfinderMaxChunks * chunk + slack, $"{wd:0} px = {wd / chunk:0.0} chunks");
		float angle = Mathf.RadToDeg(Mathf.Abs(Mathf.AngleDifference((w - origin).Angle(), (Event.Sites[0].Position - origin).Angle())));
		// The search can turn a site by a few degrees; the roll keeps 75.
		Check("the wayfinder is well off the event's bearing", angle >= 60f, $"{angle:0} degrees apart");
		Check("the event is dormant", Event.IsDormant);
		Check("nothing is marked before it is seen or revealed", !PointsOfInterest.AnyMarked);
		firstSiteAtStart = Event.Sites[0].Position;
		notes.Add($"event sites: {string.Join("; ", Event.Sites.Select(s => $"({s.Position.X:0},{s.Position.Y:0})"))}");
		notes.Add($"wayfinder: ({w.X:0},{w.Y:0}) {wd:0} px");
		step = 2;
	}

	private void CheckDeterminism()
	{
		StageDefinition stage = StageCatalog.GetByIndex(StageIndex);
		var entry = SideEventDirector.ForStage(stage.Id).Value;
		SideEvent again = entry.Create();
		int before = PointsOfInterest.Live.Count;
		ExplorePlacer placer = game.CreateExplorePlacer(1, SideEventDirector.EventMinChunks, SideEventDirector.EventMaxChunks);
		again.PlaceSites(placer);
		bool same = again.Sites.Count == Event.Sites.Count;
		for (int i = 0; same && i < again.Sites.Count; i++)
			same = again.Sites[i].Position.DistanceTo(Event.Sites[i].Position) < 0.01f;
		// Leave the run's list as it was: the copy's sites were never the run's.
		PointsOfInterest.Live.RemoveRange(before, PointsOfInterest.Live.Count - before);
		again.Free();
		Check("the same seed lays the event out in the same places", same);
		step = 3;
	}

	private void SendAway()
	{
		Vector2 origin = game.StageOrigin;
		Vector2 toEvent = (Event.Sites[0].Position - origin).Normalized();
		lookout = origin - toEvent * 6f * TerrainStreamer.ChunkTiles * game.ProceduralTilePixels;
		Teleport(lookout, 120);
		step = 4;
	}

	private void CheckHiddenThenTakeWayfinder()
	{
		Check("still nothing marked after walking away", !PointsOfInterest.AnyMarked);
		Check("still dormant", Event.IsDormant);
		// Onto the wayfinder: its chunks are loaded on arrival, and it is taken with the feet.
		Vector2 w = director.Wayfinder.Site.Position;
		Teleport(w - (player.FootPosition - player.GlobalPosition), 40);
		step = 5;
	}

	private void CheckRevealed()
	{
		bool taken = director.Wayfinder == null || !IsInstanceValid(director.Wayfinder) || director.Wayfinder.Taken;
		Check("walking onto the wayfinder takes it", taken);
		Check("taking it reveals", PointsOfInterest.Revealed);
		int marked = Event.Sites.Count(s => s.Marked);
		Check("every site of the event is marked for an arrow", marked == Event.Sites.Count, $"{marked} of {Event.Sites.Count}");
		step = 6;
	}

	// 900 px from the first site, on the side away from the rest of the event (the lanterns are a
	// chain, and 900 px south of one can be beside the next): near enough to load its ground, past
	// every arrival radius.
	private Vector2 Approach
	{
		get
		{
			Vector2 first = Event.Sites[0].Position;
			Vector2 centroid = Vector2.Zero;
			foreach (PointOfInterest site in Event.Sites)
				centroid += site.Position;
			centroid /= Event.Sites.Count;
			Vector2 away = first - centroid;
			return first + (away.LengthSquared() > 1f ? away.Normalized() : Vector2.Down) * 900f;
		}
	}

	private void FirstVisit()
	{
		Teleport(Approach, 200);
		step = 7;
	}

	private void RecordVisitAndLeave()
	{
		Check("dormant at 900 px from the site", Event.IsDormant);
		visitA = PropsNear(Event.Sites[0].Position, 700f);
		int inside = 0;
		foreach (PointOfInterest site in Event.Sites)
			inside += PropsNear(site.Position, 70f).Count;
		Check("no decor, prop or hazard inside a site's footprint", inside == 0, $"{visitA.Count} things within 700 px of site 0, {inside} on a footprint");
		Teleport(lookout, 200);
		step = 8;
	}

	private void ComeBack()
	{
		Teleport(Approach, 200);
		step = 9;
	}

	private void CompareVisits()
	{
		List<Vector2> visitB = PropsNear(Event.Sites[0].Position, 700f);
		bool same = visitA.Count == visitB.Count && visitA.Zip(visitB).All(p => p.First.DistanceTo(p.Second) < 0.5f);
		Check("the ground round the site is furnished the same after it was forgotten", same, $"{visitA.Count} then {visitB.Count}");
		if (!same)
		{
			notes.Add("first visit: " + string.Join(", ", visitA.Select(v => $"({v.X:0},{v.Y:0})")));
			notes.Add("second visit: " + string.Join(", ", visitB.Select(v => $"({v.X:0},{v.Y:0})")));
		}
		Check("the site did not move", Event.Sites[0].Position.DistanceTo(firstSiteAtStart) < 0.01f);
		step = 10;
	}

	private void Arrive()
	{
		Teleport(Event.Sites[0].Position + new Vector2(0f, 30f), 20);
		step = 11;
	}

	private void CheckBegun()
	{
		Check("it begins when the player arrives", Event.IsFound, $"t={game.RunSeconds:0.0}s");
		var label = game.GetNodeOrNull<CanvasLayer>("UIOverlay")?.GetNodeOrNull<Label>("SideEventObjective");
		Check("the objective line is up", label != null && !string.IsNullOrEmpty(label.Text), label?.Text ?? "no label");
		if (!Complete)
		{
			Finish("arrived (--no-complete)");
			return;
		}
		completeClock = 0f;
		step = 12;
	}

	// Stand on whatever the event wants - the objective arrow's target, else the nearest site not
	// yet done - clear the ordinary swarm round it, and bleed any elite there.
	private void DriveToCompletion(float delta)
	{
		completeClock += delta;
		SideEvent e = Event;
		if (e == null || e.Sites.All(s => s.Done) && PointsOfInterest.ObjectiveTarget == null)
		{
			bool won = e == null || (bool)(typeof(SideEvent).GetProperty("Succeeded",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(e) ?? false);
			Check("the event can be finished", won, $"{completeClock:0.0}s of game time after arriving");
			Check("no arrows are left once it is over", !PointsOfInterest.AnyMarked);
			Finish("event over");
			return;
		}
		if (completeClock > 240f)
		{
			Check("the event can be finished", false, "not finished after 240 s");
			Finish("gave up");
			return;
		}

		Vector2? target = PointsOfInterest.ObjectiveTarget;
		if (target == null)
		{
			float best = float.MaxValue;
			foreach (PointOfInterest site in e.Sites)
			{
				if (site.Done) continue;
				float d = site.Position.DistanceSquaredTo(player.GlobalPosition);
				if (d < best) { best = d; target = site.Position; }
			}
		}
		if (target is not Vector2 at)
			return;
		player.GlobalPosition = at;
		player.Velocity = Vector2.Zero;
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			float d = enemy.GlobalPosition.DistanceTo(at);
			if (enemy.IsMiniBoss && d < 140f && slowBleed)
			{
				// The bleed is the only damage that counts: whatever the player's own spells took since
				// the last frame is given back first, or a strong opening loadout kills the elite before
				// its phase. The bleed itself goes through TakeDamage, so a ward still cuts it.
				if (enemy != bleedTarget)
				{
					bleedTarget = enemy;
					bleedFloor = enemy.Health;
				}
				enemy.Health = Math.Max(enemy.Health, bleedFloor);
				bleedCarry += enemy.MaxHealth * SlowBleedPerSecond * delta;
				int whole = Mathf.FloorToInt(bleedCarry);
				if (whole > 0)
				{
					bleedCarry -= whole;
					enemy.TakeDamage(whole, false);
				}
				bleedFloor = enemy.Health;
			}
			else if (enemy.IsMiniBoss && d < 140f)
				enemy.TakeDamage(Math.Max(1, enemy.Health / 6 + 50), false);
			else if (!enemy.IsMiniBoss && d < 500f && e is not ThawEvent)
				enemy.QueueFree();
		}
	}

	// Everything the streamer furnished within radius of a point: children of the chunks' prop
	// holders (Node2DGame names them Props_x_y), sorted so two visits compare in order.
	private List<Vector2> PropsNear(Vector2 centre, float radius)
	{
		var found = new List<Vector2>();
		foreach (Node child in game.GetChildren())
		{
			if (!child.Name.ToString().StartsWith("Props_"))
				continue;
			foreach (Node prop in child.GetChildren())
			{
				if (prop is Node2D p2 && p2 is not Landmark && p2.GlobalPosition.DistanceTo(centre) <= radius)
				{
					found.Add(p2.GlobalPosition);
					if (logProps)
						GD.Print($"EXPLOREPROBE prop {prop.Name} ({prop.GetType().Name}) at {p2.GlobalPosition} under {child.Name}");
				}
			}
		}
		found.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
		return found;
	}

	private void AutoPickLevelUp()
	{
		levelUpMenu ??= FindNode<LevelUpMenu>(game);
		LevelUpMenu menu = levelUpMenu;
		if (menu == null || !IsInstanceValid(menu) || !menu.Visible)
			return;
		IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions();
		if (options == null || options.Count == 0)
			return;
		LevelUpOption pick = options.FirstOrDefault(o => o.IsNewUnlock) ?? options.OrderBy(o => o.NextLevel).First();
		menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, pick.AutoChoice());
	}

	private static T FindNode<T>(Node root) where T : Node
	{
		if (root is T hit)
			return hit;
		foreach (Node child in root.GetChildren())
		{
			T found = FindNode<T>(child);
			if (found != null)
				return found;
		}
		return null;
	}

	private void Finish(string why)
	{
		finished = true;
		GD.Print("");
		GD.Print($"EXPLOREPROBE RESULT stage {StageIndex} ({StageCatalog.GetByIndex(StageIndex)?.DisplayName}) seed {Seed}: "
			+ $"{passes} passed, {fails} failed - {why}");
		foreach (string note in notes)
			GD.Print($"  note: {note}");
		if (fails > 0)
			GD.PushError($"EXPLOREPROBE stage {StageIndex}: {fails} check(s) failed.");
		GetTree().Quit(fails > 0 ? 1 : 0);
	}
}
