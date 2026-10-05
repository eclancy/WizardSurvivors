using Godot;
using WizardSurvivors.scripts;

// Starts the chapter's side event, once per run (.ai/side-events.md).
//
// The table below is the whole mapping. Each entry is a chapter, the event that lives there and the
// Discovery site it pays out; UnlockCatalog says what the site gives. A chapter that is not listed
// simply has no event, which is true of four of the nine today.
//
// It starts after the opening minute and a half, not at once: the first minute of a run is the
// player assembling a build, and an objective arriving before they have one is a demand they
// cannot yet meet. It never starts once the boss has arrived.
public partial class SideEventDirector : Node
{
	public Node2DGame Game { get; set; }

	[Export] public float StartAfterSeconds { get; set; } = 90f;

	private bool started;

	public static (string SiteId, System.Func<SideEvent> Create)? ForStage(string stageId) => stageId switch
	{
		"stage_0" => ("the_grove", () => new GroveEvent()),
		"stage_1" => ("gaolers_key", () => new GaolersKeyEvent()),
		"stage_3" => ("bog_lanterns", () => new BogLanternsEvent()),
		"stage_5" => ("the_thaw", () => new ThawEvent()),
		"stage_6" => ("caravan_master", () => new CaravanMasterEvent()),
		_ => null,
	};

	public override void _Process(double delta)
	{
		if (started || Game == null || Game.RunFinished || Game.BossFightActive || Game.RunPlayer == null)
			return;
		if (Game.RunSeconds < StartAfterSeconds)
			return;

		started = true;
		Start();
	}

	/// <summary>Starts this chapter's event now, if it has one. Public so a probe can skip the wait.</summary>
	public SideEvent Start()
	{
		started = true;
		StageDefinition stage = StageCatalog.GetByIndex(Global.SelectedStageIdx);
		var entry = stage != null ? ForStage(stage.Id) : null;
		if (entry == null)
			return null;

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		SideEvent sideEvent = entry.Value.Create();
		sideEvent.Name = "SideEvent";
		sideEvent.Game = Game;
		sideEvent.SiteId = entry.Value.SiteId;
		sideEvent.PaysChest = saveManager != null && DiscoveryRewards.IsSiteExhausted(saveManager.Data, entry.Value.SiteId);
		Game.AddChild(sideEvent);
		return sideEvent;
	}
}
