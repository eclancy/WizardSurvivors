using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Cataclysm, the Pyromancer's Apotheosis (issue #61). Verb: burn - the crowd is the fuel.
//
// Whatever Cinderbreath kills leaves a burning crater. Anything that dies inside a live crater, to
// any source, detonates and opens a crater of its own. In a dense wave the craters sustain each
// other and the ground the player is walking through is on fire; against a lone boss it does
// almost nothing, which is the intended asymmetry.
//
// Two bounds, because a chain reaction in a swarm is unbounded otherwise. The craters live in a
// fixed pool (the oldest is reused when it is full - the "pool the craters" note in #61), and new
// craters are QUEUED from the kill event and opened in _Process a few per frame, rather than opened
// inside the kill that caused them. Opening one deals damage, which kills, which would re-enter the
// handler, which is recursion with no floor.
public partial class CataclysmField : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxCraters = 24;
	private const float CraterSeconds = 6f;
	private const float CraterRadius = 56f;
	private const float DetonationRadius = 72f;
	private const float TickSeconds = 0.5f;
	private const int OpensPerFrame = 6;

	private struct Crater
	{
		public Vector2 At;
		public float Age;
		public uint Seed;
		public bool Live;
	}

	private readonly Crater[] craters = new Crater[MaxCraters];
	// Live slots ordered north to south each frame, so a nearer crater's fire stands in front.
	private readonly int[] order = new int[MaxCraters];
	private int orderCount;
	private readonly Queue<(Vector2 At, bool Detonate)> pending = new();
	private int nextSlot;
	private uint seedClock;
	private float tick;

	// A crater is a crater (CraterGround: a molten bowl with a lit rim, cracks and ejecta) filled with
	// fire out to CraterRadius (CraterFire), and a detonation is fire thrown out to exactly
	// DetonationRadius (ImpactDebris). It used to be a translucent disc with a rim at CraterRadius - an
	// area marker, gone 2026-10-23 - and then Ember Caps' lick of flame in a ring round a glowing dot.
	//
	// ON THE GROUND (2026-10-28). This node drew the whole crater at z 1, over every actor, so a crater
	// opened under the pack covered the pack and the wizard walking through it. The hole is now at
	// absolute z -15 and the fire in it at -14, both under the actors, as Meteor Swarm's are; only the
	// detonation's debris, which is in the air, goes over them (ImpactDebrisLayer, z 6).
	private const float BurnSeconds = CraterSeconds - 0.4f;
	private const float CoolSeconds = 0.6f;
	private const float DissolveSeconds = 0.6f;
	private static readonly ElementColors.Ramp FireRamp = ElementColors.GetRamp(Element.Fire);
	private Node2D ground;

	public override void _Ready()
	{
		ZIndex = CraterGround.FireZ;
		ZAsRelative = false;
		ground = new Node2D { Name = "Ground", ZIndex = CraterGround.GroundZ, ZAsRelative = false, Material = CraterGround.Material };
		AddChild(ground);
		ground.Draw += DrawGround;
		Player.EnemyKilled += OnEnemyKilled;
	}

	public override void _ExitTree()
	{
		Player.EnemyKilled -= OnEnemyKilled;
		if (ground != null && IsInstanceValid(ground))
			ground.Draw -= DrawGround;
	}

	private int Damage => Mathf.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(SpellData.CurrentLevel) ?? 4) * 0.6f));

	private void OnEnemyKilled(Enemy enemy, Vector2 at, SpellData source)
	{
		if (source != null && source.Id == SpellData?.Id)
		{
			pending.Enqueue((at, false));
			return;
		}
		if (InsideLiveCrater(at))
			pending.Enqueue((at, true));
	}

	private bool InsideLiveCrater(Vector2 at)
	{
		float r2 = CraterRadius * CraterRadius;
		for (int i = 0; i < MaxCraters; i++)
			if (craters[i].Live && craters[i].At.DistanceSquaredTo(at) <= r2)
				return true;
		return false;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		QueueRedraw();
		ground.QueueRedraw();

		for (int i = 0; i < MaxCraters; i++)
		{
			if (!craters[i].Live) continue;
			craters[i].Age += dt;
			if (craters[i].Age >= CraterSeconds) craters[i].Live = false;
		}

		for (int n = 0; n < OpensPerFrame && pending.Count > 0; n++)
		{
			var (at, detonate) = pending.Dequeue();
			craters[nextSlot] = new Crater { At = at, Age = 0f, Seed = ++seedClock * 2654435761u, Live = true };
			nextSlot = (nextSlot + 1) % MaxCraters;
			if (detonate)
			{
				HitWithin(at, DetonationRadius, Damage * 2);
				if (GetParent() is Node2D arena)
					ImpactDebrisLayer.Spawn(arena, at, DetonationRadius, FireRamp, Colors.White);
			}
		}

		// Insertion by y, as CraterLayer does: at most twenty-four, nearly sorted from the frame before.
		orderCount = 0;
		for (int i = 0; i < MaxCraters; i++)
		{
			if (!craters[i].Live) continue;
			int k = orderCount++;
			while (k > 0 && craters[order[k - 1]].At.Y > craters[i].At.Y)
			{
				order[k] = order[k - 1];
				k--;
			}
			order[k] = i;
		}

		tick -= dt;
		if (tick > 0f) return;
		tick = TickSeconds;
		for (int i = 0; i < MaxCraters; i++)
			if (craters[i].Live)
				HitWithin(craters[i].At, CraterRadius, Damage);
	}

	private void HitWithin(Vector2 at, float radius, int damage)
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef)) return;
		float r2 = radius * radius;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is Enemy e && IsInstanceValid(e) && !e.IsDying && e.GlobalPosition.DistanceSquaredTo(at) <= r2)
				PlayerRef.DealDamageToEnemy(e, damage, source: SpellData);
		}
	}

	// The holes, on the ground child: molten while the crater burns, cooling and dissolving as it ends.
	private void DrawGround()
	{
		for (int n = 0; n < orderCount; n++)
		{
			Crater c = craters[order[n]];
			if (!c.Live) continue;
			float heat = CraterGround.HeatAt(c.Age, BurnSeconds, CoolSeconds, c.Seed);
			float left = Mathf.Clamp((CraterSeconds - c.Age) / DissolveSeconds, 0f, 1f);
			CraterGround.Draw(ground, ground.ToLocal(c.At), CraterRadius, heat, c.Age, left);
		}
	}

	// The fire filling each crater to CraterRadius, the reach its ticks test: every crater's flames,
	// then every crater's embers, so the renderer batches them (CraterFire.DrawFire).
	public override void _Draw()
	{
		for (int n = 0; n < orderCount; n++)
		{
			Crater c = craters[order[n]];
			if (c.Live)
				CraterFire.DrawFire(this, ToLocal(c.At), CraterRadius, c.Age, BurnSeconds, c.Seed, Colors.White);
		}
		for (int n = 0; n < orderCount; n++)
		{
			Crater c = craters[order[n]];
			if (c.Live)
				CraterFire.DrawEmbers(this, ToLocal(c.At), CraterRadius, c.Age, BurnSeconds, c.Seed, Colors.White);
		}
	}
}
