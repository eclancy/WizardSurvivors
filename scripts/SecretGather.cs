using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// GATHER (.ai/achievements-and-secrets.md section 5, "Things to gather"): touch every one of a set, in any
// order, within a run - the Fallen Star's seven shards, three lit lamps, scattered pages. Distinct from
// sequence (an order that resets) and from the lanterns' explore (a side event's): nothing resets here,
// and the order is the player's.
//
// A code-made Node2D, no scene, no Area2D: one distance check per untaken point a frame, only while its
// director is active. Progress is the owner's run state - State.Marks, one bit a point, and State.Step the
// count - so a set half gathered stays half gathered when its chunk is forgotten. Up to 64 points.
//
// It draws a warm glint on each point not yet taken by default (DrawSelf); a secret with its own art for
// them (a shard, a page) sets DrawSelf false and draws from IsTaken.
public partial class SecretGather : Node2D
{
	public SecretDirector Director { get; set; }

	/// <summary>Where the progress lives: Marks has a bit set for each point taken, Step counts them.</summary>
	public SecretDirector.SiteState State { get; set; }

	/// <summary>The points, in world pixels.</summary>
	public IReadOnlyList<Vector2> Points { get; set; } = Array.Empty<Vector2>();

	/// <summary>Walking within this of a point takes it.</summary>
	public float TouchRadius { get; set; } = 44f;

	public bool DrawSelf { get; set; } = true;

	/// <summary>Called when a point is taken, with its index.</summary>
	public event Action<int> Taken;

	/// <summary>Called once, when the last point is taken.</summary>
	public event Action Completed;

	public int Count => Math.Min(64, Points.Count);
	public int Gathered => State == null ? 0 : System.Numerics.BitOperations.PopCount(State.Marks);
	public bool IsComplete => Count > 0 && Gathered >= Count;
	public bool IsTaken(int index) => State != null && index >= 0 && index < 64 && (State.Marks & (1UL << index)) != 0;

	private float clock;

	public override void _Ready()
	{
		State ??= new SecretDirector.SiteState();
		ZIndex = DrawLayers.GroundTell;
		ZAsRelative = false;
		// Points are world positions: draw them where they are, whatever the parent.
		TopLevel = true;
		GlobalPosition = Vector2.Zero;
	}

	public override void _Process(double delta)
	{
		clock += (float)delta;
		Player player = Director?.Player;
		if (player == null || IsComplete)
			return;
		Vector2 him = player.GlobalPosition;
		bool near = false;
		for (int i = 0; i < Count; i++)
		{
			if (IsTaken(i))
				continue;
			float d2 = him.DistanceSquaredTo(Points[i]);
			if (d2 < 1600f * 1600f)
				near = true;
			if (!Director.Active || d2 > TouchRadius * TouchRadius)
				continue;
			State.Marks |= 1UL << i;
			State.Step = Gathered;
			Taken?.Invoke(i);
			if (IsComplete)
				Completed?.Invoke();
		}
		if (DrawSelf && near)
			QueueRedraw();
	}

	public override void _Draw()
	{
		if (!DrawSelf)
			return;
		for (int i = 0; i < Count; i++)
			if (!IsTaken(i))
				SecretSite.DrawGlint(this, Points[i], clock + i * 0.37f);
	}

	/// <summary>
	/// Points strewn along a line from <paramref name="from"/> to <paramref name="to"/> - a trail, the
	/// Fallen Star's scorch - each nudged a seeded distance off the line and onto open ground through the
	/// placer, so the set is fixed for the run. Rolled before tested.
	/// </summary>
	public static List<Vector2> AlongTrail(ExplorePlacer placer, Vector2 from, Vector2 to, int count, float scatter = 120f, float footprint = 40f)
	{
		var points = new List<Vector2>(count);
		Vector2 along = to - from;
		Vector2 side = along.LengthSquared() > 1f ? new Vector2(-along.Y, along.X).Normalized() : Vector2.Right;
		for (int i = 0; i < count; i++)
		{
			float t = (i + 0.5f) / count;
			float off = (placer.NextFloat() * 2f - 1f) * scatter;
			Vector2 wanted = from + along * t + side * off;
			points.Add(placer.Near(wanted, 0f, scatter * 0.5f, footprint, points, scatter * 0.5f, wanted, scatter * 4f));
		}
		return points;
	}
}
