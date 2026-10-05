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
// The base owns everything the five have in common - the objective line, the off-screen pointer,
// the success and failure beats, and standing down when the boss arrives - so a subclass is only
// its own rules. Each one is a code-created Node2D parented to the run at the origin, so it draws
// in world space with ToLocal(world) and needs no scene.
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

	/// <summary>Shown once, when the event starts.</summary>
	protected abstract string Announcement { get; }

	/// <summary>The line under the run timer while the event is live. Read every frame.</summary>
	protected abstract string Objective { get; }

	/// <summary>Where the pointer points, or null for nowhere. Read every frame.</summary>
	protected abstract Vector2? PointerTarget { get; }

	protected abstract void Begin();
	protected abstract void Tick(float delta);

	private enum State { Running, Resolved }

	private State state = State.Running;
	private Label objectiveLabel;
	private SideEventPointer pointer;
	private float resolvedSeconds;
	private string resolvedText = string.Empty;
	private float announceSeconds = AnnounceSeconds;
	private const float AnnounceSeconds = 3.5f;
	private const float ResolvedLingerSeconds = 5f;

	public override void _Ready()
	{
		// Under the actors, over the ground: these are things on the floor.
		ZIndex = 1;
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

			pointer = new SideEventPointer { Name = "SideEventPointer" };
			overlay.AddChild(pointer);
		}

		Begin();
	}

	public override void _ExitTree()
	{
		if (objectiveLabel != null && IsInstanceValid(objectiveLabel))
			objectiveLabel.QueueFree();
		if (pointer != null && IsInstanceValid(pointer))
			pointer.QueueFree();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		QueueRedraw();

		if (state == State.Resolved)
		{
			resolvedSeconds += dt;
			if (pointer != null) pointer.Target = null;
			if (objectiveLabel != null) objectiveLabel.Text = resolvedText;
			if (resolvedSeconds >= ResolvedLingerSeconds)
				QueueFree();
			return;
		}

		if (Game == null || Game.RunFinished || Player == null)
			return;

		// The boss fight is a duel. Same rule as chests and the Warden: no set-pieces during it.
		if (Game.BossFightActive)
		{
			Resolve("");
			OnStoodDown();
			return;
		}

		Tick(dt);
		if (state == State.Resolved)
			return;

		announceSeconds -= dt;
		if (objectiveLabel != null)
			objectiveLabel.Text = announceSeconds > 0f ? Announcement : Objective;
		if (pointer != null)
			pointer.Target = PointerTarget;
	}

	/// <summary>Called when the boss ends the event early. Clean up anything left on the field.</summary>
	protected virtual void OnStoodDown() { }

	/// <summary>The player won. Grants the site's discovery, or a chest once it is exhausted.</summary>
	protected void Succeed(Vector2 rewardPosition)
	{
		if (state == State.Resolved)
			return;

		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		List<DiscoveryRewards.Grant> grants = PaysChest || saveManager == null
			? new List<DiscoveryRewards.Grant>()
			: DiscoveryRewards.GrantSite(saveManager, SiteId);

		if (grants.Count == 0)
		{
			Game?.SpawnRewardChestAt(rewardPosition);
			Resolve("Something was left behind.");
			return;
		}

		var parts = new List<string>();
		foreach (var grant in grants)
		{
			Game?.RecordDiscovery(grant.DisplayName, grant.IsWizard);
			parts.Add(grant.IsWizard ? $"{grant.DisplayName} is free" : $"{grant.DisplayName} recovered");
		}
		SfxPlayer.Global(SfxCatalog.MetaUnlock);
		Resolve(string.Join(" - ", parts));
	}

	protected void Fail(string message) => Resolve(message);

	private void Resolve(string text)
	{
		state = State.Resolved;
		resolvedText = text;
		resolvedSeconds = string.IsNullOrEmpty(text) ? ResolvedLingerSeconds : 0f;
	}

	protected bool IsResolved => state == State.Resolved;

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

	protected void DrawProgressArc(Vector2 world, float radius, float progress, Color color, float width = 3f)
	{
		Vector2 local = ToLocal(world);
		DrawArc(local, radius, 0f, Mathf.Tau, 40, new Color(0f, 0f, 0f, 0.45f), width + 2f);
		if (progress > 0f)
			DrawArc(local, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Mathf.Clamp(progress, 0f, 1f), 40, color, width);
	}
}
