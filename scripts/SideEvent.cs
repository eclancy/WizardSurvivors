using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// One optional encounter inside a run, on the chapters that have one (.ai/side-events.md).
//
// Every chapter is somewhere the dark wizard holds something that was taken. A side event is the
// player going to get it: a wizard in a cell, a spell carried by something on a road. Winning one
// grants its Discovery unlock on the spot (DiscoveryRewards); once the site is exhausted it pays a
// chest instead, so the encounter stays worth doing on every later run.
//
// IT IS OUT THERE FROM THE FIRST SECOND, AND IT WAITS (2026-10-22). An event used to start itself at
// 90 pace seconds, a few hundred pixels from wherever the player happened to be. Reported: "the events
// are already populated and it's up to the player to adventure, rather than wait for timers". Now
// SideEventDirector places it at stage start, from the run's seed, four to six chunks out
// (PlaceSites), and it stands there DORMANT: drawn, lit, giving off its far tell, and asking nothing.
// No objective line, no arrow. The player finds it by walking - or by taking the chapter's wayfinder,
// which marks it (PointsOfInterest) - and the event BEGINS when he arrives (ArrivalRadius). From then
// on it runs exactly as it always did.
//
// The base owns everything the five have in common - where they are, the dormant wait and the
// arrival, the far tell, the objective line, the off-screen pointer, the success and failure beats,
// and standing down when the boss arrives - so a subclass is only its own rules. Each one is a
// code-created Node2D parented to the run at the origin, so it draws in world space with
// ToLocal(world) and needs no scene. What it draws on the ground goes under the actors (ZIndex -5);
// what stands up (the bog lanterns) is a child sprite sorted with them; its light is a child layer
// drawn additively over them, so a glow brightens what is under it and never hides it.
//
// One constraint every subclass inherits: nothing an event places in the world joins "enemies"
// unless it IS an enemy. Every targeting path iterates that group, and a lantern or a block of ice
// in it would pull the whole loadout off the swarm that is killing the player.
public abstract partial class SideEvent : Node2D
{
	public Node2DGame Game { get; set; }
	public string SiteId { get; set; } = string.Empty;

	/// <summary>True when the site's unlocks are all owned; the event then pays a chest.</summary>
	public bool PaysChest { get; set; }

	protected Player Player => Game?.RunPlayer;

	/// <summary>Shown once, when the player arrives.</summary>
	protected abstract string Announcement { get; }

	/// <summary>The line under the run timer while the event is live. Read every frame.</summary>
	protected abstract string Objective { get; }

	/// <summary>Where the objective pointer points, or null for nowhere. Read every frame once running.</summary>
	protected abstract Vector2? PointerTarget { get; }

	/// <summary>Runs once, when the player arrives. The sites are already placed.</summary>
	protected abstract void Begin();
	protected abstract void Tick(float delta);

	// --- where it is ---------------------------------------------------------------------------

	private readonly List<PointOfInterest> sites = new();

	/// <summary>The event's places, in the order PlaceSites laid them out. Fixed for the run.</summary>
	public IReadOnlyList<PointOfInterest> Sites => sites;

	/// <summary>Which pictogram the arrows to this event carry (ExploreArt.Glyphs).</summary>
	protected abstract string SiteGlyph { get; }

	/// <summary>Open ground kept round a site: placement tests it, and decor stays out of it.</summary>
	protected virtual float SiteFootprint => 160f;

	/// <summary>How close to any of its sites the player must come for the event to begin.</summary>
	protected virtual float ArrivalRadius => 380f;

	/// <summary>
	/// The colour of the far tell - a column of light and a wide faint glow round each site, which is
	/// how a dormant event is seen from beyond the edge of the screen. Cold, because it is his
	/// (.ai/world-and-tone.md): warm light is reserved for what he does not have.
	/// </summary>
	protected abstract Color TellColor { get; }

	/// <summary>
	/// The far tell's colour, for the chapter's hint caption (StageHintCaption) to wear as its rim,
	/// so the light the player is sent toward is the colour the hint told him to look for.
	/// </summary>
	public Color FarTellColor => TellColor;

	/// <summary>
	/// Lays the event out from the run seed: by default one site in the event band. Called by the
	/// director before the node enters the tree, and never again - the sites are the run's.
	/// </summary>
	public virtual void PlaceSites(ExplorePlacer placer)
	{
		AddSite(placer.EventSite(SiteFootprint));
	}

	protected PointOfInterest AddSite(Vector2 world, string glyph = null)
	{
		var site = PointsOfInterest.Add(new PointOfInterest(world, glyph ?? SiteGlyph));
		sites.Add(site);
		return site;
	}

	/// <summary>The first site: the grove, the block, the cell, the caravan's camp.</summary>
	protected Vector2 MainSite => sites.Count > 0 ? sites[0].Position : Vector2.Zero;

	private enum State { Dormant, Running, Resolved }

	private State state = State.Dormant;
	private Label objectiveLabel;
	private SideEventPointer pointer;
	private Node2D lightLayer;
	private float resolvedSeconds;
	private string resolvedText = string.Empty;
	private float announceSeconds;
	private const float AnnounceSeconds = 3.5f;
	private const float ResolvedLingerSeconds = 5f;

	/// <summary>Seconds this event has existed, for flicker and animation. Game time.</summary>
	protected float Clock { get; private set; }

	/// <summary>Placed and waiting: nobody has come yet.</summary>
	public bool IsDormant => state == State.Dormant;

	/// <summary>The player has arrived; the event is running or over.</summary>
	public bool IsFound => state != State.Dormant;

	public override void _Ready()
	{
		// Under the actors, over the ground: what this node draws is on the floor. Y-sorted, so a
		// child that stands up (a lantern) sorts with the actors at its own foot.
		ZIndex = -5;
		YSortEnabled = true;

		lightLayer = new Node2D
		{
			Name = "Light",
			// Over the actors (0) and under PhantasmRise (5): light falls on whoever stands in it.
			ZIndex = 8,
			Material = ExploreArt.Additive,
			TextureFilter = TextureFilterEnum.Linear,
		};
		lightLayer.Draw += OnLightDraw;
		AddChild(lightLayer);

		var overlay = Game?.GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (overlay != null)
		{
			objectiveLabel = new Label
			{
				Name = "SideEventObjective",
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AnchorLeft = 0f,
				AnchorRight = 1f,
				// Under the boss bar's slot (44-78), which a live event never shares: events stand
				// down the moment the boss arrives.
				OffsetTop = 84f,
				OffsetBottom = 112f,
			};
			ResponsiveLayout.SetFont(objectiveLabel, ResponsiveLayout.TextRole.Micro);
			objectiveLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.86f, 0.55f));
			objectiveLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
			objectiveLabel.AddThemeConstantOverride("outline_size", 5);
			overlay.AddChild(objectiveLabel);
			// Added after Node2DGame ran SafeArea.Apply on the overlay, so it was never inset: on a
			// phone the line sat on the XP bar, which the heading-sized win line made unreadable.
			SafeArea.ApplyTo(objectiveLabel);

			pointer = new SideEventPointer { Name = "SideEventPointer" };
			overlay.AddChild(pointer);
		}

		OnPlaced();
	}

	/// <summary>Called once in _Ready, dormant: build anything that stands in the world (sprites).</summary>
	protected virtual void OnPlaced() { }

	public override void _ExitTree()
	{
		if (lightLayer != null && IsInstanceValid(lightLayer))
			lightLayer.Draw -= OnLightDraw;
		if (objectiveLabel != null && IsInstanceValid(objectiveLabel))
			objectiveLabel.QueueFree();
		if (pointer != null && IsInstanceValid(pointer))
			pointer.QueueFree();
		PointsOfInterest.ObjectiveTarget = null;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		Clock += dt;
		QueueRedraw();
		lightLayer?.QueueRedraw();

		if (state == State.Resolved)
		{
			resolvedSeconds += dt;
			if (pointer != null) pointer.Target = null;
			PointsOfInterest.ObjectiveTarget = null;
			if (objectiveLabel != null)
			{
				objectiveLabel.Text = resolvedText;
				// In over a quarter second and out over the last one, rather than blinking on and off.
				float fadeIn = Mathf.Clamp(resolvedSeconds / 0.25f, 0f, 1f);
				float fadeOut = Mathf.Clamp(ResolvedLingerSeconds - resolvedSeconds, 0f, 1f);
				objectiveLabel.Modulate = new Color(1f, 1f, 1f, Mathf.Min(fadeIn, fadeOut));
			}
			if (resolvedSeconds >= ResolvedLingerSeconds)
				QueueFree();
			return;
		}

		if (Game == null || Game.RunFinished || Player == null)
			return;

		// The boss fight is a duel. Same rule as chests and the Warden: no set-pieces during it. An
		// event nobody found simply is not there any more.
		if (Game.BossFightActive)
		{
			bool wasRunning = state == State.Running;
			Resolve("");
			if (wasRunning)
				OnStoodDown();
			return;
		}

		if (state == State.Dormant)
		{
			DormantTick(dt);
			foreach (PointOfInterest site in sites)
			{
				if (!site.Done && PlayerWithin(site.Position, ArrivalRadius))
				{
					Arrive();
					break;
				}
			}
			if (state == State.Dormant)
				return;
		}

		Tick(dt);
		if (state == State.Resolved)
			return;

		announceSeconds -= dt;
		if (objectiveLabel != null)
			objectiveLabel.Text = announceSeconds > 0f ? Announcement : Objective;
		Vector2? target = PointerTarget;
		if (pointer != null)
			pointer.Target = target;
		PointsOfInterest.ObjectiveTarget = target;
	}

	/// <summary>
	/// The player has come: the event begins. Public so a probe (and _UiShot --win-event) can start
	/// it where it stands without the walk.
	/// </summary>
	public void Arrive()
	{
		if (state != State.Dormant)
			return;
		state = State.Running;
		announceSeconds = AnnounceSeconds;
		foreach (PointOfInterest site in sites)
			site.Seen = true;
		Begin();
	}

	/// <summary>Every frame while nobody has come. Animation only - a dormant event asks nothing.</summary>
	protected virtual void DormantTick(float delta) { }

	/// <summary>Called when the boss ends a running event early. Clean up anything left on the field.</summary>
	protected virtual void OnStoodDown() { }

	/// <summary>
	/// The line shown when a won event pays a chest, because its discovery is already owned. Says
	/// what was earned, in the event's own words; the chest itself rises where the player can see it.
	/// </summary>
	protected virtual string ChestLine => "Something was left behind - a chest";

	// Where a reward lands, measured from the player: never under his feet (a chest there opened on
	// the frame it appeared, before it could be seen) and never off screen (the caravan master can
	// die anywhere a spell reaches him). The camera at 0.85 shows about 420 px above and below him.
	private const float RewardMinDistance = 80f;
	private const float RewardMaxDistance = 200f;

	/// <summary>The player won. Grants the site's discovery, or a chest once it is exhausted.</summary>
	protected void Succeed(Vector2 rewardPosition)
	{
		if (state == State.Resolved)
			return;

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		List<DiscoveryRewards.Grant> grants = PaysChest || saveManager == null
			? new List<DiscoveryRewards.Grant>()
			: DiscoveryRewards.GrantSite(saveManager, SiteId);

		// Every one of the five events comes through here, so all five get the same beat: a column
		// of light where the reward arrives, near the player, and a heading-sized line saying what it
		// was (.ai/side-events.md, "How a win shows").
		Vector2 spot = RewardSpotNear(rewardPosition);
		PhantasmRise.Play(Game, spot);
		Succeeded = true;

		if (grants.Count == 0)
		{
			// The chest's ghost finishes turning solid just before the column thins away.
			Game?.SpawnRewardChestAt(spot, PhantasmRise.Seconds * 0.6f);
			SfxPlayer.AtPosition(SfxCatalog.EventReward, spot, 0f);
			Resolve(ChestLine);
			return;
		}

		var parts = new List<string>();
		foreach (var grant in grants)
		{
			Game?.RecordDiscovery(grant.DisplayName, grant.IsWizard);
			parts.Add(grant.IsWizard ? $"{grant.DisplayName} is free - a new wizard" : $"{grant.DisplayName} recovered - a new spell");
		}
		// meta_unlock, not event_reward: an unlock is the save changing, and that has its own sound.
		SfxPlayer.Global(SfxCatalog.MetaUnlock);
		Resolve(string.Join("  /  ", parts));
	}

	private Vector2 RewardSpotNear(Vector2 site)
	{
		if (Player == null || Game == null)
			return site;
		Vector2 from = Player.GlobalPosition;
		Vector2 offset = site - from;
		float distance = offset.Length();
		// Standing on the site (the grove, the thaw): put it a step up-screen from him instead.
		Vector2 direction = distance > 1f ? offset / distance : Vector2.Up;
		return Game.ReachablePoint(from + direction * Mathf.Clamp(distance, RewardMinDistance, RewardMaxDistance));
	}

	protected void Fail(string message) => Resolve(message);

	private void Resolve(string text)
	{
		state = State.Resolved;
		resolvedText = text;
		resolvedSeconds = string.IsNullOrEmpty(text) ? ResolvedLingerSeconds : 0f;
		// Won, lost or stood down, there is nothing left out there to walk to.
		foreach (PointOfInterest site in sites)
			site.Done = true;
		if (Succeeded && objectiveLabel != null)
		{
			// The win line is a heading, not the objective's small print: bold Cinzel, wrapped so a
			// long one fits the phone's 720 px canvas, in pale gold over the same black outline.
			ResponsiveLayout.SetFont(objectiveLabel, ResponsiveLayout.TextRole.Title);
			objectiveLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			objectiveLabel.AddThemeColorOverride("font_color", new Color(1f, 0.94f, 0.72f));
			objectiveLabel.AddThemeConstantOverride("outline_size", 7);
		}
	}

	protected bool IsResolved => state == State.Resolved;

	/// <summary>True once the event was won (not failed or stood down).</summary>
	protected bool Succeeded { get; private set; }

	/// <summary>Seconds since the event resolved, for a subclass that draws its own ending.</summary>
	protected float SecondsSinceResolved => resolvedSeconds;

	// --- light ---------------------------------------------------------------------------------

	private void OnLightDraw()
	{
		if (state == State.Resolved || lightLayer == null)
			return;
		DrawLight(lightLayer);
	}

	/// <summary>
	/// Draws on the additive light layer, in this node's local space. By default, the far tell over
	/// every site not yet done - full while dormant (it is how the event is found), dimmed once the
	/// player has arrived (he is standing in it).
	/// </summary>
	protected virtual void DrawLight(CanvasItem light)
	{
		float strength = IsDormant ? 1f : 0.4f;
		for (int i = 0; i < sites.Count; i++)
		{
			if (!sites[i].Done)
				DrawFarTell(light, sites[i].Position, TellColor, strength * Flicker(i));
		}
	}

	// Beyond this from the player a site's light cannot reach the screen on any canvas (the widest
	// view is about 1500 px to a side, the tell reaches 520 px up and 480 px round), so it is not
	// drawn at all. Four sites at most, so the test is the whole cost.
	private const float LightCullDistance = 2600f;

	/// <summary>True if something at <paramref name="world"/> could light the screen.</summary>
	protected bool NearPlayer(Vector2 world) =>
		Player != null && Player.GlobalPosition.DistanceSquaredTo(world) < LightCullDistance * LightCullDistance;

	/// <summary>
	/// The far tell: a wide, faint glow (whose edge reaches onto the screen from about a third of a
	/// screen beyond it), a tighter pool on the ground, and a thin column of light standing up out of
	/// it (seen from below the site, from much further). Light, not a marker: nothing on it says
	/// "go here" except that it is there.
	/// </summary>
	protected void DrawFarTell(CanvasItem light, Vector2 world, Color color, float strength)
	{
		if (!NearPlayer(world) || strength <= 0f)
			return;
		Vector2 p = ToLocal(world);
		ExploreArt.DrawGlow(light, p, 480f, new Color(color, 0.075f * strength));
		ExploreArt.DrawGlow(light, p, 210f, new Color(color, 0.2f * strength));
		ExploreArt.DrawShaft(light, p, 22f, 540f, new Color(color, 0.32f * strength));
		ExploreArt.DrawShaft(light, p, 7f, 500f, new Color(color.Lerp(Colors.White, 0.5f), 0.4f * strength));
	}

	/// <summary>
	/// His light flickers; found things breathe (.ai/stage-secrets.md section 2: a flicker is a hazard
	/// or his, a slow breath is a secret). Irregular, 0.7 to 1.1, different per <paramref name="seed"/>,
	/// with now and then a gutter.
	/// </summary>
	protected float Flicker(int seed)
	{
		float t = Clock;
		float f = 0.92f + 0.07f * Mathf.Sin(t * 7.3f + seed * 1.7f) + 0.05f * Mathf.Sin(t * 17.9f + seed * 4.1f)
			+ 0.03f * Mathf.Sin(t * 31.3f + seed * 2.3f);
		// A gutter: a short dip every few seconds, at a different moment for each light.
		float gutter = Mathf.Sin(t * 1.37f + seed * 2.9f);
		if (gutter > 0.94f)
			f -= (gutter - 0.94f) * 4f;
		return Mathf.Clamp(f, 0.6f, 1.15f);
	}

	// --- shared helpers -----------------------------------------------------------------------

	protected bool PlayerWithin(Vector2 world, float radius) =>
		Player != null && Player.GlobalPosition.DistanceTo(world) <= radius;

	/// <summary>A ring of stage enemies around a point - the pressure every event applies.</summary>
	protected void SpawnRing(Vector2 centre, int count, float radius, float healthMultiplier = 1f)
	{
		if (Game == null)
			return;
		float offset = (float)GD.RandRange(0.0, Mathf.Tau);
		for (int i = 0; i < count; i++)
		{
			float angle = offset + i * Mathf.Tau / count;
			Game.SpawnEventEnemy(null, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, healthMultiplier, false);
		}
	}

	protected static bool IsGone(Enemy enemy) => enemy == null || !IsInstanceValid(enemy) || enemy.IsDying;

	// The track a progress ring fills along. It was a solid black ring at 45% under every event's
	// progress, which over the bog lanterns read as an opaque boundary that hid the lantern it
	// circled. Now a faint dashed ring in the progress colour: it says where the edge is, and is
	// light enough to see the ground (and the thing) through.
	private const int TrackDashes = 28;

	protected void DrawProgressArc(Vector2 world, float radius, float progress, Color color, float width = 3f, float trackAlpha = 0.24f)
	{
		Vector2 local = ToLocal(world);
		Color track = new Color(color, color.A * trackAlpha);
		float dash = Mathf.Tau / TrackDashes;
		for (int k = 0; k < TrackDashes; k++)
			DrawArc(local, radius, k * dash, k * dash + dash * 0.55f, 4, track, 2f);
		if (progress > 0f)
			DrawArc(local, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Mathf.Clamp(progress, 0f, 1f), 48, new Color(color, color.A * 0.9f), width);
	}
}
