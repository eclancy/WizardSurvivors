using Godot;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// The run's half of the stage secrets (issue #20; .ai/stage-secrets.md). Added by Node2DGame beside
// the SideEventDirector, before the first chunk is furnished, so the keepsake's ground is reserved
// before any tree can be put on it.
//
// What it holds:
//   - SecretRunState (section 7.4): progress per site, keyed by a stable id - a flower pen's
//     Landmark.Key - and NEVER on the site's node, because a pen is freed with its chunk and made
//     again when the player walks back. A half-walked lap is still half-walked.
//   - The landmark hook (7.1): every flower pen on a chapter whose catalog has the Fenced Bloom gets
//     a FencedBloom attached as it streams in.
//   - The keepsake (the First Tree), placed once at stage start from the terrain seed, ten to twelve
//     chunks out on open ground (ExplorePlacer, the side events' placer, on its own stream).
//   - The seeds a bloom drops, and which one is in the player's hands.
//   - Finding: the toast, the page mark the first time ever, and the keepsake's found level.
public partial class SecretDirector : Node
{
	public Node2DGame Game { get; set; }

	/// <summary>The chapter's StageCatalog id.</summary>
	public string StageId { get; private set; } = "";

	/// <summary>One site's progress for the run. A lap round a pen is Progress, signed by direction.</summary>
	public sealed class SiteState
	{
		public float Progress;
		public bool Done;
	}

	private readonly Dictionary<string, SiteState> sites = new();

	public SiteState StateOf(string key)
	{
		if (!sites.TryGetValue(key, out SiteState state))
			sites[key] = state = new SiteState();
		return state;
	}

	/// <summary>The chapter's keepsake, placed at stage start; null on a chapter without one.</summary>
	public FirstTree Keepsake { get; private set; }

	/// <summary>The seed in the player's hands, if any. Only one at a time.</summary>
	public SecretSeed Carried { get; set; }

	/// <summary>The keepsake band, in terrain chunks (1536 px) from the start: 70 to 85 s of walking.</summary>
	public const float KeepsakeMinChunks = 10f;
	public const float KeepsakeMaxChunks = 12f;

	// Its own placer stream, so a change to how the side event lays itself out never moves it.
	private const int KeepsakeStream = 7;

	private readonly HashSet<string> foundThisRun = new();
	private bool hooked;
	private SecretToast toast;

	public Player Player => Game?.RunPlayer;

	/// <summary>Secrets stand down when the boss arrives, as chests and the side event do (section 4).</summary>
	public bool Active => Game != null && !Game.RunFinished && !Game.BossFightActive;

	public override void _Ready()
	{
		if (Game == null)
			return;
		StageId = StageCatalog.GetByIndex(Global.SelectedStageIdx)?.Id ?? "";

		if (SecretCatalog.StageHas(StageId, SecretCatalog.FencedBloomId))
		{
			Landmark.Spawned += OnLandmarkSpawned;
			hooked = true;
		}

		if (SecretCatalog.StageHas(StageId, SecretCatalog.FirstTreeId))
		{
			ExplorePlacer placer = Game.CreateExplorePlacer(KeepsakeStream, KeepsakeMinChunks, KeepsakeMaxChunks);
			Vector2 at = placer.EventSite(FirstTree.Footprint);
			foreach (var (centre, radius) in placer.Reserved)
				Game.ReserveExploreGround(centre, radius);
			Keepsake = new FirstTree { Name = "FirstTree", Director = this, Position = at };
			Game.AddChild(Keepsake);
			if (OS.GetCmdlineUserArgs().Contains("--explore-log"))
				GD.Print($"SECRETS stage {StageId}: the First Tree at {at} ({at.DistanceTo(Game.StageOrigin) / 1536f:0.0} chunks out)");
		}

		toast = new SecretToast { Name = "SecretToast" };
		AddChild(toast);
	}

	public override void _ExitTree()
	{
		if (hooked)
			Landmark.Spawned -= OnLandmarkSpawned;
		hooked = false;
	}

	// Spawned fires from the landmark's _EnterTree, while its parent is still adding it; a child
	// added then is refused ("parent busy"), so the bloom is attached at the end of the frame.
	private void OnLandmarkSpawned(Landmark landmark)
	{
		if (landmark == null || landmark.Kind != "flower_pen")
			return;
		Callable.From(() => AttachBloom(landmark)).CallDeferred();
	}

	private void AttachBloom(Landmark landmark)
	{
		if (!IsInstanceValid(landmark) || landmark.HasNode("FencedBloom"))
			return;
		landmark.AddChild(new FencedBloom { Name = "FencedBloom", Director = this, Site = landmark });
	}

	/// <summary>A seed on the ground, at <paramref name="at"/>. Only on a chapter that has somewhere to plant it.</summary>
	public void DropSeed(Vector2 at)
	{
		if (Keepsake == null || Game == null)
			return;
		var seed = new SecretSeed { Name = "SecretSeed", Director = this };
		Game.AddChild(seed);
		seed.GlobalPosition = at;
	}

	/// <summary>The seed in his hands goes into the ground: gone from the world.</summary>
	public void PlantCarriedSeed()
	{
		if (Carried == null)
			return;
		SecretSeed seed = Carried;
		Carried = null;
		seed.Planted();
	}

	/// <summary>
	/// A secret was found: the toast, the page mark the first time ever on this save, and what it pays
	/// once ever - granted and saved at once, so dying a minute later takes nothing back (section 8).
	/// A second of the same kind in one run (another pen bloomed) is quieter: a sound and no toast.
	/// </summary>
	public void Found(string secretId)
	{
		SecretDefinition secret = SecretCatalog.Get(secretId);
		if (secret == null)
		{
			GD.PrintErr($"SecretDirector: '{secretId}' is not in SecretCatalog.");
			return;
		}
		if (!foundThisRun.Add(secretId))
		{
			SfxPlayer.Global(SfxCatalog.EventReward);
			return;
		}

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		bool firstEver = saveManager != null && saveManager.Data.RecordSecretFound(secretId);
		string paid = "";
		if (firstEver)
		{
			switch (secret.MetaReward)
			{
				case SecretMetaReward.FoundLevel:
					saveManager.Data.FoundUpgradeLevels[secret.MetaRewardId] = saveManager.Data.FoundUpgradeLevel(secret.MetaRewardId) + 1;
					Player?.ApplyFoundLevelNow(secret.MetaRewardId);
					break;
				case SecretMetaReward.ArcaneEnergy:
					saveManager.Data.TotalCurrency += secret.MetaAmount;
					break;
			}
			paid = SecretCatalog.DescribeMeta(secret);
			saveManager.SaveGame();
		}

		toast?.Show(secret.DisplayName, secret.RunReward, paid, firstEver);
		// The save changing has its own sound (meta_unlock); a secret found again is a reward.
		SfxPlayer.Global(firstEver ? SfxCatalog.MetaUnlock : SfxCatalog.EventReward);
	}
}
