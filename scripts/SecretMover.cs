using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// FOLLOW (.ai/stage-secrets.md 7.3 and 6.4's Warm Wisp; .ai/achievements-and-secrets.md section 5,
// "Things to follow"): something that moves ahead of him and waits. It stands at the start of a path laid
// from the seed; when he comes within StartleRadius it hops to the next stand along it and waits there
// facing him; at the last stand it settles, and that is the secret found (or the place where the secret
// is - the Pale Hart's high seat). If he wanders LeashRadius off it while it is part-way along, it goes
// back to its start, so a chase given up is a chase to begin again, never a lock (rule 4).
//
// A code-made Node2D, no scene, no collision, never in "enemies". Its progress is the owner's run state
// (State.Step: the stand it is at), so a mover on a chunk forgotten and made again is where it was. It
// draws a warm wisp by default - the only warm thing that moves and is not his (section 2) - and a
// secret with its own figure (the hart, the blown page) sets DrawSelf false and draws from HopT, Facing
// and GlobalPosition instead.
//
// Laying out a path: LayPath, from the placer the site was placed with, so the path is as fixed for the
// run as the site is.
public partial class SecretMover : Node2D
{
	public SecretDirector Director { get; set; }

	/// <summary>Where its progress lives: Step is the stand it has reached (0 = home).</summary>
	public SecretDirector.SiteState State { get; set; }

	/// <summary>The stands, in world pixels: [0] is home, the last is where it settles.</summary>
	public IReadOnlyList<Vector2> Path { get; set; } = Array.Empty<Vector2>();

	/// <summary>He comes this close and it goes on.</summary>
	public float StartleRadius { get; set; } = 250f;

	/// <summary>He wanders this far from it, part-way along, and it goes home.</summary>
	public float LeashRadius { get; set; } = 1500f;

	/// <summary>How long one hop takes, and how high the arc rises.</summary>
	public float HopSeconds { get; set; } = 0.8f;
	public float HopHeight { get; set; } = 36f;

	/// <summary>False when the owner draws the figure itself.</summary>
	public bool DrawSelf { get; set; } = true;

	/// <summary>Called when it lands on a stand (the stand's index).</summary>
	public event Action<int> Landed;

	/// <summary>Called once, when it lands on the last stand.</summary>
	public event Action Settled;

	/// <summary>Called when it gives up on him and goes back to the start.</summary>
	public event Action WentHome;

	/// <summary>0 to 1 through a hop; 1 when it is standing.</summary>
	public float HopT { get; private set; } = 1f;

	/// <summary>The way it is looking: at him while it waits, along the hop while it moves.</summary>
	public Vector2 Facing { get; private set; } = Vector2.Down;

	public bool IsSettled => State != null && Path.Count > 0 && State.Step >= Path.Count - 1;

	private Vector2 hopFrom;
	private Vector2 hopTo;
	private float clock;

	public override void _Ready()
	{
		State ??= new SecretDirector.SiteState();
		ZIndex = DrawLayers.Air;
		ZAsRelative = false;
		if (Path.Count > 0)
			GlobalPosition = Path[Math.Clamp(State.Step, 0, Path.Count - 1)];
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		if (Path.Count == 0)
			return;

		if (HopT < 1f)
		{
			HopT = MathF.Min(1f, HopT + dt / MathF.Max(0.05f, HopSeconds));
			float eased = HopT * HopT * (3f - 2f * HopT);
			GlobalPosition = hopFrom.Lerp(hopTo, eased) + new Vector2(0f, -HopHeight * MathF.Sin(HopT * MathF.PI));
			if (HopT >= 1f)
			{
				GlobalPosition = hopTo;
				Landed?.Invoke(State.Step);
				if (IsSettled)
					Settled?.Invoke();
			}
			QueueRedraw();
			return;
		}

		Player player = Director?.Player;
		if (player == null)
			return;
		Vector2 him = player.GlobalPosition;
		Vector2 toHim = him - GlobalPosition;
		if (toHim.LengthSquared() > 1f)
			Facing = toHim.Normalized();

		bool active = Director.Active;
		if (active && !IsSettled && toHim.LengthSquared() <= StartleRadius * StartleRadius)
			HopTo(State.Step + 1);
		else if (active && !IsSettled && State.Step > 0 && toHim.LengthSquared() > LeashRadius * LeashRadius)
		{
			State.Step = 0;
			GlobalPosition = Path[0];
			WentHome?.Invoke();
		}

		if (DrawSelf && toHim.LengthSquared() < 1600f * 1600f)
			QueueRedraw();
	}

	private void HopTo(int stand)
	{
		stand = Math.Clamp(stand, 0, Path.Count - 1);
		hopFrom = GlobalPosition;
		hopTo = Path[stand];
		State.Step = stand;
		Vector2 along = hopTo - hopFrom;
		if (along.LengthSquared() > 1f)
			Facing = along.Normalized();
		HopT = 0f;
	}

	public override void _Draw()
	{
		if (!DrawSelf)
			return;
		// The default figure: a warm wisp, a breathing core over a faint halo, its shadow on the ground
		// below so a hop reads as a hop.
		float lift = HopT < 1f ? HopHeight * MathF.Sin(HopT * MathF.PI) : 0f;
		DrawSetTransform(new Vector2(0f, lift + 10f), 0f, new Vector2(1f, 0.4f));
		DrawCircle(Vector2.Zero, 9f, new Color(0f, 0f, 0f, 0.25f));
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		DrawCircle(Vector2.Zero, 14f, new Color(SecretSite.GoldHi, 0.10f + 0.08f * SecretSite.Breath(clock)));
		SecretSite.DrawGlint(this, Vector2.Zero, clock);
	}

	/// <summary>
	/// A path for a mover, from the placer its site was placed with: <paramref name="stands"/> points
	/// after <paramref name="home"/>, each a seeded step of <paramref name="minStep"/> to
	/// <paramref name="maxStep"/> from the last on open ground, kept <paramref name="spacing"/> from every
	/// stand before it and within <paramref name="reach"/> of home. Rolled before tested (ExplorePlacer.Near).
	/// </summary>
	public static List<Vector2> LayPath(ExplorePlacer placer, Vector2 home, int stands, float minStep, float maxStep,
		float footprint = 60f, float spacing = 200f, float reach = 6000f)
	{
		var path = new List<Vector2> { home };
		for (int i = 0; i < stands; i++)
			path.Add(placer.Near(path[path.Count - 1], minStep, maxStep, footprint, path, spacing, home, reach));
		return path;
	}
}
