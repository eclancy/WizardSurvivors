using Godot;
using System.Linq;
using WizardSurvivors.scripts;

// Places the chapter's side event and its wayfinder at stage start (.ai/side-events.md).
//
// The table below is the whole mapping. Each entry is a chapter, the event that lives there, the
// Discovery site it pays out and the wayfinder that reveals it; UnlockCatalog says what the site
// gives. A chapter that is not listed has no event - and so no wayfinder, since there would be
// nothing for it to point at - which is true of four of the nine today.
//
// NO TIMER (2026-10-22). This used to start the event at 90 pace seconds, near wherever the player
// was. Now, the moment the run's terrain exists, it lays the event out in the world from the run's
// terrain seed - four to six chunks from the start, on open ground (ExplorePlacer) - and leaves it
// there, dormant, for the player to find. The event begins itself when he arrives (SideEvent). The
// first-minute grace the timer gave ("an objective arriving before they have a build is a demand they
// cannot yet meet") now comes from distance: the event is half a minute's walk away at the least, and
// going there is the player's choice.
//
// THE WAYFINDER. Each event chapter also hides one object much nearer the start, 1.5 to 2.5 chunks
// out on a bearing at least 75 degrees from the event's: take it and every point of interest on the
// map gets an arrow at the edge of the screen for the rest of the run (PointsOfInterest, PoiPointer).
// It is the Arcane Compass by default; the Cursed Dungeon has the Gaoler's own map nailed to a post,
// and the Frozen Waste a cairn with a bearing plate (WayfinderPickup).
public partial class SideEventDirector : Node
{
	public Node2DGame Game { get; set; }

	/// <summary>This run's event, placed and waiting; null on a chapter without one.</summary>
	public SideEvent Event { get; private set; }

	/// <summary>This run's wayfinder; null on a chapter without an event, and freed once taken.</summary>
	public WayfinderPickup Wayfinder { get; private set; }

	// The bands, in terrain chunks (1536 px) from the start. At the wizard's 220 px/s a chunk is
	// seven seconds of straight walking; the swarm and the ground make it more (.ai/side-events.md
	// gives the measured walks). Near enough that the trip is a real part of a 15-minute run, far
	// enough that nobody falls over the event without going looking.
	public const float EventMinChunks = 4f;
	public const float EventMaxChunks = 6f;
	public const float WayfinderMinChunks = 1.5f;
	public const float WayfinderMaxChunks = 2.5f;
	// So the wayfinder never sits on the way to the event: its arrow should send the player
	// somewhere he was not already going.
	public const float WayfinderMinSeparationDegrees = 75f;

	public static (string SiteId, System.Func<SideEvent> Create, WayfinderKind Wayfinder)? ForStage(string stageId) => stageId switch
	{
		"stage_0" => ("the_grove", () => new GroveEvent(), WayfinderKind.Compass),
		"stage_1" => ("gaolers_key", () => new GaolersKeyEvent(), WayfinderKind.GaolersMap),
		"stage_3" => ("bog_lanterns", () => new BogLanternsEvent(), WayfinderKind.Compass),
		"stage_5" => ("the_thaw", () => new ThawEvent(), WayfinderKind.BearingCairn),
		"stage_6" => ("caravan_master", () => new CaravanMasterEvent(), WayfinderKind.Compass),
		_ => null,
	};

	private PoiPointer poiPointer;
	private bool logPlacement;

	public override void _Ready()
	{
		PointsOfInterest.Reset();
		logPlacement = OS.GetCmdlineUserArgs().Contains("--explore-log");
		if (Game == null)
			return;

		StageDefinition stage = StageCatalog.GetByIndex(Global.SelectedStageIdx);
		var entry = stage != null ? ForStage(stage.Id) : null;
		if (entry == null)
		{
			if (logPlacement)
				GD.Print($"EXPLORE stage {Global.SelectedStageIdx}: no side event, no wayfinder");
			return;
		}

		ExplorePlacer placer = Game.CreateExplorePlacer(stream: 1, EventMinChunks, EventMaxChunks);
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		SideEvent sideEvent = entry.Value.Create();
		sideEvent.Name = "SideEvent";
		sideEvent.Game = Game;
		sideEvent.SiteId = entry.Value.SiteId;
		sideEvent.PaysChest = saveManager != null && DiscoveryRewards.IsSiteExhausted(saveManager.Data, entry.Value.SiteId);
		sideEvent.PlaceSites(placer);

		// The wayfinder, on its own stream so a change to how an event lays itself out never moves it.
		ExplorePlacer near = Game.CreateExplorePlacer(stream: 2, EventMinChunks, EventMaxChunks);
		float separation = Mathf.DegToRad(WayfinderMinSeparationDegrees);
		float bearing = placer.EventBearing + separation + near.NextFloat() * (Mathf.Tau - 2f * separation);
		WayfinderKind kind = entry.Value.Wayfinder;
		Vector2 wayfinderAt = near.AtBearing(bearing, WayfinderMinChunks, WayfinderMaxChunks, WayfinderPickup.Footprint);
		var site = PointsOfInterest.Add(new PointOfInterest(wayfinderAt, WayfinderPickup.GlyphFor(kind), isWayfinder: true));
		var wayfinder = new WayfinderPickup { Name = "Wayfinder", Game = Game, Kind = kind, Site = site };

		// Before the first chunk is furnished (LoadInitialTerrain runs after this), so no tree, rock
		// or spike plate is ever put down on a lantern's pool or across the caravan's road.
		foreach (var (centre, radius) in placer.Reserved)
			Game.ReserveExploreGround(centre, radius);
		foreach (var (centre, radius) in near.Reserved)
			Game.ReserveExploreGround(centre, radius);

		Game.AddChild(sideEvent);
		Game.AddChild(wayfinder);
		Event = sideEvent;
		Wayfinder = wayfinder;

		var overlay = Game.GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (overlay != null)
		{
			poiPointer = new PoiPointer { Name = "PoiPointer" };
			overlay.AddChild(poiPointer);
		}

		if (logPlacement)
		{
			Vector2 origin = Game.StageOrigin;
			GD.Print($"EXPLORE stage {Global.SelectedStageIdx} seed {Game.TerrainSeed}: {sideEvent.GetType().Name} "
				+ $"bearing {Mathf.RadToDeg(placer.EventBearing):0} deg; wayfinder {kind} at ({wayfinderAt.X:0},{wayfinderAt.Y:0}) "
				+ $"{wayfinderAt.DistanceTo(origin):0} px, bearing {Mathf.RadToDeg(bearing) % 360f:0} deg");
			for (int i = 0; i < sideEvent.Sites.Count; i++)
			{
				Vector2 p = sideEvent.Sites[i].Position;
				GD.Print($"EXPLORE   site {i} {sideEvent.Sites[i].Glyph} at ({p.X:0},{p.Y:0}), {p.DistanceTo(origin):0} px from the start");
			}
		}
	}

	public override void _ExitTree()
	{
		if (poiPointer != null && IsInstanceValid(poiPointer))
			poiPointer.QueueFree();
		PointsOfInterest.Reset();
	}

	public override void _Process(double delta)
	{
		if (Game == null || PointsOfInterest.Live.Count == 0)
			return;
		PointsOfInterest.Suspended = Game.BossFightActive;
		if (Game.RunFinished || Game.BossFightActive)
			return;

		// What is on screen now is seen: the camera's view of the world, grown by a little so a
		// lantern whose flame is just over the edge counts. Read from the canvas transform, so it is
		// right on both canvas shapes and at any zoom.
		Viewport viewport = GetViewport();
		if (viewport == null)
			return;
		Transform2D toWorld = viewport.GetCanvasTransform().AffineInverse();
		Rect2 screen = viewport.GetVisibleRect();
		Vector2 a = toWorld * screen.Position;
		Vector2 b = toWorld * screen.End;
		var view = new Rect2(a, b - a).Abs().Grow(SeenMargin);
		PointsOfInterest.UpdateSeen(view);
		if (logPlacement)
			LogDiscoveries();
	}

	// World pixels past the screen's edge at which a point counts as seen.
	private const float SeenMargin = 80f;

	private int loggedSeen;

	private void LogDiscoveries()
	{
		int seen = 0;
		foreach (PointOfInterest p in PointsOfInterest.Live)
			if (p.Seen) seen++;
		if (seen == loggedSeen)
			return;
		loggedSeen = seen;
		GD.Print($"EXPLORE t={Game.RunSeconds:0.0}s seen {seen} of {PointsOfInterest.Live.Count} points, revealed={PointsOfInterest.Revealed}");
	}

	/// <summary>
	/// Starts this chapter's event now, where it stands, as if the player had arrived. For probes and
	/// _UiShot --win-event; a run never calls it.
	/// </summary>
	public SideEvent Start()
	{
		if (Event == null || !IsInstanceValid(Event))
			return null;
		Event.Arrive();
		return Event;
	}
}
