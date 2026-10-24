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
		public bool Live;
	}

	private readonly Crater[] craters = new Crater[MaxCraters];
	private readonly Queue<(Vector2 At, bool Detonate)> pending = new();
	private int nextSlot;
	private float tick;
	private float flicker;

	private static readonly Color Ember = new Color(1.0f, 0.42f, 0.10f, 0.32f);
	private static readonly Color Rim = new Color(1.0f, 0.72f, 0.30f, 0.85f);

	public override void _Ready()
	{
		ZIndex = 1;
		Player.EnemyKilled += OnEnemyKilled;
	}

	public override void _ExitTree() => Player.EnemyKilled -= OnEnemyKilled;

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
		flicker += dt;
		QueueRedraw();

		for (int i = 0; i < MaxCraters; i++)
		{
			if (!craters[i].Live) continue;
			craters[i].Age += dt;
			if (craters[i].Age >= CraterSeconds) craters[i].Live = false;
		}

		for (int n = 0; n < OpensPerFrame && pending.Count > 0; n++)
		{
			var (at, detonate) = pending.Dequeue();
			craters[nextSlot] = new Crater { At = at, Age = 0f, Live = true };
			nextSlot = (nextSlot + 1) % MaxCraters;
			if (detonate)
				HitWithin(at, DetonationRadius, Damage * 2);
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

	public override void _Draw()
	{
		for (int i = 0; i < MaxCraters; i++)
		{
			if (!craters[i].Live) continue;
			float fade = 1f - craters[i].Age / CraterSeconds;
			Vector2 p = ToLocal(craters[i].At);
			float wobble = Mathf.Sin(flicker * 7f + i) * 2f;
			DrawCircle(p, CraterRadius, new Color(Ember, Ember.A * fade));
			DrawArc(p, CraterRadius + wobble, 0f, Mathf.Tau, 28, new Color(Rim, Rim.A * fade), 2.5f);
			DrawCircle(p, 6f + wobble, new Color(1f, 0.85f, 0.5f, 0.8f * fade));
		}
	}
}
