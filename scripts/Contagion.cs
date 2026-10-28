using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Contagion: a plague put on one enemy that jumps to everything near it when it dies
// (.ai/side-events.md). Kept by the lantern-tender in the Blighted Swamp.
//
// **The cadence nothing else has: it spreads on death, by itself.** The cast only ever touches one
// enemy. Everything after that is the swarm infecting itself - a carrier that dies in a pack seeds
// the whole pack, each of those seeds its neighbours, and the plague walks through the crowd for as
// long as the crowd stays packed. Against stragglers it does almost nothing, which is the point:
// it is a spell about density, where Frost Shard's on-kill burst is a spell about killing.
//
// **Pure Poison.** Poison had no pure spell, so it could never reach its capstone.
//
// Bounded twice, because a chain reaction in a swarm is unbounded otherwise: a cast can infect at
// most MaxInfected enemies in total, and a mark lapses after MarkSeconds if its carrier survives.
//
// **Marsh Fever** (the level 4 element path into Water, ElementPaths): the marks and the bursts of
// a jump are repainted in Water's ramp, and every infection drags its carrier (the rider, applied by
// Player.DealDamageToEnemy on the infecting hit).
public partial class Contagion : Node2D, IPaintsElementPath
{
	[Export] public float SpreadRadius { get; set; } = 95f;
	[Export] public float MarkSeconds { get; set; } = 6f;
	[Export] public int MaxInfected { get; set; } = 24;

	public int Damage { get; set; } = 6;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	// How much faster the poison runs (Hyper Acceleration): each tick bites PoisonRate times harder and
	// the poison runs out PoisonRate times sooner, so the dose is the same and a carrier dies sooner -
	// which is what makes the plague spread sooner. The mark, and so the spread on death, is unchanged.
	public float PoisonRate { get; set; } = 1f;

	// Carriers one cast may infect, each with its own plague (Infinite Horizon's +3 on the base one).
	public const int MaxCarriersPerCast = 6;

	private struct Mark
	{
		public Enemy Enemy;
		public Vector2 LastSeen;
		public float Remaining;
	}

	private readonly List<Mark> marks = new();
	private readonly List<Vector2> pendingSpreads = new();
	private readonly List<(Vector2 At, float Age, uint Seed)> bursts = new();
	private uint burstSeed;
	private readonly HashSet<ulong> everInfected = new();
	private float pollTimer;

	// Poison's hot tone for the carrier's mark, and its ramp for the spores a jump throws; an element
	// path repaints both in its own.
	private Color plague = new Color(0.62f, 0.86f, 0.30f, 0.9f);
	private ElementColors.Ramp spores = ElementColors.GetRamp(Element.Poison);

	// The spores a jump throws are in the air, over the bodies they land on; the carriers' marks are on
	// the floor at their feet, under them (DrawLayers). One node at z 1 drew both over every body.
	private Node2D air;

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.GroundTell;
		air = new Node2D { Name = "Spores", ZAsRelative = false, ZIndex = DrawLayers.Air };
		AddChild(air);
		air.Draw += DrawSpores;
		if (SpellData?.GetBranchElement() is Element branch)
		{
			ElementColors.Ramp ramp = ElementColors.GetRamp(branch);
			plague = new Color(ramp.Hot, 0.9f);
			spores = ramp;
		}
	}

	/// <summary>Infects the first carrier. Call once, after adding to the tree.</summary>
	public void InfectFirst(Enemy carrier) => Infect(carrier);

	private void Infect(Enemy enemy)
	{
		if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying || everInfected.Count >= MaxInfected)
			return;
		if (!everInfected.Add(enemy.GetInstanceId()))
			return;

		marks.Add(new Mark { Enemy = enemy, LastSeen = enemy.GlobalPosition, Remaining = MarkSeconds });
		// The poison carries the damage over time; the hit on infection is what credits the spell
		// in the per-spell damage table and makes the jump visible as a number.
		float rate = MathF.Max(0.25f, PoisonRate);
		enemy.ApplyPoison(Math.Max(1, Mathf.RoundToInt(Damage * rate)), MarkSeconds / rate);
		if (PlayerRef != null && IsInstanceValid(PlayerRef))
			PlayerRef.DealDamageToEnemy(enemy, Damage, source: SpellData);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		QueueRedraw();
		air?.QueueRedraw();

		for (int i = bursts.Count - 1; i >= 0; i--)
		{
			var b = bursts[i];
			b.Age += dt;
			if (b.Age > ImpactDebris.LifeSeconds) bursts.RemoveAt(i); else bursts[i] = b;
		}

		pollTimer -= dt;
		if (pollTimer <= 0f)
		{
			pollTimer = 0.1f;
			Poll(0.1f);
		}

		if (marks.Count == 0 && pendingSpreads.Count == 0 && bursts.Count == 0)
			QueueFree();
	}

	private void Poll(float step)
	{
		pendingSpreads.Clear();
		for (int i = marks.Count - 1; i >= 0; i--)
		{
			Mark mark = marks[i];
			bool dead = mark.Enemy == null || !IsInstanceValid(mark.Enemy) || mark.Enemy.IsDying;
			if (dead)
			{
				pendingSpreads.Add(mark.LastSeen);
				marks.RemoveAt(i);
				continue;
			}

			mark.LastSeen = mark.Enemy.GlobalPosition;
			mark.Remaining -= step;
			if (mark.Remaining <= 0f)
				marks.RemoveAt(i);
			else
				marks[i] = mark;
		}

		if (pendingSpreads.Count == 0)
			return;

		float radiusSquared = SpreadRadius * SpreadRadius;
		foreach (Vector2 origin in pendingSpreads)
		{
			bursts.Add((origin, 0f, ++burstSeed + (uint)(GetInstanceId() % 9973)));
			foreach (Node node in EnemyRegistry.Snapshot)
			{
				if (node is Enemy neighbour && origin.DistanceSquaredTo(neighbour.GlobalPosition) <= radiusSquared)
					Infect(neighbour);
			}
		}
	}

	public override void _Draw()
	{
		// Each carrier's mark at its feet: a status on one enemy, not an area, so it stays.
		foreach (Mark mark in marks)
		{
			Vector2 p = ToLocal(mark.LastSeen);
			DrawArc(p + new Vector2(0f, 10f), 9f, 0f, Mathf.Tau, 16, plague, 2f);
		}
	}

	// A jump: plague spores thrown out of the corpse to exactly SpreadRadius, the reach that was
	// infected. It was a ring opening to that radius - an area marker - and went (2026-10-23).
	private void DrawSpores()
	{
		int count = ImpactDebris.CountFor(SpreadRadius);
		foreach (var burst in bursts)
			ImpactDebris.Draw(air, ToLocal(burst.At), SpreadRadius, burst.Age, burst.Seed, spores, count, Colors.White);
	}
}
