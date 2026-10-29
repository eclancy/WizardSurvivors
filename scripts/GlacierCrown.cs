using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Glacial Spike's Glacier Crown (IceSpike.GlacierCrownId, its Ascension that deepens Ice, 2026-10-29):
// a ring of spikes erupts round the target and stands, and the pack inside is penned. The proposal
// said eight; at the ring's size eight stood like fence posts with a body's width between them, and
// twelve read as a wall.
//
// The spikes are IceSpikes like any other (Player.FireIceSpikes raises them, IceSpike.StandSeconds holds
// them up); this node is the pen. For as long as the crown stands it watches the band the ring of spikes
// occupies, and anything in that band is shoved back to the side of the ice it started on: what was
// inside the ring when it rose is turned back in, what was outside is turned away. Not physics - the
// player's body and every enemy's mask stay as they are (CLAUDE.md, "Collision layers") - but a shove
// through Enemy.ApplyKnockback, which a boss's KnockbackResistance discounts, so a boss shoulders its way
// out and a skeleton cannot. Nothing is rooted: what is penned can still walk, round and round inside,
// which is what makes it a pen and not Gravewell's hold or the Long Winter's stop.
//
// Draws nothing; the spikes are the wall. Fixed arrays, one scan every ScanSeconds through the enemy grid.
public partial class GlacierCrown : Node2D
{
	public const int RingSpikes = 12;
	public const float StandSeconds = 1.5f;
	/// <summary>The ring's radius, as a share of the spell's spike radius.</summary>
	public const float RingShare = 1.5f;
	/// <summary>A ring spike's size against the spell's spike.</summary>
	public const float RingSpikeSize = 0.6f;
	private const float PenShove = 95f;
	private const float ScanSeconds = 0.05f;
	private const int MaxPenned = 48;

	private static readonly List<Enemy> scratch = new();

	private float ringRadius;
	private float wallHalf;
	private bool standing;
	private float standUntil;
	private float clock;
	private float scan;
	private readonly Enemy[] penned = new Enemy[MaxPenned];
	private int pennedCount;
	private const int MaxStruck = 96;
	private readonly Enemy[] struck = new Enemy[MaxStruck];
	private int struckCount;

	/// <summary>A pen round <paramref name="centre"/> (world), its wall the band ringRadius +- wallHalf.</summary>
	public static GlacierCrown Raise(Node2D arena, Vector2 centre, float ringRadius, float wallHalf)
	{
		var crown = new GlacierCrown { ringRadius = ringRadius, wallHalf = wallHalf };
		arena.AddChild(crown);
		crown.GlobalPosition = centre;
		return crown;
	}

	public override void _Ready() => Name = "GlacierCrown";

	/// <summary>
	/// Called by the first ring spike to strike: the ice is up. Whatever is inside the ring now is what it
	/// pens, until StandSeconds from now.
	/// </summary>
	public void Close()
	{
		if (standing)
			return;
		standing = true;
		standUntil = clock + StandSeconds;
		Vector2 at = GlobalPosition;
		float inner2 = ringRadius * ringRadius;
		EnemyRegistry.CollectNear(at, ringRadius, scratch);
		foreach (Enemy enemy in scratch)
		{
			if (pennedCount >= MaxPenned)
				break;
			if (IsInstanceValid(enemy) && !enemy.IsDying && at.DistanceSquaredTo(enemy.GlobalPosition) < inner2)
				penned[pennedCount++] = enemy;
		}
		scratch.Clear();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		// A crown whose ring never rose (every spot was water) lets go after the time it would have stood.
		if (!standing)
		{
			if (clock > StandSeconds + 1f)
				QueueFree();
			return;
		}
		if (clock >= standUntil)
		{
			QueueFree();
			return;
		}
		scan -= dt;
		if (scan > 0f)
			return;
		scan = ScanSeconds;
		Vector2 at = GlobalPosition;
		EnemyRegistry.CollectNear(at, ringRadius + wallHalf, scratch);
		foreach (Enemy enemy in scratch)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = at - enemy.GlobalPosition;
			float d = offset.Length();
			if (d < 1f || MathF.Abs(d - ringRadius) > wallHalf)
				continue;
			// Back to the side of the ice it started on.
			Vector2 inward = offset / d;
			enemy.ApplyKnockback((IsPenned(enemy) ? inward : -inward) * PenShove);
		}
		scratch.Clear();
	}

	/// <summary>
	/// True the first time a spike of this cast strikes <paramref name="enemy"/>: the crown's spikes stand
	/// close enough to overlap, and an enemy between two of them is struck once, not twice.
	/// </summary>
	public bool Claim(Enemy enemy)
	{
		for (int i = 0; i < struckCount; i++)
		{
			if (ReferenceEquals(struck[i], enemy))
				return false;
		}
		if (struckCount < MaxStruck)
			struck[struckCount++] = enemy;
		return true;
	}

	private bool IsPenned(Enemy enemy)
	{
		for (int i = 0; i < pennedCount; i++)
		{
			if (ReferenceEquals(penned[i], enemy))
				return true;
		}
		return false;
	}
}
