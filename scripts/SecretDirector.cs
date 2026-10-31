using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// The run's half of the stage secrets (issue #20; .ai/stage-secrets.md; .ai/achievements-and-secrets.md
// sections 5 and 6). Added by Node2DGame beside the SideEventDirector, before the first chunk is
// furnished, so the ground of everything it places is reserved before any tree can be put on it.
//
// What it holds:
//   - SecretRunState (stage-secrets 7.4): progress per site, keyed by a stable id - a secret's own id, or
//     a flower pen's Landmark.Key - and NEVER on the site's node, because a site on a chunk is freed with
//     it and made again when the player walks back. A half-walked lap is still half-walked.
//   - Placing the chapter's secrets (piece A4). Every built secret in SecretCatalog.ForStage is put in the
//     world by its SecretPlacement, from the terrain seed, once at stage start - the anchor three to four
//     chunks out, the keepsake ten to twelve, the spell vault five to seven at a bearing at least 90
//     degrees from the side event's and the keepsake's (6.4) - each on its own ExplorePlacer stream, so
//     a change to one never moves another. A Custom secret hooks itself in (Install; the Fenced Bloom
//     rides every flower pen through HookLandmarks). So a chapter's secrets are its catalog file and its
//     nodes, and never an edit here.
//   - What a placed site asks of the ground: its far tell, furnished into each chunk round it as the
//     chunk streams in (FurnishChunk), and the cells it makes solid, stamped into the navigation flags
//     every time the chunk's data is made (StampChunk), so the flow field routes the horde round a
//     crystal as it routes round a fence.
//   - The seen mark (4.3): the first time the player comes within a site's SeenRadius it is recorded on
//     the save's GlimpsedSecretIds - saved with the next save, not at once - and secret_seen breathes once
//     at the site. No toast and no text: that would be a pointer.
//   - Finding (Found): the toast, the page mark the first time ever, the keepsake's pay, and the secret's
//     deed through AchievementManager.CompleteNow - which grants a vault's spell on the spot, so it joins
//     the level-up pool from the next level-up of the same run (decision 9).
//   - The run's budgets (stage-secrets section 3): two chests a run from all secrets together.
//   - The seeds a bloom drops, and which one is in the player's hands.
public partial class SecretDirector : Node
{
	public Node2DGame Game { get; set; }

	/// <summary>The chapter's StageCatalog id.</summary>
	public string StageId { get; private set; } = "";

	/// <summary>
	/// One site's progress for the run. What each field means is the site's: a lap is Progress signed by
	/// direction, a breakable's is the damage landed, a mover's stand is Step, a gather's points are Marks.
	/// </summary>
	public sealed class SiteState
	{
		public float Progress;
		public bool Done;
		/// <summary>A count or a place along: the stand a mover is at, how many a lure has taken, points gathered.</summary>
		public int Step;
		/// <summary>One bit a part, for a set: the points of a gather.</summary>
		public ulong Marks;
	}

	private readonly Dictionary<string, SiteState> sites = new();

	public SiteState StateOf(string key)
	{
		if (!sites.TryGetValue(key, out SiteState state))
			sites[key] = state = new SiteState();
		return state;
	}

	/// <summary>The chapter's anchor, keepsake and spell vault, placed at stage start; null where it has none.</summary>
	public SecretSite Anchor { get; private set; }
	public SecretSite Keepsake { get; private set; }
	public SecretSite Vault { get; private set; }

	/// <summary>Every site placed at stage start, in the order placed.</summary>
	public IReadOnlyList<SecretSite> Placed => placed;

	/// <summary>The placed site of this secret, or null.</summary>
	public SecretSite PlacedSite(string secretId) =>
		placed.FirstOrDefault(s => s.SecretId.Equals(secretId, StringComparison.OrdinalIgnoreCase));

	/// <summary>The seed in the player's hands, if any. Only one at a time.</summary>
	public SecretSeed Carried { get; set; }

	// The bands, in terrain chunks (1536 px) from the start: stage-secrets section 4 and 6.4.
	public const float AnchorMinChunks = 3f;
	public const float AnchorMaxChunks = 4f;
	/// <summary>The keepsake band: 70 to 85 s of walking.</summary>
	public const float KeepsakeMinChunks = 10f;
	public const float KeepsakeMaxChunks = 12f;
	/// <summary>The vault band: 35 to 50 s of walking (.ai/achievements-and-secrets.md 6.4).</summary>
	public const float VaultMinChunks = 5f;
	public const float VaultMaxChunks = 7f;
	/// <summary>How far apart, in bearing from the start, a vault is from the event and the keepsake.</summary>
	public const float MinBearingSeparationDegrees = 90f;

	// Each kind on its own placer stream, so a change to how one is laid out never moves another. 7 is
	// the First Tree's and must not change: it would move every Forest run's keepsake.
	private const int KeepsakeStream = 7;
	private const int VaultStream = 8;
	private const int AnchorStream = 9;

	/// <summary>
	/// How near is seen (4.3: "came within about 450 px of the site, once"): the near tell is on screen by
	/// then on both canvases - a phone shows about 420 px to a side at the run camera's 0.85.
	/// </summary>
	public const float SeenRadius = 450f;

	/// <summary>Chests a run from all secrets together (stage-secrets section 3): about 20 drop a run.</summary>
	public const int SecretChestBudget = 2;

	/// <summary>
	/// _UiShot --secret=&lt;id&gt;: that secret, if this chapter places it, goes beside the spawn instead of
	/// where the seed would put it, so it can be photographed and worked without the walk
	/// (.ai/stage-secrets.md 7.8). Never set in play.
	/// </summary>
	public static string PlaceBesideSpawn { get; set; } = "";

	/// <summary>Where "beside the spawn" is: due east, a quarter of a chunk out - on screen from the start on a PC.</summary>
	public const float BesideSpawnChunks = 0.25f;

	private readonly List<SecretSite> placed = new();
	private readonly List<SecretSite> live = new();
	private readonly HashSet<string> glimpsed = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> foundThisRun = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<(Vector2 Centre, float Radius)> stamps = new();
	private readonly List<(string Kind, string NodeName, string SecretId, Func<Landmark, SecretSite> Create)> landmarkHooks = new();
	private SecretDefinition installing;
	private bool hooked;
	private bool logPlacement;
	private int chestsPaid;
	private SecretToast toast;

	public Player Player => Game?.RunPlayer;

	/// <summary>Secrets stand down when the boss arrives, as chests and the side event do (section 4).</summary>
	public bool Active => Game != null && !Game.RunFinished && !Game.BossFightActive;

	private SaveData Save => GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;

	public override void _Ready()
	{
		if (Game == null)
			return;
		StageId = StageCatalog.GetByIndex(Global.SelectedStageIdx)?.Id ?? "";
		logPlacement = OS.GetCmdlineUserArgs().Contains("--explore-log");

		List<SecretDefinition> here = SecretCatalog.ForStage(StageId).ToList();
		foreach (SecretDefinition secret in here.Where(s => s.Placement == SecretPlacement.Custom && s.Install != null))
		{
			installing = secret;
			secret.Install(this);
		}
		installing = null;

		var avoid = new List<float>();
		float? eventBearing = SideEventBearing();
		if (eventBearing is float b)
			avoid.Add(b);
		Anchor = PlaceAll(here, SecretPlacement.Anchor, AnchorStream, AnchorMinChunks, AnchorMaxChunks, avoid);
		// The keepsake keeps clear of the anchor; with no anchor it goes on the seed's own bearing, which is
		// exactly where the First Tree has always been (stream 7, EventSite).
		Keepsake = PlaceAll(here, SecretPlacement.Keepsake, KeepsakeStream, KeepsakeMinChunks, KeepsakeMaxChunks,
			Anchor != null ? new List<float> { BearingOf(Anchor.Position) } : new List<float>());
		if (Keepsake != null)
			avoid.Add(BearingOf(Keepsake.Position));
		Vault = PlaceAll(here, SecretPlacement.Vault, VaultStream, VaultMinChunks, VaultMaxChunks, avoid);

		// Already seen on this save: no check a frame for those.
		if (Save is SaveData save)
			foreach (string id in save.GlimpsedSecretIds)
				glimpsed.Add(id);

		toast = new SecretToast { Name = "SecretToast" };
		AddChild(toast);
	}

	public override void _ExitTree()
	{
		if (hooked)
			Landmark.Spawned -= OnLandmarkSpawned;
		hooked = false;
	}

	// --- placing -------------------------------------------------------------------------------------

	private SecretSite PlaceAll(List<SecretDefinition> here, SecretPlacement kind, int stream, float minChunks, float maxChunks, List<float> avoid)
	{
		SecretSite first = null;
		int k = 0;
		foreach (SecretDefinition secret in here.Where(s => s.Placement == kind))
		{
			SecretSite site = Place(secret, stream + 16 * k++, minChunks, maxChunks, avoid);
			first ??= site;
		}
		return first;
	}

	private SecretSite Place(SecretDefinition secret, int stream, float minChunks, float maxChunks, List<float> avoid)
	{
		if (secret.CreateSite == null)
		{
			GD.PushError($"SecretDirector: '{secret.Id}' is placed as {secret.Placement} but has no CreateSite; it is not in the world.");
			return null;
		}
		ExplorePlacer placer = Game.CreateExplorePlacer(stream, minChunks, maxChunks);
		Vector2 at;
		bool beside = !string.IsNullOrEmpty(PlaceBesideSpawn) && PlaceBesideSpawn.Equals(secret.Id, StringComparison.OrdinalIgnoreCase);
		if (beside)
			at = placer.AtBearing(0f, BesideSpawnChunks, BesideSpawnChunks, Mathf.Min(secret.Footprint, 160f));
		else if (avoid.Count == 0)
			at = placer.EventSite(secret.Footprint);
		else
			at = placer.AtBearing(PickBearing(placer, avoid), minChunks, maxChunks, secret.Footprint);

		SecretSite site = secret.CreateSite();
		site.Name = ToPascal(secret.Id);
		site.Director = this;
		site.SecretId = secret.Id;
		site.Position = at;
		site.LayOut(placer);
		foreach (var (centre, radius) in placer.Reserved)
			Game.ReserveExploreGround(centre, radius);
		foreach (var (centre, radius) in site.SolidFootprints)
			AddStamp(centre, radius);
		Game.AddChild(site);
		placed.Add(site);

		if (logPlacement || beside)
			GD.Print($"SECRETS stage {StageId}: {secret.DisplayName} ({secret.Placement}) at ({at.X:0},{at.Y:0}), "
				+ $"{at.DistanceTo(Game.StageOrigin) / 1536f:0.0} chunks out, bearing {Mathf.RadToDeg(BearingOf(at)):0} deg"
				+ (beside ? " - beside the spawn (--secret)" : ""));
		return site;
	}

	/// <summary>
	/// A bearing at least <see cref="MinBearingSeparationDegrees"/> from every one in
	/// <paramref name="avoid"/>: twenty-four tried round from a rolled start, the first that clears them all
	/// taken, or - when none can (an event and a keepsake opposite each other leave no 90-degree gap) -
	/// the one farthest from them. Rolled once, before anything is tested.
	/// </summary>
	private static float PickBearing(ExplorePlacer placer, List<float> avoid)
	{
		float start = placer.NextFloat() * Mathf.Tau;
		float need = Mathf.DegToRad(MinBearingSeparationDegrees);
		float best = start, bestGap = -1f;
		for (int k = 0; k < 24; k++)
		{
			float bearing = start + k * Mathf.Tau / 24f;
			float gap = float.MaxValue;
			foreach (float other in avoid)
				gap = Mathf.Min(gap, Mathf.Abs(Mathf.AngleDifference(bearing, other)));
			if (gap >= need)
				return bearing;
			if (gap > bestGap)
			{
				bestGap = gap;
				best = bearing;
			}
		}
		return best;
	}

	private float BearingOf(Vector2 world) => (world - Game.StageOrigin).Angle();

	private float? SideEventBearing()
	{
		SideEvent sideEvent = Game.GetNodeOrNull<SideEventDirector>("SideEventDirector")?.Event;
		return sideEvent != null && sideEvent.Sites.Count > 0 ? BearingOf(sideEvent.Sites[0].Position) : null;
	}

	private static string ToPascal(string id) =>
		string.Concat(id.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));

	// --- what a site asks of the ground --------------------------------------------------------------

	/// <summary>
	/// Node2DGame's furnishing, once per chunk as it streams in (beside the curated props): every placed
	/// site's far tell scatters its share of the chunk into the chunk's prop holder, which forgets it with
	/// the chunk. A pure function of (seed, chunk), so a chunk walked back to is furnished the same.
	/// </summary>
	public void FurnishChunk(TerrainChunk chunk, Node2D parent, Rect2 rect)
	{
		for (int i = 0; i < placed.Count; i++)
		{
			SecretSite site = placed[i];
			if (site == null || !IsInstanceValid(site))
				continue;
			site.FarTell?.Furnish(Game, site.GlobalPosition, chunk.Coord, parent, rect, i);
		}
	}

	/// <summary>
	/// Node2DGame.PlanChunkFeatures, every time a chunk's data is made: the cells a site makes solid go
	/// into its flags as a fence's do (TerrainStreamer.FlagFence) - solid in the navigation grid, so the
	/// flow field routes round them and nothing spawns or drops inside them; a shot still flies over.
	/// Chunk data is forgotten and made again as the player moves, so this runs on every making.
	/// </summary>
	public void StampChunk(TerrainChunk chunk)
	{
		TerrainStreamer terrain = Game?.Terrain;
		if (stamps.Count == 0 || terrain == null || chunk == null)
			return;
		const int n = TerrainStreamer.ChunkTiles;
		float half = terrain.TilePixels * 0.5f;
		foreach (var (centre, radius) in stamps)
		{
			Vector2I c0 = terrain.CellOfWorld(centre - new Vector2(radius, radius));
			Vector2I c1 = terrain.CellOfWorld(centre + new Vector2(radius, radius));
			int x0 = Math.Max(c0.X, chunk.MinCell.X), x1 = Math.Min(c1.X, chunk.MinCell.X + n - 1);
			int y0 = Math.Max(c0.Y, chunk.MinCell.Y), y1 = Math.Min(c1.Y, chunk.MinCell.Y + n - 1);
			for (int cy = y0; cy <= y1; cy++)
			{
				for (int cx = x0; cx <= x1; cx++)
				{
					Vector2 mid = terrain.CellOriginWorld(new Vector2I(cx, cy)) + new Vector2(half, half);
					if (mid.DistanceSquaredTo(centre) > radius * radius)
						continue;
					chunk.Flags[(cx - chunk.MinCell.X) + (cy - chunk.MinCell.Y) * n] |= TerrainStreamer.FlagFence;
				}
			}
		}
	}

	private void AddStamp(Vector2 centre, float radius)
	{
		stamps.Add((centre, radius));
		TerrainStreamer terrain = Game?.Terrain;
		if (terrain == null)
			return;
		// Placing the site asked about its ground, so those chunks' data exists already, made before the
		// stamp did: stamp it now. (A chunk not made yet is stamped by PlanChunkFeatures when it is.)
		Vector2I a = terrain.ChunkOfWorld(centre - new Vector2(radius, radius));
		Vector2I b = terrain.ChunkOfWorld(centre + new Vector2(radius, radius));
		for (int y = a.Y; y <= b.Y; y++)
			for (int x = a.X; x <= b.X; x++)
				StampChunk(terrain.DataFor(new Vector2I(x, y)));
	}

	// --- landmarks -----------------------------------------------------------------------------------

	/// <summary>
	/// For a Custom secret's Install: a site of <paramref name="create"/>'s making is attached, as a child
	/// named <paramref name="nodeName"/>, to every landmark of <paramref name="kind"/> as it streams in
	/// (Landmark.Spawned). The Fenced Bloom is every flower pen.
	/// </summary>
	public void HookLandmarks(string kind, string nodeName, Func<Landmark, SecretSite> create)
	{
		landmarkHooks.Add((kind, nodeName, installing?.Id ?? "", create));
		if (hooked)
			return;
		Landmark.Spawned += OnLandmarkSpawned;
		hooked = true;
	}

	// Spawned fires from the landmark's _EnterTree, while its parent is still adding it; a child
	// added then is refused ("parent busy"), so the site is attached at the end of the frame.
	private void OnLandmarkSpawned(Landmark landmark)
	{
		if (landmark == null)
			return;
		foreach (var hook in landmarkHooks)
		{
			if (landmark.Kind != hook.Kind)
				continue;
			var attach = hook;
			Callable.From(() => Attach(landmark, attach)).CallDeferred();
		}
	}

	private void Attach(Landmark landmark, (string Kind, string NodeName, string SecretId, Func<Landmark, SecretSite> Create) hook)
	{
		if (!IsInstanceValid(landmark) || landmark.HasNode(hook.NodeName))
			return;
		SecretSite site = hook.Create(landmark);
		site.Name = hook.NodeName;
		site.Director = this;
		site.SecretId = hook.SecretId;
		landmark.AddChild(site);
	}

	// --- the seen mark ---------------------------------------------------------------------------------

	/// <summary>A site entering the world. Called by SecretSite; the seen mark watches every one.</summary>
	public void Register(SecretSite site)
	{
		if (site != null && !live.Contains(site))
			live.Add(site);
	}

	public void Unregister(SecretSite site) => live.Remove(site);

	public override void _Process(double delta)
	{
		Player player = Player;
		if (player == null || live.Count == 0 || !Active)
			return;
		Vector2 at = player.GlobalPosition;
		for (int i = live.Count - 1; i >= 0; i--)
		{
			SecretSite site = live[i];
			if (site == null || !IsInstanceValid(site))
			{
				live.RemoveAt(i);
				continue;
			}
			if (glimpsed.Contains(site.SecretId))
				continue;
			float r = site.SeenRadius;
			if (at.DistanceSquaredTo(site.TellPosition) <= r * r)
				Glimpse(site);
		}
	}

	/// <summary>
	/// The player has been near enough to see it (4.3): onto the save's glimpsed list - saved with the next
	/// save, since losing a seen mark to a crash costs nothing - and one breath of secret_seen at the site,
	/// the first time on this save. Nothing else: no toast, no text, which would be a pointer.
	/// </summary>
	private void Glimpse(SecretSite site)
	{
		glimpsed.Add(site.SecretId);
		if (Save?.RecordSecretGlimpsed(site.SecretId) == true)
			SfxPlayer.AtPosition(SfxCatalog.SecretSeen, site.TellPosition, 0f);
	}

	/// <summary>True once this secret's tell has been seen, this run or any before (for probes and the pause page).</summary>
	public bool IsGlimpsed(string secretId) => glimpsed.Contains(secretId);

	// --- rewards ----------------------------------------------------------------------------------------

	/// <summary>
	/// A chest from the run's secret budget, near <paramref name="near"/> on ground he can stand on. False,
	/// and nothing, once the budget is spent.
	/// </summary>
	public bool PayChest(Vector2 near)
	{
		if (Game == null || chestsPaid >= SecretChestBudget)
			return false;
		chestsPaid++;
		// Rises as a ghost first, as a side event's does, and cannot be taken until it is solid.
		Game.SpawnRewardChestAt(Game.ReachablePoint(near), PhantasmRise.Seconds * 0.6f);
		return true;
	}

	public int ChestsPaid => chestsPaid;

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
	/// A secret was found: the toast, the page mark the first time ever on this save, what it pays once ever
	/// - granted and saved at once, so dying a minute later takes nothing back (section 8) - and its deed,
	/// through AchievementManager.CompleteNow (.ai/achievements-and-secrets.md 2.6), which grants a vault's
	/// spell on the spot: the level-up pool reads the save live, so the spell can be offered from the next
	/// level-up of this run (decision 9). Never into the run itself - no loadout, no level, no offer.
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
			SfxPlayer.Global(SfxCatalog.SecretFound);
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
		}

		// The deed: recorded, its spell granted if the save lacks it, and the save written - every find,
		// so a deed missed by an older build is recorded by the next one (CompleteNow changes nothing twice).
		List<RunUnlockEntry> granted = saveManager != null
			? AchievementManager.CompleteNow(saveManager, AchievementDefinitions.SecretDeedId(secretId))
			: new List<RunUnlockEntry>();
		string line = firstEver || string.IsNullOrEmpty(secret.RepeatReward) ? secret.RunReward : secret.RepeatReward;
		foreach (RunUnlockEntry entry in granted.Where(e => e.Kind == RunUnlockKind.Spell))
		{
			// On the results screen with the run's other discoveries. The kind leads, as a side event's
			// line does (SideEvent.Succeed): "Spell recovered - Starfall".
			Game?.RecordDiscovery(new DiscoveryRewards.Grant(entry.Id, entry.Name, false));
			paid = $"Spell recovered - {entry.Name}";
		}
		// A bought spell's vault says so (4.5): the spell was owned before the vault was found.
		if (firstEver && secret.MetaReward == SecretMetaReward.Spell && granted.Count == 0)
		{
			paid = $"Spell already known - {secret.SpellName}";
			line = "";
		}

		toast?.Show(secret.DisplayName, line, paid, firstEver);
		// meta_unlock is the save changing (the page mark, a spell); secret_found is a secret found again.
		SfxPlayer.Global(firstEver ? SfxCatalog.MetaUnlock : SfxCatalog.SecretFound);
	}

	/// <summary>True if this secret was found in this run.</summary>
	public bool FoundThisRun(string secretId) => foundThisRun.Contains(secretId);
}
